using System;
using System.Reflection;
using System.Runtime.InteropServices;
using Hecton8.Core.Contracts;
using NUnit.Framework;
using Unity.Mathematics;

namespace Hecton8.Tests.Editor
{
    public sealed class ContractAuthorityEditTests
    {
        [Test]
        public void PhysicsConstants_AreFiniteAndPossible()
        {
            ref readonly float gravity = ref HectonPhysicsContract.GravityMetersPerSecondSquared;
            ref readonly float waterDensity = ref HectonPhysicsContract.WaterDensityKgPerCubicMeter;
            ref readonly double aupSector = ref HectonPhysicsContract.AupSectorSizeMeters;

            Assert.That(math.isfinite(gravity), Is.True);
            Assert.That(gravity, Is.GreaterThan(0f));
            Assert.That(math.isfinite(waterDensity), Is.True);
            Assert.That(waterDensity, Is.GreaterThan(1000f));
            Assert.That(HectonPhysicsContract.HydrostaticPressureKPaPerMeter, Is.GreaterThan(0f));
            Assert.That(math.isfinite(aupSector), Is.True);
            Assert.That(aupSector, Is.EqualTo(5000.0d));
        }

        [Test]
        public void SurvivalConstants_AreFiniteAndPossible()
        {
            Assert.That(HectonSurvivalContract.StandardOxygenKPa, Is.GreaterThan(0f).And.LessThan(HectonSurvivalContract.KPaPerAtmosphere));
            Assert.That(HectonSurvivalContract.StandardOxygenFraction01, Is.InRange(0f, 1f));
            Assert.That(HectonSurvivalContract.MaxOxygenFraction01, Is.EqualTo(1f));
            Assert.That(HectonSurvivalContract.DefaultPlayerOxygenKPaPerSecond, Is.GreaterThan(0f));
            Assert.That(HectonSurvivalContract.DefaultCo2FatalKPa, Is.GreaterThan(HectonSurvivalContract.DefaultCo2ToxicityThresholdKPa));
        }

        [Test]
        public void EcologyAndScalabilityConstants_AreBounded()
        {
            Assert.That(HectonEcologyContract.LotkaBirthRate, Is.GreaterThan(0f));
            Assert.That(HectonEcologyContract.LotkaFeedRate, Is.GreaterThan(0f));
            Assert.That(HectonEcologyContract.LotkaPreyCarryingCapacity, Is.GreaterThan(1f));
            Assert.That(HectonEcologyContract.WorldPreyBirthRatePerSecond, Is.GreaterThan(0f));
            Assert.That(HectonEcologyContract.WorldReproductionFoodThreshold01, Is.InRange(0f, 1f));
            Assert.That(ScalabilityContract.MaxBoidsCount_Low, Is.LessThan(ScalabilityContract.MaxBoidsCount_Ultra));

            float lodSum = ScalabilityContract.Lod0ScreenRatio01 +
                           ScalabilityContract.Lod1ScreenRatio01 +
                           ScalabilityContract.Lod2ScreenRatio01;
            Assert.That(lodSum, Is.EqualTo(1f).Within(0.0001f));
        }

        [Test]
        public void SignalLaneIds_AreUnique()
        {
            FieldInfo[] fields = typeof(HectonSignalLaneContract).GetFields(BindingFlags.Public | BindingFlags.Static);
            bool[] seen = new bool[HectonDataSovereigntyContract.TypedSignalLaneMaxCount + 1];
            int laneCount = 0;

            for (int i = 0; i < fields.Length; i++)
            {
                FieldInfo field = fields[i];
                if (field.FieldType != typeof(byte))
                    continue;

                byte lane = (byte)field.GetRawConstantValue();
                Assert.That(lane, Is.Not.EqualTo(0), field.Name + " uses reserved lane 0.");
                Assert.That(lane, Is.LessThanOrEqualTo(HectonDataSovereigntyContract.TypedSignalLaneMaxCount), field.Name + " exceeds lane capacity.");
                Assert.That(seen[lane], Is.False, field.Name + " duplicates lane id " + lane + ".");
                seen[lane] = true;
                laneCount++;
            }

            Assert.That(laneCount, Is.GreaterThan(0));
            Assert.That(HectonSignalLaneContract.SignalLaneRegistryHash, Is.Not.EqualTo(0u));
        }

        [Test]
        public void ContractVersionHash_IsPresent()
        {
            Assert.That(HectonContractVersion.IsValid, Is.True);
            Assert.That(HectonContractVersion.HashLo, Is.Not.EqualTo(0UL));
            Assert.That(HectonContractVersion.HashHi, Is.Not.EqualTo(0UL));
        }

        [Test]
        public void ContractStructs_AreQuestSafePackOne()
        {
            Type[] types = typeof(HectonPhysicsContract).Assembly.GetTypes();
            for (int i = 0; i < types.Length; i++)
            {
                Type type = types[i];
                if (!type.IsValueType ||
                    type.IsEnum ||
                    type.Namespace != "Hecton8.Core.Contracts")
                {
                    continue;
                }

                var layout = (StructLayoutAttribute)Attribute.GetCustomAttribute(type, typeof(StructLayoutAttribute));
                Assert.That(layout, Is.Not.Null, type.FullName + " lacks StructLayout.");
                Assert.That(layout.Pack, Is.EqualTo(HectonPlatformContract.AbiStructPackBytes), type.FullName + " is not Pack=1.");
            }
        }

        [Test]
        public void PlatformAndDataSovereigntyConstants_AreBounded()
        {
            Assert.That(HectonPlatformContract.QuestSafeComputeThreadsPerGroup, Is.LessThanOrEqualTo(HectonPlatformContract.UniversalMaxComputeThreadsPerGroup));
            Assert.That(HectonPlatformContract.MetalSafeComputeThreadsPerGroup, Is.LessThanOrEqualTo(HectonPlatformContract.UniversalMaxComputeThreadsPerGroup));
            Assert.That(HectonPlatformContract.UniversalMaxComputeThreadsPerGroup, Is.EqualTo(1024));
            Assert.That(HectonPlatformContract.SteamDeckMicroSdReadBudgetBytesPerFrameLow, Is.LessThan(HectonPlatformContract.SteamDeckMicroSdReadBudgetBytesPerFrameUltra));
            Assert.That(HectonDataSovereigntyContract.BlackBoxFrameCapacity, Is.EqualTo(300));
            Assert.That(HectonDataSovereigntyContract.TypedSignalLaneMaxCount, Is.EqualTo(255));
        }

        [Test]
        public void VisualOverkillConstants_ScaleFromDearLieToUltra()
        {
            Assert.That(HectonVisualOverkillContract.LowTierRaymarchSteps, Is.EqualTo(0));
            Assert.That(HectonVisualOverkillContract.LowTierPomTaps, Is.EqualTo(0));
            Assert.That(HectonVisualOverkillContract.UltraTierRaymarchSteps, Is.GreaterThan(HectonVisualOverkillContract.HighTierRaymarchSteps));
            Assert.That(HectonVisualOverkillContract.UltraTierPomTaps, Is.EqualTo(16));
            Assert.That(HectonVisualOverkillContract.UltraTierWakeSiltParticles, Is.GreaterThan(HectonVisualOverkillContract.LowTierWakeSiltParticles));
            Assert.That(HectonVisualOverkillContract.UltraTierSaltCrystalSpawnChance01, Is.InRange(0f, 1f));
        }
    }
}
