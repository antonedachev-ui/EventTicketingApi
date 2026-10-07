using System.Data;
using Microsoft.Data.SqlClient;

namespace EventTicketing.Api.Data;

// Runs a stored procedure and returns its result-set data alongside its raw SQL RETURN code.
// EventDataAccess interprets that code for the specific operation.
public sealed class SqlStoredProcedureExecutor : ISqlStoredProcedureExecutor
{

    private readonly string _connectionString;

    public SqlStoredProcedureExecutor(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("EventTicketing")
            ?? throw new InvalidOperationException("Connection string 'EventTicketing' is not configured.");
    }

    public async Task<StoredProcedureResult<T>> ExecuteAsync<T>(
        string procedureName,
        Action<SqlParameterCollection>? configureParameters,
        Func<SqlDataReader, CancellationToken, Task<T>> readData,
        CancellationToken cancellationToken = default)
    {
        // Each call owns its connection and command; the caller supplies only parameters and a reader mapper.
        await using var connection = new SqlConnection(_connectionString);

        await using var command = new SqlCommand(procedureName, connection)
        {
            CommandType = CommandType.StoredProcedure
        };

        configureParameters?.Invoke(command.Parameters);

        // SQL RETURN is separate from any rows or SELECT result sets produced by the procedure.
        var returnParameter = command.Parameters.Add( "@ReturnValue", SqlDbType.Int);

        returnParameter.Direction = ParameterDirection.ReturnValue;

        await connection.OpenAsync(cancellationToken);

        T data;

        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            // The mapper must read every result set it needs while the reader is open.
            data = await readData( reader, cancellationToken);
        }

        // SqlClient populates the RETURN parameter only after the reader is closed.
        var returnCode = (int)returnParameter.Value;

        return new StoredProcedureResult<T>
        {
            ReturnCode = returnCode,
            Data = data
        };
    }

}
