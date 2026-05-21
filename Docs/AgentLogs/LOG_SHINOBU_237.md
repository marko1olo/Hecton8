# LOG_SHINOBU_237

## 2026-05-21 - Silt And Propwash GPU Director

What was wrong:
- Propwash/silt requirement had no hard proof that CPU ParticleSystem/raycast wake authority was gone.
- Existing marine snow GPU path existed, but propwash events had no dedicated 32B DTO ring, no SDF proximity event dispatch, no 300-frame propwash black box, and no SHINOBU_237 self-audit artifact.
- Existing draw path used mesh indirect submission; prompt required `Graphics.DrawProceduralIndirect`.

What was done:
- Added `PropwashEventDTO` explicit 32B ABI and related raw unmanaged DTOs in `Assets/_Project/Scripts/VFX/PropwashGpuContracts.cs`.
- Added Burst `GenerateMockPropwashEventsJob` for 500 deterministic events and `HarvestKinematicWakeJob` using `UnsafeUtility.AsRef` with camera-AUP subtraction.
- Added DataVault ids `71492..71495` for event ring, cursor, telemetry ring, and tuning.
- Integrated propwash event upload, Vault-backed tuning, biome tint, telemetry, black-box dump, editor gizmo, and `DispatchWakeProximityInjection` into `HectonMarineSnowRenderer`.
- Added compute kernels `CS_EvaluateWakeProximity`, `CS_IntegrateSiltParticles`, and `CS_RebaseParticles`; main particle kernel now consumes propwash flow.
- Switched renderer submission to `Graphics.DrawProceduralIndirect`; `_MarineSnowIndirectArgs` remains GPU-written and CPU unread.
- Added `PropwashGpuLayoutValidator`, `PropwashGpuTunerWindow`, and `Particle_System_Scanner`.
- Updated `Docs/ARCHITECTURE/PROPWASH_GPU_DIRECTOR_SHINOBU_237.md`, `Docs/Reports/RENDERING_OPTIMIZATION_REPORT.json`, and `Docs/Reports/SHINOBU_237_SELF_AUDIT.xml`.

Cinematic cheats used:
- Triangle-wave mock thrust instead of fluid simulation for stress data.
- GPU "Dear Lie" proximity from SDF/height textures instead of CPU physics truth.
- Radial/lift propwash flow and fake curl/noise sampling instead of hydrodynamic Navier-Stokes.
- Continuous sample-budget scaling instead of binary tiers.

Exact microseconds saved, static estimate:
- CPU seabed raycasts in owned propwash/silt path: 0 calls remain, estimated saved 30-250 us per active wake cluster versus synchronous broadphase query.
- CPU ParticleSystem emit/simulation in owned propwash/silt path: 0 emit calls remain, estimated saved 80-700 us at dense wake loads.
- CPU visible particle count readback: 0 readbacks, estimated saved 40-200 us and one GPU/CPU sync hazard.
- CPU event upload: bounded 512 * 32B = 16384 bytes; target cost <25 us for mock event copy, profiler pending.
- Propwash telemetry write: one 64B record per frame, target cost <5 us.

Verification:
- `rg` found 0 `Physics.Raycast`/`RaycastNonAlloc` hits in `Assets/_Project/Scripts/VFX`.
- `rg` found 0 `.Emit(` or `ParticleSystem.Emit` hits in `Assets/_Project/Scripts/VFX`.
- Remaining `ParticleSystem` hits are `CameraJuiceSystem` camera-local speed lines, no emission calls, no collision module, not propwash/silt authority.
- `rg` found `Graphics.DrawProceduralIndirect` in `HectonMarineSnowRenderer`.
- `rg` found 0 `RenderMeshIndirect` hits in touched renderer/report.
- Build was not launched: CPU load was 100, exceeding the project gate of 50; no `dotnet/csc` process was active.

<SELF_AUDIT agent="SHINOBU_237" status="CODE_COMPLETE_BUILD_BLOCKED_BY_CPU_GATE">
  <byteLayout name="PropwashEventDTO" sizeBytes="32" offsets="LocalPosition:0,ThrustVector:12,Intensity:24,Radius:28" />
  <vaultBuffer id="71492" name="PropwashGpuEventRing" capacity="512" strideBytes="32" />
  <vaultBuffer id="71493" name="PropwashGpuRingCursor" capacity="1" strideBytes="32" />
  <vaultBuffer id="71494" name="PropwashGpuTelemetryRing" capacity="300" strideBytes="64" />
  <vaultBuffer id="71495" name="PropwashGpuTuning" capacity="1" strideBytes="32" />
  <hotPath managedAllocBytes="0" evidence="NativeArray/GraphicsBuffer/Burst/static scan; profiler pending because build gate blocked" />
  <forbiddenPaths particleEmitCalls="0" propwashRaycasts="0" forbiddenWakeParticles="0" />
  <draw api="Graphics.DrawProceduralIndirect" cpuVisibleCountReadback="false" />
  <build status="NOT_RUN" reason="CPU load 100 > 50 gate" />
</SELF_AUDIT>

## 2026-05-21 - Ultra Polish Pass, Static Patch

What was wrong:
- Propwash helper names hid owner mutation: `TryResolvePropwash*` could rebind/ensure Vault state from a read-looking path.
- GPU event upload used one `_PropwashEvents` buffer, leaving a possible CPU lock/write hazard against the previous GPU consumer.
- Task 17 was only partially represented by key-value tuning; `vehicle_wake_profiles.csv` had no dedicated Vault lookup table.
- Editor facade exposed sliders but did not draw the requested direct telemetry waterfall.
- Low-quality compute sampling still carried a 24-event floor, too high for severe thermal collapse.

What was done:
- Replaced propwash read-like helpers with `TryAcquireReadyPropwash*` methods that only use already-created Vault handles.
- Renamed propwash parameter construction to `BuildPropwash*` and retained mutating cache behavior only in `CapturePropwashTuningSnapshot`.
- Added `PropwashWakeProfileDTO` as a 64B row, `PropwashGpuWakeProfiles=71496`, a cold `vehicle_wake_profiles.csv` parser, and an editor/source-data CSV under `Assets/_SourceData/VFX/Propwash`.
- Added `_propwashEventBufferA/B`; uploads now write the inactive buffer through `LockBufferForWrite` before publishing it to compute.
- Added `TelemetryWaterfallElement` to `PropwashGpuTunerWindow`; it draws particle budget and GPU microsecond curves from `PropwashGpuTelemetryRing`.
- Changed HLSL propwash event sampling to curve continuously from 4 samples to active count with `curvedQuality`.

Cinematic cheats used:
- Continued rejection of CPU seabed truth; SDF/height/depth compute path remains the terrain-proximity lie.
- Wake profiles tune scalar emission, lift, curl, and tint only; no CPU fluid state or hydrodynamic solver was introduced.

Exact microseconds saved, static estimate:
- Hidden Vault ensure/rebind from propwash read path: removed; exact frame savings profiler-pending, spike class removed.
- Single-buffer GPU upload hazard: replaced by double-buffer; expected driver fence avoidance on low-end discrete/UMA paths.
- Low-quality event sampling: 4-event floor instead of 24, theoretical propwash flow loop cost floor reduced by 83.3%.
- Editor telemetry waterfall: editor-only, gameplay cost 0 us.

Verification:
- Static scan: 0 `TryResolvePropwash` and 0 `ResolvePropwash` hits remain in touched renderer.
- Static scan: touched propwash renderer still uses `Graphics.DrawProceduralIndirect`; touched files have no `RenderMeshIndirect`.
- Static scan: Burst propwash jobs retain exact Fast/Standard synchronous compile attributes and `[NoAlias]` fields.
- Build not launched yet; CPU gate must be rechecked before any dotnet invocation.

<SELF_AUDIT agent="SHINOBU_237" status="STATIC_PATCHED_BUILD_GATED">
  <taskCount>20</taskCount>
  <layout name="PropwashEventDTO" bytes="32" math="12+12+4+4=32" />
  <layout name="PropwashWakeProfileDTO" bytes="64" math="16x4B lanes=64" />
  <vaultBuffer id="71496" name="PropwashGpuWakeProfiles" capacity="64" strideBytes="64" />
  <gpuUpload propwashEvents="double-buffered" buffers="_propwashEventBufferA,_propwashEventBufferB" />
  <qualityCurve lowSamples="4" ultraSamples="activeCount" switchType="continuous smoothstep" />
  <compileGuard status="PENDING_CPU_GATE" />
</SELF_AUDIT>

## 2026-05-21 - Player IO Fence Pass

What was wrong:
- Wake profile CSV staging had to be proven source-data/editor-only. A player `StreamingAssets` route would violate the current binary-payload ledger and risk platform file IO stutter.

What was done:
- Kept `vehicle_wake_profiles.csv` under `Assets/_SourceData/VFX/Propwash`.
- Confirmed `Assets/StreamingAssets/vehicle_wake_profiles.csv` is absent.
- Scoped wake profile staging fields and parse refresh to `UNITY_EDITOR`; player builds retain deterministic default `PropwashGpuWakeProfiles` rows until a VFX `.h8bin` or Data Monolith hydration route is approved.

Cinematic cheats used:
- No additional simulation. Player runtime keeps scalar defaults and lets GPU SDF/depth/curl math fake the wake visual.

Exact microseconds saved, static estimate:
- Player wake-profile file polling: 0 us, removed from player compile path.
- Player wake-profile managed staging arrays: 0 bytes, removed from non-editor compile path.

Verification:
- `Assets/StreamingAssets/vehicle_wake_profiles.csv`: absent.
- `Assets/_SourceData/VFX/Propwash/vehicle_wake_profiles.csv`: present.
- Static scan still reports 0 propwash `TryResolve*`/`ResolvePropwash*` read-like names in touched renderer.
- Build gate rechecked: CPU load 100, no active `dotnet/csc`; build not launched.

## 2026-05-21 - Burst Immediate-Run Fence

What was wrong:
- Four VFX wake jobs had correct Burst compile attributes but were called through direct `Execute()`, which could leave immediate wake math outside the Burst route.

What was done:
- Replaced the SHINOBU propwash mock call with `propwashJob.Run()`.
- Replaced the scalar vehicle wake, mock flow, and mock dynamic wake call sites with `Run()` as well. These are local renderer VFX jobs, not foreign domain ownership.

Cinematic cheats used:
- No CPU fluid solver was added. The fallback still writes compact thrust events; SDF/depth/curl compute kernels fake seabed silt response and wake turbulence.

Exact microseconds saved, static estimate:
- Managed immediate calls removed for 1 vehicle wake row, 1 flow row, up to 500 propwash rows, and mock dynamic wake rows. Profiler number pending; this closes the architectural defect before compile.

Verification:
- Static scan: `.Execute()` job call-sites are absent from `HectonMarineSnowRenderer`.
- Static scan: `job.Run()`, `mockFlowJob.Run()`, `propwashJob.Run()`, and `mockWakeJob.Run()` are present.
- Build gate rechecked after the broader immediate-run patch: CPU load 100, no active `dotnet/csc`; build not launched.

## 2026-05-21 - Vehicle Command Propwash Bridge

What was wrong:
- Vehicle wake commands were converted to `FluidImpulseSignal`, but the propwash compute event ring could still be driven only by mock/harvest paths. Real throttle needed a compact Burst bridge into `_PropwashEvents`.

What was done:
- Added `CommitVehicleWakePropwashEventJob` in `PropwashGpuContracts`.
- `PublishVehicleWakeImpulse` now writes one real vehicle wake result into `PropwashGpuEventRing`, updates `PropwashGpuRingCursor`, and uploads through the existing propwash double-buffer.
- Local position is produced by converting event and camera runtime positions to AUP, subtracting in double precision, and only then casting to `float3`.

Cinematic cheats used:
- The vehicle command is reduced to one scalar thrust DTO. The GPU still owns SDF/depth proximity, silt spawn, curl advection, and particle draw.

Exact microseconds saved, static estimate:
- Avoids any CPU particle/raycast response for real vehicle throttle. Added CPU cost is one cooldown-gated Burst `IJob.Run()` and a bounded 16 KB upload.

Verification:
- Static scan: `CommitVehicleWakePropwashEventJob` exists and is called from `PublishVehicleWakeImpulse`.
- Static scan: forbidden propwash `.Emit()`, `Physics.Raycast`, `RaycastNonAlloc`, `TryResolvePropwash`, and `ResolvePropwash` hits remain 0 in touched files.
- Build gate rechecked after the bridge patch: CPU load 100, no active `dotnet/csc`; build not launched.

## 2026-05-21 - Unity Profile Finite Guard

What was wrong:
- Touched SHINOBU renderer paths used `float.IsFinite`, which can fail on Unity profile/compiler combinations even when `math.isfinite` is already available.
- Direct local wake `IJob.Execute()` call-sites were visible again in the renderer after the bridge edit.

What was done:
- Replaced `float.IsFinite` with `math.isfinite`.
- Replaced direct local wake job `Execute()` call-sites with `Run()`.

Cinematic cheats used:
- No simulation expansion. This pass preserves the compact thrust DTO and GPU SDF/depth/curl fake.

Exact microseconds saved, static estimate:
- Runtime visual cost unchanged. Compatibility and Burst-route risk removed before build.

Verification:
- Static scan: direct local wake job `Execute()` call-sites are absent; `Run()` call-sites are present.
- Static scan: `float.IsFinite` and `double.IsFinite` are absent in touched SHINOBU renderer/contracts/editor files.
- Build gate rechecked: CPU load 97, no active `dotnet/csc`; build not launched.

## 2026-05-21 - Strict Burst Run Repatch

What was wrong:
- A fresh strict scan still found five direct local wake `IJob.Execute()` call-sites in `HectonMarineSnowRenderer`, contradicting the previous status text.

What was done:
- Replaced the scalar vehicle wake bridge, vehicle propwash commit bridge, mock flow, mock propwash, and mock dynamic wake call-sites with `Run()`.
- Re-ran the exact scan: only `Run()` call-sites remain for those local wake jobs.

Cinematic cheats used:
- No CPU particle path added. This preserves compact thrust DTOs and lets compute handle SDF proximity, silt emission, curl/noise advection, and indirect draw.

Exact microseconds saved, static estimate:
- Prevents managed direct execution on five local wake preparation paths. Runtime delta needs Unity Burst profiler; build/profiler gate is blocked by machine load.

Verification:
- Static scan: `job.Execute()`, `mockFlowJob.Execute()`, `propwashJob.Execute()`, and `mockWakeJob.Execute()` are absent from touched SHINOBU renderer/contracts files.
- Static scan: `job.Run()`, `mockFlowJob.Run()`, `propwashJob.Run()`, and `mockWakeJob.Run()` are present.
- Static scan: forbidden `float.IsFinite`, `double.IsFinite`, `Pack=1`, propwash `.Emit()`, and propwash raycast hits remain 0 in touched SHINOBU files.
- Build gate rechecked: CPU load 100, no active `dotnet/csc`; build not launched.

## 2026-05-21 - Propwash Vault Compaction Fence

What was wrong:
- Propwash Vault read helpers skipped the `_dataVault.IsCompactionFenceActive` guard that adjacent VFX resolver helpers already use.

What was done:
- Added compaction-fence rejection to all five propwash accessors: events, cursor, telemetry, tuning, and wake profiles.

Cinematic cheats used:
- No gameplay truth route changed. If Vault memory is compacting, the cosmetic propwash read is skipped and the GPU visual continues from the last bound buffer/default parameters.

Exact microseconds saved, static estimate:
- Not an ALU optimization. Cost is one branch per accessor; saved failure mode is undefined NativeArray access during compaction.

Verification:
- Static readback confirms each `TryAcquireReadyPropwash*` helper now checks `_dataVault.IsCompactionFenceActive` before `Resolve`.
- Static scan: local wake job call-sites remain `Run()`, no direct local wake `Execute()` call-sites in touched SHINOBU renderer/contracts files.
- Static scan: forbidden finite helpers, `Pack=1`, propwash `.Emit()`, and propwash raycast hits remain 0.
- Build gate rechecked: CPU load 100, no active `dotnet/csc`; build not launched.

## 2026-05-21 - Editor Facade Vault Fence

What was wrong:
- The UI Toolkit tuner could write `PropwashGpuTuning` or resolve `PropwashGpuTelemetryRing` during Vault compaction.

What was done:
- Added `IDataVault.IsCompactionFenceActive` guards before tuning writes, telemetry handle binding, and waterfall paint-time resolve.

Cinematic cheats used:
- No rendering path changed. This keeps editor controls as a tuning bridge over compact DTOs, not a gameplay simulation path.

Exact microseconds saved, static estimate:
- Gameplay: 0 us, editor-only. Risk removed: unsafe native access from Play Mode tooling during compaction.

Verification:
- Static scan: `PropwashGpuTunerWindow` contains compaction-fence checks before tuning and telemetry Vault access.
- Static scan: forbidden finite helpers, `Pack=1`, propwash `.Emit()`, propwash raycast hits, and propwash `TryResolve*`/`ResolvePropwash*` names remain 0.
- Build gate rechecked: CPU load 100, no active `dotnet/csc`; build not launched.

## 2026-05-21 - Gizmo Vault Fence Reuse

What was wrong:
- `OnDrawGizmosSelected` directly resolved `_propwashEventHandle`, bypassing the fenced propwash accessor.

What was done:
- Replaced the direct resolve with `TryAcquireReadyPropwashEvents`.

Cinematic cheats used:
- None; this is editor debug plumbing. The live visual remains GPU-authored.

Exact microseconds saved, static estimate:
- Gameplay: 0 us, editor-only. Risk removed: scene-view gizmo read during Vault compaction.

Verification:
- Static scan: `_propwashEventHandle.Resolve` appears only inside `TryAcquireReadyPropwashEvents`.
- Static scan: forbidden finite helpers, `Pack=1`, propwash `.Emit()`, propwash raycast hits, and propwash read-like resolver names remain 0.
- Build gate rechecked: CPU load 100, no active `dotnet/csc`; build not launched.

## 2026-05-21 - Biome Tint Render Route

What was wrong:
- Compute tagged propwash silt and the renderer published `_PropwashBiomeTint`, but the material shader did not consume that flag/tint in the visible RGB path.

What was done:
- Added `_PropwashBiomeTint` to `Hecton_MarineSnow.shader`.
- Added flag-8 propwash silt color selection so visible silt particles lerp to biome RGB.
- Published the same Vault-backed tint vector to compute and material, with cached material updates to avoid redundant `SetVector` calls.

Cinematic cheats used:
- Biome difference is a shader color fake over compact propwash particles. No CPU sediment material simulation, no per-particle CPU color writes, no terrain material lookup loop.

Exact microseconds saved, static estimate:
- Avoids adding per-particle RGB lanes or CPU-side biome sorting. Runtime CPU cost is one material vector update only when the tint changes; GPU cost is one color lerp for visible propwash silt.

Verification:
- Static scan: `_PropwashBiomeTint` is present in compute, renderer, and material shader.
- Static scan: `Hecton_MarineSnow.shader` tests particle flag `8u` and uses `_PropwashBiomeTint.rgb`.
- Static scan: forbidden finite helpers, `Pack=1`, propwash `.Emit()`, propwash raycast hits, and propwash read-like resolver names remain 0.
- Build gate rechecked after the code patch: CPU load 100, no active `dotnet/csc`; build not launched.

## 2026-05-21 - HLSL Resolver Name Fence

What was wrong:
- Broadening the forbidden read-like resolver scan to touched HLSL found `ResolvePropwashEventFlow`. The function did not mutate state, but the name weakened static proof.

What was done:
- Renamed `ResolvePropwashEventFlow` and its call-sites to `ComputePropwashEventFlow`.

Cinematic cheats used:
- No simulation change. The shader still computes compact radial/curl propwash flow from event DTOs instead of CPU particles or seabed physics.

Exact microseconds saved, static estimate:
- 0 us runtime. The gain is audit strictness: no exception list for `ResolvePropwash*`.

Verification:
- Static scan: forbidden finite helpers, `Pack=1`, propwash `.Emit()`, propwash raycast hits, `TryResolvePropwash`, and `ResolvePropwash` hits are 0 across touched C#/HLSL files.
- Static scan: `ComputePropwashEventFlow` is present at the helper and two call-sites.
- Build gate rechecked again after static scans: CPU load 98.49, no active `dotnet/csc`; build not launched.

## 2026-05-21 - Cursor-Aware Propwash GPU Upload

What was wrong:
- `UploadPropwashEventGpuBuffer` copied the first `activeCount` Vault rows and ignored `PropwashRingCursorDTO.WriteCursor`. After a ring wrap, that could upload a stale prefix instead of the current wake window.

What was done:
- Changed propwash GPU upload to accept `PropwashRingCursorDTO`.
- Added `ComputePropwashUploadStart` and `WrapPropwashUploadIndex`.
- Vehicle wake and mock wake upload call-sites now pass the cursor row instead of a detached count.

Cinematic cheats used:
- No CPU simulation added. This preserves the compact DTO ring and keeps SDF proximity, silt spawn, advection, and visible count on GPU.

Exact microseconds saved, static estimate:
- Adds at most 512 integer wraps during upload; avoids any CPU sort or ring normalization pass. Upload remains bounded at 16 KB.

Verification:
- Static scan: active-count-only `UploadPropwashEventGpuBuffer` call-sites are absent.
- Static scan: forbidden finite helpers, `Pack=1`, propwash `.Emit()`, propwash raycast hits, `TryResolvePropwash`, and `ResolvePropwash` hits remain 0 across touched C#/HLSL files.
- Build gate rechecked: CPU load 100, no active `dotnet/csc`; build not launched.

## 2026-05-21 - Editor Status String Purge

What was wrong:
- `PropwashGpuTunerWindow` concatenated a live tuning version into a Label status message on apply.

What was done:
- Replaced the dynamic status message with a constant string.

Cinematic cheats used:
- None; this is editor facade hygiene. The live simulation remains compute-driven.

Exact microseconds saved, static estimate:
- Gameplay: 0 us. Editor: removes one transient managed string per apply.

Verification:
- Static scan: `string.Format`, interpolated strings, `foreach`, `ToList`, and `ToArray` are absent in `PropwashGpuTunerWindow`.
- Static scan: `SetStatus("Applied PropwashGpuTuning.")` is the apply success route.

## 2026-05-21 - CSV Numeric Parser Fail-Closed

What was wrong:
- `PropwashGpuProfileCsvParser.TryParseFloat` accepted partial tokens. A malformed `1abc` field would hydrate as `1` instead of rejecting the row.

What was done:
- Added a full-token consumption check after sign/integer/fraction parsing.

Cinematic cheats used:
- None; this is cold authoring-data validation. The runtime visual remains compact DTOs plus compute.

Exact microseconds saved, static estimate:
- Gameplay: 0 us. Cold parse cost: one integer compare per numeric token. Saved failure mode: silent corrupt wake-profile/tuning hydration.

Verification:
- Static readback: parser now returns false when parsed index does not equal token length.
- Static scan: forbidden finite helpers, `Pack=1`, propwash `.Emit()`, propwash raycast hits, `TryResolvePropwash`, and `ResolvePropwash` hits remain 0 across touched C#/HLSL files.
- Build gate rechecked: CPU load 85.12, no active `dotnet/csc`; build not launched.

## 2026-05-21 - CSV Optional Field Fail-Closed

What was wrong:
- Wake profile optional fields ignored malformed present tokens and retained default values, allowing bad authoring rows to hydrate silently.

What was done:
- Changed optional parse semantics: absent or empty optional columns are allowed; present malformed columns reject the row.

Cinematic cheats used:
- None; this is cold data hygiene for the tuning bridge.

Exact microseconds saved, static estimate:
- Gameplay: 0 us. Cold parse adds one branch per optional field and prevents silent profile corruption.

Verification:
- Static readback: `TryApplyWakeProfileLine` rejects when any `TryParseOptionalFloat` call fails.
- Static readback: `TryParseOptionalFloat` returns true for absent/empty columns and false for malformed present columns.
- Static scan: forbidden finite helpers, `Pack=1`, propwash `.Emit()`, propwash raycast hits, `TryResolvePropwash`, and `ResolvePropwash` hits remain 0 across touched C#/HLSL files.
- Build gate rechecked: CPU load 100, no active `dotnet/csc`; build not launched.

## 2026-05-21 - Procedural Indirect Args ABI

What was wrong:
- `Graphics.DrawProceduralIndirect` used a buffer allocated with indexed indirect args size, and the compute clear kernel wrote an unused fifth uint at offset 16.

What was done:
- Added a 16-byte procedural indirect args stride in the renderer.
- Removed the unused offset-16 store from `ClearVisibleParticles`.

Cinematic cheats used:
- No CPU count path. GPU still owns visible particle count via `_MarineSnowIndirectArgs.InterlockedAdd(4, 1u, visibleIndex)`.

Exact microseconds saved, static estimate:
- One raw UAV store removed from the clear kernel; args buffer shrinks by 4 bytes. Main value is ABI correctness, not throughput.

Verification:
- Static scan: `DrawProceduralIndirect` remains the draw API; `RenderMeshIndirect` remains absent from touched renderer/report.
- Static scan: `_MarineSnowIndirectArgs.Store` clears offsets 0/4/8/12 only.
- Static scan: forbidden finite helpers, `Pack=1`, propwash `.Emit()`, propwash raycast hits, `TryResolvePropwash`, and `ResolvePropwash` hits remain 0 across touched C#/HLSL files.
- Build gate rechecked: CPU load 100, no active `dotnet/csc`; build not launched.

## 2026-05-21 - Continuous Proximity Event Budget

What was wrong:
- `CS_EvaluateWakeProximity` could run across the full uploaded propwash event ring while flow/advection sampling was already quality-scaled.

What was done:
- Added C# `ComputePropwashEventSampleBudget`.
- Added HLSL `ComputePropwashEventSampleBudget`.
- Wake proximity dispatch and shader guard now use the same continuous budget.

Cinematic cheats used:
- Low quality now spends fewer SDF/height proximity samples, preserving the visual fake instead of simulating every possible wake contact.

Exact microseconds saved, static estimate:
- Low-quality path drops proximity dispatch from up to 512 event threads to the smooth sample budget. CPU terrain query remains 0 us.

Verification:
- Static scan: proximity dispatch uses `ComputePropwashEventSampleBudget(_debugPropwashEventCount, quality)`.
- Static scan: `CS_EvaluateWakeProximity` rejects `eventIndex >= sampleBudget`.
- Static scan: forbidden finite helpers, `Pack=1`, propwash `.Emit()`, propwash raycast hits, `TryResolvePropwash`, and `ResolvePropwash` hits remain 0 across touched C#/HLSL files.
- Build gate rechecked: CPU load 100, no active `dotnet/csc`; build not launched.

## 2026-05-21 - Parallel Harvest Ring Clamp

What was wrong:
- `HarvestKinematicWakeJob` could wrap parallel write indices if a future kinematic source table exceeded the 512-row event ring.

What was done:
- Clamped the processed source count to `capacity` before the parallel write guard.

Cinematic cheats used:
- No extra simulation. CPU still writes compact thrust DTOs; shader still owns SDF proximity, silt spawn, advection, and indirect visibility.

Exact microseconds saved, static estimate:
- Adds one integer min in the Burst guard. Prevents a future race without atomics, CPU sorting, or shader circular indexing.

Verification:
- Static scan: `HarvestKinematicWakeJob` count guard is `math.min(math.min(SourceCount, Sources.Length), capacity)`.
- Static scan: forbidden finite helpers, `Pack=1`, propwash `.Emit()`, propwash raycast hits, `TryResolvePropwash`, and `ResolvePropwash` hits remain 0 across touched C#/HLSL files.
- `git diff --check` for the touched C#/docs set is clean.
- Build gate rechecked: CPU load 92.29, no active `dotnet/csc`; build not launched.

## 2026-05-21 - Deterministic Script Metadata

What was wrong:
- New SHINOBU propwash C# files had no `.meta` files, leaving GUID assignment to Unity import timing.

What was done:
- Added MonoImporter `.meta` files for `PropwashGpuContracts.cs`, `PropwashGpuTunerWindow.cs`, and `PropwashGpuLayoutValidator.cs`.

Cinematic cheats used:
- None. This is asset identity hygiene, not runtime simulation.

Exact microseconds saved, static estimate:
- Gameplay: 0 us. Prevents import churn and cross-agent GUID instability.

Verification:
- Static scan: each new GUID occurs exactly once under `Assets`.
- `git diff --check` for the touched C#/meta/docs set is clean.
- Build gate rechecked: CPU load 100, no active `dotnet/csc`; build not launched.

## 2026-05-21 - Dedicated Rendering Scanner Artifact

What was wrong:
- The shared `Docs/Reports/RENDERING_OPTIMIZATION_REPORT.json` is currently owned at the top level by SHINOBU_235 and only nests the older SHINOBU_237 report.

What was done:
- Added `Docs/Reports/RENDERING_OPTIMIZATION_REPORT_SHINOBU_237.json` with current SHINOBU_237 scanner metrics and explicit shared-report collision note.

Cinematic cheats used:
- Scanner proof records the same route: compact Vault DTOs feed GPU SDF/depth/curl particles; CPU ParticleSystem/raycast wake authority is absent.

Exact microseconds saved, static estimate:
- Runtime: 0 us added. Preserves proof that owned wake/silt path has 0 CPU emit calls and 0 CPU raycasts.

Verification:
- `ConvertFrom-Json` parsed the dedicated report.
- Report records `forbiddenCpuParticlesEradicated=true`, `vfxEmitCallHits=0`, and `vfxRaycastHits=0`.
- `git diff --check` for the dedicated report is clean.

## 2026-05-21 - Propwash Overflow Dump Trigger

What was wrong:
- Propwash overflow telemetry compared a clamped event count against ring capacity, so the overflow dump trigger could remain silent.

What was done:
- `RecordPropwashTelemetry` now reads `PropwashRingCursorDTO.DroppedCount`, records it as `OverflowCount`, stores the actual write cursor, and calls `DumpBlackBoxOnce()` on overflow or >1500 us estimated GPU time.

Cinematic cheats used:
- None. The fix preserves the same GPU visual fake and only repairs forensic proof.

Exact microseconds saved, static estimate:
- Adds one fenced cursor read in telemetry. Avoids blind crash/spike investigation; no hot CPU particle or raycast path added.

Verification:
- Static scan: `overflowCount = math.max(0, cursor.DroppedCount)` is present.
- Static scan: `if (overflowCount > 0 || estimatedGpuMicroseconds > 1500) DumpBlackBoxOnce();` is present.
- Static forbidden scan remains 0 across touched C#/HLSL files.
- `git diff --check` for the touched renderer/docs set is clean except existing CRLF warning on the Unity C# file.
- Build gate rechecked: CPU load 100, no active `dotnet/csc`; build not launched.

## 2026-05-21 - Real Propwash Telemetry Scalars

What was wrong:
- Propwash telemetry recorded placeholder max intensity and local position, reducing black-box diagnostic value.

What was done:
- The propwash GPU upload loop now tracks the strongest sanitized event and writes its intensity/local position into the telemetry ring.

Cinematic cheats used:
- None. This improves forensic data while preserving the GPU-side silt visual fake.

Exact microseconds saved, static estimate:
- Adds one compare per uploaded event in the existing bounded 512-row upload. Avoids any second ring scan or GPU readback.

Verification:
- Static scan: `_debugPropwashMaxIntensity` and `_debugPropwashStrongestLocalPosition` are set during upload and consumed by telemetry.
- Static forbidden scan remains 0 across touched C#/HLSL files.
- `git diff --check` for the renderer is clean except existing CRLF warning.
- Build gate rechecked: CPU load 100, no active `dotnet/csc`; build not launched.

## 2026-05-21 - Dedicated AUP Rebase Buffer Route

What was wrong:
- The AUP rebase branch was still coupled to `CSMain`, and the new dedicated rebase dispatch needed exact read/write buffer semantics to avoid reading stale or wrong ping-pong buffers.

What was done:
- Added `_rebaseKernel`/`_rebaseThreadGroupSize` discovery and dispatch.
- `DispatchAupRebaseIfNeeded` now rebases the current read buffer through write bindings before `CSMain`, then clears `_AupShiftOffset` so simulation does not re-apply the same shift.
- HLSL load routes were audited: `CSMain` reads `LoadSiltParticle`; `AccumulateSonarGlow`, `CS_IntegrateSiltParticles`, and `CS_RebaseParticles` read `LoadWrittenSiltParticle`.

Cinematic cheats used:
- The particle cloud is camera-local GPU presentation state. Rebase is a one-pass GPU offset, not CPU particle teleportation, not physics, and not terrain recomputation.

Exact microseconds saved, static estimate:
- Normal frames: avoids any extra CPU work and leaves rebase dispatch inactive.
- Shift frames: replaces CPU-side per-particle rewrite/readback with one bounded GPU pass over active particles. Runtime measurement remains pending Unity/profiler proof.

Verification:
- Static HLSL line check confirms `CSMain` uses `LoadSiltParticle` and `CS_RebaseParticles` uses `LoadWrittenSiltParticle`.
- Static forbidden scan remains 0 across touched C#/HLSL/editor files.
- Direct managed `.Execute()` job call-site scan remains 0 in touched SHINOBU renderer/contracts files.
- `git diff --check` reports no whitespace errors; only Unity CRLF warnings on touched shader/C# files.
- Build gate rechecked: CPU load 96.34, no active `dotnet/csc`; build not launched.

## 2026-05-21 - Build Gate Recheck After AUP Route Audit

What was wrong:
- Project rules forbid launching `dotnet build` when CPU load is above 50 or compiler processes are active.

What was done:
- Re-ran the CPU/compiler gate after updating SHINOBU status, rationale, log, and self-audit files.

Cinematic cheats used:
- None. This is command discipline.

Exact microseconds saved, static estimate:
- Avoided a compile-wall build while system CPU was saturated. No runtime code path changed.

Verification:
- CPU probe returned 100.00.
- `dotnet/csc` process scan returned no active compiler process.
- `dotnet build` was not launched.

## 2026-05-21 - Mock DTO Wake Quality Lane

What was wrong:
- `RefreshDynamicWakeBinding` still built `wakeDtoParams` as `new Vector4(MockWakeCapacity, 0f, ...)`.
- That made the DTO wake loop ignore the continuous low-tier scalar even after the main structured dynamic wake path was fixed.

What was done:
- Added one local `wakeLowTierWeight` derived from `ResolveDynamicWakeLowTierWeight(_resolvedScalabilityParams.x)`.
- Published the same scalar into both structured wake params and mock DTO wake params.

Cinematic cheats used:
- DTO wake remains a shader-side force/radial blend fake. No CPU fluid simulation, CPU particle emission, raycast, or GPU readback was added.

Exact microseconds saved, static estimate:
- CPU change is 0 us beyond one reused scalar. Low-tier GPU DTO wake ALU now collapses through the same radial fake weight as structured wakes.

Verification:
- Static scan for `new Vector4(MockWakeCapacity, 0f`, `_DynamicWakeParams.y > 0.5`, `step(0.5, _DynamicWake...)`, and `_MATH_LOD_LOW` returned 0 hits in touched renderer/compute/material files.
- Static forbidden scan remains 0 across touched C#/HLSL/editor files.
- Direct managed `.Execute()` job call-site scan remains 0 in touched SHINOBU renderer/contracts files.
- `git diff --check` reports only Unity CRLF warnings on touched shader/C# files.

## 2026-05-21 - Continuous Marine Snow Scalability Lanes

What was wrong:
- `_MarineSnowScalabilityParams` still came from static Low/Mid/High/Ultra rows.
- HLSL used hard tier comparisons for flow sampling, curl fake, turbulence, bubble depth shrink, SDF collision, and depth collision.

What was done:
- Added `BuildContinuousScalabilityParams`, deriving flow quality, stagger cadence, SDF collision weight, and depth collision weight from `GlobalQualityWeight`, pressure, stress, and policy masks.
- Replaced row-selected particle capacity with a continuous lerp through row capacities before pressure clamping.
- Replaced HLSL hard tier checks with `saturate`, `smoothstep`, `lerp`, and particle-index dither gates for collision cadence.

Cinematic cheats used:
- Low quality now keeps the optical fake: reduced flow sampling, radial wake flow, and dithered collision lanes instead of full CPU/GPU contact simulation.

Exact microseconds saved, static estimate:
- Low-tier collision and high-detail curl/turbulence lanes no longer jump to full execution. Expected low/MX350 saving is proportional to the dithered collision weights; exact value requires Unity GPU profiler capture.

Verification:
- Static scan for old scalability table refs and `_MarineSnowScalabilityParams.x` hard thresholds returned 0 hits in touched renderer/compute files.
- Static scan for `step(0.5, _MarineSnowMaelstrom...)` returned 0 hits.
- `git diff --check` reports only Unity CRLF warnings on touched shader/C# files.

## 2026-05-21 - Build Gate After Continuous Marine Snow Scalability Audit

What was wrong:
- Build policy forbids launching `dotnet build` when CPU is above 50 or any `dotnet/csc` process is active.

What was done:
- Re-ran static gates, XML parse, diff check, CPU probe, and compiler process scan after Loop 46.

Cinematic cheats used:
- None. This is compile-wall discipline.

Exact microseconds saved, static estimate:
- Avoided launching a rebuild during CPU saturation and active dotnet workload. Runtime code path unchanged.

Verification:
- Static forbidden scans returned 0 hits in touched SHINOBU files.
- Self-audit XML parsed successfully.
- `git diff --check` reported only Unity CRLF warnings.
- CPU probe returned 100.00.
- Active `dotnet` processes were present; build was not launched.

## 2026-05-21 - Raw Native Blackbox Dumps

What was wrong:
- The silt blackbox dump used `BinaryWriter` field-by-field serialization.
- The propwash dump used a local stack copy per telemetry entry before writing bytes.

What was done:
- Converted silt dump to a raw 16-byte header plus one or two ring-ordered native telemetry spans from `NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr`.
- Converted propwash dump to one or two raw native telemetry spans with IO/permission failure containment.

Cinematic cheats used:
- None. This is forensic output hardening.

Exact microseconds saved, static estimate:
- Failure-path serialization shrinks from per-field/per-entry calls to at most two native span writes per ring. Runtime hot path cost remains 0 us.

Verification:
- Static scan for `BinaryWriter`, `writer.Write`, and `UnsafeUtility.AddressOf(ref entry)` in touched dump paths returned 0 hits.
- Static forbidden scan remains 0 across touched C#/HLSL/editor files.
- Direct managed `.Execute()` job call-site scan remains 0 in touched SHINOBU renderer/contracts files.

## 2026-05-21 - Continuous Stress Capacity Shed

What was wrong:
- `ResolveActiveParticleCount` snapped capacity to the low row when `ResolveSystemStress01() > 0.8f`.
- The snap targeted `LowMarineSnowCount`, which is the wrong low-row target for bubble and debris pools.

What was done:
- Replaced the threshold with `math.smoothstep(0.65f, 0.95f, systemStress01)`.
- Lerp target now comes from the low-row capacity for the active fluid type.

Cinematic cheats used:
- Capacity shedding remains visual-only. Gameplay truth, DTO layout, and propwash event ownership do not change.

Exact microseconds saved, static estimate:
- CPU cost is one scalar smoothstep/lerp. GPU cost sheds smoothly under stress instead of jumping from full capacity to the low row.

Verification:
- Static scan for `ResolveSystemStress01() >` returned 0 hits in `HectonMarineSnowRenderer`.
- `git diff --check` reports only Unity CRLF warnings on the touched renderer.

## 2026-05-21 - Build Gate After Stress Capacity Audit

What was wrong:
- Build policy forbids launching `dotnet build` when CPU is above 50 or any `dotnet/csc` process is active.

What was done:
- Re-ran static gates, XML parse, diff check, CPU probe, and compiler process scan after Loop 48.

Cinematic cheats used:
- None. This is compile-wall discipline.

Exact microseconds saved, static estimate:
- Avoided launching a rebuild during CPU saturation and active dotnet workload. Runtime code path unchanged.

Verification:
- Static forbidden scans returned 0 hits in touched SHINOBU files.
- Self-audit XML parsed successfully.
- `git diff --check` reported only Unity CRLF warnings.
- CPU probe returned 100.00.
- Active `dotnet` processes were present; build was not launched.

## 2026-05-21 - Emergency Mock Ring Cursor Parity

What was wrong:
- `GenerateMockPropwashEventsJob` read `PropwashRingCursorDTO` but started writes from slot 0 every frame, so the mock stress path did not exercise wrapped Vault ring semantics.

What was done:
- Changed mock event generation to start from `WrapIndex(cursor.WriteCursor, capacity)` and advance `cursor.WriteCursor` by the generated event count.
- Updated the binary payload ledger to state that mock and real vehicle propwash writes share the same cursor-aware snapshot route.

Cinematic cheats used:
- The mock path remains deterministic synthetic thrust vectors; no CPU particles, raycasts, or fluid physics were introduced.

Exact microseconds saved, static estimate:
- Runtime cost change is one integer wrap per mock generation. It prevents stale diagnostic proof and exercises the real ring upload path under stress.

Verification:
- Static line check confirms `baseCursor = WrapIndex(cursor.WriteCursor, capacity)`.
- Static forbidden scan remains 0 across touched C#/HLSL/editor files.
- Direct managed `.Execute()` job call-site scan remains 0 in touched SHINOBU renderer/contracts files.
- `git diff --check` for `PropwashGpuContracts.cs` is clean.

## 2026-05-21 - Live Harvest Gizmo Cursor Parity

What was wrong:
- `OnDrawGizmosSelected` used `events[i]`, so after ring wrap the editor visualization could show stale low-index rows while the GPU consumed the cursor-ordered snapshot.

What was done:
- Added fenced cursor acquisition to the gizmo path.
- Clamped cursor `EventCount`, computed `ComputePropwashUploadStart(cursor.WriteCursor, eventCount, events.Length)`, and read rows through `WrapPropwashUploadIndex`.
- Updated the binary payload ledger and self-audit to state that editor proof now follows the same wrapped event window as GPU upload.

Cinematic cheats used:
- None added. The gizmo is editor evidence only; runtime remains GPU SDF/depth/curl propwash presentation.

Exact microseconds saved, static estimate:
- Gameplay cost remains 0 us. Editor scene view cost is capped at 32 wrapped index reads and avoids managed debug copies.

Verification:
- Static line check confirms `OnDrawGizmosSelected` uses `TryAcquireReadyPropwashCursor`, `ComputePropwashUploadStart`, and `WrapPropwashUploadIndex`.
- Strict scan for direct `events[i]` assumptions in the touched renderer returned 0 hits.
- Static forbidden scan remains 0 across touched C#/HLSL/editor files.
- Direct managed `.Execute()` job call-site scan remains 0 in touched SHINOBU renderer/contracts files.
- `git diff --check` for `HectonMarineSnowRenderer.cs` reports only Unity CRLF warning.

## 2026-05-21 - Build Gate After Gizmo Cursor Audit

What was wrong:
- Project rules forbid launching `dotnet build` when CPU load is above 50 or compiler processes are active.

What was done:
- Re-ran CPU/compiler gate after the gizmo cursor patch and documentation updates.

Cinematic cheats used:
- None. This is compile-wall discipline.

Exact microseconds saved, static estimate:
- Avoided launching a build during CPU load 69.61. Runtime code path unchanged.

Verification:
- CPU probe returned 69.61.
- `dotnet/csc` process scan returned no active compiler process.
- `dotnet build` was not launched.

## 2026-05-21 - Current Ledger Propwash Boundary Preservation

What was wrong:
- The active `BINARY_PAYLOAD_INTEGRATION_LEDGER.md` was concurrently rewritten into a short current-format ledger, so the prior SHINOBU_237 addendum was no longer present in the working file.

What was done:
- Re-extracted the SHINOBU_237 XML assignment with a tag matcher that accepts extra attributes and verified `TASK_COUNT=20`.
- Added active range row `71492..71496` for propwash GPU event/cursor/telemetry/tuning/profile buffers.
- Added a compact SHINOBU_237 payload boundary row with DTO sizes, cursor route, GPU presentation boundary, source CSV route, and pending proof status.

Cinematic cheats used:
- None. This is documentation authority preservation.

Exact microseconds saved, static estimate:
- Runtime cost 0 us. Avoids integration ambiguity around BufferID and propwash payload ABI.

Verification:
- Prompt extraction reported `PROMPT_BLOCK_BYTES=19900` and `TASK_COUNT=20`.
- Ledger now includes `71492..71496` and `SHINOBU_237 Propwash GPU Payload Boundary`.

## 2026-05-21 - Marine Snow Shader Variant Strip

What was wrong:
- `_MATH_LOD_LOW` was a `multi_compile` keyword in marine snow compute/material shaders. Compute used it as a hard compile-time zero-flow path, while runtime quality parameters already controlled low-tier dynamic wake cost. The material shader did not consume it.

What was done:
- Removed `_MATH_LOD_LOW` pragmas from `Hecton_MarineSnow.compute` and `Hecton_MarineSnow.shader`.
- Removed the compile-time `#if defined(_MATH_LOD_LOW)` branch in `ResolveDynamicWakeFlow`.
- Left Unity instancing/stereo pragmas intact because XR procedural draw correctness is not proven without Unity import and Frame Debugger evidence.

Cinematic cheats used:
- The low-tier path remains the same visual fake: runtime slot caps and dominant-axis radial flow, not fluid simulation and not CPU particles.

Exact microseconds saved, static estimate:
- Runtime frame cost is unchanged by design. Import/warmup variant surface is reduced by one SHINOBU-owned keyword axis; exact warmup savings require Unity shader import timing.

Verification:
- Static `_MATH_LOD_LOW` scan returned 0 hits in marine snow compute/material shaders.
- Static forbidden scan remains 0 across touched C#/HLSL/editor files.
- Direct managed `.Execute()` job call-site scan remains 0 in touched SHINOBU renderer/contracts files.
- `git diff --check` reports only Unity CRLF warnings on touched shader/C# files.

## 2026-05-21 - Dynamic Wake Low-Tier Continuum

What was wrong:
- The dynamic wake path still used binary 0.5 tier checks after the shader variant strip.
- Mock wake params wrote `1` or `0`, C# sanitization clamped capacity through a threshold, and HLSL DTO flow used `step(0.5, ...)`.

What was done:
- Replaced mock wake tier output with `ResolveDynamicWakeLowTierWeight`.
- Replaced C# capacity threshold with `math.lerp(16f, 4f, lowTier)`.
- Replaced HLSL `step(0.5, ...)` / `> 0.5` low-tier gates with `saturate` plus `lerp`.

Cinematic cheats used:
- Dynamic wake remains a visual fake: radial thrust and bounded slot sampling feed GPU silt motion instead of CPU particles or fluid simulation.

Exact microseconds saved, static estimate:
- No CPU particle cost added. Low-tier GPU wake samples collapse continuously toward 4 dynamic slots instead of jumping across a hard threshold; exact GPU delta needs Unity RenderDoc/profiler capture.

Verification:
- Static binary-threshold scan for `_DynamicWakeParams.y > 0.5`, `step(0.5, _DynamicWake...)`, and `lowTier > 0.5` returned 0 hits in the touched renderer/compute files.
- Static forbidden scan remains 0 across touched C#/HLSL/editor files.
- Direct managed `.Execute()` job call-site scan remains 0 in touched SHINOBU renderer/contracts files.
- `git diff --check` reports only Unity CRLF warnings on touched shader/C# files.

## 2026-05-21 - Build Gate After Dynamic Wake Continuum Audit

What was wrong:
- Project rules forbid launching `dotnet build` while total CPU is above 50 or compiler processes are already active.

What was done:
- Re-ran the CPU/compiler gate after Loop 43 validation and documentation updates.

Cinematic cheats used:
- None. This is compile-wall discipline.

Exact microseconds saved, static estimate:
- Avoided launching a build during CPU load 100.00. Runtime code path unchanged.

Verification:
- CPU probe returned 100.00.
- `dotnet/csc` process scan returned no active compiler process.
- `dotnet build` was not launched.

## 2026-05-21 - Mock DTO Wake Quality Lane

What was wrong:
- `RefreshDynamicWakeBinding` still published mock DTO wake params with a hardcoded low-tier lane of `0f`, so the DTO mock loop stayed high-detail regardless of `GlobalQualityWeight`.

What was done:
- Reused `ResolveDynamicWakeLowTierWeight(_resolvedScalabilityParams.x)` for both structured wake params and mock DTO wake params.

Cinematic cheats used:
- DTO mock wakes remain bounded radial/detail flow fakes in the shader. No CPU particles, no raycasts, no fluid solve.

Exact microseconds saved, static estimate:
- CPU cost unchanged except one reused scalar. Low-quality DTO wake GPU ALU now follows the same reduced detail lane as the structured wake path.

Verification:
- Static scan for `new Vector4(MockWakeCapacity, 0f` returned 0 hits.
- Dynamic wake 0.5 threshold scan returned 0 hits in touched renderer/compute files.

## 2026-05-21 - Continuous Marine Snow Scalability Lanes

What was wrong:
- Marine snow scalability still selected static Low/Mid/High/Ultra rows and HLSL consumed `_MarineSnowScalabilityParams.x` with hard tier checks.

What was done:
- Replaced table selection with continuous lanes derived from `GlobalQualityWeight`, pressure, stress, and policy masks.
- Particle pool capacity now lerps through Low/Mid/High/Ultra row capacities before pressure clamping.
- HLSL flow, curl/turbulence, SDF collision, depth collision, and maelstrom lanes use `saturate`, `smoothstep`, `lerp`, and deterministic particle-index dither.

Cinematic cheats used:
- Low quality thins collision/detail lanes and keeps radial/curl visual fakes instead of CPU particle simulation or real fluid physics.

Exact microseconds saved, static estimate:
- Low-quality GPU work sheds SDF/depth collision cadence continuously. Exact frame delta requires Unity GPU profiling; CPU path stays bounded to scalar parameter refresh.

Verification:
- Static scan for old scalability row references and `_MarineSnowScalabilityParams.x` hard thresholds returned 0 hits in touched files.
- Diff check reported only Unity CRLF warnings.

## 2026-05-21 - Continuous Stress Capacity Shed

What was wrong:
- `ResolveActiveParticleCount` still snapped capacity when `ResolveSystemStress01() > 0.8f`, and the fallback target was marine-snow specific.

What was done:
- Replaced the threshold with `math.smoothstep(0.65f, 0.95f, systemStress01)`.
- Capacity now lerps toward the low-row capacity for the active fluid type before density/render-scale adjustments.

Cinematic cheats used:
- None added. This is continuous budget breathing for the existing GPU visual fake.

Exact microseconds saved, static estimate:
- Avoids abrupt GPU particle-count collapse and type-mismatched fallback. CPU cost is one smoothstep/lerp in scalar budget calculation.

Verification:
- Static scan for `ResolveSystemStress01() >` hard thresholds returned 0 hits.
- XML parse and diff check passed.

## 2026-05-21 - Raw Native Blackbox Dumps

What was wrong:
- Silt dump used `BinaryWriter` field serialization. Propwash dump copied entries one-by-one to stack spans.

What was done:
- `TryWriteBlackBoxDump` writes a raw 16-byte header and one or two native telemetry chunks from `NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr`.
- `PropwashTelemetryDump.TryWrite` writes contiguous native ring chunks and catches IO/permission failures.

Cinematic cheats used:
- None. This is forensic payload integrity.

Exact microseconds saved, static estimate:
- Failure-path dump shrinks from per-field/per-entry writes to at most two native span writes per ring. Hot gameplay path unchanged.

Verification:
- Static scan for `BinaryWriter`, `writer.Write`, and `UnsafeUtility.AddressOf(ref entry)` in touched dump paths returned 0 hits.
- Forbidden API and direct `.Execute()` scans remained 0.

## 2026-05-21 - Build Gate After Raw Native Dump Audit

What was wrong:
- Project rules forbid `dotnet build` while CPU load is above 50 or compiler processes are active.

What was done:
- Re-ran the CPU/compiler gate after the raw dump patch and static validations.

Cinematic cheats used:
- None. This is compile-wall discipline.

Exact microseconds saved, static estimate:
- Avoided launching a build during CPU load 94.00. Runtime code path unchanged.

Verification:
- CPU probe returned 94.00.
- `dotnet/csc` process scan returned no active compiler process.
- `dotnet build` was not launched.

## 2026-05-21 - WakeSources Vault Bridge

What was wrong:
- The static `HarvestKinematicWakeJob` existed, but SHINOBU had no active safe runtime source for vehicle/apex kinematic rows. A direct dependency on KCC runtime DTOs would violate compile-wall isolation.

What was done:
- Added `HarvestWakeSourcePropwashJob` to bridge existing VFX `WakeSources` into `PropwashEventDTO`.
- `HectonMarineSnowRenderer` now caches `BufferID.WakeSources` only if it already exists, resolves it behind the native-state and compaction-fence guards, subtracts camera AUP before local float conversion, and uploads the propwash GPU buffer after cursor changes.
- The bridge accepts vehicle and apex-predator wake source kinds only; player flora wakes stay out of the propwash ring.

Cinematic cheats used:
- Reuses the existing wake visual field as the truth source for propwash presentation. No CPU fluid physics, vehicle scene scan, raycast, or particle simulation was added.

Exact microseconds saved, static estimate:
- Avoids an O(vehicle objects + physics state scan) renderer path. Added work is one bounded Burst `IJob.Run()` over at most 16 existing wake rows plus one existing cursor-aware GPU upload when events change.

Verification:
- Prompt extraction with strict SHINOBU id matcher returned `TASK_COUNT=20`.
- Forbidden API and direct `.Execute()` scans returned 0 hits in touched SHINOBU files.
- CPU probe returned 100.00; no `dotnet/csc` process was active; `dotnet build` was not launched by policy.

## 2026-05-21 - WakeSource Ref-Readonly Bridge Read

What was wrong:
- `HarvestWakeSourcePropwashJob` copied each 128-byte `WakeSource` row into a local value before filtering vehicle/apex wake rows.

What was done:
- Added `WakeSourceStrideBytes=128` to `PropwashGpuContracts`.
- Patched `HarvestWakeSourcePropwashJob` to read through `NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr` and `UnsafeUtility.AsRef<WakeSource>` as `ref readonly`.
- Added editor layout validation for `WakeSource` stride.

Cinematic cheats used:
- None added. This is memory-path cleanup for the existing visual fake route.

Exact microseconds saved, static estimate:
- Removes up to 2048 bytes of source-row copying per bridge pass. The bridge remains bounded to 16 rows and still does no CPU particle simulation or raycast.

Verification:
- Static scan for `WakeSource source = WakeSources[i]` returned 0 hits.
- Forbidden API and direct `.Execute()` scans returned 0 hits in touched SHINOBU files.
- CPU probe returned 100.00; no `dotnet/csc` process was active; `dotnet build` was not launched by policy.

## 2026-05-21 - Hot GraphicsBuffer Resize Fence

What was wrong:
- `EnsureParticleBudget()` ran in the gameplay tick and could call `ResizeParticleBuffers()` when continuous `GlobalQualityWeight` or pressure changed resolved capacity.

What was done:
- Runtime `EnsureParticleBudget()` now refreshes scalability only.
- `ResizeParticleBuffers()` is only reachable through the non-playing editor fence after buffers already exist.
- Cold allocation capacity now comes from tuning/max, and `ResolveActiveParticleCount()` clamps active dispatch count by `_resolvedParticleCapacity` so quality still scales GPU work without reallocating buffers.

Cinematic cheats used:
- Preserved the existing GPU visual fake route. Quality now sheds active particle count, event samples, and collision cadence instead of rebuilding owned GPU memory.

Exact microseconds saved, static estimate:
- Avoids release/recreate of five `GraphicsBuffer`s during quality pressure changes. Added hot work is one integer clamp in active-count calculation.

Verification:
- Runtime resize route no longer calls `ResizeParticleBuffers()` from player tick.
- `git diff --check` reported no whitespace errors, only Unity LF-to-CRLF warnings.
- CPU probe returned 100.00; no `dotnet/csc` process was active; `dotnet build` was not launched by policy.

## 2026-05-21 - Hot Vault Lease and Snapshot Purity Fence

What was wrong:
- `EnsureNativeState()` could walk Vault handles and run default initializers every gameplay tick after readiness.
- `ResolveSiltTuningSnapshot()` had a read-like name but wrote a default tuning DTO back into Vault when version was zero.

What was done:
- Added a cached ready-lease return to `EnsureNativeState()` while the Vault exists and no compaction fence is active.
- Renamed the snapshot helper to `CaptureSiltTuningSnapshot()` and removed fallback Vault writeback from that snapshot path.
- Default tuning publication remains in `InitializeDefaultSiltTuning`, the owner-phase initialization path.

Cinematic cheats used:
- None added. This is authority-route cleanup so the existing GPU fake does not pay per-tick Vault polling.

Exact microseconds saved, static estimate:
- Removes repeated Vault handle lookup/default-initializer checks from every ready gameplay tick. Added hot cost is one cached Vault/fence check.

Verification:
- Static scan for `ResolveSiltTuningSnapshot`, propwash read-like resolver names, forbidden APIs, and direct runtime `.Execute()` call-sites returned 0 hits.
- CPU probe returned 100.00; no `dotnet/csc` process was active; `dotnet build` was not launched by policy.

## 2026-05-21 - Direct Floating-Origin AUP Read

What was wrong:
- SHINOBU hot paths used `GlobalSignals.CurrentRuntimeOriginAup()`, a legacy bridge wrapper over the floating-origin runtime, for runtime-position-to-AUP conversion and shader origin upload.

What was done:
- Replaced those reads with `HectonFloatingOrigin.CurrentTotalOffsetDouble`.
- Added finite guards before building AUP or uploading the shader origin vector.
- Kept AUP math in double precision until the camera-local delta downcast.

Cinematic cheats used:
- None added. This keeps the existing visual fake route tied to the authoritative floating-origin owner without a legacy signal bridge.

Exact microseconds saved, static estimate:
- Removes two wrapper calls per active tick and one legacy GlobalSignals dependency from this renderer. Runtime precision path remains double-local-float.

Verification:
- Static scan for `CurrentRuntimeOriginAup` and `GlobalSignals.` in touched SHINOBU files returned 0 hits.
- CPU probe returned 100.00; no `dotnet/csc` process was active; `dotnet build` was not launched by policy.

## 2026-05-21 - Ecosystem Flow-Field Upload Resize Fence

What was wrong:
- `RefreshFlowFieldUpload()` could release and recreate `_flowFieldBuffer` from the gameplay tick when the vegetation bridge flow-grid length changed.

What was done:
- Added a cold `flowFieldUploadCapacity` setting clamped to `40401..262144` float2 rows.
- Allocated the flow-field upload `GraphicsBuffer` during `EnsureBuffers()` instead of first runtime upload.
- Validated payload length against `gridResolution * gridResolution` before publishing shader metadata.
- Fenced resize to non-playing editor only; runtime oversized or inconsistent payloads now disable flow sampling instead of reallocating.

Cinematic cheats used:
- Runtime failure mode falls back to zero ecosystem flow plus existing curl/radial/turbulence visual fakes. No CPU downsample, no partial square upload, no hot allocation.

Exact microseconds saved, static estimate:
- Removes one possible GPU buffer release/create path from active play. Default cold memory is about 323 KB; upper editor capacity is about 2 MB. Hot added cost is integer validation and a capacity compare.

Verification:
- Forbidden API scan and direct `.Execute()` scan returned 0 hits in touched SHINOBU files.
- `git diff --check` reported no whitespace errors, only Unity LF-to-CRLF warnings.
- CPU probe returned 100.00; no `dotnet/csc` process was active; `dotnet build` was not launched by policy.

## 2026-05-21 - Sonar/Fog RenderTexture Runtime Resize Fence

What was wrong:
- `EnsureSonarGlowTexture()` and `EnsureFogDensityTexture()` were callable from gameplay dispatch paths and could release/recreate `RenderTexture`s after camera dimension or render-scale changes.

What was done:
- Existing sonar glow and fog density textures are kept during play even if requested dimensions change.
- Non-playing editor resize remains available for tuning.
- Existing texel-size publication continues from the allocated texture dimensions, so shader metadata does not claim a size that was not allocated.

Cinematic cheats used:
- On runtime resolution changes, quality degrades by retaining the previous low-res auxiliary texture rather than reallocating. The main silt/propwash visuals continue through GPU fakes and continuous quality lanes.

Exact microseconds saved, static estimate:
- Removes two possible `RenderTexture.Release/new/Create` stalls from active frames. Added hot cost is one `Application.isPlaying` branch on dimension mismatch.

Verification:
- Forbidden API scan and direct `.Execute()` scan returned 0 hits in touched SHINOBU files.
- Renderer `git diff --check` reported no whitespace errors, only Unity LF-to-CRLF warning.
- Build not launched; latest CPU gate record remains 100.00.

## 2026-05-21 - DataVault Handle Cache Rebind and WakeSources Probe

What was wrong:
- The native-ready fast path could miss optional `BufferID.WakeSources` if another VFX owner created it after SHINOBU had already cached required Vault handles.
- `BindDataVault()` cleared only two handle fields, leaving stale cached handle identity possible after DataVault service replacement.

What was done:
- Added a 30-frame optional WakeSources handle probe while the cached Vault lease is ready, stopping after acquisition.
- Expanded `BindDataVault()` to clear every cached SHINOBU Vault handle and reset the optional probe frame.

Cinematic cheats used:
- None added. This preserves the existing WakeSources-to-propwash visual bridge without direct KCC coupling or private fallback arrays.

Exact microseconds saved, static estimate:
- Avoids stale-handle failure/recovery paths and keeps late WakeSources acquisition bounded. Before acquisition: one frame-counter check per tick and one handle lookup per 30 frames. After acquisition: 0 us extra.

Verification:
- Forbidden API scan and direct `.Execute()` scan returned 0 hits in touched SHINOBU files.
- Renderer `git diff --check` reported no whitespace errors, only Unity LF-to-CRLF warning.
- Build not launched; latest CPU gate record remains 100.00.

## 2026-05-21 - Vault Compaction Handle Cache Invalidation

What was wrong:
- `RefreshDataVaultBinding()` still invalidated only wake-job and silt telemetry handles during `IDataVault.IsCompactionFenceActive`.
- Propwash event/cursor/telemetry/tuning, wake-profile, silt tuning, dynamic wake, mock flow, and optional WakeSources handles could remain cached across a Vault relocation window.
- The helper contained a dead `_dataVault` self-compare branch that could never perform a service rebind.

What was done:
- Added `ClearVaultHandleCache()` as the single full cached-handle invalidation route.
- Routed DataVault service rebind, compaction-fence invalidation, and native lease teardown through that route.
- Removed the impossible self-compare branch; rebind authority stays lifecycle/hot-swap-owned, not gameplay registry polling.

Cinematic cheats used:
- None added. This is native handle safety for the existing GPU silt/propwash visual fake.

Exact microseconds saved, static estimate:
- Steady-state hot path unchanged. Compaction/rebind clears a dozen handle structs instead of two, preventing stale native handle use after Vault relocation.

Verification:
- Prompt reparse with `Task\s+\d{2}:` returned 20 task labels; the earlier `<TASK>` scan returned 0 because the batch block uses plain task text, not XML task tags.
- XML and JSON report parse gates passed.
- Forbidden API scan, direct `.Execute()` scan, and dead rebind branch scan passed.
- Diff check reported no whitespace errors, only repository LF-to-CRLF warnings.
- CPU probe returned 100.00; no compiler process was active; `dotnet build` was not launched by policy.

## 2026-05-21 - Wake Proximity Particle Alias Fence

What was wrong:
- `CS_EvaluateWakeProximity` wrote particle slot `eventIndex % particleCount`.
- C# dispatched the kernel by propwash event sample budget only, so low active particle counts could allow multiple event threads to write the same particle slot in one pass.

What was done:
- C# proximity dispatch now caps work by `_activeParticleCount` and the continuous propwash event sample budget.
- HLSL clamps `sampleBudget` by `particleCount` before rejecting threads and before the modulo write.

Cinematic cheats used:
- The pass remains a bounded SDF/height visual silt injection. No CPU physics, no atomics, no staging scatter list.

Exact microseconds saved, static estimate:
- Prevents wasted SDF/height samples above live particle capacity on low-tier frames. Added cost is one integer min in C# and one uint min in HLSL.

Verification:
- Patch presence scan found the C# and HLSL clamps.
- Forbidden API scan and direct `.Execute()` scan returned 0 hits in touched SHINOBU files.
- CPU probe returned 100.00; no compiler process was active; `dotnet build` was not launched by policy.

## 2026-05-21 - Propwash Flow Event Sample Capacity Fence

What was wrong:
- `ComputePropwashEventFlow` could sample the continuous propwash event budget without considering live particle capacity.
- Stress could reduce active particles while leaving every remaining particle to loop an oversized event set.

What was done:
- HLSL now reads `_MarineSnowMetaParams.x` in `ComputePropwashEventFlow`.
- The propwash flow event `sampleBudget` is clamped by live particle count before the event loop.

Cinematic cheats used:
- Kept the GPU-only radial/thrust visual fake. No CPU event prefilter and no shader keyword split.

Exact microseconds saved, static estimate:
- Low-capacity frames shed event-distance checks in `CSMain` and `CS_IntegrateSiltParticles`; added cost is one uint min per flow evaluation.

Verification:
- Patch presence scan found the HLSL flow clamp.
- XML/JSON parse, forbidden API scan, direct `.Execute()` scan, dead rebind branch scan, and diff check passed.
- CPU probe returned 100.00; no compiler process was active; `dotnet build` was not launched by policy.

## 2026-05-21 - Continuous Stagger Cadence Dither

What was wrong:
- C# emitted a float stagger cadence lane from continuous quality.
- HLSL cast that lane to a uint bitmask, producing stepped and non-monotonic cadence for flow, fog density, and sonar accumulation.

What was done:
- C# now publishes `flowQuality` directly in `_MarineSnowScalabilityParams.y`.
- HLSL `ShouldRunStaggeredRate` uses deterministic hash dither through `ShouldRunQualityLane`.

Cinematic cheats used:
- Kept probabilistic sample thinning as the visual fake. No shader keyword, no quality tier branch, no CPU scheduling path.

Exact microseconds saved, static estimate:
- Low/stressed frames shed flow/fog/sonar samples continuously. High-end cost remains full-rate because the dither helper returns true at gate one.

Verification:
- Stagger bitmask scan found no `staggerMask` or `& staggerMask` path.
- XML/JSON parse, forbidden API scan, and direct `.Execute()` scan passed.
- CPU probe returned 100.00; no compiler process was active; `dotnet build` was not launched by policy.
