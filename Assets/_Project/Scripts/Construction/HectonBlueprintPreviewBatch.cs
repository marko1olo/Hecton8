using Hecton8.Core;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Memory;
using Hecton8.World;
using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.Rendering;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Hecton8.Construction
{
    [DisallowMultipleComponent]
    public sealed class HectonBlueprintPreviewBatch : MonoBehaviour, IRenderable, ILateFrameTickable, IGlobalRegistryHotSwapListener
    {
        private const string HologramShaderPath = "Assets/_Project/Shaders/Hecton_ConstructionDearLieHologram.shader";
        private const float DefaultDearLieWiggleSpeed = 18f;

        [SerializeField] private Material previewMaterial;
        [SerializeField] private Shader previewShader;
        [SerializeField] private Camera targetCamera;
        [SerializeField, Min(1)] private int capacity = 128;
        [SerializeField] private Color validColor = new Color(0.08f, 1f, 0.72f, 0.72f);
        [SerializeField] private Color invalidColor = new Color(1f, 0.18f, 0.12f, 0.78f);

        private VaultGenerationHandle<BuilderGhostStateDTO> _stateHandle;
        private VaultGenerationHandle<BuilderGhostVisualDTO> _visualHandle;
        private VaultGenerationHandle<HolographyTelemetryEntry> _telemetryHandle;
        private VaultGenerationHandle<BuilderGhostIndirectArgsDTO> _argsHandle;
        private IDataVault _vault;
        private GraphicsBuffer _stateBufferA;
        private GraphicsBuffer _stateBufferB;
        private GraphicsBuffer _visualBufferA;
        private GraphicsBuffer _visualBufferB;
        private GraphicsBuffer _argsBufferA;
        private GraphicsBuffer _argsBufferB;
        private Bounds _drawBounds = new Bounds(Vector3.zero, new Vector3(256f, 256f, 256f));
        private JobHandle _pendingBuildHandle;
        private bool _registeredRenderable;
        private bool _registeredLateFrame;
        private bool _registeredHotSwap;
        private bool _pendingBuildScheduled;
        private bool _pendingBuildDiscard;
        private int _activeCount;
        private int _uploadedCount;
        private int _pendingBuildCount;
        private int _capacityResolved;
        private int _writeBufferIndex;
        private uint _previewFrameCounter;
        private uint _lastPreviewSignalFrame;
        private bool _hasLastPreviewSignalFrame;
        private bool _drawBoundsValid;
        private GraphicsBuffer _boundStateBuffer;
        private GraphicsBuffer _boundVisualBuffer;
        private uint _lastSignalBatchHash;
        private int _lastSignalBatchCount;
        private bool _hasLastSignalBatchHash;

        private static readonly int BuilderGhostStatesId = Shader.PropertyToID("_H8BuilderGhostStates");
        private static readonly int BuilderGhostVisualsId = Shader.PropertyToID("_H8BuilderGhostVisuals");

        private void Awake()
        {
            ConfigureSignalLane();
            EnsureBuffersCold();
            EnsureMaterial();
            EnsureGraphicsBuffers();
        }

        private void OnEnable()
        {
            ConfigureSignalLane();
            if (!Application.isPlaying)
                return;

            EnsureBuffersCold();
            TryRegisterHotSwapListener();
            _registeredRenderable = GlobalRegistry.Renderables.TryRegister(this);
            _registeredLateFrame = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.UI);
        }

        private void OnDisable()
        {
            TryUnregisterHotSwapListener();
            if (_registeredRenderable)
            {
                GlobalRegistry.Renderables.Unregister(this);
                _registeredRenderable = false;
            }

            if (_registeredLateFrame)
            {
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.UI);
                _registeredLateFrame = false;
            }

            CompletePendingBuildForTeardown();
            _uploadedCount = 0;
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot == GlobalRegistryServiceSlot.Dispatcher && currentService != null && isActiveAndEnabled)
            {
                if (_registeredLateFrame)
                {
                    GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.UI);
                    _registeredLateFrame = false;
                }

                _registeredLateFrame = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.UI);
                return;
            }

            if (serviceSlot == GlobalRegistryServiceSlot.DataVault && currentService != null && isActiveAndEnabled)
            {
                _vault = currentService as IDataVault;
                _stateHandle = default;
                _visualHandle = default;
                _telemetryHandle = default;
                _argsHandle = default;
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
            _telemetryHandle = default;
            _argsHandle = default;
            _boundStateBuffer = null;
            _boundVisualBuffer = null;
            _vault = null;

            if (previewMaterial != null && previewMaterial.hideFlags == HideFlags.DontSave)
                Destroy(previewMaterial);
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (!Application.isPlaying ||
                !TryReadCachedBuffers(
                    out NativeArray<BuilderGhostStateDTO> states,
                    out _,
                    out _,
                    out _) ||
                _activeCount <= 0)
            {
                return;
            }

            int count = math.min(_activeCount, states.Length);
            for (int i = 0; i < count; i++)
            {
                BuilderGhostStateDTO state = states[i];
                Matrix4x4 matrix = ToMatrix4x4(in state.LocalToWorld);
                bool blocked = (state.ValidationFlags & (BuilderGhostValidationFlags.SdfBlocked | BuilderGhostValidationFlags.BoundsBlocked | BuilderGhostValidationFlags.NonFinite)) != 0u;
                Gizmos.matrix = matrix;
                Gizmos.color = blocked ? new Color(1f, 0f, 0f, 0.35f) : new Color(0f, 1f, 0.4f, 0.25f);
                Gizmos.DrawWireCube(Vector3.zero, Vector3.one);
                Gizmos.color = blocked ? Color.red : Color.green;
                for (int corner = 0; corner < ShinobuSocketConstructionRuntime.BuilderGhostSdfCornerCount; corner++)
                {
                    float sx = (corner & 1) == 0 ? -0.5f : 0.5f;
                    float sy = (corner & 2) == 0 ? -0.5f : 0.5f;
                    float sz = (corner & 4) == 0 ? -0.5f : 0.5f;
                    Gizmos.DrawSphere(new Vector3(sx, sy, sz), 0.035f);
                }
            }

            Gizmos.matrix = Matrix4x4.identity;
        }
#endif

        public void Render(float deltaTime)
        {
            DrawPreparedBatch();
        }

        public void LateFrameTick()
        {
            TryFinalizePendingBuildAndUpload();
            ConsumeConstructionPreviewSignals();
            RecordActiveTelemetryHeartbeat();
        }

        public bool SetPreview(int index, Vector3 position, Quaternion rotation, Vector3 scale, uint requirementMask, uint ownedMask)
        {
            if (!TryReadCachedBuffers(
                    out NativeArray<BuilderGhostStateDTO> states,
                    out NativeArray<BuilderGhostVisualDTO> visuals,
                    out _,
                    out NativeArray<BuilderGhostIndirectArgsDTO> args))
            {
                return false;
            }

            if (_pendingBuildScheduled ||
                (uint)index >= (uint)states.Length ||
                (uint)index >= (uint)visuals.Length)
            {
                return false;
            }

            uint flags = BuilderGhostValidationFlags.Active |
                         BuilderGhostValidationFlags.PresentationOnly |
                         BuilderGhostValidationFlags.RollbackExcluded |
                         BuilderGhostValidationFlags.GridSnapped;
            if ((ownedMask & requirementMask) == requirementMask)
                flags |= BuilderGhostValidationFlags.Valid;
            else
                flags |= BuilderGhostValidationFlags.BoundsBlocked;

            if (!TryResolveAupFromRuntimeOrigin(position, out AbsoluteUniversePosition centerAup) ||
                !TryResolveRuntimeOriginAup(out double3 runtimeOriginAup))
            {
                return false;
            }

            float quality = ShinobuSocketConstructionRuntime.ResolveGlobalQualityWeight();
            uint frame = CapturePreviewFrameId();
            JobHandle dependency = ScheduleBuilderGhostStateBuild(
                states,
                visuals,
                index,
                centerAup.ToAbsoluteDouble3(),
                runtimeOriginAup,
                new quaternion(rotation.x, rotation.y, rotation.z, rotation.w),
                (float3)scale,
                0u,
                flags,
                0f,
                quality,
                DefaultDearLieWiggleSpeed,
                frame,
                default);
            _pendingBuildHandle = ScheduleIndirectArgsBuild(args, index + 1, dependency);
            _pendingBuildScheduled = true;
            _pendingBuildDiscard = false;
            _pendingBuildCount = index + 1;

            _activeCount = math.max(_activeCount, index + 1);
            return true;
        }

        public void SetActivePreviewCount(int count)
        {
            _activeCount = math.clamp(count, 0, ResolveCapacity());
            if (_uploadedCount > _activeCount)
                _uploadedCount = _activeCount;
            if (_activeCount <= 0)
                ClearPreviews();
        }

        public void ClearPreviews()
        {
            _activeCount = 0;
            _uploadedCount = 0;
            _drawBoundsValid = false;
            _lastSignalBatchHash = 0u;
            _lastSignalBatchCount = 0;
            _hasLastSignalBatchHash = false;
            _hasLastPreviewSignalFrame = false;
            if (_pendingBuildScheduled)
                _pendingBuildDiscard = true;
        }

        private void DrawPreparedBatch()
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
                null,
                null,
                ShadowCastingMode.Off,
                false,
                0);
        }

        private bool TryFinalizePendingBuildAndUpload()
        {
            if (!_pendingBuildScheduled)
                return false;

            if (!DispatcherJobFence.TryFinalizeCompleted(ref _pendingBuildHandle))
                return false;

            int uploadCount = _pendingBuildDiscard ? 0 : _pendingBuildCount;
            _pendingBuildScheduled = false;
            _pendingBuildDiscard = false;
            _pendingBuildCount = 0;
            if (!TryReadCachedBuffers(
                    out NativeArray<BuilderGhostStateDTO> states,
                    out NativeArray<BuilderGhostVisualDTO> visuals,
                    out NativeArray<HolographyTelemetryEntry> telemetry,
                    out NativeArray<BuilderGhostIndirectArgsDTO> args))
            {
                _uploadedCount = 0;
                _drawBoundsValid = false;
                return false;
            }

            if (!HasGraphicsBuffers())
            {
                _uploadedCount = 0;
                _drawBoundsValid = false;
                return false;
            }

            int writeCount = math.min(uploadCount, math.min(states.Length, visuals.Length));
            UpdateDrawBoundsFromStates(states, writeCount);
            if (!_drawBoundsValid)
            {
                _uploadedCount = 0;
                return true;
            }

            GraphicsBuffer stateTarget = ResolveWriteStateBuffer();
            GraphicsBuffer visualTarget = ResolveWriteVisualBuffer();
            GraphicsBuffer argsTarget = ResolveWriteArgsBuffer();
            GraphicsBufferUploadUtility.UploadNativeArray(stateTarget, states, writeCount);
            GraphicsBufferUploadUtility.UploadNativeArray(visualTarget, visuals, writeCount);
            GraphicsBufferUploadUtility.UploadNativeArray(argsTarget, args, 1);
            for (int i = 0; i < writeCount; i++)
            {
                BuilderGhostStateDTO writtenState = states[i];
                WriteTelemetry(
                    telemetry,
                    writtenState,
                    (uint)ShinobuSocketConstructionRuntime.BuilderGhostSdfCornerCount,
                    0f,
                    ResolveTelemetrySdfDistance(writtenState.ValidationFlags),
                    visuals[i].GlobalQualityWeight);
            }

            _uploadedCount = writeCount;
            _writeBufferIndex ^= 1;
            return true;
        }

        private static JobHandle ScheduleIndirectArgsBuild(NativeArray<BuilderGhostIndirectArgsDTO> args, int instanceCount, JobHandle dependency)
        {
            if (!args.IsCreated || args.Length <= 0)
                return dependency;

            return new BuildBuilderGhostIndirectArgsJob
            {
                Args = args,
                InstanceCount = (uint)math.max(0, instanceCount)
            }.Schedule(dependency);
        }

        private static void ConfigureSignalLane()
        {
            SignalBus<ConstructionPreviewSignal>.Configure(
                expectedCapacity: 4,
                maxFrameSignals: 8,
                lowTierFrameSignals: 8,
                laneHash: ConstructionPreviewSignal.LaneHash);
            SignalBus<ConstructionPreviewSignal>.EnsureInitialized();
        }

        private void ConsumeConstructionPreviewSignals()
        {
            if (_pendingBuildScheduled)
                return;

            ReadOnlySpan<ConstructionPreviewSignal> signals = SignalBus<ConstructionPreviewSignal>.GetFrameSnapshot();
            uint frame = CapturePreviewFrameId();
            if (signals.Length <= 0)
            {
                if (_hasLastPreviewSignalFrame && frame - _lastPreviewSignalFrame > 1u)
                    ClearPreviews();
                return;
            }

            if (!TryReadCachedBuffers(
                    out NativeArray<BuilderGhostStateDTO> states,
                    out NativeArray<BuilderGhostVisualDTO> visuals,
                    out _,
                    out NativeArray<BuilderGhostIndirectArgsDTO> args))
            {
                return;
            }

            int capacityLimit = math.min(states.Length, visuals.Length);
            uint batchHash = ComputePreviewSignalBatchHash(signals, capacityLimit, out int activeSignalCount);
            if (activeSignalCount <= 0)
            {
                ClearPreviews();
                return;
            }

            if (_hasLastSignalBatchHash &&
                _lastSignalBatchHash == batchHash &&
                _lastSignalBatchCount == activeSignalCount &&
                _uploadedCount == activeSignalCount &&
                _drawBoundsValid)
            {
                _activeCount = activeSignalCount;
                _lastPreviewSignalFrame = frame;
                _hasLastPreviewSignalFrame = true;
                return;
            }

            int writeCount = 0;
            JobHandle buildDependency = default;
            if (!TryResolveRuntimeOriginAup(out double3 runtimeOriginAup))
                return;

            for (int i = 0; i < signals.Length && writeCount < capacityLimit; i++)
            {
                ConstructionPreviewSignal signal = signals[i];
                if ((signal.Flags & ConstructionPreviewSignal.FlagActive) == 0)
                    continue;
                if (!AbsoluteUniversePosition.IsFinite(in signal.CenterAup))
                    continue;

                quaternion rotation = new quaternion(signal.Rotation.x, signal.Rotation.y, signal.Rotation.z, signal.Rotation.w);
                float3 safeScale = math.max(signal.Scale, new float3(0.001f));
                uint flags = BuilderGhostValidationFlags.Active |
                             BuilderGhostValidationFlags.PresentationOnly |
                             BuilderGhostValidationFlags.RollbackExcluded |
                             BuilderGhostValidationFlags.GridSnapped;
                if (signal.IsValid != 0)
                    flags |= BuilderGhostValidationFlags.Valid;
                if ((signal.Flags & ConstructionPreviewSignal.FlagSocketSnap) != 0)
                    flags |= BuilderGhostValidationFlags.SocketSnap;
                if ((signal.Flags & ConstructionPreviewSignal.FlagDearLieActive) != 0)
                    flags |= BuilderGhostValidationFlags.DearLieActive;
                if (signal.IsValid == 0 && signal.FailureFlags == 0u)
                    flags |= BuilderGhostValidationFlags.BoundsBlocked;
                if (signal.IsValid == 0 && signal.FailureFlags != 0u)
                    flags |= BuilderGhostValidationFlags.SdfBlocked;

                buildDependency = ScheduleBuilderGhostStateBuild(
                    states,
                    visuals,
                    writeCount,
                    signal.CenterAup.ToAbsoluteDouble3(),
                    runtimeOriginAup,
                    rotation,
                    safeScale,
                    signal.ModuleHash,
                    flags,
                    signal.DearLieDampen,
                    signal.GlobalQualityWeight,
                    signal.DearLieWiggleSpeed,
                    signal.Frame != 0u ? signal.Frame : frame,
                    buildDependency);
                _lastPreviewSignalFrame = frame;
                _hasLastPreviewSignalFrame = true;
                writeCount++;
            }

            if (writeCount <= 0)
                return;

            _pendingBuildHandle = ScheduleIndirectArgsBuild(args, writeCount, buildDependency);
            _pendingBuildScheduled = true;
            _pendingBuildDiscard = false;
            _pendingBuildCount = writeCount;
            _activeCount = writeCount;
            _lastSignalBatchHash = batchHash;
            _lastSignalBatchCount = writeCount;
            _hasLastSignalBatchHash = true;
        }

        private static uint ComputePreviewSignalBatchHash(ReadOnlySpan<ConstructionPreviewSignal> signals, int capacityLimit, out int activeCount)
        {
            activeCount = 0;
            uint hash = 2166136261u;
            int limit = math.max(0, capacityLimit);
            for (int i = 0; i < signals.Length && activeCount < limit; i++)
            {
                ConstructionPreviewSignal signal = signals[i];
                if ((signal.Flags & ConstructionPreviewSignal.FlagActive) == 0)
                    continue;

                hash = FoldPreviewSignal(hash, in signal);
                activeCount++;
            }

            return ShinobuSocketConstructionRuntime.FoldHash(hash, (uint)activeCount);
        }

        private static uint FoldPreviewSignal(uint hash, in ConstructionPreviewSignal signal)
        {
            hash = FoldAup(hash, in signal.CenterAup);
            hash = FoldFloat4(hash, signal.Rotation);
            hash = FoldFloat3(hash, signal.Scale);
            hash = ShinobuSocketConstructionRuntime.FoldHash(hash, signal.ModuleHash);
            hash = ShinobuSocketConstructionRuntime.FoldHash(hash, signal.FailureFlags);
            hash = ShinobuSocketConstructionRuntime.FoldHash(hash, signal.ResultHash);
            hash = ShinobuSocketConstructionRuntime.FoldHash(hash, signal.IsValid);
            hash = ShinobuSocketConstructionRuntime.FoldHash(hash, signal.Flags);
            hash = ShinobuSocketConstructionRuntime.FoldHash(hash, math.asuint(signal.DearLieDampen));
            hash = ShinobuSocketConstructionRuntime.FoldHash(hash, math.asuint(signal.GlobalQualityWeight));
            return ShinobuSocketConstructionRuntime.FoldHash(hash, math.asuint(signal.DearLieWiggleSpeed));
        }

        private static uint FoldAup(uint hash, in AbsoluteUniversePosition aup)
        {
            hash = FoldLong(hash, aup.GridX);
            hash = FoldLong(hash, aup.GridY);
            hash = FoldLong(hash, aup.GridZ);
            hash = ShinobuSocketConstructionRuntime.FoldHash(hash, math.asuint(aup.LocalX));
            hash = ShinobuSocketConstructionRuntime.FoldHash(hash, math.asuint(aup.LocalY));
            return ShinobuSocketConstructionRuntime.FoldHash(hash, math.asuint(aup.LocalZ));
        }

        private static uint FoldLong(uint hash, long value)
        {
            ulong bits = (ulong)value;
            hash = ShinobuSocketConstructionRuntime.FoldHash(hash, (uint)bits);
            return ShinobuSocketConstructionRuntime.FoldHash(hash, (uint)(bits >> 32));
        }

        private static uint FoldFloat3(uint hash, float3 value)
        {
            uint3 bits = math.asuint(value);
            hash = ShinobuSocketConstructionRuntime.FoldHash(hash, bits.x);
            hash = ShinobuSocketConstructionRuntime.FoldHash(hash, bits.y);
            return ShinobuSocketConstructionRuntime.FoldHash(hash, bits.z);
        }

        private static uint FoldFloat4(uint hash, float4 value)
        {
            uint4 bits = math.asuint(value);
            hash = ShinobuSocketConstructionRuntime.FoldHash(hash, bits.x);
            hash = ShinobuSocketConstructionRuntime.FoldHash(hash, bits.y);
            hash = ShinobuSocketConstructionRuntime.FoldHash(hash, bits.z);
            return ShinobuSocketConstructionRuntime.FoldHash(hash, bits.w);
        }

        private JobHandle ScheduleBuilderGhostStateBuild(
            NativeArray<BuilderGhostStateDTO> states,
            NativeArray<BuilderGhostVisualDTO> visuals,
            int index,
            double3 centerAup,
            double3 runtimeOriginAup,
            quaternion rotation,
            float3 scale,
            uint moduleHash,
            uint validationFlags,
            float dearLieDampen,
            float globalQualityWeight,
            float dearLieWiggleSpeed,
            uint frame,
            JobHandle dependency)
        {
            BuildBuilderGhostStateJob buildJob = new BuildBuilderGhostStateJob
            {
                States = states,
                Visuals = visuals,
                TargetAup = centerAup,
                RuntimeOriginAup = runtimeOriginAup,
                Rotation = rotation,
                BoundsScale = math.max(scale, new float3(0.001f)),
                GridSizeMeters = 0d,
                PrefabHashID = moduleHash,
                ValidationFlags = validationFlags,
                AnimationPhase = ResolvePreviewAnimationPhase(frame),
                GlobalQualityWeight = globalQualityWeight,
                DearLieDampen = dearLieDampen,
                DearLieWiggleSpeed = dearLieWiggleSpeed,
                ValidColor = new float4(validColor.r, validColor.g, validColor.b, validColor.a),
                InvalidColor = new float4(invalidColor.r, invalidColor.g, invalidColor.b, invalidColor.a),
                Frame = frame,
                StateIndex = index
            };
            return buildJob.Schedule(dependency);
        }

        private void WriteTelemetry(
            NativeArray<HolographyTelemetryEntry> telemetry,
            BuilderGhostStateDTO state,
            uint sdfCornerChecks,
            float solverMicroseconds,
            float minSdfDistance,
            float globalQualityWeight)
        {
            ShinobuSocketConstructionRuntime.WriteHolographyTelemetry(
                telemetry,
                CapturePreviewFrameId(),
                state.AUP_TargetPosition,
                state.PrefabHashID,
                sdfCornerChecks,
                state.ValidationFlags,
                solverMicroseconds,
                minSdfDistance,
                state.ValidationStateHash,
                globalQualityWeight);
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

        private static float ResolvePreviewAnimationPhase(uint frame)
        {
            return math.frac(frame * (1f / 120f));
        }

        private void RecordActiveTelemetryHeartbeat()
        {
            if (_pendingBuildScheduled)
                return;

            if (!TryReadCachedBuffers(
                    out NativeArray<BuilderGhostStateDTO> states,
                    out NativeArray<BuilderGhostVisualDTO> visuals,
                    out NativeArray<HolographyTelemetryEntry> telemetry,
                    out _) ||
                !telemetry.IsCreated ||
                telemetry.Length <= 0)
            {
                return;
            }

            int count = math.min(_activeCount, math.min(states.Length, visuals.Length));
            for (int i = 0; i < count; i++)
            {
                BuilderGhostStateDTO state = states[i];
                if ((state.ValidationFlags & BuilderGhostValidationFlags.Active) == 0u)
                    continue;

                WriteTelemetry(
                    telemetry,
                    state,
                    (uint)ShinobuSocketConstructionRuntime.BuilderGhostSdfCornerCount,
                    0f,
                    ResolveTelemetrySdfDistance(state.ValidationFlags),
                    visuals[i].GlobalQualityWeight);
                return;
            }
        }

        private void EnsureBuffersCold()
        {
            int resolvedCapacity = ResolveCapacity();
            if (!TryBindVaultCold(out IDataVault vault))
                return;

            _vault = vault;
            if (vault.TryResolveHandle(in _stateHandle, out NativeArray<BuilderGhostStateDTO> states) &&
                vault.TryResolveHandle(in _visualHandle, out NativeArray<BuilderGhostVisualDTO> visuals) &&
                vault.TryResolveHandle(in _telemetryHandle, out NativeArray<HolographyTelemetryEntry> telemetry) &&
                vault.TryResolveHandle(in _argsHandle, out NativeArray<BuilderGhostIndirectArgsDTO> args) &&
                states.IsCreated &&
                states.Length >= resolvedCapacity &&
                visuals.IsCreated &&
                visuals.Length >= resolvedCapacity &&
                telemetry.IsCreated &&
                telemetry.Length >= ShinobuSocketConstructionRuntime.TelemetryCapacity &&
                args.IsCreated &&
                args.Length >= 1)
            {
                return;
            }

            _stateHandle = vault.EnsureGenerationHandle<BuilderGhostStateDTO>(
                ShinobuSocketConstructionRuntime.BuilderGhostStateBufferId,
                resolvedCapacity,
                SystemID.Construction,
                NativeArrayOptions.UninitializedMemory);
            _visualHandle = vault.EnsureGenerationHandle<BuilderGhostVisualDTO>(
                ShinobuSocketConstructionRuntime.BuilderGhostVisualBufferId,
                resolvedCapacity,
                SystemID.Construction,
                NativeArrayOptions.UninitializedMemory);
            _telemetryHandle = vault.EnsureGenerationHandle<HolographyTelemetryEntry>(
                ShinobuSocketConstructionRuntime.BuilderGhostTelemetryBufferId,
                ShinobuSocketConstructionRuntime.TelemetryCapacity,
                SystemID.Construction,
                NativeArrayOptions.UninitializedMemory);
            _argsHandle = vault.EnsureGenerationHandle<BuilderGhostIndirectArgsDTO>(
                ShinobuSocketConstructionRuntime.BuilderGhostIndirectArgsBufferId,
                1,
                SystemID.Construction,
                NativeArrayOptions.UninitializedMemory);
        }

        private bool TryReadCachedBuffers(
            out NativeArray<BuilderGhostStateDTO> states,
            out NativeArray<BuilderGhostVisualDTO> visuals,
            out NativeArray<HolographyTelemetryEntry> telemetry,
            out NativeArray<BuilderGhostIndirectArgsDTO> args)
        {
            states = default;
            visuals = default;
            telemetry = default;
            args = default;

            IDataVault vault = _vault;
            if (vault == null)
                return false;

            return vault.TryResolveHandle(in _stateHandle, out states) &&
                   vault.TryResolveHandle(in _visualHandle, out visuals) &&
                   vault.TryResolveHandle(in _telemetryHandle, out telemetry) &&
                   vault.TryResolveHandle(in _argsHandle, out args) &&
                   states.IsCreated &&
                   visuals.IsCreated &&
                   telemetry.IsCreated &&
                   args.IsCreated;
        }

        private static bool TryBindVaultCold(out IDataVault vault)
        {
            vault = GlobalRegistry.DataVault;
            return vault != null;
        }

        private static bool TryResolveAupFromRuntimeOrigin(Vector3 runtimePosition, out AbsoluteUniversePosition aup)
        {
            aup = default;
            if (!math.isfinite(runtimePosition.x) ||
                !math.isfinite(runtimePosition.y) ||
                !math.isfinite(runtimePosition.z))
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

        private static bool TryResolveRuntimeOriginAup(out double3 runtimeOriginAup)
        {
            runtimeOriginAup = HectonFloatingOrigin.CurrentTotalOffsetDouble;
            return math.all(math.isfinite(runtimeOriginAup));
        }

        private void EnsureGraphicsBuffers()
        {
            int resolvedCapacity = ResolveCapacity();
            if (_stateBufferA != null &&
                _stateBufferB != null &&
                _visualBufferA != null &&
                _visualBufferB != null &&
                _argsBufferA != null &&
                _argsBufferB != null &&
                _capacityResolved >= resolvedCapacity)
            {
                return;
            }

            ReleaseGraphicsBuffer(ref _stateBufferA);
            ReleaseGraphicsBuffer(ref _stateBufferB);
            ReleaseGraphicsBuffer(ref _visualBufferA);
            ReleaseGraphicsBuffer(ref _visualBufferB);
            ReleaseGraphicsBuffer(ref _argsBufferA);
            ReleaseGraphicsBuffer(ref _argsBufferB);
            _capacityResolved = resolvedCapacity;
            _stateBufferA = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<BuilderGhostStateDTO>(resolvedCapacity);
            _stateBufferB = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<BuilderGhostStateDTO>(resolvedCapacity);
            _visualBufferA = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<BuilderGhostVisualDTO>(resolvedCapacity);
            _visualBufferB = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<BuilderGhostVisualDTO>(resolvedCapacity);
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
                   _argsBufferB != null &&
                   _capacityResolved >= ResolveCapacity();
        }

        private void CompletePendingBuildForTeardown()
        {
            if (!_pendingBuildScheduled)
                return;

            DispatcherJobFence.TryComplete(ref _pendingBuildHandle, forceComplete: true);
            _pendingBuildScheduled = false;
            _pendingBuildDiscard = false;
            _pendingBuildCount = 0;
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

        private int ResolveCapacity()
        {
            return math.clamp(capacity, 1, ShinobuSocketConstructionRuntime.BuilderGhostStateCapacity);
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

        private static float ResolveTelemetrySdfDistance(uint flags)
        {
            return (flags & BuilderGhostValidationFlags.SdfBlocked) != 0u ? -1f : 1f;
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
            float3 boundsCenter = (min + max) * 0.5f;
            _drawBounds = new Bounds(
                new Vector3(boundsCenter.x, boundsCenter.y, boundsCenter.z),
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

        private static Matrix4x4 ToMatrix4x4(in float4x4 matrix)
        {
            Matrix4x4 result;
            result.m00 = matrix.c0.x;
            result.m10 = matrix.c0.y;
            result.m20 = matrix.c0.z;
            result.m30 = matrix.c0.w;
            result.m01 = matrix.c1.x;
            result.m11 = matrix.c1.y;
            result.m21 = matrix.c1.z;
            result.m31 = matrix.c1.w;
            result.m02 = matrix.c2.x;
            result.m12 = matrix.c2.y;
            result.m22 = matrix.c2.z;
            result.m32 = matrix.c2.w;
            result.m03 = matrix.c3.x;
            result.m13 = matrix.c3.y;
            result.m23 = matrix.c3.z;
            result.m33 = matrix.c3.w;
            return result;
        }

        private static void ReleaseGraphicsBuffer(ref GraphicsBuffer buffer)
        {
            if (buffer == null)
                return;

            buffer.Release();
            buffer = null;
        }
    }
}
