# Status - WAKE_TURBULENCE_COMPUTE

Prompt: Leviathan & Pod Advection
Agent: VFX_TECHNICAL_ARTIST
Domain: Environment.Fluids / VFX
Status: PENDING VERIFICATION

## Hygiene

- Prompt extracted from `Docs/Tasks/CURRENT_BATCH.md`: yes
- Existing status file at session start: missing
- Existing rationale file at session start: missing
- Task count in XML tag: 15
- Polish mandate parsed: yes

## Relevant Mandates

- [x] CORE_Weather_Abyssal_FlowField_Currents.txt
- [x] REND_VFX_Fluid_Aesthetics_Compute_Particles.txt
- [x] GPU_Compute_Kernels_Kernels_Optimization_MX350.txt
- [x] GPU_Compute_Warp_Sizing_Mobile.txt
- [x] MATH_Coordinate_Precision_AUP_FloatingOrigin.txt
- [x] DBG_Telemetry_Crash_Reporting_PostMortem.txt
- [x] OPT_Zero_GC_Policy_AllocFree_Mandate.txt
- [x] OPT_Cinematic_Cheat_Protocol_Visual_Fake_First.txt

## Tasks

- [x] 1. SINGLETON ERADICATION: Extend `HectonFluidEngine`.
  DOD: Added wake state, buffers, payload binding, and telemetry inside the existing fluid service; no singleton or concrete producer dependency added. Rejected: new wake manager singleton. Estimate: 18 us/frame worst-case when 8 wakes active.
- [x] 2. SIGNAL MIGRATION: Consume `FluidImpulseSignal(AUP, Vector, Radius, Lifetime)`.
  DOD: Added typed `SignalBus<FluidImpulseSignal>` lane and drain into fluid advection. Rejected: direct calls from Leviathan/Drop Pod into `HectonFluidEngine`. Estimate: 3 us/frame for 32-lane snapshot scan; zero when empty.
- [x] 3. ASMDEF ISOLATION: `Hecton8.Environment.Fluids` -> `Contracts`.
  DOD: Verified `Hecton8.Environment.Fluids.asmdef` already references `Hecton8.Environment.Fluids.Contracts`; avoided adding a reverse Core dependency that would create a cycle. Rejected: putting `ISignal` in fluids contracts because `ISignal` is Core-owned. Estimate: 0 runtime us.
- [x] 4. WAKE S.O.A.: Define `StructuredBuffer<float4> _DynamicWakes`; max 8 active wakes.
  DOD: Added `_DynamicWakes` xyz/intensity and `_DynamicWakeVectors` vector/radius buffers, each fixed at 8 slots. Rejected: AoS managed class list. Estimate: 256 bytes upload per visual dispatch.
- [x] 5. SIGNAL DRAIN: Dead-slot wake allocation from `FluidImpulseSignal`.
  DOD: Dead slots are reused first, weakest live slot is replaced only when full, and low tier uses a 2-slot limit. Rejected: queue growth or dynamic allocation. Estimate: 4 us for full 8-slot scan.
- [x] 6. SHADER MATH: 8-wake vortex/push injection in `Hecton_FluidAdvection.compute`; no `length()`.
  DOD: Shader iterates a fixed 8 slots, uses `dot(delta, delta)` radius tests, cross-product vortex, and dot-gated push. Rejected: `length()`/normalize built-ins in the particle loop. Estimate: Low 2 wakes = ~14 ALU/particle; High 8 wakes = ~56 ALU/particle.
- [x] 7. LEVIATHAN TIE-IN: Emit impulse on sharp Alpha Leviathan tail direction change.
  DOD: `ProceduralLeviathanSpineIK` emits `FluidImpulseSignal` when a leviathan tail direction dot drops under threshold with cooldown. Rejected: polling fluid engine or coupling to creature controller internals. Estimate: 2 us on candidate tail ticks.
- [x] 8. DROP POD TIE-IN: Emit 50m splashdown push impulse.
  DOD: `OrbitalRelativityDirector` emits a 50m `FluidImpulseSignal` at `PrologueCompleteSignal` ocean handoff. Rejected: expanding the existing abyssal splash field only, because the prompt requires the new wake impulse path. Estimate: <1 us once per prologue.
- [x] 9. DECAY: Native job reduces intensity by `dt * decayRate` before upload.
  DOD: `DynamicWakeDecayJob` decays `_DynamicWakes.w` and CPU lifetime in a Burst `IJobParallelFor.Run` immediately before GPU upload. Rejected: managed per-frame list iteration. Estimate: 2 us for 8 slots.
- [x] 10. AUP SHIFT SAFETY: Subtract `AupShiftSignal` from active wake AUPs.
  DOD: fluid advection drain applies `-signal.ShiftMeters` to active wake positions when consuming `AupShiftSignal`. Rejected: also shifting in `OnOriginShift`, because that can double-apply when both origin event and signal fire. Estimate: 1 us per active wake on shift frames only.
- [x] 11. MATH LOD: Low tier caps active wakes to 2.
  DOD: `ResolveDynamicWakeSlotLimit(lowTier)` limits allocation, decay, count, and shader iteration to 2 on Unknown/Low/Mx350. Rejected: shader-only cap that leaves CPU reporting 8 active wakes. Estimate: Low saves ~75% wake ALU.
- [x] 12. EXECUTION PHASE: Compute dispatch in `VISUAL_SYNC`.
  DOD: RenderGraph dispatch is routed through `VisualSyncRenderPassEvent = RenderPassEvent.BeforeRenderingTransparents`, the existing visual phase before transparent particle presentation. Rejected: fixed/update dispatch or CPU particle mutation. Estimate: no extra dispatch phase beyond existing advection pass.
- [x] 13. ZERO-GC: Array updates allocate 0 bytes.
  DOD: Wake state uses fixed `NativeArray` buffers and `GraphicsBuffer` uploads; signal reads use `ReadOnlySpan`. Rejected: managed `List<Wake>` or per-frame arrays. Estimate: 0 B/frame managed allocation in wake path.
- [x] 14. BLACKBOX DUMP: Push `ActiveTurbulenceWakes` to telemetry.
  DOD: Added `ActiveTurbulenceWakes` to the 300-frame fluid advection telemetry ring, global warning telemetry, and `Dump_WAKE_TURBULENCE_COMPUTE.bin`. Rejected: chat-only reporting. Estimate: one int in the ring entry; no hot allocation.
- [x] 15. OMEGA COMPILE CHECK: Verify GPU buffer mapping. [BLOCKED BY DEPENDENCY: global compile wall]
  DOD: Static mapping verified from C# property IDs to RenderGraph imports/use calls to compute shader declarations. Unity MCP compile was unavailable and dotnet builds are blocked by unrelated generated/reference failures. Rejected: claiming a clean compile. Estimate: 0 runtime us.

## Iteration Log

### Loop 0 - Intake

- Extracted prompt and counted 15 tasks.
- Created status/rationale files because none existed at session start.
- Next: read mandates and inspect current fluid/compute implementation.

### Loop 1 - Tasks 1-5

- Implemented the signal lane, wake SOA buffers, dead-slot allocation, payload binding, and C# drain path.
- Re-extracted prompt with an attribute-aware CLI scan after the strict id-only regex missed prompts with role/chat_name attributes.
- Compile attempt 1: `dotnet build Hecton8.Core.csproj --no-restore -v:quiet -clp:ErrorsOnly` failed before task-specific validation on unrelated missing generated asmdef references (`Audio.Virtualization`, `Core.Scheduling`, `Environment.Fluids`, etc.).
- Compile attempt 2: Unity MCP validation failed because no Unity session was available.
- Compile attempt 3: `dotnet build Hecton8.slnx --no-restore -v:quiet -clp:ErrorsOnly` failed on missing `project.assets.json` for editor/sample projects plus the same global reference faults. No syntax errors from the touched wake files were reported before the global wall.

### Loop 2 - Tasks 6-10

- Re-read the wake prompt with an attribute-aware PowerShell regex and verified task text.
- Verified shader has no `length()` matches and uses squared distance for radius gating.
- Added and re-read producer tie-ins for Leviathan tail direction changes and Drop Pod splashdown.
- Removed duplicate origin-listener wake shifting; active wake AUPs are shifted only from `AupShiftSignal` per prompt.

### Loop 3 - Tasks 11-15

- Verified low-tier cap is applied in slot allocation, native decay clearing, telemetry count, and shader slot limit.
- Added explicit `VisualSyncRenderPassEvent` constant to the RenderGraph pass that dispatches advection before transparents.
- Static GPU mapping check: `_DynamicWakes`, `_DynamicWakeVectors`, and `_DynamicWakeParams` exist in shader, property IDs, bind/unbind, payload, RenderGraph import, and use-buffer calls.
- Zero-GC scan found only pre-existing cold reusable `List<>` fields in touched files; no new per-frame managed containers, `foreach`, string formatting, or unload calls were introduced.

### Loop 4 - Omega Polish

- Parsed the first `<POLISH_MANDATE>` after `WAKE_TURBULENCE_COMPUTE` only after all 15 tasks were checked/blocked.
- Re-scanned touched hot paths for `math.sqrt`, `math.normalize`, shader `length()`, managed `foreach`, string formatting, `.ToString()`, and dynamic containers.
- Confirmed new exact length calculations use `math.rsqrt`/`rsqrt`; shader radius tests use `dot(delta, delta)` and `rcp`.
- `git diff --check` reported CRLF normalization warnings only.

### Loop 5 - Recursive Re-Verification

- Re-extracted the wake prompt and rechecked all 15 tasks against the implemented files.
- Shader `length(` scan returned no matches.
- Verified the named evidence points: `DynamicWakeDecayJob`, `FluidImpulseSignal` drain, 50m splash radius, Leviathan tail threshold, `ActiveTurbulenceWakes`, and GPU wake bindings.
- Remaining verification block is external: no Unity MCP session, and dotnet builds fail on unrelated project reference/generated asset faults.
