using System;
using System.Collections.Generic;
using System.IO;
using Conditional = System.Diagnostics.ConditionalAttribute;
using Hecton8.Core;
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
        private const int WordCapacity = 320;
        private const int WordStride = 32;
        private const int QuestWordStart = 0;
        private const int QuestWordCount = 64;
        private const int ItemWordStart = 64;
        private const int ItemWordCount = 64;
        private const int LocationWordStart = 128;
        private const int LocationWordCount = 64;
        private const int NarrativeWordStart = 192;
        private const int NarrativeWordCount = 32;
        private const int PhaseWordStart = 224;
        private const int PhaseWordCount = 32;
        private const int EntityDestroyWordStart = 256;
        private const int EntityDestroyWordCount = 32;
        private const int DeadlockWordStart = 288;
        private const int DeadlockWordCount = 32;
        private const uint ActiveFlagSalt = 0xA11F0A11u;
        private const uint CompletedFlagSalt = 0xC0DE0C01u;
        private const uint BiomeFlagSalt = 0xB10F0001u;
        private const uint DepthFlagSalt = 0xD37A0001u;
        private const uint EclipseFlagHash = 0xE011C1E5u;
        private const uint EntityDestroyFlagSalt = 0xD357F1A6u;
        private const uint DeadlockFlagSalt = 0xDEAD10CCu;
        private const string QuestAuditLogFileName = "quest_transition_audit.log";
        private const string NativeMemoryOwner = nameof(QuestStateManager);
        private const NativeAllocationLifetime NativeMemoryLifetime = NativeAllocationLifetime.Scene;
        private static readonly uint _abyssalPhaseFlagHash = QuestFlagHashKernel.ComputeStableHash("phase.abyssal");
        private static readonly uint _thermalPhaseFlagHash = QuestFlagHashKernel.ComputeStableHash("phase.thermal");

        // COLD ALLOC: List<QuestRuntimeResult>[32] - transition handoff from packed runtime to facade - owner: QuestStateManager
        private readonly List<QuestRuntimeResult> _runtimeResults = new List<QuestRuntimeResult>(32);

        private NativeArray<uint> _globalPrerequisites;
        private NativeArray<uint> _checksumResult;
        private NativeArray<QuestNodeDescriptor> _nodes;
        private NativeArray<QuestPrerequisiteDescriptor> _prerequisites;
        private NativeList<int> _activatedQuestIndices;
        private NativeList<int> _completedQuestIndices;
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
        private string[] _questTitlesByQuestIndex;
        private string[] _questDescriptionsByQuestIndex;
        private int[] _proceduralNodeIndexByQuestIndex;
        private QuestRevertDescriptor[] _revertDescriptors;
        private QuestBitAddress _abyssalPhaseAddress;
        private QuestBitAddress _thermalPhaseAddress;
        private ThresholdFlag[] _depthThresholdFlags;
        private int _authoredQuestCount;
        private string _compileErrorSummary = string.Empty;
        private uint _stateVersion;
        private uint _stateChecksum;
        private bool _isInitialized;

        public bool HasCompileErrors => !string.IsNullOrEmpty(_compileErrorSummary);

        public string CompileErrorSummary => _compileErrorSummary;

        public int WordCount => WordCapacity;

        public int ResultCount => _runtimeResults.Count;

        public uint StateVersion => _stateVersion;

        public uint StateChecksum => _stateChecksum;

        private static void RegisterTrackedNativeArray<T>(NativeArray<T> array, string label) where T : struct
        {
            if (!array.IsCreated)
                return;

            NativeMemorySentinel.RegisterNativeArray(
                array,
                NativeMemoryOwner,
                label,
                NativeMemoryLifetime);
        }

        public void Dispose()
        {
            if (_activatedQuestIndices.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeList(NativeMemoryOwner, nameof(_activatedQuestIndices));
                _activatedQuestIndices.Dispose();
            }

            if (_completedQuestIndices.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeList(NativeMemoryOwner, nameof(_completedQuestIndices));
                _completedQuestIndices.Dispose();
            }

            if (_nodes.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeArray(_nodes);
                _nodes.Dispose();
            }

            if (_prerequisites.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeArray(_prerequisites);
                _prerequisites.Dispose();
            }

            if (_globalPrerequisites.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeArray(_globalPrerequisites);
                _globalPrerequisites.Dispose();
            }

            if (_checksumResult.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeArray(_checksumResult);
                _checksumResult.Dispose();
            }

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
            _questTitlesByQuestIndex = null;
            _questDescriptionsByQuestIndex = null;
            _proceduralNodeIndexByQuestIndex = null;
            _revertDescriptors = null;
            _abyssalPhaseAddress = default;
            _thermalPhaseAddress = default;
            _depthThresholdFlags = null;
            _authoredQuestCount = 0;
            _compileErrorSummary = string.Empty;
            _stateVersion = 0u;
            _stateChecksum = 0u;
            _isInitialized = false;
        }

        public bool Initialize(QuestData[] allQuests)
        {
            Dispose();

            _authoredQuestCount = allQuests != null ? allQuests.Length : 0;
            int questArrayLength = _authoredQuestCount + ProceduralQuestCapacity;
            _globalPrerequisites = new NativeArray<uint>(WordCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            _checksumResult = new NativeArray<uint>(1, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<uint>[1] - Burst checksum result slot - owner: QuestStateManager
            RegisterTrackedNativeArray(_globalPrerequisites, nameof(_globalPrerequisites));
            RegisterTrackedNativeArray(_checksumResult, nameof(_checksumResult));
            _bitAddressByHash = new Dictionary<uint, QuestBitAddress>(Math.Max(questArrayLength * 6, 16)); // COLD ALLOC: Dictionary<uint,QuestBitAddress>[questArrayLength*6] - compiled flag lookup - owner: QuestStateManager
            _questIndexByHash = new Dictionary<uint, int>(Math.Max(questArrayLength, 16)); // COLD ALLOC: Dictionary<uint,int>[questArrayLength] - quest hash to source index mapping - owner: QuestStateManager
            _revertDescriptorIndexByItemHash = new Dictionary<uint, int>(Math.Max(questArrayLength, 8)); // COLD ALLOC: Dictionary<uint,int>[questArrayLength] - critical item revert lookup - owner: QuestStateManager
            _activeAddressesByQuestIndex = new QuestBitAddress[questArrayLength]; // COLD ALLOC: QuestBitAddress[questArrayLength] - quest active bit cache - owner: QuestStateManager
            _completedAddressesByQuestIndex = new QuestBitAddress[questArrayLength]; // COLD ALLOC: QuestBitAddress[questArrayLength] - quest completed bit cache - owner: QuestStateManager
            _phaseGateMasksByQuestIndex = new uint[questArrayLength]; // COLD ALLOC: uint[questArrayLength] - authored phase gate bitmask cache for O(1) manual activation guards - owner: QuestStateManager
            _questHashesByQuestIndex = new uint[questArrayLength]; // COLD ALLOC: uint[questArrayLength] - quest hash cache - owner: QuestStateManager
            _markerTargetHashesByQuestIndex = new uint[questArrayLength]; // COLD ALLOC: uint[questArrayLength] - quest marker target hash cache for authored and procedural directives - owner: QuestStateManager
            _markerWorldPositionsByQuestIndex = new Vector3[questArrayLength]; // COLD ALLOC: Vector3[questArrayLength] - quest marker fallback positions - owner: QuestStateManager
            _markerHeightOffsetsByQuestIndex = new float[questArrayLength]; // COLD ALLOC: float[questArrayLength] - quest marker height offsets - owner: QuestStateManager
            _questTitlesByQuestIndex = new string[questArrayLength]; // COLD ALLOC: string[questArrayLength] - quest title cache for authored and procedural presentation - owner: QuestStateManager
            _questDescriptionsByQuestIndex = new string[questArrayLength]; // COLD ALLOC: string[questArrayLength] - quest description cache for authored and procedural presentation - owner: QuestStateManager
            _proceduralNodeIndexByQuestIndex = new int[questArrayLength]; // COLD ALLOC: int[questArrayLength] - procedural completion-node slot mapping - owner: QuestStateManager

            // COLD ALLOC: List<QuestNodeDescriptor>[questArrayLength*2] - compiled quest DAG nodes - owner: QuestStateManager
            List<QuestNodeDescriptor> nodeBuilder = new List<QuestNodeDescriptor>(Math.Max(questArrayLength * 2, 8));
            // COLD ALLOC: List<QuestPrerequisiteDescriptor>[questArrayLength*3] - flattened prerequisite masks - owner: QuestStateManager
            List<QuestPrerequisiteDescriptor> prerequisiteBuilder = new List<QuestPrerequisiteDescriptor>(Math.Max(questArrayLength * 3, 8));
            // COLD ALLOC: List<ThresholdFlag>[32] - unique depth threshold addresses - owner: QuestStateManager
            List<ThresholdFlag> depthFlags = new List<ThresholdFlag>(32);
            // COLD ALLOC: List<QuestRevertDescriptor>[questArrayLength] - critical item revert descriptors - owner: QuestStateManager
            List<QuestRevertDescriptor> revertBuilder = new List<QuestRevertDescriptor>(Math.Max(questArrayLength, 4));
            // COLD ALLOC: Dictionary<uint,string>[questArrayLength*6] - collision diagnostics for stable hashes - owner: QuestStateManager
            Dictionary<uint, string> hashLabels = new Dictionary<uint, string>(Math.Max(questArrayLength * 6, 16));
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
                    RegisterCompileError($"Quest '{questData.name}' resolved to hash 0. Stable IDs are required.");
                    continue;
                }

                if (_questIndexByHash.TryGetValue(questHash, out int existingQuestIndex) &&
                    existingQuestIndex != questIndex)
                {
                    RegisterCompileError($"Quest hash collision for '{questData.questId}'. Source indices {existingQuestIndex} and {questIndex} resolve to 0x{questHash:X8}.");
                    continue;
                }

                _questIndexByHash[questHash] = questIndex;
                _questHashesByQuestIndex[questIndex] = questHash;
                _phaseGateMasksByQuestIndex[questIndex] = ResolvePhaseGateMask(questData.phaseGate);
                _questTitlesByQuestIndex[questIndex] = questData.DisplayTitleOrFallback;
                _questDescriptionsByQuestIndex[questIndex] = questData.DescriptionOrFallback;
                _markerTargetHashesByQuestIndex[questIndex] = QuestFlagHashKernel.ComputeStableHash(questData.markerTargetId);
                _markerWorldPositionsByQuestIndex[questIndex] = questData.markerWorldPosition;
                _markerHeightOffsetsByQuestIndex[questIndex] = math.max(0f, questData.markerHeightOffset);
                _activeAddressesByQuestIndex[questIndex] = RegisterStateBit(
                    MixHash(questHash, ActiveFlagSalt),
                    QuestStateBand.Quest,
                    $"quest-active:{questData.questId}",
                    hashLabels,
                    bandBitUsage);
                _completedAddressesByQuestIndex[questIndex] = RegisterStateBit(
                    MixHash(questHash, CompletedFlagSalt),
                    QuestStateBand.Quest,
                    $"quest-complete:{questData.questId}",
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

                RegisterTriggerStateBit(questData.triggerType, questData.triggerId, questData.triggerValue, hashLabels, bandBitUsage, depthFlags);
                RegisterCompletionStateBit(questData.completionType, questData.completionId, questData.completionValue, hashLabels, bandBitUsage, depthFlags);

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
                        PayloadHash = ResolveSignalPayloadHash(activationSignalKind, questData.triggerId, questData.triggerValue),
                        PrereqMask = prereqMask,
                        CompletionFlagID = _completedAddressesByQuestIndex[questIndex].FlagId,
                        PhaseGate = phaseGateAddress.FlagId,
                        ActiveFlagID = _activeAddressesByQuestIndex[questIndex].FlagId,
                        CriticalItemHash = ResolveCriticalItemHash(questData),
                        PrereqStartIndex = activationPrereqStart,
                        PrereqWordIndex = prereqWordIndex,
                        RequiredValue = questData.triggerValue,
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
                        PayloadHash = ResolveSignalPayloadHash(completionSignalKind, questData.completionId, questData.completionValue),
                        PrereqMask = prereqMask,
                        CompletionFlagID = _completedAddressesByQuestIndex[questIndex].FlagId,
                        PhaseGate = ResolvePhaseGateFlagId(questData.phaseGate),
                        ActiveFlagID = _activeAddressesByQuestIndex[questIndex].FlagId,
                        CriticalItemHash = ResolveCriticalItemHash(questData),
                        PrereqStartIndex = completionPrereqStart,
                        PrereqWordIndex = prereqWordIndex,
                        RequiredValue = questData.completionValue,
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
                    $"entity-destroy:{questData.questId}:{criticalItemHash:X8}",
                    hashLabels,
                    bandBitUsage);
                QuestBitAddress deadlockAddress = RegisterStateBit(
                    MixHash(criticalItemHash, DeadlockFlagSalt),
                    QuestStateBand.Deadlock,
                    $"deadlock:{questData.questId}:{criticalItemHash:X8}",
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
                    $"quest-active:procedural:{proceduralOffset}",
                    hashLabels,
                    bandBitUsage);
                _completedAddressesByQuestIndex[questIndex] = RegisterStateBit(
                    MixHash(slotSeed, CompletedFlagSalt),
                    QuestStateBand.Quest,
                    $"quest-complete:procedural:{proceduralOffset}",
                    hashLabels,
                    bandBitUsage);
                _proceduralNodeIndexByQuestIndex[questIndex] = nodeBuilder.Count + proceduralOffset;
            }

            int nodeCapacity = nodeBuilder.Count + ProceduralQuestCapacity;
            _nodes = new NativeArray<QuestNodeDescriptor>(nodeCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            RegisterTrackedNativeArray(_nodes, nameof(_nodes));
            for (int i = 0; i < nodeBuilder.Count; i++)
                _nodes[i] = nodeBuilder[i];

            _prerequisites = new NativeArray<QuestPrerequisiteDescriptor>(prerequisiteBuilder.Count, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            RegisterTrackedNativeArray(_prerequisites, nameof(_prerequisites));
            for (int i = 0; i < prerequisiteBuilder.Count; i++)
                _prerequisites[i] = prerequisiteBuilder[i];

            if (_runtimeResults.Capacity < nodeCapacity + revertBuilder.Count)
                _runtimeResults.Capacity = nodeCapacity + revertBuilder.Count;

            _activatedQuestIndices = new NativeList<int>(Math.Max(nodeCapacity, 1), Allocator.Persistent);
            _completedQuestIndices = new NativeList<int>(Math.Max(nodeCapacity, 1), Allocator.Persistent);
            NativeMemorySentinel.RegisterNativeList(_activatedQuestIndices, NativeMemoryOwner, nameof(_activatedQuestIndices), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeList(_completedQuestIndices, NativeMemoryOwner, nameof(_completedQuestIndices), NativeMemoryLifetime);
            _revertDescriptors = revertBuilder.ToArray();
            _depthThresholdFlags = depthFlags.ToArray();
            _isInitialized = true;
            RefreshStateMetadata(resetVersion: true);
            return !HasCompileErrors;
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
            _questTitlesByQuestIndex[questIndex] = string.IsNullOrWhiteSpace(title) ? "ATLAS-6 DIRECTIVE" : title;
            _questDescriptionsByQuestIndex[questIndex] = description ?? string.Empty;
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

        public bool TryGetQuestPresentation(
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

            if (!TryGetQuestIndex(questHash, out int questIndex))
                return false;

            title = _questTitlesByQuestIndex != null && questIndex < _questTitlesByQuestIndex.Length
                ? _questTitlesByQuestIndex[questIndex]
                : string.Empty;
            description = _questDescriptionsByQuestIndex != null && questIndex < _questDescriptionsByQuestIndex.Length
                ? _questDescriptionsByQuestIndex[questIndex]
                : string.Empty;
            markerTargetHash = _markerTargetHashesByQuestIndex != null && questIndex < _markerTargetHashesByQuestIndex.Length
                ? _markerTargetHashesByQuestIndex[questIndex]
                : 0u;
            markerWorldPosition = _markerWorldPositionsByQuestIndex != null && questIndex < _markerWorldPositionsByQuestIndex.Length
                ? _markerWorldPositionsByQuestIndex[questIndex]
                : default;
            markerHeightOffset = _markerHeightOffsetsByQuestIndex != null && questIndex < _markerHeightOffsetsByQuestIndex.Length
                ? _markerHeightOffsetsByQuestIndex[questIndex]
                : 0f;
            return !string.IsNullOrWhiteSpace(title) || markerTargetHash != 0u || markerWorldPosition.sqrMagnitude > 0.0001f;
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
            JobHandle signalEvaluationHandle = job.Schedule();
            signalEvaluationHandle.Complete();

            bool graphMutation = _activatedQuestIndices.Length > 0 || _completedQuestIndices.Length > 0;
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
            _runtimeResults.Clear();

            if (!_isInitialized ||
                itemHash == 0u ||
                _revertDescriptorIndexByItemHash == null ||
                !_revertDescriptorIndexByItemHash.TryGetValue(itemHash, out int descriptorIndex) ||
                _revertDescriptors == null ||
                descriptorIndex < 0 ||
                descriptorIndex >= _revertDescriptors.Length)
            {
                return false;
            }

            QuestRevertDescriptor descriptor = _revertDescriptors[descriptorIndex];
            if (!GetFlag(descriptor.CompletedFlagId))
                return false;

            bool mutated = false;
            mutated |= SetBitByFlagId(descriptor.EntityDestroyFlagId);
            mutated |= SetBitByFlagId(descriptor.DeadlockFlagId);
            mutated |= ClearBitByFlagId(descriptor.CompletedFlagId);
            mutated |= SetBitByFlagId(descriptor.ActiveFlagId);
            if (!mutated)
                return false;

            QuestSignalPayload payload = new QuestSignalPayload
            {
                EntityHash = itemHash,
                EventType = (ushort)QuestSignalKind.ItemLost,
                ItemId = itemHash,
                Timestamp = timestamp
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

        public NativeArray<uint> CapturePackedStateSnapshot(Allocator allocator)
        {
            NativeArray<uint> snapshot = new NativeArray<uint>(WordCapacity, allocator, NativeArrayOptions.ClearMemory);
            if (!_globalPrerequisites.IsCreated)
                return snapshot;

            unsafe
            {
                int copyBytes = WordCapacity * UnsafeUtility.SizeOf<uint>();
                void* sourcePtr = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(_globalPrerequisites);
                void* destinationPtr = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(snapshot);
                if (!UnsafeMemoryCopyGuard.SafeCopy(destinationPtr, copyBytes, sourcePtr, copyBytes))
                    UnsafeMemoryCopyGuard.ReportRejectedCopy(nameof(QuestStateManager));
            }

            return snapshot;
        }

        public QuestSaveHeader BuildSaveHeader(double timestamp)
        {
            QuestSaveHeader header = default;
            header.Magic = QuestSaveHeader.HeaderMagic;
            header.Version = _stateVersion;
            header.FlagCount = WordCapacity;
            header.Checksum = _stateChecksum;
            header.Timestamp = timestamp;
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
                            UnsafeMemoryCopyGuard.ReportRejectedCopy(nameof(QuestStateManager));
                    }
                }
            }

            uint schemaVersion = header.ReadSchemaVersion();
            bool headerMatchesPackedLayout =
                header.Magic == QuestSaveHeader.HeaderMagic &&
                header.FlagCount == WordCapacity;
            bool schemaRecognized = schemaVersion == 0u || schemaVersion == QuestSaveHeader.CurrentSchemaVersion;

            _stateVersion = headerMatchesPackedLayout ? header.Version : 0u;
            _stateChecksum = headerMatchesPackedLayout && schemaRecognized ? header.Checksum : 0u;
            if (_stateVersion == 0u || _stateChecksum == 0u)
                RefreshStateMetadata(resetVersion: false);
        }

        public void RestoreLegacyState(IEnumerable<string> activeQuestIds, IEnumerable<string> completedQuestIds)
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

        private void RestoreLegacyRange(IEnumerable<string> questIds, bool completed)
        {
            if (questIds == null)
                return;

            foreach (string questId in questIds)
            {
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
                    RegisterCompileError($"Quest '{questData.questId}' references unknown prerequisite quest '{prerequisiteQuestId}'.");
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

            QuestBitAddress address = RegisterStateBit(payloadHash, band, $"{signalKind}:{signalId}:{signalValue}", hashLabels, bandBitUsage);
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
                    RegisterCompileError($"Quest bit collision between '{existingLabel}' and '{debugLabel}' at 0x{bitHash:X8}.");
                }

                return existingAddress;
            }

            int bandIndex = (int)band;
            int bandBitIndex = bandBitUsage[bandIndex];
            int bandCapacity = GetBandWordCount(band) * WordStride;
            if (bandBitIndex >= bandCapacity)
            {
                RegisterCompileError($"Quest state band '{band}' exceeded its {bandCapacity} bit ceiling.");
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

        private void RegisterCompileError(string message)
        {
            if (string.IsNullOrEmpty(_compileErrorSummary))
            {
                _compileErrorSummary = message;
                return;
            }

            _compileErrorSummary += System.Environment.NewLine + message;
        }

        [Conditional("DEVELOPMENT_BUILD")]
        private void AppendTransitionAudit(int questIndex, bool completed, QuestTransitionType transitionType, QuestSignalPayload signal)
        {
            if (questIndex < 0 ||
                _questHashesByQuestIndex == null ||
                questIndex >= _questHashesByQuestIndex.Length)
            {
                return;
            }

            string state;
            switch (transitionType)
            {
                case QuestTransitionType.Activate:
                    state = completed ? "Complete" : "Active";
                    break;

                case QuestTransitionType.Complete:
                    state = "Complete";
                    break;

                case QuestTransitionType.Revert:
                    state = "Revert";
                    break;

                default:
                    state = completed ? "Complete" : "Active";
                    break;
            }

            try
            {
                double timestamp = signal.Timestamp > 0d ? signal.Timestamp : Time.timeAsDouble;
                string path = Path.Combine(Application.persistentDataPath, QuestAuditLogFileName);
                File.AppendAllText(
                    path,
                    $"[{timestamp:F3}] Quest 0x{_questHashesByQuestIndex[questIndex]:X8} -> {state}\n");
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[QuestStateManager] Quest audit append failed: {exception.Message}");
            }
        }

        private bool TrySetResolvedBit(uint bitHash)
        {
            return bitHash != 0u &&
                   _bitAddressByHash != null &&
                   _bitAddressByHash.TryGetValue(bitHash, out QuestBitAddress address) &&
                   SetBit(address);
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

            ComputePackedStateChecksumJob checksumJob = new ComputePackedStateChecksumJob
            {
                GlobalPrerequisites = _globalPrerequisites,
                Result = _checksumResult
            };
            JobHandle checksumHandle = checksumJob.Schedule();
            checksumHandle.Complete();
            _stateChecksum = _checksumResult[0];
        }

        private static uint ResolveCriticalItemHash(QuestData questData)
        {
            if (questData == null)
                return 0u;

            if (!string.IsNullOrWhiteSpace(questData.criticalItemId))
                return QuestFlagHashKernel.ComputeStableHash(questData.criticalItemId);

            return questData.completionType == QuestCompletionType.OnItemCollected
                ? QuestFlagHashKernel.ComputeStableHash(questData.completionId)
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
                case QuestSignalKind.DiscoveryMade:
                case QuestSignalKind.AudioLogFound:
                case QuestSignalKind.SignalDecoded:
                    return QuestFlagHashKernel.ComputeStableHash(signalId);

                case QuestSignalKind.BiomeEntered:
                case QuestSignalKind.DepthReached:
                    return ComputeNumericSignalHash(signalKind, signalValue);

                case QuestSignalKind.EclipseStarted:
                    return EclipseFlagHash;

                default:
                    return 0u;
            }
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

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private struct ComputePackedStateChecksumJob : IJob
        {
            [ReadOnly] public NativeArray<uint> GlobalPrerequisites;
            [WriteOnly] public NativeArray<uint> Result;

            public void Execute()
            {
                unchecked
                {
                    uint hash = Hecton.Localization.LocHash.FnvOffsetBasis;
                    for (int i = 0; i < GlobalPrerequisites.Length; i++)
                    {
                        uint word = GlobalPrerequisites[i];
                        hash ^= word & 0xFFu;
                        hash *= Hecton.Localization.LocHash.FnvPrime;
                        hash ^= (word >> 8) & 0xFFu;
                        hash *= Hecton.Localization.LocHash.FnvPrime;
                        hash ^= (word >> 16) & 0xFFu;
                        hash *= Hecton.Localization.LocHash.FnvPrime;
                        hash ^= (word >> 24) & 0xFFu;
                        hash *= Hecton.Localization.LocHash.FnvPrime;
                    }

                    Result[0] = hash;
                }
            }
        }

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
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
