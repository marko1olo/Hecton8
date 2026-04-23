// ============================================================================
// HECTON-8 — QuestManager.cs
// Stateless quest hub — listens to world events and advances quests.
// ============================================================================

using System.Collections.Generic;
using Hecton.Localization;
using Hecton8.AtlasSignal;
using Hecton8.Celestial;
using Hecton8.Core;
using Hecton8.Gameplay;
using Hecton8.Interaction;
using Hecton8.Items;
using Hecton8.Narrative;
using Hecton8.SaveSystem;
using Hecton8.UI;
using Unity.Collections;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Hecton8.Quest
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-130)]
    public sealed class QuestManager : MonoBehaviour, ISaveable, ISlowTickable
    {
        [Header("── Quest Registry ──────────────────────────")]
        [Tooltip("All project quests. Assign in the inspector.")]
        [SerializeField] private QuestData[] allQuests = new QuestData[0];

        private const string QuestFolder = "Assets/_Project/Data/Lore/Quests";

        public static QuestManager Instance { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState() => Instance = null;

        private readonly HashSet<string> _activeQuests = new HashSet<string>(64);
        private readonly HashSet<string> _completedQuests = new HashSet<string>(128);
        private readonly Dictionary<string, QuestData> _questLookup = new Dictionary<string, QuestData>(64);

        private float _currentDepth;
        private bool _registered;
        private bool _biomeDiscoveryRegistered;
        private bool _hasLookupAmbiguity;
        private string _lookupAmbiguitySummary;

        public int SavePriority => 7;
        public int LoadPriority => 7;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            BuildLookup();
        }

        private void OnEnable()
        {
            TryRegister();

            if (SaveManager.Instance != null)
                SaveManager.Instance.Register(this);

            SubscribeToEvents();
            TrySubscribeToBiomeDiscovery();
        }

        private void OnDisable()
        {
            TryUnregister();

            if (SaveManager.Instance != null)
                SaveManager.Instance.Unregister(this);

            UnsubscribeFromEvents();
            UnsubscribeFromBiomeDiscovery();
        }

        private void OnDestroy()
        {
            TryUnregister();

            if (Instance == this)
                Instance = null;
        }

        private void Start()
        {
            for (int i = 0; i < allQuests.Length; i++)
            {
                QuestData quest = allQuests[i];
                if (quest != null && quest.autoActivateOnStart)
                    ActivateQuest(quest.questId);
            }

            TrySubscribeToBiomeDiscovery();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            TryAutoPopulateQuestRegistry();
            BuildLookup();
        }
#endif

        public void SlowTick()
        {
            for (int i = 0; i < allQuests.Length; i++)
            {
                QuestData quest = allQuests[i];
                if (quest == null)
                    continue;

                if (quest.triggerType == QuestTriggerType.OnDepthReached &&
                    !_activeQuests.Contains(quest.questId) &&
                    !_completedQuests.Contains(quest.questId) &&
                    _currentDepth >= quest.triggerValue)
                {
                    ActivateQuest(quest.questId);
                }

                if (quest.completionType == QuestCompletionType.OnDepthReached &&
                    _activeQuests.Contains(quest.questId) &&
                    _currentDepth >= quest.completionValue)
                {
                    CompleteQuest(quest.questId);
                }
            }
        }

        public void ActivateQuest(string questId)
        {
            if (string.IsNullOrEmpty(questId))
                return;

            if (IsRegistryAmbiguous() || _activeQuests.Contains(questId) || _completedQuests.Contains(questId))
                return;

            if (!TryResolveQuestData(questId, out QuestData quest))
            {
                Debug.LogWarning($"[Quest] Cannot activate unknown questId '{questId}'.");
                return;
            }

            _activeQuests.Add(questId);
            QuestEvents.RaiseActivated(questId);

            string title = quest.DisplayTitleOrFallback;
            LocalizationManager localization = LocalizationManager.Instance;
            NotificationEvents.PushInfo(localization != null
                ? localization.GetFormatted(LocalizationKeys.QUEST_NEW_OBJECTIVE, title)
                : "NEW OBJECTIVE: " + title);
        }

        public void CompleteQuest(string questId)
        {
            if (string.IsNullOrEmpty(questId))
                return;

            if (IsRegistryAmbiguous() || !_activeQuests.Contains(questId))
                return;

            if (!TryResolveQuestData(questId, out QuestData quest))
            {
                Debug.LogWarning($"[Quest] Cannot complete unknown questId '{questId}'.");
                _activeQuests.Remove(questId);
                return;
            }

            _activeQuests.Remove(questId);
            _completedQuests.Add(questId);
            QuestEvents.RaiseCompleted(questId);

            string title = quest.DisplayTitleOrFallback;
            LocalizationManager localization = LocalizationManager.Instance;
            NotificationEvents.PushInfo(localization != null
                ? localization.GetFormatted(LocalizationKeys.QUEST_COMPLETED, title)
                : "OBJECTIVE COMPLETED: " + title);
        }

        public bool IsActive(string questId) => _activeQuests.Contains(questId);
        public bool IsCompleted(string questId) => _completedQuests.Contains(questId);
        public void UpdateDepth(float depthMeters) => _currentDepth = depthMeters;

        public NativeArray<uint> CapturePackedStateSnapshot(Allocator allocator)
        {
            return new NativeArray<uint>(0, allocator, NativeArrayOptions.ClearMemory);
        }

        public static void StageLoadedPackedState(uint[] packedWords)
        {
        }

        public void PopulateSaveData(SaveData data)
        {
            if (data == null)
                return;

            data.questActiveIds.Clear();
            data.questCompletedIds.Clear();

            foreach (string id in _activeQuests)
                data.questActiveIds.Add(id);

            foreach (string id in _completedQuests)
                data.questCompletedIds.Add(id);
        }

        public void LoadFromSaveData(SaveData data)
        {
            _activeQuests.Clear();
            _completedQuests.Clear();

            if (data == null || IsRegistryAmbiguous())
                return;

            if (data.questActiveIds != null)
            {
                foreach (string id in data.questActiveIds)
                {
                    if (!string.IsNullOrEmpty(id) && TryResolveQuestData(id, out _))
                        _activeQuests.Add(id);
                }
            }

            if (data.questCompletedIds != null)
            {
                foreach (string id in data.questCompletedIds)
                {
                    if (!string.IsNullOrEmpty(id) && TryResolveQuestData(id, out _))
                        _completedQuests.Add(id);
                }
            }
        }

        private void TryRegister()
        {
            if (_registered)
                return;

            GameTickManager gameTickManager = GameTickManager.Instance;
            if (gameTickManager == null)
                return;

            gameTickManager.Register(this);
            _registered = true;
        }

        private void TryUnregister()
        {
            if (!_registered)
                return;

            GameTickManager gameTickManager = GameTickManager.Instance;
            if (gameTickManager != null)
                gameTickManager.Unregister(this);

            _registered = false;
        }

        private void SubscribeToEvents()
        {
            InteractionEvents.OnItemCollected += HandleItemCollected;
            NarrativeEvents.OnDiscoveryMade += HandleDiscoveryMade;
            NarrativeEvents.OnDepthTierReached += HandleDepthTierReached;
            AudioLogEvents.OnLogDiscovered += HandleAudioLogDiscovered;
            HectonCelestialEngine.OnEclipseStart += HandleEclipseStart;
            AtlasSignalEvents.OnSignalDecoded += HandleSignalDecoded;
        }

        private void UnsubscribeFromEvents()
        {
            InteractionEvents.OnItemCollected -= HandleItemCollected;
            NarrativeEvents.OnDiscoveryMade -= HandleDiscoveryMade;
            NarrativeEvents.OnDepthTierReached -= HandleDepthTierReached;
            AudioLogEvents.OnLogDiscovered -= HandleAudioLogDiscovered;
            HectonCelestialEngine.OnEclipseStart -= HandleEclipseStart;
            AtlasSignalEvents.OnSignalDecoded -= HandleSignalDecoded;
        }

        private void TrySubscribeToBiomeDiscovery()
        {
            if (_biomeDiscoveryRegistered)
                return;

            HectonDiscoveryManager discoveryManager = HectonDiscoveryManager.Instance;
            if (discoveryManager == null)
                return;

            discoveryManager.OnBiomeDiscovered += HandleBiomeDiscovered;
            _biomeDiscoveryRegistered = true;
        }

        private void UnsubscribeFromBiomeDiscovery()
        {
            if (!_biomeDiscoveryRegistered)
                return;

            HectonDiscoveryManager discoveryManager = HectonDiscoveryManager.Instance;
            if (discoveryManager != null)
                discoveryManager.OnBiomeDiscovered -= HandleBiomeDiscovered;

            _biomeDiscoveryRegistered = false;
        }

        private void HandleDiscoveryMade(string discoveryId)
        {
            ProcessTrigger(QuestTriggerType.OnDiscoveryMade, discoveryId, 0f);
            ProcessCompletion(QuestCompletionType.OnDiscoveryMade, discoveryId, 0f);
        }

        private void HandleItemCollected(ItemData itemData, int quantity, Transform interactor)
        {
            string itemId = itemData != null ? itemData.PersistentId : string.Empty;
            ProcessTrigger(QuestTriggerType.OnItemCollected, itemId, quantity);
            ProcessCompletion(QuestCompletionType.OnItemCollected, itemId, quantity);
        }

        private void HandleAudioLogDiscovered(string logId)
        {
            ProcessTrigger(QuestTriggerType.OnAudioLogFound, logId, 0f);
            ProcessCompletion(QuestCompletionType.OnAudioLogFound, logId, 0f);
        }

        private void HandleEclipseStart()
        {
            ProcessTrigger(QuestTriggerType.OnEclipseStart, string.Empty, 0f);
        }

        private void HandleSignalDecoded(string messageId)
        {
            ProcessCompletion(QuestCompletionType.OnSignalDecoded, messageId, 0f);
        }

        private void HandleBiomeDiscovered(int biomeId)
        {
            ProcessTrigger(QuestTriggerType.OnBiomeEntered, string.Empty, biomeId);
            ProcessCompletion(QuestCompletionType.OnBiomeEntered, string.Empty, biomeId);
        }

        private void HandleDepthTierReached(int tier)
        {
            _currentDepth = tier switch
            {
                1 => 0f,
                2 => 100f,
                3 => 300f,
                4 => 1000f,
                _ => 0f,
            };
        }

        private void BuildLookup()
        {
            _questLookup.Clear();
            _hasLookupAmbiguity = false;
            _lookupAmbiguitySummary = string.Empty;

            for (int i = 0; i < allQuests.Length; i++)
            {
                QuestData quest = allQuests[i];
                if (quest == null || string.IsNullOrEmpty(quest.questId))
                    continue;

                if (_questLookup.TryGetValue(quest.questId, out QuestData existing) && !ReferenceEquals(existing, quest))
                {
                    RegisterLookupAmbiguity(quest.questId, existing, quest);
                    continue;
                }

                _questLookup[quest.questId] = quest;
            }
        }

        private bool TryResolveQuestData(string questId, out QuestData questData)
        {
            if (_questLookup.Count == 0 && allQuests != null && allQuests.Length > 0)
                BuildLookup();

            return _questLookup.TryGetValue(questId, out questData);
        }

        private bool IsRegistryAmbiguous()
        {
            if (_questLookup.Count == 0 && allQuests != null && allQuests.Length > 0)
                BuildLookup();

            return _hasLookupAmbiguity;
        }

        private void RegisterLookupAmbiguity(string questId, QuestData existing, QuestData incoming)
        {
            _hasLookupAmbiguity = true;
            if (!string.IsNullOrEmpty(_lookupAmbiguitySummary))
                return;

            string existingName = existing != null ? existing.name : "null";
            string incomingName = incoming != null ? incoming.name : "null";
            _lookupAmbiguitySummary = $"questId '{questId}' resolves to both '{existingName}' and '{incomingName}'.";
        }

        private void ProcessTrigger(QuestTriggerType type, string id, float value)
        {
            for (int i = 0; i < allQuests.Length; i++)
            {
                QuestData quest = allQuests[i];
                if (quest == null || quest.triggerType != type || _activeQuests.Contains(quest.questId) || _completedQuests.Contains(quest.questId))
                    continue;

                if (quest.triggerType == QuestTriggerType.OnBiomeEntered)
                {
                    if (!Mathf.Approximately(quest.triggerValue, value))
                        continue;
                }
                else if (quest.triggerType == QuestTriggerType.OnItemCollected)
                {
                    if (!string.IsNullOrEmpty(quest.triggerId) && quest.triggerId != id)
                        continue;

                    if (quest.triggerValue > 0f && value < quest.triggerValue)
                        continue;
                }
                else if (!string.IsNullOrEmpty(quest.triggerId) && quest.triggerId != id)
                {
                    continue;
                }

                ActivateQuest(quest.questId);
            }
        }

        private void ProcessCompletion(QuestCompletionType type, string id, float value)
        {
            for (int i = 0; i < allQuests.Length; i++)
            {
                QuestData quest = allQuests[i];
                if (quest == null || quest.completionType != type || !_activeQuests.Contains(quest.questId))
                    continue;

                if (quest.completionType == QuestCompletionType.OnBiomeEntered)
                {
                    if (!Mathf.Approximately(quest.completionValue, value))
                        continue;
                }
                else if (quest.completionType == QuestCompletionType.OnItemCollected)
                {
                    if (!string.IsNullOrEmpty(quest.completionId) && quest.completionId != id)
                        continue;

                    if (quest.completionValue > 0f && value < quest.completionValue)
                        continue;
                }
                else if (!string.IsNullOrEmpty(quest.completionId) && quest.completionId != id)
                {
                    continue;
                }

                CompleteQuest(quest.questId);
            }
        }

#if UNITY_EDITOR
        private void TryAutoPopulateQuestRegistry()
        {
            if (allQuests != null && allQuests.Length > 0)
                return;

            string[] guids = AssetDatabase.FindAssets("t:QuestData", new[] { QuestFolder });
            if (guids == null || guids.Length == 0)
                return;

            QuestData[] loaded = new QuestData[guids.Length];
            int count = 0;
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                QuestData quest = AssetDatabase.LoadAssetAtPath<QuestData>(path);
                if (quest == null)
                    continue;

                loaded[count++] = quest;
            }

            if (count <= 0)
                return;

            if (count != loaded.Length)
                System.Array.Resize(ref loaded, count);

            allQuests = loaded;
            EditorUtility.SetDirty(this);
        }
#endif
    }
}
