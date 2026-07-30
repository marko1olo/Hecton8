# -*- coding: utf-8 -*-
"""Apply WaitForDispatcher fix: poll from Update instead of async NextFrameAsync.

Evidence 2026-07-30 p0fix smoke:
  L695 runner installed
  L707 waiting for dispatcher
  L740 SystemDispatcher init begins (bootstrap)
  ... SceneActivate short-circuit succeeds ...
  never: dispatcher acquired / lanes registered / ecology sample
  result: BATCH_TIMEOUT

Root cause: WaitForDispatcherAndStart awaits AwaitableDebtMonitor.NextFrameAsync.
In batchmode that is Task.Yield + MainThreadAsync. The runner's continuation is
queued but does not re-enter while bootstrap owns the main await chain / editor
playmode pump. ColdTick watchdog cannot help because lanes register only AFTER
this wait returns. Fix: drive the wait from MonoBehaviour.Update which the
player loop always invokes in editor playmode batch.
"""
from pathlib import Path

path = Path(r"C:\hades\Hecton8\Assets\_Project\Scripts\QA\Headless\HeadlessSimulationRunner.cs")
text = path.read_text(encoding="utf-8")

old_start = """        private async Awaitable RunStartupAsync(CancellationToken cancellationToken)
        {
            try
            {
                InitializeColdState();
                await WaitForDispatcherAndStart(cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
            }
            catch (IOException exception)
            {
                if (!_finished)
                    FailAndQuit(1, TimeoutHash, exception.GetType().Name);
            }
            catch (UnauthorizedAccessException exception)
            {
                if (!_finished)
                    FailAndQuit(1, TimeoutHash, exception.GetType().Name);
            }
            catch (InvalidOperationException exception)
            {
                if (!_finished)
                    FailAndQuit(1, TimeoutHash, exception.GetType().Name);
            }
            catch (ArgumentException exception)
            {
                if (!_finished)
                    FailAndQuit(1, TimeoutHash, exception.GetType().Name);
            }
        }"""

new_start = """        private void BeginStartup()
        {
            try
            {
                InitializeColdState();
                // Do NOT await NextFrameAsync for the dispatcher gate. In -batchmode
                // AwaitableDebtMonitor.NextFrameAsync is Task.Yield + MainThreadAsync; the
                // runner continuation is not guaranteed to resume while bootstrap holds the
                // main await chain. Evidence: headless_smoke_20260730_p0fix.log logs
                // "waiting for dispatcher" then SystemDispatcher init + SceneActivate
                // short-circuit, then silence until BATCH_TIMEOUT — no DISPATCHER_TIMEOUT
                // either, because ColdTick only runs after RegisterRuntimeLanes.
                // Player-loop Update always runs in editor playmode; poll there instead.
                _startupTime = Time.realtimeSinceStartupAsDouble;
                _awaitingDispatcher = true;
                LogRunnerLifecycle("waiting for dispatcher");
                TryCompleteDispatcherWait();
            }
            catch (IOException exception)
            {
                if (!_finished)
                    FailAndQuit(1, TimeoutHash, exception.GetType().Name);
            }
            catch (UnauthorizedAccessException exception)
            {
                if (!_finished)
                    FailAndQuit(1, TimeoutHash, exception.GetType().Name);
            }
            catch (InvalidOperationException exception)
            {
                if (!_finished)
                    FailAndQuit(1, TimeoutHash, exception.GetType().Name);
            }
            catch (ArgumentException exception)
            {
                if (!_finished)
                    FailAndQuit(1, TimeoutHash, exception.GetType().Name);
            }
        }

        private void Update()
        {
            if (!_awaitingDispatcher || _finished)
                return;

            TryCompleteDispatcherWait();
        }"""

if old_start not in text:
    raise SystemExit("BEGIN_STARTUP block not found")
text = text.replace(old_start, new_start, 1)

# Start() calls RunStartupAsync — retarget
old_call = "            _ = RunStartupAsync(destroyCancellationToken);"
new_call = "            BeginStartup();"
if old_call not in text:
    raise SystemExit("RunStartupAsync call not found")
text = text.replace(old_call, new_call, 1)

old_wait = """        private async Awaitable WaitForDispatcherAndStart(CancellationToken cancellationToken)
        {
            // Logged before the wait, not after, because the wait itself is the suspect. In batchmode
            // AwaitableDebtMonitor.NextFrameAsync resolves through Task.Yield() rather than a real frame
            // boundary, so a loop that awaits "next frame" is not guaranteed a frame at all - which means
            // the deadline below is only re-evaluated if something else pumps the player loop. If this
            // marker appears with no matching quit marker, the loop never resumed, and no watchdog inside
            // this component can help: ColdTick only runs once RegisterRuntimeLanes has succeeded, which
            // happens after this method returns. That watchdog has to live in the batch runner.
            LogRunnerLifecycle("waiting for dispatcher");
            _startupTime = Time.realtimeSinceStartupAsDouble;
            while (GlobalRegistry.Dispatcher == null && Time.realtimeSinceStartupAsDouble - _startupTime <= _startupTimeoutSeconds)
            {
                if (cancellationToken.IsCancellationRequested || _finished)
                    return;

                await AwaitableDebtMonitor.NextFrameAsync(cancellationToken);
            }

            if (cancellationToken.IsCancellationRequested || _finished)
                return;

            if (GlobalRegistry.Dispatcher == null)
            {
                FailAndQuit(1, TimeoutHash, "[DISPATCHER_TIMEOUT]");
                return;
            }

            ForceHeadlessRuntimePolicy();
            CacheDataVaultCold();
            if (!EnsureVaultBuffersCold() || !TryInitializeGhostState())
            {
                FailAndQuit(1, DataVaultUnavailableHash, "[DATAVAULT_UNAVAILABLE]");
                return;
            }

            RegisterRuntimeLanes();
            TryRegisterHotSwapListener();
            HectonFloatingOrigin.RegisterListener(this);
            _originListenerRegistered = true;
            GlobalRegistry.TickDispatcher?.RequestHeadlessTimeDilation(TimeDilationScalar, RunnerHash);
            if (!_started)
                FailAndQuit(1, TimeoutHash, "[RUNNER_REGISTRATION_FAILED]");
        }"""

new_wait = """        private void TryCompleteDispatcherWait()
        {
            if (!_awaitingDispatcher || _finished)
                return;

            if (GlobalRegistry.Dispatcher == null)
            {
                if (Time.realtimeSinceStartupAsDouble - _startupTime > _startupTimeoutSeconds)
                {
                    _awaitingDispatcher = false;
                    FailAndQuit(1, TimeoutHash, "[DISPATCHER_TIMEOUT]");
                }

                return;
            }

            _awaitingDispatcher = false;
            LogRunnerLifecycle("dispatcher acquired");

            try
            {
                ForceHeadlessRuntimePolicy();
                CacheDataVaultCold();
                if (!EnsureVaultBuffersCold() || !TryInitializeGhostState())
                {
                    FailAndQuit(1, DataVaultUnavailableHash, "[DATAVAULT_UNAVAILABLE]");
                    return;
                }

                RegisterRuntimeLanes();
                TryRegisterHotSwapListener();
                HectonFloatingOrigin.RegisterListener(this);
                _originListenerRegistered = true;
                GlobalRegistry.TickDispatcher?.RequestHeadlessTimeDilation(TimeDilationScalar, RunnerHash);
                if (!_started)
                    FailAndQuit(1, TimeoutHash, "[RUNNER_REGISTRATION_FAILED]");
                else
                    LogRunnerLifecycle("runtime lanes registered; dilation requested");
            }
            catch (IOException exception)
            {
                if (!_finished)
                    FailAndQuit(1, TimeoutHash, exception.GetType().Name);
            }
            catch (UnauthorizedAccessException exception)
            {
                if (!_finished)
                    FailAndQuit(1, TimeoutHash, exception.GetType().Name);
            }
            catch (InvalidOperationException exception)
            {
                if (!_finished)
                    FailAndQuit(1, TimeoutHash, exception.GetType().Name);
            }
            catch (ArgumentException exception)
            {
                if (!_finished)
                    FailAndQuit(1, TimeoutHash, exception.GetType().Name);
            }
        }"""

if old_wait not in text:
    raise SystemExit("WAIT block not found")
text = text.replace(old_wait, new_wait, 1)

# add field _awaitingDispatcher near other bools
old_field = "        private bool _runtimePolicyCaptured;"
new_field = "        private bool _runtimePolicyCaptured;\n        private bool _awaitingDispatcher;"
if old_field not in text:
    raise SystemExit("field anchor not found")
text = text.replace(old_field, new_field, 1)

# clear flag on destroy / finish paths if needed - OnDestroy should clear
old_destroy = """        private void OnDestroy()
        {
            _ghostStepPending = false;"""
new_destroy = """        private void OnDestroy()
        {
            _awaitingDispatcher = false;
            _ghostStepPending = false;"""
if old_destroy not in text:
    raise SystemExit("OnDestroy anchor not found")
text = text.replace(old_destroy, new_destroy, 1)

path.write_text(text, encoding="utf-8")
print("OK patched", path)
# verify
t2 = path.read_text(encoding="utf-8")
for s in ("BeginStartup", "TryCompleteDispatcherWait", "_awaitingDispatcher", "dispatcher acquired", "private void Update()"):
    print(s, "YES" if s in t2 else "NO")
for s in ("RunStartupAsync", "WaitForDispatcherAndStart"):
    print(s, "STILL" if s in t2 else "gone")
