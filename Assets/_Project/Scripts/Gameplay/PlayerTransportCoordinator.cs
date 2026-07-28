using System;
using Hecton8.Core;
using Hecton8.Core.Contracts.Signals;
using UnityEngine;

namespace Hecton8.Gameplay
{
    /// <summary>
    /// Resolves the currently active player transport source.
    /// </summary>
    /// <remarks>
    /// Tool-driven transport is resolved from <see cref="PlayerToolManager"/>.
    /// External mounted transport can temporarily claim ownership through <see cref="SetExternalTransportSource"/>.
    /// </remarks>
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/Gameplay/Player Transport Coordinator")]
    public sealed class PlayerTransportCoordinator : MonoBehaviour, IUpdatable, IPlayerTransportLifecycleResolver, IGlobalRegistryHotSwapListener
    {
        private const float DefaultTransportPropulsionReference = 800f;

        [Header("References")]
        [Tooltip("Optional explicit tool owner used for handheld transport resolution.")]
        [SerializeField] private PlayerToolManager playerToolManager;

        private IPlayerTransportSource _externalTransportSource;
        private MonoBehaviour _externalTransportBehaviour;
        private PlayerTransportFeelContract _externalTransportFeelContract;
        private IPlayerTransportLifecycleOwner _externalTransportLifecycleOwner;
        private IPlayerTransportLifecycleOwner _publishedLifecycleOwner;
        private bool _registered;
        private bool _registeredHotSwap;
        private uint _toolLoadoutSignalSourceId;
        private uint _lastToolLoadoutSignalSequence;

        /// <summary>
        /// Raised when the resolved runtime transport lifecycle owner changes.
        /// </summary>
        public event Action<IPlayerTransportLifecycleOwner> ActiveTransportLifecycleChanged;

        /// <summary>
        /// Installs the transport coordinator on the bootstrap-published player root when the authored
        /// prefab does not already carry one.
        /// </summary>
        /// <param name="playerRoot">Bootstrap-published player root.</param>
        internal static void EnsureOnPlayerRoot(GameObject playerRoot)
        {
            if (playerRoot == null)
                return;

            if (playerRoot.TryGetComponent(out PlayerTransportCoordinator _))
                return;

            // Unguarded on purpose - see the rationale block at
            // PlayerRuntimeContextService.SyncPlayerContextColdInternal, which owns the call order and
            // the inactive-player-root argument. Short form: the player root is this type's authored
            // home - HectonPlayerMovement.ResolvePlayerTransportCoordinatorCold (:5544) and
            // ResolveReferencesCold below both TryGetComponent it off their own GameObject, and
            // MountablePlayerTransport.ResolveRiderReferences (:908-915) refuses the mount outright when
            // the rider has none. An editor-only guard would ship a build where no vehicle can be
            // boarded.
            playerRoot.AddComponent<PlayerTransportCoordinator>(); // COLD ALLOC: PlayerTransportCoordinator[1] - player transport source resolver install on the bootstrap-published player root - owner: PlayerRuntimeContextService
        }

        private void Awake()
        {
            ResolveReferencesCold();
            PublishActiveTransportLifecycleChanged();
        }

        private void OnEnable()
        {
            ResolveReferencesCold();
            TryRegisterHotSwapListener();
            TryRegister();
            PublishActiveTransportLifecycleChanged();
        }

        private void Start()
        {
            ResolveReferencesCold();
            TryRegisterHotSwapListener();
            TryRegister();
        }

        private void OnDisable()
        {
            TryUnregisterTick();
            TryUnregisterHotSwapListener();
            _publishedLifecycleOwner = null;
            _lastToolLoadoutSignalSequence = 0u;
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot != GlobalRegistryServiceSlot.Dispatcher)
                return;

            TryUnregisterTick();
            if (currentService != null && isActiveAndEnabled)
                TryRegister();
        }

        /// <inheritdoc />
        public void Tick(float dt)
        {
            ConsumeToolLoadoutChangedSignals();
        }

        /// <summary>
        /// Registers an external transport source such as a mounted vehicle.
        /// </summary>
        public bool SetExternalTransportSource(IPlayerTransportSource source)
        {
            if (source == null)
            {
                _externalTransportSource = null;
                _externalTransportBehaviour = null;
                _externalTransportFeelContract = null;
                return false;
            }

            MonoBehaviour sourceBehaviour = source as MonoBehaviour;
            if (sourceBehaviour == null)
                return false;

            _externalTransportSource = source;
            _externalTransportBehaviour = sourceBehaviour;
            sourceBehaviour.TryGetComponent(out _externalTransportFeelContract);
            _externalTransportLifecycleOwner = sourceBehaviour as IPlayerTransportLifecycleOwner;
            PublishActiveTransportLifecycleChanged();
            return true;
        }

        /// <summary>
        /// Clears the current external transport source if the owner matches.
        /// </summary>
        public void ClearExternalTransportSource(IPlayerTransportSource source)
        {
            if (source == null || !ReferenceEquals(_externalTransportSource, source))
                return;

            _externalTransportSource = null;
            _externalTransportBehaviour = null;
            _externalTransportFeelContract = null;
            _externalTransportLifecycleOwner = null;
            PublishActiveTransportLifecycleChanged();
        }

        /// <summary>
        /// Returns true when any transport source is currently resolved.
        /// </summary>
        public bool HasActiveTransportSource()
        {
            return TryResolveTransportSource(out _);
        }

        /// <summary>
        /// Resolves the active transport lifecycle owner for collision, charging, and failure logic.
        /// </summary>
        public bool TryResolveTransportLifecycleOwner(out IPlayerTransportLifecycleOwner lifecycleOwner)
        {
            if (TryResolveExternalTransportLifecycleOwner(out lifecycleOwner))
                return true;

            ResolveReferences();
            if (playerToolManager == null || playerToolManager.IsSwapping)
            {
                lifecycleOwner = null;
                return false;
            }

            lifecycleOwner = playerToolManager.CurrentTool as IPlayerTransportLifecycleOwner;
            return lifecycleOwner != null;
        }

        /// <summary>
        /// Returns true when the resolved transport is actively engaged.
        /// </summary>
        public bool IsTransportActive()
        {
            return TryResolveTransportSource(out IPlayerTransportSource source) && source.IsTransportActive;
        }

        /// <summary>
        /// Returns true when an external mounted transport currently owns handheld-tool suppression.
        /// </summary>
        public bool BlocksHandheldToolUsage()
        {
            if (!TryResolveExternalTransportSource(out _))
                return false;

            PlayerTransportPreset preset = ResolveTransportPreset();
            return preset == null || preset.HolsterToolOnMount;
        }

        /// <summary>
        /// Resolves current transport propulsion force.
        /// </summary>
        public float ResolveTransportPropulsionForce()
        {
            if (!TryResolveTransportSource(out IPlayerTransportSource source))
                return 0f;

            if (source is IKinematicVehicleTransportSource kinematicVehicleSource && kinematicVehicleSource.IsVehicleMotionAuthoritative)
                return 0f;

            return Mathf.Max(0f, source.GetTransportPropulsionForce());
        }

        /// <summary>
        /// Resolves current transport speed multiplier.
        /// </summary>
        public float ResolveTransportSpeedMultiplier()
        {
            if (!TryResolveTransportSource(out IPlayerTransportSource source))
                return 1f;

            if (source is IKinematicVehicleTransportSource kinematicVehicleSource && kinematicVehicleSource.IsVehicleMotionAuthoritative)
                return 1f;

            return Mathf.Max(0.01f, source.GetTransportSpeedMultiplier());
        }

        /// <summary>
        /// Resolves current transport drag coefficient multiplier.
        /// </summary>
        public float ResolveTransportDragCoefficientMultiplier()
        {
            if (!TryResolveTransportSource(out IPlayerTransportSource source))
                return 1f;

            if (source is IKinematicVehicleTransportSource kinematicVehicleSource && kinematicVehicleSource.IsVehicleMotionAuthoritative)
                return 1f;

            return Mathf.Max(0.01f, source.GetTransportDragCoefficientMultiplier());
        }

        /// <summary>
        /// Resolves current normalized transport boost.
        /// </summary>
        public float ResolveTransportBoost01()
        {
            if (!TryResolveTransportSource(out IPlayerTransportSource source))
                return 0f;

            float transportBoost = Mathf.Clamp01(source.GetTransportBoost01());
            if (transportBoost > 0f)
                return transportBoost;

            PlayerTransportFeelContract transportFeelContract = ResolveTransportFeelContract();
            float reference = transportFeelContract != null
                ? Mathf.Max(0.01f, transportFeelContract.PropulsionForceReference)
                : DefaultTransportPropulsionReference;
            return Mathf.Clamp01(source.GetTransportPropulsionForce() / reference);
        }

        /// <summary>
        /// Resolves the active transport feel contract for presentation and audio consumers.
        /// </summary>
        internal PlayerTransportFeelContract ResolveTransportFeelContract()
        {
            if (TryResolveExternalTransportFeelContract(out PlayerTransportFeelContract contract))
                return contract;

            ResolveReferences();
            if (playerToolManager == null || playerToolManager.IsSwapping)
                return null;

            return playerToolManager.CurrentToolTransportFeelContract;
        }

        internal PlayerTransportPreset ResolveTransportPreset()
        {
            PlayerTransportFeelContract contract = ResolveTransportFeelContract();
            return contract != null ? contract.Preset : null;
        }

        internal PlayerTransportOccupancyMode ResolveTransportOccupancyMode()
        {
            PlayerTransportFeelContract contract = ResolveTransportFeelContract();
            return contract != null
                ? contract.OccupancyMode
                : PlayerTransportOccupancyMode.Handheld;
        }

        internal float ResolveTransportCameraMotionScale()
        {
            PlayerTransportFeelContract contract = ResolveTransportFeelContract();
            return contract != null
                ? Mathf.Clamp01(contract.CameraMotionScale)
                : 1f;
        }

        internal bool TryResolveTransportSource(out IPlayerTransportSource source)
        {
            if (TryResolveExternalTransportSource(out source))
                return true;

            ResolveReferences();
            if (playerToolManager == null || playerToolManager.IsSwapping)
            {
                source = null;
                return false;
            }

            source = playerToolManager.CurrentToolTransportSource;
            return source != null;
        }

        private bool TryResolveExternalTransportSource(out IPlayerTransportSource source)
        {
            if ((object)_externalTransportBehaviour != null && _externalTransportBehaviour != null)
            {
                source = _externalTransportSource;
                return source != null;
            }

            _externalTransportSource = null;
            _externalTransportBehaviour = null;
            source = null;
            return false;
        }

        private bool TryResolveExternalTransportFeelContract(out PlayerTransportFeelContract contract)
        {
            if ((object)_externalTransportBehaviour != null &&
                _externalTransportBehaviour != null &&
                _externalTransportFeelContract != null)
            {
                contract = _externalTransportFeelContract;
                return true;
            }

            _externalTransportFeelContract = null;
            contract = null;
            return false;
        }

        private bool TryResolveExternalTransportLifecycleOwner(out IPlayerTransportLifecycleOwner lifecycleOwner)
        {
            if ((object)_externalTransportBehaviour != null &&
                _externalTransportBehaviour != null &&
                _externalTransportLifecycleOwner != null)
            {
                lifecycleOwner = _externalTransportLifecycleOwner;
                return true;
            }

            _externalTransportLifecycleOwner = null;
            lifecycleOwner = null;
            return false;
        }

        private void ResolveReferences()
        {
            if (playerToolManager == null)
            {
                IPlayerRuntimeContext runtimeContext = PlayerRuntimeContextService.ActiveRuntimeContext;
                if (runtimeContext != null)
                    playerToolManager = runtimeContext.ToolManager;
            }
        }

        private void ResolveReferencesCold()
        {
            ResolveReferences();
            if (playerToolManager == null)
                gameObject.TryGetComponent(out playerToolManager);
        }

        private void TryRegister()
        {
            if (_registered || !Application.isPlaying)
                return;

            if (GlobalRegistry.Dispatcher == null)
                return;

            _registered = GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.Player);
        }

        private void TryUnregisterTick()
        {
            if (!_registered)
                return;

            GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Player);
            _registered = false;
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

        private void ConsumeToolLoadoutChangedSignals()
        {
            ResolveReferences();
            uint sourceId = ResolveToolLoadoutSignalSourceId();
            if (sourceId == 0u)
                return;

            bool changed = false;
            ReadOnlySpan<ToolLoadoutChangedSignal> signals = SignalBus<ToolLoadoutChangedSignal>.GetFrameSnapshot();
            for (int i = 0; i < signals.Length; i++)
            {
                ref readonly ToolLoadoutChangedSignal signal = ref signals[i];
                if (signal.SourceId != sourceId ||
                    signal.Sequence == 0u ||
                    signal.Sequence <= _lastToolLoadoutSignalSequence)
                {
                    continue;
                }

                _lastToolLoadoutSignalSequence = signal.Sequence;
                changed = true;
            }

            if (changed)
                PublishActiveTransportLifecycleChanged();
        }

        private uint ResolveToolLoadoutSignalSourceId()
        {
            if (_toolLoadoutSignalSourceId == 0u && playerToolManager != null && playerToolManager.gameObject != null)
            {
                _toolLoadoutSignalSourceId =
                    RuntimeOriginRoute.FoldEntityIdToSourceId(EntityId.ToULong(playerToolManager.gameObject.GetEntityId()));
            }

            return _toolLoadoutSignalSourceId;
        }

        private void PublishActiveTransportLifecycleChanged()
        {
            IPlayerTransportLifecycleOwner lifecycleOwner;
            if (!TryResolveTransportLifecycleOwner(out lifecycleOwner))
                lifecycleOwner = null;

            if (ReferenceEquals(_publishedLifecycleOwner, lifecycleOwner))
                return;

            _publishedLifecycleOwner = lifecycleOwner;
            ActiveTransportLifecycleChanged?.Invoke(lifecycleOwner);
        }
    }
}
