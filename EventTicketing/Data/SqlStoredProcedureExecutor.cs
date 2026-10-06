using System.Data;
using Microsoft.Data.SqlClient;

namespace EventTicketing.Api.Data;

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
        await using var connection = new SqlConnection(_connectionString);

        await using var command = new SqlCommand(procedureName, connection)
        {
            CommandType = CommandType.StoredProcedure
        };

        configureParameters?.Invoke(command.Parameters);

        var returnParameter = command.Parameters.Add( "@ReturnValue", SqlDbType.Int);

        returnParameter.Direction = ParameterDirection.ReturnValue;

        await connection.OpenAsync(cancellationToken);

        T data;

        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            data = await readData( reader, cancellationToken);
        }

        // RETURN value is available after the reader is closed.
        var returnCode = (int)returnParameter.Value;

        return new StoredProcedureResult<T>
        {
            ReturnCode = returnCode,
            Data = data
        };
    }
    
}
