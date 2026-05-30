using System.Threading;
using Hecton8.Core.Contracts.Signals;
using Unity.Mathematics;

namespace Hecton8.Core
{
    internal static class SignalBridgeState
    {
        private const float MilliScale = 1000f;
        private const float MaxMilliScalar = int.MaxValue / MilliScale;

        private static SurvivalVitalsChangedSignal s_latestSurvivalDeathSignal;
        private static int s_latestSurvivalDeathSignalSequence;
        private static int s_latestCraftingCompletedSignalSequence;
        private static int s_latestCraftingCompletedUnitCount;
        private static int s_timeDilationScalarMilli = 1000;
        private static int s_timeDilationSequence;
        private static int s_simulationPaused;
        private static int s_bulletTimeVisualMilli;
        private static int s_legacyPublishDropCount;

        public static float TimeDilationScalar => Volatile.Read(ref s_timeDilationScalarMilli) * 0.001f;

        public static bool SimulationPaused => Volatile.Read(ref s_simulationPaused) != 0;

        public static float BulletTimeVisualIntensity01 => Volatile.Read(ref s_bulletTimeVisualMilli) * 0.001f;

        public static int LegacyPublishDropCount => Volatile.Read(ref s_legacyPublishDropCount);

        public static uint LatestCraftingCompletedSequence => unchecked((uint)Volatile.Read(ref s_latestCraftingCompletedSignalSequence));

        public static uint LatestCraftingCompletedUnitCount => unchecked((uint)Volatile.Read(ref s_latestCraftingCompletedUnitCount));

        public static void RecordTimeDilation(in TimeDilationSignal signal)
        {
            Volatile.Write(ref s_timeDilationScalarMilli, ToNonNegativeMilli(signal.Scalar, 1f));
            Volatile.Write(ref s_timeDilationSequence, unchecked((int)signal.Sequence));
        }

        public static void RecordSimulationPause(in SimulationPauseSignal signal)
        {
            Volatile.Write(ref s_simulationPaused, signal.Paused != 0 ? 1 : 0);
        }

        public static void RecordBulletTimeVisual(in BulletTimeVisualSignal signal)
        {
            Volatile.Write(ref s_bulletTimeVisualMilli, ToSaturatedMilli(signal.Intensity01));
        }

        public static CraftingCompletedSignal RecordCraftingCompleted(in CraftingCompletedSignal signal)
        {
            CraftingCompletedSignal sequencedSignal = signal;
            AdvanceSignalSequence(ref s_latestCraftingCompletedSignalSequence);
            sequencedSignal.Sequence = unchecked((uint)Volatile.Read(ref s_latestCraftingCompletedSignalSequence));
            if (sequencedSignal.Quantity > 0)
                AdvanceSignalCounter(ref s_latestCraftingCompletedUnitCount, sequencedSignal.Quantity);

            return sequencedSignal;
        }

        public static void RecordSurvivalVitals(in SurvivalVitalsChangedSignal signal)
        {
            if ((signal.Flags & SurvivalVitalsChangedSignalFlags.Death) == 0u)
                return;

            s_latestSurvivalDeathSignal = signal;
            AdvanceSignalSequence(ref s_latestSurvivalDeathSignalSequence);
        }

        public static void RecordLegacyPublishDrop()
        {
            int current = Volatile.Read(ref s_legacyPublishDropCount);
            if (current < int.MaxValue)
                Interlocked.Increment(ref s_legacyPublishDropCount);
        }

        public static bool TryGetLatestSurvivalDeath(out SurvivalVitalsChangedSignal signal, out int sequence)
        {
            sequence = Volatile.Read(ref s_latestSurvivalDeathSignalSequence);
            signal = s_latestSurvivalDeathSignal;
            return sequence != 0;
        }

        public static void Reset()
        {
            s_latestSurvivalDeathSignal = default;
            Volatile.Write(ref s_latestSurvivalDeathSignalSequence, 0);
            Volatile.Write(ref s_latestCraftingCompletedSignalSequence, 0);
            Volatile.Write(ref s_latestCraftingCompletedUnitCount, 0);
            Volatile.Write(ref s_timeDilationScalarMilli, 1000);
            Volatile.Write(ref s_timeDilationSequence, 0);
            Volatile.Write(ref s_simulationPaused, 0);
            Volatile.Write(ref s_bulletTimeVisualMilli, 0);
            Volatile.Write(ref s_legacyPublishDropCount, 0);
        }

        private static void AdvanceSignalSequence(ref int sequence)
        {
            int next = unchecked(Volatile.Read(ref sequence) + 1);
            if (next == 0)
                next = 1;

            Volatile.Write(ref sequence, next);
        }

        private static void AdvanceSignalCounter(ref int counter, int amount)
        {
            if (amount <= 0)
                return;

            int current = Volatile.Read(ref counter);
            int next = current > int.MaxValue - amount
                ? int.MaxValue
                : current + amount;
            Volatile.Write(ref counter, next);
        }

        private static int ToNonNegativeMilli(float value, float fallback)
        {
            float safeValue = math.isfinite(value) ? value : fallback;
            safeValue = math.clamp(safeValue, 0f, MaxMilliScalar);
            return (int)math.round(safeValue * MilliScale);
        }

        private static int ToSaturatedMilli(float value)
        {
            float safeValue = math.isfinite(value) ? math.saturate(value) : 0f;
            return (int)math.round(safeValue * MilliScale);
        }
    }
}
