// ============================================================================
// HECTON-8 — DeployableFlare.cs
// Thrown chemical light source for underwater illumination.
//
// ARCHITECTURE:
//   • Standalone prop — uses ITickable via GameTickManager (no Update).
//   • State machine for fuel countdown and fade-out.
//   • Physics-based sinking with high drag (underwater simulation).
//
// ZERO GC:
//   • ITickable.Tick() — no Update(), no allocations.
//   • Cached Transform, Light, ParticleSystem, Rigidbody.
//   • State machine with enum (no coroutines).
//   • Pre-cached light intensity for Lerp.
//
// USAGE:
//   1. Create flare prefab with PointLight, ParticleSystem, Rigidbody.
//   2. Assign this script and configure fuel duration.
//   3. Spawn via ObjectPoolManager or Instantiate when thrown.
// ============================================================================

using Hecton8.Audio;
using Hecton8.Core;
using Hecton8.Physics;
using Hecton8.World;
using UnityEngine;

namespace Hecton8.Gameplay
{
    /// <summary>
    /// State machine states for flare lifecycle.
    /// </summary>
    public enum FlareState
    {
        Inactive,   // Not yet deployed
        Burning,    // Active illumination
        Fading,     // Fuel depleted, fading out
        Extinguished // Completely out
    }

    /// <summary>
    /// Deployable chemical flare for underwater illumination.
    /// Burns for a configurable duration, then fades out smoothly.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(Light))]
    public sealed class DeployableFlare : MonoBehaviour, ITickable, IUpdatable
    {
        // ══════════════════════════════════════════════════════════
        //  INSPECTOR — FUEL / LIFETIME
        // ══════════════════════════════════════════════════════════

        [Header("── Fuel / Lifetime ───────────────────────────")]
        [Tooltip("Duration the flare burns in seconds.")]
        [SerializeField, Range(10f, 300f)] private float fuelDuration = 60f;

        [Tooltip("Duration of fade-out in seconds.")]
        [SerializeField, Range(0.5f, 10f)] private float fadeDuration = 2f;

        // ══════════════════════════════════════════════════════════
        //  INSPECTOR — LIGHT
        // ══════════════════════════════════════════════════════════

        [Header("── Light ──────────────────────────────────────")]
        [Tooltip("Point Light component (auto-found if not assigned).")]
        [SerializeField] private Light pointLight;

        [Tooltip("Initial light intensity.")]
        [SerializeField, Range(0f, 10f)] private float maxIntensity = 3f;

        [Tooltip("Light range in meters.")]
        [SerializeField, Range(1f, 50f)] private float lightRange = 15f;

        [Tooltip("Light color.")]
        [SerializeField] private Color lightColor = new Color(1f, 0.6f, 0.2f); // Orange-red

        // ══════════════════════════════════════════════════════════
        //  INSPECTOR — PHYSICS
        // ══════════════════════════════════════════════════════════

        [Header("── Physics (Underwater) ──────────────────────")]
        [Tooltip("Rigidbody drag for slow underwater sinking.")]
        [SerializeField, Range(0f, 10f)] private float underwaterDrag = 3f;

        [Tooltip("Rigidbody angular drag.")]
        [SerializeField, Range(0f, 5f)] private float underwaterAngularDrag = 1f;

        [Tooltip("Should the flare sink slowly?")]
        [SerializeField] private bool enableSinking = true;

        [Tooltip("Sinking speed (negative Y velocity).")]
        [SerializeField, Range(0f, 2f)] private float sinkSpeed = 0.3f;

        // ══════════════════════════════════════════════════════════
        //  INSPECTOR — VFX
        // ══════════════════════════════════════════════════════════

        [Header("── VFX ────────────────────────────────────────")]
        [Tooltip("Particle system for sparks/bubbles (auto-found if not assigned).")]
        [SerializeField] private ParticleSystem flareParticles;

        [Tooltip("Particle emission rate during burn.")]
        [SerializeField, Range(1f, 50f)] private float particleEmissionRate = 10f;

        // ══════════════════════════════════════════════════════════
        //  INSPECTOR — AUDIO
        // ══════════════════════════════════════════════════════════

        [Header("── Audio ─────────────────────────────────────")]
        [Tooltip("Sound played when flare is deployed.")]
        [SerializeField] private AudioClip deploySound;

        [Tooltip("Sound played when flare extinguishes.")]
        [SerializeField] private AudioClip extinguishSound;

        [Tooltip("Volume for deploy sound.")]
        [SerializeField, Range(0f, 1f)] private float deployVolume = 0.6f;

        // ══════════════════════════════════════════════════════════
        //  RUNTIME STATE
        // ══════════════════════════════════════════════════════════

        private Transform _transform;
        private Rigidbody _rigidbody;
        private ParticleSystem.EmissionModule _emissionModule;

        private FlareState _state = FlareState.Inactive;
        private float _fuelTimer;
        private float _fadeTimer;
        private float _currentIntensity;
        private bool _isRegistered;
        private int _spatialHandle;
        private int _faunaSpatialHandle;
        private Vector3 _lastSpatialPosition;

        // ══════════════════════════════════════════════════════════
        //  PUBLIC ACCESSORS
        // ══════════════════════════════════════════════════════════

        /// <summary>Current state of the flare.</summary>
        public FlareState State => _state;

        /// <summary>Remaining fuel time in seconds.</summary>
        public float RemainingFuel => _fuelTimer;

        /// <summary>Is the flare currently burning?</summary>
        public bool IsBurning => _state == FlareState.Burning;

        // ══════════════════════════════════════════════════════════
        //  LIFECYCLE
        // ══════════════════════════════════════════════════════════

        private void Awake()
        {
            _transform = transform;

            // Cache Rigidbody
            _rigidbody = GetComponent<Rigidbody>();

            // Auto-find Light if not assigned
            if (pointLight == null)
            {
                pointLight = GetComponent<Light>();
            }

            // Auto-find ParticleSystem if not assigned
            if (flareParticles != null)
            {
                _emissionModule = flareParticles.emission;
            }

            // Configure physics for underwater
            ConfigurePhysics();

            // Initialize light
            InitializeLight();
            Hecton8.Core.HectonUrpShadowBudgetGuard.RegisterDynamicShadowLight(pointLight);
        }

        private void OnEnable()
        {
            // Do NOT auto-register - wait for Deploy() call
        }

        private void OnDisable()
        {
            // Unregister from tick system
            UnregisterFromTick();
            UnregisterSpatialHandle();
            Hecton8.Core.HectonUrpShadowBudgetGuard.UnregisterDynamicShadowLight(pointLight);
        }

        // ══════════════════════════════════════════════════════════
        //  ITickable
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Called by GameTickManager every frame.
        /// Handles fuel countdown, fade-out, and sinking.
        /// </summary>
        /// <param name="deltaTime">Time.deltaTime.</param>
        public void Tick(float deltaTime)
        {
            RefreshSpatialHandle();

            switch (_state)
            {
                case FlareState.Burning:
                    TickBurning(deltaTime);
                    break;

                case FlareState.Fading:
                    TickFading(deltaTime);
                    break;

                case FlareState.Extinguished:
                case FlareState.Inactive:
                default:
                    // No tick processing
                    break;
            }
        }

        // ══════════════════════════════════════════════════════════
        //  STATE MACHINE
        // ══════════════════════════════════════════════════════════

        private void TickBurning(float deltaTime)
        {
            // Countdown fuel
            _fuelTimer -= deltaTime;

            // Apply sinking
            if (enableSinking && _rigidbody != null)
            {
                // Gentle constant sinking force
                PhysicsForceRouter.QueueForce(
                    _rigidbody,
                    Vector3.down * sinkSpeed * deltaTime,
                    ForceMode.VelocityChange);
            }

            // Check for fuel depletion
            if (_fuelTimer <= 0f)
            {
                StartFadeOut();
            }
        }

        private void TickFading(float deltaTime)
        {
            _fadeTimer += deltaTime;

            // Calculate fade progress (0 to 1)
            float fadeProgress = Mathf.Clamp01(_fadeTimer / fadeDuration);

            // Lerp light intensity
            _currentIntensity = Mathf.Lerp(maxIntensity, 0f, fadeProgress);

            if (pointLight != null)
            {
                pointLight.intensity = _currentIntensity;
            }

            // Fade particle emission
            if (flareParticles != null)
            {
                _emissionModule.rateOverTime = particleEmissionRate * (1f - fadeProgress);
            }

            // Check for complete fade
            if (fadeProgress >= 1f)
            {
                Extinguish();
            }
        }

        private void StartFadeOut()
        {
            _state = FlareState.Fading;
            _fadeTimer = 0f;

            // Play extinguish sound
            if (extinguishSound != null && Hecton8.Core.GlobalRegistry.Audio is Hecton8.Core.IAudioService audio)
            {
                audio.PlayAtPoint(extinguishSound, _transform.position, deployVolume);
            }
        }

        private void Extinguish()
        {
            _state = FlareState.Extinguished;

            // Stop particles
            if (flareParticles != null)
            {
                flareParticles.Stop();
            }

            // Disable light
            if (pointLight != null)
            {
                pointLight.enabled = false;
            }

            // Unregister from tick system to save performance
            UnregisterFromTick();
            UnregisterSpatialHandle();
        }

        // ══════════════════════════════════════════════════════════
        //  PUBLIC API
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Deploys the flare, starting the burn cycle.
        /// Call this after spawning/throwing the flare.
        /// </summary>
        public void Deploy()
        {
            if (_state != FlareState.Inactive && _state != FlareState.Extinguished)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogWarning($"[DeployableFlare] Deploy called on already active flare (state={_state})", this);
#endif
                return;
            }

            // Initialize state
            _state = FlareState.Burning;
            _fuelTimer = fuelDuration;
            _fadeTimer = 0f;
            _currentIntensity = maxIntensity;

            // Enable light
            if (pointLight != null)
            {
                pointLight.enabled = true;
                pointLight.intensity = maxIntensity;
                pointLight.range = lightRange;
                pointLight.color = lightColor;
            }

            // Start particles
            if (flareParticles != null)
            {
                _emissionModule.rateOverTime = particleEmissionRate;
                flareParticles.Play();
            }

            // Play deploy sound
            if (deploySound != null && Hecton8.Core.GlobalRegistry.Audio is Hecton8.Core.IAudioService audio)
            {
                audio.PlayAtPoint(deploySound, _transform.position, deployVolume);
            }

            // Register with tick system
            RegisterToTick();
            RegisterSpatialHandle();
        }

        /// <summary>
        /// Immediately extinguishes the flare.
        /// </summary>
        public void ForceExtinguish()
        {
            if (_state == FlareState.Extinguished) return;

            _state = FlareState.Extinguished;

            // Stop particles
            if (flareParticles != null)
            {
                flareParticles.Stop();
            }

            // Disable light
            if (pointLight != null)
            {
                pointLight.enabled = false;
            }

            // Unregister from tick
            UnregisterFromTick();
            UnregisterSpatialHandle();
        }

        /// <summary>
        /// Resets the flare to inactive state (for pooling).
        /// </summary>
        public void ResetFlare()
        {
            _state = FlareState.Inactive;
            _fuelTimer = fuelDuration;
            _fadeTimer = 0f;
            _currentIntensity = 0f;

            // Reset light
            if (pointLight != null)
            {
                pointLight.enabled = false;
                pointLight.intensity = 0f;
            }

            // Reset particles
            if (flareParticles != null)
            {
                flareParticles.Stop();
                _emissionModule.rateOverTime = 0f;
            }

            // Unregister from tick
            UnregisterFromTick();
            UnregisterSpatialHandle();
        }

        // ══════════════════════════════════════════════════════════
        //  PRIVATE — INITIALIZATION
        // ══════════════════════════════════════════════════════════

        private void ConfigurePhysics()
        {
            if (_rigidbody == null) return;

            // Set high drag for underwater feel
            _rigidbody.linearDamping = underwaterDrag;
            _rigidbody.angularDamping = underwaterAngularDrag;

            // Disable gravity for underwater buoyancy feel
            _rigidbody.useGravity = false;
        }

        private void InitializeLight()
        {
            if (pointLight == null) return;

            // Configure as point light
            pointLight.type = LightType.Point;
            pointLight.enabled = false;
            pointLight.intensity = 0f;
            pointLight.range = lightRange;
            pointLight.color = lightColor;

            // Set shadows to soft for better quality
            pointLight.shadows = LightShadows.Soft;
            pointLight.shadowStrength = 0.5f;
        }

        // ══════════════════════════════════════════════════════════
        //  PRIVATE — TICK REGISTRATION
        // ══════════════════════════════════════════════════════════

        private void RegisterToTick()
        {
            if (_isRegistered) return;
            if (!Application.isPlaying || GlobalRegistry.Dispatcher == null) return;

            GlobalRegistry.RegisterUpdatable(this, PriorityLayer.Environment);
            _isRegistered = true;
        }

        private void UnregisterFromTick()
        {
            if (!_isRegistered) return;

            GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Environment);
            _isRegistered = false;
        }

        private void RegisterSpatialHandle()
        {
            if (_spatialHandle == 0)
                _spatialHandle = WorldSpatialHashGrid.RegisterSignal(this);

            if (_faunaSpatialHandle == 0)
                _faunaSpatialHandle = FaunaSpatialHashRegistry.RegisterSignal(this);

            _lastSpatialPosition = _transform.position;
        }

        private void UnregisterSpatialHandle()
        {
            if (_spatialHandle != 0)
            {
                WorldSpatialHashGrid.Unregister(_spatialHandle);
                _spatialHandle = 0;
            }

            if (_faunaSpatialHandle != 0)
            {
                FaunaSpatialHashRegistry.Unregister(_faunaSpatialHandle);
                _faunaSpatialHandle = 0;
            }
        }

        private void RefreshSpatialHandle()
        {
            if (_spatialHandle == 0)
                return;

            Vector3 currentPosition = _transform.position;
            WorldSpatialHashGrid.UpdateGridPosition(_spatialHandle, _lastSpatialPosition, currentPosition);
            if (_faunaSpatialHandle != 0)
                FaunaSpatialHashRegistry.Refresh(_faunaSpatialHandle);
            _lastSpatialPosition = currentPosition;
        }

        // ══════════════════════════════════════════════════════════
        //  EDITOR
        // ══════════════════════════════════════════════════════════

#if UNITY_EDITOR
        private void OnValidate()
        {
            // Ensure light is configured
            if (pointLight == null)
            {
                pointLight = GetComponent<Light>();
            }

            // Ensure particles are configured
            if (flareParticles == null)
            {
                flareParticles = Hecton8.Core.ComponentReferenceUtility.ResolveOwnedComponent<ParticleSystem>(transform);
            }
        }

        private void OnDrawGizmosSelected()
        {
            // Draw light range
            Gizmos.color = new Color(1f, 0.6f, 0.2f, 0.2f);
            Gizmos.DrawWireSphere(transform.position, lightRange);

            // Draw sink direction
            if (enableSinking)
            {
                Gizmos.color = Color.cyan;
                Vector3 sinkDir = Vector3.down * 2f;
                Gizmos.DrawRay(transform.position, sinkDir);
            }
        }
#endif
    }
}

