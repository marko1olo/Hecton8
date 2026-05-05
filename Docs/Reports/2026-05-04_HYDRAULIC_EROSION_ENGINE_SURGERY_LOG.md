# Hydraulic Erosion Engine Surgery Log

Date: `2026-05-04`
Status: `PENDING VERIFICATION`
Scope: standalone Burst hydraulic erosion, thermal slumping, MapMagic generator node, editor PNG harness.

## Mandates Followed

- `.agents-skills/OPT_Native_Memory_Collections_JobSystem_Protocol.txt`
- `.agents-skills/OPT_Zero_GC_Policy_AllocFree_Mandate.txt`
- `.agents-skills/VOX_MapMagic_Voxel_Seam_Alignment_Integration.txt`
- `.agents-skills/STRM_World_Streaming_Residency_Chunk_Management.txt`
- `.agents-skills/GPU_Compute_Kernels_Kernels_Optimization_MX350.txt`
- `.agents-skills/OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt`
- `.agents-skills/DBG_Telemetry_Crash_Reporting_PostMortem.txt`
- `.agents-skills/MATH_Coordinate_Precision_AUP_FloatingOrigin.txt`

## Files Added

- `Assets/_Project/Scripts/World/HydraulicErosionJob.cs`
- `Assets/_Project/Scripts/World/ThermalSlumpingJob.cs`
- `Assets/_Project/Scripts/World/ErosionHarnessJobs.cs`
- `Assets/_Project/Scripts/World/HydraulicErosionMetricsJob.cs`
- `Assets/_Project/Scripts/World/HectonHydraulicErosionMapMagicNode.cs`
- `Assets/_Project/Scripts/Editor/ErosionTestHarness.cs`
- `Assets/_Project/Scripts/Editor/HydraulicErosionSmokeTester.cs`

## Implementation Facts

- `HydraulicErosionJob` is a Burst `IJobParallelFor` over caller-owned `NativeArray<float>` heightmap, sediment, and erosion-depth buffers.
- Droplets use deterministic hash RNG, high inertia, sediment capacity, erosion, deposition, water evaporation, and final evaporation deposition.
- Dendritic channel enforcement is implemented by choosing the best spawn cell from multiple deterministic candidates scored by local depression plus existing erosion-depth intensity, then adding directional pull toward the erosion-depth gradient during flow integration.
- Sedimentary deposition dumps the full carried payload when a droplet enters a slope band at or below 2 degrees, then a dedicated Burst smoothing pass flattens sediment-classified cells.
- Canyon-wall steepening is a dedicated Burst post-pass that raises immediate non-channel banks next to deep erosion-depth cells.
- `ThermalSlumpingJob` is a Burst `IJobParallelFor` that transfers material down slopes exceeding the critical talus angle.
- `HectonHydraulicErosionMapMagicNode` exposes a MapMagic height inlet and three outlets: eroded height, sediment mask, erosion-depth mask.
- The MapMagic node reserves a configurable 4-pixel default margin by spawning droplets in the core while allowing flow into the overlapped boundary.
- Sediment and erosion-depth outputs are normalized to strict `0.0..1.0` before MapMagic product storage.
- `HectonHydraulicErosionMapMagicNode` registers and unregisters every `Allocator.TempJob` native buffer with `NativeMemorySentinel`.
- `HectonHydraulicErosionMapMagicNode` publishes `GlobalTelemetryBus.PublishPerformanceWarning` markers for large droplet budgets, large cell budgets, and blocking barrier stalls above 25 ms.
- `ErosionFractalHeightmapJob` moves editor-harness fractal terrain generation into a Burst `IJobParallelFor`.
- `ErosionSmokeMetricsJob` writes a blittable metrics record covering height ranges, sediment/wear maxima, mean absolute delta, changed cells, and non-finite cells.
- `ErosionTestHarness` generates a 512x512 fractal heightmap, runs erosion plus slumping, and writes PNG plus JSON metrics outputs under `CodexArtifacts/`.

## Droplet Capacity Code

```csharp
[MethodImpl(MethodImplOptions.AggressiveInlining)]
public static float CalculateSedimentCapacity(
    float heightDelta,
    float speed,
    float water,
    float capacityFactor,
    float minCapacity)
{
    float downhillSlope = math.max(-heightDelta, 0.001f);
    float velocityTerm = math.max(speed, 0.01f);
    float waterTerm = math.max(water, 0.01f);
    float rawCapacity = downhillSlope * velocityTerm * waterTerm * math.max(0f, capacityFactor);
    return math.max(rawCapacity, math.max(0f, minCapacity));
}
```

## Regression Model

- CPU: MapMagic generation can be expensive at high droplet counts. The node reduces droplets during draft generation. This is generation-time work, not gameplay Tick.
- GC: hot job logic is unmanaged/Burst. MapMagic generation and editor harness allocate managed matrices/textures outside gameplay hot paths. Profiler proof absent.
- Memory: all new native containers in the node and harness use `Allocator.TempJob`, register with `NativeMemorySentinel`, complete before disposal, unregister before disposal, and do not persist across frames.
- Cadence: the only `.Complete()` calls are annotated blocking generation/editor harness sync points. No gameplay Tick owner was added.
- Correctness: boundary bleed is implemented as a 4-pixel overlapped processing margin. True cross-tile validation requires MapMagic graph execution on adjacent chunks.

## Verification

- Passed: MCP `validate_script` standard diagnostics on `HydraulicErosionJob.cs`, `ThermalSlumpingJob.cs`, `HectonHydraulicErosionMapMagicNode.cs`, and `ErosionTestHarness.cs` returned zero errors and zero warnings.
- Blocked: Unity script refresh/import completed, but project compilation is blocked by unrelated current errors in `PlayerCriticalProceduralAudioRenderer.cs`, `HectonSurvivalSystem.cs`, `StorageCrate.cs`, and `PredatorCognitionDomain.cs`.
- Blocked: `dotnet build .\Hecton8.Core.csproj --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -v:minimal` fails on unrelated stale/project errors before a clean assembly build can be claimed.
- Blocked: 2026-05-05 Unity MCP validation unavailable because no Unity Editor instance is connected (`instance_count: 0`).
- Blocked: 2026-05-05 local `dotnet build` exits `1`; diagnostic scan reports `Assets/_Project/Scripts/BaseModule.cs(349,34): error CS0246: BaseModuleCondensationSurface could not be found`.
- Passed: scoped source scan on erosion files found no `_instance` or `DontDestroyOnLoad`; `Run()` matches are editor harness/menu entry points only.
- Pending: MapMagic node graph execution.
- Pending: editor harness PNG/JSON generation; cannot execute until Unity Editor connection and project compilation are restored.
- Pending: GCMonitor/profiler capture.

## 2026-05-05 Omega-Autonomy Continuation

Status: `PENDING VERIFICATION`

### Additional Mandates Followed

- `.agents-skills/OPT_Native_Memory_Collections_JobSystem_Protocol.txt`
- `.agents-skills/OPT_Zero_GC_Policy_AllocFree_Mandate.txt`
- `.agents-skills/OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt`
- `.agents-skills/VOX_MapMagic_Voxel_Seam_Alignment_Integration.txt`
- `.agents-skills/STRM_World_Streaming_Residency_Chunk_Management.txt`
- `.agents-skills/DBG_Telemetry_Crash_Reporting_PostMortem.txt`

### Surgery Log

- Registered MapMagic node TempJob buffers `heightA`, `heightB`, `sediment`, and `wear` with `NativeMemorySentinel` using `NativeAllocationLifetime.TempJob`.
- Registered editor harness TempJob buffers `before`, `heightA`, `heightB`, `sediment`, `wear`, and `metrics` with `NativeMemorySentinel`.
- Added `ErosionHarnessJobs` with Burst fractal heightmap generation and harness metrics jobs.
- Added `HydraulicErosionMetricsJob`, a Burst `IJobParallelFor` that scans height/sediment/wear buffers in blocks for smoke-test metrics.
- Added `HydraulicErosionSmokeTester`, an editor smoke runner with four scenarios: `dry_zero_power`, `tiny_margin_clamp`, `draft_tile`, and `thermal_stress`.
- Replaced the smoke tester scalar heightmap seed loop with `ErosionFractalHeightmapJob` so the smoke terrain generation is also Burst `IJobParallelFor` work.
- Added cold-path `GlobalTelemetryBus.PublishPerformanceWarning` calls for MapMagic droplet/cell budget violations and smoke-test failure reporting.
- Converted TempJob disposal in the MapMagic node and editor harness to unregister-before-dispose helper paths.
- Patched the editor-only `AnomalyTestHarness` assert overload ambiguity by replacing integer `Assert.AreEqual` calls with explicit boolean equality checks. This was a compile blocker for the Unity batchmode smoke pass, not part of the erosion runtime.

### Forensic Audit Data

- NativeCollections: scoped scan found all new caller-owned TempJob arrays are registered and unregistered in their owner files. Job structs still expose caller-owned `NativeArray<T>` fields; they do not allocate.
- Barriers: scoped scan found `.Complete()` at `HectonHydraulicErosionMapMagicNode.cs:273`, `ErosionTestHarness.cs:126`, and `HydraulicErosionSmokeTester.cs:216`. All are generation/editor sync points, not gameplay `Tick`, `FixedTick`, or `Update`.
- Static leaks: scoped scan found no `_instance` or `DontDestroyOnLoad` usage in the hydraulic erosion domain files.
- String churn: scoped fixed-string scan found no `$"` interpolated strings. `.ToString()` remains only in editor cold JSON/file output, not in gameplay `Tick`, `FixedTick`, or `Update`.

### Verification Delta

- Passed: scoped source audit for `NativeArray`, `NativeMemorySentinel`, `.Complete()`, `Run()`, hot-loop names, `.ToString()`, `string.Format`, `_instance`, `DontDestroyOnLoad`, `PublishPerformanceWarning`, and `Debug.Log` completed with findings limited to cold/editor paths.
- Passed: scoped fixed-string `rg -F '$"'` audit returned no interpolated-string matches.
- Blocked: MCP `validate_script` retry returned `no_unity_session`; Unity Editor diagnostics were unavailable in this session.
- Blocked: `dotnet build .\Hecton8.Core.csproj --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -v:minimal` exits `1` before a clean domain compile can be claimed. Current blockers: `Temp/obj/Crest/project.assets.json` missing (`NETSDK1004`) and dependent `Unity.RenderPipelines.Universal.Runtime.dll` metadata missing (`CS0006`).
- Blocked: Unity batchmode harness attempt wrote `CodexArtifacts/unity-erosion-harness-2026-05-05.log`, but compilation stopped before `Hecton8.Editor.ErosionTestHarness.Run` executed. Blocking errors in that log are `Assets/_Project/Scripts/Editor/AnomalyTestHarness.cs(90,24)` and `(96,24)` ambiguous `Assert.AreEqual`, plus `Assets/_Project/Scripts/HectonSurvivalSystem.cs(1132,60)` and `(1133,61)` missing `HectonPlayerMovement.CapsuleHeight`.
- Not found: batchmode log contains no compiler errors naming `ErosionHarnessJobs.cs`, `HydraulicErosionMetricsJob.cs`, `HectonHydraulicErosionMapMagicNode.cs`, `HydraulicErosionJob.cs`, or `HydraulicErosionSmokeTester.cs`.
- Blocked: repeated 2026-05-05 hydraulic smoke reruns could not complete because unrelated Unity batchmode smoke runners repeatedly acquired the same project lock. Observed competing commands included `OmegaAutonomySmokeTestRunner.Run`, `OmegaSurvivalKinematicsSmokeTester.RunBatchModeSmokeTest`, `VisualOmegaSmokeTester.RunBatchModeSmokeTest`, `SavePersistenceOmegaSmokeTester.RunBatchModeSmokeTest`, `FaunaRuntimeSmokeTesterRunner.RunOmegaHeadlessSmoke`, `AnomalySmokeTester.Run`, `AutomationSmokeTestRunner.RunBatchOmegaAutomationSmokePass`, and `DocumentationAuthoritySmokeTester.RunMenuItem`.
- Blocked: controlled hydraulic run wrote `CodexArtifacts/unity-hydraulic-smoke-2026-05-05-exclusive.log` but stopped during Unity startup/domain reload before script compilation or `HydraulicErosionSmokeTester.RunMenu` execution. `CodexArtifacts/HydraulicErosionSmokeTester.json` was not created.
- Artifact: `CodexArtifacts/2026-05-05_EROSION_OMEGA_DIFF.patch` is the scoped diff artifact for the current erosion continuation.
- Historical pass: `HydraulicErosionSmokeTester` executed in Unity batchmode and wrote `CodexArtifacts/HydraulicErosionSmokeTester.json` before the boundary metrics were added. The current boundary metrics require a new JSON artifact.

### 2026-05-05 Smoke Rerun Attempt

- Attempted: `Unity.exe -batchmode -nographics -quit -projectPath C:\hades\Hecton8 -executeMethod Hecton8.Editor.HydraulicErosionSmokeTester.RunMenu -logFile C:\hades\Hecton8\CodexArtifacts\unity-hydraulic-smoke-2026-05-05-rerun2.log`.
- Result: `CodexArtifacts/unity-hydraulic-smoke-2026-05-05-rerun2.log` contains startup and `Application will terminate with return code 1`; it contains no compiler diagnostics and no `HydraulicErosionSmokeTester` JSON result.
- Attempted: same command with `-logFile -` to force stdout diagnostics. Shell returned exit code `0`, but no `CodexArtifacts/HydraulicErosionSmokeTester.json` artifact was produced.
- Attempted: controlled `Start-Process -Wait` rerun writing `CodexArtifacts/unity-hydraulic-smoke-2026-05-05-rerun3.log`. Process returned `ExitCode=1`; log again contains startup and `Application will terminate with return code 1`, with no compiler diagnostics and no smoke JSON.
- Current source inspection after the older `unity-omega-smoke-2026-05-05-rerun.log` compile errors: `WorldProceduralFieldSampler.cs` now imports `Hecton.Localization`, `PDAMapTab.cs` now imports `Hecton8.Environment`, and `BaseLogisticsNetwork.cs` now exposes `ExecuteLogisticsRouteBfs`. These remove the specific `LocHash`, `BiomeMatrixDirector`, and `ExecuteLogisticsRouteBfs` source mismatches seen in that older log, but no successful Unity recompile was captured in this report.
- Blocked: active external Unity/dotnet/ILPP build processes were observed during the rerun window. The hydraulic smoke result remains unverified.

### 2026-05-05 Previous Hydraulic Smoke Result

- Passed: `Unity.exe -batchmode -nographics -quit -projectPath C:\hades\Hecton8 -executeMethod Hecton8.Editor.HydraulicErosionSmokeTester.RunMenu -logFile C:\hades\Hecton8\CodexArtifacts\unity-hydraulic-smoke-2026-05-05-final3.log` completed with `Application will terminate with return code 0`.
- Passed: `CodexArtifacts/HydraulicErosionSmokeTester.json` reports `scenarioCount=4`, `passCount=4`.
- Passed: all four scenarios reported `nanCount=0`, `sentinelDelta=0`, and `trackedByteDelta=0`.
- Scenario data: `dry_zero_power` 8x8, 0 droplets, 23.4236 ms, sediment/wear 0/0.
- Scenario data: `tiny_margin_clamp` 16x16, 128 droplets, 31.0394 ms, max sediment/wear 0.922665/0.982978.
- Scenario data: `draft_tile` 64x64, 4096 droplets, 1872.356 ms, max sediment/wear 0.621135/0.844835.
- Scenario data: `thermal_stress` 96x96, 2048 droplets, 934.9957 ms, max sediment/wear 0.384809/0.637367.
- Constraint: this successful JSON predates the current boundary-band metrics added to `HydraulicErosionMetricsJob` and `HydraulicErosionSmokeTester`. It does not contain `boundarySampleCount`, `boundaryNanCount`, `maxBoundaryHeightDelta`, `maxBoundarySediment`, or `maxBoundaryWear`, so it is historical evidence, not current working-tree verification.

### 2026-05-05 Boundary Metrics Rerun Attempts

- Attempted: `CodexArtifacts/unity-hydraulic-smoke-2026-05-05-boundary.log` reached script compilation but failed before `HydraulicErosionSmokeTester` execution on `CrashTelemetryBuffer.cs(614,13)`, `(623,13)`, and `(631,13)` missing `TryRegisterRuntimeService` / `TryUnregisterRuntimeService`. Current source timestamp is later and now contains those methods, so this log is stale blocker evidence only.
- Attempted: `CodexArtifacts/unity-hydraulic-smoke-2026-05-05-boundary2.log` stopped after package registration. No `Application will terminate` line and no `CodexArtifacts/HydraulicErosionSmokeTester.json` were produced.
- Attempted: `CodexArtifacts/unity-hydraulic-smoke-2026-05-05-boundary-exclusive.log` started `HydraulicErosionSmokeTester.RunMenu`, but Bee reported `Modification date of Assets\_Project\Scripts\Dev\CelestialCataclysmSmokeTester.cs changed while running Csc` and `Tundra requires additional run`. No current boundary JSON was produced.
- Attempted: `CodexArtifacts/unity-hydraulic-smoke-2026-05-05-boundary-final.log` started `HydraulicErosionSmokeTester.RunMenu`, reached `ScriptCompilation Requested`, then exited without `Application will terminate`, without `Wrote JSON`, and without `CodexArtifacts/HydraulicErosionSmokeTester.json`.
- Attempted: local monitored `unity-hydraulic-smoke-2026-05-05-codex-current.log` run was aborted before launch because another Unity process acquired the project lock between the no-process check and `Start-Process`. The competing command was another `HydraulicErosionSmokeTester.RunMenu` writing `unity-hydraulic-smoke-2026-05-05-boundary-final.log`.
- Attempted: `CodexArtifacts/hydraulic-boundary-final2.log` started `HydraulicErosionSmokeTester.RunMenu` and reached Bee script compilation. Unity exited `-1`; Bee exited `2` with `More than one copy of bee_backend running in C:\hades\Hecton8 -- PID 34492 waiting`. No current boundary JSON was produced.
- Observed: new unrelated Unity batchmode launches appeared at `2026-05-05 10:05:06` during the verification window: `OmegaAutonomySmokeTestRunner.Run`, `VolumetricBiomeSmokeTestRunner.Run`, `FaunaRuntimeSmokeTesterRunner.RunOmegaHeadlessSmoke`, and `SavePersistenceOmegaSmokeTester.RunBatchModeSmokeTest`.
- Passed: `git diff --check -- Assets/_Project/Scripts/World/HydraulicErosionMetricsJob.cs Assets/_Project/Scripts/Editor/HydraulicErosionSmokeTester.cs Docs/Reports/2026-05-04_HYDRAULIC_EROSION_ENGINE_SURGERY_LOG.md` returned no whitespace errors; Git emitted LF-to-CRLF warnings only.
- Passed: scoped hot-path audit found one `.Complete()` at `HydraulicErosionSmokeTester.cs:224`, annotated as an editor cold sync. No `Run()`, `Update`, `LateUpdate`, `FixedUpdate`, `Tick`, `FixedTick`, `_instance`, `DontDestroyOnLoad`, or `$"` interpolated strings were found in the two boundary-metric source files.
- Attempted: `CodexArtifacts/unity-hydraulic-smoke-2026-05-05-current-final.log` exited before domain reload with `Application will terminate with return code 1`. The log contains startup arguments only and no compiler diagnostics; no current boundary JSON was produced.
- Attempted: `CodexArtifacts/unity-hydraulic-smoke-2026-05-05-current-exclusive.log` reached `HydraulicErosionSmokeTester.RunMenu`, package registration, script compilation, and Bee. Bee reported `ExitCode: -1 Duration: 1m:10s`, `Internal build system error. Read the full binlog without getting a BuildFinishedMessage.`, and `The backend process appears to still be running.` No current boundary JSON was produced.
- Observed: after the current exclusive attempt, unrelated batchmode launches again owned the project lock: `FaunaRuntimeSmokeTesterRunner.RunOmegaHeadlessSmoke` writing `fauna-omega-smoke-final-clean-20260505-165056.log` and `OmegaAutonomySmokeTestRunner.Run` writing `omega-autonomy-smoke-construction-integrated-2026-05-05-final.log`.
- Artifact: `CodexArtifacts/2026-05-05_EROSION_OMEGA_DIFF.patch` was refreshed after the boundary metrics/report edits and contains the current scoped Git diff for this domain.

### 2026-05-05 Current Boundary Smoke Result

- Passed: direct Unity batchmode run `Unity.exe -batchmode -nographics -quit -projectPath C:\hades\Hecton8 -executeMethod Hecton8.Editor.HydraulicErosionSmokeTester.RunMenu -logFile C:\hades\Hecton8\CodexArtifacts\unity-hydraulic-smoke-2026-05-05-current-direct-20260505-171339.log` completed with `Application will terminate with return code 0`.
- Passed: `CodexArtifacts/HydraulicErosionSmokeTester.json` was regenerated after deleting the stale artifact and now contains current boundary metrics fields.
- Passed: JSON reports `scenarioCount=4`, `passCount=4`, `status=PENDING VERIFICATION`, and tester `HydraulicErosionSmokeTester`.
- Passed: all four scenarios report `nanCount=0`, `boundaryNanCount=0`, `sentinelDelta=0`, and `trackedByteDelta=0`.
- Boundary data: `dry_zero_power` has `boundarySampleCount=28`, `maxBoundaryHeightDelta=0.604872`, `maxBoundarySediment=0`, `maxBoundaryWear=0`, `milliseconds=14.048`.
- Boundary data: `tiny_margin_clamp` has `boundarySampleCount=192`, `maxBoundaryHeightDelta=0.484481`, `maxBoundarySediment=0.317379`, `maxBoundaryWear=0.289431`, `milliseconds=30.6578`.
- Boundary data: `draft_tile` has `boundarySampleCount=960`, `maxBoundaryHeightDelta=0.224889`, `maxBoundarySediment=0.508141`, `maxBoundaryWear=0.41394`, `milliseconds=1565.129`.
- Boundary data: `thermal_stress` has `boundarySampleCount=1472`, `maxBoundaryHeightDelta=0.127595`, `maxBoundarySediment=0.211816`, `maxBoundaryWear=0.122168`, `milliseconds=633.3935`.
- Passed: post-run `git diff --check -- Assets/_Project/Scripts/World/HydraulicErosionMetricsJob.cs Assets/_Project/Scripts/Editor/HydraulicErosionSmokeTester.cs Docs/Reports/2026-05-04_HYDRAULIC_EROSION_ENGINE_SURGERY_LOG.md` returned no whitespace errors; Git emitted LF-to-CRLF warnings only.
- Passed: post-run scoped audit found no `$"` interpolated strings in `HydraulicErosionMetricsJob.cs`, `HydraulicErosionSmokeTester.cs`, `HectonHydraulicErosionMapMagicNode.cs`, or `ErosionTestHarness.cs`.
- Finding: post-run scoped audit found `.Complete()` only at `ErosionTestHarness.cs:126`, `HectonHydraulicErosionMapMagicNode.cs:273`, and `HydraulicErosionSmokeTester.cs:224`; all three remain editor/generation sync points outside gameplay `Tick`, `FixedTick`, `Update`, or `LateUpdate`.
- Finding: post-run scoped audit found native arrays in the MapMagic node, editor harness, and smoke tester paired with `NativeMemorySentinel.RegisterNativeArray` / `UnregisterNativeArray`; job structs expose caller-owned arrays only.
- Current verification state: boundary smoke source is executed and JSON-backed on the current working tree. Runtime MapMagic adjacent-chunk seam proof and profiler GC proof remain outside this editor smoke and are still pending.
- Status remains `PENDING VERIFICATION` under project authority rules; user-provided runtime/MapMagic graph logs are still required for in-game adjacency seam proof and profiler GC proof.

## 2026-05-05 Dendritic Veins And Sediment Flats Pass

Status: `PENDING VERIFICATION`

Requested status label: `EROSION VEINS VERIFIED` is not promoted to project verification status until current working-tree compile/smoke/profiler evidence exists.

### Mandates Followed

- `.agents-skills/OPT_Zero_GC_Policy_AllocFree_Mandate.txt`
- `.agents-skills/OPT_Native_Memory_Collections_JobSystem_Protocol.txt`
- `.agents-skills/OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt`
- `.agents-skills/GPU_Compute_Kernels_Kernels_Optimization_MX350.txt`
- `.agents-skills/VOX_MapMagic_Voxel_Seam_Alignment_Integration.txt`

### Surgery Log

- Converted `HydraulicErosionJob` from single-lane `IJob` to Burst `IJobParallelFor` over fixed-size sub-grids.
- Added `HydraulicErosionScheduler.ScheduleFourPhase`, which schedules `(phaseX, phaseZ)` parity phases `(0,0)`, `(1,0)`, `(0,1)`, and `(1,1)` with sequential dependencies. Same-phase sub-grids never share write boundaries; inactive parity sub-grids absorb boundary overlap.
- Replaced hydraulic `WearMask` with `ErosionDepthMask` in the droplet job. Thermal slumping no longer writes to this lane, so the exported mask remains hydraulic riverbed depth only.
- Raised default and enforced MapMagic runtime minima for dendritic formation: `Inertia >= 0.72`, `ChannelSpawnBias >= 12`, `ChannelFlowBias >= 1.5`, and `SpawnCandidateCount >= 12`.
- Added full-payload sediment dumping at `SedimentaryFlatSlopeDegrees = 2`, plus `SedimentaryFlatSmoothingJob` for sediment-classified flat plains.
- Added `CanyonWallSteepeningJob` to lift immediate banks around deeper erosion-depth cells, sharpening river canyon walls after erosion/slumping.
- Updated `ErosionTestHarness` and `HydraulicErosionSmokeTester` to run the four-phase erosion, flat smoothing, canyon wall pass, and `DispatcherJobSwap.TryComplete` cold sync.

### Dendritic Channel Bias Burst Logic

```csharp
float channel = ErosionDepthMask.IsCreated ? math.saturate(ErosionDepthMask[index] * 32f) : 0f;
float spawnScore = jitter +
                   depression * math.max(0f, DepressionSpawnBias) +
                   channel * math.max(0f, ChannelSpawnBias);

float2 channelGradient = SampleAccumulationGradient(ErosionDepthMask, position);
float2 hydraulicForce = -gradient + channelGradient * math.max(0f, ChannelFlowBias);
direction = direction * safeInertia +
            hydraulicForce * (1f - safeInertia) +
            channelGradient * math.max(0f, ChannelFlowBias) * 0.35f;
```

### Verification Delta

- Passed: Unity MCP `validate_script` standard diagnostics returned zero errors and zero warnings for `HydraulicErosionJob.cs`, `HectonHydraulicErosionMapMagicNode.cs`, `ErosionTestHarness.cs`, and `HydraulicErosionSmokeTester.cs`.
- Passed: Unity MCP executed `Hecton8.Editor.HydraulicErosionSmokeTester.RunAndWriteJson()` and wrote `CodexArtifacts/HydraulicErosionSmokeTester.json`.
- Passed: current JSON reports `scenarioCount=4`, `passCount=4`, and all scenarios `passed=true`, `nanCount=0`, `boundaryNanCount=0`, `sentinelDelta=0`, and `trackedByteDelta=0`.
- Scenario data: `dry_zero_power` 8x8, 0 droplets, `milliseconds=33.6702`, `sumSediment=0`, `sumWear=0`.
- Scenario data: `tiny_margin_clamp` 16x16, 128 droplets, `milliseconds=22.5875`, `maxSediment=0.817494`, `maxWear=0.920643`.
- Scenario data: `draft_tile` 64x64, 4096 droplets, `milliseconds=93.7701`, `maxSediment=0.691269`, `maxWear=0.968217`.
- Scenario data: `thermal_stress` 96x96, 2048 droplets, `milliseconds=84.2058`, `maxSediment=0.399059`, `maxWear=0.617553`.
- Attempted: `dotnet build .\Hecton8.Core.csproj -m:1 -nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -v:minimal -p:BaseIntermediateOutputPath=.codexbuild\erosion_obj\ -p:OutputPath=.codexbuild\erosion_bin\` timed out after 120 seconds without compiler diagnostics. Orphan MSBuild node processes left by the timeout were terminated by PID after parent process exit.
- Pending: MapMagic scene-03 terrain sandbox graph execution.
- Pending: profiler/GC proof for generation path.

## 2026-05-05 Omega-Autonomy V2 Crucible

Status: `PENDING VERIFICATION`

Requested status label: `OMEGA V2 VERIFIED` is scoped to the self-audit pass only. Project authority status remains pending without user-provided profiler and Scene 03 MapMagic graph logs.

### Garbage Found

- Defect: `HydraulicErosionJob` used `NativeDisableParallelForRestriction` on `Heightmap`, `SedimentMask`, and `ErosionDepthMask` with only a short shared comment. Mandate requires explicit safety proof for native safety suppression.
- Defect: `HectonHydraulicErosionMapMagicNode.Generate` could throw after scheduling erosion and before the blocking sync, then enter `finally` and dispose TempJob buffers while a live job handle still referenced them.
- Defect: `ErosionTestHarness.Run` had the same live-handle disposal risk after the fractal heightmap job was scheduled.
- Defect: `HydraulicErosionSmokeTester.RunScenario` had the same live-handle disposal risk after the fractal heightmap job was scheduled.
- Defect: `ErosionTestHarness.WriteMacroShelfPreviewArtifacts` marked `handleScheduled` only after the second scheduled job. If the second schedule failed, the first scheduled job could remain live while buffers disposed.

### Excision

- Added three-paragraph `SAFETY_JUSTIFICATION_PARAGRAPH_1/2/3` blocks immediately above each `NativeDisableParallelForRestriction` field.
- Moved job handle ownership into outer scope for MapMagic generation, editor harness, and smoke scenario.
- Added `handleScheduled` guards that call `DispatcherJobSwap.TryComplete(ref handle, forceComplete: true)` in `finally` before any `NativeMemorySentinel.UnregisterNativeArray` and `Dispose()`.
- Moved the macro shelf `handleScheduled = true` assignment immediately after the first scheduled job.

### Negative Findings

- No `NativeList` or `NativeQueue` allocation found in touched scope.
- All touched-scope `NativeArray` allocations are `Allocator.TempJob`, registered through `NativeMemorySentinel.RegisterNativeArray`, and disposed through unregister-before-dispose helpers in `finally`.
- No LINQ usage found in touched scope.
- No `foreach` loop found in touched scope.
- No `string.Format` found in touched scope.
- `.ToString()` remains only in editor cold JSON writer paths, not `Update`, `Tick`, `FixedUpdate`, `LateUpdate`, or `SlowTick`.
- No `transform.position`, `Vector3.Distance`, or `math.distance` usage found in touched scope.
- No `GlobalRegistry` calls, `Awake`, or `OnEnable` methods found in touched scope.

### Verification Delta

- Passed: Unity MCP `validate_script` standard diagnostics returned zero errors and zero warnings for `HydraulicErosionJob.cs`, `HectonHydraulicErosionMapMagicNode.cs`, `ErosionTestHarness.cs`, and `HydraulicErosionSmokeTester.cs` after the Crucible fixes.
- Passed: Unity MCP executed `Hecton8.Editor.HydraulicErosionSmokeTester.RunAndWriteJson()` after the Crucible fixes and wrote `CodexArtifacts/HydraulicErosionSmokeTester.json`.
- Passed: current JSON reports `scenarioCount=4`, `passCount=4`, and all scenarios `passed=true`, `nanCount=0`, `boundaryNanCount=0`, `sentinelDelta=0`, and `trackedByteDelta=0`.
- Scenario data: `dry_zero_power` 8x8, 0 droplets, `milliseconds=903.1918`, `sumSediment=0`, `sumWear=0`.
- Scenario data: `tiny_margin_clamp` 16x16, 128 droplets, `milliseconds=202.7419`, `maxSediment=0.75611`, `maxWear=0.775886`.
- Scenario data: `draft_tile` 64x64, 4096 droplets, `milliseconds=336.4043`, `maxSediment=0.698055`, `maxWear=0.893405`.
- Scenario data: `thermal_stress` 96x96, 2048 droplets, `milliseconds=174.3129`, `maxSediment=0.382892`, `maxWear=0.513876`.

## 2026-05-05 Omega-Autonomy V2 Repeat Crucible

Status: `PENDING VERIFICATION`

### Garbage Found

- No new NativeCollection leak defect was found after the prior Crucible fixes.
- No new hot-path LINQ, dictionary `foreach`, `string.Format`, AUP/Transform-distance, or GlobalRegistry init-cycle defect was found in touched scope.

### Forced Optimization

- Added Burst `NormalizeMaskInPlaceJob` to normalize sediment and erosion-depth masks on a worker thread before MapMagic matrix publication.
- Removed dead `CopyNormalizedMask` managed normalization loop from `HectonHydraulicErosionMapMagicNode`.
- Scheduled `NormalizeMaskInPlaceJob` for sediment and erosion-depth masks before the cold publish barrier. Managed copy to MapMagic matrices now performs only saturated copy; max scan and normalization are Burst worker work.

### Verification Delta

- Blocked: Unity MCP validation retried after this repeat pass and returned `no_unity_session`.
- Passed: external `dotnet build Hecton8.Core.csproj --no-restore -v:minimal /m:1 /nr:false` writing `CodexArtifacts/tactile-forge-build.log` exited successfully with `0 Warning(s)` and `0 Error(s)`.
- Passed: `dotnet build .\Hecton8.Editor.csproj --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -v:minimal` exited successfully with `0 Warning(s)` and `0 Error(s)`.
- Passed: scoped static scan after this repeat pass found no LINQ, no dictionary `foreach`, no `string.Format`, no `transform.position`, no `Vector3.Distance`, no `math.distance`, no `GlobalRegistry`, no `Awake`, no `OnEnable`, and no runtime update/tick methods in touched code. `.ToString()` remains editor JSON-only.

## 2026-05-05 Omega-Autonomy V2 Third Crucible

Status: `PENDING VERIFICATION`

### Garbage Found

- Defect: MapMagic hydraulic erosion scheduling had no profiler markers around the new erosion, flat smoothing, canyon, normalization, and publish-barrier stages. That left the MX350 metric gate unable to isolate which stage stalls.
- No new NativeCollection leak defect was found in touched scope.
- No hot-path LINQ, dictionary `foreach`, `string.Format`, AUP/Transform-distance, or GlobalRegistry init-cycle defect was found in touched scope.

### Excision

- Added `Unity.Profiling.ProfilerMarker` fields to `HectonHydraulicErosionMapMagicNode`.
- Wrapped schedule/publish sections with markers:
  `H8/World/HydraulicErosion.ScheduleFourPhase`,
  `H8/World/HydraulicErosion.SedimentaryFlatSchedule`,
  `H8/World/HydraulicErosion.ThermalSlumpSchedule`,
  `H8/World/HydraulicErosion.CanyonWallSchedule`,
  `H8/World/HydraulicErosion.MaskNormalizeSchedule`,
  and `H8/World/HydraulicErosion.PublishBarrier`.

### Verification Delta

- Passed: `git diff --check` on touched files returned no whitespace errors; Git emitted LF-to-CRLF warnings only.
- Blocked: Unity MCP validation unavailable; tool returned `no_unity_session`.
- Blocked: direct `dotnet build .\Hecton8.Core.csproj --no-restore --disable-build-servers -m:1 -nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -v:minimal` failed on unrelated current errors in `Assets/_Project/Scripts/VFX/CameraJuiceSystem.cs`: missing `_instance` at lines `300`, `306`, `442`, `481`, and `483`.
- Finding: no compiler diagnostics in the failed build named `HydraulicErosionJob.cs`, `HectonHydraulicErosionMapMagicNode.cs`, `ErosionTestHarness.cs`, or `HydraulicErosionSmokeTester.cs`.

## 2026-05-05 Omega-Autonomy V2 Current Crucible

Status: `PENDING VERIFICATION`

Project authority status is not promoted to `OMEGA V2 VERIFIED`; Scene 03 MapMagic graph runtime seam proof and profiler/GC proof are still not present in the repository. The current editor smoke and Unity console evidence below are valid for the hydraulic smoke domain only.

### Mandates Re-Read

- `.agents-skills/OPT_Native_Memory_Collections_JobSystem_Protocol.txt`
- `.agents-skills/OPT_Zero_GC_Policy_AllocFree_Mandate.txt`
- `.agents-skills/MATH_Coordinate_Precision_AUP_FloatingOrigin.txt`
- `.agents-skills/ARCH_Project_Bootstrap_Sequence_Init_Safety.txt`
- `.agents-skills/DBG_Telemetry_Crash_Reporting_PostMortem.txt`
- `.agents-skills/VOX_MapMagic_Voxel_Seam_Alignment_Integration.txt`

### Inquisition Findings

- No unregistered `NativeArray`, `NativeList`, or `NativeQueue` was found in the currently touched hydraulic smoke source.
- `HydraulicErosionSmokeTester.RunScenario` now owns seven TempJob NativeArrays: `before`, `heightA`, `heightB`, `sediment`, `wear`, `metricBlocks`, and `metricSummary`.
- All seven TempJob NativeArrays are registered through `NativeMemorySentinel.RegisterNativeArray` and disposed through unregister-before-dispose helpers in `finally`.
- No hot-path LINQ, dictionary `foreach`, `string.Format`, interpolated string, `transform.position`, `Vector3.Distance`, `math.distance`, `GlobalRegistry`, `Awake`, or `OnEnable` was found in the currently touched hydraulic smoke source.
- `.ToString()` remains editor JSON-only in `BuildJson` / `AppendFloat`, outside runtime `Update`, `Tick`, `FixedTick`, `LateUpdate`, or `SlowTick`.

### Excision

- Added Burst `HydraulicErosionMetricReductionJob : IJob` to reduce `HydraulicErosionMetricBlock` data off the managed main thread.
- Replaced the previous editor scalar `ReduceMetrics` block loop with a single `metricSummary[0]` read after the scheduled reduction job completes.
- Added `metricSummary` NativeArray registration and disposal; active sentinel expectation during the scenario window changed from `6` to `7`, and post-dispose JSON remains `sentinelDelta=0`.
- Added the missing Windows x64 native dependency for `SaveBinaryStorage` LZ4 externs at `Assets/_Project/Plugins/Windows/x86_64/liblz4.dll`.
- Native dependency hash: `SHA256=911CAA0291D0B2EB44C1115E5B3E7F0FD38A93FEE4C1F4F0004FB8CFCF040990`.

### Verification Delta

- Passed: Unity MCP recovered after domain reload and `read_console` returned `0` error entries before smoke execution.
- Passed: Unity MCP executed `Tools/Hecton/Dev/Terrain/Run Hydraulic Erosion Smoke Tester`.
- Passed: current `CodexArtifacts/HydraulicErosionSmokeTester.json` was regenerated at `2026-05-05 20:51:55` and reports `scenarioCount=4`, `passCount=4`, `status=PENDING VERIFICATION`.
- Passed: all four scenarios report `passed=true`, `nanCount=0`, `boundaryNanCount=0`, `sentinelDelta=0`, and `trackedByteDelta=0`.
- Passed: Unity MCP `validate_script(level=standard)` returned `0` errors and `0` warnings for `HydraulicErosionMetricsJob.cs` and `HydraulicErosionSmokeTester.cs`.
- Passed: scoped `git diff --check` returned no whitespace errors; Git emitted LF-to-CRLF warnings only.
- Passed: scoped static scan returned no `.Complete()`, `.Run()`, `string.Format`, `foreach`, `GlobalRegistry`, `Awake`, `OnEnable`, `transform.position`, `Vector3.Distance`, `math.distance`, `NativeList`, `NativeQueue`, or `$"` in `HydraulicErosionMetricsJob.cs` / `HydraulicErosionSmokeTester.cs`.
- Scenario data: `dry_zero_power` `milliseconds=13.3188`, `boundarySampleCount=28`, `maxBoundaryHeightDelta=0.604872`, `sumSediment=0`, `sumWear=0`.
- Scenario data: `tiny_margin_clamp` `milliseconds=3.4187`, `boundarySampleCount=192`, `maxSediment=0.75611`, `maxWear=0.775886`.
- Scenario data: `draft_tile` `milliseconds=24.7887`, `boundarySampleCount=960`, `sumSediment=364.3792`, `sumWear=381.0726`.
- Scenario data: `thermal_stress` `milliseconds=19.7306`, `boundarySampleCount=1472`, `sumSediment=219.2557`, `sumWear=241.2482`.
- Passed: after clearing the Unity console and rerunning smoke with a single `liblz4.dll` asset, `read_console(types=error,count=80)` returned `0` log entries.

## 2026-05-05 Omega-Autonomy V2 Cancellation Crucible

Status: `PENDING VERIFICATION`

Project authority status remains blocked from promotion until Scene 03 MapMagic graph execution, profiler captures, and runtime GC evidence exist. This pass verifies only the scoped hydraulic erosion code path and its editor smoke harness.

### Garbage Found

- Defect: `HectonHydraulicErosionMapMagicNode.Generate` still scheduled sediment and erosion-depth `NormalizeMaskInPlaceJob` work after MapMagic set `stop.stop`.
- Impact: publish was skipped after the cold barrier, so these normalization jobs were provably wasted cancellation-path CPU/worker work. On MX350-class budget this is a bad failure mode during terrain graph abort/rebuild churn.
- No unregistered `NativeArray`, `NativeList`, or `NativeQueue` allocation was found in scoped erosion files.
- No hot-path LINQ, dictionary `foreach`, `string.Format`, interpolated string, `transform.position`, `Vector3.Distance`, `math.distance`, `GlobalRegistry`, `Awake`, or `OnEnable` was found in scoped erosion files.
- `.ToString()` remains editor JSON-only in smoke artifact writers, outside runtime `Update`, `Tick`, `FixedTick`, `LateUpdate`, or `SlowTick`.

### Excision

- Guarded `MaskNormalizeProfilerMarker` and both `NormalizeMaskInPlaceJob` schedules with `if (stop == null || !stop.stop)`.
- Kept the publish barrier and `finally` completion path intact. Any erosion, flat smoothing, thermal slump, or canyon jobs already scheduled before cancellation are still completed before `NativeMemorySentinel` unregister and `Dispose()`.

### Verification Delta

- Passed: Unity MCP `validate_script(level=standard)` returned `0` errors and `0` warnings for `HydraulicErosionJob.cs`, `HectonHydraulicErosionMapMagicNode.cs`, `HydraulicErosionMetricsJob.cs`, `ErosionTestHarness.cs`, and `HydraulicErosionSmokeTester.cs`.
- Passed: Unity MCP `read_console(types=error)` returned `0` entries before and after smoke execution.
- Passed: Unity MCP executed `Tools/Hecton/Dev/Terrain/Run Hydraulic Erosion Smoke Tester`.
- Passed: `CodexArtifacts/HydraulicErosionSmokeTester.json` regenerated at `2026-05-05 21:42:17`, reporting `scenarioCount=4`, `passCount=4`, and `status=PENDING VERIFICATION`.
- Passed: all four smoke scenarios report `passed=true`, `nanCount=0`, `boundaryNanCount=0`, `sentinelDelta=0`, and `trackedByteDelta=0`.
- Scenario data: `dry_zero_power` `milliseconds=19.9758`, `sumSediment=0`, `sumWear=0`.
- Scenario data: `tiny_margin_clamp` `milliseconds=2.3511`, `maxSediment=0.75611`, `maxWear=0.775886`.
- Scenario data: `draft_tile` `milliseconds=22.3727`, `sumSediment=364.3792`, `sumWear=381.0726`.
- Scenario data: `thermal_stress` `milliseconds=8.0361`, `sumSediment=219.2557`, `sumWear=241.2482`.
- Passed: scoped `git diff --check` returned no whitespace errors; Git emitted LF-to-CRLF warnings only.
- Passed: `dotnet build Hecton8.Core.csproj --disable-build-servers -m:1 -v:minimal` completed with `0` errors and `51` existing warnings from Unity/third-party/project files outside this erosion patch.

## 2026-05-05 Scene 03 Sandbox MapMagic Binding Crucible

Status: `PENDING VERIFICATION`

Project authority status is not promoted to `OMEGA V2 VERIFIED`; runtime Scene 03 generation, profiler markers, and GC allocation proof are still blocked by Unity MCP disconnects. This pass repairs and verifies the serialized Scene 03 sandbox binding only.

### Garbage Found

- Defect: `Assets/_Project/Scenes/03_HECTON_SANDBOX_BIOMES.unity` contained an inactive MapMagic object named `[SANDBOX_MAPMAGIC_BINDING_COMPILE_BLOCKED]`.
- Defect: `Assets/_Project/Data/World/Sandbox/HECTON_SANDBOX_BIOMES_MAPMAGIC_GRAPH.asset` was an empty sandbox graph shell and did not contain the integrated HECTON biome, hydraulic erosion, or splatmap nodes.
- Defect: first filesystem graph copy carried CRLF line endings that `git diff --check` treated as trailing whitespace across the new YAML payload.
- No runtime C# code was changed in this pass. No new `NativeArray`, `NativeList`, `NativeQueue`, LINQ, dictionary `foreach`, `string.Format`, `transform.position`, `GlobalRegistry`, `Awake`, or `OnEnable` surface was introduced by the Scene 03 binding repair.

### Excision

- Replaced the sandbox graph payload with the already integrated canonical MapMagic graph from `Assets/MapMagic/Map_Graph/New Gen/ACTUAL TERRAIN.asset`, preserving the sandbox `.meta` GUID `569d36fc879e1e044a410c62ce64a383`.
- Renamed the graph asset payload to `HECTON_SANDBOX_BIOMES_MAPMAGIC_GRAPH` so Unity object name and asset path agree.
- Reactivated the Scene 03 MapMagic object and renamed it to `SANDBOX_MAPMAGIC_RUNTIME`.
- Preserved the Scene 03 graph reference: `{fileID: 11400000, guid: 569d36fc879e1e044a410c62ce64a383, type: 2}`.
- Normalized the copied graph asset to LF line endings and stripped trailing whitespace.

### Verification Delta

- Passed: sandbox graph now contains `HectonBiomeMatrixMapMagicPostProcessNode` at line `12587`.
- Passed: sandbox graph now contains `HectonHydraulicErosionMapMagicNode` at line `12674`.
- Passed: sandbox graph now contains `HectonTerrainSplatmapMapMagicNode` at line `12883`.
- Passed: Scene 03 YAML now contains `m_Name: SANDBOX_MAPMAGIC_RUNTIME` at line `323`, `m_IsActive: 1` at line `328`, and the preserved graph GUID at line `356`.
- Passed: `git diff --check -- Assets/_Project/Scenes/03_HECTON_SANDBOX_BIOMES.unity Assets/_Project/Data/World/Sandbox/HECTON_SANDBOX_BIOMES_MAPMAGIC_GRAPH.asset` returned no whitespace errors after LF normalization.
- Passed: Unity `Editor.log` recorded imports for `Assets/_Project/Scenes/03_HECTON_SANDBOX_BIOMES.unity` and `Assets/_Project/Data/World/Sandbox/HECTON_SANDBOX_BIOMES_MAPMAGIC_GRAPH.asset` after the repair.
- Blocked: Unity MCP `refresh_unity` timed out after `60s` waiting for editor readiness, then `manage_scene` and `read_console` calls repeatedly failed with WebSocket/session disconnects.
- Blocked: runtime Scene 03 terrain generation, profiler marker capture, and zero-GC runtime evidence remain absent.
