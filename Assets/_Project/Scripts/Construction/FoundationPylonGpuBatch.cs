using System;
using System.Diagnostics;
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
    public sealed class FoundationPylonGpuBatch : MonoBehaviour, IRenderable, ILateFrameTickable, IOriginShiftListener, IGlobalRegistryHotSwapListener
    {
        private const string PylonShaderPath = "Assets/_Project/Shaders/Hecton_FoundationPylon.shader";
        private const uint WarningMask = FoundationPylonFlags.ExtensionCulled | FoundationPylonFlags.OutOfSdfBounds | FoundationPylonFlags.NonFinite;
        private const int MaxVaultJobLocks = 18;

        [SerializeField] private Material pylonMaterial;
        [SerializeField] private Shader pylonShader;
        [SerializeField] private Camera targetCamera;
        [SerializeField, Min(1)] private int maxModules = FoundationSnappingCalculatorRuntime.ModuleCapacity;
        [SerializeField] private Color baseColor = new Color(0.04f, 0.72f, 0.86f, 0.86f);
        [SerializeField] private Color embeddedColor = new Color(0.02f, 0.12f, 0.15f, 1f);

        private IDataVault _vault;
        private GraphicsBuffer _matrixBufferA;
        private GraphicsBuffer _matrixBufferB;
        private GraphicsBuffer _surfaceBufferA;
        private GraphicsBuffer _surfaceBufferB;
        private GraphicsBuffer _argsBufferA;
        private GraphicsBuffer _argsBufferB;
        private GraphicsBuffer _boundMatrixBuffer;
        private GraphicsBuffer _boundSurfaceBuffer;
        private IPlayerRuntimeContext _cachedPlayerContext;
        private VaultGenerationHandle<byte> _encodedSdfHandle;
        private JobHandle _pendingHandle;
        private readonly BufferID[] _pendingVaultLocks = new BufferID[MaxVaultJobLocks]; // COLD ALLOC: BufferID[18] - fixed DataVault job lock list - owner: FoundationPylonGpuBatch
        private Bounds _drawBounds = new Bounds(Vector3.zero, new Vector3(1f, 1f, 1f));
        private Vector3 _pendingCameraWorldOffset;
        private Vector3 _uploadedCameraWorldOffset;
        private Vector3 _lastCameraWorldOffset;
        private double3 _cachedOriginAup;
        private bool _registeredRenderable;
        private bool _registeredLateFrame;
        private bool _registeredOriginListener;
        private bool _registeredHotSwapListener;
        private bool _pendingScheduled;
        private bool _pendingDiscard;
        private bool _pendingProfileReadFence;
        private bool _pendingSocketModuleReadFence;
        private bool _drawBoundsValid;
        private bool _vaultInitialized;
        private bool _encodedSdfHandleValid;
        private bool _mockSdfGenerated;
        private bool _originSnapshotValid;
        private int _pendingSlotCount;
        private int _pendingVaultLockCount;
        private int _uploadedSlotCount;
        private int _capacityResolved;
        private int _writeBufferIndex;
        private uint _frameCounter;
        private long _pendingScheduleTicks;
        private Color _lastBaseColor;
        private Color _lastEmbeddedColor;
        private bool _materialColorsApplied;

        private static readonly int PylonMatricesId = Shader.PropertyToID("_H8FoundationPylonMatrices");
        private static readonly int PylonSurfacesId = Shader.PropertyToID("_H8FoundationPylonSurfaces");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int EmbeddedColorId = Shader.PropertyToID("_EmbeddedColor");
        private static readonly int CameraWorldOffsetId = Shader.PropertyToID("_H8FoundationPylonCameraWorldOffset");

        private void Awake()
        {
            ConfigureSignalLane();
            CachePlayerContextCold();
            EnsureCameraCold();
            RefreshOriginSnapshotCold();
            EnsureMaterial();
            EnsureGraphicsBuffers();
        }

        private void OnEnable()
        {
            ConfigureSignalLane();
            if (!Application.isPlaying)
                return;

            CachePlayerContextCold();
            TryRegisterHotSwapListener();
            EnsureCameraCold();
            RefreshOriginSnapshotCold();
            EnsureBuffersCold();
            if (!_registeredOriginListener)
            {
                HectonFloatingOrigin.RegisterListener(this);
                _registeredOriginListener = true;
            }

            EnsureMaterial();
            EnsureGraphicsBuffers();
            _registeredRenderable = GlobalRegistry.Renderables.TryRegister(this);
            _registeredLateFrame = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.UI);
        }

        private void OnDisable()
        {
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

            if (_registeredOriginListener)
            {
                HectonFloatingOrigin.UnregisterListener(this);
                _registeredOriginListener = false;
            }

            TryUnregisterHotSwapListener();
            CompletePendingForTeardown();
            _uploadedSlotCount = 0;
            _drawBoundsValid = false;
        }

        private void OnDestroy()
        {
            TryUnregisterHotSwapListener();
            CompletePendingForTeardown();
            ReleaseGraphicsBuffer(ref _matrixBufferA);
            ReleaseGraphicsBuffer(ref _matrixBufferB);
            ReleaseGraphicsBuffer(ref _surfaceBufferA);
            ReleaseGraphicsBuffer(ref _surfaceBufferB);
            ReleaseGraphicsBuffer(ref _argsBufferA);
            ReleaseGraphicsBuffer(ref _argsBufferB);
            _boundMatrixBuffer = null;
            _boundSurfaceBuffer = null;
            _vault = null;
            _encodedSdfHandle = default;
            _vaultInitialized = false;
            _encodedSdfHandleValid = false;

            if (pylonMaterial != null && pylonMaterial.hideFlags == HideFlags.DontSave)
                Destroy(pylonMaterial);
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (!Application.isPlaying ||
                _vault == null ||
                !_originSnapshotValid ||
                !FoundationSnappingCalculatorRuntime.TryReadVaultViews(_vault, out FoundationSnappingVaultViews views) ||
                !views.DebugRays.IsCreated ||
                _uploadedSlotCount <= 0)
            {
                return;
            }

            double3 origin = _cachedOriginAup;
            int count = math.min(_uploadedSlotCount, views.DebugRays.Length);
            for (int i = 0; i < count; i++)
            {
                FoundationDebugRayDTO ray = views.DebugRays[i];
                if ((ray.Flags & FoundationPylonFlags.Active) == 0u &&
                    (ray.Flags & FoundationPylonFlags.ExtensionCulled) == 0u &&
                    (ray.Flags & FoundationPylonFlags.OutOfSdfBounds) == 0u)
                {
                    continue;
                }

                Vector3 start = ToRuntime(ray.OriginAup, origin);
                Vector3 end = ToRuntime(ray.HitAup, origin);
                Gizmos.color = (ray.Flags & FoundationPylonFlags.Active) != 0u ? Color.cyan : Color.red;
                Gizmos.DrawLine(start, end);
                Gizmos.DrawWireSphere(end, 0.08f);
            }
        }
#endif

        public void Render(float deltaTime)
        {
            DrawPreparedBatch();
        }

        public void LateFrameTick()
        {
            TryFinalizePendingAndUpload();
            TrySchedulePylonBuild();
        }

        private static void ConfigureSignalLane()
        {
            SignalBus<FoundationStructuralWarningSignal>.Configure(
                expectedCapacity: 8,
                maxFrameSignals: 32,
                lowTierFrameSignals: 32,
                laneHash: FoundationStructuralWarningSignal.LaneHash);
            SignalBus<FoundationStructuralWarningSignal>.EnsureInitialized();
        }

        private bool TrySchedulePylonBuild()
        {
            if (_pendingScheduled)
                return false;

            if (_vault == null ||
                !_vaultInitialized ||
                pylonMaterial == null ||
                !HasGraphicsBuffers() ||
                !FoundationSnappingCalculatorRuntime.TryReadVaultViews(_vault, out FoundationSnappingVaultViews foundationViews))
            {
                return false;
            }

            int moduleCount = ResolveMaxModules();
            bool useSocketInputs = false;
            if (moduleCount <= 0)
            {
                ClearUploadedBatch();
                return false;
            }

            if (!TryResolveCameraAup(out double3 cameraAup, out Vector3 cameraWorldOffset))
                return false;

            FoundationSdfConfigDTO sdfConfig = foundationViews.SdfConfig.IsCreated && foundationViews.SdfConfig.Length > 0
                ? foundationViews.SdfConfig[0]
                : FoundationSnappingCalculatorRuntime.CreateDefaultMockSdfConfig(cameraAup);
            sdfConfig = FoundationSnappingCalculatorRuntime.SanitizeSdfConfig(sdfConfig);
            bool usingRealSdf = TryResolveEncodedVoxelSdf(sdfConfig, out NativeArray<byte> encodedSdf);
            if (!TryBeginSocketModuleReadFenceForSchedule(out useSocketInputs))
                return false;

            if (!TryBeginVaultJobLocks(useSocketInputs, usingRealSdf))
            {
                ReleasePendingSocketModuleReadFence();
                return false;
            }

            if (!FoundationSnappingCalculatorRuntime.TryBeginProfileReadFence())
            {
                ReleasePendingSocketModuleReadFence();
                ReleasePendingVaultJobLocks();
                return false;
            }

            _pendingProfileReadFence = true;
            bool scheduleCommitted = false;
            bool hasScheduledHandle = false;
            JobHandle lastScheduledHandle = default;
            try
            {
            bool lockedSocketInputs = useSocketInputs;
            if (!FoundationSnappingCalculatorRuntime.TryReadVaultViews(_vault, out foundationViews) ||
                !TryPrepareModuleInputs(
                    foundationViews,
                    lockedSocketInputs,
                    out moduleCount,
                    out useSocketInputs,
                    out ConstructionSocketVaultViews socketViews))
            {
                ReleasePendingProfileReadFence();
                ReleasePendingVaultJobLocks();
                return false;
            }

            if (moduleCount <= 0)
            {
                ClearUploadedBatch();
                ReleasePendingProfileReadFence();
                ReleasePendingVaultJobLocks();
                return false;
            }

            FoundationTuningDTO tuning = foundationViews.Tuning.IsCreated && foundationViews.Tuning.Length > 0
                ? foundationViews.Tuning[0]
                : FoundationSnappingCalculatorRuntime.CreateDefaultTuning(FoundationSnappingCalculatorRuntime.ResolveGlobalQualityWeight());
            tuning = FoundationSnappingCalculatorRuntime.SanitizeTuning(
                tuning,
                FoundationSnappingCalculatorRuntime.ResolveGlobalQualityWeight());
            tuning.Frame = CaptureFrameId();
            if (foundationViews.Tuning.IsCreated && foundationViews.Tuning.Length > 0)
                foundationViews.Tuning[0] = tuning;

            sdfConfig = foundationViews.SdfConfig.IsCreated && foundationViews.SdfConfig.Length > 0
                ? FoundationSnappingCalculatorRuntime.SanitizeSdfConfig(foundationViews.SdfConfig[0])
                : FoundationSnappingCalculatorRuntime.CreateDefaultMockSdfConfig(cameraAup);
            if (usingRealSdf && !TryResolveEncodedVoxelSdf(sdfConfig, out encodedSdf))
            {
                usingRealSdf = false;
                encodedSdf = default;
            }

            JobHandle dependency = default;
            if (useSocketInputs)
            {
                dependency = new BuildFoundationModulesFromSocketModulesJob
                {
                    SocketModules = socketViews.Modules,
                    FoundationModules = foundationViews.Modules,
                    ModuleCount = moduleCount
                }.Schedule(moduleCount, 32);
                lastScheduledHandle = dependency;
                hasScheduledHandle = true;
            }

            if (!usingRealSdf)
            {
                sdfConfig = FoundationSnappingCalculatorRuntime.CreateDefaultMockSdfConfig(cameraAup);
                if (foundationViews.SdfConfig.IsCreated && foundationViews.SdfConfig.Length > 0)
                    foundationViews.SdfConfig[0] = sdfConfig;

                if (!_mockSdfGenerated && foundationViews.MockSdfDistances.IsCreated)
                {
                    dependency = new GenerateMockSeafloorSDFJob
                    {
                        Distances = foundationViews.MockSdfDistances,
                        Config = sdfConfig
                    }.Schedule(foundationViews.MockSdfDistances.Length, 128, dependency);
                    lastScheduledHandle = dependency;
                    hasScheduledHandle = true;
                    _mockSdfGenerated = true;
                }
            }

            int slotCount = math.min(
                moduleCount * FoundationSnappingCalculatorRuntime.MaxRaysPerModule,
                FoundationSnappingCalculatorRuntime.PylonCapacity);
            JobHandle pylonHandle = new CalculateFoundationPylonsJob
            {
                Modules = foundationViews.Modules,
                MockSdfDistances = foundationViews.MockSdfDistances,
                EncodedVoxelSdfTexture3D = encodedSdf,
                RayOrigins = foundationViews.RayOrigins,
                ProfileRanges = foundationViews.ProfileRanges,
                PylonMatrices = foundationViews.PylonMatrices,
                PylonSurfaces = foundationViews.PylonSurfaces,
                PerModuleCounters = foundationViews.PerModuleCounters,
                DebugRays = foundationViews.DebugRays,
                SdfConfig = sdfConfig,
                Tuning = tuning,
                CameraAup = cameraAup,
                ModuleCount = moduleCount,
                ProfileCount = FoundationSnappingCalculatorRuntime.GetLoadedProfileCount(),
                RayOriginCount = FoundationSnappingCalculatorRuntime.GetLoadedRayOriginCount(),
                UseEncodedByteSdf = usingRealSdf ? 1 : 0
            }.Schedule(moduleCount, 16, dependency);
            lastScheduledHandle = pylonHandle;
            hasScheduledHandle = true;

            JobHandle reduceHandle = new ReduceFoundationPylonCountersJob
            {
                PerModuleCounters = foundationViews.PerModuleCounters,
                FrameCounters = foundationViews.FrameCounters,
                ModuleCount = moduleCount
            }.Schedule(pylonHandle);
            lastScheduledHandle = reduceHandle;

            JobHandle compactHandle = new CompactFoundationPylonDrawListJob
            {
                PylonMatrices = foundationViews.PylonMatrices,
                PylonSurfaces = foundationViews.PylonSurfaces,
                FrameCounters = foundationViews.FrameCounters,
                SlotCount = slotCount
            }.Schedule(reduceHandle);
            lastScheduledHandle = compactHandle;

            _pendingHandle = new BuildFoundationPylonIndirectArgsJob
            {
                FrameCounters = foundationViews.FrameCounters,
                Args = foundationViews.IndirectArgs,
                SlotCount = slotCount
            }.Schedule(compactHandle);
            lastScheduledHandle = _pendingHandle;
            _pendingScheduled = true;
            _pendingDiscard = false;
            _pendingSlotCount = slotCount;
            _pendingCameraWorldOffset = cameraWorldOffset;
            _pendingScheduleTicks = Stopwatch.GetTimestamp();
            scheduleCommitted = true;
            return true;
            }
            finally
            {
                if (!scheduleCommitted)
                {
                    if (hasScheduledHandle)
                        DispatcherJobFence.TryComplete(ref lastScheduledHandle, forceComplete: true);

                    _pendingScheduled = false;
                    _pendingDiscard = false;
                    _pendingSlotCount = 0;
                    _pendingCameraWorldOffset = Vector3.zero;
                    ReleasePendingProfileReadFence();
                    ReleasePendingSocketModuleReadFence();
                    ReleasePendingVaultJobLocks();
                }
            }
        }

        private bool TryFinalizePendingAndUpload()
        {
            if (!_pendingScheduled)
                return false;

            if (!DispatcherJobFence.TryFinalizeCompleted(ref _pendingHandle))
                return false;

            ReleasePendingProfileReadFence();
            ReleasePendingSocketModuleReadFence();
            int uploadSlots = _pendingDiscard ? 0 : _pendingSlotCount;
            Vector3 cameraWorldOffset = _pendingCameraWorldOffset;
            _pendingScheduled = false;
            _pendingDiscard = false;
            _pendingSlotCount = 0;
            _pendingCameraWorldOffset = Vector3.zero;
            if (uploadSlots <= 0)
            {
                ClearUploadedBatch();
                ReleasePendingVaultJobLocks();
                return true;
            }

            if (_vault == null ||
                !FoundationSnappingCalculatorRuntime.TryReadVaultViews(_vault, out FoundationSnappingVaultViews views) ||
                !HasGraphicsBuffers())
            {
                ClearUploadedBatch();
                ReleasePendingVaultJobLocks();
                return false;
            }

            FoundationPylonFrameCounters counters = views.FrameCounters.IsCreated && views.FrameCounters.Length > 0
                ? views.FrameCounters[0]
                : default;
            int activeUploadSlots = math.min(uploadSlots, math.max(0, counters.SlotCount));
            int writeCount = math.min(activeUploadSlots, math.min(views.PylonMatrices.Length, views.PylonSurfaces.Length));
            _uploadedCameraWorldOffset = cameraWorldOffset;
            UpdateDrawBounds(views.PylonMatrices, views.PylonSurfaces, writeCount, cameraWorldOffset);
            if (!_drawBoundsValid)
            {
                _uploadedSlotCount = 0;
                ReleasePendingVaultJobLocks();
                return true;
            }

            GraphicsBufferUploadUtility.UploadNativeArray(ResolveWriteMatrixBuffer(), views.PylonMatrices, writeCount);
            GraphicsBufferUploadUtility.UploadNativeArray(ResolveWriteSurfaceBuffer(), views.PylonSurfaces, writeCount);
            GraphicsBufferUploadUtility.UploadNativeArray(ResolveWriteArgsBuffer(), views.IndirectArgs, 1);
            _uploadedSlotCount = writeCount;
            _writeBufferIndex ^= 1;

            double3 firstAup = views.Modules.IsCreated && views.Modules.Length > 0 ? views.Modules[0].CenterAup : double3.zero;
            float elapsedUs = ResolveElapsedMicroseconds(_pendingScheduleTicks);
            FoundationSnappingCalculatorRuntime.WriteTelemetry(
                views.Telemetry,
                views.TelemetryCursor,
                firstAup,
                CaptureFrameId(),
                in counters,
                elapsedUs,
                views.Tuning.IsCreated && views.Tuning.Length > 0 ? views.Tuning[0].GlobalQualityWeight : 1f);

            if ((counters.Flags & FoundationPylonFlags.NonFinite) != 0u)
                FoundationSnappingCalculatorRuntime.DumpTelemetry(views.Telemetry);

            if ((counters.Flags & WarningMask) != 0u)
                PublishStructuralWarning(firstAup, in counters, views.Tuning.IsCreated && views.Tuning.Length > 0 ? views.Tuning[0] : FoundationSnappingCalculatorRuntime.CreateDefaultTuning(1f));

            ReleasePendingVaultJobLocks();
            return true;
        }

        private bool TryPrepareModuleInputs(
            FoundationSnappingVaultViews foundationViews,
            bool allowSocketInputs,
            out int moduleCount,
            out bool useSocketInputs,
            out ConstructionSocketVaultViews socketViews)
        {
            moduleCount = 0;
            useSocketInputs = false;
            socketViews = default;
            if (!allowSocketInputs ||
                !ShinobuSocketConstructionRuntime.TryReadVaultViews(_vault, out socketViews) ||
                !socketViews.Modules.IsCreated ||
                !socketViews.Counters.IsCreated ||
                socketViews.Counters.Length <= 0)
            {
                return TryPopulatePreviewFallback(foundationViews.Modules, out moduleCount);
            }

            moduleCount = math.clamp(socketViews.Counters[0], 0, math.min(socketViews.Modules.Length, math.min(foundationViews.Modules.Length, ResolveMaxModules())));
            if (moduleCount <= 0)
                return TryPopulatePreviewFallback(foundationViews.Modules, out moduleCount);

            useSocketInputs = true;
            return true;
        }

        private bool TryBeginSocketModuleReadFenceForSchedule(out bool useSocketInputs)
        {
            useSocketInputs = false;
            if (!ShinobuSocketConstructionRuntime.TryBeginModuleReadFence())
                return true;

            if (ShinobuSocketConstructionRuntime.TryReadVaultViews(_vault, out ConstructionSocketVaultViews socketViews) &&
                socketViews.Modules.IsCreated &&
                socketViews.Counters.IsCreated &&
                socketViews.Counters.Length > 0)
            {
                int socketModuleCount = math.clamp(socketViews.Counters[0], 0, math.min(socketViews.Modules.Length, ResolveMaxModules()));
                if (socketModuleCount > 0)
                {
                    _pendingSocketModuleReadFence = true;
                    useSocketInputs = true;
                    return true;
                }
            }

            ShinobuSocketConstructionRuntime.EndModuleReadFence();
            return true;
        }

        private bool TryPopulatePreviewFallback(NativeArray<FoundationModuleAupDTO> modules, out int moduleCount)
        {
            moduleCount = 0;
            if (!modules.IsCreated || modules.Length <= 0)
                return false;

            ReadOnlySpan<ConstructionPreviewSignal> signals = SignalBus<ConstructionPreviewSignal>.GetFrameSnapshot();
            for (int i = 0; i < signals.Length; i++)
            {
                ConstructionPreviewSignal signal = signals[i];
                if ((signal.Flags & ConstructionPreviewSignal.FlagActive) == 0 ||
                    !AbsoluteUniversePosition.IsFinite(in signal.CenterAup))
                {
                    continue;
                }

                FoundationModuleAupDTO module;
                module.CenterAup = signal.CenterAup.ToAbsoluteDouble3();
                module.Rotation = new quaternion(signal.Rotation.x, signal.Rotation.y, signal.Rotation.z, signal.Rotation.w);
                module.BoundsExtents = math.max(math.abs(signal.Scale) * 0.5f, new float3(0.5f));
                module.GroundClearanceMeters = 0.05f;
                module.ModuleHash = signal.ModuleHash;
                module.Flags = FoundationPylonFlags.Active | FoundationPylonFlags.PresentationOnly | FoundationPylonFlags.RollbackExcluded;
                modules[0] = module;
                moduleCount = 1;
                return true;
            }

            return true;
        }

        private bool TryResolveEncodedVoxelSdf(FoundationSdfConfigDTO config, out NativeArray<byte> encodedSdf)
        {
            encodedSdf = default;
            if (_vault == null ||
                !_encodedSdfHandleValid ||
                !_vault.TryReadHandle(in _encodedSdfHandle, out encodedSdf) ||
                !encodedSdf.IsCreated)
            {
                return false;
            }

            long sx = math.max(1, config.SizeX);
            long sy = math.max(1, config.SizeY);
            long sz = math.max(1, config.SizeZ);
            if (sx > int.MaxValue / sy)
                return false;

            long slice = sx * sy;
            if (slice > int.MaxValue / sz)
                return false;

            long expected = slice * sz;
            return expected > 0L &&
                   expected <= int.MaxValue &&
                   encodedSdf.Length >= expected;
        }

        private void DrawPreparedBatch()
        {
            if (_uploadedSlotCount <= 0 || pylonMaterial == null || !_drawBoundsValid || !IsDrawBoundsCameraVisible())
                return;

            GraphicsBuffer matrixBuffer = _writeBufferIndex == 0 ? _matrixBufferB : _matrixBufferA;
            GraphicsBuffer surfaceBuffer = _writeBufferIndex == 0 ? _surfaceBufferB : _surfaceBufferA;
            GraphicsBuffer argsBuffer = _writeBufferIndex == 0 ? _argsBufferB : _argsBufferA;
            if (matrixBuffer == null || surfaceBuffer == null || argsBuffer == null)
                return;

            if (!ReferenceEquals(_boundMatrixBuffer, matrixBuffer))
            {
                pylonMaterial.SetBuffer(PylonMatricesId, matrixBuffer);
                _boundMatrixBuffer = matrixBuffer;
            }

            if (!ReferenceEquals(_boundSurfaceBuffer, surfaceBuffer))
            {
                pylonMaterial.SetBuffer(PylonSurfacesId, surfaceBuffer);
                _boundSurfaceBuffer = surfaceBuffer;
            }

            ApplyMaterialStateIfNeeded();
            UnityEngine.Graphics.DrawProceduralIndirect(
                pylonMaterial,
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

        private void EnsureBuffersCold()
        {
            if (_vault == null)
                _vault = GlobalRegistry.DataVault;

            if (_vault == null || _vaultInitialized)
                return;

            if (!_originSnapshotValid)
                RefreshOriginSnapshotCold();

            if (!_originSnapshotValid ||
                !FoundationSnappingCalculatorRuntime.InitializeVault(_vault, _cachedOriginAup))
                return;

            TryCacheEncodedVoxelSdfHandleCold();
            _vaultInitialized = true;
        }

        private void TryCacheEncodedVoxelSdfHandleCold()
        {
            _encodedSdfHandle = default;
            _encodedSdfHandleValid = _vault != null &&
                                     _vault.TryGetGenerationHandle<byte>(BufferID.VoxelSdfTexture3D, out _encodedSdfHandle);
        }

        private void EnsureGraphicsBuffers()
        {
            int resolvedCapacity = ResolvePylonCapacity();
            if (_matrixBufferA != null &&
                _matrixBufferB != null &&
                _surfaceBufferA != null &&
                _surfaceBufferB != null &&
                _argsBufferA != null &&
                _argsBufferB != null &&
                _capacityResolved >= resolvedCapacity)
            {
                return;
            }

            ReleaseGraphicsBuffer(ref _matrixBufferA);
            ReleaseGraphicsBuffer(ref _matrixBufferB);
            ReleaseGraphicsBuffer(ref _surfaceBufferA);
            ReleaseGraphicsBuffer(ref _surfaceBufferB);
            ReleaseGraphicsBuffer(ref _argsBufferA);
            ReleaseGraphicsBuffer(ref _argsBufferB);
            _capacityResolved = resolvedCapacity;
            // COLD ALLOC: GraphicsBuffer[6] - recreated only on capacity changes outside the Burst hot path - owner: FoundationPylonGpuBatch
            _matrixBufferA = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<PylonMatrixDTO>(resolvedCapacity);
            _matrixBufferB = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<PylonMatrixDTO>(resolvedCapacity);
            _surfaceBufferA = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<FoundationPylonSurfaceDTO>(resolvedCapacity);
            _surfaceBufferB = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<FoundationPylonSurfaceDTO>(resolvedCapacity);
            _argsBufferA = CreateIndirectArgsBuffer();
            _argsBufferB = CreateIndirectArgsBuffer();
            _boundMatrixBuffer = null;
            _boundSurfaceBuffer = null;
        }

        private void EnsureMaterial()
        {
            if (pylonMaterial != null)
                return;

#if UNITY_EDITOR
            if (pylonShader == null)
                pylonShader = AssetDatabase.LoadAssetAtPath<Shader>(PylonShaderPath);
#endif

            if (pylonShader == null)
                return;

            // COLD ALLOC: Material[1] - runtime pylon shader material fallback - owner: FoundationPylonGpuBatch
            pylonMaterial = new Material(pylonShader)
            {
                enableInstancing = false,
                hideFlags = HideFlags.DontSave
            };
        }

        private void EnsureCameraCold()
        {
            if (targetCamera != null)
                return;

            IPlayerRuntimeContext playerContext = _cachedPlayerContext;
            targetCamera = playerContext != null ? playerContext.PlayerCamera : null;
        }

        private void CachePlayerContextCold()
        {
            _cachedPlayerContext = GlobalRegistry.Player;
        }

        private void TryRegisterHotSwapListener()
        {
            if (_registeredHotSwapListener)
                return;

            _registeredHotSwapListener = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterHotSwapListener()
        {
            if (!_registeredHotSwapListener)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _registeredHotSwapListener = false;
        }

        private void RefreshOriginSnapshotCold()
        {
            _cachedOriginAup = HectonFloatingOrigin.CurrentTotalOffsetDouble;
            _originSnapshotValid = math.all(math.isfinite(_cachedOriginAup));
        }

        private void ApplyMaterialStateIfNeeded()
        {
            if (pylonMaterial == null)
                return;

            if (!_materialColorsApplied || _lastBaseColor != baseColor)
            {
                pylonMaterial.SetColor(BaseColorId, baseColor);
                _lastBaseColor = baseColor;
            }

            if (!_materialColorsApplied || _lastEmbeddedColor != embeddedColor)
            {
                pylonMaterial.SetColor(EmbeddedColorId, embeddedColor);
                _lastEmbeddedColor = embeddedColor;
            }

            if (!_materialColorsApplied || _lastCameraWorldOffset != _uploadedCameraWorldOffset)
            {
                Vector3 offset = _uploadedCameraWorldOffset;
                pylonMaterial.SetVector(CameraWorldOffsetId, new Vector4(offset.x, offset.y, offset.z, 0f));
                _lastCameraWorldOffset = offset;
            }

            _materialColorsApplied = true;
        }

        private void UpdateDrawBounds(
            NativeArray<PylonMatrixDTO> matrices,
            NativeArray<FoundationPylonSurfaceDTO> surfaces,
            int count,
            Vector3 cameraWorldOffset)
        {
            _drawBoundsValid = false;
            if (!matrices.IsCreated || !surfaces.IsCreated || count <= 0)
                return;

            float3 worldOffset = new float3(cameraWorldOffset.x, cameraWorldOffset.y, cameraWorldOffset.z);
            float3 min = new float3(float.MaxValue);
            float3 max = new float3(float.MinValue);
            bool found = false;
            int safeCount = math.min(count, math.min(matrices.Length, surfaces.Length));
            for (int i = 0; i < safeCount; i++)
            {
                FoundationPylonSurfaceDTO surface = surfaces[i];
                if ((surface.Flags & FoundationPylonFlags.Active) == 0u)
                    continue;

                float4x4 matrix = matrices[i].LocalToWorld;
                float3 center = matrix.c3.xyz + worldOffset;
                float lateralInflation = 1f + math.saturate(surface.SurfaceNormalFlare.w);
                float3 extents =
                    math.abs(matrix.c0.xyz) * (0.5f * lateralInflation) +
                    math.abs(matrix.c1.xyz) * 0.5f +
                    math.abs(matrix.c2.xyz) * (0.5f * lateralInflation);
                if (!math.all(math.isfinite(center)) || !math.all(math.isfinite(extents)) || math.lengthsq(extents) <= 0.000001f)
                    continue;

                min = math.min(min, center - extents);
                max = math.max(max, center + extents);
                found = true;
            }

            if (!found)
                return;

            float3 size = math.max(max - min, new float3(0.001f));
            float3 centerBounds = (min + max) * 0.5f;
            _drawBounds = new Bounds(
                new Vector3(centerBounds.x, centerBounds.y, centerBounds.z),
                new Vector3(size.x, size.y, size.z));
            _drawBoundsValid = true;
        }

        private void PublishStructuralWarning(double3 firstAup, in FoundationPylonFrameCounters counters, FoundationTuningDTO tuning)
        {
            FoundationStructuralWarningSignal signal;
            signal.ModuleAup = firstAup;
            signal.ModuleHash = 0u;
            signal.WarningFlags = counters.Flags & WarningMask;
            signal.RequestedLengthMeters = counters.MaxResolvedLength;
            signal.MaxLengthMeters = tuning.MaxPylonLengthMeters;
            signal.Frame = CaptureFrameId();
            signal.ResultHash = counters.ResultHash;
            signal._pad0 = 0ul;
            signal._pad1 = 0ul;
            SignalBus<FoundationStructuralWarningSignal>.TryPush(in signal);
        }

        public void OnOriginShift(in OriginShiftEventData shiftData)
        {
            _cachedOriginAup = shiftData.NewTotalOffsetDouble;
            _originSnapshotValid = math.all(math.isfinite(_cachedOriginAup));
            _pendingDiscard = _pendingScheduled;
            ClearUploadedBatch();
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot != GlobalRegistryServiceSlot.Player)
                return;

            Camera previousCamera = _cachedPlayerContext != null ? _cachedPlayerContext.PlayerCamera : null;
            _cachedPlayerContext = currentService as IPlayerRuntimeContext;
            if (targetCamera == null || ReferenceEquals(targetCamera, previousCamera))
                targetCamera = _cachedPlayerContext != null ? _cachedPlayerContext.PlayerCamera : null;
        }

        private bool HasGraphicsBuffers()
        {
            return _matrixBufferA != null &&
                   _matrixBufferB != null &&
                   _surfaceBufferA != null &&
                   _surfaceBufferB != null &&
                   _argsBufferA != null &&
                   _argsBufferB != null &&
                   _capacityResolved >= ResolvePylonCapacity();
        }

        private int ResolveMaxModules()
        {
            return math.clamp(maxModules, 1, FoundationSnappingCalculatorRuntime.ModuleCapacity);
        }

        private int ResolvePylonCapacity()
        {
            return ResolveMaxModules() * FoundationSnappingCalculatorRuntime.MaxRaysPerModule;
        }

        private uint CaptureFrameId()
        {
            uint frame = TimeSliceScheduler.CurrentFrameId;
            if (frame != 0u)
            {
                if (frame > _frameCounter)
                    _frameCounter = frame;
                return frame;
            }

            _frameCounter = unchecked(_frameCounter + 1u);
            return _frameCounter != 0u ? _frameCounter : 1u;
        }

        private bool TryResolveCameraAup(out double3 cameraAup, out Vector3 cameraWorldOffset)
        {
            cameraAup = _cachedOriginAup;
            cameraWorldOffset = Vector3.zero;
            if (!_originSnapshotValid || !math.all(math.isfinite(_cachedOriginAup)))
                return false;

            if (targetCamera == null || targetCamera.transform == null)
                return false;

            Vector3 p = targetCamera.transform.position;
            cameraWorldOffset = p;
            cameraAup = _cachedOriginAup + new double3(p.x, p.y, p.z);
            return math.all(math.isfinite(cameraAup));
        }

        private bool IsDrawBoundsCameraVisible()
        {
            if (targetCamera == null || targetCamera.transform == null)
                return true;

            Vector3 localCenter = targetCamera.transform.InverseTransformPoint(_drawBounds.center);
            float radius = _drawBounds.extents.magnitude;
            return localCenter.z + radius >= targetCamera.nearClipPlane &&
                   localCenter.z - radius <= targetCamera.farClipPlane;
        }

        private static float ResolveElapsedMicroseconds(long startTicks)
        {
            long delta = Stopwatch.GetTimestamp() - startTicks;
            if (delta <= 0)
                return 0f;

            return (float)(delta * 1000000.0 / Stopwatch.Frequency);
        }

        private GraphicsBuffer ResolveWriteMatrixBuffer()
        {
            return _writeBufferIndex == 0 ? _matrixBufferA : _matrixBufferB;
        }

        private GraphicsBuffer ResolveWriteSurfaceBuffer()
        {
            return _writeBufferIndex == 0 ? _surfaceBufferA : _surfaceBufferB;
        }

        private GraphicsBuffer ResolveWriteArgsBuffer()
        {
            return _writeBufferIndex == 0 ? _argsBufferA : _argsBufferB;
        }

        private bool TryBeginVaultJobLocks(bool includeSocketInputs, bool includeEncodedSdf)
        {
            ReleasePendingVaultJobLocks();
            if (_vault == null)
                return false;

            return TryAddVaultJobLock(FoundationSnappingCalculatorRuntime.ModuleBufferId) &&
                   TryAddVaultJobLock(FoundationSnappingCalculatorRuntime.PylonMatrixBufferId) &&
                   TryAddVaultJobLock(FoundationSnappingCalculatorRuntime.PylonSurfaceBufferId) &&
                   TryAddVaultJobLock(FoundationSnappingCalculatorRuntime.PerModuleCounterBufferId) &&
                   TryAddVaultJobLock(FoundationSnappingCalculatorRuntime.FrameCounterBufferId) &&
                   TryAddVaultJobLock(FoundationSnappingCalculatorRuntime.TelemetryBufferId) &&
                   TryAddVaultJobLock(FoundationSnappingCalculatorRuntime.TelemetryCursorBufferId) &&
                   TryAddVaultJobLock(FoundationSnappingCalculatorRuntime.TuningBufferId) &&
                   TryAddVaultJobLock(FoundationSnappingCalculatorRuntime.MockSdfDistanceBufferId) &&
                   TryAddVaultJobLock(FoundationSnappingCalculatorRuntime.SdfConfigBufferId) &&
                   TryAddVaultJobLock(FoundationSnappingCalculatorRuntime.RayOriginBufferId) &&
                   TryAddVaultJobLock(FoundationSnappingCalculatorRuntime.ProfileRangeBufferId) &&
                   TryAddVaultJobLock(FoundationSnappingCalculatorRuntime.DebugRayBufferId) &&
                   TryAddVaultJobLock(FoundationSnappingCalculatorRuntime.IndirectArgsBufferId) &&
                   (!includeSocketInputs ||
                    (TryAddVaultJobLock(BufferID.ConstructionSocketModules) &&
                     TryAddVaultJobLock(BufferID.ConstructionSocketCounters))) &&
                   (!includeEncodedSdf || TryAddVaultJobLock(BufferID.VoxelSdfTexture3D));
        }

        private bool TryAddVaultJobLock(BufferID bufferId)
        {
            if (_pendingVaultLockCount >= _pendingVaultLocks.Length ||
                _vault == null ||
                !_vault.TryLockBuffer(bufferId, SystemID.Construction))
            {
                ReleasePendingVaultJobLocks();
                return false;
            }

            _pendingVaultLocks[_pendingVaultLockCount++] = bufferId;
            return true;
        }

        private void ReleasePendingVaultJobLocks()
        {
            if (_vault == null || _pendingVaultLockCount <= 0)
            {
                _pendingVaultLockCount = 0;
                return;
            }

            for (int i = _pendingVaultLockCount - 1; i >= 0; i--)
            {
                BufferID bufferId = _pendingVaultLocks[i];
                if (bufferId != BufferID.Unknown)
                    _vault.TryUnlockBuffer(bufferId, SystemID.Construction);
                _pendingVaultLocks[i] = BufferID.Unknown;
            }

            _pendingVaultLockCount = 0;
        }

        private void ReleasePendingProfileReadFence()
        {
            if (!_pendingProfileReadFence)
                return;

            FoundationSnappingCalculatorRuntime.EndProfileReadFence();
            _pendingProfileReadFence = false;
        }

        private void ReleasePendingSocketModuleReadFence()
        {
            if (!_pendingSocketModuleReadFence)
                return;

            ShinobuSocketConstructionRuntime.EndModuleReadFence();
            _pendingSocketModuleReadFence = false;
        }

        private static GraphicsBuffer CreateIndirectArgsBuffer()
        {
            // COLD ALLOC: GraphicsBuffer[1] - one indirect argument row per ping-pong buffer - owner: FoundationPylonGpuBatch
            return new GraphicsBuffer(
                GraphicsBuffer.Target.IndirectArguments,
                GraphicsBuffer.UsageFlags.LockBufferForWrite,
                1,
                UnsafeUtility.SizeOf<FoundationPylonIndirectArgsDTO>());
        }

        private void ClearUploadedBatch()
        {
            _uploadedSlotCount = 0;
            _drawBoundsValid = false;
        }

        private void CompletePendingForTeardown()
        {
            if (!_pendingScheduled)
            {
                ReleasePendingProfileReadFence();
                ReleasePendingSocketModuleReadFence();
                ReleasePendingVaultJobLocks();
                return;
            }

            DispatcherJobFence.TryComplete(ref _pendingHandle, forceComplete: true);
            ReleasePendingProfileReadFence();
            ReleasePendingSocketModuleReadFence();
            _pendingScheduled = false;
            _pendingDiscard = false;
            _pendingSlotCount = 0;
            _pendingCameraWorldOffset = Vector3.zero;
            ReleasePendingVaultJobLocks();
        }

        private static void ReleaseGraphicsBuffer(ref GraphicsBuffer buffer)
        {
            if (buffer == null)
                return;

            buffer.Release();
            buffer = null;
        }

        private static Vector3 ToRuntime(double3 aup, double3 origin)
        {
            double3 local = aup - origin;
            return new Vector3((float)local.x, (float)local.y, (float)local.z);
        }
    }
}
