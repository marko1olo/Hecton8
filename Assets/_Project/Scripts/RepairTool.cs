// ============================================================================
// HECTON-8 — RepairTool.cs
// Remontnyy instrument igroka.
//
// NASLEDOVANIE:
//   PlayerTool → RepairTool
//
// LOGIKA:
//   • UsePrimary(dt):
//       1. Reads the queued interaction probe result.
//       2. Converts submarine hits through AUP double3 into local hull space.
//       3. Erases GlobalDataVault.HullDents and emits typed repair signals.
//   • ToolTick(dt):
//       Records the 300-frame blackbox heartbeat and gates idle visuals.
//
// VIZUAL:
//   • sparksVFX         — iskry.
//   • repairLine        — LineRenderer lucha/dugi.
//   • weldLight         — yarkiy point light dlya Bloom v shleme.
//
// ZERO GC:
//   • InteractionSurfaceHit — struct.
//   • TryGetComponent — zero GC.
//   • SystemDispatcher tick only.
// ============================================================================

using System;
using System.IO;
using System.Runtime.InteropServices;
using Hecton.Localization;
using Hecton8.Core;
using Hecton8.Core.Contracts;
using Hecton8.Core.Memory;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Gameplay;
using Hecton8.Audio;
using Hecton8.Interaction;
using Hecton8.Items;
using Hecton8.Tools;
using Hecton8.UI;
using Hecton8.World;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Audio;

using ISubmarineDamageControlTarget = global::Hecton8.Core.Contracts.ISubmarineDamageControlTarget;
using ISubmarineRepairRoomResolver = global::Hecton8.Core.Contracts.ISubmarineRepairRoomResolver;

namespace Hecton8.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class RepairTool : PlayerTool, IBatteryTool, ILateFrameTickable, IGlobalRegistryHotSwapListener
    {
        private static int s_x001RepairToolSignalPushDropCount;
        private const string RepairToolNoPowerHeadline = "NO POWER";
        private const string RepairToolDrainingHeadline = "DRAINING";
        private const string RepairToolFloodedHeadline = "FLOODED";
        private const string RepairToolSealedHeadline = "SEALED";
        private const string RepairToolCriticalDamageHeadline = "CRITICAL DAMAGE";
        private const string RepairToolHeavyDamageHeadline = "HEAVY DAMAGE";
        private const string RepairToolPatchingHeadline = "PATCHING";
        private const string RepairToolCategory = "REPAIR";
        private const string RepairToolModuleLabel = "BASE MODULE";
        private const uint RepairSparksSignalHash = 0x44525350u;
        private const uint HullRepairSourceHash = 0x574C4452u; // WLDR
        private const uint HullRepairTelemetryHash = 0x48445250u; // HDRP
        private const uint RepairBlackBoxDumpFaultHash = 0x574C4446u; // WLDF
        private const byte RepairSparkDebrisKind = DebrisSpawnSignal.DebrisKindSparks;
        private const int HullDentVaultCapacity = 16;
        private const int HullDentRadiusQuantizationStepsPerMeter = 16;
        private const float InvHullDentRadiusQuantizationStepsPerMeter = 1f / HullDentRadiusQuantizationStepsPerMeter;
        private const float InvHullDentDepthQuantizationSteps = 1f / 255f;
        private const float HullDentRepairRadiusMeters = 2f;
        private const float HullDentRepairRadiusSq = HullDentRepairRadiusMeters * HullDentRepairRadiusMeters;
        private const float HullDentRepairDepthScale = 0.01f;
        private const float MinimumStoredHullDentDepthMeters = 0.001f;
        private const float HullRepairEpsilon = 0.0001f;
        private const int RepairBlackBoxFrameCount = 300;
        private const int RepairBlackBoxEntrySizeBytes = 64;
        private const string RepairBlackBoxDumpPath = "Docs/AgentLogs/Dump_SHINOBU_224_RepairTool.bin";
        private static readonly System.Threading.WaitCallback RepairBlackBoxDumpWorkerCallback = RunRepairBlackBoxDumpWorker;
        private static readonly ulong HullDentsMutationGuardMask = MutationGuardBit(BufferID.HullDents);
        private static readonly ulong RepairBlackBoxMutationGuardMask = MutationGuardBit(BufferID.RepairToolBlackBox);
        private const byte RepairBlackBoxFlagEquipped = 1 << 0;
        private const byte RepairBlackBoxFlagRepairing = 1 << 1;
        private const byte RepairBlackBoxFlagDentTouched = 1 << 2;
        private const byte RepairBlackBoxFlagDentRepaired = 1 << 3;
        private const byte RepairBlackBoxFlagVaultChanged = 1 << 4;
        private const byte RepairBlackBoxFlagInvalidMath = 1 << 5;
        private const float PowerIndicatorLowChargeThreshold01 = 0.2f;
        private static readonly char[] s_integrityDiagnosticPrefixChars = "INTEGRITY ".ToCharArray();
        private static FixedCharBuffer s_hudBuffer = new FixedCharBuffer(512); // COLD ALLOC: char[512] - repair tool HUD staging buffer - owner: RepairTool
        private static IBabelLocalization s_cachedRepairBabelLocalization;
        private static ushort s_cachedRepairLocalizationLanguageId = ushort.MaxValue;
        private static string s_locRepairToolCategory = RepairToolCategory;
        private static string s_locRepairToolHudNoTarget = "REPAIR TOOL - NO TARGET";
        private static string s_locRepairToolHudSealed = "REPAIR TOOL - MODULE SEALED";
        private static string s_locRepairToolHudRestored = "REPAIR TOOL - MODULE RESTORED";
        private static string s_locRepairToolHudInvalidTarget = "REPAIR TOOL - INVALID TARGET";
        private static string s_locRepairToolHudNoModule = "REPAIR TOOL - NO MODULE IN RANGE";
        private static string s_locRepairToolHudNotServiceable = "REPAIR TOOL - TARGET NOT SERVICEABLE";
        private static string s_locRepairToolHudNoPower = "REPAIR TOOL - NO POWER";
        private static string s_locRepairToolHudDraining = "REPAIR TOOL - DRAINING";
        private static string s_locRepairToolHudFlooded = "REPAIR TOOL - FLOODED";
        private static string s_locRepairToolHudCriticalDamage = "REPAIR TOOL - CRITICAL DAMAGE";
        private static string s_locRepairToolHudHeavyDamage = "REPAIR TOOL - HEAVY DAMAGE";
        private static string s_locRepairToolHudPatching = "REPAIR TOOL - PATCHING";
        private static string s_locRepairToolLogStartedTitle = "MODULE REPAIR STARTED";
        private static string s_locRepairToolLogStartedMessage = "{0} entered active repair service. {1} {2}";
        private static string s_locRepairToolLogRestoredTitle = "MODULE RESTORED";
        private static string s_locRepairToolLogRestoredMessage = "{0} reached full integrity and dry status.";
        private static string s_locRepairToolLogDiagNoPower = "SERVICE DIAG - NO POWER";
        private static string s_locRepairToolLogDiagDraining = "SERVICE DIAG - DRAINING";
        private static string s_locRepairToolLogDiagFlooded = "SERVICE DIAG - FLOODED";
        private static string s_locRepairToolLogDiagSealed = "SERVICE DIAG - SEALED";
        private static string s_locRepairToolLogDiagCritical = "SERVICE DIAG - CRITICAL DAMAGE";
        private static string s_locRepairToolLogDiagHeavy = "SERVICE DIAG - HEAVY DAMAGE";
        private static string s_locRepairToolLogDiagPatching = "SERVICE DIAG - PATCHING";
        private static string s_locRepairToolLogDiagGeneric = "SERVICE DIAG - {0}";
        private static string s_locRepairToolOperationalActive = "REPAIR TOOL // ACTIVE SERVICE";
        private static string s_locRepairToolOperationalStandby = "REPAIR TOOL // STANDBY";
        private static string s_locRepairToolOperationalActiveDirective = "Hold the beam steady until the service window closes.";
        private static string s_locRepairToolOperationalStandbyDirective = "Sweep a damaged module to diagnose or begin repair.";
        private static string s_locRepairToolSummaryNoPower = "Integrity {0:0}% // compartment flooded // pumps offline.";
        private static string s_locRepairToolSummaryDraining = "Integrity {0:0}% // pumps are clearing floodwater.";
        private static string s_locRepairToolSummaryFlooded = "Integrity {0:0}% // compartment breach still active.";
        private static string s_locRepairToolSummarySealed = "Integrity 100% // hull stable // compartment dry.";
        private static string s_locRepairToolSummaryCritical = "Integrity {0:0}% // hull failure risk elevated.";
        private static string s_locRepairToolSummaryHeavy = "Integrity {0:0}% // hull is compromised but recoverable.";
        private static string s_locRepairToolSummaryPatching = "Integrity {0:0}% // module is nearly sealed.";
        private static string s_locRepairToolRecommendNoPower = "Restore power before expecting water evacuation.";
        private static string s_locRepairToolRecommendDraining = "Hold perimeter and let the compartment finish draining.";
        private static string s_locRepairToolRecommendFlooded = "Continue repair until integrity reaches 100% and pump cycle can start.";
        private static string s_locRepairToolRecommendSealed = "No further repair action required.";
        private static string s_locRepairToolRecommendCritical = "Maintain continuous repair contact until the module exits critical range.";
        private static string s_locRepairToolRecommendHeavy = "Keep the repair beam on target and avoid leaving the module unattended.";
        private static string s_locRepairToolRecommendPatching = "Finish the repair cycle to restore full integrity.";
        private static string s_locRepairToolPriorityServiceBlocked = "SERVICE BLOCKED";
        private static string s_locRepairToolPriorityStabilizing = "STABILIZING";
        private static string s_locRepairToolPriorityImmediateService = "IMMEDIATE SERVICE";
        private static string s_locRepairToolPriorityServiceComplete = "SERVICE COMPLETE";
        private static string s_locRepairToolPriorityCriticalResponse = "CRITICAL RESPONSE";
        private static string s_locRepairToolPriorityActiveService = "ACTIVE SERVICE";
        private static string s_locRepairToolPriorityFinalPass = "FINAL PASS";
        private static uint s_hullRepairSignalFrame;

        [StructLayout(LayoutKind.Explicit, Size = RepairBlackBoxEntrySizeBytes)]
        private struct RepairToolBlackBoxEntry
        {
            [FieldOffset(0)]
            public AbsoluteUniversePosition HitAup;
            [FieldOffset(48)]
            public uint Frame;
            [FieldOffset(52)]
            public uint StateHash;
            [FieldOffset(56)]
            public ushort ActiveDentCount;
            [FieldOffset(58)]
            public ushort TouchedDentCount;
            [FieldOffset(60)]
            public byte RepairedDentCount;
            [FieldOffset(61)]
            public byte Battery255;
            [FieldOffset(62)]
            public byte Flags;
            [FieldOffset(63)]
            public byte Reserved0;
        }

        private struct ServiceDiagnosis
        {
            public string status;
            public string headline;
            public string summary;
            public uint summaryKey;
            public string summaryFallback;
            public string recommendation;
            public string severity;
            public string priority;
            public int integrityPercent;
            public bool hasIntegrityPercent;
        }

        private enum PowerIndicatorVisualState : byte
        {
            Unknown = 0,
            Off = 1,
            Low = 2,
            On = 3
        }

        private enum RepairBeamVisualMode : byte
        {
            None = 0,
            Hit = 1,
            Miss = 2,
            Diagnostic = 3
        }

        // ══════════════════════════════════════════════════════════
        //  INSPECTOR
        // ══════════════════════════════════════════════════════════

        [Header("── Repair Settings ───────────────────────────")]
        [Tooltip("Maksimalnaya dalnost remonta.")]
        [SerializeField] private float repairRange = 4f;

        [Tooltip("Skorost remonta (edinits tselostnosti v sekundu).")]
        [SerializeField] private float repairSpeed = 20f;

        [Tooltip("Sloi, po kotorym rabotaet remontnyy luch.")]
        [SerializeField] private LayerMask repairMask = Hecton8.Core.HectonLayerMasks.FieldToolSurfaceLayerMask;

        [Header("── Visuals ───────────────────────────────────")]
        [SerializeField] private LineRenderer repairLine;
        [SerializeField] private ParticleSystem sparksVFX;
        [SerializeField] private Light weldLight;
        [SerializeField] private AudioSource repairLoopAudio;

        [Header("── Battery ───────────────────────────────────")]
        [Tooltip("Optional battery item type accepted by the repair tool.")]

        // ══════════════════════════════════════════════════════════
        //  RUNTIME STATE
        // ══════════════════════════════════════════════════════════

        private InteractionSurfaceHit _hit;
        private bool _isRepairing;
        private bool _wasRepairingLastFrame;
        private bool _invalidTargetReportedThisUse;
        private bool _noTargetReportedThisUse;
        private bool _healthyTargetReportedThisUse;
        private bool _activeRepairReportedThisUse;
        private bool _secondaryLatched;
        private uint _diagnosisEvaluationStamp;
        private uint _cachedDiagnosisStamp = uint.MaxValue;
        private bool _cachedDiagnosisValid;
        private Collider _cachedServiceTargetCollider;
        private IRepairableModuleTarget _cachedServiceTargetModule;
        private IVoxelRepairWeldTarget _cachedServiceTargetVoxelRepairTarget;
        private Collider _cachedAirlockTargetCollider;
        private BaseAirlock _cachedServiceTargetAirlock;
        private Collider _cachedSubmarineDamageTargetCollider;
        private ISubmarineDamageControlTarget _cachedSubmarineDamageTarget;
        private ISubmarineRepairRoomResolver _cachedSubmarineRepairRoomResolver;
        private Transform _cachedSubmarineDamageTargetTransform;
        private IDataVault _dataVault;
        private AudioMixerGroup _cachedRepairAudioMixerGroup;
        private DiegeticTooltipSystem _cachedDiegeticTooltipSystem;
        private VaultGenerationHandle<float4> _hullDentsHandle;
        private VaultGenerationHandle<RepairToolBlackBoxEntry> _repairBlackBoxHandle;
        private bool _ownsRepairBlackBoxBuffer;
        private bool _repairBlackBoxDumpedThisFault;
        private bool _repairBlackBoxDumpPending;
        private readonly RepairToolBlackBoxEntry[] _repairBlackBoxDumpSnapshot = new RepairToolBlackBoxEntry[RepairBlackBoxFrameCount]; // COLD ALLOC: managed fault-dump snapshot - owner: RepairTool
        private int _repairBlackBoxDumpInFlight;
        private uint _repairBlackBoxDumpFrame;
        private readonly char[] _integrityDiagnosticBuffer = new char[24]; // COLD ALLOC: char[24] — repair-tool floating integrity diagnostic buffer — owner: RepairTool

        // ══════════════════════════════════════════════════════════
        //  IBatteryTool STATE
        // ══════════════════════════════════════════════════════════

        [Header("── Battery Settings ─────────────────────────")]
        [Tooltip("Battery item type this tool uses.")]
        [SerializeField] private ItemData _batteryItemType;

        [Header("── Battery Visuals ──────────────────────────")]
        [Tooltip("Mesh to hide when battery is removed.")]
        [SerializeField] private GameObject _batteryMesh;

        [Tooltip("Renderer for power indicator light.")]
        [SerializeField] private Renderer _powerIndicatorRenderer;

        [Tooltip("Shared material for the power indicator when no battery power is available.")]
        [SerializeField] private Material _powerIndicatorOffMaterial;

        [Tooltip("Shared material for the power indicator when battery charge is low.")]
        [SerializeField] private Material _powerIndicatorLowMaterial;

        [Tooltip("Shared material for the power indicator when battery charge is serviceable.")]
        [SerializeField] private Material _powerIndicatorOnMaterial;

        private ItemData _installedBattery;
        private float _batteryCharge;
        private ServiceDiagnosis _cachedDiagnosis;

        private Material _powerIndicatorDefaultMaterial;
        private Material _powerIndicatorAppliedMaterial;
        private PowerIndicatorVisualState _powerIndicatorVisualState = PowerIndicatorVisualState.Unknown;
        private bool _powerIndicatorAppliedVisible = true;
        private uint _repairBlackBoxFrame;
        private bool _lateFrameRegistered;
        private bool _powerIndicatorDirty;
        private bool _repairVisualStateDirty;
        private bool _pendingRepairVisualActive;
        private bool _beamVisualDirty;
        private RepairBeamVisualMode _pendingBeamVisualMode;
        private Vector3 _pendingBeamHitPoint;
        private Vector3 _pendingBeamHitNormal;
        private bool _pendingSparkEmit;
        private ushort _pendingSparkQuantity;
        private float _pendingSparkQuality01;

        // ══════════════════════════════════════════════════════════
        //  IBatteryTool IMPLEMENTATION
        // ══════════════════════════════════════════════════════════

        /// <summary>True if the tool currently has a battery installed.</summary>
        public bool HasBattery => _installedBattery != null;

        /// <summary>Current battery charge level (0-1). Returns 0 if no battery.</summary>
        public float BatteryCharge => _installedBattery != null ? GetRuntimeBatteryNormalized(_batteryCharge) : 0f;

        /// <summary>The battery item currently installed (null if none).</summary>
        public ItemData BatteryItem => _installedBattery;

        /// <summary>
        /// Removes the battery from the tool.
        /// </summary>
        public ItemData RemoveBattery()
        {
            if (_installedBattery == null)
                return null;

            ItemData removed = _installedBattery;
            _installedBattery = null;
            _batteryCharge = 0f;
            SetRuntimeBatteryNormalized(0f);

            UpdateBatteryVisuals();
            QueuePowerIndicatorUpdate();

            return removed;
        }

        /// <summary>
        /// Inserts a battery into the tool.
        /// </summary>
        public bool InsertBattery(ItemData battery, float charge)
        {
            if (battery == null)
                return false;

            _installedBattery = battery;
            _batteryCharge = math.saturate(charge);
            SetRuntimeBatteryNormalized(_batteryCharge);

            UpdateBatteryVisuals();
            QueuePowerIndicatorUpdate();

            return true;
        }

        private void UpdateBatteryVisuals()
        {
            if (_batteryMesh != null)
                _batteryMesh.SetActive(_installedBattery != null);
        }

        private void UpdatePowerIndicator()
        {
            if (_powerIndicatorRenderer == null)
                return;

            CachePowerIndicatorDefaultMaterial();
            PowerIndicatorVisualState nextState = ResolvePowerIndicatorVisualState();
            Material nextMaterial = ResolvePowerIndicatorMaterial(nextState);
            bool nextVisible = nextState != PowerIndicatorVisualState.Off || nextMaterial != null;
            if (nextMaterial == null)
                nextMaterial = _powerIndicatorDefaultMaterial;

            if (_powerIndicatorVisualState == nextState &&
                ReferenceEquals(_powerIndicatorAppliedMaterial, nextMaterial) &&
                _powerIndicatorAppliedVisible == nextVisible)
            {
                return;
            }

            if (_powerIndicatorRenderer.enabled != nextVisible)
                _powerIndicatorRenderer.enabled = nextVisible;

            if (nextVisible &&
                nextMaterial != null &&
                !ReferenceEquals(_powerIndicatorRenderer.sharedMaterial, nextMaterial))
            {
                _powerIndicatorRenderer.sharedMaterial = nextMaterial;
            }

            _powerIndicatorVisualState = nextState;
            _powerIndicatorAppliedMaterial = nextMaterial;
            _powerIndicatorAppliedVisible = nextVisible;
        }

        private void CachePowerIndicatorDefaultMaterial()
        {
            if (_powerIndicatorDefaultMaterial == null && _powerIndicatorRenderer != null)
                _powerIndicatorDefaultMaterial = _powerIndicatorRenderer.sharedMaterial;
        }

        private PowerIndicatorVisualState ResolvePowerIndicatorVisualState()
        {
            float currentCharge = BatteryCharge;
            if (_installedBattery == null || currentCharge <= 0f)
                return PowerIndicatorVisualState.Off;

            return currentCharge <= PowerIndicatorLowChargeThreshold01
                ? PowerIndicatorVisualState.Low
                : PowerIndicatorVisualState.On;
        }

        private Material ResolvePowerIndicatorMaterial(PowerIndicatorVisualState visualState)
        {
            switch (visualState)
            {
                case PowerIndicatorVisualState.Off:
                    return _powerIndicatorOffMaterial;
                case PowerIndicatorVisualState.Low:
                    return _powerIndicatorLowMaterial != null ? _powerIndicatorLowMaterial : _powerIndicatorDefaultMaterial;
                case PowerIndicatorVisualState.On:
                    return _powerIndicatorOnMaterial != null ? _powerIndicatorOnMaterial : _powerIndicatorDefaultMaterial;
                default:
                    return _powerIndicatorDefaultMaterial;
            }
        }

        // ══════════════════════════════════════════════════════════
        //  LIFECYCLE
        // ══════════════════════════════════════════════════════════

        private void Awake()
        {
            CachePowerIndicatorDefaultMaterial();
            CacheRepairLocalizationCold();
            CacheRepairAudioCold();
            CacheRepairVaultCold();
            CacheRepairRenderableCold();
            TryAssignRepairAudioMixerRoute();
            ApplyRepairVisuals(false);
        }

        private void OnDisable()
        {
            ClearRepairAudioCold();
            ClearRepairRenderableCold();
            FlushPendingRepairBlackBoxDump();
            ReleaseVaultState();
            ClearPendingRepairVisualSync();
            TryUnregisterLateFrameTick();
        }

        public override void OnSpawn()
        {
            base.OnSpawn();
            CacheRepairLocalizationCold();
            CacheRepairAudioCold();
            CacheRepairVaultCold();
            CacheRepairRenderableCold();
            TryAssignRepairAudioMixerRoute();
            _isRepairing = false;
            _wasRepairingLastFrame = false;
            _invalidTargetReportedThisUse = false;
            _noTargetReportedThisUse = false;
            _healthyTargetReportedThisUse = false;
            _activeRepairReportedThisUse = false;
            _secondaryLatched = false;
            _repairBlackBoxDumpedThisFault = false;
            _repairBlackBoxDumpPending = false;
            ApplyRepairVisuals(false);
        }

        public override void OnDespawn()
        {
            SyncRepairChargeMirrorFromCentral();
            _isRepairing = false;
            _wasRepairingLastFrame = false;
            _invalidTargetReportedThisUse = false;
            _noTargetReportedThisUse = false;
            _healthyTargetReportedThisUse = false;
            _activeRepairReportedThisUse = false;
            _secondaryLatched = false;
            FlushPendingRepairBlackBoxDump();
            _repairBlackBoxDumpedThisFault = false;
            ApplyRepairVisuals(false);
            ClearDiagnosticLaserTelemetry();
            ReleaseEquippedAudio();
            ClearRepairAudioCold();
            ClearRepairRenderableCold();
            ReleaseVaultState();
            ClearPendingRepairVisualSync();
            TryUnregisterLateFrameTick();
            base.OnDespawn();
        }

        public override void OnEquip()
        {
            base.OnEquip();
            CacheRepairLocalizationCold();
            CacheRepairAudioCold();
            CacheRepairVaultCold();
            CacheRepairRenderableCold();
            TryAssignRepairAudioMixerRoute();
            _isRepairing = false;
            _wasRepairingLastFrame = false;
            _invalidTargetReportedThisUse = false;
            _noTargetReportedThisUse = false;
            _healthyTargetReportedThisUse = false;
            _activeRepairReportedThisUse = false;
            _secondaryLatched = false;
            ApplyRepairVisuals(false);
            ClearDiagnosticLaserTelemetry();
            QueuePowerIndicatorUpdate();
            PrewarmEquippedAudio();
        }

        public override void OnUnequip()
        {
            SyncRepairChargeMirrorFromCentral();
            _isRepairing = false;
            _wasRepairingLastFrame = false;
            _invalidTargetReportedThisUse = false;
            _noTargetReportedThisUse = false;
            _healthyTargetReportedThisUse = false;
            _activeRepairReportedThisUse = false;
            _secondaryLatched = false;
            ApplyRepairVisuals(false);
            ClearDiagnosticLaserTelemetry();
            ReleaseEquippedAudio();
            ClearRepairAudioCold();
            ClearRepairRenderableCold();
            ReleaseVaultState();
            ClearPendingRepairVisualSync();
            TryUnregisterLateFrameTick();
            base.OnUnequip();
        }

        protected override void OnToolRegistryServiceReplaced(GlobalRegistryServiceSlot serviceSlot, object previousService, object currentService)
        {
            base.OnToolRegistryServiceReplaced(serviceSlot, previousService, currentService);

            switch (serviceSlot)
            {
                case GlobalRegistryServiceSlot.Dispatcher:
                    bool needsLateFrame = _lateFrameRegistered ||
                                          _powerIndicatorDirty ||
                                          _repairVisualStateDirty ||
                                          _beamVisualDirty ||
                                          _pendingSparkEmit;
                    TryUnregisterLateFrameTick();
                    if (currentService != null && isActiveAndEnabled && needsLateFrame)
                        TryRegisterLateFrameTick();
                    break;
                case GlobalRegistryServiceSlot.DataVault:
                    RebindRepairVault(currentService as IDataVault);
                    break;
                case GlobalRegistryServiceSlot.Audio:
                    CacheRepairAudioMixerGroup(currentService as IAudioService);
                    if (repairLoopAudio != null)
                        repairLoopAudio.outputAudioMixerGroup = null;
                    TryAssignRepairAudioMixerRoute();
                    break;
                case GlobalRegistryServiceSlot.LocalizationRuntime:
                    IBabelLocalization localization = currentService as IBabelLocalization;
                    s_cachedRepairBabelLocalization = localization;
                    s_cachedRepairLocalizationLanguageId = localization != null
                        ? localization.ActiveLanguageId
                        : ushort.MaxValue;
                    RefreshRepairLocalizationCache(localization);
                    break;
            }
        }

        internal override float ResolveModularBatteryNormalized()
        {
            return _installedBattery != null ? BatteryCharge : 0f;
        }

        private void SyncRepairChargeMirrorFromCentral()
        {
            _batteryCharge = _installedBattery != null ? BatteryCharge : 0f;
        }

        private float ResolveRuntimeRepairRange()
        {
            return GetRuntimeMaxRange(repairRange);
        }

        private void TryAssignRepairAudioMixerRoute()
        {
            if (repairLoopAudio == null || repairLoopAudio.outputAudioMixerGroup != null)
                return;

            AudioMixerGroup mixerGroup = _cachedRepairAudioMixerGroup;
            if (mixerGroup != null)
                repairLoopAudio.outputAudioMixerGroup = mixerGroup;
        }

        private void CacheRepairAudioCold()
        {
            CacheRepairAudioMixerGroup(GlobalRegistry.Audio);
        }

        private void CacheRepairAudioMixerGroup(IAudioService audioService)
        {
            _cachedRepairAudioMixerGroup = IsAudioServiceUsable(audioService)
                ? audioService.AmbientGroup
                : null;
        }

        private static bool IsAudioServiceUsable(IAudioService audioService)
        {
            if (audioService == null || !audioService.IsAudioRuntimeReady)
                return false;

            if (audioService is Behaviour behaviour)
                return behaviour != null && behaviour.isActiveAndEnabled;

            return true;
        }

        private static void CacheRepairLocalizationCold()
        {
            IBabelLocalization localization = GlobalRegistry.BabelLocalization;
            ushort languageId = localization != null ? localization.ActiveLanguageId : ushort.MaxValue;
            if (ReferenceEquals(s_cachedRepairBabelLocalization, localization) &&
                s_cachedRepairLocalizationLanguageId == languageId)
            {
                return;
            }

            s_cachedRepairBabelLocalization = localization;
            s_cachedRepairLocalizationLanguageId = languageId;
            RefreshRepairLocalizationCache(s_cachedRepairBabelLocalization);
        }

        private static void RefreshRepairLocalizationCache(IBabelLocalization localization)
        {
            s_locRepairToolCategory = ResolveBabelString(localization, H8ToolLocHashes.REPAIR_TOOL_CATEGORY, RepairToolCategory);
            s_locRepairToolHudNoTarget = ResolveBabelString(localization, H8ToolLocHashes.REPAIR_TOOL_HUD_NO_TARGET, "REPAIR TOOL - NO TARGET");
            s_locRepairToolHudSealed = ResolveBabelString(localization, H8ToolLocHashes.REPAIR_TOOL_HUD_SEALED, "REPAIR TOOL - MODULE SEALED");
            s_locRepairToolHudRestored = ResolveBabelString(localization, H8ToolLocHashes.REPAIR_TOOL_HUD_RESTORED, "REPAIR TOOL - MODULE RESTORED");
            s_locRepairToolHudInvalidTarget = ResolveBabelString(localization, H8ToolLocHashes.REPAIR_TOOL_HUD_INVALID_TARGET, "REPAIR TOOL - INVALID TARGET");
            s_locRepairToolHudNoModule = ResolveBabelString(localization, H8ToolLocHashes.REPAIR_TOOL_HUD_NO_MODULE, "REPAIR TOOL - NO MODULE IN RANGE");
            s_locRepairToolHudNotServiceable = ResolveBabelString(localization, H8ToolLocHashes.REPAIR_TOOL_HUD_NOT_SERVICEABLE, "REPAIR TOOL - TARGET NOT SERVICEABLE");
            s_locRepairToolHudNoPower = ResolveBabelString(localization, H8ToolLocHashes.REPAIR_TOOL_HUD_NO_POWER, "REPAIR TOOL - NO POWER");
            s_locRepairToolHudDraining = ResolveBabelString(localization, H8ToolLocHashes.REPAIR_TOOL_HUD_DRAINING, "REPAIR TOOL - DRAINING");
            s_locRepairToolHudFlooded = ResolveBabelString(localization, H8ToolLocHashes.REPAIR_TOOL_HUD_FLOODED, "REPAIR TOOL - FLOODED");
            s_locRepairToolHudCriticalDamage = ResolveBabelString(localization, H8ToolLocHashes.REPAIR_TOOL_HUD_CRITICAL_DAMAGE, "REPAIR TOOL - CRITICAL DAMAGE");
            s_locRepairToolHudHeavyDamage = ResolveBabelString(localization, H8ToolLocHashes.REPAIR_TOOL_HUD_HEAVY_DAMAGE, "REPAIR TOOL - HEAVY DAMAGE");
            s_locRepairToolHudPatching = ResolveBabelString(localization, H8ToolLocHashes.REPAIR_TOOL_HUD_PATCHING, "REPAIR TOOL - PATCHING");
            s_locRepairToolLogStartedTitle = ResolveBabelString(localization, H8ToolLocHashes.REPAIR_TOOL_LOG_STARTED_TITLE, "MODULE REPAIR STARTED");
            s_locRepairToolLogStartedMessage = ResolveBabelString(localization, H8ToolLocHashes.REPAIR_TOOL_LOG_STARTED_MESSAGE, "{0} entered active repair service. {1} {2}");
            s_locRepairToolLogRestoredTitle = ResolveBabelString(localization, H8ToolLocHashes.REPAIR_TOOL_LOG_RESTORED_TITLE, "MODULE RESTORED");
            s_locRepairToolLogRestoredMessage = ResolveBabelString(localization, H8ToolLocHashes.REPAIR_TOOL_LOG_RESTORED_MESSAGE, "{0} reached full integrity and dry status.");
            s_locRepairToolLogDiagNoPower = ResolveBabelString(localization, H8ToolLocHashes.REPAIR_TOOL_LOG_DIAG_NO_POWER, "SERVICE DIAG - NO POWER");
            s_locRepairToolLogDiagDraining = ResolveBabelString(localization, H8ToolLocHashes.REPAIR_TOOL_LOG_DIAG_DRAINING, "SERVICE DIAG - DRAINING");
            s_locRepairToolLogDiagFlooded = ResolveBabelString(localization, H8ToolLocHashes.REPAIR_TOOL_LOG_DIAG_FLOODED, "SERVICE DIAG - FLOODED");
            s_locRepairToolLogDiagSealed = ResolveBabelString(localization, H8ToolLocHashes.REPAIR_TOOL_LOG_DIAG_SEALED, "SERVICE DIAG - SEALED");
            s_locRepairToolLogDiagCritical = ResolveBabelString(localization, H8ToolLocHashes.REPAIR_TOOL_LOG_DIAG_CRITICAL, "SERVICE DIAG - CRITICAL DAMAGE");
            s_locRepairToolLogDiagHeavy = ResolveBabelString(localization, H8ToolLocHashes.REPAIR_TOOL_LOG_DIAG_HEAVY, "SERVICE DIAG - HEAVY DAMAGE");
            s_locRepairToolLogDiagPatching = ResolveBabelString(localization, H8ToolLocHashes.REPAIR_TOOL_LOG_DIAG_PATCHING, "SERVICE DIAG - PATCHING");
            s_locRepairToolLogDiagGeneric = ResolveBabelString(localization, H8ToolLocHashes.REPAIR_TOOL_LOG_DIAG_GENERIC, "SERVICE DIAG - {0}");
            s_locRepairToolOperationalActive = ResolveBabelString(localization, H8ToolLocHashes.REPAIR_TOOL_OPERATIONAL_ACTIVE, "REPAIR TOOL // ACTIVE SERVICE");
            s_locRepairToolOperationalStandby = ResolveBabelString(localization, H8ToolLocHashes.REPAIR_TOOL_OPERATIONAL_STANDBY, "REPAIR TOOL // STANDBY");
            s_locRepairToolOperationalActiveDirective = ResolveBabelString(localization, H8ToolLocHashes.REPAIR_TOOL_OPERATIONAL_ACTIVE_DIRECTIVE, "Hold the beam steady until the service window closes.");
            s_locRepairToolOperationalStandbyDirective = ResolveBabelString(localization, H8ToolLocHashes.REPAIR_TOOL_OPERATIONAL_STANDBY_DIRECTIVE, "Sweep a damaged module to diagnose or begin repair.");
            s_locRepairToolSummaryNoPower = ResolveBabelString(localization, H8ToolLocHashes.REPAIR_TOOL_SUMMARY_NO_POWER, "Integrity {0:0}% // compartment flooded // pumps offline.");
            s_locRepairToolSummaryDraining = ResolveBabelString(localization, H8ToolLocHashes.REPAIR_TOOL_SUMMARY_DRAINING, "Integrity {0:0}% // pumps are clearing floodwater.");
            s_locRepairToolSummaryFlooded = ResolveBabelString(localization, H8ToolLocHashes.REPAIR_TOOL_SUMMARY_FLOODED, "Integrity {0:0}% // compartment breach still active.");
            s_locRepairToolSummarySealed = ResolveBabelString(localization, H8ToolLocHashes.REPAIR_TOOL_SUMMARY_SEALED, "Integrity 100% // hull stable // compartment dry.");
            s_locRepairToolSummaryCritical = ResolveBabelString(localization, H8ToolLocHashes.REPAIR_TOOL_SUMMARY_CRITICAL, "Integrity {0:0}% // hull failure risk elevated.");
            s_locRepairToolSummaryHeavy = ResolveBabelString(localization, H8ToolLocHashes.REPAIR_TOOL_SUMMARY_HEAVY, "Integrity {0:0}% // hull is compromised but recoverable.");
            s_locRepairToolSummaryPatching = ResolveBabelString(localization, H8ToolLocHashes.REPAIR_TOOL_SUMMARY_PATCHING, "Integrity {0:0}% // module is nearly sealed.");
            s_locRepairToolRecommendNoPower = ResolveBabelString(localization, H8ToolLocHashes.REPAIR_TOOL_RECOMMEND_NO_POWER, "Restore power before expecting water evacuation.");
            s_locRepairToolRecommendDraining = ResolveBabelString(localization, H8ToolLocHashes.REPAIR_TOOL_RECOMMEND_DRAINING, "Hold perimeter and let the compartment finish draining.");
            s_locRepairToolRecommendFlooded = ResolveBabelString(localization, H8ToolLocHashes.REPAIR_TOOL_RECOMMEND_FLOODED, "Continue repair until integrity reaches 100% and pump cycle can start.");
            s_locRepairToolRecommendSealed = ResolveBabelString(localization, H8ToolLocHashes.REPAIR_TOOL_RECOMMEND_SEALED, "No further repair action required.");
            s_locRepairToolRecommendCritical = ResolveBabelString(localization, H8ToolLocHashes.REPAIR_TOOL_RECOMMEND_CRITICAL, "Maintain continuous repair contact until the module exits critical range.");
            s_locRepairToolRecommendHeavy = ResolveBabelString(localization, H8ToolLocHashes.REPAIR_TOOL_RECOMMEND_HEAVY, "Keep the repair beam on target and avoid leaving the module unattended.");
            s_locRepairToolRecommendPatching = ResolveBabelString(localization, H8ToolLocHashes.REPAIR_TOOL_RECOMMEND_PATCHING, "Finish the repair cycle to restore full integrity.");
            s_locRepairToolPriorityServiceBlocked = ResolveBabelString(localization, H8ToolLocHashes.REPAIR_TOOL_PRIORITY_SERVICE_BLOCKED, "SERVICE BLOCKED");
            s_locRepairToolPriorityStabilizing = ResolveBabelString(localization, H8ToolLocHashes.REPAIR_TOOL_PRIORITY_STABILIZING, "STABILIZING");
            s_locRepairToolPriorityImmediateService = ResolveBabelString(localization, H8ToolLocHashes.REPAIR_TOOL_PRIORITY_IMMEDIATE_SERVICE, "IMMEDIATE SERVICE");
            s_locRepairToolPriorityServiceComplete = ResolveBabelString(localization, H8ToolLocHashes.REPAIR_TOOL_PRIORITY_SERVICE_COMPLETE, "SERVICE COMPLETE");
            s_locRepairToolPriorityCriticalResponse = ResolveBabelString(localization, H8ToolLocHashes.REPAIR_TOOL_PRIORITY_CRITICAL_RESPONSE, "CRITICAL RESPONSE");
            s_locRepairToolPriorityActiveService = ResolveBabelString(localization, H8ToolLocHashes.REPAIR_TOOL_PRIORITY_ACTIVE_SERVICE, "ACTIVE SERVICE");
            s_locRepairToolPriorityFinalPass = ResolveBabelString(localization, H8ToolLocHashes.REPAIR_TOOL_PRIORITY_FINAL_PASS, "FINAL PASS");
        }

        private static string ResolveBabelString(IBabelLocalization localization, uint keyHash, string fallback)
        {
            return fallback ?? string.Empty;
        }

        private void ClearRepairAudioCold()
        {
            _cachedRepairAudioMixerGroup = null;
        }

        private void CacheRepairRenderableCold()
        {
            _cachedDiegeticTooltipSystem = null;

            RegistryBucket<IRenderable> renderables = GlobalRegistry.Renderables;
            int count = renderables.Count;
            for (int i = 0; i < count; i++)
            {
                if (renderables.GetAt(i) is DiegeticTooltipSystem tooltipSystem)
                {
                    _cachedDiegeticTooltipSystem = tooltipSystem;
                    return;
                }
            }
        }

        private void ClearRepairRenderableCold()
        {
            _cachedDiegeticTooltipSystem = null;
        }

        private void PrewarmEquippedAudio()
        {
            AudioResidencyCache.PrewarmAudioSource(repairLoopAudio, AudioResidencyDomain.Player);
        }

        private void ReleaseEquippedAudio()
        {
            AudioResidencyCache.ReleaseAudioSource(repairLoopAudio);
        }

        private float ResolveRuntimeRepairPowerPerSecond()
        {
            float runtimePower = GetRuntimePowerScalar(1f);
            if (!math.isfinite(runtimePower))
                runtimePower = 1f;

            return FiniteNonNegativeOrZero(repairSpeed) * math.max(0.1f, runtimePower);
        }

        private float ResolveRuntimeRepairPowerNormalized()
        {
            float runtimePower = GetRuntimePowerScalar(1f);
            return math.isfinite(runtimePower) ? math.saturate(runtimePower) : 0f;
        }

        // ══════════════════════════════════════════════════════════
        //  TOOL ACTIONS
        // ══════════════════════════════════════════════════════════

        public override void UsePrimary(float deltaTime)
        {
            float safeDeltaTime = FiniteNonNegativeOrZero(deltaTime);
            if (safeDeltaTime <= 0f)
            {
                _isRepairing = false;
                UpdateBeamMiss();
                InvalidateDiagnosisCache();
                return;
            }

            if (!TryBeginToolUse(safeDeltaTime, true))
            {
                _isRepairing = false;
                if (!_noTargetReportedThisUse)
                {
                    PublishWarningMessage(StableText(H8ToolLocHashes.REPAIR_TOOL_HUD_NO_POWER, "REPAIR TOOL - NO POWER"));
                    _noTargetReportedThisUse = true;
                }

                UpdateBeamMiss();
                InvalidateDiagnosisCache();
                return;
            }

            _isRepairing = true;
            bool didHit = TryGetRepairHit(out _hit);

            if (!didHit)
            {
                if (!_noTargetReportedThisUse)
                {
                    PublishWarningMessage(StableText(H8ToolLocHashes.REPAIR_TOOL_HUD_NO_TARGET, "REPAIR TOOL - NO TARGET"));
                    _noTargetReportedThisUse = true;
                }
                UpdateBeamMiss();
                InvalidateDiagnosisCache();
                return;
            }

            if (TryHandleAirlockHit(safeDeltaTime)) return;

            if (TryHandleSubmarineDamageControlHit(safeDeltaTime))
            {
                ClearIntegrityDiagnostic();
                InvalidateDiagnosisCache();
                return;
            }

            CacheRepairTargetsForCollider(_hit.collider, out IRepairableModuleTarget module, out IVoxelRepairWeldTarget voxelRepairTarget);
            if (module != null)
            {
                if (TryHandleModuleRepair(safeDeltaTime, module)) return;
            }
            else
            {
                if (voxelRepairTarget != null)
                {
                    if (TryHandleVoxelRepair(voxelRepairTarget)) return;
                }

                if (!_invalidTargetReportedThisUse)
                {
                    PublishWarningMessage(StableText(H8ToolLocHashes.REPAIR_TOOL_HUD_INVALID_TARGET, "REPAIR TOOL - INVALID TARGET"));
                    _invalidTargetReportedThisUse = true;
                }
                UpdateBeamMiss();
                ClearIntegrityDiagnostic();
            }

            InvalidateDiagnosisCache();
        }

        private bool TryHandleAirlockHit(float safeDeltaTime)
        {
            BaseAirlock airlock = FindRepairAirlock(_hit.collider);
            if (airlock != null && airlock.TryApplyWeldOverride(safeDeltaTime, _hit.point))
            {
                UpdateBeamHit(_hit.point, _hit.normal);
                ClearIntegrityDiagnostic();
                InvalidateDiagnosisCache();
                return true;
            }
            return false;
        }

        private bool TryHandleModuleRepair(float safeDeltaTime, IRepairableModuleTarget module)
        {
            if (!module.TryReadRepairState(out ModuleRepairReadSnapshot beforeState))
            {
                UpdateBeamMiss();
                ClearIntegrityDiagnostic();
                InvalidateDiagnosisCache();
                return true;
            }

            float beforeIntegrity = beforeState.CurrentIntegrity;
            float beforeMaxIntegrity = beforeState.MaxIntegrity;
            bool beforeFlooded = (beforeState.Flags & ModuleRepairReadSnapshot.FlagFlooded) != 0u;

            if (IsIntegrityAtMax(beforeIntegrity, beforeMaxIntegrity) && !beforeFlooded)
            {
                if (!_healthyTargetReportedThisUse)
                {
                    PublishInfoMessage(StableText(H8ToolLocHashes.REPAIR_TOOL_HUD_SEALED, "REPAIR TOOL - MODULE SEALED"));
                    _healthyTargetReportedThisUse = true;
                }

                UpdateBeamHit(_hit.point, _hit.normal);
                PublishIntegrityDiagnostic(module, _hit.point, _hit.normal);
                InvalidateDiagnosisCache();
                return true;
            }

            float repairAmount = ResolveRuntimeRepairPowerPerSecond() * safeDeltaTime;
            ToolEffectEvents.TryRaiseEffectApplied(
                EffectType.Weld,
                module,
                null,
                repairAmount,
                _hit.point);
            module.ApplyRepair(repairAmount);
            UpdateBeamHit(_hit.point, _hit.normal);
            PublishIntegrityDiagnostic(module, _hit.point, _hit.normal);

            if (!_activeRepairReportedThisUse)
            {
                ServiceDiagnosis diagnosis = BuildDiagnosis(module);
                PublishActiveRepairInfo(diagnosis.headline);
                s_hudBuffer.Clear();
                if (TryWriteRepairStartedLogSummary(ref s_hudBuffer, diagnosis))
                {
                    FieldOperationLogSystem.RecordOperation(
                        StableText(H8ToolLocHashes.REPAIR_TOOL_CATEGORY, RepairToolCategory),
                        StableText(H8ToolLocHashes.REPAIR_TOOL_LOG_STARTED_TITLE, "MODULE REPAIR STARTED"),
                        in s_hudBuffer,
                        "INFO");
                }

                _activeRepairReportedThisUse = true;
            }

            if ((IsIntegrityBelowMax(beforeIntegrity, beforeMaxIntegrity) || beforeFlooded) &&
                TryReadModuleRepairState(module, out ModuleRepairReadSnapshot restoredState) &&
                IsModuleIntegrityAtMax(in restoredState) &&
                (restoredState.Flags & ModuleRepairReadSnapshot.FlagFlooded) == 0u)
            {
                PublishInfoMessage(StableText(H8ToolLocHashes.REPAIR_TOOL_HUD_RESTORED, "REPAIR TOOL - MODULE RESTORED"));
                s_hudBuffer.Clear();
                if (TryWriteRepairRestoredLogSummary(ref s_hudBuffer))
                {
                    FieldOperationLogSystem.RecordOperation(
                        StableText(H8ToolLocHashes.REPAIR_TOOL_CATEGORY, RepairToolCategory),
                        StableText(H8ToolLocHashes.REPAIR_TOOL_LOG_RESTORED_TITLE, "MODULE RESTORED"),
                        in s_hudBuffer,
                        "INFO");
                }
            }
            return false;
        }

        private bool TryHandleVoxelRepair(IVoxelRepairWeldTarget voxelRepairTarget)
        {
            bool repairVoxelHit = false;
            if (TryResolveAupFromPlayerPose(_hit.point, out AbsoluteUniversePosition hitAup))
            {
                double3 absoluteHitPoint = hitAup.ToAbsoluteDouble3();
                Vector3 repairDirection = TryResolveRepairRay(out _, out Vector3 poseDirection)
                    ? poseDirection
                    : ResolveRepairForwardFallback();
                repairVoxelHit = math.all(math.isfinite(absoluteHitPoint)) &&
                                 voxelRepairTarget.TryApplyRepairWeldDda(
                                     absoluteHitPoint,
                                     repairDirection,
                                     ResolveRuntimeRepairPowerNormalized(),
                                     ResolveRuntimeRepairRange());
            }

            if (repairVoxelHit)
            {
                UpdateBeamHit(_hit.point, _hit.normal);
                ClearIntegrityDiagnostic();
                QueueToolHapticFeedback(ResolveRuntimeRepairPowerPerSecond(), FiniteAtLeast(repairSpeed, 1f));
                InvalidateDiagnosisCache();
                return true;
            }
            return false;
        }

        public override void UseSecondary(float deltaTime)
        {
            if (_secondaryLatched)
                return;

            _secondaryLatched = true;

            bool didHit = TryGetRepairHit(out _hit);

            if (!didHit)
            {
                PublishWarningMessage(StableText(H8ToolLocHashes.REPAIR_TOOL_HUD_NO_MODULE, "REPAIR TOOL - NO MODULE IN RANGE"));
                InvalidateDiagnosisCache();
                return;
            }

            CacheRepairTargetsForCollider(_hit.collider, out IRepairableModuleTarget module, out _);
            if (module == null)
            {
                PublishWarningMessage(StableText(H8ToolLocHashes.REPAIR_TOOL_HUD_NOT_SERVICEABLE, "REPAIR TOOL - TARGET NOT SERVICEABLE"));
                InvalidateDiagnosisCache();
                return;
            }

            ServiceDiagnosis diagnosis = BuildDiagnosis(module);
            PublishDiagnosis(diagnosis);
            string logTitle = GetServiceDiagnosisLogTitle(diagnosis.headline);
            s_hudBuffer.Clear();
            if (TryWriteDiagnosisLogSummary(ref s_hudBuffer, diagnosis))
            {
                FieldOperationLogSystem.RecordOperation(
                    StableText(H8ToolLocHashes.REPAIR_TOOL_CATEGORY, RepairToolCategory),
                    logTitle,
                    in s_hudBuffer,
                    diagnosis.severity);
            }

            InvalidateDiagnosisCache();
        }

        public override void ToolTick(float deltaTime)
        {
            if (_wasRepairingLastFrame && !_isRepairing)
                SetRepairVisuals(false);

            if (_powerIndicatorRenderer != null)
                QueuePowerIndicatorUpdate();

            _wasRepairingLastFrame = _isRepairing;
            bool repairingThisFrame = _isRepairing;
            _isRepairing = false;

            PlayerInputState inputState = TryGetInputService(out IInputService inputService) && inputService.IsPlayerInputEnabled
                ? inputService.GetState()
                : default;

            if (!inputState.HasAction(PlayerInputAction.PrimaryFire))
            {
                _invalidTargetReportedThisUse = false;
                _noTargetReportedThisUse = false;
                _healthyTargetReportedThisUse = false;
                _activeRepairReportedThisUse = false;
            }

            if (!inputState.HasAction(PlayerInputAction.SecondaryFire))
                _secondaryLatched = false;

            if (!IsEquipped)
            {
                ClearDiagnosticLaserTelemetry();
                AdvanceRepairEvaluationStamps();
                return;
            }

            if (!repairingThisFrame)
                UpdateDiagnosticLaserPreview();

            byte heartbeatFlags = (byte)(RepairBlackBoxFlagEquipped |
                                         (repairingThisFrame ? RepairBlackBoxFlagRepairing : 0));
            RecordRepairBlackBox(ResolveRepairBlackBoxPoint(), 0, 0, 0, heartbeatFlags);
            AdvanceRepairEvaluationStamps();
        }

        public void LateFrameTick()
        {
            if (_powerIndicatorDirty)
            {
                _powerIndicatorDirty = false;
                UpdatePowerIndicator();
            }

            if (_repairVisualStateDirty)
            {
                _repairVisualStateDirty = false;
                ApplyRepairVisuals(_pendingRepairVisualActive);
            }

            if (_beamVisualDirty)
            {
                _beamVisualDirty = false;
                switch (_pendingBeamVisualMode)
                {
                    case RepairBeamVisualMode.Hit:
                        ApplyBeamHit(_pendingBeamHitPoint, _pendingBeamHitNormal);
                        break;
                    case RepairBeamVisualMode.Miss:
                        ApplyBeamMiss();
                        break;
                    case RepairBeamVisualMode.Diagnostic:
                        ApplyDiagnosticLaserPreview();
                        break;
                }

                _pendingBeamVisualMode = RepairBeamVisualMode.None;
            }

            if (_pendingSparkEmit)
            {
                _pendingSparkEmit = false;
                EmitRepairSparkParticles(_pendingSparkQuantity, _pendingSparkQuality01);
            }

            if (!IsEquipped &&
                !_powerIndicatorDirty &&
                !_repairVisualStateDirty &&
                !_beamVisualDirty &&
                !_pendingSparkEmit)
            {
                TryUnregisterLateFrameTick();
            }
        }

        private void QueuePowerIndicatorUpdate()
        {
            _powerIndicatorDirty = true;
            TryRegisterLateFrameTick();
        }

        private void TryRegisterLateFrameTick()
        {
            if (_lateFrameRegistered || !Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            _lateFrameRegistered = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Player);
        }

        private void TryUnregisterLateFrameTick()
        {
            if (!_lateFrameRegistered)
                return;

            GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Player);
            _lateFrameRegistered = false;
        }

        private void ClearPendingRepairVisualSync()
        {
            _powerIndicatorDirty = false;
            _repairVisualStateDirty = false;
            _beamVisualDirty = false;
            _pendingSparkEmit = false;
            _pendingRepairVisualActive = false;
            _pendingBeamVisualMode = RepairBeamVisualMode.None;
        }

        public override string BuildLegacyOperationalSummaryString()
        {
            return RepairToolCategory;
        }

        public override void WriteOperationalSummary(ref FixedCharBuffer buffer)
        {
            if (_isRepairing)
            {
                AppendText(ref buffer, StableText(H8ToolLocHashes.REPAIR_TOOL_OPERATIONAL_ACTIVE, "REPAIR TOOL // ACTIVE SERVICE"));
                return;
            }

            if (TryGetServiceDiagnosisCached(out ServiceDiagnosis diagnosis))
            {
                AppendText(ref buffer, "REPAIR TOOL // ");
                AppendText(ref buffer, diagnosis.priority);
                return;
            }

            AppendText(ref buffer, StableText(H8ToolLocHashes.REPAIR_TOOL_OPERATIONAL_STANDBY, "REPAIR TOOL // STANDBY"));
        }

        public override string BuildLegacyOperationalDirectiveString()
        {
            return "Sweep a damaged module to diagnose or begin repair.";
        }

        public override void WriteOperationalDirective(ref FixedCharBuffer buffer)
        {
            if (_isRepairing)
            {
                AppendText(
                    ref buffer,
                    StableText(
                        H8ToolLocHashes.REPAIR_TOOL_OPERATIONAL_ACTIVE_DIRECTIVE,
                        "Hold the beam steady until the service window closes."));
                return;
            }

            if (TryGetServiceDiagnosisCached(out ServiceDiagnosis diagnosis))
            {
                AppendText(ref buffer, diagnosis.recommendation);
                return;
            }

            AppendText(
                ref buffer,
                StableText(
                    H8ToolLocHashes.REPAIR_TOOL_OPERATIONAL_STANDBY_DIRECTIVE,
                    "Sweep a damaged module to diagnose or begin repair."));
        }

        // ══════════════════════════════════════════════════════════
        //  PRIVATE — VISUAL STATE
        // ══════════════════════════════════════════════════════════

        private void UpdateBeamHit(Vector3 hitPoint, Vector3 hitNormal)
        {
            _pendingBeamHitPoint = hitPoint;
            _pendingBeamHitNormal = hitNormal;
            _pendingBeamVisualMode = RepairBeamVisualMode.Hit;
            _beamVisualDirty = true;
            SetRepairVisuals(true);
            TryRegisterLateFrameTick();
        }

        private void ApplyBeamHit(Vector3 hitPoint, Vector3 hitNormal)
        {
            ApplyRepairVisuals(true);
            Vector3 safeNormal = ResolveFiniteDirection(hitNormal, ResolveRepairForwardFallback());

            if (repairLine != null)
            {
                repairLine.useWorldSpace = true;
                if (TryResolveRepairRay(out Vector3 origin, out _))
                {
                    if (!repairLine.enabled)
                        repairLine.enabled = true;

                    repairLine.SetPosition(0, origin);
                    repairLine.SetPosition(1, hitPoint);
                }
                else
                {
                    repairLine.enabled = false;
                }
            }

            if (sparksVFX != null)
            {
                Transform t = sparksVFX.transform;
                t.position = hitPoint;
                t.rotation = Quaternion.LookRotation(safeNormal);
            }

            if (weldLight != null)
            {
                weldLight.transform.position = hitPoint - safeNormal * 0.05f;
            }

            if (repairLoopAudio != null && !repairLoopAudio.isPlaying)
            {
                TryAssignRepairAudioMixerRoute();
                repairLoopAudio.Play();
            }
        }

        private void UpdateBeamMiss()
        {
            _pendingBeamVisualMode = RepairBeamVisualMode.Miss;
            _beamVisualDirty = true;
            SetRepairVisuals(true);
            TryRegisterLateFrameTick();
        }

        private void ApplyBeamMiss()
        {
            ApplyRepairVisuals(true);

            if (repairLine != null)
            {
                repairLine.useWorldSpace = true;
                if (TryResolveRepairRay(out Vector3 origin, out Vector3 direction))
                {
                    if (!repairLine.enabled)
                        repairLine.enabled = true;

                    repairLine.SetPosition(0, origin);
                    repairLine.SetPosition(1, origin + direction * ResolveRuntimeRepairRange());
                }
                else
                {
                    repairLine.enabled = false;
                }
            }

            if (sparksVFX != null && sparksVFX.isPlaying)
            {
                sparksVFX.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }

            if (weldLight != null)
            {
                if (TryResolveRepairRay(out Vector3 origin, out Vector3 direction))
                    weldLight.transform.position = origin + direction * ResolveRuntimeRepairRange();
                else
                    weldLight.enabled = false;
            }

            if (repairLoopAudio != null && !repairLoopAudio.isPlaying)
            {
                TryAssignRepairAudioMixerRoute();
                repairLoopAudio.Play();
            }
        }

        private void SetRepairVisuals(bool active)
        {
            _pendingRepairVisualActive = active;
            _repairVisualStateDirty = true;
            TryRegisterLateFrameTick();
        }

        private void ApplyRepairVisuals(bool active)
        {
            if (repairLine != null)
                repairLine.enabled = active;

            if (weldLight != null)
                weldLight.enabled = active;

            if (!active)
            {
                if (sparksVFX != null && sparksVFX.isPlaying)
                    sparksVFX.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

                if (repairLoopAudio != null && repairLoopAudio.isPlaying)
                    repairLoopAudio.Stop();
            }
        }

        private void UpdateDiagnosticLaserPreview()
        {
            _pendingBeamVisualMode = RepairBeamVisualMode.Diagnostic;
            _beamVisualDirty = true;
            TryRegisterLateFrameTick();
        }

        private void ApplyDiagnosticLaserPreview()
        {
            if (!TryGetRepairHit(out _hit))
            {
                if (repairLine != null)
                    repairLine.enabled = false;
                ClearIntegrityDiagnostic();
                return;
            }

            if (repairLine != null)
            {
                repairLine.useWorldSpace = true;
                if (TryResolveRepairRay(out Vector3 origin, out _))
                {
                    repairLine.enabled = true;
                    repairLine.SetPosition(0, origin);
                    repairLine.SetPosition(1, _hit.point);
                }
                else
                {
                    repairLine.enabled = false;
                }
            }

            CacheRepairTargetsForCollider(_hit.collider, out IRepairableModuleTarget module, out _);
            if (module != null)
                PublishIntegrityDiagnostic(module, _hit.point, _hit.normal);
            else
                ClearIntegrityDiagnostic();
        }

        private void ClearDiagnosticLaserTelemetry()
        {
            if (!_isRepairing)
                SetRepairVisuals(false);
            ClearIntegrityDiagnostic();
        }

        private void CacheRepairTargetsForCollider(Collider collider, out IRepairableModuleTarget module, out IVoxelRepairWeldTarget voxelRepairTarget)
        {
            module = null;
            voxelRepairTarget = null;
            if (collider == null)
                return;

            if (!ReferenceEquals(_cachedServiceTargetCollider, collider))
            {
                _cachedServiceTargetCollider = collider;
                _cachedServiceTargetModule = null;
                _cachedServiceTargetVoxelRepairTarget = null;

                TryResolveCachedRepairModule(collider, out _cachedServiceTargetModule);

                if (_cachedServiceTargetModule == null &&
                    !TryResolveCachedVoxelRepairTarget(collider, out _cachedServiceTargetVoxelRepairTarget))
                {
                    _cachedServiceTargetVoxelRepairTarget = null;
                }
            }

            module = _cachedServiceTargetModule;
            voxelRepairTarget = _cachedServiceTargetVoxelRepairTarget;
        }

        private BaseAirlock FindRepairAirlock(Collider collider)
        {
            if (collider == null)
                return null;

            if (!ReferenceEquals(_cachedAirlockTargetCollider, collider))
            {
                _cachedAirlockTargetCollider = collider;
                _cachedServiceTargetAirlock = null;

                TryResolveCachedRepairAirlock(collider, out _cachedServiceTargetAirlock);
            }

            return _cachedServiceTargetAirlock;
        }

        private static bool TryResolveCachedRepairModule(Collider collider, out IRepairableModuleTarget module)
        {
            module = null;
            if (collider == null ||
                !InteractableRegistry.TryResolve(collider, out InteractableRegistry.TargetInfo targetInfo))
            {
                return false;
            }

            module = targetInfo.RepairableModuleTarget;
            if (module == null && targetInfo.BaseModule != null)
                module = targetInfo.BaseModule as IRepairableModuleTarget;

            return module != null;
        }

        private static bool TryResolveCachedVoxelRepairTarget(Collider collider, out IVoxelRepairWeldTarget voxelRepairTarget)
        {
            voxelRepairTarget = null;
            if (collider == null ||
                !InteractableRegistry.TryResolve(collider, out InteractableRegistry.TargetInfo targetInfo))
            {
                return false;
            }

            voxelRepairTarget = targetInfo.VoxelRepairWeldTarget;
            return voxelRepairTarget != null;
        }

        private static bool TryResolveCachedRepairAirlock(Collider collider, out BaseAirlock airlock)
        {
            airlock = null;
            if (collider == null ||
                !InteractableRegistry.TryResolve(collider, out InteractableRegistry.TargetInfo targetInfo))
            {
                return false;
            }

            airlock = targetInfo.Interactable as BaseAirlock;
            return airlock != null;
        }

        private bool TryHandleSubmarineDamageControlHit(float deltaTime)
        {
            ISubmarineDamageControlTarget damageTarget = CacheSubmarineDamageControlTargetForCollider(_hit.collider);
            if (damageTarget == null)
                return false;

            float safeDeltaTime = FiniteNonNegativeOrZero(deltaTime);
            if (safeDeltaTime <= 0f)
                return false;

            float repairPowerPerSecond = ResolveRuntimeRepairPowerPerSecond();
            float intensity01 = ResolveRuntimeRepairPowerNormalized();
            int repairRoomId = -1;
            ISubmarineRepairRoomResolver roomResolver = _cachedSubmarineRepairRoomResolver;
            if (roomResolver != null)
                roomResolver.TryResolveRepairRoom(_hit.point, out repairRoomId);

            bool dentChanged = TryRepairVaultHullDents(
                _hit.point,
                safeDeltaTime,
                repairPowerPerSecond,
                intensity01,
                _cachedSubmarineDamageTargetTransform,
                repairRoomId,
                out _,
                out _);
            bool breachRepairQueued = damageTarget.TryQueueRepairHit(_hit.point, safeDeltaTime, repairPowerPerSecond, intensity01);
            if (!dentChanged && !breachRepairQueued)
                return false;

            UpdateBeamHit(_hit.point, _hit.normal);
            PublishRepairSparkSignal(_hit.point, intensity01);
            QueueToolHapticFeedback(repairPowerPerSecond, FiniteAtLeast(repairSpeed, 1f));

            if (!_activeRepairReportedThisUse)
            {
                PublishActiveRepairInfo(RepairToolPatchingHeadline);
                _activeRepairReportedThisUse = true;
            }

            return true;
        }

        private ISubmarineDamageControlTarget CacheSubmarineDamageControlTargetForCollider(Collider collider)
        {
            if (collider == null)
                return null;

            if (!ReferenceEquals(_cachedSubmarineDamageTargetCollider, collider))
            {
                _cachedSubmarineDamageTargetCollider = collider;
                _cachedSubmarineDamageTarget = null;
                _cachedSubmarineRepairRoomResolver = null;
                _cachedSubmarineDamageTargetTransform = null;

                TryResolveCachedSubmarineDamageTarget(
                    collider,
                    out _cachedSubmarineDamageTarget,
                    out _cachedSubmarineRepairRoomResolver,
                    out _cachedSubmarineDamageTargetTransform);
            }

            return _cachedSubmarineDamageTarget;
        }

        private static bool TryResolveCachedSubmarineDamageTarget(
            Collider collider,
            out ISubmarineDamageControlTarget damageTarget,
            out ISubmarineRepairRoomResolver roomResolver,
            out Transform targetTransform)
        {
            damageTarget = null;
            roomResolver = null;
            targetTransform = null;
            if (collider == null ||
                !InteractableRegistry.TryResolve(collider, out InteractableRegistry.TargetInfo targetInfo) ||
                targetInfo.SubmarineDamageControlTarget == null)
            {
                return false;
            }

            damageTarget = targetInfo.SubmarineDamageControlTarget;
            roomResolver = targetInfo.SubmarineRepairRoomResolver;
            Component component = damageTarget as Component;
            targetTransform = component != null ? component.transform : collider.transform;
            return true;
        }

        private void PublishRepairSparkSignal(Vector3 worldPoint, float intensity01)
        {
            if (!IsFiniteVector(worldPoint))
                return;

            float safeIntensity01 = math.isfinite(intensity01) ? math.saturate(intensity01) : 0f;
            float quality01 = ResolveRepairQualityWeight();
            ushort sparkQuantity = ResolveRepairSparkQuantity(safeIntensity01, quality01);
            byte flags = (byte)(DebrisSpawnSignal.FlagToolSparks | DebrisSpawnSignal.FlagComputeShard);

            if (!TryResolveAupFromPlayerPose(worldPoint, out AbsoluteUniversePosition sparkAup))
                return;

            DebrisSpawnSignal signal = new DebrisSpawnSignal
            {
                PositionAup = sparkAup,
                SpeciesHash = RepairSparksSignalHash,
                SourceEntityId = unchecked((uint)EntityId.ToULong(gameObject.GetEntityId())),
                Intensity01 = safeIntensity01,
                DebrisKind = RepairSparkDebrisKind,
                Flags = flags,
                Quantity = sparkQuantity
            };
            SignalBus<DebrisSpawnSignal>.TryPushTracked(in signal, ref s_x001RepairToolSignalPushDropCount);
            QueueRepairSparkParticles(sparkQuantity, quality01);
        }

        private void QueueRepairSparkParticles(ushort sparkQuantity, float quality01)
        {
            _pendingSparkQuantity = sparkQuantity;
            _pendingSparkQuality01 = quality01;
            _pendingSparkEmit = true;
            TryRegisterLateFrameTick();
        }

        private static ushort ResolveRepairSparkQuantity(float intensity01, float quality01)
        {
            float qualityCurve = ResolveRepairQualityCurve(quality01);
            int min = (int)math.round(math.lerp(2f, 8f, qualityCurve));
            int max = (int)math.round(math.lerp(6f, 32f, qualityCurve));
            return (ushort)math.clamp((int)math.round(math.lerp(min, max, math.saturate(intensity01))), 1, 64);
        }

        private void EmitRepairSparkParticles(ushort sparkQuantity, float quality01)
        {
            if (sparksVFX == null || sparkQuantity == 0)
                return;

            float qualityCurve = ResolveRepairQualityCurve(quality01);
            int localCap = (int)math.round(math.lerp(6f, 16f, qualityCurve));
            sparksVFX.Emit(math.clamp((int)sparkQuantity, 1, localCap));
        }

        private static float ResolveRepairQualityWeight()
        {
            float quality = HomeostasisBrain.GlobalQualityWeight;
            return math.isfinite(quality) ? math.saturate(quality) : 0.5f;
        }

        private static float ResolveRepairQualityCurve(float quality01)
        {
            return math.smoothstep(0f, 1f, math.saturate(quality01));
        }

        private static byte ResolveRepairQualityWeightByte()
        {
            float quality = ResolveRepairQualityWeight();
            return (byte)math.clamp((int)math.round(quality * 255f), 0, 255);
        }

        private bool TryRepairVaultHullDents(
            Vector3 worldPoint,
            float deltaTime,
            float repairPowerPerSecond,
            float intensity01,
            Transform submarineRoot,
            int roomId,
            out int touchedDentCount,
            out int repairedDentCount)
        {
            touchedDentCount = 0;
            repairedDentCount = 0;
            float safeDeltaTime = FiniteNonNegativeOrZero(deltaTime);
            float safeRepairPowerPerSecond = FiniteNonNegativeOrZero(repairPowerPerSecond);
            float safeIntensity01 = math.isfinite(intensity01) ? math.saturate(intensity01) : 0f;
            float3 localPoint = default;
            bool invalidRepairMath = submarineRoot != null &&
                                     (!IsFiniteVector(worldPoint) || !IsFiniteQuaternion(submarineRoot.rotation));
            bool localHitResolved = submarineRoot != null &&
                                     !invalidRepairMath &&
                                     TryProjectSubmarineLocalHit(submarineRoot, worldPoint, out localPoint);
            if (submarineRoot == null ||
                safeDeltaTime <= 0f ||
                safeRepairPowerPerSecond <= 0f ||
                !localHitResolved)
            {
                if (invalidRepairMath)
                {
                    RecordRepairBlackBox(
                        worldPoint,
                        0,
                        0,
                        0,
                        (byte)(RepairBlackBoxFlagEquipped | RepairBlackBoxFlagRepairing | RepairBlackBoxFlagInvalidMath));
                }

                return false;
            }

            IDataVault vault = ResolveDataVault();
            if (vault == null)
                return false;

            if (!EnsureHullDentsHandle(vault))
                return false;

            float repairDelta = safeDeltaTime *
                                safeRepairPowerPerSecond *
                                HullDentRepairDepthScale *
                                math.max(0.1f, safeIntensity01);
            if (repairDelta <= 0f || !math.isfinite(repairDelta))
                return false;

            if (!vault.TryAcquireMutationGuard(HullDentsMutationGuardMask))
                return false;

            bool changed = false;
            bool invalidMathDetected = false;
            ushort repairedDentMask = 0;
            int activeDentCount = 0;
            try
            {
                if (!TryResolveHullDents(vault, out NativeArray<float4> dents, allowEnsure: false))
                    return false;

                int count = math.min(HullDentVaultCapacity, dents.Length);
                for (int dentIndex = 0; dentIndex < count; dentIndex++)
                {
                    float4 dent = dents[dentIndex];
                    if (!math.all(math.isfinite(dent)))
                    {
                        dents[dentIndex] = default;
                        changed = true;
                        invalidMathDetected = true;
                        continue;
                    }

                    float depth = UnpackHullDentDepth(dent.w);
                    if (depth <= MinimumStoredHullDentDepthMeters)
                    {
                        if (dent.w < 0f || !math.isfinite(dent.w))
                        {
                            dent.w = 0f;
                            dents[dentIndex] = dent;
                            changed = true;
                        }

                        continue;
                    }

                    activeDentCount++;
                    float3 dentPoint = new float3(dent.x, dent.y, dent.z);
                    float3 dentDelta = dentPoint - localPoint;
                    if (math.lengthsq(dentDelta) > HullDentRepairRadiusSq)
                        continue;

                    touchedDentCount++;
                    float radius = UnpackHullDentRadius(dent.w);
                    float repairedDepth = math.max(0f, depth - repairDelta);
                    float repairedPacked = repairedDepth <= MinimumStoredHullDentDepthMeters
                        ? 0f
                        : PackHullDentRadiusDepth(radius, repairedDepth);

                    if (math.abs(repairedPacked - dent.w) <= HullRepairEpsilon)
                        continue;

                    dent.w = math.max(0f, repairedPacked);
                    dents[dentIndex] = dent;
                    changed = true;

                    if (repairedDepth <= MinimumStoredHullDentDepthMeters)
                    {
                        repairedDentCount++;
                        activeDentCount = math.max(0, activeDentCount - 1);
                        repairedDentMask |= (ushort)(1 << dentIndex);
                    }
                }
            }
            finally
            {
                vault.ReleaseMutationGuard(HullDentsMutationGuardMask);
            }

            if (repairedDentMask != 0)
                PublishHullRepairedSignals(worldPoint, roomId, repairedDentMask);

            if (changed || repairedDentCount > 0)
                CrashTelemetryBuffer.ReportHullDentState(HullRepairTelemetryHash, activeDentCount, BuildHullRepairTelemetryFlags(touchedDentCount, repairedDentCount));

            byte blackBoxFlags = (byte)(RepairBlackBoxFlagEquipped | RepairBlackBoxFlagRepairing);
            if (changed)
                blackBoxFlags |= RepairBlackBoxFlagVaultChanged;
            if (touchedDentCount > 0)
                blackBoxFlags |= RepairBlackBoxFlagDentTouched;
            if (repairedDentCount > 0)
                blackBoxFlags |= RepairBlackBoxFlagDentRepaired;
            if (invalidMathDetected)
                blackBoxFlags |= RepairBlackBoxFlagInvalidMath;
            RecordRepairBlackBox(worldPoint, activeDentCount, touchedDentCount, repairedDentCount, blackBoxFlags);

            return changed;
        }

        private IDataVault ResolveDataVault()
        {
            return _dataVault;
        }

        private void CacheRepairVaultCold()
        {
            RebindRepairVault(GlobalRegistry.DataVault);
        }

        private void RebindRepairVault(IDataVault vault)
        {
            if (ReferenceEquals(_dataVault, vault))
                return;

            ReleaseVaultState();
            _dataVault = vault;
            if (_dataVault != null)
            {
                EnsureHullDentsHandle(_dataVault, allowBorrow: true);
                EnsureRepairBlackBoxHandle(_dataVault, createIfMissing: true);
            }
        }

        private bool EnsureHullDentsHandle(IDataVault vault, bool allowBorrow = false)
        {
            if (vault == null)
                return false;

            if (vault.IsCompactionFenceActive)
            {
                ClearHullDentsDescriptor();
                return false;
            }

            if (IsHullDentsHandle(in _hullDentsHandle) &&
                vault.TryResolveHandle(in _hullDentsHandle, out NativeArray<float4> currentDents) &&
                currentDents.IsCreated &&
                currentDents.Length >= HullDentVaultCapacity)
            {
                return true;
            }

            ClearHullDentsDescriptor();
            if (!allowBorrow)
                return false;

            if (!vault.TryGetGenerationHandle(BufferID.HullDents, out VaultGenerationHandle<float4> borrowed) ||
                !IsHullDentsHandle(in borrowed) ||
                !vault.TryResolveHandle(in borrowed, out NativeArray<float4> dents) ||
                !dents.IsCreated ||
                dents.Length < HullDentVaultCapacity)
            {
                return false;
            }

            _hullDentsHandle = borrowed;
            return true;
        }

        private bool EnsureRepairBlackBoxHandle(IDataVault vault, bool createIfMissing = false)
        {
            if (vault == null)
                return false;

            if (vault.IsCompactionFenceActive)
            {
                return false;
            }

            if (IsRepairBlackBoxHandle(in _repairBlackBoxHandle) &&
                vault.TryResolveHandle(in _repairBlackBoxHandle, out NativeArray<RepairToolBlackBoxEntry> currentBlackBox) &&
                currentBlackBox.IsCreated &&
                currentBlackBox.Length >= RepairBlackBoxFrameCount)
            {
                return true;
            }

            if (!createIfMissing)
                return false;

            ClearRepairBlackBoxDescriptor();
            if (vault.TryGetGenerationHandle(BufferID.RepairToolBlackBox, out VaultGenerationHandle<RepairToolBlackBoxEntry> existing) &&
                IsRepairBlackBoxHandle(in existing) &&
                vault.TryResolveHandle(in existing, out NativeArray<RepairToolBlackBoxEntry> existingBlackBox) &&
                existingBlackBox.IsCreated &&
                existingBlackBox.Length >= RepairBlackBoxFrameCount)
            {
                _repairBlackBoxHandle = existing;
                _ownsRepairBlackBoxBuffer = false;
                return true;
            }

            if (vault.IsAllocationLocked)
                return false;

            VaultGenerationHandle<RepairToolBlackBoxEntry> acquired = vault.EnsureGenerationHandle<RepairToolBlackBoxEntry>(
                BufferID.RepairToolBlackBox,
                RepairBlackBoxFrameCount,
                SystemID.GameplayTools,
                NativeArrayOptions.ClearMemory);
            if (!IsRepairBlackBoxHandle(in acquired) ||
                !vault.TryResolveHandle(in acquired, out NativeArray<RepairToolBlackBoxEntry> acquiredBlackBox) ||
                !acquiredBlackBox.IsCreated ||
                acquiredBlackBox.Length < RepairBlackBoxFrameCount)
            {
                bool ownsAcquired = true;
                ReleaseVaultBuffer(vault, ref acquired, ref ownsAcquired);
                ClearRepairBlackBoxDescriptor();
                return false;
            }

            _repairBlackBoxHandle = acquired;
            _ownsRepairBlackBoxBuffer = true;
            return true;
        }

        private bool TryResolveHullDents(IDataVault vault, out NativeArray<float4> dents, bool allowEnsure)
        {
            dents = default;
            if (vault == null)
                return false;

            if (allowEnsure && !EnsureHullDentsHandle(vault, allowBorrow: true))
                return false;

            if (!IsHullDentsHandle(in _hullDentsHandle))
                return false;

            if (!vault.TryResolveHandle(in _hullDentsHandle, out dents) ||
                !dents.IsCreated ||
                dents.Length < HullDentVaultCapacity)
            {
                if (allowEnsure)
                    ClearHullDentsDescriptor();

                return false;
            }

            return true;
        }

        private bool TryResolveRepairBlackBox(
            IDataVault vault,
            out NativeArray<RepairToolBlackBoxEntry> blackBox,
            bool allowEnsure)
        {
            blackBox = default;
            if (vault == null)
                return false;

            if (allowEnsure && !EnsureRepairBlackBoxHandle(vault, createIfMissing: true))
                return false;

            if (!IsRepairBlackBoxHandle(in _repairBlackBoxHandle))
                return false;

            if (!vault.TryResolveHandle(in _repairBlackBoxHandle, out blackBox) ||
                !blackBox.IsCreated ||
                blackBox.Length < RepairBlackBoxFrameCount)
            {
                if (allowEnsure)
                    ClearRepairBlackBoxDescriptor();

                return false;
            }

            return true;
        }

        private bool TryReadOnlyRepairBlackBox(
            IDataVault vault,
            out NativeArray<RepairToolBlackBoxEntry>.ReadOnly blackBox)
        {
            blackBox = default;
            return vault != null &&
                   IsRepairBlackBoxHandle(in _repairBlackBoxHandle) &&
                   vault.TryReadOnlyHandle(in _repairBlackBoxHandle, out blackBox) &&
                   blackBox.IsCreated &&
                   blackBox.Length >= RepairBlackBoxFrameCount;
        }

        private void ReleaseVaultState()
        {
            FlushPendingRepairBlackBoxDump();
            ClearHullDentsDescriptor();
            ReleaseRepairBlackBoxBuffer();
            _dataVault = null;
        }

        private void ClearHullDentsDescriptor()
        {
            _hullDentsHandle = default;
        }

        private void ClearRepairBlackBoxDescriptor()
        {
            _repairBlackBoxHandle = default;
            _ownsRepairBlackBoxBuffer = false;
        }

        private void ReleaseRepairBlackBoxBuffer()
        {
            ReleaseVaultBuffer(_dataVault, ref _repairBlackBoxHandle, ref _ownsRepairBlackBoxBuffer);
        }

        private static bool IsVaultHandleCreated<T>(in VaultGenerationHandle<T> handle) where T : struct
        {
            return handle.BufferID != 0u && handle.Generation != 0u;
        }

        private static bool IsHullDentsHandle(in VaultGenerationHandle<float4> handle)
        {
            return handle.BufferID == unchecked((uint)(int)BufferID.HullDents) &&
                   handle.SystemID == (uint)SystemID.Vfx &&
                   handle.Generation != 0u;
        }

        private static bool IsRepairBlackBoxHandle(in VaultGenerationHandle<RepairToolBlackBoxEntry> handle)
        {
            return handle.BufferID == unchecked((uint)(int)BufferID.RepairToolBlackBox) &&
                   handle.SystemID == (uint)SystemID.GameplayTools &&
                   handle.Generation != 0u;
        }

        private static ulong MutationGuardBit(BufferID bufferId)
        {
            return 1UL << ((int)bufferId & 31);
        }

        private static void ReleaseVaultBuffer<T>(
            IDataVault vault,
            ref VaultGenerationHandle<T> handle,
            ref bool ownsBuffer) where T : struct
        {
            if (ownsBuffer && vault != null && IsVaultHandleCreated(in handle))
                vault.ReleaseBuffer(in handle);

            handle = default;
            ownsBuffer = false;
        }

        private Vector3 ResolveRepairBlackBoxPoint()
        {
            if (IsFiniteVector(_hit.point))
                return _hit.point;

            if (TryResolveRepairRay(out Vector3 origin, out _))
                return origin;

            return Vector3.zero;
        }

        private void RecordRepairBlackBox(
            Vector3 worldPoint,
            int activeDentCount,
            int touchedDentCount,
            int repairedDentCount,
            byte flags)
        {
            IDataVault vault = ResolveDataVault();
            if (vault == null || !EnsureRepairBlackBoxHandle(vault))
                return;

            uint frame = NextRepairBlackBoxFrame();
            bool invalid = !IsFiniteVector(worldPoint);
            AbsoluteUniversePosition hitAup = default;
            if (!invalid && !TryResolveAupFromPlayerPose(worldPoint, out hitAup))
            {
                invalid = true;
            }

            if (invalid)
                flags |= RepairBlackBoxFlagInvalidMath;

            int index = (int)(frame % RepairBlackBoxFrameCount);
            byte battery255 = (byte)math.clamp(
                (int)math.round(math.saturate(ResolveModularBatteryNormalized()) * 255f),
                0,
                255);
            RepairToolBlackBoxEntry entry = default;
            entry.HitAup = invalid ? default : hitAup;
            entry.Frame = frame;
            entry.StateHash = BuildRepairBlackBoxStateHash(activeDentCount, touchedDentCount, repairedDentCount, flags);
            entry.ActiveDentCount = (ushort)math.clamp(activeDentCount, 0, ushort.MaxValue);
            entry.TouchedDentCount = (ushort)math.clamp(touchedDentCount, 0, ushort.MaxValue);
            entry.RepairedDentCount = (byte)math.clamp(repairedDentCount, 0, byte.MaxValue);
            entry.Battery255 = battery255;
            entry.Flags = flags;
            entry.Reserved0 = 0;

            if (!vault.TryAcquireMutationGuard(RepairBlackBoxMutationGuardMask))
            {
                GlobalTelemetryBus.PublishUnityLogFault(
                    RepairBlackBoxDumpFaultHash,
                    unchecked((uint)(int)BufferID.RepairToolBlackBox),
                    2u);
                return;
            }

            try
            {
                if (!TryResolveRepairBlackBox(vault, out NativeArray<RepairToolBlackBoxEntry> blackBox, allowEnsure: false))
                    return;

                blackBox[index] = entry;
            }
            finally
            {
                vault.ReleaseMutationGuard(RepairBlackBoxMutationGuardMask);
            }

            if ((flags & RepairBlackBoxFlagInvalidMath) == 0)
            {
                _repairBlackBoxDumpedThisFault = false;
                return;
            }

            if (_repairBlackBoxDumpedThisFault)
                return;

            _repairBlackBoxDumpedThisFault = true;
            _repairBlackBoxDumpPending = true;
        }

        private void FlushPendingRepairBlackBoxDump()
        {
            if (!_repairBlackBoxDumpPending)
                return;

            if (DumpRepairBlackBox(_dataVault))
                _repairBlackBoxDumpPending = false;
        }

        private bool DumpRepairBlackBox(IDataVault vault)
        {
            if (vault == null || !EnsureRepairBlackBoxHandle(vault))
                return false;
            if (System.Threading.Interlocked.CompareExchange(ref _repairBlackBoxDumpInFlight, 1, 0) != 0)
                return true;

            bool queued = false;
            try
            {
                if (!TryReadOnlyRepairBlackBox(vault, out NativeArray<RepairToolBlackBoxEntry>.ReadOnly blackBox))
                    return false;

                int entrySize = UnsafeUtility.SizeOf<RepairToolBlackBoxEntry>();
                if (entrySize != RepairBlackBoxEntrySizeBytes)
                    return false;

                for (int i = 0; i < RepairBlackBoxFrameCount; i++)
                    _repairBlackBoxDumpSnapshot[i] = blackBox[i];

                _repairBlackBoxDumpFrame = _repairBlackBoxFrame;
                if (!System.Threading.ThreadPool.QueueUserWorkItem(RepairBlackBoxDumpWorkerCallback, this))
                {
                    GlobalTelemetryBus.PublishUnityLogFault(RepairBlackBoxDumpFaultHash, 0u, 2u);
                    return false;
                }

                queued = true;
                return true;
            }
            catch (Exception)
            {
                GlobalTelemetryBus.PublishUnityLogFault(RepairBlackBoxDumpFaultHash, 0u, 1u);
                return false;
            }
            finally
            {
                if (!queued)
                    System.Threading.Volatile.Write(ref _repairBlackBoxDumpInFlight, 0);
            }
        }

        private static void RunRepairBlackBoxDumpWorker(object state)
        {
            if (state is RepairTool repairTool)
                repairTool.WriteRepairBlackBoxDumpWorker();
        }

        private void WriteRepairBlackBoxDumpWorker()
        {
            try
            {
                WriteRepairBlackBoxSnapshotCold(_repairBlackBoxDumpSnapshot, _repairBlackBoxDumpFrame);
            }
            catch (Exception)
            {
                GlobalTelemetryBus.PublishUnityLogFault(RepairBlackBoxDumpFaultHash, 0u, 1u);
            }
            finally
            {
                System.Threading.Volatile.Write(ref _repairBlackBoxDumpInFlight, 0);
            }
        }

        private static void WriteRepairBlackBoxSnapshotCold(RepairToolBlackBoxEntry[] snapshot, uint frame)
        {
            int entrySize = UnsafeUtility.SizeOf<RepairToolBlackBoxEntry>();
            string path = Path.Combine(Application.dataPath, "..", RepairBlackBoxDumpPath);
            const int HeaderBytes = 12;
            const int RowBytes = 64;
            Span<byte> payload = stackalloc byte[HeaderBytes + RepairBlackBoxFrameCount * RowBytes];
            WriteInt32LittleEndian(payload, 0, RepairBlackBoxFrameCount);
            WriteInt32LittleEndian(payload, 4, entrySize);
            WriteUInt32LittleEndian(payload, 8, frame);
            int offset = HeaderBytes;
            for (int i = 0; i < RepairBlackBoxFrameCount; i++)
            {
                RepairToolBlackBoxEntry entry = snapshot[i];
                WriteInt64LittleEndian(payload, offset, entry.HitAup.GridX);
                WriteInt64LittleEndian(payload, offset + 8, entry.HitAup.GridY);
                WriteInt64LittleEndian(payload, offset + 16, entry.HitAup.GridZ);
                WriteFloat32LittleEndian(payload, offset + 24, entry.HitAup.LocalX);
                WriteFloat32LittleEndian(payload, offset + 28, entry.HitAup.LocalY);
                WriteFloat32LittleEndian(payload, offset + 32, entry.HitAup.LocalZ);
                WriteFloat32LittleEndian(payload, offset + 36, 0f);
                WriteUInt64LittleEndian(payload, offset + 40, 0UL);
                WriteUInt32LittleEndian(payload, offset + 48, entry.Frame);
                WriteUInt32LittleEndian(payload, offset + 52, entry.StateHash);
                WriteUInt16LittleEndian(payload, offset + 56, entry.ActiveDentCount);
                WriteUInt16LittleEndian(payload, offset + 58, entry.TouchedDentCount);
                payload[offset + 60] = entry.RepairedDentCount;
                payload[offset + 61] = entry.Battery255;
                payload[offset + 62] = entry.Flags;
                payload[offset + 63] = entry.Reserved0;
                offset += RowBytes;
            }

            NativeFaultDumpWriter.TryWriteAll(path, payload, payload.Length);
        }

        private static void WriteFloat32LittleEndian(Span<byte> destination, int offset, float value)
        {
            WriteUInt32LittleEndian(destination, offset, math.asuint(value));
        }

        private static void WriteInt32LittleEndian(Span<byte> destination, int offset, int value)
        {
            WriteUInt32LittleEndian(destination, offset, unchecked((uint)value));
        }

        private static void WriteInt64LittleEndian(Span<byte> destination, int offset, long value)
        {
            WriteUInt64LittleEndian(destination, offset, unchecked((ulong)value));
        }

        private static void WriteUInt64LittleEndian(Span<byte> destination, int offset, ulong value)
        {
            WriteUInt32LittleEndian(destination, offset, unchecked((uint)value));
            WriteUInt32LittleEndian(destination, offset + 4, unchecked((uint)(value >> 32)));
        }

        private static void WriteUInt32LittleEndian(Span<byte> destination, int offset, uint value)
        {
            destination[offset] = (byte)value;
            destination[offset + 1] = (byte)(value >> 8);
            destination[offset + 2] = (byte)(value >> 16);
            destination[offset + 3] = (byte)(value >> 24);
        }

        private static void WriteUInt16LittleEndian(Span<byte> destination, int offset, ushort value)
        {
            destination[offset] = (byte)value;
            destination[offset + 1] = (byte)(value >> 8);
        }

        private static uint BuildRepairBlackBoxStateHash(int activeDentCount, int touchedDentCount, int repairedDentCount, byte flags)
        {
            unchecked
            {
                uint hash = 2166136261u;
                hash = (hash ^ HullRepairTelemetryHash) * 16777619u;
                hash = (hash ^ (uint)math.clamp(activeDentCount, 0, ushort.MaxValue)) * 16777619u;
                hash = (hash ^ (uint)math.clamp(touchedDentCount, 0, ushort.MaxValue)) * 16777619u;
                hash = (hash ^ (uint)math.clamp(repairedDentCount, 0, byte.MaxValue)) * 16777619u;
                hash = (hash ^ flags) * 16777619u;
                return hash;
            }
        }

        private static bool TryProjectSubmarineLocalHit(Transform submarineRoot, Vector3 worldPoint, out float3 localPoint)
        {
            localPoint = default;
            if (submarineRoot == null || !IsFiniteVector(worldPoint))
                return false;
            if (!IsFiniteQuaternion(submarineRoot.rotation))
                return false;

            Vector3 rootPosition = submarineRoot.position;
            double3 relativeWorldDouble = new double3(
                (double)worldPoint.x - rootPosition.x,
                (double)worldPoint.y - rootPosition.y,
                (double)worldPoint.z - rootPosition.z);
            if (!math.all(math.isfinite(relativeWorldDouble)))
                return false;

            Vector3 relativeWorld = new Vector3(
                (float)relativeWorldDouble.x,
                (float)relativeWorldDouble.y,
                (float)relativeWorldDouble.z);
            if (!IsFiniteVector(relativeWorld))
                return false;

            Vector3 localVector = Quaternion.Inverse(submarineRoot.rotation) * relativeWorld;
            Vector3 lossyScale = submarineRoot.lossyScale;
            localVector.x /= ResolveSafeScale(lossyScale.x);
            localVector.y /= ResolveSafeScale(lossyScale.y);
            localVector.z /= ResolveSafeScale(lossyScale.z);
            if (!IsFiniteVector(localVector))
                return false;

            localPoint = new float3(localVector.x, localVector.y, localVector.z);
            return math.all(math.isfinite(localPoint));
        }

        private void PublishHullRepairedSignal(Vector3 worldPoint, int roomId, int dentIndex, int repairedDentCount)
        {
            if (!IsFiniteVector(worldPoint))
                return;

            if (!TryResolveAupFromPlayerPose(worldPoint, out AbsoluteUniversePosition hitAup))
                return;

            float quality01 = ResolveRepairQualityWeight();
            byte qualityWeightQ8 = ResolveRepairQualityWeightByte();
            byte flags = HullRepairedSignal.CompletedFlag;

            HullRepairedSignal signal = new HullRepairedSignal
            {
                HitAup = hitAup,
                RoomId = roomId,
                SourceHash = HullRepairSourceHash,
                Frame = NextHullRepairSignalFrame(),
                DentIndex = (byte)math.clamp(dentIndex, 0, 255),
                DentsRepairedCount = (byte)math.clamp(repairedDentCount, 0, 255),
                QualityWeightQ8 = qualityWeightQ8,
                Flags = flags
            };
            SignalBus<HullRepairedSignal>.TryPushTracked(in signal, ref s_x001RepairToolSignalPushDropCount);
        }

        private void PublishHullRepairedSignals(Vector3 worldPoint, int roomId, ushort repairedDentMask)
        {
            if (repairedDentMask == 0 || !IsFiniteVector(worldPoint))
                return;

            int repairedCount = 0;
            for (int dentIndex = 0; dentIndex < HullDentVaultCapacity; dentIndex++)
            {
                int bit = 1 << dentIndex;
                if ((repairedDentMask & bit) == 0)
                    continue;

                repairedCount++;
                PublishHullRepairedSignal(worldPoint, roomId, dentIndex, repairedCount);
            }
        }

        private static uint BuildHullRepairTelemetryFlags(int touchedDentCount, int repairedDentCount)
        {
            uint touched = (uint)math.clamp(touchedDentCount, 0, 255);
            uint repaired = (uint)math.clamp(repairedDentCount, 0, 255);
            return ResolveRepairQualityWeightByte() |
                   (touched << 8) |
                   (repaired << 16);
        }

        private static uint NextHullRepairSignalFrame()
        {
            unchecked
            {
                s_hullRepairSignalFrame++;
                return s_hullRepairSignalFrame;
            }
        }

        private static float PackHullDentRadiusDepth(float radius, float depth)
        {
            float safeRadius = math.min(FiniteNonNegativeOrZero(radius), 15.9375f);
            float safeDepth = math.saturate(FiniteNonNegativeOrZero(depth));
            int radiusQ = Mathf.Clamp(
                Mathf.RoundToInt(safeRadius * HullDentRadiusQuantizationStepsPerMeter),
                0,
                255);
            int depthQ = Mathf.Clamp(Mathf.RoundToInt(safeDepth * 255f), 0, 255);
            return (depthQ << 8) | radiusQ;
        }

        private static float UnpackHullDentRadius(float packed)
        {
            int packedInt = Mathf.Max(0, Mathf.RoundToInt(SanitizePackedHullDentValue(packed)));
            return (packedInt & 255) * InvHullDentRadiusQuantizationStepsPerMeter;
        }

        private static float UnpackHullDentDepth(float packed)
        {
            int packedInt = Mathf.Max(0, Mathf.RoundToInt(SanitizePackedHullDentValue(packed)));
            return ((packedInt >> 8) & 255) * InvHullDentDepthQuantizationSteps;
        }

        private static bool IsFiniteVector(Vector3 value)
        {
            return math.isfinite(value.x) && math.isfinite(value.y) && math.isfinite(value.z);
        }

        private bool TryResolveAupFromPlayerPose(Vector3 runtimePosition, out AbsoluteUniversePosition positionAup)
        {
            positionAup = default;
            if (!IsFiniteVector(runtimePosition))
                return false;

            if (!TryGetPlayerRuntimeContext(out IPlayerRuntimeContext playerContext) ||
                !playerContext.TryGetPlayerPoseSnapshot(out PlayerRuntimePoseSnapshot snapshot) ||
                !snapshot.Aup.IsFinite() ||
                !math.all(math.isfinite(snapshot.RuntimePosition)))
                return false;

            double3 deltaMeters = new double3(
                (double)runtimePosition.x - snapshot.RuntimePosition.x,
                (double)runtimePosition.y - snapshot.RuntimePosition.y,
                (double)runtimePosition.z - snapshot.RuntimePosition.z);
            positionAup = AbsoluteUniversePosition.OffsetMeters(
                in snapshot.Aup,
                deltaMeters);
            return AbsoluteUniversePosition.IsFinite(in positionAup);
        }

        private static Vector3 ResolveFiniteDirection(Vector3 value, Vector3 fallback)
        {
            if (IsFiniteVector(value))
            {
                float lengthSq = value.sqrMagnitude;
                if (math.isfinite(lengthSq) && lengthSq > HullRepairEpsilon * HullRepairEpsilon)
                    return value * math.rsqrt(lengthSq);
            }

            if (IsFiniteVector(fallback))
            {
                float fallbackLengthSq = fallback.sqrMagnitude;
                if (math.isfinite(fallbackLengthSq) && fallbackLengthSq > HullRepairEpsilon * HullRepairEpsilon)
                    return fallback * math.rsqrt(fallbackLengthSq);
            }

            return Vector3.forward;
        }

        private static bool IsFiniteQuaternion(Quaternion value)
        {
            return math.isfinite(value.x) &&
                   math.isfinite(value.y) &&
                   math.isfinite(value.z) &&
                   math.isfinite(value.w);
        }

        private static float FiniteNonNegativeOrZero(float value)
        {
            return math.isfinite(value) && value > 0f ? value : 0f;
        }

        private static float FiniteAtLeast(float value, float minimum)
        {
            return math.isfinite(value) && value > minimum ? value : minimum;
        }

        private static float SanitizePackedHullDentValue(float value)
        {
            return math.isfinite(value) && value > 0f ? value : 0f;
        }

        private static float ResolveSafeScale(float scale)
        {
            return math.isfinite(scale) && math.abs(scale) > HullRepairEpsilon ? scale : 1f;
        }

        private void PublishIntegrityDiagnostic(IRepairableModuleTarget module, Vector3 hitPoint, Vector3 hitNormal)
        {
            if (module == null || !TryBuildIntegrityDiagnosticBuffer(module, out int length))
            {
                ClearIntegrityDiagnostic();
                return;
            }

            DiegeticTooltipSystem tooltipSystem = ResolveDiegeticTooltipSystem();
            if (tooltipSystem == null)
                return;

            Vector3 anchorPoint = hitPoint + (hitNormal * 0.035f);
            tooltipSystem.ShowDiagnostic(anchorPoint, _integrityDiagnosticBuffer.AsSpan(0, length), new Color(0.46f, 0.98f, 0.94f, 0.96f));
        }

        private void ClearIntegrityDiagnostic()
        {
            DiegeticTooltipSystem tooltipSystem = ResolveDiegeticTooltipSystem();
            if (tooltipSystem != null)
                tooltipSystem.ClearDiagnostic();
        }

        private DiegeticTooltipSystem ResolveDiegeticTooltipSystem()
        {
            return _cachedDiegeticTooltipSystem;
        }

        private bool TryBuildIntegrityDiagnosticBuffer(IRepairableModuleTarget module, out int length)
        {
            length = 0;
            if (module == null)
                return false;

            if (!TryReadModuleRepairState(module, out ModuleRepairReadSnapshot snapshot))
                return false;

            int cursor = 0;
            s_integrityDiagnosticPrefixChars.CopyTo(_integrityDiagnosticBuffer, cursor);
            cursor += s_integrityDiagnosticPrefixChars.Length;
            int integrityPercent = ResolveModuleIntegrityPercent(in snapshot);
            if (!integrityPercent.TryFormat(_integrityDiagnosticBuffer.AsSpan(cursor), out int written))
                return false;

            cursor += written;
            if (cursor >= _integrityDiagnosticBuffer.Length)
                return false;

            _integrityDiagnosticBuffer[cursor++] = '%';
            length = cursor;
            return true;
        }

        private bool TryReadServiceDiagnosis(out ServiceDiagnosis diagnosis)
        {
            diagnosis = default;

            bool didHit = TryGetRepairHit(out _hit);

            CacheRepairTargetsForCollider(didHit ? _hit.collider : null, out IRepairableModuleTarget module, out _);
            if (module == null)
                return false;

            diagnosis = BuildDiagnosis(module);
            return true;
        }

        private bool TryGetServiceDiagnosisCached(out ServiceDiagnosis diagnosis)
        {
            uint currentStamp = _diagnosisEvaluationStamp;
            if (_cachedDiagnosisStamp == currentStamp)
            {
                diagnosis = _cachedDiagnosis;
                return _cachedDiagnosisValid;
            }

            bool valid = TryReadServiceDiagnosis(out diagnosis);
            _cachedDiagnosisStamp = currentStamp;
            _cachedDiagnosisValid = valid;
            _cachedDiagnosis = diagnosis;
            return valid;
        }

        private void InvalidateDiagnosisCache()
        {
            _cachedDiagnosisStamp = uint.MaxValue;
            _cachedDiagnosisValid = false;
            _cachedDiagnosis = default;
        }

        private bool TryGetRepairHit(out InteractionSurfaceHit hit)
        {
            hit = default;
            return TryResolveRepairRay(out Vector3 origin, out Vector3 direction) &&
                   RequestPrimarySurfaceHit(origin, direction, ResolveRuntimeRepairRange(), ResolveRepairSurfaceMask(), QueryTriggerInteraction.Ignore, out hit);
        }

        private int ResolveRepairSurfaceMask()
        {
            return HectonLayerMasks.ResolveSurfaceInteractionLayerMask(repairMask.value);
        }

        private bool TryResolveRepairRay(out Vector3 origin, out Vector3 direction)
        {
            origin = default;
            direction = default;

            if (!TryGetPlayerRuntimeContext(out IPlayerRuntimeContext playerContext) ||
                !playerContext.TryGetPlayerPoseSnapshot(out PlayerRuntimePoseSnapshot snapshot))
            {
                return false;
            }

            float3 runtimePosition = snapshot.RuntimePosition;
            float3 forward = snapshot.Forward;
            float forwardLengthSq = math.lengthsq(forward);
            if (!math.all(math.isfinite(runtimePosition)) ||
                !math.all(math.isfinite(forward)) ||
                !math.isfinite(forwardLengthSq) ||
                forwardLengthSq <= 0.0001f)
            {
                return false;
            }

            float invForwardLength = math.rsqrt(math.max(forwardLengthSq, 0.0001f));
            origin = new Vector3(runtimePosition.x, runtimePosition.y, runtimePosition.z);
            direction = new Vector3(
                forward.x * invForwardLength,
                forward.y * invForwardLength,
                forward.z * invForwardLength);
            return true;
        }

        private Vector3 ResolveRepairForwardFallback()
        {
            if (TryResolveRepairRay(out _, out Vector3 direction))
                return direction;

            return Vector3.forward;
        }

        private void AdvanceRepairEvaluationStamps()
        {
            unchecked
            {
                _diagnosisEvaluationStamp++;
            }
        }

        private uint NextRepairBlackBoxFrame()
        {
            unchecked
            {
                _repairBlackBoxFrame++;
                return _repairBlackBoxFrame;
            }
        }

        private static bool TryReadModuleRepairState(IRepairableModuleTarget module, out ModuleRepairReadSnapshot snapshot)
        {
            snapshot = default;
            return module != null && module.TryReadRepairState(out snapshot);
        }

        private static float ResolveModuleIntegrity01(IRepairableModuleTarget module)
        {
            if (!TryReadModuleRepairState(module, out ModuleRepairReadSnapshot snapshot))
                return 0f;

            return ResolveModuleIntegrity01(in snapshot);
        }

        private static float ResolveModuleIntegrity01(in ModuleRepairReadSnapshot snapshot)
        {
            float current = snapshot.CurrentIntegrity;
            float max = snapshot.MaxIntegrity;
            if (!float.IsFinite(current) || !float.IsFinite(max) || max <= 0.01f)
                return 0f;

            return math.saturate(current / max);
        }

        private static int ResolveModuleIntegrityPercent(IRepairableModuleTarget module)
        {
            if (!TryReadModuleRepairState(module, out ModuleRepairReadSnapshot snapshot))
                return 0;

            return ResolveModuleIntegrityPercent(in snapshot);
        }

        private static int ResolveModuleIntegrityPercent(in ModuleRepairReadSnapshot snapshot)
        {
            return (int)(ResolveModuleIntegrity01(in snapshot) * 100f + 0.5f);
        }

        private static bool IsModuleIntegrityAtMax(IRepairableModuleTarget module)
        {
            return TryReadModuleRepairState(module, out ModuleRepairReadSnapshot snapshot) &&
                   IsModuleIntegrityAtMax(in snapshot);
        }

        private static bool IsModuleIntegrityAtMax(in ModuleRepairReadSnapshot snapshot)
        {
            return IsIntegrityAtMax(snapshot.CurrentIntegrity, snapshot.MaxIntegrity);
        }

        private static bool IsIntegrityAtMax(float current, float max)
        {
            return float.IsFinite(current) &&
                   float.IsFinite(max) &&
                   max > 0.01f &&
                   current >= max;
        }

        private static bool IsIntegrityBelowMax(float current, float max)
        {
            return float.IsFinite(max) &&
                   max > 0.01f &&
                   (!float.IsFinite(current) || current < max);
        }

        private static ServiceDiagnosis BuildDiagnosis(IRepairableModuleTarget module)
        {
            if (!TryReadModuleRepairState(module, out ModuleRepairReadSnapshot snapshot))
                return default;

            float integrity01 = ResolveModuleIntegrity01(in snapshot);
            int integrityPercent = (int)(integrity01 * 100f + 0.5f);
            bool isFlooded = (snapshot.Flags & ModuleRepairReadSnapshot.FlagFlooded) != 0u;
            bool isDraining = (snapshot.Flags & ModuleRepairReadSnapshot.FlagDraining) != 0u;
            bool hasPower = (snapshot.Flags & ModuleRepairReadSnapshot.FlagHasPower) != 0u;

            if (isFlooded && !hasPower && IsModuleIntegrityAtMax(in snapshot))
                return BuildNoPowerDiagnosis(integrityPercent);

            if (isFlooded && isDraining)
                return BuildDrainingDiagnosis(integrityPercent);

            if (isFlooded)
                return BuildFloodedDiagnosis(integrityPercent);

            if (integrity01 >= 0.999f)
                return BuildSealedDiagnosis();

            if (integrity01 <= 0.25f)
                return BuildCriticalDamageDiagnosis(integrityPercent);

            if (integrity01 <= 0.65f)
                return BuildHeavyDamageDiagnosis(integrityPercent);

            return BuildPatchingDiagnosis(integrityPercent);
        }

        private static ServiceDiagnosis BuildNoPowerDiagnosis(int integrityPercent)
        {
            return new ServiceDiagnosis
            {
                status = "FLOODED",
                headline = RepairToolNoPowerHeadline,
                summaryKey = H8ToolLocHashes.REPAIR_TOOL_SUMMARY_NO_POWER,
                summaryFallback = "Integrity {0:0}% // compartment flooded // pumps offline.",
                recommendation = StableText(
                    H8ToolLocHashes.REPAIR_TOOL_RECOMMEND_NO_POWER,
                    "Restore power before expecting water evacuation."),
                severity = "WARN",
                priority = StableText(H8ToolLocHashes.REPAIR_TOOL_PRIORITY_SERVICE_BLOCKED, "SERVICE BLOCKED"),
                integrityPercent = integrityPercent,
                hasIntegrityPercent = true
            };
        }

        private static ServiceDiagnosis BuildDrainingDiagnosis(int integrityPercent)
        {
            return new ServiceDiagnosis
            {
                status = "DRAINING",
                headline = RepairToolDrainingHeadline,
                summaryKey = H8ToolLocHashes.REPAIR_TOOL_SUMMARY_DRAINING,
                summaryFallback = "Integrity {0:0}% // pumps are clearing floodwater.",
                recommendation = StableText(
                    H8ToolLocHashes.REPAIR_TOOL_RECOMMEND_DRAINING,
                    "Hold perimeter and let the compartment finish draining."),
                severity = "INFO",
                priority = StableText(H8ToolLocHashes.REPAIR_TOOL_PRIORITY_STABILIZING, "STABILIZING"),
                integrityPercent = integrityPercent,
                hasIntegrityPercent = true
            };
        }

        private static ServiceDiagnosis BuildFloodedDiagnosis(int integrityPercent)
        {
            return new ServiceDiagnosis
            {
                status = "FLOODED",
                headline = RepairToolFloodedHeadline,
                summaryKey = H8ToolLocHashes.REPAIR_TOOL_SUMMARY_FLOODED,
                summaryFallback = "Integrity {0:0}% // compartment breach still active.",
                recommendation = StableText(
                    H8ToolLocHashes.REPAIR_TOOL_RECOMMEND_FLOODED,
                    "Continue repair until integrity reaches 100% and pump cycle can start."),
                severity = "WARN",
                priority = StableText(H8ToolLocHashes.REPAIR_TOOL_PRIORITY_IMMEDIATE_SERVICE, "IMMEDIATE SERVICE"),
                integrityPercent = integrityPercent,
                hasIntegrityPercent = true
            };
        }

        private static ServiceDiagnosis BuildSealedDiagnosis()
        {
            return new ServiceDiagnosis
            {
                status = "SEALED",
                headline = RepairToolSealedHeadline,
                summary = StableText(
                    H8ToolLocHashes.REPAIR_TOOL_SUMMARY_SEALED,
                    "Integrity 100% // hull stable // compartment dry."),
                recommendation = StableText(
                    H8ToolLocHashes.REPAIR_TOOL_RECOMMEND_SEALED,
                    "No further repair action required."),
                severity = "INFO",
                priority = StableText(H8ToolLocHashes.REPAIR_TOOL_PRIORITY_SERVICE_COMPLETE, "SERVICE COMPLETE")
            };
        }

        private static ServiceDiagnosis BuildCriticalDamageDiagnosis(int integrityPercent)
        {
            return new ServiceDiagnosis
            {
                status = "CRITICAL",
                headline = RepairToolCriticalDamageHeadline,
                summaryKey = H8ToolLocHashes.REPAIR_TOOL_SUMMARY_CRITICAL,
                summaryFallback = "Integrity {0:0}% // hull failure risk elevated.",
                recommendation = StableText(
                    H8ToolLocHashes.REPAIR_TOOL_RECOMMEND_CRITICAL,
                    "Maintain continuous repair contact until the module exits critical range."),
                severity = "CRITICAL",
                priority = StableText(H8ToolLocHashes.REPAIR_TOOL_PRIORITY_CRITICAL_RESPONSE, "CRITICAL RESPONSE"),
                integrityPercent = integrityPercent,
                hasIntegrityPercent = true
            };
        }

        private static ServiceDiagnosis BuildHeavyDamageDiagnosis(int integrityPercent)
        {
            return new ServiceDiagnosis
            {
                status = "DAMAGED",
                headline = RepairToolHeavyDamageHeadline,
                summaryKey = H8ToolLocHashes.REPAIR_TOOL_SUMMARY_HEAVY,
                summaryFallback = "Integrity {0:0}% // hull is compromised but recoverable.",
                recommendation = StableText(
                    H8ToolLocHashes.REPAIR_TOOL_RECOMMEND_HEAVY,
                    "Keep the repair beam on target and avoid leaving the module unattended."),
                severity = "WARN",
                priority = StableText(H8ToolLocHashes.REPAIR_TOOL_PRIORITY_ACTIVE_SERVICE, "ACTIVE SERVICE"),
                integrityPercent = integrityPercent,
                hasIntegrityPercent = true
            };
        }

        private static ServiceDiagnosis BuildPatchingDiagnosis(int integrityPercent)
        {
            return new ServiceDiagnosis
            {
                status = "DAMAGED",
                headline = RepairToolPatchingHeadline,
                summaryKey = H8ToolLocHashes.REPAIR_TOOL_SUMMARY_PATCHING,
                summaryFallback = "Integrity {0:0}% // module is nearly sealed.",
                recommendation = StableText(
                    H8ToolLocHashes.REPAIR_TOOL_RECOMMEND_PATCHING,
                    "Finish the repair cycle to restore full integrity."),
                severity = "INFO",
                priority = StableText(H8ToolLocHashes.REPAIR_TOOL_PRIORITY_FINAL_PASS, "FINAL PASS"),
                integrityPercent = integrityPercent,
                hasIntegrityPercent = true
            };
        }

        private static void PublishDiagnosis(ServiceDiagnosis diagnosis)
        {
            s_hudBuffer.Clear();
            if (!TryWriteDiagnosisMessage(ref s_hudBuffer, diagnosis))
                return;

            if (diagnosis.severity == "CRITICAL")
                ToolHitUtility.ShowWarning(in s_hudBuffer);
            else if (diagnosis.severity == "WARN")
                ToolHitUtility.ShowWarning(in s_hudBuffer);
            else
                ToolHitUtility.ShowInfo(in s_hudBuffer);
        }

        private static void PublishInfoMessage(string message)
        {
            s_hudBuffer.Clear();
            if (AppendText(ref s_hudBuffer, message))
                ToolHitUtility.ShowInfo(in s_hudBuffer);
        }

        private static void PublishWarningMessage(string message)
        {
            s_hudBuffer.Clear();
            if (AppendText(ref s_hudBuffer, message))
                ToolHitUtility.ShowWarning(in s_hudBuffer);
        }

        private static void PublishActiveRepairInfo(string headline)
        {
            s_hudBuffer.Clear();
            if (TryWriteActiveRepairHudMessage(ref s_hudBuffer, headline))
                ToolHitUtility.ShowInfo(in s_hudBuffer);
        }

        private static bool TryWriteDiagnosisMessage(ref FixedCharBuffer buffer, ServiceDiagnosis diagnosis)
        {
            if (!AppendText(ref buffer, "REPAIR DIAG - ") ||
                !AppendText(ref buffer, diagnosis.headline) ||
                !AppendText(ref buffer, " // ") ||
                !AppendText(ref buffer, diagnosis.priority) ||
                !AppendText(ref buffer, " // "))
                return false;

            if (!TryWriteDiagnosisSummary(ref buffer, diagnosis))
                return false;

            return AppendText(ref buffer, " // ") &&
                   AppendText(ref buffer, diagnosis.recommendation);
        }

        private static bool TryWriteActiveRepairHudMessage(ref FixedCharBuffer buffer, string headline)
        {
            switch (headline)
            {
                case RepairToolNoPowerHeadline:
                    return AppendText(ref buffer, StableText(H8ToolLocHashes.REPAIR_TOOL_HUD_NO_POWER, "REPAIR TOOL - NO POWER"));
                case RepairToolDrainingHeadline:
                    return AppendText(ref buffer, StableText(H8ToolLocHashes.REPAIR_TOOL_HUD_DRAINING, "REPAIR TOOL - DRAINING"));
                case RepairToolFloodedHeadline:
                    return AppendText(ref buffer, StableText(H8ToolLocHashes.REPAIR_TOOL_HUD_FLOODED, "REPAIR TOOL - FLOODED"));
                case RepairToolSealedHeadline:
                    return AppendText(ref buffer, StableText(H8ToolLocHashes.REPAIR_TOOL_HUD_SEALED, "REPAIR TOOL - SEALED"));
                case RepairToolCriticalDamageHeadline:
                    return AppendText(ref buffer, StableText(H8ToolLocHashes.REPAIR_TOOL_HUD_CRITICAL_DAMAGE, "REPAIR TOOL - CRITICAL DAMAGE"));
                case RepairToolHeavyDamageHeadline:
                    return AppendText(ref buffer, StableText(H8ToolLocHashes.REPAIR_TOOL_HUD_HEAVY_DAMAGE, "REPAIR TOOL - HEAVY DAMAGE"));
                case RepairToolPatchingHeadline:
                    return AppendText(ref buffer, StableText(H8ToolLocHashes.REPAIR_TOOL_HUD_PATCHING, "REPAIR TOOL - PATCHING"));
                default:
                    return AppendText(ref buffer, "REPAIR TOOL - ") &&
                           AppendText(ref buffer, headline);
            }
        }

        private static bool TryWriteDiagnosisSummary(ref FixedCharBuffer buffer, ServiceDiagnosis diagnosis)
        {
            if (!diagnosis.hasIntegrityPercent)
                return AppendText(ref buffer, diagnosis.summary);

            string template = StableText(diagnosis.summaryKey, diagnosis.summaryFallback);
            return TryAppendSingleIntTemplate(ref buffer, template, diagnosis.integrityPercent);
        }

        private static bool TryWriteDiagnosisLogSummary(ref FixedCharBuffer buffer, ServiceDiagnosis diagnosis)
        {
            if (!TryWriteDiagnosisSummary(ref buffer, diagnosis))
                return false;

            return AppendText(ref buffer, " ") &&
                   AppendText(ref buffer, diagnosis.recommendation);
        }

        private static bool TryWriteRepairStartedLogSummary(ref FixedCharBuffer buffer, ServiceDiagnosis diagnosis)
        {
            string template = StableText(
                H8ToolLocHashes.REPAIR_TOOL_LOG_STARTED_MESSAGE,
                "{0} entered active repair service. {1} {2}");
            return TryAppendRepairStartedTemplate(ref buffer, template, RepairToolModuleLabel, diagnosis);
        }

        private static bool TryWriteRepairRestoredLogSummary(ref FixedCharBuffer buffer)
        {
            string template = StableText(
                H8ToolLocHashes.REPAIR_TOOL_LOG_RESTORED_MESSAGE,
                "{0} reached full integrity and dry status.");
            return TryAppendSingleStringTemplate(ref buffer, template, RepairToolModuleLabel);
        }

        private static bool TryAppendRepairStartedTemplate(
            ref FixedCharBuffer buffer,
            string template,
            string targetLabel,
            ServiceDiagnosis diagnosis)
        {
            ReadOnlySpan<char> templateSpan = template.AsSpan();
            if (templateSpan.Length <= 0)
                return AppendText(ref buffer, targetLabel);

            int segmentStart = 0;
            for (int i = 0; i < templateSpan.Length; i++)
            {
                if (templateSpan[i] != '{' || i + 1 >= templateSpan.Length)
                    continue;

                char tokenIndex = templateSpan[i + 1];
                int tokenEnd = i + 2;
                while (tokenEnd < templateSpan.Length && templateSpan[tokenEnd] != '}')
                    tokenEnd++;

                if (tokenEnd >= templateSpan.Length)
                    continue;

                if (i > segmentStart && !buffer.Append(templateSpan.Slice(segmentStart, i - segmentStart)))
                    return false;

                bool wroteToken = false;
                switch (tokenIndex)
                {
                    case '0':
                        wroteToken = AppendText(ref buffer, targetLabel);
                        break;
                    case '1':
                        wroteToken = TryWriteDiagnosisSummary(ref buffer, diagnosis);
                        break;
                    case '2':
                        wroteToken = AppendText(ref buffer, diagnosis.recommendation);
                        break;
                }

                if (!wroteToken && !buffer.Append(templateSpan.Slice(i, tokenEnd - i + 1)))
                    return false;

                i = tokenEnd;
                segmentStart = tokenEnd + 1;
            }

            return segmentStart >= templateSpan.Length || buffer.Append(templateSpan.Slice(segmentStart));
        }

        private static bool TryAppendSingleStringTemplate(ref FixedCharBuffer buffer, string template, string value)
        {
            ReadOnlySpan<char> templateSpan = template.AsSpan();
            if (templateSpan.Length <= 0)
                return AppendText(ref buffer, value);

            bool wroteTemplateToken = false;
            int segmentStart = 0;
            for (int i = 0; i < templateSpan.Length; i++)
            {
                if (templateSpan[i] != '{' || i + 1 >= templateSpan.Length || templateSpan[i + 1] != '0')
                    continue;

                int tokenEnd = i + 2;
                while (tokenEnd < templateSpan.Length && templateSpan[tokenEnd] != '}')
                    tokenEnd++;

                if (tokenEnd >= templateSpan.Length)
                    continue;

                if (i > segmentStart && !buffer.Append(templateSpan.Slice(segmentStart, i - segmentStart)))
                    return false;

                if (!AppendText(ref buffer, value))
                    return false;

                wroteTemplateToken = true;
                i = tokenEnd;
                segmentStart = tokenEnd + 1;
            }

            if (!wroteTemplateToken)
                return buffer.Append(templateSpan);

            return segmentStart >= templateSpan.Length || buffer.Append(templateSpan.Slice(segmentStart));
        }

        private static bool TryAppendSingleIntTemplate(ref FixedCharBuffer buffer, string template, int value)
        {
            ReadOnlySpan<char> templateSpan = template.AsSpan();
            if (templateSpan.Length <= 0)
                return buffer.AppendInt(value);

            bool wroteTemplateToken = false;
            int segmentStart = 0;
            for (int i = 0; i < templateSpan.Length; i++)
            {
                if (templateSpan[i] != '{' || i + 1 >= templateSpan.Length || templateSpan[i + 1] != '0')
                    continue;

                int tokenEnd = i + 2;
                while (tokenEnd < templateSpan.Length && templateSpan[tokenEnd] != '}')
                    tokenEnd++;

                if (tokenEnd >= templateSpan.Length)
                    continue;

                if (i > segmentStart && !buffer.Append(templateSpan.Slice(segmentStart, i - segmentStart)))
                    return false;

                if (!buffer.AppendInt(value))
                    return false;

                wroteTemplateToken = true;
                i = tokenEnd;
                segmentStart = tokenEnd + 1;
            }

            if (!wroteTemplateToken)
                return buffer.Append(templateSpan);

            return segmentStart >= templateSpan.Length || buffer.Append(templateSpan.Slice(segmentStart));
        }

        private static string GetServiceDiagnosisLogTitle(string headline)
        {
            switch (headline)
            {
                case RepairToolNoPowerHeadline:
                    return StableText(H8ToolLocHashes.REPAIR_TOOL_LOG_DIAG_NO_POWER, "SERVICE DIAG - NO POWER");
                case RepairToolDrainingHeadline:
                    return StableText(H8ToolLocHashes.REPAIR_TOOL_LOG_DIAG_DRAINING, "SERVICE DIAG - DRAINING");
                case RepairToolFloodedHeadline:
                    return StableText(H8ToolLocHashes.REPAIR_TOOL_LOG_DIAG_FLOODED, "SERVICE DIAG - FLOODED");
                case RepairToolSealedHeadline:
                    return StableText(H8ToolLocHashes.REPAIR_TOOL_LOG_DIAG_SEALED, "SERVICE DIAG - SEALED");
                case RepairToolCriticalDamageHeadline:
                    return StableText(H8ToolLocHashes.REPAIR_TOOL_LOG_DIAG_CRITICAL, "SERVICE DIAG - CRITICAL DAMAGE");
                case RepairToolHeavyDamageHeadline:
                    return StableText(H8ToolLocHashes.REPAIR_TOOL_LOG_DIAG_HEAVY, "SERVICE DIAG - HEAVY DAMAGE");
                case RepairToolPatchingHeadline:
                    return StableText(H8ToolLocHashes.REPAIR_TOOL_LOG_DIAG_PATCHING, "SERVICE DIAG - PATCHING");
                default:
                    return "SERVICE DIAG";
            }
        }

        private static string StableText(uint keyHash, string fallback)
        {
            return keyHash switch
            {
                H8ToolLocHashes.REPAIR_TOOL_CATEGORY => s_locRepairToolCategory,
                H8ToolLocHashes.REPAIR_TOOL_HUD_NO_TARGET => s_locRepairToolHudNoTarget,
                H8ToolLocHashes.REPAIR_TOOL_HUD_SEALED => s_locRepairToolHudSealed,
                H8ToolLocHashes.REPAIR_TOOL_HUD_RESTORED => s_locRepairToolHudRestored,
                H8ToolLocHashes.REPAIR_TOOL_HUD_INVALID_TARGET => s_locRepairToolHudInvalidTarget,
                H8ToolLocHashes.REPAIR_TOOL_HUD_NO_MODULE => s_locRepairToolHudNoModule,
                H8ToolLocHashes.REPAIR_TOOL_HUD_NOT_SERVICEABLE => s_locRepairToolHudNotServiceable,
                H8ToolLocHashes.REPAIR_TOOL_HUD_NO_POWER => s_locRepairToolHudNoPower,
                H8ToolLocHashes.REPAIR_TOOL_HUD_DRAINING => s_locRepairToolHudDraining,
                H8ToolLocHashes.REPAIR_TOOL_HUD_FLOODED => s_locRepairToolHudFlooded,
                H8ToolLocHashes.REPAIR_TOOL_HUD_CRITICAL_DAMAGE => s_locRepairToolHudCriticalDamage,
                H8ToolLocHashes.REPAIR_TOOL_HUD_HEAVY_DAMAGE => s_locRepairToolHudHeavyDamage,
                H8ToolLocHashes.REPAIR_TOOL_HUD_PATCHING => s_locRepairToolHudPatching,
                H8ToolLocHashes.REPAIR_TOOL_LOG_STARTED_TITLE => s_locRepairToolLogStartedTitle,
                H8ToolLocHashes.REPAIR_TOOL_LOG_STARTED_MESSAGE => s_locRepairToolLogStartedMessage,
                H8ToolLocHashes.REPAIR_TOOL_LOG_RESTORED_TITLE => s_locRepairToolLogRestoredTitle,
                H8ToolLocHashes.REPAIR_TOOL_LOG_RESTORED_MESSAGE => s_locRepairToolLogRestoredMessage,
                H8ToolLocHashes.REPAIR_TOOL_LOG_DIAG_NO_POWER => s_locRepairToolLogDiagNoPower,
                H8ToolLocHashes.REPAIR_TOOL_LOG_DIAG_DRAINING => s_locRepairToolLogDiagDraining,
                H8ToolLocHashes.REPAIR_TOOL_LOG_DIAG_FLOODED => s_locRepairToolLogDiagFlooded,
                H8ToolLocHashes.REPAIR_TOOL_LOG_DIAG_SEALED => s_locRepairToolLogDiagSealed,
                H8ToolLocHashes.REPAIR_TOOL_LOG_DIAG_CRITICAL => s_locRepairToolLogDiagCritical,
                H8ToolLocHashes.REPAIR_TOOL_LOG_DIAG_HEAVY => s_locRepairToolLogDiagHeavy,
                H8ToolLocHashes.REPAIR_TOOL_LOG_DIAG_PATCHING => s_locRepairToolLogDiagPatching,
                H8ToolLocHashes.REPAIR_TOOL_LOG_DIAG_GENERIC => s_locRepairToolLogDiagGeneric,
                H8ToolLocHashes.REPAIR_TOOL_OPERATIONAL_ACTIVE => s_locRepairToolOperationalActive,
                H8ToolLocHashes.REPAIR_TOOL_OPERATIONAL_STANDBY => s_locRepairToolOperationalStandby,
                H8ToolLocHashes.REPAIR_TOOL_OPERATIONAL_ACTIVE_DIRECTIVE => s_locRepairToolOperationalActiveDirective,
                H8ToolLocHashes.REPAIR_TOOL_OPERATIONAL_STANDBY_DIRECTIVE => s_locRepairToolOperationalStandbyDirective,
                H8ToolLocHashes.REPAIR_TOOL_SUMMARY_NO_POWER => s_locRepairToolSummaryNoPower,
                H8ToolLocHashes.REPAIR_TOOL_SUMMARY_DRAINING => s_locRepairToolSummaryDraining,
                H8ToolLocHashes.REPAIR_TOOL_SUMMARY_FLOODED => s_locRepairToolSummaryFlooded,
                H8ToolLocHashes.REPAIR_TOOL_SUMMARY_SEALED => s_locRepairToolSummarySealed,
                H8ToolLocHashes.REPAIR_TOOL_SUMMARY_CRITICAL => s_locRepairToolSummaryCritical,
                H8ToolLocHashes.REPAIR_TOOL_SUMMARY_HEAVY => s_locRepairToolSummaryHeavy,
                H8ToolLocHashes.REPAIR_TOOL_SUMMARY_PATCHING => s_locRepairToolSummaryPatching,
                H8ToolLocHashes.REPAIR_TOOL_RECOMMEND_NO_POWER => s_locRepairToolRecommendNoPower,
                H8ToolLocHashes.REPAIR_TOOL_RECOMMEND_DRAINING => s_locRepairToolRecommendDraining,
                H8ToolLocHashes.REPAIR_TOOL_RECOMMEND_FLOODED => s_locRepairToolRecommendFlooded,
                H8ToolLocHashes.REPAIR_TOOL_RECOMMEND_SEALED => s_locRepairToolRecommendSealed,
                H8ToolLocHashes.REPAIR_TOOL_RECOMMEND_CRITICAL => s_locRepairToolRecommendCritical,
                H8ToolLocHashes.REPAIR_TOOL_RECOMMEND_HEAVY => s_locRepairToolRecommendHeavy,
                H8ToolLocHashes.REPAIR_TOOL_RECOMMEND_PATCHING => s_locRepairToolRecommendPatching,
                H8ToolLocHashes.REPAIR_TOOL_PRIORITY_SERVICE_BLOCKED => s_locRepairToolPriorityServiceBlocked,
                H8ToolLocHashes.REPAIR_TOOL_PRIORITY_STABILIZING => s_locRepairToolPriorityStabilizing,
                H8ToolLocHashes.REPAIR_TOOL_PRIORITY_IMMEDIATE_SERVICE => s_locRepairToolPriorityImmediateService,
                H8ToolLocHashes.REPAIR_TOOL_PRIORITY_SERVICE_COMPLETE => s_locRepairToolPriorityServiceComplete,
                H8ToolLocHashes.REPAIR_TOOL_PRIORITY_CRITICAL_RESPONSE => s_locRepairToolPriorityCriticalResponse,
                H8ToolLocHashes.REPAIR_TOOL_PRIORITY_ACTIVE_SERVICE => s_locRepairToolPriorityActiveService,
                H8ToolLocHashes.REPAIR_TOOL_PRIORITY_FINAL_PASS => s_locRepairToolPriorityFinalPass,
                _ => null
            } ?? fallback ?? string.Empty;
        }

        private static bool AppendText(ref FixedCharBuffer buffer, string value)
        {
            return string.IsNullOrEmpty(value) || buffer.Append(value);
        }

        // ══════════════════════════════════════════════════════════
        //  IBatteryTool IMPLEMENTATION
        // ══════════════════════════════════════════════════════════

    
        #region JulesLink_RepairRateMaterialCalculator
        private static void JulesLink_RepairRateMaterialCalculator() { _ = typeof(Hecton8.PureLogic.Systems.RepairRateMaterialCalculator); }
        #endregion

        #region JulesLink_WeldHeatDissipationCalculator
        private static void JulesLink_WeldHeatDissipationCalculator() { _ = typeof(Hecton8.PureLogic.Systems.WeldHeatDissipationCalculator); }
        #endregion
}
}
