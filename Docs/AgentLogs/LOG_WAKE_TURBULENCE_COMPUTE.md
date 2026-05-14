# LOG - WAKE_TURBULENCE_COMPUTE

## 2026-05-14 - Leviathan & Pod Advection

What was wrong:
- Marine snow and advection particles had only ambient flow/SDF response; no dynamic wake turbines for Alpha Leviathan tail changes or Drop Pod splashdown.
- Producers had no decoupled way to drive fluid VFX without direct fluid-engine references.
- Existing advection telemetry did not expose active wake counts or the mandated wake dump file.

What was done:
- Added `FluidImpulseSignal` on the typed Core signal bus with finite guards and a 32-packet lane.
- Extended `HectonFluidEngine` with fixed SOA wake storage: `_DynamicWakes` xyz/intensity, `_DynamicWakeVectors` vector/radius, CPU lifetime array, low-tier two-slot cap, dead-slot reuse, weakest-slot replacement, and pre-upload Burst decay.
- Bound wake buffers through `FluidAdvectionRenderGraphPayload`, `HectonFluidAdvectionRenderFeature`, and `Hecton_FluidAdvection.compute`.
- Added shader wake math using squared distance, `rcp`, dot-gated shove, and cross-product vortex. Shader `length()` scan returned no matches.
- Wired `ProceduralLeviathanSpineIK` to publish tail-whip impulses on sharp direction changes.
- Wired `OrbitalRelativityDirector` to publish a 50m splashdown impulse at ocean handoff.
- Added `ActiveTurbulenceWakes` to the 300-frame fluid telemetry ring, global telemetry, and `Docs/AgentLogs/Dump_WAKE_TURBULENCE_COMPUTE.bin`.
- Logged status and rationale with five loops plus Omega polish.

Cinematic cheats used:
- Wake turbines are temporary visual velocity primitives, not fluid truth.
- Low tier caps active wake evaluation to 2 slots; high tier allows 8 stacked visual wakes.
- Shader uses squared radius gates and `rsqrt`/`rcp` math instead of exact distance.
- Drop Pod uses one 50m push vector for instant window-clearing snow displacement instead of simulating splash physics.

Exact microseconds saved:
- Low-tier two-slot cap avoids 6 of 8 wake checks per particle, estimated 5-10 us saved on MX350-heavy particle dispatches.
- GPU-side wake displacement avoids CPU per-particle updates; expected CPU saving versus 4096-particle managed displacement is >100 us/frame.
- Fixed 256-byte wake upload avoids dynamic buffer/list churn; managed allocation remains 0 B/frame in the wake path.

Verification:
- `dotnet build Hecton8.Core.csproj --no-restore -v:quiet -clp:ErrorsOnly`: blocked by unrelated missing generated/project references before task-specific proof.
- Unity MCP validation: blocked by `no_unity_session`.
- `dotnet build Hecton8.slnx --no-restore -v:quiet -clp:ErrorsOnly`: blocked by missing `project.assets.json` for editor/sample projects plus global reference faults.
- `git diff --check`: CRLF normalization warnings only.
- Static GPU mapping verified for `_DynamicWakes`, `_DynamicWakeVectors`, and `_DynamicWakeParams`.

Status:
- `Docs/Tasks/Status_WAKE_TURBULENCE_COMPUTE.md` remains `PENDING VERIFICATION` because compile validation is blocked by global dependencies, not by a known wake-specific compiler error.

## 2026-05-14 - Wake Hardening Pass

What was wrong:
- Dynamic wake GPU uploads were single-buffered and repeated every visual payload. That is small bandwidth, but still violates the project's double-buffer and dirty-upload rules.
- Local wake tiering had no hysteresis, so rapid global scalability changes could flip between 2-wake and 8-wake modes.
- Wake lifetime decayed only when a RenderGraph payload was built. No-particle/no-camera frames could preserve stale wake state.
- High-tier shader response did not spend saved low-tier budget on extra visible turbulence.

What was done:
- Replaced the single `_DynamicWakes` / `_DynamicWakeVectors` GPU pair with A/B double-buffered wake and vector buffers.
- Added dirty/active/zero-state upload gates. Idle clean frames skip wake uploads; active frames upload only the parity that will be bound.
- Added a 2.5s high-tier upgrade hold. Low/MX350/low-memory downgrades apply immediately.
- Moved wake aging into `LateFrameTick` and guarded it with `_dynamicWakeLastDecayFrame` so wake state decays once per frame, independent of RenderGraph consumption.
- Added high-tier-only radial billow and cross-shear to `ApplyDynamicWakes`; MX350 low-tier branch keeps the previous cheaper push/vortex path.

Cinematic cheats used:
- Still no physical fluid solve. The high-tier addition is a local visual fake: squared-distance core falloff, radial billow, and shear.
- Low tier preserves the cheap two-slot wake turbine approximation; high tier buys denser turbulence with the saved budget.

Exact microseconds saved:
- Idle wake path now skips two 8-float4 GPU uploads after zero state is resident, estimated 1-3 us/frame saved on MX350 under no-wake/no-particle conditions.
- Double-buffering avoids CPU writes to the same wake buffer most recently consumed by GPU; stall risk reduced, exact profiler proof pending.
- No-particle active wake frames pay an 8-slot native decay job only while wake state exists, estimated ~2 us active and ~0 us clean idle.
- High-tier billow/shear intentionally spends extra ALU only when `_DynamicWakeParams.y` is high-tier, estimated +3-6 us under dense high-tier particle dispatch for better visible turbulence.

Verification:
- Shader scans: `length(` returned no matches; built-in `normalize(` returned no matches.
- Hot-path static scan found only pre-existing cold scratch `List<>` fields in `HectonFluidEngine` and `ProceduralLeviathanSpineIK`.
- `git diff --check`: CRLF normalization warnings only.
- `dotnet build Hecton8.Core.csproj --no-restore -v:quiet -clp:ErrorsOnly`: still blocked by unrelated global missing namespaces/types (`Hecton8.Environment.Fluids`, `Core.Scheduling`, `Audio.Virtualization`, `Physics.CCD`, `MacroSwarm`, `AcousticAup`, etc.).
- Unity MCP validation: transport failed to `127.0.0.1:8088/mcp`; no active Unity MCP session was reachable.

Status:
- `PENDING VERIFICATION`. Code review and static scans passed for the wake hardening changes. Unity import/console/profiler proof is still absent.

## 2026-05-14 - Verification Refresh

What was wrong:
- The status log still recorded a global compile wall, but the current workspace had moved forward under parallel-agent edits.
- Two stale build errors were investigated and the referenced symbols were present in source. A fresh focused build was required before touching unrelated domains.

What was done:
- Re-inspected `LeviathanTerrainIkJob.TailWhipDurationSeconds` and `PrologueSplashdownSineSweepProbeJob`; both exist in current source.
- Re-ran `dotnet build Hecton8.Core.csproj --no-restore -v:quiet -clp:ErrorsOnly`.
- Re-read and rechecked the wake code path: dynamic wake queueing, fail-closed radius/lifetime guards, AUP shift, low-tier slot clearing, once-per-frame decay, A/B GPU wake buffer upload, RenderGraph payload binding, and shader wake advection.
- Updated `Status_WAKE_TURBULENCE_COMPUTE.md` and `Rationale_WAKE_TURBULENCE_COMPUTE.md` with Loop 8 evidence.

Cinematic cheats used:
- No additional physical simulation was added. The wake remains a controlled visual velocity primitive over particles.
- Low tier still uses two cheap wake turbines. High/Ultra keep the added billow/shear branch as visual overkill.

Exact microseconds saved:
- No new runtime optimization in this pass.
- Static audit preserves prior savings: Low-tier avoids 6 of 8 wake checks per particle, no shader exact `length()`/built-in `normalize()`, and idle clean wake frames skip the two 8-float4 uploads.

Verification:
- `dotnet build Hecton8.Core.csproj --no-restore -v:quiet -clp:ErrorsOnly`: succeeded, 0 errors, 77 warnings.
- Shader scan for `length(` and built-in `normalize(`: no matches.
- Hot-path scan: only pre-existing reusable scratch `List<>` fields were found; no new managed wake container or formatting path.
- `git diff --check` on wake-touched files: no whitespace errors.
- Unity MCP/Editor validation: still unavailable in this shell session, so profiler and in-Editor console proof remain pending.

Status:
- `PENDING VERIFICATION` only because Unity Editor/MCP runtime proof is absent. Focused Core compile is no longer blocked.

## 2026-05-14 - Compile Wall Cleanup

What was wrong:
- Solution-level build exposed three unrelated code symbol breaks after the wake path was focused-clean:
- `PredatorCognitionDomain` emitted `AlphaLeviathanTelemetryFlags.NoPlayerTarget`, but the flag contract did not define it.
- `RelayRouteAuthoringUtility` called `RelayHUDElement.Tick(float)`, but `RelayHUDElement` implements `ILateFrameTickable.LateFrameTick()`.
- `SpatialAudioManager` called the newer four-argument `VirtualVoiceUtility.ComputeStableKey`, while the generated Core project referenced an older `Hecton8.Audio.Virtualization.Contracts.dll` surface.

What was done:
- Added `NoPlayerTarget = 1 << 5` to `AlphaLeviathanTelemetryFlags`.
- Changed relay HUD authoring verification to call `LateFrameTick()`.
- Added a local `ComputeVirtualVoiceStableKey` hash in `SpatialAudioManager` and routed virtual voice enqueue through it.
- Killed an orphaned timed-out `dotnet build` process and reran focused Core with `-m:1 /nr:false`.

Cinematic cheats used:
- None in this cleanup. These were compile and verification unblocks, not wake visual changes.

Exact microseconds saved:
- No new wake runtime savings in this pass.
- Audio stable-key hash remains integer-only and allocation-free; expected cost is below 1 us per queued virtual voice.

Verification:
- `dotnet build Hecton8.Core.csproj --no-restore -v:quiet -clp:ErrorsOnly -m:1 /nr:false`: succeeded, 0 errors, 0 warnings.
- `dotnet build Hecton8.slnx --no-restore -v:quiet -clp:ErrorsOnly -m:1 /nr:false`: failed only on missing generated `Temp/obj/*/project.assets.json` restore artifacts across third-party/editor projects.
- `git diff --check` on the cross-domain compile fixes: no whitespace errors, CRLF normalization warnings only.

Status:
- Wake implementation remains complete and focused Core compile-clean. Full solution verification is blocked by generated project restore artifacts and missing Unity Editor/MCP runtime proof.
