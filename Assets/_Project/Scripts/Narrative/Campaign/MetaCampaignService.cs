using System;
using System.IO;
using System.Runtime.InteropServices;
using Hecton8.Core;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Memory;
using Hecton8.SaveSystem;
using Hecton8.World;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
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

    [StructLayout(LayoutKind.Explicit, Size = 8)]
    internal struct MetaCampaignVariableSlot
    {
        [FieldOffset(0)]
        public uint VariableHash;
        [FieldOffset(4)]
        public int Value;
    }

    [StructLayout(LayoutKind.Explicit, Size = 128)]
    internal struct MetaCampaignEvaluationResult
    {
        [FieldOffset(0)]
        public FixedList128Bytes<MetaCampaignVariableChange> Changes;
    }

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Explicit, Size = 64)]
    internal struct MetaCampaignBlackBoxEntry
    {
        [System.Runtime.InteropServices.FieldOffset(0)]
        public uint Frame;
        [System.Runtime.InteropServices.FieldOffset(4)]
        public uint StageHash;
        [System.Runtime.InteropServices.FieldOffset(8)]
        public uint VariableHash;
        [System.Runtime.InteropServices.FieldOffset(12)]
        public int Value;
        [System.Runtime.InteropServices.FieldOffset(16)]
        public float Toxicity01;
        [System.Runtime.InteropServices.FieldOffset(20)]
        public ushort Sequence;
        [System.Runtime.InteropServices.FieldOffset(22)]
        public byte ChangeKind;
        [System.Runtime.InteropServices.FieldOffset(23)]
        public byte Flags;
        [System.Runtime.InteropServices.FieldOffset(24)]
        private byte _pad0;
        [System.Runtime.InteropServices.FieldOffset(25)]
        private byte _pad1;
        [System.Runtime.InteropServices.FieldOffset(26)]
        private byte _pad2;
        [System.Runtime.InteropServices.FieldOffset(27)]
        private byte _pad3;
        [System.Runtime.InteropServices.FieldOffset(28)]
        private byte _pad4;
        [System.Runtime.InteropServices.FieldOffset(29)]
        private byte _pad5;
        [System.Runtime.InteropServices.FieldOffset(30)]
        private byte _pad6;
        [System.Runtime.InteropServices.FieldOffset(31)]
        private byte _pad7;
        [System.Runtime.InteropServices.FieldOffset(32)]
        private byte _pad8;
        [System.Runtime.InteropServices.FieldOffset(33)]
        private byte _pad9;
        [System.Runtime.InteropServices.FieldOffset(34)]
        private byte _pad10;
        [System.Runtime.InteropServices.FieldOffset(35)]
        private byte _pad11;
        [System.Runtime.InteropServices.FieldOffset(36)]
        private byte _pad12;
        [System.Runtime.InteropServices.FieldOffset(37)]
        private byte _pad13;
        [System.Runtime.InteropServices.FieldOffset(38)]
        private byte _pad14;
        [System.Runtime.InteropServices.FieldOffset(39)]
        private byte _pad15;
        [System.Runtime.InteropServices.FieldOffset(40)]
        private byte _pad16;
        [System.Runtime.InteropServices.FieldOffset(41)]
        private byte _pad17;
        [System.Runtime.InteropServices.FieldOffset(42)]
        private byte _pad18;
        [System.Runtime.InteropServices.FieldOffset(43)]
        private byte _pad19;
        [System.Runtime.InteropServices.FieldOffset(44)]
        private byte _pad20;
        [System.Runtime.InteropServices.FieldOffset(45)]
        private byte _pad21;
        [System.Runtime.InteropServices.FieldOffset(46)]
        private byte _pad22;
        [System.Runtime.InteropServices.FieldOffset(47)]
        private byte _pad23;
        [System.Runtime.InteropServices.FieldOffset(48)]
        private byte _pad24;
        [System.Runtime.InteropServices.FieldOffset(49)]
        private byte _pad25;
        [System.Runtime.InteropServices.FieldOffset(50)]
        private byte _pad26;
        [System.Runtime.InteropServices.FieldOffset(51)]
        private byte _pad27;
        [System.Runtime.InteropServices.FieldOffset(52)]
        private byte _pad28;
        [System.Runtime.InteropServices.FieldOffset(53)]
        private byte _pad29;
        [System.Runtime.InteropServices.FieldOffset(54)]
        private byte _pad30;
        [System.Runtime.InteropServices.FieldOffset(55)]
        private byte _pad31;
        [System.Runtime.InteropServices.FieldOffset(56)]
        private byte _pad32;
        [System.Runtime.InteropServices.FieldOffset(57)]
        private byte _pad33;
        [System.Runtime.InteropServices.FieldOffset(58)]
        private byte _pad34;
        [System.Runtime.InteropServices.FieldOffset(59)]
        private byte _pad35;
        [System.Runtime.InteropServices.FieldOffset(60)]
        private byte _pad36;
        [System.Runtime.InteropServices.FieldOffset(61)]
        private byte _pad37;
        [System.Runtime.InteropServices.FieldOffset(62)]
        private byte _pad38;
        [System.Runtime.InteropServices.FieldOffset(63)]
        private byte _pad39;
    }

    /// <summary>
    /// Registry-bound global campaign DAG. It consumes progression signals and publishes state deltas without scene singletons.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/Narrative/Meta Campaign Service")]
    public sealed class MetaCampaignService : MonoBehaviour, IMetaCampaignService, IUpdatable, ILateFrameTickable, ISaveable, IServiceHeartbeat, IServiceShutdown, IGlobalRegistryHotSwapListener
    {
        private static int s_x001MetaCampaignServiceSignalPushDropCount;
        public const uint ToxicityLevelHash = 0x903D9D8Eu;
        public const uint LeviathanAwakenedHash = 0x2B00DC54u;
        public const uint BaseDeltaDestroyedHash = 0x46BAFD85u;
        public const uint CampaignStageHash = 0xB792FC5Fu;
        public const uint CartographyCorruptionPoiHash = 0xBB42BB83u;
        public const uint VwsToxicityBroadcastHash = 0x370C7AFCu;

        internal const byte RuleMatchAny = 0;
        internal const byte RuleMatchPoi = 1;
        internal const byte RuleMatchQuest = 2;

        private const string DumpRelativePath = "Docs/AgentLogs/Dump_META_CAMPAIGN_DIRECTOR.bin";
        private const int GlobalVariableCapacity = MetaCampaignDTO.MaxGlobalVariables;
        private const int RuleCapacity = 5;
        private const int BlackBoxCapacity = 300;
        private const float ToxicityValueMax = 4f;
        private const float InvToxicityValueMax = 1f / ToxicityValueMax;
        private const uint AgentHash = 0x18E7D58Cu;
        private const uint ServiceHash = 0xAA625239u;
        private const uint StageHashMultiplier = 0x9E3779B9u;
        private const SystemID VaultOwnerSystemId = SystemID.MetaCampaign;
        private const BufferID VariablesBufferId = BufferID.MetaCampaignVariables;
        private const BufferID RulesBufferId = BufferID.MetaCampaignRules;
        private const BufferID BlackBoxBufferId = BufferID.MetaCampaignBlackBox;
        private static readonly int _HectonOceanToxicityId = Shader.PropertyToID("_HectonOceanToxicity");

        private VaultGenerationHandle<MetaCampaignVariableSlot> _variablesHandle;
        private VaultGenerationHandle<MetaCampaignRule> _rulesHandle;
        private VaultGenerationHandle<MetaCampaignBlackBoxEntry> _blackBoxHandle;
        private IDataVault _dataVault;
        private MetaCampaignEvaluationResult _pendingEvaluationResult;
        private bool _evaluationPending;
        private bool _serviceRegistered;
        private bool _serviceReady;
        private bool _visualStateDirty;
        private bool _audioBroadcastDirty;
        private bool _cartographyStateDirty;
        private bool _saveServiceRegistered;
        private bool _registeredHotSwapListener;
        private bool _runtimeOwnerAborted;
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
        private byte _pendingVisualChangeKind;
        private uint _pendingAudioBroadcastVariableHash;
        private uint _pendingCartographyFrame;
        private int _blackBoxCursor;
        private uint _pendingVisualFrame;
        private ushort _sequence;
        private ISaveService _saveService;
        private ISaveService _registeredSaveService;
        private IEcosystemDirectorService _ecosystemDirector;

        public bool IsInitialized =>
            _dataVault != null &&
            IsExactVaultHandle(in _variablesHandle, VariablesBufferId) &&
            IsExactVaultHandle(in _rulesHandle, RulesBufferId) &&
            IsExactVaultHandle(in _blackBoxHandle, BlackBoxBufferId);

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
            if (!TryRegisterService())
                return;

            AllocateRuntimeState();
            SeedDefaultState();
        }

        private void OnEnable()
        {
            _shutdown = false;
            if (!TryRegisterService())
                return;

            AllocateRuntimeState();
            EnsureDefaultVariables();
            RefreshCachedStateFromVariables();
            TryRegisterTickLanes();

            _saveService = GlobalRegistry.Save;
            _ecosystemDirector = GlobalRegistry.EcosystemDirector;
            TryRegisterHotSwapListener();
            TryRegisterSaveService();
            QueueCachedVisualState(
                GlobalWorldStateSignal.ChangeKindLoad,
                (uint)Hecton8.Core.SystemDispatcher.CurrentFrameIndex);
        }

        private void Start()
        {
            if (!TryRegisterService())
                return;

            TryRegisterTickLanes();
            if (_serviceRegistered)
            {
                if (!IsSaveServiceUsable(_saveService))
                    _saveService = GlobalRegistry.Save;
                if (_ecosystemDirector == null)
                    _ecosystemDirector = GlobalRegistry.EcosystemDirector;
                TryRegisterHotSwapListener();
                TryRegisterSaveService();
            }
        }

        private void OnDisable()
        {
            if (_runtimeOwnerAborted)
                return;

            ShutdownServiceState();
        }

        private void OnDestroy()
        {
            if (_runtimeOwnerAborted)
                return;

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

            if (!SignalBus<ProgressionEventSignal>.TryConsumeFrame(out ProgressionEventSignal signal))
                return;

            if (!TryReadRules(out NativeArray<MetaCampaignRule>.ReadOnly rules) ||
                !TryReadVariables(out NativeArray<MetaCampaignVariableSlot>.ReadOnly variables))
            {
                return;
            }

            _pendingEvaluationResult = EvaluateRules(in signal, rules, variables);
            _evaluationPending = _pendingEvaluationResult.Changes.Length > 0;
        }

        public void LateFrameTick()
        {
            if (_evaluationPending)
                CompletePendingEvaluation();

            FlushCachedVisualState();
            FlushCampaignBroadcast();
            FlushCartographyState();
        }

        public bool TryGetGlobalVariable(uint variableHash, out int value)
        {
            value = 0;
            if (!IsInitialized || variableHash == 0u)
                return false;

            return TryFindVariableValue(variableHash, out value);
        }

        public bool TryForceSetGlobalVariable(uint variableHash, int value, byte reason)
        {
            if (!IsInitialized || variableHash == 0u)
                return false;

            CompletePendingEvaluation();
            if (TryFindVariableValue(variableHash, out int existing) && existing == value)
                return true;

            return ApplyGlobalVariableChange(
                variableHash,
                value,
                reason != 0 ? reason : GlobalWorldStateSignal.ChangeKindDevConsole,
                ResolveSideEffectFlags(variableHash),
                (uint)Hecton8.Core.SystemDispatcher.CurrentFrameIndex);
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

            if (TryReadVariables(out NativeArray<MetaCampaignVariableSlot>.ReadOnly variables))
            {
                int count = 0;
                for (int i = 0; i < variables.Length && count < MetaCampaignDTO.MaxGlobalVariables; i++)
                {
                    MetaCampaignVariableSlot slot = variables[i];
                    if (slot.VariableHash == 0u)
                        continue;

                    dto.variableHashes[count] = slot.VariableHash;
                    dto.variableValues[count] = slot.Value;
                    count++;
                }

                dto.variableCount = count;
            }

            SortMetaCampaignVariables(ref dto);
            data.metaCampaign = dto;
        }

        public void LoadFromSaveData(SaveData data)
        {
            if (!IsInitialized)
                AllocateRuntimeState();

            CompletePendingEvaluation();

            if (data == null)
            {
                ResetDefaultState(GlobalWorldStateSignal.ChangeKindLoad);
                return;
            }

            MetaCampaignDTO dto = data.metaCampaign;
            dto.EnsureCapacity();
            if (!TryReplaceGlobalVariablesFromSave(ref dto))
                return;

            data.metaCampaign = dto;
            RefreshCachedStateFromVariables();
            PublishCampaignStateSnapshot(
                GlobalWorldStateSignal.ChangeKindLoad,
                (byte)(GlobalWorldStateSignal.FlagVisualRefresh | GlobalWorldStateSignal.FlagCartographyRefresh),
                (uint)Hecton8.Core.SystemDispatcher.CurrentFrameIndex);
        }

        private void AllocateRuntimeState()
        {
            if (IsInitialized)
                return;

            IDataVault vault = CacheDataVaultCold();
            if (vault == null)
                return;

            bool variablesReady = EnsureVaultBuffer(
                ref _variablesHandle,
                VariablesBufferId,
                GlobalVariableCapacity,
                NativeArrayOptions.ClearMemory);
            bool rulesReady = EnsureVaultBuffer(
                ref _rulesHandle,
                RulesBufferId,
                RuleCapacity,
                NativeArrayOptions.ClearMemory);
            bool blackBoxReady = EnsureVaultBuffer(
                ref _blackBoxHandle,
                BlackBoxBufferId,
                BlackBoxCapacity,
                NativeArrayOptions.ClearMemory);

            if (variablesReady && rulesReady && blackBoxReady)
                BuildRules();
        }

        private void BuildRules()
        {
            if (!TryAcquireRulesWrite(out NativeArray<MetaCampaignRule> rules, out IDataVault lockedVault))
                return;

            byte fullShiftFlags = (byte)(GlobalWorldStateSignal.FlagVisualRefresh |
                                         GlobalWorldStateSignal.FlagAudioBroadcast |
                                         GlobalWorldStateSignal.FlagCartographyRefresh);

            try
            {
                rules[0] = CreateRule(BaseDeltaDestroyedHash, BaseDeltaDestroyedHash, 1, fullShiftFlags);
                rules[1] = CreateRule(BaseDeltaDestroyedHash, ToxicityLevelHash, 2, fullShiftFlags);
                rules[2] = CreateRule(BaseDeltaDestroyedHash, CampaignStageHash, 1, fullShiftFlags);
                rules[3] = CreateRule(LeviathanAwakenedHash, LeviathanAwakenedHash, 1, GlobalWorldStateSignal.FlagAudioBroadcast);
                rules[4] = CreateRule(LeviathanAwakenedHash, CampaignStageHash, 2, fullShiftFlags);
            }
            finally
            {
                ReleaseRulesWrite(lockedVault);
            }
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

        private bool TryRegisterService()
        {
            if (_runtimeOwnerAborted)
                return false;

            if (!Application.isPlaying)
                return true;

            if (!_serviceRegistered)
            {
                if (TryAbortForUsableExistingRuntime())
                    return false;

                IMetaCampaignService registered = GlobalRegistry.MetaCampaign;
                if (!ReferenceEquals(registered, null) && !ReferenceEquals(registered, this))
                {
                    MetaCampaignService staleService = registered as MetaCampaignService;
                    if (ReferenceEquals(staleService, null))
                    {
                        _runtimeOwnerAborted = true;
                        Destroy(this);
                        return false;
                    }

                    GlobalRegistry.UnregisterMetaCampaignService(registered);
                    staleService._serviceRegistered = false;
                    staleService._serviceReady = false;
                }

                if (TryAbortForUsableExistingRuntime())
                    return false;

                GlobalRegistry.RegisterMetaCampaignService(this);
                _serviceRegistered = ReferenceEquals(GlobalRegistry.MetaCampaign, this);
                if (!_serviceRegistered)
                {
                    _runtimeOwnerAborted = true;
                    Destroy(this);
                    return false;
                }
            }

            _runtimeOwnerAborted = false;
            return true;
        }

        private void TryRegisterTickLanes()
        {
            if (!_serviceRegistered || _runtimeOwnerAborted || _shutdown)
                return;

            if (!_updatableRegistered)
                _updatableRegistered = GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.Core);
            if (!_lateFrameRegistered)
                _lateFrameRegistered = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Core);
            _serviceReady = _updatableRegistered && _lateFrameRegistered;
        }

        private bool TryAbortForUsableExistingRuntime()
        {
            IMetaCampaignService registered = GlobalRegistry.MetaCampaign;
            if (ReferenceEquals(registered, null) || ReferenceEquals(registered, this))
                return false;

            if (IsMetaCampaignRuntimeUsable(registered))
            {
                _runtimeOwnerAborted = true;
                Destroy(this);
                return true;
            }

            MetaCampaignService staleService = registered as MetaCampaignService;
            if (!ReferenceEquals(staleService, null))
            {
                GlobalRegistry.UnregisterMetaCampaignService(registered);
                staleService._serviceRegistered = false;
                staleService._serviceReady = false;
            }

            return false;
        }

        private static bool IsMetaCampaignRuntimeUsable(IMetaCampaignService service)
        {
            if (ReferenceEquals(service, null))
                return false;

            MetaCampaignService runtime = service as MetaCampaignService;
            return ReferenceEquals(runtime, null) ||
                   (runtime != null &&
                    runtime._serviceRegistered &&
                    runtime.isActiveAndEnabled &&
                    !runtime._shutdown &&
                    !runtime._runtimeOwnerAborted);
        }

        private void TryRegisterSaveService()
        {
            if (_saveServiceRegistered || _shutdown || !_serviceRegistered)
                return;

            ISaveService saveService = _saveService;
            if (!IsSaveServiceUsable(saveService))
            {
                saveService = GlobalRegistry.Save;
                _saveService = saveService;
            }

            if (!IsSaveServiceUsable(saveService))
                return;

            saveService.Register(this);
            _registeredSaveService = saveService;
            _saveService = saveService;
            _saveServiceRegistered = true;
        }

        private void TryUnregisterSaveService()
        {
            if (!_saveServiceRegistered && _registeredSaveService == null)
                return;

            ISaveService saveService = _registeredSaveService != null ? _registeredSaveService : _saveService;
            if (saveService != null)
                saveService.Unregister(this);

            _registeredSaveService = null;
            _saveServiceRegistered = false;
        }

        private static bool IsSaveServiceUsable(ISaveService saveService)
        {
            return saveService != null && saveService.IsInitialized;
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot == GlobalRegistryServiceSlot.DataVault)
            {
                _evaluationPending = false;
                _pendingEvaluationResult = default;
                _visualStateDirty = false;
                _audioBroadcastDirty = false;
                _cartographyStateDirty = false;
                _pendingVisualChangeKind = 0;
                _pendingAudioBroadcastVariableHash = 0u;
                _pendingCartographyFrame = 0u;
                _pendingVisualFrame = 0u;
                ReleaseRuntimeState(previousService as IDataVault ?? _dataVault);
                _dataVault = currentService as IDataVault;

                if (!_shutdown && isActiveAndEnabled)
                {
                    AllocateRuntimeState();
                    EnsureDefaultVariables();
                    RefreshCachedStateFromVariables();
                }

                return;
            }

            if (serviceSlot == GlobalRegistryServiceSlot.EcosystemDirector)
            {
                _ecosystemDirector = currentService as IEcosystemDirectorService;
                return;
            }

            if (serviceSlot != GlobalRegistryServiceSlot.Save)
                return;

            TryUnregisterSaveService();
            _saveService = currentService as ISaveService;
            TryRegisterSaveService();
        }

        private void TryRegisterHotSwapListener()
        {
            if (_registeredHotSwapListener || !Application.isPlaying)
                return;

            _registeredHotSwapListener = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterHotSwapListener()
        {
            if (!_registeredHotSwapListener)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _registeredHotSwapListener = false;
        }

        private void ShutdownServiceState()
        {
            if (_runtimeOwnerAborted)
                return;

            if (_shutdown)
                return;

            TryUnregisterSaveService();

            TryUnregisterHotSwapListener();

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
            ReleaseRuntimeState(_dataVault);
            _evaluationPending = false;
            _pendingEvaluationResult = default;
            _visualStateDirty = false;
            _audioBroadcastDirty = false;
            _cartographyStateDirty = false;
            _pendingVisualChangeKind = 0;
            _pendingAudioBroadcastVariableHash = 0u;
            _pendingCartographyFrame = 0u;
            _pendingVisualFrame = 0u;
            _shutdown = true;
        }

        private void CompletePendingEvaluation()
        {
            if (!_evaluationPending)
                return;

            _evaluationPending = false;
            MetaCampaignEvaluationResult result = _pendingEvaluationResult;
            _pendingEvaluationResult = default;
            int changeCount = result.Changes.Length;
            if (changeCount <= 0)
                return;

            if (!TryApplyVariableChanges(
                    in result,
                    out FixedList128Bytes<MetaCampaignVariableChange> appliedChanges,
                    out bool shouldRetry))
            {
                if (shouldRetry)
                {
                    _pendingEvaluationResult = result;
                    _evaluationPending = true;
                }

                return;
            }

            RefreshCachedStateFromVariables();
            uint frame = (uint)Hecton8.Core.SystemDispatcher.CurrentFrameIndex;
            byte aggregateSideEffectFlags = 0;
            uint broadcastVariableHash = 0u;
            changeCount = appliedChanges.Length;
            for (int i = 0; i < changeCount; i++)
            {
                MetaCampaignVariableChange change = appliedChanges[i];
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

        private bool SeedDefaultState()
        {
            if (!IsInitialized)
                return false;

            if (!TryResetDefaultVariables())
                return false;

            RefreshCachedStateFromVariables();
            return true;
        }

        private void ResetDefaultState(byte changeKind)
        {
            if (!SeedDefaultState())
                return;

            PublishCampaignStateSnapshot(
                changeKind,
                (byte)(GlobalWorldStateSignal.FlagVisualRefresh | GlobalWorldStateSignal.FlagCartographyRefresh),
                (uint)Hecton8.Core.SystemDispatcher.CurrentFrameIndex);
        }

        private bool EnsureDefaultVariables()
        {
            if (!IsInitialized ||
                !TryAcquireVariablesWrite(out NativeArray<MetaCampaignVariableSlot> variables, out IDataVault lockedVault))
            {
                return false;
            }

            try
            {
                return TryEnsureDefaultVariables(variables);
            }
            finally
            {
                ReleaseVariablesWrite(lockedVault);
            }
        }

        private bool ApplyGlobalVariableChange(
            uint variableHash,
            int value,
            byte changeKind,
            byte sideEffectFlags,
            uint frame)
        {
            if (!TryUpsertGlobalVariable(variableHash, value))
                return false;

            RefreshCachedStateFromVariables();
            PublishGlobalVariableSignal(variableHash, value, changeKind, sideEffectFlags, frame);
            PublishStateSideEffects(changeKind, sideEffectFlags, variableHash, frame);
            return true;
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
            SignalBus<GlobalWorldStateSignal>.TryPushTracked(new GlobalWorldStateSignal
            {
                PositionAup = default,
                VariableHash = variableHash,
                Value = value,
                StageHash = _currentStageHash,
                ChangeKind = changeKind,
                Flags = sideEffectFlags,
                Sequence = _sequence
            }, ref s_x001MetaCampaignServiceSignalPushDropCount);

            WriteBlackBox(frame, variableHash, value, changeKind, sideEffectFlags);
        }

        private void PublishStateSideEffects(
            byte changeKind,
            byte sideEffectFlags,
            uint broadcastVariableHash,
            uint frame)
        {
            if ((sideEffectFlags & GlobalWorldStateSignal.FlagVisualRefresh) != 0)
                QueueCachedVisualState(changeKind, frame);

            if ((sideEffectFlags & GlobalWorldStateSignal.FlagAudioBroadcast) != 0)
                QueueCampaignBroadcast(broadcastVariableHash);

            if ((sideEffectFlags & GlobalWorldStateSignal.FlagCartographyRefresh) != 0)
                QueueCartographyState(frame);

            GlobalTelemetryBus.PublishModTelemetry(AgentHash, _currentStageHash, _toxicity01);
        }

        private void QueueCachedVisualState(byte changeKind, uint frame)
        {
            _visualStateDirty = true;
            _pendingVisualChangeKind = changeKind;
            _pendingVisualFrame = frame;
        }

        private void FlushCachedVisualState()
        {
            if (!_visualStateDirty)
                return;

            _visualStateDirty = false;
            byte changeKind = _pendingVisualChangeKind;
            uint frame = _pendingVisualFrame;
            _pendingVisualChangeKind = 0;
            _pendingVisualFrame = 0u;
            PublishCachedVisualState(changeKind, frame);
        }

        private void PublishCachedVisualState(byte changeKind, uint frame)
        {
            if (!math.isfinite(_toxicity01))
            {
                DumpBlackBox();
                _toxicity01 = 0f;
            }

            Shader.SetGlobalFloat(_HectonOceanToxicityId, _toxicity01);
            IEcosystemDirectorService ecosystemDirector = _ecosystemDirector;
            if (ecosystemDirector != null)
                ecosystemDirector.ApplyCampaignToxicityPressure(_toxicity01, _currentStageHash, frame);

            WriteBlackBox(frame, ToxicityLevelHash, (int)math.round(_toxicity01 * ToxicityValueMax), changeKind, GlobalWorldStateSignal.FlagVisualRefresh);
        }

        private void QueueCampaignBroadcast(uint variableHash)
        {
            _audioBroadcastDirty = true;
            if (variableHash != 0u || _pendingAudioBroadcastVariableHash == 0u)
                _pendingAudioBroadcastVariableHash = variableHash;
        }

        private void FlushCampaignBroadcast()
        {
            if (!_audioBroadcastDirty)
                return;

            _audioBroadcastDirty = false;
            uint variableHash = _pendingAudioBroadcastVariableHash;
            _pendingAudioBroadcastVariableHash = 0u;
            PublishCampaignBroadcast(variableHash);
        }

        private void PublishCampaignBroadcast(uint variableHash)
        {
            float severity01 = math.max(0.1f, _toxicity01);
            SignalBus<VocalWarningSignal>.TryPushTracked(new VocalWarningSignal
            {
                WarningHash = VocalWarningHashes.Radiation,
                SourceId = variableHash != 0u ? variableHash : VwsToxicityBroadcastHash,
                Severity01 = severity01,
                CooldownSeconds = 30f,
                Priority = (byte)VocalWarningId.Radiation,
                Flags = VocalWarningSignalFlags.HabitatIntegrityCompromised
            }, ref s_x001MetaCampaignServiceSignalPushDropCount);
        }

        private void QueueCartographyState(uint frame)
        {
            _cartographyStateDirty = true;
            _pendingCartographyFrame = frame;
        }

        private void FlushCartographyState()
        {
            if (!_cartographyStateDirty)
                return;

            _cartographyStateDirty = false;
            uint frame = _pendingCartographyFrame;
            _pendingCartographyFrame = 0u;
            PublishCartographyState(frame);
        }

        private void PublishCampaignStateSnapshot(byte changeKind, byte sideEffectFlags, uint frame)
        {
            sideEffectFlags |= GlobalWorldStateSignal.FlagAupIndependent;
            _sequence++;
            SignalBus<GlobalWorldStateSignal>.TryPushTracked(new GlobalWorldStateSignal
            {
                PositionAup = default,
                VariableHash = CampaignStageHash,
                Value = _currentStage,
                StageHash = _currentStageHash,
                ChangeKind = changeKind,
                Flags = sideEffectFlags,
                Sequence = _sequence
            }, ref s_x001MetaCampaignServiceSignalPushDropCount);

            PublishStateSideEffects(changeKind, sideEffectFlags, CampaignStageHash, frame);
            WriteBlackBox(frame, CampaignStageHash, _currentStage, changeKind, sideEffectFlags);
        }

        private void PublishCartographyState(uint frame)
        {
            SignalBus<NarrativePoiStateSignal>.TryPushTracked(new NarrativePoiStateSignal
            {
                StateMask = ((ulong)_currentStageHash << 32) | (uint)math.clamp(_currentStage, 0, int.MaxValue),
                PoiHash = CartographyCorruptionPoiHash,
                Frame = frame,
                PoiIndex = 0,
                Operation = _toxicity01 > 0f || _currentStage > 0 ? (byte)1 : (byte)0,
                Flags = GlobalWorldStateSignal.FlagCartographyRefresh
            }, ref s_x001MetaCampaignServiceSignalPushDropCount);
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
            return TryFindVariableValue(variableHash, out int value) ? value : fallback;
        }

        private bool TryUpsertGlobalVariable(uint variableHash, int value)
        {
            if (variableHash == 0u || !IsInitialized)
                return false;

            if (!TryAcquireVariablesWrite(out NativeArray<MetaCampaignVariableSlot> variables, out IDataVault lockedVault))
                return false;

            try
            {
                return TryUpsertVariableSlot(variables, variableHash, value);
            }
            finally
            {
                ReleaseVariablesWrite(lockedVault);
            }
        }

        private bool TryApplyVariableChanges(
            in MetaCampaignEvaluationResult result,
            out FixedList128Bytes<MetaCampaignVariableChange> appliedChanges,
            out bool shouldRetry)
        {
            appliedChanges = default;
            shouldRetry = false;
            int changeCount = result.Changes.Length;
            if (changeCount <= 0)
                return true;

            if (!TryAcquireVariablesWrite(out NativeArray<MetaCampaignVariableSlot> variables, out IDataVault lockedVault))
            {
                shouldRetry = true;
                return false;
            }

            try
            {
                if (!CanApplyVariableChanges(variables, in result.Changes))
                    return false;

                for (int i = 0; i < changeCount; i++)
                {
                    MetaCampaignVariableChange change = result.Changes[i];
                    if (!TryUpsertVariableSlot(variables, change.VariableHash, change.Value))
                        return false;

                    appliedChanges.Add(change);
                }

                return appliedChanges.Length == changeCount;
            }
            finally
            {
                ReleaseVariablesWrite(lockedVault);
            }
        }

        private bool TryReplaceGlobalVariablesFromSave(ref MetaCampaignDTO dto)
        {
            if (!TryAcquireVariablesWrite(out NativeArray<MetaCampaignVariableSlot> variables, out IDataVault lockedVault))
                return false;

            try
            {
                int count = math.clamp(dto.variableCount, 0, math.min(dto.variableHashes.Length, dto.variableValues.Length));
                if (!CanReplaceVariablesFromSave(ref dto, count, variables.Length))
                    return false;

                for (int i = 0; i < variables.Length; i++)
                    variables[i] = default;

                for (int i = 0; i < count; i++)
                {
                    uint variableHash = dto.variableHashes[i];
                    if (variableHash == 0u)
                        continue;

                    if (!TryUpsertVariableSlot(variables, variableHash, dto.variableValues[i]))
                        return false;
                }

                return TryEnsureDefaultVariables(variables);
            }
            finally
            {
                ReleaseVariablesWrite(lockedVault);
            }
        }

        private bool TryResetDefaultVariables()
        {
            if (!TryAcquireVariablesWrite(out NativeArray<MetaCampaignVariableSlot> variables, out IDataVault lockedVault))
                return false;

            try
            {
                for (int i = 0; i < variables.Length; i++)
                    variables[i] = default;

                return TryEnsureDefaultVariables(variables);
            }
            finally
            {
                ReleaseVariablesWrite(lockedVault);
            }
        }

        private static bool TryEnsureDefaultVariables(NativeArray<MetaCampaignVariableSlot> variables)
        {
            return TryEnsureVariableSlot(variables, CampaignStageHash, 0) &&
                   TryEnsureVariableSlot(variables, ToxicityLevelHash, 0) &&
                   TryEnsureVariableSlot(variables, LeviathanAwakenedHash, 0) &&
                   TryEnsureVariableSlot(variables, BaseDeltaDestroyedHash, 0);
        }

        private static bool TryEnsureVariableSlot(NativeArray<MetaCampaignVariableSlot> variables, uint variableHash, int fallback)
        {
            int value = fallback;
            if (TryFindVariableValue(variables.AsReadOnly(), variableHash, out int existing))
                value = existing;

            return TryUpsertVariableSlot(variables, variableHash, value);
        }

        private static bool TryUpsertVariableSlot(NativeArray<MetaCampaignVariableSlot> variables, uint variableHash, int value)
        {
            if (variableHash == 0u || !variables.IsCreated)
                return false;

            int firstEmptyIndex = -1;
            for (int i = 0; i < variables.Length; i++)
            {
                MetaCampaignVariableSlot slot = variables[i];
                if (slot.VariableHash == variableHash)
                {
                    slot.Value = value;
                    variables[i] = slot;
                    return true;
                }

                if (slot.VariableHash == 0u && firstEmptyIndex < 0)
                    firstEmptyIndex = i;
            }

            if (firstEmptyIndex < 0)
                return false;

            variables[firstEmptyIndex] = new MetaCampaignVariableSlot
            {
                VariableHash = variableHash,
                Value = value
            };
            return true;
        }

        private static bool CanApplyVariableChanges(
            NativeArray<MetaCampaignVariableSlot> variables,
            in FixedList128Bytes<MetaCampaignVariableChange> changes)
        {
            if (!variables.IsCreated)
                return false;

            int emptySlots = 0;
            for (int i = 0; i < variables.Length; i++)
            {
                if (variables[i].VariableHash == 0u)
                    emptySlots++;
            }

            int requiredNewSlots = 0;
            for (int i = 0; i < changes.Length; i++)
            {
                uint variableHash = changes[i].VariableHash;
                if (variableHash == 0u)
                    return false;

                if (!TryFindVariableValue(variables.AsReadOnly(), variableHash, out _))
                    requiredNewSlots++;
            }

            return requiredNewSlots <= emptySlots;
        }

        private static bool CanReplaceVariablesFromSave(ref MetaCampaignDTO dto, int count, int capacity)
        {
            if (capacity <= 0)
                return false;

            int requiredSlots = 0;
            for (int i = 0; i < count; i++)
            {
                uint variableHash = dto.variableHashes[i];
                if (variableHash == 0u || SaveVariableHashExistsBefore(ref dto, i, variableHash))
                    continue;

                requiredSlots++;
            }

            if (!SaveVariableHashExists(ref dto, count, CampaignStageHash))
                requiredSlots++;
            if (!SaveVariableHashExists(ref dto, count, ToxicityLevelHash))
                requiredSlots++;
            if (!SaveVariableHashExists(ref dto, count, LeviathanAwakenedHash))
                requiredSlots++;
            if (!SaveVariableHashExists(ref dto, count, BaseDeltaDestroyedHash))
                requiredSlots++;

            return requiredSlots <= capacity;
        }

        private static bool SaveVariableHashExists(ref MetaCampaignDTO dto, int count, uint variableHash)
        {
            for (int i = 0; i < count; i++)
            {
                if (dto.variableHashes[i] == variableHash)
                    return true;
            }

            return false;
        }

        private static bool SaveVariableHashExistsBefore(ref MetaCampaignDTO dto, int index, uint variableHash)
        {
            for (int i = 0; i < index; i++)
            {
                if (dto.variableHashes[i] == variableHash)
                    return true;
            }

            return false;
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

        private static MetaCampaignEvaluationResult EvaluateRules(
            in ProgressionEventSignal signal,
            NativeArray<MetaCampaignRule>.ReadOnly rules,
            NativeArray<MetaCampaignVariableSlot>.ReadOnly variables)
        {
            MetaCampaignEvaluationResult result = default;
            for (int i = 0; i < rules.Length; i++)
            {
                MetaCampaignRule rule = rules[i];
                if (!MatchesRule(in rule, in signal))
                    continue;

                if (TryFindVariableValue(variables, rule.VariableHash, out int existing) && existing == rule.Value)
                    continue;

                TryAppendChange(ref result, rule.VariableHash, rule.Value, rule.SideEffectFlags);
            }

            return result;
        }

        private static bool MatchesRule(in MetaCampaignRule rule, in ProgressionEventSignal signal)
        {
            if (rule.TriggerHash == 0u || rule.VariableHash == 0u)
                return false;

            switch (rule.MatchMode)
            {
                case RuleMatchPoi:
                    return signal.PoiHash == rule.TriggerHash;
                case RuleMatchQuest:
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

        private bool TryFindVariableValue(uint variableHash, out int value)
        {
            value = 0;
            if (variableHash == 0u ||
                !TryReadVariables(out NativeArray<MetaCampaignVariableSlot>.ReadOnly variables))
            {
                return false;
            }

            return TryFindVariableValue(variables, variableHash, out value);
        }

        private static bool TryFindVariableValue(
            NativeArray<MetaCampaignVariableSlot>.ReadOnly variables,
            uint variableHash,
            out int value)
        {
            value = 0;
            if (variableHash == 0u || !variables.IsCreated)
                return false;

            for (int i = 0; i < variables.Length; i++)
            {
                MetaCampaignVariableSlot slot = variables[i];
                if (slot.VariableHash != variableHash)
                    continue;

                value = slot.Value;
                return true;
            }

            return false;
        }

        private IDataVault CacheDataVaultCold()
        {
            if (_dataVault == null)
                _dataVault = GlobalRegistry.DataVault;

            return _dataVault;
        }

        private bool EnsureVaultBuffer<T>(
            ref VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredCapacity,
            NativeArrayOptions options) where T : struct
        {
            IDataVault vault = _dataVault;
            if (vault == null)
                return false;

            if (IsExactVaultHandle(in handle, bufferId) &&
                vault.TryReadOnlyHandle(in handle, out NativeArray<T>.ReadOnly existing) &&
                existing.IsCreated &&
                existing.Length >= requiredCapacity)
            {
                return true;
            }

            if (vault.IsAllocationLocked || vault.IsCompactionFenceActive)
                return false;

            if (handle.BufferID != 0u && handle.Generation != 0u)
                vault.ReleaseBuffer(in handle);

            handle = vault.EnsureGenerationHandle<T>(
                bufferId,
                requiredCapacity,
                VaultOwnerSystemId,
                options);

            return IsExactVaultHandle(in handle, bufferId) &&
                   vault.TryReadOnlyHandle(in handle, out NativeArray<T>.ReadOnly resolved) &&
                   resolved.IsCreated &&
                   resolved.Length >= requiredCapacity;
        }

        private bool TryReadVariables(out NativeArray<MetaCampaignVariableSlot>.ReadOnly variables)
        {
            variables = default;
            IDataVault vault = _dataVault;
            return vault != null &&
                   IsExactVaultHandle(in _variablesHandle, VariablesBufferId) &&
                   vault.TryReadOnlyHandle(in _variablesHandle, out variables) &&
                   variables.IsCreated &&
                   variables.Length >= GlobalVariableCapacity;
        }

        private bool TryReadRules(out NativeArray<MetaCampaignRule>.ReadOnly rules)
        {
            rules = default;
            IDataVault vault = _dataVault;
            return vault != null &&
                   IsExactVaultHandle(in _rulesHandle, RulesBufferId) &&
                   vault.TryReadOnlyHandle(in _rulesHandle, out rules) &&
                   rules.IsCreated &&
                   rules.Length >= RuleCapacity;
        }

        private bool TryAcquireVariablesWrite(out NativeArray<MetaCampaignVariableSlot> variables, out IDataVault lockedVault)
        {
            variables = default;
            lockedVault = null;
            IDataVault vault = _dataVault;
            if (vault == null ||
                !IsExactVaultHandle(in _variablesHandle, VariablesBufferId) ||
                !vault.TryAcquireWriteLock(in _variablesHandle, VaultOwnerSystemId, out variables))
            {
                return false;
            }

            bool keepLock = false;
            try
            {
                if (variables.IsCreated && variables.Length >= GlobalVariableCapacity)
                {
                    lockedVault = vault;
                    keepLock = true;
                    return true;
                }

                variables = default;
                return false;
            }
            finally
            {
                if (!keepLock)
                {
                    vault.ReleaseWriteLock(in _variablesHandle, VaultOwnerSystemId);
                    variables = default;
                }
            }
        }

        private void ReleaseVariablesWrite(IDataVault lockedVault)
        {
            if (lockedVault != null && IsExactVaultHandle(in _variablesHandle, VariablesBufferId))
                lockedVault.ReleaseWriteLock(in _variablesHandle, VaultOwnerSystemId);
        }

        private bool TryAcquireRulesWrite(out NativeArray<MetaCampaignRule> rules, out IDataVault lockedVault)
        {
            rules = default;
            lockedVault = null;
            IDataVault vault = _dataVault;
            if (vault == null ||
                !IsExactVaultHandle(in _rulesHandle, RulesBufferId) ||
                !vault.TryAcquireWriteLock(in _rulesHandle, VaultOwnerSystemId, out rules))
            {
                return false;
            }

            bool keepLock = false;
            try
            {
                if (rules.IsCreated && rules.Length >= RuleCapacity)
                {
                    lockedVault = vault;
                    keepLock = true;
                    return true;
                }

                rules = default;
                return false;
            }
            finally
            {
                if (!keepLock)
                {
                    vault.ReleaseWriteLock(in _rulesHandle, VaultOwnerSystemId);
                    rules = default;
                }
            }
        }

        private void ReleaseRulesWrite(IDataVault lockedVault)
        {
            if (lockedVault != null && IsExactVaultHandle(in _rulesHandle, RulesBufferId))
                lockedVault.ReleaseWriteLock(in _rulesHandle, VaultOwnerSystemId);
        }

        private bool TryAcquireBlackBoxWrite(out NativeArray<MetaCampaignBlackBoxEntry> blackBox, out IDataVault lockedVault)
        {
            blackBox = default;
            lockedVault = null;
            IDataVault vault = _dataVault;
            if (vault == null ||
                !IsExactVaultHandle(in _blackBoxHandle, BlackBoxBufferId) ||
                !vault.TryAcquireWriteLock(in _blackBoxHandle, VaultOwnerSystemId, out blackBox))
            {
                return false;
            }

            bool keepLock = false;
            try
            {
                if (blackBox.IsCreated && blackBox.Length >= BlackBoxCapacity)
                {
                    lockedVault = vault;
                    keepLock = true;
                    return true;
                }

                blackBox = default;
                return false;
            }
            finally
            {
                if (!keepLock)
                {
                    vault.ReleaseWriteLock(in _blackBoxHandle, VaultOwnerSystemId);
                    blackBox = default;
                }
            }
        }

        private void ReleaseBlackBoxWrite(IDataVault lockedVault)
        {
            if (lockedVault != null && IsExactVaultHandle(in _blackBoxHandle, BlackBoxBufferId))
                lockedVault.ReleaseWriteLock(in _blackBoxHandle, VaultOwnerSystemId);
        }

        private bool TryReadBlackBox(out NativeArray<MetaCampaignBlackBoxEntry>.ReadOnly blackBox)
        {
            blackBox = default;
            IDataVault vault = _dataVault;
            return vault != null &&
                   IsExactVaultHandle(in _blackBoxHandle, BlackBoxBufferId) &&
                   vault.TryReadOnlyHandle(in _blackBoxHandle, out blackBox) &&
                   blackBox.IsCreated &&
                   blackBox.Length >= BlackBoxCapacity;
        }

        private void ReleaseRuntimeState(IDataVault vault)
        {
            ReleaseVaultBuffer(vault, ref _variablesHandle);
            ReleaseVaultBuffer(vault, ref _rulesHandle);
            ReleaseVaultBuffer(vault, ref _blackBoxHandle);

            if (ReferenceEquals(_dataVault, vault))
                _dataVault = null;
        }

        private static void ReleaseVaultBuffer<T>(IDataVault vault, ref VaultGenerationHandle<T> handle) where T : struct
        {
            if (vault != null && handle.BufferID != 0u && handle.Generation != 0u)
                vault.ReleaseBuffer(in handle);

            handle = default;
        }

        private static bool IsExactVaultHandle<T>(in VaultGenerationHandle<T> handle, BufferID expectedBufferId) where T : struct
        {
            return handle.BufferID == unchecked((uint)(int)expectedBufferId) && handle.Generation != 0u;
        }

        private void WriteBlackBox(uint frame, uint variableHash, int value, byte changeKind, byte flags)
        {
            if (!math.isfinite(_toxicity01))
            {
                DumpBlackBox();
                return;
            }

            if (!TryAcquireBlackBoxWrite(out NativeArray<MetaCampaignBlackBoxEntry> blackBox, out IDataVault lockedVault))
                return;

            try
            {
                int slot = _blackBoxCursor % BlackBoxCapacity;
                _blackBoxCursor = (_blackBoxCursor + 1) % BlackBoxCapacity;
                blackBox[slot] = new MetaCampaignBlackBoxEntry
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
            finally
            {
                ReleaseBlackBoxWrite(lockedVault);
            }
        }

        private void DumpBlackBox()
        {
            if (!TryReadBlackBox(out NativeArray<MetaCampaignBlackBoxEntry>.ReadOnly blackBox))
                return;

            NativeArray<byte> payload = default;
            try
            {
                string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
                string dumpPath = Path.Combine(projectRoot, DumpRelativePath);
                const int headerBytes = 16;
                const int rowBytes = 24;
                int count = blackBox.Length;
                int byteCount = headerBytes + count * rowBytes;
                payload = H8Memory.Allocate<byte>(byteCount, VaultOwnerSystemId, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
                if (!payload.IsCreated)
                    return;

                unsafe
                {
                    byte* bytes = (byte*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(payload);
                    WriteUInt(bytes, 0, 0x4D43424Cu);
                    WriteUInt(bytes, 4, ServiceHash);
                    WriteInt(bytes, 8, _blackBoxCursor);
                    WriteInt(bytes, 12, count);

                    int cursor = headerBytes;
                    for (int i = 0; i < count; i++)
                    {
                        MetaCampaignBlackBoxEntry entry = blackBox[i];
                        WriteUInt(bytes, cursor, entry.Frame);
                        WriteUInt(bytes, cursor + 4, entry.StageHash);
                        WriteUInt(bytes, cursor + 8, entry.VariableHash);
                        WriteInt(bytes, cursor + 12, entry.Value);
                        WriteFloat(bytes, cursor + 16, entry.Toxicity01);
                        bytes[cursor + 20] = entry.ChangeKind;
                        bytes[cursor + 21] = entry.Flags;
                        WriteUShort(bytes, cursor + 22, entry.Sequence);
                        cursor += rowBytes;
                    }
                }

                if (!NativeFaultDumpWriter.TryWriteAll(dumpPath, payload, byteCount))
                {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    Hecton8.Core.H8Debug.LogError("MetaCampaignService blackbox native dump write failed.");
#endif
                }
            }
            catch (Exception)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Hecton8.Core.H8Debug.LogError("MetaCampaignService blackbox dump failed.");
#endif
            }
            finally
            {
                if (payload.IsCreated)
                    H8Memory.Release(ref payload, VaultOwnerSystemId);
            }
        }

        private static unsafe void WriteUInt(byte* data, int offset, uint value)
        {
            data[offset] = (byte)value;
            data[offset + 1] = (byte)(value >> 8);
            data[offset + 2] = (byte)(value >> 16);
            data[offset + 3] = (byte)(value >> 24);
        }

        private static unsafe void WriteInt(byte* data, int offset, int value)
        {
            WriteUInt(data, offset, unchecked((uint)value));
        }

        private static unsafe void WriteUShort(byte* data, int offset, ushort value)
        {
            data[offset] = (byte)value;
            data[offset + 1] = (byte)(value >> 8);
        }

        private static unsafe void WriteFloat(byte* data, int offset, float value)
        {
            UnsafeUtility.MemCpy(data + offset, &value, sizeof(float));
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
