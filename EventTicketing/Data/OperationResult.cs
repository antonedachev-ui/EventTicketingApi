namespace EventTicketing.Api.Data
{
    // EventDataAccess turns a raw SQL return code into the enum for that operation, then
    // passes this result to the controller's HTTP mapping. Data may be absent for some outcomes.
    public sealed class OperationResult<TStatus, TData>
    where TStatus : Enum
    {
        public required TStatus Status { get; init; }
        public TData? Data { get; init; }
    }
}
