using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Hecton8.Core.Memory;
using Hecton8.Rendering.OceanSinglePass;
using NUnit.Framework;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Tests.Editor
{
    public sealed class OceanSinglePassGuillotineEditTests
    {
        [Test]
        public void VisualOverridesDto_IsExactArm64CBufferLayout()
        {
            Assert.AreEqual(32, UnsafeUtility.SizeOf<OceanVisualOverridesDTO>());
            Assert.AreEqual(0, OffsetOf<OceanVisualOverridesDTO>(nameof(OceanVisualOverridesDTO.FoamAndShadowParams)));
            Assert.AreEqual(16, OffsetOf<OceanVisualOverridesDTO>(nameof(OceanVisualOverridesDTO.ShorelineDepthParams)));
            Assert.AreEqual(0, typeof(OceanVisualOverridesDTO).GetProperties().Length);
        }

        [Test]
        public void RuntimeDtos_AreExplicitCacheLineSizedRows()
        {
            Assert.AreEqual(64, UnsafeUtility.SizeOf<OceanGuillotineTuningDTO>());
            Assert.AreEqual(64, UnsafeUtility.SizeOf<OceanAestheticProfileDTO>());
            Assert.AreEqual(64, UnsafeUtility.SizeOf<OceanRenderTelemetryEntry>());
            Assert.AreEqual(64, UnsafeUtility.SizeOf<OceanMockRenderStateDTO>());
            Assert.AreEqual(32, OffsetOf<OceanRenderTelemetryEntry>(nameof(OceanRenderTelemetryEntry.WakeScrollOffset)));
            Assert.AreEqual(48, OffsetOf<OceanRenderTelemetryEntry>(nameof(OceanRenderTelemetryEntry.StateHash)));
            Assert.AreEqual(32, UnsafeUtility.SizeOf<ShorelineFoamParamsDTO>());
            Assert.AreEqual(64, UnsafeUtility.SizeOf<ShorelineFoamRuntimeStateDTO>());
            Assert.AreEqual(64, UnsafeUtility.SizeOf<ShorelineFoamTelemetryEntry>());
            Assert.AreEqual(0, OffsetOf<ShorelineFoamParamsDTO>(nameof(ShorelineFoamParamsDTO.FoamIntensityAndFalloff)));
            Assert.AreEqual(16, OffsetOf<ShorelineFoamParamsDTO>(nameof(ShorelineFoamParamsDTO.QualityAndLimits)));
            Assert.AreEqual(0, typeof(ShorelineFoamParamsDTO).GetProperties().Length);
        }

        [Test]
        public void WakeResolution_ScalesContinuouslyAcrossQualityWeight()
        {
            int low = OceanSinglePassMath.ResolveWakeResolution(0f);
            int mid = OceanSinglePassMath.ResolveWakeResolution(0.5f);
            int high = OceanSinglePassMath.ResolveWakeResolution(1f);

            Assert.AreEqual(OceanSinglePassConstants.WakeMinResolution, low);
            Assert.Greater(mid, low);
            Assert.Less(mid, high);
            Assert.AreEqual(OceanSinglePassConstants.WakeMaxResolution, high);
            Assert.AreEqual(0, mid % OceanSinglePassConstants.WakeResolutionQuantum);
        }

        [Test]
        public void ShorelineFoam_QualityScalesActiveLimitContinuously()
        {
            int low = ShorelineFoamMath.ResolveActiveLimit(0f, ShorelineFoamConstants.MaxCapacity);
            int mid = ShorelineFoamMath.ResolveActiveLimit(0.5f, ShorelineFoamConstants.MaxCapacity);
            int high = ShorelineFoamMath.ResolveActiveLimit(1f, ShorelineFoamConstants.MaxCapacity);

            Assert.AreEqual(1, low);
            Assert.Greater(mid, low);
            Assert.Less(mid, high);
            Assert.AreEqual(ShorelineFoamConstants.MaxCapacity, high);
        }

        [Test]
        public void ShorelineFoam_MockRingWritesAndDecaysWithoutProperties()
        {
            using NativeArray<ShorelineFoamParamsDTO> foam = new NativeArray<ShorelineFoamParamsDTO>(
                4,
                Allocator.TempJob,
                NativeArrayOptions.ClearMemory);
            using NativeArray<ShorelineFoamRuntimeStateDTO> state = new NativeArray<ShorelineFoamRuntimeStateDTO>(
                1,
                Allocator.TempJob,
                NativeArrayOptions.ClearMemory);

            GenerateMockShorelineFoamDataJob generate = new GenerateMockShorelineFoamDataJob
            {
                FoamParams = foam,
                State = state,
                Profile = ShorelineFoamMath.CreateDefaultProfile(),
                Frame = 7u,
                GlobalQualityWeight = 0.5f,
                WaterSurfaceLocalY = -3f,
                CameraLocalY = 3f,
                DeltaSeconds = 1f / 60f
            };
            generate.Run();

            Assert.AreEqual(1u, state[0].TotalWritten);
            Assert.Greater(state[0].ActiveCount, 0u);
            Assert.Greater(foam[0].FoamIntensityAndFalloff.w, 0f);

            DecayShorelineFoamOpacityJob decay = new DecayShorelineFoamOpacityJob
            {
                FoamParams = foam,
                DecayRate = 0.5f,
                DeltaSeconds = 0.5f
            };
            decay.Run(foam.Length);

            Assert.Less(foam[0].FoamIntensityAndFalloff.w, 1f);
            Assert.IsTrue(ShorelineFoamMath.ValidateRuntimeLayouts());
        }

        [Test]
        public void WakeAupWrapping_IsStableAtHundredKilometerCoordinates()
        {
            double3 cameraA = new double3(100000.0, 0.0, -99999.0);
            double3 cameraB = cameraA + new double3(
                OceanSinglePassConstants.WakeTextureWorldSizeMeters * 17.0,
                0.0,
                -OceanSinglePassConstants.WakeTextureWorldSizeMeters * 11.0);

            float4 a = OceanSinglePassMath.ResolveWakeScrollOffset(cameraA, OceanSinglePassConstants.WakeTextureWorldSizeMeters);
            float4 b = OceanSinglePassMath.ResolveWakeScrollOffset(cameraB, OceanSinglePassConstants.WakeTextureWorldSizeMeters);

            Assert.AreEqual(a.x, b.x, 0.0001f);
            Assert.AreEqual(a.y, b.y, 0.0001f);
            Assert.IsTrue(math.all(math.isfinite(a)));
            Assert.IsTrue(math.all(math.isfinite(b)));
        }

        [Test]
        public void CsvProfileParser_ConsumesUtf8BytesWithoutManagedTokenObjects()
        {
            byte[] bytes = Encoding.ASCII.GetBytes(
                "biome,foamThreshold,foamIntensity,wakeStrength,wakeLifespan,shorelineFade,reflectionMix,seaLevel,quality\n" +
                "Toxic_Swamp,0.81,2.5,1.7,5.0,12.0,0.2,-1.5,0.35\n");
            using NativeArray<OceanAestheticProfileDTO> profiles = new NativeArray<OceanAestheticProfileDTO>(
                4,
                Allocator.Temp,
                NativeArrayOptions.UninitializedMemory);

            bool parsed = OceanAestheticProfileCsvParser.TryParseProfiles(bytes, profiles, out int count, out uint fileHash);

            Assert.IsTrue(parsed);
            Assert.AreEqual(1, count);
            Assert.AreNotEqual(0u, fileHash);
            Assert.AreEqual(OceanAestheticProfileCsvParser.HashLowerAscii(Encoding.ASCII.GetBytes("Toxic_Swamp")), profiles[0].BiomeHash);
            Assert.AreEqual(0.81f, profiles[0].FoamThreshold, 0.0001f);
            Assert.AreEqual(12.0f, profiles[0].ShorelineDepthFadeMeters, 0.0001f);
        }

        [Test]
        public void CrestCameraConstructors_AreCutAtKnownOceanPaths()
        {
            AssertProjectFileContains("Assets/Crest/Crest/Scripts/LodData/OceanDepthCache.cs", "HectonRealtimeDepthCacheDisabled = true");
            AssertProjectFileContains("Assets/Crest/Crest/Scripts/Reflection/OceanPlanarReflection.cs", "HectonPlanarReflectionDisabled = true");
            AssertProjectFileContains("Assets/_Project/Scripts/Plugins/Crest/HectonCrestOceanDepthCacheRuntimeBridge.cs", "HectonRuntimeDepthCacheCameraDisabled = true");
            AssertProjectFileDoesNotContain("Assets/Crest/Crest/Scripts/LodData/OceanDepthCache.cs", "AddComponent<Camera>");
            AssertProjectFileDoesNotContain("Assets/Crest/Crest/Scripts/Reflection/OceanPlanarReflection.cs", "AddComponent<Camera>");
            AssertProjectFileContains("Assets/_Project/Prefabs/Ocean_Crest.prefab", "_createSeaFloorDepthData: 0");
            AssertProjectFileContains("Assets/_Project/Prefabs/Ocean_Crest.prefab", "_createFoamSim: 0");
        }

        [Test]
        public void BufferIds_AreVisualPresentationRange_NotRollbackAuthority()
        {
            Assert.AreEqual(71895, (int)OceanSinglePassConstants.VisualOverridesBuffer);
            Assert.AreEqual(71896, (int)OceanSinglePassConstants.TuningBuffer);
            Assert.AreEqual(71897, (int)OceanSinglePassConstants.TelemetryRingBuffer);
            Assert.AreEqual(71898, (int)OceanSinglePassConstants.TelemetryCursorBuffer);
            Assert.AreEqual(71901, (int)OceanSinglePassConstants.MockRenderStateBuffer);
            Assert.AreNotEqual((int)BufferID.ShinobuNetcodeFuzzerSnapshotRing, (int)OceanSinglePassConstants.VisualOverridesBuffer);
        }

        private static void AssertProjectFileContains(string relativePath, string token)
        {
            string path = ToProjectPath(relativePath);
            Assert.IsTrue(FileContains(path, token), relativePath + " missing token: " + token);
        }

        private static void AssertProjectFileDoesNotContain(string relativePath, string token)
        {
            string path = ToProjectPath(relativePath);
            Assert.IsFalse(FileContains(path, token), relativePath + " contains forbidden token: " + token);
        }

        private static bool FileContains(string path, string token)
        {
            foreach (string line in File.ReadLines(path))
            {
                if (line.IndexOf(token, StringComparison.Ordinal) >= 0)
                    return true;
            }

            return false;
        }

        private static string ToProjectPath(string relativePath)
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            return Path.Combine(projectRoot, relativePath);
        }

        private static int OffsetOf<T>(string fieldName) where T : struct
        {
            return Marshal.OffsetOf<T>(fieldName).ToInt32();
        }
    }
}
