# LOG_QA_WATCHDOG_BOT

## 2026-05-13 QA Watchdog Report
Status: PENDING VERIFICATION.

What was wrong:
- No active QA status/rationale files existed for this prompt at session start.
- The requested CrashTelemetrySignal contract was not present in scanned GlobalSignals contracts.
- No EventBus save request signal was found; only ISaveService.SaveGameAsync exists.
- No global chunk-residency interface was found; direct WorldChunkResidencyManager coupling would cross the QA domain boundary.
- Unity batchmode could not open C:\hades\Hecton8 because a live editor instance already owned the project.
- Full Core compile is blocked before QA by unrelated Cartography reference errors. Temp QA compile is not authoritative because Library/ScriptAssemblies is stale.

What was done:
- Added isolated QA assembly files under Assets/_Project/Scripts/QA:
  - Hecton8.QA.asmdef
  - QAEnduranceWatchdogBot.cs
  - Editor/Hecton8.QA.Editor.asmdef
  - Editor/QAEnduranceBatchRunner.cs
- Implemented autorun entry through command line, environment variable, and Temp flag.
- Implemented GlobalRegistry-based IInputService override. No direct player movement singleton injection.
- Implemented FastTick endurance loop, AUP distance tracking, stuck detection, 5m recovery, PDA/sonar stress cadence, save-service fallback, leak detection, origin-shift logging, NaN/crash interception, and blackbox dump.
- Implemented Docs/AgentLogs/QA_Endurance_Log.csv writer with char[] + Span<char> + TryFormat and a background FileStream.WriteAsync path.
- Added fixed 300-frame NativeArray blackbox dump target: Docs/AgentLogs/Dump_QA_WATCHDOG_BOT.bin.
- Added editor batch runner for the 10km endurance path through Assets/_Project/Scenes/00_BOOTSTRAP.unity.
- Created static purge report: Docs/AgentLogs/QA_Purge_STATIC_SOURCE_QA_WATCHDOG_BOT.md.
- Updated Docs/Tasks/Status_QA_WATCHDOG_BOT.md and Docs/AgentLogs/Rationale_QA_WATCHDOG_BOT.md.

Cinematic cheats used:
- Collision recovery uses deterministic vertical reposition instead of obstacle escape physics.
- PDA/sonar is stressed by command/signal pulses every 500m rather than manual UI traversal.
- Math LOD uses sparse Low-tier sampling and denser High/Ultra telemetry/sonar stress. Low: 1000m CSV. Middle: 1000m CSV with normal radar. High: 500m CSV. Ultra: 250m CSV and heavier sonar pulse radius.
- AUP tracking uses squared distance gating and cold event writes instead of continuous expensive diagnostics.

Exact microseconds saved or estimated:
- Input override via GlobalRegistry: <1us per GetState path versus direct movement mutation diagnostics and branch coupling.
- Stuck check: 2us FastTick estimate; avoided physics escape solver work estimated 50-300us during trap recovery.
- Radar/PDA stress event: 5us per 500m scheduling event; avoided UI scene scraping.
- Save fallback scheduling: 4us per 2km gate; actual save cost remains outside FastTick.
- CSV enqueue: <10us per sample on i3/MX350 target; avoided File.AppendAllText/string interpolation stalls estimated 20-500us plus allocation spikes.
- Blackbox write: 2us per frame for high-level state.
- Static purge edits: 0us runtime change; this agent only flagged Core hot-loop logging because those files are owned by other domains.

Verification evidence:
- QA static audit clean for Update, FixedUpdate, LateUpdate, Debug.Log, File.AppendAllText, and string interpolation in Assets/_Project/Scripts/QA.
- Feature audit confirmed QA_Endurance_Log, PHYSICS_TRAP, LEAK_CRITICAL, WriteAsync, Span<char>, TryFormat, NativeArray blackbox, FastTick, IOriginShiftListener, and System.Environment usage in QA source.
- UnityCompile_QA_WATCHDOG_BOT.log records the live-editor project lock.
- TempCompile_QA_WATCHDOG_BOT.log records non-authoritative stale ScriptAssemblies dependency failures.
- Status remains PENDING VERIFICATION until Unity playmode/profiler artifacts prove the 10km run and 0B CSV writer allocation.

## 2026-05-13 QA Watchdog Recheck Addendum
Status: PENDING VERIFICATION.

What was wrong:
- Source recheck found the QA input wrapper was architecturally invalid. GlobalRegistry.RegisterInputService is ready-locked after bootstrap, so replacing the input service during the run could throw CriticalBootException and break the test before swimming.

What was done:
- Added a generic automation input override path in PhysicsDeterminismSignals.
- Updated InputDispatcher to consume the latest valid automation override before storing _currentState and publishing deterministic input.
- Changed QAEnduranceWatchdogBot to publish the desired PlayerInputState into that lane instead of unregistering/registering IInputService.
- Re-ran source-only audits. No dotnet build was launched.

Cinematic cheats used:
- Input remains deterministic and frame-scoped. No Rigidbody push is used for locomotion; teleport remains only the PHYSICS_TRAP recovery cheat.

Exact microseconds saved or estimated:
- Removed runtime registry unregister/register and service-rebound work from QA input install: cold-path correctness fix, estimated 10-200us avoided when starting the run plus removal of CriticalBootException risk.
- New automation override path: <1us latest-signal publish plus <1us InputDispatcher consume on i3/MX350.

Verification evidence:
- git diff --check passed for InputDispatcher and PhysicsDeterminismSignals, with only a CRLF normalization warning on existing InputDispatcher line endings.
- QA source audit found no Update, FixedUpdate, LateUpdate, Debug.Log, File.AppendAllText, string interpolation, or input-service register/unregister calls.

## 2026-05-13 QA Watchdog Recheck Addendum 2
Status: PENDING VERIFICATION.

What was wrong:
- Dispatcher-order recheck showed normal IUpdatable input capture runs before Player FastTick. The previous-frame override handoff is acceptable, but StopRun could leave one unconsumed automation override behind if the bot stopped after publishing in FastTick.

What was done:
- Added PhysicsDeterminismSignals.ClearInputOverride.
- QAEnduranceWatchdogBot.StopRun now clears the override lane before unregistering.
- InputDispatcher.ApplyAutomationOverride now returns a bool and updates _lastDeliveredLookDelta when automation is actually consumed.
- PhysicsDeterminismSignals.PublishInput now carries an optional byte flag, so the consumed QA override is still visible on the normal deterministic InputSignal.
- Re-ran source-only checks. No dotnet build was launched.

Cinematic cheats used:
- Kept deterministic input intent as a cheap latest-snapshot signal, not a physics force, service hot-swap, or transform driver.

Exact microseconds saved or estimated:
- ClearInputOverride: single struct reset on cold StopRun path, <1us.
- ApplyAutomationOverride bool return plus InputSignal flag propagation: branch/register-only hot-path cost, <1us estimated on i3/MX350.

Verification evidence:
- git diff --check passed for InputDispatcher, PhysicsDeterminismSignals, and QAEnduranceWatchdogBot; only existing InputDispatcher CRLF normalization warning remained.
- QA source audit found no Update, FixedUpdate, LateUpdate, Debug.Log, File.AppendAllText, or input-service register/unregister calls.
- Fixed-string audit found no string interpolation in QA C# files.

## 2026-05-13 QA Watchdog Recheck Addendum 3
Status: PENDING VERIFICATION.

What was wrong:
- QA runtime re-read found QAEnduranceWatchdogBot registered ILateFrameTickable only to call AutoResetEvent.Set on the CSV writer every frame. That was unnecessary synchronization because TryEnqueue already signals the writer and WriterLoop has a timeout fallback.

What was done:
- Removed ILateFrameTickable from QAEnduranceWatchdogBot.
- Removed late-frame registration/unregistration.
- Removed QAEnduranceCsvWriter.Pulse.
- CSV writer now wakes on record enqueue, 100ms timeout, or shutdown.
- Re-ran source-only checks. No dotnet build was launched.

Cinematic cheats used:
- Kept sparse CSV sampling as a record-driven wakeup instead of a per-frame flush loop.

Exact microseconds saved or estimated:
- Removed one late-lane dispatch plus one AutoResetEvent.Set per active frame. Estimated 3-30us/frame saved on i3/MX350 depending on scheduler state.

Verification evidence:
- rg found no ILateFrameTickable, LateFrameTick, Pulse, lateTick field, TryRegisterLateFrameTickable, or UnregisterLateFrameTickable in QAEnduranceWatchdogBot.
- QA source audit remains clean for Update, FixedUpdate, LateUpdate, Debug.Log, File.AppendAllText, and input-service register/unregister calls.

## 2026-05-13 QA Watchdog Recheck Addendum 4
Status: PENDING VERIFICATION.

What was wrong:
- Cold allocation audit found legitimate QA harness and CSV writer allocations without mandated COLD ALLOC ownership comments.

What was done:
- Added canonical COLD ALLOC comments for the autorun GameObject/component, blackbox NativeArray, QAEnduranceCsvWriter instance, static float/header buffers, writer gate/signal, record/char/byte buffers, FileStream, Thread, crash dump writers, result writer, and static header byte encoder.
- Re-ran source-only checks. No dotnet build was launched.

Cinematic cheats used:
- Fixed-size buffers remain the cheap path; no dynamic per-frame formatting or main-thread file writes were introduced.

Exact microseconds saved or estimated:
- 0us direct runtime change. The comments preserve memory ownership review and prevent future unbounded buffer expansion.

Verification evidence:
- git diff --check passed for QAEnduranceWatchdogBot after comment additions.
- rg confirmed COLD ALLOC annotations on cold heap/native allocations; remaining new hits are value-type records/vectors or already annotated cold I/O.

## 2026-05-13 QA Watchdog Recheck Addendum 5
Status: PENDING VERIFICATION.

What was wrong:
- Autorun creation used AddComponent on an active GameObject, so Unity could call OnEnable before runOnEnable was assigned. The batch bot could be created but not start.
- Duplicate prevention used Object.FindAnyObjectByType, a scene search that should not be in the runtime harness path.
- The autorun root was not protected from scene handoff.

What was done:
- Added subsystem-reset static active-instance and created flags.
- Replaced FindAnyObjectByType with the static guard.
- Created the autorun root inactive, added the component, assigned runOnEnable and tier, marked the root DontDestroyOnLoad, then activated it.
- Added an instance-accepted guard so rejected duplicate components do not run StopRun or clear the accepted bot's automation override during teardown.
- Re-ran source-only checks. No dotnet build was launched.

Cinematic cheats used:
- None. This is bootstrap correctness.

Exact microseconds saved or estimated:
- Removed one cold scene search during autorun setup. Frame-time impact is 0us; correctness gain is that the 10km command-line run can actually begin and survive scene transition.

Verification evidence:
- rg found no FindAnyObjectByType or FindObjectOfType in QAEnduranceWatchdogBot.
- Source read confirmed runOnEnable is assigned before root.SetActive(true), and DontDestroyOnLoad is applied before activation.
