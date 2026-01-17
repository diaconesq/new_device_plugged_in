using System.Management;

try
{
    // Get initial list of devices
    Console.Write("Scanning initial devices... ");
    var devicesBefore = GetCurrentDevices();
    Console.WriteLine($"found {devicesBefore.Count} devices.\n");

    // Set up WMI event watcher for device creation
    var creationWatcher = new ManagementEventWatcher(new WqlEventQuery("SELECT * FROM __InstanceCreationEvent WITHIN 1 WHERE TargetInstance ISA 'Win32_PnPEntity'"));
    
    creationWatcher.EventArrived += (sender, args) =>
    {
        try
        {
            var targetInstance = (ManagementBaseObject)args.NewEvent["TargetInstance"];
            string? deviceId = targetInstance["DeviceID"]?.ToString();
            
            if (!string.IsNullOrEmpty(deviceId) && !devicesBefore.Contains(deviceId))
            {
                Console.WriteLine($"\n*** NEW DEVICE DETECTED at {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} ***");
                Console.WriteLine(new string('=', 100));
                
                var deviceInfo = GetDeviceDetails(deviceId);
                Console.WriteLine($"Name:         {deviceInfo.Name}");
                Console.WriteLine($"Device ID:    {deviceInfo.DeviceId}");
                Console.WriteLine($"Status:       {deviceInfo.Status}");
                Console.WriteLine($"Manufacturer: {deviceInfo.Manufacturer}");
                Console.WriteLine($"Description:  {deviceInfo.Description}");
                Console.WriteLine(new string('=', 100));
                
                // Add to the list so we don't report it again
                devicesBefore.Add(deviceId);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error processing device creation event: {ex.Message}");
        }
    };

    // Set up WMI event watcher for device deletion
    var deletionWatcher = new ManagementEventWatcher(new WqlEventQuery("SELECT * FROM __InstanceDeletionEvent WITHIN 1 WHERE TargetInstance ISA 'Win32_PnPEntity'"));
    
    deletionWatcher.EventArrived += (sender, args) =>
    {
        try
        {
            var targetInstance = (ManagementBaseObject)args.NewEvent["TargetInstance"];
            string? deviceId = targetInstance["DeviceID"]?.ToString();
            string? deviceName = targetInstance["Name"]?.ToString() ?? "Unknown";
            
            if (!string.IsNullOrEmpty(deviceId))
            {
                Console.WriteLine($"\n*** DEVICE REMOVED at {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} ***");
                Console.WriteLine(new string('=', 100));
                Console.WriteLine($"Name:         {deviceName}");
                Console.WriteLine($"Device ID:    {deviceId}");
                Console.WriteLine(new string('=', 100));
                
                // Remove from the list if it was there
                devicesBefore.Remove(deviceId);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error processing device deletion event: {ex.Message}");
        }
    };

    // Start watching
    creationWatcher.Start();
    deletionWatcher.Start();

    Console.WriteLine("Please plug in or remove devices. (Press Ctrl+C to exit!!)\n");

    // Keep the program running
    while (true)
    {
        Thread.Sleep(1000);
    }
}
catch (Exception ex)
{
    Console.WriteLine($"Error: {ex.Message}");
    Console.WriteLine($"Stack trace: {ex.StackTrace}");
}

// Function to get list of current device IDs
static HashSet<string> GetCurrentDevices()
{
    var devices = new HashSet<string>();
    
    ManagementObjectSearcher searcher = new ManagementObjectSearcher(
        "SELECT DeviceID FROM Win32_PnPEntity");
    
    foreach (ManagementObject device in searcher.Get())
    {
        string? deviceId = device["DeviceID"]?.ToString();
        if (!string.IsNullOrEmpty(deviceId))
        {
            devices.Add(deviceId);
        }
    }
    
    return devices;
}

// Function to get detailed information about a specific device
static DeviceInfo GetDeviceDetails(string deviceId)
{
    ManagementObjectSearcher searcher = new ManagementObjectSearcher(
        $"SELECT * FROM Win32_PnPEntity WHERE DeviceID = '{deviceId.Replace("\\", "\\\\")}'");
    
    foreach (ManagementObject device in searcher.Get())
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
    
    return new DeviceInfo { DeviceId = deviceId };
}

// Class to store device information
class DeviceInfo
{
    public string Name { get; set; } = "Unknown";
    public string DeviceId { get; set; } = "";
    public string Status { get; set; } = "N/A";
    public string Manufacturer { get; set; } = "N/A";
    public string Description { get; set; } = "N/A";
}
