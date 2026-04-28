// ============================================================================
// HECTON-8 - ShellVerificationPlayModeCompileGate.cs
// Editor-only guard for shell verification runs under editor compilation churn.
// Yields to active runtime telemetry instead of killing measurement sessions.
// ============================================================================

#if UNITY_EDITOR
using Hecton8.Dev;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Hecton8.Editor
{
    internal static class ShellVerificationPlayModeCompileGate
    {
        private const string EnableMenuPath = "Hecton/Dev/Verification/Enable Shell Smoke Compile Gate";
        private const string DisableMenuPath = "Hecton/Dev/Verification/Disable Shell Smoke Compile Gate";
        private const string BootstrapSceneName = "00_BOOTSTRAP";
        private const string MainMenuSceneName = "01_MAIN_MENU";
        private const double RetryStableWindowSeconds = 0.75d;
        private const double EnteredPlayModeRetryBudgetSeconds = 20d;
        private const int MaxRetryAttempts = 3;

        private static bool _isRegistered;
        private static bool _retryPending;
        private static bool _awaitingDirtyPlayEvaluation;
        private static double _retryRequestedAt;
        private static double _stableSince;
        private static int _retryAttempts;
        private static string _lastReason = "None";

        [MenuItem(EnableMenuPath, false, 140)]
        private static void Enable()
        {
            RegisterCallbacks();
        }

        [MenuItem(DisableMenuPath, false, 141)]
        private static void Disable()
        {
            UnregisterCallbacks(clearRetryState: true);
        }

        private static void RegisterCallbacks()
        {
            if (_isRegistered)
                return;

            EditorApplication.playModeStateChanged -= HandlePlayModeStateChanged;
            EditorApplication.playModeStateChanged += HandlePlayModeStateChanged;
            EditorApplication.update -= HandleEditorUpdate;
            EditorApplication.update += HandleEditorUpdate;
            _isRegistered = true;
        }

        private static void UnregisterCallbacks(bool clearRetryState)
        {
            if (_isRegistered)
            {
                EditorApplication.playModeStateChanged -= HandlePlayModeStateChanged;
                EditorApplication.update -= HandleEditorUpdate;
                _isRegistered = false;
            }

            if (clearRetryState)
                ClearRetryState();
        }

        private static void HandlePlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingPlayMode || state == PlayModeStateChange.EnteredEditMode)
            {
                _awaitingDirtyPlayEvaluation = false;
                if (!_retryPending)
                    _retryAttempts = 0;

                return;
            }

            if (!ShouldGuardVerificationSession())
                return;

            if (state == PlayModeStateChange.ExitingEditMode)
            {
                _awaitingDirtyPlayEvaluation = true;
                _lastReason = "Entering play";
                return;
            }

            if (state != PlayModeStateChange.EnteredPlayMode || !_awaitingDirtyPlayEvaluation)
                return;

            _awaitingDirtyPlayEvaluation = false;
            if (!EditorApplication.isCompiling && !EditorApplication.isUpdating)
            {
                _retryAttempts = 0;
                return;
            }

            if (ShouldYieldToRuntimeProfiler("EnteredPlayMode"))
                return;

            if (_retryAttempts >= MaxRetryAttempts)
            {
                Debug.LogWarning(
                    $"[ShellSmokeGate] Dirty play entry persisted after {_retryAttempts} retries. " +
                    $"scene={SceneManager.GetActiveScene().name} compiling={EditorApplication.isCompiling} updating={EditorApplication.isUpdating}");
                return;
            }

            _retryPending = true;
            _retryRequestedAt = EditorApplication.timeSinceStartup;
            _stableSince = 0d;
            _retryAttempts++;
            _lastReason = $"DirtyPlayEntry retry={_retryAttempts}";

            Debug.LogWarning(
                $"[ShellSmokeGate] Aborting dirty play entry and scheduling retry. " +
                $"scene={SceneManager.GetActiveScene().name} compiling={EditorApplication.isCompiling} updating={EditorApplication.isUpdating} retry={_retryAttempts}");

            RequestStopPlayMode($"ShellSmokeDirtyPlay:{_retryAttempts}");
        }

        private static void HandleEditorUpdate()
        {
            if (!_retryPending)
                return;

            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isPlaying)
                return;

            if (!ShouldGuardVerificationSession())
            {
                ClearRetryState();
                return;
            }

            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                _stableSince = 0d;
                return;
            }

            double now = EditorApplication.timeSinceStartup;
            if (_stableSince <= 0d)
            {
                _stableSince = now;
                return;
            }

            if ((now - _retryRequestedAt) > EnteredPlayModeRetryBudgetSeconds)
            {
                Debug.LogWarning($"[ShellSmokeGate] Retry window expired. lastReason={_lastReason}");
                ClearRetryState();
                return;
            }

            if ((now - _stableSince) < RetryStableWindowSeconds)
                return;

            Debug.Log($"[ShellSmokeGate] Retrying play after stable editor window. retry={_retryAttempts}");
            _retryPending = false;
            _awaitingDirtyPlayEvaluation = true;
            RequestStartPlayMode("ShellSmokeRetryStableWindow");
        }

        private static bool ShouldGuardVerificationSession()
        {
            Scene activeScene = SceneManager.GetActiveScene();
            if (!activeScene.IsValid())
                return false;

            string sceneName = activeScene.name;
            if (!string.Equals(sceneName, BootstrapSceneName, System.StringComparison.Ordinal) &&
                !string.Equals(sceneName, MainMenuSceneName, System.StringComparison.Ordinal))
            {
                return false;
            }

            ShellVerificationRuntimeSmokeTester shellSmoke =
                Object.FindAnyObjectByType<ShellVerificationRuntimeSmokeTester>(FindObjectsInactive.Include);
            return shellSmoke != null || ShellVerificationRuntimeSmokeTester.HasPersistedResumeState();
        }

        private static bool ShouldYieldToRuntimeProfiler(string reason)
        {
            RuntimePerformanceProfiler profiler =
                Object.FindAnyObjectByType<RuntimePerformanceProfiler>(FindObjectsInactive.Include);
            if (profiler == null || !profiler.IsProfilingActive)
                return false;

            RuntimeDiagnosticsTrace.WriteEvent(
                "play.dirty_entry",
                $"owner={nameof(ShellVerificationPlayModeCompileGate)} reason={reason} action=continue " +
                $"scene={SceneManager.GetActiveScene().name} compiling={EditorApplication.isCompiling} updating={EditorApplication.isUpdating}");
            return true;
        }

        private static void ClearRetryState()
        {
            _retryPending = false;
            _awaitingDirtyPlayEvaluation = false;
            _retryRequestedAt = 0d;
            _stableSince = 0d;
            _retryAttempts = 0;
            _lastReason = "None";
        }

        private static void RequestStopPlayMode(string reason)
        {
            RuntimeDiagnosticsTrace.WriteEvent(
                "play.exit_request",
                $"owner={nameof(ShellVerificationPlayModeCompileGate)} reason={reason} scene={SceneManager.GetActiveScene().name} " +
                $"compiling={EditorApplication.isCompiling} updating={EditorApplication.isUpdating} paused={EditorApplication.isPaused}");
            EditorApplication.isPlaying = false;
        }

        private static void RequestStartPlayMode(string reason)
        {
            RuntimeDiagnosticsTrace.WriteEvent(
                "play.enter_request",
                $"owner={nameof(ShellVerificationPlayModeCompileGate)} reason={reason} scene={SceneManager.GetActiveScene().name} " +
                $"compiling={EditorApplication.isCompiling} updating={EditorApplication.isUpdating} paused={EditorApplication.isPaused}");
            EditorApplication.isPlaying = true;
        }
    }
}
#endif
