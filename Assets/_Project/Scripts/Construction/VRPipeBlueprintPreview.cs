using Hecton8.Core;
using Hecton8.Core.Memory;
using Hecton8.World;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Hecton8.Construction
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/Construction/VR Pipe Blueprint Preview")]
    public sealed class VRPipeBlueprintPreview : MonoBehaviour, ILateFrameTickable, IRenderable, IGlobalRegistryHotSwapListener
    {
        private const int ControlPointCount = 4;
        private const int MaxPreviewInstances = 64;
        private const uint StateMatricesDirty = 1u << 0;
        private const float PointDirtyDistanceSq = 0.000025f;
        private const string HologramShaderPath = "Assets/_Project/Shaders/Hecton_ConstructionDearLieHologram.shader";
        private const uint PipePreviewHash = 0x56525050u;
        private const BufferID PipeStateBufferId = (BufferID)70946;
        private const BufferID PipeVisualBufferId = (BufferID)70947;
        private const BufferID PipeIndirectArgsBufferId = (BufferID)70948;

        [Header("Preview")]
        [SerializeField] private Material previewMaterial;
        [SerializeField] private Shader previewShader;
        [SerializeField] private bool previewActive;
        [SerializeField, Min(0.01f)] private float segmentLengthMeters = 0.35f;
        [SerializeField, Min(0.001f)] private float segmentRadiusMeters = 0.035f;
        [SerializeField] private Camera targetCamera;
        [SerializeField] private Color validColor = new Color(0.08f, 1f, 0.72f, 0.70f);
        [SerializeField] private Color invalidColor = new Color(1f, 0.18f, 0.12f, 0.78f);

        [Header("Control Points")]
        [SerializeField] private Transform point0;
        [SerializeField] private Transform point1;
        [SerializeField] private Transform point2;
        [SerializeField] private Transform point3;

        private AbsoluteUniversePosition _runtimePointAup0;
        private AbsoluteUniversePosition _runtimePointAup1;
        private AbsoluteUniversePosition _runtimePointAup2;
        private AbsoluteUniversePosition _runtimePointAup3;
        private bool _hasRuntimePoint0;
        private bool _hasRuntimePoint1;
        private bool _hasRuntimePoint2;
        private bool _hasRuntimePoint3;
        private VaultGenerationHandle<BuilderGhostStateDTO> _stateHandle;
        private VaultGenerationHandle<BuilderGhostVisualDTO> _visualHandle;
        private VaultGenerationHandle<BuilderGhostIndirectArgsDTO> _argsHandle;
        private IDataVault _vault;
        private IDataVault _pendingBuildWriteLockVault;
        private GraphicsBuffer _stateBufferA;
        private GraphicsBuffer _stateBufferB;
        private GraphicsBuffer _visualBufferA;
        private GraphicsBuffer _visualBufferB;
        private GraphicsBuffer _argsBufferA;
        private GraphicsBuffer _argsBufferB;
        private Bounds _drawBounds = new Bounds(Vector3.zero, new Vector3(8f, 8f, 8f));
        private Vector3 _cachedPoint0;
        private Vector3 _cachedPoint1;
        private Vector3 _cachedPoint2;
        private Vector3 _cachedPoint3;
        private float _cachedSegmentLengthMeters;
        private float _cachedSegmentRadiusMeters;
        private JobHandle _pendingBuildHandle;
        private uint _stateFlags = StateMatricesDirty;
        private bool _registeredLateFrame;
        private bool _registeredRenderable;
        private bool _registeredHotSwap;
        private bool _pendingBuildScheduled;
        private bool _pendingBuildDiscard;
        private bool _drawBoundsValid;
        private int _pendingBuildWriteLockCount;
        private uint _previewFrameCounter;
        private Transform _cachedTransform;
        private int _uploadedCount;
        private int _writeBufferIndex;
        private GraphicsBuffer _boundStateBuffer;
        private GraphicsBuffer _boundVisualBuffer;
        private HectonXRRuntimeState.XRActiveChangedHandler _xrActiveChangedHandler;

        private static readonly int BuilderGhostStatesId = Shader.PropertyToID("_H8BuilderGhostStates");
        private static readonly int BuilderGhostVisualsId = Shader.PropertyToID("_H8BuilderGhostVisuals");

        public bool PreviewActive
        {
            get => previewActive;
            set
            {
                if (previewActive == value)
                    return;

                previewActive = value;
                _stateFlags |= StateMatricesDirty;
                if (!previewActive)
                    ClearPreparedPreview();
            }
        }

        private void Awake()
        {
            EnsureXrActiveChangedHandlerCold();
            CacheRuntimeReferences();
            EnsureBuffersCold();
            EnsureMaterial();
            EnsureGraphicsBuffers();
        }

        private void OnEnable()
        {
            EnsureXrActiveChangedHandlerCold();
            CacheRuntimeReferences();
            HectonXRRuntimeState.XRActiveChanged -= _xrActiveChangedHandler;
            HectonXRRuntimeState.XRActiveChanged += _xrActiveChangedHandler;
            TryRegisterHotSwapListener();
            EnsureBuffersCold();
            EnsureMaterial();
            EnsureGraphicsBuffers();
            RefreshXRRegistration();
        }

        private void OnDisable()
        {
            if (_xrActiveChangedHandler != null)
                HectonXRRuntimeState.XRActiveChanged -= _xrActiveChangedHandler;
            TryUnregisterHotSwapListener();
            TryUnregisterRuntime();
            CompletePendingBuildForTeardown();
            ClearPreparedPreview();
            ClearVaultDescriptorState();
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (!isActiveAndEnabled)
                return;

            if (serviceSlot == GlobalRegistryServiceSlot.Dispatcher && currentService != null)
            {
                if (_registeredLateFrame)
                {
                    GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Player);
                    _registeredLateFrame = false;
                }

                TryRegisterRuntime();
                return;
            }

            if (serviceSlot == GlobalRegistryServiceSlot.DataVault)
            {
                CompletePendingBuildForTeardown();
                ClearVaultDescriptorState();
                _vault = currentService as IDataVault;
                EnsureBuffersCold();
            }
        }

        private void TryRegisterHotSwapListener()
        {
            if (_registeredHotSwap || !Application.isPlaying)
                return;

            _registeredHotSwap = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterHotSwapListener()
        {
            if (!_registeredHotSwap)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _registeredHotSwap = false;
        }

        private void ClearVaultDescriptorState()
        {
            _vault = null;
            _stateHandle = default;
            _visualHandle = default;
            _argsHandle = default;
            _stateFlags |= StateMatricesDirty;
        }

        private void OnDestroy()
        {
            CompletePendingBuildForTeardown();
            ReleaseGraphicsBuffer(ref _stateBufferA);
            ReleaseGraphicsBuffer(ref _stateBufferB);
            ReleaseGraphicsBuffer(ref _visualBufferA);
            ReleaseGraphicsBuffer(ref _visualBufferB);
            ReleaseGraphicsBuffer(ref _argsBufferA);
            ReleaseGraphicsBuffer(ref _argsBufferB);
            ClearVaultDescriptorState();
            _boundStateBuffer = null;
            _boundVisualBuffer = null;

            if (previewMaterial != null && previewMaterial.hideFlags == HideFlags.DontSave)
                Destroy(previewMaterial);
        }

        private void EnsureXrActiveChangedHandlerCold()
        {
            if (_xrActiveChangedHandler != null)
                return;

            _xrActiveChangedHandler = HandleXRActiveChanged;
        }

        public void Render(float deltaTime)
        {
            DrawPreparedPreview();
        }

        public void LateFrameTick()
        {
            TryFinalizePendingBuildAndUpload();
            if (!HectonXRRuntimeState.IsXRActive || !previewActive || previewMaterial == null)
                return;

            if (ShouldRebuildPreview())
                SchedulePreviewBuild();
        }

        public void SetPreviewPoint(int index, Vector3 runtimePosition)
        {
            if ((uint)index >= ControlPointCount)
                return;

            if (!TryResolveAupFromRuntimeOrigin(runtimePosition, out AbsoluteUniversePosition runtimeAup))
                return;

            if (!TryGetRuntimePointAup(index, out AbsoluteUniversePosition previousAup) ||
                AbsoluteUniversePosition.DistanceSq(in previousAup, in runtimeAup) > PointDirtyDistanceSq)
            {
                _stateFlags |= StateMatricesDirty;
            }

            SetRuntimePointAup(index, in runtimeAup);
        }

        public void ClearRuntimePoints()
        {
            bool hadRuntimePoint = _hasRuntimePoint0 ||
                                   _hasRuntimePoint1 ||
                                   _hasRuntimePoint2 ||
                                   _hasRuntimePoint3;

            _hasRuntimePoint0 = false;
            _hasRuntimePoint1 = false;
            _hasRuntimePoint2 = false;
            _hasRuntimePoint3 = false;

            if (hadRuntimePoint)
                _stateFlags |= StateMatricesDirty;
        }

        private bool ShouldRebuildPreview()
        {
            if (_pendingBuildScheduled)
                return false;

            Vector3 p0 = ResolvePointRuntime(0, point0);
            Vector3 p1 = ResolvePointRuntime(1, point1);
            Vector3 p2 = ResolvePointRuntime(2, point2);
            Vector3 p3 = ResolvePointRuntime(3, point3);

            if ((_stateFlags & StateMatricesDirty) != 0u ||
                (_cachedPoint0 - p0).sqrMagnitude > PointDirtyDistanceSq ||
                (_cachedPoint1 - p1).sqrMagnitude > PointDirtyDistanceSq ||
                (_cachedPoint2 - p2).sqrMagnitude > PointDirtyDistanceSq ||
                (_cachedPoint3 - p3).sqrMagnitude > PointDirtyDistanceSq ||
                math.abs(_cachedSegmentLengthMeters - segmentLengthMeters) > 0.0001f ||
                math.abs(_cachedSegmentRadiusMeters - segmentRadiusMeters) > 0.0001f)
            {
                _cachedPoint0 = p0;
                _cachedPoint1 = p1;
                _cachedPoint2 = p2;
                _cachedPoint3 = p3;
                _cachedSegmentLengthMeters = segmentLengthMeters;
                _cachedSegmentRadiusMeters = segmentRadiusMeters;
                return true;
            }

            return false;
        }

        private void SchedulePreviewBuild()
        {
            if (_pendingBuildScheduled ||
                !TryAcquirePreviewWriteBuffers(
                    out NativeArray<BuilderGhostStateDTO> states,
                    out NativeArray<BuilderGhostVisualDTO> visuals,
                    out NativeArray<BuilderGhostIndirectArgsDTO> args,
                    out IDataVault vault))
            {
                return;
            }

            if (!TryResolveRuntimeOriginAup(out double3 runtimeOriginAup))
            {
                ReleasePreviewWriteLocks(vault, 3);
                return;
            }

            float quality = ShinobuSocketConstructionRuntime.ResolveGlobalQualityWeight();
            BuildPipeBlueprintPreviewJob job = new BuildPipeBlueprintPreviewJob
            {
                States = states,
                Visuals = visuals,
                Args = args,
                Point0Aup = ResolvePointAup(0, point0),
                Point1Aup = ResolvePointAup(1, point1),
                Point2Aup = ResolvePointAup(2, point2),
                Point3Aup = ResolvePointAup(3, point3),
                RuntimeOriginAup = runtimeOriginAup,
                SegmentLengthMeters = segmentLengthMeters,
                SegmentRadiusMeters = segmentRadiusMeters,
                GlobalQualityWeight = quality,
                DearLieDampen = math.lerp(0.05f, 0.22f, ShinobuSocketConstructionRuntime.SmoothQuality(quality)),
                DearLieWiggleSpeed = math.lerp(8f, 22f, ShinobuSocketConstructionRuntime.SmoothQuality(quality)),
                ValidColor = new float4(validColor.r, validColor.g, validColor.b, validColor.a),
                InvalidColor = new float4(invalidColor.r, invalidColor.g, invalidColor.b, invalidColor.a),
                PrefabHashID = PipePreviewHash,
                Frame = CapturePreviewFrameId(),
                MaxSegments = MaxPreviewInstances
            };

            bool scheduled = false;
            try
            {
                JobHandle pendingHandle = job.Schedule();
                _pendingBuildHandle = pendingHandle;
                _pendingBuildWriteLockVault = vault;
                _pendingBuildWriteLockCount = 3;
                _pendingBuildScheduled = true;
                _pendingBuildDiscard = false;
                _stateFlags &= ~StateMatricesDirty;
                scheduled = true;
            }
            finally
            {
                if (!scheduled)
                    ReleasePreviewWriteLocks(vault, 3);
            }
        }

        private bool TryFinalizePendingBuildAndUpload()
        {
            if (!_pendingBuildScheduled)
                return false;

            if (!DispatcherJobFence.TryFinalizeCompleted(ref _pendingBuildHandle))
                return false;

            _pendingBuildScheduled = false;
            try
            {
                if (!TryReadLockedPreviewBuffers(
                        out NativeArray<BuilderGhostStateDTO> states,
                        out NativeArray<BuilderGhostVisualDTO> visuals,
                        out NativeArray<BuilderGhostIndirectArgsDTO> args))
                {
                    ClearPreparedPreview();
                    return false;
                }

                if (!HasGraphicsBuffers())
                {
                    ClearPreparedPreview();
                    return false;
                }

                int uploadCount = _pendingBuildDiscard || args.Length <= 0
                    ? 0
                    : math.clamp((int)args[0].InstanceCount, 0, math.min(states.Length, visuals.Length));
                _pendingBuildDiscard = false;
                UpdateDrawBoundsFromStates(states, uploadCount);
                if (!_drawBoundsValid || uploadCount <= 0)
                {
                    _uploadedCount = 0;
                    return true;
                }

                GraphicsBufferUploadUtility.UploadNativeArray(ResolveWriteStateBuffer(), states, uploadCount);
                GraphicsBufferUploadUtility.UploadNativeArray(ResolveWriteVisualBuffer(), visuals, uploadCount);
                GraphicsBufferUploadUtility.UploadNativeArray(ResolveWriteArgsBuffer(), args, 1);
                _uploadedCount = uploadCount;
                _writeBufferIndex ^= 1;
                return true;
            }
            finally
            {
                ReleasePendingPreviewWriteLocks();
            }
        }

        private void DrawPreparedPreview()
        {
            if (_uploadedCount <= 0 || previewMaterial == null || !_drawBoundsValid || !IsDrawBoundsCameraVisible())
                return;

            GraphicsBuffer stateBuffer = _writeBufferIndex == 0 ? _stateBufferB : _stateBufferA;
            GraphicsBuffer visualBuffer = _writeBufferIndex == 0 ? _visualBufferB : _visualBufferA;
            GraphicsBuffer argsBuffer = _writeBufferIndex == 0 ? _argsBufferB : _argsBufferA;
            if (stateBuffer == null || visualBuffer == null || argsBuffer == null)
                return;

            if (!ReferenceEquals(_boundStateBuffer, stateBuffer))
            {
                previewMaterial.SetBuffer(BuilderGhostStatesId, stateBuffer);
                _boundStateBuffer = stateBuffer;
            }

            if (!ReferenceEquals(_boundVisualBuffer, visualBuffer))
            {
                previewMaterial.SetBuffer(BuilderGhostVisualsId, visualBuffer);
                _boundVisualBuffer = visualBuffer;
            }

            UnityEngine.Graphics.DrawProceduralIndirect(
                previewMaterial,
                _drawBounds,
                MeshTopology.Triangles,
                argsBuffer,
                0,
                targetCamera,
                null,
                ShadowCastingMode.Off,
                false,
                0);
        }

        private void ClearPreparedPreview()
        {
            _uploadedCount = 0;
            _drawBoundsValid = false;
            if (_pendingBuildScheduled)
                _pendingBuildDiscard = true;
        }

        private Vector3 ResolvePointRuntime(int index, Transform authoredPoint)
        {
            if (authoredPoint != null)
                return authoredPoint.position;

            if (TryGetRuntimePointAup(index, out AbsoluteUniversePosition runtimeAup) &&
                TryResolveRuntimeFloat3AupDelta(in runtimeAup, out float3 runtimePoint))
            {
                return (Vector3)runtimePoint;
            }

            return _cachedTransform != null ? _cachedTransform.position : Vector3.zero;
        }

        private static bool TryResolveRuntimeFloat3AupDelta(in AbsoluteUniversePosition position, out float3 runtimePoint)
        {
            runtimePoint = default;
            if (!AbsoluteUniversePosition.IsFinite(in position) ||
                !TryResolveRuntimeOriginAup(out double3 originAup))
            {
                return false;
            }

            double3 localDelta = position.ToAbsoluteDouble3() - originAup;
            if (!math.all(math.isfinite(localDelta)) ||
                math.any(math.abs(localDelta) > (double)float.MaxValue))
                return false;

            runtimePoint.x = (float)localDelta.x;
            runtimePoint.y = (float)localDelta.y;
            runtimePoint.z = (float)localDelta.z;
            return math.all(math.isfinite(runtimePoint));
        }

        private double3 ResolvePointAup(int index, Transform authoredPoint)
        {
            if (authoredPoint != null &&
                TryResolveAupFromRuntimeOrigin(authoredPoint.position, out AbsoluteUniversePosition authoredAup))
            {
                return authoredAup.ToAbsoluteDouble3();
            }

            if (TryGetRuntimePointAup(index, out AbsoluteUniversePosition runtimeAup))
                return runtimeAup.ToAbsoluteDouble3();

            Vector3 fallback = _cachedTransform != null ? _cachedTransform.position : Vector3.zero;
            return TryResolveAupFromRuntimeOrigin(fallback, out AbsoluteUniversePosition fallbackAup)
                ? fallbackAup.ToAbsoluteDouble3()
                : TryResolveRuntimeOriginAup(out double3 originAup) ? originAup : double3.zero;
        }

        private static bool TryResolveAupFromRuntimeOrigin(Vector3 runtimePosition, out AbsoluteUniversePosition aup)
        {
            aup = default;
            if (!float.IsFinite(runtimePosition.x) ||
                !float.IsFinite(runtimePosition.y) ||
                !float.IsFinite(runtimePosition.z))
            {
                return false;
            }

            if (!TryResolveRuntimeOriginAup(out double3 originAup))
                return false;

            double3 resolved = originAup + new double3(runtimePosition.x, runtimePosition.y, runtimePosition.z);
            if (!math.all(math.isfinite(resolved)))
                return false;

            aup = AbsoluteUniversePosition.FromAbsolutePosition(resolved);
            return AbsoluteUniversePosition.IsFinite(in aup);
        }

        private bool TryGetRuntimePointAup(int index, out AbsoluteUniversePosition aup)
        {
            switch (index)
            {
                case 0:
                    aup = _runtimePointAup0;
                    return _hasRuntimePoint0;
                case 1:
                    aup = _runtimePointAup1;
                    return _hasRuntimePoint1;
                case 2:
                    aup = _runtimePointAup2;
                    return _hasRuntimePoint2;
                case 3:
                    aup = _runtimePointAup3;
                    return _hasRuntimePoint3;
                default:
                    aup = default;
                    return false;
            }
        }

        private void SetRuntimePointAup(int index, in AbsoluteUniversePosition aup)
        {
            switch (index)
            {
                case 0:
                    _runtimePointAup0 = aup;
                    _hasRuntimePoint0 = true;
                    break;
                case 1:
                    _runtimePointAup1 = aup;
                    _hasRuntimePoint1 = true;
                    break;
                case 2:
                    _runtimePointAup2 = aup;
                    _hasRuntimePoint2 = true;
                    break;
                case 3:
                    _runtimePointAup3 = aup;
                    _hasRuntimePoint3 = true;
                    break;
            }
        }

        private void EnsureBuffersCold()
        {
            if (!TryResolveVaultCold(out IDataVault vault))
                return;

            _vault = vault;
            if (vault.TryReadHandle(in _stateHandle, out NativeArray<BuilderGhostStateDTO> states) &&
                vault.TryReadHandle(in _visualHandle, out NativeArray<BuilderGhostVisualDTO> visuals) &&
                vault.TryReadHandle(in _argsHandle, out NativeArray<BuilderGhostIndirectArgsDTO> args) &&
                states.IsCreated &&
                visuals.IsCreated &&
                args.IsCreated &&
                states.Length >= MaxPreviewInstances &&
                visuals.Length >= MaxPreviewInstances &&
                args.Length >= 1)
            {
                return;
            }

            _stateHandle = vault.EnsureGenerationHandle<BuilderGhostStateDTO>(
                PipeStateBufferId,
                MaxPreviewInstances,
                SystemID.Construction,
                NativeArrayOptions.UninitializedMemory);
            _visualHandle = vault.EnsureGenerationHandle<BuilderGhostVisualDTO>(
                PipeVisualBufferId,
                MaxPreviewInstances,
                SystemID.Construction,
                NativeArrayOptions.UninitializedMemory);
            _argsHandle = vault.EnsureGenerationHandle<BuilderGhostIndirectArgsDTO>(
                PipeIndirectArgsBufferId,
                1,
                SystemID.Construction,
                NativeArrayOptions.UninitializedMemory);
        }

        private bool TryAcquirePreviewWriteBuffers(
            out NativeArray<BuilderGhostStateDTO> states,
            out NativeArray<BuilderGhostVisualDTO> visuals,
            out NativeArray<BuilderGhostIndirectArgsDTO> args,
            out IDataVault vault)
        {
            states = default;
            visuals = default;
            args = default;
            vault = null;

            EnsureBuffersCold();
            vault = _vault;
            if (vault == null)
                return false;

            int acquiredCount = 0;
            if (!vault.TryAcquireWriteLock(in _stateHandle, SystemID.Construction, out states))
                return false;
            acquiredCount = 1;
            if (!states.IsCreated || states.Length < MaxPreviewInstances)
            {
                ReleasePreviewWriteLocks(vault, acquiredCount);
                return false;
            }

            if (!vault.TryAcquireWriteLock(in _visualHandle, SystemID.Construction, out visuals))
            {
                ReleasePreviewWriteLocks(vault, acquiredCount);
                return false;
            }
            acquiredCount = 2;
            if (!visuals.IsCreated || visuals.Length < MaxPreviewInstances)
            {
                ReleasePreviewWriteLocks(vault, acquiredCount);
                return false;
            }

            if (!vault.TryAcquireWriteLock(in _argsHandle, SystemID.Construction, out args))
            {
                ReleasePreviewWriteLocks(vault, acquiredCount);
                return false;
            }
            acquiredCount = 3;
            if (!args.IsCreated || args.Length < 1)
            {
                ReleasePreviewWriteLocks(vault, acquiredCount);
                return false;
            }

            return true;
        }

        private bool TryReadLockedPreviewBuffers(
            out NativeArray<BuilderGhostStateDTO> states,
            out NativeArray<BuilderGhostVisualDTO> visuals,
            out NativeArray<BuilderGhostIndirectArgsDTO> args)
        {
            states = default;
            visuals = default;
            args = default;

            IDataVault vault = _pendingBuildWriteLockVault;
            return vault != null &&
                   _pendingBuildWriteLockCount == 3 &&
                   vault.TryResolveHandle(in _stateHandle, out states) &&
                   vault.TryResolveHandle(in _visualHandle, out visuals) &&
                   vault.TryResolveHandle(in _argsHandle, out args) &&
                   states.IsCreated &&
                   visuals.IsCreated &&
                   args.IsCreated &&
                   states.Length >= MaxPreviewInstances &&
                   visuals.Length >= MaxPreviewInstances &&
                   args.Length >= 1;
        }

        private void ReleasePendingPreviewWriteLocks()
        {
            IDataVault vault = _pendingBuildWriteLockVault;
            int acquiredCount = _pendingBuildWriteLockCount;
            _pendingBuildWriteLockVault = null;
            _pendingBuildWriteLockCount = 0;
            ReleasePreviewWriteLocks(vault, acquiredCount);
        }

        private void ReleasePreviewWriteLocks(IDataVault vault, int acquiredCount)
        {
            if (vault == null || acquiredCount <= 0)
                return;

            if (acquiredCount >= 3)
                vault.ReleaseWriteLock(in _argsHandle, SystemID.Construction);
            if (acquiredCount >= 2)
                vault.ReleaseWriteLock(in _visualHandle, SystemID.Construction);
            if (acquiredCount >= 1)
                vault.ReleaseWriteLock(in _stateHandle, SystemID.Construction);
        }

        private bool TryResolveVaultCold(out IDataVault vault)
        {
            vault = _vault;
            if (vault != null)
                return true;

            vault = GlobalRegistry.DataVault;
            _vault = vault;
            return vault != null;
        }

        private void EnsureGraphicsBuffers()
        {
            if (_stateBufferA != null &&
                _stateBufferB != null &&
                _visualBufferA != null &&
                _visualBufferB != null &&
                _argsBufferA != null &&
                _argsBufferB != null)
            {
                return;
            }

            ReleaseGraphicsBuffer(ref _stateBufferA);
            ReleaseGraphicsBuffer(ref _stateBufferB);
            ReleaseGraphicsBuffer(ref _visualBufferA);
            ReleaseGraphicsBuffer(ref _visualBufferB);
            ReleaseGraphicsBuffer(ref _argsBufferA);
            ReleaseGraphicsBuffer(ref _argsBufferB);
            _stateBufferA = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<BuilderGhostStateDTO>(MaxPreviewInstances);
            _stateBufferB = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<BuilderGhostStateDTO>(MaxPreviewInstances);
            _visualBufferA = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<BuilderGhostVisualDTO>(MaxPreviewInstances);
            _visualBufferB = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<BuilderGhostVisualDTO>(MaxPreviewInstances);
            _argsBufferA = CreateIndirectArgsBuffer();
            _argsBufferB = CreateIndirectArgsBuffer();
            _boundStateBuffer = null;
            _boundVisualBuffer = null;
        }

        private bool HasGraphicsBuffers()
        {
            return _stateBufferA != null &&
                   _stateBufferB != null &&
                   _visualBufferA != null &&
                   _visualBufferB != null &&
                   _argsBufferA != null &&
                   _argsBufferB != null;
        }

        private uint CapturePreviewFrameId()
        {
            uint dispatcherFrame = TimeSliceScheduler.CurrentFrameId;
            if (dispatcherFrame != 0u)
            {
                if (dispatcherFrame > _previewFrameCounter)
                    _previewFrameCounter = dispatcherFrame;
                return dispatcherFrame;
            }

            _previewFrameCounter = unchecked(_previewFrameCounter + 1u);
            return _previewFrameCounter != 0u ? _previewFrameCounter : 1u;
        }

        private static bool TryResolveRuntimeOriginAup(out double3 runtimeOriginAup)
        {
            runtimeOriginAup = HectonFloatingOrigin.CurrentTotalOffsetDouble;
            return math.all(math.isfinite(runtimeOriginAup));
        }

        private void EnsureMaterial()
        {
            if (previewMaterial != null)
                return;

#if UNITY_EDITOR
            if (previewShader == null)
                previewShader = AssetDatabase.LoadAssetAtPath<Shader>(HologramShaderPath);

            if (previewShader == null)
                return;

            previewMaterial = new Material(previewShader)
            {
                enableInstancing = false,
                hideFlags = HideFlags.DontSave
            };
#endif
        }

        private void UpdateDrawBoundsFromStates(NativeArray<BuilderGhostStateDTO> states, int count)
        {
            _drawBoundsValid = false;
            if (!states.IsCreated || count <= 0)
                return;

            float3 min = new float3(float.MaxValue);
            float3 max = new float3(float.MinValue);
            int safeCount = math.min(count, states.Length);
            bool found = false;
            for (int i = 0; i < safeCount; i++)
            {
                BuilderGhostStateDTO state = states[i];
                if ((state.ValidationFlags & BuilderGhostValidationFlags.Active) == 0u)
                    continue;

                float3 center = state.LocalToWorld.c3.xyz;
                float3 axisX = state.LocalToWorld.c0.xyz * 0.5f;
                float3 axisY = state.LocalToWorld.c1.xyz * 0.5f;
                float3 axisZ = state.LocalToWorld.c2.xyz * 0.5f;
                if (!math.all(math.isfinite(center)) ||
                    !math.all(math.isfinite(axisX)) ||
                    !math.all(math.isfinite(axisY)) ||
                    !math.all(math.isfinite(axisZ)))
                {
                    continue;
                }

                float3 extents = math.abs(axisX) + math.abs(axisY) + math.abs(axisZ);
                min = math.min(min, center - extents);
                max = math.max(max, center + extents);
                found = true;
            }

            if (!found)
                return;

            float3 size = math.max(max - min, new float3(0.001f));
            float3 centerPoint = (min + max) * 0.5f;
            _drawBounds = new Bounds(
                new Vector3(centerPoint.x, centerPoint.y, centerPoint.z),
                new Vector3(size.x, size.y, size.z));
            _drawBoundsValid = true;
        }

        private bool IsDrawBoundsCameraVisible()
        {
            if (targetCamera == null)
                return true;

            Transform cameraTransform = targetCamera.transform;
            if (cameraTransform == null)
                return true;

            Vector3 localCenter = cameraTransform.InverseTransformPoint(_drawBounds.center);
            float radius = _drawBounds.extents.magnitude;
            return localCenter.z + radius >= targetCamera.nearClipPlane &&
                   localCenter.z - radius <= targetCamera.farClipPlane;
        }

        private void TryRegisterRuntime()
        {
            if (!Application.isPlaying || !HectonXRRuntimeState.IsXRActive)
                return;

            if (!_registeredLateFrame && GlobalRegistry.Dispatcher != null)
            {
                _registeredLateFrame = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Player);
            }

            if (!_registeredRenderable)
                _registeredRenderable = GlobalRegistry.Renderables.TryRegister(this);
        }

        private void TryUnregisterRuntime()
        {
            if (_registeredLateFrame)
            {
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Player);
                _registeredLateFrame = false;
            }

            if (_registeredRenderable)
            {
                GlobalRegistry.Renderables.Unregister(this);
                _registeredRenderable = false;
            }
        }

        private void HandleXRActiveChanged(bool isActive)
        {
            _stateFlags |= StateMatricesDirty;
            if (isActive)
            {
                EnsureBuffersCold();
                TryRegisterRuntime();
                return;
            }

            TryUnregisterRuntime();
            ClearPreparedPreview();
        }

        private void RefreshXRRegistration()
        {
            if (HectonXRRuntimeState.IsXRActive)
            {
                EnsureBuffersCold();
                TryRegisterRuntime();
                return;
            }

            TryUnregisterRuntime();
        }

        private void CompletePendingBuildForTeardown()
        {
            if (!_pendingBuildScheduled)
            {
                ReleasePendingPreviewWriteLocks();
                return;
            }

            DispatcherJobFence.TryComplete(ref _pendingBuildHandle, forceComplete: true);
            _pendingBuildScheduled = false;
            _pendingBuildDiscard = false;
            ReleasePendingPreviewWriteLocks();
        }

        private GraphicsBuffer ResolveWriteStateBuffer()
        {
            return _writeBufferIndex == 0 ? _stateBufferA : _stateBufferB;
        }

        private GraphicsBuffer ResolveWriteVisualBuffer()
        {
            return _writeBufferIndex == 0 ? _visualBufferA : _visualBufferB;
        }

        private GraphicsBuffer ResolveWriteArgsBuffer()
        {
            return _writeBufferIndex == 0 ? _argsBufferA : _argsBufferB;
        }

        private static GraphicsBuffer CreateIndirectArgsBuffer()
        {
            return new GraphicsBuffer(
                GraphicsBuffer.Target.IndirectArguments,
                GraphicsBuffer.UsageFlags.LockBufferForWrite,
                1,
                UnsafeUtility.SizeOf<BuilderGhostIndirectArgsDTO>());
        }

        private static void ReleaseGraphicsBuffer(ref GraphicsBuffer buffer)
        {
            if (buffer == null)
                return;

            buffer.Release();
            buffer = null;
        }

        private void CacheRuntimeReferences()
        {
            _cachedTransform = transform;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (segmentLengthMeters < 0.01f)
                segmentLengthMeters = 0.01f;
            if (segmentRadiusMeters < 0.001f)
                segmentRadiusMeters = 0.001f;
            _stateFlags |= StateMatricesDirty;
            CacheRuntimeReferences();
        }
#endif
    }
}
