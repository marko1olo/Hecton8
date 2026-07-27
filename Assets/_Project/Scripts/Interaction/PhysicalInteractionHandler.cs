// ============================================================================
// HECTON-8 — PhysicalInteractionHandler.cs
// Player-owned interaction layer for physical pocket pickups and heavy cargo drag.
// ============================================================================

namespace Hecton8.Interaction
{
    using Hecton8.Core;
    using Hecton8.Gameplay;
    using Hecton8.Items;
    using Hecton8.World;
    using Unity.Mathematics;
    using UnityEngine;

    /// <summary>
    /// Receiver contract for collider-backed physical panel buttons.
    /// Keeps physical interaction logic independent from concrete UI component load order.
    /// </summary>
    public interface IPhysicalPanelButtonReceiver
    {
        /// <summary>
        /// Attempts to queue a physical hand press through the interaction signal service.
        /// </summary>
        /// <param name="sampleFrame">Frame stamp captured by the physical hand probe for all receivers in this sample.</param>
        bool TryQueueHandPress(
            Vector3 handPosition,
            Vector3 handForward,
            IInteractionSignalService interactionSignals,
            Collider handSourceCollider,
            PhysicalHandSide fallbackHandSide,
            int sampleFrame);
    }

    /// <summary>
    /// Owns physical interaction sequences that should happen before inventory insertion
    /// or while dragging heavy rigidbody cargo in front of the player.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/Interaction/Physical Interaction Handler")]
    public sealed class PhysicalInteractionHandler : MonoBehaviour, ITickable, IFixedTickable, ILateFrameTickable, IGlobalRegistryHotSwapListener
    {
        private enum InteractionState : byte
        {
            Idle,
            PullingPocketItem,
            DraggingHeavyObject,
            DraggingCablePlug
        }

        private const int MaxParentComponentResolveDepth = 32;
        private const float MaxInteractionDeltaTime = 0.05f;
        private const float MinPanelButtonProbeRadius = 0.005f;
        private const float MaxPanelButtonProbeRadius = 0.2f;
        private const float MaxPocketPickupOffsetMeters = 4f;
        private const float MinSafeLocalScaleMagnitude = 0.001f;
        private const float MaxSafeLocalScaleMagnitude = 32f;

        [Header("── References ──────────────────")]
        [Tooltip("Optional explicit anchor for physical pickup arrival and heavy cargo hold point. Falls back to the local player camera.")]
        [SerializeField] private Transform interactionAnchor;

        [Tooltip("Optional explicit survival reference. Resolved on Awake when null.")]
        [SerializeField] private HectonSurvivalSystem survivalSystem;

        [Header("── Pocket Pickup ──────────────────")]
        [Tooltip("Maximum rigidbody mass still treated as a pocket pickup.")]
        [SerializeField, Range(0.1f, 80f)] private float maxPocketPickupMass = 18f;

        [Tooltip("Duration of the pull-to-hand pickup sequence.")]
        [SerializeField, Range(0.05f, 1f)] private float pickupDuration = 0.22f;

        [Tooltip("World-space offset from the anchor where the item finishes before entering inventory.")]
        [SerializeField] private Vector3 pocketPickupAnchorOffset = new Vector3(0f, -0.12f, 0.35f);

        [Tooltip("Final scale multiplier reached before the item enters inventory.")]
        [SerializeField, Range(0.05f, 1f)] private float pickupFinalScaleMultiplier = 0.2f;

        [Tooltip("Linear speed used by rigidbody-based pocket pickups.")]
        [SerializeField, Range(0.5f, 40f)] private float pickupMoveSpeed = 10f;

        [Header("── Heavy Carry ──────────────────")]
        [Tooltip("Minimum rigidbody mass treated as heavy carry cargo.")]
        [SerializeField, Range(1f, 400f)] private float heavyCarryMinMass = 25f;

        [Tooltip("Maximum rigidbody mass the player can still drag manually.")]
        [SerializeField, Range(5f, 800f)] private float heavyCarryMaxMass = 220f;

        [Tooltip("Distance in front of the player where heavy cargo is held.")]
        [SerializeField, Range(0.5f, 6f)] private float heavyCarryDistance = 2.4f;

        [Tooltip("Move speed used while dragging heavy cargo.")]
        [SerializeField, Range(0.25f, 12f)] private float heavyCarryMoveSpeed = 2.1f;

        [Tooltip("Maximum allowed separation before the drag breaks.")]
        [SerializeField, Range(1f, 12f)] private float heavyCarryBreakDistance = 5f;

        [Tooltip("Suit energy drained per second while dragging heavy cargo.")]
        [SerializeField, Range(0f, 20f)] private float heavyCarryEnergyDrainPerSecond = 3.5f;

        [Header("Physical Panels")]
        [Tooltip("Enables collider-volume diegetic panel button presses from the kinematic hand probe.")]
        [SerializeField] private bool enablePhysicalPanelButtons = true;

        [Tooltip("Radius around the hand probe used to overlap physical panel button trigger volumes.")]
        [SerializeField, Range(0.005f, 0.2f)] private float panelButtonProbeRadius = 0.035f;

        [Tooltip("Layer mask containing physical diegetic panel button BoxCollider trigger volumes.")]
        [SerializeField] private LayerMask panelButtonMask = HectonLayerMasks.UILayerMask | HectonLayerMasks.InteractableLayerMask;

        [Header("Flora Pick IK")]
        [Tooltip("Radius around the physical hand probe used to resolve an indirect-flora harvest snap target.")]
        [SerializeField, Range(0.1f, 2f)] private float floraHarvestSnapSearchRadius = 1.25f;

        [Tooltip("Seconds the hand probe latches to the resolved flora harvest target during a pick animation.")]
        [SerializeField, Range(0.05f, 0.6f)] private float floraHarvestSnapDuration = 0.18f;

        [Tooltip("Optional capability filter for flora pick snap resolution. None accepts any harvestable flora target.")]
        [SerializeField] private FloraDataTemplate.VulnerabilityMask floraHarvestSnapCapabilityMask = FloraDataTemplate.VulnerabilityMask.None;

        [Header("── Heavy Carry Movement Feel ──────────────────")]
        [Tooltip("Movement-force multiplier while dragging the lightest valid heavy object.")]
        [SerializeField, Range(0.1f, 1f)] private float lightHeavyCarryForceMultiplier = 0.76f;

        [Tooltip("Movement-force multiplier while dragging the heaviest valid heavy object.")]
        [SerializeField, Range(0.1f, 1f)] private float maxHeavyCarryForceMultiplier = 0.42f;

        [Tooltip("Max-speed multiplier while dragging the lightest valid heavy object.")]
        [SerializeField, Range(0.1f, 1f)] private float lightHeavyCarrySpeedMultiplier = 0.82f;

        [Tooltip("Max-speed multiplier while dragging the heaviest valid heavy object.")]
        [SerializeField, Range(0.1f, 1f)] private float maxHeavyCarrySpeedMultiplier = 0.52f;

        [Tooltip("Cargo follow-speed multiplier while dragging the lightest valid heavy object.")]
        [SerializeField, Range(0.25f, 1.5f)] private float lightHeavyCarryFollowSpeedMultiplier = 1.08f;

        [Tooltip("Cargo follow-speed multiplier while dragging the heaviest valid heavy object.")]
        [SerializeField, Range(0.25f, 1.5f)] private float maxHeavyCarryFollowSpeedMultiplier = 0.72f;

        [Tooltip("Extra catch-up boost applied when dragged cargo falls behind the anchor point.")]
        [SerializeField, Range(1f, 3f)] private float heavyCarryCatchUpSpeedMultiplier = 1.65f;

        [Tooltip("Base vertical offset for dragged cargo relative to the interaction anchor.")]
        [SerializeField, Range(-2f, 1f)] private float heavyCarryVerticalOffset = -0.34f;

        [Tooltip("How strongly camera pitch is allowed to raise or lower dragged cargo.")]
        [SerializeField, Range(0f, 1f)] private float heavyCarryPitchInfluence = 0.28f;

        [Tooltip("Maximum extra vertical offset contributed by camera pitch while dragging cargo.")]
        [SerializeField, Range(0f, 2f)] private float heavyCarryMaxVerticalPitchOffset = 0.72f;

        [Tooltip("How much the heaviest draggable cargo sags downward instead of locking to a flat anchor line.")]
        [SerializeField, Range(0f, 1.5f)] private float heavyCarryLoadSag = 0.3f;

        [Tooltip("How much the heaviest draggable cargo trails behind the anchor instead of sitting on a perfect distance ring.")]
        [SerializeField, Range(0f, 1f)] private float heavyCarryRearLagDistance = 0.24f;

#if UNITY_EDITOR || DEVELOPMENT_BUILD

        [Header("── Diagnostics ──────────────────")]
#pragma warning disable CS0414
        [SerializeField] private string _debugState = "Idle";
        [SerializeField] private string _debugTargetName;
#pragma warning restore CS0414
#endif

        private Transform _cachedTransform;
        private Camera _playerCamera;
        private PhysicalHandController _physicalHandController;
        private IInteractionSignalService _interactionSignals;
        private IPhysicsService _physicsService;
        private IOrganicToolHitService _organicToolHits;
        private InteractionState _state;
        private bool _hasExplicitInteractionAnchor;
        private bool _registeredTick;
        private bool _registeredFixedTick;
        private bool _registeredLateFrameTick;
        private bool _registeredHotSwapListener;
        private bool _dispatcherAvailable;
        private Transform _pendingPocketVisualTransform;
        private Vector3 _pendingPocketVisualPosition;
        private Vector3 _pendingPocketVisualScale;
        private bool _pendingPocketVisualPositionDirty;
        private bool _pendingPocketVisualScaleDirty;
        private float _stateTimer;
        private Vector3 _pullSmoothDampVelocity;
        private const int MaxPanelButtonOverlaps = 8;
        private static readonly int _DefaultPanelButtonLayerMask =
            HectonLayerMasks.UILayerMask |
            HectonLayerMasks.InteractableLayerMask;
        private readonly Collider[] _panelButtonOverlaps = new Collider[MaxPanelButtonOverlaps]; // COLD ALLOC: Collider[8] - physical panel button overlap buffer - owner: PhysicalInteractionHandler
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private bool _panelButtonOverlapSaturationLogged;
#endif

        private IInteractable _activeInteractable;
        private MonoBehaviour _activeBehaviour;
        private Transform _activeTargetTransform;
        private Rigidbody _activeBody;
        private Collider _activeCollider;
        private HeavyCarryInteractable _activeHeavyCarry;
        private VRCableDragPlug _activeCablePlug;
        private Vector3 _activeOriginalLocalScale;
        private Vector3 _activeTargetLocalScale;
        private bool _activeBodyWasKinematic;
        private bool _activeBodyDetectCollisions;
        private Vector3 _activeBodyLinearVelocity;
        private Vector3 _activeBodyAngularVelocity;
        private bool _activeColliderWasEnabled;
        private float _activeHeavyCarryMass;
        private int _resolvedPanelButtonLayerMask;

        /// <summary>
        /// True while the player is actively dragging a heavy rigidbody object.
        /// </summary>
        public bool IsDraggingHeavyObject =>
            _state == InteractionState.DraggingHeavyObject &&
            _physicalHandController != null &&
            _physicalHandController.IsGrabbing;

        /// <summary>
        /// Normalized 0-1 load factor for the currently dragged heavy object.
        /// </summary>
        public float HeavyCarryLoad01
        {
            get
            {
                if (!IsDraggingHeavyObject)
                    return 0f;

                float minMass = ClampFiniteRange(heavyCarryMinMass, 1f, 400f, 25f);
                float maxMass = ClampFiniteRange(heavyCarryMaxMass, 5f, 800f, 220f);
                if (maxMass < minMass)
                    maxMass = minMass;

                float safeActiveMass = math.isfinite(_activeHeavyCarryMass) ? _activeHeavyCarryMass : minMass;
                float massRange = math.max(maxMass - minMass, 0.01f);
                return math.saturate((safeActiveMass - minMass) / massRange);
            }
        }

        private void Awake()
        {
            _cachedTransform = transform;
            _hasExplicitInteractionAnchor = interactionAnchor != null;
            ResolveColdReferences();
            TryGetComponent(out _physicalHandController);
            RefreshPanelButtonLayerMask();
            if (enablePhysicalPanelButtons && HectonXRRuntimeState.IsXRActive)
                EnsurePhysicalHandController();
        }

        private void OnEnable()
        {
            HectonXRRuntimeState.XRActiveChanged -= HandleXRActiveChanged;
            HectonXRRuntimeState.XRActiveChanged += HandleXRActiveChanged;
            _interactionSignals = GlobalRegistry.InteractionSignals;
            _physicsService = GlobalRegistry.Physics;
            _organicToolHits = GlobalRegistry.OrganicToolHits;
            _dispatcherAvailable = GlobalRegistry.Dispatcher != null;
            ResolveColdReferences();
            TryRegisterHotSwapListener();
            RefreshPanelButtonLayerMask();
            if (enablePhysicalPanelButtons && HectonXRRuntimeState.IsXRActive)
                EnsurePhysicalHandController();
            RefreshTickRegistration();
        }

        private void OnDisable()
        {
            HectonXRRuntimeState.XRActiveChanged -= HandleXRActiveChanged;
            _dispatcherAvailable = false;
            TryUnregisterHotSwapListener();
            _interactionSignals = null;
            _physicsService = null;
            _organicToolHits = null;
            CancelActiveInteraction();
            UnregisterFromTickSystems();
        }

        private void OnDestroy()
        {
            _dispatcherAvailable = false;
            TryUnregisterHotSwapListener();
            _interactionSignals = null;
            _physicsService = null;
            _organicToolHits = null;
        }

        /// <inheritdoc />
        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (!isActiveAndEnabled)
                return;

            if (serviceSlot == GlobalRegistryServiceSlot.Player)
            {
                ResolveColdReferences(currentService as IPlayerRuntimeContext, forcePlayerCameraRefresh: true);
                return;
            }

            if (serviceSlot == GlobalRegistryServiceSlot.InteractionSignals)
            {
                _interactionSignals = currentService as IInteractionSignalService;
                return;
            }

            if (serviceSlot == GlobalRegistryServiceSlot.Physics)
            {
                _physicsService = currentService as IPhysicsService;
                return;
            }

            if (serviceSlot == GlobalRegistryServiceSlot.DestructibleOrganicRuntime)
            {
                _organicToolHits = currentService as IOrganicToolHitService;
                return;
            }

            if (serviceSlot != GlobalRegistryServiceSlot.Dispatcher)
                return;

            UnregisterFromTickSystems();
            _dispatcherAvailable = currentService != null;
            if (_dispatcherAvailable)
                RefreshTickRegistration();
        }

        /// <summary>
        /// Attempts to intercept a normal interaction and route it into the physical interaction layer.
        /// Returns true only when the interaction has been consumed by this handler.
        /// </summary>
        public bool TryHandleInteraction(IInteractable interactable, Transform interactor)
        {
            InteractableRegistry.TargetInfo targetInfo = default;
            return TryHandleInteraction(interactable, interactor, in targetInfo);
        }

        internal bool TryHandleInteraction(
            IInteractable interactable,
            Transform interactor,
            in InteractableRegistry.TargetInfo targetInfo)
        {
            if (interactable == null || interactor == null || !ReferenceEquals(interactor, _cachedTransform))
                return false;

            if (_state == InteractionState.PullingPocketItem)
                return true;

            if (_state == InteractionState.DraggingHeavyObject)
            {
                if (ReferenceEquals(interactable, _activeInteractable))
                {
                    CancelActiveInteraction();
                    return true;
                }

                return false;
            }

            if (_state == InteractionState.DraggingCablePlug)
            {
                if (ReferenceEquals(interactable, _activeInteractable))
                {
                    CancelActiveInteraction();
                    return true;
                }

                return false;
            }

            MonoBehaviour behaviour = interactable as MonoBehaviour;
            if (behaviour == null)
                return false;

            if (TryBeginCablePlugDrag(interactable, behaviour, in targetInfo))
                return true;

            if (TryBeginPocketPickup(interactable, behaviour, in targetInfo))
                return true;

            if (TryBeginHeavyCarry(interactable, behaviour, in targetInfo))
                return true;

            return false;
        }

        /// <summary>
        /// Resolves the nearest indirect-flora harvest point and starts a transient physical hand snap.
        /// </summary>
        public bool TryBeginFloraHarvestSnap()
        {
            return TryBeginFloraHarvestSnap((uint)floraHarvestSnapCapabilityMask);
        }

        /// <summary>
        /// Resolves the nearest indirect-flora harvest point with an explicit tool capability filter.
        /// </summary>
        public bool TryBeginFloraHarvestSnap(uint toolCapabilityMask)
        {
            if (_state != InteractionState.Idle)
                return false;

            if (!EnsurePhysicalHandController())
                return false;

            if (!_physicalHandController.TryGetInteractionProbePose(out Vector3 handPosition, out _))
                return false;
            if (!IsFiniteVector(handPosition))
                return false;

            IOrganicToolHitService organicManager = _organicToolHits;
            if (organicManager == null)
                return false;

            float searchRadius = ClampFiniteRange(floraHarvestSnapSearchRadius, 0.1f, 2f, 1.25f);
            if (!organicManager.TryResolveNearestHarvestInteractionPoint(
                    handPosition,
                    searchRadius,
                    toolCapabilityMask,
                    out FloraHarvestInteractionPoint interactionPoint))
            {
                return false;
            }

            float snapDuration = ClampFiniteRange(floraHarvestSnapDuration, 0.05f, 0.6f, 0.18f);
            bool started = _physicalHandController.TryBeginHarvestSnap(in interactionPoint, snapDuration);
            if (started)
                RefreshTickRegistration();
            return started;
        }

        /// <summary>
        /// Exposes the active flora pick snap pose for animation layers that read from the interaction handler.
        /// </summary>
        public bool TryGetFloraHarvestSnapPose(out Vector3 position, out Quaternion rotation, out float blend)
        {
            if (_physicalHandController == null)
            {
                position = default;
                rotation = default;
                blend = 0f;
                return false;
            }

            return _physicalHandController.TryGetHarvestSnapPose(out position, out rotation, out blend);
        }

        /// <summary>
        /// Cancels the current physical interaction, restoring altered rigidbody state when needed.
        /// </summary>
        public void CancelActiveInteraction()
        {
            if (_state == InteractionState.PullingPocketItem)
                RestorePocketPickupState();
            else if (_state == InteractionState.DraggingHeavyObject && _physicalHandController != null)
                _physicalHandController.EndGrab(PhysicalHandGrabEndReason.ManualRelease);
            else if (_state == InteractionState.DraggingCablePlug && _activeCablePlug != null)
                _activeCablePlug.EndDrag();

            ClearActiveState();
        }

        /// <summary>
        /// Hard external release hook for tactile systems that detect invalid hand constraints.
        /// </summary>
        public void ForceRelease()
        {
            CancelActiveInteraction();
        }

        public void Tick(float deltaTime)
        {
            float safeDeltaTime = ClampInteractionDeltaTime(deltaTime);
            TickPhysicalPanelButtons();

            if (_state == InteractionState.Idle)
                return;

            if (_activeTargetTransform == null || !(_activeBehaviour != null && _activeBehaviour.gameObject.activeInHierarchy))
            {
                CancelActiveInteraction();
                return;
            }

            switch (_state)
            {
                case InteractionState.PullingPocketItem:
                    TickPocketPickup(safeDeltaTime);
                    break;

                case InteractionState.DraggingHeavyObject:
                    TickHeavyCarry(safeDeltaTime);
                    break;

                case InteractionState.DraggingCablePlug:
                    TickCablePlugDrag();
                    break;
            }
        }

        public void FixedTick(float fixedDeltaTime)
        {
            float safeFixedDeltaTime = ClampInteractionDeltaTime(fixedDeltaTime);
            if (_physicalHandController != null)
            {
                Vector3 controllerPosition = GetAnchorTargetPosition();
                Quaternion controllerRotation = interactionAnchor != null ? interactionAnchor.rotation : _cachedTransform.rotation;
                bool handControllerRequiredFixedTick = _physicalHandController.RequiresFixedTick;
                _physicalHandController.StepFixed(safeFixedDeltaTime, controllerPosition, controllerRotation);
                if (handControllerRequiredFixedTick != _physicalHandController.RequiresFixedTick ||
                    _registeredLateFrameTick != _physicalHandController.RequiresLateFrameTick)
                {
                    RefreshTickRegistration();
                }
            }

            if (_state == InteractionState.Idle)
                return;

            switch (_state)
            {
                case InteractionState.DraggingHeavyObject:
                    FixedTickHeavyCarry(safeFixedDeltaTime);
                    break;
            }
        }

        /// <summary>
        /// Finalizes deferred hand-probe jobs in the dispatcher-owned late-frame swap phase.
        /// </summary>
        public void LateFrameTick()
        {
            FlushPocketPickupVisualPose();

            if (_physicalHandController != null)
            {
                _physicalHandController.LateFrameTick();
                if (!_physicalHandController.RequiresLateFrameTick)
                    RefreshTickRegistration();
            }
            else if (_state == InteractionState.Idle)
            {
                RefreshTickRegistration();
            }
        }

        private void TickPhysicalPanelButtons()
        {
            if (!enablePhysicalPanelButtons || !HectonXRRuntimeState.IsXRActive)
                return;

            if (_physicalHandController == null)
                return;

            if (!PhysicalHandReceiverRegistry.HasReceivers)
                return;

            IInteractionSignalService interactionSignals = _interactionSignals;
            if (interactionSignals == null || !interactionSignals.IsInitialized)
                return;

            if (!_physicalHandController.TryGetInteractionProbePose(out Vector3 handPosition, out Quaternion handRotation))
                return;
            if (!IsFiniteVector(handPosition))
                return;

            Collider handSourceCollider = null;
            PhysicalHandSide handSide = _physicalHandController.HandSide;
            _physicalHandController.TryGetInteractionProbeCollider(out handSourceCollider);

            float probeRadius = ResolvePanelButtonProbeRadius();
            int hitCount = PhysicalHandReceiverRegistry.QuerySphere(
                handPosition,
                probeRadius,
                _resolvedPanelButtonLayerMask,
                _panelButtonOverlaps);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (hitCount >= _panelButtonOverlaps.Length && !_panelButtonOverlapSaturationLogged)
            {
                _panelButtonOverlapSaturationLogged = true;
                Hecton8.Core.H8Debug.LogWarning("[PhysicalInteractionHandler] Physical panel overlap buffer saturated.", this);
            }
#endif
            if (hitCount <= 0)
                return;

            Vector3 handForward = handRotation * Vector3.forward;
            if (!IsFiniteVector(handForward))
                return;

            IPhysicalPanelButtonReceiver bestButton = null;
            float bestDistanceSq = float.MaxValue;
            float bestCenterDistanceSq = float.MaxValue;
            for (int i = 0; i < hitCount && i < _panelButtonOverlaps.Length; i++)
            {
                Collider candidate = _panelButtonOverlaps[i];
                _panelButtonOverlaps[i] = null;
                if (candidate == null)
                    continue;

                if (!PhysicalHandReceiverRegistry.TryResolve(candidate, out IPhysicalPanelButtonReceiver button))
                    continue;

                Bounds candidateBounds = candidate.bounds;
                float distanceSq = candidateBounds.SqrDistance(handPosition);
                float centerDistanceSq = math.lengthsq((float3)(handPosition - candidateBounds.center));
                if (distanceSq > bestDistanceSq ||
                    (distanceSq == bestDistanceSq && centerDistanceSq >= bestCenterDistanceSq))
                {
                    continue;
                }

                bestDistanceSq = distanceSq;
                bestCenterDistanceSq = centerDistanceSq;
                bestButton = button;
            }

            if (bestButton != null)
            {
                int sampleFrame = Hecton8.Core.SystemDispatcher.CurrentFrameIndex;
                bestButton.TryQueueHandPress(handPosition, handForward, interactionSignals, handSourceCollider, handSide, sampleFrame);
            }
        }

        private void HandleXRActiveChanged(bool isActive)
        {
            if (isActive && enablePhysicalPanelButtons)
                EnsurePhysicalHandController();

            RefreshTickRegistration();
        }

        private void RefreshPanelButtonLayerMask()
        {
            // Same dead-query hole as PlayerInteraction.ResolveInteractableLayerMask: an
            // inspector-cleared mask (Nothing) rejects every receiver in
            // PhysicalHandReceiverRegistry.QuerySphere, so no panel button can ever be pressed.
            _resolvedPanelButtonLayerMask = InteractionProbeLayerMask.Resolve(
                panelButtonMask.value,
                _DefaultPanelButtonLayerMask);
        }

        private bool TryBeginCablePlugDrag(
            IInteractable interactable,
            MonoBehaviour behaviour,
            in InteractableRegistry.TargetInfo targetInfo)
        {
            VRCableDragPlug cablePlug = targetInfo.Interactable as VRCableDragPlug;
            if (cablePlug == null)
                cablePlug = interactable as VRCableDragPlug;
            if (cablePlug == null)
                return false;

            Transform cableAnchor = interactionAnchor != null ? interactionAnchor : _cachedTransform;
            cablePlug.BeginDrag(cableAnchor, this);

            _activeInteractable = interactable;
            _activeBehaviour = behaviour;
            _activeTargetTransform = cablePlug.transform;
            _activeBody = null;
            _activeCollider = null;
            _activeHeavyCarry = null;
            _activeCablePlug = cablePlug;
            _activeOriginalLocalScale = Vector3.one;
            _activeTargetLocalScale = Vector3.one;
            _stateTimer = 0f;
            _pullSmoothDampVelocity = Vector3.zero;

            _state = InteractionState.DraggingCablePlug;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _debugState = "DraggingCablePlug";
            CacheDebugTargetName(_activeBehaviour);
#endif
            RefreshTickRegistration();
            return true;
        }

        private bool TryBeginPocketPickup(
            IInteractable interactable,
            MonoBehaviour behaviour,
            in InteractableRegistry.TargetInfo targetInfo)
        {
            if (targetInfo.Pickup == null &&
                !(targetInfo.PickupSource is PickupItem) &&
                !(targetInfo.PickupSource is HectonItem) &&
                !(interactable is PickupItem) &&
                !(interactable is HectonItem))
            {
                return false;
            }

            Rigidbody body = targetInfo.PhysicsBody;

            if (body != null)
            {
                float bodyMass = body.mass;
                float pocketMassLimit = ClampFiniteRange(maxPocketPickupMass, 0.1f, 80f, 18f);
                if (!math.isfinite(bodyMass) || bodyMass < 0f || bodyMass > pocketMassLimit)
                    return false;
            }

            Collider targetCollider = targetInfo.PhysicsCollider;

            _activeInteractable = interactable;
            _activeBehaviour = behaviour;
            _activeTargetTransform = behaviour.transform;
            _activeBody = body;
            _activeCollider = targetCollider;
            _activeHeavyCarry = null;
            _activeCablePlug = null;
            _activeOriginalLocalScale = SanitizeLocalScale(_activeTargetTransform.localScale);
            _activeTargetLocalScale = SanitizeLocalScale(_activeOriginalLocalScale * ClampFiniteRange(pickupFinalScaleMultiplier, 0.05f, 1f, 0.2f));
            _stateTimer = 0f;
            _pullSmoothDampVelocity = Vector3.zero;

            if (_activeCollider != null)
            {
                _activeColliderWasEnabled = _activeCollider.enabled;
                _activeCollider.enabled = false;
            }

            if (_activeBody != null)
            {
                _activeBodyWasKinematic = _activeBody.isKinematic;
                _activeBodyDetectCollisions = _activeBody.detectCollisions;
                _activeBodyLinearVelocity = IsFiniteVector(_activeBody.linearVelocity) ? _activeBody.linearVelocity : Vector3.zero;
                _activeBodyAngularVelocity = IsFiniteVector(_activeBody.angularVelocity) ? _activeBody.angularVelocity : Vector3.zero;
                _activeBody.isKinematic = true;
                _activeBody.detectCollisions = false;
            }

            _state = InteractionState.PullingPocketItem;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _debugState = "PullingPocketItem";
            CacheDebugTargetName(_activeBehaviour);
#endif
            RefreshTickRegistration();
            return true;
        }

        private static bool TryResolveParentComponent<T>(Transform start, out T component) where T : Component
        {
            component = null;
            Transform current = start;
            int depth = 0;
            while (current != null && depth < MaxParentComponentResolveDepth)
            {
                if (current.TryGetComponent(out component))
                    return true;

                current = current.parent;
                depth++;
            }

            return false;
        }

        private void ResolveColdReferences(IPlayerRuntimeContext playerContext = null, bool forcePlayerCameraRefresh = false)
        {
            if (survivalSystem == null)
                TryGetComponent(out survivalSystem);

            if (forcePlayerCameraRefresh || _playerCamera == null)
            {
                if (playerContext == null)
                    playerContext = GlobalRegistry.Player;

                _playerCamera = playerContext != null ? playerContext.PlayerCamera : null;
                if (_playerCamera == null)
                    TryGetComponent(out _playerCamera);
                if (_playerCamera == null)
                    TryResolveParentComponent(transform, out _playerCamera);
            }

            if (!_hasExplicitInteractionAnchor &&
                _playerCamera != null &&
                (interactionAnchor == null || forcePlayerCameraRefresh))
            {
                interactionAnchor = _playerCamera.transform;
            }
        }

        private bool TryBeginHeavyCarry(
            IInteractable interactable,
            MonoBehaviour behaviour,
            in InteractableRegistry.TargetInfo targetInfo)
        {
            HeavyCarryInteractable heavyCarry = targetInfo.Interactable as HeavyCarryInteractable;
            if (heavyCarry == null)
                heavyCarry = interactable as HeavyCarryInteractable;
            if (heavyCarry == null)
                return false;

            if (!heavyCarry.TryGetCarryBody(out Rigidbody carryBody) || carryBody == null)
                return false;

            float bodyMass = carryBody.mass;
            float minMass = ClampFiniteRange(heavyCarryMinMass, 1f, 400f, 25f);
            float maxMass = ClampFiniteRange(heavyCarryMaxMass, 5f, 800f, 220f);
            if (maxMass < minMass)
                maxMass = minMass;

            if (carryBody.isKinematic ||
                !math.isfinite(bodyMass) ||
                bodyMass < minMass ||
                bodyMass > maxMass ||
                !IsFiniteVector(carryBody.worldCenterOfMass))
            {
                return false;
            }

            if (!EnsurePhysicalHandController() || !_physicalHandController.BeginGrab(heavyCarry, carryBody))
                return false;

            _activeInteractable = interactable;
            _activeBehaviour = behaviour;
            _activeTargetTransform = carryBody.transform;
            _activeBody = carryBody;
            _activeCollider = null;
            _activeHeavyCarry = heavyCarry;
            _activeHeavyCarry.SetDraggedState(true);
            _activeCablePlug = null;
            _activeOriginalLocalScale = _activeTargetTransform.localScale;
            _activeTargetLocalScale = _activeOriginalLocalScale;
            _stateTimer = 0f;
            _pullSmoothDampVelocity = Vector3.zero;
            _activeHeavyCarryMass = bodyMass;

            _state = InteractionState.DraggingHeavyObject;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _debugState = "DraggingHeavyObject";
            CacheDebugTargetName(_activeBehaviour);
#endif
            RefreshTickRegistration();
            return true;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private void CacheDebugTargetName(MonoBehaviour behaviour)
        {
            _debugTargetName = behaviour != null ? behaviour.name : null;
        }
#endif

        private bool EnsurePhysicalHandController()
        {
            if (_physicalHandController != null)
                return true;

            if (TryGetComponent(out _physicalHandController))
                return true;

            _physicalHandController = gameObject.AddComponent<PhysicalHandController>(); // COLD ALLOC: PhysicalHandController[1] - heavy-object articulation grab proxy - owner: PhysicalInteractionHandler
            return _physicalHandController != null;
        }

        private void TickPocketPickup(float deltaTime)
        {
            _stateTimer += deltaTime;

            float duration = ClampFiniteRange(pickupDuration, 0.05f, 1f, 0.22f);
            float progress = math.saturate(_stateTimer / duration);
            QueuePocketPickupVisualScale(_activeTargetTransform, (Vector3)math.lerp((float3)_activeOriginalLocalScale, (float3)_activeTargetLocalScale, progress));

            if (_activeTargetTransform != null)
            {
                Vector3 targetPosition = GetAnchorTargetPosition();
                Vector3 currentPosition = _activeTargetTransform.position;
                if (!IsFiniteVector(targetPosition) || !IsFiniteVector(currentPosition))
                    return;

                Vector3 toTarget = targetPosition - currentPosition;
                float distanceSq = toTarget.sqrMagnitude;
                float maxStep = ClampFiniteRange(pickupMoveSpeed, 0.5f, 40f, 10f) * math.max(0f, deltaTime);
                Vector3 nextPosition;
                if (distanceSq <= maxStep * maxStep || distanceSq <= 0.00000001f)
                {
                    nextPosition = targetPosition;
                    _pullSmoothDampVelocity = Vector3.zero;
                }
                else
                {
                    float inverseDistance = math.rcp(math.max(ApproximateMagnitudeNoSqrt(toTarget), 0.000001f));
                    Vector3 step = toTarget * (maxStep * inverseDistance);
                    nextPosition = currentPosition + step;
                    _pullSmoothDampVelocity = deltaTime > 0.0001f ? step / deltaTime : Vector3.zero;
                }

                if (!IsFiniteVector(nextPosition))
                    return;

                QueuePocketPickupVisualPosition(_activeTargetTransform, nextPosition);
            }

            if (progress >= 1f)
                CompletePocketPickup();
        }

        private void QueuePocketPickupVisualPosition(Transform target, Vector3 position)
        {
            if (target == null || !IsFiniteVector(position))
                return;

            _pendingPocketVisualTransform = target;
            _pendingPocketVisualPosition = position;
            _pendingPocketVisualPositionDirty = true;
        }

        private void QueuePocketPickupVisualScale(Transform target, Vector3 localScale)
        {
            if (target == null || !IsFiniteVector(localScale))
                return;

            _pendingPocketVisualTransform = target;
            _pendingPocketVisualScale = localScale;
            _pendingPocketVisualScaleDirty = true;
        }

        private void FlushPocketPickupVisualPose()
        {
            Transform target = _pendingPocketVisualTransform;
            if (target != null)
            {
                if (_pendingPocketVisualPositionDirty)
                    target.position = _pendingPocketVisualPosition;
                if (_pendingPocketVisualScaleDirty)
                    target.localScale = _pendingPocketVisualScale;
            }

            _pendingPocketVisualTransform = null;
            _pendingPocketVisualPositionDirty = false;
            _pendingPocketVisualScaleDirty = false;
        }

        private void TickHeavyCarry(float deltaTime)
        {
            if (_physicalHandController == null)
            {
                if (_activeBody == null || interactionAnchor == null)
                {
                    CancelActiveInteraction();
                    return;
                }

                Vector3 anchorPosition = interactionAnchor.position;
                Vector3 bodyPosition = _activeBody.worldCenterOfMass;
                if (!IsFiniteVector(anchorPosition) || !IsFiniteVector(bodyPosition))
                {
                    CancelActiveInteraction();
                    return;
                }

                if (!TryResolveAupFromRuntimeOrigin(anchorPosition, out AbsoluteUniversePosition anchorAup) ||
                    !TryResolveAupFromRuntimeOrigin(bodyPosition, out AbsoluteUniversePosition bodyAup))
                {
                    CancelActiveInteraction();
                    return;
                }

                float breakDistance = ClampFiniteRange(heavyCarryBreakDistance, 1f, 12f, 5f);
                if (AbsoluteUniversePosition.DistanceSq(in anchorAup, in bodyAup) > breakDistance * breakDistance)
                {
                    CancelActiveInteraction();
                    return;
                }
            }
            else if (!_physicalHandController.IsGrabbing)
            {
                CancelActiveInteraction();
                return;
            }

            if (survivalSystem != null)
            {
                float energyDrainPerSecond = ClampFiniteRange(heavyCarryEnergyDrainPerSecond, 0f, 20f, 3.5f);
                survivalSystem.DrainEnergy(energyDrainPerSecond * deltaTime);
                if (survivalSystem.Energy <= 0.01f)
                {
                    CancelActiveInteraction();
                    return;
                }
            }
        }

        private void TickCablePlugDrag()
        {
            if (_activeCablePlug == null || !_activeCablePlug.IsDragging)
            {
                ClearActiveState();
                return;
            }

            if (_physicalHandController == null)
                return;

            if (!_physicalHandController.TryGetInteractionProbePose(out Vector3 handPosition, out Quaternion handRotation))
                return;

            Vector3 forward = handRotation * Vector3.forward;
            if (!IsFiniteVector(handPosition) || !IsFiniteVector(forward))
                return;

            _activeCablePlug.SetManualDragPose(handPosition, forward);
        }

        private void FixedTickHeavyCarry(float fixedDeltaTime)
        {
            if (_physicalHandController == null || !_physicalHandController.IsGrabbing)
                return;
        }

        private void CompletePocketPickup()
        {
            if (_activeInteractable == null)
            {
                CancelActiveInteraction();
                return;
            }

            _activeInteractable.Interact(_cachedTransform);

            if (_activeBehaviour != null && _activeBehaviour.gameObject.activeInHierarchy)
                RestorePocketPickupState(false);

            ClearActiveState();
        }

        private void RestorePocketPickupState(bool restoreMotion = true)
        {
            if (_activeTargetTransform != null)
                QueuePocketPickupVisualScale(_activeTargetTransform, _activeOriginalLocalScale);

            if (_activeCollider != null)
                _activeCollider.enabled = _activeColliderWasEnabled;

            if (_activeBody != null)
            {
                _activeBody.isKinematic = _activeBodyWasKinematic;
                _activeBody.detectCollisions = _activeBodyDetectCollisions;
                if (restoreMotion && !_activeBodyWasKinematic)
                {
                    Vector3 restoredLinearVelocity = IsFiniteVector(_activeBodyLinearVelocity) ? _activeBodyLinearVelocity : Vector3.zero;
                    Vector3 currentLinearVelocity = IsFiniteVector(_activeBody.linearVelocity) ? _activeBody.linearVelocity : Vector3.zero;
                    Vector3 deltaVelocity = restoredLinearVelocity - currentLinearVelocity;
                    if (IsFiniteVector(deltaVelocity) && deltaVelocity.sqrMagnitude > 0.000001f)
                        _physicsService?.QueueForce(_activeBody, deltaVelocity, ForceMode.VelocityChange);

                    Vector3 restoredAngularVelocity = IsFiniteVector(_activeBodyAngularVelocity) ? _activeBodyAngularVelocity : Vector3.zero;
                    _physicsService?.QueueAngularVelocitySet(_activeBody, restoredAngularVelocity);
                }
            }
        }

        private void ClearActiveState()
        {
            _state = InteractionState.Idle;
            _stateTimer = 0f;
            _pullSmoothDampVelocity = Vector3.zero;
            _activeInteractable = null;
            _activeBehaviour = null;
            _activeTargetTransform = null;
            _activeBody = null;
            _activeCollider = null;
            if (_activeHeavyCarry != null)
                _activeHeavyCarry.SetDraggedState(false);
            _activeHeavyCarry = null;
            _activeCablePlug = null;
            _activeBodyLinearVelocity = Vector3.zero;
            _activeBodyAngularVelocity = Vector3.zero;
            _activeHeavyCarryMass = 0f;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _debugState = "Idle";
            _debugTargetName = null;
#endif
            RefreshTickRegistration();
        }

        /// <summary>
        /// Resolves the player movement-force multiplier imposed by the current heavy carry load.
        /// </summary>
        public float ResolveHeavyCarryForceMultiplier()
        {
            if (!IsDraggingHeavyObject)
                return 1f;

            float lightMultiplier = ClampFiniteRange(lightHeavyCarryForceMultiplier, 0.1f, 1f, 0.76f);
            float maxMultiplier = ClampFiniteRange(maxHeavyCarryForceMultiplier, 0.1f, 1f, 0.42f);
            return math.lerp(lightMultiplier, maxMultiplier, HeavyCarryLoad01);
        }

        /// <summary>
        /// Resolves the player max-speed multiplier imposed by the current heavy carry load.
        /// </summary>
        public float ResolveHeavyCarrySpeedMultiplier()
        {
            if (!IsDraggingHeavyObject)
                return 1f;

            float lightMultiplier = ClampFiniteRange(lightHeavyCarrySpeedMultiplier, 0.1f, 1f, 0.82f);
            float maxMultiplier = ClampFiniteRange(maxHeavyCarrySpeedMultiplier, 0.1f, 1f, 0.52f);
            return math.lerp(lightMultiplier, maxMultiplier, HeavyCarryLoad01);
        }

        private float ResolveHeavyCarryFollowSpeed(float separationDistance)
        {
            float lightFollowMultiplier = ClampFiniteRange(lightHeavyCarryFollowSpeedMultiplier, 0.25f, 1.5f, 1.08f);
            float maxFollowMultiplier = ClampFiniteRange(maxHeavyCarryFollowSpeedMultiplier, 0.25f, 1.5f, 0.72f);
            float loadSpeedMultiplier = math.lerp(
                lightFollowMultiplier,
                maxFollowMultiplier,
                HeavyCarryLoad01);

            float resolvedCarryDistance = ClampFiniteRange(heavyCarryDistance, 0.5f, 6f, 2.4f);
            float resolvedCatchUp = ClampFiniteRange(heavyCarryCatchUpSpeedMultiplier, 1f, 3f, 1.65f);
            float resolvedMoveSpeed = ClampFiniteRange(heavyCarryMoveSpeed, 0.25f, 12f, 2.1f);
            float catchUpRatio = math.saturate(separationDistance / resolvedCarryDistance);
            float catchUpMultiplier = math.lerp(1f, resolvedCatchUp, catchUpRatio);
            return resolvedMoveSpeed * loadSpeedMultiplier * catchUpMultiplier;
        }

        private Vector3 GetAnchorTargetPosition()
        {
            if (interactionAnchor == null)
                return _cachedTransform.position;

            if (_state == InteractionState.DraggingHeavyObject)
                return ResolveHeavyCarryTargetPosition(interactionAnchor);

            Vector3 offset = interactionAnchor.TransformDirection(SanitizePocketPickupOffset(pocketPickupAnchorOffset));
            Vector3 targetPosition = interactionAnchor.position + offset;
            return IsFiniteVector(targetPosition) ? targetPosition : _cachedTransform.position;
        }

        private Vector3 ResolveHeavyCarryTargetPosition(Transform anchor)
        {
            Vector3 anchorForward = anchor.forward;
            Vector3 planarForward = anchorForward - (Vector3.up * anchorForward.y);
            if (planarForward.sqrMagnitude < 0.0001f)
            {
                Vector3 cachedForward = _cachedTransform.forward;
                planarForward = cachedForward - (Vector3.up * cachedForward.y);
            }

            if (planarForward.sqrMagnitude < 0.0001f)
                planarForward = Vector3.forward;

            planarForward = NormalizeVectorApproxNoSqrt(planarForward, Vector3.forward);

            float load = HeavyCarryLoad01;
            float resolvedDistance = ClampFiniteRange(heavyCarryDistance, 0.5f, 6f, 2.4f);
            float rearLag = ClampFiniteRange(heavyCarryRearLagDistance, 0f, 1f, 0.24f);
            float verticalOffset = ClampFiniteRange(heavyCarryVerticalOffset, -2f, 1f, -0.34f);
            float pitchInfluence = ClampFiniteRange(heavyCarryPitchInfluence, 0f, 1f, 0.28f);
            float maxPitchOffset = ClampFiniteRange(heavyCarryMaxVerticalPitchOffset, 0f, 2f, 0.72f);
            float loadSag = ClampFiniteRange(heavyCarryLoadSag, 0f, 1.5f, 0.3f);
            float carriedDistance = math.max(0.1f, resolvedDistance - load * rearLag);
            float pitchOffset = math.clamp(anchorForward.y, -1f, 1f) * maxPitchOffset * pitchInfluence;

            Vector3 offset = planarForward * carriedDistance;
            offset.y = verticalOffset + pitchOffset - load * loadSag;

            Vector3 targetPosition = anchor.position + offset;
            return IsFiniteVector(targetPosition) ? targetPosition : _cachedTransform.position;
        }

        private static Vector3 SanitizePocketPickupOffset(Vector3 value)
        {
            if (!IsFiniteVector(value))
                return new Vector3(0f, -0.12f, 0.35f);

            return (Vector3)math.clamp(
                (float3)value,
                new float3(-MaxPocketPickupOffsetMeters),
                new float3(MaxPocketPickupOffsetMeters));
        }

        private static Vector3 SanitizeLocalScale(Vector3 value)
        {
            if (!IsFiniteVector(value))
                return Vector3.one;

            return new Vector3(
                SanitizeLocalScaleComponent(value.x),
                SanitizeLocalScaleComponent(value.y),
                SanitizeLocalScaleComponent(value.z));
        }

        private static float SanitizeLocalScaleComponent(float value)
        {
            if (!math.isfinite(value))
                return 1f;

            float sign = value < 0f ? -1f : 1f;
            float magnitude = math.clamp(math.abs(value), MinSafeLocalScaleMagnitude, MaxSafeLocalScaleMagnitude);
            return sign * magnitude;
        }

        private static bool IsFiniteVector(Vector3 value)
        {
            return !(float.IsNaN(value.x) || float.IsNaN(value.y) || float.IsNaN(value.z) ||
                     float.IsInfinity(value.x) || float.IsInfinity(value.y) || float.IsInfinity(value.z));
        }

        private static bool TryResolveAupFromRuntimeOrigin(Vector3 runtimePosition, out AbsoluteUniversePosition aup)
        {
            aup = default;
            if (!IsFiniteVector(runtimePosition))
                return false;

            AbsoluteUniversePosition originAup = RuntimeOriginRoute.CurrentRuntimeOriginAup();
            if (!originAup.IsFinite())
                return false;

            aup = AbsoluteUniversePosition.OffsetMeters(
                in originAup,
                new double3(runtimePosition.x, runtimePosition.y, runtimePosition.z));
            return aup.IsFinite();
        }

        private static float ClampInteractionDeltaTime(float deltaTime)
        {
            return math.isfinite(deltaTime) ? math.min(math.max(0f, deltaTime), MaxInteractionDeltaTime) : 0f;
        }

        private float ResolvePanelButtonProbeRadius()
        {
            return math.isfinite(panelButtonProbeRadius)
                ? math.clamp(panelButtonProbeRadius, MinPanelButtonProbeRadius, MaxPanelButtonProbeRadius)
                : 0.035f;
        }

        private static float ClampFiniteRange(float value, float min, float max, float fallback)
        {
            return math.isfinite(value) ? math.clamp(value, min, max) : fallback;
        }

        private static Vector3 NormalizeVectorApproxNoSqrt(Vector3 value, Vector3 fallback)
        {
            float lengthSq = value.sqrMagnitude;
            if (lengthSq <= 0.000001f || !IsFiniteVector(value))
                return fallback;

            return value * math.rcp(math.max(ApproximateMagnitudeNoSqrt(value), 0.000001f));
        }

        private static float ApproximateMagnitudeNoSqrt(Vector3 value)
        {
            float3 absValue = math.abs(new float3(value.x, value.y, value.z));
            float largest = math.cmax(absValue);
            float smallest = math.cmin(absValue);
            float middle = absValue.x + absValue.y + absValue.z - largest - smallest;
            return largest + (middle * 0.375f) + (smallest * 0.125f);
        }

        private void RefreshTickRegistration()
        {
            if (!Application.isPlaying || !_dispatcherAvailable)
                return;

            bool panelButtonTickReady =
                enablePhysicalPanelButtons &&
                HectonXRRuntimeState.IsXRActive &&
                _physicalHandController != null;
            bool needsTick =
                _state != InteractionState.Idle ||
                panelButtonTickReady;
            bool handControllerNeedsFixedTick =
                _physicalHandController != null &&
                (panelButtonTickReady ||
                 _state == InteractionState.DraggingHeavyObject ||
                 _physicalHandController.RequiresFixedTick);
            bool needsFixedTick =
                handControllerNeedsFixedTick ||
                _state == InteractionState.DraggingHeavyObject;
            bool needsLateFrameTick =
                _pendingPocketVisualPositionDirty ||
                _pendingPocketVisualScaleDirty ||
                _state == InteractionState.PullingPocketItem ||
                (_physicalHandController != null && _physicalHandController.RequiresLateFrameTick);

            if (needsTick && !_registeredTick)
            {
                _registeredTick = GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.Player);
            }
            else if (!needsTick && _registeredTick)
            {
                GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Player);
                _registeredTick = false;
            }

            if (needsFixedTick && !_registeredFixedTick)
            {
                _registeredFixedTick = GlobalRegistry.TryRegisterFixedTickable(this, PriorityLayer.Player);
            }
            else if (!needsFixedTick && _registeredFixedTick)
            {
                GlobalRegistry.UnregisterFixedTickable(this, PriorityLayer.Player);
                _registeredFixedTick = false;
            }

            if (needsLateFrameTick && !_registeredLateFrameTick)
            {
                _registeredLateFrameTick = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Player);
            }
            else if (!needsLateFrameTick && _registeredLateFrameTick)
            {
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Player);
                _registeredLateFrameTick = false;
            }
        }

        private void UnregisterFromTickSystems()
        {
            if (_registeredTick)
            {
                GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Player);
                _registeredTick = false;
            }

            if (_registeredFixedTick)
            {
                GlobalRegistry.UnregisterFixedTickable(this, PriorityLayer.Player);
                _registeredFixedTick = false;
            }

            if (_registeredLateFrameTick)
            {
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Player);
                _registeredLateFrameTick = false;
            }
        }

        private void TryRegisterHotSwapListener()
        {
            if (_registeredHotSwapListener || !Application.isPlaying)
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

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (UnityEditor.EditorApplication.isCompiling ||
                UnityEditor.EditorApplication.isUpdating ||
                UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            maxPocketPickupMass = ClampFiniteRange(maxPocketPickupMass, 0.1f, 80f, 18f);
            pickupDuration = ClampFiniteRange(pickupDuration, 0.05f, 1f, 0.22f);
            pickupFinalScaleMultiplier = ClampFiniteRange(pickupFinalScaleMultiplier, 0.05f, 1f, 0.2f);
            pickupMoveSpeed = ClampFiniteRange(pickupMoveSpeed, 0.5f, 40f, 10f);

            heavyCarryMinMass = ClampFiniteRange(heavyCarryMinMass, 1f, 400f, 25f);
            heavyCarryMaxMass = ClampFiniteRange(heavyCarryMaxMass, 5f, 800f, 220f);
            if (heavyCarryMaxMass < heavyCarryMinMass)
                heavyCarryMaxMass = heavyCarryMinMass;
            heavyCarryDistance = ClampFiniteRange(heavyCarryDistance, 0.5f, 6f, 2.4f);
            heavyCarryMoveSpeed = ClampFiniteRange(heavyCarryMoveSpeed, 0.25f, 12f, 2.1f);
            heavyCarryBreakDistance = ClampFiniteRange(heavyCarryBreakDistance, 1f, 12f, 5f);
            heavyCarryEnergyDrainPerSecond = ClampFiniteRange(heavyCarryEnergyDrainPerSecond, 0f, 20f, 3.5f);
            lightHeavyCarryForceMultiplier = ClampFiniteRange(lightHeavyCarryForceMultiplier, 0.1f, 1f, 0.76f);
            maxHeavyCarryForceMultiplier = ClampFiniteRange(maxHeavyCarryForceMultiplier, 0.1f, 1f, 0.42f);
            lightHeavyCarrySpeedMultiplier = ClampFiniteRange(lightHeavyCarrySpeedMultiplier, 0.1f, 1f, 0.82f);
            maxHeavyCarrySpeedMultiplier = ClampFiniteRange(maxHeavyCarrySpeedMultiplier, 0.1f, 1f, 0.52f);
            lightHeavyCarryFollowSpeedMultiplier = ClampFiniteRange(lightHeavyCarryFollowSpeedMultiplier, 0.25f, 1.5f, 1.08f);
            maxHeavyCarryFollowSpeedMultiplier = ClampFiniteRange(maxHeavyCarryFollowSpeedMultiplier, 0.25f, 1.5f, 0.72f);
            heavyCarryCatchUpSpeedMultiplier = ClampFiniteRange(heavyCarryCatchUpSpeedMultiplier, 1f, 3f, 1.65f);
            heavyCarryVerticalOffset = ClampFiniteRange(heavyCarryVerticalOffset, -2f, 1f, -0.34f);
            heavyCarryPitchInfluence = ClampFiniteRange(heavyCarryPitchInfluence, 0f, 1f, 0.28f);
            heavyCarryMaxVerticalPitchOffset = ClampFiniteRange(heavyCarryMaxVerticalPitchOffset, 0f, 2f, 0.72f);
            heavyCarryLoadSag = ClampFiniteRange(heavyCarryLoadSag, 0f, 1.5f, 0.3f);
            heavyCarryRearLagDistance = ClampFiniteRange(heavyCarryRearLagDistance, 0f, 1f, 0.24f);
            pocketPickupAnchorOffset = SanitizePocketPickupOffset(pocketPickupAnchorOffset);
            panelButtonProbeRadius = ResolvePanelButtonProbeRadius();
            floraHarvestSnapSearchRadius = ClampFiniteRange(floraHarvestSnapSearchRadius, 0.1f, 2f, 1.25f);
            floraHarvestSnapDuration = ClampFiniteRange(floraHarvestSnapDuration, 0.05f, 0.6f, 0.18f);
            RefreshPanelButtonLayerMask();
        }
#endif
    }
}
