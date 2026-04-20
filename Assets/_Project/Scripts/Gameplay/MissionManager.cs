// ============================================================================
// HECTON-8 — MissionManager.cs
// Singleton manager for mission/quest system.
// ============================================================================

using System.Collections.Generic;
using Hecton.Localization;
using Hecton8.Bootstrap;
using Hecton8.Core;
using Hecton8.Inventory;
using Hecton8.Items;
using Hecton8.SaveSystem;
using Hecton8.UI;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Hecton8.Gameplay
{
    /// <summary>Singleton manager for handling missions and quests.</summary>
    public sealed class MissionManager : MonoBehaviour, ISaveable
    {
        private const string MissionDataRoot = "Assets/_Project/Data";

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
        private bool _hasLookupAmbiguity;
        private string _lookupAmbiguitySummary;

        private PlayerInventory _playerInventory;
        private bool _registeredWithSaveManager;

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
            if (Instance == null)
                Instance = this;

            ResolveDependencies();
            RegisterWithSaveManager();
        }

        private void Start()
        {
            ResolveDependencies();
            RegisterWithSaveManager();
        }

        private void OnDisable()
        {
            if (_registeredWithSaveManager)
            {
                SaveManager sm = SaveManager.Instance;
                if (sm != null) sm.Unregister(this);
                _registeredWithSaveManager = false;
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            TryAutoPopulateMissionRegistry();
            BuildMissionLookup();
        }
#endif

        /// <summary>Starts a mission by its ID.</summary>
        /// <param name="missionId">The unique identifier of the mission.</param>
        public void StartMission(string missionId)
        {
            if (string.IsNullOrEmpty(missionId))
                return;

            if (IsRegistryAmbiguous())
                return;

            if (_activeMissions.ContainsKey(missionId) || _completedMissions.Contains(missionId))
                return;

            if (!TryResolveMissionData(missionId, out MissionData data))
            {
                Debug.LogWarning($"[Mission] Cannot start unknown missionId '{missionId}'.");
                return;
            }

            if (!TryValidateMissionDefinition(data, out string definitionError))
            {
                Debug.LogError($"[Mission] Cannot start invalid mission '{missionId}'. {definitionError}", data);
                return;
            }

            MissionInstance instance = new MissionInstance(data);
            _activeMissions[missionId] = instance;
            NotifyMissionStarted(data);
        }

        /// <summary>Completes an objective for a mission.</summary>
        /// <param name="missionId">The mission identifier.</param>
        /// <param name="objectiveId">The objective identifier.</param>
        public void CompleteObjective(string missionId, string objectiveId)
        {
            if (string.IsNullOrEmpty(objectiveId))
                return;

            if (!_activeMissions.TryGetValue(missionId, out MissionInstance mission))
                return;

            if (!MissionHasObjective(mission.Data, objectiveId))
            {
                Debug.LogWarning($"[Mission] Mission '{missionId}' cannot complete unknown objectiveId '{objectiveId}'.");
                return;
            }

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

#if UNITY_EDITOR
        private void TryAutoPopulateMissionRegistry()
        {
            if (availableMissions != null && availableMissions.Count > 0)
                return;

            string[] missionGuids = AssetDatabase.FindAssets("t:MissionData", new[] { MissionDataRoot });
            if (missionGuids == null || missionGuids.Length == 0)
                return;

            // COLD ALLOC: List<MissionData>[missionGuids.Length] - editor-time mission registry bootstrap - owner: MissionManager
            List<MissionData> loadedMissions = new List<MissionData>(missionGuids.Length);
            for (int i = 0; i < missionGuids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(missionGuids[i]);
                MissionData mission = AssetDatabase.LoadAssetAtPath<MissionData>(path);
                if (mission == null)
                    continue;

                loadedMissions.Add(mission);
            }

            if (loadedMissions.Count <= 0)
                return;

            availableMissions = loadedMissions;
            EditorUtility.SetDirty(this);
        }
#endif

        private void BuildMissionLookup()
        {
            _missionLookup.Clear();
            _hasLookupAmbiguity = false;
            _lookupAmbiguitySummary = string.Empty;

            if (availableMissions == null)
                return;

            for (int i = 0; i < availableMissions.Count; i++)
            {
                MissionData m = availableMissions[i];
                if (m == null || string.IsNullOrEmpty(m.missionId))
                    continue;

                if (_missionLookup.TryGetValue(m.missionId, out MissionData existing) && !ReferenceEquals(existing, m))
                {
                    RegisterLookupAmbiguity(m.missionId, existing, m);
                    Debug.LogError($"[Mission] Duplicate missionId '{m.missionId}' between '{existing.name}' and '{m.name}'.", m);
                    continue;
                }

                _missionLookup[m.missionId] = m;
            }
        }

        private bool TryResolveMissionData(string missionId, out MissionData missionData)
        {
            if (_missionLookup.Count == 0 && availableMissions != null && availableMissions.Count > 0)
                BuildMissionLookup();

            return _missionLookup.TryGetValue(missionId, out missionData);
        }

        private static bool TryValidateMissionDefinition(MissionData data, out string errorSummary)
        {
            errorSummary = string.Empty;
            if (data == null)
            {
                errorSummary = "MissionData is null.";
                return false;
            }

            if (data.objectives == null || data.objectives.Count == 0)
            {
                errorSummary = "Mission has no objectives.";
                return false;
            }

            int objectiveCount = data.objectives.Count;
            // COLD ALLOC: Dictionary<string, byte>[objectiveCount] - mission objective identity validation on activation/load - owner: MissionManager
            Dictionary<string, byte> objectiveIds = new Dictionary<string, byte>(objectiveCount, System.StringComparer.Ordinal);
            for (int i = 0; i < objectiveCount; i++)
            {
                ObjectiveData objective = data.objectives[i];
                if (objective == null)
                {
                    errorSummary = $"Mission has null objective at index {i}.";
                    return false;
                }

                if (string.IsNullOrWhiteSpace(objective.objectiveId))
                {
                    errorSummary = $"Mission has empty objectiveId at index {i}.";
                    return false;
                }

                if (objectiveIds.ContainsKey(objective.objectiveId))
                {
                    errorSummary = $"Mission has duplicate objectiveId '{objective.objectiveId}'.";
                    return false;
                }

                objectiveIds.Add(objective.objectiveId, 0);
            }

            return true;
        }

        private static bool MissionHasObjective(MissionData data, string objectiveId)
        {
            if (data == null || string.IsNullOrEmpty(objectiveId) || data.objectives == null)
                return false;

            for (int i = 0; i < data.objectives.Count; i++)
            {
                ObjectiveData objective = data.objectives[i];
                if (objective == null)
                    continue;

                if (objective.objectiveId == objectiveId)
                    return true;
            }

            return false;
        }

        private bool IsRegistryAmbiguous()
        {
            if (_missionLookup.Count == 0 && availableMissions != null && availableMissions.Count > 0)
                BuildMissionLookup();

            if (!_hasLookupAmbiguity)
                return false;

            Debug.LogError(
                "[Mission] Mission registry has ambiguous mission IDs. " +
                $"Operation aborted: {_lookupAmbiguitySummary}");
            return true;
        }

        private void RegisterLookupAmbiguity(string missionId, MissionData existing, MissionData incoming)
        {
            _hasLookupAmbiguity = true;

            if (!string.IsNullOrEmpty(_lookupAmbiguitySummary))
                return;

            string existingName = existing != null ? existing.name : "null";
            string incomingName = incoming != null ? incoming.name : "null";
            _lookupAmbiguitySummary =
                $"missionId '{missionId}' resolves to both '{existingName}' and '{incomingName}'.";
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

            if (data == null)
                return;

            if (IsRegistryAmbiguous())
                return;

            if (data.missionCompletedIds != null)
                foreach (string id in data.missionCompletedIds)
                    if (!string.IsNullOrEmpty(id) && TryResolveMissionData(id, out MissionData completedMissionData))
                    {
                        if (!TryValidateMissionDefinition(completedMissionData, out string definitionError))
                        {
                            Debug.LogWarning($"[Mission] Save references invalid completed missionId '{id}'. {definitionError} Skipping.");
                            continue;
                        }

                        _completedMissions.Add(id);
                    }
                    else if (!string.IsNullOrEmpty(id))
                        Debug.LogWarning($"[Mission] Save references unknown completed missionId '{id}'. Skipping.");

            if (data.missionActiveIds != null)
            {
                foreach (string id in data.missionActiveIds)
                {
                    if (string.IsNullOrEmpty(id))
                        continue;

                    if (TryResolveMissionData(id, out MissionData mData))
                    {
                        if (!TryValidateMissionDefinition(mData, out string definitionError))
                        {
                            Debug.LogWarning($"[Mission] Save references invalid active missionId '{id}'. {definitionError} Skipping.");
                            continue;
                        }

                        _activeMissions[id] = new MissionInstance(mData);
                    }
                    else
                        Debug.LogWarning($"[Mission] Save references unknown active missionId '{id}'. Skipping.");
                }
            }

            HashSet<string>.Enumerator completedEnumerator = _completedMissions.GetEnumerator();
            while (completedEnumerator.MoveNext())
                _activeMissions.Remove(completedEnumerator.Current);
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

            NotificationEvents.PushInfo(string.Format(
                ResolveLocalized(LocalizationKeys.MISSION_STARTED, "NEW MISSION: {0}"),
                ResolveMissionTitle(data)));
        }

        private void NotifyMissionCompleted(MissionData data)
        {
            if (data == null)
                return;

            NotificationEvents.PushInfo(string.Format(
                ResolveLocalized(LocalizationKeys.MISSION_COMPLETED, "MISSION COMPLETED: {0}"),
                ResolveMissionTitle(data)));
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
                        NotificationEvents.PushInfo(string.Format(
                            ResolveLocalized(LocalizationKeys.MISSION_REWARD_EXPERIENCE, "MISSION: EXPERIENCE LOGGED {0}"),
                            Mathf.Max(0f, reward.experience)));
                        break;

                    case RewardData.RewardType.Unlock:
                        if (!string.IsNullOrEmpty(reward.itemId))
                            NotificationEvents.PushInfo(string.Format(
                                ResolveLocalized(LocalizationKeys.MISSION_REWARD_UNLOCK, "MISSION: UNLOCKED {0}"),
                                reward.itemId.ToUpperInvariant()));
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
                NotificationEvents.PushWarning(ResolveLocalized(
                    LocalizationKeys.MISSION_REWARD_MISSING_CATALOG,
                    "MISSION: ITEM CATALOG UNAVAILABLE"));
                return;
            }

            if (_playerInventory == null)
            {
                NotificationEvents.PushWarning(ResolveLocalized(
                    LocalizationKeys.MISSION_REWARD_STORAGE_OFFLINE,
                    "MISSION: INVENTORY OFFLINE"));
                return;
            }

            ItemData item = itemCatalog.FindById(reward.itemId);
            if (item == null)
            {
                NotificationEvents.PushWarning(string.Format(
                    ResolveLocalized(LocalizationKeys.MISSION_REWARD_UNKNOWN_ITEM, "MISSION: REWARD ITEM NOT FOUND {0}"),
                    reward.itemId.ToUpperInvariant()));
                return;
            }

            int quantity = Mathf.Max(1, reward.count);
            bool granted = _playerInventory.TryAddItem(item, quantity);
            string itemName = item.itemName != null ? item.itemName.ToUpperInvariant() : item.name.ToUpperInvariant();

            if (granted)
            {
                NotificationEvents.PushInfo(string.Format(
                    ResolveLocalized(LocalizationKeys.MISSION_REWARD_ITEM, "REWARD RECEIVED: {0}"),
                    itemName));
                return;
            }

            NotificationEvents.PushWarning(string.Format(
                ResolveLocalized(LocalizationKeys.MISSION_REWARD_NO_CAPACITY, "MISSION: NO SPACE FOR REWARD {0}"),
                itemName));
            NotificationEvents.PushWarning(string.Format(
                ResolveLocalized(LocalizationKeys.MISSION_REWARD_PENDING, "REWARD PENDING DELIVERY: {0}"),
                itemName));
        }

        private static string ResolveMissionTitle(MissionData data)
        {
            return data != null && !string.IsNullOrWhiteSpace(data.title)
                ? data.title
                : "UNKNOWN MISSION";
        }

        private static string ResolveLocalized(string key, string fallback)
        {
            LocalizationManager manager = LocalizationManager.Instance;
            return manager != null
                ? manager.GetOrFallback(manager.CurrentLanguage, key, fallback)
                : fallback;
        }
    }
}
