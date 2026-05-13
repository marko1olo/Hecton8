# Status_BIOLUMINESCENCE_DIRECTOR

Date: 2026-05-13
Agent Role: LIGHTING_TECH
Prompt: BIOLUMINESCENCE_DIRECTOR
Domain: Domain 29 - Bioluminescence Sync
Status: PENDING VERIFICATION

Mandates loaded:
- OPT_Cinematic_Cheat_Protocol_Visual_Fake_First.txt
- OPT_Zero_GC_Policy_AllocFree_Mandate.txt
- OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt
- REND_Abyssal_Lighting_Voxel_Occlusion_Shadows.txt
- REND_Shader_Noir_Aesthetics_Dithering_Fog.txt
- REND_URP_Graphics_HotPath_Optimization_HLOD.txt
- DBG_Telemetry_Crash_Reporting_PostMortem.txt
- MATH_Coordinate_Precision_AUP_FloatingOrigin.txt

## Checklist

- [x] Task 1 - Singleton eradication | DONE | DOD: removed `HectonBiolumManager.Instance`; director now registers/unregisters through `GameBootstrapper.RegisterBiolumDirector`. Rejected: static singleton facade over GlobalRegistry. Estimate: 15 us/frame saved if old singleton Update paths were reintroduced.
- [x] Task 2 - Signal migration | DONE | DOD: camera/player position resolves through `GameBootstrapper`/`GlobalRegistry`; wake data consumes `GlobalSignals.TryDequeueMovementAcoustic`. Rejected: `Player.Instance.Position`. Estimate: 5 us/frame saved.
- [x] Task 3 - ASMDEF isolation | BLOCKED BY DEPENDENCY | DOD: verified no `Hecton8.Lighting.asmdef` or `Hecton8.Core.Contracts.asmdef` exists. Rejected: creating a child lighting assembly while `WorldSpatialHashGrid`/`SpatialQueryHit` are internal and `GlobalRegistry` stores concrete `HectonBiolumManager`, which would create assembly breakage. Estimate: 0 us/frame, compile isolation blocked upstream.
- [x] Task 4 - Dead code hunt | DONE | DOD: `rg` found no `MaterialPropertyBlock` glow scripts under `Assets/_Project/Scripts/World/Biolum`. Rejected: deleting unrelated MPB users outside the domain. Estimate: 20 us/frame potential avoided under dense flora.
- [x] Task 5 - Global pulse sine | DONE | DOD: central manager publishes one master sine from celestial time/Unity time fallback. Rejected: per-coral material sine. Estimate: 40 us/frame saved under dense coral.
- [x] Task 6 - Global shader vars | DONE | DOD: `_BiolumMasterPhase` and `_BiolumIntensity` pushed with `Shader.SetGlobalVector`. Rejected: per-material mutation. Estimate: 30 us/frame saved.
- [x] Task 7 - Material audit culling | DONE | DOD: coral, GPUI coral, kelp, GPUI kelp, and sargassum shaders removed `_BiolumPulseAmplitude` and `_BiolumPulseFrequency`; all authored biolum pulses now read director globals. Rejected: material clone/property churn. Estimate: 15 us/frame saved.
- [x] Task 8 - Day/night suppression | DONE | DOD: reads `CelestialRuntimeSnapshot` from `GlobalRegistry`; daytime shallows above -50m force global biolum intensity to 0 unless eclipse is active. Rejected: all-day shallow glow. Estimate: 10 us/frame GPU ALU saved when suppressed.
- [x] Task 9 - Proximity blackout | DONE | DOD: `WorldSpatialHashGrid` bioform query filters apex predators and schedules a Burst `IJobParallelFor`; intensity fades to 0.1 over 2 seconds. Rejected: per-plant predator checks. Estimate: 50 us/frame saved in plant fields.
- [x] Task 10 - Touch ripple wake | BLOCKED BY DEPENDENCY / ADAPTED | DOD: exact `EntityWakeSignal` does not exist; implemented compatible wake consumption from existing `MovementAcousticSignal`, injecting AUP-derived runtime position and velocity radius into `_BiolumTouchRipples`. Rejected: spawned ripple GameObjects. Estimate: 35 us/frame saved.
- [x] Task 11 - Burst distance job | DONE | DOD: ripple distance calculation runs in `RippleDistanceJob : IJobParallelFor`; capacity is fixed at 16 so all live ripples are the 16 closest retained by replacement score. Rejected: LINQ/managed sort. Estimate: 25 us/frame saved.
- [x] Task 12 - Shader ripple math | DONE | DOD: shader uses `dot(diff, diff)` and inverse-square flash multiplier up to 3.0x. Rejected: `distance()`/sqrt. Estimate: 10 us/frame GPU ALU saved.
- [x] Task 13 - Wave synchronization | DONE | DOD: samples `HectonFluidEngine.TrySampleModAbyssalFlow`; high current raises pulse frequency up to +20%. Rejected: new direct AbyssalFlow owner dependency. Estimate: 0 us/frame, visual coherence gain.
- [x] Task 14 - AUP shift safety | DONE | DOD: manager implements `IOriginShiftListener` and subtracts `OriginShiftEventData.ShiftOffset` from active ripple runtime positions. Rejected: draining shared `AupShiftSignal` queue and stealing from chunk residency. Estimate: correctness guard.
- [x] Task 15 - Math LOD | DONE | DOD: both coral shader variants wrap the ripple loop in `#if !defined(_MATH_LOD_LOW)` and C# uploads count 0 on low tier. Rejected: uniform full ripple path on MX350. Estimate: 60 us/frame GPU saved on low tier.
- [x] Task 16 - Zero-GC | DONE | DOD: hot path uses fixed arrays, persistent NativeArrays, GraphicsBuffer, no LINQ; file/string allocations are cold crash-dump only. Rejected: per-frame containers and spawned objects. Estimate: 0 B/frame target.
- [x] Task 17 - Omega compile check | BLOCKED BY DEPENDENCY | DOD: `dotnet build Hecton8.Core.csproj --no-restore` fails before this domain in `Hecton8.Bootstrap.Contracts`; no biolum errors surfaced in the no-project-reference Core pass, which fails on unrelated Cartography/CameraJuice/Submarine/GlobalSignals missing types. Rejected: reporting a clean compile. Estimate: verification only.
- [x] Task 18 - Telemetry | DONE | DOD: active ripple count publishes to `GlobalTelemetryBus` and a 300-frame NativeArray blackbox dumps to `Docs/AgentLogs/Dump_BIOLUMINESCENCE_DIRECTOR.bin` on invalid biolum math/input. Rejected: chat-only telemetry claim. Estimate: crash diagnosis gain.

## Iteration Log

- Loop 0: Prompt extracted. Mandates loaded. No code changed.
- Loop 1: Tasks 1-5 executed. Read manager lifecycle, removed singleton access, inserted bootstrap registration, global pulse path. Compile attempt 1 blocked by `BootstrapStatus.cs` missing `ITickDispatcher`/`GlobalRegistry`.
- Loop 2: Tasks 6-10 executed. Read shader and manager again, added global vectors, celestial suppression, predator blackout, and MovementAcoustic wake adapter. Exact `EntityWakeSignal` absent, marked dependency/adapted.
- Loop 3: Tasks 11-13 executed. Read ripple code and shader falloff, confirmed `IJobParallelFor`, no LINQ, `dot(diff,diff)`, and AbyssalFlow +20% modulation.
- Loop 4: Tasks 14-16 executed. Read AUP origin contracts, implemented `IOriginShiftListener`, low-tier shader guard, fixed-buffer zero-GC audit. Cold allocations documented.
- Loop 5: Tasks 17-18 executed. Build pass blocked by external missing types; telemetry ring and dump path implemented. `git diff --check` returned only line-ending warnings.
- Loop 6: OMEGA polish executed after all task boxes were checked/blocked. Audited touched code for hot-path `foreach`, LINQ, `ToString`, `distance`, `sqrt`, and `normalize`; no hits in the biolum hot path. Replaced the new predator fade division with a compile-time reciprocal constant. Remaining division hits are pre-existing legacy zone/sonar code, comments, includes, or shader `rcp` use.
- Loop 7: Continuation audit on 2026-05-13, per user instruction not to launch `dotnet build`. Re-read prompt, rules, status, and rationale. Fixed concrete misses: ripple distance job now drives nearest-first upload order, Tick finalization uses non-warning `TryFinalizeCompleted`, touch ripple uploads are double-buffered and skipped on low-tier/count-zero frames, shader radius gating now enforces `dot(diff,diff) < radiusSq`, and NaN paths throttle cold dump I/O. Static check only: `git diff --check` returned CRLF warnings only; forbidden hot-path scan found only fixed-capacity cold `List<T>` fields.
- Loop 8: Second continuation audit on 2026-05-13, no `dotnet build` launched. Re-read prompt from `CURRENT_BATCH.md`. Fixed double celestial multiplier application by keeping `_BiolumIntensity.x` as director dimming only, mirrored the global biolum contract into `Hecton_CoralMaster_GPUI.shader`, and raised only the ForwardLit passes that bind `StructuredBuffer<float4>` to target 4.5. Static checks only: old pulse properties no longer appear in either coral shader; `git diff --check` reports CRLF warnings only.
- Loop 9: Third continuation audit on 2026-05-13, no `dotnet build` launched. Found and fixed a shader-global type collision: legacy `HectonBiolumController` no longer writes `_BiolumIntensity` as a scalar, and `HectonIndirectVegetationRenderer` now derives culling intensity from `_BiolumIntensity.x` through `Shader.GetGlobalVector`. Static search confirms no remaining `Shader.SetGlobalFloat` or `Shader.GetGlobalFloat` calls target `_BiolumIntensity`.
- Loop 10: Fourth continuation audit on 2026-05-13, no `dotnet build` launched. Extended synchronized pulse ownership beyond coral to kelp, GPUI kelp, and sargassum. Removed remaining `_BiolumPulseAmplitude` and `_BiolumPulseFrequency` shader properties, replaced `_Time.y` biolum pulse phases with `_BiolumMasterPhase.x`, and multiplied authored plant biolum by `_BiolumIntensity.x`. Static search confirms those material pulse property names are absent from `Assets/_Project/Art/Shaders`.
