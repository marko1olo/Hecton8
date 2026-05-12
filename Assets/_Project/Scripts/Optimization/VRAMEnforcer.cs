using Hecton8.Core;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Hecton8.Optimization
{
    /// <summary>
    /// Applies MX350-class runtime rendering and simulation clamps during bootstrap.
    /// </summary>
    internal static class VRAMEnforcer
    {
        private const int LowVramGraphicsMemoryMbThreshold = 2048;
        private const int HalfResolutionTextureMipLimit = 1;
        private const int SharedMemoryTextureMipLimit = 2;
        private const float LowVramBoidPopulationScale = 0.5f;
        private const float SharedMemoryBoidPopulationScale = 0.4f;

        private static bool _initialized;
        private static bool _lowVramBudgetActive;
        private static bool _sharedMemoryBudgetActive;
        private static bool _capturedMipLimit;
        private static int _baselineMipLimit;

#if UNITY_EDITOR
        private static bool _editorRestoreHookRegistered;
#endif

        /// <summary>
        /// Detected dedicated graphics memory reported by the active runtime in MB.
        /// </summary>
        internal static int DetectedGraphicsMemoryMb { get; private set; }

        /// <summary>
        /// Returns whether the current hardware falls under the MX350 guard profile.
        /// </summary>
        internal static bool IsLowVramBudgetActive => _lowVramBudgetActive;

        /// <summary>
        /// Returns whether UMA/shared-memory Deck-style budgeting is active.
        /// </summary>
        internal static bool IsSharedMemoryBudgetActive => _sharedMemoryBudgetActive;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _initialized = false;
            _lowVramBudgetActive = false;
            _sharedMemoryBudgetActive = false;
            _capturedMipLimit = false;
            DetectedGraphicsMemoryMb = 0;

#if UNITY_EDITOR
            if (_editorRestoreHookRegistered)
            {
                EditorApplication.playModeStateChanged -= HandleEditorPlayModeStateChanged;
                _editorRestoreHookRegistered = false;
            }
#endif
        }

        /// <summary>
        /// Applies low-VRAM runtime clamps once during bootstrap.
        /// </summary>
        internal static void InitializeRuntimeBudget()
        {
            if (_initialized)
                return;

            _initialized = true;
            HardwareTierDetector.EnsureInitialized();
            DetectedGraphicsMemoryMb = Mathf.Max(0, SystemInfo.graphicsMemorySize);
            _sharedMemoryBudgetActive = HardwareTierDetector.SharedMemoryModeActive;
            _lowVramBudgetActive =
                _sharedMemoryBudgetActive ||
                (DetectedGraphicsMemoryMb > 0 && DetectedGraphicsMemoryMb <= LowVramGraphicsMemoryMbThreshold);
            if (!_lowVramBudgetActive)
                return;

            CaptureBaselines();
            ApplyTextureBudget();

#if UNITY_EDITOR
            EnsureEditorRestoreHook();
#endif
        }

        /// <summary>
        /// Applies the hardware boid clamp for compute-driven fauna systems.
        /// </summary>
        internal static int ApplyBoidPopulationBudget(int requestedCount, int minimumCount, int maximumCount)
        {
            if (Application.isPlaying && !_initialized)
                InitializeRuntimeBudget();

            int clampedRequested = Mathf.Clamp(requestedCount, minimumCount, maximumCount);
            if (!_lowVramBudgetActive)
                return clampedRequested;

            float scale = _sharedMemoryBudgetActive ? SharedMemoryBoidPopulationScale : LowVramBoidPopulationScale;
            int scaledCount = Mathf.RoundToInt(clampedRequested * scale);
            return Mathf.Clamp(scaledCount, minimumCount, maximumCount);
        }

        private static void CaptureBaselines()
        {
            if (!_capturedMipLimit)
            {
                _baselineMipLimit = QualitySettings.globalTextureMipmapLimit;
                _capturedMipLimit = true;
            }
        }

        private static void ApplyTextureBudget()
        {
            int minimumMipLimit = _sharedMemoryBudgetActive
                ? SharedMemoryTextureMipLimit
                : HalfResolutionTextureMipLimit;
            int enforcedMipLimit = Mathf.Max(QualitySettings.globalTextureMipmapLimit, minimumMipLimit);
            if (QualitySettings.globalTextureMipmapLimit != enforcedMipLimit)
                QualitySettings.globalTextureMipmapLimit = enforcedMipLimit;
        }

#if UNITY_EDITOR
        private static void EnsureEditorRestoreHook()
        {
            if (Application.isBatchMode)
                return;

            if (_editorRestoreHookRegistered)
                return;

            EditorApplication.playModeStateChanged += HandleEditorPlayModeStateChanged;
            _editorRestoreHookRegistered = true;
        }

        private static void HandleEditorPlayModeStateChanged(PlayModeStateChange state)
        {
            if (Application.isBatchMode)
                return;

            if (state != PlayModeStateChange.ExitingPlayMode && state != PlayModeStateChange.EnteredEditMode)
                return;

            RestoreEditorOverrides();
        }

        private static void RestoreEditorOverrides()
        {
            if (_capturedMipLimit)
                QualitySettings.globalTextureMipmapLimit = _baselineMipLimit;
        }
#endif
    }
}
