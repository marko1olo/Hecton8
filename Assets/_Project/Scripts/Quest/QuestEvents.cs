// ============================================================================
// HECTON-8 — QuestEvents.cs
// Статическая шина событий квестовой системы. Zero GC.
// ============================================================================

using System;

namespace Hecton8.Quest
{
    public static class QuestEvents
    {
        [UnityEngine.RuntimeInitializeOnLoadMethod(
            UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            OnQuestActivated = null;
            OnQuestCompleted = null;
            OnQuestFailed = null;
        }

        /// <summary>Квест активирован. string: questId.</summary>
        public static event Action<string> OnQuestActivated;

        /// <summary>Квест завершён. string: questId.</summary>
        public static event Action<string> OnQuestCompleted;

        /// <summary>Квест провален. string: questId.</summary>
        public static event Action<string> OnQuestFailed;

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
    }
}
