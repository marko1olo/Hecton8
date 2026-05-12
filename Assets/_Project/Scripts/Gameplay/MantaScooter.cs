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
    using Hecton8.Inventory;
    using Hecton8.Items;
    using Hecton8.Tools;
    using Hecton8.UI;
    using System;
    using System.Collections.Generic;
    using Unity.Mathematics;
    using UnityEngine;

    /// <summary>
    /// Handheld propulsion scooter that increases swim speed.
    /// Implements IBatteryTool for battery swapping via BatteryCharger.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/Gameplay/Tools/Manta Scooter")]
    public sealed class MantaScooter : PlayerTool, IBatteryTool, ITickable, IUpdatable, IPlayerTransportSource, IPlayerTransportLifecycleOwner, IDamageSignalEmitter, ILocalizationLanguageChangedListener
    {
        private const float DefaultTransportPropulsionReference = 800f;
        private const float ThrottleBlendSpeedFloor = 0.01f;
        private const float ThrottleBlendDenominatorFloor = 0.0001f;
        private const float ThrottleExponentEpsilon = 0.001f;
        private const float PadeOneTwelfth = 0.0833333333f;
        private const float DegreesToRadians = 0.017453292519943295f;
        private const float MaxSpotConeRadians = 1.56206965f;
        private const float CosFourthCoefficient = 0.0416666679f;
        private const float HeadlightNoiseCellsPerSecond = 64f;

        // ══════════════════════════════════════════════════════════
        //  INSPECTOR
        // ══════════════════════════════════════════════════════════

        [Header("── Propulsion ────────────────────────────────")]
        [Tooltip("Swim speed multiplier when scooter is active.")]
        [SerializeField, Range(1.5f, 4f)] private float speedMultiplier = 2.2f;

        [Tooltip("Drag coefficient multiplier blended in while scooter thrust is active.")]
        [SerializeField, Range(0.35f, 1.25f)] private float transportDragCoefficientMultiplier = 0.72f;

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

        [Header("Drive Misfires")]
        [Tooltip("Hull-stress threshold where the scooter starts suffering propulsion misfires.")]
        [SerializeField, Range(0f, 1f)] private float misfireStressThreshold = 0.7f;

        [Tooltip("Longest delay between misfire events when the hull is only barely above the failure threshold.")]
        [SerializeField, Range(0.1f, 10f)] private float misfireIntervalMax = 2.6f;

        [Tooltip("Shortest delay between misfire events under extreme hull stress.")]
        [SerializeField, Range(0.05f, 5f)] private float misfireIntervalMin = 0.72f;

        [Tooltip("Shortest time that a misfire stalls propulsion output.")]
        [SerializeField, Range(0.02f, 0.75f)] private float misfireStallDurationMin = 0.08f;

        [Tooltip("Longest time that a misfire stalls propulsion output.")]
        [SerializeField, Range(0.02f, 1.2f)] private float misfireStallDurationMax = 0.24f;

        [Tooltip("Minimum steering deviation applied to transport thrust during a misfire event.")]
        [SerializeField, Range(0f, 20f)] private float misfireDeviationMinDegrees = 5f;

        [Tooltip("Maximum steering deviation applied to transport thrust during a misfire event.")]
        [SerializeField, Range(0f, 20f)] private float misfireDeviationMaxDegrees = 10f;

        [Tooltip("Minimum duration of a forced EMP misfire lockout injected by abyssal hazards.")]
        [SerializeField, Range(0.1f, 6f)] private float empMisfireMinimumDuration = 1.5f;

        [Header("── Visuals ────────────────────────────────────")]
        [Tooltip("Mesh to hide when battery is removed.")]
        [SerializeField] private GameObject batteryMesh;

        [Tooltip("Renderer for power indicator light.")]
        [SerializeField] private Renderer powerIndicatorRenderer;

        [Tooltip("Optional pooled world-body prefab used when a handheld Manta catastrophically bails out at speed. Falls back to ToolData.worldPrefab when unset.")]
        [SerializeField] private GameObject bailoutWreckPrefab;

        [Tooltip("Emission color when powered.")]
        [SerializeField] private Color powerOnColor = new Color(0f, 0.9f, 1f);

        [Tooltip("Emission color when low battery.")]
        [SerializeField] private Color lowBatteryColor = new Color(1f, 0.3f, 0f);

        [Header("── Headlights ────────────────────────────────")]
        [Tooltip("Optional primary spotlight used for scooter volumetric shafts and abyssal lens failure.")]
        [SerializeField] private Light primaryHeadlight;

        [Tooltip("Optional secondary spotlight used for scooter volumetric shafts and abyssal lens failure.")]
        [SerializeField] private Light secondaryHeadlight;

        [Tooltip("Base shaft energy injected into the screen-space volumetric pass.")]
        [SerializeField, Range(0f, 4f)] private float headlightVolumetricStrength = 1.15f;

        [Tooltip("How aggressively hull stress shifts the headlight spectrum.")]
        [SerializeField, Range(0f, 1f)] private float headlightSpectrumGlitchStrength = 0.28f;

        [Tooltip("Maximum spotlight cone jitter introduced by hull stress.")]
        [SerializeField, Range(0f, 12f)] private float headlightAngleJitterMaxDegrees = 4.5f;

        [Tooltip("Maximum intensity modulation injected into the headlights by hull stress.")]
        [SerializeField, Range(0f, 0.5f)] private float headlightIntensityJitter = 0.16f;

        [Tooltip("Temporal frequency of the headlight lens-glitch noise.")]
        [SerializeField, Range(0.1f, 20f)] private float headlightGlitchFrequency = 4.8f;

        [Header("── HUD Display ────────────────────────────────")]
        [Tooltip("Canvas group for the HUD display.")]
        [SerializeField] private CanvasGroup hudCanvasGroup;

        [Tooltip("Text component for depth display.")]
        [SerializeField] private TMPro.TMP_Text depthText;

        [Tooltip("Text component for battery display.")]
        [SerializeField] private TMPro.TMP_Text batteryText;
        private char[] _depthHudBuffer;
        private char[] _batteryHudBuffer;
        private FixedCharBuffer _toolWarningBuffer = new FixedCharBuffer(128); // COLD ALLOC: char[128] - manta warning HUD staging buffer - owner: MantaScooter

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
        private VehicleUpgradeModule _vehicleUpgradeModule;
        private AudioSource _motorAudioSource;
        private Rigidbody _playerRigidbody;
        private Transform _cachedTransform;
        private bool _isActive;
        private bool _isMoving;
        private float _driveThrottleCurrent;
        private float _inventoryConditionDrainAccumulator;
        private int _inventoryConditionAnchorIndex = -1;
        private int _inventoryConditionAnchorHashId;
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
        private string[] _localizedSummaryActiveCache;
        private string[] _localizedSummaryStandbyCache;
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
        private float _misfireIntervalTimer;
        private float _misfireStallTimer;
        private float _misfireDeviationPitchDegrees;
        private float _misfireDeviationYawDegrees;
        private uint _misfireSequence;
        private float _empMisfireTimer;
        private Light[] _headlightSlots;
        private Vector4[] _headlightPositionsWs;
        private Vector4[] _headlightDirectionsWs;
        private Vector4[] _headlightColors;
        private Vector4[] _headlightConeData;
        private Color[] _headlightBaseColors;
        private float[] _headlightBaseSpotAngles;
        private float[] _headlightBaseIntensities;
        private float[] _headlightBaseRanges;
        private bool _headlightsRegisteredForShadowBudget;
        private bool _headlightStateInitialized;
        private float _headlightGlitchPhase;
        private Vector3 _lastPublishedVolumetricVelocity;
        private bool _hasLastPublishedVolumetricVelocity;
        // COLD ALLOC: List<IDamageSignalReceiver>[1] - handheld transport damage listeners (player trauma dispatcher) - owner: MantaScooter
        private readonly List<IDamageSignalReceiver> _damageReceivers = new List<IDamageSignalReceiver>(1);

        private const int MaxHeadlights = 2;
        private const int ParentComponentResolveDepth = 32;
        private static readonly int _HeadlightCountId = Shader.PropertyToID("_HectonScooterHeadlightCount");
        private static readonly int _HeadlightPositionsWsId = Shader.PropertyToID("_HectonScooterHeadlightPositionsWS");
        private static readonly int _HeadlightDirectionsWsId = Shader.PropertyToID("_HectonScooterHeadlightDirectionsWS");
        private static readonly int _HeadlightColorsId = Shader.PropertyToID("_HectonScooterHeadlightColors");
        private static readonly int _HeadlightConeDataId = Shader.PropertyToID("_HectonScooterHeadlightConeData");
        private static readonly int _ScooterVelocityWsId = Shader.PropertyToID("_HectonScooterVelocityWS");
        private static readonly int _ScooterBrakeCloudId = Shader.PropertyToID("_HectonScooterBrakeCloud");

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

        internal int CopyHeadlightPayloadNonAlloc(
            Vector4[] positionsWs,
            Vector4[] directionsWs,
            Vector4[] colors,
            Vector4[] coneData)
        {
            if (positionsWs == null ||
                directionsWs == null ||
                colors == null ||
                coneData == null ||
                positionsWs.Length < MaxHeadlights ||
                directionsWs.Length < MaxHeadlights ||
                colors.Length < MaxHeadlights ||
                coneData.Length < MaxHeadlights ||
                _headlightPositionsWs == null ||
                _headlightDirectionsWs == null ||
                _headlightColors == null ||
                _headlightConeData == null)
            {
                return 0;
            }

            int activeCount = 0;
            for (int slotIndex = 0; slotIndex < MaxHeadlights; slotIndex++)
            {
                Vector4 payloadPosition = _headlightPositionsWs[slotIndex];
                Vector4 payloadDirection = _headlightDirectionsWs[slotIndex];
                Vector4 payloadColor = _headlightColors[slotIndex];
                Vector4 payloadCone = _headlightConeData[slotIndex];
                positionsWs[slotIndex] = payloadPosition;
                directionsWs[slotIndex] = payloadDirection;
                colors[slotIndex] = payloadColor;
                coneData[slotIndex] = payloadCone;
                if (payloadColor.w > 0.0001f && payloadPosition.w > 0.0001f)
                    activeCount++;
            }

            return activeCount;
        }

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
            ResetInventoryConditionDrainCache();

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
            _currentCharge = math.saturate(charge);
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
            TryGetComponent(out _vehicleUpgradeModule);
            _headlightSlots = new Light[MaxHeadlights]; // COLD ALLOC: Light[2] â€” scooter headlight cache for volumetric shafts â€” owner: MantaScooter
            _headlightPositionsWs = new Vector4[MaxHeadlights]; // COLD ALLOC: Vector4[2] â€” scooter headlight world-position payloads â€” owner: MantaScooter
            _headlightDirectionsWs = new Vector4[MaxHeadlights]; // COLD ALLOC: Vector4[2] â€” scooter headlight direction payloads â€” owner: MantaScooter
            _headlightColors = new Vector4[MaxHeadlights]; // COLD ALLOC: Vector4[2] â€” scooter headlight spectral payloads â€” owner: MantaScooter
            _headlightConeData = new Vector4[MaxHeadlights]; // COLD ALLOC: Vector4[2] â€” scooter headlight cone payloads â€” owner: MantaScooter
            _headlightBaseColors = new Color[MaxHeadlights]; // COLD ALLOC: Color[2] â€” authored headlight colors for recovery after hull-stress glitches â€” owner: MantaScooter
            _headlightBaseSpotAngles = new float[MaxHeadlights]; // COLD ALLOC: float[2] â€” authored headlight cone cache for recovery after hull-stress glitches â€” owner: MantaScooter
            _headlightBaseIntensities = new float[MaxHeadlights]; // COLD ALLOC: float[2] â€” authored headlight intensity cache for recovery after hull-stress glitches â€” owner: MantaScooter
            _headlightBaseRanges = new float[MaxHeadlights]; // COLD ALLOC: float[2] â€” authored headlight range cache for shaft falloff publishing â€” owner: MantaScooter
            _mpb = new MaterialPropertyBlock(); // COLD ALLOC: MaterialPropertyBlock[1] — power indicator emission — owner: MantaScooter
            _depthHudBuffer = new char[16]; // COLD ALLOC: char[16] - scooter depth HUD buffer - owner: MantaScooter
            _batteryHudBuffer = new char[8]; // COLD ALLOC: char[8] - scooter battery HUD buffer - owner: MantaScooter
            RefreshMantaLocalizationCache();
            BindTransportPresetToFeelContract();
            EnsureTransportLifecycleInitialized();
            CacheHeadlightDefaults();
            RegisterHeadlightShadowBudget();
            ClearHeadlightGlobals();

            // Setup motor audio
            TryGetComponent(out _motorAudioSource);
            TryAssignMotorMixerRoute();
        }

        private void OnEnable()
        {
            LocalizationEvents.RegisterLanguageListener(this);
            RefreshMantaLocalizationCache();
            RegisterHeadlightShadowBudget();
            TryAssignMotorMixerRoute();
        }

        private void OnDisable()
        {
            LocalizationEvents.UnregisterLanguageListener(this);
            UnregisterHeadlightShadowBudget();
            RestoreHeadlightDefaults();
            ClearHeadlightGlobals();
        }

        public override void OnSpawn()
        {
            base.OnSpawn();
            ResolveVehicleUpgradeModule();
            _isActive = false;
            _registeredTick = false;
            _debugActivationState = ActivationStateSpawned;
            ResetMisfireState();
            _empMisfireTimer = 0f;
            BindTransportPresetToFeelContract();
            EnsureTransportLifecycleInitialized();
            ResolvePlayerReferences();
            ResetHudStateCache();
            CacheHeadlightDefaults();
            RegisterHeadlightShadowBudget();
            ClearHeadlightGlobals();
            UpdateBatteryVisuals();
            UpdatePowerIndicator();
        }

        public override void OnDespawn()
        {
            DeactivateScooter();
            RestoreHeadlightDefaults();
            ClearHeadlightGlobals();
            UnregisterHeadlightShadowBudget();
            UnregisterFromTick();
            ResetHudStateCache();
            _damageReceivers.Clear();
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
            RestoreHeadlightDefaults();
            ClearHeadlightGlobals();
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
                PublishToolWarning(_localizedTransportBrokenWarning);
                return;
            }

            // Check battery
            if (!_hasBattery)
            {
                if (_isActive)
                    DeactivateScooter();

                _debugActivationState = ActivationStateNoBattery;
                PublishToolWarning(_localizedNoBatteryWarning);
                return;
            }

            if (_currentCharge < minChargeToActivate)
            {
                if (_isActive)
                    DeactivateScooter();

                _debugActivationState = ActivationStateBatteryTooLow;
                PublishToolWarning(_localizedNoBatteryWarning);
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
            UpdateHullStressMisfire(deltaTime, _isMoving);
            float driveThrottleOutput = ResolveEffectiveDriveThrottleOutput();

            if (_isMoving)
            {
                // Drain battery while moving
                float chargeDrain = ResolveEffectiveBatteryDrainRate() * driveThrottleOutput * deltaTime;
                _currentCharge = math.max(0f, _currentCharge - chargeDrain);
                DrainInventoryCondition(chargeDrain);
                UpdatePowerIndicator();
                UpdateHUD();

                if (_currentCharge <= 0f)
                {
                    DeactivateScooter();
                    _debugActivationState = ActivationStateBatteryDepleted;
                    PublishToolWarning(_localizedBatteryDepletedWarning);
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

            if (_empMisfireTimer > 0f)
            {
                _empMisfireTimer -= deltaTime;
                if (_empMisfireTimer < 0f)
                    _empMisfireTimer = 0f;
            }

            UpdateHeadlightState(deltaTime);

            if (_isActive)
            {
                IInputService inputService = GlobalRegistry.Input;
                PlayerInputState inputState = inputService != null && inputService.IsPlayerInputEnabled
                    ? inputService.GetState()
                    : default;
                if (!inputState.HasAction(PlayerInputAction.PrimaryFire))
                    UpdateHullStressMisfire(deltaTime, false);

                TickDriveRelease(deltaTime);
            }

            UpdateHUD();
        }

        // ══════════════════════════════════════════════════════════
        //  PUBLIC API
        // ══════════════════════════════════════════════════════════

        public override string GetOperationalSummary()
        {
            int batteryPercent = RoundToIntPositive(_currentCharge * 100f);
            if (!_summaryStateInitialized ||
                _lastSummaryHasBattery != _hasBattery ||
                _lastSummaryActive != _isActive ||
                _lastSummaryBatteryPercent != batteryPercent)
            {
                _cachedOperationalSummary = !_hasBattery
                    ? _localizedSummaryNoBattery
                    : _isActive
                        ? ResolveSummaryVariant(_localizedSummaryActiveCache, _localizedSummaryActiveFormat, batteryPercent)
                        : ResolveSummaryVariant(_localizedSummaryStandbyCache, _localizedSummaryStandbyFormat, batteryPercent);

                _lastSummaryHasBattery = _hasBattery;
                _lastSummaryActive = _isActive;
                _lastSummaryBatteryPercent = batteryPercent;
                _summaryStateInitialized = true;
            }

            return _cachedOperationalSummary;
        }

        public override void WriteOperationalSummary(ref FixedCharBuffer buffer)
        {
            if (!_hasBattery)
            {
                AppendText(ref buffer, _localizedSummaryNoBattery);
                return;
            }

            AppendText(ref buffer, _isActive ? "MANTA // ACTIVE // BAT " : "MANTA // STANDBY // BAT ");
            buffer.AppendInt(math.clamp((int)math.round(_currentCharge * 100f), 0, 100));
            AppendText(ref buffer, "%");
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

        public override void WriteOperationalDirective(ref FixedCharBuffer buffer)
        {
            bool batteryLow = _hasBattery && _currentCharge < minChargeToActivate;
            AppendText(
                ref buffer,
                !_hasBattery
                    ? _localizedDirectiveInsertBattery
                    : batteryLow
                        ? _localizedDirectiveSwapRecharge
                        : _isActive
                            ? _localizedDirectiveHoldForward
                            : _localizedDirectiveHoldPrimary);
        }

        // ══════════════════════════════════════════════════════════
        //  PRIVATE — ACTIVATION
        // ══════════════════════════════════════════════════════════

        private void ActivateScooter()
        {
            _isActive = true;
            _debugActivationState = ActivationStateActivated;

            if (PlayerCriticalProceduralAudioRenderer.IsRuntimeInstalled)
            {
                if (_motorAudioSource != null && _motorAudioSource.isPlaying)
                    _motorAudioSource.Stop();

                UpdatePowerIndicator();
                return;
            }

            // Start motor sound
            if (_motorAudioSource != null && motorLoopClip != null && !_motorAudioSource.isPlaying)
            {
                TryAssignMotorMixerRoute();
                _motorAudioSource.clip = motorLoopClip;
                _motorAudioSource.volume = motorVolume;
                _motorAudioSource.Play();
            }

            UpdatePowerIndicator();
        }

        private void TryAssignMotorMixerRoute()
        {
            if (_motorAudioSource == null || _motorAudioSource.outputAudioMixerGroup != null)
                return;

            if (GlobalRegistry.Audio is SpatialAudioManager spatialAudioManager)
                _motorAudioSource.outputAudioMixerGroup = spatialAudioManager.SfxGroup;
        }

        private void DeactivateScooter()
        {
            _isActive = false;
            _isMoving = false;
            _driveThrottleCurrent = 0f;
            _inventoryConditionDrainAccumulator = 0f;
            ResetInventoryConditionDrainCache();
            _debugActivationState = ActivationStateIdle;
            ResetMisfireState();

            if (PlayerCriticalProceduralAudioRenderer.IsRuntimeInstalled)
            {
                if (_motorAudioSource != null && _motorAudioSource.isPlaying)
                    _motorAudioSource.Stop();

                RestoreHeadlightDefaults();
                ClearHeadlightGlobals();
                UpdatePowerIndicator();
                return;
            }

            // Stop motor sound
            if (_motorAudioSource != null && _motorAudioSource.isPlaying)
                _motorAudioSource.Stop();

            RestoreHeadlightDefaults();
            ClearHeadlightGlobals();
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

            return math.lerp(1f, ResolveConfiguredTransportSpeedMultiplier(), ResolveEffectiveDriveThrottleOutput());
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
            return ResolveConfiguredTransportPropulsionForce() * ResolveEffectiveDriveThrottleOutput() * _currentCharge; // Scale force with battery level
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
        /// Resolves transport drag coefficient multiplier for generic transport consumers.
        /// </summary>
        public float GetTransportDragCoefficientMultiplier()
        {
            if (_isTransportBroken || !_isActive || !_hasBattery || _currentCharge < minChargeToActivate)
                return 1f;

            return math.lerp(1f, ResolveConfiguredTransportDragCoefficientMultiplier(), ResolveEffectiveDriveThrottleOutput());
        }

        /// <summary>
        /// Resolves normalized transport boost for generic transport consumers.
        /// </summary>
        public float GetTransportBoost01()
        {
            float propulsionReference = ResolveTransportPropulsionReference();
            return math.saturate(GetPropulsionForce() / propulsionReference);
        }

        /// <summary>
        /// Forces a temporary EMP misfire lockout on the active transport without spoofing hull stress.
        /// </summary>
        internal void ApplyEmpDisruption(float duration)
        {
            float disruptionDuration = math.max(empMisfireMinimumDuration, duration);
            if (disruptionDuration <= 0.0001f)
                return;

            _empMisfireTimer = math.max(_empMisfireTimer, disruptionDuration);
            _misfireIntervalTimer = 0f;
        }

        /// <summary>
        /// Returns the currently active misfire thrust deviation authored by hull stress.
        /// X = local pitch offset, Y = local yaw offset.
        /// </summary>
        internal bool TryGetHullStressMisfireDeviation(out Vector2 deviationDegrees)
        {
            if (_misfireStallTimer <= 0.0001f)
            {
                deviationDegrees = Vector2.zero;
                return false;
            }

            deviationDegrees = new Vector2(_misfireDeviationPitchDegrees, _misfireDeviationYawDegrees);
            return true;
        }

        /// <summary>
        /// Spawns a detached pooled wreck body for high-speed handheld bailout events.
        /// </summary>
        /// <param name="inheritedVelocity">Player body velocity at bailout time.</param>
        /// <param name="bailoutImpulse">Controller-resolved bailout impulse.</param>
        /// <param name="severity">Normalized crash severity.</param>
        internal bool TrySpawnEmergencyBailoutWreck(Vector3 inheritedVelocity, Vector3 bailoutImpulse, float severity)
        {
            GameObject wreckPrefab = bailoutWreckPrefab != null
                ? bailoutWreckPrefab
                : ToolData != null ? ToolData.worldPrefab : null;
            if (wreckPrefab == null)
                return false;

            ObjectPoolManager poolManager = GlobalRegistry.ObjectPool;
            if (poolManager == null)
                return false;

            Transform spawnTransform = _cachedTransform != null ? _cachedTransform : transform;
            GameObject wreckInstance = poolManager.Spawn(wreckPrefab, spawnTransform.position, spawnTransform.rotation);
            if (wreckInstance == null)
                return false;

            if (!wreckInstance.TryGetComponent(out MantaEmergencyWreck wreck))
                wreck = wreckInstance.AddComponent<MantaEmergencyWreck>();

            wreck.BindResidencyPrefabSource(wreckPrefab);
            wreck.ActivateEmergencyDrift(inheritedVelocity, bailoutImpulse, severity);
            BreakTransport();
            spawnTransform.gameObject.SetActive(false);
            return true;
        }

        /// <summary>
        /// Recharges the installed battery through a transport charging station.
        /// </summary>
        public void RechargeTransport(float normalizedChargeDelta)
        {
            if (!_hasBattery || normalizedChargeDelta <= 0f)
                return;

            _currentCharge = math.saturate(_currentCharge + normalizedChargeDelta * ResolveStationChargeRateScale());
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

            float previousIntegrityNormalized = ResolveCurrentIntegrityNormalized();
            float startSpeed = ResolveCollisionDamageStartSpeed();
            if (impactSpeed <= startSpeed)
                return;

            float maxSpeed = ResolveCollisionDamageMaxSpeed(startSpeed);
            float maxDamage = ResolveCollisionDamageAtMaxSpeed();
            if (maxDamage <= 0f)
                return;

            EnsureTransportLifecycleInitialized();
            float damageT = InverseLerpSaturated(startSpeed, maxSpeed, impactSpeed);
            float damage = math.lerp(0f, maxDamage, damageT);
            _currentIntegrity = math.max(0f, _currentIntegrity - damage);
            float nextIntegrityNormalized = ResolveCurrentIntegrityNormalized();
            DamageSignal damageSignal = BuildDamageSignal(impactSpeed, hitPoint, (uint)DamageTypeMask.Impact, previousIntegrityNormalized, nextIntegrityNormalized);
            DispatchIntegrityChanged(previousIntegrityNormalized, nextIntegrityNormalized, damageSignal);

            float previousPowerChannel = ResolvePowerChannel(previousIntegrityNormalized);
            float nextPowerChannel = ResolvePowerChannel(nextIntegrityNormalized);
            if (math.abs(nextPowerChannel - previousPowerChannel) > 0.0001f)
                DispatchPowerChanged(previousPowerChannel, nextPowerChannel, damageSignal);

            DispatchClarityChanged(0f, math.saturate(math.max(damageT, 1f - nextIntegrityNormalized)), damageSignal);
            DispatchTraumaThresholdCrossed(ResolveTraumaLevel(nextIntegrityNormalized, damageT));
            if (_currentIntegrity <= 0.0001f)
                BreakTransport();
        }

        /// <summary>
        /// Registers a damage receiver for scooter collision and failure packets.
        /// </summary>
        public void RegisterDamageReceiver(IDamageSignalReceiver receiver)
        {
            if (receiver == null)
                return;

            for (int i = 0; i < _damageReceivers.Count; i++)
            {
                if (ReferenceEquals(_damageReceivers[i], receiver))
                    return;
            }

            _damageReceivers.Add(receiver);
        }

        /// <summary>
        /// Unregisters a previously registered scooter damage receiver.
        /// </summary>
        public void UnregisterDamageReceiver(IDamageSignalReceiver receiver)
        {
            if (receiver == null)
                return;

            for (int i = _damageReceivers.Count - 1; i >= 0; i--)
            {
                if (ReferenceEquals(_damageReceivers[i], receiver))
                {
                    _damageReceivers.RemoveAt(i);
                    break;
                }
            }
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
                return math.max(0.01f, transportFeelContract.PropulsionForceReference);

            if (transportPreset != null)
                return math.max(0.01f, transportPreset.PropulsionForceReference);

            return DefaultTransportPropulsionReference;
        }

        private float ResolveConfiguredTransportPropulsionForce()
        {
            if (transportPreset != null)
                return math.max(0f, transportPreset.PropulsionForce);

            return DefaultTransportPropulsionReference;
        }

        private float ResolveConfiguredTransportSpeedMultiplier()
        {
            if (transportPreset != null)
                return math.max(1f, transportPreset.SpeedMultiplier);

            return speedMultiplier;
        }

        private float ResolveConfiguredTransportDragCoefficientMultiplier()
        {
            return math.clamp(transportDragCoefficientMultiplier, 0.01f, 4f);
        }

        private void DrainInventoryCondition(float normalizedDrain)
        {
            if (!math.isfinite(normalizedDrain) || normalizedDrain <= 0f)
                return;

            ItemData itemData = ToolData;
            int toolHashId = itemData != null ? itemData.PersistentHashId : 0;
            if (toolHashId == 0)
                return;

            _inventoryConditionDrainAccumulator += normalizedDrain;
            if (_inventoryConditionDrainAccumulator < 0.001f)
                return;

            IPlayerInventoryService inventoryService = GlobalRegistry.PlayerInventory;
            PlayerInventory inventory = inventoryService != null ? inventoryService.Inventory : GlobalRegistry.PlayerInventoryRuntime;
            if (inventory == null)
            {
                _inventoryConditionDrainAccumulator = math.min(_inventoryConditionDrainAccumulator, 0.01f);
                return;
            }

            ushort qualityMilli;
            if (_inventoryConditionAnchorHashId == toolHashId &&
                _inventoryConditionAnchorIndex >= 0 &&
                inventory.TryDrainItemConditionAtAnchor(
                    _inventoryConditionAnchorIndex,
                    toolHashId,
                    _inventoryConditionDrainAccumulator,
                    out qualityMilli))
            {
                ApplyInventoryConditionDrainResult(qualityMilli);
                return;
            }

            if (inventory.TryDrainItemConditionByHash(
                    toolHashId,
                    _inventoryConditionDrainAccumulator,
                    out int anchorIndex,
                    out qualityMilli))
            {
                _inventoryConditionAnchorIndex = anchorIndex;
                _inventoryConditionAnchorHashId = toolHashId;
                ApplyInventoryConditionDrainResult(qualityMilli);
                return;
            }

            ResetInventoryConditionDrainCache();
            _inventoryConditionDrainAccumulator = math.min(_inventoryConditionDrainAccumulator, 0.01f);
        }

        private void ApplyInventoryConditionDrainResult(ushort qualityMilli)
        {
            _currentCharge = math.min(_currentCharge, qualityMilli * 0.001f);
            _inventoryConditionDrainAccumulator = 0f;
        }

        private void ResetInventoryConditionDrainCache()
        {
            _inventoryConditionAnchorIndex = -1;
            _inventoryConditionAnchorHashId = 0;
        }

        private void BindTransportPresetToFeelContract()
        {
            PlayerTransportFeelContract transportFeelContract = TransportFeelContract;
            if (transportFeelContract != null && transportPreset != null)
                transportFeelContract.BindPreset(transportPreset);
        }

        private void TickDriveRelease(float deltaTime)
        {
            IInputService inputService = GlobalRegistry.Input;
            PlayerInputState inputState = inputService != null && inputService.IsPlayerInputEnabled
                ? inputService.GetState()
                : default;
            bool primaryHeld = inputState.HasAction(PlayerInputAction.PrimaryFire);
            if (primaryHeld)
                return;

            _driveThrottleCurrent = AdvanceDriveThrottle(_driveThrottleCurrent, 0f, deltaTime);
            if (_driveThrottleCurrent <= 0.0001f)
                DeactivateScooter();
        }

        private float AdvanceDriveThrottle(float currentThrottle, float targetThrottle, float deltaTime)
        {
            float clampedCurrent = math.saturate(currentThrottle);
            float clampedTarget = math.saturate(targetThrottle);
            float sharpness = clampedTarget > clampedCurrent
                ? ResolveConfiguredThrottleRiseSharpness()
                : ResolveConfiguredThrottleFallSharpness();
            float blend = FastThrottleDecayBlend01(sharpness, deltaTime);
            return math.lerp(clampedCurrent, clampedTarget, blend);
        }

        private float ResolveDriveThrottleOutput()
        {
            float throttle = math.saturate(_driveThrottleCurrent);
            float exponent = ResolveConfiguredThrottleOutputExponent();
            if (math.abs(exponent - 1f) <= ThrottleExponentEpsilon)
                return throttle;

            if (math.abs(exponent - 2f) <= ThrottleExponentEpsilon)
                return throttle * throttle;

            return ApproximateThrottlePower01(throttle, exponent);
        }

        private float ResolveEffectiveDriveThrottleOutput()
        {
            return ResolveDriveThrottleOutput() * ResolveMisfireThrottleScale();
        }

        private float ResolveMisfireThrottleScale()
        {
            return (_misfireStallTimer > 0.0001f || _empMisfireTimer > 0.0001f) ? 0f : 1f;
        }

        private float ResolveConfiguredThrottleRiseSharpness()
        {
            if (transportPreset != null)
                return math.max(0.5f, transportPreset.ThrottleRiseSharpness);

            return 10f;
        }

        private float ResolveConfiguredThrottleFallSharpness()
        {
            if (transportPreset != null)
                return math.max(0.5f, transportPreset.ThrottleFallSharpness);

            return 8f;
        }

        private float ResolveConfiguredThrottleOutputExponent()
        {
            if (transportPreset != null)
                return math.max(0.5f, transportPreset.ThrottleOutputExponent);

            return 1f;
        }

        private static float FastThrottleDecayBlend01(float blendSpeed, float deltaTime)
        {
            float x = math.max(ThrottleBlendSpeedFloor, blendSpeed) * math.max(0f, deltaTime);
            float x2 = x * x;
            float numerator = 1f - 0.5f * x + x2 * PadeOneTwelfth;
            float denominator = 1f + 0.5f * x + x2 * PadeOneTwelfth;
            return math.saturate(1f - numerator / math.max(ThrottleBlendDenominatorFloor, denominator));
        }

        private static float ApproximateThrottlePower01(float throttle, float exponent)
        {
            float t = math.saturate(throttle);
            float e = math.max(0.5f, exponent);
            float square = t * t;
            if (e <= 1f)
            {
                float pseudoRoot = math.saturate(t * (1.5f - 0.5f * t));
                return math.lerp(pseudoRoot, t, math.saturate((e - 0.5f) * 2f));
            }

            if (e <= 2f)
                return math.lerp(t, square, math.saturate(e - 1f));

            float cubic = square * t;
            return math.lerp(square, cubic, math.saturate(e - 2f));
        }

        private static int RoundToIntPositive(float value)
        {
            return value <= 0f ? 0 : (int)math.floor(value + 0.5f);
        }

        private static float InverseLerpSaturated(float min, float max, float value)
        {
            float range = max - min;
            if (math.abs(range) <= 0.000001f)
                return value >= max ? 1f : 0f;

            return math.saturate((value - min) / range);
        }

        private void ResolveVehicleUpgradeModule()
        {
            if (_vehicleUpgradeModule == null)
                TryGetComponent(out _vehicleUpgradeModule);
        }

        private float ResolveEffectiveBatteryDrainRate()
        {
            ResolveVehicleUpgradeModule();
            float drainScale = _vehicleUpgradeModule != null
                ? math.max(0.1f, _vehicleUpgradeModule.ChargeDrainScale)
                : 1f;
            float abyssalOverstrainMultiplier = _playerMovement != null
                ? _playerMovement.CurrentAbyssalCounterDriveEnergyMultiplier
                : 1f;
            return math.max(0f, batteryDrainRate * drainScale * abyssalOverstrainMultiplier);
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
            return math.saturate(_currentIntegrity / ResolveMaxIntegrity());
        }

        private float ResolveMaxIntegrity()
        {
            ResolveVehicleUpgradeModule();
            float integrityBonus = _vehicleUpgradeModule != null
                ? math.max(0f, _vehicleUpgradeModule.MaxIntegrityBonus)
                : 0f;

            if (transportPreset != null)
                return math.max(1f, transportPreset.MaxIntegrity + integrityBonus);

            return 100f + integrityBonus;
        }

        private float ResolveCollisionDamageStartSpeed()
        {
            if (transportPreset != null)
                return math.max(0f, transportPreset.CollisionDamageStartSpeed);

            return 6f;
        }

        private float ResolveCollisionDamageMaxSpeed(float minimum)
        {
            if (transportPreset != null)
                return math.max(minimum + 0.01f, transportPreset.CollisionDamageMaxSpeed);

            return math.max(minimum + 0.01f, 14f);
        }

        private float ResolveCollisionDamageAtMaxSpeed()
        {
            if (transportPreset != null)
                return math.max(0f, transportPreset.CollisionDamageAtMaxSpeed);

            return 42f;
        }

        private float ResolveStationChargeRateScale()
        {
            if (transportPreset != null)
                return math.max(0f, transportPreset.StationChargeRateScale);

            return 1f;
        }

        private void UpdateHullStressMisfire(float deltaTime, bool driveRequested)
        {
            if (!driveRequested || (_playerMovement == null && _empMisfireTimer <= 0.0001f))
            {
                ResetMisfireState();
                return;
            }

            float stress01 = math.max(ResolveHullStressMisfire01(), ResolveEmpMisfire01());
            if (stress01 <= 0f)
            {
                ResetMisfireState();
                return;
            }

            if (_misfireStallTimer > 0f)
            {
                _misfireStallTimer -= deltaTime;
                if (_misfireStallTimer <= 0f)
                {
                    _misfireStallTimer = 0f;
                    _misfireDeviationPitchDegrees = 0f;
                    _misfireDeviationYawDegrees = 0f;
                }
            }

            _misfireIntervalTimer -= deltaTime;
            if (_misfireIntervalTimer > 0f)
                return;

            StartHullStressMisfire(stress01);
        }

        private float ResolveEmpMisfire01()
        {
            return _empMisfireTimer > 0.0001f ? 1f : 0f;
        }

        private float ResolveHullStressMisfire01()
        {
            float threshold = math.saturate(misfireStressThreshold);
            if (_playerMovement == null || _playerMovement.CurrentHullStress01 <= threshold)
                return 0f;

            return InverseLerpSaturated(threshold, 1f, _playerMovement.CurrentHullStress01);
        }

        private void StartHullStressMisfire(float stress01)
        {
            _misfireSequence++;
            float interval = math.lerp(
                math.max(0.1f, misfireIntervalMax),
                math.max(0.05f, misfireIntervalMin),
                stress01);
            float duration = math.lerp(
                math.max(0.02f, misfireStallDurationMin),
                math.max(0.02f, misfireStallDurationMax),
                stress01);
            float deviationMagnitude = math.lerp(
                math.max(0f, misfireDeviationMinDegrees),
                math.max(0f, misfireDeviationMaxDegrees),
                stress01);
            float signedPitch = math.lerp(-1f, 1f, Hash01(_misfireSequence * 92821u + 17u));
            float signedYaw = math.lerp(-1f, 1f, Hash01(_misfireSequence * 68917u + 53u));

            _misfireIntervalTimer = interval;
            _misfireStallTimer = duration;
            _misfireDeviationPitchDegrees = signedPitch * deviationMagnitude * 0.55f;
            _misfireDeviationYawDegrees = signedYaw * deviationMagnitude;

            AcousticZoneController controller = GlobalRegistry.AcousticZone;
            if (controller != null)
                controller.PlayMantaMisfire(stress01);
        }

        private void ResetMisfireState()
        {
            _misfireIntervalTimer = 0f;
            _misfireStallTimer = 0f;
            _misfireDeviationPitchDegrees = 0f;
            _misfireDeviationYawDegrees = 0f;
        }

        private static float Hash01(uint value)
        {
            value ^= 2747636419u;
            value *= 2654435769u;
            value ^= value >> 16;
            value *= 2654435769u;
            value ^= value >> 16;
            return (value & 0x00FFFFFFu) / 16777215f;
        }

        private static float CheapUnsignedNoise(float phase, uint salt)
        {
            float sample = math.max(0f, phase) * HeadlightNoiseCellsPerSecond;
            uint cell = (uint)math.floor(sample);
            float t = math.frac(sample);
            float eased = t * t * (3f - 2f * t);
            float a = Hash01(cell * 747796405u + salt);
            float b = Hash01((cell + 1u) * 747796405u + salt);
            return math.lerp(a, b, eased);
        }

        private static float CheapSignedNoise(float phase, uint salt)
        {
            return CheapUnsignedNoise(phase, salt) * 2f - 1f;
        }

        private static float CheapPulse01(float phase, uint salt)
        {
            float t = math.frac(math.max(0f, phase) + Hash01(salt));
            return 1f - math.abs(t * 2f - 1f);
        }

        private static float ApproximateCosPositive(float radians)
        {
            float x = math.clamp(radians, 0f, MaxSpotConeRadians);
            float x2 = x * x;
            return math.saturate(1f - 0.5f * x2 + CosFourthCoefficient * x2 * x2);
        }

        private static Color LerpColor(Color from, Color to, float t)
        {
            float blend = math.saturate(t);
            return new Color(
                math.lerp(from.r, to.r, blend),
                math.lerp(from.g, to.g, blend),
                math.lerp(from.b, to.b, blend),
                math.lerp(from.a, to.a, blend));
        }

        private void DispatchIntegrityChanged(float prev, float next, DamageSignal signal)
        {
            for (int i = 0; i < _damageReceivers.Count; i++)
                _damageReceivers[i].OnIntegrityChanged(prev, next, signal);
        }

        private void DispatchPowerChanged(float prev, float next, DamageSignal signal)
        {
            for (int i = 0; i < _damageReceivers.Count; i++)
                _damageReceivers[i].OnPowerChanged(prev, next, signal);
        }

        private void DispatchClarityChanged(float prev, float next, DamageSignal signal)
        {
            for (int i = 0; i < _damageReceivers.Count; i++)
                _damageReceivers[i].OnClarityChanged(prev, next, signal);
        }

        private void DispatchTraumaThresholdCrossed(TraumaLevel level)
        {
            if (level == TraumaLevel.None)
                return;

            for (int i = 0; i < _damageReceivers.Count; i++)
                _damageReceivers[i].OnTraumaThresholdCrossed(level);
        }

        private DamageSignal BuildDamageSignal(
            float impactSpeed,
            Vector3 hitPoint,
            uint damageType,
            float previousIntegrityNormalized,
            float nextIntegrityNormalized)
        {
            DamageSignal signal = default;
            signal.magnitude = math.max(0f, impactSpeed);
            signal.localPoint = _cachedTransform != null
                ? (float3)_cachedTransform.InverseTransformPoint(hitPoint)
                : float3.zero;
            signal.damageType = damageType;
            signal.integrityDelta = (byte)math.clamp(
                RoundToIntPositive(math.abs(nextIntegrityNormalized - previousIntegrityNormalized) * byte.MaxValue),
                0,
                byte.MaxValue);
            signal.depth = _mantaSurvivalSystem != null ? math.max(0f, _mantaSurvivalSystem.Depth) : 0f;
            signal.sourceID = DamageSourceIds.MantaScooter;
            return signal;
        }

        private static float ResolvePowerChannel(float integrityNormalized)
        {
            return integrityNormalized >= 0.4f
                ? 1f
                : math.saturate(integrityNormalized / 0.4f);
        }

        private static TraumaLevel ResolveTraumaLevel(float integrityNormalized, float damageT)
        {
            if (integrityNormalized <= 0.0001f || damageT >= 0.98f)
                return TraumaLevel.Catastrophic;

            if (integrityNormalized < 0.4f || damageT >= 0.72f)
                return TraumaLevel.Critical;

            if (integrityNormalized < 0.65f || damageT >= 0.42f)
                return TraumaLevel.Significant;

            return damageT > 0.05f
                ? TraumaLevel.Minor
                : TraumaLevel.None;
        }

        private void BreakTransport()
        {
            if (_isTransportBroken)
                return;

            _currentIntegrity = 0f;
            _isTransportBroken = true;
            ResetMisfireState();
            _empMisfireTimer = 0f;
            DeactivateScooter();
            _debugActivationState = ActivationStateBroken;
            PublishToolWarning(_localizedTransportBrokenWarning);
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

        private void CacheHeadlightDefaults()
        {
            if (_headlightSlots == null ||
                _headlightBaseColors == null ||
                _headlightBaseSpotAngles == null ||
                _headlightBaseIntensities == null ||
                _headlightBaseRanges == null)
            {
                return;
            }

            _headlightSlots[0] = primaryHeadlight;
            _headlightSlots[1] = secondaryHeadlight;

            for (int slotIndex = 0; slotIndex < MaxHeadlights; slotIndex++)
            {
                Light headlight = _headlightSlots[slotIndex];
                if (headlight == null)
                {
                    _headlightBaseColors[slotIndex] = Color.black;
                    _headlightBaseSpotAngles[slotIndex] = 0f;
                    _headlightBaseIntensities[slotIndex] = 0f;
                    _headlightBaseRanges[slotIndex] = 0f;
                    continue;
                }

                _headlightBaseColors[slotIndex] = headlight.color;
                _headlightBaseSpotAngles[slotIndex] = headlight.spotAngle;
                _headlightBaseIntensities[slotIndex] = headlight.intensity;
                _headlightBaseRanges[slotIndex] = headlight.range;
            }

            _headlightStateInitialized = true;
        }

        private void RegisterHeadlightShadowBudget()
        {
            if (_headlightsRegisteredForShadowBudget)
                return;

            if (_headlightSlots == null)
                CacheHeadlightDefaults();

            bool registeredAny = false;
            for (int slotIndex = 0; slotIndex < MaxHeadlights; slotIndex++)
            {
                Light headlight = _headlightSlots[slotIndex];
                if (headlight == null)
                    continue;

                registeredAny |= HectonUrpShadowBudgetGuard.RegisterAuthoritativeForwardSpotlight(headlight);
            }

            _headlightsRegisteredForShadowBudget = registeredAny;
        }

        private void UnregisterHeadlightShadowBudget()
        {
            if (!_headlightsRegisteredForShadowBudget || _headlightSlots == null)
                return;

            for (int slotIndex = 0; slotIndex < MaxHeadlights; slotIndex++)
            {
                Light headlight = _headlightSlots[slotIndex];
                if (headlight == null)
                    continue;

                HectonUrpShadowBudgetGuard.UnregisterDynamicShadowLight(headlight);
            }

            _headlightsRegisteredForShadowBudget = false;
        }

        private void UpdateHeadlightState(float deltaTime)
        {
            if (!_headlightStateInitialized)
                CacheHeadlightDefaults();

            if (_headlightSlots == null ||
                _headlightPositionsWs == null ||
                _headlightDirectionsWs == null ||
                _headlightColors == null ||
                _headlightConeData == null)
            {
                return;
            }

            bool allowHeadlights = _isActive && !_isTransportBroken;
            float stress01 = allowHeadlights ? ResolveHullStressMisfire01() : 0f;
            _headlightGlitchPhase += deltaTime * math.lerp(0.35f, headlightGlitchFrequency, stress01);

            int activeCount = 0;
            for (int slotIndex = 0; slotIndex < MaxHeadlights; slotIndex++)
            {
                Light headlight = _headlightSlots[slotIndex];
                if (headlight == null)
                    continue;

                if (headlight.type != LightType.Spot)
                {
                    RestoreHeadlight(slotIndex, headlight);
                    continue;
                }

                if (!allowHeadlights || !headlight.enabled || !headlight.gameObject.activeInHierarchy)
                {
                    RestoreHeadlight(slotIndex, headlight);
                    continue;
                }

                ApplyHeadlightMalfunction(slotIndex, headlight, stress01);
                WriteHeadlightPayload(activeCount, headlight);
                activeCount++;
            }

            for (int payloadIndex = activeCount; payloadIndex < MaxHeadlights; payloadIndex++)
            {
                _headlightPositionsWs[payloadIndex] = Vector4.zero;
                _headlightDirectionsWs[payloadIndex] = Vector4.zero;
                _headlightColors[payloadIndex] = Vector4.zero;
                _headlightConeData[payloadIndex] = Vector4.zero;
            }

            PublishVolumetricSiltGlobals(deltaTime, allowHeadlights);
            Shader.SetGlobalInt(_HeadlightCountId, activeCount);
            Shader.SetGlobalVectorArray(_HeadlightPositionsWsId, _headlightPositionsWs);
            Shader.SetGlobalVectorArray(_HeadlightDirectionsWsId, _headlightDirectionsWs);
            Shader.SetGlobalVectorArray(_HeadlightColorsId, _headlightColors);
            Shader.SetGlobalVectorArray(_HeadlightConeDataId, _headlightConeData);
        }

        private void PublishVolumetricSiltGlobals(float deltaTime, bool allowHeadlights)
        {
            Vector3 velocity = allowHeadlights && _playerRigidbody != null ? _playerRigidbody.linearVelocity : Vector3.zero;
            float speedSq = velocity.sqrMagnitude;
            float speed = ApproximateMagnitudeFromSq(speedSq);
            float previousSpeedSq = _lastPublishedVolumetricVelocity.sqrMagnitude;
            float previousSpeed = ApproximateMagnitudeFromSq(previousSpeedSq);
            float brakeStrength = 0f;
            if (_hasLastPublishedVolumetricVelocity && previousSpeedSq > 0.01f && TryResolveSafeReciprocal(deltaTime, out float inverseDeltaTime))
            {
                Vector3 acceleration = SanitizeFiniteVector((velocity - _lastPublishedVolumetricVelocity) * inverseDeltaTime);
                float previousInvSpeed = math.rsqrt(previousSpeedSq);
                float brakingDeceleration = math.max(0f, Vector3.Dot(-acceleration, _lastPublishedVolumetricVelocity) * previousInvSpeed);
                float speedDrop = math.max(0f, previousSpeed - speed);
                brakeStrength = math.saturate(brakingDeceleration * 0.035f + speedDrop * 0.18f);
            }

            Shader.SetGlobalVector(_ScooterVelocityWsId, new Vector4(velocity.x, velocity.y, velocity.z, speed));
            Shader.SetGlobalFloat(_ScooterBrakeCloudId, brakeStrength);
            _lastPublishedVolumetricVelocity = velocity;
            _hasLastPublishedVolumetricVelocity = allowHeadlights;
        }

        private static float ApproximateMagnitudeFromSq(float magnitudeSq)
        {
            return magnitudeSq > 0.000001f ? magnitudeSq * math.rsqrt(magnitudeSq) : 0f;
        }

        private static bool TryResolveSafeReciprocal(float value, out float reciprocal)
        {
            if (!float.IsFinite(value) || math.abs(value) <= 0.0001f)
            {
                reciprocal = 0f;
                return false;
            }

            reciprocal = 1f / value;
            return float.IsFinite(reciprocal);
        }

        private static Vector3 SanitizeFiniteVector(Vector3 value)
        {
            return float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z)
                ? value
                : Vector3.zero;
        }

        private void ApplyHeadlightMalfunction(int slotIndex, Light headlight, float stress01)
        {
            if (headlight == null || slotIndex < 0 || slotIndex >= MaxHeadlights)
                return;

            Color baseColor = _headlightBaseColors[slotIndex];
            float baseSpotAngle = _headlightBaseSpotAngles[slotIndex];
            float baseIntensity = _headlightBaseIntensities[slotIndex];
            float baseRange = _headlightBaseRanges[slotIndex];

            if (stress01 <= 0.0001f)
            {
                headlight.color = baseColor;
                headlight.spotAngle = baseSpotAngle;
                headlight.intensity = baseIntensity;
                headlight.range = baseRange;
                return;
            }

            float primaryNoise = CheapSignedNoise(_headlightGlitchPhase + slotIndex * 0.37f, (uint)(slotIndex + 1) * 151u);
            float secondaryNoise = CheapSignedNoise(_headlightGlitchPhase * 0.71f + slotIndex * 0.29f, (uint)(slotIndex + 1) * 349u);
            float spectrumNoise = CheapUnsignedNoise(_headlightGlitchPhase * 0.43f + slotIndex, (uint)(slotIndex + 1) * 877u);
            float stressPulse = stress01 * CheapPulse01(_headlightGlitchPhase * 1.37f + slotIndex * 1.11f, (uint)(slotIndex + 1) * 1223u);
            float glitchNoise = math.max(math.abs(primaryNoise), math.abs(secondaryNoise));

            Color glitchedColor = new Color(
                math.saturate(baseColor.r * (1f + headlightSpectrumGlitchStrength * stressPulse)),
                math.saturate(baseColor.g * (1f - headlightSpectrumGlitchStrength * glitchNoise * 0.72f)),
                math.saturate(baseColor.b * (1f + headlightSpectrumGlitchStrength * math.lerp(-0.18f, 0.52f, spectrumNoise) * stress01)),
                baseColor.a);

            headlight.color = LerpColor(baseColor, glitchedColor, stress01);
            headlight.spotAngle = math.clamp(baseSpotAngle + primaryNoise * headlightAngleJitterMaxDegrees * stress01, 4f, 179f);
            headlight.intensity = math.max(0f, baseIntensity * (1f - headlightIntensityJitter * stress01 + math.abs(secondaryNoise) * headlightIntensityJitter * stress01));
            headlight.range = math.max(0.1f, baseRange * math.lerp(1f, 0.92f, stress01 * math.abs(primaryNoise)));
        }

        private void WriteHeadlightPayload(int payloadIndex, Light headlight)
        {
            if (payloadIndex < 0 || payloadIndex >= MaxHeadlights || headlight == null)
                return;

            float outerAngleRadians = math.max(1f, headlight.spotAngle * 0.5f) * DegreesToRadians;
            float innerAngleRadians = outerAngleRadians * 0.76f;
            float outerCos = ApproximateCosPositive(outerAngleRadians);
            float innerCos = ApproximateCosPositive(innerAngleRadians);
            Transform headlightTransform = headlight.transform;
            Vector3 directionWs = headlightTransform.forward;
            Color lightColor = headlight.color;
            Vector3 positionWs = headlightTransform.position;

            _headlightPositionsWs[payloadIndex] = new Vector4(
                positionWs.x,
                positionWs.y,
                positionWs.z,
                math.max(0.1f, headlight.range));

            _headlightDirectionsWs[payloadIndex] = new Vector4(
                directionWs.x,
                directionWs.y,
                directionWs.z,
                innerCos);

            _headlightColors[payloadIndex] = new Vector4(
                lightColor.r,
                lightColor.g,
                lightColor.b,
                math.max(0f, headlight.intensity));

            _headlightConeData[payloadIndex] = new Vector4(
                outerCos,
                math.max(0f, headlightVolumetricStrength),
                math.max(0f, headlight.range > 0.0001f ? 1f / headlight.range : 0f),
                1f);
        }

        private void RestoreHeadlightDefaults()
        {
            if (_headlightSlots == null)
                return;

            for (int slotIndex = 0; slotIndex < MaxHeadlights; slotIndex++)
            {
                RestoreHeadlight(slotIndex, _headlightSlots[slotIndex]);
            }
        }

        private void RestoreHeadlight(int slotIndex, Light headlight)
        {
            if (headlight == null || slotIndex < 0 || slotIndex >= MaxHeadlights)
                return;

            headlight.color = _headlightBaseColors[slotIndex];
            headlight.spotAngle = _headlightBaseSpotAngles[slotIndex];
            headlight.intensity = _headlightBaseIntensities[slotIndex];
            headlight.range = _headlightBaseRanges[slotIndex];
        }

        private void ClearHeadlightGlobals()
        {
            if (_headlightPositionsWs == null ||
                _headlightDirectionsWs == null ||
                _headlightColors == null ||
                _headlightConeData == null)
            {
                return;
            }

            for (int slotIndex = 0; slotIndex < MaxHeadlights; slotIndex++)
            {
                _headlightPositionsWs[slotIndex] = Vector4.zero;
                _headlightDirectionsWs[slotIndex] = Vector4.zero;
                _headlightColors[slotIndex] = Vector4.zero;
                _headlightConeData[slotIndex] = Vector4.zero;
            }

            Shader.SetGlobalInt(_HeadlightCountId, 0);
            Shader.SetGlobalVectorArray(_HeadlightPositionsWsId, _headlightPositionsWs);
            Shader.SetGlobalVectorArray(_HeadlightDirectionsWsId, _headlightDirectionsWs);
            Shader.SetGlobalVectorArray(_HeadlightColorsId, _headlightColors);
            Shader.SetGlobalVectorArray(_HeadlightConeDataId, _headlightConeData);
            Shader.SetGlobalVector(_ScooterVelocityWsId, Vector4.zero);
            Shader.SetGlobalFloat(_ScooterBrakeCloudId, 0f);
            _lastPublishedVolumetricVelocity = Vector3.zero;
            _hasLastPublishedVolumetricVelocity = false;
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
                int depthTenths = RoundToIntPositive(_playerMovement.CurrentDepth * 10f);
                if (depthTenths != _lastDepthTenths)
                {
                    SetDepthHudText(depthTenths);
                    _lastDepthTenths = depthTenths;
                }
            }

            // Update battery display
            if (batteryText != null)
            {
                int batteryPercent = RoundToIntPositive(_currentCharge * 100f);
                if (batteryPercent != _lastBatteryPercent)
                {
                    SetBatteryHudText(batteryPercent);
                    _lastBatteryPercent = batteryPercent;
                }
            }
        }

        private void SetDepthHudText(int depthTenths)
        {
            if (depthText == null || _depthHudBuffer == null)
                return;

            int length = WriteDepthHudBuffer(_depthHudBuffer, depthTenths);
            depthText.SetCharArray(_depthHudBuffer, 0, length);
            depthText.UpdateVertexData(TMPro.TMP_VertexDataUpdateFlags.All);
        }

        private void SetBatteryHudText(int batteryPercent)
        {
            if (batteryText == null || _batteryHudBuffer == null)
                return;

            int length = WritePercentHudBuffer(_batteryHudBuffer, batteryPercent);
            batteryText.SetCharArray(_batteryHudBuffer, 0, length);
            batteryText.UpdateVertexData(TMPro.TMP_VertexDataUpdateFlags.All);
        }

        private static int WriteDepthHudBuffer(char[] buffer, int depthTenths)
        {
            int clampedTenths = math.max(0, depthTenths);
            int wholeMeters = clampedTenths / 10;
            int tenths = clampedTenths % 10;
            int length = WriteUnsignedInt(buffer, 0, wholeMeters);
            buffer[length++] = '.';
            buffer[length++] = (char)('0' + tenths);
            buffer[length++] = 'm';
            return length;
        }

        private static int WritePercentHudBuffer(char[] buffer, int percent)
        {
            int clampedPercent = math.clamp(percent, 0, 100);
            int length = WriteUnsignedInt(buffer, 0, clampedPercent);
            buffer[length++] = '%';
            return length;
        }

        private static int WriteUnsignedInt(char[] buffer, int startIndex, int value)
        {
            if (value <= 0)
            {
                buffer[startIndex] = '0';
                return 1;
            }

            int digitCount = 0;
            int remaining = value;
            while (remaining > 0)
            {
                digitCount++;
                remaining /= 10;
            }

            int writeIndex = startIndex + digitCount - 1;
            int currentValue = value;
            while (currentValue > 0)
            {
                buffer[writeIndex--] = (char)('0' + (currentValue % 10));
                currentValue /= 10;
            }

            return digitCount;
        }

        // ══════════════════════════════════════════════════════════
        //  PRIVATE — REFERENCES
        // ══════════════════════════════════════════════════════════

        private void ResolvePlayerReferences()
        {
            if (_playerMovement == null)
                TryResolveParentComponent(_cachedTransform != null ? _cachedTransform : transform, out _playerMovement);

            if (_mantaSurvivalSystem == null && _playerMovement != null)
                _playerMovement.TryGetComponent(out _mantaSurvivalSystem);

            if (_playerRigidbody == null && _playerMovement != null)
                _playerMovement.TryGetComponent(out _playerRigidbody);

            if ((_playerMovement == null || _mantaSurvivalSystem == null || _playerRigidbody == null) &&
                GameBootstrapper.TryGetCurrentPlayerTransform(out Transform playerTransform))
            {
                if (_playerMovement == null)
                    playerTransform.TryGetComponent(out _playerMovement);

                if (_mantaSurvivalSystem == null)
                    playerTransform.TryGetComponent(out _mantaSurvivalSystem);

                if (_playerRigidbody == null)
                    playerTransform.TryGetComponent(out _playerRigidbody);
            }
        }

        private static bool TryResolveParentComponent<T>(Transform start, out T component) where T : Component
        {
            component = null;
            Transform current = start;
            for (int depth = 0; current != null && depth < ParentComponentResolveDepth; depth++)
            {
                if (current.TryGetComponent(out component))
                    return true;

                current = current.parent;
            }

            return false;
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
            if (!Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            _registeredTick = GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.Player);
        }

        private void UnregisterFromTick()
        {
            if (!_registeredTick)
                return;

            GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Player);
            _registeredTick = false;
        }

        public void OnLocalizationLanguageChanged(in LocalizationEventPayload payload)

        {

            HandleMantaLanguageChanged((GameLanguage)payload.Language);

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
            EnsureSummaryCache(ref _localizedSummaryActiveCache, _localizedSummaryActiveFormat);
            EnsureSummaryCache(ref _localizedSummaryStandbyCache, _localizedSummaryStandbyFormat);
            _localizedDirectiveInsertBattery = ResolveMantaLocalizedLabel(LocalizationKeys.MANTA_DIRECTIVE_INSERT_BATTERY, "Insert a battery to activate propulsion.");
            _localizedDirectiveSwapRecharge = ResolveMantaLocalizedLabel(LocalizationKeys.MANTA_DIRECTIVE_SWAP_OR_RECHARGE, "Battery depleted. Swap or recharge.");
            _localizedDirectiveHoldForward = ResolveMantaLocalizedLabel(LocalizationKeys.MANTA_DIRECTIVE_HOLD_FORWARD, "Hold forward to propel. Release to coast.");
            _localizedDirectiveHoldPrimary = ResolveMantaLocalizedLabel(LocalizationKeys.MANTA_DIRECTIVE_HOLD_PRIMARY, "Hold primary to activate propulsion while swimming.");
            _localizedTransportBrokenWarning = ResolveMantaLocalizedLabel(LocalizationKeys.MANTA_HUD_BATTERY_DEPLETED, "MANTA - DRIVE FAILURE");
        }

        private static string ResolveSummaryVariant(string[] cache, string fallbackFormat, int batteryPercent)
        {
            int clampedPercent = math.clamp(batteryPercent, 0, 100);
            if (cache == null || cache.Length <= clampedPercent)
                return fallbackFormat;

            string cachedVariant = cache[clampedPercent];
            if (string.IsNullOrEmpty(cachedVariant))
                return fallbackFormat;

            return cachedVariant;
        }

        private static void EnsureSummaryCache(ref string[] cache, string format)
        {
            if (cache == null || cache.Length != 101)
                cache = new string[101]; // COLD ALLOC: string[101] - localized battery summary lookup table - owner: MantaScooter

            for (int percent = 0; percent <= 100; percent++)
                cache[percent] = CreatePercentSummary(format, percent);
        }

        private static string CreatePercentSummary(string format, int percent)
        {
            if (string.IsNullOrEmpty(format))
                return string.Empty;

            int tokenIndex = format.IndexOf("{0}", System.StringComparison.Ordinal);
            if (tokenIndex < 0)
                return format;

            int clampedPercent = math.clamp(percent, 0, 100);
            int digitCount = CountUnsignedDigits(clampedPercent);
            return string.Create(format.Length - 3 + digitCount, (format, clampedPercent, tokenIndex), static (buffer, state) =>
            {
                state.format.AsSpan(0, state.tokenIndex).CopyTo(buffer);
                int cursor = state.tokenIndex;
                cursor += WriteUnsignedInt(buffer, cursor, state.clampedPercent);
                state.format.AsSpan(state.tokenIndex + 3).CopyTo(buffer.Slice(cursor));
            });
        }

        private static int CountUnsignedDigits(int value)
        {
            int digits = 1;
            int remaining = math.max(0, value);
            while (remaining >= 10)
            {
                remaining /= 10;
                digits++;
            }

            return digits;
        }

        private static int WriteUnsignedInt(System.Span<char> buffer, int startIndex, int value)
        {
            if (value <= 0)
            {
                buffer[startIndex] = '0';
                return 1;
            }

            int digitCount = CountUnsignedDigits(value);
            int writeIndex = startIndex + digitCount - 1;
            int currentValue = value;
            while (currentValue > 0)
            {
                buffer[writeIndex--] = (char)('0' + (currentValue % 10));
                currentValue /= 10;
            }

            return digitCount;
        }

        private static string ResolveMantaLocalizedLabel(string key, string fallback)
        {
            LocalizationManager manager = Hecton8.Core.GlobalRegistry.Localization;
            return manager != null
                ? manager.GetOrFallback(manager.CurrentLanguage, key, fallback)
                : fallback;
        }

        private static bool AppendText(ref FixedCharBuffer buffer, string value)
        {
            return string.IsNullOrEmpty(value) || buffer.Append(value);
        }

        private void PublishToolWarning(string message)
        {
            _toolWarningBuffer.Clear();
            if (AppendText(ref _toolWarningBuffer, message))
                ToolHitUtility.ShowWarning(in _toolWarningBuffer);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            empMisfireMinimumDuration = math.clamp(empMisfireMinimumDuration, 0.1f, 6f);
            BindTransportPresetToFeelContract();
        }
#endif
    }
}
