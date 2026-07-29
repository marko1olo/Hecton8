using Hecton8.Bootstrap;
using Hecton8.Core;
using Hecton8.Core.Contracts;
using Hecton8.Gameplay;
using Hecton8.World;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using Stopwatch = System.Diagnostics.Stopwatch;
#endif

namespace Hecton8.UI
{
    /// <summary>
    /// HUD-only enemy radar fake: spatial hash contacts, flat XZ math, one instanced mesh draw.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/UI/Fake Radar Blip Controller")]
    public sealed class FakeRadarBlipController : MonoBehaviour, ILateFrameTickable, IRenderable, IScanEventListener, IGlobalRegistryHotSwapListener
    {
        private const int MaxBlips = 64;
        private const int HudInternalLayerIndex = 17;
        private const float DefaultRadarRangeMeters = 100f;
        private const float DefaultRadarRadiusPixels = 74f;
        private const float BlipSizePixels = 7f;
        private const float DefaultRadarCenterInsetX = 142f;
        private const float DefaultRadarCenterInsetY = 132f;
        private const float ProjectionDistanceMeters = 0.5f;
        private const float ThermalNoiseStartDepthMeters = 4000f;
        private const float ThermalNoiseFullDepthMeters = 6500f;
        private const float ThermalNoiseCycleSeconds = 0.83f;
        private const int ThermalNoiseMaxGhostBlips = 8;
        private const float ThermalNoiseRadialByteScale = 0.0032156863f;
        private const float WreckSignalDistortionSeconds = 1.35f;
        private const float WreckSignalDistortionThicknessPixels = 3.0f;
        private const float BlipFlickerFrequency = 18f;
        private const float BlipFlickerIntensity = 0.18f;
        private const float BlipFillAlpha = 0.36f;
        private const int PlayerTransformResolveIntervalFrames = 30;
        private const int MinimumQualityBlipCapacity = 16;
        private const uint ThermalNoiseHashSalt = 0x54484E31u;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private const double RadarSolveBudgetWarningMilliseconds = 0.1d;
        private const int RadarPerformanceWarningCooldownFrames = 30;
#endif
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private const uint RadarSolveBudgetWarningHash = 648937224u;
        private const uint RadarSolveBudgetContextHash = 2418241056u;
#endif

        private static readonly int _BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int _FlickerFrequencyId = Shader.PropertyToID("_FlickerFrequency");
        private static readonly int _FlickerIntensityId = Shader.PropertyToID("_FlickerIntensity");
        private static readonly int _FillAlphaId = Shader.PropertyToID("_FillAlpha");
        [SerializeField, Min(1f)] private float radarRangeMeters = DefaultRadarRangeMeters;
        [SerializeField, Min(1f)] private float radarRadiusPixels = DefaultRadarRadiusPixels;
        [SerializeField, Min(1f)] private float blipSizePixels = BlipSizePixels;
        [SerializeField] private Vector2 radarCenterInsetPixels = DefaultRadarCenterInsetPixels();
        [SerializeField] private Mesh radarBlipMesh;
        [SerializeField] private Material radarBlipMaterial;
        [SerializeField] private Color blipColor = new Color(1f, 0.24f, 0.28f, 0.92f);

        // COLD ALLOC: SpatialQueryHit[64] - fixed hostile radar query buffer - owner: FakeRadarBlipController
        private readonly SpatialQueryHit[] _queryHits = new SpatialQueryHit[MaxBlips];
        // COLD ALLOC: Matrix4x4[64] - instanced hostile radar blip matrices - owner: FakeRadarBlipController
        private readonly Matrix4x4[] _blipMatrices = new Matrix4x4[MaxBlips];

#pragma warning disable CS0414
        private bool _registered;
#pragma warning restore CS0414
        private bool _registeredLateFrame;
        private bool _registeredRenderable;
        private bool _scanEventsRegistered;
        private bool _hotSwapListenerRegistered;
        private bool _radarCullScheduled;
        private bool _radarBlipMaterialPropertiesDirty = true;
        private bool _missingBlipDrawAssetsAnnounced;
        private int _scheduledCandidateCount;
        private int _scheduledBlipCapacity = MaxBlips;
        private int _qualityBlipCapacity = MaxBlips;
        private int _qualityThermalGhostCapacity = ThermalNoiseMaxGhostBlips;
        private Transform _playerTransform;
        private IPlayerRuntimeContext _cachedPlayerContext;
        private int _nextPlayerTransformResolveFrame;
        private Camera _projectionCamera;
        private bool _projectionCameraRequiresHudLayer;
        private Mesh _radarBlipMesh;
        private MaterialPropertyBlock _radarBlipProperties;
        private Color _appliedRadarBlipColor;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private int _nextRadarPerformanceWarningFrame;
#endif
        private float _wreckSignalDistortionTime;
        private float _wreckSignalDistortionPhase;
        private float _thermalNoiseClock;
        private uint _thermalNoiseCycleIndex;
        private Quaternion _scheduledPlaneRotation = Quaternion.identity;
        private Vector3 _scheduledPlaneScale = Vector3.one;
        private Vector3 _scheduledPlaneCenter;
        private Vector3 _scheduledCameraRight;
        private Vector3 _scheduledCameraUp;
        private Vector2 _scheduledRadarCenter;
        private float3 _lastRadarForwardBucket = DefaultRadarForwardBucket();
        private float _scheduledRadarRadius;
        private float _scheduledWorldPerPixel;
        private int _visibleBlipMatrixCount;
        private int _discardedContactsWithoutAupCount;
        private bool _blipMatricesDirty;
        private bool _discardScheduledCullResult;

        public int DiscardedContactsWithoutAupCount => _discardedContactsWithoutAupCount;

        private static Vector2 DefaultRadarCenterInsetPixels()
        {
            return MakeVector2(DefaultRadarCenterInsetX, DefaultRadarCenterInsetY);
        }

        private static float3 DefaultRadarForwardBucket()
        {
            return MakeFloat3(0f, 0f, 1f);
        }

        private static Vector2 MakeVector2(float x, float y)
        {
            Vector2 value = default;
            value.x = x;
            value.y = y;
            return value;
        }

        private static float2 MakeFloat2(float x, float y)
        {
            float2 value = default;
            value.x = x;
            value.y = y;
            return value;
        }

        private static float3 MakeFloat3(float x, float y, float z)
        {
            float3 value = default;
            value.x = x;
            value.y = y;
            value.z = z;
            return value;
        }

        private static void IncrementCounterSaturated(ref int counter)
        {
            if (counter < int.MaxValue)
                counter++;
        }

        private void OnEnable()
        {
            CacheRegistryServicesCold();
            TryRegisterHotSwapListener();
            ResolvePlayerTransform();
            EnsureRuntimeResources();
            TryRegisterScanEvents();
            TryRegister();
        }

        private void Start()
        {
            CacheRegistryServicesCold();
            ResolvePlayerTransform();
            ResolveProjectionCamera();
            EnsureRuntimeResources();
            TryRegisterScanEvents();
            TryRegister();
        }

        private void OnDisable()
        {
            DrainScheduledCullForDisable();
            ClearVisibleBlipHandoff();
            TryUnregisterScanEvents();
            TryUnregister();
            TryUnregisterHotSwapListener();
        }

        private void OnDestroy()
        {
            TryUnregisterScanEvents();
            TryUnregister();
            TryUnregisterHotSwapListener();
            DisposeRuntimeResources();
        }

        private void AdvanceRadarPresentation(float deltaTime)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            long tickStart = Stopwatch.GetTimestamp();
#endif

            try
            {
                float safeDeltaTime = math.isfinite(deltaTime) ? math.max(0f, deltaTime) : 0f;
                RefreshQualityPolicy();
                AdvanceThermalNoiseClock(safeDeltaTime);

                if (_projectionCamera == null ||
                    _radarBlipMesh == null ||
                    radarBlipMaterial == null)
                {
                    ClearVisibleBlipHandoff();
                    return;
                }

                if (_wreckSignalDistortionTime > 0f)
                    _wreckSignalDistortionTime = math.max(0f, _wreckSignalDistortionTime - safeDeltaTime);

                if (_radarCullScheduled)
                    return;

                ScheduleBlipCull(_projectionCamera);
            }
            finally
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                PublishRadarSolveWarningIfNeeded(tickStart);
#endif
            }
        }

        private void AdvanceThermalNoiseClock(float deltaTime)
        {
            if (deltaTime <= 0f)
                return;

            float cycleSeconds = math.max(0.01f, ThermalNoiseCycleSeconds);
            _thermalNoiseClock += math.min(deltaTime, cycleSeconds * 4f);
            int cycles = (int)math.min(4f, math.floor(_thermalNoiseClock / cycleSeconds));
            if (cycles <= 0)
                return;

            _thermalNoiseClock -= cycleSeconds * cycles;
            unchecked
            {
                _thermalNoiseCycleIndex += (uint)cycles;
            }
        }

        public void LateFrameTick()
        {
            AdvanceRadarPresentation(SystemDispatcher.CurrentFrameDeltaTime);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            long tickStart = Stopwatch.GetTimestamp();
#endif

            try
            {
                if (!_radarCullScheduled)
                {
                    return;
                }

                bool discardCompletedCull = _discardScheduledCullResult;
                int visibleCount = _scheduledCandidateCount;
                _radarCullScheduled = false;
                _scheduledCandidateCount = 0;
                _discardScheduledCullResult = false;

                if (discardCompletedCull)
                {
                    ClearVisibleBlipHandoff();
                    return;
                }

                _visibleBlipMatrixCount = visibleCount;
                _blipMatricesDirty = false;
            }
            finally
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                PublishRadarSolveWarningIfNeeded(tickStart);
#endif
            }
        }

        public void Render(float deltaTime)
        {
            if (_visibleBlipMatrixCount <= 0 ||
                _projectionCamera == null ||
                _radarBlipMesh == null ||
                radarBlipMaterial == null)
            {
                return;
            }

            Camera renderCamera = GlobalRenderContext.CurrentCamera;
            if (renderCamera == null ||
                renderCamera != _projectionCamera ||
                renderCamera.cameraType == CameraType.Preview ||
                renderCamera.cameraType == CameraType.Reflection)
            {
                return;
            }

            int visibleCount = math.min(_visibleBlipMatrixCount, MaxBlips);
            if (visibleCount <= 0)
                return;

            DrawBlipMatrices(renderCamera, visibleCount);
        }

        private void ClearVisibleBlipHandoff()
        {
            if (_visibleBlipMatrixCount == 0 && !_blipMatricesDirty)
                return;

            _visibleBlipMatrixCount = 0;
            _blipMatricesDirty = false;
        }

        private void AppendVisibleBlipMatrix(Matrix4x4 matrix, ref int visibleCount)
        {
            int blipCapacity = math.clamp(_scheduledBlipCapacity, MinimumQualityBlipCapacity, MaxBlips);
            if (visibleCount >= blipCapacity)
            {
                visibleCount = math.min(visibleCount, blipCapacity);
                return;
            }

            _blipMatrices[visibleCount] = matrix;
            visibleCount++;
        }

        public void OnScanEvent(in ScanEventPayload payload)
        {
            if ((ScanEventType)payload.EventType != ScanEventType.ScanTriggered ||
                payload.Reserved != ScanEvents.WreckSignalReservedMarker)
            {
                return;
            }

            _wreckSignalDistortionTime = WreckSignalDistortionSeconds;
            uint hash = MixHash(math.asuint(payload.Position.x) ^ math.asuint(payload.Position.z) ^ 0x57524543u);
            _wreckSignalDistortionPhase = (hash & 0xFFFFu) * (1f / 65535f);
        }

        private void ScheduleBlipCull(Camera projectionCamera)
        {
            if (!TryResolvePlayerAup(out AbsoluteUniversePosition playerAup))
            {
                ClearVisibleBlipHandoff();
                _scheduledCandidateCount = 0;
                _scheduledBlipCapacity = math.clamp(_qualityBlipCapacity, MinimumQualityBlipCapacity, MaxBlips);
                _radarCullScheduled = false;
                return;
            }

            int hitCount = FaunaSpatialHashRegistry.CollectContactsNonAlloc(
                in playerAup,
                radarRangeMeters,
                SpatialTargetKind.Bioform,
                _queryHits);
            int blipCapacity = math.clamp(_qualityBlipCapacity, MinimumQualityBlipCapacity, MaxBlips);

            float range = math.max(1f, radarRangeMeters);
            float rangeSqr = range * range;
            float invRange = 1f / range;
            float radius = math.max(1f, radarRadiusPixels);
            float projectionDistance = math.max(
                projectionCamera.nearClipPlane + 0.05f,
                ProjectionDistanceMeters);
            ResolveCameraPlaneMetrics(
                projectionCamera,
                projectionDistance,
                out float worldPerPixel,
                out float halfWidth,
                out float halfHeight);

            Transform cameraTransform = projectionCamera.transform;
            Vector3 cameraForward = cameraTransform.forward;
            Vector3 cameraRight = cameraTransform.right;
            Vector3 cameraUp = cameraTransform.up;
            Vector3 planeCenter = cameraTransform.position + cameraForward * projectionDistance;
            Vector2 radarCenter = default;
            radarCenter.x = halfWidth - math.max(0f, radarCenterInsetPixels.x) * worldPerPixel;
            radarCenter.y = -halfHeight + math.max(0f, radarCenterInsetPixels.y) * worldPerPixel;
            float blipWorldSize = math.max(1f, blipSizePixels) * worldPerPixel;
            float blipHalfExtent = blipWorldSize * 0.5f;
            float radarRadiusWorld = radius * worldPerPixel;
            float2 boundsMin = MakeFloat2(-halfWidth - blipHalfExtent, -halfHeight - blipHalfExtent);
            float2 boundsMax = MakeFloat2(halfWidth + blipHalfExtent, halfHeight + blipHalfExtent);
            Quaternion planeRotation = cameraTransform.rotation;
            Vector3 planeScale = default;
            planeScale.x = blipWorldSize;
            planeScale.y = blipWorldSize;
            planeScale.z = 1f;
            _scheduledPlaneCenter = planeCenter;
            _scheduledCameraRight = cameraRight;
            _scheduledCameraUp = cameraUp;
            _scheduledPlaneRotation = planeRotation;
            _scheduledPlaneScale = planeScale;
            _scheduledRadarCenter = radarCenter;
            _scheduledRadarRadius = radius;
            _scheduledWorldPerPixel = worldPerPixel;
            float3 radarForwardFlatF3 = ResolveRadarForwardBucket(cameraForward);
            float3 radarRightFlatF3 = MakeFloat3(radarForwardFlatF3.z, 0f, -radarForwardFlatF3.x);

            int visibleCount = 0;
            float2 radarCenter2 = MakeFloat2(radarCenter.x, radarCenter.y);
            float radarScale = invRange * radarRadiusWorld;
            for (int i = 0; i < hitCount && visibleCount < blipCapacity; i++)
            {
                SpatialQueryHit hit = _queryHits[i];
                if (!(hit.Owner is IFaunaSpatialContact faunaContact) || !faunaContact.IsAggressiveContact)
                    continue;

                if (!hit.HasAbsolutePosition)
                {
                    IncrementCounterSaturated(ref _discardedContactsWithoutAupCount);
                    continue;
                }

                AbsoluteUniversePosition hitAup = hit.AbsolutePosition;
                float3 enemyDeltaAup = AupPrecisionMath.LocalDeltaFloat3Clamped(
                    hitAup.ToAbsoluteDouble3(),
                    playerAup.ToAbsoluteDouble3(),
                    AupPrecisionMath.DefaultMaxLocalCastMeters,
                    float3.zero);
                float2 flatDelta = MakeFloat2(
                    math.dot(enemyDeltaAup, radarRightFlatF3),
                    math.dot(enemyDeltaAup, radarForwardFlatF3));

                float distanceSqr = math.lengthsq(flatDelta);
                if (distanceSqr <= 0.0001f || distanceSqr > rangeSqr)
                    continue;

                float2 planeOffset = radarCenter2 + flatDelta * radarScale;
                if (planeOffset.x < boundsMin.x ||
                    planeOffset.x > boundsMax.x ||
                    planeOffset.y < boundsMin.y ||
                    planeOffset.y > boundsMax.y)
                {
                    continue;
                }

                Vector3 worldPosition = planeCenter + cameraRight * planeOffset.x + cameraUp * planeOffset.y;
                AppendVisibleBlipMatrix(Matrix4x4.TRS(worldPosition, planeRotation, planeScale), ref visibleCount);
            }

            AppendThermalNoiseGhostBlips(
                ref visibleCount,
                _scheduledRadarRadius,
                _scheduledWorldPerPixel,
                _scheduledRadarCenter,
                _scheduledPlaneCenter,
                _scheduledCameraRight,
                _scheduledCameraUp,
                _scheduledPlaneRotation,
                _scheduledPlaneScale);

            AppendWreckSignalDistortion(ref visibleCount);

            _scheduledCandidateCount = visibleCount;
            _scheduledBlipCapacity = blipCapacity;
            _radarCullScheduled = true;
            _discardScheduledCullResult = false;
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

            if (serviceSlot != GlobalRegistryServiceSlot.Player)
                return;

            _cachedPlayerContext = currentService as IPlayerRuntimeContext;
            AssignPlayerTransform(_cachedPlayerContext != null ? _cachedPlayerContext.PlayerTransform : null);
            _projectionCamera = null;
            _projectionCameraRequiresHudLayer = false;
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

        private void CacheRegistryServicesCold()
        {
            RefreshQualityPolicy();
            IPlayerRuntimeContext playerContext = GlobalRegistry.Player;
            if (ReferenceEquals(_cachedPlayerContext, playerContext))
                return;

            _cachedPlayerContext = playerContext;
            AssignPlayerTransform(playerContext != null ? playerContext.PlayerTransform : null);
            _projectionCamera = null;
            _projectionCameraRequiresHudLayer = false;
        }

        private void RefreshQualityPolicy()
        {
            float qualityWeight01 = SanitizeQualityWeight01(HomeostasisBrain.GlobalQualityWeight);
            _qualityBlipCapacity = ResolveQualityCapacity(qualityWeight01, MinimumQualityBlipCapacity, MaxBlips);
            _qualityThermalGhostCapacity = ResolveQualityCapacity(qualityWeight01, 0, ThermalNoiseMaxGhostBlips);
        }

        private bool TryResolvePlayerAup(out AbsoluteUniversePosition playerAup)
        {
            IPlayerRuntimeContext playerContext = _cachedPlayerContext;
            if (playerContext != null &&
                playerContext.TryGetMovementRuntimeState(out PlayerMovementRuntimeState movementState) &&
                (movementState.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) != 0u &&
                movementState.PredictedAup.IsFinite())
            {
                playerAup = movementState.PredictedAup;
                return true;
            }

            playerAup = default;
            return false;
        }

        private void AppendWreckSignalDistortion(ref int visibleCount)
        {
            int blipCapacity = math.clamp(_scheduledBlipCapacity, MinimumQualityBlipCapacity, MaxBlips);
            if (_wreckSignalDistortionTime <= 0f || visibleCount >= blipCapacity)
                return;

            float pulse01 = math.saturate(_wreckSignalDistortionTime / WreckSignalDistortionSeconds);
            float scanRaw = _wreckSignalDistortionPhase + (1f - pulse01) * 1.75f;
            float scan01 = scanRaw - math.floor(scanRaw);
            float radarRadiusWorld = math.max(1f, _scheduledRadarRadius) * _scheduledWorldPerPixel;
            float lineY = radarRadiusWorld * (scan01 * 2f - 1f);
            Vector3 worldPosition =
                _scheduledPlaneCenter +
                _scheduledCameraRight * _scheduledRadarCenter.x +
                _scheduledCameraUp * (_scheduledRadarCenter.y + lineY);
            Vector3 lineScale = default;
            lineScale.x = radarRadiusWorld * 2f;
            lineScale.y = math.max(1f, WreckSignalDistortionThicknessPixels) * _scheduledWorldPerPixel * (0.65f + 0.95f * pulse01);
            lineScale.z = 1f;
            AppendVisibleBlipMatrix(Matrix4x4.TRS(worldPosition, _scheduledPlaneRotation, lineScale), ref visibleCount);
        }

        private void DrawBlipMatrices(Camera renderCamera, int visibleCount)
        {
            ApplyRadarBlipMaterialProperties();

            UnityEngine.Graphics.DrawMeshInstanced(
                _radarBlipMesh,
                0,
                radarBlipMaterial,
                _blipMatrices,
                visibleCount,
                _radarBlipProperties,
                ShadowCastingMode.Off,
                false,
                HudInternalLayerIndex,
                renderCamera,
                LightProbeUsage.Off);
        }

        private void ApplyRadarBlipMaterialProperties()
        {
            EnsureRadarBlipPropertiesCold();

            if (!_radarBlipMaterialPropertiesDirty && ColorsMatch(_appliedRadarBlipColor, blipColor))
                return;

            _radarBlipProperties.Clear();
            _radarBlipProperties.SetColor(_BaseColorId, blipColor);
            _radarBlipProperties.SetFloat(_FlickerFrequencyId, BlipFlickerFrequency);
            _radarBlipProperties.SetFloat(_FlickerIntensityId, BlipFlickerIntensity);
            _radarBlipProperties.SetFloat(_FillAlphaId, BlipFillAlpha);
            _appliedRadarBlipColor = blipColor;
            _radarBlipMaterialPropertiesDirty = false;
        }

        private static bool ColorsMatch(Color lhs, Color rhs)
        {
            return lhs.r == rhs.r &&
                lhs.g == rhs.g &&
                lhs.b == rhs.b &&
                lhs.a == rhs.a;
        }

        private void ClearScheduledCullState()
        {
            if (!_radarCullScheduled)
                return;

            _radarCullScheduled = false;
            _scheduledCandidateCount = 0;
            _discardScheduledCullResult = false;
            ClearVisibleBlipHandoff();
        }

        private void DrainScheduledCullForDisable()
        {
            ClearScheduledCullState();
        }

        private void AppendThermalNoiseGhostBlips(
            ref int visibleCount,
            float radius,
            float worldPerPixel,
            Vector2 radarCenter,
            Vector3 planeCenter,
            Vector3 cameraRight,
            Vector3 cameraUp,
            Quaternion planeRotation,
            Vector3 planeScale)
        {
            int blipCapacity = math.clamp(_scheduledBlipCapacity, MinimumQualityBlipCapacity, MaxBlips);
            int ghostCapacity = math.clamp(_qualityThermalGhostCapacity, 0, ThermalNoiseMaxGhostBlips);
            if (visibleCount >= blipCapacity || ghostCapacity <= 0 || !TryResolvePlayerDepthMeters(out float depthMeters))
                return;

            float thermalNoise01 = math.saturate(
                (depthMeters - ThermalNoiseStartDepthMeters) /
                math.max(1f, ThermalNoiseFullDepthMeters - ThermalNoiseStartDepthMeters));
            if (thermalNoise01 <= 0f)
                return;

            int ghostCount = math.clamp(
                1 + (int)math.floor(thermalNoise01 * ghostCapacity),
                1,
                ghostCapacity);
            uint depthBucket = unchecked((uint)(int)math.floor(depthMeters));
            float acceptance = 0.35f + 0.57f * thermalNoise01;
            uint acceptanceThreshold = (uint)math.clamp((int)(acceptance * 255f), 0, 255);
            float radiusWorld = radius * worldPerPixel;
            for (int i = 0; i < ghostCount && visibleCount < blipCapacity; i++)
            {
                uint hash = HashThermalNoiseGhost(_thermalNoiseCycleIndex, depthBucket, unchecked((uint)i));
                if (((hash >> 24) & 0xFFu) > acceptanceThreshold)
                    continue;

                float radial01 = 0.16f + ((hash >> 16) & 0xFFu) * ThermalNoiseRadialByteScale;
                Vector2 planeOffset = radarCenter + ResolveThermalGhostDirection(hash) * (radiusWorld * radial01);
                Vector3 worldPosition = planeCenter + cameraRight * planeOffset.x + cameraUp * planeOffset.y;
                AppendVisibleBlipMatrix(Matrix4x4.TRS(worldPosition, planeRotation, planeScale), ref visibleCount);
            }
        }

        private bool TryResolvePlayerDepthMeters(out float depthMeters)
        {
            IPlayerRuntimeContext playerContext = _cachedPlayerContext;
            if (playerContext != null &&
                playerContext.TryGetMovementRuntimeState(out PlayerMovementRuntimeState movementState) &&
                (movementState.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) != 0u &&
                math.isfinite(movementState.DepthMeters))
            {
                depthMeters = math.max(0f, movementState.DepthMeters);
                return true;
            }

            depthMeters = 0f;
            return false;
        }

        private static uint HashThermalNoiseGhost(uint cycleIndex, uint depthBucket, uint ordinal)
        {
            uint hash = ThermalNoiseHashSalt;
            hash ^= MixHash(cycleIndex + 0x9E3779B9u);
            hash ^= MixHash(depthBucket + 0x85EBCA6Bu);
            hash ^= MixHash(ordinal + 0xC2B2AE35u);
            return MixHash(hash);
        }

        private static Vector2 ResolveThermalGhostDirection(uint hash)
        {
            const float Diagonal = 0.70710678f;
            switch ((int)((hash >> 8) & 0x0Fu))
            {
                case 0: return MakeVector2(0f, 1f);
                case 1: return MakeVector2(Diagonal, Diagonal);
                case 2: return MakeVector2(1f, 0f);
                case 3: return MakeVector2(Diagonal, -Diagonal);
                case 4: return MakeVector2(0f, -1f);
                case 5: return MakeVector2(-Diagonal, -Diagonal);
                case 6: return MakeVector2(-1f, 0f);
                case 7: return MakeVector2(-Diagonal, Diagonal);
                case 8: return MakeVector2(0.38268343f, 0.9238795f);
                case 9: return MakeVector2(0.9238795f, 0.38268343f);
                case 10: return MakeVector2(0.9238795f, -0.38268343f);
                case 11: return MakeVector2(0.38268343f, -0.9238795f);
                case 12: return MakeVector2(-0.38268343f, -0.9238795f);
                case 13: return MakeVector2(-0.9238795f, -0.38268343f);
                case 14: return MakeVector2(-0.9238795f, 0.38268343f);
                default: return MakeVector2(-0.38268343f, 0.9238795f);
            }
        }

        private float3 ResolveRadarForwardBucket(Vector3 cameraForward)
        {
            float3 flatForward = MakeFloat3(cameraForward.x, 0f, cameraForward.z);
            if (math.lengthsq(flatForward) <= 0.0001f)
                return _lastRadarForwardBucket;

            const float Diagonal = 0.70710677f;
            float absX = math.abs(flatForward.x);
            float absZ = math.abs(flatForward.z);
            float signX = flatForward.x < 0f ? -1f : 1f;
            float signZ = flatForward.z < 0f ? -1f : 1f;
            float3 bucket;
            if (absX > absZ * 2f)
                bucket = MakeFloat3(signX, 0f, 0f);
            else if (absZ > absX * 2f)
                bucket = MakeFloat3(0f, 0f, signZ);
            else
                bucket = MakeFloat3(signX * Diagonal, 0f, signZ * Diagonal);

            _lastRadarForwardBucket = bucket;
            return bucket;
        }

        private static uint MixHash(uint value)
        {
            value ^= value >> 16;
            value *= 0x7FEB352Du;
            value ^= value >> 15;
            value *= 0x846CA68Bu;
            value ^= value >> 16;
            return value;
        }

        private static float SmoothStep01(float value)
        {
            float t = SanitizeQualityWeight01(value);
            return t * t * (3f - 2f * t);
        }

        private static float SanitizeQualityWeight01(float value)
        {
            return math.saturate(math.isfinite(value) ? value : 0f);
        }

        private static int ResolveQualityCapacity(float qualityWeight01, int minimum, int maximum)
        {
            int safeMinimum = math.max(0, minimum);
            int safeMaximum = math.max(safeMinimum, maximum);
            float qualityCurve = SmoothStep01(qualityWeight01);
            return math.clamp(
                (int)math.round(math.lerp(safeMinimum, safeMaximum, qualityCurve)),
                safeMinimum,
                safeMaximum);
        }

        private void ResolvePlayerTransform()
        {
            if (_playerTransform != null)
                return;

            IPlayerRuntimeContext playerContext = _cachedPlayerContext;
            if (playerContext != null && playerContext.PlayerTransform != null)
            {
                AssignPlayerTransform(playerContext.PlayerTransform);
                return;
            }

            int frame = Hecton8.Core.SystemDispatcher.CurrentFrameIndex;
            if (frame < _nextPlayerTransformResolveFrame)
                return;

            _nextPlayerTransformResolveFrame = frame + PlayerTransformResolveIntervalFrames;

            if (GameBootstrapper.TryGetCurrentPlayerTransform(out Transform scenePlayerTransform))
                AssignPlayerTransform(scenePlayerTransform);
        }

        private void AssignPlayerTransform(Transform playerTransform)
        {
            if (_playerTransform == playerTransform)
                return;

            _playerTransform = playerTransform;
            _nextPlayerTransformResolveFrame = 0;
        }

        private void ResolveProjectionCamera()
        {
            if (IsProjectionCameraUsable(_projectionCamera, _projectionCameraRequiresHudLayer))
                return;

            _projectionCamera = null;
            _projectionCameraRequiresHudLayer = false;

            SuitHUDV4CanvasOverlay overlay = null;
            SuitHUDV4CanvasOverlay.TryResolveActiveRuntime(ref overlay);
            if (overlay != null)
            {
                if (TryAssignProjectionCamera(overlay.ProjectionCamera, false))
                    return;

                Canvas canvas = overlay.TargetCanvas;
                if (canvas != null && TryAssignProjectionCamera(canvas.worldCamera, false))
                    return;
            }

            IPlayerRuntimeContext playerContext = _cachedPlayerContext;
            if (playerContext != null && TryAssignProjectionCamera(playerContext.PlayerCamera, true))
                return;

            TryAssignProjectionCamera(GlobalRenderContext.CurrentCamera, true);
        }

        private bool TryAssignProjectionCamera(Camera candidate, bool requireHudLayer)
        {
            if (!IsProjectionCameraUsable(candidate, requireHudLayer))
                return false;

            _projectionCamera = candidate;
            _projectionCameraRequiresHudLayer = requireHudLayer;
            return true;
        }

        private static bool IsProjectionCameraUsable(Camera candidate, bool requireHudLayer)
        {
            return candidate != null &&
                   candidate.isActiveAndEnabled &&
                   (!requireHudLayer || (candidate.cullingMask & (1 << HudInternalLayerIndex)) != 0);
        }

        /// <summary>
        /// Resolves the authored blip draw pair, or reports the authoring gap once without throwing.
        /// </summary>
        /// <remarks>
        /// The three <c>UnityEngine.Assertions.Assert</c> calls removed from this method THREW - nothing under
        /// Assets sets <c>Assert.raiseExceptions = false</c> - and both callers reach here BEFORE they register
        /// anything: <see cref="OnEnable"/> (:159) and <see cref="Start"/> (:169) each call this and only then
        /// <see cref="TryRegisterScanEvents"/> and <see cref="TryRegister"/>. An unassigned inspector slot
        /// therefore cost far more than a skipped draw - it threw out of the Unity message and skipped
        /// <c>ScanEvents.Register(this)</c>, <c>SystemDispatcher.Register((ILateFrameTickable)this,
        /// PriorityLayer.UI)</c> and <c>GlobalRegistry.Renderables.TryRegister(this)</c>. The controller never
        /// received a scan event and never ticked again for the session, so assigning the asset later in
        /// play-mode could not recover it.
        ///
        /// The asserts guarded nothing: <c>_radarBlipMesh</c> is deliberately left null on an invalid pair and
        /// <see cref="AdvanceRadarPresentation"/> already treats that as "clear the handoff and return"
        /// (:203-208), as does the second guard at :287.
        /// </remarks>
        private void EnsureRuntimeResources()
        {
            if (!Application.isPlaying)
                return;

            EnsureRadarBlipPropertiesCold();
            bool meshAssigned = radarBlipMesh != null;
            bool materialAssigned = radarBlipMaterial != null;
            bool authoredMeshValid = meshAssigned && radarBlipMesh.subMeshCount > 0 && radarBlipMesh.GetIndexCount(0) > 0u;
            bool authoredMaterialValid = materialAssigned && radarBlipMaterial.enableInstancing;
            _radarBlipMesh = authoredMeshValid && authoredMaterialValid ? radarBlipMesh : null;
            _radarBlipMaterialPropertiesDirty = true;

            // Report LAST and once. Both callers continue to their registration calls after this returns, so a
            // future re-introduced throw here can no longer strand scan events or the UI late-frame lane.
            if ((authoredMeshValid && authoredMaterialValid) || _missingBlipDrawAssetsAnnounced)
                return;

            _missingBlipDrawAssetsAnnounced = true;
            LogInvalidRadarBlipDrawAssets(meshAssigned, authoredMeshValid, materialAssigned, authoredMaterialValid);
        }

        /// <summary>
        /// One-shot report of an unusable authored blip pair. The latch guarantees single emission and every
        /// parameter is a primitive, so no string work and no allocation reaches the late-frame cadence.
        /// </summary>
        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogInvalidRadarBlipDrawAssets(
            bool meshAssigned,
            bool authoredMeshValid,
            bool materialAssigned,
            bool authoredMaterialValid)
        {
            if (!meshAssigned)
            {
                Hecton8.Core.H8Debug.LogError("FakeRadarBlipController: serialized field 'radarBlipMesh' is unassigned. No radar blip renders this session - AdvanceRadarPresentation clears the visible-blip handoff and returns. Scan events, the UI late-frame tick and the renderable registration all stay live. Assign the authored blip quad mesh in the inspector.");
            }
            else if (!authoredMeshValid)
            {
                Hecton8.Core.H8Debug.LogError("FakeRadarBlipController: the mesh assigned to 'radarBlipMesh' has no indexed submesh 0 (subMeshCount is 0 or GetIndexCount(0) is 0), so the instanced blip draw would submit no triangles. No radar blip renders this session. Reimport or replace that mesh asset with one that carries an index buffer.");
            }

            if (!materialAssigned)
            {
                Hecton8.Core.H8Debug.LogError("FakeRadarBlipController: serialized field 'radarBlipMaterial' is unassigned. No radar blip renders this session - AdvanceRadarPresentation clears the visible-blip handoff and returns. Runtime material generation is forbidden: assign the authored radar blip material in the inspector.");
                return;
            }

            if (!authoredMaterialValid)
            {
                Hecton8.Core.H8Debug.LogError("FakeRadarBlipController: the material assigned to 'radarBlipMaterial' has Enable GPU Instancing OFF, which the instanced blip draw requires. No radar blip renders this session. Tick 'Enable GPU Instancing' on that material asset.");
            }
        }

        private void EnsureRadarBlipPropertiesCold()
        {
            if (_radarBlipProperties != null)
                return;

            // COLD ALLOC: MaterialPropertyBlock[1] - fake radar blip instanced draw payload - owner: FakeRadarBlipController.
            _radarBlipProperties = new MaterialPropertyBlock();
            _radarBlipMaterialPropertiesDirty = true;
        }

        private void DisposeRuntimeResources()
        {
            ClearScheduledCullState();

            _radarBlipMesh = null;

            _radarBlipProperties?.Clear();
            _radarBlipMaterialPropertiesDirty = true;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private void PublishRadarSolveWarningIfNeeded(long startTimestamp)
        {
            long elapsedTicks = Stopwatch.GetTimestamp() - startTimestamp;
            double elapsedMilliseconds = elapsedTicks * 1000.0d / Stopwatch.Frequency;
            if (elapsedMilliseconds <= RadarSolveBudgetWarningMilliseconds || Hecton8.Core.SystemDispatcher.CurrentFrameIndex < _nextRadarPerformanceWarningFrame)
                return;

            GlobalTelemetryBus.PublishPerformanceWarning(
                RadarSolveBudgetWarningHash,
                RadarSolveBudgetContextHash,
                (float)elapsedMilliseconds);
            _nextRadarPerformanceWarningFrame = Hecton8.Core.SystemDispatcher.CurrentFrameIndex + RadarPerformanceWarningCooldownFrames;
        }
#endif

        private static void ResolveCameraPlaneMetrics(
            Camera camera,
            float distance,
            out float worldPerPixel,
            out float halfWidth,
            out float halfHeight)
        {
            if (camera.orthographic)
            {
                halfHeight = math.max(0.001f, camera.orthographicSize);
                halfWidth = halfHeight * math.max(0.001f, camera.aspect);
            }
            else
            {
                float projectionY = math.abs(camera.projectionMatrix.m11);
                halfHeight = math.max(0.001f, distance) / math.max(0.001f, projectionY);
                halfWidth = halfHeight * math.max(0.001f, camera.aspect);
            }

            worldPerPixel = (halfHeight * 2f) / math.max(1, camera.pixelHeight);
        }

        private void TryRegister()
        {
            if (!Application.isPlaying)
                return;

            if (!_registeredLateFrame)
                _registeredLateFrame = SystemDispatcher.Register((ILateFrameTickable)this, PriorityLayer.UI);

            if (!_registeredRenderable)
                _registeredRenderable = GlobalRegistry.Renderables.TryRegister(this);
        }

        private void TryRegisterScanEvents()
        {
            if (_scanEventsRegistered || !Application.isPlaying)
                return;

            ScanEvents.Register(this);
            _scanEventsRegistered = true;
        }

        private void TryUnregisterScanEvents()
        {
            if (!_scanEventsRegistered)
                return;

            ScanEvents.Unregister(this);
            _scanEventsRegistered = false;
        }

        private void TryUnregister()
        {
            if (_registeredLateFrame)
            {
                SystemDispatcher.UnregisterLateFrameTickableDirect(this, PriorityLayer.UI);
                _registeredLateFrame = false;
            }

            if (_registeredRenderable)
            {
                GlobalRegistry.Renderables.Unregister(this);
                _registeredRenderable = false;
            }
        }
    }
}
