using UnityEngine;

namespace Hecton8.Graphics.Caustics
{
    /// <summary>
    /// Legacy caustics compatibility shim. Runtime caustics authority is
    /// Hecton8.Rendering.AbyssalDeferredCausticsRuntime.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-9210)]
    public sealed class AnalyticalCausticsService : MonoBehaviour
    {
        public static AnalyticalCausticsService EnsureRuntimeInstance()
        {
            return null;
        }

        public void InitializeService()
        {
            enabled = false;
        }

        private void Awake()
        {
            enabled = false;
        }
    }
}
