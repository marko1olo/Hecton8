using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using Hecton.Localization;
using Hecton8.Core;
using Hecton8.Core.Contracts;
using Hecton8.Core.Memory;
using Hecton8.World;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Atmosphere
{
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct BaseAtmosphereTelemetryEntry
    {
        [FieldOffset(0)]
        public int FrameIndex;
        [FieldOffset(4)]
        public uint StateHash;
        [FieldOffset(8)]
        public int ActiveCompartmentIndex;
        [FieldOffset(12)]
        public int CompartmentCount;
        [FieldOffset(16)]
        public float OxygenKPa;
        [FieldOffset(20)]
        public float CarbonDioxideKPa;
        [FieldOffset(24)]
        public float NitrogenKPa;
        [FieldOffset(28)]
        public float TotalPressureKPa;
        [FieldOffset(32)]
        public float StaminaRecoveryMultiplier;
        [FieldOffset(36)]
        public float TickIntervalSeconds;
        [FieldOffset(40)]
        public float TickAccumulator;
        [FieldOffset(44)]
        public float GlobalQualityWeight01;
        [FieldOffset(48)]
        public ushort Flags;
        [FieldOffset(50)]
        public byte SolveMode;
        [FieldOffset(51)]
        public byte QualityWeightByte;
        [FieldOffset(52)]
        public uint _pad0;
        [FieldOffset(56)]
        public ulong _pad1;
    }

    [DisallowMultipleComponent]
    public sealed class BaseAtmosphereEngine : MonoBehaviour, IFixedTickable, IPostFixedTickable, IGlobalRegistryHotSwapListener
    {
        private const int MaxCompartmentCapacity = 128;
        private const int BlackBoxCapacity = 300;
        private const float DefaultMaxPressureKPa = HectonSurvivalContract.KPaPerAtmosphere;
        private const float DefaultOxygenConsumptionKPaPerSecond = HectonSurvivalContract.DefaultPlayerOxygenKPaPerSecond;
        private const float DefaultCarbonDioxideGenerationKPaPerSecond = HectonSurvivalContract.DefaultPlayerCarbonDioxideKPaPerSecond;
        private const float DefaultScrubberKPaPerSecond = HectonSurvivalContract.DefaultScrubberKPaPerSecond;
        private const float DefaultSuitRuptureThreshold = 0.65f;
        private const float DefaultSuitRuptureDrainPerSecond = 0.35f;
        private const float OxygenDepletedKPa = 1f;
        private const SystemID OwnerSystemId = SystemID.HabitatAtmosphere;
        private const BufferID FrontBufferId = (BufferID)0x42415341; // "BASA"
        private const BufferID BackBufferId = (BufferID)0x42415342; // "BASB"
        private const BufferID CarbonDioxideByteLaneBufferId = (BufferID)0x42415343; // "BASC"
        private const BufferID BlackBoxBufferId = (BufferID)0x42415344; // "BASD"

        private static readonly uint OxygenDepletedEventHash =
            unchecked((uint)LocHash.Compute("Atmosphere.OxygenDepleted"));
        private static readonly uint BaseAtmosphereContextHash =
            unchecked((uint)LocHash.Compute(nameof(BaseAtmosphereEngine)));

        [SerializeField] private int compartmentCapacity = BaseAtmosphereMath.DefaultCompartmentCapacity;
        [SerializeField] private float maxPressureKPa = DefaultMaxPressureKPa;
        [SerializeField] private float playerStressMultiplier = 1f;
        [SerializeField] private float scrubberKPaPerSecond = DefaultScrubberKPaPerSecond;
        [SerializeField] private byte scrubberByteReductionPerColdTick = 4;
        [SerializeField] private float authoringLogisticsPowerWatts;
        [SerializeField] private float suitRuptureDamage;
        [SerializeField] private float suitRuptureThreshold = DefaultSuitRuptureThreshold;
        [SerializeField] private float suitRuptureDrainPerSecond = DefaultSuitRuptureDrainPerSecond;
        [SerializeField] private bool seedDefaultAtmosphereOnEnable = true;

        private NativeArray<CompartmentState> _front;
        private NativeArray<CompartmentState> _back;
        private NativeArray<byte> _carbonDioxideByteLane;
        private NativeArray<BaseAtmosphereTelemetryEntry> _blackBox;
        private VaultBufferHandle<CompartmentState> _frontHandle;
        private VaultBufferHandle<CompartmentState> _backHandle;
        private VaultBufferHandle<byte> _carbonDioxideByteLaneHandle;
        private VaultBufferHandle<BaseAtmosphereTelemetryEntry> _blackBoxHandle;
        private IDataVault _dataVault;
        private IPowerGridService _powerGrid;
        private JobHandle _coldTickHandle;
        private JobHandle _disposeHandle;
        private bool _coldTickRunning;
        private bool _registered;
        private bool _registeredHotSwap;
        private bool _pendingVaultRebind;
        private bool _seededDefaultAtmosphere;
        private bool _activeOxygenTextDirty = true;
        private bool _oxygenDepletedTelemetryPublished;
        private int _blackBoxCursor;
        private int _fixedFrameIndex;
        private int _activeCompartmentIndex;
        private int _lastActiveOxygenWholePercent = -1;
        private int _lastSolveBudget;
        private float _tickAccumulator;
        private float _lastResolvedTickIntervalSeconds = BaseAtmosphereMath.LowColdTickSeconds;
        private float _lastQualityWeight01;
        private float _activeStaminaRecoveryMultiplier = 1f;
        private BaseAtmosphereSolveMode _lastSolveMode = BaseAtmosphereSolveMode.ActiveCompartment1Hz;

        public int CompartmentCount => _front.IsCreated ? _front.Length : 0;
        public int ActiveCompartmentIndex => _activeCompartmentIndex;
        public bool IsColdTickRunning => _coldTickRunning;
        public float LastResolvedTickIntervalSeconds => _lastResolvedTickIntervalSeconds;
        public BaseAtmosphereSolveMode LastSolveMode => _lastSolveMode;
        public float ActiveStaminaRecoveryMultiplier => _activeStaminaRecoveryMultiplier;

        public bool ActiveTraumaGlitchRequested =>
            TryGetCompartmentState(_activeCompartmentIndex, out CompartmentState state) &&
            BaseAtmosphereMath.HasFlag(state.Flags, BaseAtmosphereFlags.TraumaGlitchRequested);

        public bool ActiveFogRequested =>
            TryGetCompartmentState(_activeCompartmentIndex, out CompartmentState state) &&
            BaseAtmosphereMath.HasFlag(state.Flags, BaseAtmosphereFlags.RenderFogRequested);

        public bool ActiveBubbleVfxRequested =>
            TryGetCompartmentState(_activeCompartmentIndex, out CompartmentState state) &&
            BaseAtmosphereMath.HasFlag(state.Flags, BaseAtmosphereFlags.BubbleVfxRequested);

        public bool ActiveHelioxIconRequested =>
            TryGetCompartmentState(_activeCompartmentIndex, out CompartmentState state) &&
            BaseAtmosphereMath.BypassesNitrogenNarcosis(state.Flags);

        private void OnEnable()
        {
            CacheRegistryServicesCold();
            TryRegisterHotSwap();
            EnsureNativeState();
            SeedDefaultAtmosphereIfNeeded();
            TryRegister();
        }

        private void OnDisable()
        {
            TryUnregister();
            TryUnregisterHotSwap();
            DisposeNativeStateDeferred();
        }

        private void OnDestroy()
        {
            TryUnregister();
            TryUnregisterHotSwap();
            DisposeNativeStateDeferred();
        }

        public void FixedTick(float fixedDeltaTime)
        {
            if (fixedDeltaTime <= 0f)
                return;

            _fixedFrameIndex++;
            if (!TryFinalizeDeferredNativeDisposal())
                return;

            if (_pendingVaultRebind && !_coldTickRunning)
                RebindNativeStateAfterVaultReplacement();

            EnsureNativeState();
            SeedDefaultAtmosphereIfNeeded();
            if (!_front.IsCreated || _coldTickRunning)
            {
                _tickAccumulator += math.max(0f, fixedDeltaTime);
                return;
            }

            float qualityWeight01 = ResolveGlobalQualityWeight01();
            _lastQualityWeight01 = qualityWeight01;
            _lastResolvedTickIntervalSeconds = BaseAtmosphereMath.ResolveColdTickIntervalSeconds(qualityWeight01);
            _lastSolveBudget = BaseAtmosphereMath.ResolveCompartmentSolveBudget(_front.Length, qualityWeight01);
            _lastSolveMode = BaseAtmosphereMath.ResolveSolveMode(qualityWeight01, _lastSolveBudget, _front.Length);
            _tickAccumulator += math.max(0f, fixedDeltaTime);
            if (_tickAccumulator + 0.0001f < _lastResolvedTickIntervalSeconds)
                return;

            float coldTickDeltaTime = _tickAccumulator;
            _tickAccumulator = 0f;
            ScheduleColdTick(coldTickDeltaTime, qualityWeight01, _lastSolveBudget);
        }

        public void PostFixedTick(float fixedDeltaTime)
        {
            TryCompleteColdTick();
        }

        public bool TrySetActiveCompartmentIndex(int compartmentIndex)
        {
            if (!_front.IsCreated || compartmentIndex < 0 || compartmentIndex >= _front.Length)
                return false;

            _activeCompartmentIndex = compartmentIndex;
            _activeOxygenTextDirty = true;
            return true;
        }

        public bool TryGetCompartmentState(int compartmentIndex, out CompartmentState state)
        {
            state = default;
            if (!_front.IsCreated || compartmentIndex < 0 || compartmentIndex >= _front.Length)
                return false;

            state = _front[compartmentIndex];
            return true;
        }

        public bool TrySetCompartmentState(int compartmentIndex, CompartmentState state)
        {
            if (_coldTickRunning || !_front.IsCreated || compartmentIndex < 0 || compartmentIndex >= _front.Length)
                return false;

            state.TotalPressureKPa = BaseAtmosphereMath.ResolveDaltonPressureFake(
                state.OxygenKPa,
                state.CarbonDioxideKPa,
                state.NitrogenKPa);
            if (state.InvMaxPressureKPa <= 0f)
                state.InvMaxPressureKPa = math.rcp(math.max(0.0001f, maxPressureKPa));

            _front[compartmentIndex] = state;
            _back[compartmentIndex] = state;
            if (compartmentIndex == _activeCompartmentIndex)
                _activeOxygenTextDirty = true;
            return true;
        }

        public bool TrySetCompartmentFlags(int compartmentIndex, ushort flags, bool enabled)
        {
            if (_coldTickRunning || !TryGetCompartmentState(compartmentIndex, out CompartmentState state))
                return false;

            state.Flags = enabled ? (ushort)(state.Flags | flags) : BaseAtmosphereMath.ClearFlags(state.Flags, flags);
            return TrySetCompartmentState(compartmentIndex, state);
        }

        public bool TryApplySmokeFake(int compartmentIndex, byte toxicityIncrement)
        {
            if (_coldTickRunning || !TryGetCompartmentState(compartmentIndex, out CompartmentState state))
                return false;

            state.Toxicity = BaseAtmosphereMath.SaturatingAddByte(state.Toxicity, toxicityIncrement);
            state.Flags = (ushort)(state.Flags | BaseAtmosphereFlags.SmokeParticlesRequested);
            return TrySetCompartmentState(compartmentIndex, state);
        }

        public bool TrySetHumidityPercent(int compartmentIndex, byte humidityPercent)
        {
            if (_coldTickRunning || !TryGetCompartmentState(compartmentIndex, out CompartmentState state))
                return false;

            state.HumidityPercent = humidityPercent;
            return TrySetCompartmentState(compartmentIndex, state);
        }

        public void SetPlayerStressMultiplier(float stressMultiplier)
        {
            playerStressMultiplier = math.max(1f, math.isfinite(stressMultiplier) ? stressMultiplier : 1f);
        }

        public void SetAuthoringLogisticsPowerWatts(float powerWatts)
        {
            authoringLogisticsPowerWatts = math.max(0f, math.isfinite(powerWatts) ? powerWatts : 0f);
        }

        public void SetSuitRuptureDamage(float damage)
        {
            suitRuptureDamage = math.max(0f, math.isfinite(damage) ? damage : 0f);
        }

        public bool TryFormatActiveOxygenText(Span<char> destination, out int charsWritten)
        {
            charsWritten = 0;
            if (!_activeOxygenTextDirty || destination.Length < 2)
                return false;

            int oxygenPercent = math.max(0, _lastActiveOxygenWholePercent);
            if (!ZeroGCFormatter.TryFormatInt(oxygenPercent, destination, out charsWritten))
                return false;

            if (charsWritten >= destination.Length)
                return false;

            destination[charsWritten] = '%';
            charsWritten++;
            _activeOxygenTextDirty = false;
            return true;
        }

        public static async Awaitable RunAirlockEqualizationFakeAsync(AudioSource hissAudio, CancellationToken cancellationToken)
        {
            if (hissAudio != null && !hissAudio.isPlaying)
                hissAudio.Play();

            try
            {
                await Awaitable.WaitForSecondsAsync(
                    BaseAtmosphereMath.AirlockEqualizationSeconds,
                    cancellationToken: cancellationToken);
            }
            finally
            {
                if (hissAudio != null && hissAudio.isPlaying)
                    hissAudio.Stop();
            }
        }

        public static unsafe bool TryBlitOxygenTankItemIds(
            NativeArray<ushort> inventoryItemIds,
            int inventoryStartIndex,
            NativeArray<ushort> suitSlotItemIds,
            int suitSlotStartIndex,
            int itemCount)
        {
            if (!inventoryItemIds.IsCreated ||
                !suitSlotItemIds.IsCreated ||
                inventoryStartIndex < 0 ||
                suitSlotStartIndex < 0 ||
                itemCount < 0 ||
                inventoryStartIndex + itemCount > inventoryItemIds.Length ||
                suitSlotStartIndex + itemCount > suitSlotItemIds.Length)
            {
                return false;
            }

            if (itemCount == 0)
                return true;

            int elementSize = UnsafeUtility.SizeOf<ushort>();
            long copyBytes = (long)itemCount * elementSize;
            long destinationBytes = (long)(suitSlotItemIds.Length - suitSlotStartIndex) * elementSize;
            ushort* source = (ushort*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(inventoryItemIds) + inventoryStartIndex;
            ushort* destination = (ushort*)NativeArrayUnsafeUtility.GetUnsafePtr(suitSlotItemIds) + suitSlotStartIndex;
            if (UnsafeMemoryCopyGuard.TryMemCpy(destination, destinationBytes, source, copyBytes))
                return true;

            UnsafeMemoryCopyGuard.ReportRejectedCopy(nameof(BaseAtmosphereEngine));
            return false;
        }

        private void TryRegister()
        {
            if (_registered || !Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            GlobalRegistry.RegisterFixedTickable(this, PriorityLayer.Environment);
            GlobalRegistry.RegisterPostFixedTickable(this, PriorityLayer.Environment);
            _registered = SystemDispatcher.GetPostFixedLane(PriorityLayer.Environment).Contains(this);
        }

        private void TryUnregister()
        {
            if (!_registered)
                return;

            GlobalRegistry.UnregisterPostFixedTickable(this, PriorityLayer.Environment);
            GlobalRegistry.UnregisterFixedTickable(this, PriorityLayer.Environment);
            _registered = false;
        }

        private void CacheRegistryServicesCold()
        {
            _dataVault = GlobalRegistry.DataVault;
            _powerGrid = GlobalRegistry.PowerGrid;
        }

        private void TryRegisterHotSwap()
        {
            if (_registeredHotSwap || !Application.isPlaying)
                return;

            _registeredHotSwap = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterHotSwap()
        {
            if (!_registeredHotSwap)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _registeredHotSwap = false;
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            switch (serviceSlot)
            {
                case GlobalRegistryServiceSlot.DataVault:
                    _dataVault = currentService as IDataVault;
                    _pendingVaultRebind = true;
                    if (!_coldTickRunning && TryFinalizeDeferredNativeDisposal())
                        RebindNativeStateAfterVaultReplacement();
                    break;
                case GlobalRegistryServiceSlot.PowerGrid:
                    _powerGrid = currentService as IPowerGridService;
                    break;
            }
        }

        private void EnsureNativeState()
        {
            if (_front.IsCreated || !TryFinalizeDeferredNativeDisposal())
                return;

            IDataVault vault = _dataVault;
            if (vault == null)
                return;

            int capacity = math.clamp(compartmentCapacity, 1, MaxCompartmentCapacity);
            _frontHandle = vault.GetBufferHandle<CompartmentState>(
                FrontBufferId,
                capacity,
                OwnerSystemId,
                NativeArrayOptions.ClearMemory);
            _backHandle = vault.GetBufferHandle<CompartmentState>(
                BackBufferId,
                capacity,
                OwnerSystemId,
                NativeArrayOptions.ClearMemory);
            _carbonDioxideByteLaneHandle = vault.GetBufferHandle<byte>(
                CarbonDioxideByteLaneBufferId,
                capacity,
                OwnerSystemId,
                NativeArrayOptions.ClearMemory);

            _front = _frontHandle.Resolve(vault);
            _back = _backHandle.Resolve(vault);
            _carbonDioxideByteLane = _carbonDioxideByteLaneHandle.Resolve(vault);
            if (!_front.IsCreated ||
                !_back.IsCreated ||
                !_carbonDioxideByteLane.IsCreated ||
                _front.Length < capacity ||
                _back.Length < capacity ||
                _carbonDioxideByteLane.Length < capacity)
            {
                ReleaseNativeStateAliases(resetSeed: true);
                return;
            }

            EnsureBlackBoxState(vault);
        }

        private void EnsureBlackBoxState()
        {
            IDataVault vault = _dataVault;
            if (vault != null)
                EnsureBlackBoxState(vault);
        }

        private void EnsureBlackBoxState(IDataVault vault)
        {
            if (_blackBox.IsCreated || vault == null)
                return;

            _blackBoxHandle = vault.GetBufferHandle<BaseAtmosphereTelemetryEntry>(
                BlackBoxBufferId,
                BlackBoxCapacity,
                OwnerSystemId,
                NativeArrayOptions.ClearMemory);
            _blackBox = _blackBoxHandle.Resolve(vault);
        }

        private void SeedDefaultAtmosphereIfNeeded()
        {
            if (_seededDefaultAtmosphere || !seedDefaultAtmosphereOnEnable || !_front.IsCreated)
                return;

            CompartmentState defaultState = BaseAtmosphereMath.CreateDefaultCompartment(
                maxPressureKPa,
                (ushort)(BaseAtmosphereFlags.Sealed | BaseAtmosphereFlags.ScrubberPowered));
            defaultState.OxygenBaseConsumptionKPaPerSecond = DefaultOxygenConsumptionKPaPerSecond;
            defaultState.CarbonDioxideGenerationKPaPerSecond = DefaultCarbonDioxideGenerationKPaPerSecond;

            for (int i = 0; i < _front.Length; i++)
            {
                _front[i] = defaultState;
                _back[i] = defaultState;
                _carbonDioxideByteLane[i] = BaseAtmosphereMath.EncodeCarbonDioxideByte(
                    defaultState.CarbonDioxideKPa,
                    defaultState.TotalPressureKPa);
            }

            _seededDefaultAtmosphere = true;
            CommitActiveCompartmentSideEffects();
        }

        private void ScheduleColdTick(float deltaTime, float qualityWeight01, int solveBudget)
        {
            if (_coldTickRunning || !_front.IsCreated)
                return;

            BaseAtmosphereColdTickJob job = new BaseAtmosphereColdTickJob
            {
                Input = _front,
                Output = _back,
                CarbonDioxideByteLane = _carbonDioxideByteLane,
                CompartmentCount = _front.Length,
                ActiveCompartmentIndex = _activeCompartmentIndex,
                CompartmentSolveCount = math.clamp(solveBudget, 1, _front.Length),
                DeltaTime = math.max(0f, deltaTime),
                PlayerStressMultiplier = math.max(1f, playerStressMultiplier),
                LogisticsPowerWatts = ResolveLogisticsPowerWatts(),
                ScrubberKPaPerSecond = math.max(0f, scrubberKPaPerSecond),
                SuitRuptureDamage = math.max(0f, suitRuptureDamage),
                SuitRuptureThreshold = math.max(0f, suitRuptureThreshold),
                SuitRuptureDrainPerSecond = math.max(0f, suitRuptureDrainPerSecond),
                VisualOverkillWeight01 = BaseAtmosphereMath.ResolveVisualOverkillWeight01(qualityWeight01),
                ScrubberBytePerColdTick = scrubberByteReductionPerColdTick,
            };

            _coldTickHandle = job.Schedule();
            _coldTickRunning = true;
        }

        private void TryCompleteColdTick()
        {
            if (!_coldTickRunning)
                return;

            if (!DispatcherJobSwap.TryComplete(ref _coldTickHandle, forceComplete: false))
                return;

            _coldTickRunning = false;
            Swap(ref _front, ref _back);
            CommitActiveCompartmentSideEffects();
        }

        private void CommitActiveCompartmentSideEffects()
        {
            if (!TryGetCompartmentState(_activeCompartmentIndex, out CompartmentState state))
                return;

            int oxygenPercent = BaseAtmosphereMath.ResolveOxygenWholePercent(state);
            if (oxygenPercent != _lastActiveOxygenWholePercent)
            {
                _lastActiveOxygenWholePercent = oxygenPercent;
                _activeOxygenTextDirty = true;
            }

            float carbonDioxideFraction = BaseAtmosphereMath.ResolveGasFraction(
                state.CarbonDioxideKPa,
                state.TotalPressureKPa);
            _activeStaminaRecoveryMultiplier =
                BaseAtmosphereMath.ResolveStaminaRecoveryMultiplierForCarbonDioxide(carbonDioxideFraction);
            RecordBlackBox(in state);

            if (state.OxygenKPa <= OxygenDepletedKPa)
            {
                if (!_oxygenDepletedTelemetryPublished)
                {
                    GlobalTelemetryBus.PublishPerformanceWarning(
                        OxygenDepletedEventHash,
                        BaseAtmosphereContextHash,
                        _activeCompartmentIndex);
                    _oxygenDepletedTelemetryPublished = true;
                }
            }
            else
            {
                _oxygenDepletedTelemetryPublished = false;
            }
        }

        private void RecordBlackBox(in CompartmentState state)
        {
            EnsureBlackBoxState();
            if (!_blackBox.IsCreated || _blackBox.Length < BlackBoxCapacity)
                return;

            int index = _blackBoxCursor;
            _blackBoxCursor = (_blackBoxCursor + 1) % BlackBoxCapacity;

            BaseAtmosphereTelemetryEntry entry;
            entry.FrameIndex = _fixedFrameIndex;
            entry.StateHash = ResolveStateHash(in state);
            entry.ActiveCompartmentIndex = _activeCompartmentIndex;
            entry.CompartmentCount = _front.IsCreated ? _front.Length : 0;
            entry.OxygenKPa = state.OxygenKPa;
            entry.CarbonDioxideKPa = state.CarbonDioxideKPa;
            entry.NitrogenKPa = state.NitrogenKPa;
            entry.TotalPressureKPa = state.TotalPressureKPa;
            entry.StaminaRecoveryMultiplier = _activeStaminaRecoveryMultiplier;
            entry.TickIntervalSeconds = _lastResolvedTickIntervalSeconds;
            entry.TickAccumulator = _tickAccumulator;
            entry.GlobalQualityWeight01 = _lastQualityWeight01;
            entry.Flags = state.Flags;
            entry.SolveMode = (byte)_lastSolveMode;
            entry.QualityWeightByte = EncodeQualityWeightByte(_lastQualityWeight01);
            entry._pad0 = 0u;
            entry._pad1 = 0ul;
            _blackBox[index] = entry;
        }

        private float ResolveLogisticsPowerWatts()
        {
            float localPower = math.max(0f, math.isfinite(authoringLogisticsPowerWatts) ? authoringLogisticsPowerWatts : 0f);
            IPowerGridService powerGrid = _powerGrid;
            if (powerGrid == null)
                return localPower;

            BatteryRuntimeSnapshot battery = powerGrid.BatterySnapshot;
            float netGeneration = powerGrid.TotalGeneration - powerGrid.TotalConsumption;
            float batteryCarrier = battery.TotalStoredEnergyWattSeconds > 1f ? 1f : 0f;
            return math.max(localPower, math.max(netGeneration, batteryCarrier));
        }

        private static float ResolveGlobalQualityWeight01()
        {
            float quality = HomeostasisBrain.GlobalQualityWeight;
            return math.saturate(math.isfinite(quality) ? quality : 0f);
        }

        private static byte EncodeQualityWeightByte(float qualityWeight01)
        {
            return (byte)math.clamp((int)math.round(math.saturate(qualityWeight01) * 255f), 0, 255);
        }

        private static uint ResolveStateHash(in CompartmentState state)
        {
            uint hash = 2166136261u;
            hash = Hash(hash, math.asuint(state.OxygenKPa));
            hash = Hash(hash, math.asuint(state.CarbonDioxideKPa));
            hash = Hash(hash, math.asuint(state.NitrogenKPa));
            hash = Hash(hash, math.asuint(state.TotalPressureKPa));
            hash = Hash(hash, state.Flags);
            hash = Hash(hash, state.Toxicity);
            hash = Hash(hash, state.HumidityPercent);
            return hash;
        }

        private static uint Hash(uint hash, uint value)
        {
            hash ^= value;
            return hash * 16777619u;
        }

        private bool TryFinalizeDeferredNativeDisposal()
        {
            return DispatcherJobSwap.TryFinalizeCompleted(ref _disposeHandle);
        }

        private void DisposeNativeStateDeferred()
        {
            ReleaseNativeStateAliases(resetSeed: true);
        }

        private void RebindNativeStateAfterVaultReplacement()
        {
            ReleaseNativeStateAliases(resetSeed: true);
            _pendingVaultRebind = false;
            EnsureNativeState();
            SeedDefaultAtmosphereIfNeeded();
        }

        private void ReleaseNativeStateAliases(bool resetSeed)
        {
            if (_coldTickRunning)
            {
                _disposeHandle = JobHandle.CombineDependencies(_disposeHandle, _coldTickHandle);
                _coldTickHandle = default;
                _coldTickRunning = false;
            }

            _front = default;
            _back = default;
            _carbonDioxideByteLane = default;
            _blackBox = default;
            _frontHandle = default;
            _backHandle = default;
            _carbonDioxideByteLaneHandle = default;
            _blackBoxHandle = default;
            _blackBoxCursor = 0;
            _lastSolveBudget = 0;
            if (resetSeed)
            {
                _seededDefaultAtmosphere = false;
                _activeOxygenTextDirty = true;
                _lastActiveOxygenWholePercent = -1;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void Swap(ref NativeArray<CompartmentState> first, ref NativeArray<CompartmentState> second)
        {
            NativeArray<CompartmentState> temp = first;
            first = second;
            second = temp;
        }
    }
}
