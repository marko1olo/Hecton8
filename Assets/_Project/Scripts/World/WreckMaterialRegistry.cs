using Hecton8.Core;
using Hecton8.Core.Memory;
using Hecton8.Gameplay;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace Hecton8.World
{
    /// <summary>
    /// Owns the render contracts for procedural wreck modules and publishes them through BRG direct draws.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WreckMaterialRegistry : MonoBehaviour, ISlowTickable, ILateFrameTickable, IOriginShiftListener, IGlobalRegistryHotSwapListener
    {
        private const int MaxModuleContracts = 16;
        private const int SharedWreckTierMaterialCount = 2;
        private const int WreckBrgMetadataCount = 1;
        private const int FrustumPlaneCount = 6;
        private const int DefaultMaxInstancesPerWreckBatch = 2048;
        private const int InvalidFrustumVersion = -1;
        private const SystemID WreckBrgVaultOwner = SystemID.WorldStreaming;
        private const BufferID WreckBrgBatchMetadataBufferId = BufferID.WreckBrgBatchMetadata;
        private const float FrustumRefreshPositionEpsilonSq = 0.25f;
        private const float FrustumRefreshRotationDotThreshold = 0.99995f;
        private const float FrustumRefreshProjectionEpsilonSq = 0.000001f;
        private const string IndirectWreckShaderName = "Hecton8/World/WreckIndirectLit";
        private const double BrgUploadTelemetryThresholdMs = 0.2d;
        private const uint WreckBrgUploadWarningHash = 0x5755504Cu; // WUPL
        private const uint WreckBrgContextHash = 0x57425247u; // WBRG
        private static readonly int _WreckMatricesId = Shader.PropertyToID("_HectonWreckMatrices");
        private static readonly int _WreckAgesId = Shader.PropertyToID("_HectonWreckAges");

        [System.Serializable]
#pragma warning disable 0649 // Unity serializes module render contracts from authored registry data.
        private struct ModuleRenderContract
        {
            [Tooltip("Optional mesh override. When empty, the generator structural mesh for the same module slot is used.")]
            public Mesh MeshOverride;

            [Tooltip("Optional shared indirect material override. When empty, the tier fallback material is used. Must use Hecton8/World/WreckIndirectLit.")]
            public Material MaterialOverride;

            [Tooltip("Submesh index used by the BRG draw.")]
            public int SubMeshIndex;

            [Tooltip("Shadow mode used by the BRG draw.")]
            public ShadowCastingMode ShadowCastingMode;

            [Tooltip("Receive-shadows flag used by the BRG draw.")]
            public bool ReceiveShadows;
        }
#pragma warning restore 0649

        private sealed class ModuleBatch
        {
            private readonly WreckMaterialRegistry _owner;
            private readonly int _moduleIndex;
            private BatchRendererGroup _batchRendererGroup;
            private GraphicsBuffer _batchHandleBuffer;
            private BatchID _batchId;
            private BatchMeshID _batchMeshId;
            private BatchMaterialID _batchMaterialId;
            private Mesh _registeredMesh;
            private Material _registeredMaterial;
            private GraphicsBuffer _registeredBatchBuffer;
            private Material _sharedMaterial;
            private GraphicsBuffer _matrixBufferA;
            private GraphicsBuffer _matrixBufferB;
            private GraphicsBuffer _activeMatrixBuffer;
            private GraphicsBuffer _ageBufferA;
            private GraphicsBuffer _ageBufferB;
            private GraphicsBuffer _activeAgeBuffer;
            private int _uploadBufferIndex;
            private Matrix4x4[] _matrices;
            private float[] _ages;
            private Matrix4x4[] _visibleMatrices;
            private float[] _visibleAges;
            private int _matrixCount;
            private int _visibleCount;
            private int _visibleSubsetFrustumVersion;
            private Bounds _drawBounds;
            private float3 _meshLocalBoundsCenter;
            private float3 _meshLocalBoundsExtents;
            private Mesh _mesh;
            private int _subMeshIndex;
            private int _layer;
            private int _uploadedInstanceCount;
            private ShadowCastingMode _shadowCastingMode;
            private bool _receiveShadows;
            private bool _matrixUploadDirty;

            public ModuleBatch(WreckMaterialRegistry owner, int moduleIndex)
            {
                _owner = owner;
                _moduleIndex = moduleIndex;
                _visibleSubsetFrustumVersion = InvalidFrustumVersion;
            }

            public bool HasContent => _matrixCount > 0;

            public bool HasPendingMatrixUpload => _matrixUploadDirty;

            public bool HasPendingVisibilityCull => false;

            public void EnsureCapacity(int minimumCapacity)
            {
                int fixedCapacity = _owner.ResolveMaxInstancesPerWreckBatch(minimumCapacity);
                if (_matrices == null || _matrices.Length < fixedCapacity)
                    _matrices = new Matrix4x4[fixedCapacity]; // COLD ALLOC: Matrix4x4[payload-clamped capacity] - BRG wreck module matrix staging - owner: WreckMaterialRegistry
                if (_ages == null || _ages.Length < fixedCapacity)
                    _ages = new float[fixedCapacity]; // COLD ALLOC: float[payload-clamped capacity] - BRG wreck module age staging - owner: WreckMaterialRegistry
                if (_visibleMatrices == null || _visibleMatrices.Length < fixedCapacity)
                    _visibleMatrices = new Matrix4x4[fixedCapacity]; // COLD ALLOC: Matrix4x4[payload-clamped capacity] - BRG visible matrix upload staging - owner: WreckMaterialRegistry
                if (_visibleAges == null || _visibleAges.Length < fixedCapacity)
                    _visibleAges = new float[fixedCapacity]; // COLD ALLOC: float[payload-clamped capacity] - managed camera-visible age upload staging - owner: WreckMaterialRegistry
            }

            public void Reset()
            {
                _matrixCount = 0;
                _visibleCount = 0;
                _uploadedInstanceCount = 0;
                _visibleSubsetFrustumVersion = InvalidFrustumVersion;
                _matrixUploadDirty = false;
            }

            public void Configure(
                Mesh mesh,
                Material material,
                int subMeshIndex,
                ShadowCastingMode shadowCastingMode,
                bool receiveShadows,
                int layer)
            {
                _mesh = mesh;
                _sharedMaterial = _owner.ResolveSharedIndirectMaterial(material);
                _subMeshIndex = math.max(0, subMeshIndex);
                _shadowCastingMode = shadowCastingMode;
                _receiveShadows = receiveShadows;
                _layer = layer;
                Bounds meshBounds = mesh != null ? mesh.bounds : new Bounds(Vector3.zero, Vector3.one);
                Vector3 center = meshBounds.center;
                Vector3 extents = meshBounds.extents;
                _meshLocalBoundsCenter = new float3(center.x, center.y, center.z);
                _meshLocalBoundsExtents = math.max(new float3(extents.x, extents.y, extents.z), new float3(0.05f));
            }

            public void AddInstance(Matrix4x4 matrix, float age01)
            {
                if (_matrices == null || _ages == null)
                    return;

                if (_matrixCount >= _matrices.Length || _matrixCount >= _ages.Length)
                    return;

                _matrices[_matrixCount] = matrix;
                _ages[_matrixCount] = math.saturate(age01);
                _matrixCount++;
            }

            public bool Publish(
                Bounds drawBounds,
                float4[] frustumPlanes,
                bool enableFrustumCulling,
                int frustumVersion,
                bool forceCullCompletion = true)
            {
                _drawBounds = drawBounds;
                if (_mesh == null || _sharedMaterial == null || _matrices == null || _matrixCount <= 0)
                {
                    _uploadedInstanceCount = 0;
                    return false;
                }

                if (!HasUploadResourcesReady(_matrixCount))
                {
                    _uploadedInstanceCount = 0;
                    return false;
                }

                if (!TryCullVisibleSubset(
                        frustumPlanes,
                        enableFrustumCulling,
                        frustumVersion,
                        forceCullCompletion,
                        out int visibleCount))
                {
                    return false;
                }

                if (visibleCount <= 0)
                {
                    _uploadedInstanceCount = 0;
                    _matrixUploadDirty = false;
                    return true;
                }

                if (_batchRendererGroup == null ||
                    _sharedMaterial == null ||
                    _matrixBufferA == null ||
                    _matrixBufferB == null ||
                    _ageBufferA == null ||
                    _ageBufferB == null)
                {
                    _uploadedInstanceCount = 0;
                    return false;
                }

                long uploadStartTimestamp = global::System.Diagnostics.Stopwatch.GetTimestamp();
                GraphicsBuffer matrixWriteBuffer = _uploadBufferIndex == 0 ? _matrixBufferA : _matrixBufferB;
                GraphicsBuffer ageWriteBuffer = _uploadBufferIndex == 0 ? _ageBufferA : _ageBufferB;
                UploadManagedArray(matrixWriteBuffer, _visibleMatrices, visibleCount);
                UploadManagedArray(ageWriteBuffer, _visibleAges, visibleCount);
                _activeMatrixBuffer = matrixWriteBuffer;
                _activeAgeBuffer = ageWriteBuffer;
                _uploadBufferIndex ^= 1;
                _sharedMaterial.SetBuffer(_WreckMatricesId, _activeMatrixBuffer);
                _sharedMaterial.SetBuffer(_WreckAgesId, _activeAgeBuffer);
                SyncBatchBuffer(_activeMatrixBuffer);
                SyncBatchRegistration();
                _uploadedInstanceCount = visibleCount;
                _matrixUploadDirty = false;
                _batchRendererGroup.SetGlobalBounds(_drawBounds);
                WreckMaterialRegistry.PublishBrgUploadWarningIfNeeded(uploadStartTimestamp);
                return true;
            }

            public bool PrepareUploadResources()
            {
                if (_mesh == null || _sharedMaterial == null || _matrices == null || _matrixCount <= 0)
                    return false;

                int preparedInstanceCapacity = math.max(1, _matrixCount);
                EnsureResources();
                if (_batchRendererGroup == null || _sharedMaterial == null)
                    return false;

                EnsureMatrixBufferCapacity(preparedInstanceCapacity);
                EnsureAgeBufferCapacity(preparedInstanceCapacity);
                SyncBatchRegistration();
                return _matrixBufferA != null &&
                       _matrixBufferB != null &&
                       _ageBufferA != null &&
                       _ageBufferB != null;
            }

            public bool HasUploadResourcesReady(int instanceCount)
            {
                return _batchRendererGroup != null &&
                       _sharedMaterial != null &&
                       HasMatrixBufferCapacity(instanceCount) &&
                       HasAgeBufferCapacity(instanceCount);
            }

            private bool TryCullVisibleSubset(
                float4[] frustumPlanes,
                bool enableFrustumCulling,
                int frustumVersion,
                bool forceCompletion,
                out int visibleCount)
            {
                visibleCount = 0;
                if (_matrices == null || _ages == null || _visibleMatrices == null || _visibleAges == null)
                    return true;

                int count = _matrixCount;
                if (count <= 0)
                    return true;

                int visibleCapacity = math.min(_visibleMatrices.Length, _visibleAges.Length);
                if (visibleCapacity <= 0)
                    return true;

                if (_visibleSubsetFrustumVersion != frustumVersion)
                    CullVisibleSubset(frustumPlanes, enableFrustumCulling, frustumVersion, count, visibleCapacity);

                visibleCount = _visibleCount;
                return true;
            }

            private void CullVisibleSubset(
                float4[] frustumPlanes,
                bool enableFrustumCulling,
                int frustumVersion,
                int count,
                int visibleCapacity)
            {
                bool hasPlanes = enableFrustumCulling && frustumPlanes != null && frustumPlanes.Length >= FrustumPlaneCount;
                _visibleCount = 0;
                int safeCount = math.min(count, _matrices.Length);
                for (int index = 0; index < safeCount; index++)
                {
                    if (_visibleCount >= visibleCapacity)
                        break;

                    Matrix4x4 matrix = _matrices[index];
                    if (hasPlanes && !IsMatrixAabbVisible(in matrix, frustumPlanes))
                        continue;

                    _visibleMatrices[_visibleCount] = matrix;
                    _visibleAges[_visibleCount] = index < _ages.Length ? math.saturate(_ages[index]) : 0.5f;
                    _visibleCount++;
                }

                _visibleSubsetFrustumVersion = frustumVersion;
            }

            private bool IsMatrixAabbVisible(in Matrix4x4 matrix, float4[] frustumPlanes)
            {
                float3 axisX = new float3(matrix.m00, matrix.m10, matrix.m20);
                float3 axisY = new float3(matrix.m01, matrix.m11, matrix.m21);
                float3 axisZ = new float3(matrix.m02, matrix.m12, matrix.m22);
                float3 translation = new float3(matrix.m03, matrix.m13, matrix.m23);
                float3 center = translation + axisX * _meshLocalBoundsCenter.x + axisY * _meshLocalBoundsCenter.y + axisZ * _meshLocalBoundsCenter.z;
                float3 extents =
                    math.abs(axisX) * _meshLocalBoundsExtents.x +
                    math.abs(axisY) * _meshLocalBoundsExtents.y +
                    math.abs(axisZ) * _meshLocalBoundsExtents.z;

                for (int planeIndex = 0; planeIndex < FrustumPlaneCount; planeIndex++)
                {
                    float4 plane = frustumPlanes[planeIndex];
                    float projectionRadius =
                        math.abs(plane.x) * extents.x +
                        math.abs(plane.y) * extents.y +
                        math.abs(plane.z) * extents.z;
                    if (math.dot(plane.xyz, center) + plane.w + projectionRadius < 0f)
                        return false;
                }

                return true;
            }

            public void ApplyOriginShift(Vector3 runtimeOffset)
            {
                if (_matrices != null)
                {
                    int count = _matrixCount;
                    if (count > 0)
                        ApplyOriginShiftToMatrices(_matrices, count, runtimeOffset);
                }

                if (_visibleMatrices != null)
                {
                    int visibleCount = _visibleCount;
                    if (visibleCount > 0)
                        ApplyOriginShiftToMatrices(_visibleMatrices, visibleCount, runtimeOffset);

                    int uploadCount = math.min(_uploadedInstanceCount, _visibleCount);
                    if (uploadCount > 0)
                        _matrixUploadDirty = true;
                    else
                        _matrixUploadDirty = false;
                }

                _drawBounds.center += runtimeOffset;
            }

            public bool FlushPendingMatrixUpload()
            {
                if (!_matrixUploadDirty)
                    return true;

                int uploadCount = math.min(_uploadedInstanceCount, _visibleCount);
                if (uploadCount <= 0 || _visibleMatrices == null)
                {
                    _uploadedInstanceCount = 0;
                    _matrixUploadDirty = false;
                    return true;
                }

                if (_batchRendererGroup == null || _sharedMaterial == null)
                    return false;

                if (!HasMatrixBufferCapacity(uploadCount))
                    return false;

                GraphicsBuffer matrixWriteBuffer = _uploadBufferIndex == 0 ? _matrixBufferA : _matrixBufferB;
                if (matrixWriteBuffer == null)
                    return false;

                long uploadStartTimestamp = global::System.Diagnostics.Stopwatch.GetTimestamp();
                _uploadedInstanceCount = uploadCount;
                UploadManagedArray(matrixWriteBuffer, _visibleMatrices, uploadCount);
                _activeMatrixBuffer = matrixWriteBuffer;
                _uploadBufferIndex ^= 1;
                _sharedMaterial.SetBuffer(_WreckMatricesId, _activeMatrixBuffer);
                SyncBatchBuffer(_activeMatrixBuffer);
                _batchRendererGroup.SetGlobalBounds(_drawBounds);
                _matrixUploadDirty = false;
                WreckMaterialRegistry.PublishBrgUploadWarningIfNeeded(uploadStartTimestamp);
                return true;
            }

            private static void ApplyOriginShiftToMatrices(Matrix4x4[] matrices, int count, Vector3 runtimeOffset)
            {
                int safeCount = math.min(count, matrices != null ? matrices.Length : 0);
                for (int i = 0; i < safeCount; i++)
                {
                    Matrix4x4 matrix = matrices[i];
                    matrix.m03 += runtimeOffset.x;
                    matrix.m13 += runtimeOffset.y;
                    matrix.m23 += runtimeOffset.z;
                    matrices[i] = matrix;
                }
            }

            public void Dispose()
            {
                if (_batchRendererGroup != null)
                {
                    if (!_batchId.Equals(default))
                        _batchRendererGroup.RemoveBatch(_batchId);
                    if (!_batchMeshId.Equals(default))
                        _batchRendererGroup.UnregisterMesh(_batchMeshId);
                    if (!_batchMaterialId.Equals(default))
                        _batchRendererGroup.UnregisterMaterial(_batchMaterialId);
                    _batchRendererGroup.Dispose();
                    _batchRendererGroup = null;
                }

                if (_batchHandleBuffer != null)
                {
                    _batchHandleBuffer.Release();
                    _batchHandleBuffer = null;
                }

                ReleaseBuffer(ref _matrixBufferA);
                ReleaseBuffer(ref _matrixBufferB);
                ReleaseBuffer(ref _ageBufferA);
                ReleaseBuffer(ref _ageBufferB);
                _activeMatrixBuffer = null;
                _activeAgeBuffer = null;
                _uploadBufferIndex = 0;

                _sharedMaterial = null;
                _registeredMaterial = null;
                _registeredMesh = null;
                _batchMeshId = default;
                _batchMaterialId = default;
                _batchId = default;
                _registeredBatchBuffer = null;
                _uploadedInstanceCount = 0;
                _matrices = null;
                _ages = null;
                _visibleMatrices = null;
                _visibleAges = null;
                _matrixCount = 0;
                _visibleCount = 0;
                _matrixUploadDirty = false;
            }

            private void EnsureResources()
            {
                if (_batchRendererGroup != null)
                    return;

                _batchRendererGroup = new BatchRendererGroup(new BatchRendererGroupCreateInfo
                {
                    cullingCallback = OnPerformCulling,
                    userContext = System.IntPtr.Zero
                });

                if (!_owner.CanAttemptBatchMetadataAcquire())
                {
                    _batchRendererGroup.Dispose();
                    _batchRendererGroup = null;
                    return;
                }

                _batchHandleBuffer = HectonBatchRendererGroupUtility.CreateBatchHandleBuffer(); // COLD ALLOC: GraphicsBuffer[1] - BRG registration handle buffer for wreck module renderer - owner: WreckMaterialRegistry

                if (!_owner.TryWriteBatchMetadata(out MetadataValue batchMetadataValue))
                {
                    _batchHandleBuffer.Release();
                    _batchHandleBuffer = null;
                    _batchRendererGroup.Dispose();
                    _batchRendererGroup = null;
                    return;
                }

                bool batchAdded = false;
                try
                {
                    NativeArray<MetadataValue> batchMetadata = H8Memory.Allocate<MetadataValue>(
                        WreckBrgMetadataCount,
                        WreckBrgVaultOwner,
                        Allocator.Temp,
                        NativeArrayOptions.ClearMemory);
                    try
                    {
                        batchMetadata[0] = batchMetadataValue;
                        _batchId = _batchRendererGroup.AddBatch(batchMetadata, _batchHandleBuffer.bufferHandle);
                    }
                    finally
                    {
                        if (batchMetadata.IsCreated)
                            H8Memory.Release(ref batchMetadata, WreckBrgVaultOwner);
                    }

                    batchAdded = !_batchId.Equals(default);
                }
                finally
                {
                    if (!batchAdded)
                    {
                        _batchId = default;
                        ReleaseBuffer(ref _batchHandleBuffer);
                        _batchRendererGroup.Dispose();
                        _batchRendererGroup = null;
                    }
                }
            }

            private static void ReleaseBuffer(ref GraphicsBuffer buffer)
            {
                if (buffer == null)
                    return;

                buffer.Release();
                buffer = null;
            }

            private static unsafe void UploadManagedArray<T>(GraphicsBuffer destination, T[] source, int count) where T : unmanaged
            {
                if (destination == null || source == null)
                    return;

                int safeCount = math.min(count, math.min(source.Length, destination.count));
                if (safeCount <= 0)
                    return;

                NativeArray<T> mapped = destination.LockBufferForWrite<T>(0, safeCount);
                try
                {
                    fixed (T* sourcePtr = source)
                    {
                        void* destinationPtr = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(mapped);
                        UnsafeUtility.MemCpy(destinationPtr, sourcePtr, safeCount * UnsafeUtility.SizeOf<T>());
                    }
                }
                finally
                {
                    destination.UnlockBufferAfterWrite<T>(safeCount);
                }
            }

            private void EnsureMatrixBufferCapacity(int instanceCount)
            {
                int required = RoundUpPowerOfTwo(instanceCount);
                if (_matrixBufferA != null &&
                    _matrixBufferA.count >= required &&
                    _matrixBufferB != null &&
                    _matrixBufferB.count >= required)
                {
                    if (_activeMatrixBuffer == null)
                        _activeMatrixBuffer = _matrixBufferA;
                    return;
                }

                ReleaseBuffer(ref _matrixBufferA);
                ReleaseBuffer(ref _matrixBufferB);

                _matrixBufferA = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<Matrix4x4>(required); // COLD ALLOC: GraphicsBuffer[NextPowerOfTwo(instanceCount)] A - wreck module matrix upload buffer - owner: WreckMaterialRegistry
                _matrixBufferB = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<Matrix4x4>(required); // COLD ALLOC: GraphicsBuffer[NextPowerOfTwo(instanceCount)] B - wreck module matrix upload buffer - owner: WreckMaterialRegistry
                _activeMatrixBuffer = _matrixBufferA;
                _uploadBufferIndex = 0;
                _registeredBatchBuffer = null;
            }

            private bool HasMatrixBufferCapacity(int instanceCount)
            {
                int required = RoundUpPowerOfTwo(instanceCount);
                return _matrixBufferA != null &&
                       _matrixBufferA.count >= required &&
                       _matrixBufferB != null &&
                       _matrixBufferB.count >= required;
            }

            private void EnsureAgeBufferCapacity(int instanceCount)
            {
                int required = RoundUpPowerOfTwo(instanceCount);
                if (_ageBufferA != null &&
                    _ageBufferA.count >= required &&
                    _ageBufferB != null &&
                    _ageBufferB.count >= required)
                {
                    if (_activeAgeBuffer == null)
                        _activeAgeBuffer = _ageBufferA;
                    return;
                }

                ReleaseBuffer(ref _ageBufferA);
                ReleaseBuffer(ref _ageBufferB);

                _ageBufferA = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<float>(required); // COLD ALLOC: GraphicsBuffer[NextPowerOfTwo(instanceCount)] A - wreck module age metadata upload buffer - owner: WreckMaterialRegistry
                _ageBufferB = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<float>(required); // COLD ALLOC: GraphicsBuffer[NextPowerOfTwo(instanceCount)] B - wreck module age metadata upload buffer - owner: WreckMaterialRegistry
                _activeAgeBuffer = _ageBufferA;
            }

            private bool HasAgeBufferCapacity(int instanceCount)
            {
                int required = RoundUpPowerOfTwo(instanceCount);
                return _ageBufferA != null &&
                       _ageBufferA.count >= required &&
                       _ageBufferB != null &&
                       _ageBufferB.count >= required;
            }

            private static int RoundUpPowerOfTwo(int value)
            {
                uint v = (uint)math.max(1, value);
                v--;
                v |= v >> 1;
                v |= v >> 2;
                v |= v >> 4;
                v |= v >> 8;
                v |= v >> 16;
                v++;
                return v > int.MaxValue ? int.MaxValue : (int)v;
            }

            private void SyncBatchRegistration()
            {
                if (_batchRendererGroup == null || _mesh == null || _sharedMaterial == null)
                    return;

                if (_registeredMesh != _mesh)
                {
                    if (!_batchMeshId.Equals(default))
                        _batchRendererGroup.UnregisterMesh(_batchMeshId);

                    _batchMeshId = _batchRendererGroup.RegisterMesh(_mesh);
                    _registeredMesh = _mesh;
                }

                if (_registeredMaterial != _sharedMaterial)
                {
                    if (!_batchMaterialId.Equals(default))
                        _batchRendererGroup.UnregisterMaterial(_batchMaterialId);

                    _batchMaterialId = _batchRendererGroup.RegisterMaterial(_sharedMaterial);
                    _registeredMaterial = _sharedMaterial;
                }
            }

            private void SyncBatchBuffer(GraphicsBuffer matrixBuffer)
            {
                if (_batchRendererGroup == null || _batchId.Equals(default) || matrixBuffer == null)
                    return;

                if (ReferenceEquals(_registeredBatchBuffer, matrixBuffer))
                    return;

                _batchRendererGroup.SetBatchBuffer(_batchId, matrixBuffer.bufferHandle);
                _registeredBatchBuffer = matrixBuffer;
            }

            private JobHandle OnPerformCulling(
                BatchRendererGroup rendererGroup,
                BatchCullingContext cullingContext,
                BatchCullingOutput cullingOutput,
                System.IntPtr userContext)
            {
                int instanceCount = _uploadedInstanceCount;
                if (instanceCount <= 0 ||
                    _batchId.Equals(default) ||
                    _batchMeshId.Equals(default) ||
                    _batchMaterialId.Equals(default))
                {
                    HectonBatchRendererGroupUtility.WriteDirectDrawOutput(
                        cullingOutput,
                        HectonBatchRendererGroupUtility.AllocateDirectDrawOutput(0, 0, 0));
                    return default;
                }

                if (!HectonBatchRendererGroupUtility.IsBoundsVisible(cullingContext.cullingPlanes, _drawBounds))
                {
                    HectonBatchRendererGroupUtility.WriteDirectDrawOutput(
                        cullingOutput,
                        HectonBatchRendererGroupUtility.AllocateDirectDrawOutput(0, 0, 0));
                    return default;
                }

                HectonBatchRendererGroupUtility.WriteAllVisibleSingleDrawOutput(
                    cullingOutput,
                    instanceCount,
                    _batchId,
                    _batchMeshId,
                    _batchMaterialId,
                    _layer,
                    _subMeshIndex,
                    _shadowCastingMode,
                    _receiveShadows,
                    MotionVectorGenerationMode.Object);
                return default;
            }
        }

        [Header("Indirect Shader")]
        [SerializeField]
        [Tooltip("Shader used by BRG wreck draws. Must consume _HectonWreckMatrices in the vertex path.")]
        private Shader indirectWreckShader;

        [Header("Tier Fallback Materials")]
        [SerializeField]
        [Tooltip("Static shared wreck materials. Slot 0 is Essential. Slot 1 is Detail and Clutter. Each material must use Hecton8/World/WreckIndirectLit.")]
        private Material[] wreckageTierSharedMaterials = { null, null };

        [SerializeField]
        [Tooltip("Legacy fallback only when the shared tier pool slot is empty. Must use Hecton8/World/WreckIndirectLit.")]
        private Material essentialTierMaterial;

        [SerializeField]
        [Tooltip("Legacy fallback only when the shared tier pool slot is empty. Must use Hecton8/World/WreckIndirectLit.")]
        private Material detailTierMaterial;

        [SerializeField]
        [Tooltip("Legacy fallback only when the shared tier pool slot is empty. Must use Hecton8/World/WreckIndirectLit.")]
        private Material clutterTierMaterial;

        [Header("Module Contracts")]
        [SerializeField]
        [Tooltip("Optional per-slot module render overrides. Slot index must match the generator module-definition index.")]
        private ModuleRenderContract[] moduleContracts = new ModuleRenderContract[MaxModuleContracts];

        [SerializeField]
        [Tooltip("When true, all wreck matrices are published through one BRG draw command using the selected module contract. When false, duplicate active material bindings are rejected because per-batch buffers are material-bound.")]
        private bool forceSingleDrawBatch = true;

        [SerializeField, Range(0, MaxModuleContracts - 1)]
        [Tooltip("Preferred module slot used as the one-draw mesh/material contract when Force Single Draw Batch is enabled.")]
        private int singleDrawModuleIndex;

        [SerializeField, Min(1)]
        [Tooltip("Fixed native-list capacity per wreck BRG batch. Runtime publish drops excess instances instead of resizing NativeLists.")]
        private int maxInstancesPerWreckBatch = DefaultMaxInstancesPerWreckBatch;

        [Header("Encrypted PDA Signal")]
        [SerializeField, Min(1f)]
        [Tooltip("Runtime distance at which the wreck emits one scan ping through ScanEvents.")]
        private float pdaSignalRadiusMeters = 200f;

        [SerializeField, Min(1f)]
        [Tooltip("Runtime radius sent to ScanEvents when the player enters the wreck signal range.")]
        private float pdaSignalPingRadiusMeters = 32f;

        [SerializeField, Min(1f)]
        [Tooltip("Distance required to re-arm the low-frequency ping after the player leaves the wreck.")]
        private float pdaSignalRearmRadiusMeters = 240f;

        private ModuleBatch[] _moduleBatches;
        private Bounds _publishedWorldBounds;
        private AbsoluteUniversePosition _publishedWreckCenterAup;
        private Transform _playerTransform;
        private Camera _viewCamera;
        private Plane[] _frustumPlaneCache;
        private float4[] _frustumPlanes;
        private Vector3 _lastFrustumCameraPosition;
        private Quaternion _lastFrustumCameraRotation = Quaternion.identity;
        private float4 _lastFrustumProjectionSignature0;
        private float4 _lastFrustumProjectionSignature1;
        private int _lastFrustumPixelWidth;
        private int _lastFrustumPixelHeight;
        private int _frustumStateVersion;
        private int _skippedVisibilityUploadCount;
        private bool _hasPublishedWreck;
        private bool _registeredSlowTick;
        private bool _registeredLateFrameTick;
        private bool _originShiftListenerRegistered;
        private bool _hotSwapListenerRegistered;
        private bool _hasRuntimeDispatcher;
        private IDataVault _dataVault;
        private VaultGenerationHandle<MetadataValue> _batchMetadataHandle;
        private IDataVault _batchMetadataWriteVault;
        private IPlayerRuntimeContext _playerRuntimeContext;
        private bool _pdaSignalLatched;
        private bool _hasCachedFrustumState;
        private bool _visibilityUploadRequested;
        private bool _originShiftUploadRequested;
        private bool _pendingWreckSignalPing;
        private float3 _pendingWreckSignalOrigin;
        private float _pendingWreckSignalRadius;

        public int SkippedVisibilityUploadCount => _skippedVisibilityUploadCount;

        private static void IncrementCounterSaturated(ref int counter)
        {
            if (counter < int.MaxValue)
                counter++;
        }

        private void Awake()
        {
            CacheRegistryServicesCold();
            ResolveIndirectShader();
            EnsureBatches();
            EnsureFrustumScratch();
        }

        private void OnEnable()
        {
            CacheRegistryServicesCold();
            ResolveIndirectShader();
            EnsureBatches();
            EnsureFrustumScratch();
            TryRegisterHotSwapListener();
            RefreshRuntimeTickRegistration();
        }

        private void OnDisable()
        {
            TryUnregisterSlowTick();
            TryUnregisterLateFrameTick();
            TryUnregisterOriginShiftListener();
            TryUnregisterHotSwapListener();
            DisposeBatches();
            ClearCachedRegistryServices();
        }

        private void OnDestroy()
        {
            TryUnregisterSlowTick();
            TryUnregisterLateFrameTick();
            TryUnregisterOriginShiftListener();
            TryUnregisterHotSwapListener();
            DisposeBatches();
            ClearCachedRegistryServices();
        }

        public void SlowTick()
        {
            if (!_hasPublishedWreck)
            {
                _visibilityUploadRequested = false;
                _originShiftUploadRequested = false;
                return;
            }

            _visibilityUploadRequested = true;
            if (!TryResolvePlayerAup(out AbsoluteUniversePosition playerAup))
                return;

            double distanceSq = AbsoluteUniversePosition.DistanceSq(in playerAup, in _publishedWreckCenterAup);
            float signalRadius = math.max(1f, pdaSignalRadiusMeters);
            double signalRadiusSq = (double)signalRadius * signalRadius;
            if (distanceSq <= signalRadiusSq)
            {
                if (_pdaSignalLatched)
                    return;

                _pdaSignalLatched = true;
                Vector3 pingCenter = _publishedWorldBounds.center;
                _pendingWreckSignalOrigin = new float3(pingCenter.x, pingCenter.y, pingCenter.z);
                _pendingWreckSignalRadius = math.max(1f, pdaSignalPingRadiusMeters);
                _pendingWreckSignalPing = true;
                return;
            }

            float rearmRadius = math.max(signalRadius, pdaSignalRearmRadiusMeters);
            double rearmRadiusSq = (double)rearmRadius * rearmRadius;
            if (distanceSq >= rearmRadiusSq)
                _pdaSignalLatched = false;
        }

        public void LateFrameTick()
        {
            if (_pendingWreckSignalPing)
            {
                _pendingWreckSignalPing = false;
                ScanEvents.TryRaiseWreckSignalPing(_pendingWreckSignalOrigin, _pendingWreckSignalRadius);
            }

            if (_visibilityUploadRequested)
            {
                _visibilityUploadRequested = false;
                RefreshVisibilityUploads();
            }

            if (_originShiftUploadRequested)
                FlushOriginShiftUploads();
        }

        private bool TryResolvePlayerAup(out AbsoluteUniversePosition playerAup)
        {
            IPlayerRuntimeContext runtimeContext = _playerRuntimeContext;
            if (runtimeContext != null)
            {
                HectonPlayerMovement playerMovement = runtimeContext.PlayerMovement;
                if (playerMovement != null)
                {
                    playerAup = playerMovement.CurrentAup;
                    return playerAup.IsFinite();
                }

                if (runtimeContext.TryGetMovementRuntimeState(out PlayerMovementRuntimeState movementState) &&
                    (movementState.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) != 0u)
                {
                    playerAup = movementState.PredictedAup;
                    return playerAup.IsFinite();
                }
            }

            playerAup = default;
            return false;
        }

        private static void PublishBrgUploadWarningIfNeeded(long uploadStartTimestamp)
        {
            if (!Application.isPlaying || uploadStartTimestamp <= 0L)
                return;

            long elapsedTicks = global::System.Diagnostics.Stopwatch.GetTimestamp() - uploadStartTimestamp;
            double elapsedMilliseconds = elapsedTicks * 1000.0 / global::System.Diagnostics.Stopwatch.Frequency;
            if (elapsedMilliseconds < BrgUploadTelemetryThresholdMs)
                return;

            GlobalTelemetryBus.PublishPerformanceWarning(
                WreckBrgUploadWarningHash,
                WreckBrgContextHash,
                (float)elapsedMilliseconds);
        }

        public void OnOriginShift(in OriginShiftEventData shiftData)
        {
            if (!_hasPublishedWreck || !_HasUsableShift(shiftData.ShiftOffset))
                return;

            Vector3 runtimeOffset = -shiftData.ShiftOffset;
            bool hasPendingMatrixUpload = false;
            if (_moduleBatches == null)
            {
                _publishedWorldBounds.center += runtimeOffset;
                _hasPublishedWreck = false;
                _originShiftUploadRequested = false;
                _hasCachedFrustumState = false;
                RefreshRuntimeTickRegistration();
                return;
            }

            for (int i = 0; i < _moduleBatches.Length; i++)
            {
                ModuleBatch batch = _moduleBatches[i];
                if (batch == null)
                    continue;

                batch.ApplyOriginShift(runtimeOffset);
                hasPendingMatrixUpload |= batch.HasPendingMatrixUpload;
            }

            _publishedWorldBounds.center += runtimeOffset;
            _originShiftUploadRequested = hasPendingMatrixUpload;
            _hasCachedFrustumState = false;
            RefreshRuntimeTickRegistration();
        }

        private void FlushOriginShiftUploads()
        {
            if (!_originShiftUploadRequested)
                return;

            if (_moduleBatches == null)
            {
                _originShiftUploadRequested = false;
                return;
            }

            bool hasPendingMatrixUpload = false;
            for (int i = 0; i < _moduleBatches.Length; i++)
            {
                ModuleBatch batch = _moduleBatches[i];
                if (batch == null || !batch.HasPendingMatrixUpload)
                    continue;

                if (!batch.FlushPendingMatrixUpload())
                {
                    hasPendingMatrixUpload = true;
                    IncrementCounterSaturated(ref _skippedVisibilityUploadCount);
                }
            }

            _originShiftUploadRequested = hasPendingMatrixUpload;
        }

        private void RefreshVisibilityUploads()
        {
            if (_moduleBatches == null)
                return;

            bool hasFrustum = TryPopulateFrustumPlanes(out bool frustumChanged);
            bool hasPendingCull = HasPendingVisibilityCull();
            if (!hasFrustum || (!frustumChanged && !hasPendingCull))
                return;

            for (int i = 0; i < _moduleBatches.Length; i++)
            {
                ModuleBatch batch = _moduleBatches[i];
                if (batch != null && batch.HasContent)
                {
                    bool published = batch.Publish(
                        _publishedWorldBounds,
                        _frustumPlanes,
                        enableFrustumCulling: true,
                        frustumVersion: _frustumStateVersion,
                        forceCullCompletion: false);
                    if (!published && batch.HasPendingVisibilityCull)
                        IncrementCounterSaturated(ref _skippedVisibilityUploadCount);
                }
            }
        }

        private bool HasPendingVisibilityCull()
        {
            if (_moduleBatches == null)
                return false;

            for (int i = 0; i < _moduleBatches.Length; i++)
            {
                ModuleBatch batch = _moduleBatches[i];
                if (batch != null && batch.HasPendingVisibilityCull)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Publishes one wreck instance stream from a managed snapshot copied after Vault render locks were released.
        /// </summary>
        public void Publish(
            ProceduralWreckModuleDefinition[] moduleDefinitions,
            Matrix4x4[] worldMatrices,
            byte[] moduleIds,
            int instanceCount,
            Bounds worldBounds,
            AbsoluteUniversePosition wreckCenterAup)
        {
            Publish(
                moduleDefinitions,
                worldMatrices,
                moduleIds,
                null,
                instanceCount,
                worldBounds,
                wreckCenterAup);
        }

        /// <summary>
        /// Publishes one wreck instance stream plus per-instance age metadata from a managed snapshot.
        /// </summary>
        public void Publish(
            ProceduralWreckModuleDefinition[] moduleDefinitions,
            Matrix4x4[] worldMatrices,
            byte[] moduleIds,
            float[] ages,
            int instanceCount,
            Bounds worldBounds,
            AbsoluteUniversePosition wreckCenterAup)
        {
            EnsureBatches();
            ResetAllBatches();
            _publishedWorldBounds = worldBounds;
            _hasPublishedWreck = false;
            _pdaSignalLatched = false;
            _hasCachedFrustumState = false;
            _visibilityUploadRequested = false;
            _originShiftUploadRequested = false;
            _pendingWreckSignalPing = false;
            bool hasFiniteWorldBounds = IsFiniteBounds(worldBounds);
            _publishedWreckCenterAup = default;

            if (worldMatrices == null || moduleIds == null || instanceCount <= 0)
            {
                RefreshRuntimeTickRegistration();
                return;
            }

            int moduleDefinitionCount = math.min(
                math.min(moduleDefinitions != null ? moduleDefinitions.Length : 0, MaxModuleContracts),
                _moduleBatches.Length);
            int safeCount = math.min(instanceCount, math.min(worldMatrices.Length, moduleIds.Length));
            int activeModuleMask = ResolveActiveModuleMask(moduleIds, safeCount, moduleDefinitionCount);

            if (forceSingleDrawBatch)
            {
                int singleModuleIndex = ResolveSingleDrawModuleIndex(moduleDefinitions, moduleDefinitionCount, activeModuleMask);
                if (singleModuleIndex >= 0)
                {
                    ModuleBatch singleBatch = _moduleBatches[singleModuleIndex];
                    if (TryConfigureBatch(singleBatch, moduleDefinitions, singleModuleIndex, math.max(1, safeCount)))
                    {
                        for (int instanceIndex = 0; instanceIndex < safeCount; instanceIndex++)
                        {
                            int moduleIndex = moduleIds[instanceIndex];
                            if (moduleIndex < 0 ||
                                moduleIndex >= moduleDefinitionCount ||
                                (activeModuleMask & (1 << moduleIndex)) == 0)
                            {
                                continue;
                            }

                            float age01 = ages != null && instanceIndex < ages.Length
                                ? ages[instanceIndex]
                                : 0.5f;
                            singleBatch.AddInstance(worldMatrices[instanceIndex], age01);
                        }

                        if (singleBatch.HasContent)
                        {
                            _hasPublishedWreck = hasFiniteWorldBounds;
                            _publishedWreckCenterAup = _hasPublishedWreck
                                ? wreckCenterAup
                                : default;
                            singleBatch.PrepareUploadResources();
                            bool hasFrustum = TryPopulateFrustumPlanes(out _);
                            if (hasFrustum)
                                singleBatch.Publish(
                                    worldBounds,
                                    _frustumPlanes,
                                    enableFrustumCulling: true,
                                    frustumVersion: _frustumStateVersion);
                        }
                        RefreshRuntimeTickRegistration();
                        return;
                    }
                }
            }

            if (HasDuplicateMaterialBufferBindings(moduleDefinitions, moduleDefinitionCount, activeModuleMask))
            {
                RefreshRuntimeTickRegistration();
                return;
            }

            int configuredBatchMask = 0;
            for (int instanceIndex = 0; instanceIndex < safeCount; instanceIndex++)
            {
                int moduleIndex = moduleIds[instanceIndex];
                if (moduleIndex < 0 || moduleIndex >= moduleDefinitionCount)
                    continue;

                ModuleBatch batch = _moduleBatches[moduleIndex];
                if (batch == null)
                    continue;

                int batchMask = 1 << moduleIndex;
                if ((configuredBatchMask & batchMask) == 0)
                {
                    if (!TryConfigureBatch(batch, moduleDefinitions, moduleIndex, math.max(1, instanceCount)))
                        continue;

                    configuredBatchMask |= batchMask;
                }

                float age01 = ages != null && instanceIndex < ages.Length
                    ? ages[instanceIndex]
                    : 0.5f;
                batch.AddInstance(worldMatrices[instanceIndex], age01);
            }

            bool hasAnyBatchContent = false;
            for (int moduleIndex = 0; moduleIndex < moduleDefinitionCount; moduleIndex++)
            {
                ModuleBatch batch = _moduleBatches[moduleIndex];
                if (batch == null || !batch.HasContent)
                    continue;

                hasAnyBatchContent = true;
            }

            _hasPublishedWreck = hasAnyBatchContent && hasFiniteWorldBounds;
            _publishedWreckCenterAup = _hasPublishedWreck
                ? wreckCenterAup
                : default;
            if (!hasAnyBatchContent)
            {
                RefreshRuntimeTickRegistration();
                return;
            }

            PrepareUploadResourcesForContent(moduleDefinitionCount);
            bool hasFrustumForPublish = TryPopulateFrustumPlanes(out _);
            if (!hasFrustumForPublish)
            {
                RefreshRuntimeTickRegistration();
                return;
            }

            for (int moduleIndex = 0; moduleIndex < moduleDefinitionCount; moduleIndex++)
            {
                ModuleBatch batch = _moduleBatches[moduleIndex];
                if (batch != null && batch.HasContent)
                    batch.Publish(
                        worldBounds,
                        _frustumPlanes,
                        enableFrustumCulling: true,
                        frustumVersion: _frustumStateVersion);
            }
            RefreshRuntimeTickRegistration();
        }

        private void PrepareUploadResourcesForContent(int moduleDefinitionCount)
        {
            if (_moduleBatches == null || moduleDefinitionCount <= 0)
                return;

            int safeCount = math.min(moduleDefinitionCount, _moduleBatches.Length);
            for (int moduleIndex = 0; moduleIndex < safeCount; moduleIndex++)
            {
                ModuleBatch batch = _moduleBatches[moduleIndex];
                if (batch != null && batch.HasContent)
                    batch.PrepareUploadResources();
            }
        }

        private bool TryPopulateFrustumPlanes(out bool frustumChanged)
        {
            frustumChanged = false;
            Camera cullCamera = ResolveViewCamera();
            if (cullCamera == null || !cullCamera.isActiveAndEnabled)
            {
                _hasCachedFrustumState = false;
                return false;
            }

            if (!HasFrustumScratchReady())
            {
                _hasCachedFrustumState = false;
                return false;
            }

            Transform cameraTransform = cullCamera.transform;
            Vector3 cameraPosition = cameraTransform.position;
            Quaternion cameraRotation = cameraTransform.rotation;
            Matrix4x4 projectionMatrix = cullCamera.projectionMatrix;
            float4 projectionSignature0 = new float4(
                projectionMatrix.m00,
                projectionMatrix.m02,
                projectionMatrix.m11,
                projectionMatrix.m12);
            float4 projectionSignature1 = new float4(
                projectionMatrix.m22,
                projectionMatrix.m23,
                projectionMatrix.m32,
                projectionMatrix.m33);
            int pixelWidth = cullCamera.pixelWidth;
            int pixelHeight = cullCamera.pixelHeight;
            if (_hasCachedFrustumState &&
                (cameraPosition - _lastFrustumCameraPosition).sqrMagnitude <= FrustumRefreshPositionEpsilonSq &&
                math.abs(Quaternion.Dot(cameraRotation, _lastFrustumCameraRotation)) >= FrustumRefreshRotationDotThreshold &&
                ProjectionSignaturesApproximatelyEqual(
                    projectionSignature0,
                    projectionSignature1,
                    _lastFrustumProjectionSignature0,
                    _lastFrustumProjectionSignature1) &&
                pixelWidth == _lastFrustumPixelWidth &&
                pixelHeight == _lastFrustumPixelHeight)
            {
                return true;
            }

            GeometryUtility.CalculateFrustumPlanes(cullCamera, _frustumPlaneCache);
            for (int i = 0; i < FrustumPlaneCount; i++)
            {
                Plane plane = _frustumPlaneCache[i];
                Vector3 normal = plane.normal;
                _frustumPlanes[i] = new float4(normal.x, normal.y, normal.z, plane.distance);
            }

            _lastFrustumCameraPosition = cameraPosition;
            _lastFrustumCameraRotation = cameraRotation;
            _lastFrustumProjectionSignature0 = projectionSignature0;
            _lastFrustumProjectionSignature1 = projectionSignature1;
            _lastFrustumPixelWidth = pixelWidth;
            _lastFrustumPixelHeight = pixelHeight;
            _hasCachedFrustumState = true;
            _frustumStateVersion++;
            frustumChanged = true;
            return true;
        }

        private static bool ProjectionSignaturesApproximatelyEqual(
            float4 current0,
            float4 current1,
            float4 previous0,
            float4 previous1)
        {
            float deltaSq = math.lengthsq(current0 - previous0) + math.lengthsq(current1 - previous1);
            return deltaSq <= FrustumRefreshProjectionEpsilonSq;
        }

        private Camera ResolveViewCamera()
        {
            if (_viewCamera != null && _viewCamera.isActiveAndEnabled)
                return _viewCamera;

            IPlayerRuntimeContext runtimeContext = _playerRuntimeContext;
            if (runtimeContext != null)
            {
                Camera playerCamera = runtimeContext.PlayerCamera;
                if (playerCamera != null)
                {
                    _viewCamera = playerCamera;
                    return _viewCamera;
                }

                Transform playerTransform = runtimeContext.PlayerTransform;
                if (playerTransform != null)
                    _playerTransform = playerTransform;
            }

            return _viewCamera;
        }

        private int ResolveSingleDrawModuleIndex(
            ProceduralWreckModuleDefinition[] moduleDefinitions,
            int moduleDefinitionCount,
            int activeModuleMask)
        {
            if (moduleDefinitions == null || moduleDefinitionCount <= 0)
                return -1;

            int preferredIndex = math.clamp(singleDrawModuleIndex, 0, moduleDefinitionCount - 1);
            if ((activeModuleMask & (1 << preferredIndex)) != 0 &&
                CanUseModuleContract(moduleDefinitions, preferredIndex))
            {
                return preferredIndex;
            }

            for (int moduleIndex = 0; moduleIndex < moduleDefinitionCount; moduleIndex++)
            {
                if ((activeModuleMask & (1 << moduleIndex)) != 0 &&
                    CanUseModuleContract(moduleDefinitions, moduleIndex))
                {
                    return moduleIndex;
                }
            }

            return -1;
        }

        private bool CanUseModuleContract(ProceduralWreckModuleDefinition[] moduleDefinitions, int moduleIndex)
        {
            if (moduleDefinitions == null || moduleIndex < 0 || moduleIndex >= moduleDefinitions.Length)
                return false;

            ProceduralWreckModuleDefinition definition = moduleDefinitions[moduleIndex];
            if (!definition.EmitsGeometry)
                return false;

            Mesh mesh = moduleContracts != null &&
                        moduleIndex < moduleContracts.Length &&
                        moduleContracts[moduleIndex].MeshOverride != null
                ? moduleContracts[moduleIndex].MeshOverride
                : definition.StructuralMesh;
            Material material = ResolveMaterialForModule(moduleIndex, definition.DrawCallPriority);
            return mesh != null && ResolveSharedIndirectMaterial(material) != null;
        }

        private bool HasDuplicateMaterialBufferBindings(
            ProceduralWreckModuleDefinition[] moduleDefinitions,
            int moduleDefinitionCount,
            int activeModuleMask)
        {
            if (moduleDefinitions == null || moduleDefinitionCount <= 1 || activeModuleMask == 0)
                return false;

            int safeCount = math.min(math.min(moduleDefinitionCount, moduleDefinitions.Length), MaxModuleContracts);
            for (int i = 0; i < safeCount; i++)
            {
                if ((activeModuleMask & (1 << i)) == 0)
                    continue;

                ProceduralWreckModuleDefinition definition = moduleDefinitions[i];
                if (!definition.EmitsGeometry)
                    continue;

                Material material = ResolveSharedIndirectMaterial(ResolveMaterialForModule(i, definition.DrawCallPriority));
                if (material == null)
                    continue;

                for (int j = i + 1; j < safeCount; j++)
                {
                    if ((activeModuleMask & (1 << j)) == 0)
                        continue;

                    ProceduralWreckModuleDefinition otherDefinition = moduleDefinitions[j];
                    if (!otherDefinition.EmitsGeometry)
                        continue;

                    Material otherMaterial = ResolveSharedIndirectMaterial(ResolveMaterialForModule(j, otherDefinition.DrawCallPriority));
                    if (object.ReferenceEquals(material, otherMaterial))
                        return true;
                }
            }

            return false;
        }

        private static int ResolveActiveModuleMask(byte[] moduleIds, int instanceCount, int moduleDefinitionCount)
        {
            if (moduleIds == null || instanceCount <= 0 || moduleDefinitionCount <= 0)
                return 0;

            int mask = 0;
            int safeCount = math.min(instanceCount, moduleIds.Length);
            int safeModuleCount = math.min(moduleDefinitionCount, MaxModuleContracts);
            for (int i = 0; i < safeCount; i++)
            {
                int moduleIndex = moduleIds[i];
                if (moduleIndex >= 0 && moduleIndex < safeModuleCount)
                    mask |= 1 << moduleIndex;
            }

            return mask;
        }

        private bool TryConfigureBatch(
            ModuleBatch batch,
            ProceduralWreckModuleDefinition[] moduleDefinitions,
            int moduleIndex,
            int capacity)
        {
            if (batch == null ||
                moduleDefinitions == null ||
                moduleIndex < 0 ||
                moduleIndex >= moduleDefinitions.Length)
            {
                return false;
            }

            ProceduralWreckModuleDefinition definition = moduleDefinitions[moduleIndex];
            if (!definition.EmitsGeometry)
                return false;

            Mesh mesh = moduleContracts != null &&
                        moduleIndex < moduleContracts.Length &&
                        moduleContracts[moduleIndex].MeshOverride != null
                ? moduleContracts[moduleIndex].MeshOverride
                : definition.StructuralMesh;
            Material material = ResolveSharedIndirectMaterial(ResolveMaterialForModule(moduleIndex, definition.DrawCallPriority));
            if (mesh == null || material == null)
                return false;

            ModuleRenderContract contract = moduleContracts != null && moduleIndex < moduleContracts.Length
                ? moduleContracts[moduleIndex]
                : default;

            batch.EnsureCapacity(math.max(1, capacity));
            batch.Configure(
                mesh,
                material,
                contract.SubMeshIndex,
                contract.ShadowCastingMode,
                contract.ReceiveShadows,
                gameObject.layer);
            return true;
        }

        private int ResolveMaxInstancesPerWreckBatch(int requestedCapacity)
        {
            int authoredCapacity = math.max(1, maxInstancesPerWreckBatch);
            int payloadCapacity = math.max(1, requestedCapacity);
            return math.min(authoredCapacity, payloadCapacity);
        }

        private IDataVault GetCachedDataVault()
        {
            return _dataVault;
        }

        private bool CanAttemptBatchMetadataAcquire()
        {
            IDataVault vault = GetCachedDataVault();
            return vault != null && !vault.IsCompactionFenceActive;
        }

        private bool EnsureBatchMetadataBuffer()
        {
            IDataVault vault = GetCachedDataVault();
            if (vault == null || vault.IsCompactionFenceActive)
                return false;

            uint expectedBufferId = unchecked((uint)(int)WreckBrgBatchMetadataBufferId);
            if (_batchMetadataHandle.BufferID == expectedBufferId &&
                vault.TryGetGenerationHandle<MetadataValue>(WreckBrgBatchMetadataBufferId, out VaultGenerationHandle<MetadataValue> existingHandle) &&
                existingHandle.BufferID == expectedBufferId)
            {
                _batchMetadataHandle = existingHandle;
                return !vault.IsCompactionFenceActive;
            }

            _batchMetadataHandle = vault.EnsureGenerationHandle<MetadataValue>(
                WreckBrgBatchMetadataBufferId,
                WreckBrgMetadataCount,
                WreckBrgVaultOwner,
                NativeArrayOptions.ClearMemory);

            return _batchMetadataHandle.BufferID == expectedBufferId && !vault.IsCompactionFenceActive;
        }

        private bool TryAcquireBatchMetadata(out NativeArray<MetadataValue> batchMetadata)
        {
            batchMetadata = default;
            IDataVault vault = GetCachedDataVault();
            if (vault == null ||
                vault.IsCompactionFenceActive ||
                _batchMetadataWriteVault != null ||
                !EnsureBatchMetadataBuffer() ||
                vault.IsCompactionFenceActive)
                return false;

            if (!vault.TryAcquireWriteLock(in _batchMetadataHandle, WreckBrgVaultOwner, out batchMetadata))
                return false;

            bool keepLock = false;
            try
            {
                if (!vault.IsCompactionFenceActive &&
                    batchMetadata.IsCreated &&
                    batchMetadata.Length >= WreckBrgMetadataCount)
                {
                    _batchMetadataWriteVault = vault;
                    keepLock = true;
                    return true;
                }

                batchMetadata = default;
                return false;
            }
            finally
            {
                if (!keepLock)
                    vault.ReleaseWriteLock(in _batchMetadataHandle, WreckBrgVaultOwner);
            }
        }

        private bool TryWriteBatchMetadata(out MetadataValue batchMetadataValue)
        {
            batchMetadataValue = new MetadataValue
            {
                NameID = _WreckAgesId,
                Value = 0u
            };

            if (!TryAcquireBatchMetadata(out NativeArray<MetadataValue> batchMetadata))
                return false;

            try
            {
                batchMetadata[0] = batchMetadataValue;
                return true;
            }
            finally
            {
                ReleaseBatchMetadataWriteLock();
            }
        }

        private void ReleaseBatchMetadataWriteLock()
        {
            IDataVault vault = _batchMetadataWriteVault;
            _batchMetadataWriteVault = null;
            if (vault == null || _batchMetadataHandle.BufferID == 0u)
                return;

            vault.ReleaseWriteLock(in _batchMetadataHandle, WreckBrgVaultOwner);
        }

        private void ReleaseBatchMetadataBuffer()
        {
            ReleaseBatchMetadataWriteLock();
            IDataVault vault = _dataVault;
            if (vault != null && _batchMetadataHandle.BufferID != 0u)
                vault.ReleaseBuffer(in _batchMetadataHandle);

            _batchMetadataHandle = default;
        }

        private void TryRegisterSlowTick()
        {
            if (_registeredSlowTick)
                return;

            if (!HasSlowTickWork() || !Application.isPlaying || !_hasRuntimeDispatcher)
                return;

            _registeredSlowTick = GlobalRegistry.TryRegisterSlowTickable(this, PriorityLayer.Environment);
        }

        private void TryRegisterLateFrameTick()
        {
            if (_registeredLateFrameTick)
                return;

            if (!HasRuntimeDispatcherWork() || !Application.isPlaying || !_hasRuntimeDispatcher)
                return;

            _registeredLateFrameTick = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Environment);
        }

        private void TryUnregisterSlowTick()
        {
            if (!_registeredSlowTick)
                return;

            GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Environment);
            _registeredSlowTick = false;
        }

        private void TryUnregisterLateFrameTick()
        {
            if (!_registeredLateFrameTick)
                return;

            GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Environment);
            _registeredLateFrameTick = false;
        }

        private bool HasSlowTickWork()
        {
            return _hasPublishedWreck;
        }

        private bool HasLateFrameTickWork()
        {
            return _visibilityUploadRequested || _originShiftUploadRequested || _pendingWreckSignalPing;
        }

        private bool HasRuntimeDispatcherWork()
        {
            return HasSlowTickWork() || HasLateFrameTickWork();
        }

        private void RefreshRuntimeTickRegistration()
        {
            if (HasRuntimeDispatcherWork())
            {
                TryRegisterSlowTick();
                TryRegisterLateFrameTick();
            }
            else
            {
                TryUnregisterSlowTick();
                TryUnregisterLateFrameTick();
            }

            RefreshOriginShiftRegistration();
        }

        private void RefreshOriginShiftRegistration()
        {
            if (_hasPublishedWreck)
            {
                TryRegisterOriginShiftListener();
                return;
            }

            TryUnregisterOriginShiftListener();
        }

        private void TryRegisterOriginShiftListener()
        {
            if (_originShiftListenerRegistered || !Application.isPlaying)
                return;

            HectonFloatingOrigin.RegisterListener(this);
            _originShiftListenerRegistered = true;
        }

        private void TryUnregisterOriginShiftListener()
        {
            if (!_originShiftListenerRegistered)
                return;

            HectonFloatingOrigin.UnregisterListener(this);
            _originShiftListenerRegistered = false;
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot == GlobalRegistryServiceSlot.Dispatcher)
            {
                TryUnregisterSlowTick();
                TryUnregisterLateFrameTick();
                _hasRuntimeDispatcher = currentService != null;
                if (currentService != null && isActiveAndEnabled)
                    RefreshRuntimeTickRegistration();
            }
            else if (serviceSlot == GlobalRegistryServiceSlot.DataVault)
            {
                DisposeBatches();
                _dataVault = currentService as IDataVault;
                if (currentService != null && isActiveAndEnabled)
                    EnsureBatches();
            }
            else if (serviceSlot == GlobalRegistryServiceSlot.Player)
            {
                _playerRuntimeContext = currentService as IPlayerRuntimeContext;
                _playerTransform = _playerRuntimeContext != null ? _playerRuntimeContext.PlayerTransform : null;
                _viewCamera = _playerRuntimeContext != null ? _playerRuntimeContext.PlayerCamera : null;
                _hasCachedFrustumState = false;
            }
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
            _hasRuntimeDispatcher = Application.isPlaying && GlobalRegistry.Dispatcher != null;

            if (Application.isPlaying && _dataVault == null)
                _dataVault = GlobalRegistry.DataVault;

            if (_playerRuntimeContext == null)
                _playerRuntimeContext = GlobalRegistry.Player;

            if (_playerRuntimeContext != null && _playerTransform == null)
                _playerTransform = _playerRuntimeContext.PlayerTransform;

            if (_playerRuntimeContext != null && _viewCamera == null)
                _viewCamera = _playerRuntimeContext.PlayerCamera;

            if (_playerTransform == null)
                WorldRuntimeReferenceUtility.TryResolvePlayerTransform(ref _playerTransform);

            if (_viewCamera == null && _playerTransform != null)
                _viewCamera = ComponentReferenceUtility.ResolveOwnedComponent<Camera>(_playerTransform);
        }

        private void ClearCachedRegistryServices()
        {
            _playerRuntimeContext = null;
            _playerTransform = null;
            _viewCamera = null;
            _hasRuntimeDispatcher = false;
            _hasCachedFrustumState = false;
        }

        private void EnsureBatches()
        {
            if (_moduleBatches != null && _moduleBatches.Length == MaxModuleContracts)
                return;

            _moduleBatches = new ModuleBatch[MaxModuleContracts]; // COLD ALLOC: ModuleBatch[16] - procedural wreck BRG owners by module slot - owner: WreckMaterialRegistry
            for (int i = 0; i < _moduleBatches.Length; i++)
                _moduleBatches[i] = new ModuleBatch(this, i);
        }

        private void EnsureFrustumScratch()
        {
            if (_frustumPlaneCache == null || _frustumPlaneCache.Length != FrustumPlaneCount)
                _frustumPlaneCache = new Plane[FrustumPlaneCount]; // COLD ALLOC: Plane[6] - player-camera wreck BRG upload culling planes - owner: WreckMaterialRegistry

            if (_frustumPlanes == null || _frustumPlanes.Length != FrustumPlaneCount)
                _frustumPlanes = new float4[FrustumPlaneCount]; // COLD ALLOC: float4[6] - managed camera-frustum snapshot copied into per-batch publish tests - owner: WreckMaterialRegistry
        }

        private bool HasFrustumScratchReady()
        {
            return _frustumPlaneCache != null &&
                   _frustumPlaneCache.Length == FrustumPlaneCount &&
                   _frustumPlanes != null &&
                   _frustumPlanes.Length == FrustumPlaneCount;
        }

        private void ResetAllBatches()
        {
            if (_moduleBatches == null)
                return;

            for (int i = 0; i < _moduleBatches.Length; i++)
                _moduleBatches[i]?.Reset();
        }

        private void DisposeBatches()
        {
            if (_moduleBatches == null)
            {
                _hasPublishedWreck = false;
                _publishedWreckCenterAup = default;
                _visibilityUploadRequested = false;
                _originShiftUploadRequested = false;
                _pendingWreckSignalPing = false;
                _pendingWreckSignalOrigin = default;
                _pendingWreckSignalRadius = 0f;
                _pdaSignalLatched = false;
                ReleaseBatchMetadataBuffer();
                RefreshRuntimeTickRegistration();
                return;
            }

            for (int i = 0; i < _moduleBatches.Length; i++)
                _moduleBatches[i]?.Dispose();

            _moduleBatches = null;
            _hasPublishedWreck = false;
            _publishedWreckCenterAup = default;
            _visibilityUploadRequested = false;
            _originShiftUploadRequested = false;
            _pendingWreckSignalPing = false;
            _pendingWreckSignalOrigin = default;
            _pendingWreckSignalRadius = 0f;
            _pdaSignalLatched = false;
            _hasCachedFrustumState = false;
            DisposeFrustumScratch();
            ReleaseBatchMetadataBuffer();
            RefreshRuntimeTickRegistration();
        }

        private void DisposeFrustumScratch()
        {
            _frustumPlanes = null;
        }

        private Material ResolveMaterialForModule(int moduleIndex, WreckLodTier tier)
        {
            Material sharedTierMaterial = ResolveSharedTierMaterial(tier);
            if (sharedTierMaterial != null)
                return sharedTierMaterial;

            if (moduleContracts != null &&
                moduleIndex >= 0 &&
                moduleIndex < moduleContracts.Length &&
                moduleContracts[moduleIndex].MaterialOverride != null)
            {
                return moduleContracts[moduleIndex].MaterialOverride;
            }

            return tier switch
            {
                WreckLodTier.Essential => essentialTierMaterial,
                WreckLodTier.Detail => detailTierMaterial,
                _ => clutterTierMaterial
            };
        }

        private Material ResolveSharedTierMaterial(WreckLodTier tier)
        {
            int index = ResolveSharedTierMaterialIndex(tier);
            if (wreckageTierSharedMaterials == null ||
                index < 0 ||
                index >= SharedWreckTierMaterialCount ||
                index >= wreckageTierSharedMaterials.Length)
            {
                return null;
            }

            return wreckageTierSharedMaterials[index];
        }

        private static int ResolveSharedTierMaterialIndex(WreckLodTier tier)
        {
            return tier == WreckLodTier.Essential ? 0 : 1;
        }

        private void ResolveIndirectShader()
        {
            if (indirectWreckShader == null)
                RuntimeShaderReferenceCatalog.TryGetWreckIndirectLitShader(out indirectWreckShader);
        }

        private Material ResolveSharedIndirectMaterial(Material sourceMaterial)
        {
            if (sourceMaterial == null)
                return null;

            Shader sourceShader = sourceMaterial.shader;
            if (sourceShader == null)
                return null;

            ResolveIndirectShader();
            if (sourceShader == indirectWreckShader || IsIndirectWreckShader(sourceShader))
                return sourceMaterial;

            return null;
        }

        private static bool IsIndirectWreckShader(Shader shader)
        {
            return shader != null &&
                   string.Equals(shader.name, IndirectWreckShaderName, System.StringComparison.Ordinal);
        }

        private static bool _HasUsableShift(Vector3 shiftOffset)
        {
            return shiftOffset.sqrMagnitude > 0.0001f;
        }

        private static bool IsFiniteBounds(Bounds bounds)
        {
            Vector3 center = bounds.center;
            Vector3 size = bounds.size;
            return math.all(math.isfinite(new float3(center.x, center.y, center.z))) &&
                   math.all(math.isfinite(new float3(size.x, size.y, size.z)));
        }
    }
}
