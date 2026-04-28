using Hecton8.Bootstrap;
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

            FindOrCreateManager<AssetLifecycleGovernor>(ref bootstrap);
            FindOrCreateManager<AssetLoadDispatcher>(ref bootstrap);
            FindOrCreateManager<VRAMMonitor>(ref bootstrap);
            FindOrCreateManager<VRAMPressureMonitor>(ref bootstrap);
            FindOrCreateManager<RenderTextureLifecycleTracker>(ref bootstrap);
            FindOrCreateManager<RenderTexturePool>(ref bootstrap);
            FindOrCreateManager<VisorRTManager>(ref bootstrap);
            FindOrCreateManager<CameraRTManager>(ref bootstrap);
            FindOrCreateManager<PostFXRTManager>(ref bootstrap);
            FindOrCreateManager<UIRTManager>(ref bootstrap);
            FindOrCreateManager<SceneInstantiationGate>(ref bootstrap);
        }

        private static T FindOrCreateManager<T>(ref GameObject bootstrap) where T : Component
        {
            T existing = ResolveExistingManager<T>();
            if (existing != null)
                return existing;

            if (bootstrap == null)
            {
                bootstrap = new GameObject("__VRAMOptimizationBootstrap");
                DontDestroyOnLoad(bootstrap);
            }

            return bootstrap.AddComponent<T>();
        }

        private static T ResolveExistingManager<T>() where T : Component
        {
            if (typeof(T) == typeof(AssetLifecycleGovernor))
                return AssetLifecycleGovernor.Instance as T;
            if (typeof(T) == typeof(AssetLoadDispatcher))
                return AssetLoadDispatcher.Instance as T;
            if (typeof(T) == typeof(VRAMMonitor))
                return VRAMMonitor.Instance as T;
            if (typeof(T) == typeof(VRAMPressureMonitor))
                return VRAMPressureMonitor.Instance as T;
            if (typeof(T) == typeof(RenderTextureLifecycleTracker))
                return RenderTextureLifecycleTracker.Instance as T;
            if (typeof(T) == typeof(RenderTexturePool))
                return RenderTexturePool.Instance as T;
            if (typeof(T) == typeof(VisorRTManager))
                return VisorRTManager.Instance as T;
            if (typeof(T) == typeof(CameraRTManager))
                return CameraRTManager.Instance as T;
            if (typeof(T) == typeof(PostFXRTManager))
                return PostFXRTManager.Instance as T;
            if (typeof(T) == typeof(UIRTManager))
                return UIRTManager.Instance as T;
            if (typeof(T) == typeof(SceneInstantiationGate))
                return SceneInstantiationGate.Instance as T;

            return null;
        }
    }
}
