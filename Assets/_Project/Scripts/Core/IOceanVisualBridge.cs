using UnityEngine;

namespace Hecton8.Core
{
    public interface IOceanVisualBridge : ISystem
    {
        Material OceanMaterial { get; }

        bool HasUnderwaterInstance { get; }

        bool HasUnderwaterRenderer(Camera camera);

        Component TryGetUnderwaterRenderer(Camera camera);

        Component EnsureUnderwaterRenderer(Camera camera);

        bool IsUnderwaterRendererEnabled(Component renderer);

        void SetUnderwaterRendererEnabled(Component renderer, bool enabled);

        bool IsUnderwaterRendererActive(Component renderer);

        void SetCopyOceanMaterialParamsEachFrame(Component renderer, bool enabled);

        void CopyUnderwaterRendererSettings(Component source, Component target);

        bool IsOceanCameraOwnedBy(Camera camera);

        void AssignOceanCamera(Camera camera);

        void ApplyUnderwaterGlobals(
            Material targetMaterial,
            Vector3 depthFogDensity,
            Color diffuse,
            Color diffuseGrazing,
            Color diffuseShadow,
            float subSurfaceSun,
            float subSurfaceBase,
            float subSurfaceSunFalloff);
    }

    public static class OceanVisualBridgeRegistry
    {
        private static IOceanVisualBridge _active;

        public static IOceanVisualBridge Active => _active;

        public static void Register(IOceanVisualBridge bridge)
        {
            if (bridge == null)
                return;

            _active = bridge;
        }

        public static void Unregister(IOceanVisualBridge bridge)
        {
            if (!ReferenceEquals(_active, bridge))
                return;

            _active = null;
        }
    }
}
