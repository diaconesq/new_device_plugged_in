namespace DeviceMonitorUi.Models;

public record DeviceChangeEvent
{
    public required DateTime Timestamp { get; init; }
    public required ChangeKind Kind { get; init; }
    public required string Name { get; init; }
    public required string DeviceId { get; init; }
    public string Manufacturer { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
}
