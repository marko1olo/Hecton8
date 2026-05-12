# Rationale_CORE_SCAVENGING_CRAFTING

Status: PENDING VERIFICATION

## Decision 0 - Task Scoping

Problem: Crafting and inventory prompt requires replacing string/object-driven checks with hash/bitmask/native layout while other agents may concurrently edit adjacent systems.
Solution: Restrict implementation to the scavenging/inventory/crafting domain; discover existing contracts first; use numeric hashes, NativeArray-backed S.O.A. storage, and EventBus/GlobalRegistry boundary points instead of concrete cross-domain dependencies.
Rejected Alternatives: Full inventory architecture rewrite before reconnaissance rejected because it risks cross-domain drift. Direct world-instantiation from inventory rejected by prompt and GlobalRegistry/EventBus mandates.
Scalability potential: Low uses O(1) bitmask fail for recipe scans and compact native arrays. Middle adds count verification only after mask hit. High uses Burst jobs for defrag/condition decay. Ultra spends saved CPU on richer inventory/world-drop presentation owned by presentation/world systems.
Hardware Impact: i3/MX350 target gains from removing string comparisons and object list traversal in craft checks; expected savings are microsecond-scale for recipe scans, pending profiler proof.

## Decision 1 - Cached Mask Before Count Verification

Problem: Existing crafting validation copied/count-built local inventory before proving that a recipe's ingredient classes were even present.
Solution: Cache `RecipeData.RecipeMask` and `PlayerInventory.CurrentInventoryMask`; use `InventorySoAUtility.CanCraftFast()` before native count copying. Networked fabricators are treated as a correctness exception because connected storage can satisfy missing local bits.
Rejected Alternatives: Always building `NativeParallelHashMap<int,int>` first was too slow for failed recipes. Rejecting recipes solely on local mask when a power/logistics grid exists would be incorrect.
Scalability potential: Low/MX350 gets one scalar AND for impossible recipes. Middle/High can browse larger recipe sets without slot scans. Ultra can spend saved CPU on richer fabricator hologram filtering or blueprint preview effects.
Hardware Impact: Expected 3-15 us saved per failed local recipe at 48 inventory slots by skipping native map clear/fill and count loop; profiler proof pending.

## Decision 2 - SOA Mirror Without Grid Rewrite

Problem: Prompt requires `ItemHashes`, `ItemCounts`, and `ItemCondition`, but current inventory authority is a shaped grid with existing save/UI contracts.
Solution: Add persistent SoA mirrors to `PlayerInventory` and refresh them at mutation seams. Counts reuse the existing stack-count native lane. The grid remains authoritative for placement.
Rejected Alternatives: Replacing `InventoryGrid` with a dense container was rejected as architecture drift and save/UI breakage. Scanning the grid per craft check was rejected as O(N) hot validation.
Scalability potential: Low uses the mirrors for zero-GC UI reads and mask checks. Middle/High can add dense container transfer jobs. Ultra can layer visual overkill in UI/fabricator without changing gameplay truth.
Hardware Impact: Mutation sync costs microseconds and removes repeated recipe-browse scans. MX350/i3 benefit is strongest when many locked recipes are displayed.

## Decision 3 - Compile Boundary

Problem: First compile pass failed before reaching a clean project state.
Solution: Kept changes scoped and recorded dependency blockers instead of editing unrelated domains.
Rejected Alternatives: Fixing AUP, survival physiology, boid, tether, and world interfaces from this agent was rejected as cross-domain sabotage risk.
Scalability potential: No runtime scalability change; this is integration hygiene.
Hardware Impact: None until dependency owners restore compile.

## Decision 4 - Hash Lookup and Mask Classing

Problem: Recipe validation and inventory lookup cannot rely on strings or managed `ItemData` comparisons in the hot path.
Solution: Use `ItemData.PersistentHashId` from the existing FNV-1a `LocHash` path, cache `RecipeMask`, and route hash-to-material-bit resolution through `InventoryMaterialMask`.
Rejected Alternatives: Adding new string IDs or comparing `PersistentId` in `CanCraft` was rejected because it repeats managed text work during recipe browsing. A larger 128-bit mask was rejected for this pass because the prompt specified `ulong`.
Scalability potential: Low uses one 64-bit mask. Middle can layer count verification only after mask pass. High/Ultra can expand authoring into denser material classes without changing craft validation shape.
Hardware Impact: i3/MX350 saves L1 cache pressure and avoids string compare branches; expected savings remain microsecond-scale per failed recipe batch.

## Decision 5 - Drop Spawn Boundary

Problem: PDA inventory drop directly spawned world prefabs through the object pool, making inventory/UI own physical hydration.
Solution: Move the discard operation to `PlayerInventory.TryDropOneItemToWorldSignal`; it registers the drop with `PersistentWorldRegistry`, publishes an unmanaged `InventoryPhysicalDropRequestPayload`, then emits existing interaction/modding discard signals.
Rejected Alternatives: Keeping the direct `ObjectPoolManager.Spawn` fallback was rejected because inventory presentation would remain coupled to prefab instantiation. Inventing a hard dependency on a future world drop listener was rejected; the existing registry/service seam is available now.
Scalability potential: Low hydrates only persistent records near the player. Middle/High can consume the unmanaged payload for richer drop VFX/audio. Ultra can add visual overkill through world presentation without inventory changes.
Hardware Impact: No direct frame win claimed; prevents duplicate spawn paths and keeps cheap devices on the persistent hydration path.

## Decision 6 - SoA Transfer and Defrag Scope

Problem: The prompt requires defragmentation and bulk transfer, but `InventoryGrid` remains a shaped spatial authority.
Solution: Implement reusable SoA utilities for dense hash/count/condition buffers: tombstone compaction and guarded `UnsafeUtility.MemCpy` transfer. Keep shaped grid mirrors slot-stable.
Rejected Alternatives: Compacting live player grid anchors was rejected because it would relocate UI/save coordinates and break reservations. Element-wise transfer loops were rejected for identical dense storage ranges.
Scalability potential: Low keeps player grid stable. Middle can use dense transfer buffers for storage. High/Ultra can move larger storage containers through MemCpy while spending saved CPU on presentation.
Hardware Impact: MX350/i3 gains are transfer-size dependent; 48-slot player grid stays microsecond-scale, bulk containers avoid per-slot branch overhead.

## Decision 7 - FrostTick Condition Job

Problem: Perishable item condition needs a native lane that UI and future storage can read without managed DTO churn.
Solution: Add `FrostTickConditionDecayJob` over `NativeArray<uint>` hashes and `NativeArray<float>` condition, with perishable hashes provided as a native table and saturating decay.
Rejected Alternatives: Coroutine spoilage and string-tag checks were rejected. Replacing existing environmental degradation wholesale was rejected because it owns gameplay quality/state transitions and would be a behavioral rewrite.
Scalability potential: Low uses small perishable hash tables. Middle can schedule the job for storage containers. High/Ultra can add richer per-item visual spoilage from the same condition lane.
Hardware Impact: Expected microsecond-scale for 48 slots with a small perishable table; no profiler proof due compile blocker.

## Decision 8 - Verification Wall

Problem: Final compile cannot prove the whole assembly while unrelated domains fail first.
Solution: Ran `dotnet build` after the scavenging changes. Initial polish build exposed the generated project file did not include the new `InventorySoAUtility.cs`; added the compile include and reran. Current external blockers are platform/save/audio/native bridge symbols such as `HectonPersistentPathPolicy`, `HardwareTierDetector`, `HectonThreadPriorityPolicy`, `HectonNativeBridge`, and `SteamDeckInputPal`.
Rejected Alternatives: Editing platform policy, save, audio bridge, and hardware tier contracts was rejected as out-of-domain and unsafe during parallel agent execution.
Scalability potential: No runtime scalability change; verification integrity is preserved.
Hardware Impact: None.

## OMEGA POLISH CHANGES

Problem: Anti-bloat pass required proof that the new inventory/crafting code did not add string formatting, sqrt/normalize, direct spawning, or hidden project-file compile gaps.
Solution: Added `InventorySoAUtility.cs` to `Hecton8.Core.csproj` so the generated build surface can see the new utility during this validation pass. Reran `dotnet build`; CORE_SCAVENGING_CRAFTING symbol errors cleared. Ran `git diff --check` and a targeted `Select-String` scan over touched files.
Rejected Alternatives: Hiding the utility inside an unrelated existing source file was rejected because the file boundary is clean and Unity will compile it as an asset after refresh. Claiming Unity regeneration would fix the `.csproj` without local proof was rejected.
Scalability potential: Low keeps all hot validation on bitmasks and scalar cached reads. Middle uses NativeArray read surfaces and dense SoA transfers. High uses Burst defrag/condition jobs. Ultra spends saved CPU on richer world-drop presentation through the unmanaged payload.
Hardware Impact: i3/MX350 keeps failed recipe checks in scalar bitwise work; physical drop path no longer performs direct prefab spawn from UI/inventory; no measured profiler data due external compile wall.

Exact cinematic cheats used:
- 64-bit hash-derived material mask as a cheap broad-phase, followed by exact count verification for correctness.
- Condition decay as saturating scalar lane math instead of per-item managed behavior.
- Persistent-world drop registration and unmanaged payload signal instead of immediate prefab construction in inventory/UI.

Polish scan result:
- `git diff --check` passed, with line-ending warnings only.
- Targeted bloat scan found no new `foreach`, `string.Format`, `$"..."`, `math.sqrt`, `Mathf.Sqrt`, `math.normalize`, or `.normalized` in touched files.
- One pre-existing cold `RecipeData` `StringBuilder.ToString()` cost-summary line remains untouched.

## Decision 9 - R&D Consistency Fixes

Problem: Second-pass code reading found two local correctness risks. The guarded SoA bulk-copy helper could copy hash lane before a later lane guard failed, producing a partially transferred dense range. The inventory mask refresh treated a nonzero grid anchor with zero stack count as absent, while the existing exact-count craft path normalizes that same dirty state as one item.
Solution: Added all-lane `UnsafeMemoryCopyGuard.CanCopy` preflight before the first byte of `TryBulkCopyIdenticalItems`. Updated `RefreshInventorySoAMirrorsAndMask` so missing anchors clear hash/count/condition tombstones and nonzero hash anchors with zero count self-heal to one before mask OR. Routed `TryCopyAvailableItemCountsNonAlloc` through `InventoryMaterialMask.ResolveBit` instead of an open-coded shift.
Rejected Alternatives: Leaving the partial-copy order was rejected because a development-build guard exception between lanes would corrupt transfer state. Treating zero-count anchors as absent was rejected because it contradicts the existing exact-count path and can false-fail recipes after legacy/dirty save state. Rewriting `InventoryGrid` ownership was rejected because shaped grid placement remains the authority.
Scalability potential: Low keeps mask/counter semantics deterministic even on dirty saves. Middle/High can reuse the SoA bulk-copy helper without transactional repair code. Ultra can layer denser storage transfer visuals because the data lane either fully copies or does not copy.
Hardware Impact: i3/MX350 gets no claimed frame-time win from this pass; the gain is correctness. Prevented recovery cost is one failed craft browse path or one partial dense transfer bug, both microsecond-scale but high-risk.

R&D verification:
- Re-extracted `<AGENT_PROMPT id="CORE_SCAVENGING_CRAFTING">` from `Docs/Tasks/CURRENT_BATCH.md` by CLI regex after the batch file format proved that attributes follow `id`.
- `git diff --check` passed with line-ending warnings only.
- Targeted bloat scan still found only the pre-existing cold `RecipeData` cost-summary `ToString()`.
- Unity MCP `validate_script` reports 0 errors/0 warnings for `InventorySoAUtility.cs`, `PlayerInventory.cs`, and `CraftingSystem.cs`.
- `dotnet build Hecton8.Core.csproj --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -v:minimal -clp:Summary` fails with 76 unrelated errors in platform/save/audio/native bridge/scatter/boid dependencies; no CORE_SCAVENGING_CRAFTING file errors were emitted.
