using Hecton8.Core;
using Hecton8.Core.Contracts.Signals;
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
    public sealed class VRPipeBlueprintPreview : MonoBehaviour, ILateFrameTickable, IRenderable
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
        [SerializeField] private Mesh segmentMesh;
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

        private readonly AbsoluteUniversePosition[] _runtimePointAups = new AbsoluteUniversePosition[ControlPointCount]; // COLD ALLOC: AbsoluteUniversePosition[4] - AUP-stable pipe blueprint control points - owner: VRPipeBlueprintPreview
        private readonly bool[] _hasRuntimePoint = new bool[ControlPointCount]; // COLD ALLOC: bool[4] - runtime point validity - owner: VRPipeBlueprintPreview
        private VaultBufferHandle<BuilderGhostStateDTO> _stateHandle;
        private VaultBufferHandle<BuilderGhostVisualDTO> _visualHandle;
        private VaultBufferHandle<BuilderGhostIndirectArgsDTO> _argsHandle;
        private IDataVault _vault;
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
        private bool _pendingBuildScheduled;
        private bool _pendingBuildDiscard;
        private bool _drawBoundsValid;
        private Transform _cachedTransform;
        private int _uploadedCount;
        private int _writeBufferIndex;
        private GraphicsBuffer _boundStateBuffer;
        private GraphicsBuffer _boundVisualBuffer;

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
            CacheRuntimeReferences();
            EnsureBuffers();
            EnsureMaterial();
            EnsureGraphicsBuffers();
        }

        private void OnEnable()
        {
            CacheRuntimeReferences();
            HectonXRRuntimeState.XRActiveChanged += HandleXRActiveChanged;
            EnsureBuffers();
            EnsureMaterial();
            EnsureGraphicsBuffers();
            RefreshXRRegistration();
        }

        private void OnDisable()
        {
            HectonXRRuntimeState.XRActiveChanged -= HandleXRActiveChanged;
            TryUnregisterRuntime();
            CompletePendingBuildForTeardown();
            ClearPreparedPreview();
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
            _stateHandle = default;
            _visualHandle = default;
            _argsHandle = default;
            _vault = null;
            _boundStateBuffer = null;
            _boundVisualBuffer = null;

            if (previewMaterial != null && previewMaterial.hideFlags == HideFlags.DontSave)
                Destroy(previewMaterial);
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

            if (!_hasRuntimePoint[index] || AbsoluteUniversePosition.DistanceSq(in _runtimePointAups[index], in runtimeAup) > PointDirtyDistanceSq)
                _stateFlags |= StateMatricesDirty;

            _runtimePointAups[index] = runtimeAup;
            _hasRuntimePoint[index] = true;
        }

        public void ClearRuntimePoints()
        {
            bool hadRuntimePoint = false;
            for (int i = 0; i < ControlPointCount; i++)
            {
                hadRuntimePoint |= _hasRuntimePoint[i];
                _hasRuntimePoint[i] = false;
            }

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
                !TryEnsureAndResolveBuffers(
                    out NativeArray<BuilderGhostStateDTO> states,
                    out NativeArray<BuilderGhostVisualDTO> visuals,
                    out NativeArray<BuilderGhostIndirectArgsDTO> args))
            {
                return;
            }

            double3 runtimeOriginAup = GlobalSignals.CurrentRuntimeOriginAup().ToAbsoluteDouble3();
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
                Frame = unchecked((uint)Time.frameCount),
                MaxSegments = MaxPreviewInstances
            };

            _pendingBuildHandle = job.Schedule();
            _pendingBuildScheduled = true;
            _pendingBuildDiscard = false;
            _stateFlags &= ~StateMatricesDirty;
        }

        private bool TryFinalizePendingBuildAndUpload()
        {
            if (!_pendingBuildScheduled)
                return false;

            if (!DispatcherJobFence.TryFinalizeCompleted(ref _pendingBuildHandle))
                return false;

            _pendingBuildScheduled = false;
            if (!TryEnsureAndResolveBuffers(
                    out NativeArray<BuilderGhostStateDTO> states,
                    out NativeArray<BuilderGhostVisualDTO> visuals,
                    out NativeArray<BuilderGhostIndirectArgsDTO> args))
            {
                ClearPreparedPreview();
                return false;
            }

            EnsureGraphicsBuffers();
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

            Graphics.DrawProceduralIndirect(
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

            if (_hasRuntimePoint[index])
                return (Vector3)_runtimePointAups[index].ToRuntimeFloat3();

            if (_cachedTransform == null)
                CacheRuntimeReferences();

            return _cachedTransform != null ? _cachedTransform.position : Vector3.zero;
        }

        private double3 ResolvePointAup(int index, Transform authoredPoint)
        {
            if (authoredPoint != null &&
                TryResolveAupFromRuntimeOrigin(authoredPoint.position, out AbsoluteUniversePosition authoredAup))
            {
                return authoredAup.ToAbsoluteDouble3();
            }

            if (_hasRuntimePoint[index])
                return _runtimePointAups[index].ToAbsoluteDouble3();

            if (_cachedTransform == null)
                CacheRuntimeReferences();

            Vector3 fallback = _cachedTransform != null ? _cachedTransform.position : Vector3.zero;
            return TryResolveAupFromRuntimeOrigin(fallback, out AbsoluteUniversePosition fallbackAup)
                ? fallbackAup.ToAbsoluteDouble3()
                : GlobalSignals.CurrentRuntimeOriginAup().ToAbsoluteDouble3();
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

            AbsoluteUniversePosition originAup = GlobalSignals.CurrentRuntimeOriginAup();
            aup = AbsoluteUniversePosition.OffsetMeters(
                in originAup,
                new double3(runtimePosition.x, runtimePosition.y, runtimePosition.z));
            return math.all(math.isfinite(aup.ToAbsoluteDouble3()));
        }

        private void EnsureBuffers()
        {
            if (!TryResolveVault(out IDataVault vault))
                return;

            _vault = vault;
            if (_stateHandle.IsCreated &&
                _visualHandle.IsCreated &&
                _argsHandle.IsCreated &&
                _stateHandle.Length >= MaxPreviewInstances &&
                _visualHandle.Length >= MaxPreviewInstances &&
                _argsHandle.Length >= 1 &&
                vault.ResolveBuffer(ref _stateHandle) &&
                vault.ResolveBuffer(ref _visualHandle) &&
                vault.ResolveBuffer(ref _argsHandle))
            {
                return;
            }

            _stateHandle = vault.GetBufferHandle<BuilderGhostStateDTO>(
                PipeStateBufferId,
                MaxPreviewInstances,
                SystemID.Construction,
                NativeArrayOptions.UninitializedMemory);
            _visualHandle = vault.GetBufferHandle<BuilderGhostVisualDTO>(
                PipeVisualBufferId,
                MaxPreviewInstances,
                SystemID.Construction,
                NativeArrayOptions.UninitializedMemory);
            _argsHandle = vault.GetBufferHandle<BuilderGhostIndirectArgsDTO>(
                PipeIndirectArgsBufferId,
                1,
                SystemID.Construction,
                NativeArrayOptions.UninitializedMemory);
        }

        private bool TryEnsureAndResolveBuffers(
            out NativeArray<BuilderGhostStateDTO> states,
            out NativeArray<BuilderGhostVisualDTO> visuals,
            out NativeArray<BuilderGhostIndirectArgsDTO> args)
        {
            EnsureBuffers();
            return TryResolveBuffers(out states, out visuals, out args);
        }

        private bool TryResolveBuffers(
            out NativeArray<BuilderGhostStateDTO> states,
            out NativeArray<BuilderGhostVisualDTO> visuals,
            out NativeArray<BuilderGhostIndirectArgsDTO> args)
        {
            states = default;
            visuals = default;
            args = default;

            IDataVault vault = _vault;
            if (vault == null && !TryResolveVault(out vault))
                return false;

            _vault = vault;
            states = _stateHandle.Resolve(vault);
            visuals = _visualHandle.Resolve(vault);
            args = _argsHandle.Resolve(vault);
            return states.IsCreated && visuals.IsCreated && args.IsCreated;
        }

        private static bool TryResolveVault(out IDataVault vault)
        {
            vault = GlobalRegistry.DataVault;
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
                GlobalRegistry.RegisterLateFrameTickable(this, PriorityLayer.Player);
                _registeredLateFrame = SystemDispatcher.GetLateFrameLane(PriorityLayer.Player).Contains(this);
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
                TryRegisterRuntime();
                return;
            }

            TryUnregisterRuntime();
        }

        private void CompletePendingBuildForTeardown()
        {
            if (!_pendingBuildScheduled)
                return;

            DispatcherJobFence.TryComplete(ref _pendingBuildHandle, forceComplete: true);
            _pendingBuildScheduled = false;
            _pendingBuildDiscard = false;
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
