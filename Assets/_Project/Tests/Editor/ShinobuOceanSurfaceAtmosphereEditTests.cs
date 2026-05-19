using System.Runtime.InteropServices;
using Hecton8.Atmosphere;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Memory;
using NUnit.Framework;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace Hecton8.Tests.Editor
{
    public sealed class ShinobuOceanSurfaceAtmosphereEditTests
    {
        [Test]
        public void WaveParametersDto_Arm64Layout_IsExact()
        {
            Assert.AreEqual(32, UnsafeUtility.SizeOf<WaveParametersDTO>());
            Assert.AreEqual(0, OffsetOf<WaveParametersDTO>(nameof(WaveParametersDTO.DirectionAndSteepness)));
            Assert.AreEqual(16, OffsetOf<WaveParametersDTO>(nameof(WaveParametersDTO.PhaseSpeed)));
            Assert.AreEqual(20, OffsetOf<WaveParametersDTO>(nameof(WaveParametersDTO.Amplitude)));
            Assert.AreEqual(24, OffsetOf<WaveParametersDTO>(nameof(WaveParametersDTO.Wavelength)));
            Assert.AreEqual(28, OffsetOf<WaveParametersDTO>(nameof(WaveParametersDTO._pad0)));
        }

        [Test]
        public void AtmosphereAndWeatherDtos_AreStd140Aligned()
        {
            Assert.AreEqual(64, UnsafeUtility.SizeOf<AtmosphereDTO>());
            Assert.AreEqual(64, UnsafeUtility.SizeOf<WeatherStateDTO>());
            Assert.AreEqual(64, UnsafeUtility.SizeOf<OceanSurfaceTelemetryEntry>());
            Assert.AreEqual(64, UnsafeUtility.SizeOf<MockBuoyancyQuery>());
            Assert.AreEqual(32, UnsafeUtility.SizeOf<MockBuoyancyResult>());
            Assert.AreEqual(64, UnsafeUtility.SizeOf<WaterlineBreachSignal>());
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
        public void RadialGridLod_ExportsWrappedCameraAupForShaderPhase()
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
        public void GlobalQualityWeight_FadesWaveBudgetContinuously()
        {
            Assert.AreEqual(4f, HectonOceanSurfaceMath.ResolveDesiredWaveCount(0.1f, 16), 0.0001f);
            Assert.AreEqual(16f, HectonOceanSurfaceMath.ResolveDesiredWaveCount(1f, 16), 0.0001f);
            Assert.Greater(HectonOceanSurfaceMath.ResolveDesiredWaveCount(0.55f, 16), 4f);
            Assert.Less(HectonOceanSurfaceMath.ResolveDesiredWaveCount(0.55f, 16), 16f);
            Assert.AreEqual(0f, HectonOceanSurfaceMath.ResolveWaveContribution(4, 0.1f, 16), 0.0001f);
            Assert.Greater(HectonOceanSurfaceMath.ResolveWaveContribution(8, 0.55f, 16), 0f);
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

            WaveParametersDTO wave = waves[0];
            wave.DirectionAndSteepness.z += 0.37f;
            waves[0] = HectonOceanSurfaceMath.SanitizeWave(wave);
            uint phaseHash = HectonOceanSurfaceMath.HashWaveState(waves, waves.Length, 1.5f, 1f);
            Assert.AreNotEqual(originalHash, phaseHash);

            wave = waves[0];
            wave.PhaseSpeed += 0.19f;
            waves[0] = HectonOceanSurfaceMath.SanitizeWave(wave);
            uint speedHash = HectonOceanSurfaceMath.HashWaveState(waves, waves.Length, 1.5f, 1f);
            Assert.AreNotEqual(phaseHash, speedHash);
        }

        [Test]
        public void MockBuoyancyJob_ProcessesTenThousandAupQueries()
        {
            using NativeArray<WaveParametersDTO> waves = CreateSingleWaveArray(Allocator.TempJob);
            using NativeArray<MockBuoyancyQuery> queries = new NativeArray<MockBuoyancyQuery>(
                OceanSurfaceAtmosphereConstants.MockBuoyancyQueryCount,
                Allocator.TempJob,
                NativeArrayOptions.UninitializedMemory);
            using NativeArray<MockBuoyancyResult> results = new NativeArray<MockBuoyancyResult>(
                OceanSurfaceAtmosphereConstants.MockBuoyancyQueryCount,
                Allocator.TempJob,
                NativeArrayOptions.UninitializedMemory);

            JobHandle hydrate = new MockBuoyancyQueryHydrationJob
            {
                Queries = queries,
                CenterAUP = new double3(50000.0, 0.0, -50000.0),
                TimeSeconds = 7.5f,
                GlobalQualityWeight = 1f,
                SeaLevel = 0f,
                Seed = 0x53485236u,
                SectorHash = 0x4F434E36u,
                SimulationFrame = 240u
            }.Schedule(queries.Length, 128);

            new MockBuoyancyQueryJob
            {
                Queries = queries,
                Waves = waves,
                Results = results
            }.Schedule(results.Length, 128, hydrate).Complete();

            Assert.IsTrue(math.isfinite(results[0].Height));
            Assert.IsTrue(math.all(math.isfinite(results[0].Normal)));
            Assert.AreNotEqual(0f, math.lengthsq(results[0].Normal));
        }

        [Test]
        public void BufferIds_AreRegisteredForOceanSurfaceAtmosphere()
        {
            Assert.AreEqual(70760, (int)BufferID.ShinobuOceanWaveParameters);
            Assert.AreEqual(70761, (int)BufferID.ShinobuOceanAtmosphere);
            Assert.AreEqual(70762, (int)BufferID.ShinobuOceanWeatherState);
            Assert.AreEqual(70765, (int)BufferID.ShinobuOceanTelemetryRing);
            Assert.AreEqual(70767, (int)BufferID.ShinobuOceanDumpScratch);
        }

        private static NativeArray<WaveParametersDTO> CreateSingleWaveArray(Allocator allocator)
        {
            NativeArray<WaveParametersDTO> waves = new NativeArray<WaveParametersDTO>(
                OceanSurfaceAtmosphereConstants.WaveCapacity,
                allocator,
                NativeArrayOptions.ClearMemory);

            WaveParametersDTO wave = default;
            wave.DirectionAndSteepness = new float4(1f, 0f, 0.25f, 0.5f);
            wave.PhaseSpeed = 1.2f;
            wave.Amplitude = 2f;
            wave.Wavelength = 32f;
            waves[0] = HectonOceanSurfaceMath.SanitizeWave(wave);

            for (int i = 1; i < waves.Length; i++)
            {
                wave.DirectionAndSteepness = new float4(0f, 1f, i * 0.11f, 0.2f);
                wave.PhaseSpeed = 0.1f;
                wave.Amplitude = 0f;
                wave.Wavelength = 48f + i;
                waves[i] = HectonOceanSurfaceMath.SanitizeWave(wave);
            }

            return waves;
        }

        private static int OffsetOf<T>(string fieldName) where T : unmanaged
        {
            return Marshal.OffsetOf(typeof(T), fieldName).ToInt32();
        }
    }
}
