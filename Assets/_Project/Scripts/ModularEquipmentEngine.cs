namespace Hecton8.Tools
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Runtime.InteropServices;
    using Unity.Burst;
    using Hecton8.Core;
    using Hecton8.Core.Memory;
    using Hecton8.Core.Contracts.Signals;
    using Hecton8.Gameplay;
    using Hecton8.Power;
    using Hecton8.World;
    using Unity.Collections;
    using Unity.Collections.LowLevel.Unsafe;
    using Unity.Jobs;
    using Unity.Mathematics;
    using UnityEngine;
    using UnityEngine.SceneManagement;

    /// <summary>
    /// Authoritative runtime owner for active handheld-tool state.
    /// Tool authoring remains in ScriptableObjects and components; hot paths read only native memory.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-9918)]
    public sealed class ModularEquipmentEngine : MonoBehaviour, IModularEquipmentService, IUpdatable, ILateFrameTickable, IPowerGridTelemetryListener, IServiceHeartbeat, IServiceShutdown
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
        private const float ActiveToolHeatWindowSeconds = 0.075f;
        private const float MinimumCoolingDenominator = 0.125f;
        private const float DepthCoolingScale = 0.0025f;
        private const float ThermalVentHeatScale = 0.35f;
        private const float ThermalProbeRadiusMeters = 1.25f;
        private const float StandardDepthFailureMeters = 500f;
        private const float HeatWarningThreshold = 0.90f;
        private const float HeatWarningResetThreshold = 0.85f;
        private const float OverheatRecoveryThreshold = 0.15f;
        private const int ThermalProbeFrameMask = 0x03;
        private const float ToolSignalLowTierFloatDelta = 0.02f;
        private const float ToolSignalMidTierFloatDelta = 0.01f;
        private const float ToolSignalHighTierFloatDelta = 0.005f;
        private const float ToolSignalUltraTierFloatDelta = 0.0025f;
        private const float ToolSignalDistanceDeltaMeters = 0.5f;
        private const int EquipmentTelemetryRingLength = 300;
        private const int EquipmentSignalQueueCapacity = 32;
        private const float EquipmentFallbackAmbientCelsius = 6f;
        private const float EquipmentDefaultCellSizeMeters = 1f;
        private const uint EquipmentFaultNonFinite = 1u << 0;
        private const uint EquipmentFaultThermalGridInvalid = 1u << 1;
        private const uint EquipmentOverheatLaneHash = 0xE1480A01u;
        private const uint ToolDepletedLaneHash = 0xE1480A02u;
        private const string EquipmentFaultDumpPath = "Docs/AgentLogs/Dump_SHINOBU_148.bin";

        // COLD ALLOC: PlayerTool[16] — managed owner mirror for native tool slots — owner: ModularEquipmentEngine
        private readonly PlayerTool[] _toolOwners = new PlayerTool[MaxTrackedTools];
        // COLD ALLOC: bool[16] — slot occupancy flags for native tool slots — owner: ModularEquipmentEngine
        private readonly bool[] _slotUsed = new bool[MaxTrackedTools];
        // COLD ALLOC: ToolModuleData[64] — authored module mirrors copied into runtime slots — owner: ModularEquipmentEngine
        private readonly ToolModuleData[] _moduleSlots = new ToolModuleData[MaxTrackedTools * ToolUpgradeSystem.MaxModuleSlots];
        // COLD ALLOC: ToolModuleData[4] — cold-path scratch buffer for one tool registration — owner: ModularEquipmentEngine
        private readonly ToolModuleData[] _registrationModules = new ToolModuleData[ToolUpgradeSystem.MaxModuleSlots];

        private NativeArray<ToolState> _toolStates;
        private NativeArray<ToolRuntimeStats> _toolStats;
        private NativeArray<byte> _toolTypes;
        private NativeArray<float> _currentHeat;
        private NativeArray<float> _batteryCharge;
        private NativeArray<uint> _statusMasks;
        private NativeArray<float> _environmentHeat01;
        private NativeArray<float> _batteryDrainRates;
        private NativeArray<float> _batteryDrainDeltaSeconds;
        private NativeArray<ActiveEquipmentDTO> _activeEquipmentStates;
        private NativeArray<ActiveEquipmentDTO> _publishedActiveEquipmentStates;
        private NativeArray<double3> _activeEquipmentAupSamples;
        private NativeArray<EquipmentGridLoadRequest> _activeEquipmentGridLoadRequests;
        private NativeArray<EquipmentTelemetryEntry> _equipmentTelemetryRing;
        private NativeArray<int> _equipmentTelemetryCursor;
        private NativeArray<EquipmentIntegrationCounters> _equipmentIntegrationCounters;
        private NativeHashMap<uint, int> _toolIndexById;
        private NativeQueue<EquipmentOverheatSignal> _equipmentOverheatSignals;
        private NativeQueue<ToolDepletedSignal> _toolDepletedSignals;
        private bool _currentHeatFromDataVault;
        private bool _batteryChargeFromDataVault;
        private bool _activeEquipmentStatesFromDataVault;
        private bool _publishedActiveEquipmentStatesFromDataVault;
        private bool _activeEquipmentAupSamplesFromDataVault;
        private bool _activeEquipmentGridLoadRequestsFromDataVault;
        private bool _equipmentTelemetryRingFromDataVault;
        private bool _equipmentTelemetryCursorFromDataVault;
        private bool _equipmentIntegrationCountersFromDataVault;
        private bool _isInitialized;
        private bool _registeredService;
        private bool _registeredUpdatable;
        private bool _registeredLateFrame;
        private bool _telemetrySubscribed;
        private bool _equipmentIntegrationScheduled;
        private bool _equipmentFaultDumped;
        private uint _pendingBatteryDrainMask;
        private uint _lastPublishedEquippedMask;
        private uint _externalActiveToolMask;
        private uint _lastTelemetryActiveMask;
        private int _thermalProbeFrameIndex;
        private int _thermalGridWidth;
        private int _thermalGridHeight;
        private int _thermalGridDepth;
        private int _thermalGridVersion;
        private uint _equipmentTickIndex;
        private float _latestSupplyRatio = 1f;
        private bool _wirelessBrownoutActive;
        private float _brownoutPulseTime;
        private float _equipmentCadenceAccumulator;
        private float _lastEquipmentTickInterval = 0.016f;
        private float _lastGlobalQualityWeight = 1f;
        private float _thermalGridCellSizeMeters = EquipmentDefaultCellSizeMeters;
        private double3 _thermalGridRootAup;
        private NativeArray<float> _thermalGridReadback;
        private JobHandle _equipmentIntegrationHandle;

        public bool IsInitialized => _isInitialized;
        public ServiceHeartbeatState HeartbeatState => IsServiceReady ? ServiceHeartbeatState.Ready : ServiceHeartbeatState.NotStarted;
        public bool IsServiceReady =>
            _isInitialized &&
            _registeredService &&
            ReferenceEquals(GlobalRegistry.ModularEquipment, this) &&
            _toolStates.IsCreated &&
            _toolStats.IsCreated &&
            _toolTypes.IsCreated &&
            _currentHeat.IsCreated &&
            _batteryCharge.IsCreated &&
            _statusMasks.IsCreated &&
            _environmentHeat01.IsCreated &&
            _toolIndexById.IsCreated &&
            _batteryDrainRates.IsCreated &&
            _batteryDrainDeltaSeconds.IsCreated &&
            _activeEquipmentStates.IsCreated &&
            _publishedActiveEquipmentStates.IsCreated &&
            _activeEquipmentAupSamples.IsCreated &&
            _activeEquipmentGridLoadRequests.IsCreated &&
            _equipmentTelemetryRing.IsCreated &&
            _equipmentTelemetryCursor.IsCreated &&
            _equipmentIntegrationCounters.IsCreated &&
            _equipmentOverheatSignals.IsCreated &&
            _toolDepletedSignals.IsCreated;

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
            if (_isInitialized)
            {
                TryRegisterService();
                TryRegisterUpdatable();
                TryRegisterLateFrame();
                return;
            }

            if (!CanOwnServiceSlot())
                return;

            if (!_toolStates.IsCreated)
            {
                // COLD ALLOC: NativeArray<ToolState>[16] — active modular tool state buffer — owner: ModularEquipmentEngine
                _toolStates = new NativeArray<ToolState>(MaxTrackedTools, Allocator.Persistent, NativeArrayOptions.ClearMemory);
                NativeMemorySentinel.RegisterNativeArray(
                    _toolStates,
                    nameof(ModularEquipmentEngine),
                    nameof(_toolStates),
                    NativeAllocationLifetime.Scene);
            }

            if (!_toolStats.IsCreated)
            {
                // COLD ALLOC: NativeArray<ToolRuntimeStats>[16] — active compiled tool-stat buffer — owner: ModularEquipmentEngine
                _toolStats = new NativeArray<ToolRuntimeStats>(MaxTrackedTools, Allocator.Persistent, NativeArrayOptions.ClearMemory);
                NativeMemorySentinel.RegisterNativeArray(
                    _toolStats,
                    nameof(ModularEquipmentEngine),
                    nameof(_toolStats),
                    NativeAllocationLifetime.Scene);
            }

            if (!_toolTypes.IsCreated)
            {
                // COLD ALLOC: NativeArray<byte>[16] - SOA tool type ids mirrored by active equipment slot - owner: ModularEquipmentEngine
                _toolTypes = new NativeArray<byte>(MaxTrackedTools, Allocator.Persistent, NativeArrayOptions.ClearMemory);
                NativeMemorySentinel.RegisterNativeArray(
                    _toolTypes,
                    nameof(ModularEquipmentEngine),
                    nameof(_toolTypes),
                    NativeAllocationLifetime.Scene);
            }

            if (!_currentHeat.IsCreated)
            {
                IDataVault dataVault = GlobalRegistry.DataVault;
                if (dataVault != null)
                {
                    _currentHeat = dataVault.GetBuffer<float>(
                        BufferID.ToolRuntimeHeat01,
                        MaxTrackedTools,
                        SystemID.GameplayTools,
                        NativeArrayOptions.ClearMemory);
                    _currentHeatFromDataVault = _currentHeat.IsCreated && _currentHeat.Length >= MaxTrackedTools;
                }

                if (!_currentHeatFromDataVault)
                {
                    // COLD ALLOC: NativeArray<float>[16] - fallback SOA current heat values mirrored by active equipment slot - owner: ModularEquipmentEngine
                    _currentHeat = new NativeArray<float>(MaxTrackedTools, Allocator.Persistent, NativeArrayOptions.ClearMemory);
                    NativeMemorySentinel.RegisterNativeArray(
                        _currentHeat,
                        nameof(ModularEquipmentEngine),
                        nameof(_currentHeat),
                        NativeAllocationLifetime.Scene);
                }
            }

            if (!_batteryCharge.IsCreated)
            {
                IDataVault dataVault = GlobalRegistry.DataVault;
                if (dataVault != null)
                {
                    _batteryCharge = dataVault.GetBuffer<float>(
                        BufferID.ToolRuntimeBatteryCharge,
                        MaxTrackedTools,
                        SystemID.GameplayTools,
                        NativeArrayOptions.ClearMemory);
                    _batteryChargeFromDataVault = _batteryCharge.IsCreated && _batteryCharge.Length >= MaxTrackedTools;
                }

                if (!_batteryChargeFromDataVault)
                {
                    // COLD ALLOC: NativeArray<float>[16] - fallback SOA absolute battery charge values mirrored by active equipment slot - owner: ModularEquipmentEngine
                    _batteryCharge = new NativeArray<float>(MaxTrackedTools, Allocator.Persistent, NativeArrayOptions.ClearMemory);
                    NativeMemorySentinel.RegisterNativeArray(
                        _batteryCharge,
                        nameof(ModularEquipmentEngine),
                        nameof(_batteryCharge),
                        NativeAllocationLifetime.Scene);
                }
            }

            if (!_statusMasks.IsCreated)
            {
                // COLD ALLOC: NativeArray<uint>[16] - SOA runtime status masks mirrored by active equipment slot - owner: ModularEquipmentEngine
                _statusMasks = new NativeArray<uint>(MaxTrackedTools, Allocator.Persistent, NativeArrayOptions.ClearMemory);
                NativeMemorySentinel.RegisterNativeArray(
                    _statusMasks,
                    nameof(ModularEquipmentEngine),
                    nameof(_statusMasks),
                    NativeAllocationLifetime.Scene);
            }

            if (!_environmentHeat01.IsCreated)
            {
                // COLD ALLOC: NativeArray<float>[16] - SOA thermal vent heat cache sampled at tier-gated cadence - owner: ModularEquipmentEngine
                _environmentHeat01 = new NativeArray<float>(MaxTrackedTools, Allocator.Persistent, NativeArrayOptions.ClearMemory);
                NativeMemorySentinel.RegisterNativeArray(
                    _environmentHeat01,
                    nameof(ModularEquipmentEngine),
                    nameof(_environmentHeat01),
                    NativeAllocationLifetime.Scene);
            }

            if (!_toolIndexById.IsCreated)
            {
                // COLD ALLOC: NativeHashMap<uint,int>[16] — tool-id to slot index table — owner: ModularEquipmentEngine
                _toolIndexById = new NativeHashMap<uint, int>(MaxTrackedTools, Allocator.Persistent);
                NativeMemorySentinel.RegisterNativeHashMap(
                    _toolIndexById,
                    nameof(ModularEquipmentEngine),
                    nameof(_toolIndexById),
                    NativeAllocationLifetime.Scene);
            }

            if (!_batteryDrainRates.IsCreated)
            {
                // COLD ALLOC: NativeArray<float>[16] — deferred battery drain rates batched for late-frame job — owner: ModularEquipmentEngine
                _batteryDrainRates = new NativeArray<float>(MaxTrackedTools, Allocator.Persistent, NativeArrayOptions.ClearMemory);
                NativeMemorySentinel.RegisterNativeArray(
                    _batteryDrainRates,
                    nameof(ModularEquipmentEngine),
                    nameof(_batteryDrainRates),
                    NativeAllocationLifetime.Scene);
            }

            if (!_batteryDrainDeltaSeconds.IsCreated)
            {
                // COLD ALLOC: NativeArray<float>[16] — deferred battery drain delta seconds batched for late-frame job — owner: ModularEquipmentEngine
                _batteryDrainDeltaSeconds = new NativeArray<float>(MaxTrackedTools, Allocator.Persistent, NativeArrayOptions.ClearMemory);
                NativeMemorySentinel.RegisterNativeArray(
                    _batteryDrainDeltaSeconds,
                    nameof(ModularEquipmentEngine),
                    nameof(_batteryDrainDeltaSeconds),
                    NativeAllocationLifetime.Scene);
            }

            InitializeActiveEquipmentNativeState();

            if (Application.isPlaying && transform.parent != null)
                transform.SetParent(null, true);

            _isInitialized = true;
            TryRegisterService();
            TryRegisterUpdatable();
            TryRegisterLateFrame();
        }

        public void Tick(float deltaTime)
        {
            if (!_isInitialized)
                return;

            if (_equipmentIntegrationScheduled)
                CompleteActiveEquipmentJob();

            float safeDeltaTime = math.max(0f, deltaTime);
            if (_wirelessBrownoutActive)
                _brownoutPulseTime += safeDeltaTime;

            _thermalProbeFrameIndex++;
            _lastGlobalQualityWeight = ResolveGlobalQualityWeight();
            _lastEquipmentTickInterval = ResolveEquipmentTickInterval(_lastGlobalQualityWeight);
            _equipmentCadenceAccumulator += safeDeltaTime;

            for (int i = 0; i < MaxTrackedTools; i++)
            {
                PlayerTool owner = _toolOwners[i];
                if (!_slotUsed[i] || owner == null)
                    continue;

                ToolState state = _toolStates[i];
                state.Durability = math.saturate(owner.DurabilityNormalized);
                if (IsOverchargeRequested(i))
                {
                    float runtimeHeatRate = math.max(0.05f, _toolStats[i].HeatGenerationRate);
                    float heatGrowth = EstimateOverchargeHeatGrowth(state.InternalHeat);
                    state.InternalHeat = math.max(0f, state.InternalHeat + (runtimeHeatRate * OverchargeHeatScale * heatGrowth * safeDeltaTime));
                    if (state.InternalHeat > OverchargeExplosionHeatThreshold)
                    {
                        state.StatusMask |= ToolRuntimeStatusMasks.Disabled | ToolRuntimeStatusMasks.Overheated;
                        _toolStates[i] = state;
                        WriteSlotMirrors(i, in state);
                        TriggerOverchargeExplosion(i);
                        continue;
                    }
                }

                _toolStates[i] = state;
            }

            if (_equipmentCadenceAccumulator < _lastEquipmentTickInterval)
                return;

            float integrationDelta = _equipmentCadenceAccumulator;
            _equipmentCadenceAccumulator = 0f;
            RefreshThermalGridReadback();
            RefreshActiveEquipmentInputs();
            ScheduleActiveEquipmentIntegration(integrationDelta);
        }

        public void LateFrameTick()
        {
            if (!_isInitialized)
                return;

            CompleteActiveEquipmentJob();

            if (_pendingBatteryDrainMask != 0u)
                ApplyPendingBatteryDrain();
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

            int moduleSlotCount = tool.CopyAuthoredModules(_registrationModules);
            uint upgradeMask;
            ToolRuntimeStats compiledStats = ToolUpgradeSystem.CompileRuntimeStats(
                profile,
                _registrationModules,
                Mathf.Min(moduleSlotCount, (int)profile.ModuleSlotCount),
                out upgradeMask);

            ToolState nextState = _toolStates[slotIndex];
            nextState.CurrentBattery = math.saturate(tool.ResolveModularBatteryNormalized()) * math.max(0.1f, compiledStats.BatteryCapacity);
            nextState.InternalHeat = math.max(0f, tool.ResolveModularHeatNormalized());
            nextState.Durability = math.saturate(tool.DurabilityNormalized);
            nextState.UpgradeBitmask = upgradeMask;
            nextState.StatusMask = ResolveStatusMask(0u, in nextState, in compiledStats, ResolveDepthMeters(tool), false);
            nextState.ToolTypeId = ResolveToolTypeId(profile.ToolId);
            nextState.ModuleSlotCount = (byte)math.clamp(moduleSlotCount, 0, ToolUpgradeSystem.MaxModuleSlots);
            nextState.Reserved0 = 0;
            nextState.Reserved1 = 0ul;

            _toolOwners[slotIndex] = tool;
            _slotUsed[slotIndex] = true;
            _toolStates[slotIndex] = nextState;
            _toolStats[slotIndex] = compiledStats;
            _toolIndexById[profile.ToolId] = slotIndex;
            WriteModuleMirror(slotIndex, _registrationModules, moduleSlotCount);
            SetBatteryAbsolute(slotIndex, nextState.CurrentBattery);
            WriteActiveEquipmentSlot(slotIndex, in nextState, in compiledStats);
            WriteSlotMirrors(slotIndex, in nextState);
            return profile.ToolId;
        }

        public void UnregisterTool(PlayerTool tool, uint toolId)
        {
            if (!_isInitialized || tool == null || toolId == 0u)
                return;

            if (!_toolIndexById.TryGetValue(toolId, out int slotIndex))
                return;

            if (!ReferenceEquals(_toolOwners[slotIndex], tool))
                return;

            ToolState previousState = _toolStates[slotIndex];
            PublishToolStateChanged(slotIndex, in previousState, forceHolstered: true);
            _toolIndexById.Remove(toolId);
            _toolOwners[slotIndex] = null;
            _slotUsed[slotIndex] = false;
            _toolStates[slotIndex] = default;
            _toolStats[slotIndex] = default;
            ClearActiveEquipmentSlot(slotIndex);
            ClearSlotMirrors(slotIndex);
            ClearModuleMirror(slotIndex);
        }

        public bool TryGetToolState(uint toolId, out ToolState state)
        {
            state = default;
            if (!_isInitialized || !_toolIndexById.TryGetValue(toolId, out int slotIndex))
                return false;

            state = _toolStates[slotIndex];
            state.CurrentBattery = ReadBatteryAbsolute(slotIndex);
            return true;
        }

        public bool TryGetToolStats(uint toolId, out ToolRuntimeStats stats)
        {
            stats = default;
            if (!_isInitialized || !_toolIndexById.TryGetValue(toolId, out int slotIndex))
                return false;

            stats = _toolStats[slotIndex];
            return true;
        }

        public bool TryInstallModule(uint toolId, ToolModuleData module)
        {
            if (!_isInitialized || module == null || !_toolIndexById.TryGetValue(toolId, out int slotIndex))
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
            if (!_isInitialized || string.IsNullOrWhiteSpace(moduleId) || !_toolIndexById.TryGetValue(toolId, out int slotIndex))
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

            if (!_toolIndexById.TryGetValue(toolId, out int slotIndex))
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
            if (!_isInitialized || !_toolIndexById.TryGetValue(toolId, out int slotIndex))
                return;

            float capacity = math.max(0.1f, _toolStats[slotIndex].BatteryCapacity);
            SetBatteryAbsolute(slotIndex, math.saturate(normalizedBattery) * capacity);
        }

        public float GetBatteryNormalized(uint toolId, float fallback)
        {
            if (!_isInitialized || !_toolIndexById.TryGetValue(toolId, out int slotIndex))
                return fallback;

            float capacity = math.max(0.1f, _toolStats[slotIndex].BatteryCapacity);
            return math.saturate(ReadBatteryAbsolute(slotIndex) / capacity);
        }

        public void ConsumeBattery(uint toolId, float normalizedBatteryDelta)
        {
            if (!_isInitialized || !_toolIndexById.TryGetValue(toolId, out int slotIndex))
                return;

            MarkSlotActive(slotIndex, math.max(0f, normalizedBatteryDelta) > 0f);
        }

        public void ConsumeBattery(uint toolId, float normalizedBatteryDrainRate, float deltaSeconds)
        {
            if (!_isInitialized || !_toolIndexById.TryGetValue(toolId, out int slotIndex))
                return;

            MarkSlotActive(slotIndex, math.max(0f, normalizedBatteryDrainRate) > 0f && math.max(0f, deltaSeconds) > 0f);
        }

        public void SetHeat(uint toolId, float normalizedHeat)
        {
            if (!_isInitialized || !_toolIndexById.TryGetValue(toolId, out int slotIndex))
                return;

            ToolState state = _toolStates[slotIndex];
            float sanitizedHeat = math.max(0f, normalizedHeat);
            state.InternalHeat = IsOverchargeRequested(slotIndex)
                ? math.max(state.InternalHeat, sanitizedHeat)
                : sanitizedHeat;
            ToolRuntimeStats stats = _toolStats[slotIndex];
            state.StatusMask = ResolveStatusMask(
                state.StatusMask,
                in state,
                in stats,
                ResolveDepthMeters(_toolOwners[slotIndex]),
                (state.StatusMask & ToolRuntimeStatusMasks.Active) != 0u);
            _toolStates[slotIndex] = state;
            WriteActiveEquipmentSlot(slotIndex, in state, in stats);
            WriteSlotMirrors(slotIndex, in state);
        }

        public void SetToolActive(uint toolId, bool active)
        {
            if (!_isInitialized || !_toolIndexById.TryGetValue(toolId, out int slotIndex))
                return;

            MarkSlotActive(slotIndex, active);
        }

        public bool TryGetPublishedActiveEquipmentState(uint toolId, out ActiveEquipmentDTO state)
        {
            state = default;
            if (!_isInitialized ||
                !_publishedActiveEquipmentStates.IsCreated ||
                !_toolIndexById.TryGetValue(toolId, out int slotIndex) ||
                (uint)slotIndex >= (uint)_publishedActiveEquipmentStates.Length)
            {
                return false;
            }

            state = _publishedActiveEquipmentStates[slotIndex];
            return state.ToolHashID != 0u;
        }

        public void SetDurability(uint toolId, float normalizedDurability)
        {
            if (!_isInitialized || !_toolIndexById.TryGetValue(toolId, out int slotIndex))
                return;

            ToolState state = _toolStates[slotIndex];
            state.Durability = math.saturate(normalizedDurability);
            ToolRuntimeStats stats = _toolStats[slotIndex];
            state.StatusMask = ResolveStatusMask(
                state.StatusMask,
                in state,
                in stats,
                ResolveDepthMeters(_toolOwners[slotIndex]),
                (state.StatusMask & ToolRuntimeStatusMasks.Active) != 0u);
            _toolStates[slotIndex] = state;
            WriteActiveEquipmentSlot(slotIndex, in state, in stats);
            WriteSlotMirrors(slotIndex, in state);
        }

        private void OnEnable()
        {
            SceneManager.sceneUnloaded += HandleSceneUnloaded;
            TryRegisterTelemetry();

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
            TryUnregisterTelemetry();
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
            TryUnregisterTelemetry();
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

            IDataVault dataVault = GlobalRegistry.DataVault;
            if (!_activeEquipmentStates.IsCreated)
            {
                _activeEquipmentStates = AcquireEquipmentBuffer<ActiveEquipmentDTO>(
                    dataVault,
                    BufferID.ShinobuActiveEquipmentState,
                    MaxTrackedTools,
                    NativeArrayOptions.UninitializedMemory,
                    ref _activeEquipmentStatesFromDataVault,
                    nameof(_activeEquipmentStates));
            }

            if (!_publishedActiveEquipmentStates.IsCreated)
            {
                _publishedActiveEquipmentStates = AcquireEquipmentBuffer<ActiveEquipmentDTO>(
                    dataVault,
                    BufferID.ShinobuActiveEquipmentPublishedState,
                    MaxTrackedTools,
                    NativeArrayOptions.UninitializedMemory,
                    ref _publishedActiveEquipmentStatesFromDataVault,
                    nameof(_publishedActiveEquipmentStates));
            }

            if (!_activeEquipmentAupSamples.IsCreated)
            {
                _activeEquipmentAupSamples = AcquireEquipmentBuffer<double3>(
                    dataVault,
                    BufferID.ShinobuActiveEquipmentAupSamples,
                    MaxTrackedTools,
                    NativeArrayOptions.UninitializedMemory,
                    ref _activeEquipmentAupSamplesFromDataVault,
                    nameof(_activeEquipmentAupSamples));
            }

            if (!_activeEquipmentGridLoadRequests.IsCreated)
            {
                _activeEquipmentGridLoadRequests = AcquireEquipmentBuffer<EquipmentGridLoadRequest>(
                    dataVault,
                    BufferID.ShinobuActiveEquipmentGridLoadRequests,
                    MaxTrackedTools,
                    NativeArrayOptions.UninitializedMemory,
                    ref _activeEquipmentGridLoadRequestsFromDataVault,
                    nameof(_activeEquipmentGridLoadRequests));
            }

            if (!_equipmentTelemetryRing.IsCreated)
            {
                _equipmentTelemetryRing = AcquireEquipmentBuffer<EquipmentTelemetryEntry>(
                    dataVault,
                    BufferID.ShinobuActiveEquipmentTelemetryRing,
                    EquipmentTelemetryRingLength,
                    NativeArrayOptions.UninitializedMemory,
                    ref _equipmentTelemetryRingFromDataVault,
                    nameof(_equipmentTelemetryRing));
            }

            if (!_equipmentTelemetryCursor.IsCreated)
            {
                _equipmentTelemetryCursor = AcquireEquipmentBuffer<int>(
                    dataVault,
                    BufferID.ShinobuActiveEquipmentTelemetryCursor,
                    1,
                    NativeArrayOptions.UninitializedMemory,
                    ref _equipmentTelemetryCursorFromDataVault,
                    nameof(_equipmentTelemetryCursor));
            }

            if (!_equipmentIntegrationCounters.IsCreated)
            {
                _equipmentIntegrationCounters = AcquireEquipmentBuffer<EquipmentIntegrationCounters>(
                    dataVault,
                    BufferID.ShinobuActiveEquipmentIntegrationCounters,
                    1,
                    NativeArrayOptions.UninitializedMemory,
                    ref _equipmentIntegrationCountersFromDataVault,
                    nameof(_equipmentIntegrationCounters));
            }

            ClearNativeArray(_activeEquipmentStates);
            ClearNativeArray(_publishedActiveEquipmentStates);
            ClearNativeArray(_activeEquipmentAupSamples);
            ClearNativeArray(_activeEquipmentGridLoadRequests);
            ClearNativeArray(_equipmentTelemetryRing);
            ClearNativeArray(_equipmentTelemetryCursor);
            ClearNativeArray(_equipmentIntegrationCounters);

            if (!_equipmentOverheatSignals.IsCreated)
            {
                _equipmentOverheatSignals = new NativeQueue<EquipmentOverheatSignal>(Allocator.Persistent);
                NativeMemorySentinel.RegisterNativeQueue(
                    _equipmentOverheatSignals,
                    EquipmentSignalQueueCapacity,
                    nameof(ModularEquipmentEngine),
                    nameof(_equipmentOverheatSignals),
                    NativeAllocationLifetime.Scene);
                PrewarmQueue(ref _equipmentOverheatSignals, EquipmentSignalQueueCapacity);
            }

            if (!_toolDepletedSignals.IsCreated)
            {
                _toolDepletedSignals = new NativeQueue<ToolDepletedSignal>(Allocator.Persistent);
                NativeMemorySentinel.RegisterNativeQueue(
                    _toolDepletedSignals,
                    EquipmentSignalQueueCapacity,
                    nameof(ModularEquipmentEngine),
                    nameof(_toolDepletedSignals),
                    NativeAllocationLifetime.Scene);
                PrewarmQueue(ref _toolDepletedSignals, EquipmentSignalQueueCapacity);
            }

            SignalBus<EquipmentOverheatSignal>.Configure(EquipmentSignalQueueCapacity, 128, 16, EquipmentOverheatLaneHash);
            SignalBus<ToolDepletedSignal>.Configure(EquipmentSignalQueueCapacity, 128, 16, ToolDepletedLaneHash);
            SignalBus<EquipmentOverheatSignal>.EnsureInitialized();
            SignalBus<ToolDepletedSignal>.EnsureInitialized();
        }

        private static NativeArray<T> AcquireEquipmentBuffer<T>(
            IDataVault dataVault,
            BufferID bufferId,
            int requiredLength,
            NativeArrayOptions options,
            ref bool fromDataVault,
            string label)
            where T : unmanaged
        {
            fromDataVault = false;
            if (dataVault != null)
            {
                NativeArray<T> vaultBuffer = dataVault.GetBuffer<T>(
                    bufferId,
                    requiredLength,
                    SystemID.GameplayTools,
                    options);
                fromDataVault = vaultBuffer.IsCreated && vaultBuffer.Length >= requiredLength;
                if (fromDataVault)
                    return vaultBuffer;
            }

            NativeArray<T> fallback = new NativeArray<T>(requiredLength, Allocator.Persistent, options);
            NativeMemorySentinel.RegisterNativeArray(
                fallback,
                nameof(ModularEquipmentEngine),
                label,
                NativeAllocationLifetime.Scene);
            return fallback;
        }

        private static unsafe void ClearNativeArray<T>(NativeArray<T> array)
            where T : unmanaged
        {
            if (!array.IsCreated || array.Length <= 0)
                return;

            UnsafeUtility.MemClear(array.GetUnsafePtr(), (long)array.Length * UnsafeUtility.SizeOf<T>());
        }

        private static void PrewarmQueue<T>(ref NativeQueue<T> queue, int expectedCount)
            where T : unmanaged
        {
            if (!queue.IsCreated)
                return;

            T value = default;
            for (int i = 0; i < expectedCount; i++)
                queue.Enqueue(value);

            for (int i = 0; i < expectedCount; i++)
                queue.TryDequeue(out _);
        }

        private int ResolveOrAllocateSlot(uint toolId)
        {
            if (_toolIndexById.TryGetValue(toolId, out int existingIndex))
                return existingIndex;

            for (int i = 0; i < MaxTrackedTools; i++)
            {
                if (!_slotUsed[i])
                    return i;
            }

            return -1;
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

        private float ReadBatteryAbsolute(int slotIndex)
        {
            if (!_toolStates.IsCreated || (uint)slotIndex >= (uint)_toolStates.Length)
                return 0f;

            return math.max(0f, _toolStates[slotIndex].CurrentBattery);
        }

        private void SetBatteryAbsolute(int slotIndex, float absoluteBattery)
        {
            if (!_toolStates.IsCreated || (uint)slotIndex >= (uint)_toolStates.Length)
                return;

            ToolState state = _toolStates[slotIndex];
            state.CurrentBattery = math.max(0f, absoluteBattery);
            ToolRuntimeStats stats = _toolStats[slotIndex];
            state.StatusMask = ResolveStatusMask(
                state.StatusMask,
                in state,
                in stats,
                ResolveDepthMeters(_toolOwners[slotIndex]),
                (state.StatusMask & ToolRuntimeStatusMasks.Active) != 0u);
            _toolStates[slotIndex] = state;
            WriteSlotMirrors(slotIndex, in state);
        }

        private void ConsumeBatteryAbsolute(int slotIndex, float absoluteBatteryDrainRate, float deltaSeconds)
        {
            if (absoluteBatteryDrainRate <= 0f || deltaSeconds <= 0f || !_toolStates.IsCreated || !_batteryDrainRates.IsCreated || !_batteryDrainDeltaSeconds.IsCreated || (uint)slotIndex >= (uint)_toolStates.Length)
                return;

            float existingAmount = _batteryDrainRates[slotIndex] * _batteryDrainDeltaSeconds[slotIndex];
            float nextAmount = existingAmount + (absoluteBatteryDrainRate * deltaSeconds);
            _batteryDrainRates[slotIndex] = nextAmount;
            _batteryDrainDeltaSeconds[slotIndex] = 1f;
            _pendingBatteryDrainMask |= 1u << slotIndex;
        }

        private void RebuildCompiledState(int slotIndex, PlayerTool owner, uint toolId)
        {
            if (owner == null)
                return;

            ToolRuntimeProfile profile = owner.BuildModularRuntimeProfile();
            int slotCount = Mathf.Min(GetConfiguredSlotCount(owner), (int)profile.ModuleSlotCount);
            ReadModuleMirror(slotIndex, slotCount, _registrationModules);

            float normalizedBattery = GetBatteryNormalized(toolId, owner.ResolveModularBatteryNormalized());
            ToolState state = _toolStates[slotIndex];
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
                ResolveDepthMeters(owner),
                (state.StatusMask & ToolRuntimeStatusMasks.Active) != 0u);
            _toolStats[slotIndex] = compiledStats;
            _toolStates[slotIndex] = state;
            SetBatteryAbsolute(slotIndex, state.CurrentBattery);
            WriteActiveEquipmentSlot(slotIndex, in state, in compiledStats);
            WriteSlotMirrors(slotIndex, in state);
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

        private void WriteActiveEquipmentSlot(int slotIndex, in ToolState state, in ToolRuntimeStats stats)
        {
            if (!_activeEquipmentStates.IsCreated || (uint)slotIndex >= (uint)_activeEquipmentStates.Length)
                return;

            PlayerTool owner = _toolOwners[slotIndex];
            uint existingFlags = _activeEquipmentStates[slotIndex].StateFlags;
            uint stateFlags = BuildActiveEquipmentFlags(state.StatusMask, existingFlags);
            float capacity = math.max(0.1f, stats.BatteryCapacity);

            _activeEquipmentStates[slotIndex] = new ActiveEquipmentDTO
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

        private void ClearActiveEquipmentSlot(int slotIndex)
        {
            if ((uint)slotIndex >= MaxTrackedTools)
                return;

            uint slotBit = 1u << slotIndex;
            _externalActiveToolMask &= ~slotBit;
            _lastTelemetryActiveMask &= ~slotBit;

            if (_activeEquipmentStates.IsCreated && slotIndex < _activeEquipmentStates.Length)
                _activeEquipmentStates[slotIndex] = default;
            if (_publishedActiveEquipmentStates.IsCreated && slotIndex < _publishedActiveEquipmentStates.Length)
                _publishedActiveEquipmentStates[slotIndex] = default;
            if (_activeEquipmentAupSamples.IsCreated && slotIndex < _activeEquipmentAupSamples.Length)
                _activeEquipmentAupSamples[slotIndex] = default;
            if (_activeEquipmentGridLoadRequests.IsCreated && slotIndex < _activeEquipmentGridLoadRequests.Length)
                _activeEquipmentGridLoadRequests[slotIndex] = default;
        }

        private void RefreshThermalGridReadback()
        {
            _thermalGridReadback = default;
            _thermalGridWidth = 0;
            _thermalGridHeight = 0;
            _thermalGridDepth = 0;
            _thermalGridVersion = 0;
            _thermalGridCellSizeMeters = EquipmentDefaultCellSizeMeters;
            _thermalGridRootAup = default;

            IThermodynamicsService thermodynamics = GlobalRegistry.ThermodynamicsService;
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

            _thermalGridReadback = grid;
            _thermalGridWidth = width;
            _thermalGridHeight = height;
            _thermalGridDepth = depth;
            _thermalGridVersion = version;
            _thermalGridCellSizeMeters = math.isfinite(cellSizeMeters) && cellSizeMeters > 0f
                ? cellSizeMeters
                : EquipmentDefaultCellSizeMeters;
            _thermalGridRootAup = HectonFloatingOrigin.ToAbsoluteUniversePositionDouble3(originWS);
        }

        private void RefreshActiveEquipmentInputs()
        {
            if (!_activeEquipmentStates.IsCreated || !_activeEquipmentAupSamples.IsCreated)
                return;

            _lastTelemetryActiveMask = 0u;
            bool gridAvailable = ResolveGridPowerAvailable();

            for (int i = 0; i < MaxTrackedTools; i++)
            {
                if (!_slotUsed[i] || _toolOwners[i] == null)
                {
                    ClearActiveEquipmentSlot(i);
                    continue;
                }

                PlayerTool owner = _toolOwners[i];
                ToolState state = _toolStates[i];
                ToolRuntimeStats stats = _toolStats[i];
                uint slotBit = 1u << i;
                bool requestedActive = owner.WasRecentlyUsed(ActiveToolHeatWindowSeconds) || (_externalActiveToolMask & slotBit) != 0u;
                bool gridPowered = requestedActive &&
                    gridAvailable &&
                    (state.UpgradeBitmask & (uint)ToolUpgradeBits.WirelessCharging) != 0u;
                float depthMeters = ResolveDepthMeters(owner);
                state.Durability = math.saturate(owner.DurabilityNormalized);
                state.StatusMask = ResolveStatusMask(state.StatusMask, in state, in stats, depthMeters, requestedActive, gridPowered);
                bool active = requestedActive && (state.StatusMask & ToolRuntimeStatusMasks.Disabled) == 0u;
                if (active)
                    _lastTelemetryActiveMask |= slotBit;

                state.StatusMask = ResolveStatusMask(state.StatusMask, in state, in stats, depthMeters, active, gridPowered);
                _toolStates[i] = state;

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
                if (ResolveToolInWater(owner))
                    flags |= ActiveEquipmentStateFlags.InWater;
                if (gridPowered)
                    flags |= ActiveEquipmentStateFlags.GridPowered;

                _activeEquipmentStates[i] = new ActiveEquipmentDTO
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

                Vector3 position = owner.transform.position;
                _activeEquipmentAupSamples[i] = HectonFloatingOrigin.ToAbsoluteUniversePositionDouble3(position);
            }
        }

        private static bool ResolveGridPowerAvailable()
        {
            return GlobalRegistry.Submarine != null &&
                   GlobalRegistry.Submarine.AtmosphereSystem != null &&
                   GlobalRegistry.PowerGrid != null;
        }

        private static bool ResolveToolInWater(PlayerTool owner)
        {
            IPlayerRuntimeContext playerContext = GlobalRegistry.Player;
            HectonPlayerMovement movement = playerContext != null ? playerContext.PlayerMovement : null;
            if (movement != null)
                return movement.IsPlayerSubmerged;

            return owner != null && owner.transform.position.y < -0.15f;
        }

        private static float ResolveGlobalQualityWeight()
        {
            float quality = HomeostasisBrain.GlobalQualityWeight;
            return math.saturate(math.isfinite(quality) ? quality : 1f);
        }

        private static float ResolveEquipmentTickInterval(float globalQualityWeight)
        {
            float q = math.saturate(math.isfinite(globalQualityWeight) ? globalQualityWeight : 1f);
            return math.lerp(0.016f, 0.2f, 1f - q);
        }

        private unsafe void ScheduleActiveEquipmentIntegration(float deltaSeconds)
        {
            if (_equipmentIntegrationScheduled ||
                !_activeEquipmentStates.IsCreated ||
                !_activeEquipmentAupSamples.IsCreated ||
                !_toolStats.IsCreated ||
                !_activeEquipmentGridLoadRequests.IsCreated ||
                !_equipmentIntegrationCounters.IsCreated ||
                !_equipmentOverheatSignals.IsCreated ||
                !_toolDepletedSignals.IsCreated)
            {
                return;
            }

            float safeDelta = math.max(0f, deltaSeconds);
            if (safeDelta <= 0f)
                return;

            ActiveEquipmentDTO* equipment = (ActiveEquipmentDTO*)_activeEquipmentStates.GetUnsafePtr();
            ToolRuntimeStats* stats = (ToolRuntimeStats*)_toolStats.GetUnsafeReadOnlyPtr();
            double3* aupSamples = (double3*)_activeEquipmentAupSamples.GetUnsafeReadOnlyPtr();
            EquipmentGridLoadRequest* gridRequests = (EquipmentGridLoadRequest*)_activeEquipmentGridLoadRequests.GetUnsafePtr();
            EquipmentIntegrationCounters* counters = (EquipmentIntegrationCounters*)_equipmentIntegrationCounters.GetUnsafePtr();
            float* thermalGrid = _thermalGridReadback.IsCreated && _thermalGridReadback.Length > 0
                ? (float*)_thermalGridReadback.GetUnsafeReadOnlyPtr()
                : null;

            EquipmentThermalBatteryJob job = new EquipmentThermalBatteryJob
            {
                Equipment = equipment,
                Stats = stats,
                ToolAups = aupSamples,
                ThermalGrid = thermalGrid,
                GridLoadRequests = gridRequests,
                Counters = counters,
                ToolCount = MaxTrackedTools,
                ThermalWidth = _thermalGridWidth,
                ThermalHeight = _thermalGridHeight,
                ThermalDepth = _thermalGridDepth,
                ThermalVersion = _thermalGridVersion,
                ThermalCellSizeMeters = _thermalGridCellSizeMeters,
                ThermalGridRootAup = _thermalGridRootAup,
                DeltaSeconds = safeDelta,
                Frame = unchecked((uint)Time.frameCount),
                AmbientFallbackCelsius = EquipmentFallbackAmbientCelsius,
                Tuning = EquipmentTuningDTO.CreateDefault(_lastGlobalQualityWeight),
                FaultNonFiniteMask = EquipmentFaultNonFinite,
                FaultGridInvalidMask = EquipmentFaultThermalGridInvalid,
                OverheatWriter = _equipmentOverheatSignals.AsParallelWriter(),
                DepletedWriter = _toolDepletedSignals.AsParallelWriter()
            };

            _equipmentIntegrationHandle = job.Schedule();
            _equipmentIntegrationScheduled = true;
            _equipmentTickIndex++;
        }

        private unsafe void CompleteActiveEquipmentJob()
        {
            if (!_equipmentIntegrationScheduled)
                return;

            long startTicks = Stopwatch.GetTimestamp();
            _equipmentIntegrationHandle.Complete();
            _equipmentIntegrationScheduled = false;

            PublishActiveEquipmentReadback();
            ProcessGridLoadRequests();
            DrainEquipmentSignalQueues();

            long endTicks = Stopwatch.GetTimestamp();
            float microseconds = (float)((endTicks - startTicks) * 1000000.0 / Stopwatch.Frequency);
            RecordEquipmentTelemetry(microseconds);
        }

        private unsafe void PublishActiveEquipmentReadback()
        {
            if (!_activeEquipmentStates.IsCreated || !_publishedActiveEquipmentStates.IsCreated)
                return;

            int count = math.min(_activeEquipmentStates.Length, _publishedActiveEquipmentStates.Length);
            UnsafeUtility.MemCpy(
                _publishedActiveEquipmentStates.GetUnsafePtr(),
                _activeEquipmentStates.GetUnsafeReadOnlyPtr(),
                (long)count * UnsafeUtility.SizeOf<ActiveEquipmentDTO>());

            for (int i = 0; i < MaxTrackedTools; i++)
            {
                if (!_slotUsed[i] || _toolOwners[i] == null)
                    continue;

                ActiveEquipmentDTO dto = _activeEquipmentStates[i];
                ToolState state = _toolStates[i];
                ToolRuntimeStats stats = _toolStats[i];
                bool active = (dto.StateFlags & ActiveEquipmentStateFlags.Active) != 0u;
                bool gridPowered = (dto.StateFlags & ActiveEquipmentStateFlags.GridPowered) != 0u;

                state.CurrentBattery = math.max(0f, dto.CurrentBattery);
                state.InternalHeat = math.max(0f, dto.ThermalLoad);
                state.StatusMask = ResolveStatusMask(
                    state.StatusMask,
                    in state,
                    in stats,
                    ResolveDepthMeters(_toolOwners[i]),
                    active,
                    gridPowered);
                state.StatusMask = ResolveHeatWarningHaptic(state.StatusMask, state.InternalHeat);
                _toolStates[i] = state;

                if (IsOverchargeRequested(i) && state.InternalHeat > OverchargeExplosionHeatThreshold)
                {
                    WriteSlotMirrors(i, in state);
                    TriggerOverchargeExplosion(i);
                    continue;
                }

                WriteSlotMirrors(i, in state);
            }
        }

        private void ProcessGridLoadRequests()
        {
            if (!_activeEquipmentGridLoadRequests.IsCreated)
                return;

            float requestedEnergy = 0f;
            for (int i = 0; i < math.min(MaxTrackedTools, _activeEquipmentGridLoadRequests.Length); i++)
            {
                EquipmentGridLoadRequest request = _activeEquipmentGridLoadRequests[i];
                requestedEnergy += math.max(0f, request.EnergyWattSeconds);
            }

            if (requestedEnergy <= 0.0001f)
                return;

            IPowerGridService powerGrid = GlobalRegistry.PowerGrid;
            if (powerGrid == null)
            {
                _wirelessBrownoutActive = true;
                return;
            }

            bool queued = powerGrid.TryQueueWirelessToolDrain(requestedEnergy, out float grantedEnergy);
            if (!queued || !math.isfinite(grantedEnergy) || grantedEnergy + 0.0001f < requestedEnergy * 0.95f)
                _wirelessBrownoutActive = true;
        }

        private void DrainEquipmentSignalQueues()
        {
            if (_equipmentOverheatSignals.IsCreated)
            {
                while (_equipmentOverheatSignals.TryDequeue(out EquipmentOverheatSignal signal))
                    SignalBus<EquipmentOverheatSignal>.TryPush(in signal);
            }

            if (_toolDepletedSignals.IsCreated)
            {
                while (_toolDepletedSignals.TryDequeue(out ToolDepletedSignal signal))
                    SignalBus<ToolDepletedSignal>.TryPush(in signal);
            }
        }

        private void RecordEquipmentTelemetry(float cpuMicroseconds)
        {
            if (!_equipmentTelemetryRing.IsCreated ||
                !_equipmentTelemetryCursor.IsCreated ||
                !_equipmentIntegrationCounters.IsCreated ||
                _equipmentTelemetryRing.Length == 0 ||
                _equipmentTelemetryCursor.Length == 0)
            {
                return;
            }

            EquipmentIntegrationCounters counters = _equipmentIntegrationCounters[0];
            int index = math.clamp(_equipmentTelemetryCursor[0], 0, _equipmentTelemetryRing.Length - 1);
            EquipmentTelemetryEntry entry = new EquipmentTelemetryEntry
            {
                Frame = unchecked((uint)Time.frameCount),
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
                ThermalGridCellCount = _thermalGridReadback.IsCreated ? _thermalGridReadback.Length : 0,
                SnapshotHash = ComputeActiveEquipmentSnapshotHash(),
                Reserved0 = 0u
            };

            _equipmentTelemetryRing[index] = entry;
            _equipmentTelemetryCursor[0] = (index + 1) % _equipmentTelemetryRing.Length;
            if (entry.FaultFlags != 0u && !_equipmentFaultDumped)
                DumpEquipmentTelemetry();
        }

        private uint ComputeActiveEquipmentSnapshotHash()
        {
            if (!_publishedActiveEquipmentStates.IsCreated)
                return 0u;

            uint hash = 2166136261u;
            int count = math.min(MaxTrackedTools, _publishedActiveEquipmentStates.Length);
            for (int i = 0; i < count; i++)
            {
                ActiveEquipmentDTO dto = _publishedActiveEquipmentStates[i];
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

        private unsafe void DumpEquipmentTelemetry()
        {
            _equipmentFaultDumped = true;
            if (!_equipmentTelemetryRing.IsCreated)
                return;

            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string dumpPath = Path.Combine(projectRoot, EquipmentFaultDumpPath);
            string directory = Path.GetDirectoryName(dumpPath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            using (FileStream stream = new FileStream(dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read))
            {
                uint header = 0x45515448u; // H8TE
                byte[] headerBytes = BitConverter.GetBytes(header);
                stream.Write(headerBytes, 0, headerBytes.Length);
                byte[] countBytes = BitConverter.GetBytes(_equipmentTelemetryRing.Length);
                stream.Write(countBytes, 0, countBytes.Length);
                byte[] strideBytes = BitConverter.GetBytes(UnsafeUtility.SizeOf<EquipmentTelemetryEntry>());
                stream.Write(strideBytes, 0, strideBytes.Length);

                void* source = _equipmentTelemetryRing.GetUnsafeReadOnlyPtr();
                int byteLength = _equipmentTelemetryRing.Length * UnsafeUtility.SizeOf<EquipmentTelemetryEntry>();
                byte[] scratch = new byte[byteLength];
                fixed (byte* destination = scratch)
                    UnsafeUtility.MemCpy(destination, source, byteLength);
                stream.Write(scratch, 0, scratch.Length);
            }
        }

        private void ApplyRuntimeHeatAndStatus(int slotIndex, PlayerTool owner, float deltaTime, HectonQualityTier scalabilityTier)
        {
            if ((uint)slotIndex >= MaxTrackedTools || owner == null)
                return;

            ToolState state = _toolStates[slotIndex];
            ToolRuntimeStats stats = _toolStats[slotIndex];
            float depthMeters = ResolveDepthMeters(owner);
            bool active = owner.WasRecentlyUsed(ActiveToolHeatWindowSeconds);
            float activePower = active ? math.max(0f, stats.HeatGenerationRate * stats.PowerScalar) : 0f;
            float coolingDenominator = ResolveCoolingDenominator(depthMeters);
            float ambientCooling = math.max(0f, stats.CooldownRate) * math.rcp(coolingDenominator);
            float environmentHeat01 = ResolveEnvironmentHeat01(slotIndex, owner, depthMeters, scalabilityTier);
            _environmentHeat01[slotIndex] = environmentHeat01;
            float ventHeat = environmentHeat01 * ThermalVentHeatScale * math.max(0.05f, stats.HeatGenerationRate);
            float nextHeat = state.InternalHeat + ((activePower + ventHeat - ambientCooling) * deltaTime);
            state.InternalHeat = math.isfinite(nextHeat) ? math.max(0f, nextHeat) : 0f;
            state.StatusMask = ResolveStatusMask(state.StatusMask, in state, in stats, depthMeters, active);
            state.StatusMask = ResolveHeatWarningHaptic(state.StatusMask, state.InternalHeat);
            _toolStates[slotIndex] = state;
            WriteSlotMirrors(slotIndex, in state);
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

        private float ResolveEnvironmentHeat01(int slotIndex, PlayerTool owner, float depthMeters, HectonQualityTier scalabilityTier)
        {
            if (!DistanceMath.IsHighQualityTier(scalabilityTier))
            {
                float depth01 = math.saturate((depthMeters - 450f) * 0.00125f);
                return depth01 * depth01 * (3f - 2f * depth01) * 0.25f;
            }

            if (((_thermalProbeFrameIndex + slotIndex) & ThermalProbeFrameMask) != 0)
                return _environmentHeat01[slotIndex];

            AbyssalThermalManager thermodynamics = GlobalRegistry.Thermodynamics;
            if (thermodynamics == null || owner == null)
                return 0f;

            if (!thermodynamics.SampleThermalFlow(owner.transform.position, ThermalProbeRadiusMeters, out AbyssalThermalManager.ThermalFlowSample sample))
                return 0f;

            return math.saturate(sample.Heat01);
        }

        private static float ResolveCoolingDenominator(float depthMeters)
        {
            float inverseDepthCooling = math.rcp(1f + math.max(0f, depthMeters) * DepthCoolingScale);
            return math.max(MinimumCoolingDenominator, inverseDepthCooling);
        }

        private static float ResolveDepthMeters(PlayerTool owner)
        {
            IPlayerRuntimeContext playerContext = GlobalRegistry.Player;
            if (playerContext != null)
            {
                HectonSurvivalSystem survivalSystem = playerContext.SurvivalSystem;
                if (survivalSystem != null)
                    return math.max(0f, survivalSystem.Depth);

                HectonPlayerMovement movement = playerContext.PlayerMovement;
                if (movement != null)
                    return math.max(0f, movement.CurrentDepth);
            }

            return owner != null ? math.max(0f, -owner.transform.position.y) : 0f;
        }

        private static byte ResolveToolTypeId(uint toolId)
        {
            byte typeId = (byte)(toolId ^ (toolId >> 8) ^ (toolId >> 16) ^ (toolId >> 24));
            return typeId != 0 ? typeId : (byte)1;
        }

        private void WriteSlotMirrors(int slotIndex, in ToolState state)
        {
            if ((uint)slotIndex >= MaxTrackedTools)
                return;

            _toolTypes[slotIndex] = state.ToolTypeId;
            _currentHeat[slotIndex] = state.InternalHeat;
            _batteryCharge[slotIndex] = state.CurrentBattery;
            _statusMasks[slotIndex] = state.StatusMask;
            _environmentHeat01[slotIndex] = math.saturate(_environmentHeat01[slotIndex]);
            PublishToolStateChanged(slotIndex, in state, forceHolstered: false);
        }

        private void ClearSlotMirrors(int slotIndex)
        {
            if ((uint)slotIndex >= MaxTrackedTools)
                return;

            ToolState previousState = _toolStates.IsCreated ? _toolStates[slotIndex] : default;
            PublishToolStateChanged(slotIndex, in previousState, forceHolstered: true);

            _toolTypes[slotIndex] = 0;
            _currentHeat[slotIndex] = 0f;
            _batteryCharge[slotIndex] = 0f;
            _statusMasks[slotIndex] = 0u;
            _environmentHeat01[slotIndex] = 0f;
            _lastPublishedEquippedMask &= ~(1u << slotIndex);
        }

        private void PublishToolStateChanged(int slotIndex, in ToolState state, bool forceHolstered)
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

            ToolRuntimeStats stats = _toolStats[slotIndex];
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

            HectonQualityTier qualityTier = GlobalRegistry.ScalabilityTier;
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

        private void TryRegisterService()
        {
            if (_registeredService)
                return;

            if (!CanOwnServiceSlot())
                return;

            GlobalRegistry.RegisterModularEquipmentService(this);
            _registeredService = ReferenceEquals(GlobalRegistry.ModularEquipment, this);
        }

        internal bool TryGetWirelessBrownoutFeedback(uint toolId, out float flickerScalar)
        {
            flickerScalar = 0f;
            if (!_wirelessBrownoutActive || !_isInitialized || !_toolIndexById.TryGetValue(toolId, out int slotIndex))
                return false;

            if ((_toolStates[slotIndex].UpgradeBitmask & (uint)ToolUpgradeBits.WirelessCharging) == 0u)
                return false;

            float pulse = 0.35f + (0.65f * math.abs(FastTriangleSigned(_brownoutPulseTime * WirelessBrownoutPulseCycles)));
            flickerScalar = pulse;
            return true;
        }

        internal bool TryGetToolBrownoutFeedback(uint toolId, out float flickerScalar)
        {
            flickerScalar = 0f;
            if (!_wirelessBrownoutActive || !_isInitialized || !_toolIndexById.TryGetValue(toolId, out int slotIndex))
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

        private void TryRegisterTelemetry()
        {
            if (_telemetrySubscribed)
                return;

            PowerGridTelemetryEvents.Register(this);
            _telemetrySubscribed = true;
        }

        private void TryUnregisterTelemetry()
        {
            if (!_telemetrySubscribed)
                return;

            PowerGridTelemetryEvents.Unregister(this);
            _telemetrySubscribed = false;
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

        /// <summary>
        /// Receives deferred aggregate power telemetry snapshots.
        /// </summary>
        /// <param name="snapshot">Aggregate power telemetry snapshot.</param>
        public void OnPowerGridTelemetryUpdated(in PowerGridTelemetrySnapshot snapshot)
        {
            ApplyPowerGridTelemetry(in snapshot);
        }

        void Hecton8.Power.IPowerGridTelemetryListener.OnPowerGridTelemetryUpdated(in Hecton8.Power.PowerGridTelemetrySnapshot snapshot)
        {
            ApplyPowerGridTelemetry(in snapshot);
        }

        private void ApplyPowerGridTelemetry(in PowerGridTelemetrySnapshot snapshot)
        {
            _latestSupplyRatio = math.saturate(snapshot.SupplyRatio);
            _wirelessBrownoutActive = _latestSupplyRatio < 0.40f;
            if (!_wirelessBrownoutActive)
                _brownoutPulseTime = 0f;
        }

        private bool IsOverchargeRequested(int slotIndex)
        {
            PlayerTool owner = slotIndex >= 0 && slotIndex < MaxTrackedTools ? _toolOwners[slotIndex] : null;
            return owner != null && owner.IsRuntimeOverchargeRequested();
        }

        private void TriggerOverchargeExplosion(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= MaxTrackedTools)
                return;

            PlayerTool owner = _toolOwners[slotIndex];
            if (owner == null)
                return;

            owner.HandleRuntimeOverchargeFailure(OverchargeExplosionPlayerDamage);

            uint runtimeToolId = owner.RuntimeToolId;
            ToolState failedState = _toolStates[slotIndex];
            PublishToolStateChanged(slotIndex, in failedState, forceHolstered: true);
            if (runtimeToolId != 0u && _toolIndexById.IsCreated)
                _toolIndexById.Remove(runtimeToolId);

            _toolOwners[slotIndex] = null;
            _slotUsed[slotIndex] = false;
            _toolStates[slotIndex] = default;
            _toolStats[slotIndex] = default;
            ClearActiveEquipmentSlot(slotIndex);
            ClearSlotMirrors(slotIndex);
            ClearModuleMirror(slotIndex);
        }

        private void DisposeNativeState()
        {
            if (_equipmentIntegrationScheduled)
            {
                _equipmentIntegrationHandle.Complete();
                _equipmentIntegrationScheduled = false;
            }

            for (int i = 0; i < MaxTrackedTools; i++)
            {
                _toolOwners[i] = null;
                _slotUsed[i] = false;
            }

            for (int i = 0; i < _moduleSlots.Length; i++)
                _moduleSlots[i] = null;

            if (_toolStates.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeArray(_toolStates);
                _toolStates.Dispose();
            }

            if (_toolStats.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeArray(_toolStats);
                _toolStats.Dispose();
            }

            if (_toolTypes.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeArray(_toolTypes);
                _toolTypes.Dispose();
            }

            if (_currentHeat.IsCreated)
            {
                if (!_currentHeatFromDataVault)
                {
                    NativeMemorySentinel.UnregisterNativeArray(_currentHeat);
                    _currentHeat.Dispose();
                }
            }

            if (_batteryCharge.IsCreated)
            {
                if (!_batteryChargeFromDataVault)
                {
                    NativeMemorySentinel.UnregisterNativeArray(_batteryCharge);
                    _batteryCharge.Dispose();
                }
            }

            if (_statusMasks.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeArray(_statusMasks);
                _statusMasks.Dispose();
            }

            if (_environmentHeat01.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeArray(_environmentHeat01);
                _environmentHeat01.Dispose();
            }

            if (_toolIndexById.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeHashMap(nameof(ModularEquipmentEngine), nameof(_toolIndexById));
                _toolIndexById.Dispose();
            }

            if (_batteryDrainRates.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeArray(_batteryDrainRates);
                _batteryDrainRates.Dispose();
            }

            if (_batteryDrainDeltaSeconds.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeArray(_batteryDrainDeltaSeconds);
                _batteryDrainDeltaSeconds.Dispose();
            }

            DisposeEquipmentArray(ref _activeEquipmentStates, _activeEquipmentStatesFromDataVault, nameof(_activeEquipmentStates));
            DisposeEquipmentArray(ref _publishedActiveEquipmentStates, _publishedActiveEquipmentStatesFromDataVault, nameof(_publishedActiveEquipmentStates));
            DisposeEquipmentArray(ref _activeEquipmentAupSamples, _activeEquipmentAupSamplesFromDataVault, nameof(_activeEquipmentAupSamples));
            DisposeEquipmentArray(ref _activeEquipmentGridLoadRequests, _activeEquipmentGridLoadRequestsFromDataVault, nameof(_activeEquipmentGridLoadRequests));
            DisposeEquipmentArray(ref _equipmentTelemetryRing, _equipmentTelemetryRingFromDataVault, nameof(_equipmentTelemetryRing));
            DisposeEquipmentArray(ref _equipmentTelemetryCursor, _equipmentTelemetryCursorFromDataVault, nameof(_equipmentTelemetryCursor));
            DisposeEquipmentArray(ref _equipmentIntegrationCounters, _equipmentIntegrationCountersFromDataVault, nameof(_equipmentIntegrationCounters));

            if (_equipmentOverheatSignals.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeQueue(nameof(ModularEquipmentEngine), nameof(_equipmentOverheatSignals));
                _equipmentOverheatSignals.Dispose();
            }

            if (_toolDepletedSignals.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeQueue(nameof(ModularEquipmentEngine), nameof(_toolDepletedSignals));
                _toolDepletedSignals.Dispose();
            }

            _isInitialized = false;
            _toolStates = default;
            _toolStats = default;
            _toolTypes = default;
            _currentHeat = default;
            _batteryCharge = default;
            _statusMasks = default;
            _environmentHeat01 = default;
            _activeEquipmentStates = default;
            _publishedActiveEquipmentStates = default;
            _activeEquipmentAupSamples = default;
            _activeEquipmentGridLoadRequests = default;
            _equipmentTelemetryRing = default;
            _equipmentTelemetryCursor = default;
            _equipmentIntegrationCounters = default;
            _toolIndexById = default;
            _equipmentOverheatSignals = default;
            _toolDepletedSignals = default;
            _batteryDrainRates = default;
            _batteryDrainDeltaSeconds = default;
            _currentHeatFromDataVault = false;
            _batteryChargeFromDataVault = false;
            _activeEquipmentStatesFromDataVault = false;
            _publishedActiveEquipmentStatesFromDataVault = false;
            _activeEquipmentAupSamplesFromDataVault = false;
            _activeEquipmentGridLoadRequestsFromDataVault = false;
            _equipmentTelemetryRingFromDataVault = false;
            _equipmentTelemetryCursorFromDataVault = false;
            _equipmentIntegrationCountersFromDataVault = false;
            _pendingBatteryDrainMask = 0u;
            _lastPublishedEquippedMask = 0u;
            _externalActiveToolMask = 0u;
            _lastTelemetryActiveMask = 0u;
            _thermalProbeFrameIndex = 0;
            _thermalGridReadback = default;
            _thermalGridWidth = 0;
            _thermalGridHeight = 0;
            _thermalGridDepth = 0;
            _thermalGridVersion = 0;
            _equipmentTickIndex = 0u;
        }

        private static void DisposeEquipmentArray<T>(ref NativeArray<T> array, bool fromDataVault, string label)
            where T : unmanaged
        {
            if (!array.IsCreated)
                return;

            if (!fromDataVault)
            {
                NativeMemorySentinel.UnregisterNativeArray(array);
                array.Dispose();
            }

            array = default;
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
        private unsafe struct EquipmentThermalBatteryJob : IJob
        {
            [NativeDisableUnsafePtrRestriction] public ActiveEquipmentDTO* Equipment;
            [NativeDisableUnsafePtrRestriction] public ToolRuntimeStats* Stats;
            [NativeDisableUnsafePtrRestriction] public double3* ToolAups;
            [NativeDisableUnsafePtrRestriction] public float* ThermalGrid;
            [NativeDisableUnsafePtrRestriction] public EquipmentGridLoadRequest* GridLoadRequests;
            [NativeDisableUnsafePtrRestriction] public EquipmentIntegrationCounters* Counters;
            [NativeDisableContainerSafetyRestriction] public NativeQueue<EquipmentOverheatSignal>.ParallelWriter OverheatWriter;
            [NativeDisableContainerSafetyRestriction] public NativeQueue<ToolDepletedSignal>.ParallelWriter DepletedWriter;
            public int ToolCount;
            public int ThermalWidth;
            public int ThermalHeight;
            public int ThermalDepth;
            public int ThermalVersion;
            public float ThermalCellSizeMeters;
            public double3 ThermalGridRootAup;
            public float DeltaSeconds;
            public uint Frame;
            public float AmbientFallbackCelsius;
            public EquipmentTuningDTO Tuning;
            public uint FaultNonFiniteMask;
            public uint FaultGridInvalidMask;

            public void Execute()
            {
                EquipmentIntegrationCounters counters = default;
                float safeDelta = math.max(0f, DeltaSeconds);
                int count = math.max(0, ToolCount);

                for (int i = 0; i < count; i++)
                {
                    GridLoadRequests[i] = default;
                    ref ActiveEquipmentDTO dto = ref UnsafeUtility.AsRef<ActiveEquipmentDTO>(Equipment + i);
                    if (dto.ToolHashID == 0u)
                        continue;

                    ToolRuntimeStats stats = Stats[i];
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

                    float ambient01 = ResolveAmbientHeat01(ambientCelsius, in Tuning);
                    float cooldownRate = math.max(0.05f, stats.CooldownRate);
                    float waterMultiplier = inWater ? math.max(1f, Tuning.WaterCoolingMultiplier) : 1f;
                    float exchange = (ambient01 - heat) * cooldownRate * math.max(0f, Tuning.CoolingGain) * waterMultiplier * safeDelta;
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
                    }

                    dto.CurrentBattery = battery;
                    dto.ThermalLoad = heat;
                    dto.StateFlags = flags;
                    dto.PowerDrawRate = drawRate;
                    dto.HeatGenerationRate = heatRate;
                    counters.PeakThermal01 = math.max(counters.PeakThermal01, math.saturate(heat));
                    counters.ActiveCount += (flags & ActiveEquipmentStateFlags.Active) != 0u ? 1u : 0u;
                }

                Counters[0] = counters;
            }

            private float SampleAmbientCelsius(int slotIndex, ref EquipmentIntegrationCounters counters, uint toolHash)
            {
                if (ThermalGrid == null)
                    return AmbientFallbackCelsius;

                if (ThermalWidth <= 0 || ThermalHeight <= 0 || ThermalDepth <= 0 || !IsFinite(ThermalCellSizeMeters) || ThermalCellSizeMeters <= 0f)
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
                int3 cell = (int3)math.floor(local * invCell);
                if (cell.x < 0 || cell.y < 0 || cell.z < 0 || cell.x >= ThermalWidth || cell.y >= ThermalHeight || cell.z >= ThermalDepth)
                    return AmbientFallbackCelsius;

                int index = cell.x + (cell.y * ThermalWidth) + (cell.z * ThermalWidth * ThermalHeight);
                float ambient = ThermalGrid[index];
                if (IsFinite(ambient))
                    return ambient;

                counters.FaultFlags |= FaultNonFiniteMask;
                counters.LastFaultToolHashID = toolHash;
                return AmbientFallbackCelsius;
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
                AssertOffset<ActiveEquipmentDTO>(nameof(ActiveEquipmentDTO.ToolHashID), 0);
                AssertOffset<ActiveEquipmentDTO>(nameof(ActiveEquipmentDTO.CurrentBattery), 4);
                AssertOffset<ActiveEquipmentDTO>(nameof(ActiveEquipmentDTO.ThermalLoad), 8);
                AssertOffset<ActiveEquipmentDTO>(nameof(ActiveEquipmentDTO.StateFlags), 12);
                AssertOffset<ActiveEquipmentDTO>(nameof(ActiveEquipmentDTO.PowerDrawRate), 16);
                AssertOffset<ActiveEquipmentDTO>(nameof(ActiveEquipmentDTO.HeatGenerationRate), 20);
                AssertOffset<ActiveEquipmentDTO>(nameof(ActiveEquipmentDTO._pad0), 24);
                AssertSize<EquipmentGridLoadRequest>(16);
                AssertSize<EquipmentIntegrationCounters>(32);
                AssertSize<EquipmentTelemetryEntry>(64);
                AssertSize<EquipmentOverheatSignal>(32);
                AssertSize<ToolDepletedSignal>(32);
                _validated = true;
            }

            private static void AssertSize<T>(int expected)
                where T : unmanaged
            {
                int observed = UnsafeUtility.SizeOf<T>();
                if (observed != expected)
                    throw new InvalidOperationException($"[SHINOBU_148] Layout size mismatch for {typeof(T).Name}: {observed} != {expected}");
            }

            private static void AssertOffset<T>(string fieldName, int expected)
                where T : unmanaged
            {
                int observed = Marshal.OffsetOf<T>(fieldName).ToInt32();
                if (observed != expected)
                    throw new InvalidOperationException($"[SHINOBU_148] Layout offset mismatch for {typeof(T).Name}.{fieldName}: {observed} != {expected}");
            }
        }

        private static float EstimateOverchargeHeatGrowth(float internalHeat)
        {
            float x = math.min(OverchargeHeatGrowthInputMax, math.max(0f, internalHeat) * OverchargeHeatExponent);
            float x2 = x * x;
            float numerator = 1f + (0.5f * x) + (0.083333336f * x2);
            float denominator = math.max(0.125f, 1f - (0.5f * x) + (0.083333336f * x2));
            return numerator * math.rcp(denominator);
        }

        private void ApplyPendingBatteryDrain()
        {
            uint pendingMask = _pendingBatteryDrainMask;
            _pendingBatteryDrainMask = 0u;

            for (int i = 0; i < MaxTrackedTools; i++)
            {
                uint slotBit = 1u << i;
                if ((pendingMask & slotBit) == 0u)
                    continue;

                float drainAmount = math.max(0f, _batteryDrainRates[i]) * math.max(0f, _batteryDrainDeltaSeconds[i]);
                if (drainAmount > 0f)
                {
                    ToolState state = _toolStates[i];
                    state.CurrentBattery = math.max(0f, state.CurrentBattery - drainAmount);
                    ToolRuntimeStats stats = _toolStats[i];
                    state.StatusMask = ResolveStatusMask(
                        state.StatusMask,
                        in state,
                        in stats,
                        ResolveDepthMeters(_toolOwners[i]),
                        (state.StatusMask & ToolRuntimeStatusMasks.Active) != 0u);
                    _toolStates[i] = state;
                    WriteSlotMirrors(i, in state);
                }

                _batteryDrainRates[i] = 0f;
                _batteryDrainDeltaSeconds[i] = 0f;
            }
        }
    }
}
