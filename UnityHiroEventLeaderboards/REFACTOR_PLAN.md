# Refactoring Plan: Transfer Patterns from UnityHiroChallenges

## Overview

This plan transfers proven refactoring patterns from `UnityHiroChallenges` to `UnityHiroEventLeaderboards`. The changes improve testability, resource management, error handling, and code clarity.

---

## Phase 1: Architecture - MVP Separation

### 1.1 Create `EventLeaderboardsViewBehaviour` (MonoBehaviour)

**Purpose**: Separate Unity lifecycle from business logic.

**Changes**:
- Create new `EventLeaderboardsViewBehaviour.cs` that:
  - Is a MonoBehaviour attached to the scene
  - Creates the Controller and View when coordinator is ready
  - Manages cleanup in `OnDestroy()`
  - Acts as glue between Unity and business logic

**Pattern from Challenges**:
```csharp
public class EventLeaderboardsViewBehaviour : MonoBehaviour
{
    private EventLeaderboardsController _controller;
    private EventLeaderboardsView _view;

    private void Start()
    {
        // Wait for coordinator, then create controller/view
    }

    private void OnDestroy()
    {
        _view?.Dispose();
    }
}
```

### 1.2 Make `EventLeaderboardsController` a Plain C# Class

**Purpose**: Enable unit testing without MonoBehaviour.

**Changes**:
- Remove MonoBehaviour inheritance
- Add constructor injection for dependencies
- Add `ArgumentNullException` validation for all dependencies

**Before**:
```csharp
public class EventLeaderboardsController : MonoBehaviour
{
    // Uses this.GetSystem<T>() at runtime
}
```

**After**:
```csharp
public class EventLeaderboardsController
{
    private readonly NakamaSystem _nakamaSystem;
    private readonly IEventLeaderboardsSystem _eventLeaderboardsSystem;

    public EventLeaderboardsController(
        NakamaSystem nakamaSystem,
        IEventLeaderboardsSystem eventLeaderboardsSystem)
    {
        _nakamaSystem = nakamaSystem ?? throw new ArgumentNullException(nameof(nakamaSystem));
        _eventLeaderboardsSystem = eventLeaderboardsSystem ?? throw new ArgumentNullException(nameof(eventLeaderboardsSystem));
    }
}
```

### 1.3 Make `EventLeaderboardsView` Implement `IDisposable`

**Purpose**: Proper resource cleanup and event unsubscription.

**Changes**:
- Add `IDisposable` interface
- Add thread-safe disposal pattern with lock
- Unsubscribe all events in `Dispose()`

---

## Phase 2: Resource Management

### 2.1 Add CancellationTokenSource Support

**Purpose**: Cancel async operations on dispose/account switch.

**Add to View**:
```csharp
private CancellationTokenSource _cts = new();
private readonly object _disposeLock = new();
private volatile bool _disposed;

private void ThrowIfDisposedOrCancelled()
{
    if (_disposed)
        throw new ObjectDisposedException(nameof(EventLeaderboardsView));
    _cts.Token.ThrowIfCancellationRequested();
}
```

### 2.2 Implement Thread-Safe Disposal

**Pattern**:
```csharp
public void Dispose()
{
    lock (_disposeLock)
    {
        if (_disposed)
            return;
        _disposed = true;
    }

    _cts.Cancel();
    _cts.Dispose();

    // Unsubscribe all events
    AccountSwitcher.AccountSwitched -= OnAccountSwitched;
    _leaderboardList.selectionChanged -= OnLeaderboardSelectionChanged;
    // ... other event unsubscriptions
}
```

---

## Phase 3: Async/Await & Error Handling

### 3.1 Standardize Async Void Handlers

**Purpose**: Prevent silent failures in async event handlers.

**Pattern for all async void handlers**:
```csharp
private async void OnAccountSwitched()
{
    try
    {
        ThrowIfDisposedOrCancelled();
        ShowSpinner();
        await RefreshEventLeaderboardsAsync();
    }
    catch (OperationCanceledException)
    {
        // Expected on dispose - ignore
    }
    catch (Exception e)
    {
        ShowError(e.Message);
        Debug.LogException(e);
    }
}
```

### 3.2 Replace `Debug.Log(e)` with `Debug.LogException(e)`

**Purpose**: Proper stack trace logging.

**Files to update**:
- `EventLeaderboardsView.cs` - all catch blocks
- `EventLeaderboardsController.cs` - all catch blocks
- `HiroEventLeaderboardsCoordinator.cs` - error handling

---

## Phase 4: UI Toolkit Improvements

### 4.1 Add `RequireElement<T>` Extension Method

**Purpose**: Fail-fast when UI elements are missing.

**Add extension**:
```csharp
public static class VisualElementExtensions
{
    public static T RequireElement<T>(this VisualElement parent, string name) where T : VisualElement
    {
        var element = parent.Q<T>(name);
        if (element == null)
            throw new InvalidOperationException($"Required UI element '{name}' of type {typeof(T).Name} not found");
        return element;
    }
}
```

### 4.2 Update UI Element Queries

**Before**:
```csharp
_submitScoreButton = rootElement.Q<Button>("submit-score-button");
```

**After**:
```csharp
_submitScoreButton = rootElement.RequireElement<Button>("submit-score-button");
```

### 4.3 Add Show/Hide Extension Methods

**Add extensions**:
```csharp
public static void Show(this VisualElement element) =>
    element.style.display = DisplayStyle.Flex;

public static void Hide(this VisualElement element) =>
    element.style.display = DisplayStyle.None;

public static void SetDisplay(this VisualElement element, bool visible) =>
    element.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
```

---

## Phase 5: Account Switching Refactor

### 5.1 Extract Static AccountSwitcher Event

**Purpose**: Decouple account switching from view references.

**Create/Update** `AccountSwitcher.cs`:
```csharp
public static class AccountSwitcher
{
    public static event Action AccountSwitched;

    public static async Task<ISession> SwitchAccountAsync(...)
    {
        var newSession = await AuthenticateAndStoreAccountAsync(...);
        AccountSwitched?.Invoke();
        return newSession;
    }
}
```

### 5.2 Subscribe in View

**In View constructor/initialization**:
```csharp
AccountSwitcher.AccountSwitched += OnAccountSwitched;
```

**In Dispose**:
```csharp
AccountSwitcher.AccountSwitched -= OnAccountSwitched;
```

---

## Phase 6: Time Calculations

### 6.1 Audit TotalSeconds vs Seconds Usage

**Check** `EventLeaderboardTimeUtility.cs` for:
- `.Seconds` should be `.TotalSeconds` for duration comparisons
- `.Minutes` should be `.TotalMinutes` for duration comparisons

**Example fix**:
```csharp
// WRONG
if (timeRemaining.Seconds > 0)

// CORRECT
if (timeRemaining.TotalSeconds > 0)
```

---

## Phase 7: Code Organization

### 7.1 Extract Modal Management (Optional)

**Purpose**: Reduce `EventLeaderboardsView.cs` size (currently 770 lines).

**Options**:
- Extract each modal to separate classes (e.g., `SubmitScoreModal`, `EventInfoModal`)
- Or create a generic `ModalManager` class

### 7.2 Private Backing Fields

**Audit and update** any public fields that should be properties:
```csharp
// Before
public List<IEventLeaderboard> EventLeaderboards;

// After
public List<IEventLeaderboard> EventLeaderboards { get; } = new();
```

### 7.3 Add XML Documentation

**Add summaries to main classes**:
```csharp
/// <summary>
/// Controller/Presenter for the Event Leaderboards system.
/// Handles business logic and coordinates with Hiro systems.
/// </summary>
public class EventLeaderboardsController { ... }
```

---

## Implementation Order

| Priority | Phase | Estimated Scope | Risk |
|----------|-------|-----------------|------|
| 1 | Phase 3.2 | Small | Low - logging only |
| 2 | Phase 6.1 | Small | Medium - bug fix |
| 3 | Phase 4 | Medium | Low - extensions |
| 4 | Phase 2 | Medium | Medium - disposal |
| 5 | Phase 3.1 | Medium | Low - error handling |
| 6 | Phase 1 | Large | High - architecture |
| 7 | Phase 5 | Medium | Medium - account switching |
| 8 | Phase 7 | Optional | Low - cleanup |

---

## Files to Modify

| File | Changes |
|------|---------|
| `EventLeaderboardsController.cs` | Remove MonoBehaviour, add constructor injection |
| `EventLeaderboardsView.cs` | Add IDisposable, disposal pattern, CTS, error handling |
| `HiroEventLeaderboardsCoordinator.cs` | Update to create ViewBehaviour |
| `EventLeaderboardTimeUtility.cs` | Audit TotalSeconds usage |
| **New** `EventLeaderboardsViewBehaviour.cs` | MonoBehaviour lifecycle management |
| **New** `VisualElementExtensions.cs` | RequireElement, Show/Hide |

---

## Testing Strategy

1. **After each phase**: Run existing tests, verify no regressions
2. **Phase 1 completion**: Add unit tests for Controller (now testable)
3. **Phase 2 completion**: Add disposal tests
4. **Phase 5 completion**: Test account switching scenarios

---

## Notes

- Each phase can be committed separately
- Phase 1 is the largest change - consider breaking into smaller commits
- Phases 2-4 can be done in parallel after Phase 1
- Phase 7 is optional polish
