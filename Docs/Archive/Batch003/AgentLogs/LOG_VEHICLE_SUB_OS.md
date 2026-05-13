# LOG_VEHICLE_SUB_OS

## 2026-05-12 - Diegetic Cockpit & Holographic Radar
Status: `PENDING VERIFICATION`

What was wrong:
- Submarine cockpit had no source-level diegetic runtime for analytical panel controls, off-screen cockpit screens, voltage brownout, external feed RT ownership, or GPU sonar point-cloud rendering.
- Acoustic sonar taps existed inside the audio renderer but were not exposed through a safe public read-only seam.
- Radar shader did not have a procedural `GraphicsBuffer` path for `Graphics.RenderMeshIndirect`.
- Project compile is already blocked outside this domain by `SuitUpgradeManager`, `GameBootstrapper`, and `SpatialAudioManager` errors.

What was done:
- Added `VehicleSubOsCockpitRuntime.cs` with dispatcher-managed `IUpdatable`, `ILateFrameTickable`, and `IRenderable` lanes.
- Added analytical ray-plane cockpit button hit testing, fixed `NativeArray<byte>` button states, Burst kinematic button interpolation, and a dashboard matrix `GraphicsBuffer`.
- Added `Hecton_CockpitHoloRadar.compute` for sonar tap to local blip conversion.
- Extended `Hecton_RadarBlipInstanced.shader` with a procedural `StructuredBuffer` + `SV_InstanceID` path while retaining legacy instancing.
- Promoted `SonarEchoTap` to a public 64-byte struct and added `TryGetCockpitSonarEchoTaps` to `PlayerCriticalProceduralAudioRenderer`.
- Added off-screen UI camera RT binding, zero-GC TMP text writes, voltage brownout, external camera RT pooling, Low/MX350 static-feed fallback, AUP-safe local radar transforms, and 300-frame blackbox telemetry.
- Logged prefab reconnaissance in `RECON_VEHICLE_SUB_OS.md`.

Cinematic Cheats used:
- Sonar hologram uses delay/pan/attenuation mapping instead of full physical sonar reconstruction.
- Damage response is a scalar chromatic/flicker-style blip attenuation in compute, not a physical display simulation.
- Low/MX350 external camera is a static noise texture instead of a live camera feed.
- Button interaction uses panel-local ray-plane math, not physics colliders.

Exact Microseconds saved:
- Analytical cockpit button hit vs `Physics.Raycast`: estimated `8-25 us/interaction`.
- Idle button job polish gate: estimated `15-45 us/frame` saved when no button is moving.
- Indirect radar draw vs managed instancing arrays: estimated `20-70 us/frame`.
- Zero-GC screen text vs `.text`/formatting: `32-160 B/frame` managed allocation eliminated; CPU estimate `5-20 us/frame`.
- Live external camera off/pool path: estimated `100-400 us/frame` and about `1.3 MB` VRAM saved at 768x432 ARGB32.
- Low/MX350 radar cap `512` vs `4096`: `87.5%` radar capacity reduction.

Final Git Diff:
- Modified `Assets/_Project/Scripts/Audio/PlayerCriticalProceduralAudioRenderer.cs` for public `SonarEchoTap` and read-only tap seam. File already had unrelated working-tree edits; only the seam is attributed to this agent.
- Modified `Assets/_Project/Art/Shaders/Hecton_RadarBlipInstanced.shader` for procedural indirect blips.
- Added `Assets/_Project/Scripts/UI/VehicleSubOsCockpitRuntime.cs` and `.meta`.
- Added `Assets/_Project/Art/Shaders/Hecton_CockpitHoloRadar.compute` and `.meta`.
- Added `Docs/AgentLogs/RECON_VEHICLE_SUB_OS.md`.
- Updated `Docs/Tasks/Status_VEHICLE_SUB_OS.md` and `Docs/AgentLogs/Rationale_VEHICLE_SUB_OS.md`.

Verification:
- `VehicleSubOsCockpitRuntime.cs` validates with `0 errors, 0 warnings`.
- Unity refresh requested script compilation. Console errors are unrelated to the new UX files.
- `dotnet build Hecton8.Core.csproj` fails in unrelated non-UX files: missing `SuitStats/SuitUpgrades`, ambiguous `AudioEvent`, and `IAudioService.QueueAudioEvent` implementation errors. Build status remains `BLOCKED BY DEPENDENCY`.

## 2026-05-12 - Continuation Hardening Pass
Status: `PENDING VERIFICATION`

What was wrong:
- Cockpit tick still had a hard early-out on radar resource readiness. That made optional radar compute/material failure capable of disabling screen updates, physical button jobs, power brownout, external feed state, and blackbox telemetry.
- Radar capacity tiers were technically allocated but visually underused. The compute dispatched one point per audio tap, while the audio seam intentionally publishes only a compact 16-tap DSP snapshot.
- The radar shader had drifted toward alpha-test/cutout behavior, which is wrong for a diegetic hologram and reads as sparse dead pixels.
- Current whole-project compile is blocked outside this domain by `HectonCelestialEngine.cs(430,119)` missing `IWeatherEventListener.OnWeatherEvent(in WeatherEventPayload)`.

What was done:
- Removed the cockpit-wide `_resourcesReady` tick bailout and made resource readiness radar-only.
- Added tiered GPU visual expansion: Low/MX350 `32` points per tap, Mid `128`, High/Ultra `256`, clamped to `512/2048/4096`.
- Added `_OutputPointCount` to `Hecton_CockpitHoloRadar.compute`; the CPU still uploads only the compact sonar taps, while the GPU scatters deterministic replica points.
- Restored additive transparent output in `Hecton_RadarBlipInstanced.shader`.
- Gated `MaterialPropertyBlock.GetPropertyBlock` behind texture/power/feed state changes, used `math.rcp` in panel/button math, and forced button matrix reupload after graphics-buffer rebuild.

Cinematic Cheats used:
- Radar visual density is deterministic GPU scatter around DSP taps, not extra acoustic truth.
- Replica jitter uses cheap hash/spiral offsets to fake volume and motion without simulating propagation.
- Additive hologram blending buys readability with bounded tiered point counts.

Exact Microseconds saved:
- MPB state-change gate: estimated `2-8 us/frame` on stable cockpit screen frames.
- Radar visual expansion keeps CPU upload at the compact tap count; avoids a CPU point synthesis path estimated `25-90 us/ping` at 512-4096 points.
- Optional-resource isolation saves failure recovery time, not frame time: cockpit screens/buttons/telemetry stay alive when radar VFX is absent.
- Low/MX350 stays at `512` max points; High/Ultra reaches `4096` points without more managed allocations.

Verification:
- `VehicleSubOsCockpitRuntime.cs`: `0 errors, 0 warnings` via Unity MCP validation.
- `PDAMapTab.cs`: `0 errors, 0 warnings` via Unity MCP validation.
- Forbidden-pattern scan on cockpit files found no `Physics.Raycast`, `GraphicRaycaster`, `Canvas`, `.text =`, `.ToString()`, `string.Format`, `foreach`, `SetData`, or `GetData`.
- Unity console has no cockpit/radar/PDA map errors after refresh. Remaining entries are one `HectonCelestialEngine` C# error and two unrelated `Hecton_MarineSnow.compute` warnings.
- `dotnet build Hecton8.Core.csproj --no-restore -m:2 /nr:false -v:minimal -clp:Summary` is still `BLOCKED BY DEPENDENCY` on the non-UX celestial interface error.

## 2026-05-12 - Loop 7 Visual Fidelity and Compile-Seam Repair
Status: `PENDING VERIFICATION`

What was wrong:
- Radar indirect quads were anchored in radar-local XY, which risks edge-on disappearance in cockpit camera angles.
- Radar compute used `sin/cos` per replicated blip, wasting MX350 SFU budget for a visual fake.
- Runtime radar quad mesh creation still used anonymous cold arrays.
- Blackbox dump order was raw circular slot order, not chronological last-frame order.
- Current project compile exposed missing UI-domain PDA point-cloud helper methods.

What was done:
- `Hecton_RadarBlipInstanced.shader` now billboards procedural radar quads from `UNITY_MATRIX_I_V` camera axes.
- `Hecton_CockpitHoloRadar.compute` now uses wrapped polynomial sine/cosine approximations.
- `VehicleSubOsCockpitRuntime.cs` now uses static cold-alloc quad arrays, uploads mesh data as non-readable, completes button jobs on disable, and dumps blackbox entries in chronological order with an explicit entry count.
- `PDAMapTab.cs` now has `TryResolvePointCloudFrame`, `DispatchSonarPointCloud`, and `IsLowMathTier`; PDA sonar raymarch dispatch writes append counts into indirect args through `GraphicsBuffer.CopyCount`.

Cinematic Cheats used:
- Camera-facing hologram quads preserve the illusion without CPU billboarding.
- Polynomial trig is sufficient for radar scatter; physical angle precision is not gameplay truth.
- PDA sonar remains a GPU sign-crossing visual shell, not a physical sonar reconstruction.

Exact Microseconds saved:
- Replacing SFU trig in cockpit radar compute: estimated `3-12 us/4096 blips` GPU-side, pending profiler.
- Static quad arrays and `UploadMeshData(true)`: cold memory cleanup only, no hot-path claim.
- PDA GPU append/indirect dispatch avoids CPU point-cloud upload/readback; estimated `55-90 us` saved on PDA refresh frames versus the older CPU upload path.
- Disable-time button job completion prevents stale work leakage; no steady-frame cost.

Verification:
- Forbidden-pattern scan on touched cockpit/PDA files found no `SetData`, `GetData`, `Camera.main`, `Physics.Raycast`, `GraphicRaycaster`, `.text =`, `ToString`, `string.Format`, or `foreach`.
- `dotnet build Hecton8.Core.csproj --no-restore -m:2 /nr:false -v:minimal -clp:Summary` now has one remaining compile error: `Assets/_Project/Scripts/EncounterDirector.cs(1778,79): ResolveCheapestAllowedCost` missing. That file is out of VEHICLE_SUB_OS domain.
- Unity MCP validation/console retries returned `Unity session not available`; latest Unity import status remains unverified.

## 2026-05-12 - Loop 8 PDA Anti-Bloat and Build Closure
Status: `PENDING VERIFICATION`

What was wrong:
- `PDAMapTab.cs` still carried the old CPU sonar point-cloud upload path after the GPU append/indirect implementation was repaired.
- Dead state included `SonarPointCloudPoint`, `_pointCloudPoints`, `_pointCloudBuffer`, upload pending/count flags, and `UploadPointCloudIfNeeded`.
- The old path contradicted the zero-GC/GPU ownership target and could be reactivated later into a `SetData` refresh stall.

What was done:
- Removed the dead CPU point-cloud struct, native array, upload flags/counts, structured buffer, upload method, and release path.
- Kept the compute raymarch append buffer and indirect args as the single PDA sonar point-cloud route.
- Corrected the PDA append-buffer cold-allocation comment to `528 x 16B`.
- Re-ran stale-symbol scans and the full core build.

Cinematic Cheats used:
- PDA sonar remains a GPU sign-crossing shell over the published SDF, not physical sonar reconstruction.
- Low tier uses a 4-axis dispatch and 8 raymarch steps; higher tiers spend budget on denser GPU raymarch work.
- Predator AUP pings stay compact as `float4` overlays in the same append buffer.

Exact Microseconds saved:
- Removed CPU point-cloud upload fallback risk: estimated `40-140 us` saved on PDA refresh frames versus `NativeArray` + `SetData` payload upload.
- Removed stale payload ownership: approximately `64 KB` native/GPU-side dead budget avoided.
- Stale-symbol and forbidden-pattern scans save no frame time; they prevent regression into managed/CPU upload paths.

Verification:
- `rg` stale-symbol scan found no `SonarPointCloudPoint`, `_pointCloudPoints`, `_pointCloudBuffer`, `BuildPointCloudPayload`, `UploadPointCloudIfNeeded`, `SetData`, or `GetData` hits in `PDAMapTab.cs`.
- Cockpit/radar forbidden-pattern scan found no `Physics.Raycast`, `GraphicRaycaster`, `Canvas`, `.text =`, `.ToString()`, `string.Format`, `foreach`, `SetData`, or `GetData`.
- `git diff --check` found no whitespace errors on touched cockpit/PDA/radar files; only existing line-ending warnings were reported.
- `dotnet build Hecton8.Core.csproj --no-restore -m:2 /nr:false -v:minimal -clp:Summary` passed with `0 errors` and `5 warnings` in unrelated audio/world fields.
- Unity MCP script validation and console reads returned `Unity session not available`; latest Unity import/shader-console status is still pending.

## 2026-05-12 - Loop 9 Cockpit Hot-Path Inquisition
Status: `PENDING VERIFICATION`

What was wrong:
- The radar compute path still re-uploaded sonar taps, rewrote compute parameters, dispatched, and rewrote indirect args on stable frames where sonar sequence, visual point count, power, and damage flicker had not changed.
- `ApplyScreenMaterial` still had a lazy `MaterialPropertyBlock` allocation fallback reachable from Tick if initialization order ever regressed.
- Panel hit math used two Transform inverse calls per interaction, off-screen UI camera properties were rewritten every Tick, and serialized panel bounds could be invalid.
- `RenderTexturePool.Rent` preserved depth in the key but still trusted caller dimensions before hashing/allocation.

What was done:
- Added a radar dispatch cache keyed by sonar sequence, visual point count, power ratio, and damage flicker. Stable radar frames now keep the existing GPU blip buffer and args without another CPU upload/compute dispatch.
- Converted the cockpit screen MPB into a single cold field allocation and removed the hot-path fallback branch.
- Changed panel hit conversion to one `worldToLocalMatrix` snapshot, gated off-screen camera property writes, clamped panel extents and lever index in `OnValidate`, and added XML docs for public cockpit API.
- Hardened `RenderTexturePool.Rent` by clamping width/height to at least `1` before keying and allocation.

Cinematic Cheats used:
- Stable sonar frames reuse the last hologram buffer instead of simulating per-frame acoustic decay.
- Damage flicker remains a scalar shader/compute fake; only changes above the dispatch epsilon buy a new blip upload.
- Low/MX350 still uses static exterior feed and a 512-point cap; High/Ultra spend dispatches on dense radar only when payload state actually changes.

Exact Microseconds saved:
- Radar stable-frame upload/dispatch gate: estimated `10-45 us/frame` CPU submit and upload bandwidth avoided, pending profiler.
- MPB cold-field fix: protects `0 B/frame`; no separate CPU claim.
- Panel matrix/camera property gating: estimated `1-6 us` saved on interaction or stable-screen frames depending on native property overhead.
- RT dimension clamp: failure-mode hardening; no steady-frame claim.

Verification:
- `dotnet build Hecton8.Core.csproj --no-restore -m:2 -nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -v:minimal -clp:Summary` passed with `0 errors`, `0 warnings`.
- Cockpit/radar/pool forbidden-pattern scan found no `SetData`, `GetData`, `Physics.Raycast`, `GraphicRaycaster`, `Canvas`, `.text =`, `.ToString()`, `string.Format`, or `foreach`.
- Cold-allocation scan reports only documented startup allocations: MPB field, NativeArrays, graphics buffers, render textures, material instance, and runtime quad mesh.
- Unity MCP `validate_script` and console reads returned `Unity session not available`; Unity import/shader-console verification remains pending.

## 2026-05-12 - Loop 10 Numeric Vaccination and Runtime Resilience
Status: `PENDING VERIFICATION`

What was wrong:
- NaN/Inf values from upstream transforms, power telemetry, oxygen telemetry, damage flicker, or acoustic compute constants could enter panel ray math, button matrices, shader parameters, or the telemetry blackbox.
- `UpdateRadarArgs(0)` still rewrote indirect args on repeated no-audio/zero-point frames.
- Radar sonar/blip/args buffers were allocated even when the optional radar compute asset was absent.
- Exterior camera feed acquisition only configured the camera during first RT allocation; if the camera target/enabled state was disturbed later, the runtime kept the RT but did not repair the camera.

What was done:
- Added finite guards and fallback clamps for ray inputs, panel-local hits, button base positions, button job progress, power/O2/CO2/speed snapshots, telemetry writes, damage flicker, radar radius/bounds, and compute shader scalar inputs.
- Added indirect-args caching by mesh and instance count.
- Split button matrix allocation from radar VFX allocation; radar GPU buffers now wait until `radarCompute` exists, and Tick retries if the reference appears later.
- Reworked external feed acquisition/release to reassert camera target/enabled state without repeated property writes when state is already correct.

Cinematic Cheats used:
- Poisoned telemetry/power values fail to finite fallback presentation values instead of attempting physical recovery.
- Silent sonar frames keep the previous zero-args state instead of simulating empty acoustic motion.
- Missing radar compute degrades to working screens/buttons/telemetry rather than reserving invisible radar buffers.

Exact Microseconds saved:
- Repeated zero/no-change indirect-args gate: estimated `2-8 us/frame` avoided in silent sonar or stable active-count frames, pending profiler.
- Optional radar allocation split: approximately `~160 KB` GPU/native buffer reservation avoided on missing-compute cockpit instances at 4096 capacity.
- External camera property gating: small native property churn reduction; no separate steady-frame claim.
- Numeric vaccination is crash-prevention hardening; no frame-time claim.

Verification:
- Unity MCP `validate_script` reports `0 errors`, `0 warnings` for `VehicleSubOsCockpitRuntime.cs`, `RenderTexturePool.cs`, and `PDADataArchaeologyDecryptLabel.cs`.
- Cockpit/radar/pool forbidden-pattern scan found no `SetData`, `GetData`, `Physics.Raycast`, `GraphicRaycaster`, `Canvas`, `.text =`, `.ToString()`, `string.Format`, or `foreach`.
- `git diff --check` on touched cockpit/radar/pool files reports no whitespace errors; Git warns only that `RenderTexturePool.cs` line endings may normalize to CRLF.
- `dotnet build Hecton8.Core.csproj` is currently blocked outside this domain by `HectonVoxelEngine` missing async mesh helper methods; a later retry timed out after `120s`.
- Unity console after compile request reports out-of-domain `SubmarineStructuralGrid` missing `ILateFrameTickable.LateFrameTick()`. No cockpit/radar validation errors are present.

## 2026-05-12 - Loop 10 Radar Gate Correction
Status: `PENDING VERIFICATION`

What was wrong:
- The Loop 9 radar dispatch cache existed but was self-defeating.
- `UploadSonarTapsAndDispatchRadar()` resets `_radarActivePoints` to `0` before calling `IsRadarDispatchDirty()`.
- `IsRadarDispatchDirty()` then compared `_radarActivePoints` against `visualPointCount`, so every active sonar frame became dirty and still executed `LockBufferForWrite`, compute parameter writes, dispatch, and indirect-args upload.

What was done:
- Re-extracted the `VEHICLE_SUB_OS` XML prompt from `Docs/Tasks/CURRENT_BATCH.md`.
- Re-read the relevant zero-GC and MX350 compute mandates.
- Removed the false-dirty `_radarActivePoints != visualPointCount` predicate from `VehicleSubOsCockpitRuntime.IsRadarDispatchDirty()`.
- Re-ran static scans, whitespace check, Unity MCP retry, and full core build.

Cinematic Cheats used:
- Stable sonar frames deliberately reuse the resident hologram buffer instead of generating fake motion every frame.
- Damage flicker and power brownout still invalidate the cache only when their scalar deltas exceed the epsilon.
- Low/MX350 keeps the 512-point cap; High/Ultra avoid redundant 4096-point dispatches until the visual payload actually changes.

Exact Microseconds saved:
- This repair restores the Loop 9 intended savings: estimated `10-45 us/frame` CPU submit/upload avoidance on stable sonar frames, pending profiler.
- No new memory allocation was added.
- No new shader work was added.

Verification:
- `rg` confirmed no `_radarActivePoints != visualPointCount` predicate remains.
- `git diff --check` found no whitespace errors on touched cockpit/PDA/radar/pool files; only existing line-ending warnings were reported.
- `dotnet build Hecton8.Core.csproj --no-restore -m:2 -nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -v:minimal -clp:Summary` passed with `0 errors`, `1 warning` in unrelated `WorldSpatialHashGrid`.
- Unity MCP `validate_script` and console reads returned `Unity session not available`; Unity import/shader-console verification remains pending.

## 2026-05-12 - Loop 11 Radar Zero-State Cache Hygiene
Status: `PENDING VERIFICATION`

What was wrong:
- The Loop 10 dispatch predicate was correct for stable active sonar frames, but zero-state transitions were still incomplete.
- Missing audio, zero taps, or radar power-off could zero indirect draw args without invalidating the last successful dispatch cache.
- If the same sonar sequence became drawable again after that zero-args frame, the cache could skip compute dispatch while the args buffer still contained zero instances.

What was done:
- Re-extracted the `VEHICLE_SUB_OS` XML prompt from `Docs/Tasks/CURRENT_BATCH.md`.
- Added `ClearRadarDrawState()` to pair `UpdateRadarArgs(0)` with `InvalidateRadarDispatchCache()`.
- Routed resource-missing, power-off, missing-audio, zero-tap, and zero-visual exits through the unified clear path.
- Kept the existing `_lastRadarArgsInstanceCount`/mesh guard, so repeated dark/no-signal frames do not rewrite zero indirect args every Tick.

Cinematic Cheats used:
- Dark/no-signal frames intentionally keep no hologram instead of simulating decay.
- Radar reappears only after a coherent dispatch state exists again.
- Low/MX350 stays at the 512-point cap; High/Ultra keep 4096-point overkill only while dispatch cache and indirect args agree.

Exact Microseconds saved:
- Correctness fix prevents stale zero-args recovery failure; no fake steady-frame claim.
- Existing indirect args guard avoids repeated zero-instance `LockBufferForWrite`, estimated `2-12 us/frame` on dark/no-signal frames pending profiler.
- No new managed allocation was added.

Verification:
- `rg` confirmed all no-signal/off-state branches now call `ClearRadarDrawState()`.
- Forbidden-pattern scan found no `SetData`, `GetData`, `Physics.Raycast`, `GraphicRaycaster`, `.text =`, `.ToString()`, `string.Format`, or `foreach` in touched cockpit/radar/pool files; only documented cold `MaterialPropertyBlock` allocation remains.
- `git diff --check` found no whitespace errors on touched cockpit/PDA/radar/pool files; only existing line-ending warnings were reported.
- `mcp validate_script Assets/_Project/Scripts/UI/VehicleSubOsCockpitRuntime.cs` returned `0 errors`, `0 warnings`.
- `dotnet build Hecton8.Core.csproj --no-restore -m:2 -nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -v:minimal -clp:Summary` is blocked outside UX by `HectonVoxelEngine.cs` missing `EnsureVoxelSurfaceMeshAvailableAsync` and `EnsureVoxelPhysicsBakeMeshAvailableAsync`.
- Unity MCP console read timed out after script validation; latest Unity console status remains pending.

## 2026-05-12 - Loop 12 Radar Args Rebuild Initialization
Status: `PENDING VERIFICATION`

What was wrong:
- A newly created `_radarArgsBuffer` is raw indirect-arguments GPU memory.
- The dispose path invalidated the args cache, but the create path did not explicitly tie a fresh buffer to a deterministic zero-instance write after the radar mesh was available.
- During tier/resource rebuilds, that left correctness dependent on downstream Tick order instead of local resource initialization.

What was done:
- Re-extracted the `VEHICLE_SUB_OS` XML prompt from `Docs/Tasks/CURRENT_BATCH.md`.
- Re-read diegetic UI, zero-GC UI streaming, and MX350 compute mandates before editing.
- Added a `radarArgsBufferCreated` gate inside `VehicleSubOsCockpitRuntime.EnsureGraphicsResources()`.
- When a fresh args buffer is allocated, the runtime now resolves/creates the radar mesh, invalidates the cached args identity, and writes zero indirect args immediately.

Cinematic Cheats used:
- Rebuild frames deliberately initialize to no hologram instead of trying to preserve a speculative stale draw.
- Active radar returns through the normal sonar dispatch gate, preserving the stable-frame skip path.
- Low/MX350 keeps deterministic 512-point rebuild behavior; High/Ultra keep deterministic 4096-point rebuild behavior.

Exact Microseconds saved:
- No steady-frame performance claim.
- The one-time zero write prevents uninitialized indirect draw risk after resource rebuild.
- Existing args caching still avoids repeated zero-instance `LockBufferForWrite` after initialization.

Verification:
- Focused `git diff --check` on touched cockpit/docs files passed.
- Forbidden-pattern scan found no `SetData`, `GetData`, `Physics.Raycast`, `GraphicRaycaster`, `.text =`, `.ToString()`, `string.Format`, or `foreach` in cockpit/PDA/pool files; only documented cold `MaterialPropertyBlock` allocation remains.
- `dotnet build Hecton8.Core.csproj --no-restore -m:2 -nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -v:minimal -clp:Summary` passed with `0 errors`, `0 warnings`.
- Unity MCP `validate_script Assets/_Project/Scripts/UI/VehicleSubOsCockpitRuntime.cs` returned `Unity session not available`; latest Unity import/console status remains pending.

## 2026-05-12 - Loop 13 Low-Tier UI RenderTexture Format Gate
Status: `PENDING VERIFICATION`

What was wrong:
- Low/MX350 cockpit UI screens were resolution-clamped but still allocated `ARGB32` color targets.
- The screen content is opaque diegetic telemetry, so low-tier alpha bandwidth was not buying visible value.
- If quality tier changed while dimensions stayed the same, format alone could not force RT recreation.

What was done:
- Re-extracted the `VEHICLE_SUB_OS` XML prompt from `Docs/Tasks/CURRENT_BATCH.md`.
- Re-read zero-GC, diegetic UI, MX350 compute, AUP, and crash telemetry mandates.
- Added a cached `_uiRenderTextureFormat` resolved during scalability-tier evaluation.
- Low/MX350 now selects `RenderTextureFormat.RGB565` when supported, with an `ARGB32` fallback.
- `EnsureRenderTargets()` now reallocates the cockpit UI RT on format mismatch, not only width/height mismatch.

Cinematic Cheats used:
- Low-tier screen quality trades unused alpha for RGB565 bandwidth savings.
- High/Ultra keep `ARGB32` for cleaner premium screen gradients and live-feed clarity.
- External camera remains high-tier only; Low/MX350 still uses static noise.

Exact Microseconds saved:
- No CPU-frame claim.
- Low-tier `512x256` color memory drops from about `512 KB` to `256 KB`, saving about `256 KB` before depth.
- Bandwidth pressure is reduced on MX350 when the off-screen UI camera renders the cockpit telemetry RT.

Verification:
- Focused `git diff --check` on touched cockpit/docs files passed.
- Static scan found no `SetData`, `GetData`, `Physics.Raycast`, `GraphicRaycaster`, `.text =`, `.ToString()`, `string.Format`, or `foreach`; hits were the documented cold MPB and intended RT format paths.
- `dotnet build Hecton8.Core.csproj --no-restore -m:2 -nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -v:minimal -clp:Summary` passed with `0 errors`, `0 warnings`.
- Unity MCP `validate_script Assets/_Project/Scripts/UI/VehicleSubOsCockpitRuntime.cs` returned `Unity session not available`; latest Unity import/console status remains pending.

## 2026-05-12 - Loop 14 Offscreen UI Camera Dirty-Frame Gate
Status: `PENDING VERIFICATION`

What was wrong:
- The off-screen cockpit UI text writes only on dirty state or at `0.1s` cadence.
- The UI camera still stayed enabled continuously, so the RenderGraph camera path could render a static telemetry RT every frame.
- The internal UI camera could also render behind the live external feed or Low/MX350 static feed where the RT was not visible.

What was done:
- Re-extracted the `VEHICLE_SUB_OS` XML prompt from `Docs/Tasks/CURRENT_BATCH.md`.
- Re-read the diegetic UI mandate's adaptive RT dirty-frame guidance.
- Added `_offscreenUiCameraRenderRequested` and a one-frame `ApplyOffscreenUiCameraState()` gate.
- Changed `UpdateOffscreenText()` to return whether it wrote text, then request a camera render only for changed text/RT state.
- Added `IsOffscreenUiVisible()` so hidden internal UI RT work is suppressed while the exterior feed owns the central screen.

Cinematic Cheats used:
- Static cockpit telemetry is held in the previous RT instead of rerendered every frame.
- External camera/static feed hides internal UI updates until the internal bus becomes visible again.
- The normal camera path is preserved; no manual `Camera.Render()` path was introduced.

Exact Microseconds saved:
- Pending profiler. The pass can skip up to `5/6` internal UI camera renders on stable internal telemetry by dropping from 60Hz to the existing 10Hz text cadence.
- During visible exterior-feed intervals, internal UI camera render work is suppressed completely.
- No new managed allocation was added.

Verification:
- Focused `git diff --check` on touched cockpit/docs files passed.
- Static scan found no `SetData`, `GetData`, `Physics.Raycast`, `GraphicRaycaster`, `.text =`, `.ToString()`, `string.Format`, or `foreach`; hits were the documented cold MPB and intended camera-enable gates.
- `dotnet build Hecton8.Core.csproj --no-restore -m:2 -nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -v:minimal -clp:Summary` is blocked outside UX by `HectonPlayerMovement.cs` missing `IPostFixedTickable.PostFixedTick(float)`.
- Unity MCP `validate_script Assets/_Project/Scripts/UI/VehicleSubOsCockpitRuntime.cs` returned `Unity session not available`; latest Unity import/console status remains pending.

## 2026-05-12 - Loop 15 Authoring Guardrails for Cockpit Screen Targets
Status: `PENDING VERIFICATION`

What was wrong:
- `VehicleSubOsCockpitRuntime` accepted `16x16` serialized screen RT dimensions through the generic render-target creation clamp. That protects allocation validity but not cockpit readability or render target pool discipline.
- Physical button grid dimensions were only lower-clamped in several paths, so bad authoring values could leak into analytical hit mapping and fallback local-position layout.

What was done:
- Re-extracted the `VEHICLE_SUB_OS` XML prompt from `Docs/Tasks/CURRENT_BATCH.md`.
- Re-read zero-GC, URP hot-path, performance-budget, and cinematic-fake mandates before editing.
- Added explicit screen constants: internal UI minimum `256x128`, Low/MX350 internal maximum `512x256`, and live external feed minimum `256x144`.
- Routed `ResolveUiWidth`, `ResolveUiHeight`, external feed acquisition, and `OnValidate` through those bounds.
- Added `ResolveButtonColumns()` and `ResolveButtonRows()` with `1..32` clamps, then used them in hit math, fallback placement, and grid-capacity calculation.

Cinematic Cheats used:
- Kept cockpit displays as bounded render-target presentation fakes rather than adding Canvas or physical screen simulation.
- Preserved the Low/MX350 static-feed cheat and RGB565 telemetry bus while leaving High/Ultra free to spend more pixels on the diegetic display.

Exact Microseconds saved:
- `0 us/frame` steady-state claim. This loop is correctness and authoring failure hardening, not a profiler-backed hot-path optimization.
- `0 B/frame` managed allocation impact. The new guards are scalar math only.
- Prevents accidental unreadable RT allocation and pool bucket churn from sub-design serialized values; live external feed minimum color payload is about `144 KB` before depth (`256x144x4`).

Verification:
- `git diff --check`: passed.
- Forbidden-pattern scan found no `SetData`, `GetData`, `Physics.Raycast`, `GraphicRaycaster`, `Canvas`, `.text =`, `.ToString()`, `string.Format`, or `foreach`; only documented cold `MaterialPropertyBlock` remains.
- `dotnet build Hecton8.Core.csproj --no-restore -m:2 -nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -v:minimal -clp:Summary` passed with `0 errors`, `0 warnings`.
- Unity MCP `validate_script Assets/_Project/Scripts/UI/VehicleSubOsCockpitRuntime.cs` and console read returned `Unity session not available`; latest Unity import/console status remains pending.

## 2026-05-12 - Loop 16 Text-Diff and Radar Binding Gate
Status: `PENDING VERIFICATION`

What was wrong:
- Loop 14 stopped continuous off-screen camera rendering, but the text path still dirtied all four TMP labels whenever the `0.1s` cadence elapsed.
- Hidden external/static feeds could consume and clear an internal UI render request before the internal telemetry bus was visible again.
- Radar render submitted the same blip buffer binding every render and only checked finite anchor translation, not the full local-to-world matrix feeding the shader.

What was done:
- Re-extracted the `VEHICLE_SUB_OS` XML prompt from `Docs/Tasks/CURRENT_BATCH.md`.
- Added cached display buckets for PWR, O2, SONAR point count, radar power state, and STATUS mode.
- Moved sonar/radar state update before off-screen text diffing so the SONAR label reflects current active points in the same Tick.
- Preserved `_offscreenUiCameraRenderRequested` while the internal bus is hidden by live exterior feed or Low/MX350 static feed.
- Cached radar material blip-buffer binding by `GraphicsBuffer` identity and invalidated it on graphics resource disposal.
- Added full `Matrix4x4` finite validation before `Graphics.RenderMeshIndirect`; the local-to-world matrix still updates every render for AUP-safe submarine-local blips.
- Added a drawable radar readiness gate so sonar compute dispatch does not run when the hologram material/mesh is absent, while compute buffers still retry when assets appear.

Cinematic Cheats used:
- Static cockpit telemetry is held as the last valid RT image instead of rebuilt just because a timer elapsed.
- Low/MX350 keeps static exterior feed as the visible cheat while internal UI work is deferred until it matters.
- Holo-radar remains a GPU point-cloud fake driven by sonar taps, not physical sonar simulation.

Exact Microseconds saved:
- Pending profiler. Expected savings are small but real on stable cockpit frames: fewer TMP geometry dirties, fewer internal UI camera render requests, one less repeated radar material buffer bind, and no sonar compute dispatch for non-drawable radar VFX.
- `0 B/frame` managed allocation impact; new state is primitive fields only.
- No steady-frame hard number is claimed without Unity profiler/player capture.

Verification:
- Forbidden-pattern scan found no `SetData`, `GetData`, `Physics.Raycast`, `GraphicRaycaster`, `Canvas`, `.text =`, `.ToString()`, `string.Format`, or `foreach` in cockpit/radar/pool files.
- `new` scan showed only documented cold/init/struct sites.
- Focused `git diff --check` reported no whitespace errors, only LF-to-CRLF working-copy warnings.
- `dotnet build` and Unity validation were not run by explicit user instruction.

## 2026-05-12 - Loop 17 UI RenderTexture Teardown Detach
Status: `PENDING VERIFICATION`

What was wrong:
- The UI RT reallocation path detached `offscreenUiCamera.targetTexture` before destroying `_uiRenderTexture`.
- Final teardown still called the generic destroy helper directly, relying on Unity lifecycle order to avoid a camera retaining a reference to a released target.

What was done:
- Added `ReleaseUiRenderTexture()`.
- Routed both UI RT reallocation and `OnDestroy()` through the same helper.
- The helper clears `offscreenUiCamera.targetTexture` when it references `_uiRenderTexture`, then destroys the RT through the existing generic path.

Cinematic Cheats used:
- No new simulation or Canvas path. This preserves the off-screen render-target presentation cheat and only hardens its lifetime.

Exact Microseconds saved:
- `0 us/frame` steady-state claim. This is render-target lifetime hardening.
- `0 B/frame` managed allocation impact.
- Prevents stale camera target references across RT format/dimension rebuilds and final teardown.

Verification:
- Focused `git diff --check` reported no whitespace errors, only LF-to-CRLF working-copy warnings.
- Forbidden-pattern scan found no `SetData`, `GetData`, `Physics.Raycast`, `GraphicRaycaster`, `Canvas`, `.text =`, `.ToString()`, `string.Format`, or `foreach`; only documented cold `MaterialPropertyBlock` remains.
- First `dotnet build --no-restore` was blocked by missing generated `project.assets.json` files; `dotnet restore` succeeded.
- Second `dotnet build -m:1 --no-restore` was blocked outside UX by `HectonVoxelEngine.cs` and `HectonFluidEngine.cs` compile errors.
- Unity MCP `validate_script Assets/_Project/Scripts/UI/VehicleSubOsCockpitRuntime.cs` failed because the local Unity MCP HTTP transport at `127.0.0.1:8088` was unavailable.
