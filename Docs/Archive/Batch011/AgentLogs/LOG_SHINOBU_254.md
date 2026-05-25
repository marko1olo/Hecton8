# SHINOBU_254 Log

Top=Old, Bottom=New.

## 2026-05-21 - Headless KCC Smoke Harness

What was wrong:
- Core KCC stability had no dedicated headless 10,000-frame gate in the test layer.
- Manual/editor playtesting could not prove no NaN, no strong SDF penetration, or <0.05 ms/frame KCC math cost.
- Runtime verification was blocked at the end by a dependency compile wall outside SHINOBU_254: `Assets/_Project/Scripts/Core/Contracts/AupPrecisionContracts.cs(347,23)` references missing `long3`.

What was done:
- Added `Assets/_Project/Tests/Editor/HeadlessKccSmokeTests.cs` and deterministic `.meta`.
- Added NUnit tests for headless dependency scanning, ARM64/explicit DTO layout, and the 100 phantom / 10,000 frame smoke run.
- Added data-only SDF generation into `BufferID.ShinobuKccEnvironmentSdf`, no Unity scene or collider dependency.
- Added 100 phantom player initialization into existing KCC vault lanes: states, inputs, proposed velocities, and fault flags.
- Added hostile input generation with zero/infinity injection and pre-simulation sanitization.
- Added fused Burst headless dispatcher job with explicit PRE_SIMULATION, SIMULATION, POST_SIMULATION functions.
- Added finite-state validator, SDF penetration detector, average us/frame threshold flag, decimal AUP drift verifier, allocation delta check, and finally-dispose cleanup.
- Added failure CSV export at `Docs/Reports/HEADLESS_KCC_FAILURES.csv`.
- Added 300-frame black box dump at `Docs/AgentLogs/Dump_SHINOBU_254.bin`.
- Added success report path `Docs/Reports/QA_OPTIMIZATION_REPORT.json`.
- Added UI Toolkit window `HECTON-8/Kinematics/Headless Smoke Tester`.
- Added editor replay gizmo that draws a flashing red skull-like marker at the first failed AUP without `FindObjectOfType`.

Cinematic cheats used:
- Voxel collision world is a deterministic SDF hollow shell with jagged sine noise and pillars. This is a math fake, not a loaded world.
- Hostile currents use polynomial sine fields, not a fluid solver.
- Collision proof uses swept SDF samples and capsule radius subtraction, not Unity Physics broadphase.

Exact microseconds saved:
- Scheduler overhead avoided: 30,000 per-frame Schedule/Complete calls rejected. Estimated saved cost: hundreds of milliseconds over the full 10,000-frame run on low-end silicon.
- Scene load/broadphase avoided: Unity Physics scene and colliders rejected. Estimated saved runtime dependency: all graphics/scene variance removed from the measurement.
- Measured average microseconds per frame: PENDING. Runtime test could not complete because Core.Contracts dependency compilation fails before Hecton8.EditModeTests can be validated.

Compile/test evidence:
- Unity batchmode command imported `HeadlessKccSmokeTests.cs` into `Library/Bee/artifacts/1900b0aEDbg.dag/Hecton8.EditModeTests.rsp`.
- Unity/Bee first pass completed with "Tundra requires additional run"; second pass stalled without diagnostics for 35 minutes and was stopped.
- Direct test csc failed before SHINOBU_254 code because dependency ref DLLs were missing.
- Direct Core.Contracts csc failed at `AupPrecisionContracts.cs(347,23): CS0246 long3`.

<SELF_AUDIT>
  <ArrayFormats>
    <KinematicStateDTO size="64" owner="existing KCC runtime" validated="UnsafeUtility.SizeOf + Marshal.OffsetOf"/>
    <ForcePacketDTO size="32" owner="existing physics contract" validated="UnsafeUtility.SizeOf + Marshal.OffsetOf"/>
    <HeadlessKccProfileDTO size="64" allocation="NativeArray TempJob UninitializedMemory"/>
    <HeadlessKccVoxelSdfInfoDTO size="64" allocation="value DTO"/>
    <HeadlessKccTestResultDTO size="64" allocation="NativeArray TempJob UninitializedMemory"/>
    <HeadlessKccFailureRecordDTO size="128" allocation="NativeArray TempJob UninitializedMemory"/>
    <HeadlessKccTelemetryEntry size="64" count="300" allocation="NativeArray TempJob UninitializedMemory"/>
    <HeadlessKccDriftProbeDTO size="64" allocation="NativeArray TempJob UninitializedMemory"/>
  </ArrayFormats>
  <EditorTooling>
    <Window menu="HECTON-8/Kinematics/Headless Smoke Tester" button="RUN 10,000 FRAME KCC TEST"/>
    <Gizmo failureMarker="flashing red skull-like marker" sceneSearch="false"/>
  </EditorTooling>
  <HotPathGC targetBytes="0" method="GC.GetAllocatedBytesForCurrentThread around scheduled 10000-frame Burst job"/>
  <ManualPhysicsQA status="replaced by deterministic headless NUnit harness, pending runtime execution after Core.Contracts compile wall is fixed"/>
</SELF_AUDIT>

## 2026-05-21 - Reattempt After Reissued Prompt

What was wrong:
- The first hard blocker was real: `AupPrecisionContracts.cs` used `long3`, and this Unity.Mathematics package does not provide `long3`.
- After fixing that, the remaining blocker is Unity/Bee backend2 stalling after `Tundra requires additional run`, before NUnit writes `SHINOBU_254_HeadlessKccSmokeTests.xml`.

What was done:
- Re-read `Docs/Tasks/Status_SHINOBU_254.md`, `Docs/AgentLogs/Rationale_SHINOBU_254.md`, and re-extracted the SHINOBU_254 XML prompt from `CURRENT_BATCH.md`.
- Added `Hecton8.Core.Contracts.long3` in `Assets/_Project/Scripts/Core/Contracts/AupPrecisionContracts.cs`.
- Re-ran direct Core.Contracts compiler pass: success, analyzer warnings only.
- Re-ran Unity batchmode with `-nographics` and the exact `Hecton8.Tests.Editor.HeadlessKccSmokeTests` filter.
- Stopped only the Unity/Bee processes launched by SHINOBU_254 after backend2 produced no new diagnostics for 20 minutes.

Cinematic Cheats used:
- Unchanged: deterministic SDF, polynomial hostile currents, swept SDF capsule resolution.

Exact Microseconds saved:
- Measured KCC frame time remains unavailable because the Unity test runner never reached NUnit execution.
- Compile contract fix saves no runtime microseconds; it removes the AUP type blocker so the smoke test can compile once Bee completes.

Remaining blocker:
- Unity/Bee backend2 stalls after rebuilding Core.Contracts. No `Docs/Reports/SHINOBU_254_HeadlessKccSmokeTests.xml`, no `QA_OPTIMIZATION_REPORT.json`, no failure CSV, and no black box dump were produced during this reattempt.
