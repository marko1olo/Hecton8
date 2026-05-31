using System.Runtime.InteropServices;
using Hecton8.Physics.Vehicles;
using NUnit.Framework;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

namespace Hecton8.Tests.Editor
{
    public sealed class SubmarineAddedMassTensorEditTests
    {
        [Test]
        public void AddedMassProfileDto_Layout_IsExplicit128Bytes()
        {
            Assert.AreEqual(128, UnsafeUtility.SizeOf<AddedMassProfileDTO>());
            Assert.AreEqual(0, Marshal.OffsetOf<AddedMassProfileDTO>(nameof(AddedMassProfileDTO.LinearAddedMass)).ToInt32());
            Assert.AreEqual(64, Marshal.OffsetOf<AddedMassProfileDTO>(nameof(AddedMassProfileDTO.AngularAddedMass)).ToInt32());
            Assert.AreEqual(64, UnsafeUtility.SizeOf<SubmarineAddedMassTuningDTO>());
            Assert.AreEqual(0, Marshal.OffsetOf<SubmarineAddedMassTuningDTO>(nameof(SubmarineAddedMassTuningDTO.BaseAddedMassMultiplier)).ToInt32());
            Assert.AreEqual(32, Marshal.OffsetOf<SubmarineAddedMassTuningDTO>(nameof(SubmarineAddedMassTuningDTO.SourceHash)).ToInt32());
        }

        [Test]
        public void CalculateAddedMassTensor_UsesDepthAndFloodVolume()
        {
            NativeArray<SubmarineKinematicState> states = new NativeArray<SubmarineKinematicState>(1, Allocator.TempJob);
            NativeArray<SubmarineMassProperties> masses = new NativeArray<SubmarineMassProperties>(1, Allocator.TempJob);
            NativeArray<SubmarineKinematicConfig> configs = new NativeArray<SubmarineKinematicConfig>(1, Allocator.TempJob);
            NativeArray<SubmarineHullProfileDTO> hulls = new NativeArray<SubmarineHullProfileDTO>(1, Allocator.TempJob);
            NativeArray<SubmarineAddedMassTuningDTO> tuning = new NativeArray<SubmarineAddedMassTuningDTO>(1, Allocator.TempJob);
            NativeArray<AddedMassProfileDTO> profiles = new NativeArray<AddedMassProfileDTO>(1, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            NativeArray<SubmarineHydrodynamicsTelemetry> telemetry = new NativeArray<SubmarineHydrodynamicsTelemetry>(SubmarineDynamicsConstants.BlackBoxFrames, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);

            try
            {
                states[0] = new SubmarineKinematicState
                {
                    Aup = new double3(0.0, -250.0, 0.0),
                    Rotation = quaternion.identity,
                    TotalMassKg = 18000f
                };
                masses[0] = new SubmarineMassProperties
                {
                    FloodMassKg = 2600f,
                    BaseMassKg = 18000f
                };
                configs[0] = new SubmarineKinematicConfig
                {
                    LocalOriginAup = double3.zero,
                    BaseMassKg = 18000f,
                    HullVolumeM3 = 22f,
                    FluidDensityKgPerM3 = MockFluidDensityGenerator.DefaultSeawaterDensityKgPerM3
                };
                hulls[0] = new SubmarineHullProfileDTO
                {
                    ProfileHash = SubmarineDynamicsConstants.SourceHashAddedMass,
                    BaseMassKg = 18000f,
                    HullVolumeM3 = 22f,
                    LengthMeters = 11f,
                    RadiusMeters = 1.4f,
                    AddedMassMultiplier = 1.25f,
                    FloodVolumeScalar = 1f
                };
                tuning[0] = new SubmarineAddedMassTuningDTO
                {
                    BaseAddedMassMultiplier = 1f,
                    DepthDensityLinear = 0.08f,
                    DepthDensityQuadratic = 0.05f,
                    RotationalDampingScalar = 1f,
                    MatrixBlendBias = 0f,
                    MaxDepthMeters = 6000f,
                    FloodVolumeScalar = 1f,
                    TensorAnisotropyScalar = 1f,
                    SourceHash = SubmarineDynamicsConstants.SourceHashAddedMass
                };

                CalculateAddedMassTensorJob job = new CalculateAddedMassTensorJob
                {
                    States = states,
                    MassProperties = masses,
                    Config = configs[0],
                    HullProfiles = hulls,
                    Tuning = tuning,
                    AddedMassProfiles = profiles,
                    HydrodynamicsTelemetry = telemetry,
                    GlobalQualityWeight = 1f,
                    Frame = 7u,
                    VehicleCount = 1
                };

                job.Execute(0);

                AddedMassProfileDTO profile = profiles[0];
                Assert.Greater(profile.LinearAddedMass.c0.x, 1f);
                Assert.Greater(profile.LinearAddedMass.c1.y, profile.LinearAddedMass.c2.z);
                Assert.Greater(profile.AngularAddedMass.c1.y, profile.AngularAddedMass.c0.x);
                Assert.AreEqual(250f, telemetry[7].DepthMeters, 0.01f);
                Assert.Greater(telemetry[7].DepthDensityScalar, 1f);
                Assert.AreNotEqual(0u, telemetry[7].Flags & SubmarineDynamicsConstants.HydroFlagFloodMassInjected);
            }
            finally
            {
                telemetry.Dispose();
                profiles.Dispose();
                tuning.Dispose();
                hulls.Dispose();
                configs.Dispose();
                masses.Dispose();
                states.Dispose();
            }
        }

        [Test]
        public void CalculateAddedMassTensor_FloodScalarZeroDisablesFloodTensorInflation()
        {
            NativeArray<SubmarineKinematicState> states = new NativeArray<SubmarineKinematicState>(2, Allocator.TempJob);
            NativeArray<SubmarineMassProperties> masses = new NativeArray<SubmarineMassProperties>(2, Allocator.TempJob);
            NativeArray<SubmarineKinematicConfig> configs = new NativeArray<SubmarineKinematicConfig>(1, Allocator.TempJob);
            NativeArray<SubmarineHullProfileDTO> hulls = new NativeArray<SubmarineHullProfileDTO>(2, Allocator.TempJob);
            NativeArray<SubmarineAddedMassTuningDTO> tuning = new NativeArray<SubmarineAddedMassTuningDTO>(1, Allocator.TempJob);
            NativeArray<AddedMassProfileDTO> profiles = new NativeArray<AddedMassProfileDTO>(2, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            NativeArray<SubmarineHydrodynamicsTelemetry> telemetry = new NativeArray<SubmarineHydrodynamicsTelemetry>(2 * SubmarineDynamicsConstants.BlackBoxFrames, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);

            try
            {
                SubmarineKinematicState state = new SubmarineKinematicState
                {
                    Aup = new double3(0.0, -80.0, 0.0),
                    Rotation = quaternion.identity,
                    TotalMassKg = 18000f
                };
                states[0] = state;
                states[1] = state;
                masses[0] = new SubmarineMassProperties { BaseMassKg = 18000f, FloodMassKg = 0f };
                masses[1] = new SubmarineMassProperties { BaseMassKg = 18000f, FloodMassKg = 2600f };
                configs[0] = new SubmarineKinematicConfig
                {
                    LocalOriginAup = double3.zero,
                    BaseMassKg = 18000f,
                    HullVolumeM3 = 22f,
                    FluidDensityKgPerM3 = MockFluidDensityGenerator.DefaultSeawaterDensityKgPerM3
                };

                SubmarineHullProfileDTO hull = new SubmarineHullProfileDTO
                {
                    ProfileHash = SubmarineDynamicsConstants.SourceHashAddedMass,
                    BaseMassKg = 18000f,
                    HullVolumeM3 = 22f,
                    LengthMeters = 11f,
                    RadiusMeters = 1.4f,
                    AddedMassMultiplier = 1f,
                    FloodVolumeScalar = 1f
                };
                hulls[0] = hull;
                hulls[1] = hull;
                tuning[0] = new SubmarineAddedMassTuningDTO
                {
                    BaseAddedMassMultiplier = 1f,
                    DepthDensityLinear = 0.08f,
                    DepthDensityQuadratic = 0.05f,
                    RotationalDampingScalar = 1f,
                    MatrixBlendBias = 0f,
                    MaxDepthMeters = 6000f,
                    FloodVolumeScalar = 0f,
                    TensorAnisotropyScalar = 1f,
                    SourceHash = SubmarineDynamicsConstants.SourceHashAddedMass
                };

                CalculateAddedMassTensorJob job = new CalculateAddedMassTensorJob
                {
                    States = states,
                    MassProperties = masses,
                    Config = configs[0],
                    HullProfiles = hulls,
                    Tuning = tuning,
                    AddedMassProfiles = profiles,
                    HydrodynamicsTelemetry = telemetry,
                    GlobalQualityWeight = 1f,
                    Frame = 11u,
                    VehicleCount = 2
                };

                job.Execute(0);
                job.Execute(1);

                float dryLateral = profiles[0].LinearAddedMass.c0.x;
                float floodedLateral = profiles[1].LinearAddedMass.c0.x;
                Assert.AreEqual(dryLateral, floodedLateral, dryLateral * 0.0001f);

                tuning[0] = new SubmarineAddedMassTuningDTO
                {
                    BaseAddedMassMultiplier = 1f,
                    DepthDensityLinear = 0.08f,
                    DepthDensityQuadratic = 0.05f,
                    RotationalDampingScalar = 1f,
                    MatrixBlendBias = 0f,
                    MaxDepthMeters = 6000f,
                    FloodVolumeScalar = 1f,
                    TensorAnisotropyScalar = 1f,
                    SourceHash = SubmarineDynamicsConstants.SourceHashAddedMass
                };

                SubmarineHullProfileDTO zeroFloodHull = hull;
                zeroFloodHull.FloodVolumeScalar = 0f;
                hulls[0] = hull;
                hulls[1] = zeroFloodHull;
                job.Frame = 12u;
                job.Execute(0);
                job.Execute(1);

                dryLateral = profiles[0].LinearAddedMass.c0.x;
                floodedLateral = profiles[1].LinearAddedMass.c0.x;
                Assert.AreEqual(dryLateral, floodedLateral, dryLateral * 0.0001f);
            }
            finally
            {
                telemetry.Dispose();
                profiles.Dispose();
                tuning.Dispose();
                hulls.Dispose();
                configs.Dispose();
                masses.Dispose();
                states.Dispose();
            }
        }
    }
}
