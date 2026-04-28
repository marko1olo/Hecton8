using Hecton8.Core;
using System;
using Unity.Collections;
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
    /// Draws distant landmark silhouettes through BRG draw commands using an externally owned matrix buffer.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-90)]
    public sealed class HectonDistantLandmarkRenderer : MonoBehaviour, ITickable, IUpdatable, IOriginShiftListener
    {
#if UNITY_EDITOR
        private const string SilhouetteShaderAssetPath = "Assets/_Project/Art/Shaders/Hecton_DistantLandmarkSilhouette.shader";
#endif

        private static readonly int LandmarkMatricesId = Shader.PropertyToID("_HectonLandmarkMatrices");
        private static readonly int LandmarkFadeId = Shader.PropertyToID("_HectonHLODInstanceFade");

        [Header("-- Rendering ----------------")]
        [SerializeField]
        [Tooltip("Shared mesh drawn for each distant landmark instance.")]
        private Mesh _mesh;

        [SerializeField]
        [Tooltip("Material used for the silhouette-only BRG draw. If empty, the hidden shader fallback is used.")]
        private Material _material;

        [SerializeField]
        [Tooltip("Optional shader fallback used to build the hidden landmark material when no material is assigned.")]
        private Shader _silhouetteShader;

        [SerializeField]
        [Tooltip("Submesh index rendered through RenderMeshPrimitives.")]
        private int _subMeshIndex;

        [SerializeField]
        [Tooltip("Optional camera override. Leave null to let Unity draw for all cameras.")]
        private Camera _cameraOverride;

        [Header("-- Bounds -------------------")]
        [SerializeField]
        [Tooltip("Fallback local center offset used when no explicit bounds are published with the landmark buffer.")]
        private Vector3 _boundsCenterOffset = Vector3.zero;

        [SerializeField]
        [Tooltip("Fallback local bounds size used when no explicit world bounds are published with the landmark buffer.")]
        private Vector3 _boundsSize = new Vector3(1200f, 600f, 1200f);

        private GraphicsBuffer _externalMatrixBuffer;
        private MaterialPropertyBlock _propertyBlock;
        private Material _runtimeMaterial;
        private NativeArray<Matrix4x4> _uploadedLandmarkMatrices;
        private NativeArray<Vector4> _uploadedLandmarkFade;
        private GraphicsBuffer _uploadedMatrixBuffer;
        private GraphicsBuffer _uploadedFadeBuffer;
        private Bounds _drawBounds;
        private int _instanceCount;
        private bool _hasBoundsOverride;
        private bool _isRegistered;
        private bool _ownsRuntimeMaterial;
        private bool _usingOwnedUploadBuffers;
        private BatchRendererGroup _batchRendererGroup;
        private NativeArray<MetadataValue> _batchMetadata;
        private GraphicsBuffer _batchHandleBuffer;
        private BatchID _batchId;
        private BatchMeshID _batchMeshId;
        private BatchMaterialID _batchMaterialId;
        private Mesh _registeredMesh;
        private Material _registeredMaterial;
        private Material _brgMaterial;
        private Material _brgMaterialSource;
        private bool _ownsBrgMaterial;

        /// <summary>
        /// Gets whether an external landmark matrix buffer is currently bound.
        /// </summary>
        public bool HasMatrixBuffer => _externalMatrixBuffer != null || (_usingOwnedUploadBuffers && _uploadedMatrixBuffer != null);

        /// <summary>
        /// Gets the currently bound landmark instance count.
        /// </summary>
        public int BoundInstanceCount => _instanceCount;

        private void Awake()
        {
            _propertyBlock = new MaterialPropertyBlock(); // COLD ALLOC: MaterialPropertyBlock[1] - distant landmark draw properties - owner: HectonDistantLandmarkRenderer
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
            if (_instanceCount <= 0)
                return;

            EnsureResources();
            if (_batchRendererGroup == null || _batchId.Equals(default))
                return;

            Mesh activeMesh = _mesh;
            Material activeMaterial = EnsureBrgMaterial();
            if (activeMesh == null || activeMaterial == null)
                return;

            if (_usingOwnedUploadBuffers)
            {
                if (_uploadedMatrixBuffer == null)
                    return;

                activeMaterial.SetBuffer(LandmarkMatricesId, _uploadedMatrixBuffer);
                if (_uploadedFadeBuffer != null)
                    activeMaterial.SetBuffer(LandmarkFadeId, _uploadedFadeBuffer);
                _batchRendererGroup.SetBatchBuffer(_batchId, _uploadedMatrixBuffer.bufferHandle);
            }
            else
            {
                if (_externalMatrixBuffer == null)
                    return;

                activeMaterial.SetBuffer(LandmarkMatricesId, _externalMatrixBuffer);
                _batchRendererGroup.SetBatchBuffer(_batchId, _externalMatrixBuffer.bufferHandle);
            }

            SyncBatchRegistration(activeMesh, activeMaterial);
            _batchRendererGroup.SetGlobalBounds(ResolveDrawBounds());
        }

        /// <summary>
        /// Binds the externally owned matrix buffer and world bounds used by the distant landmark draw.
        /// </summary>
        /// <param name="matrixBuffer">World matrix buffer with one <see cref="Matrix4x4"/> per landmark instance.</param>
        /// <param name="instanceCount">Visible landmark count stored in <paramref name="matrixBuffer"/>.</param>
        /// <param name="drawBounds">World-space bounds that conservatively cover the published landmarks.</param>
        public void BindInstanceBuffer(GraphicsBuffer matrixBuffer, int instanceCount, Bounds drawBounds)
        {
            _externalMatrixBuffer = matrixBuffer;
            _usingOwnedUploadBuffers = false;
            _instanceCount = Mathf.Max(0, instanceCount);
            _drawBounds = drawBounds;
            _hasBoundsOverride = true;
        }

        /// <summary>
        /// Uploads cartographer-owned landmark bounds into renderer-owned BRG matrix storage.
        /// Accepts <see cref="NativeList{T}.AsArray"/> without managed allocations.
        /// </summary>
        /// <param name="landmarkBounds">Native bounds list published by the cartographer.</param>
        /// <param name="landmarkCount">Valid landmark count.</param>
        public void BindNativeBounds(NativeArray<Bounds> landmarkBounds, int landmarkCount)
        {
            if (!landmarkBounds.IsCreated || landmarkCount <= 0 || landmarkBounds.Length < landmarkCount)
            {
                ClearBinding();
                return;
            }

            EnsureOwnedMatrixUploadCapacity(landmarkCount);
            if (!_uploadedLandmarkMatrices.IsCreated || _uploadedMatrixBuffer == null || !_uploadedLandmarkFade.IsCreated || _uploadedFadeBuffer == null)
            {
                ClearBinding();
                return;
            }

            Bounds combinedBounds = default;
            bool hasCombinedBounds = false;
            for (int i = 0; i < landmarkCount; i++)
            {
                Bounds landmark = landmarkBounds[i];
                Vector3 clampedSize = new Vector3(
                    Mathf.Max(0.5f, landmark.size.x),
                    Mathf.Max(0.5f, landmark.size.y),
                    Mathf.Max(0.5f, landmark.size.z));

                _uploadedLandmarkMatrices[i] = Matrix4x4.TRS(landmark.center, Quaternion.identity, clampedSize);
                _uploadedLandmarkFade[i] = new Vector4(1f, 0f, 0f, 0f);
                if (hasCombinedBounds)
                    combinedBounds.Encapsulate(landmark);
                else
                {
                    combinedBounds = landmark;
                    hasCombinedBounds = true;
                }
            }

            GraphicsBufferUploadUtility.UploadNativeArray(_uploadedMatrixBuffer, _uploadedLandmarkMatrices, landmarkCount);
            GraphicsBufferUploadUtility.UploadNativeArray(_uploadedFadeBuffer, _uploadedLandmarkFade, landmarkCount);
            _externalMatrixBuffer = null;
            _usingOwnedUploadBuffers = true;
            _instanceCount = landmarkCount;
            _drawBounds = hasCombinedBounds
                ? combinedBounds
                : new Bounds(transform.position + _boundsCenterOffset, _boundsSize);
            _hasBoundsOverride = hasCombinedBounds;
        }

        /// <summary>
        /// Uploads bridge-owned HLOD payload into renderer-owned BRG matrix storage without managed allocations.
        /// </summary>
        /// <param name="hlodEntries">Native HLOD registry payload published by the world bridge.</param>
        /// <param name="hlodCount">Valid HLOD entry count.</param>
        public void BindNativeHLOD(NativeArray<HLODData> hlodEntries, int hlodCount)
        {
            if (!hlodEntries.IsCreated || hlodCount <= 0 || hlodEntries.Length < hlodCount)
            {
                ClearBinding();
                return;
            }

            EnsureOwnedMatrixUploadCapacity(hlodCount);
            if (!_uploadedLandmarkMatrices.IsCreated || _uploadedMatrixBuffer == null || !_uploadedLandmarkFade.IsCreated || _uploadedFadeBuffer == null)
            {
                ClearBinding();
                return;
            }

            Bounds combinedBounds = default;
            bool hasCombinedBounds = false;
            for (int i = 0; i < hlodCount; i++)
            {
                HLODData entry = hlodEntries[i];
                Vector3 clampedSize = new Vector3(
                    Mathf.Max(0.5f, entry.Size.x),
                    Mathf.Max(0.5f, entry.Size.y),
                    Mathf.Max(0.5f, entry.Size.z));
                Bounds bounds = new Bounds(entry.Center, clampedSize);
                _uploadedLandmarkMatrices[i] = Matrix4x4.TRS(entry.Center, Quaternion.identity, clampedSize);
                _uploadedLandmarkFade[i] = new Vector4(Mathf.Clamp01(entry.Fade01), 0f, 0f, 0f);
                if (hasCombinedBounds)
                    combinedBounds.Encapsulate(bounds);
                else
                {
                    combinedBounds = bounds;
                    hasCombinedBounds = true;
                }
            }

            GraphicsBufferUploadUtility.UploadNativeArray(_uploadedMatrixBuffer, _uploadedLandmarkMatrices, hlodCount);
            GraphicsBufferUploadUtility.UploadNativeArray(_uploadedFadeBuffer, _uploadedLandmarkFade, hlodCount);
            _externalMatrixBuffer = null;
            _usingOwnedUploadBuffers = true;
            _instanceCount = hlodCount;
            _drawBounds = hasCombinedBounds
                ? combinedBounds
                : new Bounds(transform.position + _boundsCenterOffset, _boundsSize);
            _hasBoundsOverride = hasCombinedBounds;
        }

        public void OnOriginShift(in OriginShiftEventData shiftData)
        {
            if (!isActiveAndEnabled || shiftData.ShiftOffset.sqrMagnitude <= 0.0001f)
                return;

            Vector3 runtimeOffset = -shiftData.ShiftOffset;
            if (_hasBoundsOverride)
            {
                Bounds drawBounds = _drawBounds;
                drawBounds.center += runtimeOffset;
                _drawBounds = drawBounds;
            }

            if (_usingOwnedUploadBuffers &&
                _instanceCount > 0 &&
                _uploadedLandmarkMatrices.IsCreated &&
                _uploadedMatrixBuffer != null)
            {
                int safeCount = Mathf.Min(_instanceCount, _uploadedLandmarkMatrices.Length);
                for (int i = 0; i < safeCount; i++)
                {
                    Matrix4x4 matrix = _uploadedLandmarkMatrices[i];
                    matrix.m03 += runtimeOffset.x;
                    matrix.m13 += runtimeOffset.y;
                    matrix.m23 += runtimeOffset.z;
                    _uploadedLandmarkMatrices[i] = matrix;
                }

                GraphicsBufferUploadUtility.UploadNativeArray(_uploadedMatrixBuffer, _uploadedLandmarkMatrices, safeCount);
            }

            if (_batchRendererGroup != null)
                _batchRendererGroup.SetGlobalBounds(ResolveDrawBounds());
        }

        /// <summary>
        /// Clears the current distant landmark binding and suppresses rendering until a new buffer is published.
        /// </summary>
        public void ClearBinding()
        {
            _externalMatrixBuffer = null;
            _usingOwnedUploadBuffers = false;
            _instanceCount = 0;
            _hasBoundsOverride = false;
        }

        private void RegisterTick()
        {
            if (_isRegistered)
                return;

            GlobalRegistry.RegisterUpdatable(this, PriorityLayer.Environment);
            _isRegistered = true;
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
#if UNITY_EDITOR
            if (_silhouetteShader == null)
                _silhouetteShader = AssetDatabase.LoadAssetAtPath<Shader>(SilhouetteShaderAssetPath);
#endif

            if (_material == null && _runtimeMaterial == null && _silhouetteShader != null)
            {
                _runtimeMaterial = new Material(_silhouetteShader)
                {
                    hideFlags = HideFlags.HideAndDontSave,
                    name = "__HectonDistantLandmarkRuntimeMaterial",
                    enableInstancing = true
                }; // COLD ALLOC: Material[1] - first-party hidden silhouette material - owner: HectonDistantLandmarkRenderer
                _ownsRuntimeMaterial = true;
            }

            if (_batchRendererGroup == null)
            {
                _batchRendererGroup = new BatchRendererGroup(new BatchRendererGroupCreateInfo
                {
                    cullingCallback = OnPerformCulling,
                    userContext = IntPtr.Zero
                });

                _batchMetadata = new NativeArray<MetadataValue>(0, Allocator.Persistent); // COLD ALLOC: NativeArray<MetadataValue>[0] - BRG metadata placeholder for distant landmark renderer - owner: HectonDistantLandmarkRenderer
                _batchHandleBuffer = HectonBatchRendererGroupUtility.CreateBatchHandleBuffer(); // COLD ALLOC: GraphicsBuffer[1] - BRG registration handle buffer for distant landmark renderer - owner: HectonDistantLandmarkRenderer
                _batchId = _batchRendererGroup.AddBatch(_batchMetadata, _batchHandleBuffer.bufferHandle);
                _batchRendererGroup.SetGlobalBounds(ResolveDrawBounds());
            }
        }

        private Material EnsureBrgMaterial()
        {
            Material sourceMaterial = ResolveMaterial();
            if (sourceMaterial == null)
                return null;

            if (_brgMaterial != null && _brgMaterialSource == sourceMaterial)
                return _brgMaterial;

            ReleaseBrgMaterial();
            _brgMaterial = new Material(sourceMaterial)
            {
                hideFlags = HideFlags.HideAndDontSave,
                name = "__HectonDistantLandmarkBrgMaterial",
                enableInstancing = true
            }; // COLD ALLOC: Material[1] - BRG-local distant landmark material clone for per-renderer buffer binding - owner: HectonDistantLandmarkRenderer
            _brgMaterialSource = sourceMaterial;
            _ownsBrgMaterial = true;
            return _brgMaterial;
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

        private void EnsureOwnedMatrixUploadCapacity(int instanceCount)
        {
            int nextCapacity = Mathf.NextPowerOfTwo(Mathf.Max(1, instanceCount));
            if (_uploadedLandmarkMatrices.IsCreated &&
                _uploadedLandmarkMatrices.Length >= nextCapacity &&
                _uploadedMatrixBuffer != null &&
                _uploadedMatrixBuffer.count >= nextCapacity &&
                _uploadedLandmarkFade.IsCreated &&
                _uploadedLandmarkFade.Length >= nextCapacity &&
                _uploadedFadeBuffer != null &&
                _uploadedFadeBuffer.count >= nextCapacity)
                return;

            if (_uploadedLandmarkMatrices.IsCreated)
                _uploadedLandmarkMatrices.Dispose();
            if (_uploadedLandmarkFade.IsCreated)
                _uploadedLandmarkFade.Dispose();

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

            _uploadedLandmarkMatrices = new NativeArray<Matrix4x4>(nextCapacity, Allocator.Persistent, NativeArrayOptions.UninitializedMemory); // COLD ALLOC: NativeArray<Matrix4x4>[NextPowerOfTwo(requiredCount)] - distant landmark native upload cache - owner: HectonDistantLandmarkRenderer
            _uploadedLandmarkFade = new NativeArray<Vector4>(nextCapacity, Allocator.Persistent, NativeArrayOptions.UninitializedMemory); // COLD ALLOC: NativeArray<Vector4>[NextPowerOfTwo(requiredCount)] - distant landmark fade upload cache - owner: HectonDistantLandmarkRenderer
            _uploadedMatrixBuffer = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<Matrix4x4>(nextCapacity); // COLD ALLOC: GraphicsBuffer[NextPowerOfTwo(requiredCount)] - distant landmark matrix upload buffer - owner: HectonDistantLandmarkRenderer
            _uploadedFadeBuffer = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<Vector4>(nextCapacity); // COLD ALLOC: GraphicsBuffer[NextPowerOfTwo(requiredCount)] - distant landmark fade upload buffer - owner: HectonDistantLandmarkRenderer
        }

        private bool TryBindPropertyBuffers()
        {
            if (_usingOwnedUploadBuffers)
            {
                if (_uploadedMatrixBuffer == null)
                    return false;

                _propertyBlock.SetBuffer(LandmarkMatricesId, _uploadedMatrixBuffer);
                if (_uploadedFadeBuffer != null && _uploadedFadeBuffer.count >= _instanceCount)
                    _propertyBlock.SetBuffer(LandmarkFadeId, _uploadedFadeBuffer);
                return true;
            }

            if (_externalMatrixBuffer == null)
                return false;

            _propertyBlock.SetBuffer(LandmarkMatricesId, _externalMatrixBuffer);
            return true;
        }

        private Material ResolveMaterial()
        {
            return _material != null ? _material : _runtimeMaterial;
        }

        private Bounds ResolveDrawBounds()
        {
            if (_hasBoundsOverride)
                return _drawBounds;

            return new Bounds(transform.position + _boundsCenterOffset, _boundsSize);
        }

        private void ReleaseResources()
        {
            _externalMatrixBuffer = null;
            _usingOwnedUploadBuffers = false;

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
            }

            if (_batchHandleBuffer != null)
            {
                _batchHandleBuffer.Release();
                _batchHandleBuffer = null;
            }

            if (_batchMetadata.IsCreated)
                _batchMetadata.Dispose();

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

            if (_uploadedLandmarkMatrices.IsCreated)
                _uploadedLandmarkMatrices.Dispose();
            if (_uploadedLandmarkFade.IsCreated)
                _uploadedLandmarkFade.Dispose();

            if (_ownsRuntimeMaterial && _runtimeMaterial != null)
            {
                if (Application.isPlaying)
                    Destroy(_runtimeMaterial);
                else
                    DestroyImmediate(_runtimeMaterial);
            }

            _runtimeMaterial = null;
            _ownsRuntimeMaterial = false;
            ReleaseBrgMaterial();
        }

        private void ReleaseBrgMaterial()
        {
            if (!_ownsBrgMaterial || _brgMaterial == null)
            {
                _brgMaterial = null;
                _brgMaterialSource = null;
                _ownsBrgMaterial = false;
                return;
            }

            if (Application.isPlaying)
                Destroy(_brgMaterial);
            else
                DestroyImmediate(_brgMaterial);

            _brgMaterial = null;
            _brgMaterialSource = null;
            _ownsBrgMaterial = false;
        }

        private JobHandle OnPerformCulling(
            BatchRendererGroup rendererGroup,
            BatchCullingContext cullingContext,
            BatchCullingOutput cullingOutput,
            IntPtr userContext)
        {
            if (_instanceCount <= 0 ||
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

            bool canCullPerInstance = _usingOwnedUploadBuffers && _uploadedLandmarkMatrices.IsCreated;
            NativeArray<byte> visibilityMask = new NativeArray<byte>(_instanceCount, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            NativeArray<float4> cullingPlanes = default;
            int planeCount = 0;
            if (canCullPerInstance)
            {
                planeCount = cullingContext.cullingPlanes.IsCreated ? cullingContext.cullingPlanes.Length : 0;
                if (planeCount > 0)
                {
                    cullingPlanes = new NativeArray<float4>(planeCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                    for (int planeIndex = 0; planeIndex < planeCount; planeIndex++)
                    {
                        Plane plane = cullingContext.cullingPlanes[planeIndex];
                        cullingPlanes[planeIndex] = new float4(plane.normal.x, plane.normal.y, plane.normal.z, plane.distance);
                    }
                }
            }

            unsafe
            {
                BatchCullingOutputDrawCommands output = HectonBatchRendererGroupUtility.AllocateDirectDrawOutput(_instanceCount, 1, 1);
                JobHandle visibilityHandle = new HectonBatchRendererGroupUtility.BuildMatrixVisibilityMaskJob
                {
                    Matrices = _uploadedLandmarkMatrices,
                    CullingPlanes = cullingPlanes,
                    VisibilityMask = visibilityMask,
                    InstanceCount = _instanceCount,
                    PlaneCount = planeCount,
                    EnableCpuCulling = canCullPerInstance,
                    GlobalOffset = float3.zero,
                    RadiusScale = 1.7321f,
                    MinRadius = 0.5f
                }.Schedule(_instanceCount, 64);

                JobHandle finalizeHandle = new HectonBatchRendererGroupUtility.FinalizeSingleDrawCommandOutputJob
                {
                    VisibilityMask = visibilityMask,
                    InstanceCount = _instanceCount,
                    BatchId = _batchId,
                    MeshId = _batchMeshId,
                    MaterialId = _batchMaterialId,
                    Layer = gameObject.layer,
                    SubMeshIndex = _subMeshIndex,
                    ShadowCastingMode = ShadowCastingMode.Off,
                    ReceiveShadows = false,
                    MotionMode = MotionVectorGenerationMode.Camera,
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
}

