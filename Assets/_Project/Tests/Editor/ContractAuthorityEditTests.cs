using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using Hecton8.Core;
using Hecton8.Core.Contracts;
using Hecton8.Core.Contracts.Signals;
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
        public void ItemAcquiredSourceKinds_MatchWireContract()
        {
            Assert.That(ItemAcquiredSignalSourceKinds.Unknown, Is.EqualTo(0));
            Assert.That(ItemAcquiredSignalSourceKinds.ResourceNode, Is.EqualTo(1));
            Assert.That(ItemAcquiredSignalSourceKinds.ProceduralOreSpawner, Is.EqualTo(2));
            Assert.That(ItemAcquiredSignalSourceKinds.Fabricator, Is.EqualTo(4));
            Assert.That(ItemAcquiredSignalSourceKinds.DeconstructionRefund, Is.EqualTo(ItemAcquiredSignalSourceKinds.Fabricator));
            Assert.That(ItemAcquiredSignalSourceKinds.DeployableSdfDrill, Is.EqualTo(7));
            Assert.That(ItemAcquiredSignalSourceKinds.LootMagnet, Is.EqualTo(8));
            Assert.That(ItemAcquiredSignalSourceKinds.ManualPickup, Is.EqualTo(9));
            Assert.That(ItemAcquiredSignalSourceKinds.VoxelCarve, Is.EqualTo(12));
            Assert.That(ItemAcquiredSignalSourceKinds.ScavengingLootOracle, Is.EqualTo(13));
            Assert.That(ItemAcquiredSignalSourceKinds.HarvestableOutcrop, Is.EqualTo(14));
            Assert.That(ItemAcquiredSignalSourceKinds.DroneMining, Is.EqualTo(15));

            string root = Directory.GetCurrentDirectory();
            string lootMagnetContracts = File.ReadAllText(Path.Combine(
                root,
                "Assets/_Project/Scripts/Gameplay/Loot/Contracts/LootMagnetContracts.cs"));
            string inventoryPickupContracts = File.ReadAllText(Path.Combine(
                root,
                "Assets/_Project/Scripts/Interaction/InventoryPickupContracts.cs"));
            string constructionManager = File.ReadAllText(Path.Combine(
                root,
                "Assets/_Project/Scripts/ConstructionManager.cs"));
            string fabricator = File.ReadAllText(Path.Combine(
                root,
                "Assets/_Project/Scripts/Fabricator.cs"));
            string deployableDrill = File.ReadAllText(Path.Combine(
                root,
                "Assets/_Project/Scripts/Gameplay/Mining/DeployableSdfDrillRuntime.cs"));
            string voxelDeltaProcessor = File.ReadAllText(Path.Combine(
                root,
                "Assets/_Project/Scripts/VoxelDeltaProcessor.cs"));
            string proceduralOreSpawner = File.ReadAllText(Path.Combine(
                root,
                "Assets/_Project/Scripts/World/Resources/ProceduralOreSpawner.cs"));
            string harvestableOutcrop = File.ReadAllText(Path.Combine(
                root,
                "Assets/_Project/Scripts/Gameplay/HarvestableOutcrop.cs"));
            string scavengingLootOracle = File.ReadAllText(Path.Combine(
                root,
                "Assets/_Project/Scripts/Scavenging/ScavengingLootOracleRuntime.cs"));
            string droneFleetTransactions = File.ReadAllText(Path.Combine(
                root,
                "Assets/_Project/Scripts/Construction/DroneFleetManager_Transactions.cs"));

            Assert.That(lootMagnetContracts, Does.Contain("ItemSourceLootMagnet = ItemAcquiredSignalSourceKinds.LootMagnet"));
            Assert.That(inventoryPickupContracts, Does.Contain("ItemSourceManualPickup = ItemAcquiredSignalSourceKinds.ManualPickup"));
            Assert.That(constructionManager, Does.Contain("SourceKind = ItemAcquiredSignalSourceKinds.DeconstructionRefund"));
            Assert.That(fabricator, Does.Contain("SourceKind = ItemAcquiredSignalSourceKinds.Fabricator"));
            Assert.That(deployableDrill, Does.Contain("SourceKind = ItemAcquiredSignalSourceKinds.DeployableSdfDrill"));
            Assert.That(voxelDeltaProcessor, Does.Contain("SourceKind = ItemAcquiredSignalSourceKinds.VoxelCarve"));
            Assert.That(proceduralOreSpawner, Does.Contain("acquiredSignal.SourceKind = ItemAcquiredSignalSourceKinds.ProceduralOreSpawner"));
            Assert.That(harvestableOutcrop, Does.Contain("SourceKind = ItemAcquiredSignalSourceKinds.HarvestableOutcrop"));
            Assert.That(scavengingLootOracle, Does.Contain("public const byte ItemSourceKind = ItemAcquiredSignalSourceKinds.ScavengingLootOracle"));
            Assert.That(droneFleetTransactions, Does.Contain("signal.SourceKind = ItemAcquiredSignalSourceKinds.DroneMining"));
        }

        [Test]
        public void FirstPartySignalRoutes_TrackSignalPushDrops()
        {
            Assert.That(ItemLifecycleSignalRoute.DroppedSignalCount, Is.GreaterThanOrEqualTo(0));
            Assert.That(SessionLifecycleSignalRoute.DroppedSignalCount, Is.GreaterThanOrEqualTo(0));
            Assert.That(ProgressionMetaSignalRoute.DroppedSignalCount, Is.GreaterThanOrEqualTo(0));

            string root = Directory.GetCurrentDirectory();
            string itemLifecycleRoute = File.ReadAllText(Path.Combine(
                root,
                "Assets/_Project/Scripts/Core/Signals/ItemLifecycleSignalRoute.cs"));
            string sessionLifecycleRoute = File.ReadAllText(Path.Combine(
                root,
                "Assets/_Project/Scripts/Core/Signals/SessionLifecycleSignalRoute.cs"));
            string progressionMetaRoute = File.ReadAllText(Path.Combine(
                root,
                "Assets/_Project/Scripts/Core/Signals/ProgressionMetaSignalRoute.cs"));

            Assert.That(itemLifecycleRoute, Does.Contain("public static int DroppedSignalCount => Volatile.Read(ref s_signalPushDropCount)"));
            Assert.That(itemLifecycleRoute, Does.Contain("if (itemHash == 0u)"));
            Assert.That(itemLifecycleRoute, Does.Contain("bool validRuntimePosition = hasRuntimePosition && math.all(math.isfinite(signalPosition));"));
            Assert.That(itemLifecycleRoute, Does.Contain("byte flags = BuildFlags(item, validRuntimePosition, itemHash);"));
            Assert.That(itemLifecycleRoute, Does.Contain("RuntimePosition = validRuntimePosition ? signalPosition : float3.zero"));
            Assert.That(itemLifecycleRoute, Does.Contain("SignalBus<ItemLifecycleSignal>.TryPushTracked(in signal, ref s_signalPushDropCount)"));
            Assert.That(itemLifecycleRoute, Does.Not.Contain("SignalBus<ItemLifecycleSignal>.TryPush(in signal)"));

            Assert.That(sessionLifecycleRoute, Does.Contain("public static int DroppedSignalCount => Volatile.Read(ref s_signalPushDropCount)"));
            Assert.That(sessionLifecycleRoute, Does.Contain("SignalBus<SessionLifecycleSignal>.TryPushTracked(in signal, ref s_signalPushDropCount)"));
            Assert.That(sessionLifecycleRoute, Does.Not.Contain("SignalBus<SessionLifecycleSignal>.TryPush(in signal)"));

            Assert.That(progressionMetaRoute, Does.Contain("public static int DroppedSignalCount => Volatile.Read(ref s_signalPushDropCount)"));
            Assert.That(progressionMetaRoute, Does.Contain("SignalBus<ProgressionMetaSignal>.TryPushTracked(in signal, ref s_signalPushDropCount)"));
            Assert.That(progressionMetaRoute, Does.Not.Contain("SignalBus<ProgressionMetaSignal>.TryPush(in signal)"));
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
