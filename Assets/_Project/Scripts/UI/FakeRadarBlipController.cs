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
    public sealed class FakeRadarBlipController : MonoBehaviour, IUpdatable, ILateFrameTickable, IRenderable, IScanEventListener
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
        private const float WreckSignalDistortionSeconds = 1.35f;
        private const float WreckSignalDistortionThicknessPixels = 3.0f;
        private const uint ThermalNoiseHashSalt = 0x54484E31u;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private const double RadarSolveBudgetWarningMilliseconds = 0.1d;
        private const int RadarPerformanceWarningCooldownFrames = 30;
#endif
        private const string RadarBlipShaderPath = "Assets/_Project/Art/Shaders/Hecton_ScannerMarkerInstanced.shader";
        private const string RadarBlipShaderName = "HECTON/Scanner/MarkerInstanced";
        private const string NativeMemoryOwner = nameof(FakeRadarBlipController);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private const uint RadarSolveBudgetWarningHash = 648937224u;
        private const uint RadarSolveBudgetContextHash = 2418241056u;
#endif

        private static readonly int _BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int _FlickerFrequencyId = Shader.PropertyToID("_FlickerFrequency");
        private static readonly int _FlickerIntensityId = Shader.PropertyToID("_FlickerIntensity");
        private static readonly int _FillAlphaId = Shader.PropertyToID("_FillAlpha");
        private static readonly int _OccludedColorId = Shader.PropertyToID("_OccludedColor");
        private static readonly int _OccludedBoostId = Shader.PropertyToID("_OccludedBoost");

        // COLD ALLOC: Camera[8] - non-alloc fallback camera resolve buffer - owner: FakeRadarBlipController
        private static readonly Camera[] s_cameraResolveBuffer = new Camera[8];

        [StructLayout(LayoutKind.Sequential)]
        private struct RadarCullCandidate
        {
            public float2 FlatDelta;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RadarCullResult
        {
            public float2 PlaneOffset;
            public int Visible;
        }

        [BurstCompile(FloatPrecision.Low, FloatMode.Fast)]
        private struct RadarBlip2DCullJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<RadarCullCandidate> Candidates;
            [WriteOnly] public NativeArray<RadarCullResult> Results;
            public float2 RadarCenter;
            public float2 BoundsMin;
            public float2 BoundsMax;
            public float RangeSqr;
            public float InvRange;
            public float RadarRadiusWorld;
            public float RadarRadiusWorldSqr;

            public void Execute(int index)
            {
                RadarCullCandidate candidate = Candidates[index];
                RadarCullResult result = default;
                float distanceSqr = math.lengthsq(candidate.FlatDelta);
                if (distanceSqr > 0.0001f && distanceSqr <= RangeSqr)
                {
                    float2 planeOffset = RadarCenter + candidate.FlatDelta * InvRange * RadarRadiusWorld;
                    float2 radarOffset = planeOffset - RadarCenter;
                    bool insideRadarScreen = math.lengthsq(radarOffset) <= RadarRadiusWorldSqr;
                    bool insideBounds = insideRadarScreen &&
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

        // COLD ALLOC: SpatialQueryHit[64] - fixed hostile radar query buffer - owner: FakeRadarBlipController
        private readonly SpatialQueryHit[] _queryHits = new SpatialQueryHit[MaxBlips];
        // COLD ALLOC: Matrix4x4[64] - instanced hostile radar blip matrices - owner: FakeRadarBlipController
        private readonly Matrix4x4[] _blipMatrices = new Matrix4x4[MaxBlips];

        private bool _registered;
        private bool _registeredLateFrame;
        private bool _registeredRenderable;
        private bool _radarCullScheduled;
        private int _scheduledCandidateCount;
        private Transform _playerTransform;
        private HectonSurvivalSystem _survivalSystem;
        private Camera _projectionCamera;
        private Mesh _radarBlipMesh;
        private Material _radarBlipMaterial;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private int _nextRadarPerformanceWarningFrame;
#endif
        private NativeArray<RadarCullCandidate> _radarCullCandidates;
        private NativeArray<RadarCullResult> _radarCullResults;
        private NativeList<Matrix4x4> _visibleBlipMatrices;
        private JobHandle _radarCullHandle;
        private float _wreckSignalDistortionTime;
        private float _wreckSignalDistortionPhase;
        private Quaternion _scheduledPlaneRotation = Quaternion.identity;
        private Vector3 _scheduledPlaneScale = Vector3.one;
        private Vector3 _scheduledPlaneCenter;
        private Vector3 _scheduledCameraRight;
        private Vector3 _scheduledCameraUp;
        private Vector2 _scheduledRadarCenter;
        private float _scheduledRadarRadius;
        private float _scheduledWorldPerPixel;
        private int _visibleBlipMatrixCount;

        private void OnEnable()
        {
            ResolvePlayerTransform();
            EnsureRuntimeResources();
            ScanEvents.Register(this);
            TryRegister();
        }

        private void Start()
        {
            ResolvePlayerTransform();
            ResolveProjectionCamera();
            EnsureRuntimeResources();
            ScanEvents.Register(this);
            TryRegister();
        }

        private void OnDisable()
        {
            CompleteOutstandingCullForShutdown();
            ScanEvents.Unregister(this);
            TryUnregister();
        }

        private void OnDestroy()
        {
            CompleteOutstandingCullForShutdown();
            ScanEvents.Unregister(this);
            TryUnregister();
            DisposeRuntimeResources();
        }

        public void Tick(float deltaTime)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            long tickStart = Stopwatch.GetTimestamp();
#endif

            try
            {
                ResolvePlayerTransform();
                ResolveProjectionCamera();

                if (_playerTransform == null ||
                    _projectionCamera == null ||
                    _radarBlipMesh == null ||
                    _radarBlipMaterial == null ||
                    !_radarCullCandidates.IsCreated ||
                    !_radarCullResults.IsCreated ||
                    !_visibleBlipMatrices.IsCreated)
                {
                    ClearVisibleBlipHandoff();
                    return;
                }

                if (_radarCullScheduled)
                    return;

                if (_wreckSignalDistortionTime > 0f)
                    _wreckSignalDistortionTime = math.max(0f, _wreckSignalDistortionTime - deltaTime);

                ScheduleBlipCull(_projectionCamera);
            }
            finally
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                PublishRadarSolveWarningIfNeeded(tickStart);
#endif
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
                    return;

                int visibleCount = CompleteScheduledBlipCull();
                if (visibleCount < 0)
                    return;

                _radarCullScheduled = false;
                _scheduledCandidateCount = 0;
                _radarCullHandle = default;

                _visibleBlipMatrixCount = visibleCount;
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

            for (int i = 0; i < visibleCount; i++)
                _blipMatrices[i] = _visibleBlipMatrices[i];

            DrawBlipMatrices(renderCamera, visibleCount);
        }

        private void ClearVisibleBlipHandoff()
        {
            _visibleBlipMatrixCount = 0;
            if (_visibleBlipMatrices.IsCreated)
                _visibleBlipMatrices.Clear();
        }

        private void AppendVisibleBlipMatrix(Matrix4x4 matrix, ref int visibleCount)
        {
            if (!_visibleBlipMatrices.IsCreated || visibleCount >= MaxBlips)
            {
                visibleCount = math.min(visibleCount, MaxBlips);
                return;
            }

            _visibleBlipMatrices.Add(matrix);
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
                _radarCullScheduled = false;
                _radarCullHandle = default;
                return;
            }

            int hitCount = FaunaSpatialHashRegistry.CollectContactsNonAlloc(
                in playerAup,
                radarRangeMeters,
                SpatialTargetKind.Bioform,
                _queryHits);

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
            float3 radarForwardFlatF3 = new float3(cameraForward.x, 0f, cameraForward.z);
            float forwardFlatLengthSqr = math.lengthsq(radarForwardFlatF3);
            if (forwardFlatLengthSqr <= 0.0001f)
            {
                Vector3 playerForward = _playerTransform.forward;
                radarForwardFlatF3 = new float3(playerForward.x, 0f, playerForward.z);
                forwardFlatLengthSqr = math.lengthsq(radarForwardFlatF3);
            }

            radarForwardFlatF3 = forwardFlatLengthSqr > 0.0001f
                ? radarForwardFlatF3 * math.rsqrt(forwardFlatLengthSqr)
                : new float3(0f, 0f, 1f);
            float3 radarRightFlatF3 = new float3(radarForwardFlatF3.z, 0f, -radarForwardFlatF3.x);

            int candidateCount = 0;
            for (int i = 0; i < hitCount && candidateCount < MaxBlips; i++)
            {
                SpatialQueryHit hit = _queryHits[i];
                if (!(hit.Owner is FaunaBrain brain) || !brain.isAggressive)
                    continue;

                AbsoluteUniversePosition hitAup = AbsoluteUniversePosition.FromRuntimePosition(hit.Position);
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
            _radarCullScheduled = true;
            if (candidateCount > 0)
            {
                _radarCullHandle = new RadarBlip2DCullJob
                {
                    Candidates = _radarCullCandidates,
                    Results = _radarCullResults,
                    RadarCenter = new float2(radarCenter.x, radarCenter.y),
                    BoundsMin = boundsMin,
                    BoundsMax = boundsMax,
                    RangeSqr = rangeSqr,
                    InvRange = invRange,
                    RadarRadiusWorld = radarRadiusWorld,
                    RadarRadiusWorldSqr = radarRadiusWorld * radarRadiusWorld
                }.Schedule(candidateCount, 32);
            }
            else
            {
                _radarCullHandle = default;
            }
        }

        private static bool TryResolvePlayerAup(out AbsoluteUniversePosition playerAup)
        {
            IPlayerRuntimeContext playerContext = GlobalRegistry.Player;
            HectonPlayerMovement playerMovement = playerContext != null ? playerContext.PlayerMovement : null;
            if (playerMovement != null)
            {
                playerAup = playerMovement.CurrentAup;
                return true;
            }

            if (PlayerRuntimeContextService.TryGetActiveRuntimeContext(out PlayerRuntimeContext runtimeContext))
            {
                PlayerMovementRuntimeState movementState = runtimeContext.MovementState;
                if ((movementState.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) != 0u)
                {
                    playerAup = movementState.PredictedAup;
                    return true;
                }
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
                if (!DispatcherJobSwap.TryComplete(ref _radarCullHandle, forceComplete: false))
                    return -1;
            }

            _visibleBlipMatrices.Clear();
            if (_scheduledCandidateCount > 0)
            {
                for (int i = 0; i < _scheduledCandidateCount && visibleCount < MaxBlips; i++)
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
            if (_wreckSignalDistortionTime <= 0f || visibleCount >= MaxBlips)
                return;

            float pulse01 = math.saturate(_wreckSignalDistortionTime / WreckSignalDistortionSeconds);
            float scanRaw = _wreckSignalDistortionPhase + (1f - pulse01) * 1.75f;
            float scan01 = scanRaw - math.floor(scanRaw);
            float radarRadiusWorld = math.max(1f, _scheduledRadarRadius) * _scheduledWorldPerPixel;
            float lineY = math.lerp(-radarRadiusWorld, radarRadiusWorld, scan01);
            Vector3 worldPosition =
                _scheduledPlaneCenter +
                _scheduledCameraRight * _scheduledRadarCenter.x +
                _scheduledCameraUp * (_scheduledRadarCenter.y + lineY);
            Vector3 lineScale = new Vector3(
                radarRadiusWorld * 2f,
                math.max(1f, WreckSignalDistortionThicknessPixels) * _scheduledWorldPerPixel * math.lerp(0.65f, 1.6f, pulse01),
                1f);
            AppendVisibleBlipMatrix(Matrix4x4.TRS(worldPosition, _scheduledPlaneRotation, lineScale), ref visibleCount);
        }

        private void DrawBlipMatrices(Camera renderCamera, int visibleCount)
        {
            _radarBlipMaterial.SetColor(_BaseColorId, blipColor);
            _radarBlipMaterial.SetColor(_OccludedColorId, blipColor);
            _radarBlipMaterial.SetFloat(_FlickerFrequencyId, 18f);
            _radarBlipMaterial.SetFloat(_FlickerIntensityId, 0.18f);
            _radarBlipMaterial.SetFloat(_FillAlphaId, 0.36f);
            _radarBlipMaterial.SetFloat(_OccludedBoostId, 1f);

            Graphics.DrawMeshInstanced(
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

        private void CompleteOutstandingCullForShutdown()
        {
            if (!_radarCullScheduled)
                return;

            if (_scheduledCandidateCount > 0)
                DispatcherJobSwap.TryComplete(ref _radarCullHandle, forceComplete: true);

            _radarCullScheduled = false;
            _scheduledCandidateCount = 0;
            _radarCullHandle = default;
            ClearVisibleBlipHandoff();
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
            if (_survivalSystem == null || visibleCount >= MaxBlips)
                return;

            float depthMeters = math.max(0f, _survivalSystem.Depth);
            float thermalNoise01 = math.saturate(
                (depthMeters - ThermalNoiseStartDepthMeters) /
                math.max(1f, ThermalNoiseFullDepthMeters - ThermalNoiseStartDepthMeters));
            if (thermalNoise01 <= 0f)
                return;

            int ghostCount = math.clamp(
                1 + (int)math.floor(thermalNoise01 * ThermalNoiseMaxGhostBlips),
                1,
                ThermalNoiseMaxGhostBlips);
            int cycleIndex = (int)math.floor(Time.unscaledTime / ThermalNoiseCycleSeconds);
            uint depthBucket = unchecked((uint)(int)math.floor(depthMeters));
            for (int i = 0; i < ghostCount && visibleCount < MaxBlips; i++)
            {
                uint hash = HashThermalNoiseGhost(unchecked((uint)cycleIndex), depthBucket, unchecked((uint)i));
                float acceptance = math.lerp(0.35f, 0.92f, thermalNoise01);
                if (((hash >> 24) & 0xFFu) / 255f > acceptance)
                    continue;

                float radial01 = 0.16f + (((hash >> 16) & 0xFFu) / 255f) * 0.82f;
                Vector2 anchoredPosition = ResolveThermalGhostDirection(hash) * (radius * radial01);
                Vector2 planeOffset = radarCenter + anchoredPosition * worldPerPixel;
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
            const float Diagonal = 0.70710678f;
            switch ((hash >> 8) & 0x0Fu)
            {
                case 0u: return new Vector2(0f, 1f);
                case 1u: return new Vector2(Diagonal, Diagonal);
                case 2u: return new Vector2(1f, 0f);
                case 3u: return new Vector2(Diagonal, -Diagonal);
                case 4u: return new Vector2(0f, -1f);
                case 5u: return new Vector2(-Diagonal, -Diagonal);
                case 6u: return new Vector2(-1f, 0f);
                case 7u: return new Vector2(-Diagonal, Diagonal);
                case 8u: return new Vector2(0.38268343f, 0.9238795f);
                case 9u: return new Vector2(0.9238795f, 0.38268343f);
                case 10u: return new Vector2(0.9238795f, -0.38268343f);
                case 11u: return new Vector2(0.38268343f, -0.9238795f);
                case 12u: return new Vector2(-0.38268343f, -0.9238795f);
                case 13u: return new Vector2(-0.9238795f, -0.38268343f);
                case 14u: return new Vector2(-0.9238795f, 0.38268343f);
                default: return new Vector2(-0.38268343f, 0.9238795f);
            }
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

        private void ResolvePlayerTransform()
        {
            if (_playerTransform != null)
            {
                if (_survivalSystem == null)
                    _playerTransform.TryGetComponent(out _survivalSystem);
                return;
            }

            IPlayerRuntimeContext playerContext = GlobalRegistry.Player;
            if (playerContext != null && playerContext.PlayerTransform != null)
            {
                _playerTransform = playerContext.PlayerTransform;
                _playerTransform.TryGetComponent(out _survivalSystem);
                return;
            }

            SceneBootstrap.TryGetCurrentPlayerTransform(out _playerTransform);
            if (_playerTransform != null)
                _playerTransform.TryGetComponent(out _survivalSystem);
        }

        private void ResolveProjectionCamera()
        {
            if (_projectionCamera != null && _projectionCamera.isActiveAndEnabled)
                return;

            SuitHUDV4CanvasOverlay overlay = SuitHUDV4CanvasOverlay.ActiveRuntimeInstance;
            if (overlay != null)
            {
                if (overlay.ProjectionCamera != null && overlay.ProjectionCamera.isActiveAndEnabled)
                {
                    _projectionCamera = overlay.ProjectionCamera;
                    return;
                }

                Canvas canvas = overlay.TargetCanvas;
                if (canvas != null && canvas.worldCamera != null && canvas.worldCamera.isActiveAndEnabled)
                {
                    _projectionCamera = canvas.worldCamera;
                    return;
                }
            }

            int cameraCount = Camera.GetAllCameras(s_cameraResolveBuffer);
            int hudMask = 1 << HudInternalLayerIndex;
            for (int i = 0; i < cameraCount && i < s_cameraResolveBuffer.Length; i++)
            {
                Camera candidate = s_cameraResolveBuffer[i];
                if (candidate != null && candidate.isActiveAndEnabled && (candidate.cullingMask & hudMask) != 0)
                {
                    _projectionCamera = candidate;
                    return;
                }
            }
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
                }; // COLD ALLOC: Material[1] - instanced hostile radar blip material - owner: FakeRadarBlipController
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                _radarBlipMaterial.name = "HUD_FakeRadarBlips_Instanced_Runtime";
#endif
            }

            if (!_radarCullCandidates.IsCreated)
            {
                _radarCullCandidates = new NativeArray<RadarCullCandidate>(MaxBlips, Allocator.Persistent, NativeArrayOptions.UninitializedMemory); // COLD ALLOC: NativeArray<RadarCullCandidate>[64] - Burst 2D HUD bounds cull input - owner: FakeRadarBlipController
                NativeMemorySentinel.RegisterNativeArray(_radarCullCandidates, NativeMemoryOwner, nameof(_radarCullCandidates), NativeAllocationLifetime.Scene);
            }

            if (!_radarCullResults.IsCreated)
            {
                _radarCullResults = new NativeArray<RadarCullResult>(MaxBlips, Allocator.Persistent, NativeArrayOptions.UninitializedMemory); // COLD ALLOC: NativeArray<RadarCullResult>[64] - Burst 2D HUD bounds cull output - owner: FakeRadarBlipController
                NativeMemorySentinel.RegisterNativeArray(_radarCullResults, NativeMemoryOwner, nameof(_radarCullResults), NativeAllocationLifetime.Scene);
            }

            if (!_visibleBlipMatrices.IsCreated)
            {
                _visibleBlipMatrices = new NativeList<Matrix4x4>(MaxBlips, Allocator.Persistent); // COLD ALLOC: NativeList<Matrix4x4>[64] - LateFrame radar render handoff to GlobalRenderContext - owner: FakeRadarBlipController
                NativeMemorySentinel.RegisterNativeList(_visibleBlipMatrices, NativeMemoryOwner, nameof(_visibleBlipMatrices), NativeAllocationLifetime.Scene);
            }
        }

        private void DisposeRuntimeResources()
        {
            CompleteOutstandingCullForShutdown();

            if (_radarBlipMaterial != null)
            {
                Destroy(_radarBlipMaterial);
                _radarBlipMaterial = null;
            }

            if (_radarBlipMesh != null)
            {
                Destroy(_radarBlipMesh);
                _radarBlipMesh = null;
            }

            DisposeNativeArray(ref _radarCullCandidates);
            DisposeNativeArray(ref _radarCullResults);
            DisposeNativeList(ref _visibleBlipMatrices, nameof(_visibleBlipMatrices));
        }

        private static void DisposeNativeArray<T>(ref NativeArray<T> array) where T : struct
        {
            if (!array.IsCreated)
                return;

            NativeMemorySentinel.UnregisterNativeArray(array);
            array.Dispose();
            array = default;
        }

        private static void DisposeNativeList<T>(ref NativeList<T> list, string label) where T : unmanaged
        {
            if (!list.IsCreated)
                return;

            NativeMemorySentinel.UnregisterNativeList(NativeMemoryOwner, label);
            list.Dispose();
            list = default;
        }

        private static Mesh BuildQuadMesh()
        {
            Mesh mesh = new Mesh
            {
                name = "HUD_FakeRadarBlip_Quad"
            }; // COLD ALLOC: Mesh[1] - reusable instanced hostile radar blip quad - owner: FakeRadarBlipController

            // COLD ALLOC: Vector3[4] - one-time quad geometry upload - owner: FakeRadarBlipController
            Vector3[] vertices =
            {
                new Vector3(-0.5f, -0.5f, 0f),
                new Vector3(-0.5f, 0.5f, 0f),
                new Vector3(0.5f, 0.5f, 0f),
                new Vector3(0.5f, -0.5f, 0f)
            };
            // COLD ALLOC: Vector2[4] - one-time quad uv upload - owner: FakeRadarBlipController
            Vector2[] uvs =
            {
                new Vector2(0f, 0f),
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(1f, 0f)
            };
            // COLD ALLOC: int[6] - one-time quad index upload - owner: FakeRadarBlipController
            int[] triangles = { 0, 1, 2, 0, 2, 3 };
            mesh.SetVertices(vertices);
            mesh.uv = uvs;
            mesh.SetTriangles(triangles, 0);
            mesh.UploadMeshData(false);
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
                halfHeight = math.tan(math.radians(camera.fieldOfView) * 0.5f) * math.max(0.001f, distance);
                halfWidth = halfHeight * math.max(0.001f, camera.aspect);
            }

            worldPerPixel = (halfHeight * 2f) / math.max(1, camera.pixelHeight);
        }

        private void TryRegister()
        {
            if (!Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            if (!_registered)
            {
                GlobalRegistry.RegisterUpdatable(this, PriorityLayer.UI);
                _registered = GlobalRegistry.Updatables.Contains(this);
            }

            if (!_registeredLateFrame)
            {
                GlobalRegistry.RegisterLateFrameTickable(this, PriorityLayer.UI);
                _registeredLateFrame = SystemDispatcher.GetLateFrameLane(PriorityLayer.UI).Contains(this);
            }

            if (!_registeredRenderable)
            {
                GlobalRegistry.Renderables.Register(this);
                _registeredRenderable = GlobalRegistry.Renderables.Contains(this);
            }
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
