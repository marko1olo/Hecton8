# Rationale_VEHICLE_SUB_OS

Status: `PENDING VERIFICATION`

## Scope
Problem: Submarine cockpit presentation was static and the batch requires diegetic physical controls, off-screen cockpit screens, a holographic radar point cloud, power degradation, and blackbox telemetry.
Solution: Build a Presentation & UX runtime (`VehicleSubOsCockpitRuntime`) that consumes existing `GlobalRegistry` services and event buses, with fixed native buffers, analytical panel math, compute-driven blips, indirect rendering, and pooled render targets.
Rejected Alternatives: World-space Canvas, GraphicRaycaster, `Physics.Raycast`, per-frame string formatting, persistent exterior camera RT, and direct Logistics/Acoustic internals.
Scalability potential: Low/MX350 uses 512 radar capacity and static external feed; Middle uses 2048 radar capacity; High/Ultra uses 4096 radar capacity and live camera RT. Saved CPU/GPU budget is spent on denser radar visuals and damage flicker only on capable tiers.
Hardware Impact: On i3/MX350 the implementation avoids Canvas rebuilds, raycasts, idle button jobs, and live exterior feed cost. Expected cockpit CPU path stays below 0.1 ms pending profiler capture.

## Decision 1: Sonar Echo Tap Seam
Problem: The acoustic renderer owned `SonarEchoTap` as a private nested struct, so Presentation could not legally consume the requested `NativeArray<SonarEchoTap>`.
Solution: Promote `SonarEchoTap` to a public 64-byte blittable struct inside the existing audio source file and expose `TryGetCockpitSonarEchoTaps(out NativeArray<SonarEchoTap>.ReadOnly, out int, out int)` from `PlayerCriticalProceduralAudioRenderer`.
Rejected Alternatives: Reflection against private fields, copying through managed arrays, or waiting for another agent to add a seam.
Scalability potential: Low/Mid/High all copy only the active tap count into a GPU upload buffer; capacity scales separately.
Hardware Impact: Avoids managed allocations and avoids audio-thread reads. Estimated `10-35 us/ping` saved versus managed event payloads.

## Decision 2: Radar Compute and Indirect Draw
Problem: Radar blips must be a cockpit-local 3D holographic point cloud without CPU mesh transforms.
Solution: `Hecton_CockpitHoloRadar.compute` maps sonar delay to radius and stereo pan to angle, writes `RadarBlipGpuData` to a `GraphicsBuffer`, and `Hecton_RadarBlipInstanced.shader` reads it through `SV_InstanceID`.
Rejected Alternatives: ParticleSystem, `DrawMeshInstanced` arrays, and Canvas/minimap overlays.
Scalability potential: Low `512`, Mid `2048`, High/Ultra `4096`. Ultra keeps capacity for visual overkill; low path clamps memory and thread count.
Hardware Impact: Keeps CPU submit path to one indirect call. Estimated `20-70 us/frame` saved over managed instancing at cockpit scale.

## Decision 3: Analytical Cockpit Controls
Problem: Player is seat-locked, so cockpit button input does not need broadphase physics.
Solution: `TryResolvePanelHit` transforms the ray into panel local space and solves one ray-plane intersection, then maps the local hit to a fixed grid.
Rejected Alternatives: Collider buttons, `Physics.Raycast`, Unity EventSystem, and GraphicRaycaster.
Scalability potential: Same math across all tiers; no low-end penalty.
Hardware Impact: Estimated `8-25 us/interaction` saved plus no collider churn.

## Decision 4: Kinematic Button Mesh Buffer
Problem: Buttons must physically move without Animator overhead.
Solution: Fixed `NativeArray<byte>` states plus Burst `ButtonKinematicJob` interpolate local Z over `0.1s`, write matrices to `GraphicsBuffer`, and update authored button transforms only when the job has completed in the dispatcher swap window.
Rejected Alternatives: Animator state machines, coroutines, per-button MonoBehaviour Update, and continuous idle jobs.
Scalability potential: Low and High share deterministic motion; high-end can bind the matrix buffer to a BRG/indirect button shader later without changing the state lane.
Hardware Impact: Idle button job was removed during polish. Estimated `15-45 us/frame` saved when cockpit is idle.

## Decision 5: Off-screen Screen and External Feed
Problem: Complex O2/Power text needs a screen texture, while the prompt forbids 3D Canvas.
Solution: Off-screen orthographic camera renders to `VSOS_UI_RT`; TMP labels update with `ZeroGCFormatter` + `SetCharArray`; central screen material receives the RT through a reused `MaterialPropertyBlock`; exterior feed RT returns to `GlobalRegistry.RenderTexturePool` using a depth-aware bucket when inactive.
Rejected Alternatives: World-space Canvas, `.text =`, per-frame material instancing, and always-on exterior camera.
Scalability potential: Low/MX350 clamps UI RT size and disables live exterior feed; high tiers keep the live RT path.
Hardware Impact: Avoids Canvas rebuild and live camera when off. Estimated `0.05-0.30 ms` saved compared with active 3D Canvas screens.

## Decision 6: Power Brownout
Problem: Cockpit visuals must degrade from submarine grid voltage without coupling to Logistics internals.
Solution: Read `GlobalRegistry.PowerGrid.TryGetGridPowerPotentialsReadOnly(grid,node)` and fall back to aggregate `PowerGridTelemetrySnapshot.SupplyRatio`; below `0.2` disables radar and dims the dashboard material.
Rejected Alternatives: Direct Logistics graph references, polling scene power node MonoBehaviours, and hardcoded always-on cockpit.
Scalability potential: Low and High react identically; high-end only spends extra visuals while voltage exists.
Hardware Impact: Zero-copy read-only NativeArray lane. Estimated `5-20 us/frame` saved versus graph traversal.

## Decision 7: AUP and Blackbox
Problem: Radar must not tear on origin shift and crashes need forensic data.
Solution: Blips stay submarine-local and are transformed by current dome local-to-world at render time. A 300-entry `NativeArray<CockpitTelemetryEntry>` circular buffer records frame, active points, interactions, flags, power, O2, CO2, speed, and anchor position; NaN dumps to `Docs/AgentLogs/Dump_VEHICLE_SUB_OS.bin`.
Rejected Alternatives: cached world positions and `Debug.Log` telemetry.
Scalability potential: Same blackbox across tiers; only visual density scales.
Hardware Impact: 64-byte entries, linear writes, no managed allocations; modulo replaced with branch-wrapped index.

## Decision 8: Continuation Hardening
Problem: The first pass made the whole cockpit tick depend on optional radar compute readiness and only drew one point per audio tap, so a missing VFX asset could disable screen/buttons/telemetry and High/Ultra radar capacity stayed mostly unused.
Solution: Treat radar graphics resources as optional, keep the cockpit tick alive without compute, expand sonar taps into tiered GPU-only visual replicas, and clamp Low/MX350/Mid/High density through `_OutputPointCount`.
Rejected Alternatives: CPU-side point synthesis, requiring more acoustic taps, or keeping alpha-tested cutout dots.
Scalability potential: Low/MX350 keeps 512 max points with 32 replicas per tap; Mid uses 2048 with 128 replicas; High/Ultra fills 4096 with 256 replicas per tap for visual overkill without increasing CPU upload count.
Hardware Impact: CPU still uploads only the active audio taps, normally 16 max. GPU threads scale by quality tier; expected CPU impact is neutral to lower due gated MPB reads, while visual density improves by `32x-256x`.

## Decision 9: Visual Fidelity and Compile-Seam Repair
Problem: Radar quads were local-plane sprites that could lose readability at oblique camera angles, compute used SFU trig per replica, blackbox dumps were not chronological, and a related UI-domain PDA point-cloud compile seam had missing helpers.
Solution: Billboard procedural radar quads in shader, replace radar compute trig with wrapped polynomial approximations, dump the telemetry ring in chronological order, complete pending button jobs on disable, and restore the PDA GPU append/indirect dispatch helper path.
Rejected Alternatives: CPU billboarding, CPU point-cloud `SetData`, raw circular dump order, or editing the remaining non-UX `EncounterDirector` blocker.
Scalability potential: Low/MX350 gets stable readable billboards with bounded 512-point radar and 4x4x4 PDA raymarch. Mid/High/Ultra spend saved CPU/SFU budget on denser cockpit/PDA holograms without extra managed allocations.
Hardware Impact: Expected GPU-side approximation gain is `3-12 us/4096 radar blips` pending profiler. PDA compile repair keeps point discovery on GPU and copies append count to indirect args, avoiding CPU readback/upload stalls.

## Decision 10: PDA Point-Cloud Single-Path Cleanup
Problem: `PDAMapTab` still carried a disabled CPU point-cloud upload path after the GPU append/indirect repair, including dead `NativeArray<SonarPointCloudPoint>` state, stale upload flags, and a legacy structured buffer release path.
Solution: Remove the CPU payload struct, native array, upload method, upload flags/counts, and old structured buffer, leaving the compute raymarch append buffer plus indirect args as the only point-cloud route.
Rejected Alternatives: Keeping a dormant `SetData` fallback, rebuilding a CPU `IJobParallelFor` cloud, or hiding the dead path behind `if false`.
Scalability potential: Low/MX350 now allocates only `528 x 16B` PDA sonar append data and runs a 4-axis dispatch; Mid/High/Ultra use the same compact GPU layout with denser raymarch work instead of CPU uploads.
Hardware Impact: Avoids roughly `40-140 us` on refresh frames where the old CPU upload could return, and removes about `64 KB` of stale native/GPU payload from the PDA sonar ownership path.

## Decision 11: Cockpit Stable-Frame Upload Gate
Problem: After visual density was increased, the cockpit still uploaded sonar taps and dispatched the radar compute every Tick even when the sonar sequence, point count, power, and damage flicker were unchanged.
Solution: Cache the last dispatched radar sequence, visual point count, power ratio, and flicker scalar. Stable frames now keep the existing GPU blip buffer and indirect args, while new sonar, brownout changes, flicker changes, tier changes, or power recovery invalidate the cache. The screen `MaterialPropertyBlock` is a single cold field allocation, panel hit math uses one `worldToLocalMatrix` snapshot, and off-screen camera properties write only on state changes.
Rejected Alternatives: Per-frame compute dispatch for identical DSP taps, CPU-side radar aging, lazy hot-path MPB allocation, and trusting serialized panel bounds without `OnValidate` clamps.
Scalability potential: Low/MX350 gets fewer PCIe/driver submissions on stable sonar while retaining the 512-point cap; Mid/High/Ultra still spend budget on denser hologram dispatch only when payload state changes.
Hardware Impact: Estimated `10-45 us/frame` CPU submit/bandwidth avoided on stable radar frames pending profiler; MPB change protects `0 B/frame`; panel/camera property gating is estimated `1-6 us` on interaction/stable-screen frames depending on driver/property overhead.

## Decision 12: Numeric Vaccination and Optional VFX Allocation
Problem: Upstream power/audio/transform inputs can legally degrade during concurrent system work. A NaN/Inf entering panel ray math, button matrices, telemetry entries, or radar compute scalars can poison GPU buffers or blackbox output. The runtime also reserved radar buffers even when the optional compute asset was missing.
Solution: Add finite guards for public flicker input, ray origins/directions, panel-local hits, button base positions, telemetry fields, button progress, radar radius/bounds, and HLSL compute constants. Cache indirect-args uploads by mesh and instance count. Allocate the button matrix buffer independently, but delay sonar/blip/args radar buffer allocation until `radarCompute` exists, with Tick retry if the reference appears later. External feed acquisition now reasserts camera target/enabled state each active frame without reallocating.
Rejected Alternatives: Relying on upstream systems to sanitize all floats, storing NaN in blackbox for later interpretation, rewriting indirect args every silent radar frame, and reserving radar GPU buffers for missing optional VFX.
Scalability potential: Low/MX350 avoids unnecessary radar buffer reservation and repeated zero-args writes; Mid/High/Ultra keep the dense radar path but reject poisoned inputs before dispatch.
Hardware Impact: Estimated `2-8 us/frame` avoided from repeated silent/unchanged args writes, `~160 KB` GPU/native reservation avoided on missing-compute cockpit instances at 4096 capacity, and crash-prevention hardening with no steady-frame claim.

## Decision 12: Radar Gate Predicate Correction
Problem: The Loop 9 stable-frame radar gate was present but logically defeated because `_radarActivePoints` is reset to `0` before `IsRadarDispatchDirty`, then compared against `visualPointCount`, forcing every active frame dirty.
Solution: Remove the self-defeating `_radarActivePoints != visualPointCount` predicate and let the cache depend on last dispatched sequence, visual point count, power, and flicker only.
Rejected Alternatives: Moving `_radarActivePoints` assignment earlier, adding another cached active-count field, or accepting the false-positive dirty state.
Scalability potential: Low/MX350 now actually keeps stable 512-point sonar frames on the resident GPU buffer; Mid/High/Ultra avoid redundant 2048/4096-point dispatches until sonar/power/flicker/tier changes require fresh visual overkill.
Hardware Impact: Restores the intended `10-45 us/frame` CPU submit/bandwidth skip path on stable sonar frames, pending profiler confirmation.

## Decision 13: Radar Zero-State Cache Hygiene
Problem: A no-signal or power-off frame could zero indirect args without invalidating the last successful radar dispatch cache, creating a recovery path where the same sonar sequence reappears but compute dispatch is skipped while args remain zero.
Solution: Route resource-missing, power-off, missing-audio, zero-tap, and zero-visual exits through `ClearRadarDrawState()`, which zeros indirect args and invalidates the dispatch cache together. Existing `_lastRadarArgsInstanceCount`/mesh tracking prevents repeated zero-args writes while the radar stays dark.
Rejected Alternatives: Invalidating only the zero-visual branch, always forcing a fresh dispatch after every dark frame, or touching the out-of-domain voxel build errors.
Scalability potential: Low/MX350 avoids redundant zero-args uploads on dark/no-signal frames and recovers deterministically when the same 512-point sequence becomes drawable again. High/Ultra preserve the resident 4096-point buffer only while the draw args and dispatch cache are coherent.
Hardware Impact: Avoids a recovery correctness bug; repeated dark/no-signal frames avoid `LockBufferForWrite` through the existing args guard, estimated `2-12 us/frame` pending profiler.

## Decision 14: Radar Args Buffer Rebuild Initialization
Problem: `_radarArgsBuffer` can be re-created during quality-tier/resource rebuilds. The previous cache invalidation happened on dispose, but the creation path did not explicitly reset the cached args identity or initialize the new raw indirect args buffer after the runtime radar mesh was available.
Solution: Track fresh args-buffer creation inside `EnsureGraphicsResources()`, create/resolve the radar mesh, then call `InvalidateRadarArgsCache()` and `UpdateRadarArgs(0)` so the new buffer starts with deterministic zero instances.
Rejected Alternatives: Trusting newly allocated raw GPU memory to be zeroed, moving `UpdateRadarArgs(0)` before runtime mesh creation where it can early-return, or forcing an active radar dispatch immediately after every resource rebuild.
Scalability potential: Low/MX350 can rebuild from 512-point radar resources without one-frame garbage draw risk; Mid/High/Ultra can rebuild 2048/4096-point resources and keep the visual-overkill path deterministic.
Hardware Impact: Crash/visual-corruption hardening only; no steady-frame performance claim. The write happens once per args-buffer creation, while the existing args cache still avoids repeated zero writes after initialization.

## Decision 15: Low-Tier UI RenderTexture Format Gate
Problem: The cockpit off-screen UI screen was clamped to low resolution on Low/MX350, but its color target still used `ARGB32`. The diegetic UI mandate explicitly calls for RGB565-style low-tier panel RTs when alpha is not required.
Solution: Cache a tier-resolved UI RT format. Low/MX350 selects `RenderTextureFormat.RGB565` only when supported by the platform, otherwise it falls back to `ARGB32`; non-low tiers retain `ARGB32`. `EnsureRenderTargets()` now recreates the RT when width, height, or format changes.
Rejected Alternatives: Forcing `RGB565` without a `SystemInfo.SupportsRenderTextureFormat` fallback, globally downgrading high-tier cockpit screens, or ignoring format changes when quality tier changes but dimensions remain identical.
Scalability potential: Low/MX350 gets cheaper cockpit UI memory and bandwidth at the 512x256 cap; Mid/High/Ultra keep ARGB32 for cleaner NASA-punk screen gradients and live-feed clarity.
Hardware Impact: At the low-tier 512x256 screen, color memory drops from roughly `512 KB` to `256 KB` before depth, saving about `256 KB` per cockpit UI RT. No CPU-frame claim.

## Decision 16: Offscreen UI Camera Dirty-Frame Gate
Problem: The cockpit text path updates on dirty state or every `0.1s`, but the off-screen UI camera remained enabled continuously. That kept the RenderGraph camera path active at frame rate even when the telemetry texture was static, and it could render behind the live/static external feed.
Solution: Add `_offscreenUiCameraRenderRequested`, make `UpdateOffscreenText()` report whether it wrote text, request a one-frame camera render after RT creation/retarget/text writes, and drive `offscreenUiCamera.enabled` through `ApplyOffscreenUiCameraState()`. `IsOffscreenUiVisible()` suppresses internal UI renders while the external feed owns the central screen.
Rejected Alternatives: Manual `Camera.Render()` outside the normal camera path, leaving the camera enabled continuously, or disabling the UI RT entirely while external feed is active and risking a stale internal screen on return.
Scalability potential: Low/MX350 renders the 512x256 RGB565 internal bus only when dirty and never behind the static feed. Mid/High/Ultra keep the RenderGraph camera route but spend render work on visible/changed cockpit screens instead of static frames.
Hardware Impact: On stable internal telemetry, the camera pass can drop from 60Hz to the existing 10Hz text cadence, skipping up to `5/6` UI camera passes pending profiler. Hidden external-feed intervals avoid the internal UI camera pass entirely.

## Decision 17: Cockpit Screen and Grid Authoring Guardrails
Problem: The cockpit runtime accepted `16x16` serialized screen render targets and unbounded physical button grid dimensions. Those values would compile and render, but they can collapse the diegetic telemetry bus into unreadable pixels, fragment render target pool buckets, or make analytical button hit layout depend on nonsense authoring data.
Solution: Add explicit minimum/low-tier maximum screen target constants. The internal telemetry RT uses a `256x128` minimum and Low/MX350 caps at `512x256`; live external feed uses a `256x144` minimum; High/Ultra authoring can still scale upward. Button columns and rows now resolve through `1..32` clamps before hit math, fallback placement, and grid-capacity calculation.
Rejected Alternatives: Trusting prefab authors to avoid bad values, globally capping High/Ultra screen RTs, or letting `CreateRenderTexture` silently clamp everything to its generic `16px` safety floor.
Scalability potential: Low/MX350 gets a readable RGB565 telemetry bus with bounded memory; Mid/High/Ultra keep larger authored cockpit displays for visual overkill. The physical control grid stays deterministic across all tiers because pathological serialized dimensions are rejected at the cockpit boundary.
Hardware Impact: No steady-frame performance claim. This is failure-mode hardening with `0 B/frame` GC impact; it prevents unreadable RT allocations and keeps live feed minimum color memory at about `144 KB` before depth (`256x144x4`) instead of allowing accidental sub-design targets.

## Decision 18: Text-Diff and Radar Binding Gate
Problem: Loop 14 made the off-screen camera dirty-frame driven, but `UpdateOffscreenText()` still rewrote all cockpit TMP labels whenever the cadence elapsed, even when PWR/O2/SONAR/STATUS content was identical. The radar render path also rebound the same blip `GraphicsBuffer` and procedural flag every render, only validated the anchor position instead of the full local-to-world matrix, and could dispatch compute when no drawable radar material/mesh was available.
Solution: Cache integer display buckets for PWR/O2, sonar point count, radar-powered state, and status mode. TMP `SetCharArray()` now fires only when a visible value changes, and `_screenUpdateAccumulator` saturates at the cadence cap while hidden. The UI camera render request is preserved while an external/static feed hides the internal RT. Radar material buffer binding is cached by `GraphicsBuffer` identity, the per-frame anchor matrix remains updated and fully finite-checked before `RenderMeshIndirect`, and sonar compute dispatch now requires a drawable radar material/mesh while compute resources remain retryable.
Rejected Alternatives: Cadence-based rewrites of unchanged TMP meshes, clearing off-screen render requests while hidden, per-frame radar `SetBuffer` calls, trusting transform matrices after only checking translation, and dispatching radar compute when the hologram cannot draw.
Scalability potential: Low/MX350 benefits most because unchanged 10Hz telemetry no longer dirties TMP geometry or triggers needless internal UI RT work; Mid/High/Ultra keep live cockpit fidelity but spend render work on changed values and active radar transforms only.
Hardware Impact: Expected to reduce small CPU/UI dirty work and driver state churn on stable cockpit frames, pending profiler. No `dotnet build` or Unity validation was run in this loop by explicit user instruction; status remains `PENDING VERIFICATION`.

## Decision 19: UI RenderTexture Teardown Detach
Problem: The UI RT reallocation path correctly detached `offscreenUiCamera.targetTexture` before destroying `_uiRenderTexture`, but `OnDestroy()` destroyed the same RT through the generic helper. That relies on Unity object disable/destroy ordering to prevent a camera from retaining a reference to a released target.
Solution: Add `ReleaseUiRenderTexture()` and route both reallocation and final teardown through it. The helper clears the offscreen camera target when it references the current UI RT, then destroys the RT through the existing generic destruction path.
Rejected Alternatives: Duplicating detach logic at each call site, clearing the camera target on every `OnDisable()` and losing the warm re-enable path, or trusting Unity lifecycle order for a critical render target.
Scalability potential: Same behavior across tiers. Low/MX350 avoids stale references to the RGB565 internal bus; Mid/High/Ultra avoid stale references to larger ARGB32 internal displays during quality changes or destruction.
Hardware Impact: Correctness/lifetime hardening only. `0 B/frame`, no steady-frame CPU claim. Build validation is blocked by out-of-domain voxel/fluid compile errors after restore; Unity MCP is unavailable.

## OMEGA POLISH CHANGES
- Honest calculation replaced with cinematic cheat: sonar angle/distance use delay/pan/attenuation visual mapping instead of physical proton-level sonar simulation; damage flicker is a scalar shader fake.
- Idle button animation job removed: button Burst job schedules only while a transition is active or initial matrix upload is dirty.
- MPB screen update gated: central screen material updates only when texture, power, or external-feed state changes.
- Telemetry ring optimized: `% 300` replaced with branch-wrapped `_telemetryWriteIndex`; `CockpitTelemetryEntry` forced to 64 bytes.
- Scalability Matrix: Low/MX350 = 512 radar cap + static noise feed; Mid = 2048 cap; High/Ultra = 4096 cap + live external RT + flicker headroom.
- Silo justification: one cross-domain edit in `PlayerCriticalProceduralAudioRenderer.cs` creates a public read-only seam for existing sonar taps; all runtime consumption remains through `GlobalRegistry.PlayerCriticalAudio`, not private coupling.

## Verification
- `mcp validate_script Assets/_Project/Scripts/UI/VehicleSubOsCockpitRuntime.cs`: `0 errors, 0 warnings`.
- `mcp validate_script Assets/_Project/Scripts/UI/PDAMapTab.cs`: `0 errors, 0 warnings`; current UI compile blocker from the previous pass is no longer present.
- Forbidden-pattern scan on new cockpit/shader files: no `Physics.Raycast`, `GraphicRaycaster`, `Canvas`, `.text =`, `.ToString()`, `string.Format`, `foreach`, `SetData`, or `GetData` hits.
- `dotnet build Hecton8.Core.csproj`: blocked by unrelated `HectonCelestialEngine.cs(430,119)` missing `IWeatherEventListener.OnWeatherEvent(in WeatherEventPayload)`.
- Unity console after refresh contains no `VehicleSubOsCockpitRuntime`, radar compute, radar shader, or `PDAMapTab` errors. Remaining entries are `HectonCelestialEngine` plus unrelated `Hecton_MarineSnow.compute` warnings.
- Loop 7 `dotnet build Hecton8.Core.csproj`: PDA errors are cleared; build now has one remaining out-of-domain error, `EncounterDirector.cs(1778,79)` missing `ResolveCheapestAllowedCost`, plus three unrelated warnings.
- Unity MCP validation retry during Loop 7 returned `Unity session not available`; no Unity-console claim is made for the latest shader/C# edits.
- Loop 8 stale-symbol scan: no CPU point-cloud `SonarPointCloudPoint`, `_pointCloudPoints`, `_pointCloudBuffer`, `UploadPointCloudIfNeeded`, `SetData`, or `GetData` hits remain in `PDAMapTab.cs`.
- Loop 8 forbidden-pattern scan on cockpit/radar files: no `Physics.Raycast`, `GraphicRaycaster`, `Canvas`, `.text =`, `.ToString()`, `string.Format`, `foreach`, `SetData`, or `GetData` hits.
- Loop 8 `dotnet build Hecton8.Core.csproj --no-restore -m:2 /nr:false -v:minimal -clp:Summary`: `PASS`, `0 errors`, `5 warnings` in unrelated audio/world fields.
- Loop 8 Unity MCP validation/console retry returned `Unity session not available`; latest Unity import status remains pending.
- Loop 9 forbidden-pattern scan on cockpit/radar/pool files: no `SetData`, `GetData`, `Physics.Raycast`, `GraphicRaycaster`, `Canvas`, `.text =`, `.ToString()`, `string.Format`, or `foreach` hits; cold allocation scan only reports documented startup allocations.
- Loop 9 `dotnet build Hecton8.Core.csproj --no-restore -m:2 -nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -v:minimal -clp:Summary`: `PASS`, `0 errors`, `0 warnings`.
- Loop 9 Unity MCP `validate_script` and console retry returned `Unity session not available`; latest Unity import/shader-console status remains pending.
- Loop 10 Unity MCP validation: `VehicleSubOsCockpitRuntime.cs`, `RenderTexturePool.cs`, and `PDADataArchaeologyDecryptLabel.cs` report `0 errors`, `0 warnings`.
- Loop 10 forbidden-pattern scan on cockpit/radar/pool files: no `SetData`, `GetData`, `Physics.Raycast`, `GraphicRaycaster`, `Canvas`, `.text =`, `.ToString()`, `string.Format`, or `foreach` hits; only documented cold MPB allocation is reported.
- Loop 10 `dotnet build Hecton8.Core.csproj --no-restore -m:2 -nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -v:minimal -clp:Summary`: `BLOCKED BY DEPENDENCY`; completed pass reached out-of-domain `HectonVoxelEngine` missing async mesh helper errors. A later retry timed out after `120s`.
- Loop 10 Unity console after compile request currently reports out-of-domain `SubmarineStructuralGrid.cs(53,117)` missing `ILateFrameTickable.LateFrameTick()`; no cockpit/radar validation errors are present.
- Loop 10 prompt re-extraction: `VEHICLE_SUB_OS` XML block re-read from `Docs/Tasks/CURRENT_BATCH.md`.
- Loop 10 predicate scan: no `_radarActivePoints != visualPointCount` false-dirty clause remains in `VehicleSubOsCockpitRuntime.cs`.
- Loop 10 `dotnet build Hecton8.Core.csproj --no-restore -m:2 -nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -v:minimal -clp:Summary`: `PASS`, `0 errors`, `1 warning` in unrelated `WorldSpatialHashGrid`.
- Loop 10 Unity MCP `validate_script` and console retry returned `Unity session not available`; latest Unity import/shader-console status remains pending.
- Loop 11 `mcp validate_script Assets/_Project/Scripts/UI/VehicleSubOsCockpitRuntime.cs`: `PASS`, `0 errors`, `0 warnings`.
- Loop 11 forbidden-pattern scan on cockpit/radar/pool files: no `SetData`, `GetData`, `Physics.Raycast`, `GraphicRaycaster`, `.text =`, `.ToString()`, `string.Format`, or `foreach`; only documented cold `MaterialPropertyBlock` allocation remains.
- Loop 11 `dotnet build Hecton8.Core.csproj --no-restore -m:2 -nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -v:minimal -clp:Summary`: `BLOCKED BY DEPENDENCY`, out-of-domain `HectonVoxelEngine.cs` missing `EnsureVoxelSurfaceMeshAvailableAsync` and `EnsureVoxelPhysicsBakeMeshAvailableAsync`.
- Loop 12 focused `git diff --check` on touched cockpit/docs files: `PASS`.
- Loop 12 forbidden-pattern scan on cockpit/PDA/pool files: no `SetData`, `GetData`, `Physics.Raycast`, `GraphicRaycaster`, `.text =`, `.ToString()`, `string.Format`, or `foreach`; only documented cold `MaterialPropertyBlock` allocation remains.
- Loop 12 `dotnet build Hecton8.Core.csproj --no-restore -m:2 -nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -v:minimal -clp:Summary`: `PASS`, `0 errors`, `0 warnings`.
- Loop 12 Unity MCP `validate_script Assets/_Project/Scripts/UI/VehicleSubOsCockpitRuntime.cs`: `PENDING VERIFICATION`, Unity session unavailable.
- Loop 13 focused `git diff --check` on touched cockpit/docs files: `PASS`.
- Loop 13 scan on `VehicleSubOsCockpitRuntime.cs`: no `SetData`, `GetData`, `Physics.Raycast`, `GraphicRaycaster`, `.text =`, `.ToString()`, `string.Format`, or `foreach`; hits are documented cold MPB and intended RT format paths.
- Loop 13 `dotnet build Hecton8.Core.csproj --no-restore -m:2 -nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -v:minimal -clp:Summary`: `PASS`, `0 errors`, `0 warnings`.
- Loop 13 Unity MCP `validate_script Assets/_Project/Scripts/UI/VehicleSubOsCockpitRuntime.cs`: `PENDING VERIFICATION`, Unity session unavailable.
- Loop 14 focused `git diff --check` on touched cockpit/docs files: `PASS`.
- Loop 14 scan on `VehicleSubOsCockpitRuntime.cs`: no `SetData`, `GetData`, `Physics.Raycast`, `GraphicRaycaster`, `.text =`, `.ToString()`, `string.Format`, or `foreach`; hits are documented cold MPB and intended camera-enable gates.
- Loop 14 `dotnet build Hecton8.Core.csproj --no-restore -m:2 -nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -v:minimal -clp:Summary`: `BLOCKED BY DEPENDENCY`, out-of-domain `HectonPlayerMovement.cs` missing `IPostFixedTickable.PostFixedTick(float)`.
- Loop 14 Unity MCP `validate_script Assets/_Project/Scripts/UI/VehicleSubOsCockpitRuntime.cs`: `PENDING VERIFICATION`, Unity session unavailable.
- Loop 16 prompt re-extraction: `VEHICLE_SUB_OS` XML block re-read from `Docs/Tasks/CURRENT_BATCH.md`.
- Loop 16 forbidden-pattern scan on cockpit/radar/pool files: no `SetData`, `GetData`, `Physics.Raycast`, `GraphicRaycaster`, `Canvas`, `.text =`, `.ToString()`, `string.Format`, or `foreach`; `new` hits are documented cold/init/struct sites.
- Loop 16 focused `git diff --check` on touched cockpit/docs/log files: no whitespace errors, only Git LF-to-CRLF working-copy warnings.
- Loop 16 build/Unity validation: `NOT RUN BY USER INSTRUCTION`; no `dotnet build`, Unity compile, or Unity validation command was executed.
