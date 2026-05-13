using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Lighting.Shafts
{
    internal struct LightShaftContribution
    {
        public uint SourceId;
        public float2 ScreenUv;
        public float3 ColorRgb;
        public float Intensity;
        public float RadialFalloff;
        public float MaxDistanceMeters;
        public float Score;
        public byte Flags;
    }

    /// <summary>
    /// Authoring component for one screen-space light shaft emitter.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/Lighting/Screen Space Light Shaft Source")]
    public sealed class ScreenSpaceLightShaftSource : MonoBehaviour
    {
        private const int MaxRegisteredSources = 64;
        private const float ViewPadding = 0.08f;
        private const float Epsilon = 0.0001f;

        // COLD ALLOC: ScreenSpaceLightShaftSource[64] - fixed screen-space shaft source registry - owner: ScreenSpaceLightShaftSource
        private static readonly ScreenSpaceLightShaftSource[] _registeredSources = new ScreenSpaceLightShaftSource[MaxRegisteredSources];
        private static int _registeredCount;

        [Header("Light Shaft Source")]
        [Tooltip("Light used for color and intensity inheritance. Cached; no scene search at runtime.")]
        [SerializeField] private Light sourceLight;
        [Tooltip("Stable signal ID. Zero falls back to this component instance ID.")]
        [SerializeField] private uint sourceId;
        [Tooltip("Fallback shaft tint when light color inheritance is disabled.")]
        [SerializeField] private Color shaftTint = new Color(0.38f, 0.92f, 1f, 1f);
        [Tooltip("Scalar applied before this source enters the top-three tracker.")]
        [SerializeField, Min(0f)] private float intensityScale = 1f;
        [Tooltip("Distance at which the fake is fully faded out in view space meters.")]
        [SerializeField, Min(0.25f)] private float maxDistanceMeters = 180f;
        [Tooltip("Radial blur falloff exponent. Higher values make tighter shafts.")]
        [SerializeField, Range(0.4f, 5f)] private float radialFalloff = 1.35f;
        [Tooltip("Uses the Light component RGB as shaft color.")]
        [SerializeField] private bool useLightColor = true;
        [Tooltip("Allows this source to emit VisualFlareSignal when intensity spikes.")]
        [SerializeField] private bool massiveBurstEmitter;
        [Tooltip("Resolved intensity threshold for VisualFlareSignal.")]
        [SerializeField, Min(0f)] private float massiveBurstThreshold = 4f;
        [Tooltip("Minimum cooldown in frames between VisualFlareSignal packets.")]
        [SerializeField, Min(1)] private int massiveBurstCooldownFrames = 45;

        private Transform _cachedTransform;
        private int _registeredIndex = -1;
        private int _lastBurstFrame = -100000;

        internal static int RegisteredCount => _registeredCount;

        internal uint ResolvedSourceId => sourceId != 0u ? sourceId : unchecked((uint)GetInstanceID());

        internal static ScreenSpaceLightShaftSource GetRegisteredAt(int index)
        {
            return index >= 0 && index < _registeredCount ? _registeredSources[index] : null;
        }

        private void Awake()
        {
            CacheLocalReferences();
        }

        private void OnEnable()
        {
            CacheLocalReferences();
            RegisterSource(this);
        }

        private void OnDisable()
        {
            UnregisterSource(this);
        }

        internal bool TryGetContribution(Camera renderCamera, out LightShaftContribution contribution)
        {
            contribution = default;

            if (renderCamera == null || _cachedTransform == null)
                return false;

            Light light = sourceLight;
            if (light == null || !light.enabled || intensityScale <= Epsilon)
                return false;

            Vector3 viewport = renderCamera.WorldToViewportPoint(_cachedTransform.position);
            if (!math.isfinite(viewport.x) || !math.isfinite(viewport.y) || !math.isfinite(viewport.z))
                return false;

            if (viewport.z <= Epsilon ||
                viewport.x < -ViewPadding ||
                viewport.x > 1f + ViewPadding ||
                viewport.y < -ViewPadding ||
                viewport.y > 1f + ViewPadding)
            {
                return false;
            }

            float distanceFade = math.saturate(1f - viewport.z * math.rcp(math.max(0.25f, maxDistanceMeters)));
            float resolvedIntensity = math.max(0f, light.intensity) * math.max(0f, intensityScale) * distanceFade;
            if (resolvedIntensity <= Epsilon)
                return false;

            Color sourceColor = useLightColor ? light.color : shaftTint;
            float3 rgb = new float3(
                math.saturate(sourceColor.r),
                math.saturate(sourceColor.g),
                math.saturate(sourceColor.b));
            float luma = math.dot(rgb, new float3(0.2126f, 0.7152f, 0.0722f));

            contribution.SourceId = ResolvedSourceId;
            contribution.ScreenUv = new float2(math.saturate(viewport.x), math.saturate(viewport.y));
            contribution.ColorRgb = rgb;
            contribution.Intensity = resolvedIntensity;
            contribution.RadialFalloff = math.max(0.4f, radialFalloff);
            contribution.MaxDistanceMeters = math.max(0.25f, maxDistanceMeters);
            contribution.Score = resolvedIntensity * math.max(0.1f, luma);
            contribution.Flags = massiveBurstEmitter ? (byte)1 : (byte)0;
            return true;
        }

        internal bool ShouldEmitBurst(float resolvedIntensity, int frame)
        {
            if (!massiveBurstEmitter || resolvedIntensity < massiveBurstThreshold)
                return false;

            int cooldown = math.max(1, massiveBurstCooldownFrames);
            if (frame - _lastBurstFrame < cooldown)
                return false;

            _lastBurstFrame = frame;
            return true;
        }

        private void CacheLocalReferences()
        {
            _cachedTransform = transform;
            if (sourceLight == null)
                TryGetComponent(out sourceLight);
        }

        private static void RegisterSource(ScreenSpaceLightShaftSource source)
        {
            if (source == null || source._registeredIndex >= 0)
                return;

            if (_registeredCount >= MaxRegisteredSources)
                return;

            source._registeredIndex = _registeredCount;
            _registeredSources[_registeredCount] = source;
            _registeredCount++;
        }

        private static void UnregisterSource(ScreenSpaceLightShaftSource source)
        {
            if (source == null)
                return;

            int index = source._registeredIndex;
            if (index < 0 || index >= _registeredCount)
            {
                source._registeredIndex = -1;
                return;
            }

            int lastIndex = _registeredCount - 1;
            ScreenSpaceLightShaftSource moved = _registeredSources[lastIndex];
            _registeredSources[index] = moved;
            _registeredSources[lastIndex] = null;
            _registeredCount = lastIndex;
            source._registeredIndex = -1;

            if (moved != null && !ReferenceEquals(moved, source))
                moved._registeredIndex = index;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (massiveBurstCooldownFrames < 1)
                massiveBurstCooldownFrames = 1;

            intensityScale = math.max(0f, intensityScale);
            maxDistanceMeters = math.max(0.25f, maxDistanceMeters);
            radialFalloff = math.clamp(radialFalloff, 0.4f, 5f);
            CacheLocalReferences();
        }
#endif
    }
}
