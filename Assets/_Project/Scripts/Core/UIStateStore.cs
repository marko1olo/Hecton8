using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

namespace Hecton8.Core
{
    /// <summary>
    /// Fixed slots in the core-owned UI state array.
    /// </summary>
    public enum UIStateSlot : int
    {
        PDA = 0,
        Count = 1
    }

    /// <summary>
    /// Bit flags carried by <see cref="UIStateData"/>.
    /// </summary>
    [System.Flags]
    public enum UIStateFlags : ushort
    {
        None = 0,
        PDAOpen = 1 << 0
    }

    /// <summary>
    /// Blittable UI state snapshot owned by core simulation and read by visual UI renderers.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct UIStateData
    {
        public uint Version;
        public uint CommandSequence;
        public ushort Flags;
        public ushort ActiveTab;
        public ushort PreviousTab;
        public ushort Reserved;
        public uint LogEntryCount;
        public uint LatestLogEventHash;
        public float OpenDurationSeconds;
    }

    /// <summary>
    /// Fixed slots in the core-owned numeric UI value buffer.
    /// </summary>
    public enum UIValueSlotId : int
    {
        Oxygen01 = 0,
        Power01 = 1,
        Health01 = 2,
        DepthMeters = 3,
        PressureAtm = 4,
        SafeDepthMeters = 5,
        OxygenCurrent = 6,
        EnergyCurrent = 7,
        IntegrityCurrent = 8,
        InventoryMassKg = 9,
        CarryCapacityKg = 10,
        InventoryLoad01 = 11,
        MovementSpeed = 12,
        ToolHeat01 = 13,
        FrostIntensity01 = 14,
        WaterSurfaceY = 15,
        Count = 16
    }

    /// <summary>
    /// Blittable scalar UI value written by simulation and read by visual presenters.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct UIValueSlot
    {
        public uint Version;
        public uint Flags;
        public float Value;
        public float PreviousValue;
        public float LastWriteUnscaledTime;
    }

    /// <summary>
    /// Core-owned native UI state. Visual UI reads this data; simulation commands can mutate it without a UI GameObject.
    /// </summary>
    public static class UIStateStore
    {
        public const int MaxPdaLogEvents = 256;
        public const int UIStateHistoryFrames = 10;

        private const int StateCount = (int)UIStateSlot.Count;
        private const int ValueSlotCount = (int)UIValueSlotId.Count;
        public const uint ValueSlotInvalidInputSnappedFlag = 1u << 0;

        private static NativeArray<UIStateData> _states;
        private static NativeArray<UIValueSlot> _valueSlots;
        private static NativeArray<UIStateData> _historyStates;
        private static NativeArray<uint> _pdaLogEventHashes;
        private static NativeArray<float> _pdaLogEventTimestamps;
        private static int _pdaLogWriteIndex;
        private static int _pdaLogCount;
        private static int _historyWriteIndex;
        private static int _historyCount;

        public static bool IsInitialized =>
            _states.IsCreated &&
            _valueSlots.IsCreated &&
            _historyStates.IsCreated &&
            _pdaLogEventHashes.IsCreated &&
            _pdaLogEventTimestamps.IsCreated;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            Shutdown();
        }

        /// <summary>
        /// Ensures the persistent native UI state arrays are allocated.
        /// </summary>
        public static void EnsureInitialized()
        {
            if (IsInitialized)
                return;

            Shutdown();
            _states = new NativeArray<UIStateData>(StateCount, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<UIStateData>[StateCount] - headless UI simulation state - owner: UIStateStore
            _valueSlots = new NativeArray<UIValueSlot>(ValueSlotCount, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<UIValueSlot>[ValueSlotCount] - headless numeric UI value bridge - owner: UIStateStore
            _historyStates = new NativeArray<UIStateData>(UIStateHistoryFrames, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<UIStateData>[UIStateHistoryFrames] - PDA UI rollback snapshot ring - owner: UIStateStore
            _pdaLogEventHashes = new NativeArray<uint>(MaxPdaLogEvents, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<uint>[MaxPdaLogEvents] - PDA event-sourced log history - owner: UIStateStore
            _pdaLogEventTimestamps = new NativeArray<float>(MaxPdaLogEvents, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<float>[MaxPdaLogEvents] - PDA event-sourced log timestamps - owner: UIStateStore
            NativeMemorySentinel.RegisterNativeArray(_states, nameof(UIStateStore), nameof(_states), NativeAllocationLifetime.Session);
            NativeMemorySentinel.RegisterNativeArray(_valueSlots, nameof(UIStateStore), nameof(_valueSlots), NativeAllocationLifetime.Session);
            NativeMemorySentinel.RegisterNativeArray(_historyStates, nameof(UIStateStore), nameof(_historyStates), NativeAllocationLifetime.Session);
            NativeMemorySentinel.RegisterNativeArray(_pdaLogEventHashes, nameof(UIStateStore), nameof(_pdaLogEventHashes), NativeAllocationLifetime.Session);
            NativeMemorySentinel.RegisterNativeArray(_pdaLogEventTimestamps, nameof(UIStateStore), nameof(_pdaLogEventTimestamps), NativeAllocationLifetime.Session);
            _pdaLogWriteIndex = 0;
            _pdaLogCount = 0;
            _historyWriteIndex = 0;
            _historyCount = 0;
        }

        /// <summary>
        /// Returns a copy of the PDA state slot.
        /// </summary>
        public static UIStateData GetPDAState()
        {
            EnsureInitialized();
            return _states[(int)UIStateSlot.PDA];
        }

        /// <summary>
        /// Returns a read-only view over the core-owned UI state array for visual presenters.
        /// </summary>
        public static NativeArray<UIStateData>.ReadOnly GetReadOnlyStates()
        {
            EnsureInitialized();
            return _states.AsReadOnly();
        }

        /// <summary>
        /// Returns a read-only view over the flat numeric value slots.
        /// </summary>
        public static NativeArray<UIValueSlot>.ReadOnly GetReadOnlyValueSlots()
        {
            EnsureInitialized();
            return _valueSlots.AsReadOnly();
        }

        /// <summary>
        /// Reads a numeric UI slot if simulation has written it at least once.
        /// </summary>
        public static bool TryReadValue(UIValueSlotId slotId, out UIValueSlot valueSlot)
        {
            valueSlot = default;
            int index = (int)slotId;
            if ((uint)index >= ValueSlotCount)
                return false;

            EnsureInitialized();
            valueSlot = _valueSlots[index];
            return valueSlot.Version != 0u;
        }

        /// <summary>
        /// Reads a numeric UI slot or returns the supplied fallback when the slot is unwritten.
        /// </summary>
        public static float ReadValueOrDefault(UIValueSlotId slotId, float fallback)
        {
            return TryReadValue(slotId, out UIValueSlot valueSlot)
                ? valueSlot.Value
                : fallback;
        }

        /// <summary>
        /// Writes the authoritative PDA open state and active tab.
        /// </summary>
        internal static void SetPDAOpenState(bool isOpen, int activeTab, float openDurationSeconds)
        {
            EnsureInitialized();
            int tab = Mathf.Clamp(activeTab, 0, ushort.MaxValue);
            UIStateData state = _states[(int)UIStateSlot.PDA];
            CapturePDAStateSnapshot(in state);
            state.Version++;
            state.CommandSequence++;
            state.Flags = isOpen
                ? (ushort)(state.Flags | (ushort)UIStateFlags.PDAOpen)
                : (ushort)(state.Flags & ~(ushort)UIStateFlags.PDAOpen);
            state.PreviousTab = state.ActiveTab;
            state.ActiveTab = (ushort)tab;
            state.OpenDurationSeconds = Mathf.Max(0f, openDurationSeconds);
            _states[(int)UIStateSlot.PDA] = state;
        }

        /// <summary>
        /// Writes the authoritative PDA active tab without changing the open flag.
        /// </summary>
        internal static void SetPDAActiveTab(int previousTab, int currentTab)
        {
            EnsureInitialized();
            UIStateData state = _states[(int)UIStateSlot.PDA];
            CapturePDAStateSnapshot(in state);
            state.Version++;
            state.CommandSequence++;
            state.PreviousTab = (ushort)Mathf.Clamp(previousTab, 0, ushort.MaxValue);
            state.ActiveTab = (ushort)Mathf.Clamp(currentTab, 0, ushort.MaxValue);
            _states[(int)UIStateSlot.PDA] = state;
        }

        /// <summary>
        /// Updates PDA logbook counters without storing any managed log strings.
        /// </summary>
        internal static void SetPDALogbookState(int entryCount, uint latestEventHash)
        {
            EnsureInitialized();
            UIStateData state = _states[(int)UIStateSlot.PDA];
            CapturePDAStateSnapshot(in state);
            state.Version++;
            state.LogEntryCount = (uint)Mathf.Max(0, entryCount);
            state.LatestLogEventHash = latestEventHash;
            _states[(int)UIStateSlot.PDA] = state;
        }

        /// <summary>
        /// Appends one event-sourced PDA log hash into the fixed native ring.
        /// </summary>
        internal static void AppendPDALogEventHash(uint eventHash)
        {
            AppendPDALogEventHash(eventHash, Time.unscaledTime);
        }

        /// <summary>
        /// Appends one event-sourced PDA log hash and timestamp into fixed native rings.
        /// </summary>
        internal static void AppendPDALogEventHash(uint eventHash, float timestampSeconds)
        {
            if (eventHash == 0u)
                return;

            EnsureInitialized();
            _pdaLogEventHashes[_pdaLogWriteIndex] = eventHash;
            _pdaLogEventTimestamps[_pdaLogWriteIndex] = Mathf.Max(0f, timestampSeconds);
            _pdaLogWriteIndex++;
            if (_pdaLogWriteIndex >= MaxPdaLogEvents)
                _pdaLogWriteIndex = 0;

            if (_pdaLogCount < MaxPdaLogEvents)
                _pdaLogCount++;

            UIStateData state = _states[(int)UIStateSlot.PDA];
            CapturePDAStateSnapshot(in state);
            state.Version++;
            state.LogEntryCount = (uint)_pdaLogCount;
            state.LatestLogEventHash = eventHash;
            _states[(int)UIStateSlot.PDA] = state;
        }

        /// <summary>
        /// Writes one numeric UI value slot without requiring any visual UI object to exist.
        /// </summary>
        public static void WriteValue(UIValueSlotId slotId, float value, float unscaledTimeSeconds)
        {
            int index = (int)slotId;
            if ((uint)index >= ValueSlotCount)
                return;

            EnsureInitialized();
            UIValueSlot valueSlot = _valueSlots[index];
            bool inputIsFinite = IsFinite(value);
            float previousValidValue = IsFinite(valueSlot.Value)
                ? valueSlot.Value
                : IsFinite(valueSlot.PreviousValue)
                    ? valueSlot.PreviousValue
                    : 0f;
            valueSlot.PreviousValue = previousValidValue;
            valueSlot.Value = inputIsFinite ? value : previousValidValue;
            valueSlot.Flags = inputIsFinite
                ? valueSlot.Flags & ~ValueSlotInvalidInputSnappedFlag
                : valueSlot.Flags | ValueSlotInvalidInputSnappedFlag;
            valueSlot.LastWriteUnscaledTime = Mathf.Max(0f, unscaledTimeSeconds);
            valueSlot.Version++;
            _valueSlots[index] = valueSlot;
        }

        /// <summary>
        /// Publishes the survival-facing HUD scalar snapshot into the headless value buffer.
        /// </summary>
        public static void WriteHUDSurvivalState(
            float oxygen01,
            float power01,
            float health01,
            float depthMeters,
            float pressureAtm,
            float safeDepthMeters,
            float oxygenCurrent,
            float energyCurrent,
            float integrityCurrent,
            float inventoryMassKg,
            float carryCapacityKg,
            float inventoryLoad01,
            float unscaledTimeSeconds)
        {
            WriteValue(UIValueSlotId.Oxygen01, Mathf.Clamp01(oxygen01), unscaledTimeSeconds);
            WriteValue(UIValueSlotId.Power01, Mathf.Clamp01(power01), unscaledTimeSeconds);
            WriteValue(UIValueSlotId.Health01, Mathf.Clamp01(health01), unscaledTimeSeconds);
            WriteValue(UIValueSlotId.DepthMeters, Mathf.Max(0f, depthMeters), unscaledTimeSeconds);
            WriteValue(UIValueSlotId.PressureAtm, Mathf.Max(1f, pressureAtm), unscaledTimeSeconds);
            WriteValue(UIValueSlotId.SafeDepthMeters, Mathf.Max(0f, safeDepthMeters), unscaledTimeSeconds);
            WriteValue(UIValueSlotId.OxygenCurrent, Mathf.Max(0f, oxygenCurrent), unscaledTimeSeconds);
            WriteValue(UIValueSlotId.EnergyCurrent, Mathf.Max(0f, energyCurrent), unscaledTimeSeconds);
            WriteValue(UIValueSlotId.IntegrityCurrent, Mathf.Max(0f, integrityCurrent), unscaledTimeSeconds);
            WriteInventoryLoadState(inventoryMassKg, carryCapacityKg, inventoryLoad01, unscaledTimeSeconds);
        }

        /// <summary>
        /// Publishes the inventory load scalar snapshot into the headless value buffer.
        /// </summary>
        public static void WriteInventoryLoadState(float totalMassKg, float carryCapacityKg, float load01, float unscaledTimeSeconds)
        {
            WriteValue(UIValueSlotId.InventoryMassKg, Mathf.Max(0f, totalMassKg), unscaledTimeSeconds);
            WriteValue(UIValueSlotId.CarryCapacityKg, Mathf.Max(0.01f, carryCapacityKg), unscaledTimeSeconds);
            WriteValue(UIValueSlotId.InventoryLoad01, Mathf.Clamp01(load01), unscaledTimeSeconds);
        }

        /// <summary>
        /// Publishes hypothermia frost intensity for HUD shader presenters.
        /// </summary>
        public static void WriteFrostIntensity(float frostIntensity01, float unscaledTimeSeconds)
        {
            WriteValue(UIValueSlotId.FrostIntensity01, Mathf.Clamp01(frostIntensity01), unscaledTimeSeconds);
        }

        /// <summary>
        /// Resolves a log event hash by newest-first index.
        /// </summary>
        public static bool TryGetPDALogEventHash(int newestFirstIndex, out uint eventHash)
        {
            eventHash = 0u;
            return TryGetPDALogEvent(newestFirstIndex, out eventHash, out _);
        }

        /// <summary>
        /// Resolves a log event hash and timestamp by newest-first index.
        /// </summary>
        public static bool TryGetPDALogEvent(int newestFirstIndex, out uint eventHash, out float timestampSeconds)
        {
            eventHash = 0u;
            timestampSeconds = 0f;
            EnsureInitialized();
            if ((uint)newestFirstIndex >= (uint)_pdaLogCount)
                return false;

            int index = _pdaLogWriteIndex - 1 - newestFirstIndex;
            if (index < 0)
                index += MaxPdaLogEvents;

            eventHash = _pdaLogEventHashes[index];
            timestampSeconds = _pdaLogEventTimestamps[index];
            return eventHash != 0u;
        }

        /// <summary>
        /// Restores the PDA simulation state from the fixed rollback ring.
        /// </summary>
        public static bool TryRollbackPDAState(int framesBack)
        {
            EnsureInitialized();
            if (_historyCount <= 0)
                return false;

            int safeFramesBack = Mathf.Clamp(framesBack, 1, _historyCount);
            int index = _historyWriteIndex - safeFramesBack;
            if (index < 0)
                index += UIStateHistoryFrames;

            UIStateData restored = _historyStates[index];
            UIStateData current = _states[(int)UIStateSlot.PDA];
            restored.Version = current.Version + 1u;
            restored.CommandSequence = current.CommandSequence + 1u;
            _states[(int)UIStateSlot.PDA] = restored;
            return true;
        }

        /// <summary>
        /// Clears transient UI state while keeping the native arrays resident.
        /// </summary>
        public static void Clear()
        {
            EnsureInitialized();
            for (int i = 0; i < _states.Length; i++)
                _states[i] = default;
            for (int i = 0; i < _valueSlots.Length; i++)
                _valueSlots[i] = default;
            for (int i = 0; i < _historyStates.Length; i++)
                _historyStates[i] = default;
            for (int i = 0; i < _pdaLogEventHashes.Length; i++)
                _pdaLogEventHashes[i] = 0u;
            for (int i = 0; i < _pdaLogEventTimestamps.Length; i++)
                _pdaLogEventTimestamps[i] = 0f;
            _pdaLogWriteIndex = 0;
            _pdaLogCount = 0;
            _historyWriteIndex = 0;
            _historyCount = 0;
        }

        /// <summary>
        /// Releases persistent native UI state arrays.
        /// </summary>
        public static void Shutdown()
        {
            JobHandle noDependency = default;
            DisposeNativeArray(ref _states, noDependency);
            DisposeNativeArray(ref _valueSlots, noDependency);
            DisposeNativeArray(ref _historyStates, noDependency);
            DisposeNativeArray(ref _pdaLogEventHashes, noDependency);
            DisposeNativeArray(ref _pdaLogEventTimestamps, noDependency);

            _pdaLogWriteIndex = 0;
            _pdaLogCount = 0;
            _historyWriteIndex = 0;
            _historyCount = 0;
        }

        private static void DisposeNativeArray<T>(ref NativeArray<T> array, JobHandle dependency) where T : struct
        {
            if (!array.IsCreated)
                return;

            NativeMemorySentinel.UnregisterNativeArray(array);
            array.Dispose(dependency);
            array = default;
        }

        private static void CapturePDAStateSnapshot(in UIStateData state)
        {
            _historyStates[_historyWriteIndex] = state;
            _historyWriteIndex++;
            if (_historyWriteIndex >= UIStateHistoryFrames)
                _historyWriteIndex = 0;

            if (_historyCount < UIStateHistoryFrames)
                _historyCount++;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
