namespace EventTicketing.Api.Data
{
    public sealed class StoredProcedureResult<T>
    {
        public int ReturnCode { get; init; }
        public required T Data { get; init; }
    }
}
