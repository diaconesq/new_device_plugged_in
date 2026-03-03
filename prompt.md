# Device Monitor UI — Implementation Plan

> Step-by-step implementation instructions for an LLM agent.
> Each step is **self-contained**: it lists every file to create/edit, exact content expectations, and a verification check.
> Steps must be executed in order. Each step builds on the previous.
>
> Reference: `spec.md` in the workspace root is the source of truth for all requirements.
>
> **After each step passes verification, commit the changes to Git** with a concise message describing what was done in that step.
>
> **Before committing each step**, perform an adversarial code review of the code created/changed in that step:
> 1. Critique the code for problems with **single responsibility, SOLID principles, naming, error handling, correctness, and readability**.
> 2. List all findings.
> 3. Implement **only** the changes that make a **relevant difference to code correctness and readability**. Skip nitpicks, stylistic preferences, and over-engineering suggestions that add complexity without clear benefit.
> 4. Then commit.

---

## Prerequisites

- Workspace root: `d:\code\new_device_plugged_in`
- Existing CLI project: `list_new_devices/` (net10.0, System.Management 10.0.2)
- Solution file: `list_new_devices.slnx`
- Target framework for new project: `net10.0-windows` (WPF requires the `-windows` TFM)

---

## Step 1 — Scaffold the WPF project

### Goal
Create a new WPF application project `device_monitor_ui` and add it to the existing solution.

### Instructions

1. Run in terminal:
   ```powershell
   dotnet new wpf -n device_monitor_ui -o device_monitor_ui --framework net10.0
   ```

2. Add the `System.Management` NuGet package:
   ```powershell
   dotnet add device_monitor_ui/device_monitor_ui.csproj package System.Management
   ```

3. Edit `device_monitor_ui/device_monitor_ui.csproj` — ensure these properties exist in the main `<PropertyGroup>`:
   ```xml
   <TargetFramework>net10.0-windows</TargetFramework>
   <UseWPF>true</UseWPF>
   <Nullable>enable</Nullable>
   <ImplicitUsings>enable</ImplicitUsings>
   <RuntimeIdentifier>win-x64</RuntimeIdentifier>
   ```

4. Add the project to the solution file `list_new_devices.slnx`:
   ```xml
   <Solution>
     <Project Path="list_new_devices/list_new_devices.csproj" />
     <Project Path="device_monitor_ui/device_monitor_ui.csproj" />
   </Solution>
   ```

5. Create the folder structure inside `device_monitor_ui/`:
   ```
   Models/
   Services/
   ViewModels/
   Infrastructure/
   ```

### Verification
- `dotnet build device_monitor_ui/device_monitor_ui.csproj` succeeds.
- The default WPF window opens when running the project.

---

## Step 2 — Infrastructure: RelayCommand

### Goal
Create a minimal `ICommand` implementation for MVVM data binding.

### Instructions

Create file `device_monitor_ui/Infrastructure/RelayCommand.cs`:

```csharp
using System.Windows.Input;

namespace DeviceMonitorUi.Infrastructure;

public class RelayCommand : ICommand
{
    private readonly Action _execute;
    private readonly Func<bool>? _canExecute;

    public RelayCommand(Action execute, Func<bool>? canExecute = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }

    public bool CanExecute(object? parameter) => _canExecute?.Invoke() ?? true;
    public void Execute(object? parameter) => _execute();
}
```

### Verification
- Project builds without errors.

---

## Step 3 — Models: ChangeKind enum and DeviceChangeEvent

### Goal
Define the data model for a single raw device event.

### Instructions

Create file `device_monitor_ui/Models/ChangeKind.cs`:

```csharp
namespace DeviceMonitorUi.Models;

public enum ChangeKind
{
    Added,
    Removed
}
```

Create file `device_monitor_ui/Models/DeviceChangeEvent.cs`:

```csharp
namespace DeviceMonitorUi.Models;

public record DeviceChangeEvent
{
    public required DateTime Timestamp { get; init; }
    public required ChangeKind Kind { get; init; }
    public required string Name { get; init; }
    public required string DeviceId { get; init; }
    public string Manufacturer { get; init; } = "";
    public string Description { get; init; } = "";
}
```

### Verification
- Project builds without errors.

---

## Step 4 — Models: DeviceChangeCluster

### Goal
Define the cluster model that holds a group of events and exposes computed summary properties.

### Instructions

Create file `device_monitor_ui/Models/DeviceChangeCluster.cs`:

```csharp
using System.Collections.ObjectModel;

namespace DeviceMonitorUi.Models;

public class DeviceChangeCluster
{
    public DateTime StartTime { get; init; }
    public DateTime LastEventTime { get; set; }
    public ObservableCollection<DeviceChangeEvent> Events { get; } = new();

    public int TotalCount => Events.Count;
    public int AddedCount => Events.Count(e => e.Kind == ChangeKind.Added);
    public int RemovedCount => Events.Count(e => e.Kind == ChangeKind.Removed);

    public bool IsAllAdded => RemovedCount == 0;
    public bool IsAllRemoved => AddedCount == 0;
    public bool IsMixed => AddedCount > 0 && RemovedCount > 0;

    public string BreakdownText => $"+{AddedCount} / -{RemovedCount}";

    public string SummaryLabel => GetBestName();

    /// <summary>
    /// 4-tier name ranking (spec §6a):
    ///   Tier 3 (worst usable): exact match in opaque blocklist
    ///   Tier 2: generic-but-descriptive (HID-compliant mouse, USB Mass Storage, etc.)
    ///   Tier 1: specific product/brand name (anything else non-empty/non-unknown)
    ///   Tier 4 (never use): "Unknown" or empty
    /// Pick the highest-tier name across all events. Same tier → first arrival wins.
    /// If only Tier 4, return "Multiple devices".
    /// </summary>
    private string GetBestName()
    {
        string bestName = "Multiple devices";
        int bestTier = 4;

        foreach (var e in Events)
        {
            int tier = ClassifyName(e.Name);
            if (tier < bestTier)
            {
                bestTier = tier;
                bestName = e.Name;
            }
        }

        return bestTier >= 4 ? "Multiple devices" : bestName;
    }

    private static readonly HashSet<string> Tier3Names = new(StringComparer.OrdinalIgnoreCase)
    {
        "USB Composite Device",
        "USB Root Hub",
        "USB Hub",
        "Generic USB Hub",
        "HID-compliant device"
    };

    internal static int ClassifyName(string name)
    {
        if (string.IsNullOrWhiteSpace(name) ||
            name.Equals("Unknown", StringComparison.OrdinalIgnoreCase))
            return 4;

        if (Tier3Names.Contains(name))
            return 3;

        // Tier 2: starts with known generic-but-descriptive prefixes
        if (name.StartsWith("HID-compliant", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("USB Mass Storage Device", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("USB Input Device", StringComparison.OrdinalIgnoreCase))
            return 2;

        // Tier 1: everything else is a specific/branded name
        return 1;
    }
}
```

### Verification
- Project builds without errors.

---

## Step 5 — Services: ClusterAggregator

### Goal
Implement the chained time-based clustering logic (spec §5). This is pure logic with no UI dependency, making it unit-testable.

### Instructions

Create file `device_monitor_ui/Services/ClusterAggregator.cs`:

```csharp
namespace DeviceMonitorUi.Services;

using DeviceMonitorUi.Models;

public class ClusterAggregator
{
    private static readonly TimeSpan ClusterThreshold = TimeSpan.FromSeconds(3);

    private DeviceChangeCluster? _currentCluster;
    private readonly object _lock = new();

    /// <summary>
    /// Raised when a new cluster is created and should be added to the UI list.
    /// </summary>
    public event Action<DeviceChangeCluster>? NewClusterCreated;

    /// <summary>
    /// Raised when an event is appended to the current (most recent) cluster.
    /// The UI should refresh that cluster's computed properties.
    /// </summary>
    public event Action<DeviceChangeCluster, DeviceChangeEvent>? EventAppendedToCluster;

    public void ProcessEvent(DeviceChangeEvent deviceEvent)
    {
        lock (_lock)
        {
            if (_currentCluster == null ||
                (deviceEvent.Timestamp - _currentCluster.LastEventTime) > ClusterThreshold)
            {
                // Start a new cluster
                _currentCluster = new DeviceChangeCluster
                {
                    StartTime = deviceEvent.Timestamp,
                    LastEventTime = deviceEvent.Timestamp
                };
                _currentCluster.Events.Add(deviceEvent);
                NewClusterCreated?.Invoke(_currentCluster);
            }
            else
            {
                // Append to current cluster
                _currentCluster.LastEventTime = deviceEvent.Timestamp;
                _currentCluster.Events.Add(deviceEvent);
                EventAppendedToCluster?.Invoke(_currentCluster, deviceEvent);
            }
        }
    }

    /// <summary>
    /// Hard reset: discard current cluster state. Used by Clear command.
    /// </summary>
    public void Reset()
    {
        lock (_lock)
        {
            _currentCluster = null;
        }
    }
}
```

### Verification
- Project builds without errors.

---

## Step 6 — Services: DeviceWatcherService

### Goal
Port the WMI device monitoring logic from the CLI project (`list_new_devices/Program.cs`) into a reusable service class that emits `DeviceChangeEvent` objects.

### Instructions

Create file `device_monitor_ui/Services/DeviceWatcherService.cs`:

```csharp
using System.Collections.Concurrent;
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

    private ManagementEventWatcher? _creationWatcher;
    private ManagementEventWatcher? _deletionWatcher;

    private readonly ConcurrentDictionary<string, bool> _trackedDevices = new();

    /// <summary>Raised for every individual device add or remove event.</summary>
    public event Action<DeviceChangeEvent>? DeviceChanged;

    public void Start()
    {
        // Snapshot current devices so we only report changes
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
                string? deviceId = device["DeviceID"]?.ToString();
                if (!string.IsNullOrEmpty(deviceId))
                    _trackedDevices.TryAdd(deviceId, true);
            }
        }
    }

    private void OnDeviceCreated(object sender, EventArrivedEventArgs args)
    {
        try
        {
            var target = (ManagementBaseObject)args.NewEvent["TargetInstance"];
            string? deviceId = target["DeviceID"]?.ToString();
            if (string.IsNullOrEmpty(deviceId) || !_trackedDevices.TryAdd(deviceId, true))
                return;

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
        catch
        {
            // Swallow per-event errors; monitoring continues
        }
    }

    private void OnDeviceRemoved(object sender, EventArrivedEventArgs args)
    {
        try
        {
            var target = (ManagementBaseObject)args.NewEvent["TargetInstance"];
            string? deviceId = target["DeviceID"]?.ToString();
            string? name = target["Name"]?.ToString() ?? "Unknown";
            if (string.IsNullOrEmpty(deviceId))
                return;

            _trackedDevices.TryRemove(deviceId, out _);

            DeviceChanged?.Invoke(new DeviceChangeEvent
            {
                Timestamp = DateTime.Now,
                Kind = ChangeKind.Removed,
                Name = name,
                DeviceId = deviceId,
                Manufacturer = "",
                Description = ""
            });
        }
        catch
        {
            // Swallow per-event errors; monitoring continues
        }
    }

    private static (string Name, string DeviceId, string Manufacturer, string Description)
        GetDeviceDetails(string deviceId)
    {
        var escaped = deviceId.Replace("\\", "\\\\");
        using var searcher = new ManagementObjectSearcher(
            $"SELECT * FROM Win32_PnPEntity WHERE DeviceID = '{escaped}'");
        using var results = searcher.Get();

        foreach (ManagementObject device in results)
        {
            using (device)
            {
                return (
                    Name: device["Name"]?.ToString() ?? "Unknown",
                    DeviceId: deviceId,
                    Manufacturer: device["Manufacturer"]?.ToString() ?? "",
                    Description: device["Description"]?.ToString() ?? ""
                );
            }
        }

        return ("Unknown", deviceId, "", "");
    }

    public void Dispose()
    {
        _creationWatcher?.Stop();
        _creationWatcher?.Dispose();
        _deletionWatcher?.Stop();
        _deletionWatcher?.Dispose();
    }
}
```

### Verification
- Project builds without errors.

---

## Step 7 — ViewModels: EventViewModel

### Goal
Create a view model for a single device event (child row in the UI).

### Instructions

Create file `device_monitor_ui/ViewModels/EventViewModel.cs`:

```csharp
using DeviceMonitorUi.Models;

namespace DeviceMonitorUi.ViewModels;

public class EventViewModel
{
    private readonly DeviceChangeEvent _event;

    public EventViewModel(DeviceChangeEvent deviceEvent)
    {
        _event = deviceEvent;
    }

    public ChangeKind Kind => _event.Kind;
    public string Icon => _event.Kind == ChangeKind.Added ? "+" : "−";
    public string Timestamp => _event.Timestamp.ToString("HH:mm:ss.fff");
    public string Name => _event.Name;
    public string DeviceId => _event.DeviceId;
    public string Manufacturer => _event.Manufacturer;
    public string Description => _event.Description;
}
```

### Verification
- Project builds without errors.

---

## Step 8 — ViewModels: ClusterViewModel

### Goal
Create a view model for a cluster row. It must expose computed summary properties and an observable collection of child event view models. It must support the single-event flat display vs multi-event expandable display.

### Instructions

Create file `device_monitor_ui/ViewModels/ClusterViewModel.cs`:

```csharp
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
        foreach (var e in cluster.Events)
            Children.Add(new EventViewModel(e));
    }

    public ObservableCollection<EventViewModel> Children { get; } = new();

    // Display mode
    public bool IsSingleEvent => _cluster.TotalCount == 1;
    public bool IsMultiEvent => _cluster.TotalCount > 1;

    // Cluster icon
    public string Icon => _cluster.IsAllAdded ? "+" :
                           _cluster.IsAllRemoved ? "−" : "~";

    public string IconColor => _cluster.IsAllAdded ? "Green" :
                               _cluster.IsAllRemoved ? "Red" : "Gray";

    // Summary fields (multi-event)
    public string Timestamp => _cluster.StartTime.ToString("HH:mm:ss.fff");
    public string TotalChanges => _cluster.TotalCount == 1
        ? "1 change"
        : $"{_cluster.TotalCount} changes";
    public string Breakdown => _cluster.BreakdownText;
    public string SummaryLabel => _cluster.SummaryLabel;

    // Single-event fields (flat row)
    public string SingleName => _cluster.Events.FirstOrDefault()?.Name ?? "";
    public string SingleDeviceId => _cluster.Events.FirstOrDefault()?.DeviceId ?? "";
    public string SingleManufacturer => _cluster.Events.FirstOrDefault()?.Manufacturer ?? "";
    public string SingleDescription => _cluster.Events.FirstOrDefault()?.Description ?? "";

    public void AddEvent(DeviceChangeEvent deviceEvent)
    {
        Children.Add(new EventViewModel(deviceEvent));
        NotifyAllChanged();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void NotifyAllChanged()
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
```

### Verification
- Project builds without errors.

---

## Step 9 — ViewModels: MainViewModel

### Goal
Create the top-level view model that owns the cluster list, the `Clear` command, the empty-state visibility, and wires `DeviceWatcherService` → `ClusterAggregator` → UI collection.

### Instructions

Create file `device_monitor_ui/ViewModels/MainViewModel.cs`:

```csharp
using System.Collections.ObjectModel;
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

    public ObservableCollection<ClusterViewModel> Clusters { get; } = new();

    public RelayCommand ClearCommand { get; }

    public bool IsEmpty => Clusters.Count == 0;

    public MainViewModel()
    {
        _aggregator = new ClusterAggregator();
        _watcher = new DeviceWatcherService();

        ClearCommand = new RelayCommand(ExecuteClear);

        // Wire aggregator events
        _aggregator.NewClusterCreated += OnNewCluster;
        _aggregator.EventAppendedToCluster += OnEventAppended;

        // Wire watcher → aggregator
        _watcher.DeviceChanged += OnDeviceChanged;

        // Track collection changes for empty state
        Clusters.CollectionChanged += (_, _) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsEmpty)));

        // Start monitoring immediately
        _watcher.Start();
    }

    private void OnDeviceChanged(DeviceChangeEvent deviceEvent)
    {
        _aggregator.ProcessEvent(deviceEvent);
    }

    private void OnNewCluster(DeviceChangeCluster cluster)
    {
        var vm = new ClusterViewModel(cluster);
        Application.Current.Dispatcher.Invoke(() => Clusters.Add(vm));
    }

    private void OnEventAppended(DeviceChangeCluster cluster, DeviceChangeEvent deviceEvent)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            // Find the existing ClusterViewModel for this cluster
            // It will be the last item in the list (current cluster is always most recent)
            if (Clusters.Count > 0)
            {
                var lastCluster = Clusters[^1];
                lastCluster.AddEvent(deviceEvent);
            }
        });
    }

    private void ExecuteClear()
    {
        Clusters.Clear();
        _aggregator.Reset();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public void Dispose()
    {
        _watcher.Dispose();
    }
}
```

### Verification
- Project builds without errors.

---

## Step 10 — UI: MainWindow XAML + code-behind

### Goal
Build the main window with:
- Top bar: title (left) + Clear button (right)
- Empty state: centered prominent text, visible when `IsEmpty` is true
- Timeline: `ItemsControl` bound to `Clusters`, using data templates for single-event (flat) and multi-event (expandable via `TreeViewItem` or `Expander`)
- Child rows with `+`/`-` icon (green/red)

### Instructions

**Replace** the contents of `device_monitor_ui/MainWindow.xaml` with:

```xml
<Window x:Class="DeviceMonitorUi.MainWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:vm="clr-namespace:DeviceMonitorUi.ViewModels"
        Title="Device Monitor"
        Width="1000" Height="600"
        WindowStartupLocation="CenterScreen"
        Background="#1E1E1E">

    <Window.Resources>
        <!-- Converters -->
        <BooleanToVisibilityConverter x:Key="BoolToVis" />

        <!-- Icon color brush by string name -->
        <Style x:Key="IconText" TargetType="TextBlock">
            <Setter Property="FontWeight" Value="Bold" />
            <Setter Property="FontSize" Value="16" />
            <Setter Property="Width" Value="24" />
            <Setter Property="TextAlignment" Value="Center" />
            <Setter Property="VerticalAlignment" Value="Center" />
        </Style>

        <!-- Child event row template -->
        <DataTemplate x:Key="ChildEventTemplate" DataType="{x:Type vm:EventViewModel}">
            <Border Padding="32,4,8,4" BorderBrush="#333" BorderThickness="0,0,0,1">
                <Grid>
                    <Grid.ColumnDefinitions>
                        <ColumnDefinition Width="30" />
                        <ColumnDefinition Width="100" />
                        <ColumnDefinition Width="*" />
                        <ColumnDefinition Width="*" />
                        <ColumnDefinition Width="*" />
                        <ColumnDefinition Width="*" />
                    </Grid.ColumnDefinitions>
                    <TextBlock Grid.Column="0" Text="{Binding Icon}" Style="{StaticResource IconText}"
                               Foreground="{Binding Kind, Converter={StaticResource KindToBrush}}" />
                    <TextBlock Grid.Column="1" Text="{Binding Timestamp}" Foreground="#CCC" VerticalAlignment="Center" />
                    <TextBlock Grid.Column="2" Text="{Binding Name}" Foreground="White" VerticalAlignment="Center" />
                    <TextBlock Grid.Column="3" Text="{Binding DeviceId}" Foreground="#999" VerticalAlignment="Center"
                               TextTrimming="CharacterEllipsis" />
                    <TextBlock Grid.Column="4" Text="{Binding Manufacturer}" Foreground="#AAA" VerticalAlignment="Center" />
                    <TextBlock Grid.Column="5" Text="{Binding Description}" Foreground="#AAA" VerticalAlignment="Center"
                               TextTrimming="CharacterEllipsis" />
                </Grid>
            </Border>
        </DataTemplate>
    </Window.Resources>

    <Grid>
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto" />
            <RowDefinition Height="*" />
        </Grid.RowDefinitions>

        <!-- Top bar -->
        <Border Grid.Row="0" Background="#2D2D2D" Padding="16,10">
            <DockPanel>
                <TextBlock Text="Device Monitor" Foreground="White"
                           FontSize="18" FontWeight="SemiBold"
                           VerticalAlignment="Center" />
                <Button DockPanel.Dock="Right" Content="Clear"
                        HorizontalAlignment="Right"
                        Command="{Binding ClearCommand}"
                        Padding="16,6" FontSize="13"
                        Background="#444" Foreground="White"
                        BorderBrush="#666" Cursor="Hand" />
            </DockPanel>
        </Border>

        <!-- Empty state -->
        <StackPanel Grid.Row="1"
                    VerticalAlignment="Center" HorizontalAlignment="Center"
                    Visibility="{Binding IsEmpty, Converter={StaticResource BoolToVis}}">
            <TextBlock Text="Monitoring device changes…"
                       Foreground="#CCC" FontSize="28" FontWeight="Light"
                       HorizontalAlignment="Center" />
            <TextBlock Text="Plug in or remove a device to see events here."
                       Foreground="#777" FontSize="14" Margin="0,8,0,0"
                       HorizontalAlignment="Center" />
        </StackPanel>

        <!-- Timeline -->
        <ScrollViewer Grid.Row="1" VerticalScrollBarVisibility="Auto"
                      Visibility="{Binding IsEmpty, Converter={StaticResource InverseBoolToVis}}">
            <ItemsControl ItemsSource="{Binding Clusters}">
                <ItemsControl.ItemTemplate>
                    <DataTemplate DataType="{x:Type vm:ClusterViewModel}">
                        <ContentControl Content="{Binding}">
                            <ContentControl.Style>
                                <Style TargetType="ContentControl">
                                    <!-- Default: multi-event template -->
                                    <Setter Property="ContentTemplate"
                                            Value="{StaticResource MultiEventTemplate}" />
                                    <Style.Triggers>
                                        <DataTrigger Binding="{Binding IsSingleEvent}" Value="True">
                                            <Setter Property="ContentTemplate"
                                                    Value="{StaticResource SingleEventTemplate}" />
                                        </DataTrigger>
                                    </Style.Triggers>
                                </Style>
                            </ContentControl.Style>
                        </ContentControl>
                    </DataTemplate>
                </ItemsControl.ItemTemplate>
            </ItemsControl>
        </ScrollViewer>
    </Grid>
</Window>
```

**Note:** This XAML references `SingleEventTemplate`, `MultiEventTemplate`, `KindToBrush`, and `InverseBoolToVis` which must be defined. They are created in Step 11.

**Replace** the contents of `device_monitor_ui/MainWindow.xaml.cs` with:

```csharp
using System.Windows;
using DeviceMonitorUi.ViewModels;

namespace DeviceMonitorUi;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;

    public MainWindow()
    {
        InitializeComponent();
        _viewModel = new MainViewModel();
        DataContext = _viewModel;
        Closed += (_, _) => _viewModel.Dispose();
    }
}
```

### Verification
- Project builds (XAML may have design-time warnings for templates not yet defined — that's OK until Step 11).

---

## Step 11 — UI: Value converters and remaining DataTemplates

### Goal
Create the value converters and remaining DataTemplates referenced by `MainWindow.xaml`:
- `KindToBrushConverter` — maps `ChangeKind.Added` → Green, `Removed` → Red
- `InverseBooleanToVisibilityConverter` — inverts `BooleanToVisibilityConverter`
- `SingleEventTemplate` — flat row for single-event clusters
- `MultiEventTemplate` — expandable row with `Expander` for multi-event clusters

### Instructions

Create file `device_monitor_ui/Infrastructure/KindToBrushConverter.cs`:

```csharp
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using DeviceMonitorUi.Models;

namespace DeviceMonitorUi.Infrastructure;

public class KindToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is ChangeKind kind && kind == ChangeKind.Added
            ? Brushes.LimeGreen
            : Brushes.Red;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
```

Create file `device_monitor_ui/Infrastructure/InverseBoolToVisibilityConverter.cs`:

```csharp
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace DeviceMonitorUi.Infrastructure;

public class InverseBoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is true ? Visibility.Collapsed : Visibility.Visible;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
```

Create file `device_monitor_ui/Infrastructure/IconColorConverter.cs`:

```csharp
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace DeviceMonitorUi.Infrastructure;

public class IconColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value?.ToString() switch
        {
            "Green" => Brushes.LimeGreen,
            "Red" => Brushes.Red,
            _ => Brushes.Gray
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
```

**Now update** `MainWindow.xaml` to add these converters and templates in the `<Window.Resources>` section. Add the following entries **above** the existing `ChildEventTemplate`:

```xml
<!-- Add these converter instances -->
<local:KindToBrushConverter x:Key="KindToBrush"
    xmlns:local="clr-namespace:DeviceMonitorUi.Infrastructure" />
<local:InverseBoolToVisibilityConverter x:Key="InverseBoolToVis"
    xmlns:local="clr-namespace:DeviceMonitorUi.Infrastructure" />
<local:IconColorConverter x:Key="IconColorConv"
    xmlns:local="clr-namespace:DeviceMonitorUi.Infrastructure" />

<!-- Single-event flat row template -->
<DataTemplate x:Key="SingleEventTemplate" DataType="{x:Type vm:ClusterViewModel}">
    <Border Padding="8,6" BorderBrush="#333" BorderThickness="0,0,0,1" Background="#252525">
        <Grid>
            <Grid.ColumnDefinitions>
                <ColumnDefinition Width="30" />
                <ColumnDefinition Width="100" />
                <ColumnDefinition Width="*" />
                <ColumnDefinition Width="*" />
                <ColumnDefinition Width="*" />
                <ColumnDefinition Width="*" />
            </Grid.ColumnDefinitions>
            <TextBlock Grid.Column="0" Text="{Binding Icon}" Style="{StaticResource IconText}"
                       Foreground="{Binding IconColor, Converter={StaticResource IconColorConv}}" />
            <TextBlock Grid.Column="1" Text="{Binding Timestamp}" Foreground="#CCC" VerticalAlignment="Center" />
            <TextBlock Grid.Column="2" Text="{Binding SingleName}" Foreground="White" VerticalAlignment="Center" />
            <TextBlock Grid.Column="3" Text="{Binding SingleDeviceId}" Foreground="#999"
                       VerticalAlignment="Center" TextTrimming="CharacterEllipsis" />
            <TextBlock Grid.Column="4" Text="{Binding SingleManufacturer}" Foreground="#AAA" VerticalAlignment="Center" />
            <TextBlock Grid.Column="5" Text="{Binding SingleDescription}" Foreground="#AAA"
                       VerticalAlignment="Center" TextTrimming="CharacterEllipsis" />
        </Grid>
    </Border>
</DataTemplate>

<!-- Multi-event expandable cluster template -->
<DataTemplate x:Key="MultiEventTemplate" DataType="{x:Type vm:ClusterViewModel}">
    <Border BorderBrush="#333" BorderThickness="0,0,0,1" Background="#252525">
        <Expander IsExpanded="False">
            <Expander.Header>
                <Grid Width="{Binding ActualWidth,
                    RelativeSource={RelativeSource AncestorType=Expander}}">
                    <Grid.ColumnDefinitions>
                        <ColumnDefinition Width="30" />
                        <ColumnDefinition Width="100" />
                        <ColumnDefinition Width="100" />
                        <ColumnDefinition Width="80" />
                        <ColumnDefinition Width="*" />
                    </Grid.ColumnDefinitions>
                    <TextBlock Grid.Column="0" Text="{Binding Icon}" Style="{StaticResource IconText}"
                               Foreground="{Binding IconColor, Converter={StaticResource IconColorConv}}" />
                    <TextBlock Grid.Column="1" Text="{Binding Timestamp}" Foreground="#CCC"
                               VerticalAlignment="Center" />
                    <TextBlock Grid.Column="2" Text="{Binding TotalChanges}" Foreground="#AAA"
                               VerticalAlignment="Center" />
                    <TextBlock Grid.Column="3" Text="{Binding Breakdown}" Foreground="#AAA"
                               VerticalAlignment="Center" />
                    <TextBlock Grid.Column="4" Text="{Binding SummaryLabel}" Foreground="White"
                               VerticalAlignment="Center" />
                </Grid>
            </Expander.Header>
            <ItemsControl ItemsSource="{Binding Children}"
                          ItemTemplate="{StaticResource ChildEventTemplate}" />
        </Expander>
    </Border>
</DataTemplate>
```

### Verification
- `dotnet build device_monitor_ui/device_monitor_ui.csproj` succeeds with no errors.

---

## Step 12 — App.xaml: Add assembly-level attribute and startup

### Goal
Add `[assembly: SupportedOSPlatform("windows")]` and ensure `App.xaml` uses `MainWindow` as the startup window.

### Instructions

**Edit** `device_monitor_ui/App.xaml` — ensure `StartupUri` points to `MainWindow.xaml`:

```xml
<Application x:Class="DeviceMonitorUi.App"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             StartupUri="MainWindow.xaml">
    <Application.Resources />
</Application>
```

**Edit** `device_monitor_ui/App.xaml.cs`:

```csharp
using System.Runtime.Versioning;
using System.Windows;

[assembly: SupportedOSPlatform("windows")]

namespace DeviceMonitorUi;

public partial class App : Application
{
}
```

### Verification
- `dotnet build device_monitor_ui/device_monitor_ui.csproj` succeeds.

---

## Step 13 — Build and launch verification

### Goal
Verify the full project builds and launches correctly.

### Instructions

1. Build the entire solution:
   ```powershell
   dotnet build list_new_devices.slnx
   ```
   Expect: 0 errors for both projects.

2. Run the WPF app:
   ```powershell
   dotnet run --project device_monitor_ui/device_monitor_ui.csproj
   ```

3. **Verify visually:**
   - Window opens with dark background.
   - Title bar shows "Device Monitor" on the left, "Clear" button on the right.
   - Center of screen shows "Monitoring device changes…" with subtext.
   - App runs without crashing.

4. Click "Clear" — empty state remains visible (already empty).

### Verification
- App opens successfully.
- Empty state is displayed.
- No exceptions in output.

---

## Step 14 — Manual plug/unplug validation

### Goal
Test the app with a real USB device to validate clustering, display, icons, and Clear.

### Instructions

1. Run the app (Step 13).

2. **Test: plug in a USB device** (e.g., WiFi dongle).
   - Verify: empty state disappears.
   - Verify: a cluster row appears with `+` icon (green), timestamp, count, breakdown, label.
   - If multiple logical devices arrive within 3s, they cluster into one row.
   - Expand the cluster — child rows show individual `+` icons, details.

3. **Test: unplug the same device.**
   - Verify: a new cluster row appears with `-` icon (red).
   - Expand — child rows show `-` icons.

4. **Test: rapid plug + unplug (within 3s).**
   - Verify: events cluster into a single row with `~` icon (grey/mixed).

5. **Test: Clear button.**
   - Click Clear.
   - Verify: all clusters disappear.
   - Verify: empty state message reappears.
   - Plug in a device again — new cluster starts fresh.

### Verification
- All acceptance criteria from `spec.md` §12 pass.
