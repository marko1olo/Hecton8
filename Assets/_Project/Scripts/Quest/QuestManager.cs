using System;
using System.Collections.Generic;
using Hecton.Localization;
using Hecton8.Core;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Memory;
using Hecton8.Interaction;
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
    /// <summary>
    /// One recorded quest spine state change. Written on the transition itself, never on a cadence.
    /// </summary>
    public struct QuestSpineTransitionRecord
    {
        public uint QuestHash;
        public int FrameIndex;
        public float TimeSeconds;
        public byte TransitionCode;
        public byte Completed;
    }

    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-130)]
    public sealed class QuestManager : MonoBehaviour, ISaveable, IQuestSystem, ILateFrameTickable, IGlobalRegistryHotSwapListener
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
        private static readonly uint _QuestNotificationMissWarningHash = unchecked((uint)LocHash.Compute("QuestManager.NotificationMiss"));
        private static readonly uint _QuestNotificationContextHash = unchecked((uint)LocHash.Compute("QuestManager.Notification"));

        // The mission spine had no observable surface at all: every transition went out as a typed
        // QuestEvents payload plus a NotificationEvents push, and nothing in the project printed or
        // counted either one. A probe run could not tell "no quest ever activated" apart from "two
        // quests activated on frame one and nobody looked", so the whole axis read as dead.
        // Counters and the ring below are always on and allocation-free on the transition path; the
        // log lines are development-only and hard-capped, so neither can turn into cadence spam.
        private const int QuestSpineTransitionRingCapacity = 16;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private const int QuestSpineTransitionLogCap = 32;
        private const int QuestSpineLogBufferCapacity = 160;
        private const string QuestSpineLogPrefix = "H8QUESTSPINE ";
        private const string QuestSpineActivateLabel = "ACTIVATE";
        private const string QuestSpineCompleteLabel = "COMPLETE";
        private const string QuestSpineRevertLabel = "REVERT";
        private const string QuestSpineQuestToken = " quest=0x";
        private const string QuestSpineTotalToken = " total=";
        private const string QuestSpineBootLabel = "BOOT authored=";
        private const string QuestSpineAutoToken = " autoActivated=";
        private const string QuestSpineGraphToken = " graphReady=";
        private const string QuestSpineRegisteredToken = " registered=";
        private const string QuestSpineLoadedToken = " loadedFromSave=";
        private const string QuestSpineTrueLabel = "1";
        private const string QuestSpineFalseLabel = "0";
        // COLD ALLOC: char[160] - development-only quest spine log line staging, reused across all capped lines - owner: QuestManager
        private static readonly char[] s_questSpineLogBuffer = new char[QuestSpineLogBufferCapacity];
        private static int s_questSpineTransitionLogCount;
#endif

        private static uint[] s_stagedLoadedPackedState;
        private static QuestSaveHeader s_stagedLoadedQuestHeader;
        private static int s_x001QuestManagerZeigarnikHapticDropCount;

        // COLD ALLOC: QuestSpineTransitionRecord[16] - newest-last quest spine transition ring read back by the headless probe - owner: QuestManager
        private static readonly QuestSpineTransitionRecord[] s_questSpineTransitionRing =
            new QuestSpineTransitionRecord[QuestSpineTransitionRingCapacity];
        private static int s_questSpineTransitionRingWriteIndex;
        private static int s_questSpineTransitionCount;
        private static int s_questSpineActivationCount;
        private static int s_questSpineCompletionCount;
        private static int s_questSpineRevertCount;
        private static int s_questSpineAutoActivationCount;
        private static int s_questSpineAuthoredQuestCount;
        private static bool s_questSpineBootObserved;
        private static bool s_questSpineStateGraphReady;

        internal static QuestManager ActiveRuntimeInstance { get; private set; }

        // COLD ALLOC: Dictionary<uint,QuestData>[64] - authored quest lookup by stable FNV quest hash - owner: QuestManager
        private readonly Dictionary<uint, QuestData> _questHashLookup = new Dictionary<uint, QuestData>(64);

        private QuestStateManager _stateManager;
        private QuestGraphEvaluator _graphEvaluator;
        private bool _loadedFromSave;
        private bool _hasLookupAmbiguity;
        private bool _serviceRegistered;
        private bool _runtimeOwnerAborted;
        private ISaveService _registeredSaveService;
        private ILocalizationTextReadModel _localizationManager;
        private IDataVault _questDagVault;
        private QuestDagBufferHandles _questDagHandles;
        private int _questDagAuthoredDependencyLinkCount;
        private bool _hotSwapRegistered;
        private bool _lateFrameRegistered;
        private int _lastItemAcquiredSnapshotGeneration = -1;
        private int _itemAcquiredIngestCount;
        private uint[] _questCompletedNotificationHashes = Array.Empty<uint>();
        private uint[] _questRestoredNotificationHashes = Array.Empty<uint>();
        private uint[] _questNewNotificationHashes = Array.Empty<uint>();
        private readonly char[] _questNotificationMessageBuffer = new char[QuestNotificationMessageCapacity];
        private readonly char[] _questNotificationTitleBuffer = new char[QuestNotificationTitleCapacity];
        private int _questNotificationMissCount;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            s_stagedLoadedPackedState = null;
            s_stagedLoadedQuestHeader = default;
            ActiveRuntimeInstance = null;
            ResetQuestSpineDiagnostics();
        }

        private static void ResetQuestSpineDiagnostics()
        {
            for (int i = 0; i < s_questSpineTransitionRing.Length; i++)
                s_questSpineTransitionRing[i] = default;

            s_questSpineTransitionRingWriteIndex = 0;
            s_questSpineTransitionCount = 0;
            s_questSpineActivationCount = 0;
            s_questSpineCompletionCount = 0;
            s_questSpineRevertCount = 0;
            s_questSpineAutoActivationCount = 0;
            s_questSpineAuthoredQuestCount = 0;
            s_questSpineBootObserved = false;
            s_questSpineStateGraphReady = false;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            s_questSpineTransitionLogCount = 0;
#endif
        }

        /// <summary>
        /// True once a quest runtime owner reached Start and reported its authored registry size.
        /// </summary>
        public static bool QuestSpineBootObserved => s_questSpineBootObserved;

        /// <summary>
        /// Authored quest asset count the runtime owner actually carried into Start.
        /// </summary>
        public static int QuestSpineAuthoredQuestCount => s_questSpineAuthoredQuestCount;

        /// <summary>
        /// Quests the auto-activation pass turned on during Start.
        /// </summary>
        public static int QuestSpineAutoActivationCount => s_questSpineAutoActivationCount;

        /// <summary>
        /// Total quest activation transitions emitted this session.
        /// </summary>
        public static int QuestSpineActivationCount => s_questSpineActivationCount;

        /// <summary>
        /// Total quest completion transitions emitted this session.
        /// </summary>
        public static int QuestSpineCompletionCount => s_questSpineCompletionCount;

        /// <summary>
        /// Total quest revert transitions emitted this session.
        /// </summary>
        public static int QuestSpineRevertCount => s_questSpineRevertCount;

        /// <summary>
        /// Total quest transitions of every kind emitted this session.
        /// </summary>
        public static int QuestSpineTransitionCount => s_questSpineTransitionCount;

        /// <summary>
        /// True when a runtime owner compiled its quest state graph without errors.
        /// </summary>
        public static bool QuestSpineStateGraphReady => s_questSpineStateGraphReady;

        /// <summary>
        /// Copies the most recent quest spine transitions oldest-first into the caller's buffer.
        /// </summary>
        /// <param name="destination">Caller-owned buffer. Nothing is copied when it is null or empty.</param>
        /// <returns>Number of records written.</returns>
        public static int CopyQuestSpineTransitions(QuestSpineTransitionRecord[] destination)
        {
            if (destination == null || destination.Length <= 0)
                return 0;

            int recorded = s_questSpineTransitionCount < QuestSpineTransitionRingCapacity
                ? s_questSpineTransitionCount
                : QuestSpineTransitionRingCapacity;
            int copyCount = recorded < destination.Length ? recorded : destination.Length;
            if (copyCount <= 0)
                return 0;

            int oldestIndex = s_questSpineTransitionRingWriteIndex - copyCount;
            while (oldestIndex < 0)
                oldestIndex += QuestSpineTransitionRingCapacity;

            for (int i = 0; i < copyCount; i++)
            {
                int ringIndex = oldestIndex + i;
                if (ringIndex >= QuestSpineTransitionRingCapacity)
                    ringIndex -= QuestSpineTransitionRingCapacity;

                destination[i] = s_questSpineTransitionRing[ringIndex];
            }

            return copyCount;
        }

        public int SavePriority => 7;

        public int LoadPriority => 7;

        internal int PackedStateWordCount => _stateManager != null ? _stateManager.WordCount : 0;

        public int QuestNotificationMissCount => _questNotificationMissCount;

        /// <summary>
        /// True once the quest runtime owner is registered in the global registry.
        /// </summary>
        public bool IsInitialized => !_runtimeOwnerAborted && _serviceRegistered && ReferenceEquals(ActiveRuntimeInstance, this);

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
                AbortDuplicateRuntimeOwner();
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
            TryRegisterLateFrameTick();
        }

        private void OnDisable()
        {
            if (ReferenceEquals(ActiveRuntimeInstance, this))
                ActiveRuntimeInstance = null;

            TryUnregisterLateFrameTick();
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

            ClearQuestNotificationDiagnostics();
        }

        private void OnDestroy()
        {
            if (ReferenceEquals(ActiveRuntimeInstance, this))
                ActiveRuntimeInstance = null;

            TryUnregisterLateFrameTick();
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

            ClearQuestNotificationDiagnostics();
        }

        private void Start()
        {
            if (!_runtimeOwnerAborted)
                BindSaveService(GlobalRegistry.Save);

            // Retried here on purpose. This owner carries [DefaultExecutionOrder(-130)], so its OnEnable
            // can run before GlobalRegistry.Dispatcher exists, and the guard in TryRegisterLateFrameTick
            // would then leave the item-acquisition bridge permanently unregistered with no second
            // attempt. Start runs after every Awake and OnEnable in the load, which is the same ordering
            // FirstHourDirector.cs:1071-1084 relies on. Both calls are idempotent.
            TryRegisterLateFrameTick();

            if (_runtimeOwnerAborted || _loadedFromSave || _stateManager == null)
            {
                PublishQuestSpineBootFacts(autoActivatedCount: 0);
                return;
            }

            _stateManager.ApplyAutoActivationFlags(allQuests);
            int autoActivatedCount = _stateManager.ResultCount;
            PublishQuestSpineBootFacts(autoActivatedCount);
            FlushRuntimeResults();
        }

        /// <summary>
        /// Records the boot facts a probe needs to separate "no quest data" from "quest data that never fired".
        /// </summary>
        /// <param name="autoActivatedCount">Quests the auto-activation pass turned on.</param>
        private void PublishQuestSpineBootFacts(int autoActivatedCount)
        {
            s_questSpineBootObserved = true;
            s_questSpineAuthoredQuestCount = allQuests != null ? allQuests.Length : 0;
            s_questSpineAutoActivationCount = autoActivatedCount;
            s_questSpineStateGraphReady = _stateManager != null && !_stateManager.HasCompileErrors;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            LogQuestSpineBootFacts(autoActivatedCount);
#endif
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
            if (!IsSaveServiceUsable(saveService))
                saveService = null;

            if (ReferenceEquals(_registeredSaveService, saveService))
                return;

            _registeredSaveService?.Unregister(this);
            _registeredSaveService = null;

            if (!IsSaveServiceUsable(saveService))
                return;

            _registeredSaveService = saveService;
            _registeredSaveService.Register(this);
        }

        private static bool IsSaveServiceUsable(ISaveService saveService)
        {
            return saveService != null && saveService.IsInitialized;
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

        private void AbortDuplicateRuntimeOwner()
        {
            if (_runtimeOwnerAborted)
                return;

            if (ReferenceEquals(ActiveRuntimeInstance, this))
                ActiveRuntimeInstance = null;

            TryUnregisterLateFrameTick();
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

            ClearQuestNotificationDiagnostics();
            _runtimeOwnerAborted = true;
            enabled = false;
        }

        private void TryRegisterLateFrameTick()
        {
            if (_runtimeOwnerAborted || _lateFrameRegistered)
                return;
            if (!Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            _lateFrameRegistered = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Player);
        }

        private void TryUnregisterLateFrameTick()
        {
            if (!_lateFrameRegistered)
                return;

            GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Player);
            _lateFrameRegistered = false;
        }

        /// <inheritdoc />
        public void LateFrameTick()
        {
            if (_runtimeOwnerAborted)
                return;

            DrainItemAcquiredSignalsIntoQuestGraph();
        }

        /// <summary>
        /// Feeds the live item-acquisition lane into the quest graph's ItemCollected input.
        /// </summary>
        /// <remarks>
        /// QuestGraphEvaluator's only item input is IInteractionEventListener.OnInteractionEvent, and the
        /// only producer of InteractionEventType.ItemCollected in the whole project is
        /// HarvestableOutcrop.cs:487 - whose script GUID (d7edd8c67a6e0b242b34c32fe9ddb1fd) is authored
        /// into zero scenes and zero prefabs. Every actual acquisition route publishes
        /// SignalBus&lt;ItemAcquiredSignal&gt; instead: PickupItem.cs:632, HectonItem.cs:501,
        /// Fabricator.cs:3483, DeployableSdfDrillRuntime.cs:1301, VoxelDeltaProcessor.cs:5845,
        /// LootMagnetSystem.cs:2043, ProceduralOreSpawner.cs:2801 and the drone lane. So every quest with
        /// completionType 0 (OnItemCollected) - Quest_FirstHour_CollectTitanium, Quest_CopperSample,
        /// Quest_RadShield - could never complete from real gameplay, and every OnItemCollected trigger
        /// (Quest_FirstHour_CraftScanner) could never fire. FirstHourDirector.cs:1804-1838 already had to
        /// work around exactly this for its own goals; the quest graph itself never got the bridge.
        ///
        /// The hash kernels are identical on both sides, so the feed is a real match and not a
        /// plausible-looking one: PickupItem.cs:293 and HectonItem.cs:421 fill ItemHash from
        /// LocHash.Compute(ItemData.PersistentId), InteractionEvents.ComputeItemHash does the same via
        /// ItemData.ResolvePersistentHashId (ItemData.cs:396-400), and QuestStateManager's
        /// ComputeSignalIdHash (:2058-2063) hashes the authored completionId with that same LocHash.
        ///
        /// Double-counting is impossible even if an item ever hits both lanes: TryActivateQuest and
        /// TryCompleteQuest are bit-guarded (QuestStateManager.cs:832 and :853), so a repeat is a no-op.
        /// The snapshot generation guard keeps one frame's signals from being ingested twice.
        /// </remarks>
        private void DrainItemAcquiredSignalsIntoQuestGraph()
        {
            QuestGraphEvaluator graphEvaluator = _graphEvaluator;
            if (graphEvaluator == null)
                return;

            int snapshotGeneration = SignalBus<ItemAcquiredSignal>.SnapshotGeneration;
            if (_lastItemAcquiredSnapshotGeneration == snapshotGeneration)
                return;

            _lastItemAcquiredSnapshotGeneration = snapshotGeneration;

            ReadOnlySpan<ItemAcquiredSignal> signals = SignalBus<ItemAcquiredSignal>.GetFrameSnapshot();
            for (int i = 0; i < signals.Length; i++)
            {
                uint itemHash = signals[i].ItemHash;
                if (itemHash == 0u)
                    continue;

                int quantity = signals[i].Quantity;
                InteractionEventPayload payload = default;
                payload.ItemHashId = itemHash;
                payload.Quantity = quantity > 0 ? quantity : 1;
                payload.EventType = (ushort)InteractionEventType.ItemCollected;
                payload.ReferenceSlot = -1;

                graphEvaluator.OnInteractionEvent(in payload);
                _itemAcquiredIngestCount++;
            }
        }

        /// <summary>
        /// Item-acquisition signals this owner has forwarded into the quest graph this session.
        /// </summary>
        public int ItemAcquiredIngestCount => _itemAcquiredIngestCount;

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
            ClearQuestNotificationDiagnostics();
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
            RecordQuestSpineTransition(questHash, completed, transitionType);

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

            TryPushQuestNotification(notificationHash);
        }

        private void TryPushQuestNotification(uint notificationHash)
        {
            if (NotificationEvents.TryPushRegisteredInfo(notificationHash))
                return;

            ReportQuestNotificationMiss(notificationHash);
        }

        private void ReportQuestNotificationMiss(uint notificationHash)
        {
            _questNotificationMissCount++;
            GlobalTelemetryBus.PublishPerformanceWarning(
                _QuestNotificationMissWarningHash,
                _QuestNotificationContextHash ^ notificationHash,
                math.max(1, _questNotificationMissCount));
        }

        private void ClearQuestNotificationDiagnostics()
        {
            _questNotificationMissCount = 0;
        }

        /// <summary>
        /// Counts one quest spine transition and stores it in the read-back ring. Allocation-free.
        /// </summary>
        /// <param name="questHash">Stable quest hash that changed state.</param>
        /// <param name="completed">True when the quest is completed after the transition.</param>
        /// <param name="transitionType">Transition class emitted by the state graph.</param>
        private static void RecordQuestSpineTransition(uint questHash, bool completed, QuestTransitionType transitionType)
        {
            switch (transitionType)
            {
                case QuestTransitionType.Complete:
                    s_questSpineCompletionCount++;
                    break;
                case QuestTransitionType.Revert:
                    s_questSpineRevertCount++;
                    break;
                default:
                    if (completed)
                        s_questSpineCompletionCount++;
                    else
                        s_questSpineActivationCount++;
                    break;
            }

            int writeIndex = s_questSpineTransitionRingWriteIndex;
            s_questSpineTransitionRing[writeIndex] = new QuestSpineTransitionRecord
            {
                QuestHash = questHash,
                FrameIndex = SystemDispatcher.CurrentFrameIndex,
                TimeSeconds = (float)SystemDispatcher.CurrentUnscaledTimeSeconds,
                TransitionCode = (byte)transitionType,
                Completed = completed ? (byte)1 : (byte)0
            };

            writeIndex++;
            s_questSpineTransitionRingWriteIndex = writeIndex >= QuestSpineTransitionRingCapacity ? 0 : writeIndex;
            s_questSpineTransitionCount++;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            LogQuestSpineTransition(questHash, completed, transitionType);
#endif
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        /// <summary>
        /// Emits one development-only quest spine transition line. Hard-capped so it can never become cadence spam.
        /// </summary>
        /// <remarks>Buffer, cap and label constants live in the matching conditional block near the class head.</remarks>
        /// <param name="questHash">Stable quest hash that changed state.</param>
        /// <param name="completed">True when the quest is completed after the transition.</param>
        /// <param name="transitionType">Transition class emitted by the state graph.</param>
        private static void LogQuestSpineTransition(uint questHash, bool completed, QuestTransitionType transitionType)
        {
            if (s_questSpineTransitionLogCount >= QuestSpineTransitionLogCap)
                return;

            s_questSpineTransitionLogCount++;

            int length = 0;
            if (!TryAppendSpan(QuestSpineLogPrefix.AsSpan(), s_questSpineLogBuffer, ref length))
                return;

            ReadOnlySpan<char> label = ResolveQuestSpineTransitionLabel(completed, transitionType);
            if (!TryAppendSpan(label, s_questSpineLogBuffer, ref length) ||
                !TryAppendSpan(QuestSpineQuestToken.AsSpan(), s_questSpineLogBuffer, ref length) ||
                !TryAppendHex32(questHash, s_questSpineLogBuffer, ref length) ||
                !TryAppendSpan(QuestSpineTotalToken.AsSpan(), s_questSpineLogBuffer, ref length) ||
                !TryAppendInt32(s_questSpineTransitionCount, s_questSpineLogBuffer, ref length))
            {
                return;
            }

            // COLD ALLOC: string[1] - one development-only spine line, capped at 32 per session - owner: QuestManager
            Hecton8.Core.H8Debug.Log(new string(s_questSpineLogBuffer, 0, length));
        }

        /// <summary>
        /// Emits the development-only quest spine boot line that separates empty quest data from inert quest data.
        /// </summary>
        /// <param name="autoActivatedCount">Quests the auto-activation pass turned on.</param>
        private void LogQuestSpineBootFacts(int autoActivatedCount)
        {
            int length = 0;
            if (!TryAppendSpan(QuestSpineLogPrefix.AsSpan(), s_questSpineLogBuffer, ref length) ||
                !TryAppendSpan(QuestSpineBootLabel.AsSpan(), s_questSpineLogBuffer, ref length) ||
                !TryAppendInt32(s_questSpineAuthoredQuestCount, s_questSpineLogBuffer, ref length) ||
                !TryAppendSpan(QuestSpineAutoToken.AsSpan(), s_questSpineLogBuffer, ref length) ||
                !TryAppendInt32(autoActivatedCount, s_questSpineLogBuffer, ref length) ||
                !TryAppendSpan(QuestSpineGraphToken.AsSpan(), s_questSpineLogBuffer, ref length) ||
                !TryAppendSpan(ResolveQuestSpineFlagLabel(s_questSpineStateGraphReady), s_questSpineLogBuffer, ref length) ||
                !TryAppendSpan(QuestSpineRegisteredToken.AsSpan(), s_questSpineLogBuffer, ref length) ||
                !TryAppendSpan(ResolveQuestSpineFlagLabel(_serviceRegistered), s_questSpineLogBuffer, ref length) ||
                !TryAppendSpan(QuestSpineLoadedToken.AsSpan(), s_questSpineLogBuffer, ref length) ||
                !TryAppendSpan(ResolveQuestSpineFlagLabel(_loadedFromSave), s_questSpineLogBuffer, ref length))
            {
                return;
            }

            // COLD ALLOC: string[1] - one development-only spine boot line per runtime owner Start - owner: QuestManager
            Hecton8.Core.H8Debug.Log(new string(s_questSpineLogBuffer, 0, length), this);
        }

        private static ReadOnlySpan<char> ResolveQuestSpineTransitionLabel(bool completed, QuestTransitionType transitionType)
        {
            switch (transitionType)
            {
                case QuestTransitionType.Complete:
                    return QuestSpineCompleteLabel.AsSpan();
                case QuestTransitionType.Revert:
                    return QuestSpineRevertLabel.AsSpan();
                default:
                    return completed ? QuestSpineCompleteLabel.AsSpan() : QuestSpineActivateLabel.AsSpan();
            }
        }

        private static ReadOnlySpan<char> ResolveQuestSpineFlagLabel(bool value)
        {
            return value ? QuestSpineTrueLabel.AsSpan() : QuestSpineFalseLabel.AsSpan();
        }

        private static bool TryAppendHex32(uint value, char[] destination, ref int length)
        {
            if (destination == null || length < 0 || destination.Length - length < 8)
                return false;

            // Nibble is kept as int on purpose. `int + uint` promotes to long in C#, which is the same
            // promotion trap CONTRIBUTING.md calls out for `someIntConst - 1u`.
            for (int shift = 28; shift >= 0; shift -= 4)
            {
                int nibble = (int)((value >> shift) & 0xFu);
                destination[length++] = (char)(nibble < 10 ? '0' + nibble : ('A' - 10) + nibble);
            }

            return true;
        }

        private static bool TryAppendInt32(int value, char[] destination, ref int length)
        {
            if (destination == null || length < 0 || length >= destination.Length)
                return false;

            if (value < 0)
            {
                destination[length++] = '-';
                if (length >= destination.Length)
                    return false;
            }

            long magnitude = value < 0 ? -(long)value : value;
            int digitStart = length;
            do
            {
                if (length >= destination.Length)
                    return false;

                destination[length++] = (char)('0' + (int)(magnitude % 10L));
                magnitude /= 10L;
            }
            while (magnitude != 0L);

            for (int low = digitStart, high = length - 1; low < high; low++, high--)
            {
                char swap = destination[low];
                destination[low] = destination[high];
                destination[high] = swap;
            }

            return true;
        }
#endif

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
