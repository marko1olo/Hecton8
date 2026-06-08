using System;
using System.IO;
using NUnit.Framework;

namespace Hecton8.Tests.Editor
{
    public sealed class WorldWaterLevelCalibrationEditTests
    {
        [Test]
        public void WorldWaterLevelCalibrationAuthoring_MovesCrestRootWithoutTerrainMutation()
        {
            string source = ReadProjectFile(
                "Assets",
                "_Project",
                "Scripts",
                "World",
                "WorldWaterLevelCalibrationAuthoring.cs");
            string apply = ExtractMethodBody(source, "public bool TryApplyWaterLevel()");
            string resolveRoot = ExtractMethodBody(source, "private Transform ResolveTargetRoot()");
            string mathSource = ExtractClassBody(source, "public static class WorldWaterLevelCalibrationMath");

            StringAssert.Contains("public const int DtoBytes = 32;", source);
            StringAssert.Contains("[StructLayout(LayoutKind.Explicit, Size = WorldWaterLevelCalibrationMath.DtoBytes)]", source);
            StringAssert.Contains("public const float MinimumCalibrationTravelMeters = 100f;", source);
            StringAssert.Contains("public const float DefaultCalibrationTravelMeters = 512f;", source);
            StringAssert.Contains("private global::Crest.OceanRenderer oceanRenderer;", source);
            StringAssert.Contains("oceanRenderer.Root", resolveRoot);
            StringAssert.Contains("targetRoot.position = new Vector3(rootPosition.x, resolvedWaterLevelY, rootPosition.z);", apply);
            StringAssert.Contains("AppliedToCrestRoot", apply);
            StringAssert.Contains("UsedFallback", apply);
            StringAssert.DoesNotContain("WorldMacroGeologyFields", source);
            StringAssert.DoesNotContain("evaluate_height", source);
            StringAssert.DoesNotContain("heightmap", source);
            StringAssert.DoesNotContain("bool ", ExtractStructBody(source, "public struct WorldWaterLevelCalibrationDTO"));
            StringAssert.Contains("math.clamp(", mathSource);
        }

        [Test]
        public void UnderwaterVisuals_ResolveWaterLevelReadsPlayerAndCrestBeforeAtmosphereTerrainFluidFallbacks()
        {
            string source = ReadProjectFile("Assets", "_Project", "Scripts", "HectonUnderwaterVisuals.cs");
            string cacheRuntime = ExtractMethodBody(source, "private void CacheRuntimeDependencies()");
            string coldResolve = ExtractMethodBody(source, "private void ResolveRuntimeServiceCachesOnColdCadence()");
            string serviceReplaced = ExtractMethodBody(source, "public void OnGlobalRegistryServiceReplaced(");
            string resolveWater = ExtractMethodBody(source, "private float ResolveWaterLevel()");

            StringAssert.Contains("using Hecton8.Core.Contracts;", source);
            StringAssert.Contains("private IHectonOceanKinematicsService _oceanKinematicsService;", source);
            StringAssert.Contains("private IHectonOceanKinematics _oceanKinematicsProvider;", source);
            StringAssert.Contains("CacheOceanKinematicsRuntimeCold();", cacheRuntime);
            StringAssert.Contains("CacheOceanKinematicsRuntimeCold();", coldResolve);
            StringAssert.Contains("case GlobalRegistryServiceSlot.OceanKinematics:", serviceReplaced);
            StringAssert.Contains("_oceanKinematicsService = currentService as IHectonOceanKinematicsService;", serviceReplaced);
            StringAssert.Contains("ReadCachedOceanKinematicsProvider()", resolveWater);
            StringAssert.Contains("oceanKinematics.SeaLevel", resolveWater);
            AssertTextBefore(resolveWater, "_playerMovement.CurrentWaterSurfaceY", "ReadCachedOceanKinematicsProvider()");
            AssertTextBefore(resolveWater, "ReadCachedOceanKinematicsProvider()", "atmosphereManager.SeaLevelY");
            AssertTextBefore(resolveWater, "atmosphereManager.SeaLevelY", "terrainRuntime.WaterSurfaceLevel");
            AssertTextBefore(resolveWater, "terrainRuntime.WaterSurfaceLevel", "_physicsEngine.WaterLevel");
        }

        [Test]
        public void OceanKinematicsRuntimeService_HotSwapConflictMarkerIsResolvedAndAbortGuardRemains()
        {
            string source = ReadProjectFile("Assets", "_Project", "Scripts", "Core", "OceanKinematicsRuntimeService.cs");
            string serviceReplaced = ExtractMethodBody(source, "public void OnGlobalRegistryServiceReplaced(");

            StringAssert.DoesNotContain("<<<<<<<", source);
            StringAssert.DoesNotContain("=======", source);
            StringAssert.DoesNotContain(">>>>>>>", source);
            StringAssert.Contains("if (_runtimeOwnerAborted)", serviceReplaced);
            StringAssert.Contains("if (serviceSlot != GlobalRegistryServiceSlot.Dispatcher)", serviceReplaced);
            AssertTextBefore(serviceReplaced, "if (_runtimeOwnerAborted)", "if (serviceSlot != GlobalRegistryServiceSlot.Dispatcher)");
        }

        [Test]
        public void CrestProviderSeaLevelRemainsCrestRootSourceForMovableCalibration()
        {
            string source = ReadProjectFile(
                "Assets",
                "_Project",
                "Scripts",
                "Plugins",
                "Crest",
                "Crest4KinematicsAdapter.cs");
            string seaLevel = ExtractMethodBody(source, "private static float ResolveSeaLevel(global::Crest.OceanRenderer oceanRenderer)");
            string tuning = ExtractMethodBody(source, "public bool TryBuildBurstTuning(");

            StringAssert.Contains("AnalyticalGerstnerWaveConstants.ResolveSeaLevelY(oceanRenderer.Root.position.y)", seaLevel);
            StringAssert.Contains("tuning.OceanSurfaceY = ResolveSeaLevel(oceanRenderer);", tuning);
            StringAssert.DoesNotContain("WorldMacroGeologyFields", source);
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
            return ExtractBalancedBody(source, source.IndexOf('{', start), signature);
        }

        private static string ExtractClassBody(string source, string signature)
        {
            int start = source.IndexOf(signature, StringComparison.Ordinal);
            Assert.That(start, Is.GreaterThanOrEqualTo(0), signature);
            return ExtractBalancedBody(source, source.IndexOf('{', start), signature);
        }

        private static string ExtractStructBody(string source, string signature)
        {
            int start = source.IndexOf(signature, StringComparison.Ordinal);
            Assert.That(start, Is.GreaterThanOrEqualTo(0), signature);
            return ExtractBalancedBody(source, source.IndexOf('{', start), signature);
        }

        private static string ExtractBalancedBody(string source, int brace, string signature)
        {
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

            Assert.Fail("Could not extract body for " + signature);
            return string.Empty;
        }

        private static void AssertTextBefore(string source, string first, string second)
        {
            int firstIndex = source.IndexOf(first, StringComparison.Ordinal);
            int secondIndex = source.IndexOf(second, StringComparison.Ordinal);
            Assert.That(firstIndex, Is.GreaterThanOrEqualTo(0), first);
            Assert.That(secondIndex, Is.GreaterThanOrEqualTo(0), second);
            Assert.That(firstIndex, Is.LessThan(secondIndex), first + " before " + second);
        }
    }
}
