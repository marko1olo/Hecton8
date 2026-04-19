// ============================================================================
// HECTON-8 — MantaScooter.cs
// Handheld propulsion vehicle (Seaglide equivalent).
//
// ARCHITECTURE:
//   • PlayerTool-derived for inventory/tool slot integration
//   • IBatteryTool for BatteryCharger compatibility
//   • ITickable for active propulsion logic
//   • Zero GC: cached refs, MaterialPropertyBlock, pre-allocated arrays
//
// FEATURES:
//   • Increases swim speed while active and has battery
//   • Drains battery only while moving
//   • HUD display showing depth and battery %
// ============================================================================

namespace Hecton8.Gameplay
{
    using Hecton.Localization;
    using Hecton8.Audio;
    using Hecton8.Bootstrap;
    using Hecton8.Core;
    using Hecton8.Input;
    using Hecton8.Items;
    using Hecton8.Tools;
    using Hecton8.UI;
    using UnityEngine;

    /// <summary>
    /// Handheld propulsion scooter that increases swim speed.
    /// Implements IBatteryTool for battery swapping via BatteryCharger.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/Gameplay/Tools/Manta Scooter")]
    public sealed class MantaScooter : PlayerTool, IBatteryTool, ITickable, IPlayerTransportSource, IPlayerTransportLifecycleOwner
    {
        private const float DefaultTransportPropulsionReference = 800f;

        // ══════════════════════════════════════════════════════════
        //  INSPECTOR
        // ══════════════════════════════════════════════════════════

        [Header("── Propulsion ────────────────────────────────")]
        [Tooltip("Swim speed multiplier when scooter is active.")]
        [SerializeField, Range(1.5f, 4f)] private float speedMultiplier = 2.2f;

        [Tooltip("Optional shared transport preset. When assigned, propulsion and feel resolve from the preset instead of local fallback values.")]
        [SerializeField] private PlayerTransportPreset transportPreset;

        [Tooltip("Battery drain per second while moving.")]
        [SerializeField, Range(0.5f, 10f)] private float batteryDrainRate = 2f;

        [Tooltip("Minimum battery charge to activate (0-1).")]
        [SerializeField, Range(0f, 0.3f)] private float minChargeToActivate = 0.05f;

        [Header("── Audio ──────────────────────────────────────")]
        [Tooltip("Looping motor sound while active.")]
        [SerializeField] private AudioClip motorLoopClip;

        [Tooltip("Motor volume.")]
        [SerializeField, Range(0f, 1f)] private float motorVolume = 0.6f;

        [Header("── Visuals ────────────────────────────────────")]
        [Tooltip("Mesh to hide when battery is removed.")]
        [SerializeField] private GameObject batteryMesh;

        [Tooltip("Renderer for power indicator light.")]
        [SerializeField] private Renderer powerIndicatorRenderer;

        [Tooltip("Emission color when powered.")]
        [SerializeField] private Color powerOnColor = new Color(0f, 0.9f, 1f);

        [Tooltip("Emission color when low battery.")]
        [SerializeField] private Color lowBatteryColor = new Color(1f, 0.3f, 0f);

        [Header("── HUD Display ────────────────────────────────")]
        [Tooltip("Canvas group for the HUD display.")]
        [SerializeField] private CanvasGroup hudCanvasGroup;

        [Tooltip("Text component for depth display.")]
        [SerializeField] private TMPro.TMP_Text depthText;

        [Tooltip("Text component for battery display.")]
        [SerializeField] private TMPro.TMP_Text batteryText;

        // ══════════════════════════════════════════════════════════
        //  IBatteryTool STATE
        // ══════════════════════════════════════════════════════════

        private ItemData _batteryItem;
        private float _currentCharge;
        private bool _hasBattery;

        // ══════════════════════════════════════════════════════════
        //  RUNTIME STATE
        // ══════════════════════════════════════════════════════════

        private HectonPlayerMovement _playerMovement;
        private HectonSurvivalSystem _mantaSurvivalSystem;
        private AudioSource _motorAudioSource;
        private Rigidbody _playerRigidbody;
        private Transform _cachedTransform;
        private bool _isActive;
        private bool _isMoving;
        private float _driveThrottleCurrent;
        private bool _registeredTick;
        private bool _hudStateInitialized;
        private bool _lastHudVisible;
        private int _lastDepthTenths = int.MinValue;
        private int _lastBatteryPercent = int.MinValue;
        private bool _summaryStateInitialized;
        private bool _lastSummaryHasBattery;
        private bool _lastSummaryActive;
        private int _lastSummaryBatteryPercent = int.MinValue;
        private string _cachedOperationalSummary = "MANTA // NO BATTERY";
        private string _cachedOperationalDirective = "Insert a battery to activate propulsion.";
        private bool _directiveStateInitialized;
        private bool _lastDirectiveHasBattery;
        private bool _lastDirectiveActive;
        private bool _lastDirectiveBatteryLow;
        private string _localizedNoBatteryWarning = "MANTA - NO BATTERY";
        private string _localizedBatteryDepletedWarning = "MANTA - BATTERY DEPLETED";
        private string _localizedSummaryNoBattery = "MANTA // NO BATTERY";
        private string _localizedSummaryActiveFormat = "MANTA // ACTIVE // BAT {0}%";
        private string _localizedSummaryStandbyFormat = "MANTA // STANDBY // BAT {0}%";
        private string _localizedDirectiveInsertBattery = "Insert a battery to activate propulsion.";
        private string _localizedDirectiveSwapRecharge = "Battery depleted. Swap or recharge.";
        private string _localizedDirectiveHoldForward = "Hold forward to propel. Release to coast.";
        private string _localizedDirectiveHoldPrimary = "Hold primary to activate propulsion while swimming.";
        private string _localizedTransportBrokenWarning = "MANTA - DRIVE FAILURE";
        [SerializeField] private string _debugActivationState = ActivationStateIdle;

        // MaterialPropertyBlock for power indicator
        private MaterialPropertyBlock _mpb;
        private static readonly int _EmissionColorID = Shader.PropertyToID("_EmissionColor");
        private const string ActivationStateIdle = "Idle";
        private const string ActivationStateSpawned = "Spawned";
        private const string ActivationStateEquipped = "Equipped";
        private const string ActivationStateUnequipped = "Unequipped";
        private const string ActivationStateNotEquipped = "NotEquipped";
        private const string ActivationStateNoBattery = "NoBattery";
        private const string ActivationStateBatteryTooLow = "BatteryTooLow";
        private const string ActivationStateMissingPlayerMovement = "MissingPlayerMovement";
        private const string ActivationStateMissingRigidbody = "MissingRigidbody";
        private const string ActivationStateNotUnderwater = "NotUnderwater";
        private const string ActivationStateActivated = "Activated";
        private const string ActivationStateMoving = "ActiveMoving";
        private const string ActivationStateIdleInWater = "ActiveIdle";
        private const string ActivationStateBatteryDepleted = "BatteryDepleted";
        private const string ActivationStateBroken = "Broken";
        private float _currentIntegrity = -1f;
        private bool _transportLifecycleInitialized;
        private bool _isTransportBroken;

        // ══════════════════════════════════════════════════════════
        //  IBatteryTool IMPLEMENTATION
        // ══════════════════════════════════════════════════════════

        /// <summary>True if the tool currently has a battery installed.</summary>
        public bool HasBattery => _hasBattery;

        /// <summary>Current battery charge level (0-1). Returns 0 if no battery.</summary>
        public float BatteryCharge => _hasBattery ? _currentCharge : 0f;

        /// <summary>The battery item currently installed (null if none).</summary>
        public ItemData BatteryItem => _batteryItem;

        /// <summary>Latest deterministic activation state for runtime verification.</summary>
        public string DebugActivationState => _debugActivationState;

        /// <summary>True while Manta propulsion is actively engaged.</summary>
        public bool IsTransportActive => !_isTransportBroken && _isActive && _hasBattery && _currentCharge >= minChargeToActivate;

        /// <summary>True when this Manta can currently accept station charge.</summary>
        public bool CanReceiveTransportCharge => _hasBattery && !_isActive && _currentCharge < 0.999f;

        /// <summary>True when this Manta has failed structurally.</summary>
        public bool IsTransportBroken => _isTransportBroken;

        /// <summary>Current normalized battery charge treated as transport charge.</summary>
        public float TransportChargeNormalized => _hasBattery ? _currentCharge : 0f;

        /// <summary>Current normalized transport integrity.</summary>
        public float TransportIntegrityNormalized => ResolveCurrentIntegrityNormalized();

        /// <summary>
        /// Removes the battery from the tool.
        /// </summary>
        public ItemData RemoveBattery()
        {
            if (!_hasBattery)
                return null;

            ItemData removed = _batteryItem;
            _batteryItem = null;
            _currentCharge = 0f;
            _hasBattery = false;

            UpdateBatteryVisuals();
            UpdatePowerIndicator();

            return removed;
        }

        /// <summary>
        /// Inserts a battery into the tool.
        /// </summary>
        public bool InsertBattery(ItemData battery, float charge)
        {
            if (battery == null)
                return false;

            _batteryItem = battery;
            _currentCharge = Mathf.Clamp01(charge);
            _hasBattery = true;

            UpdateBatteryVisuals();
            UpdatePowerIndicator();

            return true;
        }

        // ══════════════════════════════════════════════════════════
        //  LIFECYCLE
        // ══════════════════════════════════════════════════════════

        private void Awake()
        {
            _cachedTransform = transform;
            _mpb = new MaterialPropertyBlock(); // COLD ALLOC: MaterialPropertyBlock[1] — power indicator emission — owner: MantaScooter
            RefreshMantaLocalizationCache();
            BindTransportPresetToFeelContract();
            EnsureTransportLifecycleInitialized();

            // Setup motor audio
            TryGetComponent(out _motorAudioSource);
            if (_motorAudioSource == null && motorLoopClip != null)
            {
                _motorAudioSource = gameObject.AddComponent<AudioSource>();
                _motorAudioSource.playOnAwake = false;
                _motorAudioSource.loop = true;
                _motorAudioSource.spatialBlend = 1f;
            }
        }

        private void OnEnable()
        {
            LocalizationManager.OnLanguageChanged += HandleMantaLanguageChanged;
            RefreshMantaLocalizationCache();
        }

        private void OnDisable()
        {
            LocalizationManager.OnLanguageChanged -= HandleMantaLanguageChanged;
        }

        public override void OnSpawn()
        {
            base.OnSpawn();
            _isActive = false;
            _registeredTick = false;
            _debugActivationState = ActivationStateSpawned;
            BindTransportPresetToFeelContract();
            EnsureTransportLifecycleInitialized();
            ResolvePlayerReferences();
            ResetHudStateCache();
            UpdateBatteryVisuals();
            UpdatePowerIndicator();
        }

        public override void OnDespawn()
        {
            DeactivateScooter();
            UnregisterFromTick();
            ResetHudStateCache();
            base.OnDespawn();
        }

        public override void OnEquip()
        {
            base.OnEquip();
            BindTransportPresetToFeelContract();
            ResolvePlayerReferences();
            _debugActivationState = ActivationStateEquipped;
            ResetHudStateCache();
            RegisterToTick();
            UpdateBatteryVisuals();
            UpdatePowerIndicator();
        }

        public override void OnUnequip()
        {
            DeactivateScooter();
            UnregisterFromTick();
            _debugActivationState = ActivationStateUnequipped;
            ResetHudStateCache();
            base.OnUnequip();
        }

        // ══════════════════════════════════════════════════════════
        //  TOOL ACTIONS
        // ══════════════════════════════════════════════════════════

        public override void UsePrimary(float deltaTime)
        {
            ResolvePlayerReferences();

            if (!IsEquipped)
            {
                _debugActivationState = ActivationStateNotEquipped;
                return;
            }

            if (_isTransportBroken)
            {
                if (_isActive)
                    DeactivateScooter();

                _debugActivationState = ActivationStateBroken;
                ToolHitUtility.ShowWarning(_localizedTransportBrokenWarning);
                return;
            }

            // Check battery
            if (!_hasBattery)
            {
                if (_isActive)
                    DeactivateScooter();

                _debugActivationState = ActivationStateNoBattery;
                ToolHitUtility.ShowWarning(_localizedNoBatteryWarning);
                return;
            }

            if (_currentCharge < minChargeToActivate)
            {
                if (_isActive)
                    DeactivateScooter();

                _debugActivationState = ActivationStateBatteryTooLow;
                ToolHitUtility.ShowWarning(_localizedNoBatteryWarning);
                return;
            }

            // Check if player is swimming
            if (_playerMovement == null)
            {
                if (_isActive)
                    DeactivateScooter();

                _debugActivationState = ActivationStateMissingPlayerMovement;
                return;
            }

            if (_playerMovement.CurrentLocomotionMode != PlayerLocomotionMode.UnderwaterSwim)
            {
                if (_isActive)
                    DeactivateScooter();

                _debugActivationState = ActivationStateNotUnderwater;
                return;
            }

            // Activate if not already active
            if (!_isActive)
                ActivateScooter();

            // Check if player is moving
            _isMoving = IsPlayerMoving();
            _debugActivationState = _isMoving ? ActivationStateMoving : ActivationStateIdleInWater;
            _driveThrottleCurrent = AdvanceDriveThrottle(_driveThrottleCurrent, 1f, deltaTime);
            float driveThrottleOutput = ResolveDriveThrottleOutput();

            if (_isMoving)
            {
                // Drain battery while moving
                _currentCharge = Mathf.Max(0f, _currentCharge - batteryDrainRate * driveThrottleOutput * deltaTime);
                UpdatePowerIndicator();
                UpdateHUD();

                if (_currentCharge <= 0f)
                {
                    DeactivateScooter();
                    _debugActivationState = ActivationStateBatteryDepleted;
                    ToolHitUtility.ShowWarning(_localizedBatteryDepletedWarning);
                }
            }
        }

        public override void UseSecondary(float deltaTime)
        {
            // Secondary does nothing for scooter - could be used for headlight toggle
        }

        public override void ToolTick(float deltaTime)
        {
            // Called by PlayerToolManager - we use ITickable for HUD updates
        }

        // ══════════════════════════════════════════════════════════
        //  ITickable
        // ══════════════════════════════════════════════════════════

        public void Tick(float deltaTime)
        {
            if (!IsEquipped)
                return;

            if (_isActive)
                TickDriveRelease(deltaTime);

            UpdateHUD();
        }

        // ══════════════════════════════════════════════════════════
        //  PUBLIC API
        // ══════════════════════════════════════════════════════════

        public override string GetOperationalSummary()
        {
            int batteryPercent = Mathf.RoundToInt(_currentCharge * 100f);
            if (!_summaryStateInitialized ||
                _lastSummaryHasBattery != _hasBattery ||
                _lastSummaryActive != _isActive ||
                _lastSummaryBatteryPercent != batteryPercent)
            {
                _cachedOperationalSummary = !_hasBattery
                    ? _localizedSummaryNoBattery
                    : _isActive
                        ? string.Format(_localizedSummaryActiveFormat, batteryPercent)
                        : string.Format(_localizedSummaryStandbyFormat, batteryPercent);

                _lastSummaryHasBattery = _hasBattery;
                _lastSummaryActive = _isActive;
                _lastSummaryBatteryPercent = batteryPercent;
                _summaryStateInitialized = true;
            }

            return _cachedOperationalSummary;
        }

        public override string GetOperationalDirective()
        {
            bool batteryLow = _hasBattery && _currentCharge < minChargeToActivate;
            if (!_directiveStateInitialized ||
                _lastDirectiveHasBattery != _hasBattery ||
                _lastDirectiveActive != _isActive ||
                _lastDirectiveBatteryLow != batteryLow)
            {
                _cachedOperationalDirective = !_hasBattery
                    ? _localizedDirectiveInsertBattery
                    : batteryLow
                        ? _localizedDirectiveSwapRecharge
                        : _isActive
                            ? _localizedDirectiveHoldForward
                            : _localizedDirectiveHoldPrimary;

                _lastDirectiveHasBattery = _hasBattery;
                _lastDirectiveActive = _isActive;
                _lastDirectiveBatteryLow = batteryLow;
                _directiveStateInitialized = true;
            }

            return _cachedOperationalDirective;
        }

        // ══════════════════════════════════════════════════════════
        //  PRIVATE — ACTIVATION
        // ══════════════════════════════════════════════════════════

        private void ActivateScooter()
        {
            _isActive = true;
            _debugActivationState = ActivationStateActivated;

            // Start motor sound
            if (_motorAudioSource != null && motorLoopClip != null && !_motorAudioSource.isPlaying)
            {
                _motorAudioSource.clip = motorLoopClip;
                _motorAudioSource.volume = motorVolume;
                _motorAudioSource.Play();
            }

            UpdatePowerIndicator();
        }

        private void DeactivateScooter()
        {
            _isActive = false;
            _isMoving = false;
            _driveThrottleCurrent = 0f;
            _debugActivationState = ActivationStateIdle;

            // Stop motor sound
            if (_motorAudioSource != null && _motorAudioSource.isPlaying)
                _motorAudioSource.Stop();

            UpdatePowerIndicator();
        }

        private bool IsPlayerMoving()
        {
            if (_playerRigidbody == null)
            {
                _debugActivationState = ActivationStateMissingRigidbody;
                return false;
            }

            return _playerRigidbody.linearVelocity.sqrMagnitude > 0.25f;
        }

        /// <summary>
        /// Gets the swim speed multiplier to apply when scooter is active.
        /// Called by HectonPlayerMovement.SwimPhysics.
        /// </summary>
        public float GetSpeedMultiplier()
        {
            if (_isTransportBroken || !_isActive || !_hasBattery || _currentCharge < minChargeToActivate)
                return 1f;

            return Mathf.Lerp(1f, ResolveConfiguredTransportSpeedMultiplier(), ResolveDriveThrottleOutput());
        }

        /// <summary>
        /// Gets the additional propulsion force to apply.
        /// Called by HectonPlayerMovement.SwimPhysics.
        /// </summary>
        public float GetPropulsionForce()
        {
            if (_isTransportBroken || !_isActive || !_hasBattery || _currentCharge < minChargeToActivate)
                return 0f;

            // Return additional force based on battery charge
            return ResolveConfiguredTransportPropulsionForce() * ResolveDriveThrottleOutput() * _currentCharge; // Scale force with battery level
        }

        /// <summary>
        /// Resolves transport propulsion force for generic transport consumers.
        /// </summary>
        public float GetTransportPropulsionForce()
        {
            return GetPropulsionForce();
        }

        /// <summary>
        /// Resolves transport speed multiplier for generic transport consumers.
        /// </summary>
        public float GetTransportSpeedMultiplier()
        {
            return GetSpeedMultiplier();
        }

        /// <summary>
        /// Resolves normalized transport boost for generic transport consumers.
        /// </summary>
        public float GetTransportBoost01()
        {
            float propulsionReference = ResolveTransportPropulsionReference();
            return Mathf.Clamp01(GetPropulsionForce() / propulsionReference);
        }

        /// <summary>
        /// Recharges the installed battery through a transport charging station.
        /// </summary>
        public void RechargeTransport(float normalizedChargeDelta)
        {
            if (!_hasBattery || normalizedChargeDelta <= 0f)
                return;

            _currentCharge = Mathf.Clamp01(_currentCharge + normalizedChargeDelta * ResolveStationChargeRateScale());
            UpdatePowerIndicator();
            UpdateHUD();
        }

        /// <summary>
        /// Applies collision impact damage to the Manta frame.
        /// </summary>
        public void ApplyTransportCollisionImpact(float impactSpeed, Vector3 hitPoint, Vector3 hitNormal)
        {
            if (_isTransportBroken)
                return;

            float startSpeed = ResolveCollisionDamageStartSpeed();
            if (impactSpeed <= startSpeed)
                return;

            float maxSpeed = ResolveCollisionDamageMaxSpeed(startSpeed);
            float maxDamage = ResolveCollisionDamageAtMaxSpeed();
            if (maxDamage <= 0f)
                return;

            EnsureTransportLifecycleInitialized();
            float damageT = Mathf.InverseLerp(startSpeed, maxSpeed, impactSpeed);
            float damage = Mathf.Lerp(0f, maxDamage, damageT);
            _currentIntegrity = Mathf.Max(0f, _currentIntegrity - damage);
            if (_currentIntegrity <= 0.0001f)
                BreakTransport();
        }

        // ══════════════════════════════════════════════════════════
        //  PRIVATE — VISUALS
        // ══════════════════════════════════════════════════════════

        private void UpdateBatteryVisuals()
        {
            if (batteryMesh != null && batteryMesh.activeSelf != _hasBattery)
                batteryMesh.SetActive(_hasBattery);
        }

        private float ResolveTransportPropulsionReference()
        {
            PlayerTransportFeelContract transportFeelContract = TransportFeelContract;
            if (transportFeelContract != null)
                return Mathf.Max(0.01f, transportFeelContract.PropulsionForceReference);

            if (transportPreset != null)
                return Mathf.Max(0.01f, transportPreset.PropulsionForceReference);

            return DefaultTransportPropulsionReference;
        }

        private float ResolveConfiguredTransportPropulsionForce()
        {
            if (transportPreset != null)
                return Mathf.Max(0f, transportPreset.PropulsionForce);

            return DefaultTransportPropulsionReference;
        }

        private float ResolveConfiguredTransportSpeedMultiplier()
        {
            if (transportPreset != null)
                return Mathf.Max(1f, transportPreset.SpeedMultiplier);

            return speedMultiplier;
        }

        private void BindTransportPresetToFeelContract()
        {
            PlayerTransportFeelContract transportFeelContract = TransportFeelContract;
            if (transportFeelContract != null && transportPreset != null)
                transportFeelContract.BindPreset(transportPreset);
        }

        private void TickDriveRelease(float deltaTime)
        {
            InputManager inputManager = InputManager.Instance;
            bool primaryHeld = inputManager != null && inputManager.IsPrimaryActionHeld;
            if (primaryHeld)
                return;

            _driveThrottleCurrent = AdvanceDriveThrottle(_driveThrottleCurrent, 0f, deltaTime);
            if (_driveThrottleCurrent <= 0.0001f)
                DeactivateScooter();
        }

        private float AdvanceDriveThrottle(float currentThrottle, float targetThrottle, float deltaTime)
        {
            float clampedCurrent = Mathf.Clamp01(currentThrottle);
            float clampedTarget = Mathf.Clamp01(targetThrottle);
            float sharpness = clampedTarget > clampedCurrent
                ? ResolveConfiguredThrottleRiseSharpness()
                : ResolveConfiguredThrottleFallSharpness();
            float blend = 1f - Mathf.Exp(-sharpness * deltaTime);
            return Mathf.Lerp(clampedCurrent, clampedTarget, blend);
        }

        private float ResolveDriveThrottleOutput()
        {
            return Mathf.Pow(Mathf.Clamp01(_driveThrottleCurrent), ResolveConfiguredThrottleOutputExponent());
        }

        private float ResolveConfiguredThrottleRiseSharpness()
        {
            if (transportPreset != null)
                return Mathf.Max(0.5f, transportPreset.ThrottleRiseSharpness);

            return 10f;
        }

        private float ResolveConfiguredThrottleFallSharpness()
        {
            if (transportPreset != null)
                return Mathf.Max(0.5f, transportPreset.ThrottleFallSharpness);

            return 8f;
        }

        private float ResolveConfiguredThrottleOutputExponent()
        {
            if (transportPreset != null)
                return Mathf.Max(0.5f, transportPreset.ThrottleOutputExponent);

            return 1f;
        }

        private void EnsureTransportLifecycleInitialized()
        {
            if (_transportLifecycleInitialized)
                return;

            _currentIntegrity = ResolveMaxIntegrity();
            _isTransportBroken = false;
            _transportLifecycleInitialized = true;
        }

        private float ResolveCurrentIntegrityNormalized()
        {
            EnsureTransportLifecycleInitialized();
            return Mathf.Clamp01(_currentIntegrity / ResolveMaxIntegrity());
        }

        private float ResolveMaxIntegrity()
        {
            if (transportPreset != null)
                return Mathf.Max(1f, transportPreset.MaxIntegrity);

            return 100f;
        }

        private float ResolveCollisionDamageStartSpeed()
        {
            if (transportPreset != null)
                return Mathf.Max(0f, transportPreset.CollisionDamageStartSpeed);

            return 6f;
        }

        private float ResolveCollisionDamageMaxSpeed(float minimum)
        {
            if (transportPreset != null)
                return Mathf.Max(minimum + 0.01f, transportPreset.CollisionDamageMaxSpeed);

            return Mathf.Max(minimum + 0.01f, 14f);
        }

        private float ResolveCollisionDamageAtMaxSpeed()
        {
            if (transportPreset != null)
                return Mathf.Max(0f, transportPreset.CollisionDamageAtMaxSpeed);

            return 42f;
        }

        private float ResolveStationChargeRateScale()
        {
            if (transportPreset != null)
                return Mathf.Max(0f, transportPreset.StationChargeRateScale);

            return 1f;
        }

        private void BreakTransport()
        {
            if (_isTransportBroken)
                return;

            _currentIntegrity = 0f;
            _isTransportBroken = true;
            DeactivateScooter();
            _debugActivationState = ActivationStateBroken;
            ToolHitUtility.ShowWarning(_localizedTransportBrokenWarning);
        }

        private void UpdatePowerIndicator()
        {
            if (powerIndicatorRenderer == null || _mpb == null)
                return;

            powerIndicatorRenderer.GetPropertyBlock(_mpb);

            if (!_hasBattery || _currentCharge <= 0f)
            {
                // No power - dark
                _mpb.SetColor(_EmissionColorID, Color.black);
            }
            else if (_currentCharge <= 0.2f)
            {
                // Low battery - orange
                _mpb.SetColor(_EmissionColorID, lowBatteryColor);
            }
            else if (_isActive)
            {
                // Active - bright cyan
                _mpb.SetColor(_EmissionColorID, powerOnColor * 2f);
            }
            else
            {
                // Standby - dim cyan
                _mpb.SetColor(_EmissionColorID, powerOnColor * 0.5f);
            }

            powerIndicatorRenderer.SetPropertyBlock(_mpb);
        }

        private void UpdateHUD()
        {
            if (hudCanvasGroup == null)
                return;

            // Show HUD only when equipped and active
            bool showHUD = IsEquipped && _hasBattery;
            if (!_hudStateInitialized || showHUD != _lastHudVisible)
            {
                hudCanvasGroup.alpha = showHUD ? 1f : 0f;
                hudCanvasGroup.blocksRaycasts = false;
                _lastHudVisible = showHUD;
                _hudStateInitialized = true;
            }

            if (!showHUD)
                return;

            // Update depth display
            if (depthText != null && _playerMovement != null)
            {
                int depthTenths = Mathf.RoundToInt(_playerMovement.CurrentDepth * 10f);
                if (depthTenths != _lastDepthTenths)
                {
                    depthText.SetText("{0:0.0}m", depthTenths * 0.1f);
                    _lastDepthTenths = depthTenths;
                }
            }

            // Update battery display
            if (batteryText != null)
            {
                int batteryPercent = Mathf.RoundToInt(_currentCharge * 100f);
                if (batteryPercent != _lastBatteryPercent)
                {
                    batteryText.SetText("{0:0}%", batteryPercent);
                    _lastBatteryPercent = batteryPercent;
                }
            }
        }

        // ══════════════════════════════════════════════════════════
        //  PRIVATE — REFERENCES
        // ══════════════════════════════════════════════════════════

        private void ResolvePlayerReferences()
        {
            if (_playerMovement == null)
                _playerMovement = GetComponentInParent<HectonPlayerMovement>();

            if (_mantaSurvivalSystem == null && _playerMovement != null)
                _playerMovement.TryGetComponent(out _mantaSurvivalSystem);

            if (_playerRigidbody == null && _playerMovement != null)
                _playerMovement.TryGetComponent(out _playerRigidbody);

            if ((_playerMovement == null || _mantaSurvivalSystem == null || _playerRigidbody == null) &&
                SceneBootstrap.TryGetCurrentPlayerTransform(out Transform playerTransform))
            {
                if (_playerMovement == null)
                    _playerMovement = playerTransform.GetComponent<HectonPlayerMovement>();

                if (_mantaSurvivalSystem == null)
                    _mantaSurvivalSystem = playerTransform.GetComponent<HectonSurvivalSystem>();

            if (_playerRigidbody == null)
                _playerRigidbody = playerTransform.GetComponent<Rigidbody>();
            }
        }

        private void ResetHudStateCache()
        {
            _hudStateInitialized = false;
            _lastHudVisible = false;
            _lastDepthTenths = int.MinValue;
            _lastBatteryPercent = int.MinValue;
            _summaryStateInitialized = false;
            _lastSummaryHasBattery = false;
            _lastSummaryActive = false;
            _lastSummaryBatteryPercent = int.MinValue;
            _cachedOperationalSummary = _localizedSummaryNoBattery;
            _directiveStateInitialized = false;
            _lastDirectiveHasBattery = false;
            _lastDirectiveActive = false;
            _lastDirectiveBatteryLow = false;
            _cachedOperationalDirective = _localizedDirectiveInsertBattery;
            if (!_isActive)
                _debugActivationState = ActivationStateIdle;
        }

        private void RegisterToTick()
        {
            if (_registeredTick)
                return;

            if (GameTickManager.Instance != null)
            {
                GameTickManager.Instance.Register(this);
                _registeredTick = true;
            }
        }

        private void UnregisterFromTick()
        {
            if (!_registeredTick)
                return;

            if (GameTickManager.Instance != null)
            {
                GameTickManager.Instance.Unregister(this);
                _registeredTick = false;
            }
        }

        private void HandleMantaLanguageChanged(GameLanguage language)
        {
            RefreshMantaLocalizationCache();
            ResetHudStateCache();
        }

        private void RefreshMantaLocalizationCache()
        {
            _localizedNoBatteryWarning = ResolveMantaLocalizedLabel(LocalizationKeys.MANTA_HUD_NO_BATTERY, "MANTA - NO BATTERY");
            _localizedBatteryDepletedWarning = ResolveMantaLocalizedLabel(LocalizationKeys.MANTA_HUD_BATTERY_DEPLETED, "MANTA - BATTERY DEPLETED");
            _localizedSummaryNoBattery = ResolveMantaLocalizedLabel(LocalizationKeys.MANTA_SUMMARY_NO_BATTERY, "MANTA // NO BATTERY");
            _localizedSummaryActiveFormat = ResolveMantaLocalizedLabel(LocalizationKeys.MANTA_SUMMARY_ACTIVE, "MANTA // ACTIVE // BAT {0}%");
            _localizedSummaryStandbyFormat = ResolveMantaLocalizedLabel(LocalizationKeys.MANTA_SUMMARY_STANDBY, "MANTA // STANDBY // BAT {0}%");
            _localizedDirectiveInsertBattery = ResolveMantaLocalizedLabel(LocalizationKeys.MANTA_DIRECTIVE_INSERT_BATTERY, "Insert a battery to activate propulsion.");
            _localizedDirectiveSwapRecharge = ResolveMantaLocalizedLabel(LocalizationKeys.MANTA_DIRECTIVE_SWAP_OR_RECHARGE, "Battery depleted. Swap or recharge.");
            _localizedDirectiveHoldForward = ResolveMantaLocalizedLabel(LocalizationKeys.MANTA_DIRECTIVE_HOLD_FORWARD, "Hold forward to propel. Release to coast.");
            _localizedDirectiveHoldPrimary = ResolveMantaLocalizedLabel(LocalizationKeys.MANTA_DIRECTIVE_HOLD_PRIMARY, "Hold primary to activate propulsion while swimming.");
            _localizedTransportBrokenWarning = ResolveMantaLocalizedLabel(LocalizationKeys.MANTA_HUD_BATTERY_DEPLETED, "MANTA - DRIVE FAILURE");
        }

        private static string ResolveMantaLocalizedLabel(string key, string fallback)
        {
            LocalizationManager manager = LocalizationManager.Instance;
            return manager != null
                ? manager.GetOrFallback(manager.CurrentLanguage, key, fallback)
                : fallback;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            BindTransportPresetToFeelContract();
        }
#endif
    }
}
