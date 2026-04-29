// ============================================================================
// HECTON-8 — GravTrap.cs
// Deployable gravity trap that pulls small objects and fish towards it.
//
// ARCHITECTURE:
//   • Standalone prop — uses ISlowTickable for physics checks.
//   • ITickable for visual rotation.
//   • Physics.OverlapSphereNonAlloc for zero-GC object detection.
//   • Configurable pull force with damping near center.
//
// ZERO GC:
//   • ISlowTickable.SlowTick() — called ~2x per second.
//   • ITickable.Tick() — for smooth rotation.
//   • Pre-allocated Collider[] buffer for OverlapSphereNonAlloc.
//   • Cached Transform, Rigidbody.
//
// USAGE:
//   1. Place on trap GameObject with visual mesh and collider.
//   2. Configure pull radius and force.
//   3. Assign spinning mesh and light/particle effects.
// ============================================================================

using Hecton8.Audio;
using Hecton8.Core;
using Hecton8.Physics;
using UnityEngine;

namespace Hecton8.Gameplay
{
    /// <summary>
    /// Deployable gravity trap that pulls nearby objects towards it.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GravTrap : MonoBehaviour, ITickable, IUpdatable, ISlowTickable
    {
        // ══════════════════════════════════════════════════════════
        //  INSPECTOR — PULL SETTINGS
        // ══════════════════════════════════════════════════════════

        [Header("── Pull Settings ──────────────────────────────")]
        [Tooltip("Radius within which objects are pulled.")]
        [SerializeField, Range(1f, 20f)] private float pullRadius = 8f;

        [Tooltip("Maximum force applied to pulled objects.")]
        [SerializeField, Range(0.1f, 50f)] private float pullForce = 10f;

        [Tooltip("Distance at which force is dampened (objects orbit instead of jitter).")]
        [SerializeField, Range(0.5f, 5f)] private float dampenDistance = 1.5f;

        [Tooltip("Layers affected by the gravity pull.")]
        [SerializeField] private LayerMask targetLayers;

        [Tooltip("Maximum number of objects to pull at once.")]
        [SerializeField, Range(1, 32)] private int maxTargets = 16;

        // ══════════════════════════════════════════════════════════
        //  INSPECTOR — VISUALS
        // ══════════════════════════════════════════════════════════

        [Header("── Visuals ─────────────────────────────────────")]
        [Tooltip("Mesh that rotates to indicate active trap.")]
        [SerializeField] private Transform spinningMesh;

        [Tooltip("Rotation speed in degrees per second.")]
        [SerializeField, Range(0f, 360f)] private float rotationSpeed = 90f;

        [Tooltip("Point light for active indicator.")]
        [SerializeField] private Light activeLight;

        [Tooltip("Particle system for active effect.")]
        [SerializeField] private ParticleSystem activeParticles;

        // ══════════════════════════════════════════════════════════
        //  INSPECTOR — AUDIO
        // ══════════════════════════════════════════════════════════

        [Header("── Audio ───────────────────────────────────────")]
        [Tooltip("Sound played when trap is deployed.")]
        [SerializeField] private AudioClip deploySound;

        [Tooltip("Volume for deploy sound.")]
        [SerializeField, Range(0f, 1f)] private float deployVolume = 0.6f;

        // ══════════════════════════════════════════════════════════
        //  RUNTIME STATE
        // ══════════════════════════════════════════════════════════

        private Transform _transform;
        private bool _isActive;
        private bool _isRegisteredTick;
        private bool _isRegisteredSlowTick;

        /// <summary>
        /// Pre-allocated buffer for OverlapSphereNonAlloc.
        /// COLD ALLOC: Collider[32] — gravity trap detection buffer — owner: GravTrap
        /// </summary>
        private Collider[] _targetBuffer;

        // ══════════════════════════════════════════════════════════
        //  PUBLIC ACCESSORS
        // ══════════════════════════════════════════════════════════

        /// <summary>Is the trap active?</summary>
        public bool IsActive => _isActive;

        /// <summary>Current pull radius.</summary>
        public float PullRadius => pullRadius;

        // ══════════════════════════════════════════════════════════
        //  LIFECYCLE
        // ══════════════════════════════════════════════════════════

        private void Awake()
        {
            _transform = transform;

            // COLD ALLOC: Collider buffer for physics detection
            _targetBuffer = new Collider[maxTargets];

            // Set default layer mask if not assigned
            if (targetLayers == 0)
            {
                targetLayers = LayerMask.GetMask("Default", "PhysicsObject");
            }
        }

        private void OnEnable()
        {
            Activate();
        }

        private void OnDisable()
        {
            Deactivate();
        }

        // ══════════════════════════════════════════════════════════
        //  ITickable — VISUAL ROTATION
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Called by GameTickManager every frame.
        /// Handles smooth visual rotation.
        /// </summary>
        /// <param name="deltaTime">Time.deltaTime.</param>
        public void Tick(float deltaTime)
        {
            if (!_isActive) return;

            // Rotate spinning mesh
            if (spinningMesh != null)
            {
                spinningMesh.Rotate(Vector3.up, rotationSpeed * deltaTime, Space.Self);
            }
        }

        // ══════════════════════════════════════════════════════════
        //  ISlowTickable — PHYSICS PULL
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Called by GameTickManager ~2x per second.
        /// Handles gravity pull physics.
        /// </summary>
        public void SlowTick()
        {
            if (!_isActive) return;

            PullNearbyObjects();
        }

        // ══════════════════════════════════════════════════════════
        //  ACTIVATION
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Activates the gravity trap.
        /// </summary>
        public void Activate()
        {
            if (_isActive) return;

            _isActive = true;

            // Enable light
            if (activeLight != null)
            {
                activeLight.enabled = true;
            }

            // Start particles
            if (activeParticles != null)
            {
                activeParticles.Play();
            }

            // Play deploy sound
            if (deploySound != null && Hecton8.Core.GlobalRegistry.Audio is Hecton8.Core.IAudioService audio)
            {
                audio.PlayAtPoint(deploySound, _transform.position, deployVolume);
            }

            // Register for tick
            RegisterToTick();
            RegisterToSlowTick();
        }

        /// <summary>
        /// Deactivates the gravity trap.
        /// </summary>
        public void Deactivate()
        {
            if (!_isActive) return;

            _isActive = false;

            // Disable light
            if (activeLight != null)
            {
                activeLight.enabled = false;
            }

            // Stop particles
            if (activeParticles != null)
            {
                activeParticles.Stop();
            }

            // Unregister from tick
            UnregisterFromTick();
            UnregisterFromSlowTick();
        }

        // ══════════════════════════════════════════════════════════
        //  PHYSICS PULL
        // ══════════════════════════════════════════════════════════

        private void PullNearbyObjects()
        {
            Vector3 trapPos = _transform.position;

            // Use NonAlloc for zero GC
            int hitCount = UnityEngine.Physics.OverlapSphereNonAlloc(
                trapPos,
                pullRadius,
                _targetBuffer,
                targetLayers,
                QueryTriggerInteraction.Ignore
            );

            for (int i = 0; i < hitCount; i++)
            {
                Collider target = _targetBuffer[i];
                if (target == null) continue;

                // Skip self
                if (target.transform == _transform) continue;

                // Get Rigidbody
                Rigidbody rb = target.attachedRigidbody;
                if (rb == null) continue;

                // Skip kinematic or sleeping bodies
                if (rb.isKinematic || rb.IsSleeping()) continue;

                // Calculate direction and distance
                Vector3 targetPos = target.transform.position;
                Vector3 direction = trapPos - targetPos;
                float distance = direction.magnitude;

                // Skip if too close (already at center)
                if (distance < 0.1f) continue;

                direction.Normalize();

                // Calculate force with damping near center
                float forceMagnitude = pullForce;

                if (distance < dampenDistance)
                {
                    // Dampen force to create orbit effect
                    float dampenFactor = distance / dampenDistance;
                    forceMagnitude *= dampenFactor * dampenFactor; // Quadratic falloff
                }

                // Apply force
                Vector3 force = direction * forceMagnitude;
                PhysicsForceRouter.QueueForce(rb, force, ForceMode.Force);
            }
        }

        // ══════════════════════════════════════════════════════════
        //  TICK REGISTRATION
        // ══════════════════════════════════════════════════════════

        private void RegisterToTick()
        {
            if (_isRegisteredTick) return;

            GlobalRegistry.RegisterUpdatable(this, PriorityLayer.Environment);
            _isRegisteredTick = true;
        }

        private void UnregisterFromTick()
        {
            if (!_isRegisteredTick) return;

            GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Environment);
            _isRegisteredTick = false;
        }

        private void RegisterToSlowTick()
        {
            if (_isRegisteredSlowTick) return;

            GlobalRegistry.RegisterSlowTickable(this, PriorityLayer.Environment);
            _isRegisteredSlowTick = true;
        }

        private void UnregisterFromSlowTick()
        {
            if (!_isRegisteredSlowTick) return;

            GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Environment);
            _isRegisteredSlowTick = false;
        }

        // ══════════════════════════════════════════════════════════
        //  EDITOR
        // ══════════════════════════════════════════════════════════

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Vector3 pos = transform.position;

            // Draw pull radius
            Gizmos.color = new Color(0.5f, 0.3f, 1f, 0.3f);
            Gizmos.DrawWireSphere(pos, pullRadius);

            // Draw dampen distance
            Gizmos.color = new Color(0.3f, 0.5f, 1f, 0.3f);
            Gizmos.DrawWireSphere(pos, dampenDistance);

            // Draw center
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireSphere(pos, 0.2f);
        }
#endif
    }
}

