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
    public sealed class MissionManager : MonoBehaviour, IQuestEventListener, IGlobalRegistryHotSwapListener
    {
        private const int MissionCacheCapacity = 32;
        private static MissionManager s_activeRuntime;

        public sealed class MissionInstance
        {
            public MissionInstance(string missionId)
            {
                MissionId = missionId ?? string.Empty;
            }

            public string MissionId { get; }
        }

        // COLD ALLOC: Dictionary<uint,MissionInstance>[32] - compatibility facade active mission cache keyed by FNV quest hash - owner: MissionManager
        private readonly Dictionary<uint, MissionInstance> _activeMissions = new Dictionary<uint, MissionInstance>(MissionCacheCapacity);
        // COLD ALLOC: HashSet<uint>[32] - compatibility facade completed mission cache keyed by FNV quest hash - owner: MissionManager
        private readonly HashSet<uint> _completedMissions = new HashSet<uint>(MissionCacheCapacity);
        private QuestManager _questManager;
        private bool _serviceRegistered;
        private bool _hotSwapRegistered;

        public static MissionManager Instance => s_activeRuntime;

        private void Awake()
        {
            MissionManager registered = GlobalRegistry.Missions;
            if (Application.isPlaying && registered != null && !ReferenceEquals(registered, this))
            {
                Destroy(gameObject);
                return;
            }
        }

        private void OnEnable()
        {
            TryRegisterService();
            TryRegisterHotSwapListener();
            QuestEvents.Register(this);
        }

        private void OnDisable()
        {
            QuestEvents.Unregister(this);
            TryUnregisterHotSwapListener();
            TryUnregisterService();
        }

        private void OnDestroy()
        {
            TryUnregisterHotSwapListener();
            TryUnregisterService();
        }

        private void TryRegisterService()
        {
            if (_serviceRegistered || !Application.isPlaying)
                return;

            MissionManager registered = GlobalRegistry.Missions;
            if (registered != null && !ReferenceEquals(registered, this))
            {
                Destroy(gameObject);
                return;
            }

            GlobalRegistry.RegisterMissionRuntime(this);
            _serviceRegistered = ReferenceEquals(GlobalRegistry.Missions, this);
            if (_serviceRegistered)
            {
                s_activeRuntime = this;
                _questManager = GlobalRegistry.Quest;
            }
        }

        private void TryUnregisterService()
        {
            if (!_serviceRegistered)
                return;

            GlobalRegistry.UnregisterMissionRuntime(this);
            _serviceRegistered = false;
            if (ReferenceEquals(s_activeRuntime, this))
                s_activeRuntime = null;
            _questManager = null;
        }

        public void StartMission(string missionId)
        {
            if (string.IsNullOrWhiteSpace(missionId))
                return;

            uint missionHash = ComputeMissionHash(missionId);
            if (missionHash == 0u || _completedMissions.Contains(missionHash) || _activeMissions.ContainsKey(missionHash))
                return;

            QuestManager questManager = _questManager;
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

            QuestManager questManager = _questManager;
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

            QuestManager questManager = _questManager;
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

            QuestManager questManager = _questManager;
            return questManager != null && questManager.IsCompleted(missionId);
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot == GlobalRegistryServiceSlot.QuestRuntime)
                _questManager = currentService as QuestManager;
        }

        private void TryRegisterHotSwapListener()
        {
            if (_hotSwapRegistered || !Application.isPlaying)
                return;

            _hotSwapRegistered = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterHotSwapListener()
        {
            if (!_hotSwapRegistered)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _hotSwapRegistered = false;
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
