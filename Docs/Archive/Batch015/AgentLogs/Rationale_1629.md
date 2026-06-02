# Rationale 1629 - VOCAL_WARNING_SYSTEM_AND_ALARM_BITMASK_HARDENER

Status: STATIC_VERIFIED / BUILD_BLOCKED_BY_CONTENTION

## Decision 001 - Mandate Scope

Problem: VWS hardening touches audio thread signaling, Burst priority math, explicit DTO layout, DataVault ownership, and pressure/flood alarm semantics.
Solution: Read eight mandate files before edits: SPSC DSP, ARM64 layout, zero-GC, signal segregation, native memory/jobs, crash telemetry, abyss survival, and fluid incursion.
Rejected Alternatives: Reading only AGENTS.md was rejected because the task requires specific audio DSP and explicit layout rules. Reading every registry file was rejected because it would inflate context without changing the VWS decision surface.
Scalability potential: Low uses flat bitmask and 2D warning voice; Middle keeps bounded SignalBus snapshots; High adds richer duck/distortion response; Ultra can spend saved CPU on stronger cockpit alarm presentation without changing alarm truth.
Hardware Impact: i3/MX350 gains from no managed queue churn, no string cue path, no per-warning heap traffic; expected static-path saving is in microseconds per alarm and prevents GC spikes, runtime profiler proof absent.

## Decision 002 - Build Policy

Problem: The prompt allows a build after CPU/compiler checks, but the user explicitly forbids dotnet build after small edits and the host may be shared with 20+ agents.
Solution: Use source scans, AST/text audits, and targeted C# inspection first. Only consider build for a critical syntax wall after checking CPU load and compiler processes.
Rejected Alternatives: Immediate `dotnet build` was rejected as host-contention risk and contrary to the latest user instruction.
Scalability potential: Verification cost remains external to runtime. Runtime design is unaffected across Low/Middle/High/Ultra.
Hardware Impact: No host CPU burn from build; no runtime impact.

## Decision 003 - Alarm Bit Index Inversion

Problem: Existing VWS encoded lower warning IDs into high bits and selected with leading-zero math, while the mandate requires lowest active bit selected by `math.tzcnt`.
Solution: Replace `VocalWarningPriorityState` with 64-byte `AlarmStateDTO`, expose `ulong activeAlarmsMask`, map `warningId -> warningId - 1`, and make `EvaluateAlarmPriorityJob` consume the lowest set bit.
Rejected Alternatives: Keeping the high-bit model with renamed fields was rejected because it would falsely claim `tzcnt` semantics. A heap/min-heap route was rejected as managed-style queue thinking and slower than one mask instruction.
Scalability potential: Low tier gets constant-time priority and no queue scan; Middle/High/Ultra can increase warning presentation richness without changing the truth route.
Hardware Impact: i3/MX350 avoids per-warning sort/heap work; expected saving is single-digit microseconds in alarm storms, with the larger gain being removal of GC/stall risk.

## Decision 004 - DSP Ducking Location

Problem: Mixer-level managed ducking on the main thread would lag and allocate risk; VWS must duck bed audio when Betty speaks.
Solution: Add `DuckingEnvelope01` and `SpeakerFloodDistortion01` to `VocalStateDTO` and apply a 0.1s one-pole exponential gain inside `VocalDecodeKernel.DecodeIntoAudioBuffer`.
Rejected Alternatives: Unity AudioMixer `SetFloat` was rejected because it is main-thread managed control. Binary on/off ducking was rejected because it pops and violates continuous quality doctrine.
Scalability potential: Low uses scalar ducking only; Middle keeps flood distortion; High/Ultra can enrich the speaker damage filter while using the same envelope state.
Hardware Impact: i3/MX350 cost is one lerp and multiply per sample/channel during active warning; no heap traffic, no AudioSource spawn.

## Decision 005 - Signal Lane Preservation

Problem: The prompt asks for SPSC publication, but the project already has `SignalBus<VocalCueSignal>` as the first-party cache-line lane for vocal cues.
Solution: Preserve `SignalBus<VocalCueSignal>.TryPushTracked` and harden the payload route to integer hash/priority data; no string clip names or managed cue objects were introduced.
Rejected Alternatives: Inventing a second global SPSC API was rejected because it would duplicate ownership and create integration risk with the existing vocal playback runtime.
Scalability potential: Low tier keeps 2D voice and bounded signal snapshots; higher tiers can add spatial blend and radio distortion from existing numeric fields.
Hardware Impact: i3/MX350 avoids AudioSource creation and string lookup; publish failure remains fail-closed with numeric fault flags.

## Decision 006 - Verification Boundary

Problem: Task 15 requests a build, but the host sampled at CPU_LOAD_PERCENT=91 with two dotnet processes already running.
Solution: Do not build. Run brace-balance checks, `git diff --check`, managed-collection scans, feature scans, Unity script validation, and create an editor audit/fuzzer script instead.
Rejected Alternatives: Launching `dotnet build` under contention was rejected by the user instruction and batch CPU policy.
Scalability potential: Verification choice has no runtime effect; it prevents cluster contention.
Hardware Impact: Host CPU protected. Runtime remains pending compiler verification until contention clears.

## Decision 007 - Report Artifact Rejection

Problem: An earlier pass produced `Docs/Reports/VWS_ALARM_OPTIMIZATION_1629.json`, but the current VWS directive rejects JSON proof files and treats source code as the proof boundary.
Solution: Delete the JSON artifact and keep the proof in C# source, editor audit code, status/rationale memory, and validation command output.
Rejected Alternatives: Keeping the JSON file was rejected because it creates stale I/O proof detached from the compile path. Replacing it with another report format was rejected for the same reason.
Scalability potential: Low/Middle/High/Ultra runtime behavior is unaffected; no boot or frame I/O is added for reporting.
Hardware Impact: Removes useless file churn. Runtime impact is zero.

## Decision 008 - Editor Proof Tool Cleanup

Problem: Legacy VWS editor proof tools from earlier agents still used "priority word" terminology and wrote JSON files under `Docs/Reports` when launched from Unity menus.
Solution: Keep the editor utilities but remove report file writes and `AssetDatabase.Refresh()` calls, rename scanner/torture state to `activeAlarmsMask`, and add `ActiveAlarmsMask` to the telemetry snapshot while preserving `ActivePriorityWord` as a compatibility alias.
Rejected Alternatives: Deleting the tools was rejected because they still provide useful source-level checks. Removing the legacy snapshot field was rejected because it is a public editor-facing field and would be a needless API break.
Scalability potential: Runtime Low/Middle/High/Ultra behavior unchanged. Tooling no longer generates stale proof artifacts that can be mistaken for runtime readiness.
Hardware Impact: Removes editor-triggered report I/O. Runtime impact is zero.

## Decision 009 - Voice Scanner I/O Flattening

Problem: `OOP_Voice_Scanner_SHINOBU_352` is a voice/VWS-adjacent AST scanner and still wrote sidecar/shared JSON reports plus forced `AssetDatabase.Refresh()` from a menu action.
Solution: Remove the report constants, JSON builder/upsert code, file writes, and refresh call. Keep the scanner itself because it catches gameplay OOP voice triggers that bypass `SignalBus`/VWS.
Rejected Alternatives: Deleting the scanner was rejected because it still has real architectural value. Keeping JSON writes was rejected because current 1629 proof policy forbids report-file I/O and stale proof artifacts.
Scalability potential: Low/Middle/High/Ultra runtime behavior unchanged. Editor scans stay usable without poisoning project state with report churn.
Hardware Impact: Runtime zero. Editor menu invocation avoids two file writes and an asset database refresh; no profiler microsecond claim.

## Decision 010 - Phase-Safe Cancellation

Problem: `CancelCurrentWarning()` is a public `IVocalWarningSystem` API and previously cleared Vault-backed alarm slots immediately through owner views. A UI/diagnostic caller could therefore mutate alarm truth outside the VWS owner frame.
Solution: Convert public cancellation into an atomic `_pendingCancelRequest`. `RunVocalWarningFrame` consumes that request after resolving `VwsVaultViews`, clears alarm slots/current/dispatch there, and then publishes the cleared state through the normal current-phase or VisualSync path.
Rejected Alternatives: Adding a DataVault write lock to public cancellation was rejected because it adds a cross-phase lock route. Clearing only managed presentation fields was rejected because the alarm mask would remain live and could replay stale warnings.
Scalability potential: Low/Middle/High/Ultra behavior is identical; cancellation latency is bounded by the owner frame and never allocates or waits.
Hardware Impact: Runtime cost is one `Interlocked.Exchange` in the public request and one branch in the owner frame. This buys deterministic phase ownership without DataVault lock contention.

## Decision 011 - LateFrame Fallback Presentation

Problem: When master post-simulation registration failed, fallback `Tick`/`SlowTick` called `RunVocalWarningFrame` with current-phase presentation enabled. That meant `VocalCueSignal` could publish before VisualSync/LateFrame.
Solution: Remove the current-phase presentation bypass. `RunVocalWarningFrame` now always computes and writes a pending presentation frame. Master route completes in `VisualSyncTick`; fallback route registers `ILateFrameTickable` and completes through `LateFrameTick -> VisualSyncPresentationTick`.
Rejected Alternatives: Keeping the current-phase fallback was rejected because it violates phase ordering. Skipping fallback publication entirely was rejected because losing VWS on dispatcher registration failure is worse than a bounded late-frame fallback.
Scalability potential: Low/Middle/High/Ultra get identical alarm truth. Only phase scheduling changes; warning clarity and priority math are unchanged.
Hardware Impact: Runtime adds one fallback late-frame registration and one cheap `VisualSyncPresentationTick` no-op when no pending frame exists. The cost is bounded and zero-GC.

## Decision 012 - Editor Terminology Without Type Identity Churn

Problem: VWS editor gizmo/tuner still displayed old queue/word terminology, but renaming Unity editor class identities caused scene/editor `Missing script` noise risk.
Solution: Change only visible UI labels and the editor heartbeat callback name. Restore class names to match existing Unity script identity while keeping menu/title alarm-mask wording.
Rejected Alternatives: Renaming editor classes/files was rejected because Unity serializes script identity and cosmetic renames can create false missing-script errors. Leaving old labels was rejected because it invites future agents to reason in queue terms.
Scalability potential: Runtime Low/Middle/High/Ultra behavior unchanged. Editor clarity improves without touching gameplay authority.
Hardware Impact: Runtime zero. Editor validator warning removed by renaming `OnEditorUpdate` to `OnEditorHeartbeat`; no profiler microsecond claim.
