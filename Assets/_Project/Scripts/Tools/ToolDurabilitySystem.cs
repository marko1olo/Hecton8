using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Hecton.Localization;
using Hecton8.Core;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Memory;
using Hecton8.Gameplay;
using Hecton8.Inventory;
using Hecton8.SaveSystem;
using Hecton8.World;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Tools
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/Tools/Tool Durability System")]
    public sealed class ToolDurabilitySystem : MonoBehaviour, ISaveable, ISlowTickable, IUpdatable, ILateFrameTickable, IToolDurabilityService, IGlobalRegistryHotSwapListener, IGlobalRegistryHotSwapRefListener
    {
        private static int s_x001ToolDurabilitySystemSignalPushDropCount;
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
        private const int PendingDurabilityCommandSizeBytes = 24;
        private const int ItemStateSizeBytes = 16;

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
        // COLD ALLOC: Dictionary<uint,int>[32] - item-hash to native durability slot mapping - owner: ToolDurabilitySystem
        private readonly Dictionary<uint, int> _slotByItemHash = new Dictionary<uint, int>(MaxTrackedTools);
        // COLD ALLOC: string[32] — native-slot tool-id mirror for event/save fanout — owner: ToolDurabilitySystem
        private readonly string[] _toolIdBySlot = new string[MaxTrackedTools];
        // COLD ALLOC: float[32] — authored max durability mirror aligned to slot state — owner: ToolDurabilitySystem
        private readonly float[] _maxDurabilityBySlot = new float[MaxTrackedTools];
        // COLD ALLOC: uint[32] — item-hash mirror aligned to slot state — owner: ToolDurabilitySystem
        private readonly uint[] _itemHashBySlot = new uint[MaxTrackedTools];
        // COLD ALLOC: float[32] - durability mirror aligned to slot state for hash-id hot reads - owner: ToolDurabilitySystem
        private readonly float[] _durabilityBySlot = new float[MaxTrackedTools];
        // COLD ALLOC: byte[32] - broken-state mirror aligned to slot state for hash-id hot reads - owner: ToolDurabilitySystem
        private readonly byte[] _brokenBySlot = new byte[MaxTrackedTools];
        // COLD ALLOC: float[32] - template wear multiplier mirror aligned to slot state for active equipment hot reads
        private readonly float[] _wearMultiplierBySlot = new float[MaxTrackedTools];
        // COLD ALLOC: bool[32] — managed slot occupancy mirror — owner: ToolDurabilitySystem
        private readonly bool[] _slotUsed = new bool[MaxTrackedTools];
        // COLD ALLOC: PendingDurabilityCommand[32] - one-frame native-job mutation queue - owner: ToolDurabilitySystem
        private readonly PendingDurabilityCommand[] _queuedDurabilityCommands = new PendingDurabilityCommand[MaxQueuedDurabilityCommands];
        // COLD ALLOC: string[32] - owner-local command id sidecar; keeps PendingDurabilityCommand blittable
        private readonly string[] _queuedDurabilityCommandToolIds = new string[MaxQueuedDurabilityCommands];
        private VaultGenerationHandle<ItemState> _itemStatesHandle;
        private VaultGenerationHandle<float> _pendingDecayDtHandle;
        private VaultGenerationHandle<float> _wearMultipliersHandle;
        private VaultGenerationHandle<byte> _slotActiveHandle;
        private VaultGenerationHandle<byte> _breakdownFlagsHandle;
        private int _queuedDurabilityCommandCount;
        private uint _durabilitySignalFrame;
        private bool _decayScheduled;

        private PlayerToolManager _playerToolManager;
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
        private IBrineFluidDensityReadModel _brineDensityReadModel;
        private static bool s_nativeLayoutValidated;
        private static bool s_nativeLayoutValid;

        public int SavePriority => 20;
        public int LoadPriority => 20;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            s_x001ToolDurabilitySystemSignalPushDropCount = 0;
            s_nativeLayoutValidated = false;
            s_nativeLayoutValid = false;
        }

        private static void RunDurabilityDecayOwnerPhase(
            NativeArray<ItemState> states,
            NativeArray<float> pendingDecayDt,
            NativeArray<float> wearMultipliers,
            NativeArray<byte> slotActive,
            NativeArray<byte> breakdownFlags)
        {
            int count = math.min(
                math.min(math.min(states.Length, pendingDecayDt.Length), math.min(wearMultipliers.Length, slotActive.Length)),
                math.min(breakdownFlags.Length, MaxTrackedTools));
            for (int index = 0; index < count; index++)
            {
                if (slotActive[index] == 0)
                {
                    pendingDecayDt[index] = 0f;
                    continue;
                }

                float dt = pendingDecayDt[index];
                pendingDecayDt[index] = 0f;
                if (dt <= 0f)
                    continue;

                ItemState state = states[index];
                ApplyDurabilityDecay(ref state, dt, wearMultipliers[index], breakdownFlags, index);
                states[index] = state;
            }
        }

        private static void ApplyDurabilityDecay(
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

        private enum DurabilityCommandKind : byte
        {
            Repair = 1,
            Break = 2,
            Reset = 3,
            Drain = 4,
            DrainByTime = 5
        }

#pragma warning disable 0649 // Assigned through object initializers before queued drain; compiler does not track array-backed command staging.
        [StructLayout(LayoutKind.Explicit, Size = 24)]
        private struct PendingDurabilityCommand
        {
            [FieldOffset(0)]
            public float Amount;

            [FieldOffset(4)]
            public float MaxDurability;

            [FieldOffset(8)]
            public uint ItemHashId;

            [FieldOffset(12)]
            public uint ToolHashId;

            [FieldOffset(16)]
            public DurabilityCommandKind Kind;

            [FieldOffset(17)]
            private byte _pad0;

            [FieldOffset(18)]
            private ushort _pad1;

            [FieldOffset(20)]
            private uint _pad2;
        }
#pragma warning restore 0649

#pragma warning disable 0649 // Reserved padding keeps native item-state layout stable for future flags.
        [StructLayout(LayoutKind.Explicit, Size = 16)]
        private struct ItemState
        {
            [FieldOffset(0)]
            public float durability;
            [FieldOffset(4)]
            public uint hashID;
            [FieldOffset(8)]
            public ushort flags;
            [FieldOffset(10)]
            public ushort reserved;
            [FieldOffset(12)]
            private uint _pad0;
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
            EnsureNativeStateCold();
        }

        private void OnEnable()
        {
            CacheRegistryDependenciesCold();
            EnsureNativeStateCold();
            TryRegisterHotSwap();
            TryRegisterService();
            TryRegisterSlowTick();
            TryRegisterUpdate();
            TryRegisterLateFrame();
            TryRegisterSaveService();
        }

        private void Start()
        {
            CacheRegistryDependenciesCold();
            EnsureNativeStateCold();
            TryRegisterHotSwap();
            TryRegisterService();
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
            if (!enableDurabilityDrain)
            {
                DrainQueuedDurabilityCommands();
                return;
            }

            if (_decayScheduled)
            {
                TryRunPendingDecayPass();
                DrainQueuedDurabilityCommands();
                return;
            }

            if (!EnsureNativeStateViews(
                    out _,
                    out NativeArray<float> pendingDecayDt,
                    out _,
                    out _,
                    out _,
                    createIfMissing: false) ||
                !HasPendingDecay(pendingDecayDt))
            {
                DrainQueuedDurabilityCommands();
                return;
            }

            _decayScheduled = true;
            _managedMirrorDirty = true;
            TryRunPendingDecayPass();
            DrainQueuedDurabilityCommands();
        }

        public void LateFrameTick()
        {
            FlushBreakdownEvents();
        }

        public void SlowTick()
        {
            ApplyEnvironmentalCorrosion();
            if (_decayScheduled)
                TryRunPendingDecayPass();
            DrainQueuedDurabilityCommands();
        }

        public float GetDurability(string toolID, float maxDurability)
        {
            if (string.IsNullOrEmpty(toolID))
                return ClampFiniteNonNegative(maxDurability);

            return _durabilityMap.TryGetValue(toolID, out float current)
                ? ClampFiniteNonNegative(current)
                : ClampFiniteNonNegative(maxDurability);
        }

        public float GetDurability(uint itemHashId, float maxDurability)
        {
            return TryReadDurability(itemHashId, maxDurability, out float durability)
                ? durability
                : ClampFiniteNonNegative(maxDurability);
        }

        public bool TryReadDurability(uint itemHashId, float maxDurability, out float durability)
        {
            durability = ClampFiniteNonNegative(maxDurability);
            int slotIndex = ResolveSlot(itemHashId);
            if (slotIndex < 0)
                return false;

            durability = ClampFiniteNonNegative(_durabilityBySlot[slotIndex]);
            return true;
        }

        public float GetDurabilityNormalized(string toolID, float maxDurability)
        {
            float current = ClampFiniteNonNegative(GetDurability(toolID, maxDurability));
            return math.saturate(current / ResolveSafeMaxDurability(maxDurability));
        }

        public float GetDurabilityNormalized(uint itemHashId, float maxDurability)
        {
            float current = ClampFiniteNonNegative(GetDurability(itemHashId, maxDurability));
            return math.saturate(current / ResolveSafeMaxDurability(maxDurability));
        }

        public bool IsBroken(string toolID)
        {
            return !string.IsNullOrEmpty(toolID) &&
                   _brokenMap.TryGetValue(toolID, out bool broken) &&
                   broken;
        }

        public bool IsBroken(uint itemHashId)
        {
            return TryReadBroken(itemHashId, out bool broken) && broken;
        }

        public bool TryReadBroken(uint itemHashId, out bool broken)
        {
            broken = false;
            int slotIndex = ResolveSlot(itemHashId);
            if (slotIndex < 0)
                return false;

            broken = _brokenBySlot[slotIndex] != 0;
            return true;
        }

        public bool IsDegraded(string toolID)
        {
            if (string.IsNullOrEmpty(toolID))
                return false;

            int slotIndex = ResolveSlot(toolID);
            if (slotIndex < 0)
                return false;

            float maxDurability = math.max(1f, _maxDurabilityBySlot[slotIndex]);
            return _durabilityMap.TryGetValue(toolID, out float current) &&
                   ClampFiniteNonNegative(current) <= maxDurability * DegradedThreshold;
        }

        public bool IsDegraded(uint itemHashId)
        {
            int slotIndex = ResolveSlot(itemHashId);
            if (slotIndex < 0)
                return false;

            float maxDurability = math.max(1f, _maxDurabilityBySlot[slotIndex]);
            return ClampFiniteNonNegative(_durabilityBySlot[slotIndex]) <= maxDurability * DegradedThreshold;
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

            if (EnsurePendingDecay(out NativeArray<float> pendingDecayDt))
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

        public bool TryDrainDurabilityByTime(uint itemHashId, float scaledDeltaTime, float maxDurability)
        {
            if (!enableDurabilityDrain ||
                itemHashId == 0u ||
                !TryResolvePositiveDurabilityAmount(scaledDeltaTime, out float safeScaledDeltaTime))
            {
                return false;
            }

            int slotIndex = ResolveSlot(itemHashId);
            if (slotIndex < 0 || string.IsNullOrEmpty(_toolIdBySlot[slotIndex]))
                return false;

            float safeMaxDurability = ResolveSafeMaxDurability(maxDurability);
            if (!TryCompleteDecayJobIfScheduled(forceComplete: false))
            {
                QueueDurabilityCommand(DurabilityCommandKind.DrainByTime, _toolIdBySlot[slotIndex], safeScaledDeltaTime, safeMaxDurability, itemHashId);
                return true;
            }

            return TryApplyDrainDurabilityByTime(slotIndex, safeScaledDeltaTime, safeMaxDurability);
        }

        public void RegisterCentralizedEquipmentMirror(string toolID, uint itemHashId, float maxDurability)
        {
            if (string.IsNullOrEmpty(toolID))
                return;

            EnsureToolRegistered(toolID, itemHashId, ResolveSafeMaxDurability(maxDurability));
        }

        public float ResolveCentralizedEquipmentWearMultiplier(uint itemHashId)
        {
            if (!enableDurabilityDrain)
                return 0f;

            float wearMultiplier = 1f;
            int slotIndex = ResolveSlot(itemHashId);
            if (slotIndex >= 0)
                wearMultiplier = ClampFiniteNonNegative(_wearMultiplierBySlot[slotIndex]);
            else
                wearMultiplier = ResolveWearMultiplier(itemHashId);

            return math.max(0.1f, globalDurabilityMultiplier) * wearMultiplier;
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
                !EnsureItemStates(out NativeArray<ItemState> itemStates) ||
                !EnsurePendingDecay(out NativeArray<float> pendingDecayDt))
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
            bool broken = (state.flags & BrokenFlag) != 0;
            WriteManagedMirror(slotIndex, toolID, currentDurability, broken);

            byte reason = !previousBroken && broken
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

            if (EnsurePendingDecay(out NativeArray<float> pendingDecayDt))
                pendingDecayDt[slotIndex] += safeScaledDeltaTime * math.max(0.1f, globalDurabilityMultiplier);
        }

        private bool TryApplyDrainDurabilityByTime(int slotIndex, float scaledDeltaTime, float maxDurability)
        {
            if ((uint)slotIndex >= MaxTrackedTools || !_slotUsed[slotIndex])
                return false;

            float safeScaledDeltaTime = ClampFiniteNonNegative(scaledDeltaTime);
            if (safeScaledDeltaTime <= 0f)
                return false;

            _maxDurabilityBySlot[slotIndex] = ResolveSafeMaxDurability(maxDurability);
            if (EnsurePendingDecay(out NativeArray<float> pendingDecayDt))
            {
                pendingDecayDt[slotIndex] += safeScaledDeltaTime * math.max(0.1f, globalDurabilityMultiplier);
                return true;
            }

            return false;
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

        public bool TryRepairTool(uint itemHashId, float amount, float maxDurability)
        {
            if (itemHashId == 0u || !TryResolvePositiveDurabilityAmount(amount, out float safeAmount))
                return false;

            int slotIndex = ResolveSlot(itemHashId);
            if (slotIndex < 0)
                return false;

            float safeMaxDurability = ResolveSafeMaxDurability(maxDurability);
            if (!TryCompleteDecayJobIfScheduled(forceComplete: false))
                return QueueDurabilityCommandBySlot(DurabilityCommandKind.Repair, slotIndex, safeAmount, safeMaxDurability, itemHashId);

            return TryApplyRepairTool(slotIndex, safeAmount, safeMaxDurability);
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
            TryApplyRepairTool(slotIndex, safeAmount, safeMaxDurability);
        }

        private bool TryApplyRepairTool(int slotIndex, float amount, float maxDurability)
        {
            if ((uint)slotIndex >= MaxTrackedTools || !_slotUsed[slotIndex])
                return false;

            float safeAmount = ClampFiniteNonNegative(amount);
            float safeMaxDurability = ResolveSafeMaxDurability(maxDurability);
            if (safeAmount <= 0f ||
                !EnsureItemStates(out NativeArray<ItemState> itemStates) ||
                !EnsurePendingDecay(out NativeArray<float> pendingDecayDt))
            {
                return false;
            }

            ItemState state = itemStates[slotIndex];
            state.durability = math.saturate(state.durability + (safeAmount / safeMaxDurability));
            if (state.durability >= DegradedThreshold)
                state.flags &= unchecked((ushort)~DegradedFlag);

            state.flags &= unchecked((ushort)~BrokenFlag);
            itemStates[slotIndex] = state;
            pendingDecayDt[slotIndex] = 0f;

            float repairedDurability = state.durability * safeMaxDurability;
            WriteManagedMirror(slotIndex, _toolIdBySlot[slotIndex], repairedDurability, false);
            PublishDurabilityChangedSignal(slotIndex, repairedDurability, safeMaxDurability, ItemDurabilityChangedSignal.ReasonRepair);
            return true;
        }

        public void RepairToolFull(string toolID, float maxDurability)
        {
            float safeMaxDurability = ResolveSafeMaxDurability(maxDurability);
            RepairTool(toolID, safeMaxDurability, safeMaxDurability);
        }

        public bool TryRepairToolFull(uint itemHashId, float maxDurability)
        {
            float safeMaxDurability = ResolveSafeMaxDurability(maxDurability);
            return TryRepairTool(itemHashId, safeMaxDurability, safeMaxDurability);
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

        public bool TryBreakTool(uint itemHashId)
        {
            if (itemHashId == 0u)
                return false;

            int slotIndex = ResolveSlot(itemHashId);
            if (slotIndex < 0)
                return false;

            if (!TryCompleteDecayJobIfScheduled(forceComplete: false))
                return QueueDurabilityCommandBySlot(DurabilityCommandKind.Break, slotIndex, 0f, math.max(1f, _maxDurabilityBySlot[slotIndex]), itemHashId);

            return TryApplyBreakTool(slotIndex);
        }

        private void ApplyBreakTool(string toolID)
        {
            int existingSlot = ResolveSlot(toolID);
            uint hashId = existingSlot >= 0 ? _itemHashBySlot[existingSlot] : unchecked((uint)Animator.StringToHash(toolID));
            int slotIndex = EnsureToolRegistered(toolID, hashId, 1f);
            TryApplyBreakTool(slotIndex);
        }

        private bool TryApplyBreakTool(int slotIndex)
        {
            if ((uint)slotIndex >= MaxTrackedTools || !_slotUsed[slotIndex] || !EnsureItemStates(out NativeArray<ItemState> itemStates))
                return false;

            if ((itemStates[slotIndex].flags & BrokenFlag) != 0)
                return true;

            ItemState state = itemStates[slotIndex];
            state.durability = 0f;
            state.flags |= (ushort)(BrokenFlag | DegradedFlag);
            itemStates[slotIndex] = state;
            float maxDurability = math.max(1f, _maxDurabilityBySlot[slotIndex]);
            WriteManagedMirror(slotIndex, _toolIdBySlot[slotIndex], 0f, true);
            PublishDurabilityChangedSignal(slotIndex, 0f, maxDurability, ItemDurabilityChangedSignal.ReasonBreak);
            return true;
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

        public bool TryResetDurability(uint itemHashId, float maxDurability)
        {
            if (itemHashId == 0u)
                return false;

            int slotIndex = ResolveSlot(itemHashId);
            if (slotIndex < 0)
                return false;

            float safeMaxDurability = ResolveSafeMaxDurability(maxDurability);
            if (!TryCompleteDecayJobIfScheduled(forceComplete: false))
                return QueueDurabilityCommandBySlot(DurabilityCommandKind.Reset, slotIndex, 0f, safeMaxDurability, itemHashId);

            return TryApplyResetDurability(slotIndex, safeMaxDurability);
        }

        private void ApplyResetDurability(string toolID, float maxDurability)
        {
            float safeMaxDurability = ResolveSafeMaxDurability(maxDurability);
            int existingSlot = ResolveSlot(toolID);
            uint hashId = existingSlot >= 0 ? _itemHashBySlot[existingSlot] : unchecked((uint)Animator.StringToHash(toolID));
            int slotIndex = EnsureToolRegistered(toolID, hashId, safeMaxDurability);
            TryApplyResetDurability(slotIndex, safeMaxDurability);
        }

        private bool TryApplyResetDurability(int slotIndex, float maxDurability)
        {
            if ((uint)slotIndex >= MaxTrackedTools || !_slotUsed[slotIndex] ||
                !EnsureItemStates(out NativeArray<ItemState> itemStates) ||
                !EnsurePendingDecay(out NativeArray<float> pendingDecayDt))
            {
                return false;
            }

            float safeMaxDurability = ResolveSafeMaxDurability(maxDurability);
            ItemState state = itemStates[slotIndex];
            state.durability = 1f;
            state.flags = 0;
            itemStates[slotIndex] = state;
            pendingDecayDt[slotIndex] = 0f;

            WriteManagedMirror(slotIndex, _toolIdBySlot[slotIndex], safeMaxDurability, false);
            PublishDurabilityChangedSignal(slotIndex, safeMaxDurability, safeMaxDurability, ItemDurabilityChangedSignal.ReasonRepair);
            return true;
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

            if (!EnsureItemStates(out NativeArray<ItemState> itemStates))
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
                WriteManagedMirror(slotIndex, pair.Key, savedDurability, false);
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
                WriteManagedMirror(slotIndex, pair.Key, 0f, true);
            }

            SyncManagedMirrorsFromNative();
        }

        private void ApplyEnvironmentalCorrosion()
        {
            if (!enableEnvironmentalCorrosion || !EnsurePlayerOwnersCached())
                return;

            if (!TryReadPlayerMovementState(out PlayerMovementRuntimeState movementState))
                return;

            float depthMeters = movementState.DepthMeters;
            if (!math.isfinite(depthMeters) || depthMeters <= UnderwaterDepthThreshold)
                return;

            bool playerInBrinePool = IsPlayerInBrinePool();
            if (playerInBrinePool)
                ApplyBrineCorrosionToTrackedTools();

            PlayerTool currentTool = _playerToolManager.CurrentTool;
            if (currentTool == null || !currentTool.IsEquipped || currentTool.IsBroken)
                return;

            ToolMetadata metadata = currentTool.RuntimeMetadata;
            if (metadata == null ||
                !currentTool.TryGetDurabilityMirror(out string toolId, out uint itemHashId, out float maxDurability))
            {
                return;
            }

            float scaledDeltaTime = heldUnderwaterCorrosionPerSecond * SlowTickDeltaTime;
            if (currentTool.WasRecentlyUsed(ActiveUseWindowSeconds))
                scaledDeltaTime += activeUseCorrosionPerSecond * SlowTickDeltaTime;

            if (TryReadPlayerSurvivalState(out PlayerSurvivalRuntimeState survivalState))
            {
                float coldStress01 = math.saturate(math.select(0f, survivalState.ColdStressSeverity01, math.isfinite(survivalState.ColdStressSeverity01)));
                float heatStress01 = math.saturate(math.select(0f, survivalState.HeatStressSeverity01, math.isfinite(survivalState.HeatStressSeverity01)));

                if (coldStress01 > 0.0001f)
                    scaledDeltaTime *= 1f + coldStress01 * coldStressCorrosionMultiplier;

                if (heatStress01 > 0.0001f)
                    scaledDeltaTime *= 1f + heatStress01 * heatStressCorrosionMultiplier;
            }

            if (scaledDeltaTime <= 0.0001f)
                return;

            DrainDurabilityByTime(toolId, itemHashId, scaledDeltaTime, maxDurability);
        }

        private void ApplyBrineCorrosionToTrackedTools()
        {
            if (!EnsurePendingDecay(out NativeArray<float> pendingDecayDt) ||
                !EnsureSlotActive(out NativeArray<byte> slotActive))
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
            IBrineFluidDensityReadModel readModel = _brineDensityReadModel;
            return readModel != null &&
                   TryResolvePlayerRuntimePosition(out Vector3 runtimePosition) &&
                   readModel.TrySampleBrineFluidDensity(runtimePosition, out float densityKgPerCubicMeter) &&
                   math.isfinite(densityKgPerCubicMeter) &&
                   densityKgPerCubicMeter >= BrineDensityThresholdKgPerCubicMeter;
        }

        private bool TryReadPlayerMovementState(out PlayerMovementRuntimeState state)
        {
            state = default;
            IPlayerRuntimeContext playerRuntimeContext = _playerRuntimeContext;
            return playerRuntimeContext != null &&
                   playerRuntimeContext.TryGetMovementRuntimeState(out state);
        }

        private bool TryReadPlayerSurvivalState(out PlayerSurvivalRuntimeState state)
        {
            state = default;
            IPlayerRuntimeContext playerRuntimeContext = _playerRuntimeContext;
            return playerRuntimeContext != null &&
                   playerRuntimeContext.TryGetSurvivalRuntimeState(out state);
        }

        private bool TryResolvePlayerRuntimePosition(out Vector3 runtimePosition)
        {
            runtimePosition = default;
            IPlayerRuntimeContext playerRuntimeContext = _playerRuntimeContext;
            if (playerRuntimeContext == null ||
                !playerRuntimeContext.TryGetPlayerPoseSnapshot(out PlayerRuntimePoseSnapshot snapshot))
                return false;

            float3 pose = snapshot.RuntimePosition;
            if (!math.all(math.isfinite(pose)))
                return false;

            runtimePosition = new Vector3(pose.x, pose.y, pose.z);
            return true;
        }

        private bool EnsurePlayerOwnersCached()
        {
            IPlayerRuntimeContext playerRuntimeContext = _playerRuntimeContext;
            if (playerRuntimeContext == null)
            {
                _playerToolManager = null;
                return false;
            }

            if (_playerToolManager == null)
                _playerToolManager = playerRuntimeContext.ToolManager;

            return _playerToolManager != null;
        }

        private void EnsureNativeStateCold()
        {
            if (!ValidateNativeLayoutCold())
                return;

            EnsureNativeStateViews(out _, out _, out _, out _, out _, createIfMissing: true);
        }

        private static bool ValidateNativeLayoutCold()
        {
            if (s_nativeLayoutValidated)
                return s_nativeLayoutValid;

            int commandSize = UnsafeUtility.SizeOf<PendingDurabilityCommand>();
            int itemStateSize = UnsafeUtility.SizeOf<ItemState>();
            s_nativeLayoutValid =
                commandSize == PendingDurabilityCommandSizeBytes &&
                (commandSize & 7) == 0 &&
                itemStateSize == ItemStateSizeBytes &&
                (itemStateSize & 7) == 0;

            s_nativeLayoutValidated = true;
            if (!s_nativeLayoutValid)
                Debug.LogError("ToolDurabilitySystem native layout violates the ARM64 8-byte DTO contract.");

            return s_nativeLayoutValid;
        }

        private void DisposeNativeState()
        {
            ClearQueuedDurabilityCommands();
            TryRunPendingDecayPass();

            ReleaseDurabilityHandles(_dataVault);
            _itemStatesHandle = default;
            _pendingDecayDtHandle = default;
            _wearMultipliersHandle = default;
            _slotActiveHandle = default;
            _breakdownFlagsHandle = default;
            _durabilitySignalFrame = 0u;
            _decayScheduled = false;
            _dataVault = null;
            _saveService = null;
            _playerRuntimeContext = null;
            _brineDensityReadModel = null;
        }

        private void ClearRuntimeState()
        {
            ClearQueuedDurabilityCommands();
            EnsureNativeStateCold();
            if (!EnsureNativeStateViews(
                    out NativeArray<ItemState> itemStates,
                    out NativeArray<float> pendingDecayDt,
                    out NativeArray<float> wearMultipliers,
                    out NativeArray<byte> slotActive,
                    out NativeArray<byte> breakdownFlags,
                    createIfMissing: true))
            {
                return;
            }

            _durabilityMap.Clear();
            _brokenMap.Clear();
            _slotByToolId.Clear();
            _slotByItemHash.Clear();
            _durabilitySignalFrame = 0u;

            for (int i = 0; i < MaxTrackedTools; i++)
            {
                _toolIdBySlot[i] = null;
                _maxDurabilityBySlot[i] = 0f;
                _itemHashBySlot[i] = 0u;
                _durabilityBySlot[i] = 0f;
                _brokenBySlot[i] = 0;
                _wearMultiplierBySlot[i] = 0f;
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

            if (!EnsureItemStates(out NativeArray<ItemState> itemStates))
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
                WriteManagedMirror(i, toolID, ResolveSafeMaxDurability(maxDurability), false);
                return i;
            }

            return -1;
        }

        private void UpdateSlotMetadata(int slotIndex, string toolID, uint itemHashId, float maxDurability)
        {
            float resolvedMaxDurability = ResolveSafeMaxDurability(maxDurability);
            uint previousHashId = _itemHashBySlot[slotIndex];
            if (previousHashId != 0u &&
                previousHashId != itemHashId &&
                _slotByItemHash.TryGetValue(previousHashId, out int previousSlot) &&
                previousSlot == slotIndex)
            {
                _slotByItemHash.Remove(previousHashId);
            }

            _toolIdBySlot[slotIndex] = toolID;
            _maxDurabilityBySlot[slotIndex] = resolvedMaxDurability;
            _itemHashBySlot[slotIndex] = itemHashId;
            if (itemHashId != 0u &&
                (!_slotByItemHash.TryGetValue(itemHashId, out int mappedSlot) || mappedSlot == slotIndex))
            {
                _slotByItemHash[itemHashId] = slotIndex;
            }

            float wearMultiplier = ResolveWearMultiplier(itemHashId);
            _wearMultiplierBySlot[slotIndex] = wearMultiplier;

            if (!EnsureItemStates(out NativeArray<ItemState> itemStates) ||
                !EnsureDurabilityView(ref _wearMultipliersHandle, BufferID.ToolDurabilityWearMultipliers, false, out NativeArray<float> wearMultipliers) ||
                !EnsureSlotActive(out NativeArray<byte> slotActive))
            {
                return;
            }

            wearMultipliers[slotIndex] = wearMultiplier;
            slotActive[slotIndex] = 1;

            ItemState state = itemStates[slotIndex];
            state.hashID = itemHashId;
            itemStates[slotIndex] = state;

            bool hasDurability = _durabilityMap.TryGetValue(toolID, out float currentDurability);
            bool hasBroken = _brokenMap.TryGetValue(toolID, out bool broken);
            if (!hasDurability)
                currentDurability = _brokenBySlot[slotIndex] != 0
                    ? 0f
                    : (_durabilityBySlot[slotIndex] > 0f ? _durabilityBySlot[slotIndex] : resolvedMaxDurability);

            if (!hasBroken)
                broken = _brokenBySlot[slotIndex] != 0;

            WriteManagedMirror(slotIndex, toolID, currentDurability, broken);
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

        private int ResolveSlot(uint itemHashId)
        {
            if (itemHashId == 0u)
                return -1;

            if (_slotByItemHash.TryGetValue(itemHashId, out int mappedSlot) &&
                (uint)mappedSlot < MaxTrackedTools &&
                _slotUsed[mappedSlot] &&
                _itemHashBySlot[mappedSlot] == itemHashId)
            {
                return mappedSlot;
            }

            for (int i = 0; i < MaxTrackedTools; i++)
            {
                if (_slotUsed[i] && _itemHashBySlot[i] == itemHashId)
                    return i;
            }

            return -1;
        }

        private void WriteManagedMirror(int slotIndex, string toolID, float durability, bool broken)
        {
            if ((uint)slotIndex >= MaxTrackedTools)
                return;

            float safeDurability = ClampFiniteNonNegative(durability);
            _durabilityBySlot[slotIndex] = safeDurability;
            _brokenBySlot[slotIndex] = broken ? (byte)1 : (byte)0;

            if (string.IsNullOrEmpty(toolID))
                return;

            _durabilityMap[toolID] = safeDurability;
            _brokenMap[toolID] = broken;
        }

        private void PublishDurabilityChangedSignal(int slotIndex, float currentDurability, float maxDurability, byte reason)
        {
            if ((uint)slotIndex >= MaxTrackedTools ||
                !EnsureItemStates(out NativeArray<ItemState> itemStates))
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
                Frame = NextDurabilitySignalFrame(),
                SlotIndex = (ushort)slotIndex,
                Reason = reason,
                Flags = flags,
                BiomeHash = 0u
            };
            SignalBus<ItemDurabilityChangedSignal>.TryPushTracked(in signal, ref s_x001ToolDurabilitySystemSignalPushDropCount);
        }

        private uint NextDurabilitySignalFrame()
        {
            uint next = _durabilitySignalFrame + 1u;
            _durabilitySignalFrame = next != 0u ? next : 1u;
            return _durabilitySignalFrame;
        }

        private bool HasNativeState()
        {
            return IsGenerationHandleCreated(in _itemStatesHandle) &&
                   IsGenerationHandleCreated(in _pendingDecayDtHandle) &&
                   IsGenerationHandleCreated(in _wearMultipliersHandle) &&
                   IsGenerationHandleCreated(in _slotActiveHandle) &&
                   IsGenerationHandleCreated(in _breakdownFlagsHandle);
        }

        private bool EnsureNativeStateViews(
            out NativeArray<ItemState> itemStates,
            out NativeArray<float> pendingDecayDt,
            out NativeArray<float> wearMultipliers,
            out NativeArray<byte> slotActive,
            out NativeArray<byte> breakdownFlags,
            bool createIfMissing)
        {
            bool itemStatesResolved = EnsureDurabilityView(ref _itemStatesHandle, BufferID.ToolDurabilityItemStates, createIfMissing, out itemStates);
            bool pendingResolved = EnsureDurabilityView(ref _pendingDecayDtHandle, BufferID.ToolDurabilityPendingDecay, createIfMissing, out pendingDecayDt);
            bool wearResolved = EnsureDurabilityView(ref _wearMultipliersHandle, BufferID.ToolDurabilityWearMultipliers, createIfMissing, out wearMultipliers);
            bool slotResolved = EnsureDurabilityView(ref _slotActiveHandle, BufferID.ToolDurabilitySlotActive, createIfMissing, out slotActive);
            bool breakdownResolved = EnsureDurabilityView(ref _breakdownFlagsHandle, BufferID.ToolDurabilityBreakdownFlags, createIfMissing, out breakdownFlags);
            return itemStatesResolved && pendingResolved && wearResolved && slotResolved && breakdownResolved;
        }

        private bool EnsureItemStates(out NativeArray<ItemState> itemStates)
        {
            return EnsureDurabilityView(ref _itemStatesHandle, BufferID.ToolDurabilityItemStates, false, out itemStates);
        }

        private bool EnsurePendingDecay(out NativeArray<float> pendingDecayDt)
        {
            return EnsureDurabilityView(ref _pendingDecayDtHandle, BufferID.ToolDurabilityPendingDecay, false, out pendingDecayDt);
        }

        private bool EnsureSlotActive(out NativeArray<byte> slotActive)
        {
            return EnsureDurabilityView(ref _slotActiveHandle, BufferID.ToolDurabilitySlotActive, false, out slotActive);
        }

        private bool EnsureBreakdownFlags(out NativeArray<byte> breakdownFlags)
        {
            return EnsureDurabilityView(ref _breakdownFlagsHandle, BufferID.ToolDurabilityBreakdownFlags, false, out breakdownFlags);
        }

        private bool EnsureDurabilityView<T>(
            ref VaultGenerationHandle<T> handle,
            BufferID bufferId,
            bool createIfMissing,
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

            if (!createIfMissing)
            {
                handle = default;
                return false;
            }

            ReleaseDurabilityHandle(vault, ref handle);
            handle = vault.EnsureGenerationHandle<T>(
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

        private bool HasPendingDecay(NativeArray<float> pendingDecayDt)
        {
            if (!pendingDecayDt.IsCreated || pendingDecayDt.Length <= 0)
                return false;

            int count = math.min(MaxTrackedTools, pendingDecayDt.Length);
            for (int i = 0; i < MaxTrackedTools; i++)
            {
                if (i >= count)
                    break;

                if (_slotUsed[i] && pendingDecayDt[i] > 0f)
                    return true;
            }

            return false;
        }

        private void SyncManagedMirrorsFromNative()
        {
            if (!_managedMirrorDirty || !EnsureItemStates(out NativeArray<ItemState> itemStates))
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

                float previousDurability = _durabilityBySlot[i];
                bool previousBroken = _brokenBySlot[i] != 0;

                WriteManagedMirror(i, toolId, currentDurability, broken);

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
            if (!EnsureBreakdownFlags(out NativeArray<byte> breakdownFlags))
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
                return true;
            }

            if (!forceComplete)
                return false;

            if (!TryRunPendingDecayPass())
                return false;

            DrainQueuedDurabilityCommands();
            return true;
        }

        private bool TryRunPendingDecayPass()
        {
            if (!_decayScheduled)
                return true;

            if (!EnsureNativeStateViews(
                    out NativeArray<ItemState> itemStates,
                    out NativeArray<float> pendingDecayDt,
                    out NativeArray<float> wearMultipliers,
                    out NativeArray<byte> slotActive,
                    out NativeArray<byte> breakdownFlags,
                    createIfMissing: false))
            {
                _decayScheduled = false;
                return false;
            }

            RunDurabilityDecayOwnerPhase(itemStates, pendingDecayDt, wearMultipliers, slotActive, breakdownFlags);
            _decayScheduled = false;
            _managedMirrorDirty = true;
            SyncManagedMirrorsFromNative();
            return true;
        }

        private void QueueDurabilityCommand(DurabilityCommandKind kind, string toolID, float amount, float maxDurability, uint itemHashId = 0u)
        {
            if (TryMergeQueuedDurabilityCommand(kind, toolID, amount, maxDurability, itemHashId))
                return;

            uint toolHashId = itemHashId != 0u ? itemHashId : unchecked((uint)Animator.StringToHash(toolID));
            PendingDurabilityCommand command = CreatePendingDurabilityCommand(kind, amount, maxDurability, itemHashId, toolHashId);
            if (_queuedDurabilityCommandCount >= MaxQueuedDurabilityCommands)
            {
                if (TryCompleteDecayJobIfScheduled(forceComplete: false))
                {
                    ApplyDurabilityCommand(in command, toolID);
                    return;
                }

                TryReplaceQueuedDurabilityCommand(in command, toolID);
                return;
            }

            _queuedDurabilityCommands[_queuedDurabilityCommandCount] = command;
            _queuedDurabilityCommandToolIds[_queuedDurabilityCommandCount] = toolID;
            _queuedDurabilityCommandCount++;
        }

        private bool QueueDurabilityCommandBySlot(DurabilityCommandKind kind, int slotIndex, float amount, float maxDurability, uint itemHashId)
        {
            if ((uint)slotIndex >= MaxTrackedTools || !_slotUsed[slotIndex])
                return false;

            string toolID = _toolIdBySlot[slotIndex];
            if (string.IsNullOrEmpty(toolID))
                return false;

            uint resolvedItemHash = itemHashId != 0u ? itemHashId : _itemHashBySlot[slotIndex];
            QueueDurabilityCommand(kind, toolID, amount, maxDurability, resolvedItemHash);
            return true;
        }

        private static PendingDurabilityCommand CreatePendingDurabilityCommand(
            DurabilityCommandKind kind,
            float amount,
            float maxDurability,
            uint itemHashId,
            uint toolHashId)
        {
            return new PendingDurabilityCommand
            {
                Amount = ClampFiniteNonNegative(amount),
                MaxDurability = ResolveSafeMaxDurability(maxDurability),
                ItemHashId = itemHashId,
                ToolHashId = toolHashId,
                Kind = kind
            };
        }

        private bool TryReplaceQueuedDurabilityCommand(in PendingDurabilityCommand command, string toolID)
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
                if (!string.Equals(_queuedDurabilityCommandToolIds[i], toolID, StringComparison.Ordinal) ||
                    !IsWearCommand(existing.Kind))
                {
                    continue;
                }

                _queuedDurabilityCommands[i] = command;
                _queuedDurabilityCommandToolIds[i] = toolID;
                return true;
            }

            for (int i = 0; i < _queuedDurabilityCommandCount; i++)
            {
                if (!IsWearCommand(_queuedDurabilityCommands[i].Kind))
                    continue;

                _queuedDurabilityCommands[i] = command;
                _queuedDurabilityCommandToolIds[i] = toolID;
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
                if (!string.Equals(_queuedDurabilityCommandToolIds[index], toolID, StringComparison.Ordinal))
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
                string toolID = _queuedDurabilityCommandToolIds[i];
                _queuedDurabilityCommands[i] = default;
                _queuedDurabilityCommandToolIds[i] = null;
                ApplyDurabilityCommand(command, toolID);
            }
        }

        private void ClearQueuedDurabilityCommands()
        {
            int count = _queuedDurabilityCommandCount;
            _queuedDurabilityCommandCount = 0;
            for (int i = 0; i < count; i++)
            {
                _queuedDurabilityCommands[i] = default;
                _queuedDurabilityCommandToolIds[i] = null;
            }
        }

        private void ApplyDurabilityCommand(in PendingDurabilityCommand command, string toolID)
        {
            if (string.IsNullOrEmpty(toolID))
                return;

            switch (command.Kind)
            {
                case DurabilityCommandKind.Repair:
                    if (command.Amount > 0f)
                        ApplyRepairTool(toolID, command.Amount, command.MaxDurability);
                    break;
                case DurabilityCommandKind.Break:
                    ApplyBreakTool(toolID);
                    break;
                case DurabilityCommandKind.Reset:
                    ApplyResetDurability(toolID, command.MaxDurability);
                    break;
                case DurabilityCommandKind.Drain:
                    if (command.Amount > 0f)
                        ApplyDrainDurability(toolID, command.Amount, command.MaxDurability);
                    break;
                case DurabilityCommandKind.DrainByTime:
                    if (command.Amount > 0f)
                        ApplyDrainDurabilityByTime(toolID, command.ItemHashId, command.Amount, command.MaxDurability);
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
            _brineDensityReadModel = GlobalRegistry.BrineFluidDensity;
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
                case GlobalRegistryServiceSlot.ResourceDistributionRuntime:
                    _brineDensityReadModel = currentService as IBrineFluidDensityReadModel;
                    break;
                case GlobalRegistryServiceSlot.Dispatcher:
                    _registeredSlowTick = false;
                    _registeredUpdate = false;
                    _registeredLateFrame = false;
                    if (currentService != null && isActiveAndEnabled)
                    {
                        TryRegisterSlowTick();
                        TryRegisterUpdate();
                        TryRegisterLateFrame();
                    }

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
            if (dataVault != null)
                EnsureNativeStateCold();
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
            if (_registeredHotSwap || !Application.isPlaying)
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
