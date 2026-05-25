using Hecton8.Core;
using Hecton8.Physics;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Caves
{
    /// <summary>
    /// Runtime owner for hanging bioluminescent cave roots attached to voxel cave ceilings.
    /// Anchors use deterministic local-bounds sampling; root motion stays on the tick path.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CaveBioRootsGenerator : MonoBehaviour, ITickable, IUpdatable, IGlobalRegistryHotSwapListener
    {
        private const int MaxRootCount = 32;
        private const string LegacyRootNamePrefix = "_BioRoot_";
        private const int SwayLutSize = 1024;
        private const int SwayLutMask = SwayLutSize - 1;
        private const int SwayLutQuarter = SwayLutSize >> 2;
        private const float InvTau = 0.15915494309189535f;
        private const float Hash24ToUnit = 1f / 16777216f;
        private const float CeilingAnchorInset = 0.12f;
        private const uint KccVelocityMaxAgeFrames = 12u;
        private static readonly float[] _SwaySinLut = CreateSwaySinLut(); // COLD ALLOC: float[1024] - visual root sway sine LUT - owner: CaveBioRootsGenerator

        [Header("Runtime Wiring")]
        [SerializeField]
        [Tooltip("Voxel volume that owns the cave mesh and local-space bounds.")]
        private HectonVoxelVolume volume;

        [SerializeField]
        [Tooltip("Optional player override used when bootstrap has not published the runtime player yet.")]
        private Transform playerTransformOverride;

        private Transform _volumeTransform;
        private Transform _playerTransform;
        private Rigidbody _playerRigidbody;
        private CavePreset _preset;
        private int _rootCount;
        private int _segmentsPerRoot;
        private float _minLength;
        private float _maxLength;
        private float _swayAmplitude;
        private float _swayFrequency;
        private float _propWashRadius;
        private float _propWashStrength;
        private float _topWidth;
        private float _tipWidth;
        private Color _glowColor;
        private float _swayTime;
        private bool _registeredTick;
        private bool _hotSwapRegistered;
        private IConnectionSplineBatchRendererService _splineRenderer;
        private long[] _rootLinkIds;
        private Vector3[][] _rootPositions;
        private Vector3[] _rootAnchorsLocal;
        private float[] _rootLengths;
        private float[] _rootPhases;

        /// <summary>
        /// Configures the generator from the cave dressing owner.
        /// </summary>
        internal void Configure(HectonVoxelVolume targetVolume, CavePreset preset, CaveBioRootConfig config, float globalIntensity)
        {
            volume = targetVolume;
            _volumeTransform = targetVolume != null ? targetVolume.transform : null;
            _preset = preset;

            if (config == null || _volumeTransform == null)
            {
                DisableAllRoots();
                TryUnregister();
                return;
            }

            _segmentsPerRoot = Mathf.Clamp(config.segmentsPerRoot, 3, 16);
            _minLength = Mathf.Max(0.5f, config.minLength);
            _maxLength = Mathf.Max(_minLength, config.maxLength);
            _swayAmplitude = Mathf.Max(0f, config.swayAmplitude) * Mathf.Max(0.1f, globalIntensity);
            _swayFrequency = Mathf.Max(0.05f, config.swayFrequency);
            _propWashRadius = Mathf.Max(0.5f, config.propWashRadius);
            _propWashStrength = Mathf.Max(0f, config.propWashStrength) * Mathf.Max(0.1f, globalIntensity);
            _topWidth = Mathf.Max(0.01f, config.topWidth);
            _tipWidth = Mathf.Clamp(config.tipWidth, 0.005f, _topWidth);
            _glowColor = config.glowColor;
            _rootCount = Mathf.Clamp(
                Mathf.RoundToInt(config.maxCount * Mathf.Clamp01(globalIntensity)),
                0,
                Mathf.Min(config.maxCount, MaxRootCount));

            EnsureBuffers();

            for (int i = 0; i < _rootCount; i++)
            {
                ResolveAnchor(i);
            }

            DisableUnusedRoots();
            if (_rootCount > 0)
                TryRegister();
            else
                TryUnregister();
        }

        /// <summary>
        /// Updates root sway in sync with the runtime tick loop.
        /// </summary>
        public void Tick(float dt)
        {
            if (_rootCount <= 0 || _volumeTransform == null)
                return;

            ResolvePlayerContext();
            _swayTime += math.max(0f, dt);

            Vector3 playerPosition = ResolvePlayerRuntimePosition();
            Vector3 playerVelocity = PhysicsDeterminismSignals.TryGetLatestKccVelocityVector(KccVelocityMaxAgeFrames, out Vector3 kccVelocity)
                ? kccVelocity
                : Vector3.zero;
            float playerSpeedSq = playerVelocity.sqrMagnitude;
            float playerSpeed = playerSpeedSq > 0.0625f ? EstimateLength3D(playerVelocity) : 0f;
            float time = _swayTime;

            for (int i = 0; i < _rootCount; i++)
            {
                Vector3[] positions = _rootPositions[i];
                if (positions == null)
                    continue;

                Vector3 anchorLocal = _rootAnchorsLocal[i];
                Vector3 anchorWS = _volumeTransform.TransformPoint(anchorLocal);
                Vector3 wakeOffsetLS = ResolvePropWashOffset(anchorWS, playerPosition, playerVelocity, playerSpeed, _rootLengths[i]);
                float phase = (time * _swayFrequency) + _rootPhases[i];
                float oscillation = FastSin(phase);
                Vector3 harmonicOffsetLS = new Vector3(oscillation * _swayAmplitude, 0f, FastCos((time * _swayFrequency * 0.73f) + _rootPhases[i]) * (_swayAmplitude * 0.35f));

                int segmentCount = positions.Length;
                float length = _rootLengths[i];
                for (int segmentIndex = 0; segmentIndex < segmentCount; segmentIndex++)
                {
                    float t = segmentCount > 1 ? segmentIndex / (float)(segmentCount - 1) : 1f;
                    float bend = t * t;
                    Vector3 segmentOffset = (harmonicOffsetLS + wakeOffsetLS) * bend;
                    segmentOffset.y = 0f;
                    positions[segmentIndex] = anchorLocal + segmentOffset + (Vector3.down * (length * t));
                }

                SubmitRootSpline(i, positions, segmentCount);
            }
        }

        private void Awake()
        {
            if (volume != null)
                _volumeTransform = volume.transform;

            CacheRegistryServicesCold();
        }

        private void OnEnable()
        {
            CacheRegistryServicesCold();
            TryRegisterHotSwapListener();
            if (_rootCount > 0)
                TryRegister();
        }

        private void OnDisable()
        {
            RemoveAllRootLinks();
            TryUnregister();
            TryUnregisterHotSwapListener();
            ClearCachedRegistryServices();
        }

        private void OnDestroy()
        {
            RemoveAllRootLinks();
            TryUnregister();
            TryUnregisterHotSwapListener();
            ClearCachedRegistryServices();
        }

        private void EnsureBuffers()
        {
            if (_rootLinkIds == null || _rootLinkIds.Length != _rootCount)
            {
                RemoveAllRootLinks();
                _rootLinkIds = new long[_rootCount]; // COLD ALLOC: long[_rootCount] - procedural root spline link IDs - owner: CaveBioRootsGenerator
                _rootPositions = new Vector3[_rootCount][]; // COLD ALLOC: Vector3[_rootCount][] - cached spline position buffers - owner: CaveBioRootsGenerator
                _rootAnchorsLocal = new Vector3[_rootCount]; // COLD ALLOC: Vector3[_rootCount] - cached local-space root anchors - owner: CaveBioRootsGenerator
                _rootLengths = new float[_rootCount]; // COLD ALLOC: float[_rootCount] - cached root lengths - owner: CaveBioRootsGenerator
                _rootPhases = new float[_rootCount]; // COLD ALLOC: float[_rootCount] - cached root sway phase offsets - owner: CaveBioRootsGenerator
            }

            for (int i = 0; i < _rootCount; i++)
            {
                _rootLinkIds[i] = ResolveRootLinkId(i);
                if (_rootPositions[i] == null || _rootPositions[i].Length != _segmentsPerRoot)
                    _rootPositions[i] = new Vector3[_segmentsPerRoot]; // COLD ALLOC: Vector3[_segmentsPerRoot] - per-root spline positions - owner: CaveBioRootsGenerator
            }
        }

        private void DisableUnusedRoots()
        {
            int childCount = transform.childCount;
            for (int i = _rootCount; i < childCount; i++)
            {
                Transform child = transform.GetChild(i);
                if (child != null && HasLegacyRootName(child.name) && child.gameObject.activeSelf)
                    child.gameObject.SetActive(false);
            }
        }

        private void DisableAllRoots()
        {
            RemoveAllRootLinks();
            int childCount = transform.childCount;
            for (int i = 0; i < childCount; i++)
            {
                Transform child = transform.GetChild(i);
                if (child != null && HasLegacyRootName(child.name) && child.gameObject.activeSelf)
                    child.gameObject.SetActive(false);
            }
        }

        private bool ResolveAnchor(int rootIndex)
        {
            if (_volumeTransform == null || !CaveRuntimeBoundsUtility.TryResolveLocalVolumeBounds(volume, _preset, out Bounds bounds))
                return false;

            float margin = 0.75f;
            float sampleX = math.lerp(bounds.min.x + margin, bounds.max.x - margin, Hash01(rootIndex + 1, 17));
            float sampleZ = math.lerp(bounds.min.z + margin, bounds.max.z - margin, Hash01(rootIndex + 1, 53));
            _rootAnchorsLocal[rootIndex] = new Vector3(sampleX, bounds.max.y - CeilingAnchorInset, sampleZ);
            _rootLengths[rootIndex] = math.lerp(_minLength, _maxLength, Hash01(rootIndex + 1, 101));
            _rootPhases[rootIndex] = Hash01(rootIndex + 1, 149) * Mathf.PI * 2f;

            return true;
        }

        private void SubmitRootSpline(int rootIndex, Vector3[] positions, int segmentCount)
        {
            if (_volumeTransform == null ||
                _rootLinkIds == null ||
                (uint)rootIndex >= (uint)_rootLinkIds.Length ||
                positions == null ||
                segmentCount < 2)
            {
                return;
            }

            Vector3 localStart = positions[0];
            Vector3 localEnd = positions[segmentCount - 1];
            Vector3 localStartForward = positions[1] - localStart;
            Vector3 localEndForward = positions[segmentCount - 2] - localEnd;
            Vector3 start = _volumeTransform.TransformPoint(localStart);
            Vector3 end = _volumeTransform.TransformPoint(localEnd);
            Vector3 startForward = ResolveSafeDirection(_volumeTransform.TransformDirection(localStartForward), Vector3.down);
            Vector3 endForward = ResolveSafeDirection(_volumeTransform.TransformDirection(localEndForward), Vector3.up);
            float radius = math.max(0.001f, (_topWidth + _tipWidth) * 0.25f);
            SplineDescriptor descriptor = LogisticsPipeBuilder.CreateSocketDescriptor(
                start,
                end,
                startForward,
                endForward,
                radius,
                PipeRenderFlags.None);
            if (TryResolveSplineRenderer(out IConnectionSplineBatchRendererService renderer))
                renderer.SubmitPipeLink(_rootLinkIds[rootIndex], descriptor, _glowColor);
        }

        private void RemoveAllRootLinks()
        {
            RemoveAllRootLinks(_splineRenderer);
        }

        private void RemoveAllRootLinks(IConnectionSplineBatchRendererService renderer)
        {
            if (_rootLinkIds == null)
                return;

            for (int i = 0; i < _rootLinkIds.Length; i++)
            {
                long linkId = _rootLinkIds[i];
                if (linkId != 0L && renderer != null)
                    renderer.RemovePipeLink(linkId);
            }
        }

        private bool TryResolveSplineRenderer(out IConnectionSplineBatchRendererService renderer)
        {
            renderer = _splineRenderer;
            if (renderer != null)
                return true;

            return false;
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot == GlobalRegistryServiceSlot.Dispatcher)
            {
                if (currentService == null)
                {
                    _registeredTick = false;
                    return;
                }

                if (isActiveAndEnabled)
                {
                    TryUnregister();
                    TryRegister();
                }

                return;
            }

            if (serviceSlot != GlobalRegistryServiceSlot.ConnectionSplineBatchRendererRuntime)
                return;

            IConnectionSplineBatchRendererService previousRenderer =
                _splineRenderer ?? previousService as IConnectionSplineBatchRendererService;
            RemoveAllRootLinks(previousRenderer);
            _splineRenderer = currentService as IConnectionSplineBatchRendererService;
        }

        private void CacheRegistryServicesCold()
        {
            if (_splineRenderer == null)
                _splineRenderer = GlobalRegistry.ConnectionSplineBatchRenderer;
        }

        private void ClearCachedRegistryServices()
        {
            _splineRenderer = null;
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

        private long ResolveRootLinkId(int rootIndex)
        {
            long owner = unchecked((long)EntityId.ToULong(GetEntityId()));
            return (owner << 32) ^ (uint)rootIndex;
        }

        private static bool HasLegacyRootName(string name)
        {
            if (string.IsNullOrEmpty(name) || name.Length < LegacyRootNamePrefix.Length)
                return false;

            for (int i = 0; i < LegacyRootNamePrefix.Length; i++)
            {
                if (name[i] != LegacyRootNamePrefix[i])
                    return false;
            }

            return true;
        }

        private void ResolvePlayerContext()
        {
            Transform runtimePlayer = BootstrapState.CurrentPlayerTransform;
            _playerTransform = runtimePlayer != null ? runtimePlayer : playerTransformOverride;
            if (_playerTransform != null && (_playerRigidbody == null || _playerRigidbody.transform != _playerTransform))
                _playerTransform.TryGetComponent(out _playerRigidbody);
        }

        private Vector3 ResolvePlayerRuntimePosition()
        {
            return _playerTransform != null ? _playerTransform.position : Vector3.zero;
        }

        private Vector3 ResolvePropWashOffset(Vector3 anchorWS, Vector3 playerPosition, Vector3 playerVelocity, float playerSpeed, float rootLength)
        {
            if (_playerTransform == null || playerSpeed <= 0.25f)
                return Vector3.zero;

            Vector3 toAnchor = anchorWS - playerPosition;
            if (toAnchor.y < 0f || toAnchor.y > (rootLength + 2f))
                return Vector3.zero;

            Vector3 horizontalDelta = new Vector3(toAnchor.x, 0f, toAnchor.z);
            float horizontalDistanceSq = (horizontalDelta.x * horizontalDelta.x) + (horizontalDelta.z * horizontalDelta.z);
            float propWashRadiusSq = _propWashRadius * _propWashRadius;
            if (horizontalDistanceSq > propWashRadiusSq || horizontalDistanceSq <= 0.000001f)
                return Vector3.zero;

            float distanceT = 1f - math.saturate(horizontalDistanceSq / math.max(0.0001f, propWashRadiusSq));
            float speedT = math.saturate(playerSpeed * 0.1f);
            Vector3 wakeDirectionWS = playerVelocity.sqrMagnitude > 0.0001f ? playerVelocity : horizontalDelta;
            wakeDirectionWS.y = 0f;
            float wakeDirectionSq = (wakeDirectionWS.x * wakeDirectionWS.x) + (wakeDirectionWS.z * wakeDirectionWS.z);
            if (wakeDirectionSq <= 0.0001f)
                return Vector3.zero;

            wakeDirectionWS *= ApproximateInvLength2D(wakeDirectionWS);
            Vector3 wakeDirectionLS = _volumeTransform.InverseTransformDirection(-wakeDirectionWS);
            return wakeDirectionLS * (_propWashStrength * distanceT * speedT);
        }

        private static float EstimateLength3D(Vector3 value)
        {
            float ax = math.abs(value.x);
            float ay = math.abs(value.y);
            float az = math.abs(value.z);
            float maxAxis = math.max(ax, math.max(ay, az));
            float minAxis = math.min(ax, math.min(ay, az));
            float midAxis = ax + ay + az - maxAxis - minAxis;
            return maxAxis + (midAxis * 0.375f) + (minAxis * 0.25f);
        }

        private static float ApproximateInvLength2D(Vector3 value)
        {
            float ax = math.abs(value.x);
            float az = math.abs(value.z);
            float length = math.max(ax, az) + (math.min(ax, az) * 0.375f);
            return length > 0.0001f ? 1f / length : 0f;
        }

        private void TryRegister()
        {
            if (_registeredTick || !Application.isPlaying)
                return;

            if (GlobalRegistry.Dispatcher == null)
                return;

            _registeredTick = GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.Environment);
        }

        private void TryUnregister()
        {
            if (!_registeredTick)
                return;

            GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Environment);
            _registeredTick = false;
        }

        private static float Hash01(int index, int salt)
        {
            uint hash = ((uint)index * 0x9E3779B9u) ^ ((uint)salt * 0x85EBCA6Bu);
            hash ^= hash >> 16;
            hash *= 0x7FEB352Du;
            hash ^= hash >> 15;
            hash *= 0x846CA68Bu;
            hash ^= hash >> 16;
            return (hash & 0x00FFFFFFu) * Hash24ToUnit;
        }

        private static float FastSin(float radians)
        {
            int index = PhaseToIndex(radians);
            return _SwaySinLut[index & SwayLutMask];
        }

        private static float FastCos(float radians)
        {
            int index = PhaseToIndex(radians);
            return _SwaySinLut[(index + SwayLutQuarter) & SwayLutMask];
        }

        private static int PhaseToIndex(float radians)
        {
            return (int)(radians * (SwayLutSize * InvTau));
        }

        private static Vector3 ResolveSafeDirection(Vector3 value, Vector3 fallback)
        {
            float sq = value.sqrMagnitude;
            if (math.isfinite(sq) && sq > 0.000001f)
                return value * math.rsqrt(sq);

            float fallbackSq = fallback.sqrMagnitude;
            return math.isfinite(fallbackSq) && fallbackSq > 0.000001f
                ? fallback * math.rsqrt(fallbackSq)
                : Vector3.down;
        }

        private static float[] CreateSwaySinLut()
        {
            float[] values = new float[SwayLutSize];
            for (int i = 0; i < SwayLutSize; i++)
            {
                values[i] = MathLodApproximation.ApproxSinBhaskara((i + 0.5f) * (2f * math.PI / SwayLutSize));
            }

            return values;
        }
    }
}
