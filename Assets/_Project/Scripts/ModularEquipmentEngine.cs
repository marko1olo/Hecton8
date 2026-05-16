namespace Hecton8.Tools
{
    using Hecton8.Core;
    using Hecton8.Core.Memory;
    using Hecton8.Core.Contracts.Signals;
    using Hecton8.Gameplay;
    using Hecton8.Power;
    using Hecton8.World;
    using Unity.Collections;
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
        private NativeHashMap<uint, int> _toolIndexById;
        private bool _currentHeatFromDataVault;
        private bool _batteryChargeFromDataVault;
        private bool _isInitialized;
        private bool _registeredService;
        private bool _registeredUpdatable;
        private bool _registeredLateFrame;
        private bool _telemetrySubscribed;
        private uint _pendingBatteryDrainMask;
        private uint _lastPublishedEquippedMask;
        private int _thermalProbeFrameIndex;
        private float _latestSupplyRatio = 1f;
        private bool _wirelessBrownoutActive;
        private float _brownoutPulseTime;

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
            _batteryDrainDeltaSeconds.IsCreated;

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

            float safeDeltaTime = math.max(0f, deltaTime);
            if (_wirelessBrownoutActive)
                _brownoutPulseTime += safeDeltaTime;

            _thermalProbeFrameIndex++;
            HectonQualityTier scalabilityTier = GlobalRegistry.ScalabilityTier;

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
                ApplyRuntimeHeatAndStatus(i, owner, safeDeltaTime, scalabilityTier);
            }
        }

        public void LateFrameTick()
        {
            if (!_isInitialized ||
                !_toolStates.IsCreated ||
                !_batteryDrainRates.IsCreated ||
                !_batteryDrainDeltaSeconds.IsCreated)
                return;

            if (_pendingBatteryDrainMask == 0u)
                return;

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

            float capacity = math.max(0.1f, _toolStats[slotIndex].BatteryCapacity);
            ConsumeBatteryAbsolute(slotIndex, math.max(0f, normalizedBatteryDelta) * capacity, 1f);
        }

        public void ConsumeBattery(uint toolId, float normalizedBatteryDrainRate, float deltaSeconds)
        {
            if (!_isInitialized || !_toolIndexById.TryGetValue(toolId, out int slotIndex))
                return;

            float capacity = math.max(0.1f, _toolStats[slotIndex].BatteryCapacity);
            ConsumeBatteryAbsolute(slotIndex, math.max(0f, normalizedBatteryDrainRate) * capacity, math.max(0f, deltaSeconds));
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
            WriteSlotMirrors(slotIndex, in state);
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
            WriteSlotMirrors(slotIndex, in state);
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

        private static uint ResolveStatusMask(uint currentStatus, in ToolState state, in ToolRuntimeStats stats, float depthMeters, bool active)
        {
            uint status = currentStatus & ToolRuntimeStatusMasks.HeatWarningHapticQueued;
            if (active)
                status |= ToolRuntimeStatusMasks.Active;

            if (state.CurrentBattery <= 0.0001f || stats.BatteryCapacity <= 0.0001f)
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
            ClearSlotMirrors(slotIndex);
            ClearModuleMirror(slotIndex);
        }

        private void DisposeNativeState()
        {
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

            _isInitialized = false;
            _toolStates = default;
            _toolStats = default;
            _toolTypes = default;
            _currentHeat = default;
            _batteryCharge = default;
            _statusMasks = default;
            _environmentHeat01 = default;
            _toolIndexById = default;
            _batteryDrainRates = default;
            _batteryDrainDeltaSeconds = default;
            _currentHeatFromDataVault = false;
            _batteryChargeFromDataVault = false;
            _pendingBatteryDrainMask = 0u;
            _lastPublishedEquippedMask = 0u;
            _thermalProbeFrameIndex = 0;
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
