using DeviceMonitorUi.Models;

namespace DeviceMonitorUi.Services;

public class ClusterAggregator
{
    private static readonly TimeSpan ClusterThreshold = TimeSpan.FromSeconds(3);
    private readonly object _lock = new();
    private DeviceChangeCluster? _currentCluster;

    public event Action<DeviceChangeCluster>? NewClusterCreated;
    public event Action<DeviceChangeCluster, DeviceChangeEvent>? EventAppendedToCluster;

    public void ProcessEvent(DeviceChangeEvent deviceEvent)
    {
        DeviceChangeCluster? newCluster = null;
        DeviceChangeCluster? appendedCluster = null;

        lock (_lock)
        {
            if (_currentCluster is null ||
                (deviceEvent.Timestamp - _currentCluster.LastEventTime) > ClusterThreshold)
            {
                _currentCluster = new DeviceChangeCluster
                {
                    StartTime = deviceEvent.Timestamp,
                    LastEventTime = deviceEvent.Timestamp
                };

                _currentCluster.Events.Add(deviceEvent);
                newCluster = _currentCluster;
            }
            else
            {
                _currentCluster.LastEventTime = deviceEvent.Timestamp;
                _currentCluster.Events.Add(deviceEvent);
                appendedCluster = _currentCluster;
            }
        }

        // Fire events outside the lock to avoid deadlock with UI dispatcher
        if (newCluster is not null)
            NewClusterCreated?.Invoke(newCluster);
        else if (appendedCluster is not null)
            EventAppendedToCluster?.Invoke(appendedCluster, deviceEvent);
    }

    public void Reset()
    {
        lock (_lock)
        {
            _currentCluster = null;
        }
    }
}
