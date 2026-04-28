using System.Collections.Generic;
using Hecton8.Bootstrap;
using Hecton8.Core;
using NASAPunk.Visor;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Hecton8.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class HectonScanMarkerSystem : MonoBehaviour, ITickable, IUpdatable
    {
        private const string MarkerShaderPath = "Assets/_Project/Art/Shaders/Hecton_ScannerMarkerInstanced.shader";
        private const int MaxMarkers = 64;
        private const float FadeDurationSeconds = 1f;
        private const float ProjectionPaddingMeters = 0.05f;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int FlickerFrequencyId = Shader.PropertyToID("_FlickerFrequency");
        private static readonly int FlickerIntensityId = Shader.PropertyToID("_FlickerIntensity");
        // COLD ALLOC: List<VisorHUDController>[2] — HUD camera resolve scratch — owner: HectonScanMarkerSystem
        private static readonly List<VisorHUDController> s_controllerResolveBuffer = new List<VisorHUDController>(2);

        private struct ActiveMarker
        {
            public float3 worldPos;
            public float timer;
            public bool active;
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
        private int _writeIndex;
        private Transform _playerTransform;
        private Material _runtimeMarkerMaterial;
        private Mesh _runtimeMarkerMesh;
        private NativeArray<Matrix4x4> _markerMatrices;
        // COLD ALLOC: Matrix4x4[64] — instanced marker draw mirror — owner: HectonScanMarkerSystem
        private readonly Matrix4x4[] _markerMatrixMirror = new Matrix4x4[MaxMarkers];
        private bool _registered;

        public void Initialize(Shader shaderOverride)
        {
            if (shaderOverride != null)
                markerShader = shaderOverride;
        }

        private void Awake()
        {
            // COLD ALLOC: ActiveMarker[64] — fixed scan marker slot buffer — owner: HectonScanMarkerSystem
            _markers = new ActiveMarker[MaxMarkers];
            EnsureHudCamera();
            EnsurePlayerTransform();
            EnsureRuntimeResources();
            EnsureMatrixBuffer();
        }

        private void OnEnable()
        {
            ScanEvents.OnNodeFound += HandleNodeFound;
            RegisterTick();
        }

        private void OnDisable()
        {
            ScanEvents.OnNodeFound -= HandleNodeFound;
            UnregisterTick();
        }

        private void OnDestroy()
        {
            UnregisterTick();

            if (_markerMatrices.IsCreated)
            {
                _markerMatrices.Dispose();
                _markerMatrices = default;
            }

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
        }

        public void Tick(float deltaTime)
        {
            EnsureHudCamera();
            EnsurePlayerTransform();
            EnsureRuntimeResources();
            EnsureMatrixBuffer();
            UpdateMarkerTimers(deltaTime);
            RenderMarkers();
        }

        private void HandleNodeFound(float3 worldPos)
        {
            for (int i = 0; i < MaxMarkers; i++)
            {
                if (_markers[i].active && math.distancesq(_markers[i].worldPos, worldPos) < 1f)
                {
                    _markers[i].timer = markerLifetime;
                    return;
                }
            }

            _markers[_writeIndex] = new ActiveMarker
            {
                worldPos = worldPos,
                timer = markerLifetime,
                active = true
            };

            _writeIndex = (_writeIndex + 1) % MaxMarkers;
        }

        private void UpdateMarkerTimers(float deltaTime)
        {
            for (int i = 0; i < MaxMarkers; i++)
            {
                if (!_markers[i].active)
                    continue;

                _markers[i].timer -= deltaTime;
                if (_markers[i].timer <= 0f)
                    _markers[i].active = false;
            }
        }

        private void RenderMarkers()
        {
            if (hudCamera == null || _playerTransform == null || _runtimeMarkerMaterial == null || _runtimeMarkerMesh == null || !_markerMatrices.IsCreated)
                return;

            int visibleCount = BuildMarkerMatrices();
            if (visibleCount <= 0)
                return;

            _runtimeMarkerMaterial.SetColor(BaseColorId, markerColor);
            _runtimeMarkerMaterial.SetFloat(FlickerFrequencyId, flickerFrequency);
            _runtimeMarkerMaterial.SetFloat(FlickerIntensityId, flickerIntensity);

            Graphics.DrawMeshInstanced(
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
            Transform cameraTransform = hudCamera.transform;
            Vector3 playerPosition = _playerTransform.position;
            float projectionDistance = hudCamera.nearClipPlane + ProjectionPaddingMeters;
            float frustumHeight = 2f * Mathf.Tan(hudCamera.fieldOfView * Mathf.Deg2Rad * 0.5f) * projectionDistance;
            float worldPerPixel = frustumHeight / Mathf.Max(1f, hudCamera.pixelHeight);
            float edgeMarginX = edgeMarginPixels / Mathf.Max(1f, hudCamera.pixelWidth);
            float edgeMarginY = edgeMarginPixels / Mathf.Max(1f, hudCamera.pixelHeight);
            float safeHalfWidth = Mathf.Max(0.001f, 0.5f - edgeMarginX);
            float safeHalfHeight = Mathf.Max(0.001f, 0.5f - edgeMarginY);
            int visibleCount = 0;

            for (int i = 0; i < MaxMarkers; i++)
            {
                if (!_markers[i].active)
                    continue;

                Vector3 viewport = hudCamera.WorldToViewportPoint((Vector3)_markers[i].worldPos);
                bool behindCamera = viewport.z <= 0.001f;
                Vector2 centeredViewport = new Vector2(viewport.x - 0.5f, viewport.y - 0.5f);
                if (behindCamera)
                    centeredViewport = -centeredViewport;

                if (centeredViewport.sqrMagnitude < 0.000001f)
                    centeredViewport = Vector2.up * 0.0001f;

                bool clamped =
                    behindCamera ||
                    centeredViewport.x < -safeHalfWidth ||
                    centeredViewport.x > safeHalfWidth ||
                    centeredViewport.y < -safeHalfHeight ||
                    centeredViewport.y > safeHalfHeight;

                Vector2 finalViewport = centeredViewport;
                if (clamped)
                {
                    float tx = safeHalfWidth / Mathf.Max(Mathf.Abs(centeredViewport.x), 0.0001f);
                    float ty = safeHalfHeight / Mathf.Max(Mathf.Abs(centeredViewport.y), 0.0001f);
                    finalViewport *= Mathf.Min(tx, ty);
                }

                float viewportX = finalViewport.x + 0.5f;
                float viewportY = finalViewport.y + 0.5f;
                Vector3 markerWorldPosition = hudCamera.ViewportToWorldPoint(new Vector3(viewportX, viewportY, projectionDistance));
                float distance = math.distance(_markers[i].worldPos, (float3)playerPosition);
                float sizePixels = markerBaseSizePixels / Mathf.Max(distance * 0.1f, 0.5f);
                sizePixels = Mathf.Clamp(sizePixels, markerMinSizePixels, markerMaxSizePixels);
                if (_markers[i].timer < FadeDurationSeconds)
                    sizePixels *= Mathf.Clamp01(_markers[i].timer / FadeDurationSeconds);

                float markerScale = Mathf.Max(0.0001f, sizePixels * worldPerPixel);
                Matrix4x4 matrix = Matrix4x4.TRS(markerWorldPosition, cameraTransform.rotation, new Vector3(markerScale, markerScale, markerScale));
                _markerMatrices[visibleCount] = matrix;
                _markerMatrixMirror[visibleCount] = matrix;
                visibleCount++;
            }

            return visibleCount;
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

        private void EnsurePlayerTransform()
        {
            if (_playerTransform == null)
                SceneBootstrap.TryGetCurrentPlayerTransform(out _playerTransform);
        }

        private void EnsureMatrixBuffer()
        {
            if (_markerMatrices.IsCreated)
                return;

            _markerMatrices = new NativeArray<Matrix4x4>(MaxMarkers, Allocator.Persistent);
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
        }

        private void RegisterTick()
        {
            if (_registered || !Application.isPlaying)
                return;

            GlobalRegistry.RegisterUpdatable(this, PriorityLayer.UI);
            _registered = true;
        }

        private void UnregisterTick()
        {
            if (!_registered)
                return;

            GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.UI);
            _registered = false;
        }

        private static Mesh CreateMarkerQuadMesh()
        {
            Mesh mesh = new Mesh
            {
                name = "ScannerMarkerQuad"
            };

            Vector3[] vertices =
            {
                new Vector3(-0.5f, -0.5f, 0f),
                new Vector3( 0.5f, -0.5f, 0f),
                new Vector3( 0.5f,  0.5f, 0f),
                new Vector3(-0.5f,  0.5f, 0f)
            };

            Vector2[] uv =
            {
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(1f, 1f),
                new Vector2(0f, 1f)
            };

            int[] triangles = { 0, 2, 1, 0, 3, 2 };
            mesh.vertices = vertices;
            mesh.uv = uv;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            mesh.UploadMeshData(false);
            return mesh;
        }
    }
}
