using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Hecton.Localization;
using Hecton8.Bootstrap;
using Hecton8.Core;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Memory;
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
    public sealed class ToolDurabilitySystem : MonoBehaviour, ISaveable, ISlowTickable, IUpdatable, ILateFrameTickable, IGlobalRegistryHotSwapListener, IGlobalRegistryHotSwapRefListener
    {
        private const int MaxTrackedTools = 32;
        private const int MaxQueuedDurabilityCommands = 32;
        private const float SlowTickDeltaTime = 0.5f;
        private const float UnderwaterDepthThreshold = 0.5f;
        private const float ActiveUseWindowSeconds = 0.7f;
        private const float DegradedThreshold = 0.25f;
        private const float BrineCorrosionPerSecond = 0.05f;
        private const float BrineDensityThresholdKgPerCubicMeter = 1249f;
        private const ushort DegradedFlag = 1 << 0;
        private const ushort BrokenFlag = 1 << 1;

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
        // COLD ALLOC: PendingDurabilityCommand[32] - one-frame native-job mutation queue - owner: ToolDurabilitySystem
        private readonly PendingDurabilityCommand[] _queuedDurabilityCommands = new PendingDurabilityCommand[MaxQueuedDurabilityCommands];
        private VaultGenerationHandle<ItemState> _itemStatesHandle;
        private VaultGenerationHandle<float> _pendingDecayDtHandle;
        private VaultGenerationHandle<float> _wearMultipliersHandle;
        private VaultGenerationHandle<byte> _slotActiveHandle;
        private VaultGenerationHandle<byte> _breakdownFlagsHandle;
        private JobHandle _scheduledDecayHandle;
        private int _queuedDurabilityCommandCount;
        private bool _decayScheduled;

        private HectonSurvivalSystem _playerSurvivalSystem;
        private PlayerToolManager _playerToolManager;
        private Transform _playerRoot;
        private bool _registeredSlowTick;
        private bool _registeredUpdate;
        private bool _registeredLateFrame;
        private bool _saveRegistered;
        private bool _serviceRegistered;
        private bool _managedMirrorDirty;
        private bool _registeredHotSwap;
        private IDataVault _dataVault;
        private ISaveService _saveService;
        private IPlayerRuntimeContext _playerRuntimeContext;

        public int SavePriority => 20;
        public int LoadPriority => 20;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
        private struct DurabilityDecayJob : IJobParallelFor
        {
            [NoAlias] public NativeArray<ItemState> States;
            [NoAlias] public NativeArray<float> PendingDecayDt;
            [NoAlias] [ReadOnly] public NativeArray<float> WearMultipliers;
            [NoAlias] [ReadOnly] public NativeArray<byte> SlotActive;
            [NoAlias] public NativeArray<byte> BreakdownFlags;

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
                DURABILITY_DECAY(ref state, dt, WearMultipliers[index], BreakdownFlags, index);
                States[index] = state;
            }

            private static void DURABILITY_DECAY(
                ref ItemState state,
                float dt,
                float wearMultiplier,
                NativeArray<byte> breakdownFlags,
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
                        breakdownFlags[slotIndex] = 1;
                    }
                }
                else
                {
                    state.flags &= unchecked((ushort)~BrokenFlag);
                }
            }
        }

        private enum DurabilityCommandKind : byte
        {
            Repair = 1,
            Break = 2,
            Reset = 3,
            Drain = 4,
            DrainByTime = 5
        }

#pragma warning disable 0649 // Assigned through object initializers before queued drain; compiler does not track array-backed command staging.
        [StructLayout(LayoutKind.Sequential)]
        private struct PendingDurabilityCommand
        {
            public string ToolId;
            public float Amount;
            public float MaxDurability;
            public uint ItemHashId;
            public DurabilityCommandKind Kind;
        }
#pragma warning restore 0649

#pragma warning disable 0649 // Reserved padding keeps native item-state layout stable for future flags.
        [StructLayout(LayoutKind.Sequential)]
        private struct ItemState
        {
            public float durability;
            public uint hashID;
            public ushort flags;
            public ushort reserved;
        }
#pragma warning restore 0649

        private void Awake()
        {
            ToolDurabilitySystem registered = GlobalRegistry.ToolDurability;
            if (registered != null && registered != this)
            {
                Destroy(gameObject);
                return;
            }

            CacheRegistryDependenciesCold();
            EnsureNativeState();
        }

        private void OnEnable()
        {
            CacheRegistryDependenciesCold();
            TryRegisterHotSwap();
            TryRegisterService();
            TryRegisterSlowTick();
            TryRegisterUpdate();
            TryRegisterSaveService();
        }

        private void Start()
        {
            CacheRegistryDependenciesCold();
            TryRegisterHotSwap();
            TryRegisterService();
            TryRegisterSlowTick();
            TryRegisterUpdate();
            TryRegisterSaveService();
        }

        private void OnDisable()
        {
            TryUnregisterLateFrame();
            TryUnregisterUpdate();
            TryUnregisterSlowTick();
            TryUnregisterSaveService();
            TryUnregisterService();
            TryUnregisterHotSwap();
        }

        private void OnDestroy()
        {
            TryUnregisterService();
            TryUnregisterHotSwap();
            DisposeNativeState();
        }

        public void Tick(float deltaTime)
        {
            TryRegisterSaveService();
            if (!TryResolveNativeState(
                    out NativeArray<ItemState> itemStates,
                    out NativeArray<float> pendingDecayDt,
                    out NativeArray<float> wearMultipliers,
                    out NativeArray<byte> slotActive,
                    out NativeArray<byte> breakdownFlags) ||
                !enableDurabilityDrain ||
                _decayScheduled ||
                !HasPendingDecay())
            {
                return;
            }

            DurabilityDecayJob decayJob = new DurabilityDecayJob
            {
                States = itemStates,
                PendingDecayDt = pendingDecayDt,
                WearMultipliers = wearMultipliers,
                SlotActive = slotActive,
                BreakdownFlags = breakdownFlags
            };

            _scheduledDecayHandle = decayJob.Schedule(MaxTrackedTools, 8);
            _decayScheduled = true;
            _managedMirrorDirty = true;
            TryRegisterLateFrame();
        }

        public void LateFrameTick()
        {
            if (!_decayScheduled)
            {
                DrainQueuedDurabilityCommands();
                TryUnregisterLateFrameIfIdle();
                return;
            }

            if (!DispatcherJobSwap.TryComplete(ref _scheduledDecayHandle, forceComplete: false))
                return;

            _decayScheduled = false;
            _managedMirrorDirty = true;
            SyncManagedMirrorsFromNative();
            FlushBreakdownEvents();
            DrainQueuedDurabilityCommands();
            TryUnregisterLateFrameIfIdle();
        }

        public void SlowTick()
        {
            ApplyEnvironmentalCorrosion();
        }

        public float GetDurability(string toolID, float maxDurability)
        {
            if (string.IsNullOrEmpty(toolID))
                return ClampFiniteNonNegative(maxDurability);

            TryCompleteDecayJobIfScheduled(forceComplete: false);
            if (_durabilityMap.TryGetValue(toolID, out float current))
                return current;

            EnsureToolRegistered(toolID, unchecked((uint)Animator.StringToHash(toolID)), maxDurability);
            return ClampFiniteNonNegative(maxDurability);
        }

        public float GetDurabilityNormalized(string toolID, float maxDurability)
        {
            float current = ClampFiniteNonNegative(GetDurability(toolID, maxDurability));
            return math.saturate(current / ResolveSafeMaxDurability(maxDurability));
        }

        public bool IsBroken(string toolID)
        {
            if (string.IsNullOrEmpty(toolID))
                return false;

            TryCompleteDecayJobIfScheduled(forceComplete: false);
            return _brokenMap.TryGetValue(toolID, out bool broken) && broken;
        }

        public bool IsDegraded(string toolID)
        {
            if (string.IsNullOrEmpty(toolID))
                return false;

            int slotIndex = ResolveSlot(toolID);
            if (slotIndex < 0 || !TryResolveItemStates(out NativeArray<ItemState> itemStates))
                return false;

            if (!TryCompleteDecayJobIfScheduled(forceComplete: false))
            {
                float maxDurability = math.max(1f, _maxDurabilityBySlot[slotIndex]);
                return _durabilityMap.TryGetValue(toolID, out float current) &&
                       current <= maxDurability * DegradedThreshold;
            }

            return (itemStates[slotIndex].flags & DegradedFlag) != 0;
        }

        public void DrainDurability(string toolID, float amount, float maxDurability)
        {
            if (!enableDurabilityDrain || string.IsNullOrEmpty(toolID) || !TryResolvePositiveDurabilityAmount(amount, out float safeAmount))
                return;

            float safeMaxDurability = ResolveSafeMaxDurability(maxDurability);
            if (!TryCompleteDecayJobIfScheduled(forceComplete: false))
            {
                QueueDurabilityCommand(DurabilityCommandKind.Drain, toolID, safeAmount, safeMaxDurability, unchecked((uint)Animator.StringToHash(toolID)));
                return;
            }

            ApplyDrainDurability(toolID, safeAmount, safeMaxDurability);
        }

        private void ApplyDrainDurability(string toolID, float amount, float maxDurability)
        {
            float safeAmount = ClampFiniteNonNegative(amount);
            float safeMaxDurability = ResolveSafeMaxDurability(maxDurability);
            if (safeAmount <= 0f)
                return;

            int slotIndex = EnsureToolRegistered(toolID, unchecked((uint)Animator.StringToHash(toolID)), safeMaxDurability);
            if (slotIndex < 0)
                return;

            if (TryResolvePendingDecay(out NativeArray<float> pendingDecayDt))
                pendingDecayDt[slotIndex] += (safeAmount / safeMaxDurability) * math.max(0.1f, globalDurabilityMultiplier);
        }

        public void DrainDurabilityByTime(string toolID, uint itemHashId, float scaledDeltaTime, float maxDurability)
        {
            if (!enableDurabilityDrain || string.IsNullOrEmpty(toolID) || !TryResolvePositiveDurabilityAmount(scaledDeltaTime, out float safeScaledDeltaTime))
                return;

            float safeMaxDurability = ResolveSafeMaxDurability(maxDurability);
            if (!TryCompleteDecayJobIfScheduled(forceComplete: false))
            {
                QueueDurabilityCommand(DurabilityCommandKind.DrainByTime, toolID, safeScaledDeltaTime, safeMaxDurability, itemHashId);
                return;
            }

            ApplyDrainDurabilityByTime(toolID, itemHashId, safeScaledDeltaTime, safeMaxDurability);
        }

        public void RegisterCentralizedEquipmentMirror(string toolID, uint itemHashId, float maxDurability)
        {
            if (string.IsNullOrEmpty(toolID))
                return;

            if (!TryCompleteDecayJobIfScheduled(forceComplete: false))
                return;

            EnsureToolRegistered(toolID, itemHashId, ResolveSafeMaxDurability(maxDurability));
        }

        public float ResolveCentralizedEquipmentWearMultiplier(uint itemHashId)
        {
            if (!enableDurabilityDrain)
                return 0f;

            return math.max(0.1f, globalDurabilityMultiplier) * ResolveWearMultiplier(itemHashId);
        }

        public void SetDurabilityNormalizedFromEquipment(string toolID, uint itemHashId, float normalizedDurability, float maxDurability)
        {
            if (string.IsNullOrEmpty(toolID))
                return;

            if (!TryCompleteDecayJobIfScheduled(forceComplete: false))
                return;

            ApplySetDurabilityNormalizedFromEquipment(toolID, itemHashId, normalizedDurability, maxDurability);
        }

        private void ApplySetDurabilityNormalizedFromEquipment(string toolID, uint itemHashId, float normalizedDurability, float maxDurability)
        {
            float safeMaxDurability = ResolveSafeMaxDurability(maxDurability);
            int slotIndex = EnsureToolRegistered(toolID, itemHashId, safeMaxDurability);
            if (slotIndex < 0 ||
                !TryResolveItemStates(out NativeArray<ItemState> itemStates) ||
                !TryResolvePendingDecay(out NativeArray<float> pendingDecayDt))
            {
                return;
            }

            ItemState state = itemStates[slotIndex];
            float previousNormalized = math.isfinite(state.durability) ? math.saturate(state.durability) : 1f;
            bool previousBroken = (state.flags & BrokenFlag) != 0;
            float safeNormalized = math.saturate(math.isfinite(normalizedDurability) ? normalizedDurability : previousNormalized);
            pendingDecayDt[slotIndex] = 0f;
            if (math.abs(previousNormalized - safeNormalized) <= 0.0001f)
                return;

            state.durability = safeNormalized;
            if (state.durability < DegradedThreshold)
                state.flags |= DegradedFlag;
            else
                state.flags &= unchecked((ushort)~DegradedFlag);

            if (autoBreakOnZero && state.durability <= 0f)
                state.flags |= BrokenFlag;
            else
                state.flags &= unchecked((ushort)~BrokenFlag);

            itemStates[slotIndex] = state;

            float currentDurability = state.durability * safeMaxDurability;
            _durabilityMap[toolID] = currentDurability;
            _brokenMap[toolID] = (state.flags & BrokenFlag) != 0;

            byte reason = !previousBroken && _brokenMap[toolID]
                ? ItemDurabilityChangedSignal.ReasonBreak
                : (safeNormalized > previousNormalized ? ItemDurabilityChangedSignal.ReasonRepair : ItemDurabilityChangedSignal.ReasonCorrosion);
            PublishDurabilityChangedSignal(slotIndex, currentDurability, safeMaxDurability, reason);
        }

        private void ApplyDrainDurabilityByTime(string toolID, uint itemHashId, float scaledDeltaTime, float maxDurability)
        {
            float safeScaledDeltaTime = ClampFiniteNonNegative(scaledDeltaTime);
            if (safeScaledDeltaTime <= 0f)
                return;

            int slotIndex = EnsureToolRegistered(toolID, itemHashId, ResolveSafeMaxDurability(maxDurability));
            if (slotIndex < 0)
                return;

            if (TryResolvePendingDecay(out NativeArray<float> pendingDecayDt))
                pendingDecayDt[slotIndex] += safeScaledDeltaTime * math.max(0.1f, globalDurabilityMultiplier);
        }

        public void RepairTool(string toolID, float amount, float maxDurability)
        {
            if (string.IsNullOrEmpty(toolID) || !TryResolvePositiveDurabilityAmount(amount, out float safeAmount))
                return;

            float safeMaxDurability = ResolveSafeMaxDurability(maxDurability);
            if (!TryCompleteDecayJobIfScheduled(forceComplete: false))
            {
                QueueDurabilityCommand(DurabilityCommandKind.Repair, toolID, safeAmount, safeMaxDurability);
                return;
            }

            ApplyRepairTool(toolID, safeAmount, safeMaxDurability);
        }

        private void ApplyRepairTool(string toolID, float amount, float maxDurability)
        {
            float safeAmount = ClampFiniteNonNegative(amount);
            float safeMaxDurability = ResolveSafeMaxDurability(maxDurability);
            if (safeAmount <= 0f)
                return;

            int existingSlot = ResolveSlot(toolID);
            uint hashId = existingSlot >= 0 ? _itemHashBySlot[existingSlot] : unchecked((uint)Animator.StringToHash(toolID));
            int slotIndex = EnsureToolRegistered(toolID, hashId, safeMaxDurability);
            if (slotIndex < 0 ||
                !TryResolveItemStates(out NativeArray<ItemState> itemStates) ||
                !TryResolvePendingDecay(out NativeArray<float> pendingDecayDt))
            {
                return;
            }

            ItemState state = itemStates[slotIndex];
            state.durability = math.saturate(state.durability + (safeAmount / safeMaxDurability));
            if (state.durability >= DegradedThreshold)
                state.flags &= unchecked((ushort)~DegradedFlag);

            state.flags &= unchecked((ushort)~BrokenFlag);
            itemStates[slotIndex] = state;
            pendingDecayDt[slotIndex] = 0f;

            float repairedDurability = state.durability * safeMaxDurability;
            _durabilityMap[toolID] = repairedDurability;
            _brokenMap[toolID] = false;
            PublishDurabilityChangedSignal(slotIndex, repairedDurability, safeMaxDurability, ItemDurabilityChangedSignal.ReasonRepair);
        }

        public void RepairToolFull(string toolID, float maxDurability)
        {
            float safeMaxDurability = ResolveSafeMaxDurability(maxDurability);
            RepairTool(toolID, safeMaxDurability, safeMaxDurability);
        }

        public void BreakTool(string toolID)
        {
            if (string.IsNullOrEmpty(toolID))
                return;

            if (!TryCompleteDecayJobIfScheduled(forceComplete: false))
            {
                QueueDurabilityCommand(DurabilityCommandKind.Break, toolID, 0f, 1f);
                return;
            }

            ApplyBreakTool(toolID);
        }

        private void ApplyBreakTool(string toolID)
        {
            int existingSlot = ResolveSlot(toolID);
            uint hashId = existingSlot >= 0 ? _itemHashBySlot[existingSlot] : unchecked((uint)Animator.StringToHash(toolID));
            int slotIndex = EnsureToolRegistered(toolID, hashId, 1f);
            if (slotIndex < 0 || !TryResolveItemStates(out NativeArray<ItemState> itemStates))
                return;

            if ((itemStates[slotIndex].flags & BrokenFlag) != 0)
                return;

            ItemState state = itemStates[slotIndex];
            state.durability = 0f;
            state.flags |= (ushort)(BrokenFlag | DegradedFlag);
            itemStates[slotIndex] = state;
            _durabilityMap[toolID] = 0f;
            _brokenMap[toolID] = true;
            float maxDurability = math.max(1f, _maxDurabilityBySlot[slotIndex]);
            PublishDurabilityChangedSignal(slotIndex, 0f, maxDurability, ItemDurabilityChangedSignal.ReasonBreak);
        }

        public void ResetDurability(string toolID, float maxDurability)
        {
            if (string.IsNullOrEmpty(toolID))
                return;

            float safeMaxDurability = ResolveSafeMaxDurability(maxDurability);
            if (!TryCompleteDecayJobIfScheduled(forceComplete: false))
            {
                QueueDurabilityCommand(DurabilityCommandKind.Reset, toolID, 0f, safeMaxDurability);
                return;
            }

            ApplyResetDurability(toolID, safeMaxDurability);
        }

        private void ApplyResetDurability(string toolID, float maxDurability)
        {
            float safeMaxDurability = ResolveSafeMaxDurability(maxDurability);
            int existingSlot = ResolveSlot(toolID);
            uint hashId = existingSlot >= 0 ? _itemHashBySlot[existingSlot] : unchecked((uint)Animator.StringToHash(toolID));
            int slotIndex = EnsureToolRegistered(toolID, hashId, safeMaxDurability);
            if (slotIndex < 0 ||
                !TryResolveItemStates(out NativeArray<ItemState> itemStates) ||
                !TryResolvePendingDecay(out NativeArray<float> pendingDecayDt))
            {
                return;
            }

            ItemState state = itemStates[slotIndex];
            state.durability = 1f;
            state.flags = 0;
            itemStates[slotIndex] = state;
            pendingDecayDt[slotIndex] = 0f;

            _durabilityMap[toolID] = safeMaxDurability;
            _brokenMap[toolID] = false;
            PublishDurabilityChangedSignal(slotIndex, safeMaxDurability, safeMaxDurability, ItemDurabilityChangedSignal.ReasonRepair);
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

            if (!TryResolveItemStates(out NativeArray<ItemState> itemStates))
                return;

            Dictionary<string, float>.Enumerator durabilityEnumerator = data.toolDurabilityMap.GetEnumerator();
            while (durabilityEnumerator.MoveNext())
            {
                KeyValuePair<string, float> pair = durabilityEnumerator.Current;
                if (string.IsNullOrEmpty(pair.Key))
                    continue;

                float savedDurability = ClampFiniteNonNegative(pair.Value);
                float resolvedMaxDurability = ResolveSafeMaxDurability(savedDurability);
                int slotIndex = EnsureToolRegistered(pair.Key, unchecked((uint)Animator.StringToHash(pair.Key)), resolvedMaxDurability);
                if (slotIndex < 0)
                    continue;

                float normalized = math.saturate(savedDurability / resolvedMaxDurability);
                ItemState state = itemStates[slotIndex];
                state.durability = normalized;
                state.flags = normalized < DegradedThreshold ? DegradedFlag : (ushort)0;
                itemStates[slotIndex] = state;
                _durabilityMap[pair.Key] = savedDurability;
            }

            Dictionary<string, bool>.Enumerator brokenEnumerator = data.toolBrokenMap.GetEnumerator();
            while (brokenEnumerator.MoveNext())
            {
                KeyValuePair<string, bool> pair = brokenEnumerator.Current;
                if (!pair.Value || string.IsNullOrEmpty(pair.Key))
                    continue;

                int slotIndex = ResolveSlot(pair.Key);
                if (slotIndex < 0)
                    slotIndex = EnsureToolRegistered(pair.Key, unchecked((uint)Animator.StringToHash(pair.Key)), 1f);

                if (slotIndex < 0)
                    continue;

                ItemState state = itemStates[slotIndex];
                state.flags |= (ushort)(BrokenFlag | DegradedFlag);
                state.durability = 0f;
                itemStates[slotIndex] = state;
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

            bool playerInBrinePool = IsPlayerInBrinePool();
            if (playerInBrinePool)
                ApplyBrineCorrosionToTrackedTools();

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

        private void ApplyBrineCorrosionToTrackedTools()
        {
            if (!TryResolvePendingDecay(out NativeArray<float> pendingDecayDt) ||
                !TryResolveSlotActive(out NativeArray<byte> slotActive))
            {
                return;
            }

            float scaledDeltaTime = BrineCorrosionPerSecond * SlowTickDeltaTime * math.max(0.1f, globalDurabilityMultiplier);
            if (scaledDeltaTime <= 0f)
                return;

            for (int i = 0; i < MaxTrackedTools; i++)
            {
                if (slotActive[i] == 0)
                    continue;

                pendingDecayDt[i] += scaledDeltaTime;
            }
        }

        private bool IsPlayerInBrinePool()
        {
            ResourceDistributionDirector director = ResourceDistributionDirector.ActiveRuntimeInstance;
            return director != null &&
                   _playerRoot != null &&
                   director.TrySampleBrineFluidDensity(_playerRoot.position, out float densityKgPerCubicMeter) &&
                   densityKgPerCubicMeter >= BrineDensityThresholdKgPerCubicMeter;
        }

        private bool ResolvePlayerOwners()
        {
            if (_playerRoot == null)
            {
                if (!GameBootstrapper.TryGetCurrentPlayerTransform(out _playerRoot) || _playerRoot == null)
                    return false;
            }

            if (_playerSurvivalSystem == null)
                _playerRoot.TryGetComponent(out _playerSurvivalSystem);

            if (_playerToolManager == null)
            {
                IPlayerRuntimeContext playerRuntimeContext = _playerRuntimeContext;
                _playerToolManager = playerRuntimeContext != null
                    ? playerRuntimeContext.ToolManager
                    : null;
                if (_playerToolManager == null)
                    _playerRoot.TryGetComponent(out _playerToolManager);
            }

            return _playerSurvivalSystem != null && _playerToolManager != null;
        }

        private void EnsureNativeState()
        {
            TryResolveNativeState(out _, out _, out _, out _, out _);
        }

        private void DisposeNativeState()
        {
            ClearQueuedDurabilityCommands();
            if (_decayScheduled)
                DispatcherJobSwap.TryComplete(ref _scheduledDecayHandle, forceComplete: true);

            ReleaseDurabilityHandles(_dataVault);
            _itemStatesHandle = default;
            _pendingDecayDtHandle = default;
            _wearMultipliersHandle = default;
            _slotActiveHandle = default;
            _breakdownFlagsHandle = default;
            _scheduledDecayHandle = default;
            _decayScheduled = false;
            _dataVault = null;
            _saveService = null;
            _playerRuntimeContext = null;
        }

        private void ClearRuntimeState()
        {
            ClearQueuedDurabilityCommands();
            EnsureNativeState();
            if (!TryResolveNativeState(
                    out NativeArray<ItemState> itemStates,
                    out NativeArray<float> pendingDecayDt,
                    out NativeArray<float> wearMultipliers,
                    out NativeArray<byte> slotActive,
                    out NativeArray<byte> breakdownFlags))
            {
                return;
            }

            _durabilityMap.Clear();
            _brokenMap.Clear();
            _slotByToolId.Clear();

            for (int i = 0; i < MaxTrackedTools; i++)
            {
                _toolIdBySlot[i] = null;
                _maxDurabilityBySlot[i] = 0f;
                _itemHashBySlot[i] = 0u;
                _slotUsed[i] = false;
                itemStates[i] = default;
                pendingDecayDt[i] = 0f;
                wearMultipliers[i] = 0f;
                slotActive[i] = 0;
                breakdownFlags[i] = 0;
            }
        }

        private int EnsureToolRegistered(string toolID, uint itemHashId, float maxDurability)
        {
            if (string.IsNullOrEmpty(toolID))
                return -1;

            EnsureNativeState();
            if (!TryResolveItemStates(out NativeArray<ItemState> itemStates))
                return -1;

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
                itemStates[i] = state;
                _durabilityMap[toolID] = ResolveSafeMaxDurability(maxDurability);
                _brokenMap[toolID] = false;
                return i;
            }

            return -1;
        }

        private void UpdateSlotMetadata(int slotIndex, string toolID, uint itemHashId, float maxDurability)
        {
            float resolvedMaxDurability = ResolveSafeMaxDurability(maxDurability);
            _toolIdBySlot[slotIndex] = toolID;
            _maxDurabilityBySlot[slotIndex] = resolvedMaxDurability;
            _itemHashBySlot[slotIndex] = itemHashId;
            if (!TryResolveItemStates(out NativeArray<ItemState> itemStates) ||
                !TryResolveBuffer(ref _wearMultipliersHandle, BufferID.ToolDurabilityWearMultipliers, out NativeArray<float> wearMultipliers) ||
                !TryResolveSlotActive(out NativeArray<byte> slotActive))
            {
                return;
            }

            wearMultipliers[slotIndex] = ResolveWearMultiplier(itemHashId);
            slotActive[slotIndex] = 1;

            ItemState state = itemStates[slotIndex];
            state.hashID = itemHashId;
            itemStates[slotIndex] = state;

            if (!_durabilityMap.ContainsKey(toolID))
                _durabilityMap[toolID] = resolvedMaxDurability;

            if (!_brokenMap.ContainsKey(toolID))
                _brokenMap[toolID] = false;
        }

        private static float ResolveWearMultiplier(uint itemHashId)
        {
            if (itemHashId != 0u && ItemTemplateRegistry.TryGetTemplate(itemHashId, out ItemTemplate template))
                return ClampFiniteNonNegative(template.WearMultiplier);

            return 1f;
        }

        private int ResolveSlot(string toolID)
        {
            return !string.IsNullOrEmpty(toolID) && _slotByToolId.TryGetValue(toolID, out int slotIndex)
                ? slotIndex
                : -1;
        }

        private void PublishDurabilityChangedSignal(int slotIndex, float currentDurability, float maxDurability, byte reason)
        {
            if ((uint)slotIndex >= MaxTrackedTools ||
                !TryResolveItemStates(out NativeArray<ItemState> itemStates))
            {
                return;
            }

            float safeCurrentDurability = ClampFiniteNonNegative(currentDurability);
            float safeMaxDurability = ResolveSafeMaxDurability(maxDurability);
            uint itemHash = _itemHashBySlot[slotIndex];
            byte flags = (itemStates[slotIndex].flags & BrokenFlag) != 0
                ? ItemDurabilityChangedSignal.FlagBroken
                : (byte)0;
            ItemDurabilityChangedSignal signal = new ItemDurabilityChangedSignal
            {
                InventoryHash = 0u,
                ItemHash = itemHash,
                Durability01 = math.saturate(safeCurrentDurability / safeMaxDurability),
                AverageEquippedDurability01 = 1f,
                Frame = (uint)math.max(0, Time.frameCount),
                SlotIndex = (ushort)slotIndex,
                Reason = reason,
                Flags = flags,
                BiomeHash = 0u
            };
            SignalBus<ItemDurabilityChangedSignal>.Push(in signal);
        }

        private bool HasNativeState()
        {
            return TryResolveNativeState(out _, out _, out _, out _, out _);
        }

        private bool TryResolveNativeState(
            out NativeArray<ItemState> itemStates,
            out NativeArray<float> pendingDecayDt,
            out NativeArray<float> wearMultipliers,
            out NativeArray<byte> slotActive,
            out NativeArray<byte> breakdownFlags)
        {
            bool itemStatesResolved = TryResolveBuffer(ref _itemStatesHandle, BufferID.ToolDurabilityItemStates, out itemStates);
            bool pendingResolved = TryResolveBuffer(ref _pendingDecayDtHandle, BufferID.ToolDurabilityPendingDecay, out pendingDecayDt);
            bool wearResolved = TryResolveBuffer(ref _wearMultipliersHandle, BufferID.ToolDurabilityWearMultipliers, out wearMultipliers);
            bool slotResolved = TryResolveBuffer(ref _slotActiveHandle, BufferID.ToolDurabilitySlotActive, out slotActive);
            bool breakdownResolved = TryResolveBuffer(ref _breakdownFlagsHandle, BufferID.ToolDurabilityBreakdownFlags, out breakdownFlags);
            return itemStatesResolved && pendingResolved && wearResolved && slotResolved && breakdownResolved;
        }

        private bool TryResolveItemStates(out NativeArray<ItemState> itemStates)
        {
            return TryResolveBuffer(ref _itemStatesHandle, BufferID.ToolDurabilityItemStates, out itemStates);
        }

        private bool TryResolvePendingDecay(out NativeArray<float> pendingDecayDt)
        {
            return TryResolveBuffer(ref _pendingDecayDtHandle, BufferID.ToolDurabilityPendingDecay, out pendingDecayDt);
        }

        private bool TryResolveSlotActive(out NativeArray<byte> slotActive)
        {
            return TryResolveBuffer(ref _slotActiveHandle, BufferID.ToolDurabilitySlotActive, out slotActive);
        }

        private bool TryResolveBreakdownFlags(out NativeArray<byte> breakdownFlags)
        {
            return TryResolveBuffer(ref _breakdownFlagsHandle, BufferID.ToolDurabilityBreakdownFlags, out breakdownFlags);
        }

        private bool TryResolveBuffer<T>(
            ref VaultGenerationHandle<T> handle,
            BufferID bufferId,
            out NativeArray<T> buffer)
            where T : struct
        {
            buffer = default;
            IDataVault vault = _dataVault;
            if (vault == null)
                return false;

            if (IsGenerationHandleCreated(in handle) &&
                vault.TryResolveHandle(in handle, out buffer) &&
                buffer.IsCreated &&
                buffer.Length >= MaxTrackedTools)
            {
                return true;
            }

            ReleaseDurabilityHandle(vault, ref handle);
            handle = vault.GetGenerationHandle<T>(
                bufferId,
                MaxTrackedTools,
                SystemID.GameplayTools,
                NativeArrayOptions.ClearMemory);

            if (!IsGenerationHandleCreated(in handle) ||
                !vault.TryResolveHandle(in handle, out buffer))
            {
                buffer = default;
                return false;
            }

            return buffer.IsCreated && buffer.Length >= MaxTrackedTools;
        }

        private static bool IsGenerationHandleCreated<T>(in VaultGenerationHandle<T> handle)
            where T : struct
        {
            return handle.BufferID != 0u && handle.Generation != 0u;
        }

        private bool HasPendingDecay()
        {
            if (!TryResolvePendingDecay(out NativeArray<float> pendingDecayDt))
                return false;

            for (int i = 0; i < MaxTrackedTools; i++)
            {
                if (_slotUsed[i] && pendingDecayDt[i] > 0f)
                    return true;
            }

            return false;
        }

        private void SyncManagedMirrorsFromNative()
        {
            if (!_managedMirrorDirty || !TryResolveItemStates(out NativeArray<ItemState> itemStates))
                return;

            for (int i = 0; i < MaxTrackedTools; i++)
            {
                if (!_slotUsed[i])
                    continue;

                string toolId = _toolIdBySlot[i];
                if (string.IsNullOrEmpty(toolId))
                    continue;

                ItemState state = itemStates[i];
                float maxDurability = math.max(1f, _maxDurabilityBySlot[i]);
                float currentDurability = math.saturate(state.durability) * maxDurability;
                bool broken = autoBreakOnZero && (state.flags & BrokenFlag) != 0;

                _durabilityMap.TryGetValue(toolId, out float previousDurability);
                _brokenMap.TryGetValue(toolId, out bool previousBroken);

                _durabilityMap[toolId] = currentDurability;
                _brokenMap[toolId] = broken;

                if (math.abs(previousDurability - currentDurability) > 0.0001f || previousBroken != broken)
                {
                    byte reason = !previousBroken && broken
                        ? ItemDurabilityChangedSignal.ReasonBreak
                        : ItemDurabilityChangedSignal.ReasonCorrosion;
                    PublishDurabilityChangedSignal(i, currentDurability, maxDurability, reason);
                }
            }

            _managedMirrorDirty = false;
        }

        private void FlushBreakdownEvents()
        {
            if (!TryResolveBreakdownFlags(out NativeArray<byte> breakdownFlags))
                return;

            for (int i = 0; i < MaxTrackedTools; i++)
            {
                if (breakdownFlags[i] == 0)
                    continue;

                if (!SystemDispatcher.TryConsumeLateFrameEventDispatch())
                    return;

                breakdownFlags[i] = 0;
            }
        }

        private void CompleteDecayJobIfScheduled()
        {
            TryCompleteDecayJobIfScheduled(forceComplete: true);
        }

        private bool TryCompleteDecayJobIfScheduled(bool forceComplete)
        {
            if (!_decayScheduled)
            {
                DrainQueuedDurabilityCommands();
                TryUnregisterLateFrameIfIdle();
                return true;
            }

            if (!DispatcherJobSwap.TryComplete(ref _scheduledDecayHandle, forceComplete))
                return false;

            _decayScheduled = false;
            _managedMirrorDirty = true;
            SyncManagedMirrorsFromNative();
            FlushBreakdownEvents();
            DrainQueuedDurabilityCommands();
            TryUnregisterLateFrameIfIdle();
            return true;
        }

        private void QueueDurabilityCommand(DurabilityCommandKind kind, string toolID, float amount, float maxDurability, uint itemHashId = 0u)
        {
            if (TryMergeQueuedDurabilityCommand(kind, toolID, amount, maxDurability, itemHashId))
            {
                TryRegisterLateFrame();
                return;
            }

            PendingDurabilityCommand command = CreatePendingDurabilityCommand(kind, toolID, amount, maxDurability, itemHashId);
            if (_queuedDurabilityCommandCount >= MaxQueuedDurabilityCommands)
            {
                if (TryCompleteDecayJobIfScheduled(forceComplete: false))
                {
                    ApplyDurabilityCommand(in command);
                    return;
                }

                TryReplaceQueuedDurabilityCommand(in command);
                TryRegisterLateFrame();
                return;
            }

            _queuedDurabilityCommands[_queuedDurabilityCommandCount++] = command;
            TryRegisterLateFrame();
        }

        private static PendingDurabilityCommand CreatePendingDurabilityCommand(
            DurabilityCommandKind kind,
            string toolID,
            float amount,
            float maxDurability,
            uint itemHashId)
        {
            return new PendingDurabilityCommand
            {
                ToolId = toolID,
                Amount = ClampFiniteNonNegative(amount),
                MaxDurability = ResolveSafeMaxDurability(maxDurability),
                ItemHashId = itemHashId,
                Kind = kind
            };
        }

        private bool TryReplaceQueuedDurabilityCommand(in PendingDurabilityCommand command)
        {
            if (command.Kind != DurabilityCommandKind.Break &&
                command.Kind != DurabilityCommandKind.Reset &&
                command.Kind != DurabilityCommandKind.Repair)
            {
                return false;
            }

            for (int i = 0; i < _queuedDurabilityCommandCount; i++)
            {
                PendingDurabilityCommand existing = _queuedDurabilityCommands[i];
                if (!string.Equals(existing.ToolId, command.ToolId, StringComparison.Ordinal) ||
                    !IsWearCommand(existing.Kind))
                {
                    continue;
                }

                _queuedDurabilityCommands[i] = command;
                return true;
            }

            for (int i = 0; i < _queuedDurabilityCommandCount; i++)
            {
                if (!IsWearCommand(_queuedDurabilityCommands[i].Kind))
                    continue;

                _queuedDurabilityCommands[i] = command;
                return true;
            }

            return false;
        }

        private bool TryMergeQueuedDurabilityCommand(DurabilityCommandKind kind, string toolID, float amount, float maxDurability, uint itemHashId)
        {
            if (_queuedDurabilityCommandCount <= 0 || string.IsNullOrEmpty(toolID))
                return false;

            for (int index = _queuedDurabilityCommandCount - 1; index >= 0; index--)
            {
                PendingDurabilityCommand queued = _queuedDurabilityCommands[index];
                if (!string.Equals(queued.ToolId, toolID, StringComparison.Ordinal))
                    continue;

                if (queued.Kind != kind)
                    return false;

                return TryMergeQueuedDurabilityCommandAt(index, amount, maxDurability, itemHashId);
            }

            return false;
        }

        private bool TryMergeQueuedDurabilityCommandAt(int index, float amount, float maxDurability, uint itemHashId)
        {
            PendingDurabilityCommand existing = _queuedDurabilityCommands[index];
            switch (existing.Kind)
            {
                case DurabilityCommandKind.Drain:
                case DurabilityCommandKind.Repair:
                    if (!CanMergeDurabilityMax(existing.MaxDurability, maxDurability))
                        return false;
                    existing.Amount = ClampFiniteNonNegative(existing.Amount) + ClampFiniteNonNegative(amount);
                    existing.MaxDurability = math.max(existing.MaxDurability, maxDurability);
                    _queuedDurabilityCommands[index] = existing;
                    return true;

                case DurabilityCommandKind.DrainByTime:
                    if (existing.ItemHashId != itemHashId ||
                        !CanMergeDurabilityMax(existing.MaxDurability, maxDurability))
                    {
                        return false;
                    }

                    existing.Amount = ClampFiniteNonNegative(existing.Amount) + ClampFiniteNonNegative(amount);
                    existing.MaxDurability = math.max(existing.MaxDurability, maxDurability);
                    _queuedDurabilityCommands[index] = existing;
                    return true;

                case DurabilityCommandKind.Break:
                    return true;

                case DurabilityCommandKind.Reset:
                    if (!CanMergeDurabilityMax(existing.MaxDurability, maxDurability))
                        return false;
                    existing.MaxDurability = maxDurability;
                    existing.ItemHashId = itemHashId;
                    _queuedDurabilityCommands[index] = existing;
                    return true;
            }

            return false;
        }

        private static bool IsWearCommand(DurabilityCommandKind kind)
        {
            return kind == DurabilityCommandKind.Drain ||
                   kind == DurabilityCommandKind.DrainByTime;
        }

        private static bool TryResolvePositiveDurabilityAmount(float value, out float resolved)
        {
            resolved = ClampFiniteNonNegative(value);
            return resolved > 0f;
        }

        private static float ResolveSafeMaxDurability(float maxDurability)
        {
            return math.isfinite(maxDurability) ? math.max(1f, maxDurability) : 1f;
        }

        private static bool CanMergeDurabilityMax(float left, float right)
        {
            return math.isfinite(left) &&
                   math.isfinite(right) &&
                   math.abs(left - right) <= 0.0001f;
        }

        private static float ClampFiniteNonNegative(float value)
        {
            return math.isfinite(value) ? math.max(0f, value) : 0f;
        }

        private void DrainQueuedDurabilityCommands()
        {
            int count = _queuedDurabilityCommandCount;
            if (count <= 0 || _decayScheduled)
                return;

            _queuedDurabilityCommandCount = 0;
            for (int i = 0; i < count; i++)
            {
                PendingDurabilityCommand command = _queuedDurabilityCommands[i];
                _queuedDurabilityCommands[i] = default;
                ApplyDurabilityCommand(command);
            }
        }

        private void ClearQueuedDurabilityCommands()
        {
            int count = _queuedDurabilityCommandCount;
            _queuedDurabilityCommandCount = 0;
            for (int i = 0; i < count; i++)
                _queuedDurabilityCommands[i] = default;
        }

        private void ApplyDurabilityCommand(in PendingDurabilityCommand command)
        {
            if (string.IsNullOrEmpty(command.ToolId))
                return;

            switch (command.Kind)
            {
                case DurabilityCommandKind.Repair:
                    if (command.Amount > 0f)
                        ApplyRepairTool(command.ToolId, command.Amount, command.MaxDurability);
                    break;
                case DurabilityCommandKind.Break:
                    ApplyBreakTool(command.ToolId);
                    break;
                case DurabilityCommandKind.Reset:
                    ApplyResetDurability(command.ToolId, command.MaxDurability);
                    break;
                case DurabilityCommandKind.Drain:
                    if (command.Amount > 0f)
                        ApplyDrainDurability(command.ToolId, command.Amount, command.MaxDurability);
                    break;
                case DurabilityCommandKind.DrainByTime:
                    if (command.Amount > 0f)
                        ApplyDrainDurabilityByTime(command.ToolId, command.ItemHashId, command.Amount, command.MaxDurability);
                    break;
            }
        }

        private void TryRegisterSlowTick()
        {
            if (_registeredSlowTick)
                return;
            if (!Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            _registeredSlowTick = GlobalRegistry.TryRegisterSlowTickable(this, PriorityLayer.Player);
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
            if (!Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            _registeredUpdate = GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.Player);
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
            if (!Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            _registeredLateFrame = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Player);
        }

        private void TryUnregisterLateFrame()
        {
            if (!_registeredLateFrame)
                return;

            GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Player);
            _registeredLateFrame = false;
        }

        private void TryUnregisterLateFrameIfIdle()
        {
            if (_decayScheduled || _queuedDurabilityCommandCount > 0)
                return;

            TryUnregisterLateFrame();
        }

        private void TryRegisterSaveService()
        {
            ISaveService saveService = _saveService;
            if (_saveRegistered || saveService == null)
                return;

            saveService.Register(this);
            _saveRegistered = true;
        }

        private void TryUnregisterSaveService()
        {
            ISaveService saveService = _saveService;
            if (!_saveRegistered || saveService == null)
                return;

            saveService.Unregister(this);
            _saveRegistered = false;
        }

        private void CacheRegistryDependenciesCold()
        {
            _dataVault = GlobalRegistry.DataVault;
            _saveService = GlobalRegistry.Save;
            _playerRuntimeContext = GlobalRegistry.Player;
        }

        private void ApplyRegistryServiceRebind(GlobalRegistryServiceSlot serviceSlot, object currentService)
        {
            switch (serviceSlot)
            {
                case GlobalRegistryServiceSlot.DataVault:
                    RebindDataVault(currentService as IDataVault);
                    break;
                case GlobalRegistryServiceSlot.Save:
                    RebindSaveService(currentService as ISaveService);
                    break;
                case GlobalRegistryServiceSlot.Player:
                    _playerRuntimeContext = currentService as IPlayerRuntimeContext;
                    _playerToolManager = _playerRuntimeContext != null ? _playerRuntimeContext.ToolManager : null;
                    break;
            }
        }

        private void RebindDataVault(IDataVault dataVault)
        {
            if (ReferenceEquals(_dataVault, dataVault))
                return;

            TryCompleteDecayJobIfScheduled(forceComplete: true);
            ReleaseDurabilityHandles(_dataVault);
            _dataVault = dataVault;
            _itemStatesHandle = default;
            _pendingDecayDtHandle = default;
            _wearMultipliersHandle = default;
            _slotActiveHandle = default;
            _breakdownFlagsHandle = default;
        }

        private void ReleaseDurabilityHandles(IDataVault dataVault)
        {
            if (dataVault == null)
                return;

            ReleaseDurabilityHandle(dataVault, ref _itemStatesHandle);
            ReleaseDurabilityHandle(dataVault, ref _pendingDecayDtHandle);
            ReleaseDurabilityHandle(dataVault, ref _wearMultipliersHandle);
            ReleaseDurabilityHandle(dataVault, ref _slotActiveHandle);
            ReleaseDurabilityHandle(dataVault, ref _breakdownFlagsHandle);
        }

        private static void ReleaseDurabilityHandle<T>(IDataVault dataVault, ref VaultGenerationHandle<T> handle)
            where T : struct
        {
            if (!IsGenerationHandleCreated(in handle))
                return;

            dataVault.ReleaseBuffer(in handle);
            handle = default;
        }

        private void RebindSaveService(ISaveService saveService)
        {
            if (ReferenceEquals(_saveService, saveService))
                return;

            TryUnregisterSaveService();
            _saveService = saveService;
            TryRegisterSaveService();
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
            if (_serviceRegistered || !Application.isPlaying)
                return;

            GlobalRegistry.RegisterToolDurabilityRuntime(this);
            _serviceRegistered = ReferenceEquals(GlobalRegistry.ToolDurability, this);
        }

        private void TryUnregisterService()
        {
            if (!_serviceRegistered)
                return;

            if (ReferenceEquals(GlobalRegistry.ToolDurability, this))
                GlobalRegistry.UnregisterToolDurabilityRuntime(this);

            _serviceRegistered = false;
        }
    }
}
