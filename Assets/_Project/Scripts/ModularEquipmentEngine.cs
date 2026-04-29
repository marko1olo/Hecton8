namespace Hecton8.Tools
{
    using Hecton8.Core;
    using Hecton8.Gameplay;
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
    public sealed class ModularEquipmentEngine : MonoBehaviour, IModularEquipmentService, IUpdatable
    {
        private const int MaxTrackedTools = 16;

        private static ModularEquipmentEngine _instance;

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
        private NativeHashMap<uint, int> _toolIndexById;
        private bool _isInitialized;
        private bool _registeredService;
        private bool _registeredUpdatable;

        public bool IsInitialized => _isInitialized;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _instance = null;
        }

        public static ModularEquipmentEngine EnsureRuntimeInstance()
        {
            if (_instance != null)
                return _instance;

            GameObject runtimeRoot = new GameObject("[ModularEquipmentEngine]"); // COLD ALLOC: GameObject[1] — bootstrap-owned equipment runtime root — owner: ModularEquipmentEngine
            return runtimeRoot.AddComponent<ModularEquipmentEngine>();
        }

        public void InitializeService()
        {
            if (_isInitialized)
            {
                TryRegisterService();
                TryRegisterUpdatable();
                return;
            }

            EnsureSingletonOwnership();
            if (_instance != this)
                return;

            if (!_toolStates.IsCreated)
            {
                // COLD ALLOC: NativeArray<ToolState>[16] — active modular tool state buffer — owner: ModularEquipmentEngine
                _toolStates = new NativeArray<ToolState>(MaxTrackedTools, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            }

            if (!_toolStats.IsCreated)
            {
                // COLD ALLOC: NativeArray<ToolRuntimeStats>[16] — active compiled tool-stat buffer — owner: ModularEquipmentEngine
                _toolStats = new NativeArray<ToolRuntimeStats>(MaxTrackedTools, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            }

            if (!_toolIndexById.IsCreated)
            {
                // COLD ALLOC: NativeHashMap<uint,int>[16] — tool-id to slot index table — owner: ModularEquipmentEngine
                _toolIndexById = new NativeHashMap<uint, int>(MaxTrackedTools, Allocator.Persistent);
            }

            if (Application.isPlaying && transform.parent != null)
                transform.SetParent(null, true);

            _isInitialized = true;
            TryRegisterService();
            TryRegisterUpdatable();
        }

        public void Tick(float deltaTime)
        {
            if (!_isInitialized)
                return;

            for (int i = 0; i < MaxTrackedTools; i++)
            {
                if (!_slotUsed[i] || _toolOwners[i] == null)
                    continue;

                ToolState state = _toolStates[i];
                state.Durability = math.saturate(_toolOwners[i].DurabilityNormalized);
                _toolStates[i] = state;
            }
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
            nextState.InternalHeat = math.saturate(tool.ResolveModularHeatNormalized());
            nextState.Durability = math.saturate(tool.DurabilityNormalized);
            nextState.UpgradeBitmask = upgradeMask;

            _toolOwners[slotIndex] = tool;
            _slotUsed[slotIndex] = true;
            _toolStates[slotIndex] = nextState;
            _toolStats[slotIndex] = compiledStats;
            _toolIndexById[profile.ToolId] = slotIndex;
            WriteModuleMirror(slotIndex, _registrationModules, moduleSlotCount);
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
            return TryGetToolStats(toolId, out ToolRuntimeStats stats) ? stats.PowerScalar : fallback;
        }

        public float GetEfficiencyScalar(uint toolId, float fallback)
        {
            return TryGetToolStats(toolId, out ToolRuntimeStats stats) ? stats.EfficiencyScalar : fallback;
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

            ToolState state = _toolStates[slotIndex];
            float capacity = math.max(0.1f, _toolStats[slotIndex].BatteryCapacity);
            state.CurrentBattery = math.saturate(normalizedBattery) * capacity;
            _toolStates[slotIndex] = state;
        }

        public float GetBatteryNormalized(uint toolId, float fallback)
        {
            if (!_isInitialized || !_toolIndexById.TryGetValue(toolId, out int slotIndex))
                return fallback;

            float capacity = math.max(0.1f, _toolStats[slotIndex].BatteryCapacity);
            return math.saturate(_toolStates[slotIndex].CurrentBattery / capacity);
        }

        public void ConsumeBattery(uint toolId, float normalizedBatteryDelta)
        {
            if (!_isInitialized || !_toolIndexById.TryGetValue(toolId, out int slotIndex))
                return;

            ToolState state = _toolStates[slotIndex];
            float capacity = math.max(0.1f, _toolStats[slotIndex].BatteryCapacity);
            state.CurrentBattery = math.max(0f, state.CurrentBattery - math.max(0f, normalizedBatteryDelta) * capacity);
            _toolStates[slotIndex] = state;
        }

        public void SetHeat(uint toolId, float normalizedHeat)
        {
            if (!_isInitialized || !_toolIndexById.TryGetValue(toolId, out int slotIndex))
                return;

            ToolState state = _toolStates[slotIndex];
            state.InternalHeat = math.saturate(normalizedHeat);
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

        private void Awake()
        {
            EnsureSingletonOwnership();
        }

        private void OnEnable()
        {
            SceneManager.sceneUnloaded += HandleSceneUnloaded;

            if (_isInitialized)
            {
                TryRegisterService();
                TryRegisterUpdatable();
            }
        }

        private void OnDisable()
        {
            SceneManager.sceneUnloaded -= HandleSceneUnloaded;
            TryUnregisterService();
            TryUnregisterUpdatable();
        }

        private void OnDestroy()
        {
            SceneManager.sceneUnloaded -= HandleSceneUnloaded;
            TryUnregisterService();
            TryUnregisterUpdatable();
            DisposeNativeState();

            if (_instance == this)
                _instance = null;
        }

        private void EnsureSingletonOwnership()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
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
        }

        private void TryRegisterService()
        {
            if (_registeredService)
                return;

            GlobalRegistry.RegisterModularEquipmentService(this);
            _registeredService = true;
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
            if (_registeredUpdatable)
                return;

            GlobalRegistry.RegisterUpdatable(this, PriorityLayer.Core);
            _registeredUpdatable = true;
        }

        private void TryUnregisterUpdatable()
        {
            if (!_registeredUpdatable)
                return;

            GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Core);
            _registeredUpdatable = false;
        }

        private void HandleSceneUnloaded(Scene unloadedScene)
        {
            if (gameObject.scene != unloadedScene)
                return;

            DisposeNativeState();
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
                _toolStates.Dispose();

            if (_toolStats.IsCreated)
                _toolStats.Dispose();

            if (_toolIndexById.IsCreated)
                _toolIndexById.Dispose();

            _isInitialized = false;
        }
    }
}
