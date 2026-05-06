using Hecton8.AI;
using Hecton8.Bootstrap;
using Hecton8.Core;
using Hecton8.Gameplay;
using Hecton8.World;
using UnityEngine;
using UnityEngine.Rendering;
using Stopwatch = System.Diagnostics.Stopwatch;
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
    public sealed class FakeRadarBlipController : MonoBehaviour, IUpdatable
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
        private const uint ThermalNoiseHashSalt = 0x54484E31u;
        private const double RadarSolveBudgetWarningMilliseconds = 0.2d;
        private const int RadarPerformanceWarningCooldownFrames = 30;
        private const string RadarBlipShaderPath = "Assets/_Project/Art/Shaders/Hecton_ScannerMarkerInstanced.shader";
        private const string RadarBlipShaderName = "HECTON/Scanner/MarkerInstanced";

        private static readonly uint _RadarSolveBudgetWarningHash = unchecked((uint)Hecton.Localization.LocHash.Compute("HUD_RADAR_BLIP_SOLVE_OVER_BUDGET"));
        private static readonly uint _RadarSolveBudgetContextHash = unchecked((uint)Hecton.Localization.LocHash.Compute("FakeRadarBlipController.Tick"));

        private static readonly int _BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int _FlickerFrequencyId = Shader.PropertyToID("_FlickerFrequency");
        private static readonly int _FlickerIntensityId = Shader.PropertyToID("_FlickerIntensity");
        private static readonly int _FillAlphaId = Shader.PropertyToID("_FillAlpha");
        private static readonly int _OccludedColorId = Shader.PropertyToID("_OccludedColor");
        private static readonly int _OccludedBoostId = Shader.PropertyToID("_OccludedBoost");

        // COLD ALLOC: Camera[8] - non-alloc fallback camera resolve buffer - owner: FakeRadarBlipController
        private static readonly Camera[] s_cameraResolveBuffer = new Camera[8];

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
        private Transform _playerTransform;
        private HectonSurvivalSystem _survivalSystem;
        private Camera _projectionCamera;
        private Mesh _radarBlipMesh;
        private Material _radarBlipMaterial;
        private int _nextRadarPerformanceWarningFrame;

        private void OnEnable()
        {
            ResolvePlayerTransform();
            EnsureRuntimeResources();
            TryRegister();
        }

        private void Start()
        {
            ResolvePlayerTransform();
            ResolveProjectionCamera();
            EnsureRuntimeResources();
            TryRegister();
        }

        private void OnDisable()
        {
            TryUnregister();
        }

        private void OnDestroy()
        {
            TryUnregister();
            DisposeRuntimeResources();
        }

        public void Tick(float deltaTime)
        {
            long tickStart = Stopwatch.GetTimestamp();

            try
            {
                ResolvePlayerTransform();
                ResolveProjectionCamera();

                if (_playerTransform == null || _projectionCamera == null || _radarBlipMesh == null || _radarBlipMaterial == null)
                    return;

                int visibleCount = BuildBlipMatrices(_projectionCamera);
                if (visibleCount <= 0)
                    return;

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
                    _projectionCamera,
                    LightProbeUsage.Off,
                    null);
            }
            finally
            {
                PublishRadarSolveWarningIfNeeded(tickStart);
            }
        }

        private int BuildBlipMatrices(Camera projectionCamera)
        {
            Vector3 playerPosition = _playerTransform.position;
            AbsoluteUniversePosition playerAup = AbsoluteUniversePosition.FromRuntimePosition(playerPosition);
            int hitCount = FaunaSpatialHashRegistry.CollectContactsNonAlloc(
                in playerAup,
                radarRangeMeters,
                SpatialTargetKind.Bioform,
                _queryHits);

            float range = Mathf.Max(1f, radarRangeMeters);
            float rangeSqr = range * range;
            float radius = Mathf.Max(1f, radarRadiusPixels);
            float projectionDistance = Mathf.Max(
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
                halfWidth - Mathf.Max(0f, radarCenterInsetPixels.x) * worldPerPixel,
                -halfHeight + Mathf.Max(0f, radarCenterInsetPixels.y) * worldPerPixel);
            float blipWorldSize = Mathf.Max(1f, blipSizePixels) * worldPerPixel;
            Quaternion planeRotation = cameraTransform.rotation;
            Vector3 planeScale = new Vector3(blipWorldSize, blipWorldSize, 1f);
            Vector3 radarForwardFlat = Vector3.ProjectOnPlane(cameraForward, Vector3.up);
            if (radarForwardFlat.sqrMagnitude <= 0.0001f)
                radarForwardFlat = Vector3.ProjectOnPlane(_playerTransform.forward, Vector3.up);
            if (radarForwardFlat.sqrMagnitude <= 0.0001f)
                radarForwardFlat = Vector3.forward;
            radarForwardFlat.Normalize();
            Vector3 radarRightFlat = Vector3.Cross(Vector3.up, radarForwardFlat);

            int visibleCount = 0;
            for (int i = 0; i < hitCount && visibleCount < MaxBlips; i++)
            {
                SpatialQueryHit hit = _queryHits[i];
                if (!(hit.Owner is FaunaBrain brain) || !brain.isAggressive)
                    continue;

                AbsoluteUniversePosition hitAup = AbsoluteUniversePosition.FromRuntimePosition(hit.Position);
                Unity.Mathematics.float3 enemyDeltaAup = AbsoluteUniversePosition.ToCameraRelativeFloat3(in hitAup, in playerAup);
                Vector3 enemyDelta = new Vector3(enemyDeltaAup.x, enemyDeltaAup.y, enemyDeltaAup.z);
                Vector2 flatDelta = new Vector2(
                    Vector3.Dot(enemyDelta, radarRightFlat),
                    Vector3.Dot(enemyDelta, radarForwardFlat));
                if (!TryResolveRadarPosition(flatDelta, rangeSqr, range, radius, out Vector2 anchoredPosition))
                    continue;

                Vector2 planeOffset = radarCenter + anchoredPosition * worldPerPixel;
                Vector3 worldPosition = planeCenter + cameraRight * planeOffset.x + cameraUp * planeOffset.y;
                _blipMatrices[visibleCount] = Matrix4x4.TRS(worldPosition, planeRotation, planeScale);
                visibleCount++;
            }

            AppendThermalNoiseGhostBlips(
                ref visibleCount,
                radius,
                worldPerPixel,
                radarCenter,
                planeCenter,
                cameraRight,
                cameraUp,
                planeRotation,
                planeScale);

            return visibleCount;
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

            float depthMeters = Mathf.Max(0f, _survivalSystem.Depth);
            float thermalNoise01 = Mathf.Clamp01(
                (depthMeters - ThermalNoiseStartDepthMeters) /
                Mathf.Max(1f, ThermalNoiseFullDepthMeters - ThermalNoiseStartDepthMeters));
            if (thermalNoise01 <= 0f)
                return;

            int ghostCount = Mathf.Clamp(
                1 + Mathf.FloorToInt(thermalNoise01 * ThermalNoiseMaxGhostBlips),
                1,
                ThermalNoiseMaxGhostBlips);
            int cycleIndex = Mathf.FloorToInt(Time.unscaledTime / ThermalNoiseCycleSeconds);
            uint depthBucket = unchecked((uint)Mathf.FloorToInt(depthMeters));
            for (int i = 0; i < ghostCount && visibleCount < MaxBlips; i++)
            {
                uint hash = HashThermalNoiseGhost(unchecked((uint)cycleIndex), depthBucket, unchecked((uint)i));
                float acceptance = Mathf.Lerp(0.35f, 0.92f, thermalNoise01);
                if (((hash >> 24) & 0xFFu) / 255f > acceptance)
                    continue;

                float radial01 = 0.16f + (((hash >> 16) & 0xFFu) / 255f) * 0.82f;
                Vector2 anchoredPosition = ResolveThermalGhostDirection(hash) * (radius * radial01);
                Vector2 planeOffset = radarCenter + anchoredPosition * worldPerPixel;
                Vector3 worldPosition = planeCenter + cameraRight * planeOffset.x + cameraUp * planeOffset.y;
                _blipMatrices[visibleCount] = Matrix4x4.TRS(worldPosition, planeRotation, planeScale);
                visibleCount++;
            }
        }

        private static bool TryResolveRadarPosition(
            Vector2 flatDelta,
            float rangeSqr,
            float range,
            float radius,
            out Vector2 anchoredPosition)
        {
            anchoredPosition = default;
            float distanceSqr = flatDelta.sqrMagnitude;
            if (distanceSqr <= 0.0001f || distanceSqr > rangeSqr)
                return false;

            Vector2 normalized = flatDelta / range;
            if (normalized.sqrMagnitude > 1f)
                normalized.Normalize();

            anchoredPosition = normalized * radius;
            return true;
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
            if (radarBlipShader == null)
                radarBlipShader = Shader.Find(RadarBlipShaderName);

            if (_radarBlipMesh == null)
                _radarBlipMesh = BuildQuadMesh();

            if (_radarBlipMaterial == null && radarBlipShader != null)
            {
                _radarBlipMaterial = new Material(radarBlipShader)
                {
                    name = "HUD_FakeRadarBlips_Instanced_Runtime",
                    enableInstancing = true
                }; // COLD ALLOC: Material[1] - instanced hostile radar blip material - owner: FakeRadarBlipController
            }
        }

        private void DisposeRuntimeResources()
        {
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

        private void PublishRadarSolveWarningIfNeeded(long startTimestamp)
        {
            long elapsedTicks = Stopwatch.GetTimestamp() - startTimestamp;
            double elapsedMilliseconds = elapsedTicks * 1000.0d / Stopwatch.Frequency;
            if (elapsedMilliseconds <= RadarSolveBudgetWarningMilliseconds || Time.frameCount < _nextRadarPerformanceWarningFrame)
                return;

            GlobalTelemetryBus.PublishPerformanceWarning(
                _RadarSolveBudgetWarningHash,
                _RadarSolveBudgetContextHash,
                (float)elapsedMilliseconds);
            _nextRadarPerformanceWarningFrame = Time.frameCount + RadarPerformanceWarningCooldownFrames;
        }

        private static void ResolveCameraPlaneMetrics(
            Camera camera,
            float distance,
            out float worldPerPixel,
            out float halfWidth,
            out float halfHeight)
        {
            if (camera.orthographic)
            {
                halfHeight = Mathf.Max(0.001f, camera.orthographicSize);
                halfWidth = halfHeight * Mathf.Max(0.001f, camera.aspect);
            }
            else
            {
                halfHeight = Mathf.Tan(camera.fieldOfView * 0.5f * Mathf.Deg2Rad) * Mathf.Max(0.001f, distance);
                halfWidth = halfHeight * Mathf.Max(0.001f, camera.aspect);
            }

            worldPerPixel = (halfHeight * 2f) / Mathf.Max(1, camera.pixelHeight);
        }

        private void TryRegister()
        {
            if (_registered || !Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            GlobalRegistry.RegisterUpdatable(this, PriorityLayer.UI);
            _registered = GlobalRegistry.Updatables.Contains(this);
        }

        private void TryUnregister()
        {
            if (!_registered)
                return;

            GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.UI);
            _registered = false;
        }
    }
}
