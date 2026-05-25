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
        private const float AuthoritativeQualityWeight = 1f;
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

        private VaultGenerationHandle<CompartmentState> _frontHandle;
        private VaultGenerationHandle<CompartmentState> _backHandle;
        private VaultGenerationHandle<byte> _carbonDioxideByteLaneHandle;
        private VaultGenerationHandle<BaseAtmosphereTelemetryEntry> _blackBoxHandle;
        private VaultGenerationHandle<CompartmentState> _pendingReleaseFrontHandle;
        private VaultGenerationHandle<CompartmentState> _pendingReleaseBackHandle;
        private VaultGenerationHandle<byte> _pendingReleaseCarbonDioxideByteLaneHandle;
        private VaultGenerationHandle<BaseAtmosphereTelemetryEntry> _pendingReleaseBlackBoxHandle;
        private IDataVault _dataVault;
        private IDataVault _pendingReleaseVault;
        private IPowerGridService _powerGrid;
        private JobHandle _coldTickHandle;
        private JobHandle _disposeHandle;
        private bool _coldTickRunning;
        private bool _registered;
        private bool _registeredHotSwap;
        private bool _pendingVaultRebind;
        private bool _pendingNativeStateRelease;
        private bool _seededDefaultAtmosphere;
        private bool _activeOxygenTextDirty = true;
        private bool _oxygenDepletedTelemetryPublished;
        private int _blackBoxCursor;
        private int _fixedFrameIndex;
        private int _activeCompartmentIndex;
        private int _lastActiveOxygenWholePercent = -1;
        private int _lastSolveBudget;
        private float _tickAccumulator;
        private float _lastResolvedTickIntervalSeconds = BaseAtmosphereMath.HighTickSeconds;
        private float _lastQualityWeight01 = AuthoritativeQualityWeight;
        private float _activeStaminaRecoveryMultiplier = 1f;
        private BaseAtmosphereSolveMode _lastSolveMode = BaseAtmosphereSolveMode.High5Hz;

        public int CompartmentCount => TryReadFront(out NativeArray<CompartmentState>.ReadOnly front) ? front.Length : 0;
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
            if (_coldTickRunning || !TryReadFront(out NativeArray<CompartmentState>.ReadOnly front))
            {
                _tickAccumulator += math.max(0f, fixedDeltaTime);
                return;
            }

            float qualityWeight01 = ResolveGlobalQualityWeight01();
            _lastQualityWeight01 = qualityWeight01;
            _lastResolvedTickIntervalSeconds = BaseAtmosphereMath.ResolveColdTickIntervalSeconds(qualityWeight01);
            _lastSolveBudget = BaseAtmosphereMath.ResolveCompartmentSolveBudget(front.Length);
            _lastSolveMode = BaseAtmosphereMath.ResolveSolveMode(_lastSolveBudget, front.Length);
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
            if (!TryReadFront(out NativeArray<CompartmentState>.ReadOnly front) ||
                compartmentIndex < 0 ||
                compartmentIndex >= front.Length)
                return false;

            _activeCompartmentIndex = compartmentIndex;
            _activeOxygenTextDirty = true;
            return true;
        }

        public bool TryGetCompartmentState(int compartmentIndex, out CompartmentState state)
        {
            state = default;
            if (!TryReadFront(out NativeArray<CompartmentState>.ReadOnly front) ||
                compartmentIndex < 0 ||
                compartmentIndex >= front.Length)
                return false;

            state = front[compartmentIndex];
            return true;
        }

        public bool TrySetCompartmentState(int compartmentIndex, CompartmentState state)
        {
            if (_coldTickRunning ||
                !TryOpenCompartmentViews(out NativeArray<CompartmentState> front, out NativeArray<CompartmentState> back, out _) ||
                compartmentIndex < 0 ||
                compartmentIndex >= front.Length)
                return false;

            state.TotalPressureKPa = BaseAtmosphereMath.ResolveDaltonPressureFake(
                state.OxygenKPa,
                state.CarbonDioxideKPa,
                state.NitrogenKPa);
            if (state.InvMaxPressureKPa <= 0f)
                state.InvMaxPressureKPa = math.rcp(math.max(0.0001f, maxPressureKPa));

            front[compartmentIndex] = state;
            back[compartmentIndex] = state;
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

            bool fixedRegistered = GlobalRegistry.TryRegisterFixedTickable(this, PriorityLayer.Environment);
            bool postFixedRegistered = GlobalRegistry.TryRegisterPostFixedTickable(this, PriorityLayer.Environment);
            _registered = fixedRegistered && postFixedRegistered;
            if (!_registered)
            {
                if (postFixedRegistered)
                    GlobalRegistry.UnregisterPostFixedTickable(this, PriorityLayer.Environment);
                if (fixedRegistered)
                    GlobalRegistry.UnregisterFixedTickable(this, PriorityLayer.Environment);
            }
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
                    ReleaseNativeStateAliases(previousService as IDataVault ?? _dataVault, resetSeed: true);
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
            if (!TryFinalizeDeferredNativeDisposal())
                return;

            IDataVault vault = _dataVault;
            if (vault == null)
                return;

            int capacity = math.clamp(compartmentCapacity, 1, MaxCompartmentCapacity);
            if (TryOpenCompartmentViews(capacity, out _, out _, out _))
            {
                EnsureBlackBoxState(vault);
                return;
            }

            if (vault.IsAllocationLocked)
                return;

            _frontHandle = vault.EnsureGenerationHandle<CompartmentState>(
                FrontBufferId,
                capacity,
                OwnerSystemId,
                NativeArrayOptions.ClearMemory);
            _backHandle = vault.EnsureGenerationHandle<CompartmentState>(
                BackBufferId,
                capacity,
                OwnerSystemId,
                NativeArrayOptions.ClearMemory);
            _carbonDioxideByteLaneHandle = vault.EnsureGenerationHandle<byte>(
                CarbonDioxideByteLaneBufferId,
                capacity,
                OwnerSystemId,
                NativeArrayOptions.ClearMemory);

            if (!TryOpenCompartmentViews(capacity, out _, out _, out _))
            {
                ReleaseNativeStateAliases(vault, resetSeed: true);
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
            if (TryOpenBlackBox(out _) || vault == null || vault.IsAllocationLocked)
                return;

            _blackBoxHandle = vault.EnsureGenerationHandle<BaseAtmosphereTelemetryEntry>(
                BlackBoxBufferId,
                BlackBoxCapacity,
                OwnerSystemId,
                NativeArrayOptions.ClearMemory);

            if (!TryOpenBlackBox(out _))
                ReleaseVaultHandle(vault, ref _blackBoxHandle);
        }

        private void SeedDefaultAtmosphereIfNeeded()
        {
            if (_seededDefaultAtmosphere ||
                !seedDefaultAtmosphereOnEnable ||
                !TryOpenCompartmentViews(out NativeArray<CompartmentState> front, out NativeArray<CompartmentState> back, out NativeArray<byte> carbonDioxideByteLane))
                return;

            CompartmentState defaultState = BaseAtmosphereMath.CreateDefaultCompartment(
                maxPressureKPa,
                (ushort)(BaseAtmosphereFlags.Sealed | BaseAtmosphereFlags.ScrubberPowered));
            defaultState.OxygenBaseConsumptionKPaPerSecond = DefaultOxygenConsumptionKPaPerSecond;
            defaultState.CarbonDioxideGenerationKPaPerSecond = DefaultCarbonDioxideGenerationKPaPerSecond;

            for (int i = 0; i < front.Length; i++)
            {
                front[i] = defaultState;
                back[i] = defaultState;
                carbonDioxideByteLane[i] = BaseAtmosphereMath.EncodeCarbonDioxideByte(
                    defaultState.CarbonDioxideKPa,
                    defaultState.TotalPressureKPa);
            }

            _seededDefaultAtmosphere = true;
            CommitActiveCompartmentSideEffects();
        }

        private void ScheduleColdTick(float deltaTime, float qualityWeight01, int solveBudget)
        {
            if (_coldTickRunning ||
                !TryOpenCompartmentViews(out NativeArray<CompartmentState> front, out NativeArray<CompartmentState> back, out NativeArray<byte> carbonDioxideByteLane))
                return;

            BaseAtmosphereColdTickJob job = new BaseAtmosphereColdTickJob
            {
                Input = front,
                Output = back,
                CarbonDioxideByteLane = carbonDioxideByteLane,
                CompartmentCount = front.Length,
                ActiveCompartmentIndex = _activeCompartmentIndex,
                CompartmentSolveCount = math.clamp(solveBudget, 1, front.Length),
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

        private static float ResolveGlobalQualityWeight01()
        {
            if (MathLodRuntimeConfig.TryReadLatestConfig(out MathLodConfigDTO config))
                return MathLodApproximation.SaturateFinite(config.GlobalQualityWeight, AuthoritativeQualityWeight);

            return MathLodApproximation.SaturateFinite(HomeostasisBrain.GlobalQualityWeight, AuthoritativeQualityWeight);
        }

        private void TryCompleteColdTick()
        {
            if (!_coldTickRunning)
                return;

            if (!DispatcherJobSwap.TryComplete(ref _coldTickHandle, forceComplete: false))
                return;

            _coldTickRunning = false;
            Swap(ref _frontHandle, ref _backHandle);
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
            if (!TryOpenBlackBox(out NativeArray<BaseAtmosphereTelemetryEntry> blackBox) ||
                !TryReadFront(out NativeArray<CompartmentState>.ReadOnly front))
                return;

            int index = _blackBoxCursor;
            _blackBoxCursor = (_blackBoxCursor + 1) % BlackBoxCapacity;

            BaseAtmosphereTelemetryEntry entry;
            entry.FrameIndex = _fixedFrameIndex;
            entry.StateHash = ResolveStateHash(in state);
            entry.ActiveCompartmentIndex = _activeCompartmentIndex;
            entry.CompartmentCount = front.Length;
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
            blackBox[index] = entry;
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

        private bool TryOpenCompartmentViews(
            out NativeArray<CompartmentState> front,
            out NativeArray<CompartmentState> back,
            out NativeArray<byte> carbonDioxideByteLane)
        {
            return TryOpenCompartmentViews(1, out front, out back, out carbonDioxideByteLane);
        }

        private bool TryOpenCompartmentViews(
            int requiredLength,
            out NativeArray<CompartmentState> front,
            out NativeArray<CompartmentState> back,
            out NativeArray<byte> carbonDioxideByteLane)
        {
            front = default;
            back = default;
            carbonDioxideByteLane = default;
            IDataVault vault = _dataVault;
            return TryOpenVaultView(vault, in _frontHandle, requiredLength, out front) &&
                   TryOpenVaultView(vault, in _backHandle, requiredLength, out back) &&
                   TryOpenVaultView(vault, in _carbonDioxideByteLaneHandle, requiredLength, out carbonDioxideByteLane) &&
                   back.Length >= front.Length &&
                   carbonDioxideByteLane.Length >= front.Length;
        }

        private bool TryReadFront(out NativeArray<CompartmentState>.ReadOnly front)
        {
            front = default;
            if (!TryReadVaultView(_dataVault, in _frontHandle, 1, out NativeArray<CompartmentState> mutableFront))
                return false;

            front = mutableFront.AsReadOnly();
            return true;
        }

        private bool TryOpenBlackBox(out NativeArray<BaseAtmosphereTelemetryEntry> blackBox)
        {
            return TryOpenVaultView(_dataVault, in _blackBoxHandle, BlackBoxCapacity, out blackBox);
        }

        private static bool TryOpenVaultView<T>(
            IDataVault vault,
            in VaultGenerationHandle<T> handle,
            int requiredLength,
            out NativeArray<T> buffer)
            where T : struct
        {
            buffer = default;
            return vault != null &&
                   IsHandleCreated(in handle) &&
                   requiredLength >= 0 &&
                   vault.TryResolveHandle(in handle, out buffer) &&
                   buffer.IsCreated &&
                   buffer.Length >= requiredLength;
        }

        private static bool TryReadVaultView<T>(
            IDataVault vault,
            in VaultGenerationHandle<T> handle,
            int requiredLength,
            out NativeArray<T> buffer)
            where T : struct
        {
            buffer = default;
            return vault != null &&
                   IsHandleCreated(in handle) &&
                   requiredLength >= 0 &&
                   vault.TryReadHandle(in handle, out buffer) &&
                   buffer.IsCreated &&
                   buffer.Length >= requiredLength;
        }

        private static bool IsHandleCreated<T>(in VaultGenerationHandle<T> handle)
            where T : struct
        {
            return handle.BufferID != 0u && handle.Generation != 0u;
        }

        private bool TryFinalizeDeferredNativeDisposal()
        {
            if (!DispatcherJobSwap.TryFinalizeCompleted(ref _disposeHandle))
                return false;

            if (_pendingNativeStateRelease)
                ReleasePendingNativeState();

            return true;
        }

        private void DisposeNativeStateDeferred()
        {
            ReleaseNativeStateAliases(_dataVault, resetSeed: true);
        }

        private void RebindNativeStateAfterVaultReplacement()
        {
            if (!TryFinalizeDeferredNativeDisposal())
                return;

            _pendingVaultRebind = false;
            EnsureNativeState();
            SeedDefaultAtmosphereIfNeeded();
        }

        private void ReleaseNativeStateAliases(IDataVault vault, bool resetSeed)
        {
            if (_coldTickRunning)
            {
                _disposeHandle = JobHandle.CombineDependencies(_disposeHandle, _coldTickHandle);
                _coldTickHandle = default;
                _coldTickRunning = false;
                CapturePendingNativeStateRelease(vault);
            }
            else
            {
                ReleaseVaultHandles(vault);
            }

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

        private void CapturePendingNativeStateRelease(IDataVault vault)
        {
            if (_pendingNativeStateRelease)
                return;

            _pendingReleaseVault = vault;
            _pendingReleaseFrontHandle = _frontHandle;
            _pendingReleaseBackHandle = _backHandle;
            _pendingReleaseCarbonDioxideByteLaneHandle = _carbonDioxideByteLaneHandle;
            _pendingReleaseBlackBoxHandle = _blackBoxHandle;
            _pendingNativeStateRelease =
                vault != null &&
                (IsHandleCreated(in _pendingReleaseFrontHandle) ||
                 IsHandleCreated(in _pendingReleaseBackHandle) ||
                 IsHandleCreated(in _pendingReleaseCarbonDioxideByteLaneHandle) ||
                 IsHandleCreated(in _pendingReleaseBlackBoxHandle));
        }

        private void ReleasePendingNativeState()
        {
            IDataVault vault = _pendingReleaseVault;
            if (vault != null)
            {
                ReleaseVaultHandle(vault, ref _pendingReleaseFrontHandle);
                ReleaseVaultHandle(vault, ref _pendingReleaseBackHandle);
                ReleaseVaultHandle(vault, ref _pendingReleaseCarbonDioxideByteLaneHandle);
                ReleaseVaultHandle(vault, ref _pendingReleaseBlackBoxHandle);
            }

            _pendingReleaseFrontHandle = default;
            _pendingReleaseBackHandle = default;
            _pendingReleaseCarbonDioxideByteLaneHandle = default;
            _pendingReleaseBlackBoxHandle = default;
            _pendingReleaseVault = null;
            _pendingNativeStateRelease = false;
        }

        private void ReleaseVaultHandles(IDataVault vault)
        {
            if (vault != null)
            {
                ReleaseVaultHandle(vault, ref _frontHandle);
                ReleaseVaultHandle(vault, ref _backHandle);
                ReleaseVaultHandle(vault, ref _carbonDioxideByteLaneHandle);
                ReleaseVaultHandle(vault, ref _blackBoxHandle);
                return;
            }

            _frontHandle = default;
            _backHandle = default;
            _carbonDioxideByteLaneHandle = default;
            _blackBoxHandle = default;
        }

        private static void ReleaseVaultHandle<T>(IDataVault vault, ref VaultGenerationHandle<T> handle)
            where T : struct
        {
            if (IsHandleCreated(in handle))
                vault.ReleaseBuffer(in handle);

            handle = default;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void Swap(ref VaultGenerationHandle<CompartmentState> first, ref VaultGenerationHandle<CompartmentState> second)
        {
            VaultGenerationHandle<CompartmentState> temp = first;
            first = second;
            second = temp;
        }
    }
}
