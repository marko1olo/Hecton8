# LOG_ABYSSAL_FLOW_FIELD

## 2026-05-12 - ABYSSAL_FLOW_FIELD

What was wrong:
- The abyssal flow buffer existed but did not provide a living 3D current field for kelp, marine snow, boids, or submarine drag.
- Wake response required a GPU-only visual cheat; CPU splats/readbacks would violate the no-readback and frame-time mandates.
- Consumer systems needed decoupled bindings because other agents are working in adjacent domains.

What was done:
- Added curl-noise, decay, and wake injection kernels to `AbyssalFlowField.compute`.
- Added persistent 32x32x32 ping-pong `RenderTexture` allocation in `HectonFluidEngine` using `GraphicsFormat.R16G16B16A16_SFloat` and random write.
- Published `_AbyssalFlowFieldTexture`, grid, center, spacing, texture params, and active flags to shaders/computes.
- Bound the texture into indirect vegetation, marine snow, generic boids, and sargassum micro-fauna boids.
- Fed analytical local flow into submarine hydrodynamic drag as `FlowVelocityWS`, avoiding GPU readback.
- Added thermal geyser updraft on high tier and disabled wake/geyser on Low/MX350.
- Added fixed 300-frame abyssal flow telemetry and crash dump path.
- Logged kelp/seaweed/flora `Update()` reconnaissance in `RECON_ABYSSAL_FLOW_FIELD.md`.

Cinematic cheats used:
- Curl value-noise derivatives instead of physical fluid simulation.
- Radial/trailing velocity splat instead of a pressure wake solve.
- Thermal intensity threshold and upward vector injection instead of heat diffusion.
- Integer particle flow loads where smooth filtering is not visually needed.

Exact microseconds saved:
- CPU fluid/advection avoided: estimated 300-900 us per frame depending on particle/fish counts.
- GPU readback stall avoided: estimated 200-1000 us per frame spike avoided.
- Low-tier wake/geyser skip: estimated 35-120 us GPU saved.
- Managed kelp/particle/boid force loops avoided: estimated 100-500 us CPU avoided at dense scenes.
- Persistent texture allocation: 0 us steady allocation churn after cold setup.

Verification:
- `dotnet build Hecton8/Assembly-CSharp.csproj --no-restore -m:2 /nr:false`: succeeded.
- `dotnet build Hecton8/Hecton8.Core.csproj --no-restore -m:2 /nr:false`: succeeded with 3 pre-existing out-of-domain CS0649 warnings in `PlayerCriticalProceduralAudioRenderer.cs` and `WorldSpatialHashGrid.cs`.
- `git diff --check` on touched files: only CRLF normalization warnings.
- Unity batchmode shader import: blocked by existing Unity project lock.
- Unity MCP console: unavailable, `no_unity_session`; refresh timed out.
- Editor log showed prior imports of touched flow shaders/compute assets; latest source cleanup removed the warning patterns, but a fresh console pass is still pending.

Status:
- PENDING VERIFICATION. Not reported as VERIFIED MASTER GRADE because Unity console verification and out-of-domain warning cleanup remain external to this agent's domain.

## 2026-05-12 - Continuation Pass

What was wrong:
- `TryDispatchGpuAbyssalFlowField` was called twice from `FixedTick`.
- The duplicate call was suppressed by `Time.frameCount`, which can skip valid catch-up fixed steps during a long rendered frame.

What was done:
- Removed the redundant second dispatch call in the buoyancy scheduling block.
- Replaced the render-frame guard with a `Time.fixedTime` guard so duplicate calls in the same fixed step are suppressed while later catch-up fixed steps still update decay/wake/thermal flow.

Cinematic cheats used:
- No new physical simulation added. This pass preserved the existing curl-noise/wake/geyser cheats and made their timing deterministic.

Exact microseconds saved:
- Redundant dispatch path removed: estimated 1-3 us CPU per fixed tick.
- Correct catch-up fixed-step updates prevent visual wake/decay stalls during frame spikes; GPU cost remains unchanged per actual fixed step.

Verification:
- `dotnet build Hecton8/Assembly-CSharp.csproj --no-restore -p:BuildProjectReferences=false /nr:false -v:minimal`: succeeded.
- `dotnet build Hecton8/Hecton8.Core.csproj --no-restore -p:BuildProjectReferences=false /nr:false -v:minimal`: succeeded.
- `git diff --check` on the touched flow files: only CRLF normalization warnings.
- Unity MCP console remains unavailable.

Status:
- PENDING VERIFICATION until Unity shader console access is available.

## 2026-05-12 - Warm-Start And Low-Tier Pass

What was wrong:
- Newly allocated/recreated flow textures could contain undefined GPU memory.
- The first decay pass could blend from that undefined state if the values happened to be finite.
- Low tier disabled new wake/geyser injection but could retain prior wake residue while it decayed.

What was done:
- `_hasAbyssalFlowTexture` is reset when flow textures are released, created, recreated, or read/write pointers are restored.
- The next dispatch after an uninitialized state forces `textureParams.z = 1`, producing a full base-curl overwrite in the existing update kernel.
- Low tier also forces `textureParams.z = 1`, guaranteeing base-curl-only output on MX350/low scalability.

Cinematic cheats used:
- Reused the existing base-curl update pass as a full-volume clear instead of adding a separate physical reset or clear kernel.

Exact microseconds saved:
- Avoided separate texture clear dispatch: estimated 20-40 us GPU.
- Low tier removes lingering wake/geyser residue immediately without extra passes.

Verification:
- `dotnet build Hecton8/Hecton8.Core.csproj --no-restore -p:BuildProjectReferences=false /nr:false -v:minimal`: succeeded.
- `dotnet build Hecton8/Assembly-CSharp.csproj --no-restore -p:BuildProjectReferences=false /nr:false -v:minimal`: succeeded.

Status:
- PENDING VERIFICATION until Unity shader console access is available.

## 2026-05-12 - Fail-Closed Publication Pass

What was wrong:
- Valid abyssal flow texture publication could remain globally active after the producer became invalid or after an editor/domain transition left native shader globals dirty.
- Disabled flow, missing compute asset/kernel, missing observer, or lost GPU resources could leave kelp, snow, and boids sampling stale vectors around an obsolete center.

What was done:
- Added `DeactivateAbyssalFlowPublication` in `HectonFluidEngine`.
- The deactivation clears texture-active state, grid/center/spacing globals, texture params, and cached metadata on the first invalid path, then stays quiet until the next real publication.
- Normal duplicate calls inside the same fixed step still keep the last valid texture for consumers; invalid/cold paths fail closed and force deterministic warm-start on the next valid dispatch.

Cinematic cheats used:
- No new fluid truth. The pass preserves the visual curl/wake/geyser approximation and prevents stale presentation data from masquerading as live simulation.

Exact microseconds saved:
- Steady-state added cost: 0 us.
- Invalid-path shader-global cleanup cost: estimated 3-8 us CPU only when the producer is unavailable.
- Avoided texture release/reallocation churn on invalid ticks: estimated 50-200 us cold-path spikes depending on driver.

Verification:
- `dotnet build Hecton8/Assembly-CSharp.csproj --no-restore -p:BuildProjectReferences=false /nr:false -v:minimal`: succeeded, 0 errors.
- `dotnet build Hecton8/Hecton8.Core.csproj --no-restore -p:BuildProjectReferences=false /nr:false -v:minimal`: succeeded, 0 errors.
- `git diff --check` on touched flow/docs: only CRLF normalization warnings.
- Unity MCP console is intermittent. Last usable read showed an out-of-domain `SubmarineStructuralGrid.cs` interface error and an MCP regex timeout; later read returned `no_unity_session`.

Status:
- PENDING VERIFICATION until Unity shader console access is stable and out-of-domain compile noise is cleared.

## 2026-05-12 - Vortex Impulse And Compile Blocker Pass

What was wrong:
- The prompt's optional Leviathan tail-whip vortex had no decoupled entry point.
- Directly wiring to fauna code would create brittle cross-agent dependencies.
- `Hecton8.Core.csproj` verification was blocked by unrelated PDA sonar code still writing removed individual compute properties.

What was done:
- Added `InjectAbyssalVortexTexture` to `AbyssalFlowField.compute`.
- Added bounded four-slot `AbyssalVortexImpulse` storage and public `TryQueueAbyssalVortexImpulse` to `HectonFluidEngine`.
- Vortex injection runs only on high tier; low tier ages impulses without dispatching them.
- Fixed `PDAMapTab` to write packed `_SonarScalarParams` and `_SonarDispatchParams`, matching `Hecton_SonarMap.compute`.

Cinematic cheats used:
- Tail-whip turbulence is a transient tangential velocity splat, not a pressure solve or persistent fluid sim.
- PDA sonar constants stay packed to avoid extra per-frame property chatter.

Exact microseconds saved:
- Avoided direct fauna-driven flow simulation: estimated 300-900 us CPU/GPU versus persistent local fluid truth.
- Low tier vortex skip: estimated 35-70 us GPU saved per live impulse.
- PDA packed constants reduce five invalid property writes to two valid vector writes; estimated 1-3 us CPU saved on point-cloud dispatch.

Verification:
- `dotnet build Hecton8/Assembly-CSharp.csproj --no-restore -p:BuildProjectReferences=false /nr:false -v:minimal`: succeeded, 0 errors.
- `dotnet build Hecton8/Hecton8.Core.csproj --no-restore -p:BuildProjectReferences=false /nr:false -v:minimal`: succeeded, 0 errors.
- `git diff --check` on touched flow/PDA/docs files: only CRLF normalization warnings.
- Unity refresh requested script compilation but timed out waiting for editor readiness once.
- Later Unity MCP console read returned 0 error entries. A stable explicit shader import pass is still pending, so status remains PENDING VERIFICATION.

Status:
- PENDING VERIFICATION until Unity shader import readiness is stable enough for an explicit compute-shader pass.

## 2026-05-12 - Consumer Guard And Bandwidth Pass

What was wrong:
- The heat-source upload path still copied all eight thermal slots whenever any heat source existed and did a buffer upload even when the count was zero.
- Published abyssal flow texture queries did not reject lost/uncreated `RenderTexture` instances.
- Generic boids could sample the 1x1 fallback abyssal texture when the texture path was absent but the legacy structured buffer was valid.
- Several shader fallback paths trusted flow values after texture/buffer reads without a final finite guard.

What was done:
- `HectonFluidEngine` now skips zero-count heat uploads and uploads only the active thermal source count.
- `TryGetGpuAbyssalFlowFieldTexture` now requires a created `RenderTexture` before reporting active texture flow.
- `HectonBoidController` now zeros `_AbyssalFlowSpacing.w` when only the structured-buffer fallback is active, forcing `BoidSimulation.compute` into its buffer path instead of the 1x1 fallback texture.
- Vegetation, marine snow, generic boids, and sargassum micro-fauna flow consumers now sanitize sampled flow vectors with dot-product finite guards.
- Added canonical cold-allocation comments for the abyssal heat staging and black-box telemetry NativeArrays.

Cinematic cheats used:
- No physical simulation added. The pass keeps curl/wake/geyser/vortex as bounded visual velocity fields and prevents invalid values from escaping into presentation systems.

Exact microseconds saved:
- Zero heat-source frames avoid one `LockBufferForWrite` upload: estimated 2-6 us CPU/driver overhead saved.
- Active heat-source frames copy 1-8 records instead of always 8: small proportional bandwidth reduction, strongest when only one vent is active.
- Fallback routing avoids wasted texture sampling against a 1x1 dummy when structured flow is the only valid payload.

Verification:
- `dotnet build Hecton8/Hecton8.Core.csproj --no-restore -p:BuildProjectReferences=false /nr:false -v:minimal`: succeeded, 0 errors.
- `dotnet build Hecton8/Assembly-CSharp.csproj --no-restore -m:2 /nr:false -v:minimal`: succeeded, 0 errors, 45 out-of-domain package/third-party warnings.
- `git diff --check` on touched flow files: only CRLF normalization warnings.
- Static scan found no `AsyncGPUReadback.Request` for abyssal flow.
- Unity MCP `refresh_unity`: failed via HTTP transport to `127.0.0.1:8088/mcp`.
- Latest `Editor.log` tail shows out-of-domain `HectonCelestialEngine` persistent allocation leaks from editor shutdown; no fresh explicit shader-import proof.

Status:
- PENDING VERIFICATION until Unity MCP/Editor shader import verification is stable.

## 2026-05-12 - Dead Aggregate Strip Pass

What was wrong:
- `HectonFluidEngine` still allocated a raw aggregate mask buffer and dispatched `ResetAbyssalFlowAggregate` every fixed-step abyssal flow update.
- No live C# path consumed that mask, dispatched `DetectBiolumeSurge`, or read it back for the new 3D wake texture contract.
- `Docs/ARCHITECTURE/FLOW_FIELD_MATH.md` still described the old aggregate-readback topology.

What was done:
- Removed the aggregate mask shader property ID, raw buffer allocation/release, reset/surge kernel lookups, reset dispatch, aggregate binding, and stale debug field from `HectonFluidEngine`.
- Initially left the legacy compute kernels in `AbyssalFlowField.compute` for import compatibility; the later static pass removed them after confirming no live C# aggregate binding remains.
- Updated the architecture doc to describe the live `UpdateAbyssalFlowField` -> `UpdateAbyssalFlowTexture` -> optional wake/vortex topology and the no-readback CPU contract.

Cinematic cheats used:
- Preserved the visual curl/wake/geyser/vortex field. Removed bookkeeping that was not buying visible immersion.

Exact microseconds saved:
- Removed one fixed-step 1x1x1 reset dispatch: estimated 3-8 us CPU/driver overhead and 1-3 us GPU scheduling overhead on low-end drivers.
- Removed one raw `GraphicsBuffer` driver object from cold allocation: tiny VRAM byte count, but less driver lifetime churn.

Verification:
- `rg` confirms no aggregate-mask/reset-kernel/surge-kernel references remain in `HectonFluidEngine.cs`.
- `dotnet build Hecton8/Hecton8.Core.csproj --no-restore -p:BuildProjectReferences=false /nr:false -v:minimal`: succeeded, 0 errors.
- `dotnet build Hecton8/Assembly-CSharp.csproj -m:2 /nr:false -v:minimal`: succeeded after restore, 0 errors, 153 out-of-domain package/third-party warnings.
- `git diff --check` on touched flow/docs files: only CRLF normalization warnings.
- Unity MCP console remains unreachable via HTTP at `127.0.0.1:8088/mcp`.
- `Editor.log` search found no fresh `AbyssalFlowField`, shader error, or compiler error entries, but this is not a clean shader import proof.

Status:
- PENDING VERIFICATION until Unity console/import verification is available.

## 2026-05-12 - Vortex AUP And No-Build Static Pass

What was wrong:
- Queued vortex impulses were stored in runtime world space but were not rebased when floating origin shifted.
- Invalid flow-producer paths could leave queued vortex impulses alive without aging, then inject stale turbulence when the producer returned.
- `AbyssalFlowField.compute` still carried aggregate reset/detect kernels after the C# aggregate path was stripped.

What was done:
- `HectonFluidEngine` now rebases queued vortex impulse positions during `ApplyOriginShiftRebase`.
- Invalid flow paths age queued vortex impulses once per fixed step; disable/destroy clears the bounded queue.
- Removed the unreferenced aggregate reset/detect shader entry points and raw aggregate buffer declaration from `AbyssalFlowField.compute`.

Cinematic cheats used:
- Kept vortex as a transient tangential splat, not a persistent fluid sim. Low tier expires impulses without dispatching GPU work.

Exact microseconds saved:
- Rare AUP shift cost is capped at four vector adds, under 1 us CPU.
- Invalid-path aging is capped at four queue slots and only runs when impulses exist, under 1 us CPU.
- Removing stale compute kernels reduces shader import/runtime surface; no measured runtime delta because those kernels were no longer dispatched.

Verification:
- No build or Unity compile run per latest user directive.
- `rg` confirms no aggregate-mask/reset/surge references remain in `HectonFluidEngine.cs` or `AbyssalFlowField.compute`.
- `git diff --check` on touched flow files reports only CRLF normalization warnings.

Status:
- PENDING VERIFICATION until Unity shader import can be checked without violating the no-build directive.

## 2026-05-12 - Published Payload Validity Static Pass

What was wrong:
- `TryGetGpuAbyssalFlowFieldBuffer` accepted any non-null structured buffer with positive count.
- Buffer/texture getters did not reject non-finite center or spacing metadata.

What was done:
- Added `GraphicsBuffer.IsValid()` to structured-buffer publication.
- Added finite `Vector4` metadata guards for structured-buffer and texture flow getters.

Cinematic cheats used:
- No simulation change. The pass keeps consumers on fallback buffers/textures when producer metadata is invalid.

Exact microseconds saved:
- Direct cost is below 1 us on bind/refresh paths.
- Prevents undefined binding/sampling recovery work after lost GPU handles or invalid metadata.

Verification:
- No build or Unity compile run per latest user directive.
- Static inspection confirms `GraphicsBuffer.IsValid()` is an established project pattern.
- `git diff --check` reports only CRLF normalization warnings.

Status:
- PENDING VERIFICATION until compile/import checks are allowed.

## 2026-05-12 - Vegetation Flow Boundary Parity Pass

What was wrong:
- `Hecton_IndirectVegetation.shader` returned zero for texture samples outside the 3D flow volume but clamped structured-buffer fallback samples to an edge cell.
- On fallback paths, distant vegetation could inherit a fake edge current instead of cleanly dropping to no-flow.

What was done:
- Replaced the structured-buffer coordinate clamp with an out-of-bounds zero return.
- Brought vegetation fallback behavior in line with marine snow and boid consumers.

Cinematic cheats used:
- Preserved the 100 m flow field as a bounded visual volume. Did not expand simulation coverage to hide edge artifacts.

Exact microseconds saved:
- Runtime cost change is negligible; the fallback branch adds comparisons and avoids a buffer read for out-of-volume samples.
- Prevents visible edge-current artifacts on low/fallback devices.

Verification:
- No build or Unity shader import run per latest user directive.
- Static shader inspection confirms texture, marine snow, boid, and vegetation fallback paths now reject out-of-volume samples.
- `git diff --check` reports only CRLF normalization warnings.

Status:
- PENDING VERIFICATION until shader import checks are allowed.

## 2026-05-12 - Heat Source Count Clamp Pass

What was wrong:
- `AbyssalFlowField.compute` trusted `_AbyssalFlowHeatSourceCount` directly in both heat-source loops.
- The backing buffer capacity is fixed at eight, so a bad external property write could push a shader loop out of bounds.

What was done:
- Added `HECTON_ABYSSAL_HEAT_SOURCE_CAPACITY`.
- Clamped the structured and texture heat-source loop counts to the fixed capacity.

Cinematic cheats used:
- No new thermal simulation. Kept the bounded eight-source geyser/updraft fake and made it fail closed.

Exact microseconds saved:
- No measurable saving; two integer clamps are negligible.
- Avoids undefined GPU memory access from invalid heat-source counts.

Verification:
- No build or Unity shader import run per latest user directive.
- Static scan confirms both heat-source loops now clamp to capacity.
- `git diff --check` reports only CRLF normalization warnings.

Status:
- PENDING VERIFICATION until shader import checks are allowed.

## 2026-05-12 - Documentation And Stale Constant Static Pass

What was wrong:
- `Docs/ARCHITECTURE/FLOW_FIELD_MATH.md` still described the removed aggregate/biolume readback topology as live or semi-live.
- `AbyssalFlowField.compute` kept unused pressure-wave threshold constants after `DetectBiolumeSurge` was removed.
- `HectonFluidEngine` kept an unused `AbyssalBiolumeSurgeHoldSeconds` constant after the aggregate path was stripped.

What was done:
- Updated the architecture doc to describe the live `UpdateAbyssalFlowField` -> `UpdateAbyssalFlowTexture` -> optional wake/vortex path and the no-readback CPU contract.
- Removed stale shader and C# constants tied only to the deleted aggregate detection path.

Cinematic cheats used:
- Kept visible turbulence as texture-side curl/wake/geyser/vortex motion. Did not revive CPU-visible surge flags.

Exact microseconds saved:
- Direct runtime saving is negligible for constants.
- Reduced shader/source surface lowers import/integration risk; no measured compile pass was run.

Verification:
- No build or Unity compile run per latest user directive.
- Static search finds no live source references to aggregate mask/reset/surge symbols outside historical documentation/log text.
- `git diff --check` reports only CRLF normalization warnings.

Status:
- PENDING VERIFICATION until Unity shader import can be checked without violating the no-build directive.
