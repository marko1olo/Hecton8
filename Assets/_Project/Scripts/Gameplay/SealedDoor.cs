// ============================================================================
// HECTON-8 — SealedDoor.cs
// Laser-cuttable sealed door for wrecks and restricted areas.
//
// ARCHITECTURE:
//   • Standalone prop — uses ITickable via GameTickManager (no Update).
//   • Progress-based cutting system.
//   • UnityEvents for progress UI and door opening.
//   • ICuttable integration for LaserCutter tool.
//
// ZERO GC:
//   • ITickable.Tick() — no Update(), no allocations.
//   • Cached Transform, Renderer, Collider.
//   • State machine with enum (no coroutines).
//   • MaterialPropertyBlock for progress visualization.
//
// USAGE:
//   1. Place on door GameObject with mesh and collider.
//   2. Configure requiredCuttingTime (seconds of laser required).
//   3. Connect OnProgressChanged to UI progress bar.
//   4. Connect OnDoorOpened to animation or game logic.
// ============================================================================

using Hecton8.Audio;
using Hecton8.Core;
using Hecton8.Gameplay;
using UnityEngine;
using UnityEngine.Events;

namespace Hecton8.Gameplay
{
    /// <summary>
    /// State machine states for door lifecycle.
    /// </summary>
    public enum DoorState
    {
        Sealed,      // Waiting to be cut
        Cutting,     // Currently being cut
        Opened,      // Door opened
        Locked       // Cannot be cut (optional)
    }

    /// <summary>
    /// Sealed door that requires laser cutting to open.
    /// Implements ICuttable for LaserCutter integration.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public sealed class SealedDoor : MonoBehaviour, ICuttable, ITickable, IUpdatable
    {
        // ══════════════════════════════════════════════════════════
        //  SHADER PROPERTY IDs — cached once, zero GC
        // ══════════════════════════════════════════════════════════

        private static readonly int _ProgressID = Shader.PropertyToID("_CutProgress");
        private static readonly int _GlowColorID = Shader.PropertyToID("_CutGlowColor");

        // ══════════════════════════════════════════════════════════
        //  INSPECTOR — CUTTING
        // ══════════════════════════════════════════════════════════

        [Header("── Cutting ────────────────────────────────────")]
        [Tooltip("Total cutting time required in seconds.")]
        [SerializeField, Range(0.5f, 30f)] private float requiredCuttingTime = 4f;

        [Tooltip("Can the door be cut? Set to false for permanently sealed doors.")]
        [SerializeField] private bool canBeCut = true;

        // ══════════════════════════════════════════════════════════
        //  INSPECTOR — VISUALS
        // ══════════════════════════════════════════════════════════

        [Header("── Visuals ─────────────────────────────────────")]
        [Tooltip("Renderer for progress material effect.")]
        [SerializeField] private Renderer doorRenderer;

        [Tooltip("Color of the cutting glow effect.")]
        [SerializeField] private Color cutGlowColor = new Color(1f, 0.5f, 0f); // Orange

        [Tooltip("Particle system for cutting sparks.")]
        [SerializeField] private ParticleSystem cuttingSparks;

        [Tooltip("Particle system for door opening.")]
        [SerializeField] private ParticleSystem openParticles;

        // ══════════════════════════════════════════════════════════
        //  INSPECTOR — AUDIO
        // ══════════════════════════════════════════════════════════

        [Header("── Audio ───────────────────────────────────────")]
        [Tooltip("Sound played while cutting.")]
        [SerializeField] private AudioClip cuttingLoopSound;

        [Tooltip("Sound played when door opens.")]
        [SerializeField] private AudioClip openSound;

        [Tooltip("Volume for cutting sound.")]
        [SerializeField, Range(0f, 1f)] private float cuttingVolume = 0.7f;

        [Tooltip("Volume for open sound.")]
        [SerializeField, Range(0f, 1f)] private float openVolume = 1f;

        // ══════════════════════════════════════════════════════════
        //  INSPECTOR — ANIMATION
        // ══════════════════════════════════════════════════════════

        [Header("── Animation ───────────────────────────────────")]
        [Tooltip("Animator for door opening animation.")]
        [SerializeField] private Animator animator;

        [Tooltip("Animation trigger name for opening.")]
        [SerializeField] private string openTriggerName = "Open";

        // ══════════════════════════════════════════════════════════
        //  EVENTS
        // ══════════════════════════════════════════════════════════

        [Header("── Events ──────────────────────────────────────")]
        [Tooltip("Invoked when cutting progress changes. Parameter: normalized progress (0-1).")]
        [SerializeField] private UnityEvent<float> OnProgressChanged;

        [Tooltip("Invoked when the door is fully cut and opens.")]
        [SerializeField] private UnityEvent OnDoorOpened;

        [Tooltip("Invoked when cutting starts.")]
        [SerializeField] private UnityEvent OnCuttingStarted;

        [Tooltip("Invoked when cutting stops before completion.")]
        [SerializeField] private UnityEvent OnCuttingStopped;

        // ══════════════════════════════════════════════════════════
        //  RUNTIME STATE
        // ══════════════════════════════════════════════════════════

        private Transform _transform;
        private Collider _collider;
        private DoorState _state = DoorState.Sealed;
        private float _currentProgress;
        private bool _isBeingCut;
        private bool _isRegistered;

        /// <summary>
        /// Cached MaterialPropertyBlock for progress VFX.
        /// Allocated once in Awake — zero GC in hot path.
        /// </summary>
        private MaterialPropertyBlock _mpb; // COLD ALLOC: MaterialPropertyBlock[1] — cut progress VFX — owner: SealedDoor

        /// <summary>
        /// Cached animator hash for open trigger.
        /// </summary>
        private int _openTriggerHash;

        // ══════════════════════════════════════════════════════════
        //  PUBLIC ACCESSORS
        // ══════════════════════════════════════════════════════════

        /// <summary>Current state of the door.</summary>
        public DoorState State => _state;

        /// <summary>Current cutting progress (0 to requiredCuttingTime).</summary>
        public float CurrentProgress => _currentProgress;

        /// <summary>Normalized progress (0 to 1).</summary>
        public float ProgressNormalized => requiredCuttingTime > 0f ? _currentProgress / requiredCuttingTime : 0f;

        /// <summary>Is the door fully opened?</summary>
        public bool IsOpened => _state == DoorState.Opened;

        /// <summary>Can the door be cut?</summary>
        public bool CanBeCut => canBeCut && _state == DoorState.Sealed;

        // ══════════════════════════════════════════════════════════
        //  LIFECYCLE
        // ══════════════════════════════════════════════════════════

        private void Awake()
        {
            _transform = transform;
            _collider = GetComponent<Collider>();
            _openTriggerHash = Animator.StringToHash(string.IsNullOrEmpty(openTriggerName) ? "Open" : openTriggerName);

            // COLD ALLOC: MaterialPropertyBlock — progress VFX
            _mpb = new MaterialPropertyBlock();

            // Auto-find renderer if not assigned
            if (doorRenderer == null)
                TryResolveOwnedComponent(transform, out doorRenderer);

            // Auto-find animator if not assigned
            if (animator == null)
                TryResolveOwnedComponent(transform, out animator);

            ResetState();
        }

        private void OnEnable()
        {
            ResetState();
        }

        private void OnDisable()
        {
            UnregisterFromTick();
        }

        private void OnDestroy()
        {
            UnregisterFromTick();
        }

        // ══════════════════════════════════════════════════════════
        //  ITickable
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Called by GameTickManager every frame.
        /// Handles cutting progress decay (optional) and state management.
        /// </summary>
        /// <param name="deltaTime">Time.deltaTime.</param>
        public void Tick(float deltaTime)
        {
            // Currently no per-frame logic needed
            // Progress is driven by ApplyCutting calls from the tool
            // This could be extended for:
            //   - Progress decay when not cutting
            //   - Visual effects during cutting
            //   - Audio loop management
        }

        // ══════════════════════════════════════════════════════════
        //  ICuttable — LASER CUTTER INTEGRATION
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Called by LaserCutter when hitting this door.
        /// Applies cutting progress based on damage amount.
        /// </summary>
        /// <param name="damage">Damage amount (typically damagePerSecond × deltaTime).</param>
        /// <param name="hitPoint">World position of the hit.</param>
        public void ApplyCutDamage(float damage, Vector3 hitPoint)
        {
            // Convert damage to cutting time
            // Assuming damage is per-second rate, damage × deltaTime = progress
            ApplyCutting(damage, hitPoint);
        }

        // ══════════════════════════════════════════════════════════
        //  PUBLIC API — CUTTING
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Applies cutting progress. Called by tools every frame while cutting.
        /// </summary>
        /// <param name="amount">Cutting progress amount (typically deltaTime).</param>
        /// <param name="hitPoint">World position of the laser hit.</param>
        public void ApplyCutting(float amount, Vector3 hitPoint)
        {
            if (!canBeCut) return;
            if (_state == DoorState.Opened) return;
            if (_state == DoorState.Locked) return;
            if (amount <= 0f) return;

            // Start cutting if first hit
            if (_state == DoorState.Sealed && !_isBeingCut)
            {
                StartCutting();
            }

            // Add progress
            _currentProgress += amount;

            // Update visuals
            UpdateProgressVisuals(hitPoint);

            // Fire progress event
            OnProgressChanged?.Invoke(ProgressNormalized);

            // Check for completion
            if (_currentProgress >= requiredCuttingTime)
            {
                OpenDoor();
            }
        }

        /// <summary>
        /// Overload without hit point (uses door center).
        /// </summary>
        /// <param name="amount">Cutting progress amount.</param>
        public void ApplyCutting(float amount)
        {
            ApplyCutting(amount, _transform.position);
        }

        /// <summary>
        /// Stops the cutting process (called when tool stops hitting door).
        /// </summary>
        public void StopCutting()
        {
            if (!_isBeingCut) return;

            _isBeingCut = false;

            // Stop cutting particles
            if (cuttingSparks != null)
            {
                cuttingSparks.Stop();
            }

            // Fire stopped event
            OnCuttingStopped?.Invoke();

            // Unregister from tick (no longer need per-frame updates)
            UnregisterFromTick();
        }

        /// <summary>
        /// Resets the door to sealed state (for testing or special gameplay).
        /// </summary>
        public void ResetDoor()
        {
            ResetState();
        }

        /// <summary>
        /// Locks the door so it cannot be cut.
        /// </summary>
        public void Lock()
        {
            _state = DoorState.Locked;
            StopCutting();
        }

        /// <summary>
        /// Unlocks the door so it can be cut.
        /// </summary>
        public void Unlock()
        {
            if (_state == DoorState.Locked)
            {
                _state = DoorState.Sealed;
            }
        }

        // ══════════════════════════════════════════════════════════
        //  STATE MACHINE
        // ══════════════════════════════════════════════════════════

        private void StartCutting()
        {
            _state = DoorState.Cutting;
            _isBeingCut = true;

            // Start cutting particles
            if (cuttingSparks != null)
            {
                cuttingSparks.Play();
            }

            // Play cutting sound
            if (cuttingLoopSound != null && Hecton8.Core.GlobalRegistry.Audio is Hecton8.Core.IAudioService audio)
            {
                audio.PlayAtPoint(cuttingLoopSound, _transform.position, cuttingVolume);
            }

            // Fire started event
            OnCuttingStarted?.Invoke();

            // Register for tick (for any per-frame logic)
            RegisterToTick();
        }

        private void OpenDoor()
        {
            _state = DoorState.Opened;
            _isBeingCut = false;

            // Stop cutting effects
            if (cuttingSparks != null)
            {
                cuttingSparks.Stop();
            }

            // Play open particles
            if (openParticles != null)
            {
                openParticles.Play();
            }

            // Play open sound
            if (openSound != null && Hecton8.Core.GlobalRegistry.Audio is Hecton8.Core.IAudioService audio)
            {
                audio.PlayAtPoint(openSound, _transform.position, openVolume);
            }

            // Trigger animation
            if (animator != null)
            {
                animator.SetTrigger(_openTriggerHash);
            }

            // Disable collider
            if (_collider != null)
            {
                _collider.enabled = false;
            }

            // Optionally disable renderer (if no animation)
            // doorRenderer.enabled = false;

            // Fire opened event
            OnDoorOpened?.Invoke();

            // Fire final progress event
            OnProgressChanged?.Invoke(1f);

            // Unregister from tick
            UnregisterFromTick();
        }

        // ══════════════════════════════════════════════════════════
        //  VISUALS
        // ══════════════════════════════════════════════════════════

        private void UpdateProgressVisuals(Vector3 hitPoint)
        {
            if (doorRenderer == null) return;
            if (_mpb == null) return;

            // Update shader properties
            doorRenderer.GetPropertyBlock(_mpb);
            _mpb.SetFloat(_ProgressID, ProgressNormalized);
            _mpb.SetColor(_GlowColorID, cutGlowColor);
            doorRenderer.SetPropertyBlock(_mpb);
        }

        private void ResetProgressVisuals()
        {
            if (doorRenderer == null) return;
            if (_mpb == null) return;

            doorRenderer.GetPropertyBlock(_mpb);
            _mpb.SetFloat(_ProgressID, 0f);
            doorRenderer.SetPropertyBlock(_mpb);
        }

        // ══════════════════════════════════════════════════════════
        //  STATE RESET
        // ══════════════════════════════════════════════════════════

        private void ResetState()
        {
            _state = canBeCut ? DoorState.Sealed : DoorState.Locked;
            _currentProgress = 0f;
            _isBeingCut = false;

            // Reset visuals
            ResetProgressVisuals();

            // Re-enable collider
            if (_collider != null)
            {
                _collider.enabled = true;
            }

            // Reset animator
            if (animator != null)
            {
                animator.Rebind();
            }
        }

        // ══════════════════════════════════════════════════════════
        //  PRIVATE — TICK REGISTRATION
        // ══════════════════════════════════════════════════════════

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

        // ══════════════════════════════════════════════════════════
        //  EDITOR
        // ══════════════════════════════════════════════════════════

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (requiredCuttingTime <= 0f)
            {
                requiredCuttingTime = 1f;
            }
        }

        private void OnDrawGizmosSelected()
        {
            // Draw door bounds
            Gizmos.color = _state == DoorState.Opened
                ? new Color(0f, 1f, 0f, 0.3f)
                : new Color(1f, 0.5f, 0f, 0.3f);

            if (_collider != null)
            {
                Gizmos.matrix = transform.localToWorldMatrix;
                if (_collider is BoxCollider box)
                {
                    Gizmos.DrawWireCube(box.center, box.size);
                }
            }
        }
#endif
    }
}

