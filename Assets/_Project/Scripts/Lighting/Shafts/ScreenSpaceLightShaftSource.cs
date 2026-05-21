using Hecton8.Core;
using Hecton8.World;
using System.Runtime.InteropServices;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Lighting.Shafts
{
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    internal struct LightShaftContribution
    {
        [FieldOffset(0)]
        public uint SourceId;
        [FieldOffset(4)]
        public uint _pad0;
        [FieldOffset(8)]
        public float2 ScreenUv;
        [FieldOffset(16)]
        public float3 ColorRgb;
        [FieldOffset(28)]
        public float Intensity;
        [FieldOffset(32)]
        public float RadialFalloff;
        [FieldOffset(36)]
        public float MaxDistanceMeters;
        [FieldOffset(40)]
        public float Score;
        [FieldOffset(44)]
        public byte Flags;
        [FieldOffset(45)]
        public byte _pad1;
        [FieldOffset(46)]
        public ushort _pad2;
        [FieldOffset(48)]
        public ulong _pad3;
        [FieldOffset(56)]
        public ulong _pad4;
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
        private static uint _nextRuntimeSourceId = 1u;

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
        private uint _runtimeSourceId;
        private int _registeredIndex = -1;
        private int _lastBurstFrame = -100000;

        internal static int RegisteredCount => _registeredCount;

        internal uint ResolvedSourceId => sourceId != 0u ? sourceId : _runtimeSourceId;

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

        internal bool TryGetContribution(Camera renderCamera, in double3 cameraAup, out LightShaftContribution contribution)
        {
            contribution = default;

            if (renderCamera == null || _cachedTransform == null)
                return false;

            Light light = sourceLight;
            if (light == null || !light.enabled || intensityScale <= Epsilon)
                return false;

            Vector3 sourcePosition = _cachedTransform.position;
            if (!TryResolveRuntimeAup(sourcePosition, out double3 sourceAup))
                return false;

            double3 aupDelta = sourceAup - cameraAup;
            float aupDistance = (float)math.sqrt(math.max(0.0, math.lengthsq(aupDelta)));
            Vector3 viewport = renderCamera.WorldToViewportPoint(sourcePosition);
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

            float distanceFade = math.saturate(1f - aupDistance * math.rcp(math.max(0.25f, maxDistanceMeters)));
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
            if (_runtimeSourceId == 0u)
                _runtimeSourceId = AllocateRuntimeSourceId();

            if (sourceLight == null)
                TryGetComponent(out sourceLight);
        }

        private static uint AllocateRuntimeSourceId()
        {
            uint id = _nextRuntimeSourceId++;
            if (_nextRuntimeSourceId == 0u)
                _nextRuntimeSourceId = 1u;

            return id != 0u ? id : 1u;
        }

        private static bool TryResolveRuntimeAup(Vector3 runtimePosition, out double3 absoluteAup)
        {
            absoluteAup = default;
            float3 runtime = new float3(runtimePosition.x, runtimePosition.y, runtimePosition.z);
            if (!math.all(math.isfinite(runtime)))
                return false;

            AbsoluteUniversePosition originAup = GlobalSignals.CurrentRuntimeOriginAup();
            if (!originAup.IsFinite())
                return false;

            AbsoluteUniversePosition sourceAup = AbsoluteUniversePosition.OffsetMeters(
                in originAup,
                new double3(runtimePosition.x, runtimePosition.y, runtimePosition.z));
            if (!sourceAup.IsFinite())
                return false;

            absoluteAup = sourceAup.ToAbsoluteDouble3();
            return math.all(math.isfinite(absoluteAup));
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
