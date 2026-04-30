using System.Runtime.InteropServices;
using Unity.Collections;
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
    /// Core-owned native UI state. Visual UI reads this data; simulation commands can mutate it without a UI GameObject.
    /// </summary>
    public static class UIStateStore
    {
        public const int MaxPdaLogEvents = 256;

        private const int StateCount = (int)UIStateSlot.Count;

        private static NativeArray<UIStateData> _states;
        private static NativeArray<uint> _pdaLogEventHashes;
        private static int _pdaLogWriteIndex;
        private static int _pdaLogCount;

        public static bool IsInitialized => _states.IsCreated && _pdaLogEventHashes.IsCreated;

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
            if (_states.IsCreated && _pdaLogEventHashes.IsCreated)
                return;

            Shutdown();
            _states = new NativeArray<UIStateData>(StateCount, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<UIStateData>[StateCount] - headless UI simulation state - owner: UIStateStore
            _pdaLogEventHashes = new NativeArray<uint>(MaxPdaLogEvents, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<uint>[MaxPdaLogEvents] - PDA event-sourced log history - owner: UIStateStore
            _pdaLogWriteIndex = 0;
            _pdaLogCount = 0;
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
        /// Writes the authoritative PDA open state and active tab.
        /// </summary>
        public static void SetPDAOpenState(bool isOpen, int activeTab, float openDurationSeconds)
        {
            EnsureInitialized();
            int tab = Mathf.Clamp(activeTab, 0, ushort.MaxValue);
            UIStateData state = _states[(int)UIStateSlot.PDA];
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
        public static void SetPDAActiveTab(int previousTab, int currentTab)
        {
            EnsureInitialized();
            UIStateData state = _states[(int)UIStateSlot.PDA];
            state.Version++;
            state.CommandSequence++;
            state.PreviousTab = (ushort)Mathf.Clamp(previousTab, 0, ushort.MaxValue);
            state.ActiveTab = (ushort)Mathf.Clamp(currentTab, 0, ushort.MaxValue);
            _states[(int)UIStateSlot.PDA] = state;
        }

        /// <summary>
        /// Updates PDA logbook counters without storing any managed log strings.
        /// </summary>
        public static void SetPDALogbookState(int entryCount, uint latestEventHash)
        {
            EnsureInitialized();
            UIStateData state = _states[(int)UIStateSlot.PDA];
            state.Version++;
            state.LogEntryCount = (uint)Mathf.Max(0, entryCount);
            state.LatestLogEventHash = latestEventHash;
            _states[(int)UIStateSlot.PDA] = state;
        }

        /// <summary>
        /// Appends one event-sourced PDA log hash into the fixed native ring.
        /// </summary>
        public static void AppendPDALogEventHash(uint eventHash)
        {
            if (eventHash == 0u)
                return;

            EnsureInitialized();
            _pdaLogEventHashes[_pdaLogWriteIndex] = eventHash;
            _pdaLogWriteIndex++;
            if (_pdaLogWriteIndex >= MaxPdaLogEvents)
                _pdaLogWriteIndex = 0;

            if (_pdaLogCount < MaxPdaLogEvents)
                _pdaLogCount++;

            UIStateData state = _states[(int)UIStateSlot.PDA];
            state.Version++;
            state.LogEntryCount = (uint)_pdaLogCount;
            state.LatestLogEventHash = eventHash;
            _states[(int)UIStateSlot.PDA] = state;
        }

        /// <summary>
        /// Resolves a log event hash by newest-first index.
        /// </summary>
        public static bool TryGetPDALogEventHash(int newestFirstIndex, out uint eventHash)
        {
            eventHash = 0u;
            EnsureInitialized();
            if ((uint)newestFirstIndex >= (uint)_pdaLogCount)
                return false;

            int index = _pdaLogWriteIndex - 1 - newestFirstIndex;
            if (index < 0)
                index += MaxPdaLogEvents;

            eventHash = _pdaLogEventHashes[index];
            return eventHash != 0u;
        }

        /// <summary>
        /// Clears transient UI state while keeping the native arrays resident.
        /// </summary>
        public static void Clear()
        {
            EnsureInitialized();
            for (int i = 0; i < _states.Length; i++)
                _states[i] = default;
            for (int i = 0; i < _pdaLogEventHashes.Length; i++)
                _pdaLogEventHashes[i] = 0u;
            _pdaLogWriteIndex = 0;
            _pdaLogCount = 0;
        }

        /// <summary>
        /// Releases persistent native UI state arrays.
        /// </summary>
        public static void Shutdown()
        {
            if (_states.IsCreated)
            {
                _states.Dispose();
                _states = default;
            }

            if (_pdaLogEventHashes.IsCreated)
            {
                _pdaLogEventHashes.Dispose();
                _pdaLogEventHashes = default;
            }

            _pdaLogWriteIndex = 0;
            _pdaLogCount = 0;
        }
    }
}
