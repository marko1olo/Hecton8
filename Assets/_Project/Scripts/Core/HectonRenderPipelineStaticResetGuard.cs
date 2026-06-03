using UnityEngine;

namespace Hecton8.Core
{
    /// <summary>
    /// Keeps the previous first-party SRP static reset hook inert.
    /// </summary>
    internal static class HectonRenderPipelineStaticResetGuard
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetForSubsystemRegistration()
        {
            // Unity owns Blitter lifetime. First-party cleanup races URP RenderGraph copy passes
            // during Play Mode transitions and leaves the package static material null.
        }
    }
}
