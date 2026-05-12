# Status_CORE_ORIGIN_SHIFT

Status authority: PENDING VERIFICATION until Unity Console, profiler, and visual captures prove runtime behavior.

Prompt: CORE_ORIGIN_SHIFT
Role: AUP_DICTATOR
Domain: ECHELON 1 / Origin Shift (AUP Manager)
Task count: 15

Mandates loaded:
- MATH_Coordinate_Precision_AUP_FloatingOrigin.txt
- OPT_Zero_GC_Policy_AllocFree_Mandate.txt
- OPT_Native_Memory_Collections_JobSystem_Protocol.txt
- ARCH_Global_Registry_ServiceLocator_DI_Init.txt
- DBG_Telemetry_Crash_Reporting_PostMortem.txt
- REND_URP_Graphics_HotPath_Optimization_HLOD.txt
- REND_GPU_Sovereignty.txt
- OPT_Cinematic_Cheat_Protocol_Visual_Fake_First.txt

## Loop 0 - Intake

- [x] Extract prompt by CLI | DOD: full XML block read from Docs/Tasks/CURRENT_BATCH.md with regex anchored to id="CORE_ORIGIN_SHIFT". | Rejected: MCP/basic file read because batch protocol forbids truncation risk. | Estimate: 1200 us.
- [x] Verify empty status/rationale state | DOD: absent files treated as clean start; no old batch data consumed. | Rejected: reading previous agents' logs because batch hygiene forbids old context. | Estimate: 800 us.
- [x] Read domain authority | DOD: Actual Domains file maps CORE_ORIGIN_SHIFT to ECHELON 1 Origin Shift/AUP Manager. | Rejected: guessing domain from filename only. | Estimate: 900 us.
- [x] Load relevant mandates | DOD: 8 task-bound mandates read before code. | Rejected: broad registry load because it bloats context and violates selective ingestion. | Estimate: 3500 us.
- [x] Re-extract prompt after Task 3 boundary | DOD: CORE_ORIGIN_SHIFT XML block re-read from CURRENT_BATCH.md via CLI. | Rejected: relying on chat summary after three task edits. | Estimate: 900 us.
- [x] Re-extract prompt after Task 9 boundary | DOD: CORE_ORIGIN_SHIFT XML block re-read from CURRENT_BATCH.md via CLI. | Rejected: continuing from compressed context without batch proof. | Estimate: 900 us.
- [x] Re-extract prompt after Task 12 boundary | DOD: CORE_ORIGIN_SHIFT XML block re-read from CURRENT_BATCH.md via CLI after Hi-Z/spatial-hash changes. | Rejected: relying on summarized task list after another mutation loop. | Estimate: 900 us.
- [x] Execute OMEGA_POLISH after core tasks | DOD: POLISH_MANDATE parsed only after all tasks were checked/blocked; transform-shift Burst attribute removed; GC/string/math scan recorded. | Rejected: writing `VERIFIED MASTER GRADE` without Unity/profiler proof. | Estimate: 9000 us static scan.

## Core Tasks

- [x] Task 1 - VFX bounds shift | Justification: HectonFloatingOrigin rebases active world-space ParticleSystem particles via preallocated GetParticles/SetParticles scratch and refreshes bounds. PENDING VERIFICATION. | Alternatives Rejected: per-system allocations and blind restart because they either allocate or kill active VFX. | Estimate: 85 us for 512 particles, 900 us for 8K particles on MX350-class CPU; unverified.
- [x] Task 2 - NativeTrailRenderer | Justification: Added AUP-ring-buffer NativeTrailRenderer using generated mesh strip and Graphics.DrawMeshInstanced. PENDING VERIFICATION. | Alternatives Rejected: Unity TrailRenderer hidden vertex state and prefab YAML mass-edit. | Estimate: 35 us for 32 samples, 120 us for 128 samples; unverified.
- [x] Task 3 - Camera cut-cut fix | Justification: custom camera rig resets previous local position/world rotation cache on origin shift and locks same-frame application. PENDING VERIFICATION. | Alternatives Rejected: next-frame-only lock because the player camera can tick after the core shift in the same dispatcher frame. | Estimate: 2 us; unverified.
- [x] Task 4 - Rigidbody interpolation reset | Justification: tracked-body teleport resets center of mass/inertia and writes Rigidbody.position/rotation while collisions are disabled and interpolation is suspended. PENDING VERIFICATION. | Alternatives Rejected: MovePosition-only teleport because interpolation can retain stale epoch state. | Estimate: 6 us/body under 64-body cap; unverified.
- [x] Task 5 - Decal re-projection | Justification: construction decal caches shift matrix translations atomically through ConstructionManager origin-shift callback. PENDING VERIFICATION. | Alternatives Rejected: full decal rebuild from module transforms on shift. | Estimate: 10 us for 64 cached decals; unverified.
- [x] Task 6 - Awaitable shift lock | Justification: SystemDispatcher frame-lock returns from Update/LateUpdate for the exact shift frame once HectonFloatingOrigin requests it. PENDING VERIFICATION. | Alternatives Rejected: timeScale pause and multi-frame freeze. | Estimate: skips later lanes, net negative work; unverified.
- [x] Task 7 - Floating-point jitter mask | Justification: `_AupJitterMask` global is armed during the shift render frame and Hecton_CoreLit rounds camera-relative WS positions to 1 mm. PENDING VERIFICATION. | Alternatives Rejected: CPU transform snapping. | Estimate: sub-0.1 ms shift-frame shader branch; unverified.
- [x] Task 8 - Shader global offset | Justification: RenderDispatcher publishes current `_TotalUniverseOffset` before beginCameraRendering renderables. PENDING VERIFICATION. | Alternatives Rejected: renderer-local offset publication. | Estimate: 4 us/camera for global writes; unverified.
- [x] Task 9 - Pre-shift event | Justification: GlobalSignals has AupPreShiftSignal queue and HectonFloatingOrigin schedules actual shift for frame+1 after publishing it. PENDING VERIFICATION. | Alternatives Rejected: committed-only AupShiftSignal. | Estimate: 3 us enqueue; unverified.
- [x] Task 10 - Squadron teleport | Justification: DroneFleetOriginShiftJob translates native drone state, SoA positions, and render matrices; ConstructionManager dispatches it on origin shift. PENDING VERIFICATION. | Alternatives Rejected: managed-only matrix rebuild. | Estimate: 50 us for 64 slots; unverified.
- [x] Task 11 - Hi-Z cache flush | Justification: GPUScatterDirector invalidates the Hi-Z depth pyramid on the committed shift frame, disables same-frame occlusion, resets scatter frame cadence, and clears foveated visibility history. PENDING VERIFICATION. | Alternatives Rejected: releasing/reallocating RenderTexture on every shift because that trades tearing for VRAM churn. | Estimate: 3 us for state invalidation; one skipped depth-pyramid dispatch on shift frame; unverified.
- [x] Task 12 - Spatial hash re-index | Justification: WorldSpatialHashGrid now rebases managed runtime-position caches and transient signal runtime positions by shift offset while leaving native AUP buckets untouched. PENDING VERIFICATION. | Alternatives Rejected: rebuilding absolute positions and calling TryUpdateEntry for each resident fish because that reinserts all occupied cells. | Estimate: 18 us for 256 cached entries, 650 us for 10K metadata entries on MX350-class CPU; unverified.
- [x] Task 13 - Zero-GC validation | Justification: static allocation scan found particle correction scratch preallocated; NativeTrailRenderer no longer reallocates capacity from Tick; shift discovery lists were raised to fixed cold capacities. PENDING VERIFICATION because GCMonitor/Profiler proof is absent. | Alternatives Rejected: per-shift ParticleSystem arrays and per-Tick NativeTrail buffer resizing. | Estimate: 0 B managed allocation in intended shift path; measurement absent.
- [x] Task 14 - Reconnaissance protocol | Justification: appended high-risk cached `Transform.position`/world-position findings to Docs/AgentLogs/RECON_CORE_ORIGIN_SHIFT.md. PENDING VERIFICATION. | Alternatives Rejected: patching all owners from CORE_ORIGIN_SHIFT because those files belong to other domains. | Estimate: 16000 us static scan; no runtime impact.
- [x] Task 15 - Omega compile check | Justification: `dotnet build Hecton8.Core.csproj --no-restore -m:1 /p:UseSharedCompilation=false` reached compiler diagnostics and is BLOCKED BY DEPENDENCY on `HectonSurvivalSystem.cs(298,29)` missing `SurvivalPhysiologyScalarResult`; Burst AUP scan found no Unity API inside remaining Burst origin-shift jobs. PENDING VERIFICATION. | Alternatives Rejected: editing Survival physiology from CORE_ORIGIN_SHIFT. | Estimate: 52180 ms build attempt.

## Loop 6 - Honest R&D / AAA Presentation Cache Hardening

- [x] Task 16 - Vegetation renderer AUP cache reset | Justification: `HectonIndirectVegetationRenderer` now implements `IOriginShiftListener`, rebases cached cull/motion camera positions, rebases explicit world bounds, invalidates far-cull history, restarts the culling cadence on committed origin shifts, and no longer depends on untracked `HardwareTierDetector` in this file. PENDING VERIFICATION. | Alternatives Rejected: releasing/reallocating BRG/indirect buffers or rebuilding vegetation instance data because field-reset invalidation is the cheaper deterministic visual fake. | Estimate: 2 us for scalar cache reset; one forced far-cull refresh on the next vegetation render, unverified.

## Verification Log

- BLOCKED BY DEPENDENCY: Loop 1 `dotnet build Hecton8.Core.csproj` failed on unrelated pre-existing compile errors:
  - `Assets/_Project/Scripts/Core/HectonArenaAllocator.cs`: missing `NativeArenaArray<>`.
  - `Assets/_Project/Scripts/TetherInstance.cs`: missing `TetherVerletTelemetryEntry`.
  - `Assets/_Project/Scripts/World/AbyssalThermalManager.cs`: `IFixedTickable.FixedTick(float)` not implemented.
  Current CORE_ORIGIN_SHIFT files produced no compiler diagnostics before the dependency wall.
- BLOCKED BY DEPENDENCY: Loop 2 `dotnet build Hecton8.Core.csproj` failed on unrelated pre-existing compile errors:
  - `Assets/_Project/Scripts/HectonSurvivalSystem.cs(298,29)`: missing `SurvivalPhysiologyScalarResult`.
  - `Assets/_Project/Scripts/Gameplay/MantaScooter.cs(38,89)`: missing `IPlayerTransportSource.GetTransportDragCoefficientMultiplier()`.
  - `Assets/_Project/Scripts/TetherInstance.cs(174,29)`: missing `TetherVerletTelemetryEntry`.
  Current CORE_ORIGIN_SHIFT files produced no compiler diagnostics before the dependency wall.
- BLOCKED BY TOOLING: Loop 3 `dotnet build Hecton8.Core.csproj` exceeded the 120 s command timeout and spawned compiler worker processes. New build workers were stopped; one older dotnet process predating this pass was left intact.
- BLOCKED BY DEPENDENCY: Loop 5 `dotnet build Hecton8.Core.csproj --no-restore -m:1 /p:UseSharedCompilation=false` failed on unrelated pre-existing compile error:
  - `Assets/_Project/Scripts/HectonSurvivalSystem.cs(298,29)`: missing `SurvivalPhysiologyScalarResult`.
  Current CORE_ORIGIN_SHIFT files produced no compiler diagnostics before the dependency wall.
- BLOCKED BY TOOLING/DEPENDENCY: Loop 6 `dotnet build Hecton8.Core.csproj --no-restore -m:1 /p:UseSharedCompilation=false` first reported 77 errors, primarily missing untracked/shared platform services (`HectonPersistentPathPolicy`, `PlatformPrecisionClock`, `HectonThreadPriorityPolicy`, `SteamDeckInputPal`, `HectonNativeBridge`, etc.). It also reported `HectonIndirectVegetationRenderer.cs(1483,18)` using `HardwareTierDetector`; that same-file dependency was removed because `HardwareTierDetector.cs` is untracked/not in the generated csproj. Follow-up build attempts timed out before diagnostics; spawned dotnet workers were stopped. PENDING VERIFICATION.
- STATIC CHECK: `rg "HardwareTierDetector" Assets/_Project/Scripts/World/HectonIndirectVegetationRenderer.cs` returned no matches after the compile-remediation patch.
- STATIC CHECK: `git diff --check` for Loop 6 files reported no whitespace errors; only existing LF/CRLF normalization warning for `HectonIndirectVegetationRenderer.cs`.
- BLOCKED BY TOOLING: Unity MCP `validate_script` for `Assets/_Project/Scripts/World/HectonIndirectVegetationRenderer.cs` timed out; Unity MCP `read_console` failed because ping was not answered / session not ready. Dotnet workers spawned by this pass were stopped.
- PENDING: Unity Console.
- PENDING: GC/profile proof.
- PENDING: visual capture.
