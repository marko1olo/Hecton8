using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Hecton8.Bootstrap;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.Profiling;
using UnityEngine.SceneManagement;
using Debug = UnityEngine.Debug;

namespace Hecton8.Editor
{
    [InitializeOnLoad]
    public static class CodexPlayModeLauncher
    {
        private enum Phase
        {
            Idle = 0,
            Compile = 1,
            EnterPlay = 2,
            WaitPlayStart = 3,
            Sampling = 4,
            ExitPlay = 5,
        }

        private const string ActiveKey = "H8.CodexPlayModeLauncher.Active";
        private const string PhaseKey = "H8.CodexPlayModeLauncher.Phase";
        private const string StatusKey = "H8.CodexPlayModeLauncher.Status";
        private const string ExitCodeKey = "H8.CodexPlayModeLauncher.ExitCode";
        private const string PhaseStartTimeKey = "H8.CodexPlayModeLauncher.PhaseStartTime";
        private const string PlayStartTimeKey = "H8.CodexPlayModeLauncher.PlayStartTime";
        private const string StartAllocatedMemoryKey = "H8.CodexPlayModeLauncher.StartAllocatedMemory";
        private const string EndAllocatedMemoryKey = "H8.CodexPlayModeLauncher.EndAllocatedMemory";
        private const string PeakAllocatedMemoryKey = "H8.CodexPlayModeLauncher.PeakAllocatedMemory";
        private const string MaxGraphicsDriverMemoryKey = "H8.CodexPlayModeLauncher.MaxGraphicsDriverMemory";
        private const string EndReservedMemoryKey = "H8.CodexPlayModeLauncher.EndReservedMemory";
        private const string EndMonoUsedMemoryKey = "H8.CodexPlayModeLauncher.EndMonoUsedMemory";
        private const string AverageFpsKey = "H8.CodexPlayModeLauncher.AverageFps";
        private const string OnePercentLowFpsKey = "H8.CodexPlayModeLauncher.OnePercentLowFps";
        private const string FrameSampleCountKey = "H8.CodexPlayModeLauncher.FrameSampleCount";
        private const string SteadyStateStartedKey = "H8.CodexPlayModeLauncher.SteadyStateStarted";
        private const string SteadyStateStartAllocatedMemoryKey = "H8.CodexPlayModeLauncher.SteadyStateStartAllocatedMemory";
        private const string SteadyStateEndAllocatedMemoryKey = "H8.CodexPlayModeLauncher.SteadyStateEndAllocatedMemory";
        private const string SteadyStateStartGc0Key = "H8.CodexPlayModeLauncher.SteadyStateStartGc0";
        private const string SteadyStateEndGc0Key = "H8.CodexPlayModeLauncher.SteadyStateEndGc0";
        private const string SteadyStateCriticalGcSpikeKey = "H8.CodexPlayModeLauncher.SteadyStateCriticalGcSpike";
        private const string SteadyStateStartFrameKey = "H8.CodexPlayModeLauncher.SteadyStateStartFrame";
        private const string SteadyStateEndFrameKey = "H8.CodexPlayModeLauncher.SteadyStateEndFrame";
        private const string StartGc0Key = "H8.CodexPlayModeLauncher.StartGc0";
        private const string EndGc0Key = "H8.CodexPlayModeLauncher.EndGc0";
        private const string StartFrameKey = "H8.CodexPlayModeLauncher.StartFrame";
        private const string EndFrameKey = "H8.CodexPlayModeLauncher.EndFrame";
        private const string CompileErrorsKey = "H8.CodexPlayModeLauncher.CompileErrors";
        private const string CompileWarningsKey = "H8.CodexPlayModeLauncher.CompileWarnings";
        private const string LogErrorsKey = "H8.CodexPlayModeLauncher.LogErrors";
        private const string LogAssertionsKey = "H8.CodexPlayModeLauncher.LogAssertions";
        private const string LogExceptionsKey = "H8.CodexPlayModeLauncher.LogExceptions";
        private const string BeeKilledKey = "H8.CodexPlayModeLauncher.BeeKilled";
        private const string LoadedScenePathKey = "H8.CodexPlayModeLauncher.LoadedScenePath";
        private const string DirtySceneReloadedKey = "H8.CodexPlayModeLauncher.DirtySceneReloaded";
        private const string UiEventSystemPresentKey = "H8.CodexPlayModeLauncher.UiEventSystemPresent";
        private const string UiInputModulePresentKey = "H8.CodexPlayModeLauncher.UiInputModulePresent";
        private const string UiInputModuleEnabledKey = "H8.CodexPlayModeLauncher.UiInputModuleEnabled";
        private const string UiInputActionsBoundKey = "H8.CodexPlayModeLauncher.UiInputActionsBound";
        private const string UiInputActionsEnabledKey = "H8.CodexPlayModeLauncher.UiInputActionsEnabled";
        private const string UiSelectedGameObjectKey = "H8.CodexPlayModeLauncher.UiSelectedGameObject";
        private const string MetricsPathKey = "H8.CodexPlayModeLauncher.MetricsPath";
        private const string MainMenuSceneName = "01_MAIN_MENU";
        private const string AutoRunFlagFileName = "run_playmode_sentinel.flag";
        private const double RequestedPlaySeconds = 15.0;
        private const double SteadyStateWarmupSeconds = 5.0;
        private const double PhaseTimeoutSeconds = 90.0;
        private const int BeeBackendKillWaitMilliseconds = 2000;
        private const int MaxFrameDeltaSamples = 4096;
        private const int MaxOnePercentWorstSamples = MaxFrameDeltaSamples / 100;
        private static readonly Encoding JsonEncoding = new UTF8Encoding(false);
        private static readonly byte[] AutoRunFlagPayload = { (byte)'1' };
        // COLD ALLOC: float[40] - editor-only streaming top-K frame hitch cache; replaces full-buffer sort - owner: CodexPlayModeLauncher
        private static readonly float[] _worstFrameDeltaSamples = new float[MaxOnePercentWorstSamples];
        private static int _frameDeltaSampleCount;
        private static int _worstFrameDeltaSampleCount;
        private static int _lastSampledFrame = -1;
        private static double _frameDeltaTotalSeconds;
        private static bool _runInvokedFromAutoRunFlag;
        private static bool _metricsWriteInFlight;

        static CodexPlayModeLauncher()
        {
            if (SessionState.GetBool(ActiveKey, false))
                AttachCallbacks();

            if (TryConsumeAutoRunFlag())
            {
                _runInvokedFromAutoRunFlag = true;
                EditorApplication.delayCall += Run;
            }
        }

        [MenuItem("Hecton8/Codex/Run Play Mode Sentinel")]
        public static async void Run()
        {
            bool resumedFromAutoRunFlag = _runInvokedFromAutoRunFlag;
            _runInvokedFromAutoRunFlag = false;
            ResetSessionState();
            SessionState.SetBool(ActiveKey, true);
            SessionState.SetString(MetricsPathKey, ResolveMetricsPath());
            SessionState.SetInt(BeeKilledKey, KillBeeBackends());
            if (!resumedFromAutoRunFlag)
                await TryWriteAutoRunFlagAsync();
            SetPhase(Phase.Compile);
            AttachCallbacks();

            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
            CompilationPipeline.RequestScriptCompilation();
            Tick();
        }

        private static void AttachCallbacks()
        {
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
            CompilationPipeline.assemblyCompilationFinished -= OnAssemblyCompilationFinished;
            CompilationPipeline.assemblyCompilationFinished += OnAssemblyCompilationFinished;
            Application.logMessageReceived -= OnLogMessageReceived;
            Application.logMessageReceived += OnLogMessageReceived;
        }

        private static void DetachCallbacks()
        {
            EditorApplication.update -= Tick;
            CompilationPipeline.assemblyCompilationFinished -= OnAssemblyCompilationFinished;
            Application.logMessageReceived -= OnLogMessageReceived;
        }

        private static void Tick()
        {
            if (!SessionState.GetBool(ActiveKey, false))
                return;

            if (HasPhaseTimedOut())
            {
                CompleteRun("phase_timeout", 2);
                return;
            }

            if (EditorApplication.isPlaying)
            {
                UpdatePeakAllocatedMemory();
                UpdateMaxGraphicsDriverMemory();
            }

            Phase phase = (Phase)SessionState.GetInt(PhaseKey, (int)Phase.Idle);
            switch (phase)
            {
                case Phase.Compile:
                    TickCompile();
                    break;
                case Phase.EnterPlay:
                    TickEnterPlay();
                    break;
                case Phase.WaitPlayStart:
                    TickWaitPlayStart();
                    break;
                case Phase.Sampling:
                    TickSampling();
                    break;
                case Phase.ExitPlay:
                    TickExitPlay();
                    break;
            }
        }

        private static void TickCompile()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
                return;

            if (SessionState.GetInt(CompileErrorsKey, 0) > 0)
            {
                CompleteRun("compile_failed", 1);
                return;
            }

            ForceLoadEntrySceneFromDisk();
            SetPhase(Phase.EnterPlay);
        }

        private static void TickEnterPlay()
        {
            TryDeleteAutoRunFlag();
            if (!EditorApplication.isPlayingOrWillChangePlaymode)
                EditorApplication.isPlaying = true;

            SetPhase(Phase.WaitPlayStart);
        }

        private static void TickWaitPlayStart()
        {
            if (!EditorApplication.isPlaying)
                return;

            long allocatedMemory = Profiler.GetTotalAllocatedMemoryLong();
            SessionState.SetString(PlayStartTimeKey, EditorApplication.timeSinceStartup.ToString("R", CultureInfo.InvariantCulture));
            SessionState.SetString(StartAllocatedMemoryKey, allocatedMemory.ToString(CultureInfo.InvariantCulture));
            SessionState.SetString(PeakAllocatedMemoryKey, allocatedMemory.ToString(CultureInfo.InvariantCulture));
            SessionState.SetString(
                MaxGraphicsDriverMemoryKey,
                Math.Max(0L, Profiler.GetAllocatedMemoryForGraphicsDriver()).ToString(CultureInfo.InvariantCulture));
            SessionState.SetInt(StartGc0Key, GC.CollectionCount(0));
            SessionState.SetInt(StartFrameKey, Time.frameCount);
            ResetFrameSamples();
            SetPhase(Phase.Sampling);
        }

        private static void TickSampling()
        {
            if (!EditorApplication.isPlaying)
            {
                CompleteRun("playmode_exited_early", 3);
                return;
            }

            double playStartTime = GetSessionDouble(PlayStartTimeKey, EditorApplication.timeSinceStartup);
            TryBeginSteadyStateWindow(playStartTime);
            RecordFrameSample();
            if (EditorApplication.timeSinceStartup - playStartTime < RequestedPlaySeconds)
                return;

            CaptureEndMetrics();
            bool criticalGcSpike = SessionState.GetBool(SteadyStateCriticalGcSpikeKey, false);
            SessionState.SetString(StatusKey, criticalGcSpike ? "critical_gc_spike" : "completed");
            SessionState.SetInt(ExitCodeKey, criticalGcSpike ? 4 : 0);
            SetPhase(Phase.ExitPlay);
            EditorApplication.isPlaying = false;
        }

        private static void TickExitPlay()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            RequestMetricsWriteAndCleanup();
        }

        private static void CaptureEndMetrics()
        {
            SessionState.SetString(EndAllocatedMemoryKey, Profiler.GetTotalAllocatedMemoryLong().ToString(CultureInfo.InvariantCulture));
            SessionState.SetString(EndReservedMemoryKey, Profiler.GetTotalReservedMemoryLong().ToString(CultureInfo.InvariantCulture));
            SessionState.SetString(EndMonoUsedMemoryKey, Profiler.GetMonoUsedSizeLong().ToString(CultureInfo.InvariantCulture));
            SessionState.SetInt(EndGc0Key, GC.CollectionCount(0));
            SessionState.SetInt(EndFrameKey, Time.frameCount);
            CaptureSteadyStateEndMetrics();
            SessionState.SetString(LoadedScenePathKey, ResolveObservedScenePath());
            StoreUiInputMetrics();
            UpdatePeakAllocatedMemory();
            UpdateMaxGraphicsDriverMemory();
            StoreFrameMetrics();
        }

        private static void TryBeginSteadyStateWindow(double playStartTime)
        {
            if (SessionState.GetBool(SteadyStateStartedKey, false) || playStartTime <= 0d)
                return;

            if (EditorApplication.timeSinceStartup - playStartTime < SteadyStateWarmupSeconds)
                return;

            SessionState.SetBool(SteadyStateStartedKey, true);
            SessionState.SetString(
                SteadyStateStartAllocatedMemoryKey,
                Profiler.GetTotalAllocatedMemoryLong().ToString(CultureInfo.InvariantCulture));
            SessionState.SetInt(SteadyStateStartGc0Key, GC.CollectionCount(0));
            SessionState.SetInt(SteadyStateStartFrameKey, Time.frameCount);
        }

        private static void CaptureSteadyStateEndMetrics()
        {
            long allocatedMemory = Profiler.GetTotalAllocatedMemoryLong();
            int gc0 = GC.CollectionCount(0);
            int frame = Time.frameCount;

            if (!SessionState.GetBool(SteadyStateStartedKey, false))
            {
                SessionState.SetString(SteadyStateStartAllocatedMemoryKey, allocatedMemory.ToString(CultureInfo.InvariantCulture));
                SessionState.SetInt(SteadyStateStartGc0Key, gc0);
                SessionState.SetInt(SteadyStateStartFrameKey, frame);
            }

            SessionState.SetString(SteadyStateEndAllocatedMemoryKey, allocatedMemory.ToString(CultureInfo.InvariantCulture));
            SessionState.SetInt(SteadyStateEndGc0Key, gc0);
            SessionState.SetInt(SteadyStateEndFrameKey, frame);
            int steadyStateStartGc0 = SessionState.GetInt(SteadyStateStartGc0Key, gc0);
            SessionState.SetBool(SteadyStateCriticalGcSpikeKey, gc0 - steadyStateStartGc0 > 0);
        }

        private static void UpdatePeakAllocatedMemory()
        {
            long current = Profiler.GetTotalAllocatedMemoryLong();
            long peak = GetSessionLong(PeakAllocatedMemoryKey, 0L);
            if (current > peak)
                SessionState.SetString(PeakAllocatedMemoryKey, current.ToString(CultureInfo.InvariantCulture));
        }

        private static void UpdateMaxGraphicsDriverMemory()
        {
            long current = Profiler.GetAllocatedMemoryForGraphicsDriver();
            if (current < 0L)
                current = 0L;

            long peak = GetSessionLong(MaxGraphicsDriverMemoryKey, 0L);
            if (current > peak)
                SessionState.SetString(MaxGraphicsDriverMemoryKey, current.ToString(CultureInfo.InvariantCulture));
        }

        private static void RecordFrameSample()
        {
            int frame = Time.frameCount;
            if (frame == _lastSampledFrame)
                return;

            _lastSampledFrame = frame;
            float delta = Time.unscaledDeltaTime;
            if (delta <= 0f || float.IsNaN(delta) || float.IsInfinity(delta))
                return;

            if (_frameDeltaSampleCount >= MaxFrameDeltaSamples)
                return;

            _frameDeltaTotalSeconds += delta;
            _frameDeltaSampleCount++;
            RecordWorstFrameDeltaSample(delta);
        }

        private static void RecordWorstFrameDeltaSample(float delta)
        {
            int count = _worstFrameDeltaSampleCount;
            if (count < _worstFrameDeltaSamples.Length)
            {
                InsertWorstFrameDeltaGrowing(count, delta);
                _worstFrameDeltaSampleCount = count + 1;
                return;
            }

            if (count <= 0 || delta <= _worstFrameDeltaSamples[0])
                return;

            InsertWorstFrameDeltaFull(count, delta);
        }

        private static void InsertWorstFrameDeltaGrowing(int count, float delta)
        {
            int cursor = count;
            while (cursor > 0 && delta < _worstFrameDeltaSamples[cursor - 1])
            {
                _worstFrameDeltaSamples[cursor] = _worstFrameDeltaSamples[cursor - 1];
                cursor--;
            }

            _worstFrameDeltaSamples[cursor] = delta;
        }

        private static void InsertWorstFrameDeltaFull(int count, float delta)
        {
            int cursor = 0;
            while (cursor + 1 < count && delta > _worstFrameDeltaSamples[cursor + 1])
            {
                _worstFrameDeltaSamples[cursor] = _worstFrameDeltaSamples[cursor + 1];
                cursor++;
            }

            _worstFrameDeltaSamples[cursor] = delta;
        }

        private static void StoreFrameMetrics()
        {
            double averageFps = _frameDeltaTotalSeconds > 0d
                ? _frameDeltaSampleCount / _frameDeltaTotalSeconds
                : 0d;
            double onePercentLowFps = CalculateOnePercentLowFps();

            SessionState.SetString(AverageFpsKey, averageFps.ToString("R", CultureInfo.InvariantCulture));
            SessionState.SetString(OnePercentLowFpsKey, onePercentLowFps.ToString("R", CultureInfo.InvariantCulture));
            SessionState.SetInt(FrameSampleCountKey, _frameDeltaSampleCount);
        }

        private static double CalculateOnePercentLowFps()
        {
            int count = _frameDeltaSampleCount;
            if (count <= 0)
                return 0d;

            int worstCount = Math.Max(1, count / 100);
            int availableWorstCount = _worstFrameDeltaSampleCount;
            if (worstCount > availableWorstCount)
                worstCount = availableWorstCount;

            double worstDeltaTotal = 0d;
            int firstWorstIndex = availableWorstCount - worstCount;
            for (int i = firstWorstIndex; i < availableWorstCount; i++)
                worstDeltaTotal += _worstFrameDeltaSamples[i];

            return worstDeltaTotal > 0d ? worstCount / worstDeltaTotal : 0d;
        }

        private static void StoreUiInputMetrics()
        {
            bool eventSystemPresent = false;
            bool inputModulePresent = false;
            bool inputModuleEnabled = false;
            bool inputActionsBound = false;
            bool inputActionsEnabled = false;
            string selectedGameObject = string.Empty;

            EventSystem eventSystem = EventSystem.current;
            if (eventSystem != null)
            {
                eventSystemPresent = true;
                GameObject selected = eventSystem.currentSelectedGameObject;
                if (selected != null)
                    selectedGameObject = selected.name;

                if (eventSystem.TryGetComponent(out InputSystemUIInputModule inputModule))
                {
                    inputModulePresent = true;
                    inputModuleEnabled = inputModule.enabled;
                    inputActionsBound = HasUsableUiActionReference(inputModule.point) &&
                                        HasUsableUiActionReference(inputModule.leftClick) &&
                                        HasUsableUiActionReference(inputModule.move) &&
                                        HasUsableUiActionReference(inputModule.submit) &&
                                        HasUsableUiActionReference(inputModule.cancel);
                    inputActionsEnabled = inputActionsBound &&
                                          IsUiActionReferenceEnabled(inputModule.point) &&
                                          IsUiActionReferenceEnabled(inputModule.leftClick) &&
                                          IsUiActionReferenceEnabled(inputModule.move) &&
                                          IsUiActionReferenceEnabled(inputModule.submit) &&
                                          IsUiActionReferenceEnabled(inputModule.cancel);
                }
            }

            SessionState.SetBool(UiEventSystemPresentKey, eventSystemPresent);
            SessionState.SetBool(UiInputModulePresentKey, inputModulePresent);
            SessionState.SetBool(UiInputModuleEnabledKey, inputModuleEnabled);
            SessionState.SetBool(UiInputActionsBoundKey, inputActionsBound);
            SessionState.SetBool(UiInputActionsEnabledKey, inputActionsEnabled);
            SessionState.SetString(UiSelectedGameObjectKey, selectedGameObject);
        }

        private static bool HasUsableUiActionReference(InputActionReference reference)
        {
            return reference != null &&
                   reference.action != null &&
                   reference.action.bindings.Count > 0;
        }

        private static bool IsUiActionReferenceEnabled(InputActionReference reference)
        {
            return reference != null &&
                   reference.action != null &&
                   reference.action.enabled;
        }

        private static void ResetFrameSamples()
        {
            _frameDeltaSampleCount = 0;
            _worstFrameDeltaSampleCount = 0;
            _lastSampledFrame = -1;
            _frameDeltaTotalSeconds = 0d;
        }

        private static void CompleteRun(string status, int exitCode)
        {
            TryDeleteAutoRunFlag();
            if (EditorApplication.isPlaying)
            {
                CaptureEndMetrics();
                SessionState.SetString(StatusKey, status);
                SessionState.SetInt(ExitCodeKey, exitCode);
                SetPhase(Phase.ExitPlay);
                EditorApplication.isPlaying = false;
                return;
            }

            SessionState.SetString(StatusKey, status);
            SessionState.SetInt(ExitCodeKey, exitCode);
            RequestMetricsWriteAndCleanup();
        }

        private static void CleanupAndExitIfBatch()
        {
            int exitCode = SessionState.GetInt(ExitCodeKey, 0);
            SessionState.SetBool(ActiveKey, false);
            DetachCallbacks();
            if (Application.isBatchMode)
                EditorApplication.Exit(exitCode);
        }

        private static void SetPhase(Phase phase)
        {
            SessionState.SetInt(PhaseKey, (int)phase);
            SessionState.SetString(PhaseStartTimeKey, EditorApplication.timeSinceStartup.ToString("R", CultureInfo.InvariantCulture));
        }

        private static bool HasPhaseTimedOut()
        {
            Phase phase = (Phase)SessionState.GetInt(PhaseKey, (int)Phase.Idle);
            if (phase == Phase.Sampling)
                return false;

            double phaseStartTime = GetSessionDouble(PhaseStartTimeKey, EditorApplication.timeSinceStartup);
            return EditorApplication.timeSinceStartup - phaseStartTime > PhaseTimeoutSeconds;
        }

        private static void OnAssemblyCompilationFinished(string assemblyPath, CompilerMessage[] messages)
        {
            if (!SessionState.GetBool(ActiveKey, false) || messages == null)
                return;

            int errors = SessionState.GetInt(CompileErrorsKey, 0);
            int warnings = SessionState.GetInt(CompileWarningsKey, 0);
            for (int i = 0; i < messages.Length; i++)
            {
                if (messages[i].type == CompilerMessageType.Error)
                    errors++;
                else if (messages[i].type == CompilerMessageType.Warning)
                    warnings++;
            }

            SessionState.SetInt(CompileErrorsKey, errors);
            SessionState.SetInt(CompileWarningsKey, warnings);
        }

        private static void OnLogMessageReceived(string condition, string stackTrace, LogType type)
        {
            if (!SessionState.GetBool(ActiveKey, false))
                return;

            switch (type)
            {
                case LogType.Error:
                    SessionState.SetInt(LogErrorsKey, SessionState.GetInt(LogErrorsKey, 0) + 1);
                    break;
                case LogType.Assert:
                    SessionState.SetInt(LogAssertionsKey, SessionState.GetInt(LogAssertionsKey, 0) + 1);
                    break;
                case LogType.Exception:
                    SessionState.SetInt(LogExceptionsKey, SessionState.GetInt(LogExceptionsKey, 0) + 1);
                    break;
            }
        }

        private static int KillBeeBackends()
        {
            int killed = 0;
            try
            {
                Process[] processes = Process.GetProcessesByName("bee_backend");
                for (int i = 0; i < processes.Length; i++)
                {
                    Process process = processes[i];
                    if (TryKillBeeBackendNoThrow(process))
                        killed++;

                    DisposeBeeBackendProcessNoThrow(process);
                }
            }
            catch (Exception exception)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogWarning("[CodexPlayModeLauncher] Failed to kill bee_backend: " + exception.Message);
#endif
            }

            return killed;
        }

        private static bool TryKillBeeBackendNoThrow(Process process)
        {
            try
            {
                if (process.HasExited)
                    return false;

                process.Kill();
                process.WaitForExit(BeeBackendKillWaitMilliseconds);
                return true;
            }
            catch (Exception exception)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogWarning("[CodexPlayModeLauncher] Failed to kill bee_backend: " + exception.Message);
#endif
                return false;
            }
        }

        private static void DisposeBeeBackendProcessNoThrow(Process process)
        {
            try
            {
                process.Dispose();
            }
            catch (Exception exception)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogWarning("[CodexPlayModeLauncher] Failed to dispose bee_backend handle: " + exception.Message);
#endif
            }
        }

        private static void ForceLoadEntrySceneFromDisk()
        {
            string targetScenePath = ResolveEntryScenePath();
            if (string.IsNullOrEmpty(targetScenePath))
                return;

            Scene activeScene = SceneManager.GetActiveScene();
            bool dirtySceneReloaded = activeScene.IsValid() && activeScene.isDirty;
            if (dirtySceneReloaded ||
                !string.Equals(activeScene.path, targetScenePath, StringComparison.OrdinalIgnoreCase))
            {
                EditorSceneManager.OpenScene(targetScenePath, UnityEditor.SceneManagement.OpenSceneMode.Single);
            }

            SessionState.SetBool(DirtySceneReloadedKey, dirtySceneReloaded);
            SessionState.SetString(LoadedScenePathKey, targetScenePath);
        }

        private static string ResolveEntryScenePath()
        {
            EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;
            for (int i = 0; i < scenes.Length; i++)
            {
                if (scenes[i] != null &&
                    scenes[i].enabled &&
                    scenes[i].path.IndexOf("00_BOOTSTRAP", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return scenes[i].path;
                }
            }

            for (int i = 0; i < scenes.Length; i++)
            {
                if (scenes[i] != null && scenes[i].enabled)
                    return scenes[i].path;
            }

            Scene activeScene = SceneManager.GetActiveScene();
            return activeScene.IsValid() ? activeScene.path : string.Empty;
        }

        private static string ResolveObservedScenePath()
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (!scene.IsValid() || !scene.isLoaded)
                    continue;

                if (scene.path.IndexOf(MainMenuSceneName, StringComparison.OrdinalIgnoreCase) >= 0)
                    return scene.path;
            }

            Scene activeScene = SceneManager.GetActiveScene();
            if (activeScene.IsValid() && activeScene.isLoaded && !string.IsNullOrEmpty(activeScene.path))
                return activeScene.path;

            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (scene.IsValid() && scene.isLoaded && !string.IsNullOrEmpty(scene.path))
                    return scene.path;
            }

            return SessionState.GetString(LoadedScenePathKey, string.Empty);
        }

        private static string ResolveMetricsPath()
        {
            DirectoryInfo projectRoot = Directory.GetParent(Application.dataPath);
            string rootPath = projectRoot != null ? projectRoot.FullName : Application.dataPath;
            return Path.Combine(rootPath, "playmode_metrics.json");
        }

        private static bool TryConsumeAutoRunFlag()
        {
            string flagPath = ResolveAutoRunFlagPath();
            if (!File.Exists(flagPath))
                return false;

            try
            {
                File.Delete(flagPath);
                return true;
            }
            catch (Exception exception)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogWarning("[CodexPlayModeLauncher] Failed to consume autorun flag: " + exception.Message);
#endif
                return false;
            }
        }

        private static async Task TryWriteAutoRunFlagAsync()
        {
            try
            {
                string flagPath = ResolveAutoRunFlagPath();
                string directory = Path.GetDirectoryName(flagPath);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);

                using (FileStream stream = new FileStream(
                           flagPath,
                           FileMode.Create,
                           FileAccess.Write,
                           FileShare.Read,
                           AutoRunFlagPayload.Length,
                           FileOptions.Asynchronous | FileOptions.WriteThrough))
                {
                    await stream.WriteAsync(AutoRunFlagPayload, 0, AutoRunFlagPayload.Length);
                    await stream.FlushAsync();
                }
            }
            catch (Exception exception)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogWarning("[CodexPlayModeLauncher] Failed to write autorun flag: " + exception.Message);
#endif
            }
        }

        private static void TryDeleteAutoRunFlag()
        {
            try
            {
                string flagPath = ResolveAutoRunFlagPath();
                if (File.Exists(flagPath))
                    File.Delete(flagPath);
            }
            catch (Exception exception)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogWarning("[CodexPlayModeLauncher] Failed to delete autorun flag: " + exception.Message);
#endif
            }
        }

        private static string ResolveAutoRunFlagPath()
        {
            DirectoryInfo projectRoot = Directory.GetParent(Application.dataPath);
            string rootPath = projectRoot != null ? projectRoot.FullName : Application.dataPath;
            return Path.Combine(rootPath, "CodexArtifacts", AutoRunFlagFileName);
        }

        private static void RequestMetricsWriteAndCleanup()
        {
            if (_metricsWriteInFlight)
                return;

            _metricsWriteInFlight = true;
            _ = WriteMetricsAndCleanupAsync();
        }

        private static async Task WriteMetricsAndCleanupAsync()
        {
            try
            {
                await WriteMetricsAsync();
            }
            catch (Exception exception)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogWarning("[CodexPlayModeLauncher] Metrics pipeline failed before cleanup: " + exception.Message);
#endif
            }
            finally
            {
                _metricsWriteInFlight = false;
                CleanupAndExitIfBatch();
            }
        }

        private static async Task WriteMetricsAsync()
        {
            string metricsPath = SessionState.GetString(MetricsPathKey, ResolveMetricsPath());
            string status = SessionState.GetString(StatusKey, "unknown");
            double playStartTime = GetSessionDouble(PlayStartTimeKey, 0d);
            double observedPlaySeconds = playStartTime > 0d
                ? Math.Max(0d, EditorApplication.timeSinceStartup - playStartTime)
                : 0d;
            int startGc0 = SessionState.GetInt(StartGc0Key, 0);
            int endGc0 = SessionState.GetInt(EndGc0Key, startGc0);
            int startFrame = SessionState.GetInt(StartFrameKey, 0);
            int endFrame = SessionState.GetInt(EndFrameKey, startFrame);
            int steadyStateStartGc0 = SessionState.GetInt(SteadyStateStartGc0Key, 0);
            int steadyStateEndGc0 = SessionState.GetInt(SteadyStateEndGc0Key, steadyStateStartGc0);
            int steadyStateStartFrame = SessionState.GetInt(SteadyStateStartFrameKey, 0);
            int steadyStateEndFrame = SessionState.GetInt(SteadyStateEndFrameKey, steadyStateStartFrame);
            int steadyStateGcGen0CollectionsDelta = Math.Max(0, steadyStateEndGc0 - steadyStateStartGc0);

            StringBuilder builder = new StringBuilder(1792);
            builder.AppendLine("{");
            AppendJsonProperty(builder, "schema", "hecton8.codex.playmode_metrics.v1", comma: true);
            AppendJsonProperty(builder, "utc", DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture), comma: true);
            AppendJsonProperty(builder, "projectPath", Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath, comma: true);
            AppendJsonProperty(builder, "status", status, comma: true);
            AppendJsonProperty(builder, "exitCode", SessionState.GetInt(ExitCodeKey, 1), comma: true);
            AppendJsonProperty(builder, "requestedPlaySeconds", RequestedPlaySeconds, comma: true);
            AppendJsonProperty(builder, "observedPlaySeconds", observedPlaySeconds, comma: true);
            AppendJsonProperty(builder, "loadedScenePath", SessionState.GetString(LoadedScenePathKey, string.Empty), comma: true);
            AppendJsonProperty(builder, "dirtySceneReloadedFromDisk", SessionState.GetBool(DirtySceneReloadedKey, false), comma: true);
            AppendJsonProperty(builder, "uiEventSystemPresent", SessionState.GetBool(UiEventSystemPresentKey, false), comma: true);
            AppendJsonProperty(builder, "uiInputModulePresent", SessionState.GetBool(UiInputModulePresentKey, false), comma: true);
            AppendJsonProperty(builder, "uiInputModuleEnabled", SessionState.GetBool(UiInputModuleEnabledKey, false), comma: true);
            AppendJsonProperty(builder, "uiInputActionsBound", SessionState.GetBool(UiInputActionsBoundKey, false), comma: true);
            AppendJsonProperty(builder, "uiInputActionsEnabled", SessionState.GetBool(UiInputActionsEnabledKey, false), comma: true);
            AppendJsonProperty(builder, "uiSelectedGameObject", SessionState.GetString(UiSelectedGameObjectKey, string.Empty), comma: true);
            AppendJsonProperty(builder, "beeBackendsKilled", SessionState.GetInt(BeeKilledKey, 0), comma: true);
            AppendJsonProperty(builder, "compileErrorCount", SessionState.GetInt(CompileErrorsKey, 0), comma: true);
            AppendJsonProperty(builder, "compileWarningCount", SessionState.GetInt(CompileWarningsKey, 0), comma: true);
            AppendJsonProperty(builder, "logErrorCount", SessionState.GetInt(LogErrorsKey, 0), comma: true);
            AppendJsonProperty(builder, "logAssertionCount", SessionState.GetInt(LogAssertionsKey, 0), comma: true);
            AppendJsonProperty(builder, "logExceptionCount", SessionState.GetInt(LogExceptionsKey, 0), comma: true);
            AppendJsonProperty(builder, "startTotalAllocatedMemoryBytes", GetSessionLong(StartAllocatedMemoryKey, 0L), comma: true);
            AppendJsonProperty(builder, "endTotalAllocatedMemoryBytes", GetSessionLong(EndAllocatedMemoryKey, 0L), comma: true);
            AppendJsonProperty(builder, "peakTotalAllocatedMemoryBytes", GetSessionLong(PeakAllocatedMemoryKey, 0L), comma: true);
            AppendJsonProperty(builder, "endTotalReservedMemoryBytes", GetSessionLong(EndReservedMemoryKey, 0L), comma: true);
            AppendJsonProperty(builder, "endMonoUsedMemoryBytes", GetSessionLong(EndMonoUsedMemoryKey, 0L), comma: true);
            AppendJsonProperty(builder, "averageFps", GetSessionDouble(AverageFpsKey, 0d), comma: true);
            AppendJsonProperty(builder, "onePercentLowFps", GetSessionDouble(OnePercentLowFpsKey, 0d), comma: true);
            AppendJsonProperty(builder, "frameSampleCount", SessionState.GetInt(FrameSampleCountKey, 0), comma: true);
            AppendJsonProperty(builder, "maxVramUsageBytes", GetSessionLong(MaxGraphicsDriverMemoryKey, 0L), comma: true);
            AppendJsonProperty(builder, "gcGen0CollectionsDelta", Math.Max(0, endGc0 - startGc0), comma: true);
            AppendJsonProperty(builder, "steadyStateWarmupSeconds", SteadyStateWarmupSeconds, comma: true);
            AppendJsonProperty(builder, "steadyStateStarted", SessionState.GetBool(SteadyStateStartedKey, false), comma: true);
            AppendJsonProperty(builder, "steadyStateStartTotalAllocatedMemoryBytes", GetSessionLong(SteadyStateStartAllocatedMemoryKey, 0L), comma: true);
            AppendJsonProperty(builder, "steadyStateEndTotalAllocatedMemoryBytes", GetSessionLong(SteadyStateEndAllocatedMemoryKey, 0L), comma: true);
            AppendJsonProperty(builder, "steadyStateGcGen0CollectionsDelta", steadyStateGcGen0CollectionsDelta, comma: true);
            AppendJsonProperty(builder, "criticalGcSpike", SessionState.GetBool(SteadyStateCriticalGcSpikeKey, false), comma: true);
            AppendJsonProperty(builder, "steadyStateFramesObserved", Math.Max(0, steadyStateEndFrame - steadyStateStartFrame), comma: true);
            AppendJsonProperty(builder, "framesObserved", Math.Max(0, endFrame - startFrame), comma: false);
            builder.AppendLine("}");

            try
            {
                await WriteTextFileAsync(metricsPath, builder.ToString(), JsonEncoding);
            }
            catch (Exception exception)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogWarning("[CodexPlayModeLauncher] Failed to write Play Mode metrics: " + exception.Message);
#endif
                return;
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (steadyStateGcGen0CollectionsDelta > 0)
            {
                Debug.LogError(
                    "[CodexPlayModeLauncher] CRITICAL_GC_SPIKE steadyStateGcGen0CollectionsDelta=" +
                    steadyStateGcGen0CollectionsDelta);
            }

            Debug.Log("[CodexPlayModeLauncher] Wrote Play Mode metrics: " + metricsPath);
#endif
        }

        private static async Task WriteTextFileAsync(string path, string text, Encoding encoding)
        {
            if (string.IsNullOrEmpty(path))
                return;

            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            byte[] bytes = encoding.GetBytes(text ?? string.Empty);
            using (FileStream stream = new FileStream(
                       path,
                       FileMode.Create,
                       FileAccess.Write,
                       FileShare.Read,
                       bytes.Length > 0 ? bytes.Length : 1,
                       FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await stream.WriteAsync(bytes, 0, bytes.Length);
                await stream.FlushAsync();
            }
        }

        private static void AppendJsonProperty(StringBuilder builder, string name, string value, bool comma)
        {
            builder.Append("  ");
            AppendJsonString(builder, name);
            builder.Append(": ");
            AppendJsonString(builder, value);
            if (comma)
                builder.Append(',');
            builder.AppendLine();
        }

        private static void AppendJsonProperty(StringBuilder builder, string name, long value, bool comma)
        {
            builder.Append("  ");
            AppendJsonString(builder, name);
            builder.Append(": ");
            builder.Append(value.ToString(CultureInfo.InvariantCulture));
            if (comma)
                builder.Append(',');
            builder.AppendLine();
        }

        private static void AppendJsonProperty(StringBuilder builder, string name, double value, bool comma)
        {
            builder.Append("  ");
            AppendJsonString(builder, name);
            builder.Append(": ");
            builder.Append(value.ToString("0.######", CultureInfo.InvariantCulture));
            if (comma)
                builder.Append(',');
            builder.AppendLine();
        }

        private static void AppendJsonProperty(StringBuilder builder, string name, bool value, bool comma)
        {
            builder.Append("  ");
            AppendJsonString(builder, name);
            builder.Append(": ");
            builder.Append(value ? "true" : "false");
            if (comma)
                builder.Append(',');
            builder.AppendLine();
        }

        private static void AppendJsonString(StringBuilder builder, string value)
        {
            builder.Append('"');
            if (!string.IsNullOrEmpty(value))
            {
                for (int i = 0; i < value.Length; i++)
                {
                    char c = value[i];
                    switch (c)
                    {
                        case '\\':
                            builder.Append("\\\\");
                            break;
                        case '"':
                            builder.Append("\\\"");
                            break;
                        case '\n':
                            builder.Append("\\n");
                            break;
                        case '\r':
                            builder.Append("\\r");
                            break;
                        case '\t':
                            builder.Append("\\t");
                            break;
                        default:
                            builder.Append(c);
                            break;
                    }
                }
            }

            builder.Append('"');
        }

        private static long GetSessionLong(string key, long fallback)
        {
            string value = SessionState.GetString(key, fallback.ToString(CultureInfo.InvariantCulture));
            return long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out long parsed)
                ? parsed
                : fallback;
        }

        private static double GetSessionDouble(string key, double fallback)
        {
            string value = SessionState.GetString(key, fallback.ToString("R", CultureInfo.InvariantCulture));
            return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed)
                ? parsed
                : fallback;
        }

        private static void ResetSessionState()
        {
            _metricsWriteInFlight = false;
            SessionState.SetBool(ActiveKey, false);
            SessionState.SetInt(PhaseKey, (int)Phase.Idle);
            SessionState.SetString(StatusKey, "starting");
            SessionState.SetInt(ExitCodeKey, 1);
            SessionState.SetString(PhaseStartTimeKey, EditorApplication.timeSinceStartup.ToString("R", CultureInfo.InvariantCulture));
            SessionState.SetString(PlayStartTimeKey, "0");
            SessionState.SetString(StartAllocatedMemoryKey, "0");
            SessionState.SetString(EndAllocatedMemoryKey, "0");
            SessionState.SetString(PeakAllocatedMemoryKey, "0");
            SessionState.SetString(MaxGraphicsDriverMemoryKey, "0");
            SessionState.SetString(EndReservedMemoryKey, "0");
            SessionState.SetString(EndMonoUsedMemoryKey, "0");
            SessionState.SetString(AverageFpsKey, "0");
            SessionState.SetString(OnePercentLowFpsKey, "0");
            SessionState.SetInt(FrameSampleCountKey, 0);
            SessionState.SetBool(SteadyStateStartedKey, false);
            SessionState.SetString(SteadyStateStartAllocatedMemoryKey, "0");
            SessionState.SetString(SteadyStateEndAllocatedMemoryKey, "0");
            SessionState.SetInt(SteadyStateStartGc0Key, 0);
            SessionState.SetInt(SteadyStateEndGc0Key, 0);
            SessionState.SetBool(SteadyStateCriticalGcSpikeKey, false);
            SessionState.SetInt(SteadyStateStartFrameKey, 0);
            SessionState.SetInt(SteadyStateEndFrameKey, 0);
            SessionState.SetInt(StartGc0Key, 0);
            SessionState.SetInt(EndGc0Key, 0);
            SessionState.SetInt(StartFrameKey, 0);
            SessionState.SetInt(EndFrameKey, 0);
            SessionState.SetInt(CompileErrorsKey, 0);
            SessionState.SetInt(CompileWarningsKey, 0);
            SessionState.SetInt(LogErrorsKey, 0);
            SessionState.SetInt(LogAssertionsKey, 0);
            SessionState.SetInt(LogExceptionsKey, 0);
            SessionState.SetInt(BeeKilledKey, 0);
            SessionState.SetString(LoadedScenePathKey, string.Empty);
            SessionState.SetBool(DirtySceneReloadedKey, false);
            SessionState.SetBool(UiEventSystemPresentKey, false);
            SessionState.SetBool(UiInputModulePresentKey, false);
            SessionState.SetBool(UiInputModuleEnabledKey, false);
            SessionState.SetBool(UiInputActionsBoundKey, false);
            SessionState.SetBool(UiInputActionsEnabledKey, false);
            SessionState.SetString(UiSelectedGameObjectKey, string.Empty);
            SessionState.SetString(MetricsPathKey, ResolveMetricsPath());
            ResetFrameSamples();
        }
    }
}
