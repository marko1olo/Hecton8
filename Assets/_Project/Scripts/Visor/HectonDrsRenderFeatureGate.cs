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

        private static IResolutionScalerService s_cachedScaler;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            s_cachedScaler = null;
        }

        internal static float ResolveSurvivalPressure01()
        {
            IResolutionScalerService scaler = s_cachedScaler;
            if (scaler == null)
            {
                scaler = GlobalRegistry.ResolutionScaler;
                s_cachedScaler = scaler;
            }

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

        internal static void Invalidate()
        {
            s_cachedScaler = null;
        }
    }
}
