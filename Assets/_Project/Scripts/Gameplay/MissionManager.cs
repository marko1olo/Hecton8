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
        private const int MissionCacheCapacity = QuestDagRuntimeConstants.DefaultQuestStateCapacity;
        private static MissionManager s_activeRuntime;

        public sealed class MissionInstance
        {
            public MissionInstance(string missionId)
            {
                MissionId = missionId ?? string.Empty;
            }

            public string MissionId { get; }
        }

        // COLD ALLOC: Dictionary<uint,MissionInstance>[64] - compatibility facade active mission cache keyed by FNV quest hash - owner: MissionManager
        private readonly Dictionary<uint, MissionInstance> _activeMissions = new Dictionary<uint, MissionInstance>(MissionCacheCapacity);
        // COLD ALLOC: HashSet<uint>[64] - compatibility facade completed mission cache keyed by FNV quest hash - owner: MissionManager
        private readonly HashSet<uint> _completedMissions = new HashSet<uint>(MissionCacheCapacity);
        // COLD ALLOC: uint[64] - active quest hash scratch for cold quest-system cache resync - owner: MissionManager
        private readonly uint[] _activeQuestHashScratch = new uint[MissionCacheCapacity];
        private IQuestSystem _questManager;
        private bool _serviceRegistered;
        private bool _hotSwapRegistered;

        public static MissionManager Instance => s_activeRuntime;

        /// <summary>
        /// Resolve-or-create the sole MissionManager / GlobalRegistry.Missions owner.
        /// Script GUID 118565efc6b6f054c835c8316440c86f has ZERO scene/prefab hits.
        /// Awake/OnEnable only register when already present; without this factory the
        /// mission facade slot stays permanently null (save mission lanes + director bridge
        /// compatibility consumers).
        /// </summary>
        public static MissionManager EnsureRuntimeInstance()
        {
            MissionManager registered = GlobalRegistry.Missions;
            if (IsMissionRuntimeUsable(registered))
                return registered;

            if (!ReferenceEquals(registered, null))
            {
                GlobalRegistry.UnregisterMissionRuntime(registered);
                registered._serviceRegistered = false;
                if (ReferenceEquals(s_activeRuntime, registered))
                    s_activeRuntime = null;
            }
            else if (!ReferenceEquals(s_activeRuntime, null) && IsMissionRuntimeUsable(s_activeRuntime))
            {
                GlobalRegistry.RegisterMissionRuntime(s_activeRuntime);
                return s_activeRuntime;
            }

            if (!Application.isPlaying)
                return null;

            // Player-build construction path: no authored/bootstrap instance reachable.
            // Must construct in player builds when bootstrap reorders or skips registration.
            GameObject runtimeRoot = new GameObject("[MissionManager]"); // COLD ALLOC
            return runtimeRoot.AddComponent<MissionManager>();
        }

        private void Awake()
        {
            if (TryAbortForUsableExistingRuntime())
                return;
        }


        private void OnEnable()
        {
            if (!TryRegisterService())
                return;

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

        private bool TryRegisterService()
        {
            if (_serviceRegistered || !Application.isPlaying)
                return true;

            if (TryAbortForUsableExistingRuntime())
                return false;

            GlobalRegistry.RegisterMissionRuntime(this);
            _serviceRegistered = ReferenceEquals(GlobalRegistry.Missions, this);
            if (_serviceRegistered)
            {
                s_activeRuntime = this;
                _questManager = GlobalRegistry.QuestSystem;
                RefreshMissionCacheFromQuestSystem();
            }

            return _serviceRegistered;
        }

        private void TryUnregisterService()
        {
            if (!_serviceRegistered)
                return;

            GlobalRegistry.UnregisterMissionRuntime(this);
            _serviceRegistered = false;
            if (ReferenceEquals(s_activeRuntime, this))
                s_activeRuntime = null;
            _activeMissions.Clear();
            _completedMissions.Clear();
            _questManager = null;
        }

        private bool TryAbortForUsableExistingRuntime()
        {
            if (!Application.isPlaying)
                return false;

            MissionManager registered = GlobalRegistry.Missions;
            if (!ReferenceEquals(registered, null) && !ReferenceEquals(registered, this))
            {
                if (IsMissionRuntimeUsable(registered))
                {
                    s_activeRuntime = registered;
                    Destroy(gameObject);
                    return true;
                }

                if (ReferenceEquals(s_activeRuntime, registered))
                    s_activeRuntime = null;

                GlobalRegistry.UnregisterMissionRuntime(registered);
            }

            MissionManager active = s_activeRuntime;
            if (ReferenceEquals(active, null) || ReferenceEquals(active, this))
                return false;

            if (IsMissionRuntimeUsable(active))
            {
                GlobalRegistry.RegisterMissionRuntime(active);
                Destroy(gameObject);
                return true;
            }

            s_activeRuntime = null;
            if (ReferenceEquals(GlobalRegistry.Missions, active))
                GlobalRegistry.UnregisterMissionRuntime(active);
            return false;
        }

        private static bool IsMissionRuntimeUsable(MissionManager manager)
        {
            return !ReferenceEquals(manager, null) &&
                   manager != null &&
                   manager._serviceRegistered &&
                   manager.isActiveAndEnabled;
        }

        public void StartMission(string missionId)
        {
            if (string.IsNullOrWhiteSpace(missionId))
                return;

            uint missionHash = ComputeMissionHash(missionId);
            if (missionHash == 0u || _completedMissions.Contains(missionHash) || _activeMissions.ContainsKey(missionHash))
                return;

            IQuestSystem questManager = _questManager;
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

            IQuestSystem questManager = _questManager;
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

            if (!_activeMissions.TryGetValue(missionHash, out MissionInstance instance))
                return null;

            IQuestSystem questManager = _questManager;
            return questManager != null && questManager.IsActive(missionId) ? instance : null;
        }

        public int ActiveMissionCount => _activeMissions.Count;

        public bool TryCopyActiveMissionsNonAlloc(MissionInstance[] destination, out int count)
        {
            count = 0;
            if (destination == null || destination.Length == 0)
                return _activeMissions.Count == 0;

            var enumerator = _activeMissions.GetEnumerator();
            while (enumerator.MoveNext() && count < destination.Length)
                destination[count++] = enumerator.Current.Value;

            return count == _activeMissions.Count;
        }

        public bool TryGetActiveMissionAt(int index, out MissionInstance instance)
        {
            instance = null;
            if ((uint)index >= (uint)_activeMissions.Count)
                return false;

            int currentIndex = 0;
            var enumerator = _activeMissions.GetEnumerator();
            while (enumerator.MoveNext())
            {
                if (currentIndex++ != index)
                    continue;

                instance = enumerator.Current.Value;
                return instance != null;
            }

            return false;
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

            IQuestSystem questManager = _questManager;
            return questManager != null && questManager.IsCompleted(missionId);
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot == GlobalRegistryServiceSlot.QuestRuntime ||
                serviceSlot == GlobalRegistryServiceSlot.QuestSystem)
            {
                _questManager = currentService as IQuestSystem;
                RefreshMissionCacheFromQuestSystem();
            }
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
                    TryEnsureActiveInstance(payload.QuestHashID);
                    return;

                case QuestEventType.Completed:
                    _activeMissions.Remove(payload.QuestHashID);
                    _completedMissions.Add(payload.QuestHashID);
                    return;

                case QuestEventType.Failed:
                case QuestEventType.RevertRequested:
                    _activeMissions.Remove(payload.QuestHashID);
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

        private bool TryEnsureActiveInstance(uint missionHash)
        {
            if (missionHash == 0u || _activeMissions.ContainsKey(missionHash))
                return false;

            IQuestSystem questManager = _questManager;
            if (questManager == null ||
                !questManager.IsActive(missionHash) ||
                !questManager.TryGetQuestIdByHash(missionHash, out string missionId))
            {
                return false;
            }

            EnsureActiveInstance(missionHash, missionId);
            return true;
        }

        private void RefreshMissionCacheFromQuestSystem()
        {
            _activeMissions.Clear();
            _completedMissions.Clear();

            IQuestSystem questManager = _questManager;
            if (questManager == null)
                return;

            int copiedCount = questManager.CopyActiveQuestHashes(_activeQuestHashScratch);
            if (copiedCount <= 0)
                return;

            int limit = copiedCount < _activeQuestHashScratch.Length
                ? copiedCount
                : _activeQuestHashScratch.Length;

            for (int i = 0; i < limit; i++)
            {
                uint missionHash = _activeQuestHashScratch[i];
                if (missionHash == 0u ||
                    !questManager.IsActive(missionHash) ||
                    !questManager.TryGetQuestIdByHash(missionHash, out string missionId))
                {
                    continue;
                }

                EnsureActiveInstance(missionHash, missionId);
            }
        }

        private static uint ComputeMissionHash(string missionId)
        {
            return string.IsNullOrWhiteSpace(missionId)
                ? 0u
                : QuestFlagHashKernel.ComputeStableHash(missionId);
        }
    }
}
