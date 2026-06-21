using Hecton8.Core.Contracts;
using Hecton8.Core.Contracts.Signals;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Core
{
    /// <summary>
    /// Core-side telemetry bridge for the isolated scheduling assembly.
    /// </summary>
    internal sealed class JobAdmissionTelemetryBridge : IJobAdmissionTelemetrySink
    {
        private static int s_x001JobAdmissionTelemetryBridgeSignalPushDropCount;

        /// <inheritdoc />
        public void ReportAdmissionDenied(JobAdmissionLane lane, uint jobHash, float estimatedCostMs, float remainingBudgetMs, int criticalDebtFrames, byte reasonFlags)
        {
            byte safeFlags = (byte)(reasonFlags | JobAdmissionTelemetryFlags.Denied);
            CpuStarvationSignal signal = new CpuStarvationSignal
            {
                JobHash = jobHash,
                Frame = Hecton8.Core.SystemDispatcher.CurrentFrameId,
                EstimatedCostMs = math.isfinite(estimatedCostMs) ? estimatedCostMs : 0f,
                RemainingBudgetMs = math.isfinite(remainingBudgetMs) ? remainingBudgetMs : 0f,
                CriticalDebtFrames = criticalDebtFrames,
                Lane = (byte)lane,
                Flags = safeFlags
            };

            SignalBus<CpuStarvationSignal>.TryPushTracked(in signal, ref s_x001JobAdmissionTelemetryBridgeSignalPushDropCount);
            CrashTelemetryBuffer.JobAdmissionTelemetryArgs args = new CrashTelemetryBuffer.JobAdmissionTelemetryArgs(
                signal.Lane,
                signal.JobHash,
                signal.EstimatedCostMs,
                signal.RemainingBudgetMs,
                signal.CriticalDebtFrames,
                signal.Flags);
            CrashTelemetryBuffer.ReportJobAdmissionState(in args);
        }

        /// <inheritdoc />
        public void ReportLaneState(JobAdmissionLane lane, float budgetMs, float refillMs, int criticalDebtFrames, uint killSwitchMask)
        {
            CrashTelemetryBuffer.ReportJobAdmissionLaneState(
                (byte)lane,
                math.isfinite(budgetMs) ? budgetMs : 0f,
                math.isfinite(refillMs) ? refillMs : 0f,
                criticalDebtFrames,
                killSwitchMask);
        }

        /// <inheritdoc />
        public void ReportCostState(int slotIndex, uint jobHash, float ewmaCostMs, int costSlotCount, float overflowEwmaCostMs)
        {
            CrashTelemetryBuffer.ReportJobAdmissionCostState(
                slotIndex,
                jobHash,
                math.isfinite(ewmaCostMs) ? ewmaCostMs : 0f,
                costSlotCount,
                math.isfinite(overflowEwmaCostMs) ? overflowEwmaCostMs : 0f);
        }

        /// <inheritdoc />
        public void ReportNonFiniteAdmissionState(JobAdmissionLane lane, uint jobHash, float value, int criticalDebtFrames)
        {
            CpuStarvationSignal signal = new CpuStarvationSignal
            {
                JobHash = jobHash,
                Frame = Hecton8.Core.SystemDispatcher.CurrentFrameId,
                EstimatedCostMs = 0f,
                RemainingBudgetMs = 0f,
                CriticalDebtFrames = criticalDebtFrames,
                Lane = (byte)lane,
                Flags = (byte)(JobAdmissionTelemetryFlags.Denied | JobAdmissionTelemetryFlags.NonFinite)
            };

            SignalBus<CpuStarvationSignal>.TryPushTracked(in signal, ref s_x001JobAdmissionTelemetryBridgeSignalPushDropCount);
            CrashTelemetryBuffer.ReportJobAdmissionNonFinite(signal.Lane, signal.JobHash, value);
        }
    }
}
