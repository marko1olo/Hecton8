namespace Hecton8.Tools
{
    using Hecton8.Core;
    using Hecton8.Gameplay;
    using Hecton8.Power;
    using Hecton8.World;
    using Unity.Burst;
    using Unity.Collections;
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
    public sealed class ModularEquipmentEngine : MonoBehaviour, IModularEquipmentService, IUpdatable, ILateFrameTickable, IPowerGridTelemetryListener
    {
        private const int MaxTrackedTools = 16;
        private const float OverchargePowerMultiplier = 3f;
        private const float OverchargeHeatExponent = 1.35f;
        private const float OverchargeHeatScale = 1.75f;
        private const float OverchargeExplosionHeatThreshold = 1.5f;
        private const float OverchargeExplosionPlayerDamage = 45f;
        private const double BatteryDrainJobWarningMilliseconds = 0.2d;

        private static readonly uint _batteryDrainJobWarningHash =
            unchecked((uint)Hecton.Localization.LocHash.Compute("ModularEquipment.BatteryDrainJob"));
        private static readonly uint _batteryDrainJobContextHash =
            unchecked((uint)Hecton.Localization.LocHash.Compute("ModularEquipment.LateFrameDrain"));

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
        private NativeArray<float> _batteryDrainRates;
        private NativeArray<float> _batteryDrainDeltaSeconds;
        private NativeHashMap<uint, int> _toolIndexById;
        private JobHandle _batteryDrainHandle;
        private bool _isInitialized;
        private bool _registeredService;
        private bool _registeredUpdatable;
        private bool _registeredLateFrame;
        private bool _telemetrySubscribed;
        private bool _batteryDrainJobScheduled;
        private uint _pendingBatteryDrainMask;
        private float _latestSupplyRatio = 1f;
        private bool _wirelessBrownoutActive;
        private float _brownoutPulseTime;

        public bool IsInitialized => _isInitialized;

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private struct ToolBatteryDrainJob : IJob
        {
            public NativeArray<ToolState> ToolStates;
            public NativeArray<float> DrainRates;
            public NativeArray<float> DeltaSeconds;
            public int SlotCount;

            public void Execute()
            {
                if (!ToolStates.IsCreated || !DrainRates.IsCreated || !DeltaSeconds.IsCreated)
                    return;

                int count = math.min(SlotCount, math.min(ToolStates.Length, math.min(DrainRates.Length, DeltaSeconds.Length)));
                for (int i = 0; i < count; i++)
                {
                    float drainAmount = math.max(0f, DrainRates[i]) * math.max(0f, DeltaSeconds[i]);
                    if (drainAmount > 0f)
                    {
                        ToolState state = ToolStates[i];
                        state.CurrentBattery = math.max(0f, state.CurrentBattery - drainAmount);
                        ToolStates[i] = state;
                    }

                    DrainRates[i] = 0f;
                    DeltaSeconds[i] = 0f;
                }
            }
        }

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

            if (_wirelessBrownoutActive)
                _brownoutPulseTime += math.max(0f, deltaTime);

            for (int i = 0; i < MaxTrackedTools; i++)
            {
                if (!_slotUsed[i] || _toolOwners[i] == null)
                    continue;

                ToolState state = _toolStates[i];
                state.Durability = math.saturate(_toolOwners[i].DurabilityNormalized);
                if (IsOverchargeRequested(i))
                {
                    float runtimeHeatRate = math.max(0.05f, _toolStats[i].HeatGenerationRate);
                    float heatGrowth = math.exp(math.max(0f, state.InternalHeat) * OverchargeHeatExponent);
                    state.InternalHeat = math.max(0f, state.InternalHeat + (runtimeHeatRate * OverchargeHeatScale * heatGrowth * math.max(0f, deltaTime)));
                    if (state.InternalHeat > OverchargeExplosionHeatThreshold)
                    {
                        _toolStates[i] = state;
                        TriggerOverchargeExplosion(i);
                        continue;
                    }
                }

                _toolStates[i] = state;
            }
        }

        public void LateFrameTick()
        {
            if (!_isInitialized || !_toolStates.IsCreated)
                return;

            CompleteBatteryDrainJobIfNeeded(false);
            if (_pendingBatteryDrainMask == 0u || _batteryDrainJobScheduled)
                return;

            _batteryDrainHandle = new ToolBatteryDrainJob
            {
                ToolStates = _toolStates,
                DrainRates = _batteryDrainRates,
                DeltaSeconds = _batteryDrainDeltaSeconds,
                SlotCount = MaxTrackedTools
            }.Schedule();
            _batteryDrainJobScheduled = true;
            _pendingBatteryDrainMask = 0u;

            CompleteBatteryDrainJobIfNeeded(true);
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

            _toolOwners[slotIndex] = tool;
            _slotUsed[slotIndex] = true;
            _toolStates[slotIndex] = nextState;
            _toolStats[slotIndex] = compiledStats;
            _toolIndexById[profile.ToolId] = slotIndex;
            WriteModuleMirror(slotIndex, _registrationModules, moduleSlotCount);
            SetBatteryAbsolute(slotIndex, nextState.CurrentBattery);
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

            _toolIndexById.Remove(toolId);
            _toolOwners[slotIndex] = null;
            _slotUsed[slotIndex] = false;
            _toolStates[slotIndex] = default;
            _toolStats[slotIndex] = default;
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
            _toolStates[slotIndex] = state;
        }

        public void SetDurability(uint toolId, float normalizedDurability)
        {
            if (!_isInitialized || !_toolIndexById.TryGetValue(toolId, out int slotIndex))
                return;

            ToolState state = _toolStates[slotIndex];
            state.Durability = math.saturate(normalizedDurability);
            _toolStates[slotIndex] = state;
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
            SceneManager.sceneUnloaded -= HandleSceneUnloaded;
            TryUnregisterTelemetry();
            TryUnregisterService();
            TryUnregisterUpdatable();
            TryUnregisterLateFrame();
            CompleteBatteryDrainJobIfNeeded(true);
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
            _toolStates[slotIndex] = state;
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
            _toolStats[slotIndex] = compiledStats;
            _toolStates[slotIndex] = state;
            SetBatteryAbsolute(slotIndex, state.CurrentBattery);
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

            float pulse = 0.35f + (0.65f * math.abs(math.sin(_brownoutPulseTime * 18f)));
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

            flickerScalar = math.saturate(0.5f + (0.5f * Mathf.Sin(Time.time * 8f)));
            return true;
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

            DisposeNativeState();
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
            if (runtimeToolId != 0u && _toolIndexById.IsCreated)
                _toolIndexById.Remove(runtimeToolId);

            _toolOwners[slotIndex] = null;
            _slotUsed[slotIndex] = false;
            _toolStates[slotIndex] = default;
            _toolStats[slotIndex] = default;
            ClearModuleMirror(slotIndex);
        }

        private void DisposeNativeState()
        {
            CompleteBatteryDrainJobIfNeeded(true);

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
            _toolIndexById = default;
            _batteryDrainRates = default;
            _batteryDrainDeltaSeconds = default;
            _pendingBatteryDrainMask = 0u;
        }

        private void CompleteBatteryDrainJobIfNeeded(bool forceComplete)
        {
            if (!_batteryDrainJobScheduled)
                return;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            long startTimestamp = System.Diagnostics.Stopwatch.GetTimestamp();
#endif
            if (!DispatcherJobSwap.TryComplete(ref _batteryDrainHandle, forceComplete))
                return;

            _batteryDrainJobScheduled = false;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            double elapsedMilliseconds =
                (System.Diagnostics.Stopwatch.GetTimestamp() - startTimestamp) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
            if (elapsedMilliseconds > BatteryDrainJobWarningMilliseconds)
            {
                GlobalTelemetryBus.PublishPerformanceWarning(
                    _batteryDrainJobWarningHash,
                    _batteryDrainJobContextHash,
                    (float)elapsedMilliseconds);
            }
#endif
        }
    }
}
