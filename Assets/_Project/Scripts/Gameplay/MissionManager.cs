// ============================================================================
// HECTON-8 — MissionManager.cs
// Singleton manager for mission/quest system.
// ============================================================================

using System.Collections.Generic;
using Hecton8.Bootstrap;
using Hecton8.Core;
using Hecton8.Inventory;
using Hecton8.Items;
using Hecton8.SaveSystem;
using Hecton8.UI;
using UnityEngine;

namespace Hecton8.Gameplay
{
    /// <summary>Singleton manager for handling missions and quests.</summary>
    public sealed class MissionManager : MonoBehaviour, ISaveable
    {
        /// <summary>Singleton instance of the mission manager.</summary>
        public static MissionManager Instance { get; private set; }

        /// <summary>List of available mission data assets.</summary>
        [Header("References")]
        [Tooltip("Mission definitions available to the runtime manager.")]
        [SerializeField] private List<MissionData> availableMissions = new List<MissionData>();
        [Tooltip("Catalog used to resolve mission item rewards.")]
        [SerializeField] private ItemCatalog itemCatalog;

        private readonly Dictionary<string, MissionInstance> _activeMissions = new Dictionary<string, MissionInstance>();
        private readonly HashSet<string> _completedMissions = new HashSet<string>();

        // COLD ALLOC: O(1) lookup — eliminates LINQ Find() in StartMission hot path
        private readonly Dictionary<string, MissionData> _missionLookup = new Dictionary<string, MissionData>(32);

        private PlayerInventory _playerInventory;
        private bool _registeredWithSaveManager;

        private const string MsgMissionStartedPrefix = "НОВАЯ МИССИЯ: ";
        private const string MsgMissionCompletedPrefix = "МИССИЯ ЗАВЕРШЕНА: ";
        private const string MsgMissionRewardPrefix = "ПОЛУЧЕНА НАГРАДА: ";
        private const string MsgMissionRewardPendingPrefix = "НАГРАДА ОЖИДАЕТ ВЫДАЧИ: ";
        private const string MsgMissionRewardMissingCatalog = "МИССИЯ: КАТАЛОГ ПРЕДМЕТОВ НЕДОСТУПЕН";
        private const string MsgMissionRewardStorageOffline = "МИССИЯ: ИНВЕНТАРЬ НЕДОСТУПЕН";
        private const string MsgMissionRewardExperiencePrefix = "МИССИЯ: ОПЫТ ЗАЧТЁН ";
        private const string MsgMissionRewardUnlockPrefix = "МИССИЯ: РАЗБЛОКИРОВАНО ";
        private const string MsgMissionRewardUnknownItemPrefix = "МИССИЯ: ПРЕДМЕТ НАГРАДЫ НЕ НАЙДЕН ";
        private const string MsgMissionRewardNoCapacityPrefix = "МИССИЯ: НЕТ МЕСТА ДЛЯ НАГРАДЫ ";
        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
            BuildMissionLookup();
            ResolveDependencies();
        }

        private void OnEnable()
        {
            ResolveDependencies();
            RegisterWithSaveManager();
        }

        private void OnDisable()
        {
            if (Instance == this)
                Instance = null;

            if (_registeredWithSaveManager)
            {
                SaveManager sm = SaveManager.Instance;
                if (sm != null) sm.Unregister(this);
                _registeredWithSaveManager = false;
            }
        }

        /// <summary>Starts a mission by its ID.</summary>
        /// <param name="missionId">The unique identifier of the mission.</param>
        public void StartMission(string missionId)
        {
            if (_activeMissions.ContainsKey(missionId) || _completedMissions.Contains(missionId))
                return;

            // O(1) lookup — no LINQ allocation
            if (!_missionLookup.TryGetValue(missionId, out MissionData data))
                return;

            MissionInstance instance = new MissionInstance(data);
            _activeMissions[missionId] = instance;
            NotifyMissionStarted(data);
        }

        /// <summary>Completes an objective for a mission.</summary>
        /// <param name="missionId">The mission identifier.</param>
        /// <param name="objectiveId">The objective identifier.</param>
        public void CompleteObjective(string missionId, string objectiveId)
        {
            if (!_activeMissions.TryGetValue(missionId, out MissionInstance mission))
                return;

            mission.CompleteObjective(objectiveId);

            if (mission.IsCompleted)
            {
                _completedMissions.Add(missionId);
                _activeMissions.Remove(missionId);
                GrantRewards(mission.Data);
                NotifyMissionCompleted(mission.Data);
            }
        }

        /// <summary>Gets an active mission instance.</summary>
        /// <param name="missionId">The mission identifier.</param>
        /// <returns>The mission instance, or null if not active.</returns>
        public MissionInstance GetActiveMission(string missionId)
        {
            _activeMissions.TryGetValue(missionId, out MissionInstance mission);
            return mission;
        }

        /// <summary>Gets all active mission instances.</summary>
        /// <returns>Enumerable of active missions.</returns>
        public IEnumerable<MissionInstance> GetActiveMissions()
        {
            return _activeMissions.Values;
        }

        /// <summary>Checks if a mission is completed.</summary>
        /// <param name="missionId">The mission identifier.</param>
        /// <returns>True if completed.</returns>
        public bool IsMissionCompleted(string missionId)
        {
            return _completedMissions.Contains(missionId);
        }

        /// <summary>Represents an active instance of a mission.</summary>
        public sealed class MissionInstance
        {
            /// <summary>The mission data.</summary>
            public MissionData Data { get; }

            /// <summary>Current state of the mission.</summary>
            public MissionData.MissionState State { get; private set; }

            /// <summary>Completed objectives.</summary>
            public Dictionary<string, bool> CompletedObjectives { get; } = new Dictionary<string, bool>();

            /// <summary>Whether the mission is completed.</summary>
            public bool IsCompleted => State == MissionData.MissionState.Completed;

            /// <summary>Creates a new mission instance.</summary>
            /// <param name="data">The mission data.</param>
            public MissionInstance(MissionData data)
            {
                Data = data;
                State = MissionData.MissionState.Active;
            }

            /// <summary>Completes an objective.</summary>
            /// <param name="objectiveId">The objective identifier.</param>
            public void CompleteObjective(string objectiveId)
            {
                CompletedObjectives[objectiveId] = true;

                // Check if all required objectives are complete
                bool allComplete = true;
                foreach (var obj in Data.objectives)
                {
                    if (!obj.isOptional && !CompletedObjectives.ContainsKey(obj.objectiveId))
                    {
                        allComplete = false;
                        break;
                    }
                }

                if (allComplete)
                {
                    State = MissionData.MissionState.Completed;
                }
            }
        }

        private void ResolveDependencies()
        {
            if (SceneBootstrap.TryGetCurrentPlayerTransform(out Transform playerTransform) &&
                playerTransform != null)
            {
                if (_playerInventory == null)
                    _playerInventory = playerTransform.GetComponent<PlayerInventory>();
            }
        }

        // ── Lookup cache ──────────────────────────────────────────

        private void BuildMissionLookup()
        {
            _missionLookup.Clear();
            if (availableMissions == null) return;
            for (int i = 0; i < availableMissions.Count; i++)
            {
                MissionData m = availableMissions[i];
                if (m != null && !string.IsNullOrEmpty(m.missionId))
                    _missionLookup[m.missionId] = m;
            }
        }

        // ── ISaveable ─────────────────────────────────────────────

        public int SavePriority => 15;
        public int LoadPriority => 15;

        public void PopulateSaveData(SaveData data)
        {
            if (data == null) return;

            data.missionActiveIds.Clear();
            data.missionCompletedIds.Clear();

            foreach (string id in _activeMissions.Keys)
                data.missionActiveIds.Add(id);

            foreach (string id in _completedMissions)
                data.missionCompletedIds.Add(id);
        }

        public void LoadFromSaveData(SaveData data)
        {
            _activeMissions.Clear();
            _completedMissions.Clear();

            if (data == null) return;

            if (data.missionCompletedIds != null)
                foreach (string id in data.missionCompletedIds)
                    if (!string.IsNullOrEmpty(id)) _completedMissions.Add(id);

            if (data.missionActiveIds != null)
            {
                foreach (string id in data.missionActiveIds)
                {
                    if (string.IsNullOrEmpty(id)) continue;
                    if (_missionLookup.TryGetValue(id, out MissionData mData))
                        _activeMissions[id] = new MissionInstance(mData);
                }
            }
        }

        private void RegisterWithSaveManager()
        {
            if (_registeredWithSaveManager) return;
            SaveManager sm = SaveManager.Instance;
            if (sm == null) return;
            sm.Register(this);
            _registeredWithSaveManager = true;
        }

        private void NotifyMissionStarted(MissionData data)
        {
            if (data == null)
                return;

            NotificationEvents.PushInfo(MsgMissionStartedPrefix + data.title);
        }

        private void NotifyMissionCompleted(MissionData data)
        {
            if (data == null)
                return;

            NotificationEvents.PushInfo(MsgMissionCompletedPrefix + data.title);
        }

        private void GrantRewards(MissionData data)
        {
            if (data == null || data.rewards == null)
                return;

            ResolveDependencies();

            for (int i = 0; i < data.rewards.Count; i++)
            {
                RewardData reward = data.rewards[i];
                if (reward == null)
                    continue;

                switch (reward.type)
                {
                    case RewardData.RewardType.Item:
                        GrantItemReward(reward);
                        break;

                    case RewardData.RewardType.Experience:
                        NotificationEvents.PushInfo(MsgMissionRewardExperiencePrefix + Mathf.Max(0f, reward.experience));
                        break;

                    case RewardData.RewardType.Unlock:
                        if (!string.IsNullOrEmpty(reward.itemId))
                            NotificationEvents.PushInfo(MsgMissionRewardUnlockPrefix + reward.itemId.ToUpperInvariant());
                        break;
                }
            }
        }

        private void GrantItemReward(RewardData reward)
        {
            if (reward == null || string.IsNullOrEmpty(reward.itemId))
                return;

            if (itemCatalog == null)
            {
                NotificationEvents.PushWarning(MsgMissionRewardMissingCatalog);
                return;
            }

            if (_playerInventory == null)
            {
                NotificationEvents.PushWarning(MsgMissionRewardStorageOffline);
                return;
            }

            ItemData item = itemCatalog.FindById(reward.itemId);
            if (item == null)
            {
                NotificationEvents.PushWarning(MsgMissionRewardUnknownItemPrefix + reward.itemId.ToUpperInvariant());
                return;
            }

            int quantity = Mathf.Max(1, reward.count);
            bool granted = _playerInventory.TryAddItem(item, quantity);
            string itemName = item.itemName != null ? item.itemName.ToUpperInvariant() : item.name.ToUpperInvariant();

            if (granted)
            {
                NotificationEvents.PushInfo(MsgMissionRewardPrefix + itemName);
                return;
            }

            NotificationEvents.PushWarning(MsgMissionRewardNoCapacityPrefix + itemName);
            NotificationEvents.PushWarning(MsgMissionRewardPendingPrefix + itemName);
        }
    }
}
