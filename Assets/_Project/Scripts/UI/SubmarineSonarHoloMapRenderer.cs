using Hecton8.Core;
using Hecton8.World;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace Hecton8.UI
{
    /// <summary>
    /// Diegetic submarine sonar map built from voxel navigation samples and terrain height, not physics raycasts.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/UI/Submarine Sonar Holo Map Renderer")]
    public sealed class SubmarineSonarHoloMapRenderer : MonoBehaviour, ILateFrameTickable, IGlobalRegistryHotSwapListener
    {
        private const int MaxGridCells = 18;
        private const int MaxGridVerticesPerAxis = MaxGridCells + 1;
        private const int MaxVertexCount = MaxGridVerticesPerAxis * MaxGridVerticesPerAxis;
        private const int MaxLineIndexCount = MaxGridCells * MaxGridVerticesPerAxis * 4;
        private const float MinimumQualityUpdateIntervalSeconds = 0.1f;
        private const float MaximumQualityUpdateIntervalSeconds = 0.03333334f;
        private const float VisibilityDotThreshold = 0.035f;
        private const float MinimumDirectionLengthSq = 0.0001f;
        private const string RuntimeMeshName = "Runtime_SubmarineSonarHoloMap";

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
        [SerializeField] private Material sonarMapMaterial = null;
        [SerializeField] private Color sonarColor = new Color(0.10f, 0.92f, 0.76f, 1f);
        [SerializeField] private int renderLayer = 0;

        // COLD ALLOC: Vector3[361] - sonar map previous vertex buffer for quality interpolation - owner: SubmarineSonarHoloMapRenderer
        private readonly Vector3[] _previousVertices = new Vector3[MaxVertexCount];
        // COLD ALLOC: Vector3[361] - sonar map current sampled vertex buffer - owner: SubmarineSonarHoloMapRenderer
        private readonly Vector3[] _currentVertices = new Vector3[MaxVertexCount];
        // COLD ALLOC: Vector3[361] - render interpolation vertex buffer - owner: SubmarineSonarHoloMapRenderer
        private readonly Vector3[] _renderVertices = new Vector3[MaxVertexCount];
        // COLD ALLOC: int[1368] - sonar map line indices, rewritten only when the active LOD changes - owner: SubmarineSonarHoloMapRenderer
        private readonly int[] _lineIndices = new int[MaxLineIndexCount];

        private Mesh _runtimeMesh;
        private MaterialPropertyBlock _materialProperties;
        private Camera _viewCamera;
        private IPlayerRuntimeContext _cachedPlayerContext;
        private Color _appliedSonarColor;
        private bool _registeredLateFrame;
        private bool _hotSwapListenerRegistered;
        private bool _hasCurrentSample;
        private bool _hasPreviousSample;
        private bool _visibleToPlayer;
        private bool _materialPropertiesDirty = true;
        private float _sampleAccumulator;
        private float _interpolationAgeSeconds;
        private float _interpolationBlendWeight;
        private float _cachedQualityWeight01 = 1f;
        private float _activeUpdateIntervalSeconds = MinimumQualityUpdateIntervalSeconds;
        private int _activeGridCells = -1;
        private int _activeIndexCount;
        private bool _missingSonarMapMaterialAnnounced;

        private void OnEnable()
        {
            CacheRegistryServicesCold();
            TryRegisterHotSwapListener();
            EnsureResources();
            TryRegisterTick();
        }

        private void Start()
        {
            CacheRegistryServicesCold();
            TryRegisterHotSwapListener();
            EnsureResources();
            TryRegisterTick();
        }

        private void OnDisable()
        {
            _visibleToPlayer = false;
            TryUnregisterHotSwapListener();
            TryUnregister();
        }

        private void OnDestroy()
        {
            TryUnregisterHotSwapListener();
            TryUnregister();
            DestroyRuntimeResources();
        }

        private void RunVisualSync(float deltaTime)
        {
            RefreshQualityPolicy();
            ApplyMaterialPropertiesIfNeeded();
            ResolveViewCamera();
            _visibleToPlayer = ResolveVisibleToPlayer();
            if (!_visibleToPlayer || _runtimeMesh == null || sonarMapMaterial == null)
            {
                return;
            }

            float safeDeltaTime = math.max(0f, deltaTime);
            float qualityWeight = math.saturate(_cachedQualityWeight01);
            int gridCells = ResolveGridCells(qualityWeight);
            float updateInterval = ResolveUpdateIntervalSeconds(qualityWeight);
            _interpolationBlendWeight = ResolveInterpolationBlendWeight(qualityWeight);
            bool gridChanged = _activeGridCells != gridCells;
            if (gridChanged)
            {
                RebuildLineIndices(gridCells);
                _hasCurrentSample = false;
                _hasPreviousSample = false;
                _interpolationAgeSeconds = 0f;
            }

            _sampleAccumulator += safeDeltaTime;
            _interpolationAgeSeconds += safeDeltaTime;
            if (!_hasCurrentSample || _sampleAccumulator >= updateInterval)
            {
                _sampleAccumulator = 0f;
                _activeUpdateIntervalSeconds = updateInterval;
                RefreshMapSample(gridCells);
            }

        }

        public void LateFrameTick()
        {
            RunVisualSync(SystemDispatcher.CurrentFrameDeltaTime);
            if (!_visibleToPlayer || _runtimeMesh == null || sonarMapMaterial == null || !_hasCurrentSample)
                return;

            Camera renderCamera = ResolveRenderCamera();
            if (renderCamera == null)
                return;

            Transform anchor = ResolveMapAnchor();
            Matrix4x4 matrix = Matrix4x4.TRS(ResolveAnchorRuntimePosition(anchor), anchor.rotation, Vector3.one);
            if (_hasPreviousSample && _interpolationBlendWeight > 0.0001f)
                UploadInterpolatedVertices();

            UnityEngine.Graphics.DrawMesh(
                _runtimeMesh,
                matrix,
                sonarMapMaterial,
                renderLayer,
                renderCamera,
                0,
                _materialProperties,
                ShadowCastingMode.Off,
                false,
                null,
                LightProbeUsage.Off);
        }

        private static Vector3 ResolveAnchorRuntimePosition(Transform anchor)
        {
            return anchor != null ? anchor.position : Vector3.zero;
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
            RefreshRuntimeMeshBounds();
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
            float ageT = math.saturate(_interpolationAgeSeconds * math.rcp(math.max(0.0001f, _activeUpdateIntervalSeconds)));
            float t = math.lerp(1f, ageT, math.saturate(_interpolationBlendWeight));
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

        private static int ResolveGridCells(float qualityWeight01)
        {
            float quality = math.saturate(qualityWeight01);
            float curve = quality * quality * (3f - (2f * quality));
            return math.clamp((int)math.round(math.lerp(8f, MaxGridCells, curve)), 8, MaxGridCells);
        }

        private static float ResolveUpdateIntervalSeconds(float qualityWeight01)
        {
            float quality = math.saturate(qualityWeight01);
            float curve = quality * quality * (3f - (2f * quality));
            return math.max(0.01666667f, math.lerp(MinimumQualityUpdateIntervalSeconds, MaximumQualityUpdateIntervalSeconds, curve));
        }

        private static float ResolveInterpolationBlendWeight(float qualityWeight01)
        {
            float quality = math.saturate(qualityWeight01);
            return math.saturate((quality - 0.35f) * 1.5384616f);
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

            IPlayerRuntimeContext playerContext = _cachedPlayerContext;
            _viewCamera = playerContext != null ? playerContext.PlayerCamera : null;
        }

        private static bool IsGameplayRenderCamera(Camera camera)
        {
            return camera != null &&
                   camera.isActiveAndEnabled &&
                   camera.cameraType != CameraType.Preview &&
                   camera.cameraType != CameraType.Reflection;
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot != GlobalRegistryServiceSlot.Player)
                return;

            Camera previousCamera = _cachedPlayerContext != null ? _cachedPlayerContext.PlayerCamera : null;
            _cachedPlayerContext = currentService as IPlayerRuntimeContext;
            if (_viewCamera == null || ReferenceEquals(_viewCamera, previousCamera))
                _viewCamera = _cachedPlayerContext != null ? _cachedPlayerContext.PlayerCamera : null;
        }

        private void CacheRegistryServicesCold()
        {
            _cachedPlayerContext = GlobalRegistry.Player;
            RefreshQualityPolicy();
        }

        private void RefreshQualityPolicy()
        {
            float value = HomeostasisBrain.GlobalQualityWeight;
            _cachedQualityWeight01 = math.saturate(math.select(_cachedQualityWeight01, value, math.isfinite(value)));
        }

        /// <summary>
        /// Builds the runtime line mesh, or reports the missing authored material once without throwing.
        /// </summary>
        /// <remarks>
        /// The <c>UnityEngine.Assertions.Assert.IsNotNull(sonarMapMaterial, ...)</c> that used to sit near the end
        /// of this method (just before <see cref="ApplyMaterialPropertiesIfNeeded"/>) was a TAUTOLOGY, not a
        /// guard: the <c>sonarMapMaterial == null</c> branch below returns before it, and nothing between the two
        /// points can destroy the material. It could therefore never fire, while still carrying the project's
        /// throwing-assert hazard for any future edit that moved it above the early return - both callers reach
        /// this method before <see cref="TryRegisterTick"/> (<see cref="OnEnable"/> :75, <see cref="Start"/> :83),
        /// so a throw there would have left the holo map permanently off the tick lane.
        ///
        /// The real defect was the opposite one: the null branch was completely SILENT. It tore down the runtime
        /// resources and returned with no diagnosis anywhere, and the draw sites at :107 and :140 then skipped
        /// quietly forever. The one-shot report below makes that authoring gap visible without throwing.
        /// </remarks>
        private void EnsureResources()
        {
            if (sonarMapMaterial == null)
            {
                DestroyRuntimeResources();

                if (!_missingSonarMapMaterialAnnounced)
                {
                    _missingSonarMapMaterialAnnounced = true;
                    LogMissingSonarMapMaterial();
                }

                return;
            }

            EnsureMaterialPropertiesCold();
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
                RefreshRuntimeMeshBounds();
                RebuildLineIndices(ResolveGridCells(_cachedQualityWeight01));
            }

            // No null re-check needed: the sonarMapMaterial == null early return at the top of this method is the
            // only reachable path for a missing material, and ApplyMaterialPropertiesIfNeeded null-guards it again.
            ApplyMaterialPropertiesIfNeeded();
        }

        /// <summary>
        /// One-shot report of the unassigned authored sonar map material. The latch guarantees single emission and
        /// the method takes no arguments, so no string work or allocation reaches any tick cadence.
        /// </summary>
        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogMissingSonarMapMaterial()
        {
            Hecton8.Core.H8Debug.LogError("SubmarineSonarHoloMapRenderer: serialized field 'sonarMapMaterial' is unassigned, so the submarine sonar holo map draws nothing this session - the runtime line mesh is torn down and both draw sites skip on the null material. The component still ticks and still tracks player visibility. Runtime material generation is forbidden: assign the authored sonar holo map material in the inspector.");
        }

        private void EnsureMaterialPropertiesCold()
        {
            if (_materialProperties != null)
                return;

            // COLD ALLOC: MaterialPropertyBlock[1] - submarine sonar holo map draw payload - owner: SubmarineSonarHoloMapRenderer.
            _materialProperties = new MaterialPropertyBlock();
            _materialPropertiesDirty = true;
        }

        private void ApplyMaterialPropertiesIfNeeded()
        {
            if (sonarMapMaterial == null)
                return;

            EnsureMaterialPropertiesCold();

            if (!_materialPropertiesDirty && SameColor(_appliedSonarColor, sonarColor))
                return;

            _materialProperties.Clear();
            _materialProperties.SetColor(_BaseColorId, sonarColor);
            _appliedSonarColor = sonarColor;
            _materialPropertiesDirty = false;
        }

        private static bool SameColor(Color lhs, Color rhs)
        {
            return math.abs(lhs.r - rhs.r) <= 0.0001f &&
                   math.abs(lhs.g - rhs.g) <= 0.0001f &&
                   math.abs(lhs.b - rhs.b) <= 0.0001f &&
                   math.abs(lhs.a - rhs.a) <= 0.0001f;
        }

        private void RefreshRuntimeMeshBounds()
        {
            if (_runtimeMesh == null)
                return;

            float safeRadius = math.max(12f, sampleRadiusMeters);
            float safeDisplayRadius = math.max(0.0001f, displayRadiusMeters);
            float verticalExtent = math.max(1f, maxHeightDeltaMeters) *
                                   safeDisplayRadius *
                                   math.rcp(safeRadius) *
                                   math.max(0.02f, verticalExaggeration);
            float safeVerticalSize = math.max(safeDisplayRadius, verticalExtent + verticalExtent);
            _runtimeMesh.bounds = new Bounds(
                Vector3.zero,
                new Vector3(safeDisplayRadius + safeDisplayRadius, safeVerticalSize, safeDisplayRadius + safeDisplayRadius));
        }

        private void TryRegisterTick()
        {
            if (_registeredLateFrame || !Application.isPlaying)
                return;

            _registeredLateFrame = SystemDispatcher.Register((ILateFrameTickable)this, PriorityLayer.UI);
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

        private void TryUnregister()
        {
            if (_registeredLateFrame)
            {
                SystemDispatcher.UnregisterLateFrameTickableDirect(this, PriorityLayer.UI);
                _registeredLateFrame = false;
            }

        }

        private void DestroyRuntimeResources()
        {
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
            _materialPropertiesDirty = true;
        }
#endif
    }
}
