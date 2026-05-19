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

        internal static bool ShouldCullForSurvivalScale()
        {
            IResolutionScalerService scaler = s_cachedScaler;
            if (scaler == null)
            {
                scaler = GlobalRegistry.ResolutionScaler;
                s_cachedScaler = scaler;
            }

            if (scaler == null)
                return false;

            if (!scaler.TryGetScaleState(out ResolutionScaleState state))
            {
                s_cachedScaler = null;
                return false;
            }

            return state.StpActive != 0 &&
                   state.CurrentRenderScale01 <= SurvivalScaleThreshold;
        }

        internal static void Invalidate()
        {
            s_cachedScaler = null;
        }
    }
}
