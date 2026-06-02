using Hecton8.Core;
using TMPro;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.UI
{
    /// <summary>
    /// Applies a local glitch tint/offset pass to a TMP UGUI label during madness windows.
    /// CanvasRenderer does not expose MaterialPropertyBlock, so this owner maintains a per-label material instance.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LocalizedTextMadnessFx : MonoBehaviour, ILateFrameTickable, IGlobalRegistryHotSwapListener
    {
        private static readonly int UnderlayColorId = Shader.PropertyToID("_UnderlayColor");
        private static readonly int UnderlayOffsetXId = Shader.PropertyToID("_UnderlayOffsetX");
        private static readonly int UnderlayOffsetYId = Shader.PropertyToID("_UnderlayOffsetY");
        private static readonly int UnderlaySoftnessId = Shader.PropertyToID("_UnderlaySoftness");
        private static readonly int GlowColorId = Shader.PropertyToID("_GlowColor");
        private static readonly int GlowOuterId = Shader.PropertyToID("_GlowOuter");
        private static readonly int GlowInnerId = Shader.PropertyToID("_GlowInner");

        private static readonly Color MadnessUnderlayColor = new Color(1f, 0.28f, 0.42f, 0.3f);
        private static readonly Color MadnessGlowColor = new Color(0.34f, 0.92f, 1f, 0.18f);

        private const float OffsetAmplitude = 0.11f;
        private const float OffsetFrequency = 7.5f;
        private const float BaseUnderlaySoftness = 0.28f;
        private const float BaseGlowOuter = 0.18f;
        private const float BaseGlowInner = 0.04f;
        private const float InvTwoPi = 0.15915494309f;
        private const float HalfPi = 1.57079632679f;
        private const string RuntimeMaterialName = "MAT_TMP_MadnessFx";

        private TextMeshProUGUI _target;
        private Material _materialInstance;
        private Material _sourceMaterial;
        private bool _registered;
        private bool _hotSwapRegistered;
        private bool _effectActive;
        private bool _activePaddingPrimed;
        private float _waveTime;

        private void OnEnable()
        {
            TryRegisterHotSwapListener();
            RegisterToTickManager();
        }

        /// <summary>
        /// Bind the effect owner to a TMP UGUI target.
        /// </summary>
        public void Bind(TextMeshProUGUI target)
        {
            if (ReferenceEquals(_target, target))
                return;

            _target = target;
            EnsureMaterialInstance();
            if (_effectActive)
            {
                PrimeActiveMeshPadding();
                ApplyActiveState(0f);
            }
            else
            {
                ApplyIdleState();
            }
        }

        /// <summary>
        /// Enable or disable the localized madness pass.
        /// </summary>
        public void SetEffectActive(bool active)
        {
            if (_effectActive == active)
                return;

            _effectActive = active;
            _waveTime = 0f;

            if (_effectActive)
            {
                EnsureMaterialInstance();
                PrimeActiveMeshPadding();
                ApplyActiveState(0f);
                return;
            }

            ApplyIdleState();
        }

        private void OnDisable()
        {
            UnregisterFromTickManager();
            TryUnregisterHotSwapListener();
            ReleaseMaterialInstance();
        }

        private void OnDestroy()
        {
            UnregisterFromTickManager();
            TryUnregisterHotSwapListener();
            ReleaseMaterialInstance();
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot != GlobalRegistryServiceSlot.Dispatcher)
                return;

            if (currentService == null)
            {
                _registered = false;
                return;
            }

            if (isActiveAndEnabled)
            {
                UnregisterFromTickManager();
                RegisterToTickManager();
            }
        }

        /// <inheritdoc />
        public void LateFrameTick()
        {
            if (!_effectActive || _materialInstance == null || _target == null)
                return;

            float deltaTime = math.max(0f, SystemDispatcher.CurrentFrameDeltaTime);
            _waveTime += deltaTime;
            float phase = EvaluateCheapWaveSigned(_waveTime * OffsetFrequency);
            ApplyActiveState(phase);
        }

        private static float EvaluateCheapWaveSigned(float phaseRadians)
        {
            float phase01 = math.frac((phaseRadians * InvTwoPi) + 0.25f);
            float triangle = 1f - math.abs(phase01 * 2f - 1f);
            return (triangle * 2f) - 1f;
        }

        private void EnsureMaterialInstance()
        {
            if (_target == null)
                return;

            Material currentMaterial = _target.fontSharedMaterial;
            Material baseMaterial =
                ReferenceEquals(currentMaterial, _materialInstance) && _sourceMaterial != null
                    ? _sourceMaterial
                    : currentMaterial;
            if (baseMaterial == null)
                return;

            if (_materialInstance != null && ReferenceEquals(_sourceMaterial, baseMaterial))
            {
                if (!ReferenceEquals(_target.fontSharedMaterial, _materialInstance))
                    _target.fontSharedMaterial = _materialInstance;
                return;
            }

            Material previousSourceMaterial = _sourceMaterial;
            _sourceMaterial = baseMaterial;

            if (_materialInstance != null)
            {
                if (_target != null && previousSourceMaterial != null && ReferenceEquals(_target.fontSharedMaterial, _materialInstance))
                    _target.fontSharedMaterial = previousSourceMaterial;

                Destroy(_materialInstance);
            }

            _materialInstance = new Material(baseMaterial) // COLD ALLOC: Material[1] — per-label TMP madness effect material — owner: LocalizedTextMadnessFx
            {
                name = RuntimeMaterialName,
                hideFlags = HideFlags.DontSave
            };
            _target.fontSharedMaterial = _materialInstance;
            _activePaddingPrimed = false;
        }

        private void ApplyIdleState()
        {
            if (_target == null)
                return;

            EnsureMaterialInstance();
            if (_materialInstance == null)
                return;

            _materialInstance.SetColor(UnderlayColorId, Color.clear);
            _materialInstance.SetFloat(UnderlayOffsetXId, 0f);
            _materialInstance.SetFloat(UnderlayOffsetYId, 0f);
            _materialInstance.SetFloat(UnderlaySoftnessId, 0f);
            _materialInstance.SetColor(GlowColorId, Color.clear);
            _materialInstance.SetFloat(GlowOuterId, 0f);
            _materialInstance.SetFloat(GlowInnerId, 0f);
            _target.UpdateMeshPadding();
            _activePaddingPrimed = false;
        }

        private void ApplyActiveState(float phase)
        {
            if (_materialInstance == null)
                return;

            if (!_activePaddingPrimed)
                PrimeActiveMeshPadding();

            float offsetX = phase * OffsetAmplitude;
            float offsetY = EvaluateCheapWaveSigned((_waveTime * (OffsetFrequency * 0.61f)) + HalfPi) * (OffsetAmplitude * 0.35f);
            float phaseAbs = math.abs(phase);
            float glowOuter = BaseGlowOuter + phaseAbs * 0.06f;
            float glowInner = BaseGlowInner + phaseAbs * 0.015f;

            _materialInstance.SetColor(UnderlayColorId, MadnessUnderlayColor);
            _materialInstance.SetFloat(UnderlayOffsetXId, offsetX);
            _materialInstance.SetFloat(UnderlayOffsetYId, offsetY);
            _materialInstance.SetFloat(UnderlaySoftnessId, BaseUnderlaySoftness);
            _materialInstance.SetColor(GlowColorId, MadnessGlowColor);
            _materialInstance.SetFloat(GlowOuterId, glowOuter);
            _materialInstance.SetFloat(GlowInnerId, glowInner);
        }

        private void PrimeActiveMeshPadding()
        {
            if (_activePaddingPrimed || _materialInstance == null || _target == null)
                return;

            _materialInstance.SetColor(UnderlayColorId, MadnessUnderlayColor);
            _materialInstance.SetFloat(UnderlayOffsetXId, OffsetAmplitude);
            _materialInstance.SetFloat(UnderlayOffsetYId, OffsetAmplitude * 0.35f);
            _materialInstance.SetFloat(UnderlaySoftnessId, BaseUnderlaySoftness);
            _materialInstance.SetColor(GlowColorId, MadnessGlowColor);
            _materialInstance.SetFloat(GlowOuterId, BaseGlowOuter + 0.06f);
            _materialInstance.SetFloat(GlowInnerId, BaseGlowInner + 0.015f);
            _target.UpdateMeshPadding();
            _activePaddingPrimed = true;
        }

        private void ReleaseMaterialInstance()
        {
            if (_materialInstance == null)
                return;

            if (_target != null && _sourceMaterial != null && ReferenceEquals(_target.fontSharedMaterial, _materialInstance))
                _target.fontSharedMaterial = _sourceMaterial;

            if (Application.isPlaying)
                Destroy(_materialInstance);
            else
                DestroyImmediate(_materialInstance);

            _materialInstance = null;
            _activePaddingPrimed = false;
        }

        private void RegisterToTickManager()
        {
            if (_registered || !Application.isPlaying)
                return;

            _registered = SystemDispatcher.Register((ILateFrameTickable)this, PriorityLayer.UI);
        }

        private void UnregisterFromTickManager()
        {
            if (!_registered)
                return;

            SystemDispatcher.UnregisterLateFrameTickableDirect(this, PriorityLayer.UI);
            _registered = false;
        }

        private void TryRegisterHotSwapListener()
        {
            if (_hotSwapRegistered || !Application.isPlaying)
                return;

            _hotSwapRegistered = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterHotSwapListener()
        {
            if (!_hotSwapRegistered)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _hotSwapRegistered = false;
        }
    }
}
