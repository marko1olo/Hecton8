using System.Collections.Generic;
using Hecton8.Core;
using Hecton8.Quest;
using UnityEngine;

namespace Hecton8.Gameplay
{
    /// <summary>
    /// Compatibility facade over the packed quest runtime.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MissionManager : MonoBehaviour, IQuestEventListener
    {
        public sealed class MissionInstance
        {
            public MissionInstance(string missionId)
            {
                MissionId = missionId ?? string.Empty;
            }

            public string MissionId { get; }
        }

        // COLD ALLOC: Dictionary<uint,MissionInstance>[32] - compatibility facade active mission cache keyed by FNV quest hash - owner: MissionManager
        private readonly Dictionary<uint, MissionInstance> _activeMissions = new Dictionary<uint, MissionInstance>(32);
        // COLD ALLOC: HashSet<uint>[32] - compatibility facade completed mission cache keyed by FNV quest hash - owner: MissionManager
        private readonly HashSet<uint> _completedMissions = new HashSet<uint>();
        private bool _serviceRegistered;

        public static MissionManager Instance { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            Instance = null;
        }

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
            TryRegisterService();
            QuestEvents.Register(this);
        }

        private void OnDisable()
        {
            QuestEvents.Unregister(this);
            TryUnregisterService();
        }

        private void OnDestroy()
        {
            TryUnregisterService();

            if (Instance == this)
                Instance = null;
        }

        private void TryRegisterService()
        {
            if (_serviceRegistered || !Application.isPlaying || Instance != this)
                return;

            GlobalRegistry.RegisterMissionRuntime(this);
            _serviceRegistered = true;
        }

        private void TryUnregisterService()
        {
            if (!_serviceRegistered)
                return;

            GlobalRegistry.UnregisterMissionRuntime(this);
            _serviceRegistered = false;
        }

        public void StartMission(string missionId)
        {
            if (string.IsNullOrWhiteSpace(missionId))
                return;

            uint missionHash = ComputeMissionHash(missionId);
            if (missionHash == 0u || _completedMissions.Contains(missionHash) || _activeMissions.ContainsKey(missionHash))
                return;

            QuestManager questManager = GlobalRegistry.Quest;
            if (questManager == null)
                return;

            questManager.ActivateQuest(missionId);
            if (questManager.IsActive(missionId))
                EnsureActiveInstance(missionHash, missionId);
        }

        public void CompleteObjective(string missionId, string objectiveId)
        {
            if (string.IsNullOrWhiteSpace(missionId) || string.IsNullOrWhiteSpace(objectiveId))
                return;

            QuestManager questManager = GlobalRegistry.Quest;
            if (questManager == null || !questManager.IsActive(missionId))
                return;

            questManager.CompleteQuest(missionId);
        }

        public MissionInstance GetActiveMission(string missionId)
        {
            if (string.IsNullOrWhiteSpace(missionId))
                return null;

            uint missionHash = ComputeMissionHash(missionId);
            if (missionHash == 0u)
                return null;

            QuestManager questManager = GlobalRegistry.Quest;
            if (questManager != null && questManager.IsActive(missionId))
                return EnsureActiveInstance(missionHash, missionId);

            _activeMissions.Remove(missionHash);
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

            uint missionHash = ComputeMissionHash(missionId);
            if (missionHash == 0u)
                return false;

            if (_completedMissions.Contains(missionHash))
                return true;

            QuestManager questManager = GlobalRegistry.Quest;
            return questManager != null && questManager.IsCompleted(missionId);
        }

        public void OnQuestEvent(in QuestEventPayload payload)
        {
            if (payload.QuestHashID == 0u)
                return;

            switch ((QuestEventType)payload.EventType)
            {
                case QuestEventType.Activated:
                    _completedMissions.Remove(payload.QuestHashID);
                    return;

                case QuestEventType.Completed:
                    _activeMissions.Remove(payload.QuestHashID);
                    _completedMissions.Add(payload.QuestHashID);
                    return;
            }
        }

        private MissionInstance EnsureActiveInstance(uint missionHash, string missionId)
        {
            if (!_activeMissions.TryGetValue(missionHash, out MissionInstance instance))
            {
                instance = new MissionInstance(missionId); // COLD ALLOC: MissionInstance[1] - compatibility facade wrapper for quest-backed mission - owner: MissionManager
                _activeMissions.Add(missionHash, instance);
            }

            return instance;
        }

        private static uint ComputeMissionHash(string missionId)
        {
            return string.IsNullOrWhiteSpace(missionId)
                ? 0u
                : QuestFlagHashKernel.ComputeStableHash(missionId);
        }
    }
}
