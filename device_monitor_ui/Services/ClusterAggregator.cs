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
                NewClusterCreated?.Invoke(_currentCluster);
                return;
            }

            _currentCluster.LastEventTime = deviceEvent.Timestamp;
            _currentCluster.Events.Add(deviceEvent);
            EventAppendedToCluster?.Invoke(_currentCluster, deviceEvent);
        }
    }

    public void Reset()
    {
        lock (_lock)
        {
            _currentCluster = null;
        }
    }
}
