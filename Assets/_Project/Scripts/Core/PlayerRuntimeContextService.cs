using Hecton8.Audio;
using Hecton8.Bootstrap;
using Hecton8.Building;
using Hecton8.Environment;
using Hecton8.Gameplay;
using Hecton8.Inventory;
using Hecton8.UI;
using Hecton8.World;
using NASAPunk.Visor;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Core
{
    /// <summary>
    /// Bootstrap-owned player runtime context published through <see cref="GlobalRegistry"/>.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-9930)]
    public sealed class PlayerRuntimeContextService : MonoBehaviour, IPlayerRuntimeContext, IPlayerSurvivalEnvironmentReadModel, IUpdatable, ISlowTickable, IServiceHeartbeat, IServiceShutdown, IGlobalRegistryHotSwapListener
    {
        private const uint KccVelocityRuntimeContextMaxAgeFrames = 12u;

        private bool _isInitialized;
        private bool _registeredUpdatable;
        private bool _registeredSlowTickable;
        private bool _registeredContext;
        private bool _registeredHotSwap;
        private bool _runtimeOwnerAborted;
        private bool _syncInProgress;
        private bool _coldContextSyncRequested;

        /// <summary>
        /// Set only when the published player root actually changes identity, so a new root takes the cold
        /// sync once and gets its component install. Kept separate from <see cref="_coldContextSyncRequested"/>
        /// because that flag is also raised by permanent steady states such as a missing HUD notification.
        /// </summary>
        private bool _coldInstallRootDirty;
        private bool _dynamicContextReferencesEnabled;
        private IPlayerInventoryService _playerInventoryService;
        private IPlayerSensoryService _playerSensoryService;
        private GameObject _playerRootOverride;
        private GameObject _playerObject;
        private Transform _playerTransform;
        private HectonPlayerMovement _playerMovement;
        private IBuoyancyAirStateReadModel _playerBuoyancyAirState;
        private Rigidbody _playerRigidbody;
        private HectonSurvivalSystem _survivalSystem;
        private HectonPlayerHealth _playerHealth;
        private PlayerToolManager _toolManager;
        private PlayerInventory _inventory;
        private PlayerTransportCoordinator _playerTransportCoordinator;
        private TraumaDispatcher _traumaDispatcher;
        private Camera _playerCamera;
        private Transform _playerCameraTransform;
        private PlayerPDA _playerPda;
        private PlayerBuilder _playerBuilder;
        private VisorHUDController _visorController;
        private PlayerFlashlight _flashlight;
        private PlayerThrusterAudio _thrusterAudio;
        private HectonUnderwaterVisuals _underwaterVisuals;
        private Transform _handAnchor;
        private Collider _playerCollider;
        private HUDNotification _hudNotification;
        private readonly PlayerRuntimeContext _runtimeContext = new PlayerRuntimeContext();
        private static PlayerRuntimeContextService s_activeRuntimeInstance;

        public static IPlayerRuntimeContext ActiveRuntimeContext => s_activeRuntimeInstance;

        /// <inheritdoc />
        public bool IsInitialized => _isInitialized;

        /// <inheritdoc />
        public ServiceHeartbeatState HeartbeatState => _isInitialized ? ServiceHeartbeatState.Ready : ServiceHeartbeatState.NotStarted;

        /// <inheritdoc />
        public bool IsServiceReady => _isInitialized;

        /// <inheritdoc />
        public GameObject PlayerObject
        {
            get
            {
                return _playerObject;
            }
        }

        /// <inheritdoc />
        public Transform PlayerTransform
        {
            get
            {
                return _playerTransform;
            }
        }

        /// <inheritdoc />
        public HectonPlayerMovement PlayerMovement
        {
            get
            {
                return _playerMovement;
            }
        }

        /// <inheritdoc />
        public IBuoyancyAirStateReadModel PlayerBuoyancyAirState
        {
            get
            {
                return _playerBuoyancyAirState;
            }
        }

        /// <inheritdoc />
        public IPlayerCuttingTensionService CuttingTensionService
        {
            get
            {
                return _playerMovement as IPlayerCuttingTensionService;
            }
        }

        /// <inheritdoc />
        public Rigidbody PlayerRigidbody
        {
            get
            {
                return _playerRigidbody;
            }
        }

        /// <inheritdoc />
        public HectonSurvivalSystem SurvivalSystem
        {
            get
            {
                return _survivalSystem;
            }
        }

        /// <inheritdoc />
        public HectonPlayerHealth PlayerHealth
        {
            get
            {
                return _playerHealth;
            }
        }

        /// <inheritdoc />
        public TraumaDispatcher TraumaDispatcher
        {
            get
            {
                return _traumaDispatcher;
            }
        }

        /// <inheritdoc />
        public PlayerToolManager ToolManager
        {
            get
            {
                return _toolManager;
            }
        }

        /// <inheritdoc />
        public PlayerInventory Inventory
        {
            get
            {
                return _inventory;
            }
        }

        /// <inheritdoc />
        public PlayerTransportCoordinator PlayerTransportCoordinator
        {
            get
            {
                return _playerTransportCoordinator;
            }
        }

        /// <inheritdoc />
        public IPlayerTransportLifecycleResolver PlayerTransportLifecycleResolver
        {
            get
            {
                return _playerTransportCoordinator;
            }
        }

        /// <inheritdoc />
        public Camera PlayerCamera
        {
            get
            {
                return _playerCamera;
            }
        }

        /// <inheritdoc />
        public PlayerPDA PlayerPDA
        {
            get
            {
                return _playerPda;
            }
        }

        /// <inheritdoc />
        public PlayerBuilder PlayerBuilder
        {
            get
            {
                return _playerBuilder;
            }
        }

        /// <inheritdoc />
        public VisorHUDController VisorController
        {
            get
            {
                return _visorController;
            }
        }

        /// <inheritdoc />
        public PlayerFlashlight Flashlight
        {
            get
            {
                return _flashlight;
            }
        }

        /// <inheritdoc />
        public PlayerThrusterAudio ThrusterAudio
        {
            get
            {
                return _thrusterAudio;
            }
        }

        /// <inheritdoc />
        public HectonUnderwaterVisuals UnderwaterVisuals
        {
            get
            {
                return _underwaterVisuals;
            }
        }

        /// <inheritdoc />
        public Transform HandAnchor
        {
            get
            {
                return _handAnchor;
            }
        }

        /// <inheritdoc />
        public Collider PlayerCollider
        {
            get
            {
                return _playerCollider;
            }
        }

        /// <inheritdoc />
        public HUDNotification HudNotification
        {
            get
            {
                return _hudNotification;
            }
        }

        /// <inheritdoc />
        public bool TryGetPlayerPoseSnapshot(out PlayerRuntimePoseSnapshot snapshot)
        {
            snapshot = default;
            if (!_runtimeContext.IsBound)
                return false;

            PlayerMovementRuntimeState movementState = _runtimeContext.MovementState;
            if ((movementState.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) == 0u)
                return false;

            if (!math.all(math.isfinite(movementState.WorldPosition)) ||
                !IsFinitePredictedAup(in movementState))
            {
                return false;
            }

            float3 runtimePosition = movementState.WorldPosition;
            AbsoluteUniversePosition aup = movementState.PredictedAup;
            float3 fallbackForward = SafeDirection(movementState.Forward, new float3(0f, 0f, 1f));
            float3 forward = SafeDirection(movementState.CameraForward, fallbackForward);
            snapshot = new PlayerRuntimePoseSnapshot(runtimePosition, forward, aup, movementState.Flags);
            return true;
        }

        /// <inheritdoc />
        public bool TryGetMovementRuntimeState(out PlayerMovementRuntimeState state)
        {
            state = _runtimeContext.MovementState;
            return _isInitialized &&
                   _runtimeContext.IsBound &&
                   (state.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) != 0u &&
                   math.isfinite(state.DepthMeters) &&
                   IsFinitePredictedAup(in state) &&
                   math.all(math.isfinite(state.WorldPosition)) &&
                   math.all(math.isfinite(state.PredictedWorldPosition)) &&
                   math.all(math.isfinite(state.Velocity)) &&
                   math.all(math.isfinite(state.Forward));
        }

        /// <inheritdoc />
        public bool TryGetLookRuntimeState(out PlayerLookState state)
        {
            state = _runtimeContext.LookState;
            return _isInitialized &&
                   _runtimeContext.IsBound &&
                   (state.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) != 0u;
        }

        /// <inheritdoc />
        public bool TryGetMovementStressRuntimeState(out PlayerMovementStressRuntimeState state)
        {
            state = _runtimeContext.MovementStressState;
            return _isInitialized &&
                   _runtimeContext.IsBound &&
                   (state.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) != 0u;
        }

        /// <inheritdoc />
        public bool TryGetEffortLoadRuntimeState(out PlayerEffortLoadRuntimeState state)
        {
            state = _runtimeContext.EffortLoadState;
            return _isInitialized &&
                   _runtimeContext.IsBound &&
                   (state.Flags & (uint)PlayerEffortLoadRuntimeFlags.HasPlayerRoot) != 0u &&
                   (state.Flags & ((uint)PlayerEffortLoadRuntimeFlags.HasInventory | (uint)PlayerEffortLoadRuntimeFlags.HasMovement)) != 0u;
        }

        /// <inheritdoc />
        public bool TryGetSurvivalRuntimeState(out PlayerSurvivalRuntimeState state)
        {
            state = _runtimeContext.SurvivalState;
            return _isInitialized &&
                   _runtimeContext.IsBound &&
                   (state.Flags & (uint)PlayerRuntimeSnapshotFlags.HasSurvival) != 0u;
        }

        public bool TryGetSurvivalEnvironmentSnapshot(out PlayerSurvivalEnvironmentSnapshot snapshot)
        {
            IPlayerSurvivalEnvironmentReadModel survivalReadModel = _survivalSystem as IPlayerSurvivalEnvironmentReadModel;
            if (survivalReadModel != null)
                return survivalReadModel.TryGetSurvivalEnvironmentSnapshot(out snapshot);

            snapshot = default;
            return false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            ShutdownActiveRuntimeForEditorReload();
            s_activeRuntimeInstance = null;
            GlobalRegistry.ClearPlayerRuntimeContextRuntime(null);
            HectonXRRuntimeState.BindPlayerContextFallbackCold(null);
        }

#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
        private static void RegisterEditorReloadHooks()
        {
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload -= ShutdownActiveRuntimeForEditorReload;
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload += ShutdownActiveRuntimeForEditorReload;
            UnityEditor.EditorApplication.quitting -= ShutdownActiveRuntimeForEditorReload;
            UnityEditor.EditorApplication.quitting += ShutdownActiveRuntimeForEditorReload;
            UnityEditor.EditorApplication.playModeStateChanged -= HandleEditorPlayModeStateChanged;
            UnityEditor.EditorApplication.playModeStateChanged += HandleEditorPlayModeStateChanged;
        }

        private static void HandleEditorPlayModeStateChanged(UnityEditor.PlayModeStateChange state)
        {
            if (state == UnityEditor.PlayModeStateChange.ExitingEditMode ||
                state == UnityEditor.PlayModeStateChange.ExitingPlayMode ||
                state == UnityEditor.PlayModeStateChange.EnteredEditMode)
            {
                ShutdownActiveRuntimeForEditorReload();
            }
        }
#endif

        private static void ShutdownActiveRuntimeForEditorReload()
        {
            PlayerRuntimeContextService runtime =
                s_activeRuntimeInstance ??
                GlobalRegistry.PlayerRuntimeContextRuntime;
            if (runtime != null)
                runtime.ShutdownServiceState();
        }

        /// <summary>
        /// Ensures a live runtime instance exists.
        /// </summary>
        /// <returns>Live player context instance.</returns>
        public static PlayerRuntimeContextService EnsureRuntimeInstance()
        {
            PlayerRuntimeContextService runtime = ResolveUsableRuntime();
            if (runtime != null)
                return runtime;

            IPlayerRuntimeContext registeredContext = GlobalRegistry.RegisteredPlayer;
            if (IsPlayerContextUsable(registeredContext) &&
                ReferenceEquals(registeredContext as PlayerRuntimeContextService, null))
            {
                return null;
            }

            // Player-build construction path: no authored/bootstrap instance reachable.
            // Player context owns bind/publish of the live player root into GlobalRegistry;
            // without create, TryBindPlayerRoot and sensory/camera consumers miss the host.
            GameObject runtimeRoot = new GameObject("[PlayerRuntimeContextService]"); // COLD ALLOC: GameObject[1] - bootstrap-owned player runtime context root - owner: PlayerRuntimeContextService
            return runtimeRoot.AddComponent<PlayerRuntimeContextService>();
        }


        /// <summary>
        /// Binds a player root to the central runtime context even before bootstrap publishes it globally.
        /// </summary>
        public static bool TryBindPlayerRoot(GameObject playerRoot, out PlayerRuntimeContext runtimeContext)
        {
            runtimeContext = null;
            if (playerRoot == null)
                return false;

            PlayerRuntimeContextService runtimeService = EnsureRuntimeInstance();
            if (runtimeService == null)
                return false;

            runtimeService.BindPlayerRoot(playerRoot);
            runtimeContext = runtimeService._runtimeContext;
            return runtimeContext != null && runtimeContext.IsBound;
        }

        /// <summary>
        /// Resolves the currently active player runtime context when one is available.
        /// </summary>
        public static bool TryGetActiveRuntimeContext(out PlayerRuntimeContext runtimeContext)
        {
            runtimeContext = null;
            PlayerRuntimeContextService runtime = s_activeRuntimeInstance;
            if (runtime == null)
                return false;

            runtimeContext = runtime._runtimeContext;
            return runtimeContext != null && runtimeContext.IsBound;
        }

        /// <summary>
        /// Explicitly initializes the service and registers it into <see cref="GlobalRegistry"/>.
        /// </summary>
        public void InitializeService()
        {
            InitializeServiceInternal(false);
        }

        internal void InitializeServiceDeferredSync()
        {
            InitializeServiceInternal(false);
        }

        internal void RefreshRuntimeContext()
        {
            if (_runtimeOwnerAborted || !_isInitialized)
                return;

            _dynamicContextReferencesEnabled = true;
            RefreshDynamicServiceReferencesCold();
            SyncPlayerContext();
        }

        private void InitializeServiceInternal(bool syncImmediately)
        {
            if (_runtimeOwnerAborted || !EnsureSingletonOwnership())
                return;

            if (_isInitialized)
            {
                if (!TryRegisterContext())
                    return;

                TryRegisterHotSwapListener();
                TryRegisterUpdatable();
                TryRegisterSlowTickable();
                if (syncImmediately)
                    SyncPlayerContext();
                return;
            }

            _isInitialized = true;
            if (!TryRegisterContext())
                return;

            TryRegisterHotSwapListener();
            TryRegisterUpdatable();
            TryRegisterSlowTickable();
            if (syncImmediately)
                SyncPlayerContext();
        }

        /// <inheritdoc />
        public void Tick(float deltaTime)
        {
            SyncPlayerContextHot();
        }

        public void SlowTick()
        {
            if (!_isInitialized)
                return;

            // A newly published player root takes the cold path exactly once, so it gets its component
            // install and its eleven service handles. Everything else keeps the cheap path - see the
            // rationale on _coldInstallRootDirty in SyncPlayerContextHot.
            if (_coldInstallRootDirty)
            {
                _coldInstallRootDirty = false;
                _coldContextSyncRequested = false;
                SyncPlayerContext();
                return;
            }

            if (_coldContextSyncRequested)
            {
                _coldContextSyncRequested = false;
                SyncPlayerContextWithoutColdLookups();
            }
        }

        private void Awake()
        {
            EnsureSingletonOwnership();
        }

        private void OnEnable()
        {
            if (_runtimeOwnerAborted)
                return;

            if (_isInitialized)
            {
                if (!EnsureSingletonOwnership())
                    return;

                if (!TryRegisterContext())
                    return;

                TryRegisterHotSwapListener();
                TryRegisterUpdatable();
                TryRegisterSlowTickable();
                SyncPlayerContext();
            }
        }

        private void OnDisable()
        {
            if (_runtimeOwnerAborted)
                return;

            TryUnregisterUpdatable();
            TryUnregisterSlowTickable();
            TryUnregisterHotSwapListener();
            TryUnregisterContext();
        }

        private void OnDestroy()
        {
            if (_runtimeOwnerAborted)
                return;

            ShutdownServiceState();
        }

        public void OnServiceShutdown()
        {
            ShutdownServiceState();
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (_runtimeOwnerAborted)
                return;

            if (serviceSlot == GlobalRegistryServiceSlot.Dispatcher)
            {
                TryUnregisterUpdatable();
                TryUnregisterSlowTickable();

                if (currentService != null && isActiveAndEnabled)
                {
                    TryRegisterUpdatable();
                    TryRegisterSlowTickable();
                }

                return;
            }

            if (!isActiveAndEnabled)
                return;

            if (serviceSlot == GlobalRegistryServiceSlot.PlayerInventory)
            {
                _playerInventoryService = currentService as IPlayerInventoryService;
                SyncPlayerContext();
                return;
            }

            if (serviceSlot == GlobalRegistryServiceSlot.PlayerSensory)
            {
                _playerSensoryService = currentService as IPlayerSensoryService;
                SyncPlayerContext();
            }
        }

        private void ShutdownServiceState()
        {
            if (_runtimeOwnerAborted)
                return;

            TryUnregisterUpdatable();
            TryUnregisterSlowTickable();
            TryUnregisterHotSwapListener();
            TryUnregisterContext();
            _isInitialized = false;
            _syncInProgress = false;
            _coldContextSyncRequested = false;
            _dynamicContextReferencesEnabled = false;
            ClearCachedPlayerReferences();

            if (ReferenceEquals(s_activeRuntimeInstance, this))
                s_activeRuntimeInstance = null;

            GlobalRegistry.ClearPlayerRuntimeContextRuntime(this);
        }

        private bool EnsureSingletonOwnership()
        {
            if (_runtimeOwnerAborted)
                return false;

            if (TryAbortForUsableExistingRuntime())
                return false;

            PlayerRuntimeContextService runtime = GlobalRegistry.PlayerRuntimeContextRuntime;
            if (!ReferenceEquals(runtime, null) && !ReferenceEquals(runtime, this))
            {
                GlobalRegistry.ClearPlayerRuntimeContextRuntime(runtime);
                runtime._registeredContext = false;
                runtime._isInitialized = false;
            }

            if (TryAbortForUsableExistingRuntime())
                return false;

            GlobalRegistry.RegisterPlayerRuntimeContextRuntime(this);
            return ReferenceEquals(GlobalRegistry.PlayerRuntimeContextRuntime, this);
        }

        private bool TryAbortForUsableExistingRuntime()
        {
            PlayerRuntimeContextService runtime = GlobalRegistry.PlayerRuntimeContextRuntime;
            if (!ReferenceEquals(runtime, null) && !ReferenceEquals(runtime, this))
            {
                if (IsPlayerRuntimeUsable(runtime))
                {
                    _runtimeOwnerAborted = true;
                    Destroy(gameObject);
                    return true;
                }

                GlobalRegistry.ClearPlayerRuntimeContextRuntime(runtime);
                runtime._registeredContext = false;
                runtime._isInitialized = false;
            }

            IPlayerRuntimeContext registeredContext = GlobalRegistry.RegisteredPlayer;
            if (ReferenceEquals(registeredContext, null) || ReferenceEquals(registeredContext, this))
                return false;

            if (IsPlayerContextUsable(registeredContext))
            {
                _runtimeOwnerAborted = true;
                Destroy(gameObject);
                return true;
            }

            PlayerRuntimeContextService staleRuntime = registeredContext as PlayerRuntimeContextService;
            if (!ReferenceEquals(staleRuntime, null))
            {
                WorldRuntimeReferenceUtility.InvalidatePlayerTransformCache(registeredContext.PlayerTransform);
                GlobalRegistry.UnregisterPlayerRuntimeContext(registeredContext);
                GlobalRegistry.ClearPlayerRuntimeContextRuntime(staleRuntime);
                staleRuntime._registeredContext = false;
                staleRuntime._isInitialized = false;
                if (ReferenceEquals(s_activeRuntimeInstance, staleRuntime))
                    s_activeRuntimeInstance = null;
            }

            return false;
        }

        private static PlayerRuntimeContextService ResolveUsableRuntime()
        {
            PlayerRuntimeContextService runtime = GlobalRegistry.PlayerRuntimeContextRuntime;
            if (IsPlayerRuntimeUsable(runtime))
                return runtime;

            if (!ReferenceEquals(runtime, null))
            {
                GlobalRegistry.ClearPlayerRuntimeContextRuntime(runtime);
                runtime._registeredContext = false;
                runtime._isInitialized = false;
            }

            IPlayerRuntimeContext registeredContext = GlobalRegistry.RegisteredPlayer;
            if (IsPlayerContextUsable(registeredContext))
                return registeredContext as PlayerRuntimeContextService;

            PlayerRuntimeContextService staleRuntime = registeredContext as PlayerRuntimeContextService;
            if (!ReferenceEquals(staleRuntime, null))
            {
                WorldRuntimeReferenceUtility.InvalidatePlayerTransformCache(registeredContext.PlayerTransform);
                GlobalRegistry.UnregisterPlayerRuntimeContext(registeredContext);
                GlobalRegistry.ClearPlayerRuntimeContextRuntime(staleRuntime);
                staleRuntime._registeredContext = false;
                staleRuntime._isInitialized = false;
                if (ReferenceEquals(s_activeRuntimeInstance, staleRuntime))
                    s_activeRuntimeInstance = null;
            }

            return null;
        }

        private static bool IsPlayerContextUsable(IPlayerRuntimeContext context)
        {
            if (ReferenceEquals(context, null))
                return false;

            PlayerRuntimeContextService runtime = context as PlayerRuntimeContextService;
            return ReferenceEquals(runtime, null) ||
                   (runtime._registeredContext && IsPlayerRuntimeUsable(runtime));
        }

        private static bool IsPlayerRuntimeUsable(PlayerRuntimeContextService runtime)
        {
            return runtime != null &&
                   runtime.isActiveAndEnabled &&
                   !runtime._runtimeOwnerAborted;
        }

        private void BindPlayerRoot(GameObject playerRoot)
        {
            if (playerRoot == null)
                return;

            _playerRootOverride = playerRoot;
            SyncPlayerContext();
        }

        private void ClearCachedPlayerReferences()
        {
            WorldRuntimeReferenceUtility.InvalidatePlayerTransformCache(_playerTransform);
            _playerRootOverride = null;
            _playerInventoryService = null;
            _playerSensoryService = null;
            _coldContextSyncRequested = false;
            _playerObject = null;
            _playerTransform = null;
            _playerMovement = null;
            _playerBuoyancyAirState = null;
            _playerRigidbody = null;
            _survivalSystem = null;
            _playerHealth = null;
            _toolManager = null;
            _inventory = null;
            _playerTransportCoordinator = null;
            _traumaDispatcher = null;
            _playerCamera = null;
            _playerCameraTransform = null;
            _playerPda = null;
            _playerBuilder = null;
            _visorController = null;
            _flashlight = null;
            _thrusterAudio = null;
            _underwaterVisuals = null;
            _handAnchor = null;
            _playerCollider = null;
            _hudNotification = null;
            _visorResolveBuffer.Clear();
            _runtimeContext.Clear();
        }

        private void SyncPlayerContext()
        {
            if (_syncInProgress)
                return;

            _syncInProgress = true;
            try
            {
                SyncPlayerContextColdInternal();
            }
            finally
            {
                _syncInProgress = false;
            }
        }

        private void SyncPlayerContextWithoutColdLookups()
        {
            if (_syncInProgress)
                return;

            _syncInProgress = true;
            try
            {
                SyncPlayerContextInternalNoColdLookups();
            }
            finally
            {
                _syncInProgress = false;
            }
        }

        private void SyncPlayerContextHot()
        {
            if (_syncInProgress)
                return;

            GameObject currentPlayerObject = BootstrapState.CurrentPlayerObject != null ? BootstrapState.CurrentPlayerObject : _playerRootOverride;
            bool playerRootChanged = !ReferenceEquals(_playerObject, currentPlayerObject);
            if (playerRootChanged ||
                _playerTransform == null ||
                _hudNotification == null && Application.isPlaying)
            {
                _coldContextSyncRequested = true;

                // A CHANGED ROOT is the only one of the three conditions above that needs the cold path, and
                // the distinction is load-bearing. The other two are steady states: in a headless run
                // _hudNotification is permanently null, so a single flag would be re-raised on every hot tick
                // forever, and routing that to the cold sync would run eleven TryGetComponent calls plus
                // CachePlayerHierarchyReferencesCold every slow tick. That is precisely why SlowTick used the
                // no-cold-lookups variant, and it must keep using it for those two.
                //
                // But the no-cold-lookups path performs neither EnsureOnPlayerRoot nor TryGetComponent - it
                // only re-points _playerObject and republishes. So the one owner that WATCHES for a new player
                // root was the one owner that skipped installing the player's components onto it, and the
                // hot path could never repair that. Installation depended entirely on a consumer happening to
                // call TryBindPlayerRoot from its own Awake (HectonSurvivalSystem.cs:709,
                // PlayerToolManager.cs:1307 and :1429). If HectonSurvivalSystem.Awake bails early on a null
                // stats asset, or the root is republished later with no consumer re-binding, the player ends
                // up with no health, trauma or transport owner and nothing ever notices.
                if (playerRootChanged)
                    _coldInstallRootDirty = true;

                if (_runtimeContext.IsBound && _playerTransform != null)
                    PublishMovementSnapshot();
                return;
            }

            _syncInProgress = true;
            try
            {
                if (_dynamicContextReferencesEnabled)
                    RefreshDynamicContextReferencesHot();

                PublishMovementSnapshot();
            }
            finally
            {
                _syncInProgress = false;
            }
        }

        private void SyncPlayerContextColdInternal()
        {
            GameObject currentPlayerObject = BootstrapState.CurrentPlayerObject != null ? BootstrapState.CurrentPlayerObject : _playerRootOverride;
            if (ReferenceEquals(_playerObject, currentPlayerObject) &&
                _playerTransform != null &&
                (_hudNotification != null || !Application.isPlaying))
            {
                if (_dynamicContextReferencesEnabled)
                    RefreshDynamicContextReferencesHot();
                PublishMovementSnapshot();
                return;
            }

            _playerObject = currentPlayerObject;
            _playerTransform = _playerObject != null ? _playerObject.transform : null;
            ClearPlayerComponentReferencesForRebind();

            if (_playerObject != null)
            {
                PlayerKinematicsRuntime.EnsureOnPlayerRoot(_playerObject);

                // Three of the eleven handles resolved below could never be non-null: no prefab carried
                // HectonPlayerHealth, TraumaDispatcher or PlayerTransportCoordinator, and no shipped code
                // added them. The reads succeeded and returned false, so the failure was silent -
                // PlayerHealth, TraumaDispatcher and PlayerTransportCoordinator were permanently null on
                // this service and on every consumer that resolves through it. What that cost:
                // HectonSurvivalSystem read RadiationExposure/Stress that HectonPlayerHealth owns, so the
                // player had no damage/injury model; its vehicle-leak oxygen drain (:2164), trauma stress
                // fold (:2430) and flooded-compartment thermal override (:2785, :2792) all sat behind
                // "_traumaDispatcher == null"; and MountablePlayerTransport.ResolveRiderReferences
                // (:908-915) refuses to mount a rider with no coordinator, so no vehicle was boardable.
                //
                // The installs sit BEFORE the TryGetComponent block on purpose: added after it, the
                // handles would stay null for this whole pass. Order inside the block is a real
                // dependency, not style. None of the three declares [DefaultExecutionOrder], so Unity
                // gives no ordering guarantee and the constructor order is the only lever:
                //   1. PlayerTransportCoordinator - TraumaDispatcher.OnEnable subscribes to its
                //      ActiveTransportLifecycleChanged event and only re-resolves the coordinator in
                //      Awake/OnEnable, so one that appears later is never picked up for the session.
                //   2. HectonPlayerHealth - TraumaDispatcher.ResolveReferences takes it by
                //      TryGetComponent in Awake; without it, PublishParasiteSporePoisonStatus silently
                //      degrades to this dispatcher's own GameObject for the combat target id.
                //   3. TraumaDispatcher last, so both of its siblings already exist.
                //
                // Deliberately NOT wrapped in "#if UNITY_EDITOR || DEVELOPMENT_BUILD". The guard on
                // PlayerKinematicsRuntime.EnsureOnPlayerRoot above (:1650) and on
                // SomaticKinematicsRuntime (:1218) is a judged, separate scope - those are VR/IK bridges
                // that allocate DataVault handles and register dispatcher lanes. Nothing in these three
                // is an editor concern; guarding them is what produced the defect.
                //
                // THE PLAYER ROOT CAN BE INACTIVE HERE, AND THAT IS FINE - DO NOT FORCE IT ACTIVE.
                // GameBootstrapper.DisablePlayer (:7889) publishes the root to BootstrapState and then
                // SetActive(false)s it, and BootstrapState.IsProductionPlayerAuthorityObject (:103) never
                // checks active state, so CurrentPlayerObject stays non-null while the root is dead for
                // every scene-readiness step up to ActivatePlayer (:7904). TryGetComponent resolves on an
                // inactive object, so the handles below are correct in the same pass, and Unity defers
                // the new components' Awake/OnEnable to ActivatePlayer's SetActive(true) (:7914) instead
                // of dropping them - which is the Kinematic Arrest Gate release, exactly when a suspended
                // player should start ticking. The four player installers at GameBootstrapper.cs:8000-8003
                // already depend on that same deferral, and HectonSurvivalSystem.OnEnable re-enters this
                // method through TryBindPlayerRoot at that activation, so a second pass runs with the root
                // live regardless. The forced SetActive(true) in EcosystemRuntimeInstaller.cs:69-71 is not
                // precedent for copying here: that is a __HECTON_* runtime root nothing else ever
                // activates, whereas forcing the player root active would break the arrest gate.
                PlayerTransportCoordinator.EnsureOnPlayerRoot(_playerObject);
                HectonPlayerHealth.EnsureOnPlayerRoot(_playerObject);
                TraumaDispatcher.EnsureOnPlayerRoot(_playerObject);

                _playerObject.TryGetComponent(out _playerMovement);
                _playerObject.TryGetComponent(out _playerBuoyancyAirState);
                _playerObject.TryGetComponent(out _playerRigidbody);
                _playerObject.TryGetComponent(out _survivalSystem);
                _playerObject.TryGetComponent(out _playerHealth);
                _playerObject.TryGetComponent(out _toolManager);
                _playerObject.TryGetComponent(out _inventory);
                _playerObject.TryGetComponent(out _playerTransportCoordinator);
                _playerObject.TryGetComponent(out _traumaDispatcher);
                _playerObject.TryGetComponent(out _playerPda);
                _playerObject.TryGetComponent(out _playerCollider);

                CachePlayerHierarchyReferencesCold();
                if (_dynamicContextReferencesEnabled)
                    RefreshDynamicContextReferencesCold();
            }

            SyncRuntimeContextAndPublish();
        }

        private void SyncPlayerContextInternalNoColdLookups()
        {
            GameObject currentPlayerObject = BootstrapState.CurrentPlayerObject != null ? BootstrapState.CurrentPlayerObject : _playerRootOverride;
            if (!ReferenceEquals(_playerObject, currentPlayerObject) || _playerTransform == null)
            {
                _playerObject = currentPlayerObject;
                _playerTransform = _playerObject != null ? _playerObject.transform : null;
                ClearPlayerComponentReferencesForRebind();
            }

            if (_dynamicContextReferencesEnabled)
                RefreshDynamicContextReferencesHot();

            SyncRuntimeContextAndPublish();
        }

        private void ClearPlayerComponentReferencesForRebind()
        {
            _playerMovement = null;
            _playerBuoyancyAirState = null;
            _playerRigidbody = null;
            _survivalSystem = null;
            _playerHealth = null;
            _toolManager = null;
            _inventory = null;
            _playerTransportCoordinator = null;
            _traumaDispatcher = null;
            _playerCamera = null;
            _playerCameraTransform = null;
            _playerPda = null;
            _playerBuilder = null;
            _visorController = null;
            _flashlight = null;
            _thrusterAudio = null;
            _underwaterVisuals = null;
            _handAnchor = null;
            _playerCollider = null;
            _runtimeContext.Clear();
        }

        private void SyncRuntimeContextAndPublish()
        {
            if (_underwaterVisuals == null)
                WorldRuntimeReferenceUtility.TryResolveHectonUnderwaterVisuals(ref _underwaterVisuals);

            if (_hudNotification == null || !_hudNotification.isActiveAndEnabled)
            {
                // This lazy resolve was always correct and always returned nothing: the project contained no
                // HUDNotification instance at all — its script guid occurred in exactly one file in the whole
                // tree, its own .cs.meta. So every warning aimed at the player resolved to null and went
                // nowhere: this context, PlayerSensoryManager through it, and EnvironmentalAnalyzerTool at
                // three separate call sites.
                //
                // Constructing on first need here, rather than from a bootstrap phase, follows the idiom the
                // bootstrapper already uses for canvas overlays: HardwareErrorCanvas is built lazily from its
                // own Show(), not from a phase list. It keeps the surface out of the main menu, where no
                // gameplay notification is ever raised, and puts the trigger at the one site that has been
                // asking for it since it was written. Construction stays owned by GameBootstrapper, which is
                // where the knowledge lives that this component needs both a Canvas ancestor and a
                // RectTransform or it silently builds no UI whatsoever.
                if (!HUDNotification.TryGetActive(out _hudNotification))
                    _hudNotification = GameBootstrapper.EnsureHudNotificationRegistered();
            }

            _runtimeContext.SyncReferences(
                _playerObject,
                _playerTransform,
                _playerMovement,
                _playerBuoyancyAirState,
                _playerRigidbody,
                _survivalSystem,
                _playerHealth,
                _toolManager,
                _inventory,
                _playerTransportCoordinator,
                _traumaDispatcher,
                _playerCamera,
                _playerPda,
                _playerBuilder,
                _visorController,
                _flashlight,
                _thrusterAudio,
                _underwaterVisuals,
                _handAnchor,
                _playerCollider,
                _hudNotification);
            PublishMovementSnapshot();
        }

        private void PublishMovementSnapshot()
        {
            if (!_runtimeContext.IsBound || _playerTransform == null)
                return;

            float3 velocity = ResolveMovementVelocitySnapshot();
            float3 forward = SafeDirection((float3)_playerTransform.forward, new float3(0f, 0f, 1f));
            Transform playerCameraTransform = ResolvePlayerCameraTransform();
            float3 cameraForward = SafeDirection(playerCameraTransform != null ? (float3)playerCameraTransform.forward : forward, forward);
            float depthMeters = SanitizeNonNegative(_playerMovement != null ? _playerMovement.CurrentDepth : 0f);
            float transportSpeedMultiplier = _playerTransportCoordinator != null
                ? math.max(0.01f, SanitizeNonNegative(_playerTransportCoordinator.ResolveTransportSpeedMultiplier()))
                : 1f;
            float underwaterStress01 = _playerMovement != null ? Sanitize01(_playerMovement.CurrentUnderwaterStressIntensity01) : 0f;
            float hullStress01 = _playerMovement != null ? Sanitize01(_playerMovement.CurrentHullStress01) : 0f;
            float abyssalCounterDriveEnergyMultiplier = _playerMovement != null
                ? math.max(1f, SanitizeNonNegative(_playerMovement.CurrentAbyssalCounterDriveEnergyMultiplier))
                : 1f;
            Vector3 fallbackPlayerPosition = default;
            bool fallbackPlayerPositionResolved = false;

            uint flags = 0u;
            if (_playerMovement != null)
                flags |= (uint)PlayerRuntimeSnapshotFlags.HasMovement;
            if (_playerRigidbody != null)
                flags |= (uint)PlayerRuntimeSnapshotFlags.HasRigidbody;
            if (_survivalSystem != null)
            {
                flags |= (uint)PlayerRuntimeSnapshotFlags.HasSurvival;
                if (_survivalSystem.IsAlive)
                    flags |= (uint)PlayerRuntimeSnapshotFlags.PlayerAlive;
                if (_survivalSystem.IsOxygenGraceActive)
                    flags |= (uint)PlayerRuntimeSnapshotFlags.OxygenGraceActive;
            }
            if (_toolManager != null)
                flags |= (uint)PlayerRuntimeSnapshotFlags.HasToolManager;
            if (_inventory != null)
                flags |= (uint)PlayerRuntimeSnapshotFlags.HasInventory;
            if (_playerTransportCoordinator != null)
                flags |= (uint)PlayerRuntimeSnapshotFlags.HasTransport;
            if (_traumaDispatcher != null)
                flags |= (uint)PlayerRuntimeSnapshotFlags.HasTrauma;
            if (_playerMovement != null && _playerMovement.IsPlayerSubmerged)
                flags |= (uint)PlayerRuntimeSnapshotFlags.Underwater;

            PlayerMovementRuntimeState movementState = default;
            if (_playerMovement != null)
            {
                AbsoluteUniversePosition currentAup = _playerMovement.CurrentAup;
                bool hasCurrentAup = currentAup.IsFinite();
                float3 currentRuntimePosition = hasCurrentAup
                    ? SafeFiniteVector(currentAup.ToRuntimeFloat3())
                    : SafeFiniteVector((float3)_playerTransform.position);
                float3 predictedRuntimePosition = SafeFiniteVector((float3)_playerMovement.PredictedRuntimePosition, currentRuntimePosition);
                movementState.WorldPosition = currentRuntimePosition;
                movementState.PredictedWorldPosition = predictedRuntimePosition;
                AbsoluteUniversePosition predictedAup = math.all(math.isfinite((float3)_playerMovement.PredictedRuntimePosition)) &&
                                                        _playerMovement.PredictedAup.IsFinite()
                    ? _playerMovement.PredictedAup
                    : currentAup;
                if (!predictedAup.IsFinite())
                    RuntimeOriginRoute.TryRuntimePositionToAup(predictedRuntimePosition, ref predictedAup);
                movementState.PredictedAup = predictedAup;
            }
            else
            {
                fallbackPlayerPosition = ToVector3(SafeFiniteVector((float3)_playerTransform.position));
                fallbackPlayerPositionResolved = true;
                movementState.WorldPosition = fallbackPlayerPosition;
                movementState.PredictedWorldPosition = movementState.WorldPosition + (velocity * 0.1f);
                var predictedAup = movementState.PredictedAup;
                if (RuntimeOriginRoute.TryRuntimePositionToAup(movementState.PredictedWorldPosition, ref predictedAup))
                    movementState.PredictedAup = predictedAup;
            }

            if (math.all(math.isfinite(movementState.WorldPosition)) &&
                movementState.PredictedAup.IsFinite())
            {
                flags |= (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot;
            }

            movementState.Velocity = velocity;
            movementState.Forward = forward;
            movementState.CameraForward = cameraForward;
            movementState.DepthMeters = depthMeters;
            movementState.TransportSpeedMultiplier = transportSpeedMultiplier;
            movementState.UnderwaterStressIntensity01 = underwaterStress01;
            movementState.Flags = flags;
            _runtimeContext.PublishMovementState(in movementState);

            PlayerMovementStressRuntimeState movementStressState = default;
            movementStressState.HullStress01 = hullStress01;
            movementStressState.UnderwaterStressIntensity01 = underwaterStress01;
            movementStressState.AbyssalCounterDriveEnergyMultiplier = abyssalCounterDriveEnergyMultiplier;
            movementStressState.Flags = flags;
            _runtimeContext.PublishMovementStressState(in movementStressState);

            PlayerLookState lookState = default;
            if (playerCameraTransform != null)
            {
                lookState.EyePosition = SafeFiniteVector((float3)playerCameraTransform.position, movementState.WorldPosition);
            }
            else
            {
                if (!fallbackPlayerPositionResolved)
                    fallbackPlayerPosition = ToVector3(SafeFiniteVector((float3)_playerTransform.position, movementState.WorldPosition));

                lookState.EyePosition = (float3)fallbackPlayerPosition;
            }
            lookState.AimForward = SafeDirection(cameraForward, forward);
            lookState.Flags = flags;
            _runtimeContext.PublishLookState(in lookState);

            PublishEffortLoadSnapshot();
        }

        private void PublishEffortLoadSnapshot()
        {
            float carriedMassKg = ResolveEffortLoadCarriedMassKg();
            float carryCapacityKg = ResolveEffortLoadCarryCapacityKg();
            float loadRatio = Hecton8.PureLogic.Systems.PlayerEffortLoadCalculator.ComputeLoadRatio(carriedMassKg, carryCapacityKg);
            float load01 = Hecton8.PureLogic.Systems.PlayerEffortLoadCalculator.ComputeLoad01(carriedMassKg, carryCapacityKg);
            float criticalEncumbranceRatio = ResolveEffortLoadCriticalEncumbranceRatio();
            float criticalStaminaFailureThreshold01 = ResolveEffortLoadCriticalStaminaFailureThreshold01();
            float stamina01 = _survivalSystem != null ? Sanitize01(_survivalSystem.EnergyNormalized) : 1f;

            uint flags = 0u;
            if (_runtimeContext.IsBound)
                flags |= (uint)PlayerEffortLoadRuntimeFlags.HasPlayerRoot;
            if (_inventory != null)
                flags |= (uint)PlayerEffortLoadRuntimeFlags.HasInventory;
            if (_playerMovement != null)
                flags |= (uint)PlayerEffortLoadRuntimeFlags.HasMovement;
            if (_survivalSystem != null)
                flags |= (uint)PlayerEffortLoadRuntimeFlags.HasSurvival;
            if (Hecton8.PureLogic.Systems.PlayerEffortLoadCalculator.IsCriticalInventoryLoad(loadRatio, criticalEncumbranceRatio))
                flags |= (uint)PlayerEffortLoadRuntimeFlags.CriticallyEncumbered;
            if (_playerMovement != null && _playerMovement.IsCriticalStaminaFailureActive)
                flags |= (uint)PlayerEffortLoadRuntimeFlags.CriticalStaminaFailureActive;
            if (_playerMovement != null && _playerMovement.IsSprinting)
                flags |= (uint)PlayerEffortLoadRuntimeFlags.Sprinting;
            if (_playerMovement != null && _playerMovement.IsPlayerSubmerged)
                flags |= (uint)PlayerEffortLoadRuntimeFlags.Submerged;
            if (_playerMovement != null && _playerMovement.IsWalking)
                flags |= (uint)PlayerEffortLoadRuntimeFlags.Walking;

            PlayerEffortLoadRuntimeState effortLoadState = default;
            effortLoadState.CarriedMassKg = carriedMassKg;
            effortLoadState.CarryCapacityKg = carryCapacityKg;
            effortLoadState.LoadRatio = loadRatio;
            effortLoadState.Load01 = load01;
            effortLoadState.MovementMultiplier = ResolveEffortLoadMovementMultiplier(carriedMassKg, carryCapacityKg);
            effortLoadState.UpwardSwimMultiplier = ResolveEffortLoadUpwardSwimMultiplier(load01);
            effortLoadState.Stamina01 = stamina01;
            effortLoadState.MovementIntent01 = _playerMovement != null ? Sanitize01(_playerMovement.CurrentMovementIntent01) : 0f;
            effortLoadState.MovementStaminaDrainMultiplier = _playerMovement != null ? SanitizeNonNegative(_playerMovement.CurrentMovementStaminaDrainMultiplier) : 0f;
            effortLoadState.CriticalEncumbranceRatio = criticalEncumbranceRatio;
            effortLoadState.CriticalStaminaFailureThreshold01 = criticalStaminaFailureThreshold01;
            effortLoadState.LocomotionModeCode = _playerMovement != null ? (uint)_playerMovement.CurrentLocomotionMode : 0u;
            effortLoadState.Flags = flags;
            _runtimeContext.PublishEffortLoadState(in effortLoadState);
        }

        private float ResolveEffortLoadCarriedMassKg()
        {
            if (_inventory != null)
                return SanitizeNonNegative(_inventory.TotalMassKg);

            if (_survivalSystem != null)
                return SanitizeNonNegative(_survivalSystem.Weight);

            return _playerMovement != null ? SanitizeNonNegative(_playerMovement.InventoryCarriedMassKg) : 0f;
        }

        private float ResolveEffortLoadCarryCapacityKg()
        {
            return _survivalSystem != null && _survivalSystem.Stats != null
                ? SanitizePositive(_survivalSystem.Stats.CarryCapacityKg, Hecton8.PureLogic.Systems.PlayerEffortLoadCalculator.DefaultCarryCapacityKg)
                : Hecton8.PureLogic.Systems.PlayerEffortLoadCalculator.DefaultCarryCapacityKg;
        }

        private float ResolveEffortLoadMovementMultiplier(float carriedMassKg, float carryCapacityKg)
        {
            float fallback = Hecton8.PureLogic.Systems.PlayerEffortLoadCalculator.ComputeMovementMultiplier(
                carriedMassKg,
                carryCapacityKg,
                Hecton8.PureLogic.Systems.PlayerEffortLoadCalculator.DefaultMinimumMovementMultiplier);

            if (_playerMovement != null)
                return SanitizeMultiplier01(_playerMovement.InventoryLoadMovementMultiplier, fallback);

            if (_inventory != null)
                return SanitizeMultiplier01(_inventory.CachedMaxSwimSpeedMultiplier, fallback);

            return SanitizeMultiplier01(fallback, 1f);
        }

        private float ResolveEffortLoadUpwardSwimMultiplier(float load01)
        {
            float fallback = Hecton8.PureLogic.Systems.PlayerEffortLoadCalculator.ComputeUpwardSwimMultiplier(
                load01,
                Hecton8.PureLogic.Systems.PlayerEffortLoadCalculator.DefaultMinimumUpwardSwimMultiplier);

            return _playerMovement != null
                ? SanitizeMultiplier01(_playerMovement.InventoryUpwardSwimMultiplier, fallback)
                : SanitizeMultiplier01(fallback, 1f);
        }

        private float ResolveEffortLoadCriticalEncumbranceRatio()
        {
            return _playerMovement != null
                ? SanitizePositive(_playerMovement.CurrentCriticalEncumbranceRatio, Hecton8.PureLogic.Systems.PlayerEffortLoadCalculator.DefaultCriticalEncumbranceRatio)
                : Hecton8.PureLogic.Systems.PlayerEffortLoadCalculator.DefaultCriticalEncumbranceRatio;
        }

        private float ResolveEffortLoadCriticalStaminaFailureThreshold01()
        {
            return _playerMovement != null
                ? Sanitize01(_playerMovement.CurrentCriticalStaminaFailureThreshold01)
                : Hecton8.PureLogic.Systems.PlayerEffortLoadCalculator.DefaultCriticalStaminaFailureThreshold01;
        }

        private Transform ResolvePlayerCameraTransform()
        {
            if (_playerCameraTransform != null)
                return _playerCameraTransform;

            if (_playerCamera == null)
                return null;

            _playerCameraTransform = _playerCamera.transform;
            return _playerCameraTransform;
        }

        private void AssignPlayerCamera(Camera playerCamera)
        {
            if (ReferenceEquals(_playerCamera, playerCamera))
            {
                if (_playerCamera != null && _playerCameraTransform == null)
                    _playerCameraTransform = _playerCamera.transform;

                return;
            }

            _playerCamera = playerCamera;
            _playerCameraTransform = playerCamera != null ? playerCamera.transform : null;
        }

        private float3 ResolveMovementVelocitySnapshot()
        {
            if (CoreDeterminismSignals.TryGetLatestKccVelocityFloat3(KccVelocityRuntimeContextMaxAgeFrames, out float3 kccVelocity))
                return SafeFiniteVector(kccVelocity);

            if (_playerMovement != null)
                return SafeFiniteVector((float3)_playerMovement.CurrentWorldVelocity);

            if (_playerRigidbody != null)
                return SafeFiniteVector((float3)_playerRigidbody.linearVelocity);

            return float3.zero;
        }

        private static Vector3 ToVector3(float3 value)
        {
            return new Vector3(value.x, value.y, value.z);
        }

        private static float Sanitize01(float value)
        {
            return math.isfinite(value) ? math.saturate(value) : 0f;
        }

        private static float SanitizeNonNegative(float value)
        {
            return math.isfinite(value) ? math.max(0f, value) : 0f;
        }

        private static float SanitizePositive(float value, float fallback)
        {
            return math.isfinite(value) && value > 0f
                ? value
                : math.max(0.001f, fallback);
        }

        private static float SanitizeMultiplier01(float value, float fallback)
        {
            return math.isfinite(value)
                ? math.saturate(value)
                : math.saturate(fallback);
        }

        private static float3 SafeFiniteVector(float3 value)
        {
            return math.all(math.isfinite(value)) ? value : float3.zero;
        }

        private static float3 SafeFiniteVector(float3 value, float3 fallback)
        {
            return math.all(math.isfinite(value)) ? value : SafeFiniteVector(fallback);
        }

        private static float3 SafeDirection(float3 direction, float3 fallback)
        {
            float directionSqr = math.lengthsq(direction);
            if (math.all(math.isfinite(direction)) && directionSqr > 0.000001f)
                return direction * math.rsqrt(directionSqr);

            float fallbackSqr = math.lengthsq(fallback);
            if (math.all(math.isfinite(fallback)) && fallbackSqr > 0.000001f)
                return fallback * math.rsqrt(fallbackSqr);

            return new float3(0f, 0f, 1f);
        }

        private static bool IsFinitePredictedAup(in PlayerMovementRuntimeState state)
        {
            var aup = state.PredictedAup;
            double3 absolute = aup.ToAbsoluteDouble3();
            return math.all(math.isfinite(absolute));
        }

        private void RefreshDynamicContextReferencesHot()
        {
            IPlayerInventoryService playerInventoryService = _playerInventoryService;
            if (playerInventoryService != null)
            {
                if (_toolManager == null)
                    _toolManager = playerInventoryService.ToolManager;

                if (_inventory == null)
                    _inventory = playerInventoryService.Inventory;

                if (_playerBuilder == null)
                    _playerBuilder = playerInventoryService.PlayerBuilder;

                if (_handAnchor == null)
                    _handAnchor = playerInventoryService.HandAnchor;
            }

            IPlayerSensoryService playerSensoryService = _playerSensoryService;
            if (playerSensoryService != null)
            {
                if (_playerCamera == null)
                    AssignPlayerCamera(playerSensoryService.PlayerCamera);

                if (_flashlight == null)
                    _flashlight = playerSensoryService.Flashlight;

                if (_thrusterAudio == null)
                    _thrusterAudio = playerSensoryService.ThrusterAudio;

                if (_underwaterVisuals == null)
                    _underwaterVisuals = playerSensoryService.UnderwaterVisuals;

                if (_visorController == null)
                    _visorController = playerSensoryService.VisorController;

                if (_hudNotification == null)
                    _hudNotification = playerSensoryService.HudNotification;
            }

            if (_toolManager != null)
            {
                if (_inventory == null)
                    _inventory = _toolManager.Inventory;

                _handAnchor = _toolManager.HandAnchor;
            }

            if (_playerPda == null)
                PlayerPDA.TryResolveActiveRuntime(ref _playerPda);
        }

        private void RefreshDynamicContextReferencesCold()
        {
            RefreshDynamicContextReferencesHot();

            if (_playerMovement != null)
            {
                if (_playerRigidbody == null)
                    _playerMovement.TryGetComponent(out _playerRigidbody);
            }

            if (_playerObject != null)
            {
                if (_flashlight == null)
                    _playerObject.TryGetComponent(out _flashlight);

                if (_thrusterAudio == null)
                    _playerObject.TryGetComponent(out _thrusterAudio);

                if (_playerBuilder == null)
                    _playerObject.TryGetComponent(out _playerBuilder);
            }

        }

        private void RefreshDynamicServiceReferencesCold()
        {
            _playerInventoryService = GlobalRegistry.RegisteredPlayerInventory;
            _playerSensoryService = GlobalRegistry.RegisteredPlayerSensory;
        }

        private void CachePlayerHierarchyReferencesCold()
        {
            if (_playerMovement != null)
            {
                Transform playerCameraTransform = _playerMovement.PlayerCameraTransform;
                if (playerCameraTransform != null)
                {
                    if (_playerCamera == null)
                    {
                        playerCameraTransform.TryGetComponent(out Camera resolvedCamera);
                        AssignPlayerCamera(resolvedCamera);
                    }

                    if (_flashlight == null)
                        playerCameraTransform.TryGetComponent(out _flashlight);

                    if (_thrusterAudio == null)
                        playerCameraTransform.TryGetComponent(out _thrusterAudio);
                }
            }

            if (_playerTransform != null && _visorController == null)
            {
                _visorController = _playerTransform.GetComponentInChildren<VisorHUDController>(true);
            }
        }

        private void TryRegisterUpdatable()
        {
            if (_registeredUpdatable || !Application.isPlaying)
                return;

            if (GlobalRegistry.Dispatcher == null)
                return;

            _registeredUpdatable = GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.Core);
        }

        private void TryUnregisterUpdatable()
        {
            if (!_registeredUpdatable)
                return;

            GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Core);
            _registeredUpdatable = false;
        }

        private void TryRegisterSlowTickable()
        {
            if (_registeredSlowTickable || !Application.isPlaying)
                return;

            if (GlobalRegistry.Dispatcher == null)
                return;

            _registeredSlowTickable = GlobalRegistry.TryRegisterSlowTickable(this, PriorityLayer.Core);
        }

        private void TryUnregisterSlowTickable()
        {
            if (!_registeredSlowTickable)
                return;

            GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Core);
            _registeredSlowTickable = false;
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

        private bool TryRegisterContext()
        {
            if (_runtimeOwnerAborted)
                return false;

            if (_registeredContext)
                return true;

            if (TryAbortForUsableExistingRuntime())
                return false;

            IPlayerRuntimeContext registeredContext = GlobalRegistry.RegisteredPlayer;
            if (!ReferenceEquals(registeredContext, null) && !ReferenceEquals(registeredContext, this))
            {
                PlayerRuntimeContextService staleRuntime = registeredContext as PlayerRuntimeContextService;
                if (ReferenceEquals(staleRuntime, null))
                {
                    _runtimeOwnerAborted = true;
                    Destroy(gameObject);
                    return false;
                }

                WorldRuntimeReferenceUtility.InvalidatePlayerTransformCache(registeredContext.PlayerTransform);
                GlobalRegistry.UnregisterPlayerRuntimeContext(registeredContext);
                GlobalRegistry.ClearPlayerRuntimeContextRuntime(staleRuntime);
                staleRuntime._registeredContext = false;
                staleRuntime._isInitialized = false;
                if (ReferenceEquals(s_activeRuntimeInstance, staleRuntime))
                    s_activeRuntimeInstance = null;
            }

            if (TryAbortForUsableExistingRuntime())
                return false;

            GlobalRegistry.RegisterPlayerRuntimeContext(this);
            _registeredContext = ReferenceEquals(GlobalRegistry.Player, this);
            if (_registeredContext)
            {
                s_activeRuntimeInstance = this;
                HectonXRRuntimeState.BindPlayerContextFallbackCold(this);
            }

            _runtimeOwnerAborted = !_registeredContext;
            if (_runtimeOwnerAborted)
                Destroy(gameObject);
            return _registeredContext;
        }

        private void TryUnregisterContext()
        {
            if (!_registeredContext)
                return;

            WorldRuntimeReferenceUtility.InvalidatePlayerTransformCache(_playerTransform);
            GlobalRegistry.UnregisterPlayerRuntimeContext(this);
            _registeredContext = false;
            if (ReferenceEquals(s_activeRuntimeInstance, this))
                s_activeRuntimeInstance = null;
            HectonXRRuntimeState.BindPlayerContextFallbackCold(null);
        }
    }
}
