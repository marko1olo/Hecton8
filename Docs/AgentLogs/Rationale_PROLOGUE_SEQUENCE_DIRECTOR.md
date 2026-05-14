# Rationale_PROLOGUE_SEQUENCE_DIRECTOR

Status: PENDING VERIFICATION.

## Decision 0 - Agent Scope

Problem: Prologue pacing touches narrative, input, audio, haptics, streaming, velocity, camera juice, and fluid systems while 20+ agents may be changing those domains.
Solution: Keep ownership in a narrative/prologue service and communicate by contracts/signals/registry interfaces only. Inspect existing contracts before introducing any type.
Rejected Alternatives: Direct references to concrete audio, input, streaming, or fluid classes would compile faster initially but create cross-domain coupling and race other agents.
Scalability potential: Low tier uses deterministic flow with cheap waits and proxy surface; Middle/High/Ultra can consume the same signals for richer VWS, haptics, camera impulse, and ocean visuals.
Hardware Impact: Estimated low-end gain vs concrete polling/wiring is 10-35 us per sequence wait iteration and lower compile churn risk on i3/MX350.

## Decision 1 - Mandate Selection

Problem: Awaitable drop sequence is not a single subsystem; it is orchestration across registry, streaming, telemetry, input, haptics, audio, and diegetic UI.
Solution: Use eight mandates: GlobalRegistry DI, Bootstrap Awaitable safety, Zero-GC, Crash Telemetry, World Streaming Residency, DSP SPSC Audio, Device/Haptics, Diegetic UI.
Rejected Alternatives: Reading only narrative docs would miss hot-path allocation, chunk readiness, and haptic/audio signal constraints.
Scalability potential: Low tier skips high-res chunk waits; Ultra can continue waiting for full visual hydration without changing service API.
Hardware Impact: Prevents blind waits and string/event spam; estimated 0.02-0.08 ms avoided during transition frames on i3/MX350.

## Decision 2 - Contract Boundary

Problem: The prologue sequence must orchestrate haptics, VWS, UI, streaming, camera juice, and orbital velocity without depending on those concrete domains.
Solution: Add `IPrologueSequenceService` and `IPrologueSequenceRuntime` to `Hecton8.Core.Contracts`, place `AwaitableDropSequenceDirector` in `Hecton8.Narrative.Prologue`, and isolate concrete signal/registry translation in `PrologueSequenceRegistryBridge` under Core.
Rejected Alternatives: Putting the state machine in `Hecton8.Core` or referencing `WorldChunkResidencyManager` directly from the narrative assembly would satisfy compile faster but break asmdef isolation and invite parallel-agent coupling.
Scalability potential: Low/MX350 follows the same state machine with proxy hydration; Mid waits for normal residency; High/Ultra can preserve full-resolution ocean handoff and richer VWS/camera/haptic responders.
Hardware Impact: Contract-only loops are one interface call plus one Awaitable per frame; estimated 8-20 us lower risk than `FindObjectOfType`/scene polling on i3/MX350.

## Decision 3 - Universe Velocity Source

Problem: The prompt names DataVault for `UniverseVelocity`, but the existing `IDataVault` API exposes generic buffer ownership and has no orbital velocity buffer ID/read model.
Solution: Read the existing authoritative `IOrbitalDirector.TryGetSnapshot()` through `GlobalRegistry` inside the Core bridge. The narrative assembly receives only a contract snapshot and never touches `IOrbitalDirector`.
Rejected Alternatives: Inventing a new DataVault buffer would create producer/consumer ownership the orbital director does not currently maintain; reading private orbital fields would be a direct dependency.
Scalability potential: Low tier can use the same cheap snapshot and high tiers can upgrade the orbital director internals without changing prologue pacing.
Hardware Impact: Reuses already-cached snapshot values; avoids persistent DataVault allocation and one extra buffer lookup per wait frame, roughly 3-8 us on i3/MX350.

## Decision 4 - Compile Pass 1 Blocker

Problem: Unity batch compile reached and copied `Hecton8.Core.Contracts.dll` and `Hecton8.Narrative.Prologue.dll`, but the project still fails due unrelated files: `ShallowsBioForgeBatchBaker.cs`, `DiegeticTooltipSystem.cs`, and `GlobalDataVault.cs`.
Solution: Treat compile status as PENDING VERIFICATION for this agent and continue task-local checks; do not edit unrelated procedural-gen, UI tooltip, or data-vault work owned by other lanes.
Rejected Alternatives: Fixing unrelated compile failures would violate domain boundaries and risk overwriting active parallel-agent work.
Scalability potential: Local prologue work remains decoupled; once dependency lanes compile, Core bridge registration and signals can be verified without narrative assembly changes.
Hardware Impact: No runtime impact; prevents churn in hot systems outside this task.

## Decision 5 - Reentry Burn and Ocean Handoff

Problem: Stage 2-10 need warning, haptics, impact sync, hydration, camera shake, fluid buoyancy, and velocity freeze without adding concrete dependencies to the narrative assembly.
Solution: The contract state machine requests `PublishHullTempCriticalWarning`, `PublishHeavyRumble`, `PublishManualReleasePrompt`, `IsOceanSurfaceReady`, `ZeroUniverseVelocity`, `PublishMassiveImpact`, and `PublishOceanHandoff`; Core bridge translates those into `VocalWarningSignal`, `HapticRequest`, `DiegeticHudSignal`, `IStreamingBackpressureService.IsChunkResident`/residency signals, `IOrbitalDirector.ForceZeroUniverseVelocity`, `CameraJuiceSignals`, and `PrologueCompleteSignal`.
Rejected Alternatives: Driving water/fx objects directly from the prologue service would make the isolated asmdef depend on audio/world/fluid/VFX. Blind timing waits were rejected because residency can complete early or late depending on IO.
Scalability potential: Low/MX350 resumes on impostor readiness; Mid/High waits for resident chunk; Ultra still gets the same impact signal for heavier water/bubble presentation.
Hardware Impact: Residency check is one cached service read and span scan; expected 3-12 us per frame versus a blind black screen or scene search. Camera/fluid activation is signal-only at splashdown.

## Decision 6 - Safety, Skip, and Black Box

Problem: Dev skip and crash reconstruction must not add hot-loop garbage or rely on chat/log strings.
Solution: Add `GlobalRegistry.IsDevelopmentBuild`, dev skip via input cancel event or Dash+Primary+Secondary chord, fixed `NativeArray<PrologueSequenceTelemetryEntry>[300]` ring, hash-only `GlobalTelemetryBus.PublishPrologueStage`, and binary dump to `Docs/AgentLogs/Dump_PROLOGUE_SEQUENCE_DIRECTOR.bin` on exception/non-finite orbital snapshot.
Rejected Alternatives: UI button lookup, managed `List<>` telemetry, string stage names, or per-frame event subscription were rejected as GC or coupling hazards.
Scalability potential: Low tier uses skip/proxy hydration to avoid black screens; High/Ultra keeps the same telemetry and can spend saved time on water response.
Hardware Impact: Wait loops stay span/interface based; no allocations in the polled path. Black-box writes occur only on fault and do not tax i3/MX350 frame time.

## Decision 7 - Residency Contract Tightening

Problem: The first pass used the streaming registry interface but then type-checked `WorldChunkResidencyManager` for `IsResident`, which preserved function but weakened cross-domain decoupling.
Solution: Expand `IStreamingBackpressureService` with `IsChunkResident(long chunkId)` and implement it as a direct wrapper in `WorldChunkResidencyManager`; the prologue bridge now uses only the registry interface plus typed residency signals.
Rejected Alternatives: Keep the concrete type-check, use reflection, or blind-wait for an ocean scene marker. Concrete dependency breaks parallel-agent boundaries; reflection violates hot-path/AOT discipline; blind waits produce black screens on weak IO.
Scalability potential: Low uses proxy/impostor readiness; Middle/High waits for resident chunk; Ultra can retain full ocean hydration with the same interface.
Hardware Impact: Same hash-table residency read as before, one interface dispatch added; expected cost remains 3-12 us per wait frame on i3/MX350 with lower compile coupling risk.

## Decision 8 - Verification Wall

Problem: After tasks 11-15, the project cannot provide a clean compile signal because MCP is unreachable and the active Unity editor already owns the project while existing unrelated compile errors are present.
Solution: Do not start a competing Unity batch process; use static scans and prior Unity log evidence showing the prologue assemblies copied, then mark compile verification as BLOCKED BY UNRELATED DEPENDENCY / ACTIVE EDITOR. Terminated only the owned timed-out `dotnet build Hecton8.Core.csproj --no-restore -v:minimal` root.
Rejected Alternatives: Killing the user's active Unity editor, editing unrelated `ShallowsBioForgeBatchBaker`, `DiegeticTooltipSystem`, or `GlobalDataVault`, or declaring compile green from static scans.
Scalability potential: No runtime effect; preserves parallel lanes and prevents active-editor contention.
Hardware Impact: No frame-time impact. Verification remains PENDING until unrelated compile blockers and MCP transport are restored.

## OMEGA POLISH CHANGES

Problem: The anti-bloat audit found one unnecessary telemetry `math.sqrt()` in `RecordStage()` and one concrete world residency dependency in the bridge.
Solution: Cache the last orbital snapshot when the runtime already reads it, derive black-box telemetry speed from cached data, and use float `math.rsqrt()` for approximate telemetry speed. Move chunk readiness behind `IStreamingBackpressureService.IsChunkResident(long chunkId)` so the bridge stays interface-only.
Rejected Alternatives: Re-query orbital runtime inside every telemetry write, keep exact double `sqrt()` for non-gameplay telemetry, or keep a `WorldChunkResidencyManager` type-check in Core bridge.
Scalability potential: Low/MX350 pays approximate float telemetry only; High/Ultra keep the same deterministic choreography and spend saved cycles on visual responders to existing VWS/haptic/camera/ocean signals.
Hardware Impact: Removes one duplicate registry/interface snapshot query and one double sqrt per `RecordStage()` with an orbital sample. Estimated low-end saving: 1-4 us on hot wait frames, zero visual loss because this value is forensic telemetry only.
Cinematic Cheats used: hash-only diegetic prompt instead of text UI allocation; hull temperature warning reuses existing hull-breach VWS asset; low-tier ocean handoff accepts proxy/impostor surface instead of full-res hydration; telemetry speed uses approximate reciprocal-square-root; water splashdown is signal-driven camera/fluid fake instead of honest impact physics.
Final Git Diff: Modified `GlobalRegistry.cs`, `GlobalRegistryContracts.cs`, `GlobalSignals.cs`, `GlobalTelemetryBus.cs`, `OrbitalRelativityDirector.cs`, `WorldChunkResidencyManager.cs`; added `PrologueSequenceContracts.cs`, `PrologueSequenceRegistryBridge.cs`, `AwaitableDropSequenceDirector.cs`, `Hecton8.Narrative.Prologue.asmdef`, and corresponding `.meta` files. Scoped diff stat for tracked files: 204 insertions, 7 deletions across 6 tracked files; untracked new prologue files are listed in git status until staged. Full working-tree diff remains available from `git diff -- Assets/_Project/Scripts/...` plus untracked file contents.
Verification: Polish static scans found no `Task.Delay`, coroutine, `StartCoroutine`, gameplay `Update`, `foreach`, `string.Format`, `.ToString()`, `math.sqrt`, `math.normalize`, or concrete `WorldChunkResidencyManager` reference in the prologue path. `GetComponents` remains a documented cold setup allocation; `Directory`/`BinaryWriter`/`File` remain fault-only black-box dump IO. `dotnet build Hecton8.Core.csproj` was attempted and blocked/timed out under existing project contention; status remains PENDING VERIFICATION.

## Decision 9 - Dev Skip Must Interrupt Dilated Silence

Problem: The first implementation polled dev skip before and after the 3-second Stage 1 `DelayDilatedAsync`, so an input cancel during orbital silence could wait until the delay completed instead of cancelling the Awaitable immediately.
Solution: Add shared `PrologueCancelReasons`, give the Core bridge a dev-only linked `CancellationTokenSource`, and route dev cancel through `CancelSequence(DevSkip)` plus token cancellation. In development builds only, `DelayDilatedAsync` uses the same H8 time snapshot pattern but polls skip each frame; release builds still call `AwaitableExtension.DelayDilated` directly.
Rejected Alternatives: Leave skip delayed, replace all release delay timing with a custom loop, or add UI-button coupling. Delayed skip violates the prompt; replacing release timing increases risk; UI coupling breaks domain isolation.
Scalability potential: Low/MX350 dev testing gets immediate shallow-water resume; production runtime keeps the cheaper established delay path. High/Ultra presentation remains signal-driven and can spend saved cycles on responders.
Hardware Impact: Release overhead is 0 us versus previous path. Development-only wait polling costs about 5-10 us per frame during the 3-second silence and allocates only one cold linked CTS per auto-run sequence.

## Decision 10 - Response-File Compile Evidence

Problem: Generated `.csproj` metadata is stale for new asmdef files, and MCP remains unreachable, so normal Unity-console verification is not available.
Solution: Use Unity Bee response files directly with Unity's bundled Roslyn compiler. Compile `Hecton8.Core.Contracts.rsp` first, then `Hecton8.Narrative.Prologue.rsp`, then probe `Hecton8.Core.rsp`.
Rejected Alternatives: Trust stale `.csproj`, run another active-editor Unity batch, or declare success from static scan only.
Scalability potential: No runtime impact; this improves evidence quality under active editor contention.
Hardware Impact: No frame-time impact. Result: Contracts and Narrative.Prologue compile clean; Core compile is blocked by unrelated `GroundPenetratingRadarRuntime.cs(309,17)` missing `GroundRadarRaymarchJob.GprOreTypes`.
