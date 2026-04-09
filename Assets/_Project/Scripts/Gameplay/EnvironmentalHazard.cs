// ============================================================================
// HECTON-8 — EnvironmentalHazard.cs
// Base class for environmental hazards like radiation, toxins.
// ============================================================================

using System.Collections.Generic;
using Hecton8.Core;
using UnityEngine;

namespace Hecton8.Gameplay
{
    /// <summary>Base class for environmental hazards that affect players within their area.</summary>
    public abstract class EnvironmentalHazard : MonoBehaviour
    {
        /// <summary>Damage per second applied to affected entities.</summary>
        [Header("Hazard Settings")]
        [Tooltip("Continuous damage applied while the player stays inside the hazard volume.")]
        [SerializeField] protected float damagePerSecond = 1f;

        /// <summary>Radius of the hazard effect.</summary>
        [Tooltip("Editor visualization radius for the hazard volume.")]
        [SerializeField] protected float effectRadius = 5f;

        /// <summary>Layers that are affected by this hazard.</summary>
        [Tooltip("Only colliders on these layers can receive hazard damage.")]
        [SerializeField] protected LayerMask affectedLayers = ~0;

        // COLD ALLOC: small overlap-safe player tracking for nested colliders inside a hazard volume.
        private readonly List<HectonPlayerHealth> _trackedHealths = new List<HectonPlayerHealth>(2);
        // COLD ALLOC: collider reference counts matched to _trackedHealths.
        private readonly List<int> _trackedColliderCounts = new List<int>(2);

        /// <summary>Called when an object stays within the trigger.</summary>
        /// <param name="other">The collider that stayed.</param>
        protected virtual void OnTriggerStay(Collider other)
        {
            if (!TryResolveHealth(other, out HectonPlayerHealth health))
                return;

            ApplyHazardEffect(health);
        }

        /// <summary>Called when an object enters the trigger.</summary>
        /// <param name="other">The collider that entered.</param>
        protected virtual void OnTriggerEnter(Collider other)
        {
            if (!TryResolveHealth(other, out HectonPlayerHealth health))
                return;

            if (TryBeginTracking(health))
                OnEnterHazard(health);
        }

        /// <summary>Called when an object exits the trigger.</summary>
        /// <param name="other">The collider that exited.</param>
        protected virtual void OnTriggerExit(Collider other)
        {
            if (!TryResolveHealth(other, out HectonPlayerHealth health))
                return;

            if (TryEndTracking(health))
                OnExitHazard(health);
        }

        /// <summary>Flushes tracked hazard occupants when the hazard is disabled mid-overlap.</summary>
        protected virtual void OnDisable()
        {
            for (int i = 0; i < _trackedHealths.Count; i++)
            {
                HectonPlayerHealth health = _trackedHealths[i];
                if (health != null)
                    OnExitHazard(health);
            }

            _trackedHealths.Clear();
            _trackedColliderCounts.Clear();
        }

        /// <summary>Applies the hazard effect to the health component.</summary>
        /// <param name="health">The health component to affect.</param>
        protected virtual void ApplyHazardEffect(HectonPlayerHealth health)
        {
            health.TakeDamage(damagePerSecond * Time.deltaTime);
        }

        /// <summary>Called when entering the hazard area.</summary>
        /// <param name="health">The player health that entered.</param>
        protected virtual void OnEnterHazard(HectonPlayerHealth health)
        {
            // Override for enter effects
        }

        /// <summary>Called when exiting the hazard area.</summary>
        /// <param name="health">The player health that exited.</param>
        protected virtual void OnExitHazard(HectonPlayerHealth health)
        {
            // Override for exit effects
        }

        /// <summary>Resolves a valid player health component for a trigger collider.</summary>
        /// <param name="other">The collider that touched the hazard.</param>
        /// <param name="health">Resolved player health.</param>
        /// <returns>True when the collider belongs to an affected player target.</returns>
        private bool TryResolveHealth(Collider other, out HectonPlayerHealth health)
        {
            health = null;

            if (other == null || !IsAffectedLayer(other.gameObject.layer))
                return false;

            health = other.GetComponentInParent<HectonPlayerHealth>();
            return health != null;
        }

        /// <summary>Starts overlap tracking for a player and reports true only on the first collider entry.</summary>
        /// <param name="health">The player health component.</param>
        /// <returns>True when this is the first active overlap for the player.</returns>
        private bool TryBeginTracking(HectonPlayerHealth health)
        {
            for (int i = 0; i < _trackedHealths.Count; i++)
            {
                if (_trackedHealths[i] != health)
                    continue;

                _trackedColliderCounts[i]++;
                return false;
            }

            _trackedHealths.Add(health);
            _trackedColliderCounts.Add(1);
            return true;
        }

        /// <summary>Ends overlap tracking for a player and reports true only on the final collider exit.</summary>
        /// <param name="health">The player health component.</param>
        /// <returns>True when the player fully left the hazard volume.</returns>
        private bool TryEndTracking(HectonPlayerHealth health)
        {
            for (int i = 0; i < _trackedHealths.Count; i++)
            {
                if (_trackedHealths[i] != health)
                    continue;

                int remaining = _trackedColliderCounts[i] - 1;
                if (remaining > 0)
                {
                    _trackedColliderCounts[i] = remaining;
                    return false;
                }

                _trackedHealths.RemoveAt(i);
                _trackedColliderCounts.RemoveAt(i);
                return true;
            }

            return false;
        }

        /// <summary>Checks if the layer is affected by this hazard.</summary>
        /// <param name="layer">The layer to check.</param>
        /// <returns>True if affected.</returns>
        private bool IsAffectedLayer(int layer)
        {
            return (affectedLayers & (1 << layer)) != 0;
        }

        /// <summary>Draws gizmos for the hazard radius.</summary>
#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, effectRadius);
        }
#endif
    }
}
