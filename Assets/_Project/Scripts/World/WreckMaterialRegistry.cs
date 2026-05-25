using Hecton8.Core;
using Hecton8.Gameplay;
using Unity.Burst;
using Unity.Burst.CompilerServices;
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
        private const int WreckBrgMetadataCount = 1;
        private const int FrustumPlaneCount = 6;
        private const int DefaultMaxInstancesPerWreckBatch = 2048;
        private const int InvalidFrustumVersion = -1;
        private const float FrustumRefreshPositionEpsilonSq = 0.25f;
        private const float FrustumRefreshRotationDotThreshold = 0.99995f;
        private const float FrustumRefreshProjectionEpsilonSq = 0.000001f;
        private const string IndirectWreckShaderName = "Hecton8/World/WreckIndirectLit";
        private const double BrgUploadTelemetryThresholdMs = 0.2d;
        private const uint WreckBrgUploadWarningHash = 0x5755504Cu; // WUPL
        private const uint WreckBrgContextHash = 0x57425247u; // WBRG
        private const Allocator DataVaultExemptRenderStagingAllocator = Allocator.Persistent;
        private const Allocator DataVaultExemptSceneScratchAllocator = Allocator.Persistent;
        private static readonly int _WreckMatricesId = Shader.PropertyToID("_HectonWreckMatrices");
        private static readonly int _WreckAgesId = Shader.PropertyToID("_HectonWreckAges");

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private struct WreckMatrixRebaseJob : IJobParallelFor
        {
            [NoAlias] public NativeArray<Matrix4x4> Matrices;
            public float3 RuntimeOffset;

            public unsafe void Execute(int index)
            {
                Matrix4x4* matrices = (Matrix4x4*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(Matrices);
                ref Matrix4x4 matrix = ref matrices[index];
                matrix.m03 += RuntimeOffset.x;
                matrix.m13 += RuntimeOffset.y;
                matrix.m23 += RuntimeOffset.z;
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private struct CullWreckMatricesToVisibleSubsetJob : IJob
        {
            [ReadOnly, NoAlias] public NativeArray<Matrix4x4> SourceMatrices;
            [ReadOnly, NoAlias] public NativeArray<float> SourceAges;
            [ReadOnly, NoAlias] public NativeArray<float4> FrustumPlanes;
            [NoAlias] public NativeList<Matrix4x4> VisibleMatrices;
            [NoAlias] public NativeList<float> VisibleAges;
            public float3 LocalBoundsCenter;
            public float3 LocalBoundsExtents;
            public int SourceCount;
            public int SourceAgeCount;
            public int OutputCapacity;
            public int PlaneCount;
            public int HasAgeData;
            public int EnableFrustumCulling;

            public void Execute()
            {
                VisibleMatrices.Clear();
                VisibleAges.Clear();

                int count = math.min(SourceCount, SourceMatrices.Length);
                if (count <= 0 || OutputCapacity <= 0)
                    return;

                for (int index = 0; index < count; index++)
                {
                    if (VisibleMatrices.Length >= OutputCapacity)
                        break;

                    Matrix4x4 matrix = SourceMatrices[index];
                    if (EnableFrustumCulling != 0 && !IsMatrixAabbVisible(in matrix))
                        continue;

                    VisibleMatrices.AddNoResize(matrix);
                    float age01 = HasAgeData != 0 && index < SourceAgeCount
                        ? SourceAges[index]
                        : 0.5f;
                    VisibleAges.AddNoResize(math.saturate(age01));
                }
            }

            private bool IsMatrixAabbVisible(in Matrix4x4 matrix)
            {
                float3 axisX = new float3(matrix.m00, matrix.m10, matrix.m20);
                float3 axisY = new float3(matrix.m01, matrix.m11, matrix.m21);
                float3 axisZ = new float3(matrix.m02, matrix.m12, matrix.m22);
                float3 translation = new float3(matrix.m03, matrix.m13, matrix.m23);
                float3 center = translation + axisX * LocalBoundsCenter.x + axisY * LocalBoundsCenter.y + axisZ * LocalBoundsCenter.z;
                float3 extents =
                    math.abs(axisX) * LocalBoundsExtents.x +
                    math.abs(axisY) * LocalBoundsExtents.y +
                    math.abs(axisZ) * LocalBoundsExtents.z;

                for (int planeIndex = 0; planeIndex < PlaneCount; planeIndex++)
                {
                    float4 plane = FrustumPlanes[planeIndex];
                    float projectionRadius =
                        math.abs(plane.x) * extents.x +
                        math.abs(plane.y) * extents.y +
                        math.abs(plane.z) * extents.z;
                    if (math.dot(plane.xyz, center) + plane.w + projectionRadius < 0f)
                        return false;
                }

                return true;
            }
        }

        [System.Serializable]
#pragma warning disable 0649 // Unity serializes module render contracts from authored registry data.
        private struct ModuleRenderContract
        {
            [Tooltip("Optional mesh override. When empty, the generator structural mesh for the same module slot is used.")]
            public Mesh MeshOverride;

            [Tooltip("Optional material override. When empty, the tier fallback material is used.")]
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
            private readonly string _matrixSentinelLabel;
            private readonly string _ageSentinelLabel;
            private readonly string _visibleMatrixSentinelLabel;
            private readonly string _visibleAgeSentinelLabel;
            private readonly string _frustumPlaneSentinelLabel;
            private readonly string _metadataSentinelLabel;
            private BatchRendererGroup _batchRendererGroup;
            private NativeArray<MetadataValue> _batchMetadata;
            private GraphicsBuffer _batchHandleBuffer;
            private BatchID _batchId;
            private BatchMeshID _batchMeshId;
            private BatchMaterialID _batchMaterialId;
            private Mesh _registeredMesh;
            private Material _registeredMaterial;
            private GraphicsBuffer _registeredBatchBuffer;
            private Material _runtimeMaterial;
            private Material _materialSource;
            private Material _runtimeMaterialSource;
            private Shader _runtimeShader;
            private GraphicsBuffer _matrixBufferA;
            private GraphicsBuffer _matrixBufferB;
            private GraphicsBuffer _activeMatrixBuffer;
            private GraphicsBuffer _ageBufferA;
            private GraphicsBuffer _ageBufferB;
            private GraphicsBuffer _activeAgeBuffer;
            private int _uploadBufferIndex;
            private NativeList<Matrix4x4> _matrices;
            private NativeList<float> _ages;
            private NativeList<Matrix4x4> _visibleMatrices;
            private NativeList<float> _visibleAges;
            private NativeArray<float4> _frustumPlaneSnapshot;
            private JobHandle _visibilityCullHandle;
            private int _pendingVisibilityFrustumVersion;
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
            private bool _ownsRuntimeMaterial;
            private bool _visibilityCullPending;

            public ModuleBatch(WreckMaterialRegistry owner, int moduleIndex)
            {
                _owner = owner;
                _moduleIndex = moduleIndex;
                _matrixSentinelLabel = string.Concat(nameof(_matrices), "_", moduleIndex);
                _ageSentinelLabel = string.Concat(nameof(_ages), "_", moduleIndex);
                _visibleMatrixSentinelLabel = string.Concat(nameof(_visibleMatrices), "_", moduleIndex);
                _visibleAgeSentinelLabel = string.Concat(nameof(_visibleAges), "_", moduleIndex);
                _frustumPlaneSentinelLabel = string.Concat(nameof(_frustumPlaneSnapshot), "_", moduleIndex);
                _metadataSentinelLabel = string.Concat(nameof(_batchMetadata), "_", moduleIndex);
                _pendingVisibilityFrustumVersion = InvalidFrustumVersion;
                _visibleSubsetFrustumVersion = InvalidFrustumVersion;
            }

            public bool HasContent => _matrices.IsCreated && _matrices.Length > 0;

            public bool HasPendingVisibilityCull => _visibilityCullPending;

            public void EnsureCapacity(int minimumCapacity)
            {
                _ = minimumCapacity;
                int fixedCapacity = _owner.ResolveMaxInstancesPerWreckBatch();
                if (!_matrices.IsCreated)
                {
                    _matrices = new NativeList<Matrix4x4>(fixedCapacity, DataVaultExemptRenderStagingAllocator); // COLD ALLOC: NativeList<Matrix4x4>[maxInstancesPerWreckBatch] - BRG wreck module matrix staging - owner: WreckMaterialRegistry
                    _ages = new NativeList<float>(fixedCapacity, DataVaultExemptRenderStagingAllocator); // COLD ALLOC: NativeList<float>[maxInstancesPerWreckBatch] - BRG wreck module age metadata staging - owner: WreckMaterialRegistry
                    _visibleMatrices = new NativeList<Matrix4x4>(fixedCapacity, DataVaultExemptRenderStagingAllocator); // COLD ALLOC: NativeList<Matrix4x4>[maxInstancesPerWreckBatch] - BRG visible wreck matrix upload subset - owner: WreckMaterialRegistry
                    _visibleAges = new NativeList<float>(fixedCapacity, DataVaultExemptRenderStagingAllocator); // COLD ALLOC: NativeList<float>[maxInstancesPerWreckBatch] - BRG visible wreck age upload subset - owner: WreckMaterialRegistry
                    _frustumPlaneSnapshot = new NativeArray<float4>(FrustumPlaneCount, DataVaultExemptSceneScratchAllocator, NativeArrayOptions.UninitializedMemory); // COLD ALLOC: NativeArray<float4>[6] - per-module immutable BRG cull plane snapshot - owner: WreckMaterialRegistry
                    NativeMemorySentinel.RegisterNativeList(_matrices, nameof(WreckMaterialRegistry), _matrixSentinelLabel, NativeAllocationLifetime.Scene);
                    NativeMemorySentinel.RegisterNativeList(_ages, nameof(WreckMaterialRegistry), _ageSentinelLabel, NativeAllocationLifetime.Scene);
                    NativeMemorySentinel.RegisterNativeList(_visibleMatrices, nameof(WreckMaterialRegistry), _visibleMatrixSentinelLabel, NativeAllocationLifetime.Scene);
                    NativeMemorySentinel.RegisterNativeList(_visibleAges, nameof(WreckMaterialRegistry), _visibleAgeSentinelLabel, NativeAllocationLifetime.Scene);
                    NativeMemorySentinel.RegisterNativeArray(_frustumPlaneSnapshot, nameof(WreckMaterialRegistry), _frustumPlaneSentinelLabel, NativeAllocationLifetime.Scene);
                    return;
                }

                if (!_ages.IsCreated)
                {
                    _ages = new NativeList<float>(fixedCapacity, DataVaultExemptRenderStagingAllocator); // COLD ALLOC: NativeList<float>[maxInstancesPerWreckBatch] - BRG wreck module age metadata staging - owner: WreckMaterialRegistry
                    NativeMemorySentinel.RegisterNativeList(_ages, nameof(WreckMaterialRegistry), _ageSentinelLabel, NativeAllocationLifetime.Scene);
                }

                if (!_visibleMatrices.IsCreated)
                {
                    _visibleMatrices = new NativeList<Matrix4x4>(fixedCapacity, DataVaultExemptRenderStagingAllocator); // COLD ALLOC: NativeList<Matrix4x4>[maxInstancesPerWreckBatch] - BRG visible wreck matrix upload subset - owner: WreckMaterialRegistry
                    NativeMemorySentinel.RegisterNativeList(_visibleMatrices, nameof(WreckMaterialRegistry), _visibleMatrixSentinelLabel, NativeAllocationLifetime.Scene);
                }

                if (!_visibleAges.IsCreated)
                {
                    _visibleAges = new NativeList<float>(fixedCapacity, DataVaultExemptRenderStagingAllocator); // COLD ALLOC: NativeList<float>[maxInstancesPerWreckBatch] - BRG visible wreck age upload subset - owner: WreckMaterialRegistry
                    NativeMemorySentinel.RegisterNativeList(_visibleAges, nameof(WreckMaterialRegistry), _visibleAgeSentinelLabel, NativeAllocationLifetime.Scene);
                }

                if (!_frustumPlaneSnapshot.IsCreated)
                {
                    _frustumPlaneSnapshot = new NativeArray<float4>(FrustumPlaneCount, DataVaultExemptSceneScratchAllocator, NativeArrayOptions.UninitializedMemory); // COLD ALLOC: NativeArray<float4>[6] - per-module immutable BRG cull plane snapshot - owner: WreckMaterialRegistry
                    NativeMemorySentinel.RegisterNativeArray(_frustumPlaneSnapshot, nameof(WreckMaterialRegistry), _frustumPlaneSentinelLabel, NativeAllocationLifetime.Scene);
                }
            }

            public void Reset()
            {
                CompletePendingVisibilityCullForBarrier();
                if (_matrices.IsCreated)
                    _matrices.Clear();
                if (_ages.IsCreated)
                    _ages.Clear();
                if (_visibleMatrices.IsCreated)
                    _visibleMatrices.Clear();
                if (_visibleAges.IsCreated)
                    _visibleAges.Clear();
                _uploadedInstanceCount = 0;
                _pendingVisibilityFrustumVersion = InvalidFrustumVersion;
                _visibleSubsetFrustumVersion = InvalidFrustumVersion;
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
                _materialSource = material;
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
                if (!_matrices.IsCreated || !_ages.IsCreated)
                    return;

                if (_matrices.Length >= _matrices.Capacity || _ages.Length >= _ages.Capacity)
                    return;

                _matrices.AddNoResize(matrix);
                _ages.AddNoResize(math.saturate(age01));
            }

            public bool Publish(
                Bounds drawBounds,
                float4[] frustumPlanes,
                bool enableFrustumCulling,
                int frustumVersion,
                bool forceCullCompletion = true)
            {
                _drawBounds = drawBounds;
                if (_mesh == null || _materialSource == null || !_matrices.IsCreated || _matrices.Length <= 0)
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
                    return true;
                }

                EnsureResources();
                EnsureRuntimeMaterial();
                EnsureMatrixBufferCapacity(visibleCount);
                EnsureAgeBufferCapacity(visibleCount);
                if (_batchRendererGroup == null ||
                    _runtimeMaterial == null ||
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
                GraphicsBufferUploadUtility.UploadNativeArray(matrixWriteBuffer, _visibleMatrices.AsArray(), visibleCount);
                GraphicsBufferUploadUtility.UploadNativeArray(ageWriteBuffer, _visibleAges.AsArray(), visibleCount);
                _activeMatrixBuffer = matrixWriteBuffer;
                _activeAgeBuffer = ageWriteBuffer;
                _uploadBufferIndex ^= 1;
                _runtimeMaterial.SetBuffer(_WreckMatricesId, _activeMatrixBuffer);
                _runtimeMaterial.SetBuffer(_WreckAgesId, _activeAgeBuffer);
                SyncBatchBuffer(_activeMatrixBuffer);
                SyncBatchRegistration();
                _uploadedInstanceCount = visibleCount;
                _batchRendererGroup.SetGlobalBounds(_drawBounds);
                WreckMaterialRegistry.PublishBrgUploadWarningIfNeeded(uploadStartTimestamp);
                return true;
            }

            private bool TryCullVisibleSubset(
                float4[] frustumPlanes,
                bool enableFrustumCulling,
                int frustumVersion,
                bool forceCompletion,
                out int visibleCount)
            {
                visibleCount = 0;
                if (!_matrices.IsCreated || !_visibleMatrices.IsCreated || !_visibleAges.IsCreated)
                    return true;

                int count = _matrices.Length;
                if (count <= 0)
                    return true;

                int visibleCapacity = math.min(_visibleMatrices.Capacity, _visibleAges.Capacity);
                if (visibleCapacity <= 0)
                    return true;

                if (_visibilityCullPending)
                {
                    if (forceCompletion)
                    {
                        if (!CompletePendingVisibilityCullForBarrier())
                            return false;
                    }
                    else if (!TryFinalizePendingVisibilityCullNoWait())
                    {
                        return false;
                    }

                    if (_visibleSubsetFrustumVersion != frustumVersion)
                    {
                        ScheduleVisibilityCull(
                            frustumPlanes,
                            enableFrustumCulling,
                            frustumVersion,
                            count,
                            visibleCapacity);
                    }
                }
                else if (_visibleSubsetFrustumVersion != frustumVersion)
                {
                    ScheduleVisibilityCull(
                        frustumPlanes,
                        enableFrustumCulling,
                        frustumVersion,
                        count,
                        visibleCapacity);
                }

                if (forceCompletion)
                {
                    if (!CompletePendingVisibilityCullForBarrier())
                        return false;
                }
                else if (!TryFinalizePendingVisibilityCullNoWait())
                {
                    return false;
                }

                visibleCount = _visibleMatrices.Length;
                return true;
            }

            private void ScheduleVisibilityCull(
                float4[] frustumPlanes,
                bool enableFrustumCulling,
                int frustumVersion,
                int count,
                int visibleCapacity)
            {
                int ageCount = _ages.IsCreated ? _ages.Length : 0;
                bool hasPlanes = enableFrustumCulling && frustumPlanes != null && frustumPlanes.Length >= FrustumPlaneCount;
                NativeArray<float4> jobFrustumPlanes = default;
                if (hasPlanes && _frustumPlaneSnapshot.IsCreated)
                {
                    for (int planeIndex = 0; planeIndex < FrustumPlaneCount; planeIndex++)
                        _frustumPlaneSnapshot[planeIndex] = frustumPlanes[planeIndex];
                    jobFrustumPlanes = _frustumPlaneSnapshot;
                }
                else
                {
                    hasPlanes = false;
                }

                _visibilityCullHandle = new CullWreckMatricesToVisibleSubsetJob
                {
                    SourceMatrices = _matrices.AsArray(),
                    SourceAges = ageCount > 0 ? _ages.AsArray() : default,
                    FrustumPlanes = jobFrustumPlanes,
                    VisibleMatrices = _visibleMatrices,
                    VisibleAges = _visibleAges,
                    LocalBoundsCenter = _meshLocalBoundsCenter,
                    LocalBoundsExtents = _meshLocalBoundsExtents,
                    SourceCount = count,
                    SourceAgeCount = ageCount,
                    OutputCapacity = visibleCapacity,
                    PlaneCount = hasPlanes ? FrustumPlaneCount : 0,
                    HasAgeData = ageCount >= count ? 1 : 0,
                    EnableFrustumCulling = hasPlanes ? 1 : 0
                }.Schedule();
                _pendingVisibilityFrustumVersion = frustumVersion;
                _visibilityCullPending = true;
            }

            public void ApplyOriginShift(Vector3 runtimeOffset)
            {
                CompletePendingVisibilityCullForBarrier();
                if (_matrices.IsCreated)
                {
                    int count = _matrices.Length;
                    if (count > 0)
                        ApplyOriginShiftToMatrices(_matrices.AsArray(), count, runtimeOffset);
                }

                if (_visibleMatrices.IsCreated)
                {
                    int visibleCount = _visibleMatrices.Length;
                    if (visibleCount > 0)
                        ApplyOriginShiftToMatrices(_visibleMatrices.AsArray(), visibleCount, runtimeOffset);

                    int uploadCount = math.min(_uploadedInstanceCount, _visibleMatrices.Length);
                    if (uploadCount > 0)
                    {
                        EnsureMatrixBufferCapacity(uploadCount);
                        GraphicsBuffer matrixWriteBuffer = _uploadBufferIndex == 0 ? _matrixBufferA : _matrixBufferB;
                        if (matrixWriteBuffer == null)
                            return;

                        _uploadedInstanceCount = uploadCount;
                        GraphicsBufferUploadUtility.UploadNativeArray(matrixWriteBuffer, _visibleMatrices.AsArray(), uploadCount);
                        _activeMatrixBuffer = matrixWriteBuffer;
                        _uploadBufferIndex ^= 1;
                        if (_runtimeMaterial != null)
                            _runtimeMaterial.SetBuffer(_WreckMatricesId, _activeMatrixBuffer);
                        SyncBatchBuffer(_activeMatrixBuffer);
                    }
                }

                _drawBounds.center += runtimeOffset;
                if (_batchRendererGroup != null)
                    _batchRendererGroup.SetGlobalBounds(_drawBounds);
            }

            private static void ApplyOriginShiftToMatrices(NativeArray<Matrix4x4> matrices, int count, Vector3 runtimeOffset)
            {
                var job = new WreckMatrixRebaseJob
                {
                    Matrices = matrices,
                    RuntimeOffset = new float3(runtimeOffset.x, runtimeOffset.y, runtimeOffset.z)
                };
                JobHandle handle = job.Schedule(count, 64);
                // BLOCKING_SYNC_POINT: floating-origin rebase is an atomic world-shift phase, not Tick cadence.
                DispatcherJobSwap.TryComplete(ref handle, forceComplete: true);
            }

            public void Dispose()
            {
                JobHandle disposeHandle = Dispose(default);
                if (!disposeHandle.IsCompleted)
                    DispatcherJobSwap.TryComplete(ref disposeHandle, forceComplete: true);
            }

            public JobHandle Dispose(JobHandle dependency)
            {
                CompletePendingVisibilityCullForBarrier();
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

                if (_batchMetadata.IsCreated)
                {
                    NativeMemorySentinel.UnregisterNativeArray(_batchMetadata);
                    NativeArray<MetadataValue> metadata = _batchMetadata;
                    _batchMetadata = default;
                    dependency = metadata.Dispose(dependency);
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

                if (_ownsRuntimeMaterial && _runtimeMaterial != null)
                {
                    if (Application.isPlaying)
                        Object.Destroy(_runtimeMaterial);
                    else
                        Object.DestroyImmediate(_runtimeMaterial);
                }

                _runtimeMaterial = null;
                _materialSource = null;
                _runtimeMaterialSource = null;
                _runtimeShader = null;
                _registeredMaterial = null;
                _registeredMesh = null;
                _batchMeshId = default;
                _batchMaterialId = default;
                _batchId = default;
                _registeredBatchBuffer = null;
                _uploadedInstanceCount = 0;
                if (_matrices.IsCreated)
                {
                    NativeMemorySentinel.UnregisterNativeList(nameof(WreckMaterialRegistry), _matrixSentinelLabel);
                    NativeList<Matrix4x4> matrices = _matrices;
                    _matrices = default;
                    dependency = matrices.Dispose(dependency);
                }
                if (_ages.IsCreated)
                {
                    NativeMemorySentinel.UnregisterNativeList(nameof(WreckMaterialRegistry), _ageSentinelLabel);
                    NativeList<float> ages = _ages;
                    _ages = default;
                    dependency = ages.Dispose(dependency);
                }
                if (_visibleMatrices.IsCreated)
                {
                    NativeMemorySentinel.UnregisterNativeList(nameof(WreckMaterialRegistry), _visibleMatrixSentinelLabel);
                    NativeList<Matrix4x4> visibleMatrices = _visibleMatrices;
                    _visibleMatrices = default;
                    dependency = visibleMatrices.Dispose(dependency);
                }
                if (_visibleAges.IsCreated)
                {
                    NativeMemorySentinel.UnregisterNativeList(nameof(WreckMaterialRegistry), _visibleAgeSentinelLabel);
                    NativeList<float> visibleAges = _visibleAges;
                    _visibleAges = default;
                    dependency = visibleAges.Dispose(dependency);
                }
                if (_frustumPlaneSnapshot.IsCreated)
                {
                    NativeMemorySentinel.UnregisterNativeArray(_frustumPlaneSnapshot);
                    NativeArray<float4> frustumPlaneSnapshot = _frustumPlaneSnapshot;
                    _frustumPlaneSnapshot = default;
                    dependency = frustumPlaneSnapshot.Dispose(dependency);
                }

                return dependency;
            }

            private bool TryFinalizePendingVisibilityCullNoWait()
            {
                if (!_visibilityCullPending)
                    return true;

                if (!DispatcherJobSwap.TryFinalizeCompleted(ref _visibilityCullHandle))
                    return false;

                MarkVisibilityCullCompleted();
                return true;
            }

            private bool CompletePendingVisibilityCullForBarrier()
            {
                if (!_visibilityCullPending)
                    return true;

                if (!DispatcherJobSwap.TryComplete(ref _visibilityCullHandle, forceComplete: true))
                    return false;

                MarkVisibilityCullCompleted();
                return true;
            }

            private void MarkVisibilityCullCompleted()
            {
                _visibilityCullPending = false;
                _visibleSubsetFrustumVersion = _pendingVisibilityFrustumVersion;
                _pendingVisibilityFrustumVersion = InvalidFrustumVersion;
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
                _batchMetadata = new NativeArray<MetadataValue>(WreckBrgMetadataCount, DataVaultExemptRenderStagingAllocator); // COLD ALLOC: NativeArray<MetadataValue>[1] - BRG age metadata declaration for wreck module renderer - owner: WreckMaterialRegistry
                _batchMetadata[0] = new MetadataValue
                {
                    NameID = _WreckAgesId,
                    Value = 0u
                };
                NativeMemorySentinel.RegisterNativeArray(_batchMetadata, nameof(WreckMaterialRegistry), _metadataSentinelLabel, NativeAllocationLifetime.Scene);
                _batchHandleBuffer = HectonBatchRendererGroupUtility.CreateBatchHandleBuffer(); // COLD ALLOC: GraphicsBuffer[1] - BRG registration handle buffer for wreck module renderer - owner: WreckMaterialRegistry
                _batchId = _batchRendererGroup.AddBatch(_batchMetadata, _batchHandleBuffer.bufferHandle);
            }

            private void EnsureRuntimeMaterial()
            {
                Shader runtimeShader = _owner.ResolveRuntimeShader(_materialSource);
                if (_runtimeMaterial != null &&
                    _runtimeMaterialSource == _materialSource &&
                    _runtimeShader == runtimeShader)
                {
                    return;
                }

                if (_ownsRuntimeMaterial && _runtimeMaterial != null)
                {
                    if (Application.isPlaying)
                        Object.Destroy(_runtimeMaterial);
                    else
                        Object.DestroyImmediate(_runtimeMaterial);
                }

                _runtimeMaterial = null;
                _ownsRuntimeMaterial = false;
                _runtimeMaterialSource = null;
                _runtimeShader = null;
                _registeredMaterial = null;
                _batchMaterialId = default;

                if (_materialSource == null || runtimeShader == null)
                    return;

                _runtimeMaterial = new Material(runtimeShader)
                {
                    hideFlags = HideFlags.HideAndDontSave,
                    enableInstancing = true
                }; // COLD ALLOC: Material[1] - BRG-local wreck module material clone - owner: WreckMaterialRegistry
                _runtimeMaterial.CopyPropertiesFromMaterial(_materialSource);
                _ownsRuntimeMaterial = true;
                _runtimeMaterialSource = _materialSource;
                _runtimeShader = runtimeShader;
            }

            private static void ReleaseBuffer(ref GraphicsBuffer buffer)
            {
                if (buffer == null)
                    return;

                buffer.Release();
                buffer = null;
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
                if (_batchRendererGroup == null || _mesh == null || _runtimeMaterial == null)
                    return;

                if (_registeredMesh != _mesh)
                {
                    if (!_batchMeshId.Equals(default))
                        _batchRendererGroup.UnregisterMesh(_batchMeshId);

                    _batchMeshId = _batchRendererGroup.RegisterMesh(_mesh);
                    _registeredMesh = _mesh;
                }

                if (_registeredMaterial != _runtimeMaterial)
                {
                    if (!_batchMaterialId.Equals(default))
                        _batchRendererGroup.UnregisterMaterial(_batchMaterialId);

                    _batchMaterialId = _batchRendererGroup.RegisterMaterial(_runtimeMaterial);
                    _registeredMaterial = _runtimeMaterial;
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
        [Tooltip("Fallback material used for Essential-tier wreck modules when no module override is configured.")]
        private Material essentialTierMaterial;

        [SerializeField]
        [Tooltip("Fallback material used for Detail-tier wreck modules when no module override is configured.")]
        private Material detailTierMaterial;

        [SerializeField]
        [Tooltip("Fallback material used for Clutter-tier wreck modules when no module override is configured.")]
        private Material clutterTierMaterial;

        [Header("Module Contracts")]
        [SerializeField]
        [Tooltip("Optional per-slot module render overrides. Slot index must match the generator module-definition index.")]
        private ModuleRenderContract[] moduleContracts = new ModuleRenderContract[MaxModuleContracts];

        [SerializeField]
        [Tooltip("When true, all wreck matrices are published through one BRG draw command using the selected module contract. Required for procedural wrecks on the CPU budget path.")]
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
        private bool _hotSwapListenerRegistered;
        private bool _pdaSignalLatched;
        private bool _hasCachedFrustumState;
        private bool _visibilityUploadRequested;

        public int SkippedVisibilityUploadCount => _skippedVisibilityUploadCount;

        private static void IncrementCounterSaturated(ref int counter)
        {
            if (counter < int.MaxValue)
                counter++;
        }

        private void Awake()
        {
            ResolveIndirectShader();
            EnsureBatches();
        }

        private void OnEnable()
        {
            ResolveIndirectShader();
            EnsureBatches();
            HectonFloatingOrigin.RegisterListener(this);
            TryRegisterHotSwapListener();
            TryRegisterSlowTick();
        }

        private void OnDisable()
        {
            TryUnregisterSlowTick();
            TryUnregisterHotSwapListener();
            HectonFloatingOrigin.UnregisterListener(this);
            DisposeBatches();
        }

        private void OnDestroy()
        {
            TryUnregisterSlowTick();
            TryUnregisterHotSwapListener();
            HectonFloatingOrigin.UnregisterListener(this);
            DisposeBatches();
        }

        public void SlowTick()
        {
            if (!_hasPublishedWreck)
                return;

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
                ScanEvents.TryRaiseWreckSignalPing(
                    new float3(pingCenter.x, pingCenter.y, pingCenter.z),
                    math.max(1f, pdaSignalPingRadiusMeters));
                return;
            }

            float rearmRadius = math.max(signalRadius, pdaSignalRearmRadiusMeters);
            double rearmRadiusSq = (double)rearmRadius * rearmRadius;
            if (distanceSq >= rearmRadiusSq)
                _pdaSignalLatched = false;
        }

        public void LateFrameTick()
        {
            if (!_visibilityUploadRequested)
                return;

            _visibilityUploadRequested = false;
            RefreshVisibilityUploads();
        }

        private static bool TryResolvePlayerAup(out AbsoluteUniversePosition playerAup)
        {
            if (PlayerRuntimeContextService.TryGetActiveRuntimeContext(out PlayerRuntimeContext runtimeContext) &&
                runtimeContext != null)
            {
                HectonPlayerMovement playerMovement = runtimeContext.PlayerMovement;
                if (playerMovement != null)
                {
                    playerAup = playerMovement.CurrentAup;
                    return true;
                }

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
            if (!_HasUsableShift(shiftData.ShiftOffset))
                return;

            Vector3 runtimeOffset = -shiftData.ShiftOffset;
            EnsureBatches();
            for (int i = 0; i < _moduleBatches.Length; i++)
                _moduleBatches[i]?.ApplyOriginShift(runtimeOffset);

            _publishedWorldBounds.center += runtimeOffset;
            _hasCachedFrustumState = false;
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
        /// Publishes one wreck instance stream into the BRG-owned module batches.
        /// </summary>
        public void Publish(
            ProceduralWreckModuleDefinition[] moduleDefinitions,
            NativeArray<Matrix4x4> worldMatrices,
            NativeArray<byte> moduleIds,
            int instanceCount,
            Bounds worldBounds,
            AbsoluteUniversePosition wreckCenterAup)
        {
            Publish(
                moduleDefinitions,
                worldMatrices,
                moduleIds,
                default,
                instanceCount,
                worldBounds,
                wreckCenterAup);
        }

        /// <summary>
        /// Publishes one wreck instance stream plus per-instance age metadata and an authoritative AUP center.
        /// </summary>
        public void Publish(
            ProceduralWreckModuleDefinition[] moduleDefinitions,
            NativeArray<Matrix4x4> worldMatrices,
            NativeArray<byte> moduleIds,
            NativeArray<float> ages,
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
            bool hasFiniteWorldBounds = IsFiniteBounds(worldBounds);
            _publishedWreckCenterAup = default;

            if (!worldMatrices.IsCreated || !moduleIds.IsCreated || instanceCount <= 0)
                return;

            int moduleDefinitionCount = math.min(
                math.min(moduleDefinitions != null ? moduleDefinitions.Length : 0, MaxModuleContracts),
                _moduleBatches.Length);

            if (forceSingleDrawBatch)
            {
                int singleModuleIndex = ResolveSingleDrawModuleIndex(moduleDefinitions, moduleDefinitionCount);
                if (singleModuleIndex >= 0)
                {
                    ModuleBatch singleBatch = _moduleBatches[singleModuleIndex];
                    if (TryConfigureBatch(singleBatch, moduleDefinitions, singleModuleIndex, math.max(1, instanceCount)))
                    {
                        int safeSingleCount = math.min(instanceCount, worldMatrices.Length);
                        for (int instanceIndex = 0; instanceIndex < safeSingleCount; instanceIndex++)
                        {
                            float age01 = ages.IsCreated && instanceIndex < ages.Length
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
                            bool hasFrustum = TryPopulateFrustumPlanes(out _);
                            if (hasFrustum)
                                singleBatch.Publish(
                                    worldBounds,
                                    _frustumPlanes,
                                    enableFrustumCulling: true,
                                    frustumVersion: _frustumStateVersion);
                        }
                        return;
                    }
                }
            }

            for (int moduleIndex = 0; moduleIndex < moduleDefinitionCount; moduleIndex++)
                TryConfigureBatch(_moduleBatches[moduleIndex], moduleDefinitions, moduleIndex, math.max(1, instanceCount));

            int safeCount = math.min(instanceCount, math.min(worldMatrices.Length, moduleIds.Length));
            for (int instanceIndex = 0; instanceIndex < safeCount; instanceIndex++)
            {
                int moduleIndex = moduleIds[instanceIndex];
                if (moduleIndex < 0 || moduleIndex >= moduleDefinitionCount)
                    continue;

                ModuleBatch batch = _moduleBatches[moduleIndex];
                if (batch == null)
                    continue;

                float age01 = ages.IsCreated && instanceIndex < ages.Length
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
                return;

            bool hasFrustumForPublish = TryPopulateFrustumPlanes(out _);
            if (!hasFrustumForPublish)
                return;

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

            EnsureFrustumScratch();
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
            if (PlayerRuntimeContextService.TryGetActiveRuntimeContext(out PlayerRuntimeContext runtimeContext) &&
                runtimeContext != null &&
                runtimeContext.PlayerCamera != null)
            {
                _viewCamera = runtimeContext.PlayerCamera;
                return _viewCamera;
            }

            WorldRuntimeReferenceUtility.TryResolvePlayerTransform(ref _playerTransform);
            if (_playerTransform != null)
                _viewCamera = ComponentReferenceUtility.ResolveOwnedComponent<Camera>(_playerTransform);

            return _viewCamera;
        }

        private int ResolveSingleDrawModuleIndex(
            ProceduralWreckModuleDefinition[] moduleDefinitions,
            int moduleDefinitionCount)
        {
            if (moduleDefinitions == null || moduleDefinitionCount <= 0)
                return -1;

            int preferredIndex = math.clamp(singleDrawModuleIndex, 0, moduleDefinitionCount - 1);
            if (CanUseModuleContract(moduleDefinitions, preferredIndex))
                return preferredIndex;

            for (int moduleIndex = 0; moduleIndex < moduleDefinitionCount; moduleIndex++)
            {
                if (CanUseModuleContract(moduleDefinitions, moduleIndex))
                    return moduleIndex;
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
            return mesh != null && material != null;
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
            Material material = ResolveMaterialForModule(moduleIndex, definition.DrawCallPriority);
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

        private int ResolveMaxInstancesPerWreckBatch()
        {
            return math.max(1, maxInstancesPerWreckBatch);
        }

        private void TryRegisterSlowTick()
        {
            if (!Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            if (!_registeredSlowTick)
                _registeredSlowTick = GlobalRegistry.TryRegisterSlowTickable(this, PriorityLayer.Environment);

            if (!_registeredLateFrameTick)
                _registeredLateFrameTick = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Environment);
        }

        private void TryUnregisterSlowTick()
        {
            if (_registeredLateFrameTick)
            {
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Environment);
                _registeredLateFrameTick = false;
            }

            if (!_registeredSlowTick)
                return;

            GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Environment);
            _registeredSlowTick = false;
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot == GlobalRegistryServiceSlot.Dispatcher)
            {
                _registeredSlowTick = false;
                _registeredLateFrameTick = false;
                if (currentService != null && isActiveAndEnabled)
                    TryRegisterSlowTick();
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

        private void EnsureBatches()
        {
            if (_moduleBatches != null && _moduleBatches.Length == MaxModuleContracts)
                return;

            _moduleBatches = new ModuleBatch[MaxModuleContracts]; // COLD ALLOC: ModuleBatch[16] - procedural wreck BRG owners by module slot - owner: WreckMaterialRegistry
            for (int i = 0; i < _moduleBatches.Length; i++)
            {
                _moduleBatches[i] = new ModuleBatch(this, i);
                _moduleBatches[i].EnsureCapacity(maxInstancesPerWreckBatch);
            }
        }

        private void EnsureFrustumScratch()
        {
            if (_frustumPlaneCache == null || _frustumPlaneCache.Length != FrustumPlaneCount)
                _frustumPlaneCache = new Plane[FrustumPlaneCount]; // COLD ALLOC: Plane[6] - player-camera wreck BRG upload culling planes - owner: WreckMaterialRegistry

            if (_frustumPlanes == null || _frustumPlanes.Length != FrustumPlaneCount)
                _frustumPlanes = new float4[FrustumPlaneCount]; // COLD ALLOC: float4[6] - managed camera-frustum snapshot copied into per-batch native job snapshots.
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
                return;
            }

            for (int i = 0; i < _moduleBatches.Length; i++)
                _moduleBatches[i]?.Dispose();

            _moduleBatches = null;
            _hasPublishedWreck = false;
            _publishedWreckCenterAup = default;
            _hasCachedFrustumState = false;
            DisposeFrustumScratch();
        }

        private void DisposeFrustumScratch()
        {
            _frustumPlanes = null;
        }

        private Material ResolveMaterialForModule(int moduleIndex, WreckLodTier tier)
        {
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

        private void ResolveIndirectShader()
        {
            if (indirectWreckShader == null)
                indirectWreckShader = Shader.Find(IndirectWreckShaderName);
        }

        private Shader ResolveRuntimeShader(Material sourceMaterial)
        {
            ResolveIndirectShader();
            if (sourceMaterial == null)
                return indirectWreckShader;

            Shader sourceShader = sourceMaterial.shader;
            if (sourceShader != null &&
                string.Equals(sourceShader.name, IndirectWreckShaderName, System.StringComparison.Ordinal))
            {
                return sourceShader;
            }

            return indirectWreckShader != null ? indirectWreckShader : sourceShader;
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
