// ============================================================================
// HECTON-8 — EnvironmentalHazard.cs
// Base class for environmental hazards like radiation, toxins.
// ============================================================================

using Hecton8.Core;
using UnityEngine;

namespace Hecton8.Gameplay
{
    /// <summary>Base class for environmental hazards that affect players within their area.</summary>
    public abstract class EnvironmentalHazard : MonoBehaviour
    {
        /// <summary>Damage per second applied to affected entities.</summary>
        [Header("Hazard Settings")]
        [SerializeField] protected float damagePerSecond = 1f;

        /// <summary>Radius of the hazard effect.</summary>
        [SerializeField] protected float effectRadius = 5f;

        /// <summary>Layers that are affected by this hazard.</summary>
        [SerializeField] protected LayerMask affectedLayers = ~0;

        /// <summary>Called when an object stays within the trigger.</summary>
        /// <param name="other">The collider that stayed.</param>
        protected virtual void OnTriggerStay(Collider other)
        {
            if (!IsAffectedLayer(other.gameObject.layer))
                return;

            var health = other.GetComponentInParent<HectonPlayerHealth>();
            if (health != null)
            {
                ApplyHazardEffect(health);
            }
        }

        /// <summary>Called when an object enters the trigger.</summary>
        /// <param name="other">The collider that entered.</param>
        protected virtual void OnTriggerEnter(Collider other)
        {
            if (!IsAffectedLayer(other.gameObject.layer))
                return;

            OnEnterHazard(other);
        }

        /// <summary>Called when an object exits the trigger.</summary>
        /// <param name="other">The collider that exited.</param>
        protected virtual void OnTriggerExit(Collider other)
        {
            if (!IsAffectedLayer(other.gameObject.layer))
                return;

            OnExitHazard(other);
        }

        /// <summary>Applies the hazard effect to the health component.</summary>
        /// <param name="health">The health component to affect.</param>
        protected virtual void ApplyHazardEffect(HectonPlayerHealth health)
        {
            health.TakeDamage(damagePerSecond * Time.deltaTime);
        }

        /// <summary>Called when entering the hazard area.</summary>
        /// <param name="other">The collider that entered.</param>
        protected virtual void OnEnterHazard(Collider other)
        {
            // Override for enter effects
        }

        /// <summary>Called when exiting the hazard area.</summary>
        /// <param name="other">The collider that exited.</param>
        protected virtual void OnExitHazard(Collider other)
        {
            // Override for exit effects
        }

        /// <summary>Checks if the layer is affected by this hazard.</summary>
        /// <param name="layer">The layer to check.</param>
        /// <returns>True if affected.</returns>
        private bool IsAffectedLayer(int layer)
        {
            return (affectedLayers & (1 << layer)) != 0;
        }

        /// <summary>Draws gizmos for the hazard radius.</summary>
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, effectRadius);
        }
    }
}