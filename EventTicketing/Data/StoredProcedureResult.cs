namespace EventTicketing.Api.Data
{
    // Raw executor output: Data comes from SELECT result sets, while ReturnCode comes from SQL RETURN.
    // Data may itself be nullable when a procedure returns no row for an expected outcome.
    public sealed class StoredProcedureResult<T>
    {
        public int ReturnCode { get; init; }
        public required T Data { get; init; }
    }
}
