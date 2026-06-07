using Hecton8.Core;
using TMPro;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.UI
{
    /// <summary>
    /// Applies a local, material-stable tint pulse to a TMP UGUI label during madness windows.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LocalizedTextMadnessFx : MonoBehaviour, ILateFrameTickable, IGlobalRegistryHotSwapListener
    {
        private static readonly Color MadnessColdColor = new Color(0.34f, 0.92f, 1f, 0.84f);
        private static readonly Color MadnessHotColor = new Color(1f, 0.28f, 0.42f, 0.92f);

        private const float OffsetFrequency = 7.5f;
        private const float InvTwoPi = 0.15915494309f;
        private const float BaseMix = 0.18f;
        private const float PulseMix = 0.26f;

        private TextMeshProUGUI _target;
        private Color _idleColor;
        private bool _idleColorCaptured;
        private bool _registered;
        private bool _hotSwapRegistered;
        private bool _effectActive;
        private float _waveTime;

        private void OnEnable()
        {
            TryRegisterHotSwapListener();
            if (_effectActive)
                RegisterToTickManager();
        }

        public void Bind(TextMeshProUGUI target)
        {
            if (ReferenceEquals(_target, target))
                return;

            if (_target != null)
                ApplyIdleState();

            _target = target;
            CaptureIdleColor();

            if (_effectActive)
            {
                RegisterToTickManager();
                ApplyActiveState(0f);
            }
            else
            {
                ApplyIdleState();
            }
        }

        public void SetEffectActive(bool active)
        {
            if (_effectActive == active)
                return;

            if (active)
                CaptureIdleColor();

            _effectActive = active;
            _waveTime = 0f;

            if (_effectActive)
            {
                RegisterToTickManager();
                ApplyActiveState(0f);
                return;
            }

            ApplyIdleState();
            UnregisterFromTickManager();
        }

        private void OnDisable()
        {
            ApplyIdleState();
            UnregisterFromTickManager();
            TryUnregisterHotSwapListener();
        }

        private void OnDestroy()
        {
            ApplyIdleState();
            UnregisterFromTickManager();
            TryUnregisterHotSwapListener();
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot != GlobalRegistryServiceSlot.Dispatcher)
                return;

            UnregisterFromTickManager();
            if (currentService != null && isActiveAndEnabled && _effectActive)
                RegisterToTickManager();
        }

        public void LateFrameTick()
        {
            if (!_effectActive)
            {
                UnregisterFromTickManager();
                return;
            }

            if (_target == null)
            {
                UnregisterFromTickManager();
                return;
            }

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

        private static Color LerpColor(Color from, Color to, float t)
        {
            float clampedT = math.saturate(t);
            return new Color(
                math.lerp(from.r, to.r, clampedT),
                math.lerp(from.g, to.g, clampedT),
                math.lerp(from.b, to.b, clampedT),
                math.lerp(from.a, to.a, clampedT));
        }

        private void CaptureIdleColor()
        {
            if (_target == null)
                return;

            _idleColor = _target.color;
            _idleColorCaptured = true;
        }

        private void ApplyIdleState()
        {
            if (_target == null || !_idleColorCaptured)
                return;

            _target.color = _idleColor;
        }

        private void ApplyActiveState(float phase)
        {
            if (_target == null)
                return;

            if (!_idleColorCaptured)
                CaptureIdleColor();

            float phaseAbs = math.abs(phase);
            Color pulseColor = LerpColor(MadnessColdColor, MadnessHotColor, phaseAbs);
            _target.color = LerpColor(_idleColor, pulseColor, BaseMix + phaseAbs * PulseMix);
        }

        private void RegisterToTickManager()
        {
            if (_registered || !Application.isPlaying || !isActiveAndEnabled)
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
