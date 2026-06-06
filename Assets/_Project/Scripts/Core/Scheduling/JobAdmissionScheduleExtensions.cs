using Hecton8.Core.Contracts;
using Unity.Jobs;
using Unity.Mathematics;

namespace Hecton8.Core.Scheduling
{
    /// <summary>
    /// Admission-aware wrappers for Unity job scheduling.
    /// </summary>
    public static class JobAdmissionScheduleExtensions
    {
        /// <summary>
        /// Schedules an <see cref="IJob"/> only after token-bucket admission.
        /// </summary>
        public static JobHandle ScheduleAdmitted<TJob>(
            this TJob jobData,
            JobAdmissionLane lane,
            JobHandle dependsOn = default)
            where TJob : struct, IJob
        {
            TryScheduleAdmitted(jobData, lane, dependsOn, out JobHandle handle);
            return handle;
        }

        /// <summary>
        /// Attempts to schedule an <see cref="IJob"/> only after token-bucket admission.
        /// </summary>
        public static bool TryScheduleAdmitted<TJob>(
            this TJob jobData,
            JobAdmissionLane lane,
            JobHandle dependsOn,
            out JobHandle handle)
            where TJob : struct, IJob
        {
            IJobAdmissionService service = JobAdmissionSchedulerBridge.Service;
            uint jobHash = JobAdmissionHash<TJob>.Value;
            if (service != null && !service.TryAdmitJob(lane, jobHash, out _))
            {
                handle = dependsOn;
                return false;
            }

            handle = jobData.Schedule(dependsOn);
            return true;
        }

        /// <summary>
        /// Schedules an <see cref="IJobParallelFor"/> only after token-bucket admission.
        /// </summary>
        public static JobHandle ScheduleParallelAdmitted<TJob>(
            this TJob jobData,
            int arrayLength,
            int innerloopBatchCount,
            JobAdmissionLane lane,
            JobHandle dependsOn = default)
            where TJob : struct, IJobParallelFor
        {
            TryScheduleParallelAdmitted(jobData, arrayLength, innerloopBatchCount, lane, dependsOn, out JobHandle handle);
            return handle;
        }

        /// <summary>
        /// Attempts to schedule an <see cref="IJobParallelFor"/> only after token-bucket admission.
        /// </summary>
        public static bool TryScheduleParallelAdmitted<TJob>(
            this TJob jobData,
            int arrayLength,
            int innerloopBatchCount,
            JobAdmissionLane lane,
            JobHandle dependsOn,
            out JobHandle handle)
            where TJob : struct, IJobParallelFor
        {
            if (arrayLength <= 0)
            {
                handle = dependsOn;
                return arrayLength == 0;
            }

            uint jobHash = JobAdmissionHash<TJob>.Value;
            int safeBatchCount = ResolveProfiledInnerloopBatchCount(jobHash, arrayLength, innerloopBatchCount);
            IJobAdmissionService service = JobAdmissionSchedulerBridge.Service;
            if (service != null && !service.TryAdmitJob(lane, jobHash, out _))
            {
                handle = dependsOn;
                return false;
            }

            handle = jobData.Schedule(arrayLength, safeBatchCount, dependsOn);
            return true;
        }

        /// <summary>
        /// Resolves a safe IJobParallelFor batch size from the caller default and any cold-boot scheduling profile.
        /// </summary>
        public static int ResolveProfiledInnerloopBatchCount(uint jobHash, int elementCount, int innerloopBatchCount)
        {
            int minBatch = innerloopBatchCount > 0 ? innerloopBatchCount : 1;
            int maxBatch = innerloopBatchCount > 0 ? ResolveDefaultMaxBatch(innerloopBatchCount) : 4;
            if (JobSchedulingProfileCatalog.TryResolveBatchBounds(jobHash, out int profileMinBatch, out int profileMaxBatch))
            {
                minBatch = profileMinBatch;
                maxBatch = profileMaxBatch;
            }

            return ResolveInnerloopBatchCount(elementCount, minBatch, maxBatch);
        }

        private static int ResolveDefaultMaxBatch(int innerloopBatchCount)
        {
            return innerloopBatchCount > int.MaxValue / 4
                ? int.MaxValue
                : innerloopBatchCount << 2;
        }

        private static int ResolveInnerloopBatchCount(int elementCount, int minBatch, int maxBatch)
        {
            int safeMin = math.max(1, minBatch);
            int safeMax = math.max(safeMin, maxBatch);
            if (elementCount <= safeMin)
                return safeMin;

            return math.min(safeMax, math.max(safeMin, elementCount));
        }

        /// <summary>
        /// Reports a completed admitted job without exposing hash calculation to callers.
        /// </summary>
        public static void ReportAdmittedJobCompleted<TJob>(JobAdmissionLane lane, float measuredCompleteMs)
            where TJob : struct
        {
            IJobAdmissionService service = JobAdmissionSchedulerBridge.Service;
            if (service == null)
                return;

            service.ReportJobCompleted(lane, JobAdmissionHash<TJob>.Value, measuredCompleteMs);
        }
    }
}
