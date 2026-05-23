using System.Collections.Generic;
using Hecton8.Bootstrap;
using Hecton8.Core;
using Hecton8.World;
using NASAPunk.Visor;
using System.Runtime.InteropServices;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Hecton8.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class HectonScanMarkerSystem : MonoBehaviour, ITickable, IUpdatable, IScanEventListener, IGlobalRegistryHotSwapListener
    {
        private const string MarkerShaderPath = "Assets/_Project/Art/Shaders/Hecton_ScannerMarkerInstanced.shader";
        private const int MaxMarkers = 64;
        private const float FadeDurationSeconds = 1f;
        private const float ProjectionPaddingMeters = 0.05f;
        private const float DegreesToHalfRadians = 0.00872664626f;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int FlickerFrequencyId = Shader.PropertyToID("_FlickerFrequency");
        private static readonly int FlickerIntensityId = Shader.PropertyToID("_FlickerIntensity");
        // COLD ALLOC: List<VisorHUDController>[2] — HUD camera resolve scratch — owner: HectonScanMarkerSystem
        private static readonly List<VisorHUDController> s_controllerResolveBuffer = new List<VisorHUDController>(2);
        // COLD ALLOC: Vector3[4] - shared scanner marker quad vertices - owner: HectonScanMarkerSystem
        private static readonly Vector3[] s_markerQuadVertices =
        {
            new Vector3(-0.5f, -0.5f, 0f),
            new Vector3(0.5f, -0.5f, 0f),
            new Vector3(0.5f, 0.5f, 0f),
            new Vector3(-0.5f, 0.5f, 0f)
        };

        // COLD ALLOC: Vector2[4] - shared scanner marker quad UVs - owner: HectonScanMarkerSystem
        private static readonly Vector2[] s_markerQuadUvs =
        {
            new Vector2(0f, 0f),
            new Vector2(1f, 0f),
            new Vector2(1f, 1f),
            new Vector2(0f, 1f)
        };

        // COLD ALLOC: int[6] - shared scanner marker quad indices - owner: HectonScanMarkerSystem
        private static readonly int[] s_markerQuadTriangles = { 0, 2, 1, 0, 3, 2 };

        [StructLayout(LayoutKind.Explicit, Size = 64)]
        private struct ActiveMarker
        {
            [FieldOffset(0)]
            public AbsoluteUniversePosition aup;
            [FieldOffset(48)]
            public float timer;
            [FieldOffset(52)]
            private uint _pad0;
            [FieldOffset(56)]
            private ulong _pad1;
        }

        [Header("── HUD Camera ───────────────────────────────")]
        [SerializeField] private Camera hudCamera;

        [Header("── Appearance ───────────────────────────────")]
        [SerializeField] private Shader markerShader;
        [SerializeField] private Color markerColor = new Color(0f, 0.9f, 1f, 0.9f);
        [SerializeField, Min(4f)] private float markerBaseSizePixels = 24f;
        [SerializeField, Min(2f)] private float markerMinSizePixels = 8f;
        [SerializeField, Min(4f)] private float markerMaxSizePixels = 40f;
        [SerializeField, Min(0.5f)] private float markerLifetime = 5f;
        [SerializeField, Min(0f)] private float edgeMarginPixels = 40f;
        [SerializeField, Min(0f)] private float flickerFrequency = 25f;
        [SerializeField, Range(0f, 0.4f)] private float flickerIntensity = 0.15f;

        private ActiveMarker[] _markers;
        private ulong _activeMarkerMask;
        private int _writeIndex;
        private Transform _playerTransform;
        private IPlayerRuntimeContext _cachedPlayerContext;
        private HectonPlayerMovement _cachedPlayerMovement;
        private Material _runtimeMarkerMaterial;
        private Mesh _runtimeMarkerMesh;
        // COLD ALLOC: Matrix4x4[64] — instanced marker draw mirror — owner: HectonScanMarkerSystem
        private readonly Matrix4x4[] _markerMatrixMirror = new Matrix4x4[MaxMarkers];
        private Color _appliedMarkerColor;
        private float _appliedFlickerFrequency;
        private float _appliedFlickerIntensity;
        private float _cachedProjectionDistance = -1f;
        private float _cachedFieldOfView = -1f;
        private float _cachedEdgeMarginPixels = -1f;
        private float _cachedWorldPerPixel;
        private float _cachedSafeHalfWidth = 0.5f;
        private float _cachedSafeHalfHeight = 0.5f;
        private int _cachedPixelWidth = -1;
        private int _cachedPixelHeight = -1;
        private bool _markerMaterialDirty = true;
        private bool _registered;
        private bool _registeredHotSwapListener;

        public void Initialize(Shader shaderOverride)
        {
            if (shaderOverride != null)
                markerShader = shaderOverride;
        }

        private void Awake()
        {
            // COLD ALLOC: ActiveMarker[64] — fixed scan marker slot buffer — owner: HectonScanMarkerSystem
            _markers = new ActiveMarker[MaxMarkers];
            CachePlayerContextCold();
            EnsureHudCamera();
            EnsurePlayerTransform();
            EnsureRuntimeResources();
        }

        private void OnEnable()
        {
            CachePlayerContextCold();
            TryRegisterHotSwapListener();
            ScanEvents.Register(this);
            RegisterTick();
        }

        private void OnDisable()
        {
            ScanEvents.Unregister(this);
            UnregisterTick();
            TryUnregisterHotSwapListener();
        }

        private void OnDestroy()
        {
            UnregisterTick();
            TryUnregisterHotSwapListener();

            if (_runtimeMarkerMaterial != null)
            {
                Destroy(_runtimeMarkerMaterial);
                _runtimeMarkerMaterial = null;
            }

            if (_runtimeMarkerMesh != null)
            {
                Destroy(_runtimeMarkerMesh);
                _runtimeMarkerMesh = null;
            }

            _cachedPlayerContext = null;
            _cachedPlayerMovement = null;
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot != GlobalRegistryServiceSlot.Player)
                return;

            _cachedPlayerContext = currentService as IPlayerRuntimeContext;
            _cachedPlayerMovement = _cachedPlayerContext != null ? _cachedPlayerContext.PlayerMovement : null;
        }

        public void Tick(float deltaTime)
        {
            if (_activeMarkerMask == 0UL)
                return;

            UpdateMarkerTimers(deltaTime);
            if (_activeMarkerMask == 0UL)
                return;

            EnsureHudCamera();
            EnsurePlayerTransform();
            EnsureRuntimeResources();
            RenderMarkers();
        }

        public void OnScanEvent(in ScanEventPayload payload)
        {
            if ((ScanEventType)payload.EventType != ScanEventType.NodeFound)
                return;

            HandleNodeFound(payload.Position);
        }

        private void HandleNodeFound(float3 worldPos)
        {
            if (!TryResolveAupFromRuntimeOrigin(new Vector3(worldPos.x, worldPos.y, worldPos.z), out AbsoluteUniversePosition markerAup))
                return;

            ulong activeMask = _activeMarkerMask;
            while (activeMask != 0UL)
            {
                int i = (int)math.tzcnt(activeMask);
                activeMask &= activeMask - 1UL;

                if (AbsoluteUniversePosition.DistanceSq(in _markers[i].aup, in markerAup) < 1d)
                {
                    _markers[i].timer = markerLifetime;
                    return;
                }
            }

            int writeSlot = _writeIndex;
            _markers[writeSlot] = new ActiveMarker
            {
                aup = markerAup,
                timer = markerLifetime
            };

            _activeMarkerMask |= 1UL << writeSlot;
            _writeIndex = (_writeIndex + 1) & (MaxMarkers - 1);
        }

        private void UpdateMarkerTimers(float deltaTime)
        {
            ulong activeMask = _activeMarkerMask;
            while (activeMask != 0UL)
            {
                int i = (int)math.tzcnt(activeMask);
                ulong bit = 1UL << i;
                activeMask &= activeMask - 1UL;

                _markers[i].timer -= deltaTime;
                if (_markers[i].timer <= 0f)
                {
                    _activeMarkerMask &= ~bit;
                }
            }
        }

        private void RenderMarkers()
        {
            if (hudCamera == null || _playerTransform == null || _runtimeMarkerMaterial == null || _runtimeMarkerMesh == null)
                return;

            int visibleCount = BuildMarkerMatrices();
            if (visibleCount <= 0)
                return;

            ApplyMarkerMaterialIfNeeded();

            UnityEngine.Graphics.DrawMeshInstanced(
                _runtimeMarkerMesh,
                0,
                _runtimeMarkerMaterial,
                _markerMatrixMirror,
                visibleCount,
                null,
                ShadowCastingMode.Off,
                false,
                0,
                hudCamera,
                LightProbeUsage.Off,
                null);
        }

        private int BuildMarkerMatrices()
        {
            ulong activeMask = _activeMarkerMask;
            if (activeMask == 0UL)
                return 0;

            Transform cameraTransform = hudCamera.transform;
            Vector3 playerPositionVector = _playerTransform.position;
            if (!TryResolvePlayerAup(playerPositionVector, out AbsoluteUniversePosition playerAup))
                return 0;

            UpdateProjectionCache();
            float projectionDistance = _cachedProjectionDistance;
            float worldPerPixel = _cachedWorldPerPixel;
            float safeHalfWidth = _cachedSafeHalfWidth;
            float safeHalfHeight = _cachedSafeHalfHeight;
            int visibleCount = 0;

            while (activeMask != 0UL)
            {
                int i = (int)math.tzcnt(activeMask);
                activeMask &= activeMask - 1UL;

                ActiveMarker marker = _markers[i];
                float3 markerRuntime = marker.aup.ToRuntimeFloat3();
                Vector3 markerRuntimePosition = new Vector3(markerRuntime.x, markerRuntime.y, markerRuntime.z);
                Vector3 viewport = hudCamera.WorldToViewportPoint(markerRuntimePosition);
                bool behindCamera = viewport.z <= 0.001f;
                float centeredX = viewport.x - 0.5f;
                float centeredY = viewport.y - 0.5f;
                if (behindCamera)
                {
                    centeredX = -centeredX;
                    centeredY = -centeredY;
                }

                if ((centeredX * centeredX) + (centeredY * centeredY) < 0.000001f)
                {
                    centeredX = 0f;
                    centeredY = 0.0001f;
                }

                bool clamped =
                    behindCamera ||
                    centeredX < -safeHalfWidth ||
                    centeredX > safeHalfWidth ||
                    centeredY < -safeHalfHeight ||
                    centeredY > safeHalfHeight;

                float finalViewportX = centeredX;
                float finalViewportY = centeredY;
                if (clamped)
                {
                    float tx = safeHalfWidth * math.rcp(math.max(math.abs(centeredX), 0.0001f));
                    float ty = safeHalfHeight * math.rcp(math.max(math.abs(centeredY), 0.0001f));
                    float clampScale = math.min(tx, ty);
                    finalViewportX *= clampScale;
                    finalViewportY *= clampScale;
                }

                float viewportX = finalViewportX + 0.5f;
                float viewportY = finalViewportY + 0.5f;
                Vector3 markerWorldPosition = hudCamera.ViewportToWorldPoint(new Vector3(viewportX, viewportY, projectionDistance));
                double distanceMeters = EstimateAupDistanceMeters(in marker.aup, in playerAup);
                double sizePixelsDouble = (double)markerBaseSizePixels * math.rcp(math.max(distanceMeters * 0.1d, 0.5d));
                float sizePixels = (float)math.clamp(sizePixelsDouble, markerMinSizePixels, markerMaxSizePixels);
                if (marker.timer < FadeDurationSeconds)
                    sizePixels *= math.saturate(marker.timer * math.rcp(FadeDurationSeconds));

                float markerScale = math.max(0.0001f, sizePixels * worldPerPixel);
                Matrix4x4 matrix = Matrix4x4.TRS(markerWorldPosition, cameraTransform.rotation, new Vector3(markerScale, markerScale, markerScale));
                _markerMatrixMirror[visibleCount] = matrix;
                visibleCount++;
            }

            return visibleCount;
        }

        private void UpdateProjectionCache()
        {
            int pixelHeight = math.max(1, hudCamera.pixelHeight);
            int pixelWidth = math.max(1, hudCamera.pixelWidth);
            float projectionDistance = hudCamera.nearClipPlane + ProjectionPaddingMeters;
            float fieldOfView = hudCamera.fieldOfView;
            float edgeMargin = edgeMarginPixels;

            if (_cachedPixelHeight == pixelHeight &&
                _cachedPixelWidth == pixelWidth &&
                _cachedProjectionDistance == projectionDistance &&
                _cachedFieldOfView == fieldOfView &&
                _cachedEdgeMarginPixels == edgeMargin)
            {
                return;
            }

            float frustumHeight = 2f * ApproximateTanPositive(fieldOfView * DegreesToHalfRadians) * projectionDistance;
            float invPixelWidth = math.rcp(pixelWidth);
            float invPixelHeight = math.rcp(pixelHeight);
            _cachedProjectionDistance = projectionDistance;
            _cachedFieldOfView = fieldOfView;
            _cachedEdgeMarginPixels = edgeMargin;
            _cachedPixelWidth = pixelWidth;
            _cachedPixelHeight = pixelHeight;
            _cachedWorldPerPixel = frustumHeight * invPixelHeight;
            _cachedSafeHalfWidth = math.max(0.001f, 0.5f - (edgeMargin * invPixelWidth));
            _cachedSafeHalfHeight = math.max(0.001f, 0.5f - (edgeMargin * invPixelHeight));
        }

        private void EnsureHudCamera()
        {
            if (hudCamera != null)
                return;

            VisorHUDController.CopyActiveControllersTo(s_controllerResolveBuffer);
            for (int i = 0; i < s_controllerResolveBuffer.Count; i++)
            {
                VisorHUDController controller = s_controllerResolveBuffer[i];
                if (controller != null && controller.HudCamera != null)
                {
                    hudCamera = controller.HudCamera;
                    break;
                }
            }

            s_controllerResolveBuffer.Clear();
        }

        private static float ApproximateTanPositive(float radians)
        {
            float x = math.clamp(radians, 0f, 1.4f);
            float x2 = x * x;
            float numerator = 15f - x2;
            float denominator = math.max(0.0001f, 15f - (6f * x2));
            return x * numerator * math.rcp(denominator);
        }

        private void EnsurePlayerTransform()
        {
            if (_playerTransform == null)
                GameBootstrapper.TryGetCurrentPlayerTransform(out _playerTransform);
        }

        private void EnsureRuntimeResources()
        {
            if (_runtimeMarkerMesh == null)
                _runtimeMarkerMesh = CreateMarkerQuadMesh();

            if (_runtimeMarkerMaterial != null)
                return;

#if UNITY_EDITOR
            if (markerShader == null)
                markerShader = AssetDatabase.LoadAssetAtPath<Shader>(MarkerShaderPath);
#endif

            if (markerShader == null)
                return;

            _runtimeMarkerMaterial = new Material(markerShader)
            {
                enableInstancing = true,
                hideFlags = HideFlags.DontSave
            };
            _markerMaterialDirty = true;
        }

        private void RegisterTick()
        {
            if (_registered || !Application.isPlaying)
                return;

            if (GlobalRegistry.Dispatcher == null)
                return;

            GlobalRegistry.RegisterUpdatable(this, PriorityLayer.UI);
            _registered = GlobalRegistry.Updatables.Contains(this);
        }

        private void UnregisterTick()
        {
            if (!_registered)
                return;

            GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.UI);
            _registered = false;
        }

        private void CachePlayerContextCold()
        {
            _cachedPlayerContext = GlobalRegistry.Player;
            _cachedPlayerMovement = _cachedPlayerContext != null ? _cachedPlayerContext.PlayerMovement : null;
        }

        private void TryRegisterHotSwapListener()
        {
            if (_registeredHotSwapListener || !Application.isPlaying)
                return;

            _registeredHotSwapListener = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterHotSwapListener()
        {
            if (!_registeredHotSwapListener)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _registeredHotSwapListener = false;
        }

        private static Mesh CreateMarkerQuadMesh()
        {
            Mesh mesh = new Mesh
            {
                name = "ScannerMarkerQuad"
            };

            mesh.SetVertices(s_markerQuadVertices);
            mesh.SetUVs(0, s_markerQuadUvs);
            mesh.SetTriangles(s_markerQuadTriangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            mesh.UploadMeshData(false);
            return mesh;
        }

        private void ApplyMarkerMaterialIfNeeded()
        {
            if (_runtimeMarkerMaterial == null)
                return;

            if (!_markerMaterialDirty &&
                SameColor(_appliedMarkerColor, markerColor) &&
                math.abs(_appliedFlickerFrequency - flickerFrequency) <= 0.0001f &&
                math.abs(_appliedFlickerIntensity - flickerIntensity) <= 0.0001f)
            {
                return;
            }

            _runtimeMarkerMaterial.SetColor(BaseColorId, markerColor);
            _runtimeMarkerMaterial.SetFloat(FlickerFrequencyId, flickerFrequency);
            _runtimeMarkerMaterial.SetFloat(FlickerIntensityId, flickerIntensity);
            _appliedMarkerColor = markerColor;
            _appliedFlickerFrequency = flickerFrequency;
            _appliedFlickerIntensity = flickerIntensity;
            _markerMaterialDirty = false;
        }

        private bool TryResolvePlayerAup(Vector3 fallbackRuntimePosition, out AbsoluteUniversePosition playerAup)
        {
            playerAup = default;
            IPlayerRuntimeContext playerContext = _cachedPlayerContext;
            if (_cachedPlayerMovement == null && playerContext != null)
            {
                _cachedPlayerMovement = playerContext.PlayerMovement;
            }

            HectonPlayerMovement movement = _cachedPlayerMovement;
            if (movement != null)
            {
                playerAup = movement.PredictedAup;
                if (playerAup.IsFinite())
                    return true;
            }

            return TryResolveAupFromRuntimeOrigin(fallbackRuntimePosition, out playerAup);
        }

        private static bool TryResolveAupFromRuntimeOrigin(Vector3 runtimePosition, out AbsoluteUniversePosition aup)
        {
            aup = default;
            if (!math.isfinite(runtimePosition.x) ||
                !math.isfinite(runtimePosition.y) ||
                !math.isfinite(runtimePosition.z))
            {
                return false;
            }

            AbsoluteUniversePosition originAup = GlobalSignals.CurrentRuntimeOriginAup();
            if (!originAup.IsFinite())
                return false;

            aup = AbsoluteUniversePosition.OffsetMeters(
                in originAup,
                new double3(runtimePosition.x, runtimePosition.y, runtimePosition.z));
            return aup.IsFinite();
        }

        private static bool SameColor(Color a, Color b)
        {
            return math.abs(a.r - b.r) <= 0.0001f &&
                   math.abs(a.g - b.g) <= 0.0001f &&
                   math.abs(a.b - b.b) <= 0.0001f &&
                   math.abs(a.a - b.a) <= 0.0001f;
        }

        private static double EstimateAupDistanceMeters(in AbsoluteUniversePosition a, in AbsoluteUniversePosition b)
        {
            double3 delta = AbsoluteUniversePosition.DeltaMetersClamped(in a, in b);
            double ax = math.abs(delta.x);
            double ay = math.abs(delta.y);
            double az = math.abs(delta.z);
            double max = math.max(ax, math.max(ay, az));
            double min = math.min(ax, math.min(ay, az));
            double mid = ax + ay + az - max - min;
            return max + (mid * 0.375d) + (min * 0.25d);
        }
    }
}
