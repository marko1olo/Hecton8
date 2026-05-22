using System;
using System.Collections.Generic;
using Hecton8.Core;
using Hecton8.SaveSystem;
using Hecton8.UI;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Hecton8.Quest
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-130)]
    public sealed class QuestManager : MonoBehaviour, ISaveable, IQuestSystem
    {
        [Header("Quest Registry")]
        [Tooltip("All authored quest assets assigned to this runtime owner.")]
        [SerializeField] private QuestData[] allQuests = Array.Empty<QuestData>();

        private const string QuestFolder = "Assets/_Project/Data/Lore/Quests";
        private const float DepthTierTwoMeters = 100f;
        private const float DepthTierThreeMeters = 300f;
        private const float DepthTierFourMeters = 1000f;

        private static uint[] s_stagedLoadedPackedState;
        private static QuestSaveHeader s_stagedLoadedQuestHeader;

        internal static QuestManager ActiveRuntimeInstance { get; private set; }

        // COLD ALLOC: Dictionary<uint,QuestData>[64] - authored quest lookup by stable FNV quest hash - owner: QuestManager
        private readonly Dictionary<uint, QuestData> _questHashLookup = new Dictionary<uint, QuestData>(64);

        private QuestStateManager _stateManager;
        private QuestGraphEvaluator _graphEvaluator;
        private bool _loadedFromSave;
        private bool _hasLookupAmbiguity;
        private bool _serviceRegistered;
        private ISaveService _registeredSaveService;
        private string _lookupAmbiguitySummary = string.Empty;
        private uint[] _questCompletedNotificationHashes = Array.Empty<uint>();
        private uint[] _questRestoredNotificationHashes = Array.Empty<uint>();
        private uint[] _questNewNotificationHashes = Array.Empty<uint>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            s_stagedLoadedPackedState = null;
            s_stagedLoadedQuestHeader = default;
            ActiveRuntimeInstance = null;
        }

        public int SavePriority => 7;

        public int LoadPriority => 7;

        internal int PackedStateWordCount => _stateManager != null ? _stateManager.WordCount : 0;

        /// <summary>
        /// True once the quest runtime owner is registered in the global registry.
        /// </summary>
        public bool IsInitialized => ReferenceEquals(GlobalRegistry.QuestSystem, this) &&
                                     ReferenceEquals(GlobalRegistry.Quest, this);

        private void Awake()
        {
            BuildLookup();
            InitializeStateGraph();
        }

        private void OnEnable()
        {
            QuestManager registeredQuest = GlobalRegistry.Quest;
            if (registeredQuest != null && registeredQuest != this)
            {
                Destroy(gameObject);
                return;
            }

            ActiveRuntimeInstance = this;
            GlobalRegistry.RegisterQuestRuntime(this);
            _serviceRegistered = ReferenceEquals(GlobalRegistry.Quest, this);

            _registeredSaveService = GlobalRegistry.Save;
            _registeredSaveService?.Register(this);
            _graphEvaluator?.Bind();
        }

        private void OnDisable()
        {
            if (ReferenceEquals(ActiveRuntimeInstance, this))
                ActiveRuntimeInstance = null;

            _graphEvaluator?.Unbind();

            _registeredSaveService?.Unregister(this);
            _registeredSaveService = null;

            if (_serviceRegistered)
            {
                GlobalRegistry.UnregisterQuestRuntime(this);
                _serviceRegistered = false;
            }
        }

        private void OnDestroy()
        {
            if (ReferenceEquals(ActiveRuntimeInstance, this))
                ActiveRuntimeInstance = null;

            _registeredSaveService?.Unregister(this);
            _registeredSaveService = null;

            _graphEvaluator?.Dispose();
            _graphEvaluator = null;

            if (_stateManager != null)
            {
                _stateManager.Dispose();
                _stateManager = null;
            }

            if (_serviceRegistered)
            {
                GlobalRegistry.UnregisterQuestRuntime(this);
                _serviceRegistered = false;
            }
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

            ActivateQuest(questHash);
        }

        public void ActivateQuest(uint questHash)
        {
            if (questHash == 0u || _stateManager == null || !_stateManager.TryActivateQuest(questHash, out int questIndex))
                return;

            _stateManager.RecordManualTransition(questIndex, completed: false);
            EmitQuestTransition(questIndex, completed: false, QuestTransitionType.Activate);
        }

        public void CompleteQuest(string questId)
        {
            if (!TryResolveQuestHash(questId, logUnknownQuest: true, out uint questHash, out _))
                return;

            CompleteQuest(questHash);
        }

        public void CompleteQuest(uint questHash)
        {
            if (questHash == 0u || _stateManager == null || !_stateManager.TryCompleteQuest(questHash, out int questIndex))
                return;

            _stateManager.RecordManualTransition(questIndex, completed: true);
            EmitQuestTransition(questIndex, completed: true, QuestTransitionType.Complete);
        }

        public bool IsActive(string questId)
        {
            return TryResolveQuestHash(questId, logUnknownQuest: false, out uint questHash, out _) &&
                   _stateManager != null &&
                   _stateManager.IsQuestActive(questHash);
        }

        public bool IsActive(uint questHash)
        {
            return questHash != 0u &&
                   _stateManager != null &&
                   _stateManager.IsQuestActive(questHash);
        }

        public bool IsCompleted(string questId)
        {
            return TryResolveQuestHash(questId, logUnknownQuest: false, out uint questHash, out _) &&
                   _stateManager != null &&
                   _stateManager.IsQuestCompleted(questHash);
        }

        public bool IsCompleted(uint questHash)
        {
            return questHash != 0u &&
                   _stateManager != null &&
                   _stateManager.IsQuestCompleted(questHash);
        }

        public bool GetFlag(uint flagId)
        {
            return flagId != 0u &&
                   _stateManager != null &&
                   _stateManager.GetFlag(flagId);
        }

        public void UpdateDepth(float depthMeters)
        {
            _graphEvaluator?.UpdateDepth(depthMeters);
        }

        public void UpdateDepthContext(float depthMeters, uint zoneHash, bool isThermalZone)
        {
            _graphEvaluator?.UpdateDepthContext(depthMeters, zoneHash, isThermalZone);
        }

        internal int CopyActiveQuestHashes(uint[] destination)
        {
            return _stateManager != null
                ? _stateManager.CopyActiveQuestHashes(destination)
                : 0;
        }

        public bool TryGetQuestIdByHash(uint questHash, out string questId)
        {
            questId = string.Empty;
            if (questHash == 0u)
                return false;

            if (_questHashLookup.Count == 0 && allQuests != null && allQuests.Length > 0)
                BuildLookup();

            if (!_questHashLookup.TryGetValue(questHash, out QuestData questData) || questData == null || string.IsNullOrWhiteSpace(questData.questId))
                return false;

            questId = questData.questId;
            return true;
        }

        internal bool TryGetQuestDataByHash(uint questHash, out QuestData questData)
        {
            questData = null;
            if (questHash == 0u)
                return false;

            if (_questHashLookup.Count == 0 && allQuests != null && allQuests.Length > 0)
                BuildLookup();

            return _questHashLookup.TryGetValue(questHash, out questData) && questData != null;
        }

        internal bool TryGetQuestPresentation(
            uint questHash,
            out string title,
            out string description,
            out uint markerTargetHash,
            out Vector3 markerWorldPosition,
            out float markerHeightOffset)
        {
            title = string.Empty;
            description = string.Empty;
            markerTargetHash = 0u;
            markerWorldPosition = default;
            markerHeightOffset = 0f;

            if (_stateManager != null &&
                _stateManager.TryGetQuestPresentation(
                    questHash,
                    out title,
                    out description,
                    out markerTargetHash,
                    out markerWorldPosition,
                    out markerHeightOffset))
            {
                return true;
            }

            if (!TryGetQuestDataByHash(questHash, out QuestData questData) || questData == null)
                return false;

            title = questData.DisplayTitleOrFallback;
            description = questData.DescriptionOrFallback;
            markerTargetHash = 0u;
            markerWorldPosition = questData.markerWorldPosition;
            markerHeightOffset = math.max(0f, questData.markerHeightOffset);
            return true;
        }

        internal bool UpsertProceduralDirective(
            uint questHash,
            uint completionItemHash,
            string title,
            string description,
            uint markerTargetHash,
            Vector3 markerWorldPosition,
            float markerHeightOffset,
            QuestPhaseGateType phaseGate,
            float requiredQuantity,
            bool activateWhenAllowed,
            out bool activatedNow)
        {
            activatedNow = false;
            if (_stateManager == null)
                return false;

            bool updated = _stateManager.TryUpsertProceduralDirective(
                questHash,
                completionItemHash,
                title,
                description,
                markerTargetHash,
                markerWorldPosition,
                markerHeightOffset,
                phaseGate,
                requiredQuantity,
                activateWhenAllowed,
                out _,
                out activatedNow);
            if (updated && activatedNow)
                FlushRuntimeResults();

            return updated;
        }

        internal unsafe bool TryCopyPackedStateSnapshot(void* destinationPtr, int destinationWordCapacity, out QuestSaveHeader header, double timestamp)
        {
            header = _stateManager != null
                ? _stateManager.BuildSaveHeader(timestamp)
                : default;
            return _stateManager != null &&
                   _stateManager.TryCopyPackedStateSnapshot(destinationPtr, destinationWordCapacity);
        }

        public static void StageLoadedPackedState(uint[] packedWords)
        {
            StageLoadedPackedState(default, packedWords);
        }

        internal static void StageLoadedPackedState(in QuestSaveHeader header, uint[] packedWords)
        {
            s_stagedLoadedQuestHeader = header;
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

            QuestData[] quests = allQuests ?? Array.Empty<QuestData>();
            int questCapacity = quests.Length;
            if (data.questActiveIds == null)
                data.questActiveIds = new List<string>(questCapacity); // COLD ALLOC: List<string>[questCapacity] - legacy save active quest id staging - owner: QuestManager
            else if (data.questActiveIds.Capacity < questCapacity)
                data.questActiveIds.Capacity = questCapacity;

            if (data.questCompletedIds == null)
                data.questCompletedIds = new List<string>(questCapacity); // COLD ALLOC: List<string>[questCapacity] - legacy save completed quest id staging - owner: QuestManager
            else if (data.questCompletedIds.Capacity < questCapacity)
                data.questCompletedIds.Capacity = questCapacity;

            data.questActiveIds.Clear();
            data.questCompletedIds.Clear();

            for (int i = 0; i < quests.Length; i++)
            {
                QuestData questData = quests[i];
                if (questData == null || string.IsNullOrWhiteSpace(questData.questId))
                    continue;

                uint questHash = QuestFlagHashKernel.ComputeStableHash(questData.questId);
                if (IsActive(questHash))
                    data.questActiveIds.Add(questData.questId);

                if (IsCompleted(questHash))
                    data.questCompletedIds.Add(questData.questId);
            }
        }

        public void LoadFromSaveData(SaveData data)
        {
            _loadedFromSave = true;
            if (_stateManager == null)
                return;

            uint[] stagedWords = s_stagedLoadedPackedState;
            QuestSaveHeader stagedHeader = s_stagedLoadedQuestHeader;
            s_stagedLoadedPackedState = null;
            s_stagedLoadedQuestHeader = default;

            if (stagedWords != null && stagedWords.Length > 0)
            {
                _stateManager.RestorePackedState(stagedHeader, stagedWords);
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
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogError($"[QuestManager] Quest registry ambiguity detected: {_lookupAmbiguitySummary}");
#endif
                enabled = false;
                return;
            }

            if (!initialized || _stateManager.HasCompileErrors)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                string compileSummary = _stateManager != null ? _stateManager.CompileErrorSummary : "Unknown quest compile error.";
                Debug.LogError($"[QuestManager] Quest state graph compilation failed.{System.Environment.NewLine}{compileSummary}");
#endif
                enabled = false;
                return;
            }

            _graphEvaluator?.Dispose();
            _graphEvaluator = new QuestGraphEvaluator(_stateManager, FlushRuntimeResults);
        }

        private void FlushRuntimeResults()
        {
            if (_stateManager == null)
                return;

            for (int i = 0; i < _stateManager.ResultCount; i++)
            {
                QuestRuntimeResult result = _stateManager.GetResult(i);
                EmitQuestTransition(result.QuestIndex, result.Completed != 0, result.TransitionType);
            }
        }

        private void EmitQuestTransition(int questIndex, bool completed, QuestTransitionType transitionType)
        {
            if (_stateManager == null ||
                !_stateManager.TryGetQuestHash(questIndex, out uint questHash))
            {
                return;
            }

            uint notificationHash = ResolveQuestNotificationHash(questIndex, completed, transitionType);

            switch (transitionType)
            {
                case QuestTransitionType.Complete:
                    QuestEvents.RaiseCompleted(questHash);
                    break;

                case QuestTransitionType.Revert:
                    QuestEvents.RaiseActivated(questHash);
                    break;

                default:
                    QuestEvents.RaiseActivated(questHash);
                    break;
            }

            if (notificationHash != 0u)
                NotificationEvents.PushRegisteredInfo(notificationHash);
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

            questHash = QuestFlagHashKernel.ComputeStableHash(questId);
            if (questHash == 0u || !TryResolveQuestData(questHash, out questData))
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if (logUnknownQuest)
                    Debug.LogWarning($"[QuestManager] Unknown questId '{questId}'.");
#endif

                return false;
            }

            return true;
        }

        private void BuildLookup()
        {
            _questHashLookup.Clear();
            _hasLookupAmbiguity = false;
            _lookupAmbiguitySummary = string.Empty;
            EnsureQuestNotificationCacheCapacity(allQuests.Length);

            for (int i = 0; i < allQuests.Length; i++)
            {
                QuestData questData = allQuests[i];
                if (questData == null || string.IsNullOrWhiteSpace(questData.questId))
                    continue;

                uint questHash = QuestFlagHashKernel.ComputeStableHash(questData.questId);
                if (questHash == 0u)
                    continue;

                if (_questHashLookup.TryGetValue(questHash, out QuestData existingQuestData) &&
                    !ReferenceEquals(existingQuestData, questData))
                {
                    RegisterLookupAmbiguity(questData.questId, existingQuestData, questData);
                    continue;
                }

                _questHashLookup[questHash] = questData;

                CacheQuestNotificationHashes(i, questData);
            }
        }

        private void EnsureQuestNotificationCacheCapacity(int questCount)
        {
            if (_questCompletedNotificationHashes.Length == questCount &&
                _questRestoredNotificationHashes.Length == questCount &&
                _questNewNotificationHashes.Length == questCount)
            {
                return;
            }

            // COLD ALLOC: uint[questCount] - pre-registered objective complete notification hashes - owner: QuestManager
            _questCompletedNotificationHashes = new uint[questCount];
            // COLD ALLOC: uint[questCount] - pre-registered objective restored notification hashes - owner: QuestManager
            _questRestoredNotificationHashes = new uint[questCount];
            // COLD ALLOC: uint[questCount] - pre-registered new objective notification hashes - owner: QuestManager
            _questNewNotificationHashes = new uint[questCount];
        }

        private void CacheQuestNotificationHashes(int questIndex, QuestData questData)
        {
            if (questIndex < 0 || questIndex >= _questCompletedNotificationHashes.Length || questData == null)
                return;

            string title = questData.DisplayTitleOrFallback;
            if (string.IsNullOrWhiteSpace(title))
                title = "UNKNOWN OBJECTIVE";

            _questCompletedNotificationHashes[questIndex] = NotificationEvents.RegisterMessage("OBJECTIVE COMPLETED: " + title);
            _questRestoredNotificationHashes[questIndex] = NotificationEvents.RegisterMessage("OBJECTIVE RESTORED: " + title);
            _questNewNotificationHashes[questIndex] = NotificationEvents.RegisterMessage("NEW OBJECTIVE: " + title);
        }

        private uint ResolveQuestNotificationHash(int questIndex, bool completed, QuestTransitionType transitionType)
        {
            if (questIndex < 0 || questIndex >= _questCompletedNotificationHashes.Length)
                return 0u;

            switch (transitionType)
            {
                case QuestTransitionType.Complete:
                    return _questCompletedNotificationHashes[questIndex];
                case QuestTransitionType.Revert:
                    return _questRestoredNotificationHashes[questIndex];
                default:
                    return completed
                        ? _questCompletedNotificationHashes[questIndex]
                        : _questNewNotificationHashes[questIndex];
            }
        }

        private bool TryResolveQuestData(uint questHash, out QuestData questData)
        {
            questData = null;

            if (_questHashLookup.Count == 0 && allQuests != null && allQuests.Length > 0)
                BuildLookup();

            return questHash != 0u && _questHashLookup.TryGetValue(questHash, out questData);
        }

        private void RegisterLookupAmbiguity(string questId, QuestData existingQuestData, QuestData incomingQuestData)
        {
            _hasLookupAmbiguity = true;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!string.IsNullOrEmpty(_lookupAmbiguitySummary))
                return;

            string existingName = existingQuestData != null ? existingQuestData.name : "null";
            string incomingName = incomingQuestData != null ? incomingQuestData.name : "null";
            _lookupAmbiguitySummary = $"questId '{questId}' resolves to both '{existingName}' and '{incomingName}'.";
#endif
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
            string[] guids = AssetDatabase.FindAssets("t:QuestData", new[] { QuestFolder });
            if (guids == null || guids.Length == 0)
                return;

            List<QuestData> loadedQuests = new List<QuestData>(guids.Length); // COLD ALLOC: List<QuestData>[guids.Length] - editor-time quest registry bootstrap - owner: QuestManager
            if (allQuests != null)
            {
                for (int i = 0; i < allQuests.Length; i++)
                {
                    QuestData existing = allQuests[i];
                    if (existing != null && !loadedQuests.Contains(existing))
                        loadedQuests.Add(existing);
                }
            }

            int previousCount = loadedQuests.Count;
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                QuestData questData = AssetDatabase.LoadAssetAtPath<QuestData>(path);
                if (questData == null)
                    continue;

                if (!loadedQuests.Contains(questData))
                    loadedQuests.Add(questData);
            }

            if (loadedQuests.Count <= 0 || loadedQuests.Count == previousCount)
                return;

            allQuests = loadedQuests.ToArray();
            EditorUtility.SetDirty(this);
        }
#endif
    }
}
