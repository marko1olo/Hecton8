namespace Hecton8.Tools
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Runtime.CompilerServices;
    using Unity.Burst;
    using Hecton8.Core;
    using Hecton8.Core.Memory;
    using Hecton8.Core.Contracts.Signals;
    using Hecton8.Gameplay;
    using Unity.Collections;
    using Unity.Collections.LowLevel.Unsafe;
    using Unity.Jobs;
    using Unity.Mathematics;
    using UnityEngine;
    using UnityEngine.SceneManagement;
#if UNITY_EDITOR
    using UnityEditor;
#endif

    /// <summary>
    /// Authoritative runtime owner for active handheld-tool state.
    /// Tool authoring remains in ScriptableObjects and components; hot paths read only native memory.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-9918)]
    public sealed partial class ModularEquipmentEngine : MonoBehaviour, IModularEquipmentService, IUpdatable, ILateFrameTickable, IServiceHeartbeat, IServiceShutdown, IGlobalRegistryHotSwapListener, IGlobalRegistryHotSwapRefListener
    {
        private static int s_x001ModularEquipmentEngineSignalPushDropCount;
        private const int MaxTrackedTools = 16;
        private const float OverchargePowerMultiplier = 3f;
        private const float OverchargeHeatExponent = 1.35f;
        private const float OverchargeHeatScale = 1.75f;
        private const float OverchargeExplosionHeatThreshold = 1.5f;
        private const float OverchargeExplosionPlayerDamage = 45f;
        private const float OverchargeHeatGrowthInputMax = 2.25f;
        private const float InvTau = 0.15915494f;
        private const float WirelessBrownoutPulseCycles = 18f * InvTau;
        private const float ToolBrownoutPulseCycles = 8f * InvTau;
        private const float StandardDepthFailureMeters = 500f;
        private const float HeatWarningThreshold = 0.90f;
        private const float HeatWarningResetThreshold = 0.85f;
        private const float OverheatRecoveryThreshold = 0.15f;
        private const float ToolSignalMinQualityFloatDelta = 0.02f;
        private const float ToolSignalMidTierFloatDelta = 0.01f;
        private const float ToolSignalHighTierFloatDelta = 0.005f;
        private const float ToolSignalUltraTierFloatDelta = 0.0025f;
        private const float ToolSignalDistanceDeltaMeters = 0.5f;
        private const int EquipmentTelemetryRingLength = 300;
        private const int EquipmentSignalQueueCapacity = 32;
        private const int EquipmentHardwareSpecCapacity = 64;
        private const float EquipmentFallbackAmbientCelsius = 6f;
        private const float EquipmentDefaultCellSizeMeters = 1f;
        private const float EquipmentMockRootOffsetMeters = 0.35f;
        private const float EquipmentFaultCostThresholdMicroseconds = 100f;
        private const float EquipmentDryHeatMultiplier = 3.25f;
        private const BufferID FlashlightTelemetryRingBufferId = BufferID.ModularEquipmentEngine_FlashlightTelemetryRingBufferId;
        private const BufferID FlashlightTelemetryCursorBufferId = BufferID.ModularEquipmentEngine_FlashlightTelemetryCursorBufferId;
        private const uint EquipmentFaultNonFinite = 1u << 0;
        private const uint EquipmentFaultThermalGridInvalid = 1u << 1;
        private const uint EquipmentFaultCsvOverflow = 1u << 2;
        private const uint EquipmentFaultOverBudget = 1u << 3;
        private const uint EquipmentFaultSignalDrop = 1u << 4;
        private const uint EquipmentFaultWriteLockContention = 1u << 5;
        private const uint EquipmentFaultWriteLockReleaseFailure = 1u << 6;
        private const int EquipmentWriteLockBufferCount = 28;
        private const uint EquipmentOverheatLaneHash = 0xE1480A01u;
        private const uint ToolDepletedLaneHash = 0xE1480A02u;
        private const uint EquipmentMockBaseToolHash = 0x53483148u;
        private const uint EquipmentTelemetryDumpFaultHash = 0x45514446u; // EQDF
        private const uint UpgradeTelemetryDumpFaultHash = 0x55504446u; // UPDF
        private const string EquipmentFaultDumpPath = "Docs/AgentLogs/Dump_1416_ModularEquipment.bin";
        private const string EquipmentFaultDumpPayloadLabel = "ModularEquipmentTelemetryDumpPayload";
        private const SystemID EquipmentVaultOwnerSystemId = SystemID.GameplayTools;
        private static readonly int FlashlightFailureStateShaderId = Shader.PropertyToID("_HectonFlashlightFailureState");
        private static readonly int FlashlightActiveShaderId = Shader.PropertyToID("_HectonFlashlightActive");
        private static readonly int FlashlightVoxelActiveShaderId = Shader.PropertyToID("_HectonFlashlightVoxelActive");
        private static readonly int FlashlightPositionWsShaderId = Shader.PropertyToID("_HectonFlashlightPositionWS");
        private static readonly int FlashlightDirectionWsShaderId = Shader.PropertyToID("_HectonFlashlightDirectionWS");
        private static readonly int FlashlightColorShaderId = Shader.PropertyToID("_HectonFlashlightColor");
        private static readonly int FlashlightConeDataShaderId = Shader.PropertyToID("_HectonFlashlightConeData");
        private static readonly int FlashlightVoxelWorldToLocalShaderId = Shader.PropertyToID("_HectonFlashlightVoxelWorldToLocal");
        private static readonly int FlashlightVoxelHalfExtentsShaderId = Shader.PropertyToID("_HectonFlashlightVoxelHalfExtents");
        private static readonly ulong EquipmentFaultTelemetryMutationGuardMask =
            EquipmentMutationGuardBit(BufferID.ShinobuActiveEquipmentTelemetryRing) |
            EquipmentMutationGuardBit(BufferID.ShinobuActiveEquipmentTelemetryCursor);
        private static readonly ulong EquipmentViewsMutationGuardMask =
            EquipmentMutationGuardBit(BufferID.ShinobuActiveEquipmentToolStates) |
            EquipmentMutationGuardBit(BufferID.ShinobuActiveEquipmentToolStats) |
            EquipmentMutationGuardBit(BufferID.ShinobuActiveEquipmentToolTypes) |
            EquipmentMutationGuardBit(BufferID.ToolRuntimeHeat01) |
            EquipmentMutationGuardBit(BufferID.ToolRuntimeBatteryCharge) |
            EquipmentMutationGuardBit(BufferID.ShinobuActiveEquipmentStatusMasks) |
            EquipmentMutationGuardBit(BufferID.ShinobuActiveEquipmentEnvironmentHeat01) |
            EquipmentMutationGuardBit(BufferID.ShinobuActiveEquipmentState) |
            EquipmentMutationGuardBit(BufferID.ShinobuActiveEquipmentPublishedState) |
            EquipmentMutationGuardBit(BufferID.ShinobuActiveEquipmentAupSamples) |
            EquipmentMutationGuardBit(BufferID.ShinobuActiveEquipmentGridLoadRequests) |
            EquipmentMutationGuardBit(BufferID.ShinobuActiveEquipmentWearDrainRates) |
            EquipmentMutationGuardBit(BufferID.ShinobuActiveEquipmentTelemetryRing) |
            EquipmentMutationGuardBit(BufferID.ShinobuActiveEquipmentTelemetryCursor) |
            EquipmentMutationGuardBit(FlashlightTelemetryRingBufferId) |
            EquipmentMutationGuardBit(FlashlightTelemetryCursorBufferId) |
            EquipmentMutationGuardBit(BufferID.ShinobuActiveEquipmentIntegrationCounters) |
            EquipmentMutationGuardBit(BufferID.ShinobuActiveEquipmentTuning) |
            EquipmentMutationGuardBit(BufferID.ShinobuActiveEquipmentHardwareSpecs) |
            EquipmentMutationGuardBit(UpgradeMatrixConstants.UpgradeMasksBuffer) |
            EquipmentMutationGuardBit(UpgradeMatrixConstants.UpgradeBaseStatsBuffer) |
            EquipmentMutationGuardBit(UpgradeMatrixConstants.UpgradeCompiledStatsBuffer) |
            EquipmentMutationGuardBit(UpgradeMatrixConstants.UpgradeLutBuffer) |
            EquipmentMutationGuardBit(UpgradeMatrixConstants.UpgradeToolModuleRulesBuffer) |
            EquipmentMutationGuardBit(UpgradeMatrixConstants.UpgradeToolProfilesBuffer) |
            EquipmentMutationGuardBit(UpgradeMatrixConstants.UpgradeTelemetryRingBuffer) |
            EquipmentMutationGuardBit(UpgradeMatrixConstants.UpgradeTelemetryCursorBuffer) |
            EquipmentMutationGuardBit(UpgradeMatrixConstants.UpgradeVisualStateBuffer);

        // COLD ALLOC: PlayerTool[16] — managed owner mirror for native tool slots — owner: ModularEquipmentEngine
        private readonly PlayerTool[] _toolOwners = new PlayerTool[MaxTrackedTools];
        // COLD ALLOC: bool[16] — slot occupancy flags for native tool slots — owner: ModularEquipmentEngine
        private readonly bool[] _slotUsed = new bool[MaxTrackedTools];
        // COLD ALLOC: ToolUpgradeModuleRuleDTO[64] — packed authored module mirrors for matrix rebuilds — owner: ModularEquipmentEngine
        private readonly ToolUpgradeModuleRuleDTO[] _moduleRuleSlots = new ToolUpgradeModuleRuleDTO[MaxTrackedTools * ToolUpgradeSystem.MaxModuleSlots];
        // COLD ALLOC: ToolUpgradeModuleRuleDTO[4] — cold-path packed scratch buffer for one tool registration — owner: ModularEquipmentEngine
        private readonly ToolUpgradeModuleRuleDTO[] _registrationRules = new ToolUpgradeModuleRuleDTO[ToolUpgradeSystem.MaxModuleSlots];

        private VaultGenerationHandle<ToolState> _toolStatesHandle;
        private VaultGenerationHandle<ToolRuntimeStats> _toolStatsHandle;
        private VaultGenerationHandle<byte> _toolTypesHandle;
        private VaultGenerationHandle<float> _currentHeatHandle;
        private VaultGenerationHandle<float> _batteryChargeHandle;
        private VaultGenerationHandle<uint> _statusMasksHandle;
        private VaultGenerationHandle<float> _environmentHeat01Handle;
        private VaultGenerationHandle<ActiveEquipmentDTO> _activeEquipmentStatesHandle;
        private VaultGenerationHandle<ActiveEquipmentDTO> _publishedActiveEquipmentStatesHandle;
        private VaultGenerationHandle<double3> _activeEquipmentAupSamplesHandle;
        private VaultGenerationHandle<EquipmentGridLoadRequest> _activeEquipmentGridLoadRequestsHandle;
        private VaultGenerationHandle<float> _activeEquipmentWearDrainRatesHandle;
        private VaultGenerationHandle<EquipmentTelemetryEntry> _equipmentTelemetryRingHandle;
        private VaultGenerationHandle<int> _equipmentTelemetryCursorHandle;
        private VaultGenerationHandle<FlashlightTelemetryEntry> _flashlightTelemetryRingHandle;
        private VaultGenerationHandle<int> _flashlightTelemetryCursorHandle;
        private VaultGenerationHandle<EquipmentIntegrationCounters> _equipmentIntegrationCountersHandle;
        private VaultGenerationHandle<EquipmentTuningDTO> _equipmentTuningHandle;
        private VaultGenerationHandle<EquipmentHardwareSpecDTO> _equipmentHardwareSpecsHandle;
        private VaultGenerationHandle<UpgradeMaskDTO> _upgradeMatrixMasksHandle;
        private VaultGenerationHandle<UpgradeStatVectorDTO> _upgradeMatrixBaseStatsHandle;
        private VaultGenerationHandle<UpgradeStatVectorDTO> _upgradeMatrixCompiledStatsHandle;
        private VaultGenerationHandle<UpgradeLutEntryDTO> _upgradeMatrixToolLutHandle;
        private VaultGenerationHandle<ToolUpgradeModuleRuleDTO> _upgradeMatrixToolRulesHandle;
        private VaultGenerationHandle<ToolRuntimeProfile> _upgradeMatrixToolProfilesHandle;
        private VaultGenerationHandle<UpgradeTelemetryEntry> _upgradeMatrixTelemetryRingHandle;
        private VaultGenerationHandle<int> _upgradeMatrixTelemetryCursorHandle;
        private VaultGenerationHandle<UpgradeVisualStateDTO> _upgradeMatrixVisualStatesHandle;
        private bool _isInitialized;
        private bool _registeredService;
        private bool _registeredUpdatable;
        private bool _registeredLateFrame;
        private bool _registeredHotSwap;
        private bool _runtimeOwnerAborted;
        private bool _equipmentSignalLanesReady;
        private bool _equipmentIntegrationScheduled;
        private bool _equipmentFaultDumped;
        private bool _equipmentFaultDumpPending;
        private bool _upgradeTelemetryFaultDumped;
        private bool _upgradeTelemetryFaultDumpPending;
        private bool _upgradeTelemetryScheduled;
        private uint _lastPublishedEquippedMask;
        private ToolStateChangedSignal _lastPublishedToolStateChangedSignal;
        private int _lastPublishedToolStateChangedSlot = -1;
        private int _lastPublishedToolStateChangedValid;
        private uint _externalActiveToolMask;
        private uint _lastTelemetryActiveMask;
        private int _thermalGridWidth;
        private int _thermalGridHeight;
        private int _thermalGridDepth;
        private int _thermalGridVersion;
        private int _thermalGridCellCount;
        private uint _equipmentTickIndex;
        private bool _wirelessBrownoutActive;
        private float _brownoutPulseTime;
        private float _equipmentCadenceAccumulator;
        private float _lastEquipmentTickInterval = 0.016f;
        private float _lastOwnerStepDeltaTime;
        private float _lastGlobalQualityWeight = 1f;
        private float _thermalGridCellSizeMeters = EquipmentDefaultCellSizeMeters;
        private double3 _thermalGridRootAup;
        private IDataVault _dataVault;
        private IThermodynamicsService _thermodynamicsService;
        private IPowerGridService _powerGridService;
        private IToolDurabilityService _toolDurabilityService;
        private IPlayerRuntimeContext _playerRuntimeContext;
        private ISubmarineRuntimeContext _submarineRuntimeContext;
        private JobHandle _equipmentIntegrationHandle;
        private IDataVault _equipmentIntegrationWriteLockVault;
        private int _equipmentIntegrationWriteLockCount;
        private ulong _equipmentIntegrationWriteGuardMask;

        private ref struct EquipmentVaultViews
        {
            public IDataVault Vault;
            public EquipmentVaultView<ToolState> ToolStates;
            public EquipmentVaultView<ToolRuntimeStats> ToolStats;
            public EquipmentVaultView<byte> ToolTypes;
            public EquipmentVaultView<float> CurrentHeat;
            public EquipmentVaultView<float> BatteryCharge;
            public EquipmentVaultView<uint> StatusMasks;
            public EquipmentVaultView<float> EnvironmentHeat01;
            public EquipmentVaultView<ActiveEquipmentDTO> ActiveEquipmentStates;
            public EquipmentVaultView<ActiveEquipmentDTO> PublishedActiveEquipmentStates;
            public EquipmentVaultView<double3> ActiveEquipmentAupSamples;
            public EquipmentVaultView<EquipmentGridLoadRequest> ActiveEquipmentGridLoadRequests;
            public EquipmentVaultView<float> ActiveEquipmentWearDrainRates;
            public EquipmentVaultView<EquipmentTelemetryEntry> EquipmentTelemetryRing;
            public EquipmentVaultView<int> EquipmentTelemetryCursor;
            public EquipmentVaultView<FlashlightTelemetryEntry> FlashlightTelemetryRing;
            public EquipmentVaultView<int> FlashlightTelemetryCursor;
            public EquipmentVaultView<EquipmentIntegrationCounters> EquipmentIntegrationCounters;
            public EquipmentVaultView<EquipmentTuningDTO> EquipmentTuning;
            public EquipmentVaultView<EquipmentHardwareSpecDTO> EquipmentHardwareSpecs;
            public EquipmentVaultView<UpgradeMaskDTO> UpgradeMasks;
            public EquipmentVaultView<UpgradeStatVectorDTO> UpgradeBaseStats;
            public EquipmentVaultView<UpgradeStatVectorDTO> UpgradeCompiledStats;
            public EquipmentVaultView<UpgradeLutEntryDTO> UpgradeToolLut;
            public EquipmentVaultView<ToolUpgradeModuleRuleDTO> UpgradeToolModuleRules;
            public EquipmentVaultView<ToolRuntimeProfile> UpgradeToolProfiles;
            public EquipmentVaultView<UpgradeTelemetryEntry> UpgradeTelemetryRing;
            public EquipmentVaultView<int> UpgradeTelemetryCursor;
            public EquipmentVaultView<UpgradeVisualStateDTO> UpgradeVisualStates;
            public int WriteLockCount;
            public ulong MutationGuardMask;
        }

        public bool IsInitialized => _isInitialized;
        public ServiceHeartbeatState HeartbeatState => IsServiceReady ? ServiceHeartbeatState.Ready : ServiceHeartbeatState.NotStarted;
        public bool IsServiceReady =>
            _isInitialized &&
            _registeredService &&
            AreEquipmentBuffersReady() &&
            _equipmentSignalLanesReady;

        public static ModularEquipmentEngine EnsureRuntimeInstance()
        {
            IModularEquipmentService registered = GlobalRegistry.ModularEquipment;
            if (IsModularEquipmentRuntimeUsable(registered))
                return registered as ModularEquipmentEngine;

            ModularEquipmentEngine staleRuntime = registered as ModularEquipmentEngine;
            if (!ReferenceEquals(staleRuntime, null))
            {
                GlobalRegistry.UnregisterModularEquipmentService(registered);
                staleRuntime._registeredService = false;
                staleRuntime._isInitialized = false;
            }
            else if (!ReferenceEquals(registered, null))
            {
                return null;
            }

            // Player-build construction path: no authored/bootstrap instance reachable.
            // Zero live scene/prefab GUID hits; without create, IModularEquipmentService stays null.
            GameObject runtimeRoot = new GameObject("[ModularEquipmentEngine]"); // COLD ALLOC: GameObject[1] - bootstrap-owned equipment runtime root - owner: ModularEquipmentEngine
            ModularEquipmentEngine engine = runtimeRoot.AddComponent<ModularEquipmentEngine>();
            engine.InitializeService();
            return engine;
        }


        public void InitializeService()
        {
            if (!TryRegisterService())
                return;

            CacheRegistryDependenciesCold();
            TryRegisterHotSwap();

            if (_isInitialized)
            {
                TryRegisterUpdatable();
                TryRegisterLateFrame();
                return;
            }

            InitializeActiveEquipmentNativeState();
            if (!TryAcquireEquipmentViewsWriteLock(out EquipmentVaultViews views))
                return;

            try
            {
                ClearNativeArray(views.ToolStates);
                ClearNativeArray(views.ToolStats);
                ClearNativeArray(views.ToolTypes);
                ClearNativeArray(views.CurrentHeat);
                ClearNativeArray(views.BatteryCharge);
                ClearNativeArray(views.StatusMasks);
                ClearNativeArray(views.EnvironmentHeat01);
            }
            finally
            {
                ReleaseEquipmentWriteLocks(ref views);
            }

            if (Application.isPlaying && transform.parent != null)
                transform.SetParent(null, true);

            _isInitialized = true;
            TryRegisterUpdatable();
            TryRegisterLateFrame();
        }

        private bool AreEquipmentBuffersReady()
        {
            return IsVaultGenerationHandleCreated(in _toolStatesHandle) &&
                   IsVaultGenerationHandleCreated(in _toolStatsHandle) &&
                   IsVaultGenerationHandleCreated(in _toolTypesHandle) &&
                   IsVaultGenerationHandleCreated(in _currentHeatHandle) &&
                   IsVaultGenerationHandleCreated(in _batteryChargeHandle) &&
                   IsVaultGenerationHandleCreated(in _statusMasksHandle) &&
                   IsVaultGenerationHandleCreated(in _environmentHeat01Handle) &&
                   IsVaultGenerationHandleCreated(in _activeEquipmentStatesHandle) &&
                   IsVaultGenerationHandleCreated(in _publishedActiveEquipmentStatesHandle) &&
                   IsVaultGenerationHandleCreated(in _activeEquipmentAupSamplesHandle) &&
                   IsVaultGenerationHandleCreated(in _activeEquipmentGridLoadRequestsHandle) &&
                   IsVaultGenerationHandleCreated(in _activeEquipmentWearDrainRatesHandle) &&
                   IsVaultGenerationHandleCreated(in _equipmentTelemetryRingHandle) &&
                   IsVaultGenerationHandleCreated(in _equipmentTelemetryCursorHandle) &&
                   IsVaultGenerationHandleCreated(in _flashlightTelemetryRingHandle) &&
                   IsVaultGenerationHandleCreated(in _flashlightTelemetryCursorHandle) &&
                   IsVaultGenerationHandleCreated(in _equipmentIntegrationCountersHandle) &&
                   IsVaultGenerationHandleCreated(in _equipmentTuningHandle) &&
                   IsVaultGenerationHandleCreated(in _equipmentHardwareSpecsHandle) &&
                   IsVaultGenerationHandleCreated(in _upgradeMatrixMasksHandle) &&
                   IsVaultGenerationHandleCreated(in _upgradeMatrixBaseStatsHandle) &&
                   IsVaultGenerationHandleCreated(in _upgradeMatrixCompiledStatsHandle) &&
                   IsVaultGenerationHandleCreated(in _upgradeMatrixToolLutHandle) &&
                   IsVaultGenerationHandleCreated(in _upgradeMatrixToolRulesHandle) &&
                   IsVaultGenerationHandleCreated(in _upgradeMatrixToolProfilesHandle) &&
                   IsVaultGenerationHandleCreated(in _upgradeMatrixTelemetryRingHandle) &&
                   IsVaultGenerationHandleCreated(in _upgradeMatrixTelemetryCursorHandle) &&
                   IsVaultGenerationHandleCreated(in _upgradeMatrixVisualStatesHandle);
        }

        public void Tick(float deltaTime)
        {
            if (!_isInitialized)
                return;

            float safeDeltaTime = math.max(0f, deltaTime);
            _lastOwnerStepDeltaTime = safeDeltaTime;

            if (_equipmentIntegrationScheduled)
                return;

            RefreshWirelessBrownoutFromPowerSnapshot();
            if (_wirelessBrownoutActive)
                _brownoutPulseTime += safeDeltaTime;

            _lastGlobalQualityWeight = ResolveGlobalQualityWeight();
            _lastEquipmentTickInterval = ResolveEquipmentTickInterval(_lastGlobalQualityWeight);
            _equipmentCadenceAccumulator += safeDeltaTime;

            if (_equipmentCadenceAccumulator < _lastEquipmentTickInterval)
                return;

            if (!TryAcquireEquipmentViewsWriteLock(out EquipmentVaultViews views))
                return;

            bool releaseViews = true;
            try
            {
                RefreshInactiveDurabilityMirrors(ref views);

                float integrationDelta = _equipmentCadenceAccumulator;
                _equipmentCadenceAccumulator = 0f;
                RefreshThermalGridReadback(out NativeArray<float>.ReadOnly thermalGridReadback);
                RefreshActiveEquipmentInputs(ref views);
                ScheduleActiveEquipmentIntegration(integrationDelta, ref views, thermalGridReadback, default);
                ScheduleToolUpgradeMatrixPostIntegration(ref views);
                if (_equipmentIntegrationScheduled)
                {
                    CaptureEquipmentIntegrationWriteLocks(ref views);
                    releaseViews = false;
                }
            }
            finally
            {
                if (releaseViews)
                    ReleaseEquipmentWriteLocks(ref views);
            }
        }

        private void RefreshInactiveDurabilityMirrors(ref EquipmentVaultViews views)
        {
            if (!views.ToolStates.IsCreated)
                return;

            int count = math.min(MaxTrackedTools, views.ToolStates.Length);
            for (int i = 0; i < MaxTrackedTools; i++)
            {
                if (i >= count)
                    break;

                PlayerTool owner = _toolOwners[i];
                if (!_slotUsed[i] || owner == null)
                    continue;

                ToolState state = views.ToolStates[i];
                uint slotBit = 1u << i;
                bool centralActive = owner.HasRuntimeActiveIntent || (_externalActiveToolMask & slotBit) != 0u;
                if (!centralActive)
                    state.Durability = math.saturate(owner.DurabilityNormalized);
                views.ToolStates[i] = state;
            }
        }

        public void LateFrameTick()
        {
            if (!_isInitialized)
                return;

            CompleteActiveEquipmentJob();
            StepFlashlightPresentationOwnerShell(_lastOwnerStepDeltaTime);
            if (TryReadActiveEquipmentNoAcquire(out NativeArray<ActiveEquipmentDTO>.ReadOnly activeStates) &&
                TryReadEquipmentIntegrationCountersNoAcquire(out NativeArray<EquipmentIntegrationCounters>.ReadOnly counters))
            {
                PublishFlashlightPresentationShaderGlobals();
                PublishFlashlightFailureShaderGlobals(activeStates, counters);
                return;
            }

            PublishInactiveFlashlightPresentationShaderGlobals();
            Shader.SetGlobalVector(FlashlightFailureStateShaderId, Vector4.zero);
        }

        private void StepFlashlightPresentationOwnerShell(float deltaTime)
        {
            IPlayerRuntimeContext playerRuntimeContext = _playerRuntimeContext;
            PlayerFlashlight flashlight = playerRuntimeContext != null ? playerRuntimeContext.Flashlight : null;
            if (flashlight == null || !flashlight.isActiveAndEnabled)
                return;

            flashlight.StepFromEquipmentOwner(deltaTime);
        }

        public uint RegisterTool(PlayerTool tool)
        {
            if (tool == null)
                return 0u;

            InitializeService();
            if (!_isInitialized)
                return 0u;

            ToolRuntimeProfile profile = tool.BuildModularRuntimeProfile();
            if (profile.ToolId == 0u)
                return 0u;

            int slotIndex = FindOrCreateSlotIndex(profile.ToolId);
            if (slotIndex < 0)
                return 0u;
            if (!TryAcquireEquipmentViewsWriteLock(out EquipmentVaultViews views))
                return 0u;

            try
            {
                int moduleSlotCount = math.clamp(
                    Mathf.Min(tool.CopyAuthoredModuleRules(_registrationRules, profile.ToolId), (int)profile.ModuleSlotCount),
                    0,
                    ToolUpgradeSystem.MaxModuleSlots);
                ulong upgradeMask64;
                ToolRuntimeStats compiledStats = ToolUpgradeSystem.CompileRuntimeStatsFromRules64(
                    profile,
                    _registrationRules,
                    moduleSlotCount,
                    out upgradeMask64);
                uint upgradeMask = (uint)(upgradeMask64 & 0xFFFFFFFFUL);

                ToolState nextState = views.ToolStates[slotIndex];
                nextState.CurrentBattery = math.saturate(tool.ResolveModularBatteryNormalized()) * math.max(0.1f, compiledStats.BatteryCapacity);
                nextState.InternalHeat = math.max(0f, tool.ResolveModularHeatNormalized());
                nextState.Durability = math.saturate(tool.DurabilityNormalized);
                nextState.UpgradeBitmask = upgradeMask;
                nextState.UpgradeBitmask64 = upgradeMask64;
                nextState.StatusMask = ResolveStatusMask(new ResolveStatusMaskContext
                {
                    CurrentStatus = 0u,
                    State = nextState,
                    Stats = compiledStats,
                    DepthMeters = ResolveDepthMeters(),
                    Active = false,
                    GridPowered = false
                });
                nextState.ToolTypeId = ResolveToolTypeId(profile.ToolId);
                nextState.ModuleSlotCount = (byte)math.clamp(moduleSlotCount, 0, ToolUpgradeSystem.MaxModuleSlots);
                nextState.Reserved0 = 0;

                if (!TryWriteUpgradeMatrixStaging(ref views, slotIndex, in profile, _registrationRules, moduleSlotCount, upgradeMask64))
                    return 0u;

                _toolOwners[slotIndex] = tool;
                _slotUsed[slotIndex] = true;
                views.ToolStates[slotIndex] = nextState;
                views.ToolStats[slotIndex] = compiledStats;
                WriteActiveEquipmentWearRate(ref views, slotIndex, tool, in compiledStats, requestedActive: false);
                WriteModuleRuleMirror(slotIndex, _registrationRules, moduleSlotCount);
                SetBatteryAbsolute(ref views, slotIndex, nextState.CurrentBattery);
                WriteActiveEquipmentSlot(ref views, slotIndex, in nextState, in compiledStats);
                WriteSlotMirrors(ref views, slotIndex, in nextState);
                RegisterDurabilityMirror(tool);
                return profile.ToolId;
            }
            finally
            {
                ReleaseEquipmentWriteLocks(ref views);
            }
        }

        public void UnregisterTool(PlayerTool tool, uint toolId)
        {
            if (!_isInitialized || tool == null || toolId == 0u)
                return;

            if (!TryResolveSlot(toolId, out int slotIndex))
                return;
            if (!TryAcquireEquipmentViewsWriteLock(out EquipmentVaultViews views))
                return;

            try
            {
                if (!ReferenceEquals(_toolOwners[slotIndex], tool))
                    return;

                ToolState previousState = views.ToolStates[slotIndex];
                PublishToolStateChanged(ref views, slotIndex, in previousState, forceHolstered: true);
                _toolOwners[slotIndex] = null;
                _slotUsed[slotIndex] = false;
                views.ToolStates[slotIndex] = default;
                views.ToolStats[slotIndex] = default;
                if (views.ActiveEquipmentWearDrainRates.IsCreated && slotIndex < views.ActiveEquipmentWearDrainRates.Length)
                    views.ActiveEquipmentWearDrainRates[slotIndex] = 0f;
                ClearActiveEquipmentSlot(ref views, slotIndex);
                ClearSlotMirrors(ref views, slotIndex);
                ClearModuleRuleMirror(slotIndex);
                ClearUpgradeMatrixStaging(ref views, slotIndex);
            }
            finally
            {
                ReleaseEquipmentWriteLocks(ref views);
            }
        }

        public bool TryGetToolState(uint toolId, out ToolState state)
        {
            state = default;
            if (!_isInitialized || !TryResolveSlot(toolId, out int slotIndex))
                return false;
            if (!TryReadToolStatesNoAcquire(out NativeArray<ToolState>.ReadOnly toolStates) ||
                (uint)slotIndex >= (uint)toolStates.Length)
                return false;

            state = toolStates[slotIndex];
            state.CurrentBattery = math.max(0f, state.CurrentBattery);
            return true;
        }

        public bool TryGetToolStats(uint toolId, out ToolRuntimeStats stats)
        {
            stats = default;
            if (!_isInitialized || !TryResolveSlot(toolId, out int slotIndex))
                return false;
            if (!TryReadToolStatsNoAcquire(out NativeArray<ToolRuntimeStats>.ReadOnly toolStats) ||
                (uint)slotIndex >= (uint)toolStats.Length)
                return false;

            stats = toolStats[slotIndex];
            return true;
        }

        public bool TryInstallModule(uint toolId, ToolModuleData module)
        {
            if (!_isInitialized || module == null || !TryResolveSlot(toolId, out int slotIndex))
                return false;

            PlayerTool owner = _toolOwners[slotIndex];
            if (owner == null || !module.IsCompatibleWith(owner.RuntimeMetadata))
                return false;

            int slotCount = GetConfiguredSlotCount(owner);
            ReadModuleRuleMirror(slotIndex, slotCount, _registrationRules);
            ToolUpgradeModuleRuleDTO moduleRule = ToolUpgradeSystem.BuildModuleRule(module, 0, toolId);
            if (!ToolUpgradeSystem.TryInsertModuleRule(_registrationRules, slotCount, moduleRule))
                return false;

            if (!RebuildCompiledState(slotIndex, owner, _registrationRules, slotCount))
                return false;

            return true;
        }

        public bool TryRemoveModule(uint toolId, string moduleId)
        {
            if (!_isInitialized || string.IsNullOrWhiteSpace(moduleId) || !TryResolveSlot(toolId, out int slotIndex))
                return false;

            PlayerTool owner = _toolOwners[slotIndex];
            if (owner == null)
                return false;

            int slotCount = GetConfiguredSlotCount(owner);
            ReadModuleRuleMirror(slotIndex, slotCount, _registrationRules);
            if (!ToolUpgradeSystem.TryRemoveModuleRule(_registrationRules, slotCount, ToolUpgradeSystem.HashModuleId(moduleId)))
                return false;

            if (!RebuildCompiledState(slotIndex, owner, _registrationRules, slotCount))
                return false;

            return true;
        }

        public bool HasUpgrade(uint toolId, ToolUpgradeBits flag)
        {
            if (!TryGetToolState(toolId, out ToolState state))
                return false;

            return (state.UpgradeBitmask64 & (ulong)flag) != 0UL;
        }

        public float GetMaxRange(uint toolId, float fallback)
        {
            return TryGetToolStats(toolId, out ToolRuntimeStats stats) ? stats.MaxRange : fallback;
        }

        public float GetPowerScalar(uint toolId, float fallback)
        {
            if (!TryGetToolStats(toolId, out ToolRuntimeStats stats))
                return fallback;

            if (!TryResolveSlot(toolId, out int slotIndex))
                return stats.PowerScalar;

            return IsOverchargeRequested(slotIndex)
                ? stats.PowerScalar * OverchargePowerMultiplier
                : stats.PowerScalar;
        }

        public float GetEfficiencyScalar(uint toolId, float fallback)
        {
            if (!TryGetToolStats(toolId, out ToolRuntimeStats stats))
                return fallback;

            if (!_wirelessBrownoutActive || !TryGetToolState(toolId, out ToolState state))
                return stats.EfficiencyScalar;

            return (state.UpgradeBitmask64 & (ulong)ToolUpgradeBits.WirelessCharging) != 0UL
                ? stats.EfficiencyScalar * 0.5f
                : stats.EfficiencyScalar;
        }

        public float GetSpeedScalar(uint toolId, float fallback)
        {
            return TryGetToolStats(toolId, out ToolRuntimeStats stats) ? stats.SpeedScalar : fallback;
        }

        public float GetHeatGenerationRate(uint toolId, float fallback)
        {
            return TryGetToolStats(toolId, out ToolRuntimeStats stats) ? stats.HeatGenerationRate : fallback;
        }

        public float GetCooldownRate(uint toolId, float fallback)
        {
            return TryGetToolStats(toolId, out ToolRuntimeStats stats) ? stats.CooldownRate : fallback;
        }

        public float GetBatteryDrainPerSecond(uint toolId, float fallback)
        {
            return TryGetToolStats(toolId, out ToolRuntimeStats stats) ? stats.BatteryDrainPerSecond : fallback;
        }

        public float GetDurabilityDrainMultiplier(uint toolId, float fallback)
        {
            return TryGetToolStats(toolId, out ToolRuntimeStats stats) ? stats.DurabilityDrainMultiplier : fallback;
        }

        public float GetRecoilImpulse(uint toolId, float fallback)
        {
            return TryGetToolStats(toolId, out ToolRuntimeStats stats) ? stats.RecoilImpulse : fallback;
        }

        public void SetBattery(uint toolId, float normalizedBattery)
        {
            if (!_isInitialized || !TryResolveSlot(toolId, out int slotIndex))
                return;
            if (!TryAcquireEquipmentViewsWriteLock(out EquipmentVaultViews views))
                return;

            try
            {
                float capacity = math.max(0.1f, views.ToolStats[slotIndex].BatteryCapacity);
                SetBatteryAbsolute(ref views, slotIndex, math.saturate(normalizedBattery) * capacity);
            }
            finally
            {
                ReleaseEquipmentWriteLocks(ref views);
            }
        }

        public float GetBatteryNormalized(uint toolId, float fallback)
        {
            if (!_isInitialized || !TryResolveSlot(toolId, out int slotIndex))
                return fallback;
            if (!TryReadToolStatesNoAcquire(out NativeArray<ToolState>.ReadOnly toolStates) ||
                !TryReadToolStatsNoAcquire(out NativeArray<ToolRuntimeStats>.ReadOnly toolStats) ||
                (uint)slotIndex >= (uint)toolStates.Length ||
                (uint)slotIndex >= (uint)toolStats.Length)
                return fallback;

            float capacity = math.max(0.1f, toolStats[slotIndex].BatteryCapacity);
            return math.saturate(math.max(0f, toolStates[slotIndex].CurrentBattery) / capacity);
        }

        public void ConsumeBattery(uint toolId, float normalizedBatteryDelta)
        {
            if (!_isInitialized || !TryResolveSlot(toolId, out int slotIndex))
                return;

            MarkSlotActive(slotIndex, math.max(0f, normalizedBatteryDelta) > 0f);
        }

        public void ConsumeBattery(uint toolId, float normalizedBatteryDrainRate, float deltaSeconds)
        {
            if (!_isInitialized || !TryResolveSlot(toolId, out int slotIndex))
                return;

            MarkSlotActive(slotIndex, math.max(0f, normalizedBatteryDrainRate) > 0f && math.max(0f, deltaSeconds) > 0f);
        }

        public void SetHeat(uint toolId, float normalizedHeat)
        {
            if (!_isInitialized || !TryResolveSlot(toolId, out int slotIndex))
                return;
            if (!TryAcquireEquipmentViewsWriteLock(out EquipmentVaultViews views))
                return;

            try
            {
                ToolState state = views.ToolStates[slotIndex];
                float sanitizedHeat = math.max(0f, normalizedHeat);
                state.InternalHeat = IsOverchargeRequested(slotIndex)
                    ? math.max(state.InternalHeat, sanitizedHeat)
                    : sanitizedHeat;
                ToolRuntimeStats stats = views.ToolStats[slotIndex];
                state.StatusMask = ResolveStatusMask(new ResolveStatusMaskContext
                {
                    CurrentStatus = state.StatusMask,
                    State = state,
                    Stats = stats,
                    DepthMeters = ResolveDepthMeters(),
                    Active = (state.StatusMask & ToolRuntimeStatusMasks.Active) != 0u,
                    GridPowered = false
                });
                views.ToolStates[slotIndex] = state;
                WriteActiveEquipmentSlot(ref views, slotIndex, in state, in stats);
                WriteSlotMirrors(ref views, slotIndex, in state);
            }
            finally
            {
                ReleaseEquipmentWriteLocks(ref views);
            }
        }

        public void SetToolActive(uint toolId, bool active)
        {
            if (!_isInitialized || !TryResolveSlot(toolId, out int slotIndex))
                return;

            MarkSlotActive(slotIndex, active);
        }

        public void SetToolActive(uint toolId, bool active, float batteryDrainPerSecond)
        {
            if (!_isInitialized || !TryResolveSlot(toolId, out int slotIndex))
                return;
            if (!TryAcquireEquipmentViewsWriteLock(out EquipmentVaultViews views))
                return;

            try
            {
                ToolRuntimeStats stats = views.ToolStats[slotIndex];
                if (active)
                {
                    stats.BatteryDrainPerSecond = math.max(0f, math.isfinite(batteryDrainPerSecond)
                        ? batteryDrainPerSecond
                        : stats.BatteryDrainPerSecond);
                    views.ToolStats[slotIndex] = stats;
                }

                MarkSlotActive(slotIndex, active);
                ToolState state = views.ToolStates[slotIndex];
                WriteActiveEquipmentSlot(ref views, slotIndex, in state, in stats);
            }
            finally
            {
                ReleaseEquipmentWriteLocks(ref views);
            }
        }

        public bool TryGetPublishedActiveEquipmentState(uint toolId, out ActiveEquipmentDTO state)
        {
            state = default;
            if (!_isInitialized ||
                !TryResolveSlot(toolId, out int slotIndex) ||
                !TryReadPublishedActiveEquipmentNoAcquire(out NativeArray<ActiveEquipmentDTO>.ReadOnly publishedStates) ||
                (uint)slotIndex >= (uint)publishedStates.Length)
            {
                return false;
            }

            state = publishedStates[slotIndex];
            return state.ToolHashID != 0u;
        }

        public bool TryGetActiveEquipmentSlot(int slotIndex, out ActiveEquipmentDTO state)
        {
            state = default;
            if (!_isInitialized ||
                !TryReadPublishedActiveEquipmentNoAcquire(out NativeArray<ActiveEquipmentDTO>.ReadOnly publishedStates) ||
                (uint)slotIndex >= (uint)publishedStates.Length)
            {
                return false;
            }

            state = publishedStates[slotIndex];
            return state.ToolHashID != 0u;
        }

        public bool TryGetLatestEquipmentTelemetry(out EquipmentTelemetryEntry entry)
        {
            entry = default;
            if (!TryReadEquipmentTelemetryNoAcquire(
                    out NativeArray<EquipmentTelemetryEntry>.ReadOnly telemetryRing,
                    out NativeArray<int>.ReadOnly telemetryCursor))
            {
                return false;
            }

            int cursor = telemetryCursor[0];
            int index = ResolveTelemetryHistoryIndex(cursor, 0, telemetryRing.Length);
            if (index < 0)
                return false;

            entry = telemetryRing[index];
            return entry.TickIndex != 0u || entry.Frame != 0u;
        }

        public bool TryGetEquipmentTelemetryEntry(int historyIndex, out EquipmentTelemetryEntry entry)
        {
            entry = default;
            if (!TryReadEquipmentTelemetryNoAcquire(
                    out NativeArray<EquipmentTelemetryEntry>.ReadOnly telemetryRing,
                    out NativeArray<int>.ReadOnly telemetryCursor) ||
                (uint)historyIndex >= (uint)telemetryRing.Length)
            {
                return false;
            }

            int index = ResolveTelemetryHistoryIndex(telemetryCursor[0], historyIndex, telemetryRing.Length);
            if (index < 0)
                return false;

            entry = telemetryRing[index];
            return entry.TickIndex != 0u || entry.Frame != 0u;
        }

        public bool TryGetLatestFlashlightTelemetry(out FlashlightTelemetryEntry entry)
        {
            entry = default;
            if (!TryReadFlashlightTelemetryNoAcquire(
                    out NativeArray<FlashlightTelemetryEntry>.ReadOnly telemetryRing,
                    out NativeArray<int>.ReadOnly telemetryCursor))
            {
                return false;
            }

            int cursor = telemetryCursor[0];
            int index = ResolveTelemetryHistoryIndex(cursor, 0, telemetryRing.Length);
            if (index < 0)
                return false;

            entry = telemetryRing[index];
            return entry.ToolHashID != 0u || entry.Frame != 0u;
        }

        public bool TryGetFlashlightTelemetryEntry(int historyIndex, out FlashlightTelemetryEntry entry)
        {
            entry = default;
            if (!TryReadFlashlightTelemetryNoAcquire(
                    out NativeArray<FlashlightTelemetryEntry>.ReadOnly telemetryRing,
                    out NativeArray<int>.ReadOnly telemetryCursor) ||
                (uint)historyIndex >= (uint)telemetryRing.Length)
            {
                return false;
            }

            int index = ResolveTelemetryHistoryIndex(telemetryCursor[0], historyIndex, telemetryRing.Length);
            if (index < 0)
                return false;

            entry = telemetryRing[index];
            return entry.ToolHashID != 0u || entry.Frame != 0u;
        }

        private static int ResolveTelemetryHistoryIndex(int cursor, int historyIndex, int ringLength)
        {
            if (ringLength <= 0)
                return -1;

            int safeHistory = math.clamp(historyIndex, 0, ringLength - 1);
            int index = cursor - 1 - safeHistory;
            if ((uint)index < (uint)ringLength)
                return index;

            index %= ringLength;
            if (index < 0)
                index += ringLength;
            return index;
        }

        public bool TryGetEquipmentTuning(out EquipmentTuningDTO tuning)
        {
            tuning = default;
            if (!TryResolveEquipmentTuningNoAcquire(out tuning))
                return false;

            return true;
        }

        public void SetEquipmentTuning(in EquipmentTuningDTO tuning)
        {
            if (!TryAcquireEquipmentViewsWriteLock(out EquipmentVaultViews views))
                return;

            try
            {
                if (views.EquipmentTuning.Length <= 0)
                    return;

                EquipmentTuningDTO sanitized = SanitizeEquipmentTuning(tuning);
                unsafe
                {
                    EquipmentTuningDTO* tuningPtr = (EquipmentTuningDTO*)views.EquipmentTuning.GetUnsafePtr();
                    ref EquipmentTuningDTO tuningRef = ref UnsafeUtility.AsRef<EquipmentTuningDTO>(tuningPtr);
                    tuningRef = sanitized;
                }
            }
            finally
            {
                ReleaseEquipmentWriteLocks(ref views);
            }
        }

        public bool SetEquipmentSlotRatesForEditor(int slotIndex, float powerDrawRate, float heatGenerationRate)
        {
            if ((uint)slotIndex >= MaxTrackedTools)
                return false;
            if (!TryAcquireEquipmentViewsWriteLock(out EquipmentVaultViews views))
                return false;

            try
            {
                if ((uint)slotIndex >= (uint)views.ActiveEquipmentStates.Length)
                    return false;

                ActiveEquipmentDTO dto = views.ActiveEquipmentStates[slotIndex];
                if (dto.ToolHashID == 0u && !_slotUsed[slotIndex])
                    return false;

                float safePower = math.max(0f, math.isfinite(powerDrawRate) ? powerDrawRate : dto.PowerDrawRate);
                float safeHeat = math.max(0f, math.isfinite(heatGenerationRate) ? heatGenerationRate : dto.HeatGenerationRate);
                dto.PowerDrawRate = safePower;
                dto.HeatGenerationRate = safeHeat;
                unsafe
                {
                    ActiveEquipmentDTO* activePtr = (ActiveEquipmentDTO*)views.ActiveEquipmentStates.GetUnsafePtr();
                    ref ActiveEquipmentDTO activeRef = ref UnsafeUtility.AsRef<ActiveEquipmentDTO>(activePtr + slotIndex);
                    activeRef = dto;
                }

                if (views.PublishedActiveEquipmentStates.IsCreated && (uint)slotIndex < (uint)views.PublishedActiveEquipmentStates.Length)
                {
                    unsafe
                    {
                        ActiveEquipmentDTO* publishedPtr = (ActiveEquipmentDTO*)views.PublishedActiveEquipmentStates.GetUnsafePtr();
                        ref ActiveEquipmentDTO publishedRef = ref UnsafeUtility.AsRef<ActiveEquipmentDTO>(publishedPtr + slotIndex);
                        publishedRef = dto;
                    }
                }

                if (views.ToolStats.IsCreated && (uint)slotIndex < (uint)views.ToolStats.Length && _slotUsed[slotIndex])
                {
                    ToolRuntimeStats stats = views.ToolStats[slotIndex];
                    float capacity = math.max(0.1f, stats.BatteryCapacity);
                    stats.BatteryDrainPerSecond = safePower * math.rcp(capacity);
                    stats.HeatGenerationRate = safeHeat * math.rcp(math.max(0.0001f, stats.PowerScalar));
                    views.ToolStats[slotIndex] = stats;
                }

                return true;
            }
            finally
            {
                ReleaseEquipmentWriteLocks(ref views);
            }
        }

        public unsafe void GenerateMockEquipmentState()
        {
            if (_equipmentIntegrationScheduled)
            {
                if (!_equipmentIntegrationHandle.IsCompleted)
                    return;

                CompleteActiveEquipmentJob();
                if (_equipmentIntegrationScheduled)
                    return;
            }

            if (!_isInitialized ||
                !TryAcquireEquipmentViewsWriteLock(out EquipmentVaultViews views))
            {
                return;
            }

            try
            {
                if (!TryResolvePlayerEquipmentAup(out double3 rootAup))
                    rootAup = double3.zero;
                GenerateMockThermalEquipmentJob job = new GenerateMockThermalEquipmentJob
                {
                    Equipment = (ActiveEquipmentDTO*)views.ActiveEquipmentStates.GetUnsafePtr(),
                    ToolAups = (double3*)views.ActiveEquipmentAupSamples.GetUnsafePtr(),
                    ToolCount = math.min(5, views.ActiveEquipmentStates.Length),
                    RootAup = rootAup,
                    BaseToolHash = EquipmentMockBaseToolHash
                };
                JobHandle mockHandle = job.Schedule(MaxTrackedTools, 4);
                H8Memory.RegisterActiveJob(EquipmentVaultOwnerSystemId, mockHandle);
                // COLD SYNC JOB: editor/CI mock state must be visible before publication; this path is not the gameplay tick.
                DispatcherJobFence.TryComplete(ref mockHandle, forceComplete: true);

                PublishActiveEquipmentReadback(ref views);
            }
            finally
            {
                ReleaseEquipmentWriteLocks(ref views);
            }
        }

        public void SetDurability(uint toolId, float normalizedDurability)
        {
            if (!_isInitialized || !TryResolveSlot(toolId, out int slotIndex))
                return;
            if (!TryAcquireEquipmentViewsWriteLock(out EquipmentVaultViews views))
                return;

            try
            {
                ToolState state = views.ToolStates[slotIndex];
                state.Durability = math.saturate(normalizedDurability);
                ToolRuntimeStats stats = views.ToolStats[slotIndex];
                state.StatusMask = ResolveStatusMask(new ResolveStatusMaskContext
                {
                    CurrentStatus = state.StatusMask,
                    State = state,
                    Stats = stats,
                    DepthMeters = ResolveDepthMeters(),
                    Active = (state.StatusMask & ToolRuntimeStatusMasks.Active) != 0u,
                    GridPowered = false
                });
                views.ToolStates[slotIndex] = state;
                WriteActiveEquipmentSlot(ref views, slotIndex, in state, in stats);
                WriteSlotMirrors(ref views, slotIndex, in state);
                SyncDurabilityMirror(slotIndex, in state);
            }
            finally
            {
                ReleaseEquipmentWriteLocks(ref views);
            }
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (Selection.activeGameObject != gameObject ||
                !TryReadPublishedActiveEquipmentNoAcquire(out NativeArray<ActiveEquipmentDTO>.ReadOnly publishedStates))
            {
                return;
            }

            int count = math.min(MaxTrackedTools, publishedStates.Length);
            Vector3 root = transform.position;
            for (int i = 0; i < count; i++)
            {
                ActiveEquipmentDTO state = publishedStates[i];
                if (state.ToolHashID == 0u)
                    continue;

                float heat = math.saturate(state.ThermalLoad);
                Vector3 position = root + (Vector3.up * (1.25f + (i * 0.14f))) + (Vector3.right * (i * 0.08f));
                Gizmos.color = Color.Lerp(Color.blue, Color.red, heat);
                Gizmos.DrawWireSphere(position, 0.10f + (heat * 0.12f));
                Handles.Label(
                    position + Vector3.up * 0.10f,
                    "0x" + state.ToolHashID.ToString("X8") +
                    " B " + state.CurrentBattery.ToString("0.0") +
                    " H " + state.ThermalLoad.ToString("0.000"));
            }
        }
#endif

        private void OnEnable()
        {
            if (_runtimeOwnerAborted || !TryRegisterService())
                return;

            CacheRegistryDependenciesCold();
            TryRegisterHotSwap();
            SceneManager.sceneUnloaded -= HandleSceneUnloaded;
            SceneManager.sceneUnloaded += HandleSceneUnloaded;

            if (_isInitialized)
            {
                TryRegisterUpdatable();
                TryRegisterLateFrame();
            }
        }

        private void OnDisable()
        {
            if (_runtimeOwnerAborted)
                return;

            SceneManager.sceneUnloaded -= HandleSceneUnloaded;
            if (!DrainEquipmentIntegrationLocksForLifecycle())
                TryRecordEquipmentWriteLockContention(EquipmentFaultWriteLockReleaseFailure);
            TryUnregisterHotSwap();
            TryUnregisterService();
            TryUnregisterUpdatable();
            TryUnregisterLateFrame();
        }

        private void OnDestroy()
        {
            if (_runtimeOwnerAborted)
                return;

            ShutdownServiceState();
        }

        public void OnServiceShutdown()
        {
            ShutdownServiceState();
        }

        private void ShutdownServiceState()
        {
            if (_runtimeOwnerAborted)
                return;

            SceneManager.sceneUnloaded -= HandleSceneUnloaded;
            TryUnregisterHotSwap();
            TryUnregisterService();
            TryUnregisterUpdatable();
            TryUnregisterLateFrame();
            DisposeNativeState();
        }

        private bool CanOwnServiceSlot()
        {
            return TryRegisterService();
        }

        private void InitializeActiveEquipmentNativeState()
        {
            EquipmentLayoutVerifier.Validate();

            if (!TryAcquireEquipmentViewsWriteLock(out EquipmentVaultViews views, createIfMissing: true))
                return;

            try
            {
                ClearActiveEquipmentNativeState(ref views);
                InitializeEquipmentTuningBuffer(ref views);
                InitializeFlashlightTelemetryBuffer(ref views);
                InitializeUpgradeTelemetryBuffer(ref views);
            }
            finally
            {
                ReleaseEquipmentWriteLocks(ref views);
            }

            EnsureEquipmentSignalLanes();
        }

        private void EnsureEquipmentSignalLanes()
        {
            if (_equipmentSignalLanesReady)
                return;

            if (_dataVault == null)
                return;

            SignalBus<EquipmentOverheatSignal>.Configure(EquipmentSignalQueueCapacity, 128, 16, EquipmentOverheatLaneHash);
            SignalBus<EquipmentOverheatSignal>.EnsureInitialized();

            SignalBus<ToolDepletedSignal>.Configure(EquipmentSignalQueueCapacity, 128, 16, ToolDepletedLaneHash);
            SignalBus<ToolDepletedSignal>.EnsureInitialized();
            _equipmentSignalLanesReady = true;
        }

        private void InitializeEquipmentTuningBuffer(ref EquipmentVaultViews views)
        {
            if (!views.EquipmentTuning.IsCreated || views.EquipmentTuning.Length <= 0)
                return;

            views.EquipmentTuning[0] = EquipmentTuningDTO.CreateDefault(_lastGlobalQualityWeight);
        }

        private void InitializeUpgradeTelemetryBuffer(ref EquipmentVaultViews views)
        {
            _upgradeTelemetryFaultDumped = false;
            _upgradeTelemetryFaultDumpPending = false;
            if (views.UpgradeTelemetryCursor.IsCreated && views.UpgradeTelemetryCursor.Length > 0)
                views.UpgradeTelemetryCursor[0] = 0;
        }

        private void InitializeFlashlightTelemetryBuffer(ref EquipmentVaultViews views)
        {
            _equipmentFaultDumped = false;
            _equipmentFaultDumpPending = false;
            if (views.FlashlightTelemetryCursor.IsCreated && views.FlashlightTelemetryCursor.Length > 0)
                views.FlashlightTelemetryCursor[0] = 0;
        }

        private bool EnsureEquipmentViews(out EquipmentVaultViews views, bool createIfMissing = false)
        {
            return EnsureEquipmentViews(_dataVault, out views, createIfMissing);
        }

        private bool EnsureEquipmentViews(IDataVault vault, out EquipmentVaultViews views, bool createIfMissing = false)
        {
            views = default;
            if (vault == null)
                return false;

            bool resolved =
                EnsureEquipmentBuffer(vault, ref _toolStatesHandle, BufferID.ShinobuActiveEquipmentToolStates, MaxTrackedTools, NativeArrayOptions.UninitializedMemory, createIfMissing, out views.ToolStates) &&
                EnsureEquipmentBuffer(vault, ref _toolStatsHandle, BufferID.ShinobuActiveEquipmentToolStats, MaxTrackedTools, NativeArrayOptions.UninitializedMemory, createIfMissing, out views.ToolStats) &&
                EnsureEquipmentBuffer(vault, ref _toolTypesHandle, BufferID.ShinobuActiveEquipmentToolTypes, MaxTrackedTools, NativeArrayOptions.UninitializedMemory, createIfMissing, out views.ToolTypes) &&
                EnsureEquipmentBuffer(vault, ref _currentHeatHandle, BufferID.ToolRuntimeHeat01, MaxTrackedTools, NativeArrayOptions.UninitializedMemory, createIfMissing, out views.CurrentHeat) &&
                EnsureEquipmentBuffer(vault, ref _batteryChargeHandle, BufferID.ToolRuntimeBatteryCharge, MaxTrackedTools, NativeArrayOptions.UninitializedMemory, createIfMissing, out views.BatteryCharge) &&
                EnsureEquipmentBuffer(vault, ref _statusMasksHandle, BufferID.ShinobuActiveEquipmentStatusMasks, MaxTrackedTools, NativeArrayOptions.UninitializedMemory, createIfMissing, out views.StatusMasks) &&
                EnsureEquipmentBuffer(vault, ref _environmentHeat01Handle, BufferID.ShinobuActiveEquipmentEnvironmentHeat01, MaxTrackedTools, NativeArrayOptions.UninitializedMemory, createIfMissing, out views.EnvironmentHeat01) &&
                EnsureEquipmentBuffer(vault, ref _activeEquipmentStatesHandle, BufferID.ShinobuActiveEquipmentState, MaxTrackedTools, NativeArrayOptions.UninitializedMemory, createIfMissing, out views.ActiveEquipmentStates) &&
                EnsureEquipmentBuffer(vault, ref _publishedActiveEquipmentStatesHandle, BufferID.ShinobuActiveEquipmentPublishedState, MaxTrackedTools, NativeArrayOptions.UninitializedMemory, createIfMissing, out views.PublishedActiveEquipmentStates) &&
                EnsureEquipmentBuffer(vault, ref _activeEquipmentAupSamplesHandle, BufferID.ShinobuActiveEquipmentAupSamples, MaxTrackedTools, NativeArrayOptions.UninitializedMemory, createIfMissing, out views.ActiveEquipmentAupSamples) &&
                EnsureEquipmentBuffer(vault, ref _activeEquipmentGridLoadRequestsHandle, BufferID.ShinobuActiveEquipmentGridLoadRequests, MaxTrackedTools, NativeArrayOptions.UninitializedMemory, createIfMissing, out views.ActiveEquipmentGridLoadRequests) &&
                EnsureEquipmentBuffer(vault, ref _activeEquipmentWearDrainRatesHandle, BufferID.ShinobuActiveEquipmentWearDrainRates, MaxTrackedTools, NativeArrayOptions.UninitializedMemory, createIfMissing, out views.ActiveEquipmentWearDrainRates) &&
                EnsureEquipmentBuffer(vault, ref _equipmentTelemetryRingHandle, BufferID.ShinobuActiveEquipmentTelemetryRing, EquipmentTelemetryRingLength, NativeArrayOptions.UninitializedMemory, createIfMissing, out views.EquipmentTelemetryRing) &&
                EnsureEquipmentBuffer(vault, ref _equipmentTelemetryCursorHandle, BufferID.ShinobuActiveEquipmentTelemetryCursor, 1, NativeArrayOptions.UninitializedMemory, createIfMissing, out views.EquipmentTelemetryCursor) &&
                EnsureEquipmentBuffer(vault, ref _flashlightTelemetryRingHandle, FlashlightTelemetryRingBufferId, EquipmentTelemetryRingLength, NativeArrayOptions.UninitializedMemory, createIfMissing, out views.FlashlightTelemetryRing) &&
                EnsureEquipmentBuffer(vault, ref _flashlightTelemetryCursorHandle, FlashlightTelemetryCursorBufferId, 1, NativeArrayOptions.UninitializedMemory, createIfMissing, out views.FlashlightTelemetryCursor) &&
                EnsureEquipmentBuffer(vault, ref _equipmentIntegrationCountersHandle, BufferID.ShinobuActiveEquipmentIntegrationCounters, MaxTrackedTools, NativeArrayOptions.UninitializedMemory, createIfMissing, out views.EquipmentIntegrationCounters) &&
                EnsureEquipmentBuffer(vault, ref _equipmentTuningHandle, BufferID.ShinobuActiveEquipmentTuning, 1, NativeArrayOptions.UninitializedMemory, createIfMissing, out views.EquipmentTuning) &&
                EnsureEquipmentBuffer(vault, ref _equipmentHardwareSpecsHandle, BufferID.ShinobuActiveEquipmentHardwareSpecs, EquipmentHardwareSpecCapacity, NativeArrayOptions.UninitializedMemory, createIfMissing, out views.EquipmentHardwareSpecs) &&
                EnsureEquipmentBuffer(vault, ref _upgradeMatrixMasksHandle, UpgradeMatrixConstants.UpgradeMasksBuffer, MaxTrackedTools, NativeArrayOptions.UninitializedMemory, createIfMissing, out views.UpgradeMasks) &&
                EnsureEquipmentBuffer(vault, ref _upgradeMatrixBaseStatsHandle, UpgradeMatrixConstants.UpgradeBaseStatsBuffer, MaxTrackedTools, NativeArrayOptions.UninitializedMemory, createIfMissing, out views.UpgradeBaseStats) &&
                EnsureEquipmentBuffer(vault, ref _upgradeMatrixCompiledStatsHandle, UpgradeMatrixConstants.UpgradeCompiledStatsBuffer, MaxTrackedTools, NativeArrayOptions.UninitializedMemory, createIfMissing, out views.UpgradeCompiledStats) &&
                EnsureEquipmentBuffer(vault, ref _upgradeMatrixToolLutHandle, UpgradeMatrixConstants.UpgradeLutBuffer, MaxTrackedTools * UpgradeMatrixConstants.ToolModuleLutEntriesPerEquipment, NativeArrayOptions.UninitializedMemory, createIfMissing, out views.UpgradeToolLut) &&
                EnsureEquipmentBuffer(vault, ref _upgradeMatrixToolRulesHandle, UpgradeMatrixConstants.UpgradeToolModuleRulesBuffer, MaxTrackedTools * UpgradeMatrixConstants.ToolModuleSlotsPerEquipment, NativeArrayOptions.UninitializedMemory, createIfMissing, out views.UpgradeToolModuleRules) &&
                EnsureEquipmentBuffer(vault, ref _upgradeMatrixToolProfilesHandle, UpgradeMatrixConstants.UpgradeToolProfilesBuffer, MaxTrackedTools, NativeArrayOptions.UninitializedMemory, createIfMissing, out views.UpgradeToolProfiles) &&
                EnsureEquipmentBuffer(vault, ref _upgradeMatrixTelemetryRingHandle, UpgradeMatrixConstants.UpgradeTelemetryRingBuffer, UpgradeMatrixConstants.TelemetryFrameCount, NativeArrayOptions.UninitializedMemory, createIfMissing, out views.UpgradeTelemetryRing) &&
                EnsureEquipmentBuffer(vault, ref _upgradeMatrixTelemetryCursorHandle, UpgradeMatrixConstants.UpgradeTelemetryCursorBuffer, 1, NativeArrayOptions.UninitializedMemory, createIfMissing, out views.UpgradeTelemetryCursor) &&
                EnsureEquipmentBuffer(vault, ref _upgradeMatrixVisualStatesHandle, UpgradeMatrixConstants.UpgradeVisualStateBuffer, MaxTrackedTools, NativeArrayOptions.UninitializedMemory, createIfMissing, out views.UpgradeVisualStates);

            if (resolved)
                views.Vault = vault;
            return resolved;
        }

        private static bool EnsureEquipmentBuffer<T>(
            IDataVault vault,
            ref VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength,
            NativeArrayOptions options,
            bool createIfMissing,
            out EquipmentVaultView<T> view)
            where T : unmanaged
        {
            if (TryResolveEquipmentBuffer(vault, in handle, requiredLength, out NativeArray<T> buffer))
            {
                view = new EquipmentVaultView<T>(buffer);
                return true;
            }

            view = default;
            if (vault == null || requiredLength <= 0)
                return false;

            if (!createIfMissing)
                return false;

            if (!ReleaseEquipmentVaultHandle(vault, ref handle))
                return false;

            handle = vault.EnsureGenerationHandle<T>(
                bufferId,
                requiredLength,
                SystemID.GameplayTools,
                options);

            if (!TryResolveEquipmentBuffer(vault, in handle, requiredLength, out buffer))
                return false;

            view = new EquipmentVaultView<T>(buffer);
            return true;
        }

        private bool TryAcquireEquipmentViewsWriteLock(out EquipmentVaultViews views, bool createIfMissing = false)
        {
            views = default;
            IDataVault vault = _dataVault;
            if (vault == null)
                return false;

            if (createIfMissing && !EnsureEquipmentViews(vault, out _, createIfMissing: true))
                return false;

            ulong guardMask = EquipmentViewsMutationGuardMask;
            if (!vault.TryAcquireMutationGuard(guardMask))
            {
                TryRecordEquipmentWriteLockContention();
                return false;
            }

            int acquiredCount = 0;
            bool acquiredAll = false;
            try
            {
                if (!TryAcquireEquipmentWriteBuffer(vault, in _toolStatesHandle, MaxTrackedTools, out views.ToolStates)) return false;
                acquiredCount++;
                if (!TryAcquireEquipmentWriteBuffer(vault, in _toolStatsHandle, MaxTrackedTools, out views.ToolStats)) return false;
                acquiredCount++;
                if (!TryAcquireEquipmentWriteBuffer(vault, in _toolTypesHandle, MaxTrackedTools, out views.ToolTypes)) return false;
                acquiredCount++;
                if (!TryAcquireEquipmentWriteBuffer(vault, in _currentHeatHandle, MaxTrackedTools, out views.CurrentHeat)) return false;
                acquiredCount++;
                if (!TryAcquireEquipmentWriteBuffer(vault, in _batteryChargeHandle, MaxTrackedTools, out views.BatteryCharge)) return false;
                acquiredCount++;
                if (!TryAcquireEquipmentWriteBuffer(vault, in _statusMasksHandle, MaxTrackedTools, out views.StatusMasks)) return false;
                acquiredCount++;
                if (!TryAcquireEquipmentWriteBuffer(vault, in _environmentHeat01Handle, MaxTrackedTools, out views.EnvironmentHeat01)) return false;
                acquiredCount++;
                if (!TryAcquireEquipmentWriteBuffer(vault, in _activeEquipmentStatesHandle, MaxTrackedTools, out views.ActiveEquipmentStates)) return false;
                acquiredCount++;
                if (!TryAcquireEquipmentWriteBuffer(vault, in _publishedActiveEquipmentStatesHandle, MaxTrackedTools, out views.PublishedActiveEquipmentStates)) return false;
                acquiredCount++;
                if (!TryAcquireEquipmentWriteBuffer(vault, in _activeEquipmentAupSamplesHandle, MaxTrackedTools, out views.ActiveEquipmentAupSamples)) return false;
                acquiredCount++;
                if (!TryAcquireEquipmentWriteBuffer(vault, in _activeEquipmentGridLoadRequestsHandle, MaxTrackedTools, out views.ActiveEquipmentGridLoadRequests)) return false;
                acquiredCount++;
                if (!TryAcquireEquipmentWriteBuffer(vault, in _activeEquipmentWearDrainRatesHandle, MaxTrackedTools, out views.ActiveEquipmentWearDrainRates)) return false;
                acquiredCount++;
                if (!TryAcquireEquipmentWriteBuffer(vault, in _equipmentTelemetryRingHandle, EquipmentTelemetryRingLength, out views.EquipmentTelemetryRing)) return false;
                acquiredCount++;
                if (!TryAcquireEquipmentWriteBuffer(vault, in _equipmentTelemetryCursorHandle, 1, out views.EquipmentTelemetryCursor)) return false;
                acquiredCount++;
                if (!TryAcquireEquipmentWriteBuffer(vault, in _flashlightTelemetryRingHandle, EquipmentTelemetryRingLength, out views.FlashlightTelemetryRing)) return false;
                acquiredCount++;
                if (!TryAcquireEquipmentWriteBuffer(vault, in _flashlightTelemetryCursorHandle, 1, out views.FlashlightTelemetryCursor)) return false;
                acquiredCount++;
                if (!TryAcquireEquipmentWriteBuffer(vault, in _equipmentIntegrationCountersHandle, MaxTrackedTools, out views.EquipmentIntegrationCounters)) return false;
                acquiredCount++;
                if (!TryAcquireEquipmentWriteBuffer(vault, in _equipmentTuningHandle, 1, out views.EquipmentTuning)) return false;
                acquiredCount++;
                if (!TryAcquireEquipmentWriteBuffer(vault, in _equipmentHardwareSpecsHandle, EquipmentHardwareSpecCapacity, out views.EquipmentHardwareSpecs)) return false;
                acquiredCount++;
                if (!TryAcquireEquipmentWriteBuffer(vault, in _upgradeMatrixMasksHandle, MaxTrackedTools, out views.UpgradeMasks)) return false;
                acquiredCount++;
                if (!TryAcquireEquipmentWriteBuffer(vault, in _upgradeMatrixBaseStatsHandle, MaxTrackedTools, out views.UpgradeBaseStats)) return false;
                acquiredCount++;
                if (!TryAcquireEquipmentWriteBuffer(vault, in _upgradeMatrixCompiledStatsHandle, MaxTrackedTools, out views.UpgradeCompiledStats)) return false;
                acquiredCount++;
                if (!TryAcquireEquipmentWriteBuffer(vault, in _upgradeMatrixToolLutHandle, MaxTrackedTools * UpgradeMatrixConstants.ToolModuleLutEntriesPerEquipment, out views.UpgradeToolLut)) return false;
                acquiredCount++;
                if (!TryAcquireEquipmentWriteBuffer(vault, in _upgradeMatrixToolRulesHandle, MaxTrackedTools * UpgradeMatrixConstants.ToolModuleSlotsPerEquipment, out views.UpgradeToolModuleRules)) return false;
                acquiredCount++;
                if (!TryAcquireEquipmentWriteBuffer(vault, in _upgradeMatrixToolProfilesHandle, MaxTrackedTools, out views.UpgradeToolProfiles)) return false;
                acquiredCount++;
                if (!TryAcquireEquipmentWriteBuffer(vault, in _upgradeMatrixTelemetryRingHandle, UpgradeMatrixConstants.TelemetryFrameCount, out views.UpgradeTelemetryRing)) return false;
                acquiredCount++;
                if (!TryAcquireEquipmentWriteBuffer(vault, in _upgradeMatrixTelemetryCursorHandle, 1, out views.UpgradeTelemetryCursor)) return false;
                acquiredCount++;
                if (!TryAcquireEquipmentWriteBuffer(vault, in _upgradeMatrixVisualStatesHandle, MaxTrackedTools, out views.UpgradeVisualStates)) return false;
                acquiredCount++;

                if (acquiredCount != EquipmentWriteLockBufferCount)
                    return false;

                views.Vault = vault;
                views.WriteLockCount = acquiredCount;
                views.MutationGuardMask = guardMask;
                acquiredAll = true;
                return true;
            }
            finally
            {
                if (!acquiredAll)
                {
                    vault.ReleaseMutationGuard(guardMask);
                    views = default;
                    TryRecordEquipmentWriteLockContention();
                }
            }
        }

        private static bool TryAcquireEquipmentWriteBuffer<T>(
            IDataVault vault,
            in VaultGenerationHandle<T> handle,
            int requiredLength,
            out EquipmentVaultView<T> view)
            where T : unmanaged
        {
            view = default;
            if (!TryResolveEquipmentBuffer(vault, in handle, requiredLength, out NativeArray<T> buffer))
                return false;

            view = new EquipmentVaultView<T>(buffer);
            return true;
        }

        private void ReleaseEquipmentWriteLocks(ref EquipmentVaultViews views)
        {
            IDataVault vault = views.Vault;
            int acquiredCount = views.WriteLockCount;
            ulong guardMask = views.MutationGuardMask;
            views.Vault = null;
            views.WriteLockCount = 0;
            views.MutationGuardMask = 0UL;
            ReleaseEquipmentWriteLocks(vault, acquiredCount, guardMask);
        }

        private void CaptureEquipmentIntegrationWriteLocks(ref EquipmentVaultViews views)
        {
            _equipmentIntegrationWriteLockVault = views.Vault;
            _equipmentIntegrationWriteLockCount = views.WriteLockCount;
            _equipmentIntegrationWriteGuardMask = views.MutationGuardMask;
            views.Vault = null;
            views.WriteLockCount = 0;
            views.MutationGuardMask = 0UL;
        }

        private bool TryResolveCapturedEquipmentIntegrationViews(out EquipmentVaultViews views)
        {
            views = default;
            if (_equipmentIntegrationWriteLockCount != EquipmentWriteLockBufferCount)
            {
                TryRecordEquipmentWriteLockContention(EquipmentFaultWriteLockReleaseFailure);
                return false;
            }

            if (!EnsureEquipmentViews(_equipmentIntegrationWriteLockVault, out views))
                return false;

            views.WriteLockCount = _equipmentIntegrationWriteLockCount;
            views.MutationGuardMask = _equipmentIntegrationWriteGuardMask;
            return true;
        }

        private void ReleaseEquipmentIntegrationWriteLocks()
        {
            IDataVault vault = _equipmentIntegrationWriteLockVault;
            int acquiredCount = _equipmentIntegrationWriteLockCount;
            ulong guardMask = _equipmentIntegrationWriteGuardMask;
            _equipmentIntegrationWriteLockVault = null;
            _equipmentIntegrationWriteLockCount = 0;
            _equipmentIntegrationWriteGuardMask = 0UL;
            ReleaseEquipmentWriteLocks(vault, acquiredCount, guardMask);
        }

        private void ReleaseEquipmentWriteLocks(IDataVault vault, int acquiredCount, ulong guardMask)
        {
            if (vault == null || acquiredCount <= 0 || guardMask == 0UL)
                return;

            vault.ReleaseMutationGuard(guardMask);
        }

        private void TryRecordEquipmentWriteLockContention(uint faultFlags = EquipmentFaultWriteLockContention)
        {
            IDataVault vault = _dataVault;
            if (vault == null ||
                !IsVaultGenerationHandleCreated(in _equipmentTelemetryRingHandle) ||
                !IsVaultGenerationHandleCreated(in _equipmentTelemetryCursorHandle))
            {
                return;
            }

            if (!vault.TryAcquireMutationGuard(EquipmentFaultTelemetryMutationGuardMask))
                return;

            try
            {
                if (!vault.TryResolveHandle(in _equipmentTelemetryCursorHandle, out NativeArray<int> cursor) ||
                    !vault.TryResolveHandle(in _equipmentTelemetryRingHandle, out NativeArray<EquipmentTelemetryEntry> ring) ||
                    !cursor.IsCreated ||
                    cursor.Length <= 0 ||
                    !ring.IsCreated ||
                    ring.Length <= 0)
                {
                    return;
                }

                int ringLength = math.min(ring.Length, EquipmentTelemetryRingLength);
                int index = math.clamp(cursor[0], 0, ringLength - 1);
                int nextIndex = index + 1;
                if (nextIndex >= ringLength)
                    nextIndex = 0;

                ring[index] = new EquipmentTelemetryEntry
                {
                    Frame = unchecked((uint)Time.frameCount),
                    TickIndex = _equipmentTickIndex,
                    ActiveToolMask = _lastTelemetryActiveMask,
                    SignalCount = 0u,
                    FaultFlags = faultFlags,
                    LastFaultToolHashID = 0u,
                    CpuMicroseconds = 0f,
                    GlobalQualityWeight = _lastGlobalQualityWeight,
                    TickIntervalSeconds = _lastEquipmentTickInterval,
                    ThermalGridVersion = _thermalGridVersion,
                    ThermalGridCellCount = _thermalGridCellCount,
                    SnapshotHash = 0u,
                    WearDrainNormalized = 0f
                };
                cursor[0] = nextIndex;
                _equipmentFaultDumpPending = true;
            }
            finally
            {
                vault.ReleaseMutationGuard(EquipmentFaultTelemetryMutationGuardMask);
            }
        }

        private static ulong EquipmentMutationGuardBit(BufferID bufferId)
        {
            return 1UL << (unchecked((int)bufferId) & 31);
        }

        private static bool TryResolveEquipmentBuffer<T>(
            IDataVault vault,
            in VaultGenerationHandle<T> handle,
            int requiredLength,
            out NativeArray<T> buffer)
            where T : struct
        {
            buffer = default;
            return vault != null &&
                   requiredLength > 0 &&
                   IsVaultGenerationHandleCreated(in handle) &&
                   vault.TryResolveHandle(in handle, out buffer) &&
                   buffer.IsCreated &&
                   buffer.Length >= requiredLength;
        }

        private static bool TryReadEquipmentBuffer<T>(
            IDataVault vault,
            in VaultGenerationHandle<T> handle,
            int requiredLength,
            out NativeArray<T>.ReadOnly buffer)
            where T : struct
        {
            buffer = default;
            return vault != null &&
                   requiredLength > 0 &&
                   IsVaultGenerationHandleCreated(in handle) &&
                   vault.TryReadOnlyHandle(in handle, out buffer) &&
                   buffer.Length >= requiredLength;
        }

        private bool TryReadToolStatesNoAcquire(out NativeArray<ToolState>.ReadOnly toolStates)
        {
            IDataVault vault = _dataVault;
            return TryReadEquipmentBuffer(vault, in _toolStatesHandle, MaxTrackedTools, out toolStates);
        }

        private bool TryReadToolStatsNoAcquire(out NativeArray<ToolRuntimeStats>.ReadOnly toolStats)
        {
            IDataVault vault = _dataVault;
            return TryReadEquipmentBuffer(vault, in _toolStatsHandle, MaxTrackedTools, out toolStats);
        }

        private bool TryReadPublishedActiveEquipmentNoAcquire(out NativeArray<ActiveEquipmentDTO>.ReadOnly publishedStates)
        {
            IDataVault vault = _dataVault;
            return TryReadEquipmentBuffer(vault, in _publishedActiveEquipmentStatesHandle, MaxTrackedTools, out publishedStates);
        }

        private bool TryReadActiveEquipmentNoAcquire(out NativeArray<ActiveEquipmentDTO>.ReadOnly activeStates)
        {
            IDataVault vault = _dataVault;
            return TryReadEquipmentBuffer(vault, in _activeEquipmentStatesHandle, MaxTrackedTools, out activeStates);
        }

        private bool TryReadEquipmentIntegrationCountersNoAcquire(out NativeArray<EquipmentIntegrationCounters>.ReadOnly counters)
        {
            IDataVault vault = _dataVault;
            return TryReadEquipmentBuffer(vault, in _equipmentIntegrationCountersHandle, MaxTrackedTools, out counters);
        }

        private bool TryReadEquipmentTelemetryNoAcquire(
            out NativeArray<EquipmentTelemetryEntry>.ReadOnly telemetryRing,
            out NativeArray<int>.ReadOnly telemetryCursor)
        {
            telemetryRing = default;
            telemetryCursor = default;
            IDataVault vault = _dataVault;
            return TryReadEquipmentBuffer(vault, in _equipmentTelemetryRingHandle, EquipmentTelemetryRingLength, out telemetryRing) &&
                   TryReadEquipmentBuffer(vault, in _equipmentTelemetryCursorHandle, 1, out telemetryCursor);
        }

        private bool TryReadFlashlightTelemetryNoAcquire(
            out NativeArray<FlashlightTelemetryEntry>.ReadOnly telemetryRing,
            out NativeArray<int>.ReadOnly telemetryCursor)
        {
            telemetryRing = default;
            telemetryCursor = default;
            IDataVault vault = _dataVault;
            return TryReadEquipmentBuffer(vault, in _flashlightTelemetryRingHandle, EquipmentTelemetryRingLength, out telemetryRing) &&
                   TryReadEquipmentBuffer(vault, in _flashlightTelemetryCursorHandle, 1, out telemetryCursor);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsVaultGenerationHandleCreated<T>(in VaultGenerationHandle<T> handle)
            where T : struct
        {
            return handle.BufferID != 0u && handle.Generation != 0u;
        }

        private static unsafe void ClearNativeArray<T>(NativeArray<T> array)
            where T : unmanaged
        {
            if (!array.IsCreated || array.Length <= 0)
                return;

            for (int i = 0; i < array.Length; i++)
                array[i] = default;
        }

        private static unsafe void ClearNativeArray<T>(EquipmentVaultView<T> view)
            where T : unmanaged
        {
            ClearNativeArray(view.AsNativeArray());
        }

        private unsafe void ClearActiveEquipmentNativeState(ref EquipmentVaultViews views)
        {
            int jobLength = 0;
            jobLength = math.max(jobLength, GetCreatedLength(views.ActiveEquipmentStates));
            jobLength = math.max(jobLength, GetCreatedLength(views.PublishedActiveEquipmentStates));
            jobLength = math.max(jobLength, GetCreatedLength(views.ActiveEquipmentAupSamples));
            jobLength = math.max(jobLength, GetCreatedLength(views.ActiveEquipmentGridLoadRequests));
            jobLength = math.max(jobLength, GetCreatedLength(views.ActiveEquipmentWearDrainRates));
            jobLength = math.max(jobLength, GetCreatedLength(views.EquipmentTelemetryRing));
            jobLength = math.max(jobLength, GetCreatedLength(views.EquipmentTelemetryCursor));
            jobLength = math.max(jobLength, GetCreatedLength(views.FlashlightTelemetryRing));
            jobLength = math.max(jobLength, GetCreatedLength(views.FlashlightTelemetryCursor));
            jobLength = math.max(jobLength, GetCreatedLength(views.EquipmentIntegrationCounters));
            jobLength = math.max(jobLength, GetCreatedLength(views.EquipmentHardwareSpecs));
            if (jobLength <= 0)
                return;

            ClearActiveEquipmentNativeStateJob job = new ClearActiveEquipmentNativeStateJob
            {
                ActiveEquipment = views.ActiveEquipmentStates.IsCreated ? (ActiveEquipmentDTO*)views.ActiveEquipmentStates.GetUnsafePtr() : null,
                PublishedEquipment = views.PublishedActiveEquipmentStates.IsCreated ? (ActiveEquipmentDTO*)views.PublishedActiveEquipmentStates.GetUnsafePtr() : null,
                AupSamples = views.ActiveEquipmentAupSamples.IsCreated ? (double3*)views.ActiveEquipmentAupSamples.GetUnsafePtr() : null,
                GridLoadRequests = views.ActiveEquipmentGridLoadRequests.IsCreated ? (EquipmentGridLoadRequest*)views.ActiveEquipmentGridLoadRequests.GetUnsafePtr() : null,
                WearDrainRates = views.ActiveEquipmentWearDrainRates.IsCreated ? (float*)views.ActiveEquipmentWearDrainRates.GetUnsafePtr() : null,
                TelemetryRing = views.EquipmentTelemetryRing.IsCreated ? (EquipmentTelemetryEntry*)views.EquipmentTelemetryRing.GetUnsafePtr() : null,
                TelemetryCursor = views.EquipmentTelemetryCursor.IsCreated ? (int*)views.EquipmentTelemetryCursor.GetUnsafePtr() : null,
                FlashlightTelemetryRing = views.FlashlightTelemetryRing.IsCreated ? (FlashlightTelemetryEntry*)views.FlashlightTelemetryRing.GetUnsafePtr() : null,
                FlashlightTelemetryCursor = views.FlashlightTelemetryCursor.IsCreated ? (int*)views.FlashlightTelemetryCursor.GetUnsafePtr() : null,
                IntegrationCounters = views.EquipmentIntegrationCounters.IsCreated ? (EquipmentIntegrationCounters*)views.EquipmentIntegrationCounters.GetUnsafePtr() : null,
                HardwareSpecs = views.EquipmentHardwareSpecs.IsCreated ? (EquipmentHardwareSpecDTO*)views.EquipmentHardwareSpecs.GetUnsafePtr() : null,
                ActiveLength = GetCreatedLength(views.ActiveEquipmentStates),
                PublishedLength = GetCreatedLength(views.PublishedActiveEquipmentStates),
                AupLength = GetCreatedLength(views.ActiveEquipmentAupSamples),
                GridLoadRequestLength = GetCreatedLength(views.ActiveEquipmentGridLoadRequests),
                WearDrainLength = GetCreatedLength(views.ActiveEquipmentWearDrainRates),
                TelemetryLength = GetCreatedLength(views.EquipmentTelemetryRing),
                CursorLength = GetCreatedLength(views.EquipmentTelemetryCursor),
                FlashlightTelemetryLength = GetCreatedLength(views.FlashlightTelemetryRing),
                FlashlightCursorLength = GetCreatedLength(views.FlashlightTelemetryCursor),
                CounterLength = GetCreatedLength(views.EquipmentIntegrationCounters),
                HardwareSpecLength = GetCreatedLength(views.EquipmentHardwareSpecs)
            };
            JobHandle clearHandle = job.Schedule(jobLength, 32);
            H8Memory.RegisterActiveJob(SystemID.GameplayTools, clearHandle);
            // COLD SYNC JOB: Vault clear must finish before equipment service registration exposes these rows.
            DispatcherJobFence.TryComplete(ref clearHandle, forceComplete: true);
        }

        private static int GetCreatedLength<T>(NativeArray<T> array)
            where T : unmanaged
        {
            return array.IsCreated ? array.Length : 0;
        }

        private static int GetCreatedLength<T>(EquipmentVaultView<T> view)
            where T : unmanaged
        {
            return view.IsCreated ? view.Length : 0;
        }

        private int FindOrCreateSlotIndex(uint toolId)
        {
            if (TryResolveSlot(toolId, out int existingIndex))
                return existingIndex;

            for (int i = 0; i < MaxTrackedTools; i++)
            {
                if (!_slotUsed[i])
                    return i;
            }

            return -1;
        }

        private bool TryResolveSlot(uint toolId, out int slotIndex)
        {
            if (TryResolveOwnerMirrorSlot(toolId, out slotIndex))
                return true;

            if (!TryReadActiveEquipmentNoAcquire(out NativeArray<ActiveEquipmentDTO>.ReadOnly activeStates))
                return false;

            for (int i = 0; i < MaxTrackedTools; i++)
            {
                if (_slotUsed[i] &&
                    (uint)i < (uint)activeStates.Length &&
                    activeStates[i].ToolHashID == toolId)
                {
                    slotIndex = i;
                    return true;
                }
            }

            return false;
        }

        private bool TryResolveOwnerMirrorSlot(uint toolId, out int slotIndex)
        {
            slotIndex = -1;
            if (toolId == 0u)
                return false;

            for (int i = 0; i < MaxTrackedTools; i++)
            {
                if (!_slotUsed[i])
                    continue;

                PlayerTool owner = _toolOwners[i];
                if (owner != null && owner.RuntimeToolId == toolId)
                {
                    slotIndex = i;
                    return true;
                }
            }

            return false;
        }

        private void WriteModuleRuleMirror(int slotIndex, ToolUpgradeModuleRuleDTO[] moduleRules, int moduleSlotCount)
        {
            int baseIndex = slotIndex * ToolUpgradeSystem.MaxModuleSlots;
            for (int i = 0; i < ToolUpgradeSystem.MaxModuleSlots; i++)
                _moduleRuleSlots[baseIndex + i] = i < moduleSlotCount ? ToolUpgradeSystem.NormalizeRuleSlot(moduleRules[i], i) : default;
        }

        private bool TryWriteUpgradeMatrixStaging(
            ref EquipmentVaultViews views,
            int slotIndex,
            in ToolRuntimeProfile profile,
            ToolUpgradeModuleRuleDTO[] moduleRules,
            int moduleSlotCount,
            ulong originalUpgradeMask)
        {
            if (!views.UpgradeMasks.IsCreated ||
                !views.UpgradeBaseStats.IsCreated ||
                !views.UpgradeCompiledStats.IsCreated ||
                !views.UpgradeToolProfiles.IsCreated ||
                !views.UpgradeToolModuleRules.IsCreated ||
                (uint)slotIndex >= (uint)views.UpgradeMasks.Length ||
                (uint)slotIndex >= (uint)views.UpgradeBaseStats.Length ||
                (uint)slotIndex >= (uint)views.UpgradeCompiledStats.Length ||
                (uint)slotIndex >= (uint)views.UpgradeToolProfiles.Length)
                return false;

            int safeSlotCount = math.clamp(moduleSlotCount, 0, ToolUpgradeSystem.MaxModuleSlots);
            int ruleBase = slotIndex * UpgradeMatrixConstants.ToolModuleSlotsPerEquipment;
            if (ruleBase < 0 || ruleBase + UpgradeMatrixConstants.ToolModuleSlotsPerEquipment > views.UpgradeToolModuleRules.Length)
                return false;

            ulong slotMask = ToolUpgradeSystem.CompileInstalledRuleMask64(moduleRules, safeSlotCount, out _);
            views.UpgradeMasks[slotIndex] = new UpgradeMaskDTO
            {
                EntityHashID = profile.ToolId,
                EquipmentHashID = profile.ToolId,
                ActiveUpgradesMask = slotMask | (originalUpgradeMask & UpgradeMatrixConstants.VisualFlagMask)
            };
            views.UpgradeBaseStats[slotIndex] = ToolUpgradeSystem.CreateIdentityStatVector(
                UpgradeMatrixCompiler.HashMask(originalUpgradeMask, profile.ToolId, 0u));
            views.UpgradeCompiledStats[slotIndex] = default;
            views.UpgradeToolProfiles[slotIndex] = profile;

            for (int i = 0; i < UpgradeMatrixConstants.ToolModuleSlotsPerEquipment; i++)
                views.UpgradeToolModuleRules[ruleBase + i] = i < safeSlotCount ? ToolUpgradeSystem.NormalizeRuleSlot(moduleRules[i], i) : default;

            return true;
        }

        private void ClearUpgradeMatrixStaging(ref EquipmentVaultViews views, int slotIndex)
        {
            if ((uint)slotIndex >= (uint)MaxTrackedTools)
                return;

            if (views.UpgradeMasks.IsCreated && (uint)slotIndex < (uint)views.UpgradeMasks.Length)
                views.UpgradeMasks[slotIndex] = default;
            if (views.UpgradeBaseStats.IsCreated && (uint)slotIndex < (uint)views.UpgradeBaseStats.Length)
                views.UpgradeBaseStats[slotIndex] = default;
            if (views.UpgradeCompiledStats.IsCreated && (uint)slotIndex < (uint)views.UpgradeCompiledStats.Length)
                views.UpgradeCompiledStats[slotIndex] = default;
            if (views.UpgradeToolProfiles.IsCreated && (uint)slotIndex < (uint)views.UpgradeToolProfiles.Length)
                views.UpgradeToolProfiles[slotIndex] = default;

            int ruleBase = slotIndex * UpgradeMatrixConstants.ToolModuleSlotsPerEquipment;
            if (!views.UpgradeToolModuleRules.IsCreated ||
                ruleBase < 0 ||
                ruleBase + UpgradeMatrixConstants.ToolModuleSlotsPerEquipment > views.UpgradeToolModuleRules.Length)
                return;

            for (int i = 0; i < UpgradeMatrixConstants.ToolModuleSlotsPerEquipment; i++)
                views.UpgradeToolModuleRules[ruleBase + i] = default;
        }

        private void ReadModuleRuleMirror(int slotIndex, int moduleSlotCount, ToolUpgradeModuleRuleDTO[] destination)
        {
            int baseIndex = slotIndex * ToolUpgradeSystem.MaxModuleSlots;
            for (int i = 0; i < ToolUpgradeSystem.MaxModuleSlots; i++)
                destination[i] = i < moduleSlotCount ? _moduleRuleSlots[baseIndex + i] : default;
        }

        private void ClearModuleRuleMirror(int slotIndex)
        {
            int baseIndex = slotIndex * ToolUpgradeSystem.MaxModuleSlots;
            for (int i = 0; i < ToolUpgradeSystem.MaxModuleSlots; i++)
                _moduleRuleSlots[baseIndex + i] = default;
        }

        private static int GetConfiguredSlotCount(PlayerTool owner)
        {
            if (owner == null || owner.RuntimeMetadata == null)
                return 0;

            return Mathf.Clamp(owner.RuntimeMetadata.maxUpgradeSlots, 0, ToolUpgradeSystem.MaxModuleSlots);
        }

        private static float ReadBatteryAbsolute(ref EquipmentVaultViews views, int slotIndex)
        {
            if (!views.ToolStates.IsCreated || (uint)slotIndex >= (uint)views.ToolStates.Length)
                return 0f;

            return math.max(0f, views.ToolStates[slotIndex].CurrentBattery);
        }

        private void SetBatteryAbsolute(ref EquipmentVaultViews views, int slotIndex, float absoluteBattery)
        {
            if (!views.ToolStates.IsCreated || (uint)slotIndex >= (uint)views.ToolStates.Length)
                return;

            ToolState state = views.ToolStates[slotIndex];
            state.CurrentBattery = math.max(0f, absoluteBattery);
            ToolRuntimeStats stats = views.ToolStats[slotIndex];
            state.StatusMask = ResolveStatusMask(new ResolveStatusMaskContext
            {
                CurrentStatus = state.StatusMask,
                State = state,
                Stats = stats,
                DepthMeters = ResolveDepthMeters(),
                Active = (state.StatusMask & ToolRuntimeStatusMasks.Active) != 0u,
                GridPowered = false
            });
            views.ToolStates[slotIndex] = state;
            WriteSlotMirrors(ref views, slotIndex, in state);
        }

        private bool RebuildCompiledState(
            int slotIndex,
            PlayerTool owner,
            ToolUpgradeModuleRuleDTO[] moduleRules,
            int moduleSlotCount)
        {
            if (owner == null || moduleRules == null)
                return false;
            if (!TryAcquireEquipmentViewsWriteLock(out EquipmentVaultViews views))
                return false;

            try
            {
                ToolRuntimeProfile profile = owner.BuildModularRuntimeProfile();
                int slotCount = math.clamp(
                    Mathf.Min(moduleSlotCount, Mathf.Min(GetConfiguredSlotCount(owner), (int)profile.ModuleSlotCount)),
                    0,
                    ToolUpgradeSystem.MaxModuleSlots);

                ToolState state = views.ToolStates[slotIndex];
                ToolRuntimeStats previousStats = views.ToolStats[slotIndex];
                float previousCapacity = math.max(0.1f, math.isfinite(previousStats.BatteryCapacity) ? previousStats.BatteryCapacity : 0.1f);
                float currentBattery = math.max(0f, math.isfinite(state.CurrentBattery) ? state.CurrentBattery : 0f);
                state.CurrentBattery = math.saturate(currentBattery / previousCapacity);

                ulong upgradeMask64;
                ToolRuntimeStats compiledStats = ToolUpgradeSystem.CompileRuntimeStatsFromRules64(profile, moduleRules, slotCount, out upgradeMask64);
                state.CurrentBattery *= math.max(0.1f, compiledStats.BatteryCapacity);
                state.UpgradeBitmask = (uint)(upgradeMask64 & 0xFFFFFFFFUL);
                state.UpgradeBitmask64 = upgradeMask64;
                state.ToolTypeId = ResolveToolTypeId(profile.ToolId);
                state.ModuleSlotCount = (byte)math.clamp(slotCount, 0, ToolUpgradeSystem.MaxModuleSlots);
                state.StatusMask = ResolveStatusMask(new ResolveStatusMaskContext
                {
                    CurrentStatus = state.StatusMask,
                    State = state,
                    Stats = compiledStats,
                    DepthMeters = ResolveDepthMeters(),
                    Active = (state.StatusMask & ToolRuntimeStatusMasks.Active) != 0u,
                    GridPowered = false
                });
                if (!TryWriteUpgradeMatrixStaging(ref views, slotIndex, in profile, moduleRules, slotCount, upgradeMask64))
                    return false;

                views.ToolStats[slotIndex] = compiledStats;
                views.ToolStates[slotIndex] = state;
                WriteActiveEquipmentWearRate(
                    ref views,
                    slotIndex,
                    owner,
                    in compiledStats,
                    owner.HasRuntimeActiveIntent || (_externalActiveToolMask & (1u << slotIndex)) != 0u);
                SetBatteryAbsolute(ref views, slotIndex, state.CurrentBattery);
                WriteActiveEquipmentSlot(ref views, slotIndex, in state, in compiledStats);
                WriteSlotMirrors(ref views, slotIndex, in state);
                WriteModuleRuleMirror(slotIndex, moduleRules, slotCount);
                return true;
            }
            finally
            {
                ReleaseEquipmentWriteLocks(ref views);
            }
        }

        private void MarkSlotActive(int slotIndex, bool active)
        {
            if ((uint)slotIndex >= MaxTrackedTools)
                return;

            uint slotBit = 1u << slotIndex;
            if (active)
                _externalActiveToolMask |= slotBit;
            else
                _externalActiveToolMask &= ~slotBit;
        }

        private void WriteActiveEquipmentSlot(ref EquipmentVaultViews views, int slotIndex, in ToolState state, in ToolRuntimeStats stats)
        {
            if (!views.ActiveEquipmentStates.IsCreated || (uint)slotIndex >= (uint)views.ActiveEquipmentStates.Length)
                return;

            PlayerTool owner = _toolOwners[slotIndex];
            uint existingFlags = views.ActiveEquipmentStates[slotIndex].StateFlags;
            uint stateFlags = BuildActiveEquipmentFlags(state.StatusMask, existingFlags);
            float capacity = math.max(0.1f, stats.BatteryCapacity);

            views.ActiveEquipmentStates[slotIndex] = new ActiveEquipmentDTO
            {
                ToolHashID = owner != null ? owner.RuntimeToolId : 0u,
                CurrentBattery = math.max(0f, state.CurrentBattery),
                ThermalLoad = math.max(0f, state.InternalHeat),
                StateFlags = stateFlags,
                PowerDrawRate = math.max(0f, stats.BatteryDrainPerSecond) * capacity,
                HeatGenerationRate = math.max(0f, stats.HeatGenerationRate * stats.PowerScalar),
                _pad0 = 0,
                _pad1 = 0
            };
        }

        private void WriteActiveEquipmentWearRate(ref EquipmentVaultViews views, int slotIndex, PlayerTool owner, in ToolRuntimeStats stats, bool requestedActive)
        {
            if (!views.ActiveEquipmentWearDrainRates.IsCreated || (uint)slotIndex >= (uint)views.ActiveEquipmentWearDrainRates.Length)
                return;

            float wearRate = 0f;
            if (requestedActive && owner != null)
            {
                wearRate = owner.ResolveActiveDurabilityDrainRateNormalized();
                float multiplier = math.max(0f, math.isfinite(stats.DurabilityDrainMultiplier) ? stats.DurabilityDrainMultiplier : 0f);
                IToolDurabilityService durability = _toolDurabilityService;
                if (owner.TryGetDurabilityMirror(out _, out uint itemHashId, out _) && durability != null)
                    multiplier *= durability.ResolveCentralizedEquipmentWearMultiplier(itemHashId);
                wearRate *= multiplier;
            }

            views.ActiveEquipmentWearDrainRates[slotIndex] = math.isfinite(wearRate) ? math.max(0f, wearRate) : 0f;
        }

        private void RegisterDurabilityMirror(PlayerTool owner)
        {
            if (owner == null || !owner.TryGetDurabilityMirror(out string toolId, out uint itemHashId, out float maxDurability))
                return;

            IToolDurabilityService durability = _toolDurabilityService;
            if (durability == null)
                return;

            _toolDurabilityService = durability;
            durability.RegisterCentralizedEquipmentMirror(toolId, itemHashId, maxDurability);
        }

        private void RegisterDurabilityMirrorsCold()
        {
            if (_toolDurabilityService == null)
                return;

            for (int i = 0; i < MaxTrackedTools; i++)
            {
                PlayerTool owner = _toolOwners[i];
                if (!_slotUsed[i] || owner == null)
                    continue;

                RegisterDurabilityMirror(owner);
            }
        }

        private void SyncDurabilityMirror(int slotIndex, in ToolState state)
        {
            if ((uint)slotIndex >= MaxTrackedTools)
                return;

            PlayerTool owner = _toolOwners[slotIndex];
            if (owner == null || !owner.TryGetDurabilityMirror(out string toolId, out uint itemHashId, out float maxDurability))
                return;

            IToolDurabilityService durability = _toolDurabilityService;
            if (durability == null)
                return;

            durability.SetDurabilityNormalizedFromEquipment(toolId, itemHashId, math.saturate(state.Durability), maxDurability);
        }

        private static uint BuildActiveEquipmentFlags(uint runtimeStatusMask, uint existingFlags)
        {
            uint flags = existingFlags & (ActiveEquipmentStateFlags.InWater | ActiveEquipmentStateFlags.GridPowered);
            if ((runtimeStatusMask & ToolRuntimeStatusMasks.Active) != 0u)
                flags |= ActiveEquipmentStateFlags.Active;
            if ((runtimeStatusMask & ToolRuntimeStatusMasks.Overheated) != 0u)
                flags |= ActiveEquipmentStateFlags.Overheated;
            if ((runtimeStatusMask & ToolRuntimeStatusMasks.LowPower) != 0u)
                flags |= ActiveEquipmentStateFlags.Depleted;
            if ((runtimeStatusMask & ToolRuntimeStatusMasks.Broken) != 0u)
                flags |= ActiveEquipmentStateFlags.Broken;
            return flags;
        }

        private void ClearActiveEquipmentSlot(ref EquipmentVaultViews views, int slotIndex)
        {
            if ((uint)slotIndex >= MaxTrackedTools)
                return;

            uint slotBit = 1u << slotIndex;
            _externalActiveToolMask &= ~slotBit;
            _lastTelemetryActiveMask &= ~slotBit;

            if (views.ActiveEquipmentStates.IsCreated && slotIndex < views.ActiveEquipmentStates.Length)
                views.ActiveEquipmentStates[slotIndex] = default;
            if (views.PublishedActiveEquipmentStates.IsCreated && slotIndex < views.PublishedActiveEquipmentStates.Length)
                views.PublishedActiveEquipmentStates[slotIndex] = default;
            if (views.ActiveEquipmentAupSamples.IsCreated && slotIndex < views.ActiveEquipmentAupSamples.Length)
                views.ActiveEquipmentAupSamples[slotIndex] = default;
            if (views.ActiveEquipmentGridLoadRequests.IsCreated && slotIndex < views.ActiveEquipmentGridLoadRequests.Length)
                views.ActiveEquipmentGridLoadRequests[slotIndex] = default;
            if (views.ActiveEquipmentWearDrainRates.IsCreated && slotIndex < views.ActiveEquipmentWearDrainRates.Length)
                views.ActiveEquipmentWearDrainRates[slotIndex] = 0f;
        }

        private void RefreshThermalGridReadback(out NativeArray<float>.ReadOnly thermalGridReadback)
        {
            thermalGridReadback = default;
            _thermalGridWidth = 0;
            _thermalGridHeight = 0;
            _thermalGridDepth = 0;
            _thermalGridVersion = 0;
            _thermalGridCellCount = 0;
            _thermalGridCellSizeMeters = EquipmentDefaultCellSizeMeters;
            _thermalGridRootAup = default;

            IThermodynamicsService thermodynamics = _thermodynamicsService;
            if (thermodynamics == null ||
                !thermodynamics.TryGetThermalGridReadback(
                    out NativeArray<float>.ReadOnly grid,
                    out int width,
                    out int height,
                    out int depth,
                    out Vector3 originWS,
                    out float cellSizeMeters,
                    out int version) ||
                !grid.IsCreated ||
                width <= 0 ||
                height <= 0 ||
                depth <= 0)
            {
                return;
            }

            thermalGridReadback = grid;
            _thermalGridWidth = width;
            _thermalGridHeight = height;
            _thermalGridDepth = depth;
            _thermalGridVersion = version;
            _thermalGridCellCount = grid.Length;
            _thermalGridCellSizeMeters = math.isfinite(cellSizeMeters) && cellSizeMeters > 0f
                ? cellSizeMeters
                : EquipmentDefaultCellSizeMeters;
            _thermalGridRootAup = TryResolveRuntimeAup(originWS, out double3 thermalGridRootAup)
                ? thermalGridRootAup
                : default;
        }

        private void RefreshActiveEquipmentInputs(ref EquipmentVaultViews views)
        {
            if (!views.ActiveEquipmentStates.IsCreated || !views.ActiveEquipmentAupSamples.IsCreated || !views.ActiveEquipmentWearDrainRates.IsCreated)
                return;

            _lastTelemetryActiveMask = 0u;
            bool gridAvailable = ResolveGridPowerAvailable();
            bool hasPlayerAup = TryResolvePlayerEquipmentAup(out double3 playerAup);
            bool playerInWater = ResolveToolInWater();
            float depthMeters = ResolveDepthMeters();

            for (int i = 0; i < MaxTrackedTools; i++)
            {
                if (!_slotUsed[i] || _toolOwners[i] == null)
                {
                    ClearActiveEquipmentSlot(ref views, i);
                    continue;
                }

                PlayerTool owner = _toolOwners[i];
                ToolState state = views.ToolStates[i];
                ToolRuntimeStats stats = views.ToolStats[i];
                if (TryResolveHardwareSpec(ref views, owner.RuntimeToolId, owner.RuntimeToolSpecHashId, out EquipmentHardwareSpecDTO hardwareSpec))
                {
                    stats = ApplyHardwareSpec(stats, in hardwareSpec);
                    views.ToolStats[i] = stats;
                }

                uint slotBit = 1u << i;
                bool requestedActive = owner.HasRuntimeActiveIntent || (_externalActiveToolMask & slotBit) != 0u;
                WriteActiveEquipmentWearRate(ref views, i, owner, in stats, requestedActive);
                bool gridPowered = requestedActive &&
                    gridAvailable &&
                    (state.UpgradeBitmask64 & (ulong)ToolUpgradeBits.WirelessCharging) != 0UL;
                state.Durability = !requestedActive && owner != null
                    ? math.saturate(owner.DurabilityNormalized)
                    : math.saturate(math.isfinite(state.Durability) ? state.Durability : 0f);
                state.StatusMask = ResolveStatusMask(new ResolveStatusMaskContext
                {
                    CurrentStatus = state.StatusMask,
                    State = state,
                    Stats = stats,
                    DepthMeters = depthMeters,
                    Active = requestedActive,
                    GridPowered = gridPowered
                });
                bool active = requestedActive && (state.StatusMask & ToolRuntimeStatusMasks.Disabled) == 0u;
                if (active)
                    _lastTelemetryActiveMask |= slotBit;

                state.StatusMask = ResolveStatusMask(new ResolveStatusMaskContext
                {
                    CurrentStatus = state.StatusMask,
                    State = state,
                    Stats = stats,
                    DepthMeters = depthMeters,
                    Active = active,
                    GridPowered = gridPowered
                });
                views.ToolStates[i] = state;

                float capacity = math.max(0.1f, stats.BatteryCapacity);
                float heatRate = math.max(0f, stats.HeatGenerationRate * stats.PowerScalar);
                if (IsOverchargeRequested(i))
                {
                    float heatGrowth = EstimateOverchargeHeatGrowth(state.InternalHeat);
                    heatRate = math.max(0.05f, stats.HeatGenerationRate) * OverchargeHeatScale * heatGrowth;
                }

                uint flags = 0u;
                if (active)
                    flags |= ActiveEquipmentStateFlags.Active;
                if ((state.StatusMask & ToolRuntimeStatusMasks.Overheated) != 0u)
                    flags |= ActiveEquipmentStateFlags.Overheated;
                if ((state.StatusMask & ToolRuntimeStatusMasks.LowPower) != 0u)
                    flags |= ActiveEquipmentStateFlags.Depleted;
                if ((state.StatusMask & ToolRuntimeStatusMasks.Broken) != 0u)
                    flags |= ActiveEquipmentStateFlags.Broken;
                if (playerInWater)
                    flags |= ActiveEquipmentStateFlags.InWater;
                if (gridPowered)
                    flags |= ActiveEquipmentStateFlags.GridPowered;

                views.ActiveEquipmentStates[i] = new ActiveEquipmentDTO
                {
                    ToolHashID = owner.RuntimeToolId,
                    CurrentBattery = math.max(0f, state.CurrentBattery),
                    ThermalLoad = math.max(0f, state.InternalHeat),
                    StateFlags = flags,
                PowerDrawRate = math.max(0f, stats.BatteryDrainPerSecond) * capacity,
                HeatGenerationRate = heatRate,
                _pad0 = 0,
                _pad1 = 0
                };

                views.ActiveEquipmentAupSamples[i] = TryResolveToolAup(owner, hasPlayerAup, in playerAup, out double3 toolAup)
                    ? toolAup
                    : (hasPlayerAup ? playerAup : double3.zero);
            }
        }

#if UNITY_EDITOR
        public EquipmentCsvParseResult IngestToolHardwareSpecsCsv(ReadOnlySpan<byte> csv)
        {
            if (!TryAcquireEquipmentViewsWriteLock(out EquipmentVaultViews views))
            {
                return new EquipmentCsvParseResult
                {
                    ParsedRows = 0,
                    SkippedRows = 0,
                    LastToolHashID = 0u,
                    FaultFlags = EquipmentFaultCsvOverflow
                };
            }

            try
            {
                return IlluminationHardwareProfilesCsvParser.Parse(csv, views.EquipmentHardwareSpecs.AsNativeArray());
            }
            finally
            {
                ReleaseEquipmentWriteLocks(ref views);
            }
        }
#endif

        private static bool TryResolveHardwareSpec(ref EquipmentVaultViews views, uint runtimeToolId, uint specToolId, out EquipmentHardwareSpecDTO spec)
        {
            spec = default;
            if (!views.EquipmentHardwareSpecs.IsCreated || (runtimeToolId == 0u && specToolId == 0u))
                return false;

            int count = math.min(views.EquipmentHardwareSpecs.Length, EquipmentHardwareSpecCapacity);
            for (int i = 0; i < count; i++)
            {
                EquipmentHardwareSpecDTO candidate = views.EquipmentHardwareSpecs[i];
                if (candidate.ToolHashID == runtimeToolId ||
                    (specToolId != 0u && candidate.ToolHashID == specToolId))
                {
                    spec = candidate;
                    return true;
                }
            }

            return false;
        }

        private static ToolRuntimeStats ApplyHardwareSpec(ToolRuntimeStats stats, in EquipmentHardwareSpecDTO spec)
        {
            float capacity = spec.BatteryCapacity > 0f && math.isfinite(spec.BatteryCapacity)
                ? spec.BatteryCapacity
                : stats.BatteryCapacity;
            stats.BatteryCapacity = math.max(0.1f, capacity);

            if (spec.PowerDrawRate > 0f && math.isfinite(spec.PowerDrawRate))
                stats.BatteryDrainPerSecond = spec.PowerDrawRate * math.rcp(stats.BatteryCapacity);

            if (spec.HeatGenerationRate > 0f && math.isfinite(spec.HeatGenerationRate))
            {
                float thermalLimit = spec.ThermalLimit > 0f && math.isfinite(spec.ThermalLimit) ? spec.ThermalLimit : 1f;
                stats.HeatGenerationRate = spec.HeatGenerationRate * math.rcp(math.max(0.0001f, thermalLimit));
            }

            if (spec.CooldownRate > 0f && math.isfinite(spec.CooldownRate))
                stats.CooldownRate = spec.CooldownRate;

            return stats;
        }

        private bool ResolveGridPowerAvailable()
        {
            ISubmarineAtmosphereRoomReadModel atmosphere = _submarineRuntimeContext != null
                ? _submarineRuntimeContext.AtmosphereSystem
                : null;
            return atmosphere != null &&
                   atmosphere.IsAtmosphereRuntimeActive &&
                   _powerGridService != null;
        }

        private bool ResolveToolInWater()
        {
            IPlayerRuntimeContext playerContext = _playerRuntimeContext;
            if (playerContext == null ||
                !playerContext.TryGetMovementRuntimeState(out PlayerMovementRuntimeState movementState))
            {
                return false;
            }

            if ((movementState.Flags & (uint)PlayerRuntimeSnapshotFlags.Underwater) != 0u)
                return true;

            return math.isfinite(movementState.DepthMeters) && movementState.DepthMeters > 0f;
        }

        private bool TryResolvePlayerEquipmentAup(out double3 aup)
        {
            aup = default;
            IPlayerRuntimeContext playerContext = _playerRuntimeContext;
            if (playerContext == null)
                return false;

            if (playerContext.TryGetPlayerPoseSnapshot(out PlayerRuntimePoseSnapshot snapshot))
            {
                aup = snapshot.Aup.ToAbsoluteDouble3();
                if (math.all(math.isfinite(aup)))
                    return true;
            }

            return false;
        }

        private bool TryResolveRuntimeAup(Vector3 runtimePosition, out double3 absoluteAup)
        {
            absoluteAup = default;
            float3 localRuntime = new float3(runtimePosition.x, runtimePosition.y, runtimePosition.z);
            if (!math.all(math.isfinite(localRuntime)))
                return false;

            IPlayerRuntimeContext playerContext = _playerRuntimeContext;
            if (playerContext == null ||
                !playerContext.TryGetPlayerPoseSnapshot(out PlayerRuntimePoseSnapshot snapshot) ||
                !snapshot.Aup.IsFinite() ||
                !math.all(math.isfinite(snapshot.RuntimePosition)))
                return false;

            double3 deltaMeters = new double3(
                (double)runtimePosition.x - snapshot.RuntimePosition.x,
                (double)runtimePosition.y - snapshot.RuntimePosition.y,
                (double)runtimePosition.z - snapshot.RuntimePosition.z);
            var positionAup = snapshot.Aup.OffsetMeters(deltaMeters);
            if (!positionAup.IsFinite())
                return false;

            absoluteAup = positionAup.ToAbsoluteDouble3();
            return math.all(math.isfinite(absoluteAup));
        }

        private static bool TryResolveToolAup(
            PlayerTool owner,
            bool hasPlayerAup,
            in double3 playerAup,
            out double3 toolAup)
        {
            toolAup = default;
            if (owner == null)
                return false;

            if (owner.IsEquipped && hasPlayerAup)
            {
                toolAup = playerAup;
                return math.all(math.isfinite(toolAup));
            }

            return false;
        }

        private static float ResolveGlobalQualityWeight()
        {
            float quality = HomeostasisBrain.GlobalQualityWeight;
            return math.saturate(math.isfinite(quality) ? quality : 1f);
        }

        private float ResolveEquipmentTickInterval(float globalQualityWeight)
        {
            float q = math.saturate(math.isfinite(globalQualityWeight) ? globalQualityWeight : 1f);
            EquipmentTuningDTO tuning = TryResolveEquipmentTuningNoAcquire(out EquipmentTuningDTO resolvedTuning)
                ? SanitizeEquipmentTuning(resolvedTuning)
                : EquipmentTuningDTO.CreateDefault(q);
            return math.lerp(tuning.MinimumTickInterval, tuning.MaximumTickInterval, 1f - q);
        }

        private bool TryResolveEquipmentTuningNoAcquire(out EquipmentTuningDTO tuning)
        {
            tuning = default;
            IDataVault vault = _dataVault;
            if (!TryReadEquipmentBuffer(vault, in _equipmentTuningHandle, 1, out NativeArray<EquipmentTuningDTO>.ReadOnly tuningBuffer))
                return false;

            tuning = tuningBuffer[0];
            return true;
        }

        private void ScheduleToolUpgradeMatrixPostIntegration(ref EquipmentVaultViews views)
        {
            _upgradeTelemetryScheduled = false;
            if (!_equipmentIntegrationScheduled)
                return;

            int matrixCount = ResolveUpgradeMatrixScheduleCount();
            if (matrixCount <= 0)
                return;

            UpgradeMatrixJobChain chain = UpgradeMatrixScheduler.ScheduleToolModuleMatrix(
                views.UpgradeToolModuleRules.AsNativeArray(),
                views.UpgradeMasks.AsNativeArray(),
                views.UpgradeBaseStats.AsNativeArray(),
                views.UpgradeToolLut.AsNativeArray(),
                views.UpgradeCompiledStats.AsNativeArray(),
                views.UpgradeToolProfiles.AsNativeArray(),
                views.ToolStats.AsNativeArray(),
                _lastGlobalQualityWeight,
                matrixCount,
                _equipmentIntegrationHandle);

            UpgradeMatrixJobChain visualChain = UpgradeMatrixScheduler.ScheduleVisualSync(
                views.UpgradeMasks.AsNativeArray(),
                views.UpgradeCompiledStats.AsNativeArray(),
                views.UpgradeVisualStates.AsNativeArray(),
                _lastGlobalQualityWeight,
                matrixCount,
                chain.Final);

            UpgradeMatrixJobChain telemetryChain = UpgradeMatrixScheduler.ScheduleTelemetry(
                views.UpgradeMasks.AsNativeArray(),
                views.UpgradeCompiledStats.AsNativeArray(),
                views.UpgradeTelemetryRing.AsNativeArray(),
                views.UpgradeTelemetryCursor.AsNativeArray(),
                _equipmentTickIndex,
                0f,
                matrixCount,
                visualChain.Final);

            _equipmentIntegrationHandle = telemetryChain.Final;
            _upgradeTelemetryScheduled = true;
            H8Memory.RegisterActiveJob(SystemID.GameplayTools, _equipmentIntegrationHandle);
        }

        private int ResolveUpgradeMatrixScheduleCount()
        {
            for (int i = MaxTrackedTools - 1; i >= 0; i--)
            {
                if (_slotUsed[i])
                    return i + 1;
            }

            return 0;
        }

        private unsafe void ScheduleActiveEquipmentIntegration(
            float deltaSeconds,
            ref EquipmentVaultViews views,
            NativeArray<float>.ReadOnly thermalGridReadback,
            JobHandle inputDeps)
        {
            if (_equipmentIntegrationScheduled ||
                !views.ActiveEquipmentStates.IsCreated ||
                !views.ActiveEquipmentAupSamples.IsCreated ||
                !views.ToolStats.IsCreated ||
                !views.ToolStates.IsCreated ||
                !views.ActiveEquipmentWearDrainRates.IsCreated ||
                !views.ActiveEquipmentGridLoadRequests.IsCreated ||
                !views.EquipmentIntegrationCounters.IsCreated ||
                !views.EquipmentTuning.IsCreated ||
                !_equipmentSignalLanesReady)
            {
                return;
            }

            float safeDelta = math.max(0f, deltaSeconds);
            if (safeDelta <= 0f)
                return;

            ActiveEquipmentDTO* equipment = (ActiveEquipmentDTO*)views.ActiveEquipmentStates.GetUnsafePtr();
            ToolState* toolStates = (ToolState*)views.ToolStates.GetUnsafePtr();
            ToolRuntimeStats* stats = (ToolRuntimeStats*)views.ToolStats.GetUnsafeReadOnlyPtr();
            double3* aupSamples = (double3*)views.ActiveEquipmentAupSamples.GetUnsafeReadOnlyPtr();
            float* wearDrainRates = (float*)views.ActiveEquipmentWearDrainRates.GetUnsafeReadOnlyPtr();
            EquipmentGridLoadRequest* gridRequests = (EquipmentGridLoadRequest*)views.ActiveEquipmentGridLoadRequests.GetUnsafePtr();
            EquipmentIntegrationCounters* counters = (EquipmentIntegrationCounters*)views.EquipmentIntegrationCounters.GetUnsafePtr();
            float* thermalGrid = thermalGridReadback.IsCreated && thermalGridReadback.Length > 0
                ? (float*)thermalGridReadback.GetUnsafeReadOnlyPtr()
                : null;
            EquipmentTuningDTO tuning = ResolveEquipmentTuningForJob(ref views, _lastGlobalQualityWeight);

            _equipmentTickIndex++;
            EquipmentStateIntegrationJob job = new EquipmentStateIntegrationJob
            {
                Equipment = equipment,
                ToolStates = toolStates,
                Stats = stats,
                ToolAups = aupSamples,
                WearDrainRates = wearDrainRates,
                ThermalGrid = thermalGrid,
                GridLoadRequests = gridRequests,
                Counters = counters,
                ToolCount = MaxTrackedTools,
                ThermalWidth = _thermalGridWidth,
                ThermalHeight = _thermalGridHeight,
                ThermalDepth = _thermalGridDepth,
                ThermalGridLength = thermalGridReadback.IsCreated ? thermalGridReadback.Length : 0,
                ThermalVersion = _thermalGridVersion,
                ThermalCellSizeMeters = _thermalGridCellSizeMeters,
                ThermalGridRootAup = _thermalGridRootAup,
                DeltaSeconds = safeDelta,
                Frame = _equipmentTickIndex,
                AmbientFallbackCelsius = EquipmentFallbackAmbientCelsius,
                DryHeatMultiplier = EquipmentDryHeatMultiplier,
                Tuning = tuning,
                FaultNonFiniteMask = EquipmentFaultNonFinite,
                FaultGridInvalidMask = EquipmentFaultThermalGridInvalid,
                OverheatWriter = SignalBus<EquipmentOverheatSignal>.ParallelWriter,
                OverheatWriterBudget = SignalBus<EquipmentOverheatSignal>.ParallelWriterBudget,
                DepletedWriter = SignalBus<ToolDepletedSignal>.ParallelWriter,
                DepletedWriterBudget = SignalBus<ToolDepletedSignal>.ParallelWriterBudget
            };

            _equipmentIntegrationHandle = job.Schedule(MaxTrackedTools, 4, inputDeps);
            H8Memory.RegisterActiveJob(SystemID.GameplayTools, _equipmentIntegrationHandle);
            _equipmentIntegrationScheduled = true;
        }

        private static EquipmentTuningDTO ResolveEquipmentTuningForJob(ref EquipmentVaultViews views, float globalQualityWeight)
        {
            EquipmentTuningDTO tuning = views.EquipmentTuning.IsCreated && views.EquipmentTuning.Length > 0
                ? views.EquipmentTuning[0]
                : EquipmentTuningDTO.CreateDefault(globalQualityWeight);
            tuning = SanitizeEquipmentTuning(tuning);
            tuning.GlobalQualityWeight = math.saturate(math.isfinite(globalQualityWeight) ? globalQualityWeight : tuning.GlobalQualityWeight);
            if (views.EquipmentTuning.IsCreated && views.EquipmentTuning.Length > 0)
                views.EquipmentTuning[0] = tuning;
            return tuning;
        }

        private static EquipmentTuningDTO SanitizeEquipmentTuning(in EquipmentTuningDTO source)
        {
            EquipmentTuningDTO tuning = source;
            tuning.GlobalQualityWeight = math.saturate(math.isfinite(tuning.GlobalQualityWeight) ? tuning.GlobalQualityWeight : 1f);
            tuning.MinimumTickInterval = math.max(0.001f, SanitizeTuningFloat(tuning.MinimumTickInterval, 0.016f));
            tuning.MaximumTickInterval = math.max(tuning.MinimumTickInterval, SanitizeTuningFloat(tuning.MaximumTickInterval, 0.2f));
            tuning.CoolingGain = math.max(0f, SanitizeTuningFloat(tuning.CoolingGain, 0.82f));
            tuning.WaterCoolingMultiplier = math.max(1f, SanitizeTuningFloat(tuning.WaterCoolingMultiplier, 2.75f));
            tuning.AmbientHeatFloorCelsius = SanitizeTuningFloat(tuning.AmbientHeatFloorCelsius, -2f);
            tuning.AmbientHeatCeilingCelsius = math.max(
                tuning.AmbientHeatFloorCelsius + 1f,
                SanitizeTuningFloat(tuning.AmbientHeatCeilingCelsius, 70f));
            tuning.ColdBatteryPenaltyMultiplier = math.max(0f, SanitizeTuningFloat(tuning.ColdBatteryPenaltyMultiplier, 1.85f));
            return tuning;
        }

        private static float SanitizeTuningFloat(float value, float fallback)
        {
            return math.isfinite(value) ? value : fallback;
        }

        private unsafe void CompleteActiveEquipmentJob(bool forceComplete = false)
        {
            if (!_equipmentIntegrationScheduled)
            {
                ReleaseEquipmentIntegrationWriteLocks();
                return;
            }

            if (!forceComplete && !_equipmentIntegrationHandle.IsCompleted)
                return;

            long startTicks = Stopwatch.GetTimestamp();
            bool releaseLocks = false;
            try
            {
                releaseLocks = true;
                bool completed = forceComplete
                    ? DispatcherJobFence.TryComplete(ref _equipmentIntegrationHandle, forceComplete: true)
                    : DispatcherJobFence.TryFinalizeCompleted(ref _equipmentIntegrationHandle);
                if (!completed)
                {
                    releaseLocks = false;
                    return;
                }

                _equipmentIntegrationScheduled = false;
                if (!TryResolveCapturedEquipmentIntegrationViews(out EquipmentVaultViews views))
                {
                    _upgradeTelemetryScheduled = false;
                    return;
                }

                PublishActiveEquipmentReadback(ref views);
                ProcessGridLoadRequests(ref views);

                long endTicks = Stopwatch.GetTimestamp();
                float microseconds = (float)((endTicks - startTicks) * 1000000.0 / Stopwatch.Frequency);
                RecordEquipmentTelemetry(ref views, microseconds);
                RecordFlashlightTelemetry(ref views, microseconds);
                if (_upgradeTelemetryScheduled)
                {
                    PatchUpgradeTelemetryMicroseconds(ref views, microseconds);
                    _upgradeTelemetryScheduled = false;
                }
            }
            finally
            {
                if (releaseLocks)
                {
                    _equipmentIntegrationScheduled = false;
                    _upgradeTelemetryScheduled = false;
                    ReleaseEquipmentIntegrationWriteLocks();
                }
            }
        }

        private unsafe void PublishActiveEquipmentReadback(ref EquipmentVaultViews views)
        {
            if (!views.ActiveEquipmentStates.IsCreated || !views.PublishedActiveEquipmentStates.IsCreated)
                return;

            int count = math.min(views.ActiveEquipmentStates.Length, views.PublishedActiveEquipmentStates.Length);
            UnsafeUtility.MemCpy(
                views.PublishedActiveEquipmentStates.GetUnsafePtr(),
                views.ActiveEquipmentStates.GetUnsafeReadOnlyPtr(),
                (long)count * UnsafeUtility.SizeOf<ActiveEquipmentDTO>());

            float depthMeters = ResolveDepthMeters();
            for (int i = 0; i < MaxTrackedTools; i++)
            {
                if (!_slotUsed[i] || _toolOwners[i] == null)
                    continue;

                ActiveEquipmentDTO dto = views.ActiveEquipmentStates[i];
                ToolState state = views.ToolStates[i];
                ToolRuntimeStats stats = views.ToolStats[i];
                bool active = (dto.StateFlags & ActiveEquipmentStateFlags.Active) != 0u;
                bool gridPowered = (dto.StateFlags & ActiveEquipmentStateFlags.GridPowered) != 0u;

                state.CurrentBattery = math.max(0f, dto.CurrentBattery);
                state.InternalHeat = math.max(0f, dto.ThermalLoad);
                state.Durability = math.saturate(state.Durability);
                state.StatusMask = ResolveStatusMask(new ResolveStatusMaskContext
                {
                    CurrentStatus = state.StatusMask,
                    State = state,
                    Stats = stats,
                    DepthMeters = depthMeters,
                    Active = active,
                    GridPowered = gridPowered
                });
                state.StatusMask = ApplyHeatWarningHapticGate(state.StatusMask, state.InternalHeat);
                views.ToolStates[i] = state;

                if (IsOverchargeRequested(i) && state.InternalHeat > OverchargeExplosionHeatThreshold)
                {
                    WriteSlotMirrors(ref views, i, in state);
                    TriggerOverchargeExplosion(ref views, i);
                    continue;
                }

                WriteSlotMirrors(ref views, i, in state);
                SyncDurabilityMirror(i, in state);
            }
        }

        private void ProcessGridLoadRequests(ref EquipmentVaultViews views)
        {
            if (!views.ActiveEquipmentGridLoadRequests.IsCreated)
                return;

            float requestedEnergy = 0f;
            for (int i = 0; i < math.min(MaxTrackedTools, views.ActiveEquipmentGridLoadRequests.Length); i++)
            {
                EquipmentGridLoadRequest request = views.ActiveEquipmentGridLoadRequests[i];
                requestedEnergy += math.max(0f, request.EnergyWattSeconds);
            }

            if (requestedEnergy <= 0.0001f)
                return;

            IPowerGridService powerGrid = _powerGridService;
            if (powerGrid == null)
            {
                _wirelessBrownoutActive = true;
                return;
            }

            bool queued = powerGrid.TryQueueWirelessToolDrain(requestedEnergy, out float grantedEnergy);
            if (!queued || !math.isfinite(grantedEnergy) || grantedEnergy + 0.0001f < requestedEnergy * 0.95f)
                _wirelessBrownoutActive = true;
        }

        private void RecordEquipmentTelemetry(ref EquipmentVaultViews views, float cpuMicroseconds)
        {
            if (!views.EquipmentTelemetryRing.IsCreated ||
                !views.EquipmentTelemetryCursor.IsCreated ||
                !views.EquipmentIntegrationCounters.IsCreated ||
                views.EquipmentTelemetryRing.Length == 0 ||
                views.EquipmentTelemetryCursor.Length == 0)
            {
                return;
            }

            EquipmentIntegrationCounters counters = AggregateIntegrationCounters(ref views);
            float safeCpuMicroseconds = math.max(0f, math.select(0f, cpuMicroseconds, math.isfinite(cpuMicroseconds)));
            uint faultFlags = counters.FaultFlags;
            if (safeCpuMicroseconds > EquipmentFaultCostThresholdMicroseconds)
                faultFlags |= EquipmentFaultOverBudget;
            int index = math.clamp(views.EquipmentTelemetryCursor[0], 0, views.EquipmentTelemetryRing.Length - 1);
            EquipmentTelemetryEntry entry = new EquipmentTelemetryEntry
            {
                Frame = _equipmentTickIndex,
                TickIndex = _equipmentTickIndex,
                BatteryDrainWattSeconds = counters.BatteryDrainWattSeconds,
                GridDrawWattSeconds = counters.GridDrawWattSeconds,
                PeakThermal01 = counters.PeakThermal01,
                ActiveToolMask = _lastTelemetryActiveMask,
                SignalCount = counters.SignalCount,
                FaultFlags = faultFlags,
                LastFaultToolHashID = counters.LastFaultToolHashID,
                CpuMicroseconds = safeCpuMicroseconds,
                GlobalQualityWeight = _lastGlobalQualityWeight,
                TickIntervalSeconds = _lastEquipmentTickInterval,
                ThermalGridVersion = _thermalGridVersion,
                ThermalGridCellCount = _thermalGridCellCount,
                SnapshotHash = ComputeActiveEquipmentSnapshotHash(ref views),
                WearDrainNormalized = counters.WearDrainNormalized
            };

            views.EquipmentTelemetryRing[index] = entry;
            views.EquipmentTelemetryCursor[0] = (index + 1) % views.EquipmentTelemetryRing.Length;
            if (entry.FaultFlags != 0u && !_equipmentFaultDumped && !_equipmentFaultDumpPending)
                _equipmentFaultDumpPending = true;
        }

        private void RecordFlashlightTelemetry(ref EquipmentVaultViews views, float cpuMicroseconds)
        {
            if (!views.FlashlightTelemetryRing.IsCreated ||
                !views.FlashlightTelemetryCursor.IsCreated ||
                !views.ActiveEquipmentStates.IsCreated ||
                !views.EquipmentIntegrationCounters.IsCreated ||
                views.FlashlightTelemetryRing.Length == 0 ||
                views.FlashlightTelemetryCursor.Length == 0)
            {
                return;
            }

            int slotIndex = -1;
            for (int i = 0; i < MaxTrackedTools; i++)
            {
                if (_slotUsed[i] && _toolOwners[i] is FlashlightTool)
                {
                    slotIndex = i;
                    break;
                }
            }

            if (slotIndex < 0 || slotIndex >= views.ActiveEquipmentStates.Length || slotIndex >= views.EquipmentIntegrationCounters.Length)
                return;

            ActiveEquipmentDTO state = views.ActiveEquipmentStates[slotIndex];
            EquipmentIntegrationCounters counters = views.EquipmentIntegrationCounters[slotIndex];
            float safeCpuMicroseconds = math.max(0f, math.select(0f, cpuMicroseconds, math.isfinite(cpuMicroseconds)));
            float depthMeters = ResolveDepthMeters();
            uint faultFlags = counters.FaultFlags;
            if (safeCpuMicroseconds > EquipmentFaultCostThresholdMicroseconds)
                faultFlags |= EquipmentFaultOverBudget;

            int index = math.clamp(views.FlashlightTelemetryCursor[0], 0, views.FlashlightTelemetryRing.Length - 1);
            FlashlightTelemetryEntry entry = new FlashlightTelemetryEntry
            {
                Frame = _equipmentTickIndex,
                ToolHashID = state.ToolHashID,
                Battery01 = counters.LastBattery01,
                Thermal01 = math.saturate(state.ThermalLoad),
                DepthMeters = depthMeters,
                AmbientCelsius = counters.LastAmbientCelsius,
                BatteryDrainWattSeconds = counters.BatteryDrainWattSeconds,
                PeakThermal01 = counters.PeakThermal01,
                CpuMicroseconds = safeCpuMicroseconds,
                GlobalQualityWeight = _lastGlobalQualityWeight,
                TickIntervalSeconds = _lastEquipmentTickInterval,
                StateFlags = state.StateFlags,
                FaultFlags = faultFlags,
                SnapshotHash = ComputeActiveEquipmentSnapshotHash(ref views),
                SignalCount = counters.SignalCount,
                WearDrainNormalized = counters.WearDrainNormalized
            };

            views.FlashlightTelemetryRing[index] = entry;
            views.FlashlightTelemetryCursor[0] = (index + 1) % views.FlashlightTelemetryRing.Length;
            if (entry.FaultFlags != 0u && !_equipmentFaultDumped && !_equipmentFaultDumpPending)
                _equipmentFaultDumpPending = true;
        }

        private void PublishFlashlightFailureShaderGlobals(
            NativeArray<ActiveEquipmentDTO>.ReadOnly activeStates,
            NativeArray<EquipmentIntegrationCounters>.ReadOnly counters)
        {
            int slotIndex = -1;
            for (int i = 0; i < MaxTrackedTools; i++)
            {
                if (_slotUsed[i] && _toolOwners[i] is FlashlightTool)
                {
                    slotIndex = i;
                    break;
                }
            }

            if (slotIndex < 0 || slotIndex >= activeStates.Length || slotIndex >= counters.Length)
            {
                Shader.SetGlobalVector(FlashlightFailureStateShaderId, Vector4.zero);
                return;
            }

            ActiveEquipmentDTO state = activeStates[slotIndex];
            EquipmentIntegrationCounters counter = counters[slotIndex];
            float battery01 = math.saturate(counter.LastBattery01);
            float thermal01 = math.saturate(state.ThermalLoad);
            uint flags = state.StateFlags;
            float depleted01 = (flags & ActiveEquipmentStateFlags.Depleted) != 0u ? 1f : math.saturate((0.18f - battery01) * 5.555556f);
            float overheated01 = (flags & ActiveEquipmentStateFlags.Overheated) != 0u ? 1f : thermal01 * thermal01;
            float broken01 = (flags & ActiveEquipmentStateFlags.Broken) != 0u ? 1f : 0f;
            float failure01 = math.saturate(math.max(depleted01, math.max(overheated01, broken01)));
            Shader.SetGlobalVector(
                FlashlightFailureStateShaderId,
                new Vector4(battery01, thermal01, failure01, (float)flags));
        }

        private void PublishFlashlightPresentationShaderGlobals()
        {
            IPlayerRuntimeContext playerRuntimeContext = _playerRuntimeContext;
            PlayerFlashlight flashlight = playerRuntimeContext != null ? playerRuntimeContext.Flashlight : null;
            if (flashlight == null || !flashlight.IsBeamPresentationActive || flashlight.PresentationAnchor == null)
            {
                PublishInactiveFlashlightPresentationShaderGlobals();
                return;
            }

            Transform anchor = flashlight.PresentationAnchor;
            float range = math.max(0.1f, flashlight.PresentationRange);
            float outerAngleRadians = math.max(1f, flashlight.PresentationSpotAngle * 0.5f) * 0.017453292519943295f;
            float innerAngleRadians = outerAngleRadians * 0.76f;
            float outerCos = ResolveFlashlightConeCos(outerAngleRadians);
            float innerCos = ResolveFlashlightConeCos(innerAngleRadians);
            Vector3 position = anchor.position;
            Vector3 direction = anchor.forward;
            Color color = flashlight.PresentationColor;
            float intensity = math.max(0f, flashlight.PresentationIntensity);

            Shader.SetGlobalFloat(FlashlightActiveShaderId, 1f);
            Shader.SetGlobalFloat(FlashlightVoxelActiveShaderId, 0f);
            Shader.SetGlobalVector(
                FlashlightPositionWsShaderId,
                new Vector4(position.x, position.y, position.z, range));
            Shader.SetGlobalVector(
                FlashlightDirectionWsShaderId,
                new Vector4(direction.x, direction.y, direction.z, innerCos));
            Shader.SetGlobalVector(
                FlashlightColorShaderId,
                new Vector4(color.r, color.g, color.b, intensity));
            Shader.SetGlobalVector(
                FlashlightConeDataShaderId,
                new Vector4(outerCos, 1f, math.rcp(math.max(range, 0.0001f)), 0.08f));
            Shader.SetGlobalVector(FlashlightVoxelHalfExtentsShaderId, Vector4.zero);
            Shader.SetGlobalMatrix(FlashlightVoxelWorldToLocalShaderId, Matrix4x4.identity);
        }

        private static void PublishInactiveFlashlightPresentationShaderGlobals()
        {
            Shader.SetGlobalFloat(FlashlightActiveShaderId, 0f);
            Shader.SetGlobalFloat(FlashlightVoxelActiveShaderId, 0f);
            Shader.SetGlobalVector(FlashlightPositionWsShaderId, Vector4.zero);
            Shader.SetGlobalVector(FlashlightDirectionWsShaderId, Vector4.zero);
            Shader.SetGlobalVector(FlashlightColorShaderId, Vector4.zero);
            Shader.SetGlobalVector(FlashlightConeDataShaderId, Vector4.zero);
            Shader.SetGlobalVector(FlashlightVoxelHalfExtentsShaderId, Vector4.zero);
            Shader.SetGlobalMatrix(FlashlightVoxelWorldToLocalShaderId, Matrix4x4.identity);
        }

        private static float ResolveFlashlightConeCos(float angleRadians)
        {
            float x = math.clamp(angleRadians, 0f, 1.5707964f);
            float x2 = x * x;
            float x4 = x2 * x2;
            return math.saturate(1f - 0.4967f * x2 + 0.03705f * x4);
        }

        private void PatchUpgradeTelemetryMicroseconds(ref EquipmentVaultViews views, float cpuMicroseconds)
        {
            if (!views.UpgradeTelemetryRing.IsCreated ||
                !views.UpgradeTelemetryCursor.IsCreated ||
                views.UpgradeTelemetryRing.Length <= 0 ||
                views.UpgradeTelemetryCursor.Length <= 0)
            {
                return;
            }

            int ringLength = math.min(views.UpgradeTelemetryRing.Length, UpgradeMatrixConstants.TelemetryFrameCount);
            int cursor = views.UpgradeTelemetryCursor[0];
            int index = cursor - 1;
            if (index < 0)
                index = ringLength - 1;

            UpgradeTelemetryEntry entry = views.UpgradeTelemetryRing[index];
            float safeMicroseconds = math.max(0f, math.select(0f, cpuMicroseconds, math.isfinite(cpuMicroseconds)));
            uint fault = (uint)math.select(0, 1, safeMicroseconds > UpgradeMatrixConstants.FaultCostThresholdMicroseconds);
            entry.BurstMicroseconds = safeMicroseconds;
            entry.FaultFlags |= fault;
            views.UpgradeTelemetryRing[index] = entry;

            if (fault != 0u && !_upgradeTelemetryFaultDumped && !_upgradeTelemetryFaultDumpPending)
                _upgradeTelemetryFaultDumpPending = true;
        }

        private void FlushPendingFaultDumps()
        {
            if (!_equipmentFaultDumpPending && !_upgradeTelemetryFaultDumpPending)
                return;

            if (!TryAcquireEquipmentViewsWriteLock(out EquipmentVaultViews views))
                return;

            try
            {
                if (_equipmentFaultDumpPending)
                {
                    if (DumpEquipmentTelemetry(ref views))
                        _equipmentFaultDumpPending = false;
                }

                if (_upgradeTelemetryFaultDumpPending)
                {
                    if (DumpUpgradeTelemetry(ref views))
                        _upgradeTelemetryFaultDumpPending = false;
                }
            }
            finally
            {
                ReleaseEquipmentWriteLocks(ref views);
            }
        }

        private static EquipmentIntegrationCounters AggregateIntegrationCounters(ref EquipmentVaultViews views)
        {
            EquipmentIntegrationCounters aggregate = default;
            if (!views.EquipmentIntegrationCounters.IsCreated)
                return aggregate;

            int count = math.min(MaxTrackedTools, views.EquipmentIntegrationCounters.Length);
            for (int i = 0; i < count; i++)
            {
                EquipmentIntegrationCounters item = views.EquipmentIntegrationCounters[i];
                aggregate.BatteryDrainWattSeconds += item.BatteryDrainWattSeconds;
                aggregate.GridDrawWattSeconds += item.GridDrawWattSeconds;
                aggregate.WearDrainNormalized += item.WearDrainNormalized;
                aggregate.PeakThermal01 = math.max(aggregate.PeakThermal01, item.PeakThermal01);
                aggregate.ActiveCount += item.ActiveCount;
                aggregate.SignalCount += item.SignalCount;
                aggregate.FaultFlags |= item.FaultFlags;
                if (item.LastFaultToolHashID != 0u)
                    aggregate.LastFaultToolHashID = item.LastFaultToolHashID;
            }

            return aggregate;
        }

        private static uint ComputeActiveEquipmentSnapshotHash(ref EquipmentVaultViews views)
        {
            if (!views.PublishedActiveEquipmentStates.IsCreated)
                return 0u;

            uint hash = 2166136261u;
            int count = math.min(MaxTrackedTools, views.PublishedActiveEquipmentStates.Length);
            for (int i = 0; i < count; i++)
            {
                ActiveEquipmentDTO dto = views.PublishedActiveEquipmentStates[i];
                hash = MixFnv(hash, dto.ToolHashID);
                hash = MixFnv(hash, math.asuint(dto.CurrentBattery));
                hash = MixFnv(hash, math.asuint(dto.ThermalLoad));
                hash = MixFnv(hash, dto.StateFlags);
            }

            return hash;
        }

        private static uint MixFnv(uint hash, uint value)
        {
            hash ^= value;
            return hash * 16777619u;
        }

        private unsafe bool DumpEquipmentTelemetry(ref EquipmentVaultViews views)
        {
            if (!views.FlashlightTelemetryRing.IsCreated && !views.EquipmentTelemetryRing.IsCreated)
                return false;

            try
            {
                bool useFlashlightRing = views.FlashlightTelemetryRing.IsCreated && views.FlashlightTelemetryRing.Length > 0;
                string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
                string dumpPath = Path.Combine(projectRoot, EquipmentFaultDumpPath);
                uint header = useFlashlightRing ? 0x46545848u : 0x45515448u; // H8TF / H8TE
                uint rowCount = useFlashlightRing
                    ? unchecked((uint)views.FlashlightTelemetryRing.Length)
                    : unchecked((uint)views.EquipmentTelemetryRing.Length);
                uint rowSize = useFlashlightRing
                    ? unchecked((uint)UnsafeUtility.SizeOf<FlashlightTelemetryEntry>())
                    : unchecked((uint)UnsafeUtility.SizeOf<EquipmentTelemetryEntry>());
                int byteLength = checked((int)(rowCount * rowSize));
                const int HeaderBytes = 16;
                NativeArray<byte> payload = NativeFaultDumpWriter.CreateTransientPayload(
                    HeaderBytes + byteLength,
                    nameof(ModularEquipmentEngine),
                    EquipmentFaultDumpPayloadLabel,
                    NativeArrayOptions.UninitializedMemory);
                try
                {
                    byte* target = (byte*)NativeArrayUnsafeUtility.GetUnsafePtr(payload);
                    Span<byte> headerBytes = new Span<byte>(target, HeaderBytes);
                    WriteUInt32LE(headerBytes, 0, header);
                    WriteUInt32LE(headerBytes, 4, rowCount);
                    WriteUInt32LE(headerBytes, 8, rowSize);
                    WriteUInt32LE(headerBytes, 12, _equipmentTickIndex);

                    void* source = useFlashlightRing
                        ? views.FlashlightTelemetryRing.GetUnsafeReadOnlyPtr()
                        : views.EquipmentTelemetryRing.GetUnsafeReadOnlyPtr();
                    UnsafeUtility.MemCpy(target + HeaderBytes, source, byteLength);
                    if (!NativeFaultDumpWriter.TryWriteAll(dumpPath, payload, HeaderBytes + byteLength))
                        return false;

                    _equipmentFaultDumped = true;
                    return true;
                }
                finally
                {
                    NativeFaultDumpWriter.DisposeTransientPayload(
                        ref payload,
                        nameof(ModularEquipmentEngine),
                        EquipmentFaultDumpPayloadLabel);
                }
            }
            catch (Exception)
            {
                GlobalTelemetryBus.PublishUnityLogFault(EquipmentTelemetryDumpFaultHash, 0u, 1u);
                return false;
            }
        }

        private unsafe bool DumpUpgradeTelemetry(ref EquipmentVaultViews views)
        {
            if (!views.UpgradeTelemetryRing.IsCreated || views.UpgradeTelemetryRing.Length <= 0)
                return false;

            try
            {
                string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
                UpgradeTelemetryEntry* source = (UpgradeTelemetryEntry*)views.UpgradeTelemetryRing.GetUnsafeReadOnlyPtr();
                ReadOnlySpan<UpgradeTelemetryEntry> telemetry = new ReadOnlySpan<UpgradeTelemetryEntry>(source, views.UpgradeTelemetryRing.Length);
                UpgradeMatrixCompiler.DumpTelemetry(telemetry, projectRoot);
                _upgradeTelemetryFaultDumped = true;
                return true;
            }
            catch (Exception)
            {
                GlobalTelemetryBus.PublishUnityLogFault(UpgradeTelemetryDumpFaultHash, 0u, 1u);
                return false;
            }
        }

        private static void WriteUInt32LE(Span<byte> buffer, int offset, uint value)
        {
            buffer[offset] = (byte)value;
            buffer[offset + 1] = (byte)(value >> 8);
            buffer[offset + 2] = (byte)(value >> 16);
            buffer[offset + 3] = (byte)(value >> 24);
        }

        private struct ResolveStatusMaskContext
        {
            public uint CurrentStatus;
            public ToolState State;
            public ToolRuntimeStats Stats;
            public float DepthMeters;
            public bool Active;
            public bool GridPowered;
        }

        private static uint ResolveStatusMask(in ResolveStatusMaskContext context)
        {
            uint status = context.CurrentStatus & ToolRuntimeStatusMasks.HeatWarningHapticQueued;
            if (context.Active)
                status |= ToolRuntimeStatusMasks.Active;

            if (!context.GridPowered && (context.State.CurrentBattery <= 0.0001f || context.Stats.BatteryCapacity <= 0.0001f))
                status |= ToolRuntimeStatusMasks.LowPower;

            if (context.State.InternalHeat >= 1f ||
                ((context.CurrentStatus & ToolRuntimeStatusMasks.Overheated) != 0u && context.State.InternalHeat > OverheatRecoveryThreshold))
            {
                status |= ToolRuntimeStatusMasks.Overheated;
            }

            if (context.State.Durability <= 0.0001f)
                status |= ToolRuntimeStatusMasks.Broken;

            bool standardToolBelowLimit = context.DepthMeters > StandardDepthFailureMeters &&
                (context.State.UpgradeBitmask64 & ((ulong)ToolUpgradeBits.DepthHardened | (ulong)ToolUpgradeBits.ThermalShield)) == 0UL;
            if (standardToolBelowLimit)
                status |= ToolRuntimeStatusMasks.DepthFailed;

            uint disablingBits = ToolRuntimeStatusMasks.LowPower |
                                  ToolRuntimeStatusMasks.Overheated |
                                  ToolRuntimeStatusMasks.Broken |
                                  ToolRuntimeStatusMasks.DepthFailed;
            if ((status & disablingBits) != 0u)
            {
                status |= ToolRuntimeStatusMasks.Disabled;
                status &= ~ToolRuntimeStatusMasks.Active;
            }

            return status;
        }

        private static uint ApplyHeatWarningHapticGate(uint status, float heat)
        {
            if (heat >= HeatWarningThreshold && (status & ToolRuntimeStatusMasks.HeatWarningHapticQueued) == 0u)
            {
                ToolHapticsRuntime.TryEnqueueSinusoidalCommand(
                    0f,
                    0.82f,
                    0.12f,
                    28f,
                    ToolHapticsRuntime.PriorityCritical,
                    0b0010);
                return status | ToolRuntimeStatusMasks.HeatWarningHapticQueued;
            }

            if (heat <= HeatWarningResetThreshold)
                return status & ~ToolRuntimeStatusMasks.HeatWarningHapticQueued;

            return status;
        }

        private float ResolveDepthMeters()
        {
            IPlayerRuntimeContext playerContext = _playerRuntimeContext;
            if (playerContext != null &&
                playerContext.TryGetMovementRuntimeState(out PlayerMovementRuntimeState movementState) &&
                math.isfinite(movementState.DepthMeters))
                return math.max(0f, movementState.DepthMeters);

            return 0f;
        }

        private static byte ResolveToolTypeId(uint toolId)
        {
            byte typeId = (byte)(toolId ^ (toolId >> 8) ^ (toolId >> 16) ^ (toolId >> 24));
            return typeId != 0 ? typeId : (byte)1;
        }

        private void WriteSlotMirrors(ref EquipmentVaultViews views, int slotIndex, in ToolState state)
        {
            if ((uint)slotIndex >= MaxTrackedTools)
                return;

            views.ToolTypes[slotIndex] = state.ToolTypeId;
            views.CurrentHeat[slotIndex] = state.InternalHeat;
            views.BatteryCharge[slotIndex] = state.CurrentBattery;
            views.StatusMasks[slotIndex] = state.StatusMask;
            views.EnvironmentHeat01[slotIndex] = math.saturate(views.EnvironmentHeat01[slotIndex]);
            PublishToolStateChanged(ref views, slotIndex, in state, forceHolstered: false);
        }

        private void ClearSlotMirrors(ref EquipmentVaultViews views, int slotIndex)
        {
            if ((uint)slotIndex >= MaxTrackedTools)
                return;

            ToolState previousState = views.ToolStates.IsCreated ? views.ToolStates[slotIndex] : default;
            PublishToolStateChanged(ref views, slotIndex, in previousState, forceHolstered: true);

            views.ToolTypes[slotIndex] = 0;
            views.CurrentHeat[slotIndex] = 0f;
            views.BatteryCharge[slotIndex] = 0f;
            views.StatusMasks[slotIndex] = 0u;
            views.EnvironmentHeat01[slotIndex] = 0f;
            _lastPublishedEquippedMask &= ~(1u << slotIndex);
        }

        private void PublishToolStateChanged(ref EquipmentVaultViews views, int slotIndex, in ToolState state, bool forceHolstered)
        {
            if ((uint)slotIndex >= MaxTrackedTools)
                return;

            PlayerTool owner = _toolOwners[slotIndex];
            if (owner == null)
            {
                _lastPublishedEquippedMask &= ~(1u << slotIndex);
                return;
            }

            uint slotBit = 1u << slotIndex;
            bool equipped = !forceHolstered && owner.IsEquipped;
            bool holsterTransition = false;
            if (!equipped && !forceHolstered)
            {
                if ((_lastPublishedEquippedMask & slotBit) == 0u)
                    return;

                holsterTransition = true;
            }

            ToolRuntimeStats stats = views.ToolStats[slotIndex];
            float capacity = math.max(0.1f, stats.BatteryCapacity);
            float battery01 = math.saturate(state.CurrentBattery * math.rcp(capacity));
            float heat01 = math.saturate(state.InternalHeat);
            float durability01 = math.saturate(state.Durability);
            float distanceMeters = math.max(0f, stats.MaxRange);
            int ammoUnits = math.clamp((int)math.round(battery01 * 100f), 0, (int)ushort.MaxValue);
            bool visible = equipped && owner.isActiveAndEnabled;
            uint statusMask = state.StatusMask;
            bool terminalHolster = forceHolstered || holsterTransition;
            if (terminalHolster)
            {
                statusMask |= ToolRuntimeStatusMasks.Disabled;
                statusMask &= ~ToolRuntimeStatusMasks.Active;
            }

            float quality01 = math.saturate(math.isfinite(_lastGlobalQualityWeight) ? _lastGlobalQualityWeight : 1f);

            byte flags = 0;
            if (equipped)
                flags |= ToolStateChangedSignal.FlagEquipped;
            if (visible)
                flags |= ToolStateChangedSignal.FlagVisible;

            ToolStateChangedSignal signal = new ToolStateChangedSignal
            {
                ToolHash = owner.RuntimeToolId,
                Frame = _equipmentTickIndex != 0u ? _equipmentTickIndex : 1u,
                Battery01 = math.isfinite(battery01) ? battery01 : 0f,
                Heat01 = math.isfinite(heat01) ? heat01 : 0f,
                DistanceMeters = math.isfinite(distanceMeters) ? distanceMeters : 0f,
                Durability01 = math.isfinite(durability01) ? durability01 : 0f,
                StatusMask = statusMask,
                AmmoUnits = (ushort)ammoUnits,
                Flags = flags,
                ToolTypeId = state.ToolTypeId
            };

            if (!terminalHolster && !ShouldPublishToolStateChanged(slotIndex, in signal, quality01))
                return;

            SignalBus<ToolStateChangedSignal>.TryPushTracked(in signal, ref s_x001ModularEquipmentEngineSignalPushDropCount);
            _lastPublishedToolStateChangedSignal = signal;
            _lastPublishedToolStateChangedSlot = slotIndex;
            _lastPublishedToolStateChangedValid = 1;
            if (equipped)
                _lastPublishedEquippedMask |= slotBit;
            else
                _lastPublishedEquippedMask &= ~slotBit;
        }

        private bool ShouldPublishToolStateChanged(int slotIndex, in ToolStateChangedSignal signal, float quality01)
        {
            if (_lastPublishedToolStateChangedValid == 0 || _lastPublishedToolStateChangedSlot != slotIndex)
                return true;

            ToolStateChangedSignal latest = _lastPublishedToolStateChangedSignal;
            if (latest.ToolHash != signal.ToolHash ||
                latest.Flags != signal.Flags ||
                latest.StatusMask != signal.StatusMask ||
                latest.AmmoUnits != signal.AmmoUnits ||
                latest.ToolTypeId != signal.ToolTypeId)
            {
                return true;
            }

            float floatDelta = ResolveToolSignalFloatDelta(quality01);
            return math.abs(latest.Battery01 - signal.Battery01) >= floatDelta ||
                math.abs(latest.Heat01 - signal.Heat01) >= floatDelta ||
                math.abs(latest.Durability01 - signal.Durability01) >= floatDelta ||
                math.abs(latest.DistanceMeters - signal.DistanceMeters) >= ToolSignalDistanceDeltaMeters;
        }

        private static float ResolveToolSignalFloatDelta(float quality01)
        {
            float q = math.saturate(math.isfinite(quality01) ? quality01 : 1f);
            float lowToMid = math.smoothstep(0f, 0.45f, q);
            float midToHigh = math.smoothstep(0.35f, 0.75f, q);
            float highToUltra = math.smoothstep(0.65f, 1f, q);
            float lowMidDelta = math.lerp(ToolSignalMinQualityFloatDelta, ToolSignalMidTierFloatDelta, lowToMid);
            float highUltraDelta = math.lerp(ToolSignalHighTierFloatDelta, ToolSignalUltraTierFloatDelta, highToUltra);
            return math.lerp(lowMidDelta, highUltraDelta, midToHigh);
        }

        private void CacheRegistryDependenciesCold()
        {
            _dataVault = GlobalRegistry.DataVault;
            _thermodynamicsService = GlobalRegistry.ThermodynamicsService;
            _powerGridService = GlobalRegistry.PowerGrid;
            _toolDurabilityService = GlobalRegistry.ToolDurabilityService;
            _playerRuntimeContext = GlobalRegistry.Player;
            _submarineRuntimeContext = GlobalRegistry.Submarine;
        }

        private void ApplyRegistryServiceRebind(GlobalRegistryServiceSlot serviceSlot, object currentService)
        {
            switch (serviceSlot)
            {
                case GlobalRegistryServiceSlot.Dispatcher:
                    TryUnregisterUpdatable();
                    TryUnregisterLateFrame();
                    if (currentService != null && isActiveAndEnabled && _isInitialized)
                    {
                        TryRegisterUpdatable();
                        TryRegisterLateFrame();
                    }
                    break;
                case GlobalRegistryServiceSlot.ModularEquipment:
                    _registeredService = ReferenceEquals(currentService, this);
                    break;
                case GlobalRegistryServiceSlot.DataVault:
                    ApplyDataVaultRebind(currentService as IDataVault);
                    break;
                case GlobalRegistryServiceSlot.ThermodynamicsService:
                    _thermodynamicsService = currentService as IThermodynamicsService;
                    break;
                case GlobalRegistryServiceSlot.PowerGrid:
                    _powerGridService = currentService as IPowerGridService;
                    break;
                case GlobalRegistryServiceSlot.ToolDurabilityRuntime:
                    _toolDurabilityService = currentService as IToolDurabilityService;
                    RegisterDurabilityMirrorsCold();
                    break;
                case GlobalRegistryServiceSlot.Player:
                    _playerRuntimeContext = currentService as IPlayerRuntimeContext;
                    break;
                case GlobalRegistryServiceSlot.Submarine:
                    _submarineRuntimeContext = currentService as ISubmarineRuntimeContext;
                    break;
            }
        }

        private void ApplyDataVaultRebind(IDataVault nextVault)
        {
            if (ReferenceEquals(_dataVault, nextVault))
                return;

            DrainEquipmentIntegrationLocksForLifecycle();

            FlushPendingFaultDumps();
            if (!TryReleaseEquipmentVaultHandlesForLifecycle(_dataVault))
            {
                _isInitialized = false;
                _equipmentSignalLanesReady = false;
                return;
            }

            _dataVault = nextVault;
            _isInitialized = false;
            _equipmentSignalLanesReady = false;
            _lastPublishedEquippedMask = 0u;
            _lastPublishedToolStateChangedSignal = default;
            _lastPublishedToolStateChangedSlot = -1;
            _lastPublishedToolStateChangedValid = 0;
            _lastTelemetryActiveMask = 0u;
            _thermalGridCellCount = 0;

            if (nextVault == null || !CanOwnServiceSlot())
                return;

            InitializeActiveEquipmentNativeState();
            if (!TryAcquireEquipmentViewsWriteLock(out EquipmentVaultViews views))
                return;

            try
            {
                ClearNativeArray(views.ToolStates);
                ClearNativeArray(views.ToolStats);
                ClearNativeArray(views.ToolTypes);
                ClearNativeArray(views.CurrentHeat);
                ClearNativeArray(views.BatteryCharge);
                ClearNativeArray(views.StatusMasks);
                ClearNativeArray(views.EnvironmentHeat01);
            }
            finally
            {
                ReleaseEquipmentWriteLocks(ref views);
            }

            _isInitialized = true;
            TryRegisterService();
            TryRegisterUpdatable();
            TryRegisterLateFrame();
        }

        void IGlobalRegistryHotSwapRefListener.OnGlobalRegistryServiceRebound(GlobalRegistryServiceSlot serviceSlot, ref object currentService)
        {
            ApplyRegistryServiceRebind(serviceSlot, currentService);
        }

        void IGlobalRegistryHotSwapListener.OnGlobalRegistryServiceReplaced(GlobalRegistryServiceSlot serviceSlot, object previousService, object currentService)
        {
            ApplyRegistryServiceRebind(serviceSlot, currentService);
        }

        private void TryRegisterHotSwap()
        {
            if (_registeredHotSwap)
                return;

            _registeredHotSwap = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterHotSwap()
        {
            if (!_registeredHotSwap)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _registeredHotSwap = false;
        }

        private bool TryRegisterService()
        {
            if (_runtimeOwnerAborted)
                return false;

            if (_registeredService)
                return true;

            if (TryAbortForUsableExistingRuntime())
                return false;

            IModularEquipmentService registered = GlobalRegistry.ModularEquipment;
            if (!ReferenceEquals(registered, null) && !ReferenceEquals(registered, this))
            {
                // hop2 LIVE Tool FAIL root cause:
                // ScannerTool (and other tool) prefabs carry ModularEquipmentEngine. On pool spawn,
                // OnEnable/InitializeService tried to steal the already-ready registry slot via
                // Unregister+Register, which throws under Ready-lock and aborts SpawnNewToolImmediate
                // (CurrentTool stayed null despite toolSlotsWithAvailableTool=4 and swap command).
                // Never steal once the registry is ready, and never Destroy(gameObject) here — that
                // would delete the tool instance this component is attached to.
                if (GlobalRegistry.Phase == GlobalRegistry.RegistryPhase.Ready ||
                    IsAttachedToPlayerToolPrefab())
                {
                    AbortServiceOwnershipWithoutDestroyingHost();
                    return false;
                }

                ModularEquipmentEngine staleRuntime = registered as ModularEquipmentEngine;
                if (ReferenceEquals(staleRuntime, null))
                {
                    AbortServiceOwnershipWithoutDestroyingHost();
                    if (!IsAttachedToPlayerToolPrefab())
                        Destroy(gameObject);
                    return false;
                }

                try
                {
                    GlobalRegistry.UnregisterModularEquipmentService(registered);
                }
                catch (System.InvalidOperationException)
                {
                    AbortServiceOwnershipWithoutDestroyingHost();
                    return false;
                }

                staleRuntime._registeredService = false;
                staleRuntime._isInitialized = false;
            }

            if (TryAbortForUsableExistingRuntime())
                return false;

            try
            {
                GlobalRegistry.RegisterModularEquipmentService(this);
            }
            catch (System.InvalidOperationException)
            {
                // Ready-lock / publication guard: leave the existing owner in place.
                AbortServiceOwnershipWithoutDestroyingHost();
                return false;
            }

            _registeredService = ReferenceEquals(GlobalRegistry.ModularEquipment, this);
            _runtimeOwnerAborted = !_registeredService;
            if (_runtimeOwnerAborted && !IsAttachedToPlayerToolPrefab())
                Destroy(gameObject);
            return _registeredService;
        }

        private bool IsAttachedToPlayerToolPrefab()
        {
            // Tool pool instances host this component for editor wiring only; the live
            // IModularEquipmentService owner is the DDOL EnsureRuntimeInstance engine.
            return GetComponentInParent<PlayerTool>() != null || GetComponent<PlayerTool>() != null;
        }

        private void AbortServiceOwnershipWithoutDestroyingHost()
        {
            _runtimeOwnerAborted = true;
            _registeredService = false;
            // Stop further enable/disable service churn without destroying the tool GO.
            if (IsAttachedToPlayerToolPrefab() && enabled)
                enabled = false;
        }

        private bool TryAbortForUsableExistingRuntime()
        {
            IModularEquipmentService registered = GlobalRegistry.ModularEquipment;
            if (ReferenceEquals(registered, null) || ReferenceEquals(registered, this))
                return false;

            if (IsModularEquipmentRuntimeUsable(registered))
            {
                AbortServiceOwnershipWithoutDestroyingHost();
                if (!IsAttachedToPlayerToolPrefab())
                    Destroy(gameObject);
                return true;
            }


            ModularEquipmentEngine staleRuntime = registered as ModularEquipmentEngine;
            if (!ReferenceEquals(staleRuntime, null))
            {
                // Ready-lock can reject Unregister; treat as "existing owner stays" and abort this instance.
                if (GlobalRegistry.Phase == GlobalRegistry.RegistryPhase.Ready ||
                    IsAttachedToPlayerToolPrefab())
                {
                    AbortServiceOwnershipWithoutDestroyingHost();
                    return true;
                }

                try
                {
                    GlobalRegistry.UnregisterModularEquipmentService(registered);
                }
                catch (System.InvalidOperationException)
                {
                    AbortServiceOwnershipWithoutDestroyingHost();
                    return true;
                }

                staleRuntime._registeredService = false;
                staleRuntime._isInitialized = false;
            }

            return false;
        }

        private static bool IsModularEquipmentRuntimeUsable(IModularEquipmentService service)

        {
            if (ReferenceEquals(service, null))
                return false;

            ModularEquipmentEngine engine = service as ModularEquipmentEngine;
            return ReferenceEquals(engine, null) ||
                   (engine != null &&
                    engine._registeredService &&
                    engine.isActiveAndEnabled &&
                    !engine._runtimeOwnerAborted);
        }

        public bool TryGetWirelessBrownoutFeedback(uint toolId, out float flickerScalar)
        {
            flickerScalar = 0f;
            if (!_wirelessBrownoutActive || !_isInitialized || !TryResolveOwnerMirrorSlot(toolId, out int slotIndex))
                return false;
            if (!TryReadToolStatesNoAcquire(out NativeArray<ToolState>.ReadOnly toolStates) ||
                (uint)slotIndex >= (uint)toolStates.Length)
                return false;

            if ((toolStates[slotIndex].UpgradeBitmask64 & (ulong)ToolUpgradeBits.WirelessCharging) == 0UL)
                return false;

            float pulse = 0.35f + (0.65f * math.abs(FastTriangleSigned(_brownoutPulseTime * WirelessBrownoutPulseCycles)));
            flickerScalar = pulse;
            return true;
        }

        public bool TryGetToolBrownoutFeedback(uint toolId, out float flickerScalar)
        {
            flickerScalar = 0f;
            if (!_wirelessBrownoutActive || !_isInitialized || !TryResolveOwnerMirrorSlot(toolId, out int slotIndex))
                return false;

            if (slotIndex < 0 || slotIndex >= MaxTrackedTools || !_slotUsed[slotIndex])
                return false;

            flickerScalar = math.saturate(0.5f + (0.5f * FastTriangleSigned(_brownoutPulseTime * ToolBrownoutPulseCycles)));
            return true;
        }

        private static float FastTriangleSigned(float phase)
        {
            float triangle01 = 1f - math.abs(math.frac(phase + 0.25f) * 2f - 1f);
            return triangle01 * 2f - 1f;
        }

        private void TryUnregisterService()
        {
            if (!_registeredService)
                return;

            GlobalRegistry.UnregisterModularEquipmentService(this);
            _registeredService = false;
        }

        private void TryRegisterUpdatable()
        {
            if (_registeredUpdatable || !Application.isPlaying)
                return;

            if (GlobalRegistry.Dispatcher == null)
                return;

            _registeredUpdatable = GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.Core);
        }

        private void TryUnregisterUpdatable()
        {
            if (!_registeredUpdatable)
                return;

            GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Core);
            _registeredUpdatable = false;
        }

        private void TryRegisterLateFrame()
        {
            if (_registeredLateFrame || !Application.isPlaying)
                return;

            if (GlobalRegistry.Dispatcher == null)
                return;

            _registeredLateFrame = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Core);
        }

        private void TryUnregisterLateFrame()
        {
            if (!_registeredLateFrame)
                return;

            GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Core);
            _registeredLateFrame = false;
        }

        private void HandleSceneUnloaded(Scene unloadedScene)
        {
            if (gameObject.scene != unloadedScene)
                return;

            ShutdownServiceState();
        }

        private void RefreshWirelessBrownoutFromPowerSnapshot()
        {
            IPowerGridService powerGrid = _powerGridService;
            if (powerGrid == null)
                return;

            float totalConsumption = math.max(0f, powerGrid.TotalConsumption);
            float totalGeneration = math.max(0f, powerGrid.TotalGeneration);
            float supplyRatio = totalConsumption > 0.0001f
                ? math.saturate(totalGeneration * math.rcp(math.max(totalConsumption, 0.0001f)))
                : 1f;
            BatteryRuntimeSnapshot battery = powerGrid.BatterySnapshot;
            if (battery.EmergencyReserveActive != 0)
                supplyRatio = math.min(supplyRatio, math.saturate(battery.ChargeNormalized));

            _wirelessBrownoutActive = supplyRatio < 0.40f;
            if (!_wirelessBrownoutActive)
                _brownoutPulseTime = 0f;
        }

        private bool IsOverchargeRequested(int slotIndex)
        {
            PlayerTool owner = slotIndex >= 0 && slotIndex < MaxTrackedTools ? _toolOwners[slotIndex] : null;
            return owner != null && owner.IsRuntimeOverchargeRequested();
        }

        private void TriggerOverchargeExplosion(ref EquipmentVaultViews views, int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= MaxTrackedTools)
                return;

            PlayerTool owner = _toolOwners[slotIndex];
            if (owner == null)
                return;

            owner.HandleRuntimeOverchargeFailure(OverchargeExplosionPlayerDamage);

            uint runtimeToolId = owner.RuntimeToolId;
            ToolState failedState = views.ToolStates[slotIndex];
            PublishToolStateChanged(ref views, slotIndex, in failedState, forceHolstered: true);
            _toolOwners[slotIndex] = null;
            _slotUsed[slotIndex] = false;
            views.ToolStates[slotIndex] = default;
            views.ToolStats[slotIndex] = default;
            ClearActiveEquipmentSlot(ref views, slotIndex);
            ClearSlotMirrors(ref views, slotIndex);
            ClearModuleRuleMirror(slotIndex);
            ClearUpgradeMatrixStaging(ref views, slotIndex);
        }

        private void DisposeNativeState()
        {
            DrainEquipmentIntegrationLocksForLifecycle();

            FlushPendingFaultDumps();
            for (int i = 0; i < MaxTrackedTools; i++)
            {
                _toolOwners[i] = null;
                _slotUsed[i] = false;
            }

            for (int i = 0; i < _moduleRuleSlots.Length; i++)
                _moduleRuleSlots[i] = default;

            bool vaultHandlesReleased = TryReleaseEquipmentVaultHandlesForLifecycle(_dataVault);
            FlushPendingFaultDumps();

            _isInitialized = false;
            _equipmentSignalLanesReady = false;
            if (vaultHandlesReleased)
            {
                _equipmentFaultDumpPending = false;
                _upgradeTelemetryFaultDumpPending = false;
            }
            _lastPublishedEquippedMask = 0u;
            _lastPublishedToolStateChangedSignal = default;
            _lastPublishedToolStateChangedSlot = -1;
            _lastPublishedToolStateChangedValid = 0;
            _externalActiveToolMask = 0u;
            _lastTelemetryActiveMask = 0u;
            _thermalGridWidth = 0;
            _thermalGridHeight = 0;
            _thermalGridDepth = 0;
            _thermalGridVersion = 0;
            _thermalGridCellCount = 0;
            _equipmentTickIndex = 0u;
            _toolDurabilityService = null;
        }

        private bool DrainEquipmentIntegrationLocksForLifecycle()
        {
            if (_equipmentIntegrationScheduled)
                CompleteActiveEquipmentJob(forceComplete: true);
            else
                ReleaseEquipmentIntegrationWriteLocks();

            return true;
        }

        private bool TryReleaseEquipmentVaultHandlesForLifecycle(IDataVault vault)
        {
            bool buffersReleased = ReleaseEquipmentVaultHandles(vault);
            if (buffersReleased)
            {
                ClearEquipmentVaultHandles();
                return true;
            }

            TryRecordEquipmentWriteLockContention(EquipmentFaultWriteLockReleaseFailure);
            _equipmentFaultDumpPending = true;
            return false;
        }

        private bool ReleaseEquipmentVaultHandles(IDataVault vault)
        {
            if (vault == null)
                return !HasEquipmentVaultHandles();

            bool released = true;
            released &= ReleaseEquipmentVaultHandle(vault, ref _toolStatesHandle);
            released &= ReleaseEquipmentVaultHandle(vault, ref _toolStatsHandle);
            released &= ReleaseEquipmentVaultHandle(vault, ref _toolTypesHandle);
            released &= ReleaseEquipmentVaultHandle(vault, ref _currentHeatHandle);
            released &= ReleaseEquipmentVaultHandle(vault, ref _batteryChargeHandle);
            released &= ReleaseEquipmentVaultHandle(vault, ref _statusMasksHandle);
            released &= ReleaseEquipmentVaultHandle(vault, ref _environmentHeat01Handle);
            released &= ReleaseEquipmentVaultHandle(vault, ref _activeEquipmentStatesHandle);
            released &= ReleaseEquipmentVaultHandle(vault, ref _publishedActiveEquipmentStatesHandle);
            released &= ReleaseEquipmentVaultHandle(vault, ref _activeEquipmentAupSamplesHandle);
            released &= ReleaseEquipmentVaultHandle(vault, ref _activeEquipmentGridLoadRequestsHandle);
            released &= ReleaseEquipmentVaultHandle(vault, ref _activeEquipmentWearDrainRatesHandle);
            released &= ReleaseEquipmentVaultHandle(vault, ref _equipmentTelemetryRingHandle);
            released &= ReleaseEquipmentVaultHandle(vault, ref _equipmentTelemetryCursorHandle);
            released &= ReleaseEquipmentVaultHandle(vault, ref _flashlightTelemetryRingHandle);
            released &= ReleaseEquipmentVaultHandle(vault, ref _flashlightTelemetryCursorHandle);
            released &= ReleaseEquipmentVaultHandle(vault, ref _equipmentIntegrationCountersHandle);
            released &= ReleaseEquipmentVaultHandle(vault, ref _equipmentTuningHandle);
            released &= ReleaseEquipmentVaultHandle(vault, ref _equipmentHardwareSpecsHandle);
            released &= ReleaseEquipmentVaultHandle(vault, ref _upgradeMatrixMasksHandle);
            released &= ReleaseEquipmentVaultHandle(vault, ref _upgradeMatrixBaseStatsHandle);
            released &= ReleaseEquipmentVaultHandle(vault, ref _upgradeMatrixCompiledStatsHandle);
            released &= ReleaseEquipmentVaultHandle(vault, ref _upgradeMatrixToolLutHandle);
            released &= ReleaseEquipmentVaultHandle(vault, ref _upgradeMatrixToolRulesHandle);
            released &= ReleaseEquipmentVaultHandle(vault, ref _upgradeMatrixToolProfilesHandle);
            released &= ReleaseEquipmentVaultHandle(vault, ref _upgradeMatrixTelemetryRingHandle);
            released &= ReleaseEquipmentVaultHandle(vault, ref _upgradeMatrixTelemetryCursorHandle);
            released &= ReleaseEquipmentVaultHandle(vault, ref _upgradeMatrixVisualStatesHandle);
            return released;
        }

        private void ClearEquipmentVaultHandles()
        {
            _toolStatesHandle = default;
            _toolStatsHandle = default;
            _toolTypesHandle = default;
            _currentHeatHandle = default;
            _batteryChargeHandle = default;
            _statusMasksHandle = default;
            _environmentHeat01Handle = default;
            _activeEquipmentStatesHandle = default;
            _publishedActiveEquipmentStatesHandle = default;
            _activeEquipmentAupSamplesHandle = default;
            _activeEquipmentGridLoadRequestsHandle = default;
            _activeEquipmentWearDrainRatesHandle = default;
            _equipmentTelemetryRingHandle = default;
            _equipmentTelemetryCursorHandle = default;
            _flashlightTelemetryRingHandle = default;
            _flashlightTelemetryCursorHandle = default;
            _equipmentIntegrationCountersHandle = default;
            _equipmentTuningHandle = default;
            _equipmentHardwareSpecsHandle = default;
            _upgradeMatrixMasksHandle = default;
            _upgradeMatrixBaseStatsHandle = default;
            _upgradeMatrixCompiledStatsHandle = default;
            _upgradeMatrixToolLutHandle = default;
            _upgradeMatrixToolRulesHandle = default;
            _upgradeMatrixToolProfilesHandle = default;
            _upgradeMatrixTelemetryRingHandle = default;
            _upgradeMatrixTelemetryCursorHandle = default;
            _upgradeMatrixVisualStatesHandle = default;
        }

        private bool HasEquipmentVaultHandles()
        {
            return IsVaultGenerationHandleCreated(in _toolStatesHandle) ||
                   IsVaultGenerationHandleCreated(in _toolStatsHandle) ||
                   IsVaultGenerationHandleCreated(in _toolTypesHandle) ||
                   IsVaultGenerationHandleCreated(in _currentHeatHandle) ||
                   IsVaultGenerationHandleCreated(in _batteryChargeHandle) ||
                   IsVaultGenerationHandleCreated(in _statusMasksHandle) ||
                   IsVaultGenerationHandleCreated(in _environmentHeat01Handle) ||
                   IsVaultGenerationHandleCreated(in _activeEquipmentStatesHandle) ||
                   IsVaultGenerationHandleCreated(in _publishedActiveEquipmentStatesHandle) ||
                   IsVaultGenerationHandleCreated(in _activeEquipmentAupSamplesHandle) ||
                   IsVaultGenerationHandleCreated(in _activeEquipmentGridLoadRequestsHandle) ||
                   IsVaultGenerationHandleCreated(in _activeEquipmentWearDrainRatesHandle) ||
                   IsVaultGenerationHandleCreated(in _equipmentTelemetryRingHandle) ||
                   IsVaultGenerationHandleCreated(in _equipmentTelemetryCursorHandle) ||
                   IsVaultGenerationHandleCreated(in _flashlightTelemetryRingHandle) ||
                   IsVaultGenerationHandleCreated(in _flashlightTelemetryCursorHandle) ||
                   IsVaultGenerationHandleCreated(in _equipmentIntegrationCountersHandle) ||
                   IsVaultGenerationHandleCreated(in _equipmentTuningHandle) ||
                   IsVaultGenerationHandleCreated(in _equipmentHardwareSpecsHandle) ||
                   IsVaultGenerationHandleCreated(in _upgradeMatrixMasksHandle) ||
                   IsVaultGenerationHandleCreated(in _upgradeMatrixBaseStatsHandle) ||
                   IsVaultGenerationHandleCreated(in _upgradeMatrixCompiledStatsHandle) ||
                   IsVaultGenerationHandleCreated(in _upgradeMatrixToolLutHandle) ||
                   IsVaultGenerationHandleCreated(in _upgradeMatrixToolRulesHandle) ||
                   IsVaultGenerationHandleCreated(in _upgradeMatrixToolProfilesHandle) ||
                   IsVaultGenerationHandleCreated(in _upgradeMatrixTelemetryRingHandle) ||
                   IsVaultGenerationHandleCreated(in _upgradeMatrixTelemetryCursorHandle) ||
                   IsVaultGenerationHandleCreated(in _upgradeMatrixVisualStatesHandle);
        }

        private static bool ReleaseEquipmentVaultHandle<T>(IDataVault vault, ref VaultGenerationHandle<T> handle)
            where T : struct
        {
            if (!IsVaultGenerationHandleCreated(in handle))
                return true;

            if (!vault.ReleaseBuffer(in handle))
                return false;

            handle = default;
            return true;
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
        private unsafe struct ClearActiveEquipmentNativeStateJob : IJobParallelFor
        {
            [NoAlias] [NativeDisableUnsafePtrRestriction] public ActiveEquipmentDTO* ActiveEquipment;
            [NoAlias] [NativeDisableUnsafePtrRestriction] public ActiveEquipmentDTO* PublishedEquipment;
            [NoAlias] [NativeDisableUnsafePtrRestriction] public double3* AupSamples;
            [NoAlias] [NativeDisableUnsafePtrRestriction] public EquipmentGridLoadRequest* GridLoadRequests;
            [NoAlias] [NativeDisableUnsafePtrRestriction] public float* WearDrainRates;
            [NoAlias] [NativeDisableUnsafePtrRestriction] public EquipmentTelemetryEntry* TelemetryRing;
            [NoAlias] [NativeDisableUnsafePtrRestriction] public int* TelemetryCursor;
            [NoAlias] [NativeDisableUnsafePtrRestriction] public FlashlightTelemetryEntry* FlashlightTelemetryRing;
            [NoAlias] [NativeDisableUnsafePtrRestriction] public int* FlashlightTelemetryCursor;
            [NoAlias] [NativeDisableUnsafePtrRestriction] public EquipmentIntegrationCounters* IntegrationCounters;
            [NoAlias] [NativeDisableUnsafePtrRestriction] public EquipmentHardwareSpecDTO* HardwareSpecs;
            public int ActiveLength;
            public int PublishedLength;
            public int AupLength;
            public int GridLoadRequestLength;
            public int WearDrainLength;
            public int TelemetryLength;
            public int CursorLength;
            public int FlashlightTelemetryLength;
            public int FlashlightCursorLength;
            public int CounterLength;
            public int HardwareSpecLength;

            public void Execute(int index)
            {
                if (ActiveEquipment != null && (uint)index < (uint)ActiveLength)
                    ActiveEquipment[index] = default;
                if (PublishedEquipment != null && (uint)index < (uint)PublishedLength)
                    PublishedEquipment[index] = default;
                if (AupSamples != null && (uint)index < (uint)AupLength)
                    AupSamples[index] = default;
                if (GridLoadRequests != null && (uint)index < (uint)GridLoadRequestLength)
                    GridLoadRequests[index] = default;
                if (WearDrainRates != null && (uint)index < (uint)WearDrainLength)
                    WearDrainRates[index] = 0f;
                if (TelemetryRing != null && (uint)index < (uint)TelemetryLength)
                    TelemetryRing[index] = default;
                if (TelemetryCursor != null && (uint)index < (uint)CursorLength)
                    TelemetryCursor[index] = 0;
                if (FlashlightTelemetryRing != null && (uint)index < (uint)FlashlightTelemetryLength)
                    FlashlightTelemetryRing[index] = default;
                if (FlashlightTelemetryCursor != null && (uint)index < (uint)FlashlightCursorLength)
                    FlashlightTelemetryCursor[index] = 0;
                if (IntegrationCounters != null && (uint)index < (uint)CounterLength)
                    IntegrationCounters[index] = default;
                if (HardwareSpecs != null && (uint)index < (uint)HardwareSpecLength)
                    HardwareSpecs[index] = default;
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
        private unsafe struct GenerateMockThermalEquipmentJob : IJobParallelFor
        {
            [NoAlias] [NativeDisableUnsafePtrRestriction] public ActiveEquipmentDTO* Equipment;
            [NoAlias] [NativeDisableUnsafePtrRestriction] public double3* ToolAups;
            public int ToolCount;
            public double3 RootAup;
            public uint BaseToolHash;

            public void Execute(int index)
            {
                if ((uint)index >= (uint)math.max(0, ToolCount))
                {
                    Equipment[index] = default;
                    ToolAups[index] = default;
                    return;
                }

                float rank = index + 1f;
                bool dryHot = (index & 1) != 0;
                float battery = math.max(0f, 92f - (rank * 13f));
                float heat = dryHot ? math.saturate(0.72f + rank * 0.055f) : math.saturate(0.08f * rank);
                Equipment[index] = new ActiveEquipmentDTO
                {
                    ToolHashID = BaseToolHash + (uint)index,
                    CurrentBattery = battery,
                    ThermalLoad = heat,
                    StateFlags = ActiveEquipmentStateFlags.Active | (dryHot ? 0u : ActiveEquipmentStateFlags.InWater),
                    PowerDrawRate = 6f + (rank * 3.25f),
                    HeatGenerationRate = dryHot ? 0.18f + (rank * 0.04f) : 0.06f + (rank * 0.025f),
                    _pad0 = 0,
                    _pad1 = 0
                };

                ToolAups[index] = RootAup + new double3(
                    EquipmentMockRootOffsetMeters * index,
                    0.0,
                    EquipmentMockRootOffsetMeters * (index & 1));
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
        private unsafe struct EquipmentStateIntegrationJob : IJobParallelFor
        {
            [NoAlias] [NativeDisableUnsafePtrRestriction] public ActiveEquipmentDTO* Equipment;
            [NoAlias] [NativeDisableUnsafePtrRestriction] public ToolState* ToolStates;
            [NoAlias] [NativeDisableUnsafePtrRestriction] public ToolRuntimeStats* Stats;
            [NoAlias] [NativeDisableUnsafePtrRestriction] public double3* ToolAups;
            [NoAlias] [NativeDisableUnsafePtrRestriction] public float* WearDrainRates;
            [NoAlias] [NativeDisableUnsafePtrRestriction] public float* ThermalGrid;
            [NoAlias] [NativeDisableUnsafePtrRestriction] public EquipmentGridLoadRequest* GridLoadRequests;
            [NoAlias] [NativeDisableUnsafePtrRestriction] public EquipmentIntegrationCounters* Counters;
            // SAFETY_JUSTIFICATION_PARAGRAPH_1:
            // SignalBus<T>.ParallelWriter is write-only and externally lane-owned; Unity's safety cannot prove the registry flushes it after this job fence.
            // SAFETY_JUSTIFICATION_PARAGRAPH_2:
            // Rejected main-thread signal emission because it would scan all tools after the job. Rejected per-tool managed events because they allocate and break Burst.
            // SAFETY_JUSTIFICATION_PARAGRAPH_3:
            // ModularEquipmentEngine schedules exactly one EquipmentStateIntegrationJob at a time, registers its JobHandle with H8Memory, and the typed lane is flushed by SignalBusRegistry after producer completion.
            [NativeDisableContainerSafetyRestriction] public global::Hecton8.Core.MpscSignalRingBuffer<EquipmentOverheatSignal>.ParallelWriter OverheatWriter;
            [NativeDisableParallelForRestriction] public NativeArray<int> OverheatWriterBudget;
            // SAFETY_JUSTIFICATION_PARAGRAPH_1:
            // SignalBus<T>.ParallelWriter safety is intentionally suppressed only for the depleted-tool signal lane; the queue is not snapshotted while the producer handle is live.
            // SAFETY_JUSTIFICATION_PARAGRAPH_2:
            // Rejected duplicating depletion state in a second NativeArray because it adds a write stream and stale cleanup. Rejected SignalBus writes on the main thread because it blocks readback.
            // SAFETY_JUSTIFICATION_PARAGRAPH_3:
            // Single equipment producer per frame, SignalBusRegistry snapshot consumer after dispatcher fencing; no other equipment job writes this lane in the same frame.
            [NativeDisableContainerSafetyRestriction] public global::Hecton8.Core.MpscSignalRingBuffer<ToolDepletedSignal>.ParallelWriter DepletedWriter;
            [NativeDisableParallelForRestriction] public NativeArray<int> DepletedWriterBudget;
            public int ToolCount;
            public int ThermalWidth;
            public int ThermalHeight;
            public int ThermalDepth;
            public int ThermalGridLength;
            public int ThermalVersion;
            public float ThermalCellSizeMeters;
            public double3 ThermalGridRootAup;
            public float DeltaSeconds;
            public uint Frame;
            public float AmbientFallbackCelsius;
            public float DryHeatMultiplier;
            public EquipmentTuningDTO Tuning;
            public uint FaultNonFiniteMask;
            public uint FaultGridInvalidMask;

            public void Execute(int i)
            {
                if ((uint)i >= (uint)math.max(0, ToolCount))
                    return;

                EquipmentIntegrationCounters counters = default;
                GridLoadRequests[i] = default;

                ref ActiveEquipmentDTO dto = ref UnsafeUtility.AsRef<ActiveEquipmentDTO>(Equipment + i);
                if (dto.ToolHashID == 0u)
                {
                    Counters[i] = counters;
                    return;
                }

                ref ToolState toolState = ref UnsafeUtility.AsRef<ToolState>(ToolStates + i);
                ToolRuntimeStats stats = Stats[i];
                float safeDelta = math.max(0f, DeltaSeconds);
                uint previousFlags = dto.StateFlags;
                uint flags = previousFlags & (ActiveEquipmentStateFlags.InWater | ActiveEquipmentStateFlags.GridPowered);
                bool requestedActive = (previousFlags & ActiveEquipmentStateFlags.Active) != 0u;
                bool gridPowered = (previousFlags & ActiveEquipmentStateFlags.GridPowered) != 0u;
                bool inWater = (previousFlags & ActiveEquipmentStateFlags.InWater) != 0u;
                float battery = SanitizeNonNegative(dto.CurrentBattery, ref counters, dto.ToolHashID);
                float heat = SanitizeNonNegative(dto.ThermalLoad, ref counters, dto.ToolHashID);
                float drawRate = SanitizeNonNegative(dto.PowerDrawRate, ref counters, dto.ToolHashID);
                float heatRate = SanitizeNonNegative(dto.HeatGenerationRate, ref counters, dto.ToolHashID);
                float ambientCelsius = SampleAmbientCelsius(i, ref counters, dto.ToolHashID);
                counters.LastAmbientCelsius = ambientCelsius;
                float requestedEnergy = requestedActive ? drawRate * safeDelta : 0f;
                float batteryDischargeMultiplier = ResolveBatteryDischargeMultiplier(ambientCelsius, in Tuning);
                float batteryRequestedEnergy = requestedEnergy * batteryDischargeMultiplier;
                float durability = toolState.Durability;
                if (IsFinite(durability))
                    durability = math.saturate(durability);
                else
                {
                    counters.FaultFlags |= FaultNonFiniteMask;
                    counters.LastFaultToolHashID = dto.ToolHashID;
                    durability = 0f;
                }

                if (requestedActive && requestedEnergy > 0f)
                {
                    if (gridPowered)
                    {
                        GridLoadRequests[i] = new EquipmentGridLoadRequest
                        {
                            ToolHashID = dto.ToolHashID,
                            EnergyWattSeconds = requestedEnergy,
                            Flags = ActiveEquipmentStateFlags.GridPowered,
                            Reserved0 = 0u
                        };
                        counters.GridDrawWattSeconds += requestedEnergy;
                    }
                    else
                    {
                        float previousBattery = battery;
                        battery = math.max(0f, battery - batteryRequestedEnergy);
                        counters.BatteryDrainWattSeconds += math.min(previousBattery, batteryRequestedEnergy);
                        if (previousBattery > 0.0001f && battery <= 0.0001f)
                        {
                            flags |= ActiveEquipmentStateFlags.Depleted;
                            if ((previousFlags & ActiveEquipmentStateFlags.Depleted) == 0u)
                            {
                                if (SignalBus<ToolDepletedSignal>.TryEnqueueBounded(DepletedWriter, DepletedWriterBudget, new ToolDepletedSignal
                                {
                                    ToolHashID = dto.ToolHashID,
                                    Frame = Frame,
                                    Battery01 = 0f,
                                    RequestedPower = drawRate * batteryDischargeMultiplier,
                                    StateFlags = flags,
                                    GridPowered = 0,
                                    Reserved0 = 0,
                                    Reserved1 = 0,
                                    Reserved2 = 0ul
                                }))
                                {
                                    counters.SignalCount++;
                                }
                                else
                                {
                                    counters.FaultFlags |= EquipmentFaultSignalDrop;
                                    counters.LastFaultToolHashID = dto.ToolHashID;
                                }
                            }
                        }
                    }
                }

                bool hasEnergy = gridPowered || battery > 0.0001f || drawRate <= 0.0001f;
                bool active = requestedActive && hasEnergy;
                if (active)
                    flags |= ActiveEquipmentStateFlags.Active;
                if (!gridPowered && battery <= 0.0001f)
                    flags |= ActiveEquipmentStateFlags.Depleted;

                if (active && WearDrainRates != null)
                {
                    float wearRate = SanitizeNonNegative(WearDrainRates[i], ref counters, dto.ToolHashID);
                    if (wearRate > 0f)
                    {
                        float previousDurability = durability;
                        durability = math.saturate(durability - (wearRate * safeDelta));
                        counters.WearDrainNormalized += math.max(0f, previousDurability - durability);
                    }
                }

                if (durability <= 0f)
                    flags &= ~ActiveEquipmentStateFlags.Active;

                float ambient01 = ResolveAmbientHeat01(ambientCelsius, in Tuning);
                float cooldownRate = math.max(0.05f, stats.CooldownRate);
                float waterMultiplier = inWater ? math.max(1f, Tuning.WaterCoolingMultiplier) : 1f;
                float quality = math.saturate(IsFinite(Tuning.GlobalQualityWeight) ? Tuning.GlobalQualityWeight : 1f);
                float coolingLod = math.lerp(0.70f, 1.0f, quality * quality * (3f - 2f * quality));
                float exchange = (ambient01 - heat) * cooldownRate * math.max(0f, Tuning.CoolingGain) * waterMultiplier * coolingLod * safeDelta;
                float dryHeatMultiplier = math.lerp(1f, math.max(1f, DryHeatMultiplier), inWater ? 0f : 1f);
                float generatedHeat = active ? heatRate * dryHeatMultiplier * safeDelta : 0f;
                heat = math.max(0f, heat + generatedHeat + exchange);

                bool wasOverheated = (previousFlags & ActiveEquipmentStateFlags.Overheated) != 0u;
                bool catastrophicOverheat = heat >= 1f;
                if (catastrophicOverheat || (wasOverheated && heat > OverheatRecoveryThreshold))
                {
                    flags |= ActiveEquipmentStateFlags.Overheated;
                    flags &= ~ActiveEquipmentStateFlags.Active;
                    if (catastrophicOverheat)
                    {
                        flags |= ActiveEquipmentStateFlags.Broken | ActiveEquipmentStateFlags.Depleted;
                        battery = 0f;
                        durability = 0f;
                    }

                    if (!wasOverheated)
                    {
                        if (SignalBus<EquipmentOverheatSignal>.TryEnqueueBounded(OverheatWriter, OverheatWriterBudget, new EquipmentOverheatSignal
                        {
                            ToolHashID = dto.ToolHashID,
                            Frame = Frame,
                            Heat01 = math.saturate(heat),
                            AmbientCelsius = ambientCelsius,
                            Severity01 = math.saturate((heat - 0.85f) * 6.666667f),
                            StateFlags = flags,
                            VisualOnly = catastrophicOverheat ? (byte)0 : (byte)1,
                            Reserved0 = 0,
                            Reserved1 = 0,
                            Reserved2 = 0u
                        }))
                        {
                            counters.SignalCount++;
                        }
                        else
                        {
                            counters.FaultFlags |= EquipmentFaultSignalDrop;
                            counters.LastFaultToolHashID = dto.ToolHashID;
                        }
                    }
                }

                if (!IsFinite(battery) || !IsFinite(heat))
                {
                    counters.FaultFlags |= FaultNonFiniteMask;
                    counters.LastFaultToolHashID = dto.ToolHashID;
                    flags |= ActiveEquipmentStateFlags.Faulted;
                    battery = 0f;
                    heat = 0f;
                    durability = 0f;
                }

                toolState.CurrentBattery = battery;
                toolState.InternalHeat = heat;
                toolState.Durability = durability;
                dto.CurrentBattery = battery;
                dto.ThermalLoad = heat;
                dto.StateFlags = flags;
                dto.PowerDrawRate = drawRate;
                dto.HeatGenerationRate = heatRate;
                counters.PeakThermal01 = math.max(counters.PeakThermal01, math.saturate(heat));
                counters.LastBattery01 = math.saturate(battery * math.rcp(math.max(0.0001f, stats.BatteryCapacity)));
                counters.ActiveCount += (flags & ActiveEquipmentStateFlags.Active) != 0u ? 1u : 0u;
                Counters[i] = counters;
            }

            private float SampleAmbientCelsius(int slotIndex, ref EquipmentIntegrationCounters counters, uint toolHash)
            {
                if (ThermalGrid == null)
                    return AmbientFallbackCelsius;

                if (ThermalWidth <= 0 || ThermalHeight <= 0 || ThermalDepth <= 0 || ThermalGridLength <= 0 || !IsFinite(ThermalCellSizeMeters) || ThermalCellSizeMeters <= 0f)
                {
                    counters.FaultFlags |= FaultGridInvalidMask;
                    counters.LastFaultToolHashID = toolHash;
                    return AmbientFallbackCelsius;
                }

                double3 delta = ToolAups[slotIndex] - ThermalGridRootAup;
                float3 local = new float3((float)delta.x, (float)delta.y, (float)delta.z);
                if (!math.all(math.isfinite(local)))
                {
                    counters.FaultFlags |= FaultNonFiniteMask;
                    counters.LastFaultToolHashID = toolHash;
                    return AmbientFallbackCelsius;
                }

                float invCell = math.rcp(ThermalCellSizeMeters);
                float3 gridPosition = local * invCell;
                int3 cell = (int3)math.floor(gridPosition);
                if (cell.x < 0 || cell.y < 0 || cell.z < 0 || cell.x >= ThermalWidth || cell.y >= ThermalHeight || cell.z >= ThermalDepth)
                    return AmbientFallbackCelsius;

                float nearest = ReadThermalCell(cell, ref counters, toolHash);
                if (!IsFinite(nearest))
                    return AmbientFallbackCelsius;

                float quality = math.saturate(IsFinite(Tuning.GlobalQualityWeight) ? Tuning.GlobalQualityWeight : 1f);
                float trilinearWeight = math.saturate((quality - 0.25f) * 1.3333334f);
                trilinearWeight = trilinearWeight * trilinearWeight * (3f - (2f * trilinearWeight));
                if (trilinearWeight <= 0.0001f)
                    return nearest;

                int3 cell1 = new int3(
                    math.min(cell.x + 1, ThermalWidth - 1),
                    math.min(cell.y + 1, ThermalHeight - 1),
                    math.min(cell.z + 1, ThermalDepth - 1));
                float3 t = math.saturate(gridPosition - cell);
                float c000 = nearest;
                float c100 = ReadThermalCell(new int3(cell1.x, cell.y, cell.z), ref counters, toolHash);
                float c010 = ReadThermalCell(new int3(cell.x, cell1.y, cell.z), ref counters, toolHash);
                float c110 = ReadThermalCell(new int3(cell1.x, cell1.y, cell.z), ref counters, toolHash);
                float c001 = ReadThermalCell(new int3(cell.x, cell.y, cell1.z), ref counters, toolHash);
                float c101 = ReadThermalCell(new int3(cell1.x, cell.y, cell1.z), ref counters, toolHash);
                float c011 = ReadThermalCell(new int3(cell.x, cell1.y, cell1.z), ref counters, toolHash);
                float c111 = ReadThermalCell(cell1, ref counters, toolHash);
                float c00 = math.lerp(c000, c100, t.x);
                float c10 = math.lerp(c010, c110, t.x);
                float c01 = math.lerp(c001, c101, t.x);
                float c11 = math.lerp(c011, c111, t.x);
                float c0 = math.lerp(c00, c10, t.y);
                float c1 = math.lerp(c01, c11, t.y);
                float trilinear = math.lerp(c0, c1, t.z);
                return IsFinite(trilinear) ? math.lerp(nearest, trilinear, trilinearWeight) : nearest;
            }

            private float ReadThermalCell(int3 cell, ref EquipmentIntegrationCounters counters, uint toolHash)
            {
                long index64 = (long)cell.x +
                               ((long)cell.y * ThermalWidth) +
                               ((long)cell.z * ThermalWidth * ThermalHeight);
                if ((ulong)index64 < (ulong)ThermalGridLength)
                {
                    float ambient = ThermalGrid[(int)index64];
                    if (IsFinite(ambient))
                        return ambient;
                }

                counters.FaultFlags |= FaultNonFiniteMask;
                counters.LastFaultToolHashID = toolHash;
                return float.NaN;
            }

            private float SanitizeNonNegative(float value, ref EquipmentIntegrationCounters counters, uint toolHash)
            {
                if (IsFinite(value))
                    return math.max(0f, value);

                counters.FaultFlags |= FaultNonFiniteMask;
                counters.LastFaultToolHashID = toolHash;
                return 0f;
            }

            private static float ResolveBatteryDischargeMultiplier(float ambientCelsius, in EquipmentTuningDTO tuning)
            {
                float coldDelta = math.max(0f, 2f - ambientCelsius);
                float penalty = math.max(0f, IsFinite(tuning.ColdBatteryPenaltyMultiplier) ? tuning.ColdBatteryPenaltyMultiplier : 1.85f);
                float quality = math.saturate(IsFinite(tuning.GlobalQualityWeight) ? tuning.GlobalQualityWeight : 1f);
                float cheapCurve = 1f + coldDelta * 0.018f * penalty;
                float expCurve = ApproximateExpDeterministic(math.min(4f, coldDelta * 0.0225f * penalty));
                float qualityBlend = quality * quality * (3f - 2f * quality);
                return math.clamp(math.lerp(cheapCurve, expCurve, qualityBlend), 1f, 4f);
            }

            private static float ApproximateExpDeterministic(float x)
            {
                float safeX = math.clamp(x, 0f, 4f);
                float x2 = safeX * safeX;
                float x3 = x2 * safeX;
                float x4 = x2 * x2;
                return 1f + safeX + (0.5f * x2) + (0.16666667f * x3) + (0.041666668f * x4);
            }

            private static float ResolveAmbientHeat01(float ambientCelsius, in EquipmentTuningDTO tuning)
            {
                float floor = IsFinite(tuning.AmbientHeatFloorCelsius) ? tuning.AmbientHeatFloorCelsius : -2f;
                float ceiling = IsFinite(tuning.AmbientHeatCeilingCelsius) ? tuning.AmbientHeatCeilingCelsius : 70f;
                float range = math.max(1f, ceiling - floor);
                return math.saturate((ambientCelsius - floor) * math.rcp(range));
            }

            private static bool IsFinite(float value)
            {
                return math.isfinite(value);
            }
        }

        private static class EquipmentLayoutVerifier
        {
            private static bool _validated;

            public static void Validate()
            {
                if (_validated)
                    return;

                AssertSize<ActiveEquipmentDTO>(32);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                AssertOffset<ActiveEquipmentDTO>(nameof(ActiveEquipmentDTO.ToolHashID), 0);
                AssertOffset<ActiveEquipmentDTO>(nameof(ActiveEquipmentDTO.CurrentBattery), 4);
                AssertOffset<ActiveEquipmentDTO>(nameof(ActiveEquipmentDTO.ThermalLoad), 8);
                AssertOffset<ActiveEquipmentDTO>(nameof(ActiveEquipmentDTO.StateFlags), 12);
                AssertOffset<ActiveEquipmentDTO>(nameof(ActiveEquipmentDTO.PowerDrawRate), 16);
                AssertOffset<ActiveEquipmentDTO>(nameof(ActiveEquipmentDTO.HeatGenerationRate), 20);
                AssertOffset<ActiveEquipmentDTO>(nameof(ActiveEquipmentDTO._pad0), 24);
                AssertOffset<ActiveEquipmentDTO>(nameof(ActiveEquipmentDTO._pad1), 28);
#endif
                AssertSize<EquipmentGridLoadRequest>(16);
                AssertSize<EquipmentIntegrationCounters>(64);
                AssertSize<EquipmentTelemetryEntry>(64);
                AssertSize<FlashlightTelemetryEntry>(64);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                AssertOffset<EquipmentTelemetryEntry>(nameof(EquipmentTelemetryEntry.Frame), 0);
                AssertOffset<EquipmentTelemetryEntry>(nameof(EquipmentTelemetryEntry.TickIndex), 4);
                AssertOffset<EquipmentTelemetryEntry>(nameof(EquipmentTelemetryEntry.BatteryDrainWattSeconds), 8);
                AssertOffset<EquipmentTelemetryEntry>(nameof(EquipmentTelemetryEntry.GridDrawWattSeconds), 12);
                AssertOffset<EquipmentTelemetryEntry>(nameof(EquipmentTelemetryEntry.PeakThermal01), 16);
                AssertOffset<EquipmentTelemetryEntry>(nameof(EquipmentTelemetryEntry.ActiveToolMask), 20);
                AssertOffset<EquipmentTelemetryEntry>(nameof(EquipmentTelemetryEntry.SignalCount), 24);
                AssertOffset<EquipmentTelemetryEntry>(nameof(EquipmentTelemetryEntry.FaultFlags), 28);
                AssertOffset<EquipmentTelemetryEntry>(nameof(EquipmentTelemetryEntry.LastFaultToolHashID), 32);
                AssertOffset<EquipmentTelemetryEntry>(nameof(EquipmentTelemetryEntry.CpuMicroseconds), 36);
                AssertOffset<EquipmentTelemetryEntry>(nameof(EquipmentTelemetryEntry.GlobalQualityWeight), 40);
                AssertOffset<EquipmentTelemetryEntry>(nameof(EquipmentTelemetryEntry.TickIntervalSeconds), 44);
                AssertOffset<EquipmentTelemetryEntry>(nameof(EquipmentTelemetryEntry.ThermalGridVersion), 48);
                AssertOffset<EquipmentTelemetryEntry>(nameof(EquipmentTelemetryEntry.ThermalGridCellCount), 52);
                AssertOffset<EquipmentTelemetryEntry>(nameof(EquipmentTelemetryEntry.SnapshotHash), 56);
                AssertOffset<EquipmentTelemetryEntry>(nameof(EquipmentTelemetryEntry.WearDrainNormalized), 60);
                AssertOffset<FlashlightTelemetryEntry>(nameof(FlashlightTelemetryEntry.Frame), 0);
                AssertOffset<FlashlightTelemetryEntry>(nameof(FlashlightTelemetryEntry.ToolHashID), 4);
                AssertOffset<FlashlightTelemetryEntry>(nameof(FlashlightTelemetryEntry.Battery01), 8);
                AssertOffset<FlashlightTelemetryEntry>(nameof(FlashlightTelemetryEntry.Thermal01), 12);
                AssertOffset<FlashlightTelemetryEntry>(nameof(FlashlightTelemetryEntry.DepthMeters), 16);
                AssertOffset<FlashlightTelemetryEntry>(nameof(FlashlightTelemetryEntry.AmbientCelsius), 20);
                AssertOffset<FlashlightTelemetryEntry>(nameof(FlashlightTelemetryEntry.BatteryDrainWattSeconds), 24);
                AssertOffset<FlashlightTelemetryEntry>(nameof(FlashlightTelemetryEntry.PeakThermal01), 28);
                AssertOffset<FlashlightTelemetryEntry>(nameof(FlashlightTelemetryEntry.CpuMicroseconds), 32);
                AssertOffset<FlashlightTelemetryEntry>(nameof(FlashlightTelemetryEntry.GlobalQualityWeight), 36);
                AssertOffset<FlashlightTelemetryEntry>(nameof(FlashlightTelemetryEntry.TickIntervalSeconds), 40);
                AssertOffset<FlashlightTelemetryEntry>(nameof(FlashlightTelemetryEntry.StateFlags), 44);
                AssertOffset<FlashlightTelemetryEntry>(nameof(FlashlightTelemetryEntry.FaultFlags), 48);
                AssertOffset<FlashlightTelemetryEntry>(nameof(FlashlightTelemetryEntry.SnapshotHash), 52);
                AssertOffset<FlashlightTelemetryEntry>(nameof(FlashlightTelemetryEntry.SignalCount), 56);
                AssertOffset<FlashlightTelemetryEntry>(nameof(FlashlightTelemetryEntry.WearDrainNormalized), 60);
#endif
                AssertSize<EquipmentOverheatSignal>(32);
                AssertSize<ToolDepletedSignal>(32);
                AssertSize<EquipmentTuningDTO>(32);
                AssertSize<EquipmentHardwareSpecDTO>(32);
                AssertSize<ToolState>(32);
                AssertSize<ToolRuntimeStats>(40);
                _validated = true;
            }

            private static void AssertSize<T>(int expected)
                where T : unmanaged
            {
                int observed = UnsafeUtility.SizeOf<T>();
                if (observed != expected)
                    throw new InvalidOperationException($"[1416] Layout size mismatch for {typeof(T).Name}: {observed} != {expected}");
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            private static void AssertOffset<T>(string fieldName, int expected)
                where T : unmanaged
            {
                var fieldInfo = typeof(T).GetField(fieldName);
                if (fieldInfo == null)
                    throw new InvalidOperationException($"[1416] Layout field missing for {typeof(T).Name}.{fieldName}");
                int observed = (int)UnsafeUtility.GetFieldOffset(fieldInfo);
                if (observed != expected)
                    throw new InvalidOperationException($"[1416] Layout offset mismatch for {typeof(T).Name}.{fieldName}: {observed} != {expected}");
            }
#endif
        }

        private static float EstimateOverchargeHeatGrowth(float internalHeat)
        {
            float x = math.min(OverchargeHeatGrowthInputMax, math.max(0f, internalHeat) * OverchargeHeatExponent);
            float x2 = x * x;
            float numerator = 1f + (0.5f * x) + (0.083333336f * x2);
            float denominator = math.max(0.125f, 1f - (0.5f * x) + (0.083333336f * x2));
            return numerator * math.rcp(denominator);
        }

    }
}
