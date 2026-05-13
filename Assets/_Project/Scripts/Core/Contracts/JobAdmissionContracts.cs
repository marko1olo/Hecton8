using System;

namespace Hecton8.Core.Contracts
{
    /// <summary>
    /// Fixed worker-admission lane IDs. The numeric values are ABI stable for Burst/job callers.
    /// </summary>
    public enum JobAdmissionLane : byte
    {
        /// <summary>Player-critical physics and kinematics. May borrow from lower lanes.</summary>
        Lane0_Critical = 0,

        /// <summary>World residency, coarse collision, and streaming-adjacent CPU work.</summary>
        Lane1_World = 1,

        /// <summary>Voxel meshing and terrain topology work.</summary>
        Lane2_Voxel = 2,

        /// <summary>Fauna, cognition, migration, and background AI work.</summary>
        Lane3_AI = 3,

        /// <summary>Presentation-only VFX and non-authoritative visual jobs.</summary>
        Lane4_VFX = 4,

        /// <summary>Save, compression, metadata, and cold IO jobs.</summary>
        Lane5_IO = 5
    }

    /// <summary>
    /// Admission lane constants shared by bootstrap, scheduling wrappers, and diagnostics.
    /// </summary>
    public static class JobAdmissionLanes
    {
        /// <summary>Number of fixed token-bucket lanes.</summary>
        public const int Count = 6;

        /// <summary>Lane index for player-critical physics and kinematics.</summary>
        public const int Lane0Critical = 0;

        /// <summary>Lane index for world residency and broad collision.</summary>
        public const int Lane1World = 1;

        /// <summary>Lane index for voxel meshing and topology.</summary>
        public const int Lane2Voxel = 2;

        /// <summary>Lane index for AI and fauna cognition.</summary>
        public const int Lane3AI = 3;

        /// <summary>Lane index for presentation-only VFX.</summary>
        public const int Lane4VFX = 4;

        /// <summary>Lane index for save/compression/IO.</summary>
        public const int Lane5IO = 5;
    }

    /// <summary>
    /// Lightweight admission telemetry sink implemented by Core. Scheduling remains isolated from Core runtime types.
    /// </summary>
    public interface IJobAdmissionTelemetrySink
    {
        /// <summary>Reports a denied low-priority job admission.</summary>
        void ReportAdmissionDenied(JobAdmissionLane lane, uint jobHash, float estimatedCostMs, float remainingBudgetMs, int criticalDebtFrames);

        /// <summary>Reports finite lane state for black-box retention.</summary>
        void ReportLaneState(JobAdmissionLane lane, float budgetMs, float refillMs, int criticalDebtFrames, uint killSwitchMask);

        /// <summary>Reports a non-finite admission state that requires a crash telemetry dump.</summary>
        void ReportNonFiniteAdmissionState(JobAdmissionLane lane, uint jobHash, float value);
    }

    /// <summary>
    /// Token-bucket admission gate for Burst/Jobs work. Implementations must be zero-GC in frame paths.
    /// </summary>
    public interface IJobAdmissionService : IDisposable
    {
        /// <summary>True after persistent native buffers are created and the service is ready for dispatcher use.</summary>
        bool IsInitialized { get; }

        /// <summary>Current VFX kill-switch mask emitted after sustained critical debt.</summary>
        uint SystemKillSwitchMask { get; }

        /// <summary>Consecutive frames where critical lane debt remained below zero.</summary>
        int CriticalDebtFrameCount { get; }

        /// <summary>True while an AUP pre-shift barrier is active and non-critical scheduling must defer.</summary>
        bool AupBarrierActive { get; }

        /// <summary>Initializes native storage and binds the telemetry sink.</summary>
        /// <param name="telemetrySink">Core-owned telemetry bridge. May be null only in isolated tests.</param>
        void Initialize(IJobAdmissionTelemetrySink telemetrySink);

        /// <summary>Refills lane budgets once at the PRE_SIMULATION dispatcher boundary.</summary>
        /// <param name="scalabilityTierProfile">0 = low/MX350, 1 = higher tier.</param>
        /// <param name="deltaTimeSeconds">Unscaled frame delta in seconds.</param>
        /// <param name="previousFrameMissedBudget">True when the previous frame exceeded the target budget.</param>
        void Refill(byte scalabilityTierProfile, float deltaTimeSeconds, bool previousFrameMissedBudget);

        /// <summary>Attempts to reserve tokens for a job before scheduling it.</summary>
        /// <param name="lane">Job lane.</param>
        /// <param name="jobHash">Stable FNV1a hash of the job struct type name.</param>
        /// <param name="estimatedCostMs">Current EWMA estimate used for the admission decision.</param>
        /// <returns>True if the job may be scheduled this frame.</returns>
        bool TryAdmitJob(JobAdmissionLane lane, uint jobHash, out float estimatedCostMs);

        /// <summary>Feeds measured completion cost back into the EWMA table.</summary>
        /// <param name="lane">Job lane.</param>
        /// <param name="jobHash">Stable FNV1a hash of the job struct type name.</param>
        /// <param name="measuredCompleteMs">Measured completion cost in milliseconds.</param>
        void ReportJobCompleted(JobAdmissionLane lane, uint jobHash, float measuredCompleteMs);

        /// <summary>Sets the AUP pre-shift barrier state.</summary>
        /// <param name="active">True while pre-shift safety is active.</param>
        void SetAupBarrierActive(bool active);

        /// <summary>Reads one lane budget for diagnostics.</summary>
        /// <param name="lane">Job lane.</param>
        /// <returns>Remaining budget in milliseconds.</returns>
        float GetLaneBudgetMs(JobAdmissionLane lane);

        /// <summary>Reads an EWMA cost estimate for diagnostics.</summary>
        /// <param name="jobHash">Stable FNV1a hash.</param>
        /// <returns>Estimated cost in milliseconds, or the cold default.</returns>
        float GetEstimatedCostMs(uint jobHash);
    }
}
