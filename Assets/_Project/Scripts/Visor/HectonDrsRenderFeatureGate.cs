using Hecton8.Core;
using Hecton8.Core.Contracts;
using UnityEngine;

namespace Hecton8.Visor
{
    /// <summary>
    /// Shared DRS survival gate for URP renderer features.
    /// Keeps the steady-state render path on a cached scaler contract instead of per-feature registry polling.
    /// </summary>
    internal static class HectonDrsRenderFeatureGate
    {
        private const float SurvivalScaleThreshold = 0.6001f;

        private static readonly ResolutionScalerHotSwapListener s_hotSwapListener = new ResolutionScalerHotSwapListener();
        private static IResolutionScalerService s_cachedScaler;
        private static bool s_hotSwapRegistered;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            s_cachedScaler = null;
            s_hotSwapRegistered = false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void PrimeBeforeSceneLoad()
        {
            PrimeCold();
        }

        internal static void PrimeCold()
        {
            if (!Application.isPlaying)
            {
                s_cachedScaler = null;
                return;
            }

            s_cachedScaler = GlobalRegistry.ResolutionScaler;
            if (!s_hotSwapRegistered)
            {
                GlobalRegistry.TryUnregisterHotSwapListener(s_hotSwapListener);
                s_hotSwapRegistered = GlobalRegistry.TryRegisterHotSwapListener(s_hotSwapListener);
            }
        }

        internal static float ResolveSurvivalPressure01()
        {
            IResolutionScalerService scaler = s_cachedScaler;
            if (scaler == null)
                return 0f;

            if (!scaler.TryGetScaleState(out ResolutionScaleState state))
            {
                s_cachedScaler = null;
                return 0f;
            }

            if (state.StpActive == 0)
                return 0f;

            float scale = Mathf.Clamp01(state.CurrentRenderScale01);
            float pressure = Mathf.Clamp01((SurvivalScaleThreshold - scale) / SurvivalScaleThreshold);
            return pressure * pressure * (3f - 2f * pressure);
        }

        internal static float ResolveSurvivalVisualWeight01()
        {
            return 1f - ResolveSurvivalPressure01();
        }

        internal static bool HasRuntimeRenderOwner()
        {
            return SystemDispatcher.ActiveRuntimeInstance != null;
        }

        internal static void Invalidate()
        {
            s_cachedScaler = null;
        }

        private sealed class ResolutionScalerHotSwapListener : IGlobalRegistryHotSwapListener
        {
            public void OnGlobalRegistryServiceReplaced(
                GlobalRegistryServiceSlot serviceSlot,
                object previousService,
                object currentService)
            {
                if (serviceSlot == GlobalRegistryServiceSlot.ResolutionScalerService)
                    s_cachedScaler = currentService as IResolutionScalerService;
            }
        }
    }
}
