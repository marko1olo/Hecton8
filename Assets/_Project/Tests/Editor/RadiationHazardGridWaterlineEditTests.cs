using System;
using System.IO;
using NUnit.Framework;

namespace Hecton8.Tests.Editor
{
    public sealed class RadiationHazardGridWaterlineEditTests
    {
        [Test]
        public void RadiationTelemetryPlayerDepthUsesProductionSeaLevelAndInvalidAupStaysZero()
        {
            string source = ReadProjectFile("Assets", "_Project", "Scripts", "Gameplay", "RadiationHazardGrid.cs");
            string recordTelemetry = ExtractMethodBody(source, "private void RecordTelemetry(in AbsoluteUniversePosition playerAup, float intensity01, float accumulatedRads, uint flags)");
            string resolveDepth = ExtractMethodBody(source, "private float ResolvePlayerDepthMeters(double3 playerAbsolute)");

            StringAssert.Contains("private const float DefaultSeaLevelY = WorldWaterLevelCalibrationMath.DefaultWaterLevelY;", source);
            StringAssert.Contains("bool hasPlayerAbsolute = AbsoluteUniversePosition.IsFinite(in playerAup);", recordTelemetry);
            StringAssert.Contains("PlayerDepthMeters = hasPlayerAbsolute ? ResolvePlayerDepthMeters(playerAbsolute) : 0f", recordTelemetry);
            StringAssert.Contains("double depthMeters = ResolveTelemetrySeaLevelY() - playerAbsolute.y;", resolveDepth);
            StringAssert.DoesNotContain("PlayerDepthMeters = (float)math.max(0d, -playerAbsolute.y)", source);
        }

        [Test]
        public void RadiationPlayerAupResolverRejectsStaleMovementWhenRuntimeContextExists()
        {
            string source = ReadProjectFile("Assets", "_Project", "Scripts", "Gameplay", "RadiationHazardGrid.cs");
            string schedule = ExtractMethodBody(source, "private JobHandle ScheduleRadiationSimulation(");
            string postSimulation = ExtractMethodBody(source, "private void PostSimulationRadiation(");
            string exposureKernel = ExtractMethodBody(source, "private JobHandle ScheduleRadiationExposureKernel(");
            string resolveAup = ExtractMethodBody(source, "private static bool TryResolvePlayerAup(IPlayerRuntimeContext playerContext, out AbsoluteUniversePosition playerAup)");
            string mutableContext = ExtractMethodBody(source, "private PlayerRuntimeContext ResolveMutablePlayerRuntimeContext()");
            string gizmos = ExtractMethodBody(source, "private void OnDrawGizmos()");
            int radiationJobIndex = source.IndexOf("private unsafe struct CalculateRadiationExposureJob", StringComparison.Ordinal);
            Assert.That(radiationJobIndex, Is.GreaterThanOrEqualTo(0));
            string jobExecute = ExtractMethodBody(source.Substring(radiationJobIndex), "public void Execute()");

            StringAssert.Contains("PlayerRuntimeContext playerContext = ResolveMutablePlayerRuntimeContext();", schedule);
            StringAssert.Contains("IPlayerRuntimeContext playerReadContext = ResolveActivePlayerRuntimeContext();", schedule);
            StringAssert.Contains("bool hasPlayerAup = TryResolvePlayerAup(playerReadContext, out AbsoluteUniversePosition playerAup);", schedule);
            StringAssert.Contains("playerReadContext,", schedule);
            StringAssert.Contains("_lastSimulationPlayerAup = hasPlayerAup ? playerAup : AbsoluteUniversePosition.Invalid();", schedule);
            StringAssert.Contains("if (hasPlayerAup)", schedule);
            StringAssert.Contains("ScheduleEmergencyMockSourceIfNeeded(in playerAup, dependency)", schedule);
            Assert.That(
                schedule.IndexOf("if (hasPlayerAup)", StringComparison.Ordinal),
                Is.LessThan(schedule.IndexOf("ScheduleEmergencyMockSourceIfNeeded(in playerAup, dependency)", StringComparison.Ordinal)));

            StringAssert.Contains("if (!hasPlayerAup)", postSimulation);
            StringAssert.Contains("hasPlayerAup = TryResolvePlayerAup(ResolveActivePlayerRuntimeContext(), out playerAup);", postSimulation);
            StringAssert.Contains("if (hasPlayerAup)", postSimulation);
            StringAssert.Contains("PublishDoseSignal(in playerAup, doseAdd, _lastGridIntensity01, RadiationDoseGridKind);", postSimulation);
            StringAssert.Contains("hasPlayerAup ? 0u : RadiationTelemetryFlagSkippedEvaluation", postSimulation);

            StringAssert.Contains("bool hasPlayerAup = AbsoluteUniversePosition.IsFinite(in playerAup);", exposureKernel);
            StringAssert.Contains("hasPlayerAup = math.all(math.isfinite(playerRuntime)) && math.all(math.isfinite(playerAbsolute));", exposureKernel);
            StringAssert.Contains("if (hasPlayerAup && sdfReadLeaseModel != null)", exposureKernel);
            StringAssert.Contains("HasPlayerAup = hasPlayerAup ? 1u : 0u", exposureKernel);

            StringAssert.Contains("playerAup = AbsoluteUniversePosition.Invalid();", resolveAup);
            StringAssert.Contains("playerContext.TryGetPlayerPoseSnapshot(out PlayerRuntimePoseSnapshot snapshot)", resolveAup);
            StringAssert.Contains("(snapshot.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) != 0u", resolveAup);
            StringAssert.Contains("AbsoluteUniversePosition snapshotAup = snapshot.Aup;", resolveAup);
            StringAssert.Contains("playerAup = snapshotAup;", resolveAup);
            StringAssert.Contains("playerContext.TryGetMovementRuntimeState(out PlayerMovementRuntimeState movementState)", resolveAup);
            StringAssert.Contains("(movementState.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) != 0u", resolveAup);
            StringAssert.Contains("AbsoluteUniversePosition predictedAup = movementState.PredictedAup;", resolveAup);
            StringAssert.Contains("if (predictedAup.IsFinite())", resolveAup);
            StringAssert.Contains("playerAup = predictedAup;", resolveAup);
            StringAssert.Contains("return true;", resolveAup);
            StringAssert.Contains("TryResolveAupFromRuntimeOrigin(Vector3.zero, out playerAup)", resolveAup);
            StringAssert.Contains("PlayerRuntimeContextService.TryGetActiveRuntimeContext", mutableContext);
            StringAssert.DoesNotContain("PlayerRuntimeContextService.TryGetActiveRuntimeContext", resolveAup);
            StringAssert.DoesNotContain("playerContext.PlayerMovement", resolveAup);
            StringAssert.DoesNotContain("playerContext.MovementState", resolveAup);
            StringAssert.DoesNotContain("CurrentAup", resolveAup);
            Assert.That(
                resolveAup.IndexOf("playerContext.TryGetPlayerPoseSnapshot(out PlayerRuntimePoseSnapshot snapshot)", StringComparison.Ordinal),
                Is.LessThan(resolveAup.IndexOf("playerContext.TryGetMovementRuntimeState(out PlayerMovementRuntimeState movementState)", StringComparison.Ordinal)));
            Assert.That(
                resolveAup.IndexOf("(movementState.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) != 0u", StringComparison.Ordinal),
                Is.LessThan(resolveAup.IndexOf("AbsoluteUniversePosition predictedAup = movementState.PredictedAup;", StringComparison.Ordinal)));
            Assert.That(
                resolveAup.IndexOf("if (predictedAup.IsFinite())", StringComparison.Ordinal),
                Is.LessThan(resolveAup.IndexOf("playerAup = predictedAup;", StringComparison.Ordinal)));
            Assert.That(
                resolveAup.IndexOf("return true;", StringComparison.Ordinal),
                Is.LessThan(resolveAup.IndexOf("TryResolveAupFromRuntimeOrigin(Vector3.zero, out playerAup)", StringComparison.Ordinal)));

            StringAssert.Contains("public uint HasPlayerAup;", source);
            StringAssert.Contains("HasPlayerAup != 0u", jobExecute);
            StringAssert.Contains("if (hasPlayerAup)", jobExecute);
            StringAssert.Contains("if (!TryResolvePlayerAup(ResolveActivePlayerRuntimeContext(), out AbsoluteUniversePosition playerAup))", gizmos);
            StringAssert.DoesNotContain("private static AbsoluteUniversePosition ResolvePlayerAup", source);
        }

        private static string ReadProjectFile(params string[] parts)
        {
            string path = Path.Combine(Directory.GetCurrentDirectory(), Path.Combine(parts));
            return File.ReadAllText(path);
        }

        private static string ExtractMethodBody(string source, string signature)
        {
            int start = source.IndexOf(signature, StringComparison.Ordinal);
            Assert.That(start, Is.GreaterThanOrEqualTo(0), signature);
            int brace = source.IndexOf('{', start);
            Assert.That(brace, Is.GreaterThanOrEqualTo(0), signature);

            int depth = 0;
            for (int i = brace; i < source.Length; i++)
            {
                if (source[i] == '{')
                    depth++;
                else if (source[i] == '}')
                {
                    depth--;
                    if (depth == 0)
                        return source.Substring(brace, i - brace + 1);
                }
            }

            Assert.Fail("Could not extract method body for " + signature);
            return string.Empty;
        }
    }
}
