#if UNITY_2021_3_OR_NEWER
using System;
using Hecton8.Core;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace Hecton8.World
{
    [DisallowMultipleComponent]
    public sealed class WreckageScatterManager : MonoBehaviour, ILateFrameTickable, IGlobalRegistryHotSwapListener
    {
        [SerializeField] private MeshRenderer[] debrisRenderers = Array.Empty<MeshRenderer>();
        [SerializeField] private float authoredQualityWeight = 0.72f;
        [SerializeField] private float shadowSurvivalFloor01 = 0.08f;
        [SerializeField] private float twoSidedShadowWeight01 = 0.86f;
        [SerializeField] private uint prefabHash;
        private byte _presentationPending;
        private byte _registeredLateFrameTick;
        private byte _registeredHotSwapListener;

        public uint PrefabHash => prefabHash;
        public int SerializedRendererCount => debrisRenderers != null ? debrisRenderers.Length : 0;
        public float AuthoredQualityWeight => authoredQualityWeight;

        private void OnEnable()
        {
            _presentationPending = 1;
            TryRegisterHotSwapListener();
            TryRegisterLateFrameTickable();
            if (!Application.isPlaying && _registeredLateFrameTick == 0)
                FlushPendingPresentation();
        }

        private void Start()
        {
            if (_presentationPending != 0 && _registeredLateFrameTick == 0)
                TryRegisterLateFrameTickable();
        }

        private void OnDisable()
        {
            _presentationPending = 0;
            TryUnregisterLateFrameTickable();
            TryUnregisterHotSwapListener();
        }

        public void LateFrameTick()
        {
            FlushPendingPresentation();
            TryUnregisterLateFrameTickable();
        }

        public bool TryReadPrefabHash(out uint hash)
        {
            hash = prefabHash;
            return hash != 0u;
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot != GlobalRegistryServiceSlot.Dispatcher ||
                currentService == null ||
                _presentationPending == 0 ||
                !isActiveAndEnabled)
            {
                return;
            }

            _registeredLateFrameTick = 0;
            TryRegisterLateFrameTickable();
        }

#if UNITY_EDITOR
        public void SetEditorBakeData(
            MeshRenderer[] authoredDebrisRenderers,
            float authoredGlobalQualityWeight,
            float authoredShadowSurvivalFloor01,
            float authoredTwoSidedShadowWeight01,
            uint authoredPrefabHash)
        {
            debrisRenderers = authoredDebrisRenderers ?? Array.Empty<MeshRenderer>();
            authoredQualityWeight = SaturateFinite(authoredGlobalQualityWeight, 0.72f);
            shadowSurvivalFloor01 = SaturateFinite(authoredShadowSurvivalFloor01, 0.08f);
            twoSidedShadowWeight01 = Mathf.Clamp(
                SaturateFinite(authoredTwoSidedShadowWeight01, 0.86f),
                shadowSurvivalFloor01 + 0.01f,
                1f);
            prefabHash = authoredPrefabHash;
        }
#endif

        private void ApplyColdPresentationQuality()
        {
            MeshRenderer[] renderers = debrisRenderers;
            if (renderers == null || renderers.Length == 0)
                return;

            float quality = SaturateFinite(HomeostasisBrain.GlobalQualityWeight, authoredQualityWeight);
            float shadowWeight = Smooth01(quality);
            ShadowCastingMode shadowMode = ResolveShadowMode(shadowWeight);
            bool receiveShadows = shadowWeight > shadowSurvivalFloor01;
            for (int i = 0; i < renderers.Length; i++)
            {
                MeshRenderer renderer = renderers[i];
                if (renderer == null)
                    continue;

                renderer.shadowCastingMode = shadowMode;
                renderer.receiveShadows = receiveShadows;
            }
        }

        private void FlushPendingPresentation()
        {
            if (_presentationPending == 0)
                return;

            _presentationPending = 0;
            ApplyColdPresentationQuality();
        }

        private void TryRegisterLateFrameTickable()
        {
            if (_registeredLateFrameTick != 0 || !Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            _registeredLateFrameTick = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Environment) ? (byte)1 : (byte)0;
        }

        private void TryUnregisterLateFrameTickable()
        {
            if (_registeredLateFrameTick == 0)
                return;

            GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Environment);
            _registeredLateFrameTick = 0;
        }

        private void TryRegisterHotSwapListener()
        {
            if (_registeredHotSwapListener != 0 || !Application.isPlaying)
                return;

            _registeredHotSwapListener = GlobalRegistry.TryRegisterHotSwapListener(this) ? (byte)1 : (byte)0;
        }

        private void TryUnregisterHotSwapListener()
        {
            if (_registeredHotSwapListener == 0)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _registeredHotSwapListener = 0;
        }

        private ShadowCastingMode ResolveShadowMode(float shadowWeight)
        {
            if (shadowWeight <= shadowSurvivalFloor01)
                return ShadowCastingMode.Off;

            if (shadowWeight >= twoSidedShadowWeight01)
                return ShadowCastingMode.TwoSided;

            return ShadowCastingMode.On;
        }

        private static float Smooth01(float value)
        {
            float q = SaturateFinite(value, 0f);
            return q * q * (3f - 2f * q);
        }

        private static float SaturateFinite(float value, float fallback)
        {
            return math.saturate(math.isfinite(value) ? value : fallback);
        }
    }
}
#endif
