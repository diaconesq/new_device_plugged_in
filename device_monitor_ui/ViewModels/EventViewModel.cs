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
    public string Icon => _event.Kind == ChangeKind.Added ? "+" : "-";
    public string Timestamp => _event.Timestamp.ToString("HH:mm:ss.fff");
    public string Name => _event.Name;
    public string DeviceId => _event.DeviceId;
    public string Manufacturer => _event.Manufacturer;
    public string Description => _event.Description;
}
