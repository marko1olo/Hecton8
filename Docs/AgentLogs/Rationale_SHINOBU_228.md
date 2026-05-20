# SHINOBU_228 Rationale - BUILDER_TOOL_HOLOGRAPHY_SYNC

Date: 2026-05-20
Status: PENDING VERIFICATION

## Decision 001 - Authority Surface

Problem: The prompt path `Assets/_Project/Scripts/Tools/Builder/` is absent. Existing authority lives in root `PlayerBuilder.cs`, root `PlacementGhost.cs`, and `Assets/_Project/Scripts/Construction/`.
Solution: Treat `PlayerBuilder` as the input/placement owner, `Construction/*Socket*` as the Burst math owner, and `HectonBlueprintPreviewBatch` as the presentation owner. Use existing `SignalBus<ConstructionPreviewSignal>` instead of adding a new one-off global lane.
Rejected Alternatives: Creating a new `Tools/Builder` tree would duplicate authority and break Global Authority Boundaries. Directly adding a new `GlobalRegistry` service was rejected because the route lacks a GREEN route card.
Scalability potential: Low uses one matrix and minimal shader ALU; Middle keeps full SDF/AABB validation; High adds richer scanline/noise; Ultra spends saved CPU on visual overkill only in presentation.
Hardware Impact: Expected low-end i3/MX350 gain is removal of PhysX broadphase preview checks and ghost spawn/despawn spikes; measured proof absent until Unity profiler.

## Decision 002 - Ghost Prefab Lifecycle Is Not Placement Truth

Problem: `PlayerBuilder.SpawnGhost()` currently spawns pooled authored ghost prefab or runtime proxy, then `PlacementGhost.FixedTick()` performs PhysX overlap validation.
Solution: Replace hot placement truth with unmanaged DTO state and validation flags. Keep final module spawning untouched because placed modules are gameplay objects, not preview ghosts.
Rejected Alternatives: Keeping pooled ghost prefab was rejected because object pooling still mutates Transform/colliders and leaves broadphase cost. Raw `Instantiate()` is not currently used in `PlayerBuilder`, but pooled preview objects still violate the mission.
Scalability potential: Low no ghost object, one DTO; Middle/High/Ultra scale hologram shader only through `GlobalQualityWeight`.
Hardware Impact: Removes collision proxy churn and renderer material swaps from preview path; exact microseconds pending profiler.

## Decision 003 - SDF/AABB Validation Over PhysX Preview Overlap

Problem: `PlacementGhost.FixedTick()` uses `Physics.OverlapBoxNonAlloc` every fixed tick. NonAlloc avoids heap but still touches PhysX broadphase and moving collider state.
Solution: Implement Burst OBB corner checks against SDF samples and existing module AABBs, with flags in a 128-byte DTO. Use existing socket construction buffers where possible.
Rejected Alternatives: `OverlapBoxNonAlloc` retained as "good enough" was rejected; it is still a broadphase cost and requires the ghost GameObject.
Scalability potential: Low checks minimal SDF samples but keeps absolute validity; higher tiers increase visual details, not correctness toggles.
Hardware Impact: Expected 60-180 us/frame saved in preview-heavy scenes on i3/MX350; measured proof absent.

## Decision 004 - DTO Layout

Problem: Builder preview state crosses Burst/GPU/telemetry boundaries and includes `double3`; layout ambiguity risks defensive copies or ARM64 alignment faults.
Solution: Use `[StructLayout(LayoutKind.Explicit, Size = 128)]` for `BuilderGhostStateDTO`: `float4x4` at 0, `double3` at 64, hashes/flags at 88-100, seven uint padding fields at 100-124.
Rejected Alternatives: Sequential layout and properties were rejected. Smaller 96-byte DTO was rejected because the prompt requires a 128-byte envelope and GPU-friendly alignment.
Scalability potential: One stable payload supports Low through Ultra without branching layouts.
Hardware Impact: 8-byte aligned `double3` avoids ARM64 unaligned access risk; cache footprint is fixed and predictable.
