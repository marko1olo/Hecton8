# AgentLogs_Batch010 AllocationAudit SLIM
Scope: Batch010
Source: C:\hades\Hecton8\Docs\Archive\Batch010\AgentLogs
FileCount: 1
Separator: ===== FILE: name =====

===== FILE: AllocationAudit_SHINOBU_100.md =====
SHINOBU_100 Native Allocation Audit
Date: 2026-05-19
Evidence class: STATIC_SOURCE
Scope: `Assets/_Project/Scripts/AI`, `Assets/_Project/Scripts/Physics`, plus explicitly named `FaunaSimulationEngine` and `VehicleMotor`.
AI/Physics Requested Types
Static scan found no direct `new NativeArray`, `new NativeList`, `new NativeHashMap`, or `new NativeParallelHashMap` using `Allocator.Persistent` under `Assets/_Project/Scripts/AI` or `Assets/_Project/Scripts/Physics`.
AI/Physics Persistent Queues And Adjacent Collections
Priority File:Line Owner Collection Element Capacity Current route Teardown
--- --- --- --- --- --- --- ---
RESOLVED `Assets/_Project/Scripts/AI/Sensory/AcousticEchoLocationRuntime.cs:224` `AcousticEchoLocationRuntime` `NativeQueue ` `EchoTap` `64` / `MaxQueuedEchoTaps` Replaced by `BufferID.AcousticEchoPendingTaps` Vault buffer; frame taps/result/blackbox remain Vault handles no owner-local queue remains
RESOLVED `Assets/_Project/Scripts/Physics/Vehicles/SubmarineDynamicsRuntime.cs:1025` `SubmarineDynamicsRuntime` `NativeQueue ` `MockFloodSignal` `64` Replaced by `SignalBus ` lane no owner-local queue remains
RESOLVED `Assets/_Project/Scripts/Physics/Vehicles/SubmarineDynamicsRuntime.cs:1032` `SubmarineDynamicsRuntime` `NativeQueue ` `MockImpactSignal` `64` Replaced by `SignalBus ` lane no owner-local queue remains
RESOLVED `Assets/_Project/Scripts/Physics/Vehicles/SubmarineDynamicsRuntime.cs:1039` `SubmarineDynamicsRuntime` `NativeQueue ` `CavitationAcousticSignal` `64` Replaced by `SignalBus ` lane no owner-local queue remains
RESOLVED `Assets/_Project/Scripts/Physics/GlobalPhysicsStateManager.Shinobu37PhysicsCulling.cs` `GlobalPhysicsStateManager` `NativeQueue ` `int` `MaxTrackedBodies` Replaced by `BufferID.ShinobuPhysicsCullingChangedIndices` plus 64B `BufferID.ShinobuPhysicsCullingChangedCount` atomic counter no owner-local queue remains
RESOLVED `Assets/_Project/Scripts/Physics/GlobalPhysicsStateManager.Shinobu37PhysicsCulling.cs` `GlobalPhysicsStateManager` `NativeQueue ` `PhysicsCullingTargetWakeRequestSignal` `64` Replaced by `BufferID.ShinobuPhysicsCullingWakeRequestMirror` plus 64B `BufferID.ShinobuPhysicsCullingWakeRequestCount` counter no owner-local queue remains
RESOLVED `Assets/_Project/Scripts/Physics/GlobalPhysicsStateManager.Shinobu37PhysicsCulling.cs` `GlobalPhysicsStateManager` `NativeParallelMultiHashMap ` `int,int` `MaxTrackedBodies` Replaced by Vault SoA: `ShinobuPhysicsCullingSpatialBucketHeads`, `SpatialNext`, and `SpatialCellHashes` no owner-local multi-hash map remains
RESOLVED `Assets/_Project/Scripts/Physics/Buoyancy/PhysicsApplySystem.BuoyancyQueue.cs` `PhysicsApplySystem` / SHINOBU_158 force transfer `NativeQueue ` `BuoyancyForcePacketDTO` `8192` soft capacity Replaced by Vault force-packet window `(BufferID)71621` plus 64B `BuoyancyCounterDTO` at `ShinobuBuoyancyCounters` (71630) no owner-local queue remains
Explicitly Named Legacy Outside AI/Physics Folder
Priority File:Line before migration Owner Collection Element Capacity Fix
--- --- --- --- --- --- ---
P0 `Assets/_Project/Scripts/Gameplay/VehicleMotor.cs:1437` `VehicleMotor` `NativeArray ` `SubmarineState` `1` Migrated to `VaultBufferHandle ` via `BufferID.VehicleMotorSubmarineStates`, capacity `32`
P0 `Assets/_Project/Scripts/Gameplay/VehicleMotor.cs:1628` `VehicleMotor` `NativeArray ` `CapsulecastCommand` `1` Migrated to `VaultBufferHandle ` via `BufferID.VehicleMotorSweepCommands`, per-motor subarray
P0 `Assets/_Project/Scripts/Gameplay/VehicleMotor.cs:1639` `VehicleMotor` `NativeArray ` `RaycastHit` `8` Migrated to `VaultBufferHandle ` via `BufferID.VehicleMotorSweepResults`, per-motor subarray
Static Post-Edit Scan
`rg -n "new Native(Array List HashMap ParallelHashMap).*Allocator\\.Persistent new NativeQueue.*Allocator\\.Persistent new NativeParallelMultiHashMap.*Allocator\\.Persistent" Assets/_Project/Scripts/AI Assets/_Project/Scripts/Physics Assets/_Project/Scripts/Gameplay/VehicleMotor.cs`
Result after SHINOBU_100 Loop 4: no matches in `Assets/_Project/Scripts/Physics/GlobalPhysicsStateManager.Shinobu37PhysicsCulling.cs`. targeted culling bridge now resolves all persistent native storage through GlobalDataVault `VaultBufferBinding `.
`rg -n "Pack\\s*=\\s*1 StructLayout\\(LayoutKind\\.Sequential" Assets/_Project/Scripts/Gameplay/VehicleMotor.cs Assets/_Project/Scripts/Fauna/FaunaSimulationEngine.cs Assets/_Project/Scripts/Physics/Vehicles/SubmarineDynamicsContracts.cs Assets/_Project/Scripts/AI/Sensory/AcousticEchoLocationRuntime.cs Assets/_Project/Scripts/Core/Memory/VaultMemoryContracts.cs`
Result: no matches.
Loop 5 ABI / Dependency Post-Edit Scan
`rg -n "GlobalRegistry\\.(DataVault Get TryGet)" Assets/_Project/Scripts/Core/SystemDispatcher.cs Assets/_Project/Scripts/Core/Memory Assets/_Project/Scripts/GlobalPhysicsStateManager.cs Assets/_Project/Scripts/Physics/GlobalPhysicsStateManager.Shinobu37PhysicsCulling.cs Assets/_Project/Scripts/Gameplay/VehicleMotor.cs Assets/_Project/Scripts/AI/Sensory/AcousticEchoLocationRuntime.cs`
Result: no matches.
`rg -n "StructLayout\\(LayoutKind\\.Sequential Pack\\s*=\\s*1" Assets/_Project/Scripts/Core/Memory/H8Memory.cs Assets/_Project/Scripts/Core/Memory/GlobalDataVault.cs Assets/_Project/Scripts/Core/Memory/VaultMemoryContracts.cs Assets/_Project/Scripts/Core/SystemDispatcher.cs Assets/_Project/Scripts/GlobalPhysicsStateManager.cs Assets/_Project/Scripts/Physics/GlobalPhysicsStateManager.Shinobu37PhysicsCulling.cs`
Result: only generic handle/view exceptions remain:
`GlobalDataVault.cs:195` `VaultBufferHandle `: generic handle, 24B, size asserted; explicit layout rejected due CLR/IL2CPP generic explicit-layout risk.
`GlobalDataVault.cs:297` `VaultBufferSlice `: generic view, 32B, size asserted; explicit layout rejected due CLR/IL2CPP generic explicit-layout risk.
External non-SHINOBU ABI debt observed by broader scan and not edited:
`Assets/_Project/Scripts/AI/Pathfinding/PathFunnelContracts.cs`: several `Pack=1` explicit structs under AI pathfinding ownership.
`Assets/_Project/Scripts/AI/Ambient/AmbientBiotaDirector.cs`: `Pack=1` visual instance and `CompileSynchronously = false` Burst jobs under Ambient Biota ownership.
SHINOBU BufferID collision check:
`636-641`: no source collisions outside SHINOBU enum declarations.
`70630-70635`: no source collisions outside SHINOBU enum declarations.
Broad enum duplicate scan still reports non-SHINOBU collisions: `70200 SaveWorldPagerWriteArena/ConstructionBuilderOccupancy`, `70550 BabelSubtitleCueState/ShinobuLogisticsCsvScratch`, and `70800-70807 AudioStem*/ShinobuActiveEquipment*`.
Loop 6 Physics Culling Scratch Post-Edit Scan
`rg -n "NativeQueue NativeParallelMultiHashMap Allocator\\.Persistent private readonly byte\\[] private readonly int3\\[]" Assets/_Project/Scripts/Physics/GlobalPhysicsStateManager.Shinobu37PhysicsCulling.cs`
Result: no matches. only remaining managed scratch in culling partial is `_physicsFrustumPlaneScratch = new Plane[6]`, pre-existing Unity API interop array for `GeometryUtility.CalculateFrustumPlanes(Camera, Plane[])`, not persistent native or rollback-authoritative state.
New Vault scratch buffers:
`BufferID.ShinobuPhysicsCullingCsvScratch = 70636`, `byte`, capacity `4096`.
`BufferID.ShinobuPhysicsCullingLegacyRadiiScratch = 70637`, `byte`, capacity `64`.
Collision check:
`70636-70637`: no source collisions outside SHINOBU enum declarations.
Build gate:
Not run. CPU measured `18.0%`, but active `dotnet` processes were present, so AGENTS/user rules forbid `dotnet build`.
Loop 7 Ecosystem/Buoyancy Post-Edit Scan
`rg -n "H8Memory\\.Allocate<.*Allocator\\.Persistent new Native(Array List HashMap ParallelHashMap).*Allocator\\.Persistent new NativeQueue.*Allocator\\.Persistent new NativeParallelMultiHashMap.*Allocator\\.Persistent Allocator\\.Persistent Pack\\s*=\\s*1" Assets/_Project/Scripts/AI Assets/_Project/Scripts/Physics Assets/_Project/Scripts/Ecosystem`
Result: no matches. This covers migrated `MigrationDirector` Vault handles, buoyancy force-packet Vault window, `PathFunnelContracts` padding cleanup, and Ambient Biota `Pack=1` removal.
New Vault-backed ecosystem buffers:
`BufferID.ShinobuMigrationGridFront = 70653`, `MigrationGridCell`, capacity `MigrationGridCellCount`.
`BufferID.ShinobuMigrationGridBack = 70654`, `MigrationGridCell`, capacity `MigrationGridCellCount`.
`BufferID.ShinobuMigrationBloodCloudPois = 70655`, `MigrationBloodCloudPoi`, capacity `BloodCloudPoiCapacity`.
`BufferID.ShinobuMigrationSwarmStates = 70656`, `MigrationSwarmState`, capacity `MigrationSwarmCapacity`.
New buoyancy transfer route:
`(BufferID)71621`, `BuoyancyForcePacketDTO`, capacity `8192`, producer `EvaluateBuoyancyJob`, consumer `PhysicsApplySystem.DrainBuoyancyForcePackets`.
`BufferID.ShinobuBuoyancyCounters = 71630`, `BuoyancyCounterDTO`, capacity `1`, 64B row used for force-packet atomic count and telemetry counters.
Build:
`dotnet build .\Assembly-CSharp.csproj --no-restore -nologo` was run after CPU gate opened (`20.7%`, no `dotnet/csc`). It failed on pre-existing external Hecton8.Core missing DTO/namespace dependencies; captured output showed no errors in SHINOBU_100-touched files.
Loop 8 ABI / Burst Post-Edit Scan
`rg -n "H8Memory\\.Allocate< new Native(Array List HashMap ParallelHashMap ParallelMultiHashMap Queue Reference Stream)\\s*< Allocator\\.Persistent" Assets/_Project/Scripts/AI Assets/_Project/Scripts/Physics Assets/_Project/Scripts/Ecosystem -g "*.cs"`
Result: no matches.
`rg -n "\\[StructLayout\\(LayoutKind\\.Sequential Pack\\s*=" Assets/_Project/Scripts/AI Assets/_Project/Scripts/Physics Assets/_Project/Scripts/Ecosystem -g "*.cs"`
Result after Loop 8: no matches.
`rg -n "\\[BurstCompile\\((?![^\\r\\n]*CompileSynchronously\\s*=\\s*true) FloatPrecision\\s*=\\s*FloatPrecision\\.Low" Assets/_Project/Scripts/AI Assets/_Project/Scripts/Physics Assets/_Project/Scripts/Ecosystem -g "*.cs" --pcre2`
Result after Loop 8: no matches.
Targeted files changed for ABI/Burst closure:
`Assets/_Project/Scripts/Physics/VerletCableDTOs.cs`
`Assets/_Project/Scripts/Physics/TetherVerletJobs.cs`
`Assets/_Project/Scripts/Physics/Exosuit/ExosuitKinematicsContracts.cs`
`Assets/_Project/Scripts/Physics/KCC/HydrodynamicKccRuntime.cs`
`Assets/_Project/Scripts/Physics/FluidMathCore.cs`
`Assets/_Project/Scripts/Ecosystem/FaunaGenome64.cs`
`Assets/_Project/Scripts/AI/Pathfinding/FunnelSmoothingJob.cs`
`Assets/_Project/Scripts/AI/Ecology/Migration/MacroSwarm.cs`
`Assets/_Project/Scripts/AI/Ecosystem/EcosystemPopulationBalancer.cs`
Manual dispose scan:
Remaining `.Dispose()` hits are service runtime teardown or GraphicsBuffer release. No NativeArray/List/HashMap/Queue disposal ownership remains in target scan.
Build gate:
Not run. CPU measured `71.9%`; AGENTS/user rules forbid `dotnet build` above `50%`.
Loop 9 Deterministic Frame Post-Edit Scan
`rg -n "Time\\.frameCount Time\\.deltaTime Time\\.fixedDeltaTime UnityEngine\\.Random Random\\.Range" Assets/_Project/Scripts/AI Assets/_Project/Scripts/Physics Assets/_Project/Scripts/Ecosystem -g "*.cs"`
Result after Loop 9: no matches.
`rg -n "H8Memory\\.Allocate< new Native(Array List HashMap ParallelHashMap ParallelMultiHashMap Queue Reference Stream)\\s*< Allocator\\.Persistent" Assets/_Project/Scripts/AI Assets/_Project/Scripts/Physics Assets/_Project/Scripts/Ecosystem -g "*.cs"`
Result after Loop 9: no matches.
`rg -n "\\[StructLayout\\(LayoutKind\\.Sequential Pack\\s*=" Assets/_Project/Scripts/AI Assets/_Project/Scripts/Physics Assets/_Project/Scripts/Ecosystem -g "*.cs"`
Result after Loop 9: no matches.
`rg -n "\\[BurstCompile\\((?![^\\r\\n]*CompileSynchronously\\s*=\\s*true) FloatPrecision\\s*=\\s*FloatPrecision\\.Low" Assets/_Project/Scripts/AI Assets/_Project/Scripts/Physics Assets/_Project/Scripts/Ecosystem -g "*.cs" --pcre2`
Result after Loop 9: no matches.
Frame source replacements:
`SignalBus .SnapshotGeneration`: deterministic signal snapshot identity.
`PathFunnelRuntimeState.FrameCounter`: Vault-resident 64B runtime-state frame source at offset 48.
`EcosystemPopulationBalancer._simulationFrameCounter`: cold-tick simulation frame source.
`GlobalPhysicsStateManager._physicsCullingSimulationFrame`: physics culling simulation frame source.
Build gate:
Not run. CPU measured `100%`; AGENTS/user rules forbid `dotnet build` above `50%`.
Loop 10 Job Fence Post-Edit Scan
`rg -n "\.Complete\(\) JobHandle\.CompleteAll \.Run\(" Assets/_Project/Scripts/AI Assets/_Project/Scripts/Physics Assets/_Project/Scripts/Ecosystem -g "*.cs"`
Result after Loop 10: no matches.
`rg -n "ScheduleBatchedJobs" Assets/_Project/Scripts/AI Assets/_Project/Scripts/Physics Assets/_Project/Scripts/Ecosystem -g "*.cs"`
Result after Loop 10:
`Assets/_Project/Scripts/Physics/GlobalPhysicsStateManager.Shinobu37PhysicsCulling.cs:1320`
`Assets/_Project/Scripts/Physics/Exosuit/ExosuitKinematicsRuntime.cs:487`
Both remaining calls are non-blocking worker-dispatch flushes. No target-tree local completion call remains attached to them.
Fence replacements:
SHINOBU_37 mock seismic wake is now pending signal scheduled into existing physics culling handle chain.
Path funnel post-simulation readback now finalizes through `DispatcherJobSwap.TryFinalizeCompleted`.
Habitat fluid incursion, Ambient Biota, Macro Ecosystem, Acoustic Echo, Exosuit, vehicle damage, cavitation, submarine SDF navigation, cable, and tether helper paths now reclaim handles through `DispatcherJobSwap` forced/non-blocking semantics.
Cold `.Run()` helper sites in Ambient Biota, cable, and tether routes were converted to scheduled handles plus explicit dispatcher-forced reclamation.
Regression scans after Loop 10:
`rg -n "Time\.frameCount Time\.deltaTime Time\.fixedDeltaTime UnityEngine\.Random Random\.Range" Assets/_Project/Scripts/AI Assets/_Project/Scripts/Physics Assets/_Project/Scripts/Ecosystem -g "*.cs"`
Result: no matches.
`rg -n "H8Memory\.Allocate< new Native(Array List HashMap ParallelHashMap ParallelMultiHashMap Queue Reference Stream)\s*< Allocator\.Persistent" Assets/_Project/Scripts/AI Assets/_Project/Scripts/Physics Assets/_Project/Scripts/Ecosystem -g "*.cs"`
Result: no matches.
`rg -n "\[StructLayout\(LayoutKind\.Sequential Pack\s*=" Assets/_Project/Scripts/AI Assets/_Project/Scripts/Physics Assets/_Project/Scripts/Ecosystem -g "*.cs"`
Result: no matches.
`rg -n "\[BurstCompile\((?![^\r\n]*CompileSynchronously\s*=\s*true) FloatPrecision\s*=\s*FloatPrecision\.Low" Assets/_Project/Scripts/AI Assets/_Project/Scripts/Physics Assets/_Project/Scripts/Ecosystem -g "*.cs" --pcre2`
Result: no matches.
Build gate:
Not run. CPU measured `100%` with no `dotnet`, `csc`, or `VBCSCompiler` process; AGENTS/user rules forbid `dotnet build` above `50%`.
Loop 11 Compile-Wall Facade Scan
`rg -n "DispatcherJobSwap using Hecton8\\.World;" Assets/_Project/Scripts/AI/Ambient Assets/_Project/Scripts/AI/Pathfinding -g "*.cs"`
Result after Loop 11:
`Assets/_Project/Scripts/AI/Pathfinding/FunnelSmoothingJob.cs:3:using Hecton8.World;`
Interpretation: AI asmdef job-fence routing no longer imports `Hecton8.World`. remaining Pathfinding import is for `AbsoluteUniversePositionBlit`, AUP payload DTO compiled in root Core assembly, not for scheduler/fence ownership.
New Core facade:
`Assets/_Project/Scripts/Core/DispatcherJobFence.cs`
`Assets/_Project/Scripts/Core/DispatcherJobFence.cs.meta`
Purpose: expose `TryComplete` and `TryFinalizeCompleted` under `Hecton8.Core` so AI assemblies do not depend on World namespace shape for dispatcher-owned fence reclamation.
Build attempt after Loop 11:
Gate before build: CPU `40%`, zero `dotnet`, `csc`, or `VBCSCompiler` processes.
Command: `dotnet build .\Assembly-CSharp.csproj --no-restore -nologo`.
Result: failed with `CSC : error CS2001: Source file 'C:\hades\Hecton8\Assets\_Project\Scripts\Construction\LogisticsPipeEvents.cs' could not be found. [C:\hades\Hecton8\Hecton8.Core.csproj]`.
Ownership note: `git status --short` reports `D Assets/_Project/Scripts/Construction/LogisticsPipeEvents.cs`. SHINOBU_100 did not restore or stub this external Construction file.
Loop 12 Runtime Routing And Allocation Scan
Fence route scan:
`rg -n "DispatcherJobSwap Hecton8\.World\.DispatcherJobSwap \.Complete\(\) JobHandle\.CompleteAll \.Run\(" Assets/_Project/Scripts/AI Assets/_Project/Scripts/Physics Assets/_Project/Scripts/Ecosystem -g "*.cs"`
Result after Loop 12: no matches.
Hot DataVault lookup scan:
`rg -n "GlobalRegistry\.DataVault" Assets/_Project/Scripts/AI Assets/_Project/Scripts/Physics Assets/_Project/Scripts/Ecosystem -g "*.cs" -g "!**/Editor/**"`
Result after Loop 12: no matches.
Native ownership and ABI scan:
`rg -n "new Native(Array List HashMap ParallelHashMap ParallelMultiHashMap Queue Reference Stream)\s*< Allocator\.Persistent H8Memory\.Allocate< StructLayout\(LayoutKind\.Sequential Pack\s*=" Assets/_Project/Scripts/AI Assets/_Project/Scripts/Physics Assets/_Project/Scripts/Ecosystem -g "*.cs"`
Result after Loop 12: no matches.
Burst and deterministic-source scan:
`rg -n "\[BurstCompile\((?![^\r\n]*CompileSynchronously\s*=\s*true) FloatPrecision\s*=\s*FloatPrecision\.Low Time\.frameCount Time\.deltaTime Time\.fixedDeltaTime UnityEngine\.Random Random\.Range" Assets/_Project/Scripts/AI Assets/_Project/Scripts/Physics Assets/_Project/Scripts/Ecosystem -g "*.cs" --pcre2`
Result after Loop 12: no matches.
Diff hygiene:
`git diff --check` on touched runtime files reports LF/CRLF normalization warnings only.
Build gate:
Build not launched. CPU sampled `85.051%`; active compiler/runtime processes: seven `dotnet` processes. External compile blocker remains `D Assets/_Project/Scripts/Construction/LogisticsPipeEvents.cs`.
Loop 13 Managed Fault Dump Allocation Scan
Managed byte dump scan:
`rg -n "File\.WriteAllBytes new byte\[" Assets/_Project/Scripts/AI Assets/_Project/Scripts/Physics Assets/_Project/Scripts/Ecosystem -g "*.cs" -g "!**/Editor/**"`
Result after Loop 13: no matches.
Private managed array classification scan:
`rg -n "private\s+(static\s+readonly\s+ readonly\s+ static\s+)?[ -Za-z0-9_<>,\.]+\s*\[\]\s+[_a-zA-Z0-9]+ new\s+[ -Za-z0-9_<>,\.]+\s*\[" Assets/_Project/Scripts/AI Assets/_Project/Scripts/Physics Assets/_Project/Scripts/Ecosystem -g "*.cs" -g "!**/Editor/**"`
Remaining classified residues:
`EcosystemHealthDirector._exploredChunkBuffer`: cold save/PDA copy buffer, managed ecosystem-health owner, not native authoritative row.
`MigrationDirector._bloodCloudPoiMirror` and `_pendingBloodCloudPoiWrites`: documented 8-row cold mirrors while Vault `NativeArray` is job-owned.
`GlobalPhysicsStateManager._physicsFrustumPlaneScratch`: Unity `GeometryUtility.CalculateFrustumPlanes(Camera, Plane[])` API scratch, not rollback/native authority.
Post-patch hard gates:
No non-editor `GlobalRegistry.DataVault`.
No `DispatcherJobSwap`, `.Complete()`, `.Run()`.
No persistent native allocation constructor or `Allocator.Persistent`.
No `Pack=`, no `LayoutKind.Sequential`.
No missing synchronous Burst attribute, no `FloatPrecision.Low`.
No Unity Time/Random contamination.
Loop 14 Cold Scratch And Compile-Wall Classification
Patch:
`GlobalPhysicsStateManager._physicsFrustumPlaneScratch` now carries canonical cold-allocation annotation:
`COLD ALLOC: Plane[6] - Unity frustum API scratch for GeometryUtility.CalculateFrustumPlanes(Camera, Plane[]) - owner: GlobalPhysicsStateManager`.
Private managed array classification after patch:
`CreatureGeneticsProfile.speciesTunings`: serialized ScriptableObject authoring data via `Array.Empty ()`; not runtime native authority.
`EcosystemMigrationProfile.temperatureRoutes`: serialized ScriptableObject authoring data; not runtime native authority.
`MigrationDirector.scavengerMigrationSpeciesIds`: serialized designer species filter; not runtime native authority.
`EcosystemHealthDirector._exploredChunkBuffer`: documented cold save/PDA copy buffer.
`MigrationDirector._bloodCloudPoiMirror` and `_pendingBloodCloudPoiWrites`: documented cold 8-row mirrors while Vault `NativeArray` rows are job-owned.
`AmbientBiotaDirector` fallback quad `Vector3[4]`, `Vector2[4]`, `int[6]`: documented cold fallback mesh arrays.
`GlobalPhysicsStateManager._physicsFrustumPlaneScratch`: documented Unity frustum API scratch.
Hard gates after patch:
`rg -n "GlobalRegistry\.DataVault DispatcherJobSwap Hecton8\.World\.DispatcherJobSwap \.Complete\(\) JobHandle\.CompleteAll \.Run\(" ...` returned no matches.
`rg -n "Allocator\.Persistent new Native(Array List HashMap ParallelHashMap ParallelMultiHashMap Queue Reference Stream)\s*< H8Memory\.Allocate< StructLayout\(LayoutKind\.Sequential Pack\s*=" ...` returned no matches.
`rg -n "\[BurstCompile\((?![^\r\n]*CompileSynchronously\s*=\s*true) FloatPrecision\s*=\s*FloatPrecision\.Low Time\.frameCount Time\.deltaTime Time\.fixedDeltaTime UnityEngine\.Random Random\.Range" ... --pcre2` returned no matches.
World namespace assembly classification:
Every target-tree file with `using Hecton8.World;` or `Hecton8.World.` was mapped to its nearest `.asmdef`.
Root AI/Physics/Ecosystem residues map to `Hecton8.Core`, not sibling World runtime asmdef.
`Assets/_Project/Scripts/AI/Pathfinding/FunnelSmoothingJob.cs` maps to `Hecton8.AI.Pathfinding`; its asmdef references `Hecton8.Core`, `Hecton8.Core.Contracts`, and `Hecton8.Core.Memory`, not `Hecton8.World.*`. import is for `AbsoluteUniversePositionBlit`, Core-compiled AUP payload namespace residue.
Build gate:
Build not launched. CPU sampled `73.2%`, above AGENTS/user limit. External deleted source remains `D Assets/_Project/Scripts/Construction/LogisticsPipeEvents.cs`.
===== END FILE: AllocationAudit_SHINOBU_100.md =====
