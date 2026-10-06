using Microsoft.Data.SqlClient;

namespace EventTicketing.Api.Data;

public interface ISqlStoredProcedureExecutor
{
    Task<StoredProcedureResult<T>> ExecuteAsync<T>(
        string procedureName,
        Action<SqlParameterCollection>? configureParameters,
        Func<SqlDataReader, CancellationToken, Task<T>> readData,
        CancellationToken cancellationToken = default);
}
