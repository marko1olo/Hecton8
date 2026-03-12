// ============================================================================
// HECTON-8 — HectonPlayerMovement.cs
// Rigidbody-based hybrid player movement for underwater NASA-Punk environment.
//
// ARCHITECTURE:
//   • ITickable      — input sampling, camera rotation (per frame)
//   • IFixedTickable — physics forces via Rigidbody.AddForce (fixed step)
//   • Integrates with HectonFluidEngine (BuoyancyObject applies buoyancy/drag)
//   • Respects HectonFabricatorUI.IsMenuOpen for input blocking
//
// HYBRID MOVEMENT:
//   Two modes determined by BuoyancyObject.IsInAir:
//
//   SWIM MODE (IsInAir == false):
//     • 3D camera-relative movement (pitch influences direction for diving)
//     • Q/E for explicit vertical ascend/descend
//     • useGravity = false
//     • High linearDamping for water resistance feel
//     • Works WITH HectonFluidEngine forces (buoyancy, currents, engine drag)
//
//   WALK MODE (IsInAir == true):
//     • XZ-only body-relative movement (camera pitch ignored for direction)
//     • No vertical input (gravity handles Y)
//     • useGravity = true
//     • Low linearDamping for air resistance
//     • HectonFluidEngine forces are zeroed (isInAir flag in BuoyancyJob)
//
//   BODY ROTATION (both modes):
//     • Body (Rigidbody) rotates by Yaw ONLY → always upright
//     • Camera rotates by Pitch ONLY → look up/down
//     • freezeRotation = true prevents physics-driven rotation
//
// MODE TRANSITIONS:
//   Edge detection: physics settings (gravity, drag) applied ONLY when
//   mode actually changes. No per-frame property thrashing.
//
// TICK REGISTRATION:
//   Deferred two-phase pattern (OnEnable → Start).
//   Debug.LogError only if GameTickManager.Instance is null at Start().
//   _registeredTick / _registeredFixedTick flags prevent double-register.
//
// ZERO GC:
//   • No allocations in Tick/FixedTick hot paths
//   • Camera reference via Inspector (no Camera.main)
//   • Struct fields for input state, cached Quaternion/Vector3 math
//   • BuoyancyObject cached in Awake — no per-frame GetComponent
//   • Transform cached — no per-frame component lookup
// ============================================================================

using Hecton8.Core;
using Hecton8.Physics;
using Hecton8.UI;
using UnityEngine;

namespace Hecton8.Gameplay
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class HectonPlayerMovement : MonoBehaviour, ITickable, IFixedTickable
    {
        // ══════════════════════════════════════════════════════════
        //  INSPECTOR — CAMERA
        // ══════════════════════════════════════════════════════════

        [Header("── Camera ────────────────────────────────────")]
        [Tooltip("Player camera Transform. Assign via Inspector — never Camera.main.")]
        [SerializeField] private Transform playerCamera;

        // ══════════════════════════════════════════════════════════
        //  INSPECTOR — MOUSE LOOK
        // ══════════════════════════════════════════════════════════

        [Header("── Mouse Look ────────────────────────────────")]
        [Tooltip("Mouse sensitivity multiplier.")]
        [SerializeField] private float mouseSensitivity = 2f;

        [Tooltip("Minimum pitch angle (looking down).")]
        [SerializeField] private float pitchMin = -85f;

        [Tooltip("Maximum pitch angle (looking up).")]
        [SerializeField] private float pitchMax = 85f;

        // ══════════════════════════════════════════════════════════
        //  INSPECTOR — SWIMMING
        // ══════════════════════════════════════════════════════════

        [Header("── Swimming ──────────────────────────────────")]
        [Tooltip("Force applied for camera-relative movement while swimming (Newtons). " +
                 "Applies to all 3 axes following camera direction.")]
        [SerializeField] private float swimForce = 600f;

        [Tooltip("Force applied for explicit ascend/descend (Q/E) while swimming.")]
        [SerializeField] private float verticalForce = 400f;

        [Tooltip("Maximum swim speed on XZ plane (m/s).")]
        [SerializeField] private float maxSwimSpeed = 12f;

        [Tooltip("Maximum vertical speed while swimming (m/s). " +
                 "Set 0 to disable Y clamping (let BuoyancyObject/gravity control it).")]
        [SerializeField] private float maxVerticalSpeed = 0f;

        [Tooltip("Rigidbody.linearDamping in swim mode. " +
                 "Adds resistance on top of HectonFluidEngine viscous drag. " +
                 "Set 0 if engine drag alone is sufficient.")]
        [SerializeField] private float swimLinearDamping = 1f;

        // ══════════════════════════════════════════════════════════
        //  INSPECTOR — WALKING
        // ══════════════════════════════════════════════════════════

        [Header("── Walking ───────────────────────────────────")]
        [Tooltip("Force applied for XZ movement while walking inside a dry module (Newtons).")]
        [SerializeField] private float walkForce = 1200f;

        [Tooltip("Maximum walk speed on XZ plane (m/s).")]
        [SerializeField] private float maxWalkSpeed = 6f;

        [Tooltip("Rigidbody.linearDamping in walk mode. " +
                 "Higher = player stops faster when releasing keys. " +
                 "Note: also affects fall speed slightly.")]
        [SerializeField] private float walkLinearDamping = 5f;

        // ══════════════════════════════════════════════════════════
        //  INSPECTOR — DIAGNOSTICS
        // ══════════════════════════════════════════════════════════

        [Header("── Diagnostics ───────────────────────────────")]
        [SerializeField] private bool _debugIsWalking;

        // ══════════════════════════════════════════════════════════
        //  CACHED REFERENCES
        // ══════════════════════════════════════════════════════════

        private Rigidbody _rb;
        private BuoyancyObject _buoyancy;
        private Transform _cachedTransform;

        // ══════════════════════════════════════════════════════════
        //  INPUT STATE (written in Tick, read in FixedTick)
        // ══════════════════════════════════════════════════════════

        // Directional input [-1..1] — raw, no smoothing
        private float _inputH;
        private float _inputV;
        private float _inputVertical; // ascend/descend (swim only)

        // Camera rotation accumulators
        private float _yaw;
        private float _pitch;

        // Flag: input was cleared this frame (menu open)
        private bool _inputCleared;

        // ══════════════════════════════════════════════════════════
        //  MODE STATE
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Current movement mode. true = walking inside dry module.
        /// Updated each FixedTick via BuoyancyObject.IsInAir.
        /// Physics settings applied only on edge (mode change).
        /// </summary>
        private bool _isWalking;

        // ══════════════════════════════════════════════════════════
        //  REGISTRATION FLAGS
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Tracks ITickable registration. Prevents double-register and orphan unregister.
        /// </summary>
        private bool _registeredTick;

        /// <summary>
        /// Tracks IFixedTickable registration. Separate from _registeredTick
        /// because Register(ITickable) and Register(IFixedTickable) are independent calls.
        /// </summary>
        private bool _registeredFixedTick;

        // ══════════════════════════════════════════════════════════
        //  CACHED MATH — avoid per-frame struct allocation
        // ══════════════════════════════════════════════════════════

        private Vector3 _moveDirection;
        private Vector3 _forceVector;
        private Vector3 _velocity;
        private Quaternion _bodyRotation;
        private Quaternion _cameraRotation;

        // ══════════════════════════════════════════════════════════
        //  LIFECYCLE
        // ══════════════════════════════════════════════════════════

        private void Awake()
        {
            _cachedTransform = transform;

            _rb = GetComponent<Rigidbody>();
            TryGetComponent(out _buoyancy);

            // Sane Rigidbody defaults
            _rb.interpolation = RigidbodyInterpolation.Interpolate;
            _rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            // Prevent Rigidbody from rotating the player (we control rotation manually)
            _rb.freezeRotation = true;

            // Initialize rotation from current transform
            Vector3 euler = _cachedTransform.eulerAngles;
            _yaw = euler.y;

            if (playerCamera != null)
            {
                _pitch = -playerCamera.localEulerAngles.x;
                // Normalize pitch from [0,360) to [-180,180)
                if (_pitch > 180f) _pitch -= 360f;
                _pitch = Mathf.Clamp(_pitch, pitchMin, pitchMax);
            }

            // Determine initial mode and apply physics settings
            _isWalking = _buoyancy != null && _buoyancy.IsInAir;
            ApplyModePhysicsSettings();

            _registeredTick = false;
            _registeredFixedTick = false;
        }

        // ====================================================================
        // TICK REGISTRATION — Deferred two-phase pattern.
        //
        // OnEnable: silent attempt. Start: fallback + error if still null.
        // Separate flags for ITickable and IFixedTickable prevent partial state.
        // ====================================================================

        private void OnEnable()
        {
            TryRegisterToTickManager();
        }

        private void Start()
        {
            // If OnEnable succeeded for both interfaces, nothing to do.
            if (_registeredTick && _registeredFixedTick)
                return;

            // Retry — all Awake() calls have completed by Start().
            TryRegisterToTickManager();

            // Final verdict: error only if still not registered.
            if (!_registeredTick || !_registeredFixedTick)
            {
                Debug.LogError(
                    "[HectonPlayerMovement] GameTickManager.Instance is null even at Start(). " +
                    "Player movement will NOT work. " +
                    "Ensure GameTickManager exists in the scene and is active.", this);
            }
        }

        private void OnDisable()
        {
            GameTickManager inst = GameTickManager.Instance;
            if (inst == null) return;

            if (_registeredTick)
            {
                inst.Unregister((ITickable)this);
                _registeredTick = false;
            }

            if (_registeredFixedTick)
            {
                inst.Unregister((IFixedTickable)this);
                _registeredFixedTick = false;
            }
        }

        /// <summary>
        /// Attempts to register both ITickable and IFixedTickable.
        /// Skips interfaces that are already registered (flag check).
        /// Silent if Instance is null — caller decides whether to log error.
        /// </summary>
        private void TryRegisterToTickManager()
        {
            GameTickManager inst = GameTickManager.Instance;
            if (inst == null) return;

            if (!_registeredTick)
            {
                inst.Register((ITickable)this);
                _registeredTick = true;
            }

            if (!_registeredFixedTick)
            {
                inst.Register((IFixedTickable)this);
                _registeredFixedTick = true;
            }
        }

        // ══════════════════════════════════════════════════════════
        //  ITickable.Tick — INPUT SAMPLING (per frame)
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Samples input every frame. Camera rotation applied immediately
        /// for responsive feel. Movement input stored for FixedTick.
        ///
        /// Input is IDENTICAL for both modes — the difference is how
        /// FixedTick interprets and applies the movement vectors.
        ///
        /// When HectonFabricatorUI.IsMenuOpen == true:
        ///   • All input vectors zeroed
        ///   • Cursor unlocked
        ///   • Early return
        /// </summary>
        public void Tick(float deltaTime)
        {
            // ── Menu check ──
            if (HectonFabricatorUI.IsMenuOpen)
            {
                _inputH = 0f;
                _inputV = 0f;
                _inputVertical = 0f;
                _inputCleared = true;

                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                return;
            }

            // ── Lock cursor for gameplay ──
            if (_inputCleared || Cursor.lockState != CursorLockMode.Locked)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
                _inputCleared = false;
            }

            // ══════════════════════════════════════════════
            //  MOUSE LOOK
            // ══════════════════════════════════════════════

            float mouseX = Input.GetAxisRaw("Mouse X");
            float mouseY = Input.GetAxisRaw("Mouse Y");

            _yaw += mouseX * mouseSensitivity;
            _pitch -= mouseY * mouseSensitivity;
            _pitch = Mathf.Clamp(_pitch, pitchMin, pitchMax);

            // Apply body rotation (Yaw only — body always upright)
            _bodyRotation = Quaternion.Euler(0f, _yaw, 0f);
            _cachedTransform.rotation = _bodyRotation;

            // Apply camera rotation (Pitch only — local X axis)
            if (playerCamera != null)
            {
                _cameraRotation = Quaternion.Euler(_pitch, 0f, 0f);
                playerCamera.localRotation = _cameraRotation;
            }

            // ══════════════════════════════════════════════
            //  MOVEMENT INPUT
            // ══════════════════════════════════════════════

            _inputH = Input.GetAxisRaw("Horizontal");
            _inputV = Input.GetAxisRaw("Vertical");

            // Vertical movement: ascend / descend (used in swim mode only)
            _inputVertical = 0f;

            if (Input.GetKey(KeyCode.Space) || Input.GetKey(KeyCode.E))
                _inputVertical += 1f;

            if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.Q))
                _inputVertical -= 1f;
        }

        // ══════════════════════════════════════════════════════════
        //  IFixedTickable.FixedTick — PHYSICS MOVEMENT (fixed step)
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// All Rigidbody interaction strictly here.
        ///
        /// Pipeline:
        ///   1. Detect mode from BuoyancyObject.IsInAir
        ///   2. On mode change → apply physics settings (gravity, drag)
        ///   3. Dispatch to SwimPhysics or WalkPhysics
        ///   4. Clamp velocity (mode-specific max speeds)
        /// </summary>
        public void FixedTick(float fixedDeltaTime)
        {
            // ══════════════════════════════════════════════
            //  MODE DETECTION (edge-triggered)
            // ══════════════════════════════════════════════

            bool shouldWalk = _buoyancy != null && _buoyancy.IsInAir;

            if (shouldWalk != _isWalking)
            {
                _isWalking = shouldWalk;
                ApplyModePhysicsSettings();
                UpdateModeDiagnostics();
            }

            // ══════════════════════════════════════════════
            //  MOVEMENT PHYSICS
            // ══════════════════════════════════════════════

            if (_isWalking)
                WalkPhysics();
            else
                SwimPhysics();

            // ══════════════════════════════════════════════
            //  VELOCITY CLAMP
            // ══════════════════════════════════════════════

            ClampVelocity();
        }

        // ══════════════════════════════════════════════════════════
        //  PRIVATE — SWIM PHYSICS (3D camera-relative)
        // ══════════════════════════════════════════════════════════

        private void SwimPhysics()
        {
            bool hasInput = _inputH != 0f || _inputV != 0f || _inputVertical != 0f;
            if (!hasInput || playerCamera == null) return;

            // ── Camera vectors (include pitch for diving) ──
            Vector3 camForward = playerCamera.forward;
            Vector3 camRight = playerCamera.right;

            // Camera-driven direction (WASD)
            float camDirX = camForward.x * _inputV + camRight.x * _inputH;
            float camDirY = camForward.y * _inputV + camRight.y * _inputH;
            float camDirZ = camForward.z * _inputV + camRight.z * _inputH;

            // Normalize camera-driven direction to prevent diagonal speed boost
            float camSqrMag = camDirX * camDirX + camDirY * camDirY + camDirZ * camDirZ;
            if (camSqrMag > 1.0001f)
            {
                float invMag = 1f / Mathf.Sqrt(camSqrMag);
                camDirX *= invMag;
                camDirY *= invMag;
                camDirZ *= invMag;
            }

            // Force: camera-driven uses swimForce, explicit vertical uses verticalForce
            _forceVector.x = camDirX * swimForce;
            _forceVector.y = camDirY * swimForce + _inputVertical * verticalForce;
            _forceVector.z = camDirZ * swimForce;

            _rb.AddForce(_forceVector, ForceMode.Force);
        }

        // ══════════════════════════════════════════════════════════
        //  PRIVATE — WALK PHYSICS (XZ body-relative)
        // ══════════════════════════════════════════════════════════

        private void WalkPhysics()
        {
            if (_inputH == 0f && _inputV == 0f) return;

            Vector3 bodyForward = _cachedTransform.forward;
            Vector3 bodyRight = _cachedTransform.right;

            // XZ movement direction
            _moveDirection.x = bodyForward.x * _inputV + bodyRight.x * _inputH;
            _moveDirection.y = 0f;
            _moveDirection.z = bodyForward.z * _inputV + bodyRight.z * _inputH;

            // Normalize to prevent diagonal speed boost
            float sqrMag = _moveDirection.x * _moveDirection.x
                         + _moveDirection.z * _moveDirection.z;

            if (sqrMag > 1.0001f)
            {
                float invMag = 1f / Mathf.Sqrt(sqrMag);
                _moveDirection.x *= invMag;
                _moveDirection.z *= invMag;
            }

            _forceVector.x = _moveDirection.x * walkForce;
            _forceVector.y = 0f;
            _forceVector.z = _moveDirection.z * walkForce;

            _rb.AddForce(_forceVector, ForceMode.Force);
        }

        // ══════════════════════════════════════════════════════════
        //  PRIVATE — VELOCITY CLAMP
        // ══════════════════════════════════════════════════════════

        private void ClampVelocity()
        {
            _velocity = _rb.linearVelocity;
            bool clamped = false;

            // ── XZ clamp (mode-specific max speed) ──
            float maxH = _isWalking ? maxWalkSpeed : maxSwimSpeed;

            if (maxH > 0f)
            {
                float xzSqr = _velocity.x * _velocity.x + _velocity.z * _velocity.z;
                float maxSqr = maxH * maxH;

                if (xzSqr > maxSqr)
                {
                    float scale = maxH / Mathf.Sqrt(xzSqr);
                    _velocity.x *= scale;
                    _velocity.z *= scale;
                    clamped = true;
                }
            }

            // ── Y clamp (swim mode only, optional) ──
            if (!_isWalking && maxVerticalSpeed > 0f)
            {
                float absY = _velocity.y < 0f ? -_velocity.y : _velocity.y;

                if (absY > maxVerticalSpeed)
                {
                    _velocity.y = _velocity.y > 0f ? maxVerticalSpeed : -maxVerticalSpeed;
                    clamped = true;
                }
            }

            if (clamped)
            {
                _rb.linearVelocity = _velocity;
            }
        }

        // ══════════════════════════════════════════════════════════
        //  PRIVATE — MODE TRANSITION
        // ══════════════════════════════════════════════════════════

        private void ApplyModePhysicsSettings()
        {
            if (_isWalking)
            {
                _rb.useGravity = true;
                _rb.linearDamping = walkLinearDamping;
            }
            else
            {
                _rb.useGravity = false;
                _rb.linearDamping = swimLinearDamping;
            }
        }

        // ══════════════════════════════════════════════════════════
        //  DIAGNOSTICS
        // ══════════════════════════════════════════════════════════

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private void UpdateModeDiagnostics()
        {
            _debugIsWalking = _isWalking;
        }

        // ══════════════════════════════════════════════════════════
        //  EDITOR VALIDATION
        // ══════════════════════════════════════════════════════════

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (mouseSensitivity < 0.01f) mouseSensitivity = 0.01f;

            if (swimForce < 0f) swimForce = 0f;
            if (verticalForce < 0f) verticalForce = 0f;
            if (maxSwimSpeed < 0f) maxSwimSpeed = 0f;
            if (maxVerticalSpeed < 0f) maxVerticalSpeed = 0f;
            if (swimLinearDamping < 0f) swimLinearDamping = 0f;

            if (walkForce < 0f) walkForce = 0f;
            if (maxWalkSpeed < 0f) maxWalkSpeed = 0f;
            if (walkLinearDamping < 0f) walkLinearDamping = 0f;

            if (pitchMin < -89.9f) pitchMin = -89.9f;
            if (pitchMax > 89.9f) pitchMax = 89.9f;
            if (pitchMin > pitchMax) pitchMin = pitchMax;
        }
#endif
    }
}