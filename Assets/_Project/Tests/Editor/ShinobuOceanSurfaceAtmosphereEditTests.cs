using System.IO;
using System.Runtime.InteropServices;
using Hecton8.Atmosphere;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Memory;
using NUnit.Framework;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

namespace Hecton8.Tests.Editor
{
    public sealed class ShinobuOceanSurfaceAtmosphereEditTests
    {
        [Test]
        public void WaveParametersDto_Arm64Layout_IsExact()
        {
            Assert.AreEqual(64, UnsafeUtility.SizeOf<WaveParametersDTO>());
            Assert.AreEqual(0, OffsetOf<WaveParametersDTO>(nameof(WaveParametersDTO.Wave1)));
            Assert.AreEqual(16, OffsetOf<WaveParametersDTO>(nameof(WaveParametersDTO.Wave2)));
            Assert.AreEqual(32, OffsetOf<WaveParametersDTO>(nameof(WaveParametersDTO.Wave3)));
            Assert.AreEqual(48, OffsetOf<WaveParametersDTO>(nameof(WaveParametersDTO.GlobalWindAndStorm)));
            Assert.AreEqual(64, UnsafeUtility.SizeOf<OceanWaveAupPhaseDTO>());
            Assert.AreEqual(0, OffsetOf<OceanWaveAupPhaseDTO>(nameof(OceanWaveAupPhaseDTO.PhaseBase0)));
            Assert.AreEqual(16, OffsetOf<OceanWaveAupPhaseDTO>(nameof(OceanWaveAupPhaseDTO.PhaseBase1)));
            Assert.AreEqual(32, OffsetOf<OceanWaveAupPhaseDTO>(nameof(OceanWaveAupPhaseDTO.CameraAupLocalXZ)));
            Assert.AreEqual(48, OffsetOf<OceanWaveAupPhaseDTO>(nameof(OceanWaveAupPhaseDTO.Frame)));
        }

        [Test]
        public void AtmosphereAndWeatherDtos_AreStd140Aligned()
        {
            Assert.AreEqual(64, UnsafeUtility.SizeOf<AtmosphereDTO>());
            Assert.AreEqual(64, UnsafeUtility.SizeOf<WeatherStateDTO>());
            Assert.AreEqual(64, UnsafeUtility.SizeOf<OceanSurfaceTelemetryEntry>());
            Assert.AreEqual(64, UnsafeUtility.SizeOf<WaterlineBreachSignal>());
            Assert.AreEqual(64, UnsafeUtility.SizeOf<BeaufortProfileDTO>());
        }

        [Test]
        public void GerstnerPhase_WrapsAupBeforeFloatTrig()
        {
            using NativeArray<WaveParametersDTO> waves = CreateSingleWaveArray(Allocator.TempJob);
            double3 a = new double3(50000.0, 0.0, -23000.0);
            double3 b = a + new double3(32.0 * 10000.0, 0.0, 0.0);

            HectonOceanSurfaceMath.EvaluateWaves(a, 12.5f, waves, 1f, out float heightA, out float3 normalA);
            HectonOceanSurfaceMath.EvaluateWaves(b, 12.5f, waves, 1f, out float heightB, out float3 normalB);

            Assert.AreEqual(heightA, heightB, 0.0002f);
            Assert.AreEqual(normalA.x, normalB.x, 0.0002f);
            Assert.AreEqual(normalA.y, normalB.y, 0.0002f);
            Assert.AreEqual(normalA.z, normalB.z, 0.0002f);
        }

        [Test]
        public void RadialGridLod_ExportsWrappedCameraAupForLodOnly()
        {
            double3 cameraAup = new double3(50000.0, 12.0, -23000.0);
            OceanSurfaceLodDTO lod = HectonOceanSurfaceMath.ResolveRadialGridLod(cameraAup, 1f);

            Assert.AreEqual((float)HectonOceanSurfaceMath.WrapMeters(cameraAup.x, 4096.0), lod.CameraAupLocalXZ.x, 0.0001f);
            Assert.AreEqual((float)HectonOceanSurfaceMath.WrapMeters(cameraAup.z, 4096.0), lod.CameraAupLocalXZ.y, 0.0001f);

            double3 rebasedCameraAup = cameraAup + new double3(4096.0 * 17.0, 0.0, -4096.0 * 5.0);
            OceanSurfaceLodDTO rebasedLod = HectonOceanSurfaceMath.ResolveRadialGridLod(rebasedCameraAup, 1f);
            Assert.AreEqual(lod.CameraAupLocalXZ.x, rebasedLod.CameraAupLocalXZ.x, 0.0001f);
            Assert.AreEqual(lod.CameraAupLocalXZ.y, rebasedLod.CameraAupLocalXZ.y, 0.0001f);
        }

        [Test]
        public void AupPhaseBase_WrapsProjectedMetersPerLane()
        {
            using NativeArray<WaveParametersDTO> waves = CreateSingleWaveArray(Allocator.TempJob);
            WaveParametersDTO wave = waves[0];
            float4 lane = HectonOceanSurfaceMath.GetWaveLane(wave, 2);
            float2 direction = HectonOceanSurfaceMath.WaveLaneDirection(lane);
            float wavelength = HectonOceanSurfaceMath.WaveLaneWavelength(lane);
            double3 cameraAup = new double3(50000.0, 0.0, -23000.0);
            double projected = (cameraAup.x * direction.x) + (cameraAup.z * direction.y);

            float phaseA = HectonOceanSurfaceMath.ResolveAupPhaseBaseRadians(projected, wavelength);
            float phaseB = HectonOceanSurfaceMath.ResolveAupPhaseBaseRadians(projected + (wavelength * 1024.0), wavelength);
            OceanWaveAupPhaseDTO phaseDto = HectonOceanSurfaceMath.ResolveAupPhaseBases(cameraAup, waves, 17u, 1f, 6);

            Assert.AreEqual(phaseA, phaseB, 0.0002f);
            Assert.AreEqual(phaseA, HectonOceanSurfaceMath.GetAupPhaseBase(phaseDto, 2), 0.0002f);
            Assert.AreEqual(17u, phaseDto.Frame);
            Assert.AreEqual(1f, phaseDto.GlobalQualityWeight, 0.0001f);
        }

        [Test]
        public void GlobalQualityWeight_FadesWaveBudgetContinuously()
        {
            Assert.AreEqual(0f, HectonOceanSurfaceMath.SanitizeQualityWeight(float.NaN), 0.0001f);
            Assert.AreEqual(1f, HectonOceanSurfaceMath.ResolveDesiredWaveCount(0f, 6), 0.0001f);
            Assert.AreEqual(1f, HectonOceanSurfaceMath.ResolveDesiredWaveCount(float.NaN, 6), 0.0001f);
            Assert.GreaterOrEqual(HectonOceanSurfaceMath.ResolveDesiredWaveCount(0.1f, 6), 1f);
            Assert.AreEqual(6f, HectonOceanSurfaceMath.ResolveDesiredWaveCount(1f, 6), 0.0001f);
            Assert.Greater(HectonOceanSurfaceMath.ResolveDesiredWaveCount(0.55f, 6), 1f);
            Assert.Less(HectonOceanSurfaceMath.ResolveDesiredWaveCount(0.55f, 6), 6f);
            Assert.AreEqual(0f, HectonOceanSurfaceMath.ResolveWaveContribution(1, 0f, 6), 0.0001f);
            Assert.AreEqual(0f, HectonOceanSurfaceMath.ResolveWaveContribution(2, 0.1f, 6), 0.0001f);
            Assert.Greater(HectonOceanSurfaceMath.ResolveWaveContribution(2, 0.55f, 6), 0f);
        }

        [Test]
        public void RuntimeQualityWeight_DrivesWaveCadenceReadbackAndTelemetry()
        {
            string path = Path.Combine("Assets", "_Project", "Scripts", "Atmosphere", "ShinobuOceanSurfaceAtmosphereRuntime.cs");
            string source = File.ReadAllText(path).Replace("\r\n", "\n");

            StringAssert.Contains("_timeSeconds = ResolveWaveEvaluationTime(_rawSimulationTimeSeconds, _globalQualityWeight);", source);
            StringAssert.Contains("ResolveReadbackSampleBudget(_globalQualityWeight)", source);
            StringAssert.Contains("ResolveFullWaveCount(_globalQualityWeight, OceanSurfaceAtmosphereConstants.MaxWaveOctaves)", source);
            StringAssert.Contains("entry.ActiveWaveCount = HectonOceanSurfaceMath.ResolveFullWaveCount(_globalQualityWeight, limit);", source);
            StringAssert.DoesNotContain("ResolveWaveEvaluationTime(_rawSimulationTimeSeconds, OceanSurfaceAtmosphereConstants.AuthoritativeQualityWeight)", source);
            StringAssert.DoesNotContain("ResolveReadbackSampleBudget(OceanSurfaceAtmosphereConstants.AuthoritativeQualityWeight)", source);
            StringAssert.DoesNotContain("ResolveFullWaveCount(OceanSurfaceAtmosphereConstants.AuthoritativeQualityWeight, limit)", source);
            StringAssert.DoesNotContain("const float authorityQuality = OceanSurfaceAtmosphereConstants.AuthoritativeQualityWeight;", source);
            StringAssert.DoesNotContain("authorityQuality", source);
        }

        [Test]
        public void RuntimeOceanReadAccessors_DoNotQueueWaveReadbacks()
        {
            string path = Path.Combine("Assets", "_Project", "Scripts", "Atmosphere", "ShinobuOceanSurfaceAtmosphereRuntime.cs");
            string source = File.ReadAllText(path).Replace("\r\n", "\n");
            string readAccessorRegion = SliceBetween(
                source,
                "public bool TrySampleWaveHeight(float3 position, float minSpatialLength, out float waterHeight)",
                "public void AssignWaveHeightSamplerCompute(ComputeShader computeShader)");
            string surfaceWeatherReadRegion = SliceBetween(
                source,
                "public bool TryGetSurfaceWeatherState(out HectonOceanSurfaceWeatherState state)",
                "public bool ApplySurfaceWeatherState(in HectonOceanSurfaceWeatherState state)");

            StringAssert.DoesNotContain("QueueWaveHeightSample", readAccessorRegion);
            StringAssert.DoesNotContain("TryCompleteWaveParameterKernel()", readAccessorRegion);
            StringAssert.DoesNotContain("TryCompleteWaveParameterKernel()", surfaceWeatherReadRegion);
            StringAssert.Contains("TryEvaluateWaveKinematicsSnapshot", readAccessorRegion);
            StringAssert.Contains("if (_waveParameterJobScheduled || !ResolveWaveBuffer(out NativeArray<WaveParametersDTO> waves))", source);
            StringAssert.Contains("HectonOceanSurfaceMath.EvaluateWavesDetailed", source);
        }

        [Test]
        public void GerstnerPhase_WrapsLongEnduranceTimeBeforeSincos()
        {
            float phase = HectonOceanSurfaceMath.WrapPhaseRadians(987654.25f);
            Assert.GreaterOrEqual(phase, 0f);
            Assert.Less(phase, OceanSurfaceAtmosphereConstants.TwoPi);

            using NativeArray<WaveParametersDTO> waves = CreateSingleWaveArray(Allocator.TempJob);
            HectonOceanSurfaceMath.EvaluateWaves(
                new double3(50000.0, 0.0, -23000.0),
                360000f,
                waves,
                1f,
                out float height,
                out float3 normal);

            Assert.IsTrue(math.isfinite(height));
            Assert.IsTrue(math.all(math.isfinite(normal)));
        }

        [Test]
        public void WaveStateHash_TracksPhaseAndSpeedChanges()
        {
            using NativeArray<WaveParametersDTO> waves = CreateSingleWaveArray(Allocator.TempJob);
            uint originalHash = HectonOceanSurfaceMath.HashWaveState(waves, waves.Length, 1.5f, 1f);
            NativeArray<WaveParametersDTO> writableWaves = waves;

            WaveParametersDTO wave = waves[0];
            wave.Wave1.x += 0.37f;
            writableWaves[0] = HectonOceanSurfaceMath.SanitizeWave(wave);
            uint phaseHash = HectonOceanSurfaceMath.HashWaveState(waves, waves.Length, 1.5f, 1f);
            Assert.AreNotEqual(originalHash, phaseHash);

            wave = waves[0];
            wave.Wave1.w += 0.19f;
            writableWaves[0] = HectonOceanSurfaceMath.SanitizeWave(wave);
            uint speedHash = HectonOceanSurfaceMath.HashWaveState(waves, waves.Length, 1.5f, 1f);
            Assert.AreNotEqual(phaseHash, speedHash);
        }

        [Test]
        public void BufferIds_AreRegisteredForOceanSurfaceAtmosphere()
        {
            Assert.AreEqual(70760, (int)BufferID.ShinobuOceanWaveParameters);
            Assert.AreEqual(70761, (int)BufferID.ShinobuOceanAtmosphere);
            Assert.AreEqual(70762, (int)BufferID.ShinobuOceanWeatherState);
            Assert.AreEqual(70765, (int)BufferID.ShinobuOceanTelemetryRing);
            Assert.AreEqual(70767, (int)BufferID.ShinobuOceanDumpScratch);
            Assert.AreEqual(70769, (int)BufferID.ShinobuOceanWaveReadbackQueries);
            Assert.AreEqual(70770, (int)BufferID.ShinobuOceanWaveReadbackResults);
            Assert.AreEqual(70771, (int)BufferID.ShinobuOceanWaveReadbackCompletedQueries);
            Assert.AreEqual(70772, (int)BufferID.ShinobuOceanWaveReadbackRingQueries);
            Assert.AreEqual(70773, (int)BufferID.ShinobuOceanBeaufortProfiles);
            Assert.AreEqual(70774, (int)BufferID.ShinobuOceanSurfaceSwell);
        }

        private static NativeArray<WaveParametersDTO> CreateSingleWaveArray(Allocator allocator)
        {
            NativeArray<WaveParametersDTO> waves = new NativeArray<WaveParametersDTO>(
                OceanSurfaceAtmosphereConstants.WaveCapacity,
                allocator,
                NativeArrayOptions.ClearMemory);

            WaveParametersDTO wave = default;
            wave.Wave1 = HectonOceanSurfaceMath.CreateWaveLane(0f, 0.5f, 32f, 1.2f);
            wave.Wave2 = HectonOceanSurfaceMath.CreateWaveLane(1.5707964f, 0.2f, 48f, 0.4f);
            wave.Wave3 = HectonOceanSurfaceMath.CreateWaveLane(0.7853982f, 0.1f, 64f, 0.25f);
            wave.GlobalWindAndStorm = new float4(1f, 0f, 11f, 0.4f);
            waves[0] = HectonOceanSurfaceMath.SanitizeWave(wave);

            for (int i = 1; i < waves.Length; i++)
            {
                wave = default;
                wave.Wave1 = HectonOceanSurfaceMath.CreateWaveLane(1.5707964f, 0.05f, 80f + i, 0.1f);
                wave.GlobalWindAndStorm = new float4(0f, 1f, 4f, 0.1f);
                waves[i] = HectonOceanSurfaceMath.SanitizeWave(wave);
            }

            return waves;
        }

        private static int OffsetOf<T>(string fieldName) where T : unmanaged
        {
            return Marshal.OffsetOf(typeof(T), fieldName).ToInt32();
        }

        private static string SliceBetween(string source, string startToken, string endToken)
        {
            int start = source.IndexOf(startToken, System.StringComparison.Ordinal);
            Assert.GreaterOrEqual(start, 0, $"Missing start token: {startToken}");
            int end = source.IndexOf(endToken, start, System.StringComparison.Ordinal);
            Assert.Greater(end, start, $"Missing end token: {endToken}");
            return source.Substring(start, end - start);
        }
    }
}
