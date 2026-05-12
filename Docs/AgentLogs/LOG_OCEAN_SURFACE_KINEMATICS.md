# LOG: OCEAN_SURFACE_KINEMATICS

## 2026-05-12 HYDRO_MECHANIC / Crest Wave Burst Sampling

What was wrong:
- Surface floaters were still architecturally vulnerable to managed ocean-height sampling patterns. Mass debris and life pods need one cache-coherent physics path, not per-object Crest queries.
- Existing fallback wave math only used a small local set and a triangle-wave cheat; it did not expose the requested persistent floater S.O.A., octave LOD, shore terrain fallback, AUP phase stability, black-box telemetry, or sargassum presentation coupling.
- Distant debris had no dedicated 500m ocean sleep bitmask, so offscreen fields could still consume buoyancy work.

What was done:
- Reused `HectonFluidEngine` as the ownership boundary and exposed `FloaterPositions` plus `BuoyancyResults` over existing Persistent NativeArrays.
- Added persistent 16-slot `NativeArray<GerstnerWaveComponent>` and Burst `WaveQueryJob` sampling through `math.sincos`.
- Added tiered octave budget: Unknown/Mobile 1, Low/MX350 4, Mid 8, High 12, Ultra 16.
- Added MapMagic R16 terrain-height fallback using `math.max(waveSurfaceY, terrainY)` inside a 14m shore band.
- Added AUP phase stability: cached positions rebase on `IOriginShiftListener`, wave phase samples runtime XZ plus total origin offset.
- Added 500m sleep mask with 495m wake hysteresis.
- Added finite-difference wave normals for high tier and flat-normal low tier after Omega polish.
- Added surface wind advection from `WeatherRuntimeSnapshot.GlobalWindVector`.
- Added splash gating: depth >1m and velocity threshold before deferred `SplashEvent` plus `DebrisSpawnSignal`.
- Added 300-frame `OceanSurfaceTelemetryEntry` ring and nonfinite crash dump path `Docs/AgentLogs/Dump_OCEAN_SURFACE_KINEMATICS.bin`.
- Published first three Gerstner waves to shader globals and made `Hecton_IndirectVegetation` sargassum mats ride shared ocean lift without rigidbodies.
- Wrote `RECON_OCEAN_SURFACE_KINEMATICS.md`: no active `Crest.SampleHeightHelper` hits under `Assets/_Project/Scripts`.

Cinematic cheats used:
- Low/MX350 does not compute finite-difference normals. It uses a stable flat/dominant-axis path and buys back frame time.
- Height-only Gerstner sampling computes scalar lift only; unused horizontal displacement was removed from physics height queries.
- Sargassum ride motion is shader-side surface lift plus reduced local bob, not physical mat simulation.
- Storm surge is an amplitude multiplier on the synthesized Gerstner spectrum, not a new fluid solver.

Exact microseconds saved:
- 35 us estimated at 256 floaters by reusing the dense registry instead of adding a second copy.
- 55-180 us estimated at 256 floaters on MX350 by capping Low/MX350 to 4 octaves instead of 16.
- Up to 4096 Gerstner component evaluations per fixed step avoided on Low tier at 256 floaters by disabling finite-difference normals outside high tier.
- 70-220 us estimated when distant debris fields sleep past 500m.
- 12-40 us estimated by R16 MapMagic alias sampling instead of managed terrain calls.
- Millisecond-scale rigidbody overhead avoided for sargassum mats by keeping them in the shader/BRG path.

Verification:
- `dotnet build Hecton8.Core.csproj` executed. It is blocked outside this domain by `UI/PDAMapTab.cs(92)` StructLayout/LayoutKind errors; earlier external wall was `SubmarineStructuralGrid.cs(654,17)` CS1501.
- Filtered build output shows no `HectonFluidEngine.cs` errors.
- Unity MCP `validate_script` is unavailable after refresh timeout: `no_unity_session`.
- Burst job slice scan found no Transform, Vector3, Rigidbody, GameObject, Shader, Time, Application, or Debug usage inside `WaveQueryJob`/`BuoyancyJob`.
- `git diff --check` returned only CRLF normalization warnings for touched source files.

Status:
- PENDING VERIFICATION per AGENTS.md. Unity Burst compile, profiler, GCMonitor, and visual validation were not available in this session.

## 2026-05-12 HYDRO_MECHANIC / Continuation Verification Pass

What was wrong:
- The ocean culling implementation had one remaining cost leak: inactive floaters skipped final buoyancy forces, but the wave-query stage could still evaluate Gerstner waves before the skip.
- The local build report was stale after concurrent dependency cleanup and a minimal UI namespace compile-gate fix.
- High-tier exact normal alignment still used `math.normalize` instead of explicit rsqrt math.

What was done:
- Added a `WaveQueryJob` early return for `simulationMode != 0`, before Gerstner, terrain fallback, or finite-difference normal work.
- Replaced the high-tier surface normal normalization path with explicit `math.rsqrt`.
- Added `using System.Runtime.InteropServices;` to `UI/PDAMapTab.cs` only to clear the missing `StructLayout` / `LayoutKind` compile gate.
- Re-ran build and static scans.

Cinematic cheats used:
- Sleeping/staggered floaters now become a hard math cut, not just a force-output cut.
- Low/MX350 still avoids finite-difference normals; high-tier spends the precision budget only on active nearby floaters.

Exact microseconds saved:
- Per inactive floater: saves `activeWaveCount` Gerstner phase evaluations before terrain/normal work. That is 1 octave on Unknown/Mobile, 4 on Low/MX350, 8 on Mid, 12 on High, 16 on Ultra.
- At 100 sleeping Low/MX350 floaters, this removes roughly 400 Gerstner component evaluations per fixed step before any MapMagic fallback savings. Exact profiler numbers remain pending.
- `math.rsqrt` normal cleanup is a micro-optimization; no profiler number claimed.

Verification:
- `dotnet build Hecton8.Core.csproj`: PASS. `Build succeeded. 0 Warning(s) 0 Error(s)`.
- Focused Burst-job slice scan found no Transform, Vector3, Rigidbody, GameObject, Shader, Time, Application, Debug, or `math.normalize(` tokens in the `WaveQueryJob` / `BuoyancyJob` slice.
- `rg Crest.SampleHeightHelper|SampleHeightHelper Assets/_Project/Scripts`: no active hits.
- `git diff --check`: CRLF normalization warnings only.
- Unity MCP `validate_script`: failed with `no_unity_session`.
- Unity MCP `read_console`: failed with `no_unity_session`.

Status:
- PENDING VERIFICATION. C# compile is clean; Unity editor Burst compile, profiler, GCMonitor, and visual validation are still not proven.

## 2026-05-12 HYDRO_MECHANIC / Continuation Hardening Pass 2

What was wrong:
- The legacy GPU buoyancy path could activate at high object counts and bypass the authoritative Burst `WaveQueryJob`. It only used three weather waves, runtime-space phase, no MapMagic fallback, no 16-octave LOD, and no finite-difference normal parity.
- `OnOriginShift` lost the rebase when an origin shift arrived while the buoyancy job was still running.

What was done:
- Added `GpuBuoyancySurfaceParityAvailable=false` and gated GPU buoyancy dispatch/readback plus `useGpuBuoyancyForce` behind it.
- Kept `WaveQueryJob` authoritative for all object counts until GPU compute reaches 16-wave/AUP/terrain/sleep/normal parity.
- Added pending origin-shift accumulation. If a shift lands during a running job, the completed stale force batch is skipped, cached floater positions are rebased, and the next fixed step gathers fresh Rigidbody state.

Cinematic cheats used:
- Non-parity GPU acceleration is rejected. The cheap path is Burst LOD and sleep culling, not stale readback.
- During the rare AUP/job overlap, one force-application frame is discarded instead of blocking the main thread or applying stale hydrodynamic truth.

Exact microseconds saved:
- 0 us claimed for the GPU parity gate. It is a correctness gate; profiler must measure the net CPU/GPU trade.
- AUP overlap path avoids a forced job completion stall. Exact spike savings are pending profiler.
- Existing inactive-floater savings still apply: sleeping/staggered slots skip all wave octaves before terrain/normal work.

Verification:
- `dotnet build Hecton8.Core.csproj`: PASS. `Build succeeded. 0 Warning(s) 0 Error(s)`.
- Focused Burst-job slice scan found no Transform, Vector3, Rigidbody, GameObject, Shader, Time, Application, Debug, or `math.normalize(` tokens.
- `git diff --check`: CRLF normalization warnings only.
- Unity MCP `validate_script`: failed with `no_unity_session`.

Status:
- PENDING VERIFICATION. Unity editor Burst compile, profiler, GCMonitor, and visual validation are still not proven.

## 2026-05-12 HYDRO_MECHANIC / Continuation Hardening Pass 3

What was wrong:
- The AUP guard still allowed a completed-but-undrained buoyancy job to apply stale pre-shift force results after an origin shift.
- Prior math verification was focused on job slices; whole-file hot math needed a stricter scan.

What was done:
- Changed `OnOriginShift` to defer and mark stale whenever any scheduled buoyancy batch is active, regardless of `JobHandle.IsCompleted`.
- Kept the nonblocking policy: stale batch is skipped at drain, pending shift is applied, next fixed step gathers fresh Rigidbody positions.
- Re-scanned the full fluid file for forbidden normalize/sqrt/length forms.

Cinematic cheats used:
- During rare AUP overlap, physical continuity is faked by skipping one stale hydrodynamic application instead of blocking or applying wrong-space forces.

Exact microseconds saved:
- 0 us steady-state. This is a correctness guard.
- Potential shift-frame stall avoided by not forcing immediate job completion; exact spike delta pending profiler.

Verification:
- `dotnet build Hecton8.Core.csproj`: PASS. `Build succeeded. 0 Warning(s) 0 Error(s)`.
- Focused Burst-job slice scan found no Transform, Vector3, Rigidbody, GameObject, Shader, Time, Application, Debug, or `math.normalize(` tokens.
- Whole-file fluid math scan found no `math.normalize(`, `.normalized`, `Mathf.Sqrt`, `math.sqrt(`, or `math.length(`.
- `git diff --check`: CRLF normalization warnings only.
- Unity MCP `validate_script`: failed with `no_unity_session`.

Status:
- PENDING VERIFICATION. Unity editor Burst compile, profiler, GCMonitor, and visual validation are still not proven.

## 2026-05-12 HYDRO_MECHANIC / Continuation Hardening Pass 4

What was wrong:
- Sargassum ocean lift used a rough direction normalization in the shader. It was cheap, but it could drift against the C# Gerstner phase used by the buoyancy path.
- Verification language needed current-state correction. Older blocked and 0-warning build entries are historical; the latest local build succeeds with external/package warnings.

What was done:
- Changed `EvaluateOceanGerstnerLift` in `Hecton_IndirectVegetation.shader` to normalize the wave direction with `rsqrt(max(dot(direction, direction), epsilon))`.
- Re-ran the local C# build, focused Burst-job API scan, shader snippet check, and diff whitespace check.
- Updated status and rationale with the superseded build evidence instead of rewriting append-only history.

Cinematic cheats used:
- Sargassum remains a vertex presentation fake driven by shared ocean wave globals. No rigidbody mats, no CPU mat sampling, no Crest helper calls.
- Direction parity was tightened only where visible phase drift matters; full physical vegetation-water coupling remains rejected.

Exact microseconds saved:
- 0 us claimed for this pass. It is a visual parity and evidence-integrity pass.
- Prior savings remain unchanged: Low/MX350 sleeps/staggers hard before wave sampling, caps Gerstner work to 4 octaves, and avoids finite-difference normals.

Verification:
- `dotnet build Hecton8.Core.csproj`: PASS. Latest observed result: 0 errors, 47 warnings from Unity package cache / third-party package output including URP/Core RP, Crest, GPUInstancer, and ShaderGraph.
- Focused Burst-job slice scan found no Transform, Vector3, Rigidbody, GameObject, Shader, Time, Application, Debug, or `math.normalize(` tokens.
- Shader ocean lift now uses `rsqrt`.
- `git diff --check`: CRLF normalization warnings only.
- Unity MCP `validate_script` on `HectonFluidEngine.cs`: PASS, 0 diagnostics.
- Unity MCP script refresh/compile request timed out after 60 seconds waiting for editor readiness.
- Unity MCP console reads after the timeout: 0 errors, 0 warnings.

Status:
- PENDING VERIFICATION. Unity editor Burst compile, profiler, GCMonitor, and visual validation are still not proven.

## 2026-05-12 HYDRO_MECHANIC / Continuation Hardening Pass 5

What was wrong:
- The Gerstner helper still used `math.normalizesafe` in repeated wave direction and finite-difference normal paths. That hid a normalize/sqrt-style helper behind a hot ocean loop.
- Unity MCP validation is currently unstable, so runtime evidence cannot be upgraded beyond local build/static proof.

What was done:
- Replaced Gerstner direction normalization with `ResolveDirectionOrDefault`, using explicit `dot + math.rsqrt`.
- Replaced finite-difference normal cleanup with `ResolveNormalOrUp`, also using explicit `dot/lengthsq + math.rsqrt`.
- Re-ran the local build, hot-math scan, focused Burst-job token scan, active Crest helper scan, and diff whitespace check.

Cinematic cheats used:
- Low/MX350 still avoids finite-difference normals entirely; this pass only makes the active exact path cleaner.
- Sargassum remains shader-driven by the first three published wave globals, not rigidbody truth.

Exact microseconds saved:
- 0 measured microseconds claimed. This is SIMD hygiene inside the per-octave wave path.
- Expected gain is small per active wave sample; profiler on target hardware is still required before publishing a number.

Verification:
- `dotnet build Hecton8.Core.csproj`: PASS. `Build succeeded. 0 Warning(s) 0 Error(s)`.
- Whole-file hot-math scan found no `math.normalize(`, `normalizesafe`, `.normalized`, `Mathf.Sqrt`, `math.sqrt(`, or `math.length(` in `HectonFluidEngine.cs`.
- Focused Burst-job slice scan found no Transform, Vector3, Rigidbody, GameObject, Shader, Time, Application, Debug, or forbidden normalize/sqrt tokens.
- `rg Crest.SampleHeightHelper|SampleHeightHelper Assets/_Project/Scripts`: no active hits.
- `git diff --check`: CRLF normalization warnings only.
- Unity MCP `validate_script`: unavailable, reason `no_unity_session`; console reads failed because MCP ping was not answered.

Status:
- PENDING VERIFICATION. Unity editor Burst compile, profiler, GCMonitor, and visual validation are still not proven.

## 2026-05-12 HYDRO_MECHANIC / Continuation Hardening Pass 6

What was wrong:
- The sargassum wave presentation path republished six wave parameter shader globals every fixed publish, even when the wave spectrum did not change.
- Only the meta/time vector must update every pass for shader/physics phase parity.

What was done:
- Added cached `Vector4` fields for `_HectonOceanSurfaceWave0A/B`, `_HectonOceanSurfaceWave1A/B`, and `_HectonOceanSurfaceWave2A/B`.
- Replaced unconditional wave parameter publication with `SetOceanSurfaceWaveGlobalIfChanged`.
- Kept `_HectonOceanSurfaceWaveMeta` publishing every pass so the shader still uses the weather time accumulator.

Cinematic cheats used:
- Sargassum remains a shader-driven ocean-lift fake. No Rigidbody mats, no CPU mat sampling, no Crest helper.
- Redundant render-state updates are skipped instead of adding more simulation.

Exact microseconds saved:
- 0 measured microseconds claimed. Profiler proof is absent.
- Expected saving: up to six skipped `Shader.SetGlobalVector` calls per fixed publish when the wave spectrum is stable.

Verification:
- First `dotnet build Hecton8.Core.csproj` reached success text but hit the 240s shell timeout; not counted as clean exit.
- Second `dotnet build Hecton8.Core.csproj`: PASS, exit 0. `1 Warning(s) 0 Error(s)`.
- The single warning is external Crest editor code: `Packages/com.waveharmonic.crest/Editor/Scripts/Utility/Shared/Helpers.cs(240,43) CS0649`.
- Whole-file hot-math scan stayed clean for `HectonFluidEngine.cs`.
- Focused Burst-job slice scan stayed clean for Unity API and forbidden normalize/sqrt tokens.
- Unity MCP `validate_script` and console reads failed with `no_unity_session`.

Status:
- PENDING VERIFICATION. Unity editor Burst compile, profiler, GCMonitor, and visual validation are still not proven.

## 2026-05-12 HYDRO_MECHANIC / Continuation Hardening Pass 7

What was wrong:
- Sargassum ocean-lift globals were only refreshed from the floater-buffer path. With zero registered floaters, the engine could release NativeArrays and leave vegetation riding stale wave data.
- `GetWaterHeightAtPosition` still used raw three-wave weather sampling rather than the same tiered harmonic Gerstner synthesis used by Burst buoyancy.
- Ocean shader global clearing needed owner discipline because Unity globals are process-wide and duplicate fluid engines can be destroyed during singleton registration.

What was done:
- Added no-floater weather publication for `_HectonOceanSurfaceWave0/1/2` globals before idle buffer release.
- Extracted shared primary-wave sanitization, storm multiplier, harmonic expansion, and weather-height sampling helpers.
- Routed `GetWaterHeightAtPosition` through the same Math LOD wave budget as the Burst path.
- Added owner-guarded ocean-global clearing on disable/destroy.

Cinematic cheats used:
- Sargassum remains a vertex shader fake, not a rigidbody fleet.
- No-floater scenes publish only the first three visible wave globals; full 16-octave work remains a physics/query budget choice, not vegetation CPU truth.

Exact microseconds saved:
- 0 measured microseconds claimed.
- Avoided future CPU failure mode: no need to keep buoyancy NativeArrays or rigidbody floaters alive just so sargassum can move.

Verification:
- `dotnet build Hecton8.Core.csproj -v:minimal`: PASS, exit 0. `47 Warning(s) 0 Error(s)`.
- Warnings are external/package/editor output: URP/Core RP, Crest, GPUInstancer, ShaderGraph, WaveHarmonic Crest.
- Static hot-math scan found no `math.normalize(`, `normalizesafe`, `.normalized`, `Mathf.Sqrt`, `math.sqrt(`, or `math.length(` in `HectonFluidEngine.cs`.
- Focused WaveQueryJob/BuoyancyJob slice scan found no Transform, Vector3, Rigidbody, GameObject, Shader, Time, Application, Debug, or forbidden normalize/sqrt tokens.
- `rg Crest.SampleHeightHelper|SampleHeightHelper Assets/_Project/Scripts`: no active hits.
- `git diff --check`: CRLF normalization warnings only.
- Unity MCP `validate_script` and console reads failed with HTTP request failure to `127.0.0.1:8088/mcp`.

Status:
- PENDING VERIFICATION. Unity editor Burst compile, profiler, GCMonitor, and visual validation are still not proven.
