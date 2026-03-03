using System.Collections.Concurrent;
using System.Diagnostics;
using System.Management;
using System.Runtime.Versioning;
using DeviceMonitorUi.Models;

namespace DeviceMonitorUi.Services;

[SupportedOSPlatform("windows")]
public class DeviceWatcherService : IDisposable
{
    private const string CreationQuery =
        "SELECT * FROM __InstanceCreationEvent WITHIN 1 WHERE TargetInstance ISA 'Win32_PnPEntity'";
    private const string DeletionQuery =
        "SELECT * FROM __InstanceDeletionEvent WITHIN 1 WHERE TargetInstance ISA 'Win32_PnPEntity'";

    private readonly ConcurrentDictionary<string, bool> _trackedDevices = new();
    private ManagementEventWatcher? _creationWatcher;
    private ManagementEventWatcher? _deletionWatcher;

    public event Action<DeviceChangeEvent>? DeviceChanged;

    public void Start()
    {
        ScanInitialDevices();

        _creationWatcher = new ManagementEventWatcher(new WqlEventQuery(CreationQuery));
        _creationWatcher.EventArrived += OnDeviceCreated;
        _creationWatcher.Start();

        _deletionWatcher = new ManagementEventWatcher(new WqlEventQuery(DeletionQuery));
        _deletionWatcher.EventArrived += OnDeviceRemoved;
        _deletionWatcher.Start();
    }

    private void ScanInitialDevices()
    {
        using var searcher = new ManagementObjectSearcher("SELECT DeviceID FROM Win32_PnPEntity");
        using var results = searcher.Get();

        foreach (ManagementObject device in results)
        {
            using (device)
            {
                var deviceId = device["DeviceID"]?.ToString();
                if (!string.IsNullOrEmpty(deviceId))
                {
                    _trackedDevices.TryAdd(deviceId, true);
                }
            }
        }
    }

    private void OnDeviceCreated(object sender, EventArrivedEventArgs args)
    {
        try
        {
            var target = (ManagementBaseObject)args.NewEvent["TargetInstance"];
            var deviceId = target["DeviceID"]?.ToString();
            if (string.IsNullOrEmpty(deviceId) || !_trackedDevices.TryAdd(deviceId, true))
            {
                return;
            }

            var info = GetDeviceDetails(deviceId);
            DeviceChanged?.Invoke(new DeviceChangeEvent
            {
                Timestamp = DateTime.Now,
                Kind = ChangeKind.Added,
                Name = info.Name,
                DeviceId = info.DeviceId,
                Manufacturer = info.Manufacturer,
                Description = info.Description
            });
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"Device creation event parse failure: {ex}");
        }
    }

    private void OnDeviceRemoved(object sender, EventArrivedEventArgs args)
    {
        try
        {
            var target = (ManagementBaseObject)args.NewEvent["TargetInstance"];
            var deviceId = target["DeviceID"]?.ToString();
            if (string.IsNullOrEmpty(deviceId))
            {
                return;
            }

            var name = target["Name"]?.ToString() ?? "Unknown";
            _trackedDevices.TryRemove(deviceId, out _);

            DeviceChanged?.Invoke(new DeviceChangeEvent
            {
                Timestamp = DateTime.Now,
                Kind = ChangeKind.Removed,
                Name = name,
                DeviceId = deviceId,
                Manufacturer = string.Empty,
                Description = string.Empty
            });
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"Device removal event parse failure: {ex}");
        }
    }

    private static (string Name, string DeviceId, string Manufacturer, string Description) GetDeviceDetails(string deviceId)
    {
        var escapedDeviceId = deviceId
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("'", "\\'", StringComparison.Ordinal);
        using var searcher =
            new ManagementObjectSearcher($"SELECT * FROM Win32_PnPEntity WHERE DeviceID = '{escapedDeviceId}'");
        using var results = searcher.Get();

        foreach (ManagementObject device in results)
        {
            using (device)
            {
                return (
                    Name: device["Name"]?.ToString() ?? "Unknown",
                    DeviceId: deviceId,
                    Manufacturer: device["Manufacturer"]?.ToString() ?? string.Empty,
                    Description: device["Description"]?.ToString() ?? string.Empty
                );
            }
        }

        return ("Unknown", deviceId, string.Empty, string.Empty);
    }

    public void Dispose()
    {
        if (_creationWatcher is not null)
        {
            _creationWatcher.EventArrived -= OnDeviceCreated;
            _creationWatcher.Stop();
            _creationWatcher.Dispose();
            _creationWatcher = null;
        }

        if (_deletionWatcher is not null)
        {
            _deletionWatcher.EventArrived -= OnDeviceRemoved;
            _deletionWatcher.Stop();
            _deletionWatcher.Dispose();
            _deletionWatcher = null;
        }
    }
}
