#if UNITY_EDITOR
using System.Runtime.InteropServices;
using Hecton8.Core;
using Hecton8.Core.Scheduling;
using Unity.Collections.LowLevel.Unsafe;
using UnityEditor;
using UnityEngine;

namespace Hecton8.Editor
{
    [InitializeOnLoad]
    public static class DispatcherFenceLayoutValidator
    {
        private const int DispatcherStateSizeBytes = 32;
        private const int DispatcherTimingSizeBytes = 32;
        private const int PresentationSuppressionSizeBytes = 32;
        private const int FenceTelemetrySizeBytes = 64;
        private const int JobDependencySizeBytes = 32;
        private const int PipelineTelemetrySizeBytes = 64;
        private const int MockTimeDilationSignalSizeBytes = 16;
        private const int JobSchedulingProfileSizeBytes = 16;

        static DispatcherFenceLayoutValidator()
        {
            Validate();
        }

        [MenuItem("Hecton/Diagnostics/Validate Dispatcher Fence Layouts")]
        public static void Validate()
        {
            bool valid =
                ValidateDispatcherState() &&
                ValidateDispatcherTiming() &&
                ValidatePresentationSuppression() &&
                ValidateJobDependency() &&
                ValidateFenceTelemetry() &&
                ValidatePipelineTelemetry() &&
                ValidateMockTimeDilationSignal() &&
                ValidateJobSchedulingProfile();

            if (!valid)
            {
                Debug.LogError(
                    "[DispatcherFenceLayoutValidator] ARM64 fence DTO layout invalid. " +
                    "State/timing/suppression/dependency/fence/pipeline/mock/profile DTOs must keep explicit padded layouts.");
            }
        }

        private static bool ValidateDispatcherState()
        {
            return ValidateSizeAndAlign<DispatcherStateDTO>(DispatcherStateSizeBytes) &&
                   OffsetOf<DispatcherStateDTO>(nameof(DispatcherStateDTO.CurrentPhaseId)) == 0 &&
                   OffsetOf<DispatcherStateDTO>(nameof(DispatcherStateDTO.CurrentFrame)) == 4 &&
                   OffsetOf<DispatcherStateDTO>(nameof(DispatcherStateDTO.ActiveBucket)) == 8 &&
                   OffsetOf<DispatcherStateDTO>(nameof(DispatcherStateDTO.ActiveBucketMask)) == 12 &&
                   OffsetOf<DispatcherStateDTO>(nameof(DispatcherStateDTO.SortedSystemCount)) == 16 &&
                   OffsetOf<DispatcherStateDTO>(nameof(DispatcherStateDTO.DisabledSystemCount)) == 20 &&
                   OffsetOf<DispatcherStateDTO>(nameof(DispatcherStateDTO.PendingSimulationJobCount)) == 24 &&
                   OffsetOf<DispatcherStateDTO>(nameof(DispatcherStateDTO.Flags)) == 28;
        }

        private static bool ValidateDispatcherTiming()
        {
            return ValidateSizeAndAlign<DispatcherTimingDTO>(DispatcherTimingSizeBytes) &&
                   DispatcherTimingLayoutGuard.ValidateLayout() &&
                   OffsetOf<DispatcherTimingDTO>(nameof(DispatcherTimingDTO.FrameDelta)) == 0 &&
                   OffsetOf<DispatcherTimingDTO>(nameof(DispatcherTimingDTO.FixedDelta)) == 4 &&
                   OffsetOf<DispatcherTimingDTO>(nameof(DispatcherTimingDTO.TimeScale)) == 8 &&
                   OffsetOf<DispatcherTimingDTO>(nameof(DispatcherTimingDTO.ActiveBucketMask)) == 12;
        }

        private static bool ValidatePresentationSuppression()
        {
            return ValidateSizeAndAlign<DispatcherPresentationSuppressionDTO>(PresentationSuppressionSizeBytes) &&
                   OffsetOf<DispatcherPresentationSuppressionDTO>(nameof(DispatcherPresentationSuppressionDTO.FrameId)) == 0 &&
                   OffsetOf<DispatcherPresentationSuppressionDTO>(nameof(DispatcherPresentationSuppressionDTO.Flags)) == 4 &&
                   OffsetOf<DispatcherPresentationSuppressionDTO>(nameof(DispatcherPresentationSuppressionDTO.GlobalQualityWeight)) == 8 &&
                   OffsetOf<DispatcherPresentationSuppressionDTO>(nameof(DispatcherPresentationSuppressionDTO.Suppression01)) == 12 &&
                   OffsetOf<DispatcherPresentationSuppressionDTO>(nameof(DispatcherPresentationSuppressionDTO.RollbackFlags)) == 16 &&
                   OffsetOf<DispatcherPresentationSuppressionDTO>(nameof(DispatcherPresentationSuppressionDTO._pad0)) == 20 &&
                   OffsetOf<DispatcherPresentationSuppressionDTO>(nameof(DispatcherPresentationSuppressionDTO._pad11)) == 31;
        }

        private static bool ValidateJobDependency()
        {
            return ValidateSizeAndAlign<JobDependencyDTO>(JobDependencySizeBytes) &&
                   OffsetOf<JobDependencyDTO>(nameof(JobDependencyDTO.JobHandleBits)) == 0 &&
                   OffsetOf<JobDependencyDTO>(nameof(JobDependencyDTO.SystemIdHash)) == 8 &&
                   OffsetOf<JobDependencyDTO>(nameof(JobDependencyDTO.FrameId)) == 12 &&
                   OffsetOf<JobDependencyDTO>(nameof(JobDependencyDTO.DependencyHash0)) == 16 &&
                   OffsetOf<JobDependencyDTO>(nameof(JobDependencyDTO.PhaseId)) == 20 &&
                   OffsetOf<JobDependencyDTO>(nameof(JobDependencyDTO.DomainId)) == 21 &&
                   OffsetOf<JobDependencyDTO>(nameof(JobDependencyDTO.DependencyCount)) == 22 &&
                   OffsetOf<JobDependencyDTO>(nameof(JobDependencyDTO.BucketId)) == 23 &&
                   OffsetOf<JobDependencyDTO>(nameof(JobDependencyDTO.Flags)) == 24 &&
                   OffsetOf<JobDependencyDTO>(nameof(JobDependencyDTO._pad0)) == 28;
        }

        private static bool ValidateFenceTelemetry()
        {
            return ValidateSizeAndAlign<DispatcherFenceTelemetryEntry>(FenceTelemetrySizeBytes) &&
                   DispatcherFenceTelemetryLayoutGuard.ValidateLayout();
        }

        private static bool ValidatePipelineTelemetry()
        {
            return ValidateSizeAndAlign<DispatcherPipelineTelemetryEntry>(PipelineTelemetrySizeBytes) &&
                   OffsetOf<DispatcherPipelineTelemetryEntry>(nameof(DispatcherPipelineTelemetryEntry.Frame)) == 0 &&
                   OffsetOf<DispatcherPipelineTelemetryEntry>(nameof(DispatcherPipelineTelemetryEntry.PreSimulationTimeMs)) == 4 &&
                   OffsetOf<DispatcherPipelineTelemetryEntry>(nameof(DispatcherPipelineTelemetryEntry.SimWaitTimeMs)) == 8 &&
                   OffsetOf<DispatcherPipelineTelemetryEntry>(nameof(DispatcherPipelineTelemetryEntry.PostSimulationTimeMs)) == 12 &&
                   OffsetOf<DispatcherPipelineTelemetryEntry>(nameof(DispatcherPipelineTelemetryEntry.VisualSyncTimeMs)) == 16 &&
                   OffsetOf<DispatcherPipelineTelemetryEntry>(nameof(DispatcherPipelineTelemetryEntry.ActiveBucket)) == 20 &&
                   OffsetOf<DispatcherPipelineTelemetryEntry>(nameof(DispatcherPipelineTelemetryEntry.SystemCount)) == 24 &&
                   OffsetOf<DispatcherPipelineTelemetryEntry>(nameof(DispatcherPipelineTelemetryEntry.Flags)) == 28;
        }

        private static bool ValidateMockTimeDilationSignal()
        {
            return ValidateSizeAndAlign<MockTimeDilationSignal>(MockTimeDilationSignalSizeBytes) &&
                   OffsetOf<MockTimeDilationSignal>(nameof(MockTimeDilationSignal.TimeScale)) == 0 &&
                   OffsetOf<MockTimeDilationSignal>(nameof(MockTimeDilationSignal.FrameDelta)) == 4 &&
                   OffsetOf<MockTimeDilationSignal>(nameof(MockTimeDilationSignal.Frame)) == 8 &&
                   OffsetOf<MockTimeDilationSignal>(nameof(MockTimeDilationSignal.SourceHash)) == 12;
        }

        private static bool ValidateJobSchedulingProfile()
        {
            return ValidateSizeAndAlign<JobSchedulingProfileDTO>(JobSchedulingProfileSizeBytes) &&
                   OffsetOf<JobSchedulingProfileDTO>(nameof(JobSchedulingProfileDTO.JobHash)) == 0 &&
                   OffsetOf<JobSchedulingProfileDTO>(nameof(JobSchedulingProfileDTO.MinBatch)) == 4 &&
                   OffsetOf<JobSchedulingProfileDTO>(nameof(JobSchedulingProfileDTO.MaxBatch)) == 6 &&
                   OffsetOf<JobSchedulingProfileDTO>(nameof(JobSchedulingProfileDTO.Flags)) == 8 &&
                   OffsetOf<JobSchedulingProfileDTO>(nameof(JobSchedulingProfileDTO.Padding0)) == 12;
        }

        private static bool ValidateSizeAndAlign<T>(int expectedSizeBytes)
            where T : struct
        {
            return UnsafeUtility.SizeOf<T>() == expectedSizeBytes &&
                   UnsafeUtility.AlignOf<T>() >= 4 &&
                   (expectedSizeBytes & 7) == 0;
        }

        private static int OffsetOf<T>(string fieldName)
            where T : struct
        {
            return Marshal.OffsetOf(typeof(T), fieldName).ToInt32();
        }
    }
}
#endif
