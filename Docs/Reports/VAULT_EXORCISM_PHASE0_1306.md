# VAULT EXORCISM PHASE 0 - 1306

Date: 2026-05-25
Agent: 1306 / MEMORY_SOVEREIGN_CONSTRUCTION_EXORCIST
Requested domain: `Assets/Project/Scripts/Construction`
Audited source root: `Assets/_Project/Scripts/Construction`
Status: PENDING VERIFICATION

## Path Boundary Finding
`Assets/Project/Scripts/Construction` does not exist on disk. The active first-party construction source path is `Assets/_Project/Scripts/Construction`, matching `AGENTS.md` folder law. Phase 0 uses the real path to avoid a false clean result.

## [ANALYSIS]
Target: persistent native collection aliases in construction, habitat, pipe, logistics, and drone systems.
Affected systems: fluid pipe pressure runtime, habitat CSR/flood/stress graph, logistics pipe scheduler, drone fleet state/A* buffers, base catalog/socket/foundation vault view structs, repair-drone acoustic event lane.
Zero GC proof: no runtime code changed in Phase 0. Static Roslyn scan has zero parse failures. Existing hot paths still contain native alias candidates and remain PENDING MIGRATION.
State check: direct native fields exist in static managers, non-MonoBehaviour managers, transient view structs, and event lanes. `GlobalDataVault` handles already exist in some files but physical views are cached in fields.
Rule quote: "Hot paths use cached IDataVault, generation-checked handles, fixed snapshots, and fail-closed stale-handle behavior"; "A system may cache a DataVault handle. It may not allocate an independent persistent NativeArray."

## Task 01 - Native Alias Inquisition
Artifact: `Docs/Reports/VAULT_EXORCISM_REPORT_1306.json`

Roslyn scanner result:
- scanned C# files: 64
- parse failures: 0
- total native field declarations: 467
- transient job parameter fields allowed by scanner: 312
- forbidden persistent candidates: 155
- forbidden MonoBehaviour candidates: 19
- raw pointer fields: 44
- audit hash: `2152423d3c7b87e9ef58b5b8a3839167800e4a7b0b1780d19bead7445768908b`

Top hit list:
- `DroneFleetManager.cs`: 36 candidates
- `HabitatGraphManager.cs`: 21 candidates
- `FluidPipeGraphRuntime.cs`: 19 candidates
- `ShinobuSocketConstructionData.cs`: 19 candidates
- `FoundationSnappingCalculatorData.cs`: 15 candidates
- `HabitatConstructionManager.cs`: 10 candidates
- `LogisticsPipeTransportScheduler.cs`: 10 candidates
- `DroneFleetManager_Transactions.cs`: 8 candidates
- `LogisticsRouteScratchMemory.cs`: 7 candidates
- `BaseModuleCatalogRuntime.cs`: 6 candidates
- `DroneFleetNavigationKernel.cs`: 2 candidates
- `RepairDroneEntity.cs`: 2 candidates

## Task 02 - Ownership Provenance
Primary owner map:

| Owner | Lifecycle observed | Current owner flaw | Planned owner route |
|---|---|---|---|
| `FluidPipeGraphRuntime` | `Awake`/`OnEnable` -> `EnsureNativeState`; allocates 18 arrays + one `NativeParallelMultiHashMap` + one `NativeQueue`; schedules pressure solve; disposes in `OnDestroy`/shutdown | private physical aliases survive across frames and defrag phases | `SystemID.Construction`, planned range `12876000..12876024`; one handle per SoA lane; resolve only in `SlowTick`, registration commands, and `LateFrameTick` |
| `HabitatGraphManager` | constructor -> `AllocateNativeBuffers`; resizes via dispose/reallocate; flood job scheduled at line 2095; public read-only native accessors expose current arrays | non-MonoBehaviour manager owns graph/flood/stress arrays and static `s_latestSiegeTargets` alias | `SystemID.Construction`, planned range `12876025..12876055`; migrate CSR, flood, stress, siege, blackbox, and room connection buffers into Vault |
| `LogisticsPipeTransportScheduler` | static reset/shutdown; allocates/grows sort and visit scratch | static scheduler holds NativeArray fields and uses direct resize/replace | `SystemID.Construction`, planned range `12876056..12876070`; either Vault scratch handles or proven frame-local nonpersistent scratch. Prompt demands Vault path. |
| `DroneFleetManager` | static allocation via `AllocateHeadlessNativeMemory`; many buffers already use `ResolveDroneVaultBuffer`; jobs read/write cached physical arrays | residual state/render/AStar physical aliases remain long-lived, but service-command aliases and fallback H8Memory bridge are removed | existing IDs include `70240..70264`, `70269..70278`, and local free lanes `72043..72045`; remove remaining cached NativeArray fields and resolve handles per phase |
| `DroneFleetManager_Transactions` | static allocation via `AllocateDroneTransactionMemory`; transaction jobs scheduled from service command flow | transaction physical aliases were removed in Loop 7; transaction lane now uses handle-only descriptors | current IDs `72046..72053`; keep resolving views only for schedule/consume windows |
| `BaseModuleCatalogRuntime`, `ShinobuSocketConstructionData`, `FoundationSnappingCalculatorData` | static `TryResolve*Views` methods build view structs | view structs are not allocated fields, but they contain native fields and can be persisted accidentally by callers | keep BufferIDs already present; replace view structs with handle sets or method-scoped `out NativeArray<T>` resolution blocks |
| `HabitatConstructionManager.IntegrityGraphBuffers` | method-scoped graph validation scratch struct | appears transient, but struct fields can escape by ref through call chain | convert to handles or local resolution object with no storage across methods |
| `RepairDroneTorchAcousticEvents` | static `NativeQueue` lane with prewarm and frame dispatch | unmanaged queue is a global event lane outside first-party `SignalBus<T>`/Vault route | migrate to `SignalBus<RepairDroneTorchAcousticPayload>` or Vault-backed ring with read cursor |

## Task 03 - Dependency Impact
External read paths requiring pure accessors:
- `FluidPipeGraphRuntime.TryReadPipeNode` currently reads `_pipePressure`, `_pipeContents`, `_pipeFlags` directly and gates on `_solveScheduled`.
- `HabitatGraphManager` exposes `Nodes`, `EdgeOffsets`, `EdgeDestinations`, `EdgeResistance`, `RoomWaterLevels`, `RoomVolumes`, `RoomFlags`, `EdgeFlags` as `NativeArray<T>.ReadOnly`.
- `HabitatGraphManager.TryGetLatestSiegeTargets` returns `s_latestSiegeTargets.AsReadOnly()`.
- `FoundationPylonGpuBatch` consumes `FoundationSnappingVaultViews` and `ConstructionSocketVaultViews`.
- `BaseModuleCatalogRuntime.Try*` helpers consume `ModuleCatalogViews`.
- `DroneFleetManager_Transactions` and main `DroneFleetManager` share static drone arrays directly.

Migration rule:
- Public `Get/TryGet/Read` accessors must call `IDataVault.TryReadOnlyHandle` only.
- No accessor may create/grow buffers, publish signals, complete jobs, compact arrays, or poll `GlobalRegistry`.
- Write paths require `TryAcquireWriteLock` + `try/finally ReleaseWriteLock`.
- Jobs receive resolved `NativeArray<T>` views only; handles do not enter job structs.

Race risk:
- Highest risk is `HabitatGraphManager` because external systems consume graph snapshots while flood/stress jobs mutate internal buffers.
- Second risk is drone double-buffer swap: current code swaps NativeArray aliases and handles together. Migration must swap handles only and resolve front/back arrays inside the phase.
- Third risk is fluid pipe visual/audio rupture outputs. Presentation must read finalized snapshots in `LateFrameTick`, not simulation arrays mid-solve.

## Task 04 - DTO Layout Extraction
Static layout state:
- Existing primary DTOs are mostly already `[StructLayout(LayoutKind.Explicit, Size = X)]`: `FluidPipeTelemetryEntry` 32B, `FluidPipeRuptureRecord` 48B, `HabitatFloodConnection` 16B, `HabitatFloodBlackBoxEntry` 48B, drone state/path/telemetry DTOs, construction socket DTOs, foundation DTOs, drainage DTOs, catalog DTOs.
- `ConstructionTelemetryEntry` already exists as a 64B explicit validation telemetry struct in `ModularBaseConstructionValidator.cs`. It is not the memory-sovereignty telemetry requested by this batch.
- Non-DTO violation is not layout. It is alias lifetime: `ModuleCatalogViews`, `ConstructionSocketVaultViews`, `FoundationSnappingVaultViews`, and `IntegrityGraphBuffers` contain NativeArray fields and should not remain persistable structs.

Required next layout work:
- Do not add a second global `ConstructionTelemetryEntry` type. Either extend the existing 64B struct with compatible fields through a reviewed ABI migration, or create `ConstructionMemoryTelemetryEntry` and record the naming exception.
- Add `UnsafeUtility.SizeOf` and `OffsetOf` guards for the migrated telemetry DTO and any touched construction DTO.
- Keep every runtime DTO multiple-of-8 and avoid `bool`, managed refs, arrays, class refs, strings, and implicit enum backing.

## Task 05 - Telemetry Ring Plan
Target blackbox:
- BufferID: planned `ConstructionMemorySovereigntyTelemetry = (BufferID)12876000`
- Capacity: 300
- Owner: `SystemID.Construction`
- Dump target: `Docs/AgentLogs/Dump_1306_Construction.bin`
- Entry size: 64 bytes

Proposed `ConstructionMemoryTelemetryEntry` layout if existing `ConstructionTelemetryEntry` cannot be safely repurposed:
- offset 0: `ulong EventHash`
- offset 8: `ulong PhaseHash`
- offset 16: `uint Frame`
- offset 20: `uint BufferId`
- offset 24: `uint SystemId`
- offset 28: `uint Generation`
- offset 32: `uint ExpectedCapacity`
- offset 36: `uint ActualCapacity`
- offset 40: `uint Flags`
- offset 44: `uint ConsecutiveFailureCount`
- offset 48: `float JobMicroseconds`
- offset 52: `float QualityWeight`
- offset 56: `uint ProcessedCount`
- offset 60: `uint StateHash`

Failure flags:
- bit 0: resolve failed
- bit 1: read-only resolve failed
- bit 2: write lock contention
- bit 3: stale generation
- bit 4: capacity mismatch
- bit 5: non-finite simulation value
- bit 6: job fence not ready
- bit 7: dump requested

Cold boot:
- Ensure telemetry handle once through `GlobalDataVault.EnsureGenerationHandle<T>`.
- Cache handle only, not `NativeArray<T>`.
- On failure branches, resolve telemetry view inside method scope; if telemetry cannot resolve, increment scalar fail counter only.
- Binary dump copies the 300-entry ring oldest-to-newest with fixed header and no managed string payload.

## Verification
Compile/build was not launched. CPU load was 90 percent and AGENTS forbids dotnet build when CPU exceeds 50 percent. Static Roslyn parse for Construction completed with zero parse failures.

## Phase 0 Verdict
Codebase is not memory-sovereign in Construction. Several systems already use Vault handles, but they still cache physical NativeArray/NativeQueue aliases across frames. Migration must remove the physical alias fields, not just add more handles.

## T.A.R.S Review Addendum - 2026-05-25
Phase 1 partial corrections were applied after self-review:
- `DroneFleetManager.ResolveDroneVaultBuffer<T>()` no longer falls back to `H8Memory.Allocate(... Allocator.Persistent)`.
- `DroneFleetManager_Transactions.cs` no longer owns field-level transaction `NativeArray<T>` aliases. The partial stores handles only and resolves write/read views per phase.
- Transaction write buffers are acquired through `TryAcquireDroneTransactionWriteBuffers()` and released through `ReleaseDroneTransactionWriteBuffers()` in `finally`.
- Transaction DTO padding is byte-explicit for `DroneTaskDTO`, `DroneTransactionIntegrityDTO`, `DroneTransactionCommandDTO`, `DroneTransactionAupSnapshotDTO`, `DroneTransactionResultDTO`, `DroneTransactionCounterDTO`, and `DroneTransactionTelemetryEntry`.
- Current regex evidence: `DroneFleetManager_Transactions.cs` has 0 field-level native collection hits. Construction-wide regex evidence still reports 167 hits, so the domain remains not release-clean.
- Build/Roslyn rerun remains blocked by local protocol: CPU 70 percent and seven `dotnet` processes active.

## APEX Override Addendum 02 - 2026-05-25
Additional post-report corrections:
- `DroneFleetManager_Transactions.cs` blackbox dump route now targets `Docs/AgentLogs/Dump_1306_Construction.bin`; the prior SHINOBU route was invalid for agent 1306 proof.
- Residual wide padding was removed from touched drone DTOs: `DroneStateDTO`, `DroneChassisSpecDTO`, `PathWaypointDTO`, `DroneTransactionTelemetrySnapshot`, `DroneTransactionDebugTask`, `MockDroneSDFHeader`, and `DroneAStarPersistentState`.
- Strict touched-file text scan now reports 0 matches for `H8Memory.Allocate`, `Allocator.Persistent`, `new NativeArray/List/Queue/HashMap`, managed throw paths, string formatting, `.ToString()`, case-sensitive LINQ calls, `foreach`, interpolated strings, and string literal concatenation.
- Broad `new` keyword scan is still nonzero and mostly pre-existing; the domain cannot be called Zero-GC clean without deeper AST/IL classification and remaining owner migration.
- Latest environment check: CPU 31.9 percent, seven active `dotnet` processes. Build/Roslyn relaunch remains blocked by protocol because another dotnet is running.

## APEX Override Addendum 03 - Flat BufferID Cap Correction

The original Phase 0 planned ranges `12876000..12876070`, `12870271..12870278`, and `12873350..12873357` are invalid for the current `GlobalDataVault` implementation because flat metadata capacity is `100000`.

Corrected local 1306 lane map:
- `72032..72038`: `LogisticsRouteScratchMemory` CSR/BFS scratch lanes.
- `72039..72040`: `RepairDroneTorchAcousticEvents` pending and next-frame lanes.
- `72041..72042`: `HectonDroneFleetEvents` snapshot pending and next-frame lanes.
- `72043..72045`: drone chassis, CSV scratch, and AStar persistent-state lanes.
- `72046..72053`: drone transaction task/command/AUP/integrity/result/counter/mask/telemetry lanes.
- `72054..72060`: `LogisticsPipeTransportScheduler` CSR/topological-sort lanes.

Additional migration after this correction:
- `LogisticsRouteScratchMemory.cs` no longer owns seven static physical `NativeArray<T>` scratch aliases.
- `BaseLogisticsNetwork.TryResolveNearestStorageEndpoint()` resolves route scratch as phase-local `NativeArray<T>` views, executes the existing CSR BFS, and releases all vault write locks in `finally`.
- `LogisticsPipeTransportScheduler.cs` no longer owns topological-sort scratch `NativeArray<int>` fields. It stores handles only; valid DAG sorting remains an async Burst job over vault-resolved views, while invalid cyclic graphs fall back to deterministic registration-order replay.

Verification state:
- Source scan under `Assets/_Project/Scripts/Construction`: 0 hits for `(BufferID)128`, `128702`, `128733`, or `128760`.
- Targeted scan over the migrated logistics/repair/transaction files: 0 hits for `internal/private static NativeArray`, `new NativeArray`, `Allocator.Persistent`, `NativeQueue<`, `H8Memory.Allocate`, and `(BufferID)128`.
- Targeted scan over `LogisticsPipeTransportScheduler.cs`: 0 hits for `new NativeArray`, `Allocator.Persistent`, `NativeMemorySentinel`, `static NativeArray`, `NativeQueue<`, and `H8Memory.Allocate`.
- This addendum supersedes the old planned high-ID values in the Phase 0 tables; those values are historical evidence only, not current implementation targets.

## APEX Override Addendum 04 - Fluid Pipe Runtime Migration

Corrected local 1306 fluid lane map:
- `72080`: pipe pressure.
- `72081`: pipe contents.
- `72082`: pipe flags.
- `72083`: pipe content kind.
- `72084`: pipe network id.
- `72085`: pipe room index.
- `72086`: pipe capacity.
- `72087`: pipe max pressure.
- `72088`: pipe flow rate.
- `72089`: pipe source rate.
- `72090`: pipe demand rate.
- `72091`: pipe visual flow vector.
- `72092`: pipe room exchange contents.
- `72093`: pipe last visual flow.
- `72094`: pipe AUP.
- `72095`: pipe telemetry ring.
- `72096`: pipe rupture telemetry ring.
- `72097`: pipe rupture budget.
- `72098`: pipe connection sources.
- `72099`: pipe connection destinations.
- `72100`: pipe rupture dispatch.

Additional migration:
- `FluidPipeGraphRuntime.cs` no longer owns persistent pipe `NativeArray<T>` fields, `NativeParallelMultiHashMap<int,int>`, or `NativeQueue<FluidPipeRuptureRecord>`.
- `FluidPipePressureSolveJob` now receives only transient `NativeArray<T>` views and scalar capacities.
- Rupture output is a bounded vault array plus 3-slot budget. This rejects unbounded queue growth and makes overflow count explicit.
- `FluidPipeRuptureRecord` is 48 B with byte pads 36..47. `FluidPipeTelemetryEntry` is 32 B. Cold layout guards verify both.

Verification state:
- Strict scan over `FluidPipeGraphRuntime.cs`, `FluidPipePressureJobs.cs`, and `FluidPipeGraphTypes.cs`: 0 hits for `private NativeArray`, `static NativeArray`, `NativeParallelMultiHashMap`, `NativeQueue<`, `new NativeArray`, `new NativeQueue`, `Allocator.Persistent`, `NativeMemorySentinel`, `H8Memory.Allocate`, `DataVaultExempt`, `NativeMemoryOwner`, managed throw markers, string formatting, LINQ markers, `foreach`, and interpolated strings.
- Persistent-field regex under Construction now reports only `DroneFleetManager.cs:833-873,942-943` and `HabitatGraphManager.cs:263-283,287`.
- Build/dotnet was not launched by user order.

Current verdict:
- `FluidPipeGraphRuntime`, `LogisticsPipeTransportScheduler`, and `LogisticsRouteScratchMemory` are removed from the persistent native field residual list.
- Construction is still not release-clean until `DroneFleetManager` and `HabitatGraphManager` stop owning persistent native fields.

## APEX Override Addendum 05 - Residual Owner Scope

Residual owner scope after latest static scan:
- `DroneFleetManager.cs:833-873,942-943`: 35 static `NativeArray<T>` fields remain. Identifier references: 822 across the manager and transaction partial.
- `HabitatGraphManager.cs:263-283,287`: 21 native fields remain, including one `NativeParallelMultiHashMap<int, HabitatFloodConnection>`. Identifier references: 523.

Rejected shortcut:
- Do not wrap these fields in a holder struct or property facade. That would make the regex cleaner and the ownership model worse.
- Do not add hot `GlobalRegistry.DataVault` property accessors for every read. That violates pure accessor and hot-polling rules.

Required next implementation slices:
- Drone service-command lanes first, because they are already represented by `VaultGenerationHandle<DroneServiceCommand>` and `VaultGenerationHandle<DroneServiceCommandCursor>`.
- Drone double-buffer state/render arrays second, because handle swaps already exist but physical arrays are still swapped.
- Habitat flood CSR arrays third, with the `NativeParallelMultiHashMap` replaced by flat connection arrays before job scheduling.

Current verdict:
- Not release-clean.
- No compile/build proof was attempted, by user order.

## APEX Override Addendum 06 - Drone Service Command Slice

Service-command migration:
- `DroneFleetManager.cs` no longer owns static physical service-command buffers. Removed aliases: `NativeArray<DroneServiceCommand>` and `NativeArray<DroneServiceCommandCursor>`.
- Service-command descriptors remain as `VaultGenerationHandle<DroneServiceCommand>` and `VaultGenerationHandle<DroneServiceCommandCursor>` on existing BufferIDs `70269` and `70270`.
- Schedule phase acquires write views through `GlobalDataVault.TryAcquireWriteLock`.
- Completion phase drains `DroneServiceCommand` through the local view and releases cursor/command locks.
- Early absent-state completion now releases service-command locks before returning.

Fallback bridge removal:
- `DroneFleetManager.cs` and `DroneFleetManager_Transactions.cs` scan to 0 hits for `NativeMemorySentinel`, `H8Memory.Allocate`, `H8Memory.Release`, `Allocator.Persistent`, `new NativeArray`, `new NativeQueue`, `NativeQueue<`, and `NativeParallelMultiHashMap`.

Residual owner scope:
- `DroneFleetManager.cs:831-871`: static native fields remain for task counts, drone state/render buffers, SoA, blackbox, tuning, macro route, AStar, task claim, telemetry accumulator, task heap, DTO mirrors, procedural args, spatial buckets, chassis specs, and editor CSV scratch.
- `HabitatGraphManager.cs:263-283,287`: native fields remain for habitat graph/flood/flood-blackbox/siege buffers plus `NativeParallelMultiHashMap<int, HabitatFloodConnection>`.

Current verdict:
- Service-command lane is no longer a persistent native field residual.
- Construction is still not release-clean because the residual owners above remain.
- No compile/build proof was attempted, by user order.

## APEX Override Addendum 07 - Drone Task Selection Scratch Slice

Task-selection scratch migration:
- Removed cached field-level native aliases for `s_TaskClaimCounts` and `s_DroneTaskPriorityHeap`.
- Both lanes now remain handle-only and are resolved as local write views inside `TryAssignFleetTask()`.
- Claim-count view is passed explicitly through task candidate and claim rebuild helpers.
- Priority heap view is passed into `DroneTaskNativeMinHeap.Nodes` only for the task-selection window.
- Both write locks are released before `PublishSnapshot()`.

Residual owner scope:
- `DroneFleetManager.cs:831-868`: static native fields remain for drone state/render/culling/SoA/blackbox/tuning/macro route/AStar/claim owners/telemetry/DTO mirrors/procedural/spatial/chassis/editor CSV.
- `HabitatGraphManager.cs:263-283,287`: native fields remain for habitat graph/flood/flood-blackbox/siege buffers plus `NativeParallelMultiHashMap<int, HabitatFloodConnection>`.

Current verdict:
- Task-selection scratch lanes are no longer persistent native field residuals.
- Construction is still not release-clean because the residual owners above remain.
- No compile/build proof was attempted, by user order.

## APEX Override Addendum 08 - Drone Chassis Specs And CSV Scratch Slice

Chassis/spec migration:
- Removed cached field-level native alias `s_DroneChassisSpecs`.
- Removed editor-only cached field-level native alias `s_DroneSpecsCsvScratch`.
- Removed `s_DroneChassisSpecsVaultBacked` and `s_DroneSpecsCsvScratchVaultBacked`.
- Chassis specs now resolve through `VaultGenerationHandle<DroneChassisSpecDTO>` on BufferID `72043`.
- Editor CSV scratch now resolves through `VaultGenerationHandle<byte>` on BufferID `72044`.
- Chassis clear/commit uses write-lock/finally release.
- Chassis read uses `TryReadOnlyHandle` and local read-only view.
- CSV import uses local write view for the import window and releases in `finally`.

Residual owner scope:
- `DroneFleetManager.cs:831-865`: static native fields remain for drone state/render/culling/SoA/blackbox/tuning/macro route/AStar/claim owners/telemetry/DTO mirrors/procedural/spatial.
- `DroneFleetManager.cs:1838/1848`: helper return-type false positives, not persistent fields.
- `HabitatGraphManager.cs:287`: latest exact `private static NativeArray<...>` hit; broader habitat native field block around `263-283` still needs AST classification before a clean claim.

Current verdict:
- Drone chassis specs and editor CSV scratch are no longer persistent native field residuals.
- Construction is still not release-clean because drone state/render/AStar/spatial fields and habitat graph fields remain.
- No compile/build proof was attempted, by user order.

## APEX Override Addendum 09 - Drone Tuning And Headless Scratch Slice

Tuning/scratch migration:
- Removed cached field-level native alias `s_DroneTuningConstants`.
- Removed cached field-level native alias `s_HeadlessTaskClaimOwners`.
- Removed cached field-level native alias `s_FleetTelemetryAccumulator`.
- Removed the three corresponding vault-backed boolean fields.
- Drone tuning now reads through `VaultGenerationHandle<DroneFleetTuningConstants>` and `TryReadOnlyHandle`.
- Tuning writes acquire a local write view and release in `finally`.
- Headless task-claim and fleet telemetry scratch now acquire local write views before the drone job chain is scheduled.
- The write locks remain held until `CompleteHeadlessSimulationAndApply()`, reset completion, or native release, so worker jobs do not write into relocatable unpinned views.

Residual owner scope:
- `DroneFleetManager.cs:831-862`: static native fields remain for drone state/render/culling/SoA/blackbox/macro route/AStar/DTO mirrors/procedural/spatial.
- `DroneFleetManager.cs:1849/1859`: helper return-type false positives, not persistent fields.
- `HabitatGraphManager.cs:287`: latest exact `private static NativeArray<...>` hit; broader habitat native field block around `263-283` still needs AST classification before a clean claim.

Current verdict:
- Drone tuning constants, task-claim owners, and fleet telemetry accumulator are no longer persistent native field residuals.
- Construction is still not release-clean because drone state/render/AStar/spatial fields and habitat graph fields remain.
- No compile/build proof was attempted, by user order.

## APEX Override Addendum 10 - Drone Black Box Ring Slice

Black-box migration:
- Removed cached field-level native alias `s_DroneBlackBox`.
- Removed `s_DroneBlackBoxVaultBacked`.
- Black-box capture now resolves `VaultGenerationHandle<DroneFleetBlackBoxEntry>` through a local write view.
- Capture releases the black-box write lock in `finally`.
- Failure dump functions receive the local black-box view as a parameter and keep the same dump destinations.

Residual owner scope:
- `DroneFleetManager.cs:831-861`: static native fields remain for drone state/render/culling/SoA/macro route/AStar/DTO mirrors/procedural/spatial.
- `DroneFleetManager.cs:1852/1862`: helper return-type false positives, not persistent fields.
- `HabitatGraphManager.cs:287`: latest exact `private static NativeArray<...>` hit; broader habitat native field block around `263-283` still needs AST classification before a clean claim.

Current verdict:
- Drone black-box ring is no longer a persistent native field residual.
- Construction is still not release-clean because drone state/render/AStar/spatial fields and habitat graph fields remain.
- No compile/build proof was attempted, by user order.

## APEX Override Addendum 11 - Drone Procedural Args Slice

Procedural args migration:
- Removed cached field-level native alias `s_DroneProceduralArgs`.
- Removed `s_DroneProceduralArgsVaultBacked`.
- Procedural indirect args remain only as `VaultGenerationHandle<DroneProceduralIndirectArgsDTO>`.
- Schedule phase optionally locks the one-row args lane and passes the local view to `BuildDroneProceduralArgsJob`.
- Completion/reset/native release releases the optional args lock with the headless scratch path.
- Render phase resolves a local current-phase Vault view for the one-row GPU upload.

Residual owner scope:
- `DroneFleetManager.cs:831-860`: static native fields remain for drone state/render/culling/SoA/macro route/AStar/DTO mirrors/spatial.
- `DroneFleetManager.cs:1856/1866`: helper return-type false positives, not persistent fields.
- `HabitatGraphManager.cs:287`: latest exact `private static NativeArray<...>` hit; broader habitat native field block around `263-283` still needs AST classification before a clean claim.

Current verdict:
- Drone procedural indirect args lane is no longer a persistent native field residual.
- Construction is still not release-clean because drone state/render/AStar/spatial fields and habitat graph fields remain.
- No compile/build proof was attempted, by user order.

## APEX Override Addendum 12 - Drone Render Upload Staging Slice

Render/culling staging migration:
- Removed cached field-level native alias `s_DroneRenderInstances`.
- Removed cached field-level native alias `s_DroneCullingStates`.
- Removed `s_DroneRenderInstancesVaultBacked` and `s_DroneCullingStatesVaultBacked`.
- Removed dead helper `ReleaseDroneCullingStatesBuffer()`.
- Render and culling staging remain only as Vault generation handles.
- `RenderRealHeadlessFleet()` acquires local render/culling write views and releases both locks in `finally`.
- `PrepareDroneRenderInstances()` writes only to caller-supplied local views.

Residual owner scope:
- `DroneFleetManager.cs:831-856`: static native fields remain for drone state buffers, render matrix double buffer, SoA state, macro route, AStar, DTO mirrors, and spatial hash buffers.
- `DroneFleetManager.cs:1862`: method return-type false positive, not a persistent field.
- `HabitatGraphManager.cs:287`: remaining exact `private static NativeArray<HabitatSiegeTargetSnapshot>` hit.

Current verdict:
- Drone render/culling staging lanes are no longer persistent native field residuals.
- Construction is still not release-clean because drone state/render matrix/AStar/spatial fields and habitat graph fields remain.
- No compile/build proof was attempted, by user order.

## APEX Override Addendum 13 - Drone Spatial And Assignment Scratch

Spatial/assignment migration:
- Removed cached field-level native aliases `s_DroneSpatialBucketHeads`, `s_DroneSpatialNextIndices`, and `s_DroneSpatialKeys`.
- Removed cached field-level native alias `s_DroneAssignmentTasks`.
- Removed corresponding vault-backed booleans.
- Spatial hash construction now writes to caller-supplied local Vault views.
- Assignment task-map construction now writes to caller-supplied local Vault view.
- Headless job completion releases spatial and assignment scratch locks with the existing headless scratch fence.

Residual owner scope:
- `DroneFleetManager.cs:831-852`: static native fields remain for drone state buffers, render matrix double buffer, SoA state, macro route/AStar buffers, and state/target DTO mirrors.
- `DroneFleetManager.cs:1874`: method return-type false positive, not a persistent field.
- `HabitatGraphManager.cs:287`: remaining exact `private static NativeArray<HabitatSiegeTargetSnapshot>` hit.

Current verdict:
- Drone spatial scratch and assignment-task lanes are no longer persistent native field residuals.
- Construction is still not release-clean because drone state/render matrix/SoA/AStar/DTO fields and habitat graph fields remain.
- No compile/build proof was attempted, by user order.

## APEX Override Addendum 14 - Drone AStar Macro And Habitat Static Siege Alias

Drone AStar/macro migration:
- Removed cached static physical aliases for drone macro waypoints, macro waypoint states, AStar open heap, AStar g-costs, AStar came-from, AStar node states, macro route nodes, macro route counts, AStar telemetry, and AStar persistent search states.
- Removed their vault-backed booleans.
- Headless schedule now acquires all ten lanes as local write views and holds locks through the job fence.
- `ScheduleDroneMacroAStar()` takes caller-supplied local views only.
- Telemetry readback and debug-route export resolve local Vault views.

Habitat static siege cleanup:
- Removed `s_latestSiegeTargets` static `NativeArray<HabitatSiegeTargetSnapshot>`.
- Latest siege getter now uses the owner instance `_siegeTargets` and clamps count to buffer length.

Verification:
- Prompt SHA-256 remains `98a598081b61322f5f770e19f5eefdb95e1e261ae6424b04e6f4c0f0468f10c4`.
- Exact removed-alias scan reports 0 hits.
- Drone forbidden scan reports 0 hits.
- Brace delta over touched source files is 0.
- `git diff --check` returns exit 0 with CRLF warnings only.
- Dotnet/build not launched.

Current verdict:
- Drone AStar/macro scratch lanes and Habitat latest siege static alias are no longer persistent static native field residuals.
- Construction remains not release-clean: drone core state/render/DTO static fields remain at `DroneFleetManager.cs:831-842`; Habitat instance native owners and sentinel registrations remain unresolved.

## APEX Override Addendum 15 - Drone Mirror DTO Lanes

Drone mirror migration:
- Removed cached static physical aliases `s_DronePositionsSoA`, `s_DroneStateBytes`, `s_DroneStateDtos`, and `s_DroneTargetDtos`.
- Removed corresponding vault-backed booleans.
- Headless job schedule now locks the four mirror lanes as local write views and holds them through job completion.
- Completion, docking controls, service drain, pending launches, slot clearing, and origin shift now use explicit local mirror views.
- Headless scratch partial-acquire failure cleanup from this addendum was re-audited in Addendum 16 and corrected to release `acquiredCount`.

Verification:
- Prompt SHA-256 remains `98a598081b61322f5f770e19f5eefdb95e1e261ae6424b04e6f4c0f0468f10c4`.
- Exact mirror alias scan reports 0 hits.
- Drone forbidden scan reports 0 hits.
- Brace delta is 0.
- `git diff --check` returns exit 0 with CRLF warnings only.
- Dotnet/build not launched.

Current verdict:
- Drone mirror/DTO lanes are no longer persistent static native field residuals.
- Remaining exact static native residuals: `DroneFleetManager.cs:831-834` state/render double buffers and generic helper return `DroneFleetManager.cs:1912`.

## APEX Override Addendum 16 - Drone Core State/Render Completion

Drone core migration:
- Removed final drone static physical aliases `s_DroneStates`, `s_DroneStateBackBuffer`, `s_DroneRenderMatrices`, and `s_DroneRenderMatrixBackBuffer`.
- Removed `s_DroneStatesVaultBacked`, `s_DroneStateBackBufferVaultBacked`, `s_DroneRenderMatricesVaultBacked`, and `s_DroneRenderMatrixBackBufferVaultBacked`.
- Cold boot now ensures only `VaultGenerationHandle<T>` descriptors for drone core buffers.
- Headless schedule acquires core state/render buffers as local write views with the scratch set and releases 24 lanes at the job fence.
- Completion swaps only handles, then reopens phase-local core views from the swapped descriptors.
- Origin shift, pending controls, docking signals, docking aborts, service completion, service command drain, pending launch, slot clear, render upload, render instance prep, black-box capture, telemetry relay, and debug route export no longer read cached state/render fields.

Lock correction:
- Previous Loop 24 claim was wrong. `acquiredCount - 1` leaked a held lock.
- Current partial failure cleanup releases `acquiredCount`.
- Release map now covers 24 lanes: four core buffers, task claim/telemetry/assignment, spatial hash, AStar/macro, mirror DTO lanes.

Verification:
- Prompt SHA-256 remains `98a598081b61322f5f770e19f5eefdb95e1e261ae6424b04e6f4c0f0468f10c4`.
- Exact core alias scan reports 0 hits.
- Exact core vault-backed boolean scan reports 0 hits.
- Construction static native field scan reports 0 hits for static field declarations.
- Drone forbidden scan reports 0 hits.
- Brace balance for `DroneFleetManager.cs` is 0.
- `git diff --check` returns exit 0 with CRLF warnings only.
- Dotnet/build not launched.

Current verdict:
- Drone static native alias class is purged.
- Remaining broad-domain blockers are Habitat instance native fields and sentinel/allocation paths, not drone static fields.

## APEX Override Addendum 17 - Habitat Siege Target Instance Alias

Habitat siege migration:
- Removed instance physical alias `private NativeArray<HabitatSiegeTargetSnapshot> _siegeTargets`.
- Added `HabitatSiegeTargetsBufferId=(BufferID)72122`.
- Added `VaultGenerationHandle<HabitatSiegeTargetSnapshot> _siegeTargetsHandle`.
- `PublishSiegeTargetSnapshot()` and `ClearSiegeTargetSnapshot()` use local Vault write views with `finally` lock release.
- `TryGetLatestSiegeTargets()` resolves a read-only Vault view through the owner handle and fails closed on stale/missing/undersized storage.

DTO byte map:
- `HabitatSiegeTargetSnapshot` remains 48 B.
- Offsets: `ModuleCenter` 0..11, `WeakPoint` 12..23, `Integrity01` 24..27, `Vulnerability01` 28..31, `NodeId` 32..35, `Flags` 36, `Reserved0..2` 37..39, `_pad0.._pad7` 40..47.
- Size is 48, divisible by 8. Padding is byte-explicit.

Verification:
- Prompt SHA-256 remains `98a598081b61322f5f770e19f5eefdb95e1e261ae6424b04e6f4c0f0468f10c4`.
- Exact physical siege field scan reports 0 hits.
- `(BufferID)72122` source scan reports only the new Habitat lane.
- Brace balance for `HabitatGraphManager.cs` is 0.
- `git diff --check` returns exit 0 with CRLF warning only.
- Dotnet/build not launched.

Current verdict:
- Habitat siege snapshot storage is now handle-only.
- Construction remains not release-clean: `HabitatGraphManager.cs:280-296` still holds 17 instance native owners and persistent allocation/sentinel paths remain.

## APEX Override Addendum 18 - Habitat Module/Room Runtime Storage

Scope:
- `Assets/_Project/Scripts/Construction/HabitatGraphManager.cs`

What changed:
- Removed physical instance fields for module-stress lanes and room-flood lanes.
- Added handle-only BufferIDs:
  - `72123` module stress scalars
  - `72124` previous module stress scalars
  - `72125` impact stress spikes
  - `72126` module compromised flags
  - `72127` room water levels
  - `72128` room volumes
  - `72129` room flood delta levels
  - `72130` room flags
- Module stress and room flood consumers now resolve phase-local Vault views; writes release in `finally`.
- Flood propagation keeps room-state locks across `HabitatFloodPropagationJob` and releases after delta consumption.

Verification:
- Exact removed-name scan reports 0 physical field uses for the eight migrated lanes.
- `HabitatGraphManager.cs` forbidden module/room allocation scan reports 0 hits for `new NativeArray<...module>`, `Allocator.Persistent...module`, and room physical field names.
- `HabitatGraphManager.cs` residual `private NativeArray<T>` scan now reports only graph lanes at `288-296`.
- Brace balance is 0.
- `git diff --check` returns exit 0 with CRLF warning only.
- Dotnet/build not launched.

Current verdict:
- Habitat module-stress and room-flood lanes are handle-only.
- Construction remains not release-clean until graph CSR/traversal lanes are migrated.

## APEX Override Addendum 19 - Habitat Graph CSR Runtime Storage

Scope:
- `Assets/_Project/Scripts/Construction/HabitatGraphManager.cs`
- Cross-domain release hook: `Assets/_Project/Scripts/ConstructionManager.cs`

What changed:
- Removed physical instance graph lanes from `HabitatGraphManager`: nodes, CSR offsets, CSR destinations, edge resistance, CSR write cursor, anchor reachability, traversal visited, traversal queue, edge flags.
- Added handle-only BufferIDs:
  - `72131` graph nodes
  - `72132` graph edge offsets
  - `72133` graph edge destinations
  - `72134` graph edge resistance
  - `72135` graph edge write cursor
  - `72136` graph anchor reachability
  - `72137` graph traversal visited scratch
  - `72138` graph traversal queue scratch
  - `72139` graph edge flags
- Rebuild/runtime rupture phases now use local `HabitatGraphWriteViews`.
- Flood propagation now leases CSR/edge-flag graph views across the scheduled job fence.
- Deconstruction CSR lanes are leased only through `TryGetDeconstructionCsrLanes()` and released by `ConstructionManager` immediately after `ExecuteModuleTeardownJob.Execute()`.

Verification:
- Exact graph physical-name scan reports 0 hits for `_nodes`, `_edgeOffsets`, `_edgeDestinations`, `_edgeResistance`, `_edgeWriteCursor`, `_anchorReachability`, `_traversalVisited`, `_anchorTraversalQueue`, `_edgeFlags`.
- `HabitatGraphManager.cs` scan reports 0 hits for `private NativeArray<`, `new NativeArray<`, `Allocator.Persistent`, `NativeMemorySentinel`, `NativeMemoryOwner`, `NativeMemoryLifetime`, `DisposeNativeArray`, `H8Memory.Allocate`, `H8Memory.Release`.
- Brace balance is 0.
- `git diff --check` over `HabitatGraphManager.cs` and `ConstructionManager.cs` returns exit 0 with CRLF warnings only.
- Dotnet/build not launched.

Current verdict:
- `HabitatGraphManager.cs` is no longer a physical native owner.
- Remaining Construction-wide native signatures are separate owners/API surface and not residual Habitat graph fields.
