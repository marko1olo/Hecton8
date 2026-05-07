// ============================================================================
// HECTON-8 — PhysicalInteractionHandler.cs
// Player-owned interaction layer for physical pocket pickups and heavy cargo drag.
// ============================================================================

namespace Hecton8.Interaction
{
    using Hecton8.Core;
    using Hecton8.Gameplay;
    using Hecton8.Items;
    using Hecton8.UI;
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
        bool TryQueueHandPress(
            Vector3 handPosition,
            Vector3 handForward,
            IInteractionSignalService interactionSignals,
            Collider handSourceCollider,
            PhysicalHandSide fallbackHandSide);
    }

    /// <summary>
    /// Owns physical interaction sequences that should happen before inventory insertion
    /// or while dragging heavy rigidbody cargo in front of the player.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/Interaction/Physical Interaction Handler")]
    public sealed class PhysicalInteractionHandler : MonoBehaviour, ITickable, IFixedTickable, ILateFrameTickable
    {
        private enum InteractionState : byte
        {
            Idle,
            PullingPocketItem,
            DraggingHeavyObject
        }

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
        private InteractionState _state;
        private bool _registeredTick;
        private bool _registeredFixedTick;
        private bool _registeredLateFrameTick;
        private float _stateTimer;
        private Vector3 _pullSmoothDampVelocity;
        private const int MaxPanelButtonOverlaps = 8;
        private static readonly int _DefaultPanelButtonLayerMask =
            HectonLayerMasks.UILayerMask |
            HectonLayerMasks.InteractableLayerMask;
        private readonly Collider[] _panelButtonOverlaps = new Collider[MaxPanelButtonOverlaps]; // COLD ALLOC: Collider[8] - physical panel button overlap buffer - owner: PhysicalInteractionHandler

        private IInteractable _activeInteractable;
        private MonoBehaviour _activeBehaviour;
        private Transform _activeTargetTransform;
        private Rigidbody _activeBody;
        private Collider _activeCollider;
        private HeavyCarryInteractable _activeHeavyCarry;
        private Vector3 _activeOriginalLocalScale;
        private Vector3 _activeTargetLocalScale;
        private bool _activeBodyWasKinematic;
        private bool _activeBodyDetectCollisions;
        private bool _activeColliderWasEnabled;
        private float _activeHeavyCarryMass;

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

                float massRange = math.max(heavyCarryMaxMass - heavyCarryMinMass, 0.01f);
                return math.saturate((_activeHeavyCarryMass - heavyCarryMinMass) / massRange);
            }
        }

        private void Awake()
        {
            _cachedTransform = transform;

            if (survivalSystem == null)
                TryGetComponent(out survivalSystem);

            if (interactionAnchor == null)
            {
                IPlayerRuntimeContext playerContext = GlobalRegistry.Player;
                _playerCamera = playerContext != null ? playerContext.PlayerCamera : null;
                if (_playerCamera == null)
                    TryGetComponent(out _playerCamera);
                if (_playerCamera == null)
                    _playerCamera = GetComponentInParent<Camera>();

                if (_playerCamera != null)
                    interactionAnchor = _playerCamera.transform;
            }

            TryGetComponent(out _physicalHandController);
            if (enablePhysicalPanelButtons)
                EnsurePhysicalHandController();
        }

        private void OnEnable()
        {
            RegisterToTickSystems();
        }

        private void OnDisable()
        {
            CancelActiveInteraction();
            UnregisterFromTickSystems();
        }

        /// <summary>
        /// Attempts to intercept a normal interaction and route it into the physical interaction layer.
        /// Returns true only when the interaction has been consumed by this handler.
        /// </summary>
        public bool TryHandleInteraction(IInteractable interactable, Transform interactor)
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

            MonoBehaviour behaviour = interactable as MonoBehaviour;
            if (behaviour == null)
                return false;

            if (TryBeginPocketPickup(interactable, behaviour))
                return true;

            if (TryBeginHeavyCarry(interactable, behaviour))
                return true;

            return false;
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
                    TickPocketPickup(deltaTime);
                    break;

                case InteractionState.DraggingHeavyObject:
                    TickHeavyCarry(deltaTime);
                    break;
            }
        }

        public void FixedTick(float fixedDeltaTime)
        {
            if (_physicalHandController != null)
            {
                Vector3 controllerPosition = GetAnchorTargetPosition();
                Quaternion controllerRotation = interactionAnchor != null ? interactionAnchor.rotation : _cachedTransform.rotation;
                _physicalHandController.StepFixed(fixedDeltaTime, controllerPosition, controllerRotation);
            }

            if (_state == InteractionState.Idle)
                return;

            switch (_state)
            {
                case InteractionState.PullingPocketItem:
                    FixedTickPocketPickup(fixedDeltaTime);
                    break;

                case InteractionState.DraggingHeavyObject:
                    FixedTickHeavyCarry(fixedDeltaTime);
                    break;
            }
        }

        /// <summary>
        /// Finalizes deferred hand-probe jobs in the dispatcher-owned late-frame swap phase.
        /// </summary>
        public void LateFrameTick()
        {
            if (_physicalHandController != null)
                _physicalHandController.LateFrameTick();
        }

        private void TickPhysicalPanelButtons()
        {
            if (!enablePhysicalPanelButtons || _physicalHandController == null)
                return;

            if (!_physicalHandController.TryGetInteractionProbePose(out Vector3 handPosition, out Quaternion handRotation))
                return;

            IInteractionSignalService interactionSignals = GlobalRegistry.InteractionSignals;
            if (interactionSignals == null || !interactionSignals.IsInitialized)
                return;

            Collider handSourceCollider = null;
            PhysicalHandSide handSide = _physicalHandController.HandSide;
            _physicalHandController.TryGetInteractionProbeCollider(out handSourceCollider);

            int hitCount = Physics.OverlapSphereNonAlloc(
                handPosition,
                panelButtonProbeRadius,
                _panelButtonOverlaps,
                ResolvePanelButtonLayerMask(),
                QueryTriggerInteraction.Collide);
            if (hitCount <= 0)
                return;

            Vector3 handForward = handRotation * Vector3.forward;
            for (int i = 0; i < hitCount && i < _panelButtonOverlaps.Length; i++)
            {
                Collider candidate = _panelButtonOverlaps[i];
                _panelButtonOverlaps[i] = null;
                if (candidate == null)
                    continue;

                if (!PhysicalHandReceiverRegistry.TryResolve(candidate, out IPhysicalPanelButtonReceiver button) &&
                    !PhysicalPanelButton.TryResolve(candidate, out button))
                    continue;

                button.TryQueueHandPress(handPosition, handForward, interactionSignals, handSourceCollider, handSide);
            }
        }

        private int ResolvePanelButtonLayerMask()
        {
            int mask = panelButtonMask.value;
            return HectonLayerMasks.IsEverythingLayerMask(mask) ? _DefaultPanelButtonLayerMask : mask;
        }

        private bool TryBeginPocketPickup(IInteractable interactable, MonoBehaviour behaviour)
        {
            if (!behaviour.TryGetComponent<PickupItem>(out _) &&
                !behaviour.TryGetComponent<HectonItem>(out _))
            {
                return false;
            }

            Rigidbody body = behaviour.GetComponent<Rigidbody>();
            if (body == null)
                body = behaviour.GetComponentInParent<Rigidbody>();

            if (body != null && body.mass > maxPocketPickupMass)
                return false;

            Collider targetCollider = behaviour.GetComponent<Collider>();
            if (targetCollider == null)
                TryResolveOwnedComponent(behaviour.transform, out targetCollider);

            _activeInteractable = interactable;
            _activeBehaviour = behaviour;
            _activeTargetTransform = behaviour.transform;
            _activeBody = body;
            _activeCollider = targetCollider;
            _activeHeavyCarry = null;
            _activeOriginalLocalScale = _activeTargetTransform.localScale;
            _activeTargetLocalScale = _activeOriginalLocalScale * pickupFinalScaleMultiplier;
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
                _activeBody.linearVelocity = Vector3.zero;
                _activeBody.angularVelocity = Vector3.zero;
                _activeBody.isKinematic = true;
                _activeBody.detectCollisions = false;
            }

            _state = InteractionState.PullingPocketItem;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _debugState = "PullingPocketItem";
            CacheDebugTargetName(_activeBehaviour);
#endif
            return true;
        }

        private static bool TryResolveOwnedComponent<T>(Transform root, out T component) where T : Component
        {
            component = null;
            if (root == null)
                return false;

            if (root.TryGetComponent(out component))
                return true;

            for (int i = 0; i < root.childCount; i++)
            {
                if (TryResolveOwnedComponent(root.GetChild(i), out component))
                    return true;
            }

            return false;
        }

        private bool TryBeginHeavyCarry(IInteractable interactable, MonoBehaviour behaviour)
        {
            if (!behaviour.TryGetComponent(out HeavyCarryInteractable heavyCarry))
                return false;

            if (!heavyCarry.TryGetCarryBody(out Rigidbody carryBody) || carryBody == null)
                return false;

            if (carryBody.isKinematic ||
                carryBody.mass < heavyCarryMinMass ||
                carryBody.mass > heavyCarryMaxMass)
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
            _activeOriginalLocalScale = _activeTargetTransform.localScale;
            _activeTargetLocalScale = _activeOriginalLocalScale;
            _stateTimer = 0f;
            _pullSmoothDampVelocity = Vector3.zero;
            _activeHeavyCarryMass = carryBody.mass;

            _state = InteractionState.DraggingHeavyObject;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _debugState = "DraggingHeavyObject";
            CacheDebugTargetName(_activeBehaviour);
#endif
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

            _physicalHandController = gameObject.AddComponent<PhysicalHandController>(); // COLD ALLOC: PhysicalHandController[1] — heavy-object articulation grab proxy — owner: PhysicalInteractionHandler
            return _physicalHandController != null;
        }

        private void TickPocketPickup(float deltaTime)
        {
            _stateTimer += deltaTime;

            float duration = pickupDuration > 0.01f ? pickupDuration : 0.01f;
            float progress = math.saturate(_stateTimer / duration);
            _activeTargetTransform.localScale = (Vector3)math.lerp((float3)_activeOriginalLocalScale, (float3)_activeTargetLocalScale, progress);

            if (_activeBody == null && interactionAnchor != null)
            {
                Vector3 targetPosition = GetAnchorTargetPosition();
                Vector3 currentPosition = _activeTargetTransform.position;
                if (!IsFiniteVector(targetPosition) || !IsFiniteVector(currentPosition))
                    return;

                Vector3 nextPosition = Vector3.SmoothDamp(
                    currentPosition,
                    targetPosition,
                    ref _pullSmoothDampVelocity,
                    duration,
                    pickupMoveSpeed);
                if (!IsFiniteVector(nextPosition))
                    return;

                _activeTargetTransform.position = nextPosition;
            }

            if (progress >= 1f)
                CompletePocketPickup();
        }

        private void FixedTickPocketPickup(float fixedDeltaTime)
        {
            if (_activeBody == null || interactionAnchor == null)
                return;

            Vector3 targetPosition = GetAnchorTargetPosition();
            Vector3 currentPosition = _activeBody.position;
            if (!IsFiniteVector(targetPosition) || !IsFiniteVector(currentPosition))
                return;

            Vector3 nextPosition = Vector3.MoveTowards(currentPosition, targetPosition, pickupMoveSpeed * fixedDeltaTime);
            if (!IsFiniteVector(nextPosition))
                return;

            _activeBody.MovePosition(nextPosition);
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

                AbsoluteUniversePosition anchorAup = AbsoluteUniversePosition.FromRuntimePosition(interactionAnchor.position);
                AbsoluteUniversePosition bodyAup = AbsoluteUniversePosition.FromRuntimePosition(_activeBody.worldCenterOfMass);
                if (AbsoluteUniversePosition.DistanceSq(in anchorAup, in bodyAup) > heavyCarryBreakDistance * heavyCarryBreakDistance)
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
                survivalSystem.DrainEnergy(heavyCarryEnergyDrainPerSecond * deltaTime);
                if (survivalSystem.Energy <= 0.01f)
                {
                    CancelActiveInteraction();
                    return;
                }
            }
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
                RestorePocketPickupState();

            ClearActiveState();
        }

        private void RestorePocketPickupState()
        {
            if (_activeTargetTransform != null)
                _activeTargetTransform.localScale = _activeOriginalLocalScale;

            if (_activeCollider != null)
                _activeCollider.enabled = _activeColliderWasEnabled;

            if (_activeBody != null)
            {
                _activeBody.isKinematic = _activeBodyWasKinematic;
                _activeBody.detectCollisions = _activeBodyDetectCollisions;
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
            _activeHeavyCarry = null;
            _activeHeavyCarryMass = 0f;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _debugState = "Idle";
            _debugTargetName = null;
#endif
        }

        /// <summary>
        /// Resolves the player movement-force multiplier imposed by the current heavy carry load.
        /// </summary>
        public float ResolveHeavyCarryForceMultiplier()
        {
            if (!IsDraggingHeavyObject)
                return 1f;

            return math.lerp(lightHeavyCarryForceMultiplier, maxHeavyCarryForceMultiplier, HeavyCarryLoad01);
        }

        /// <summary>
        /// Resolves the player max-speed multiplier imposed by the current heavy carry load.
        /// </summary>
        public float ResolveHeavyCarrySpeedMultiplier()
        {
            if (!IsDraggingHeavyObject)
                return 1f;

            return math.lerp(lightHeavyCarrySpeedMultiplier, maxHeavyCarrySpeedMultiplier, HeavyCarryLoad01);
        }

        private float ResolveHeavyCarryFollowSpeed(float separationDistance)
        {
            float loadSpeedMultiplier = math.lerp(
                lightHeavyCarryFollowSpeedMultiplier,
                maxHeavyCarryFollowSpeedMultiplier,
                HeavyCarryLoad01);

            float catchUpRatio = math.saturate(separationDistance / math.max(heavyCarryDistance, 0.01f));
            float catchUpMultiplier = math.lerp(1f, heavyCarryCatchUpSpeedMultiplier, catchUpRatio);
            return heavyCarryMoveSpeed * loadSpeedMultiplier * catchUpMultiplier;
        }

        private Vector3 GetAnchorTargetPosition()
        {
            if (interactionAnchor == null)
                return _cachedTransform.position;

            Vector3 offset = interactionAnchor.TransformDirection(pocketPickupAnchorOffset);
            if (_state == InteractionState.DraggingHeavyObject)
            {
                Vector3 planarForward = Vector3.ProjectOnPlane(interactionAnchor.forward, Vector3.up);
                if (planarForward.sqrMagnitude < 0.0001f)
                    planarForward = Vector3.ProjectOnPlane(_cachedTransform.forward, Vector3.up);
                if (planarForward.sqrMagnitude < 0.0001f)
                    planarForward = Vector3.forward;

                planarForward = (Vector3)math.normalizesafe((float3)planarForward, new float3(0f, 0f, 1f));

                float load = HeavyCarryLoad01;
                float carriedDistance = math.max(0.1f, heavyCarryDistance - load * heavyCarryRearLagDistance);
                float pitchOffset = math.clamp(interactionAnchor.forward.y, -1f, 1f) * heavyCarryMaxVerticalPitchOffset * heavyCarryPitchInfluence;

                offset = planarForward * carriedDistance;
                offset.y = heavyCarryVerticalOffset + pitchOffset - load * heavyCarryLoadSag;
            }

            Vector3 targetPosition = interactionAnchor.position + offset;
            return IsFiniteVector(targetPosition) ? targetPosition : _cachedTransform.position;
        }

        private static bool IsFiniteVector(Vector3 value)
        {
            return !(float.IsNaN(value.x) || float.IsNaN(value.y) || float.IsNaN(value.z) ||
                     float.IsInfinity(value.x) || float.IsInfinity(value.y) || float.IsInfinity(value.z));
        }

        private void RegisterToTickSystems()
        {
            if (!Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            if (!_registeredTick)
            {
                GlobalRegistry.RegisterUpdatable(this, PriorityLayer.Player);
                _registeredTick = GlobalRegistry.Updatables.Contains(this);
            }

            if (!_registeredFixedTick)
            {
                GlobalRegistry.RegisterFixedTickable(this, PriorityLayer.Player);
                _registeredFixedTick = GlobalRegistry.FixedTickables.Contains(this);
            }

            if (!_registeredLateFrameTick)
            {
                GlobalRegistry.RegisterLateFrameTickable(this, PriorityLayer.Player);
                _registeredLateFrameTick = SystemDispatcher.GetLateFrameLane(PriorityLayer.Player).Contains(this);
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

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (UnityEditor.EditorApplication.isCompiling ||
                UnityEditor.EditorApplication.isUpdating ||
                UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            if (pickupDuration < 0.05f)
                pickupDuration = 0.05f;

            if (heavyCarryMaxMass < heavyCarryMinMass)
                heavyCarryMaxMass = heavyCarryMinMass;
            if (lightHeavyCarryFollowSpeedMultiplier < 0.25f)
                lightHeavyCarryFollowSpeedMultiplier = 0.25f;
            if (maxHeavyCarryFollowSpeedMultiplier < 0.25f)
                maxHeavyCarryFollowSpeedMultiplier = 0.25f;
            if (heavyCarryCatchUpSpeedMultiplier < 1f)
                heavyCarryCatchUpSpeedMultiplier = 1f;
            if (heavyCarryMaxVerticalPitchOffset < 0f)
                heavyCarryMaxVerticalPitchOffset = 0f;
            if (heavyCarryLoadSag < 0f)
                heavyCarryLoadSag = 0f;
            if (heavyCarryRearLagDistance < 0f)
                heavyCarryRearLagDistance = 0f;
        }
#endif
    }
}
