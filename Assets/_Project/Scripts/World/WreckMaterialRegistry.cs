using Hecton8.Core;
using Unity.Burst;
using Unity.Collections;
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
    public sealed class WreckMaterialRegistry : MonoBehaviour, IOriginShiftListener
    {
        private const int MaxModuleContracts = 16;
        private const string IndirectWreckShaderName = "Hecton8/World/WreckIndirectLit";
        private static readonly int _WreckMatricesId = Shader.PropertyToID("_HectonWreckMatrices");

        [System.Serializable]
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

        private sealed class ModuleBatch
        {
            private readonly WreckMaterialRegistry _owner;
            private readonly int _moduleIndex;
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
            private GraphicsBuffer _matrixBuffer;
            private NativeList<Matrix4x4> _matrices;
            private Bounds _drawBounds;
            private Mesh _mesh;
            private int _subMeshIndex;
            private int _layer;
            private ShadowCastingMode _shadowCastingMode;
            private bool _receiveShadows;
            private bool _ownsRuntimeMaterial;

            public ModuleBatch(WreckMaterialRegistry owner, int moduleIndex)
            {
                _owner = owner;
                _moduleIndex = moduleIndex;
            }

            public bool HasContent => _matrices.IsCreated && _matrices.Length > 0;

            public void EnsureCapacity(int minimumCapacity)
            {
                int nextCapacity = math.max(1, minimumCapacity);
                if (!_matrices.IsCreated)
                {
                    _matrices = new NativeList<Matrix4x4>(nextCapacity, Allocator.Persistent); // COLD ALLOC: NativeList<Matrix4x4>[maxPlacements] - BRG wreck module matrix staging - owner: WreckMaterialRegistry
                    return;
                }

                if (_matrices.Capacity < nextCapacity)
                    _matrices.Capacity = nextCapacity;
            }

            public void Reset()
            {
                if (_matrices.IsCreated)
                    _matrices.Clear();
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
            }

            public void AddMatrix(Matrix4x4 matrix)
            {
                if (_matrices.IsCreated)
                    _matrices.Add(matrix);
            }

            public void Publish(Bounds drawBounds)
            {
                _drawBounds = drawBounds;
                if (_mesh == null || _materialSource == null || !_matrices.IsCreated || _matrices.Length <= 0)
                    return;

                EnsureResources();
                EnsureRuntimeMaterial();
                EnsureMatrixBufferCapacity(_matrices.Length);
                if (_batchRendererGroup == null || _runtimeMaterial == null || _matrixBuffer == null)
                    return;

                GraphicsBufferUploadUtility.UploadNativeArray(_matrixBuffer, _matrices.AsArray(), _matrices.Length);
                _runtimeMaterial.SetBuffer(_WreckMatricesId, _matrixBuffer);
                SyncBatchBuffer(_matrixBuffer);
                SyncBatchRegistration();
                _batchRendererGroup.SetGlobalBounds(_drawBounds);
            }

            public void ApplyOriginShift(Vector3 runtimeOffset)
            {
                if (_matrices.IsCreated)
                {
                    int count = _matrices.Length;
                    for (int i = 0; i < count; i++)
                    {
                        Matrix4x4 matrix = _matrices[i];
                        matrix.m03 += runtimeOffset.x;
                        matrix.m13 += runtimeOffset.y;
                        matrix.m23 += runtimeOffset.z;
                        _matrices[i] = matrix;
                    }

                    if (_matrixBuffer != null && count > 0)
                        GraphicsBufferUploadUtility.UploadNativeArray(_matrixBuffer, _matrices.AsArray(), count);
                }

                _drawBounds.center += runtimeOffset;
                if (_batchRendererGroup != null)
                    _batchRendererGroup.SetGlobalBounds(_drawBounds);
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

                if (_batchMetadata.IsCreated)
                    _batchMetadata.Dispose();

                if (_batchHandleBuffer != null)
                {
                    _batchHandleBuffer.Release();
                    _batchHandleBuffer = null;
                }

                if (_matrixBuffer != null)
                {
                    _matrixBuffer.Release();
                    _matrixBuffer = null;
                }

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
                if (_matrices.IsCreated)
                    _matrices.Dispose();
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
                _batchMetadata = new NativeArray<MetadataValue>(0, Allocator.Persistent); // COLD ALLOC: NativeArray<MetadataValue>[0] - BRG metadata placeholder for wreck module renderer - owner: WreckMaterialRegistry
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
                    name = $"__WreckModule_{_moduleIndex}_BRG",
                    enableInstancing = true
                }; // COLD ALLOC: Material[1] - BRG-local wreck module material clone - owner: WreckMaterialRegistry
                _runtimeMaterial.CopyPropertiesFromMaterial(_materialSource);
                _ownsRuntimeMaterial = true;
                _runtimeMaterialSource = _materialSource;
                _runtimeShader = runtimeShader;
            }

            private void EnsureMatrixBufferCapacity(int instanceCount)
            {
                int required = Mathf.NextPowerOfTwo(math.max(1, instanceCount));
                if (_matrixBuffer != null && _matrixBuffer.count >= required)
                    return;

                if (_matrixBuffer != null)
                    _matrixBuffer.Release();

                _matrixBuffer = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<Matrix4x4>(required); // COLD ALLOC: GraphicsBuffer[NextPowerOfTwo(instanceCount)] - wreck module matrix upload buffer - owner: WreckMaterialRegistry
                _registeredBatchBuffer = null;
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
                int instanceCount = _matrices.IsCreated ? _matrices.Length : 0;
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

                NativeArray<byte> visibilityMask = new NativeArray<byte>(instanceCount, Allocator.TempJob, NativeArrayOptions.ClearMemory);
                int planeCount = cullingContext.cullingPlanes.IsCreated ? cullingContext.cullingPlanes.Length : 0;
                NativeArray<float4> cullingPlanes = default;
                if (planeCount > 0)
                {
                    cullingPlanes = new NativeArray<float4>(planeCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                    for (int planeIndex = 0; planeIndex < planeCount; planeIndex++)
                    {
                        Plane plane = cullingContext.cullingPlanes[planeIndex];
                        cullingPlanes[planeIndex] = new float4(plane.normal.x, plane.normal.y, plane.normal.z, plane.distance);
                    }
                }

                unsafe
                {
                    BatchCullingOutputDrawCommands output = HectonBatchRendererGroupUtility.AllocateDirectDrawOutput(instanceCount, 1, 1);
                    JobHandle visibilityHandle = new HectonBatchRendererGroupUtility.BuildMatrixVisibilityMaskJob
                    {
                        Matrices = _matrices.AsArray(),
                        CullingPlanes = cullingPlanes,
                        VisibilityMask = visibilityMask,
                        InstanceCount = instanceCount,
                        PlaneCount = planeCount,
                        EnableCpuCulling = true,
                        GlobalOffset = float3.zero,
                        RadiusScale = 1.7321f,
                        MinRadius = 0.5f
                    }.Schedule(instanceCount, 64);

                    JobHandle finalizeHandle = new HectonBatchRendererGroupUtility.FinalizeSingleDrawCommandOutputJob
                    {
                        VisibilityMask = visibilityMask,
                        InstanceCount = instanceCount,
                        BatchId = _batchId,
                        MeshId = _batchMeshId,
                        MaterialId = _batchMaterialId,
                        Layer = _layer,
                        SubMeshIndex = _subMeshIndex,
                        ShadowCastingMode = _shadowCastingMode,
                        ReceiveShadows = _receiveShadows,
                        MotionMode = MotionVectorGenerationMode.Object,
                        VisibleInstances = output.visibleInstances,
                        DrawCommands = output.drawCommands,
                        DrawRanges = output.drawRanges,
                        OutputCommands = HectonBatchRendererGroupUtility.GetDirectDrawOutputPointer(cullingOutput)
                    }.Schedule(visibilityHandle);

                    JobHandle disposeHandle = visibilityMask.Dispose(finalizeHandle);
                    if (cullingPlanes.IsCreated)
                        disposeHandle = cullingPlanes.Dispose(disposeHandle);

                    return disposeHandle;
                }
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

        private ModuleBatch[] _moduleBatches;

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
        }

        private void OnDisable()
        {
            HectonFloatingOrigin.UnregisterListener(this);
            DisposeBatches();
        }

        private void OnDestroy()
        {
            HectonFloatingOrigin.UnregisterListener(this);
            DisposeBatches();
        }

        public void OnOriginShift(in OriginShiftEventData shiftData)
        {
            if (!_HasUsableShift(shiftData.ShiftOffset))
                return;

            Vector3 runtimeOffset = -shiftData.ShiftOffset;
            EnsureBatches();
            for (int i = 0; i < _moduleBatches.Length; i++)
                _moduleBatches[i]?.ApplyOriginShift(runtimeOffset);
        }

        /// <summary>
        /// Publishes one wreck instance stream into the BRG-owned module batches.
        /// </summary>
        public void Publish(
            ProceduralWreckModuleDefinition[] moduleDefinitions,
            NativeArray<Matrix4x4> worldMatrices,
            NativeArray<byte> moduleIds,
            int instanceCount,
            Bounds worldBounds)
        {
            EnsureBatches();
            ResetAllBatches();

            int moduleDefinitionCount = math.min(
                math.min(moduleDefinitions != null ? moduleDefinitions.Length : 0, MaxModuleContracts),
                _moduleBatches.Length);

            for (int moduleIndex = 0; moduleIndex < moduleDefinitionCount; moduleIndex++)
            {
                ProceduralWreckModuleDefinition definition = moduleDefinitions[moduleIndex];
                if (!definition.EmitsGeometry)
                    continue;

                Mesh mesh = moduleContracts != null &&
                            moduleIndex < moduleContracts.Length &&
                            moduleContracts[moduleIndex].MeshOverride != null
                    ? moduleContracts[moduleIndex].MeshOverride
                    : definition.StructuralMesh;
                Material material = ResolveMaterialForModule(moduleIndex, definition.DrawCallPriority);
                if (mesh == null || material == null)
                    continue;

                ModuleRenderContract contract = moduleContracts != null && moduleIndex < moduleContracts.Length
                    ? moduleContracts[moduleIndex]
                    : default;

                ModuleBatch batch = _moduleBatches[moduleIndex];
                batch.EnsureCapacity(math.max(1, instanceCount));
                batch.Configure(
                    mesh,
                    material,
                    contract.SubMeshIndex,
                    contract.ShadowCastingMode,
                    contract.ReceiveShadows,
                    gameObject.layer);
            }

            int safeCount = math.min(instanceCount, math.min(worldMatrices.Length, moduleIds.Length));
            for (int instanceIndex = 0; instanceIndex < safeCount; instanceIndex++)
            {
                int moduleIndex = moduleIds[instanceIndex];
                if (moduleIndex < 0 || moduleIndex >= moduleDefinitionCount)
                    continue;

                ModuleBatch batch = _moduleBatches[moduleIndex];
                if (batch == null)
                    continue;

                batch.AddMatrix(worldMatrices[instanceIndex]);
            }

            for (int moduleIndex = 0; moduleIndex < moduleDefinitionCount; moduleIndex++)
            {
                ModuleBatch batch = _moduleBatches[moduleIndex];
                if (batch == null || !batch.HasContent)
                    continue;

                batch.Publish(worldBounds);
            }
        }

        private void EnsureBatches()
        {
            if (_moduleBatches != null && _moduleBatches.Length == MaxModuleContracts)
                return;

            _moduleBatches = new ModuleBatch[MaxModuleContracts]; // COLD ALLOC: ModuleBatch[16] - procedural wreck BRG owners by module slot - owner: WreckMaterialRegistry
            for (int i = 0; i < _moduleBatches.Length; i++)
                _moduleBatches[i] = new ModuleBatch(this, i);
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
                return;

            for (int i = 0; i < _moduleBatches.Length; i++)
                _moduleBatches[i]?.Dispose();

            _moduleBatches = null;
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
    }
}
