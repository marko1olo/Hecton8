using Hecton8.Core.Contracts.Signals;
using System;
using UnityEngine;

namespace Hecton8.Core
{
    public static class AupSignalRoute
    {
        [Obsolete("Use TryQueuePreShift(in AupPreShiftSignal) so overflow/drop semantics stay visible at the producer.", true)]
        public static void QueuePreShift(in AupPreShiftSignal signal)
        {
            TryQueuePreShift(in signal);
        }

        public static bool TryQueuePreShift(in AupPreShiftSignal signal)
        {
            SignalCorridorRuntime.EnsureInitialized();
            AupPreShiftSignal sanitizedSignal = signal;
            int guardCode = SignalPayloadFiniteGuards.Sanitize(ref sanitizedSignal);
            if (guardCode != 0)
                GlobalTelemetryBus.PublishMathGuardInvalidNumber(guardCode);

            uint shiftFrameId = sanitizedSignal.ShiftFrameId != 0u ? sanitizedSignal.ShiftFrameId : Hecton8.Core.SystemDispatcher.CurrentFrameId;
            SystemDispatcher dispatcher = SystemDispatcher.ActiveRuntimeInstance;
            if (dispatcher != null)
                dispatcher.RequestAupPreShiftPause(shiftFrameId);

            return SignalBus<AupPreShiftSignal>.TryPush(in sanitizedSignal);
        }

        [Obsolete("Use TryQueueShift(in AupShiftSignal) so overflow/drop semantics stay visible at the producer.", true)]
        public static void QueueShift(in AupShiftSignal signal)
        {
            TryQueueShift(in signal);
        }

        public static bool TryQueueShift(in AupShiftSignal signal)
        {
            SignalCorridorRuntime.EnsureInitialized();
            AupShiftSignal sanitizedSignal = signal;
            int guardCode = SignalPayloadFiniteGuards.Sanitize(ref sanitizedSignal);
            if (guardCode != 0)
                GlobalTelemetryBus.PublishMathGuardInvalidNumber(guardCode);

            SystemDispatcher dispatcher = SystemDispatcher.ActiveRuntimeInstance;
            if (dispatcher != null)
                dispatcher.ReleaseAupPreShiftPause(sanitizedSignal.ShiftFrameId);

            return SignalBus<AupShiftSignal>.TryPush(in sanitizedSignal);
        }
    }

    public static class SimulationSignalRoute
    {
        public static float TimeDilationScalar => SignalBridgeState.TimeDilationScalar;

        public static bool SimulationPaused => SignalBridgeState.SimulationPaused;

        public static float BulletTimeVisualIntensity01 => SignalBridgeState.BulletTimeVisualIntensity01;

        [Obsolete("Use TryQueueTimeDilation(in TimeDilationSignal) so overflow/drop semantics stay visible at the producer.", true)]
        public static void QueueTimeDilation(in TimeDilationSignal signal)
        {
            TryQueueTimeDilation(in signal);
        }

        public static bool TryQueueTimeDilation(in TimeDilationSignal signal)
        {
            SignalCorridorRuntime.EnsureInitialized();
            TimeDilationSignal sanitizedSignal = signal;
            int guardCode = SignalPayloadFiniteGuards.Sanitize(ref sanitizedSignal);
            if (guardCode != 0)
                GlobalTelemetryBus.PublishMathGuardInvalidNumber(guardCode);

            SignalBridgeState.RecordTimeDilation(in sanitizedSignal);
            return SignalBus<TimeDilationSignal>.TryPush(in sanitizedSignal);
        }

        [Obsolete("Use TryQueuePause(in SimulationPauseSignal) so overflow/drop semantics stay visible at the producer.", true)]
        public static void QueuePause(in SimulationPauseSignal signal)
        {
            TryQueuePause(in signal);
        }

        public static bool TryQueuePause(in SimulationPauseSignal signal)
        {
            SignalCorridorRuntime.EnsureInitialized();
            SimulationPauseSignal sanitizedSignal = signal;
            int guardCode = SignalPayloadFiniteGuards.Sanitize(ref sanitizedSignal);
            if (guardCode != 0)
                GlobalTelemetryBus.PublishMathGuardInvalidNumber(guardCode);

            SignalBridgeState.RecordSimulationPause(in sanitizedSignal);
            bool queued = SignalBus<SimulationPauseSignal>.TryPush(in sanitizedSignal);

            SystemPauseSignal pauseSignal = default;
            pauseSignal.SourceHash = sanitizedSignal.SourceHash;
            pauseSignal.Frame = sanitizedSignal.Frame;
            pauseSignal.Sequence = sanitizedSignal.Sequence;
            pauseSignal.Paused = sanitizedSignal.Paused;
            pauseSignal.Flags = sanitizedSignal.Flags;
            pauseSignal.RestoreScalar = sanitizedSignal.RestoreScalar;
            return SignalBus<SystemPauseSignal>.TryPush(in pauseSignal) && queued;
        }

        [Obsolete("Use TryQueueBulletTimeVisual(in BulletTimeVisualSignal) so overflow/drop semantics stay visible at the producer.", true)]
        public static void QueueBulletTimeVisual(in BulletTimeVisualSignal signal)
        {
            TryQueueBulletTimeVisual(in signal);
        }

        public static bool TryQueueBulletTimeVisual(in BulletTimeVisualSignal signal)
        {
            SignalCorridorRuntime.EnsureInitialized();
            BulletTimeVisualSignal sanitizedSignal = signal;
            int guardCode = SignalPayloadFiniteGuards.Sanitize(ref sanitizedSignal);
            if (guardCode != 0)
                GlobalTelemetryBus.PublishMathGuardInvalidNumber(guardCode);

            SignalBridgeState.RecordBulletTimeVisual(in sanitizedSignal);
            return SignalBus<BulletTimeVisualSignal>.TryPush(in sanitizedSignal);
        }
    }

    public static class CraftingSignalRoute
    {
        public static uint LatestCompletedUnitCount => SignalBridgeState.LatestCraftingCompletedUnitCount;

        [Obsolete("Use TryQueueCompleted(in CraftingCompletedSignal) so overflow/drop semantics stay visible at the producer.", true)]
        public static void QueueCompleted(in CraftingCompletedSignal signal)
        {
            TryQueueCompleted(in signal);
        }

        public static bool TryQueueCompleted(in CraftingCompletedSignal signal)
        {
            SignalCorridorRuntime.EnsureInitialized();
            CraftingCompletedSignal sequencedSignal = SignalBridgeState.RecordCraftingCompleted(in signal);
            return SignalBus<CraftingCompletedSignal>.TryPush(in sequencedSignal);
        }
    }

    public static class SurvivalSignalRoute
    {
        public static bool TryGetLatestDeath(out SurvivalVitalsChangedSignal signal, out int sequence)
        {
            return SignalBridgeState.TryGetLatestSurvivalDeath(out signal, out sequence);
        }

        [Obsolete("Use TryQueueVitals(in SurvivalVitalsChangedSignal) so overflow/drop semantics stay visible at the producer.", true)]
        public static void QueueVitals(in SurvivalVitalsChangedSignal signal)
        {
            TryQueueVitals(in signal);
        }

        public static bool TryQueueVitals(in SurvivalVitalsChangedSignal signal)
        {
            SignalCorridorRuntime.EnsureInitialized();
            SignalBridgeState.RecordSurvivalVitals(in signal);
            return SignalBus<SurvivalVitalsChangedSignal>.TryPush(in signal);
        }
    }

    public static class ScannerSignalRoute
    {
        public static bool TryGetLatestActive(out ScannerToolActiveSignal signal, out int sequence)
        {
            return SignalBus<ScannerToolActiveSignal>.TryGetLatest(out signal, out sequence);
        }
    }
}
