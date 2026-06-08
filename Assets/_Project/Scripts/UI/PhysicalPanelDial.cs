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
        private const float DefaultScrollDegreesPerUnit = 0.15f;
        private const float DefaultMinimumDegrees = -135f;
        private const float DefaultMaximumDegrees = 135f;
        private const float DefaultDialHalfExtent = 32f;
        private const float DefaultAudioPitch = 1f;

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
        private float degreesPerScrollUnit = DefaultScrollDegreesPerUnit;

        [SerializeField, Tooltip("Minimum clamped dial angle in degrees.")]
        private float minimumDegrees = DefaultMinimumDegrees;

        [SerializeField, Tooltip("Maximum clamped dial angle in degrees.")]
        private float maximumDegrees = DefaultMaximumDegrees;

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
            CacheAudioService(GlobalRegistry.Audio);
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
                CacheAudioService(currentService as IAudioService);
        }

        /// <inheritdoc />
        public void ReceiveCanvasInput(in DiegeticPanelInputEvent inputEvent)
        {
            if (inputEvent.PanelId != panelId ||
                (inputEvent.EventType & DiegeticPanelInputEventType.Scroll) == 0 ||
                !IsInsideDialHotZone(inputEvent.CanvasHitPoint) ||
                !math.all(math.isfinite(inputEvent.AnalogDelta)) ||
                math.lengthsq(inputEvent.AnalogDelta) <= MinimumScrollSq)
            {
                return;
            }

            float scrollY = inputEvent.AnalogDelta.y;
            if (!math.isfinite(scrollY) || math.abs(scrollY) <= 0.0001f)
                return;

            float scrollDegrees = scrollY * ResolveSafeScrollScale();
            if (!math.isfinite(scrollDegrees) || math.abs(scrollDegrees) <= 0.000001f)
                return;

            CacheBaseRotation();
            _currentDegrees = ClampDialDegrees(_currentDegrees + scrollDegrees);
            ApplyRotation();
            QueueScrollAudio();
        }

        public void SetAngleDegrees(float degrees)
        {
            CacheBaseRotation();
            _currentDegrees = ClampDialDegrees(degrees);
            ApplyRotation();
        }

        private bool IsInsideDialHotZone(float2 canvasPosition)
        {
            if (!math.all(math.isfinite(canvasPosition)))
                return false;

            float2 center = new float2(
                SanitizeFinite(dialCenter.x, 0f),
                SanitizeFinite(dialCenter.y, 0f));
            float2 extents = math.max(
                new float2(0.5f, 0.5f),
                new float2(
                    SanitizeFinite(dialHalfExtents.x, DefaultDialHalfExtent),
                    SanitizeFinite(dialHalfExtents.y, DefaultDialHalfExtent)));
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
            _currentDegrees = ClampDialDegrees(_currentDegrees);
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
            IAudioService audio = ResolveAudioService();
            if (!emitScrollAudio || scrollAudioEventId == 0u || audio == null)
                return;

            Vector3 sourcePosition = (knobTransform != null ? knobTransform : transform).position;
            if (!math.all(math.isfinite((float3)sourcePosition)))
                return;

            AudioEvent audioEvent = new AudioEvent(
                scrollAudioEventId,
                sourcePosition,
                ResolveSafeAudioVolume(),
                ResolveSafeAudioPitch());
            audio.QueueAudioEvent(in audioEvent);
        }

        private void CacheAudioService(IAudioService audioService)
        {
            _cachedAudioService = IsAudioServiceUsable(audioService) ? audioService : null;
        }

        private IAudioService ResolveAudioService()
        {
            IAudioService audioService = _cachedAudioService;
            if (IsAudioServiceUsable(audioService))
                return audioService;

            _cachedAudioService = null;
            return null;
        }

        private static bool IsAudioServiceUsable(IAudioService audioService)
        {
            if (audioService == null || !audioService.IsAudioRuntimeReady)
                return false;

            if (audioService is Behaviour behaviour)
                return behaviour != null && behaviour.isActiveAndEnabled;

            return true;
        }

        private static float SanitizeFinite(float value, float fallback)
        {
            return math.isfinite(value) ? value : fallback;
        }

        private void ResolveSafeDialBounds(out float minimum, out float maximum)
        {
            minimum = SanitizeFinite(minimumDegrees, DefaultMinimumDegrees);
            maximum = SanitizeFinite(maximumDegrees, DefaultMaximumDegrees);
            if (maximum < minimum)
            {
                float tmp = minimum;
                minimum = maximum;
                maximum = tmp;
            }
        }

        private float ClampDialDegrees(float degrees)
        {
            ResolveSafeDialBounds(out float minimum, out float maximum);
            return math.clamp(SanitizeFinite(degrees, 0f), minimum, maximum);
        }

        private float ResolveSafeScrollScale()
        {
            return math.clamp(
                SanitizeFinite(degreesPerScrollUnit, DefaultScrollDegreesPerUnit),
                -10f,
                10f);
        }

        private float ResolveSafeAudioVolume()
        {
            return math.saturate(SanitizeFinite(scrollAudioVolume, 0f));
        }

        private float ResolveSafeAudioPitch()
        {
            return math.clamp(
                SanitizeFinite(scrollAudioPitch, DefaultAudioPitch),
                0.25f,
                2.5f);
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
            dialCenter.x = SanitizeFinite(dialCenter.x, 0f);
            dialCenter.y = SanitizeFinite(dialCenter.y, 0f);
            dialHalfExtents.x = math.max(0.5f, SanitizeFinite(dialHalfExtents.x, DefaultDialHalfExtent));
            dialHalfExtents.y = math.max(0.5f, SanitizeFinite(dialHalfExtents.y, DefaultDialHalfExtent));
            ResolveSafeDialBounds(out minimumDegrees, out maximumDegrees);
            degreesPerScrollUnit = ResolveSafeScrollScale();
            scrollAudioVolume = ResolveSafeAudioVolume();
            scrollAudioPitch = ResolveSafeAudioPitch();
        }
#endif
    }
}
