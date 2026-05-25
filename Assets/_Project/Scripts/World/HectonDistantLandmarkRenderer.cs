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
    public sealed class HectonDistantLandmarkRenderer : MonoBehaviour, ITickable, IUpdatable, ILateFrameTickable, IOriginShiftListener, IGlobalRegistryHotSwapListener
    {
#if UNITY_EDITOR
        private const string SilhouetteShaderAssetPath = "Assets/_Project/Art/Shaders/Hecton_DistantLandmarkSilhouette.shader";
#endif
        private const int BrgMetadataPlaceholderCount = 1;

        private static readonly int LandmarkMatricesId = Shader.PropertyToID("_HectonLandmarkMatrices");
        private static readonly int LandmarkFadeId = Shader.PropertyToID("_HectonLandmarkInstanceFade");

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
        private Matrix4x4[] _uploadedLandmarkMatrices;
        private Vector4[] _uploadedLandmarkFade;
        private GraphicsBuffer _uploadedMatrixBuffer;
        private GraphicsBuffer _uploadedFadeBuffer;
        private GraphicsBuffer _uploadedMatrixBufferA;
        private GraphicsBuffer _uploadedMatrixBufferB;
        private GraphicsBuffer _uploadedFadeBufferA;
        private GraphicsBuffer _uploadedFadeBufferB;
        private int _ownedUploadBufferIndex;
        private Bounds _drawBounds;
        private int _instanceCount;
        private bool _hasBoundsOverride;
        private bool _isRegistered;
        private bool _usingOwnedUploadBuffers;
        private BatchRendererGroup _batchRendererGroup;
        private GraphicsBuffer _batchHandleBuffer;
        private BatchID _batchId;
        private BatchMeshID _batchMeshId;
        private BatchMaterialID _batchMaterialId;
        private Mesh _registeredMesh;
        private Material _registeredMaterial;
        private GraphicsBuffer _registeredBatchBuffer;
        private bool _hotSwapListenerRegistered;

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
            _drawBounds = new Bounds(transform.position + _boundsCenterOffset, _boundsSize);
            EnsureResources();
        }

        private void OnEnable()
        {
            HectonFloatingOrigin.RegisterListener(this);
            TryRegisterHotSwapListener();
            RegisterTick();
        }

        private void OnDisable()
        {
            HectonFloatingOrigin.UnregisterListener(this);
            UnregisterTick();
            TryUnregisterHotSwapListener();
        }

        private void OnDestroy()
        {
            HectonFloatingOrigin.UnregisterListener(this);
            UnregisterTick();
            TryUnregisterHotSwapListener();
            ReleaseResources();
        }

        /// <inheritdoc />
        public void Tick(float dt)
        {
        }

        public void LateFrameTick()
        {
            if (_instanceCount <= 0)
                return;

            EnsureResources();
            if (_batchRendererGroup == null || _batchId.Equals(default))
                return;

            Mesh activeMesh = _mesh;
            Material activeMaterial = ResolveMaterial();
            if (activeMesh == null || activeMaterial == null)
                return;

            if (_usingOwnedUploadBuffers)
            {
                if (_uploadedMatrixBuffer == null)
                    return;

                Shader.SetGlobalBuffer(LandmarkMatricesId, _uploadedMatrixBuffer);
                if (_uploadedFadeBuffer != null)
                    Shader.SetGlobalBuffer(LandmarkFadeId, _uploadedFadeBuffer);
                SyncBatchBuffer(_uploadedMatrixBuffer);
            }
            else
            {
                if (_externalMatrixBuffer == null)
                    return;

                Shader.SetGlobalBuffer(LandmarkMatricesId, _externalMatrixBuffer);
                SyncBatchBuffer(_externalMatrixBuffer);
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
            if (_uploadedLandmarkMatrices == null || _uploadedMatrixBuffer == null || _uploadedLandmarkFade == null || _uploadedFadeBuffer == null)
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

            PublishOwnedUploadBuffers(landmarkCount);
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
            if (_uploadedLandmarkMatrices == null || _uploadedMatrixBuffer == null || _uploadedLandmarkFade == null || _uploadedFadeBuffer == null)
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

            PublishOwnedUploadBuffers(hlodCount);
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
                _uploadedLandmarkMatrices != null &&
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

                PublishOwnedUploadBuffers(safeCount);
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
            if (_isRegistered || !Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            _isRegistered = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Environment);
        }

        private void UnregisterTick()
        {
            if (!_isRegistered)
                return;

            GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Environment);
            _isRegistered = false;
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot == GlobalRegistryServiceSlot.Dispatcher && currentService != null && isActiveAndEnabled)
                RegisterTick();
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

        private void EnsureResources()
        {
            if (_batchRendererGroup == null)
            {
                _batchRendererGroup = new BatchRendererGroup(new BatchRendererGroupCreateInfo
                {
                    cullingCallback = OnPerformCulling,
                    userContext = IntPtr.Zero
                });

                _batchHandleBuffer = HectonBatchRendererGroupUtility.CreateBatchHandleBuffer(); // COLD ALLOC: GraphicsBuffer[1] - BRG registration handle buffer for distant landmark renderer - owner: HectonDistantLandmarkRenderer
                using (NativeArray<MetadataValue> batchMetadata = new NativeArray<MetadataValue>(BrgMetadataPlaceholderCount, Allocator.Temp, NativeArrayOptions.ClearMemory))
                {
                    _batchId = _batchRendererGroup.AddBatch(batchMetadata, _batchHandleBuffer.bufferHandle);
                }
                _batchRendererGroup.SetGlobalBounds(ResolveDrawBounds());
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

        private void EnsureOwnedMatrixUploadCapacity(int instanceCount)
        {
            int nextCapacity = Mathf.NextPowerOfTwo(Mathf.Max(1, instanceCount));
            if (_uploadedLandmarkMatrices != null &&
                _uploadedLandmarkMatrices.Length >= nextCapacity &&
                HasUploadCapacity(_uploadedMatrixBufferA, nextCapacity) &&
                HasUploadCapacity(_uploadedMatrixBufferB, nextCapacity) &&
                _uploadedLandmarkFade != null &&
                _uploadedLandmarkFade.Length >= nextCapacity &&
                HasUploadCapacity(_uploadedFadeBufferA, nextCapacity) &&
                HasUploadCapacity(_uploadedFadeBufferB, nextCapacity))
                return;

            _uploadedLandmarkMatrices = null;
            _uploadedLandmarkFade = null;
            ReleaseUploadBuffer(ref _uploadedMatrixBufferA);
            ReleaseUploadBuffer(ref _uploadedMatrixBufferB);
            ReleaseUploadBuffer(ref _uploadedFadeBufferA);
            ReleaseUploadBuffer(ref _uploadedFadeBufferB);
            _uploadedMatrixBuffer = null;
            _uploadedFadeBuffer = null;
            _registeredBatchBuffer = null;
            _ownedUploadBufferIndex = 0;

            _uploadedLandmarkMatrices = new Matrix4x4[nextCapacity]; // COLD ALLOC: Matrix4x4[NextPowerOfTwo(requiredCount)] - distant landmark CPU upload cache - owner: HectonDistantLandmarkRenderer
            _uploadedLandmarkFade = new Vector4[nextCapacity]; // COLD ALLOC: Vector4[NextPowerOfTwo(requiredCount)] - distant landmark fade CPU upload cache - owner: HectonDistantLandmarkRenderer
            _uploadedMatrixBufferA = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<Matrix4x4>(nextCapacity); // COLD ALLOC: GraphicsBuffer[NextPowerOfTwo(requiredCount)] - distant landmark matrix upload buffer A - owner: HectonDistantLandmarkRenderer
            _uploadedMatrixBufferB = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<Matrix4x4>(nextCapacity); // COLD ALLOC: GraphicsBuffer[NextPowerOfTwo(requiredCount)] - distant landmark matrix upload buffer B - owner: HectonDistantLandmarkRenderer
            _uploadedFadeBufferA = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<Vector4>(nextCapacity); // COLD ALLOC: GraphicsBuffer[NextPowerOfTwo(requiredCount)] - distant landmark fade upload buffer A - owner: HectonDistantLandmarkRenderer
            _uploadedFadeBufferB = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<Vector4>(nextCapacity); // COLD ALLOC: GraphicsBuffer[NextPowerOfTwo(requiredCount)] - distant landmark fade upload buffer B - owner: HectonDistantLandmarkRenderer
        }

        private void PublishOwnedUploadBuffers(int instanceCount)
        {
            GraphicsBuffer matrixWrite = _ownedUploadBufferIndex == 0 ? _uploadedMatrixBufferA : _uploadedMatrixBufferB;
            GraphicsBuffer fadeWrite = _ownedUploadBufferIndex == 0 ? _uploadedFadeBufferA : _uploadedFadeBufferB;
            if (matrixWrite == null || fadeWrite == null)
                return;

            GraphicsBufferUploadUtility.UploadArray(matrixWrite, _uploadedLandmarkMatrices, instanceCount);
            GraphicsBufferUploadUtility.UploadArray(fadeWrite, _uploadedLandmarkFade, instanceCount);
            _uploadedMatrixBuffer = matrixWrite;
            _uploadedFadeBuffer = fadeWrite;
            _ownedUploadBufferIndex ^= 1;
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
                _registeredBatchBuffer = null;
            }

            if (_batchHandleBuffer != null)
            {
                _batchHandleBuffer.Release();
                _batchHandleBuffer = null;
            }

            ReleaseUploadBuffer(ref _uploadedMatrixBufferA);
            ReleaseUploadBuffer(ref _uploadedMatrixBufferB);
            ReleaseUploadBuffer(ref _uploadedFadeBufferA);
            ReleaseUploadBuffer(ref _uploadedFadeBufferB);
            _uploadedMatrixBuffer = null;
            _uploadedFadeBuffer = null;
            _registeredBatchBuffer = null;
            _ownedUploadBufferIndex = 0;

            _uploadedLandmarkMatrices = null;
            _uploadedLandmarkFade = null;
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

        private static bool HasUploadCapacity(GraphicsBuffer buffer, int requiredCapacity)
        {
            return buffer != null && buffer.IsValid() && buffer.count >= requiredCapacity;
        }

        private static void ReleaseUploadBuffer(ref GraphicsBuffer buffer)
        {
            if (buffer == null)
                return;

            buffer.Release();
            buffer = null;
        }
    }
}

