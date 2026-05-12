using Hecton8.Core;
using Hecton8.World;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Hecton8.UI
{
    /// <summary>
    /// Diegetic submarine sonar map built from voxel navigation samples and terrain height, not physics raycasts.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/UI/Submarine Sonar Holo Map Renderer")]
    public sealed class SubmarineSonarHoloMapRenderer : MonoBehaviour, ITickable, ILateFrameTickable
    {
        private const int MaxGridCells = 18;
        private const int MaxGridVerticesPerAxis = MaxGridCells + 1;
        private const int MaxVertexCount = MaxGridVerticesPerAxis * MaxGridVerticesPerAxis;
        private const int MaxLineIndexCount = MaxGridCells * MaxGridVerticesPerAxis * 4;
        private const float LowTierUpdateIntervalSeconds = 0.1f;
        private const float MidTierUpdateIntervalSeconds = 0.06666667f;
        private const float HighTierUpdateIntervalSeconds = 0.03333334f;
        private const float VisibilityDotThreshold = 0.035f;
        private const float MinimumDirectionLengthSq = 0.0001f;
        private const string RuntimeMeshName = "Runtime_SubmarineSonarHoloMap";
        private const string RuntimeMaterialName = "Runtime_SubmarineSonarHoloMap";
        private const string SonarMapShaderName = "Hecton8/Submarine/SonarHoloMapStencil";
#if UNITY_EDITOR
        private const string SonarMapShaderPath = "Assets/_Project/Art/Shaders/Hecton_SubmarineSonarHoloMapStencil.shader";
#endif

        private static readonly int _BaseColorId = Shader.PropertyToID("_BaseColor");

        [Header("Anchors")]
        [SerializeField] private Transform mapAnchor = null;
        [SerializeField] private Transform sampleOrigin = null;

        [Header("Sampling")]
        [SerializeField, Min(12f)] private float sampleRadiusMeters = 120f;
        [SerializeField, Min(0.02f)] private float displayRadiusMeters = 0.42f;
        [SerializeField, Range(0.02f, 1.25f)] private float verticalExaggeration = 0.32f;
        [SerializeField, Min(1f)] private float maxHeightDeltaMeters = 80f;

        [Header("Rendering")]
        [SerializeField] private Shader sonarMapShader = null;
        [SerializeField] private Color sonarColor = new Color(0.10f, 0.92f, 0.76f, 1f);
        [SerializeField] private int renderLayer = 0;

        // COLD ALLOC: Vector3[361] - sonar map previous vertex buffer for high-tier interpolation - owner: SubmarineSonarHoloMapRenderer
        private readonly Vector3[] _previousVertices = new Vector3[MaxVertexCount];
        // COLD ALLOC: Vector3[361] - sonar map current sampled vertex buffer - owner: SubmarineSonarHoloMapRenderer
        private readonly Vector3[] _currentVertices = new Vector3[MaxVertexCount];
        // COLD ALLOC: Vector3[361] - high-tier render interpolation vertex buffer - owner: SubmarineSonarHoloMapRenderer
        private readonly Vector3[] _renderVertices = new Vector3[MaxVertexCount];
        // COLD ALLOC: int[1368] - sonar map line indices, rewritten only when the active LOD changes - owner: SubmarineSonarHoloMapRenderer
        private readonly int[] _lineIndices = new int[MaxLineIndexCount];

        private Mesh _runtimeMesh;
        private Material _runtimeMaterial;
        private Camera _viewCamera;
        private bool _registeredTick;
        private bool _registeredLateFrame;
        private bool _hasCurrentSample;
        private bool _hasPreviousSample;
        private bool _visibleToPlayer;
        private bool _interpolationEnabled;
        private float _sampleAccumulator;
        private float _interpolationAgeSeconds;
        private float _activeUpdateIntervalSeconds = LowTierUpdateIntervalSeconds;
        private int _activeGridCells = -1;
        private int _activeIndexCount;

        private void OnEnable()
        {
            EnsureResources();
            TryRegisterTick();
        }

        private void Start()
        {
            EnsureResources();
            TryRegisterTick();
        }

        private void OnDisable()
        {
            _visibleToPlayer = false;
            TryUnregister();
        }

        private void OnDestroy()
        {
            TryUnregister();
            DestroyRuntimeResources();
        }

        public void Tick(float deltaTime)
        {
            EnsureResources();
            ResolveViewCamera();
            _visibleToPlayer = ResolveVisibleToPlayer();
            if (!_visibleToPlayer || _runtimeMesh == null || _runtimeMaterial == null)
            {
                RefreshLateFrameRegistration();
                return;
            }

            HectonQualityTier tier = GlobalRegistry.ScalabilityTier;
            int gridCells = ResolveGridCells(tier);
            float updateInterval = ResolveUpdateIntervalSeconds(tier);
            _interpolationEnabled = ResolveInterpolationEnabled(tier);
            if (_activeGridCells != gridCells)
                RebuildLineIndices(gridCells);

            float safeDeltaTime = math.max(0f, deltaTime);
            _sampleAccumulator += safeDeltaTime;
            _interpolationAgeSeconds += safeDeltaTime;
            if (!_hasCurrentSample || _sampleAccumulator >= updateInterval)
            {
                _sampleAccumulator = 0f;
                _activeUpdateIntervalSeconds = updateInterval;
                RefreshMapSample(gridCells);
            }

            RefreshLateFrameRegistration();
        }

        public void LateFrameTick()
        {
            if (!_visibleToPlayer || _runtimeMesh == null || _runtimeMaterial == null || !_hasCurrentSample)
                return;

            Camera renderCamera = ResolveRenderCamera();
            if (renderCamera == null)
                return;

            Transform anchor = ResolveMapAnchor();
            Matrix4x4 matrix = Matrix4x4.TRS(anchor.position, anchor.rotation, Vector3.one);
            if (_interpolationEnabled && _hasPreviousSample)
                UploadInterpolatedVertices();

            Graphics.DrawMesh(
                _runtimeMesh,
                matrix,
                _runtimeMaterial,
                renderLayer,
                renderCamera,
                0,
                null,
                ShadowCastingMode.Off,
                false,
                null,
                LightProbeUsage.Off,
                null);
        }

        private void RefreshMapSample(int gridCells)
        {
            if (_hasCurrentSample)
            {
                System.Array.Copy(_currentVertices, _previousVertices, _currentVertices.Length);
                _hasPreviousSample = true;
            }

            Transform anchor = ResolveMapAnchor();
            Transform origin = sampleOrigin != null ? sampleOrigin : anchor;
            Vector3 originPosition = origin.position;
            Quaternion originRotation = origin.rotation;
            float safeRadius = math.max(12f, sampleRadiusMeters);
            float invGridCells = math.rcp(math.max(1, gridCells));
            float worldStep = (safeRadius + safeRadius) * invGridCells;
            float displayScale = math.max(0.0001f, displayRadiusMeters) * math.rcp(safeRadius);
            float safeMaxHeightDelta = math.max(1f, maxHeightDeltaMeters);
            float safeVerticalScale = displayScale * math.max(0.02f, verticalExaggeration);
            int vertexCount = (gridCells + 1) * (gridCells + 1);

            for (int z = 0; z <= gridCells; z++)
            {
                float zWorld = (z * worldStep) - safeRadius;
                for (int x = 0; x <= gridCells; x++)
                {
                    float xWorld = (x * worldStep) - safeRadius;
                    Vector3 sampleOffset = originRotation * new Vector3(xWorld, 0f, zWorld);
                    Vector3 samplePosition = originPosition + sampleOffset;
                    float heightDelta = ResolveHybridFloorDelta(samplePosition, originPosition.y);
                    heightDelta = math.clamp(heightDelta, -safeMaxHeightDelta, safeMaxHeightDelta);
                    int vertexIndex = ToVertexIndex(x, z, gridCells);
                    _currentVertices[vertexIndex] = new Vector3(
                        xWorld * displayScale,
                        heightDelta * safeVerticalScale,
                        zWorld * displayScale);
                }
            }

            for (int i = vertexCount; i < _currentVertices.Length; i++)
                _currentVertices[i] = Vector3.zero;

            UploadVertices(_currentVertices);
            _interpolationAgeSeconds = 0f;
            _hasCurrentSample = true;
        }

        private static float ResolveHybridFloorDelta(Vector3 samplePosition, float originY)
        {
            float3 position = new float3(samplePosition.x, samplePosition.y, samplePosition.z);
            if (VoxelDynamicNavGridRuntime.TrySampleHybridNavigation(position, out VoxelDynamicNavGridRuntime.HybridNavigationSample sample))
            {
                float floorY = sample.HasTerrainHeight != 0 || sample.Mode != VoxelDynamicNavGridRuntime.HybridNavigationMode.OpenWaterHeightmap
                    ? sample.FloorBoundaryY
                    : samplePosition.y;
                return floorY - originY;
            }

            return samplePosition.y - originY;
        }

        private void UploadInterpolatedVertices()
        {
            float t = math.saturate(_interpolationAgeSeconds * math.rcp(math.max(0.0001f, _activeUpdateIntervalSeconds)));
            int vertexCount = (_activeGridCells + 1) * (_activeGridCells + 1);
            for (int i = 0; i < vertexCount; i++)
                _renderVertices[i] = Vector3.LerpUnclamped(_previousVertices[i], _currentVertices[i], t);

            for (int i = vertexCount; i < _renderVertices.Length; i++)
                _renderVertices[i] = Vector3.zero;

            UploadVertices(_renderVertices);
        }

        private void UploadVertices(Vector3[] vertices)
        {
            if (_runtimeMesh == null)
                return;

            _runtimeMesh.SetVertices(
                vertices,
                0,
                vertices.Length,
                MeshUpdateFlags.DontRecalculateBounds |
                MeshUpdateFlags.DontValidateIndices |
                MeshUpdateFlags.DontNotifyMeshUsers);
        }

        private void RebuildLineIndices(int gridCells)
        {
            int cursor = 0;
            for (int z = 0; z <= gridCells; z++)
            {
                for (int x = 0; x < gridCells; x++)
                {
                    _lineIndices[cursor++] = ToVertexIndex(x, z, gridCells);
                    _lineIndices[cursor++] = ToVertexIndex(x + 1, z, gridCells);
                }
            }

            for (int x = 0; x <= gridCells; x++)
            {
                for (int z = 0; z < gridCells; z++)
                {
                    _lineIndices[cursor++] = ToVertexIndex(x, z, gridCells);
                    _lineIndices[cursor++] = ToVertexIndex(x, z + 1, gridCells);
                }
            }

            for (int i = cursor; i < _lineIndices.Length; i++)
                _lineIndices[i] = 0;

            _activeGridCells = gridCells;
            _activeIndexCount = cursor;
            if (_runtimeMesh != null)
                _runtimeMesh.SetIndices(_lineIndices, 0, _activeIndexCount, MeshTopology.Lines, 0, false);
        }

        private static int ToVertexIndex(int x, int z, int gridCells)
        {
            return z * (gridCells + 1) + x;
        }

        private static int ResolveGridCells(HectonQualityTier tier)
        {
            switch (tier)
            {
                case HectonQualityTier.High:
                    return 16;
                case HectonQualityTier.Ultra:
                    return MaxGridCells;
                case HectonQualityTier.Mid:
                    return 12;
                default:
                    return 8;
            }
        }

        private static float ResolveUpdateIntervalSeconds(HectonQualityTier tier)
        {
            switch (tier)
            {
                case HectonQualityTier.High:
                case HectonQualityTier.Ultra:
                    return HighTierUpdateIntervalSeconds;
                case HectonQualityTier.Mid:
                    return MidTierUpdateIntervalSeconds;
                default:
                    return LowTierUpdateIntervalSeconds;
            }
        }

        private static bool ResolveInterpolationEnabled(HectonQualityTier tier)
        {
            return tier == HectonQualityTier.High || tier == HectonQualityTier.Ultra;
        }

        private bool ResolveVisibleToPlayer()
        {
            if (_viewCamera == null)
                return false;

            Transform anchor = ResolveMapAnchor();
            Vector3 cameraPosition = _viewCamera.transform.position;
            Vector3 toMonitor = anchor.position - cameraPosition;
            float directionLengthSq = toMonitor.sqrMagnitude;
            if (directionLengthSq <= MinimumDirectionLengthSq)
                return true;

            float invLength = math.rsqrt(directionLengthSq);
            float3 toMonitorDirection = (float3)toMonitor * invLength;
            float3 cameraForward = (float3)_viewCamera.transform.forward;
            if (!math.all(math.isfinite(toMonitorDirection)) || !math.all(math.isfinite(cameraForward)))
                return false;

            return math.dot(cameraForward, toMonitorDirection) >= VisibilityDotThreshold;
        }

        private Transform ResolveMapAnchor()
        {
            return mapAnchor != null ? mapAnchor : transform;
        }

        private Camera ResolveRenderCamera()
        {
            Camera renderCamera = GlobalRenderContext.CurrentCamera;
            if (IsGameplayRenderCamera(renderCamera))
                return renderCamera;

            ResolveViewCamera();
            return IsGameplayRenderCamera(_viewCamera) ? _viewCamera : null;
        }

        private void ResolveViewCamera()
        {
            if (_viewCamera != null)
                return;

            IPlayerRuntimeContext playerContext = GlobalRegistry.Player;
            _viewCamera = playerContext != null ? playerContext.PlayerCamera : null;
        }

        private static bool IsGameplayRenderCamera(Camera camera)
        {
            return camera != null &&
                   camera.isActiveAndEnabled &&
                   camera.cameraType != CameraType.Preview &&
                   camera.cameraType != CameraType.Reflection;
        }

        private void EnsureResources()
        {
            if (_runtimeMesh == null)
            {
                _runtimeMesh = new Mesh
                {
                    name = RuntimeMeshName,
                    hideFlags = HideFlags.DontSave,
                    indexFormat = IndexFormat.UInt16
                };

                _runtimeMesh.MarkDynamic();
                _runtimeMesh.SetVertices(
                    _currentVertices,
                    0,
                    _currentVertices.Length,
                    MeshUpdateFlags.DontRecalculateBounds |
                    MeshUpdateFlags.DontValidateIndices |
                    MeshUpdateFlags.DontNotifyMeshUsers);
                _runtimeMesh.bounds = new Bounds(Vector3.zero, new Vector3(displayRadiusMeters * 2f, displayRadiusMeters, displayRadiusMeters * 2f));
                RebuildLineIndices(ResolveGridCells(GlobalRegistry.ScalabilityTier));
            }

#if UNITY_EDITOR
            if (sonarMapShader == null)
                sonarMapShader = AssetDatabase.LoadAssetAtPath<Shader>(SonarMapShaderPath);
#endif
            if (sonarMapShader == null)
                sonarMapShader = Shader.Find(SonarMapShaderName);

            if (_runtimeMaterial == null && sonarMapShader != null)
            {
                _runtimeMaterial = new Material(sonarMapShader)
                {
                    name = RuntimeMaterialName,
                    hideFlags = HideFlags.DontSave
                };
            }

            if (_runtimeMaterial != null && _runtimeMaterial.HasProperty(_BaseColorId))
                _runtimeMaterial.SetColor(_BaseColorId, sonarColor);
        }

        private void TryRegisterTick()
        {
            if (_registeredTick || !Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            _registeredTick = GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.UI);
            RefreshLateFrameRegistration();
        }

        private void RefreshLateFrameRegistration()
        {
            bool shouldRegister = isActiveAndEnabled &&
                                  Application.isPlaying &&
                                  GlobalRegistry.Dispatcher != null &&
                                  _visibleToPlayer &&
                                  _runtimeMesh != null &&
                                  _runtimeMaterial != null &&
                                  _hasCurrentSample;
            if (shouldRegister)
            {
                if (_registeredLateFrame)
                    return;

                _registeredLateFrame = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.UI);
                return;
            }

            if (_registeredLateFrame)
            {
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.UI);
                _registeredLateFrame = false;
            }
        }

        private void TryUnregister()
        {
            if (_registeredLateFrame)
            {
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.UI);
                _registeredLateFrame = false;
            }

            if (_registeredTick)
            {
                GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.UI);
                _registeredTick = false;
            }
        }

        private void DestroyRuntimeResources()
        {
            if (_runtimeMaterial != null)
            {
                DestroyUnityObject(_runtimeMaterial);
                _runtimeMaterial = null;
            }

            if (_runtimeMesh != null)
            {
                DestroyUnityObject(_runtimeMesh);
                _runtimeMesh = null;
            }
        }

        private static void DestroyUnityObject(Object target)
        {
            if (target == null)
                return;

            if (Application.isPlaying)
            {
                Destroy(target);
                return;
            }

            DestroyImmediate(target);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            sampleRadiusMeters = math.max(12f, sampleRadiusMeters);
            displayRadiusMeters = math.max(0.02f, displayRadiusMeters);
            verticalExaggeration = math.clamp(verticalExaggeration, 0.02f, 1.25f);
            maxHeightDeltaMeters = math.max(1f, maxHeightDeltaMeters);
        }
#endif
    }
}
