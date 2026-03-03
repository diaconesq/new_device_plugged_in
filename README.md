# Device Monitor

A Windows desktop app that detects USB/PnP device plug-in and removal in real time, displaying events as a clustered timeline.

![.NET 10](https://img.shields.io/badge/.NET-10.0-blue)
![WPF](https://img.shields.io/badge/UI-WPF-purple)
![Windows](https://img.shields.io/badge/OS-Windows-0078d4)

## What it does

Plug in a USB keyboard, mouse, or any device — the app instantly shows what appeared (or disappeared), grouped into time-based clusters. Each cluster picks the most meaningful device name automatically, and you can expand it to see every individual event with full details.

## Projects

| Project | Description |
|---|---|
| `device_monitor_ui/` | WPF desktop app — the main UI |
| `list_new_devices/` | Console app — the original CLI prototype |

## Quick start

```powershell
# Build and run the UI
dotnet run --project device_monitor_ui/device_monitor_ui.csproj
```

> Requires **Windows** and **.NET 10 SDK**. Uses WMI (`Win32_PnPEntity`) for device detection — must run with sufficient privileges.

## Features

- **Real-time monitoring** — auto-starts on launch, no setup needed
- **Clustered timeline** — rapid-fire events (within 3 s) are grouped into a single expandable row
- **Smart naming** — clusters show the best device name using a 4-tier ranking (branded → generic → infrastructure → unknown)
- **Resizable columns** — drag column headers to resize
- **Dark theme** — easy on the eyes

## Architecture

- **WMI event watchers** for `__InstanceCreationEvent` / `__InstanceDeletionEvent` on `Win32_PnPEntity`
- **MVVM** pattern (hand-rolled, no framework dependency)
- **Chained clustering** with a configurable Δt (default 3 s)

## Docs

- [`spec.md`](spec.md) — living specification (source of truth)
- [`prompt.md`](prompt.md) — original implementation plan

## License

MIT
