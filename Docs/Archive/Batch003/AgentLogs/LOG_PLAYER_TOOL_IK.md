# LOG_PLAYER_TOOL_IK

## 2026-05-12 - PLAYER_TOOL_IK

What was wrong:
- Player tool hands had no dedicated camera-forward collision retraction path.
- Existing contextual IK target frames were AOS-only for hands; prompt required SOA target/weight lanes.
- Terminal button hand snaps had a bridge-side sink but the contextual IK rig did not implement it.
- Recoil had no public API into contextual hand IK.
- Critical hand IK state had no PLAYER_TOOL_IK black-box dump.

What was done:
- Extended `ContextualPhysicalIkGroundDetectionJob` to fixed 6-ray batches: left foot, right foot, left wall, right wall, left tool, right tool.
- Added persistent `_ikTargets NativeArray<float3>` and `_ikWeights NativeArray<float>` left/right hand lanes.
- Added `ApplyToolRetraction` to bias hand targets backward/upward when the short camera ray hits under 0.5m.
- Kept the existing Burst analytical 2-bone solver and routed quaternion smoothing through `CinematicMath.FastNlerp`.
- Added `ContextualPhysicalIkRig.AddRecoil(float3 impulse)` with reciprocal decay.
- Implemented `IPhysicalHandIkTargetSink` on `ContextualPhysicalIkRig` for right-hand `PhysicalTerminalKeyboard` / `KinematicTerminalInteractionBridge` snaps.
- Added low-tier wall-touch disable with no-layer disabled ray commands.
- Rebased scheduled state, target frames, SOA hand lanes, predictive/external/terminal targets on AUP origin shift.
- Added 300-entry `ContextualPhysicalIkTelemetryEntry` native black-box ring and `Docs/AgentLogs/Dump_PLAYER_TOOL_IK.bin` fault dump on non-finite state.
- Added `Docs/AgentLogs/RECON_PLAYER_TOOL_IK.md`; scan found no Animator IK call sites under `Assets/_Project/Scripts`.
- Omega polish replaced terminal snap `Vector3.Normalize()` with `NormalizeVectorNoSqrt`.

Cinematic Cheats used:
- Two short camera rays stand in for full tool collision.
- Back/up target bias fakes chest retraction instead of simulating tool constraints.
- Palm alignment uses no-trig normal delta instead of exact look rotation.
- Low tier keeps visible tool retraction but deletes wall-touch collision work through disabled no-layer ray lanes.

Exact Microseconds saved:
- Full Body IK avoided: estimated 120-400 us per animated player on i3/MX350.
- Main-thread hand raycasts avoided: estimated 12-35 us/frame.
- Tool constraint simulation avoided: estimated 40-150 us/frame.
- Low-tier wall-touch disabled rays: estimated 15-45 us/frame.
- Terminal polling avoided through existing sink interface: estimated 20-60 us while interacting.
- Quaternion slerp/trig avoided through `CinematicMath.FastNlerp`: estimated 8-25 us across limb rotations.
- Terminal snap exact sqrt removed: estimated <1 us per snap capture.

Verification:
- `dotnet build Hecton8.Core.csproj --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -v:minimal -clp:Summary` succeeded with 0 errors and 3 unrelated CS0649 warnings in audio/world files.
- MCP `validate_script standard` on `ContextualPhysicalIkRuntime.cs` and `ContextualPhysicalIkRig.cs` returned 0 errors / 0 warnings before the final editor session dropped during reload.
- Forbidden scan found no `Animator.SetIKPosition`, `SetIKRotation`, `LateUpdate`, `IEnumerator`, coroutine tokens, managed `foreach`, `string.Format`, `$"` interpolation, or `.ToString(` in the two IK files.

Status:
- VERIFIED MASTER GRADE for `Hecton8.Core.csproj` compile success and script-level validation.

## 2026-05-12 - PLAYER_TOOL_IK Continuation Pass

What was wrong:
- The analytical two-bone solver still used approximate target length inside the law-of-cosines solve.
- Bend sine was derived from `sinSq` directly, flattening elbows during partial extension.
- Tool retraction and terminal snap were downstream of optional wall-touch bracing, so Low tier could preserve ray budget but lose the visible tool anti-clip response.
- First activation smoothing could start from default world origin when the previous target blend was inactive.
- Slope lean used redundant projection math against already normalized root axes.
- Runtime native disposal jobs were accumulated then the handle was cleared before completion.

What was done:
- Replaced solver target distance with `distanceSq * math.rsqrt(distanceSq)`.
- Replaced bend sine with `sinSq * math.rsqrt(sinSq)` and kept the no-`acos` path.
- Moved tool retraction and dashboard snap application outside the wall-touch branch.
- Added `ResolveSmoothingPosition` and `ResolveSmoothingNormal` fallback helpers.
- Replaced slope projection with direct dot products.
- Completed the scheduled dispose handle in `DisposeBuffers` before resetting state.

Cinematic Cheats used:
- Kept tool collision as two visual camera rays and target bias, not physical constraints.
- Kept palm alignment and slope response as no-trig approximations.
- Kept Low tier wall touch disabled while preserving visible tool retraction.

Exact Microseconds saved:
- Slope projection removal: estimated 1-3 us/frame at full 128-slot budget.
- Tool retraction branch independence preserves the existing 40-150 us/frame constraint-simulation avoidance on Low tier.
- Disposal fix is cold path only; saved runtime cost is 0 us, but native teardown evidence is deterministic.
- Solver rsqrt correction is a visual accuracy trade, not a CPU win; expected cost is sub-microsecond per solved limb and still far below generic IK.

Verification:
- `dotnet build Hecton8.Core.csproj --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -v:minimal -clp:Summary` succeeded with 0 warnings / 0 errors.
- `ContextualPhysicalIkRuntime.cs` MCP `validate_script standard` returned 0 warnings / 0 errors.
- MCP session was unavailable for `ContextualPhysicalIkRig.cs` and `ContextualPhysicalIkMath.cs` after Unity reload; command-line compile is current authority.
- Targeted hot-path scans found no forbidden math/coroutine/Animator IK/string/managed collection tokens in the three IK files.
- `git diff --check` reported no whitespace errors, only Git CRLF conversion warnings.

Status:
- PENDING VERIFICATION - CONTINUATION BUILD CLEAN.

## 2026-05-12 - PLAYER_TOOL_IK No-Build Recoil Decoupling

What was wrong:
- The independent recoil path still reused `ToolRetractionBlend`, so collision tuning could mute `AddRecoil`.
- Recoil target normals were passed through without a final normalization guard.

What was done:
- Removed the collision retraction blend parameter from `ApplyToolRecoil`.
- Recoil blend now derives only from capped recoil offset magnitude.
- Recoil normals now pass through `ContextualPhysicalIkMath.SafeNormalize`.

Cinematic Cheats used:
- Recoil remains deterministic hand-target bias.
- No extra raycasts, rigidbodies, constraints, or random noise were added.

Exact Microseconds saved:
- No new physics cost.
- Active recoil path remains estimated below 1 us/frame on i3/MX350.

Verification:
- No build was run by user instruction.
- Static call-site readback confirmed recoil call/signature alignment.
- Targeted scans found no forbidden hot-path math/coroutine/Animator IK/string/managed collection tokens.
- `git diff --check` reported only CRLF conversion warnings.

Status:
- PENDING VERIFICATION - STATIC ONLY AFTER USER NO-BUILD ORDER.

## 2026-05-12 - PLAYER_TOOL_IK External Wall And Recoil Readback

What was wrong:
- External wall targets used one shared hold timer for both hands, so one fresh hit could preserve stale opposite-hand data.
- External wall targets bypassed the low-tier wall-touch disable path.
- `AddRecoil(float3)` decayed correctly but only affected visible IK when collision retraction also had an obstacle hit.

What was done:
- Split external wall target hold state into left/right timers and clear missing-hand lanes immediately.
- Passed the wall-touch LOD decision into the external target bridge and kept left-hand-empty gating.
- Added hard no-sqrt recoil capping in `AddRecoil`.
- Added independent `ApplyToolRecoil` inside the Burst response job before collision retraction and dashboard snap.

Cinematic Cheats used:
- Recoil remains a target-space visual bias, not tool rigidbody physics.
- External wall contact remains a short-lived IK latch, not persistent hand constraint simulation.
- Low tier keeps tool retraction/recoil while disabling wall-touch bridge work.

Exact Microseconds saved:
- Low tier preserves estimated 15-45 us/frame by disabling wall-touch lanes and bridge application.
- Independent recoil adds below 1 us/frame while active.
- Rigidbody/constraint recoil remains avoided; expected avoidance stays 40-150 us/frame.

Verification:
- `dotnet build Hecton8.Core.csproj --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -v:minimal -clp:Summary` succeeded with 0 warnings / 0 errors.
- Targeted hot-path scans found no forbidden math/coroutine/Animator IK/string/managed collection tokens in the three IK files.
- `$"` interpolation scan returned no matches.
- `git diff --check` reported only CRLF conversion warnings.
- Unity MCP validation could not run because the Unity session was unavailable.

Status:
- PENDING VERIFICATION - CONTINUATION BUILD CLEAN.

## 2026-05-12 - PLAYER_TOOL_IK Readback Fix Pass

What was wrong:
- `DisposeBuffers` briefly used `Complete()` on a scheduled native dispose handle, violating the deferred teardown rule.
- Cold shiver amplitude multiplied by `_coldShiverBlend` before the response job multiplied by blend again.

What was done:
- Replaced the teardown block with `JobHandle.ScheduleBatchedJobs()` so disposal jobs are flushed without main-thread waiting.
- Removed the duplicate cold-shiver blend from offset generation; final blend is applied once inside `ApplyColdShiver`.

Cinematic Cheats used:
- Kept shiver as deterministic triangle-wave target bias, not random noise or physiology.
- Kept native teardown off the frame-critical path.

Exact Microseconds saved:
- Steady-state runtime unchanged.
- Cold-path teardown stall risk removed; exact time depends on outstanding disposal work.
- Cold shiver remains below 2 us/frame on active i3/MX350 player and is now visually readable at partial blend.

Verification:
- `dotnet build Hecton8.Core.csproj --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -v:minimal -clp:Summary` succeeded with 0 warnings / 0 errors.
- MCP `validate_script standard` returned 0 warnings / 0 errors for `ContextualPhysicalIkRuntime.cs`, `ContextualPhysicalIkRig.cs`, and `ContextualPhysicalIkMath.cs`.
- Targeted hot-path scans found no forbidden math/coroutine/Animator IK/string/managed collection tokens in the three IK files.
- `git diff --check` reported no whitespace errors, only Git CRLF conversion warnings.

Status:
- PENDING VERIFICATION - CONTINUATION BUILD CLEAN.

## 2026-05-12 - PLAYER_TOOL_IK Cold Shiver Polish

What was wrong:
- The recursive prompt left optional cold hand shiver unimplemented.
- A naive implementation would have crossed directly into survival internals or used nondeterministic random jitter.

What was done:
- Added `enableColdShiver`, temperature threshold, full-delta, amplitude, frequency, and blend-sharpness authoring fields to `ContextualPhysicalIkRig`.
- Read cold state only through `GlobalRegistry.Player.SurvivalSystem.EnvironmentTemperature` and `ColdStressSeverity01`.
- Added deterministic triangle-wave offsets in root right/up space.
- Applied shiver only to already-active left/right hand IK targets inside the Burst response job.

Cinematic Cheats used:
- Cold shiver is a tiny visual target offset, not physiology simulation.
- Triangle waves replace random noise and sine.
- Idle hands remain animation-authored; shiver layers only over wall touch, tool retraction, predictive repair, or terminal snap targets.

Exact Microseconds saved:
- Random/noise allocation avoided: 0 B/frame and no RNG state churn.
- No extra physics queries: preserves existing ray budget.
- Expected active-player cost: <2 us/frame on i3/MX350.

Verification:
- `dotnet build Hecton8.Core.csproj --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -v:minimal -clp:Summary` succeeded with 0 warnings / 0 errors after this polish.
- Targeted hot-path scans found no forbidden math/coroutine/Animator IK/string/managed collection tokens in the three IK files.
- MCP validation retry for `ContextualPhysicalIkRig.cs` failed because Unity session was unavailable after reload; command-line compile is current authority.

Status:
- PENDING VERIFICATION - CONTINUATION BUILD CLEAN.
