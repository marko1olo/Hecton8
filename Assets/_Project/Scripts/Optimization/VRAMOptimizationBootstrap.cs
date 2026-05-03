using Hecton8.Bootstrap;
using Hecton8.Core;
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
            if (!Application.isPlaying || !GameBootstrapper.HasRuntimeInstance)
                return;

            EnsureRuntimeManagers();
        }

        internal static void EnsureRuntimeManagers()
        {
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
            }

            T manager = bootstrap.AddComponent<T>();
            GameBootstrapper.PersistRuntimeService(manager);
            return manager;
        }

        private static T ResolveExistingManager<T>() where T : Component
        {
            if (typeof(T) == typeof(AssetLifecycleGovernor))
                return GlobalRegistry.AssetLifecycle as T;
            if (typeof(T) == typeof(AssetLoadDispatcher))
                return GlobalRegistry.AssetLoadDispatcher as T;
            if (typeof(T) == typeof(VRAMMonitor))
                return GlobalRegistry.VRAMMonitor as T;
            if (typeof(T) == typeof(VRAMPressureMonitor))
                return GlobalRegistry.VRAMPressure as T;
            if (typeof(T) == typeof(RenderTextureLifecycleTracker))
                return GlobalRegistry.RenderTextureLifecycle as T;
            if (typeof(T) == typeof(RenderTexturePool))
                return GlobalRegistry.RenderTexturePool as T;
            if (typeof(T) == typeof(VisorRTManager))
                return GlobalRegistry.VisorRT as T;
            if (typeof(T) == typeof(CameraRTManager))
                return GlobalRegistry.CameraRT as T;
            if (typeof(T) == typeof(PostFXRTManager))
                return GlobalRegistry.PostFXRT as T;
            if (typeof(T) == typeof(UIRTManager))
                return GlobalRegistry.UIRT as T;
            if (typeof(T) == typeof(SceneInstantiationGate))
                return SceneInstantiationGate.Instance as T;

            return null;
        }
    }
}
