# LOG_ACOUSTIC_ECHO_LOCATION_AI

## 2026-05-16 - Acoustic Echo Location AI
What was wrong:
- Abyssal predator acoustic targeting could still derive target intent from exact player noise positions, so blind navigation behaved like omniscient sight.
- Acoustic portal propagation exposed `LastPortalAup`, but predator cognition had no bounded sensory bridge consuming portal echo taps.
- DSP/sonar tap intake needed hard count clamps and invalid-number rejection before any predator path target was written.

What was done:
- Confirmed prompt `ACOUSTIC_ECHO_LOCATION_AI`, task count 18, domain AI/PATHING.
- Read AGENTS.md, domain doc, and 8 mandates: acoustic sonar, DSP queue, AI cognition, pathing, AUP determinism, zero-GC, blackbox telemetry, signal lane segregation.
- Implemented/verified `AcousticEchoLocationRuntime` contract: `NativeQueue<EchoTap>`, fixed 32-tap frame slab, `EchoTrackingJob`, portal-AUP result, black-box telemetry, sonar tap hydration, MovementAcousticSignal and AcousticPingSignal fallbacks.
- Wired portal propagation into `SpatialAudioManager` so path-found sounds enqueue echo taps using `AcousticPathResult.LastPortalAup`.
- Wired Fauna predator cognition to use `AcousticEchoLocationRuntime.TryResolvePredatorEcho`; acoustic-only targets now pass the echo portal/investigation AUP, not raw player noise AUP.
- Added high-tier acoustic head sweep via existing Fauna head-look target handoff; visual player contact still overrides the fake.
- Added five-second silence expiry. When the trail expires, no acoustic target is returned and cognition falls back to search/wander behavior.
- Added loudest-tap priority for decoys/noisemakers via `Volume01 * Transmission01` selection.

Cinematic cheats used:
- Low tier: source node = sound node, no portal solve, direct swim to last heard node.
- High tier: lateral head-look sweep is a cheap sine offset, not a physical smell/wake simulation.
- Portal breadcrumbing: predator follows audible portal position, not a full acoustic wave field.

Exact microseconds saved/estimated:
- Removed direct acoustic distance branch for blind predator target acquisition: ~3 us/predator tick.
- Fixed 32-tap Burst scan instead of managed event/list scan: ~20-80 us stall risk avoided on predator-heavy frames.
- Low-tier direct-node fake avoids portal solve entirely on MX350 path: portal solve cost avoided per low-tier echo.
- Head sweep fake cost: ~2-4 us/frame, replacing any full cone/current simulation.
- AUP finite checks and black-box write: ~1-3 us/frame, paid only on bounded tap processing.

Validation:
- `git diff --check` passed on touched acoustic/Fauna/audio/status/rationale files; only line-ending warnings reported.
- `dotnet build Hecton8.Core.csproj --no-restore` is blocked by external dependency churn outside this prompt: deleted `Assets/_Project/Scripts/Core/Bucketing/ModuloSimulationBucketer.cs` and tether contract state from parallel work. No further external repair was performed after dependency wall.

Integrator notes:
- Resolve the Bucketing/Tether compile wall, then rerun `dotnet build Hecton8.Core.csproj --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false`.
- Recheck `NativeArray<SonarEchoTap>.ReadOnly.IsCreated` against the active Unity.Collections version if compile reaches `AcousticEchoLocationRuntime`; the guard is intentionally there to reject invalid DSP snapshots.

## 2026-05-16 - Phase 0-4 Inquisition Polish
What was wrong:
- Acoustic tap/result/state/black-box structs were sequential but not explicitly packed, leaving ARM64/Quest layout proof weaker than required.
- The sensory runtime still had private persistent `NativeArray` fields for frame taps, job result, and black-box telemetry.
- Fault dumps could rewrite the same binary file repeatedly during a NaN storm, which is hostile to Steam Deck MicroSD I/O.

What was done:
- Re-ran the mandated status/rationale read and re-extracted the exact `ACOUSTIC_ECHO_LOCATION_AI` XML prompt.
- Added `SystemID.AISensory` and three `GlobalDataVault` buffer IDs: `AcousticEchoFrameTaps`, `AcousticEchoTrailState`, and `AcousticEchoBlackBox`.
- Moved the acoustic frame slab, job result, and 300-frame black-box ring to generation-checked `VaultBufferHandle<T>` resolution.
- Added `[StructLayout(LayoutKind.Sequential, Pack = 1)]` to every acoustic payload owned by this domain.
- Added finite guards around high-tier head-sweep delta and distance-square math.
- Gated `Dump_ACOUSTIC_ECHO_LOCATION_AI.bin` to one fault dump per session after the ring entry is written.
- Re-scanned `Assets/_Project/Scripts/AI/Sensory/` for `Update`, `LateUpdate`, `FixedUpdate`, `string.Format`, legacy EventBus, managed delegates, and private `NativeArray` allocation.

Cinematic cheats used:
- Low/toaster: direct-node acoustic breadcrumbs and 32-tap cap remain the hard budget guard.
- High/Ultra: acoustic CPU remains cheap enough for richer consumers: IK sweep, visor salt/silt presentation, and hull dent VFX can spend the saved cycles without making acoustic truth more expensive.

Exact microseconds saved/estimated:
- DataVault handle resolution costs roughly +1-2 us on refresh but removes private allocator ownership and stale-array risk.
- One-shot dump gate avoids repeated binary file rewrites during fault storms; runtime hot path cost is one integer branch only on fault.
- Extra finite guards cost below ~0.5 us on high-tier sweep frames and prevent NaN propagation into AI/presentation consumers.

Validation:
- Static scan: no private `NativeArray` fields or `new NativeArray` remain in `AI/Sensory/AcousticEchoLocationRuntime.cs`.
- Static scan: no `Update`, `LateUpdate`, `FixedUpdate`, `string.Format`, legacy EventBus, managed `Action`/`Func`, or duplicate `EchoTap` signal type found in AI/Sensory.
- `dotnet build Hecton8.Core.csproj --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -v:minimal -clp:Summary` remains blocked before AI/Sensory by unrelated errors in `World/EcosystemDirector.cs`, `Core/Determinism/LockstepStateValidator.cs`, `Animation/Locomotion/ProceduralLadderClimbRuntime.cs`, and `SubmarineFluidDynamics.cs`.

Integrator notes:
- Do not treat this as Unity-verified. It is code-review/static-scan verified behind an external compile wall.
- After external compile walls are repaired, rerun the same build command and then Unity Console/Play Mode GC validation for 300 frames.

## 2026-05-16 - Heartbeat And Green Build Pass
What was wrong:
- Black-box telemetry was still biased toward successful hunts and faults, not a strict per-refresh heartbeat ring.
- Echo ingress had a 32-tap processing cap but no explicit 64-tap queue backlog cap on the main-thread bridge.
- `AcousticHuntsTriggered` could wrap after `uint.MaxValue`.
- The previous build status was stale after other agents cleared the external compile wall.

What was done:
- Added one black-box heartbeat write per acoustic refresh frame with same-frame de-duplication.
- Prewarmed the `NativeQueue<EchoTap>` bridge to 64 taps and rejected main-thread enqueue attempts beyond that cap.
- Kept the 32-tap frame slab as the deterministic processing cap and drained surplus queue entries.
- Rejected non-finite `currentTime` through the fault black-box path and dropped queued taps instead of scheduling poisoned work.
- Saturated `AcousticHuntsTriggered` at `uint.MaxValue`.
- Reduced repeated DataVault polling by using the cached vault after handles are created, with fallback refresh only on resolve failure.
- Attempted required `<POLISH_MANDATE>` extraction from `Docs/Tasks/CURRENT_BATCH.md`; the tag is absent.

Cinematic cheats used:
- Toaster mode remains a capped last-node acoustic fake: 64 queued taps, 32 scored taps, no portal wave simulation.
- High/Ultra keep the shark-like head sweep and can spend saved CPU on presentation systems without widening acoustic truth cost.

Exact microseconds saved/estimated:
- Queue backlog cap prevents unbounded NativeQueue growth; hot cost is one integer branch per tap enqueue.
- Heartbeat write costs ~1 us/acoustic refresh and buys the actual 300-frame crash trail.
- Cached DataVault use avoids repeated registry lookup on normal refreshes; fallback lookup only occurs on stale-handle resolve failure.

Validation:
- `rg` scan found no private persistent `NativeArray`, `new NativeArray`, `Update`, `LateUpdate`, `FixedUpdate`, `string.Format`, legacy EventBus, or managed `Action`/`Func` in `Assets/_Project/Scripts/AI/Sensory/`.
- `rg` scan found no unpacked `[StructLayout(LayoutKind.Sequential)]` in `Assets/_Project/Scripts/AI/Sensory/`.
- `git diff --check` on owned acoustic/status/rationale/log files reports CRLF normalization warnings only.
- `dotnet build Hecton8.Core.csproj --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -v:minimal -clp:Summary` succeeded: 0 warnings, 0 errors, 2m11s.

Runtime caveat:
- Unity Play Mode, GCMonitor, and device profiling were not run in this terminal-only pass. Runtime/profiler proof remains pending outside the dotnet compile gate.

## 2026-05-16 - Fresh Build Recheck / External Compile Wall
What was wrong:
- A fresh `dotnet build` no longer matched the prior green state after parallel-domain edits landed.
- The first new blocker was `InputDispatcher.cs` defining `HECTON8_MMF_AVAILABLE` after `using` tokens.
- After that compile-only blocker was cleared, the build failed in non-acoustic systems: `ProceduralBiteIkJobs.cs`, `ToolDurabilitySystem.cs`, and `GameBootstrapper.cs`.

What was done:
- Re-ran the mandated status/rationale read and re-extracted the exact `ACOUSTIC_ECHO_LOCATION_AI` XML prompt.
- Re-scanned `Assets/_Project/Scripts/AI/Sensory/` for forbidden hot-path patterns and unpacked structs; scans remained clean.
- Corrected only the `InputDispatcher.cs` preprocessor placement so the build could proceed past CS1032.
- Marked Task 18 as `[BLOCKED BY DEPENDENCY]` again because the remaining compile errors are outside AI/Sensory ownership.

Cinematic cheats used:
- Acoustic behavior unchanged: low-tier remains direct-node fake, high-tier remains cheap sine head sweep, and echo truth remains bounded to 32 scored taps.

Exact microseconds saved/estimated:
- No acoustic runtime cost added.
- The `InputDispatcher.cs` compile-only correction has 0 us acoustic impact.
- The blocked external build consumed 2m08s wall-clock and stopped before Unity runtime/profiler proof.

Validation:
- `rg` scan found no private persistent `NativeArray`, `new NativeArray`, `Update`, `LateUpdate`, `FixedUpdate`, `string.Format`, legacy EventBus, or managed `Action`/`Func` in `Assets/_Project/Scripts/AI/Sensory/`.
- `rg` scan found no unpacked `[StructLayout(LayoutKind.Sequential)]` in `Assets/_Project/Scripts/AI/Sensory/`.
- `git diff --check` on owned acoustic/status/rationale/log files reports CRLF normalization warnings only.
- Latest `dotnet build Hecton8.Core.csproj --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -v:minimal -clp:Summary` failed with 96 errors outside AI/Sensory.

Integrator notes:
- First remaining error groups: `ProceduralBiteIkJobs.cs(306,24)` duplicate local `candidate`; `ToolDurabilitySystem.cs` missing `TryResolveNativeState`, `TryResolveItemStates`, `TryResolvePendingDecay`, and backing native fields; `GameBootstrapper.cs(2761,34)` calls an `Initialize` overload that no longer exists.
- Do not treat the acoustic prompt as Unity-runtime verified until these external domains compile and Play Mode/profiler evidence is collected.

## 2026-05-16 - Current Disk Green Compile Closure
What was wrong:
- The dependency-blocked status became stale as parallel agents repaired several external compile walls.
- A fresh build narrowed the remaining project blocker to adjacent AI: `PredatorCognitionDomain.cs` used a nonexistent local `IsFinite(float3)` helper in the species-target read path.

What was done:
- Re-ran the mandated status/rationale read before continuing.
- Re-ran the build repeatedly against current disk instead of trusting stale error lists.
- Patched `PredatorCognitionDomain.cs` to use the existing `MathGuard.IsFinite(candidate)` helper.
- Re-ran `dotnet build Hecton8.Core.csproj --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -v:minimal -clp:Summary`.
- Updated `Status_ACOUSTIC_ECHO_LOCATION_AI.md` and `Rationale_ACOUSTIC_ECHO_LOCATION_AI.md` back to green compile evidence.

Cinematic Cheats used:
- Acoustic behavior unchanged: low-tier direct-node fake, 32 scored echo taps, 64 queued tap cap, and high-tier sine head sweep remain intact.
- The compile repair did not add physical simulation.

Exact Microseconds saved/estimated:
- Acoustic runtime cost added: 0 us.
- Adjacent AI finite-helper correction added: 0 us measured; it reuses an existing math guard call.
- Compile verification wall-clock: 79,130,000 us.

Validation:
- `dotnet build Hecton8.Core.csproj --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -v:minimal -clp:Summary`: Build succeeded, 0 warnings, 0 errors, 1m19.13s.
- Final current-disk rerun of the same command after a transient parallel Bootstrap/DataVault signature race: Build succeeded, 0 warnings, 0 errors, 3m06.93s.

Residual risk:
- Unity Editor import, Play Mode, GCMonitor, profiler capture, Quest/Android player build, Metal/Mac player build, Steam Deck storage profiling, and IL2CPP strip build were not run in this terminal pass.

## 2026-05-16 - Queue Hardening And External Wall Recheck
What was wrong:
- The acoustic runtime still exposed an unused direct `NativeQueue<EchoTap>.ParallelWriter` surface.
- If a future producer used that writer, it could bypass `TryEnqueueEchoTap` validation and `_queuedEchoTapCount` accounting.
- A fresh build after the hardening pass exposed new external compile walls outside AI/Sensory.

What was done:
- Re-ran the mandated status/rationale read and re-extracted the exact XML assignment.
- Re-read the only owned domain script: `Assets/_Project/Scripts/AI/Sensory/AcousticEchoLocationRuntime.cs`.
- Removed the unused `EchoTapWriter` API.
- Added `IsValidTap` and revalidates every dequeued echo tap before it enters the frame slab and `EchoTrackingJob`.
- Re-scanned AI/Sensory for direct writer use, forbidden managed/event/update patterns, private NativeArray allocation, and struct packing.

Cinematic Cheats used:
- No physical acoustic wavefield was added.
- Toaster path remains 64 queued taps, 32 scored taps, direct-node fake for low tier.
- High/Ultra retain the cheap sine head sweep and saved CPU budget for presentation systems.

Exact Microseconds saved/estimated:
- Removed direct writer bypass: 0 us hot-path cost.
- Added queue-drain finite validation: <1 us/frame for the 32-tap cap on i3/MX350.
- Latest failed compile validation wall-clock: 35,980,000 us.

Validation:
- `rg` found no `EchoTapWriter` or `AsParallelWriter()` remaining in AI/Sensory.
- `rg` found no private persistent `NativeArray`, `new NativeArray`, `Update`, `LateUpdate`, `FixedUpdate`, `string.Format`, legacy EventBus, or managed `Action`/`Func` in AI/Sensory.
- `rg "StructLayout"` in AI/Sensory shows all four acoustic structs use `Pack = 1`.
- `dotnet build Hecton8.Core.csproj --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -v:minimal -clp:Summary` failed outside AI/Sensory: first stable groups are `LockstepStateValidator.cs`, `EcosystemDirector.cs`, `GlobalSignals.cs`, and `TetherManager.cs`.

Integrator notes:
- Unity batchmode was not run because the C# compile gate is currently blocked before Unity import/runtime evidence can be trusted.
- The acoustic patch itself is static-clean; project validation is blocked by external current-disk edits.

## 2026-05-16 - Current-Disk Wall Shift Recheck
What was wrong:
- The previous external compile-wall report was stale after more parallel edits landed.
- First current-disk recheck hit `SubmarineFluidDynamics.cs(4925,10): error CS1513`; source readback showed balanced braces and the error disappeared on rerun, so it was treated as a transient race.
- Stable current-disk build now fails outside AI/Sensory in UI navigation, ecosystem DataVault migration, dispatcher scheduled raycasts, and tether/winch contracts.

What was done:
- Re-ran the mandatory status/rationale read and re-extracted the exact XML prompt.
- Re-read `AGENTS.md` and `Docs/Actual Domains of Project.txt`.
- Re-scanned the owned AI/Sensory directory: one `.cs`, packed structs present, no `EchoTapWriter`, no `AsParallelWriter`, no local persistent NativeArray allocation, no `Update`, no `string.Format`, no legacy EventBus/delegate patterns.
- Re-ran `dotnet build Hecton8.Core.csproj --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -v:minimal -clp:Summary`.

Cinematic Cheats used:
- Acoustic behavior unchanged: portal breadcrumb truth stays bounded to 32 scored taps.
- Low tier remains direct-node fake.
- High/Ultra remain sine head-sweep only; no physical wavefield or smell simulation was added.

Exact Microseconds saved/estimated:
- Acoustic runtime cost added in this pass: 0 us.
- Current failed build wall-clock: 220,720,000 us.
- Static scan cost only; no gameplay hot path was modified.

Validation:
- Owned-domain static scans remain clean.
- Current stable build fails with 167 errors outside AI/Sensory. First groups: `DiegeticGyroCompassRuntime.cs` missing private snapshot/calibration/blackbox fields, `EcosystemDirector.cs` missing many DataVault handle fields and alias methods, `SystemDispatcher.cs` missing scheduled raycast buffers, `TetherSignals.cs` / `TetherManager.cs` / `HeavyTowWinch.cs` tether API mismatch.

Integrator notes:
- Task 18 remains `[BLOCKED BY DEPENDENCY]`.
- Unity batchmode, Play Mode, GCMonitor, profiler, Quest/Android, Metal/Mac, and Steam Deck I/O validation remain blocked by the current C# compile wall.

## 2026-05-16 - Current-Disk Green Compile And Unity Gate Attempt
What was wrong:
- The last dependency-blocked compile report was stale against current source.
- `DiegeticGyroCompassRuntime.cs` no longer contained the `_snapshot` line reported by the previous build output.

What was done:
- Re-ran the mandatory status/rationale read.
- Re-extracted the exact `ACOUSTIC_ECHO_LOCATION_AI` XML assignment from `CURRENT_BATCH.md`.
- Re-scanned AI/Sensory for forbidden direct writer, private NativeArray, Update-family, string formatting, legacy EventBus/delegate, and packing defects.
- Re-ran `dotnet build Hecton8.Core.csproj --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -v:minimal -clp:Summary`.
- Attempted Unity 6000.4.1f1 batchmode import/compile and wrote `Docs/AgentLogs/Unity_ACOUSTIC_ECHO_LOCATION_AI.log`.

Cinematic Cheats used:
- Acoustic behavior unchanged: low tier direct-node fake, 64 queued tap cap, 32 scored taps, loudest noisemaker priority, five-second silence expiry, and high-tier sine head sweep.
- No physical acoustic wavefield or smell simulation was added.

Exact Microseconds saved/estimated:
- Acoustic runtime cost added in this pass: 0 us.
- Latest green C# compile gate wall-clock: 1,310,000 us.
- Unity batchmode abort gate wall-clock: approximately 500,000 us; no runtime validation occurred.

Validation:
- `dotnet build Hecton8.Core.csproj --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -v:minimal -clp:Summary`: Build succeeded, 0 warnings, 0 errors, 1.31s.
- `rg` found no `EchoTapWriter`, `AsParallelWriter()`, private persistent `NativeArray`, `new NativeArray`, `Update`, `LateUpdate`, `FixedUpdate`, `string.Format`, legacy EventBus, or managed `Action`/`Func` in `Assets/_Project/Scripts/AI/Sensory/`.
- `rg "StructLayout"` in AI/Sensory shows all four acoustic structs use `Pack = 1`.
- `git diff --check` on acoustic and documentation files reports repository-normal CRLF conversion warnings only.
- Unity batchmode failed before project import because another Unity instance has `C:/hades/Hecton8` open. MCP resource/template discovery returned empty lists, so the live Editor could not be queried through MCP.

Integrator notes:
- Task 18 is green for its explicit `dotnet build` requirement.
- Unity runtime, Play Mode, GCMonitor, profiler, Quest/Android, Metal/Mac, and Steam Deck I/O validation are still pending because the current open Unity Editor blocks batchmode and no MCP bridge is available.

## 2026-05-16 - Project-Root Dump Path And Unity Batchmode Green
What was wrong:
- `AcousticEchoLocationRuntime.DumpBlackBox()` resolved `Docs/AgentLogs/Dump_ACOUSTIC_ECHO_LOCATION_AI.bin` from the process working directory.
- That is not stable across Unity batchmode, open Editor launch contexts, or Steam Deck shortcuts.

What was done:
- Added `ResolveDumpPath()` to anchor the dump under `Path.GetFullPath(Path.Combine(Application.dataPath, "..", DumpRelativePath))`.
- Kept the old relative path only as a fallback when `Application.dataPath` is empty.
- Re-ran the acoustic static scans, `dotnet build`, and Unity batchmode.

Cinematic Cheats used:
- No new physical simulation.
- Low tier remains direct-node fake.
- High/Ultra remain portal breadcrumb plus cheap sine head sweep; saved CPU remains presentation budget for the consumer-side overkill systems.

Exact Microseconds saved/estimated:
- Hot-path cost added: 0 us.
- Fault-path path resolution: one-shot only after invalid AUP/NaN queue payload, not per frame.
- Latest green C# compile gate wall-clock: 80,580,000 us.
- Unity batchmode gate wall-clock: approximately 3,700,000 us.

Validation:
- `dotnet build Hecton8.Core.csproj --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -v:minimal -clp:Summary`: Build succeeded, 0 warnings, 0 errors, 1m20.58s.
- `rg` found no `EchoTapWriter`, `AsParallelWriter()`, private persistent `NativeArray`, `new NativeArray`, `Update`, `LateUpdate`, `FixedUpdate`, `string.Format`, legacy EventBus, or managed `Action`/`Func` in `Assets/_Project/Scripts/AI/Sensory/`.
- `rg "StructLayout|ResolveDumpPath|Application.dataPath|Path.GetFullPath"` confirms all four acoustic structs are `Pack = 1` and the dump path is project-root anchored.
- Unity 6000.4.1f1 batchmode log `Docs/AgentLogs/Unity_ACOUSTIC_ECHO_LOCATION_AI_RERUN.log`: process exit code 0. No `error CS`, `warning CS`, `Exception`, `Fatal`, `Aborting`, or `Compilation failed` matches. One licensing access-token update message appears, followed by successful entitlement/license initialization.

Residual risk:
- Play Mode, GCMonitor, profiler timing, Quest/Android IL2CPP, Metal/Mac, and Steam Deck I/O profiling are still not executed from this terminal pass.

## 2026-05-17 - Same-Frame Gate And Unity Asmdef Wall
What was wrong:
- The acoustic refresh path still let multiple predators in the same frame re-enter the "job still running" branch.
- Current Unity batchmode evidence contradicted the stale Unity-green status: root editor tools and audio virtualization failed under Unity asmdef compilation.

What was done:
- Re-ran the mandatory status/rationale read and re-extracted the exact XML assignment.
- Confirmed AI/Sensory static scan remains clean: no `EchoTapWriter`, no `AsParallelWriter()`, no private persistent `NativeArray`, no `new NativeArray`, no `Update`/`LateUpdate`/`FixedUpdate`, no `string.Format`, no legacy EventBus, and no managed `Action`/`Func`.
- Kept all four acoustic structs on `[StructLayout(..., Pack = 1)]`.
- Added same-frame stalled-job gating in `AcousticEchoLocationRuntime.RefreshForFrame`.
- Added `Assets/_Project/Editor/Hecton8.Project.Editor.asmdef` plus meta so root `_Project/Editor` scripts compile against the same project/editor references as the existing editor assembly.
- Added direct `Hecton8.Core.Contracts` reference to `Assets/_Project/Scripts/Audio/Virtualization/Hecton8.Audio.Virtualization.asmdef`.

Cinematic Cheats used:
- No physical acoustic wavefield, raymarch, or smell simulation was added.
- Low tier remains the direct-node fake with 64 queued taps and 32 scored taps.
- High/Ultra remain portal breadcrumb plus cheap sine head sweep; saved CPU remains available to consumer presentation systems.

Exact Microseconds saved/estimated:
- Same-frame stalled-job gate: 0 us when the job is complete; estimated 1-3 us saved per 32 predator resolver calls during a stalled acoustic job on i3/MX350.
- Unity asmdef repair: 0 us runtime, compile topology only.
- Latest green C# compile gate wall-clock: 331,190,000 us.
- Unity moved compiler wall reported by Tundra: 9,610,000 us inside `Unity_ACOUSTIC_ECHO_LOCATION_AI_ASMDEF2.log`.

Validation:
- `dotnet build Hecton8.Core.csproj --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -v:minimal -clp:Summary`: Build succeeded, 0 warnings, 0 errors, 5m31.19s.
- `Unity_ACOUSTIC_ECHO_LOCATION_AI_CURRENT.log` first failed in root editor/audio asmdef contracts.
- After the asmdef repair, `Unity_ACOUSTIC_ECHO_LOCATION_AI_ASMDEF2.log` moved the wall outside AI/Sensory to `PlayerKinematicsRuntime`, `GroundPenetratingRadarRuntime`, `ProceduralCrabLegIKRuntime`, `FoveatedRenderCommander`, and `GlobalSignals`.

Integrator notes:
- Task 18 remains green for the explicit XML `dotnet build` requirement.
- Unity batchmode, Play Mode, GCMonitor, profiler, Quest/Android, Metal/Mac, and Steam Deck I/O validation remain blocked by external Unity asmdef/runtime ownership, not by acoustic echo code.
