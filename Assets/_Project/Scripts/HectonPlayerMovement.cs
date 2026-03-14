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
//     • HORIZONTAL camera-relative movement (camera pitch does NOT affect direction)
//     • 'W' always means "forward on the horizontal plane"
//     • Vertical movement ONLY via Q/E/Space/Ctrl (explicit ascend/descend)
//     • useGravity = false
//     • High linearDamping for water resistance feel
//     • Works WITH HectonFluidEngine forces (buoyancy, currents, engine drag)
//
//   WALK MODE (IsInAir == true):
//     • XZ-only body-relative movement (camera pitch ignored for direction)
//     • No vertical input (gravity handles Y)
//     • useGravity = true
//     • Low linearDamping for air resistance
//     • Ground detection via SphereCast for slope stability and future jumping
//     • HectonFluidEngine forces are zeroed (isInAir flag in BuoyancyJob)
//
//   BODY ROTATION (both modes):
//     • Body (Rigidbody) rotates by Yaw ONLY → always upright
//     • Camera rotates by Pitch ONLY → look up/down
//     • freezeRotation = true prevents physics-driven rotation
//
// GROUND DETECTION:
//   SphereCast downward from player center each FixedTick when walking.
//   If grounded on Terrain/Default layer:
//     • Applies counter-gravity force to prevent slope sliding
//     • Sets _isGrounded flag for future jump implementation
//   If airborne (walking but not grounded):
//     • Normal gravity applies (freefall)
//
// MODE TRANSITIONS:
//   Edge detection: physics settings (gravity, drag) applied ONLY when
//   mode actually changes. No per-frame property thrashing.
//
// ZERO GC:
//   • No allocations in Tick/FixedTick hot paths
//   • Camera reference via Inspector (no Camera.main)
//   • LayerMask cached at Awake
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
        [Tooltip("Force applied for horizontal movement while swimming (Newtons). " +
                 "Movement direction is the HORIZONTAL projection of camera forward — " +
                 "camera pitch does NOT affect swim direction.")]
        [SerializeField] private float swimForce = 600f;

        [Tooltip("Force applied for explicit ascend/descend (Q/E/Space/Ctrl) while swimming.")]
        [SerializeField] private float verticalForce = 400f;

        [Tooltip("Maximum swim speed on XZ plane (m/s).")]
        [SerializeField] private float maxSwimSpeed = 12f;

        // NOTE: maxVerticalSpeed removed in v5 refactor.
        // Swim mode now uses full 3D magnitude clamp via maxSwimSpeed.
        // All axes symmetric — no separate vertical limit needed.

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

        [Tooltip("Impulse force applied upward when jumping on land (Newtons). " +
                 "Only applies when _isWalking && _isGrounded && Space pressed.")]
        [SerializeField] private float jumpForce = 5f;

        // ══════════════════════════════════════════════════════════
        //  INSPECTOR — GROUND DETECTION
        // ══════════════════════════════════════════════════════════

        [Header("── Ground Detection ─────────────────────────")]
        [Tooltip("Radius of the SphereCast used for ground detection.")]
        [SerializeField] private float groundCheckRadius = 0.3f;

        [Tooltip("Distance below player center to cast for ground.")]
        [SerializeField] private float groundCheckDistance = 0.4f;

        [Tooltip("Layers considered as ground for walking. " +
                 "Should include Terrain and Default, exclude Water.")]
        [SerializeField] private LayerMask groundLayers = ~0; // default: everything

        [Tooltip("Multiplier for counter-gravity force when grounded on slopes. " +
                 "1.0 = exactly cancel gravity. Higher = more slope stability.")]
        [SerializeField, Range(1f, 2f)]
        private float slopeStabilityFactor = 1.1f;

        // ══════════════════════════════════════════════════════════
        //  INSPECTOR — DIAGNOSTICS
        // ══════════════════════════════════════════════════════════

        [Header("── Diagnostics ───────────────────────────────")]
        [SerializeField] private bool _debugIsWalking;
        [SerializeField] private bool _debugIsGrounded;

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

        // Jump request flag (set in Tick, consumed in FixedTick)
        private bool _jumpRequested;

        // ══════════════════════════════════════════════════════════
        //  MODE STATE
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Current movement mode. true = walking (in air / on land).
        /// Updated each FixedTick via BuoyancyObject.IsInAir.
        /// Physics settings applied only on edge (mode change).
        /// </summary>
        private bool _isWalking;

        /// <summary>
        /// True when SphereCast hits ground while in walk mode.
        /// Used for slope stability and future jumping.
        /// </summary>
        private bool _isGrounded;

        // ══════════════════════════════════════════════════════════
        //  REGISTRATION FLAGS
        // ══════════════════════════════════════════════════════════

        private bool _registeredTick;
        private bool _registeredFixedTick;

        // ══════════════════════════════════════════════════════════
        //  CACHED MATH — avoid per-frame struct allocation
        // ══════════════════════════════════════════════════════════

        private Vector3 _moveDirection;
        private Vector3 _forceVector;
        private Vector3 _velocity;
        private Quaternion _bodyRotation;
        private Quaternion _cameraRotation;

        // Ground check cached structs (avoid stack allocation each frame)
        private RaycastHit _groundHit;
        private Vector3 _groundCheckOrigin;

        // Cached gravity vector (avoid Physics.gravity property access per frame)
        private Vector3 _cachedGravity;

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

            // Cache gravity
            _cachedGravity = UnityEngine.Physics.gravity;

            // Determine initial mode and apply physics settings
            _isWalking = _buoyancy != null && _buoyancy.IsInAir;
            ApplyModePhysicsSettings();

            _registeredTick = false;
            _registeredFixedTick = false;
        }

        // ====================================================================
        // TICK REGISTRATION — Deferred two-phase pattern.
        // ====================================================================

        private void OnEnable()
        {
            TryRegisterToTickManager();
        }

        private void Start()
        {
            if (_registeredTick && _registeredFixedTick)
                return;

            TryRegisterToTickManager();

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

            // ── Vertical / Jump input ──
            // Behavior depends on mode (evaluated in FixedTick):
            //   WALK: Space = jump impulse (one-shot, requires grounded)
            //   SWIM: Space = continuous ascend force, Ctrl = descend force
            _inputVertical = 0f;

            if (_isWalking)
            {
                // Jump request: GetKeyDown for one-shot impulse
                if (Input.GetKeyDown(KeyCode.Space))
                    _jumpRequested = true;
            }
            else
            {
                // Swim: continuous vertical forces
                if (Input.GetKey(KeyCode.Space) || Input.GetKey(KeyCode.E))
                    _inputVertical += 1f;

                if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.Q))
                    _inputVertical -= 1f;
            }
        }

        // ══════════════════════════════════════════════════════════
        //  IFixedTickable.FixedTick — PHYSICS MOVEMENT (fixed step)
        // ══════════════════════════════════════════════════════════

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
            //  GROUND DETECTION (walk mode only)
            // ══════════════════════════════════════════════

            if (_isWalking)
            {
                GroundCheck();
            }
            else
            {
                _isGrounded = false;
            }

            // ══════════════════════════════════════════════
            //  JUMP (walk mode only, consumes request)
            // ══════════════════════════════════════════════

            if (_jumpRequested)
            {
                _jumpRequested = false;

                if (_isWalking && _isGrounded)
                {
                    // Cancel any residual downward velocity before jump
                    _velocity = _rb.linearVelocity;
                    if (_velocity.y < 0f)
                    {
                        _velocity.y = 0f;
                        _rb.linearVelocity = _velocity;
                    }

                    _rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
                }
            }

            // ══════════════════════════════════════════════
            //  MOVEMENT PHYSICS
            // ══════════════════════════════════════════════

            if (_isWalking)
                WalkPhysics();
            else
                SwimPhysics();

            // ══════════════════════════════════════════════
            //  GROUND STABILITY (walk mode, grounded)
            // ══════════════════════════════════════════════

            if (_isWalking && _isGrounded)
            {
                ApplyGroundStability();
            }

            // ══════════════════════════════════════════════
            //  VELOCITY CLAMP
            // ══════════════════════════════════════════════

            ClampVelocity();

            // ══════════════════════════════════════════════
            //  DIAGNOSTICS UPDATE
            // ══════════════════════════════════════════════

            UpdateGroundDiagnostics();
        }

        // ══════════════════════════════════════════════════════════
        //  PRIVATE — GROUND DETECTION
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// SphereCast downward from player center to detect ground.
        /// Only called when _isWalking == true.
        ///
        /// Uses groundCheckRadius to avoid edge-case misses with thin geometry.
        /// Cast origin is offset upward by radius to prevent starting inside geometry.
        ///
        /// Zero-GC: uses cached _groundHit and _groundCheckOrigin.
        /// </summary>
        private void GroundCheck()
        {
            _groundCheckOrigin.x = _cachedTransform.position.x;
            _groundCheckOrigin.y = _cachedTransform.position.y + groundCheckRadius;
            _groundCheckOrigin.z = _cachedTransform.position.z;

            _isGrounded = UnityEngine.Physics.SphereCast(
                _groundCheckOrigin,
                groundCheckRadius,
                Vector3.down,
                out _groundHit,
                groundCheckDistance + groundCheckRadius,
                groundLayers,
                QueryTriggerInteraction.Ignore
            );
        }

        /// <summary>
        /// When grounded on a slope, applies a counter-gravity force projected
        /// along the ground normal. This prevents the player from sliding down
        /// slopes due to gravity's tangential component.
        ///
        /// The force is: -gravity projected onto the surface plane, scaled by
        /// slopeStabilityFactor for slight overcorrection (prevents drift).
        ///
        /// Also cancels downward velocity when grounded to prevent gravity
        /// accumulation while standing still.
        /// </summary>
        private void ApplyGroundStability()
        {
            // Cancel accumulated downward velocity when grounded
            _velocity = _rb.linearVelocity;
            if (_velocity.y < 0f)
            {
                _velocity.y = 0f;
                _rb.linearVelocity = _velocity;
            }

            // Counter-gravity force along ground normal
            // gravityAlongNormal = dot(gravity, normal) * normal
            // counterForce = -gravity + gravityAlongNormal = force that keeps us on surface
            Vector3 gravityForce = _cachedGravity * _rb.mass;
            float dot = Vector3.Dot(gravityForce, _groundHit.normal);

            // Only apply on slopes (dot < 0 means gravity pushes into surface)
            if (dot < 0f)
            {
                // Force to cancel the component of gravity along the slope
                _forceVector.x = -gravityForce.x + _groundHit.normal.x * dot;
                _forceVector.y = -gravityForce.y + _groundHit.normal.y * dot;
                _forceVector.z = -gravityForce.z + _groundHit.normal.z * dot;

                // Apply with stability factor (slight overcorrection)
                _forceVector.x *= slopeStabilityFactor;
                _forceVector.y *= slopeStabilityFactor;
                _forceVector.z *= slopeStabilityFactor;

                _rb.AddForce(_forceVector, ForceMode.Force);
            }
        }

        // ══════════════════════════════════════════════════════════
        //  PRIVATE — SWIM PHYSICS (horizontal camera-relative)
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// 6DOF Look-Relative Swim Movement.
        ///
        /// W/S: move along camera.forward (FULL 3D — looking down + W = dive).
        /// A/D: strafe along camera.right (horizontal component only).
        /// Space/E: explicit ascend (continuous upward force).
        /// Ctrl/Q:  explicit descend (continuous downward force).
        ///
        /// All axes use swimForce for symmetric speed.
        /// Vertical from Space/Ctrl uses verticalForce (allows independent tuning).
        /// All velocities clamped to maxSwimSpeed in ClampVelocity().
        ///
        /// Why full 3D forward:
        ///   - Player looks down → W should dive, not slide on XZ.
        ///   - Subnautica-style navigation requires look-relative 6DOF.
        ///   - Explicit ascend/descend keys provide fine vertical control
        ///     independent of camera pitch.
        ///
        /// Zero GC: all struct math, no allocations.
        /// </summary>
        private void SwimPhysics()
        {
            bool hasInput = _inputH != 0f || _inputV != 0f || _inputVertical != 0f;
            if (!hasInput || playerCamera == null) return;

            // ── Camera vectors (world space) ──
            Vector3 camForward = playerCamera.forward; // full 3D direction
            Vector3 camRight   = playerCamera.right;   // full 3D direction

            // ── 3D movement from WASD ──
            // W/S: along camera.forward (includes Y component = dive/rise)
            // A/D: along camera.right (includes Y from camera roll, usually ~0)
            float dirX = camForward.x * _inputV + camRight.x * _inputH;
            float dirY = camForward.y * _inputV + camRight.y * _inputH;
            float dirZ = camForward.z * _inputV + camRight.z * _inputH;

            // ── Normalize to prevent diagonal speed boost ──
            float sqrMag = dirX * dirX + dirY * dirY + dirZ * dirZ;
            if (sqrMag > 1.0001f)
            {
                float invMag = 1f / Mathf.Sqrt(sqrMag);
                dirX *= invMag;
                dirY *= invMag;
                dirZ *= invMag;
            }

            // ── Apply swim force (uniform on all axes) ──
            _forceVector.x = dirX * swimForce;
            _forceVector.y = dirY * swimForce;
            _forceVector.z = dirZ * swimForce;

            // ── Add explicit vertical force (Space/Ctrl — independent of camera) ──
            _forceVector.y += _inputVertical * verticalForce;

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

        /// <summary>
        /// Clamps velocity based on current mode.
        ///
        /// WALK MODE: XZ clamp only (Y is gravity-controlled).
        ///
        /// SWIM MODE: Full 3D magnitude clamp to maxSwimSpeed.
        ///   All axes are symmetric — diving at full speed should
        ///   be the same speed as swimming forward.
        ///   This replaces the separate XZ + Y clamping.
        ///   External forces (currents, BuoyancyObject) can push
        ///   beyond this limit — we only clamp player-driven velocity.
        ///
        /// Zero GC: struct math only.
        /// </summary>
        private void ClampVelocity()
        {
            _velocity = _rb.linearVelocity;
            bool clamped = false;

            if (_isWalking)
            {
                // ── Walk: XZ clamp only (Y is gravity-controlled) ──
                if (maxWalkSpeed > 0f)
                {
                    float xzSqr = _velocity.x * _velocity.x
                                 + _velocity.z * _velocity.z;
                    float maxSqr = maxWalkSpeed * maxWalkSpeed;

                    if (xzSqr > maxSqr)
                    {
                        float scale = maxWalkSpeed / Mathf.Sqrt(xzSqr);
                        _velocity.x *= scale;
                        _velocity.z *= scale;
                        clamped = true;
                    }
                }
            }
            else
            {
                // ── Swim: Full 3D magnitude clamp (symmetric axes) ──
                if (maxSwimSpeed > 0f)
                {
                    float fullSqr = _velocity.x * _velocity.x
                                  + _velocity.y * _velocity.y
                                  + _velocity.z * _velocity.z;
                    float maxSqr = maxSwimSpeed * maxSwimSpeed;

                    if (fullSqr > maxSqr)
                    {
                        float scale = maxSwimSpeed / Mathf.Sqrt(fullSqr);
                        _velocity.x *= scale;
                        _velocity.y *= scale;
                        _velocity.z *= scale;
                        clamped = true;
                    }
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

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private void UpdateGroundDiagnostics()
        {
            _debugIsGrounded = _isGrounded;
        }

        // ══════════════════════════════════════════════════════════
        //  PUBLIC API
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Whether the player is currently grounded (walking mode + ground detected).
        /// Useful for jump systems, animation triggers, etc.
        /// </summary>
        public bool IsGrounded => _isGrounded && _isWalking;

        /// <summary>
        /// Whether the player is in walk mode (as opposed to swim mode).
        /// </summary>
        public bool IsWalking => _isWalking;

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
            // if (maxVerticalSpeed < 0f) maxVerticalSpeed = 0f;
            if (swimLinearDamping < 0f) swimLinearDamping = 0f;

            if (walkForce < 0f) walkForce = 0f;
            if (maxWalkSpeed < 0f) maxWalkSpeed = 0f;
            if (walkLinearDamping < 0f) walkLinearDamping = 0f;
            if (jumpForce < 0f) jumpForce = 0f;

            if (groundCheckRadius < 0.01f) groundCheckRadius = 0.01f;
            if (groundCheckDistance < 0.01f) groundCheckDistance = 0.01f;

            if (pitchMin < -89.9f) pitchMin = -89.9f;
            if (pitchMax > 89.9f) pitchMax = 89.9f;
            if (pitchMin > pitchMax) pitchMin = pitchMax;
        }

        private void OnDrawGizmosSelected()
        {
            if (!Application.isPlaying) return;

            // Visualize ground check sphere
            Vector3 origin = transform.position + Vector3.up * groundCheckRadius;
            Vector3 castEnd = origin + Vector3.down * (groundCheckDistance + groundCheckRadius);

            if (_isGrounded)
            {
                Gizmos.color = new Color(0f, 1f, 0f, 0.5f);
                Gizmos.DrawWireSphere(_groundHit.point, groundCheckRadius);
            }
            else
            {
                Gizmos.color = new Color(1f, 0f, 0f, 0.3f);
                Gizmos.DrawWireSphere(castEnd, groundCheckRadius);
            }

            Gizmos.color = new Color(1f, 1f, 0f, 0.5f);
            Gizmos.DrawLine(origin, castEnd);
        }
#endif
    }
}