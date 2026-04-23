using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.World
{
    internal struct AcousticOcclusionResult
    {
        public float Transmission01;
        public float LowPassCutoffHz;
        public int HitCount;

        public AcousticOcclusionResult(float transmission01, float lowPassCutoffHz, int hitCount)
        {
            Transmission01 = transmission01;
            LowPassCutoffHz = lowPassCutoffHz;
            HitCount = hitCount;
        }
    }

    /// <summary>
    /// Shared zero-GC acoustic occlusion evaluation for sonar, hearing, and world-geometry filtering.
    /// </summary>
    internal static class AcousticOcclusionUtility
    {
        public const float OpenLowPassCutoffHertz = 22000f;
        public const float MinimumLowPassCutoffHertz = 80f;
        public const float DeepShadowTransmissionThreshold = 0.15f;

        private const float DefaultAbsorption01 = 0.50f;
        private const float RockAbsorption01 = 0.98f;
        private const float MetalAbsorption01 = 0.85f;
        private const float SedimentAbsorption01 = 0.60f;
        private const float WaterAbsorption01 = 0.05f;

        private static readonly int PlayerLayer = LayerMask.NameToLayer("Player");
        private static readonly int TriggerZoneLayer = LayerMask.NameToLayer("TriggerZone");
        private static readonly int TransparentFxLayer = LayerMask.NameToLayer("TransparentFX");
        private static readonly int FirstPersonToolsLayer = LayerMask.NameToLayer("FirstPersonTools");
        private static readonly int VoxelCaveLayer = LayerMask.NameToLayer("VoxelCave");
        private static readonly int BaseModuleLayer = LayerMask.NameToLayer("BaseModule");
        private static readonly int VehicleLayer = LayerMask.NameToLayer("Vehicle");
        private static readonly int WaterLayer = LayerMask.NameToLayer("Water");

        public static int BuildSensoryMask()
        {
            int mask = UnityEngine.Physics.DefaultRaycastLayers;
            mask &= ~LayerBit(PlayerLayer);
            mask &= ~LayerBit(TriggerZoneLayer);
            mask &= ~LayerBit(TransparentFxLayer);
            mask &= ~LayerBit(FirstPersonToolsLayer);
            return mask;
        }

        public static AcousticOcclusionResult EvaluateOcclusionPath(
            Vector3 sourcePosition,
            Vector3 listenerPosition,
            int layerMask,
            RaycastHit[] hitBuffer,
            Transform ignoreOriginRoot,
            Transform ignoreTargetRoot)
        {
            if (hitBuffer == null || hitBuffer.Length == 0)
            {
                return new AcousticOcclusionResult(
                    1f,
                    OpenLowPassCutoffHertz,
                    0);
            }

            Vector3 delta = listenerPosition - sourcePosition;
            float distance = delta.magnitude;
            if (distance <= 0.001f)
            {
                return new AcousticOcclusionResult(
                    1f,
                    OpenLowPassCutoffHertz,
                    0);
            }

            Vector3 direction = delta / distance;
            int hitCount = UnityEngine.Physics.RaycastNonAlloc(
                sourcePosition,
                direction,
                hitBuffer,
                distance,
                layerMask,
                QueryTriggerInteraction.Ignore);
            if (hitCount <= 0)
            {
                return new AcousticOcclusionResult(
                    1f,
                    OpenLowPassCutoffHertz,
                    0);
            }

            float transmission01 = 1f;
            int occludingHitCount = 0;
            for (int i = 0; i < hitCount; i++)
            {
                Collider collider = hitBuffer[i].collider;
                if (collider == null || ShouldIgnoreCollider(collider.transform, ignoreOriginRoot, ignoreTargetRoot))
                    continue;

                float absorption01 = ResolveAbsorption01(collider);
                transmission01 *= 1f - math.clamp(absorption01, 0f, 1f);
                occludingHitCount++;
            }

            if (occludingHitCount <= 0)
            {
                return new AcousticOcclusionResult(
                    1f,
                    OpenLowPassCutoffHertz,
                    0);
            }

            float lowPassCutoffHz = math.max(
                MinimumLowPassCutoffHertz,
                OpenLowPassCutoffHertz / math.pow(2f, occludingHitCount));

            return new AcousticOcclusionResult(
                math.clamp(transmission01, 0f, 1f),
                math.clamp(lowPassCutoffHz, MinimumLowPassCutoffHertz, OpenLowPassCutoffHertz),
                occludingHitCount);
        }

        private static bool ShouldIgnoreCollider(Transform hitTransform, Transform ignoreOriginRoot, Transform ignoreTargetRoot)
        {
            if (hitTransform == null)
                return true;

            Transform root = hitTransform.root;
            if (ignoreOriginRoot != null && root == ignoreOriginRoot)
                return true;

            return ignoreTargetRoot != null && root == ignoreTargetRoot;
        }

        private static float ResolveAbsorption01(Collider collider)
        {
            if (collider == null)
                return DefaultAbsorption01;

            if (collider.CompareTag("MetalFloor") ||
                collider.CompareTag("Grate") ||
                collider.CompareTag("BaseModule") ||
                collider.CompareTag("Vehicle"))
            {
                return MetalAbsorption01;
            }

            if (collider.CompareTag("Sand") || collider.CompareTag("Wet"))
                return SedimentAbsorption01;

            if (collider.CompareTag("Rock"))
                return RockAbsorption01;

            int layer = collider.gameObject.layer;
            if (layer == WaterLayer)
                return WaterAbsorption01;

            if (layer == BaseModuleLayer || layer == VehicleLayer)
                return MetalAbsorption01;

            if (layer == VoxelCaveLayer)
                return RockAbsorption01;

            return DefaultAbsorption01;
        }

        private static int LayerBit(int layer)
        {
            return layer >= 0 ? 1 << layer : 0;
        }
    }
}
