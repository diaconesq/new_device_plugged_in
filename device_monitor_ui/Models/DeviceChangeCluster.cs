using System.Collections.ObjectModel;

namespace DeviceMonitorUi.Models;

public class DeviceChangeCluster
{
    private static readonly HashSet<string> Tier3Names = new(StringComparer.OrdinalIgnoreCase)
    {
        "USB Composite Device",
        "USB Root Hub",
        "USB Hub",
        "Generic USB Hub",
        "HID-compliant device"
    };

    public DateTime StartTime { get; init; }
    public DateTime LastEventTime { get; set; }
    public ObservableCollection<DeviceChangeEvent> Events { get; } = new();

    public int TotalCount => Events.Count;
    public int AddedCount => Events.Count(e => e.Kind == ChangeKind.Added);
    public int RemovedCount => Events.Count(e => e.Kind == ChangeKind.Removed);
    public bool IsAllAdded => AddedCount > 0 && RemovedCount == 0;
    public bool IsAllRemoved => RemovedCount > 0 && AddedCount == 0;
    public bool IsMixed => AddedCount > 0 && RemovedCount > 0;
    public string BreakdownText => $"+{AddedCount} / -{RemovedCount}";
    public string SummaryLabel => GetBestName();

    private string GetBestName()
    {
        string bestName = "Multiple devices";
        int bestTier = 4;

        foreach (var deviceEvent in Events)
        {
            int tier = ClassifyName(deviceEvent.Name);
            if (tier < bestTier)
            {
                bestTier = tier;
                bestName = deviceEvent.Name;
            }
        }

        return bestTier >= 4 ? "Multiple devices" : bestName;
    }

    internal static int ClassifyName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Equals("Unknown", StringComparison.OrdinalIgnoreCase))
        {
            return 4;
        }

        if (Tier3Names.Contains(name))
        {
            return 3;
        }

        if (name.StartsWith("HID-compliant ", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("USB Mass Storage Device", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("USB Input Device", StringComparison.OrdinalIgnoreCase))
        {
            return 2;
        }

        return 1;
    }
}
