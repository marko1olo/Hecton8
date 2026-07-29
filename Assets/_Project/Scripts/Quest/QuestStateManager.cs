using System;
using System.Collections.Generic;
using Conditional = System.Diagnostics.ConditionalAttribute;
using Hecton.Localization;
using Hecton8.Core;
using Hecton8.Core.Memory;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Quest
{
    internal sealed class QuestStateManager : IDisposable
    {
        private const int ProceduralQuestCapacity = 8;
        private const int QuestTitleCharCapacity = 128;
        private const int QuestDescriptionCharCapacity = 512;
        private const int WordCapacity = QuestRuntimeLayout.WordCapacity;
        private const int WordStride = QuestRuntimeLayout.WordStrideBits;
        private const int QuestWordStart = QuestRuntimeLayout.QuestWordStart;
        private const int QuestWordCount = QuestRuntimeLayout.QuestWordCount;
        private const int ItemWordStart = QuestRuntimeLayout.ItemWordStart;
        private const int ItemWordCount = QuestRuntimeLayout.ItemWordCount;
        private const int LocationWordStart = QuestRuntimeLayout.LocationWordStart;
        private const int LocationWordCount = QuestRuntimeLayout.LocationWordCount;
        private const int NarrativeWordStart = QuestRuntimeLayout.NarrativeWordStart;
        private const int NarrativeWordCount = QuestRuntimeLayout.NarrativeWordCount;
        private const int PhaseWordStart = QuestRuntimeLayout.PhaseWordStart;
        private const int PhaseWordCount = QuestRuntimeLayout.PhaseWordCount;
        private const int EntityDestroyWordStart = QuestRuntimeLayout.EntityDestroyWordStart;
        private const int EntityDestroyWordCount = QuestRuntimeLayout.EntityDestroyWordCount;
        private const int DeadlockWordStart = QuestRuntimeLayout.DeadlockWordStart;
        private const int DeadlockWordCount = QuestRuntimeLayout.DeadlockWordCount;
        private const uint ActiveFlagSalt = 0xA11F0A11u;
        private const uint CompletedFlagSalt = 0xC0DE0C01u;
        private const uint BiomeFlagSalt = 0xB10F0001u;
        private const uint DepthFlagSalt = 0xD37A0001u;
        private const uint EclipseFlagHash = 0xE011C1E5u;
        private const uint EntityDestroyFlagSalt = 0xD357F1A6u;
        private const uint DeadlockFlagSalt = 0xDEAD10CCu;
        private const int QuestTransitionAuditCapacity = QuestDagRuntimeConstants.TelemetryCapacity;
        private const string NativeMemoryOwner = nameof(QuestStateManager);
        private const string QuestGateLogPrefix = "H8QUESTGATE ";

        // 256 is a proven bound, not a guess. The longest line the gate diagnosis can emit is the census row:
        // prefix 12 + stage tag 5 + fixed tokens 60 + two hex8 fields 16 + four ints at their int.MinValue
        // width 44 + one signal-kind label 14 (closed switch over a byte enum, longest is "EclipseStarted")
        // + one float at its widest TryFormat output 32 = 183. Every unchecked writer below - CopyString,
        // WriteInt, WriteHex8 - therefore cannot run off the end, and WriteFloat is already self-limiting
        // because TryFormat fails into a one-char fallback when the remaining span is too small.
        private const int QuestGateLogBufferCapacity = 256;
        private const int QuestGateSignalLogCap = 24;

        // Session, not Scene, and the DECLARATION was the defect - the buffers were never leaked.
        // These two lists are allocated from Initialize, which QuestManager reaches from Awake, and
        // QuestManager lives in 02_HECTON_WORLD (guid 118b59a08371522459b4d4f62de86712 is present in
        // 02_HECTON_WORLD.unity and 010_TEST.unity and in no other live scene or prefab; it is not in
        // 01_MAIN_MENU). HECTON-8 loads 02_HECTON_WORLD ADDITIVELY over the menu, so at that Awake
        // SceneManager.GetActiveScene() is still 01_MAIN_MENU. The collection registrars take no explicit
        // Scene - only the private RegisterPointer overload does - so a Scene declaration left
        // NativeMemorySentinel to infer the owner scene through ResolveCurrentSceneIdentity
        // (NativeMemorySentinel.cs:2179) and it stamped both records sceneBuildIndex=1
        // sceneScope=active-scene-at-alloc. UnloadSceneAsync(01_MAIN_MENU) then found them alive and raised
        // two CRITICAL_MEMORY_VIOLATION scene-leak errors plus a FatalMemoryLeakException -
        // Logs/h8_worldsim_probe5.log:8427, :8442 and :8577 (context=01_MAIN_MENU active=10) - against
        // buffers belonging to a scene that had not unloaded. Nothing in Dispose was broken: QuestManager
        // OnDestroy calls Dispose (QuestManager.cs:305-309), Initialize calls it before reallocating
        // (:178), and ReleaseNativeList unregisters the sentinel id before disposing the list.
        // Session is the honest declaration because this owner provably outlives the scene that was active
        // when it allocated, and it is the sibling declaration: QuestGraphEvaluator (:71),
        // QuestDagResolverRuntime (:608) and QuestEvents (:349) - constructed and disposed by the same
        // QuestManager instance, on the same OnDestroy - already declare Session, and this class's own four
        // NativeArrays go through H8Memory.Allocate with Allocator.Persistent, which H8Memory classifies as
        // Session (H8Memory.cs:5878-5879). This was the only Scene declaration left in the quest domain,
        // which is why only these two of its six native buffers were ever reported.
        // Tracking is not weakened: Session still counts as persistent (NativeMemorySentinel.cs:2626-2631),
        // both ids are still unregistered in Dispose, and a genuine leak by this owner is still fatal at
        // AssertNoAllocationsAfterServiceShutdown (NativeMemorySentinel.cs:1749), which
        // GlobalRegistry.ResetStaticState calls (GlobalRegistry.cs:2883) over every tracked record
        // regardless of lifetime.
        private const NativeAllocationLifetime NativeMemoryLifetime = NativeAllocationLifetime.Session;
        private const Allocator DataVaultExemptQuestStateAllocator = Allocator.Persistent;
        private const SystemID NativeArrayOwnerSystem = SystemID.QuestDag;
        private static readonly uint _abyssalPhaseFlagHash = QuestFlagHashKernel.ComputeStableHash("phase.abyssal");
        private static readonly uint _thermalPhaseFlagHash = QuestFlagHashKernel.ComputeStableHash("phase.thermal");

        // COLD ALLOC: List<QuestRuntimeResult>[32] - transition handoff from packed runtime to facade - owner: QuestStateManager
        private readonly List<QuestRuntimeResult> _runtimeResults = new List<QuestRuntimeResult>(32);
        private readonly QuestTransitionAuditEntry[] _transitionAuditRing = new QuestTransitionAuditEntry[QuestTransitionAuditCapacity]; // COLD ALLOC: QuestTransitionAuditEntry[300] - fixed dev transition ring, no file I/O in signal drain - owner: QuestStateManager

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // COLD ALLOC: char[QuestGateLogBufferCapacity] - development-only quest gate diagnosis line staging - owner: QuestStateManager
        // Instance-scoped, not static: the cap counter beside it has to reset when a fresh QuestStateManager
        // is built, and a static counter would silence the diagnosis on every play session after the first
        // inside one editor process.
        private readonly char[] _questGateLogBuffer = new char[QuestGateLogBufferCapacity];
        private int _questGateSignalLogCount;
#endif

        private NativeArray<uint> _globalPrerequisites;
        private NativeArray<uint> _validPackedWordMasks;
        private NativeArray<QuestNodeDescriptor> _nodes;
        private NativeArray<QuestPrerequisiteDescriptor> _prerequisites;
        private NativeList<int> _activatedQuestIndices;
        private NativeList<int> _completedQuestIndices;
        private int _activatedQuestIndicesSentinelId;
        private int _completedQuestIndicesSentinelId;
        private Dictionary<uint, QuestBitAddress> _bitAddressByHash;
        private Dictionary<uint, int> _questIndexByHash;
        private Dictionary<uint, int> _revertDescriptorIndexByItemHash;
        private QuestBitAddress[] _activeAddressesByQuestIndex;
        private QuestBitAddress[] _completedAddressesByQuestIndex;
        private uint[] _phaseGateMasksByQuestIndex;
        private uint[] _questHashesByQuestIndex;
        private uint[] _markerTargetHashesByQuestIndex;
        private Vector3[] _markerWorldPositionsByQuestIndex;
        private float[] _markerHeightOffsetsByQuestIndex;
        private char[][] _questTitleBuffersByQuestIndex;
        private char[][] _questDescriptionBuffersByQuestIndex;
        private int[] _questTitleLengthsByQuestIndex;
        private int[] _questDescriptionLengthsByQuestIndex;
        private int[] _proceduralNodeIndexByQuestIndex;
        private QuestRevertDescriptor[] _revertDescriptors;
        private QuestBitAddress _abyssalPhaseAddress;
        private QuestBitAddress _thermalPhaseAddress;
        private ThresholdFlag[] _depthThresholdFlags;
        private int _authoredQuestCount;
        private string _compileErrorSummary = string.Empty;
        private int _compileErrorCount;
        private uint _stateVersion;
        private uint _stateChecksum;
        private int _transitionAuditWriteIndex;
        private int _transitionAuditCount;
        private bool _isInitialized;
        private ILocalizationTextReadModel _localizationManager;

        // Diagnosis counters for the Mission row. Before these existed the only observable quest fact was
        // "completions=0", which cannot distinguish the three states that produce it: no signal ever reached
        // the graph, a signal reached it and matched no node, or a node matched and the transition was
        // suppressed. _signalsEvaluated separates the first from the other two, _signalsMatchedNothing
        // separates the second from the third, and the two transition counts are the graph's own output
        // before QuestManager's telemetry sees it - so a disagreement between _graphCompletions here and
        // QuestManager.QuestSpineCompletionCount localises the break to the flush side rather than the graph.
        // Plain int increments on the signal path, no allocation, and they ship in release because they are
        // the numeric evidence, not the log line.
        private int _signalsEvaluated;
        private int _signalsMatchedNothing;
        private int _graphActivations;
        private int _graphCompletions;

        /// <summary>Signals handed to <see cref="EvaluateSignal"/> since the last initialize. Zero means the graph was never fed.</summary>
        internal int SignalsEvaluated => _signalsEvaluated;

        /// <summary>Signals that matched no node at all. Equal to <see cref="SignalsEvaluated"/> means every signal was irrelevant to the authored graph.</summary>
        internal int SignalsMatchedNothing => _signalsMatchedNothing;

        /// <summary>Activations the packed graph produced from signals, excluding the auto-activation pass.</summary>
        internal int GraphActivations => _graphActivations;

        /// <summary>Completions the packed graph produced. A nonzero value here with completions=0 upstream is a flush defect, not a graph defect.</summary>
        internal int GraphCompletions => _graphCompletions;

        private struct QuestTransitionAuditEntry
        {
            public double Timestamp;
            public uint QuestHash;
            public uint EntityHash;
            public uint ItemId;
            public float NumericValue;
            public ushort SignalEventType;
            public byte TransitionType;
            public byte Completed;
        }

        public bool HasCompileErrors => _compileErrorCount > 0;

        public string CompileErrorSummary => _compileErrorSummary;

        public int CompileErrorCount => _compileErrorCount;

        public int WordCount => WordCapacity;

        public int ResultCount => _runtimeResults.Count;

        public uint StateVersion => _stateVersion;

        public uint StateChecksum => _stateChecksum;

        private bool HasLiveNativeState =>
            _activatedQuestIndices.IsCreated ||
            _completedQuestIndices.IsCreated ||
            _nodes.IsCreated ||
            _validPackedWordMasks.IsCreated ||
            _prerequisites.IsCreated ||
            _globalPrerequisites.IsCreated;

        public void Dispose()
        {
            // Every release below runs even when an earlier one fails. ReleaseNativeList rethrows whatever
            // NativeMemorySentinel.Unregister or NativeList.Dispose threw, and letting that escape from here
            // abandoned the four H8Memory-tracked NativeArrays underneath it - turning one failed unregister
            // into five surviving buffers, all of which then die together at
            // NativeMemorySentinel.AssertNoAllocationsAfterServiceShutdown with the wrong owner story.
            // The first failure is still rethrown after the sweep, so the existing contract is unchanged:
            // Initialize re-checks HasLiveNativeState, which reads the real IsCreated flags rather than the
            // exception, and refuses to overwrite state that survived a failed Dispose.
            //
            // Repeat Dispose is a no-op by construction: ReleaseNativeList only touches a sentinel id when it
            // is > 0 and only disposes a list when IsCreated, and it zeroes both on the way out, while every
            // H8Memory.Release below is IsCreated-guarded and nulls its handle. Nothing here logs on the
            // happy path.

            // Census emitted before the releases because it reads _globalPrerequisites and _nodes. Guarded
            // on _isInitialized inside, so the Dispose that Initialize calls on a cold state manager prints
            // nothing, and a repeat Dispose after a successful one prints nothing either.
            LogQuestGateCensus("WAIT");

            Exception firstReleaseException = null;

            try
            {
                ReleaseNativeList(ref _activatedQuestIndices, ref _activatedQuestIndicesSentinelId);
            }
            catch (Exception exception)
            {
                firstReleaseException = exception;
            }

            try
            {
                ReleaseNativeList(ref _completedQuestIndices, ref _completedQuestIndicesSentinelId);
            }
            catch (Exception exception)
            {
                if (firstReleaseException == null)
                    firstReleaseException = exception;
            }

            if (_nodes.IsCreated)
                H8Memory.Release(ref _nodes, NativeArrayOwnerSystem);

            if (_validPackedWordMasks.IsCreated)
                H8Memory.Release(ref _validPackedWordMasks, NativeArrayOwnerSystem);

            if (_prerequisites.IsCreated)
                H8Memory.Release(ref _prerequisites, NativeArrayOwnerSystem);

            if (_globalPrerequisites.IsCreated)
                H8Memory.Release(ref _globalPrerequisites, NativeArrayOwnerSystem);

            if (firstReleaseException != null)
            {
                // Cold path - quest teardown, never a tick - and silent unless a release actually failed.
                // H8Debug carries [Conditional("UNITY_EDITOR")] + [Conditional("DEVELOPMENT_BUILD")], so the
                // compiler deletes this call from a release player instead of a runtime flag skipping it.
                // Logged as well as rethrown because the rethrow alone is not diagnosable: any caller that
                // swallows Dispose would leave no record at all of which release refused, and the surviving
                // sentinel record then only surfaces much later as an anonymous shutdown-time leak count.
                Hecton8.Core.H8Debug.LogException(firstReleaseException);
                throw firstReleaseException;
            }

            if (HasLiveNativeState)
                return;

            _runtimeResults.Clear();
            _bitAddressByHash = null;
            _questIndexByHash = null;
            _revertDescriptorIndexByItemHash = null;
            _activeAddressesByQuestIndex = null;
            _completedAddressesByQuestIndex = null;
            _phaseGateMasksByQuestIndex = null;
            _questHashesByQuestIndex = null;
            _markerTargetHashesByQuestIndex = null;
            _markerWorldPositionsByQuestIndex = null;
            _markerHeightOffsetsByQuestIndex = null;
            _questTitleBuffersByQuestIndex = null;
            _questDescriptionBuffersByQuestIndex = null;
            _questTitleLengthsByQuestIndex = null;
            _questDescriptionLengthsByQuestIndex = null;
            _proceduralNodeIndexByQuestIndex = null;
            _revertDescriptors = null;
            _abyssalPhaseAddress = default;
            _thermalPhaseAddress = default;
            _depthThresholdFlags = null;
            _authoredQuestCount = 0;
            _compileErrorSummary = string.Empty;
            _compileErrorCount = 0;
            _stateVersion = 0u;
            _stateChecksum = 0u;
            _localizationManager = null;
            _isInitialized = false;
            _signalsEvaluated = 0;
            _signalsMatchedNothing = 0;
            _graphActivations = 0;
            _graphCompletions = 0;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _questGateSignalLogCount = 0;
#endif
        }

        public bool Initialize(QuestData[] allQuests, ILocalizationTextReadModel localizationManager)
        {
            // Caught, not propagated. Dispose rethrows a release failure, and an exception escaping here
            // escaped QuestManager.InitializeStateGraph out of Awake, skipping the branch that exists for
            // exactly this outcome - it logs and sets enabled = false (QuestManager.cs:912-919) - and leaving
            // a half-built owner registered instead. Returning false takes that branch. The
            // HasLiveNativeState guard below still stands on its own for the case where Dispose reported
            // nothing but left a buffer created.
            try
            {
                Dispose();
            }
            catch (Exception exception)
            {
                Hecton8.Core.H8Debug.LogException(exception);
                return false;
            }

            if (HasLiveNativeState)
                return false;

            _localizationManager = localizationManager;

            _authoredQuestCount = allQuests != null ? allQuests.Length : 0;
            long questArrayLengthLong = (long)_authoredQuestCount + ProceduralQuestCapacity;
            if (questArrayLengthLong > int.MaxValue)
            {
                Dispose();
                return false;
            }

            int questArrayLength = (int)questArrayLengthLong;
            if (!TryResolveQuestStateManagedCapacity(questArrayLength, 6, 16, out int bitAddressCapacity) ||
                !TryResolveQuestStateManagedCapacity(questArrayLength, 2, 8, out int nodeBuilderCapacity) ||
                !TryResolveQuestStateManagedCapacity(questArrayLength, 3, 8, out int prerequisiteBuilderCapacity) ||
                !TryResolveQuestStateManagedCapacity(questArrayLength, 6, 16, out int hashLabelCapacity))
            {
                Dispose();
                return false;
            }

            _globalPrerequisites = H8Memory.Allocate<uint>(WordCapacity, NativeArrayOwnerSystem, DataVaultExemptQuestStateAllocator, NativeArrayOptions.ClearMemory);
            if (!_globalPrerequisites.IsCreated)
            {
                Dispose();
                return false;
            }
            _validPackedWordMasks = H8Memory.Allocate<uint>(WordCapacity, NativeArrayOwnerSystem, DataVaultExemptQuestStateAllocator, NativeArrayOptions.ClearMemory);
            if (!_validPackedWordMasks.IsCreated)
            {
                Dispose();
                return false;
            }

            _bitAddressByHash = new Dictionary<uint, QuestBitAddress>(bitAddressCapacity); // COLD ALLOC: Dictionary<uint,QuestBitAddress>[questArrayLength*6] - compiled flag lookup - owner: QuestStateManager
            _questIndexByHash = new Dictionary<uint, int>(Math.Max(questArrayLength, 16)); // COLD ALLOC: Dictionary<uint,int>[questArrayLength] - quest hash to source index mapping - owner: QuestStateManager
            _revertDescriptorIndexByItemHash = new Dictionary<uint, int>(Math.Max(questArrayLength, 8)); // COLD ALLOC: Dictionary<uint,int>[questArrayLength] - critical item revert lookup - owner: QuestStateManager
            _activeAddressesByQuestIndex = new QuestBitAddress[questArrayLength]; // COLD ALLOC: QuestBitAddress[questArrayLength] - quest active bit cache - owner: QuestStateManager
            _completedAddressesByQuestIndex = new QuestBitAddress[questArrayLength]; // COLD ALLOC: QuestBitAddress[questArrayLength] - quest completed bit cache - owner: QuestStateManager
            _phaseGateMasksByQuestIndex = new uint[questArrayLength]; // COLD ALLOC: uint[questArrayLength] - authored phase gate bitmask cache for O(1) manual activation guards - owner: QuestStateManager
            _questHashesByQuestIndex = new uint[questArrayLength]; // COLD ALLOC: uint[questArrayLength] - quest hash cache - owner: QuestStateManager
            _markerTargetHashesByQuestIndex = new uint[questArrayLength]; // COLD ALLOC: uint[questArrayLength] - quest marker target hash cache for authored and procedural directives - owner: QuestStateManager
            _markerWorldPositionsByQuestIndex = new Vector3[questArrayLength]; // COLD ALLOC: Vector3[questArrayLength] - quest marker fallback positions - owner: QuestStateManager
            _markerHeightOffsetsByQuestIndex = new float[questArrayLength]; // COLD ALLOC: float[questArrayLength] - quest marker height offsets - owner: QuestStateManager
            _questTitleBuffersByQuestIndex = CreateQuestTextBuffers(questArrayLength, QuestTitleCharCapacity); // COLD ALLOC: char[questArrayLength][128] - quest title presentation cache - owner: QuestStateManager
            _questDescriptionBuffersByQuestIndex = CreateQuestTextBuffers(questArrayLength, QuestDescriptionCharCapacity); // COLD ALLOC: char[questArrayLength][512] - quest description presentation cache - owner: QuestStateManager
            _questTitleLengthsByQuestIndex = new int[questArrayLength]; // COLD ALLOC: int[questArrayLength] - quest title presentation lengths - owner: QuestStateManager
            _questDescriptionLengthsByQuestIndex = new int[questArrayLength]; // COLD ALLOC: int[questArrayLength] - quest description presentation lengths - owner: QuestStateManager
            _proceduralNodeIndexByQuestIndex = new int[questArrayLength]; // COLD ALLOC: int[questArrayLength] - procedural completion-node slot mapping - owner: QuestStateManager

            // COLD ALLOC: List<QuestNodeDescriptor>[questArrayLength*2] - compiled quest DAG nodes - owner: QuestStateManager
            List<QuestNodeDescriptor> nodeBuilder = new List<QuestNodeDescriptor>(nodeBuilderCapacity);
            // COLD ALLOC: List<QuestPrerequisiteDescriptor>[questArrayLength*3] - flattened prerequisite masks - owner: QuestStateManager
            List<QuestPrerequisiteDescriptor> prerequisiteBuilder = new List<QuestPrerequisiteDescriptor>(prerequisiteBuilderCapacity);
            // COLD ALLOC: List<ThresholdFlag>[32] - unique depth threshold addresses - owner: QuestStateManager
            List<ThresholdFlag> depthFlags = new List<ThresholdFlag>(32);
            // COLD ALLOC: List<QuestRevertDescriptor>[questArrayLength] - critical item revert descriptors - owner: QuestStateManager
            List<QuestRevertDescriptor> revertBuilder = new List<QuestRevertDescriptor>(Math.Max(questArrayLength, 4));
            // COLD ALLOC: Dictionary<uint,string>[questArrayLength*6] - collision diagnostics for stable hashes - owner: QuestStateManager
            Dictionary<uint, string> hashLabels = new Dictionary<uint, string>(hashLabelCapacity);
            Span<int> bandBitUsage = stackalloc int[7];

            EnsurePhaseGateAddressesRegistered(hashLabels, bandBitUsage);

            for (int questIndex = 0; questIndex < _authoredQuestCount; questIndex++)
            {
                QuestData questData = allQuests[questIndex];
                if (questData == null || string.IsNullOrWhiteSpace(questData.questId))
                    continue;

                uint questHash = QuestFlagHashKernel.ComputeStableHash(questData.questId);
                if (questHash == 0u)
                {
                    RegisterCompileError("Quest hash resolved to zero. Stable IDs are required.");
                    continue;
                }

                if (_questIndexByHash.TryGetValue(questHash, out int existingQuestIndex) &&
                    existingQuestIndex != questIndex)
                {
                    RegisterCompileError(BuildQuestHashCollisionError(questData.questId, existingQuestIndex, questIndex, questHash));
                    continue;
                }

                _questIndexByHash[questHash] = questIndex;
                _questHashesByQuestIndex[questIndex] = questHash;
                _phaseGateMasksByQuestIndex[questIndex] = ResolvePhaseGateMask(questData.phaseGate);
                CopyAuthoredQuestPresentation(questData, questIndex);
                _markerTargetHashesByQuestIndex[questIndex] = QuestFlagHashKernel.ComputeStableHash(questData.markerTargetId);
                _markerWorldPositionsByQuestIndex[questIndex] = questData.RuntimeMarkerWorldPosition;
                _markerHeightOffsetsByQuestIndex[questIndex] = questData.RuntimeMarkerHeightOffset;
                _activeAddressesByQuestIndex[questIndex] = RegisterStateBit(
                    MixHash(questHash, ActiveFlagSalt),
                    QuestStateBand.Quest,
                    "quest-active",
                    hashLabels,
                    bandBitUsage);
                _completedAddressesByQuestIndex[questIndex] = RegisterStateBit(
                    MixHash(questHash, CompletedFlagSalt),
                    QuestStateBand.Quest,
                    "quest-complete",
                    hashLabels,
                    bandBitUsage);
            }

            for (int questIndex = 0; questIndex < _authoredQuestCount; questIndex++)
            {
                QuestData questData = allQuests[questIndex];
                if (questData == null || string.IsNullOrWhiteSpace(questData.questId))
                    continue;

                uint questHash = _questHashesByQuestIndex[questIndex];
                if (questHash == 0u)
                    continue;

                float triggerValue = questData.RuntimeTriggerValue;
                float completionValue = questData.RuntimeCompletionValue;
                RegisterTriggerStateBit(questData.triggerType, questData.triggerId, triggerValue, hashLabels, bandBitUsage, depthFlags);
                RegisterCompletionStateBit(questData.completionType, questData.completionId, completionValue, hashLabels, bandBitUsage, depthFlags);

                QuestSignalKind activationSignalKind = MapTriggerSignalKind(questData.triggerType);
                if (activationSignalKind != QuestSignalKind.None)
                {
                    int activationPrereqStart = prerequisiteBuilder.Count;
                    QuestBitAddress phaseGateAddress = BuildQuestActivationPrerequisites(
                        questData,
                        prerequisiteBuilder,
                        hashLabels,
                        bandBitUsage);
                    BuildPrerequisiteGate(prerequisiteBuilder, activationPrereqStart, prerequisiteBuilder.Count - activationPrereqStart, out ushort prereqWordIndex, out uint prereqMask);

                    nodeBuilder.Add(new QuestNodeDescriptor
                    {
                        QuestHash = questHash,
                        PayloadHash = ResolveSignalPayloadHash(activationSignalKind, questData.triggerId, triggerValue),
                        PrereqMask = prereqMask,
                        CompletionFlagID = _completedAddressesByQuestIndex[questIndex].FlagId,
                        PhaseGate = phaseGateAddress.FlagId,
                        ActiveFlagID = _activeAddressesByQuestIndex[questIndex].FlagId,
                        CriticalItemHash = ResolveCriticalItemHash(questData),
                        PrereqStartIndex = activationPrereqStart,
                        PrereqWordIndex = prereqWordIndex,
                        RequiredValue = triggerValue,
                        ActiveMask = _activeAddressesByQuestIndex[questIndex].BitMask,
                        CompletedMask = _completedAddressesByQuestIndex[questIndex].BitMask,
                        SetMask = _activeAddressesByQuestIndex[questIndex].BitMask,
                        ClearMask = 0u,
                        PrereqCount = (byte)math.min(prerequisiteBuilder.Count - activationPrereqStart, byte.MaxValue),
                        SignalKind = (byte)activationSignalKind,
                        TransitionType = (byte)QuestTransitionType.Activate,
                        QuestIndex = questIndex,
                        ActiveWordIndex = _activeAddressesByQuestIndex[questIndex].WordIndex,
                        CompletedWordIndex = _completedAddressesByQuestIndex[questIndex].WordIndex,
                        SetWordIndex = _activeAddressesByQuestIndex[questIndex].WordIndex,
                        ClearWordIndex = -1
                    });
                }

                QuestSignalKind completionSignalKind = MapCompletionSignalKind(questData.completionType);
                if (completionSignalKind != QuestSignalKind.None)
                {
                    int completionPrereqStart = prerequisiteBuilder.Count;
                    prerequisiteBuilder.Add(new QuestPrerequisiteDescriptor
                    {
                        StateWordIndex = _activeAddressesByQuestIndex[questIndex].WordIndex,
                        RequiredMask = _activeAddressesByQuestIndex[questIndex].BitMask
                    });
                    BuildPrerequisiteGate(prerequisiteBuilder, completionPrereqStart, 1, out ushort prereqWordIndex, out uint prereqMask);

                    nodeBuilder.Add(new QuestNodeDescriptor
                    {
                        QuestHash = questHash,
                        PayloadHash = ResolveSignalPayloadHash(completionSignalKind, questData.completionId, completionValue),
                        PrereqMask = prereqMask,
                        CompletionFlagID = _completedAddressesByQuestIndex[questIndex].FlagId,
                        PhaseGate = ResolvePhaseGateFlagId(questData.phaseGate),
                        ActiveFlagID = _activeAddressesByQuestIndex[questIndex].FlagId,
                        CriticalItemHash = ResolveCriticalItemHash(questData),
                        PrereqStartIndex = completionPrereqStart,
                        PrereqWordIndex = prereqWordIndex,
                        RequiredValue = completionValue,
                        ActiveMask = _activeAddressesByQuestIndex[questIndex].BitMask,
                        CompletedMask = _completedAddressesByQuestIndex[questIndex].BitMask,
                        SetMask = _completedAddressesByQuestIndex[questIndex].BitMask,
                        ClearMask = _activeAddressesByQuestIndex[questIndex].BitMask,
                        PrereqCount = 1,
                        SignalKind = (byte)completionSignalKind,
                        TransitionType = (byte)QuestTransitionType.Complete,
                        QuestIndex = questIndex,
                        ActiveWordIndex = _activeAddressesByQuestIndex[questIndex].WordIndex,
                        CompletedWordIndex = _completedAddressesByQuestIndex[questIndex].WordIndex,
                        SetWordIndex = _completedAddressesByQuestIndex[questIndex].WordIndex,
                        ClearWordIndex = _activeAddressesByQuestIndex[questIndex].WordIndex
                    });
                }

                uint criticalItemHash = ResolveCriticalItemHash(questData);
                if (criticalItemHash == 0u)
                    continue;

                QuestBitAddress entityDestroyAddress = RegisterStateBit(
                    MixHash(criticalItemHash, EntityDestroyFlagSalt),
                    QuestStateBand.EntityDestroy,
                    BuildQuestHashLabel("entity-destroy:", questData.questId, criticalItemHash),
                    hashLabels,
                    bandBitUsage);
                QuestBitAddress deadlockAddress = RegisterStateBit(
                    MixHash(criticalItemHash, DeadlockFlagSalt),
                    QuestStateBand.Deadlock,
                    BuildQuestHashLabel("deadlock:", questData.questId, criticalItemHash),
                    hashLabels,
                    bandBitUsage);

                QuestRevertDescriptor descriptor = new QuestRevertDescriptor
                {
                    CriticalItemHash = criticalItemHash,
                    EntityDestroyFlagId = entityDestroyAddress.FlagId,
                    DeadlockFlagId = deadlockAddress.FlagId,
                    ActiveFlagId = _activeAddressesByQuestIndex[questIndex].FlagId,
                    CompletedFlagId = _completedAddressesByQuestIndex[questIndex].FlagId,
                    RespawnEventHash = QuestFlagHashKernel.ComputeStableHash(questData.respawnEventId),
                    QuestIndex = questIndex
                };

                _revertDescriptorIndexByItemHash[criticalItemHash] = revertBuilder.Count;
                revertBuilder.Add(descriptor);
            }

            for (int proceduralOffset = 0; proceduralOffset < ProceduralQuestCapacity; proceduralOffset++)
            {
                int questIndex = _authoredQuestCount + proceduralOffset;
                uint slotSeed = 0x5100F000u + (uint)proceduralOffset;
                _activeAddressesByQuestIndex[questIndex] = RegisterStateBit(
                    MixHash(slotSeed, ActiveFlagSalt),
                    QuestStateBand.Quest,
                    BuildProceduralQuestLabel("quest-active:procedural:", proceduralOffset),
                    hashLabels,
                    bandBitUsage);
                _completedAddressesByQuestIndex[questIndex] = RegisterStateBit(
                    MixHash(slotSeed, CompletedFlagSalt),
                    QuestStateBand.Quest,
                    BuildProceduralQuestLabel("quest-complete:procedural:", proceduralOffset),
                    hashLabels,
                    bandBitUsage);
                _proceduralNodeIndexByQuestIndex[questIndex] = nodeBuilder.Count + proceduralOffset;
            }

            long nodeCapacityLong = (long)nodeBuilder.Count + ProceduralQuestCapacity;
            if (nodeCapacityLong > int.MaxValue)
            {
                Dispose();
                return false;
            }

            int nodeCapacity = (int)nodeCapacityLong;
            _nodes = H8Memory.Allocate<QuestNodeDescriptor>(nodeCapacity, NativeArrayOwnerSystem, DataVaultExemptQuestStateAllocator, NativeArrayOptions.ClearMemory);
            if (!_nodes.IsCreated)
            {
                Dispose();
                return false;
            }

            for (int i = 0; i < nodeBuilder.Count; i++)
                _nodes[i] = nodeBuilder[i];

            _prerequisites = default;
            if (prerequisiteBuilder.Count > 0)
            {
                _prerequisites = H8Memory.Allocate<QuestPrerequisiteDescriptor>(
                    prerequisiteBuilder.Count,
                    NativeArrayOwnerSystem,
                    DataVaultExemptQuestStateAllocator,
                    NativeArrayOptions.UninitializedMemory);
                if (!_prerequisites.IsCreated)
                {
                    Dispose();
                    return false;
                }
            }

            for (int i = 0; i < prerequisiteBuilder.Count; i++)
                _prerequisites[i] = prerequisiteBuilder[i];

            long runtimeResultCapacityLong = (long)nodeCapacity + revertBuilder.Count;
            if (runtimeResultCapacityLong > int.MaxValue)
            {
                Dispose();
                return false;
            }

            int runtimeResultCapacity = (int)runtimeResultCapacityLong;
            if (_runtimeResults.Capacity < runtimeResultCapacity)
                _runtimeResults.Capacity = runtimeResultCapacity;

            _activatedQuestIndices = new NativeList<int>(Math.Max(nodeCapacity, 1), DataVaultExemptQuestStateAllocator);
            _completedQuestIndices = new NativeList<int>(Math.Max(nodeCapacity, 1), DataVaultExemptQuestStateAllocator);
            try
            {
                RegisterNativeList(_activatedQuestIndices, nameof(_activatedQuestIndices), out _activatedQuestIndicesSentinelId);
                RegisterNativeList(_completedQuestIndices, nameof(_completedQuestIndices), out _completedQuestIndicesSentinelId);
            }
            catch
            {
                Dispose();
                return false;
            }

            _revertDescriptors = CopyListToArray(revertBuilder);
            _depthThresholdFlags = CopyListToArray(depthFlags);
            _isInitialized = true;
            RefreshStateMetadata(resetVersion: true);

            // The authored expectation, printed once at boot: which signal kind and which payload hash each
            // of the authored quests is waiting for. Without this the log only proves 12 quests were loaded,
            // not what any of them needs, so "authored=12 completions=0" was unreadable.
            LogQuestGateCensus("ARMED");
            return !HasCompileErrors;
        }

        private static void RegisterNativeList<T>(NativeList<T> list, string label, out int sentinelId)
            where T : unmanaged
        {
            sentinelId = NativeMemorySentinel.RegisterNativeListInstance(list, NativeMemoryOwner, label, NativeMemoryLifetime);
            if (sentinelId <= 0)
                throw new InvalidOperationException($"Native memory sentinel registration failed for {label}.");
        }

        private static void ReleaseNativeList<T>(ref NativeList<T> list, ref int sentinelId)
            where T : unmanaged
        {
            Exception firstException = null;

            if (sentinelId > 0)
            {
                try
                {
                    NativeMemorySentinel.Unregister(sentinelId);
                }
                catch (Exception exception)
                {
                    firstException = exception;
                }
                finally
                {
                    sentinelId = 0;
                }
            }

            if (list.IsCreated)
            {
                try
                {
                    list.Dispose();
                }
                catch (Exception exception)
                {
                    if (firstException == null)
                        firstException = exception;
                }
                finally
                {
                    list = default;
                }
            }
            else
            {
                list = default;
            }

            if (firstException != null)
                throw firstException;
        }

        public void RebindLocalization(ILocalizationTextReadModel localizationManager, QuestData[] allQuests)
        {
            _localizationManager = localizationManager;
            if (!_isInitialized)
                return;

            int authoredCount = math.min(_authoredQuestCount, allQuests != null ? allQuests.Length : 0);
            for (int i = 0; i < authoredCount; i++)
                CopyAuthoredQuestPresentation(allQuests[i], i);
        }

        public bool TryUpsertProceduralDirective(
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
            out int questIndex,
            out bool activatedNow)
        {
            questIndex = -1;
            activatedNow = false;

            // _runtimeResults describes the outcome of THIS call and nothing else. Every other producer
            // clears it on entry - ApplyAutoActivationFlags, EvaluateSignal and TryRevertCriticalItem all
            // do - and QuestManager.FlushRuntimeResults replays the whole list, so this was the one
            // producer that let a previous call's results be emitted a second time. Concretely: Start()
            // leaves two Activate results here after ApplyAutoActivationFlags, and a signal-driven
            // EvaluateSignal leaves its Complete results here; the next activating upsert from
            // ResourceScarcityDirector then re-emitted them, adding a duplicate QuestEvents.TryRaiseCompleted,
            // a duplicate objective notification, a second Zeigarnik injection attempt, and a second
            // Completed=1 record in the quest spine ring - which is a fabricated completion in the probe's
            // Mission row rather than a real one. ResourceScarcityDirector loops over every cached
            // directive in one pass, so the duplication compounded within a single pass. Cleared before the
            // guards, like the two producers above, so a rejected upsert also cannot leave stale results
            // behind for a later flush. List<T>.Clear on this unmanaged struct element type allocates
            // nothing.
            _runtimeResults.Clear();

            if (!_isInitialized ||
                questHash == 0u ||
                completionItemHash == 0u ||
                _questIndexByHash == null ||
                _questHashesByQuestIndex == null ||
                _proceduralNodeIndexByQuestIndex == null)
            {
                return false;
            }

            if (!TryGetQuestIndex(questHash, out questIndex))
            {
                questIndex = AllocateProceduralQuestSlot(questHash);
                if (questIndex < _authoredQuestCount)
                    return false;
            }

            if (questIndex < _authoredQuestCount)
                return false;

            _questHashesByQuestIndex[questIndex] = questHash;
            _phaseGateMasksByQuestIndex[questIndex] = ResolvePhaseGateMask(phaseGate);
            CopyProceduralQuestPresentation(questIndex, title, description);
            _markerTargetHashesByQuestIndex[questIndex] = markerTargetHash;
            _markerWorldPositionsByQuestIndex[questIndex] = markerWorldPosition;
            _markerHeightOffsetsByQuestIndex[questIndex] = math.max(0f, markerHeightOffset);
            ConfigureProceduralCompletionNode(questIndex, questHash, completionItemHash, phaseGate, requiredQuantity);

            bool mutated = false;
            if (activateWhenAllowed && IsBitSet(_completedAddressesByQuestIndex[questIndex]))
                mutated |= ClearBit(_completedAddressesByQuestIndex[questIndex]);

            if (activateWhenAllowed &&
                !IsBitSet(_activeAddressesByQuestIndex[questIndex]) &&
                !IsBitSet(_completedAddressesByQuestIndex[questIndex]) &&
                PhaseGateSatisfied(questIndex) &&
                SetBit(_activeAddressesByQuestIndex[questIndex]))
            {
                mutated = true;
                activatedNow = true;
                _runtimeResults.Add(new QuestRuntimeResult(questIndex, completed: false, QuestTransitionType.Activate));
                AppendTransitionAudit(questIndex, completed: false, QuestTransitionType.Activate, default);
            }

            if (mutated)
                RefreshStateMetadata(resetVersion: false);

            return true;
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

            if (!TryGetQuestIndex(questHash, out int questIndex))
                return false;

            TryCopyCachedQuestText(
                _questTitleBuffersByQuestIndex,
                _questTitleLengthsByQuestIndex,
                questIndex,
                titleDestination,
                out titleLength);
            TryCopyCachedQuestText(
                _questDescriptionBuffersByQuestIndex,
                _questDescriptionLengthsByQuestIndex,
                questIndex,
                descriptionDestination,
                out descriptionLength);
            markerTargetHash = _markerTargetHashesByQuestIndex != null && questIndex < _markerTargetHashesByQuestIndex.Length
                ? _markerTargetHashesByQuestIndex[questIndex]
                : 0u;
            markerWorldPosition = _markerWorldPositionsByQuestIndex != null && questIndex < _markerWorldPositionsByQuestIndex.Length
                ? _markerWorldPositionsByQuestIndex[questIndex]
                : default;
            markerHeightOffset = _markerHeightOffsetsByQuestIndex != null && questIndex < _markerHeightOffsetsByQuestIndex.Length
                ? _markerHeightOffsetsByQuestIndex[questIndex]
                : 0f;
            return titleLength > 0 || markerTargetHash != 0u || markerWorldPosition.sqrMagnitude > 0.0001f;
        }

        private static char[][] CreateQuestTextBuffers(int count, int capacity)
        {
            char[][] buffers = new char[count][];
            for (int i = 0; i < count; i++)
                buffers[i] = new char[capacity];

            return buffers;
        }

        private void CopyAuthoredQuestPresentation(QuestData questData, int questIndex)
        {
            if (questData == null || !IsQuestTextIndexValid(questIndex))
                return;

            char[] titleBuffer = _questTitleBuffersByQuestIndex[questIndex];
            if (questData.TryWriteDisplayTitleOrFallback(_localizationManager, titleBuffer, out int titleLength))
                _questTitleLengthsByQuestIndex[questIndex] = math.min(titleLength, titleBuffer.Length);
            else
                _questTitleLengthsByQuestIndex[questIndex] = CopySpanToBuffer("UNKNOWN OBJECTIVE".AsSpan(), titleBuffer);

            char[] descriptionBuffer = _questDescriptionBuffersByQuestIndex[questIndex];
            if (questData.TryWriteDescriptionOrFallback(_localizationManager, descriptionBuffer, out int descriptionLength))
                _questDescriptionLengthsByQuestIndex[questIndex] = math.min(descriptionLength, descriptionBuffer.Length);
            else
                _questDescriptionLengthsByQuestIndex[questIndex] = 0;
        }

        private void CopyProceduralQuestPresentation(int questIndex, string title, string description)
        {
            if (!IsQuestTextIndexValid(questIndex))
                return;

            _questTitleLengthsByQuestIndex[questIndex] = CopySpanToBuffer(
                string.IsNullOrWhiteSpace(title) ? "ATLAS-6 DIRECTIVE".AsSpan() : title.AsSpan(),
                _questTitleBuffersByQuestIndex[questIndex]);
            _questDescriptionLengthsByQuestIndex[questIndex] = CopySpanToBuffer(
                string.IsNullOrEmpty(description) ? ReadOnlySpan<char>.Empty : description.AsSpan(),
                _questDescriptionBuffersByQuestIndex[questIndex]);
        }

        private bool IsQuestTextIndexValid(int questIndex)
        {
            return _questTitleBuffersByQuestIndex != null &&
                   _questDescriptionBuffersByQuestIndex != null &&
                   _questTitleLengthsByQuestIndex != null &&
                   _questDescriptionLengthsByQuestIndex != null &&
                   (uint)questIndex < (uint)_questTitleBuffersByQuestIndex.Length &&
                   (uint)questIndex < (uint)_questDescriptionBuffersByQuestIndex.Length &&
                   (uint)questIndex < (uint)_questTitleLengthsByQuestIndex.Length &&
                   (uint)questIndex < (uint)_questDescriptionLengthsByQuestIndex.Length;
        }

        private bool HasCachedQuestTitle(int questIndex)
        {
            return _questTitleLengthsByQuestIndex != null &&
                   (uint)questIndex < (uint)_questTitleLengthsByQuestIndex.Length &&
                   _questTitleLengthsByQuestIndex[questIndex] > 0;
        }

        private static bool TryCopyCachedQuestText(
            char[][] buffers,
            int[] lengths,
            int questIndex,
            char[] destination,
            out int length)
        {
            length = 0;
            if (buffers == null ||
                lengths == null ||
                destination == null ||
                destination.Length == 0 ||
                (uint)questIndex >= (uint)buffers.Length ||
                (uint)questIndex >= (uint)lengths.Length)
            {
                return false;
            }

            char[] source = buffers[questIndex];
            int sourceLength = math.min(lengths[questIndex], source != null ? source.Length : 0);
            if (sourceLength <= 0)
                return true;

            length = CopySpanToBuffer(source.AsSpan(0, sourceLength), destination);
            return length == sourceLength;
        }

        private static int CopySpanToBuffer(ReadOnlySpan<char> source, char[] destination)
        {
            if (destination == null || destination.Length == 0 || source.Length == 0)
                return 0;

            int length = math.min(source.Length, destination.Length);
            for (int i = 0; i < length; i++)
                destination[i] = source[i];

            return length;
        }

        public bool TryGetQuestHash(int questIndex, out uint questHash)
        {
            questHash = 0u;
            return _questHashesByQuestIndex != null &&
                   questIndex >= 0 &&
                   questIndex < _questHashesByQuestIndex.Length &&
                   (questHash = _questHashesByQuestIndex[questIndex]) != 0u;
        }

        public bool IsQuestActive(uint questHash)
        {
            return TryGetQuestAddresses(questHash, out QuestBitAddress activeAddress, out _) &&
                   IsBitSet(activeAddress);
        }

        public bool IsQuestCompleted(uint questHash)
        {
            return TryGetQuestAddresses(questHash, out _, out QuestBitAddress completedAddress) &&
                   IsBitSet(completedAddress);
        }

        public bool GetFlag(uint flagId)
        {
            return flagId != 0u &&
                   _bitAddressByHash != null &&
                   _bitAddressByHash.TryGetValue(flagId, out QuestBitAddress address) &&
                   IsBitSet(address);
        }

        public bool SetFlag(uint flagId)
        {
            if (flagId == 0u ||
                _bitAddressByHash == null ||
                !_bitAddressByHash.TryGetValue(flagId, out QuestBitAddress address) ||
                !SetBit(address))
            {
                return false;
            }

            RefreshStateMetadata(resetVersion: false);
            return true;
        }

        public bool ClearFlag(uint flagId)
        {
            if (flagId == 0u ||
                _bitAddressByHash == null ||
                !_bitAddressByHash.TryGetValue(flagId, out QuestBitAddress address) ||
                !ClearBit(address))
            {
                return false;
            }

            RefreshStateMetadata(resetVersion: false);
            return true;
        }

        public bool TryActivateQuest(uint questHash, out int questIndex)
        {
            questIndex = -1;
            if (!TryGetQuestIndex(questHash, out questIndex))
                return false;

            QuestBitAddress activeAddress = _activeAddressesByQuestIndex[questIndex];
            QuestBitAddress completedAddress = _completedAddressesByQuestIndex[questIndex];
            if (IsBitSet(activeAddress) || IsBitSet(completedAddress))
                return false;

            if (!PhaseGateSatisfied(questIndex))
                return false;

            if (!SetBit(activeAddress))
                return false;

            RefreshStateMetadata(resetVersion: false);
            return true;
        }

        public bool TryCompleteQuest(uint questHash, out int questIndex)
        {
            questIndex = -1;
            if (!TryGetQuestIndex(questHash, out questIndex))
                return false;

            QuestBitAddress activeAddress = _activeAddressesByQuestIndex[questIndex];
            QuestBitAddress completedAddress = _completedAddressesByQuestIndex[questIndex];
            if (!IsBitSet(activeAddress) || IsBitSet(completedAddress))
                return false;

            bool changed = SetBit(completedAddress);
            if (!changed)
                return false;

            ClearBit(activeAddress);
            RefreshStateMetadata(resetVersion: false);
            return true;
        }

        public void ApplyAutoActivationFlags(QuestData[] allQuests)
        {
            _runtimeResults.Clear();
            if (!_isInitialized || allQuests == null)
                return;

            bool mutated = false;
            for (int questIndex = 0; questIndex < allQuests.Length; questIndex++)
            {
                QuestData questData = allQuests[questIndex];
                if (questData == null || !questData.autoActivateOnStart || string.IsNullOrWhiteSpace(questData.questId))
                    continue;

                uint questHash = QuestFlagHashKernel.ComputeStableHash(questData.questId);
                if (!TryActivateQuest(questHash, out int activatedQuestIndex))
                    continue;

                mutated = true;
                _runtimeResults.Add(new QuestRuntimeResult(activatedQuestIndex, completed: false, QuestTransitionType.Activate));
                AppendTransitionAudit(activatedQuestIndex, completed: false, QuestTransitionType.Activate, default);
            }

            if (mutated)
                RefreshStateMetadata(resetVersion: false);
        }

        public void EvaluateSignal(in QuestSignalPayload signal)
        {
            _runtimeResults.Clear();
            if (!_isInitialized || !_nodes.IsCreated)
                return;

            bool persistentMutation = ApplyPersistentSignalState(signal);
            _activatedQuestIndices.Clear();
            _completedQuestIndices.Clear();

            EvaluateQuestSignalJob job = new EvaluateQuestSignalJob
            {
                Signal = signal,
                GlobalPrerequisites = _globalPrerequisites,
                Nodes = _nodes,
                Prerequisites = _prerequisites,
                ActivatedQuestIndices = _activatedQuestIndices,
                CompletedQuestIndices = _completedQuestIndices
            };
            job.Execute();

            bool graphMutation = _activatedQuestIndices.Length > 0 || _completedQuestIndices.Length > 0;
            _signalsEvaluated++;
            _graphActivations += _activatedQuestIndices.Length;
            _graphCompletions += _completedQuestIndices.Length;
            if (!graphMutation)
                _signalsMatchedNothing++;

            LogQuestGateSignal(signal, _activatedQuestIndices.Length, _completedQuestIndices.Length);

            for (int i = 0; i < _activatedQuestIndices.Length; i++)
            {
                int questIndex = _activatedQuestIndices[i];
                _runtimeResults.Add(new QuestRuntimeResult(questIndex, completed: false, QuestTransitionType.Activate));
                AppendTransitionAudit(questIndex, completed: false, QuestTransitionType.Activate, signal);
            }

            for (int i = 0; i < _completedQuestIndices.Length; i++)
            {
                int questIndex = _completedQuestIndices[i];
                _runtimeResults.Add(new QuestRuntimeResult(questIndex, completed: true, QuestTransitionType.Complete));
                AppendTransitionAudit(questIndex, completed: true, QuestTransitionType.Complete, signal);
            }

            if (persistentMutation || graphMutation)
                RefreshStateMetadata(resetVersion: false);
        }

        public bool TryRevertCriticalItem(uint itemHash, double timestamp, out QuestRevertRequest request)
        {
            request = default;
            double safeTimestamp = ResolveAuditTimestamp(timestamp);

            if (!_isInitialized ||
                itemHash == 0u ||
                _bitAddressByHash == null ||
                _questHashesByQuestIndex == null ||
                _revertDescriptorIndexByItemHash == null ||
                !_revertDescriptorIndexByItemHash.TryGetValue(itemHash, out int descriptorIndex) ||
                _revertDescriptors == null ||
                descriptorIndex < 0 ||
                descriptorIndex >= _revertDescriptors.Length)
            {
                return false;
            }

            QuestRevertDescriptor descriptor = _revertDescriptors[descriptorIndex];
            if (descriptor.QuestIndex < 0 ||
                descriptor.QuestIndex >= _questHashesByQuestIndex.Length)
            {
                return false;
            }

            if (!_bitAddressByHash.TryGetValue(descriptor.EntityDestroyFlagId, out QuestBitAddress entityDestroyAddress) ||
                !_bitAddressByHash.TryGetValue(descriptor.DeadlockFlagId, out QuestBitAddress deadlockAddress) ||
                !_bitAddressByHash.TryGetValue(descriptor.CompletedFlagId, out QuestBitAddress completedAddress) ||
                !_bitAddressByHash.TryGetValue(descriptor.ActiveFlagId, out QuestBitAddress activeAddress))
            {
                return false;
            }

            if (!ApplyQuestRevertMutation(
                    _globalPrerequisites,
                    entityDestroyAddress.WordIndex,
                    entityDestroyAddress.BitMask,
                    deadlockAddress.WordIndex,
                    deadlockAddress.BitMask,
                    completedAddress.WordIndex,
                    completedAddress.BitMask,
                    activeAddress.WordIndex,
                    activeAddress.BitMask))
            {
                return false;
            }

            _runtimeResults.Clear();
            QuestSignalPayload payload = new QuestSignalPayload
            {
                EntityHash = itemHash,
                EventType = (ushort)QuestSignalKind.ItemLost,
                ItemId = itemHash,
                Timestamp = safeTimestamp
            };

            _runtimeResults.Add(new QuestRuntimeResult(descriptor.QuestIndex, completed: false, QuestTransitionType.Revert));
            AppendTransitionAudit(descriptor.QuestIndex, completed: false, QuestTransitionType.Revert, payload);
            RefreshStateMetadata(resetVersion: false);

            request = new QuestRevertRequest(
                _questHashesByQuestIndex[descriptor.QuestIndex],
                descriptor.CriticalItemHash,
                descriptor.RespawnEventHash,
                descriptor.QuestIndex);
            return true;
        }

        public QuestRuntimeResult GetResult(int index) => _runtimeResults[index];

        private static bool TryResolveQuestStateManagedCapacity(
            int baseCount,
            int multiplier,
            int minimum,
            out int capacity)
        {
            capacity = 0;
            if (baseCount < 0 || multiplier < 0 || minimum < 0)
                return false;

            long capacityLong = (long)baseCount * multiplier;
            if (capacityLong < minimum)
                capacityLong = minimum;

            if (capacityLong > int.MaxValue)
                return false;

            capacity = (int)capacityLong;
            return true;
        }

        public int CopyActiveQuestHashes(uint[] destination)
        {
            if (destination == null ||
                destination.Length <= 0 ||
                _questHashesByQuestIndex == null ||
                _activeAddressesByQuestIndex == null ||
                _completedAddressesByQuestIndex == null)
            {
                return 0;
            }

            int count = 0;
            for (int questIndex = 0; questIndex < _questHashesByQuestIndex.Length && count < destination.Length; questIndex++)
            {
                uint questHash = _questHashesByQuestIndex[questIndex];
                if (questHash == 0u)
                    continue;

                if (!IsBitSet(_activeAddressesByQuestIndex[questIndex]))
                    continue;

                if (IsBitSet(_completedAddressesByQuestIndex[questIndex]))
                    continue;

                destination[count++] = questHash;
            }

            return count;
        }

        public int CopyActiveQuestStates(NativeArray<QuestStateDTO> destination, int maxCount)
        {
            if (!destination.IsCreated ||
                maxCount <= 0 ||
                _questHashesByQuestIndex == null ||
                _activeAddressesByQuestIndex == null ||
                _completedAddressesByQuestIndex == null)
            {
                return 0;
            }

            int capacity = math.min(maxCount, destination.Length);
            int count = 0;
            for (int questIndex = 0; questIndex < _questHashesByQuestIndex.Length && count < capacity; questIndex++)
            {
                uint questHash = _questHashesByQuestIndex[questIndex];
                if (questHash == 0u)
                    continue;

                if (!IsBitSet(_activeAddressesByQuestIndex[questIndex]))
                    continue;

                if (IsBitSet(_completedAddressesByQuestIndex[questIndex]))
                    continue;

                destination[count++] = new QuestStateDTO
                {
                    ActiveQuestHashID = questHash,
                    CompletionProgress = 0f,
                    InjectedSubQuestHashID = 0u,
                    StateFlags = 0u
                };
            }

            return count;
        }

        public unsafe bool TryCopyPackedStateSnapshot(void* destinationPtr, int destinationWordCapacity)
        {
            if (destinationPtr == null || destinationWordCapacity < WordCapacity)
                return false;

            if (!_globalPrerequisites.IsCreated)
                return false;

            int copyBytes = WordCapacity * UnsafeUtility.SizeOf<uint>();
            void* sourcePtr = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(_globalPrerequisites);
            if (!UnsafeMemoryCopyGuard.SafeCopy(destinationPtr, copyBytes, sourcePtr, copyBytes))
            {
                UnsafeMemoryCopyGuard.ReportRejectedCopy(nameof(QuestStateManager));
                return false;
            }

            return true;
        }

        public QuestSaveHeader BuildSaveHeader(double timestamp)
        {
            QuestSaveHeader header = default;
            header.Magic = QuestSaveHeader.HeaderMagic;
            header.Version = _stateVersion;
            header.FlagCount = WordCapacity;
            header.Checksum = _stateChecksum;
            header.Timestamp = SanitizeNonNegativeFiniteTimestamp(timestamp);
            header.WriteSchemaVersion();
            return header;
        }

        public void RestorePackedState(uint[] packedWords)
        {
            RestorePackedState(default, packedWords);
        }

        public void RestorePackedState(in QuestSaveHeader header, uint[] packedWords)
        {
            if (!_globalPrerequisites.IsCreated)
                return;

            unsafe
            {
                UnsafeUtility.MemClear(
                    NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(_globalPrerequisites),
                    WordCapacity * UnsafeUtility.SizeOf<uint>());
            }

            bool copiedPackedState = false;
            if (packedWords != null && packedWords.Length > 0)
            {
                int copyWordCount = Math.Min(packedWords.Length, WordCapacity);
                unsafe
                {
                    fixed (uint* sourcePtr = packedWords)
                    {
                        int copyBytes = copyWordCount * UnsafeUtility.SizeOf<uint>();
                        int destinationBytes = WordCapacity * UnsafeUtility.SizeOf<uint>();
                        void* destinationPtr = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(_globalPrerequisites);
                        if (!UnsafeMemoryCopyGuard.SafeCopy(destinationPtr, destinationBytes, sourcePtr, copyBytes))
                        {
                            UnsafeMemoryCopyGuard.ReportRejectedCopy(nameof(QuestStateManager));
                            UnsafeUtility.MemClear(destinationPtr, destinationBytes);
                            copiedPackedState = false;
                        }
                        else
                        {
                            copiedPackedState = true;
                        }
                    }
                }
            }

            ApplyValidPackedWordMasks();

            uint schemaVersion = header.ReadSchemaVersion();
            bool headerMatchesPackedLayout =
                header.Magic == QuestSaveHeader.HeaderMagic &&
                header.FlagCount == WordCapacity;
            bool schemaRecognized = schemaVersion == 0u || schemaVersion == QuestSaveHeader.CurrentSchemaVersion;
            uint restoredChecksum = ComputePackedStateChecksum(_globalPrerequisites);
            bool trustedHeader =
                headerMatchesPackedLayout &&
                schemaRecognized &&
                copiedPackedState &&
                header.Version != 0u &&
                header.Checksum == restoredChecksum;

            _stateVersion = trustedHeader
                ? header.Version
                : 0u;
            _stateChecksum = trustedHeader
                ? header.Checksum
                : 0u;
            if (!trustedHeader)
                RefreshStateMetadata(resetVersion: false);
        }

        public void RestoreLegacyState(List<string> activeQuestIds, List<string> completedQuestIds)
        {
            if (!_globalPrerequisites.IsCreated)
                return;

            unsafe
            {
                void* destinationPtr = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(_globalPrerequisites);
                UnsafeUtility.MemClear(destinationPtr, WordCapacity * UnsafeUtility.SizeOf<uint>());
            }

            RestoreLegacyRange(activeQuestIds, completed: false);
            RestoreLegacyRange(completedQuestIds, completed: true);
            RefreshStateMetadata(resetVersion: false);
        }

        public void RecordManualTransition(int questIndex, bool completed)
        {
            AppendTransitionAudit(
                questIndex,
                completed,
                completed ? QuestTransitionType.Complete : QuestTransitionType.Activate,
                default);
        }

        private void RestoreLegacyRange(List<string> questIds, bool completed)
        {
            if (questIds == null)
                return;

            int questCount = questIds.Count;
            for (int i = 0; i < questCount; i++)
            {
                string questId = questIds[i];
                if (string.IsNullOrWhiteSpace(questId))
                    continue;

                uint questHash = QuestFlagHashKernel.ComputeStableHash(questId);
                if (!TryGetQuestIndex(questHash, out int questIndex))
                    continue;

                if (completed)
                {
                    SetBit(_completedAddressesByQuestIndex[questIndex]);
                    ClearBit(_activeAddressesByQuestIndex[questIndex]);
                }
                else
                {
                    SetBit(_activeAddressesByQuestIndex[questIndex]);
                }
            }
        }

        private bool ApplyPersistentSignalState(in QuestSignalPayload signal)
        {
            bool mutated = ApplyPhaseContextFlags((QuestSignalContextFlags)signal.Flags);
            QuestSignalKind kind = (QuestSignalKind)signal.EventType;
            switch (kind)
            {
                case QuestSignalKind.ItemCollected:
                case QuestSignalKind.CraftCompleted:
                case QuestSignalKind.DiscoveryMade:
                case QuestSignalKind.AudioLogFound:
                case QuestSignalKind.SignalDecoded:
                    return mutated | TrySetResolvedBit(signal.EntityHash);

                case QuestSignalKind.BiomeEntered:
                    return mutated | TrySetResolvedBit(ResolveBiomeSignalHash(signal));

                case QuestSignalKind.DepthReached:
                    return mutated | ApplyDepthThresholdFlags(signal.NumericValue);

                case QuestSignalKind.EclipseStarted:
                    return mutated | TrySetResolvedBit(EclipseFlagHash);

                default:
                    return mutated;
            }
        }

        private bool ApplyPhaseContextFlags(QuestSignalContextFlags contextFlags)
        {
            bool mutated = false;
            if ((contextFlags & QuestSignalContextFlags.ThermalPhase) != 0u)
                mutated |= SetBit(_thermalPhaseAddress);

            if ((contextFlags & QuestSignalContextFlags.AbyssalPhase) != 0u)
                mutated |= SetBit(_abyssalPhaseAddress);

            return mutated;
        }

        private bool ApplyDepthThresholdFlags(float depth)
        {
            if (_depthThresholdFlags == null)
                return false;

            bool mutated = false;
            for (int i = 0; i < _depthThresholdFlags.Length; i++)
            {
                ThresholdFlag flag = _depthThresholdFlags[i];
                if (depth < flag.Threshold)
                    continue;

                mutated |= SetBit(flag.Address);
            }

            return mutated;
        }

        private QuestBitAddress BuildQuestActivationPrerequisites(
            QuestData questData,
            List<QuestPrerequisiteDescriptor> prerequisiteBuilder,
            Dictionary<uint, string> hashLabels,
            Span<int> bandBitUsage)
        {
            QuestBitAddress phaseGateAddress = ResolvePhaseGateAddress(questData.phaseGate, hashLabels, bandBitUsage);
            if (phaseGateAddress.FlagId != 0u)
            {
                prerequisiteBuilder.Add(new QuestPrerequisiteDescriptor
                {
                    StateWordIndex = phaseGateAddress.WordIndex,
                    RequiredMask = phaseGateAddress.BitMask
                });
            }

            if (questData.prerequisiteQuestIds == null)
                return phaseGateAddress;

            for (int i = 0; i < questData.prerequisiteQuestIds.Length; i++)
            {
                string prerequisiteQuestId = questData.prerequisiteQuestIds[i];
                uint prerequisiteQuestHash = QuestFlagHashKernel.ComputeStableHash(prerequisiteQuestId);
                if (!TryGetQuestIndex(prerequisiteQuestHash, out int prerequisiteQuestIndex))
                {
                    RegisterCompileError(BuildUnknownPrerequisiteError(questData.questId, prerequisiteQuestId));
                    continue;
                }

                QuestBitAddress completedAddress = _completedAddressesByQuestIndex[prerequisiteQuestIndex];
                prerequisiteBuilder.Add(new QuestPrerequisiteDescriptor
                {
                    StateWordIndex = completedAddress.WordIndex,
                    RequiredMask = completedAddress.BitMask
                });
            }

            return phaseGateAddress;
        }

        private QuestBitAddress ResolvePhaseGateAddress(
            QuestPhaseGateType phaseGateType,
            Dictionary<uint, string> hashLabels,
            Span<int> bandBitUsage)
        {
            switch (phaseGateType)
            {
                case QuestPhaseGateType.Abyssal:
                    if (_abyssalPhaseAddress.FlagId == 0u)
                        _abyssalPhaseAddress = RegisterStateBit(_abyssalPhaseFlagHash, QuestStateBand.Phase, "phase.abyssal", hashLabels, bandBitUsage);
                    return _abyssalPhaseAddress;

                case QuestPhaseGateType.Thermal:
                    if (_thermalPhaseAddress.FlagId == 0u)
                        _thermalPhaseAddress = RegisterStateBit(_thermalPhaseFlagHash, QuestStateBand.Phase, "phase.thermal", hashLabels, bandBitUsage);
                    return _thermalPhaseAddress;

                default:
                    return default;
            }
        }

        private void EnsurePhaseGateAddressesRegistered(
            Dictionary<uint, string> hashLabels,
            Span<int> bandBitUsage)
        {
            ResolvePhaseGateAddress(QuestPhaseGateType.Abyssal, hashLabels, bandBitUsage);
            ResolvePhaseGateAddress(QuestPhaseGateType.Thermal, hashLabels, bandBitUsage);
        }

        private uint ResolvePhaseGateFlagId(QuestPhaseGateType phaseGateType)
        {
            switch (phaseGateType)
            {
                case QuestPhaseGateType.Abyssal:
                    return _abyssalPhaseFlagHash;

                case QuestPhaseGateType.Thermal:
                    return _thermalPhaseFlagHash;

                default:
                    return 0u;
            }
        }

        private uint ResolvePhaseGateMask(QuestPhaseGateType phaseGateType)
        {
            switch (phaseGateType)
            {
                case QuestPhaseGateType.Abyssal:
                    return _abyssalPhaseAddress.BitMask;

                case QuestPhaseGateType.Thermal:
                    return _thermalPhaseAddress.BitMask;

                default:
                    return 0u;
            }
        }

        private void RegisterTriggerStateBit(
            QuestTriggerType triggerType,
            string triggerId,
            float triggerValue,
            Dictionary<uint, string> hashLabels,
            Span<int> bandBitUsage,
            List<ThresholdFlag> depthFlags)
        {
            RegisterSignalStateBit(MapTriggerSignalKind(triggerType), triggerId, triggerValue, hashLabels, bandBitUsage, depthFlags);
        }

        private void RegisterCompletionStateBit(
            QuestCompletionType completionType,
            string completionId,
            float completionValue,
            Dictionary<uint, string> hashLabels,
            Span<int> bandBitUsage,
            List<ThresholdFlag> depthFlags)
        {
            RegisterSignalStateBit(MapCompletionSignalKind(completionType), completionId, completionValue, hashLabels, bandBitUsage, depthFlags);
        }

        private void RegisterSignalStateBit(
            QuestSignalKind signalKind,
            string signalId,
            float signalValue,
            Dictionary<uint, string> hashLabels,
            Span<int> bandBitUsage,
            List<ThresholdFlag> depthFlags)
        {
            if (signalKind == QuestSignalKind.None)
                return;

            uint payloadHash = ResolveSignalPayloadHash(signalKind, signalId, signalValue);
            QuestStateBand band = MapStateBand(signalKind);
            if (payloadHash == 0u && signalKind != QuestSignalKind.EclipseStarted)
                return;

            QuestBitAddress address = RegisterStateBit(payloadHash, band, BuildSignalDebugLabel(signalKind, signalId, signalValue), hashLabels, bandBitUsage);
            if (signalKind == QuestSignalKind.DepthReached)
            {
                depthFlags.Add(new ThresholdFlag
                {
                    Threshold = signalValue,
                    Address = address
                });
            }
        }

        private QuestBitAddress RegisterStateBit(
            uint bitHash,
            QuestStateBand band,
            string debugLabel,
            Dictionary<uint, string> hashLabels,
            Span<int> bandBitUsage)
        {
            if (bitHash == 0u)
                return default;

            if (_bitAddressByHash.TryGetValue(bitHash, out QuestBitAddress existingAddress))
            {
                if (hashLabels.TryGetValue(bitHash, out string existingLabel) &&
                    !string.Equals(existingLabel, debugLabel, StringComparison.Ordinal))
                {
                    RegisterCompileError(BuildQuestBitCollisionError(existingLabel, debugLabel, bitHash));
                }

                return existingAddress;
            }

            int bandIndex = (int)band;
            int bandBitIndex = bandBitUsage[bandIndex];
            int bandCapacity = GetBandWordCount(band) * WordStride;
            if (bandBitIndex >= bandCapacity)
            {
                RegisterCompileError(BuildQuestBandCapacityError(band, bandCapacity));
                return default;
            }

            QuestBitAddress address = new QuestBitAddress
            {
                WordIndex = GetBandStartWord(band) + (bandBitIndex >> 5),
                BitMask = 1u << (bandBitIndex & 0x1F),
                FlagId = bitHash
            };

            bandBitUsage[bandIndex] = bandBitIndex + 1;
            _bitAddressByHash.Add(bitHash, address);
            hashLabels[bitHash] = debugLabel;
            RegisterValidPackedWordMask(address);
            return address;
        }

        private void BuildPrerequisiteGate(
            List<QuestPrerequisiteDescriptor> prerequisites,
            int prerequisiteStart,
            int prerequisiteCount,
            out ushort gateWordIndex,
            out uint gateMask)
        {
            gateWordIndex = ushort.MaxValue;
            gateMask = 0u;
            if (prerequisiteCount <= 0)
                return;

            int sharedWordIndex = prerequisites[prerequisiteStart].StateWordIndex;
            uint sharedMask = 0u;
            for (int i = 0; i < prerequisiteCount; i++)
            {
                QuestPrerequisiteDescriptor prerequisite = prerequisites[prerequisiteStart + i];
                if (prerequisite.StateWordIndex != sharedWordIndex)
                {
                    gateWordIndex = ushort.MaxValue;
                    gateMask = 0u;
                    return;
                }

                sharedMask |= prerequisite.RequiredMask;
            }

            gateWordIndex = (ushort)sharedWordIndex;
            gateMask = sharedMask;
        }

        private static T[] CopyListToArray<T>(List<T> source)
        {
            if (source == null || source.Count == 0)
                return Array.Empty<T>();

            T[] result = new T[source.Count];
            source.CopyTo(result);
            return result;
        }

        private static string BuildQuestHashCollisionError(string questId, int existingQuestIndex, int questIndex, uint questHash)
        {
            return "Quest hash collision.";
        }

        private static string BuildQuestHashLabel(string prefix, string questId, uint hash)
        {
            return string.IsNullOrEmpty(prefix) ? "quest-bit" : prefix;
        }

        private static string BuildProceduralQuestLabel(string prefix, int proceduralOffset)
        {
            return string.IsNullOrEmpty(prefix) ? "quest-procedural" : prefix;
        }

        private static string BuildUnknownPrerequisiteError(string questId, string prerequisiteQuestId)
        {
            return "Quest references unknown prerequisite.";
        }

        private static string BuildSignalDebugLabel(QuestSignalKind signalKind, string signalId, float signalValue)
        {
            return ResolveQuestSignalKindLabel(signalKind);
        }

        private static string BuildQuestBitCollisionError(string existingLabel, string debugLabel, uint bitHash)
        {
            return "Quest bit collision.";
        }

        private static string BuildQuestBandCapacityError(QuestStateBand band, int bandCapacity)
        {
            return "Quest state band capacity exceeded.";
        }

        private static string ResolveQuestSignalKindLabel(QuestSignalKind signalKind)
        {
            switch (signalKind)
            {
                case QuestSignalKind.ItemCollected:
                    return "ItemCollected";
                case QuestSignalKind.DepthReached:
                    return "DepthReached";
                case QuestSignalKind.BiomeEntered:
                    return "BiomeEntered";
                case QuestSignalKind.DiscoveryMade:
                    return "DiscoveryMade";
                case QuestSignalKind.AudioLogFound:
                    return "AudioLogFound";
                case QuestSignalKind.EclipseStarted:
                    return "EclipseStarted";
                case QuestSignalKind.SignalDecoded:
                    return "SignalDecoded";
                case QuestSignalKind.ItemLost:
                    return "ItemLost";
                case QuestSignalKind.CraftCompleted:
                    return "CraftCompleted";
                default:
                    return "None";
            }
        }

        private static string ResolveQuestStateBandLabel(QuestStateBand band)
        {
            switch (band)
            {
                case QuestStateBand.Item:
                    return "Item";
                case QuestStateBand.Location:
                    return "Location";
                case QuestStateBand.Narrative:
                    return "Narrative";
                case QuestStateBand.Phase:
                    return "Phase";
                case QuestStateBand.EntityDestroy:
                    return "EntityDestroy";
                case QuestStateBand.Deadlock:
                    return "Deadlock";
                default:
                    return "Quest";
            }
        }

        private static int CountIntDigits(int value)
        {
            long remaining = value;
            int digits = remaining < 0L ? 2 : 1;
            if (remaining < 0L)
                remaining = -remaining;

            while (remaining >= 10L)
            {
                remaining /= 10L;
                digits++;
            }

            return digits;
        }

        private static int WriteInt(int value, Span<char> buffer, int start)
        {
            long remaining = value;
            bool negative = remaining < 0L;
            if (negative)
            {
                buffer[start++] = '-';
                remaining = -remaining;
            }

            int digitCount = CountPositiveIntDigits(remaining);
            int write = start + digitCount - 1;
            do
            {
                buffer[write--] = (char)('0' + remaining % 10L);
                remaining /= 10L;
            }
            while (write >= start);

            return start + digitCount;
        }

        private static int CountPositiveIntDigits(long value)
        {
            int digits = 1;
            while (value >= 10L)
            {
                value /= 10L;
                digits++;
            }

            return digits;
        }

        private static int WriteHex8(uint value, Span<char> buffer, int start)
        {
            for (int shift = 28; shift >= 0; shift -= 4)
            {
                int nibble = (int)((value >> shift) & 0xFu);
                buffer[start++] = (char)(nibble < 10 ? '0' + nibble : 'A' + nibble - 10);
            }

            return start;
        }

        private static int CountFloatChars(float value)
        {
            Span<char> temp = stackalloc char[32];
            return value.TryFormat(temp, out int written, ReadOnlySpan<char>.Empty, null) ? written : 1;
        }

        private static int WriteFloat(float value, Span<char> buffer, int start)
        {
            return value.TryFormat(buffer.Slice(start), out int written, ReadOnlySpan<char>.Empty, null)
                ? start + written
                : CopyString("0", buffer, start);
        }

        private static int CountDoubleF3Chars(double value)
        {
            Span<char> temp = stackalloc char[32];
            return value.TryFormat(temp, out int written, "F3".AsSpan(), null) ? written : 1;
        }

        private static int WriteDoubleF3(double value, Span<char> buffer, int start)
        {
            return value.TryFormat(buffer.Slice(start), out int written, "F3".AsSpan(), null)
                ? start + written
                : CopyString("0", buffer, start);
        }

        private static int CopyString(string value, Span<char> buffer, int start)
        {
            for (int i = 0; i < value.Length; i++)
                buffer[start + i] = value[i];

            return start + value.Length;
        }

        private void RegisterCompileError(string message)
        {
            _compileErrorCount++;
            if (_compileErrorCount == 1)
                _compileErrorSummary = message;
        }

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        private void AppendTransitionAudit(int questIndex, bool completed, QuestTransitionType transitionType, QuestSignalPayload signal)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (questIndex < 0 ||
                _questHashesByQuestIndex == null ||
                questIndex >= _questHashesByQuestIndex.Length)
            {
                return;
            }

            double auditTimestamp = ResolveAuditTimestamp(signal.Timestamp);
            int writeIndex = _transitionAuditWriteIndex;
            _transitionAuditRing[writeIndex] = new QuestTransitionAuditEntry
            {
                Timestamp = auditTimestamp,
                QuestHash = _questHashesByQuestIndex[questIndex],
                EntityHash = signal.EntityHash,
                ItemId = signal.ItemId,
                NumericValue = signal.NumericValue,
                SignalEventType = signal.EventType,
                TransitionType = (byte)transitionType,
                Completed = completed ? (byte)1 : (byte)0
            };

            _transitionAuditWriteIndex = writeIndex + 1 < QuestTransitionAuditCapacity ? writeIndex + 1 : 0;
            if (_transitionAuditCount < QuestTransitionAuditCapacity)
                _transitionAuditCount++;
#endif
        }

        /// <summary>
        /// Emits one development-only line per signal the packed graph evaluated, naming the signal and the
        /// transitions it produced, so a run can distinguish "no signal reached the quest graph" from
        /// "signals reached it and matched no authored node".
        /// </summary>
        /// <remarks>
        /// The two Conditional attributes delete the CALL SITE in a release player, which is what
        /// AGENTS.md requires of a log on a signal path - a serialized bool would not, and this runs inside
        /// the late-frame event drain. The one string per line is therefore editor/development only and is
        /// additionally hard-capped at <see cref="QuestGateSignalLogCap"/> lines per state manager, matching
        /// the cap discipline of QuestManager.LogQuestSpineTransition.
        /// </remarks>
        /// <param name="signal">Signal that was just evaluated.</param>
        /// <param name="activatedCount">Quests the signal activated.</param>
        /// <param name="completedCount">Quests the signal completed.</param>
        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        private void LogQuestGateSignal(QuestSignalPayload signal, int activatedCount, int completedCount)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (_questGateSignalLogCount >= QuestGateSignalLogCap)
                return;

            _questGateSignalLogCount++;

            Span<char> buffer = _questGateLogBuffer.AsSpan();
            int length = CopyString(QuestGateLogPrefix, buffer, 0);
            length = CopyString(activatedCount > 0 || completedCount > 0 ? "HIT kind=" : "MISS kind=", buffer, length);
            length = CopyString(ResolveQuestSignalKindLabel((QuestSignalKind)signal.EventType), buffer, length);
            length = CopyString(" hash=0x", buffer, length);
            length = WriteHex8(signal.EntityHash, buffer, length);
            length = CopyString(" value=", buffer, length);
            length = WriteFloat(signal.NumericValue, buffer, length);
            length = CopyString(" act=", buffer, length);
            length = WriteInt(activatedCount, buffer, length);
            length = CopyString(" comp=", buffer, length);
            length = WriteInt(completedCount, buffer, length);
            length = CopyString(" nodes=", buffer, length);
            length = WriteInt(_nodes.IsCreated ? _nodes.Length : 0, buffer, length);

            // COLD ALLOC: string[1] - one line per evaluated signal, capped at QuestGateSignalLogCap and deleted from a release player by the Conditional attributes - owner: QuestStateManager
            Hecton8.Core.H8Debug.Log(new string(_questGateLogBuffer, 0, length));
#endif
        }

        /// <summary>
        /// Emits the quest gate census: one development-only line per authored quest naming the exact
        /// condition that would advance it next, plus one summary line carrying the signal counters.
        /// </summary>
        /// <remarks>
        /// This exists because <c>completions=0</c> is not a diagnosis. The census answers, per quest,
        /// which signal kind and which payload hash the graph is waiting for, whether the quest is idle,
        /// active or done, and whether its phase gate and prerequisite quests are already satisfied - so a
        /// row that never completes names the blocking condition instead of a bare zero. Called from
        /// Initialize (the authored expectation, before any signal) and from Dispose (the final waiting
        /// state), both cold paths that run once per session.
        /// </remarks>
        /// <param name="stageLabel">Short stage tag written into the line, ARMED or WAIT.</param>
        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        private void LogQuestGateCensus(string stageLabel)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!_isInitialized ||
                _questHashesByQuestIndex == null ||
                _activeAddressesByQuestIndex == null ||
                _completedAddressesByQuestIndex == null)
            {
                return;
            }

            Span<char> buffer = _questGateLogBuffer.AsSpan();
            int length = CopyString(QuestGateLogPrefix, buffer, 0);
            length = CopyString(stageLabel, buffer, length);
            length = CopyString(" SUMMARY authored=", buffer, length);
            length = WriteInt(_authoredQuestCount, buffer, length);
            length = CopyString(" nodes=", buffer, length);
            length = WriteInt(_nodes.IsCreated ? _nodes.Length : 0, buffer, length);
            length = CopyString(" signals=", buffer, length);
            length = WriteInt(_signalsEvaluated, buffer, length);
            length = CopyString(" unmatched=", buffer, length);
            length = WriteInt(_signalsMatchedNothing, buffer, length);
            length = CopyString(" graphAct=", buffer, length);
            length = WriteInt(_graphActivations, buffer, length);
            length = CopyString(" graphComp=", buffer, length);
            length = WriteInt(_graphCompletions, buffer, length);

            // COLD ALLOC: string[1] - one summary line per census, cold path - owner: QuestStateManager
            Hecton8.Core.H8Debug.Log(new string(_questGateLogBuffer, 0, length));

            for (int questIndex = 0; questIndex < _authoredQuestCount; questIndex++)
                LogQuestGateCensusRow(stageLabel, questIndex);
#endif
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        /// <summary>
        /// Emits one census row for a single authored quest.
        /// </summary>
        /// <param name="stageLabel">Short stage tag written into the line.</param>
        /// <param name="questIndex">Authored quest slot to describe.</param>
        private void LogQuestGateCensusRow(string stageLabel, int questIndex)
        {
            if ((uint)questIndex >= (uint)_questHashesByQuestIndex.Length)
                return;

            uint questHash = _questHashesByQuestIndex[questIndex];
            if (questHash == 0u)
                return;

            bool completed = IsBitSet(_completedAddressesByQuestIndex[questIndex]);
            bool active = !completed && IsBitSet(_activeAddressesByQuestIndex[questIndex]);

            // Declared and seeded outside the guard on purpose: an `out` inside a short-circuiting `&&`
            // leaves the local not-definitely-assigned on the false branch, which is CS0165 at the use site
            // below.
            QuestNodeDescriptor node = default;
            bool hasNode = false;
            if (!completed)
            {
                hasNode = TryFindQuestNode(
                    questIndex,
                    active ? QuestTransitionType.Complete : QuestTransitionType.Activate,
                    out node);
            }

            Span<char> buffer = _questGateLogBuffer.AsSpan();
            int length = CopyString(QuestGateLogPrefix, buffer, 0);
            length = CopyString(stageLabel, buffer, length);
            length = CopyString(" q=0x", buffer, length);
            length = WriteHex8(questHash, buffer, length);
            length = CopyString(" i=", buffer, length);
            length = WriteInt(questIndex, buffer, length);
            length = CopyString(" state=", buffer, length);
            length = CopyString(completed ? "DONE" : active ? "ACTIVE" : "IDLE", buffer, length);
            length = CopyString(" need=", buffer, length);

            if (completed)
            {
                length = CopyString("none", buffer, length);
            }
            else if (!hasNode)
            {
                // No node of the wanted transition class exists. For an idle quest that is an authored
                // Manual trigger, which no signal can ever satisfy; for an active quest it means the
                // completion type mapped to QuestSignalKind.None, so nothing can ever close it.
                length = CopyString(active ? "UNCLOSEABLE" : "Manual", buffer, length);
            }
            else
            {
                length = CopyString(ResolveQuestSignalKindLabel((QuestSignalKind)node.SignalKind), buffer, length);
                length = CopyString(" hash=0x", buffer, length);
                length = WriteHex8(node.PayloadHash, buffer, length);
                length = CopyString(" value=", buffer, length);
                length = WriteFloat(node.RequiredValue, buffer, length);
                length = CopyString(" prereq=", buffer, length);
                length = WriteInt(NodePrerequisitesSatisfied(node) ? 1 : 0, buffer, length);
            }

            length = CopyString(" phaseGate=", buffer, length);
            length = WriteInt(PhaseGateSatisfied(questIndex) ? 1 : 0, buffer, length);

            // COLD ALLOC: string[1] - one line per authored quest per census, cold path - owner: QuestStateManager
            Hecton8.Core.H8Debug.Log(new string(_questGateLogBuffer, 0, length));
        }

        /// <summary>
        /// Finds the compiled node that carries a given transition class for a quest slot.
        /// </summary>
        /// <param name="questIndex">Quest slot to search for.</param>
        /// <param name="transitionType">Transition class the node must carry.</param>
        /// <param name="node">Matching node when the search succeeds.</param>
        /// <returns>True when a node exists.</returns>
        private bool TryFindQuestNode(int questIndex, QuestTransitionType transitionType, out QuestNodeDescriptor node)
        {
            node = default;
            if (!_nodes.IsCreated)
                return false;

            for (int nodeIndex = 0; nodeIndex < _nodes.Length; nodeIndex++)
            {
                QuestNodeDescriptor candidate = _nodes[nodeIndex];
                if (candidate.QuestIndex != questIndex || candidate.TransitionType != (byte)transitionType)
                    continue;

                node = candidate;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Reports whether a node's prerequisites are already satisfied, using the evaluator's own predicate.
        /// </summary>
        /// <remarks>
        /// Deliberately delegates to <see cref="EvaluateQuestSignalJob.PrerequisitesSatisfied"/> rather than
        /// reimplementing the check. A second copy of the gate logic would drift from the one the graph
        /// actually runs, and a diagnostic that disagrees with the evaluator is worse than none.
        /// </remarks>
        /// <param name="node">Node to test.</param>
        /// <returns>True when the node's prerequisite gate is open.</returns>
        private bool NodePrerequisitesSatisfied(in QuestNodeDescriptor node)
        {
            if (!_globalPrerequisites.IsCreated)
                return false;

            if (node.PrereqCount > 0 && !_prerequisites.IsCreated)
                return false;

            return EvaluateQuestSignalJob.PrerequisitesSatisfied(node, _prerequisites, _globalPrerequisites);
        }
#endif

        private bool TrySetResolvedBit(uint bitHash)
        {
            return bitHash != 0u &&
                   _bitAddressByHash != null &&
                   _bitAddressByHash.TryGetValue(bitHash, out QuestBitAddress address) &&
                   SetBit(address);
        }

        private static double SanitizeNonNegativeFiniteTimestamp(double timestamp)
        {
            return math.isfinite(timestamp) && timestamp >= 0d
                ? timestamp
                : 0d;
        }

        private static double ResolveAuditTimestamp(double timestamp)
        {
            if (math.isfinite(timestamp) && timestamp > 0d)
                return timestamp;

            return SanitizeNonNegativeFiniteTimestamp(Time.timeAsDouble);
        }

        private bool TryGetQuestIndex(uint questHash, out int questIndex)
        {
            questIndex = -1;
            return questHash != 0u &&
                   _questIndexByHash != null &&
                   _questIndexByHash.TryGetValue(questHash, out questIndex);
        }

        private bool TryGetQuestAddresses(uint questHash, out QuestBitAddress activeAddress, out QuestBitAddress completedAddress)
        {
            activeAddress = default;
            completedAddress = default;
            if (!TryGetQuestIndex(questHash, out int questIndex))
                return false;

            activeAddress = _activeAddressesByQuestIndex[questIndex];
            completedAddress = _completedAddressesByQuestIndex[questIndex];
            return true;
        }

        private bool PhaseGateSatisfied(int questIndex)
        {
            if (_phaseGateMasksByQuestIndex == null ||
                questIndex < 0 ||
                questIndex >= _phaseGateMasksByQuestIndex.Length ||
                !_globalPrerequisites.IsCreated)
            {
                return true;
            }

            uint requiredPhaseMask = _phaseGateMasksByQuestIndex[questIndex];
            return requiredPhaseMask == 0u ||
                   (_globalPrerequisites[PhaseWordStart] & requiredPhaseMask) != 0u;
        }

        private bool IsBitSet(QuestBitAddress address)
        {
            return _globalPrerequisites.IsCreated &&
                   address.WordIndex >= 0 &&
                   address.WordIndex < _globalPrerequisites.Length &&
                   (_globalPrerequisites[address.WordIndex] & address.BitMask) == address.BitMask;
        }

        private bool SetBit(QuestBitAddress address)
        {
            if (!_globalPrerequisites.IsCreated || address.WordIndex < 0 || address.WordIndex >= _globalPrerequisites.Length)
                return false;

            uint currentValue = _globalPrerequisites[address.WordIndex];
            uint updatedValue = currentValue | address.BitMask;
            if (currentValue == updatedValue)
                return false;

            _globalPrerequisites[address.WordIndex] = updatedValue;
            return true;
        }

        private bool ClearBit(QuestBitAddress address)
        {
            if (!_globalPrerequisites.IsCreated || address.WordIndex < 0 || address.WordIndex >= _globalPrerequisites.Length)
                return false;

            uint currentValue = _globalPrerequisites[address.WordIndex];
            uint updatedValue = currentValue & ~address.BitMask;
            if (currentValue == updatedValue)
                return false;

            _globalPrerequisites[address.WordIndex] = updatedValue;
            return true;
        }

        private void RegisterValidPackedWordMask(QuestBitAddress address)
        {
            if (!_validPackedWordMasks.IsCreated ||
                address.WordIndex < 0 ||
                address.WordIndex >= _validPackedWordMasks.Length)
            {
                return;
            }

            _validPackedWordMasks[address.WordIndex] |= address.BitMask;
        }

        private void ApplyValidPackedWordMasks()
        {
            if (!_globalPrerequisites.IsCreated)
                return;

            if (!_validPackedWordMasks.IsCreated)
            {
                for (int i = 0; i < _globalPrerequisites.Length; i++)
                    _globalPrerequisites[i] = 0u;
                return;
            }

            int count = math.min(_globalPrerequisites.Length, _validPackedWordMasks.Length);
            for (int i = 0; i < count; i++)
                _globalPrerequisites[i] &= _validPackedWordMasks[i];

            for (int i = count; i < _globalPrerequisites.Length; i++)
                _globalPrerequisites[i] = 0u;
        }

        private bool SetBitByFlagId(uint flagId)
        {
            return flagId != 0u &&
                   _bitAddressByHash.TryGetValue(flagId, out QuestBitAddress address) &&
                   SetBit(address);
        }

        private bool ClearBitByFlagId(uint flagId)
        {
            return flagId != 0u &&
                   _bitAddressByHash.TryGetValue(flagId, out QuestBitAddress address) &&
                   ClearBit(address);
        }

        private void RefreshStateMetadata(bool resetVersion)
        {
            _stateVersion = resetVersion ? 1u : _stateVersion + 1u;
            if (!_globalPrerequisites.IsCreated)
            {
                _stateChecksum = 0u;
                return;
            }

            _stateChecksum = ComputePackedStateChecksum(_globalPrerequisites);
        }

        private static uint ResolveCriticalItemHash(QuestData questData)
        {
            if (questData == null)
                return 0u;

            if (!string.IsNullOrWhiteSpace(questData.criticalItemId))
                return ComputeSignalIdHash(questData.criticalItemId);

            return questData.completionType == QuestCompletionType.OnItemCollected
                ? ComputeSignalIdHash(questData.completionId)
                : 0u;
        }

        private static uint ResolveBiomeSignalHash(in QuestSignalPayload signal)
        {
            return signal.EntityHash != 0u
                ? signal.EntityHash
                : ComputeNumericSignalHash(QuestSignalKind.BiomeEntered, signal.NumericValue);
        }

        private static QuestSignalKind MapTriggerSignalKind(QuestTriggerType triggerType)
        {
            switch (triggerType)
            {
                case QuestTriggerType.OnItemCollected:
                    return QuestSignalKind.ItemCollected;
                case QuestTriggerType.OnCraftCompleted:
                    return QuestSignalKind.CraftCompleted;
                case QuestTriggerType.OnDepthReached:
                    return QuestSignalKind.DepthReached;
                case QuestTriggerType.OnBiomeEntered:
                    return QuestSignalKind.BiomeEntered;
                case QuestTriggerType.OnDiscoveryMade:
                    return QuestSignalKind.DiscoveryMade;
                case QuestTriggerType.OnAudioLogFound:
                    return QuestSignalKind.AudioLogFound;
                case QuestTriggerType.OnEclipseStart:
                    return QuestSignalKind.EclipseStarted;
                case QuestTriggerType.OnSignalDetected:
                    return QuestSignalKind.SignalDecoded;
                default:
                    return QuestSignalKind.None;
            }
        }

        private static QuestSignalKind MapCompletionSignalKind(QuestCompletionType completionType)
        {
            switch (completionType)
            {
                case QuestCompletionType.OnItemCollected:
                    return QuestSignalKind.ItemCollected;
                case QuestCompletionType.OnCraftCompleted:
                    return QuestSignalKind.CraftCompleted;
                case QuestCompletionType.OnDepthReached:
                    return QuestSignalKind.DepthReached;
                case QuestCompletionType.OnBiomeEntered:
                    return QuestSignalKind.BiomeEntered;
                case QuestCompletionType.OnDiscoveryMade:
                    return QuestSignalKind.DiscoveryMade;
                case QuestCompletionType.OnAudioLogFound:
                    return QuestSignalKind.AudioLogFound;
                case QuestCompletionType.OnSignalDecoded:
                    return QuestSignalKind.SignalDecoded;
                default:
                    return QuestSignalKind.None;
            }
        }

        private static QuestStateBand MapStateBand(QuestSignalKind signalKind)
        {
            switch (signalKind)
            {
                case QuestSignalKind.ItemCollected:
                case QuestSignalKind.CraftCompleted:
                case QuestSignalKind.ItemLost:
                    return QuestStateBand.Item;
                case QuestSignalKind.BiomeEntered:
                    return QuestStateBand.Location;
                case QuestSignalKind.DiscoveryMade:
                case QuestSignalKind.AudioLogFound:
                case QuestSignalKind.SignalDecoded:
                    return QuestStateBand.Narrative;
                case QuestSignalKind.DepthReached:
                case QuestSignalKind.EclipseStarted:
                    return QuestStateBand.Phase;
                default:
                    return QuestStateBand.Quest;
            }
        }

        private static int GetBandStartWord(QuestStateBand band)
        {
            switch (band)
            {
                case QuestStateBand.Quest:
                    return QuestWordStart;
                case QuestStateBand.Item:
                    return ItemWordStart;
                case QuestStateBand.Location:
                    return LocationWordStart;
                case QuestStateBand.Narrative:
                    return NarrativeWordStart;
                case QuestStateBand.Phase:
                    return PhaseWordStart;
                case QuestStateBand.EntityDestroy:
                    return EntityDestroyWordStart;
                case QuestStateBand.Deadlock:
                    return DeadlockWordStart;
                default:
                    return QuestWordStart;
            }
        }

        private static int GetBandWordCount(QuestStateBand band)
        {
            switch (band)
            {
                case QuestStateBand.Quest:
                    return QuestWordCount;
                case QuestStateBand.Item:
                    return ItemWordCount;
                case QuestStateBand.Location:
                    return LocationWordCount;
                case QuestStateBand.Narrative:
                    return NarrativeWordCount;
                case QuestStateBand.Phase:
                    return PhaseWordCount;
                case QuestStateBand.EntityDestroy:
                    return EntityDestroyWordCount;
                case QuestStateBand.Deadlock:
                    return DeadlockWordCount;
                default:
                    return QuestWordCount;
            }
        }

        private static uint ResolveSignalPayloadHash(QuestSignalKind signalKind, string signalId, float signalValue)
        {
            switch (signalKind)
            {
                case QuestSignalKind.ItemCollected:
                case QuestSignalKind.CraftCompleted:
                case QuestSignalKind.DiscoveryMade:
                case QuestSignalKind.AudioLogFound:
                case QuestSignalKind.SignalDecoded:
                    return ComputeSignalIdHash(signalId);

                case QuestSignalKind.BiomeEntered:
                case QuestSignalKind.DepthReached:
                    return ComputeNumericSignalHash(signalKind, signalValue);

                case QuestSignalKind.EclipseStarted:
                    return EclipseFlagHash;

                default:
                    return 0u;
            }
        }

        private static uint ComputeSignalIdHash(string signalId)
        {
            return string.IsNullOrWhiteSpace(signalId)
                ? 0u
                : unchecked((uint)Hecton.Localization.LocHash.Compute(signalId));
        }

        private static uint ComputeNumericSignalHash(QuestSignalKind signalKind, float numericValue)
        {
            unchecked
            {
                uint hash = Hecton.Localization.LocHash.FnvOffsetBasis;
                hash ^= (uint)signalKind;
                hash *= Hecton.Localization.LocHash.FnvPrime;

                uint valueBits = (uint)BitConverter.SingleToInt32Bits(numericValue);
                hash ^= valueBits & 0xFFu;
                hash *= Hecton.Localization.LocHash.FnvPrime;
                hash ^= (valueBits >> 8) & 0xFFu;
                hash *= Hecton.Localization.LocHash.FnvPrime;
                hash ^= (valueBits >> 16) & 0xFFu;
                hash *= Hecton.Localization.LocHash.FnvPrime;
                hash ^= (valueBits >> 24) & 0xFFu;
                hash *= Hecton.Localization.LocHash.FnvPrime;
                hash ^= signalKind == QuestSignalKind.BiomeEntered ? BiomeFlagSalt : DepthFlagSalt;
                hash *= Hecton.Localization.LocHash.FnvPrime;
                return hash;
            }
        }

        private static uint MixHash(uint sourceHash, uint salt)
        {
            unchecked
            {
                uint mixed = sourceHash ^ salt;
                mixed *= Hecton.Localization.LocHash.FnvPrime;
                mixed ^= salt >> 8;
                mixed *= Hecton.Localization.LocHash.FnvPrime;
                return mixed;
            }
        }

        private struct ThresholdFlag
        {
            public float Threshold;
            public QuestBitAddress Address;
        }

        private int AllocateProceduralQuestSlot(uint questHash)
        {
            for (int questIndex = _authoredQuestCount; questIndex < _questHashesByQuestIndex.Length; questIndex++)
            {
                if (_questHashesByQuestIndex[questIndex] != 0u)
                    continue;

                _questHashesByQuestIndex[questIndex] = questHash;
                _questIndexByHash[questHash] = questIndex;
                return questIndex;
            }

            return -1;
        }

        private void ConfigureProceduralCompletionNode(
            int questIndex,
            uint questHash,
            uint completionItemHash,
            QuestPhaseGateType phaseGate,
            float requiredQuantity)
        {
            int nodeIndex = _proceduralNodeIndexByQuestIndex[questIndex];
            if (!_nodes.IsCreated || nodeIndex < 0 || nodeIndex >= _nodes.Length)
                return;

            QuestBitAddress activeAddress = _activeAddressesByQuestIndex[questIndex];
            QuestBitAddress completedAddress = _completedAddressesByQuestIndex[questIndex];
            _nodes[nodeIndex] = new QuestNodeDescriptor
            {
                QuestHash = questHash,
                PayloadHash = completionItemHash,
                PrereqMask = 0u,
                CompletionFlagID = completedAddress.FlagId,
                FailureFlagID = 0u,
                RevertFlagID = 0u,
                PhaseGate = ResolvePhaseGateFlagId(phaseGate),
                ActiveFlagID = activeAddress.FlagId,
                CriticalItemHash = 0u,
                PrereqStartIndex = 0,
                PrereqWordIndex = ushort.MaxValue,
                ReservedWordIndex = 0,
                RequiredValue = math.max(1f, requiredQuantity),
                ActiveMask = activeAddress.BitMask,
                CompletedMask = completedAddress.BitMask,
                SetMask = completedAddress.BitMask,
                ClearMask = activeAddress.BitMask,
                PrereqCount = 0,
                SignalKind = (byte)QuestSignalKind.ItemCollected,
                TransitionType = (byte)QuestTransitionType.Complete,
                Reserved = 0,
                QuestIndex = questIndex,
                ActiveWordIndex = activeAddress.WordIndex,
                CompletedWordIndex = completedAddress.WordIndex,
                SetWordIndex = completedAddress.WordIndex,
                ClearWordIndex = activeAddress.WordIndex
            };
        }

        private static uint ComputePackedStateChecksum(NativeArray<uint> globalPrerequisites)
        {
            unchecked
            {
                uint hash = Hecton.Localization.LocHash.FnvOffsetBasis;
                for (int i = 0; i < globalPrerequisites.Length; i++)
                {
                    uint word = globalPrerequisites[i];
                    hash ^= word & 0xFFu;
                    hash *= Hecton.Localization.LocHash.FnvPrime;
                    hash ^= (word >> 8) & 0xFFu;
                    hash *= Hecton.Localization.LocHash.FnvPrime;
                    hash ^= (word >> 16) & 0xFFu;
                    hash *= Hecton.Localization.LocHash.FnvPrime;
                    hash ^= (word >> 24) & 0xFFu;
                    hash *= Hecton.Localization.LocHash.FnvPrime;
                }

                return hash;
            }
        }

        private static bool ApplyQuestRevertMutation(
            NativeArray<uint> globalPrerequisites,
            int entityDestroyWordIndex,
            uint entityDestroyMask,
            int deadlockWordIndex,
            uint deadlockMask,
            int completedWordIndex,
            uint completedMask,
            int activeWordIndex,
            uint activeMask)
        {
            if (!IsValidQuestWord(globalPrerequisites, completedWordIndex) ||
                !IsValidQuestWord(globalPrerequisites, entityDestroyWordIndex) ||
                !IsValidQuestWord(globalPrerequisites, deadlockWordIndex) ||
                !IsValidQuestWord(globalPrerequisites, activeWordIndex))
            {
                return false;
            }

            if ((globalPrerequisites[completedWordIndex] & completedMask) != completedMask)
                return false;

            globalPrerequisites[entityDestroyWordIndex] |= entityDestroyMask;
            globalPrerequisites[deadlockWordIndex] |= deadlockMask;
            globalPrerequisites[completedWordIndex] &= ~completedMask;
            globalPrerequisites[activeWordIndex] |= activeMask;
            return true;
        }

        private static bool IsValidQuestWord(NativeArray<uint> globalPrerequisites, int wordIndex)
        {
            return wordIndex >= 0 && wordIndex < globalPrerequisites.Length;
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private struct EvaluateQuestSignalJob : IJob
        {
            public QuestSignalPayload Signal;
            public NativeArray<uint> GlobalPrerequisites;
            [ReadOnly] public NativeArray<QuestNodeDescriptor> Nodes;
            [ReadOnly] public NativeArray<QuestPrerequisiteDescriptor> Prerequisites;
            public NativeList<int> ActivatedQuestIndices;
            public NativeList<int> CompletedQuestIndices;

            public void Execute()
            {
                for (int nodeIndex = 0; nodeIndex < Nodes.Length; nodeIndex++)
                {
                    QuestNodeDescriptor node = Nodes[nodeIndex];
                    if (node.SignalKind != Signal.EventType)
                        continue;

                    if (!MatchesSignal(node, Signal))
                        continue;

                    if (!PrerequisitesSatisfied(node, Prerequisites, GlobalPrerequisites))
                        continue;

                    if (node.TransitionType == (byte)QuestTransitionType.Activate)
                    {
                        if ((GlobalPrerequisites[node.ActiveWordIndex] & node.ActiveMask) == node.ActiveMask)
                            continue;

                        if ((GlobalPrerequisites[node.CompletedWordIndex] & node.CompletedMask) == node.CompletedMask)
                            continue;

                        GlobalPrerequisites[node.SetWordIndex] |= node.SetMask;
                        ActivatedQuestIndices.AddNoResize(node.QuestIndex);
                        continue;
                    }

                    if ((GlobalPrerequisites[node.CompletedWordIndex] & node.CompletedMask) == node.CompletedMask)
                        continue;

                    if ((GlobalPrerequisites[node.ActiveWordIndex] & node.ActiveMask) != node.ActiveMask)
                        continue;

                    GlobalPrerequisites[node.SetWordIndex] |= node.SetMask;
                    if (node.ClearMask != 0u && node.ClearWordIndex >= 0)
                        GlobalPrerequisites[node.ClearWordIndex] &= ~node.ClearMask;

                    CompletedQuestIndices.AddNoResize(node.QuestIndex);
                }
            }

            private static bool PrerequisitesSatisfied(
                QuestNodeDescriptor node,
                NativeArray<QuestPrerequisiteDescriptor> prerequisites,
                NativeArray<uint> globalPrerequisites)
            {
                if (node.PrereqMask != 0u && node.PrereqWordIndex != ushort.MaxValue)
                {
                    return (globalPrerequisites[node.PrereqWordIndex] & node.PrereqMask) == node.PrereqMask;
                }

                for (int prerequisiteIndex = 0; prerequisiteIndex < node.PrereqCount; prerequisiteIndex++)
                {
                    QuestPrerequisiteDescriptor prerequisite = prerequisites[node.PrereqStartIndex + prerequisiteIndex];
                    if ((globalPrerequisites[prerequisite.StateWordIndex] & prerequisite.RequiredMask) != prerequisite.RequiredMask)
                        return false;
                }

                return true;
            }

            private static bool MatchesSignal(QuestNodeDescriptor node, QuestSignalPayload signal)
            {
                switch ((QuestSignalKind)node.SignalKind)
                {
                    case QuestSignalKind.ItemCollected:
                    case QuestSignalKind.CraftCompleted:
                    case QuestSignalKind.DiscoveryMade:
                    case QuestSignalKind.AudioLogFound:
                    case QuestSignalKind.SignalDecoded:
                        if (node.PayloadHash != 0u && node.PayloadHash != signal.EntityHash)
                            return false;

                        return node.RequiredValue <= 0f || signal.NumericValue >= node.RequiredValue;

                    case QuestSignalKind.DepthReached:
                        return signal.NumericValue >= node.RequiredValue;

                    case QuestSignalKind.BiomeEntered:
                        return (int)signal.NumericValue == (int)node.RequiredValue;

                    case QuestSignalKind.EclipseStarted:
                        return true;

                    default:
                        return false;
                }
            }
        }
    }
}
