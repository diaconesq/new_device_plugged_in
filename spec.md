# Device Monitor UI — Specification

> Living document. Updated as decisions are made.  
> Last updated: 2026-03-02

---

## 1. Overview

A Windows-native desktop application that monitors device plug-in and removal events in real time, presenting them as a clean, clustered timeline. The CLI project (`list_new_devices`) provides the device-watching blueprint; this UI wraps that functionality.

---

## 2. Platform

| Decision | Value |
|---|---|
| UI framework | **WPF** (.NET) |
| OS target | Windows only (`[assembly: SupportedOSPlatform("windows")]`) |
| Project location | New WPF project added to existing solution (`device_monitor_ui/`) |

---

## 3. Main Window Layout

- **Top bar:** app title (left) + `Clear` button (right)
- **Content area:** single live timeline of clustered device change events
- **Empty state:** when no clusters exist, show a prominent centered message (see §7)

No split pane, no inventory view, no secondary screens.

---

## 4. Monitoring Behavior

- Monitoring **starts automatically** on app launch — no user action required
- Monitors both **device additions** and **device removals** via WMI (`Win32_PnPEntity`)
- Runs continuously until app is closed

---

## 5. Clustering Algorithm

Raw WMI events are grouped into clusters before display.

### Rule
**Chained time-based clustering with $\Delta t = 3\text{s}$:**

- When the first event arrives, create a new cluster.
- Each subsequent event: if it arrives within **3 seconds** of the last event in the current cluster, append it to that cluster.
- If more than 3 seconds have elapsed since the last event, **close** the current cluster and start a new one.

### Properties
- No "active" vs "finalized" visual distinction — clusters just grow as events arrive.
- No timer-based sealing; clusters are implicitly closed by the gap between events.

---

## 6. Timeline Feed (Cluster Rows)

Each cluster appears as a row in the timeline. Display mode depends on cluster size.

### Single-event clusters
If a cluster contains exactly **1 event**, display it as a **flat row** — no expand/collapse, no parent/child structure.

| Field | Value |
|---|---|
| Icon | `+` (green) if Added; `-` (red) if Removed |
| Timestamp | Event timestamp |
| Device name | Device name |
| Device ID | Device ID |
| Manufacturer | Manufacturer |
| Description | Description |

### Multi-event clusters
If a cluster contains **2 or more events**, display as a **collapsible parent row** with child rows (see §7).

### Cluster row icon (multi-event)
| Cluster contents | Icon |
|---|---|
| All additions | `+` (green) |
| All removals | `-` (red) |
| Mixed | `±` or a distinct mixed icon (e.g. `~`) (neutral/grey) |

### Cluster row fields (multi-event, always visible)
| Field | Example |
|---|---|
| Icon | See above |
| Start timestamp | `14:03:21.118` |
| Total changes | `3 changes` |
| Breakdown | `+3 / -0` |
| Top label | Best-ranked device name across cluster (see §6a); fallback: `Multiple devices` |

No duration column.

### 6a. "First meaningful" device name
Pick the **best-ranked name** across all events in the cluster using this priority order:

| Tier | Description | Examples |
|---|---|---|
| **1 — Best** | Specific product/brand name | `Logitech HID-compliant Cordless Mouse`, `Dell WD19 Dock`, `802.11n USB Wireless LAN Card` |
| **2 — Acceptable** | Generic but descriptive HID/functional name | `HID-compliant mouse`, `HID-compliant keyboard`, `USB Mass Storage Device` |
| **3 — Last resort** | Opaque infrastructure names | `USB Composite Device`, `USB Root Hub`, `USB Hub`, `Generic USB Hub`, `HID-compliant device` |
| **4 — Never use** | Truly unknown | `Unknown`, empty string |

**Selection rule:** pick the highest-tier name available across all events in the cluster. Within the same tier, prefer the **first arrival**.  
If only Tier 4 names exist, fall back to `Multiple devices`.

**Tier classification heuristic:**
- Tier 1: name contains a word not in the Tier 2/3 keyword list (i.e. has brand/model specificity)
- Tier 2: name starts with `HID-compliant` + specific type (mouse, keyboard, etc.), or is a known functional label
- Tier 3: exact match against opaque blocklist (`USB Composite Device`, `USB Root Hub`, `USB Hub`, `Generic USB Hub`, `HID-compliant device`)
- Tier 4: `Unknown` or null/empty

**Example:**  
Cluster events: `USB Composite Device` → `HID-compliant mouse` → `Logitech HID-compliant Cordless Mouse`  
→ Tier 3, Tier 2, Tier 1 → label is `Logitech HID-compliant Cordless Mouse`

**Example (no Tier 1 available):**  
Cluster events: `USB Composite Device` → `HID-compliant mouse`  
→ Tier 3, Tier 2 → label is `HID-compliant mouse`

### Example rows
**Single-event (flat):**
```
[+] 23:16:55.275 | 802.11n USB Wireless LAN Card | USB\VID_148F&PID_7601\1.0 | Ralink Technology, Corp. | 802.11n USB Wireless LAN Card
[-] 23:14:59.300 | USB Input Device | USB\VID_046D&PID_C52B&MI_02\... | | 
```

**Multi-event clusters:**
```
[+] 23:16:55.275 | 2 changes | +2 / -0 | 802.11n USB Wireless LAN Card
[-] 23:14:59.300 | 2 changes | +0 / -2 | USB Input Device
[~] 14:07:10.334 | 6 changes | +5 / -1 | Logitech USB Receiver
[+] 14:15:00.120 | 14 changes | +12 / -2 | Dell WD19 Dock
```

---

## 7. Expandable Child Rows

Clicking a cluster row expands it to show one row per individual device event.

### Child row fields
| Field | Notes |
|---|---|
| **Icon** | `+` (green) for Added; `-` (red) for Removed |
| Timestamp | Time of this individual event |
| Device name | `Name` from WMI |
| Device ID | `DeviceID` from WMI |
| Manufacturer | `Manufacturer` from WMI (blank if unavailable) |
| Description | `Description` from WMI (blank if unavailable) |

---

## 8. Empty State

Shown when the cluster list is empty (on launch, or after `Clear`).

- **Primary text:** `Monitoring device changes…` (large, prominent, centered)
- **Secondary text:** `Plug in or remove a device to see events here.` (smaller, muted)
- Disappears automatically when the first cluster arrives
- Reappears immediately after `Clear`

---

## 9. Controls

Only one control: the **`Clear` button**.

### Clear behavior (hard reset)
1. Remove all clusters from the timeline
2. Reset the cluster aggregator (`currentCluster = null`)
3. Empty state message reappears immediately
4. Next device event starts a fresh cluster from scratch

---

## 10. Architecture

### Project structure
```
device_monitor_ui/
  App.xaml / App.xaml.cs
  MainWindow.xaml / MainWindow.xaml.cs
  Models/
    DeviceChangeEvent.cs       # single raw event
    DeviceChangeCluster.cs     # group of events + computed summary
  Services/
    DeviceWatcherService.cs    # WMI create/delete watchers
    ClusterAggregator.cs       # 3s chaining logic
  ViewModels/
    MainViewModel.cs
    ClusterViewModel.cs
    EventViewModel.cs
  Infrastructure/
    RelayCommand.cs
```

### Data contracts

**`DeviceChangeEvent`**
```csharp
DateTime Timestamp
ChangeKind Kind          // Added | Removed
string Name
string DeviceId
string Manufacturer
string Description
```

**`DeviceChangeCluster`** (computed properties)
```csharp
DateTime StartTime
DateTime LastEventTime
ObservableCollection<DeviceChangeEvent> Events
int TotalCount           // computed
int AddedCount           // computed
int RemovedCount         // computed
string SummaryLabel      // first name or "Multiple devices"
string BreakdownText     // "+X / -Y"
```

### Threading model
- WMI callbacks arrive on background threads
- All UI updates marshalled via `Application.Current.Dispatcher.Invoke(...)`

### Error handling
- Per-event parse errors are caught and logged; monitoring continues
- No crash on a single bad WMI event

---

## 11. Build Order

1. Scaffold WPF project + MVVM skeleton
2. Port `DeviceWatcherService` from existing CLI (`list_new_devices`)
3. Implement `ClusterAggregator` (pure logic, unit-testable)
4. Bind timeline UI (cluster rows + child row templates)
5. Implement `Clear` hard reset
6. Implement empty state
7. Manual validation with real plug/unplug (USB WiFi dongle, docking station)

---

## 12. Acceptance Criteria

- [ ] App launches and starts monitoring with no user action
- [ ] Empty state message is visible on launch
- [ ] Plug/unplug produces clustered rows using 3s chained rule
- [ ] Single-event clusters display as flat rows (no expand/collapse)
- [ ] Multi-event cluster summary shows icon, timestamp, total count, `+/-` breakdown, top label
- [ ] Cluster icon is `+` (green) for all-additions, `-` (red) for all-removals, `~` (grey) for mixed
- [ ] Expanding a multi-event cluster reveals child rows
- [ ] Child rows show `+`/`-` icon (green/red), time, name, device ID, manufacturer, description
- [ ] `Clear` performs hard reset: timeline cleared, aggregator reset, empty state reappears
- [ ] App runs continuously without user input
- [ ] WMI watchers dispose cleanly on app exit

---

## 13. Out of Scope (v1)

- Pause/Resume monitoring
- Export to file (JSON/CSV)
- Copy selected rows to clipboard
- Persistent history across sessions
- Settings/configuration UI
- Duration column on cluster rows
- Active vs. finalized cluster visual distinction
- Notifications / system tray

---

## Revision History

| Date | Change |
|---|---|
| 2026-03-02 | Initial spec created from UX discovery session |
| 2026-03-02 | Added empty state requirement (§8) |
| 2026-03-02 | Defined "first meaningful device name" with explicit generic blocklist (§6) |
| 2026-03-02 | Narrowed generic blocklist — HID-specific names (e.g. `HID-compliant mouse`) are meaningful enough to use; removed from blocklist (§6) |
| 2026-03-02 | Replaced blocklist approach with 4-tier ranking system for cluster label selection; best name across cluster wins (§6) |
| 2026-03-02 | Single-event clusters display flat (no expand); multi-event clusters show `+`/`-`/`~` icon based on addition/removal/mixed content (§6) |
