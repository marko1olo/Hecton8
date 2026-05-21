using UnityEngine;

namespace Hecton8.Core
{
    public interface IOceanVisualBridge : ISystem
    {
        Material OceanMaterial { get; }

        int CameraColorTextureId { get; }

        bool HasUnderwaterInstance { get; }

        bool HasUnderwaterPass(Camera camera);

        Component TryGetUnderwaterPass(Camera camera);

        Component EnsureUnderwaterPass(Camera camera);

        bool IsUnderwaterPassEnabled(Component underwaterPass);

        void SetUnderwaterPassEnabled(Component underwaterPass, bool enabled);

        bool IsUnderwaterPassActive(Component underwaterPass);

        void SetCopyOceanMaterialParamsEachFrame(Component underwaterPass, bool enabled);

        void CopyUnderwaterPassSettings(Component source, Component target);

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
