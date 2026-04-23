using System;
using System.Collections.Generic;
using Hecton.Localization;
using Hecton8.AtlasSignal;
using Hecton8.Celestial;
using Hecton8.Core;
using Hecton8.Items;
using Hecton8.Modding;
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
    public sealed class QuestManager : MonoBehaviour, ISaveable
    {
        [Header("Quest Registry")]
        [Tooltip("All authored quest assets assigned to this runtime owner.")]
        [SerializeField] private QuestData[] allQuests = Array.Empty<QuestData>();

        private const string QuestFolder = "Assets/_Project/Data/Lore/Quests";
        private const string EventSubscriberId = "quest.manager";
        private const float DepthTierTwoMeters = 100f;
        private const float DepthTierThreeMeters = 300f;
        private const float DepthTierFourMeters = 1000f;

        private static uint[] s_stagedLoadedPackedState;

        // COLD ALLOC: Dictionary<string,QuestData>[64] - authored quest lookup by stable questId - owner: QuestManager
        private readonly Dictionary<string, QuestData> _questLookup = new Dictionary<string, QuestData>(64);
        private QuestStateManager _stateManager;
        private HectonEventSubscription _itemCollectedSubscription;
        private HectonEventSubscription _biomeDiscoveredSubscription;
        private HectonEventSubscription _loreAcquiredSubscription;
        private bool _loadedFromSave;
        private bool _hasLookupAmbiguity;
        private string _lookupAmbiguitySummary = string.Empty;

        public static QuestManager Instance { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            Instance = null;
            s_stagedLoadedPackedState = null;
        }

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
            InitializeStateGraph();
        }

        private void OnEnable()
        {
            if (SaveManager.Instance != null)
                SaveManager.Instance.Register(this);

            SubscribeToEvents();
        }

        private void OnDisable()
        {
            if (SaveManager.Instance != null)
                SaveManager.Instance.Unregister(this);

            UnsubscribeFromEvents();
        }

        private void OnDestroy()
        {
            UnsubscribeFromEvents();

            if (_stateManager != null)
            {
                _stateManager.Dispose();
                _stateManager = null;
            }

            if (Instance == this)
                Instance = null;
        }

        private void Start()
        {
            if (_loadedFromSave || _stateManager == null)
                return;

            _stateManager.ApplyAutoActivationFlags(allQuests);
            FlushRuntimeResults();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            TryAutoPopulateQuestRegistry();
            BuildLookup();
        }
#endif

        public void ActivateQuest(string questId)
        {
            if (!TryResolveQuestHash(questId, logUnknownQuest: true, out uint questHash, out _))
                return;

            if (_stateManager == null || !_stateManager.TryActivateQuest(questHash, out int questIndex))
                return;

            EmitQuestTransition(questIndex, completed: false);
        }

        public void CompleteQuest(string questId)
        {
            if (!TryResolveQuestHash(questId, logUnknownQuest: true, out uint questHash, out _))
                return;

            if (_stateManager == null || !_stateManager.TryCompleteQuest(questHash, out int questIndex))
                return;

            EmitQuestTransition(questIndex, completed: true);
        }

        public bool IsActive(string questId)
        {
            return TryResolveQuestHash(questId, logUnknownQuest: false, out uint questHash, out _) &&
                   _stateManager != null &&
                   _stateManager.IsQuestActive(questHash);
        }

        public bool IsCompleted(string questId)
        {
            return TryResolveQuestHash(questId, logUnknownQuest: false, out uint questHash, out _) &&
                   _stateManager != null &&
                   _stateManager.IsQuestCompleted(questHash);
        }

        public void UpdateDepth(float depthMeters)
        {
            EvaluateSignal(new QuestSignal(QuestSignalKind.DepthReached, 0u, depthMeters));
        }

        public NativeArray<uint> CapturePackedStateSnapshot(Allocator allocator)
        {
            return _stateManager != null
                ? _stateManager.CapturePackedStateSnapshot(allocator)
                : new NativeArray<uint>(0, allocator, NativeArrayOptions.ClearMemory);
        }

        public static void StageLoadedPackedState(uint[] packedWords)
        {
            if (packedWords == null || packedWords.Length <= 0)
            {
                s_stagedLoadedPackedState = null;
                return;
            }

            if (s_stagedLoadedPackedState == null || s_stagedLoadedPackedState.Length != packedWords.Length)
            {
                // COLD ALLOC: uint[packedWords.Length] - staged packed quest words from SaveManager load handoff - owner: QuestManager
                s_stagedLoadedPackedState = new uint[packedWords.Length];
            }

            Array.Copy(packedWords, s_stagedLoadedPackedState, packedWords.Length);
        }

        public void PopulateSaveData(SaveData data)
        {
            if (data == null)
                return;

            if (data.questActiveIds == null)
                data.questActiveIds = new List<string>();
            if (data.questCompletedIds == null)
                data.questCompletedIds = new List<string>();

            data.questActiveIds.Clear();
            data.questCompletedIds.Clear();

            for (int i = 0; i < allQuests.Length; i++)
            {
                QuestData questData = allQuests[i];
                if (questData == null || string.IsNullOrWhiteSpace(questData.questId))
                    continue;

                if (IsActive(questData.questId))
                    data.questActiveIds.Add(questData.questId);

                if (IsCompleted(questData.questId))
                    data.questCompletedIds.Add(questData.questId);
            }
        }

        public void LoadFromSaveData(SaveData data)
        {
            _loadedFromSave = true;

            if (_stateManager == null)
                return;

            uint[] stagedWords = s_stagedLoadedPackedState;
            s_stagedLoadedPackedState = null;

            if (stagedWords != null && stagedWords.Length > 0)
            {
                _stateManager.RestorePackedState(stagedWords);
                return;
            }

            IEnumerable<string> activeQuestIds = data != null ? data.questActiveIds : null;
            IEnumerable<string> completedQuestIds = data != null ? data.questCompletedIds : null;
            _stateManager.RestoreLegacyState(activeQuestIds, completedQuestIds);
        }

        private void InitializeStateGraph()
        {
            if (_stateManager == null)
                _stateManager = new QuestStateManager();

            bool initialized = _stateManager.Initialize(allQuests);
            if (_hasLookupAmbiguity)
            {
                Debug.LogError($"[QuestManager] Quest registry ambiguity detected: {_lookupAmbiguitySummary}");
                enabled = false;
                return;
            }

            if (!initialized || _stateManager.HasCompileErrors)
            {
                string compileSummary = _stateManager != null ? _stateManager.CompileErrorSummary : "Unknown quest compile error.";
                Debug.LogError($"[QuestManager] Quest state graph compilation failed.{System.Environment.NewLine}{compileSummary}");
                enabled = false;
            }
        }

        private void SubscribeToEvents()
        {
            if (_itemCollectedSubscription == null)
                _itemCollectedSubscription = HectonEventBus.Subscribe<ItemCollectedEvent>(HandleItemCollected, EventSubscriberId);

            if (_biomeDiscoveredSubscription == null)
                _biomeDiscoveredSubscription = HectonEventBus.Subscribe<BiomeDiscoveredEvent>(HandleBiomeDiscovered, EventSubscriberId);

            if (_loreAcquiredSubscription == null)
                _loreAcquiredSubscription = HectonEventBus.Subscribe<LoreAcquiredEvent>(HandleLoreAcquired, EventSubscriberId);

            NarrativeEvents.OnDepthTierReached += HandleDepthTierReached;
            HectonCelestialEngine.OnEclipseStart += HandleEclipseStart;
            AtlasSignalEvents.OnSignalDecoded += HandleSignalDecoded;
        }

        private void UnsubscribeFromEvents()
        {
            if (_itemCollectedSubscription != null)
            {
                _itemCollectedSubscription.Dispose();
                _itemCollectedSubscription = null;
            }

            if (_biomeDiscoveredSubscription != null)
            {
                _biomeDiscoveredSubscription.Dispose();
                _biomeDiscoveredSubscription = null;
            }

            if (_loreAcquiredSubscription != null)
            {
                _loreAcquiredSubscription.Dispose();
                _loreAcquiredSubscription = null;
            }

            NarrativeEvents.OnDepthTierReached -= HandleDepthTierReached;
            HectonCelestialEngine.OnEclipseStart -= HandleEclipseStart;
            AtlasSignalEvents.OnSignalDecoded -= HandleSignalDecoded;
        }

        private void HandleItemCollected(ItemCollectedEvent evt)
        {
            if (evt == null)
                return;

            string itemId = evt.Item != null ? evt.Item.PersistentId : string.Empty;
            uint payloadHash = string.IsNullOrWhiteSpace(itemId)
                ? 0u
                : unchecked((uint)LocHash.Compute(itemId));

            EvaluateSignal(new QuestSignal(QuestSignalKind.ItemCollected, payloadHash, evt.Quantity));
        }

        private void HandleBiomeDiscovered(BiomeDiscoveredEvent evt)
        {
            if (evt == null)
                return;

            EvaluateSignal(new QuestSignal(QuestSignalKind.BiomeEntered, 0u, evt.BiomeId));
        }

        private void HandleLoreAcquired(LoreAcquiredEvent evt)
        {
            if (evt == null)
                return;

            EvaluateSignal(new QuestSignal(QuestSignalKind.DiscoveryMade, evt.LoreHash, 0f));
            EvaluateSignal(new QuestSignal(QuestSignalKind.AudioLogFound, evt.LoreHash, 0f));
        }

        private void HandleDepthTierReached(int tier)
        {
            UpdateDepth(MapDepthTierToMeters(tier));
        }

        private void HandleEclipseStart()
        {
            EvaluateSignal(new QuestSignal(QuestSignalKind.EclipseStarted, 0u, 0f));
        }

        private void HandleSignalDecoded(string messageId)
        {
            uint payloadHash = string.IsNullOrWhiteSpace(messageId)
                ? 0u
                : unchecked((uint)LocHash.Compute(messageId));

            EvaluateSignal(new QuestSignal(QuestSignalKind.SignalDecoded, payloadHash, 0f));
        }

        private void EvaluateSignal(QuestSignal signal)
        {
            if (_stateManager == null)
                return;

            _stateManager.EvaluateSignal(signal);
            FlushRuntimeResults();
        }

        private void FlushRuntimeResults()
        {
            if (_stateManager == null)
                return;

            for (int i = 0; i < _stateManager.ResultCount; i++)
            {
                QuestRuntimeResult result = _stateManager.GetResult(i);
                EmitQuestTransition(result.QuestIndex, result.Completed);
            }
        }

        private void EmitQuestTransition(int questIndex, bool completed)
        {
            QuestData questData = GetQuestDataByIndex(questIndex);
            if (questData == null || string.IsNullOrWhiteSpace(questData.questId))
                return;

            string title = questData.DisplayTitleOrFallback;
            LocalizationManager localization = LocalizationManager.Instance;
            if (completed)
            {
                QuestEvents.RaiseCompleted(questData.questId);
                NotificationEvents.PushInfo(localization != null
                    ? localization.GetFormatted(LocalizationKeys.QUEST_COMPLETED, title)
                    : "OBJECTIVE COMPLETED: " + title);
                return;
            }

            QuestEvents.RaiseActivated(questData.questId);
            NotificationEvents.PushInfo(localization != null
                ? localization.GetFormatted(LocalizationKeys.QUEST_NEW_OBJECTIVE, title)
                : "NEW OBJECTIVE: " + title);
        }

        private QuestData GetQuestDataByIndex(int questIndex)
        {
            return questIndex >= 0 && questIndex < allQuests.Length
                ? allQuests[questIndex]
                : null;
        }

        private bool TryResolveQuestHash(string questId, bool logUnknownQuest, out uint questHash, out QuestData questData)
        {
            questHash = 0u;
            questData = null;

            if (string.IsNullOrWhiteSpace(questId) || _hasLookupAmbiguity)
                return false;

            if (!TryResolveQuestData(questId, out questData))
            {
                if (logUnknownQuest)
                    Debug.LogWarning($"[QuestManager] Unknown questId '{questId}'.");

                return false;
            }

            questHash = unchecked((uint)LocHash.Compute(questId));
            return questHash != 0u;
        }

        private void BuildLookup()
        {
            _questLookup.Clear();
            _hasLookupAmbiguity = false;
            _lookupAmbiguitySummary = string.Empty;

            for (int i = 0; i < allQuests.Length; i++)
            {
                QuestData questData = allQuests[i];
                if (questData == null || string.IsNullOrWhiteSpace(questData.questId))
                    continue;

                if (_questLookup.TryGetValue(questData.questId, out QuestData existingQuestData) &&
                    !ReferenceEquals(existingQuestData, questData))
                {
                    RegisterLookupAmbiguity(questData.questId, existingQuestData, questData);
                    continue;
                }

                _questLookup[questData.questId] = questData;
            }
        }

        private bool TryResolveQuestData(string questId, out QuestData questData)
        {
            if (_questLookup.Count == 0 && allQuests != null && allQuests.Length > 0)
                BuildLookup();

            return _questLookup.TryGetValue(questId, out questData);
        }

        private void RegisterLookupAmbiguity(string questId, QuestData existingQuestData, QuestData incomingQuestData)
        {
            _hasLookupAmbiguity = true;
            if (!string.IsNullOrEmpty(_lookupAmbiguitySummary))
                return;

            string existingName = existingQuestData != null ? existingQuestData.name : "null";
            string incomingName = incomingQuestData != null ? incomingQuestData.name : "null";
            _lookupAmbiguitySummary = $"questId '{questId}' resolves to both '{existingName}' and '{incomingName}'.";
        }

        private static float MapDepthTierToMeters(int tier)
        {
            switch (tier)
            {
                case 2:
                    return DepthTierTwoMeters;
                case 3:
                    return DepthTierThreeMeters;
                case 4:
                    return DepthTierFourMeters;
                default:
                    return 0f;
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

            QuestData[] loadedQuests = new QuestData[guids.Length];
            int loadedCount = 0;
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                QuestData questData = AssetDatabase.LoadAssetAtPath<QuestData>(path);
                if (questData == null)
                    continue;

                loadedQuests[loadedCount++] = questData;
            }

            if (loadedCount <= 0)
                return;

            if (loadedCount != loadedQuests.Length)
                Array.Resize(ref loadedQuests, loadedCount);

            allQuests = loadedQuests;
            EditorUtility.SetDirty(this);
        }
#endif
    }
}
