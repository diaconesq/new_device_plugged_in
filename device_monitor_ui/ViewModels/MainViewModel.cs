using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using DeviceMonitorUi.Infrastructure;
using DeviceMonitorUi.Models;
using DeviceMonitorUi.Services;

namespace DeviceMonitorUi.ViewModels;

public class MainViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly DeviceWatcherService _watcher;
    private readonly ClusterAggregator _aggregator;
    private readonly Dictionary<DeviceChangeCluster, ClusterViewModel> _clusterMap = new();

    public MainViewModel()
    {
        _watcher = new DeviceWatcherService();
        _aggregator = new ClusterAggregator();
        ClearCommand = new RelayCommand(ExecuteClear);

        Clusters.CollectionChanged += OnClustersCollectionChanged;
        _watcher.DeviceChanged += OnDeviceChanged;
        _aggregator.NewClusterCreated += OnNewCluster;
        _aggregator.EventAppendedToCluster += OnEventAppendedToCluster;

        _watcher.Start();
    }

    public ObservableCollection<ClusterViewModel> Clusters { get; } = new();
    public RelayCommand ClearCommand { get; }
    public bool IsEmpty => Clusters.Count == 0;

    public event PropertyChangedEventHandler? PropertyChanged;

    public void Dispose()
    {
        Clusters.CollectionChanged -= OnClustersCollectionChanged;
        _watcher.DeviceChanged -= OnDeviceChanged;
        _aggregator.NewClusterCreated -= OnNewCluster;
        _aggregator.EventAppendedToCluster -= OnEventAppendedToCluster;
        _watcher.Dispose();
    }

    private void OnDeviceChanged(DeviceChangeEvent deviceEvent)
    {
        _aggregator.ProcessEvent(deviceEvent);
    }

    private void OnNewCluster(DeviceChangeCluster cluster)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            var clusterVm = new ClusterViewModel(cluster);
            Clusters.Add(clusterVm);
            _clusterMap[cluster] = clusterVm;
        });
    }

    private void OnEventAppendedToCluster(DeviceChangeCluster cluster, DeviceChangeEvent deviceEvent)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            if (_clusterMap.TryGetValue(cluster, out var clusterVm))
            {
                clusterVm.AddEvent(deviceEvent);
            }
        });
    }

    private void OnClustersCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsEmpty)));
    }

    private void ExecuteClear()
    {
        Clusters.Clear();
        _clusterMap.Clear();
        _aggregator.Reset();
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsEmpty)));
    }
}
