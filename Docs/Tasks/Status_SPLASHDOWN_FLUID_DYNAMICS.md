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

## Verification

- `mcp__unityMCP__.refresh_unity`: timed out after 60s waiting for editor readiness.
- `mcp__unityMCP__.validate_script` / `read_console`: Unity session unavailable.
- `dotnet build Hecton8.Core.csproj --no-restore`: blocked by pre-existing generated-project reference failures (`Hecton8.Environment.Fluids`, `Hecton8.Core.Scheduling`, audio virtualization, brine samples, tether signals, etc.).
- Local isolated compile of `Assets/_Project/Scripts/Environment/Fluids/FluidImpulseJob.cs`: PASS.
- Local compiler syntax-risk pass over `HectonFluidEngine.cs`: no splashdown syntax errors; dependency errors are external to this patch path.
