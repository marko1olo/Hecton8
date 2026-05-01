using System.Diagnostics;
using Hecton8.Core;
using Unity.Collections;

namespace Hecton8.SaveSystem
{
    public enum SaveEventType : byte
    {
        SaveStarted = 0,
        SaveCompleted = 1,
        SaveFailed = 2,
        LoadStarted = 3,
        LoadCompleted = 4,
        LoadFailed = 5,
        EmergencyBackupRestoreRequested = 6
    }

    public struct SaveEventPayload
    {
        public SaveEventType Type;
        public ulong TimestampTicks;
        public FixedString64Bytes SlotName;
        public FixedString128Bytes Message;
    }

    public interface ISaveEventListener
    {
        void OnSaveEvent(in SaveEventPayload payload);
    }

    public static class SaveEvents
    {
        // COLD ALLOC: RegistryBucket<ISaveEventListener>[16] - save event listener registry drained on dispatcher LateUpdate - owner: SaveEvents
        private static readonly RegistryBucket<ISaveEventListener> _listeners = new RegistryBucket<ISaveEventListener>(16);
        private static NativeQueue<SaveEventPayload> _pendingEvents;

        public static int PendingCount => _pendingEvents.IsCreated ? _pendingEvents.Count : 0;

        [UnityEngine.RuntimeInitializeOnLoadMethod(
            UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            if (_pendingEvents.IsCreated)
            {
                _pendingEvents.Dispose();
                _pendingEvents = default;
            }

            _listeners.Clear();
        }

        public static void Register(ISaveEventListener listener)
        {
            if (listener == null)
                return;

            EnsureInitialized();
            _listeners.Register(listener);
        }

        public static void Unregister(ISaveEventListener listener)
        {
            if (listener == null)
                return;

            _listeners.Unregister(listener);
        }

        public static void FlushPending()
        {
            if (!_pendingEvents.IsCreated || _listeners.Count <= 0)
            {
                DrainWithoutDispatch();
                return;
            }

            while (!_pendingEvents.IsEmpty())
            {
                if (!SystemDispatcher.TryConsumeLateFrameEventDispatch())
                    return;

                if (!_pendingEvents.TryDequeue(out SaveEventPayload payload))
                    return;

                ISaveEventListener[] rawArray = _listeners.RawArray;
                int count = _listeners.Count;
                for (int i = count - 1; i >= 0; i--)
                    rawArray[i].OnSaveEvent(in payload);
            }
        }

        public static void RaiseSaveStarted(string slot)
        {
            Enqueue(SaveEventType.SaveStarted, slot, default);
        }

        public static void RaiseSaveCompleted(string slot)
        {
            Enqueue(SaveEventType.SaveCompleted, slot, default);
        }

        public static void RaiseSaveFailed(string slot, string error)
        {
            Enqueue(SaveEventType.SaveFailed, slot, error);
        }

        public static void RaiseLoadStarted(string slot)
        {
            Enqueue(SaveEventType.LoadStarted, slot, default);
        }

        public static void RaiseLoadCompleted(string slot)
        {
            Enqueue(SaveEventType.LoadCompleted, slot, default);
        }

        public static void RaiseLoadFailed(string slot, string error)
        {
            Enqueue(SaveEventType.LoadFailed, slot, error);
        }

        public static void RaiseEmergencyBackupRestoreRequested(string slot)
        {
            Enqueue(SaveEventType.EmergencyBackupRestoreRequested, slot, default);
        }

        private static void EnsureInitialized()
        {
            if (!_pendingEvents.IsCreated)
            {
                _pendingEvents = new NativeQueue<SaveEventPayload>(Allocator.Persistent); // COLD ALLOC: NativeQueue<SaveEventPayload>[16] - deferred save event lane flushed by SystemDispatcher LateUpdate - owner: SaveEvents
            }
        }

        private static void Enqueue(SaveEventType type, string slot, string message)
        {
            EnsureInitialized();
            _pendingEvents.Enqueue(new SaveEventPayload
            {
                Type = type,
                TimestampTicks = unchecked((ulong)Stopwatch.GetTimestamp()),
                SlotName = string.IsNullOrEmpty(slot) ? default : slot,
                Message = string.IsNullOrEmpty(message) ? default : message
            });
        }

        private static void DrainWithoutDispatch()
        {
            if (!_pendingEvents.IsCreated)
                return;

            while (_pendingEvents.TryDequeue(out _))
            {
            }
        }
    }
}
