namespace slskd.Telemetry;

public record ErrorSummary
{
    public string Exception { get; init; }
    public long Count { get; init; }
}