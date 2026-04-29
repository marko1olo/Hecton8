using System;
using System.Collections.Generic;
using Hecton.Localization;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Quest
{
    internal enum QuestSignalKind : byte
    {
        None = 0,
        ItemCollected = 1,
        DepthReached = 2,
        BiomeEntered = 3,
        DiscoveryMade = 4,
        AudioLogFound = 5,
        EclipseStarted = 6,
        SignalDecoded = 7
    }

    internal enum QuestStateBand : byte
    {
        Quest = 0,
        Item = 1,
        Location = 2,
        Narrative = 3,
        Phase = 4,
        EntityDestroy = 5,
        Deadlock = 6
    }

    internal struct QuestSignal
    {
        public QuestSignal(QuestSignalKind kind, uint payloadHash, float numericValue)
        {
            Kind = kind;
            PayloadHash = payloadHash;
            NumericValue = numericValue;
        }

        public QuestSignalKind Kind;
        public uint PayloadHash;
        public float NumericValue;
    }

    internal struct QuestBitAddress
    {
        public int WordIndex;
        public uint BitMask;
    }

    internal struct QuestPrerequisiteDescriptor
    {
        public int StateWordIndex;
        public uint RequiredMask;
    }

    internal struct QuestNodeDescriptor
    {
        public uint QuestHash;
        public uint PayloadHash;
        public float RequiredValue;
        public int PrereqStartIndex;
        public int QuestIndex;
        public int ActiveWordIndex;
        public int CompletedWordIndex;
        public int SetWordIndex;
        public int ClearWordIndex;
        public uint ActiveMask;
        public uint CompletedMask;
        public uint SetMask;
        public uint ClearMask;
        public byte PrereqCount;
        public byte SignalKind;
        public byte TransitionType;
        public byte Reserved;
    }

    internal readonly struct QuestRuntimeResult
    {
        public QuestRuntimeResult(int questIndex, bool completed)
        {
            QuestIndex = questIndex;
            Completed = completed;
        }

        public int QuestIndex { get; }
        public bool Completed { get; }
    }

    internal struct QuestTransitionHistoryEntry
    {
        public uint Sequence;
        public uint QuestHash;
        public uint SignalPayloadHash;
        public float SignalNumericValue;
        public int SnapshotWordOffset;
        public int FrameIndex;
        public byte Completed;
        public byte SignalKind;
        public ushort Reserved;
    }

    internal sealed class QuestStateManager : IDisposable
    {
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
        private const int TransitionHistoryCapacity = 256;
        private const uint ActiveFlagSalt = 0xA11F0A11u;
        private const uint CompletedFlagSalt = 0xC0DE0C01u;
        private const uint BiomeFlagSalt = 0xB10F0001u;
        private const uint DepthFlagSalt = 0xD37A0001u;
        private const uint EclipseFlagHash = 0xE011C1E5u;

        private readonly List<QuestRuntimeResult> _runtimeResults = new List<QuestRuntimeResult>(32); // COLD ALLOC: List<QuestRuntimeResult>[32] — transition handoff from native job to facade — owner: QuestStateManager
        private NativeArray<uint> _globalPrerequisites;
        private NativeArray<QuestNodeDescriptor> _nodes;
        private NativeArray<QuestPrerequisiteDescriptor> _prerequisites;
        private NativeList<int> _activatedQuestIndices;
        private NativeList<int> _completedQuestIndices;
        private Dictionary<uint, QuestBitAddress> _bitAddressByHash;
        private Dictionary<uint, int> _questIndexByHash;
        private QuestBitAddress[] _activeAddressesByQuestIndex;
        private QuestBitAddress[] _completedAddressesByQuestIndex;
        private uint[] _questHashesByQuestIndex;
        private ThresholdFlag[] _depthThresholdFlags;
        private NativeArray<QuestTransitionHistoryEntry> _transitionHistory;
        private NativeArray<uint> _transitionHistoryWords;
        private int _transitionHistoryWriteIndex;
        private int _transitionHistoryCount;
        private uint _transitionSequence;
        private string _compileErrorSummary = string.Empty;
        private bool _isInitialized;

        public bool HasCompileErrors => !string.IsNullOrEmpty(_compileErrorSummary);

        public string CompileErrorSummary => _compileErrorSummary;

        public int WordCount => WordCapacity;

        public void Dispose()
        {
            if (_activatedQuestIndices.IsCreated)
                _activatedQuestIndices.Dispose();

            if (_completedQuestIndices.IsCreated)
                _completedQuestIndices.Dispose();

            if (_nodes.IsCreated)
                _nodes.Dispose();

            if (_prerequisites.IsCreated)
                _prerequisites.Dispose();

            if (_globalPrerequisites.IsCreated)
                _globalPrerequisites.Dispose();

            if (_transitionHistory.IsCreated)
                _transitionHistory.Dispose();

            if (_transitionHistoryWords.IsCreated)
                _transitionHistoryWords.Dispose();

            _runtimeResults.Clear();
            _bitAddressByHash = null;
            _questIndexByHash = null;
            _activeAddressesByQuestIndex = null;
            _completedAddressesByQuestIndex = null;
            _questHashesByQuestIndex = null;
            _depthThresholdFlags = null;
            _transitionHistoryWriteIndex = 0;
            _transitionHistoryCount = 0;
            _transitionSequence = 0u;
            _compileErrorSummary = string.Empty;
            _isInitialized = false;
        }

        public bool Initialize(QuestData[] allQuests)
        {
            Dispose();

            int questArrayLength = allQuests != null ? allQuests.Length : 0;
            _globalPrerequisites = new NativeArray<uint>(WordCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            _bitAddressByHash = new Dictionary<uint, QuestBitAddress>(Math.Max(questArrayLength * 4, 16)); // COLD ALLOC: Dictionary<uint,QuestBitAddress>[questArrayLength*4] — precompiled quest hash lookup — owner: QuestStateManager
            _questIndexByHash = new Dictionary<uint, int>(Math.Max(questArrayLength, 16)); // COLD ALLOC: Dictionary<uint,int>[questArrayLength] — quest hash to source index mapping — owner: QuestStateManager
            _activeAddressesByQuestIndex = new QuestBitAddress[questArrayLength]; // COLD ALLOC: QuestBitAddress[questArrayLength] — quest active bit cache — owner: QuestStateManager
            _completedAddressesByQuestIndex = new QuestBitAddress[questArrayLength]; // COLD ALLOC: QuestBitAddress[questArrayLength] — quest completed bit cache — owner: QuestStateManager
            _questHashesByQuestIndex = new uint[questArrayLength]; // COLD ALLOC: uint[questArrayLength] — quest hash cache for rollback history — owner: QuestStateManager

            // COLD ALLOC: List<QuestNodeDescriptor>[questArrayLength*2] — compiled quest state graph nodes — owner: QuestStateManager
            List<QuestNodeDescriptor> nodeBuilder = new List<QuestNodeDescriptor>(Math.Max(questArrayLength * 2, 8));
            // COLD ALLOC: List<QuestPrerequisiteDescriptor>[questArrayLength] — flattened prerequisite mask table — owner: QuestStateManager
            List<QuestPrerequisiteDescriptor> prerequisiteBuilder = new List<QuestPrerequisiteDescriptor>(Math.Max(questArrayLength, 8));
            // COLD ALLOC: List<ThresholdFlag>[32] — unique depth threshold bit addresses — owner: QuestStateManager
            List<ThresholdFlag> depthFlags = new List<ThresholdFlag>(32);
            // COLD ALLOC: Dictionary<uint,string>[questArrayLength*4] — collision diagnostics for precompiled hashes — owner: QuestStateManager
            Dictionary<uint, string> hashLabels = new Dictionary<uint, string>(Math.Max(questArrayLength * 4, 16));
            Span<int> bandBitUsage = stackalloc int[7];

            for (int questIndex = 0; questIndex < questArrayLength; questIndex++)
            {
                QuestData questData = allQuests[questIndex];
                if (questData == null || string.IsNullOrWhiteSpace(questData.questId))
                    continue;

                uint questHash = unchecked((uint)LocHash.Compute(questData.questId));
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

                RegisterTriggerStateBit(questData.triggerType, questData.triggerId, questData.triggerValue, hashLabels, bandBitUsage, depthFlags);
                RegisterCompletionStateBit(questData.completionType, questData.completionId, questData.completionValue, hashLabels, bandBitUsage, depthFlags);

                QuestSignalKind activationSignalKind = MapTriggerSignalKind(questData.triggerType);
                if (activationSignalKind != QuestSignalKind.None)
                {
                    nodeBuilder.Add(new QuestNodeDescriptor
                    {
                        QuestHash = questHash,
                        PayloadHash = ResolveSignalPayloadHash(activationSignalKind, questData.triggerId, questData.triggerValue),
                        RequiredValue = questData.triggerValue,
                        PrereqStartIndex = prerequisiteBuilder.Count,
                        QuestIndex = questIndex,
                        ActiveWordIndex = _activeAddressesByQuestIndex[questIndex].WordIndex,
                        CompletedWordIndex = _completedAddressesByQuestIndex[questIndex].WordIndex,
                        SetWordIndex = _activeAddressesByQuestIndex[questIndex].WordIndex,
                        ClearWordIndex = -1,
                        ActiveMask = _activeAddressesByQuestIndex[questIndex].BitMask,
                        CompletedMask = _completedAddressesByQuestIndex[questIndex].BitMask,
                        SetMask = _activeAddressesByQuestIndex[questIndex].BitMask,
                        ClearMask = 0u,
                        PrereqCount = 0,
                        SignalKind = (byte)activationSignalKind,
                        TransitionType = 0,
                        Reserved = 0
                    });
                }

                QuestSignalKind completionSignalKind = MapCompletionSignalKind(questData.completionType);
                if (completionSignalKind != QuestSignalKind.None)
                {
                    prerequisiteBuilder.Add(new QuestPrerequisiteDescriptor
                    {
                        StateWordIndex = _activeAddressesByQuestIndex[questIndex].WordIndex,
                        RequiredMask = _activeAddressesByQuestIndex[questIndex].BitMask
                    });

                    nodeBuilder.Add(new QuestNodeDescriptor
                    {
                        QuestHash = questHash,
                        PayloadHash = ResolveSignalPayloadHash(completionSignalKind, questData.completionId, questData.completionValue),
                        RequiredValue = questData.completionValue,
                        PrereqStartIndex = prerequisiteBuilder.Count - 1,
                        QuestIndex = questIndex,
                        ActiveWordIndex = _activeAddressesByQuestIndex[questIndex].WordIndex,
                        CompletedWordIndex = _completedAddressesByQuestIndex[questIndex].WordIndex,
                        SetWordIndex = _completedAddressesByQuestIndex[questIndex].WordIndex,
                        ClearWordIndex = _activeAddressesByQuestIndex[questIndex].WordIndex,
                        ActiveMask = _activeAddressesByQuestIndex[questIndex].BitMask,
                        CompletedMask = _completedAddressesByQuestIndex[questIndex].BitMask,
                        SetMask = _completedAddressesByQuestIndex[questIndex].BitMask,
                        ClearMask = _activeAddressesByQuestIndex[questIndex].BitMask,
                        PrereqCount = 1,
                        SignalKind = (byte)completionSignalKind,
                        TransitionType = 1,
                        Reserved = 0
                    });
                }
            }

            _nodes = new NativeArray<QuestNodeDescriptor>(nodeBuilder.Count, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            for (int i = 0; i < nodeBuilder.Count; i++)
                _nodes[i] = nodeBuilder[i];

            _prerequisites = new NativeArray<QuestPrerequisiteDescriptor>(prerequisiteBuilder.Count, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            for (int i = 0; i < prerequisiteBuilder.Count; i++)
                _prerequisites[i] = prerequisiteBuilder[i];

            if (_runtimeResults.Capacity < nodeBuilder.Count)
                _runtimeResults.Capacity = nodeBuilder.Count;

            _activatedQuestIndices = new NativeList<int>(Math.Max(nodeBuilder.Count, 1), Allocator.Persistent);
            _completedQuestIndices = new NativeList<int>(Math.Max(nodeBuilder.Count, 1), Allocator.Persistent);
            _transitionHistory = new NativeArray<QuestTransitionHistoryEntry>(TransitionHistoryCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            _transitionHistoryWords = new NativeArray<uint>(TransitionHistoryCapacity * WordCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            _depthThresholdFlags = depthFlags.ToArray();
            _isInitialized = true;
            return !HasCompileErrors;
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

        public bool TryActivateQuest(uint questHash, out int questIndex)
        {
            questIndex = -1;
            if (!TryGetQuestIndex(questHash, out questIndex))
                return false;

            QuestBitAddress activeAddress = _activeAddressesByQuestIndex[questIndex];
            QuestBitAddress completedAddress = _completedAddressesByQuestIndex[questIndex];
            if (IsBitSet(activeAddress) || IsBitSet(completedAddress))
                return false;

            return SetBit(activeAddress);
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
            return true;
        }

        public void ApplyAutoActivationFlags(QuestData[] allQuests)
        {
            _runtimeResults.Clear();
            if (!_isInitialized || allQuests == null)
                return;

            for (int questIndex = 0; questIndex < allQuests.Length; questIndex++)
            {
                QuestData questData = allQuests[questIndex];
                if (questData == null || !questData.autoActivateOnStart || string.IsNullOrWhiteSpace(questData.questId))
                    continue;

                uint questHash = unchecked((uint)LocHash.Compute(questData.questId));
                if (TryActivateQuest(questHash, out int activatedQuestIndex))
                {
                    _runtimeResults.Add(new QuestRuntimeResult(activatedQuestIndex, completed: false));
                    AppendTransitionHistory(activatedQuestIndex, completed: false, default);
                }
            }
        }

        public void EvaluateSignal(QuestSignal signal)
        {
            _runtimeResults.Clear();
            if (!_isInitialized || !_nodes.IsCreated)
                return;

            ApplyPersistentSignalState(signal);
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
            job.Run();

            for (int i = 0; i < _activatedQuestIndices.Length; i++)
            {
                int questIndex = _activatedQuestIndices[i];
                _runtimeResults.Add(new QuestRuntimeResult(questIndex, completed: false));
                AppendTransitionHistory(questIndex, completed: false, signal);
            }

            for (int i = 0; i < _completedQuestIndices.Length; i++)
            {
                int questIndex = _completedQuestIndices[i];
                _runtimeResults.Add(new QuestRuntimeResult(questIndex, completed: true));
                AppendTransitionHistory(questIndex, completed: true, signal);
            }
        }

        public int ResultCount => _runtimeResults.Count;

        public int TransitionHistoryCount => math.min(_transitionHistoryCount, TransitionHistoryCapacity);

        public QuestRuntimeResult GetResult(int index) => _runtimeResults[index];

        public bool TryGetTransitionHistory(int newestHistoryOffset, out QuestTransitionHistoryEntry entry)
        {
            entry = default;
            if (!_transitionHistory.IsCreated || newestHistoryOffset < 0 || newestHistoryOffset >= TransitionHistoryCount)
                return false;

            entry = _transitionHistory[ResolveTransitionHistorySlot(newestHistoryOffset)];
            return true;
        }

        public NativeArray<uint> CapturePackedStateSnapshot(Allocator allocator)
        {
            NativeArray<uint> snapshot = new NativeArray<uint>(WordCapacity, allocator, NativeArrayOptions.ClearMemory);
            if (!_globalPrerequisites.IsCreated)
                return snapshot;

            unsafe
            {
                void* sourcePtr = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(_globalPrerequisites);
                void* destinationPtr = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(snapshot);
                UnsafeUtility.MemCpy(destinationPtr, sourcePtr, WordCapacity * UnsafeUtility.SizeOf<uint>());
            }

            return snapshot;
        }

        public void RestorePackedState(uint[] packedWords)
        {
            if (!_globalPrerequisites.IsCreated)
                return;

            ClearTransitionHistory();
            unsafe
            {
                UnsafeUtility.MemClear(
                    NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(_globalPrerequisites),
                    WordCapacity * UnsafeUtility.SizeOf<uint>());
            }

            if (packedWords == null || packedWords.Length <= 0)
                return;

            int copyWordCount = Math.Min(packedWords.Length, WordCapacity);
            unsafe
            {
                fixed (uint* sourcePtr = packedWords)
                {
                    void* destinationPtr = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(_globalPrerequisites);
                    UnsafeUtility.MemCpy(destinationPtr, sourcePtr, copyWordCount * UnsafeUtility.SizeOf<uint>());
                }
            }
        }

        public void RestoreLegacyState(IEnumerable<string> activeQuestIds, IEnumerable<string> completedQuestIds)
        {
            if (!_globalPrerequisites.IsCreated)
                return;

            ClearTransitionHistory();
            unsafe
            {
                void* destinationPtr = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(_globalPrerequisites);
                UnsafeUtility.MemClear(destinationPtr, WordCapacity * UnsafeUtility.SizeOf<uint>());
            }

            RestoreLegacyRange(activeQuestIds, completed: false);
            RestoreLegacyRange(completedQuestIds, completed: true);
        }

        public bool TryRestoreTransitionHistory(int newestHistoryOffset)
        {
            if (!_globalPrerequisites.IsCreated ||
                !_transitionHistory.IsCreated ||
                !_transitionHistoryWords.IsCreated ||
                newestHistoryOffset < 0 ||
                newestHistoryOffset >= TransitionHistoryCount)
            {
                return false;
            }

            QuestTransitionHistoryEntry entry = _transitionHistory[ResolveTransitionHistorySlot(newestHistoryOffset)];
            if (entry.SnapshotWordOffset < 0 || entry.SnapshotWordOffset + WordCapacity > _transitionHistoryWords.Length)
                return false;

            unsafe
            {
                void* destinationPtr = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(_globalPrerequisites);
                void* sourcePtr = (byte*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(_transitionHistoryWords) + (entry.SnapshotWordOffset * UnsafeUtility.SizeOf<uint>());
                UnsafeUtility.MemCpy(destinationPtr, sourcePtr, WordCapacity * UnsafeUtility.SizeOf<uint>());
            }

            return true;
        }

        public void RecordManualTransition(int questIndex, bool completed)
        {
            AppendTransitionHistory(questIndex, completed, default);
        }

        private void RestoreLegacyRange(IEnumerable<string> questIds, bool completed)
        {
            if (questIds == null)
                return;

            foreach (string questId in questIds)
            {
                if (string.IsNullOrWhiteSpace(questId))
                    continue;

                uint questHash = unchecked((uint)LocHash.Compute(questId));
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

        private void ApplyPersistentSignalState(QuestSignal signal)
        {
            switch (signal.Kind)
            {
                case QuestSignalKind.ItemCollected:
                case QuestSignalKind.DiscoveryMade:
                case QuestSignalKind.AudioLogFound:
                case QuestSignalKind.SignalDecoded:
                    TrySetResolvedBit(signal.PayloadHash);
                    break;

                case QuestSignalKind.BiomeEntered:
                    TrySetResolvedBit(ComputeNumericSignalHash(QuestSignalKind.BiomeEntered, signal.NumericValue));
                    break;

                case QuestSignalKind.DepthReached:
                    ApplyDepthThresholdFlags(signal.NumericValue);
                    break;

                case QuestSignalKind.EclipseStarted:
                    TrySetResolvedBit(EclipseFlagHash);
                    break;
            }
        }

        private void ApplyDepthThresholdFlags(float depth)
        {
            if (_depthThresholdFlags == null)
                return;

            for (int i = 0; i < _depthThresholdFlags.Length; i++)
            {
                ThresholdFlag flag = _depthThresholdFlags[i];
                if (depth < flag.Threshold)
                    continue;

                SetBit(flag.Address);
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
            QuestSignalKind signalKind = MapTriggerSignalKind(triggerType);
            RegisterSignalStateBit(signalKind, triggerId, triggerValue, hashLabels, bandBitUsage, depthFlags);
        }

        private void RegisterCompletionStateBit(
            QuestCompletionType completionType,
            string completionId,
            float completionValue,
            Dictionary<uint, string> hashLabels,
            Span<int> bandBitUsage,
            List<ThresholdFlag> depthFlags)
        {
            QuestSignalKind signalKind = MapCompletionSignalKind(completionType);
            RegisterSignalStateBit(signalKind, completionId, completionValue, hashLabels, bandBitUsage, depthFlags);
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
                BitMask = 1u << (bandBitIndex & 0x1F)
            };

            bandBitUsage[bandIndex] = bandBitIndex + 1;
            _bitAddressByHash.Add(bitHash, address);
            hashLabels[bitHash] = debugLabel;
            return address;
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

        private void AppendTransitionHistory(int questIndex, bool completed, in QuestSignal signal)
        {
            if (!_globalPrerequisites.IsCreated ||
                !_transitionHistory.IsCreated ||
                !_transitionHistoryWords.IsCreated ||
                questIndex < 0 ||
                _questHashesByQuestIndex == null ||
                questIndex >= _questHashesByQuestIndex.Length)
            {
                return;
            }

            int slot = _transitionHistoryWriteIndex;
            int snapshotWordOffset = slot * WordCapacity;
            unsafe
            {
                void* destinationPtr = (byte*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(_transitionHistoryWords) + (snapshotWordOffset * UnsafeUtility.SizeOf<uint>());
                void* sourcePtr = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(_globalPrerequisites);
                UnsafeUtility.MemCpy(destinationPtr, sourcePtr, WordCapacity * UnsafeUtility.SizeOf<uint>());
            }

            _transitionHistory[slot] = new QuestTransitionHistoryEntry
            {
                Sequence = ++_transitionSequence,
                QuestHash = _questHashesByQuestIndex[questIndex],
                SignalPayloadHash = signal.PayloadHash,
                SignalNumericValue = signal.NumericValue,
                SnapshotWordOffset = snapshotWordOffset,
                FrameIndex = Time.frameCount,
                Completed = completed ? (byte)1 : (byte)0,
                SignalKind = (byte)signal.Kind,
                Reserved = 0
            };

            _transitionHistoryWriteIndex = (_transitionHistoryWriteIndex + 1) % TransitionHistoryCapacity;
            if (_transitionHistoryCount < TransitionHistoryCapacity)
                _transitionHistoryCount++;
        }

        private void ClearTransitionHistory()
        {
            _transitionHistoryWriteIndex = 0;
            _transitionHistoryCount = 0;
            _transitionSequence = 0u;

            if (_transitionHistory.IsCreated)
            {
                unsafe
                {
                    UnsafeUtility.MemClear(
                        NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(_transitionHistory),
                        _transitionHistory.Length * UnsafeUtility.SizeOf<QuestTransitionHistoryEntry>());
                }
            }

            if (_transitionHistoryWords.IsCreated)
            {
                unsafe
                {
                    UnsafeUtility.MemClear(
                        NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(_transitionHistoryWords),
                        _transitionHistoryWords.Length * UnsafeUtility.SizeOf<uint>());
                }
            }
        }

        private int ResolveTransitionHistorySlot(int newestHistoryOffset)
        {
            int slot = _transitionHistoryWriteIndex - 1 - newestHistoryOffset;
            while (slot < 0)
                slot += TransitionHistoryCapacity;

            return slot;
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

        private void ClearBit(QuestBitAddress address)
        {
            if (!_globalPrerequisites.IsCreated || address.WordIndex < 0 || address.WordIndex >= _globalPrerequisites.Length)
                return;

            _globalPrerequisites[address.WordIndex] &= ~address.BitMask;
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
                    return string.IsNullOrWhiteSpace(signalId)
                        ? 0u
                        : unchecked((uint)LocHash.Compute(signalId));

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
                uint hash = (uint)LocHash.FnvOffsetBasis;
                hash ^= (uint)signalKind;
                hash *= LocHash.FnvPrime;

                uint valueBits = (uint)BitConverter.SingleToInt32Bits(numericValue);
                hash ^= valueBits & 0xFFu;
                hash *= LocHash.FnvPrime;
                hash ^= (valueBits >> 8) & 0xFFu;
                hash *= LocHash.FnvPrime;
                hash ^= (valueBits >> 16) & 0xFFu;
                hash *= LocHash.FnvPrime;
                hash ^= (valueBits >> 24) & 0xFFu;
                hash *= LocHash.FnvPrime;
                hash ^= signalKind == QuestSignalKind.BiomeEntered ? BiomeFlagSalt : DepthFlagSalt;
                hash *= LocHash.FnvPrime;
                return hash;
            }
        }

        private static uint MixHash(uint sourceHash, uint salt)
        {
            unchecked
            {
                uint mixed = sourceHash ^ salt;
                mixed *= LocHash.FnvPrime;
                mixed ^= salt >> 8;
                mixed *= LocHash.FnvPrime;
                return mixed;
            }
        }

        private struct ThresholdFlag
        {
            public float Threshold;
            public QuestBitAddress Address;
        }

        [BurstCompile(FloatMode = FloatMode.Fast)]
        private struct EvaluateQuestSignalJob : IJob
        {
            public QuestSignal Signal;
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
                    if (node.SignalKind != (byte)Signal.Kind)
                        continue;

                    if (!MatchesSignal(node, Signal))
                        continue;

                    if (!PrerequisitesSatisfied(node, Prerequisites, GlobalPrerequisites))
                        continue;

                    if (node.TransitionType == 0)
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
                for (int prerequisiteIndex = 0; prerequisiteIndex < node.PrereqCount; prerequisiteIndex++)
                {
                    QuestPrerequisiteDescriptor prerequisite = prerequisites[node.PrereqStartIndex + prerequisiteIndex];
                    if ((globalPrerequisites[prerequisite.StateWordIndex] & prerequisite.RequiredMask) != prerequisite.RequiredMask)
                        return false;
                }

                return true;
            }

            private static bool MatchesSignal(QuestNodeDescriptor node, QuestSignal signal)
            {
                switch ((QuestSignalKind)node.SignalKind)
                {
                    case QuestSignalKind.ItemCollected:
                        if (node.PayloadHash != 0u && node.PayloadHash != signal.PayloadHash)
                            return false;

                        return node.RequiredValue <= 0f || signal.NumericValue >= node.RequiredValue;

                    case QuestSignalKind.DepthReached:
                        return signal.NumericValue >= node.RequiredValue;

                    case QuestSignalKind.BiomeEntered:
                        return (int)signal.NumericValue == (int)node.RequiredValue;

                    case QuestSignalKind.DiscoveryMade:
                    case QuestSignalKind.AudioLogFound:
                    case QuestSignalKind.SignalDecoded:
                        return node.PayloadHash == 0u || node.PayloadHash == signal.PayloadHash;

                    case QuestSignalKind.EclipseStarted:
                        return true;

                    default:
                        return false;
                }
            }
        }
    }
}
