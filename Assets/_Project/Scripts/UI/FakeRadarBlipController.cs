using Hecton8.AI;
using Hecton8.Bootstrap;
using Hecton8.Core;
using Hecton8.Gameplay;
using Hecton8.World;
using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using Stopwatch = System.Diagnostics.Stopwatch;
#endif
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Hecton8.UI
{
    /// <summary>
    /// HUD-only enemy radar fake: spatial hash contacts, flat XZ math, one instanced mesh draw.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/UI/Fake Radar Blip Controller")]
    public sealed class FakeRadarBlipController : MonoBehaviour, IUpdatable, ILateFrameTickable, IRenderable, IScanEventListener, IGlobalRegistryHotSwapListener
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
        private const int SurvivalSystemResolveIntervalFrames = 30;
        private const int PlayerTransformResolveIntervalFrames = 30;
        private const int MinimumQualityBlipCapacity = 16;
        private const uint ThermalNoiseHashSalt = 0x54484E31u;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private const double RadarSolveBudgetWarningMilliseconds = 0.1d;
        private const int RadarPerformanceWarningCooldownFrames = 30;
#endif
        private const string RadarBlipShaderPath = "Assets/_Project/Art/Shaders/Hecton_RadarBlipInstanced.shader";
        private const string RadarBlipShaderName = "HECTON/HUD/RadarBlipInstanced";
        private const string NativeMemoryOwner = nameof(FakeRadarBlipController);
        private const Allocator DataVaultExemptRadarCullAllocator = Allocator.Persistent;
        private const Allocator DataVaultExemptRadarRenderHandoffAllocator = Allocator.Persistent;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private const uint RadarSolveBudgetWarningHash = 648937224u;
        private const uint RadarSolveBudgetContextHash = 2418241056u;
#endif

        private static readonly int _BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int _FlickerFrequencyId = Shader.PropertyToID("_FlickerFrequency");
        private static readonly int _FlickerIntensityId = Shader.PropertyToID("_FlickerIntensity");
        private static readonly int _FillAlphaId = Shader.PropertyToID("_FillAlpha");
        // COLD ALLOC: Vector2[16] — deterministic thermal ghost direction LUT — owner: FakeRadarBlipController
        private static readonly Vector2[] s_thermalGhostDirections = CreateThermalGhostDirections();

        [StructLayout(LayoutKind.Explicit, Size = 8)]
        private struct RadarCullCandidate
        {
            [FieldOffset(0)]
            public float2 FlatDelta;
        }

        [StructLayout(LayoutKind.Explicit, Size = 16)]
        private struct RadarCullResult
        {
            [FieldOffset(0)]
            public float2 PlaneOffset;
            [FieldOffset(8)]
            public int Visible;
            [FieldOffset(12)]
            public int Padding;
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private struct RadarBlip2DCullJob : IJobParallelFor
        {
            [ReadOnly, NoAlias] public NativeArray<RadarCullCandidate> Candidates;
            [WriteOnly, NoAlias] public NativeArray<RadarCullResult> Results;
            public float2 RadarCenter;
            public float2 BoundsMin;
            public float2 BoundsMax;
            public float ScreenCircleSourceRadiusSqr;
            public float RadarScale;

            public void Execute(int index)
            {
                RadarCullCandidate candidate = Candidates[index];
                RadarCullResult result = default;
                float distanceSqr = math.lengthsq(candidate.FlatDelta);
                if (distanceSqr > 0.0001f && distanceSqr <= ScreenCircleSourceRadiusSqr)
                {
                    float2 planeOffset = RadarCenter + candidate.FlatDelta * RadarScale;
                    bool insideBounds =
                        planeOffset.x >= BoundsMin.x &&
                        planeOffset.x <= BoundsMax.x &&
                        planeOffset.y >= BoundsMin.y &&
                        planeOffset.y <= BoundsMax.y;

                    if (insideBounds)
                    {
                        result.PlaneOffset = planeOffset;
                        result.Visible = 1;
                    }
                }

                Results[index] = result;
            }
        }

        [SerializeField, Min(1f)] private float radarRangeMeters = DefaultRadarRangeMeters;
        [SerializeField, Min(1f)] private float radarRadiusPixels = DefaultRadarRadiusPixels;
        [SerializeField, Min(1f)] private float blipSizePixels = BlipSizePixels;
        [SerializeField] private Vector2 radarCenterInsetPixels = new Vector2(DefaultRadarCenterInsetX, DefaultRadarCenterInsetY);
        [SerializeField] private Shader radarBlipShader;
        [SerializeField] private Color blipColor = new Color(1f, 0.24f, 0.28f, 0.92f);

        // COLD ALLOC: SpatialQueryHit[64] — fixed hostile radar query buffer — owner: FakeRadarBlipController
        private readonly SpatialQueryHit[] _queryHits = new SpatialQueryHit[MaxBlips];
        // COLD ALLOC: Matrix4x4[64] — instanced hostile radar blip matrices — owner: FakeRadarBlipController
        private readonly Matrix4x4[] _blipMatrices = new Matrix4x4[MaxBlips];

        private bool _registered;
        private bool _registeredLateFrame;
        private bool _registeredRenderable;
        private bool _scanEventsRegistered;
        private bool _hotSwapListenerRegistered;
        private bool _radarCullScheduled;
        private bool _radarBlipMaterialPropertiesDirty = true;
        private int _scheduledCandidateCount;
        private int _scheduledBlipCapacity = MaxBlips;
        private int _qualityBlipCapacity = MaxBlips;
        private int _qualityThermalGhostCapacity = ThermalNoiseMaxGhostBlips;
        private Transform _playerTransform;
        private HectonSurvivalSystem _survivalSystem;
        private IPlayerRuntimeContext _cachedPlayerContext;
        private int _nextPlayerTransformResolveFrame;
        private int _nextSurvivalSystemResolveFrame;
        private Camera _projectionCamera;
        private bool _projectionCameraRequiresHudLayer;
        private Mesh _radarBlipMesh;
        private Material _radarBlipMaterial;
        private Color _appliedRadarBlipColor;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private int _nextRadarPerformanceWarningFrame;
#endif
        private NativeArray<RadarCullCandidate> _radarCullCandidates;
        private NativeArray<RadarCullResult> _radarCullResults;
        private NativeList<Matrix4x4> _visibleBlipMatrices;
        private JobHandle _radarCullHandle;
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
        private float3 _lastRadarForwardBucket = new float3(0f, 0f, 1f);
        private float _scheduledRadarRadius;
        private float _scheduledWorldPerPixel;
        private int _visibleBlipMatrixCount;
        private int _discardedContactsWithoutAupCount;
        private bool _blipMatricesDirty;
        private bool _discardScheduledCullResult;

        public int DiscardedContactsWithoutAupCount => _discardedContactsWithoutAupCount;

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

        public void Tick(float deltaTime)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            long tickStart = Stopwatch.GetTimestamp();
#endif

            try
            {
                float safeDeltaTime = math.isfinite(deltaTime) ? math.max(0f, deltaTime) : 0f;
                RefreshQualityPolicy();
                AdvanceThermalNoiseClock(safeDeltaTime);
                ResolvePlayerTransform();
                ResolveProjectionCamera();

                if (_projectionCamera == null ||
                    _radarBlipMesh == null ||
                    _radarBlipMaterial == null ||
                    !_radarCullCandidates.IsCreated ||
                    !_radarCullResults.IsCreated ||
                    !_visibleBlipMatrices.IsCreated)
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
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            long tickStart = Stopwatch.GetTimestamp();
#endif

            try
            {
                if (!_radarCullScheduled)
                {
                    return;
                }

                int visibleCount = CompleteScheduledBlipCull();
                if (visibleCount < 0)
                    return;

                bool discardCompletedCull = _discardScheduledCullResult;
                _radarCullScheduled = false;
                _scheduledCandidateCount = 0;
                _radarCullHandle = default;
                _discardScheduledCullResult = false;

                if (discardCompletedCull)
                {
                    ClearVisibleBlipHandoff();
                    return;
                }

                _visibleBlipMatrixCount = visibleCount;
                _blipMatricesDirty = visibleCount > 0;
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
                !_visibleBlipMatrices.IsCreated ||
                _projectionCamera == null ||
                _radarBlipMesh == null ||
                _radarBlipMaterial == null)
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

            int visibleCount = math.min(_visibleBlipMatrixCount, _visibleBlipMatrices.Length);
            if (visibleCount <= 0)
                return;

            if (_blipMatricesDirty)
            {
                for (int i = 0; i < visibleCount; i++)
                    _blipMatrices[i] = _visibleBlipMatrices[i];
                _blipMatricesDirty = false;
            }

            DrawBlipMatrices(renderCamera, visibleCount);
        }

        private void ClearVisibleBlipHandoff()
        {
            bool hasVisibleNativeBlips = _visibleBlipMatrices.IsCreated && _visibleBlipMatrices.Length > 0;
            if (_visibleBlipMatrixCount == 0 && !_blipMatricesDirty && !hasVisibleNativeBlips)
                return;

            _visibleBlipMatrixCount = 0;
            _blipMatricesDirty = false;
            if (hasVisibleNativeBlips)
                _visibleBlipMatrices.Clear();
        }

        private void AppendVisibleBlipMatrix(Matrix4x4 matrix, ref int visibleCount)
        {
            int blipCapacity = math.clamp(_scheduledBlipCapacity, MinimumQualityBlipCapacity, MaxBlips);
            if (!_visibleBlipMatrices.IsCreated || visibleCount >= blipCapacity)
            {
                visibleCount = math.min(visibleCount, blipCapacity);
                return;
            }

            _visibleBlipMatrices.AddNoResize(matrix);
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
                _radarCullHandle = default;
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
            Vector2 radarCenter = new Vector2(
                halfWidth - math.max(0f, radarCenterInsetPixels.x) * worldPerPixel,
                -halfHeight + math.max(0f, radarCenterInsetPixels.y) * worldPerPixel);
            float blipWorldSize = math.max(1f, blipSizePixels) * worldPerPixel;
            float blipHalfExtent = blipWorldSize * 0.5f;
            float radarRadiusWorld = radius * worldPerPixel;
            float2 boundsMin = new float2(-halfWidth - blipHalfExtent, -halfHeight - blipHalfExtent);
            float2 boundsMax = new float2(halfWidth + blipHalfExtent, halfHeight + blipHalfExtent);
            Quaternion planeRotation = cameraTransform.rotation;
            Vector3 planeScale = new Vector3(blipWorldSize, blipWorldSize, 1f);
            _scheduledPlaneCenter = planeCenter;
            _scheduledCameraRight = cameraRight;
            _scheduledCameraUp = cameraUp;
            _scheduledPlaneRotation = planeRotation;
            _scheduledPlaneScale = planeScale;
            _scheduledRadarCenter = radarCenter;
            _scheduledRadarRadius = radius;
            _scheduledWorldPerPixel = worldPerPixel;
            float3 radarForwardFlatF3 = ResolveRadarForwardBucket(cameraForward);
            float3 radarRightFlatF3 = new float3(radarForwardFlatF3.z, 0f, -radarForwardFlatF3.x);

            int candidateCount = 0;
            for (int i = 0; i < hitCount && candidateCount < blipCapacity; i++)
            {
                SpatialQueryHit hit = _queryHits[i];
                if (!(hit.Owner is FaunaBrain brain) || !brain.isAggressive)
                    continue;

                if (!hit.HasAbsolutePosition)
                {
                    IncrementCounterSaturated(ref _discardedContactsWithoutAupCount);
                    continue;
                }

                AbsoluteUniversePosition hitAup = hit.AbsolutePosition;
                float3 enemyDeltaAup = AbsoluteUniversePosition.ToCameraRelativeFloat3(in hitAup, in playerAup);
                float2 flatDelta = new float2(
                    math.dot(enemyDeltaAup, radarRightFlatF3),
                    math.dot(enemyDeltaAup, radarForwardFlatF3));

                _radarCullCandidates[candidateCount] = new RadarCullCandidate
                {
                    FlatDelta = flatDelta
                };
                candidateCount++;
            }

            _scheduledCandidateCount = candidateCount;
            _scheduledBlipCapacity = blipCapacity;
            _radarCullScheduled = true;
            _discardScheduledCullResult = false;
            if (candidateCount > 0)
            {
                _radarCullHandle = new RadarBlip2DCullJob
                {
                    Candidates = _radarCullCandidates,
                    Results = _radarCullResults,
                    RadarCenter = new float2(radarCenter.x, radarCenter.y),
                    BoundsMin = boundsMin,
                    BoundsMax = boundsMax,
                    ScreenCircleSourceRadiusSqr = rangeSqr,
                    RadarScale = invRange * radarRadiusWorld
                }.Schedule(candidateCount, 32);
            }
            else
            {
                _radarCullHandle = default;
            }
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot == GlobalRegistryServiceSlot.Dispatcher)
            {
                if (currentService != null && isActiveAndEnabled)
                    TryRegister();
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
            float qualityWeight01 = HomeostasisBrain.GlobalQualityWeight;
            float qualityCurve = SmoothStep01(qualityWeight01);
            _qualityBlipCapacity = math.clamp(
                (int)math.round(math.lerp(MinimumQualityBlipCapacity, MaxBlips, qualityCurve)),
                MinimumQualityBlipCapacity,
                MaxBlips);
            _qualityThermalGhostCapacity = math.clamp(
                (int)math.round(math.lerp(0f, ThermalNoiseMaxGhostBlips, qualityCurve)),
                0,
                ThermalNoiseMaxGhostBlips);
        }

        private bool TryResolvePlayerAup(out AbsoluteUniversePosition playerAup)
        {
            if (PlayerRuntimeContextService.TryGetActiveRuntimeContext(out PlayerRuntimeContext runtimeContext))
            {
                PlayerMovementRuntimeState movementState = runtimeContext.MovementState;
                if ((movementState.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) != 0u)
                {
                    playerAup = movementState.PredictedAup;
                    return true;
                }
            }

            IPlayerRuntimeContext playerContext = _cachedPlayerContext;
            HectonPlayerMovement playerMovement = playerContext != null ? playerContext.PlayerMovement : null;
            if (playerMovement != null)
            {
                playerAup = playerMovement.CurrentAup;
                return true;
            }

            playerAup = default;
            return false;
        }

        private int CompleteScheduledBlipCull()
        {
            int visibleCount = 0;
            if (!_visibleBlipMatrices.IsCreated)
                return 0;

            if (_scheduledCandidateCount > 0)
            {
                if (!DispatcherJobSwap.TryFinalizeCompleted(ref _radarCullHandle))
                    return -1;
            }

            if (_visibleBlipMatrices.Length > 0)
                _visibleBlipMatrices.Clear();
            if (_scheduledCandidateCount > 0)
            {
                int blipCapacity = math.clamp(_scheduledBlipCapacity, MinimumQualityBlipCapacity, MaxBlips);
                for (int i = 0; i < _scheduledCandidateCount && visibleCount < blipCapacity; i++)
                {
                    RadarCullResult cullResult = _radarCullResults[i];
                    if (cullResult.Visible == 0)
                        continue;

                    Vector2 planeOffset = new Vector2(cullResult.PlaneOffset.x, cullResult.PlaneOffset.y);
                    Vector3 worldPosition = _scheduledPlaneCenter + _scheduledCameraRight * planeOffset.x + _scheduledCameraUp * planeOffset.y;
                    AppendVisibleBlipMatrix(Matrix4x4.TRS(worldPosition, _scheduledPlaneRotation, _scheduledPlaneScale), ref visibleCount);
                }
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
            return visibleCount;
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
            Vector3 lineScale = new Vector3(
                radarRadiusWorld * 2f,
                math.max(1f, WreckSignalDistortionThicknessPixels) * _scheduledWorldPerPixel * (0.65f + 0.95f * pulse01),
                1f);
            AppendVisibleBlipMatrix(Matrix4x4.TRS(worldPosition, _scheduledPlaneRotation, lineScale), ref visibleCount);
        }

        private void DrawBlipMatrices(Camera renderCamera, int visibleCount)
        {
            ApplyRadarBlipMaterialProperties();

            UnityEngine.Graphics.DrawMeshInstanced(
                _radarBlipMesh,
                0,
                _radarBlipMaterial,
                _blipMatrices,
                visibleCount,
                null,
                ShadowCastingMode.Off,
                false,
                HudInternalLayerIndex,
                renderCamera,
                LightProbeUsage.Off,
                null);
        }

        private void ApplyRadarBlipMaterialProperties()
        {
            if (!_radarBlipMaterialPropertiesDirty && ColorsMatch(_appliedRadarBlipColor, blipColor))
                return;

            _radarBlipMaterial.SetColor(_BaseColorId, blipColor);
            _radarBlipMaterial.SetFloat(_FlickerFrequencyId, BlipFlickerFrequency);
            _radarBlipMaterial.SetFloat(_FlickerIntensityId, BlipFlickerIntensity);
            _radarBlipMaterial.SetFloat(_FillAlphaId, BlipFillAlpha);
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

        private JobHandle BuildOutstandingCullDisposeDependency()
        {
            if (!_radarCullScheduled)
                return default;

            JobHandle dependency = _scheduledCandidateCount > 0 ? _radarCullHandle : default;
            _radarCullScheduled = false;
            _scheduledCandidateCount = 0;
            _radarCullHandle = default;
            _discardScheduledCullResult = false;
            ClearVisibleBlipHandoff();
            return dependency;
        }

        private void DrainScheduledCullForDisable()
        {
            if (!_radarCullScheduled)
                return;

            if (_scheduledCandidateCount <= 0 ||
                DispatcherJobSwap.TryFinalizeCompleted(ref _radarCullHandle))
            {
                _radarCullScheduled = false;
                _scheduledCandidateCount = 0;
                _radarCullHandle = default;
                _discardScheduledCullResult = false;
                return;
            }

            _discardScheduledCullResult = true;
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
            if (_survivalSystem == null || visibleCount >= blipCapacity || ghostCapacity <= 0)
                return;

            float depthMeters = math.max(0f, _survivalSystem.Depth);
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
            return s_thermalGhostDirections[(int)((hash >> 8) & 0x0Fu)];
        }

        private static Vector2[] CreateThermalGhostDirections()
        {
            const float Diagonal = 0.70710678f;
            // COLD ALLOC: Vector2[16] — deterministic thermal ghost direction LUT backing store — owner: FakeRadarBlipController
            return new Vector2[]
            {
                new Vector2(0f, 1f),
                new Vector2(Diagonal, Diagonal),
                new Vector2(1f, 0f),
                new Vector2(Diagonal, -Diagonal),
                new Vector2(0f, -1f),
                new Vector2(-Diagonal, -Diagonal),
                new Vector2(-1f, 0f),
                new Vector2(-Diagonal, Diagonal),
                new Vector2(0.38268343f, 0.9238795f),
                new Vector2(0.9238795f, 0.38268343f),
                new Vector2(0.9238795f, -0.38268343f),
                new Vector2(0.38268343f, -0.9238795f),
                new Vector2(-0.38268343f, -0.9238795f),
                new Vector2(-0.9238795f, -0.38268343f),
                new Vector2(-0.9238795f, 0.38268343f),
                new Vector2(-0.38268343f, 0.9238795f)
            };
        }

        private float3 ResolveRadarForwardBucket(Vector3 cameraForward)
        {
            float3 flatForward = new float3(cameraForward.x, 0f, cameraForward.z);
            if (math.lengthsq(flatForward) <= 0.0001f)
                return _lastRadarForwardBucket;

            const float Diagonal = 0.70710677f;
            float absX = math.abs(flatForward.x);
            float absZ = math.abs(flatForward.z);
            float signX = flatForward.x < 0f ? -1f : 1f;
            float signZ = flatForward.z < 0f ? -1f : 1f;
            float3 bucket;
            if (absX > absZ * 2f)
                bucket = new float3(signX, 0f, 0f);
            else if (absZ > absX * 2f)
                bucket = new float3(0f, 0f, signZ);
            else
                bucket = new float3(signX * Diagonal, 0f, signZ * Diagonal);

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
            float t = math.saturate(math.isfinite(value) ? value : 1f);
            return t * t * (3f - 2f * t);
        }

        private void ResolvePlayerTransform()
        {
            if (_playerTransform != null)
            {
                ResolveSurvivalSystemForCachedPlayer();
                return;
            }

            IPlayerRuntimeContext playerContext = _cachedPlayerContext;
            if (playerContext != null && playerContext.PlayerTransform != null)
            {
                AssignPlayerTransform(playerContext.PlayerTransform);
                ResolveSurvivalSystemForCachedPlayer();
                return;
            }

            int frame = Time.frameCount;
            if (frame < _nextPlayerTransformResolveFrame)
                return;

            _nextPlayerTransformResolveFrame = frame + PlayerTransformResolveIntervalFrames;

            if (GameBootstrapper.TryGetCurrentPlayerTransform(out Transform scenePlayerTransform))
            {
                AssignPlayerTransform(scenePlayerTransform);
                ResolveSurvivalSystemForCachedPlayer();
            }
        }

        private void AssignPlayerTransform(Transform playerTransform)
        {
            if (_playerTransform == playerTransform)
                return;

            _playerTransform = playerTransform;
            _survivalSystem = null;
            _nextPlayerTransformResolveFrame = 0;
            _nextSurvivalSystemResolveFrame = 0;
        }

        private void ResolveSurvivalSystemForCachedPlayer()
        {
            if (_survivalSystem != null || _playerTransform == null)
            {
                return;
            }

            int frame = Time.frameCount;
            if (frame < _nextSurvivalSystemResolveFrame)
                return;

            _nextSurvivalSystemResolveFrame = frame + SurvivalSystemResolveIntervalFrames;
            _playerTransform.TryGetComponent(out _survivalSystem);
            if (_survivalSystem != null)
                _nextSurvivalSystemResolveFrame = 0;
        }

        private void ResolveProjectionCamera()
        {
            if (IsProjectionCameraUsable(_projectionCamera, _projectionCameraRequiresHudLayer))
                return;

            _projectionCamera = null;
            _projectionCameraRequiresHudLayer = false;

            SuitHUDV4CanvasOverlay overlay = SuitHUDV4CanvasOverlay.ActiveRuntimeInstance;
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

        private void EnsureRuntimeResources()
        {
            if (!Application.isPlaying)
                return;

#if UNITY_EDITOR
            if (radarBlipShader == null)
                radarBlipShader = AssetDatabase.LoadAssetAtPath<Shader>(RadarBlipShaderPath);
#endif
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (radarBlipShader == null)
                radarBlipShader = Shader.Find(RadarBlipShaderName);
#endif

            if (_radarBlipMesh == null)
                _radarBlipMesh = BuildQuadMesh();

            if (_radarBlipMaterial == null && radarBlipShader != null)
            {
                _radarBlipMaterial = new Material(radarBlipShader)
                {
                    enableInstancing = true
                }; // COLD ALLOC: Material[1] — instanced hostile radar blip material — owner: FakeRadarBlipController
                _radarBlipMaterialPropertiesDirty = true;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                _radarBlipMaterial.name = "HUD_FakeRadarBlips_Instanced_Runtime";
#endif
            }

            if (!_radarCullCandidates.IsCreated)
            {
                _radarCullCandidates = new NativeArray<RadarCullCandidate>(MaxBlips, DataVaultExemptRadarCullAllocator, NativeArrayOptions.UninitializedMemory); // COLD ALLOC: NativeArray<RadarCullCandidate>[64] — Burst 2D HUD bounds cull input — owner: FakeRadarBlipController
                NativeMemorySentinel.RegisterNativeArray(_radarCullCandidates, NativeMemoryOwner, nameof(_radarCullCandidates), NativeAllocationLifetime.Scene);
            }

            if (!_radarCullResults.IsCreated)
            {
                _radarCullResults = new NativeArray<RadarCullResult>(MaxBlips, DataVaultExemptRadarCullAllocator, NativeArrayOptions.UninitializedMemory); // COLD ALLOC: NativeArray<RadarCullResult>[64] — Burst 2D HUD bounds cull output — owner: FakeRadarBlipController
                NativeMemorySentinel.RegisterNativeArray(_radarCullResults, NativeMemoryOwner, nameof(_radarCullResults), NativeAllocationLifetime.Scene);
            }

            if (!_visibleBlipMatrices.IsCreated)
            {
                _visibleBlipMatrices = new NativeList<Matrix4x4>(MaxBlips, DataVaultExemptRadarRenderHandoffAllocator); // COLD ALLOC: NativeList<Matrix4x4>[64] — LateFrame radar render handoff to GlobalRenderContext — owner: FakeRadarBlipController
                NativeMemorySentinel.RegisterNativeList(_visibleBlipMatrices, NativeMemoryOwner, nameof(_visibleBlipMatrices), NativeAllocationLifetime.Scene);
            }
        }

        private void DisposeRuntimeResources()
        {
            JobHandle nativeDisposeDependency = BuildOutstandingCullDisposeDependency();

            if (_radarBlipMaterial != null)
            {
                DestroyUnityObject(_radarBlipMaterial);
                _radarBlipMaterial = null;
                _radarBlipMaterialPropertiesDirty = true;
            }

            if (_radarBlipMesh != null)
            {
                DestroyUnityObject(_radarBlipMesh);
                _radarBlipMesh = null;
            }

            nativeDisposeDependency = DisposeNativeArray(ref _radarCullCandidates, nativeDisposeDependency);
            nativeDisposeDependency = DisposeNativeArray(ref _radarCullResults, nativeDisposeDependency);
            DisposeNativeList(ref _visibleBlipMatrices, nameof(_visibleBlipMatrices), nativeDisposeDependency);
        }

        private static void DestroyUnityObject(UnityEngine.Object instance)
        {
            if (instance == null)
                return;

            if (Application.isPlaying)
                Destroy(instance);
            else
                DestroyImmediate(instance);
        }

        private static JobHandle DisposeNativeArray<T>(ref NativeArray<T> array, JobHandle dependency) where T : struct
        {
            if (!array.IsCreated)
                return dependency;

            NativeMemorySentinel.UnregisterNativeArray(array);
            JobHandle disposeHandle = array.Dispose(dependency);
            array = default;
            return disposeHandle;
        }

        private static JobHandle DisposeNativeList<T>(ref NativeList<T> list, string label, JobHandle dependency) where T : unmanaged
        {
            if (!list.IsCreated)
                return dependency;

            NativeMemorySentinel.UnregisterNativeList(NativeMemoryOwner, label);
            JobHandle disposeHandle = list.Dispose(dependency);
            list = default;
            return disposeHandle;
        }

        private static Mesh BuildQuadMesh()
        {
            Mesh mesh = new Mesh
            {
                name = "HUD_FakeRadarBlip_Quad"
            }; // COLD ALLOC: Mesh[1] — reusable instanced hostile radar blip quad — owner: FakeRadarBlipController

            // COLD ALLOC: Vector3[4] — one-time quad geometry upload — owner: FakeRadarBlipController
            Vector3[] vertices =
            {
                new Vector3(-0.5f, -0.5f, 0f),
                new Vector3(-0.5f, 0.5f, 0f),
                new Vector3(0.5f, 0.5f, 0f),
                new Vector3(0.5f, -0.5f, 0f)
            };
            // COLD ALLOC: Vector2[4] — one-time quad uv upload — owner: FakeRadarBlipController
            Vector2[] uvs =
            {
                new Vector2(0f, 0f),
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(1f, 0f)
            };
            // COLD ALLOC: int[6] — one-time quad index upload — owner: FakeRadarBlipController
            int[] triangles = { 0, 1, 2, 0, 2, 3 };
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.UploadMeshData(true);
            return mesh;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private void PublishRadarSolveWarningIfNeeded(long startTimestamp)
        {
            long elapsedTicks = Stopwatch.GetTimestamp() - startTimestamp;
            double elapsedMilliseconds = elapsedTicks * 1000.0d / Stopwatch.Frequency;
            if (elapsedMilliseconds <= RadarSolveBudgetWarningMilliseconds || Time.frameCount < _nextRadarPerformanceWarningFrame)
                return;

            GlobalTelemetryBus.PublishPerformanceWarning(
                RadarSolveBudgetWarningHash,
                RadarSolveBudgetContextHash,
                (float)elapsedMilliseconds);
            _nextRadarPerformanceWarningFrame = Time.frameCount + RadarPerformanceWarningCooldownFrames;
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

            if (!_registered)
                _registered = GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.UI);

            if (!_registeredLateFrame)
                _registeredLateFrame = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.UI);

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
            if (_registered)
            {
                GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.UI);
                _registered = false;
            }

            if (_registeredLateFrame)
            {
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.UI);
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
