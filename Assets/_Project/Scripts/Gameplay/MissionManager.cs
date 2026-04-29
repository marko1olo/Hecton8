using System.Collections.Generic;
using Hecton8.Quest;
using UnityEngine;

namespace Hecton8.Gameplay
{
    /// <summary>
    /// Compatibility facade over the packed quest runtime.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MissionManager : MonoBehaviour
    {
        public sealed class MissionInstance
        {
            public MissionInstance(string missionId)
            {
                MissionId = missionId ?? string.Empty;
            }

            public string MissionId { get; }
        }

        // COLD ALLOC: Dictionary<string,MissionInstance>[32] - compatibility facade active mission cache - owner: MissionManager
        private readonly Dictionary<string, MissionInstance> _activeMissions = new Dictionary<string, MissionInstance>(32);
        // COLD ALLOC: HashSet<string>[32] - compatibility facade completed mission cache - owner: MissionManager
        private readonly HashSet<string> _completedMissions = new HashSet<string>();

        public static MissionManager Instance { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
        }

        private void OnEnable()
        {
            QuestEvents.OnQuestActivated += HandleQuestActivated;
            QuestEvents.OnQuestCompleted += HandleQuestCompleted;
        }

        private void OnDisable()
        {
            QuestEvents.OnQuestActivated -= HandleQuestActivated;
            QuestEvents.OnQuestCompleted -= HandleQuestCompleted;
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        public void StartMission(string missionId)
        {
            if (string.IsNullOrWhiteSpace(missionId))
                return;

            if (_completedMissions.Contains(missionId) || _activeMissions.ContainsKey(missionId))
                return;

            QuestManager questManager = QuestManager.Instance;
            if (questManager == null)
                return;

            questManager.ActivateQuest(missionId);
            if (questManager.IsActive(missionId))
                EnsureActiveInstance(missionId);
        }

        public void CompleteObjective(string missionId, string objectiveId)
        {
            if (string.IsNullOrWhiteSpace(missionId) || string.IsNullOrWhiteSpace(objectiveId))
                return;

            QuestManager questManager = QuestManager.Instance;
            if (questManager == null || !questManager.IsActive(missionId))
                return;

            questManager.CompleteQuest(missionId);
        }

        public MissionInstance GetActiveMission(string missionId)
        {
            if (string.IsNullOrWhiteSpace(missionId))
                return null;

            QuestManager questManager = QuestManager.Instance;
            if (questManager != null && questManager.IsActive(missionId))
                return EnsureActiveInstance(missionId);

            _activeMissions.Remove(missionId);
            return null;
        }

        public IEnumerable<MissionInstance> GetActiveMissions()
        {
            return _activeMissions.Values;
        }

        public bool IsMissionCompleted(string missionId)
        {
            if (string.IsNullOrWhiteSpace(missionId))
                return false;

            if (_completedMissions.Contains(missionId))
                return true;

            QuestManager questManager = QuestManager.Instance;
            return questManager != null && questManager.IsCompleted(missionId);
        }

        private MissionInstance EnsureActiveInstance(string missionId)
        {
            if (!_activeMissions.TryGetValue(missionId, out MissionInstance instance))
            {
                instance = new MissionInstance(missionId); // COLD ALLOC: MissionInstance[1] - compatibility facade wrapper for quest-backed mission - owner: MissionManager
                _activeMissions.Add(missionId, instance);
            }

            return instance;
        }

        private void HandleQuestActivated(string questId)
        {
            if (string.IsNullOrWhiteSpace(questId))
                return;

            EnsureActiveInstance(questId);
            _completedMissions.Remove(questId);
        }

        private void HandleQuestCompleted(string questId)
        {
            if (string.IsNullOrWhiteSpace(questId))
                return;

            _activeMissions.Remove(questId);
            _completedMissions.Add(questId);
        }
    }
}
