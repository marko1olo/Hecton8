using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using Hecton8.Core.Contracts;
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
    public sealed class AwaitableDropSequenceDirector : MonoBehaviour, IPrologueSequenceService
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

        private NativeArray<PrologueSequenceTelemetryEntry> _blackBox;
        private IPrologueSequenceRuntime _runtime;
        private PrologueStage _stage;
        private int _blackBoxCursor;
        private bool _configured;
        private bool _running;
        private bool _cancelRequested;
        private bool _disposed;
        private bool _blackBoxDumped;
        private bool _devSkipHandoffPublished;
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

        public bool IsConfigured => _configured;
        public bool IsRunning => _running;
        public PrologueStage CurrentStage => _stage;

        public void Configure(IPrologueSequenceRuntime runtime)
        {
            if (_disposed)
                return;

            _runtime = runtime;
            _configured = runtime != null;
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
            _inputLockReleased = false;
            _hasLastOrbitalSnapshot = false;
            _lastAtmosphericReentry = default;
            _lastComplete = default;
            _lastOrbital = default;
            _hasPublishedTelemetry = false;

            try
            {
                _runtime.PrepareSequenceRun();

                if (!await AwaitAtmosphericReentryAsync(cancellationToken))
                    return;

                if (!await RunOrbitalSilenceAsync(cancellationToken))
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

        private void OnDisable()
        {
            CancelSequence(PrologueCancelReasons.ExplicitCancel);
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
            if (_blackBox.IsCreated)
            {
                _blackBox.Dispose();
                _blackBoxCursor = 0;
            }
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

            RecordStage(PrologueStage.OrbitalSilence, SilenceHash, (byte)(PrologueInputLockFlags.Look | PrologueInputLockFlags.Translation));
            _runtime.PublishInputLock(PrologueInputLockFlags.Look | PrologueInputLockFlags.Translation, paused: true);
            _runtime.PublishMuffledBreathing(1f, 3f);
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

                if (_runtime.TryConsumeAtmosphericReentry(out _lastAtmosphericReentry))
                    RecordStage(PrologueStage.ReentryBurn, HashAtmospheric(in _lastAtmosphericReentry), _lastAtmosphericReentry.Flags);

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
                else if (_lastAtmosphericReentry.UniverseVelocityMetersPerSecond >= Mach10MetersPerSecond)
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
            _runtime.PublishInputLock(PrologueInputLockFlags.Translation, paused: true);
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
            RecordStage(PrologueStage.ImpactSync, ImpactHash, _lastComplete.Flags);
            if (ShouldStopForCancellation(cancellationToken))
                return false;

            if (TryHandleDevelopmentSkip())
                return false;

            await _runtime.NextFrameAsync(cancellationToken);
            return !ShouldStopForCancellation(cancellationToken) && !TryHandleDevelopmentSkip();
        }

        private async Awaitable<bool> AwaitOceanHydrationAsync(CancellationToken cancellationToken)
        {
            while (true)
            {
                if (ShouldStopForCancellation(cancellationToken))
                    return false;

                if (TryHandleDevelopmentSkip())
                    return false;

                bool allowProxy = _runtime.IsLowTier;
                byte hydrationMode = allowProxy
                    ? (byte)PrologueHydrationMode.LowTierProxySurface
                    : (byte)PrologueHydrationMode.HighResolutionSurface;

                if (_runtime.IsOceanSurfaceReady(allowProxy))
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
            if (_blackBox.IsCreated)
                return;

            _blackBox = new NativeArray<PrologueSequenceTelemetryEntry>(
                TelemetryCapacity,
                Allocator.Persistent,
                NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<PrologueSequenceTelemetryEntry>[300] - prologue sequence black-box ring - owner: AwaitableDropSequenceDirector
        }

        private void RecordStage(PrologueStage stage, uint stateHash, byte flags)
        {
            _stage = stage;

            if (_blackBox.IsCreated)
            {
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

                _blackBox[_blackBoxCursor] = entry;
                _blackBoxCursor = (_blackBoxCursor + 1) % _blackBox.Length;
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
            if (_inputLockReleased)
                return;

            IPrologueSequenceRuntime runtime = _runtime;
            if (runtime == null)
                return;

            try
            {
                runtime.PublishInputLock(PrologueInputLockFlags.None, paused: false);
                _inputLockReleased = true;
            }
            catch (Exception)
            {
                DumpBlackBox();
                TryDumpRuntimeBlackBox(runtime);
            }
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

        private void DumpBlackBox()
        {
            if (_blackBoxDumped || !_blackBox.IsCreated)
                return;

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
                    for (int i = 0; i < _blackBox.Length; i++)
                    {
                        int index = (_blackBoxCursor + i) % _blackBox.Length;
                        PrologueSequenceTelemetryEntry entry = _blackBox[index];
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

        [StructLayout(LayoutKind.Sequential)]
        private struct PrologueSequenceTelemetryEntry
        {
            public uint Frame;
            public uint StateHash;
            public double UniverseSpeedMetersPerSecond;
            public double PlanetDistanceMeters;
            public ushort Sequence;
            public byte Stage;
            public byte Flags;
        }
    }
}
