// ============================================================================
// HECTON-8 — SargassumPhysicsZone.cs
// Sticky-drag trigger volume for dense sargassum clusters.
// ============================================================================

namespace Hecton8.Gameplay
{
    using Unity.Mathematics;
    using UnityEngine;

    /// <summary>
    /// Applies localized sticky drag to bodies moving through a sargassum cluster.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    [AddComponentMenu("Hecton8/Gameplay/Sargassum Physics Zone")]
    public sealed class SargassumPhysicsZone : MonoBehaviour
    {
        [Header("── Sticky Drag ─────────────────────────")]
        [Tooltip("Target max-speed multiplier while fully inside the sargassum mass.")]
        [SerializeField, Range(0.3f, 1f)] private float speedMultiplier = 0.68f;

        [Tooltip("Target drag multiplier while fully inside the sargassum mass.")]
        [SerializeField, Range(1f, 4f)] private float dragMultiplier = 2.15f;

        [Header("── Cut Response ───────────────────────")]
        [Tooltip("Optional responder that updates shader masking and debris bursts when a fast rigidbody cuts through the bush.")]
        [SerializeField] private SargassumCutResponder cutResponder;

        [Tooltip("Minimum rigidbody speed that counts as a cutting pass through the sargassum.")]
        [SerializeField, Range(0.1f, 20f)] private float cutSpeedThreshold = 3.6f;

        [Tooltip("World-space radius passed into the cut responder.")]
        [SerializeField, Range(0.1f, 4f)] private float cutRadius = 0.85f;

        [Header("── Diagnostics ─────────────────────────")]
        [SerializeField] private int _debugInfluencedBodies;

        private Collider _triggerCollider;

        /// <summary>
        /// Configures the sticky-drag and cut-response defaults for this zone.
        /// </summary>
        /// <param name="responder">Optional cluster cut responder.</param>
        /// <param name="targetSpeedMultiplier">Sticky max-speed multiplier.</param>
        /// <param name="targetDragMultiplier">Sticky drag multiplier.</param>
        /// <param name="targetCutSpeedThreshold">Minimum cutter speed.</param>
        /// <param name="targetCutRadius">Responder cut radius.</param>
        public void Configure(
            SargassumCutResponder responder,
            float targetSpeedMultiplier,
            float targetDragMultiplier,
            float targetCutSpeedThreshold,
            float targetCutRadius)
        {
            cutResponder = responder;
            speedMultiplier = math.clamp(targetSpeedMultiplier, 0.1f, 1f);
            dragMultiplier = math.max(1f, targetDragMultiplier);
            cutSpeedThreshold = math.max(0.1f, targetCutSpeedThreshold);
            cutRadius = math.max(0.1f, targetCutRadius);
        }

        private void Reset()
        {
            _triggerCollider = GetComponent<Collider>();
            if (_triggerCollider != null)
                _triggerCollider.isTrigger = true;
        }

        private void Awake()
        {
            _triggerCollider = GetComponent<Collider>();
            if (_triggerCollider != null)
                _triggerCollider.isTrigger = true;
        }

        private void OnDisable()
        {
            _debugInfluencedBodies = 0;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!TryResolveInfluence(other, out SargassumMovementInfluence influence))
                return;

            influence.EnterZone(speedMultiplier, dragMultiplier);
            _debugInfluencedBodies++;
            TryRegisterCut(other);
        }

        private void OnTriggerStay(Collider other)
        {
            if (!TryResolveInfluence(other, out SargassumMovementInfluence influence))
                return;

            influence.StayZone(speedMultiplier, dragMultiplier);
            TryRegisterCut(other);
        }

        private void OnTriggerExit(Collider other)
        {
            if (!TryResolveInfluence(other, out SargassumMovementInfluence influence))
                return;

            influence.ExitZone();
            if (_debugInfluencedBodies > 0)
                _debugInfluencedBodies--;
        }

        private bool TryResolveInfluence(Collider other, out SargassumMovementInfluence influence)
        {
            influence = null;
            if (other == null || other.isTrigger)
                return false;

            Rigidbody attachedBody = other.attachedRigidbody;
            if (attachedBody != null)
            {
                if (attachedBody.TryGetComponent(out influence))
                    return true;

                return attachedBody.TryGetComponent(out HectonPlayerMovement _) &&
                       attachedBody.TryGetComponent(out influence);
            }

            if (other.TryGetComponent(out influence))
                return true;

            return other.TryGetComponent(out HectonPlayerMovement _) &&
                   other.TryGetComponent(out influence);
        }

        private void TryRegisterCut(Collider other)
        {
            if (cutResponder == null || other == null)
                return;

            Rigidbody attachedBody = other.attachedRigidbody;
            if (attachedBody == null)
                return;

            Vector3 velocity = attachedBody.linearVelocity;
            float speedSq = velocity.sqrMagnitude;
            float cutSpeedThresholdSq = cutSpeedThreshold * cutSpeedThreshold;
            if (speedSq < cutSpeedThresholdSq)
                return;

            float speed = speedSq * math.rsqrt(speedSq);
            Vector3 contactPoint = other.ClosestPoint(transform.position);
            cutResponder.RegisterCut(contactPoint, velocity, speed);
        }
    }
}
