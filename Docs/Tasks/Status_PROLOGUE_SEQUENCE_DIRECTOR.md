# Status_PROLOGUE_SEQUENCE_DIRECTOR

Authority: `Docs/Tasks/CURRENT_BATCH.md` tag `PROLOGUE_SEQUENCE_DIRECTOR`.
Domain: Echelon 8 Presentation & UX / AUP Narrative Triggers + VWS.
Status: PENDING VERIFICATION.

## Mandates Read

- `ARCH_Global_Registry_ServiceLocator_DI_Init.txt`
- `ARCH_Project_Bootstrap_Sequence_Init_Safety.txt`
- `OPT_Zero_GC_Policy_AllocFree_Mandate.txt`
- `DBG_Telemetry_Crash_Reporting_PostMortem.txt`
- `STRM_World_Streaming_Residency_Chunk_Management.txt`
- `AUD_DSP_Audio_Synthesis_ThreadSafe_SPSC.txt`
- `CTRL_Device_Abstraction_Haptics.txt`
- `UI_Diegetic_Physical_Interfaces.txt`
- `CORE_Global_State_Reset_NonReload_Transitions.txt`

## Loop 0 - Prompt Extraction

- [x] Extract own XML prompt with CLI | DOD: PowerShell raw read + regex against `Docs/Tasks/CURRENT_BATCH.md`; no MCP truncation. Alternative rejected: relying on chat copy. Estimate: 900 us.
- [x] Read domain authority | DOD: `Docs/Actual Domains of Project.txt` inspected and matched to Echelon 8 Presentation & UX. Alternative rejected: inventing narrative domain. Estimate: 700 us.
- [x] Initialize agent status/rationale files | DOD: own files created because no prior own-state existed. Alternative rejected: using chat as durable state. Estimate: 600 us.

## Primary Tasks

- [x] Task 1 - Singleton eradication | DOD: `IPrologueSequenceService` added to Core.Contracts and registered through `GlobalRegistry.RegisterPrologueSequenceRuntime`; no `PrologueManager.Instance` path introduced. Alternative rejected: scene singleton/FindObject polling. Estimate: 14 us cold registration, 0 us wait-loop cost.
- [x] Task 2 - Signal migration | DOD: Core bridge consumes `AtmosphericReentrySignal` through `SignalBus` snapshots and emits direct `SystemPauseSignal` for input lock flags. Alternative rejected: destructive legacy dequeue cursor shared with VFX. Estimate: 2-5 us per polled frame.
- [x] Task 3 - ASMDEF isolation | DOD: `Hecton8.Narrative.Prologue.asmdef` references only `Hecton8.Core.Contracts`, `Unity.Collections`, and `Unity.Mathematics`; concrete domains live in Core bridge. Alternative rejected: direct Core/World/Audio refs from narrative assembly. Estimate: 0 us runtime, compile boundary only.
- [x] Task 4 - Awaitable sequence runner | DOD: `AwaitableDropSequenceDirector.RunPrologueSequenceAsync(CancellationToken)` implements the state machine and passes tokens to every Awaitable wait. Alternative rejected: coroutine/Update runner. Estimate: 4-12 us per frame while waiting.
- [x] Task 5 - Stage 1 orbital silence | DOD: sequence locks look+translation flags, publishes muffled breathing through mixer/acoustic DSP signals, and waits 3 seconds via `DelayDilatedAsync`. Alternative rejected: `Task.Delay` or hard-coded unscaled timer. Estimate: 6 us start cost, 0 allocations in wait loop.
- [x] Task 6 - Stage 2 re-entry burn | DOD: emits `VocalWarningSignal` with `HullTempCritical` hash mapped to VWS, emits `HapticRequest` heavy rumble, and awaits Mach 10 via orbital snapshot/atmospheric fallback. Alternative rejected: adding a new VWS clip table dependency or blind timer. Estimate: 5-14 us per wait frame.
- [x] Task 7 - Stage 3 manual override | DOD: look-input is unlocked by only retaining translation lock and `DiegeticHudSignal`/HUD hash prompt requests manual release. Alternative rejected: world-space UI prefab direct reference. Estimate: 4 us publish cost.
- [x] Task 8 - Impact synchronization | DOD: `PrologueCompleteSignal` snapshot gates impact and then awaits exactly one `NextFrameAsync(ct)`. Alternative rejected: same-frame water swap or hard-coded delay. Estimate: one frame, 0 allocations.
- [x] Task 9 - Chunk hydration wait | DOD: bridge checks configured `IStreamingBackpressureService.IsChunkResident(oceanSurfaceChunkId)` and `SectorResidencyHydratedSignal` snapshots; no N-second wait. Alternative rejected: scene load sleep or concrete world-manager dependency in the bridge. Estimate: 3-12 us per wait frame.
- [x] Task 10 - Water transition | DOD: calls `ForceZeroUniverseVelocity`, `CameraJuiceSignals.PublishImpact(1f)`, and emits ocean-handoff `PrologueCompleteSignal` consumed by `HectonFluidEngine`. Alternative rejected: direct fluid engine mutation from narrative. Estimate: 10-20 us transition publish cost.
- [x] Task 11 - Dev skip protocol | DOD: dev-only skip reads `GlobalRegistry.IsDevelopmentBuild`, input cancel/chord, cancels sequence, forces shallow-water hydration proxy, zeros velocity, and publishes ocean handoff. Alternative rejected: scene-search skip button or release-only skip path. Estimate: 8-18 us on skip poll, 0 us when non-dev gated.
- [x] Task 12 - Zero-GC waiting loops | DOD: wait loops use interface reads, `ReadOnlySpan` signal snapshots, fixed `NativeArray` black-box, and existing Awaitable frame/dilated waits; allocations are cold setup or fault-only dump IO. Alternative rejected: LINQ, managed timers, per-frame subscriptions, string stage names. Estimate: 4-14 us per polled frame; profiler proof absent, status remains PENDING VERIFICATION.
- [x] Task 13 - Blackbox dump | DOD: `PrologueStage` telemetry is published hash-only through `GlobalTelemetryBus.PublishPrologueStage`, and the last 300 sequence samples dump to `Docs/AgentLogs/Dump_PROLOGUE_SEQUENCE_DIRECTOR.bin` on fault/NaN. Alternative rejected: managed log lists or chat-only forensic state. Estimate: 3-8 us on stage change, fault dump off hot path.
- [x] Task 14 - Math LOD hydration bypass | DOD: low-tier path allows `ActiveImpostorCount`/proxy residency to resume before high-res ocean chunk hydration; high tier keeps high-res residency gate. Alternative rejected: one-size blind black-screen delay. Estimate: avoids unbounded wait on MX350/i3; per-frame check 2-6 us.
- [x] Task 15 - Omega compile check | DOD: static scan found no `Task.Delay`, coroutine, `StartCoroutine`, or gameplay `Update` in prologue path; delay uses `AwaitableExtension.DelayDilated` via runtime port. Alternative rejected: managed timer or coroutine. Estimate: 0 B managed timer avoided; compile remains blocked by unrelated project failures.

## Iterative Self-Review

- [x] Loop 1 - Tasks 1-5 contract/readback | Result: verified registry slot, asmdef isolation, Awaitable signature, Stage 1 input/audio/dilated wait. Fixes: none. Estimate: 1400 us.
- [x] Loop 2 - Tasks 6-10 signal/readback | Result: verified VWS/haptics/manual prompt/impact/hydration/water handoff signal paths. Fixes: added black-box samples in wait loops before this checkpoint. Estimate: 1800 us.
- [x] Loop 3 - Tasks 11-15 zero-GC/static scan | Result: verified skip path, telemetry path, low-tier proxy gate, and no forbidden delay/coroutine/update tokens. Fixes: none. Estimate: 1100 us.
- [x] Loop 4 - Cross-domain decoupling review | Result: concrete `WorldChunkResidencyManager` bridge dependency was removed; `IStreamingBackpressureService.IsChunkResident` now carries residency read model. Alternative rejected: keeping type-check in Core bridge. Estimate: saves compile coupling, same runtime cost.
- [x] Loop 5 - Verification hygiene review | Result: `git diff --check` passes for touched files except line-ending warnings; MCP console is unreachable; owned timed-out MSBuild root was terminated. Alternative rejected: launching a second Unity editor over active project. Estimate: 900 us.
- [x] Loop 6 - Dev skip interruption recheck | Result: Stage 1 dilated silence no longer traps dev skip for the full 3 seconds; bridge uses a dev-only linked cancellation token and interruptible dilated wait, while release builds keep `AwaitableExtension.DelayDilated`. Alternative rejected: accepting delayed skip during cinematic silence. Estimate: 5-10 us per dev-only wait frame, 0 us release overhead.
- [x] Loop 7 - Non-reload lifecycle reset review | Result: `PrologueSequenceRegistryBridge` now clears transient hydration readiness, signal cursors, skip latch, and stale service reference on `OnEnable` before re-registration. Alternative rejected: trusting `OnDisable`/domain reload to clear second-run state. Estimate: 0 us hot path; 8-20 us avoided stale gate debugging cost on re-entry.
- [x] Loop 8 - Signal self-feedback review | Result: `TryConsumePrologueComplete` now skips bridge-authored `PRLG` ocean-handoff packets so the manual override gate cannot consume its own output during same-frame reuse. Alternative rejected: phase-only filtering, because existing cockpit/orbital producers use `PhaseOceanHandoff`. Estimate: 1 uint compare per consumed complete signal.
- [x] Loop 9 - Director repeated-run state review | Result: `AwaitableDropSequenceDirector` now clears cached atmospheric, complete, orbital, and telemetry suppression state at run entry. Alternative rejected: assuming a service instance runs only once, because registry services can be invoked manually after dev skip/cancel. Estimate: 0 us hot path; prevents stale Mach/sequence carryover.
- [x] Loop 10 - Runtime repeated-run state review | Result: `IPrologueSequenceRuntime.PrepareSequenceRun()` now resets bridge observation state at every sequence start, not only `OnEnable`. Alternative rejected: relying on scene/component lifecycle for service-level reruns. Estimate: one interface call at run start, 0 us wait-loop cost.
- [x] Loop 11 - Manual gate producer review | Result: `TryConsumePrologueComplete` now rejects autonomous `ORBI` whiteout completion as well as self-authored `PRLG`, keeping Stage 3 gated by cockpit/manual producers. Alternative rejected: accepting any complete signal on the shared lane. Estimate: one extra uint compare per complete signal.
- [x] Loop 12 - Manual gate source whitelist review | Result: audited all `PrologueCompleteSignal` producers and narrowed Stage 3 acceptance to `MOVR` only. Alternative rejected: blacklist of known non-manual producers. Estimate: one uint inequality per complete signal, lane cap 8.
- [x] Loop 13 - Run preparation fault guard review | Result: moved `PrepareSequenceRun()` under the director `try/finally` envelope after local run-state reset. Alternative rejected: trusting all future runtime adapters to be infallible. Estimate: 0 us hot-path cost.

## Verification

- [x] Compile verification pass after Tasks 1-5. BLOCKED BY UNRELATED DEPENDENCY: Unity batch compile copied Core.Contracts and Narrative.Prologue assemblies, then failed in `ShallowsBioForgeBatchBaker.cs`, `DiegeticTooltipSystem.cs`, and `GlobalDataVault.cs`.
- [x] Compile verification pass after Tasks 6-10. BLOCKED BY UNRELATED DEPENDENCY: no prologue errors in Unity compile log; global errors remain outside this domain. Second batch attempt stalled in Unity after asmdef refresh and was terminated to avoid leaving an owned batch process running.
- [x] Compile verification pass after Tasks 11-15. BLOCKED BY UNRELATED DEPENDENCY / ACTIVE EDITOR: MCP console still fails at `127.0.0.1:8088`; active Unity editor already owns the project; narrow `dotnet build Hecton8.Core.csproj --no-restore -v:minimal` timed out and its owned root process was terminated. Static scans found no prologue forbidden wait patterns.
- [x] Response-file compile pass after dev-skip upgrade. PARTIAL PASS: Unity Roslyn `Hecton8.Core.Contracts.rsp` and `Hecton8.Narrative.Prologue.rsp` compile clean after sequencing. `Hecton8.Core.rsp` fails in unrelated `GroundPenetratingRadarRuntime.cs(309,17)` on missing `GroundRadarRaymarchJob.GprOreTypes`; no prologue errors emitted before that wall.
- [x] Response-file compile pass after lifecycle reset. PASS ON PRIMARY BEE SET: `1300b0aEDbg` `Hecton8.Core.Contracts.rsp`, `Hecton8.Narrative.Prologue.rsp`, `Hecton8.Core.rsp`, and `Hecton8.Prologue.Space.rsp` compile with exit 0. SECONDARY BEE SET BLOCKED/STALE: `1900b0aEDbg` lacks `PrologueSequenceContracts.cs` in Core.Contracts and fails on unrelated/missing audio virtualization, fauna cognition, WFC/outpost, ore ID, and fluid impulse references.
- [x] Response-file compile pass after self-feedback filter. PASS: primary `1300b0aEDbg` `Hecton8.Core.rsp` compiles with exit 0 after the bridge filter patch.
- [x] Response-file compile pass after director repeated-run reset. PASS: primary `1300b0aEDbg` `Hecton8.Narrative.Prologue.rsp` compiles with exit 0 after the director patch.
- [x] Response-file compile pass after runtime run-reset contract. PASS: primary `1300b0aEDbg` `Hecton8.Core.Contracts.rsp`, `Hecton8.Narrative.Prologue.rsp`, and `Hecton8.Core.rsp` compile with exit 0.
- [x] Response-file compile pass after manual gate producer filter. PASS: primary `1300b0aEDbg` `Hecton8.Core.rsp` compiles with exit 0.
- [x] Response-file compile pass after manual source whitelist. PASS: primary `1300b0aEDbg` `Hecton8.Core.rsp` compiles with exit 0.
- [x] Response-file compile pass after run preparation guard. PASS: primary `1300b0aEDbg` `Hecton8.Narrative.Prologue.rsp` compiles with exit 0.
- [x] Prompt re-extraction drift noted. BLOCKED BY BATCH FILE ROTATION: current `Docs/Tasks/CURRENT_BATCH.md` no longer contains `PROLOGUE_SEQUENCE_DIRECTOR`; durable status/rationale remain the active local memory for the already-extracted assignment.
- [x] Five strict iterative self-review loops.
- [x] Polish mandate read and executed only after all tasks were checked or blocked. DOD: OMEGA audit removed telemetry `math.sqrt`, removed concrete world residency dependency, rescanned forbidden wait/string/iteration patterns, and documented final scoped diff in rationale. Status remains PENDING VERIFICATION due unrelated compile wall.
