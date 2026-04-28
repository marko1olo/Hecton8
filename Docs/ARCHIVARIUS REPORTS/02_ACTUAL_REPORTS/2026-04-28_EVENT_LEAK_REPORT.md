# EVENT_LEAK_REPORT.md — HectonEventBus Subscription Audit
**Status:** ⚠️ PARTIAL LEAK RISK DETECTED  
**Scan Date:** 2026-04-28  
**Scope:** All `HectonEventBus.Subscribe<T>` calls in `Assets/_Project/Scripts/`

---

## Methodology
`HectonEventBus.Subscribe<T>()` returns an `IDisposable` token. If the token is stored but `.Dispose()` is never called in `OnDisable`/`OnDestroy`, the handler remains rooted → **memory leak** on scene teardown or object pool despawn.

## Subscriptions Inventory

| Subscriber File | Event Type | Token Stored? | OnDisable Unbind? | Risk |
|-----------------|------------|---------------|-------------------|------|
| `QuestManager.cs` | `ItemCollectedEvent`, `BiomeDiscoveredEvent`, `LoreAcquiredEvent` | ✅ Yes | ⚠️ UNVERIFIED | **MEDIUM** |
| `RunModifierController.cs` | `PlayerDiedEvent`, `GameLoadedEvent` | ✅ Yes | ❌ NO (only SaveManager.Unregister) | **HIGH** |
| `PDAContextualAdvisorySystem.cs` | `GameLoadedEvent`, `PlayerSpawnedEvent` | ✅ Yes | ⚠️ UNVERIFIED | **MEDIUM** |
| `PlayerAchievementRegistry.cs` | `ItemCraftedEvent`, `GameLoadedEvent` | ✅ Yes | ⚠️ UNVERIFIED | **MEDIUM** |
| `GlobalProfileManager.cs` | `AchievementUnlockedEvent`, `BiomeDiscoveredEvent`, `GameLoadedEvent`, `PlayerDiedEvent`, `ItemCollectedEvent`, `ItemCraftedEvent`, `ItemRecycledEvent` | ✅ Yes | ❌ NO (only FlushCurrentRunRecords) | **HIGH** |
| `DynamicDifficultyDirector.cs` | `AchievementUnlockedEvent`, `PlayerAdvisoryIssuedEvent`, `BiomeDiscoveredEvent`, `GameLoadedEvent`, `PlayerDiedEvent` | ✅ Yes | ✅ `UnbindOwnerSubscriptions()` called | LOW |
| `PDALogbookManager.cs` | `ItemCraftedEvent`, `GameLoadedEvent`, `PlayerSpawnedEvent` | ✅ Yes | ✅ `UnsubscribeFromOwners()` called | LOW |
| `SubtitleManager.cs` | `SubtitleEventBus.OnPlaybackEvent` | N/A (static event) | ✅ `-=` in `OnDisable` | LOW |
| `EnvironmentalStrainManager.cs` | `ItemRecycledEvent`, `ItemDiscardedEvent` | ✅ Yes | ⚠️ UNVERIFIED | **MEDIUM** |
| `Meta/RunModifierController.cs` | (see above) | — | — | **HIGH** |

## Critical Leaks

### 1. `GlobalProfileManager.cs`
- Stores 7 subscription tokens.
- `OnDisable` calls `FlushCurrentRunRecords()` — **does NOT dispose EventBus tokens**.
- If this singleton is ever destroyed or scene-reloaded, handlers stay rooted.

### 2. `RunModifierController.cs`
- Stores `_playerDiedSubscription`, `_gameLoadedSubscription`.
- `OnDisable` only calls `SaveManager.Instance?.Unregister(this)`.
- **Missing:** `_playerDiedSubscription?.Dispose(); _gameLoadedSubscription?.Dispose();`

## Recommended Fix Pattern
```csharp
private void OnDisable()
{
    _playerDiedSubscription?.Dispose();
    _playerDiedSubscription = null;
    _gameLoadedSubscription?.Dispose();
    _gameLoadedSubscription = null;
}
```

## Verdict
- **High Risk:** 2 files (`GlobalProfileManager`, `RunModifierController`)
- **Medium Risk:** 4 files (unverified unbind path)
- **Low Risk:** 3 files (explicit unbind confirmed)
- **Status:** PENDING VERIFICATION via manual code read of `OnDisable` bodies.
