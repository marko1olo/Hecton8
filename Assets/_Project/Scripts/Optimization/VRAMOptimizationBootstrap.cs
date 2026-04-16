using UnityEngine;

namespace Hecton8.Optimization
{
    /// <summary>
    /// Bootstrap for VRAM optimization systems.
    /// Creates singleton instances and ensures DontDestroyOnLoad.
    /// </summary>
    [DefaultExecutionOrder(-8000)]
    public sealed class VRAMOptimizationBootstrap : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            if (!Application.isPlaying)
                return;

            GameObject bootstrap = null;

            FindOrCreateManager<VRAMMonitor>(ref bootstrap);
            FindOrCreateManager<RenderTextureLifecycleTracker>(ref bootstrap);
            FindOrCreateManager<RenderTexturePool>(ref bootstrap);
            FindOrCreateManager<VisorRTManager>(ref bootstrap);
            FindOrCreateManager<CameraRTManager>(ref bootstrap);
            FindOrCreateManager<PostFXRTManager>(ref bootstrap);
            FindOrCreateManager<UIRTManager>(ref bootstrap);
        }

        private static T FindOrCreateManager<T>(ref GameObject bootstrap) where T : Component
        {
            T existing = Object.FindAnyObjectByType<T>(FindObjectsInactive.Include);
            if (existing != null)
                return existing;

            if (bootstrap == null)
            {
                bootstrap = new GameObject("__VRAMOptimizationBootstrap");
                DontDestroyOnLoad(bootstrap);
            }

            return bootstrap.AddComponent<T>();
        }
    }
}
