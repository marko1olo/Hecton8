using Hecton8.Core;
using Hecton8.Core.Contracts;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Memory;
using System;
using System.Collections.Generic;
using Hecton8.Gameplay;
using Hecton8.World;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace Hecton8.Physics
{
    /// <summary>
    /// Player-owned tether runtime host.
    /// Physics executes in <see cref="FixedTick(float)"/> and visuals render in <see cref="LateFrameTick"/>.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/Physics/Tether Manager")]
    public sealed class TetherManager : MonoBehaviour, IFixedTickable, ILateFrameTickable, ISlowTickable, IOriginShiftListener, IGlobalRegistryHotSwapListener
    {
        private static readonly int _TetherPositionsId = Shader.PropertyToID("_TetherPositions");
        private static readonly int _TetherSegmentTensionsId = Shader.PropertyToID("_TetherSegmentTensions");
        private static readonly int _TetherDrawParamsId = Shader.PropertyToID("_TetherDrawParams");
        private const int TetherBlackBoxCapacity = 300;
        private const int MaxManagedTetherInstances = 64;
        private const int InitialPooledTetherInstances = 64;
        private const string TetherBlackBoxDumpRelativePath = "Docs/AgentLogs/Dump_VERLET_CABLES_MANAGER.bin";
        private const string TetherBlackBoxH8DumpRelativePath = "Docs/AgentLogs/Dump_VERLET_CABLES_MANAGER.h8dump";
        private const ulong TetherBlackBoxMagic = 0x4D47524D48544554ul;
        private const float TetherFixedClockWrapSeconds = 4096f;
        private const uint Shinobu143AupPinNodes = 1u << 0;
        private const uint Shinobu143AupPinConstraints = 1u << 1;
        private const uint Shinobu143AupPinEndpoints = 1u << 2;
        private const uint Shinobu143AupPinSegmentTensions = 1u << 3;
        private const uint Shinobu143AupPinSolverStats = 1u << 4;
        private const uint Shinobu143AupPinForcePackets = 1u << 5;
        private const uint Shinobu143AupPinTelemetryRing = 1u << 6;
        private const uint Shinobu143AupPinTelemetryHead = 1u << 7;
        private const uint Shinobu143AupPinPinnedAups = 1u << 8;
        private const uint Shinobu143AupPinPinnedMask = 1u << 9;
        // COLD ALLOC: Plane[6] - reused camera frustum for tether upload rejection - owner: TetherManager
        private static readonly Plane[] s_TetherFrustumPlanes = new Plane[6];

        [Header("Tether Rendering")]
        [Tooltip("Required authored material for tether line rendering. Runtime material creation is forbidden.")]
        [SerializeField] private Material tetherRenderMaterial;

        [Tooltip("Optional authored six-index segment mesh for indirect tether rendering. When omitted the system falls back to RenderPrimitives without generating meshes.")]
        [SerializeField] private Mesh indirectTetherSegmentMesh;

        [Tooltip("Fallback tether line tint used by the procedural line-strip renderer.")]
        [SerializeField] private Color tetherRenderColor = new Color(0.22f, 0.92f, 0.96f, 0.92f);

        [Tooltip("Cheap visual overdrive tint blended in as cable tension and stress rise.")]
        [SerializeField] private Color tetherStressColor = new Color(1f, 0.38f, 0.12f, 0.96f);

        [Tooltip("Maps per-segment constraint delta into localized stress glow.")]
        [SerializeField, Range(0.1f, 8f)] private float tetherSegmentStressScale = 2.5f;

        [Tooltip("World-space half-width used by the procedural tube impostor shader.")]
        [SerializeField, Range(0.01f, 0.35f)] private float tetherRenderRadius = 0.045f;

        [Tooltip("Padding applied around per-tether bounds before the procedural draw is submitted.")]
        [SerializeField, Range(0f, 4f)] private float tetherBoundsPadding = 1.2f;

        [Tooltip("Optional explicit camera used for tether rendering. Null renders to all cameras.")]
        [SerializeField] private Camera renderCamera;

        [Tooltip("Maximum tether count allowed to use virtual bend detection and catenary rendering simultaneously.")]
        [SerializeField, Range(1, 8)] private int maxVisualizedTethers = 4;

        [Header("Tether Profiles")]
        [Tooltip("Optional authored tow-cable profile. When omitted the runtime falls back to HeavyTowWinch tuning.")]
        [SerializeField] private TetherProfileSO towCableProfile;

        [Header("Diagnostics")]
#pragma warning disable CS0414
        [SerializeField] private int _debugActiveTetherCount;
        [SerializeField] private float _debugPeakTension;
#pragma warning restore CS0414

        // COLD ALLOC: List<TetherInstance>[64] - active tether registry sized for the 50-cable SHINOBU target without gameplay resize - owner: TetherManager
        private readonly List<TetherInstance> _activeInstances = new List<TetherInstance>(MaxManagedTetherInstances);
        // COLD ALLOC: List<TetherInstance>[64] - prewarmed tether instances reused across attach/release cycles - owner: TetherManager
        private readonly List<TetherInstance> _pooledInstances = new List<TetherInstance>(MaxManagedTetherInstances);
        private MaterialPropertyBlock _renderPropertyBlock;
        private GraphicsBuffer _indirectTetherArgsBuffer;
        private Mesh _indirectArgsMesh;
        private int _indirectArgsSegmentCount = -1;
        private bool _supportsIndirectTetherRenderingCold;
        private IDataVault _dataVault;
        private ICablePhysics132Service _cablePhysics132Service;
        private bool _registeredFixedTick;
        private bool _registeredLateFrameTick;
        private bool _registeredSlowTick;
        private bool _registeredOriginShiftListener;
        private bool _hotSwapRegistered;
        private TetherTelemetryState _telemetryState;
        private JobHandle _shinobu143AupMockHandle;
        private bool _shinobu143AupMockScheduled;
        private IDataVault _shinobu143AupMockPinVault;
        private uint _shinobu143AupMockPinMask;
        private long _shinobu143AupMockScheduleTicks;
        private float _shinobu143LastMockElapsedUs;
        private JobHandle _shinobu132CableMockHandle;
        private bool _shinobu132CableMockScheduled;
        private ICablePhysics132Service _shinobu132CableMockLeaseService;
        private IDataVault _shinobu132CableMockLeaseVault;
        private long _shinobu132CableMockScheduleTicks;
        private float _shinobu132LastMockElapsedUs;
        private bool _shinobu132CableBootstrapRequested;
        private JobHandle _shinobu328TensionMockHandle;
        private bool _shinobu328TensionMockScheduled;
        private IDataVault _shinobu328TensionMockLeaseVault;
        private long _shinobu328TensionMockScheduleTicks;
        private float _shinobu328LastMockElapsedUs;
        private bool _shinobu328TensionBootstrapRequested;
        private int _shinobu328LastActiveTetherCount;
        private int _shinobu328LastNodesPerTether;
        private uint _shinobu328LastFrameIndex;
        private float _fixedStepClockSeconds;
        private int _fixedFrameIndex;
        private bool _telemetryDumped;
        private HectonQualityTier _cachedQualityTier = HectonQualityTier.Unknown;
        private float _cachedQualityWeight01 = 1f;
        private HectonMapMagicVegetationBridge _cachedVegetationBridge;
        private HectonFluidEngine _cachedFluidEngine;
        private HectonVoxelEngine _cachedVoxelEngineRuntime;
        private IVoxelSonarSdfReadModel _cachedVoxelSdfReadModel;
        private IWeatherService _cachedWeatherService;
        private Camera _cachedPlayerCamera;
        private HectonPlayerMovement _cachedPlayerMovement;
        private Shinobu143AupBufferViews _shinobu143AupViews;

        internal uint CurrentFixedFrameIndex => unchecked((uint)math.max(0, _fixedFrameIndex));
        internal HectonQualityTier CachedQualityTier => _cachedQualityTier;
        internal float CachedQualityWeight01 => _cachedQualityWeight01;
        internal IDataVault CachedDataVault => _dataVault;
        internal HectonMapMagicVegetationBridge CachedVegetationBridge => _cachedVegetationBridge;
        internal HectonFluidEngine CachedFluidEngine => _cachedFluidEngine;
        internal HectonVoxelEngine CachedVoxelEngineRuntime => _cachedVoxelEngineRuntime;
        internal IVoxelSonarSdfReadModel CachedVoxelSdfReadModel => _cachedVoxelSdfReadModel;
        internal IWeatherService CachedWeatherService => _cachedWeatherService;

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Explicit, Size = 64)]
        private struct TetherManagerTelemetryEntry
        {
            [System.Runtime.InteropServices.FieldOffset(0)]
            public uint FrameIndex;
            [System.Runtime.InteropServices.FieldOffset(4)]
            public int ActiveTethers;
            [System.Runtime.InteropServices.FieldOffset(8)]
            public float PeakTension;
            [System.Runtime.InteropServices.FieldOffset(12)]
            public uint Flags;
            [System.Runtime.InteropServices.FieldOffset(16)]
            private ulong _pad0;
            [System.Runtime.InteropServices.FieldOffset(24)]
            private ulong _pad1;
            [System.Runtime.InteropServices.FieldOffset(32)]
            private ulong _pad2;
            [System.Runtime.InteropServices.FieldOffset(40)]
            private ulong _pad3;
            [System.Runtime.InteropServices.FieldOffset(48)]
            private ulong _pad4;
            [System.Runtime.InteropServices.FieldOffset(56)]
            private ulong _pad5;
        }

        private void Awake()
        {
            if (!VerletCableLayout.Validate())
            {
                Hecton8.Core.H8Debug.LogError("[TETHER] Verlet DTO layout validation failed. Tether manager disabled.", this);
                enabled = false;
                return;
            }

            TetherSignals.EnsureInitialized();

            if (renderCamera == null)
            {
                Camera childCamera = Hecton8.Core.ComponentReferenceUtility.ResolveOwnedComponent<Camera>(transform);
                if (childCamera != null)
                    renderCamera = childCamera;
            }

            _renderPropertyBlock ??= new MaterialPropertyBlock(); // COLD ALLOC: MaterialPropertyBlock[1] - procedural tether render binding payload - owner: TetherManager
            EnsurePresentationResourcesCold();
            RefreshColdDependencyCache();
            PrewarmTetherPool(InitialPooledTetherInstances);
            _telemetryState.Ensure(TetherBlackBoxCapacity);
            EnsureShinobu132CableBootstrap();
            EnsureShinobu143AupBootstrap();
            EnsureShinobu328TensionBootstrap();
        }

        private void OnEnable()
        {
            EnsurePresentationResourcesCold();
            RefreshColdDependencyCache();
            TryRegisterHotSwapListener();
            TryRegisterSlowTickable();
            TryRegisterFixedTickable();
            TryRegisterLateFrameTickable();

            if (!_registeredOriginShiftListener)
            {
                HectonFloatingOrigin.RegisterListener(this);
                _registeredOriginShiftListener = HectonFloatingOrigin.IsListenerRegistered(this);
            }
        }

        private void OnDisable()
        {
            CompleteShinobu328TensionMockForTeardown();
            CompleteShinobu143AupMockForTeardown();
            CompleteShinobu132CableMockForTeardown();
            TryUnregisterFixedTickable();
            TryUnregisterLateFrameTickable();
            TryUnregisterSlowTickable();
            TryUnregisterHotSwapListener();

            if (_registeredOriginShiftListener)
            {
                HectonFloatingOrigin.UnregisterListener(this);
                _registeredOriginShiftListener = false;
            }

            for (int i = _activeInstances.Count - 1; i >= 0; i--)
                DetachTether(_activeInstances[i], false, true);
        }

        private void TryRegisterFixedTickable()
        {
            if (_registeredFixedTick || !Application.isPlaying)
                return;

            if (GlobalRegistry.Dispatcher == null)
                return;

            _registeredFixedTick = GlobalRegistry.TryRegisterFixedTickable(this, PriorityLayer.Environment);
        }

        private void TryUnregisterFixedTickable()
        {
            if (!_registeredFixedTick)
                return;

            GlobalRegistry.UnregisterFixedTickable(this, PriorityLayer.Environment);
            _registeredFixedTick = false;
        }

        private void TryRegisterLateFrameTickable()
        {
            if (_registeredLateFrameTick || !Application.isPlaying)
                return;

            if (GlobalRegistry.Dispatcher == null)
                return;

            _registeredLateFrameTick = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Player);
        }

        private void TryUnregisterLateFrameTickable()
        {
            if (!_registeredLateFrameTick)
                return;

            GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Player);
            _registeredLateFrameTick = false;
        }

        private void TryRegisterSlowTickable()
        {
            if (_registeredSlowTick || !Application.isPlaying)
                return;

            if (GlobalRegistry.Dispatcher == null)
                return;

            _registeredSlowTick = GlobalRegistry.TryRegisterSlowTickable(this, PriorityLayer.Environment);
        }

        private void TryUnregisterSlowTickable()
        {
            if (!_registeredSlowTick)
                return;

            GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Environment);
            _registeredSlowTick = false;
        }

        private void OnDestroy()
        {
            CompleteShinobu328TensionMockForTeardown();
            CompleteShinobu143AupMockForTeardown();
            CompleteShinobu132CableMockForTeardown();
            TryUnregisterLateFrameTickable();
            TryUnregisterFixedTickable();
            TryUnregisterSlowTickable();
            TryUnregisterHotSwapListener();

            if (_registeredOriginShiftListener)
            {
                HectonFloatingOrigin.UnregisterListener(this);
                _registeredOriginShiftListener = false;
            }

            for (int i = 0; i < _pooledInstances.Count; i++)
            {
                if (_pooledInstances[i] != null)
                    _pooledInstances[i].DisposeRuntimeResources();
            }

            for (int i = 0; i < _activeInstances.Count; i++)
            {
                if (_activeInstances[i] != null)
                    _activeInstances[i].DisposeRuntimeResources();
            }

            ReleaseIndirectTetherRenderResources();

            _telemetryState.Dispose();
            _shinobu143AupViews.Clear();
            _dataVault = null;
            _cachedVegetationBridge = null;
            _cachedFluidEngine = null;
            _cachedVoxelEngineRuntime = null;
            _cachedVoxelSdfReadModel = null;
            _cachedWeatherService = null;
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (!isActiveAndEnabled)
                return;

            switch (serviceSlot)
            {
                case GlobalRegistryServiceSlot.Dispatcher:
                    TryUnregisterFixedTickable();
                    TryUnregisterLateFrameTickable();
                    TryUnregisterSlowTickable();
                    if (currentService == null)
                        return;

                    TryRegisterSlowTickable();
                    TryRegisterFixedTickable();
                    TryRegisterLateFrameTickable();
                    break;
                case GlobalRegistryServiceSlot.DataVault:
                    CompleteShinobu328TensionMockForTeardown();
                    CompleteShinobu143AupMockForTeardown();
                    CompleteShinobu132CableMockForTeardown();
                    IDataVault currentVault = currentService as IDataVault;
                    RebindTetherInstancesForDataVault(currentVault);
                    _dataVault = currentVault;
                    _shinobu143AupViews.Clear();
                    _shinobu132CableBootstrapRequested = false;
                    _shinobu328TensionBootstrapRequested = false;
                    EnsureShinobu132CableBootstrap();
                    EnsureShinobu143AupBootstrap();
                    EnsureShinobu328TensionBootstrap();
                    break;
                case GlobalRegistryServiceSlot.CablePhysics132Runtime:
                    CompleteShinobu132CableMockForTeardown();
                    _cablePhysics132Service = currentService as ICablePhysics132Service;
                    _shinobu132CableBootstrapRequested = false;
                    EnsureShinobu132CableBootstrap();
                    break;
                case GlobalRegistryServiceSlot.MapMagicVegetationRuntime:
                    _cachedVegetationBridge = currentService as HectonMapMagicVegetationBridge;
                    WorldRuntimeReferenceUtility.TryResolveHectonMapMagicVegetationBridge(ref _cachedVegetationBridge);
                    break;
                case GlobalRegistryServiceSlot.FluidRuntime:
                    _cachedFluidEngine = currentService as HectonFluidEngine;
                    break;
                case GlobalRegistryServiceSlot.VoxelEngineRuntime:
                    _cachedVoxelEngineRuntime = currentService as HectonVoxelEngine;
                    _cachedVoxelSdfReadModel = _cachedVoxelEngineRuntime as IVoxelSonarSdfReadModel;
                    break;
                case GlobalRegistryServiceSlot.Weather:
                    _cachedWeatherService = currentService as IWeatherService;
                    break;
                case GlobalRegistryServiceSlot.Player:
                    CachePlayerContext(currentService as IPlayerRuntimeContext);
                    break;
            }
        }

        private void RebindTetherInstancesForDataVault(IDataVault currentVault)
        {
            for (int i = 0; i < _activeInstances.Count; i++)
                _activeInstances[i]?.RebindDataVault(currentVault);

            for (int i = 0; i < _pooledInstances.Count; i++)
                _pooledInstances[i]?.RebindDataVault(currentVault);
        }

        private void TryRegisterHotSwapListener()
        {
            if (_hotSwapRegistered || !Application.isPlaying)
                return;

            _hotSwapRegistered = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterHotSwapListener()
        {
            if (!_hotSwapRegistered)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _hotSwapRegistered = false;
        }

        /// <summary>
        /// Creates or reuses a tow-cable runtime instance.
        /// </summary>
        public TetherInstance AttachTowCable(
            HeavyTowWinch owner,
            HectonPlayerMotor playerMotor,
            Rigidbody legacyAnchorBody,
            Rigidbody payloadBody,
            Collider payloadCollider,
            float initialDistance)
        {
            _ = legacyAnchorBody;
            return AttachTowCable(owner, playerMotor, payloadBody, payloadCollider, initialDistance);
        }

        /// <summary>
        /// Creates or reuses a tow-cable runtime instance anchored to the player motor route.
        /// </summary>
        public TetherInstance AttachTowCable(
            HeavyTowWinch owner,
            HectonPlayerMotor playerMotor,
            Rigidbody payloadBody,
            Collider payloadCollider,
            float initialDistance)
        {
            if (owner == null || playerMotor == null || payloadBody == null || payloadCollider == null)
                return null;

            TetherInstance instance = RentInstance();
            if (instance == null)
                return null;

            RefreshColdDependencyCache();
            instance.Configure(owner, playerMotor, payloadBody, payloadCollider, initialDistance, _cachedQualityWeight01);
            if (!_activeInstances.Contains(instance))
            {
                if (_activeInstances.Count >= MaxManagedTetherInstances)
                {
                    ReturnInstanceToPool(instance);
                    return null;
                }

                _activeInstances.Add(instance);
            }

            _debugActiveTetherCount = _activeInstances.Count;
            return instance;
        }

        internal bool ExecuteFireRequest(
            HeavyTowWinch owner,
            HectonPlayerMotor playerMotor,
            Rigidbody payloadBody,
            Collider payloadCollider,
            float initialDistance)
        {
            if (owner == null ||
                playerMotor == null ||
                payloadBody == null ||
                payloadCollider == null)
            {
                return false;
            }

            if (owner.HasActiveTow)
                owner.ReleaseTow(false);

            TetherInstance instance = AttachTowCable(
                owner,
                playerMotor,
                payloadBody,
                payloadCollider,
                initialDistance);
            if (instance == null)
                return false;

            return owner.CompleteSignalAttach(instance, payloadBody);
        }

        /// <summary>
        /// Releases an active tether and returns it to the local pool.
        /// </summary>
        public void DetachTether(TetherInstance instance, bool snapped, bool notifyOwner)
        {
            if (instance == null)
                return;

            int index = _activeInstances.IndexOf(instance);
            if (index >= 0)
            {
                int lastIndex = _activeInstances.Count - 1;
                _activeInstances[index] = _activeInstances[lastIndex];
                _activeInstances.RemoveAt(lastIndex);
            }

            HeavyTowWinch owner = notifyOwner ? instance.Owner : null;
            ReturnInstanceToPool(instance);

            if (notifyOwner && owner != null)
                owner.OnTetherDetached(instance, snapped);

            _debugActiveTetherCount = _activeInstances.Count;
        }

        /// <inheritdoc />
        public void OnOriginShift(in OriginShiftEventData shiftData)
        {
            Vector3 shiftOffset = shiftData.ShiftOffset;
            float shiftSqrMagnitude = shiftOffset.sqrMagnitude;
            if (!math.isfinite(shiftSqrMagnitude) || shiftSqrMagnitude <= 0.000001f)
                return;

            float3 shiftOffsetF3 = new float3(shiftOffset.x, shiftOffset.y, shiftOffset.z);
            for (int i = 0; i < _activeInstances.Count; i++)
            {
                TetherInstance instance = _activeInstances[i];
                if (instance == null || !instance.IsActive)
                    continue;

                instance.RebaseManagedRuntimeState(shiftOffset);
                if (instance.RebaseVerletRuntime(shiftOffsetF3))
                {
                    instance.CommitVisualRebaseUpload();
                    continue;
                }

                if (instance.RebaseVisualStagingRuntime(shiftOffsetF3))
                    instance.CommitVisualRebaseUpload();
            }
        }

        /// <inheritdoc />
        public void FixedTick(float fixedDeltaTime)
        {
            _fixedStepClockSeconds = AdvanceFixedStepClock(_fixedStepClockSeconds, fixedDeltaTime);
            int fixedFrameIndex = AdvanceFixedFrameIndex();
            ScheduleShinobu132CableMock(fixedDeltaTime, unchecked((uint)fixedFrameIndex));
            ScheduleShinobu143AupMock(fixedDeltaTime, unchecked((uint)fixedFrameIndex));
            ScheduleShinobu328TensionMock(fixedDeltaTime, unchecked((uint)fixedFrameIndex));
            int activeCount = _activeInstances.Count;
            float qualityWeight01 = _cachedQualityWeight01;
            for (int i = activeCount - 1; i >= 0; i--)
            {
                TetherInstance instance = _activeInstances[i];
                if (instance == null)
                {
                    _activeInstances.RemoveAt(i);
                    continue;
                }

                TetherLifecycleState state = instance.Simulate(fixedDeltaTime, _fixedStepClockSeconds, fixedFrameIndex, activeCount, maxVisualizedTethers, qualityWeight01);
                if (state == TetherLifecycleState.Alive)
                    continue;

                bool snapped = state == TetherLifecycleState.Snapped;
                DetachTether(instance, snapped, true);
                activeCount = _activeInstances.Count;
            }

            _debugActiveTetherCount = _activeInstances.Count;
            float peakTension = ResolvePeakTension();
            _debugPeakTension = peakTension;
            WriteBlackBoxSample(_debugActiveTetherCount, peakTension, 0u);
        }

        private static float AdvanceFixedStepClock(float currentSeconds, float fixedDeltaTime)
        {
            float safeCurrent = math.isfinite(currentSeconds) ? currentSeconds : 0f;
            if (fixedDeltaTime <= 0f || !math.isfinite(fixedDeltaTime))
                return safeCurrent;

            float next = safeCurrent + math.min(fixedDeltaTime, 0.05f);
            if (!math.isfinite(next))
                return 0f;

            return next >= TetherFixedClockWrapSeconds
                ? next - TetherFixedClockWrapSeconds
                : next;
        }

        private int AdvanceFixedFrameIndex()
        {
            if (_fixedFrameIndex == int.MaxValue)
            {
                _fixedFrameIndex = 0;
                return _fixedFrameIndex;
            }

            _fixedFrameIndex++;
            return _fixedFrameIndex;
        }

        /// <inheritdoc />
        public void LateFrameTick()
        {
            // L19 hop2 LIVE: tether line rendering is presentation-only for hop probes.
            if (Application.isBatchMode)
                return;

            Material renderMaterial = ResolveRenderMaterial();
            if (renderMaterial == null || _activeInstances.Count == 0)
                return;

            _renderPropertyBlock.Clear();
            RenderParams renderParams = new RenderParams(renderMaterial)
            {
                matProps = _renderPropertyBlock,
                camera = renderCamera,
                layer = gameObject.layer,
                receiveShadows = false,
                shadowCastingMode = ShadowCastingMode.Off,
                motionVectorMode = MotionVectorGenerationMode.Camera,
                renderingLayerMask = 1u
            };

            float deltaTime = SystemDispatcher.CurrentFrameDeltaTime;
            float qualityWeight01 = _cachedQualityWeight01;
            int visualTier = ResolveTetherVisualTier(qualityWeight01);
            float visualOverkill01 = Smooth01(math.saturate((qualityWeight01 - 0.55f) * 2.2222223f));
            float crystalDensity = math.lerp(0f, 1f, visualOverkill01);
            float siltIntensity = math.lerp(0f, 0.55f, visualOverkill01);
            float visualClock = math.isfinite(_fixedStepClockSeconds) ? _fixedStepClockSeconds : 0f;
            bool hasFrustum = renderCamera != null;
            if (hasFrustum)
                GeometryUtility.CalculateFrustumPlanes(renderCamera, s_TetherFrustumPlanes);
            for (int i = 0; i < _activeInstances.Count; i++)
            {
                TetherInstance instance = _activeInstances[i];
                if (instance == null || !instance.IsActive)
                    continue;

                instance.UpdateVisuals(deltaTime, qualityWeight01, hasFrustum ? s_TetherFrustumPlanes : null);
                if (!instance.IsVisualReady || instance.IsVisualCulled)
                    continue;

                int segmentCount = math.max(0, instance.VisualPointCount - 1);
                if (segmentCount <= 0)
                    continue;

                bool useIndirect = HasIndirectTetherRenderResources();
                if (!instance.UploadVisualDrawParams(
                        tetherRenderColor,
                        tetherStressColor,
                        tetherSegmentStressScale,
                        tetherRenderRadius,
                        instance.VisualPointCount,
                        useIndirect,
                        visualTier,
                        crystalDensity,
                        siltIntensity,
                        visualClock))
                    continue;

                _renderPropertyBlock.Clear();
                _renderPropertyBlock.SetBuffer(_TetherPositionsId, instance.VisualSegmentBuffer);
                _renderPropertyBlock.SetBuffer(_TetherSegmentTensionsId, instance.VisualSegmentTensionBuffer);
                _renderPropertyBlock.SetBuffer(_TetherDrawParamsId, instance.VisualDrawParamsBuffer);
                renderParams.worldBounds = instance.GetVisualBounds(tetherBoundsPadding);
                if (useIndirect && TryRenderIndirectTether(renderParams, segmentCount))
                    continue;

                if (useIndirect)
                {
                    if (!instance.UploadVisualDrawParams(
                            tetherRenderColor,
                            tetherStressColor,
                            tetherSegmentStressScale,
                            tetherRenderRadius,
                            instance.VisualPointCount,
                            false,
                            visualTier,
                            crystalDensity,
                            siltIntensity,
                            visualClock))
                        continue;

                    _renderPropertyBlock.SetBuffer(_TetherDrawParamsId, instance.VisualDrawParamsBuffer);
                }

                UnityEngine.Graphics.RenderPrimitives(renderParams, MeshTopology.Triangles, segmentCount * 6, 1);
            }
        }

        private bool HasIndirectTetherRenderResources()
        {
            return _supportsIndirectTetherRenderingCold &&
                   indirectTetherSegmentMesh != null &&
                   _indirectTetherArgsBuffer != null;
        }

        private static int ResolveTetherVisualTier(float qualityWeight01)
        {
            float quality = Smooth01(qualityWeight01);
            return math.clamp((int)math.round(quality * 3f), 0, 3);
        }

        private bool TryRenderIndirectTether(RenderParams renderParams, int segmentCount)
        {
            if (segmentCount <= 0)
                return false;

            Mesh mesh = indirectTetherSegmentMesh;
            if (mesh == null || _indirectTetherArgsBuffer == null)
                return false;

            UploadIndirectTetherArgs(mesh, segmentCount);
            UnityEngine.Graphics.RenderMeshIndirect(renderParams, mesh, _indirectTetherArgsBuffer, 1, 0);
            return true;
        }

        private void EnsurePresentationResourcesCold()
        {
            _supportsIndirectTetherRenderingCold = SystemInfo.supportsInstancing && SystemInfo.supportsComputeShaders;
            if (!_supportsIndirectTetherRenderingCold)
            {
                ReleaseIndirectTetherRenderResources();
                return;
            }

            if (indirectTetherSegmentMesh == null)
            {
                ReleaseIndirectTetherRenderResources();
                return;
            }

            EnsureIndirectTetherArgsBufferCold();
        }

        private void EnsureIndirectTetherArgsBufferCold()
        {
            if (_indirectTetherArgsBuffer != null)
                return;

            _indirectTetherArgsBuffer = new GraphicsBuffer(
                GraphicsBuffer.Target.IndirectArguments,
                GraphicsBuffer.UsageFlags.LockBufferForWrite,
                1,
                GraphicsBuffer.IndirectDrawIndexedArgs.size); // COLD ALLOC: GraphicsBuffer[1] - tether RenderMeshIndirect draw args - owner: TetherManager
            _indirectArgsMesh = null;
            _indirectArgsSegmentCount = -1;
        }

        private void UploadIndirectTetherArgs(Mesh mesh, int segmentCount)
        {
            if (_indirectTetherArgsBuffer == null || mesh == null || segmentCount <= 0)
                return;

            if (_indirectArgsMesh == mesh && _indirectArgsSegmentCount == segmentCount)
                return;

            NativeArray<GraphicsBuffer.IndirectDrawIndexedArgs> argsWrite =
                _indirectTetherArgsBuffer.LockBufferForWrite<GraphicsBuffer.IndirectDrawIndexedArgs>(0, 1);
            try
            {
                argsWrite[0] = new GraphicsBuffer.IndirectDrawIndexedArgs
                {
                    indexCountPerInstance = mesh.GetIndexCount(0),
                    instanceCount = (uint)segmentCount,
                    startIndex = mesh.GetIndexStart(0),
                    baseVertexIndex = (uint)Mathf.Max(0, mesh.GetBaseVertex(0)),
                    startInstance = 0u
                };
            }
            finally
            {
                _indirectTetherArgsBuffer.UnlockBufferAfterWrite<GraphicsBuffer.IndirectDrawIndexedArgs>(1);
            }
            _indirectArgsMesh = mesh;
            _indirectArgsSegmentCount = segmentCount;
        }

        private void ReleaseIndirectTetherRenderResources()
        {
            if (_indirectTetherArgsBuffer != null)
            {
                _indirectTetherArgsBuffer.Release();
                _indirectTetherArgsBuffer = null;
            }

            _indirectArgsMesh = null;
            _indirectArgsSegmentCount = -1;
        }

        private TetherInstance RentInstance()
        {
            int pooledCount = _pooledInstances.Count;
            if (pooledCount > 0)
            {
                int lastIndex = pooledCount - 1;
                TetherInstance pooled = _pooledInstances[lastIndex];
                _pooledInstances.RemoveAt(lastIndex);
                if (pooled != null)
                {
                    pooled.InitializeManager(this);
                    pooled.gameObject.SetActive(true);
                    return pooled;
                }
            }

            return null;
        }

        private void PrewarmTetherPool(int requestedCount)
        {
            int targetCount = math.clamp(requestedCount, 0, MaxManagedTetherInstances);
            while (_pooledInstances.Count < targetCount)
            {
                TetherInstance instance = CreateColdPooledInstance();
                if (instance == null)
                    return;

                _pooledInstances.Add(instance);
            }
        }

        private TetherInstance CreateColdPooledInstance()
        {
            if (_activeInstances.Count + _pooledInstances.Count >= MaxManagedTetherInstances)
                return null;

            GameObject tetherObject = new GameObject("TetherInstance");
            tetherObject.transform.SetParent(transform, false);
            tetherObject.transform.localPosition = Vector3.zero;
            tetherObject.transform.localRotation = Quaternion.identity;
            tetherObject.transform.localScale = Vector3.one;
            // COLD ALLOC: TetherInstance[1] - pooled tether runtime child created only during manager prewarm - owner: TetherManager
            TetherInstance instance = tetherObject.AddComponent<TetherInstance>();
            instance.InitializeManager(this);
            tetherObject.SetActive(false);
            return instance;
        }

        private void ReturnInstanceToPool(TetherInstance instance)
        {
            if (instance == null)
                return;

            instance.Deactivate();
            if (_pooledInstances.Count >= MaxManagedTetherInstances || _pooledInstances.Contains(instance))
                return;

            _pooledInstances.Add(instance);
        }

        /// <inheritdoc />
        public void SlowTick()
        {
            RefreshQualityCache();
        }

        private void RefreshColdDependencyCache()
        {
            RefreshQualityCache();
            WorldRuntimeReferenceUtility.TryResolveHectonMapMagicVegetationBridge(ref _cachedVegetationBridge);
            _cachedFluidEngine = GlobalRegistry.Fluid;
            _cachedVoxelEngineRuntime = GlobalRegistry.VoxelEngine;
            _cachedVoxelSdfReadModel = GlobalRegistry.VoxelSonarSdf;
            _cachedWeatherService = GlobalRegistry.Weather;
            if (_dataVault == null)
                _dataVault = GlobalRegistry.DataVault;
            if (_cablePhysics132Service == null)
                _cablePhysics132Service = GlobalRegistry.CablePhysics132;

            CachePlayerContext(GlobalRegistry.Player);
        }

        private void RefreshQualityCache()
        {
            _cachedQualityWeight01 = ResolveGlobalQualityWeight01();
            _cachedQualityTier = ResolveQualityTierFromGlobalWeight(_cachedQualityWeight01);
        }

        private void CachePlayerContext(IPlayerRuntimeContext playerContext)
        {
            if (playerContext == null)
            {
                _cachedPlayerCamera = null;
                _cachedPlayerMovement = null;
                return;
            }

            if (playerContext.PlayerCamera != null)
                _cachedPlayerCamera = playerContext.PlayerCamera;
            if (playerContext.PlayerMovement != null)
                _cachedPlayerMovement = playerContext.PlayerMovement;
        }

        private void EnsureShinobu143AupBootstrap()
        {
            if (_dataVault == null)
                return;

            float qualityWeight = _cachedQualityWeight01;

            TetherAupVaultBootstrap.EnsureMockBuffers(
                _dataVault,
                qualityWeight,
                CurrentFixedFrameIndex);
            RefreshShinobu143AupViewsCold();
        }

        private void EnsureShinobu132CableBootstrap()
        {
            ICablePhysics132Service cableService = _cablePhysics132Service;
            if (_dataVault == null || cableService == null)
                return;
            if (_shinobu132CableBootstrapRequested &&
                cableService.TryHasMockBuffers(_dataVault))
            {
                return;
            }

            float qualityWeight = _cachedQualityWeight01;

            cableService.EnsureMockBuffers(
                _dataVault,
                qualityWeight,
                CurrentFixedFrameIndex);
            _shinobu132CableBootstrapRequested = true;
        }

        private void ScheduleShinobu132CableMock(float fixedDeltaTime, uint frameIndex)
        {
            // L19 hop2 LIVE: cable mock jobs not required for hop input validation under batchmode.
            if (Application.isBatchMode)
                return;

            ICablePhysics132Service cableService = _cablePhysics132Service;
            if (_dataVault == null || cableService == null)
                return;

            TryFinalizeShinobu132CableMock();
            if (_shinobu132CableMockScheduled)
                return;

            if (!ResolveShinobu132CameraContext(out Vector3 cameraPosition, out double3 cameraAup))
                return;

            float qualityWeight = _cachedQualityWeight01;
            float safeDelta = math.isfinite(fixedDeltaTime) && fixedDeltaTime > 0f
                ? math.min(fixedDeltaTime, 0.05f)
                : 0.02f;
            float3 gravity = new float3(0f, -HectonPhysicsContract.GravityMetersPerSecondSquaredConst * 0.16f, 0f);
            float3 abyssalFlow = float3.zero;
            if (_cachedFluidEngine != null &&
                _cachedFluidEngine.TrySampleModAbyssalFlow(cameraPosition, out float3 sampledAbyssalFlow) &&
                math.all(math.isfinite(sampledAbyssalFlow)))
            {
                abyssalFlow = sampledAbyssalFlow;
            }

            _shinobu132CableMockScheduleTicks = System.Diagnostics.Stopwatch.GetTimestamp();
            if (!cableService.TryScheduleMockFromVault(
                    _dataVault,
                    frameIndex,
                    safeDelta,
                    gravity,
                    abyssalFlow,
                    cameraAup,
                    qualityWeight,
                    _shinobu132LastMockElapsedUs,
                    default,
                    out _shinobu132CableMockHandle))
            {
                return;
            }

            _shinobu132CableMockScheduled = true;
            _shinobu132CableMockLeaseService = cableService;
            _shinobu132CableMockLeaseVault = _dataVault;
            H8Memory.RegisterActiveJob(SystemID.Physics, _shinobu132CableMockHandle);
        }

        private void TryFinalizeShinobu132CableMock()
        {
            if (!_shinobu132CableMockScheduled)
                return;

            if (!DispatcherJobFence.TryFinalizeCompleted(ref _shinobu132CableMockHandle))
                return;

            FinishShinobu132CableMockCompletion();
        }

        private void CompleteShinobu132CableMockForTeardown()
        {
            if (!_shinobu132CableMockScheduled)
                return;

            if (!DispatcherJobFence.TryComplete(ref _shinobu132CableMockHandle, forceComplete: true))
                return;

            FinishShinobu132CableMockCompletion();
        }

        private void FinishShinobu132CableMockCompletion()
        {
            ICablePhysics132Service cableService = _shinobu132CableMockLeaseService;
            IDataVault cableVault = _shinobu132CableMockLeaseVault;
            _shinobu132CableMockScheduled = false;
            _shinobu132CableMockLeaseService = null;
            _shinobu132CableMockLeaseVault = null;
            try
            {
                long elapsedTicks = System.Diagnostics.Stopwatch.GetTimestamp() - _shinobu132CableMockScheduleTicks;
                _shinobu132LastMockElapsedUs = (float)math.max(
                    0.0d,
                    elapsedTicks * 1000000.0d / System.Diagnostics.Stopwatch.Frequency);
            }
            finally
            {
                cableService?.ReleaseMockScheduleBufferPins(cableVault);
            }

            cableService?.TryDumpLatestFault(cableVault);
        }

        private void EnsureShinobu328TensionBootstrap()
        {
            if (_dataVault == null)
                return;
            if (_shinobu328TensionBootstrapRequested &&
                HarpoonTensionSolver328.TryHasMockBuffers(_dataVault))
            {
                return;
            }

            float qualityWeight = _cachedQualityWeight01;

            HarpoonTensionSolver328.EnsureSignalLanes();
            HarpoonTensionSolver328.EnsureMockBuffers(
                _dataVault,
                qualityWeight,
                CurrentFixedFrameIndex);
            _shinobu328TensionBootstrapRequested = HarpoonTensionSolver328.TryHasMockBuffers(_dataVault);
        }

        private void ScheduleShinobu328TensionMock(float fixedDeltaTime, uint frameIndex)
        {
            // L19 hop2 LIVE: HarpoonTensionForceJob writes StressStates/PhysicsEvents with
            // NativeDisableContainerSafetyRestriction aliasing, then PhysX UpdateMassDistribution
            // ACCESS_VIOLATION on the main thread under -batchmode. Tension mock is not required
            // for hop input validation.
            if (Application.isBatchMode)
                return;

            if (_dataVault == null)
                return;

            TryFinalizeShinobu328TensionMock();
            if (_shinobu328TensionMockScheduled)
                return;

            EnsureShinobu328TensionBootstrap();
            Vector3 cameraPosition;
            if (!ResolveShinobu132CameraContext(out cameraPosition, out double3 cameraAup))
                return;

            float qualityWeight = _cachedQualityWeight01;
            float safeDelta = math.isfinite(fixedDeltaTime) && fixedDeltaTime > 0f
                ? math.min(fixedDeltaTime, 0.05f)
                : 0.02f;

            _shinobu328TensionMockScheduleTicks = System.Diagnostics.Stopwatch.GetTimestamp();
            if (!HarpoonTensionSolver328.TryScheduleMockFromVault(
                    _dataVault,
                    frameIndex,
                    safeDelta,
                    cameraAup,
                    qualityWeight,
                    _shinobu328LastMockElapsedUs,
                    default,
                    out HarpoonTensionSchedule328 schedule))
            {
                return;
            }

            _shinobu328TensionMockHandle = schedule.Handle;
            _shinobu328TensionMockLeaseVault = _dataVault;
            _shinobu328LastActiveTetherCount = schedule.ActiveTetherCount;
            _shinobu328LastNodesPerTether = HarpoonTensionSolver328Constants.MockNodesPerTether;
            _shinobu328LastFrameIndex = frameIndex;
            _shinobu328TensionMockScheduled = true;
            H8Memory.RegisterActiveJob(SystemID.Physics, _shinobu328TensionMockHandle);
        }

        private void TryFinalizeShinobu328TensionMock()
        {
            if (!_shinobu328TensionMockScheduled)
                return;

            if (!DispatcherJobFence.TryFinalizeCompleted(ref _shinobu328TensionMockHandle))
                return;

            FinishShinobu328TensionMockCompletion();
        }

        private void CompleteShinobu328TensionMockForTeardown()
        {
            if (!_shinobu328TensionMockScheduled)
                return;

            if (!DispatcherJobFence.TryComplete(ref _shinobu328TensionMockHandle, forceComplete: true))
                return;

            FinishShinobu328TensionMockCompletion();
        }

        private void FinishShinobu328TensionMockCompletion()
        {
            IDataVault tensionVault = _shinobu328TensionMockLeaseVault;
            _shinobu328TensionMockScheduled = false;
            _shinobu328TensionMockLeaseVault = null;
            try
            {
                long elapsedTicks = System.Diagnostics.Stopwatch.GetTimestamp() - _shinobu328TensionMockScheduleTicks;
                _shinobu328LastMockElapsedUs = (float)math.max(
                    0.0d,
                    elapsedTicks * 1000000.0d / System.Diagnostics.Stopwatch.Frequency);
            }
            finally
            {
                HarpoonTensionSolver328.ReleaseMockScheduleBufferPins(tensionVault);
            }

            float qualityWeight = _cachedQualityWeight01;

            HarpoonTensionSolver328.TryPublishCompletedSignalsFromVault(
                tensionVault,
                _shinobu328LastActiveTetherCount,
                _shinobu328LastNodesPerTether,
                _shinobu328LastFrameIndex,
                qualityWeight,
                1);
            HarpoonTensionSolver328.TryDumpTelemetryIfFault(tensionVault, string.Empty, 1);
        }

        private bool ResolveShinobu132CameraContext(out Vector3 cameraPosition, out double3 cameraAup)
        {
            Camera sourceCamera = renderCamera != null ? renderCamera : _cachedPlayerCamera;
            Transform sourceTransform = sourceCamera != null ? sourceCamera.transform : transform;
            cameraPosition = sourceTransform != null ? sourceTransform.position : Vector3.zero;
            if (!math.all(math.isfinite(new float3(cameraPosition.x, cameraPosition.y, cameraPosition.z))))
            {
                cameraAup = RuntimeOriginRoute.CurrentRuntimeOriginAup().ToAbsoluteDouble3();
                cameraPosition = Vector3.zero;
                return math.all(math.isfinite(cameraAup));
            }

            HectonPlayerMovement movement = _cachedPlayerMovement;
            if (movement != null)
            {
                AbsoluteUniversePosition bodyAup = movement.CurrentAup;
                float3 bodyRuntime = bodyAup.ToRuntimeFloat3();
                float3 cameraRuntime = new float3(cameraPosition.x, cameraPosition.y, cameraPosition.z);
                float3 cameraOffset = cameraRuntime - bodyRuntime;
                cameraAup = bodyAup.ToAbsoluteDouble3() + new double3(cameraOffset.x, cameraOffset.y, cameraOffset.z);
                return math.all(math.isfinite(cameraAup));
            }

            return TryResolveRuntimeAup(cameraPosition, out cameraAup);
        }

        private static bool TryResolveRuntimeAup(Vector3 runtimePosition, out double3 absoluteAup)
        {
            absoluteAup = default;
            float3 localRuntime = new float3(runtimePosition.x, runtimePosition.y, runtimePosition.z);
            if (!math.all(math.isfinite(localRuntime)))
                return false;

            AbsoluteUniversePosition originAup = RuntimeOriginRoute.CurrentRuntimeOriginAup();
            if (!originAup.IsFinite())
                return false;

            AbsoluteUniversePosition positionAup = AbsoluteUniversePosition.OffsetMeters(
                in originAup,
                new double3(runtimePosition.x, runtimePosition.y, runtimePosition.z));
            if (!positionAup.IsFinite())
                return false;

            absoluteAup = positionAup.ToAbsoluteDouble3();
            return math.all(math.isfinite(absoluteAup));
        }

        private void ScheduleShinobu143AupMock(float fixedDeltaTime, uint frameIndex)
        {
            // L19 hop2 LIVE: AUP tether mock not required for hop input validation under batchmode.
            if (Application.isBatchMode)
                return;

            if (_dataVault == null)
                return;

            TryFinalizeShinobu143AupMock();
            if (_shinobu143AupMockScheduled)
                return;

            if (!TryLockShinobu143AupMockBuffers(_dataVault, out IDataVault pinVault))
                return;

            bool pinsTransferred = false;
            try
            {
                if (!_shinobu143AupViews.TryRead(
                        pinVault,
                        out NativeArray<TetherNodeDTO> nodes,
                        out NativeArray<TetherConstraintDTO> constraints,
                        out NativeArray<TetherEndpointAupDTO> endpoints,
                        out NativeArray<float> segmentTensions,
                        out NativeArray<float> solverStats,
                        out NativeArray<TetherForcePacketDTO> forcePackets,
                        out NativeArray<TetherAupTelemetryEntry> telemetryRing,
                        out NativeArray<int> telemetryHead,
                        out NativeArray<double3> pinnedAups,
                        out NativeArray<byte> pinnedMask))
                {
                    return;
                }

                float qualityWeight = _cachedQualityWeight01;
                float safeDelta = math.isfinite(fixedDeltaTime) && fixedDeltaTime > 0f
                    ? math.min(fixedDeltaTime, 0.05f)
                    : 0.02f;
                float damping = math.lerp(0.965f, 0.992f, Smooth01(qualityWeight));
                float3 gravity = new float3(0f, -HectonPhysicsContract.GravityMetersPerSecondSquaredConst * 0.18f, 0f);
                float tensionScale = ResolveTowSpringStiffness(null);
                if (!math.isfinite(tensionScale) || tensionScale <= 0f)
                    tensionScale = 24f;
                _shinobu143AupMockScheduleTicks = System.Diagnostics.Stopwatch.GetTimestamp();
                _shinobu143AupMockHandle = TetherAupSolverScheduler.ScheduleMock(
                    nodes,
                    constraints,
                    endpoints,
                    segmentTensions,
                    solverStats,
                    forcePackets,
                    telemetryRing,
                    telemetryHead,
                    pinnedAups,
                    pinnedMask,
                    frameIndex,
                    0x5348494Eu,
                    safeDelta,
                    gravity,
                    float3.zero,
                    damping,
                    math.lerp(0.18f, 1.1f, qualityWeight),
                    math.max(0f, tensionScale),
                    _shinobu143LastMockElapsedUs,
                    qualityWeight,
                    default);
                _shinobu143AupMockScheduled = true;
                pinsTransferred = true;
                H8Memory.RegisterActiveJob(SystemID.Physics, _shinobu143AupMockHandle);
            }
            finally
            {
                if (!pinsTransferred)
                    ReleaseShinobu143AupMockPins();
            }
        }

        private void TryFinalizeShinobu143AupMock()
        {
            if (!_shinobu143AupMockScheduled)
                return;

            if (!DispatcherJobFence.TryFinalizeCompleted(ref _shinobu143AupMockHandle))
                return;

            FinishShinobu143AupMockCompletion();
        }

        private void CompleteShinobu143AupMockForTeardown()
        {
            if (!_shinobu143AupMockScheduled)
                return;

            if (!DispatcherJobFence.TryComplete(ref _shinobu143AupMockHandle, forceComplete: true))
                return;

            FinishShinobu143AupMockCompletion();
        }

        private void FinishShinobu143AupMockCompletion()
        {
            _shinobu143AupMockScheduled = false;
            long elapsedTicks = System.Diagnostics.Stopwatch.GetTimestamp() - _shinobu143AupMockScheduleTicks;
            _shinobu143LastMockElapsedUs = (float)math.max(
                0.0d,
                elapsedTicks * 1000000.0d / System.Diagnostics.Stopwatch.Frequency);

            try
            {
                IDataVault telemetryVault = _shinobu143AupMockPinVault;
                if (TetherAupRuntimeIntrospection.TrySampleLatestTelemetry(telemetryVault, out TetherAupTelemetryEntry telemetry) &&
                    (telemetry.Flags & (TetherNodeRuntimeFlags.NonFiniteRecovered | TetherNodeRuntimeFlags.ConstraintFault)) != 0u)
                {
                    TetherAupRuntimeIntrospection.TryDumpCableSurgeon(telemetryVault, telemetry.Flags);
                }
            }
            finally
            {
                ReleaseShinobu143AupMockPins();
            }
        }

        private bool TryResolveShinobu143AupBuffers(
            IDataVault vault,
            out NativeArray<TetherNodeDTO> nodes,
            out NativeArray<TetherConstraintDTO> constraints,
            out NativeArray<TetherEndpointAupDTO> endpoints,
            out NativeArray<float> segmentTensions,
            out NativeArray<float> solverStats,
            out NativeArray<TetherForcePacketDTO> forcePackets,
            out NativeArray<TetherAupTelemetryEntry> telemetryRing,
            out NativeArray<int> telemetryHead,
            out NativeArray<double3> pinnedAups,
            out NativeArray<byte> pinnedMask)
        {
            nodes = default;
            constraints = default;
            endpoints = default;
            segmentTensions = default;
            solverStats = default;
            forcePackets = default;
            telemetryRing = default;
            telemetryHead = default;
            pinnedAups = default;
            pinnedMask = default;
            if (vault == null)
                return false;

            if (!TryOpenExistingPhysicsVaultBuffer(
                    vault,
                    BufferID.Shinobu143TetherAupNodes,
                    TetherAupRuntimeConstants.MockNodeCapacity,
                    out nodes) ||
                !TryOpenExistingPhysicsVaultBuffer(
                    vault,
                    BufferID.Shinobu143TetherConstraints,
                    TetherAupRuntimeConstants.MockConstraintCapacity,
                    out constraints) ||
                !TryOpenExistingPhysicsVaultBuffer(
                    vault,
                    BufferID.Shinobu143TetherEndpoints,
                    TetherAupRuntimeConstants.MockTetherCount,
                    out endpoints) ||
                !TryOpenExistingPhysicsVaultBuffer(
                    vault,
                    BufferID.Shinobu143TetherSegmentTensions,
                    TetherAupRuntimeConstants.MockConstraintCapacity,
                    out segmentTensions) ||
                !TryOpenExistingPhysicsVaultBuffer(
                    vault,
                    BufferID.Shinobu143TetherSolverStats,
                    TetherAupRuntimeConstants.SolverStatsCapacity,
                    out solverStats) ||
                !TryOpenExistingPhysicsVaultBuffer(
                    vault,
                    BufferID.Shinobu143TetherForcePackets,
                    TetherAupRuntimeConstants.MockForcePacketCapacity,
                    out forcePackets) ||
                !TryOpenExistingPhysicsVaultBuffer(
                    vault,
                    BufferID.Shinobu143TetherTelemetryRing,
                    TetherAupRuntimeConstants.TelemetryCapacity,
                    out telemetryRing) ||
                !TryOpenExistingPhysicsVaultBuffer(
                    vault,
                    BufferID.Shinobu143TetherTelemetryHead,
                    1,
                    out telemetryHead) ||
                !TryOpenExistingPhysicsVaultBuffer(
                    vault,
                    BufferID.Shinobu143TetherPinnedAups,
                    TetherAupRuntimeConstants.MockNodeCapacity,
                    out pinnedAups) ||
                !TryOpenExistingPhysicsVaultBuffer(
                    vault,
                    BufferID.Shinobu143TetherPinnedMask,
                    TetherAupRuntimeConstants.MockNodeCapacity,
                    out pinnedMask))
            {
                return false;
            }

            return nodes.IsCreated &&
                constraints.IsCreated &&
                endpoints.IsCreated &&
                segmentTensions.IsCreated &&
                solverStats.IsCreated &&
                forcePackets.IsCreated &&
                telemetryRing.IsCreated &&
                telemetryHead.IsCreated &&
                pinnedAups.IsCreated &&
                pinnedMask.IsCreated;
        }

        private void RefreshShinobu143AupViewsCold()
        {
            IDataVault vault = _dataVault;
            if (vault == null)
            {
                _shinobu143AupViews.Clear();
                return;
            }

            if (!TryResolveShinobu143AupBuffers(
                    vault,
                    out NativeArray<TetherNodeDTO> nodes,
                    out NativeArray<TetherConstraintDTO> constraints,
                    out NativeArray<TetherEndpointAupDTO> endpoints,
                    out NativeArray<float> segmentTensions,
                    out NativeArray<float> solverStats,
                    out NativeArray<TetherForcePacketDTO> forcePackets,
                    out NativeArray<TetherAupTelemetryEntry> telemetryRing,
                    out NativeArray<int> telemetryHead,
                    out NativeArray<double3> pinnedAups,
                    out NativeArray<byte> pinnedMask))
            {
                _shinobu143AupViews.Clear();
                return;
            }

            _shinobu143AupViews.Set(
                vault,
                vault.VaultGenerationID,
                nodes,
                constraints,
                endpoints,
                segmentTensions,
                solverStats,
                forcePackets,
                telemetryRing,
                telemetryHead,
                pinnedAups,
                pinnedMask);
        }

        private struct Shinobu143AupBufferViews
        {
            private IDataVault _vault;
            private uint _vaultGenerationId;
            private NativeArray<TetherNodeDTO> _nodes;
            private NativeArray<TetherConstraintDTO> _constraints;
            private NativeArray<TetherEndpointAupDTO> _endpoints;
            private NativeArray<float> _segmentTensions;
            private NativeArray<float> _solverStats;
            private NativeArray<TetherForcePacketDTO> _forcePackets;
            private NativeArray<TetherAupTelemetryEntry> _telemetryRing;
            private NativeArray<int> _telemetryHead;
            private NativeArray<double3> _pinnedAups;
            private NativeArray<byte> _pinnedMask;

            public void Clear()
            {
                _vault = null;
                _vaultGenerationId = 0u;
                _nodes = default;
                _constraints = default;
                _endpoints = default;
                _segmentTensions = default;
                _solverStats = default;
                _forcePackets = default;
                _telemetryRing = default;
                _telemetryHead = default;
                _pinnedAups = default;
                _pinnedMask = default;
            }

            public void Set(
                IDataVault vault,
                uint vaultGenerationId,
                NativeArray<TetherNodeDTO> nodes,
                NativeArray<TetherConstraintDTO> constraints,
                NativeArray<TetherEndpointAupDTO> endpoints,
                NativeArray<float> segmentTensions,
                NativeArray<float> solverStats,
                NativeArray<TetherForcePacketDTO> forcePackets,
                NativeArray<TetherAupTelemetryEntry> telemetryRing,
                NativeArray<int> telemetryHead,
                NativeArray<double3> pinnedAups,
                NativeArray<byte> pinnedMask)
            {
                _vault = vault;
                _vaultGenerationId = vaultGenerationId;
                _nodes = nodes;
                _constraints = constraints;
                _endpoints = endpoints;
                _segmentTensions = segmentTensions;
                _solverStats = solverStats;
                _forcePackets = forcePackets;
                _telemetryRing = telemetryRing;
                _telemetryHead = telemetryHead;
                _pinnedAups = pinnedAups;
                _pinnedMask = pinnedMask;
            }

            public bool TryRead(
                IDataVault vault,
                out NativeArray<TetherNodeDTO> nodes,
                out NativeArray<TetherConstraintDTO> constraints,
                out NativeArray<TetherEndpointAupDTO> endpoints,
                out NativeArray<float> segmentTensions,
                out NativeArray<float> solverStats,
                out NativeArray<TetherForcePacketDTO> forcePackets,
                out NativeArray<TetherAupTelemetryEntry> telemetryRing,
                out NativeArray<int> telemetryHead,
                out NativeArray<double3> pinnedAups,
                out NativeArray<byte> pinnedMask)
            {
                nodes = default;
                constraints = default;
                endpoints = default;
                segmentTensions = default;
                solverStats = default;
                forcePackets = default;
                telemetryRing = default;
                telemetryHead = default;
                pinnedAups = default;
                pinnedMask = default;

                if (vault == null ||
                    !object.ReferenceEquals(_vault, vault) ||
                    vault.IsCompactionFenceActive ||
                    vault.VaultGenerationID != _vaultGenerationId ||
                    !IsReady())
                {
                    return false;
                }

                nodes = _nodes;
                constraints = _constraints;
                endpoints = _endpoints;
                segmentTensions = _segmentTensions;
                solverStats = _solverStats;
                forcePackets = _forcePackets;
                telemetryRing = _telemetryRing;
                telemetryHead = _telemetryHead;
                pinnedAups = _pinnedAups;
                pinnedMask = _pinnedMask;
                return true;
            }

            private bool IsReady()
            {
                return _nodes.IsCreated &&
                    _nodes.Length >= TetherAupRuntimeConstants.MockNodeCapacity &&
                    _constraints.IsCreated &&
                    _constraints.Length >= TetherAupRuntimeConstants.MockConstraintCapacity &&
                    _endpoints.IsCreated &&
                    _endpoints.Length >= TetherAupRuntimeConstants.MockTetherCount &&
                    _segmentTensions.IsCreated &&
                    _segmentTensions.Length >= TetherAupRuntimeConstants.MockConstraintCapacity &&
                    _solverStats.IsCreated &&
                    _solverStats.Length >= TetherAupRuntimeConstants.SolverStatsCapacity &&
                    _forcePackets.IsCreated &&
                    _forcePackets.Length >= TetherAupRuntimeConstants.MockForcePacketCapacity &&
                    _telemetryRing.IsCreated &&
                    _telemetryRing.Length >= TetherAupRuntimeConstants.TelemetryCapacity &&
                    _telemetryHead.IsCreated &&
                    _telemetryHead.Length > 0 &&
                    _pinnedAups.IsCreated &&
                    _pinnedAups.Length >= TetherAupRuntimeConstants.MockNodeCapacity &&
                    _pinnedMask.IsCreated &&
                    _pinnedMask.Length >= TetherAupRuntimeConstants.MockNodeCapacity;
            }
        }

        internal static HectonQualityTier SanitizeQualityTier(HectonQualityTier tier)
        {
            switch (tier)
            {
                case HectonQualityTier.Low:
                case HectonQualityTier.CompactPc:
                case HectonQualityTier.Mid:
                case HectonQualityTier.High:
                case HectonQualityTier.Ultra:
                    return tier;
                default:
                    return HectonQualityTier.Unknown;
            }
        }

        private static float ResolveGlobalQualityWeight01()
        {
            float qualityWeight = HomeostasisBrain.GlobalQualityWeight;
            if (!math.isfinite(qualityWeight))
                qualityWeight = 1f;

            return math.saturate(qualityWeight);
        }

        private static HectonQualityTier ResolveQualityTierFromGlobalWeight(float qualityWeight)
        {
            return SanitizeQualityTier((HectonQualityTier)ResolveCompatibilityQualityTierOrdinal(qualityWeight));
        }

        private static int ResolveCompatibilityQualityTierOrdinal(float qualityWeight)
        {
            float q = Smooth01(math.isfinite(qualityWeight) ? qualityWeight : 1f);
            float tierOrdinal = math.lerp((int)HectonQualityTier.Low, (int)HectonQualityTier.Ultra, q);
            return math.clamp(
                (int)math.round(tierOrdinal),
                (int)HectonQualityTier.Low,
                (int)HectonQualityTier.Ultra);
        }

        private static float Smooth01(float value)
        {
            float x = math.saturate(value);
            return x * x * (3f - 2f * x);
        }

        private Material ResolveRenderMaterial()
        {
            return tetherRenderMaterial;
        }

        internal float ResolveTowSpringStiffness(HeavyTowWinch owner)
        {
            if (towCableProfile != null)
                return SanitizeProfileScalar(towCableProfile.SpringStiffness, 0f, 0f);

            return owner != null ? owner.ResolveTowSpringStiffness() : 0f;
        }

        internal float ResolveTowOverDampingMultiplier(HeavyTowWinch owner)
        {
            if (towCableProfile != null)
                return SanitizeProfileScalar(towCableProfile.OverDampingMultiplier, 1f, 1f);

            return owner != null ? owner.ResolveTowOverDampingMultiplier() : 1f;
        }

        internal float ResolveTowSnapTensionThreshold(HeavyTowWinch owner)
        {
            if (towCableProfile != null)
                return SanitizeProfileScalar(towCableProfile.SnapTensionThreshold, 1f, 1f);

            return owner != null ? owner.ResolveSnapTensionThreshold() : 1f;
        }

        private static float SanitizeProfileScalar(float value, float minimum, float fallback)
        {
            return math.isfinite(value) ? math.max(minimum, value) : fallback;
        }

        private float ResolvePeakTension()
        {
            float peak = 0f;
            for (int i = 0; i < _activeInstances.Count; i++)
            {
                TetherInstance instance = _activeInstances[i];
                if (instance == null || !instance.IsActive)
                    continue;

                peak = math.max(peak, instance.CurrentPeakTension);
            }

            return peak;
        }

        private static bool TryOpenExistingPhysicsVaultBuffer<T>(
            IDataVault vault,
            BufferID bufferId,
            int requiredLength,
            out NativeArray<T> buffer)
            where T : struct
        {
            buffer = default;
            if (vault == null ||
                requiredLength <= 0 ||
                !vault.TryGetGenerationHandle(bufferId, out VaultGenerationHandle<T> handle))
            {
                return false;
            }

            return TryOpenPhysicsVaultBuffer(vault, ref handle, bufferId, requiredLength, out buffer);
        }

        private static bool TryOpenPhysicsVaultBuffer<T>(
            IDataVault vault,
            ref VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength,
            out NativeArray<T> buffer)
            where T : struct
        {
            buffer = default;
            if (vault == null ||
                requiredLength <= 0 ||
                !IsPhysicsVaultHandle(in handle, bufferId) ||
                !vault.TryResolveHandle(in handle, out buffer) ||
                !buffer.IsCreated ||
                buffer.Length < requiredLength)
            {
                buffer = default;
                return false;
            }

            return true;
        }

        private static bool IsPhysicsVaultHandle<T>(
            in VaultGenerationHandle<T> handle,
            BufferID bufferId)
            where T : struct
        {
            return handle.BufferID == (uint)bufferId &&
                   handle.SystemID == (uint)SystemID.Physics &&
                   handle.Generation != 0u;
        }

        private bool TryLockShinobu143AupMockBuffers(IDataVault vault, out IDataVault pinVault)
        {
            pinVault = null;
            if (vault == null || _shinobu143AupMockPinMask != 0u || vault.IsCompactionFenceActive)
                return false;

            _shinobu143AupMockPinVault = vault;
            bool locked = false;
            try
            {
                if (!TryLockShinobu143AupMockBuffer(BufferID.Shinobu143TetherAupNodes, Shinobu143AupPinNodes) ||
                    !TryLockShinobu143AupMockBuffer(BufferID.Shinobu143TetherConstraints, Shinobu143AupPinConstraints) ||
                    !TryLockShinobu143AupMockBuffer(BufferID.Shinobu143TetherEndpoints, Shinobu143AupPinEndpoints) ||
                    !TryLockShinobu143AupMockBuffer(BufferID.Shinobu143TetherSegmentTensions, Shinobu143AupPinSegmentTensions) ||
                    !TryLockShinobu143AupMockBuffer(BufferID.Shinobu143TetherSolverStats, Shinobu143AupPinSolverStats) ||
                    !TryLockShinobu143AupMockBuffer(BufferID.Shinobu143TetherForcePackets, Shinobu143AupPinForcePackets) ||
                    !TryLockShinobu143AupMockBuffer(BufferID.Shinobu143TetherTelemetryRing, Shinobu143AupPinTelemetryRing) ||
                    !TryLockShinobu143AupMockBuffer(BufferID.Shinobu143TetherTelemetryHead, Shinobu143AupPinTelemetryHead) ||
                    !TryLockShinobu143AupMockBuffer(BufferID.Shinobu143TetherPinnedAups, Shinobu143AupPinPinnedAups) ||
                    !TryLockShinobu143AupMockBuffer(BufferID.Shinobu143TetherPinnedMask, Shinobu143AupPinPinnedMask))
                {
                    return false;
                }

                pinVault = vault;
                locked = true;
                return true;
            }
            finally
            {
                if (!locked)
                    ReleaseShinobu143AupMockPins();
            }
        }

        private bool TryLockShinobu143AupMockBuffer(BufferID bufferId, uint pinBit)
        {
            IDataVault vault = _shinobu143AupMockPinVault;
            if (vault == null)
                return false;

            if ((_shinobu143AupMockPinMask & pinBit) != 0u)
                return true;

            if (!vault.TryLockBuffer(bufferId, SystemID.Physics))
                return false;

            _shinobu143AupMockPinMask |= pinBit;
            return true;
        }

        private void ReleaseShinobu143AupMockPins()
        {
            IDataVault vault = _shinobu143AupMockPinVault;
            uint mask = _shinobu143AupMockPinMask;
            _shinobu143AupMockPinVault = null;
            _shinobu143AupMockPinMask = 0u;
            if (vault == null || mask == 0u)
                return;

            TryUnlockShinobu143AupMockPin(vault, mask, Shinobu143AupPinPinnedMask, BufferID.Shinobu143TetherPinnedMask);
            TryUnlockShinobu143AupMockPin(vault, mask, Shinobu143AupPinPinnedAups, BufferID.Shinobu143TetherPinnedAups);
            TryUnlockShinobu143AupMockPin(vault, mask, Shinobu143AupPinTelemetryHead, BufferID.Shinobu143TetherTelemetryHead);
            TryUnlockShinobu143AupMockPin(vault, mask, Shinobu143AupPinTelemetryRing, BufferID.Shinobu143TetherTelemetryRing);
            TryUnlockShinobu143AupMockPin(vault, mask, Shinobu143AupPinForcePackets, BufferID.Shinobu143TetherForcePackets);
            TryUnlockShinobu143AupMockPin(vault, mask, Shinobu143AupPinSolverStats, BufferID.Shinobu143TetherSolverStats);
            TryUnlockShinobu143AupMockPin(vault, mask, Shinobu143AupPinSegmentTensions, BufferID.Shinobu143TetherSegmentTensions);
            TryUnlockShinobu143AupMockPin(vault, mask, Shinobu143AupPinEndpoints, BufferID.Shinobu143TetherEndpoints);
            TryUnlockShinobu143AupMockPin(vault, mask, Shinobu143AupPinConstraints, BufferID.Shinobu143TetherConstraints);
            TryUnlockShinobu143AupMockPin(vault, mask, Shinobu143AupPinNodes, BufferID.Shinobu143TetherAupNodes);
        }

        private static void TryUnlockShinobu143AupMockPin(IDataVault vault, uint mask, uint pinBit, BufferID bufferId)
        {
            if ((mask & pinBit) != 0u)
                vault.TryUnlockBuffer(bufferId, SystemID.Physics);
        }

        private void WriteBlackBoxSample(int activeTethers, float peakTension, uint flags)
        {
            if (!_telemetryState.IsReady(TetherBlackBoxCapacity))
                return;

            if (!math.isfinite(peakTension))
            {
                peakTension = 0f;
                flags |= 1u;
            }

            TetherManagerTelemetryEntry entry = default;
            entry.FrameIndex = CurrentFixedFrameIndex;
            entry.ActiveTethers = activeTethers;
            entry.PeakTension = peakTension;
            entry.Flags = flags;
            if (!_telemetryState.TryWrite(in entry))
            {
                return;
            }

            if ((flags & 1u) != 0u)
                DumpBlackBoxOnce();
        }

        private void DumpBlackBoxOnce()
        {
            if (_telemetryDumped ||
                !_telemetryState.TryRead(out NativeArray<TetherManagerTelemetryEntry> telemetryRing, out int telemetryHead))
            {
                return;
            }

            _telemetryDumped = true;
            try
            {
                TetherBlackBoxDumpWriter.WritePrimaryAndLegacy(
                    TetherBlackBoxH8DumpRelativePath,
                    TetherBlackBoxDumpRelativePath,
                    TetherBlackBoxMagic,
                    telemetryRing,
                    telemetryHead,
                    1u);
            }
            catch
            {
                // Fault-path dump must not cascade into physics failure.
            }
        }

        private struct TetherTelemetryState : System.IDisposable
        {
            private const SystemID OwnerSystem = SystemID.Physics;

            private NativeArray<TetherManagerTelemetryEntry> _ring;
            private NativeArray<int> _head;

            public bool IsReady(int capacity)
            {
                return _ring.IsCreated &&
                       _ring.Length >= capacity &&
                       _head.IsCreated &&
                       _head.Length > 0;
            }

            public bool Ensure(int capacity)
            {
                if (IsReady(capacity))
                    return true;

                Dispose();
                if (capacity <= 0)
                    return false;

                try
                {
                    _ring = H8Memory.Allocate<TetherManagerTelemetryEntry>(
                        capacity,
                        OwnerSystem,
                        Allocator.Persistent,
                        NativeArrayOptions.ClearMemory);

                    _head = H8Memory.Allocate<int>(
                        1,
                        OwnerSystem,
                        Allocator.Persistent,
                        NativeArrayOptions.ClearMemory);
                    if (!_ring.IsCreated || !_head.IsCreated)
                    {
                        Dispose();
                        return false;
                    }

                    return true;
                }
                catch
                {
                    Dispose();
                    return false;
                }
            }

            public bool TryWrite(in TetherManagerTelemetryEntry entry)
            {
                if (!IsReady(1))
                    return false;

                int head = _head[0];
                if (head < 0 || head >= _ring.Length)
                    head = 0;

                _ring[head] = entry;
                head++;
                if (head >= _ring.Length)
                    head = 0;

                _head[0] = head;
                return true;
            }

            public bool TryRead(out NativeArray<TetherManagerTelemetryEntry> ring, out int head)
            {
                ring = default;
                head = 0;
                if (!IsReady(1))
                    return false;

                ring = _ring;
                head = _head[0];
                if (head < 0 || head >= _ring.Length)
                    head = 0;
                return true;
            }

            public void Dispose()
            {
                H8Memory.Release(ref _head, OwnerSystem);
                H8Memory.Release(ref _ring, OwnerSystem);
            }
        }
    }
}
