using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using Hecton8.Core.Contracts;
using Hecton8.Core.Memory;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Gameplay;
using Hecton8.Optimization;
using Hecton8.World;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hecton8.Core
{
    /// <summary>
    /// Manages the world origin shift to maintain precision while preserving an
    /// absolute-universe coordinate space for async systems.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-10000)]
    public sealed class HectonFloatingOrigin : MonoBehaviour, ITickable, IUpdatable, ILateFrameTickable, IServiceHeartbeat, IServiceShutdown, IGlobalRegistryHotSwapListener
    {
        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
        private struct AupDriftCheckJob : IJobParallelFor
        {
            [ReadOnly, NoAlias] public NativeArray<double3> RuntimePositions;
            [ReadOnly, NoAlias] public NativeArray<double3> TrackedAbsolutePositions;
            public double3 CommittedTotalOffset;
            public double MaxDeltaSq;
            [WriteOnly, NoAlias] public NativeArray<byte> InvalidMask;

            public void Execute(int index)
            {
                double3 expectedRuntime = TrackedAbsolutePositions[index] - CommittedTotalOffset;
                double3 delta = expectedRuntime - RuntimePositions[index];
                bool finite =
                    math.all(math.isfinite(expectedRuntime)) &&
                    math.all(math.isfinite(delta)) &&
                    math.all(math.isfinite(RuntimePositions[index]));
                InvalidMask[index] = !finite || math.lengthsq(delta) > MaxDeltaSq ? (byte)1 : (byte)0;
            }
        }

        private struct CriticalAupTracker
        {
            public Transform Transform;
            public double3 AbsolutePosition;
            public Vector3 LastRuntimePosition;
            public bool Initialized;
        }

        private static readonly int _AupJitterMaskId = Shader.PropertyToID("_AupJitterMask");
        private const int OriginShiftListenerCapacity = 1024;
        private static readonly ListenerSlot[] _originShiftListeners = new ListenerSlot[OriginShiftListenerCapacity]; // COLD ALLOC: ListenerSlot[1024] - registered origin-shift listeners - owner: HectonFloatingOrigin
        private static int _originShiftListenerCount;
        private const int OriginShiftParticleSystemCapacity = 4096;
        private static readonly ParticleSystem[] _originShiftParticleSystems = new ParticleSystem[OriginShiftParticleSystemCapacity]; // COLD ALLOC: ParticleSystem[4096] - scene-discovered world-space particle rebase registry - owner: HectonFloatingOrigin
        private static int _originShiftParticleSystemCount;
        private const int PrecisionWatchdogIntervalFrames = 300;
        private const int ShiftStabilityWatchdogFrames = 1200;
        private const float PrecisionWatchdogSafeRadiusMeters = HectonPhysicsContract.AupSectorSizeMetersFloat;
        private const float PrecisionWatchdogSafeRadiusSq = PrecisionWatchdogSafeRadiusMeters * PrecisionWatchdogSafeRadiusMeters;
        private const float MinimumShiftThresholdMeters = 2000f;
        private const float ShiftDeadzoneReleaseMeters = 4500f;
        private const float OutwardMotionSpeedEpsilon = 0.05f;
        private const int DriftCheckEntityCapacity = 2;
        private const SystemID DriftCheckOwnerSystemId = SystemID.CoreDeterminism;
        private const int OriginShiftParticleBufferCapacity = 16384;
        private const int ShiftSceneRootCapacity = 1024;
        private const int ShiftParticleSystemCapacity = 4096;
        private const float DriftCheckThresholdMeters = 0.001f;
        private const double DriftCheckThresholdSq = (double)DriftCheckThresholdMeters * DriftCheckThresholdMeters;
        private const int PostShiftUnloadUnusedAssetsMinimumFrames = 300;

        private static OriginShiftEventData _lastShiftEvent;
        private static HectonFloatingOrigin s_activeRuntime;

        // COLD ALLOC: List<GameObject>[1024] - scene root staging for shift target and particle rebases - owner: HectonFloatingOrigin
        private readonly List<GameObject> _sceneRootObjects = new List<GameObject>(ShiftSceneRootCapacity);
        private readonly List<Scene> _pendingLoadedScenes = new List<Scene>(8);
        // COLD ALLOC: List<Transform>[1024] - root transform staging for VISUAL_SYNC origin shifts - owner: HectonFloatingOrigin
        private readonly List<Transform> _shiftTargetTransforms = new List<Transform>(ShiftSceneRootCapacity);
        private readonly List<MonoBehaviour> _sceneComponentScratch = new List<MonoBehaviour>(512);
        // COLD ALLOC: List<ParticleSystem>[4096] - shift-frame particle system discovery staging - owner: HectonFloatingOrigin
        private readonly List<ParticleSystem> _sceneParticleSystemScratch = new List<ParticleSystem>(ShiftParticleSystemCapacity);
        // COLD ALLOC: ParticleSystem.Particle[16384] - shift-frame world-space particle rebase scratch - owner: HectonFloatingOrigin
        private readonly ParticleSystem.Particle[] _originShiftParticleScratch = new ParticleSystem.Particle[OriginShiftParticleBufferCapacity];

        private Transform[] _shiftTargetArray = Array.Empty<Transform>();
        private bool _shiftTargetsDirty = true;
        private bool _isRegistered;
        private bool _lateFrameRegistered;
        private bool _hotSwapListenerRegistered;
        private bool _sceneEventsSubscribed;
        private bool _isShiftInProgress;
        private bool _hasPendingShift;
        private bool _hasPendingImmediateShiftVisualSync;
        private bool _physicsPauseActive;
        private bool _shiftDeadzoneArmed = true;
        private bool _hasPreviousAnchorPosition;
        private bool _sceneRebaseTickLockHeld;
        private bool _postShiftUnloadUnusedAssetsRunning;
        private SimulationMode _physicsSimulationModeBeforeShift = SimulationMode.FixedUpdate;
        private bool _driftCheckScheduled;
        private int _physicsResumeFrame = -1;
        private int _pendingShiftFrame = -1;
        private int _aupJitterMaskReleaseFrame = -1;
        private int _lastPostShiftUnloadUnusedAssetsFrame = -PostShiftUnloadUnusedAssetsMinimumFrames;
        private float _thresholdSqr;
        private float _anchorResolveTimer;
        private uint _shiftSequence;
        private int _driftCheckCount;
        private int _lastShiftSceneReadyCount;
        private int _lastShiftSceneTotal;
        private bool _pendingSceneSynchronizationVisualSync;
        private bool _pendingParticleSystemRebase;
        private bool _pendingAupJitterMaskUpload;
        private float _pendingAupJitterMaskValue;
        private Vector3 _previousAnchorPosition;
        private Vector3 _pendingShiftOffset;
        private Vector3 _pendingImmediateShiftVisualSyncOffset;
        private OriginShiftEventData _pendingParticleSystemRebaseEvent;
        private Rigidbody _anchorRigidbody;
        private CriticalAupTracker _playerDriftTracker;
        private CriticalAupTracker _submarineDriftTracker;
        private IDataVault _dataVault;
        private IPlayerRuntimeContext _playerRuntimeContext;
        private ISubmarineRuntimeContext _submarineRuntime;
        private VaultGenerationHandle<double3> _driftCheckRuntimePositionsHandle;
        private VaultGenerationHandle<double3> _driftCheckAbsolutePositionsHandle;
        private VaultGenerationHandle<byte> _driftCheckInvalidMaskHandle;
        private JobHandle _driftCheckHandle;
        private int _precisionWatchdogCountdown;
        private int _precisionWatchdogCachedFrame = -1;
        private bool _precisionWatchdogDueThisFrame;

        private const float AnchorResolveCooldown = 1f;

        [Header("── Settings ────────────────────────────────")]
        [Tooltip("Distance from (0,0,0) that triggers a shift.")]
        [SerializeField] private float _threshold = 4000f;

        [Tooltip("Object to follow (normally Player). If null, resolves from cached player runtime context.")]
        [SerializeField] private Transform _anchor;

        /// <summary>Cumulative absolute-universe offset committed since startup.</summary>
        public Vector3 TotalOffset { get; private set; }

        private double3 _totalOffsetDouble;

        /// <summary>Cumulative absolute-universe offset committed since startup.</summary>
        public Vector3 TotalUniverseOffset => TotalOffset;

        /// <summary>Current absolute-universe offset committed since startup.</summary>
        public static Vector3 CurrentTotalOffset
        {
            get
            {
                HectonFloatingOrigin origin = s_activeRuntime;
                return origin != null ? origin.TotalOffset : Vector3.zero;
            }
        }

        /// <summary>Current committed origin offset in double precision for AUP authority math.</summary>
        public static double3 CurrentTotalOffsetDouble
        {
            get
            {
                HectonFloatingOrigin origin = s_activeRuntime;
                return origin != null ? origin._totalOffsetDouble : double3.zero;
            }
        }

        /// <summary>Fractional fixed-step interpolation alpha sampled for the current render frame.</summary>
        public static float CurrentFixedInterpolationAlpha => ResolveFixedInterpolationAlpha();

        /// <summary>Current committed shift sequence.</summary>
        public static uint CurrentShiftSequence
        {
            get
            {
                HectonFloatingOrigin origin = s_activeRuntime;
                return origin != null ? origin._shiftSequence : 0u;
            }
        }

        /// <summary>True while the floating-origin shift job is executing.</summary>
        public static bool IsShiftInProgress
        {
            get
            {
                HectonFloatingOrigin origin = s_activeRuntime;
                return origin != null && origin._isShiftInProgress;
            }
        }

        /// <summary>True while PhysX remains paused for the shift window.</summary>
        public static bool IsPhysicsPausedForShift
        {
            get
            {
                HectonFloatingOrigin origin = s_activeRuntime;
                return origin != null && origin._physicsPauseActive;
            }
        }

        /// <summary>Editor/runtime readback for the unmanaged AUP coordinator state.</summary>
        public static bool TryGetAupUniverseTunerSnapshot(out AupUniverseTunerSnapshot snapshot)
        {
            HectonFloatingOrigin origin = s_activeRuntime;
            IDataVault vault = ResolveAupTunerVault(origin);
            uint sequence = origin != null ? origin._shiftSequence : 0u;
            return AupOriginShiftCoordinator.TryGetEditorSnapshot(vault, sequence, out snapshot);
        }

        /// <summary>Editor facade for the unmanaged rebase threshold.</summary>
        public static void SetRebaseThresholdForTuner(float thresholdMeters)
        {
            HectonFloatingOrigin origin = s_activeRuntime;
            IDataVault vault = ResolveAupTunerVault(origin);
            float threshold = math.clamp(math.isfinite(thresholdMeters) ? thresholdMeters : 4000f, 2000f, 8000f);
            if (origin != null)
            {
                origin._threshold = threshold;
                origin.RefreshThresholdCache();
            }

            AupOriginShiftCoordinator.SetRebaseThreshold(vault, threshold);
        }

        /// <summary>Editor facade that raises the unmanaged pending rebase flag.</summary>
        public static void ForceRebaseNowForTuner()
        {
            HectonFloatingOrigin origin = s_activeRuntime;
            IDataVault vault = ResolveAupTunerVault(origin);
            AupOriginShiftCoordinator.RequestManualRebase(vault);
        }

        /// <summary>Editor facade for cold CSV override reloads outside the simulation hot path.</summary>
        public static bool ReloadAupConstantsForTuner()
        {
#if UNITY_EDITOR
            HectonFloatingOrigin origin = s_activeRuntime;
            IDataVault vault = ResolveAupTunerVault(origin);
            return AupOriginShiftCoordinator.TryReloadCsvOverrideFromDisk(vault);
#else
            return false;
#endif
        }

        private static IDataVault ResolveAupTunerVault(HectonFloatingOrigin origin)
        {
            return origin != null ? origin._dataVault : GlobalRegistry.DataVault;
        }

        /// <summary>Last committed shift event payload.</summary>
        public static OriginShiftEventData LastShiftEvent => _lastShiftEvent;

        /// <inheritdoc />
        public ServiceHeartbeatState HeartbeatState => _isRegistered && !_isShiftInProgress ? ServiceHeartbeatState.Ready : ServiceHeartbeatState.Booting;

        /// <inheritdoc />
        public bool IsServiceReady => _isRegistered && !_isShiftInProgress;

        /// <summary>
        /// Ensures a live floating-origin owner exists for bootstrap-owned simulation.
        /// </summary>
        /// <returns>Live floating-origin owner.</returns>
        internal static HectonFloatingOrigin EnsureRuntimeInstance()
        {
            HectonFloatingOrigin origin = s_activeRuntime;
            if (origin != null)
                return origin;

            origin = GlobalRegistry.FloatingOrigin;
            if (origin != null)
            {
                s_activeRuntime = origin;
                return origin;
            }

            // Player-build construction path: no authored/bootstrap instance reachable.
            // Floating origin owns AUP/origin rebase authority; without create, world
            // simulation loses continuous coordinate space when bootstrap reorders.            GameObject runtimeRoot = new GameObject("[HectonFloatingOrigin]"); // COLD ALLOC: GameObject[1] - bootstrap-owned AUP/floating-origin authority - owner: HectonFloatingOrigin
            return runtimeRoot.AddComponent<HectonFloatingOrigin>();
        }

        /// <summary>
        /// Freezes physics integration and clears transient kinematic state before instantaneous AUP travel.
        /// </summary>
        public static void BeginSafeTeleportProtocol()
        {
            GlobalRegistry.Physics?.ClearQueuedPackets();
            HectonFloatingOrigin origin = s_activeRuntime;
            if (origin != null)
                BeginSafeTeleportProtocolInternal(origin);
        }

        /// <summary>
        /// Releases the one-frame physics pause requested by <see cref="BeginSafeTeleportProtocol"/>.
        /// </summary>
        public static void EndSafeTeleportProtocol()
        {
            HectonFloatingOrigin origin = s_activeRuntime;
            if (origin != null)
                origin._physicsResumeFrame = SystemDispatcher.CurrentFrameIndex + 1;
        }

        internal static bool TryFlushInitialSceneRebaseBeforeTicks()
        {
            HectonFloatingOrigin origin = s_activeRuntime;
            if (origin == null)
                return true;

            // Active transform shift job owns the world; do not mutate scenes under it.
            if (origin._isShiftInProgress)
                return false;

            // Physics pause must NOT soft-deadlock the dispatcher bootstrap lock.
            // Pending loaded scenes acquire SceneRebaseTickLock; the previous early-return
            // here (and in ProcessPending) left that lock held while FO.Tick - the only
            // ResumePhysicsAfterShift driver - was starved by IsOriginShiftBootstrapLocked.
            // Drive the pause frame-gate and drain pending scene offset apply from this
            // path (headless Update calls us outside the locked master sim).
            if (origin._physicsPauseActive &&
                SystemDispatcher.CurrentFrameIndex >= origin._physicsResumeFrame)
            {
                origin.ResumePhysicsAfterShift();
            }

            if (origin._pendingLoadedScenes.Count > 0 || origin._shiftTargetsDirty)
                origin.ProcessPendingSceneSynchronization();

            // If physics remains paused only because the scene-rebase barrier never
            // advanced (async broadcast cancelled/watchdog) and there is nothing left
            // to apply, complete the barrier so ResumePhysicsAfterShift can finish and
            // the bootstrap lock can release.
            if (origin._physicsPauseActive &&
                origin._pendingLoadedScenes.Count == 0 &&
                HasPendingSceneRebaseBarrier(origin))
            {
                origin.CompleteSceneRebaseBarrier();
                origin.ResumePhysicsAfterShift();
            }

            return origin._pendingLoadedScenes.Count == 0 &&
                   !origin._shiftTargetsDirty &&
                   !origin._sceneRebaseTickLockHeld &&
                   !origin._physicsPauseActive;
        }


        /// <summary>
        /// Headless/bootstrap diagnostics: FO lock + pause + pending scene state.
        /// Does not mutate. Used by HeadlessSimulationRunner timeout/wait traces.
        /// </summary>
        internal static void CopyBootstrapDrainSnapshot(
            out bool hasOrigin,
            out bool shiftInProgress,
            out bool physicsPauseActive,
            out bool sceneRebaseTickLockHeld,
            out int pendingLoadedSceneCount,
            out bool shiftTargetsDirty,
            out bool sceneRebaseBarrierPending)
        {
            HectonFloatingOrigin origin = s_activeRuntime;
            if (origin == null)
            {
                hasOrigin = false;
                shiftInProgress = false;
                physicsPauseActive = false;
                sceneRebaseTickLockHeld = false;
                pendingLoadedSceneCount = 0;
                shiftTargetsDirty = false;
                sceneRebaseBarrierPending = false;
                return;
            }

            hasOrigin = true;
            shiftInProgress = origin._isShiftInProgress;
            physicsPauseActive = origin._physicsPauseActive;
            sceneRebaseTickLockHeld = origin._sceneRebaseTickLockHeld;
            pendingLoadedSceneCount = origin._pendingLoadedScenes.Count;
            shiftTargetsDirty = origin._shiftTargetsDirty;
            sceneRebaseBarrierPending = HasPendingSceneRebaseBarrier(origin);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _lastShiftEvent = default;
            s_activeRuntime = null;
            ClearOriginShiftListeners();
            ClearOriginShiftParticleSystems();
            HectonShaderGlobalDataVaultBridge.ResetAupShaderGlobals();
            HectonXRRuntimeState.ResetShaderGlobals();
        }

        /// <summary>
        /// Converts the supplied runtime-space position into absolute-universe space
        /// using the currently committed offset.
        /// </summary>
        /// <param name="runtimePosition">Runtime-space position.</param>
        /// <returns>Absolute-universe position.</returns>
        public static Vector3 ToAbsoluteUniversePosition(Vector3 runtimePosition)
        {
            double3 absolutePosition = ToAbsoluteUniversePositionDouble3(runtimePosition);
            return ToVector3(absolutePosition);
        }

        /// <summary>
        /// Converts runtime-space to absolute-universe coordinates without reducing the committed offset to float.
        /// </summary>
        public static double3 ToAbsoluteUniversePositionDouble3(Vector3 runtimePosition)
        {
            return new double3(runtimePosition.x, runtimePosition.y, runtimePosition.z) + CurrentTotalOffsetDouble;
        }

        /// <summary>
        /// Converts the supplied absolute-universe position into runtime space
        /// using the currently committed offset.
        /// </summary>
        /// <param name="absoluteUniversePosition">Absolute-universe position.</param>
        /// <returns>Runtime-space position.</returns>
        public static Vector3 ToRuntimePosition(Vector3 absoluteUniversePosition)
        {
            return ToRuntimePosition(
                new double3(absoluteUniversePosition.x, absoluteUniversePosition.y, absoluteUniversePosition.z),
                CurrentTotalOffsetDouble);
        }

        /// <summary>
        /// Converts the supplied absolute-universe position into runtime space
        /// using the currently committed offset.
        /// </summary>
        /// <param name="absoluteUniversePosition">Absolute-universe position.</param>
        /// <returns>Runtime-space position.</returns>
        public static Vector3 ToRuntimePosition(double3 absoluteUniversePosition)
        {
            return ToRuntimePosition(absoluteUniversePosition, CurrentTotalOffsetDouble);
        }

        /// <summary>
        /// Converts the supplied absolute-universe position into runtime space
        /// using an explicit committed total offset.
        /// </summary>
        /// <param name="absoluteUniversePosition">Absolute-universe position.</param>
        /// <param name="committedTotalOffset">Committed absolute-universe offset.</param>
        /// <returns>Runtime-space position.</returns>
        public static Vector3 ToRuntimePosition(Vector3 absoluteUniversePosition, Vector3 committedTotalOffset)
        {
            return ToRuntimePosition(
                new double3(absoluteUniversePosition.x, absoluteUniversePosition.y, absoluteUniversePosition.z),
                new double3(committedTotalOffset.x, committedTotalOffset.y, committedTotalOffset.z));
        }

        /// <summary>
        /// Converts the supplied absolute-universe position into runtime space
        /// using an explicit committed total offset.
        /// </summary>
        /// <param name="absoluteUniversePosition">Absolute-universe position.</param>
        /// <param name="committedTotalOffset">Committed absolute-universe offset.</param>
        /// <returns>Runtime-space position.</returns>
        public static Vector3 ToRuntimePosition(double3 absoluteUniversePosition, double3 committedTotalOffset)
        {
            return ToVector3(absoluteUniversePosition - committedTotalOffset);
        }

        internal static void ResyncBody(Rigidbody body, in AbsoluteUniversePosition absolutePosition)
        {
            if (body == null)
                return;

            float3 runtimePosition3 = absolutePosition.ToRuntimeFloat3();
            if (!math.all(math.isfinite(runtimePosition3)))
                return;

            Vector3 runtimePosition = new Vector3(
                runtimePosition3.x,
                runtimePosition3.y,
                runtimePosition3.z);
            Vector3 linearVelocity = IsFiniteVector(body.linearVelocity)
                ? body.linearVelocity
                : Vector3.zero;
            Vector3 angularVelocity = IsFiniteVector(body.angularVelocity)
                ? body.angularVelocity
                : Vector3.zero;
            bool wasSleeping = body.IsSleeping();

            body.position = runtimePosition;
            body.MovePosition(runtimePosition);
            IPhysicsService physicsService = GlobalRegistry.Physics;
            physicsService?.QueueLinearVelocitySet(body, linearVelocity, wake: !wasSleeping);
            physicsService?.QueueAngularVelocitySet(body, angularVelocity, wake: !wasSleeping);
            if (wasSleeping)
                body.Sleep();
            else
                body.WakeUp();

            body.PublishTransform();
        }

        /// <summary>
        /// Registers a listener for committed floating-origin shifts.
        /// </summary>
        /// <param name="listener">Listener to register.</param>
        public static void RegisterListener(IOriginShiftListener listener)
        {
            if (listener == null)
                return;

            TryRegisterOriginShiftListener(listener);
        }

        /// <summary>
        /// Checks whether a listener is currently registered in the fixed origin-shift bucket.
        /// </summary>
        /// <param name="listener">Listener instance to test.</param>
        /// <returns>True when the listener is present.</returns>
        public static bool IsListenerRegistered(IOriginShiftListener listener)
        {
            return listener != null && ContainsOriginShiftListener(listener);
        }

        /// <summary>
        /// Unregisters a listener from committed floating-origin shifts.
        /// </summary>
        /// <param name="listener">Listener to unregister.</param>
        public static void UnregisterListener(IOriginShiftListener listener)
        {
            if (listener == null)
                return;

            TryUnregisterOriginShiftListener(listener);
        }

        /// <summary>
        /// Marks the root-transform cache dirty so the next shift rebuilds it on the cold path.
        /// </summary>
        public static void MarkShiftTargetsDirty()
        {
            HectonFloatingOrigin origin = s_activeRuntime;
            if (origin == null)
                return;

            origin._shiftTargetsDirty = true;
        }

        /// <summary>
        /// Waits until no shift job is executing and the atomic physics pause gate has ended.
        /// Async systems must call this before writing runtime transforms that depend on
        /// the current floating-origin offset.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Stable committed shift payload for the current frame.</returns>
        public static async Awaitable<OriginShiftEventData> WaitForShiftStabilityAsync(CancellationToken cancellationToken = default)
        {
            int watchdog = 0;
            HectonFloatingOrigin origin = s_activeRuntime;
            while (origin != null &&
                   (origin._isShiftInProgress ||
                    origin._physicsPauseActive ||
                    HasPendingSceneRebaseBarrier(origin)))
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (watchdog++ > ShiftStabilityWatchdogFrames)
                {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    Hecton8.Core.H8Debug.LogError("[FloatingOrigin] WaitForShiftStabilityAsync timed out.", origin);
#endif
                    break;
                }

                await AwaitableDebtMonitor.NextFrameAsync(cancellationToken);
                origin = s_activeRuntime;
            }

            double3 currentOffsetDouble = CurrentTotalOffsetDouble;
            Vector3 currentOffset = ToVector3(currentOffsetDouble);
            uint currentSequence = CurrentShiftSequence;
            return new OriginShiftEventData(
                Vector3.zero,
                currentOffset,
                currentOffset,
                currentOffsetDouble,
                currentOffsetDouble,
                currentSequence,
                SystemDispatcher.CurrentFrameIndex);
        }

        private void Awake()
        {
            HectonFloatingOrigin registeredOrigin = GlobalRegistry.FloatingOrigin;
            if (registeredOrigin != null && registeredOrigin != this)
            {
                Destroy(gameObject);
                return;
            }

            GlobalRegistry.RegisterFloatingOriginRuntime(this);
            s_activeRuntime = this;
            _dataVault = GlobalRegistry.DataVault;
            _playerRuntimeContext = GlobalRegistry.Player;
            _submarineRuntime = GlobalRegistry.Submarine;
            TryRegisterHotSwapListener();
            RefreshThresholdCache();
            AupOriginShiftCoordinator.GenerateEmergencyMockThresholds(_dataVault);
            TryResolveAnchor(force: true);
            EnsureDriftCheckBuffers();
            PublishGlobalOffsets();
            SubscribeSceneEvents();
            SynchronizeLoadedSceneOriginShiftRegistries();
        }

        private void OnEnable()
        {
            TryRegisterHotSwapListener();
            TryRegister();
            SynchronizeLoadedSceneOriginShiftRegistries();
            MarkShiftTargetsDirty();
            TryPrepareShiftTargets();
        }

        private void OnDisable()
        {
            TryUnregister();
            TryUnregisterHotSwapListener();
        }

        private void OnDestroy()
        {
            ShutdownServiceState();
        }

        public void OnServiceShutdown()
        {
            ShutdownServiceState();
        }

        /// <inheritdoc />
        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot == GlobalRegistryServiceSlot.Player)
            {
                _playerRuntimeContext = currentService as IPlayerRuntimeContext;
                if (_anchor == null && _playerRuntimeContext != null)
                    TryResolveAnchor(force: true);
                return;
            }

            if (serviceSlot == GlobalRegistryServiceSlot.Submarine)
            {
                _submarineRuntime = currentService as ISubmarineRuntimeContext;
                return;
            }

            if (serviceSlot != GlobalRegistryServiceSlot.DataVault)
                return;

            _dataVault = currentService as IDataVault;
            DisposeDriftCheckState();
            if (_dataVault == null)
                return;

            RefreshThresholdCache();
            AupOriginShiftCoordinator.GenerateEmergencyMockThresholds(_dataVault);
            EnsureDriftCheckBuffers();
            PublishGlobalOffsets();
        }

        private void ShutdownServiceState()
        {
            ReleaseSceneRebaseTickLock();
            TryUnregister();
            TryUnregisterHotSwapListener();
            UnsubscribeSceneEvents();
            DisposeDriftCheckState();

            if (ReferenceEquals(s_activeRuntime, this))
                s_activeRuntime = null;

            if (GlobalRegistry.FloatingOrigin == this)
            {
                if (_physicsPauseActive)
                {
                    UnityEngine.Physics.simulationMode = _physicsSimulationModeBeforeShift;
                }

                ClearOriginShiftListeners();
                ClearOriginShiftParticleSystems();
                GlobalRegistry.UnregisterFloatingOriginRuntime(this);
            }
        }

        /// <summary>
        /// Explicit bootstrap registration pass after <see cref="GlobalRegistry.Dispatcher"/> exists.
        /// </summary>
        internal void InitializeService()
        {
            if (GlobalRegistry.FloatingOrigin != this)
                GlobalRegistry.RegisterFloatingOriginRuntime(this);

            s_activeRuntime = this;
            _dataVault = GlobalRegistry.DataVault;
            TryRegisterHotSwapListener();
            RefreshThresholdCache();
            AupOriginShiftCoordinator.GenerateEmergencyMockThresholds(_dataVault);
            TryResolveAnchor(force: true);
            EnsureDriftCheckBuffers();
            PublishGlobalOffsets();
            SubscribeSceneEvents();
            SynchronizeLoadedSceneOriginShiftRegistries();
            TryRegister();
            MarkShiftTargetsDirty();
            TryPrepareShiftTargets();
        }

        /// <summary>
        /// Monitors anchor distance and commits a synchronized floating-origin shift.
        /// </summary>
        /// <param name="deltaTime">Scaled tick delta supplied by the tick manager.</param>
        public void Tick(float deltaTime)
        {
            UpdateAupJitterMaskRelease();
            if (_isShiftInProgress)
                return;

            if (_physicsPauseActive)
            {
                if (SystemDispatcher.CurrentFrameIndex >= _physicsResumeFrame)
                    ResumePhysicsAfterShift();
                else
                    return;
            }

            if (_shiftTargetsDirty || _pendingLoadedScenes.Count > 0)
                _pendingSceneSynchronizationVisualSync = true;

            if (_hasPendingShift)
            {
                if (SystemDispatcher.CurrentFrameIndex >= _pendingShiftFrame)
                {
                    Vector3 pendingShiftOffset = _pendingShiftOffset;
                    _pendingShiftOffset = Vector3.zero;
                    _pendingShiftFrame = -1;
                    _hasPendingShift = false;
                    QueueImmediateShiftVisualSync(pendingShiftOffset);
                }

                return;
            }

            Vector3 anchorRuntimePosition = Vector3.zero;
            bool hasAnchorRuntimePosition = _anchor != null && TryGetTransformWorldPosition(_anchor, out anchorRuntimePosition);
            IDataVault vault = _dataVault;
            if (AupOriginShiftCoordinator.TickPreSimulation(
                    vault,
                    deltaTime,
                    hasAnchorRuntimePosition,
                    anchorRuntimePosition,
                    _totalOffsetDouble,
                    out Vector3 aupRequestedShift))
            {
                _shiftDeadzoneArmed = false;
                BeginShiftWorld(aupRequestedShift);
                return;
            }

            if (_driftCheckScheduled && ConsumeCompletedDriftCheck())
                return;

            UpdateCriticalEntityTrackers();
            bool precisionWatchdogFrame = ShouldRunPrecisionWatchdogFrame();
            if (!_driftCheckScheduled && precisionWatchdogFrame)
                ScheduleAupDriftCheck();

            if (_anchor == null)
            {
                _anchorResolveTimer -= deltaTime;
                if (_anchorResolveTimer > 0f)
                    return;

                TryResolveAnchor(force: false);
                if (_anchor == null)
                    return;
            }

            if (!TryGetTransformWorldPosition(_anchor, out Vector3 anchorPosition))
                return;

            float anchorDistanceSqr = VectorLengthSq(anchorPosition);
            if (anchorDistanceSqr <= ShiftDeadzoneReleaseMeters * ShiftDeadzoneReleaseMeters)
                _shiftDeadzoneArmed = true;

            bool isMovingAwayFromCenter = IsAnchorMovingAwayFromCenter(anchorPosition, deltaTime);
            if (precisionWatchdogFrame &&
                anchorDistanceSqr >= PrecisionWatchdogSafeRadiusSq &&
                _shiftDeadzoneArmed &&
                isMovingAwayFromCenter)
            {
                _shiftDeadzoneArmed = false;
                BeginShiftWorld(anchorPosition);
                return;
            }

            if (anchorDistanceSqr > _thresholdSqr && _shiftDeadzoneArmed && isMovingAwayFromCenter)
            {
                _shiftDeadzoneArmed = false;
                BeginShiftWorld(anchorPosition);
            }
        }

        public void LateFrameTick()
        {
            if (_hasPendingImmediateShiftVisualSync && !_isShiftInProgress && !_physicsPauseActive)
            {
                Vector3 shiftOffset = _pendingImmediateShiftVisualSyncOffset;
                _pendingImmediateShiftVisualSyncOffset = Vector3.zero;
                _hasPendingImmediateShiftVisualSync = false;
                BeginShiftWorldImmediate(shiftOffset);
            }

            if (_pendingSceneSynchronizationVisualSync && !_isShiftInProgress && !_physicsPauseActive)
            {
                _pendingSceneSynchronizationVisualSync = false;
                ProcessPendingSceneSynchronization();
            }

            if (_pendingParticleSystemRebase)
            {
                OriginShiftEventData shiftEvent = _pendingParticleSystemRebaseEvent;
                _pendingParticleSystemRebaseEvent = default;
                _pendingParticleSystemRebase = false;
                RebaseParticleSystemsForOriginShift(in shiftEvent);
            }

            if (_pendingAupJitterMaskUpload)
            {
                _pendingAupJitterMaskUpload = false;
                Shader.SetGlobalFloat(_AupJitterMaskId, _pendingAupJitterMaskValue);
            }
        }

        private bool ShouldRunPrecisionWatchdogFrame()
        {
            int frame = SystemDispatcher.CurrentFrameIndex;
            if (_precisionWatchdogCachedFrame == frame)
                return _precisionWatchdogDueThisFrame;

            _precisionWatchdogCachedFrame = frame;
            if (_precisionWatchdogCountdown <= 0)
            {
                _precisionWatchdogCountdown = PrecisionWatchdogIntervalFrames - 1;
                _precisionWatchdogDueThisFrame = true;
                return true;
            }

            _precisionWatchdogCountdown--;
            _precisionWatchdogDueThisFrame = false;
            return false;
        }

        private void BeginShiftWorld(Vector3 shiftOffset)
        {
            if (!IsFiniteVector(shiftOffset))
            {
                CrashTelemetryBuffer.ReportNanPhysicsRecovery(shiftOffset, Vector3.zero);
                return;
            }

            if (VectorLengthSq(shiftOffset) <= 0.0001f)
                return;

            if (_isShiftInProgress)
                return;

            if (_hasPendingShift)
                return;

            _pendingShiftOffset = shiftOffset;
            _pendingShiftFrame = SystemDispatcher.CurrentFrameIndex + 1;
            _hasPendingShift = true;
            PublishAupPreShiftSignal(shiftOffset, _shiftSequence + 1u);
        }

        private void QueueImmediateShiftVisualSync(Vector3 shiftOffset)
        {
            _pendingImmediateShiftVisualSyncOffset = shiftOffset;
            _hasPendingImmediateShiftVisualSync = true;
            TryRegister();
        }

        private void BeginShiftWorldImmediate(Vector3 shiftOffset)
        {
            if (!IsFiniteVector(shiftOffset))
            {
                CrashTelemetryBuffer.ReportNanPhysicsRecovery(shiftOffset, Vector3.zero);
                return;
            }

            if (VectorLengthSq(shiftOffset) <= 0.0001f)
                return;

            if (_isShiftInProgress)
                return;

            _isShiftInProgress = true;
            _ = ShiftWorldAsync(shiftOffset, destroyCancellationToken);
        }

        private async Awaitable ShiftWorldAsync(Vector3 shiftOffset, CancellationToken cancellationToken)
        {
            if (!IsFiniteVector(shiftOffset))
            {
                CrashTelemetryBuffer.ReportNanPhysicsRecovery(shiftOffset, Vector3.zero);
                _isShiftInProgress = false;
                return;
            }

            _isShiftInProgress = true;
            SystemDispatcher.RequestOriginShiftFrameLock(SystemDispatcher.CurrentFrameIndex);
            bool trackedBodiesPrepared = false;
            bool trackedBodiesFinalized = false;
            bool xrPoseLockActive = false;
            bool vaultAllocationLockActive = false;
            uint nextShiftSequence = _shiftSequence + 1u;
            AupOriginShiftScheduleInfo aupScheduleInfo = default;
            double aupRebaseElapsedMs = 0d;
            int gen0CollectionCountBeforeShift = GC.CollectionCount(0);
            HectonXRRuntimeState.BeginOriginShiftPoseLock();
            xrPoseLockActive = HectonXRRuntimeState.IsXRActive;
            PausePhysicsForShift();
            IPhysicsService physicsService = GlobalRegistry.Physics;
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                IDataVault vault = _dataVault;
                if (vault != null)
                {
                    AupOriginShiftCoordinator.OpenOrAcquireRuntimeStateForOwnerRoute(vault, out _);
                    vault.LockAllocationsForAupShift(nextShiftSequence);
                    vaultAllocationLockActive = true;
                }

                if (physicsService != null)
                {
                    physicsService.PrepareTrackedBodiesForOriginShift();
                    trackedBodiesPrepared = true;
                }

                if (_shiftTargetsDirty)
                    RebuildShiftTargetCache();

                double3 shiftDouble = new double3(shiftOffset.x, shiftOffset.y, shiftOffset.z);
                long aupRebaseStartTicks = System.Diagnostics.Stopwatch.GetTimestamp();
                JobHandle aupRebaseHandle = AupOriginShiftCoordinator.ScheduleVaultOriginRebase(
                    vault,
                    shiftOffset,
                    _totalOffsetDouble + shiftDouble,
                    nextShiftSequence,
                    out aupScheduleInfo);

                CompleteAupRebaseBeforeSceneMutation(ref aupRebaseHandle);
                ApplyOriginShiftToCachedRootTransforms(shiftOffset);
                if (vault != null)
                {
                    AupOriginShiftCoordinator.ReleaseScheduledRebaseLocks(vault, in aupScheduleInfo);
                    aupScheduleInfo.Flags = 0;
                }

                aupRebaseElapsedMs = (System.Diagnostics.Stopwatch.GetTimestamp() - aupRebaseStartTicks) *
                    1000.0d /
                    System.Diagnostics.Stopwatch.Frequency;

                physicsService?.CommitTrackedBodiesForOriginShift(shiftOffset);

                double3 previousTotalOffsetDouble = _totalOffsetDouble;
                Vector3 previousTotalOffset = TotalOffset;
                _totalOffsetDouble += new double3(shiftOffset.x, shiftOffset.y, shiftOffset.z);
                TotalOffset = ToVector3(_totalOffsetDouble);
                _shiftSequence++;
                float fixedInterpolationAlpha = ResolveFixedInterpolationAlpha();
                _lastShiftEvent = new OriginShiftEventData(
                    shiftOffset,
                    previousTotalOffset,
                    TotalOffset,
                    previousTotalOffsetDouble,
                    _totalOffsetDouble,
                    _shiftSequence,
                    SystemDispatcher.CurrentFrameIndex,
                    fixedInterpolationAlpha);

                ArmAupJitterMask(SystemDispatcher.CurrentFrameIndex);
                QueueParticleSystemRebase(in _lastShiftEvent);
                PublishAupShiftSignal(in _lastShiftEvent);
                CrashTelemetryBuffer.ReportOriginShift(shiftOffset, _shiftSequence);
                PublishGlobalOffsets();
                AupOriginShiftCoordinator.RecordRebaseCompletion(
                    vault,
                    in aupScheduleInfo,
                    aupRebaseElapsedMs,
                    _totalOffsetDouble);
                HectonXRRuntimeState.EndOriginShiftPoseLock(_shiftSequence, fixedInterpolationAlpha);
                xrPoseLockActive = false;
                ResyncCriticalEntityTrackersAfterShift();
                if (physicsService != null)
                {
                    physicsService.FinalizeTrackedBodiesAfterOriginShift();
                    trackedBodiesFinalized = true;
                }
                WorldSpatialHashGrid.HandleOriginShift(_lastShiftEvent);
                await BroadcastOriginShiftAsync(_lastShiftEvent, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                CompleteSceneRebaseBarrier();
                _physicsResumeFrame = SystemDispatcher.CurrentFrameIndex;
            }
            catch (Exception exception)
            {
                CompleteSceneRebaseBarrier();
                _physicsResumeFrame = SystemDispatcher.CurrentFrameIndex;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Hecton8.Core.H8Debug.LogException(exception);
#endif
            }
            finally
            {
                if (xrPoseLockActive)
                    HectonXRRuntimeState.EndOriginShiftPoseLock(_shiftSequence, ResolveFixedInterpolationAlpha());

                if (trackedBodiesPrepared && !trackedBodiesFinalized)
                    physicsService?.FinalizeTrackedBodiesAfterOriginShift();

                if (vaultAllocationLockActive)
                {
                    IDataVault vault = _dataVault;
                    AupOriginShiftCoordinator.ReleaseScheduledRebaseLocks(vault, in aupScheduleInfo);
                    vault?.UnlockAllocationsAfterAupShift(nextShiftSequence);
                }

                _isShiftInProgress = false;
            }

            if (trackedBodiesFinalized)
                _ = RunPostShiftUnusedAssetUnloadGuardAsync(gen0CollectionCountBeforeShift, cancellationToken);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (trackedBodiesFinalized)
                Hecton8.Core.H8Debug.Log("[FloatingOrigin] shift committed.");
#endif
        }

        private static void PublishAupShiftSignal(in OriginShiftEventData shiftData)
        {
            int3 sectorDelta = ResolveAupSectorDelta(shiftData.PreviousTotalOffsetDouble, shiftData.NewTotalOffsetDouble);
            AupShiftSignal signal = new AupShiftSignal
            {
                ShiftMeters = new float3(shiftData.ShiftOffset.x, shiftData.ShiftOffset.y, shiftData.ShiftOffset.z),
                ShiftFrameId = shiftData.Sequence,
                SectorDelta = sectorDelta,
                Flags = shiftData.IsSafeTeleport != 0 ? 1u : 0u
            };
            AupSignalRoute.TryQueueShift(in signal);
        }

        private void PublishAupPreShiftSignal(Vector3 shiftOffset, uint nextShiftSequence)
        {
            double3 previousOffset = _totalOffsetDouble;
            double3 newOffset = previousOffset + new double3(shiftOffset.x, shiftOffset.y, shiftOffset.z);
            AupPreShiftSignal signal = new AupPreShiftSignal
            {
                ShiftMeters = new float3(shiftOffset.x, shiftOffset.y, shiftOffset.z),
                ShiftFrameId = nextShiftSequence,
                SectorDelta = ResolveAupSectorDelta(previousOffset, newOffset),
                Flags = 0u
            };
            AupSignalRoute.TryQueuePreShift(in signal);
        }

        private static int3 ResolveAupSectorDelta(double3 previousOffset, double3 newOffset)
        {
            AbsoluteUniversePosition previousAup = AbsoluteUniversePosition.FromAbsolutePosition(previousOffset);
            AbsoluteUniversePosition newAup = AbsoluteUniversePosition.FromAbsolutePosition(newOffset);

            return new int3(
                ClampLongToInt(newAup.GridX - previousAup.GridX),
                ClampLongToInt(newAup.GridY - previousAup.GridY),
                ClampLongToInt(newAup.GridZ - previousAup.GridZ));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int ClampLongToInt(long value)
        {
            if (value > int.MaxValue)
                return int.MaxValue;
            if (value < int.MinValue)
                return int.MinValue;
            return (int)value;
        }

        private async Awaitable RunPostShiftUnusedAssetUnloadGuardAsync(
            int gen0CollectionCountBeforeShift,
            CancellationToken cancellationToken)
        {
            if (_postShiftUnloadUnusedAssetsRunning)
                return;

            int currentFrame = SystemDispatcher.CurrentFrameIndex;
            if (currentFrame - _lastPostShiftUnloadUnusedAssetsFrame < PostShiftUnloadUnusedAssetsMinimumFrames)
                return;

            _postShiftUnloadUnusedAssetsRunning = true;
            try
            {
                await AwaitableDebtMonitor.NextFrameAsync(cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();

                if (GC.CollectionCount(0) != gen0CollectionCountBeforeShift)
                    return;

                _lastPostShiftUnloadUnusedAssetsFrame = SystemDispatcher.CurrentFrameIndex;
                AssetLifecycleGovernor governor = GlobalRegistry.AssetLifecycle;
                if (governor != null)
                {
                    governor.SetHeapSanitizerBlindFrameWindow(true, 0f);
                    try
                    {
                        governor.ForceDrainPendingReleaseQueue();
                    }
                    finally
                    {
                        governor.SetHeapSanitizerBlindFrameWindow(false, 0f);
                    }
                }

                IRenderTexturePoolService pool = GlobalRegistry.RenderTexturePoolService;
                pool?.ClearAllPools();
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                _postShiftUnloadUnusedAssetsRunning = false;
            }
        }

        private void PausePhysicsForShift()
        {
            _physicsSimulationModeBeforeShift = UnityEngine.Physics.simulationMode;
            UnityEngine.Physics.simulationMode = SimulationMode.Script;
            _physicsPauseActive = true;
            _physicsResumeFrame = SystemDispatcher.CurrentFrameIndex + 1;
        }

        private static void BeginSafeTeleportProtocolInternal(HectonFloatingOrigin origin)
        {
            if (!origin._physicsPauseActive)
                origin.PausePhysicsForShift();

            IPhysicsService physicsService = GlobalRegistry.Physics;
            physicsService?.ResetTrackedBodiesForSafeTeleportState();
            physicsService?.ArmSafeTeleportSpeculativeCcd();

            IPlayerRuntimeContext playerContext = origin._playerRuntimeContext;
            playerContext?.PlayerMovement?.ResetKinematicTransientStateForTeleport();

            ISubmarineRuntimeContext submarine = origin._submarineRuntime;
            MonoBehaviour submarineBehaviour = submarine as MonoBehaviour;
            if (submarineBehaviour != null && submarineBehaviour.TryGetComponent(out VehicleMotor vehicleMotor))
                vehicleMotor.ResetHydrodynamicPresentationState();

            double3 currentOffsetDouble = origin._totalOffsetDouble;
            Vector3 currentOffset = ToVector3(currentOffsetDouble);
            _lastShiftEvent = new OriginShiftEventData(
                Vector3.zero,
                currentOffset,
                currentOffset,
                currentOffsetDouble,
                currentOffsetDouble,
                origin._shiftSequence,
                SystemDispatcher.CurrentFrameIndex,
                ResolveFixedInterpolationAlpha(),
                isSafeTeleport: true);
        }

        private void ResumePhysicsAfterShift()
        {
            if (!_physicsPauseActive)
                return;

            if (HasPendingSceneRebaseBarrier(this))
                return;

            UnityEngine.Physics.simulationMode = _physicsSimulationModeBeforeShift;
            _physicsPauseActive = false;
            _physicsResumeFrame = -1;
        }

        private static float ResolveFixedInterpolationAlpha()
        {
            return SystemDispatcher.CurrentFixedInterpolationAlpha;
        }

        private static async Awaitable AwaitTransformShiftJobAsync(JobHandle handle, CancellationToken cancellationToken)
        {
            int watchdog = 0;
            try
            {
                while (!handle.IsCompleted)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (watchdog++ > ShiftStabilityWatchdogFrames)
                    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                        Hecton8.Core.H8Debug.LogError("[FloatingOrigin] Transform shift job timed out. Forcing completion before physics resumes.");
#endif
                        break;
                    }

                    await AwaitableDebtMonitor.NextFrameAsync(cancellationToken);
                }
            }
            finally
            {
                DispatcherJobSwap.TryComplete(ref handle, true);
            }
        }

        private static void CompleteAupRebaseBeforeSceneMutation(ref JobHandle handle)
        {
            // Origin shift is the only legal mutation window for these DataVault AUP views.
            // Completing here prevents one-frame NativeArray alias exposure while transforms,
            // tethers, telemetry, and physics owners are rebased.
            DispatcherJobSwap.TryComplete(ref handle, true);
        }

        private async Awaitable BroadcastOriginShiftAsync(OriginShiftEventData shiftData, CancellationToken cancellationToken)
        {
            int totalLoadedScenes = CountLoadedScenes();
            Interlocked.Exchange(ref _lastShiftSceneReadyCount, 0);
            Interlocked.Exchange(ref _lastShiftSceneTotal, totalLoadedScenes);

            if (totalLoadedScenes > 0)
            {
                await BroadcastSceneOriginShiftListenersAsync(shiftData, cancellationToken);
                Interlocked.Exchange(ref _lastShiftSceneReadyCount, totalLoadedScenes);
            }

            await BroadcastNonSceneOriginShiftListenersAsync(shiftData, cancellationToken);

            int watchdog = 0;
            while (HasPendingSceneRebaseBarrier(this))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (watchdog++ > ShiftStabilityWatchdogFrames)
                    break;

                await AwaitableDebtMonitor.NextFrameAsync(cancellationToken);
            }
        }

        private async Awaitable BroadcastSceneOriginShiftListenersAsync(OriginShiftEventData shiftData, CancellationToken cancellationToken)
        {
            for (int i = _originShiftListenerCount - 1; i >= 0; i--)
            {
                cancellationToken.ThrowIfCancellationRequested();
                IOriginShiftListener listener = _originShiftListeners[i].Listener;
                if (listener == null)
                {
                    RemoveOriginShiftListenerAt(i);
                    continue;
                }

                UnityEngine.Object unityListener = listener as UnityEngine.Object;
                if (!ReferenceEquals(unityListener, null) && unityListener == null)
                {
                    RemoveOriginShiftListenerAt(i);
                    continue;
                }

                if (!IsSceneResidentOriginShiftListener(listener))
                    continue;

                await DispatchOriginShiftListenerAsync(listener, shiftData, cancellationToken);
            }
        }

        private void QueueParticleSystemRebase(in OriginShiftEventData shiftData)
        {
            _pendingParticleSystemRebaseEvent = shiftData;
            _pendingParticleSystemRebase = true;
        }

        private void RebaseParticleSystemsForOriginShift(in OriginShiftEventData shiftData)
        {
            Vector3 shiftOffset = shiftData.ShiftOffset;
            if (!IsFiniteVector(shiftOffset) || VectorLengthSq(shiftOffset) <= 0.0001f)
                return;

            for (int i = _originShiftParticleSystemCount - 1; i >= 0; i--)
            {
                ParticleSystem particleSystem = _originShiftParticleSystems[i];
                if (particleSystem == null)
                {
                    RemoveOriginShiftParticleSystemAt(i);
                    continue;
                }

                RebaseParticleSystemForOriginShift(particleSystem, shiftOffset);
            }
        }

        private void RebaseParticleSystemForOriginShift(ParticleSystem particleSystem, Vector3 shiftOffset)
        {
            if (particleSystem == null)
                return;

            ParticleSystem.MainModule mainModule = particleSystem.main;
            if (mainModule.simulationSpace != ParticleSystemSimulationSpace.World)
            {
                particleSystem.Simulate(0f, false, false);
                return;
            }

            int totalParticles = particleSystem.particleCount;
            int offset = 0;
            while (offset < totalParticles)
            {
                int requestedCount = math.min(_originShiftParticleScratch.Length, totalParticles - offset);
                int particleCount = particleSystem.GetParticles(_originShiftParticleScratch, requestedCount, offset);
                if (particleCount <= 0)
                    break;

                for (int particleIndex = 0; particleIndex < particleCount; particleIndex++)
                {
                    ParticleSystem.Particle particle = _originShiftParticleScratch[particleIndex];
                    Vector3 rebasedPosition = particle.position - shiftOffset;
                    if (IsFiniteVector(rebasedPosition))
                        particle.position = rebasedPosition;
                    _originShiftParticleScratch[particleIndex] = particle;
                }

                particleSystem.SetParticles(_originShiftParticleScratch, particleCount, offset);
                offset += particleCount;
            }

            particleSystem.Simulate(0f, false, false);
        }

        private static async Awaitable DispatchOriginShiftListenerAsync(
            IOriginShiftListener listener,
            OriginShiftEventData shiftData,
            CancellationToken cancellationToken)
        {
            if (listener is IAwaitableOriginShiftListener awaitableListener)
                await awaitableListener.OnOriginShiftAsync(shiftData, cancellationToken);
            else
                listener.OnOriginShift(in shiftData);
        }

        private static bool TryRegisterOriginShiftListener(IOriginShiftListener listener)
        {
            if (ContainsOriginShiftListener(listener))
                return false;

            if (_originShiftListenerCount >= OriginShiftListenerCapacity)
                return false;

            _originShiftListeners[_originShiftListenerCount].Listener = listener;
            _originShiftListenerCount++;
            return true;
        }

        private static bool TryUnregisterOriginShiftListener(IOriginShiftListener listener)
        {
            for (int i = 0; i < _originShiftListenerCount; i++)
            {
                if (!ReferenceEquals(_originShiftListeners[i].Listener, listener))
                    continue;

                RemoveOriginShiftListenerAt(i);
                return true;
            }

            return false;
        }

        private static bool ContainsOriginShiftListener(IOriginShiftListener listener)
        {
            for (int i = 0; i < _originShiftListenerCount; i++)
            {
                if (ReferenceEquals(_originShiftListeners[i].Listener, listener))
                    return true;
            }

            return false;
        }

        private static void RemoveOriginShiftListenerAt(int index)
        {
            if ((uint)index >= (uint)_originShiftListenerCount)
                return;

            int lastIndex = _originShiftListenerCount - 1;
            _originShiftListeners[index] = _originShiftListeners[lastIndex];
            _originShiftListeners[lastIndex].Clear();
            _originShiftListenerCount = lastIndex;
        }

        private static void ClearOriginShiftListeners()
        {
            for (int i = 0; i < _originShiftListenerCount; i++)
                _originShiftListeners[i].Clear();

            _originShiftListenerCount = 0;
        }

        private static bool TryRegisterOriginShiftParticleSystem(ParticleSystem particleSystem)
        {
            if (particleSystem == null)
                return false;

            for (int i = 0; i < _originShiftParticleSystemCount; i++)
            {
                if (ReferenceEquals(_originShiftParticleSystems[i], particleSystem))
                    return false;
            }

            if (_originShiftParticleSystemCount >= _originShiftParticleSystems.Length)
                return false;

            _originShiftParticleSystems[_originShiftParticleSystemCount] = particleSystem;
            _originShiftParticleSystemCount++;
            return true;
        }

        private static void RemoveOriginShiftParticleSystemAt(int index)
        {
            if ((uint)index >= (uint)_originShiftParticleSystemCount)
                return;

            int lastIndex = _originShiftParticleSystemCount - 1;
            _originShiftParticleSystems[index] = _originShiftParticleSystems[lastIndex];
            _originShiftParticleSystems[lastIndex] = null;
            _originShiftParticleSystemCount = lastIndex;
        }

        private static void CompactOriginShiftParticleSystems()
        {
            for (int i = _originShiftParticleSystemCount - 1; i >= 0; i--)
            {
                ParticleSystem particleSystem = _originShiftParticleSystems[i];
                if (particleSystem == null)
                    RemoveOriginShiftParticleSystemAt(i);
            }
        }

        private static void ClearOriginShiftParticleSystems()
        {
            for (int i = 0; i < _originShiftParticleSystemCount; i++)
                _originShiftParticleSystems[i] = null;

            _originShiftParticleSystemCount = 0;
        }

        private struct ListenerSlot
        {
            public IOriginShiftListener Listener;

            public void Clear()
            {
                Listener = null;
            }
        }

        private async Awaitable BroadcastNonSceneOriginShiftListenersAsync(OriginShiftEventData shiftData, CancellationToken cancellationToken)
        {
            for (int i = _originShiftListenerCount - 1; i >= 0; i--)
            {
                cancellationToken.ThrowIfCancellationRequested();
                IOriginShiftListener listener = _originShiftListeners[i].Listener;
                if (listener == null)
                {
                    RemoveOriginShiftListenerAt(i);
                    continue;
                }

                UnityEngine.Object unityListener = listener as UnityEngine.Object;
                if (!ReferenceEquals(unityListener, null) && unityListener == null)
                {
                    RemoveOriginShiftListenerAt(i);
                    continue;
                }

                if (IsSceneResidentOriginShiftListener(listener))
                    continue;

                await DispatchOriginShiftListenerAsync(listener, shiftData, cancellationToken);
            }
        }

        private static bool IsSceneResidentOriginShiftListener(IOriginShiftListener listener)
        {
            Component component = listener as Component;
            if (component == null)
                return false;

            Scene scene = component.gameObject.scene;
            return scene.IsValid() && scene.isLoaded;
        }

        private static int CountLoadedScenes()
        {
            int loadedSceneCount = 0;
            int sceneCount = SceneManager.sceneCount;
            for (int sceneIndex = 0; sceneIndex < sceneCount; sceneIndex++)
            {
                Scene scene = SceneManager.GetSceneAt(sceneIndex);
                if (scene.IsValid() && scene.isLoaded)
                    loadedSceneCount++;
            }

            return loadedSceneCount;
        }

        private static bool HasPendingSceneRebaseBarrier(HectonFloatingOrigin origin)
        {
            if (origin == null)
                return false;

            int totalCount = Volatile.Read(ref origin._lastShiftSceneTotal);
            return totalCount > 0 && Volatile.Read(ref origin._lastShiftSceneReadyCount) < totalCount;
        }

        private void CompleteSceneRebaseBarrier()
        {
            int totalCount = Volatile.Read(ref _lastShiftSceneTotal);
            Interlocked.Exchange(ref _lastShiftSceneReadyCount, totalCount);
        }

        private void SynchronizeLoadedSceneOriginShiftRegistries()
        {
            CompactOriginShiftParticleSystems();
            int sceneCount = SceneManager.sceneCount;
            for (int sceneIndex = 0; sceneIndex < sceneCount; sceneIndex++)
            {
                Scene scene = SceneManager.GetSceneAt(sceneIndex);
                if (!scene.IsValid() || !scene.isLoaded)
                    continue;

                SynchronizeSceneOriginShiftRegistries(scene);
            }

            _sceneComponentScratch.Clear();
            _sceneParticleSystemScratch.Clear();
            _sceneRootObjects.Clear();
        }

        private void SynchronizeSceneOriginShiftRegistries(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded)
                return;

            try
            {
                _sceneRootObjects.Clear();
                scene.GetRootGameObjects(_sceneRootObjects);
                for (int rootIndex = 0; rootIndex < _sceneRootObjects.Count; rootIndex++)
                {
                    GameObject rootObject = _sceneRootObjects[rootIndex];
                    if (rootObject == null)
                        continue;

                    _sceneComponentScratch.Clear();
                    rootObject.GetComponentsInChildren(true, _sceneComponentScratch);
                    for (int componentIndex = 0; componentIndex < _sceneComponentScratch.Count; componentIndex++)
                    {
                        if (_sceneComponentScratch[componentIndex] is IOriginShiftListener listener)
                            RegisterListener(listener);
                    }

                    _sceneParticleSystemScratch.Clear();
                    rootObject.GetComponentsInChildren(true, _sceneParticleSystemScratch);
                    int particleSystemCount = _sceneParticleSystemScratch.Count;
                    for (int particleSystemIndex = 0; particleSystemIndex < particleSystemCount; particleSystemIndex++)
                        TryRegisterOriginShiftParticleSystem(_sceneParticleSystemScratch[particleSystemIndex]);
                }
            }
            finally
            {
                _sceneComponentScratch.Clear();
                _sceneParticleSystemScratch.Clear();
                _sceneRootObjects.Clear();
            }
        }

        private void UpdateCriticalEntityTrackers()
        {
            if (_anchor == null)
                TryResolveAnchor(force: false);

            UpdateCriticalEntityTracker(ref _playerDriftTracker, _anchor);

            ISubmarineRuntimeContext submarine = _submarineRuntime;
            Transform submarineTransform = submarine != null ? submarine.PlatformTransform : null;
            UpdateCriticalEntityTracker(ref _submarineDriftTracker, submarineTransform);
        }

        private static void UpdateCriticalEntityTracker(ref CriticalAupTracker tracker, Transform target)
        {
            tracker.Transform = target;
            if (target == null)
            {
                tracker.Initialized = false;
                tracker.LastRuntimePosition = Vector3.zero;
                return;
            }

            if (!TryGetTransformWorldPosition(target, out Vector3 runtimePosition))
                return;

            if (!tracker.Initialized)
            {
                tracker.AbsolutePosition = new double3(runtimePosition.x, runtimePosition.y, runtimePosition.z) + CurrentTotalOffsetDouble;
                tracker.LastRuntimePosition = runtimePosition;
                tracker.Initialized = true;
                return;
            }

            Vector3 runtimeDelta = runtimePosition - tracker.LastRuntimePosition;
            float3 runtimeDelta3 = new float3(runtimeDelta.x, runtimeDelta.y, runtimeDelta.z);
            if (math.all(math.isfinite(runtimeDelta3)))
                tracker.AbsolutePosition += new double3(runtimeDelta.x, runtimeDelta.y, runtimeDelta.z);

            tracker.LastRuntimePosition = runtimePosition;
        }

        private void ScheduleAupDriftCheck()
        {
            if (!EnsureDriftCheckBuffers(
                    out NativeArray<double3> runtimePositions,
                    out NativeArray<double3> absolutePositions,
                    out NativeArray<byte> invalidMask))
            {
                return;
            }

            int writeIndex = 0;
            writeIndex = StageCriticalEntityForDriftCheck(
                _playerDriftTracker,
                runtimePositions,
                absolutePositions,
                writeIndex);
            writeIndex = StageCriticalEntityForDriftCheck(
                _submarineDriftTracker,
                runtimePositions,
                absolutePositions,
                writeIndex);
            if (writeIndex <= 0)
                return;

            _driftCheckCount = writeIndex;
            _driftCheckHandle = new AupDriftCheckJob
            {
                RuntimePositions = runtimePositions,
                TrackedAbsolutePositions = absolutePositions,
                CommittedTotalOffset = _totalOffsetDouble,
                MaxDeltaSq = DriftCheckThresholdSq,
                InvalidMask = invalidMask
            }.Schedule(writeIndex, 1);
            _driftCheckScheduled = true;
        }

        private static int StageCriticalEntityForDriftCheck(
            in CriticalAupTracker tracker,
            NativeArray<double3> runtimePositions,
            NativeArray<double3> absolutePositions,
            int writeIndex)
        {
            if (!tracker.Initialized || tracker.Transform == null || writeIndex >= DriftCheckEntityCapacity)
                return writeIndex;

            if (!TryGetTransformWorldPosition(tracker.Transform, out Vector3 runtimePosition) || !math.all(math.isfinite(tracker.AbsolutePosition)))
                return writeIndex;

            runtimePositions[writeIndex] = new double3(runtimePosition.x, runtimePosition.y, runtimePosition.z);
            absolutePositions[writeIndex] = tracker.AbsolutePosition;
            return writeIndex + 1;
        }

        private bool ConsumeCompletedDriftCheck()
        {
            if (!DispatcherJobSwap.TryComplete(ref _driftCheckHandle, false))
                return false;

            _driftCheckScheduled = false;
            if (!TryResolveDriftCheckBuffers(
                    out NativeArray<double3> runtimePositions,
                    out NativeArray<double3> absolutePositions,
                    out NativeArray<byte> invalidMask))
            {
                _driftCheckCount = 0;
                return false;
            }

            double maxDriftErrorSq = ResolveMaxDriftErrorSq(runtimePositions, absolutePositions);
            Vector3 telemetryPosition = TryGetTransformWorldPosition(_anchor, out Vector3 anchorTelemetryPosition) ? anchorTelemetryPosition : Vector3.zero;
            CrashTelemetryBuffer.ReportAupMaxDriftError(telemetryPosition, ResolveDriftErrorMeters(maxDriftErrorSq));

            bool hasInvalidEntity = false;
            for (int i = 0; i < _driftCheckCount; i++)
            {
                if (invalidMask[i] != 0)
                {
                    hasInvalidEntity = true;
                    break;
                }
            }

            _driftCheckCount = 0;
            if (!hasInvalidEntity || _anchor == null)
                return false;

            Vector3 forcedShiftOffset = ResolveForcedDriftShiftOffset(invalidMask);
            if (VectorLengthSq(forcedShiftOffset) <= 0.0001f)
            {
                if (!TryGetTransformWorldPosition(_anchor, out forcedShiftOffset))
                    return false;
            }

            if (!IsFiniteVector(forcedShiftOffset))
            {
                CrashTelemetryBuffer.ReportNanPhysicsRecovery(forcedShiftOffset, Vector3.zero);
                return false;
            }

            CrashTelemetryBuffer.ReportAupJitterCorrection(forcedShiftOffset, DriftCheckThresholdMeters);
            _shiftDeadzoneArmed = false;
            BeginShiftWorld(forcedShiftOffset);
            return true;
        }

        private double ResolveMaxDriftErrorSq(
            NativeArray<double3> runtimePositions,
            NativeArray<double3> absolutePositions)
        {
            double maxDriftErrorSq = 0d;
            for (int i = 0; i < _driftCheckCount; i++)
            {
                double driftErrorSq = ResolveDriftErrorSq(runtimePositions, absolutePositions, i);
                if (!math.isfinite(driftErrorSq))
                    return double.PositiveInfinity;

                maxDriftErrorSq = math.max(maxDriftErrorSq, driftErrorSq);
            }

            return maxDriftErrorSq;
        }

        private double ResolveDriftErrorSq(
            NativeArray<double3> runtimePositions,
            NativeArray<double3> absolutePositions,
            int index)
        {
            double3 expectedRuntime = absolutePositions[index] - _totalOffsetDouble;
            double3 delta = expectedRuntime - runtimePositions[index];
            double driftErrorSq = math.lengthsq(delta);
            return math.all(math.isfinite(delta)) && math.isfinite(driftErrorSq) ? driftErrorSq : double.PositiveInfinity;
        }

        private static float ResolveDriftErrorMeters(double driftErrorSq)
        {
            if (driftErrorSq <= 0d)
                return 0f;

            if (!math.isfinite(driftErrorSq))
                return float.MaxValue;

            double driftErrorMeters = driftErrorSq * math.rsqrt(math.max(driftErrorSq, 0.000001d));
            if (!math.isfinite(driftErrorMeters))
                return float.MaxValue;

            return driftErrorMeters >= float.MaxValue ? float.MaxValue : (float)driftErrorMeters;
        }

        private Vector3 ResolveForcedDriftShiftOffset(NativeArray<byte> invalidMask)
        {
            if (TryResolveForcedDriftShiftOffset(in _playerDriftTracker, invalidMask, 0, out Vector3 shiftOffset))
                return shiftOffset;

            if (TryResolveForcedDriftShiftOffset(in _submarineDriftTracker, invalidMask, 1, out shiftOffset))
                return shiftOffset;

            return Vector3.zero;
        }

        private bool TryResolveForcedDriftShiftOffset(
            in CriticalAupTracker tracker,
            NativeArray<byte> invalidMask,
            int maskIndex,
            out Vector3 shiftOffset)
        {
            shiftOffset = Vector3.zero;
            if (maskIndex < 0 ||
                maskIndex >= DriftCheckEntityCapacity ||
                !invalidMask.IsCreated ||
                maskIndex >= invalidMask.Length ||
                invalidMask[maskIndex] == 0 ||
                !tracker.Initialized ||
                tracker.Transform == null)
            {
                return false;
            }

            double3 expectedRuntime = tracker.AbsolutePosition - _totalOffsetDouble;
            if (!math.all(math.isfinite(expectedRuntime)) || math.lengthsq(expectedRuntime) <= 0.0001d)
                return false;

            float3 expectedRuntime3 = new float3((float)expectedRuntime.x, (float)expectedRuntime.y, (float)expectedRuntime.z);
            if (!math.all(math.isfinite(expectedRuntime3)))
                return false;

            shiftOffset = new Vector3(expectedRuntime3.x, expectedRuntime3.y, expectedRuntime3.z);
            return true;
        }

        private void ResyncCriticalEntityTrackersAfterShift()
        {
            ResyncCriticalEntityTrackerAfterShift(ref _playerDriftTracker);
            ResyncCriticalEntityTrackerAfterShift(ref _submarineDriftTracker);
        }

        private static void ResyncCriticalEntityTrackerAfterShift(ref CriticalAupTracker tracker)
        {
            if (!tracker.Initialized || tracker.Transform == null)
                return;

            if (TryGetTransformWorldPosition(tracker.Transform, out Vector3 runtimePosition))
                tracker.LastRuntimePosition = runtimePosition;
        }

        private void EnsureDriftCheckBuffers()
        {
            _ = EnsureDriftCheckBuffers(out _, out _, out _);
        }

        private bool EnsureDriftCheckBuffers(
            out NativeArray<double3> runtimePositions,
            out NativeArray<double3> absolutePositions,
            out NativeArray<byte> invalidMask)
        {
            runtimePositions = default;
            absolutePositions = default;
            invalidMask = default;
            IDataVault vault = _dataVault;
            if (vault == null)
                return false;

            return OpenOrAcquireDriftCheckBuffer(
                       vault,
                       ref _driftCheckRuntimePositionsHandle,
                       BufferID.FloatingOriginDriftRuntimePositions,
                       out runtimePositions) &&
                   OpenOrAcquireDriftCheckBuffer(
                       vault,
                       ref _driftCheckAbsolutePositionsHandle,
                       BufferID.FloatingOriginDriftAbsolutePositions,
                       out absolutePositions) &&
                   OpenOrAcquireDriftCheckBuffer(
                       vault,
                       ref _driftCheckInvalidMaskHandle,
                       BufferID.FloatingOriginDriftInvalidMask,
                       out invalidMask);
        }

        private bool TryResolveDriftCheckBuffers(
            out NativeArray<double3> runtimePositions,
            out NativeArray<double3> absolutePositions,
            out NativeArray<byte> invalidMask)
        {
            runtimePositions = default;
            absolutePositions = default;
            invalidMask = default;
            IDataVault vault = _dataVault;
            return TryOpenDriftCheckBuffer(
                       vault,
                       ref _driftCheckRuntimePositionsHandle,
                       BufferID.FloatingOriginDriftRuntimePositions,
                       out runtimePositions) &&
                   TryOpenDriftCheckBuffer(
                       vault,
                       ref _driftCheckAbsolutePositionsHandle,
                       BufferID.FloatingOriginDriftAbsolutePositions,
                       out absolutePositions) &&
                   TryOpenDriftCheckBuffer(
                       vault,
                       ref _driftCheckInvalidMaskHandle,
                       BufferID.FloatingOriginDriftInvalidMask,
                       out invalidMask);
        }

        private static bool OpenOrAcquireDriftCheckBuffer<T>(
            IDataVault vault,
            ref VaultGenerationHandle<T> handle,
            BufferID bufferId,
            out NativeArray<T> buffer) where T : struct
        {
            if (TryOpenDriftCheckBuffer(vault, ref handle, bufferId, out buffer))
                return true;

            if (vault == null)
            {
                buffer = default;
                return false;
            }

            if (vault.IsAllocationLocked)
            {
                if (!vault.TryGetGenerationHandle(bufferId, out handle))
                {
                    buffer = default;
                    return false;
                }

                return TryOpenDriftCheckBuffer(vault, ref handle, bufferId, out buffer);
            }

            handle = vault.EnsureGenerationHandle<T>(
                bufferId,
                DriftCheckEntityCapacity,
                DriftCheckOwnerSystemId,
                NativeArrayOptions.ClearMemory);
            return TryOpenDriftCheckBuffer(vault, ref handle, bufferId, out buffer);
        }

        private static bool TryOpenDriftCheckBuffer<T>(
            IDataVault vault,
            ref VaultGenerationHandle<T> handle,
            BufferID bufferId,
            out NativeArray<T> buffer) where T : struct
        {
            buffer = default;
            if (vault == null ||
                !IsDriftCheckHandle(in handle, bufferId) ||
                !vault.TryResolveHandle(in handle, out buffer) ||
                !buffer.IsCreated ||
                buffer.Length < DriftCheckEntityCapacity)
            {
                buffer = default;
                return false;
            }

            return true;
        }

        private static bool IsDriftCheckHandle<T>(
            in VaultGenerationHandle<T> handle,
            BufferID bufferId) where T : struct
        {
            return handle.BufferID == (uint)bufferId &&
                   handle.SystemID == (uint)DriftCheckOwnerSystemId &&
                   handle.Generation != 0u;
        }

        private void DisposeDriftCheckState()
        {
            if (_driftCheckScheduled)
                DispatcherJobSwap.TryComplete(ref _driftCheckHandle, true);

            _driftCheckRuntimePositionsHandle = default;
            _driftCheckAbsolutePositionsHandle = default;
            _driftCheckInvalidMaskHandle = default;
            _driftCheckHandle = default;
            _driftCheckScheduled = false;
            _driftCheckCount = 0;
        }

        private void RebuildShiftTargetCache()
        {
            _shiftTargetTransforms.Clear();

            int sceneCount = SceneManager.sceneCount;
            for (int i = 0; i < sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (!scene.isLoaded)
                    continue;

                _sceneRootObjects.Clear();
                scene.GetRootGameObjects(_sceneRootObjects);
                for (int j = 0; j < _sceneRootObjects.Count; j++)
                {
                    GameObject rootObject = _sceneRootObjects[j];
                    if (rootObject == null)
                        continue;

                    _shiftTargetTransforms.Add(rootObject.transform);
                }
            }

            int transformCount = _shiftTargetTransforms.Count;
            if (_shiftTargetArray.Length != transformCount)
            {
                _shiftTargetArray = transformCount == 0
                    ? Array.Empty<Transform>()
                    : new Transform[transformCount]; // COLD ALLOC: Transform[transformCount] — cached root transform snapshot for atomic origin shifts — owner: HectonFloatingOrigin
            }

            for (int i = 0; i < transformCount; i++)
                _shiftTargetArray[i] = _shiftTargetTransforms[i];

            _shiftTargetsDirty = false;
        }

        private void ApplyOriginShiftToCachedRootTransforms(Vector3 shiftOffset)
        {
            if (!IsFiniteVector(shiftOffset))
                return;

            for (int i = 0; i < _shiftTargetArray.Length; i++)
            {
                Transform rootTransform = _shiftTargetArray[i];
                if (rootTransform == null)
                    continue;

                Vector3 localPositionVector = rootTransform.localPosition;
                if (!IsFiniteVector(localPositionVector))
                    continue;

                rootTransform.localPosition = localPositionVector - shiftOffset;
            }
        }

        private void TryResolveAnchor(bool force)
        {
            if (_anchor != null)
                return;

            if (!force && _anchorResolveTimer > 0f)
                return;

            _anchorResolveTimer = AnchorResolveCooldown;

            IPlayerRuntimeContext playerRuntimeContext = _playerRuntimeContext ?? PlayerRuntimeContextService.ActiveRuntimeContext;
            if (playerRuntimeContext != null && playerRuntimeContext.PlayerTransform != null)
            {
                _playerRuntimeContext = playerRuntimeContext;
                _anchor = playerRuntimeContext.PlayerTransform;
                _anchorRigidbody = playerRuntimeContext.PlayerRigidbody;
                _hasPreviousAnchorPosition = TryGetTransformWorldPosition(_anchor, out _previousAnchorPosition);
                return;
            }

            _anchorRigidbody = null;
        }

        private void RefreshThresholdCache()
        {
            if (_threshold < MinimumShiftThresholdMeters)
                _threshold = MinimumShiftThresholdMeters;

            _thresholdSqr = _threshold * _threshold;
        }

        private bool IsAnchorMovingAwayFromCenter(Vector3 anchorPosition, float deltaTime)
        {
            Vector3 anchorVelocity = Vector3.zero;
            if (_anchorRigidbody != null)
            {
                anchorVelocity = _anchorRigidbody.linearVelocity;
            }
            else if (_hasPreviousAnchorPosition)
            {
                float safeDeltaTime = math.max(deltaTime, 0.0001f);
                anchorVelocity = (anchorPosition - _previousAnchorPosition) * math.rcp(safeDeltaTime);
            }

            _previousAnchorPosition = anchorPosition;
            _hasPreviousAnchorPosition = true;
            if (VectorLengthSq(anchorPosition) <= 0.0001f)
                return false;

            float3 anchorPosition3 = new float3(anchorPosition.x, anchorPosition.y, anchorPosition.z);
            float3 anchorVelocity3 = new float3(anchorVelocity.x, anchorVelocity.y, anchorVelocity.z);
            float anchorPositionSq = math.lengthsq(anchorPosition3);
            if (!math.isfinite(anchorPositionSq) || anchorPositionSq <= 0.0001f)
                return false;

            float3 radialDirection = anchorPosition3 * math.rsqrt(math.max(anchorPositionSq, 0.0001f));
            float radialVelocity = math.dot(radialDirection, anchorVelocity3);
            return radialVelocity > OutwardMotionSpeedEpsilon;
        }

        private void PublishGlobalOffsets()
        {
            Vector4 offset = new Vector4(TotalOffset.x, TotalOffset.y, TotalOffset.z, 0f);
            HectonShaderGlobalDataVaultBridge.PublishAupShaderGlobals(offset, ResolveAupShiftOffsetForCurrentFrame(), ResolveAupJitterMask());
            HectonXRRuntimeState.PublishOriginShiftState(_shiftSequence, ResolveFixedInterpolationAlpha());
        }

        internal static void PublishCurrentGlobalOffsetsForRenderLoop()
        {
            HectonFloatingOrigin origin = s_activeRuntime;
            if (origin != null)
            {
                origin.PublishGlobalOffsets();
                return;
            }

            HectonShaderGlobalDataVaultBridge.ResetAupShaderGlobals();
            HectonXRRuntimeState.PublishOriginShiftState(0u, 0f);
        }

        private static Vector4 ResolveAupShiftOffsetForCurrentFrame()
        {
            if (_lastShiftEvent.Sequence == 0u || _lastShiftEvent.Frame != SystemDispatcher.CurrentFrameIndex)
                return Vector4.zero;

            Vector3 runtimeOffset = -_lastShiftEvent.ShiftOffset;
            return new Vector4(runtimeOffset.x, runtimeOffset.y, runtimeOffset.z, 0f);
        }

        private void ArmAupJitterMask(int frame)
        {
            _aupJitterMaskReleaseFrame = frame + 1;
            QueueAupJitterMaskUpload(1f);
        }

        private void UpdateAupJitterMaskRelease()
        {
            if (_aupJitterMaskReleaseFrame < 0 || SystemDispatcher.CurrentFrameIndex <= _aupJitterMaskReleaseFrame)
                return;

            _aupJitterMaskReleaseFrame = -1;
            QueueAupJitterMaskUpload(0f);
        }

        private void QueueAupJitterMaskUpload(float value)
        {
            _pendingAupJitterMaskValue = math.saturate(value);
            _pendingAupJitterMaskUpload = true;
        }

        private float ResolveAupJitterMask()
        {
            return _aupJitterMaskReleaseFrame >= 0 && SystemDispatcher.CurrentFrameIndex <= _aupJitterMaskReleaseFrame ? 1f : 0f;
        }

        private static Vector3 ToVector3(double3 value)
        {
            return new Vector3((float)value.x, (float)value.y, (float)value.z);
        }

        private void TryRegister()
        {
            if (_isRegistered && _lateFrameRegistered)
                return;
            if (!Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            if (!_isRegistered)
            {
                _isRegistered = GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.Core);
            }

            if (!_lateFrameRegistered)
                _lateFrameRegistered = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Core);
        }

        private void TryUnregister()
        {
            if (!_isRegistered && !_lateFrameRegistered)
                return;

            if (_isRegistered)
                GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Core);
            if (_lateFrameRegistered)
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Core);

            _isRegistered = false;
            _lateFrameRegistered = false;
            _pendingSceneSynchronizationVisualSync = false;
            _pendingParticleSystemRebase = false;
            _pendingAupJitterMaskUpload = false;
        }

        private void TryRegisterHotSwapListener()
        {
            if (_hotSwapListenerRegistered)
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

        private void SubscribeSceneEvents()
        {
            if (_sceneEventsSubscribed)
                return;

            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;
            SceneManager.sceneUnloaded -= HandleSceneUnloaded;
            SceneManager.sceneUnloaded += HandleSceneUnloaded;
            SceneManager.activeSceneChanged -= HandleActiveSceneChanged;
            SceneManager.activeSceneChanged += HandleActiveSceneChanged;
            _sceneEventsSubscribed = true;
        }

        private void UnsubscribeSceneEvents()
        {
            if (!_sceneEventsSubscribed)
                return;

            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneUnloaded -= HandleSceneUnloaded;
            SceneManager.activeSceneChanged -= HandleActiveSceneChanged;
            _sceneEventsSubscribed = false;
        }

        private void AcquireSceneRebaseTickLock()
        {
            if (_sceneRebaseTickLockHeld)
                return;

            SystemDispatcher.RequestOriginShiftBootstrapLock();
            _sceneRebaseTickLockHeld = true;
        }

        private void ReleaseSceneRebaseTickLock()
        {
            if (!_sceneRebaseTickLockHeld)
                return;

            _sceneRebaseTickLockHeld = false;
            SystemDispatcher.ReleaseOriginShiftBootstrapLock();
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            _shiftTargetsDirty = true;
            SynchronizeSceneOriginShiftRegistries(scene);
            QueuePendingLoadedScene(scene);
        }

        private void HandleSceneUnloaded(Scene scene)
        {
            _shiftTargetsDirty = true;
            RemovePendingLoadedScene(scene);
            CompactOriginShiftParticleSystems();
            TryPrepareShiftTargets();
        }

        private void HandleActiveSceneChanged(Scene previousScene, Scene newScene)
        {
            _shiftTargetsDirty = true;
            SynchronizeSceneOriginShiftRegistries(newScene);
            TryPrepareShiftTargets();
        }

        private void TryPrepareShiftTargets()
        {
            // Physics pause no longer blocks target-cache rebuild. Holding dirty targets
            // under pause kept SceneRebaseTickLock alive (see ProcessPending finally) and
            // starved Frost/Slow via IsOriginShiftBootstrapLocked. Rebuild is read-only
            // cache work against already-committed world state.
            if (!Application.isPlaying ||
                _isShiftInProgress ||
                _pendingLoadedScenes.Count > 0 ||
                !_shiftTargetsDirty)
            {
                return;
            }

            RebuildShiftTargetCache();
            if (_pendingLoadedScenes.Count == 0 && !_shiftTargetsDirty)
                ReleaseSceneRebaseTickLock();
        }

        private void ProcessPendingSceneSynchronization()
        {
            // Block only while a transform shift job is mutating the world.
            // Physics pause previously early-returned here, which left SceneRebaseTickLock
            // held (acquired in QueuePendingLoadedScene) and starved SystemDispatcher
            // master simulation - including FO.Tick that would clear the pause.
            // Applying the already-committed offset to newly loaded scenes is safe under
            // physics pause: it does not start a new shift.
            if (_isShiftInProgress)
                return;

            try
            {
                if (_pendingLoadedScenes.Count > 0)
                {
                    Vector3 committedTotalOffset = TotalOffset;
                    for (int i = 0; i < _pendingLoadedScenes.Count; i++)
                    {
                        Scene loadedScene = _pendingLoadedScenes[i];
                        if (!loadedScene.IsValid() || !loadedScene.isLoaded)
                            continue;

                        ApplyCommittedOffsetToLoadedScene(loadedScene, committedTotalOffset);
                    }

                    _pendingLoadedScenes.Clear();
                }

                TryPrepareShiftTargets();
            }
            finally
            {
                if (_pendingLoadedScenes.Count == 0 && !_shiftTargetsDirty)
                    ReleaseSceneRebaseTickLock();
            }
        }

        private void QueuePendingLoadedScene(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded)
                return;

            for (int i = 0; i < _pendingLoadedScenes.Count; i++)
            {
                if (_pendingLoadedScenes[i].handle == scene.handle)
                    return;
            }

            _pendingLoadedScenes.Add(scene);
            AcquireSceneRebaseTickLock();
        }

        private void RemovePendingLoadedScene(Scene scene)
        {
            if (!scene.IsValid())
                return;

            for (int i = _pendingLoadedScenes.Count - 1; i >= 0; i--)
            {
                if (_pendingLoadedScenes[i].handle != scene.handle)
                    continue;

                _pendingLoadedScenes.RemoveAt(i);
            }

            if (_pendingLoadedScenes.Count == 0 && !_shiftTargetsDirty)
                ReleaseSceneRebaseTickLock();
        }

        private void ApplyCommittedOffsetToLoadedScene(Scene scene, Vector3 committedTotalOffset)
        {
            if (!IsFiniteVector(committedTotalOffset) || VectorLengthSq(committedTotalOffset) <= 0.0001f)
                return;

            _sceneRootObjects.Clear();
            scene.GetRootGameObjects(_sceneRootObjects);
            for (int i = 0; i < _sceneRootObjects.Count; i++)
            {
                GameObject rootObject = _sceneRootObjects[i];
                if (rootObject == null)
                    continue;

                Transform rootTransform = rootObject.transform;
                Vector3 rootPosition = rootTransform.localPosition;
                if (!IsFiniteVector(rootPosition))
                {
                    CrashTelemetryBuffer.ReportNanPhysicsRecovery(rootPosition, Vector3.zero);
                    continue;
                }

                rootTransform.localPosition = rootPosition - committedTotalOffset;
            }
        }

        private static bool IsFiniteVector(Vector3 value)
        {
            float3 value3 = new float3(value.x, value.y, value.z);
            return math.all(math.isfinite(value3));
        }

        private static bool TryGetTransformWorldPosition(Transform source, out Vector3 position)
        {
            position = Vector3.zero;
            if (source == null)
                return false;

            source.GetPositionAndRotation(out position, out _);
            return IsFiniteVector(position);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float VectorLengthSq(Vector3 value)
        {
            float3 value3 = new float3(value.x, value.y, value.z);
            return math.lengthsq(value3);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            RefreshThresholdCache();
        }
#endif
    }
}
