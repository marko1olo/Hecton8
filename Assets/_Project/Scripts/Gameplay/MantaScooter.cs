// ============================================================================
// HECTON-8 â€” MantaScooter.cs
// Handheld propulsion vehicle (Seaglide equivalent).
//
// ARCHITECTURE:
//   â€¢ PlayerTool-derived for inventory/tool slot integration
//   â€¢ IBatteryTool for BatteryCharger compatibility
//   â€¢ ITickable for active propulsion logic
//   â€¢ Zero GC: cached refs and pre-allocated arrays
//
// FEATURES:
//   â€¢ Increases swim speed while active and has battery
//   â€¢ Requests propulsion draw only while moving; central equipment solver drains battery
//   â€¢ HUD display showing depth and battery %
// ============================================================================

namespace Hecton8.Gameplay
{
    using Hecton.Localization;
    using Hecton8.Core;
    using Hecton8.Core.Contracts;
    using Hecton8.Core.Contracts.Physics;
    using Hecton8.Core.Contracts.Signals;
    using Hecton8.Items;
    using Hecton8.Tools;
    using Hecton8.UI;
    using System;
    using Unity.Mathematics;
    using UnityEngine;

    /// <summary>
    /// Handheld propulsion scooter that increases swim speed.
    /// Implements IBatteryTool for battery swapping via BatteryCharger.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/Gameplay/Tools/Manta Scooter")]
    public sealed class MantaScooter : PlayerTool, IBatteryTool, ITickable, IUpdatable, ILateFrameTickable, IPlayerTransportSource, IPlayerTransportLifecycleOwner, IDamageSignalEmitter, ILocalizationLanguageChangedListener, IGlobalRegistryHotSwapListener
    {
        private static int s_x001DirectSignalPushDropCount_MantaScooter;

        private const float DefaultTransportPropulsionReference = 800f;
        private const float ThrottleBlendSpeedFloor = 0.01f;
        private const float ThrottleBlendDenominatorFloor = 0.0001f;
        private const float ThrottleExponentEpsilon = 0.001f;
        private const float PadeOneTwelfth = 0.0833333333f;
        private const float DegreesToRadians = 0.017453292519943295f;
        private const float MaxSpotConeRadians = 1.56206965f;
        private const float CosFourthCoefficient = 0.0416666679f;
        private const float HeadlightNoiseCellsPerSecond = 64f;
        private const float HeadlightSignalMinIntensity = 0.0001f;
        private const uint HeadlightSignalSourceSalt = 0x4D484C54u;
        private const uint MantaCameraImpactSourceHash = 0x4D4E5441u;
        private const float MantaCameraImpactRadiusMeters = 12f;
        private const float MantaCameraImpactAmplitudeScale = 0.78f;
        private const float MantaCameraImpactTranslationGain = 0.72f;
        private const float MantaCameraImpactRotationGain = 0.55f;

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  INSPECTOR
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        [Header("â”€â”€ Propulsion â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€")]
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

        [Header("â”€â”€ Visuals â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€")]
        [Tooltip("Mesh to hide when battery is removed.")]
        [SerializeField] private GameObject batteryMesh;

        [Tooltip("Optional pooled world-body prefab used when a handheld Manta catastrophically bails out at speed. Falls back to ToolData.worldPrefab when unset.")]
        [SerializeField] private GameObject bailoutWreckPrefab;

        [Header("â”€â”€ Headlights â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€")]
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

        [Header("â”€â”€ HUD Display â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€")]
        [Tooltip("Canvas group for the HUD display.")]
        [SerializeField] private CanvasGroup hudCanvasGroup;

        [Tooltip("Text component for depth display.")]
        [SerializeField] private TMPro.TMP_Text depthText;

        [Tooltip("Text component for battery display.")]
        [SerializeField] private TMPro.TMP_Text batteryText;
        private char[] _depthHudBuffer;
        private char[] _batteryHudBuffer;
        private FixedCharBuffer _toolWarningBuffer = new FixedCharBuffer(128); // COLD ALLOC: char[128] - manta warning HUD staging buffer - owner: MantaScooter

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  IBatteryTool STATE
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        private ItemData _batteryItem;
        private float _currentCharge;
        private bool _hasBattery;

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  RUNTIME STATE
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        private VehicleUpgradeModule _vehicleUpgradeModule;
        private IInputService _cachedInputService;
        private IObjectPoolService _cachedObjectPool;
        private IToolAcousticCueService _cachedToolAcousticCues;
        private IBabelLocalization _cachedBabelLocalization;
        private Transform _cachedTransform;
        private bool _vehicleUpgradeModuleLookupAttempted;
        private bool _seaglideMovementStateCacheResolved;
        private bool _seaglideMovementStateCacheValid;
        private bool _hasLastSeaglideMovementSnapshotAup;
        private bool _headlightPresentationDirty;
        private bool _headlightClearGlobalsDirty;
        private bool _hudPresentationDirty;
        private bool _registeredLateFrame;
        private bool _unregisterLateFrameAfterHeadlightClear;
        private bool _dispatcherAvailable;
        private bool _headlightDefaultsRestoreDirty;
        private bool _isActive;
        private bool _isMoving;
        private float _driveThrottleCurrent;
        private float _headlightPresentationDeltaTime;
        private bool _registeredTick;
        private bool _hotSwapListenerRegistered;
        private bool _hudStateInitialized;
        private bool _lastHudVisible;
        private int _lastDepthTenths = int.MinValue;
        private int _lastBatteryPercent = int.MinValue;
        private FixedCharBuffer _localizedNoBatteryWarningBuffer = new FixedCharBuffer(96);
        private FixedCharBuffer _localizedBatteryDepletedWarningBuffer = new FixedCharBuffer(96);
        private FixedCharBuffer _localizedSummaryNoBatteryBuffer = new FixedCharBuffer(96);
        private FixedCharBuffer _localizedSummaryActiveFormatBuffer = new FixedCharBuffer(96);
        private FixedCharBuffer _localizedSummaryStandbyFormatBuffer = new FixedCharBuffer(96);
        private FixedCharBuffer _localizedDirectiveInsertBatteryBuffer = new FixedCharBuffer(160);
        private FixedCharBuffer _localizedDirectiveSwapRechargeBuffer = new FixedCharBuffer(160);
        private FixedCharBuffer _localizedDirectiveHoldForwardBuffer = new FixedCharBuffer(160);
        private FixedCharBuffer _localizedDirectiveHoldPrimaryBuffer = new FixedCharBuffer(160);
        private FixedCharBuffer _localizedTransportBrokenWarningBuffer = new FixedCharBuffer(96);
        private FixedCharBuffer _legacyMantaSummaryBuffer = new FixedCharBuffer(128);
        private FixedCharBuffer _legacyMantaDirectiveBuffer = new FixedCharBuffer(160);
        private byte _powerIndicatorVisualState = byte.MaxValue;
        private uint _headlightPayloadHash = uint.MaxValue;
        private int _lastPublishedHeadlightPayloadCount = -1;
        private uint _headlightSignalDropCount;
        private ushort _lastHeadlightSignalDropSlot;
        private byte _lastHeadlightSignalDropOperation;
        private double3 _lastSeaglideAup;
        private double3 _lastSeaglideMovementSnapshotAup;
        private PlayerMovementRuntimeState _cachedSeaglideMovementState;
        [SerializeField] private string _debugActivationState = ActivationStateIdle;

        private const string ActivationStateIdle = "Idle";
        private const string ActivationStateSpawned = "Spawned";
        private const string ActivationStateEquipped = "Equipped";
        private const string ActivationStateUnequipped = "Unequipped";
        private const string ActivationStateNotEquipped = "NotEquipped";
        private const string ActivationStateNoBattery = "NoBattery";
        private const string ActivationStateBatteryTooLow = "BatteryTooLow";
        private const string ActivationStateMissingPlayerMovement = "MissingPlayerMovement";
        private const string ActivationStateMissingRuntimeContext = "MissingRuntimeContext";
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
        private bool _hasLastSeaglideAup;
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
        private byte _publishedHeadlightSignalMask;
        private float _headlightGlitchPhase;
        private Vector3 _lastPublishedVolumetricVelocity;
        private bool _hasLastPublishedVolumetricVelocity;
        // COLD ALLOC: IDamageSignalReceiver[4] - handheld transport damage listeners (player trauma dispatcher) - owner: MantaScooter
        private readonly IDamageSignalReceiver[] _damageReceivers = new IDamageSignalReceiver[DamageReceiverCapacity];
        private int _damageReceiverCount;

        private const int DamageReceiverCapacity = 4;
        private const int MaxHeadlights = 2;
        private static readonly int _HeadlightCountId = Shader.PropertyToID("_HectonScooterHeadlightCount");
        private static readonly int _HeadlightPositionsWsId = Shader.PropertyToID("_HectonScooterHeadlightPositionsWS");
        private static readonly int _HeadlightDirectionsWsId = Shader.PropertyToID("_HectonScooterHeadlightDirectionsWS");
        private static readonly int _HeadlightColorsId = Shader.PropertyToID("_HectonScooterHeadlightColors");
        private static readonly int _HeadlightConeDataId = Shader.PropertyToID("_HectonScooterHeadlightConeData");
        private static readonly int _ScooterVelocityWsId = Shader.PropertyToID("_HectonScooterVelocityWS");
        private static readonly int _ScooterBrakeCloudId = Shader.PropertyToID("_HectonScooterBrakeCloud");

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  IBatteryTool IMPLEMENTATION
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        /// <summary>True if the tool currently has a battery installed.</summary>
        public bool HasBattery => _hasBattery;

        /// <summary>Current battery charge level (0-1). Returns 0 if no battery.</summary>
        public float BatteryCharge => _hasBattery ? GetRuntimeBatteryNormalized(_currentCharge) : 0f;

        /// <summary>The battery item currently installed (null if none).</summary>
        public ItemData BatteryItem => _batteryItem;

        /// <summary>Latest deterministic activation state for runtime verification.</summary>
        public string DebugActivationState => _debugActivationState;

        /// <summary>True while Manta propulsion is actively engaged.</summary>
        public bool IsTransportActive => !_isTransportBroken && _isActive && _hasBattery && BatteryCharge >= minChargeToActivate;

        /// <summary>True when this Manta can currently accept station charge.</summary>
        public bool CanReceiveTransportCharge => _hasBattery && !_isActive && BatteryCharge < 0.999f;

        /// <summary>True when this Manta has failed structurally.</summary>
        public bool IsTransportBroken => _isTransportBroken;

        /// <summary>Current normalized battery charge treated as transport charge.</summary>
        public float TransportChargeNormalized => BatteryCharge;

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
            SetRuntimeBatteryNormalized(0f);

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
            SetRuntimeBatteryNormalized(_currentCharge);

            UpdateBatteryVisuals();
            UpdatePowerIndicator();

            return true;
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  LIFECYCLE
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        private void Awake()
        {
            _cachedTransform = transform;
            CacheVehicleUpgradeModuleCold();
            _headlightSlots = new Light[MaxHeadlights]; // COLD ALLOC: Light[2] Ã¢â‚¬â€ scooter headlight cache for volumetric shafts Ã¢â‚¬â€ owner: MantaScooter
            _headlightPositionsWs = new Vector4[MaxHeadlights]; // COLD ALLOC: Vector4[2] Ã¢â‚¬â€ scooter headlight world-position payloads Ã¢â‚¬â€ owner: MantaScooter
            _headlightDirectionsWs = new Vector4[MaxHeadlights]; // COLD ALLOC: Vector4[2] Ã¢â‚¬â€ scooter headlight direction payloads Ã¢â‚¬â€ owner: MantaScooter
            _headlightColors = new Vector4[MaxHeadlights]; // COLD ALLOC: Vector4[2] Ã¢â‚¬â€ scooter headlight spectral payloads Ã¢â‚¬â€ owner: MantaScooter
            _headlightConeData = new Vector4[MaxHeadlights]; // COLD ALLOC: Vector4[2] Ã¢â‚¬â€ scooter headlight cone payloads Ã¢â‚¬â€ owner: MantaScooter
            _headlightBaseColors = new Color[MaxHeadlights]; // COLD ALLOC: Color[2] Ã¢â‚¬â€ authored headlight colors for recovery after hull-stress glitches Ã¢â‚¬â€ owner: MantaScooter
            _headlightBaseSpotAngles = new float[MaxHeadlights]; // COLD ALLOC: float[2] Ã¢â‚¬â€ authored headlight cone cache for recovery after hull-stress glitches Ã¢â‚¬â€ owner: MantaScooter
            _headlightBaseIntensities = new float[MaxHeadlights]; // COLD ALLOC: float[2] Ã¢â‚¬â€ authored headlight intensity cache for recovery after hull-stress glitches Ã¢â‚¬â€ owner: MantaScooter
            _headlightBaseRanges = new float[MaxHeadlights]; // COLD ALLOC: float[2] Ã¢â‚¬â€ authored headlight range cache for shaft falloff publishing Ã¢â‚¬â€ owner: MantaScooter
            _depthHudBuffer = new char[16]; // COLD ALLOC: char[16] - scooter depth HUD buffer - owner: MantaScooter
            _batteryHudBuffer = new char[8]; // COLD ALLOC: char[8] - scooter battery HUD buffer - owner: MantaScooter
            RefreshMantaLocalizationCache();
            BindTransportPresetToFeelContract();
            EnsureTransportLifecycleInitialized();
            CacheHeadlightDefaults();
            RegisterHeadlightShadowBudget();
            ClearHeadlightGlobalsImmediate();

        }

        private void OnEnable()
        {
            RefreshCachedRegistryServices();
            _dispatcherAvailable = GlobalRegistry.Dispatcher != null;
            TryRegisterHotSwapListener();
            LocalizationEvents.RegisterLanguageListener(this);
            RefreshMantaLocalizationCache();
            RegisterHeadlightShadowBudget();
            ConfigureMantaSignalLanesCold();
            CameraJuiceSignals.EnsurePrewarmed();
            EnsureTransportLifecycleInitialized();
            PlayerTransportLifecycleRegistry.Register(this, this);
        }

        private void OnDisable()
        {
            PlayerTransportLifecycleRegistry.Unregister(this, this);
            LocalizationEvents.UnregisterLanguageListener(this);
            TryUnregisterHotSwapListener();
            UnregisterFromTick();
            UnregisterFromLateFrame();
            UnregisterHeadlightShadowBudget();
            RestoreHeadlightDefaults();
            ClearHeadlightGlobalsImmediate();
        }

        public override void OnSpawn()
        {
            base.OnSpawn();
            _vehicleUpgradeModuleLookupAttempted = false;
            CacheVehicleUpgradeModuleCold();
            _isActive = false;
            _registeredTick = false;
            _debugActivationState = ActivationStateSpawned;
            ResetMisfireState();
            _empMisfireTimer = 0f;
            BindTransportPresetToFeelContract();
            EnsureTransportLifecycleInitialized();
            PlayerTransportLifecycleRegistry.Register(this, this);
            ResetHudStateCache();
            CacheHeadlightDefaults();
            RegisterHeadlightShadowBudget();
            ClearHeadlightGlobals();
            _unregisterLateFrameAfterHeadlightClear = true;
            UpdateBatteryVisuals();
            UpdatePowerIndicator();
        }

        public override void OnDespawn()
        {
            PlayerTransportLifecycleRegistry.Unregister(this, this);
            SyncMantaChargeMirrorFromCentral();
            DeactivateScooter();
            RestoreHeadlightDefaults();
            ClearHeadlightGlobals();
            _unregisterLateFrameAfterHeadlightClear = true;
            UnregisterHeadlightShadowBudget();
            UnregisterFromTick();
            ResetHudStateCache();
            ClearDamageReceivers();
            base.OnDespawn();
        }

        public override void OnEquip()
        {
            base.OnEquip();
            BindTransportPresetToFeelContract();
            _debugActivationState = ActivationStateEquipped;
            ResetHudStateCache();
            RegisterToTick();
            RegisterToLateFrame();
            UpdateBatteryVisuals();
            UpdatePowerIndicator();
        }

        public override void OnUnequip()
        {
            SyncMantaChargeMirrorFromCentral();
            DeactivateScooter();
            RestoreHeadlightDefaults();
            ClearHeadlightGlobals();
            UnregisterFromTick();
            _unregisterLateFrameAfterHeadlightClear = true;
            _debugActivationState = ActivationStateUnequipped;
            ResetHudStateCache();
            base.OnUnequip();
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  TOOL ACTIONS
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        public override void UsePrimary(float deltaTime)
        {
            ResetSeaglideMovementStateCache();

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
                PublishToolWarning(_localizedTransportBrokenWarningBuffer.AsSpan());
                return;
            }

            // Check battery
            if (!_hasBattery)
            {
                if (_isActive)
                    DeactivateScooter();

                _debugActivationState = ActivationStateNoBattery;
                PublishToolWarning(_localizedNoBatteryWarningBuffer.AsSpan());
                return;
            }

            float currentCharge = BatteryCharge;
            if (currentCharge < minChargeToActivate)
            {
                if (_isActive)
                    DeactivateScooter();

                _debugActivationState = ActivationStateBatteryTooLow;
                PublishToolWarning(_localizedNoBatteryWarningBuffer.AsSpan());
                return;
            }

            RefreshSeaglideMovementStateSnapshot(deltaTime);
            if (!TryResolveSeaglideMovementState(out PlayerMovementRuntimeState movementState))
            {
                if (_isActive)
                    DeactivateScooter();

                _debugActivationState = ActivationStateMissingRuntimeContext;
                return;
            }

            if (!IsMovementStateUnderwater(in movementState))
            {
                if (_isActive)
                    DeactivateScooter();

                _debugActivationState = ActivationStateNotUnderwater;
                return;
            }

            // Activate if not already active
            if (!_isActive)
                ActivateScooter();

            _driveThrottleCurrent = AdvanceDriveThrottle(_driveThrottleCurrent, 1f, deltaTime);
            float driveThrottleOutput = ResolveEffectiveDriveThrottleOutput();
            _isMoving = IsPlayerMoving() || driveThrottleOutput > 0.01f;
            UpdateHullStressMisfire(deltaTime, _isMoving);
            _debugActivationState = _isMoving ? ActivationStateMoving : ActivationStateIdleInWater;

            if (_isMoving)
            {
                TrySubmitHydrodynamicRequest(deltaTime, driveThrottleOutput, currentCharge);
                MarkScooterActiveForCentralSolver(true, driveThrottleOutput);
                UpdatePowerIndicator();
                QueueHudPresentation();

                if (BatteryCharge <= 0f)
                {
                    DeactivateScooter();
                    _debugActivationState = ActivationStateBatteryDepleted;
                    PublishToolWarning(_localizedBatteryDepletedWarningBuffer.AsSpan());
                }
            }
            else
            {
                MarkScooterActiveForCentralSolver(false, 0f);
            }
        }

        public override void UseSecondary(float deltaTime)
        {
            // Secondary does nothing for scooter - could be used for headlight toggle
        }

        internal override float ResolveModularBatteryNormalized()
        {
            return _hasBattery ? BatteryCharge : 0f;
        }

        protected override void ConfigureModularRuntimeProfile(ref ToolRuntimeProfile profile)
        {
            profile.BatteryDrainPerSecond = ResolveEffectiveBatteryDrainRate();
        }

        private void MarkScooterActiveForCentralSolver(bool active, float driveThrottleOutput)
        {
            if (!TryGetModularEquipment(out IModularEquipmentService service) || RuntimeToolId == 0u)
                return;

            float requestedDrainRate = active
                ? ResolveEffectiveBatteryDrainRate() * math.saturate(driveThrottleOutput)
                : 0f;
            service.SetToolActive(RuntimeToolId, active, requestedDrainRate);
        }

        private void SyncMantaChargeMirrorFromCentral()
        {
            _currentCharge = _hasBattery ? BatteryCharge : 0f;
        }

        public override void ToolTick(float deltaTime)
        {
            // Called by PlayerToolManager - we use ITickable for HUD updates
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  ITickable
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        public void Tick(float deltaTime)
        {
            ResetSeaglideMovementStateCache();

            if (!IsEquipped)
                return;

            RefreshSeaglideMovementStateSnapshot(deltaTime);

            if (_empMisfireTimer > 0f)
            {
                _empMisfireTimer -= deltaTime;
                if (_empMisfireTimer < 0f)
                    _empMisfireTimer = 0f;
            }

            QueueHeadlightPresentation(deltaTime);

            if (_isActive)
            {
                IInputService inputService = _cachedInputService;
                PlayerInputState inputState = inputService != null && inputService.IsPlayerInputEnabled
                    ? inputService.GetState()
                    : default;
                if (!inputState.HasAction(PlayerInputAction.PrimaryFire))
                    UpdateHullStressMisfire(deltaTime, false);

                TickDriveRelease(deltaTime);
            }

            QueueHudPresentation();
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  PUBLIC API
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        public void LateFrameTick()
        {
            if (_headlightDefaultsRestoreDirty)
            {
                _headlightDefaultsRestoreDirty = false;
                RestoreHeadlightDefaults();
            }

            if (_headlightClearGlobalsDirty)
            {
                _headlightClearGlobalsDirty = false;
                ClearHeadlightGlobalsImmediate();
            }

            if (_headlightPresentationDirty)
            {
                float deltaTime = math.clamp(_headlightPresentationDeltaTime, 0.0001f, 0.2f);
                _headlightPresentationDirty = false;
                _headlightPresentationDeltaTime = 0f;
                UpdateHeadlightState(deltaTime);
            }

            if (_hudPresentationDirty)
            {
                _hudPresentationDirty = false;
                UpdateHUD();
            }

            if (_unregisterLateFrameAfterHeadlightClear &&
                !_headlightClearGlobalsDirty &&
                !_headlightPresentationDirty &&
                !_hudPresentationDirty)
            {
                _unregisterLateFrameAfterHeadlightClear = false;
                UnregisterFromLateFrame();
            }
        }

        public override string BuildLegacyOperationalSummaryString()
        {
            _legacyMantaSummaryBuffer.Clear();
            WriteOperationalSummary(ref _legacyMantaSummaryBuffer);
            return _hasBattery
                ? (_isActive ? "MANTA // ACTIVE" : "MANTA // STANDBY")
                : "MANTA // NO BATTERY";
        }

        public override void WriteOperationalSummary(ref FixedCharBuffer buffer)
        {
            if (!_hasBattery)
            {
                AppendText(ref buffer, _localizedSummaryNoBatteryBuffer.AsSpan());
                return;
            }

            int batteryPercent = math.clamp((int)math.round(BatteryCharge * 100f), 0, 100);
            AppendPercentTemplate(
                ref buffer,
                _isActive ? _localizedSummaryActiveFormatBuffer.AsSpan() : _localizedSummaryStandbyFormatBuffer.AsSpan(),
                batteryPercent);
        }

        public override string BuildLegacyOperationalDirectiveString()
        {
            _legacyMantaDirectiveBuffer.Clear();
            WriteOperationalDirective(ref _legacyMantaDirectiveBuffer);
            if (!_hasBattery)
                return "Insert a battery to activate propulsion.";

            bool batteryLow = BatteryCharge < minChargeToActivate;
            if (batteryLow)
                return "Battery depleted. Swap or recharge.";

            return _isActive
                ? "Hold forward to propel. Release to coast."
                : "Hold primary to activate propulsion while swimming.";
        }

        public override void WriteOperationalDirective(ref FixedCharBuffer buffer)
        {
            bool batteryLow = _hasBattery && BatteryCharge < minChargeToActivate;
            AppendText(
                ref buffer,
                !_hasBattery
                    ? _localizedDirectiveInsertBatteryBuffer.AsSpan()
                    : batteryLow
                        ? _localizedDirectiveSwapRechargeBuffer.AsSpan()
                        : _isActive
                            ? _localizedDirectiveHoldForwardBuffer.AsSpan()
                            : _localizedDirectiveHoldPrimaryBuffer.AsSpan());
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  PRIVATE â€” ACTIVATION
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        private void ActivateScooter()
        {
            _isActive = true;
            _debugActivationState = ActivationStateActivated;
            UpdatePowerIndicator();
        }

        private void DeactivateScooter()
        {
            _isActive = false;
            _isMoving = false;
            _driveThrottleCurrent = 0f;
            _hasLastSeaglideAup = false;
            _hasLastSeaglideMovementSnapshotAup = false;
            MarkScooterActiveForCentralSolver(false, 0f);
            _debugActivationState = ActivationStateIdle;
            ResetMisfireState();
            QueueHeadlightDefaultsRestore();
            ClearHeadlightGlobals();
            UpdatePowerIndicator();
        }

        private bool IsPlayerMoving()
        {
            if (!TryResolveSeaglideMovementState(out PlayerMovementRuntimeState movementState))
            {
                _debugActivationState = ActivationStateMissingRuntimeContext;
                return false;
            }

            return math.lengthsq(movementState.Velocity) > 0.25f;
        }

        private bool TrySubmitHydrodynamicRequest(float deltaTime, float driveThrottleOutput, float batteryCharge)
        {
            if (!TryResolveSeaglideMovementState(out PlayerMovementRuntimeState movementState))
                return false;

            if (!math.isfinite(deltaTime) || deltaTime <= 0f)
                return false;

            float safeDelta = math.clamp(deltaTime, 0.0001f, 0.2f);
            double3 currentAup = movementState.PredictedAup.ToAbsoluteDouble3();
            if (!math.all(math.isfinite(currentAup)))
                return false;

            double3 previousAup = _hasLastSeaglideAup
                ? _lastSeaglideAup
                : RewindAupByLocalVelocity(currentAup, movementState.Velocity, safeDelta);

            float3 forward = SafeDirection(movementState.CameraForward, SafeDirection(movementState.Forward, new float3(0f, 0f, 1f)));
            if (!math.all(math.isfinite(previousAup)) ||
                !math.all(math.isfinite(forward)) ||
                !math.all(math.isfinite(movementState.Velocity)))
            {
                return false;
            }

            SeaglidePropulsionRequestDTO request = default;
            request.CurrentAUP = currentAup;
            request.PreviousAUP = previousAup;
            request.InputVector = forward;
            request.ForwardVector = SafeDirection(movementState.Forward, forward);
            request.Throttle01 = math.saturate(driveThrottleOutput);
            request.DeltaTime = safeDelta;
            request.TargetEntityHash = SeaglideHydrodynamicsConstants.PlayerBodyTargetHash;
            request.RequestHash = RuntimeToolId != 0u ? RuntimeToolId : SeaglideHydrodynamicsConstants.SourceHash;
            request.Flags = SeaglideHydrodynamicsConstants.FlagActive | SeaglideHydrodynamicsConstants.FlagPlayerControlled;
            request.BatteryLevel = math.saturate(batteryCharge);
            request.SurfaceNormal = new float3(0f, 1f, 0f);

            SeaglidePropulsionRequestSignal signal = default;
            signal.Request = request;
            signal.Velocity = movementState.Velocity;
            signal.BatteryLevel = request.BatteryLevel;
            signal.MassKg = SeaglideHydrodynamicsConstants.DefaultBaseMassKg;
            signal.AddedMassKg = SeaglideHydrodynamicsConstants.DefaultAddedMassKg;
            signal.TargetEntityHash = request.TargetEntityHash;
            signal.FrameIndex = request.FrameIndex;
            signal.Flags = request.Flags;

            bool submitted = SignalBus<SeaglidePropulsionRequestSignal>.TryPushTracked(in signal, ref s_x001DirectSignalPushDropCount_MantaScooter);
            if (submitted)
            {
                _lastSeaglideAup = currentAup;
                _hasLastSeaglideAup = true;
            }

            return submitted;
        }

        private void ResetSeaglideMovementStateCache()
        {
            _seaglideMovementStateCacheResolved = false;
            _seaglideMovementStateCacheValid = false;
            _cachedSeaglideMovementState = default;
        }

        private void RefreshSeaglideMovementStateSnapshot(float deltaTime)
        {
            _seaglideMovementStateCacheResolved = true;
            _seaglideMovementStateCacheValid = false;
            _cachedSeaglideMovementState = default;
            if (!TryGetPlayerRuntimeContext(out IPlayerRuntimeContext playerContext) ||
                !playerContext.IsInitialized ||
                !playerContext.TryGetMovementRuntimeState(out PlayerMovementRuntimeState publishedState) ||
                (publishedState.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) == 0u ||
                !math.isfinite(publishedState.DepthMeters))
            {
                _hasLastSeaglideMovementSnapshotAup = false;
                return;
            }

            Hecton8.World.AbsoluteUniversePosition predictedPosition = publishedState.PredictedAup;
            if (!predictedPosition.IsFinite())
            {
                _hasLastSeaglideMovementSnapshotAup = false;
                return;
            }

            double3 currentAup = predictedPosition.ToAbsoluteDouble3();
            if (!math.all(math.isfinite(currentAup)))
            {
                _hasLastSeaglideMovementSnapshotAup = false;
                return;
            }

            float safeDelta = math.clamp(
                math.select(0.0001f, deltaTime, math.isfinite(deltaTime) && deltaTime > 0f),
                0.0001f,
                0.2f);
            float3 aupVelocity = ResolveSeaglideAupVelocity(currentAup, safeDelta);
            float3 publishedVelocity = publishedState.Velocity;
            bool publishedVelocityValid = math.all(math.isfinite(publishedVelocity));
            bool shouldUseAupVelocity = !publishedVelocityValid ||
                (math.lengthsq(publishedVelocity) <= 0.000001f && math.lengthsq(aupVelocity) > 0.000001f);
            float3 velocity = shouldUseAupVelocity
                ? aupVelocity
                : publishedVelocity;
            _lastSeaglideMovementSnapshotAup = currentAup;
            _hasLastSeaglideMovementSnapshotAup = true;

            float3 fallbackForward = SafeDirection(publishedState.Forward, new float3(0f, 0f, 1f));
            float3 cameraForward = SafeDirection(publishedState.CameraForward, fallbackForward);

            float3 currentRuntime = publishedState.WorldPosition;
            if (!math.all(math.isfinite(currentRuntime)))
                currentRuntime = predictedPosition.ToRuntimeFloat3();
            if (!math.all(math.isfinite(currentRuntime)))
                currentRuntime = publishedState.PredictedWorldPosition;
            if (!math.all(math.isfinite(currentRuntime)))
                currentRuntime = float3.zero;

            float3 predictedRuntime = publishedState.PredictedWorldPosition;
            if (!math.all(math.isfinite(predictedRuntime)))
                predictedRuntime = currentRuntime + (velocity * 0.1f);

            PlayerMovementRuntimeState movementState = publishedState;
            movementState.WorldPosition = currentRuntime;
            movementState.PredictedWorldPosition = predictedRuntime;
            movementState.PredictedAup = predictedPosition;
            movementState.Velocity = math.all(math.isfinite(velocity)) ? velocity : float3.zero;
            movementState.Forward = fallbackForward;
            movementState.CameraForward = cameraForward;
            movementState.DepthMeters = math.max(0f, publishedState.DepthMeters);
            movementState.TransportSpeedMultiplier = math.max(0.01f, GetSpeedMultiplier());
            movementState.UnderwaterStressIntensity01 = math.saturate(publishedState.UnderwaterStressIntensity01);

            _cachedSeaglideMovementState = movementState;
            _seaglideMovementStateCacheValid = true;
        }

        private bool TryResolveSeaglideMovementState(out PlayerMovementRuntimeState movementState)
        {
            movementState = _cachedSeaglideMovementState;
            return _seaglideMovementStateCacheResolved && _seaglideMovementStateCacheValid;
        }

        private static bool IsMovementStateUnderwater(in PlayerMovementRuntimeState movementState)
        {
            return (movementState.Flags & (uint)PlayerRuntimeSnapshotFlags.Underwater) != 0u;
        }

        private Vector3 ResolveSeaglidePresentationVelocity(bool allowHeadlights)
        {
            if (!allowHeadlights || !TryResolveSeaglideMovementState(out PlayerMovementRuntimeState movementState))
                return Vector3.zero;

            return new Vector3(movementState.Velocity.x, movementState.Velocity.y, movementState.Velocity.z);
        }

        private static float3 SafeDirection(float3 value, float3 fallback)
        {
            float sq = math.lengthsq(value);
            return math.select(fallback, value * math.rsqrt(math.max(sq, 0.000001f)), math.isfinite(sq) && sq > 0.000001f);
        }

        private float3 ResolveSeaglideAupVelocity(double3 currentAup, float deltaTime)
        {
            if (!_hasLastSeaglideMovementSnapshotAup || !math.all(math.isfinite(_lastSeaglideMovementSnapshotAup)))
                return float3.zero;

            double3 deltaAup = AupPrecisionMath.LocalDeltaDouble(currentAup, _lastSeaglideMovementSnapshotAup);
            float3 localDelta = AupPrecisionMath.DowncastLocalDelta(deltaAup, float3.zero);
            float3 velocity = localDelta * math.rcp(math.max(deltaTime, 0.0001f));
            return math.all(math.isfinite(velocity)) ? velocity : float3.zero;
        }

        private static double3 RewindAupByLocalVelocity(double3 currentAup, float3 velocity, float deltaTime)
        {
            if (!math.all(math.isfinite(currentAup)) || !math.all(math.isfinite(velocity)) || !math.isfinite(deltaTime))
                return currentAup;

            double safeDelta = math.max((double)deltaTime, 0.0001d);
            double3 displacementMeters = new double3(
                (double)velocity.x * safeDelta,
                (double)velocity.y * safeDelta,
                (double)velocity.z * safeDelta);
            // Rewind in a local AUP frame, then rehydrate, so the active path does not subtract from absolute coordinates directly.
            double3 localOrigin = currentAup;
            double3 currentLocal = AupPrecisionMath.LocalDeltaDouble(currentAup, localOrigin);
            double3 previousLocal = currentLocal - displacementMeters;
            double3 previous = localOrigin + previousLocal;
            return math.all(math.isfinite(previous)) ? previous : currentAup;
        }

        /// <summary>
        /// Gets the swim speed multiplier to apply when scooter is active.
        /// Consumed by the player transport coordinator when the scooter is active.
        /// </summary>
        public float GetSpeedMultiplier()
        {
            float currentCharge = BatteryCharge;
            if (_isTransportBroken || !_isActive || !_hasBattery || currentCharge < minChargeToActivate)
                return 1f;

            return math.lerp(1f, ResolveConfiguredTransportSpeedMultiplier(), ResolveEffectiveDriveThrottleOutput());
        }

        /// <summary>
        /// Legacy transport force path is disabled. SHINOBU_227 emits AUP propulsion requests through typed SignalBus.
        /// </summary>
        public float GetPropulsionForce()
        {
            return 0f;
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
            if (_isTransportBroken || !_isActive || !_hasBattery || BatteryCharge < minChargeToActivate)
                return 1f;

            return math.lerp(1f, ResolveConfiguredTransportDragCoefficientMultiplier(), ResolveEffectiveDriveThrottleOutput());
        }

        /// <summary>
        /// Resolves normalized transport boost for generic transport consumers.
        /// </summary>
        public float GetTransportBoost01()
        {
            float propulsionReference = ResolveTransportPropulsionReference();
            if (_isTransportBroken || !_isActive || !_hasBattery || BatteryCharge < minChargeToActivate)
                return 0f;

            float authoredForce = ResolveConfiguredTransportPropulsionForce() * ResolveEffectiveDriveThrottleOutput() * BatteryCharge;
            return math.saturate(authoredForce / propulsionReference);
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

            if (!TryResolveCachedObjectPool(out IObjectPoolService poolManager))
                return false;

            Transform spawnTransform = _cachedTransform != null ? _cachedTransform : transform;
            GameObject wreckInstance = poolManager.Spawn(wreckPrefab, spawnTransform.position, spawnTransform.rotation);
            if (wreckInstance == null)
                return false;

            if (!wreckInstance.TryGetComponent(out MantaEmergencyWreck wreck))
            {
                poolManager.Despawn(wreckInstance);
                return false;
            }

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

            _currentCharge = math.saturate(BatteryCharge + normalizedChargeDelta * ResolveStationChargeRateScale());
            SetRuntimeBatteryNormalized(_currentCharge);
            UpdatePowerIndicator();
            QueueHudPresentation();
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
            HabitatDamageSignal damageSignal = BuildDamageSignal(impactSpeed, hitPoint, (uint)DamageTypeMask.Impact, previousIntegrityNormalized, nextIntegrityNormalized);
            PublishMantaCameraImpact(damageT, impactSpeed, hitPoint, hitNormal);
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

            for (int i = 0; i < _damageReceiverCount; i++)
            {
                if (ReferenceEquals(_damageReceivers[i], receiver))
                    return;
            }

            if (_damageReceiverCount >= DamageReceiverCapacity)
                return;

            _damageReceivers[_damageReceiverCount] = receiver;
            _damageReceiverCount++;
        }

        /// <summary>
        /// Unregisters a previously registered scooter damage receiver.
        /// </summary>
        public void UnregisterDamageReceiver(IDamageSignalReceiver receiver)
        {
            if (receiver == null)
                return;

            for (int i = _damageReceiverCount - 1; i >= 0; i--)
            {
                if (ReferenceEquals(_damageReceivers[i], receiver))
                {
                    int lastIndex = _damageReceiverCount - 1;
                    _damageReceivers[i] = _damageReceivers[lastIndex];
                    _damageReceivers[lastIndex] = null;
                    _damageReceiverCount = lastIndex;
                    return;
                }
            }
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  PRIVATE â€” VISUALS
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        private void ClearDamageReceivers()
        {
            for (int i = 0; i < _damageReceiverCount; i++)
                _damageReceivers[i] = null;

            _damageReceiverCount = 0;
        }

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

        private void BindTransportPresetToFeelContract()
        {
            PlayerTransportFeelContract transportFeelContract = TransportFeelContract;
            if (transportFeelContract != null && transportPreset != null)
                transportFeelContract.BindPreset(transportPreset);
        }

        private void TickDriveRelease(float deltaTime)
        {
            IInputService inputService = _cachedInputService;
            PlayerInputState inputState = inputService != null && inputService.IsPlayerInputEnabled
                ? inputService.GetState()
                : default;
            bool primaryHeld = inputState.HasAction(PlayerInputAction.PrimaryFire);
            if (primaryHeld)
                return;

            _driveThrottleCurrent = AdvanceDriveThrottle(_driveThrottleCurrent, 0f, deltaTime);
            if (_driveThrottleCurrent <= 0.0001f)
                DeactivateScooter();
            else
                MarkScooterActiveForCentralSolver(true, ResolveEffectiveDriveThrottleOutput());
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

        private void CacheVehicleUpgradeModuleCold()
        {
            if (_vehicleUpgradeModule != null || _vehicleUpgradeModuleLookupAttempted)
                return;

            _vehicleUpgradeModuleLookupAttempted = true;
            TryGetComponent(out _vehicleUpgradeModule);
        }

        private float ResolveEffectiveBatteryDrainRate()
        {
            float drainScale = _vehicleUpgradeModule != null
                ? math.max(0.1f, _vehicleUpgradeModule.ChargeDrainScale)
                : 1f;
            float abyssalOverstrainMultiplier = TryResolvePlayerMovementStressState(out PlayerMovementStressRuntimeState stressState)
                ? math.max(1f, stressState.AbyssalCounterDriveEnergyMultiplier)
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
            float maxIntegrity = ResolveMaxIntegrity();
            float currentIntegrity = _transportLifecycleInitialized ? _currentIntegrity : maxIntegrity;
            return math.saturate(currentIntegrity / math.max(1f, maxIntegrity));
        }

        private float ResolveMaxIntegrity()
        {
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
            if (!driveRequested)
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
            if (!TryResolvePlayerMovementStressState(out PlayerMovementStressRuntimeState stressState) ||
                stressState.HullStress01 <= threshold)
            {
                return 0f;
            }

            return InverseLerpSaturated(threshold, 1f, stressState.HullStress01);
        }

        private bool TryResolvePlayerMovementStressState(out PlayerMovementStressRuntimeState stressState)
        {
            if (TryGetPlayerRuntimeContext(out IPlayerRuntimeContext playerContext) &&
                playerContext.TryGetMovementStressRuntimeState(out stressState))
            {
                return true;
            }

            stressState = default;
            return false;
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

            IToolAcousticCueService acousticCues = _cachedToolAcousticCues;
            if (acousticCues != null)
                acousticCues.PlayMantaMisfire(stress01);
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

        private void DispatchIntegrityChanged(float prev, float next, HabitatDamageSignal signal)
        {
            int count = _damageReceiverCount;
            for (int i = 0; i < count; i++)
            {
                IDamageSignalReceiver receiver = _damageReceivers[i];
                if (receiver != null)
                    receiver.OnIntegrityChanged(prev, next, signal);
            }
        }

        private void DispatchPowerChanged(float prev, float next, HabitatDamageSignal signal)
        {
            int count = _damageReceiverCount;
            for (int i = 0; i < count; i++)
            {
                IDamageSignalReceiver receiver = _damageReceivers[i];
                if (receiver != null)
                    receiver.OnPowerChanged(prev, next, signal);
            }
        }

        private void DispatchClarityChanged(float prev, float next, HabitatDamageSignal signal)
        {
            int count = _damageReceiverCount;
            for (int i = 0; i < count; i++)
            {
                IDamageSignalReceiver receiver = _damageReceivers[i];
                if (receiver != null)
                    receiver.OnClarityChanged(prev, next, signal);
            }
        }

        private void DispatchTraumaThresholdCrossed(TraumaLevel level)
        {
            if (level == TraumaLevel.None)
                return;

            int count = _damageReceiverCount;
            for (int i = 0; i < count; i++)
            {
                IDamageSignalReceiver receiver = _damageReceivers[i];
                if (receiver != null)
                    receiver.OnTraumaThresholdCrossed(level);
            }
        }

        private HabitatDamageSignal BuildDamageSignal(
            float impactSpeed,
            Vector3 hitPoint,
            uint damageType,
            float previousIntegrityNormalized,
            float nextIntegrityNormalized)
        {
            HabitatDamageSignal signal = default;
            signal.magnitude = math.max(0f, impactSpeed);
            signal.localPoint = _cachedTransform != null
                ? (float3)_cachedTransform.InverseTransformPoint(hitPoint)
                : float3.zero;
            signal.damageType = damageType;
            signal.integrityDelta = (byte)math.clamp(
                RoundToIntPositive(math.abs(nextIntegrityNormalized - previousIntegrityNormalized) * byte.MaxValue),
                0,
                byte.MaxValue);
            signal.depth = ResolveCachedDepthMeters();
            signal.sourceID = DamageSourceIds.MantaScooter;
            return signal;
        }

        private static void PublishMantaCameraImpact(float damageT, float impactSpeed, Vector3 hitPoint, Vector3 hitNormal)
        {
            float severity = math.saturate(math.max(damageT, impactSpeed * 0.08f));
            if (severity <= 0.0001f || !IsFiniteVector(hitPoint))
                return;

            Vector3 direction = IsFiniteVector(hitNormal)
                ? -hitNormal
                : Vector3.zero;
            byte priority = severity >= 0.72f
                ? CameraJuiceSignals.HighPriority
                : CameraJuiceSignals.NormalPriority;
            CameraJuiceSignals.TryPublishImpact(
                severity,
                hitPoint,
                direction,
                CameraJuiceSignals.SharpKineticImpactProfileHash,
                MantaCameraImpactAmplitudeScale,
                priority,
                MantaCameraImpactRadiusMeters,
                MantaCameraImpactTranslationGain,
                MantaCameraImpactRotationGain,
                MantaCameraImpactSourceHash);
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
            PublishToolWarning(_localizedTransportBrokenWarningBuffer.AsSpan());
        }

        private void UpdatePowerIndicator()
        {
            byte nextState = ResolvePowerIndicatorState();
            if (_powerIndicatorVisualState == nextState)
                return;

            _powerIndicatorVisualState = nextState;
        }

        private byte ResolvePowerIndicatorState()
        {
            float currentCharge = BatteryCharge;

            if (!_hasBattery || currentCharge <= 0f)
                return 0;
            if (currentCharge <= 0.2f)
                return 1;
            return _isActive ? (byte)2 : (byte)3;
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

            bool allowHeadlights = IsTransportActive;
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
            uint payloadHash = ComputeHeadlightPayloadHash(activeCount);
            if (_headlightPayloadHash != payloadHash || _lastPublishedHeadlightPayloadCount != activeCount)
            {
                Shader.SetGlobalInt(_HeadlightCountId, activeCount);
                Shader.SetGlobalVectorArray(_HeadlightPositionsWsId, _headlightPositionsWs);
                Shader.SetGlobalVectorArray(_HeadlightDirectionsWsId, _headlightDirectionsWs);
                Shader.SetGlobalVectorArray(_HeadlightColorsId, _headlightColors);
                Shader.SetGlobalVectorArray(_HeadlightConeDataId, _headlightConeData);
                _headlightPayloadHash = payloadHash;
                _lastPublishedHeadlightPayloadCount = activeCount;
            }
            PublishHeadlightSignals(activeCount, allowHeadlights);
        }

        private void PublishVolumetricSiltGlobals(float deltaTime, bool allowHeadlights)
        {
            Vector3 velocity = ResolveSeaglidePresentationVelocity(allowHeadlights);
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

        private static bool IsFiniteVector(Vector3 value)
        {
            return float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z);
        }

        private static Vector3 SanitizeFiniteVector(Vector3 value)
        {
            return IsFiniteVector(value)
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

        private uint ComputeHeadlightPayloadHash(int activeCount)
        {
            uint hash = 2166136261u;
            hash = FoldHash(hash, (uint)math.clamp(activeCount, 0, MaxHeadlights));
            int count = math.clamp(activeCount, 0, MaxHeadlights);
            for (int payloadIndex = 0; payloadIndex < count; payloadIndex++)
            {
                hash = FoldVectorHash(hash, _headlightPositionsWs[payloadIndex]);
                hash = FoldVectorHash(hash, _headlightDirectionsWs[payloadIndex]);
                hash = FoldVectorHash(hash, _headlightColors[payloadIndex]);
                hash = FoldVectorHash(hash, _headlightConeData[payloadIndex]);
            }

            return hash != 0u ? hash : 1u;
        }

        private static uint FoldVectorHash(uint hash, Vector4 value)
        {
            hash = FoldHash(hash, math.asuint(value.x));
            hash = FoldHash(hash, math.asuint(value.y));
            hash = FoldHash(hash, math.asuint(value.z));
            return FoldHash(hash, math.asuint(value.w));
        }

        private static uint FoldHash(uint hash, uint value)
        {
            unchecked
            {
                hash ^= value;
                return hash * 16777619u;
            }
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
            _headlightClearGlobalsDirty = true;
            RegisterToLateFrame();
        }

        private void QueueHeadlightDefaultsRestore()
        {
            _headlightDefaultsRestoreDirty = true;
            RegisterToLateFrame();
        }

        private void ClearHeadlightGlobalsImmediate()
        {
            _headlightClearGlobalsDirty = false;
            if (_headlightPositionsWs == null ||
                _headlightDirectionsWs == null ||
                _headlightColors == null ||
                _headlightConeData == null)
            {
                PublishHeadlightClearSignals();
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
            _headlightPayloadHash = uint.MaxValue;
            _lastPublishedHeadlightPayloadCount = 0;
            Shader.SetGlobalVector(_ScooterVelocityWsId, Vector4.zero);
            Shader.SetGlobalFloat(_ScooterBrakeCloudId, 0f);
            _lastPublishedVolumetricVelocity = Vector3.zero;
            _hasLastPublishedVolumetricVelocity = false;
            PublishHeadlightClearSignals();
        }

        private void PublishHeadlightSignals(int activeCount, bool allowHeadlights)
        {
            if (_headlightPositionsWs == null ||
                _headlightDirectionsWs == null ||
                _headlightColors == null ||
                _headlightConeData == null)
            {
                PublishHeadlightClearSignals();
                return;
            }

            uint sourceId = ResolveHeadlightSignalSourceId();
            PlayerMovementRuntimeState movementState = default;
            bool hasMovementState = allowHeadlights && TryResolveSeaglideMovementState(out movementState);
            byte activeMask = 0;
            for (int payloadIndex = 0; payloadIndex < MaxHeadlights; payloadIndex++)
            {
                bool hasPayload = allowHeadlights && hasMovementState && payloadIndex < activeCount;
                if (!hasPayload)
                    continue;

                Vector4 position = _headlightPositionsWs[payloadIndex];
                Vector4 direction = _headlightDirectionsWs[payloadIndex];
                Vector4 color = _headlightColors[payloadIndex];
                Vector4 cone = _headlightConeData[payloadIndex];
                float intensity = math.max(0f, color.w * math.max(1f, cone.y));
                if (position.w <= 0.1f || intensity <= HeadlightSignalMinIntensity)
                    continue;

                byte payloadBit = (byte)(1 << payloadIndex);
                Vector3 positionWs = new Vector3(position.x, position.y, position.z);
                if (!TryResolveRuntimeAup(positionWs, in movementState, out Hecton8.World.AbsoluteUniversePosition positionAup))
                    continue;

                float3 forward = NormalizeHeadlightSignalDirection(new float3(direction.x, direction.y, direction.z));
                SubmarineLightsChangedSignal signal = default;
                signal.PositionAup = positionAup;
                signal.Forward = forward;
                signal.RangeMeters = math.max(0.1f, position.w);
                signal.Intensity = intensity;
                signal.SourceId = sourceId;
                signal.Slot = (ushort)payloadIndex;
                signal.Operation = SubmarineLightsChangedSignalOperations.Upsert;
                signal.Flags = SubmarineLightsChangedSignalFlags.Powered;
                signal.SpotOuterCos = math.clamp(cone.x, -1f, 1f);
                if (SignalBus<SubmarineLightsChangedSignal>.TryPushTracked(in signal, ref s_x001DirectSignalPushDropCount_MantaScooter))
                {
                    activeMask |= payloadBit;
                }
                else
                {
                    RecordHeadlightSignalDrop(payloadIndex, SubmarineLightsChangedSignalOperations.Upsert);
                    if ((_publishedHeadlightSignalMask & payloadBit) != 0)
                        activeMask |= payloadBit;
                }
            }

            PublishRetiredHeadlightSignals(sourceId, activeMask);
        }

        private static bool TryResolveRuntimeAup(
            Vector3 runtimePosition,
            in PlayerMovementRuntimeState movementState,
            out Hecton8.World.AbsoluteUniversePosition positionAup)
        {
            positionAup = default;
            float3 localRuntime = new float3(runtimePosition.x, runtimePosition.y, runtimePosition.z);
            if (!math.all(math.isfinite(localRuntime)))
                return false;

            Hecton8.World.AbsoluteUniversePosition referenceAup = movementState.PredictedAup;
            if (!referenceAup.IsFinite())
                return false;

            float3 referenceRuntime = movementState.PredictedWorldPosition;
            if (!math.all(math.isfinite(referenceRuntime)))
                return false;

            double3 referenceAbsolute = referenceAup.ToAbsoluteDouble3();
            float3 runtimeDelta = localRuntime - referenceRuntime;
            if (!math.all(math.isfinite(referenceAbsolute)) ||
                !math.all(math.isfinite(runtimeDelta)))
            {
                return false;
            }

            positionAup = Hecton8.World.AbsoluteUniversePosition.FromAbsolutePosition(
                referenceAbsolute + new double3(runtimeDelta.x, runtimeDelta.y, runtimeDelta.z));
            return positionAup.IsFinite();
        }

        private void PublishHeadlightClearSignals()
        {
            byte retiredMask = _publishedHeadlightSignalMask;
            if (retiredMask == 0)
                return;

            uint sourceId = ResolveHeadlightSignalSourceId();
            byte remainingMask = retiredMask;
            for (int payloadIndex = 0; payloadIndex < MaxHeadlights; payloadIndex++)
            {
                if ((retiredMask & (1 << payloadIndex)) == 0)
                    continue;

                if (PublishHeadlightRemoveSignal(sourceId, payloadIndex, SubmarineLightsChangedSignalFlags.BrownoutSuppressed))
                {
                    remainingMask = (byte)(remainingMask & ~(1 << payloadIndex));
                }
                else
                {
                    RecordHeadlightSignalDrop(payloadIndex, SubmarineLightsChangedSignalOperations.Remove);
                }
            }

            _publishedHeadlightSignalMask = remainingMask;
        }

        private void PublishRetiredHeadlightSignals(uint sourceId, byte activeMask)
        {
            byte retiredMask = (byte)(_publishedHeadlightSignalMask & ~activeMask);
            byte nextPublishedMask = activeMask;
            for (int payloadIndex = 0; payloadIndex < MaxHeadlights; payloadIndex++)
            {
                if ((retiredMask & (1 << payloadIndex)) == 0)
                    continue;

                if (!PublishHeadlightRemoveSignal(sourceId, payloadIndex, SubmarineLightsChangedSignalFlags.BrownoutSuppressed))
                {
                    nextPublishedMask = (byte)(nextPublishedMask | (1 << payloadIndex));
                    RecordHeadlightSignalDrop(payloadIndex, SubmarineLightsChangedSignalOperations.Remove);
                }
            }

            _publishedHeadlightSignalMask = nextPublishedMask;
        }

        private bool PublishHeadlightRemoveSignal(uint sourceId, int payloadIndex, byte flags)
        {
            SubmarineLightsChangedSignal signal = default;
            signal.SourceId = sourceId;
            signal.Slot = (ushort)math.clamp(payloadIndex, 0, MaxHeadlights - 1);
            signal.Operation = SubmarineLightsChangedSignalOperations.Remove;
            signal.Flags = flags;
            return SignalBus<SubmarineLightsChangedSignal>.TryPushTracked(in signal, ref s_x001DirectSignalPushDropCount_MantaScooter);
        }

        private void RecordHeadlightSignalDrop(int payloadIndex, byte operation)
        {
            _headlightSignalDropCount++;
            _lastHeadlightSignalDropSlot = (ushort)math.clamp(payloadIndex, 0, MaxHeadlights - 1);
            _lastHeadlightSignalDropOperation = operation;
        }

        private static void ConfigureMantaSignalLanesCold()
        {
            SignalBus<SeaglidePropulsionRequestSignal>.Configure(
                SeaglidePropulsionRequestSignal.ExpectedCapacity,
                maxFrameSignals: SeaglidePropulsionRequestSignal.MaxFrameSignals,
                lowTierFrameSignals: SeaglidePropulsionRequestSignal.LowTierFrameSignals,
                laneHash: SeaglidePropulsionRequestSignal.LaneHash);
            SignalBus<SeaglidePropulsionRequestSignal>.EnsureInitialized();
            SignalBus<SubmarineLightsChangedSignal>.Configure(
                SubmarineLightsChangedSignal.ExpectedCapacity,
                maxFrameSignals: SubmarineLightsChangedSignal.MaxFrameSignals,
                lowTierFrameSignals: SubmarineLightsChangedSignal.LowTierFrameSignals,
                laneHash: SubmarineLightsChangedSignal.LaneHash);
            SignalBus<SubmarineLightsChangedSignal>.EnsureInitialized();
        }

        private static uint ComputeStableSignalLaneHash(string label)
        {
            const uint fnvOffset = 2166136261u;
            const uint fnvPrime = 16777619u;
            uint hash = fnvOffset;
            if (!string.IsNullOrEmpty(label))
            {
                for (int i = 0; i < label.Length; i++)
                {
                    hash ^= label[i];
                    hash *= fnvPrime;
                }
            }

            return hash != 0u ? hash : 1u;
        }

        private uint ResolveHeadlightSignalSourceId()
        {
            uint sourceId = unchecked((uint)GetHashCode()) ^ HeadlightSignalSourceSalt;
            return sourceId != 0u ? sourceId : HeadlightSignalSourceSalt;
        }

        private static float3 NormalizeHeadlightSignalDirection(float3 direction)
        {
            float lengthSq = math.lengthsq(direction);
            if (!float.IsFinite(lengthSq) || lengthSq <= 0.000001f)
                return new float3(0f, 0f, 1f);

            return direction * math.rsqrt(lengthSq);
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
            if (depthText != null && TryResolveDepthTenths(out int depthTenths))
            {
                if (depthTenths != _lastDepthTenths)
                {
                    SetDepthHudText(depthTenths);
                    _lastDepthTenths = depthTenths;
                }
            }

            // Update battery display
            if (batteryText != null)
            {
                int batteryPercent = RoundToIntPositive(BatteryCharge * 100f);
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

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  PRIVATE â€” REFERENCES
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        private bool TryResolveDepthTenths(out int depthTenths)
        {
            float depthMeters = ResolveCachedDepthMeters();
            if (!math.isfinite(depthMeters))
            {
                depthTenths = 0;
                return false;
            }

            depthTenths = RoundToIntPositive(depthMeters * 10f);
            return true;
        }

        private float ResolveCachedDepthMeters()
        {
            if (TryResolveSeaglideMovementState(out PlayerMovementRuntimeState movementState) &&
                (movementState.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) != 0u &&
                math.isfinite(movementState.DepthMeters))
            {
                return math.max(0f, movementState.DepthMeters);
            }

            if (TryGetPlayerRuntimeContext(out IPlayerRuntimeContext playerContext) &&
                playerContext.IsInitialized &&
                playerContext.TryGetMovementRuntimeState(out movementState) &&
                (movementState.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) != 0u &&
                math.isfinite(movementState.DepthMeters))
            {
                return math.max(0f, movementState.DepthMeters);
            }

            return 0f;
        }

        private void ResetHudStateCache()
        {
            _hudStateInitialized = false;
            _lastHudVisible = false;
            _lastDepthTenths = int.MinValue;
            _lastBatteryPercent = int.MinValue;
            if (!_isActive)
                _debugActivationState = ActivationStateIdle;
        }

        private void RegisterToTick()
        {
            if (_registeredTick)
                return;
            if (!Application.isPlaying || !_dispatcherAvailable)
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

        private void QueueHeadlightPresentation(float deltaTime)
        {
            _headlightPresentationDeltaTime = math.clamp(
                math.select(0.0001f, deltaTime, math.isfinite(deltaTime) && deltaTime > 0f),
                0.0001f,
                0.2f);
            _headlightPresentationDirty = true;
        }

        private void QueueHudPresentation()
        {
            _hudPresentationDirty = true;
        }

        private void RegisterToLateFrame()
        {
            if (_registeredLateFrame)
                return;
            if (!Application.isPlaying || !_dispatcherAvailable)
                return;

            _registeredLateFrame = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Player);
        }

        private void UnregisterFromLateFrame()
        {
            UnregisterFromLateFrame(clearPendingPresentation: true);
        }

        private void UnregisterFromLateFrame(bool clearPendingPresentation)
        {
            if (!_registeredLateFrame)
                return;

            GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Player);
            _registeredLateFrame = false;
            if (!clearPendingPresentation)
                return;

            _headlightClearGlobalsDirty = false;
            _headlightPresentationDirty = false;
            _hudPresentationDirty = false;
            _headlightDefaultsRestoreDirty = false;
            _unregisterLateFrameAfterHeadlightClear = false;
            _headlightPresentationDeltaTime = 0f;
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot == GlobalRegistryServiceSlot.Dispatcher)
            {
                bool needsTick = _registeredTick || IsEquipped;
                bool needsLateFrame = _registeredLateFrame ||
                                      IsEquipped ||
                                      _headlightClearGlobalsDirty ||
                                      _headlightPresentationDirty ||
                                      _hudPresentationDirty ||
                                      _headlightDefaultsRestoreDirty ||
                                      _unregisterLateFrameAfterHeadlightClear;

                UnregisterFromTick();
                UnregisterFromLateFrame(clearPendingPresentation: false);
                _dispatcherAvailable = currentService != null;
                if (currentService != null && isActiveAndEnabled && IsEquipped && needsTick)
                    RegisterToTick();

                if (currentService != null && isActiveAndEnabled && needsLateFrame)
                    RegisterToLateFrame();

                return;
            }

            if (serviceSlot != GlobalRegistryServiceSlot.Input &&
                serviceSlot != GlobalRegistryServiceSlot.ObjectPool &&
                serviceSlot != GlobalRegistryServiceSlot.AcousticZoneRuntime &&
                serviceSlot != GlobalRegistryServiceSlot.LocalizationRuntime)
            {
                return;
            }

            RefreshCachedRegistryServices();
            if (serviceSlot == GlobalRegistryServiceSlot.ObjectPool)
                CacheObjectPoolService(currentService as ObjectPoolManager);

            if (serviceSlot == GlobalRegistryServiceSlot.LocalizationRuntime)
            {
                RefreshMantaLocalizationCache();
                ResetHudStateCache();
            }
        }

        private void RefreshCachedRegistryServices()
        {
            _cachedInputService = GlobalRegistry.Input;
            CacheObjectPoolService(null);
            _cachedToolAcousticCues = GlobalRegistry.ToolAcousticCues;
            _cachedBabelLocalization = GlobalRegistry.BabelLocalization;
        }

        private void CacheObjectPoolService(ObjectPoolManager candidate)
        {
            ObjectPoolManager pool = candidate;
            if (ObjectPoolManager.IsRuntimeOwnerUsableForRegistry(pool) ||
                ObjectPoolManager.TryResolveActiveRuntime(ref pool))
            {
                _cachedObjectPool = pool;
                return;
            }

            _cachedObjectPool = null;
        }

        private bool TryResolveCachedObjectPool(out IObjectPoolService pool)
        {
            ObjectPoolManager cached = _cachedObjectPool as ObjectPoolManager;
            if (ObjectPoolManager.IsRuntimeOwnerUsableForRegistry(cached))
            {
                pool = cached;
                return true;
            }

            ObjectPoolManager resolved = cached;
            if (ObjectPoolManager.TryResolveActiveRuntime(ref resolved))
            {
                _cachedObjectPool = resolved;
                pool = resolved;
                return true;
            }

            _cachedObjectPool = null;
            pool = null;
            return false;
        }

        private void TryRegisterHotSwapListener()
        {
            if (_hotSwapListenerRegistered || !Application.isPlaying)
                return;

            _hotSwapListenerRegistered = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterHotSwapListener()
        {
            if (!_hotSwapListenerRegistered)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _hotSwapListenerRegistered = false;
        }

        public void OnLocalizationLanguageChanged(in LocalizationEventPayload payload)
        {
            HandleMantaLanguageChanged();
        }

        private void HandleMantaLanguageChanged()
        {
            RefreshMantaLocalizationCache();
            ResetHudStateCache();
        }

        private void RefreshMantaLocalizationCache()
        {
            CopyMantaLocalizedLabel(H8ToolLocHashes.MANTA_HUD_NO_BATTERY, "MANTA - NO BATTERY", ref _localizedNoBatteryWarningBuffer);
            CopyMantaLocalizedLabel(H8ToolLocHashes.MANTA_HUD_BATTERY_DEPLETED, "MANTA - BATTERY DEPLETED", ref _localizedBatteryDepletedWarningBuffer);
            CopyMantaLocalizedLabel(H8ToolLocHashes.MANTA_SUMMARY_NO_BATTERY, "MANTA // NO BATTERY", ref _localizedSummaryNoBatteryBuffer);
            CopyMantaLocalizedLabel(H8ToolLocHashes.MANTA_SUMMARY_ACTIVE, "MANTA // ACTIVE // BAT {0}%", ref _localizedSummaryActiveFormatBuffer);
            CopyMantaLocalizedLabel(H8ToolLocHashes.MANTA_SUMMARY_STANDBY, "MANTA // STANDBY // BAT {0}%", ref _localizedSummaryStandbyFormatBuffer);
            CopyMantaLocalizedLabel(H8ToolLocHashes.MANTA_DIRECTIVE_INSERT_BATTERY, "Insert a battery to activate propulsion.", ref _localizedDirectiveInsertBatteryBuffer);
            CopyMantaLocalizedLabel(H8ToolLocHashes.MANTA_DIRECTIVE_SWAP_OR_RECHARGE, "Battery depleted. Swap or recharge.", ref _localizedDirectiveSwapRechargeBuffer);
            CopyMantaLocalizedLabel(H8ToolLocHashes.MANTA_DIRECTIVE_HOLD_FORWARD, "Hold forward to propel. Release to coast.", ref _localizedDirectiveHoldForwardBuffer);
            CopyMantaLocalizedLabel(H8ToolLocHashes.MANTA_DIRECTIVE_HOLD_PRIMARY, "Hold primary to activate propulsion while swimming.", ref _localizedDirectiveHoldPrimaryBuffer);
            CopyMantaLocalizedLabel(H8ToolLocHashes.MANTA_HUD_BATTERY_DEPLETED, "MANTA - DRIVE FAILURE", ref _localizedTransportBrokenWarningBuffer);
        }

        private void CopyMantaLocalizedLabel(uint keyHash, ReadOnlySpan<char> fallback, ref FixedCharBuffer destination)
        {
            destination.Clear();
            IBabelLocalization localization = _cachedBabelLocalization;
            if (localization != null &&
                localization.TryGetLocalizedBuffer(keyHash, out char[] buffer, out int length) &&
                buffer != null &&
                length > 0 &&
                destination.Append(buffer.AsSpan(0, length)))
            {
                return;
            }

            destination.Append(fallback);
        }

        private static bool AppendPercentTemplate(ref FixedCharBuffer buffer, ReadOnlySpan<char> template, int percent)
        {
            int tokenIndex = IndexOfToken(template);
            if (tokenIndex < 0)
            {
                if (!buffer.Append(template))
                    return false;

                if (!buffer.Append(" "))
                    return false;

                if (!buffer.AppendInt(math.clamp(percent, 0, 100)))
                    return false;

                return buffer.Append("%");
            }

            if (!buffer.Append(template.Slice(0, tokenIndex)))
                return false;

            if (!buffer.AppendInt(math.clamp(percent, 0, 100)))
                return false;

            return buffer.Append(template.Slice(tokenIndex + 3));
        }

        private static int IndexOfToken(ReadOnlySpan<char> template)
        {
            for (int i = 0; i <= template.Length - 3; i++)
            {
                if (template[i] == '{' && template[i + 1] == '0' && template[i + 2] == '}')
                    return i;
            }

            return -1;
        }

        private static bool AppendText(ref FixedCharBuffer buffer, ReadOnlySpan<char> value)
        {
            return value.Length == 0 || buffer.Append(value);
        }

        private void PublishToolWarning(ReadOnlySpan<char> message)
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
