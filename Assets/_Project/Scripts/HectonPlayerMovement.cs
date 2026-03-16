// ============================================================================
// HECTON-8 — HectonPlayerMovement.cs
// Rigidbody-based hybrid player movement for underwater NASA-Punk environment.
//
// v2.0 FIX — SLOPE PHYSICS:
//   [BUG] ApplyGroundStability() обнуляла velocity.y каждый FixedTick
//         когда игрок на земле. При ходьбе по наклонной поверхности
//         горизонтальная сила толкает игрока по XZ, но гравитация вниз
//         мгновенно обнуляется → игрок "парит" над склоном.
//         Jump также обнулял velocity.y — менее критично (одноразово),
//         но мешает естественной физике.
//
//   [FIX] ApplyGroundStability: удалено velocity.y = 0.
//         Заменено на: полная отмена гравитации + мягкая snap-сила.
//         Когда на земле, гравитация полностью компенсируется
//         counter-force. Snap-сила прижимает к поверхности.
//
//   [FIX] WalkPhysics: движение проецируется на плоскость склона
//         через Vector3.ProjectOnPlane(moveDir, groundNormal).
//         При ходьбе вниз по склону вектор силы направлен ВДОЛЬ
//         поверхности, а не горизонтально → игрок "приклеен" к земле.
//
//   [FIX] Jump: удалено velocity.y = 0. Импульс прыжка применяется
//         поверх текущей скорости. На пологих склонах разница
//         незначительна. На крутых — реалистичное снижение высоты прыжка.
//
// АРХИТЕКТУРА:
//   • ITickable      — input sampling, camera rotation (per frame)
//   • IFixedTickable — physics forces via Rigidbody.AddForce (fixed step)
//   • Integrates with HectonFluidEngine (BuoyancyObject applies buoyancy/drag)
//   • Respects HectonFabricatorUI.IsMenuOpen for input blocking
//
// HYBRID MOVEMENT:
//   SWIM MODE (IsInAir == false):
//     • 6DOF look-relative movement
//     • Vertical via Q/E/Space/Ctrl
//     • useGravity = false, high linearDamping
//
//   WALK MODE (IsInAir == true):
//     • XZ body-relative, slope-projected when grounded (v2.0)
//     • useGravity = true, low linearDamping
//     • Ground snap force prevents micro-bouncing (v2.0)
//     • Jump via Space (grounded only)
//
// ZERO GC:
//   • No allocations in Tick/FixedTick hot paths
//   • All struct math, cached references
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
        [Tooltip("Force applied for horizontal movement while swimming (Newtons).")]
        [SerializeField] private float swimForce = 600f;

        [Tooltip("Force applied for explicit ascend/descend (Q/E/Space/Ctrl) while swimming.")]
        [SerializeField] private float verticalForce = 400f;

        [Tooltip("Maximum swim speed on XZ plane (m/s).")]
        [SerializeField] private float maxSwimSpeed = 12f;

        [Tooltip("Rigidbody.linearDamping in swim mode.")]
        [SerializeField] private float swimLinearDamping = 1f;

        // ══════════════════════════════════════════════════════════
        //  INSPECTOR — WALKING
        // ══════════════════════════════════════════════════════════

        [Header("── Walking ───────────────────────────────────")]
        [Tooltip("Force applied for XZ movement while walking (Newtons).")]
        [SerializeField] private float walkForce = 1200f;

        [Tooltip("Maximum walk speed on XZ plane (m/s).")]
        [SerializeField] private float maxWalkSpeed = 6f;

        [Tooltip("Rigidbody.linearDamping in walk mode.")]
        [SerializeField] private float walkLinearDamping = 5f;

        [Tooltip("Impulse force applied upward when jumping (Newtons).")]
        [SerializeField] private float jumpForce = 5f;

        // ══════════════════════════════════════════════════════════
        //  INSPECTOR — GROUND DETECTION
        // ══════════════════════════════════════════════════════════

        [Header("── Ground Detection ─────────────────────────")]
        [Tooltip("Radius of the SphereCast used for ground detection.")]
        [SerializeField] private float groundCheckRadius = 0.3f;

        [Tooltip("Distance below player center to cast for ground.")]
        [SerializeField] private float groundCheckDistance = 0.4f;

        [Tooltip("Layers considered as ground for walking.")]
        [SerializeField] private LayerMask groundLayers = ~0;

        [Tooltip("Multiplier for counter-gravity force when grounded. " +
                 "1.0 = exactly cancel gravity. Higher = more slope stability.")]
        [SerializeField, Range(1f, 2f)]
        private float slopeStabilityFactor = 1.1f;

        [Tooltip("Gentle downward force (m/s²) applied when grounded to prevent " +
                 "micro-bouncing from physics solver imprecision. " +
                 "Acts as 'ground snap'. Too high → sinks into ground. " +
                 "Too low → floats on bumps.")]
        [SerializeField, Range(0f, 20f)]
        private float groundSnapForce = 8f;

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

        private float _inputH;
        private float _inputV;
        private float _inputVertical;

        private float _yaw;
        private float _pitch;

        private bool _inputCleared;
        private bool _jumpRequested;

        // ══════════════════════════════════════════════════════════
        //  MODE STATE
        // ══════════════════════════════════════════════════════════

        private bool _isWalking;
        private bool _isGrounded;

        // ══════════════════════════════════════════════════════════
        //  REGISTRATION FLAGS
        // ══════════════════════════════════════════════════════════

        private bool _registeredTick;
        private bool _registeredFixedTick;

        // ══════════════════════════════════════════════════════════
        //  CACHED MATH
        // ══════════════════════════════════════════════════════════

        private Vector3 _moveDirection;
        private Vector3 _forceVector;
        private Vector3 _velocity;
        private Quaternion _bodyRotation;
        private Quaternion _cameraRotation;

        private RaycastHit _groundHit;
        private Vector3 _groundCheckOrigin;

        private Vector3 _cachedGravity;

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

            Vector3 euler = _cachedTransform.eulerAngles;
            _yaw = euler.y;

            if (playerCamera != null)
            {
                _pitch = -playerCamera.localEulerAngles.x;
                if (_pitch > 180f) _pitch -= 360f;
                _pitch = Mathf.Clamp(_pitch, pitchMin, pitchMax);
            }

            _cachedGravity = UnityEngine.Physics.gravity;

            _isWalking = _buoyancy != null && _buoyancy.IsInAir;
            ApplyModePhysicsSettings();

            _registeredTick = false;
            _registeredFixedTick = false;
        }

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
                    "Player movement will NOT work.", this);
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

            if (_inputCleared || Cursor.lockState != CursorLockMode.Locked)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
                _inputCleared = false;
            }

            // ── Mouse Look ──
            float mouseX = Input.GetAxisRaw("Mouse X");
            float mouseY = Input.GetAxisRaw("Mouse Y");

            _yaw += mouseX * mouseSensitivity;
            _pitch -= mouseY * mouseSensitivity;
            _pitch = Mathf.Clamp(_pitch, pitchMin, pitchMax);

            _bodyRotation = Quaternion.Euler(0f, _yaw, 0f);
            _cachedTransform.rotation = _bodyRotation;

            if (playerCamera != null)
            {
                _cameraRotation = Quaternion.Euler(_pitch, 0f, 0f);
                playerCamera.localRotation = _cameraRotation;
            }

            // ── Movement Input ──
            _inputH = Input.GetAxisRaw("Horizontal");
            _inputV = Input.GetAxisRaw("Vertical");

            _inputVertical = 0f;

            if (_isWalking)
            {
                if (Input.GetKeyDown(KeyCode.Space))
                    _jumpRequested = true;
            }
            else
            {
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
            // ── Mode Detection (edge-triggered) ──
            bool shouldWalk = _buoyancy != null && _buoyancy.IsInAir;

            if (shouldWalk != _isWalking)
            {
                _isWalking = shouldWalk;
                ApplyModePhysicsSettings();
                UpdateModeDiagnostics();
            }

            // ── Ground Detection (walk mode only) ──
            if (_isWalking)
            {
                GroundCheck();
            }
            else
            {
                _isGrounded = false;
            }

            // ══════════════════════════════════════════════
            //  JUMP (v2.0: no velocity.y zeroing)
            //
            //  БЫЛО (v1, артефакт):
            //    _velocity = _rb.linearVelocity;
            //    if (_velocity.y < 0f) {
            //        _velocity.y = 0f;
            //        _rb.linearVelocity = _velocity;
            //    }
            //    _rb.AddForce(Vector3.up * jumpForce, Impulse);
            //
            //  СТАЛО (v2.0):
            //    _rb.AddForce(Vector3.up * jumpForce, Impulse);
            //
            //  Почему безопасно:
            //    На пологих склонах velocity.y ≈ 0 → разницы нет.
            //    На крутых склонах velocity.y может быть -2..-3 м/с,
            //    что слегка снижает высоту прыжка (реалистично).
            //    ApplyGroundStability (v2.0) не даёт гравитации
            //    накапливать скорость вниз → velocity.y при стоянии ≈ 0.
            // ══════════════════════════════════════════════

            if (_jumpRequested)
            {
                _jumpRequested = false;

                if (_isWalking && _isGrounded)
                {
                    _rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
                }
            }

            // ── Movement Physics ──
            if (_isWalking)
                WalkPhysics();
            else
                SwimPhysics();

            // ── Ground Stability (walk mode, grounded) ──
            if (_isWalking && _isGrounded)
            {
                ApplyGroundStability();
            }

            // ── Velocity Clamp ──
            ClampVelocity();

            UpdateGroundDiagnostics();
        }

        // ══════════════════════════════════════════════════════════
        //  PRIVATE — GROUND DETECTION
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// SphereCast downward from player center to detect ground.
        /// Only called when _isWalking == true.
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

        // ══════════════════════════════════════════════════════════
        //  PRIVATE — GROUND STABILITY (v2.0: slope-safe)
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Prevents slope sliding and micro-bouncing when grounded.
        ///
        /// v2.0 FIX: Полностью переписан.
        ///
        /// БЫЛО (v1, ЛЕВИТАЦИЯ):
        ///   _velocity = _rb.linearVelocity;
        ///   if (_velocity.y &lt; 0f) {
        ///       _velocity.y = 0f;              ← УБИВАЕТ ходьбу по склонам!
        ///       _rb.linearVelocity = _velocity; ← Каждый FixedTick → левитация
        ///   }
        ///   ... + частичная компенсация гравитации
        ///
        /// СТАЛО (v2.0, КОРРЕКТНО):
        ///   1. ПОЛНАЯ компенсация гравитации: AddForce(-gravity * mass * factor).
        ///      Когда на земле, гравитация полностью отменяется, как будто
        ///      под ногами твёрдая поверхность (реакция опоры).
        ///      Это предотвращает sliding по склону при стоянии без движения.
        ///
        ///   2. Ground snap: мягкая сила вниз (groundSnapForce * mass).
        ///      Прижимает игрока к поверхности, компенсируя микро-отскоки
        ///      от физического солвера. Без этого на неровностях
        ///      игрок "подпрыгивает" на 1-2 см каждый кадр.
        ///
        ///   3. velocity.y НЕ обнуляется. Ходьба по склону вниз
        ///      (через ProjectOnPlane в WalkPhysics) создаёт отрицательную
        ///      Y-скорость — это нормально и ожидаемо. Обнуление убивало бы
        ///      этот компонент, вызывая "парение" над склоном.
        ///
        /// ВЗАИМОДЕЙСТВИЕ С WalkPhysics:
        ///   WalkPhysics проецирует вектор движения на плоскость склона.
        ///   ApplyGroundStability отменяет гравитацию и прижимает к земле.
        ///   Вместе они создают плавное перемещение по наклонным поверхностям
        ///   без подпрыгивания и без скольжения.
        /// </summary>
        private void ApplyGroundStability()
        {
            // ── 1. Полная компенсация гравитации ──
            // Когда на земле, поверхность реагирует на гравитацию.
            // В Rigidbody-системе мы эмулируем это через counter-force.
            // slopeStabilityFactor > 1.0 даёт лёгкую перекомпенсацию
            // для предотвращения drift'а на крутых склонах.
            _forceVector.x = -_cachedGravity.x * _rb.mass * slopeStabilityFactor;
            _forceVector.y = -_cachedGravity.y * _rb.mass * slopeStabilityFactor;
            _forceVector.z = -_cachedGravity.z * _rb.mass * slopeStabilityFactor;

            _rb.AddForce(_forceVector, ForceMode.Force);

            // ── 2. Ground snap: мягкое прижимание к поверхности ──
            // Компенсирует микро-отскоки от collision solver.
            // Направлен вниз, масштабирован массой для consistency.
            // Величина groundSnapForce (по умолчанию ~8) подбирается
            // так, чтобы:
            //   • Не проваливаться в землю (слишком высокая)
            //   • Не "парить" на bump'ах (слишком низкая)
            //   • Не мешать прыжку (прыжок ставит _isGrounded = false
            //     на следующем кадре → snap не применяется)
            if (groundSnapForce > 0f)
            {
                _rb.AddForce(Vector3.down * (groundSnapForce * _rb.mass), ForceMode.Force);
            }
        }

        // ══════════════════════════════════════════════════════════
        //  PRIVATE — SWIM PHYSICS (6DOF look-relative)
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// 6DOF Look-Relative Swim Movement.
        /// W/S: along camera.forward (full 3D — looking down + W = dive).
        /// A/D: strafe along camera.right.
        /// Space/E: ascend. Ctrl/Q: descend.
        /// Zero GC.
        /// </summary>
        private void SwimPhysics()
        {
            bool hasInput = _inputH != 0f || _inputV != 0f || _inputVertical != 0f;
            if (!hasInput || playerCamera == null) return;

            Vector3 camForward = playerCamera.forward;
            Vector3 camRight   = playerCamera.right;

            float dirX = camForward.x * _inputV + camRight.x * _inputH;
            float dirY = camForward.y * _inputV + camRight.y * _inputH;
            float dirZ = camForward.z * _inputV + camRight.z * _inputH;

            float sqrMag = dirX * dirX + dirY * dirY + dirZ * dirZ;
            if (sqrMag > 1.0001f)
            {
                float invMag = 1f / Mathf.Sqrt(sqrMag);
                dirX *= invMag;
                dirY *= invMag;
                dirZ *= invMag;
            }

            _forceVector.x = dirX * swimForce;
            _forceVector.y = dirY * swimForce;
            _forceVector.z = dirZ * swimForce;

            _forceVector.y += _inputVertical * verticalForce;

            _rb.AddForce(_forceVector, ForceMode.Force);
        }

        // ══════════════════════════════════════════════════════════
        //  PRIVATE — WALK PHYSICS (v2.0: slope-projected)
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// XZ body-relative movement with slope projection.
        ///
        /// v2.0 FIX: Когда игрок на земле, вектор движения проецируется
        /// на плоскость склона через Vector3.ProjectOnPlane(dir, groundNormal).
        ///
        /// БЫЛО (v1, ПОДПРЫГИВАНИЕ НА СКЛОНАХ):
        ///   _moveDirection = (forward * inputV + right * inputH) с Y=0
        ///   _forceVector = _moveDirection * walkForce с Y=0
        ///   → Горизонтальная сила на склоне = игрок "съезжает" с поверхности,
        ///     кратковременно теряет контакт, падает обратно → bounce.
        ///
        /// СТАЛО (v2.0, ПЛАВНО):
        ///   Когда grounded:
        ///     _moveDirection = ProjectOnPlane(horizontalDir, groundNormal).normalized
        ///     _forceVector = _moveDirection * walkForce (включая Y-компонент!)
        ///   → Сила направлена ВДОЛЬ поверхности, не отрывая от неё.
        ///   → Ходьба вниз по склону: Y-компонент отрицательный → "приклеен".
        ///   → Ходьба вверх по склону: Y-компонент положительный → карабкается.
        ///   → Плоская земля: Y ≈ 0 → поведение как в v1.
        ///
        /// Когда NOT grounded (в воздухе при ходьбе):
        ///   Используется горизонтальный вектор без проекции (air control).
        ///
        /// Zero GC: ProjectOnPlane returns struct.
        /// </summary>
        private void WalkPhysics()
        {
            if (_inputH == 0f && _inputV == 0f) return;

            Vector3 bodyForward = _cachedTransform.forward;
            Vector3 bodyRight = _cachedTransform.right;

            // ── Горизонтальное направление движения (до проекции) ──
            _moveDirection.x = bodyForward.x * _inputV + bodyRight.x * _inputH;
            _moveDirection.y = 0f;
            _moveDirection.z = bodyForward.z * _inputV + bodyRight.z * _inputH;

            // ── Нормализация (предотвращает diagonal speed boost) ──
            float sqrMag = _moveDirection.x * _moveDirection.x
                         + _moveDirection.z * _moveDirection.z;

            if (sqrMag > 1.0001f)
            {
                float invMag = 1f / Mathf.Sqrt(sqrMag);
                _moveDirection.x *= invMag;
                _moveDirection.z *= invMag;
            }

            // ══════════════════════════════════════════════
            //  v2.0: SLOPE PROJECTION (grounded only)
            //
            //  Проецирует горизонтальный вектор движения на
            //  плоскость склона (определённую нормалью _groundHit).
            //
            //  Пример:
            //    Склон 30°, нормаль = (0, 0.866, 0.5)
            //    Движение forward = (0, 0, 1)
            //    ProjectOnPlane = (0, -0.25, 0.866) ← вниз по склону!
            //    Normalize = (0, -0.277, 0.961)
            //
            //  Результат: сила направлена ВДОЛЬ поверхности склона,
            //  а не горизонтально. Игрок "скользит" по земле.
            //
            //  На плоской земле (нормаль = up):
            //    ProjectOnPlane = (moveDir.x, 0, moveDir.z) ← без изменений.
            // ══════════════════════════════════════════════

            if (_isGrounded)
            {
                // Проекция на плоскость склона
                _moveDirection = Vector3.ProjectOnPlane(_moveDirection, _groundHit.normal);

                // Ренормализация: ProjectOnPlane может изменить длину вектора.
                // На крутых склонах длина уменьшается (косинус угла).
                // Ренормализация восстанавливает полную силу в любом направлении.
                float projSqr = _moveDirection.sqrMagnitude;
                if (projSqr > 0.0001f)
                {
                    float invMag = 1f / Mathf.Sqrt(projSqr);
                    _moveDirection.x *= invMag;
                    _moveDirection.y *= invMag;
                    _moveDirection.z *= invMag;
                }
            }

            // ── Применяем силу ──
            // v2.0: _forceVector.y теперь НЕ обнуляется!
            // Slope projection задаёт правильный Y-компонент.
            // На плоской земле Y ≈ 0 (поведение как в v1).
            _forceVector.x = _moveDirection.x * walkForce;
            _forceVector.y = _moveDirection.y * walkForce;
            _forceVector.z = _moveDirection.z * walkForce;

            _rb.AddForce(_forceVector, ForceMode.Force);
        }

        // ══════════════════════════════════════════════════════════
        //  PRIVATE — VELOCITY CLAMP
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Clamps velocity based on current mode.
        /// WALK: XZ clamp only (Y is gravity/slope controlled).
        /// SWIM: Full 3D magnitude clamp.
        /// Zero GC.
        /// </summary>
        private void ClampVelocity()
        {
            _velocity = _rb.linearVelocity;
            bool clamped = false;

            if (_isWalking)
            {
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
            if (swimLinearDamping < 0f) swimLinearDamping = 0f;

            if (walkForce < 0f) walkForce = 0f;
            if (maxWalkSpeed < 0f) maxWalkSpeed = 0f;
            if (walkLinearDamping < 0f) walkLinearDamping = 0f;
            if (jumpForce < 0f) jumpForce = 0f;

            if (groundCheckRadius < 0.01f) groundCheckRadius = 0.01f;
            if (groundCheckDistance < 0.01f) groundCheckDistance = 0.01f;
            if (groundSnapForce < 0f) groundSnapForce = 0f;

            if (pitchMin < -89.9f) pitchMin = -89.9f;
            if (pitchMax > 89.9f) pitchMax = 89.9f;
            if (pitchMin > pitchMax) pitchMin = pitchMax;
        }

        private void OnDrawGizmosSelected()
        {
            if (!Application.isPlaying) return;

            Vector3 origin = transform.position + Vector3.up * groundCheckRadius;
            Vector3 castEnd = origin + Vector3.down * (groundCheckDistance + groundCheckRadius);

            if (_isGrounded)
            {
                // Ground hit — green sphere at contact point
                Gizmos.color = new Color(0f, 1f, 0f, 0.5f);
                Gizmos.DrawWireSphere(_groundHit.point, groundCheckRadius);

                // v2.0: Visualize ground normal
                Gizmos.color = Color.cyan;
                Gizmos.DrawLine(_groundHit.point,
                                _groundHit.point + _groundHit.normal * 1.5f);

                // v2.0: Visualize projected walk direction (if moving)
                if (_inputH != 0f || _inputV != 0f)
                {
                    Vector3 projDir = Vector3.ProjectOnPlane(
                        _cachedTransform.forward * _inputV + _cachedTransform.right * _inputH,
                        _groundHit.normal).normalized;

                    Gizmos.color = Color.yellow;
                    Gizmos.DrawLine(_groundHit.point,
                                    _groundHit.point + projDir * 2f);
                }
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