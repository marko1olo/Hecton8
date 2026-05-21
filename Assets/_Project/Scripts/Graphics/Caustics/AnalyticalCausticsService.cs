using Hecton8.Core;
using UnityEngine;

namespace Hecton8.Graphics.Caustics
{
    /// <summary>
    /// Legacy caustics compatibility shim. Runtime caustics authority is
    /// Hecton8.Rendering.AbyssalDeferredCausticsRuntime.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-9210)]
    public sealed class AnalyticalCausticsService : MonoBehaviour, ICausticsService, IServiceHeartbeat, IServiceShutdown, IGlobalRegistryHotSwapListener
    {
        public ServiceHeartbeatState HeartbeatState => ServiceHeartbeatState.NotStarted;
        public bool IsServiceReady => false;
        public int TickCount => 0;

        public static AnalyticalCausticsService EnsureRuntimeInstance()
        {
            return null;
        }

        public void InitializeService()
        {
            enabled = false;
        }

        public void OnGlobalRegistryServiceReplaced(GlobalRegistryServiceSlot serviceSlot, object previousService, object currentService)
        {
        }

        public void OnServiceShutdown()
        {
        }

        private void Awake()
        {
            enabled = false;
        }
    }
}
