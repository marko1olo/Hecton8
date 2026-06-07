using System;
using System.Collections.Generic;
using Hecton.Localization;
using Hecton8.Core;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Memory;
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
    public sealed class QuestManager : MonoBehaviour, ISaveable, IQuestSystem, IGlobalRegistryHotSwapListener
    {
        [Header("Quest Registry")]
        [Tooltip("All authored quest assets assigned to this runtime owner.")]
        [SerializeField] private QuestData[] allQuests = Array.Empty<QuestData>();

        private const string QuestFolder = "Assets/_Project/Data/Lore/Quests";
        private const string ObjectiveCompletedPrefix = "OBJECTIVE COMPLETED: ";
        private const string ObjectiveRestoredPrefix = "OBJECTIVE RESTORED: ";
        private const string ObjectiveNewPrefix = "NEW OBJECTIVE: ";
        private const string UnknownObjectiveLabel = "UNKNOWN OBJECTIVE";
        private const int QuestNotificationMessageCapacity = 192;
        private const int QuestNotificationTitleCapacity = 128;
        private const float DepthTierTwoMeters = 100f;
        private const float DepthTierThreeMeters = 300f;
        private const float DepthTierFourMeters = 1000f;
        private const float ZeigarnikHapticLow01 = 0.2f;
        private const float ZeigarnikHapticHigh01 = 0.9f;
        private const float ZeigarnikHapticSeconds = 0.09f;

        private static uint[] s_stagedLoadedPackedState;
        private static QuestSaveHeader s_stagedLoadedQuestHeader;
        private static int s_x001QuestManagerZeigarnikHapticDropCount;

        internal static QuestManager ActiveRuntimeInstance { get; private set; }

        // COLD ALLOC: Dictionary<uint,QuestData>[64] - authored quest lookup by stable FNV quest hash - owner: QuestManager
        private readonly Dictionary<uint, QuestData> _questHashLookup = new Dictionary<uint, QuestData>(64);

        private QuestStateManager _stateManager;
        private QuestGraphEvaluator _graphEvaluator;
        private bool _loadedFromSave;
        private bool _hasLookupAmbiguity;
        private bool _serviceRegistered;
        private ISaveService _registeredSaveService;
        private ILocalizationTextReadModel _localizationManager;
        private IDataVault _questDagVault;
        private QuestDagBufferHandles _questDagHandles;
        private int _questDagAuthoredDependencyLinkCount;
        private bool _hotSwapRegistered;
        private uint[] _questCompletedNotificationHashes = Array.Empty<uint>();
        private uint[] _questRestoredNotificationHashes = Array.Empty<uint>();
        private uint[] _questNewNotificationHashes = Array.Empty<uint>();
        private readonly char[] _questNotificationMessageBuffer = new char[QuestNotificationMessageCapacity];
        private readonly char[] _questNotificationTitleBuffer = new char[QuestNotificationTitleCapacity];

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
        public bool IsInitialized => _serviceRegistered && ReferenceEquals(ActiveRuntimeInstance, this);

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

            TryRegisterHotSwapListener();
            BindSaveService(GlobalRegistry.Save);
            BindLocalization(GlobalRegistry.LocalizationText);
            BindQuestDagVault(GlobalRegistry.DataVault);
            _graphEvaluator?.Bind();
        }

        private void OnDisable()
        {
            if (ReferenceEquals(ActiveRuntimeInstance, this))
                ActiveRuntimeInstance = null;

            _graphEvaluator?.Unbind();

            TryUnregisterHotSwapListener();
            BindSaveService(null);
            BindQuestDagVault(null);
            _localizationManager = null;

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

            TryUnregisterHotSwapListener();
            BindSaveService(null);
            BindQuestDagVault(null);
            _localizationManager = null;

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

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            switch (serviceSlot)
            {
                case GlobalRegistryServiceSlot.Save:
                    BindSaveService(currentService as ISaveService);
                    break;
                case GlobalRegistryServiceSlot.LocalizationRuntime:
                    BindLocalization(currentService as ILocalizationTextReadModel);
                    break;
                case GlobalRegistryServiceSlot.DataVault:
                    BindQuestDagVault(currentService as IDataVault);
                    break;
            }
        }

        private void BindSaveService(ISaveService saveService)
        {
            if (ReferenceEquals(_registeredSaveService, saveService))
                return;

            _registeredSaveService?.Unregister(this);
            _registeredSaveService = saveService;
            _registeredSaveService?.Register(this);
        }

        private void BindLocalization(ILocalizationTextReadModel localizationManager)
        {
            if (ReferenceEquals(_localizationManager, localizationManager))
                return;

            _localizationManager = localizationManager;
            RefreshQuestPresentationCaches();
        }

        private void BindQuestDagVault(IDataVault vault)
        {
            if (ReferenceEquals(_questDagVault, vault))
                return;

            ReleaseQuestDagSnapshotHandles();
            _questDagVault = vault;
            _questDagHandles = default;
            if (vault == null)
                return;

            SignalCorridorRuntime.EnsureHapticPulseSignalLaneInitialized();
            if (TryEnsureQuestDagSnapshotHandles(vault))
            {
                TryPublishAuthoredQuestDependencyLinks();
                TryPublishQuestDagStateSnapshot(0u, 0u);
            }
        }

        private void ReleaseQuestDagSnapshotHandles()
        {
            _questDagHandles = default;
        }

        private void RefreshQuestPresentationCaches()
        {
            QuestData[] quests = allQuests ?? Array.Empty<QuestData>();
            _stateManager?.RebindLocalization(_localizationManager, quests);

            EnsureQuestNotificationCacheCapacity(quests.Length);
            for (int i = 0; i < quests.Length; i++)
                CacheQuestNotificationHashes(i, quests[i]);
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
            TryPublishQuestDagStateSnapshot(0u, 0u);
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
            bool zeigarnikInjected = TryActivateZeigarnikChildQuest(questHash, out uint injectedQuestHash, out int injectedQuestIndex);
            EmitQuestTransition(questIndex, completed: true, QuestTransitionType.Complete);
            if (zeigarnikInjected)
            {
                TryPublishQuestDagStateSnapshot(questHash, injectedQuestHash);
                PublishZeigarnikHaptic();
                EmitQuestTransition(injectedQuestIndex, completed: false, QuestTransitionType.Activate);
                return;
            }

            TryPublishQuestDagStateSnapshot(0u, 0u);
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

        public int CopyActiveQuestHashes(uint[] destination)
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

        public bool TryCopyQuestPresentation(
            uint questHash,
            char[] titleDestination,
            out int titleLength,
            char[] descriptionDestination,
            out int descriptionLength,
            out uint markerTargetHash,
            out Vector3 markerWorldPosition,
            out float markerHeightOffset)
        {
            titleLength = 0;
            descriptionLength = 0;
            markerTargetHash = 0u;
            markerWorldPosition = default;
            markerHeightOffset = 0f;

            if (_stateManager != null &&
                _stateManager.TryCopyQuestPresentation(
                    questHash,
                    titleDestination,
                    out titleLength,
                    descriptionDestination,
                    out descriptionLength,
                    out markerTargetHash,
                    out markerWorldPosition,
                    out markerHeightOffset))
            {
                return true;
            }

            if (!TryGetQuestDataByHash(questHash, out QuestData questData) || questData == null)
                return false;

            if (titleDestination != null &&
                questData.TryWriteDisplayTitleOrFallback(_localizationManager, titleDestination, out int fallbackTitleLength))
            {
                titleLength = math.min(fallbackTitleLength, titleDestination.Length);
            }

            if (descriptionDestination != null &&
                questData.TryWriteDescriptionOrFallback(_localizationManager, descriptionDestination, out int fallbackDescriptionLength))
            {
                descriptionLength = math.min(fallbackDescriptionLength, descriptionDestination.Length);
            }

            markerTargetHash = 0u;
            markerWorldPosition = questData.RuntimeMarkerWorldPosition;
            markerHeightOffset = questData.RuntimeMarkerHeightOffset;
            return titleLength > 0 || markerWorldPosition.sqrMagnitude > 0.0001f;
        }

        public bool UpsertProceduralDirective(
            uint questHash,
            uint completionItemHash,
            string title,
            string description,
            uint markerTargetHash,
            Vector3 markerWorldPosition,
            float markerHeightOffset,
            byte phaseGateCode,
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
                (QuestPhaseGateType)phaseGateCode,
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
            header = default;
            if (_stateManager == null)
                return false;

            QuestSaveHeader candidateHeader = _stateManager.BuildSaveHeader(timestamp);
            if (!_stateManager.TryCopyPackedStateSnapshot(destinationPtr, destinationWordCapacity))
                return false;

            header = candidateHeader;
            return true;
        }

        public static void StageLoadedPackedState(uint[] packedWords)
        {
            StageLoadedPackedState(default, packedWords);
        }

        internal static void StageLoadedPackedState(in QuestSaveHeader header, uint[] packedWords)
        {
            if (packedWords == null || packedWords.Length <= 0)
            {
                s_stagedLoadedPackedState = null;
                s_stagedLoadedQuestHeader = default;
                return;
            }

            s_stagedLoadedQuestHeader = header;
            int wordCount = Math.Min(packedWords.Length, QuestRuntimeLayout.WordCapacity);
            if (s_stagedLoadedPackedState == null || s_stagedLoadedPackedState.Length != wordCount)
            {
                // COLD ALLOC: uint[wordCount] - staged packed quest words from SaveManager load handoff - owner: QuestManager
                s_stagedLoadedPackedState = new uint[wordCount];
            }

            Array.Copy(packedWords, s_stagedLoadedPackedState, wordCount);
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
                TryPublishQuestDagStateSnapshot(0u, 0u);
                return;
            }

            List<string> activeQuestIds = data != null ? data.questActiveIds : null;
            List<string> completedQuestIds = data != null ? data.questCompletedIds : null;
            _stateManager.RestoreLegacyState(activeQuestIds, completedQuestIds);
            TryPublishQuestDagStateSnapshot(0u, 0u);
        }

        private void InitializeStateGraph()
        {
            if (_stateManager == null)
                _stateManager = new QuestStateManager();

            bool initialized = _stateManager.Initialize(allQuests, _localizationManager);
            if (_hasLookupAmbiguity)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Hecton8.Core.H8Debug.LogError("[QuestManager] Quest registry ambiguity detected.");
#endif
                enabled = false;
                return;
            }

            if (!initialized || _stateManager.HasCompileErrors)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Hecton8.Core.H8Debug.LogError("[QuestManager] Quest state graph compilation failed.");
#endif
                enabled = false;
                return;
            }

            _graphEvaluator?.Dispose();
            _graphEvaluator = new QuestGraphEvaluator(_stateManager, FlushRuntimeResults);
            TryPublishAuthoredQuestDependencyLinks();
            TryPublishQuestDagStateSnapshot(0u, 0u);
        }

        private void FlushRuntimeResults()
        {
            if (_stateManager == null)
                return;

            bool wroteZeigarnikSnapshot = false;
            for (int i = 0; i < _stateManager.ResultCount; i++)
            {
                QuestRuntimeResult result = _stateManager.GetResult(i);
                uint completedQuestHash = 0u;
                uint injectedQuestHash = 0u;
                int injectedQuestIndex = -1;
                bool zeigarnikInjected = result.TransitionType == QuestTransitionType.Complete &&
                                          _stateManager.TryGetQuestHash(result.QuestIndex, out completedQuestHash) &&
                                          TryActivateZeigarnikChildQuest(completedQuestHash, out injectedQuestHash, out injectedQuestIndex);

                EmitQuestTransition(result.QuestIndex, result.Completed != 0, result.TransitionType);
                if (zeigarnikInjected)
                {
                    TryPublishQuestDagStateSnapshot(completedQuestHash, injectedQuestHash);
                    wroteZeigarnikSnapshot = true;
                    PublishZeigarnikHaptic();
                    EmitQuestTransition(injectedQuestIndex, completed: false, QuestTransitionType.Activate);
                }
            }

            if (!wroteZeigarnikSnapshot)
                TryPublishQuestDagStateSnapshot(0u, 0u);
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
                    QuestEvents.TryRaiseCompleted(questHash);
                    break;

                case QuestTransitionType.Revert:
                    QuestEvents.TryRaiseActivated(questHash);
                    break;

                default:
                    QuestEvents.TryRaiseActivated(questHash);
                    break;
            }

            if (notificationHash != 0u)
                NotificationEvents.TryPushRegisteredInfo(notificationHash);
        }

        private QuestData GetQuestDataByIndex(int questIndex)
        {
            QuestData[] quests = allQuests;
            return quests != null && questIndex >= 0 && questIndex < quests.Length
                ? quests[questIndex]
                : null;
        }

        private bool TryActivateZeigarnikChildQuest(uint completedQuestHash, out uint injectedQuestHash, out int injectedQuestIndex)
        {
            injectedQuestHash = 0u;
            injectedQuestIndex = -1;
            if (completedQuestHash == 0u || _stateManager == null || allQuests == null)
                return false;

            for (int i = 0; i < allQuests.Length; i++)
            {
                QuestData questData = allQuests[i];
                if (questData == null || string.IsNullOrWhiteSpace(questData.questId))
                    continue;

                uint candidateQuestHash = QuestFlagHashKernel.ComputeStableHash(questData.questId);
                if (candidateQuestHash == 0u ||
                    _stateManager.IsQuestActive(candidateQuestHash) ||
                    _stateManager.IsQuestCompleted(candidateQuestHash) ||
                    !AreZeigarnikPrerequisitesSatisfied(questData, completedQuestHash))
                {
                    continue;
                }

                if (!_stateManager.TryActivateQuest(candidateQuestHash, out injectedQuestIndex))
                    continue;

                _stateManager.RecordManualTransition(injectedQuestIndex, completed: false);
                injectedQuestHash = candidateQuestHash;
                return true;
            }

            return false;
        }

        private bool AreZeigarnikPrerequisitesSatisfied(QuestData questData, uint completedQuestHash)
        {
            string[] prerequisiteQuestIds = questData != null ? questData.prerequisiteQuestIds : null;
            if (prerequisiteQuestIds == null || prerequisiteQuestIds.Length == 0)
                return false;

            bool containsCompletedQuest = false;
            for (int i = 0; i < prerequisiteQuestIds.Length; i++)
            {
                string prerequisiteQuestId = prerequisiteQuestIds[i];
                if (string.IsNullOrWhiteSpace(prerequisiteQuestId))
                    return false;

                uint prerequisiteQuestHash = QuestFlagHashKernel.ComputeStableHash(prerequisiteQuestId);
                if (prerequisiteQuestHash == 0u || !_stateManager.IsQuestCompleted(prerequisiteQuestHash))
                    return false;

                containsCompletedQuest |= prerequisiteQuestHash == completedQuestHash;
            }

            return containsCompletedQuest;
        }

        private bool TryEnsureQuestDagSnapshotHandles(IDataVault vault)
        {
            if (vault == null || vault.IsAllocationLocked || vault.IsCompactionFenceActive)
                return false;

            int questCount = allQuests != null ? allQuests.Length : 0;
            int questStateCapacity = math.max(QuestDagRuntimeConstants.DefaultQuestStateCapacity, math.max(1, questCount));
            int dependencyLinkCapacity = math.max(1, _questDagAuthoredDependencyLinkCount);

            if (_questDagHandles.QuestStates.Generation != 0u &&
                _questDagHandles.DependencyLinks.Generation != 0u &&
                _questDagHandles.Counters.Generation != 0u &&
                _questDagHandles.QuestStateCapacity >= questStateCapacity &&
                _questDagHandles.DependencyLinkCapacity >= dependencyLinkCapacity)
            {
                if (vault.TryGetGenerationHandle<QuestStateDTO>(
                        BufferID.QuestDagQuestStates,
                        out VaultGenerationHandle<QuestStateDTO> questStates) &&
                    vault.TryGetGenerationHandle<QuestDependencyLinkDTO>(
                        BufferID.QuestDagDependencyLinks,
                        out VaultGenerationHandle<QuestDependencyLinkDTO> dependencyLinks) &&
                    vault.TryGetGenerationHandle<int>(
                        BufferID.QuestDagCounters,
                        out VaultGenerationHandle<int> counters))
                {
                    _questDagHandles.QuestStates = questStates;
                    _questDagHandles.DependencyLinks = dependencyLinks;
                    _questDagHandles.Counters = counters;
                    return true;
                }
            }

            _questDagHandles.QuestStateCapacity = questStateCapacity;
            _questDagHandles.DependencyLinkCapacity = dependencyLinkCapacity;
            _questDagHandles.Counters = vault.EnsureGenerationHandle<int>(
                BufferID.QuestDagCounters,
                QuestDagRuntimeConstants.CounterCount,
                SystemID.QuestDag,
                NativeArrayOptions.ClearMemory);
            _questDagHandles.QuestStates = vault.EnsureGenerationHandle<QuestStateDTO>(
                BufferID.QuestDagQuestStates,
                questStateCapacity,
                SystemID.QuestDag,
                NativeArrayOptions.ClearMemory);
            _questDagHandles.DependencyLinks = vault.EnsureGenerationHandle<QuestDependencyLinkDTO>(
                BufferID.QuestDagDependencyLinks,
                dependencyLinkCapacity,
                SystemID.QuestDag,
                NativeArrayOptions.ClearMemory);

            return _questDagHandles.Counters.Generation != 0u &&
                   _questDagHandles.QuestStates.Generation != 0u &&
                   _questDagHandles.DependencyLinks.Generation != 0u;
        }

        private int CountAuthoredQuestDependencyLinks()
        {
            if (allQuests == null)
                return 0;

            int count = 0;
            for (int questIndex = 0; questIndex < allQuests.Length; questIndex++)
            {
                QuestData questData = allQuests[questIndex];
                if (questData == null || string.IsNullOrWhiteSpace(questData.questId))
                    continue;

                string[] prerequisiteQuestIds = questData.prerequisiteQuestIds;
                if (prerequisiteQuestIds == null)
                    continue;

                for (int i = 0; i < prerequisiteQuestIds.Length; i++)
                {
                    if (!string.IsNullOrWhiteSpace(prerequisiteQuestIds[i]) &&
                        QuestFlagHashKernel.ComputeStableHash(prerequisiteQuestIds[i]) != 0u)
                    {
                        count++;
                    }
                }
            }

            return count;
        }

        private bool TryPublishAuthoredQuestDependencyLinks()
        {
            IDataVault vault = _questDagVault;
            if (!TryEnsureQuestDagSnapshotHandles(vault) ||
                !QuestDagVault.TryAcquireQuestDagWriteBuffer(
                    vault,
                    in _questDagHandles.DependencyLinks,
                    BufferID.QuestDagDependencyLinks,
                    _questDagHandles.DependencyLinkCapacity,
                    out NativeArray<QuestDependencyLinkDTO> links))
            {
                return false;
            }

            int count = 0;
            try
            {
                if (allQuests != null)
                {
                    for (int questIndex = 0; questIndex < allQuests.Length; questIndex++)
                    {
                        QuestData questData = allQuests[questIndex];
                        if (questData == null || string.IsNullOrWhiteSpace(questData.questId))
                            continue;

                        uint childQuestHash = QuestFlagHashKernel.ComputeStableHash(questData.questId);
                        if (childQuestHash == 0u)
                            continue;

                        string[] prerequisiteQuestIds = questData.prerequisiteQuestIds;
                        if (prerequisiteQuestIds == null)
                            continue;

                        for (int i = 0; i < prerequisiteQuestIds.Length; i++)
                        {
                            if ((uint)count >= (uint)links.Length)
                                break;

                            string prerequisiteQuestId = prerequisiteQuestIds[i];
                            if (string.IsNullOrWhiteSpace(prerequisiteQuestId))
                                continue;

                            uint parentQuestHash = QuestFlagHashKernel.ComputeStableHash(prerequisiteQuestId);
                            if (parentQuestHash == 0u)
                                continue;

                            links[count++] = new QuestDependencyLinkDTO
                            {
                                ParentQuestHashID = parentQuestHash,
                                ChildQuestHashID = childQuestHash,
                                Flags = 0u
                            };
                        }
                    }
                }

                MockQuestDatabase.SortDependencyLinks(links, count);
                for (int i = count; i < links.Length; i++)
                    links[i] = default;
            }
            finally
            {
                vault.ReleaseWriteLock(in _questDagHandles.DependencyLinks, SystemID.QuestDag);
            }

            return TryWriteQuestDagCounters(-1, count, false, false);
        }

        private bool TryPublishQuestDagStateSnapshot(uint completedQuestHash, uint injectedQuestHash)
        {
            IDataVault vault = _questDagVault;
            if (_stateManager == null ||
                !TryEnsureQuestDagSnapshotHandles(vault) ||
                !QuestDagVault.TryAcquireQuestDagWriteBuffer(
                    vault,
                    in _questDagHandles.QuestStates,
                    BufferID.QuestDagQuestStates,
                    _questDagHandles.QuestStateCapacity,
                    out NativeArray<QuestStateDTO> questStates))
            {
                return false;
            }

            int count = 0;
            bool zeigarnikInjected = completedQuestHash != 0u && injectedQuestHash != 0u;
            bool failClosed = false;
            try
            {
                count = _stateManager.CopyActiveQuestStates(questStates, _questDagHandles.QuestStateCapacity);
                if (zeigarnikInjected && (uint)count < (uint)questStates.Length)
                {
                    questStates[count++] = new QuestStateDTO
                    {
                        ActiveQuestHashID = completedQuestHash,
                        CompletionProgress = 1f,
                        InjectedSubQuestHashID = injectedQuestHash,
                        StateFlags = (uint)(QuestStateFlags.ZeigarnikProgressArmed | QuestStateFlags.ZeigarnikInjected)
                    };
                }
                else if (zeigarnikInjected)
                {
                    failClosed = true;
                }

                for (int i = count; i < questStates.Length; i++)
                    questStates[i] = default;
            }
            finally
            {
                vault.ReleaseWriteLock(in _questDagHandles.QuestStates, SystemID.QuestDag);
            }

            return TryWriteQuestDagCounters(count, -1, zeigarnikInjected && !failClosed, failClosed);
        }

        private bool TryWriteQuestDagCounters(
            int questStateCount,
            int dependencyLinkCount,
            bool zeigarnikInjected,
            bool zeigarnikFailClosed)
        {
            IDataVault vault = _questDagVault;
            if (!TryEnsureQuestDagSnapshotHandles(vault) ||
                !QuestDagVault.TryAcquireQuestDagWriteBuffer(
                    vault,
                    in _questDagHandles.Counters,
                    BufferID.QuestDagCounters,
                    QuestDagRuntimeConstants.CounterCount,
                    out NativeArray<int> counters))
            {
                return false;
            }

            try
            {
                if (questStateCount >= 0)
                    counters[(int)QuestDagRuntimeConstants.CounterSlot.QuestStateCount] = questStateCount;
                if (dependencyLinkCount >= 0)
                    counters[(int)QuestDagRuntimeConstants.CounterSlot.DependencyLinkCount] = dependencyLinkCount;
                if (zeigarnikInjected)
                    counters[(int)QuestDagRuntimeConstants.CounterSlot.ZeigarnikInjectedCount] =
                        counters[(int)QuestDagRuntimeConstants.CounterSlot.ZeigarnikInjectedCount] + 1;
                if (zeigarnikFailClosed)
                    counters[(int)QuestDagRuntimeConstants.CounterSlot.ZeigarnikFailClosedCount] =
                        counters[(int)QuestDagRuntimeConstants.CounterSlot.ZeigarnikFailClosedCount] + 1;

                counters[(int)QuestDagRuntimeConstants.CounterSlot.LastLoadSourceHash] =
                    unchecked((int)QuestDagRuntimeConstants.SignalSourceHash);
                return true;
            }
            finally
            {
                vault.ReleaseWriteLock(in _questDagHandles.Counters, SystemID.QuestDag);
            }
        }

        private static void PublishZeigarnikHaptic()
        {
            HapticPulseSignal pulse = new HapticPulseSignal
            {
                LowFrequencyMotor01 = ZeigarnikHapticLow01,
                HighFrequencyMotor01 = ZeigarnikHapticHigh01,
                DurationSeconds = ZeigarnikHapticSeconds,
                PriorityFlags = HapticPulseSignal.PackPriorityAndSourceHash(
                    HapticPulseSignal.PriorityTool,
                    QuestDagRuntimeConstants.SignalSourceHash)
            };
            SignalBus<HapticPulseSignal>.TryPushTracked(in pulse, ref s_x001QuestManagerZeigarnikHapticDropCount);
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
                    Hecton8.Core.H8Debug.LogWarning("[QuestManager] Unknown questId.");
#endif

                return false;
            }

            return true;
        }

        private void BuildLookup()
        {
            QuestData[] quests = allQuests ?? Array.Empty<QuestData>();
            _questHashLookup.Clear();
            _hasLookupAmbiguity = false;
            _questDagAuthoredDependencyLinkCount = CountAuthoredQuestDependencyLinks();
            EnsureQuestNotificationCacheCapacity(quests.Length);

            for (int i = 0; i < quests.Length; i++)
            {
                QuestData questData = quests[i];
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

            _questCompletedNotificationHashes[questIndex] = RegisterQuestNotification(ObjectiveCompletedPrefix.AsSpan(), questData);
            _questRestoredNotificationHashes[questIndex] = RegisterQuestNotification(ObjectiveRestoredPrefix.AsSpan(), questData);
            _questNewNotificationHashes[questIndex] = RegisterQuestNotification(ObjectiveNewPrefix.AsSpan(), questData);
        }

        private uint RegisterQuestNotification(ReadOnlySpan<char> prefix, QuestData questData)
        {
            int messageLength = 0;
            if (!TryAppendSpan(prefix, _questNotificationMessageBuffer, ref messageLength))
                return 0u;

            if (questData == null ||
                !questData.TryWriteDisplayTitleOrFallback(_localizationManager, _questNotificationTitleBuffer, out int titleLength) ||
                titleLength <= 0)
            {
                UnknownObjectiveLabel.AsSpan().CopyTo(_questNotificationTitleBuffer);
                titleLength = UnknownObjectiveLabel.Length;
            }

            ReadOnlySpan<char> titleSpan = _questNotificationTitleBuffer.AsSpan(
                0,
                math.min(titleLength, _questNotificationTitleBuffer.Length));
            if (!TryAppendSpan(titleSpan, _questNotificationMessageBuffer, ref messageLength))
                return 0u;

            return NotificationEvents.RegisterMessage(_questNotificationMessageBuffer.AsSpan(0, messageLength));
        }

        private static bool TryAppendSpan(ReadOnlySpan<char> source, char[] destination, ref int length)
        {
            if (destination == null || length < 0 || length > destination.Length)
                return false;

            int available = destination.Length - length;
            if (available <= 0)
                return source.Length == 0;

            int copyLength = math.min(source.Length, available);
            if (copyLength <= 0)
                return true;

            source.Slice(0, copyLength).CopyTo(destination.AsSpan(length, copyLength));
            length += copyLength;
            return copyLength == source.Length;
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
            HashSet<QuestData> seenQuests = new HashSet<QuestData>(); // COLD ALLOC: HashSet<QuestData> - editor-time duplicate guard - owner: QuestManager
            if (allQuests != null)
            {
                for (int i = 0; i < allQuests.Length; i++)
                {
                    QuestData existing = allQuests[i];
                    if (existing != null && seenQuests.Add(existing))
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

                if (seenQuests.Add(questData))
                    loadedQuests.Add(questData);
            }

            if (loadedQuests.Count <= 0 || loadedQuests.Count == previousCount)
                return;

            if (allQuests == null || allQuests.Length != loadedQuests.Count)
                allQuests = new QuestData[loadedQuests.Count];

            loadedQuests.CopyTo(allQuests);
            EditorUtility.SetDirty(this);
        }
#endif
    }
}
