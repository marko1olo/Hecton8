using System;
using System.Runtime.CompilerServices;
using System.Threading;
using Hecton.Localization;
using Hecton8.Core;
using Hecton8.Core.Contracts;
using Hecton8.World;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Atmosphere
{
    [DisallowMultipleComponent]
    public sealed class BaseAtmosphereEngine : MonoBehaviour, IFixedTickable, IPostFixedTickable
    {
        private const int MaxCompartmentCapacity = 128;
        private const float DefaultMaxPressureKPa = HectonSurvivalContract.KPaPerAtmosphere;
        private const float DefaultOxygenConsumptionKPaPerSecond = HectonSurvivalContract.DefaultPlayerOxygenKPaPerSecond;
        private const float DefaultCarbonDioxideGenerationKPaPerSecond = HectonSurvivalContract.DefaultPlayerCarbonDioxideKPaPerSecond;
        private const float DefaultScrubberKPaPerSecond = HectonSurvivalContract.DefaultScrubberKPaPerSecond;
        private const float DefaultSuitRuptureThreshold = 0.65f;
        private const float DefaultSuitRuptureDrainPerSecond = 0.35f;
        private const float OxygenDepletedKPa = 1f;

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
        private JobHandle _coldTickHandle;
        private JobHandle _disposeHandle;
        private bool _coldTickRunning;
        private bool _registered;
        private bool _seededDefaultAtmosphere;
        private bool _activeOxygenTextDirty = true;
        private bool _oxygenDepletedTelemetryPublished;
        private int _activeCompartmentIndex;
        private int _lastActiveOxygenWholePercent = -1;
        private float _tickAccumulator;
        private float _lastResolvedTickIntervalSeconds = BaseAtmosphereMath.LowColdTickSeconds;
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
            EnsureNativeState();
            SeedDefaultAtmosphereIfNeeded();
            TryRegister();
        }

        private void OnDisable()
        {
            TryUnregister();
            DisposeNativeStateDeferred();
        }

        private void OnDestroy()
        {
            TryUnregister();
            DisposeNativeStateDeferred();
        }

        public void FixedTick(float fixedDeltaTime)
        {
            if (fixedDeltaTime <= 0f)
                return;

            if (!TryFinalizeDeferredNativeDisposal())
                return;

            EnsureNativeState();
            SeedDefaultAtmosphereIfNeeded();
            if (!_front.IsCreated || _coldTickRunning)
            {
                _tickAccumulator += math.max(0f, fixedDeltaTime);
                return;
            }

            HectonQualityTier tier = GlobalRegistry.ScalabilityTier;
            _lastResolvedTickIntervalSeconds = BaseAtmosphereMath.ResolveColdTickIntervalSeconds(tier);
            _lastSolveMode = BaseAtmosphereMath.ResolveSolveMode(tier);
            _tickAccumulator += math.max(0f, fixedDeltaTime);
            if (_tickAccumulator + 0.0001f < _lastResolvedTickIntervalSeconds)
                return;

            float coldTickDeltaTime = _tickAccumulator;
            _tickAccumulator = 0f;
            ScheduleColdTick(coldTickDeltaTime, tier);
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

        private void EnsureNativeState()
        {
            if (_front.IsCreated || !TryFinalizeDeferredNativeDisposal())
                return;

            int capacity = math.clamp(compartmentCapacity, 1, MaxCompartmentCapacity);
            _front = new NativeArray<CompartmentState>(capacity, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: base atmosphere front buffer - owner: BaseAtmosphereEngine
            _back = new NativeArray<CompartmentState>(capacity, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: base atmosphere back buffer - owner: BaseAtmosphereEngine
            _carbonDioxideByteLane = new NativeArray<byte>(capacity, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: CO2 toxicity byte lane - owner: BaseAtmosphereEngine
            NativeMemorySentinel.RegisterNativeArray(_front, nameof(BaseAtmosphereEngine), nameof(_front), NativeAllocationLifetime.Scene);
            NativeMemorySentinel.RegisterNativeArray(_back, nameof(BaseAtmosphereEngine), nameof(_back), NativeAllocationLifetime.Scene);
            NativeMemorySentinel.RegisterNativeArray(_carbonDioxideByteLane, nameof(BaseAtmosphereEngine), nameof(_carbonDioxideByteLane), NativeAllocationLifetime.Scene);
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

        private void ScheduleColdTick(float deltaTime, HectonQualityTier tier)
        {
            if (_coldTickRunning || !_front.IsCreated)
                return;

            bool highScalability = BaseAtmosphereMath.IsHighScalability(tier);
            BaseAtmosphereColdTickJob job = new BaseAtmosphereColdTickJob
            {
                Input = _front,
                Output = _back,
                CarbonDioxideByteLane = _carbonDioxideByteLane,
                CompartmentCount = _front.Length,
                ActiveCompartmentIndex = _activeCompartmentIndex,
                DeltaTime = math.max(0f, deltaTime),
                PlayerStressMultiplier = math.max(1f, playerStressMultiplier),
                LogisticsPowerWatts = ResolveLogisticsPowerWatts(),
                ScrubberKPaPerSecond = math.max(0f, scrubberKPaPerSecond),
                SuitRuptureDamage = math.max(0f, suitRuptureDamage),
                SuitRuptureThreshold = math.max(0f, suitRuptureThreshold),
                SuitRuptureDrainPerSecond = math.max(0f, suitRuptureDrainPerSecond),
                SolveMode = (byte)_lastSolveMode,
                ScrubberBytePerColdTick = scrubberByteReductionPerColdTick,
                ScalabilityHigh = highScalability ? (byte)1 : (byte)0
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

        private float ResolveLogisticsPowerWatts()
        {
            float localPower = math.max(0f, math.isfinite(authoringLogisticsPowerWatts) ? authoringLogisticsPowerWatts : 0f);
            IPowerGridService powerGrid = GlobalRegistry.PowerGrid;
            if (powerGrid == null)
                return localPower;

            BatteryRuntimeSnapshot battery = powerGrid.BatterySnapshot;
            float netGeneration = powerGrid.TotalGeneration - powerGrid.TotalConsumption;
            float batteryCarrier = battery.TotalStoredEnergyWattSeconds > 1f ? 1f : 0f;
            return math.max(localPower, math.max(netGeneration, batteryCarrier));
        }

        private bool TryFinalizeDeferredNativeDisposal()
        {
            return DispatcherJobSwap.TryFinalizeCompleted(ref _disposeHandle);
        }

        private void DisposeNativeStateDeferred()
        {
            if (!_front.IsCreated)
                return;

            NativeMemorySentinel.UnregisterNativeArray(_front);
            NativeMemorySentinel.UnregisterNativeArray(_back);
            NativeMemorySentinel.UnregisterNativeArray(_carbonDioxideByteLane);

            if (_coldTickRunning)
            {
                JobHandle disposeHandle = _front.Dispose(_coldTickHandle);
                disposeHandle = _back.Dispose(disposeHandle);
                disposeHandle = _carbonDioxideByteLane.Dispose(disposeHandle);
                _disposeHandle = disposeHandle;
                _coldTickHandle = default;
                _coldTickRunning = false;
            }
            else
            {
                _front.Dispose();
                _back.Dispose();
                _carbonDioxideByteLane.Dispose();
            }

            _front = default;
            _back = default;
            _carbonDioxideByteLane = default;
            _seededDefaultAtmosphere = false;
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
