using Hecton8.Core;
using Unity.Mathematics;
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
        private const float HardwareWeightMinGraphicsMemoryMb = 1024f;
        private const float HardwareWeightFullGraphicsMemoryMb = 8192f;
        private const float MinimumBoidPopulationScale = 0.4f;
        private const float SharedMemoryWeightCeiling = 0.35f;
        private const float BootstrapMipLimitMax = 2f;

        private static bool _initialized;
        private static bool _lowVramBudgetActive;
        private static bool _sharedMemoryBudgetActive;
        private static bool _capturedMipLimit;
        private static int _baselineMipLimit;
        private static float _hardwareBudgetWeight = 1f;

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
            _hardwareBudgetWeight = 1f;
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
            _hardwareBudgetWeight = ResolveHardwareBudgetWeight(DetectedGraphicsMemoryMb, _sharedMemoryBudgetActive);
            _lowVramBudgetActive = _hardwareBudgetWeight < 0.999f;

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
            if (!_initialized && !Application.isPlaying)
                return clampedRequested;

            float qualityCurve = ResolveQualityCurve();
            float hardwareScale = math.lerp(MinimumBoidPopulationScale, 1f, _hardwareBudgetWeight);
            float qualityScale = math.lerp(MinimumBoidPopulationScale, 1f, qualityCurve);
            float scale = math.saturate(math.min(hardwareScale, qualityScale));
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
            int minimumMipLimit = ResolveMinimumTextureMipLimit();
            int enforcedMipLimit = Mathf.Max(QualitySettings.globalTextureMipmapLimit, minimumMipLimit);
            if (QualitySettings.globalTextureMipmapLimit != enforcedMipLimit)
                QualitySettings.globalTextureMipmapLimit = enforcedMipLimit;
        }

        private static int ResolveMinimumTextureMipLimit()
        {
            float qualityCurve = ResolveQualityCurve();
            float usableWeight = math.saturate(math.min(_hardwareBudgetWeight, qualityCurve));
            float mipLimit = math.lerp(BootstrapMipLimitMax, 0f, usableWeight);
            return math.clamp((int)math.round(mipLimit), 0, 2);
        }

        private static float ResolveHardwareBudgetWeight(int graphicsMemoryMb, bool sharedMemoryModeActive)
        {
            float detectedMb = math.max(graphicsMemoryMb, HardwareWeightMinGraphicsMemoryMb);
            float dedicatedWeight = math.smoothstep(HardwareWeightMinGraphicsMemoryMb, HardwareWeightFullGraphicsMemoryMb, detectedMb);
            float sharedWeight = math.select(1f, SharedMemoryWeightCeiling, sharedMemoryModeActive);
            return math.saturate(math.min(dedicatedWeight, sharedWeight));
        }

        private static float ResolveQualityCurve()
        {
            if (MathLodRuntimeConfig.TryReadLatestConfig(out MathLodConfigDTO config))
                return math.smoothstep(0.15f, 0.85f, MathLodApproximation.SaturateFinite(config.GlobalQualityWeight, 1f));

            float quality = HomeostasisBrain.GlobalQualityWeight;
            return math.smoothstep(0.15f, 0.85f, math.saturate(math.select(1f, quality, math.isfinite(quality))));
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
