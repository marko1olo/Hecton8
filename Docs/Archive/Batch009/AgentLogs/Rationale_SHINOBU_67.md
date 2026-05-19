# SHINOBU_67 Rationale - Modular Base Construction Validator

Date: 2026-05-19
Status: PENDING VERIFICATION - OCCUPANCY VAULT LOCK ADDED, BUILD NOT RERUN BY USER ORDER

## Session Boundary

Problem: User requested Agent 67 as modular base validator. `CURRENT_BATCH.md` contains two `SHINOBU_67` blocks; the first matching construction validator is the active directive for this task, while the later Addressables block is ignored as conflicting duplicate prompt data.
Solution: Extracted the specific `<AGENT_PROMPT id="SHINOBU_67" role="MODULAR_BASE_CONSTRUCTION_VALIDATOR">` block via PowerShell regex and bound the domain to WFC grid / SDF collision / SIP costs / inventory deduction.
Rejected Alternatives: Following the later Addressables duplicate would violate the explicit user goal and construction task body. Guessing without extraction would violate batch protocol.
Scalability potential: Low uses strict grid/AABB/SDF center/corner probes; Middle/High/Ultra increase SDF probe density, richer telemetry, and visual diagnostic overlays while preserving one mathematical validator.
Hardware Impact: Avoiding `Physics.OverlapBox` removes broadphase traversal and managed collider surfaces from build dragging; expected gain is microseconds per preview validation on i3/MX350, pending profiler proof.

## Pre-Code Decision Journal

Problem: Bases clipping into mountains require authoritative terrain collision without Unity physics.
Solution: Use local-AUP AABB sampling against an injected SDF sampler interface/mock and grid occupancy maps. Use continuous `GlobalQualityWeight` only to vary probe count/clearance detail, not to bypass authority entirely.
Rejected Alternatives: `Physics.OverlapBox`, prefab instantiation for preview, mesh collider checks, and concrete terrain class dependencies are rejected because they allocate or stall and couple to systems owned by other agents.
Scalability potential: Low probes center + bottom footprint; Middle probes eight AABB corners; High adds face centers; Ultra adds edge midpoints and richer red-box gizmo diagnostics.
Hardware Impact: Grid hash lookup plus 1-26 SDF samples should stay below 0.1 ms for preview drag on weak CPUs; exact timing is PENDING VERIFICATION.

Problem: Resource deduction must integrate with inventory without owning inventory internals.
Solution: Use an unmanaged construction/crafting request signal or existing typed inventory contract if present; commit only after transaction success. Keep rollback deterministic.
Rejected Alternatives: Direct `PlayerInventory` concrete calls, string item names, managed dictionaries, or immediate local subtraction are rejected as cross-domain ownership violations.
Scalability potential: Low emits compact cost hashes; High/Ultra can expose richer preview cost breakdown through editor/UI lanes without changing core transaction truth.
Hardware Impact: Numeric hash/cost arrays avoid per-build string/lookup overhead and remove GC risk.

## Implementation Decisions

Problem: `PlayerBuilder.UpdateTerrainSdfPlacementState` used `Physics.OverlapBoxNonAlloc` against terrain/vehicle/debris layers, so construction correctness depended on collider broadphase and missed non-collider SDF terrain.
Solution: Replaced the overlap with `ModularBaseConstructionValidator` request/bounds/settings/SIP DTOs. The builder now constructs a root-AUP-local request, runs AABB/SDF math, and additionally probes published voxel SDF density through `HectonVoxelVolume.GetSDFDensity` for active cave/terrain volumes. The validation block reason is now `TERRAIN SDF`, not collider obstacle.
Rejected Alternatives: Keeping layer masks, moving to `OverlapBoxCommand`, or using mesh collider checks are all rejected because they stay in physics/collider ownership and do not solve non-collider mountain clipping.
Scalability potential: Low uses one center probe via continuous quality weight; Middle/High/Ultra scale to more AABB probes and editor red boxes without changing the gameplay contract.
Hardware Impact: Removes one physics broadphase query per drag validation. Expected win is 8-35 us on weak CPUs, but exact profiler data is PENDING.

Problem: The required construction request layout was arithmetically easy to get wrong and any bool/property DTO would fail ARM64/DOD rules.
Solution: Added explicit-layout DTOs: `ConstructionRequestDTO` 64B, `StructuralBoundsDTO` 32B, `ConstructionValidationSettingsDTO` 32B, `ConstructionValidationResultDTO` 32B, `BaseModuleOccupancyDTO` 32B, and `ConstructionTelemetryEntry` 64B. Jobs use raw fields only.
Rejected Alternatives: `Pack=1`, managed classes, `bool IsValid`, properties, and object references were rejected.
Scalability potential: Same DTOs feed low-tier scalar checks and high-tier diagnostic overlays.
Hardware Impact: Fixed-size lanes keep validation cache-local and Burst-compatible.

Problem: Agent 41 `GlobalWorldSamplerData` has a strong Burst surface but no active runtime lease visible to `PlayerBuilder`.
Solution: The validator owns a local `MockWorldSampler` and a real adapter path through published voxel SDF density. The mock path proves the terrain-distance contract; the actual builder also checks active SDF volumes without Unity physics.
Rejected Alternatives: Creating a hard `GlobalWorldSampler` singleton or reading private sampler state would be cross-domain coupling.
Scalability potential: When a real sampler lease is exposed, the same `ConstructionRequestDTO` can route to `GlobalWorldSampler.SampleDistanceOnly` without changing builder UI/commit code.
Hardware Impact: Current path is 1-9 probes; no allocations and no collider broadphase.

Problem: Agent 20 HullIntegrity ledger DTO lives behind a separate Habitat.Deformation asmdef, and referencing it from `PlayerBuilder` broke `Hecton8.Core.csproj`.
Solution: Removed the direct dependency. SIP preflight now uses `ConstructionSipBudgetDTO` with local budget/yield/volume/depth fallback. The Burst contract is ready for a proper ledger handoff when a first-party interface is exported.
Rejected Alternatives: Adding Core -> Habitat.Deformation dependency or duplicating `BaseIntegrityLedgerDTO` would create assembly cycles or layout drift.
Scalability potential: Low gets scalar warning only; High/Ultra can show richer yellow diagnostic overlays from the same pressure ratio.
Hardware Impact: One scalar pressure ratio, effectively free.

Problem: The prompt requested `CraftingRequestSignal`, but source inspection found no production signal; only `MockCraftingRequestSignal` exists.
Solution: Kept the existing production transaction seam: `HabitatConstructionManager.HasBuildResources` and `ConsumeBuildResources` own inventory deduction and rollback. No mock crafting lane is used in production construction. Self-audit found the debug deploy path ignored `ConsumeResources` failure after spawn, so that path now destroys the spawned module and returns false on transaction failure.
Rejected Alternatives: Inventing `CraftingRequestSignal` or publishing `MockCraftingRequestSignal` would be fake integration and cross-domain sabotage.
Scalability potential: Inventory can later expose a proper unmanaged reservation signal without changing the validator math.
Hardware Impact: No new per-frame inventory overhead; deduction remains click-only.

Problem: Human tuning required direct control over grid snap, max bounds, and terrain clearance.
Solution: Added `WfcBuilderTunerWindow` and reserved DataVault IDs `ConstructionBuilderTuning`, `ConstructionBuilderTelemetry`, `ConstructionBuilderBounds`; tuner writes `ConstructionValidationSettingsDTO` into the vault, cold-loads `Data/Construction/module_bounds.csv` through the span parser, and draws SceneView grid/rejection boxes.
Rejected Alternatives: Inspector-only fields and debug GameObjects were rejected.
Scalability potential: Weak hardware uses the same vault settings but fewer probes; high-end devices can push clearance/gizmo diagnostics visually harder.
Hardware Impact: Editor-only.

Problem: Ultra-polish preflight proved the old implementation still treated Vault integration as a report claim, not an executable memory contract.
Solution: Added `TryResolveTelemetryRing`, `TryResolveBoundsOverrideBuffer`, `TryParseModuleBoundsCsvToVault`, and `GenerateEmergencyMockBounds`. PlayerBuilder now writes one telemetry row to the `ConstructionBuilderTelemetry` Vault ring per terrain validation.
Rejected Alternatives: Persistent private `NativeArray` fields in the validator, managed per-frame logs, or leaving CSV overrides only in caller-owned hash maps.
Scalability potential: Bounds and telemetry now live in Vault lanes and can be consumed by lower-tier scalar validation and higher-tier editor/BRG diagnostics without recompilation.
Hardware Impact: One 64B telemetry write per placement validation; bounds hydration is cold-path only.

Problem: The previous quality curve did not fully satisfy the prompt's low-tier center-only collapse because `lerp(1,9,0.1)` rounded to two probes.
Solution: Changed probe budget to `ceil(lerp(1,9,step(0.3,q) * smoothstep(0.3,1.0,q)))`, so weights below 0.3 run only the center probe and high-tier reaches all nine probes.
Rejected Alternatives: Hard binary `if (quality < 0.3)` branch or a fixed eight-corner test on every device.
Scalability potential: Toaster path is center-only; middle/high/ultra breathe continuously into richer collision evidence.
Hardware Impact: At `GlobalQualityWeight=0.1`, terrain probe count drops from 2 to 1; static saving is one SDF query per drag validation.

Problem: The construction SDF cave adapter still violated the AUP rule after the previous pass. `TryFindVoxelSdfIntersection` computed `double3 probeAup = RootAUP + localProbe` and immediately reduced it to absolute `float3` before calling `HectonVoxelVolume.GetSDFDensity`. At 100km scale this can quantize the exact probe location and produce false terrain acceptance or rejection near cave/mountain boundaries.
Solution: Added a narrow runtime-space SDF facade to `HectonVoxelVolume`: `GetSDFDensity(double3, out float)` subtracts the floating-origin offset before float conversion, and `TrySampleRuntimeSdfDensity(Vector3, out float)` samples already-local runtime coordinates. `PlayerBuilder.TryFindVoxelSdfIntersection` now uses `HectonFloatingOrigin.ToRuntimePosition(probeAup)` and never hands absolute float AUP to the cave sampler.
Rejected Alternatives: Keeping the absolute `float3` AUP cast, reintroducing `Physics.OverlapBox`, or inventing a fake GlobalWorldSampler lease not present in runtime source.
Scalability potential: Low/Middle/High/Ultra use the same precision-safe probe path; only probe count breathes with quality weight.
Hardware Impact: Probe count and cache behavior are unchanged. Correctness gain is removal of large-coordinate float jitter in terrain rejection; expected frame delta is 0 us.

Problem: Live preview still called `ValidatePlacementNoOccupancy`, leaving occupied-cell rejection dependent on other semantic/socket paths instead of the SHINOBU_67 grid validator layer.
Solution: Added `PlayerBuilder.TryFindOccupiedConstructionGridCell`, a no-allocation AUP-local grid compare over `ConstructionManager.SpawnedModules`. It computes each registered module's grid coordinate relative to the same base RootAUP and raises `ConstructionValidationFlags.OccupiedGridCell` with block reason `GRID OCCUPIED`.
Rejected Alternatives: Allocating a `NativeParallelMultiHashMap` per drag validation, treating collider overlap as occupancy truth, or pretending a Vault occupancy mirror exists before the construction/logistics owner exports it.
Scalability potential: Low/Middle/High/Ultra all run the same integer grid compare; future Vault occupancy can replace the fallback with O(1) hash lookup without changing the public validator DTO.
Hardware Impact: O(moduleCount) integer math on preview drag; for a 500-room base this is hundreds of integer comparisons, not physics broadphase. Exact profiler proof is absent.

Problem: Occupancy and live voxel SDF evidence were applied after `ValidatePlacementNoOccupancy`, but `ResultHash` still represented the original no-occupancy/no-live-SDF result. That weakens black-box forensic value.
Solution: Added `ModularBaseConstructionValidator.ApplyFailureFlags()` and routed both live occupancy and voxel SDF failures through it. It updates `FailureFlags`, `OccupiedCellHash`, `MinSdfDistance`, `IsValid`, `StructuralWarning`, and `ResultHash` together.
Rejected Alternatives: Hand-patching fields in `PlayerBuilder`, ignoring hash drift, or recomputing hash in multiple call sites.
Scalability potential: Same helper works for low single-probe and ultra multi-probe modes; result hashing remains deterministic across quality weights.
Hardware Impact: One FNV-style hash recompute per detected failure; expected cost below measurable threshold.

Problem: Self-audit found two wrong assumptions after the first compile pass: the dump path used another agent ID and `ResolveGlobalQualityWeight` inverted `SystemHealthIndex01` even though the project already exposes `HomeostasisBrain.GlobalQualityWeight`.
Solution: Changed the dump target to `Docs/AgentLogs/Dump_SHINOBU_67.bin`, read `HomeostasisBrain.GlobalQualityWeight` directly, and trigger a one-shot telemetry dump when validation state becomes non-finite.
Rejected Alternatives: Keeping SHI inversion would be a hidden quality polarity bug. Keeping another agent dump name would break black-box ownership.
Scalability potential: Low/Middle/High/Ultra now consume the same canonical continuous quality scalar used by the scalability dictator.
Hardware Impact: No frame cost change in the finite path; dump I/O only runs on non-finite fault.

## Verification Notes

Problem: Project compile state changed during concurrent agent work.
Solution: Ran `dotnet build Hecton8.Core.csproj --no-restore` and `dotnet build Hecton8.Editor.csproj --no-restore`. Earlier pass reached clean Core/Editor output after SHINOBU_67 symbol fixes. Later pass stopped on external `EconomyRuntimeInstaller.cs` missing `TradeMarauderDirector`; final pass now stops on external `Assets/_Project/Scripts/World/VolcanicUpdraftDirector.cs(1451,58)` missing `VolcanicUpdraftVault.SafeNormalize`.
Rejected Alternatives: Fixing economy/trade from the construction validator domain or reporting a clean build after a new external compile wall appeared.
Scalability potential: N/A.
Hardware Impact: N/A.

Current Blocker: `VolcanicUpdraftDirector.cs` references `VolcanicUpdraftVault.SafeNormalize`, which is not visible to `Hecton8.Core.csproj`. This is outside SHINOBU_67 construction domain. Scoped scans show no `Physics.OverlapBox`/`OverlapBoxNonAlloc` remains in SHINOBU_67 construction files.

Ultra Polish Note: Per user instruction, no `dotnet build` was launched during the final polish passes. Verification was limited to source scans and `git diff --check`.

Problem: Task 08 was previously left as external render-owner work, but the repo already had a construction-owned `HectonBlueprintPreviewBatch` fallback. It was not consuming an unmanaged construction validation lane, used a Burst job without `CompileSynchronously`, and its preview instance used `Pack` instead of an explicit manual layout.
Solution: Added `ConstructionPreviewSignal` as a 96B explicit `ISignal` packet containing AUP center, rotation, scale, module hash, validation flags, result hash, frame, and validity byte. PlayerBuilder emits it every preview draw after SHINOBU_67 validation. `HectonBlueprintPreviewBatch` now consumes `SignalBus<ConstructionPreviewSignal>`, colors green/red from validity, uses explicit 64B `BlueprintPreviewInstance`, and has `[BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]` plus `[NoAlias]`.
Rejected Alternatives: Directly reaching into Agent 09 BRG, instantiating a new preview renderer GameObject from PlayerBuilder, or deleting the fallback visual path without proving a scene-owned replacement exists.
Scalability potential: Low-tier SignalBus lane limit is one preview packet; middle/high/ultra can consume more preview packets without changing the validator DTO. The current fallback is DrawMeshInstanced, not final BRG, but the data seam is now unmanaged and renderer-owned.
Hardware Impact: One signal push and one instanced matrix update for the preview fallback. Static hot-path cost should be below the old collider broadphase cost; measured profiler proof is absent.

Problem: The preview fallback initially still owned private persistent `NativeArray` buffers, violating the H-PHI Vault law even though it was outside the validator kernel.
Solution: Added Vault BufferIDs `ConstructionPreviewWrite`, `ConstructionPreviewBuild`, and `ConstructionPreviewMatrices`. `HectonBlueprintPreviewBatch` now resolves these buffers from `GlobalDataVault`; a later strict pass removed the private `NativeArray` alias fields entirely and kept only `VaultBufferHandle<T>` fields. The only managed mirror left is the cold `Matrix4x4[]` required by `Graphics.DrawMeshInstanced` fallback.
Rejected Alternatives: Continuing to allocate preview native buffers with `Allocator.Persistent`, documenting private alias fields as acceptable, pretending renderer buffers are outside construction responsibility, or replacing the fallback with a new runtime GameObject.
Scalability potential: Low-tier consumes one preview packet and one matrix; higher tiers can scale preview count through Vault capacities without adding new memory owners.
Hardware Impact: Removes one construction preview native-memory owner from scene lifetime; frame math cost unchanged.

Problem: A stricter H-PHI re-scan found that the preview batch still declared private `NativeArray` alias fields and scheduled the matrix job against Vault pointers without locking the backing buffers against compaction. The SIP pressure ratio also used a division shape that was less explicit than the project NaN-guard rule, and the quality curve lacked the mandated `math.step` term.
Solution: Removed `_writeInstances`, `_buildInstances`, and `_matrices` fields from `HectonBlueprintPreviewBatch`. All preview data access now resolves local `NativeArray` views from `VaultBufferHandle<T>` inside the method that needs them. `TryLockPreviewVaultBuffers()` locks `ConstructionPreviewWrite`, `ConstructionPreviewBuild`, and `ConstructionPreviewMatrices` before the scheduled `BuildPreviewMatricesJob`; completion and teardown release those locks. `ResolveProbeBudget()` now uses `math.step * math.smoothstep * math.lerp`, and SIP pressure uses `math.rcp(math.max(projectedSip, 0.0001f))`.
Rejected Alternatives: Treating private aliases as harmless because Vault owns backing memory, blocking the main thread with a mid-frame forced Complete for preview every frame, relying on undocumented DataVault non-compaction, or using a binary low/high quality switch.
Scalability potential: Low tier processes one preview packet/matrix and one SDF probe; middle/high/ultra increase preview packet capacity and SDF evidence through the same Vault lanes and curve without adding memory owners.
Hardware Impact: Removes three private native-array field surfaces and one Vault-compaction race from the construction preview path. Measured profiler proof remains absent; expected frame delta is small, but the ownership and relocation risk are materially reduced on constrained devices.

Problem: Live occupied-cell rejection was still a direct `ConstructionManager.SpawnedModules` list comparison after the no-physics pass. It was zero-allocation and AUP-local, but it did not leave an unmanaged Vault occupancy surface for the validator and did not meet the spirit of Task 02/06 grid hash validation.
Solution: Added `BufferID.ConstructionBuilderOccupancy`, `OccupancyHashTableCapacity=4096`, and frame-stamped fixed hash helpers over `NativeArray<BaseModuleOccupancyDTO>`. `PlayerBuilder.TryFindOccupiedConstructionGridCell` now resolves the Vault table, hydrates module grid cells into it with `TryInsertOccupancyCell`, and rejects the candidate through `TryFindOccupiedCell`. The old direct list compare remains only as a fallback if the Vault is unavailable or the fixed table overflows.
Rejected Alternatives: Private persistent `NativeParallelMultiHashMap` in PlayerBuilder was rejected by the Vault law. Per-frame `NativeParallelMultiHashMap` allocation was rejected by Zero-GC. Reintroducing collider overlap was rejected because it does not solve non-collider mountain/base clipping.
Scalability potential: Low tier uses the same 4096-slot fixed table and one SDF probe; middle/high/ultra can increase table capacity later by one Vault constant without changing builder logic. The grid truth stays deterministic and independent of visual meshes.
Hardware Impact: Removes the normal-path O(moduleCount) comparison after hydration; candidate rejection is fixed-table O(1). Hydration still walks registered modules because no authoritative construction graph export exists in this domain, but it performs no managed allocation or Unity physics query.

Problem: The new `ConstructionBuilderOccupancy` Vault table was correctly off-heap but still hydrated and queried without an explicit Vault lock. If the DataVault defragger relocates that buffer while PlayerBuilder holds the resolved `NativeArray` view, the validator can read/write stale memory.
Solution: `PlayerBuilder.TryFindOccupiedConstructionGridCell` now resolves the table once to ensure allocation exists, locks `BufferID.ConstructionBuilderOccupancy` with `SystemID.Construction`, re-resolves the table under the lock, hydrates module rows, queries `TryFindOccupiedCell`, unlocks, and only then returns the result. The direct list compare remains only if lock/resolve/hydration fails.
Rejected Alternatives: Assuming no compaction during preview drag was rejected because the H-PHI rule explicitly requires lock discipline. Holding a private `NativeArray` alias field was rejected. Force-completing unrelated jobs was not necessary because this path does not schedule a job over the occupancy table.
Scalability potential: Low/middle/high/ultra use the same locked fixed table. High-tier can raise the capacity without changing lock semantics.
Hardware Impact: Adds two Vault calls around occupancy hydration/lookup. The cost is smaller than a native relocation fault; expected runtime impact is below profiler noise compared with removed physics broadphase.

Problem: Task 14 requested `AcousticEchoTap`, but source scan shows multiple incompatible `AcousticEchoTap` structs in AI, Exosuit, Audio Virtualization, and UI namespaces. Importing any one would create sibling-domain coupling or another duplicate contract collision.
Solution: Build commit now emits the canonical global `AcousticPingSignal` with `ChannelMetalStress`, AUP center, radius derived from module extents, intensity, and folded entity/module source hash.
Rejected Alternatives: Adding `Hecton8.Construction.AcousticEchoTap` just to satisfy a name, referencing AI/Exosuit/Audio `AcousticEchoTap`, or relying only on local `AudioClip buildSound`.
Scalability potential: Weak devices get one compact metal-stress acoustic packet; higher tiers can route it through richer audio/occlusion without construction knowing the audio implementation.
Hardware Impact: One typed signal enqueue on commit only; zero build-drag frame cost.

Problem: Task 15 needed a flora exclusion broadcast. Existing `BaseModuleNavModifier` already performs terrain-hole/artificial-structure registration on module enable, but there was no typed construction signal for flora owners to consume.
Solution: Added 80B explicit `FloraExclusionSignal` carrying AUP center, AABB extents, module hash, source entity low bits, frame, and operation. PlayerBuilder emits it on successful build commit after inventory transaction success. Existing `BaseModuleNavModifier` remains the immediate bridge for active module vegetation exclusion.
Rejected Alternatives: Direct mutation of flora managers from PlayerBuilder, fake unmanaged signal with no AABB data, or replacing the existing vegetation bridge in this pass.
Scalability potential: Low/mid/high/ultra all receive one stable AABB packet; future flora BRG/GPU culling owners can consume it without changing construction commit code.
Hardware Impact: One typed signal enqueue on commit only; no per-frame drag cost.

Problem: New signal DTOs must be part of the layout audit or the self-audit would under-report SHINOBU_67's actual binary surface.
Solution: Extended `ModularBaseConstructionValidator.ValidateStructLayout()` to assert `ConstructionPreviewSignal=96B` and `FloraExclusionSignal=80B`.
Rejected Alternatives: Treating signal DTOs as documentation-only or leaving layout verification to Unity import.
Scalability potential: Same explicit packets are valid from low-tier fallback preview to future ultra BRG rendering.
Hardware Impact: Layout checks run only when the validator writes telemetry/fault guards; no steady-state cost.

---

# SHINOBU_67 Rationale - Addressables Heap Sanitizer

Date: 2026-05-18
Status: ACTIVE - ADDRESSABLES PASS

## Session Boundary

Problem: `CURRENT_BATCH.md` contains two `SHINOBU_67` blocks. The first is modular-base validation; the user request names Addressables sanitizer and memory control during chunk loading.
Solution: Extracted the second block, `<AGENT_PROMPT id="SHINOBU_67" role="ADDRESSABLES_HEAP_SANITIZER">`, by CLI and treated it as active while preserving old logs as historical contamination.
Rejected Alternatives: Continuing the older construction pass would ignore the current user directive. Deleting prior logs would violate evidence preservation.
Scalability potential: Low/Middle/High/Ultra differ by TTL duration, VRAM panic response, and cache retention aggressiveness while keeping one deterministic handle manager.
Hardware Impact: The intended gain is fewer duplicate Addressables handles, fewer bundle unload/reload churn cycles, and fewer GC/release spikes on i3/MX350 and Steam Deck-class storage. Measured proof is pending.

## Pre-Code Decision Journal

Problem: The repository already has `AssetLifecycleGovernor` registered in `GlobalRegistry.AssetLifecycle`. Creating a new unmanaged-looking `AssetHandleManager` beside it would split release authority and could double-release handles.
Solution: Extend `AssetLifecycleGovernor` as the central AssetHandleManager surface. Add native ref mirrors and fixed handle slots underneath the existing registry, then route first-party chunk/item loads through it.
Rejected Alternatives: New singleton, direct static manager, or a second scene component with its own release queue. All create ownership ambiguity.
Scalability potential: Existing lifecycle owner can already see VRAM monitor, player distance, dispatcher, loading-screen context, and hard-reaper windows; adding TTL policy there lets cache behavior breathe with hardware.
Hardware Impact: Avoids duplicate manager dispatch per asset operation; expected CPU saving is microseconds per request and fewer release spikes, pending profiler.

Problem: Unity `AsyncOperationHandle` is a managed engine struct and cannot live purely in `NativeHashMap`.
Solution: Store authoritative hot ref state in `NativeHashMap<uint,int>` / fixed `NativeArray<AssetTrackerDTO>` and keep a parallel preallocated `AsyncOperationHandle[]` pool as the Unity bridge. `AssetTrackerDTO.HandlePointer` stores a stable slot id, not a GC object.
Rejected Alternatives: `Dictionary<string, AsyncOperationHandle>`, raw GCHandle pointers, or pretending Addressables handles can be passed into Burst jobs.
Scalability potential: Native ref/TTL mirrors support cheap audits on weak devices and richer editor diagnostics on high-end machines.
Hardware Impact: Ref mutation stays integer-only; handle pool avoids repeated managed collection churn from duplicate loads.

Problem: Immediate `Addressables.Release()` on chunk boundary causes bundle unload/reload churn and visible stalls when the player oscillates around streaming borders.
Solution: Ref-count reaches zero arms a TTL. A Burst TTL job marks assets releasable, and actual `Addressables.Release()` is drained only during blind frames or VRAM panic.
Rejected Alternatives: immediate release on every unload, `Resources.UnloadUnusedAssets()`, or unlimited caching with no pressure override.
Scalability potential: Weak I/O uses shorter but still nonzero TTL to reduce churn; large-memory machines retain visited assets longer and trade RAM for seamless return traversal.
Hardware Impact: Expected reduction is one Addressables release/reload pair per oscillating chunk crossing; exact microseconds pending Unity profiler.

## Implementation Decisions

Problem: Mandatory tracking demanded a `NativeHashMap<uint, ulong>` handle pointer/id, while Unity `AsyncOperationHandle` cannot legally live inside unmanaged Burst memory.
Solution: Added `_nativeHandlePointers` as `AssetHash -> slot+1` and kept the actual `AsyncOperationHandle` in a fixed managed pool indexed by that slot. `AssetTrackerDTO.HandlePointer` mirrors the same slot id for editor/telemetry.
Rejected Alternatives: `Dictionary<string, AsyncOperationHandle>`, `GCHandle` pointers, or storing Unity handles directly in native containers.
Scalability potential: Low/Middle/High/Ultra all use the same fixed slot table; higher tiers increase retention through TTL, not allocation strategy.
Hardware Impact: Cache hit lookup stays integer-keyed and avoids a second Addressables load/handle allocation. Exact microseconds are PENDING PROFILER.

Problem: Chunk streaming owned direct `Addressables.LoadAssetAsync<GameObject>` and immediate release paths, producing duplicate handles when a chunk was discarded and immediately requested again.
Solution: `WorldChunkResidencyManager` now acquires chunk prefab handles through `AssetLifecycleGovernor.TryAcquireAddressableGameObject()` and releases by stable asset hash through `ReleaseAddressableAsset()`. Successful loads call `MarkAddressableLoaded()` with chunk size/AUP metadata for distance reaper context.
Rejected Alternatives: local handle dictionaries in the chunk manager, blocking promotion until disk is fast, or adding a new AssetHandleManager singleton beside `GlobalRegistry.AssetLifecycle`.
Scalability potential: Weak storage keeps recently discarded chunks for a short hold-delay; high/ultra machines can hold visited chunks up to 300s+shared-bundle multiplier.
Hardware Impact: Expected saved cost is one duplicate handle and possible bundle reload per boundary oscillation; exact frame data not yet measured.

Problem: Item/world prefab Addressables used `AssetReferenceGameObject.LoadAssetAsync()` and released the handle locally, bypassing the central sanitizer.
Solution: `ItemCatalog` world prefab prewarm/dispatch now acquires through the governor and releases by `DispatchAssetKey`. Local direct release remains only as a fallback when no governor is registered.
Rejected Alternatives: deleting the existing item catalog runtime ledger or routing through a string-keyed map.
Scalability potential: The same TTL/cache policy now covers interactable world prefabs near chunks without forcing item systems to know release timing.
Hardware Impact: Reduces duplicate prefab handles during repeated prewarm/release loops; exact savings PENDING PROFILER.

Problem: Blind-frame release needed a deterministic visual mask without depending on unavailable Agent 76 APIs.
Solution: Reused existing hard-reaper scanner interference/static glitch window, added `SetHeapSanitizerMockBlindFrame()`, allowed zero-delta dispatcher blind frames, and allowed VRAM panic override. Normal `Addressables.Release()` is re-queued when no blind frame exists.
Rejected Alternatives: `Resources.UnloadUnusedAssets()`, release-on-zero immediately, or assuming an origin-shift interface not exposed in the current codebase.
Scalability potential: Low devices release less often and only when visually masked unless VRAM panic; high/ultra devices retain more and release in planned sweeps.
Hardware Impact: Moves Addressables release work out of visible frames; exact spike reduction is PENDING UNITY PROFILER.

Problem: Bundle fragmentation causes the whole bundle to churn when several small assets share a prefix.
Solution: Compute a cold path bundle-prefix hash from address/GUID, maintain `_bundlePrefixRefCounts`, and inflate TTL for shared bundles.
Rejected Alternatives: forcing `CleanBundleCache()` every unload or ignoring bundle residency economics.
Scalability potential: Low retains shared bundles just long enough to avoid MicroSD thrash; high/ultra extend the retention window and spend RAM for smoother traversal.
Hardware Impact: One prefix hash on load; expected I/O win is fewer unload/reload cycles for shared bundles.

Problem: Human operators need leak visibility before profiler capture.
Solution: Added `HeapSanitizerTunerWindow` with Base TTL, VRAM Panic Threshold, CSV override load, active tracker list, and red `LEAK SUSPECT` warning when refcount exceeds 50. Added DataVault mirror buffers `AddressableHeapCacheProfiles` and `AddressableHeapTelemetry` so cache profiles and heap samples have a vault-backed human-control surface.
Rejected Alternatives: console-only warnings or inspector-only serialized fields.
Scalability potential: Same controls can tune toaster, middle, high, and ultra cache behavior without recompiling.
Hardware Impact: Editor-only.

## Verification Notes

Problem: Full compile verification was initially blocked by the project rule forbidding `dotnet` when CPU is under work.
Solution: Checked for running `dotnet/csc/VBCSCompiler` and sampled CPU. First samples were 81.9% then 71.3%, so build was deferred. Later CPU was 31.4% with no compiler processes, so `dotnet build Hecton8.Core.csproj --no-restore -m:1 -nr:false /p:UseSharedCompilation=false /v:minimal` was launched and succeeded.
Rejected Alternatives: violating the CPU guard to produce a fake compile report.
Scalability potential: N/A.
Hardware Impact: N/A.

Current Verification State: Static scans show chunk and item-world-prefab loads route through `AssetLifecycleGovernor`. Direct first-party Addressables releases remain in bootstrap dependency download handles, content-authority fixed ledgers, central governor, and fallback paths when the governor is unavailable. `git diff --check` reports only LF/CRLF warnings on touched files. Core build succeeded with 8 existing CS0649 warnings in `GlobalPhysicsStateManager.PhysicsDistanceCullingJob`, 0 errors.

## Ultra Polish Decisions - 2026-05-19

Problem: The previous Addressables pass satisfied the literal `NativeHashMap<uint, ulong>` prompt but violated the stronger H-PHI/Data Vault law. `AssetLifecycleGovernor` privately allocated `_nativeRefCounts`, `_nativeHandleSlots`, `_nativeHandlePointers`, `_bundlePrefixRefCounts`, and multiple persistent `NativeArray` fields. That local ownership can fragment native/managed-adjacent memory during scene lifetime and creates a second allocation authority beside `GlobalDataVault`.
Solution: Removed the private `NativeHashMap` containers and private native allocations. Added Vault buffers `AddressableHeapTrackers`, `AddressableHeapTimeToLive`, `AddressableHeapTrackerFlags`, and `AddressableHeapHandleMap`. The handle map is now a fixed, Vault-backed, open-addressed 64B `AssetHandleMapEntryDTO` table storing `AssetHash`, slot, refcount, bundle prefix, and `ulong HandlePointer` identifier. `AssetLifecycleGovernor` holds `NativeArray` aliases only; it never disposes Vault storage.
Rejected Alternatives: Keeping private `NativeHashMap` because the first XML requested it was rejected after the ultra mandate elevated Vault sovereignty. Storing `AsyncOperationHandle` in unmanaged memory was rejected because Unity handles are not blittable. Allocating a second manager was rejected because `GlobalRegistry.AssetLifecycle` is already the asset lifecycle authority.
Scalability potential: Low/Middle/High/Ultra use the same fixed integer table. Low keeps small TTL windows and fast release under VRAM panic; High/Ultra expand TTL retention through `GlobalQualityWeight` without changing allocation shape. Shared bundles still inflate TTL through table scans, not an extra map.
Hardware Impact: Removes five private persistent native owners from `AssetLifecycleGovernor`; expected gain is reduced native heap fragmentation pressure on i3/MX350 and fewer allocation ledger surfaces. Per acquire/release remains integer slot lookup plus one `Interlocked` refcount. Measured profiler proof remains absent.

Problem: The prior Burst jobs used bare `[BurstCompile]` and no `[NoAlias]`, weakening SIMD/vectorization and audit compliance.
Solution: Updated `AssetTtlEvaluationJob` and `MockChunkLoadSpamJob` to `[BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]` and marked NativeArray/pointer fields `[NoAlias]`.
Rejected Alternatives: Relying on Burst defaults or leaving mock/test jobs outside the directive was rejected; the prompt explicitly demands exact flags for every job written by this domain.
Scalability potential: Low-tier TTL evaluation remains O(capacity) at slow cadence; high-tier can expand cache retention without extra GC or virtual dispatch.
Hardware Impact: SIMD proof still requires Burst Inspector, but source now exposes non-aliasing constraints for AVX2/NEON codegen.

Problem: SlowTick helper methods read `GlobalRegistry.VRAMPressure` and `GlobalRegistry.Dispatcher` directly, violating the hot-path service-cache rule in spirit.
Solution: Cached `VRAMPressureMonitor` and `SystemDispatcher` during registration and changed blind-frame/VRAM checks to use cached fields. `GlobalRegistry.DataVault` remains a cold resolve path only when `_dataVault` is not established.
Rejected Alternatives: Polling registry convenience properties every slow tick was rejected. Inventing a new signal lane for two local query dependencies was not necessary for this bounded polish pass.
Scalability potential: Low/Middle/High/Ultra keep the same release gates; the cached VRAM provider controls panic release consistently.
Hardware Impact: Tiny CPU change; the relevant win is architectural isolation, not measurable microseconds.

Problem: The open-address map introduced a new primary DTO requiring ARM64/false-sharing audit.
Solution: `AssetHandleMapEntryDTO` is explicit 64B. Offsets: `HandlePointer` 0..7, `AssetHash` 8..11, `BundlePrefixHash` 12..15, `Slot` 16..19, `RefCount` 20..23, `Flags` 24..27, `Generation` 28..31, `_pad0` 32..39, `_pad1` 40..47, `_pad2` 48..55, `_pad3` 56..63. Total is one 64B cache line.
Rejected Alternatives: Sequential layout and `Pack=1` were rejected. A compact 24B map record was rejected because parallel mutation could false-share adjacent entries if Agent 35 later writes refcounts from job lanes.
Scalability potential: Weak devices get cache-line isolation for atomic/ref lanes; top-tier hardware can tolerate larger map capacity without changing code.
Hardware Impact: 64B table entries spend memory to prevent false-sharing hazards. At 8192 tracked handles and 2x map capacity, table memory is about 1MB, acceptable against the project memory ceiling.

Verification: Static scan found no `NativeHashMap`, no private `new NativeArray`, no Sentinel registration/unregistration, and no old `_native*` fields in `AssetLifecycleGovernor`. CPU guard was 16.7% and no `dotnet/csc/VBCSCompiler` process was active. `dotnet build Hecton8.Core.csproj --no-restore -m:1 -nr:false /p:UseSharedCompilation=false /v:minimal` passed with 8 pre-existing CS0649 warnings in `GlobalPhysicsStateManager.PhysicsDistanceCullingJob`, 0 errors.

Problem: A stricter re-scan found two remaining Addressables defects. First, `AssetLifecycleGovernor` still declared private persistent `NativeArray<>` fields as Vault aliases, which violates the literal Vault law even if allocation ownership belongs to `GlobalDataVault`. Second, `AssetTtlEvaluationJob` still used `job.Run()`, preventing dependency visibility and making the Burst kernel look synchronous to the slow-tick lane.
Solution: Removed the six private `NativeArray<>` fields and replaced all tracker/TTL/flags/map/profile/telemetry access with local Vault views resolved from `VaultBufferHandle<T>` inside each method. Converted TTL evaluation to a scheduled `JobHandle` launched after release draining, guarded by Vault buffer locks on `AddressableHeapTrackers`, `AddressableHeapTimeToLive`, and `AddressableHeapTrackerFlags`. The next slow tick consumes completed results without blocking; any runtime mutation of the same lanes first completes the exact scheduled dependency as a write fence and records pending TTL results for the next drain.
Rejected Alternatives: Keeping alias fields and documenting them as Vault-owned was rejected because the mandate forbids private `NativeArray` fields, not only private allocations. Keeping `job.Run()` was rejected because it hides dependency graph semantics. Scheduling the job before `DrainPendingReleaseQueue()` was rejected because release flow can clear tracker slots and race the job.
Scalability potential: Low-tier still runs short TTL and fast VRAM panic. Middle/high/ultra keep longer retention through `GlobalQualityWeight`. The TTL job remains one fixed Vault-lane pass; larger hardware scales by retention policy, not extra allocation surfaces.
Hardware Impact: Removes persistent `NativeArray` fields from the governor object and exposes Vault locks to the defragger while the scheduled Burst job owns pointers. Expected frame delta is small; the value is lower fragmentation risk and fewer hidden ownership surfaces on i3/MX350. Build proof is pending because CPU guard measured 100%, and a later guard saw active `dotnet`/`csc` compiler processes.

Verification: Static scan over `AssetLifecycleGovernor.cs`, `AssetRecord.cs`, and `HeapSanitizerTunerWindow.cs` found no `private NativeArray`, no `NativeHashMap`, no `new NativeArray`, no `job.Run()`, no LINQ, no `foreach`, no `string.Format`, no `Resources.UnloadUnusedAssets`, and no `UnityEngine.Random`. `git diff --check` reports only LF/CRLF warnings in touched files. `dotnet build` was not launched because the CPU guard exceeded 50% and another compiler session appeared.

Problem: The alias-purge pass still had one concurrency defect. `CompleteTtlEvaluationForMutation()` could block a normal Addressables acquire/release/pin/clear call if the scheduled TTL job had not finished. That violates the job-dependency rule even though it protected the Vault lanes from data races.
Solution: Replaced the blocking mutation fence with `TryPrepareTrackerMutation()`. It calls `Complete()` only after `JobHandle.IsCompleted` is true. If the TTL job is still running, cache-hit acquire updates the managed `AssetRecord` and returns the existing `AsyncOperationHandle` without touching native lanes; release decrements managed refcount and marks `_nativeRefSyncRequired`; clear/pin fail fast and leave pending release state queued. When the TTL job completes, `SyncNativeRefCountsFromRegistry()` reconciles `AssetTrackerDTO.ReferenceCount`, handle-map refcounts, TTL seconds, and pending/releasable flags from the managed registry before release draining.
Rejected Alternatives: Blocking `Complete()` on every mutation was rejected. Writing native tracker lanes while the TTL job owns the same pointers was rejected as a race. Creating duplicate cold-miss Addressables handles during a pending TTL job was rejected because it violates the sanitizer contract; cold misses wait/retry instead of becoming untracked handles.
Scalability potential: Low-tier keeps short TTL and will retry cold misses rather than block on a job. High/ultra keep long TTL and should hit the managed cache path more often, so they avoid both disk churn and blocking fences.
Hardware Impact: Removes a possible main-thread wait from acquire/release. The fallback cache-hit path is managed-registry-only plus a later native sync pass; expected cost is a few scalar writes now and one fixed tracker pass later. Build proof is still pending because CPU samples measured 100%, 100%, 98.1%, and 84.8% with active `dotnet`/`csc`.

Verification: Static scan found no `private NativeArray`, `NativeHashMap`, `new NativeArray`, `job.Run()`, LINQ, `foreach`, `string.Format`, `Resources.UnloadUnusedAssets`, `UnityEngine.Random`, or queue `Contains()` in the sanitizer files. `Complete()` appears only in teardown or after an explicit `IsCompleted` guard.

Problem: The sanitizer still had a managed-heap expansion trap. `_registry` and `_pendingRelease` were cold-allocated at 512 entries while `maxTrackedAddressableHandles` can be configured up to 8192. A large chunk traversal could therefore force `Dictionary<uint, AssetRecord>` rehashing or `Queue<uint>` backing-array growth during gameplay, exactly the managed heap fragmentation path this agent is supposed to eliminate.
Solution: Added `MaxTrackedAddressableCapacity = 8192` and `MaxAddressableHandleMapCapacity = 16384`. Pre-sized `_registry` and `_pendingRelease` to `MaxTrackedAddressableCapacity`, clamped handle/map capacity through named constants, and forced `maxRegistryCapacity >= maxTrackedAddressableHandles` in `Awake()` and `EnsureNativeHandleStorage()`.
Rejected Alternatives: Leaving the 512-entry containers and relying on average-case content was rejected because streaming boundaries are adversarial. Replacing the managed registry with a pure native handle table was rejected for this pass because Unity `AsyncOperationHandle` is not blittable and the existing managed `AssetRecord` carries owner/retry/blind-frame metadata outside Burst.
Scalability potential: Low-tier devices can run the default 1024 slot table without a managed resize; middle/high/ultra can raise the sanitizer to 8192 tracked handles and still avoid runtime growth. The native map remains a fixed 2x open-address Vault lane, so cache-hit cost remains bounded.
Hardware Impact: Prevents managed array/dictionary growth and rehash spikes during heavy chunk streaming. Worst-case cold managed bridge memory rises deliberately at boot; in exchange, runtime capacity churn is removed inside the sanitizer ceiling. Build proof is deferred after this patch because CPU guard sampled 100%, 100%, 100%, 100% with no compiler process.

Problem: `ResolveAdaptiveTtlSeconds` used a smooth polynomial over raw `GlobalQualityWeight`, so a weak-device weight of 0.1 still produced a TTL above the prompt's explicit 10 second floor. That is mathematically softer than the required low-tier collapse and also missed the ultra mandate's explicit `math.step` term.
Solution: Changed the curve to `math.step(0.3f, q) * smoothPolynomial(saturate((q - 0.3) / 0.7))`, then kept `math.lerp(10s, highTtl, curve)`. Below 0.3 the cache TTL is exactly 10 seconds; from 0.3 to 1.0 it scales continuously to the high-end 300 second cache window.
Rejected Alternatives: A binary `if (quality < 0.3f)` branch was rejected because the scalability pillar forbids dichotomy. Keeping the old curve was rejected because it quietly retained too much memory on weak I/O hardware.
Scalability potential: Low tier gets strict minimum TTL; middle tier ramps without a visual/cache pop; high/ultra can retain visited chunks for long traversal returns and spend RAM to buy seamless streaming.
Hardware Impact: Weak devices release unused assets sooner after the blind-frame gate instead of carrying an accidental 18s+ hold at quality 0.1. High hardware behavior remains capped by `baseAddressableTtlSeconds` and profile overrides.

Problem: The pending-release queue obeyed Blind Frames, but two cold eviction paths still bypassed it. `EvictLowestPriorityUnusedAssets()` and `ReleaseDistantChunkAddressables()` called `ExecuteReleaseFlow()` directly, so soft pressure or the normal distant-chunk sweep could invoke `Addressables.Release()` on a visible frame even when no VRAM panic existed.
Solution: Added `TryExecuteOrDeferBlindFrameRelease()`. It reuses `IsAddressableReleaseBlockedByBlindFrame()` for all priority and distant evictions. If the frame is not blind and VRAM is below panic threshold, it sets `PendingRelease`, enqueues the asset, and marks `_nativeRefSyncRequired` so the Vault tracker refcount catches up after the TTL job fence. If the hard reaper, mock fade, zero-delta dispatcher, or VRAM panic is active, it writes the zero-ref record and executes the existing release flow.
Rejected Alternatives: Leaving distant eviction as a direct release was rejected because it violated the user's primary requirement. Duplicating blind-frame checks in the chunk manager was rejected because the governor is the release authority. Blocking until the next blind frame was rejected because it would stall the main thread.
Scalability potential: Low devices queue distant releases until a visual mask unless memory pressure crosses the panic threshold. Middle/high/ultra devices keep the same rule but retain more assets through the quality-weighted TTL, so the queued release path is less frequently exercised.
Hardware Impact: Prevents visible-frame Addressables release spikes from priority and distance sweeps. Expected win is stall relocation, not raw arithmetic savings; measured profiler proof remains pending.

Verification: Static scans after the blind-gate patch found no forbidden sanitizer hot-path patterns. `SelfAudit_SHINOBU_67_Addressables.xml` parses as XML. `git diff --check` reports only LF/CRLF warnings. Build was not launched because the latest CPU guard sampled 99.3%, 98.4%, 84.3%, and 99.4% with no active dotnet/csc/VBCSCompiler process.

Problem: The ultra mandate allows heavy unloads during Blind Frames or Origin Shifts, but the implementation only recognized hard-reaper/mock/zero-delta blind frames plus VRAM panic. `HectonFloatingOrigin` already called `ForceDrainPendingReleaseQueue()` after a shift; without an explicit sanitizer blind window that call could simply requeue work if the dispatcher delta was nonzero.
Solution: Added `SetHeapSanitizerBlindFrameWindow()` as the non-mock public gate in `AssetLifecycleGovernor`. `SetHeapSanitizerMockBlindFrame()` now delegates to it. Wrapped the existing post-shift drain in `HectonFloatingOrigin.RunPostShiftUnusedAssetUnloadGuardAsync()` with `SetHeapSanitizerBlindFrameWindow(true, 0f)` and a `finally` reset to false.
Rejected Alternatives: Treating every `ForceDrainPendingReleaseQueue()` caller as blind was rejected because soft-pressure callers must still defer. Adding origin-shift knowledge inside the governor was rejected because the origin-shift system already owns the shift moment. Leaving the post-shift call as a no-op under nonzero delta was rejected because it would violate the explicit Origin Shift unload allowance.
Scalability potential: Low devices can clear queued zero-ref handles during the already-masked origin shift instead of carrying them into visible traversal. Middle/high/ultra retain more by TTL policy, but the declared shift window gives every tier one deterministic cleanup point.
Hardware Impact: Adds two scalar state writes around an existing drain call. Expected win is fewer delayed releases after rebasing and no visible-frame release stall after the shift.

Verification: XML self-audit parses after the origin-shift patch. Static sanitizer scan remains clean for forbidden patterns. `git diff --check` reports only LF/CRLF warnings. Build was not launched because the latest CPU guard sampled 35.4%, 30.9%, 64.2%, and 100% with no active dotnet/csc/VBCSCompiler process.

Problem: The non-reload lifecycle still had a real leak window. `OnDisable()` removed the governor from tick/service registries, but it did not release the fixed `AsyncOperationHandle[]` bridge, clear managed residency records, or zero the Vault tracker/map lanes. If a persistent bootstrap service was disabled, duplicated, or torn down with domain reload disabled, the sanitizer could be invisible to `GlobalRegistry` while still advertising active handles and holding Unity Addressables references until `OnDestroy()`.
Solution: Added `ResetAddressableHeapRuntimeState()`. `OnDisable()` and `OnDestroy()` now share an idempotent reset path: unregister ingress first, release hard-reaper async callbacks, clear blind-window state, clear managed registry/queues/scratch lists, complete the TTL job for teardown, release every valid fixed-pool Addressables handle exactly once, clear `AddressableHeapTrackers`, `AddressableHeapTimeToLive`, `AddressableHeapTrackerFlags`, and `AddressableHeapHandleMap`, then drop Vault handles. The teardown clear preserves `AddressableHeapTelemetry` so the last 300 samples remain available for forensic dumps; cold boot still clears telemetry and cache-profile lanes before mock profile hydration. `OnEnable()` now rehydrates Vault storage and fallback assets before registration so a deliberate re-enable starts from a clean table.
Rejected Alternatives: Relying on `OnDestroy()` was rejected by the non-reload reset mandate. Clearing Vault lanes without releasing the managed handle bridge was rejected because it would hide live Unity handles from the editor facade. Keeping stale Vault lanes for the next instance to clear was rejected because the editor leak window and service-disabled leak window remain observable. Routing teardown through the normal Blind Frame queue was rejected because disabled service state must fail closed and release ownership deterministically.
Scalability potential: Low devices do not carry dead handles through scene/service disable and therefore avoid extra MicroSD churn on the next boot. Middle/high/ultra can still retain warmed assets during normal gameplay through TTL policy, but lifecycle reset is deterministic across all tiers.
Hardware Impact: Prevents stale live handle rows and fixed-pool Addressables references from surviving non-reload disable paths. Runtime hot-path cost is 0 us; reset cost is teardown-only O(handleCapacity + mapCapacity) fixed loops, paid when the service is disabled/destroyed.

Verification: XML self-audit parses after the reset patch. Static sanitizer scan found no forbidden hot-path patterns (`private NativeArray`, `NativeHashMap`, `new NativeArray`, `job.Run`, LINQ, `foreach`, `string.Format`, `Resources.UnloadUnusedAssets`, `UnityEngine.Random`, queue `Contains`). `git diff --check` reports only LF/CRLF warnings. Build was not launched because CPU guard sampled 15.5%, 27.6%, 7.0%, and 63.6% with no compiler process; the last sample violates the >50% rule.

Problem: The first reset pass cleared ownership but left runtime counters stale. A deliberate disable/re-enable would start with old `_frameSequence`, hard-reaper anchor/timers, cache hit/miss totals, forced VRAM release count, leak suspect hash, and Addressables dependency group counters. That is not a memory leak, but it corrupts heap telemetry and can trigger a hard reaper from an old travel anchor.
Solution: Extended `ResetAddressableHeapRuntimeState()` to reset frame cadence, cold tick warning/release times, hard-reaper anchor and next interval, resident byte count, release/cache/VRAM counters, pending TTL/leak hashes, deferred mutation count, and Addressables dependency group stats. The telemetry ring itself is still preserved because it is forensic history, not live control state.
Rejected Alternatives: Clearing the telemetry ring on disable was rejected because it would erase the last 300 heap samples before QA can inspect them. Leaving counters as historical totals was rejected because the editor facade and cold tick logic treat them as live runtime state.
Scalability potential: All tiers re-enter with clean live counters while keeping forensic bytes. Weak devices avoid an immediate stale hard-reaper sweep after service re-enable; high/ultra retain normal TTL behavior after fresh runtime registration.
Hardware Impact: 0 us hot path. Reset-only scalar writes; prevents stale telemetry-driven false positives and stale hard-reaper work.

Verification: XML self-audit parses after the counter reset patch. Static sanitizer scan remains clean for forbidden patterns. `git diff --check` reports only LF/CRLF warnings. Build was not launched because CPU guard sampled 56.2%, 16.1%, 44.5%, and 89.2% with no compiler process; two samples violate the >50% rule.

Problem: The distant chunk release path still violated reference counting. `ReleaseDistantChunkAddressables()` selected chunk records by distance and then set `record.RefCount = 0` before release. A far-away chunk with a valid active owner could therefore lose its `AsyncOperationHandle` even though the ownership count had not reached zero. That is not a performance optimization; it is a lifetime corruption vector.
Solution: Changed the distant release selector to require `record.RefCount == 0` and `!record.PendingRelease` before enqueueing a candidate, then rechecked the same conditions immediately before calling the Blind Frame release gate. Distance can now accelerate only an already-unused chunk handle; it cannot override active reference ownership.
Rejected Alternatives: Leaving distance as a stronger authority than refcount was rejected because it contradicts the sanitizer's primary contract. Forcing a release and hoping the chunk manager reacquires later was rejected because it creates duplicate handles and visible-frame faults. Removing the distant release pass entirely was rejected for this bounded pass because zero-ref far chunks still need a deterministic cleanup path.
Scalability potential: Low devices still get aggressive cleanup of unused far chunks when a Blind Frame or VRAM panic permits it. Middle/high/ultra retain active handles correctly while TTL and bundle-prefix inflation decide cache lifetime.
Hardware Impact: Hot arithmetic change is one integer compare and one pending flag compare per distant candidate. The important saving is avoiding accidental active-handle release, duplicate reload, and follow-on managed heap churn on weak devices.

Verification: Static sanitizer scan found no forbidden patterns after the patch. XML self-audit parses. `git diff --check` reports only LF/CRLF warnings. Build remains deferred because CPU guard sampled 63.2%, 39.3%, 79.6%, and 15.2% with no compiler process; two samples violate the >50% rule.

Problem: Refcount was fixed, but the sanitizer still carried spatial state. `AssetRecord` stored `AbsoluteUniversePosition` plus a runtime `Vector3`, addressable acquire/mark methods accepted chunk coordinates, and the hard-reaper used AUP travel distance as a release trigger. That drags world-streaming spatial authority into a memory lifetime manager and weakens Task 13.
Solution: Removed the spatial fields from `AssetRecord`, removed coordinate parameters from governor addressable acquire/mark paths, removed the hard-reaper AUP travel trigger, and left the reaper cadence to time plus explicit pressure/owner calls. Chunk position and AUP distance remain in `WorldChunkResidencyManager` and `ItemCatalog`, which own spatial residency; the sanitizer owns keys, refcounts, TTL, flags, handles, telemetry, and Blind Frame release gates.
Rejected Alternatives: Keeping AUP in a managed tracking struct was rejected because it makes memory cleanup reason about world position. Converting the position to another local scalar was rejected because the sanitizer should not need spatial evidence after owner release/refcount zero. Removing ItemCatalog's own world-prefab distance eviction was rejected because that spatial owner already tracks AUP access outside the sanitizer.
Scalability potential: Low devices still get short TTL and VRAM panic cleanup. High/ultra retain cached handles through TTL and bundle-profile policy. Spatial Math LOD stays in streaming owners instead of polluting handle lifetime records.
Hardware Impact: Removes two spatial fields from every managed `AssetRecord` and removes AUP conversion on Addressables acquire/loaded paths. The hard-reaper no longer polls player AUP each cold tick, saving small scalar work and removing a precision-risk dependency.

Verification: Static sanitizer scans now find no `HasAbsoluteUniversePosition`, `AbsoluteUniverseAup`, `AbsoluteUniversePosition`, `Vector3 absoluteUniversePosition`, `HardReaperTravel*`, or `using Hecton8.World` in the sanitizer files. Forbidden hot-path scan remains clean. XML self-audit still parses. Build remains deferred because CPU guard sampled 81.0%, 74.2%, 52.9%, and 91.2% with no compiler process.

Problem: A neighboring content-authority cleanup path still used AUP-shift stress as a reason to drain the governor, but it did not explicitly declare a sanitizer Blind Frame. `ContentRuntimeServices.TickAupShiftCleanup()` could therefore call `ForceDrainPendingReleaseQueue()` and have the governor immediately requeue the handles when dispatcher delta was not zero.
Solution: Wrapped that existing governor drain/priority-eviction call in `SetHeapSanitizerBlindFrameWindow(true, 0f)` with a `finally` reset. This does not move content bundle ownership into the sanitizer; it only marks the already-detected AUP shift as a valid blind release mask for the governor.
Rejected Alternatives: Treating every `ForceDrainPendingReleaseQueue()` call as blind was rejected because VRAM/content-pressure cleanup can happen in visible frames. Removing the content cleanup call was rejected because it already exists as a low-frequency stress path. Releasing content-owned fixed ledgers through the sanitizer was rejected because `ContentRuntimeServices` owns VFX/bundle residency separately.
Scalability potential: Low devices can clear queued zero-ref handles during an AUP-shift stress mask instead of carrying them into visible traversal. Middle/high/ultra keep the same retention policy, but the blind window lets the governor use an already-hidden cleanup opportunity.
Hardware Impact: 0 us hot path beyond two scalar writes around an existing cleanup call. Expected benefit is fewer delayed Addressables releases after origin/AUP-shift stress, with no new managed allocation.

Verification: Static re-scan after the content blind-window patch found no sanitizer AUP/state regressions and no forbidden hot-path sanitizer patterns. XML self-audit parses. Build remains CPU-gated.

Problem: The content VRAM hard-ceiling path also invoked governor drain/eviction, but that state is not a visual Blind Frame. Wrapping it in the blind-window API would hide a semantic lie: visible-frame release is only legal for true Blind Frames or explicit VRAM panic.
Solution: Added `SetHeapSanitizerVramPanicWindow()` to `AssetLifecycleGovernor` and routed `IsAddressableReleaseBlockedByBlindFrame()`, TTL panic evaluation, and TTL scheduling through `IsVramPanicReleaseFrame()`. `ContentRuntimeServices.TickVramIntercept()` now opens a try/finally VRAM panic window only around the existing governor cleanup. `VRAMPressureMonitor.VramPressureFactor` remains the normal Agent 45 path; the explicit window is for content hard-ceiling emergency pressure.
Rejected Alternatives: Using `SetHeapSanitizerBlindFrameWindow()` for VRAM pressure was rejected because it weakens the blind-frame invariant. Leaving the content hard-ceiling call as a normal drain was rejected because the governor might requeue and fail to clear memory under actual hard ceiling pressure. Calling the hard reaper every content tick was rejected because it would spam visual static and bundle-cache cleanup under sustained pressure.
Scalability potential: Low devices get a deterministic emergency release escape when content predicts VRAM ceiling breach. Middle/high/ultra still retain longer TTL caches until either the real VRAMPressureMonitor or explicit hard-ceiling path declares panic.
Hardware Impact: Adds two scalar panic-window writes around an existing content cleanup call and one extra boolean check inside the sanitizer release predicate. It prevents false visible-frame release in normal frames while allowing real hard-ceiling relief.

Verification: Static re-scan after the VRAM panic-window patch found no sanitizer AUP/state regressions and no forbidden hot-path sanitizer patterns. The explicit governor cleanup call scan now shows AUP-shift cleanup wrapped in `SetHeapSanitizerBlindFrameWindow` and hard-ceiling cleanup wrapped in `SetHeapSanitizerVramPanicWindow`. XML self-audit parses. `git diff --check` reports only LF/CRLF warnings. Build remains deferred because the latest CPU guard sampled 26.9%, 73.4%, 100%, and 79.8% with no compiler process; three samples violate the >50% rule.

Problem: `SetHeapSanitizerBlindFrameWindow()` and `SetHeapSanitizerMockBlindFrame()` shared `_mockScreenFadeToBlackActive/_mockScreenFadeToBlackUntil`. A short origin-shift or content AUP-shift try/finally window could therefore reset a real fade-to-black mask that was still active, pushing queued `Addressables.Release()` work back into visible traversal frames later.
Solution: Split explicit sanitizer Blind Frame windows into `_explicitBlindFrameWindowActive/_explicitBlindFrameWindowUntil`. `SetHeapSanitizerMockBlindFrame()` now owns only the mock fade state. `IsBlindReleaseFrame()` checks hard-reaper, explicit window, mock fade, then dispatcher zero-delta independently.
Rejected Alternatives: Reference-counting the blind-window state was rejected because callers are already scoped try/finally owners; separate timed lanes are simpler and avoid accidental cross-domain cancellation. Keeping shared fields was rejected because it made one visual mask overwrite another.
Scalability potential: Low devices can rely on multiple independent release masks without accidental cancellation; high/ultra retain longer TTL caches but still drain during the earliest valid mask.
Hardware Impact: Two scalar fields and one extra branch in the cold release predicate. The benefit is avoiding delayed release backlog and visible-frame release churn caused by a clobbered fade window.

Verification: Static re-scan after the blind-window state isolation patch found no sanitizer AUP/state regressions and no forbidden hot-path sanitizer patterns. The explicit governor cleanup call scan still shows origin/AUP cleanup wrapped in explicit Blind Frames and hard-ceiling cleanup wrapped in a VRAM panic window. XML self-audit parses. `git diff --check` reports only LF/CRLF warnings. Build remains deferred because CPU guard sampled 68.1%, 11.8%, 16.6%, and 36.0% with no compiler process; one sample violates the >50% rule.

Problem: The cold-miss Addressables path created an `AsyncOperationHandle` and then called `RegisterAddressableHandleSlot()` as a `void` method. Under a Vault resolve/slot validation failure, the newly created handle could escape to the caller without a tracker/map row, violating the no-orphan-handle contract.
Solution: Changed `RegisterAddressableHandleSlot()` to return `bool`. Both string-address and `AssetReferenceGameObject` cold-miss paths now fail closed: if the tracker row cannot be written, the just-created handle is released immediately, the out handle is reset to default, leak telemetry is dumped, and the acquire returns false.
Rejected Alternatives: Assuming registration cannot fail was rejected because Vault handles can be absent during bootstrap races or non-reload transitions. Queuing an untracked handle for later repair was rejected because it creates exactly the orphan class this sanitizer exists to prevent.
Scalability potential: All tiers prefer a missing asset retry/fallback over a leaked handle. Low devices avoid hidden managed heap growth; high/ultra preserve deterministic handle accounting even under larger cache tables.
Hardware Impact: 0 us on successful cache hit/cold-miss registration. Failure path pays one immediate `Addressables.Release()` for the just-created handle to prevent a permanent leak.

Verification: Static re-scan after the cold-miss fail-closed patch shows both `RegisterAddressableHandleSlot()` call sites guarded by failure release logic. Forbidden hot-path sanitizer scan remains clean. XML self-audit parses. `git diff --check` reports only LF/CRLF warnings. Build remains deferred because CPU guard sampled 100%, 99.8%, 62.8%, and 87.7% with no compiler process; all samples violate the >50% rule.

Problem: `ExecuteReleaseFlow()` treated every `ClearNativeHandleSlot()` failure as a reason to requeue. That is only correct when a scheduled TTL job still owns the Vault lanes. If the native map row is missing or corrupted, requeueing forever preserves the managed `AsyncOperationHandle` leak instead of eliminating it.
Solution: Added `IsTrackerMutationBlockedByScheduledJob()` to distinguish real dependency ownership from missing native metadata. If the TTL job is still running, release remains deferred. Otherwise the release flow clears the fixed managed pool slot best-effort, marks leak telemetry, and still releases the managed Addressables handle before removing the registry row.
Rejected Alternatives: Always releasing despite a running TTL job was rejected as a data race. Always requeueing on native clear failure was rejected because it converts tracker corruption into a permanent managed handle leak. Hard-resetting the entire Vault table on one row fault was rejected because it could invalidate unrelated live handles.
Scalability potential: Low devices avoid permanent leaks after rare tracker mismatch; high/ultra keep large cache tables without turning one corrupt row into unbounded residency.
Hardware Impact: Success path unchanged except one boolean. Fault path scans the fixed handle pool once and releases the managed handle, trading bounded O(capacity) cleanup for leak elimination.

Verification: Static re-scan after the native-slot fail-closed patch found the guarded release path and no forbidden sanitizer hot-path patterns. XML self-audit parses. `git diff --check` reports only LF/CRLF warnings. Build remains deferred because CPU guard sampled 76.2%, 98.5%, 73.5%, and 100% with no compiler process; all samples violate the >50% rule.

Problem: The cold-miss path assumed `LoadAssetAsync` always returns a valid handle. If Unity returned an invalid handle immediately, the sanitizer could write an active tracker row with no valid engine handle and force later cleanup to discover the mismatch.
Solution: Added an immediate `handle.IsValid()` guard after both string-address and `AssetReferenceGameObject` loads. Invalid handles are not registered, the out handle is reset, leak/fault telemetry is dumped, and acquire returns false.
Rejected Alternatives: Registering invalid handles and relying on the next cache hit to clear them was rejected because it pollutes the native table and leak UI. Releasing an invalid handle was rejected because Unity validity already says there is no owned handle to release.
Scalability potential: All tiers keep the native table clean under bad Addressables keys or catalog faults.
Hardware Impact: One validity branch on cold miss only. Prevents stale active rows and later cleanup scans.

Verification: Static re-scan after the invalid cold-handle rejection patch found no forbidden sanitizer hot-path patterns. XML self-audit parses. `git diff --check` reports only LF/CRLF warnings. Build remains deferred because CPU guard sampled 100%, 100%, 36.5%, and 13.4% with no compiler process; two samples violate the >50% rule.

Problem: The native-slot fail-closed path released the managed handle, but a stale native tracker/map row could remain if native clear failed before the registry row was removed. Later TTL evaluation cannot queue that row because `QueueExpiredAddressableRelease()` requires a managed registry record.
Solution: Hardened `SyncNativeRefCountsFromRegistry()`: when a native tracker's `AssetHash` has no managed registry record, the sanitizer now removes the handle-map entry, clears the fixed pool slot best-effort, recomputes bundle-prefix sharing, and zeroes tracker/TTL/flag lanes.
Rejected Alternatives: Leaving stale native rows for reset was rejected because telemetry/editor facade would keep reporting phantom active handles. Recreating managed records from native rows was rejected because the managed `AsyncOperationHandle` may already have been released.
Scalability potential: Low devices avoid phantom active handles that suppress reuse under pressure; high/ultra large tables stay self-healing after rare tracker mismatch.
Hardware Impact: Only runs during native sync passes after deferred mutation/faults. Cost is a bounded row cleanup and optional handle-pool scan for a faulted key.

Verification: Static re-scan after the stale native row scrub patch found no forbidden sanitizer hot-path patterns. XML self-audit parses. `git diff --check` reports only LF/CRLF warnings. Build remains deferred because CPU guard sampled 12.2%, 15.0%, 15.1%, and 51.6% with no compiler process; the last sample violates the >50% rule.

Problem: Compile proof was still pending after several sanitizer patches, and the CPU guard eventually allowed one bounded build attempt.
Solution: Ran `dotnet build Hecton8.Core.csproj --no-restore -m:1 -nr:false /p:UseSharedCompilation=false /v:minimal` only after CPU sampled 28.0%, 21.2%, 20.1%, 13.8% and no `dotnet/csc/VBCSCompiler` process existed.
Rejected Alternatives: Running build under earlier >50% CPU samples was rejected. Editing sibling AI/save/visor domains from the Addressables sanitizer pass was rejected because those compile errors are outside the memory sanitizer boundary.
Scalability potential: N/A.
Hardware Impact: N/A.

Verification Result: Build failed outside SHINOBU_67 touched files. Errors: `Assets/_Project/Scripts/AI/Ecosystem/ShinobuFloraFaunaSymbiosisSolver.cs(1363,37)` `math.reversebytes` missing; `Assets/_Project/Scripts/Core/HomeostasisBrain.ScalabilityDictator.cs(1977,58)` unassigned `sanitizedWeight`; `Assets/_Project/Scripts/SaveBinaryPayloadCodec.cs(476,81)` and `(589,21)` missing `IndustrialLoreBitMask`; `Assets/_Project/Scripts/Visor/HectonScooterVolumetricShaftsFeature.cs(935,17)` and `HectonAbyssalSsdoFeature.cs(402,17)` missing `HectonDrsRenderFeatureGate`. Existing `GlobalPhysicsStateManager` CS0649 warnings also remain. No compiler error referenced the SHINOBU_67 Addressables sanitizer files.
