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
Solution: Add shared `PrologueCancelReasons`, give the Core bridge an auto-run linked `CancellationTokenSource`, and route dev cancel through `CancelSequence(DevSkip)` plus token cancellation. In development builds only, `DelayDilatedAsync` uses the same H8 time snapshot pattern but polls skip each frame; release builds still call `AwaitableExtension.DelayDilated` directly.
Rejected Alternatives: Leave skip delayed, replace all release delay timing with a custom loop, or add UI-button coupling. Delayed skip violates the prompt; replacing release timing increases risk; UI coupling breaks domain isolation.
Scalability potential: Low/MX350 dev testing gets immediate shallow-water resume; production runtime keeps the cheaper established delay path. High/Ultra presentation remains signal-driven and can spend saved cycles on responders.
Hardware Impact: Release wait-loop overhead is 0 us versus previous path. Auto-run pays one cold linked CTS allocation; development-only wait polling costs about 5-10 us per frame during the 3-second silence.

## Decision 10 - Response-File Compile Evidence

Problem: Generated `.csproj` metadata is stale for new asmdef files, and MCP remains unreachable, so normal Unity-console verification is not available.
Solution: Use Unity Bee response files directly with Unity's bundled Roslyn compiler. Compile `Hecton8.Core.Contracts.rsp` first, then `Hecton8.Narrative.Prologue.rsp`, then probe `Hecton8.Core.rsp`.
Rejected Alternatives: Trust stale `.csproj`, run another active-editor Unity batch, or declare success from static scan only.
Scalability potential: No runtime impact; this improves evidence quality under active editor contention.
Hardware Impact: No frame-time impact. Result: Contracts and Narrative.Prologue compile clean; Core compile is blocked by unrelated `GroundPenetratingRadarRuntime.cs(309,17)` missing `GroundRadarRaymarchJob.GprOreTypes`.

## Decision 11 - Non-Reload Lifecycle Reset

Problem: `PrologueSequenceRegistryBridge` cached transient readiness flags, per-frame signal cursors, skip state, and a resolved service reference across enable cycles. In domain-reload-disabled or scene-transition reuse, a second prologue run could inherit "ocean ready" from a previous forced hydration or keep a stale service reference after inspector wiring changed.
Solution: Add `ResetTransientSequenceState()` at the start of `OnEnable()` and clear `_service` before `ResolveService()`. The reset is scalar-only and runs before registration, input binding, and auto-run start.
Rejected Alternatives: Trust `OnDisable` to cover every entry path, clear state lazily inside `IsOceanSurfaceReady`, or keep stale service as a fallback. `OnDisable` is not a deterministic reset owner for non-reload transitions; lazy clearing touches the wait path; stale service fallback can register a dead component.
Scalability potential: Low/MX350 gets deterministic second-run proxy behavior instead of accidental instant hydration; Middle/High/Ultra keep high-res hydration gating and richer responders without contaminated readiness state.
Hardware Impact: Hot path cost is 0 us. `OnEnable` scalar reset cost is below measurement relevance; estimated 8-20 us avoided in stale-state branch checks/debugging and prevents unbounded correctness drift.

## Decision 12 - Primary vs Secondary Bee Verification

Problem: There are multiple Bee response-file sets. The primary `1300b0aEDbg` set sees the new prologue contracts and compiles touched assemblies; the secondary `1900b0aEDbg` set is stale or configured without several dependencies.
Solution: Treat `1300b0aEDbg` as the valid narrow post-edit evidence set for this work and record `1900b0aEDbg` as blocked/stale. `1300b0aEDbg` compiles `Hecton8.Core.Contracts`, `Hecton8.Narrative.Prologue`, `Hecton8.Core`, and `Hecton8.Prologue.Space` with exit 0. `1900b0aEDbg` lacks `PrologueSequenceContracts.cs` in Core.Contracts and fails on unrelated audio virtualization, fauna cognition, WFC/outpost, ore ID, and fluid impulse references.
Rejected Alternatives: Declare full Unity verification from one response-file set, or edit unrelated assemblies to make stale verification green. Full verification still requires Unity import/console/Play Mode/GCMonitor; unrelated fixes violate domain boundaries.
Scalability potential: No runtime effect; preserves reliable evidence while avoiding cross-lane churn.
Hardware Impact: No frame-time impact. Status remains PENDING VERIFICATION because MCP/Unity console and runtime profiling are unavailable.

## Decision 13 - Complete Signal Self-Feedback Filter

Problem: The bridge both consumes `PrologueCompleteSignal` for the manual override gate and emits `PrologueCompleteSignal` for ocean handoff. In a same-frame re-enable or repeated run, a bridge-authored `PRLG` handoff packet could be read as if it came from the cockpit/orbital producers.
Solution: `TryConsumePrologueComplete` now skips packets whose `SourceHash` equals the bridge `SourceHash`. Existing cockpit lever (`MOVR`) and orbital director (`ORBI`) packets remain accepted.
Rejected Alternatives: Filter by `PhaseWhiteout` or reject all `PhaseOceanHandoff` packets. Existing producers currently mark the manual lever and orbital completion as `PhaseOceanHandoff`, so phase-only filtering would break the handoff gate.
Scalability potential: Low/MX350 avoids accidental instant progress after dev skip/re-enable; High/Ultra retain the same richer downstream water/audio/VFX response because only self-authored feedback is discarded.
Hardware Impact: One uint compare per consumed complete signal; capacity is 8, so worst-case cost is below 1 us on i3/MX350. No allocation and no hot-frame polling change outside the existing wait loop.

## Decision 14 - Director Repeated-Run State Reset

Problem: `AwaitableDropSequenceDirector` reset cancellation flags at run entry but left `_lastAtmosphericReentry`, `_lastComplete`, `_lastOrbital`, and `_hasPublishedTelemetry` from the previous run. If a service instance is invoked again after cancel/dev skip, stale atmospheric velocity could pass the Mach gate when no new snapshot exists, and telemetry could suppress the first repeated-run stage packet.
Solution: Clear cached snapshots and telemetry publication gate at the start of `RunPrologueSequenceAsync` before awaiting atmospheric reentry.
Rejected Alternatives: Treat prologue as one-shot only, or clear state lazily inside each wait loop. One-shot assumptions do not hold for dev skip/manual testing; lazy clears scatter correctness state across hot wait loops.
Scalability potential: Low/MX350 repeated test loops keep deterministic proxy/high-res decisions; High/Ultra presentation can rerun without stale sequence hashes contaminating downstream signal timing.
Hardware Impact: Hot path cost is 0 us. Run-entry reset is scalar assignment only; it removes stale Mach/sequence carryover without adding wait-loop branches.

## Decision 15 - Runtime Run-Start Reset Hook

Problem: The director now clears its cached snapshots on every run, but the bridge still only cleared hydration readiness and signal cursors on `OnEnable`. A same enabled service instance invoked after dev skip could retain forced proxy/high-res readiness and skip the intended chunk hydration gate.
Solution: Expand `IPrologueSequenceRuntime` with `PrepareSequenceRun()` and call it once at `RunPrologueSequenceAsync` entry. The bridge implementation reuses the same scalar `ResetTransientSequenceState()` used by `OnEnable`.
Rejected Alternatives: Force consumers to disable/enable the bridge before rerunning, or add checks inside `IsOceanSurfaceReady`. Lifecycle choreography is fragile for dev tooling; hot-path hydration checks should not carry extra stale-state cleanup branches.
Scalability potential: Low/MX350 repeated runs recalculate proxy readiness from current streaming state; Middle/High/Ultra repeat runs must revalidate high-res ocean residency before splashdown.
Hardware Impact: One interface dispatch at sequence start; 0 us added to per-frame wait loops. Removes stale forced-hydration carryover without affecting normal one-shot cost.

## Decision 16 - Manual Gate Producer Filtering

Problem: `OrbitalRelativityDirector` emits `PrologueCompleteSignal` from source `ORBI` automatically at cloud whiteout. Stage 3 is the manual cockpit override stage; accepting any complete signal on the shared lane allows autonomous orbital whiteout to bypass the manual release.
Solution: `TryConsumePrologueComplete` rejects both bridge-authored `PRLG` and orbital `ORBI` packets. Cockpit/manual producers such as `MOVR` remain valid complete-signal sources for the manual gate.
Rejected Alternatives: Accept all complete signals, or filter only by phase. Accept-all breaks player agency; phase-only filtering is invalid because both cockpit and orbital producers currently use `PhaseOceanHandoff`.
Scalability potential: Low/MX350 still gets non-VR lever fallback via the cockpit producer; High/Ultra keep manual agency before spending cycles on richer water/audio/VFX handoff.
Hardware Impact: One additional uint compare per `PrologueCompleteSignal` in a lane capped at 8 entries; below 1 us worst-case on i3/MX350. No allocation.

## Decision 17 - Manual Gate Source Whitelist

Problem: The previous Stage 3 filter rejected known autonomous producers, but any unknown future `PrologueCompleteSignal` source could still satisfy the manual override gate by sharing the global lane.
Solution: Replace the blacklist with a `MOVR` whitelist after auditing all current producers. `OpenXRManualOverrideLever` is the only accepted release source; bridge `PRLG`, orbital `ORBI`, and unknown producers are ignored by the gate.
Rejected Alternatives: Keep the PRLG/ORBI blacklist, or introduce a new signal type for manual release. A blacklist fails closed only for known producers; a new lane would force additional consumers during an active parallel batch.
Scalability potential: Low/MX350 keeps the existing non-VR lever fallback through `MOVR`; Middle/High/Ultra preserve manual agency before expensive water/audio/VFX overkill responders start.
Hardware Impact: One uint inequality per complete signal in a lane capped at 8 entries; same worst-case sub-1 us cost as the blacklist, with stricter correctness.

## Decision 18 - Run Preparation Fault Guard

Problem: `RunPrologueSequenceAsync` marked the service as running and then called `_runtime.PrepareSequenceRun()` before entering the guarded `try/finally`. The current bridge reset is scalar-only, but the public runtime contract should not be able to strand `_running` or input-lock cleanup if a future implementation faults.
Solution: Reset director-local run state first, then execute `PrepareSequenceRun()` inside the `try/finally` sequence envelope.
Rejected Alternatives: Trust the current bridge implementation forever, or wrap only `PrepareSequenceRun()` in a separate catch. Trusting a single adapter is brittle; a second catch duplicates the fault/black-box path.
Scalability potential: Low/MX350 and Ultra behavior is unchanged in the hot path. The change buys deterministic cleanup for future runtime adapters without adding wait-loop branches.
Hardware Impact: 0 us hot-path cost. No added allocation. One call moved under existing exception handling; normal sequence cost is unchanged.

## Decision 19 - Hydration Cancellation and Dynamic LOD

Problem: The ocean hydration wait checked `IsOceanSurfaceReady()` in the while condition, so a same-frame cancellation with a ready surface could still proceed into water transition. It also sampled `IsLowTier` only once, forcing high-res hydration if the device downshifted during the wait.
Solution: Convert hydration wait to an explicit loop: check cancellation/dev skip first, re-sample `IsLowTier` every iteration, then test surface readiness and record the current hydration mode.
Rejected Alternatives: Keep the while-condition readiness test, or only add a cancellation check after readiness. Both preserve a cancellation race; one-shot LOD sampling ignores thermal/memory changes during a streamed transition.
Scalability potential: Low/MX350 can downshift into proxy hydration mid-wait; Middle/High/Ultra still require high-res readiness until quality policy says otherwise.
Hardware Impact: Adds one `IsLowTier` property read on hydration wait frames, estimated 1-3 us. Prevents unbounded high-res wait on constrained devices and keeps water transition cancellable.

## Decision 20 - Auto-Run Disable Cancellation Token

Problem: Release auto-run passed `destroyCancellationToken` directly. `OnDisable()` requested sequence cancellation, but disable does not cancel `destroyCancellationToken`, so a release `DelayDilatedAsync` could keep running until its timer completed.
Solution: Always create one linked `CancellationTokenSource` for bridge auto-run and cancel it from `RequestRunCancellation()`. Release builds still use `AwaitableExtension.DelayDilated`; the token now terminates it on disable, while dev skip keeps the same immediate interruption path.
Rejected Alternatives: Keep the dev-only CTS, or replace release dilated delay with a custom polling loop. Dev-only CTS leaves a release lifecycle race; a custom release loop adds per-frame code where the existing Awaitable delay already accepts a cancellation token.
Scalability potential: Low/MX350 avoids disabled-scene wait leakage; Middle/High/Ultra keep identical visual pacing and spend no extra wait-loop cycles.
Hardware Impact: One cold managed CTS allocation per auto-run sequence. Hot path stays unchanged; release wait-loop overhead remains inside the existing Awaitable delay implementation.

## Decision 21 - Auto-Run Patch Verification Wall

Problem: Core response-file compile first exposed a real prologue bridge regression (`PrepareSequenceRun()` missing). After reapplying the bridge reset/manual-gate methods, the prologue error disappeared, but the same Core response file now fails in unrelated save/hardware-profile/voxel symbols.
Solution: Fix the prologue-owned missing interface implementation, confirm Core.Contracts and Narrative.Prologue compile clean, then stop at the unrelated wall: `SaveMasterHashV10.cs`, `VoxelDeltaProcessor.cs`, and earlier `BinaryLayoutManifest.cs`/hardware profile references sit outside this domain.
Rejected Alternatives: Edit binary save layout, voxel delta, or hardware profile systems to force a green Core compile. Those are outside the assigned presentation/narrative trigger domain and are likely owned by parallel agents.
Scalability potential: No runtime change beyond Decision 20; the response-file evidence is scoped to removal of prologue errors.
Hardware Impact: No frame-time impact. Verification remains PENDING for full Core until unrelated save/hardware profile dependencies are restored.

## Decision 22 - Impact Sync Pre-Wait Cancellation

Problem: `RunImpactSyncAsync` recorded the impact-sync stage and always awaited one frame before checking explicit cancellation/dev skip. The stage is intentionally one frame, but a cancellation already requested before entry should not spend that frame.
Solution: Check cancellation and dev skip immediately after recording the impact-sync stage, then keep the required one-frame await and post-wait check.
Rejected Alternatives: Remove the post-wait check, or keep only the post-wait check. Removing the post check misses same-frame cancellation during the await; post-only delays known cancellation by a frame.
Scalability potential: Low/MX350 exits one frame earlier under skip/cancel; High/Ultra preserve the one-frame synchronization when not cancelled.
Hardware Impact: Two branch checks before a single Awaitable frame wait; estimated below 1 us and no allocation.

## Decision 23 - Cleanup Fault-Path Hardening

Problem: The director released input lock directly in `finally` and only then cleared `_running`. If a runtime adapter threw during unlock, the service could remain marked running and the cleanup exception could mask the real sequence outcome.
Solution: Clear `_running` before unlock and route final unlock through `ReleaseInputLockNoThrow()`. Runtime dump and telemetry fallback calls now use no-throw wrappers so a fault path cannot recursively hide the black-box evidence.
Rejected Alternatives: Leave direct `finally` unlock, or call telemetry/runtime dump directly from catch/finally. Direct calls are cheaper in text only; the try boundary is outside wait loops and prevents cleanup from becoming a second crash.
Scalability potential: Low/MX350 avoids stranded input locks during disable/dev skip faults; Middle/High/Ultra keep the same presentation pacing and richer downstream responders because normal state-machine timing is unchanged.
Hardware Impact: 0 us per wait frame. Normal sequence cleanup pays one method call and no thrown exception; fault-only paths dump black-box data instead of burning additional recovery time.

## Decision 24 - Dev-Skip Cancellation Guard

Problem: Dev skip can be entered from token cancellation, explicit cancel state, or skip polling. The handoff calls runtime hydration, velocity, impact, and ocean signals; if one throws inside cancellation handling, the sibling catch block will not catch it and the crash can escape without the intended sequence fault record.
Solution: Route all dev-skip entry points through `TryExecuteDevelopmentSkipHandoff()`, latch the handoff once, dump black-box data on runtime failure, and use guarded runtime black-box dump for non-finite orbital detection.
Rejected Alternatives: Let cancellation handlers call the handoff directly, or wrap each runtime handoff call independently. Direct calls leave an unlogged secondary failure; per-call wrapping adds noise and still needs one shared latch.
Scalability potential: Low/MX350 dev testing reaches forced shallow-water handoff or produces a clear dump; High/Ultra production pacing is unchanged because the guarded path is dev-skip/fault only.
Hardware Impact: 0 us release hot path. Dev-only skip adds one guarded helper call and a try boundary when skip/cancel is already active; no per-frame wait-loop allocation. Verification: Core.Contracts and Narrative.Prologue response files compile with exit 0; Core response-file probe is blocked by unrelated `SaveMasterHashV10.cs(237,26)` missing `xxHash3`.

## Decision 25 - Duplicate Input-Unlock Signal Removal

Problem: Normal water transition published `PublishInputLock(None)` and then the sequence `finally` published the same unlock again through the guarded cleanup path. The duplicate signal spent a lane slot and left one unguarded cleanup call in the success path.
Solution: Remove the normal-path unlock from `RunWaterTransition()` and rely on the guarded `finally` release. Dev-skip keeps immediate guarded unlock because it exits through cancellation before normal completion.
Rejected Alternatives: Keep duplicate unlock for symmetry, or remove the final cleanup release. Duplicate unlock is wasteful; removing final cleanup would make fault/cancel paths less deterministic.
Scalability potential: Low/MX350 saves one unnecessary signal publish on completed prologue; High/Ultra keep identical visible pacing while downstream water/audio/VFX responders receive less duplicate control traffic.
Hardware Impact: Saves one `SystemPauseSignal` publish per completed run, estimated 3-8 us and one signal-lane entry. Verification this pass is static only by user request; no dotnet rebuild or response-file compile was run.

## Decision 26 - Hydration Fallback Specificity

Problem: With `oceanSurfaceChunkId == 0` and `allowAnyHydratedChunkFallback == true`, the bridge accepted any `SectorResidencyHydratedSignal` on high tier. A random ecosystem or stress-test sector could release the splashdown gate before the ocean/shallow-water surface was ready.
Solution: Keep exact configured chunk matching, keep forced shallow-water hash matching, and restrict arbitrary fallback to low-tier proxy mode with `FlagProxyFallback` present.
Rejected Alternatives: Disable fallback completely, or keep accepting any hydrated sector. Disabling fallback risks black-screen stalls in unconfigured low-tier/dev scenes; accepting any sector breaks high-tier visual integrity.
Scalability potential: Low/MX350 can still use the cheap proxy fake; Middle/High/Ultra require the configured ocean chunk or deliberate shallow-water signal before spending cycles on water/audio/VFX overkill.
Hardware Impact: Adds one proxy-flag branch per residency signal in a lane capped at 64. Estimated cost below 1 us on i3/MX350; prevents false readiness and wasted splashdown work. Verification this pass is static only by user request; no dotnet rebuild or response-file compile was run.

## Decision 27 - Registry Ownership and Cancellation Guard

Problem: `PrologueSequenceRegistryBridge` configured and could auto-run its local service even if `GlobalRegistry` rejected registration because another prologue runtime already owned the slot. Cancellation also called the service before CTS cancellation, so a future throwing service adapter could block token cancellation.
Solution: After registration, the bridge now verifies `GlobalRegistry.PrologueSequence` still points to its service before auto-run. Cancellation catches service failures, publishes hash telemetry, and always attempts CTS cancellation through `CancelRunSourceNoThrow()`. Disposal nulls the field before cancel/dispose to avoid reuse.
Rejected Alternatives: Let duplicate local bridges run, or rely on current `CancelSequence` never throwing. Duplicate local runners break deterministic sequence ownership; adapter assumptions do not survive parallel integration.
Scalability potential: Low/MX350 avoids duplicated signal spam from accidental duplicate bridges; Middle/High/Ultra keep a single authoritative cinematic state machine before expensive water/audio/VFX responders activate.
Hardware Impact: 0 us hot path. Duplicate/disable paths add only scalar checks and fault-only telemetry; preventing duplicate runners avoids multiple wait-loop and signal costs. Verification this pass is static only by user request; no dotnet rebuild or response-file compile was run.

## Decision 28 - Pre-Registration Ownership Repair

Problem: Static readback showed `GlobalRegistry.RegisterServiceAllowSameInstance` replaces an existing slot instead of rejecting it. The previous post-register check was too late: a duplicate bridge could overwrite the authoritative prologue runtime, then consider itself registered.
Solution: Check `GlobalRegistry.PrologueSequence` before `RegisterPrologueSequenceRuntime()` and return if another service already owns the slot. Bind input and hot-swap only after registration ownership is proven.
Rejected Alternatives: Keep post-register validation only, or bind input before ownership is known. Post-register validation cannot prevent overwrite; pre-ownership input binding lets rejected bridges react to dev-skip input.
Scalability potential: Low/MX350 avoids duplicate wait loops and duplicate VWS/haptic/splashdown signals from scene misconfiguration; Middle/High/Ultra preserve one authoritative cinematic state machine before expensive responders activate.
Hardware Impact: 0 us hot path. Enable-time adds one registry pointer read and equality check; avoids duplicate sequence CPU/signal traffic under misconfiguration. Verification this pass is static only by user request; no dotnet rebuild or response-file compile was run.

## Decision 29 - Hot-Path Registry Cache And Auto-Run CTS Retention

Problem: The bridge still read `GlobalRegistry` service slots from prologue wait paths (`OrbitalDirector`, `StreamingBackpressure`, `TickDispatcher`, and dev input fallback), and the auto-run linked `CancellationTokenSource` stayed retained after normal sequence completion until destroy/re-enable.
Solution: Cache development flag, input, orbital, streaming, and tick-dispatcher dependencies during enable and refresh them via `IGlobalRegistryHotSwapListener`. The dev-only dilated wait refreshes only from the cached dispatcher field. Auto-run now goes through a guarded Awaitable wrapper that releases the linked CTS when the sequence finishes and reports hash telemetry if the service faults outside the director envelope.
Rejected Alternatives: Keep per-frame registry slot reads because they are convenient, or dispose the CTS only from `OnDestroy`. Registry polling violates the hot-path cache mandate; destroy-only disposal retains an unnecessary linked-token registration after successful prologue completion.
Scalability potential: Low/MX350 gets cheaper prologue wait frames and avoids retained lifecycle baggage after splashdown. Middle/High/Ultra keep the same cinematic staging while preserving one authoritative service cache that can be upgraded by hot-swap events for richer downstream orbital/ocean responders.
Hardware Impact: Estimated 2-8 us saved on wait frames that sample orbital/streaming/dev-skip state, depending on service access contention. One cold CTS is still allocated for auto-run cancellation, but it is released at sequence completion instead of waiting for destroy/re-enable. Verification this pass is static only by user request; no dotnet rebuild or response-file compile was run. `Tools/Architecture/HectonPhiAudit.ps1 -Json` timed out at 60 seconds, so no project-wide H-Phi metric is claimed.

## Decision 30 - Dev-Skip Cancellation Priority

Problem: Dev skip can set `_cancelReason = DevSkip`, then a same-frame disable can call `CancelSequence(ExplicitCancel)` before the Awaitable cancellation unwinds. That race downgrades the intended shallow-water handoff into an ordinary cancellation.
Solution: Normalize the incoming reason once and preserve an already-latched dev-skip reason unless the new reason is also dev skip. Later generic cancellation still marks `_cancelRequested`, but it cannot erase the forced handoff path.
Rejected Alternatives: Trust cancellation ordering, or move dev skip into the bridge only. Ordering is not deterministic during disable/scene teardown; bridge-only handling would duplicate the director's black-box and stage ownership.
Scalability potential: Low/MX350 dev iteration keeps deterministic shallow-water resume instead of a dead cancelled cinematic. Middle/High/Ultra production cadence is unchanged because the branch only matters after cancellation is requested.
Hardware Impact: 0 us steady-state. Cancellation path adds one byte comparison and branch; it prevents one lost dev handoff under interruption races. Verification this pass is static only by user request; no dotnet rebuild or response-file compile was run.

## Decision 31 - Hydration LOD Hysteresis

Problem: Loop 14 made hydration quality dynamic, but direct per-frame low/high policy sampling can flip proxy hydration on and off during thermal/memory tier churn. That violates the state-hysteresis mandate and can create inconsistent splashdown readiness.
Solution: Add a 150-frame hysteresis band to `IsLowTier` in the bridge. A new low-memory pressure flag still forces immediate downshift to the cheap proxy path; upgrades or non-emergency tier flips must stay stable before changing the cached hydration mode.
Rejected Alternatives: One-shot quality sampling, or immediate per-frame switching. One-shot sampling traps constrained devices in high-res waits; immediate switching trades correctness for flicker.
Scalability potential: Low/toaster path still reaches proxy water quickly under pressure. Middle/High/Ultra keep high-resolution hydration stable and avoid accidental proxy flicker, preserving expensive water/audio/VFX overkill only when the tier is stable.
Hardware Impact: Adds a few scalar bool/int branches to hydration wait frames, estimated 1-2 us. Prevents repeated readiness churn and wasted transition work during unstable quality policy. Verification this pass is static only by user request; no dotnet rebuild or response-file compile was run.
