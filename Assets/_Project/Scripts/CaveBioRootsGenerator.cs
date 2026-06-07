using Hecton8.Core;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Caves
{
    /// <summary>
    /// Runtime owner for hanging bioluminescent cave roots attached to voxel cave ceilings.
    /// Anchors use deterministic local-bounds sampling; root motion stays on the VISUAL_SYNC path.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CaveBioRootsGenerator : MonoBehaviour, ILateFrameTickable, ISlowTickable, IGlobalRegistryHotSwapListener
    {
        private const int MaxRootCount = 32;
        private const string LegacyRootNamePrefix = "_BioRoot_";
        private const int SwayLutSize = 1024;
        private const int SwayLutMask = SwayLutSize - 1;
        private const int SwayLutQuarter = SwayLutSize >> 2;
        private const float InvTau = 0.15915494309189535f;
        private const float Hash24ToUnit = 1f / 16777216f;
        private const float CeilingAnchorInset = 0.12f;
        private const float MaxGlobalIntensity = 1.25f;
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
        private bool _registeredLateFrameTick;
        private bool _registeredSlowTick;
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
        internal void ConfigureCold(HectonVoxelVolume targetVolume, CavePreset preset, CaveBioRootConfig config, float globalIntensity)
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

            float safeGlobalIntensity = ClampFinite(globalIntensity, 1f, 0f, MaxGlobalIntensity);
            float safeEffectIntensity = math.max(0.1f, safeGlobalIntensity);
            _segmentsPerRoot = Mathf.Clamp(config.segmentsPerRoot, 3, 16);
            _minLength = ClampFinite(config.minLength, 3f, 0.5f, 24f);
            _maxLength = math.max(_minLength, ClampFinite(config.maxLength, 9f, 0.5f, 32f));
            _swayAmplitude = ClampFinite(config.swayAmplitude, 0.45f, 0f, 4f) * safeEffectIntensity;
            _swayFrequency = ClampFinite(config.swayFrequency, 0.55f, 0.05f, 3f);
            _propWashRadius = ClampFinite(config.propWashRadius, 6f, 0.5f, 18f);
            _propWashStrength = ClampFinite(config.propWashStrength, 2.2f, 0f, 8f) * safeEffectIntensity;
            _topWidth = ClampFinite(config.topWidth, 0.14f, 0.01f, 0.5f);
            _tipWidth = math.min(_topWidth, ClampFinite(config.tipWidth, 0.04f, 0.005f, 0.3f));
            _glowColor = SanitizeColor(config.glowColor);
            int safeMaxRootCount = Mathf.Clamp(config.maxCount, 0, MaxRootCount);
            _rootCount = Mathf.Clamp(
                Mathf.RoundToInt(safeMaxRootCount * safeGlobalIntensity),
                0,
                safeMaxRootCount);

            EnsureBuffers();

            int resolvedRootCount = 0;
            for (int i = 0; i < _rootCount; i++)
            {
                if (ResolveAnchor(i))
                {
                    resolvedRootCount++;
                }
                else
                {
                    _rootAnchorsLocal[i] = Vector3.zero;
                    _rootLengths[i] = 0f;
                    _rootPhases[i] = 0f;
                }
            }

            DisableUnusedRoots();
            if (resolvedRootCount > 0)
                TryRegister();
            else
                TryUnregister();
        }

        /// <summary>
        /// Updates root sway in VISUAL_SYNC so simulation never depends on spline presentation.
        /// </summary>
        public void LateFrameTick()
        {
            if (_rootCount <= 0 || _volumeTransform == null)
                return;

            float dt = SystemDispatcher.CurrentFrameDeltaTime;
            _swayTime += ClampFinite(dt, 0f, 0f, 0.25f);

            Vector3 playerPosition = ResolvePlayerRuntimePosition();
            Vector3 playerVelocity = CoreDeterminismSignals.TryGetLatestKccVelocityVector(KccVelocityMaxAgeFrames, out Vector3 kccVelocity)
                ? kccVelocity
                : Vector3.zero;
            if (!IsFiniteVector3(playerVelocity))
                playerVelocity = Vector3.zero;
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
                if (!IsFiniteVector3(anchorLocal) || !IsFiniteVector3(anchorWS))
                    continue;

                float rootLength = _rootLengths[i];
                if (!math.isfinite(rootLength) || rootLength <= 0f)
                    continue;

                Vector3 wakeOffsetLS = ResolvePropWashOffset(anchorWS, playerPosition, playerVelocity, playerSpeed, rootLength);
                float phase = (time * _swayFrequency) + _rootPhases[i];
                float oscillation = FastSin(phase);
                Vector3 harmonicOffsetLS = new Vector3(oscillation * _swayAmplitude, 0f, FastCos((time * _swayFrequency * 0.73f) + _rootPhases[i]) * (_swayAmplitude * 0.35f));

                int segmentCount = positions.Length;
                float length = rootLength;
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

        public void SlowTick()
        {
            ResolvePlayerContext();
        }

        private void Awake()
        {
            if (volume != null)
                _volumeTransform = volume.transform;

            CacheRegistryServicesCold();
            ResolvePlayerContext();
        }

        private void OnEnable()
        {
            CacheRegistryServicesCold();
            ResolvePlayerContext();
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
            if (_volumeTransform == null ||
                !CaveRuntimeBoundsUtility.TryResolveLocalVolumeBounds(volume, _preset, out Bounds bounds) ||
                !IsFiniteBounds(bounds))
            {
                return false;
            }

            float margin = 0.75f;
            float minX = bounds.min.x + margin;
            float maxX = bounds.max.x - margin;
            float minZ = bounds.min.z + margin;
            float maxZ = bounds.max.z - margin;
            if (maxX < minX)
                minX = maxX = bounds.center.x;
            if (maxZ < minZ)
                minZ = maxZ = bounds.center.z;

            float sampleX = math.lerp(minX, maxX, Hash01(rootIndex + 1, 17));
            float sampleZ = math.lerp(minZ, maxZ, Hash01(rootIndex + 1, 53));
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
            if (!IsFiniteVector3(localStart) ||
                !IsFiniteVector3(localEnd) ||
                !IsFiniteVector3(localStartForward) ||
                !IsFiniteVector3(localEndForward))
            {
                return;
            }

            Vector3 start = _volumeTransform.TransformPoint(localStart);
            Vector3 end = _volumeTransform.TransformPoint(localEnd);
            if (!IsFiniteVector3(start) || !IsFiniteVector3(end))
                return;

            Vector3 startForward = ResolveSafeDirection(_volumeTransform.TransformDirection(localStartForward), Vector3.down);
            Vector3 endForward = ResolveSafeDirection(_volumeTransform.TransformDirection(localEndForward), Vector3.up);
            float radius = ClampFinite((_topWidth + _tipWidth) * 0.25f, 0.04f, 0.001f, 0.5f);
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
                TryUnregister();
                if (isActiveAndEnabled)
                {
                    if (currentService != null)
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
        }

        private Vector3 ResolvePlayerRuntimePosition()
        {
            if (_playerTransform == null)
                return Vector3.zero;

            Vector3 position = _playerTransform.position;
            return IsFiniteVector3(position) ? position : Vector3.zero;
        }

        private Vector3 ResolvePropWashOffset(Vector3 anchorWS, Vector3 playerPosition, Vector3 playerVelocity, float playerSpeed, float rootLength)
        {
            if (_playerTransform == null ||
                playerSpeed <= 0.25f ||
                !IsFiniteVector3(anchorWS) ||
                !IsFiniteVector3(playerPosition) ||
                !IsFiniteVector3(playerVelocity) ||
                !math.isfinite(playerSpeed) ||
                !math.isfinite(rootLength))
            {
                return Vector3.zero;
            }

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
            return IsFiniteVector3(wakeDirectionLS) ? wakeDirectionLS * (_propWashStrength * distanceT * speedT) : Vector3.zero;
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
            if (!IsFiniteVector3(value))
                return 0f;

            float ax = math.abs(value.x);
            float az = math.abs(value.z);
            float length = math.max(ax, az) + (math.min(ax, az) * 0.375f);
            return length > 0.0001f ? 1f / length : 0f;
        }

        private static bool IsFiniteBounds(Bounds bounds)
        {
            return IsFiniteVector3(bounds.min) &&
                   IsFiniteVector3(bounds.max) &&
                   IsFiniteVector3(bounds.center);
        }

        private static bool IsFiniteVector3(Vector3 value)
        {
            return math.isfinite(value.x) &&
                   math.isfinite(value.y) &&
                   math.isfinite(value.z);
        }

        private static Color SanitizeColor(Color value)
        {
            return math.isfinite(value.r) &&
                   math.isfinite(value.g) &&
                   math.isfinite(value.b) &&
                   math.isfinite(value.a)
                ? value
                : new Color(0.26f, 0.92f, 0.88f, 0.9f);
        }

        private static float ClampFinite(float value, float fallback, float minimum, float maximum)
        {
            float safeFallback = math.select(minimum, fallback, math.isfinite(fallback));
            float safeValue = math.select(safeFallback, value, math.isfinite(value));
            return math.clamp(safeValue, minimum, maximum);
        }

        private void TryRegister()
        {
            if (!Application.isPlaying)
                return;

            if (GlobalRegistry.Dispatcher == null)
                return;

            if (!_registeredSlowTick)
                _registeredSlowTick = GlobalRegistry.TryRegisterSlowTickable(this, PriorityLayer.Environment);

            if (!_registeredLateFrameTick)
                _registeredLateFrameTick = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Environment);
        }

        private void TryUnregister()
        {
            if (_registeredSlowTick)
            {
                GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Environment);
                _registeredSlowTick = false;
            }

            if (_registeredLateFrameTick)
            {
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Environment);
                _registeredLateFrameTick = false;
            }
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
