using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using Hecton8.Core;
using Hecton8.Core.Contracts;
using Hecton8.Core.Memory;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Narrative.Prologue
{
    /// <summary>
    /// Contract-only prologue pacing state machine. All concrete domain work is delegated to the runtime port.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-8550)]
    public sealed class AwaitableDropSequenceDirector : MonoBehaviour, IPrologueSequenceService, IGlobalRegistryHotSwapListener
    {
        private const int TelemetryCapacity = 300;
        private const double Mach10MetersPerSecond = 3430d;
        private const double Mach10MetersPerSecondSq = Mach10MetersPerSecond * Mach10MetersPerSecond;
        private const uint SourceHash = PrologueSignalSourceHashes.SequenceDirector;
        private const uint AwaitHash = 0x41574149u; // AWAI
        private const uint SilenceHash = 0x53494C45u; // SILE
        private const uint BurnHash = 0x4255524Eu; // BURN
        private const uint ManualHash = 0x4D414E55u; // MANU
        private const uint ImpactHash = 0x494D5041u; // IMPA
        private const uint HydrationHash = 0x48594452u; // HYDR
        private const uint WaterHash = 0x57415452u; // WATR
        private const uint CompleteHash = 0x444F4E45u; // DONE
        private const uint DevSkipHash = 0x534B4950u; // SKIP
        private const uint CancelHash = 0x43414E43u; // CANC
        private const uint FaultHash = 0x46414C54u; // FALT
        private const uint DumpFailedHash = 0x444D5046u; // DMPF
        private const string DumpFileName = "Dump_PROLOGUE_SEQUENCE_DIRECTOR.bin";
        private const SystemID OwnerSystemId = SystemID.PrologueSequence;

        private IDataVault _dataVault;
        private VaultGenerationHandle<PrologueSequenceTelemetryEntry> _blackBoxHandle;
        private IPrologueSequenceRuntime _runtime;
        private PrologueStage _stage;
        private int _blackBoxCursor;
        private bool _configured;
        private bool _running;
        private bool _cancelRequested;
        private bool _disposed;
        private bool _blackBoxDumped;
        private bool _devSkipHandoffPublished;
        private bool _inputLockAcquired;
        private bool _inputLockReleased;
        private byte _cancelReason;
        private PrologueAtmosphericReentrySnapshot _lastAtmosphericReentry;
        private PrologueCompleteSnapshot _lastComplete;
        private PrologueOrbitalSnapshot _lastOrbital;
        private bool _hasLastOrbitalSnapshot;
        private PrologueStage _lastPublishedStage;
        private uint _lastPublishedStateHash;
        private byte _lastPublishedFlags;
        private bool _hasPublishedTelemetry;
        private bool _registeredHotSwap;

        [Header("Impact Sync")]
        [Tooltip("Near-surface distance gate before hydration and world handoff.")]
        [SerializeField] private float impactSyncDistanceMeters = 120f;
        [Tooltip("Minimum post-release hold before the distance gate can complete.")]
        [SerializeField] private float impactSyncMinimumHoldSeconds = 0.65f;
        [Tooltip("Bounded fallback if no valid distance owner is available.")]
        [SerializeField] private float impactSyncWatchdogSeconds = 8f;

        public bool IsConfigured => _configured;
        public bool IsRunning => _running;
        public PrologueStage CurrentStage => _stage;

        public void Configure(IPrologueSequenceRuntime runtime)
        {
            if (_disposed)
                return;

            _runtime = runtime;
            _configured = runtime != null;
            TryRegisterHotSwap();
            EnsureBlackBox();
            RecordStage(PrologueStage.None, SourceHash, 0);
        }

        public async Awaitable RunPrologueSequenceAsync(CancellationToken cancellationToken)
        {
            if (!_configured || _runtime == null || _running || _disposed)
                return;

            _running = true;
            _cancelRequested = false;
            _cancelReason = 0;
            _blackBoxDumped = false;
            _devSkipHandoffPublished = false;
            _inputLockAcquired = false;
            _inputLockReleased = false;
            _hasLastOrbitalSnapshot = false;
            _lastAtmosphericReentry = default;
            _lastComplete = default;
            _lastOrbital = default;
            _hasPublishedTelemetry = false;

            try
            {
                _runtime.PrepareSequenceRun();

                if (!await RunOrbitalSilenceAsync(cancellationToken))
                    return;

                if (!await AwaitAtmosphericReentryAsync(cancellationToken))
                    return;

                if (!await RunReentryBurnAsync(cancellationToken))
                    return;

                if (!await RunManualOverrideAsync(cancellationToken))
                    return;

                if (!await RunImpactSyncAsync(cancellationToken))
                    return;

                if (!await AwaitOceanHydrationAsync(cancellationToken))
                    return;

                RunWaterTransition();
                RecordStage(PrologueStage.Complete, CompleteHash, 0);
            }
            catch (OperationCanceledException)
            {
                if (_cancelReason == PrologueCancelReasons.DevSkip)
                    TryExecuteDevelopmentSkipHandoff();
                else
                    RecordStage(
                        PrologueStage.Cancelled,
                        CancelHash,
                        _cancelReason == 0 ? PrologueCancelReasons.TokenCancelled : _cancelReason);
            }
            catch (Exception)
            {
                RecordStage(PrologueStage.Faulted, FaultHash, PrologueCancelReasons.NonFinite);
                DumpBlackBox();
                TryDumpRuntimeBlackBox(_runtime);
            }
            finally
            {
                _running = false;
                ReleaseInputLockNoThrow();
            }
        }

        public void CancelSequence(byte reason)
        {
            byte normalizedReason = reason == 0 ? PrologueCancelReasons.ExplicitCancel : reason;
            if (_cancelReason == PrologueCancelReasons.DevSkip &&
                normalizedReason != PrologueCancelReasons.DevSkip)
            {
                _cancelRequested = true;
                return;
            }

            _cancelRequested = true;
            _cancelReason = normalizedReason;
        }

        private void OnEnable()
        {
            TryRegisterHotSwap();
        }

        private void OnDisable()
        {
            CancelSequence(PrologueCancelReasons.ExplicitCancel);
            if (_running)
                ReleaseInputLockNoThrow();
            TryUnregisterHotSwap();
        }

        private void OnDestroy()
        {
            Dispose();
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            if (_running)
            {
                CancelSequence(PrologueCancelReasons.ExplicitCancel);
                ReleaseInputLockNoThrow();
            }

            _disposed = true;
            TryUnregisterHotSwap();
            ReleaseBlackBox();
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot != GlobalRegistryServiceSlot.DataVault)
                return;

            if (ReferenceEquals(previousService, currentService))
            {
                _dataVault = currentService as IDataVault;
                return;
            }

            ReleaseBlackBox(previousService as IDataVault ?? _dataVault);
            _dataVault = currentService as IDataVault;
            _blackBoxCursor = 0;
            EnsureBlackBox();
        }

        private async Awaitable<bool> AwaitAtmosphericReentryAsync(CancellationToken cancellationToken)
        {
            RecordStage(PrologueStage.AwaitingAtmosphericReentry, AwaitHash, 0);

            while (true)
            {
                if (ShouldStopForCancellation(cancellationToken))
                    return false;

                if (TryHandleDevelopmentSkip())
                    return false;

                if (_runtime.TryConsumeAtmosphericReentry(out _lastAtmosphericReentry))
                {
                    if (!IsFiniteAtmospheric(in _lastAtmosphericReentry))
                    {
                        RecordStage(PrologueStage.Faulted, FaultHash, PrologueCancelReasons.NonFinite);
                        DumpBlackBox();
                        TryDumpRuntimeBlackBox(_runtime);
                        return false;
                    }

                    RecordStage(PrologueStage.AwaitingAtmosphericReentry, HashAtmospheric(in _lastAtmosphericReentry), _lastAtmosphericReentry.Flags);
                    return true;
                }

                if (_runtime.TryGetOrbitalSnapshot(out _lastOrbital))
                {
                    _hasLastOrbitalSnapshot = true;
                    if (!IsFiniteOrbital(in _lastOrbital))
                    {
                        RecordStage(PrologueStage.Faulted, FaultHash, PrologueCancelReasons.NonFinite);
                        DumpBlackBox();
                        TryDumpRuntimeBlackBox(_runtime);
                        return false;
                    }

                    if (_lastOrbital.ReentryHeat01 > 0.001f)
                    {
                        RecordStage(PrologueStage.AwaitingAtmosphericReentry, HashOrbital(in _lastOrbital), _lastOrbital.Flags);
                        return true;
                    }
                }

                RecordStage(PrologueStage.AwaitingAtmosphericReentry, AwaitHash, 0);
                await _runtime.NextFrameAsync(cancellationToken);
            }
        }

        private async Awaitable<bool> RunOrbitalSilenceAsync(CancellationToken cancellationToken)
        {
            if (ShouldStopForCancellation(cancellationToken))
                return false;

            RecordStage(PrologueStage.OrbitalSilence, SilenceHash, 0);
            _runtime.PublishMuffledBreathing(0.65f, 3f);
            await _runtime.DelayDilatedAsync(3f, cancellationToken);
            return !ShouldStopForCancellation(cancellationToken) && !TryHandleDevelopmentSkip();
        }

        private async Awaitable<bool> RunReentryBurnAsync(CancellationToken cancellationToken)
        {
            RecordStage(PrologueStage.ReentryBurn, BurnHash, 0);
            _runtime.PublishHullTempCriticalWarning(1f);
            _runtime.PublishHeavyRumble(1f, 0.8f);

            while (true)
            {
                if (ShouldStopForCancellation(cancellationToken))
                    return false;

                if (TryHandleDevelopmentSkip())
                    return false;

                bool hasFreshAtmosphericReentry = _runtime.TryConsumeAtmosphericReentry(out _lastAtmosphericReentry);
                if (hasFreshAtmosphericReentry)
                {
                    if (!IsFiniteAtmospheric(in _lastAtmosphericReentry))
                    {
                        RecordStage(PrologueStage.Faulted, FaultHash, PrologueCancelReasons.NonFinite);
                        DumpBlackBox();
                        TryDumpRuntimeBlackBox(_runtime);
                        return false;
                    }

                    RecordStage(PrologueStage.ReentryBurn, HashAtmospheric(in _lastAtmosphericReentry), _lastAtmosphericReentry.Flags);
                }

                if (_runtime.TryGetOrbitalSnapshot(out _lastOrbital))
                {
                    _hasLastOrbitalSnapshot = true;
                    if (!IsFiniteOrbital(in _lastOrbital))
                    {
                        RecordStage(PrologueStage.Faulted, FaultHash, PrologueCancelReasons.NonFinite);
                        DumpBlackBox();
                        TryDumpRuntimeBlackBox(_runtime);
                        return false;
                    }

                    double speedSq = math.lengthsq(_lastOrbital.UniverseVelocity);
                    RecordStage(PrologueStage.ReentryBurn, HashOrbital(in _lastOrbital), _lastOrbital.Flags);
                    if (speedSq >= Mach10MetersPerSecondSq)
                        return true;
                }
                else if (hasFreshAtmosphericReentry && _lastAtmosphericReentry.UniverseVelocityMetersPerSecond >= Mach10MetersPerSecond)
                {
                    return true;
                }

                RecordStage(PrologueStage.ReentryBurn, BurnHash, 0);
                await _runtime.NextFrameAsync(cancellationToken);
            }
        }

        private async Awaitable<bool> RunManualOverrideAsync(CancellationToken cancellationToken)
        {
            RecordStage(PrologueStage.ManualOverride, ManualHash, (byte)PrologueInputLockFlags.Translation);
            PublishSequenceInputLock(PrologueInputLockFlags.Translation, paused: true);
            _runtime.PublishManualReleasePrompt();

            while (true)
            {
                if (ShouldStopForCancellation(cancellationToken))
                    return false;

                if (TryHandleDevelopmentSkip())
                    return false;

                if (_runtime.TryConsumePrologueComplete(out _lastComplete))
                {
                    RecordStage(PrologueStage.ManualOverride, HashComplete(in _lastComplete), _lastComplete.Flags);
                    return true;
                }

                RecordStage(PrologueStage.ManualOverride, ManualHash, (byte)PrologueInputLockFlags.Translation);
                await _runtime.NextFrameAsync(cancellationToken);
            }
        }

        private async Awaitable<bool> RunImpactSyncAsync(CancellationToken cancellationToken)
        {
            double elapsedSeconds = 0d;

            while (true)
            {
                if (ShouldStopForCancellation(cancellationToken))
                    return false;

                if (TryHandleDevelopmentSkip())
                    return false;

                bool rangeReached;
                uint impactStateHash;
                byte impactFlags;
                if (!TryResolveImpactRangeReached(out rangeReached, out impactStateHash, out impactFlags))
                    return false;

                RecordStage(PrologueStage.ImpactSync, impactStateHash, impactFlags);

                if (elapsedSeconds >= SanitizedNonNegative(impactSyncMinimumHoldSeconds, 0.65f) && rangeReached)
                    return true;

                if (elapsedSeconds >= SanitizedPositive(impactSyncWatchdogSeconds, 8f))
                    return true;

                await _runtime.NextFrameAsync(cancellationToken);
                elapsedSeconds += ResolveFrameDeltaSeconds();
            }
        }

        private async Awaitable<bool> AwaitOceanHydrationAsync(CancellationToken cancellationToken)
        {
            while (true)
            {
                if (ShouldStopForCancellation(cancellationToken))
                    return false;

                if (TryHandleDevelopmentSkip())
                    return false;

                if (_runtime.IsOceanSurfaceReady(allowProxy: false))
                {
                    RecordStage(
                        PrologueStage.AwaitOceanHydration,
                        HydrationHash,
                        (byte)PrologueHydrationMode.HighResolutionSurface);
                    return true;
                }

                float survivalProxyPressure01 = math.saturate(_runtime.SurvivalProxyPressure01);
                bool survivalProxyAllowed = survivalProxyPressure01 >=
                                            PrologueSequenceQualityPolicy.SurvivalProxyActivationThreshold01;
                bool handoffProxyAllowed = _runtime.IsStandaloneOrbitHandoffProxyAllowed;
                bool allowProxy = survivalProxyAllowed || handoffProxyAllowed;
                byte hydrationMode = handoffProxyAllowed
                    ? (byte)PrologueHydrationMode.StandaloneOrbitHandoffProxy
                    : survivalProxyAllowed
                        ? (byte)PrologueHydrationMode.SurvivalProxySurface
                        : (byte)PrologueHydrationMode.HighResolutionSurface;

                if (allowProxy && _runtime.IsOceanSurfaceReady(allowProxy: true))
                {
                    RecordStage(PrologueStage.AwaitOceanHydration, HydrationHash, hydrationMode);
                    return true;
                }

                RecordStage(
                    PrologueStage.AwaitOceanHydration,
                    HydrationHash,
                    hydrationMode);
                await _runtime.NextFrameAsync(cancellationToken);
            }
        }

        private void RunWaterTransition()
        {
            RecordStage(PrologueStage.WaterTransition, WaterHash, 0);
            _runtime.ZeroUniverseVelocity();
            _runtime.PublishMassiveImpact();
            _runtime.PublishOceanHandoff();
        }

        private bool TryResolveImpactRangeReached(out bool rangeReached, out uint stateHash, out byte flags)
        {
            rangeReached = false;
            stateHash = ImpactHash;
            flags = _lastComplete.Flags;
            double impactDistance = SanitizedPositive(impactSyncDistanceMeters, 120f);
            bool hasDistance = false;
            double nearestDistance = double.MaxValue;

            if (_runtime.TryGetOrbitalSnapshot(out _lastOrbital))
            {
                _hasLastOrbitalSnapshot = true;
                if (!IsFiniteOrbital(in _lastOrbital))
                {
                    RecordStage(PrologueStage.Faulted, FaultHash, PrologueCancelReasons.NonFinite);
                    DumpBlackBox();
                    TryDumpRuntimeBlackBox(_runtime);
                    return false;
                }

                nearestDistance = math.min(nearestDistance, math.max(0d, _lastOrbital.PlanetDistanceMeters));
                hasDistance = true;
                stateHash = HashOrbital(in _lastOrbital);
                flags = _lastOrbital.Flags;
            }

            if (_runtime.TryConsumeAtmosphericReentry(out _lastAtmosphericReentry))
            {
                if (!IsFiniteAtmospheric(in _lastAtmosphericReentry))
                {
                    RecordStage(PrologueStage.Faulted, FaultHash, PrologueCancelReasons.NonFinite);
                    DumpBlackBox();
                    TryDumpRuntimeBlackBox(_runtime);
                    return false;
                }

                nearestDistance = math.min(nearestDistance, math.max(0d, (double)_lastAtmosphericReentry.AltitudeMeters));
                hasDistance = true;
                stateHash = HashAtmospheric(in _lastAtmosphericReentry);
                flags = _lastAtmosphericReentry.Flags;
            }

            rangeReached = hasDistance && nearestDistance <= impactDistance;
            return true;
        }

        private void OnValidate()
        {
            impactSyncDistanceMeters = SanitizedPositive(impactSyncDistanceMeters, 120f);
            impactSyncMinimumHoldSeconds = SanitizedNonNegative(impactSyncMinimumHoldSeconds, 0.65f);
            impactSyncWatchdogSeconds = math.max(
                impactSyncMinimumHoldSeconds,
                SanitizedPositive(impactSyncWatchdogSeconds, 8f));
        }

        private static double ResolveFrameDeltaSeconds()
        {
            float delta = SystemDispatcher.CurrentFrameDeltaTime;
            return math.isfinite(delta) && delta > 0f ? delta : 1d / 60d;
        }

        private static float SanitizedPositive(float value, float fallback)
        {
            return math.isfinite(value) && value > 0f ? value : fallback;
        }

        private static float SanitizedNonNegative(float value, float fallback)
        {
            return math.isfinite(value) && value >= 0f ? value : fallback;
        }

        private bool ShouldStopForCancellation(CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                if (_cancelReason == PrologueCancelReasons.DevSkip)
                    TryExecuteDevelopmentSkipHandoff();
                else
                    RecordStage(
                        PrologueStage.Cancelled,
                        CancelHash,
                        _cancelReason == 0 ? PrologueCancelReasons.TokenCancelled : _cancelReason);
                return true;
            }

            if (_cancelRequested)
            {
                if (_cancelReason == PrologueCancelReasons.DevSkip)
                    TryExecuteDevelopmentSkipHandoff();
                else
                    RecordStage(PrologueStage.Cancelled, CancelHash, _cancelReason);
                return true;
            }

            return false;
        }

        private bool TryHandleDevelopmentSkip()
        {
            if (!_runtime.IsDevelopmentBuild || !_runtime.ShouldSkipPrologue)
                return false;

            CancelSequence(PrologueCancelReasons.DevSkip);
            TryExecuteDevelopmentSkipHandoff();
            return true;
        }

        private void TryExecuteDevelopmentSkipHandoff()
        {
            if (_devSkipHandoffPublished)
                return;

            _devSkipHandoffPublished = true;
            try
            {
                ExecuteDevelopmentSkipHandoff();
            }
            catch (Exception)
            {
                RecordStage(PrologueStage.Faulted, DevSkipHash, PrologueCancelReasons.DevSkip);
                DumpBlackBox();
                TryDumpRuntimeBlackBox(_runtime);
                ReleaseInputLockNoThrow();
            }
        }

        private void ExecuteDevelopmentSkipHandoff()
        {
            RecordStage(PrologueStage.DevSkip, DevSkipHash, (byte)PrologueHydrationMode.DevForcedShallowWater);
            _runtime.ForceShallowWaterHydration();
            _runtime.ZeroUniverseVelocity();
            _runtime.PublishMassiveImpact();
            _runtime.PublishOceanHandoff();
            ReleaseInputLockNoThrow();
        }

        private void EnsureBlackBox()
        {
            IDataVault vault = CacheDataVaultCold();
            if (vault == null)
                return;

            if (vault.IsCompactionFenceActive)
            {
                ClearBlackBoxDescriptor();
                return;
            }

            if (IsVaultHandleCreated(in _blackBoxHandle) &&
                vault.TryReadOnlyHandle(in _blackBoxHandle, out NativeArray<PrologueSequenceTelemetryEntry>.ReadOnly buffer) &&
                buffer.IsCreated &&
                buffer.Length >= TelemetryCapacity)
            {
                return;
            }

            if (IsVaultHandleCreated(in _blackBoxHandle))
                vault.ReleaseBuffer(in _blackBoxHandle);
            ClearBlackBoxDescriptor();

            if (vault.TryGetGenerationHandle<PrologueSequenceTelemetryEntry>(
                    BufferID.PrologueSequenceTelemetryRing,
                    out VaultGenerationHandle<PrologueSequenceTelemetryEntry> existing) &&
                vault.TryReadOnlyHandle(in existing, out NativeArray<PrologueSequenceTelemetryEntry>.ReadOnly existingBuffer) &&
                existingBuffer.IsCreated &&
                existingBuffer.Length >= TelemetryCapacity)
            {
                _blackBoxHandle = existing;
                return;
            }

            if (vault.IsAllocationLocked)
                return;

            VaultGenerationHandle<PrologueSequenceTelemetryEntry> acquired = vault.EnsureGenerationHandle<PrologueSequenceTelemetryEntry>(
                BufferID.PrologueSequenceTelemetryRing,
                TelemetryCapacity,
                OwnerSystemId,
                NativeArrayOptions.ClearMemory);
            if (!IsVaultHandleCreated(in acquired) ||
                !vault.TryReadOnlyHandle(in acquired, out NativeArray<PrologueSequenceTelemetryEntry>.ReadOnly acquiredBuffer) ||
                !acquiredBuffer.IsCreated ||
                acquiredBuffer.Length < TelemetryCapacity)
            {
                ClearBlackBoxDescriptor();
                _blackBoxCursor = 0;
                return;
            }

            _blackBoxHandle = acquired;
        }

        private void RecordStage(PrologueStage stage, uint stateHash, byte flags)
        {
            _stage = stage;

            IDataVault vault = _dataVault;
            if (vault != null &&
                IsVaultHandleCreated(in _blackBoxHandle) &&
                vault.TryAcquireWriteLock(in _blackBoxHandle, OwnerSystemId, out NativeArray<PrologueSequenceTelemetryEntry> blackBox))
            {
                try
                {
                    if (!blackBox.IsCreated || blackBox.Length < TelemetryCapacity)
                        return;

                    PrologueSequenceTelemetryEntry entry = default;
                    entry.Frame = _runtime != null ? _runtime.CurrentFrame : 0u;
                    entry.StateHash = stateHash;
                    entry.Stage = (byte)stage;
                    entry.Flags = flags;
                    entry.Sequence = _lastComplete.Sequence != 0 ? _lastComplete.Sequence : _lastAtmosphericReentry.Sequence;
                    if (_hasLastOrbitalSnapshot)
                    {
                        entry.UniverseSpeedMetersPerSecond = ResolveTelemetrySpeedMetersPerSecond();
                        entry.PlanetDistanceMeters = _lastOrbital.PlanetDistanceMeters;
                    }

                    int index = math.clamp(_blackBoxCursor, 0, TelemetryCapacity - 1);
                    blackBox[index] = entry;
                    _blackBoxCursor = (index + 1) % TelemetryCapacity;
                }
                finally
                {
                    vault.ReleaseWriteLock(in _blackBoxHandle, OwnerSystemId);
                }
            }

            if (_runtime != null &&
                (!_hasPublishedTelemetry ||
                 _lastPublishedStage != stage ||
                 _lastPublishedStateHash != stateHash ||
                 _lastPublishedFlags != flags))
            {
                TryPushTelemetryNoThrow(stage, stateHash, flags);
                _lastPublishedStage = stage;
                _lastPublishedStateHash = stateHash;
                _lastPublishedFlags = flags;
                _hasPublishedTelemetry = true;
            }
        }

        private void ReleaseInputLockNoThrow()
        {
            if (_inputLockReleased || !_inputLockAcquired)
                return;

            IPrologueSequenceRuntime runtime = _runtime;
            if (runtime == null)
                return;

            try
            {
                runtime.PublishInputLock(PrologueInputLockFlags.None, paused: false);
                _inputLockReleased = true;
                _inputLockAcquired = false;
            }
            catch (Exception)
            {
                DumpBlackBox();
                TryDumpRuntimeBlackBox(runtime);
            }
        }

        private void PublishSequenceInputLock(PrologueInputLockFlags flags, bool paused)
        {
            _runtime.PublishInputLock(flags, paused);
            if (flags == PrologueInputLockFlags.None || !paused)
                return;

            _inputLockAcquired = true;
            _inputLockReleased = false;
        }

        private void TryDumpRuntimeBlackBox(IPrologueSequenceRuntime runtime)
        {
            if (runtime == null)
                return;

            try
            {
                runtime.DumpBlackBox();
            }
            catch (Exception)
            {
            }
        }

        private void TryPushTelemetryNoThrow(PrologueStage stage, uint stateHash, byte flags)
        {
            IPrologueSequenceRuntime runtime = _runtime;
            if (runtime == null)
                return;

            try
            {
                runtime.PushTelemetry(stage, stateHash, flags);
            }
            catch (Exception)
            {
            }
        }

        private double ResolveTelemetrySpeedMetersPerSecond()
        {
            double speedSq = math.lengthsq(_lastOrbital.UniverseVelocity);
            if (!math.isfinite(speedSq) || speedSq <= 0d)
                return 0d;

            float speedSqF = (float)math.min(speedSq, (double)float.MaxValue);
            return speedSqF * math.rsqrt(math.max(speedSqF, 0.000001f));
        }

        private static bool IsFiniteOrbital(in PrologueOrbitalSnapshot snapshot)
        {
            return math.all(math.isfinite(snapshot.UniverseVelocity)) &&
                   math.isfinite(snapshot.PlanetDistanceMeters) &&
                   math.isfinite(snapshot.ReentryHeat01) &&
                   math.isfinite(snapshot.CloudWhiteout01);
        }

        private static bool IsFiniteAtmospheric(in PrologueAtmosphericReentrySnapshot snapshot)
        {
            return math.isfinite(snapshot.AltitudeMeters) &&
                   math.isfinite(snapshot.UniverseVelocityMetersPerSecond) &&
                   math.isfinite(snapshot.Heat01);
        }

        private void DumpBlackBox()
        {
            IDataVault vault = _dataVault;
            if (_blackBoxDumped ||
                vault == null ||
                !IsVaultHandleCreated(in _blackBoxHandle) ||
                !vault.TryReadOnlyHandle(in _blackBoxHandle, out NativeArray<PrologueSequenceTelemetryEntry>.ReadOnly blackBox) ||
                !blackBox.IsCreated ||
                blackBox.Length <= 0)
            {
                return;
            }

            _blackBoxDumped = true;
            try
            {
                string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
                string folder = Path.Combine(projectRoot, "Docs", "AgentLogs");
                Directory.CreateDirectory(folder);
                string path = Path.Combine(folder, DumpFileName);

                using (BinaryWriter writer = new BinaryWriter(File.Open(path, FileMode.Create, FileAccess.Write, FileShare.Read)))
                {
                    writer.Write(SourceHash);
                    writer.Write(TelemetryCapacity);
                    writer.Write(_blackBoxCursor);
                    int length = math.min(TelemetryCapacity, blackBox.Length);
                    for (int i = 0; i < length; i++)
                    {
                        int index = (_blackBoxCursor + i) % length;
                        PrologueSequenceTelemetryEntry entry = blackBox[index];
                        writer.Write(entry.Frame);
                        writer.Write(entry.StateHash);
                        writer.Write(entry.UniverseSpeedMetersPerSecond);
                        writer.Write(entry.PlanetDistanceMeters);
                        writer.Write(entry.Sequence);
                        writer.Write(entry.Stage);
                        writer.Write(entry.Flags);
                    }
                }
            }
            catch (Exception)
            {
                TryPushTelemetryNoThrow(PrologueStage.Faulted, DumpFailedHash, 1);
            }
        }

        private IDataVault CacheDataVaultCold()
        {
            if (_dataVault == null)
                _dataVault = GlobalRegistry.DataVault;

            return _dataVault;
        }

        private void ReleaseBlackBox()
        {
            ReleaseBlackBox(_dataVault);
        }

        private void ReleaseBlackBox(IDataVault vault)
        {
            if (vault != null && IsVaultHandleCreated(in _blackBoxHandle))
                vault.ReleaseBuffer(in _blackBoxHandle);

            ClearBlackBoxDescriptor();
            _blackBoxCursor = 0;
        }

        private void ClearBlackBoxDescriptor()
        {
            _blackBoxHandle = default;
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

        private static bool IsVaultHandleCreated<T>(in VaultGenerationHandle<T> handle) where T : struct
        {
            return handle.BufferID != 0u && handle.Generation != 0u;
        }

        private static uint HashAtmospheric(in PrologueAtmosphericReentrySnapshot snapshot)
        {
            uint hash = 2166136261u;
            hash = Mix(hash, math.asuint(snapshot.AltitudeMeters));
            hash = Mix(hash, math.asuint(snapshot.UniverseVelocityMetersPerSecond));
            hash = Mix(hash, math.asuint(snapshot.Heat01));
            hash = Mix(hash, snapshot.Sequence);
            hash = Mix(hash, snapshot.Phase);
            hash = Mix(hash, snapshot.Flags);
            return hash;
        }

        private static uint HashComplete(in PrologueCompleteSnapshot snapshot)
        {
            uint hash = 2166136261u;
            hash = Mix(hash, snapshot.Frame);
            hash = Mix(hash, math.asuint(snapshot.WhiteoutHoldSeconds));
            hash = Mix(hash, snapshot.Sequence);
            hash = Mix(hash, snapshot.Phase);
            hash = Mix(hash, snapshot.Flags);
            return hash;
        }

        private static uint HashOrbital(in PrologueOrbitalSnapshot snapshot)
        {
            uint hash = 2166136261u;
            hash = Mix(hash, HashDouble(snapshot.UniverseVelocity.x));
            hash = Mix(hash, HashDouble(snapshot.UniverseVelocity.y));
            hash = Mix(hash, HashDouble(snapshot.UniverseVelocity.z));
            hash = Mix(hash, HashDouble(snapshot.PlanetDistanceMeters));
            hash = Mix(hash, math.asuint(snapshot.ReentryHeat01));
            hash = Mix(hash, math.asuint(snapshot.CloudWhiteout01));
            hash = Mix(hash, snapshot.Sequence);
            hash = Mix(hash, snapshot.MathLod);
            hash = Mix(hash, snapshot.Flags);
            return hash;
        }

        private static uint HashDouble(double value)
        {
            long bits = BitConverter.DoubleToInt64Bits(value);
            return unchecked((uint)bits ^ (uint)(bits >> 32));
        }

        private static uint Mix(uint hash, uint value)
        {
            return unchecked((hash ^ value) * 16777619u);
        }

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Explicit, Size = 64)]
        private struct PrologueSequenceTelemetryEntry
        {
            [System.Runtime.InteropServices.FieldOffset(0)]
            public double UniverseSpeedMetersPerSecond;
            [System.Runtime.InteropServices.FieldOffset(8)]
            public double PlanetDistanceMeters;
            [System.Runtime.InteropServices.FieldOffset(16)]
            public uint Frame;
            [System.Runtime.InteropServices.FieldOffset(20)]
            public uint StateHash;
            [System.Runtime.InteropServices.FieldOffset(24)]
            public ushort Sequence;
            [System.Runtime.InteropServices.FieldOffset(26)]
            public byte Stage;
            [System.Runtime.InteropServices.FieldOffset(27)]
            public byte Flags;
            [System.Runtime.InteropServices.FieldOffset(28)]
            private byte _pad0;
            [System.Runtime.InteropServices.FieldOffset(29)]
            private byte _pad1;
            [System.Runtime.InteropServices.FieldOffset(30)]
            private byte _pad2;
            [System.Runtime.InteropServices.FieldOffset(31)]
            private byte _pad3;
            [System.Runtime.InteropServices.FieldOffset(32)]
            private byte _pad4;
            [System.Runtime.InteropServices.FieldOffset(33)]
            private byte _pad5;
            [System.Runtime.InteropServices.FieldOffset(34)]
            private byte _pad6;
            [System.Runtime.InteropServices.FieldOffset(35)]
            private byte _pad7;
            [System.Runtime.InteropServices.FieldOffset(36)]
            private byte _pad8;
            [System.Runtime.InteropServices.FieldOffset(37)]
            private byte _pad9;
            [System.Runtime.InteropServices.FieldOffset(38)]
            private byte _pad10;
            [System.Runtime.InteropServices.FieldOffset(39)]
            private byte _pad11;
            [System.Runtime.InteropServices.FieldOffset(40)]
            private byte _pad12;
            [System.Runtime.InteropServices.FieldOffset(41)]
            private byte _pad13;
            [System.Runtime.InteropServices.FieldOffset(42)]
            private byte _pad14;
            [System.Runtime.InteropServices.FieldOffset(43)]
            private byte _pad15;
            [System.Runtime.InteropServices.FieldOffset(44)]
            private byte _pad16;
            [System.Runtime.InteropServices.FieldOffset(45)]
            private byte _pad17;
            [System.Runtime.InteropServices.FieldOffset(46)]
            private byte _pad18;
            [System.Runtime.InteropServices.FieldOffset(47)]
            private byte _pad19;
            [System.Runtime.InteropServices.FieldOffset(48)]
            private byte _pad20;
            [System.Runtime.InteropServices.FieldOffset(49)]
            private byte _pad21;
            [System.Runtime.InteropServices.FieldOffset(50)]
            private byte _pad22;
            [System.Runtime.InteropServices.FieldOffset(51)]
            private byte _pad23;
            [System.Runtime.InteropServices.FieldOffset(52)]
            private byte _pad24;
            [System.Runtime.InteropServices.FieldOffset(53)]
            private byte _pad25;
            [System.Runtime.InteropServices.FieldOffset(54)]
            private byte _pad26;
            [System.Runtime.InteropServices.FieldOffset(55)]
            private byte _pad27;
            [System.Runtime.InteropServices.FieldOffset(56)]
            private byte _pad28;
            [System.Runtime.InteropServices.FieldOffset(57)]
            private byte _pad29;
            [System.Runtime.InteropServices.FieldOffset(58)]
            private byte _pad30;
            [System.Runtime.InteropServices.FieldOffset(59)]
            private byte _pad31;
            [System.Runtime.InteropServices.FieldOffset(60)]
            private byte _pad32;
            [System.Runtime.InteropServices.FieldOffset(61)]
            private byte _pad33;
            [System.Runtime.InteropServices.FieldOffset(62)]
            private byte _pad34;
            [System.Runtime.InteropServices.FieldOffset(63)]
            private byte _pad35;
        }
    }
}
