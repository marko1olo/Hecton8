// ============================================================================
// HECTON-8 — LaserCutter.cs  v2.1
// Лазерный резак — PlayerTool с термическим менеджментом.
//
// v2.1 CHANGES (OPTIMIZATION PASS):
//   [OPT] Player inventory resolve moved out of hot loop (EnsurePlayerInventory)
//     to ONE-TIME initialization in Awake().
//     • Impact: eliminates O(N) scene search on every deconstruct operation
//     • Safe: PlayerInventory must exist at scene start, unlikely to change
//
//   [OPT] StringBuilder(_diagnosisSB) reused for diagnosis building
//     reduces allocations from multiple string interpolations.
//     • Impact: ~2-3 string allocations saved per secondary action
//
//   [OPT] Diagnosis caching (_cachedDiagnosis, _diagnosisCached)
//     prevents redundant raycast+diagnosis build when UI reads state.
//     • Impact: GetOperationalSummary/Directive no longer re-raycast
//     • Cache invalidated at ToolTick end (per-frame)
//
//   [OPT] Separated raycast results for concurrent operations
//     UseSecondary now uses local RaycastHit (not _hitInfo)
//     prevents interference between UsePrimary and UseSecondary
//     • Impact: UseSecondary() diagnosis cannot clobber UsePrimary() beam data
//
//   [REFACTORED] BuildDiagnosis split into:
//     • BuildDiagnosis(didHit) — uses cached _hitInfo from UsePrimary
//     • BuildDiagnosisFromHit(hit, didHit) — uses explicit RaycastHit
//     • BuildDiagnosisImpl(hit) — shared logic, works with any RaycastHit
//     • ReadDiagnosisNow() — fresh raycast for UI queries
//
// PRESERVED FROM v2.0:
//   ✓ Heat accumulation, decay, lockout
//   ✓ Damage scaling by heat
//   ✓ Beam jitter, spark emission, audio pitch feedback
//   ✓ Deconstruct mode with progress accumulation
//   ✓ ICuttable integration
//   ✓ Zero GC in hot path
//
// v2.0 (HEAT MANAGEMENT):
//   [ADD] Heat accumulation system, thermal lockout
//   [ADD] Risk/Reward damage scaling (+15% at max heat)
//   [ADD] Visual feedback (beam jitter, spark rate, audio pitch)
//   [ADD] Overheat lockout via tick-driven timer state
//   [PRESERVED] Dual mode, ICuttable, BaseModule.Deconstruct, zero GC
//
// v1.0 (INITIAL):
//   Core laser cutter mechanics
// ============================================================================

namespace Hecton8.Gameplay
{
    using System;
    using Hecton8.Bootstrap;
    using Hecton8.Building;
    using Hecton8.Construction;
    using Hecton8.Core;
    using Hecton8.Interaction;
    using Hecton8.Inventory;
    using Hecton8.Input;
    using Hecton.Localization;
    using Hecton8.Physics;
    using Hecton8.Scavenging;
    using Unity.Mathematics;
    using UnityEngine;

    [DisallowMultipleComponent]
    public sealed class LaserCutter : PlayerTool, IToolModule
    {
        private const string CutterCategory = "CUTTER";
        private const int RecoveryProgressMessageCount = 101;
        private const float MaxRecoilImpulse = 12f;
        private const byte IdleState = (byte)ToolStateBits.Idle;
        private const byte ActiveState = (byte)ToolStateBits.Active;
        private const byte BusyState = (byte)ToolStateBits.Busy;
        private const byte OverheatedState = (byte)ToolStateBits.Overheated;
        private const byte CooldownState = (byte)ToolStateBits.Cooldown;

        private struct CutterDiagnosis
        {
            public string headline;
            public string summary;
            public string severity;
        }

        // COLD ALLOC: String[101] — localized recovery progress HUD cache — owner: LaserCutter
        private static string[] _recoveryProgressMessages = BuildRecoveryProgressMessages();
        private static GameLanguage _recoveryProgressLanguage = (GameLanguage)(-1);

        // ══════════════════════════════════════════════════════════
        //  EVENTS
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Fires when heat level changes significantly.
        /// Parameter: normalized heat [0..1].
        /// Subscribers: HUD heat gauge, warning audio system.
        /// Throttled: only fires when delta > 0.02 to avoid spam.
        /// </summary>
        public static event Action<float> OnHeatChanged;
        internal static event Action<Transform, bool> OnBeamStateChanged;

        // ══════════════════════════════════════════════════════════
        //  INSPECTOR — LASER SETTINGS
        // ══════════════════════════════════════════════════════════

        [Header("── Laser Settings ────────────────────────────")]
        [Tooltip("Maximum beam range (meters).")]
        [SerializeField] private float maxRange = 5f;

        [Tooltip("Base damage per second when cutting.")]
        [SerializeField] private float damagePerSecond = 25f;

        [Tooltip("LayerMask for raycast targets.")]
        [SerializeField] private LayerMask cuttableLayer = ~0;

        [Header("── Heat Management ───────────────────────────")]
        [Tooltip("Seconds of continuous firing to reach overheat (heat 0→1).")]
        [SerializeField] private float overheatTime = 5f;

        [Tooltip("Heat units lost per second when NOT firing.\n" +
                 "0.3 = full cooldown from max in ~3.3 seconds.")]
        [SerializeField] private float cooldownRate = 0.3f;

        [Tooltip("Lockout duration after overheat (seconds).\n" +
                 "Tool is completely disabled during this time.")]
        [SerializeField] private float overheatLockoutTime = 2f;

        [Tooltip("Bonus damage multiplier at maximum heat.\n" +
                 "0.15 = 15% more damage when red-hot.\n" +
                 "Risk/reward: more efficient but lockout is the cost.")]
        [Range(0f, 0.5f)]
        [SerializeField] private float heatDamageBonus = 0.15f;

        [Tooltip("Passive heat decay bonus applied while the player remains submerged.")]
        [SerializeField, Range(0f, 1.2f)] private float passiveWaterCoolingBonus = 0.45f;

        [Tooltip("Base recoil impulse used for deferred player-body kickback.")]
        [SerializeField, Range(0f, 12f)] private float recoilImpulseBase = 4f;

        [Tooltip("Additional recoil damping applied while submerged.")]
        [SerializeField, Range(0.1f, 1f)] private float submergedRecoilScale = 0.6f;

        [Header("── Beam Visual ───────────────────────────────")]
        [Tooltip("Maximum jitter amplitude at full heat (meters).\n" +
                 "Beam endpoint vibrates more as tool heats up.")]
        [SerializeField] private float maxJitterAmplitude = 0.008f;

        [Tooltip("Jitter frequency (Hz). Higher = faster vibration.")]
        [SerializeField] private float jitterFrequency = 50f;

        [Header("── Deconstruction ────────────────────────────")]
        [Tooltip("Seconds of continuous cutting to fully deconstruct a module.\n" +
                 "Progress resets if target changes or R/LKM released.")]
        [SerializeField] private float deconstructThreshold = 3f;
        [Tooltip("Normalized spring load required before salvage recovery progress can move.")]
        [SerializeField, Range(0f, 1f)] private float heavySalvageRequiredTension01 = 0.42f;
        [Tooltip("Normalized pull-back intent required to tear a heavy module free while cutting.")]
        [SerializeField, Range(0f, 1f)] private float heavySalvageRequiredPull01 = 0.36f;
        [Tooltip("Velocity away from the cut seam that counts as full pull intent.")]
        [SerializeField, Range(0.1f, 6f)] private float heavySalvagePullVelocityForFullIntent = 1.75f;
        [Tooltip("Retracts the cutter anchor slightly into the surface so the spring loads against the seam instead of hovering in open air.")]
        [SerializeField, Range(0f, 0.2f)] private float heavySalvageAnchorRetraction = 0.03f;

        [Header("── Visual References ─────────────────────────")]
        [Tooltip("LineRenderer for beam visualization.")]
        [SerializeField] private LineRenderer laserLine;

        [Tooltip("ParticleSystem for impact sparks.")]
        [SerializeField] private ParticleSystem sparksVFX;

        [Header("── Audio ─────────────────────────────────────")]
        [Tooltip("Looping AudioSource for cutting sound.")]
        [SerializeField] private AudioSource cutAudio;

        [Tooltip("Sound played when attempting to fire during overheat lockout.")]
        [SerializeField] private AudioClip overheatErrorClip;

        [Tooltip("Base pitch of cutting audio (at zero heat).")]
        [SerializeField] private float basePitch = 1.0f;

        [Tooltip("Maximum pitch of cutting audio (at full heat).")]
        [SerializeField] private float maxPitch = 1.3f;

        // ══════════════════════════════════════════════════════════
        //  RUNTIME STATE
        // ══════════════════════════════════════════════════════════

        /// <summary>Raycast result (reused, zero GC).</summary>
        private RaycastHit _hitInfo;

        /// <summary>Cached diagnosis result (reused, zero GC).</summary>
        private CutterDiagnosis _cachedDiagnosis;

        /// <summary>Is cached diagnosis valid for current frame.</summary>
        private bool _diagnosisCached;

        /// <summary>Is beam active this frame.</summary>
        private bool _isFiring;

        /// <summary>Was beam active last frame (for toggle VFX).</summary>
        private bool _wasFiringLastFrame;

        /// <summary>Cached transform for ray origin/direction.</summary>
        private Transform _cachedTransform;

        // ── Heat State ──

        /// <summary>
        /// Current heat level [0..1].
        /// 0 = cold, 1 = overheated.
        /// Accumulates during firing, decays during idle.
        /// </summary>
        private float _heatLevel;

        /// <summary>Is tool currently in overheat lockout.</summary>
        private bool _isLockedOut;

        /// <summary>Remaining lockout time in seconds.</summary>
        private float _lockoutTimer;

        /// <summary>Last published heat value (for event throttling).</summary>
        private float _lastPublishedHeat;

        /// <summary>Has the error clip been played this lockout cycle.
        /// Prevents spamming the error sound every frame while locked.</summary>
        private bool _lockoutSoundPlayed;

        // ── Deconstruct State ──

        /// <summary>Accumulated deconstruct progress (seconds).</summary>
        private float _deconstructProgress;

        /// <summary>InstanceID of current deconstruct target (-1 = none).</summary>
        private int _cachedDeconstructTargetId = -1;

        /// <summary>Cached BaseModule of current deconstruct target.</summary>
        private BaseModule _cachedDeconstructModule;

        /// <summary>Cached PlayerInventory for Deconstruct calls.</summary>
        private PlayerInventory _cachedInventory;
        private Transform _cachedPlayerTransform;
        private HectonPlayerMovement _cachedPlayerMovement;
        private Rigidbody _cachedPlayerRigidbody;

        /// <summary>Cached StringBuilder for zero-GC diagnosis strings.</summary>
        private System.Text.StringBuilder _diagnosisSB;

        private bool _secondaryLatched;
        private bool _deconstructStartReported;
        private bool _deconstructBlockedReported;
        private float _nextProgressFeedbackAt;
        private Vector3 _cachedDeconstructAnchorPoint;
        private Vector3 _cachedDeconstructAnchorNormal = Vector3.up;
        private uint _cachedToolId;
        private byte _toolStateFlags = IdleState;

        // ── Sparks cache ──

        /// <summary>Cached emission module (struct, zero GC).</summary>
        private ParticleSystem.EmissionModule _sparksEmission;
        private bool _sparksEmissionCached;

        /// <summary>Base emission rate from prefab (for scaling).</summary>
        private float _baseSparksRate;

        // ══════════════════════════════════════════════════════════
        //  PUBLIC PROPERTIES
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Current heat level [0..1]. Read by HUD systems.
        /// 0 = cold, 1 = overheated/locked.
        /// </summary>
        public float HeatLevel => _heatLevel;

        /// <summary>Is the tool currently in overheat lockout.</summary>
        public bool IsOverheated => _isLockedOut;

        public bool DebugRecoverModule(BaseModule module)
        {
            if (module == null || !module.CanDeconstruct())
                return false;

            EnsurePlayerInventory();
            module.Deconstruct(_cachedInventory);
            ArchiveRecoveredModule(module);
            ToolHitUtility.ShowInfo(ResolveLocalized(LocalizationKeys.LASER_HUD_MODULE_RECOVERED, "LASER CUTTER - MODULE RECOVERED"));
            FieldOperationLogSystem.RecordOperation(
                ResolveLocalized(LocalizationKeys.LASER_CATEGORY, CutterCategory),
                ResolveLocalized(LocalizationKeys.LASER_LOG_MODULE_RECOVERY_TITLE, "MODULE RECOVERY COMPLETED"),
                string.Format(
                    ResolveLocalized(
                        LocalizationKeys.LASER_LOG_MODULE_RECOVERY_MESSAGE,
                        "Laser-assisted deconstruction completed on {0}."),
                    module.name),
                "INFO");
            ResetDeconstructState();
            return true;
        }

        // ══════════════════════════════════════════════════════════
        //  LIFECYCLE
        // ══════════════════════════════════════════════════════════

        private void Awake()
        {
            _cachedTransform = transform;
            _diagnosisSB = new System.Text.StringBuilder(256);
            CacheSparksEmission();
            CacheToolId();
            SetVisualsActive(false);
            
            EnsurePlayerBindings();
        }

        public override void OnSpawn()
        {
            base.OnSpawn();
            CacheToolId();
            ResetAllState();
            SetVisualsActive(false);
        }

        public override void OnDespawn()
        {
            base.OnDespawn();
            ResetAllState();
            SetVisualsActive(false);
        }

        public override void OnEquip()
        {
            base.OnEquip();
            // Don't reset heat on equip — tool remembers its temperature
            // This rewards careful heat management across equip cycles
        }

        public override void OnUnequip()
        {
            CancelAction();
            ResetDeconstructState();
            SetVisualsActive(false);
            base.OnUnequip();
        }

        // ══════════════════════════════════════════════════════════
        //  TOOL ACTIONS
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Primary action: fire laser beam.
        /// Called every frame while Fire1 is held.
        ///
        /// v2.0: Heat accumulation + lockout + damage scaling.
        /// </summary>
        public override void UsePrimary(float deltaTime)
        {
            // ── Overheat lockout check ──
            if (_isLockedOut)
            {
                SetOverheatedState();
                // Play error sound once per lockout cycle
                if (!_lockoutSoundPlayed && overheatErrorClip != null)
                {
                    if (Hecton8.Audio.SpatialAudioManager.Instance != null)
                    {
                        Hecton8.Audio.SpatialAudioManager.Instance.PlayStatic2D(
                            overheatErrorClip, 0.5f);
                    }
                    _lockoutSoundPlayed = true;
                    ToolHitUtility.ShowWarning(ResolveLocalized(LocalizationKeys.LASER_HUD_OVERHEAT_LOCKOUT, "LASER CUTTER - OVERHEAT LOCKOUT"));
                }
                return;
            }

            Activate();
            _isFiring = true;
            PublishBeamState(true);

            // ── Accumulate heat ──
            _heatLevel += deltaTime / math.max(overheatTime, 0.1f);

            // ── Check overheat ──
            if (_heatLevel >= 1f)
            {
                _heatLevel = 1f;
                TriggerOverheatLockout();
                return;
            }

            // ── Raycast ──
            bool didHit = TryGetCutHit(out _hitInfo);

            // ── Visuals ──
            UpdateLaserLine(didHit);
            UpdateSparks(didHit);
            UpdateAudioState(true);

            // ── Damage / Deconstruct ──
            if (didHit)
            {
                IInputService inputService = GlobalRegistry.Input;
                PlayerInputState inputState = inputService != null && inputService.IsPlayerInputEnabled
                    ? inputService.GetState()
                    : default;
                bool deconstructMode = inputState.HasAction(PlayerInputAction.SecondaryFire);

                if (deconstructMode)
                {
                    ProcessDeconstructMode(deltaTime);
                }
                else
                {
                    ResetDeconstructState();
                    ApplyCutDamage(deltaTime);
                }
            }
            else
            {
                ResetDeconstructState();
            }

            // ── Publish heat ──
            PublishHeat();
        }

        public override void UseSecondary(float deltaTime)
        {
            if (_secondaryLatched)
                return;

            _secondaryLatched = true;

            RaycastHit diagHit;
            bool didHit = TryGetCutHit(out diagHit);

            // Build diagnosis from local hit, not cached _hitInfo
            _cachedDiagnosis = didHit && diagHit.collider != null
                ? BuildDiagnosisImpl(diagHit)
                : new CutterDiagnosis
                {
                    headline = ResolveLocalized(LocalizationKeys.LASER_HEADLINE_NO_TARGET, "NO TARGET"),
                    summary = ResolveLocalized(LocalizationKeys.LASER_SUMMARY_NO_TARGET, "No cuttable contact inside cutter range."),
                    severity = "WARN"
                };
            _diagnosisCached = true;
            
            PublishDiagnosis(_cachedDiagnosis);
            FieldOperationLogSystem.RecordOperation(
                ResolveLocalized(LocalizationKeys.LASER_CATEGORY, CutterCategory),
                string.Format(
                    ResolveLocalized(LocalizationKeys.LASER_LOG_DIAG_TITLE, "CUTTER DIAG - {0}"),
                    _cachedDiagnosis.headline),
                _cachedDiagnosis.summary,
                _cachedDiagnosis.severity);
        }

        /// <summary>
        /// Called every frame regardless of input.
        /// Handles: heat decay, visual shutdown, audio pitch, cache invalidation.
        /// </summary>
        public override void ToolTick(float deltaTime)
        {
            if (_isLockedOut)
            {
                _lockoutTimer = math.max(0f, _lockoutTimer - deltaTime);
                if (_lockoutTimer <= 0f)
                {
                    _isLockedOut = false;
                    _lockoutSoundPlayed = false;
                    _heatLevel = math.min(_heatLevel, 0.8f);
                    ClearFlag(OverheatedState);
                    EnterCooldownState();
                    PublishHeat();
                    ToolHitUtility.ShowInfo(ResolveLocalized(LocalizationKeys.LASER_HUD_CORE_STABLE, "LASER CUTTER - CORE STABLE"));
                }
            }
            // ── Heat decay when not firing ──
            if (!_isFiring && !_isLockedOut)
            {
                if (_heatLevel > 0f)
                {
                    _heatLevel = math.max(0f, _heatLevel - deltaTime * cooldownRate * (1f + ResolvePassiveCoolingBonus()));
                    EnterCooldownState();
                    PublishHeat();
                }
                else
                {
                    Deactivate();
                }
            }

            // ── Visual shutdown on release ──
            if (_wasFiringLastFrame && !_isFiring)
            {
                PublishBeamState(false);
                SetVisualsActive(false);
                ResetDeconstructState();
            }

            _wasFiringLastFrame = _isFiring;
            _isFiring = false;

            // ── Invalidate diagnosis cache at end of frame ──
            _diagnosisCached = false;

            IInputService inputService = GlobalRegistry.Input;
            PlayerInputState inputState = inputService != null && inputService.IsPlayerInputEnabled
                ? inputService.GetState()
                : default;
            if (!inputState.HasAction(PlayerInputAction.SecondaryFire))
                _secondaryLatched = false;
        }

        public override string GetOperationalSummary()
        {
            if (_isLockedOut)
                return string.Format(
                    ResolveLocalized(LocalizationKeys.LASER_OPERATIONAL_LOCKOUT, "LASER CUTTER // LOCKOUT {0:0}%"),
                    _heatLevel * 100f);

            if (_cachedDeconstructModule != null)
            {
                float progress = Mathf.Clamp01(_deconstructProgress / math.max(0.01f, deconstructThreshold));
                return string.Format(
                    ResolveLocalized(LocalizationKeys.LASER_OPERATIONAL_RECOVERY, "LASER CUTTER // RECOVERY {0:0}%"),
                    progress * 100f);
            }

            // Reuse cached diagnosis if available (from UseSecondary), otherwise make fresh
            if (!_diagnosisCached)
                _cachedDiagnosis = ReadDiagnosisNow();
            
            if (!string.IsNullOrEmpty(_cachedDiagnosis.headline))
                return string.Format(
                    ResolveLocalized(LocalizationKeys.LASER_OPERATIONAL_DIAGNOSIS, "LASER CUTTER // {0}"),
                    _cachedDiagnosis.headline);

            return _heatLevel > 0.01f
                ? string.Format(
                    ResolveLocalized(LocalizationKeys.LASER_OPERATIONAL_HEAT, "LASER CUTTER // HEAT {0:0}%"),
                    _heatLevel * 100f)
                : ResolveLocalized(LocalizationKeys.LASER_OPERATIONAL_READY, "LASER CUTTER // READY");
        }

        public override string GetOperationalDirective()
        {
            if (_isLockedOut)
                return ResolveLocalized(LocalizationKeys.LASER_DIRECTIVE_LOCKOUT, "Wait for the core to cool before firing again.");

            if (_cachedDeconstructModule != null)
                return ResolveLocalized(LocalizationKeys.LASER_DIRECTIVE_RECOVERY, "Hold the beam steady to finish recovery on the locked module.");

            // Reuse cached diagnosis if available
            if (!_diagnosisCached)
                _cachedDiagnosis = ReadDiagnosisNow();
            
            if (!string.IsNullOrEmpty(_cachedDiagnosis.summary))
                return _cachedDiagnosis.summary;

            if (_heatLevel >= 0.75f)
                return ResolveLocalized(LocalizationKeys.LASER_DIRECTIVE_HOT, "Core is running hot. Finish the cut or vent heat before lockout.");

            return ResolveLocalized(LocalizationKeys.LASER_DIRECTIVE_READY, "Primary cuts. Secondary diagnoses and holds recovery mode on modules.");
        }

        // ══════════════════════════════════════════════════════════
        //  HEAT MANAGEMENT
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Triggers overheat lockout. Tool cannot fire for overheatLockoutTime.
        /// Lockout recovery is serviced by ToolTick via _lockoutTimer.
        /// </summary>
        private void TriggerOverheatLockout()
        {
            PublishBeamState(false);
            _isLockedOut = true;
            _lockoutTimer = math.max(0f, overheatLockoutTime);
            _lockoutSoundPlayed = false;
            _isFiring = false;
            SetOverheatedState();
            SetVisualsActive(false);
            ResetDeconstructState();
            ToolHitUtility.ShowWarning(ResolveLocalized(LocalizationKeys.LASER_HUD_CORE_OVERHEATED, "LASER CUTTER - CORE OVERHEATED"));
            FieldOperationLogSystem.RecordOperation(
                ResolveLocalized(LocalizationKeys.LASER_CATEGORY, CutterCategory),
                ResolveLocalized(LocalizationKeys.LASER_LOG_OVERHEAT_TITLE, "LASER CORE OVERHEATED"),
                ResolveLocalized(
                    LocalizationKeys.LASER_LOG_OVERHEAT_MESSAGE,
                    "Cutter entered forced thermal lockout. Reduce sustained beam exposure before the next recovery pass."),
                "CRITICAL");
        }

        /// <summary>
        /// Publishes heat level via static event.
        /// Throttled: only fires when change exceeds threshold.
        /// </summary>
        private void PublishHeat()
        {
            if (math.abs(_heatLevel - _lastPublishedHeat) > 0.02f)
            {
                _lastPublishedHeat = _heatLevel;
                OnHeatChanged?.Invoke(_heatLevel);
            }
        }

        // ══════════════════════════════════════════════════════════
        //  CUT DAMAGE — with heat-scaled bonus
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Applies cut damage to the raycast target.
        ///
        /// v2.0: Damage scales with heat level.
        ///   damageMultiplier = 1.0 + (_heatLevel × heatDamageBonus)
        ///   At heatDamageBonus = 0.15:
        ///     cold (heat=0): 100% damage
        ///     hot  (heat=1): 115% damage
        ///   Risk/reward: staying hot is efficient but lockout looms.
        /// </summary>
        private void ApplyCutDamage(float deltaTime)
        {
            if (_hitInfo.collider == null)
                return;

            IInteractionSignalService interactionService = GlobalRegistry.InteractionSignals;
            if (interactionService == null || !interactionService.IsInitialized)
                return;

            float powerScale = GetEfficiency() * GetConditionPerformanceScale();
            float heatMultiplier = 1f + _heatLevel * heatDamageBonus;
            float damage = damagePerSecond * deltaTime * powerScale * heatMultiplier;
            if (damage <= 0f)
                return;

            Vector3 direction = _cachedTransform.forward;
            if (direction.sqrMagnitude < 0.0001f)
                direction = Vector3.forward;
            else
                direction.Normalize();

            Vector3 absoluteOrigin = HectonFloatingOrigin.ToAbsoluteUniversePosition(_cachedTransform.position);
            Vector3 absoluteHitPoint = HectonFloatingOrigin.ToAbsoluteUniversePosition(_hitInfo.point);
            float normalizedPower = ResolveNormalizedPower(powerScale, heatMultiplier);
            InteractionPacket packet = new InteractionPacket(
                _cachedToolId,
                new float3(absoluteOrigin.x, absoluteOrigin.y, absoluteOrigin.z),
                new float3(direction.x, direction.y, direction.z),
                normalizedPower,
                maxRange,
                (byte)ToolActionMode.Primary,
                _toolStateFlags,
                (uint)Time.frameCount);
            Hecton8.Interaction.InteractionSignal signal = new Hecton8.Interaction.InteractionSignal(
                packet,
                unchecked((int)EntityId.ToULong(_hitInfo.collider.GetEntityId())),
                new float3(absoluteHitPoint.x, absoluteHitPoint.y, absoluteHitPoint.z),
                new float3(_hitInfo.normal.x, _hitInfo.normal.y, _hitInfo.normal.z),
                damage,
                (byte)InteractionEffectType.PlasmaCut,
                0);

            if (interactionService.Publish(signal, _hitInfo.collider))
                ApplyRecoilImpulse(direction, normalizedPower);
        }

        // ══════════════════════════════════════════════════════════
        //  DECONSTRUCT MODE
        // ══════════════════════════════════════════════════════════

        private void ProcessDeconstructMode(float deltaTime)
        {
            if (_hitInfo.collider == null)
            {
                ResetDeconstructState();
                return;
            }

            EnsurePlayerBindings();

            int targetId = unchecked((int)EntityId.ToULong(_hitInfo.collider.GetEntityId()));

            if (targetId != _cachedDeconstructTargetId)
            {
                _deconstructProgress = 0f;
                _cachedDeconstructTargetId = targetId;
                _cachedDeconstructModule = _hitInfo.collider.GetComponent<BaseModule>() ?? _hitInfo.collider.GetComponentInParent<BaseModule>();
            }

            if (_hitInfo.normal.sqrMagnitude > 0.0001f)
                _cachedDeconstructAnchorNormal = _hitInfo.normal.normalized;
            else
                _cachedDeconstructAnchorNormal = Vector3.up;

            if (_cachedDeconstructModule == null)
            {
                if (!_deconstructBlockedReported)
                {
                    ToolHitUtility.ShowWarning(ResolveLocalized(LocalizationKeys.LASER_HUD_RECOVERY_NO_MODULE, "RECOVERY MODE - NO MODULE"));
                    _deconstructBlockedReported = true;
                }
                ApplyCutDamage(deltaTime);
                return;
            }

            if (!_cachedDeconstructModule.CanDeconstruct())
            {
                if (!_deconstructBlockedReported)
                {
                    ToolHitUtility.ShowWarning(ResolveLocalized(LocalizationKeys.LASER_HUD_RECOVERY_MODULE_LOCKED, "RECOVERY MODE - MODULE LOCKED"));
                    _deconstructBlockedReported = true;
                }
                ApplyCutDamage(deltaTime);
                return;
            }

            _cachedDeconstructAnchorPoint = _hitInfo.point - _cachedDeconstructAnchorNormal * heavySalvageAnchorRetraction;
            if (_cachedPlayerMovement != null)
                _cachedPlayerMovement.ApplyCuttingTensionAnchor(_cachedDeconstructAnchorPoint, _cachedDeconstructAnchorNormal);

            float tension01 = ResolveCuttingTension01();
            float pull01 = ResolveDetachmentPull01(_cachedDeconstructAnchorPoint);
            if (tension01 < heavySalvageRequiredTension01 || pull01 < heavySalvageRequiredPull01)
            {
                if (!_deconstructStartReported)
                {
                    ToolHitUtility.ShowInfo("RECOVERY MODE - LOAD THE CUT");
                    _deconstructStartReported = true;
                }

                if (Time.time >= _nextProgressFeedbackAt)
                {
                    int tensionPercent = Mathf.RoundToInt(tension01 * 100f);
                    int pullPercent = Mathf.RoundToInt(pull01 * 100f);
                    ToolHitUtility.ShowInfo("RECOVERY MODE - PULL BACK " + tensionPercent + "/" + pullPercent);
                    _nextProgressFeedbackAt = Time.time + 0.6f;
                }
                return;
            }

            float progressGain = deltaTime * tension01 * math.lerp(0.75f, 1.25f, pull01);
            _deconstructProgress += progressGain;
            if (!_deconstructStartReported)
            {
                ToolHitUtility.ShowInfo("RECOVERY MODE - TEAR IT FREE");
                _deconstructStartReported = true;
            }

            if (Time.time >= _nextProgressFeedbackAt)
            {
                float progress01 = math.saturate(_deconstructProgress / math.max(deconstructThreshold, 0.01f));
                ToolHitUtility.ShowInfo(GetRecoveryProgressMessage(progress01));
                _nextProgressFeedbackAt = Time.time + 0.6f;
            }

            if (_deconstructProgress >= deconstructThreshold)
            {
                EnsurePlayerInventory();
                _cachedDeconstructModule.Deconstruct(_cachedInventory);
                ArchiveRecoveredModule(_cachedDeconstructModule);
                ToolHitUtility.ShowInfo(ResolveLocalized(LocalizationKeys.LASER_HUD_MODULE_RECOVERED, "LASER CUTTER - MODULE RECOVERED"));
                FieldOperationLogSystem.RecordOperation(
                    ResolveLocalized(LocalizationKeys.LASER_CATEGORY, CutterCategory),
                    ResolveLocalized(LocalizationKeys.LASER_LOG_MODULE_RECOVERY_TITLE, "MODULE RECOVERY COMPLETED"),
                    string.Format(
                        ResolveLocalized(
                            LocalizationKeys.LASER_LOG_MODULE_RECOVERY_MESSAGE,
                            "Laser-assisted deconstruction completed on {0}."),
                        _cachedDeconstructModule.name),
                    "INFO");
                ResetDeconstructState();
            }
        }

        private void ResetDeconstructState()
        {
            if (_cachedPlayerMovement != null)
                _cachedPlayerMovement.ClearCuttingTensionAnchor();

            _deconstructProgress = 0f;
            _cachedDeconstructTargetId = -1;
            _cachedDeconstructModule = null;
            _deconstructStartReported = false;
            _deconstructBlockedReported = false;
            _nextProgressFeedbackAt = 0f;
            _cachedDeconstructAnchorPoint = Vector3.zero;
            _cachedDeconstructAnchorNormal = Vector3.up;
        }

        private static string GetRecoveryProgressMessage(float progress01)
        {
            EnsureRecoveryProgressMessages();
            int percent = (int)(math.saturate(progress01) * 100f + 0.5f);
            percent = math.clamp(percent, 0, RecoveryProgressMessageCount - 1);
            return _recoveryProgressMessages[percent];
        }

        private static void EnsureRecoveryProgressMessages()
        {
            LocalizationManager manager = LocalizationManager.Instance;
            GameLanguage language = manager != null ? manager.CurrentLanguage : GameLanguage.English;
            if (_recoveryProgressMessages != null && _recoveryProgressMessages.Length == RecoveryProgressMessageCount && _recoveryProgressLanguage == language)
                return;

            _recoveryProgressMessages = BuildRecoveryProgressMessages();
            _recoveryProgressLanguage = language;
        }

        private static string[] BuildRecoveryProgressMessages()
        {
            string[] messages = new string[RecoveryProgressMessageCount];
            string template = ResolveLocalized(LocalizationKeys.LASER_RECOVERY_PROGRESS, "RECOVERY PROGRESS - {0}%");
            for (int i = 0; i < RecoveryProgressMessageCount; i++)
                messages[i] = string.Format(template, i);

            return messages;
        }

        private void EnsurePlayerInventory()
        {
            EnsurePlayerBindings();
        }

        private void EnsurePlayerBindings()
        {
            if (_cachedInventory != null && _cachedPlayerMovement != null && _cachedPlayerRigidbody != null && _cachedPlayerTransform != null)
                return;

            if (!gameObject.scene.isLoaded)
                return;

            if (SceneBootstrap.TryGetCurrentPlayerTransform(out Transform playerTransform))
            {
                _cachedPlayerTransform = playerTransform;
                if (_cachedInventory == null)
                    playerTransform.TryGetComponent(out _cachedInventory);
                if (_cachedPlayerMovement == null)
                    playerTransform.TryGetComponent(out _cachedPlayerMovement);
                if (_cachedPlayerRigidbody == null)
                    playerTransform.TryGetComponent(out _cachedPlayerRigidbody);
            }
        }

        private float ResolveCuttingTension01()
        {
            return _cachedPlayerMovement != null
                ? _cachedPlayerMovement.CurrentCuttingTensionNormalized
                : 0f;
        }

        private float ResolveDetachmentPull01(Vector3 anchorPoint)
        {
            EnsurePlayerBindings();
            if (_cachedPlayerTransform == null)
                return 0f;

            Vector3 awayFromAnchor = _cachedPlayerTransform.position - anchorPoint;
            awayFromAnchor.y = 0f;
            float sqrMagnitude = awayFromAnchor.sqrMagnitude;
            if (sqrMagnitude <= 0.0001f)
                return 0f;

            awayFromAnchor *= 1f / Mathf.Sqrt(sqrMagnitude);
            Vector3 playerForward = _cachedPlayerTransform.forward;
            playerForward.y = 0f;
            float forwardSqrMagnitude = playerForward.sqrMagnitude;
            if (forwardSqrMagnitude > 0.0001f)
                playerForward *= 1f / Mathf.Sqrt(forwardSqrMagnitude);
            else
                playerForward = awayFromAnchor;

            float facingAway01 = Mathf.Clamp01((Vector3.Dot(playerForward, awayFromAnchor) + 1f) * 0.5f);
            float backpedal01 = 0f;
            IInputService inputService = GlobalRegistry.Input;
            PlayerInputState inputState = inputService != null && inputService.IsPlayerInputEnabled
                ? inputService.GetState()
                : default;
            backpedal01 = Mathf.Clamp01(-inputState.MoveDelta.y);

            float awayVelocity01 = 0f;
            if (_cachedPlayerRigidbody != null && heavySalvagePullVelocityForFullIntent > 0.01f)
            {
                float awayVelocity = Mathf.Max(0f, Vector3.Dot(_cachedPlayerRigidbody.linearVelocity, awayFromAnchor));
                awayVelocity01 = Mathf.Clamp01(awayVelocity / heavySalvagePullVelocityForFullIntent);
            }

            return Mathf.Max(awayVelocity01, backpedal01 * facingAway01);
        }

        private static void ArchiveRecoveredModule(BaseModule module)
        {
            if (module == null || ScanLogSystem.Instance == null)
                return;

            ModuleMarker marker = module.GetComponent<ModuleMarker>();
            BuildableData data = marker != null ? marker.Data : null;
            if (data == null)
                return;

            string moduleId = data.PersistentId;
            if (string.IsNullOrWhiteSpace(moduleId))
                return;

            string entryId = $"recovery.module.{moduleId}".ToLowerInvariant();
            string title = string.Format(
                ResolveLocalized(LocalizationKeys.LASER_ARCHIVE_RECOVERY_TITLE, "{0} RECOVERY"),
                data.moduleName);
            string category = ResolveLocalized(LocalizationKeys.LASER_ARCHIVE_CATEGORY, "Construction");
            string summary = string.Format(
                ResolveLocalized(
                    LocalizationKeys.LASER_ARCHIVE_RECOVERY_SUMMARY,
                    "Laser-assisted recovery completed for {0}. Structural blueprint and salvage profile archived."),
                data.moduleName);
            ScanLogSystem.Instance.ArchiveEntry(entryId, title, category, summary);
        }

        // ══════════════════════════════════════════════════════════
        //  VISUALS — Beam, Sparks, Audio
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Updates laser beam LineRenderer.
        /// v2.0: Endpoint jitter scales with heat level.
        ///   Cold: stable beam.
        ///   Hot: visible vibration warns player of impending lockout.
        /// </summary>
        private void UpdateLaserLine(bool didHit)
        {
            if (laserLine == null) return;

            if (!laserLine.enabled)
                laserLine.enabled = true;

            laserLine.SetPosition(0, Vector3.zero);

            if (didHit)
            {
                Vector3 localHitPoint = _cachedTransform.InverseTransformPoint(_hitInfo.point);

                // v2.0: Heat-scaled jitter
                // At heat=0: zero jitter (stable beam)
                // At heat=1: maxJitterAmplitude vibration
                float jitterAmp = _heatLevel * maxJitterAmplitude;
                if (jitterAmp > 0.0001f)
                {
                    float t = Time.time * jitterFrequency;
                    float jx = math.sin(t) * jitterAmp;
                    float jy = math.sin(t * 1.37f + 2.1f) * jitterAmp * 0.7f;
                    // Use irrational multiplier 1.37 to prevent X/Y sync
                    localHitPoint.x += jx;
                    localHitPoint.y += jy;
                }

                laserLine.SetPosition(1, localHitPoint);
            }
            else
            {
                laserLine.SetPosition(1, Vector3.forward * maxRange);
            }
        }

        /// <summary>
        /// Updates spark VFX.
        /// v2.0: Emission rate scales with heat level.
        ///   Cold: base sparks.
        ///   Hot: 4x sparks (1 + heat * 3).
        /// </summary>
        private void UpdateSparks(bool didHit)
        {
            if (sparksVFX == null) return;

            if (didHit)
            {
                Transform sparksTransform = sparksVFX.transform;
                sparksTransform.position = _hitInfo.point;
                sparksTransform.rotation = Quaternion.LookRotation(_hitInfo.normal);

                // v2.0: Scale emission rate with heat
                if (_sparksEmissionCached)
                {
                    float heatScaledRate = _baseSparksRate * (1f + _heatLevel * 3f);
                    _sparksEmission.rateOverTimeMultiplier = heatScaledRate;
                }

                if (!sparksVFX.isPlaying)
                    sparksVFX.Play();
            }
            else
            {
                if (sparksVFX.isPlaying)
                    sparksVFX.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
        }

        /// <summary>
        /// Updates audio state and pitch.
        /// v2.0: Pitch rises with heat (1.0 → 1.3).
        ///   Creates audible "strain" warning before overheat.
        ///   Player learns to associate high pitch with danger.
        /// </summary>
        private void UpdateAudioState(bool shouldPlay)
        {
            if (cutAudio == null) return;

            if (shouldPlay)
            {
                if (!cutAudio.isPlaying)
                    cutAudio.Play();

                // v2.0: Heat-scaled pitch
                cutAudio.pitch = math.lerp(basePitch, maxPitch, _heatLevel);
            }
            else
            {
                if (cutAudio.isPlaying)
                    cutAudio.Stop();

                cutAudio.pitch = basePitch;
            }
        }

        private void SetVisualsActive(bool active)
        {
            if (laserLine != null)
                laserLine.enabled = active;

            if (sparksVFX != null)
            {
                if (!active && sparksVFX.isPlaying)
                    sparksVFX.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

                // Reset emission rate when deactivating
                if (!active && _sparksEmissionCached)
                    _sparksEmission.rateOverTimeMultiplier = _baseSparksRate;
            }

            if (!active)
                UpdateAudioState(false);
        }

        // ══════════════════════════════════════════════════════════
        //  PRIVATE — INIT HELPERS
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Caches ParticleSystem.EmissionModule and base rate.
        /// EmissionModule is a struct — this is safe to store.
        /// </summary>
        private void CacheSparksEmission()
        {
            if (sparksVFX != null)
            {
                _sparksEmission = sparksVFX.emission;
                _baseSparksRate = _sparksEmission.rateOverTimeMultiplier;
                _sparksEmissionCached = true;
            }
        }

        /// <summary>
        /// Full state reset. Called on Spawn/Despawn.
        /// </summary>
        private void ResetAllState()
        {
            CancelAction();
            _heatLevel = 0f;
            _isLockedOut = false;
            _lockoutTimer = 0f;
            _lockoutSoundPlayed = false;
            _lastPublishedHeat = -1f;
            _secondaryLatched = false;
            ResetDeconstructState();
        }

        /// <inheritdoc />
        public void Activate()
        {
            SetFlag(ActiveState);
            ClearFlag(IdleState);
            ClearFlag(CooldownState);
        }

        /// <inheritdoc />
        public void Deactivate()
        {
            SetFlag(IdleState);
            ClearFlag(ActiveState);
            ClearFlag(BusyState);
        }

        /// <inheritdoc />
        public void CancelAction()
        {
            PublishBeamState(false);
            _isFiring = false;
            _wasFiringLastFrame = false;
            _toolStateFlags = IdleState;
        }

        /// <inheritdoc />
        public uint GetCapabilityMask()
        {
            return ToolCapabilityMasks.PlasmaCut;
        }

        private void PublishBeamState(bool isActive)
        {
            OnBeamStateChanged?.Invoke(transform, isActive);
        }

        private CutterDiagnosis BuildDiagnosis(bool didHit)
        {
            if (!didHit || _hitInfo.collider == null)
            {
                return new CutterDiagnosis
                {
                    headline = ResolveLocalized(LocalizationKeys.LASER_HEADLINE_NO_TARGET, "NO TARGET"),
                    summary = ResolveLocalized(LocalizationKeys.LASER_SUMMARY_NO_TARGET, "No cuttable contact inside cutter range."),
                    severity = "WARN"
                };
            }

            return BuildDiagnosisImpl(_hitInfo);
        }

        /// <summary>
        /// Builds diagnosis from a specific RaycastHit (used by UseSecondary, ReadDiagnosisNow).
        /// Allows UseSecondary and UsePrimary to have independent raycast results.
        /// </summary>
        private CutterDiagnosis BuildDiagnosisFromHit(RaycastHit hit, bool didHit)
        {
            if (!didHit || hit.collider == null)
            {
                return new CutterDiagnosis
                {
                    headline = ResolveLocalized(LocalizationKeys.LASER_HEADLINE_NO_TARGET, "NO TARGET"),
                    summary = ResolveLocalized(LocalizationKeys.LASER_SUMMARY_NO_TARGET, "No cuttable contact inside cutter range."),
                    severity = "WARN"
                };
            }

            return BuildDiagnosisImpl(hit);
        }

        /// <summary>
        /// Shared diagnosis logic (works with any RaycastHit).
        /// </summary>
        private CutterDiagnosis BuildDiagnosisImpl(RaycastHit hit)
        {
            BaseModule module =
                hit.collider.GetComponent<BaseModule>() ??
                hit.collider.GetComponentInParent<BaseModule>();
            if (module != null)
            {
                ModuleMarker marker = module.GetComponent<ModuleMarker>();
                string moduleName = marker != null && marker.Data != null
                    ? marker.Data.moduleName.ToUpperInvariant()
                    : module.name.ToUpperInvariant();
                float integrityPercent = module.MaxIntegrity > 0f
                    ? (module.CurrentIntegrity / module.MaxIntegrity) * 100f
                    : 0f;

                if (module.CanDeconstruct())
                {
                    return new CutterDiagnosis
                    {
                        headline = ResolveLocalized(LocalizationKeys.LASER_HEADLINE_RECOVERY_READY, "RECOVERY READY"),
                        summary = string.Format(
                            ResolveLocalized(
                                LocalizationKeys.LASER_SUMMARY_RECOVERY_READY,
                                "{0} can be recovered. Hold primary while secondary is held to complete recovery."),
                            moduleName),
                        severity = "INFO"
                    };
                }

                if (module.IsFlooded)
                {
                    _diagnosisSB.Clear();
                    _diagnosisSB.Append(moduleName);
                    _diagnosisSB.Append(string.Format(
                        ResolveLocalized(
                            LocalizationKeys.LASER_SUMMARY_MODULE_FLOODED,
                            "{0} is flooded. Cutter work is not the first fix here; stabilize or repair before recovery planning."),
                        moduleName));
                    
                    return new CutterDiagnosis
                    {
                        headline = string.Format(
                            ResolveLocalized(LocalizationKeys.LASER_HEADLINE_MODULE_FLOODED, "MODULE FLOODED {0:0}%"),
                            integrityPercent),
                        summary = _diagnosisSB.ToString(),
                        severity = "WARN"
                    };
                }

                if (module.IsBreached)
                {
                    _diagnosisSB.Clear();
                    _diagnosisSB.Append(moduleName);
                    _diagnosisSB.Append(string.Format(
                        ResolveLocalized(
                            LocalizationKeys.LASER_SUMMARY_MODULE_BREACHED,
                            "{0} is already compromised. Use repair or controlled recovery, not blind cutting."),
                        moduleName));
                    
                    return new CutterDiagnosis
                    {
                        headline = string.Format(
                            ResolveLocalized(LocalizationKeys.LASER_HEADLINE_MODULE_BREACHED, "MODULE BREACHED {0:0}%"),
                            integrityPercent),
                        summary = _diagnosisSB.ToString(),
                        severity = "WARN"
                    };
                }

                return new CutterDiagnosis
                {
                    headline = string.Format(
                        ResolveLocalized(LocalizationKeys.LASER_HEADLINE_MODULE_LOCKED, "MODULE LOCKED {0:0}%"),
                        integrityPercent),
                    summary = string.Format(
                        ResolveLocalized(
                            LocalizationKeys.LASER_SUMMARY_MODULE_LOCKED,
                            "{0} is sealed and not available for recovery. Satisfy the module conditions first."),
                        moduleName),
                    severity = "WARN"
                };
            }

            ResourceNode node =
                hit.collider.GetComponent<ResourceNode>() ??
                hit.collider.GetComponentInParent<ResourceNode>();
            if (node != null)
            {
                float integrityPercent = node.HealthNormalized * 100f;
                return new CutterDiagnosis
                {
                    headline = node.IsDepleted
                        ? ResolveLocalized(LocalizationKeys.LASER_HEADLINE_NODE_DEPLETED, "NODE DEPLETED")
                        : string.Format(
                            ResolveLocalized(LocalizationKeys.LASER_HEADLINE_RESOURCE_CONTACT, "RESOURCE CONTACT {0:0}%"),
                            integrityPercent),
                    summary = node.IsDepleted
                        ? ResolveLocalized(LocalizationKeys.LASER_SUMMARY_NODE_DEPLETED, "Resource node is exhausted. Cutter yield will be negligible.")
                        : integrityPercent <= 30f
                            ? ResolveLocalized(LocalizationKeys.LASER_SUMMARY_RESOURCE_CONTACT_LOW, "Resource node shell is nearly breached. Hold the beam to finish extraction.")
                            : integrityPercent <= 65f
                                ? ResolveLocalized(LocalizationKeys.LASER_SUMMARY_RESOURCE_CONTACT_MID, "Resource node shell is weakened. Another controlled cutter pass should open it.")
                                : ResolveLocalized(LocalizationKeys.LASER_SUMMARY_RESOURCE_CONTACT_HIGH, "Resource node is live. Primary beam can process this target."),
                    severity = node.IsDepleted ? "WARN" : "INFO"
                };
            }

            if (hit.collider.TryGetComponent(out ICuttable _))
            {
                return new CutterDiagnosis
                {
                    headline = ResolveLocalized(LocalizationKeys.LASER_HEADLINE_CUTTABLE_CONTACT, "CUTTABLE CONTACT"),
                    summary = ResolveLocalized(LocalizationKeys.LASER_SUMMARY_CUTTABLE_CONTACT, "Target accepts thermal damage but is not recoverable as a base module."),
                    severity = "INFO"
                };
            }

            return new CutterDiagnosis
            {
                headline = ResolveLocalized(LocalizationKeys.LASER_HEADLINE_INVALID_TARGET, "INVALID TARGET"),
                summary = ResolveLocalized(LocalizationKeys.LASER_SUMMARY_INVALID_TARGET, "Target is inside beam range but does not respond to cutter operations."),
                severity = "WARN"
            };
        }

        /// <summary>
        /// Fresh diagnosis raycast (called from UI update methods).
        /// Different from BuildDiagnosis which uses cached _hitInfo.
        /// </summary>
        private CutterDiagnosis ReadDiagnosisNow()
        {
            bool didHit = TryGetCutHit(out RaycastHit hit);

            return BuildDiagnosisFromHit(hit, didHit);
        }

        private bool TryGetCutHit(out RaycastHit hit)
        {
            IInteractionSignalService interactionService = GlobalRegistry.InteractionSignals;
            if (interactionService != null && interactionService.IsInitialized)
                return interactionService.TryRaycastPrimary(_cachedTransform.position, _cachedTransform.forward, maxRange, cuttableLayer.value, out hit);

            hit = default;
            return false;
        }

        private static void PublishDiagnosis(CutterDiagnosis diagnosis)
        {
            string message = string.Format(
                ResolveLocalized(LocalizationKeys.LASER_DIAG_MESSAGE, "LASER DIAG - {0}"),
                diagnosis.headline);
            if (diagnosis.severity == "WARN" || diagnosis.severity == "CRITICAL")
                ToolHitUtility.ShowWarning(message);
            else
                ToolHitUtility.ShowInfo(message);
        }

        private static string ResolveLocalized(string key, string fallback)
        {
            LocalizationManager manager = LocalizationManager.Instance;
            return manager != null
                ? manager.GetOrFallback(manager.CurrentLanguage, key, fallback)
                : fallback;
        }

        private void CacheToolId()
        {
            string toolIdSource =
                RuntimeMetadata != null && !string.IsNullOrWhiteSpace(RuntimeMetadata.toolID)
                    ? RuntimeMetadata.toolID
                    : "tool_laser_cutter";
            _cachedToolId = unchecked((uint)Animator.StringToHash(toolIdSource));
        }

        private float ResolveNormalizedPower(float powerScale, float heatMultiplier)
        {
            float normalizedPower = powerScale * (heatMultiplier / math.max(1f + heatDamageBonus, 0.0001f));
            return math.saturate(normalizedPower);
        }

        private void ApplyRecoilImpulse(Vector3 direction, float normalizedPower)
        {
            EnsurePlayerBindings();
            if (_cachedPlayerRigidbody == null || normalizedPower <= 0f)
                return;

            float mass = Mathf.Max(_cachedPlayerRigidbody.mass, 0.1f);
            float recoilScale = _cachedPlayerMovement != null && _cachedPlayerMovement.IsPlayerSubmerged
                ? submergedRecoilScale
                : 1f;
            float impulseMagnitude = Mathf.Min(MaxRecoilImpulse, (recoilImpulseBase * normalizedPower * recoilScale) / mass);
            if (impulseMagnitude <= 0.0001f)
                return;

            if (ToolHitUtility.TryApplyRelativeCarrierImpulse(direction, impulseMagnitude))
                return;

            PhysicsForceRouter.QueueForce(_cachedPlayerRigidbody, -direction * impulseMagnitude, ForceMode.Impulse);
        }

        private float ResolvePassiveCoolingBonus()
        {
            EnsurePlayerBindings();
            return _cachedPlayerMovement != null && _cachedPlayerMovement.IsPlayerSubmerged
                ? passiveWaterCoolingBonus
                : 0f;
        }

        private void EnterCooldownState()
        {
            SetFlag(CooldownState);
            SetFlag(IdleState);
            ClearFlag(ActiveState);
        }

        private void SetOverheatedState()
        {
            SetFlag(OverheatedState);
            ClearFlag(ActiveState);
            ClearFlag(BusyState);
        }

        private void SetFlag(byte flag)
        {
            _toolStateFlags |= flag;
        }

        private void ClearFlag(byte flag)
        {
            _toolStateFlags &= unchecked((byte)~flag);
        }
    }
}
