# Manual Review Pass 7 - Native Lifetime And Runtime Mesh Closure

Status: STATIC METHOD REVIEW - NO UNITY/PROFILER PROOF
Date: 2026-06-02

## Scope

This pass reads surrounding methods for the highest-density unresolved runtime suspects from `HOTSPOT_REVIEW.md`. It does not close every `RUNTIME_PRECLASSIFICATION.md` line. It upgrades several vague native allocation and runtime mesh/material buckets into concrete proof/fix gates.

## Reviewed Callsite Groups

| File | Evidence Read | Static Classification | Required Closure |
|---|---|---|---|
| `Assets/_Project/Scripts/World/ProceduralWreckGenerator.cs` | `GenerateInternal()`/`GenerateInternalAsync()` call `BuildMergedMesh*()` when `wreckMaterialRegistry == null`; `BuildMergedMesh*()` and `BuildProxyMesh()` call `new Mesh()` and `Mesh.ApplyAndDisposeWritableMeshData`. The class is a `MonoBehaviour`, `IUpdatable`, `ISlowTickable`, and `ILateFrameTickable`, not editor-only. | `P0_RUNTIME_MESH_GENERATION_ROUTE_CONFIRMED_UNLESS_REGISTRY_MANDATORY` | Player build proof that `wreckMaterialRegistry` is mandatory and the mesh path is unreachable, or move mesh merge/proxy generation to editor/offline bake only. |
| `Assets/_Project/Scripts/World/ProceduralWreckGenerator.cs` | `HectonCompoundColliderAutoFitter` and primitive collider creation are under `#if UNITY_EDITOR`. | `LEGAL_EDITOR_OFFLINE_COLLIDER_FITTER` | No player proof needed for this fitter, but generated collider proxy output still needs prefab audit. |
| `Assets/_Project/Scripts/World/VegetationNavGridSynchronizer.cs` | Abyssal path scheduling allocates `NativeList<Vector3>` with `Allocator.Persistent` per request and copies job snapshots through H8Memory allocations; completion uses `DispatcherJobSwap.TryComplete`. | `RUNTIME_HOT_SUSPECT_NATIVE_SCRATCH_PER_PATH` | Replace with preallocated path-job scratch/double buffers or provide 300-frame path spam proof showing no post-bootstrap native allocation/growth and bounded completion windows. |
| `Assets/_Project/Scripts/World/VegetationFlowFieldIntegrator.cs` | Flow/threat/thermal solve staging allocates H8Memory persistent arrays in slow-tick job preparation and completes through dispatcher fences. | `YELLOW_OWNER_SLOW_TICK_NATIVE_STAGING` | Pool all recurring staging buffers or prove solve cadence, allocation count, and bytes are bounded after bootstrap. |
| `Assets/_Project/Scripts/World/GroundPenetratingRadarRuntime.cs` | Each radar job creates pending `Hits`, `SignalStrength`, `AgeSeconds`, `OreTypes`, `PingGpu`, `Counters`, and `MaxSignalStrength` arrays through H8Memory; SDF snapshots can allocate/grow to lease length. Fallback draw material is created if prefab material is missing. | `P1_RUNTIME_SCAN_STAGING_ALLOCATION` plus existing `P0_FALLBACK_MATERIAL_PROOF_GATE` | Preallocate radar pending buffers and max SDF snapshot capacity, or prove ping spam causes zero post-bootstrap allocation/growth; assign production material in prefab. |
| `Assets/_Project/Scripts/World/PersistentWorldRegistry.cs` | Sector load/write paths use `Allocator.TempJob` arrays/lists around async sector IO and convert loaded native records into managed arrays. | `YELLOW_STREAMING_SAVE_IO_TEMPJOB_ROUTE` | Prove these paths run only inside explicit async save/streaming windows with frame-budgeted cadence, not gameplay hot accessors; record IO latency, allocation, and frame impact. |
| `Assets/_Project/Scripts/UI/SettingsPanel.cs` | `CreateMenuStyleTextCold()` and `ConfigureMenuStyleLayoutCold()` create TMP text/layout components in cold menu assembly. | `LIKELY_LEGAL_COLD_UI_ASSEMBLY` | 300-frame menu open/change/close proof: no post-bootstrap hierarchy growth or repeated layout component creation after panel construction. |
| `Assets/_Project/Scripts/UI/FontAssetRecovery.cs` | Editor asset repair and material creation are guarded by `#if UNITY_EDITOR`; runtime `RefreshTextComponent()` can call `ForceMeshUpdate(true, true)`. | `EDITOR_REPAIR_LEGAL_RUNTIME_FORCE_MESH_UPDATE_PROOF_REQUIRED` | Prove runtime font refresh is bootstrap/recovery only and not called during steady-state text updates; all release prefabs must use assigned static font atlas/material assets. |
| `Assets/_Project/Scripts/Audio/NativeAudioFrameRingBuffer.cs` | `Initialize()` resolves DataVault once, allocates fixed native bridge buffers, validates power-of-two capacity, and reuses `Clear()` for same capacity. | `GREENISH_FIXED_AUDIO_RING_OWNER` | DSP/profile proof still required, but static shape is not a dynamic growth defect if initialization is boot-only. |
| `Assets/_Project/Scripts/Core/Memory/H8Memory.cs` | `CompleteAllOwnerJobs()` has explicit shutdown blocking comment; `EnsureTrackingCapacity()` can grow tracking arrays when allocation record capacity is exhausted. | `CORE_OWNER_LEGAL_WITH_GROWTH_PROOF_REQUIRED` | Prewarm tracking capacity for target scenes or provide H8Memory growth counters proving no tracking reallocation during gameplay. |
| `Assets/_Project/Scripts/Scavenging/ScavengingLootOracle.cs` | Native scratch is fixed-size persistent storage; post-simulation publish uses non-forced `DispatcherJobFence.TryComplete`; `EnsureHost()` creates a `HideAndDontSave` runtime GameObject and orphan cleanup scans `Resources.FindObjectsOfTypeAll`. | `GREENISH_FIXED_JOB_OWNER_WITH_BOOTSTRAP_HOST_PROOF_REQUIRED` | Author/bootstrap the runtime host or prove `EnsureHost()` and orphan scan are boot/reload-only, never normal gameplay cadence. |

## Release Meaning

- The wreck generator is now more strongly classified than earlier passes: the runtime mesh path is not editor-only by file structure.
- Native allocation comments and H8Memory ownership are not enough to pass zero-GC release law. Runtime staging buffers need either preallocation or profiler/native-memory evidence.
- UI cold assembly and editor repair paths are not defects by themselves. They still need steady-state proof because release UI acceptance is based on no post-bootstrap hierarchy/material/text-mesh churn.
- The audio ring remains one of the cleaner reviewed runtime data structures: fixed capacity, owner initialization, and no obvious per-frame growth in the read context.

## Non-Closure

This pass covers top hotspots only. Full closure still requires line-level classification of every `RUNTIME_TRIAGE.md` and `RUNTIME_PRECLASSIFICATION.md` entry, followed by Unity/player/profiler proof.
