using System.Runtime.InteropServices;
using Hecton8.Core;
using Unity.Collections;

namespace Hecton8.Quest
{
    public readonly struct QuestRevertRequest
    {
        public QuestRevertRequest(uint questHash, uint itemHash, uint respawnEventHash, int questIndex)
        {
            QuestHash = questHash;
            ItemHash = itemHash;
            RespawnEventHash = respawnEventHash;
            QuestIndex = questIndex;
        }

        public uint QuestHash { get; }
        public uint ItemHash { get; }
        public uint RespawnEventHash { get; }
        public int QuestIndex { get; }
    }

    public enum QuestEventType : byte
    {
        Activated = 0,
        Completed = 1,
        Failed = 2,
        RevertRequested = 3
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct QuestEventPayload
    {
        public uint QuestHashID;
        public ushort EventType;
        public ushort Reserved;
    }

    public interface IQuestEventListener
    {
        void OnQuestEvent(in QuestEventPayload payload);
    }

    public static class QuestEvents
    {
        // COLD ALLOC: RegistryBucket<IQuestEventListener>[16] - quest event listener registry drained on dispatcher LateUpdate - owner: QuestEvents
        private static readonly RegistryBucket<IQuestEventListener> _listeners = new RegistryBucket<IQuestEventListener>(16);
        private static NativeQueue<QuestEventPayload> _pendingEvents;

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

        public static void Register(IQuestEventListener listener)
        {
            if (listener == null)
                return;

            EnsureInitialized();
            _listeners.Register(listener);
        }

        public static void Unregister(IQuestEventListener listener)
        {
            if (listener == null)
                return;

            _listeners.Unregister(listener);
        }

        public static void FlushPending()
        {
            QuestGraphEvaluator.FlushPendingSignals();

            if (!_pendingEvents.IsCreated || _listeners.Count <= 0)
            {
                DrainWithoutDispatch();
                return;
            }

            while (!_pendingEvents.IsEmpty())
            {
                if (!SystemDispatcher.TryConsumeLateFrameEventDispatch())
                    return;

                if (!_pendingEvents.TryDequeue(out QuestEventPayload payload))
                    return;

                IQuestEventListener[] rawArray = _listeners.RawArray;
                int count = _listeners.Count;
                for (int i = count - 1; i >= 0; i--)
                    rawArray[i].OnQuestEvent(in payload);
            }
        }

        public static void RaiseActivated(uint questHash)
        {
            Enqueue(QuestEventType.Activated, questHash);
        }

        public static void RaiseCompleted(uint questHash)
        {
            Enqueue(QuestEventType.Completed, questHash);
        }

        public static void RaiseFailed(uint questHash)
        {
            Enqueue(QuestEventType.Failed, questHash);
        }

        public static void RaiseRevertRequested(in QuestRevertRequest request)
        {
            Enqueue(QuestEventType.RevertRequested, request.QuestHash);
        }

        private static void EnsureInitialized()
        {
            if (!_pendingEvents.IsCreated)
            {
                _pendingEvents = new NativeQueue<QuestEventPayload>(Allocator.Persistent); // COLD ALLOC: NativeQueue<QuestEventPayload>[16] - deferred quest event lane flushed by SystemDispatcher LateUpdate - owner: QuestEvents
            }
        }

        private static void Enqueue(QuestEventType type, uint questHash)
        {
            EnsureInitialized();
            _pendingEvents.Enqueue(new QuestEventPayload
            {
                QuestHashID = questHash,
                EventType = (ushort)type,
                Reserved = 0
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
