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
    public sealed class ModularEquipmentEngine : MonoBehaviour, IModularEquipmentService, IUpdatable, ILateFrameTickable, IServiceHeartbeat, IServiceShutdown, IGlobalRegistryHotSwapListener, IGlobalRegistryHotSwapRefListener
    {
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
        private const float ToolSignalLowTierFloatDelta = 0.02f;
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
        private const uint EquipmentFaultNonFinite = 1u << 0;
        private const uint EquipmentFaultThermalGridInvalid = 1u << 1;
        private const uint EquipmentFaultCsvOverflow = 1u << 2;
        private const uint EquipmentOverheatLaneHash = 0xE1480A01u;
        private const uint ToolDepletedLaneHash = 0xE1480A02u;
        private const uint EquipmentMockBaseToolHash = 0x53483148u;
        private const string EquipmentFaultDumpPath = "Docs/AgentLogs/Dump_SHINOBU_224.bin";

        // COLD ALLOC: PlayerTool[16] — managed owner mirror for native tool slots — owner: ModularEquipmentEngine
        private readonly PlayerTool[] _toolOwners = new PlayerTool[MaxTrackedTools];
        // COLD ALLOC: bool[16] — slot occupancy flags for native tool slots — owner: ModularEquipmentEngine
        private readonly bool[] _slotUsed = new bool[MaxTrackedTools];
        // COLD ALLOC: ToolModuleData[64] — authored module mirrors copied into runtime slots — owner: ModularEquipmentEngine
        private readonly ToolModuleData[] _moduleSlots = new ToolModuleData[MaxTrackedTools * ToolUpgradeSystem.MaxModuleSlots];
        // COLD ALLOC: ToolModuleData[4] — cold-path scratch buffer for one tool registration — owner: ModularEquipmentEngine
        private readonly ToolModuleData[] _registrationModules = new ToolModuleData[ToolUpgradeSystem.MaxModuleSlots];

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
        private VaultGenerationHandle<EquipmentIntegrationCounters> _equipmentIntegrationCountersHandle;
        private VaultGenerationHandle<EquipmentTuningDTO> _equipmentTuningHandle;
        private VaultGenerationHandle<EquipmentHardwareSpecDTO> _equipmentHardwareSpecsHandle;
        private bool _isInitialized;
        private bool _registeredService;
        private bool _registeredUpdatable;
        private bool _registeredLateFrame;
        private bool _registeredHotSwap;
        private bool _equipmentSignalLanesReady;
        private bool _equipmentIntegrationScheduled;
        private bool _equipmentFaultDumped;
        private uint _lastPublishedEquippedMask;
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
        private float _lastGlobalQualityWeight = 1f;
        private float _thermalGridCellSizeMeters = EquipmentDefaultCellSizeMeters;
        private double3 _thermalGridRootAup;
        private IDataVault _dataVault;
        private IThermodynamicsService _thermodynamicsService;
        private IPowerGridService _powerGridService;
        private ToolDurabilitySystem _toolDurabilityService;
        private IPlayerRuntimeContext _playerRuntimeContext;
        private ISubmarineRuntimeContext _submarineRuntimeContext;
        private HectonQualityTier _cachedScalabilityTier = HectonQualityTier.Unknown;
        private JobHandle _equipmentIntegrationHandle;

        private struct EquipmentVaultViews
        {
            public NativeArray<ToolState> ToolStates;
            public NativeArray<ToolRuntimeStats> ToolStats;
            public NativeArray<byte> ToolTypes;
            public NativeArray<float> CurrentHeat;
            public NativeArray<float> BatteryCharge;
            public NativeArray<uint> StatusMasks;
            public NativeArray<float> EnvironmentHeat01;
            public NativeArray<ActiveEquipmentDTO> ActiveEquipmentStates;
            public NativeArray<ActiveEquipmentDTO> PublishedActiveEquipmentStates;
            public NativeArray<double3> ActiveEquipmentAupSamples;
            public NativeArray<EquipmentGridLoadRequest> ActiveEquipmentGridLoadRequests;
            public NativeArray<float> ActiveEquipmentWearDrainRates;
            public NativeArray<EquipmentTelemetryEntry> EquipmentTelemetryRing;
            public NativeArray<int> EquipmentTelemetryCursor;
            public NativeArray<EquipmentIntegrationCounters> EquipmentIntegrationCounters;
            public NativeArray<EquipmentTuningDTO> EquipmentTuning;
            public NativeArray<EquipmentHardwareSpecDTO> EquipmentHardwareSpecs;
        }

        public bool IsInitialized => _isInitialized;
        public ServiceHeartbeatState HeartbeatState => IsServiceReady ? ServiceHeartbeatState.Ready : ServiceHeartbeatState.NotStarted;
        public bool IsServiceReady =>
            _isInitialized &&
            _registeredService &&
            ReferenceEquals(GlobalRegistry.ModularEquipment, this) &&
            AreEquipmentBuffersReady() &&
            _equipmentSignalLanesReady;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
        }

        public static ModularEquipmentEngine EnsureRuntimeInstance()
        {
            IModularEquipmentService registered = GlobalRegistry.ModularEquipment;
            if (registered is ModularEquipmentEngine runtime)
                return runtime;
            if (registered != null)
                return null;

            GameObject runtimeRoot = new GameObject("[ModularEquipmentEngine]"); // COLD ALLOC: GameObject[1] — bootstrap-owned equipment runtime root — owner: ModularEquipmentEngine
            ModularEquipmentEngine engine = runtimeRoot.AddComponent<ModularEquipmentEngine>();
            engine.InitializeService();
            return engine;
        }

        public void InitializeService()
        {
            CacheRegistryDependenciesCold();
            TryRegisterHotSwap();

            if (_isInitialized)
            {
                TryRegisterService();
                TryRegisterUpdatable();
                TryRegisterLateFrame();
                return;
            }

            if (!CanOwnServiceSlot())
                return;

            InitializeActiveEquipmentNativeState();
            if (!TryResolveEquipmentViews(out EquipmentVaultViews views))
                return;

            ClearNativeArray(views.ToolStates);
            ClearNativeArray(views.ToolStats);
            ClearNativeArray(views.ToolTypes);
            ClearNativeArray(views.CurrentHeat);
            ClearNativeArray(views.BatteryCharge);
            ClearNativeArray(views.StatusMasks);
            ClearNativeArray(views.EnvironmentHeat01);

            if (Application.isPlaying && transform.parent != null)
                transform.SetParent(null, true);

            _isInitialized = true;
            TryRegisterService();
            TryRegisterUpdatable();
            TryRegisterLateFrame();
        }

        private bool AreEquipmentBuffersReady()
        {
            return TryResolveEquipmentViews(out _);
        }

        public void Tick(float deltaTime)
        {
            if (!_isInitialized)
                return;

            if (_equipmentIntegrationScheduled)
                return;

            float safeDeltaTime = math.max(0f, deltaTime);
            RefreshWirelessBrownoutFromPowerSnapshot();
            if (_wirelessBrownoutActive)
                _brownoutPulseTime += safeDeltaTime;

            _lastGlobalQualityWeight = ResolveGlobalQualityWeight();
            _lastEquipmentTickInterval = ResolveEquipmentTickInterval(_lastGlobalQualityWeight);
            _equipmentCadenceAccumulator += safeDeltaTime;

            if (_equipmentCadenceAccumulator < _lastEquipmentTickInterval)
                return;

            if (!TryResolveEquipmentViews(out EquipmentVaultViews views))
                return;

            RefreshInactiveDurabilityMirrors(ref views);

            float integrationDelta = _equipmentCadenceAccumulator;
            _equipmentCadenceAccumulator = 0f;
            RefreshThermalGridReadback(out NativeArray<float> thermalGridReadback);
            RefreshActiveEquipmentInputs(ref views);
            ScheduleActiveEquipmentIntegration(integrationDelta, ref views, thermalGridReadback);
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

            int slotIndex = ResolveOrAllocateSlot(profile.ToolId);
            if (slotIndex < 0)
                return 0u;
            if (!TryResolveEquipmentViews(out EquipmentVaultViews views))
                return 0u;

            int moduleSlotCount = tool.CopyAuthoredModules(_registrationModules);
            uint upgradeMask;
            ToolRuntimeStats compiledStats = ToolUpgradeSystem.CompileRuntimeStats(
                profile,
                _registrationModules,
                Mathf.Min(moduleSlotCount, (int)profile.ModuleSlotCount),
                out upgradeMask);

            ToolState nextState = views.ToolStates[slotIndex];
            nextState.CurrentBattery = math.saturate(tool.ResolveModularBatteryNormalized()) * math.max(0.1f, compiledStats.BatteryCapacity);
            nextState.InternalHeat = math.max(0f, tool.ResolveModularHeatNormalized());
            nextState.Durability = math.saturate(tool.DurabilityNormalized);
            nextState.UpgradeBitmask = upgradeMask;
            nextState.StatusMask = ResolveStatusMask(0u, in nextState, in compiledStats, ResolveDepthMeters(), false);
            nextState.ToolTypeId = ResolveToolTypeId(profile.ToolId);
            nextState.ModuleSlotCount = (byte)math.clamp(moduleSlotCount, 0, ToolUpgradeSystem.MaxModuleSlots);
            nextState.Reserved0 = 0;
            nextState.Reserved1 = 0ul;

            _toolOwners[slotIndex] = tool;
            _slotUsed[slotIndex] = true;
            views.ToolStates[slotIndex] = nextState;
            views.ToolStats[slotIndex] = compiledStats;
            WriteActiveEquipmentWearRate(ref views, slotIndex, tool, in compiledStats, requestedActive: false);
            WriteModuleMirror(slotIndex, _registrationModules, moduleSlotCount);
            SetBatteryAbsolute(ref views, slotIndex, nextState.CurrentBattery);
            WriteActiveEquipmentSlot(ref views, slotIndex, in nextState, in compiledStats);
            WriteSlotMirrors(ref views, slotIndex, in nextState);
            RegisterDurabilityMirror(tool);
            return profile.ToolId;
        }

        public void UnregisterTool(PlayerTool tool, uint toolId)
        {
            if (!_isInitialized || tool == null || toolId == 0u)
                return;

            if (!TryResolveSlot(toolId, out int slotIndex))
                return;
            if (!TryResolveEquipmentViews(out EquipmentVaultViews views))
                return;

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
            ClearModuleMirror(slotIndex);
        }

        public bool TryGetToolState(uint toolId, out ToolState state)
        {
            state = default;
            if (!_isInitialized || !TryResolveSlot(toolId, out int slotIndex))
                return false;
            if (!TryResolveEquipmentViews(out EquipmentVaultViews views))
                return false;

            state = views.ToolStates[slotIndex];
            state.CurrentBattery = ReadBatteryAbsolute(ref views, slotIndex);
            return true;
        }

        public bool TryGetToolStats(uint toolId, out ToolRuntimeStats stats)
        {
            stats = default;
            if (!_isInitialized || !TryResolveSlot(toolId, out int slotIndex))
                return false;
            if (!TryResolveEquipmentViews(out EquipmentVaultViews views))
                return false;

            stats = views.ToolStats[slotIndex];
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
            int baseIndex = slotIndex * ToolUpgradeSystem.MaxModuleSlots;
            ReadModuleMirror(slotIndex, slotCount, _registrationModules);
            if (!ToolUpgradeSystem.TryInsertModule(_registrationModules, slotCount, module))
                return false;

            WriteModuleMirror(slotIndex, _registrationModules, slotCount);
            RebuildCompiledState(slotIndex, owner, toolId);
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
            ReadModuleMirror(slotIndex, slotCount, _registrationModules);
            if (!ToolUpgradeSystem.TryRemoveModule(_registrationModules, slotCount, moduleId))
                return false;

            WriteModuleMirror(slotIndex, _registrationModules, slotCount);
            RebuildCompiledState(slotIndex, owner, toolId);
            return true;
        }

        public bool HasUpgrade(uint toolId, ToolUpgradeBits flag)
        {
            if (!TryGetToolState(toolId, out ToolState state))
                return false;

            return (state.UpgradeBitmask & (uint)flag) != 0u;
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

            return (state.UpgradeBitmask & (uint)ToolUpgradeBits.WirelessCharging) != 0u
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
            if (!TryResolveEquipmentViews(out EquipmentVaultViews views))
                return;

            float capacity = math.max(0.1f, views.ToolStats[slotIndex].BatteryCapacity);
            SetBatteryAbsolute(ref views, slotIndex, math.saturate(normalizedBattery) * capacity);
        }

        public float GetBatteryNormalized(uint toolId, float fallback)
        {
            if (!_isInitialized || !TryResolveSlot(toolId, out int slotIndex))
                return fallback;
            if (!TryResolveEquipmentViews(out EquipmentVaultViews views))
                return fallback;

            float capacity = math.max(0.1f, views.ToolStats[slotIndex].BatteryCapacity);
            return math.saturate(ReadBatteryAbsolute(ref views, slotIndex) / capacity);
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
            if (!TryResolveEquipmentViews(out EquipmentVaultViews views))
                return;

            ToolState state = views.ToolStates[slotIndex];
            float sanitizedHeat = math.max(0f, normalizedHeat);
            state.InternalHeat = IsOverchargeRequested(slotIndex)
                ? math.max(state.InternalHeat, sanitizedHeat)
                : sanitizedHeat;
            ToolRuntimeStats stats = views.ToolStats[slotIndex];
            state.StatusMask = ResolveStatusMask(
                state.StatusMask,
                in state,
                in stats,
                ResolveDepthMeters(),
                (state.StatusMask & ToolRuntimeStatusMasks.Active) != 0u);
            views.ToolStates[slotIndex] = state;
            WriteActiveEquipmentSlot(ref views, slotIndex, in state, in stats);
            WriteSlotMirrors(ref views, slotIndex, in state);
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
            if (!TryResolveEquipmentViews(out EquipmentVaultViews views))
                return;

            ToolRuntimeStats stats = views.ToolStats[slotIndex];
            if (active)
            {
                stats.BatteryDrainPerSecond = math.max(0f, math.isfinite(batteryDrainPerSecond)
                    ? batteryDrainPerSecond
                    : stats.BatteryDrainPerSecond);
                views.ToolStats[slotIndex] = stats;
            }

            MarkSlotActive(slotIndex, active);
            WriteActiveEquipmentSlot(ref views, slotIndex, in views.ToolStates[slotIndex], in stats);
        }

        public bool TryGetPublishedActiveEquipmentState(uint toolId, out ActiveEquipmentDTO state)
        {
            state = default;
            if (!_isInitialized ||
                !TryResolveEquipmentViews(out EquipmentVaultViews views) ||
                !TryResolveSlot(toolId, out int slotIndex) ||
                (uint)slotIndex >= (uint)views.PublishedActiveEquipmentStates.Length)
            {
                return false;
            }

            state = views.PublishedActiveEquipmentStates[slotIndex];
            return state.ToolHashID != 0u;
        }

        public bool TryGetActiveEquipmentSlot(int slotIndex, out ActiveEquipmentDTO state)
        {
            state = default;
            if (!_isInitialized ||
                !TryResolveEquipmentViews(out EquipmentVaultViews views) ||
                (uint)slotIndex >= (uint)views.PublishedActiveEquipmentStates.Length)
            {
                return false;
            }

            state = views.PublishedActiveEquipmentStates[slotIndex];
            return state.ToolHashID != 0u;
        }

        public bool TryGetLatestEquipmentTelemetry(out EquipmentTelemetryEntry entry)
        {
            entry = default;
            if (!TryResolveEquipmentViews(out EquipmentVaultViews views) ||
                views.EquipmentTelemetryRing.Length <= 0 ||
                views.EquipmentTelemetryCursor.Length <= 0)
            {
                return false;
            }

            int cursor = views.EquipmentTelemetryCursor[0] - 1;
            if (cursor < 0)
                cursor = views.EquipmentTelemetryRing.Length - 1;

            entry = views.EquipmentTelemetryRing[cursor];
            return entry.TickIndex != 0u || entry.Frame != 0u;
        }

        public bool TryGetEquipmentTelemetryEntry(int historyIndex, out EquipmentTelemetryEntry entry)
        {
            entry = default;
            if (!TryResolveEquipmentViews(out EquipmentVaultViews views) ||
                views.EquipmentTelemetryRing.Length <= 0 ||
                (uint)historyIndex >= (uint)views.EquipmentTelemetryRing.Length)
            {
                return false;
            }

            int cursor = views.EquipmentTelemetryCursor.IsCreated && views.EquipmentTelemetryCursor.Length > 0
                ? views.EquipmentTelemetryCursor[0]
                : 0;
            int index = cursor - 1 - historyIndex;
            while (index < 0)
                index += views.EquipmentTelemetryRing.Length;

            entry = views.EquipmentTelemetryRing[index];
            return entry.TickIndex != 0u || entry.Frame != 0u;
        }

        public bool TryGetEquipmentTuning(out EquipmentTuningDTO tuning)
        {
            tuning = default;
            if (!TryResolveEquipmentViews(out EquipmentVaultViews views) ||
                views.EquipmentTuning.Length <= 0)
                return false;

            tuning = views.EquipmentTuning[0];
            return true;
        }

        public void SetEquipmentTuning(in EquipmentTuningDTO tuning)
        {
            if (!TryResolveEquipmentViews(out EquipmentVaultViews views) ||
                views.EquipmentTuning.Length <= 0)
                return;

            views.EquipmentTuning[0] = SanitizeEquipmentTuning(tuning);
        }

        public bool SetEquipmentSlotRatesForEditor(int slotIndex, float powerDrawRate, float heatGenerationRate)
        {
            if (!TryResolveEquipmentViews(out EquipmentVaultViews views) ||
                (uint)slotIndex >= (uint)views.ActiveEquipmentStates.Length)
                return false;

            ActiveEquipmentDTO dto = views.ActiveEquipmentStates[slotIndex];
            if (dto.ToolHashID == 0u && !_slotUsed[slotIndex])
                return false;

            float safePower = math.max(0f, math.isfinite(powerDrawRate) ? powerDrawRate : dto.PowerDrawRate);
            float safeHeat = math.max(0f, math.isfinite(heatGenerationRate) ? heatGenerationRate : dto.HeatGenerationRate);
            dto.PowerDrawRate = safePower;
            dto.HeatGenerationRate = safeHeat;
            views.ActiveEquipmentStates[slotIndex] = dto;

            if (views.PublishedActiveEquipmentStates.IsCreated && (uint)slotIndex < (uint)views.PublishedActiveEquipmentStates.Length)
                views.PublishedActiveEquipmentStates[slotIndex] = dto;

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
                !TryResolveEquipmentViews(out EquipmentVaultViews views))
            {
                return;
            }

            if (!TryResolvePlayerEquipmentAup(out double3 rootAup))
                rootAup = double3.zero;
            GenerateMockEquipmentStateJob job = new GenerateMockEquipmentStateJob
            {
                Equipment = (ActiveEquipmentDTO*)views.ActiveEquipmentStates.GetUnsafePtr(),
                ToolAups = (double3*)views.ActiveEquipmentAupSamples.GetUnsafePtr(),
                ToolCount = math.min(5, views.ActiveEquipmentStates.Length),
                RootAup = rootAup,
                BaseToolHash = EquipmentMockBaseToolHash
            };
            job.Run(MaxTrackedTools);

            PublishActiveEquipmentReadback(ref views);
        }

        public void SetDurability(uint toolId, float normalizedDurability)
        {
            if (!_isInitialized || !TryResolveSlot(toolId, out int slotIndex))
                return;
            if (!TryResolveEquipmentViews(out EquipmentVaultViews views))
                return;

            ToolState state = views.ToolStates[slotIndex];
            state.Durability = math.saturate(normalizedDurability);
            ToolRuntimeStats stats = views.ToolStats[slotIndex];
            state.StatusMask = ResolveStatusMask(
                state.StatusMask,
                in state,
                in stats,
                ResolveDepthMeters(),
                (state.StatusMask & ToolRuntimeStatusMasks.Active) != 0u);
            views.ToolStates[slotIndex] = state;
            WriteActiveEquipmentSlot(ref views, slotIndex, in state, in stats);
            WriteSlotMirrors(ref views, slotIndex, in state);
            SyncDurabilityMirror(slotIndex, in state);
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (Selection.activeGameObject != gameObject ||
                !TryResolveEquipmentViews(out EquipmentVaultViews views))
            {
                return;
            }

            int count = math.min(MaxTrackedTools, views.PublishedActiveEquipmentStates.Length);
            Vector3 root = transform.position;
            for (int i = 0; i < count; i++)
            {
                ActiveEquipmentDTO state = views.PublishedActiveEquipmentStates[i];
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
            CacheRegistryDependenciesCold();
            TryRegisterHotSwap();
            SceneManager.sceneUnloaded += HandleSceneUnloaded;

            if (_isInitialized)
            {
                TryRegisterService();
                TryRegisterUpdatable();
                TryRegisterLateFrame();
            }
        }

        private void OnDisable()
        {
            SceneManager.sceneUnloaded -= HandleSceneUnloaded;
            TryUnregisterHotSwap();
            TryUnregisterService();
            TryUnregisterUpdatable();
            TryUnregisterLateFrame();
        }

        private void OnDestroy()
        {
            ShutdownServiceState();
        }

        public void OnServiceShutdown()
        {
            ShutdownServiceState();
        }

        private void ShutdownServiceState()
        {
            SceneManager.sceneUnloaded -= HandleSceneUnloaded;
            TryUnregisterHotSwap();
            TryUnregisterService();
            TryUnregisterUpdatable();
            TryUnregisterLateFrame();
            DisposeNativeState();
        }

        private bool CanOwnServiceSlot()
        {
            IModularEquipmentService registered = GlobalRegistry.ModularEquipment;
            if (registered != null && !ReferenceEquals(registered, this))
            {
                Destroy(gameObject);
                return false;
            }

            return true;
        }

        private void InitializeActiveEquipmentNativeState()
        {
            EquipmentLayoutVerifier.Validate();

            if (!TryResolveEquipmentViews(out EquipmentVaultViews views))
                return;

            ClearActiveEquipmentNativeState(ref views);
            InitializeEquipmentTuningBuffer(ref views);

            EnsureEquipmentSignalLanes();
        }

        private void EnsureEquipmentSignalLanes()
        {
            if (_equipmentSignalLanesReady)
                return;

            if (_dataVault == null)
                return;

            SignalBus<EquipmentOverheatSignal>.Configure(EquipmentSignalQueueCapacity, 128, 16, EquipmentOverheatLaneHash);
            SignalBus<ToolDepletedSignal>.Configure(EquipmentSignalQueueCapacity, 128, 16, ToolDepletedLaneHash);
            SignalBus<EquipmentOverheatSignal>.EnsureInitialized();
            SignalBus<ToolDepletedSignal>.EnsureInitialized();
            _equipmentSignalLanesReady = true;
        }

        private void InitializeEquipmentTuningBuffer(ref EquipmentVaultViews views)
        {
            if (!views.EquipmentTuning.IsCreated || views.EquipmentTuning.Length <= 0)
                return;

            views.EquipmentTuning[0] = EquipmentTuningDTO.CreateDefault(_lastGlobalQualityWeight);
        }

        private bool TryResolveEquipmentViews(out EquipmentVaultViews views)
        {
            views = default;
            IDataVault vault = _dataVault;
            if (vault == null)
                return false;

            return TryResolveOrAcquireEquipmentBuffer(vault, ref _toolStatesHandle, BufferID.ShinobuActiveEquipmentToolStates, MaxTrackedTools, NativeArrayOptions.UninitializedMemory, out views.ToolStates) &&
                   TryResolveOrAcquireEquipmentBuffer(vault, ref _toolStatsHandle, BufferID.ShinobuActiveEquipmentToolStats, MaxTrackedTools, NativeArrayOptions.UninitializedMemory, out views.ToolStats) &&
                   TryResolveOrAcquireEquipmentBuffer(vault, ref _toolTypesHandle, BufferID.ShinobuActiveEquipmentToolTypes, MaxTrackedTools, NativeArrayOptions.UninitializedMemory, out views.ToolTypes) &&
                   TryResolveOrAcquireEquipmentBuffer(vault, ref _currentHeatHandle, BufferID.ToolRuntimeHeat01, MaxTrackedTools, NativeArrayOptions.UninitializedMemory, out views.CurrentHeat) &&
                   TryResolveOrAcquireEquipmentBuffer(vault, ref _batteryChargeHandle, BufferID.ToolRuntimeBatteryCharge, MaxTrackedTools, NativeArrayOptions.UninitializedMemory, out views.BatteryCharge) &&
                   TryResolveOrAcquireEquipmentBuffer(vault, ref _statusMasksHandle, BufferID.ShinobuActiveEquipmentStatusMasks, MaxTrackedTools, NativeArrayOptions.UninitializedMemory, out views.StatusMasks) &&
                   TryResolveOrAcquireEquipmentBuffer(vault, ref _environmentHeat01Handle, BufferID.ShinobuActiveEquipmentEnvironmentHeat01, MaxTrackedTools, NativeArrayOptions.UninitializedMemory, out views.EnvironmentHeat01) &&
                   TryResolveOrAcquireEquipmentBuffer(vault, ref _activeEquipmentStatesHandle, BufferID.ShinobuActiveEquipmentState, MaxTrackedTools, NativeArrayOptions.UninitializedMemory, out views.ActiveEquipmentStates) &&
                   TryResolveOrAcquireEquipmentBuffer(vault, ref _publishedActiveEquipmentStatesHandle, BufferID.ShinobuActiveEquipmentPublishedState, MaxTrackedTools, NativeArrayOptions.UninitializedMemory, out views.PublishedActiveEquipmentStates) &&
                   TryResolveOrAcquireEquipmentBuffer(vault, ref _activeEquipmentAupSamplesHandle, BufferID.ShinobuActiveEquipmentAupSamples, MaxTrackedTools, NativeArrayOptions.UninitializedMemory, out views.ActiveEquipmentAupSamples) &&
                   TryResolveOrAcquireEquipmentBuffer(vault, ref _activeEquipmentGridLoadRequestsHandle, BufferID.ShinobuActiveEquipmentGridLoadRequests, MaxTrackedTools, NativeArrayOptions.UninitializedMemory, out views.ActiveEquipmentGridLoadRequests) &&
                   TryResolveOrAcquireEquipmentBuffer(vault, ref _activeEquipmentWearDrainRatesHandle, BufferID.ShinobuActiveEquipmentWearDrainRates, MaxTrackedTools, NativeArrayOptions.UninitializedMemory, out views.ActiveEquipmentWearDrainRates) &&
                   TryResolveOrAcquireEquipmentBuffer(vault, ref _equipmentTelemetryRingHandle, BufferID.ShinobuActiveEquipmentTelemetryRing, EquipmentTelemetryRingLength, NativeArrayOptions.UninitializedMemory, out views.EquipmentTelemetryRing) &&
                   TryResolveOrAcquireEquipmentBuffer(vault, ref _equipmentTelemetryCursorHandle, BufferID.ShinobuActiveEquipmentTelemetryCursor, 1, NativeArrayOptions.UninitializedMemory, out views.EquipmentTelemetryCursor) &&
                   TryResolveOrAcquireEquipmentBuffer(vault, ref _equipmentIntegrationCountersHandle, BufferID.ShinobuActiveEquipmentIntegrationCounters, MaxTrackedTools, NativeArrayOptions.UninitializedMemory, out views.EquipmentIntegrationCounters) &&
                   TryResolveOrAcquireEquipmentBuffer(vault, ref _equipmentTuningHandle, BufferID.ShinobuActiveEquipmentTuning, 1, NativeArrayOptions.UninitializedMemory, out views.EquipmentTuning) &&
                   TryResolveOrAcquireEquipmentBuffer(vault, ref _equipmentHardwareSpecsHandle, BufferID.ShinobuActiveEquipmentHardwareSpecs, EquipmentHardwareSpecCapacity, NativeArrayOptions.UninitializedMemory, out views.EquipmentHardwareSpecs);
        }

        private static bool TryResolveOrAcquireEquipmentBuffer<T>(
            IDataVault vault,
            ref VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength,
            NativeArrayOptions options,
            out NativeArray<T> buffer)
            where T : struct
        {
            if (TryResolveEquipmentBuffer(vault, in handle, requiredLength, out buffer))
                return true;

            buffer = default;
            if (vault == null || requiredLength <= 0)
            {
                handle = default;
                return false;
            }

            ReleaseEquipmentVaultHandle(vault, ref handle);
            handle = vault.GetGenerationHandle<T>(
                bufferId,
                requiredLength,
                SystemID.GameplayTools,
                options);

            return TryResolveEquipmentBuffer(vault, in handle, requiredLength, out buffer);
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

            UnsafeUtility.MemClear(array.GetUnsafePtr(), (long)array.Length * UnsafeUtility.SizeOf<T>());
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
                IntegrationCounters = views.EquipmentIntegrationCounters.IsCreated ? (EquipmentIntegrationCounters*)views.EquipmentIntegrationCounters.GetUnsafePtr() : null,
                HardwareSpecs = views.EquipmentHardwareSpecs.IsCreated ? (EquipmentHardwareSpecDTO*)views.EquipmentHardwareSpecs.GetUnsafePtr() : null,
                ActiveLength = GetCreatedLength(views.ActiveEquipmentStates),
                PublishedLength = GetCreatedLength(views.PublishedActiveEquipmentStates),
                AupLength = GetCreatedLength(views.ActiveEquipmentAupSamples),
                GridLoadRequestLength = GetCreatedLength(views.ActiveEquipmentGridLoadRequests),
                WearDrainLength = GetCreatedLength(views.ActiveEquipmentWearDrainRates),
                TelemetryLength = GetCreatedLength(views.EquipmentTelemetryRing),
                CursorLength = GetCreatedLength(views.EquipmentTelemetryCursor),
                CounterLength = GetCreatedLength(views.EquipmentIntegrationCounters),
                HardwareSpecLength = GetCreatedLength(views.EquipmentHardwareSpecs)
            };
            job.Run(jobLength);
        }

        private static int GetCreatedLength<T>(NativeArray<T> array)
            where T : unmanaged
        {
            return array.IsCreated ? array.Length : 0;
        }

        private int ResolveOrAllocateSlot(uint toolId)
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

            if (!TryResolveEquipmentViews(out EquipmentVaultViews views) ||
                !views.ActiveEquipmentStates.IsCreated)
                return false;

            for (int i = 0; i < MaxTrackedTools; i++)
            {
                if (_slotUsed[i] &&
                    (uint)i < (uint)views.ActiveEquipmentStates.Length &&
                    views.ActiveEquipmentStates[i].ToolHashID == toolId)
                {
                    slotIndex = i;
                    return true;
                }
            }

            return false;
        }

        private void WriteModuleMirror(int slotIndex, ToolModuleData[] modules, int moduleSlotCount)
        {
            int baseIndex = slotIndex * ToolUpgradeSystem.MaxModuleSlots;
            for (int i = 0; i < ToolUpgradeSystem.MaxModuleSlots; i++)
                _moduleSlots[baseIndex + i] = i < moduleSlotCount ? modules[i] : null;
        }

        private void ReadModuleMirror(int slotIndex, int moduleSlotCount, ToolModuleData[] destination)
        {
            int baseIndex = slotIndex * ToolUpgradeSystem.MaxModuleSlots;
            for (int i = 0; i < ToolUpgradeSystem.MaxModuleSlots; i++)
                destination[i] = i < moduleSlotCount ? _moduleSlots[baseIndex + i] : null;
        }

        private void ClearModuleMirror(int slotIndex)
        {
            int baseIndex = slotIndex * ToolUpgradeSystem.MaxModuleSlots;
            for (int i = 0; i < ToolUpgradeSystem.MaxModuleSlots; i++)
                _moduleSlots[baseIndex + i] = null;
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
            state.StatusMask = ResolveStatusMask(
                state.StatusMask,
                in state,
                in stats,
                ResolveDepthMeters(),
                (state.StatusMask & ToolRuntimeStatusMasks.Active) != 0u);
            views.ToolStates[slotIndex] = state;
            WriteSlotMirrors(ref views, slotIndex, in state);
        }

        private void RebuildCompiledState(int slotIndex, PlayerTool owner, uint toolId)
        {
            if (owner == null)
                return;
            if (!TryResolveEquipmentViews(out EquipmentVaultViews views))
                return;

            ToolRuntimeProfile profile = owner.BuildModularRuntimeProfile();
            int slotCount = Mathf.Min(GetConfiguredSlotCount(owner), (int)profile.ModuleSlotCount);
            ReadModuleMirror(slotIndex, slotCount, _registrationModules);

            float normalizedBattery = GetBatteryNormalized(toolId, owner.ResolveModularBatteryNormalized());
            ToolState state = views.ToolStates[slotIndex];
            state.CurrentBattery = math.saturate(normalizedBattery);

            uint upgradeMask;
            ToolRuntimeStats compiledStats = ToolUpgradeSystem.CompileRuntimeStats(profile, _registrationModules, slotCount, out upgradeMask);
            state.CurrentBattery *= math.max(0.1f, compiledStats.BatteryCapacity);
            state.UpgradeBitmask = upgradeMask;
            state.ToolTypeId = ResolveToolTypeId(profile.ToolId);
            state.ModuleSlotCount = (byte)math.clamp(slotCount, 0, ToolUpgradeSystem.MaxModuleSlots);
            state.StatusMask = ResolveStatusMask(
                state.StatusMask,
                in state,
                in compiledStats,
                ResolveDepthMeters(),
                (state.StatusMask & ToolRuntimeStatusMasks.Active) != 0u);
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
                _pad1 = 0,
                _pad2 = 0,
                _pad3 = 0,
                _pad4 = 0,
                _pad5 = 0,
                _pad6 = 0,
                _pad7 = 0
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
                ToolDurabilitySystem durability = _toolDurabilityService;
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

            ToolDurabilitySystem durability = _toolDurabilityService;
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

            ToolDurabilitySystem durability = _toolDurabilityService;
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

        private void RefreshThermalGridReadback(out NativeArray<float> thermalGridReadback)
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
                    out NativeArray<float> grid,
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
            _thermalGridRootAup = HectonFloatingOrigin.ToAbsoluteUniversePositionDouble3(originWS);
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
                    (state.UpgradeBitmask & (uint)ToolUpgradeBits.WirelessCharging) != 0u;
                state.Durability = !requestedActive && owner != null
                    ? math.saturate(owner.DurabilityNormalized)
                    : math.saturate(math.isfinite(state.Durability) ? state.Durability : 0f);
                state.StatusMask = ResolveStatusMask(state.StatusMask, in state, in stats, depthMeters, requestedActive, gridPowered);
                bool active = requestedActive && (state.StatusMask & ToolRuntimeStatusMasks.Disabled) == 0u;
                if (active)
                    _lastTelemetryActiveMask |= slotBit;

                state.StatusMask = ResolveStatusMask(state.StatusMask, in state, in stats, depthMeters, active, gridPowered);
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
                    _pad1 = 0,
                    _pad2 = 0,
                    _pad3 = 0,
                    _pad4 = 0,
                    _pad5 = 0,
                    _pad6 = 0,
                    _pad7 = 0
                };

                views.ActiveEquipmentAupSamples[i] = TryResolveToolAup(owner, hasPlayerAup, in playerAup, out double3 toolAup)
                    ? toolAup
                    : (hasPlayerAup ? playerAup : double3.zero);
            }
        }

        public EquipmentCsvParseResult IngestToolHardwareSpecsCsv(ReadOnlySpan<byte> csv)
        {
            if (!TryResolveEquipmentViews(out EquipmentVaultViews views))
            {
                return new EquipmentCsvParseResult
                {
                    ParsedRows = 0,
                    SkippedRows = 0,
                    LastToolHashID = 0u,
                    FaultFlags = EquipmentFaultCsvOverflow
                };
            }

            return EquipmentHardwareSpecsCsvParser.Parse(csv, views.EquipmentHardwareSpecs);
        }

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
            return _submarineRuntimeContext != null &&
                   _submarineRuntimeContext.AtmosphereSystem != null &&
                   _powerGridService != null;
        }

        private bool ResolveToolInWater()
        {
            IPlayerRuntimeContext playerContext = _playerRuntimeContext;
            HectonPlayerMovement movement = playerContext != null ? playerContext.PlayerMovement : null;
            return movement != null && movement.IsPlayerSubmerged;
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

            HectonPlayerMovement movement = playerContext.PlayerMovement;
            if (movement != null)
            {
                aup = movement.CurrentAup.ToAbsoluteDouble3();
                if (math.all(math.isfinite(aup)))
                    return true;
            }

            return false;
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
            if (!TryResolveEquipmentBuffer(vault, in _equipmentTuningHandle, 1, out NativeArray<EquipmentTuningDTO> tuningBuffer))
                return false;

            tuning = tuningBuffer[0];
            return true;
        }

        private unsafe void ScheduleActiveEquipmentIntegration(
            float deltaSeconds,
            ref EquipmentVaultViews views,
            NativeArray<float> thermalGridReadback)
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
                Tuning = tuning,
                FaultNonFiniteMask = EquipmentFaultNonFinite,
                FaultGridInvalidMask = EquipmentFaultThermalGridInvalid,
                OverheatWriter = SignalBus<EquipmentOverheatSignal>.ParallelWriter,
                DepletedWriter = SignalBus<ToolDepletedSignal>.ParallelWriter
            };

            _equipmentIntegrationHandle = job.Schedule(MaxTrackedTools, 4);
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
            return tuning;
        }

        private static float SanitizeTuningFloat(float value, float fallback)
        {
            return math.isfinite(value) ? value : fallback;
        }

        private unsafe void CompleteActiveEquipmentJob()
        {
            if (!_equipmentIntegrationScheduled)
                return;

            if (!_equipmentIntegrationHandle.IsCompleted)
                return;

            long startTicks = Stopwatch.GetTimestamp();
            Hecton8.Core.DispatcherJobFence.TryFinalizeCompleted(ref _equipmentIntegrationHandle);
            _equipmentIntegrationScheduled = false;
            if (!TryResolveEquipmentViews(out EquipmentVaultViews views))
                return;

            PublishActiveEquipmentReadback(ref views);
            ProcessGridLoadRequests(ref views);

            long endTicks = Stopwatch.GetTimestamp();
            float microseconds = (float)((endTicks - startTicks) * 1000000.0 / Stopwatch.Frequency);
            RecordEquipmentTelemetry(ref views, microseconds);
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
                state.StatusMask = ResolveStatusMask(
                    state.StatusMask,
                    in state,
                    in stats,
                    depthMeters,
                    active,
                    gridPowered);
                state.StatusMask = ResolveHeatWarningHaptic(state.StatusMask, state.InternalHeat);
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
                FaultFlags = counters.FaultFlags,
                LastFaultToolHashID = counters.LastFaultToolHashID,
                CpuMicroseconds = math.max(0f, cpuMicroseconds),
                GlobalQualityWeight = _lastGlobalQualityWeight,
                TickIntervalSeconds = _lastEquipmentTickInterval,
                ThermalGridVersion = _thermalGridVersion,
                ThermalGridCellCount = _thermalGridCellCount,
                SnapshotHash = ComputeActiveEquipmentSnapshotHash(ref views),
                WearDrainNormalized = counters.WearDrainNormalized
            };

            views.EquipmentTelemetryRing[index] = entry;
            views.EquipmentTelemetryCursor[0] = (index + 1) % views.EquipmentTelemetryRing.Length;
            if (entry.FaultFlags != 0u && !_equipmentFaultDumped)
                DumpEquipmentTelemetry(ref views);
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

        private unsafe void DumpEquipmentTelemetry(ref EquipmentVaultViews views)
        {
            _equipmentFaultDumped = true;
            if (!views.EquipmentTelemetryRing.IsCreated)
                return;

            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string dumpPath = Path.Combine(projectRoot, EquipmentFaultDumpPath);
            string directory = Path.GetDirectoryName(dumpPath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            using (FileStream stream = new FileStream(dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read))
            {
                uint header = 0x45515448u; // H8TE
                Span<byte> headerBytes = stackalloc byte[16];
                WriteUInt32LE(headerBytes, 0, header);
                WriteUInt32LE(headerBytes, 4, unchecked((uint)views.EquipmentTelemetryRing.Length));
                WriteUInt32LE(headerBytes, 8, unchecked((uint)UnsafeUtility.SizeOf<EquipmentTelemetryEntry>()));
                WriteUInt32LE(headerBytes, 12, _equipmentTickIndex);
                stream.Write(headerBytes);

                void* source = views.EquipmentTelemetryRing.GetUnsafeReadOnlyPtr();
                int byteLength = views.EquipmentTelemetryRing.Length * UnsafeUtility.SizeOf<EquipmentTelemetryEntry>();
                stream.Write(new ReadOnlySpan<byte>(source, byteLength));
            }
        }

        private static void WriteUInt32LE(Span<byte> buffer, int offset, uint value)
        {
            buffer[offset] = (byte)value;
            buffer[offset + 1] = (byte)(value >> 8);
            buffer[offset + 2] = (byte)(value >> 16);
            buffer[offset + 3] = (byte)(value >> 24);
        }

        private static uint ResolveStatusMask(uint currentStatus, in ToolState state, in ToolRuntimeStats stats, float depthMeters, bool active, bool gridPowered = false)
        {
            uint status = currentStatus & ToolRuntimeStatusMasks.HeatWarningHapticQueued;
            if (active)
                status |= ToolRuntimeStatusMasks.Active;

            if (!gridPowered && (state.CurrentBattery <= 0.0001f || stats.BatteryCapacity <= 0.0001f))
                status |= ToolRuntimeStatusMasks.LowPower;

            if (state.InternalHeat >= 1f ||
                ((currentStatus & ToolRuntimeStatusMasks.Overheated) != 0u && state.InternalHeat > OverheatRecoveryThreshold))
            {
                status |= ToolRuntimeStatusMasks.Overheated;
            }

            if (state.Durability <= 0.0001f)
                status |= ToolRuntimeStatusMasks.Broken;

            bool standardToolBelowLimit = depthMeters > StandardDepthFailureMeters &&
                (state.UpgradeBitmask & ((uint)ToolUpgradeBits.DepthHardened | (uint)ToolUpgradeBits.ThermalShield)) == 0u;
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

        private static uint ResolveHeatWarningHaptic(uint status, float heat)
        {
            if (heat >= HeatWarningThreshold && (status & ToolRuntimeStatusMasks.HeatWarningHapticQueued) == 0u)
            {
                ToolHapticsRuntime.EnqueueSinusoidalCommand(
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
            if (playerContext != null)
            {
                HectonSurvivalSystem survivalSystem = playerContext.SurvivalSystem;
                if (survivalSystem != null)
                    return math.max(0f, survivalSystem.Depth);

                HectonPlayerMovement movement = playerContext.PlayerMovement;
                if (movement != null)
                    return math.max(0f, movement.CurrentDepth);
            }

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

            HectonQualityTier qualityTier = _cachedScalabilityTier;
            bool lowTier = qualityTier == HectonQualityTier.Low ||
                qualityTier == HectonQualityTier.Mx350 ||
                qualityTier == HectonQualityTier.Unknown;

            byte flags = 0;
            if (equipped)
                flags |= ToolStateChangedSignal.FlagEquipped;
            if (visible)
                flags |= ToolStateChangedSignal.FlagVisible;
            if (lowTier)
                flags |= ToolStateChangedSignal.FlagLowTierFallback;

            ToolStateChangedSignal signal = new ToolStateChangedSignal
            {
                ToolHash = owner.RuntimeToolId,
                Frame = unchecked((uint)Time.frameCount),
                Battery01 = math.isfinite(battery01) ? battery01 : 0f,
                Heat01 = math.isfinite(heat01) ? heat01 : 0f,
                DistanceMeters = math.isfinite(distanceMeters) ? distanceMeters : 0f,
                Durability01 = math.isfinite(durability01) ? durability01 : 0f,
                StatusMask = statusMask,
                AmmoUnits = (ushort)ammoUnits,
                Flags = flags,
                ToolTypeId = state.ToolTypeId
            };

            if (!terminalHolster && !ShouldPublishToolStateChanged(in signal, qualityTier))
                return;

            GlobalSignals.Publish(signal);
            if (equipped)
                _lastPublishedEquippedMask |= slotBit;
            else
                _lastPublishedEquippedMask &= ~slotBit;
        }

        private static bool ShouldPublishToolStateChanged(in ToolStateChangedSignal signal, HectonQualityTier qualityTier)
        {
            if (!GlobalSignals.TryGetLatestToolStateChangedSignal(out ToolStateChangedSignal latest, out _))
                return true;

            if (latest.ToolHash != signal.ToolHash ||
                latest.Flags != signal.Flags ||
                latest.StatusMask != signal.StatusMask ||
                latest.AmmoUnits != signal.AmmoUnits ||
                latest.ToolTypeId != signal.ToolTypeId)
            {
                return true;
            }

            float floatDelta = ResolveToolSignalFloatDelta(qualityTier);
            return math.abs(latest.Battery01 - signal.Battery01) >= floatDelta ||
                math.abs(latest.Heat01 - signal.Heat01) >= floatDelta ||
                math.abs(latest.Durability01 - signal.Durability01) >= floatDelta ||
                math.abs(latest.DistanceMeters - signal.DistanceMeters) >= ToolSignalDistanceDeltaMeters;
        }

        private static float ResolveToolSignalFloatDelta(HectonQualityTier qualityTier)
        {
            switch (qualityTier)
            {
                case HectonQualityTier.Ultra:
                    return ToolSignalUltraTierFloatDelta;
                case HectonQualityTier.High:
                    return ToolSignalHighTierFloatDelta;
                case HectonQualityTier.Mid:
                    return ToolSignalMidTierFloatDelta;
                default:
                    return ToolSignalLowTierFloatDelta;
            }
        }

        private void CacheRegistryDependenciesCold()
        {
            _dataVault = GlobalRegistry.DataVault;
            _thermodynamicsService = GlobalRegistry.ThermodynamicsService;
            _powerGridService = GlobalRegistry.PowerGrid;
            _toolDurabilityService = GlobalRegistry.ToolDurability;
            _playerRuntimeContext = GlobalRegistry.Player;
            _submarineRuntimeContext = GlobalRegistry.Submarine;
            _cachedScalabilityTier = GlobalRegistry.ScalabilityTier;
        }

        private void ApplyRegistryServiceRebind(GlobalRegistryServiceSlot serviceSlot, object currentService)
        {
            switch (serviceSlot)
            {
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
                    _toolDurabilityService = currentService as ToolDurabilitySystem;
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

            if (_equipmentIntegrationScheduled)
            {
                DispatcherJobFence.TryComplete(ref _equipmentIntegrationHandle, forceComplete: true);
                _equipmentIntegrationScheduled = false;
            }

            ReleaseEquipmentVaultHandles(_dataVault);
            ClearEquipmentVaultHandles();
            _dataVault = nextVault;
            _isInitialized = false;
            _equipmentSignalLanesReady = false;
            _lastPublishedEquippedMask = 0u;
            _lastTelemetryActiveMask = 0u;
            _thermalGridCellCount = 0;

            if (nextVault == null || !CanOwnServiceSlot())
                return;

            InitializeActiveEquipmentNativeState();
            if (!TryResolveEquipmentViews(out EquipmentVaultViews views))
                return;

            ClearNativeArray(views.ToolStates);
            ClearNativeArray(views.ToolStats);
            ClearNativeArray(views.ToolTypes);
            ClearNativeArray(views.CurrentHeat);
            ClearNativeArray(views.BatteryCharge);
            ClearNativeArray(views.StatusMasks);
            ClearNativeArray(views.EnvironmentHeat01);

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

        private void TryRegisterService()
        {
            if (_registeredService)
                return;

            if (!CanOwnServiceSlot())
                return;

            GlobalRegistry.RegisterModularEquipmentService(this);
            _registeredService = ReferenceEquals(GlobalRegistry.ModularEquipment, this);
        }

        public bool TryGetWirelessBrownoutFeedback(uint toolId, out float flickerScalar)
        {
            flickerScalar = 0f;
            if (!_wirelessBrownoutActive || !_isInitialized || !TryResolveSlot(toolId, out int slotIndex))
                return false;
            if (!TryResolveEquipmentViews(out EquipmentVaultViews views))
                return false;

            if ((views.ToolStates[slotIndex].UpgradeBitmask & (uint)ToolUpgradeBits.WirelessCharging) == 0u)
                return false;

            float pulse = 0.35f + (0.65f * math.abs(FastTriangleSigned(_brownoutPulseTime * WirelessBrownoutPulseCycles)));
            flickerScalar = pulse;
            return true;
        }

        public bool TryGetToolBrownoutFeedback(uint toolId, out float flickerScalar)
        {
            flickerScalar = 0f;
            if (!_wirelessBrownoutActive || !_isInitialized || !TryResolveSlot(toolId, out int slotIndex))
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

            GlobalRegistry.RegisterUpdatable(this, PriorityLayer.Core);
            _registeredUpdatable = GlobalRegistry.Updatables.Contains(this);
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

            GlobalRegistry.RegisterLateFrameTickable(this, PriorityLayer.Core);
            _registeredLateFrame = SystemDispatcher.GetLateFrameLane(PriorityLayer.Core).Contains(this);
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
            ClearModuleMirror(slotIndex);
        }

        private void DisposeNativeState()
        {
            if (_equipmentIntegrationScheduled)
            {
                DispatcherJobFence.TryComplete(ref _equipmentIntegrationHandle, forceComplete: true);
                _equipmentIntegrationScheduled = false;
            }

            for (int i = 0; i < MaxTrackedTools; i++)
            {
                _toolOwners[i] = null;
                _slotUsed[i] = false;
            }

            for (int i = 0; i < _moduleSlots.Length; i++)
                _moduleSlots[i] = null;

            ReleaseEquipmentVaultHandles(_dataVault);
            ClearEquipmentVaultHandles();

            _isInitialized = false;
            _equipmentSignalLanesReady = false;
            _lastPublishedEquippedMask = 0u;
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

        private void ReleaseEquipmentVaultHandles(IDataVault vault)
        {
            if (vault == null)
                return;

            ReleaseEquipmentVaultHandle(vault, ref _toolStatesHandle);
            ReleaseEquipmentVaultHandle(vault, ref _toolStatsHandle);
            ReleaseEquipmentVaultHandle(vault, ref _toolTypesHandle);
            ReleaseEquipmentVaultHandle(vault, ref _currentHeatHandle);
            ReleaseEquipmentVaultHandle(vault, ref _batteryChargeHandle);
            ReleaseEquipmentVaultHandle(vault, ref _statusMasksHandle);
            ReleaseEquipmentVaultHandle(vault, ref _environmentHeat01Handle);
            ReleaseEquipmentVaultHandle(vault, ref _activeEquipmentStatesHandle);
            ReleaseEquipmentVaultHandle(vault, ref _publishedActiveEquipmentStatesHandle);
            ReleaseEquipmentVaultHandle(vault, ref _activeEquipmentAupSamplesHandle);
            ReleaseEquipmentVaultHandle(vault, ref _activeEquipmentGridLoadRequestsHandle);
            ReleaseEquipmentVaultHandle(vault, ref _activeEquipmentWearDrainRatesHandle);
            ReleaseEquipmentVaultHandle(vault, ref _equipmentTelemetryRingHandle);
            ReleaseEquipmentVaultHandle(vault, ref _equipmentTelemetryCursorHandle);
            ReleaseEquipmentVaultHandle(vault, ref _equipmentIntegrationCountersHandle);
            ReleaseEquipmentVaultHandle(vault, ref _equipmentTuningHandle);
            ReleaseEquipmentVaultHandle(vault, ref _equipmentHardwareSpecsHandle);
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
            _equipmentIntegrationCountersHandle = default;
            _equipmentTuningHandle = default;
            _equipmentHardwareSpecsHandle = default;
        }

        private static void ReleaseEquipmentVaultHandle<T>(IDataVault vault, ref VaultGenerationHandle<T> handle)
            where T : struct
        {
            if (!IsVaultGenerationHandleCreated(in handle))
                return;

            vault.ReleaseBuffer(in handle);
            handle = default;
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
            [NoAlias] [NativeDisableUnsafePtrRestriction] public EquipmentIntegrationCounters* IntegrationCounters;
            [NoAlias] [NativeDisableUnsafePtrRestriction] public EquipmentHardwareSpecDTO* HardwareSpecs;
            public int ActiveLength;
            public int PublishedLength;
            public int AupLength;
            public int GridLoadRequestLength;
            public int WearDrainLength;
            public int TelemetryLength;
            public int CursorLength;
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
                if (IntegrationCounters != null && (uint)index < (uint)CounterLength)
                    IntegrationCounters[index] = default;
                if (HardwareSpecs != null && (uint)index < (uint)HardwareSpecLength)
                    HardwareSpecs[index] = default;
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
        private unsafe struct GenerateMockEquipmentStateJob : IJobParallelFor
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
                Equipment[index] = new ActiveEquipmentDTO
                {
                    ToolHashID = BaseToolHash + (uint)index,
                    CurrentBattery = 95f - (rank * 7f),
                    ThermalLoad = 0.08f * rank,
                    StateFlags = ActiveEquipmentStateFlags.Active | ActiveEquipmentStateFlags.InWater,
                    PowerDrawRate = 6f + (rank * 2.5f),
                    HeatGenerationRate = 0.06f + (rank * 0.035f),
                    _pad0 = 0,
                    _pad1 = 0,
                    _pad2 = 0,
                    _pad3 = 0,
                    _pad4 = 0,
                    _pad5 = 0,
                    _pad6 = 0,
                    _pad7 = 0
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
            [NativeDisableContainerSafetyRestriction] public NativeQueue<EquipmentOverheatSignal>.ParallelWriter OverheatWriter;
            // SAFETY_JUSTIFICATION_PARAGRAPH_1:
            // SignalBus<T>.ParallelWriter safety is intentionally suppressed only for the depleted-tool signal lane; the queue is not snapshotted while the producer handle is live.
            // SAFETY_JUSTIFICATION_PARAGRAPH_2:
            // Rejected duplicating depletion state in a second NativeArray because it adds a write stream and stale cleanup. Rejected SignalBus writes on the main thread because it blocks readback.
            // SAFETY_JUSTIFICATION_PARAGRAPH_3:
            // Single equipment producer per frame, SignalBusRegistry snapshot consumer after dispatcher fencing; no other equipment job writes this lane in the same frame.
            [NativeDisableContainerSafetyRestriction] public NativeQueue<ToolDepletedSignal>.ParallelWriter DepletedWriter;
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
                float requestedEnergy = requestedActive ? drawRate * safeDelta : 0f;
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
                        battery = math.max(0f, battery - requestedEnergy);
                        counters.BatteryDrainWattSeconds += math.min(previousBattery, requestedEnergy);
                        if (previousBattery > 0.0001f && battery <= 0.0001f)
                        {
                            flags |= ActiveEquipmentStateFlags.Depleted;
                            if ((previousFlags & ActiveEquipmentStateFlags.Depleted) == 0u)
                            {
                                DepletedWriter.Enqueue(new ToolDepletedSignal
                                {
                                    ToolHashID = dto.ToolHashID,
                                    Frame = Frame,
                                    Battery01 = 0f,
                                    RequestedPower = drawRate,
                                    StateFlags = flags,
                                    GridPowered = 0,
                                    Reserved0 = 0,
                                    Reserved1 = 0,
                                    Reserved2 = 0ul
                                });
                                counters.SignalCount++;
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
                float generatedHeat = active ? heatRate * safeDelta : 0f;
                heat = math.max(0f, heat + generatedHeat + exchange);

                bool wasOverheated = (previousFlags & ActiveEquipmentStateFlags.Overheated) != 0u;
                if (heat >= 1f || (wasOverheated && heat > OverheatRecoveryThreshold))
                {
                    flags |= ActiveEquipmentStateFlags.Overheated;
                    flags &= ~ActiveEquipmentStateFlags.Active;
                    if (!wasOverheated)
                    {
                        OverheatWriter.Enqueue(new EquipmentOverheatSignal
                        {
                            ToolHashID = dto.ToolHashID,
                            Frame = Frame,
                            Heat01 = math.saturate(heat),
                            AmbientCelsius = ambientCelsius,
                            Severity01 = math.saturate((heat - 0.85f) * 6.666667f),
                            StateFlags = flags,
                            VisualOnly = 1,
                            Reserved0 = 0,
                            Reserved1 = 0,
                            Reserved2 = 0u
                        });
                        counters.SignalCount++;
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
                trilinearWeight = math.step(0.25f, quality) * trilinearWeight * trilinearWeight * (3f - (2f * trilinearWeight));
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
                int index = cell.x + (cell.y * ThermalWidth) + (cell.z * ThermalWidth * ThermalHeight);
                if ((uint)index < (uint)ThermalGridLength)
                {
                    float ambient = ThermalGrid[index];
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
#endif
                AssertSize<EquipmentGridLoadRequest>(16);
                AssertSize<EquipmentIntegrationCounters>(64);
                AssertSize<EquipmentTelemetryEntry>(64);
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
                    throw new InvalidOperationException($"[SHINOBU_224] Layout size mismatch for {typeof(T).Name}: {observed} != {expected}");
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            private static void AssertOffset<T>(string fieldName, int expected)
                where T : unmanaged
            {
                var fieldInfo = typeof(T).GetField(fieldName);
                if (fieldInfo == null)
                    throw new InvalidOperationException($"[SHINOBU_224] Layout field missing for {typeof(T).Name}.{fieldName}");
                int observed = (int)UnsafeUtility.GetFieldOffset(fieldInfo);
                if (observed != expected)
                    throw new InvalidOperationException($"[SHINOBU_224] Layout offset mismatch for {typeof(T).Name}.{fieldName}: {observed} != {expected}");
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
