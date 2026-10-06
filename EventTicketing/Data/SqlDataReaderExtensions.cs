using Microsoft.Data.SqlClient;

namespace EventTicketing.Api.Data
{
    public static class SqlDataReaderExtensions
    {
        public static async Task<List<T>> ReadListAsync<T>(this SqlDataReader reader, Func<SqlDataReader, T> map, CancellationToken cancellationToken = default)
        {
            var result = new List<T>();

            while (await reader.ReadAsync(cancellationToken))
            {
                result.Add(map(reader));
            }

            return result;
        }
    }
}
