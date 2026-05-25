using Hecton8.Gameplay;
using UnityEngine;

namespace Hecton8.Visor
{
    /// <summary>
    /// Legacy facade kept so old scenes do not fail script resolution.
    /// Runtime flashlight presentation is now published by ModularEquipmentEngine from Vault state.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerFlashlight))]
    public sealed class HectonFlashlightVoxelShadowProvider : MonoBehaviour
    {
        private const bool RuntimeVoxelShadowProviderEnabled = false;

        private static readonly int _FlashlightActiveId = Shader.PropertyToID("_HectonFlashlightActive");
        private static readonly int _FlashlightVoxelActiveId = Shader.PropertyToID("_HectonFlashlightVoxelActive");
        private static readonly int _FlashlightPositionWsId = Shader.PropertyToID("_HectonFlashlightPositionWS");
        private static readonly int _FlashlightDirectionWsId = Shader.PropertyToID("_HectonFlashlightDirectionWS");
        private static readonly int _FlashlightColorId = Shader.PropertyToID("_HectonFlashlightColor");
        private static readonly int _FlashlightConeDataId = Shader.PropertyToID("_HectonFlashlightConeData");
        private static readonly int _FlashlightVoxelWorldToLocalId = Shader.PropertyToID("_HectonFlashlightVoxelWorldToLocal");
        private static readonly int _FlashlightVoxelHalfExtentsId = Shader.PropertyToID("_HectonFlashlightVoxelHalfExtents");

        private void Awake()
        {
            PublishInactiveGlobals();
        }

        private void OnEnable()
        {
            PublishInactiveGlobals();
        }

        private void OnDisable()
        {
            PublishInactiveGlobals();
        }

        private void OnDestroy()
        {
            PublishInactiveGlobals();
        }

        public static bool IsRuntimeEnabled()
        {
            return RuntimeVoxelShadowProviderEnabled;
        }

        public static void PublishInactiveGlobals()
        {
            Shader.SetGlobalFloat(_FlashlightActiveId, 0f);
            Shader.SetGlobalFloat(_FlashlightVoxelActiveId, 0f);
            Shader.SetGlobalVector(_FlashlightPositionWsId, Vector4.zero);
            Shader.SetGlobalVector(_FlashlightDirectionWsId, Vector4.zero);
            Shader.SetGlobalVector(_FlashlightColorId, Vector4.zero);
            Shader.SetGlobalVector(_FlashlightConeDataId, Vector4.zero);
            Shader.SetGlobalVector(_FlashlightVoxelHalfExtentsId, Vector4.zero);
            Shader.SetGlobalMatrix(_FlashlightVoxelWorldToLocalId, Matrix4x4.identity);
        }
    }
}
