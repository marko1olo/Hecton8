using Hecton8.Core;
using Unity.Collections;
using System;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Hecton8.World
{
    /// <summary>
    /// Draws far-field cartographer HLODs through BRG draw commands with per-instance fade.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-89)]
    public sealed class HectonHLODRenderer : MonoBehaviour, ITickable, IUpdatable, IOriginShiftListener
    {
#if UNITY_EDITOR
        private const string ShaderAssetPath = "Assets/_Project/Art/Shaders/Hecton_HLODUnlitFog.shader";
#endif
        private const int BrgMetadataPlaceholderCount = 1;
        private const Allocator DataVaultExemptHlodBrgAllocator = Allocator.Persistent;
        private const Allocator DataVaultExemptHlodUploadAllocator = Allocator.Persistent;

        private static readonly int InstanceMatricesId = Shader.PropertyToID("_HectonHLODInstanceMatrices");
        private static readonly int InstanceFadeId = Shader.PropertyToID("_HectonHLODInstanceFade");
        private static readonly int GlobalFloatingOffsetId = Shader.PropertyToID("_GlobalFloatingOffset");

        [Header("-- Rendering ----------------")]
        [SerializeField]
        [Tooltip("Shared HLOD mesh drawn for every published far-field instance.")]
        private Mesh _mesh;

        [SerializeField]
        [Tooltip("Optional explicit HLOD material. Hidden fallback is used when empty.")]
        private Material _material;

        [SerializeField]
        [Tooltip("Optional hidden shader fallback used for the runtime HLOD material.")]
        private Shader _shader;

        [SerializeField]
        [Tooltip("Submesh index rendered through the BRG draw commands.")]
        private int _subMeshIndex;

        [SerializeField]
        [Tooltip("Optional camera override. Leave null for all cameras.")]
        private Camera _cameraOverride;

        [Header("-- Bounds -------------------")]
        [SerializeField]
        [Tooltip("Fallback center offset used when no explicit HLOD bounds were published.")]
        private Vector3 _boundsCenterOffset = Vector3.zero;

        [SerializeField]
        [Tooltip("Fallback conservative HLOD bounds.")]
        private Vector3 _boundsSize = new Vector3(3000f, 1600f, 3000f);

        private GraphicsBuffer _matrixBuffer;
        private GraphicsBuffer _fadeBuffer;
        private NativeArray<Matrix4x4> _uploadedMatrices;
        private NativeArray<Vector4> _uploadedFade;
        private GraphicsBuffer _uploadedMatrixBuffer;
        private GraphicsBuffer _uploadedFadeBuffer;
        private Bounds _drawBounds;
        private int _instanceCount;
        private bool _hasBoundsOverride;
        private bool _isRegistered;
        private Vector4 _lastGlobalFloatingOffset = new Vector4(float.NaN, float.NaN, float.NaN, float.NaN);
        private BatchRendererGroup _batchRendererGroup;
        private NativeArray<MetadataValue> _batchMetadata;
        private GraphicsBuffer _batchHandleBuffer;
        private BatchID _batchId;
        private BatchMeshID _batchMeshId;
        private BatchMaterialID _batchMaterialId;
        private Mesh _registeredMesh;
        private Material _registeredMaterial;
        private GraphicsBuffer _registeredBatchBuffer;
        private Bounds _registeredDrawBounds;
        private bool _registeredDrawBoundsValid;

        private void Awake()
        {
            _drawBounds = new Bounds(transform.position + _boundsCenterOffset, _boundsSize);
            EnsureResources();
        }

        private void OnEnable()
        {
            HectonFloatingOrigin.RegisterListener(this);
            RegisterTick();
        }

        private void OnDisable()
        {
            HectonFloatingOrigin.UnregisterListener(this);
            UnregisterTick();
        }

        private void OnDestroy()
        {
            HectonFloatingOrigin.UnregisterListener(this);
            ReleaseResources();
        }

        /// <inheritdoc />
        public void Tick(float dt)
        {
            if (_instanceCount <= 0 || _matrixBuffer == null || _fadeBuffer == null)
                return;

            EnsureResources();
            if (_batchRendererGroup == null || _batchId.Equals(default))
                return;

            Mesh activeMesh = _mesh;
            Material activeMaterial = ResolveMaterial();
            if (activeMesh == null || activeMaterial == null)
                return;

            Vector4 globalFloatingOffset = ResolveGlobalFloatingOffset();
            if (_lastGlobalFloatingOffset != globalFloatingOffset)
            {
                Shader.SetGlobalVector(GlobalFloatingOffsetId, globalFloatingOffset);
                _lastGlobalFloatingOffset = globalFloatingOffset;
            }

            Shader.SetGlobalBuffer(InstanceMatricesId, _matrixBuffer);
            Shader.SetGlobalBuffer(InstanceFadeId, _fadeBuffer);
            SyncBatchRegistration(activeMesh, activeMaterial);
            SyncBatchBuffer(_matrixBuffer);
            SetBatchGlobalBoundsIfChanged(ResolveDrawBounds());
        }

        /// <summary>
        /// Uploads cartographer-owned HLOD instances into renderer-owned BRG buffers without managed allocations.
        /// </summary>
        public void BindNativeInstances(NativeArray<HLODInstance> instances, int instanceCount)
        {
            if (!instances.IsCreated || instanceCount <= 0 || instances.Length < instanceCount)
            {
                ClearBinding();
                return;
            }

            EnsureOwnedUploadCapacity(instanceCount);
            if (!_uploadedMatrices.IsCreated || !_uploadedFade.IsCreated || _uploadedMatrixBuffer == null || _uploadedFadeBuffer == null)
            {
                ClearBinding();
                return;
            }

            Vector4 globalFloatingOffset = ResolveGlobalFloatingOffset();
            Vector3 floatingOffset = new Vector3(globalFloatingOffset.x, globalFloatingOffset.y, globalFloatingOffset.z);
            Bounds combinedBounds = default;
            bool hasCombinedBounds = false;
            for (int i = 0; i < instanceCount; i++)
            {
                HLODInstance instance = instances[i];
                _uploadedMatrices[i] = instance.LocalToWorld;
                _uploadedFade[i] = new Vector4(Mathf.Clamp01(instance.Fade01), 0f, 0f, 0f);

                Bounds worldBounds = instance.LocalBounds;
                worldBounds.center += floatingOffset;
                if (hasCombinedBounds)
                    combinedBounds.Encapsulate(worldBounds);
                else
                {
                    combinedBounds = worldBounds;
                    hasCombinedBounds = true;
                }
            }

            GraphicsBufferUploadUtility.UploadNativeArray(_uploadedMatrixBuffer, _uploadedMatrices, instanceCount);
            GraphicsBufferUploadUtility.UploadNativeArray(_uploadedFadeBuffer, _uploadedFade, instanceCount);
            _matrixBuffer = _uploadedMatrixBuffer;
            _fadeBuffer = _uploadedFadeBuffer;
            _instanceCount = instanceCount;
            _drawBounds = hasCombinedBounds ? combinedBounds : new Bounds(transform.position + _boundsCenterOffset, _boundsSize);
            _hasBoundsOverride = hasCombinedBounds;
        }

        public void OnOriginShift(in OriginShiftEventData shiftData)
        {
            if (!isActiveAndEnabled || !_hasBoundsOverride || shiftData.ShiftOffset.sqrMagnitude <= 0.0001f)
                return;

            Bounds drawBounds = _drawBounds;
            drawBounds.center -= shiftData.ShiftOffset;
            _drawBounds = drawBounds;

            if (_batchRendererGroup != null)
                SetBatchGlobalBoundsIfChanged(_drawBounds);
        }

        /// <summary>
        /// Clears the current HLOD binding and suppresses rendering until a new instance list arrives.
        /// </summary>
        public void ClearBinding()
        {
            _matrixBuffer = null;
            _fadeBuffer = null;
            _instanceCount = 0;
            _hasBoundsOverride = false;
        }

        private void RegisterTick()
        {
            if (_isRegistered || !Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            GlobalRegistry.RegisterUpdatable(this, PriorityLayer.Environment);
            _isRegistered = GlobalRegistry.Updatables.Contains(this);
        }

        private void UnregisterTick()
        {
            if (!_isRegistered)
                return;

            GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Environment);
            _isRegistered = false;
        }

        private void EnsureResources()
        {
            if (_batchRendererGroup == null)
            {
                _batchRendererGroup = new BatchRendererGroup(new BatchRendererGroupCreateInfo
                {
                    cullingCallback = OnPerformCulling,
                    userContext = IntPtr.Zero
                });

                _batchMetadata = new NativeArray<MetadataValue>(BrgMetadataPlaceholderCount, DataVaultExemptHlodBrgAllocator); // COLD ALLOC: NativeArray<MetadataValue>[1] - BRG metadata placeholder for HLOD renderer - owner: HectonHLODRenderer
                NativeMemorySentinel.RegisterNativeArray(_batchMetadata, nameof(HectonHLODRenderer), nameof(_batchMetadata), NativeAllocationLifetime.Session);
                _batchHandleBuffer = HectonBatchRendererGroupUtility.CreateBatchHandleBuffer(); // COLD ALLOC: GraphicsBuffer[1] - BRG registration handle buffer for HLOD renderer - owner: HectonHLODRenderer
                _batchId = _batchRendererGroup.AddBatch(_batchMetadata, _batchHandleBuffer.bufferHandle);
                _registeredDrawBoundsValid = false;
                SetBatchGlobalBoundsIfChanged(ResolveDrawBounds());
            }
        }

        private void SyncBatchRegistration(Mesh activeMesh, Material activeMaterial)
        {
            if (_batchRendererGroup == null)
                return;

            if (_registeredMesh != activeMesh)
            {
                if (!_batchMeshId.Equals(default))
                    _batchRendererGroup.UnregisterMesh(_batchMeshId);

                _batchMeshId = activeMesh != null ? _batchRendererGroup.RegisterMesh(activeMesh) : default;
                _registeredMesh = activeMesh;
            }

            if (_registeredMaterial != activeMaterial)
            {
                if (!_batchMaterialId.Equals(default))
                    _batchRendererGroup.UnregisterMaterial(_batchMaterialId);

                _batchMaterialId = activeMaterial != null ? _batchRendererGroup.RegisterMaterial(activeMaterial) : default;
                _registeredMaterial = activeMaterial;
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

        private void SetBatchGlobalBoundsIfChanged(Bounds bounds)
        {
            if (_batchRendererGroup == null)
                return;

            if (_registeredDrawBoundsValid &&
                _registeredDrawBounds.center == bounds.center &&
                _registeredDrawBounds.size == bounds.size)
            {
                return;
            }

            _batchRendererGroup.SetGlobalBounds(bounds);
            _registeredDrawBounds = bounds;
            _registeredDrawBoundsValid = true;
        }

        private void EnsureOwnedUploadCapacity(int instanceCount)
        {
            int nextCapacity = Mathf.NextPowerOfTwo(Mathf.Max(1, instanceCount));
            if (_uploadedMatrices.IsCreated &&
                _uploadedMatrices.Length >= nextCapacity &&
                _uploadedFade.IsCreated &&
                _uploadedFade.Length >= nextCapacity &&
                _uploadedMatrixBuffer != null &&
                _uploadedMatrixBuffer.count >= nextCapacity &&
                _uploadedFadeBuffer != null &&
                _uploadedFadeBuffer.count >= nextCapacity)
            {
                return;
            }

            if (_uploadedMatrices.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeArray(_uploadedMatrices);
                _uploadedMatrices.Dispose();
            }

            if (_uploadedFade.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeArray(_uploadedFade);
                _uploadedFade.Dispose();
            }

            if (_uploadedMatrixBuffer != null)
            {
                _uploadedMatrixBuffer.Release();
                _uploadedMatrixBuffer = null;
            }

            if (_uploadedFadeBuffer != null)
            {
                _uploadedFadeBuffer.Release();
                _uploadedFadeBuffer = null;
            }

            _uploadedMatrices = new NativeArray<Matrix4x4>(nextCapacity, DataVaultExemptHlodUploadAllocator, NativeArrayOptions.UninitializedMemory); // COLD ALLOC: NativeArray<Matrix4x4>[NextPowerOfTwo(requiredCount)] - HLOD matrix upload cache - owner: HectonHLODRenderer
            _uploadedFade = new NativeArray<Vector4>(nextCapacity, DataVaultExemptHlodUploadAllocator, NativeArrayOptions.UninitializedMemory); // COLD ALLOC: NativeArray<Vector4>[NextPowerOfTwo(requiredCount)] - HLOD fade upload cache - owner: HectonHLODRenderer
            NativeMemorySentinel.RegisterNativeArray(_uploadedMatrices, nameof(HectonHLODRenderer), nameof(_uploadedMatrices), NativeAllocationLifetime.Session);
            NativeMemorySentinel.RegisterNativeArray(_uploadedFade, nameof(HectonHLODRenderer), nameof(_uploadedFade), NativeAllocationLifetime.Session);
            _uploadedMatrixBuffer = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<Matrix4x4>(nextCapacity); // COLD ALLOC: GraphicsBuffer[NextPowerOfTwo(requiredCount)] - HLOD matrix buffer - owner: HectonHLODRenderer
            _uploadedFadeBuffer = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<Vector4>(nextCapacity); // COLD ALLOC: GraphicsBuffer[NextPowerOfTwo(requiredCount)] - HLOD fade buffer - owner: HectonHLODRenderer
        }

        private Material ResolveMaterial()
        {
            return _material;
        }

        private Bounds ResolveDrawBounds()
        {
            if (_hasBoundsOverride)
                return _drawBounds;

            return new Bounds(transform.position + _boundsCenterOffset, _boundsSize);
        }

        private void ReleaseResources()
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
                _batchId = default;
                _batchMeshId = default;
                _batchMaterialId = default;
                _registeredMesh = null;
                _registeredMaterial = null;
                _registeredBatchBuffer = null;
                _registeredDrawBoundsValid = false;
            }

            if (_batchHandleBuffer != null)
            {
                _batchHandleBuffer.Release();
                _batchHandleBuffer = null;
            }

            if (_batchMetadata.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeArray(_batchMetadata);
                _batchMetadata.Dispose();
            }

            if (_uploadedMatrixBuffer != null)
            {
                _uploadedMatrixBuffer.Release();
                _uploadedMatrixBuffer = null;
                _registeredBatchBuffer = null;
            }

            if (_uploadedFadeBuffer != null)
            {
                _uploadedFadeBuffer.Release();
                _uploadedFadeBuffer = null;
            }

            if (_uploadedMatrices.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeArray(_uploadedMatrices);
                _uploadedMatrices.Dispose();
            }

            if (_uploadedFade.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeArray(_uploadedFade);
                _uploadedFade.Dispose();
            }

        }

        private JobHandle OnPerformCulling(
            BatchRendererGroup rendererGroup,
            BatchCullingContext cullingContext,
            BatchCullingOutput cullingOutput,
            IntPtr userContext)
        {
            if (_instanceCount <= 0 ||
                _matrixBuffer == null ||
                _fadeBuffer == null ||
                _batchId.Equals(default) ||
                _batchMeshId.Equals(default) ||
                _batchMaterialId.Equals(default))
            {
                HectonBatchRendererGroupUtility.WriteDirectDrawOutput(
                    cullingOutput,
                    HectonBatchRendererGroupUtility.AllocateDirectDrawOutput(0, 0, 0));
                return default;
            }

            Bounds drawBounds = ResolveDrawBounds();
            if (!HectonBatchRendererGroupUtility.IsBoundsVisible(cullingContext.cullingPlanes, drawBounds))
            {
                HectonBatchRendererGroupUtility.WriteDirectDrawOutput(
                    cullingOutput,
                    HectonBatchRendererGroupUtility.AllocateDirectDrawOutput(0, 0, 0));
                return default;
            }

            HectonBatchRendererGroupUtility.WriteAllVisibleSingleDrawOutput(
                cullingOutput,
                _instanceCount,
                _batchId,
                _batchMeshId,
                _batchMaterialId,
                gameObject.layer,
                _subMeshIndex,
                ShadowCastingMode.Off,
                receiveShadows: false,
                MotionVectorGenerationMode.Camera);
            return default;
        }

        private static Vector4 ResolveGlobalFloatingOffset()
        {
            Vector3 totalOffset = HectonMapMagicVegetationBridge.GlobalTotalUniverseOffset;
            return new Vector4(totalOffset.x, totalOffset.y, totalOffset.z, 0f);
        }
    }
}
