using System.Runtime.InteropServices;
using Hecton8.Vehicles.Automation;
using NUnit.Framework;

namespace Hecton8.Tests.Editor
{
    public sealed class SubmarineAutopilotLayoutTests
    {
        [Test]
        public void AutopilotStateDTO_MatchesCanonicalUnsafeLayout()
        {
            Assert.That(Marshal.SizeOf<AutopilotStateDTO>(), Is.EqualTo(64));
            Assert.That(Marshal.OffsetOf<AutopilotStateDTO>(nameof(AutopilotStateDTO.TargetAUP)).ToInt32(), Is.EqualTo(0));
            Assert.That(Marshal.OffsetOf<AutopilotStateDTO>(nameof(AutopilotStateDTO.DesiredVelocity)).ToInt32(), Is.EqualTo(24));
            Assert.That(Marshal.OffsetOf<AutopilotStateDTO>(nameof(AutopilotStateDTO.TargetSpeed)).ToInt32(), Is.EqualTo(36));
            Assert.That(Marshal.OffsetOf<AutopilotStateDTO>(nameof(AutopilotStateDTO.SubmarineHashID)).ToInt32(), Is.EqualTo(40));
            Assert.That(Marshal.OffsetOf<AutopilotStateDTO>(nameof(AutopilotStateDTO.NavFlags)).ToInt32(), Is.EqualTo(44));
        }

        [Test]
        public void AutopilotAvoidanceDTO_MatchesCanonicalUnsafeLayout()
        {
            Assert.That(Marshal.SizeOf<AutopilotAvoidanceDTO>(), Is.EqualTo(64));
            Assert.That(Marshal.OffsetOf<AutopilotAvoidanceDTO>(nameof(AutopilotAvoidanceDTO.Repulsion)).ToInt32(), Is.EqualTo(0));
            Assert.That(Marshal.OffsetOf<AutopilotAvoidanceDTO>(nameof(AutopilotAvoidanceDTO.Forward)).ToInt32(), Is.EqualTo(12));
            Assert.That(Marshal.OffsetOf<AutopilotAvoidanceDTO>(nameof(AutopilotAvoidanceDTO.FlowVelocity)).ToInt32(), Is.EqualTo(24));
            Assert.That(Marshal.OffsetOf<AutopilotAvoidanceDTO>(nameof(AutopilotAvoidanceDTO.AverageSdfPressure01)).ToInt32(), Is.EqualTo(36));
            Assert.That(Marshal.OffsetOf<AutopilotAvoidanceDTO>(nameof(AutopilotAvoidanceDTO.NearestHitDistance)).ToInt32(), Is.EqualTo(40));
            Assert.That(Marshal.OffsetOf<AutopilotAvoidanceDTO>(nameof(AutopilotAvoidanceDTO.ActiveFeelerCount)).ToInt32(), Is.EqualTo(44));
            Assert.That(Marshal.OffsetOf<AutopilotAvoidanceDTO>(nameof(AutopilotAvoidanceDTO.HitFeelerCount)).ToInt32(), Is.EqualTo(48));
            Assert.That(Marshal.OffsetOf<AutopilotAvoidanceDTO>(nameof(AutopilotAvoidanceDTO.Flags)).ToInt32(), Is.EqualTo(52));
        }

        [Test]
        public void AutopilotFeelerResultDTO_MatchesCanonicalUnsafeLayout()
        {
            Assert.That(Marshal.SizeOf<AutopilotFeelerResultDTO>(), Is.EqualTo(64));
            Assert.That(Marshal.OffsetOf<AutopilotFeelerResultDTO>(nameof(AutopilotFeelerResultDTO.StartRuntime)).ToInt32(), Is.EqualTo(0));
            Assert.That(Marshal.OffsetOf<AutopilotFeelerResultDTO>(nameof(AutopilotFeelerResultDTO.EndRuntime)).ToInt32(), Is.EqualTo(12));
            Assert.That(Marshal.OffsetOf<AutopilotFeelerResultDTO>(nameof(AutopilotFeelerResultDTO.HitRuntime)).ToInt32(), Is.EqualTo(24));
            Assert.That(Marshal.OffsetOf<AutopilotFeelerResultDTO>(nameof(AutopilotFeelerResultDTO.Repulsion)).ToInt32(), Is.EqualTo(36));
            Assert.That(Marshal.OffsetOf<AutopilotFeelerResultDTO>(nameof(AutopilotFeelerResultDTO.HitDistance)).ToInt32(), Is.EqualTo(48));
            Assert.That(Marshal.OffsetOf<AutopilotFeelerResultDTO>(nameof(AutopilotFeelerResultDTO.SdfDensity)).ToInt32(), Is.EqualTo(52));
            Assert.That(Marshal.OffsetOf<AutopilotFeelerResultDTO>(nameof(AutopilotFeelerResultDTO.FeelerIndex)).ToInt32(), Is.EqualTo(56));
            Assert.That(Marshal.OffsetOf<AutopilotFeelerResultDTO>(nameof(AutopilotFeelerResultDTO.Flags)).ToInt32(), Is.EqualTo(60));
        }

        [Test]
        public void AutopilotTelemetryEntry_MatchesCanonicalUnsafeLayout()
        {
            Assert.That(Marshal.SizeOf<AutopilotTelemetryEntry>(), Is.EqualTo(64));
            Assert.That(Marshal.OffsetOf<AutopilotTelemetryEntry>(nameof(AutopilotTelemetryEntry.FirstAUP)).ToInt32(), Is.EqualTo(0));
            Assert.That(Marshal.OffsetOf<AutopilotTelemetryEntry>(nameof(AutopilotTelemetryEntry.AverageRepulsion)).ToInt32(), Is.EqualTo(24));
            Assert.That(Marshal.OffsetOf<AutopilotTelemetryEntry>(nameof(AutopilotTelemetryEntry.AverageRepulsionMagnitude)).ToInt32(), Is.EqualTo(36));
            Assert.That(Marshal.OffsetOf<AutopilotTelemetryEntry>(nameof(AutopilotTelemetryEntry.Frame)).ToInt32(), Is.EqualTo(40));
            Assert.That(Marshal.OffsetOf<AutopilotTelemetryEntry>(nameof(AutopilotTelemetryEntry.ActiveAutopilots)).ToInt32(), Is.EqualTo(44));
            Assert.That(Marshal.OffsetOf<AutopilotTelemetryEntry>(nameof(AutopilotTelemetryEntry.FeelerCount)).ToInt32(), Is.EqualTo(48));
            Assert.That(Marshal.OffsetOf<AutopilotTelemetryEntry>(nameof(AutopilotTelemetryEntry.Flags)).ToInt32(), Is.EqualTo(52));
            Assert.That(Marshal.OffsetOf<AutopilotTelemetryEntry>(nameof(AutopilotTelemetryEntry.EstimatedBurstMicroseconds)).ToInt32(), Is.EqualTo(56));
            Assert.That(Marshal.OffsetOf<AutopilotTelemetryEntry>(nameof(AutopilotTelemetryEntry.StateHash)).ToInt32(), Is.EqualTo(60));
        }

        [Test]
        public void AutopilotWaypointDTO_MatchesCanonicalUnsafeLayout()
        {
            Assert.That(Marshal.SizeOf<AutopilotWaypointDTO>(), Is.EqualTo(32));
            Assert.That(Marshal.OffsetOf<AutopilotWaypointDTO>(nameof(AutopilotWaypointDTO.TargetAUP)).ToInt32(), Is.EqualTo(0));
            Assert.That(Marshal.OffsetOf<AutopilotWaypointDTO>(nameof(AutopilotWaypointDTO.AcceptanceRadius)).ToInt32(), Is.EqualTo(24));
            Assert.That(Marshal.OffsetOf<AutopilotWaypointDTO>(nameof(AutopilotWaypointDTO.Flags)).ToInt32(), Is.EqualTo(28));
        }

        [Test]
        public void AutopilotRouteRangeDTO_MatchesCanonicalUnsafeLayout()
        {
            Assert.That(Marshal.SizeOf<AutopilotRouteRangeDTO>(), Is.EqualTo(32));
            Assert.That(Marshal.OffsetOf<AutopilotRouteRangeDTO>(nameof(AutopilotRouteRangeDTO.StartIndex)).ToInt32(), Is.EqualTo(0));
            Assert.That(Marshal.OffsetOf<AutopilotRouteRangeDTO>(nameof(AutopilotRouteRangeDTO.Count)).ToInt32(), Is.EqualTo(4));
            Assert.That(Marshal.OffsetOf<AutopilotRouteRangeDTO>(nameof(AutopilotRouteRangeDTO.CurrentOffset)).ToInt32(), Is.EqualTo(8));
            Assert.That(Marshal.OffsetOf<AutopilotRouteRangeDTO>(nameof(AutopilotRouteRangeDTO.AcceptanceRadius)).ToInt32(), Is.EqualTo(12));
            Assert.That(Marshal.OffsetOf<AutopilotRouteRangeDTO>(nameof(AutopilotRouteRangeDTO.Flags)).ToInt32(), Is.EqualTo(16));
            Assert.That(Marshal.OffsetOf<AutopilotRouteRangeDTO>(nameof(AutopilotRouteRangeDTO.RouteHash)).ToInt32(), Is.EqualTo(20));
        }

        [Test]
        public void AutopilotHandlingProfileDTO_MatchesCanonicalUnsafeLayout()
        {
            Assert.That(Marshal.SizeOf<AutopilotHandlingProfileDTO>(), Is.EqualTo(32));
            Assert.That(Marshal.OffsetOf<AutopilotHandlingProfileDTO>(nameof(AutopilotHandlingProfileDTO.NameHash)).ToInt32(), Is.EqualTo(0));
            Assert.That(Marshal.OffsetOf<AutopilotHandlingProfileDTO>(nameof(AutopilotHandlingProfileDTO.MaxTurnRateRadians)).ToInt32(), Is.EqualTo(4));
            Assert.That(Marshal.OffsetOf<AutopilotHandlingProfileDTO>(nameof(AutopilotHandlingProfileDTO.AccelerationLimit)).ToInt32(), Is.EqualTo(8));
            Assert.That(Marshal.OffsetOf<AutopilotHandlingProfileDTO>(nameof(AutopilotHandlingProfileDTO.SpeedScale)).ToInt32(), Is.EqualTo(12));
            Assert.That(Marshal.OffsetOf<AutopilotHandlingProfileDTO>(nameof(AutopilotHandlingProfileDTO.RepulsionWeight)).ToInt32(), Is.EqualTo(16));
            Assert.That(Marshal.OffsetOf<AutopilotHandlingProfileDTO>(nameof(AutopilotHandlingProfileDTO.Flags)).ToInt32(), Is.EqualTo(20));
        }

        [Test]
        public void AutopilotTuningDTO_MatchesCanonicalUnsafeLayout()
        {
            Assert.That(Marshal.SizeOf<AutopilotTuningDTO>(), Is.EqualTo(128));
            Assert.That(Marshal.OffsetOf<AutopilotTuningDTO>(nameof(AutopilotTuningDTO.FeelerLength)).ToInt32(), Is.EqualTo(0));
            Assert.That(Marshal.OffsetOf<AutopilotTuningDTO>(nameof(AutopilotTuningDTO.SdfThresholdMeters)).ToInt32(), Is.EqualTo(4));
            Assert.That(Marshal.OffsetOf<AutopilotTuningDTO>(nameof(AutopilotTuningDTO.RepulsionWeight)).ToInt32(), Is.EqualTo(8));
            Assert.That(Marshal.OffsetOf<AutopilotTuningDTO>(nameof(AutopilotTuningDTO.MaxTurnRateRadians)).ToInt32(), Is.EqualTo(12));
            Assert.That(Marshal.OffsetOf<AutopilotTuningDTO>(nameof(AutopilotTuningDTO.WaypointAcceptanceRadius)).ToInt32(), Is.EqualTo(16));
            Assert.That(Marshal.OffsetOf<AutopilotTuningDTO>(nameof(AutopilotTuningDTO.FlowCompensationWeight)).ToInt32(), Is.EqualTo(20));
            Assert.That(Marshal.OffsetOf<AutopilotTuningDTO>(nameof(AutopilotTuningDTO.TargetSpeedFallback)).ToInt32(), Is.EqualTo(24));
            Assert.That(Marshal.OffsetOf<AutopilotTuningDTO>(nameof(AutopilotTuningDTO.GlobalQualityWeight)).ToInt32(), Is.EqualTo(28));
            Assert.That(Marshal.OffsetOf<AutopilotTuningDTO>(nameof(AutopilotTuningDTO.SdfOrigin)).ToInt32(), Is.EqualTo(32));
            Assert.That(Marshal.OffsetOf<AutopilotTuningDTO>(nameof(AutopilotTuningDTO.SdfCellSize)).ToInt32(), Is.EqualTo(44));
            Assert.That(Marshal.OffsetOf<AutopilotTuningDTO>(nameof(AutopilotTuningDTO.SdfDimensions)).ToInt32(), Is.EqualTo(56));
            Assert.That(Marshal.OffsetOf<AutopilotTuningDTO>(nameof(AutopilotTuningDTO.SdfRangeMeters)).ToInt32(), Is.EqualTo(68));
            Assert.That(Marshal.OffsetOf<AutopilotTuningDTO>(nameof(AutopilotTuningDTO.Flags)).ToInt32(), Is.EqualTo(72));
            Assert.That(Marshal.OffsetOf<AutopilotTuningDTO>(nameof(AutopilotTuningDTO.ActiveVehicleCount)).ToInt32(), Is.EqualTo(76));
            Assert.That(Marshal.OffsetOf<AutopilotTuningDTO>(nameof(AutopilotTuningDTO.FlowOrigin)).ToInt32(), Is.EqualTo(80));
            Assert.That(Marshal.OffsetOf<AutopilotTuningDTO>(nameof(AutopilotTuningDTO.FlowCellSize)).ToInt32(), Is.EqualTo(92));
            Assert.That(Marshal.OffsetOf<AutopilotTuningDTO>(nameof(AutopilotTuningDTO.FlowDimensions)).ToInt32(), Is.EqualTo(104));
            Assert.That(Marshal.OffsetOf<AutopilotTuningDTO>(nameof(AutopilotTuningDTO.SourceHash)).ToInt32(), Is.EqualTo(116));
            Assert.That(Marshal.OffsetOf<AutopilotTuningDTO>(nameof(AutopilotTuningDTO.ResolvedQualityWeight)).ToInt32(), Is.EqualTo(120));
        }
    }
}
