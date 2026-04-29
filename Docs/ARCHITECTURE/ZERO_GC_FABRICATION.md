# ZERO-GC Fabrication

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

If recursive intermediate fabrication is introduced later, the same flattened representation can be expanded into an in-degree table and processed with Kahn's algorithm without changing the inventory-count seam.

## Physical craft output

Craft completion does not inject the crafted result directly into the inventory anymore.

Runtime flow:

1. `Fabricator` resolves an output pose and `VelocityChange` vector from its output socket.
2. `PersistentWorldRegistry.TryRegisterDroppedItem(...)` registers the crafted item as a persistent world record.
3. Hydration uses the pooled item proxy path already used by dropped inventory items.
4. The hydrated `Rigidbody.mass` is set from `ItemData.MassKg`.
5. The queued `VelocityChange` ejects the item from the machine without per-frame force spam.

If the persistent world registry is unavailable, the fabricator falls back to direct inventory insertion to avoid silent item loss.

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
