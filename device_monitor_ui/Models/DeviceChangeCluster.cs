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

    /// <summary>
    /// Generic device-class vocabulary used by Windows.
    /// If every word in a device name comes from this set, the name is Tier 2 (generic-descriptive).
    /// If at least one word is outside this set, the name is Tier 1 (branded/specific).
    /// </summary>
    private static readonly HashSet<string> GenericVocabulary = new(StringComparer.OrdinalIgnoreCase)
    {
        // Bus / protocol words
        "USB", "HID", "HID-compliant", "PCI", "PCI-E", "PCIe", "HDMI", "I2C",
        "Bluetooth", "Thunderbolt", "SATA", "NVMe", "SCSI", "IDE",
        // Device-type words
        "Device", "Controller", "Hub", "Root", "Host", "Port",
        "Keyboard", "Mouse", "Touchpad", "Trackpad", "Gamepad", "Joystick",
        "Audio", "Sound", "Speaker", "Microphone", "Headset",
        "Display", "Monitor", "Screen", "Video", "Graphics", "Adapter",
        "Camera", "Webcam", "Sensor", "Biometric", "Fingerprint",
        "Printer", "Scanner", "Fax",
        "Storage", "Drive", "Disk", "Volume", "Reader", "Card",
        "Network", "Ethernet", "Wireless", "LAN", "WAN", "Modem",
        "Serial", "Parallel", "Infrared", "IR",
        // Qualifier words
        "Input", "Output", "Composite", "Virtual", "Generic", "Standard",
        "System", "Consumer", "Control", "Mass", "Enhanced", "Compliant",
        "Compatible", "Integrated", "Embedded", "Internal", "External",
        "Vendor-defined", "Vendor", "Defined",
        // Prepositions / articles (may appear in names)
        "for", "with", "and", "the", "a", "an", "of", "on",
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

        // Split on spaces and check each word against the generic vocabulary.
        // If every word is generic → Tier 2 (generic-descriptive).
        // If any word is NOT generic → Tier 1 (branded/specific).
        var words = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        bool allGeneric = words.All(word => GenericVocabulary.Contains(word));

        return allGeneric ? 2 : 1;
    }
}
