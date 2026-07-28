using Hecton8.Core;
using Hecton8.Core.Memory;
using Unity.Collections;
using System;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace Hecton8.World
{
    /// <summary>
    /// Draws far-field cartographer HLODs through BRG draw commands with per-instance fade.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-89)]
    public sealed class HectonHLODRenderer : MonoBehaviour, ILateFrameTickable, IOriginShiftListener, IGlobalRegistryHotSwapListener
    {
        private const int BrgMetadataPlaceholderCount = 1;
        private const SystemID NativeMemoryOwner = SystemID.GraphicsScalability;

        private static readonly int InstanceMatricesId = Shader.PropertyToID("_HectonHLODInstanceMatrices");
        private static readonly int InstanceFadeId = Shader.PropertyToID("_HectonHLODInstanceFade");
        private static readonly int GlobalFloatingOffsetId = Shader.PropertyToID("_GlobalFloatingOffset");

        [Header("-- Rendering ----------------")]
        [SerializeField]
        [Tooltip("Shared HLOD mesh drawn for every published far-field instance.")]
        private Mesh _mesh;

        [SerializeField]
        [Tooltip("Required authored HLOD material. Runtime material generation is forbidden.")]
        private Material _material;

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
        private Matrix4x4[] _uploadedMatrices;
        private Vector4[] _uploadedFade;
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
        private Vector4 _lastGlobalFloatingOffset = new Vector4(float.NaN, float.NaN, float.NaN, float.NaN);
        private BatchRendererGroup _batchRendererGroup;
        private GraphicsBuffer _batchHandleBuffer;
        private BatchID _batchId;
        private BatchMeshID _batchMeshId;
        private BatchMaterialID _batchMaterialId;
        private Mesh _registeredMesh;
        private Material _registeredMaterial;
        private GraphicsBuffer _registeredBatchBuffer;
        private Bounds _registeredDrawBounds;
        private bool _registeredDrawBoundsValid;
        private bool _hotSwapListenerRegistered;
        private bool _missingDrawAssetsAnnounced;

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
            if (_instanceCount <= 0 || _matrixBuffer == null || _fadeBuffer == null)
                return;

            if (!AreResourcesReady())
                return;

            Mesh activeMesh = _mesh;
            Material activeMaterial = _material;
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
            if (_uploadedMatrices == null || _uploadedFade == null || _uploadedMatrixBuffer == null || _uploadedFadeBuffer == null)
            {
                ClearBinding();
                return;
            }

            Vector4 globalFloatingOffset = ResolveGlobalFloatingOffset();
            Vector3 floatingOffset = new Vector3(globalFloatingOffset.x, globalFloatingOffset.y, globalFloatingOffset.z);
            Bounds combinedBounds = default;
            bool hasCombinedBounds = false;
            int acceptedCount = 0;
            for (int i = 0; i < instanceCount; i++)
            {
                HLODInstance instance = instances[i];
                if (!TryResolveRenderableInstance(in instance, floatingOffset, out Bounds worldBounds))
                    continue;

                _uploadedMatrices[acceptedCount] = instance.LocalToWorld;
                _uploadedFade[acceptedCount] = new Vector4(Sanitize01(instance.Fade01), 0f, 0f, 0f);
                if (hasCombinedBounds)
                    combinedBounds.Encapsulate(worldBounds);
                else
                {
                    combinedBounds = worldBounds;
                    hasCombinedBounds = true;
                }

                acceptedCount++;
            }

            if (acceptedCount <= 0)
            {
                ClearBinding();
                return;
            }

            PublishOwnedUploadBuffers(acceptedCount);
            _instanceCount = acceptedCount;
            _drawBounds = hasCombinedBounds ? combinedBounds : new Bounds(transform.position + _boundsCenterOffset, _boundsSize);
            _hasBoundsOverride = hasCombinedBounds;
        }

        public void OnOriginShift(in OriginShiftEventData shiftData)
        {
            Vector3 shiftOffset = shiftData.ShiftOffset;
            float shiftSqrMagnitude = shiftOffset.sqrMagnitude;
            if (!isActiveAndEnabled ||
                !_hasBoundsOverride ||
                !IsFinite(shiftOffset) ||
                !float.IsFinite(shiftSqrMagnitude) ||
                shiftSqrMagnitude <= 0.0001f)
            {
                return;
            }

            Bounds drawBounds = _drawBounds;
            drawBounds.center -= shiftOffset;
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
        /// first <c>SetBatchGlobalBoundsIfChanged</c> never ran, so <see cref="AreResourcesReady"/> returned
        /// false for the rest of the session and <see cref="LateFrameTick"/> could never recover even after a
        /// mesh or material was assigned in the inspector during play-mode.
        ///
        /// Both fields are optional by construction, which is why the asserts were indefensible: LateFrameTick
        /// null-checks <c>_mesh</c> and <c>_material</c> and returns, <see cref="SyncBatchRegistration"/>
        /// resolves null to a <c>default</c> batch id, and <see cref="OnPerformCulling"/> writes an empty draw
        /// output for a default mesh/material id. A BRG with no registered mesh is already the normal state
        /// between Awake and the first <see cref="BindNativeInstances"/> call, so building it unconditionally
        /// adds no new state. The throw, by contrast, escaped Awake.
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

                _batchHandleBuffer = HectonBatchRendererGroupUtility.CreateBatchHandleBuffer(); // COLD ALLOC: GraphicsBuffer[1] - BRG registration handle buffer for HLOD renderer - owner: HectonHLODRenderer
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
                _registeredDrawBoundsValid = false;
                SetBatchGlobalBoundsIfChanged(ResolveDrawBounds());
            }

            // Report LAST. Everything this component owns is already built above, so a future re-introduced
            // throw here can no longer delete the BRG batch.
            if ((_mesh != null && _material != null) || _missingDrawAssetsAnnounced)
                return;

            _missingDrawAssetsAnnounced = true;
            LogMissingHlodDrawAssets(_mesh != null, _material != null);
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogMissingHlodDrawAssets(bool meshAuthored, bool materialAuthored)
        {
            if (!meshAuthored && !materialAuthored)
            {
                Hecton8.Core.H8Debug.LogError("HectonHLODRenderer: serialized fields '_mesh' and '_material' are both unassigned. LateFrameTick null-guards both and skips the draw, so far-field cartographer HLODs render nothing. The BRG batch, floating-origin listener and LateFrame tick registration all stay live. Runtime mesh/material generation is forbidden - assign the authored HLOD pair in the inspector.");
                return;
            }

            if (!meshAuthored)
            {
                Hecton8.Core.H8Debug.LogError("HectonHLODRenderer: serialized field '_mesh' is unassigned. LateFrameTick null-guards it and skips the draw, so far-field cartographer HLODs render nothing. Every registration stays live. Assign the authored HLOD mesh in the inspector.");
                return;
            }

            Hecton8.Core.H8Debug.LogError("HectonHLODRenderer: serialized field '_material' is unassigned. LateFrameTick null-guards it and skips the draw, so far-field cartographer HLODs render nothing. Every registration stays live. Runtime material generation is forbidden - assign the authored HLOD material in the inspector.");
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

        private void SetBatchGlobalBoundsIfChanged(Bounds bounds)
        {
            if (_batchRendererGroup == null || !IsFiniteBounds(bounds))
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
            if (_uploadedMatrices != null &&
                _uploadedMatrices.Length >= nextCapacity &&
                _uploadedFade != null &&
                _uploadedFade.Length >= nextCapacity &&
                HasUploadCapacity(_uploadedMatrixBufferA, nextCapacity) &&
                HasUploadCapacity(_uploadedMatrixBufferB, nextCapacity) &&
                HasUploadCapacity(_uploadedFadeBufferA, nextCapacity) &&
                HasUploadCapacity(_uploadedFadeBufferB, nextCapacity))
            {
                return;
            }

            _uploadedMatrices = null;
            _uploadedFade = null;
            ReleaseUploadBuffer(ref _uploadedMatrixBufferA);
            ReleaseUploadBuffer(ref _uploadedMatrixBufferB);
            ReleaseUploadBuffer(ref _uploadedFadeBufferA);
            ReleaseUploadBuffer(ref _uploadedFadeBufferB);
            _uploadedMatrixBuffer = null;
            _uploadedFadeBuffer = null;
            _registeredBatchBuffer = null;
            _ownedUploadBufferIndex = 0;

            _uploadedMatrices = new Matrix4x4[nextCapacity]; // COLD ALLOC: Matrix4x4[NextPowerOfTwo(requiredCount)] - HLOD CPU upload cache - owner: HectonHLODRenderer
            _uploadedFade = new Vector4[nextCapacity]; // COLD ALLOC: Vector4[NextPowerOfTwo(requiredCount)] - HLOD fade CPU upload cache - owner: HectonHLODRenderer
            _uploadedMatrixBufferA = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<Matrix4x4>(nextCapacity); // COLD ALLOC: GraphicsBuffer[NextPowerOfTwo(requiredCount)] - HLOD matrix buffer A - owner: HectonHLODRenderer
            _uploadedMatrixBufferB = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<Matrix4x4>(nextCapacity); // COLD ALLOC: GraphicsBuffer[NextPowerOfTwo(requiredCount)] - HLOD matrix buffer B - owner: HectonHLODRenderer
            _uploadedFadeBufferA = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<Vector4>(nextCapacity); // COLD ALLOC: GraphicsBuffer[NextPowerOfTwo(requiredCount)] - HLOD fade buffer A - owner: HectonHLODRenderer
            _uploadedFadeBufferB = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<Vector4>(nextCapacity); // COLD ALLOC: GraphicsBuffer[NextPowerOfTwo(requiredCount)] - HLOD fade buffer B - owner: HectonHLODRenderer
        }

        private void PublishOwnedUploadBuffers(int instanceCount)
        {
            GraphicsBuffer matrixWrite = _ownedUploadBufferIndex == 0 ? _uploadedMatrixBufferA : _uploadedMatrixBufferB;
            GraphicsBuffer fadeWrite = _ownedUploadBufferIndex == 0 ? _uploadedFadeBufferA : _uploadedFadeBufferB;
            if (matrixWrite == null || fadeWrite == null)
                return;

            GraphicsBufferUploadUtility.UploadArray(matrixWrite, _uploadedMatrices, instanceCount);
            GraphicsBufferUploadUtility.UploadArray(fadeWrite, _uploadedFade, instanceCount);
            _uploadedMatrixBuffer = matrixWrite;
            _uploadedFadeBuffer = fadeWrite;
            _matrixBuffer = matrixWrite;
            _fadeBuffer = fadeWrite;
            _ownedUploadBufferIndex ^= 1;
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

            ReleaseUploadBuffer(ref _uploadedMatrixBufferA);
            ReleaseUploadBuffer(ref _uploadedMatrixBufferB);
            ReleaseUploadBuffer(ref _uploadedFadeBufferA);
            ReleaseUploadBuffer(ref _uploadedFadeBufferB);
            _uploadedMatrixBuffer = null;
            _uploadedFadeBuffer = null;
            _registeredBatchBuffer = null;
            _ownedUploadBufferIndex = 0;

            _uploadedMatrices = null;
            _uploadedFade = null;
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
            if (!IsFinite(totalOffset))
                totalOffset = Vector3.zero;

            return new Vector4(totalOffset.x, totalOffset.y, totalOffset.z, 0f);
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

        private static bool TryResolveRenderableInstance(
            in HLODInstance instance,
            Vector3 floatingOffset,
            out Bounds worldBounds)
        {
            worldBounds = default;
            if (!IsFinite(instance.LocalToWorld))
                return false;

            Bounds localBounds = instance.LocalBounds;
            if (!IsFiniteBounds(localBounds) || !float.IsFinite(instance.Fade01))
                return false;

            worldBounds = localBounds;
            worldBounds.center += floatingOffset;
            return IsFiniteBounds(worldBounds);
        }

        private static float Sanitize01(float value)
        {
            return float.IsFinite(value) ? Mathf.Clamp01(value) : 0f;
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

        private static bool IsFinite(Matrix4x4 value)
        {
            return float.IsFinite(value.m00) &&
                   float.IsFinite(value.m01) &&
                   float.IsFinite(value.m02) &&
                   float.IsFinite(value.m03) &&
                   float.IsFinite(value.m10) &&
                   float.IsFinite(value.m11) &&
                   float.IsFinite(value.m12) &&
                   float.IsFinite(value.m13) &&
                   float.IsFinite(value.m20) &&
                   float.IsFinite(value.m21) &&
                   float.IsFinite(value.m22) &&
                   float.IsFinite(value.m23) &&
                   float.IsFinite(value.m30) &&
                   float.IsFinite(value.m31) &&
                   float.IsFinite(value.m32) &&
                   float.IsFinite(value.m33);
        }
    }
}
