// ============================================================================
// HECTON-8 — PhysicalInteractionHandler.cs
// Player-owned interaction layer for physical pocket pickups and heavy cargo drag.
// ============================================================================

namespace Hecton8.Interaction
{
    using Hecton8.Core;
    using Hecton8.Gameplay;
    using Hecton8.Items;
    using UnityEngine;

    /// <summary>
    /// Owns physical interaction sequences that should happen before inventory insertion
    /// or while dragging heavy rigidbody cargo in front of the player.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/Interaction/Physical Interaction Handler")]
    public sealed class PhysicalInteractionHandler : MonoBehaviour, ITickable, IFixedTickable
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

        [Header("── Diagnostics ──────────────────")]
        [SerializeField] private string _debugState = "Idle";
        [SerializeField] private string _debugTargetName;

        private Transform _cachedTransform;
        private Camera _playerCamera;
        private InteractionState _state;
        private bool _registeredTick;
        private bool _registeredFixedTick;
        private float _stateTimer;
        private Vector3 _pullSmoothDampVelocity;

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

        private void Awake()
        {
            _cachedTransform = transform;

            if (survivalSystem == null)
                TryGetComponent(out survivalSystem);

            if (interactionAnchor == null)
            {
                _playerCamera = GetComponentInChildren<Camera>(true);
                if (_playerCamera != null)
                    interactionAnchor = _playerCamera.transform;
            }
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
            else if (_state == InteractionState.DraggingHeavyObject && _activeHeavyCarry != null)
                _activeHeavyCarry.SetDraggedState(false);

            ClearActiveState();
        }

        public void Tick(float deltaTime)
        {
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
                targetCollider = behaviour.GetComponentInChildren<Collider>(true);

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
            _debugState = "PullingPocketItem";
            _debugTargetName = _activeBehaviour.gameObject.name;
            return true;
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
            _activeHeavyCarry.SetDraggedState(true);

            _state = InteractionState.DraggingHeavyObject;
            _debugState = "DraggingHeavyObject";
            _debugTargetName = _activeBehaviour.gameObject.name;
            return true;
        }

        private void TickPocketPickup(float deltaTime)
        {
            _stateTimer += deltaTime;

            float duration = pickupDuration > 0.01f ? pickupDuration : 0.01f;
            float progress = Mathf.Clamp01(_stateTimer / duration);
            _activeTargetTransform.localScale = Vector3.Lerp(_activeOriginalLocalScale, _activeTargetLocalScale, progress);

            if (_activeBody == null && interactionAnchor != null)
            {
                Vector3 targetPosition = GetAnchorTargetPosition();
                Vector3 nextPosition = Vector3.SmoothDamp(
                    _activeTargetTransform.position,
                    targetPosition,
                    ref _pullSmoothDampVelocity,
                    duration,
                    pickupMoveSpeed);
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
            Vector3 nextPosition = Vector3.MoveTowards(currentPosition, targetPosition, pickupMoveSpeed * fixedDeltaTime);
            _activeBody.MovePosition(nextPosition);
        }

        private void TickHeavyCarry(float deltaTime)
        {
            if (_activeBody == null || interactionAnchor == null)
            {
                CancelActiveInteraction();
                return;
            }

            Vector3 separation = interactionAnchor.position - _activeBody.worldCenterOfMass;
            if (separation.sqrMagnitude > heavyCarryBreakDistance * heavyCarryBreakDistance)
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
            if (_activeBody == null || interactionAnchor == null)
                return;

            Vector3 targetPosition = GetAnchorTargetPosition();
            Vector3 currentPosition = _activeBody.position;
            Vector3 nextPosition = Vector3.MoveTowards(currentPosition, targetPosition, heavyCarryMoveSpeed * fixedDeltaTime);
            _activeBody.MovePosition(nextPosition);
            _activeBody.angularVelocity = Vector3.zero;
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
            _debugState = "Idle";
            _debugTargetName = null;
        }

        private Vector3 GetAnchorTargetPosition()
        {
            if (interactionAnchor == null)
                return _cachedTransform.position;

            Vector3 offset = interactionAnchor.TransformDirection(pocketPickupAnchorOffset);
            if (_state == InteractionState.DraggingHeavyObject)
            {
                offset = interactionAnchor.forward * heavyCarryDistance;
            }

            return interactionAnchor.position + offset;
        }

        private void RegisterToTickSystems()
        {
            if (!_registeredTick && GameTickManager.Instance != null)
            {
                GameTickManager.Instance.Register((ITickable)this);
                _registeredTick = true;
            }

            if (!_registeredFixedTick && GameTickManager.Instance != null)
            {
                GameTickManager.Instance.Register((IFixedTickable)this);
                _registeredFixedTick = true;
            }
        }

        private void UnregisterFromTickSystems()
        {
            if (_registeredTick && GameTickManager.Instance != null)
            {
                GameTickManager.Instance.Unregister((ITickable)this);
                _registeredTick = false;
            }

            if (_registeredFixedTick && GameTickManager.Instance != null)
            {
                GameTickManager.Instance.Unregister((IFixedTickable)this);
                _registeredFixedTick = false;
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
        }
#endif
    }
}
