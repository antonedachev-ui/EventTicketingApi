namespace EventTicketing.Api.Data
{
    public sealed class OperationResult<TStatus, TData>
    where TStatus : Enum
    {
        public required TStatus Status { get; init; }
        public TData? Data { get; init; }
    }
}
