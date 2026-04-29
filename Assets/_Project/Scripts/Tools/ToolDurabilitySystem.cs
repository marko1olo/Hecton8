using System;
using System.Collections.Generic;
using Hecton.Localization;
using Hecton8.Bootstrap;
using Hecton8.Core;
using Hecton8.Gameplay;
using Hecton8.Inventory;
using Hecton8.SaveSystem;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Tools
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/Tools/Tool Durability System")]
    public sealed class ToolDurabilitySystem : MonoBehaviour, ISaveable, ISlowTickable, IUpdatable, ILateFrameTickable
    {
        private const int MaxTrackedTools = 32;
        private const float SlowTickDeltaTime = 0.5f;
        private const float UnderwaterDepthThreshold = 0.5f;
        private const float ActiveUseWindowSeconds = 0.7f;
        private const float DegradedThreshold = 0.25f;
        private const ushort DegradedFlag = 1 << 0;
        private const ushort BrokenFlag = 1 << 1;

        public static ToolDurabilitySystem Instance { get; private set; }

        [Header("── Settings ────────────────────────────────")]
        [Tooltip("Enable runtime tool wear processing.")]
        [SerializeField] private bool enableDurabilityDrain = true;

        [Tooltip("Global multiplier applied after authored and template wear.")]
        [Range(0.1f, 2f)]
        [SerializeField] private float globalDurabilityMultiplier = 1f;

        [Tooltip("Automatically mark the tool broken once durability reaches zero.")]
        [SerializeField] private bool autoBreakOnZero = true;

        [Tooltip("Passive corrosion on the currently held tool while the player stays underwater.")]
        [SerializeField] private bool enableEnvironmentalCorrosion = true;

        [Tooltip("Base corrosion per second for a held underwater tool.")]
        [Range(0f, 1f)]
        [SerializeField] private float heldUnderwaterCorrosionPerSecond = 0.04f;

        [Tooltip("Extra corrosion per second when the held underwater tool was used recently.")]
        [Range(0f, 2f)]
        [SerializeField] private float activeUseCorrosionPerSecond = 0.12f;

        [Tooltip("Extra corrosion multiplier applied during cold stress.")]
        [Range(0f, 2f)]
        [SerializeField] private float coldStressCorrosionMultiplier = 0.55f;

        [Tooltip("Extra corrosion multiplier applied during heat stress.")]
        [Range(0f, 2f)]
        [SerializeField] private float heatStressCorrosionMultiplier = 0.35f;

        // COLD ALLOC: Dictionary<string,float>[32] — compatibility durability mirror for UI/save callers — owner: ToolDurabilitySystem
        private readonly Dictionary<string, float> _durabilityMap = new Dictionary<string, float>(MaxTrackedTools);
        // COLD ALLOC: Dictionary<string,bool>[32] — compatibility broken-state mirror for UI/save callers — owner: ToolDurabilitySystem
        private readonly Dictionary<string, bool> _brokenMap = new Dictionary<string, bool>(MaxTrackedTools);
        // COLD ALLOC: Dictionary<string,int>[32] — tool-id to native durability slot mapping — owner: ToolDurabilitySystem
        private readonly Dictionary<string, int> _slotByToolId = new Dictionary<string, int>(MaxTrackedTools);
        // COLD ALLOC: string[32] — native-slot tool-id mirror for event/save fanout — owner: ToolDurabilitySystem
        private readonly string[] _toolIdBySlot = new string[MaxTrackedTools];
        // COLD ALLOC: float[32] — authored max durability mirror aligned to slot state — owner: ToolDurabilitySystem
        private readonly float[] _maxDurabilityBySlot = new float[MaxTrackedTools];
        // COLD ALLOC: uint[32] — item-hash mirror aligned to slot state — owner: ToolDurabilitySystem
        private readonly uint[] _itemHashBySlot = new uint[MaxTrackedTools];
        // COLD ALLOC: bool[32] — managed slot occupancy mirror — owner: ToolDurabilitySystem
        private readonly bool[] _slotUsed = new bool[MaxTrackedTools];

        private NativeArray<ItemState> _itemStates;
        private NativeArray<float> _pendingDecayDt;
        private NativeArray<float> _wearMultipliers;
        private NativeArray<byte> _slotActive;
        private NativeQueue<BreakdownEvent> _breakdownEvents;
        private JobHandle _scheduledDecayHandle;
        private bool _decayScheduled;

        private HectonSurvivalSystem _playerSurvivalSystem;
        private PlayerToolManager _playerToolManager;
        private Transform _playerRoot;
        private bool _registeredSlowTick;
        private bool _registeredUpdate;
        private bool _registeredLateFrame;
        private bool _saveRegistered;

        public event Action<string, float, float> OnDurabilityChanged;
        public event Action<string> OnToolBroken;
        public event Action<string, float> OnToolRepaired;

        public int SavePriority => 20;
        public int LoadPriority => 20;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            Instance = null;
        }

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Low)]
        private struct DurabilityDecayJob : IJobParallelFor
        {
            public NativeArray<ItemState> States;
            public NativeArray<float> PendingDecayDt;
            [ReadOnly] public NativeArray<float> WearMultipliers;
            [ReadOnly] public NativeArray<byte> SlotActive;
            public NativeQueue<BreakdownEvent>.ParallelWriter BreakdownWriter;

            public void Execute(int index)
            {
                if (SlotActive[index] == 0)
                {
                    PendingDecayDt[index] = 0f;
                    return;
                }

                float dt = PendingDecayDt[index];
                PendingDecayDt[index] = 0f;
                if (dt <= 0f)
                    return;

                ItemState state = States[index];
                DURABILITY_DECAY(ref state, dt, state.hashID, WearMultipliers[index], BreakdownWriter, index);
                States[index] = state;
            }

            private static void DURABILITY_DECAY(
                ref ItemState state,
                float dt,
                uint hashID,
                float wearMultiplier,
                NativeQueue<BreakdownEvent>.ParallelWriter breakdownWriter,
                int slotIndex)
            {
                float decay = math.max(0f, dt) * math.max(0f, wearMultiplier);
                state.durability = math.saturate(state.durability - decay);

                if (state.durability < DegradedThreshold)
                    state.flags |= DegradedFlag;
                else
                    state.flags &= unchecked((ushort)~DegradedFlag);

                if (state.durability <= 0f)
                {
                    if ((state.flags & BrokenFlag) == 0)
                    {
                        state.flags |= BrokenFlag;
                        breakdownWriter.Enqueue(new BreakdownEvent
                        {
                            SlotIndex = slotIndex,
                            HashId = hashID
                        });
                    }
                }
                else
                {
                    state.flags &= unchecked((ushort)~BrokenFlag);
                }
            }
        }

        private struct BreakdownEvent
        {
            public int SlotIndex;
            public uint HashId;
        }

        private struct ItemState
        {
            public float durability;
            public uint hashID;
            public ushort flags;
            public ushort reserved;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            EnsureNativeState();
        }

        private void OnEnable()
        {
            TryRegisterSlowTick();
            TryRegisterUpdate();
            TryRegisterLateFrame();
            TryRegisterSaveService();
        }

        private void Start()
        {
            TryRegisterSlowTick();
            TryRegisterUpdate();
            TryRegisterLateFrame();
            TryRegisterSaveService();
        }

        private void OnDisable()
        {
            TryUnregisterLateFrame();
            TryUnregisterUpdate();
            TryUnregisterSlowTick();
            TryUnregisterSaveService();
        }

        private void OnDestroy()
        {
            DisposeNativeState();

            if (Instance == this)
                Instance = null;
        }

        public void Tick(float deltaTime)
        {
            TryRegisterSaveService();
            if (!enableDurabilityDrain || !_itemStates.IsCreated || _decayScheduled || !HasPendingDecay())
                return;

            DurabilityDecayJob decayJob = new DurabilityDecayJob
            {
                States = _itemStates,
                PendingDecayDt = _pendingDecayDt,
                WearMultipliers = _wearMultipliers,
                SlotActive = _slotActive,
                BreakdownWriter = _breakdownEvents.AsParallelWriter()
            };

            _scheduledDecayHandle = decayJob.Schedule(MaxTrackedTools, 8);
            _decayScheduled = true;
        }

        public void LateFrameTick()
        {
            if (!_decayScheduled)
                return;

            _scheduledDecayHandle.Complete();
            _decayScheduled = false;
            SyncManagedMirrorsFromNative();
            FlushBreakdownEvents();
        }

        public void SlowTick()
        {
            ApplyEnvironmentalCorrosion();
        }

        public float GetDurability(string toolID, float maxDurability)
        {
            if (string.IsNullOrEmpty(toolID))
                return Mathf.Max(0f, maxDurability);

            if (_durabilityMap.TryGetValue(toolID, out float current))
                return current;

            EnsureToolRegistered(toolID, unchecked((uint)Animator.StringToHash(toolID)), maxDurability);
            return Mathf.Max(0f, maxDurability);
        }

        public float GetDurabilityNormalized(string toolID, float maxDurability)
        {
            float current = GetDurability(toolID, maxDurability);
            return Mathf.Clamp01(current / Mathf.Max(1f, maxDurability));
        }

        public bool IsBroken(string toolID)
        {
            if (string.IsNullOrEmpty(toolID))
                return false;

            return _brokenMap.TryGetValue(toolID, out bool broken) && broken;
        }

        public bool IsDegraded(string toolID)
        {
            if (string.IsNullOrEmpty(toolID))
                return false;

            int slotIndex = ResolveSlot(toolID);
            if (slotIndex < 0 || !_itemStates.IsCreated)
                return false;

            return (_itemStates[slotIndex].flags & DegradedFlag) != 0;
        }

        public void DrainDurability(string toolID, float amount, float maxDurability)
        {
            if (!enableDurabilityDrain || amount <= 0f)
                return;

            int slotIndex = EnsureToolRegistered(toolID, unchecked((uint)Animator.StringToHash(toolID)), maxDurability);
            if (slotIndex < 0)
                return;

            _pendingDecayDt[slotIndex] += (amount / Mathf.Max(1f, maxDurability)) * Mathf.Max(0.1f, globalDurabilityMultiplier);
        }

        public void DrainDurabilityByTime(string toolID, uint itemHashId, float scaledDeltaTime, float maxDurability)
        {
            if (!enableDurabilityDrain || scaledDeltaTime <= 0f)
                return;

            int slotIndex = EnsureToolRegistered(toolID, itemHashId, maxDurability);
            if (slotIndex < 0)
                return;

            _pendingDecayDt[slotIndex] += scaledDeltaTime * Mathf.Max(0.1f, globalDurabilityMultiplier);
        }

        public void RepairTool(string toolID, float amount, float maxDurability)
        {
            if (string.IsNullOrEmpty(toolID) || amount <= 0f)
                return;

            CompleteDecayJobIfScheduled();

            int existingSlot = ResolveSlot(toolID);
            uint hashId = existingSlot >= 0 ? _itemHashBySlot[existingSlot] : unchecked((uint)Animator.StringToHash(toolID));
            int slotIndex = EnsureToolRegistered(toolID, hashId, maxDurability);
            if (slotIndex < 0)
                return;

            ItemState state = _itemStates[slotIndex];
            state.durability = math.saturate(state.durability + (amount / Mathf.Max(1f, maxDurability)));
            if (state.durability >= DegradedThreshold)
                state.flags &= unchecked((ushort)~DegradedFlag);

            state.flags &= unchecked((ushort)~BrokenFlag);
            _itemStates[slotIndex] = state;
            _pendingDecayDt[slotIndex] = 0f;

            float repairedDurability = state.durability * Mathf.Max(1f, maxDurability);
            _durabilityMap[toolID] = repairedDurability;
            _brokenMap[toolID] = false;
            OnToolRepaired?.Invoke(toolID, repairedDurability);
            OnDurabilityChanged?.Invoke(toolID, repairedDurability, Mathf.Max(1f, maxDurability));
        }

        public void RepairToolFull(string toolID, float maxDurability)
        {
            RepairTool(toolID, Mathf.Max(1f, maxDurability), maxDurability);
        }

        public void BreakTool(string toolID)
        {
            if (string.IsNullOrEmpty(toolID))
                return;

            CompleteDecayJobIfScheduled();

            int existingSlot = ResolveSlot(toolID);
            uint hashId = existingSlot >= 0 ? _itemHashBySlot[existingSlot] : unchecked((uint)Animator.StringToHash(toolID));
            int slotIndex = EnsureToolRegistered(toolID, hashId, 1f);
            if (slotIndex < 0)
                return;

            if ((_itemStates[slotIndex].flags & BrokenFlag) != 0)
                return;

            ItemState state = _itemStates[slotIndex];
            state.durability = 0f;
            state.flags |= (ushort)(BrokenFlag | DegradedFlag);
            _itemStates[slotIndex] = state;
            _durabilityMap[toolID] = 0f;
            _brokenMap[toolID] = true;
            OnDurabilityChanged?.Invoke(toolID, 0f, Mathf.Max(1f, _maxDurabilityBySlot[slotIndex]));
            OnToolBroken?.Invoke(toolID);
        }

        public void ResetDurability(string toolID, float maxDurability)
        {
            if (string.IsNullOrEmpty(toolID))
                return;

            CompleteDecayJobIfScheduled();

            int existingSlot = ResolveSlot(toolID);
            uint hashId = existingSlot >= 0 ? _itemHashBySlot[existingSlot] : unchecked((uint)Animator.StringToHash(toolID));
            int slotIndex = EnsureToolRegistered(toolID, hashId, maxDurability);
            if (slotIndex < 0)
                return;

            ItemState state = _itemStates[slotIndex];
            state.durability = 1f;
            state.flags = 0;
            _itemStates[slotIndex] = state;
            _pendingDecayDt[slotIndex] = 0f;

            float resolvedMax = Mathf.Max(1f, maxDurability);
            _durabilityMap[toolID] = resolvedMax;
            _brokenMap[toolID] = false;
            OnDurabilityChanged?.Invoke(toolID, resolvedMax, resolvedMax);
        }

        public void PopulateSaveData(SaveData data)
        {
            if (data == null)
                return;

            CompleteDecayJobIfScheduled();

            data.toolDurabilityMap.Clear();
            data.toolBrokenMap.Clear();

            Dictionary<string, float>.Enumerator durabilityEnumerator = _durabilityMap.GetEnumerator();
            while (durabilityEnumerator.MoveNext())
            {
                KeyValuePair<string, float> pair = durabilityEnumerator.Current;
                data.toolDurabilityMap[pair.Key] = pair.Value;
            }

            Dictionary<string, bool>.Enumerator brokenEnumerator = _brokenMap.GetEnumerator();
            while (brokenEnumerator.MoveNext())
            {
                KeyValuePair<string, bool> pair = brokenEnumerator.Current;
                if (pair.Value)
                    data.toolBrokenMap[pair.Key] = true;
            }
        }

        public void LoadFromSaveData(SaveData data)
        {
            CompleteDecayJobIfScheduled();
            ClearRuntimeState();

            if (data == null)
                return;

            Dictionary<string, float>.Enumerator durabilityEnumerator = data.toolDurabilityMap.GetEnumerator();
            while (durabilityEnumerator.MoveNext())
            {
                KeyValuePair<string, float> pair = durabilityEnumerator.Current;
                float resolvedMaxDurability = Mathf.Max(1f, pair.Value);
                int slotIndex = EnsureToolRegistered(pair.Key, unchecked((uint)Animator.StringToHash(pair.Key)), resolvedMaxDurability);
                if (slotIndex < 0)
                    continue;

                float normalized = Mathf.Clamp01(pair.Value / resolvedMaxDurability);
                ItemState state = _itemStates[slotIndex];
                state.durability = normalized;
                state.flags = normalized < DegradedThreshold ? DegradedFlag : (ushort)0;
                _itemStates[slotIndex] = state;
                _durabilityMap[pair.Key] = pair.Value;
            }

            Dictionary<string, bool>.Enumerator brokenEnumerator = data.toolBrokenMap.GetEnumerator();
            while (brokenEnumerator.MoveNext())
            {
                KeyValuePair<string, bool> pair = brokenEnumerator.Current;
                if (!pair.Value)
                    continue;

                int slotIndex = ResolveSlot(pair.Key);
                if (slotIndex < 0)
                    slotIndex = EnsureToolRegistered(pair.Key, unchecked((uint)Animator.StringToHash(pair.Key)), 1f);

                if (slotIndex < 0)
                    continue;

                ItemState state = _itemStates[slotIndex];
                state.flags |= (ushort)(BrokenFlag | DegradedFlag);
                state.durability = 0f;
                _itemStates[slotIndex] = state;
                _durabilityMap[pair.Key] = 0f;
                _brokenMap[pair.Key] = true;
            }

            SyncManagedMirrorsFromNative();
        }

        private void ApplyEnvironmentalCorrosion()
        {
            if (!enableEnvironmentalCorrosion || !ResolvePlayerOwners())
                return;

            if (_playerSurvivalSystem == null || _playerToolManager == null || _playerSurvivalSystem.Depth <= UnderwaterDepthThreshold)
                return;

            PlayerTool currentTool = _playerToolManager.CurrentTool;
            if (currentTool == null || !currentTool.IsEquipped || currentTool.IsBroken)
                return;

            ToolMetadata metadata = currentTool.RuntimeMetadata;
            if (metadata == null || string.IsNullOrEmpty(metadata.toolID))
                return;

            float scaledDeltaTime = heldUnderwaterCorrosionPerSecond * SlowTickDeltaTime;
            if (currentTool.WasRecentlyUsed(ActiveUseWindowSeconds))
                scaledDeltaTime += activeUseCorrosionPerSecond * SlowTickDeltaTime;

            if (_playerSurvivalSystem.IsInColdStress)
                scaledDeltaTime *= 1f + (_playerSurvivalSystem.ColdStressSeverity01 * coldStressCorrosionMultiplier);

            if (_playerSurvivalSystem.IsInHeatStress)
                scaledDeltaTime *= 1f + (_playerSurvivalSystem.HeatStressSeverity01 * heatStressCorrosionMultiplier);

            if (scaledDeltaTime <= 0.0001f)
                return;

            uint itemHashId = currentTool.ToolData != null
                ? unchecked((uint)LocHash.Compute(currentTool.ToolData.PersistentId))
                : unchecked((uint)Animator.StringToHash(metadata.toolID));
            DrainDurabilityByTime(metadata.toolID, itemHashId, scaledDeltaTime, metadata.maxDurability);
        }

        private bool ResolvePlayerOwners()
        {
            if (_playerRoot == null)
            {
                if (!SceneBootstrap.TryGetCurrentPlayerTransform(out _playerRoot) || _playerRoot == null)
                    return false;
            }

            if (_playerSurvivalSystem == null)
                _playerRoot.TryGetComponent(out _playerSurvivalSystem);

            if (_playerToolManager == null)
                _playerToolManager = GlobalRegistry.Player != null && GlobalRegistry.Player.ToolManager != null
                    ? GlobalRegistry.Player.ToolManager
                    : _playerRoot.GetComponent<PlayerToolManager>();

            return _playerSurvivalSystem != null && _playerToolManager != null;
        }

        private void EnsureNativeState()
        {
            if (!_itemStates.IsCreated)
                _itemStates = new NativeArray<ItemState>(MaxTrackedTools, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<ItemState>[32] — authoritative tool durability slots — owner: ToolDurabilitySystem

            if (!_pendingDecayDt.IsCreated)
                _pendingDecayDt = new NativeArray<float>(MaxTrackedTools, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<float>[32] — pending scaled durability-decay dt per slot — owner: ToolDurabilitySystem

            if (!_wearMultipliers.IsCreated)
                _wearMultipliers = new NativeArray<float>(MaxTrackedTools, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<float>[32] — compiled ItemTemplate wear multipliers per slot — owner: ToolDurabilitySystem

            if (!_slotActive.IsCreated)
                _slotActive = new NativeArray<byte>(MaxTrackedTools, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<byte>[32] — native slot occupancy mask for durability jobs — owner: ToolDurabilitySystem

            if (!_breakdownEvents.IsCreated)
                _breakdownEvents = new NativeQueue<BreakdownEvent>(Allocator.Persistent); // COLD ALLOC: NativeQueue<BreakdownEvent>(Persistent) — deferred tool-breakdown event lane — owner: ToolDurabilitySystem
        }

        private void DisposeNativeState()
        {
            if (_decayScheduled)
            {
                JobHandle disposeHandle = _scheduledDecayHandle;

                if (_itemStates.IsCreated)
                    disposeHandle = _itemStates.Dispose(disposeHandle);

                if (_pendingDecayDt.IsCreated)
                    disposeHandle = _pendingDecayDt.Dispose(disposeHandle);

                if (_wearMultipliers.IsCreated)
                    disposeHandle = _wearMultipliers.Dispose(disposeHandle);

                if (_slotActive.IsCreated)
                    disposeHandle = _slotActive.Dispose(disposeHandle);

                if (_breakdownEvents.IsCreated)
                    disposeHandle = _breakdownEvents.Dispose(disposeHandle);
            }
            else
            {
                if (_itemStates.IsCreated)
                    _itemStates.Dispose();

                if (_pendingDecayDt.IsCreated)
                    _pendingDecayDt.Dispose();

                if (_wearMultipliers.IsCreated)
                    _wearMultipliers.Dispose();

                if (_slotActive.IsCreated)
                    _slotActive.Dispose();

                if (_breakdownEvents.IsCreated)
                    _breakdownEvents.Dispose();
            }

            _itemStates = default;
            _pendingDecayDt = default;
            _wearMultipliers = default;
            _slotActive = default;
            _breakdownEvents = default;
            _scheduledDecayHandle = default;
            _decayScheduled = false;
        }

        private void ClearRuntimeState()
        {
            EnsureNativeState();

            _durabilityMap.Clear();
            _brokenMap.Clear();
            _slotByToolId.Clear();

            for (int i = 0; i < MaxTrackedTools; i++)
            {
                _toolIdBySlot[i] = null;
                _maxDurabilityBySlot[i] = 0f;
                _itemHashBySlot[i] = 0u;
                _slotUsed[i] = false;
                _itemStates[i] = default;
                _pendingDecayDt[i] = 0f;
                _wearMultipliers[i] = 0f;
                _slotActive[i] = 0;
            }

            while (_breakdownEvents.IsCreated && _breakdownEvents.TryDequeue(out _))
            {
            }
        }

        private int EnsureToolRegistered(string toolID, uint itemHashId, float maxDurability)
        {
            if (string.IsNullOrEmpty(toolID))
                return -1;

            EnsureNativeState();

            if (_slotByToolId.TryGetValue(toolID, out int existingSlot))
            {
                UpdateSlotMetadata(existingSlot, toolID, itemHashId, maxDurability);
                return existingSlot;
            }

            for (int i = 0; i < MaxTrackedTools; i++)
            {
                if (_slotUsed[i])
                    continue;

                _slotUsed[i] = true;
                _slotByToolId[toolID] = i;
                _toolIdBySlot[i] = toolID;
                UpdateSlotMetadata(i, toolID, itemHashId, maxDurability);

                ItemState state = new ItemState
                {
                    durability = 1f,
                    hashID = itemHashId
                };
                _itemStates[i] = state;
                _durabilityMap[toolID] = Mathf.Max(1f, maxDurability);
                _brokenMap[toolID] = false;
                return i;
            }

            return -1;
        }

        private void UpdateSlotMetadata(int slotIndex, string toolID, uint itemHashId, float maxDurability)
        {
            float resolvedMaxDurability = Mathf.Max(1f, maxDurability);
            _toolIdBySlot[slotIndex] = toolID;
            _maxDurabilityBySlot[slotIndex] = resolvedMaxDurability;
            _itemHashBySlot[slotIndex] = itemHashId;
            _wearMultipliers[slotIndex] = ResolveWearMultiplier(itemHashId);
            _slotActive[slotIndex] = 1;

            ItemState state = _itemStates[slotIndex];
            state.hashID = itemHashId;
            _itemStates[slotIndex] = state;

            if (!_durabilityMap.ContainsKey(toolID))
                _durabilityMap[toolID] = resolvedMaxDurability;

            if (!_brokenMap.ContainsKey(toolID))
                _brokenMap[toolID] = false;
        }

        private static float ResolveWearMultiplier(uint itemHashId)
        {
            if (itemHashId != 0u && ItemTemplateRegistry.TryGetTemplate(itemHashId, out ItemTemplate template))
                return Mathf.Max(0f, template.WearMultiplier);

            return 1f;
        }

        private int ResolveSlot(string toolID)
        {
            return !string.IsNullOrEmpty(toolID) && _slotByToolId.TryGetValue(toolID, out int slotIndex)
                ? slotIndex
                : -1;
        }

        private bool HasPendingDecay()
        {
            if (!_pendingDecayDt.IsCreated)
                return false;

            for (int i = 0; i < MaxTrackedTools; i++)
            {
                if (_slotUsed[i] && _pendingDecayDt[i] > 0f)
                    return true;
            }

            return false;
        }

        private void SyncManagedMirrorsFromNative()
        {
            if (!_itemStates.IsCreated)
                return;

            for (int i = 0; i < MaxTrackedTools; i++)
            {
                if (!_slotUsed[i])
                    continue;

                string toolId = _toolIdBySlot[i];
                if (string.IsNullOrEmpty(toolId))
                    continue;

                ItemState state = _itemStates[i];
                float maxDurability = Mathf.Max(1f, _maxDurabilityBySlot[i]);
                float currentDurability = math.saturate(state.durability) * maxDurability;
                bool broken = autoBreakOnZero && (state.flags & BrokenFlag) != 0;

                _durabilityMap.TryGetValue(toolId, out float previousDurability);
                _brokenMap.TryGetValue(toolId, out bool previousBroken);

                _durabilityMap[toolId] = currentDurability;
                _brokenMap[toolId] = broken;

                if (math.abs(previousDurability - currentDurability) > 0.0001f || previousBroken != broken)
                    OnDurabilityChanged?.Invoke(toolId, currentDurability, maxDurability);
            }
        }

        private void FlushBreakdownEvents()
        {
            while (_breakdownEvents.IsCreated && _breakdownEvents.TryDequeue(out BreakdownEvent breakdown))
            {
                if ((uint)breakdown.SlotIndex >= (uint)_toolIdBySlot.Length)
                    continue;

                string toolId = _toolIdBySlot[breakdown.SlotIndex];
                if (!string.IsNullOrEmpty(toolId))
                    OnToolBroken?.Invoke(toolId);
            }
        }

        private void CompleteDecayJobIfScheduled()
        {
            if (!_decayScheduled)
                return;

            _scheduledDecayHandle.Complete();
            _decayScheduled = false;
            SyncManagedMirrorsFromNative();
            FlushBreakdownEvents();
        }

        private void TryRegisterSlowTick()
        {
            if (_registeredSlowTick)
                return;

            GlobalRegistry.RegisterSlowTickable(this, PriorityLayer.Player);
            _registeredSlowTick = true;
        }

        private void TryUnregisterSlowTick()
        {
            if (!_registeredSlowTick)
                return;

            GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Player);
            _registeredSlowTick = false;
        }

        private void TryRegisterUpdate()
        {
            if (_registeredUpdate)
                return;

            GlobalRegistry.RegisterUpdatable(this, PriorityLayer.Player);
            _registeredUpdate = true;
        }

        private void TryUnregisterUpdate()
        {
            if (!_registeredUpdate)
                return;

            GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Player);
            _registeredUpdate = false;
        }

        private void TryRegisterLateFrame()
        {
            if (_registeredLateFrame)
                return;

            GlobalRegistry.RegisterLateFrameTickable(this, PriorityLayer.Player);
            _registeredLateFrame = true;
        }

        private void TryUnregisterLateFrame()
        {
            if (!_registeredLateFrame)
                return;

            GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Player);
            _registeredLateFrame = false;
        }

        private void TryRegisterSaveService()
        {
            if (_saveRegistered || GlobalRegistry.Save == null)
                return;

            GlobalRegistry.Save.Register(this);
            _saveRegistered = true;
        }

        private void TryUnregisterSaveService()
        {
            if (!_saveRegistered || GlobalRegistry.Save == null)
                return;

            GlobalRegistry.Save.Unregister(this);
            _saveRegistered = false;
        }
    }
}
