# ZERO-GC Fabrication
Date: 2026-05-07

Status: PENDING VERIFICATION

<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_START -->
## 2026-05-17 R4 Interior Actuality Boundary

This document is active only where it agrees with:

- `Docs/README.md`
- `Docs/DOC_GOVERNANCE.md`
- `Docs/ARCHITECTURE/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md`
- current source files
- fresh verification logs and artifacts

No Unity import, Unity Console, Play Mode, profiler, GCMonitor, Memory Profiler, Frame Debugger, player build, save/load route, or visual-route proof is implied unless this document links a fresh evidence artifact. Historical counters and older version claims inside this file are subordinate to the current authority spine above.
<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_END -->

Verification: PENDING VERIFICATION

## 2026-05-11 Historical Override + 2026-05-17 Actuality Pointer

- Historical data boundary snapshot: `Docs/Reports/2026-05-11_DOCUMENTATION_CURRENT_DATA_CONTINUATION.md`.
- Historical manifest: `Docs/Reports/2026-05-11_ACTIVE_DOCUMENTATION_MANIFEST.json`.
- Current actuality manifest: `Docs/Reports/2026-05-17_ACTIVE_DOCUMENTATION_ACTUALITY_MANIFEST.json`.
- Current actuality ledger: `Docs/ARCHITECTURE/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md`.
- Visual-realistic-fake doctrine snapshot: `Docs/Reports/2026-05-11_AGENTS_SKILLS_VISUAL_FAKE_AUDIT.md`; re-check `.agents-skills` for newer mandates before implementation.
- Historical May 14/R43 CLI compile wording is stale report text, not current proof. Current R28 static/tool boundary: AtlasCheck fails `57` RealtimeCSG refs; Mod API static validation now passes (`Status=PASS`, `SchemaRevision=14`, `SourceSignals=160`, `ModCommandSizeBytes=64`). Unity import, Console, Play Mode, profiler, GCMonitor, player build, scene wiring, save/load, and visual proof remain PENDING VERIFICATION.
- Existing May 4 boundary sections in this file are historical unless they describe local system intent not contradicted by newer reports.
- Unity import, Unity Console, Play Mode, profiler, GCMonitor, player build, frame-time, memory, scene wiring, and visual quality remain `PENDING VERIFICATION`.
## Historical 2026-05-04 Boundary

- Read `Docs/Reports/2026-05-04_DOCUMENTATION_SORTING_AUTHORITY_MAP.md`, `Docs/Reports/2026-05-04_DOCUMENTATION_ACTUALITY_SWEEP.md`, `Docs/Reports/2026-05-04_WARNING_CLEANUP.md`, `Docs/Reports/2026-05-04_FOUNDATION_GUARD_UNSAFE_COPY_AND_MENU_LOOP_REPAIR.md`, and `Docs/Reports/2026-05-01_CURRENT_PROJECT_STATE.md` before using this fabrication contract as current runtime truth.
- This document is the intended allocation-free fabrication path, not proof of 0 B/frame, complete recipe coverage, or save/logistics integration.
- Re-open `Fabricator`, `CraftingSystem`, `PlayerInventory`, `BaseLogisticsNetwork`, and `PersistentWorldRegistry` before surgery.

## Purpose

This document defines the runtime recipe-resolution path for Fabricators in HECTON-8.
The goal is deterministic, allocation-free recipe checks against the SOA inventory owner and deterministic physical output synthesis through the persistent world registry.

Mandates followed:
- `DATA_Inventory_Resources_Items_SOA_Layout.txt`
- `OPT_Zero_GC_Policy_AllocFree_Mandate.txt`
- `OPT_Native_Memory_Collections_JobSystem_Protocol.txt`
- `ARCH_Global_Registry_ServiceLocator_DI_Init.txt`
- `PHYS_Physics_Integrity_Determinism_ForceMode.txt`
- `STRM_Persistent_Object_Registry.txt`

## Runtime ownership

- `RecipeData` remains the authored source of truth.
- `Fabricator` owns the per-station native scratch buffers used for recipe checks.
- `PlayerInventory` owns the SOA stack counts and exposes a non-alloc copy seam for available item totals.
- `PersistentWorldRegistry` owns crafted-item world synthesis and pooled hydration.

## Resolution buffers

Each `Fabricator` allocates its scratch buffers once in `Awake()` and releases them in `OnDestroy()`:

- `NativeParallelHashMap<int, int>`: accessible counts keyed by item hash.
- `NativeArray<int2>`: flattened recipe costs where `x = itemHashId`, `y = requiredAmount`.
- `NativeArray<byte>[1]`: Burst result cell.

These buffers are persistent and reused for every craftability query. No managed lists, LINQ, or transient arrays are created in the hot path.

## Recipe resolution math

### 1. Flatten authored ingredients

`CraftingSystem.TryBuildRecipeCostBuffer(...)` converts the managed recipe ingredient list into a contiguous `NativeArray<int2>`.

- Duplicate ingredient hashes are merged in-place.
- Adjusted ingredient costs are resolved before the buffer is evaluated.
- The current cap is `32` unique ingredient hashes per recipe.

### 2. Copy SOA inventory counts

`PlayerInventory.TryCopyAvailableItemCountsNonAlloc(...)` walks the anchor-backed stack arrays and aggregates available counts into a caller-owned `NativeParallelHashMap<int, int>`.

- Only anchored stacks are counted.
- Counts are keyed by shared item hash IDs.
- The map is cleared and reused by the caller.

### 3. Merge logistics-network counts

`CraftingSystem.MergeAccessibleNetworkCounts(...)` adds `BaseLogisticsNetwork.CountAccessibleItem(...)` results into the same native count map for every ingredient hash present in the flattened recipe buffer.

This preserves the existing fabricator rule: local inventory + linked network inventory are both valid supply sources.

### 4. Burst availability check

`EvaluateRecipeAvailabilityJob` runs a flat array check:

```csharp
for (int index = 0; index < recipeCostCount; index++)
{
    int2 cost = recipeCosts[index];
    if (!availableItemCounts.TryGetValue(cost.x, out int availableCount) ||
        availableCount < cost.y)
    {
        canCraft = 0;
        break;
    }
}
```

Current authored recipes are one-hop ingredient graphs, so a flat contiguous check is sufficient and cheaper than building an explicit dependency DAG every time.


## Physical craft output

Craft completion does not inject the crafted result directly into the inventory anymore.

Runtime flow:

1. `Fabricator` resolves an output pose and `VelocityChange` vector from its output socket.
2. `PersistentWorldRegistry.TryRegisterDroppedItem(...)` registers the crafted item as a persistent world record.
3. Hydration uses the pooled item proxy path already used by dropped inventory items.
4. The hydrated `Rigidbody.mass` is set from `ItemData.MassKg`.
5. The queued `VelocityChange` ejects the item from the machine without per-frame force spam.

If the persistent world registry is unavailable, the fabricator falls back to direct inventory insertion to avoid silent item loss.

## Deconstruction rules

The scrap grinder uses the same flat native recipe-cost representation in reverse.

Runtime ownership:

- `Fabricator.TryDeconstructItem(int itemHashId)` is the deconstruction entry point.
- `PlayerInventory.TryConsumeFirstMatchingItemByHash(...)` supplies the consumed stack state (`flags`, `qualityMilli`) without managed allocations.
- `CraftingSystem.TryBuildDeconstructionYieldBuffer(...)` converts one crafted result back into reclaimed `NativeArray<int2>` outputs.

Deconstruction math:

1. Resolve the crafted item's source `RecipeData` through the existing result-item reverse lookup.
2. Flatten the source ingredients into the same `NativeArray<int2>` cost buffer used by crafting.
3. Apply reclaim percentage in a Burst-compatible flat job.

```csharp
int scaledYield = (cost.y * reclaimPercent) / (safeResultQuantity * 100);
if (scaledYield <= 0 && reclaimPercent > 0)
    scaledYield = 1;
```

Reclaim percentages:

- Normal item: `80%`
- Degraded item (`IS_DEGRADED` bit set or `qualityMilli < 250`): `30%`

State-bit rule:

- `IS_DEGRADED` is bit `8` in the item-state SOA bitfield.
- Rust moved off bit `8` so deconstruction and impact damage do not alias the degraded state.

Physical salvage output:

1. The fabricator resolves a dedicated catch-bin pose when authored, otherwise falls back to the normal output socket.
2. Each reclaimed ingredient stack is registered through `PersistentWorldRegistry.TryRegisterDroppedItem(...)`.
3. The hydrated rigidbody inherits `ItemData.MassKg`.
4. Reclaimed stacks receive one authored `VelocityChange` burst so they pop into the bin and settle.

## Carry-mass propagation

`PlayerInventory` now computes `TotalMassKg` from SOA stack counts and per-item `MassKg`.

This scalar is propagated into:

- `HectonSurvivalSystem.SetWeight(...)` for oxygen/energy load penalties.
- `HectonPlayerMovement.SetRuntimeInventoryLoadMovementMultiplier(...)` for runtime movement slowdown.

The movement path subscribes to `PlayerInventory.InventoryChanged`, resolves a normalized carry-load factor, and applies it through existing runtime multipliers instead of branching a second locomotion implementation.

## Zero-GC notes

- Native scratch buffers are cold allocations only.
- Recipe availability checks reuse persistent native memory.
- No LINQ, `List<T>` creation, or per-check array allocation is permitted.
- Crafted world output reuses the pooled hydration pipeline owned by `PersistentWorldRegistry`.
