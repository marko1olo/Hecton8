# LOG_SUBMARINE_DAMAGE_CONTROL

## 2026-05-12 - SUBMARINE_DAMAGE_CONTROL

STATUS: PENDING VERIFICATION

What was wrong:
- Submarine breach leaks were specified as a ParticleSystem GameObject avalanche risk.
- Repair sparks were still tool-local ParticleSystem behavior.
- Breach severity did not have one packed source of truth for repair, leak VFX, audio, and sinking mass.

What was done:
- Added `ISubmarineDamageControlTarget` and implemented it on `SubmarineStructuralGrid`.
- Added `NativeArray<float4> _breaches` capped at 64; xyz remains submarine-local, w is severity.
- Added `BreachRepairJob` with Burst and `math.distancesq` repair radius checks.
- Added swap-last compaction for `w <= 0` breach removal; no `Array.Copy`.
- Added `Hecton_LeakPlume.compute`; one 64-thread dispatch expands packed breaches into GPU plume points.
- Added low-tier visible plume cap of 8 while CPU repair/flood logic still processes 64.
- Routed breach spray/scorch feedback through `AbyssalFluidDecalManager.RegisterPressureSpray`; no Decal GameObjects for this path.
- Routed repair sparks through `DebrisSpawnSignal` using the existing repair-spark species hash.
- Routed active leak audio through cadence-limited `ImpactSignal` at submarine AUP.
- Added critical breach VWS warning when peak breach severity exceeds 0.9.
- Added `SubmarineFluidDynamics.ApplyDamageControlLeakMass` and bounded damage-control leak mass coupling into Rigidbody mass.
- Added 300-frame damage-control black-box telemetry and `Docs/AgentLogs/Dump_SUBMARINE_DAMAGE_CONTROL.bin` dump path for invalid breach state.
- Logged prefab reconnaissance in `Docs/AgentLogs/RECON_SUBMARINE_DAMAGE_CONTROL.md`.

Cinematic cheats used:
- No visible cabin water simulation; sinking mass and existing screen-space overlay carry the consequence.
- GPU plume points are a four-point-per-breach visual fake, not fluid simulation.
- Screen-space fluid spray is used for scorch/leak marks, not DecalProjector GameObjects.
- Visual direction normalization was replaced with `ResolveSafeDirection` reciprocal-square-root fallback.

Exact microseconds saved:
- ParticleSystem leak prefab replacement: estimated 300-900 us saved on i3/MX350 during multi-breach frames, unmeasured due compile wall.
- Repair spark ParticleSystem playback removal: estimated 20-80 us saved per active repair frame, plus 0 B hot-path allocation target.
- Low-tier visible cap 64 -> 8: estimated 70-180 us GPU-side plume/scatter budget saved on MX350 depending on downstream renderer.
- Swap-last compaction over 64 entries: estimated <5 us versus managed list/copy churn; deterministic bounded loop.

Verification:
- `dotnet build Hecton8.Core.csproj --no-restore -m:2 /nr:false` run twice.
- Build remains blocked by unrelated project dependency errors: missing `SuitStats`, missing `SuitUpgrades`, missing `PlayerKinematicsHandTarget`, and existing interface/ambiguous audio issues from outside this prompt.
- `dotnet build-server shutdown` executed after build attempts.
- Scoped scan found no `Array.Copy`, coroutine, `math.sqrt`, `math.normalize`, or `.normalized` in the new damage-control paths after polish.

Integrator note:
- Do not mark VERIFIED. Compile wall is outside SUBMARINE_DAMAGE_CONTROL domain, but final runtime verification still depends on the global project compiling.

## 2026-05-12 - CONTINUATION HARDENING

STATUS: PENDING VERIFICATION

What was wrong:
- Breach add events could theoretically touch `_breaches` while `BreachRepairJob` still owned it.
- Empty leak state could continue dispatching the compute plume kernel after buffers were already clean.
- `Hecton_LeakPlume.compute` used sine hash for visual wobble despite the MX350 compute mandate preferring integer/triangle cheats.
- `RepairTool` submarine interface lookup scratch capacity was small enough to risk first-hit List growth on dense prefabs.

What was done:
- Added a fixed deferred breach lane and flushed it only after the repair job is finished.
- Added in-flight telemetry flagging that does not read the NativeArray while the job is active.
- Skips unchanged empty plume dispatches after one dirty-frame clear.
- Replaced sine hash with integer avalanche hash and triangle-wave plume offsets.
- Increased repair-tool submarine lookup scratch capacity from 4 to 16.

Cinematic cheats used:
- Leak motion remains four synthetic GPU points per breach, not droplets.
- Empty-plume idle path keeps global visible count at 0 instead of simulating absence.

Exact microseconds saved:
- Empty unchanged leak state: one compute dispatch skipped per post-fixed tick after clear; exact gain unmeasured.
- Shader sine removal: small ALU reduction per visible plume dispatch; exact gain unmeasured.
- Deferred breach lane: avoids safety stalls/race fixes without forcing a main-thread job completion.

Verification:
- Unity MCP `validate_script` attempted for touched scripts but failed with `no_unity_session`.
- Static scan found no new `Array.Copy`, coroutine, `math.sqrt`, `math.normalize`, `.normalized`, or shader `sin(` in the touched damage-control paths.
- `dotnet build Hecton8.Core.csproj --no-restore -m:2 /nr:false` still fails outside this domain in `World/AcousticOcclusionUtility.cs` for missing `AcousticSurfaceResponse`, `NativeArray<>`, and `JobHandle`.
- `dotnet build-server shutdown` executed.

Integrator note:
- Do not mark VERIFIED. The current compile wall is external to SUBMARINE_DAMAGE_CONTROL, and Unity import/console verification is still unavailable.

## 2026-05-12 - GPU RENDER BRIDGE AND CLEAN CSHARP BUILD

STATUS: PENDING VERIFICATION

What was wrong:
- `_LeakPlumeParticleBuffer` was populated by compute but no render path consumed it.
- The visible leak plume shader only handled conventional mesh plumes.
- The previous compile wall cleared after parallel work, so task 15 needed a fresh build check.

What was done:
- Added a procedural `_UseLeakParticleBuffer` branch to `Hecton_LeakPlume.shader`.
- `SubmarineStructuralGrid` now registers `ILateFrameTickable` and draws plume particles with one `Graphics.RenderPrimitives` call.
- The draw path uses a cached `MaterialPropertyBlock`, no CPU readback, no per-leak transforms, no GameObjects, shadows off, light probes off.
- Existing ambient leak plume meshes keep the default non-buffer shader path.

Cinematic cheats used:
- Four billboard points per visible breach carry the leak effect.
- Low tier still caps visibility to 8 breaches while logic processes all 64.
- No droplet physics, no cabin fluid mesh, no CPU particle state readback.

Exact microseconds saved:
- Render bridge replaces any future per-breach quad/GameObject path with one procedural draw; exact savings require Unity profiler.
- Low tier draw count is 32 quads instead of 256; exact GPU gain requires frame capture.

Verification:
- Static scan found no `Array.Copy`, coroutine, `math.sqrt`, `math.normalize`, `.normalized`, or shader `sin(` in touched damage-control paths.
- `git diff --check` passed for touched damage-control files.
- `dotnet build Hecton8.Core.csproj --no-restore -m:2 /nr:false` succeeded with 0 warnings and 0 errors.
- `dotnet build-server shutdown` executed cleanly.
- Unity MCP `validate_script` and console read still failed with `no_unity_session`; shader import and runtime visual capture are not verified.

Integrator note:
- C# compile is clean. Do not mark runtime VERIFIED until Unity import, shader console, Play Mode, and profiler/GC evidence are captured.

## 2026-05-12 - PROCEDURAL PLUME HARDENING AND PARTIAL UNITY VALIDATION

STATUS: PENDING VERIFICATION

What was wrong:
- The procedural plume shader path multiplied tint/alpha by `input.color`, but `Graphics.RenderPrimitives` does not provide a reliable mesh color stream. Active leaks could draw as fully transparent.
- `FindKernel("CSSpawnLeakParticles")` could throw if Unity failed to import the compute kernel.
- Unity validation evidence was mixed and needed to be separated from actual runtime proof.

What was done:
- Procedural leak-plume branch now uses constant white RGB and breach severity as alpha before the material tint/mask path.
- Compute bootstrap now calls `HasKernel("CSSpawnLeakParticles")` before `FindKernel`.
- Re-ran scoped static scans and generated C# build.
- Used Unity MCP while available to validate `SubmarineStructuralGrid.cs`, inspect shader/compute/material imports, and read console state.

Cinematic cheats used:
- Leak visuals remain four procedural billboard points per visible breach.
- No CPU particle readback, no mesh rebuild, no per-leak GameObject, no cabin water volume.

Exact microseconds saved:
- Avoided CPU mesh/color-stream workaround: estimated 20-120 us per active leak frame depending on breach count; unmeasured.
- `HasKernel` guard adds one cold import-time check and prevents exception-path churn; no hot-frame cost after cached kernel index.

Verification:
- `dotnet build Hecton8.Core.csproj --no-restore -m:2 /nr:false` succeeded with 0 warnings and 0 errors.
- Unity MCP `validate_script` for `SubmarineStructuralGrid.cs`: 0 warnings, 0 errors.
- Unity MCP `validate_script` for `RepairTool.cs`: 1 generic warning, 0 errors.
- Unity MCP `validate_script` for `SubmarineFluidDynamics.cs`: MCP regex timeout, not a script diagnostic.
- Unity MCP asset checks found `Hecton_LeakPlume.compute`, `Hecton_LeakPlume.shader`, and `Mat_LeakPlume.mat`.
- Unity MCP material readback confirmed `Mat_LeakPlume` uses `HECTON/VFX/LeakPlume`.
- Unity console read, before the session dropped again, reported unrelated `PDAMapTab.cs` errors and no `Hecton_LeakPlume` errors.

Integrator note:
- Do not mark VERIFIED. Runtime Play Mode, visual capture, profiler frame time, and GC proof are still absent.

## 2026-05-12 - BLACK BOX TELEMETRY FOOTPRINT HARDENING

STATUS: PENDING VERIFICATION

What was wrong:
- The damage-control telemetry ring was wider than required for the 300-frame black-box mandate.
- The normal parallel build lane was overloaded by unrelated MSBuild workers from concurrent sessions.

What was done:
- Packed `DamageControlTelemetryEntry` to an explicit 32-byte layout.
- Converted active/visible breach counts from `int` to `ushort`, valid for the fixed 64-breach cap.
- Preserved first breach local position, severity sum, frame, flags, and state hash in the telemetry dump.
- Re-ran C# compile as a single-node build with shared compilation disabled.

Cinematic cheats used:
- No change to gameplay visuals; this is postmortem telemetry hygiene.

Exact microseconds saved:
- Runtime frame-time delta is below useful standalone measurement.
- Native telemetry footprint is now 9.6 KB for 300 entries before allocator overhead instead of a wider record.

Verification:
- `git diff --check` passed for scoped damage-control files; line-ending normalization warnings only.
- Static scan found no `Array.Copy`, coroutine, `math.sqrt`, `math.normalize`, `.normalized`, `FindObjectOfType`, `GameObject.Find`, or `Camera.main` in the touched damage-control scripts.
- Static shader scan found no `sin(`, `cos(`, `pow(`, or `sqrt(` in `Hecton_LeakPlume.compute` / shader.
- `dotnet build Hecton8.Core.csproj --no-restore -m:1 /nr:false /p:UseSharedCompilation=false /p:BuildInParallel=false -v:minimal` succeeded with 0 errors.
- The 5 compile warnings are external package warnings from Unity ShaderGraph/Crest, not the touched damage-control files.
- Unity MCP console read still reports `no_unity_session` after reconnect loss.

Integrator note:
- Do not mark VERIFIED. Runtime Play Mode, shader visual capture, profiler frame time, and GC proof are still absent.

## 2026-05-12 - LEAK MASS CAP HARDENING AND FULL REBUILD

STATUS: PENDING VERIFICATION

What was wrong:
- `ApplyDamageControlLeakMass` had a zero-capacity fallback that could let damage-control ballast grow without a predictable upper bound.
- A narrow `--no-dependencies` build after the fix failed on missing Temp metadata outputs, which was not a C# diagnostic.

What was done:
- Bounded damage-control leak mass by exterior displacement water mass.
- Added a dry-rigidbody-mass fallback only when displacement is unavailable.
- Re-ran scoped static scans and a full single-node C# build.

Cinematic cheats used:
- Flooding consequence remains scalar mass only.
- No cabin water volume, no visual fluid simulation, no per-compartment mesh.

Exact microseconds saved:
- Avoided any new interior fluid simulation path; projected saved budget remains simulation-dependent and unmeasured.
- The added scalar clamp cost is below useful standalone measurement on i3/MX350.

Verification:
- `dotnet build Hecton8.Core.csproj --no-restore -m:1 /nr:false /p:UseSharedCompilation=false /p:BuildInParallel=false -v:minimal` succeeded in 00:02:31.81 with 0 errors.
- 47 warnings are from external URP/GPUInstancer/Crest/ShaderGraph package projects, not touched damage-control files.
- `git diff --check` passed for scoped damage-control files; line-ending normalization warnings only.
- Static scan found no `Array.Copy`, coroutine, `math.sqrt`, `math.normalize`, `.normalized`, `FindObjectOfType`, `GameObject.Find`, or `Camera.main` in touched damage-control scripts.
- Static shader scan found no `sin(`, `cos(`, `pow(`, or `sqrt(` in `Hecton_LeakPlume.compute` / shader.
- Unity MCP console read failed at the transport layer against `127.0.0.1:8088`; no fresh Unity console/runtime proof was available.

Integrator note:
- Do not mark VERIFIED. Runtime Play Mode, shader visual capture, profiler frame time, and GC proof are still absent.
