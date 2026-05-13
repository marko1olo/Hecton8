# Status_VEHICLE_SUB_OS

Prompt: `VEHICLE_SUB_OS`
Role: `UX_ENGINEER`
Domain: `PRESENTATION & UX / DIEGETIC COCKPIT`
Status: `PENDING VERIFICATION`
Task count: `18`

Relevant mandates loaded:
- `UI_Diegetic_Physical_Interfaces.txt`
- `UI_Data_Streaming_ZeroGC_Optimization.txt`
- `GPU_Compute_Kernels_Kernels_Optimization_MX350.txt`
- `LOGI_Energy_Networks_Power_Grid_Graph_Flow.txt`
- `MATH_Coordinate_Precision_AUP_FloatingOrigin.txt`
- `DBG_Telemetry_Crash_Reporting_PostMortem.txt`
- `OPT_Zero_GC_Policy_AllocFree_Mandate.txt`

## Loop 1: Tasks 1-5
- [x] Task 1: Holographic radar compute | DOD: `Hecton_CockpitHoloRadar.compute` translates public `SonarEchoTap` delay/pan/attenuation into local `GraphicsBuffer` blips with 64-thread groups | Alternative rejected: CPU transform arrays and particle systems | Estimate: `35-110 us/ping` saved on i3/MX350
- [x] Task 2: Radar point cloud draw | DOD: `Hecton_RadarBlipInstanced.shader` now supports procedural `StructuredBuffer` blips and `VehicleSubOsCockpitRuntime.Render` submits `Graphics.RenderMeshIndirect` | Alternative rejected: `DrawMeshInstanced` managed batches | Estimate: `20-70 us/frame` saved at 512-4096 capacity
- [x] Task 3: Physical button raycast fake | DOD: `TryResolvePanelHit` uses local analytical ray-plane intersection | Alternative rejected: `Physics.Raycast` and collider grids | Estimate: `8-25 us/interaction` saved
- [x] Task 4: Button state machine | DOD: fixed `NativeArray<byte>` states use `0 off / 1 transitioning / 2 on`, with target byte lane for deterministic toggles | Alternative rejected: MonoBehaviour bools and allocations | Estimate: `1-5 us/frame` saved under interaction spam
- [x] Task 5: Kinematic button mesh | DOD: Burst `ButtonKinematicJob` interpolates local Z over `0.1s`, uploads `float4x4` dashboard matrices to a `GraphicsBuffer`, and only schedules while dirty/transitioning | Alternative rejected: Animator clips and per-frame idle jobs | Estimate: `15-45 us/frame` saved when idle
- [x] Compile check after Tasks 1-5 | Result: `BLOCKED BY DEPENDENCY`; `VehicleSubOsCockpitRuntime.cs` validates with 0 diagnostics, full build blocked by unrelated `SuitStats/SuitUpgrades` and `AudioEvent` dependency errors

## Loop 2: Tasks 6-10
- [x] Task 6: Off-screen UI rendering | DOD: orthographic off-screen camera targets `VSOS_UI_RT`, bound to central screen material via a reused `MaterialPropertyBlock` | Alternative rejected: world-space Canvas | Estimate: `0.05-0.30 ms` Canvas rebuild avoided
- [x] Task 7: RenderGraph optimization | DOD: UI camera is configured as orthographic, no MSAA/HDR, early depth `-100`, target bound to dashboard material | Alternative rejected: late camera blits and per-frame material swaps | Estimate: `40-120 us/frame` avoided in screen path
- [x] Task 8: Zero-GC text | DOD: O2/Power/Sonar/Status lines use fixed `char[]`, `ZeroGCFormatter`, and `TMP_Text.SetCharArray()` | Alternative rejected: `.text`, interpolation, and `ToString()` | Estimate: `32-160 B/frame` GC eliminated
- [x] Task 9: Power grid integration | DOD: reads `GlobalRegistry.PowerGrid.TryGetGridPowerPotentialsReadOnly` node ratio, falls back to power telemetry; voltage `<0.2` kills radar and dims screen material | Alternative rejected: direct Logistics graph coupling | Estimate: `zero-copy telemetry; 5-20 us/frame` saved
- [x] Task 10: External camera feed | DOD: analytical lever index toggles central screen between UI RT and exterior camera RT; low tier refuses live feed | Alternative rejected: UI button events and always-on camera | Estimate: `0.1-0.4 ms/frame` saved when feed off
- [x] Compile check after Tasks 6-10 | Result: `BLOCKED BY DEPENDENCY`; Unity console has no diagnostics for `VehicleSubOsCockpitRuntime.cs`, project compile still blocked outside assigned UX domain

## Loop 3: Tasks 11-15
- [x] Task 11: Render target pooling | DOD: external camera RT returns to `GlobalRegistry.RenderTexturePool` with depth-aware buckets when the lever turns off, and all live/pooled RTs release in teardown | Alternative rejected: persistent live exterior feed RT and private one-slot pool drift | Estimate: `1.3 MB` VRAM saved at 768x432 ARGB32
- [x] Task 12: Math LOD | DOD: Low/MX350 tier caps radar to `512`, Mid to `2048`, High/Ultra to `4096`; low tier uses static noise texture instead of camera feed | Alternative rejected: balanced single-tier path | Estimate: `up to 87.5%` radar buffer capacity reduction on toaster path
- [x] Task 13: AUP shift safety | DOD: radar blips remain submarine-local and are transformed by the current dome anchor matrix at render; no global shader offset accumulation | Alternative rejected: cached world positions | Estimate: prevents origin-shift tearing
- [x] Task 14: Reconnaissance protocol | DOD: `RECON_VEHICLE_SUB_OS.md` records prefab scan; submarine prefab has no Canvas/CanvasRenderer/GraphicRaycaster hits | Alternative rejected: blind prefab edit | Estimate: prevents unnecessary prefab churn
- [x] Task 15: Telemetry | DOD: fixed 300-frame `NativeArray<CockpitTelemetryEntry>` blackbox plus telemetry bus publishes `RadarActivePoints` and `CockpitInteractions`; NaN detection dumps `Dump_VEHICLE_SUB_OS.bin` | Alternative rejected: `Debug.Log` hot path | Estimate: `0 B/frame` managed telemetry
- [x] Compile check after Tasks 11-15 | Result: `BLOCKED BY DEPENDENCY`; `dotnet build Hecton8.Core.csproj` failed on unrelated `SuitUpgradeManager`, `GameBootstrapper`, and `SpatialAudioManager`

## Loop 4: Recursive Re-Verification
- [x] Re-read prompt from `CURRENT_BATCH.md` | Result: exact `VEHICLE_SUB_OS` XML block re-extracted after core work
- [x] RenderTexture disposal audit | Result: `_externalRenderTexture` target is nulled before pooling; pooled/live/UI RTs release/destroy in `OnDestroy`; no per-frame RT construction
- [x] Damage flicker feasibility | Result: `SetDamageFlicker(float)` and compute `_DamageFlicker` path added; it is a visual fake, not physical simulation
- [x] Compile check after recursive pass | Result: `BLOCKED BY DEPENDENCY`; new cockpit script validates clean, full project compile remains externally broken

## Loop 5: Polish Mandate
- [x] Read `<POLISH_MANDATE>` only after all core tasks were done or blocked | Result: `OMEGA_POLISH` read after Loop 4
- [x] Anti-bloat inquisition | Result: idle button job removed, MPB update gated, telemetry ring modulo replaced with branch wrap, `CockpitTelemetryEntry` padded to 64 bytes
- [x] Final compile / console verification | Result: `BLOCKED BY DEPENDENCY`; dotnet build still fails in non-UX files, Unity script validation reports 0 diagnostics for `VehicleSubOsCockpitRuntime.cs`

## Loop 6: Continuation Hardening
- [x] Optional resource isolation audit | DOD: removed the cockpit-wide `_resourcesReady` early return so power text, external feed, buttons, and telemetry continue even if optional radar compute/material references are absent | Alternative rejected: disabling the whole cockpit because one VFX asset is missing | Estimate: avoids full feature blackout; `0 us/frame` extra hot cost
- [x] Radar density over capacity audit | DOD: compute now expands each published sonar tap into tiered visual replicas: Low/MX350 `32/tap`, Mid `128/tap`, High/Ultra `256/tap`, clamped to `512/2048/4096` capacity | Alternative rejected: one visible blip per 16 audio taps, which wasted the allocated radar buffer | Estimate: `32x-256x` visual density for the same CPU upload count
- [x] Shader blend correctness audit | DOD: restored additive transparent hologram output for `Hecton_RadarBlipInstanced.shader` while retaining procedural `StructuredBuffer` support | Alternative rejected: alpha-test/dithered cutout dots that read as dead pixels instead of holographic sonar | Estimate: no extra CPU; GPU overdraw remains bounded by tiered point count
- [x] Hot-path micro-audit | DOD: swapped analytical division to `math.rcp`, gated `MaterialPropertyBlock.GetPropertyBlock` after state-change checks, and marks button matrix upload dirty after graphics buffer rebuilds | Alternative rejected: redundant MPB reads and silent stale button matrix buffers after LOD changes | Estimate: `2-8 us/frame` saved on stable screen frames
- [x] Compile check after continuation | Result: `BLOCKED BY DEPENDENCY`; `VehicleSubOsCockpitRuntime.cs` and `PDAMapTab.cs` validate with 0 diagnostics; `dotnet build Hecton8.Core.csproj` now reaches a single unrelated `HectonCelestialEngine` interface error

## Loop 7: Visual Fidelity and Compile-Seam Repair
- [x] Prompt re-extraction | Result: first strict regex missed attributes on `<AGENT_PROMPT>`; corrected extraction with `id="VEHICLE_SUB_OS"[^>]*` and re-read the exact prompt cover-to-cover
- [x] Radar billboard fidelity | DOD: procedural radar blip shader now camera-billboards each indirect quad in world space using view inverse axes | Alternative rejected: radar-local XY quads that can go edge-on and lose premium readability | Estimate: no CPU cost; tiny vertex ALU buys stable AAA hologram readability
- [x] Radar compute ALU audit | DOD: replaced inner-loop `sin/cos` calls with wrapped polynomial approximations in `Hecton_CockpitHoloRadar.compute` | Alternative rejected: SFU trig per blip replica on MX350 | Estimate: `3-12 us/4096 blips` GPU-side approximation win pending profiler
- [x] Cold allocation audit | DOD: runtime radar quad now uses predeclared static vertex/UV/index arrays with canonical `COLD ALLOC` comments and `UploadMeshData(true)` | Alternative rejected: anonymous `new[]` arrays inside mesh creation | Estimate: small cold memory cleanup, no frame-time cost
- [x] Blackbox chronology audit | DOD: dump now writes only valid entries in chronological circular-buffer order and includes `entryCount` | Alternative rejected: raw slot-order dumps that force postmortem reconstruction | Estimate: crash-only correctness; no hot-path cost
- [x] Disable-time job safety | DOD: `OnDisable` completes pending button jobs before unregistering from dispatcher lanes | Alternative rejected: letting disabled cockpit instances carry unfinished button jobs until destroy | Estimate: prevents stale transform state on disable/re-enable
- [x] PDA UI compile seam repair | DOD: added missing `TryResolvePointCloudFrame`, `DispatchSonarPointCloud`, and `IsLowMathTier` helpers in `PDAMapTab.cs`; GPU append buffer dispatch now copies append count to indirect args without CPU readback | Alternative rejected: reverting to CPU `SetData` point-cloud upload | Estimate: removes `PDAMapTab` compile errors and keeps PDA sonar on GPU
- [x] Compile check after Loop 7 | Result: `BLOCKED BY DEPENDENCY`; `dotnet build Hecton8.Core.csproj` now has one remaining out-of-domain error in `EncounterDirector.cs(1778,79)` missing `ResolveCheapestAllowedCost`; no `VehicleSubOsCockpitRuntime` or `PDAMapTab` compile errors remain in dotnet output; Unity MCP session unavailable during final validation retry

## Loop 8: PDA Anti-Bloat and Build Closure
- [x] PDA stale CPU point-cloud cleanup | DOD: removed dead `SonarPointCloudPoint`, `NativeArray`, structured buffer, upload flag/count state, and `UploadPointCloudIfNeeded`; PDA sonar now has one compute append + indirect draw route | Alternative rejected: maintaining a disabled CPU `SetData` fallback that contradicted zero-GC/GPU ownership | Estimate: `40-140 us` saved on PDA refresh frames and `~64 KB` stale native/GPU payload avoided
- [x] Stale-symbol scan | DOD: `rg` found no `SonarPointCloudPoint`, `_pointCloudPoints`, `_pointCloudBuffer`, `BuildPointCloudPayload`, `UploadPointCloudIfNeeded`, `SetData`, or `GetData` hits in `PDAMapTab.cs` | Alternative rejected: trusting compile alone while dead code remained | Estimate: prevents future regression, no frame-time claim
- [x] Forbidden hot-path scan | DOD: cockpit/radar files scan clean for `Physics.Raycast`, `GraphicRaycaster`, `Canvas`, `.text =`, `string.Format`, `.ToString()`, `foreach`, `SetData`, and `GetData` | Alternative rejected: manual eyeballing only | Estimate: protects `0 B/frame` UI path
- [x] Compile check after Loop 8 | Result: `PASS`; `dotnet build Hecton8.Core.csproj --no-restore -m:2 /nr:false -v:minimal -clp:Summary` succeeded with `0 errors`, `5 warnings` in unrelated audio/world fields
- [x] Unity validation retry | Result: `PENDING VERIFICATION`; `validate_script` and console reads returned `Unity session not available`, so latest Unity import/shader-console status is not claimed

## Loop 9: Cockpit Hot-Path Inquisition
- [x] Radar redispatch gate | DOD: `VehicleSubOsCockpitRuntime` now caches dispatched sonar sequence, visual point count, power, and damage flicker, skipping `LockBufferForWrite`, compute parameter writes, dispatch, and indirect-args upload when the radar payload is unchanged | Alternative rejected: per-frame radar upload/dispatch with identical DSP taps | Estimate: `10-45 us/frame` CPU submit/bandwidth avoided on stable sonar frames, pending profiler
- [x] Screen MPB allocation audit | DOD: screen `MaterialPropertyBlock` is now a single cold field allocation with no Tick fallback allocation branch; public cockpit API received XML docs | Alternative rejected: lazy hot-path `new MaterialPropertyBlock()` escape hatch | Estimate: protects `0 B/frame`, no fake CPU claim
- [x] Analytical panel/camera property audit | DOD: panel hit conversion uses one `worldToLocalMatrix` snapshot instead of two Transform inverse calls, UI camera properties are only written when state differs, panel extents and external lever index are clamped in `OnValidate` | Alternative rejected: repeated native property churn and authoring-time invalid bounds | Estimate: `1-6 us/interaction/frame` avoided depending on input/camera state
- [x] RenderTexturePool safety audit | DOD: `RenderTexturePool.Rent` clamps width/height to at least `1` before hashing and allocation while preserving depth in the key | Alternative rejected: trusting every caller to pass valid RT dimensions | Estimate: failure-mode hardening, no steady-frame claim
- [x] Forbidden hot-path scan after Loop 9 | Result: cockpit/radar/pool scan found no `SetData`, `GetData`, `Physics.Raycast`, `GraphicRaycaster`, `Canvas`, `.text =`, `.ToString()`, `string.Format`, or `foreach` hits; only cold allocation scan hits remain documented
- [x] Compile check after Loop 9 | Result: `PASS`; `dotnet build Hecton8.Core.csproj --no-restore -m:2 -nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -v:minimal -clp:Summary` succeeded with `0 errors`, `0 warnings`
- [x] Unity validation retry after Loop 9 | Result: `PENDING VERIFICATION`; MCP `validate_script` and console read returned `Unity session not available`, so latest Unity import/shader status is not claimed

## Loop 10: Numeric Vaccination and Runtime Resilience
- [x] Finite-value vaccination | DOD: cockpit ray input, button base positions, telemetry writes, power/O2/CO2/speed snapshots, damage flicker, button progress, radar radius/bounds, and compute shader scalar inputs now fail closed or clamp finite before feeding GPU buffers/native telemetry | Alternative rejected: trusting upstream audio/power/UI values to never emit NaN/Inf | Estimate: crash-prevention hardening, no steady-frame claim
- [x] Radar args upload gate | DOD: `UpdateRadarArgs` caches last instance count and mesh, skipping `LockBufferForWrite` for repeated zero/no-audio and repeated active counts | Alternative rejected: rewriting indirect args every empty radar frame | Estimate: `2-8 us/frame` avoided during silent sonar or stable active count frames, pending profiler
- [x] Optional radar allocation audit | DOD: button matrix buffer remains available, but sonar/blip/args radar buffers are not allocated until the optional radar compute exists; Tick retries allocation if the compute reference appears later | Alternative rejected: reserving radar buffers when radar VFX is absent | Estimate: `~160 KB` GPU/native buffer reservation avoided on missing-compute cockpit instances at 4096 cap
- [x] External feed camera state audit | DOD: live external feed now reasserts target texture/enabled state without reallocating, and release only writes camera properties when they differ | Alternative rejected: one-shot acquisition that could leave a disabled camera with a valid RT | Estimate: correctness fix plus small native property churn reduction
- [x] Unity script validation after Loop 10 | Result: `PASS`; `VehicleSubOsCockpitRuntime.cs`, `RenderTexturePool.cs`, and `PDADataArchaeologyDecryptLabel.cs` validate with `0 errors`, `0 warnings`
- [x] Compile/console check after Loop 10 | Result: `BLOCKED BY DEPENDENCY`; completed `dotnet build` reached out-of-domain `HectonVoxelEngine` missing async mesh helper errors, retry timed out, and Unity console currently reports out-of-domain `SubmarineStructuralGrid` missing `ILateFrameTickable.LateFrameTick()`; no cockpit/radar validation errors

## Loop 10: Radar Gate Correction
- [x] Prompt re-extraction | Result: exact `VEHICLE_SUB_OS` XML block re-extracted with attribute-tolerant regex before continuing post-polish work
- [x] Radar cache predicate repair | DOD: removed `_radarActivePoints != visualPointCount` from `IsRadarDispatchDirty`; `_radarActivePoints` is intentionally reset before the predicate, so that clause forced per-frame `LockBufferForWrite` and compute dispatch | Alternative rejected: trusting Loop 9 status without rereading code | Estimate: restores the claimed `10-45 us/frame` stable-sonar skip path on MX350-class hardware
- [x] Static scan after repair | Result: no stale `_radarActivePoints != visualPointCount` predicate remains; cockpit file has only documented cold `MaterialPropertyBlock` allocation
- [x] Compile check after Loop 10 | Result: `PASS`; `dotnet build Hecton8.Core.csproj --no-restore -m:2 -nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -v:minimal -clp:Summary` succeeded with `0 errors`, `1 warning` in unrelated `WorldSpatialHashGrid`
- [x] Unity validation retry after Loop 10 | Result: `PENDING VERIFICATION`; MCP `validate_script` and console read returned `Unity session not available`, so latest Unity import/shader-console status is not claimed

## Loop 11: Radar Zero-State Cache Hygiene
- [x] Prompt re-extraction | Result: exact `VEHICLE_SUB_OS` XML block re-extracted before the state-transition audit
- [x] Radar no-signal/off-state cache repair | DOD: added `ClearRadarDrawState()` and route resource-missing, power-off, missing-audio, zero-tap, and zero-visual exits through it; this zeros indirect args and invalidates the dispatch cache consistently | Alternative rejected: leaving stale cached sequence alive after a zero-args frame | Estimate: prevents invisible stale-buffer/zero-args recovery bug; no steady-frame fake claim
- [x] Indirect args rewrite guard audit | DOD: existing `_lastRadarArgsInstanceCount`/mesh guard prevents repeated `LockBufferForWrite` calls when the radar remains at zero instances | Alternative rejected: zeroing args every dark/no-audio Tick | Estimate: `2-12 us/frame` avoided on dark/no-signal frames pending profiler
- [x] Static scan after Loop 11 | Result: cockpit/radar/pool scan found no `SetData`, `GetData`, `Physics.Raycast`, `GraphicRaycaster`, `.text =`, `.ToString()`, `string.Format`, or `foreach`; only documented cold `MaterialPropertyBlock` allocation remains in cockpit script
- [x] Unity script validation after Loop 11 | Result: `PASS`; MCP `validate_script Assets/_Project/Scripts/UI/VehicleSubOsCockpitRuntime.cs` returned `0 errors`, `0 warnings`
- [x] Compile check after Loop 11 | Result: `BLOCKED BY DEPENDENCY`; `dotnet build Hecton8.Core.csproj --no-restore -m:2 -nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -v:minimal -clp:Summary` failed only in out-of-domain `HectonVoxelEngine.cs` missing `EnsureVoxelSurfaceMeshAvailableAsync` / `EnsureVoxelPhysicsBakeMeshAvailableAsync`

## Loop 12: Radar Args Rebuild Initialization
- [x] Prompt/domain/mandate recheck | Result: exact `VEHICLE_SUB_OS` XML block re-extracted; domain remains `PRESENTATION & UX / DIEGETIC COCKPIT`; active mandates: diegetic UI zero-GC, GPU compute MX350, AUP/local-space radar, and blackbox evidence logging
- [x] Radar args buffer rebuild audit | DOD: `EnsureGraphicsResources()` now tracks fresh `_radarArgsBuffer` creation, invalidates `_lastRadarArgsInstanceCount`/mesh, and immediately writes zero indirect args after the radar mesh exists | Alternative rejected: trusting a newly allocated raw indirect args buffer to contain safe zero data | Estimate: prevents uninitialized indirect draw on tier/resource rebuild; no steady-frame fake claim
- [x] Static scan after Loop 12 | Result: focused `git diff --check` on touched cockpit/docs files passed; forbidden-pattern scan found no `SetData`, `GetData`, `Physics.Raycast`, `GraphicRaycaster`, `.text =`, `.ToString()`, `string.Format`, or `foreach`; only documented cold `MaterialPropertyBlock` allocation remains
- [x] Compile check after Loop 12 | Result: `PASS`; `dotnet build Hecton8.Core.csproj --no-restore -m:2 -nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -v:minimal -clp:Summary` succeeded with `0 errors`, `0 warnings`
- [x] Unity validation retry after Loop 12 | Result: `PENDING VERIFICATION`; MCP `validate_script Assets/_Project/Scripts/UI/VehicleSubOsCockpitRuntime.cs` returned `Unity session not available`, so latest Unity import/console status is not claimed

## Loop 13: Low-Tier UI RenderTexture Format Gate
- [x] Prompt/domain/mandate recheck | Result: exact `VEHICLE_SUB_OS` XML block re-extracted; mandate evidence reloaded from zero-GC, diegetic UI, MX350 compute, AUP, and crash telemetry docs
- [x] Low-tier UI RT audit | DOD: cockpit off-screen UI RT now resolves `RGB565` on Low/MX350 when supported, falls back to `ARGB32` if unsupported, and treats format mismatch as a reallocation trigger | Alternative rejected: keeping Low/MX350 UI in `ARGB32` despite no alpha requirement | Estimate: saves about `256 KB` color RT memory at the clamped `512x256` low-tier screen; no CPU-frame claim
- [x] High-tier visual preservation | DOD: non-low tiers keep `ARGB32`; the external camera feed stays `ARGB32` because Low/MX350 disables live feed and uses the static noise path | Alternative rejected: globally downgrading all cockpit screen feeds | Estimate: preserves top-tier cockpit fidelity while enforcing toaster VRAM discipline
- [x] Static scan after Loop 13 | Result: focused `git diff --check` passed; scan hits are the documented cold `MaterialPropertyBlock`, the intentional UI format gate, and the high-tier external camera `ARGB32` path; no hot-path string/raycast/SetData/GetData violations
- [x] Compile check after Loop 13 | Result: `PASS`; `dotnet build Hecton8.Core.csproj --no-restore -m:2 -nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -v:minimal -clp:Summary` succeeded with `0 errors`, `0 warnings`
- [x] Unity validation retry after Loop 13 | Result: `PENDING VERIFICATION`; MCP `validate_script Assets/_Project/Scripts/UI/VehicleSubOsCockpitRuntime.cs` returned `Unity session not available`, so latest Unity import/console status is not claimed

## Loop 14: Offscreen UI Camera Dirty-Frame Gate
- [x] Prompt/domain/mandate recheck | Result: exact `VEHICLE_SUB_OS` XML block re-extracted; diegetic UI mandate confirms adaptive RT dirty-frame rendering instead of every-frame panel camera rendering
- [x] UI camera render cadence audit | DOD: off-screen cockpit UI camera now enables only after RT creation/retarget or zero-GC text writes; the next non-dirty Tick disables it again | Alternative rejected: leaving the UI camera enabled at 60Hz while text updates at 10Hz | Estimate: skips up to `5/6` internal UI camera passes on stable screens, pending profiler
- [x] Hidden-feed camera suppression | DOD: `IsOffscreenUiVisible()` prevents internal UI camera renders behind live external feed or Low/MX350 static feed, while lever toggles request a fresh internal-bus render when returning | Alternative rejected: rendering the hidden internal telemetry RT behind the feed | Estimate: avoids hidden UI camera work during exterior feed; no fake microsecond claim
- [x] Static scan after Loop 14 | Result: focused `git diff --check` passed; no `SetData`, `GetData`, `Physics.Raycast`, `GraphicRaycaster`, `.text =`, `.ToString()`, `string.Format`, or `foreach`; only documented cold MPB and intentional camera-enable gates remain
- [x] Compile check after Loop 14 | Result: `BLOCKED BY DEPENDENCY`; `dotnet build Hecton8.Core.csproj --no-restore -m:2 -nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -v:minimal -clp:Summary` failed only in out-of-domain `HectonPlayerMovement.cs` missing `IPostFixedTickable.PostFixedTick(float)`
- [x] Unity validation retry after Loop 14 | Result: `PENDING VERIFICATION`; MCP `validate_script Assets/_Project/Scripts/UI/VehicleSubOsCockpitRuntime.cs` returned `Unity session not available`, so latest Unity import/console status is not claimed

## Loop 15: Authoring Guardrails for Cockpit Screen Targets
- [x] Prompt/domain/mandate recheck | Result: exact `VEHICLE_SUB_OS` XML block re-extracted; domain remains `PRESENTATION & UX / DIEGETIC COCKPIT`; active mandates re-read from zero-GC, URP hot path, performance budget, and visual-fake-first docs
- [x] Screen target dimension guard | DOD: cockpit UI RT now clamps to a usable `256x128` minimum and Low/MX350 caps at `512x256`; live external feed clamps to at least `256x144` while High/Ultra can still author larger RTs | Alternative rejected: accepting `16x16` serialized targets that technically render but destroy cockpit readability | Estimate: failure-mode hardening; `0 us/frame` hot-path cost, prevents unreadable/badly bucketed RT allocations
- [x] Physical button grid guard | DOD: analytical hit math and fallback button placement now resolve `buttonColumns`/`buttonRows` through `1..32` clamps and cap grid capacity before button count resolution | Alternative rejected: unbounded serialized grid values leaking into index math or panel layout | Estimate: failure-mode hardening; no steady-frame claim
- [x] Static scan after Loop 15 | Result: focused `git diff --check` passed; forbidden-pattern scan found no `SetData`, `GetData`, `Physics.Raycast`, `GraphicRaycaster`, `Canvas`, `.text =`, `.ToString()`, `string.Format`, or `foreach`; only documented cold `MaterialPropertyBlock` remains
- [x] Compile check after Loop 15 | Result: `PASS`; `dotnet build Hecton8.Core.csproj --no-restore -m:2 -nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -v:minimal -clp:Summary` succeeded with `0 errors`, `0 warnings`
- [x] Unity validation retry after Loop 15 | Result: `PENDING VERIFICATION`; MCP `validate_script Assets/_Project/Scripts/UI/VehicleSubOsCockpitRuntime.cs` and console read returned `Unity session not available`, so Unity import/console status is not claimed

## Loop 16: Text-Diff and Radar Binding Gate
- [x] Prompt re-extraction | Result: exact `VEHICLE_SUB_OS` XML block re-extracted after user requested continued polish and explicitly forbade `dotnet build`
- [x] Offscreen text diff gate | DOD: cockpit PWR/O2/SONAR/STATUS text now caches integer display buckets and writes TMP only when visible values change; `_screenUpdateAccumulator` saturates at the `0.1s` cadence instead of growing while hidden | Alternative rejected: rewriting all four TMP meshes every cadence tick when text content is identical | Estimate: avoids avoidable TMP mesh dirties and hidden UI RT render requests; pending profiler, no fake microsecond claim
- [x] Hidden UI render persistence | DOD: `ApplyOffscreenUiCameraState()` no longer clears `_offscreenUiCameraRenderRequested` while the internal bus is hidden by external/static feed; the internal RT gets a fresh render when it becomes visible again | Alternative rejected: clearing the request behind an exterior feed and risking stale internal telemetry on return | Estimate: correctness hardening; no steady-frame claim
- [x] Radar material binding gate | DOD: radar blip buffer and procedural flag bind once per buffer identity, while the per-frame local-to-world matrix still updates; render path now validates the full matrix before indirect draw | Alternative rejected: rebinding the same `GraphicsBuffer` every render and trusting transform matrices to be finite | Estimate: small driver-call reduction on stable radar frames, pending profiler
- [x] Radar drawable readiness gate | DOD: sonar compute dispatch now requires a drawable radar material/mesh, while compute buffers remain optional/retryable through `ShouldRetryRadarGraphicsResources()` | Alternative rejected: dispatching radar compute when no hologram can render | Estimate: avoids wasted GPU submit when VFX draw assets are missing; no steady-frame claim
- [x] Static scan after Loop 16 | Result: forbidden-pattern scan found no `SetData`, `GetData`, `Physics.Raycast`, `GraphicRaycaster`, `Canvas`, `.text =`, `.ToString()`, `string.Format`, or `foreach`; `new` scan contains only documented cold/init/struct sites; focused `git diff --check` reported no whitespace errors
- [x] Compile/build check after Loop 16 | Result: `NOT RUN BY USER INSTRUCTION`; no `dotnet build`, Unity compile, or Unity validation command was executed in this loop

## Loop 17: UI RenderTexture Teardown Detach
- [x] UI RT lifetime audit | DOD: `ReleaseUiRenderTexture()` now clears `offscreenUiCamera.targetTexture` before destroying `_uiRenderTexture`, and both quality-tier reallocation plus `OnDestroy` use the same teardown helper | Alternative rejected: relying on Unity disable/destroy ordering to clear a camera reference to a released RT | Estimate: failure-mode hardening; no steady-frame claim
- [x] Static scan after Loop 17 | Result: focused `git diff --check` reported no whitespace errors; forbidden-pattern scan found no `SetData`, `GetData`, `Physics.Raycast`, `GraphicRaycaster`, `Canvas`, `.text =`, `.ToString()`, `string.Format`, or `foreach`; only documented cold `MaterialPropertyBlock` remains
- [x] Compile check after Loop 17 | Result: `BLOCKED BY DEPENDENCY`; initial `--no-restore` build hit missing generated `project.assets.json` files, restore succeeded, then `dotnet build -m:1 --no-restore` failed only in out-of-domain `HectonVoxelEngine.cs` and `HectonFluidEngine.cs`
- [x] Unity validation retry after Loop 17 | Result: `PENDING VERIFICATION`; MCP `validate_script Assets/_Project/Scripts/UI/VehicleSubOsCockpitRuntime.cs` failed because the local Unity MCP HTTP transport at `127.0.0.1:8088` was unavailable
