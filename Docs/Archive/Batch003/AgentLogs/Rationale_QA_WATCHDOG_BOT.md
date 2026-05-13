# Rationale_QA_WATCHDOG_BOT

Status: PENDING VERIFICATION
Evidence Class Baseline: STATIC_DOC until compile/playmode artifacts exist.

## Decision 0: Active Batch Hygiene
Problem: QA status and rationale files were missing at session start.
Solution: Created fresh active-batch state files before code edits. DOD pattern: file-backed state machine.
Rejected Alternatives: Reusing archived batch files; chat-only memory. Both violate active batch hygiene and anti-amnesia protocol.
Scalability potential: Low tier gets deterministic QA state without agent memory dependence; Ultra tier can accumulate more detailed artifacts without changing runtime code.
Hardware Impact: 0us runtime impact on i3/MX350; editor-only process bookkeeping.

## Decision 1: Mandate Set
Problem: The endurance bot crosses QA, input, AUP, streaming, telemetry, save, and performance domains.
Solution: Loaded QA evidence, zero-GC, perf budget, crash telemetry, AUP precision, streaming residency, save persistence, and GlobalRegistry mandates.
Rejected Alternatives: Reading every mandate; it wastes context and increases cross-domain drift. Reading none violates registry-first protocol.
Scalability potential: Low tier uses minimal FastTick work and sparse sampling; Middle/High/Ultra can increase visual stress through existing quality tiers while QA remains deterministic.
Hardware Impact: Expected QA bot hot path target below 0.1ms on i3/MX350; proof pending profiler.

## Decision 2: QA Isolation And Input Override
Problem: A 10km swim bot needs deterministic control without reaching into player movement or singleton input owners.
Solution: Added Hecton8.QA asmdef and, after recheck, routed automation through PhysicsDeterminismSignals into InputDispatcher so IInputService.GetState exposes the override without replacing service ownership.
Rejected Alternatives: Direct HectonPlayerMovement mutation, InputManager mutation, Rigidbody pushing, and runtime GlobalRegistry input-service replacement. These couple QA to gameplay internals, hide input-stack regressions, or violate the ready lock.
Scalability potential: Low tier reads one deterministic override snapshot; Middle/High/Ultra can add denser telemetry while the locomotion path stays identical.
Hardware Impact: Estimated <1us override publish/consume on i3/MX350; no extra device input polling.

## Decision 3: CSV Writer Path
Problem: The prompt requires QA_Endurance_Log.csv without Debug.Log telemetry and with zero-GC formatting.
Solution: FastTick enqueues a blittable record into a bounded managed ring. A background thread formats with Span<char>/TryFormat and calls FileStream.WriteAsync.
Rejected Alternatives: File.AppendAllText, string interpolation, StringBuilder, or main-thread file writes. All allocate or stall the frame lane.
Scalability potential: Low writes every 1000m; High writes every 500m; Ultra writes every 250m for visual-overkill stress while preserving a cheap main-thread enqueue.
Hardware Impact: Main thread target <10us per sample on i3/MX350; disk latency is off-thread.

## Decision 4: Crash And Blackbox
Problem: The exact CrashTelemetrySignal contract requested by the prompt does not exist in the scanned GlobalSignals file.
Solution: Implemented available fixed-size ComplianceViolationSignal plus Application.logMessageReceived interception and a 300-frame NativeArray blackbox dump to Dump_QA_WATCHDOG_BOT.bin.
Rejected Alternatives: Inventing a new CrashTelemetrySignal or relying on chat output. Both would be fake integration evidence.
Scalability potential: Low stores only high-level state; Ultra can extend record detail without changing dump semantics.
Hardware Impact: NativeArray write estimated 2us/frame on i3/MX350; fault dump cost occurs only after crash/NaN.

## Decision 5: Blocked Contracts
Problem: Save request EventBus and global chunk-residency interface were not present in scanned contracts.
Solution: Marked exact EventBus save and chunk-residency verification as dependency-blocked. Implemented guarded ISaveService fallback every 2km; refused direct WorldChunkResidencyManager coupling.
Rejected Alternatives: Reflection, concrete scene searches, or invented signals. These would break the multi-agent domain boundary and create false proof.
Scalability potential: Low can run without residency probing; Middle/High/Ultra can attach once an interface exists.
Hardware Impact: 0us for blocked residency path; save scheduling is cold and outside FastTick.

## Decision 6: Verification Wall
Problem: Unity batchmode cannot open the project because a live Unity editor owns C:\hades\Hecton8. Temp compile also uses stale Library/ScriptAssemblies that lack current source contracts; full Hecton8.Core.csproj fails before QA on Cartography references.
Solution: Marked Task 19 as blocked/pending verification and retained all logs as evidence: UnityCompile_QA_WATCHDOG_BOT.log and TempCompile_QA_WATCHDOG_BOT.log.
Rejected Alternatives: Killing the user's Unity editor, editing generated csproj/project references, or claiming compile success from static source. Those are operationally unsafe or false.
Scalability potential: Low tier can be verified once the editor releases the lock; High/Ultra require profiler/playmode artifacts after compile clears.
Hardware Impact: 0us runtime impact. Verification is blocked before execution, not by QA runtime cost.

## Decision 7: Omega Polish Gate
Problem: The batch required reading POLISH_MANDATE only after core tasks were terminal. The tag is absent from CURRENT_BATCH.md, and QA source still needed a final anti-bloat sweep.
Solution: Logged the missing tag as evidence and ran targeted QA-folder audits for Update/FixedUpdate/LateUpdate, Debug.Log, File.AppendAllText, string interpolation, Span/TryFormat, FileStream.WriteAsync, FastTick, IOriginShiftListener, and System.Environment qualification.
Rejected Alternatives: Inferring a polish mandate from neighboring prompts or re-running broad repository scans as QA proof. Both would violate strict prompt parsing and contaminate evidence.
Scalability potential: Low tier remains sparse at 1000m CSV cadence; Middle/High/Ultra can tighten sample cadence and sonar stress without changing the deterministic input path.
Hardware Impact: Static polish saves 0us by itself. The concrete rejected hot-path alternatives would have cost 20-500us during disk/log stalls on i3/MX350; retained QA path targets <10us enqueue and <0.1ms frame cost pending profiler proof.

## Decision 8: Ready-Lock Safe Input Injection
Problem: The first QA input override attempted to unregister the real IInputService and register a wrapper at runtime. GlobalRegistry is ready-locked after bootstrap, so RegisterInputService can throw CriticalBootException; the path also risks service-rebound side effects.
Solution: Added a generic automation override lane to PhysicsDeterminismSignals and made InputDispatcher consume the latest valid override before assigning _currentState and publishing the deterministic input signal. QA now publishes PlayerInputState intent through that lane. DOD pattern: decoupled signal handoff, IInputService remains the authoritative read surface.
Rejected Alternatives: Reflection into GlobalRegistry hot-swap tokens, direct HectonPlayerMovement mutation, Rigidbody pushing, or keeping the ready-lock hijack. Each is either unsafe, domain-coupled, or hides input-stack regressions.
Scalability potential: Low tier consumes one latest override with no native queue growth; Middle/High/Ultra can use the same lane for denser automation scenarios without replacing service ownership.
Hardware Impact: Estimated <1us write/read on i3/MX350. Removed registry unregister/register, hot-swap notifications, and possible H8Memory reaping from the QA hot path; saved cold-path risk is correctness, not frame time.

## Decision 9: Override Lifetime Cleanup
Problem: Dispatcher order places IUpdatable input capture before Player FastTick. The previous-frame override model is valid, but a StopRun after FastTick could leave one stale automation input for the next frame.
Solution: Added PhysicsDeterminismSignals.ClearInputOverride and call it from QAEnduranceWatchdogBot.StopRun. InputDispatcher.ApplyAutomationOverride now returns whether it consumed an override so _lastDeliveredLookDelta stays coherent with the overridden state. PhysicsDeterminismSignals.PublishInput now accepts an optional byte flag so automation provenance survives into the normal deterministic input queue.
Rejected Alternatives: Registering QA in the Core IUpdatable lane, widening maxFrameAge, or leaving stale decay to frame age only. Those either violate the FastTick objective, increase override persistence, or create one-frame post-run automation leakage.
Scalability potential: Low tier gets deterministic one-frame-at-most override handoff with explicit teardown; Middle/High/Ultra can stress the same lane without queue growth or service rebinding.
Hardware Impact: ClearInputOverride is a single struct reset on StopRun. Boolean return and byte flag copy in InputDispatcher are branch/register-only work, estimated <1us on i3/MX350.

## Decision 10: No Per-Frame Writer Pulse
Problem: QAEnduranceWatchdogBot registered ILateFrameTickable only to call AutoResetEvent.Set on the CSV writer every frame. The writer already receives a signal on TryEnqueue and has a 100ms timeout, so the per-frame pulse was wasted hot-path kernel work.
Solution: Removed ILateFrameTickable from the bot, removed late-frame registration/unregistration, and removed QAEnduranceCsvWriter.Pulse. CSV output remains decoupled: enqueue signals the writer, timeout handles missed signals, dispose drains pending records.
Rejected Alternatives: Keeping LateFrameTick as a "flush safety" path, reducing pulse cadence, or moving the pulse to FastTick. All keep unnecessary hot-path synchronization when record-driven signaling already exists.
Scalability potential: Low tier avoids per-frame writer wakeups entirely; Middle/High/Ultra can increase CSV cadence without paying a constant every-frame synchronization tax.
Hardware Impact: Removed one AutoResetEvent.Set and one late-lane dispatch per active frame. Estimated saving 3-30us/frame on i3/MX350 depending on OS scheduler state; profiler proof still pending.

## Decision 11: Cold Allocation Ownership
Problem: QA harness and CSV writer owned legitimate cold allocations, but several lacked the mandated COLD ALLOC ownership comment.
Solution: Added canonical comments to autorun GameObject/component creation, NativeArray blackbox allocation, CSV writer instance, static format/header buffers, queue gate/signal, record/char/byte buffers, async FileStream, writer Thread, and cold crash/result file writers.
Rejected Alternatives: Treating QA code as exempt, relying on the static audit report, or removing useful cold buffers to avoid comments. Those hide memory lifetime ownership instead of documenting it.
Scalability potential: Low tier keeps fixed-size buffers visible for MX350 memory review; Middle/High/Ultra can scale queue/line capacities with explicit owner comments.
Hardware Impact: 0us runtime change. Documentation prevents accidental unbounded buffer growth and supports review of the fixed CSV/blackbox memory footprint.

## Decision 12: Autorun Bootstrap Safety
Problem: Runtime AddComponent on an active GameObject can invoke OnEnable before runOnEnable is assigned, so the batch-created bot could fail to start. The old duplicate guard also used FindAnyObjectByType, a scene search banned outside narrow initialization.
Solution: Create the autorun root, deactivate it before adding QAEnduranceWatchdogBot, assign runOnEnable and tier, mark the root DontDestroyOnLoad, then activate it. Added static active-instance and created flags reset at subsystem registration, an instance-accepted ownership guard for teardown, and removed the FindAnyObjectByType dependency.
Rejected Alternatives: Leaving the race to Unity callback order, forcing BeginRun manually after AddComponent, or keeping scene search as a duplicate guard. Those are brittle, duplicate logic, or violate registry-style lookup discipline.
Scalability potential: Low tier batch runs now survive bootstrap-to-world scene handoff without extra scene scans; higher tiers use the same deterministic harness startup.
Hardware Impact: Removes one cold scene search. Runtime frame impact is 0us; correctness impact is critical because the 10km run can now actually begin from command-line autorun.
