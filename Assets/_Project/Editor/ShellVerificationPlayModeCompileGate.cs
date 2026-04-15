// ============================================================================
// HECTON-8 - ShellVerificationPlayModeCompileGate.cs
// Editor-only guard that retries shell verification play sessions after editor
// compilation churn corrupts the initial play-mode entry.
// ============================================================================

#if UNITY_EDITOR
using Hecton8.Dev;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hecton8.Editor
{
    /// <summary>
    /// Retries bootstrap play-mode entry when editor compilation spills into the
    /// first verification run and invalidates shell smoke automation.
    /// </summary>
    [InitializeOnLoad]
    internal static class ShellVerificationPlayModeCompileGate
    {
        private const string BootstrapSceneName = "00_BOOTSTRAP";
        private const string MainMenuSceneName = "01_MAIN_MENU";
        private const double RetryStableWindowSeconds = 0.75d;
        private const double EnteredPlayModeRetryBudgetSeconds = 20d;
        private const int MaxRetryAttempts = 3;

        private static bool _retryPending;
        private static bool _awaitingDirtyPlayEvaluation;
        private static double _retryRequestedAt;
        private static double _stableSince;
        private static int _retryAttempts;
        private static string _lastReason = "None";

        static ShellVerificationPlayModeCompileGate()
        {
            EditorApplication.playModeStateChanged -= HandlePlayModeStateChanged;
            EditorApplication.playModeStateChanged += HandlePlayModeStateChanged;
            EditorApplication.update -= HandleEditorUpdate;
            EditorApplication.update += HandleEditorUpdate;
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

            if (state != PlayModeStateChange.EnteredPlayMode)
                return;

            if (!_awaitingDirtyPlayEvaluation)
                return;

            _awaitingDirtyPlayEvaluation = false;
            if (!EditorApplication.isCompiling && !EditorApplication.isUpdating)
            {
                _retryAttempts = 0;
                return;
            }

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

            EditorApplication.isPlaying = false;
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
            EditorApplication.isPlaying = true;
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
            return shellSmoke != null;
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
    }
}
#endif
