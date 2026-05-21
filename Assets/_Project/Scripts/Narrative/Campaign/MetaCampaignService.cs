using System;
using System.IO;
using System.Runtime.InteropServices;
using Hecton8.Core;
using Hecton8.Core.Contracts.Signals;
using Hecton8.SaveSystem;
using Hecton8.World;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Narrative.Campaign
{
    [StructLayout(LayoutKind.Explicit, Size = 16)]
    internal struct MetaCampaignRule
    {
        [FieldOffset(0)]
        public uint TriggerHash;
        [FieldOffset(4)]
        public uint VariableHash;
        [FieldOffset(8)]
        public int Value;
        [FieldOffset(12)]
        public byte MatchMode;
        [FieldOffset(13)]
        public byte SideEffectFlags;
        [FieldOffset(14)]
        public ushort Reserved;
    }

    [StructLayout(LayoutKind.Explicit, Size = 16)]
    internal struct MetaCampaignVariableChange
    {
        [FieldOffset(0)]
        public uint VariableHash;
        [FieldOffset(4)]
        public int Value;
        [FieldOffset(8)]
        public byte SideEffectFlags;
        [FieldOffset(9)]
        public byte Reserved0;
        [FieldOffset(10)]
        public ushort Reserved1;
        [FieldOffset(12)]
        private uint _pad0;
    }

    [StructLayout(LayoutKind.Explicit, Size = 128)]
    internal struct MetaCampaignEvaluationResult
    {
        [FieldOffset(0)]
        public FixedList128Bytes<MetaCampaignVariableChange> Changes;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    internal struct MetaCampaignBlackBoxEntry
    {
        [FieldOffset(0)]
        public uint Frame;
        [FieldOffset(4)]
        public uint StageHash;
        [FieldOffset(8)]
        public uint VariableHash;
        [FieldOffset(12)]
        public int Value;
        [FieldOffset(16)]
        public float Toxicity01;
        [FieldOffset(20)]
        public byte ChangeKind;
        [FieldOffset(21)]
        public byte Flags;
        [FieldOffset(22)]
        public ushort Sequence;
        [FieldOffset(24)]
        private ulong _pad0;
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    internal struct MetaCampaignRuleEvaluationJob : IJob
    {
        public ProgressionEventSignal Signal;
        [NoAlias, ReadOnly]
        public NativeArray<MetaCampaignRule> Rules;
        [ReadOnly]
        public NativeParallelHashMap<uint, int> Variables;
        [NoAlias]
        public NativeArray<MetaCampaignEvaluationResult> Output;

        public void Execute()
        {
            MetaCampaignEvaluationResult result = default;
            for (int i = 0; i < Rules.Length; i++)
            {
                MetaCampaignRule rule = Rules[i];
                if (!Matches(rule, Signal))
                    continue;

                if (Variables.TryGetValue(rule.VariableHash, out int existing) && existing == rule.Value)
                    continue;

                TryAppendChange(ref result, rule.VariableHash, rule.Value, rule.SideEffectFlags);
            }

            Output[0] = result;
        }

        private static bool Matches(in MetaCampaignRule rule, in ProgressionEventSignal signal)
        {
            if (rule.TriggerHash == 0u || rule.VariableHash == 0u)
                return false;

            switch (rule.MatchMode)
            {
                case MetaCampaignService.RuleMatchPoi:
                    return signal.PoiHash == rule.TriggerHash;
                case MetaCampaignService.RuleMatchQuest:
                    return signal.QuestHash == rule.TriggerHash;
                default:
                    return signal.PoiHash == rule.TriggerHash || signal.QuestHash == rule.TriggerHash;
            }
        }

        private static void TryAppendChange(
            ref MetaCampaignEvaluationResult result,
            uint variableHash,
            int value,
            byte sideEffectFlags)
        {
            for (int i = 0; i < result.Changes.Length; i++)
            {
                MetaCampaignVariableChange existing = result.Changes[i];
                if (existing.VariableHash != variableHash)
                    continue;

                existing.Value = value;
                existing.SideEffectFlags |= sideEffectFlags;
                result.Changes[i] = existing;
                return;
            }

            if (result.Changes.Length >= result.Changes.Capacity)
                return;

            result.Changes.Add(new MetaCampaignVariableChange
            {
                VariableHash = variableHash,
                Value = value,
                SideEffectFlags = sideEffectFlags
            });
        }
    }

    /// <summary>
    /// Registry-bound global campaign DAG. It consumes progression signals and publishes state deltas without scene singletons.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/Narrative/Meta Campaign Service")]
    public sealed class MetaCampaignService : MonoBehaviour, IMetaCampaignService, IUpdatable, ILateFrameTickable, ISaveable, IServiceHeartbeat, IServiceShutdown
    {
        public const uint ToxicityLevelHash = 0x903D9D8Eu;
        public const uint LeviathanAwakenedHash = 0x2B00DC54u;
        public const uint BaseDeltaDestroyedHash = 0x46BAFD85u;
        public const uint CampaignStageHash = 0xB792FC5Fu;
        public const uint CartographyCorruptionPoiHash = 0xBB42BB83u;
        public const uint VwsToxicityBroadcastHash = 0x370C7AFCu;

        internal const byte RuleMatchAny = 0;
        internal const byte RuleMatchPoi = 1;
        internal const byte RuleMatchQuest = 2;

        private const string NativeMemoryOwner = nameof(MetaCampaignService);
        private const string DumpRelativePath = "Docs/AgentLogs/Dump_META_CAMPAIGN_DIRECTOR.bin";
        private const int GlobalVariableCapacity = MetaCampaignDTO.MaxGlobalVariables;
        private const int RuleCapacity = 5;
        private const int BlackBoxCapacity = 300;
        private const float ToxicityValueMax = 4f;
        private const float InvToxicityValueMax = 1f / ToxicityValueMax;
        private const uint AgentHash = 0x18E7D58Cu;
        private const uint ServiceHash = 0xAA625239u;
        private const uint StageHashMultiplier = 0x9E3779B9u;
        private const NativeAllocationLifetime NativeMemoryLifetime = NativeAllocationLifetime.Session;
        private const Allocator DataVaultExemptOwnerIndexAllocator = Allocator.Persistent;
        private static readonly int _HectonOceanToxicityId = Shader.PropertyToID("_HectonOceanToxicity");

        private NativeParallelHashMap<uint, int> _globalVariables;
        private NativeParallelHashMap<uint, int> _queryVariables;
        private NativeArray<MetaCampaignRule> _rules;
        private NativeArray<MetaCampaignEvaluationResult> _evaluationOutput;
        private NativeArray<MetaCampaignBlackBoxEntry> _blackBox;
        private JobHandle _evaluationHandle;
        private bool _evaluationPending;
        private bool _serviceRegistered;
        private bool _serviceReady;
        private bool _saveRuntimeRegistered;
        private bool _updatableRegistered;
        private bool _lateFrameRegistered;
        private bool _shutdown;
        private int _tickCount;
        private int _currentStage;
        private uint _currentStageHash;
        private float _toxicity01;
        private int _toxicityLevel;
        private int _baseDeltaDestroyed;
        private byte _leviathanAwakened;
        private int _blackBoxCursor;
        private ushort _sequence;

        public bool IsInitialized =>
            _globalVariables.IsCreated &&
            _queryVariables.IsCreated &&
            _rules.IsCreated &&
            _evaluationOutput.IsCreated &&
            _blackBox.IsCreated;

        public uint CurrentCampaignStageHash => _currentStageHash;

        public int CurrentCampaignStage => _currentStage;

        public float OceanToxicity01 => _toxicity01;

        public bool IsLeviathanAwakened => _leviathanAwakened != 0;

        public ServiceHeartbeatState HeartbeatState =>
            _shutdown ? ServiceHeartbeatState.Shutdown :
            IsServiceReady ? ServiceHeartbeatState.Ready :
            ServiceHeartbeatState.NotStarted;

        public bool IsServiceReady => _serviceReady && IsInitialized;

        public int TickCount => _tickCount;

        public int SavePriority => 6;

        public int LoadPriority => 6;

        private void Awake()
        {
            AllocateRuntimeState();
            SeedDefaultState();
        }

        private void OnEnable()
        {
            _shutdown = false;
            AllocateRuntimeState();
            EnsureDefaultVariables();
            RefreshCachedStateFromVariables();
            TryRegisterService();
            if (!_serviceRegistered)
                return;

            TryRegisterSaveRuntime();
            PublishCachedVisualState(GlobalWorldStateSignal.ChangeKindLoad, (uint)Time.frameCount);
        }

        private void Start()
        {
            TryRegisterService();
            if (_serviceRegistered)
                TryRegisterSaveRuntime();
        }

        private void OnDisable()
        {
            ShutdownServiceState();
        }

        private void OnDestroy()
        {
            ShutdownServiceState();
        }

        public void OnServiceShutdown()
        {
            ShutdownServiceState();
        }

        public void Tick(float deltaTime)
        {
            if (!IsServiceReady)
                return;

            _tickCount++;
            if (_evaluationPending)
                return;

            if (!GlobalSignals.TryDequeueProgressionEvent(out ProgressionEventSignal signal))
                return;

            _evaluationOutput[0] = default;
            MetaCampaignRuleEvaluationJob job = new MetaCampaignRuleEvaluationJob
            {
                Signal = signal,
                Rules = _rules,
                Variables = _globalVariables,
                Output = _evaluationOutput
            };
            _evaluationHandle = job.Schedule();
            _evaluationPending = true;
        }

        public void LateFrameTick()
        {
            if (!_evaluationPending || !_evaluationHandle.IsCompleted)
                return;

            CompletePendingEvaluation();
        }

        public bool TryGetGlobalVariable(uint variableHash, out int value)
        {
            value = 0;
            if (!IsInitialized || variableHash == 0u)
                return false;

            return _queryVariables.TryGetValue(variableHash, out value);
        }

        public bool TryForceSetGlobalVariable(uint variableHash, int value, byte reason)
        {
            if (!IsInitialized || variableHash == 0u)
                return false;

            CompletePendingEvaluation();
            if (_queryVariables.TryGetValue(variableHash, out int existing) && existing == value)
                return true;

            ApplyGlobalVariableChange(
                variableHash,
                value,
                reason != 0 ? reason : GlobalWorldStateSignal.ChangeKindDevConsole,
                ResolveSideEffectFlags(variableHash),
                (uint)Time.frameCount);
            return true;
        }

        public void PopulateSaveData(SaveData data)
        {
            if (data == null || !IsInitialized)
                return;

            CompletePendingEvaluation();
            MetaCampaignDTO dto = data.metaCampaign;
            dto.EnsureCapacity();
            dto.currentStage = _currentStage;
            dto.currentStageHash = _currentStageHash;
            dto.toxicityPermille = (int)math.round(math.saturate(_toxicity01) * 1000f);
            dto.flags = _leviathanAwakened;
            dto.variableCount = 0;

            NativeKeyValueArrays<uint, int> pairs = _globalVariables.GetKeyValueArrays(Allocator.Temp);
            try
            {
                int count = math.min(pairs.Length, MetaCampaignDTO.MaxGlobalVariables);
                for (int i = 0; i < count; i++)
                {
                    dto.variableHashes[i] = pairs.Keys[i];
                    dto.variableValues[i] = pairs.Values[i];
                }

                dto.variableCount = count;
            }
            finally
            {
                pairs.Dispose();
            }

            SortMetaCampaignVariables(ref dto);
            data.metaCampaign = dto;
        }

        public void LoadFromSaveData(SaveData data)
        {
            if (!IsInitialized)
                AllocateRuntimeState();

            CompletePendingEvaluation();
            ClearGlobalVariables();

            if (data == null)
            {
                ResetDefaultState(GlobalWorldStateSignal.ChangeKindLoad);
                return;
            }

            MetaCampaignDTO dto = data.metaCampaign;
            dto.EnsureCapacity();
            int count = math.clamp(dto.variableCount, 0, math.min(dto.variableHashes.Length, dto.variableValues.Length));
            for (int i = 0; i < count; i++)
            {
                uint variableHash = dto.variableHashes[i];
                if (variableHash == 0u)
                    continue;

                UpsertGlobalVariable(variableHash, dto.variableValues[i]);
            }

            data.metaCampaign = dto;
            EnsureDefaultVariables();
            RefreshCachedStateFromVariables();
            PublishCampaignStateSnapshot(
                GlobalWorldStateSignal.ChangeKindLoad,
                (byte)(GlobalWorldStateSignal.FlagVisualRefresh | GlobalWorldStateSignal.FlagCartographyRefresh),
                (uint)Time.frameCount);
        }

        private void AllocateRuntimeState()
        {
            if (IsInitialized)
                return;

            if (!_globalVariables.IsCreated)
            {
                _globalVariables = new NativeParallelHashMap<uint, int>(GlobalVariableCapacity, DataVaultExemptOwnerIndexAllocator);
                NativeMemorySentinel.RegisterNativeParallelHashMap(_globalVariables, NativeMemoryOwner, nameof(_globalVariables), NativeMemoryLifetime);
            }

            if (!_queryVariables.IsCreated)
            {
                _queryVariables = new NativeParallelHashMap<uint, int>(GlobalVariableCapacity, DataVaultExemptOwnerIndexAllocator);
                NativeMemorySentinel.RegisterNativeParallelHashMap(_queryVariables, NativeMemoryOwner, nameof(_queryVariables), NativeMemoryLifetime);
            }

            if (!_rules.IsCreated)
            {
                _rules = new NativeArray<MetaCampaignRule>(RuleCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
                NativeMemorySentinel.RegisterNativeArray(_rules, NativeMemoryOwner, nameof(_rules), NativeMemoryLifetime);
                BuildRules();
            }

            if (!_evaluationOutput.IsCreated)
            {
                _evaluationOutput = new NativeArray<MetaCampaignEvaluationResult>(1, Allocator.Persistent, NativeArrayOptions.ClearMemory);
                NativeMemorySentinel.RegisterNativeArray(_evaluationOutput, NativeMemoryOwner, nameof(_evaluationOutput), NativeMemoryLifetime);
            }

            if (!_blackBox.IsCreated)
            {
                _blackBox = new NativeArray<MetaCampaignBlackBoxEntry>(BlackBoxCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
                NativeMemorySentinel.RegisterNativeArray(_blackBox, NativeMemoryOwner, nameof(_blackBox), NativeMemoryLifetime);
            }
        }

        private void BuildRules()
        {
            byte fullShiftFlags = (byte)(GlobalWorldStateSignal.FlagVisualRefresh |
                                         GlobalWorldStateSignal.FlagAudioBroadcast |
                                         GlobalWorldStateSignal.FlagCartographyRefresh);

            _rules[0] = CreateRule(BaseDeltaDestroyedHash, BaseDeltaDestroyedHash, 1, fullShiftFlags);
            _rules[1] = CreateRule(BaseDeltaDestroyedHash, ToxicityLevelHash, 2, fullShiftFlags);
            _rules[2] = CreateRule(BaseDeltaDestroyedHash, CampaignStageHash, 1, fullShiftFlags);
            _rules[3] = CreateRule(LeviathanAwakenedHash, LeviathanAwakenedHash, 1, GlobalWorldStateSignal.FlagAudioBroadcast);
            _rules[4] = CreateRule(LeviathanAwakenedHash, CampaignStageHash, 2, fullShiftFlags);
        }

        private static MetaCampaignRule CreateRule(uint triggerHash, uint variableHash, int value, byte sideEffectFlags)
        {
            return new MetaCampaignRule
            {
                TriggerHash = triggerHash,
                VariableHash = variableHash,
                Value = value,
                MatchMode = RuleMatchAny,
                SideEffectFlags = sideEffectFlags
            };
        }

        private void TryRegisterService()
        {
            if (!Application.isPlaying)
                return;

            if (!_serviceRegistered)
            {
                IMetaCampaignService registered = GlobalRegistry.MetaCampaign;
                if (registered != null && !ReferenceEquals(registered, this))
                {
                    Destroy(this);
                    return;
                }

                GlobalRegistry.RegisterMetaCampaignService(this);
                _serviceRegistered = ReferenceEquals(GlobalRegistry.MetaCampaign, this);
                if (!_serviceRegistered)
                    return;
            }

            if (!_updatableRegistered)
                _updatableRegistered = GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.Core);
            if (!_lateFrameRegistered)
                _lateFrameRegistered = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Core);
            _serviceReady = _updatableRegistered && _lateFrameRegistered;
        }

        private void TryRegisterSaveRuntime()
        {
            if (_saveRuntimeRegistered)
                return;

            SaveManager saveRuntime = GlobalRegistry.SaveRuntime;
            if (saveRuntime == null)
                return;

            saveRuntime.Register(this);
            _saveRuntimeRegistered = true;
        }

        private void ShutdownServiceState()
        {
            if (_shutdown)
                return;

            if (_saveRuntimeRegistered)
            {
                SaveManager saveRuntime = GlobalRegistry.SaveRuntime;
                if (saveRuntime != null)
                    saveRuntime.Unregister(this);
                _saveRuntimeRegistered = false;
            }

            if (_updatableRegistered)
            {
                GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Core);
                _updatableRegistered = false;
            }

            if (_lateFrameRegistered)
            {
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Core);
                _lateFrameRegistered = false;
            }

            if (_serviceRegistered)
            {
                GlobalRegistry.UnregisterMetaCampaignService(this);
                _serviceRegistered = false;
            }

            _serviceReady = false;
            DisposeRuntimeState(_evaluationPending ? _evaluationHandle : default);
            _evaluationPending = false;
            _evaluationHandle = default;
            _shutdown = true;
        }

        private void DisposeRuntimeState(JobHandle dependency)
        {
            if (_globalVariables.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeParallelHashMap(NativeMemoryOwner, nameof(_globalVariables));
                _globalVariables.Dispose(dependency);
                _globalVariables = default;
            }

            if (_queryVariables.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeParallelHashMap(NativeMemoryOwner, nameof(_queryVariables));
                _queryVariables.Dispose(dependency);
                _queryVariables = default;
            }

            DisposeNativeArray(ref _rules, dependency);
            DisposeNativeArray(ref _evaluationOutput, dependency);
            DisposeNativeArray(ref _blackBox, dependency);
        }

        private static void DisposeNativeArray<T>(ref NativeArray<T> array, JobHandle dependency) where T : struct
        {
            if (!array.IsCreated)
                return;

            NativeMemorySentinel.UnregisterNativeArray(array);
            array.Dispose(dependency);
            array = default;
        }

        private void CompletePendingEvaluation()
        {
            if (!_evaluationPending)
                return;

            if (!DispatcherJobFence.TryFinalizeCompleted(ref _evaluationHandle))
                return;

            _evaluationPending = false;
            MetaCampaignEvaluationResult result = _evaluationOutput.IsCreated ? _evaluationOutput[0] : default;
            int changeCount = result.Changes.Length;
            if (changeCount <= 0)
                return;

            for (int i = 0; i < changeCount; i++)
            {
                MetaCampaignVariableChange change = result.Changes[i];
                UpsertGlobalVariable(change.VariableHash, change.Value);
            }

            RefreshCachedStateFromVariables();
            uint frame = (uint)Time.frameCount;
            byte aggregateSideEffectFlags = 0;
            uint broadcastVariableHash = 0u;
            for (int i = 0; i < changeCount; i++)
            {
                MetaCampaignVariableChange change = result.Changes[i];
                aggregateSideEffectFlags |= change.SideEffectFlags;
                broadcastVariableHash = SelectBroadcastVariable(broadcastVariableHash, change.VariableHash);
                PublishGlobalVariableSignal(
                    change.VariableHash,
                    change.Value,
                    GlobalWorldStateSignal.ChangeKindRule,
                    change.SideEffectFlags,
                    frame);
            }

            PublishStateSideEffects(
                GlobalWorldStateSignal.ChangeKindRule,
                aggregateSideEffectFlags,
                broadcastVariableHash,
                frame);
        }

        private void SeedDefaultState()
        {
            if (!IsInitialized)
                return;

            ClearGlobalVariables();
            UpsertGlobalVariable(CampaignStageHash, 0);
            UpsertGlobalVariable(ToxicityLevelHash, 0);
            UpsertGlobalVariable(LeviathanAwakenedHash, 0);
            UpsertGlobalVariable(BaseDeltaDestroyedHash, 0);
            RefreshCachedStateFromVariables();
        }

        private void ResetDefaultState(byte changeKind)
        {
            SeedDefaultState();
            PublishCampaignStateSnapshot(
                changeKind,
                (byte)(GlobalWorldStateSignal.FlagVisualRefresh | GlobalWorldStateSignal.FlagCartographyRefresh),
                (uint)Time.frameCount);
        }

        private void EnsureDefaultVariables()
        {
            EnsureGlobalVariable(CampaignStageHash, 0);
            EnsureGlobalVariable(ToxicityLevelHash, 0);
            EnsureGlobalVariable(LeviathanAwakenedHash, 0);
            EnsureGlobalVariable(BaseDeltaDestroyedHash, 0);
        }

        private void ApplyGlobalVariableChange(
            uint variableHash,
            int value,
            byte changeKind,
            byte sideEffectFlags,
            uint frame)
        {
            UpsertGlobalVariable(variableHash, value);
            RefreshCachedStateFromVariables();
            PublishGlobalVariableSignal(variableHash, value, changeKind, sideEffectFlags, frame);
            PublishStateSideEffects(changeKind, sideEffectFlags, variableHash, frame);
        }

        private void PublishGlobalVariableSignal(
            uint variableHash,
            int value,
            byte changeKind,
            byte sideEffectFlags,
            uint frame)
        {
            sideEffectFlags |= GlobalWorldStateSignal.FlagAupIndependent;
            _sequence++;
            GlobalSignals.Publish(new GlobalWorldStateSignal
            {
                PositionAup = default,
                VariableHash = variableHash,
                Value = value,
                StageHash = _currentStageHash,
                ChangeKind = changeKind,
                Flags = sideEffectFlags,
                Sequence = _sequence
            });

            WriteBlackBox(frame, variableHash, value, changeKind, sideEffectFlags);
        }

        private void PublishStateSideEffects(
            byte changeKind,
            byte sideEffectFlags,
            uint broadcastVariableHash,
            uint frame)
        {
            if ((sideEffectFlags & GlobalWorldStateSignal.FlagVisualRefresh) != 0)
                PublishCachedVisualState(changeKind, frame);

            if ((sideEffectFlags & GlobalWorldStateSignal.FlagAudioBroadcast) != 0)
                PublishCampaignBroadcast(broadcastVariableHash);

            if ((sideEffectFlags & GlobalWorldStateSignal.FlagCartographyRefresh) != 0)
                PublishCartographyState(frame);

            GlobalTelemetryBus.PublishModTelemetry(AgentHash, _currentStageHash, _toxicity01);
        }

        private void PublishCachedVisualState(byte changeKind, uint frame)
        {
            if (!math.isfinite(_toxicity01))
            {
                DumpBlackBox();
                _toxicity01 = 0f;
            }

            Shader.SetGlobalFloat(_HectonOceanToxicityId, _toxicity01);
            IEcosystemDirectorService ecosystemDirector = GlobalRegistry.EcosystemDirector;
            if (ecosystemDirector != null)
                ecosystemDirector.ApplyCampaignToxicityPressure(_toxicity01, _currentStageHash, frame);

            WriteBlackBox(frame, ToxicityLevelHash, (int)math.round(_toxicity01 * ToxicityValueMax), changeKind, GlobalWorldStateSignal.FlagVisualRefresh);
        }

        private void PublishCampaignBroadcast(uint variableHash)
        {
            float severity01 = math.max(0.1f, _toxicity01);
            GlobalSignals.Publish(new VocalWarningSignal
            {
                WarningHash = VocalWarningHashes.Radiation,
                SourceId = variableHash != 0u ? variableHash : VwsToxicityBroadcastHash,
                Severity01 = severity01,
                CooldownSeconds = 30f,
                Priority = (byte)VocalWarningId.Radiation,
                Flags = VocalWarningSignalFlags.HabitatIntegrityCompromised
            });
        }

        private void PublishCampaignStateSnapshot(byte changeKind, byte sideEffectFlags, uint frame)
        {
            sideEffectFlags |= GlobalWorldStateSignal.FlagAupIndependent;
            _sequence++;
            GlobalSignals.Publish(new GlobalWorldStateSignal
            {
                PositionAup = default,
                VariableHash = CampaignStageHash,
                Value = _currentStage,
                StageHash = _currentStageHash,
                ChangeKind = changeKind,
                Flags = sideEffectFlags,
                Sequence = _sequence
            });

            PublishStateSideEffects(changeKind, sideEffectFlags, CampaignStageHash, frame);
            WriteBlackBox(frame, CampaignStageHash, _currentStage, changeKind, sideEffectFlags);
        }

        private void PublishCartographyState(uint frame)
        {
            GlobalSignals.Publish(new NarrativePoiStateSignal
            {
                StateMask = ((ulong)_currentStageHash << 32) | (uint)math.clamp(_currentStage, 0, int.MaxValue),
                PoiHash = CartographyCorruptionPoiHash,
                Frame = frame,
                PoiIndex = 0,
                Operation = _toxicity01 > 0f || _currentStage > 0 ? (byte)1 : (byte)0,
                Flags = GlobalWorldStateSignal.FlagCartographyRefresh
            });
        }

        private void RefreshCachedStateFromVariables()
        {
            _currentStage = ResolveVariable(CampaignStageHash, 0);
            _currentStageHash = ResolveStageHash(_currentStage);
            _toxicityLevel = ResolveVariable(ToxicityLevelHash, 0);
            _toxicity01 = math.saturate(_toxicityLevel * InvToxicityValueMax);
            _leviathanAwakened = ResolveVariable(LeviathanAwakenedHash, 0) > 0 ? (byte)1 : (byte)0;
            _baseDeltaDestroyed = ResolveVariable(BaseDeltaDestroyedHash, 0);
        }

        private int ResolveVariable(uint variableHash, int fallback)
        {
            return _globalVariables.TryGetValue(variableHash, out int value) ? value : fallback;
        }

        private void UpsertGlobalVariable(uint variableHash, int value)
        {
            if (variableHash == 0u || !IsInitialized)
                return;

            if (!_globalVariables.TryAdd(variableHash, value))
                _globalVariables[variableHash] = value;

            if (!_queryVariables.TryAdd(variableHash, value))
                _queryVariables[variableHash] = value;
        }

        private void EnsureGlobalVariable(uint variableHash, int fallback)
        {
            int value = fallback;
            if (_globalVariables.TryGetValue(variableHash, out int existing))
                value = existing;

            UpsertGlobalVariable(variableHash, value);
        }

        private void ClearGlobalVariables()
        {
            if (_globalVariables.IsCreated)
                _globalVariables.Clear();
            if (_queryVariables.IsCreated)
                _queryVariables.Clear();
        }

        private static byte ResolveSideEffectFlags(uint variableHash)
        {
            byte flags = GlobalWorldStateSignal.FlagAudioBroadcast;
            if (variableHash == ToxicityLevelHash || variableHash == CampaignStageHash)
                flags |= GlobalWorldStateSignal.FlagVisualRefresh | GlobalWorldStateSignal.FlagCartographyRefresh;
            return flags;
        }

        private static uint SelectBroadcastVariable(uint currentHash, uint candidateHash)
        {
            if (candidateHash == 0u)
                return currentHash;

            if (candidateHash == LeviathanAwakenedHash || currentHash == 0u)
                return candidateHash;

            if (currentHash == LeviathanAwakenedHash)
                return currentHash;

            if (candidateHash == ToxicityLevelHash)
                return candidateHash;

            return currentHash;
        }

        private static uint ResolveStageHash(int stage)
        {
            return unchecked(CampaignStageHash ^ ((uint)math.max(0, stage) * StageHashMultiplier));
        }

        private static void SortMetaCampaignVariables(ref MetaCampaignDTO dto)
        {
            int count = math.clamp(
                dto.variableCount,
                0,
                math.min(MetaCampaignDTO.MaxGlobalVariables, math.min(dto.variableHashes.Length, dto.variableValues.Length)));
            dto.variableCount = count;

            for (int i = 1; i < count; i++)
            {
                uint hash = dto.variableHashes[i];
                int value = dto.variableValues[i];
                int j = i - 1;
                while (j >= 0 && dto.variableHashes[j] > hash)
                {
                    dto.variableHashes[j + 1] = dto.variableHashes[j];
                    dto.variableValues[j + 1] = dto.variableValues[j];
                    j--;
                }

                dto.variableHashes[j + 1] = hash;
                dto.variableValues[j + 1] = value;
            }
        }

        private void WriteBlackBox(uint frame, uint variableHash, int value, byte changeKind, byte flags)
        {
            if (!_blackBox.IsCreated)
                return;

            if (!math.isfinite(_toxicity01))
            {
                DumpBlackBox();
                return;
            }

            int slot = _blackBoxCursor % BlackBoxCapacity;
            _blackBoxCursor = (_blackBoxCursor + 1) % BlackBoxCapacity;
            _blackBox[slot] = new MetaCampaignBlackBoxEntry
            {
                Frame = frame,
                StageHash = _currentStageHash,
                VariableHash = variableHash,
                Value = value,
                Toxicity01 = _toxicity01,
                ChangeKind = changeKind,
                Flags = flags,
                Sequence = _sequence
            };
        }

        private void DumpBlackBox()
        {
            if (!_blackBox.IsCreated)
                return;

            try
            {
                string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
                string dumpPath = Path.Combine(projectRoot, DumpRelativePath);
                string directory = Path.GetDirectoryName(dumpPath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                    Directory.CreateDirectory(directory);

                using (FileStream stream = new FileStream(dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read))
                using (BinaryWriter writer = new BinaryWriter(stream))
                {
                    writer.Write(0x4D43424Cu);
                    writer.Write(ServiceHash);
                    writer.Write(_blackBoxCursor);
                    writer.Write(_blackBox.Length);
                    for (int i = 0; i < _blackBox.Length; i++)
                    {
                        MetaCampaignBlackBoxEntry entry = _blackBox[i];
                        writer.Write(entry.Frame);
                        writer.Write(entry.StageHash);
                        writer.Write(entry.VariableHash);
                        writer.Write(entry.Value);
                        writer.Write(entry.Toxicity01);
                        writer.Write(entry.ChangeKind);
                        writer.Write(entry.Flags);
                        writer.Write(entry.Sequence);
                    }
                }
            }
            catch (Exception exception)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogError("MetaCampaignService blackbox dump failed: " + exception.Message);
#endif
            }
        }
    }

    /// <summary>
    /// Hidden developer hook for scripted diagnostics. It is intentionally not exposed through UI.
    /// </summary>
    public static class MetaCampaignDevConsole
    {
        public static bool TryForceSetGlobal(uint variableHash, int value)
        {
            IMetaCampaignService service = GlobalRegistry.MetaCampaign;
            return service != null &&
                   service.TryForceSetGlobalVariable(variableHash, value, GlobalWorldStateSignal.ChangeKindDevConsole);
        }
    }
}
