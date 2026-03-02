using System.Collections.Concurrent;
using System.Management;
using System.Runtime.Versioning;

[assembly: SupportedOSPlatform("windows")]

// Constants
const int SeparatorWidth = 100;
const int SleepIntervalMs = 1000;
const string TimestampFormat = "yyyy-MM-dd HH:mm:ss.fff";
const string DeviceCreationQuery = "SELECT * FROM __InstanceCreationEvent WITHIN 1 WHERE TargetInstance ISA 'Win32_PnPEntity'";
const string DeviceDeletionQuery = "SELECT * FROM __InstanceDeletionEvent WITHIN 1 WHERE TargetInstance ISA 'Win32_PnPEntity'";

// Thread-safe collection for tracking devices
var trackedDevices = new ConcurrentDictionary<string, bool>();

// Set up cancellation for graceful shutdown
using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (sender, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};

try
{
    // Get initial list of devices
    Console.Write("Scanning initial devices... ");
    var initialDevices = GetCurrentDevices();
    foreach (var deviceId in initialDevices)
    {
        trackedDevices.TryAdd(deviceId, true);
    }
    Console.WriteLine($"found {trackedDevices.Count} devices.\n");

    // Set up WMI event watchers with proper disposal
    using var creationWatcher = new ManagementEventWatcher(new WqlEventQuery(DeviceCreationQuery));
    using var deletionWatcher = new ManagementEventWatcher(new WqlEventQuery(DeviceDeletionQuery));
    
    creationWatcher.EventArrived += (sender, args) =>
    {
        try
        {
            var targetInstance = (ManagementBaseObject)args.NewEvent["TargetInstance"];
            string? deviceId = targetInstance["DeviceID"]?.ToString();
            
            if (!string.IsNullOrEmpty(deviceId) && trackedDevices.TryAdd(deviceId, true))
            {
                var deviceInfo = GetDeviceDetails(deviceId);
                PrintDeviceEvent("NEW DEVICE DETECTED", deviceInfo);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error processing device creation: {ex.Message}");
        }
    };
    
    deletionWatcher.EventArrived += (sender, args) =>
    {
        try
        {
            var targetInstance = (ManagementBaseObject)args.NewEvent["TargetInstance"];
            string? deviceId = targetInstance["DeviceID"]?.ToString();
            string? deviceName = targetInstance["Name"]?.ToString() ?? "Unknown";
            
            if (!string.IsNullOrEmpty(deviceId))
            {
                trackedDevices.TryRemove(deviceId, out _);
                
                var deviceInfo = new DeviceInfo
                {
                    Name = deviceName,
                    DeviceId = deviceId
                };
                PrintDeviceEvent("DEVICE REMOVED", deviceInfo, isRemoval: true);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error processing device removal: {ex.Message}");
        }
    };

    // Start watching
    creationWatcher.Start();
    deletionWatcher.Start();

    Console.WriteLine("Monitoring device changes. (Press Ctrl+C to exit)\n");

    // Keep the program running until cancelled
    while (!cts.Token.IsCancellationRequested)
    {
        Thread.Sleep(SleepIntervalMs);
    }
}
catch (OperationCanceledException)
{
    // Expected when user cancels
}
catch (Exception ex)
{
    Console.WriteLine($"\nFatal error: {ex.Message}");
}

// Helper functions
static void PrintDeviceEvent(string eventType, DeviceInfo deviceInfo, bool isRemoval = false)
{
    Console.WriteLine($"\n*** {eventType} at {DateTime.Now.ToString(TimestampFormat)} ***");
    Console.WriteLine(new string('=', SeparatorWidth));
    Console.WriteLine($"Name:         {deviceInfo.Name}");
    Console.WriteLine($"Device ID:    {deviceInfo.DeviceId}");
    
    if (!isRemoval)
    {
        Console.WriteLine($"Status:       {deviceInfo.Status}");
        Console.WriteLine($"Manufacturer: {deviceInfo.Manufacturer}");
        Console.WriteLine($"Description:  {deviceInfo.Description}");
    }
    
    Console.WriteLine(new string('=', SeparatorWidth));
}

static HashSet<string> GetCurrentDevices()
{
    var devices = new HashSet<string>();
    
    using var searcher = new ManagementObjectSearcher("SELECT DeviceID FROM Win32_PnPEntity");
    using var results = searcher.Get();
    
    foreach (ManagementObject device in results)
    {
        using (device)
        {
            string? deviceId = device["DeviceID"]?.ToString();
            if (!string.IsNullOrEmpty(deviceId))
            {
                devices.Add(deviceId);
            }
        }
    }
    
    return devices;
}

static DeviceInfo GetDeviceDetails(string deviceId)
{
    var escapedDeviceId = deviceId.Replace("\\", "\\\\");
    
    using var searcher = new ManagementObjectSearcher(
        $"SELECT * FROM Win32_PnPEntity WHERE DeviceID = '{escapedDeviceId}'");
    using var results = searcher.Get();
    
    foreach (ManagementObject device in results)
    {
        using (device)
        {
            return new DeviceInfo
            {
                Name = device["Name"]?.ToString() ?? "Unknown",
                DeviceId = deviceId,
                Status = device["Status"]?.ToString() ?? "N/A",
                Manufacturer = device["Manufacturer"]?.ToString() ?? "N/A",
                Description = device["Description"]?.ToString() ?? "N/A"
            };
        }
    }
    
    return new DeviceInfo { DeviceId = deviceId };
}

// Record to store device information
record DeviceInfo
{
    public string Name { get; init; } = "Unknown";
    public string DeviceId { get; init; } = "";
    public string Status { get; init; } = "N/A";
    public string Manufacturer { get; init; } = "N/A";
    public string Description { get; init; } = "N/A";
}
