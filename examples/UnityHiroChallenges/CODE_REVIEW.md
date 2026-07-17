# Code Review - Challenges System

**Scope**: Assets/UnityHiroChallenges/Scripts, Assets/UnityHiroChallenges/Editor, Assets/UnityHiroChallenges/HeroicUI

---

## Review #1 - Initial Review (Resolved)

<details>
<summary>Click to expand resolved issues from initial review</summary>

### ChallengesView.cs

#### Critical Issues (Resolved)

1. ~~**View created with discard operator, never disposed**~~
   **FIXED**: ChallengesViewBehaviour now manages View lifecycle and calls Dispose() in OnDestroy().

2. ~~**Null check missing before subscribing to coordinator**~~
   **FIXED**: Null checks are in place before subscribing to coordinator events.

3. ~~**LINQ usage** - `RemoveAll` violates project guideline.~~
   **FIXED**: Replaced with reverse for-loop.

4. ~~**Inconsistent logging** - Mix of `Debug.Log(e)` and `Debug.LogException(e)`.~~
   **FIXED**: All exception logging now uses `Debug.LogException(e)` consistently.

5. ~~**OnCoordinatorReady infinite loop** - `while (!_controller.IsInitialized) await Task.Yield()` without timeout or error check.~~
   **FIXED**: Refactored to use TaskCompletionSource pattern with proper error handling.

6. ~~**difference.Seconds vs TotalSeconds** - Uses `difference.Seconds > 0` which is only the seconds component, mislabeling challenges.~~
   **FIXED**: Changed to `difference.TotalSeconds > 0`.

7. ~~**_challengesList.selectionChanged not cleaned up**~~
   **FIXED**: Now unsubscribed in Dispose().

8. ~~**_challengesSystemObserver declared but never assigned**~~
   **FIXED**: Removed unused field.

#### Medium Issues (Resolved)

9. ~~**Unused fields `_challengeEntryTemplate` and `_challengeParticipantTemplate`**~~
   **NOT AN ISSUE**: Required for ListView `makeItem` callbacks.

10. ~~**Double blank lines** - minor style issue.~~
    **FIXED**: Removed double blank lines.

11. ~~**Dispose doesn't unsubscribe from coordinator event**~~
    **FIXED**: ChallengesViewBehaviour handles coordinator subscription/unsubscription.

### ChallengesController.cs

#### Critical Issues (Resolved)

1. ~~**LINQ usage** - `using System.Linq` and multiple LINQ calls.~~
   **FIXED**: All LINQ removed. Uses foreach loops and List.Sort().

2. ~~**Public field instead of property** - `CurrentUserId` was a public field.~~
   **FIXED**: Now uses `public string CurrentUserId { get; private set; }`

3. **Mutable list exposed publicly** - WON'T FIX
   `ListView.itemsSource` requires `IList`, so `Challenges` must remain `List<IChallenge>`. Using `IReadOnlyList` would require duplicating the list in the view.

4. ~~**GetTemplate(int) relies on Dictionary order** - `ElementAt()` is not stable across runs.~~
   **FIXED**: Now stores ordered list of templates and indexes into that.

5. ~~**Subscribes to coordinator events but never unsubscribes**~~
   **FIXED**: Controller is now plain C# class; ChallengesViewBehaviour handles lifecycle.

6. ~~**async void handlers lack try/catch**~~
   **FIXED**: All async void handlers now have try/catch.

#### Medium Issues (Resolved)

7. ~~**Unnecessary KeyValuePair allocation**~~
   **FIXED**: Refactored to use templates directly.

8. **DTOs at bottom should be separate file or use records** - WON'T FIX
   Unity's .NET runtime lacks `System.Runtime.CompilerServices.IsExternalInit`, required for C# records. A polyfill exists but adds complexity for minimal benefit in a sample project.

### ChallengeParticipantView.cs

1. ~~**Uses `<color=blue>` in UI Toolkit Label** - UI Toolkit does not support rich text by default.~~
   **FIXED**: Removed unsupported markup.

### AccountSwitcher.cs

#### Critical Issues (Resolved)

1. ~~**Unsafe cast without null check**~~
   **FIXED**: Now uses pattern matching (`is not Session session`) with descriptive `InvalidOperationException` on type mismatch.

2. **Static event - memory leak risk** - ACCEPTABLE
   Static events can hold references to subscribers indefinitely. Current subscribers (`ChallengesView`) properly unsubscribe in `Dispose()`. Risk is documented; no code change needed.

3. ~~**PlayerPrefs not saved**~~
   **FIXED**: Added `PlayerPrefs.Save()` after `SetString` in `SaveCache()`.

#### Medium Issues (Resolved)

4. ~~**Cache reset inconsistency**~~
   **FIXED**: Added `PlayerPrefs.Save()` after `DeleteKey` in `ClearAccounts()` for consistency.

5. ~~**EnsureAccountsExistAsync fires AccountSwitched multiple times**~~
   **FIXED**: Extracted `AuthenticateAndStoreAccountAsync` and `ApplySessionAsync` private methods.

### AccountSwitcherEditor.cs (Editor)

1. ~~**OnCoordinatorInitialized throws when coordinator is missing**~~
   **FIXED**: Now uses graceful handling with error logging instead of throwing.

### WalletDisplay.cs

#### Critical Issues (Resolved)

1. ~~**float.Parse without culture - can fail**~~
   **FIXED**: Now uses `float.TryParse` with `CultureInfo.InvariantCulture`.

2. ~~**Fire-and-forget async without error handling**~~
   **FIXED**: `OnEconomyUpdated` is now async void with try/catch.

3. ~~**CancellationTokenSource not disposed**~~
   **FIXED**: Old CTS is now disposed before creating a new one.

4. ~~**Task.Delay with Time.deltaTime is problematic**~~
   **FIXED**: Now uses fixed `FrameDelaySeconds` (0.016s / ~60fps).

5. ~~**Dispose doesn't cancel running tasks**~~
   **FIXED**: `Dispose()` now cancels and disposes both CTS instances.

### HiroChallengesCoordinator.cs

1. **Hard-coded server key and PlayerPrefs tokens** - WON'T FIX
   Intentional for demo project. Server is configured specifically for this sample.

### Architecture Concerns (Resolved)

1. ~~**Tight coupling**: `AccountSwitcher` takes `ChallengesController` as parameter.~~
   **FIXED**: Now uses static `AccountSwitched` event for decoupling.

2. ~~**View instantiation**: Controller creates View but doesn't manage its lifecycle.~~
   **FIXED**: ChallengesViewBehaviour (MonoBehaviour) now creates and manages View lifecycle.

3. ~~**No CancellationToken propagation**: Long-running operations don't support cancellation.~~
   **FIXED**: ChallengesView now has a `CancellationTokenSource` cancelled in `Dispose()`.

4. ~~**Mixed responsibilities**: `ChallengesController` is both MonoBehaviour and business logic.~~
   **FIXED**: ChallengesController is now a plain C# class with constructor injection.

</details>

---

## Review #2 - Follow-up Review

### ChallengesView.cs

#### Critical Issues

1. ~~**Race condition: CancellationTokenSource not thread-safe**~~ (Lines 90, 156-162)
   **FIXED**: Added `_disposed` volatile flag with lock in `Dispose()`. New `ThrowIfDisposedOrCancelled()` helper checks disposed state before accessing `_cts.Token`, preventing `ObjectDisposedException`.

2. ~~**Null reference in UpdateSelectedChallengePanel**~~ (Line 532)
   **FIXED**: `ChallengesController.SelectChallengeAsync` now returns empty list instead of null. Follows best practice of never returning null for collections.

3. **Fire-and-forget async pattern** (Line 114)
   ```csharp
   _ = InitializeAsync();
   ```
   Discarding the task means exceptions are silently swallowed even with try/catch (unobserved task exceptions).

#### Medium Issues

4. **Tight coupling to static AccountSwitcher** (Lines 112, 404, 716, 859)
   View has direct static dependency on `AccountSwitcher`. Violates dependency injection principles and makes testing harder.

5. **No validation on user input in CreateChallenge** (Lines 711-743)
   No validation of `_modalNameField.value` (could be empty), `_modalMaxParticipantsField.value`, or parsed invitee IDs before calling controller.

### ChallengeView.cs

#### Medium Issues

6. **Missing null checks on UI elements** (Lines 33-40)
   `SetVisualElement` queries UI elements but doesn't validate they exist:
   ```csharp
   _nameLabel = visualElement.Q<Label>("name"); // Could be null
   ```
   If UXML template is missing elements, `SetChallenge` will throw NullReferenceException.

### ChallengeParticipantView.cs

#### Medium Issues

7. **Missing null checks on UI elements** (Lines 30-36)
   Same issue as ChallengeView - no validation after `Q<>()` queries.

### WalletDisplay.cs

#### Medium Issues

8. **CancellationTokenSource lifecycle risk** (Lines 60-69)
   If `HandleWalletUpdatedAsync` is called rapidly, the old CTS is disposed while the previous task may still be using its token, potentially causing `ObjectDisposedException`.

### AccountSwitcherEditor.cs

#### Medium Issues

9. **Editor window null reference risk** (Lines 57, 110)
   Accesses `HiroCoordinator.Instance` which could be null when not in play mode or after domain reload. While null checks exist, the code pattern is fragile.

### ChallengesController.cs

#### Low Issues

10. **No event notification on account switch** (Lines 53-59)
    `SwitchCompleteAsync` clears state but doesn't notify observers. View must manually call `RefreshChallengesAsync`.

### General

#### Low Issues

11. **No timeout on async operations**
    Async operations like `RefreshChallengesAsync` have no timeout. Spinner could spin forever if server hangs.

12. **Magic strings for date format** (ChallengesView.cs:556)
    ```csharp
    endTime.ToString("MMM dd, HH:mm")
    ```
    Should be a constant.

---

## What is Strong

- **Clean MVP Architecture**: Proper separation between Controller (business logic), View (UI), and ViewBehaviour (MonoBehaviour lifecycle)
- **Dependency Injection**: Constructor validation with `ArgumentNullException`
- **Async/Await Patterns**: Proper cancellation token support and error handling
- **UI Toolkit Best Practices**: `RequireElement` pattern for fail-fast on missing UI
- **Comprehensive Tests**: Integration tests cover real user flows
- **Resource Cleanup**: Proper `IDisposable` implementation throughout
- **Extension Methods**: Clean `Show`/`Hide`/`SetDisplay` helpers
- **User-Friendly Errors**: `ShowError()` pattern for consistent error display

---

## Summary

### Review #1
| Severity | Original | Remaining |
|----------|----------|-----------|
| Critical | 16 | 0 |
| Medium | 10 | 0 |

### Review #2
| Severity | Count |
|----------|-------|
| Critical | 1 |
| Medium | 6 |
| Low | 3 |

### Recommended Priority

1. Fix `_cts` race condition - add synchronization or use single-operation pattern
2. Add null check for `participants` in `UpdateSelectedChallengePanel`
3. Add null validation for UI elements in `ChallengeView` and `ChallengeParticipantView`
4. Review `WalletDisplay` CTS lifecycle for rapid updates
5. Add input validation in `CreateChallenge`
