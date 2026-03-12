# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Unity sample project demonstrating event leaderboards using the Hiro framework and Nakama backend. Displays real-time competitive leaderboards with zone-based promotions/demotions and tiered rewards.

## Build and Development

This is a Unity project (2022.3+). Open with Unity Hub or Unity Editor directly. No command-line build scripts.

**Running the project:**
1. Start a local Nakama server (see `../Nakama+Hiro/local.yml` for Docker configuration)
2. Open the project in Unity Editor
3. Play the main scene

**Account switching for testing:**
- Menu: `Tools > Nakama > Account Switcher` - Switch between test accounts
- Menu: `Tools > Nakama > Clear Test Accounts` - Reset stored accounts

## Architecture

### Core Pattern: MVP (Model-View-Presenter)

```
HiroEventLeaderboardsCoordinator (MonoBehaviour)
    ↓ Creates Hiro systems, handles authentication
EventLeaderboardsViewBehaviour (MonoBehaviour)
    ↓ Unity lifecycle, creates Controller/View, cleanup in OnDestroy
EventLeaderboardsController (Plain C# class)
    ↓ Business logic, data operations, testable
EventLeaderboardsView (IDisposable)
    ↓ UI presentation, event handlers, modal management
```

### Key Files

| File | Purpose |
|------|---------|
| `Scripts/HiroEventLeaderboardsCoordinator.cs` | System initialization, Nakama authentication, session management |
| `Scripts/EventLeaderboardsViewBehaviour.cs` | MonoBehaviour lifecycle, creates Controller/View, cleanup |
| `Scripts/EventLeaderboardsController.cs` | Business logic (plain C#, constructor injection, testable) |
| `Scripts/EventLeaderboardsView.cs` | UI presentation (IDisposable, thread-safe disposal) |
| `Scripts/AccountSwitcher.cs` | Static event for decoupled account switch notifications |
| `Scripts/VisualElementExtensions.cs` | RequireElement, Show/Hide helpers |
| `Scripts/EventLeaderboardZoneCalculator.cs` | Calculate promotion/demotion zones |
| `Scripts/EventLeaderboardTimeUtility.cs` | Time calculations and formatting |
| `Editor/AccountSwitcher.cs` | Editor window for multi-account testing |

### Hiro Framework Integration

Systems are obtained via the coordinator and injected into the controller:
```csharp
var nakamaSystem = coordinator.GetSystem<NakamaSystem>();
var eventLeaderboardsSystem = coordinator.GetSystem<EventLeaderboardsSystem>();
var economySystem = coordinator.GetSystem<EconomySystem>();

_controller = new EventLeaderboardsController(nakamaSystem, eventLeaderboardsSystem, economySystem);
```

### UI Toolkit

Uses Unity's UIElements (not UGUI). Templates are in `Assets/UnityHiroEventLeaderboards/UI/`:
- `EventLeaderboards.uxml` - Main layout with modals
- `EventLeaderboard.uxml` - List item template
- `EventLeaderboardRecord.uxml` - Score record template
- `EventLeaderboardZone.uxml` - Zone indicator template

View classes query elements with `rootElement.RequireElement<T>("element-id")` (fail-fast) or `rootElement.Q<T>("element-id")` (nullable).

### Zone Calculation Logic

`EventLeaderboardZoneCalculator` determines promotion/demotion boundaries:
1. **Change zones** (percentage-based): Top X% promote, bottom Y% demote
2. **Reward tiers** (rank-based): Specific rank ranges determine tier changes

Priority: Change zones → Reward tiers fallback.

## Conventions

- **Namespace**: `HiroEventLeaderboards` (runtime), `HiroEventLeaderboards.Editor` (editor)
- **Private fields**: Leading underscore (`_fieldName`)
- **Async methods**: Named with `Async` suffix, try/catch with `OperationCanceledException` handling
- **View classes**: Sealed, IDisposable with thread-safe disposal pattern
- **Controller**: Plain C# class with constructor injection and `ArgumentNullException` validation
- **Error handling**: Show errors via `ShowError(message)`, use `Debug.LogException(e)` for logging
- **UI elements**: Use `RequireElement<T>()` for required elements (fail-fast), `Q<T>()` for optional
- **Display toggling**: Use `element.Show()`, `element.Hide()`, `element.SetDisplay(bool)`
- **Account switching**: Use `AccountSwitcher.AccountSwitched` event for decoupled notifications
