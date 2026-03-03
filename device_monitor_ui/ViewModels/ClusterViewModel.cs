using System.Collections.ObjectModel;
using System.ComponentModel;
using DeviceMonitorUi.Models;

namespace DeviceMonitorUi.ViewModels;

public class ClusterViewModel : INotifyPropertyChanged
{
    private readonly DeviceChangeCluster _cluster;

    public ClusterViewModel(DeviceChangeCluster cluster)
    {
        _cluster = cluster;
        foreach (var deviceEvent in cluster.Events)
        {
            Children.Add(new EventViewModel(deviceEvent));
        }
    }

    public ObservableCollection<EventViewModel> Children { get; } = new();
    public bool IsSingleEvent => _cluster.TotalCount == 1;
    public bool IsMultiEvent => _cluster.TotalCount > 1;
    public string Icon => _cluster.IsAllAdded ? "+" : _cluster.IsAllRemoved ? "-" : "~";
    public string IconColor => _cluster.IsAllAdded ? "Green" : _cluster.IsAllRemoved ? "Red" : "Gray";
    public string Timestamp => _cluster.StartTime.ToString("HH:mm:ss.fff");
    public string TotalChanges => _cluster.TotalCount == 1 ? "1 change" : $"{_cluster.TotalCount} changes";
    public string Breakdown => _cluster.BreakdownText;
    public string SummaryLabel => _cluster.SummaryLabel;
    public string SingleName => _cluster.Events.FirstOrDefault()?.Name ?? string.Empty;
    public string SingleDeviceId => _cluster.Events.FirstOrDefault()?.DeviceId ?? string.Empty;
    public string SingleManufacturer => _cluster.Events.FirstOrDefault()?.Manufacturer ?? string.Empty;
    public string SingleDescription => _cluster.Events.FirstOrDefault()?.Description ?? string.Empty;

    public event PropertyChangedEventHandler? PropertyChanged;

    public void AddEvent(DeviceChangeEvent deviceEvent)
    {
        Children.Add(new EventViewModel(deviceEvent));
        NotifyChanged();
    }

    private void NotifyChanged()
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSingleEvent)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsMultiEvent)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Icon)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IconColor)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TotalChanges)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Breakdown)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SummaryLabel)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SingleName)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SingleDeviceId)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SingleManufacturer)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SingleDescription)));
    }
}
