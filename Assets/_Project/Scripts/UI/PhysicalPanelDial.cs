using Hecton8.Core;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.UI
{
    /// <summary>
    /// Diegetic scroll-to-knob bridge for physical terminal dials.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/UI/Physical Panel Dial")]
    public sealed class PhysicalPanelDial : MonoBehaviour, IPanelInteractable, IGlobalRegistryHotSwapListener
    {
        private const float MinimumAxisLengthSq = 0.0001f;
        private const float MinimumScrollSq = 0.000001f;
        private const float RadiansPerDegree = 0.0174532924f;
        private const float HalfPi = 1.57079637f;
        private const float Pi = 3.14159274f;
        private const float TwoPi = 6.28318548f;
        private const float InvTwoPi = 0.159154943f;

        [SerializeField, Tooltip("Stable panel id accepted by this dial.")]
        private int panelId = 1;

        [SerializeField, Tooltip("Canvas-space center of the dial hot zone.")]
        private Vector2 dialCenter = new Vector2(256f, 128f);

        [SerializeField, Tooltip("Canvas-space half extents of the dial hot zone.")]
        private Vector2 dialHalfExtents = new Vector2(32f, 32f);

        [SerializeField, Tooltip("Optional transform rotated by this dial. Defaults to this transform.")]
        private Transform knobTransform;

        [SerializeField, Tooltip("Local-space rotation axis for the physical knob mesh.")]
        private Vector3 localRotationAxis = Vector3.forward;

        [SerializeField, Tooltip("Degrees applied per native scroll-wheel unit.")]
        private float degreesPerScrollUnit = 0.15f;

        [SerializeField, Tooltip("Minimum clamped dial angle in degrees.")]
        private float minimumDegrees = -135f;

        [SerializeField, Tooltip("Maximum clamped dial angle in degrees.")]
        private float maximumDegrees = 135f;

        [SerializeField, Tooltip("Routes scroll ticks into the central NativeQueue-backed audio drain when an event id is authored.")]
        private bool emitScrollAudio = true;

        [SerializeField, Tooltip("One-based authored audio event id for mechanical dial ticks. Zero disables audio.")]
        private uint scrollAudioEventId;

        [SerializeField, Range(0f, 1f), Tooltip("Linear volume for mechanical dial ticks.")]
        private float scrollAudioVolume = 0.22f;

        [SerializeField, Range(0.25f, 2.5f), Tooltip("Pitch for mechanical dial ticks.")]
        private float scrollAudioPitch = 1f;

        private Quaternion _baseLocalRotation;
        private float _currentDegrees;
        private IAudioService _cachedAudioService;
        private bool _baseRotationCached;
        private bool _hotSwapListenerRegistered;

        /// <summary>Current clamped dial angle in degrees.</summary>
        public float CurrentDegrees => _currentDegrees;

        private void Awake()
        {
            CacheBaseRotation();
        }

        private void OnEnable()
        {
            CacheBaseRotation();
            _cachedAudioService = Hecton8.Audio.SpatialAudioManager.ActiveRuntimeInstance;
            TryRegisterHotSwapListener();
        }

        private void OnDisable()
        {
            TryUnregisterHotSwapListener();
        }

        private void OnDestroy()
        {
            TryUnregisterHotSwapListener();
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot == GlobalRegistryServiceSlot.Audio)
                _cachedAudioService = currentService as IAudioService;
        }

        /// <inheritdoc />
        public void ReceiveCanvasInput(in DiegeticPanelInputEvent inputEvent)
        {
            if (inputEvent.PanelId != panelId ||
                (inputEvent.EventType & DiegeticPanelInputEventType.Scroll) == 0 ||
                !IsInsideDialHotZone(inputEvent.CanvasHitPoint) ||
                math.lengthsq(inputEvent.AnalogDelta) <= MinimumScrollSq)
            {
                return;
            }

            float scrollY = inputEvent.AnalogDelta.y;
            if (!math.isfinite(scrollY) || math.abs(scrollY) <= 0.0001f)
                return;

            CacheBaseRotation();
            _currentDegrees = math.clamp(
                _currentDegrees + scrollY * degreesPerScrollUnit,
                minimumDegrees,
                maximumDegrees);
            ApplyRotation();
            QueueScrollAudio();
        }

        public void SetAngleDegrees(float degrees)
        {
            CacheBaseRotation();
            _currentDegrees = math.clamp(
                math.isfinite(degrees) ? degrees : 0f,
                minimumDegrees,
                maximumDegrees);
            ApplyRotation();
        }

        private bool IsInsideDialHotZone(float2 canvasPosition)
        {
            float2 center = new float2(dialCenter.x, dialCenter.y);
            float2 extents = math.max(new float2(0.5f, 0.5f), new float2(dialHalfExtents.x, dialHalfExtents.y));
            float2 delta = math.abs(canvasPosition - center);
            return delta.x <= extents.x && delta.y <= extents.y;
        }

        private void CacheBaseRotation()
        {
            if (_baseRotationCached)
                return;

            Transform target = knobTransform != null ? knobTransform : transform;
            _baseLocalRotation = target.localRotation;
            _baseRotationCached = true;
        }

        private void ApplyRotation()
        {
            Transform target = knobTransform != null ? knobTransform : transform;
            float3 axis = new float3(localRotationAxis.x, localRotationAxis.y, localRotationAxis.z);
            float axisLengthSq = math.lengthsq(axis);
            if (!math.isfinite(axisLengthSq) || axisLengthSq <= MinimumAxisLengthSq)
                axis = new float3(0f, 0f, 1f);
            else
                axis *= math.rsqrt(axisLengthSq);

            Quaternion unityDelta = ApproximateAxisRotationNoTrig(axis, _currentDegrees * RadiansPerDegree);
            target.localRotation = _baseLocalRotation * unityDelta;
        }

        private static Quaternion ApproximateAxisRotationNoTrig(float3 axis, float radians)
        {
            float3 safeAxis = NormalizeVectorApproxNoSqrt(axis, new float3(0f, 0f, 1f));
            ApproximateSinCosFullNoTrig(radians * 0.5f, out float sinHalf, out float cosHalf);
            Quaternion rotation = new Quaternion(
                safeAxis.x * sinHalf,
                safeAxis.y * sinHalf,
                safeAxis.z * sinHalf,
                cosHalf);
            return NormalizeQuaternionNoSqrt(rotation);
        }

        private static void ApproximateSinCosFullNoTrig(float radians, out float sin, out float cos)
        {
            float x = radians - (TwoPi * math.round(radians * InvTwoPi));
            float cosSign = 1f;
            if (x > HalfPi)
            {
                x = Pi - x;
                cosSign = -1f;
            }
            else if (x < -HalfPi)
            {
                x = -Pi - x;
                cosSign = -1f;
            }

            float x2 = x * x;
            sin = x * (1f - (x2 * (0.16666667f - (x2 * 0.008333333f))));
            cos = cosSign * (1f - (x2 * (0.5f - (x2 * 0.041666667f))));
        }

        private static float3 NormalizeVectorApproxNoSqrt(float3 value, float3 fallback)
        {
            if (!math.all(math.isfinite(value)))
                value = fallback;

            float lengthSq = math.lengthsq(value);
            if (lengthSq <= 0.000001f)
                return fallback;

            float3 absValue = math.abs(value);
            float largest = math.max(absValue.x, math.max(absValue.y, absValue.z));
            float smallest = math.min(absValue.x, math.min(absValue.y, absValue.z));
            float middle = absValue.x + absValue.y + absValue.z - largest - smallest;
            float magnitude = largest + (middle * 0.375f) + (smallest * 0.125f);
            return value * math.rcp(math.max(magnitude, 0.000001f));
        }

        private static Quaternion NormalizeQuaternionNoSqrt(Quaternion value)
        {
            float4 v = new float4(value.x, value.y, value.z, value.w);
            float4 absValue = math.abs(v);
            float largest = math.max(math.max(absValue.x, absValue.y), math.max(absValue.z, absValue.w));
            float smallest = math.min(math.min(absValue.x, absValue.y), math.min(absValue.z, absValue.w));
            float middleSum = absValue.x + absValue.y + absValue.z + absValue.w - largest - smallest;
            float magnitude = largest + (middleSum * 0.25f) + (smallest * 0.125f);
            v *= math.rcp(math.max(magnitude, 0.000001f));
            return new Quaternion(v.x, v.y, v.z, v.w);
        }

        private void QueueScrollAudio()
        {
            IAudioService audio = _cachedAudioService;
            if (!emitScrollAudio || scrollAudioEventId == 0u || audio == null || !audio.IsInitialized)
                return;

            Vector3 sourcePosition = (knobTransform != null ? knobTransform : transform).position;
            if (!math.all(math.isfinite((float3)sourcePosition)))
                return;

            AudioEvent audioEvent = new AudioEvent(
                scrollAudioEventId,
                sourcePosition,
                math.saturate(scrollAudioVolume),
                math.clamp(scrollAudioPitch, 0.25f, 2.5f));
            audio.QueueAudioEvent(in audioEvent);
        }

        private void TryRegisterHotSwapListener()
        {
            if (_hotSwapListenerRegistered || !Application.isPlaying)
                return;

            _hotSwapListenerRegistered = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterHotSwapListener()
        {
            if (!_hotSwapListenerRegistered)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _hotSwapListenerRegistered = false;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            dialHalfExtents.x = math.max(0.5f, dialHalfExtents.x);
            dialHalfExtents.y = math.max(0.5f, dialHalfExtents.y);
            if (maximumDegrees < minimumDegrees)
                maximumDegrees = minimumDegrees;
            degreesPerScrollUnit = math.clamp(
                math.isfinite(degreesPerScrollUnit) ? degreesPerScrollUnit : 0.15f,
                -10f,
                10f);
            scrollAudioVolume = math.saturate(scrollAudioVolume);
            scrollAudioPitch = math.clamp(scrollAudioPitch, 0.25f, 2.5f);
        }
#endif
    }
}
