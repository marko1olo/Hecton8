using Hecton8.Core;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace Hecton8.World
{
    /// <summary>
    /// Generates and renders seabed scatter entirely on the GPU from the active MapMagic height payload.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GPUScatterDirector : MonoBehaviour, IUpdatable
    {
        private const int ThreadGroupSize = 64;
        private const int FrustumPlaneCount = 6;
        private static readonly int _ScatterInstancesId = Shader.PropertyToID("_HectonScatterInstances");
        private static readonly int _VisibleIndicesId = Shader.PropertyToID("_HectonVisibleScatterIndices");
        private static readonly int _HeightTextureId = Shader.PropertyToID("_HectonScatterHeightTexture");
        private static readonly int _HeightResolutionId = Shader.PropertyToID("_HectonScatterHeightResolution");
        private static readonly int _TerrainPositionId = Shader.PropertyToID("_HectonScatterTerrainPosition");
        private static readonly int _TerrainSizeId = Shader.PropertyToID("_HectonScatterTerrainSize");
        private static readonly int _FieldRectId = Shader.PropertyToID("_HectonScatterFieldRect");
        private static readonly int _GridResolutionId = Shader.PropertyToID("_HectonScatterGridResolution");
        private static readonly int _CellSizeId = Shader.PropertyToID("_HectonScatterCellSize");
        private static readonly int _ScatterSeedId = Shader.PropertyToID("_HectonScatterSeed");
        private static readonly int _ScaleRangeId = Shader.PropertyToID("_HectonScatterScaleRange");
        private static readonly int _MinNormalYId = Shader.PropertyToID("_HectonScatterMinNormalY");
        private static readonly int _CameraPositionId = Shader.PropertyToID("_HectonScatterCameraPosition");
        private static readonly int _CameraForwardId = Shader.PropertyToID("_HectonScatterCameraForward");
        private static readonly int _MaxDistanceId = Shader.PropertyToID("_HectonScatterMaxDistance");
        private static readonly int _PeripheralDistanceId = Shader.PropertyToID("_HectonScatterPeripheralDistance");
        private static readonly int _PeripheralDotId = Shader.PropertyToID("_HectonScatterPeripheralDot");
        private static readonly int _FrustumPlanesId = Shader.PropertyToID("_HectonScatterFrustumPlanes");
        private static readonly int _ModInstanceMatricesId = Shader.PropertyToID("_HectonModInstanceMatrices");
        private static readonly int _ModInstanceCountId = Shader.PropertyToID("_HectonModInstanceCount");
        private const int MaxModInstancesPerFrame = 1024;

        private struct ScatterInstanceGpuData
        {
            public Vector4 PositionScale;
            public Vector4 NormalRotation;
        }

        private static GPUScatterDirector _activeInstance;

        [Header("References")]
        [SerializeField]
        [Tooltip("Authoritative vegetation bridge that owns the active MapMagic height payload.")]
        private HectonMapMagicVegetationBridge vegetationBridge;

        [SerializeField]
        [Tooltip("Compute shader that generates the GPU-resident scatter placement stream.")]
        private ComputeShader scatterCompute;

        [SerializeField]
        [Tooltip("Shared authored material for the indirect scatter draw. Per-instance payload is supplied through global graphics buffers.")]
        private Material scatterMaterial;

        [SerializeField]
        [Tooltip("Mesh rendered for each generated scatter instance.")]
        private Mesh scatterMesh;

        [SerializeField]
        [Tooltip("Optional camera override. When empty, the active player camera is resolved from the runtime context.")]
        private Camera viewCamera;

        [SerializeField]
        [Tooltip("Optional player transform override used to center the scatter field.")]
        private Transform playerTransform;

        [Header("Scatter Field")]
        [SerializeField, Range(12f, 80f)]
        [Tooltip("Radius in meters of the player-centered seabed scatter field.")]
        private float scatterRadiusMeters = 42f;

        [SerializeField, Range(0.5f, 4f)]
        [Tooltip("Cell size in meters used by the spatial hash placement grid.")]
        private float cellSizeMeters = 1.35f;

        [SerializeField, Range(0.15f, 2.5f)]
        [Tooltip("Minimum authored scale applied to generated scatter instances.")]
        private float minScale = 0.42f;

        [SerializeField, Range(0.15f, 3f)]
        [Tooltip("Maximum authored scale applied to generated scatter instances.")]
        private float maxScale = 1.18f;

        [SerializeField, Range(0.2f, 1f)]
        [Tooltip("Minimum terrain normal Y accepted for a generated seabed instance.")]
        private float minimumNormalY = 0.64f;

        [SerializeField, Min(256)]
        [Tooltip("Hard cap for generated scatter instances in the player field.")]
        private int maxScatterInstances = 16384;

        [SerializeField]
        [Tooltip("Stable seed used by the spatial hash when jittering cell placement.")]
        private uint scatterSeed = 149521u;

        [Header("GPU Culling")]
        [SerializeField, Range(24f, 120f)]
        [Tooltip("Absolute distance limit for generated scatter instances.")]
        private float maxVisibleDistance = 58f;

        [SerializeField, Range(8f, 64f)]
        [Tooltip("Distance after which peripheral cone culling starts rejecting off-axis scatter.")]
        private float peripheralCullDistance = 30f;

        [SerializeField, Range(-1f, 1f)]
        [Tooltip("Minimum dot product against the camera forward vector required beyond the peripheral cull distance.")]
        private float peripheralCullDot = 0.5f;

        [Header("Shadows")]
        [SerializeField]
        [Tooltip("Shadow mode used by the indirect draw.")]
        private ShadowCastingMode shadowCastingMode = ShadowCastingMode.On;

        [SerializeField]
        [Tooltip("Whether generated scatter receives shadows.")]
        private bool receiveShadows = true;

        [Header("Diagnostics")]
        [SerializeField] private int _debugGridResolution;
        [SerializeField] private int _debugVisibleCount;
        [SerializeField] private Bounds _debugDrawBounds;

        private bool _registered;
        private int _generateKernel = -1;
        private int _gridResolution;
        private GraphicsBuffer _instanceBuffer;
        private GraphicsBuffer _visibleIndicesBuffer;
        private GraphicsBuffer _argsBuffer;
        private GraphicsBuffer _modInstanceMatrixBuffer;
        private NativeArray<float4x4> _modInstanceMatrices;
        private readonly GraphicsBuffer.IndirectDrawIndexedArgs[] _argsUpload = new GraphicsBuffer.IndirectDrawIndexedArgs[1]; // COLD ALLOC: IndirectDrawIndexedArgs[1] — indirect indexed args upload cache for GPU scatter — owner: GPUScatterDirector
        private readonly Plane[] _frustumPlaneCache = new Plane[FrustumPlaneCount]; // COLD ALLOC: Plane[6] — reusable frustum plane cache for GPU scatter dispatch — owner: GPUScatterDirector
        private readonly Vector4[] _frustumPlaneUpload = new Vector4[FrustumPlaneCount]; // COLD ALLOC: Vector4[6] — reusable GPU frustum plane upload payload for GPU scatter dispatch — owner: GPUScatterDirector
        private int _modInstanceCount;

        private void Awake()
        {
            _activeInstance = this;
            ResolveDependencies();
            EnsureResources();
            EnsureModInstanceResources();
            TryRegister();
        }

        private void OnEnable()
        {
            _activeInstance = this;
            ResolveDependencies();
            EnsureResources();
            EnsureModInstanceResources();
            TryRegister();
        }

        private void OnDisable()
        {
            if (_activeInstance == this)
                _activeInstance = null;

            TryUnregister();
            ReleaseResources();
        }

        private void OnDestroy()
        {
            if (_activeInstance == this)
                _activeInstance = null;

            TryUnregister();
            ReleaseResources();
        }

        /// <summary>
        /// Generates and renders the current player-centered scatter field.
        /// </summary>
        public void Tick(float deltaTime)
        {
            if (deltaTime < 0f)
                return;

            ResolveDependencies();
            EnsureResources();
            EnsureModInstanceResources();
            FlushModInstanceLayer();
            if (scatterCompute == null ||
                _generateKernel < 0 ||
                scatterMesh == null ||
                scatterMaterial == null ||
                _instanceBuffer == null ||
                _visibleIndicesBuffer == null ||
                _argsBuffer == null ||
                viewCamera == null ||
                playerTransform == null ||
                vegetationBridge == null ||
                !vegetationBridge.TryGetActiveHeightTexturePayload(out HectonMapMagicVegetationBridge.TerrainHeightTexturePayload heightPayload))
            {
                _debugVisibleCount = 0;
                return;
            }

            PopulateFrustumPlaneUpload(viewCamera);

            Vector3 center = playerTransform.position;
            float diameter = scatterRadiusMeters * 2f;
            float minX = center.x - scatterRadiusMeters;
            float minZ = center.z - scatterRadiusMeters;
            Vector4 fieldRect = new Vector4(minX, minZ, diameter, diameter);

            _visibleIndicesBuffer.SetCounterValue(0u);
            scatterCompute.SetTexture(_generateKernel, _HeightTextureId, heightPayload.HeightTexture);
            scatterCompute.SetBuffer(_generateKernel, _ScatterInstancesId, _instanceBuffer);
            scatterCompute.SetBuffer(_generateKernel, _VisibleIndicesId, _visibleIndicesBuffer);
            scatterCompute.SetInt(_HeightResolutionId, heightPayload.HeightmapResolution);
            scatterCompute.SetVector(_TerrainPositionId, heightPayload.TerrainPosition);
            scatterCompute.SetVector(_TerrainSizeId, heightPayload.TerrainSize);
            scatterCompute.SetVector(_FieldRectId, fieldRect);
            scatterCompute.SetInt(_GridResolutionId, _gridResolution);
            scatterCompute.SetFloat(_CellSizeId, cellSizeMeters);
            scatterCompute.SetInt(_ScatterSeedId, unchecked((int)scatterSeed));
            scatterCompute.SetVector(_ScaleRangeId, new Vector4(math.min(minScale, maxScale), math.max(minScale, maxScale), 0f, 0f));
            scatterCompute.SetFloat(_MinNormalYId, math.saturate(minimumNormalY));
            scatterCompute.SetVector(_CameraPositionId, viewCamera.transform.position);
            scatterCompute.SetVector(_CameraForwardId, viewCamera.transform.forward);
            scatterCompute.SetFloat(_MaxDistanceId, math.max(1f, maxVisibleDistance));
            scatterCompute.SetFloat(_PeripheralDistanceId, math.max(0f, peripheralCullDistance));
            scatterCompute.SetFloat(_PeripheralDotId, math.clamp(peripheralCullDot, -1f, 1f));
            scatterCompute.SetVectorArray(_FrustumPlanesId, _frustumPlaneUpload);

            int candidateCount = _gridResolution * _gridResolution;
            int dispatchGroups = math.max(1, (candidateCount + ThreadGroupSize - 1) / ThreadGroupSize);
            scatterCompute.Dispatch(_generateKernel, dispatchGroups, 1, 1);

            GraphicsBuffer.CopyCount(_visibleIndicesBuffer, _argsBuffer, sizeof(uint));
            Shader.SetGlobalBuffer(_ScatterInstancesId, _instanceBuffer);
            Shader.SetGlobalBuffer(_VisibleIndicesId, _visibleIndicesBuffer);

            float terrainTop = heightPayload.TerrainPosition.y + heightPayload.TerrainSize.y;
            Bounds drawBounds = new Bounds(
                new Vector3(center.x, heightPayload.TerrainPosition.y + heightPayload.TerrainSize.y * 0.5f, center.z),
                new Vector3(diameter, math.max(8f, terrainTop - heightPayload.TerrainPosition.y), diameter));

            Graphics.DrawMeshInstancedIndirect(
                scatterMesh,
                0,
                scatterMaterial,
                drawBounds,
                _argsBuffer,
                0,
                null,
                shadowCastingMode,
                receiveShadows,
                gameObject.layer,
                viewCamera);

            _debugGridResolution = _gridResolution;
            _debugVisibleCount = candidateCount;
            _debugDrawBounds = drawBounds;
        }

        /// <summary>
        /// Adds one mod-authored matrix to the reserved GPU instancing layer.
        /// </summary>
        public static bool SubmitModInstanceMatrix(uint modHash, uint resourceHash, in float4x4 matrix)
        {
            GPUScatterDirector instance = _activeInstance;
            if (instance == null || modHash == 0u || resourceHash == 0u)
                return false;

            return instance.TrySubmitModInstanceMatrix(in matrix);
        }

        private void ResolveDependencies()
        {
            WorldRuntimeReferenceUtility.TryResolveHectonMapMagicVegetationBridge(ref vegetationBridge);
            WorldRuntimeReferenceUtility.TryResolvePlayerTransform(ref playerTransform);

            if (viewCamera == null && playerTransform != null)
            {
                viewCamera = GlobalRegistry.Player != null && GlobalRegistry.Player.PlayerCamera != null
                    ? GlobalRegistry.Player.PlayerCamera
                    : ComponentReferenceUtility.ResolveOwnedComponent<Camera>(playerTransform);
            }
        }

        private void EnsureResources()
        {
            if (scatterCompute == null || scatterMesh == null || scatterMaterial == null)
                return;

            if (_generateKernel < 0)
                _generateKernel = scatterCompute.FindKernel("GenerateScatterInstances");

            int requestedGrid = math.max(8, Mathf.CeilToInt((scatterRadiusMeters * 2f) / math.max(0.25f, cellSizeMeters)));
            int requestedCapacity = requestedGrid * requestedGrid;
            int clampedCapacity = math.min(math.max(1, maxScatterInstances), requestedCapacity);
            _gridResolution = math.max(1, Mathf.FloorToInt(math.sqrt(clampedCapacity)));
            int resolvedCapacity = _gridResolution * _gridResolution;
            EnsureInstanceBufferCapacity(resolvedCapacity);
            EnsureVisibleIndexBufferCapacity(resolvedCapacity);
            EnsureIndirectArgsBuffer();
        }

        private void EnsureModInstanceResources()
        {
            if (!_modInstanceMatrices.IsCreated)
                _modInstanceMatrices = new NativeArray<float4x4>(MaxModInstancesPerFrame, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<float4x4>[1024] - mod instancing matrix upload staging - owner: GPUScatterDirector

            if (_modInstanceMatrixBuffer == null)
                _modInstanceMatrixBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, MaxModInstancesPerFrame, UnsafeUtility.SizeOf<float4x4>()); // COLD ALLOC: GraphicsBuffer[1024] - reserved mod instancing matrix layer - owner: GPUScatterDirector
        }

        private bool TrySubmitModInstanceMatrix(in float4x4 matrix)
        {
            EnsureModInstanceResources();
            if (!_modInstanceMatrices.IsCreated || _modInstanceCount >= MaxModInstancesPerFrame)
                return false;

            _modInstanceMatrices[_modInstanceCount] = matrix;
            _modInstanceCount++;
            return true;
        }

        private void FlushModInstanceLayer()
        {
            if (_modInstanceMatrixBuffer == null || !_modInstanceMatrices.IsCreated)
                return;

            if (_modInstanceCount > 0)
            {
                _modInstanceMatrixBuffer.SetData(_modInstanceMatrices, 0, 0, _modInstanceCount);
                Shader.SetGlobalBuffer(_ModInstanceMatricesId, _modInstanceMatrixBuffer);
            }

            Shader.SetGlobalInt(_ModInstanceCountId, _modInstanceCount);
            _modInstanceCount = 0;
        }

        private void EnsureInstanceBufferCapacity(int requiredCapacity)
        {
            if (_instanceBuffer != null && _instanceBuffer.count >= requiredCapacity)
                return;

            ReleaseBuffer(ref _instanceBuffer);
            _instanceBuffer = GraphicsBufferUploadUtility.CreateStructuredBuffer<ScatterInstanceGpuData>(requiredCapacity); // COLD ALLOC: GraphicsBuffer[gridResolution²] — persistent GPU scatter instance payload buffer — owner: GPUScatterDirector
        }

        private void EnsureVisibleIndexBufferCapacity(int requiredCapacity)
        {
            if (_visibleIndicesBuffer != null && _visibleIndicesBuffer.count >= requiredCapacity)
                return;

            ReleaseBuffer(ref _visibleIndicesBuffer);
            _visibleIndicesBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Append, requiredCapacity, UnsafeUtility.SizeOf<uint>()); // COLD ALLOC: GraphicsBuffer[gridResolution²] — append visible-index buffer for GPU scatter indirect draw — owner: GPUScatterDirector
        }

        private void EnsureIndirectArgsBuffer()
        {
            if (_argsBuffer == null)
                _argsBuffer = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments, 1, GraphicsBuffer.IndirectDrawIndexedArgs.size); // COLD ALLOC: GraphicsBuffer[1] — indirect indexed draw args for GPU scatter — owner: GPUScatterDirector

            _argsUpload[0].indexCountPerInstance = scatterMesh != null ? scatterMesh.GetIndexCount(0) : 0u;
            _argsUpload[0].instanceCount = 0u;
            _argsUpload[0].startIndex = scatterMesh != null ? scatterMesh.GetIndexStart(0) : 0u;
            _argsUpload[0].baseVertexIndex = scatterMesh != null ? (uint)math.max(0, scatterMesh.GetBaseVertex(0)) : 0u;
            _argsUpload[0].startInstance = 0u;
            _argsBuffer.SetData(_argsUpload);
        }

        private void PopulateFrustumPlaneUpload(Camera cullCamera)
        {
            GeometryUtility.CalculateFrustumPlanes(cullCamera, _frustumPlaneCache);
            for (int planeIndex = 0; planeIndex < FrustumPlaneCount; planeIndex++)
            {
                Plane plane = _frustumPlaneCache[planeIndex];
                _frustumPlaneUpload[planeIndex] = new Vector4(plane.normal.x, plane.normal.y, plane.normal.z, plane.distance);
            }
        }

        private void TryRegister()
        {
            if (_registered || !Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            GlobalRegistry.RegisterUpdatable(this, PriorityLayer.Environment);
            _registered = true;
        }

        private void TryUnregister()
        {
            if (!_registered)
                return;

            GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Environment);
            _registered = false;
        }

        private void ReleaseResources()
        {
            ReleaseBuffer(ref _instanceBuffer);
            ReleaseBuffer(ref _visibleIndicesBuffer);
            ReleaseBuffer(ref _argsBuffer);
            ReleaseBuffer(ref _modInstanceMatrixBuffer);
            if (_modInstanceMatrices.IsCreated)
            {
                _modInstanceMatrices.Dispose();
                _modInstanceMatrices = default;
            }

            _modInstanceCount = 0;
        }

        private static void ReleaseBuffer(ref GraphicsBuffer buffer)
        {
            if (buffer == null)
                return;

            buffer.Release();
            buffer = null;
        }
    }
}
