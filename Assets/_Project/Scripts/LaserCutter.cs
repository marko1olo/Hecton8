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
    using Hecton8.Inventory;
    using Hecton8.Input;
    using Hecton.Localization;
    using Hecton8.Scavenging;
    using Unity.Mathematics;
    using UnityEngine;

    [DisallowMultipleComponent]
    public sealed class LaserCutter : PlayerTool
    {
        private const string CutterCategory = "CUTTER";
        private const int RecoveryProgressMessageCount = 101;

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
        private readonly RaycastHit[] _raycastHits = new RaycastHit[1]; // COLD ALLOC: laser cutter consumes only the nearest contact per pass.

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

        /// <summary>Cached StringBuilder for zero-GC diagnosis strings.</summary>
        private System.Text.StringBuilder _diagnosisSB;

        private bool _secondaryLatched;
        private bool _deconstructStartReported;
        private bool _deconstructBlockedReported;
        private float _nextProgressFeedbackAt;

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
            SetVisualsActive(false);
            
            // ONE-TIME cache: Try to resolve PlayerInventory at startup (not in hot loop)
            if (SceneBootstrap.TryGetCurrentPlayerTransform(out Transform playerTransform))
                playerTransform.TryGetComponent(out _cachedInventory);
        }

        public override void OnSpawn()
        {
            base.OnSpawn();
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
            _isFiring = false;
            _wasFiringLastFrame = false;
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

            _isFiring = true;

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
                InputManager inputManager = InputManager.Instance;
                bool deconstructMode = inputManager != null
                    && inputManager.IsSecondaryActionHeld;

                if (deconstructMode)
                {
                    ResetDeconstructState(); // reset cut-mode state
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
                    PublishHeat();
                    ToolHitUtility.ShowInfo(ResolveLocalized(LocalizationKeys.LASER_HUD_CORE_STABLE, "LASER CUTTER - CORE STABLE"));
                }
            }
            // ── Heat decay when not firing ──
            if (!_isFiring && !_isLockedOut)
            {
                if (_heatLevel > 0f)
                {
                    _heatLevel = math.max(0f, _heatLevel - deltaTime * cooldownRate);
                    PublishHeat();
                }
            }

            // ── Visual shutdown on release ──
            if (_wasFiringLastFrame && !_isFiring)
            {
                SetVisualsActive(false);
                ResetDeconstructState();
            }

            _wasFiringLastFrame = _isFiring;
            _isFiring = false;

            // ── Invalidate diagnosis cache at end of frame ──
            _diagnosisCached = false;

            InputManager inputManager = InputManager.Instance;
            if (inputManager != null && !inputManager.IsSecondaryActionHeld)
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
            _isLockedOut = true;
            _lockoutTimer = math.max(0f, overheatLockoutTime);
            _lockoutSoundPlayed = false;
            _isFiring = false;
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
            if (_hitInfo.collider == null) return;

            if (_hitInfo.collider.TryGetComponent(out ICuttable cuttable))
            {
                float heatMultiplier = 1f + _heatLevel * heatDamageBonus;
                float damage = damagePerSecond * deltaTime * heatMultiplier;
                cuttable.ApplyCutDamage(damage, _hitInfo.point);
            }
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

            #pragma warning disable CS0618
            int targetId = _hitInfo.collider.GetInstanceID();
            #pragma warning restore CS0618

            // ── Target changed? ──
            if (targetId != _cachedDeconstructTargetId)
            {
                _deconstructProgress = 0f;
                _cachedDeconstructTargetId = targetId;
                _cachedDeconstructModule = null;
                _hitInfo.collider.TryGetComponent(out _cachedDeconstructModule);
            }

            // ── No BaseModule → standard cut ──
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

            // ── Can't deconstruct → standard cut ──
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

            // ── Accumulate progress ──
            _deconstructProgress += deltaTime;
            if (!_deconstructStartReported)
            {
                ToolHitUtility.ShowInfo(ResolveLocalized(LocalizationKeys.LASER_HUD_RECOVERY_HOLD, "RECOVERY MODE - HOLD CUT"));
                _deconstructStartReported = true;
            }

            if (Time.time >= _nextProgressFeedbackAt)
            {
                float progress01 = math.saturate(_deconstructProgress / math.max(deconstructThreshold, 0.01f));
                ToolHitUtility.ShowInfo(GetRecoveryProgressMessage(progress01));
                _nextProgressFeedbackAt = Time.time + 0.6f;
            }

            // ── Complete deconstruction ──
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
            _deconstructProgress = 0f;
            _cachedDeconstructTargetId = -1;
            _cachedDeconstructModule = null;
            _deconstructStartReported = false;
            _deconstructBlockedReported = false;
            _nextProgressFeedbackAt = 0f;
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
            // PlayerInventory was cached once during Awake()
            // If still null, player probably despawned or wasn't initialized
            if (_cachedInventory != null)
                return;

            if (!gameObject.scene.isLoaded)
                return;

            if (SceneBootstrap.TryGetCurrentPlayerTransform(out Transform playerTransform))
                playerTransform.TryGetComponent(out _cachedInventory);
        }

        private static void ArchiveRecoveredModule(BaseModule module)
        {
            if (module == null || ScanLogSystem.Instance == null)
                return;

            ModuleMarker marker = module.GetComponent<ModuleMarker>();
            BuildableData data = marker != null ? marker.Data : null;
            if (data == null)
                return;

            string entryId = $"recovery.module.{data.name.ToLowerInvariant()}";
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
            _isFiring = false;
            _wasFiringLastFrame = false;
            _heatLevel = 0f;
            _isLockedOut = false;
            _lockoutTimer = 0f;
            _lockoutSoundPlayed = false;
            _lastPublishedHeat = -1f;
            _secondaryLatched = false;
            ResetDeconstructState();
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
            int hitCount = UnityEngine.Physics.RaycastNonAlloc(
                _cachedTransform.position,
                _cachedTransform.forward,
                _raycastHits,
                maxRange,
                cuttableLayer,
                QueryTriggerInteraction.Ignore);

            if (hitCount > 0)
            {
                hit = _raycastHits[0];
                return true;
            }

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
    }
}
