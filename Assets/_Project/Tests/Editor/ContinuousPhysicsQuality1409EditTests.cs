using System.IO;
using Hecton8.Physics.KCC;
using Hecton8.Physics.Vehicles;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Tests.Editor
{
    public sealed class ContinuousPhysicsQuality1409EditTests
    {
        [Test]
        public void BallastBuoyancy_QualitySweep_PreservesIntegratedForce()
        {
            SubmarineBallastForcePacketDTO low = EvaluateBallastForce(0f);
            SubmarineBallastForcePacketDTO mid = EvaluateBallastForce(0.5f);
            SubmarineBallastForcePacketDTO high = EvaluateBallastForce(1f);

            Assert.AreEqual(4, low.ActiveSamples);
            Assert.AreEqual(low.SubmergedRatio, mid.SubmergedRatio, 0.000001f);
            Assert.AreEqual(low.SubmergedRatio, high.SubmergedRatio, 0.000001f);
            Assert.AreEqual(low.NetForce.y, mid.NetForce.y, 0.01f);
            Assert.AreEqual(low.NetForce.y, high.NetForce.y, 0.01f);
            Assert.That(low.NetForce.y, Is.GreaterThan(1f));
        }

        [Test]
        public void KccDynamicEpsilon_QualitySweep_IsContinuousAndMonotonic()
        {
            const float skin = 0.08f;
            float q0 = HydrodynamicKccMath.ResolveDynamicPenetrationEpsilon(0f, skin);
            float q25 = HydrodynamicKccMath.ResolveDynamicPenetrationEpsilon(0.25f, skin);
            float q50 = HydrodynamicKccMath.ResolveDynamicPenetrationEpsilon(0.5f, skin);
            float q75 = HydrodynamicKccMath.ResolveDynamicPenetrationEpsilon(0.75f, skin);
            float q1 = HydrodynamicKccMath.ResolveDynamicPenetrationEpsilon(1f, skin);

            Assert.Greater(q0, q25);
            Assert.Greater(q25, q50);
            Assert.Greater(q50, q75);
            Assert.Greater(q75, q1);
            Assert.AreEqual((q0 + q1) * 0.5f, q50, 0.000001f);
        }

        [Test]
        public void BallastBuoyancy_NonFiniteSwell_FailsClosedWithoutNaN()
        {
            SubmarineBallastForcePacketDTO packet = EvaluateBallastForce(0.5f, float.NaN);

            Assert.IsTrue(math.isfinite(packet.SubmergedRatio));
            Assert.IsTrue(math.all(math.isfinite(packet.NetForce)));
            Assert.IsTrue(math.all(math.isfinite(packet.BuoyantForce)));
        }

        [Test]
        public void ContinuousPhysics1409_StaticAudit_BansReintroducedQualityConstants()
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string[] files =
            {
                Path.Combine(projectRoot, "Assets", "_Project", "Scripts", "Physics", "Seaglide", "SeaglideHydrodynamicsRuntime.cs"),
                Path.Combine(projectRoot, "Assets", "_Project", "Scripts", "Physics", "Buoyancy", "BuoyancyDisplacementRuntime.cs"),
                Path.Combine(projectRoot, "Assets", "_Project", "Scripts", "Physics", "Buoyancy", "AsyncReadback", "AsyncBuoyancyReadbackRuntime.cs"),
                Path.Combine(projectRoot, "Assets", "_Project", "Scripts", "Physics", "Cavitation", "AbyssalCavitationRuntime.cs"),
                Path.Combine(projectRoot, "Assets", "_Project", "Scripts", "Physics", "Vehicles", "SubmarineBallastBuoyancyContracts.cs"),
                Path.Combine(projectRoot, "Assets", "_Project", "Scripts", "Physics", "KCC", "HydrodynamicKccRuntime.cs"),
                Path.Combine(projectRoot, "Assets", "_Project", "Art", "Shaders", "Hecton_WaveHeightSampler.compute")
            };

            string[] forbidden =
            {
                "ResolvedQualityWeight = " + "SeaglideSimdMath.AuthoritativeQualityWeight",
                "GlobalQualityWeight = " + "BuoyancyDisplacementConstants.AuthoritativeQualityWeight",
                "GlobalQualityWeight = " + "SubmarineBallastConstants.AuthoritativeQualityWeight",
                "SetFloat(OceanQualityId, " + "AsyncBuoyancyReadbackConstants.AuthoritativeQualityWeight",
                "SetVector(WaveSampleLodId, new Vector4(maxWavelength, activeWaveCount, " + "AsyncBuoyancyReadbackConstants.AuthoritativeQualityWeight",
                "Smooth01(" + "AbyssalCavitationConstants.AuthoritativeQualityWeight",
                "GlobalQualityWeight = math.saturate(Tuning.GlobalQualityWeight)",
                "float quality = " + "HydrodynamicKccMath.AuthoritativeQualityWeight"
            };

            for (int i = 0; i < files.Length; i++)
            {
                string source = File.ReadAllText(files[i]);
                for (int j = 0; j < forbidden.Length; j++)
                    Assert.IsFalse(source.Contains(forbidden[j]), forbidden[j] + " in " + files[i]);
            }
        }

        private static SubmarineBallastForcePacketDTO EvaluateBallastForce(float quality, float surfaceSwellMeters = 2f)
        {
            NativeArray<BallastTankDTO> tanks = new NativeArray<BallastTankDTO>(1, Allocator.TempJob);
            NativeArray<SubmarineBallastFluidSampleDTO> samples = new NativeArray<SubmarineBallastFluidSampleDTO>(1, Allocator.TempJob);
            NativeArray<SubmarineBallastForcePacketDTO> packets = new NativeArray<SubmarineBallastForcePacketDTO>(1, Allocator.TempJob);
            NativeArray<SubmarineBallastTelemetryEntry> telemetry = new NativeArray<SubmarineBallastTelemetryEntry>(SubmarineBallastConstants.TelemetryCapacity, Allocator.TempJob);

            try
            {
                samples[0] = new SubmarineBallastFluidSampleDTO
                {
                    HullAup = new double3(0d, -10d, 0d),
                    OceanSurfaceAup = new double3(0d, -7d, 0d),
                    HullHeightMeters = 10f,
                    HullVolumeCubicMeters = 25f,
                    FluidDensityKgPerM3 = 1030f,
                    AmbientPressureATM = 1.3f,
                    GlobalQualityWeight = quality,
                    SurfaceSwellMeters = surfaceSwellMeters,
                    TargetEntityHash = 1409u
                };

                CalculateBuoyancyForceJob job = new CalculateBuoyancyForceJob
                {
                    Tanks = tanks,
                    FluidSamples = samples,
                    ForcePackets = packets,
                    TelemetryRing = telemetry,
                    TankCount = 0,
                    Frame = 17u
                };

                job.Execute(0);
                Assert.AreEqual(math.saturate(quality), telemetry[17].GlobalQualityWeight, 0.000001f);
                return packets[0];
            }
            finally
            {
                telemetry.Dispose();
                packets.Dispose();
                samples.Dispose();
                tanks.Dispose();
            }
        }
    }
}
