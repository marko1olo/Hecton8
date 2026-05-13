using Hecton8.Core.Contracts;
using Hecton8.Core.Signals;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Core
{
    /// <summary>
    /// Core-side telemetry bridge for the isolated scheduling assembly.
    /// </summary>
    internal sealed class JobAdmissionTelemetryBridge : IJobAdmissionTelemetrySink
    {
        private const byte StarvedFlag = 1;
        private const byte NonFiniteFlag = 2;

        /// <inheritdoc />
        public void ReportAdmissionDenied(JobAdmissionLane lane, uint jobHash, float estimatedCostMs, float remainingBudgetMs, int criticalDebtFrames)
        {
            CpuStarvationSignal signal = new CpuStarvationSignal
            {
                JobHash = jobHash,
                Frame = unchecked((uint)Time.frameCount),
                EstimatedCostMs = math.isfinite(estimatedCostMs) ? estimatedCostMs : 0f,
                RemainingBudgetMs = math.isfinite(remainingBudgetMs) ? remainingBudgetMs : 0f,
                CriticalDebtFrames = criticalDebtFrames,
                Lane = (byte)lane,
                Flags = StarvedFlag
            };

            GlobalSignals.Publish(in signal);
            CrashTelemetryBuffer.ReportJobAdmissionState(
                signal.Lane,
                signal.JobHash,
                signal.EstimatedCostMs,
                signal.RemainingBudgetMs,
                signal.CriticalDebtFrames,
                signal.Flags);
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
        public void ReportNonFiniteAdmissionState(JobAdmissionLane lane, uint jobHash, float value)
        {
            CpuStarvationSignal signal = new CpuStarvationSignal
            {
                JobHash = jobHash,
                Frame = unchecked((uint)Time.frameCount),
                EstimatedCostMs = 0f,
                RemainingBudgetMs = 0f,
                CriticalDebtFrames = GlobalRegistry.JobAdmission != null ? GlobalRegistry.JobAdmission.CriticalDebtFrameCount : 0,
                Lane = (byte)lane,
                Flags = NonFiniteFlag
            };

            GlobalSignals.Publish(in signal);
            CrashTelemetryBuffer.ReportJobAdmissionNonFinite(signal.Lane, signal.JobHash, value);
        }
    }
}
