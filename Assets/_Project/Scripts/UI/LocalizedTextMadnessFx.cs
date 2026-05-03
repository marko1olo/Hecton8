using Hecton8.Core;
using TMPro;
using UnityEngine;

namespace Hecton8.UI
{
    /// <summary>
    /// Applies a local glitch tint/offset pass to a TMP UGUI label during madness windows.
    /// CanvasRenderer does not expose MaterialPropertyBlock, so this owner maintains a per-label material instance.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LocalizedTextMadnessFx : MonoBehaviour, ITickable, IUpdatable
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

        private TextMeshProUGUI _target;
        private Material _materialInstance;
        private Material _sourceMaterial;
        private bool _registered;
        private bool _effectActive;
        private float _waveTime;

        /// <summary>
        /// Bind the effect owner to a TMP UGUI target.
        /// </summary>
        public void Bind(TextMeshProUGUI target)
        {
            if (ReferenceEquals(_target, target))
                return;

            _target = target;
            EnsureMaterialInstance();
            ApplyIdleState();
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
                RegisterToTickManager();
                ApplyActiveState(0f);
                return;
            }

            ApplyIdleState();
            UnregisterFromTickManager();
        }

        private void OnDisable()
        {
            UnregisterFromTickManager();
            ReleaseMaterialInstance();
        }

        private void OnDestroy()
        {
            UnregisterFromTickManager();
            ReleaseMaterialInstance();
        }

        /// <inheritdoc />
        public void Tick(float deltaTime)
        {
            if (!_effectActive || _materialInstance == null || _target == null)
            {
                UnregisterFromTickManager();
                return;
            }

            _waveTime += deltaTime;
            float phase = Mathf.Sin(_waveTime * OffsetFrequency);
            ApplyActiveState(phase);
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

            _materialInstance = new Material(baseMaterial); // COLD ALLOC: Material[1] — per-label TMP madness effect material — owner: LocalizedTextMadnessFx
            _materialInstance.name = string.Concat(baseMaterial.name, " (MadnessFx)");
            _target.fontSharedMaterial = _materialInstance;
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
        }

        private void ApplyActiveState(float phase)
        {
            if (_materialInstance == null)
                return;

            float offsetX = phase * OffsetAmplitude;
            float offsetY = Mathf.Cos(_waveTime * (OffsetFrequency * 0.61f)) * (OffsetAmplitude * 0.35f);
            float glowOuter = BaseGlowOuter + Mathf.Abs(phase) * 0.06f;
            float glowInner = BaseGlowInner + Mathf.Abs(phase) * 0.015f;

            _materialInstance.SetColor(UnderlayColorId, MadnessUnderlayColor);
            _materialInstance.SetFloat(UnderlayOffsetXId, offsetX);
            _materialInstance.SetFloat(UnderlayOffsetYId, offsetY);
            _materialInstance.SetFloat(UnderlaySoftnessId, BaseUnderlaySoftness);
            _materialInstance.SetColor(GlowColorId, MadnessGlowColor);
            _materialInstance.SetFloat(GlowOuterId, glowOuter);
            _materialInstance.SetFloat(GlowInnerId, glowInner);
            _target.UpdateMeshPadding();
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
        }

        private void RegisterToTickManager()
        {
            if (_registered || !Application.isPlaying)
                return;

            if (GlobalRegistry.Dispatcher == null)
                return;

            GlobalRegistry.RegisterUpdatable(this, PriorityLayer.UI);
            _registered = GlobalRegistry.Updatables.Contains(this);
        }

        private void UnregisterFromTickManager()
        {
            if (!_registered)
                return;

            GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.UI);
            _registered = false;
        }
    }
}
