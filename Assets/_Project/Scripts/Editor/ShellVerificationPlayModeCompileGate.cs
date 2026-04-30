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
        private const int MaxRetryAttempts = 3;

        private static bool _isRegistered;
        private static bool _awaitingDirtyPlayEvaluation;
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
            _isRegistered = true;
        }

        private static void UnregisterCallbacks(bool clearRetryState)
        {
            if (_isRegistered)
            {
                EditorApplication.playModeStateChanged -= HandlePlayModeStateChanged;
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
                    $"[ShellSmokeGate] Dirty play entry persisted after {_retryAttempts} blocked entries. " +
                    $"scene={SceneManager.GetActiveScene().name} compiling={EditorApplication.isCompiling} updating={EditorApplication.isUpdating}");
                return;
            }

            _retryAttempts++;
            _lastReason = $"DirtyPlayEntry blocked={_retryAttempts}";

            Debug.LogWarning(
                $"[ShellSmokeGate] Aborting dirty play entry. Automatic Play Mode retry is disabled. " +
                $"scene={SceneManager.GetActiveScene().name} compiling={EditorApplication.isCompiling} updating={EditorApplication.isUpdating} blocked={_retryAttempts}");

            RequestStopPlayMode($"ShellSmokeDirtyPlay:{_retryAttempts}");
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
            _awaitingDirtyPlayEvaluation = false;
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

    }
}
#endif
