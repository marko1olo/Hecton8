# Status_SPLASHDOWN_FLUID_DYNAMICS

Agent ID: SPLASHDOWN_FLUID_DYNAMICS
Role: FLUID_MECHANIC
Domain: Environment.Fluids / Echelon 2 Abyssal Flow Fields
Task count: 15
Status: PENDING VERIFICATION

## Mandates Read

- [x] CORE_Weather_Abyssal_FlowField_Currents.txt
- [x] REND_VFX_Fluid_Aesthetics_Compute_Particles.txt
- [x] MATH_Coordinate_Precision_AUP_FloatingOrigin.txt
- [x] OPT_Zero_GC_Policy_AllocFree_Mandate.txt
- [x] OPT_Cinematic_Cheat_Protocol_Visual_Fake_First.txt
- [x] MATH_Rsqrt_i3_SIMD.txt
- [x] DBG_Telemetry_Crash_Reporting_PostMortem.txt

## Prompt Extraction

- [x] Extracted `<AGENT_PROMPT id="SPLASHDOWN_FLUID_DYNAMICS">` from `Docs/Tasks/CURRENT_BATCH.md` via PowerShell raw file read and regex, opening tag through closing tag. DOD: CLI extraction, not MCP partial read. Rejected: relying on neighboring prompts or archive logs. Estimate: one cold file read.
- [x] Re-extracted after implementation with regex allowing XML attributes: `<AGENT_PROMPT id="SPLASHDOWN_FLUID_DYNAMICS" ...>`. DOD: three-task anti-amnesia pass. Rejected: archive batch matches. Estimate: 25 us regex scan over hot file cache.
- [x] Re-extracted during 2026-05-14 follow-up audit. DOD: cover-to-cover XML block confirmed before patching job sequencing. Rejected: trusting previous chat summary. Estimate: hot-cache regex scan, ~25 us.
- [x] Re-extracted during 2026-05-14 layout audit. DOD: prompt requirements checked against buffer layout and shader consumers before patching. Rejected: assuming previous implementation was spatially correct. Estimate: hot-cache regex scan, ~25 us.
- [x] Re-extracted during 2026-05-14 quality-drop audit. DOD: prompt Low/MX350 vector-overwrite bypass checked before modifying completion/upload logic. Rejected: relying on stale summary state. Estimate: hot-cache regex scan, ~25 us.
- [x] Re-extracted during 2026-05-14 grid-center audit. DOD: prompt radial field correctness checked against shader world-position mapping. Rejected: treating the previous flat-index fix as sufficient without coordinate proof. Estimate: hot-cache regex scan, ~25 us.
- [x] Re-extracted during 2026-05-14 duplicate-signal audit. DOD: `Runs in SIMULATION once upon impact` checked against multiple prologue completion publishers. Rejected: assuming one producer in a 20+ agent batch. Estimate: hot-cache regex scan, ~25 us.
- [x] 2026-05-14 fallback-buffer audit extraction [BLOCKED BY LIVE BATCH REPLACEMENT]: live `Docs/Tasks/CURRENT_BATCH.md` no longer contains `<AGENT_PROMPT id="SPLASHDOWN_FLUID_DYNAMICS">`; it now contains a different batch. DOD fallback: continued from this status/rationale durable record and logged the mismatch. Rejected: acting on neighboring/current unrelated prompts. Estimate: hot-cache `rg`, ~25 us. Status remains PENDING VERIFICATION.

## Tasks

- [x] 1. SINGLETON ERADICATION: Extended existing `HectonFluidEngine`; no new singleton/runtime owner. DOD: existing owner of abyssal buffers used. Rejected: `SplashdownFluidRuntime` scene component. Estimate: avoids one scene lookup/registration, ~8 us cold only.
- [x] 2. SIGNAL MIGRATION: Consumes `SignalBus<PrologueCompleteSignal>.GetFrameSnapshot()` and filters `PhaseOceanHandoff` / forced whiteout. DOD: decoupled signal read, no direct prologue reference. Rejected: direct `CameraJuice` coupling. Estimate: one span pass per frame, <4 us for normal signal counts.
- [x] 3. ASMDEF ISOLATION: Added `FluidImpulseJob` under existing `Hecton8.Environment.Fluids` asmdef that references `Contracts`. DOD: domain-local Burst job. Rejected: root `Hecton8.Core` job type. Estimate: zero runtime cost.
- [x] 4. VECTOR FIELD OVERRIDE: Created Burst `FluidImpulseJob` with persistent `NativeArray<float4>` output and stats. DOD: isolated local compile against Unity assemblies passed. Rejected: per-cell managed loop in `FixedTick`. Estimate: saves 200-450 us on i3 versus managed field mutation.
- [x] 5. RADIAL PUSH: Job writes cells within 30m radius and shader applies the override buffer to structured and texture flow outputs. DOD: distance-square culling and finite guards. Rejected: unbounded global impulse. Estimate: affected cells only; avoids 32^3 full expensive math beyond simple cull.
- [x] 6. DECAY MATH: Uses inverse-square gain with `math.rcp(max(distanceSq, 1f))` and `math.rsqrt` normalization. DOD: center divide-by-zero blocked. Rejected: `sqrt` + divide normalization. Estimate: ~30-70 us saved across 32^3 cells on low CPU.
- [x] 7. DAMPENING: `ResolveSplashdownImpulseParams()` feeds a 10-second linear strength scale to `AbyssalFlowField.compute`. DOD: shader blends back to ambient. Rejected: CPU buffer rewrite every frame. Estimate: saves one 512 KB upload per frame during decay.
- [x] 8. SPAWN BURST: Queues exactly 500 bubble slots into the existing advected bubble structured buffers. DOD: fixed capacity, ring cursor. Rejected: `ParticleSystem.Emit` as primary truth. Estimate: one fixed upload event, no per-frame spawn GC.
- [x] 9. INITIAL VELOCITY: Bubble velocity uses the same radial/upward inverse-square vector and max-velocity clamp. DOD: `math.rsqrt` and finite sanitize. Rejected: random velocity spray. Estimate: stable GPU advection input, no CPU simulation loop.
- [x] 10. AUP SHIFT SAFETY: Converts `CapsuleAup` to runtime and rebases active impulse position on origin shift. DOD: runtime-native buffers; uploaded vector/falloff buffer remains valid under uniform origin rebases. Rejected: storing raw world doubles in shader path or killing active decay on shift. Estimate: avoids per-cell double math.
- [x] 11. MATH LOD: Low/Unknown/MX350/low-memory tiers bypass the 3D impulse job and spawn bubbles only. DOD: device-tier gate. Rejected: universal vector overwrite. Estimate: saves ~250-600 us on i3/MX350 impact frame.
- [x] 12. EXECUTION PHASE: Signal drained once per Unity frame from `FixedTick` SIMULATION lane; duplicate sequences ignored. DOD: `_lastProcessedSplashdownFrame` and `_lastProcessedSplashdownSequence`. Rejected: late-frame visual polling. Estimate: avoids repeated impulse scheduling.
- [x] 13. ZERO-GC: Hot path uses persistent `NativeArray` / `GraphicsBuffer` state, no LINQ or managed collections in impact drain. DOD: rg scan for LINQ/new in touched paths; new allocations are cold persistent buffers only. Rejected: heap bubble list. Estimate: 0 B managed per splash hot path.
- [x] 14. BLACKBOX DUMP: `AbyssalFlowTelemetryEntry` now stores `FluidImpulseCount`; dump path is `Docs/AgentLogs/Dump_SPLASHDOWN_FLUID_DYNAMICS.bin`. DOD: telemetry hash includes count. Rejected: chat-only diagnostics. Estimate: no frame cost except existing ring write.
- [x] 15. OMEGA COMPILE CHECK: Verified `math.rsqrt` in job and bubble velocity path; `FluidImpulseJob` compiles in isolation. [BLOCKED BY UNITY SESSION FOR FULL UNITY COMPILE] Unity MCP readiness timed out and later reported no session; generated `Hecton8.Core.csproj` has unrelated stale reference failures. Estimate: no runtime estimate; verification blocked externally.

## Loop Log

- Loop 0: Existing engine scanned. `HectonFluidEngine` already owns abyssal flow buffers, advected bubble buffers, `PrologueCompleteSignal` exists in `GlobalSignals`, and GPU advection consumes `_AbyssalFlowFieldResult` / `_AbyssalFlowFieldTexture`. Implementation will stay inside `HectonFluidEngine` and existing compute shader path.
- Loop 1: Shader review found the helper was declared but not wired. Patched `UpdateAbyssalFlowField` and `UpdateAbyssalFlowTexture` to apply `_AbyssalSplashdownImpulseBuffer`.
- Loop 2: C# review found stale affected-count carryover and old dump filename. Patched `_lastSplashdownFluidImpulseCount = 0` on new signal and switched dump path to `Dump_SPLASHDOWN_FLUID_DYNAMICS.bin`.
- Loop 3: Isolated `FluidImpulseJob` compile passed using Unity 6000.4.1f1 assemblies. Full Unity compile unavailable through MCP.
- Loop 4: Local compiler pass over `HectonFluidEngine.cs` found dependency-reference noise only; no syntax errors or `Splashdown` / `FluidImpulse` semantic errors surfaced in the filtered output.
- Loop 5: Prompt re-read and rg scan verified rsqrt usage, fixed-capacity bubble writes, and no LINQ allocations in the inserted hot-path methods.
- Loop 6: Omega polish audit removed origin-shift upload invalidation because the impulse buffer stores relative vectors/falloff, not absolute positions.
- Loop 7: Re-audit found `_gpuSplashdownImpulseBuffer` was allocated with base abyssal buffers even on Low/MX350 or no-splash sessions. Patched lazy allocation in `EnsureSplashdownImpulseGpuBuffer()`. DOD: base GPU flow init no longer pays splashdown VRAM until a non-low splash actually schedules. Rejected: keeping 512 KB resident for a rare cinematic. Estimate: saves 512 KB VRAM plus buffer creation on cold low/base paths.
- Loop 8: Re-audit found off-volume splashes could schedule a 32^3 job that writes zero useful cells. Patched `IsSplashdownInsideFlowVolume()` guard; off-volume impacts keep the 500-bubble cinematic fake and skip vector-field work. DOD: bounded AABB plus splash radius. Rejected: letting Burst prove emptiness by scanning 32768 cells. Estimate: saves ~250-600 us CPU and one 512 KB upload on invalid/off-volume impact frames.
- Loop 9: Lazy-buffer review found a base abyssal GPU-buffer reset could release the splash buffer while leaving `_splashdownImpulseUploaded` true. Patched `ReleaseGpuAbyssalFlowBuffers()` to disable the uploaded flag when the buffer is released. DOD: shader params cannot stay active with only the empty fallback bound. Rejected: reallocate splash buffer during every base flow reset. Estimate: avoids both GPU out-of-bounds risk and unnecessary recovery allocation.
- Loop 10: Re-audit found a second signal arriving while `FluidImpulseJob` is active could reset splash state before the first job uploads. Patched pre-drain completion and made `ScheduleSplashdownImpulseField()` return success/failure. DOD: active vector wake is no longer clobbered by bubble-only or busy fallback telemetry. Rejected: blocking main thread on `JobHandle.Complete()` for every new signal. Estimate: avoids a potential 0.2-0.6 ms stall and preserves the already scheduled pressure wave.
- Loop 11: Re-audit found Low-tier resolution only stopped new vector jobs, not an already uploaded vector wake if quality dropped mid-decay. Patched `ResolveSplashdownImpulseParams()` to return zero while Low/MX350/low-memory is active. DOD: low path performs bubble/drift presentation only. Rejected: releasing the buffer on quality drop, which would force reallocation if quality recovers. Estimate: prevents 32768 shader buffer reads per flow dispatch on low tier during remaining decay.
- Loop 12: Re-audit found zero-cell or invalid jobs still uploaded a full 512 KB field. Patched `SplashdownImpulseNoAffectedCellsFlag` and skipped upload when affected count is zero. DOD: no useful vector field means no GPU upload. Rejected: uploading zeros for bookkeeping. Estimate: saves one 512 KB upload on invalid/no-cell impact edge cases.
- Loop 13: Re-audit found `FluidImpulseJob` wrote cells as `x + y*res + z*res^2` while the structured abyssal flow kernel reads node indices as `x + z*res + y*res^2`. Patched the job to the structured layout and made the texture kernel use `ResolveFlatIndex(coord)`. DOD: structured buffer and 3D texture now sample the same splash cell. Rejected: duplicating a separate texture-layout upload. Estimate: removes spatially wrong pressure-wave reads without extra memory or dispatches.
- Loop 14: Re-audit found a high-tier splash job could finish after a runtime Low/MX350 quality drop, still uploading and retaining the 512 KB vector buffer before shader params suppressed sampling. Patched completion to publish Low-tier telemetry, clear active timing, and release the lazy splash buffer instead of uploading. DOD: Low-tier bypass enforced at completion as well as scheduling and shader-param resolution. Rejected: upload-then-zero-params and unsafe job cancellation. Estimate: saves one 512 KB upload plus 512 KB VRAM retention on the quality-drop edge.
- Loop 15: Re-audit found `ResolveNodeWorldPosition()` still used integer half-extents, placing a 32-cell volume at `[-50m, 46.875m]` while `FluidImpulseJob` and the 3D texture use cell centers `[-48.4375m, 48.4375m]`. Patched the structured shader path to derive world positions from `((coord + 0.5) / resolution - 0.5) * worldSize`. DOD: structured flow, texture flow, and splash impulse now share one centered coordinate convention. Rejected: keeping a half-cell bias or duplicating separate impulse coordinates. Estimate: no extra runtime cost; removes spatial drift without extra buffers.
- Loop 16: Re-audit found multiple systems can publish `PrologueCompleteSignal`, while `DrainSplashdownFluidSignals()` could queue more than one 500-bubble burst in the same snapshot or repeat on sequence-zero publishers across frames. Patched a one-shot `_splashdownImpactConsumed` gate, added source-hash tracking, made `QueueSplashdownFluidImpulse()` report whether a valid signal was handled, and reset the guard on splashdown state disposal. DOD: one valid cinematic impact per prologue handoff; invalid coordinate packets do not consume the event. Rejected: letting duplicate publishers spawn repeated bubble rings or relying on sequence alone. Estimate: prevents 500 duplicate bubble writes plus two full bubble-buffer uploads per duplicate signal.
- Loop 17: Re-audit found lazy splash-buffer allocation made the no-splash compute binding depend on full fluid-advection state. After `DisposeFluidAdvectionState()` and re-enable, `FixedTick` could dispatch abyssal flow before `LateFrameTick` rebuilt `_emptyAbyssalFlowBuffer`. Patched `EnsureEmptyAbyssalFlowFallbackBuffer()` and called it from `EnsureGpuAbyssalFlowBuffers()`. DOD: compute always gets a valid one-element zero fallback while the 512 KB splash vector buffer remains lazy. Rejected: rebuilding all advection buffers in the abyssal dispatch path or reintroducing the full splash buffer on base flow init. Estimate: prevents invalid/null `SetBuffer` edge; keeps no-splash memory at one float4 fallback instead of 512 KB.

## Verification

- `mcp__unityMCP__.refresh_unity`: timed out after 60s waiting for editor readiness.
- `mcp__unityMCP__.validate_script` / `read_console`: Unity session unavailable.
- `dotnet build Hecton8.Core.csproj --no-restore`: blocked by pre-existing generated-project reference failures (`Hecton8.Environment.Fluids`, `Hecton8.Core.Scheduling`, audio virtualization, brine samples, tether signals, etc.).
- Local isolated compile of `Assets/_Project/Scripts/Environment/Fluids/FluidImpulseJob.cs`: PASS.
- Local compiler syntax-risk pass over `HectonFluidEngine.cs`: no splashdown syntax errors; dependency errors are external to this patch path.
- 2026-05-14 `git diff --check` on splashdown-touched paths: PASS except Git warning that `HectonFluidEngine.cs` LF will be converted to CRLF next touch.
- 2026-05-14 `dotnet build Hecton8.Core.csproj --no-restore /p:UseSharedCompilation=false`: BLOCKED by 130 pre-existing cross-domain missing-reference errors; observed `HectonFluidEngine.cs` error remains the stale `Hecton8.Environment.Fluids` assembly reference at line 47, not the new splashdown edits.
- 2026-05-14 isolated Roslyn `FluidImpulseJob.cs` probe: BLOCKED by local Unity Mono Roslyn missing `System.Text.Encoding.CodePages, Version=4.1.1.0`; earlier isolated compile result remains the last valid PASS.
- 2026-05-14 Unity MCP `read_console`: BLOCKED, `no_unity_session`.
- 2026-05-14 follow-up `git diff --check`: PASS except Git LF-to-CRLF warnings on touched files.
- 2026-05-14 follow-up `dotnet build Hecton8.Core.csproj --no-restore /p:UseSharedCompilation=false`: TIMED OUT after 120s against the same generated-project missing-reference wall; stale `HectonFluidEngine.cs(47,27)` namespace reference remains the only HectonFluidEngine line surfaced before timeout.
- 2026-05-14 follow-up Unity MCP `read_console`: BLOCKED by HTTP transport failure to `127.0.0.1:8088/mcp`.
- 2026-05-14 layout audit `git diff --check` on splashdown C#/compute paths: PASS except Git LF-to-CRLF warnings.
- 2026-05-14 layout audit isolated `FluidImpulseJob.cs` compile through .NET Roslyn with Unity references: PASS.
- 2026-05-14 layout audit Unity MCP `read_console`: BLOCKED by HTTP transport failure to `127.0.0.1:8088/mcp`.
- 2026-05-14 quality-drop audit `git diff --check` on splashdown paths: PASS except Git LF-to-CRLF warnings.
- 2026-05-14 quality-drop audit static allocation scan on touched C# files: only pre-existing field-level `List<T>` allocations at `HectonFluidEngine.cs:1262` and `HectonFluidEngine.cs:1266`; no new splashdown hot-path allocation pattern found.
- 2026-05-14 quality-drop audit isolated `FluidImpulseJob.cs` compile through .NET Roslyn with live `Library/ScriptAssemblies` Unity references: PASS.
- 2026-05-14 quality-drop audit `dotnet build Hecton8.Core.csproj --no-restore /p:UseSharedCompilation=false /m:1`: TIMED OUT after 94s against the known generated-project dependency wall; leftover `dotnet` build/compiler processes were terminated after the timeout.
- 2026-05-14 quality-drop audit Unity MCP `read_console`: BLOCKED by HTTP transport failure to `127.0.0.1:8088/mcp`.
- 2026-05-14 grid-center audit `git diff --check` on splashdown C#/compute/doc paths: PASS except Git LF-to-CRLF warnings.
- 2026-05-14 grid-center audit isolated `FluidImpulseJob.cs` compile through .NET Roslyn with live `Library/ScriptAssemblies` Unity references: PASS.
- 2026-05-14 grid-center audit local shader compiler lookup: no `dxc` or `fxc` executable found in PATH or Unity `Editor/Data/Tools`; shader compile remains Unity-import blocked.
- 2026-05-14 grid-center audit Unity MCP `read_console`: BLOCKED by HTTP transport failure to `127.0.0.1:8088/mcp`.
- 2026-05-14 grid-center audit cleanup: leftover `dotnet` compiler worker processes from the isolated compile probe were terminated.
- 2026-05-14 duplicate-signal audit `git diff --check` on splashdown C#/compute/doc paths: PASS except Git LF-to-CRLF warnings.
- 2026-05-14 duplicate-signal audit isolated `FluidImpulseJob.cs` compile through .NET Roslyn with live `Library/ScriptAssemblies` Unity references: PASS.
- 2026-05-14 duplicate-signal audit static allocation scan on touched C# files: only pre-existing field-level `List<T>` allocations at `HectonFluidEngine.cs:1264` and `HectonFluidEngine.cs:1268`; no new splashdown hot-path allocation pattern found.
- 2026-05-14 duplicate-signal audit Unity MCP `read_console`: BLOCKED; only placeholder tool available in this request and it reported unsupported/unavailable.
- 2026-05-14 duplicate-signal audit cleanup: leftover `dotnet` compiler worker processes from the isolated compile probe were terminated.
- 2026-05-14 fallback-buffer audit `git diff --check` on splashdown C#/compute/doc paths: PASS except Git LF-to-CRLF warning on `HectonFluidEngine.cs`.
- 2026-05-14 fallback-buffer audit static allocation scan on touched C# files: only pre-existing field-level `List<T>` allocations at `HectonFluidEngine.cs:1269` and `HectonFluidEngine.cs:1273`; no new splashdown hot-path allocation pattern found.
- 2026-05-14 fallback-buffer audit isolated `FluidImpulseJob.cs` compile through .NET Roslyn with live `Library/ScriptAssemblies` Unity references: PASS.
- 2026-05-14 fallback-buffer audit full `dotnet build Hecton8.Core.csproj --no-restore /p:UseSharedCompilation=false /m:1`: TIMED OUT after 120s against the known generated-project dependency wall; leftover `dotnet`/`VBCSCompiler` workers were terminated after the timeout.
- 2026-05-14 fallback-buffer audit Unity MCP resources: BLOCKED; no MCP resources/tools exposed for Unity in this session, so console/runtime verification remains unavailable.
