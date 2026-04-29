// ============================================================================
// HECTON-8 — QuestEvents.cs
// Статическая шина событий квестовой системы. Zero GC.
// ============================================================================

using System;

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

    public static class QuestEvents
    {
        [UnityEngine.RuntimeInitializeOnLoadMethod(
            UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            OnQuestActivated = null;
            OnQuestCompleted = null;
            OnQuestFailed = null;
            OnQuestRevertRequested = null;
        }

        /// <summary>Квест активирован. string: questId.</summary>
        public static event Action<string> OnQuestActivated;

        /// <summary>Квест завершён. string: questId.</summary>
        public static event Action<string> OnQuestCompleted;

        /// <summary>Квест провален. string: questId.</summary>
        public static event Action<string> OnQuestFailed;

        /// <summary>Критический квестовый предмет утрачен. Consumers should re-spawn the authored item for reacquisition.</summary>
        public static event Action<QuestRevertRequest> OnQuestRevertRequested;

        public static void RaiseActivated(string questId)
        {
            if (string.IsNullOrEmpty(questId)) return;
            OnQuestActivated?.Invoke(questId);
        }

        public static void RaiseCompleted(string questId)
        {
            if (string.IsNullOrEmpty(questId)) return;
            OnQuestCompleted?.Invoke(questId);
        }

        public static void RaiseFailed(string questId)
        {
            if (string.IsNullOrEmpty(questId)) return;
            OnQuestFailed?.Invoke(questId);
        }

        public static void RaiseRevertRequested(in QuestRevertRequest request)
        {
            var handler = OnQuestRevertRequested;
            handler?.Invoke(request);
        }
    }
}
