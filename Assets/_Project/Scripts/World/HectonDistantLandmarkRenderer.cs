using Hecton8.Core;
using Hecton8.Core.Memory;
using System;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace Hecton8.World
{
    /// <summary>
    /// Draws distant landmark silhouettes through BRG draw commands using an externally owned matrix buffer.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-90)]
    public sealed class HectonDistantLandmarkRenderer : MonoBehaviour, ILateFrameTickable, IOriginShiftListener, IGlobalRegistryHotSwapListener
    {
        private const int BrgMetadataPlaceholderCount = 1;
        private const SystemID NativeMemoryOwner = SystemID.GraphicsScalability;

        private static readonly int LandmarkMatricesId = Shader.PropertyToID("_HectonLandmarkMatrices");
        private static readonly int LandmarkFadeId = Shader.PropertyToID("_HectonLandmarkInstanceFade");

        [Header("-- Rendering ----------------")]
        [SerializeField]
        [Tooltip("Shared mesh drawn for each distant landmark instance.")]
        private Mesh _mesh;

        [SerializeField]
        [Tooltip("Required authored material used for the silhouette-only BRG draw. Runtime material generation is forbidden.")]
        private Material _material;

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
        private bool _missingDrawAssetsAnnounced;

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
        public void LateFrameTick()
        {
            if (_instanceCount <= 0)
                return;

            if (!AreResourcesReady())
                return;

            Mesh activeMesh = _mesh;
            Material activeMaterial = _material;
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
            if (matrixBuffer == null || !matrixBuffer.IsValid() || instanceCount <= 0 || !IsFiniteBounds(drawBounds))
            {
                ClearBinding();
                return;
            }

            _externalMatrixBuffer = matrixBuffer;
            _usingOwnedUploadBuffers = false;
            _instanceCount = Mathf.Min(instanceCount, matrixBuffer.count);
            if (_instanceCount <= 0)
            {
                ClearBinding();
                return;
            }

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

            int validCount = 0;
            Bounds combinedBounds = default;
            bool hasCombinedBounds = false;
            for (int i = 0; i < landmarkCount; i++)
            {
                Bounds landmark = landmarkBounds[i];
                if (!IsFiniteBounds(landmark))
                    continue;

                Vector3 clampedSize = new Vector3(
                    Mathf.Max(0.5f, landmark.size.x),
                    Mathf.Max(0.5f, landmark.size.y),
                    Mathf.Max(0.5f, landmark.size.z));

                Bounds safeBounds = new Bounds(landmark.center, clampedSize);
                _uploadedLandmarkMatrices[validCount] = Matrix4x4.TRS(landmark.center, Quaternion.identity, clampedSize);
                _uploadedLandmarkFade[validCount] = new Vector4(1f, 0f, 0f, 0f);
                if (hasCombinedBounds)
                    combinedBounds.Encapsulate(safeBounds);
                else
                {
                    combinedBounds = safeBounds;
                    hasCombinedBounds = true;
                }

                validCount++;
            }

            if (validCount <= 0)
            {
                ClearBinding();
                return;
            }

            PublishOwnedUploadBuffers(validCount);
            _externalMatrixBuffer = null;
            _usingOwnedUploadBuffers = true;
            _instanceCount = validCount;
            _drawBounds = combinedBounds;
            _hasBoundsOverride = true;
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

            int validCount = 0;
            Bounds combinedBounds = default;
            bool hasCombinedBounds = false;
            for (int i = 0; i < hlodCount; i++)
            {
                HLODData entry = hlodEntries[i];
                if (!IsFinite(entry.Center) || !IsFinite(entry.Size) || !float.IsFinite(entry.Fade01))
                    continue;

                Vector3 clampedSize = new Vector3(
                    Mathf.Max(0.5f, entry.Size.x),
                    Mathf.Max(0.5f, entry.Size.y),
                    Mathf.Max(0.5f, entry.Size.z));
                Bounds bounds = new Bounds(entry.Center, clampedSize);
                if (!IsFiniteBounds(bounds))
                    continue;

                _uploadedLandmarkMatrices[validCount] = Matrix4x4.TRS(entry.Center, Quaternion.identity, clampedSize);
                _uploadedLandmarkFade[validCount] = new Vector4(Mathf.Clamp01(entry.Fade01), 0f, 0f, 0f);
                if (hasCombinedBounds)
                    combinedBounds.Encapsulate(bounds);
                else
                {
                    combinedBounds = bounds;
                    hasCombinedBounds = true;
                }

                validCount++;
            }

            if (validCount <= 0)
            {
                ClearBinding();
                return;
            }

            PublishOwnedUploadBuffers(validCount);
            _externalMatrixBuffer = null;
            _usingOwnedUploadBuffers = true;
            _instanceCount = validCount;
            _drawBounds = combinedBounds;
            _hasBoundsOverride = true;
        }

        public void OnOriginShift(in OriginShiftEventData shiftData)
        {
            Vector3 shiftOffset = shiftData.ShiftOffset;
            float shiftSqrMagnitude = shiftOffset.sqrMagnitude;
            if (!isActiveAndEnabled ||
                !IsFinite(shiftOffset) ||
                !float.IsFinite(shiftSqrMagnitude) ||
                shiftSqrMagnitude <= 0.0001f)
            {
                return;
            }

            Vector3 runtimeOffset = -shiftOffset;
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
            if (serviceSlot != GlobalRegistryServiceSlot.Dispatcher)
                return;

            UnregisterTick();
            if (currentService != null && isActiveAndEnabled)
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

        /// <summary>
        /// Creates the BRG batch once and reports unassigned draw assets without throwing.
        /// </summary>
        /// <remarks>
        /// <c>UnityEngine.Assertions.Assert</c> THROWS in this project - nothing under Assets sets
        /// <c>Assert.raiseExceptions = false</c> - so the two asserts that used to open this method made every
        /// statement below them unreachable whenever a serialized slot was empty. That cost the whole BRG
        /// object graph: <c>new BatchRendererGroup</c>, <c>CreateBatchHandleBuffer</c>, <c>AddBatch</c> and the
        /// first <c>SetGlobalBounds</c> never ran, so <see cref="AreResourcesReady"/> returned false for the
        /// rest of the session and <see cref="LateFrameTick"/> could never recover even after a mesh or
        /// material was assigned in the inspector during play-mode.
        ///
        /// Both fields are optional by construction, which is why the asserts were indefensible: LateFrameTick
        /// null-checks <c>_mesh</c> and <c>_material</c> and returns, <see cref="SyncBatchRegistration"/>
        /// resolves null to a <c>default</c> batch id, and <see cref="OnPerformCulling"/> writes an empty draw
        /// output for a default mesh/material id. A BRG with no registered mesh is already the normal state
        /// between Awake and the first bind call, so building it unconditionally adds no new state. The throw,
        /// by contrast, escaped Awake.
        /// </remarks>
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
                NativeArray<MetadataValue> batchMetadata = H8Memory.Allocate<MetadataValue>(
                    BrgMetadataPlaceholderCount,
                    NativeMemoryOwner,
                    Allocator.Temp,
                    NativeArrayOptions.ClearMemory);
                try
                {
                    if (!batchMetadata.IsCreated)
                    {
                        ReleaseResources();
                        return;
                    }

                    _batchId = _batchRendererGroup.AddBatch(batchMetadata, _batchHandleBuffer.bufferHandle);
                }
                finally
                {
                    if (batchMetadata.IsCreated)
                        H8Memory.Release(ref batchMetadata, NativeMemoryOwner);
                }
                _batchRendererGroup.SetGlobalBounds(ResolveDrawBounds());
            }

            // Report LAST. Everything this component owns is already built above, so a future re-introduced
            // throw here can no longer delete the BRG batch.
            if ((_mesh != null && _material != null) || _missingDrawAssetsAnnounced)
                return;

            _missingDrawAssetsAnnounced = true;
            LogMissingLandmarkDrawAssets(_mesh != null, _material != null);
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogMissingLandmarkDrawAssets(bool meshAuthored, bool materialAuthored)
        {
            if (!meshAuthored && !materialAuthored)
            {
                Hecton8.Core.H8Debug.LogError("HectonDistantLandmarkRenderer: serialized fields '_mesh' and '_material' are both unassigned. LateFrameTick null-guards both and skips the draw, so distant landmark silhouettes render nothing. The BRG batch, floating-origin listener and LateFrame tick registration all stay live. Runtime mesh/material generation is forbidden - assign the authored landmark pair in the inspector.");
                return;
            }

            if (!meshAuthored)
            {
                Hecton8.Core.H8Debug.LogError("HectonDistantLandmarkRenderer: serialized field '_mesh' is unassigned. LateFrameTick null-guards it and skips the draw, so distant landmark silhouettes render nothing. Every registration stays live. Assign the authored landmark mesh in the inspector.");
                return;
            }

            Hecton8.Core.H8Debug.LogError("HectonDistantLandmarkRenderer: serialized field '_material' is unassigned. LateFrameTick null-guards it and skips the draw, so distant landmark silhouettes render nothing. Every registration stays live. Runtime material generation is forbidden - assign the authored silhouette material in the inspector.");
        }

        private bool AreResourcesReady()
        {
            return _batchRendererGroup != null &&
                   !_batchId.Equals(default) &&
                   _batchHandleBuffer != null;
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

        private Bounds ResolveDrawBounds()
        {
            if (_hasBoundsOverride)
                return IsFiniteBounds(_drawBounds) ? _drawBounds : new Bounds(transform.position + _boundsCenterOffset, _boundsSize);

            Bounds fallbackBounds = new Bounds(transform.position + _boundsCenterOffset, _boundsSize);
            return IsFiniteBounds(fallbackBounds) ? fallbackBounds : new Bounds(transform.position, Vector3.one);
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

        private static bool IsFiniteBounds(Bounds bounds)
        {
            Vector3 extents = bounds.extents;
            return IsFinite(bounds.center) &&
                   IsFinite(extents) &&
                   extents.x >= 0f &&
                   extents.y >= 0f &&
                   extents.z >= 0f;
        }

        private static bool IsFinite(Vector3 value)
        {
            return float.IsFinite(value.x) &&
                   float.IsFinite(value.y) &&
                   float.IsFinite(value.z);
        }
    }
}

