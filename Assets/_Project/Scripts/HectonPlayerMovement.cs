// ============================================================================
// HECTON-8 — HectonPlayerMovement.cs  v7.0
// Rigidbody-based hybrid player movement — FULL IMMERSION BUILD
//
// v7.0 ADDITIONS:
//   • Depth calculation + feeding to CameraJuiceInput
//   • Depth-based swim slowdown (pressure resistance)
//   • Collision camera shake via OnCollisionEnter
//   • Splash / submerge events exposed as pollable properties
//   • FOV offset applied from CameraJuiceProcessor
//   • Visual pitch inertia fed through juice processor
//   • Exhale event exposed
//   • New diagnostic fields for depth, FOV, splash, exhale
//
// v6.3 PRESERVED:
//   • Crest dynamic height, smoothed immersion, single GroundCheck
//   • Surface lock, graduated gravity, ground snap, mode detection
//   • Zero-rotation Rigidbody, zero-jitter camera
// ============================================================================

using Hecton8.Core;
using Hecton8.Physics;
using Hecton8.UI;
using Hecton8.Input;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Hecton8.Gameplay
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class HectonPlayerMovement : MonoBehaviour, ITickable, IFixedTickable
    {
        private const float GroundCheckSkin = 0.02f;
        private static readonly string[] _locomotionModeLabels =
        {
            "DryGroundWalk",
            "DryInteriorWalk",
            "ShallowWadeWalk",
            "SurfaceSwim",
            "UnderwaterSwim"
        }; // COLD ALLOC: string[5] — editor diagnostics labels — owner: HectonPlayerMovement

        // ══════════════════════════════════════════════════════════
        //  INSPECTOR — REFERENCES
        // ══════════════════════════════════════════════════════════

        [Header("── References ────────────────────────────────")]
        [SerializeField] private Transform playerCamera;
        [SerializeField] private SuitData currentSuitData;
        [SerializeField] private ControlScheme controlScheme;
        [SerializeField] private bool leanIntoTurn = true;

        // ══════════════════════════════════════════════════════════
        //  INSPECTOR — WATER CONFIGURATION
        // ══════════════════════════════════════════════════════════

        [Header("── Water Configuration ──────────────────────")]
        [Tooltip("Fallback water surface Y when Crest is unavailable.")]
        [SerializeField] private float waterSurfaceY = 4900f;

        [SerializeField] private float playerHeight = 1.8f;

        [SerializeField, Range(0.3f, 0.95f)]
        [Tooltip("Immersion ratio above which player switches from walking to swimming.")]
        private float swimTransitionThreshold = 0.7f;

        [Header("── Surface Swim Realism ─────────────────────────")]
        [Tooltip("Depth band near the waterline treated as surface swim instead of deep 3D swim.")]
        [SerializeField, Range(0.1f, 2.5f)] private float surfaceSwimDepthBand = 0.85f;
        [Tooltip("How strongly forward swim is flattened near the surface. 1 = strongly planar.")]
        [SerializeField, Range(0f, 1f)] private float surfaceForwardPitchSuppression = 0.85f;
        [Tooltip("Forward swim force multiplier while surface swimming.")]
        [SerializeField, Range(0.1f, 1f)] private float surfaceForwardForceMultiplier = 0.82f;
        [Tooltip("Strafe swim force multiplier while surface swimming.")]
        [SerializeField, Range(0.1f, 1f)] private float surfaceStrafeForceMultiplier = 0.72f;
        [Tooltip("Vertical swim force multiplier while surface swimming.")]
        [SerializeField, Range(0.1f, 1f)] private float surfaceVerticalForceMultiplier = 0.4f;
        [Tooltip("Extra drag applied while surface swimming.")]
        [SerializeField, Range(1f, 3f)] private float surfaceDragMultiplier = 1.35f;
        [Tooltip("Max speed multiplier while surface swimming.")]
        [SerializeField, Range(0.2f, 1f)] private float surfaceMaxSpeedMultiplier = 0.72f;
        [Tooltip("Depth window where upward surface escape is strongly damped.")]
        [SerializeField, Range(0.02f, 0.6f)] private float surfaceAscendReleaseDepth = 0.18f;
        [Tooltip("Damping applied to upward velocity at the top of the water.")]
        [SerializeField, Range(0f, 20f)] private float surfaceAscendVelocityDamping = 5f;
        [Tooltip("Minimum pitch-down angle that counts as deliberate surface dive intent.")]
        [SerializeField, Range(0f, 80f)] private float surfaceDivePitchCommit = 24f;
        [Tooltip("Minimum forward input that counts as deliberate surface dive intent.")]
        [SerializeField, Range(0f, 1f)] private float surfaceDiveForwardCommit = 0.35f;

        // ══════════════════════════════════════════════════════════
        //  INSPECTOR — CREST OCEAN INTEGRATION
        // ══════════════════════════════════════════════════════════

        [Header("── Crest Ocean Integration ───────────────────")]
        [Tooltip("Enable dynamic water height from Crest Ocean waves.")]
        [SerializeField] private bool useCrestOceanHeight = true;

        // ══════════════════════════════════════════════════════════
        //  INSPECTOR — GRADUATED GRAVITY
        // ══════════════════════════════════════════════════════════

        [Header("── Graduated Gravity ────────────────────────")]
        [SerializeField, Range(1f, 3f)]
        private float gravityFadeRate = 1.4f;

        [SerializeField, Range(1f, 5f)]
        private float snapFadeRate = 2.5f;

        // ══════════════════════════════════════════════════════════
        //  INSPECTOR — MOUSE LOOK
        // ══════════════════════════════════════════════════════════

        [Header("── Mouse Look ────────────────────────────────")]
        [SerializeField] private float mouseSensitivity = 0.12f;
        [SerializeField] private float pitchMin = -85f;
        [SerializeField] private float pitchMax = 85f;

        // ══════════════════════════════════════════════════════════
        //  INSPECTOR — SWIM VERTICAL DEFAULTS
        // ══════════════════════════════════════════════════════════

        [Header("── Control Scheme ───────────────────────────")]
        
        [Header("── Input System ─────────────────────────────")]

        [Header("── Swim Vertical (fallback если нет ControlScheme) ──")]





        // ══════════════════════════════════════════════════════════
        //  INSPECTOR — GROUND DETECTION
        // ══════════════════════════════════════════════════════════

        [Header("── Ground Detection ─────────────────────────")]
        [SerializeField] private float groundCheckRadius = 0.3f;
        [SerializeField] private float groundCheckDistance = 0.4f;
        [SerializeField, Range(5f, 89f)] private float maxGroundAngle = 60f;
        [SerializeField] private LayerMask groundLayers = ~0;
        [SerializeField, Range(1f, 2f)] private float slopeStabilityFactor = 1.1f;
        [SerializeField, Range(0f, 20f)] private float groundSnapForce = 8f;
        [SerializeField, Range(0f, 0.3f)] private float jumpBufferTime = 0.12f;
        [SerializeField, Range(0f, 0.3f)] private float dryGroundGraceTime = 0.12f;
        [SerializeField, Range(0f, 0.3f)] private float shoreGroundGraceTime = 0.14f;
        [SerializeField, Range(0f, 0.6f)] private float stepAssistHeight = 0.3f;
        [SerializeField, Range(0.05f, 0.8f)] private float stepAssistForwardDistance = 0.28f;
        [SerializeField, Range(0f, 0.2f)] private float stepAssistClearance = 0.04f;
        [SerializeField, Range(0f, 0.2f)] private float stepAssistCooldownTime = 0.06f;
        [SerializeField, Range(0f, 0.6f)] private float jumpHeadClearanceDistance = 0.18f;
        [SerializeField, Range(0.02f, 0.3f)] private float surfaceBreachDepthWindow = 0.12f;
        [SerializeField, Range(0.3f, 0.95f)] private float surfaceBreachMinImmersion = 0.45f;
        [SerializeField, Range(0.05f, 1f)] private float dryAirControlMultiplier = 0.4f;
        [SerializeField, Range(0f, 1f)] private float dryAirDampingMultiplier = 0.18f;

        // ══════════════════════════════════════════════════════════
        //  INSPECTOR — FOV
        // ══════════════════════════════════════════════════════════

        [Header("── FOV ───────────────────────────────────────")]
        [Tooltip("Base FOV of the camera. FOV compression applies relative to this.")]
        [SerializeField] private float baseFov = 70f;

        // ══════════════════════════════════════════════════════════
        //  INSPECTOR — DIAGNOSTICS
        // ══════════════════════════════════════════════════════════

        [Header("── Diagnostics ───────────────────────────────")]
        [SerializeField] private bool _debugIsWalking;
        [SerializeField] private string _debugLocomotionMode;
        [SerializeField] private bool _debugIsGrounded;
        [SerializeField] private float _debugImmersionRatio;
        [SerializeField] private float _debugSmoothedImmersion;
        [SerializeField] private float _debugGravityScale;
        [SerializeField] private float _debugSnapScale;
        [SerializeField] private float _debugBodyYaw;
        [SerializeField] private float _debugCameraYaw;
        [SerializeField] private float _debugCurrentRoll;
#pragma warning disable CS0414
        [SerializeField] private bool _debugStepEvent;
#pragma warning restore CS0414
        [SerializeField] private string _debugSuitName;
        [SerializeField] private float _debugSpeed;
        [SerializeField] private float _debugDynamicWaterY;
        [SerializeField] private bool _debugCrestAvailable;
        [SerializeField] private bool _debugCrestSampling;
        [SerializeField] private float _debugDepth;
        [SerializeField] private float _debugFovOffset;
        [SerializeField] private bool _debugSplashThisFrame;
        [SerializeField] private bool _debugExhaleThisFrame;
        [SerializeField] private bool _debugIsSubmerged;

        // ══════════════════════════════════════════════════════════
        //  CACHED REFERENCES
        // ══════════════════════════════════════════════════════════

        private Rigidbody _rb;
        private BuoyancyObject _buoyancy;
        private CapsuleCollider _capsuleCollider;
        private Transform _cachedTransform;
        private Camera _cameraComponent;
        private InputManager _inputManager;
        private InputManager _subscribedInputManager;
        private PlayerSwimPresentationController _swimPresentationController;

        // ══════════════════════════════════════════════════════════
        //  CREST OCEAN — runtime state
        // ══════════════════════════════════════════════════════════

        private Crest.SampleHeightHelper _crestHeightSampler;
        private bool _crestAvailable;
        private float _dynamicWaterSurfaceY;
        private bool _crestSamplingSucceeded;

        // ══════════════════════════════════════════════════════════
        //  CAMERA JUICE
        // ══════════════════════════════════════════════════════════

        private CameraJuiceProcessor _juiceProcessor;
        private CameraJuiceInput _juiceInput;
        private CameraJuiceOutput _juiceOutput;
        private Vector3 _cameraBaseLocalPos;

        // ══════════════════════════════════════════════════════════
        //  INPUT STATE
        // ══════════════════════════════════════════════════════════

        private float _inputH;
        private float _inputV;
        private float _inputVertical;
        private float _mouseXDelta;

        private float _cameraYaw;
        private float _cameraPitch;

        private bool _inputCleared;
        private bool _jumpRequested;
        private bool _isSprinting;
        private float _jumpBufferTimer;

        // ══════════════════════════════════════════════════════════
        //  BODY YAW (decoupled from camera)
        // ══════════════════════════════════════════════════════════

        private float _bodyYaw;
        private float _bodyYawVelocity;

        // ══════════════════════════════════════════════════════════
        //  MODE STATE
        // ══════════════════════════════════════════════════════════

        private bool _isWalking;
        private bool _isGrounded;
        private bool _wasGroundedLastFrame;
        private float _dryGroundGraceTimer;
        private float _shoreGroundGraceTimer;
        private float _stepAssistCooldownTimer;
        private float _currentFixedDeltaTime = 0.02f;
        private float _waterImmersionRatio;
        private float _smoothedImmersionRatio;
        private float _currentLinearDamping;
        private float _gravityScale;
        private float _snapScale;
        private float _currentDepth;  // v7.0: meters below water surface
        private bool _isSurfaceSwimming;
        private PlayerLocomotionMode _currentLocomotionMode = PlayerLocomotionMode.DryGroundWalk;
        private float _surfaceBreachLockTimer;

        // ══════════════════════════════════════════════════════════
        //  AMBIENT CURRENT
        // ══════════════════════════════════════════════════════════

        private float _currentTimer;

        // ══════════════════════════════════════════════════════════
        //  SPEED TRACKING
        // ══════════════════════════════════════════════════════════

        private float _prevSpeed;
        private float _prevYawForMomentum;

        // ══════════════════════════════════════════════════════════
        //  REGISTRATION
        // ══════════════════════════════════════════════════════════

        private bool _registeredTick;
        private bool _registeredFixedTick;

        // ══════════════════════════════════════════════════════════
        //  CACHED MATH
        // ══════════════════════════════════════════════════════════

        private Vector3 _moveDirection;
        private Vector3 _forceVector;
        private Vector3 _velocity;
        private Quaternion _cameraWorldRotation;

        private RaycastHit _groundHit;
        private Vector3 _groundCheckOrigin;
        private Vector3 _cachedGravity;
        private Vector3 _smoothedGroundNormal;
        private float _minGroundNormalY;
        private readonly RaycastHit[] _groundHitBuffer = new RaycastHit[8]; // COLD ALLOC: reused walkable-ground filter buffer for slope/wall separation

        // ══════════════════════════════════════════════════════════
        //  EVENTS
        // ══════════════════════════════════════════════════════════

        public event System.Action OnFootstep;

        /// <summary>Fired when a splash is detected. Float = intensity 0-1.</summary>
        public event System.Action<float> OnWaterSplash;

        /// <summary>Fired when head crosses submerge threshold. Bool = now submerged.</summary>
        public event System.Action<bool> OnSubmergeChange;

        /// <summary>Fired on each exhale cycle underwater. For bubble VFX / audio.</summary>
        public event System.Action OnExhale;

        // ══════════════════════════════════════════════════════════
        //  CONSTANTS
        // ══════════════════════════════════════════════════════════

        private const float DEG_TO_RAD = 0.01745329f;

        // ══════════════════════════════════════════════════════════
        //  EFFECTIVE WATER SURFACE — Crest or fallback
        // ══════════════════════════════════════════════════════════

        private float EffectiveWaterSurfaceY => (_crestAvailable && useCrestOceanHeight)
            ? _dynamicWaterSurfaceY
            : waterSurfaceY;

        // ══════════════════════════════════════════════════════════
        //  PUBLIC API
        // ══════════════════════════════════════════════════════════

        public void SetSuit(SuitData newSuit)
        {
            if (newSuit == null) { Debug.LogWarning("[HectonPlayerMovement] null suit.", this); return; }
            currentSuitData = newSuit;
            ApplySuitToRigidbody();
            EnsureJuiceProcessor();
            _juiceProcessor.Initialize(leanIntoTurn);
            UpdateSuitDiagnostics();
        }

        public SuitData CurrentSuit => currentSuitData;
        public float WaterImmersionRatio => _waterImmersionRatio;
        public bool IsGrounded => _isGrounded && _isWalking;
        public bool IsWalking => _isWalking;
        /// <summary>Resolved locomotion mode for movement, camera, audio, and VFX consumers.</summary>
        public PlayerLocomotionMode CurrentLocomotionMode => _currentLocomotionMode;
        public float CurrentRoll => _juiceProcessor != null ? _juiceProcessor.CurrentRoll : 0f;
        public float BodyYaw => _bodyYaw;
        public float CameraYaw => _cameraYaw;
        public float CurrentWaterSurfaceY => EffectiveWaterSurfaceY;
        public float CurrentDepth => _currentDepth;
        public bool IsPlayerSubmerged => _juiceProcessor != null && _juiceProcessor.IsSubmerged;

        // ══════════════════════════════════════════════════════════
        //  LIFECYCLE
        // ══════════════════════════════════════════════════════════

        private void Awake()
        {
            _cachedTransform = transform;

            _rb = GetComponent<Rigidbody>();
            TryGetComponent(out _capsuleCollider);
            TryGetComponent(out _buoyancy);
            TryGetComponent(out _swimPresentationController);

            _rb.interpolation = RigidbodyInterpolation.Interpolate;
            _rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            _rb.freezeRotation = true;
            _rb.constraints = RigidbodyConstraints.FreezeRotation;
            _rb.useGravity = false;

            // Cache camera component for FOV manipulation
            if (playerCamera != null)
                _cameraComponent = playerCamera.GetComponent<Camera>();

            Vector3 euler = _cachedTransform.eulerAngles;
            _cameraYaw = euler.y;
            _bodyYaw = euler.y;
            _bodyYawVelocity = 0f;

            if (playerCamera != null)
            {
                float camX = playerCamera.localEulerAngles.x;
                _cameraPitch = camX > 180f ? camX - 360f : camX;
                _cameraPitch = -_cameraPitch;
                _cameraPitch = math.clamp(_cameraPitch, pitchMin, pitchMax);
                _cameraBaseLocalPos = playerCamera.localPosition;
            }

            if (_cameraComponent != null)
                baseFov = _cameraComponent.fieldOfView;

            _cachedGravity = UnityEngine.Physics.gravity;
            _smoothedGroundNormal = Vector3.up;
            RefreshGroundSlopeCache();

            EnsureJuiceProcessor();
            _juiceProcessor.Initialize(leanIntoTurn);

            // ── Crest integration ──
            if (useCrestOceanHeight && _crestHeightSampler == null)
            {
                _crestHeightSampler = new Crest.SampleHeightHelper(); // COLD ALLOC: reused Crest sampler for the whole scene lifetime
            }

            _dynamicWaterSurfaceY = waterSurfaceY;
            _crestSamplingSucceeded = false;
            InitCrest();

            _waterImmersionRatio = ComputeImmersionRatio();
            _smoothedImmersionRatio = _waterImmersionRatio;
            _currentDepth = ComputeDepth();
            if (IsInDryInterior())
            {
                _waterImmersionRatio = 0f;
                _smoothedImmersionRatio = 0f;
                _currentDepth = 0f;
            }
            _isWalking = _waterImmersionRatio < swimTransitionThreshold;
            _prevSpeed = 0f;
            _prevYawForMomentum = _cameraYaw;
            _currentTimer = 0f;

            ApplySuitToRigidbody();

            _registeredTick = false;
            _registeredFixedTick = false;

            _inputManager = InputManager.Instance;
            UpdateSuitDiagnostics();
        }

        private void EnsureJuiceProcessor()
        {
            if (_juiceProcessor != null)
                return;

            _juiceProcessor = new CameraJuiceProcessor();
        }

        private void OnEnable() 
        { 
            RefreshInputManagerBinding();
            TryRegisterToTickManager(); 
        }

        private void Start()
        {
            if (_registeredTick && _registeredFixedTick) return;
            TryRegisterToTickManager();
            if (!_registeredTick || !_registeredFixedTick)
                Debug.LogError("[HectonPlayerMovement] GameTickManager.Instance is null.", this);

            if (useCrestOceanHeight && !_crestAvailable)
                InitCrest();
        }

        private void OnDisable()
        {
            GameTickManager inst = GameTickManager.Instance;
            UnsubscribeFromInput();

            if (inst == null) return;
            if (_registeredTick) { inst.Unregister((ITickable)this); _registeredTick = false; }
            if (_registeredFixedTick) { inst.Unregister((IFixedTickable)this); _registeredFixedTick = false; }
        }

        private void TryRegisterToTickManager()
        {
            GameTickManager inst = GameTickManager.Instance;
            if (inst == null) return;
            if (!_registeredTick) { inst.Register((ITickable)this); _registeredTick = true; }
            if (!_registeredFixedTick) { inst.Register((IFixedTickable)this); _registeredFixedTick = true; }
        }

        // ══════════════════════════════════════════════════════════
        //  COLLISION — camera shake integration
        // ══════════════════════════════════════════════════════════

        private void OnCollisionEnter(Collision collision)
        {
            if (_juiceProcessor == null || currentSuitData == null) return;
            if (!currentSuitData.enableCollisionShake) return;

            float relSpeed = collision.relativeVelocity.magnitude;
            _juiceProcessor.RegisterCollisionImpulse(relSpeed, currentSuitData);
        }

        // ══════════════════════════════════════════════════════════
        //  CREST OCEAN HEIGHT SAMPLING
        // ══════════════════════════════════════════════════════════

        private void InitCrest()
        {
            _crestAvailable = false;
            if (!useCrestOceanHeight) return;

            if (Crest.OceanRenderer.Instance != null)
            {
                _crestAvailable = true;
                _dynamicWaterSurfaceY = Crest.OceanRenderer.Instance.SeaLevel;
            }

            UpdateCrestDiagnostics();
        }

        private void UpdateCrestWaterHeight()
        {
            if (!useCrestOceanHeight) return;

            if (!_crestAvailable)
            {
                if (Crest.OceanRenderer.Instance != null)
                {
                    _crestAvailable = true;
                    _dynamicWaterSurfaceY = Crest.OceanRenderer.Instance.SeaLevel;
                    UpdateCrestDiagnostics();
                }
                else
                {
                    return;
                }
            }

            if (Crest.OceanRenderer.Instance == null)
            {
                _crestAvailable = false;
                UpdateCrestDiagnostics();
                return;
            }

            if (_crestHeightSampler == null)
            {
                _crestAvailable = false;
                UpdateCrestDiagnostics();
                return;
            }

            Vector3 samplePos = _rb.position;
            _crestHeightSampler.Init(samplePos, 0f, true);

            _crestSamplingSucceeded = _crestHeightSampler.Sample(out float waterHeight);

            if (_crestSamplingSucceeded)
            {
                _dynamicWaterSurfaceY = waterHeight;
            }
            else
            {
                _dynamicWaterSurfaceY = Crest.OceanRenderer.Instance.SeaLevel;
            }

            UpdateCrestDiagnostics();
        }

        // ══════════════════════════════════════════════════════════
        //  INPUT SYSTEM INTEGRATION (Zero GC)
        // ══════════════════════════════════════════════════════════

        private void SubscribeToInput()
        {
            if (_inputManager == null || _subscribedInputManager == _inputManager) return;

            _inputManager.OnJump += HandleJumpInput;
            _inputManager.OnSprint += HandleSprintStarted;
            _inputManager.OnSprintCanceled += HandleSprintCanceled;
            _subscribedInputManager = _inputManager;
        }

        private void UnsubscribeFromInput()
        {
            if (_subscribedInputManager == null) return;

            _subscribedInputManager.OnJump -= HandleJumpInput;
            _subscribedInputManager.OnSprint -= HandleSprintStarted;
            _subscribedInputManager.OnSprintCanceled -= HandleSprintCanceled;
            _subscribedInputManager = null;
        }

        private void RefreshInputManagerBinding()
        {
            InputManager currentManager = InputManager.Instance;
            if (ReferenceEquals(_inputManager, currentManager) &&
                ReferenceEquals(_subscribedInputManager, currentManager))
            {
                return;
            }

            UnsubscribeFromInput();
            _inputManager = currentManager;
            SubscribeToInput();
        }

        // ══════════════════════════════════════════════════════════
        //  SPRINT EVENTS (for CameraJuiceSystem integration)
        // ══════════════════════════════════════════════════════════

        public event System.Action OnSprintStarted;
        public event System.Action OnSprintEnded;

        private void HandleJumpInput()
        {
            _jumpRequested = true;
            _jumpBufferTimer = jumpBufferTime;
        }

        private void HandleSprintStarted()
        {
            _isSprinting = true;
            OnSprintStarted?.Invoke();
        }

        private void HandleSprintCanceled()
        {
            _isSprinting = false;
            OnSprintEnded?.Invoke();
        }

        // ══════════════════════════════════════════════════════════
        //  Tick — INPUT + CAMERA (render framerate)
        // ══════════════════════════════════════════════════════════

        public void Tick(float deltaTime)
        {
            SuitData suit = currentSuitData;
            if (suit == null) return;

            EnsureJuiceProcessor();

            RefreshInputManagerBinding();

            if (IsGameplayInputBlockedByMenu())
            {
                _inputH = 0f; _inputV = 0f; _inputVertical = 0f; _mouseXDelta = 0f;
                _isSprinting = false;
                _inputCleared = true;
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                BuildJuiceInput(deltaTime, suit);
                _juiceOutput = _juiceProcessor.Process(in _juiceInput, suit);
                ApplyCameraState();
                return;
            }

            if (_inputCleared || Cursor.lockState != CursorLockMode.Locked)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
                _inputCleared = false;
            }

            if (_inputManager != null && _inputManager.IsPlayerInputEnabled)
            {
                Vector2 lookDelta = _inputManager.LookInput;
                _mouseXDelta = lookDelta.x;
                _cameraYaw += lookDelta.x * mouseSensitivity;
                _cameraPitch -= lookDelta.y * mouseSensitivity;
                _cameraPitch = math.clamp(_cameraPitch, pitchMin, pitchMax);

                Vector2 moveInput = _inputManager.MoveInput;
                _inputH = moveInput.x;
                _inputV = moveInput.y;
                _inputVertical = _isWalking ? 0f : ResolveVerticalInput();
                _isSprinting = _inputManager.IsSprinting;
            }
            else
            {
                Vector2 lookDelta = ReadLookFallback();
                _mouseXDelta = lookDelta.x;
                _cameraYaw += lookDelta.x * mouseSensitivity;
                _cameraPitch -= lookDelta.y * mouseSensitivity;
                _cameraPitch = math.clamp(_cameraPitch, pitchMin, pitchMax);

                Vector2 moveInput = ReadMoveFallback();
                _inputH = moveInput.x;
                _inputV = moveInput.y;
                _inputVertical = _isWalking ? 0f : ReadVerticalFallbackKeys();
                _isSprinting = IsSprintFallbackHeld();
            }

            _velocity = _rb.linearVelocity;
            float currentSpeed = math.sqrt(
                _velocity.x * _velocity.x +
                _velocity.y * _velocity.y +
                _velocity.z * _velocity.z);
            float yawDelta = _cameraYaw - _prevYawForMomentum;

            BuildJuiceInput(deltaTime, suit);
            _juiceInput.speedDelta = currentSpeed - _prevSpeed;
            _juiceInput.yawDelta = yawDelta;
            _juiceOutput = _juiceProcessor.Process(in _juiceInput, suit);

            _prevSpeed = currentSpeed;
            _prevYawForMomentum = _cameraYaw;

            if (_juiceOutput.stepEvent)
            {
                OnFootstep?.Invoke();
                UpdateStepDiagnostics();
            }

            if (_juiceProcessor.SplashThisFrame)
            {
                OnWaterSplash?.Invoke(_juiceProcessor.SplashIntensity);
            }

            if (_juiceProcessor.SubmergeChangedThisFrame)
            {
                OnSubmergeChange?.Invoke(_juiceProcessor.IsSubmerged);
            }

            if (_juiceProcessor.ExhaleThisFrame)
            {
                OnExhale?.Invoke();
            }

            ApplyCameraState();
            UpdateDiagnostics(currentSpeed);

        }

        private void BuildJuiceInput(float deltaTime, SuitData suit)
        {
            _velocity = _rb.linearVelocity;
            _juiceInput.isWalking = _isWalking;
            _juiceInput.locomotionMode = _currentLocomotionMode;
            _juiceInput.isGrounded = _isGrounded;
            _juiceInput.hasMovementInput = _inputH != 0f || _inputV != 0f || _inputVertical != 0f;
            _juiceInput.inputH = _inputH;
            _juiceInput.mouseXDelta = _mouseXDelta;
            _juiceInput.horizontalSpeed = math.sqrt(_velocity.x * _velocity.x + _velocity.z * _velocity.z);
            _juiceInput.verticalVelocity = _velocity.y;
            _juiceInput.wasGroundedLastFrame = _wasGroundedLastFrame;
            _juiceInput.deltaTime = deltaTime;
            _juiceInput.immersionRatio = _waterImmersionRatio;

            // v7.0 additions
            _juiceInput.depth = _currentDepth;
            _juiceInput.swimSpeed = math.sqrt(
                _velocity.x * _velocity.x +
                _velocity.y * _velocity.y +
                _velocity.z * _velocity.z);
            _juiceInput.cameraPitch = _cameraPitch;
            _juiceInput.swimVerticalInput = _inputVertical;

            if (_swimPresentationController != null)
            {
                _juiceInput.swimPresentationMode = _swimPresentationController.CurrentMode;
                _juiceInput.swimStrokePhase = _swimPresentationController.CurrentStrokePhase;
                _juiceInput.swimPropulsionPulse = _swimPresentationController.CurrentPropulsionPulse;
                _juiceInput.swimGuideWeight = _swimPresentationController.CurrentGuideWeight;
            }
            else
            {
                _juiceInput.swimPresentationMode = PlayerSwimPresentationMode.None;
                _juiceInput.swimStrokePhase = 0f;
                _juiceInput.swimPropulsionPulse = 0f;
                _juiceInput.swimGuideWeight = 0f;
            }
        }

        private float ResolveVerticalInput()
        {
            float inputSystemVertical = _inputManager != null ? _inputManager.VerticalMovementInput : 0f;
            if (math.abs(inputSystemVertical) > 0.01f)
                return math.clamp(inputSystemVertical, -1f, 1f);

            return ReadVerticalFallbackKeys();
        }

        private float ReadVerticalFallbackKeys()
        {
            bool ascend =
                KeyHeld(controlScheme != null ? controlScheme.swimAscendPrimary : KeyCode.Space) ||
                KeyHeld(controlScheme != null ? controlScheme.swimAscendAlternate : KeyCode.None);

            bool descend =
                KeyHeld(controlScheme != null ? controlScheme.swimDescendPrimary : KeyCode.C) ||
                KeyHeld(controlScheme != null ? controlScheme.swimDescendAlternate : KeyCode.C) ||
                KeyHeld(controlScheme != null ? controlScheme.swimDescendLegacy : KeyCode.Q);

            if (ascend == descend)
                return 0f;

            return ascend ? 1f : -1f;
        }

        private static Vector2 ReadMoveFallback()
        {
            if (Keyboard.current == null)
                return Vector2.zero;

            float horizontal = 0f;
            float vertical = 0f;

            if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed)
                horizontal -= 1f;
            if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed)
                horizontal += 1f;
            if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed)
                vertical -= 1f;
            if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed)
                vertical += 1f;

            return Vector2.ClampMagnitude(new Vector2(horizontal, vertical), 1f);
        }

        private static Vector2 ReadLookFallback()
        {
            return Mouse.current != null ? Mouse.current.delta.ReadValue() : Vector2.zero;
        }

        private static bool IsSprintFallbackHeld()
        {
            return KeyHeld(KeyCode.LeftControl) ||
                   KeyHeld(KeyCode.RightControl) ||
                   KeyHeld(KeyCode.LeftShift) ||
                   KeyHeld(KeyCode.RightShift);
        }

        private static bool KeyHeld(KeyCode key)
        {
            if (key == KeyCode.None || Keyboard.current == null)
                return false;

            return key switch
            {
                KeyCode.Space => Keyboard.current.spaceKey.isPressed,
                KeyCode.LeftControl => Keyboard.current.leftCtrlKey.isPressed,
                KeyCode.RightControl => Keyboard.current.rightCtrlKey.isPressed,
                KeyCode.C => Keyboard.current.cKey.isPressed,
                KeyCode.Q => Keyboard.current.qKey.isPressed,
                KeyCode.E => Keyboard.current.eKey.isPressed,
                KeyCode.LeftShift => Keyboard.current.leftShiftKey.isPressed,
                KeyCode.RightShift => Keyboard.current.rightShiftKey.isPressed,
                _ => false,
            };
        }

        private void ApplyCameraState()
        {
            if (playerCamera == null) return;

            // v7.0a: direct camera pitch — pitch inertia removed (caused reverse jerk)
            float finalPitch = _cameraPitch + _juiceOutput.pitchOffset;
            finalPitch = math.clamp(finalPitch, pitchMin - 5f, pitchMax + 5f);
            float finalRoll = _juiceOutput.rollOffset;

            _cameraWorldRotation = Quaternion.Euler(finalPitch, _cameraYaw, finalRoll);
            playerCamera.rotation = _cameraWorldRotation;

            Vector3 finalPos;
            finalPos.x = _cameraBaseLocalPos.x + _juiceOutput.localPositionOffset.x;
            finalPos.y = _cameraBaseLocalPos.y + _juiceOutput.localPositionOffset.y;
            finalPos.z = _cameraBaseLocalPos.z + _juiceOutput.localPositionOffset.z;
            playerCamera.localPosition = finalPos;

            // FOV compression
            if (_cameraComponent != null)
            {
                float targetFov = baseFov + _juiceOutput.fovOffset;
                _cameraComponent.fieldOfView = math.lerp(
                    _cameraComponent.fieldOfView, targetFov,
                    1f - math.exp(-8f * _juiceInput.deltaTime));
            }
        }

        private static bool IsGameplayInputBlockedByMenu()
        {
            return HectonFabricatorUI.IsMenuOpen || PlayerPDA.IsOpen || PauseMenuController.IsAnyOpen;
        }

        // ══════════════════════════════════════════════════════════
        //  FixedTick — PHYSICS
        // ══════════════════════════════════════════════════════════

        public void FixedTick(float fixedDeltaTime)
        {
            SuitData suit = currentSuitData;
            if (suit == null) return;

            EnsureJuiceProcessor();
            _currentFixedDeltaTime = fixedDeltaTime;

            // ═══════════════════════════════════════════════
            //  1. FORCE-OVERRIDE: BuoyancyObject-proof
            // ═══════════════════════════════════════════════
            _rb.useGravity = false;

            // ═══════════════════════════════════════════════
            //  2. BODY YAW SPRING
            // ═══════════════════════════════════════════════
            if (_isWalking)
            {
                _bodyYaw = _cameraYaw;
                _bodyYawVelocity = 0f;
            }
            else
            {
                _bodyYaw = SpringDamp(_bodyYaw, _cameraYaw, ref _bodyYawVelocity,
                    suit.bodyYawSpringOmega, fixedDeltaTime);
            }

            // ═══════════════════════════════════════════════
            //  3. PRE-IMPACT VELOCITY TRACKING
            // ═══════════════════════════════════════════════
            _juiceProcessor.TrackVerticalVelocity(_rb.linearVelocity.y);
            _wasGroundedLastFrame = _isGrounded;

            // ═══════════════════════════════════════════════
            //  4. GROUND CHECK
            // ═══════════════════════════════════════════════
            GroundCheck();

            // ═══════════════════════════════════════════════
            //  5. CREST HEIGHT + WATER IMMERSION + DEPTH
            // ═══════════════════════════════════════════════
            UpdateCrestWaterHeight();
            _waterImmersionRatio = ComputeImmersionRatio();
            _currentDepth = ComputeDepth();

            if (IsInDryInterior())
            {
                _waterImmersionRatio = 0f;
                _smoothedImmersionRatio = 0f;
                _currentDepth = 0f;
            }

            // ═══════════════════════════════════════════════
            //  6. SMOOTHED IMMERSION + GROUNDED OVERRIDE
            // ═══════════════════════════════════════════════
            if (_waterImmersionRatio > _smoothedImmersionRatio)
            {
                float enterT = 1f - math.exp(-12f * fixedDeltaTime);
                _smoothedImmersionRatio = math.lerp(_smoothedImmersionRatio, _waterImmersionRatio, enterT);
            }
            else
            {
                float exitT = 1f - math.exp(-3f * fixedDeltaTime);
                _smoothedImmersionRatio = math.lerp(_smoothedImmersionRatio, _waterImmersionRatio, exitT);
            }

            float physicsImmersion = _smoothedImmersionRatio;
            bool isShallowEnoughForShore = physicsImmersion < swimTransitionThreshold;
            bool isDryLand = physicsImmersion <= 0.01f;
            if (_isGrounded && isDryLand)
            {
                _dryGroundGraceTimer = dryGroundGraceTime;
            }
            else if (_dryGroundGraceTimer > 0f)
            {
                _dryGroundGraceTimer -= fixedDeltaTime;
                if (_dryGroundGraceTimer < 0f)
                    _dryGroundGraceTimer = 0f;
            }

            if (_isGrounded && isShallowEnoughForShore)
            {
                _shoreGroundGraceTimer = shoreGroundGraceTime;
            }
            else if (_shoreGroundGraceTimer > 0f)
            {
                _shoreGroundGraceTimer -= fixedDeltaTime;
                if (_shoreGroundGraceTimer < 0f)
                    _shoreGroundGraceTimer = 0f;
            }

            if (_jumpBufferTimer > 0f)
            {
                _jumpBufferTimer -= fixedDeltaTime;
                if (_jumpBufferTimer <= 0f)
                {
                    _jumpBufferTimer = 0f;
                    _jumpRequested = false;
                }
            }

            if (_surfaceBreachLockTimer > 0f)
            {
                _surfaceBreachLockTimer -= fixedDeltaTime;
                if (_surfaceBreachLockTimer < 0f)
                    _surfaceBreachLockTimer = 0f;
            }

            if (_stepAssistCooldownTimer > 0f)
            {
                _stepAssistCooldownTimer -= fixedDeltaTime;
                if (_stepAssistCooldownTimer < 0f)
                    _stepAssistCooldownTimer = 0f;
            }

            bool hasDryGroundSupport = _isGrounded || (_dryGroundGraceTimer > 0f && isDryLand);
            bool hasShoreGroundSupport = _isGrounded || (_shoreGroundGraceTimer > 0f && isShallowEnoughForShore);
            bool groundedOnDryLand = hasDryGroundSupport && isDryLand;
            bool groundedOnShore = hasShoreGroundSupport && isShallowEnoughForShore;

            // ═══════════════════════════════════════════════
            //  7A. GRADUATED GRAVITY
            // ═══════════════════════════════════════════════
            if (groundedOnShore)
            {
                _gravityScale = 1f;
            }
            else
            {
                _gravityScale = 1f - math.saturate(physicsImmersion * gravityFadeRate);
            }

            if (_gravityScale > 0.001f)
            {
                float mass = _rb.mass;
                _forceVector.x = _cachedGravity.x * mass * _gravityScale;
                _forceVector.y = _cachedGravity.y * mass * _gravityScale;
                _forceVector.z = _cachedGravity.z * mass * _gravityScale;
                _rb.AddForce(_forceVector, ForceMode.Force);
            }

            // ═══════════════════════════════════════════════
            //  7B. GROUND SNAP SCALE
            // ═══════════════════════════════════════════════
            if (groundedOnShore)
            {
                _snapScale = 1f;
            }
            else
            {
                _snapScale = 1f - math.saturate(physicsImmersion * snapFadeRate);
            }

            // ═══════════════════════════════════════════════
            //  8. MODE DETECTION
            // ═══════════════════════════════════════════════
            bool shouldWalk = ShouldUseLandLocomotion(physicsImmersion, hasShoreGroundSupport);

            if (shouldWalk != _isWalking)
            {
                _isWalking = shouldWalk;
                ApplyModePhysics(suit);
                UpdateModeDiagnostics();
            }

            _isSurfaceSwimming = !_isWalking && IsSurfaceSwimBand(physicsImmersion);
            _currentLocomotionMode = ResolveLocomotionMode(physicsImmersion);
            UpdateModeDiagnostics();

            // ═══════════════════════════════════════════════
            //  9. DAMPING TRANSITION
            // ═══════════════════════════════════════════════
            SmoothDampingTransition(fixedDeltaTime, suit);

            // ═══════════════════════════════════════════════
            //  10. JUMP
            // ═══════════════════════════════════════════════
            if (_jumpRequested)
            {
                if ((groundedOnDryLand || groundedOnShore) && _jumpBufferTimer > 0f)
                {
                    if (TryApplyJumpImpulse(suit.jumpImpulse))
                    {
                        ConsumeJumpRequest();
                        _dryGroundGraceTimer = 0f;
                        _shoreGroundGraceTimer = 0f;
                        _surfaceBreachLockTimer = 0f;
                    }
                }
            }

            // ═══════════════════════════════════════════════
            //  11. MOVEMENT + FORCES
            // ═══════════════════════════════════════════════
            if (_isWalking)
            {
                bool hasLandInput = _inputH != 0f || _inputV != 0f;
                WalkPhysics(suit, fixedDeltaTime);

                if (hasLandInput)
                    TryApplyStepAssist(groundedOnDryLand, groundedOnShore);

                if (_isGrounded && _snapScale > 0.001f)
                    ApplyGroundStability(_snapScale);
            }
            else
            {
                SwimPhysics(suit, fixedDeltaTime);

                if (_isSurfaceSwimming)
                    ApplySurfaceLock(suit);

                if (_waterImmersionRatio > 0.3f)
                    ApplyAmbientCurrent(suit, fixedDeltaTime);
            }

            // ═══════════════════════════════════════════════
            //  12. VELOCITY CLAMP
            // ═══════════════════════════════════════════════
            ClampVelocity(suit);
            UpdateGroundDiagnostics();
        }

        // ══════════════════════════════════════════════════════════
        //  WATER IMMERSION + DEPTH
        // ══════════════════════════════════════════════════════════

        private float ComputeImmersionRatio()
        {
            float surfaceY = EffectiveWaterSurfaceY;
            float feetY = GetBodyBottomY();
            float headY = GetBodyTopY();

            if (feetY >= surfaceY) return 0f;
            if (headY <= surfaceY) return 1f;

            return math.clamp((surfaceY - feetY) / playerHeight, 0f, 1f);
        }

        /// <summary>
        /// Depth in meters below water surface. 0 = at surface. Positive = deeper.
        /// Returns 0 if above water.
        /// </summary>
        private float ComputeDepth()
        {
            float surfaceY = EffectiveWaterSurfaceY;
            float eyeY = GetBodyEyeY();
            float depth = surfaceY - eyeY;
            return depth > 0f ? depth : 0f;
        }

        private bool IsInDryInterior()
        {
            return _buoyancy != null && _buoyancy.IsInDryZone;
        }

        private bool IsSurfaceSwimBand(float physicsImmersion)
        {
            if (IsInDryInterior())
                return false;

            if (physicsImmersion < 0.3f || physicsImmersion >= 0.999f)
                return false;

            return _currentDepth <= surfaceSwimDepthBand;
        }

        private PlayerLocomotionMode ResolveLocomotionMode(float physicsImmersion)
        {
            if (IsInDryInterior())
                return PlayerLocomotionMode.DryInteriorWalk;

            if (_isWalking)
            {
                if (physicsImmersion > 0.01f)
                    return PlayerLocomotionMode.ShallowWadeWalk;

                return PlayerLocomotionMode.DryGroundWalk;
            }

            return _isSurfaceSwimming
                ? PlayerLocomotionMode.SurfaceSwim
                : PlayerLocomotionMode.UnderwaterSwim;
        }

        private bool HasSurfaceDiveIntent()
        {
            if (_inputVertical < -0.1f)
                return true;

            return _inputV > surfaceDiveForwardCommit && _cameraPitch >= surfaceDivePitchCommit;
        }

        // ══════════════════════════════════════════════════════════
        //  SUIT APPLICATION
        // ══════════════════════════════════════════════════════════

        private void ApplySuitToRigidbody()
        {
            if (currentSuitData == null) return;
            _rb.mass = currentSuitData.mass;
            _rb.useGravity = false;

            if (_isWalking)
            {
                _currentLinearDamping = currentSuitData.walkDrag;
                _rb.linearDamping = _currentLinearDamping;
            }
            else
            {
                _currentLinearDamping = 0f;
                _rb.linearDamping = 0f;
            }
        }

        private void ApplyModePhysics(SuitData suit)
        {
            if (!_isWalking)
            {
                _rb.linearDamping = 0f;
                _currentLinearDamping = 0f;
            }
        }

        private void SmoothDampingTransition(float fixedDeltaTime, SuitData suit)
        {
            float targetDamping;
            if (_isWalking)
            {
                float wadeFactor = 1f + _waterImmersionRatio * suit.wadeSlowdownFactor;
                targetDamping = suit.walkDrag * wadeFactor;

                if (IsDryLandAirborne())
                    targetDamping *= dryAirDampingMultiplier;
            }
            else
            {
                targetDamping = 0f;
            }

            if (math.abs(_currentLinearDamping - targetDamping) > 0.01f)
            {
                float t = 1f - math.exp(-suit.dampingTransitionSpeed * fixedDeltaTime);
                _currentLinearDamping = math.lerp(_currentLinearDamping, targetDamping, t);
                _rb.linearDamping = _currentLinearDamping;
            }
            else if (_currentLinearDamping != targetDamping)
            {
                _currentLinearDamping = targetDamping;
                _rb.linearDamping = targetDamping;
            }
        }

        // ══════════════════════════════════════════════════════════
        //  GROUND DETECTION + SMOOTHED NORMAL
        // ══════════════════════════════════════════════════════════

        private void GroundCheck()
        {
            GroundCheck(_currentFixedDeltaTime);
        }

        private void GroundCheck(float fixedDeltaTime)
        {
            Vector3 rbPos = _rb.position;
            float bodyBottomY = GetBodyBottomY();
            _groundCheckOrigin.x = rbPos.x;
            _groundCheckOrigin.y = bodyBottomY + groundCheckRadius + GroundCheckSkin;
            _groundCheckOrigin.z = rbPos.z;

            int hitCount = UnityEngine.Physics.SphereCastNonAlloc(
                _groundCheckOrigin,
                groundCheckRadius,
                Vector3.down,
                _groundHitBuffer,
                groundCheckDistance + GroundCheckSkin,
                groundLayers,
                QueryTriggerInteraction.Ignore);

            _isGrounded = false;
            float bestDistance = float.MaxValue;
            float bestNormalY = _minGroundNormalY;

            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit hit = _groundHitBuffer[i];
                Collider hitCollider = hit.collider;
                if (hitCollider == null)
                    continue;

                if (hitCollider.attachedRigidbody == _rb)
                    continue;

                float normalY = hit.normal.y;
                if (normalY < _minGroundNormalY)
                    continue;

                if (!_isGrounded || hit.distance < bestDistance || (math.abs(hit.distance - bestDistance) <= 0.001f && normalY > bestNormalY))
                {
                    _groundHit = hit;
                    bestDistance = hit.distance;
                    bestNormalY = normalY;
                    _isGrounded = true;
                }
            }

            if (_isGrounded)
            {
                float normalT = 1f - math.exp(-15f * fixedDeltaTime);
                _smoothedGroundNormal = Vector3.Slerp(_smoothedGroundNormal, _groundHit.normal, normalT);

                float sqrMag = _smoothedGroundNormal.sqrMagnitude;
                if (sqrMag > 0.001f && math.abs(sqrMag - 1f) > 0.001f)
                {
                    _smoothedGroundNormal = _smoothedGroundNormal.normalized;
                }
            }
            else
            {
                _groundHit = default;
                float resetT = 1f - math.exp(-5f * fixedDeltaTime);
                _smoothedGroundNormal = Vector3.Slerp(_smoothedGroundNormal, Vector3.up, resetT);
            }
        }

        // ══════════════════════════════════════════════════════════
        //  GROUND STABILITY
        // ══════════════════════════════════════════════════════════

        private void ApplyGroundStability(float scale)
        {
            if (scale <= 0.001f) return;

            float mass = _rb.mass;
            float gravityAlongNormal = Vector3.Dot(_cachedGravity, _smoothedGroundNormal);
            _forceVector.x = (_smoothedGroundNormal.x * gravityAlongNormal) - _cachedGravity.x;
            _forceVector.y = (_smoothedGroundNormal.y * gravityAlongNormal) - _cachedGravity.y;
            _forceVector.z = (_smoothedGroundNormal.z * gravityAlongNormal) - _cachedGravity.z;

            float tangentSqr = _forceVector.x * _forceVector.x + _forceVector.y * _forceVector.y + _forceVector.z * _forceVector.z;
            if (tangentSqr > 0.000001f)
            {
                float slopeHoldForce = mass * _gravityScale * scale;
                _forceVector.x *= slopeHoldForce;
                _forceVector.y *= slopeHoldForce;
                _forceVector.z *= slopeHoldForce;
                _rb.AddForce(_forceVector, ForceMode.Force);
            }

            float gravityIntoGround = Vector3.Dot(-_cachedGravity, _smoothedGroundNormal);
            if (gravityIntoGround > 0f)
            {
                float supportForce = gravityIntoGround * mass * slopeStabilityFactor * scale;
                _forceVector.x = _smoothedGroundNormal.x * supportForce;
                _forceVector.y = _smoothedGroundNormal.y * supportForce;
                _forceVector.z = _smoothedGroundNormal.z * supportForce;
                _rb.AddForce(_forceVector, ForceMode.Force);
            }

            if (groundSnapForce > 0f)
            {
                float snapForce = groundSnapForce * mass * scale;
                _forceVector.x = -_smoothedGroundNormal.x * snapForce;
                _forceVector.y = -_smoothedGroundNormal.y * snapForce;
                _forceVector.z = -_smoothedGroundNormal.z * snapForce;
                _rb.AddForce(_forceVector, ForceMode.Force);
            }
        }

        // ══════════════════════════════════════════════════════════
        //  SURFACE LOCK
        // ══════════════════════════════════════════════════════════

        private void ApplySurfaceLock(SuitData suit)
        {
            if (suit.surfaceLockStrength <= 0f) return;
            if (_surfaceBreachLockTimer > 0f) return;
            if (!_isSurfaceSwimming) return;
            if (IsInDryInterior()) return;

            if (HasSurfaceDiveIntent()) return;

            if (_isGrounded) return;
            if (_shoreGroundGraceTimer > 0f && _smoothedImmersionRatio < swimTransitionThreshold) return;

            float surfaceY = EffectiveWaterSurfaceY;
            float feetY = GetBodyBottomY();

            if (feetY >= surfaceY - 0.1f) return;

            float eyeY = GetBodyEyeY();
            float error = eyeY - surfaceY;

            if (math.abs(error) > suit.surfaceLockRange) return;

            float shoreBlend = math.saturate((_waterImmersionRatio - 0.5f) * 2.5f);

            float springForce = -error * suit.surfaceLockStrength * shoreBlend;
            float dampForce = -_rb.linearVelocity.y * suit.surfaceLockDamping * shoreBlend;
            float totalForce = (springForce + dampForce) * _rb.mass;

            _forceVector.x = 0f;
            _forceVector.y = totalForce;
            _forceVector.z = 0f;
            _rb.AddForce(_forceVector, ForceMode.Force);
        }

        private float GetBodyBottomY()
        {
            if (_capsuleCollider != null)
                return _capsuleCollider.bounds.min.y;

            return _rb.position.y - playerHeight * 0.5f;
        }

        private float GetBodyTopY()
        {
            if (_capsuleCollider != null)
                return _capsuleCollider.bounds.max.y;

            return _rb.position.y + playerHeight * 0.5f;
        }

        private float GetBodyEyeY()
        {
            return math.lerp(GetBodyBottomY(), GetBodyTopY(), 0.85f);
        }

        private bool TryGetCapsuleCastGeometry(float inset, out Vector3 point1, out Vector3 point2, out float radius)
        {
            if (_capsuleCollider != null)
            {
                Bounds bounds = _capsuleCollider.bounds;
                float extentsX = bounds.extents.x;
                float extentsZ = bounds.extents.z;
                radius = math.max(0.01f, math.min(extentsX, extentsZ) - inset);
                float segmentHalf = math.max(0f, bounds.extents.y - radius - inset);
                Vector3 center = bounds.center;
                point1 = center + Vector3.up * segmentHalf;
                point2 = center - Vector3.up * segmentHalf;
                return true;
            }

            radius = math.max(groundCheckRadius - inset, 0.01f);
            float halfHeight = math.max(playerHeight * 0.5f - radius - inset, 0f);
            Vector3 centerFallback = _rb.position;
            point1 = centerFallback + Vector3.up * halfHeight;
            point2 = centerFallback - Vector3.up * halfHeight;
            return true;
        }

        private void RefreshGroundSlopeCache()
        {
            _minGroundNormalY = math.cos(maxGroundAngle * DEG_TO_RAD);
        }

        private void ConsumeJumpRequest()
        {
            _jumpRequested = false;
            _jumpBufferTimer = 0f;
        }

        private bool TryApplyJumpImpulse(float impulse)
        {
            if (impulse <= 0f)
                return false;

            if (!HasJumpHeadClearance())
                return false;

            _velocity = _rb.linearVelocity;
            if (_velocity.y < 0f)
            {
                _velocity.y = 0f;
                _rb.linearVelocity = _velocity;
            }

            _isGrounded = false;
            _wasGroundedLastFrame = false;
            _snapScale = 0f;
            _dryGroundGraceTimer = 0f;
            _shoreGroundGraceTimer = 0f;

            if (_juiceProcessor != null)
                _juiceProcessor.RegisterLandJumpLaunch();

            _rb.AddForce(Vector3.up * impulse, ForceMode.VelocityChange);
            return true;
        }

        private bool HasJumpHeadClearance()
        {
            if (jumpHeadClearanceDistance <= 0f)
                return true;

            if (!TryGetCapsuleCastGeometry(0.02f, out Vector3 point1, out Vector3 point2, out float radius))
                return true;

            int hitCount = UnityEngine.Physics.CapsuleCastNonAlloc(
                point1,
                point2,
                radius,
                Vector3.up,
                _groundHitBuffer,
                jumpHeadClearanceDistance,
                groundLayers,
                QueryTriggerInteraction.Ignore);

            for (int i = 0; i < hitCount; i++)
            {
                Collider hitCollider = _groundHitBuffer[i].collider;
                if (hitCollider == null)
                    continue;

                if (hitCollider.attachedRigidbody == _rb)
                    continue;

                return false;
            }

            return true;
        }

        private bool TryApplyStepAssist(bool groundedOnDryLand, bool groundedOnShore)
        {
            if (stepAssistHeight <= 0f || stepAssistForwardDistance <= 0f)
                return false;

            if (_stepAssistCooldownTimer > 0f)
                return false;

            if (!(groundedOnDryLand || groundedOnShore || _isGrounded))
                return false;

            if (_rb.linearVelocity.y > 0.5f)
                return false;

            float dirX = _moveDirection.x;
            float dirZ = _moveDirection.z;
            float planarSqr = dirX * dirX + dirZ * dirZ;
            if (planarSqr <= 0.0001f)
                return false;

            float invPlanarMag = 1f / math.sqrt(planarSqr);
            dirX *= invPlanarMag;
            dirZ *= invPlanarMag;

            float probeRadius = math.max(groundCheckRadius * 0.85f, 0.05f);
            float currentBottomY = GetBodyBottomY();

            _groundCheckOrigin.x = _rb.position.x;
            _groundCheckOrigin.y = currentBottomY + probeRadius + GroundCheckSkin;
            _groundCheckOrigin.z = _rb.position.z;

            if (!TryFindStepObstacle(_groundCheckOrigin, probeRadius, dirX, dirZ, out RaycastHit obstacleHit))
                return false;

            float forwardDistance = math.min(stepAssistForwardDistance, math.max(obstacleHit.distance + stepAssistClearance, probeRadius));

            Vector3 raisedOrigin = _groundCheckOrigin;
            raisedOrigin.y += stepAssistHeight;

            if (HasForwardBlockAtHeight(raisedOrigin, probeRadius, dirX, dirZ, forwardDistance))
                return false;

            Vector3 landingOrigin;
            landingOrigin.x = raisedOrigin.x + dirX * forwardDistance;
            landingOrigin.y = raisedOrigin.y;
            landingOrigin.z = raisedOrigin.z + dirZ * forwardDistance;

            if (!TryFindStepLanding(landingOrigin, probeRadius, out RaycastHit landingHit))
                return false;

            float landedCenterY = landingOrigin.y - landingHit.distance;
            float targetBottomY = landedCenterY - probeRadius;
            float stepDeltaY = targetBottomY - currentBottomY;
            if (stepDeltaY <= GroundCheckSkin || stepDeltaY > stepAssistHeight + GroundCheckSkin)
                return false;

            Vector3 newPosition = _rb.position;
            newPosition.x += dirX * forwardDistance;
            newPosition.y += stepDeltaY;
            newPosition.z += dirZ * forwardDistance;
            _rb.position = newPosition;

            _velocity = _rb.linearVelocity;
            if (_velocity.y < 0f)
            {
                _velocity.y = 0f;
                _rb.linearVelocity = _velocity;
            }

            _stepAssistCooldownTimer = stepAssistCooldownTime;
            _dryGroundGraceTimer = dryGroundGraceTime;
            if (groundedOnShore)
                _shoreGroundGraceTimer = shoreGroundGraceTime;

            GroundCheck();
            _waterImmersionRatio = ComputeImmersionRatio();
            _currentDepth = ComputeDepth();
            return true;
        }

        private bool TryFindStepObstacle(Vector3 origin, float radius, float dirX, float dirZ, out RaycastHit obstacleHit)
        {
            obstacleHit = default;
            Vector3 direction = new Vector3(dirX, 0f, dirZ);
            int hitCount = UnityEngine.Physics.SphereCastNonAlloc(
                origin,
                radius,
                direction,
                _groundHitBuffer,
                stepAssistForwardDistance,
                groundLayers,
                QueryTriggerInteraction.Ignore);

            float bestDistance = float.MaxValue;
            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit hit = _groundHitBuffer[i];
                Collider hitCollider = hit.collider;
                if (hitCollider == null)
                    continue;

                if (hitCollider.attachedRigidbody == _rb)
                    continue;

                if (hit.distance <= 0.001f)
                    continue;

                if (hit.normal.y >= _minGroundNormalY)
                    continue;

                if (hit.distance < bestDistance)
                {
                    bestDistance = hit.distance;
                    obstacleHit = hit;
                }
            }

            return bestDistance < float.MaxValue;
        }

        private bool HasForwardBlockAtHeight(Vector3 origin, float radius, float dirX, float dirZ, float distance)
        {
            Vector3 direction = new Vector3(dirX, 0f, dirZ);
            int hitCount = UnityEngine.Physics.SphereCastNonAlloc(
                origin,
                radius,
                direction,
                _groundHitBuffer,
                distance,
                groundLayers,
                QueryTriggerInteraction.Ignore);

            for (int i = 0; i < hitCount; i++)
            {
                Collider hitCollider = _groundHitBuffer[i].collider;
                if (hitCollider == null)
                    continue;

                if (hitCollider.attachedRigidbody == _rb)
                    continue;

                return true;
            }

            return false;
        }

        private bool TryFindStepLanding(Vector3 origin, float radius, out RaycastHit landingHit)
        {
            landingHit = default;
            int hitCount = UnityEngine.Physics.SphereCastNonAlloc(
                origin,
                radius,
                Vector3.down,
                _groundHitBuffer,
                stepAssistHeight + radius + GroundCheckSkin,
                groundLayers,
                QueryTriggerInteraction.Ignore);

            float bestDistance = float.MaxValue;
            float bestNormalY = _minGroundNormalY;
            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit hit = _groundHitBuffer[i];
                Collider hitCollider = hit.collider;
                if (hitCollider == null)
                    continue;

                if (hitCollider.attachedRigidbody == _rb)
                    continue;

                float normalY = hit.normal.y;
                if (normalY < _minGroundNormalY)
                    continue;

                if (hit.distance < bestDistance || (math.abs(hit.distance - bestDistance) <= 0.001f && normalY > bestNormalY))
                {
                    bestDistance = hit.distance;
                    bestNormalY = normalY;
                    landingHit = hit;
                }
            }

            return bestDistance < float.MaxValue;
        }

        // ══════════════════════════════════════════════════════════
        //  SWIM PHYSICS — with depth pressure resistance
        // ══════════════════════════════════════════════════════════

        private void SwimPhysics(SuitData suit, float fixedDeltaTime)
        {
            _velocity = _rb.linearVelocity;
            float speed = math.sqrt(
                _velocity.x * _velocity.x +
                _velocity.y * _velocity.y +
                _velocity.z * _velocity.z);
            bool isSurfaceSwim = _isSurfaceSwimming;
            bool hasSurfaceDiveIntent = isSurfaceSwim && HasSurfaceDiveIntent();

            // ── Depth-based drag increase (v7.0) ──
            float depthDragAdd = 0f;
            if (_currentDepth > suit.depthSwimSlowdownStart && suit.depthDragIncreaseMax > 0f)
            {
                float depthT = math.saturate(
                    (_currentDepth - suit.depthSwimSlowdownStart) /
                    math.max(suit.depthSwimSlowdownEnd - suit.depthSwimSlowdownStart, 0.01f));
                depthDragAdd = depthT * suit.depthDragIncreaseMax;
            }

            float effectiveDragCoeff = suit.swimDragCoefficient + depthDragAdd;
            if (isSurfaceSwim)
                effectiveDragCoeff *= surfaceDragMultiplier;

            // ── Quadratic drag ──
            if (speed > 0.01f)
            {
                float dragMagnitude = effectiveDragCoeff * speed * speed;
                float maxDrag = speed * _rb.mass * 0.9f / fixedDeltaTime;
                if (dragMagnitude > maxDrag) dragMagnitude = maxDrag;

                float invSpeed = 1f / speed;
                _forceVector.x = -_velocity.x * invSpeed * dragMagnitude;
                _forceVector.y = -_velocity.y * invSpeed * dragMagnitude;
                _forceVector.z = -_velocity.z * invSpeed * dragMagnitude;
                _rb.AddForce(_forceVector, ForceMode.Force);
            }

            // ── Swim thrust ──
            bool hasInput = _inputH != 0f || _inputV != 0f || _inputVertical != 0f;
            if (!hasInput) return;

            // ── Depth-based swim force reduction (v7.0) ──
            float depthSlowdown = 1f;
            if (_currentDepth > suit.depthSwimSlowdownStart && suit.depthSwimSlowdownMax > 0f)
            {
                float slowT = math.saturate(
                    (_currentDepth - suit.depthSwimSlowdownStart) /
                    math.max(suit.depthSwimSlowdownEnd - suit.depthSwimSlowdownStart, 0.01f));
                depthSlowdown = 1f - slowT * suit.depthSwimSlowdownMax;
            }

            float sprintMult = _isSprinting ? suit.sprintMultiplier : 1f;
            float effectiveSwimForce = suit.swimForce * depthSlowdown * sprintMult;
            float effectiveVerticalForce = suit.swimVerticalForce * depthSlowdown * sprintMult;

            float bodyYawRad = _bodyYaw * DEG_TO_RAD;
            float pitchRad = _cameraPitch * DEG_TO_RAD;

            float sinBodyYaw = math.sin(bodyYawRad);
            float cosBodyYaw = math.cos(bodyYawRad);
            float sinPitch = math.sin(pitchRad);
            float cosPitch = math.cos(pitchRad);

            float surfaceDepthT = isSurfaceSwim
                ? math.saturate(_currentDepth / math.max(surfaceSwimDepthBand, 0.01f))
                : 1f;
            float surfacePitchBlend = isSurfaceSwim
                ? math.lerp(1f - surfaceForwardPitchSuppression, 1f, surfaceDepthT)
                : 1f;

            float fwdPlanarScale = math.lerp(1f, cosPitch, surfacePitchBlend);
            float fwdX = sinBodyYaw * fwdPlanarScale;
            float fwdY = (!isSurfaceSwim || hasSurfaceDiveIntent) ? -sinPitch : 0f;
            float fwdZ = cosBodyYaw * fwdPlanarScale;

            float rightX = cosBodyYaw;
            float rightZ = -sinBodyYaw;

            float forwardScale = isSurfaceSwim ? surfaceForwardForceMultiplier : 1f;
            float strafeScale = isSurfaceSwim ? surfaceStrafeForceMultiplier : 1f;

            float dirX = fwdX * (_inputV * forwardScale) + rightX * (_inputH * strafeScale);
            float dirY = fwdY * (_inputV * forwardScale);
            float dirZ = fwdZ * (_inputV * forwardScale) + rightZ * (_inputH * strafeScale);

            float sqrMag = dirX * dirX + dirY * dirY + dirZ * dirZ;
            if (sqrMag > 1.0001f)
            {
                float invMag = 1f / math.sqrt(sqrMag);
                dirX *= invMag; dirY *= invMag; dirZ *= invMag;
            }

            float verticalInput = _inputVertical;
            if (isSurfaceSwim && verticalInput > 0f)
            {
                float ascendGate = math.saturate(_currentDepth / math.max(surfaceAscendReleaseDepth, 0.01f));
                verticalInput *= ascendGate;
            }

            _forceVector.x = dirX * effectiveSwimForce;
            _forceVector.y = dirY * effectiveSwimForce;
            _forceVector.z = dirZ * effectiveSwimForce;
            _forceVector.y += verticalInput * effectiveVerticalForce * (isSurfaceSwim ? surfaceVerticalForceMultiplier : 1f);

            _rb.AddForce(_forceVector, ForceMode.Force);

            if (isSurfaceSwim && surfaceAscendVelocityDamping > 0f && _velocity.y > 0f)
            {
                float upwardDampingT = 1f - math.saturate(_currentDepth / math.max(surfaceAscendReleaseDepth, 0.01f));
                if (upwardDampingT > 0f)
                {
                    _forceVector.x = 0f;
                    _forceVector.y = -_velocity.y * _rb.mass * surfaceAscendVelocityDamping * upwardDampingT;
                    _forceVector.z = 0f;
                    _rb.AddForce(_forceVector, ForceMode.Force);
                }
            }
        }

        // ══════════════════════════════════════════════════════════
        //  WALK PHYSICS
        // ══════════════════════════════════════════════════════════

        private void WalkPhysics(SuitData suit, float fixedDeltaTime)
        {
            if (_inputH == 0f && _inputV == 0f) return;

            float yawRad = _bodyYaw * DEG_TO_RAD;
            float sinYaw = math.sin(yawRad);
            float cosYaw = math.cos(yawRad);

            _moveDirection.x = sinYaw * _inputV + cosYaw * _inputH;
            _moveDirection.y = 0f;
            _moveDirection.z = cosYaw * _inputV - sinYaw * _inputH;

            float sqrMag = _moveDirection.x * _moveDirection.x + _moveDirection.z * _moveDirection.z;
            if (sqrMag > 1.0001f)
            {
                float invMag = 1f / math.sqrt(sqrMag);
                _moveDirection.x *= invMag;
                _moveDirection.z *= invMag;
            }

            if (_isGrounded)
            {
                _moveDirection = Vector3.ProjectOnPlane(_moveDirection, _smoothedGroundNormal);
                float projSqr = _moveDirection.sqrMagnitude;
                if (projSqr > 0.0001f)
                {
                    float invMag = 1f / math.sqrt(projSqr);
                    _moveDirection.x *= invMag;
                    _moveDirection.y *= invMag;
                    _moveDirection.z *= invMag;
                }
            }

            float wadeMultiplier = 1f - _waterImmersionRatio * suit.wadeSlowdownFactor;
            wadeMultiplier = math.max(wadeMultiplier, 0.2f);
            float sprintMult = CanUseLandSprint() ? suit.sprintMultiplier : 1f;
            float force = suit.walkForce * wadeMultiplier * sprintMult;

            if (IsDryLandAirborne())
                force *= dryAirControlMultiplier;

            _forceVector.x = _moveDirection.x * force;
            _forceVector.y = _moveDirection.y * force;
            _forceVector.z = _moveDirection.z * force;
            _rb.AddForce(_forceVector, ForceMode.Force);
        }

        private bool ShouldUseLandLocomotion(float physicsImmersion, bool hasShoreGroundSupport)
        {
            if (IsInDryInterior())
                return true;

            if (physicsImmersion <= 0.01f)
                return true;

            if (physicsImmersion >= swimTransitionThreshold)
                return false;

            return hasShoreGroundSupport;
        }

        private bool IsDryLandAirborne()
        {
            if (!_isWalking || _isGrounded)
                return false;

            if (_dryGroundGraceTimer > 0f)
                return false;

            if (_shoreGroundGraceTimer > 0f)
                return false;

            return _waterImmersionRatio <= 0.01f;
        }

        private bool CanUseLandSprint()
        {
            if (!_isSprinting || !_isWalking)
                return false;

            if (_isGrounded)
                return true;

            if (_dryGroundGraceTimer > 0f)
                return true;

            return _shoreGroundGraceTimer > 0f;
        }

        // ══════════════════════════════════════════════════════════
        //  AMBIENT CURRENT
        // ══════════════════════════════════════════════════════════

        private void ApplyAmbientCurrent(SuitData suit, float fixedDeltaTime)
        {
            if (suit.ambientCurrentStrength <= 0f) return;
            _currentTimer += fixedDeltaTime;
            if (_currentTimer > 100000f) _currentTimer -= 100000f;

            float strength = suit.ambientCurrentStrength * _waterImmersionRatio;
            Unity.Mathematics.float3 phantom = CurrentManager.SampleCurrent(
                new Unity.Mathematics.float3(transform.position.x, transform.position.y, transform.position.z),
                _currentTimer,
                0.018f,
                0.12f,
                strength,
                0.2f);
            Vector3 localVolumeCurrent = Hecton8.Physics.CurrentVolume.SampleAt(transform.position) * _waterImmersionRatio;
            _forceVector.x = phantom.x + localVolumeCurrent.x;
            _forceVector.y = phantom.y + localVolumeCurrent.y;
            _forceVector.z = phantom.z + localVolumeCurrent.z;
            _rb.AddForce(_forceVector, ForceMode.Force);
        }

        // ══════════════════════════════════════════════════════════
        //  VELOCITY CLAMP
        // ══════════════════════════════════════════════════════════

        private void ClampVelocity(SuitData suit)
        {
            _velocity = _rb.linearVelocity;

            if (_isWalking)
            {
                float maxSpd = suit.maxWalkSpeed;
                float wadeMultiplier = 1f - _waterImmersionRatio * suit.wadeSlowdownFactor;
                maxSpd *= math.max(wadeMultiplier, 0.2f);
                if (CanUseLandSprint()) maxSpd *= suit.sprintMultiplier;

                if (maxSpd > 0f)
                {
                    if (_isGrounded)
                    {
                        Vector3 planarVelocity = Vector3.ProjectOnPlane(_velocity, _smoothedGroundNormal);
                        float planarSqr = planarVelocity.sqrMagnitude;
                        float maxSqr = maxSpd * maxSpd;
                        if (planarSqr > maxSqr)
                        {
                            float scale = maxSpd / math.sqrt(planarSqr);
                            Vector3 normalVelocity = _velocity - planarVelocity;
                            planarVelocity.x *= scale;
                            planarVelocity.y *= scale;
                            planarVelocity.z *= scale;
                            _rb.linearVelocity = planarVelocity + normalVelocity;
                        }
                    }
                    else
                    {
                        float xzSqr = _velocity.x * _velocity.x + _velocity.z * _velocity.z;
                        float maxSqr = maxSpd * maxSpd;
                        if (xzSqr > maxSqr)
                        {
                            float scale = maxSpd / math.sqrt(xzSqr);
                            _velocity.x *= scale; _velocity.z *= scale;
                            _rb.linearVelocity = _velocity;
                        }
                    }
                }
            }
            else
            {
                float maxSpd = suit.maxSwimSpeed;
                if (_isSurfaceSwimming) maxSpd *= surfaceMaxSpeedMultiplier;
                if (_isSprinting) maxSpd *= suit.sprintMultiplier;
                if (maxSpd > 0f)
                {
                    float fullSqr = _velocity.x * _velocity.x + _velocity.y * _velocity.y + _velocity.z * _velocity.z;
                    float maxSqr = maxSpd * maxSpd;
                    if (fullSqr > maxSqr)
                    {
                        float scale = maxSpd / math.sqrt(fullSqr);
                        _velocity.x *= scale; _velocity.y *= scale; _velocity.z *= scale;
                        _rb.linearVelocity = _velocity;
                    }
                }
            }
        }

        // ══════════════════════════════════════════════════════════
        //  SPRING UTILITY
        // ══════════════════════════════════════════════════════════

        private static float SpringDamp(float current, float target, ref float velocity, float omega, float dt)
        {
            float n1 = velocity - (current - target) * (omega * omega * dt);
            float n2 = 1f + omega * dt;
            velocity = n1 / (n2 * n2);
            return current + velocity * dt;
        }

        // ══════════════════════════════════════════════════════════
        //  DIAGNOSTICS
        // ══════════════════════════════════════════════════════════

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private void UpdateModeDiagnostics()
        {
            _debugIsWalking = _isWalking;
            int modeIndex = (int)_currentLocomotionMode;
            _debugLocomotionMode = (uint)modeIndex < (uint)_locomotionModeLabels.Length
                ? _locomotionModeLabels[modeIndex]
                : "Unknown";
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private void UpdateGroundDiagnostics() { _debugIsGrounded = _isGrounded; }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private void UpdateStepDiagnostics() { _debugStepEvent = true; }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private void UpdateSuitDiagnostics()
        {
            _debugSuitName = currentSuitData != null ? currentSuitData.name : "NONE";
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private void UpdateCrestDiagnostics()
        {
            _debugCrestAvailable = _crestAvailable;
            _debugDynamicWaterY = _dynamicWaterSurfaceY;
            _debugCrestSampling = _crestSamplingSucceeded;
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private void UpdateDiagnostics(float speed)
        {
            _debugCurrentRoll = _juiceProcessor != null ? _juiceProcessor.CurrentRoll : 0f;
            _debugImmersionRatio = _waterImmersionRatio;
            _debugSmoothedImmersion = _smoothedImmersionRatio;
            _debugGravityScale = _gravityScale;
            _debugSnapScale = _snapScale;
            _debugBodyYaw = _bodyYaw;
            _debugCameraYaw = _cameraYaw;
            _debugSpeed = speed;
            _debugDynamicWaterY = EffectiveWaterSurfaceY;
            _debugCrestAvailable = _crestAvailable;
            _debugCrestSampling = _crestSamplingSucceeded;
            _debugDepth = _currentDepth;
            _debugFovOffset = _juiceOutput.fovOffset;
            _debugSplashThisFrame = _juiceProcessor != null && _juiceProcessor.SplashThisFrame;
            _debugExhaleThisFrame = _juiceProcessor != null && _juiceProcessor.ExhaleThisFrame;
            _debugIsSubmerged = _juiceProcessor != null && _juiceProcessor.IsSubmerged;
        }

        // ══════════════════════════════════════════════════════════
        //  EDITOR
        // ══════════════════════════════════════════════════════════
#if UNITY_EDITOR
        private void OnValidate()
        {
            if (UnityEditor.EditorApplication.isCompiling ||
                UnityEditor.EditorApplication.isUpdating ||
                UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            if (mouseSensitivity < 0.01f) mouseSensitivity = 0.01f;
            if (groundCheckRadius < 0.01f) groundCheckRadius = 0.01f;
            if (groundCheckDistance < 0.01f) groundCheckDistance = 0.01f;
            if (dryGroundGraceTime < 0f) dryGroundGraceTime = 0f;
            if (dryGroundGraceTime > 0.3f) dryGroundGraceTime = 0.3f;
            if (maxGroundAngle < 5f) maxGroundAngle = 5f;
            if (maxGroundAngle > 89f) maxGroundAngle = 89f;
            if (pitchMin < -89.9f) pitchMin = -89.9f;
            if (pitchMax > 89.9f) pitchMax = 89.9f;
            if (pitchMin > pitchMax) pitchMin = pitchMax;
            if (playerHeight < 0.5f) playerHeight = 0.5f;
            if (baseFov < 30f) baseFov = 30f;
            if (baseFov > 120f) baseFov = 120f;
            if (surfaceSwimDepthBand < 0.1f) surfaceSwimDepthBand = 0.1f;
            if (surfaceAscendReleaseDepth < 0.02f) surfaceAscendReleaseDepth = 0.02f;
            if (surfaceDivePitchCommit < 0f) surfaceDivePitchCommit = 0f;
            if (surfaceDivePitchCommit > 80f) surfaceDivePitchCommit = 80f;
            if (surfaceDiveForwardCommit < 0f) surfaceDiveForwardCommit = 0f;
            if (surfaceDiveForwardCommit > 1f) surfaceDiveForwardCommit = 1f;
            if (surfaceBreachDepthWindow < SurfaceStateUtility.ExitUnderwaterDepth)
                surfaceBreachDepthWindow = SurfaceStateUtility.ExitUnderwaterDepth;
            if (surfaceBreachMinImmersion >= 0.98f)
                surfaceBreachMinImmersion = 0.97f;

            RefreshGroundSlopeCache();
        }

        private void OnDrawGizmosSelected()
        {
            if (!Application.isPlaying) return;

            Vector3 bodyPos = transform.position;
            float bodyBottomY = GetBodyBottomY();
            Vector3 origin = new Vector3(bodyPos.x, bodyBottomY + groundCheckRadius + GroundCheckSkin, bodyPos.z);
            Vector3 castEnd = origin + Vector3.down * (groundCheckDistance + GroundCheckSkin);

            // Water level
            float effectiveY = EffectiveWaterSurfaceY;
            Gizmos.color = new Color(0f, 0.5f, 1f, 0.3f);
            Vector3 waterCenter = transform.position;
            waterCenter.y = effectiveY;
            Gizmos.DrawWireCube(waterCenter, new Vector3(6f, 0.02f, 6f));

            // Immersion indicator
            if (_waterImmersionRatio > 0.01f)
            {
                Gizmos.color = new Color(0f, 0.3f, 1f, 0.5f);
                float immersedHeight = playerHeight * _waterImmersionRatio;
                Vector3 immCenter = transform.position;
                immCenter.y += immersedHeight * 0.5f;
                Gizmos.DrawWireCube(immCenter, new Vector3(0.5f, immersedHeight, 0.5f));
            }

            if (_isGrounded)
            {
                Gizmos.color = new Color(0f, 1f, 0f, 0.5f);
                Gizmos.DrawWireSphere(_groundHit.point, groundCheckRadius);
                Gizmos.color = Color.cyan;
                Gizmos.DrawLine(_groundHit.point, _groundHit.point + _groundHit.normal * 1.5f);

                Gizmos.color = Color.magenta;
                Gizmos.DrawLine(_groundHit.point,
                    _groundHit.point + _smoothedGroundNormal * 1.2f);
            }
            else
            {
                Gizmos.color = new Color(1f, 0f, 0f, 0.3f);
                Gizmos.DrawWireSphere(castEnd, groundCheckRadius);
            }

            Gizmos.color = new Color(1f, 1f, 0f, 0.5f);
            Gizmos.DrawLine(origin, castEnd);

            // Body vs camera yaw
            if (!_isWalking)
            {
                Vector3 pos = transform.position + Vector3.up * 1.5f;
                float camR = _cameraYaw * DEG_TO_RAD;
                float bodR = _bodyYaw * DEG_TO_RAD;
                Gizmos.color = Color.green;
                Gizmos.DrawLine(pos, pos + new Vector3(math.sin(camR), 0f, math.cos(camR)) * 2f);
                Gizmos.color = Color.red;
                Gizmos.DrawLine(pos, pos + new Vector3(math.sin(bodR), 0f, math.cos(bodR)) * 1.5f);
            }

            // Depth indicator
            if (_currentDepth > 0.5f)
            {
                Gizmos.color = new Color(0f, 0f, 0.8f, 0.4f);
                Vector3 depthStart = transform.position;
                depthStart.y = effectiveY;
                Gizmos.DrawLine(depthStart, transform.position);
            }
        }
#endif
    }
}
