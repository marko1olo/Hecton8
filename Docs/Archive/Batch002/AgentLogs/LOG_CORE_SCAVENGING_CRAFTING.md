# LOG_CORE_SCAVENGING_CRAFTING

## 2026-05-11 - Bitmask Crafting and SoA Inventory Pass

What was wrong:
- Craft validation performed count-copy work before proving that required material classes existed.
- Inventory did not expose the requested hash/count/condition SoA read surface.
- Recipe data had no cached `ulong` recipe mask.
- PDA inventory drop path directly spawned world prefabs through the object pool fallback.
- New SoA utility source initially was not included in `Hecton8.Core.csproj`, causing `InventorySoAUtility` symbol errors during dotnet validation.

What was done:
- Added `NativeArray<uint> _itemHashes` and `NativeArray<float> _itemCondition` mirrors to `PlayerInventory`; existing `NativeArray<ushort> _stackCounts` is the count lane.
- Added `PlayerInventory.CurrentInventoryMask`, refreshed at `NotifyInventoryChanged`.
- Added read-only NativeArray and unsafe pointer hooks for hashes/counts/condition.
- Added `InventoryMaterialMask` and `RecipeData.RecipeMask`.
- Changed `CraftingSystem.CanCraft` to broad-phase with `InventorySoAUtility.CanCraftFast(currentMask, recipeMask)` before local count-map copy when no logistics grid can satisfy missing materials.
- Added `InventorySoAUtility.DefragmentJob`, `TryBulkCopyIdenticalItems`, `FrostTickConditionDecayJob`, and `ResolveStackInsert`.
- Replaced PDA direct drop spawn with `PlayerInventory.TryDropOneItemToWorldSignal`, `PersistentWorldRegistry.TryRegisterDroppedItemWithState`, `InventoryPhysicalDropRequestPayload`, `InteractionEvents.RaiseItemLost`, and existing `ItemDiscardedEvent`.
- Added `Docs/AgentLogs/RECON_CORE_SCAVENGING_CRAFTING.md`.
- Added `Assets\_Project\Scripts\Inventory\InventorySoAUtility.cs` to `Hecton8.Core.csproj` for local dotnet validation.

Cinematic cheats used:
- 64-bit hash-derived material mask: cheap broad-phase, exact count verification retained for correctness.
- Condition decay is scalar saturating math over a float lane, not object behavior.
- World-drop hydration is delegated to persistent world/service payloads, not immediate prefab construction.

Exact microseconds saved:
- Failed local recipe check: estimated 3-15 us saved at 48 slots by skipping `NativeParallelHashMap` clear/fill and slot scan.
- Recipe mask read: estimated sub-0.1 us per recipe.
- Inventory mask read: estimated 0.02 us scalar read; mutation refresh estimated 4-12 us at 48 anchors.
- Mass query: estimated 0.02 us scalar read versus 3-15 us slot scan.
- Max-stack clamp: estimated sub-0.1 us per candidate stack.
- Bulk identical SoA transfer: expected tens of nanoseconds for small dense ranges; larger storage transfers avoid per-slot branch overhead.

Verification:
- `git diff --check` passed with line-ending warnings only.
- Targeted anti-bloat scan found no new `foreach`, `string.Format`, interpolation, sqrt, or normalize in touched files. One pre-existing cold `RecipeData` `StringBuilder.ToString()` remains.
- `dotnet build Hecton8.Core.csproj --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -v:minimal -clp:Summary` still fails outside this domain: missing platform/save/audio/native bridge symbols including `HectonPersistentPathPolicy`, `HardwareTierDetector`, `HectonThreadPriorityPolicy`, `HectonNativeBridge`, `SteamDeckInputPal`.
- No CORE_SCAVENGING_CRAFTING file errors remain after the project include fix.

Status:
- PENDING VERIFICATION.
- Task 15 is `[BLOCKED BY DEPENDENCY]` because the project build is not clean outside this domain.

## 2026-05-12 - Honest R&D Consistency Pass

What was wrong:
- `InventorySoAUtility.TryBulkCopyIdenticalItems` validated and copied lanes sequentially. A guard failure after the hash lane could leave a partial transfer.
- `PlayerInventory.RefreshInventorySoAMirrorsAndMask` treated a nonzero grid anchor with zero stack count as absent, while `TryCopyAvailableItemCountsNonAlloc` already treats that legacy/dirty state as one item. That mismatch could false-fail the new mask fast path.
- `TryCopyAvailableItemCountsNonAlloc` had an open-coded mask shift instead of the central `InventoryMaterialMask` resolver.

What was done:
- Added all-lane `UnsafeMemoryCopyGuard.CanCopy` preflight before any `TryMemCpy` call in the bulk SoA transfer helper.
- Made SoA mirror refresh clear hash/count/condition for missing anchors and normalize a nonzero hash anchor with zero stack count to one before computing `CurrentInventoryMask`.
- Routed available-resource mask construction through `InventoryMaterialMask.ResolveBit`.

Cinematic cheats used:
- Same 64-bit material-mask broad phase; exact count verification remains the correctness wall.
- No new visual simulation. This pass was deterministic data hygiene, not presentation work.

Exact microseconds saved:
- No new frame-time saving claimed. This pass prevents partial transfer repair and false craft rejection. The protected work is still microsecond-scale at 48 slots; correctness is the measurable value.

Verification:
- Re-extracted the `CORE_SCAVENGING_CRAFTING` XML block from `Docs/Tasks/CURRENT_BATCH.md` with CLI regex.
- `git diff --check` passed with line-ending warnings only.
- Targeted anti-bloat scan still only finds the pre-existing cold `RecipeData` `StringBuilder.ToString()` summary.
- Unity MCP `validate_script` reports 0 errors/0 warnings for `InventorySoAUtility.cs`, `PlayerInventory.cs`, and `CraftingSystem.cs`.
- `dotnet build Hecton8.Core.csproj --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -v:minimal -clp:Summary` still fails outside this domain with 76 errors: missing platform/save/audio/native bridge/scatter/boid symbols including `HectonPersistentPathPolicy`, `PlatformPrecisionClock`, `SteamDeckInputPal`, `HectonThreadPriorityPolicy`, `HectonNativeBridge`, `HectonNativeLibrary`, `UploadIndirectArgsStaticMeshData`, and `EnsureScatterTelemetryResources`.

Status:
- PENDING VERIFICATION.
