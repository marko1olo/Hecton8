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
        //  INSPECTOR — SWIM VERTICAL (Subnautica-style defaults)
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
        [SerializeField] private LayerMask groundLayers = ~0;
        [SerializeField, Range(1f, 2f)] private float slopeStabilityFactor = 1.1f;
        [SerializeField, Range(0f, 20f)] private float groundSnapForce = 8f;

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
        [SerializeField] private bool _debugIsGrounded;
        [SerializeField] private float _debugImmersionRatio;
        [SerializeField] private float _debugSmoothedImmersion;
        [SerializeField] private float _debugGravityScale;
        [SerializeField] private float _debugSnapScale;
        [SerializeField] private float _debugBodyYaw;
        [SerializeField] private float _debugCameraYaw;
        [SerializeField] private float _debugCurrentRoll;
        [SerializeField] private bool _debugStepEvent;
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
        private Transform _cachedTransform;
        private Camera _cameraComponent;
        private InputManager _inputManager;
        private InputManager _subscribedInputManager;

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
        private float _waterImmersionRatio;
        private float _smoothedImmersionRatio;
        private float _currentLinearDamping;
        private float _gravityScale;
        private float _snapScale;
        private float _currentDepth;  // v7.0: meters below water surface

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
            _juiceProcessor.Initialize(leanIntoTurn);
            UpdateSuitDiagnostics();
        }

        public SuitData CurrentSuit => currentSuitData;
        public float WaterImmersionRatio => _waterImmersionRatio;
        public bool IsGrounded => _isGrounded && _isWalking;
        public bool IsWalking => _isWalking;
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
            TryGetComponent(out _buoyancy);

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

            _juiceProcessor = new CameraJuiceProcessor();
            _juiceProcessor.Initialize(leanIntoTurn);

            // ── Crest integration ──
            _dynamicWaterSurfaceY = waterSurfaceY;
            _crestSamplingSucceeded = false;
            InitCrest();

            _waterImmersionRatio = ComputeImmersionRatio();
            _smoothedImmersionRatio = _waterImmersionRatio;
            _currentDepth = ComputeDepth();
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
                _crestHeightSampler = new Crest.SampleHeightHelper();
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
                    _crestHeightSampler = new Crest.SampleHeightHelper();
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

        private void HandleJumpInput()
        {
            if (_isWalking && _isGrounded)
                _jumpRequested = true;
        }

        private void HandleSprintStarted()
        {
            if (_isWalking)
                _isSprinting = true;
        }

        private void HandleSprintCanceled()
        {
            _isSprinting = false;
        }

        // ══════════════════════════════════════════════════════════
        //  Tick — INPUT + CAMERA (render framerate)
        // ══════════════════════════════════════════════════════════

        public void Tick(float deltaTime)
        {
            SuitData suit = currentSuitData;
            if (suit == null) return;

            RefreshInputManagerBinding();

            if (HectonFabricatorUI.IsMenuOpen || PlayerPDA.IsOpen)
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

                if (!_isWalking)
                    _isSprinting = false;
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

                if (_isWalking)
                    _isSprinting = KeyHeld(KeyCode.LeftShift) || KeyHeld(KeyCode.RightShift);
                else
                    _isSprinting = false;
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
                KeyHeld(controlScheme != null ? controlScheme.swimDescendPrimary : KeyCode.LeftControl) ||
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

        // ══════════════════════════════════════════════════════════
        //  FixedTick — PHYSICS
        // ══════════════════════════════════════════════════════════

        public void FixedTick(float fixedDeltaTime)
        {
            SuitData suit = currentSuitData;
            if (suit == null) return;

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
            bool groundedOnShore = _isGrounded && physicsImmersion < swimTransitionThreshold;

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
            bool inWater = physicsImmersion > 0.01f;
            bool deepEnough = physicsImmersion >= swimTransitionThreshold;

            bool shouldWalk;
            if (!inWater)
            {
                shouldWalk = _isGrounded;
            }
            else if (deepEnough)
            {
                shouldWalk = false;
            }
            else
            {
                shouldWalk = _isGrounded;
            }

            if (shouldWalk != _isWalking)
            {
                _isWalking = shouldWalk;
                ApplyModePhysics(suit);
                UpdateModeDiagnostics();
            }

            // ═══════════════════════════════════════════════
            //  9. DAMPING TRANSITION
            // ═══════════════════════════════════════════════
            SmoothDampingTransition(fixedDeltaTime, suit);

            // ═══════════════════════════════════════════════
            //  10. JUMP
            // ═══════════════════════════════════════════════
            if (_jumpRequested)
            {
                _jumpRequested = false;
                if (_isWalking && _isGrounded)
                    _rb.AddForce(Vector3.up * suit.jumpImpulse, ForceMode.Impulse);
            }

            // ═══════════════════════════════════════════════
            //  11. MOVEMENT + FORCES
            // ═══════════════════════════════════════════════
            if (_isWalking)
            {
                WalkPhysics(suit, fixedDeltaTime);

                if (_isGrounded && _snapScale > 0.001f)
                    ApplyGroundStability(_snapScale);
            }
            else
            {
                SwimPhysics(suit, fixedDeltaTime);

                if (_waterImmersionRatio > 0.3f && _waterImmersionRatio < 0.98f)
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
            float feetY = _rb.position.y;
            float headY = feetY + playerHeight;

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
            float headY = _rb.position.y + playerHeight * 0.85f;
            float depth = surfaceY - headY;
            return depth > 0f ? depth : 0f;
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
            Vector3 rbPos = _rb.position;
            _groundCheckOrigin.x = rbPos.x;
            _groundCheckOrigin.y = rbPos.y + groundCheckRadius;
            _groundCheckOrigin.z = rbPos.z;

            _isGrounded = UnityEngine.Physics.SphereCast(
                _groundCheckOrigin, groundCheckRadius,
                Vector3.down, out _groundHit,
                groundCheckDistance + groundCheckRadius,
                groundLayers, QueryTriggerInteraction.Ignore);

            if (_isGrounded)
            {
                float normalT = 1f - math.exp(-15f * Time.fixedDeltaTime);
                _smoothedGroundNormal = Vector3.Slerp(_smoothedGroundNormal, _groundHit.normal, normalT);

                float sqrMag = _smoothedGroundNormal.sqrMagnitude;
                if (sqrMag > 0.001f && math.abs(sqrMag - 1f) > 0.001f)
                {
                    _smoothedGroundNormal = _smoothedGroundNormal.normalized;
                }
            }
            else
            {
                float resetT = 1f - math.exp(-5f * Time.fixedDeltaTime);
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

            _forceVector.x = -_cachedGravity.x * mass * slopeStabilityFactor * scale;
            _forceVector.y = -_cachedGravity.y * mass * slopeStabilityFactor * scale;
            _forceVector.z = -_cachedGravity.z * mass * slopeStabilityFactor * scale;
            _rb.AddForce(_forceVector, ForceMode.Force);

            if (groundSnapForce > 0f)
            {
                _forceVector.x = 0f;
                _forceVector.y = -groundSnapForce * mass * scale;
                _forceVector.z = 0f;
                _rb.AddForce(_forceVector, ForceMode.Force);
            }
        }

        // ══════════════════════════════════════════════════════════
        //  SURFACE LOCK
        // ══════════════════════════════════════════════════════════

        private void ApplySurfaceLock(SuitData suit)
        {
            if (suit.surfaceLockStrength <= 0f) return;

            bool isDiving = _inputV > 0.1f && _cameraPitch > 20f;
            bool isDescending = _inputVertical < -0.1f;
            if (isDiving || isDescending) return;

            if (_isGrounded) return;

            float surfaceY = EffectiveWaterSurfaceY;
            float feetY = _rb.position.y;

            if (feetY >= surfaceY - 0.1f) return;

            float eyeY = feetY + playerHeight * 0.85f;
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

            float effectiveSwimForce = suit.swimForce * depthSlowdown;

            float bodyYawRad = _bodyYaw * DEG_TO_RAD;
            float pitchRad = _cameraPitch * DEG_TO_RAD;

            float sinBodyYaw = math.sin(bodyYawRad);
            float cosBodyYaw = math.cos(bodyYawRad);
            float sinPitch = math.sin(pitchRad);
            float cosPitch = math.cos(pitchRad);

            float fwdX = sinBodyYaw * cosPitch;
            float fwdY = -sinPitch;
            float fwdZ = cosBodyYaw * cosPitch;

            float rightX = cosBodyYaw;
            float rightZ = -sinBodyYaw;

            float dirX = fwdX * _inputV + rightX * _inputH;
            float dirY = fwdY * _inputV;
            float dirZ = fwdZ * _inputV + rightZ * _inputH;

            float sqrMag = dirX * dirX + dirY * dirY + dirZ * dirZ;
            if (sqrMag > 1.0001f)
            {
                float invMag = 1f / math.sqrt(sqrMag);
                dirX *= invMag; dirY *= invMag; dirZ *= invMag;
            }

            _forceVector.x = dirX * effectiveSwimForce;
            _forceVector.y = dirY * effectiveSwimForce;
            _forceVector.z = dirZ * effectiveSwimForce;
            _forceVector.y += _inputVertical * suit.swimVerticalForce * depthSlowdown;

            _rb.AddForce(_forceVector, ForceMode.Force);
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
            float sprintMult = (_isSprinting && _isGrounded) ? suit.sprintMultiplier : 1f;
            float force = suit.walkForce * wadeMultiplier * sprintMult;

            _forceVector.x = _moveDirection.x * force;
            _forceVector.y = _moveDirection.y * force;
            _forceVector.z = _moveDirection.z * force;
            _rb.AddForce(_forceVector, ForceMode.Force);
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
            _forceVector.x = math.sin(_currentTimer * 0.1f) * strength;
            _forceVector.y = math.sin(_currentTimer * 0.07f) * strength * 0.3f;
            _forceVector.z = math.cos(_currentTimer * 0.13f) * strength;
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
                if (_isSprinting && _isGrounded) maxSpd *= suit.sprintMultiplier;

                if (maxSpd > 0f)
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
            else
            {
                float maxSpd = suit.maxSwimSpeed;
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
        private void UpdateModeDiagnostics() { _debugIsWalking = _isWalking; }

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
            if (mouseSensitivity < 0.01f) mouseSensitivity = 0.01f;
            if (groundCheckRadius < 0.01f) groundCheckRadius = 0.01f;
            if (groundCheckDistance < 0.01f) groundCheckDistance = 0.01f;
            if (pitchMin < -89.9f) pitchMin = -89.9f;
            if (pitchMax > 89.9f) pitchMax = 89.9f;
            if (pitchMin > pitchMax) pitchMin = pitchMax;
            if (playerHeight < 0.5f) playerHeight = 0.5f;
            if (baseFov < 30f) baseFov = 30f;
            if (baseFov > 120f) baseFov = 120f;
        }

        private void OnDrawGizmosSelected()
        {
            if (!Application.isPlaying) return;

            Vector3 origin = transform.position + Vector3.up * groundCheckRadius;
            Vector3 castEnd = origin + Vector3.down * (groundCheckDistance + groundCheckRadius);

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
