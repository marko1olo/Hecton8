using System;
using System.IO;
using NUnit.Framework;

namespace Hecton8.Tests.Editor
{
    public sealed class EditorNativeMemorySentinelStaticAuditTests
    {
        private const string LSystemGenomeLabPath = "Assets/_Project/Scripts/Editor/LSystemGenomeLabWindow.cs";
        private const string AnomalySmokeTesterPath = "Assets/_Project/Scripts/Editor/AnomalySmokeTester.cs";
        private const string AnomalyTestHarnessPath = "Assets/_Project/Scripts/Editor/AnomalyTestHarness.cs";
        private const string AutomationSmokeTesterPath = "Assets/_Project/Scripts/AutomationSmokeTester.cs";
        private const string AutomationOmegaSmokeTesterPath = "Assets/_Project/Scripts/AutomationOmegaSmokeTester.cs";
        private const string ThermalMeltSmokeTesterPath = "Assets/_Project/Scripts/ThermalMeltSmokeTester.cs";
        private const string VoxelDeformationSmokeTesterPath = "Assets/_Project/Scripts/VoxelDeformationSmokeTester.cs";
        private const string BiomeTransitionSmokeTesterPath = "Assets/_Project/Scripts/World/BiomeTransitionSmokeTester.cs";
        private const string BiomeBoundarySdfSmokeTesterPath = "Assets/_Project/Scripts/Dev/BiomeBoundarySdfSmokeTester.cs";
        private const string OmegaAutonomySmokeTesterPath = "Assets/_Project/Scripts/Dev/OmegaAutonomySmokeTester.cs";
        private const string SpaceEngine098TerrainSmokeTesterPath = "Assets/_Project/Scripts/Dev/SpaceEngine098/SpaceEngine098TerrainSmokeTester.cs";
        private const string FaunaRuntimeSmokeTesterPath = "Assets/_Project/Scripts/FaunaRuntimeSmokeTester.cs";
        private const string HectonSandboxAbyssalShelfSmokeTesterPath = "Assets/_Project/Scripts/World/HectonSandboxAbyssalShelfSmokeTester.cs";
        private const string WorldPlanetaryCanvasSmokeTesterPath = "Assets/_Project/Scripts/World/PlanetaryCanvasSmokeTester.cs";
        private const string SavePersistenceOmegaSmokeTesterPath = "Assets/_Project/Scripts/SavePersistenceOmegaSmokeTester.cs";
        private const string SaveSystemRuntimeSmokeTesterPath = "Assets/_Project/Scripts/SaveSystemRuntimeSmokeTester.cs";
        private const string SaveRecoverySmokeTesterPath = "Assets/_Project/Scripts/SaveRecoverySmokeTester.cs";
        private const string SaveSidecarStoragePath = "Assets/_Project/Scripts/SaveSidecarStorage.cs";
        private const string SaveBinaryStoragePath = "Assets/_Project/Scripts/SaveBinaryStorage.cs";
        private const string EntityDeltaCompressionArchitecturePath = "Assets/_Project/Scripts/SaveSystem/EntityDeltaCompressionArchitecture.cs";
        private const string WalIntegrityFuzzerCorePath = "Assets/_Project/Scripts/SaveSystem/WalIntegrityFuzzerCore.cs";
        private const string WalIntegrityFuzzerCoreShinobu357Path = "Assets/_Project/Scripts/SaveSystem/WalIntegrityFuzzerCore_SHINOBU357.cs";
        private const string Arm64MemoryAlignmentXRayWindowPath = "Assets/_Project/Scripts/Editor/Arm64MemoryAlignmentXRayWindow.cs";
        private const string BaseModuleCatalogEditorToolsPath = "Assets/_Project/Scripts/Editor/BaseModuleCatalogEditorTools.cs";
        private const string EconomyRecipeTunerWindowPath = "Assets/_Project/Scripts/Editor/EconomyRecipeTunerWindow.cs";
        private const string Shinobu132CablePhysicsTunerWindowPath = "Assets/_Project/Scripts/Editor/Shinobu132CablePhysicsTunerWindow.cs";
        private const string HectonSpatialHashEditorSelfTestsPath = "Assets/_Project/Scripts/Editor/HectonSpatialHashEditorSelfTests.cs";
        private const string GeographySanityPipelinePath = "Assets/_Project/Scripts/Editor/GeographySanity/GeographySanityPipeline.cs";
        private const string GeographySanityProfileCsvPath = "Assets/_Project/Scripts/Editor/GeographySanity/GeographySanityProfileCsv.cs";
        private const string GeologyForgeGeneratorPath = "Assets/_Project/Scripts/Editor/GeologyForge/GeologyForgeGenerator.cs";
        private const string GeologyForgeNativeMemoryPath = "Assets/_Project/Scripts/Editor/GeologyForge/GeologyForgeNativeMemory.cs";
        private const string GeologyForgeWindowPath = "Assets/_Project/Scripts/Editor/GeologyForge/GeologyForgeWindow.cs";
        private const string GeologyProfileCsvPath = "Assets/_Project/Scripts/Editor/GeologyForge/GeologyProfileCsv.cs";
        private const string TopographyForgeGeneratorPath = "Assets/_Project/Scripts/Editor/GeologyForge/TopographyForgeGenerator.cs";
        private const string TopographyForgeCsvPath = "Assets/_Project/Scripts/Editor/GeologyForge/TopographyForgeCsv.cs";
        private const string TopographyForgeWindowPath = "Assets/_Project/Scripts/Editor/GeologyForge/TopographyForgeWindow.cs";
        private const string OfflineGeometryBakerPath = "Assets/_Project/Scripts/Editor/OfflineGeometryBaker/Shinobu213/OfflineGeometryBaker.cs";
        private const string OfflineGeometryBakeBlackBoxPath = "Assets/_Project/Scripts/Editor/OfflineGeometryBaker/Shinobu213/OfflineGeometryBakeBlackBox.cs";
        private const string OfflineOptimizationProfileCsvPath = "Assets/_Project/Scripts/Editor/OfflineGeometryBaker/Shinobu213/OfflineOptimizationProfileCsv.cs";
        private const string InteriorClutterForgePath = "Assets/_Project/Scripts/Editor/OfflineGeometryBaker/InteriorClutterForge.cs";
        private const string InteriorClutterForgeSupportPath = "Assets/_Project/Scripts/Editor/OfflineGeometryBaker/InteriorClutterForgeSupport.cs";
        private const string InteriorClutterForgeJobsPath = "Assets/_Project/Scripts/Editor/OfflineGeometryBaker/InteriorClutterForgeJobs.cs";
        private const string BioForgeGeneratorPath = "Assets/_Project/Scripts/Editor/ProceduralGen/BioForgeGenerator.cs";
        private const string ErosionTestHarnessPath = "Assets/_Project/Scripts/Editor/ErosionTestHarness.cs";
        private const string HydraulicErosionSmokeTesterPath = "Assets/_Project/Scripts/Editor/HydraulicErosionSmokeTester.cs";
        private const string PlanetaryCanvasSmokeTesterPath = "Assets/_Project/Scripts/Editor/PlanetaryCanvasSmokeTester.cs";
        private const string HydraulicErosionForgeBakerPath = "Assets/_Project/Scripts/Editor/HydraulicErosionForge/Shinobu242/HydraulicErosionForgeBaker.cs";
        private const string HydraulicErosionWeatheringCsvPath = "Assets/_Project/Scripts/Editor/HydraulicErosionForge/Shinobu242/HydraulicErosionWeatheringCsv.cs";
        private const string HlodImpostorBakerPath = "Assets/_Project/Scripts/Editor/HectonOctahedralImpostorBaker.cs";
        private const string HlodImpostorForgeWindowPath = "Assets/_Project/Scripts/Editor/HlodImpostorForgeWindow.cs";
        private const string AITextureNativeMemoryPath = "Assets/_Project/Scripts/Editor/AITextureControlMapBaker/Shinobu269/AITextureNativeMemory.cs";
        private const string AITextureControlMapBakerPath = "Assets/_Project/Scripts/Editor/AITextureControlMapBaker/Shinobu269/AITextureControlMapBaker.cs";
        private const string AITextureBakeBlackBoxPath = "Assets/_Project/Scripts/Editor/AITextureControlMapBaker/Shinobu269/AITextureBakeBlackBox.cs";
        private const string AITextureMockMeshJobsPath = "Assets/_Project/Scripts/Editor/AITextureControlMapBaker/Shinobu269/AITextureMockMeshJobs.cs";
        private const string AITextureProfileCsvPath = "Assets/_Project/Scripts/Editor/AITextureControlMapBaker/Shinobu269/AITextureProfileCsv.cs";
        private const string WorldProceduralProxySceneBuilderPath = "Assets/_Project/Scripts/Editor/WorldProceduralProxySceneBuilder.cs";
        private const string TexturePackerAsmdefPath = "Assets/_Project/Scripts/Editor/TextureChannelPacker/Hecton8.Rendering.TexturePacker.Editor.asmdef";
        private const string TexturePackerWindowPath = "Assets/_Project/Scripts/Editor/TextureChannelPacker/TextureChannelPackerWindow.cs";
        private const string TexturePackerPath = "Assets/_Project/Scripts/Editor/TextureChannelPacker/HectonArmTextureChannelPacker.cs";
        private const string HabitatDamageBakePipelinePath = "Assets/_Project/Scripts/Habitat/Deformation/Editor/DamageBake/HabitatDamageBakePipeline.cs";

        [Test]
        public void NativeAllocationContractsAndSentinelStayInDirectlyReferencedAssemblies()
        {
            string contracts = ReadProjectFile("Assets/_Project/Scripts/Core/Contracts/NativeAllocationContracts.cs");
            string contractsAsmdef = ReadProjectFile("Assets/_Project/Scripts/Core/Contracts/Hecton8.Core.Contracts.asmdef");
            string coreAsmdef = ReadProjectFile("Assets/_Project/Scripts/Hecton8.Core.asmdef");
            string coreMemoryAsmdef = ReadProjectFile("Assets/_Project/Scripts/Core/Memory/Hecton8.Core.Memory.asmdef");

            StringAssert.Contains("\"name\": \"Hecton8.Core.Contracts\"", contractsAsmdef);
            StringAssert.Contains("\"Hecton8.Core.Contracts\"", coreAsmdef);
            StringAssert.Contains("\"Hecton8.Core.Memory\"", coreAsmdef);
            StringAssert.Contains("\"Hecton8.Core.Contracts\"", coreMemoryAsmdef);
            StringAssert.Contains("namespace Hecton8.Core", contracts);
            StringAssert.Contains("public enum NativeAllocationLifetime : byte", contracts);
            StringAssert.Contains("public struct NativeAllocationSnapshotSource", contracts);

            AssertAsmdefReferencesCoreContracts("Assets/_Project/Scripts/Dev/SpaceEngine098/Hecton8.Dev.SpaceEngine098.asmdef");
            AssertAsmdefReferencesCoreContracts("Assets/_Project/Scripts/Editor/GeographySanity/Hecton8.World.GeographySanity.Editor.asmdef");
            AssertAsmdefReferencesCoreContracts("Assets/_Project/Scripts/Editor/GeologyForge/Hecton8.World.OfflineGeology.Editor.asmdef");
            AssertAsmdefReferencesCoreContracts("Assets/_Project/Scripts/Editor/HydraulicErosionForge/Shinobu242/Hecton8.World.HydraulicErosionForge.Editor.asmdef");
            AssertAsmdefReferencesCoreContracts("Assets/_Project/Scripts/Editor/OfflineGeometryBaker/Hecton8.HabitatInteriorClutterForge.Editor.asmdef");
            AssertAsmdefReferencesCoreContractsAndMemory("Assets/_Project/Scripts/Editor/OfflineGeometryBaker/Shinobu213/Hecton8.World.OfflineGeometry.Editor.asmdef");
            AssertAsmdefReferencesCoreContracts("Assets/_Project/Scripts/Editor/ProceduralGen/Hecton8.Editor.ProceduralGen.asmdef");
            AssertAsmdefReferencesCoreContracts(TexturePackerAsmdefPath);
            AssertAsmdefReferencesCoreContracts("Assets/_Project/Scripts/Habitat/Deformation/Editor/DamageBake/Hecton8.Habitat.Deformation.DamageBake.Editor.asmdef");
            AssertAsmdefReferencesCoreContracts("Assets/_Project/Scripts/Plugins/Hecton8.Plugins.asmdef");
            AssertAsmdefReferencesCoreContracts("Assets/_Project/Scripts/World/BiotaDensityMapBaker/Editor/Hecton8.World.BiotaDensityMapBaker.Editor.asmdef");
            AssertAsmdefReferencesCoreContractsAndMemory("Assets/_Project/Scripts/World/OfflineHadalTrenchBaker/Editor/Hecton8.World.OfflineHadalTrenchBaker.Editor.asmdef");
            AssertAsmdefReferencesCoreContractsAndMemory("Assets/_Project/Tests/Editor/SaveSystem/Hecton8.SaveSystem.EditModeTests.asmdef");
        }

        [Test]
        public void LSystemGenomeLabTracksPreviewAndTempJobNativeArrays()
        {
            string source = ReadProjectFile(LSystemGenomeLabPath);

            StringAssert.Contains("private static NativeArray<T> AllocateTrackedTempJobArray<T>", source);
            StringAssert.Contains("DisposeTrackedNativeArray<T>", source);
            StringAssert.Contains("NativeMemorySentinel.RegisterNativeArray(array, NativeMemoryOwner, label, NativeAllocationLifetime.TempJob)", source);
            StringAssert.Contains("NativeMemorySentinel.UnregisterPointer(trackedPointer)", source);
            StringAssert.Contains("DisposePreviewArray(ref _previewExpandedSymbols, ref _previewExpandedSymbolsSentinelId)", source);
            StringAssert.Contains("NativeMemorySentinel.Unregister(sentinelId)", source);
            StringAssert.Contains("MockGenomesLabel", source);
            StringAssert.Contains("PreviewSeedLabel", source);
            StringAssert.Contains("PreviewStatsLabel", source);
            StringAssert.Contains("PreviewBlackBoxLabel", source);
            StringAssert.Contains("PreviewCursorLabel", source);

            Assert.AreEqual(1, CountOccurrences(source, "Allocator.TempJob"), "TempJob allocation must stay inside AllocateTrackedTempJobArray.");
            Assert.AreEqual(6, CountOccurrences(source, "AllocateTrackedTempJobArray<"), "Mock + four preview scratch arrays plus the helper definition are expected.");
            Assert.AreEqual(1, CountOccurrences(source, "NativeAllocationLifetime.TempJob"), "TempJob sentinel lifetime must stay centralized.");
            Assert.AreEqual(5, CountOccurrences(source, "NativeAllocationLifetime.Session"), "The persistent preview workspace has five tracked session arrays.");

            StringAssert.DoesNotContain("using NativeArray<FloraGenomeDTO> mockGenomes = new NativeArray<FloraGenomeDTO>", source);
            StringAssert.DoesNotContain("new NativeArray<FloraPlantSeedDTO>(1, Allocator.TempJob", source);
            StringAssert.DoesNotContain("new NativeArray<FloraGenomeJobStats>(1, Allocator.TempJob", source);
            StringAssert.DoesNotContain("new NativeArray<FloraGenomeBlackBoxEntry>(FloraGenomeLSystemConstants.BlackBoxFrameCount, Allocator.TempJob", source);
            StringAssert.DoesNotContain("new NativeArray<int>(1, Allocator.TempJob", source);
        }

        [Test]
        public void AnomalySmokeTesterTracksTempJobNativeArraysAtomically()
        {
            string source = ReadProjectFile(AnomalySmokeTesterPath);

            StringAssert.Contains("private static NativeArray<T> AllocateTrackedTempJobArray<T>", source);
            StringAssert.Contains("NativeMemorySentinel.RegisterNativeArray(array, NativeMemoryOwner, label, NativeAllocationLifetime.TempJob)", source);
            StringAssert.Contains("NativeMemorySentinel.UnregisterPointer(trackedPointer)", source);
            StringAssert.Contains("AllocateTrackedTempJobArray<AnomalyBasinRecord>", source);
            StringAssert.Contains("AllocateTrackedTempJobArray<byte>", source);
            StringAssert.Contains("AllocateTrackedTempJobArray<float>(PillarTerrainCount", source);
            StringAssert.Contains("DisposeTracked(ref acceptedCells)", source);
            StringAssert.Contains("DisposeTracked(ref sdf)", source);
            Assert.AreEqual(1, CountOccurrences(source, "new NativeArray<T>("), "Anomaly smoke NativeArray construction must stay centralized in AllocateTrackedTempJobArray.");
            Assert.AreEqual(1, CountOccurrences(source, "RegisterNativeArray(array, NativeMemoryOwner, label, NativeAllocationLifetime.TempJob)"), "Anomaly smoke registrations must stay centralized in AllocateTrackedTempJobArray.");

            StringAssert.DoesNotContain("RegisterTempJobBuffers", source);
            StringAssert.DoesNotContain("new NativeArray<float>", source);
            StringAssert.DoesNotContain("new NativeArray<byte>", source);
            StringAssert.DoesNotContain("new NativeArray<int>", source);
            StringAssert.DoesNotContain("new NativeArray<AnomalyBasinRecord>", source);
            StringAssert.DoesNotContain("terrainHeights = new NativeArray", source);
            StringAssert.DoesNotContain("sdf = new NativeArray", source);
        }

        [Test]
        public void AnomalyTestHarnessTracksTempJobCollectionsAtomically()
        {
            string source = ReadProjectFile(AnomalyTestHarnessPath);

            StringAssert.Contains("private static NativeArray<T> AllocateTrackedTempJobArray<T>", source);
            StringAssert.Contains("private static NativeQueue<T> AllocateTrackedTempJobQueue<T>", source);
            StringAssert.Contains("NativeMemorySentinel.RegisterNativeArray(array, NativeMemoryOwner, label, NativeAllocationLifetime.TempJob)", source);
            StringAssert.Contains("sentinelId = NativeMemorySentinel.RegisterNativeQueueInstance(queue, capacity, NativeMemoryOwner, label, NativeAllocationLifetime.TempJob)", source);
            StringAssert.Contains("AllocateTrackedTempJobArray<AnomalyBasinRecord>", source);
            StringAssert.Contains("AllocateTrackedTempJobArray<AnomalyBrinePoolBounds>", source);
            StringAssert.Contains("AllocateTrackedTempJobQueue<AnomalyBasinFloodFillState>", source);
            StringAssert.Contains("DisposeTrackedQueue(ref pendingFloodStates, ref pendingFloodStatesSentinelId)", source);
            StringAssert.Contains("DisposeTracked(ref fissureInfluence)", source);
            Assert.AreEqual(1, CountOccurrences(source, "new NativeArray<T>("), "Anomaly harness NativeArray construction must stay centralized in AllocateTrackedTempJobArray.");
            Assert.AreEqual(1, CountOccurrences(source, "new NativeQueue<T>("), "Anomaly harness NativeQueue construction must stay centralized in AllocateTrackedTempJobQueue.");
            Assert.AreEqual(1, CountOccurrences(source, "RegisterNativeArray(array, NativeMemoryOwner, label, NativeAllocationLifetime.TempJob)"), "Anomaly harness array registrations must stay centralized in AllocateTrackedTempJobArray.");
            Assert.AreEqual(1, CountOccurrences(source, "RegisterNativeQueueInstance(queue, capacity, NativeMemoryOwner, label, NativeAllocationLifetime.TempJob)"), "Anomaly harness queue registrations must stay centralized in AllocateTrackedTempJobQueue.");

            StringAssert.DoesNotContain("RegisterTempJobBuffers", source);
            StringAssert.DoesNotContain("RegisterTempJobArray", source);
            StringAssert.DoesNotContain("RegisterTempJobQueue", source);
            StringAssert.DoesNotContain("new NativeArray<float>", source);
            StringAssert.DoesNotContain("new NativeArray<byte>", source);
            StringAssert.DoesNotContain("new NativeArray<int>", source);
            StringAssert.DoesNotContain("new NativeArray<uint>", source);
            StringAssert.DoesNotContain("new NativeArray<AnomalyBasinRecord>", source);
            StringAssert.DoesNotContain("new NativeArray<AnomalyBrinePoolBounds>", source);
            StringAssert.DoesNotContain("new NativeArray<AnomalyFeatureRecord>", source);
            StringAssert.DoesNotContain("new NativeQueue<AnomalyBasinFloodFillState>", source);
            StringAssert.DoesNotContain("NativeMemorySentinel.RegisterNativeArray(heightmap", source);
            StringAssert.DoesNotContain("NativeMemorySentinel.RegisterNativeQueue(pendingFloodStates", source);
            StringAssert.DoesNotContain("NativeMemorySentinel.UnregisterNativeQueue", source);
        }

        [Test]
        public void AutomationSmokeTestersTrackRouteScratchAtomically()
        {
            string smoke = ReadProjectFile(AutomationSmokeTesterPath);
            string omega = ReadProjectFile(AutomationOmegaSmokeTesterPath);

            StringAssert.Contains("private static NativeArray<T> AllocateTrackedTempArray<T>", smoke);
            StringAssert.Contains("NativeMemorySentinel.RegisterNativeArray(array, NativeMemoryOwner, label, NativeAllocationLifetime.Temp)", smoke);
            StringAssert.Contains("AllocateTrackedTempArray<int>(4, nameof(edgeOffsets)", smoke);
            StringAssert.Contains("AllocateTrackedTempArray<byte>(3, nameof(visited)", smoke);
            StringAssert.Contains("DisposeTempArray(ref result)", smoke);
            Assert.AreEqual(1, CountOccurrences(smoke, "new NativeArray<T>("), "Automation smoke Temp allocations must stay centralized in AllocateTrackedTempArray.");
            Assert.AreEqual(1, CountOccurrences(smoke, "RegisterNativeArray(array, NativeMemoryOwner, label, NativeAllocationLifetime.Temp)"), "Automation smoke Temp registrations must stay centralized in AllocateTrackedTempArray.");

            StringAssert.DoesNotContain("RegisterTempArray", smoke);
            StringAssert.DoesNotContain("new NativeArray<int>", smoke);
            StringAssert.DoesNotContain("new NativeArray<byte>", smoke);
            StringAssert.DoesNotContain("NativeMemorySentinel.RegisterNativeArray(edgeOffsets", smoke);
            StringAssert.DoesNotContain("result.Dispose()", smoke);

            StringAssert.Contains("private static NativeArray<T> AllocateTrackedTempJobArray<T>", omega);
            StringAssert.Contains("NativeMemorySentinel.RegisterNativeArray(array, NativeMemoryOwner, label, NativeAllocationLifetime.TempJob)", omega);
            StringAssert.Contains("AllocateTrackedTempJobArray<int>(nodeCount + 1, nameof(edgeOffsets)", omega);
            StringAssert.Contains("AllocateTrackedTempJobArray<byte>(nodeCount, nameof(visited)", omega);
            StringAssert.Contains("DisposeTempJobArray(ref routeResult)", omega);
            Assert.AreEqual(1, CountOccurrences(omega, "new NativeArray<T>("), "Automation omega smoke TempJob allocations must stay centralized in AllocateTrackedTempJobArray.");
            Assert.AreEqual(1, CountOccurrences(omega, "RegisterNativeArray(array, NativeMemoryOwner, label, NativeAllocationLifetime.TempJob)"), "Automation omega smoke TempJob registrations must stay centralized in AllocateTrackedTempJobArray.");

            StringAssert.DoesNotContain("RegisterTempJobArray", omega);
            StringAssert.DoesNotContain("new NativeArray<int>", omega);
            StringAssert.DoesNotContain("new NativeArray<byte>", omega);
            StringAssert.DoesNotContain("NativeMemorySentinel.RegisterNativeArray(edgeOffsets", omega);
            StringAssert.DoesNotContain("routeResult.Dispose()", omega);
        }

        [Test]
        public void ThermalMeltSmokeTesterTracksTempJobScratchAtomically()
        {
            string source = ReadProjectFile(ThermalMeltSmokeTesterPath);

            StringAssert.Contains("private static NativeArray<T> AllocateTrackedTempJobArray<T>", source);
            StringAssert.Contains("DisposeTrackedTempJobArray<T>", source);
            StringAssert.Contains("NativeMemorySentinel.RegisterNativeArray(array, NativeMemoryOwner, label, NativeAllocationLifetime.TempJob)", source);
            StringAssert.Contains("NativeMemorySentinel.UnregisterPointer(trackedPointer)", source);
            StringAssert.Contains("AllocateTrackedTempJobArray<float3>(2, nameof(positions)", source);
            StringAssert.Contains("AllocateTrackedTempJobArray<VoxelModifiedCellEntry>", source);
            StringAssert.Contains("AllocateTrackedTempJobArray<VoxelSdfRaycastHit>", source);
            StringAssert.Contains("AllocateTrackedTempJobArray<byte>(16, nameof(sdf)", source);
            StringAssert.Contains("DisposeTrackedTempJobArray(ref passability)", source);
            Assert.AreEqual(1, CountOccurrences(source, "new NativeArray<T>("), "Thermal melt smoke TempJob allocations must stay centralized in AllocateTrackedTempJobArray.");
            Assert.AreEqual(1, CountOccurrences(source, "RegisterNativeArray(array, NativeMemoryOwner, label, NativeAllocationLifetime.TempJob)"), "Thermal melt smoke registrations must stay centralized in AllocateTrackedTempJobArray.");

            StringAssert.DoesNotContain("new NativeArray<float3>", source);
            StringAssert.DoesNotContain("new NativeArray<float>", source);
            StringAssert.DoesNotContain("new NativeArray<byte>", source);
            StringAssert.DoesNotContain("new NativeArray<int>", source);
            StringAssert.DoesNotContain("new NativeArray<VoxelModifiedCellEntry>", source);
            StringAssert.DoesNotContain("new NativeArray<VoxelSdfRaycastHit>", source);
            StringAssert.DoesNotContain("positions.Dispose()", source);
            StringAssert.DoesNotContain("density.Dispose()", source);
            StringAssert.DoesNotContain("if (sdf.IsCreated)", source);
        }

        [Test]
        public void VoxelDeformationSmokeTesterTracksTempJobScratchAtomically()
        {
            string source = ReadProjectFile(VoxelDeformationSmokeTesterPath);

            StringAssert.Contains("private static NativeArray<T> AllocateTrackedTempJobArray<T>", source);
            StringAssert.Contains("DisposeTrackedTempJobArray<T>", source);
            StringAssert.Contains("NativeMemorySentinel.RegisterNativeArray(array, NativeMemoryOwner, label, NativeAllocationLifetime.TempJob)", source);
            StringAssert.Contains("NativeMemorySentinel.UnregisterPointer(trackedPointer)", source);
            StringAssert.Contains("AllocateTrackedTempJobArray<byte>(8, nameof(passability)", source);
            StringAssert.Contains("AllocateTrackedTempJobArray<ushort>(8, nameof(distance)", source);
            StringAssert.Contains("VoxelDynamicNavGridRuntime.ResolvePureVoidBlockCount(passability.Length)", source);
            StringAssert.Contains("AllocateTrackedTempJobArray<sbyte>(27, nameof(density)", source);
            StringAssert.Contains("AllocateTrackedTempJobArray<float3>(1, nameof(positions)", source);
            StringAssert.Contains("DisposeTrackedTempJobArray(ref ambientOcclusion)", source);
            Assert.AreEqual(1, CountOccurrences(source, "new NativeArray<T>("), "Voxel deformation smoke TempJob allocations must stay centralized in AllocateTrackedTempJobArray.");
            Assert.AreEqual(1, CountOccurrences(source, "RegisterNativeArray(array, NativeMemoryOwner, label, NativeAllocationLifetime.TempJob)"), "Voxel deformation smoke registrations must stay centralized in AllocateTrackedTempJobArray.");

            StringAssert.DoesNotContain("new NativeArray<byte>", source);
            StringAssert.DoesNotContain("new NativeArray<ushort>", source);
            StringAssert.DoesNotContain("new NativeArray<int>", source);
            StringAssert.DoesNotContain("new NativeArray<float>", source);
            StringAssert.DoesNotContain("new NativeArray<sbyte>", source);
            StringAssert.DoesNotContain("new NativeArray<float3>", source);
            StringAssert.DoesNotContain("if (passability.IsCreated)", source);
            StringAssert.DoesNotContain("passability.Dispose()", source);
            StringAssert.DoesNotContain("ambientOcclusion.Dispose()", source);
        }

        [Test]
        public void BiomeTransitionSmokeTesterTracksTempJobScratchAtomically()
        {
            string source = ReadProjectFile(BiomeTransitionSmokeTesterPath);

            StringAssert.Contains("private static NativeArray<T> AllocateTrackedTempJobArray<T>", source);
            StringAssert.Contains("DisposeTrackedTempJobArray<T>", source);
            StringAssert.Contains("NativeMemorySentinel.RegisterNativeArray(array, NativeMemoryOwner, label, NativeAllocationLifetime.TempJob)", source);
            StringAssert.Contains("NativeMemorySentinel.UnregisterPointer(trackedPointer)", source);
            StringAssert.Contains("AllocateTrackedTempJobArray<BiomeTransitionSample>", source);
            StringAssert.Contains("AllocateTrackedTempJobArray<BiomeTransitionFogSource>", source);
            StringAssert.Contains("AllocateTrackedTempJobArray<AbsoluteUniversePositionBlit128>", source);
            StringAssert.Contains("AllocateTrackedTempJobArray<BiomeTransitionFogResult>", source);
            StringAssert.Contains("DisposeTrackedTempJobArray(ref results)", source);
            Assert.AreEqual(1, CountOccurrences(source, "new NativeArray<T>("), "Biome transition smoke TempJob allocations must stay centralized in AllocateTrackedTempJobArray.");
            Assert.AreEqual(1, CountOccurrences(source, "RegisterNativeArray(array, NativeMemoryOwner, label, NativeAllocationLifetime.TempJob)"), "Biome transition smoke registrations must stay centralized in AllocateTrackedTempJobArray.");

            StringAssert.DoesNotContain("new NativeArray<BiomeTransitionSample>", source);
            StringAssert.DoesNotContain("new NativeArray<BiomeTransitionFogSource>", source);
            StringAssert.DoesNotContain("new NativeArray<AbsoluteUniversePositionBlit128>", source);
            StringAssert.DoesNotContain("new NativeArray<BiomeTransitionFogResult>", source);
            StringAssert.DoesNotContain("samples.Dispose()", source);
            StringAssert.DoesNotContain("results.Dispose()", source);
            StringAssert.DoesNotContain("if (samples.IsCreated)", source);
        }

        [Test]
        public void BiomeBoundarySdfSmokeTesterTracksTempJobScratchAtomically()
        {
            string source = ReadProjectFile(BiomeBoundarySdfSmokeTesterPath);

            StringAssert.Contains("private static NativeArray<T> AllocateTrackedTempJobArray<T>", source);
            StringAssert.Contains("DisposeTracked<T>", source);
            StringAssert.Contains("NativeMemorySentinel.RegisterNativeArray(array, NativeMemoryOwner, label, NativeMemoryLifetime)", source);
            StringAssert.Contains("NativeMemorySentinel.UnregisterPointer(trackedPointer)", source);
            StringAssert.Contains("AllocateTrackedTempJobArray<byte>(CellCount, nameof(map)", source);
            StringAssert.Contains("AllocateTrackedTempJobArray<uint>(CellCount, nameof(hashes)", source);
            StringAssert.Contains("AllocateTrackedTempJobArray<BiomeBoundarySdfResult>(1, nameof(result)", source);
            StringAssert.Contains("DisposeTracked(ref result)", source);
            Assert.AreEqual(1, CountOccurrences(source, "new NativeArray<T>("), "Biome boundary SDF smoke allocations must stay centralized in AllocateTrackedTempJobArray.");
            Assert.AreEqual(1, CountOccurrences(source, "RegisterNativeArray(array, NativeMemoryOwner, label, NativeMemoryLifetime)"), "Biome boundary SDF smoke registrations must stay centralized in AllocateTrackedTempJobArray.");

            StringAssert.DoesNotContain("private static void Register<T>", source);
            StringAssert.DoesNotContain("new NativeArray<byte>", source);
            StringAssert.DoesNotContain("new NativeArray<uint>", source);
            StringAssert.DoesNotContain("new NativeArray<BiomeBoundarySdfResult>", source);
            StringAssert.DoesNotContain("Register(map", source);
            StringAssert.DoesNotContain("map.Dispose()", source);
        }

        [Test]
        public void OmegaAutonomySmokeTesterTracksTempJobScratchAtomically()
        {
            string source = ReadProjectFile(OmegaAutonomySmokeTesterPath);

            StringAssert.Contains("private static NativeArray<T> AllocateTrackedNativeArray<T>", source);
            StringAssert.Contains("DisposeTrackedNativeArray<T>", source);
            StringAssert.Contains("NativeMemorySentinel.RegisterNativeArray(array, NativeMemoryOwner, label, NativeMemoryLifetime)", source);
            StringAssert.Contains("NativeMemorySentinel.UnregisterPointer(trackedPointer)", source);
            StringAssert.Contains("AllocateTrackedNativeArray<int>(5, nameof(edgeOffsets)", source);
            StringAssert.Contains("AllocateTrackedNativeArray<byte>(4, nameof(storageCapacityByNode)", source);
            StringAssert.Contains("AllocateTrackedNativeArray<int>(8, nameof(intValues)", source);
            StringAssert.Contains("AllocateTrackedNativeArray<byte>(8, nameof(byteValues)", source);
            StringAssert.Contains("DisposeTrackedNativeArray(ref checksumResult)", source);
            Assert.AreEqual(1, CountOccurrences(source, "new NativeArray<T>("), "Omega autonomy smoke allocations must stay centralized in AllocateTrackedNativeArray.");
            Assert.AreEqual(1, CountOccurrences(source, "RegisterNativeArray(array, NativeMemoryOwner, label, NativeMemoryLifetime)"), "Omega autonomy smoke registrations must stay centralized in AllocateTrackedNativeArray.");

            StringAssert.DoesNotContain("private static void RegisterNativeArray<T>", source);
            StringAssert.DoesNotContain("new NativeArray<int>", source);
            StringAssert.DoesNotContain("new NativeArray<byte>", source);
            StringAssert.DoesNotContain("RegisterNativeArray(edgeOffsets", source);
            StringAssert.DoesNotContain("RegisterNativeArray(intValues", source);
            StringAssert.DoesNotContain("checksumResult.Dispose()", source);
        }

        [Test]
        public void SpaceEngine098TerrainSmokeTesterTracksTempJobScratchAtomically()
        {
            string source = ReadProjectFile(SpaceEngine098TerrainSmokeTesterPath);

            StringAssert.Contains("private static NativeArray<T> AllocateTrackedTempJobArray<T>", source);
            StringAssert.Contains("DisposeTracked<T>", source);
            StringAssert.Contains("NativeMemorySentinel.RegisterNativeArray(array, NativeMemoryOwner, label, NativeMemoryLifetime)", source);
            StringAssert.Contains("NativeMemorySentinel.UnregisterPointer(trackedPointer)", source);
            StringAssert.Contains("AllocateTrackedTempJobArray<float>(sampleCount, nameof(input)", source);
            StringAssert.Contains("AllocateTrackedTempJobArray<float3>(4, nameof(craterCenters)", source);
            StringAssert.Contains("AllocateTrackedTempJobArray<SpaceEngine098PipelineMetricSample>", source);
            StringAssert.Contains("DisposeTracked(ref metrics)", source);
            Assert.AreEqual(1, CountOccurrences(source, "new NativeArray<T>("), "SpaceEngine098 terrain smoke allocations must stay centralized in AllocateTrackedTempJobArray.");
            Assert.AreEqual(1, CountOccurrences(source, "RegisterNativeArray(array, NativeMemoryOwner, label, NativeMemoryLifetime)"), "SpaceEngine098 terrain smoke registrations must stay centralized in AllocateTrackedTempJobArray.");

            StringAssert.DoesNotContain("private static void Register<T>", source);
            StringAssert.DoesNotContain("new NativeArray<float>", source);
            StringAssert.DoesNotContain("new NativeArray<float3>", source);
            StringAssert.DoesNotContain("new NativeArray<SpaceEngine098PipelineMetricSample>", source);
            StringAssert.DoesNotContain("Register(input", source);
            StringAssert.DoesNotContain("metrics.Dispose()", source);
        }

        [Test]
        public void FaunaRuntimeSmokeTesterTracksTempJobScratchAtomically()
        {
            string source = ReadProjectFile(FaunaRuntimeSmokeTesterPath);

            StringAssert.Contains("private static NativeArray<T> AllocateTrackedNativeArray<T>", source);
            StringAssert.Contains("DisposeTrackedNativeArray<T>", source);
            StringAssert.Contains("NativeMemorySentinel.RegisterNativeArray(array, NativeMemoryOwner, label, NativeMemoryLifetime)", source);
            StringAssert.Contains("NativeMemorySentinel.UnregisterPointer(trackedPointer)", source);
            StringAssert.Contains("AllocateTrackedNativeArray<AbsoluteUniversePositionBlit128>", source);
            StringAssert.Contains("AllocateTrackedNativeArray<double>", source);
            StringAssert.Contains("AllocateTrackedNativeArray<FaunaParasiteAttachInput>", source);
            StringAssert.Contains("AllocateTrackedNativeArray<FaunaParasiteAttachResult>", source);
            StringAssert.Contains("DisposeTrackedNativeArray(ref distanceErrors)", source);
            StringAssert.Contains("DisposeTrackedNativeArray(ref results)", source);
            Assert.AreEqual(1, CountOccurrences(source, "new NativeArray<T>("), "Fauna runtime smoke allocations must stay centralized in AllocateTrackedNativeArray.");
            Assert.AreEqual(1, CountOccurrences(source, "RegisterNativeArray(array, NativeMemoryOwner, label, NativeMemoryLifetime)"), "Fauna runtime smoke registrations must stay centralized in AllocateTrackedNativeArray.");

            StringAssert.DoesNotContain("private static void RegisterNativeArray<T>", source);
            StringAssert.DoesNotContain("new NativeArray<AbsoluteUniversePositionBlit128>", source);
            StringAssert.DoesNotContain("new NativeArray<double>", source);
            StringAssert.DoesNotContain("new NativeArray<FaunaParasiteAttachInput>", source);
            StringAssert.DoesNotContain("new NativeArray<FaunaParasiteAttachResult>", source);
            StringAssert.DoesNotContain("RegisterNativeArray(predatorAups", source);
            StringAssert.DoesNotContain("distanceErrors.Dispose()", source);
            StringAssert.DoesNotContain("results.Dispose()", source);
        }

        [Test]
        public void HectonSandboxAbyssalShelfSmokeTesterTracksTempJobScratchAtomically()
        {
            string source = ReadProjectFile(HectonSandboxAbyssalShelfSmokeTesterPath);

            StringAssert.Contains("private static NativeArray<T> AllocateTrackedTempJobArray<T>", source);
            StringAssert.Contains("DisposeTrackedTempJobArray<T>", source);
            StringAssert.Contains("NativeMemorySentinel.RegisterNativeArray(", source);
            StringAssert.Contains("NativeMemorySentinel.UnregisterPointer(trackedPointer)", source);
            StringAssert.Contains("AllocateTrackedTempJobArray<AbsoluteUniversePosition>", source);
            StringAssert.Contains("AllocateTrackedTempJobArray<HectonSandboxAbyssalShelfAuditSample>", source);
            StringAssert.Contains("AllocateTrackedTempJobArray<HectonSandboxAbyssalShelfSampleReduction>", source);
            StringAssert.Contains("AllocateTrackedTempJobArray<HectonSandboxAbyssalShelfSmokeSummary>", source);
            StringAssert.Contains("DisposeTrackedTempJobArray(ref summary)", source);
            Assert.AreEqual(1, CountOccurrences(source, "new NativeArray<T>("), "Abyssal shelf smoke allocations must stay centralized in AllocateTrackedTempJobArray.");

            StringAssert.DoesNotContain("RegisterTempJobArray", source);
            StringAssert.DoesNotContain("new NativeArray<AbsoluteUniversePosition>", source);
            StringAssert.DoesNotContain("new NativeArray<HectonSandboxAbyssalShelfAuditSample>", source);
            StringAssert.DoesNotContain("new NativeArray<HectonSandboxAbyssalShelfSampleReduction>", source);
            StringAssert.DoesNotContain("new NativeArray<HectonSandboxAbyssalShelfSmokeSummary>", source);
            StringAssert.DoesNotContain("NativeMemorySentinel.UnregisterNativeArray(positions)", source);
            StringAssert.DoesNotContain("positions.Dispose()", source);
        }

        [Test]
        public void WorldPlanetaryCanvasSmokeTesterTracksTempJobScratchAtomically()
        {
            string source = ReadProjectFile(WorldPlanetaryCanvasSmokeTesterPath);

            StringAssert.Contains("private static NativeArray<T> AllocateTrackedTempJobArray<T>", source);
            StringAssert.Contains("DisposeTracked<T>", source);
            StringAssert.Contains("NativeMemorySentinel.RegisterNativeArray(array, NativeMemoryOwner, label, NativeAllocationLifetime.TempJob)", source);
            StringAssert.Contains("NativeMemorySentinel.UnregisterPointer(trackedPointer)", source);
            StringAssert.Contains("AllocateTrackedTempJobArray<float>(CellCount, \"heights\"", source);
            StringAssert.Contains("AllocateTrackedTempJobArray<float4>(CellCount, \"weights\"", source);
            StringAssert.Contains("DisposeTracked(ref slopeWeights)", source);
            Assert.AreEqual(1, CountOccurrences(source, "new NativeArray<T>("), "World planetary canvas smoke allocations must stay centralized in AllocateTrackedTempJobArray.");
            Assert.AreEqual(1, CountOccurrences(source, "RegisterNativeArray(array, NativeMemoryOwner, label, NativeAllocationLifetime.TempJob)"), "World planetary canvas smoke registrations must stay centralized in AllocateTrackedTempJobArray.");

            StringAssert.DoesNotContain("RegisterTempJobArray", source);
            StringAssert.DoesNotContain("new NativeArray<float>", source);
            StringAssert.DoesNotContain("new NativeArray<float4>", source);
            StringAssert.DoesNotContain("NativeMemorySentinel.RegisterNativeArray(heights", source);
            StringAssert.DoesNotContain("heights.Dispose()", source);
        }

        [Test]
        public void SavePersistenceOmegaSmokeTesterTracksTempJobScratchAtomically()
        {
            string source = ReadProjectFile(SavePersistenceOmegaSmokeTesterPath);

            StringAssert.Contains("private static NativeArray<T> AllocateTrackedTempJobArray<T>", source);
            StringAssert.Contains("DisposeTrackedTempJobArray<T>", source);
            StringAssert.Contains("NativeMemorySentinel.RegisterNativeArray(array, NativeMemoryOwner, label, NativeAllocationLifetime.TempJob)", source);
            StringAssert.Contains("NativeMemorySentinel.UnregisterPointer(trackedPointer)", source);
            StringAssert.Contains("AllocateTrackedTempJobArray<IndexedSectorBoundsProbe>(8, BoundsProbeLabel", source);
            StringAssert.Contains("AllocateTrackedTempJobArray<byte>(probes.Length, BoundsProbeResultsLabel", source);
            StringAssert.Contains("DisposeTrackedTempJobArray(ref probes)", source);
            StringAssert.Contains("DisposeTrackedTempJobArray(ref results)", source);
            Assert.AreEqual(2, CountOccurrences(source, "new NativeArray<T>("), "Save persistence omega smoke should contain one helper construction and one self-audit literal.");
            Assert.AreEqual(2, CountOccurrences(source, "RegisterNativeArray(array, NativeMemoryOwner, label, NativeAllocationLifetime.TempJob)"), "Save persistence omega smoke should contain one helper registration and one self-audit literal.");

            StringAssert.DoesNotContain("RegisterTempJobArray", source);
            StringAssert.DoesNotContain("new NativeArray<IndexedSectorBoundsProbe>", source);
            StringAssert.DoesNotContain("new NativeArray<byte>", source);
            StringAssert.DoesNotContain("NativeMemorySentinel.UnregisterNativeArray(probes)", source);
            StringAssert.DoesNotContain("probes.Dispose()", source);
            StringAssert.DoesNotContain("results.Dispose()", source);
        }

        [Test]
        public void SaveSystemRuntimeSmokeTesterTracksFallbackScratchAtomically()
        {
            string source = ReadProjectFile(SaveSystemRuntimeSmokeTesterPath);

            StringAssert.Contains("private static NativeArray<T> AllocateTrackedTempJobArray<T>", source);
            StringAssert.Contains("private static NativeList<T> AllocateTrackedTempJobList<T>", source);
            StringAssert.Contains("DisposeTrackedTempJobArray<T>", source);
            StringAssert.Contains("private static void DisposeTrackedTempJobList<T>", source);
            StringAssert.Contains("NativeMemorySentinel.RegisterNativeArray(", source);
            StringAssert.Contains("NativeMemorySentinel.RegisterNativeListInstance(", source);
            StringAssert.Contains("NativeMemorySentinel.UnregisterPointer(trackedPointer)", source);
            StringAssert.Contains("NativeMemorySentinel.Unregister(sentinelId)", source);
            StringAssert.Contains("AllocateTrackedTempJobArray<long>(1, RequestedSectorScratchLabel", source);
            StringAssert.Contains("AllocateTrackedTempJobList<PersistentWorldDeltaRecord>(16, RestoredRecordsScratchLabel)", source);
            StringAssert.Contains("DisposeTrackedTempJobList(ref restoredRecords, ref restoredRecordsSentinelId)", source);
            Assert.AreEqual(1, CountOccurrences(source, "new NativeArray<T>("), "Save system runtime smoke array allocation must stay centralized in AllocateTrackedTempJobArray.");
            Assert.AreEqual(1, CountOccurrences(source, "new NativeList<T>("), "Save system runtime smoke list allocation must stay centralized in AllocateTrackedTempJobList.");

            StringAssert.DoesNotContain("new NativeArray<long>", source);
            StringAssert.DoesNotContain("new NativeList<PersistentWorldDeltaRecord>", source);
            StringAssert.DoesNotContain("NativeMemorySentinel.RegisterNativeArray(requestedSectors", source);
            StringAssert.DoesNotContain("NativeMemorySentinel.RegisterNativeList(restoredRecords", source);
            StringAssert.DoesNotContain("NativeMemorySentinel.UnregisterNativeList", source);
            StringAssert.DoesNotContain("requestedSectors.Dispose()", source);
            StringAssert.DoesNotContain("restoredRecords.Dispose()", source);
        }

        [Test]
        public void SaveRecoverySmokeTesterTracksTempStagingBuffersAtomically()
        {
            string source = ReadProjectFile(SaveRecoverySmokeTesterPath);

            StringAssert.Contains("private static NativeArray<T> AllocateTrackedTempArray<T>", source);
            StringAssert.Contains("DisposeTrackedTempArray<T>", source);
            StringAssert.Contains("NativeMemorySentinel.RegisterNativeArray(array, NativeMemoryOwner, label, NativeAllocationLifetime.Temp)", source);
            StringAssert.Contains("NativeMemorySentinel.UnregisterPointer(trackedPointer)", source);
            StringAssert.Contains("AllocateTrackedTempArray<PersistentWorldDeltaRecord>(1, PersistentWorldDeltasLabel", source);
            StringAssert.Contains("AllocateTrackedTempArray<byte>(SmokeRawPayloadCapacityBytes, RawBufferLabel", source);
            StringAssert.Contains("AllocateTrackedTempArray<byte>(SmokeCompressedPayloadCapacityBytes, CompressedBufferLabel", source);
            StringAssert.Contains("DisposeTrackedTempArray(ref persistentWorldDeltas)", source);
            Assert.AreEqual(1, CountOccurrences(source, "new NativeArray<T>("), "Save recovery staging allocations must stay centralized in AllocateTrackedTempArray.");
            Assert.AreEqual(1, CountOccurrences(source, "RegisterNativeArray(array, NativeMemoryOwner, label, NativeAllocationLifetime.Temp)"), "Save recovery staging registrations must stay centralized in AllocateTrackedTempArray.");

            StringAssert.DoesNotContain("new NativeArray<PersistentWorldDeltaRecord>", source);
            StringAssert.DoesNotContain("new NativeArray<byte>", source);
            StringAssert.DoesNotContain("compressedBuffer.Dispose()", source);
            StringAssert.DoesNotContain("rawBuffer.Dispose()", source);
            StringAssert.DoesNotContain("persistentWorldDeltas.Dispose()", source);
        }

        [Test]
        public void SaveSidecarStorageTracksTempBuffersAtomically()
        {
            string source = ReadProjectFile(SaveSidecarStoragePath);

            StringAssert.Contains("private static NativeArray<byte> AllocateTempNativeArrayBuffer", source);
            StringAssert.Contains("NativeArray<byte> buffer = new NativeArray<byte>(length, Allocator.Temp, options);", source);
            StringAssert.Contains("RegisterTempNativeArrayBuffer(buffer, label)", source);
            StringAssert.Contains("H8Memory.Release(ref buffer, NativeArrayOwnerSystem)", source);
            StringAssert.Contains("RestoreTempNativeArrayBufferSentinelOrThrow", source);
            StringAssert.Contains("AllocateTempNativeArrayBuffer(byteCount, MetadataWriteBufferLabel", source);
            StringAssert.Contains("AllocateTempNativeArrayBuffer((int)fileLength, MetadataReadBufferLabel", source);
            StringAssert.Contains("AllocateTempNativeArrayBuffer(byteCount, MaintenanceWriteBufferLabel", source);
            StringAssert.Contains("AllocateTempNativeArrayBuffer((int)fileLength, MaintenanceReadBufferLabel", source);
            StringAssert.Contains("DisposeTempNativeArrayBuffer(ref buffer, MetadataWriteBufferLabel)", source);
            StringAssert.Contains("DisposeTempNativeArrayBuffer(ref buffer, MaintenanceReadBufferLabel)", source);
            StringAssert.Contains("if (!TryResolveMetadataByteCount(metadata, out int byteCount, out error))", source);
            StringAssert.Contains("if (!TryResolveMaintenanceByteCount(record, out int byteCount, out error))", source);
            StringAssert.Contains("private static bool TryAddStringByteCount(ref long total, string value, out string error)", source);
            StringAssert.Contains("private static bool TryResolveUtf16ByteCount(int charCount, out int byteCount)", source);
            StringAssert.Contains("return Path.Combine(root, NormalizePersistentRelativeSegment(relativePath));", source);
            StringAssert.Contains("SaveMetadata loaded = new SaveMetadata();", source);
            StringAssert.Contains("if (string.IsNullOrEmpty(error))", source);
            StringAssert.Contains("error = reader.Error;", source);
            StringAssert.Contains("metadata = null;", source);
            StringAssert.Contains("metadata = loaded;", source);
            Assert.AreEqual(2, CountOccurrences(source, "SaveSlotMaintenanceRecord loaded = new SaveSlotMaintenanceRecord();"), "Current and legacy maintenance decoders must stage into local records.");
            Assert.AreEqual(2, CountOccurrences(source, "record = loaded;"), "Maintenance decoders should publish records only after complete decode.");
            StringAssert.Contains("loaded.ApplyStateFlags(stateFlags);", source);
            Assert.AreEqual(1, CountOccurrences(source, "new NativeArray<byte>"), "Sidecar Temp byte buffers must stay centralized in AllocateTempNativeArrayBuffer.");
            Assert.AreEqual(2, CountOccurrences(source, "NativeMemorySentinel.RegisterNativeArray(buffer"), "Sidecar storage has one normal registration and one restore registration.");

            StringAssert.DoesNotContain("RegisterTempNativeArrayBuffer(buffer, \"", source);
            StringAssert.DoesNotContain("new NativeArray<byte>(byteCount", source);
            StringAssert.DoesNotContain("new NativeArray<byte>((int)fileLength", source);
            StringAssert.DoesNotContain("GetStringByteCount(", source);
            StringAssert.DoesNotContain("checked(value.Length * sizeof(char))", source);
            StringAssert.DoesNotContain("checked(charCount * sizeof(char))", source);
            StringAssert.DoesNotContain("HectonPersistentPathPolicy.CombineFile(relativePath)", source);
            StringAssert.DoesNotContain("metadata = new SaveMetadata();\n                return reader.ReadString", source);
            StringAssert.DoesNotContain("record.ApplyStateFlags(stateFlags);", source);
            StringAssert.DoesNotContain("record = new SaveSlotMaintenanceRecord();\n            error = string.Empty;\n            if (!reader.ReadString(out record.SlotName)", source);
        }

        [Test]
        public void EntityDeltaCompressionArchitectureTracksTelemetryDumpPayloadAtomically()
        {
            string source = ReadProjectFile(EntityDeltaCompressionArchitecturePath);

            StringAssert.Contains("private static NativeArray<byte> AllocateTempNativeArrayBuffer", source);
            StringAssert.Contains("NativeArray<byte> buffer = new NativeArray<byte>(length, Allocator.Temp, options);", source);
            StringAssert.Contains("RegisterTempNativeArrayBuffer(buffer, label)", source);
            StringAssert.Contains("H8Memory.Release(ref buffer, NativeArrayOwnerSystem)", source);
            StringAssert.Contains("RestoreTempNativeArrayBufferSentinelOrThrow", source);
            StringAssert.Contains("NativeArray<byte> payload = AllocateTempNativeArrayBuffer(", source);
            StringAssert.Contains("TelemetryDumpPayloadLabel", source);
            StringAssert.Contains("DisposeTempNativeArrayBuffer(ref payload, TelemetryDumpPayloadLabel)", source);
            Assert.AreEqual(1, CountOccurrences(source, "new NativeArray<byte>"), "Entity delta telemetry dump Temp payload allocation must stay centralized in AllocateTempNativeArrayBuffer.");
            Assert.AreEqual(2, CountOccurrences(source, "NativeMemorySentinel.RegisterNativeArray(buffer"), "Entity delta telemetry dump has one normal registration and one restore registration.");

            StringAssert.DoesNotContain("RegisterTempNativeArrayBuffer(payload", source);
            StringAssert.DoesNotContain("new NativeArray<byte>(", source.Replace("new NativeArray<byte>(length, Allocator.Temp, options);", string.Empty));
        }

        [Test]
        public void SaveBinaryStorageTracksIndexedSectorWriteHandleScratchAtomically()
        {
            string source = ReadProjectFile(SaveBinaryStoragePath);

            StringAssert.Contains("internal static NativeArray<T> AllocateRegisteredArray<T>", source);
            StringAssert.Contains("NativeArray<T> array = new NativeArray<T>(length, Allocator.TempJob, options);", source);
            StringAssert.Contains("NativeMemorySentinel.RegisterNativeArray(array, NativeMemoryOwner, label, NativeAllocationLifetime.TempJob)", source);
            StringAssert.Contains("writeHandle.SourceStates = IndexedSectorEntityStateWriteHandle.AllocateRegisteredArray<EntityDataRecord>", source);
            StringAssert.Contains("writeHandle.SortEntries = IndexedSectorEntityStateWriteHandle.AllocateRegisteredArray<SectorEntityStateSortEntry>", source);
            StringAssert.Contains("writeHandle.CompactStates = IndexedSectorEntityStateWriteHandle.AllocateRegisteredArray<SectorCompactEntityStateRecord16>", source);
            StringAssert.Contains("writeHandle.FileBytes = IndexedSectorEntityStateWriteHandle.AllocateRegisteredArray<byte>", source);
            StringAssert.Contains("writeHandle.RadixOffsets = IndexedSectorEntityStateWriteHandle.AllocateRegisteredArray<int>", source);
            StringAssert.Contains("DisposeRegisteredArray(ref SourceStates)", source);
            StringAssert.Contains("DisposeRegisteredArray(ref RadixOffsets)", source);
            StringAssert.Contains("DisposeTrackedNativeArrayByPointer(ref array)", source);
            StringAssert.Contains("JobHandle scheduledHandle = default;", source);
            StringAssert.Contains("scheduledHandle = buildHandle;", source);
            StringAssert.Contains("scheduledHandle = sortHandle;", source);
            StringAssert.Contains("scheduledHandle = extractHandle;", source);
            StringAssert.Contains("scheduledHandle = compactHandle;", source);
            StringAssert.Contains("scheduledHandle = compressHandle;", source);
            StringAssert.Contains("writeHandle.Handle = scheduledHandle;", source);
            StringAssert.Contains("DisposeIndexedSectorEntityStateOverrideWriteDeferred(ref writeHandle, default);", source);
            Assert.AreEqual(1, CountOccurrences(source, "NativeArray<T> array = new NativeArray<T>(length, Allocator.TempJob, options);"), "Indexed-sector entity-state write scratch allocation must stay centralized in AllocateRegisteredArray.");

            int disposeMethodIndex = source.IndexOf("internal JobHandle DisposeDeferred(JobHandle dependency)", StringComparison.Ordinal);
            Assert.GreaterOrEqual(disposeMethodIndex, 0, source);
            int disposeNextMethodIndex = source.IndexOf(
                "private static NativeArray<T> AllocateRegisteredPersistentScratchNativeArray",
                disposeMethodIndex,
                StringComparison.Ordinal);
            Assert.Greater(disposeNextMethodIndex, disposeMethodIndex, source);

            string disposeMethodBody = source.Substring(disposeMethodIndex, disposeNextMethodIndex - disposeMethodIndex);
            StringAssert.Contains("disposeHandle.Complete();", disposeMethodBody);
            StringAssert.Contains("Dispose();", disposeMethodBody);
            StringAssert.DoesNotContain("UnregisterNativeMemorySentinel();", disposeMethodBody);
            StringAssert.DoesNotContain("SourceStates.Dispose(disposeHandle);", disposeMethodBody);
            StringAssert.DoesNotContain("RadixOffsets.Dispose(disposeHandle);", disposeMethodBody);

            StringAssert.DoesNotContain("internal void RegisterNativeMemorySentinel", source);
            StringAssert.DoesNotContain("writeHandle.RegisterNativeMemorySentinel()", source);
            StringAssert.DoesNotContain("RegisterArray(SourceStates", source);
            StringAssert.DoesNotContain("writeHandle.SourceStates = new NativeArray<EntityDataRecord>", source);
            StringAssert.DoesNotContain("writeHandle.SortEntries = new NativeArray<SectorEntityStateSortEntry>", source);
            StringAssert.DoesNotContain("writeHandle.RadixScratch = new NativeArray<SectorEntityStateSortEntry>", source);
            StringAssert.DoesNotContain("writeHandle.SortedEntityStates = new NativeArray<EntityDataRecord>", source);
            StringAssert.DoesNotContain("writeHandle.CompactStates = new NativeArray<SectorCompactEntityStateRecord16>", source);
            StringAssert.DoesNotContain("writeHandle.FileBytes = new NativeArray<byte>", source);
            StringAssert.DoesNotContain("writeHandle.ResultLength = new NativeArray<int>", source);
            StringAssert.DoesNotContain("writeHandle.RadixCounts = new NativeArray<int>", source);
            StringAssert.DoesNotContain("writeHandle.RadixOffsets = new NativeArray<int>", source);
        }

        [Test]
        public void SaveBinaryStorageTracksPersistentReadBuffersAtomically()
        {
            string source = ReadProjectFile(SaveBinaryStoragePath);

            StringAssert.Contains("private static NativeArray<byte> AllocateCachedReadWindowBytes(int length, out int sentinelId)", source);
            StringAssert.Contains("windowBytes = AllocateCachedReadWindowBytes((int)windowLength, out windowBytesSentinelId);", source);
            StringAssert.Contains("BytesSentinelId = windowBytesSentinelId", source);
            StringAssert.Contains("DisposeCachedReadWindowBytes(ref window.Bytes, ref window.BytesSentinelId)", source);
            StringAssert.Contains("private static NativeArray<byte> AllocateReadOnlyMappingBytes(int length)", source);
            StringAssert.Contains("fileBytes = AllocateReadOnlyMappingBytes((int)fileLength);", source);
            StringAssert.Contains("DisposeReadOnlyMappingBytes(ref fileBytes)", source);
            StringAssert.DoesNotContain("RestoreCachedReadWindowSentinelOrThrow", source);
            StringAssert.DoesNotContain("RestoreReadOnlyMappingSentinelOrThrow", source);
            Assert.AreEqual(2, CountOccurrences(source, "NativeArray<byte> bytes = new NativeArray<byte>(length, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);"), "Cached read window and read-only mapping byte allocations must stay centralized in their helper methods.");

            StringAssert.DoesNotContain("windowBytes = new NativeArray<byte>", source);
            StringAssert.DoesNotContain("NativeMemorySentinel.RegisterNativeArray(windowBytes", source);
            StringAssert.DoesNotContain("fileBytes = new NativeArray<byte>", source);
            StringAssert.DoesNotContain("NativeMemorySentinel.RegisterNativeArray(fileBytes", source);
        }

        [Test]
        public void SaveBinaryStorageTracksIndexedSectorPersistentScratchAtomically()
        {
            string source = ReadProjectFile(SaveBinaryStoragePath);

            StringAssert.Contains("private static NativeArray<T> AllocateRegisteredPersistentScratchNativeArray<T>", source);
            StringAssert.Contains("NativeArray<T> array = new NativeArray<T>(length, Allocator.Persistent, options);", source);
            StringAssert.Contains("RegisterPersistentScratchNativeArray(array, label)", source);
            StringAssert.Contains("DisposeRegisteredPersistentScratchNativeArray(ref compactBytes, IndexedSectorCompactionBufferLabel)", source);
            StringAssert.Contains("DisposeRegisteredPersistentScratchNativeArray(ref commitBytes, IndexedSectorCommitBufferLabel)", source);
            StringAssert.Contains("compactBytes = AllocateRegisteredPersistentScratchNativeArray<byte>", source);
            StringAssert.Contains("commitBytes = AllocateRegisteredPersistentScratchNativeArray<byte>", source);
            Assert.AreEqual(1, CountOccurrences(source, "NativeArray<T> array = new NativeArray<T>(length, Allocator.Persistent, options);"), "Indexed-sector persistent scratch allocation must stay centralized in AllocateRegisteredPersistentScratchNativeArray.");

            StringAssert.DoesNotContain("compactBytes = new NativeArray<byte>", source);
            StringAssert.DoesNotContain("commitBytes = new NativeArray<byte>", source);
            StringAssert.DoesNotContain("RegisterPersistentScratchNativeArray(compactBytes", source);
            StringAssert.DoesNotContain("RegisterPersistentScratchNativeArray(commitBytes", source);
        }

        [Test]
        public void WalIntegrityFuzzerCoreTracksTempAndTempJobScratchAtomically()
        {
            string source = ReadProjectFile(WalIntegrityFuzzerCorePath);

            StringAssert.Contains("private static NativeArray<T> AllocateTrackedTempArray<T>", source);
            StringAssert.Contains("private static NativeArray<T> AllocateTrackedTempJobArray<T>", source);
            StringAssert.Contains("private static NativeArray<T> AllocateTrackedArray<T>", source);
            StringAssert.Contains("DisposeTrackedTempArray<T>", source);
            StringAssert.Contains("DisposeTrackedTempJobArray<T>", source);
            StringAssert.Contains("NativeMemorySentinel.RegisterNativeArray(array, NativeMemoryOwner, label, lifetime)", source);
            StringAssert.Contains("NativeMemorySentinel.UnregisterPointer(trackedPointer)", source);
            StringAssert.Contains("AllocateTrackedTempArray<WalFuzzerProfileDTO>(4, ProfilesScratchLabel", source);
            StringAssert.Contains("AllocateTrackedTempArray<byte>((int)info.Length, ProfileCsvBytesScratchLabel", source);
            StringAssert.Contains("AllocateTrackedTempJobArray<WalFuzzerTelemetryEntry>(TelemetryCapacity, TelemetryScratchLabel", source);
            StringAssert.Contains("buffers.CurrentTree = AllocateTrackedTempJobArray<MerkleNodeDTO>", source);
            StringAssert.Contains("buffers.Lz4HashTable = AllocateTrackedTempJobArray<int>", source);
            StringAssert.Contains("DisposeTrackedTempJobArray(ref buffers.CurrentTree)", source);
            StringAssert.Contains("DisposeTrackedTempJobArray(ref replayedDeltaBytes)", source);
            StringAssert.Contains("readback = AllocateTrackedTempJobArray<byte>(payloadBytes, LoopReadbackScratchLabel", source);
            Assert.AreEqual(1, CountOccurrences(source, "new NativeArray<T>("), "WAL fuzzer NativeArray construction must stay centralized in AllocateTrackedArray.");
            Assert.AreEqual(1, CountOccurrences(source, "RegisterNativeArray(array, NativeMemoryOwner, label, lifetime)"), "WAL fuzzer registrations must stay centralized in AllocateTrackedArray.");

            StringAssert.DoesNotContain("new NativeArray<byte>", source);
            StringAssert.DoesNotContain("new NativeArray<int>", source);
            StringAssert.DoesNotContain("new NativeArray<MerkleNodeDTO>", source);
            StringAssert.DoesNotContain("new NativeArray<StateLeafDescriptor>", source);
            StringAssert.DoesNotContain("new NativeArray<StateDeltaRecordDTO>", source);
            StringAssert.DoesNotContain("new NativeArray<WalFuzzerProfileDTO>", source);
            StringAssert.DoesNotContain("new NativeArray<WalFuzzerResultDTO>", source);
            StringAssert.DoesNotContain("new NativeArray<WalFuzzerTelemetryEntry>", source);
            StringAssert.DoesNotContain("payload.Dispose()", source);
            StringAssert.DoesNotContain("recovered.Dispose()", source);
            StringAssert.DoesNotContain("telemetry.Dispose()", source);
            StringAssert.DoesNotContain("buffers.CurrentTree.Dispose()", source);
            StringAssert.DoesNotContain("replayedDeltaBytes.Dispose()", source);
            StringAssert.DoesNotContain("readback.Dispose()", source);
        }

        [Test]
        public void WalIntegrityFuzzerCoreShinobu357TracksFallbackScratchAtomically()
        {
            string source = ReadProjectFile(WalIntegrityFuzzerCoreShinobu357Path);

            StringAssert.Contains("EnsureShinobu357VaultBuffer(vault, Shinobu357PayloadBufferId", source);
            StringAssert.Contains("ResolveShinobu357Prefix(payloadOwner, payloadBytes)", source);
            StringAssert.Contains("payloadOwner = AllocateTrackedTempJobArray<byte>(payloadBytes, Shinobu357PayloadFallbackScratchLabel", source);
            StringAssert.Contains("corruptWalOwner = AllocateTrackedTempJobArray<byte>(payloadBytes, Shinobu357CorruptWalFallbackScratchLabel", source);
            StringAssert.Contains("stateOwner = AllocateTrackedTempJobArray<WalFuzzStateDTO>(1, Shinobu357StateFallbackScratchLabel", source);
            StringAssert.Contains("telemetryOwner = AllocateTrackedTempJobArray<WalFuzzTelemetryEntry>(Shinobu357TelemetryCapacity, Shinobu357TelemetryFallbackScratchLabel", source);
            StringAssert.Contains("legacyTelemetry = AllocateTrackedTempJobArray<WalFuzzerTelemetryEntry>(TelemetryCapacity, Shinobu357LegacyTelemetryScratchLabel", source);
            StringAssert.Contains("hashScratchOwner = AllocateTrackedTempJobArray<byte>(backupByteCount, Shinobu357HashScratchFallbackLabel", source);
            StringAssert.Contains("fileHandleStatusOwner = AllocateTrackedTempJobArray<WalFuzzFileHandleStatusDTO>(1, Shinobu357FileHandleStatusFallbackLabel", source);
            StringAssert.Contains("DisposeTrackedTempJobArray(ref legacyTelemetry)", source);
            StringAssert.Contains("DisposeTrackedTempJobArray(ref payloadOwner)", source);
            StringAssert.Contains("DisposeTrackedTempJobArray(ref hashScratchOwner)", source);

            StringAssert.DoesNotContain("new NativeArray<", source);
            StringAssert.DoesNotContain(".Dispose()", source);
            StringAssert.DoesNotContain("NativeMemorySentinel.RegisterNativeArray(", source);
        }

        [Test]
        public void HydraulicErosionSmokeTesterTracksTempJobNativeArraysAtomically()
        {
            string source = ReadProjectFile(HydraulicErosionSmokeTesterPath);

            StringAssert.Contains("private static NativeArray<T> AllocateTrackedTempJobArray<T>", source);
            StringAssert.Contains("NativeMemorySentinel.RegisterNativeArray(array, NativeMemoryOwner, label, NativeAllocationLifetime.TempJob)", source);
            StringAssert.Contains("NativeMemorySentinel.UnregisterPointer(trackedPointer)", source);
            StringAssert.Contains("AllocateTrackedTempJobArray<float>(pixelCount", source);
            StringAssert.Contains("AllocateTrackedTempJobArray<HydraulicErosionMetricBlock>", source);
            StringAssert.Contains("DisposeTracked(ref metricBlocks)", source);
            StringAssert.Contains("DisposeTracked(ref wear)", source);
            Assert.AreEqual(1, CountOccurrences(source, "new NativeArray<T>("), "Hydraulic erosion smoke NativeArray construction must stay centralized in AllocateTrackedTempJobArray.");
            Assert.AreEqual(1, CountOccurrences(source, "RegisterNativeArray(array, NativeMemoryOwner, label, NativeAllocationLifetime.TempJob)"), "Hydraulic erosion smoke registrations must stay centralized in AllocateTrackedTempJobArray.");

            StringAssert.DoesNotContain("RegisterTempJobBuffers", source);
            StringAssert.DoesNotContain("new NativeArray<float>", source);
            StringAssert.DoesNotContain("new NativeArray<HydraulicErosionMetricBlock>", source);
            StringAssert.DoesNotContain("NativeMemorySentinel.RegisterNativeArray(before", source);
            StringAssert.DoesNotContain("NativeMemorySentinel.RegisterNativeArray(metricBlocks", source);
        }

        [Test]
        public void ErosionTestHarnessTracksTempJobArraysAndQueueAtomically()
        {
            string source = ReadProjectFile(ErosionTestHarnessPath);

            StringAssert.Contains("private static NativeArray<T> AllocateTrackedTempJobArray<T>", source);
            StringAssert.Contains("private static NativeQueue<HydraulicErosionHeightDelta> AllocateTrackedHeightDeltaQueue", source);
            StringAssert.Contains("registrationId = NativeMemorySentinel.RegisterNativeArray(array, NativeMemoryOwner, label, NativeAllocationLifetime.TempJob)", source);
            StringAssert.Contains("registrationId = NativeMemorySentinel.RegisterNativeQueueInstance(queue, heightDeltaQueueCapacity, NativeMemoryOwner, HeightDeltaQueueLabel, NativeAllocationLifetime.TempJob)", source);
            StringAssert.Contains("NativeMemorySentinel.Unregister(registrationId)", source);
            StringAssert.Contains("DisposeTrackedQueue(ref heightDeltas, ref heightDeltasRegistrationId)", source);
            StringAssert.DoesNotContain("NativeMemorySentinel.UnregisterNativeQueue", source);
            StringAssert.Contains("AllocateTrackedTempJobArray<Color32>", source);
            StringAssert.Contains("AllocateTrackedTempJobArray<ErosionSmokeMetrics>", source);
            StringAssert.Contains("AllocateTrackedHeightDeltaQueue(ResolveHeightDeltaQueueCapacity", source);
            Assert.AreEqual(1, CountOccurrences(source, "new NativeArray<T>("), "Erosion harness NativeArray construction must stay centralized in AllocateTrackedTempJobArray.");
            Assert.AreEqual(1, CountOccurrences(source, "new NativeQueue<HydraulicErosionHeightDelta>"), "Erosion harness height-delta queue construction must stay centralized in AllocateTrackedHeightDeltaQueue.");

            StringAssert.DoesNotContain("RegisterTempJobBuffers", source);
            StringAssert.DoesNotContain("new NativeArray<float>", source);
            StringAssert.DoesNotContain("new NativeArray<int>", source);
            StringAssert.DoesNotContain("new NativeArray<Color32>", source);
            StringAssert.DoesNotContain("new NativeArray<ErosionSmokeMetrics>", source);
            StringAssert.DoesNotContain("NativeMemorySentinel.RegisterNativeArray(raw", source);
            StringAssert.DoesNotContain("NativeMemorySentinel.RegisterNativeArray(pixels", source);
            StringAssert.DoesNotContain("NativeMemorySentinel.RegisterNativeArray(maxValue", source);
        }

        [Test]
        public void PlanetaryCanvasSmokeTesterTracksInfluenceCellsAtomically()
        {
            string source = ReadProjectFile(PlanetaryCanvasSmokeTesterPath);

            StringAssert.Contains("private static NativeArray<T> AllocateTrackedTempJobArray<T>", source);
            StringAssert.Contains("DisposeTracked<T>", source);
            StringAssert.Contains("NativeMemorySentinel.RegisterNativeArray(array, NativeMemoryOwner, label, NativeAllocationLifetime.TempJob)", source);
            StringAssert.Contains("NativeMemorySentinel.UnregisterPointer(trackedPointer)", source);
            StringAssert.Contains("AllocateTrackedTempJobArray<WorldProceduralFieldSampler.BiomeInfluenceCell>", source);
            StringAssert.Contains("DisposeTracked(ref influenceCells)", source);
            Assert.AreEqual(1, CountOccurrences(source, "new NativeArray<T>("), "Planetary canvas influence-cell construction must stay centralized in AllocateTrackedTempJobArray.");
            Assert.AreEqual(1, CountOccurrences(source, "RegisterNativeArray(array, NativeMemoryOwner, label, NativeAllocationLifetime.TempJob)"), "Planetary canvas registrations must stay centralized in AllocateTrackedTempJobArray.");

            StringAssert.DoesNotContain("new NativeArray<WorldProceduralFieldSampler.BiomeInfluenceCell>", source);
            StringAssert.DoesNotContain("influenceCellsRegistered", source);
            StringAssert.DoesNotContain("nameof(influenceCells)", source);
            StringAssert.DoesNotContain("influenceCells.Dispose()", source);
        }

        [Test]
        public void Arm64MemoryAlignmentXRayTracksMockStressArraysAtomically()
        {
            string source = ReadProjectFile(Arm64MemoryAlignmentXRayWindowPath);

            StringAssert.Contains("using Hecton8.Core;", source);
            StringAssert.Contains("private static NativeArray<T> AllocateTrackedTempJobArray<T>", source);
            StringAssert.Contains("DisposeTracked<T>", source);
            StringAssert.Contains("NativeMemorySentinel.RegisterNativeArray(array, NativeMemoryOwner, label, NativeAllocationLifetime.TempJob)", source);
            StringAssert.Contains("NativeMemorySentinel.UnregisterPointer(trackedPointer)", source);
            StringAssert.Contains("AllocateTrackedTempJobArray<MockAlignedLayout>", source);
            StringAssert.Contains("AllocateTrackedTempJobArray<double>", source);
            StringAssert.Contains("DisposeTracked(ref input)", source);
            StringAssert.Contains("DisposeTracked(ref output)", source);
            Assert.AreEqual(1, CountOccurrences(source, "new NativeArray<T>("), "ARM64 alignment mock stress arrays must stay centralized in AllocateTrackedTempJobArray.");

            StringAssert.DoesNotContain("new NativeArray<MockAlignedLayout>", source);
            StringAssert.DoesNotContain("new NativeArray<double>", source);
            StringAssert.DoesNotContain("input.Dispose()", source);
            StringAssert.DoesNotContain("output.Dispose()", source);
        }

        [Test]
        public void BaseModuleCatalogEditorTracksCsvCostScratchThroughSentinel()
        {
            string source = ReadProjectFile(BaseModuleCatalogEditorToolsPath);

            StringAssert.Contains("private static NativeArray<T> AllocateTrackedArray<T>", source);
            StringAssert.Contains("DisposeTrackedArray<T>", source);
            StringAssert.Contains("NativeMemorySentinel.RegisterNativeArray(array, NativeMemoryOwner, label, lifetime)", source);
            StringAssert.Contains("NativeMemorySentinel.UnregisterPointer(trackedPointer)", source);
            StringAssert.Contains("costs = AllocateTrackedArray<ModuleCostDTO>", source);
            StringAssert.Contains("DisposeTrackedArray(ref csvCosts)", source);
            Assert.AreEqual(1, CountOccurrences(source, "new NativeArray<T>("), "Base module catalog CSV scratch allocation must stay centralized in AllocateTrackedArray.");

            StringAssert.DoesNotContain("new NativeArray<ModuleCostDTO>", source);
            StringAssert.DoesNotContain("csvCosts.Dispose()", source);
        }

        [Test]
        public void EconomyRecipeTunerTracksTempImportScratchThroughSentinel()
        {
            string source = ReadProjectFile(EconomyRecipeTunerWindowPath);

            StringAssert.Contains("private static NativeArray<T> AllocateTrackedArray<T>", source);
            StringAssert.Contains("DisposeTrackedArray<T>", source);
            StringAssert.Contains("NativeMemorySentinel.RegisterNativeArray(array, NativeMemoryOwner, label, lifetime)", source);
            StringAssert.Contains("NativeMemorySentinel.UnregisterPointer(trackedPointer)", source);
            StringAssert.Contains("binary = AllocateTrackedArray<byte>", source);
            StringAssert.Contains("constants = AllocateTrackedArray<ItemPhysicalConstantsDTO>", source);
            StringAssert.Contains("DisposeTrackedArray(ref binary)", source);
            StringAssert.Contains("DisposeTrackedArray(ref constants)", source);
            Assert.AreEqual(1, CountOccurrences(source, "new NativeArray<T>("), "Economy recipe tuner import scratch allocations must stay centralized in AllocateTrackedArray.");

            StringAssert.DoesNotContain("new NativeArray<byte>", source);
            StringAssert.DoesNotContain("new NativeArray<ItemPhysicalConstantsDTO>", source);
            StringAssert.DoesNotContain("using (NativeArray<byte>", source);
            StringAssert.DoesNotContain("using (NativeArray<ItemPhysicalConstantsDTO>", source);
        }

        [Test]
        public void Shinobu132CablePhysicsTunerTracksCsvScratchThroughSentinel()
        {
            string source = ReadProjectFile(Shinobu132CablePhysicsTunerWindowPath);

            StringAssert.Contains("private static NativeArray<T> AllocateTrackedArray<T>", source);
            StringAssert.Contains("DisposeTrackedArray<T>", source);
            StringAssert.Contains("NativeMemorySentinel.RegisterNativeArray(array, NativeMemoryOwner, label, lifetime)", source);
            StringAssert.Contains("NativeMemorySentinel.UnregisterPointer(trackedPointer)", source);
            StringAssert.Contains("csvBytes = AllocateTrackedArray<byte>", source);
            StringAssert.Contains("DisposeTrackedArray(ref csvBytes)", source);
            Assert.AreEqual(1, CountOccurrences(source, "new NativeArray<T>("), "Cable physics tuner CSV scratch allocation must stay centralized in AllocateTrackedArray.");

            StringAssert.DoesNotContain("new NativeArray<byte>", source);
            StringAssert.DoesNotContain("using (NativeArray<byte>", source);
            StringAssert.DoesNotContain("csvBytes.Dispose()", source);
        }

        [Test]
        public void HectonSpatialHashEditorSelfTestsTrackResultListsThroughSentinel()
        {
            string source = ReadProjectFile(HectonSpatialHashEditorSelfTestsPath);

            StringAssert.Contains("using Hecton8.Core;", source);
            StringAssert.Contains("private static NativeList<int> AllocateTrackedResults", source);
            StringAssert.Contains("DisposeTrackedResults", source);
            StringAssert.Contains("NativeMemorySentinel.RegisterNativeListInstance(results, NativeMemoryOwner, label, NativeAllocationLifetime.Temp)", source);
            StringAssert.Contains("NativeMemorySentinel.Unregister(sentinelId)", source);
            StringAssert.DoesNotContain("bool disposed = !", source);
            StringAssert.Contains("AllocateTrackedResults(8, RecycledHandleResultsLabel)", source);
            StringAssert.Contains("AllocateTrackedResults(4, MoveResultsLabel)", source);
            StringAssert.Contains("AllocateTrackedResults(4, LargeAupResultsLabel)", source);
            Assert.AreEqual(1, CountOccurrences(source, "new NativeList<int>("), "Spatial hash self-test NativeList construction must stay centralized in AllocateTrackedResults.");

            StringAssert.DoesNotContain("new NativeList<int>(8", source);
            StringAssert.DoesNotContain("new NativeList<int>(4", source);
            StringAssert.DoesNotContain("NativeMemorySentinel.RegisterNativeList(", source);
            StringAssert.DoesNotContain("NativeMemorySentinel.UnregisterNativeList", source);
        }

        [Test]
        public void GeographySanityUsesSentinelWrappersForNativeCollections()
        {
            string pipeline = ReadProjectFile(GeographySanityPipelinePath);
            string profiles = ReadProjectFile(GeographySanityProfileCsvPath);

            StringAssert.Contains("private static NativeArray<T> AllocateNativeArray<T>", pipeline);
            StringAssert.Contains("ReleaseNativeArray<T>", pipeline);
            StringAssert.Contains("NativeMemorySentinel.RegisterNativeArray(", pipeline);
            StringAssert.Contains("NativeMemorySentinel.UnregisterPointer(trackedPointer)", pipeline);
            Assert.AreEqual(1, CountOccurrences(pipeline, "new NativeArray<T>("), "Pipeline allocations must stay centralized in AllocateNativeArray.");
            StringAssert.DoesNotContain("new NativeArray<GeographySanityTelemetryEntry>", pipeline);
            StringAssert.DoesNotContain("new NativeArray<float>", pipeline);
            StringAssert.DoesNotContain("new NativeArray<SpatialEntityDTO>", pipeline);
            StringAssert.DoesNotContain("new NativeArray<SpatialAnomalyRuleDTO>", pipeline);
            StringAssert.DoesNotContain("new NativeArray<NavigationRequestDTO>", pipeline);
            StringAssert.DoesNotContain("new NativeArray<CrushDepthMaterialDTO>", pipeline);

            StringAssert.Contains("NativeMemorySentinel.RegisterNativeListInstance(", profiles);
            StringAssert.Contains("NativeMemorySentinel.Unregister(_profilesSentinelId)", profiles);
            StringAssert.DoesNotContain("NativeMemorySentinel.RegisterNativeList(", profiles);
            Assert.AreEqual(1, CountOccurrences(profiles, "new NativeList<SanityProfileDTO>"), "Profile store owns exactly one tracked NativeList.");
        }

        [Test]
        public void GeologyForgeGeneratorTracksBakeScratchThroughSentinel()
        {
            string source = ReadProjectFile(GeologyForgeGeneratorPath);

            StringAssert.Contains("private static NativeArray<T> AllocateGeologyArray<T>", source);
            StringAssert.Contains("ReleaseGeologyArray<T>", source);
            StringAssert.Contains("private static NativeParallelMultiHashMap<TKey, TValue> AllocateGeologyMultiHashMap<TKey, TValue>", source);
            StringAssert.Contains("ReleaseGeologyMultiHashMap<TKey, TValue>", source);
            StringAssert.Contains("NativeMemorySentinel.RegisterNativeArray(", source);
            StringAssert.Contains("NativeMemorySentinel.UnregisterPointer(trackedPointer)", source);
            StringAssert.Contains("NativeMemorySentinel.RegisterNativeParallelMultiHashMapInstance(", source);
            StringAssert.Contains("NativeMemorySentinel.Unregister(sentinelId)", source);
            StringAssert.DoesNotContain("NativeMemorySentinel.RegisterNativeParallelMultiHashMap(", source);
            StringAssert.DoesNotContain("NativeMemorySentinel.UnregisterNativeParallelMultiHashMap", source);
            Assert.AreEqual(1, CountOccurrences(source, "new NativeArray<T>("), "Geology Forge NativeArray allocations must stay centralized in AllocateGeologyArray.");
            Assert.AreEqual(1, CountOccurrences(source, "new NativeParallelMultiHashMap<TKey, TValue>("), "Geology Forge multi-hash-map allocations must stay centralized in AllocateGeologyMultiHashMap.");

            StringAssert.DoesNotContain("new NativeArray<GeologyBakeTelemetryEntry>", source);
            StringAssert.DoesNotContain("new NativeArray<float>", source);
            StringAssert.DoesNotContain("new NativeArray<int>", source);
            StringAssert.DoesNotContain("new NativeArray<GeologyRawVertex>", source);
            StringAssert.DoesNotContain("new NativeArray<GeologyVertex32>", source);
            StringAssert.DoesNotContain("new NativeArray<uint>", source);
            StringAssert.DoesNotContain("new NativeParallelMultiHashMap<ulong, int>", source);
            StringAssert.DoesNotContain("telemetry.Dispose()", source);
            StringAssert.DoesNotContain("density.Dispose()", source);
            StringAssert.DoesNotContain("counts.Dispose()", source);
            StringAssert.DoesNotContain("offsets.Dispose()", source);
            StringAssert.DoesNotContain("rawVertices.Dispose()", source);
            StringAssert.DoesNotContain("lodVertices.Dispose()", source);
            StringAssert.DoesNotContain("packed.Dispose()", source);
            StringAssert.DoesNotContain("indices.Dispose()", source);
            StringAssert.DoesNotContain("normalBuckets.Dispose()", source);
        }

        [Test]
        public void OfflineGeometryBakerTracksTransientNativeArraysThroughReflectionBridge()
        {
            string source = ReadProjectFile(OfflineGeometryBakerPath);

            StringAssert.Contains("using System.Reflection;", source);
            StringAssert.Contains("private const string NativeMemorySentinelTypeName = \"Hecton8.Core.NativeMemorySentinel\";", source);
            StringAssert.Contains("private static NativeArray<T> AllocateTrackedNativeArray<T>", source);
            StringAssert.Contains("DisposeTrackedNativeArray<T>", source);
            StringAssert.Contains("private static void RegisterTrackedNativeArray<T>", source);
            StringAssert.Contains("private static void UnregisterTrackedNativeArray(IntPtr trackedPointer)", source);
            StringAssert.Contains("sentinelType.GetMethod(\"RegisterNativeArray\", BindingFlags.Public | BindingFlags.Static)", source);
            StringAssert.Contains("sentinelType.GetMethod(\"UnregisterPointer\", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(IntPtr) }, null)", source);
            StringAssert.Contains("method.MakeGenericMethod(typeof(T)).Invoke", source);
            StringAssert.Contains("method.Invoke(null, new object[] { trackedPointer })", source);
            StringAssert.Contains("new object[] { array, NativeMemoryOwner, label, lifetime }", source);
            StringAssert.Contains("DisposeTrackedNativeArray(ref lod0Raw)", source);
            StringAssert.Contains("DisposeTrackedNativeArray(ref records)", source);
            Assert.AreEqual(1, CountOccurrences(source, "new NativeArray<T>("), "Offline geometry transient allocations must stay centralized in AllocateTrackedNativeArray.");

            StringAssert.DoesNotContain("using Hecton8.Core;", source);
            StringAssert.DoesNotContain("new NativeArray<OfflineGeometryRawVertex>", source);
            StringAssert.DoesNotContain("new NativeArray<OfflinePrimitiveFitResult>", source);
            StringAssert.DoesNotContain("new NativeArray<float3>", source);
            StringAssert.DoesNotContain("new NativeArray<ushort>", source);
            StringAssert.DoesNotContain("new NativeArray<int>", source);
            StringAssert.DoesNotContain("new NativeArray<OfflineGeometryVertex32>", source);
            StringAssert.DoesNotContain("new NativeArray<uint>", source);
            StringAssert.DoesNotContain("new NativeArray<OfflineSubMeshRange>", source);
            StringAssert.DoesNotContain("new NativeArray<OfflineLodManifestRecord>", source);
            StringAssert.DoesNotContain("raw.Dispose()", source);
            StringAssert.DoesNotContain("ranges.Dispose()", source);
            StringAssert.DoesNotContain("fit.Dispose()", source);
            StringAssert.DoesNotContain("hull.Dispose()", source);
            StringAssert.DoesNotContain("hullIndexBuffer.Dispose()", source);
            StringAssert.DoesNotContain("hullCount.Dispose()", source);
            StringAssert.DoesNotContain("hullIndexCount.Dispose()", source);
            StringAssert.DoesNotContain("rawVertices.Dispose()", source);
            StringAssert.DoesNotContain("packed.Dispose()", source);
            StringAssert.DoesNotContain("indices.Dispose()", source);
            StringAssert.DoesNotContain("records.Dispose()", source);
        }

        [Test]
        public void OfflineOptimizationProfileCsvTracksStagingBytesThroughSentinel()
        {
            string source = ReadProjectFile(OfflineOptimizationProfileCsvPath);

            StringAssert.Contains("using Hecton8.Core;", source);
            StringAssert.Contains("private const string NativeMemoryOwner = nameof(OfflineOptimizationProfileCsv);", source);
            StringAssert.Contains("bytes = AllocateTrackedArray<byte>(length, Allocator.Temp, NativeArrayOptions.UninitializedMemory, CsvBytesLabel, NativeAllocationLifetime.Temp)", source);
            StringAssert.Contains("DisposeTrackedArray(ref bytes)", source);
            StringAssert.Contains("NativeMemorySentinel.RegisterNativeArray(array, NativeMemoryOwner, label, lifetime)", source);
            StringAssert.Contains("NativeMemorySentinel.UnregisterPointer(trackedPointer)", source);
            Assert.AreEqual(1, CountOccurrences(source, "new NativeArray<T>("), "Offline optimization profile CSV byte staging must stay centralized in AllocateTrackedArray.");

            StringAssert.DoesNotContain("new NativeArray<byte>", source);
            StringAssert.DoesNotContain("bytes.Dispose()", source);
        }

        [Test]
        public void OfflineGeometryBakeBlackBoxTracksPersistentRingThroughReflectionBridge()
        {
            string source = ReadProjectFile(OfflineGeometryBakeBlackBoxPath);

            StringAssert.Contains("using System.Reflection;", source);
            StringAssert.Contains("private static NativeArray<T> AllocateTrackedNativeArray<T>", source);
            StringAssert.Contains("DisposeTrackedNativeArray<T>", source);
            StringAssert.Contains("RegisterNativeMemorySentinel(array, label, ResolveNativeAllocationLifetimeName(allocator))", source);
            StringAssert.Contains("UnregisterNativeMemorySentinel(array)", source);
            StringAssert.Contains("method.MakeGenericMethod(typeof(T)).Invoke", source);
            StringAssert.Contains("DisposeTrackedNativeArray(ref _ring)", source);
            Assert.AreEqual(1, CountOccurrences(source, "new NativeArray<T>("), "Offline geometry black-box ring allocation must stay centralized in AllocateTrackedNativeArray.");

            StringAssert.DoesNotContain("new NativeArray<OfflineGeometryBakeTelemetryEntry>", source);
            StringAssert.DoesNotContain("_ring.Dispose()", source);
        }

        [Test]
        public void TopographyForgeUsesSentinelWrapperForNativeArrays()
        {
            string source = ReadProjectFile(TopographyForgeGeneratorPath);

            StringAssert.Contains("private static NativeArray<T> AllocateTopographyArray<T>", source);
            StringAssert.Contains("ReleaseTopographyArray<T>", source);
            StringAssert.Contains("NativeMemorySentinel.RegisterNativeArray(", source);
            StringAssert.Contains("NativeMemorySentinel.UnregisterPointer(trackedPointer)", source);
            Assert.AreEqual(1, CountOccurrences(source, "new NativeArray<T>("), "Topography allocations must stay centralized in AllocateTopographyArray.");
            StringAssert.DoesNotContain("heights = new NativeArray<float>", source);
            StringAssert.DoesNotContain("blackBox = new NativeArray<TopographyBakeTelemetryEntry>", source);
            StringAssert.DoesNotContain("state = new NativeArray<TopographyBakeRunStateDTO>", source);
        }

        [Test]
        public void GeologyForgeEditorToolsTrackPreviewCsvAndRecipeNativeCollections()
        {
            string helper = ReadProjectFile(GeologyForgeNativeMemoryPath);
            string window = ReadProjectFile(GeologyForgeWindowPath);
            string profiles = ReadProjectFile(GeologyProfileCsvPath);
            string topography = ReadProjectFile(TopographyForgeCsvPath);
            string topographyWindow = ReadProjectFile(TopographyForgeWindowPath);
            string combined = window + profiles + topography + topographyWindow;

            StringAssert.Contains("internal static class GeologyForgeNativeMemory", helper);
            StringAssert.Contains("internal static NativeArray<T> AllocateArray<T>", helper);
            StringAssert.Contains("internal static NativeList<T> AllocateList<T>", helper);
            StringAssert.Contains("DisposeArray<T>", helper);
            StringAssert.Contains("DisposeList<T>", helper);
            StringAssert.Contains("NativeMemorySentinel.RegisterNativeArray(array, owner, label, ResolveLifetime(allocator))", helper);
            StringAssert.Contains("NativeMemorySentinel.RegisterNativeListInstance(list, owner, label, ResolveLifetime(allocator))", helper);
            StringAssert.Contains("NativeMemorySentinel.UnregisterPointer(trackedPointer)", helper);
            StringAssert.Contains("NativeMemorySentinel.Unregister(sentinelId)", helper);
            StringAssert.DoesNotContain("NativeMemorySentinel.RegisterNativeList(", helper);
            StringAssert.DoesNotContain("NativeMemorySentinel.UnregisterNativeList", helper);
            StringAssert.Contains("GeologyForgeNativeMemory.AllocateArray<float>", window);
            StringAssert.Contains("GeologyForgeNativeMemory.AllocateArray<byte>", profiles);
            StringAssert.Contains("GeologyForgeNativeMemory.AllocateArray<byte>", topography);
            StringAssert.Contains("GeologyForgeNativeMemory.AllocateList<TopographyBiomeRecipeDTO>", topography);
            StringAssert.Contains("GeologyForgeNativeMemory.DisposeList(ref _recipes, ref _recipesSentinelId)", topography);
            StringAssert.Contains("private const string NativeMemoryOwner = nameof(TopographyForgePreview);", topographyWindow);
            StringAssert.Contains("GeologyForgeNativeMemory.AllocateArray<TopographyBiomeKernelDTO>", topographyWindow);
            StringAssert.Contains("GeologyForgeNativeMemory.AllocateArray<TectonicRiftSegmentDTO>", topographyWindow);
            StringAssert.Contains("GeologyForgeNativeMemory.AllocateArray<double2>", topographyWindow);
            StringAssert.Contains("GeologyForgeNativeMemory.AllocateArray<float>", topographyWindow);
            StringAssert.Contains("GeologyForgeNativeMemory.AllocateArray<Color32>", topographyWindow);
            StringAssert.Contains("GeologyForgeNativeMemory.DisposeArray(ref pixels)", topographyWindow);
            Assert.AreEqual(1, CountOccurrences(helper, "new NativeArray<T>("), "Geology editor NativeArray construction must stay centralized in GeologyForgeNativeMemory.");
            Assert.AreEqual(1, CountOccurrences(helper, "new NativeList<T>("), "Geology editor NativeList construction must stay centralized in GeologyForgeNativeMemory.");

            StringAssert.DoesNotContain("new NativeArray<float>", combined);
            StringAssert.DoesNotContain("new NativeArray<byte>", combined);
            StringAssert.DoesNotContain("new NativeArray<TopographyBiomeKernelDTO>", combined);
            StringAssert.DoesNotContain("new NativeArray<TectonicRiftSegmentDTO>", combined);
            StringAssert.DoesNotContain("new NativeArray<double2>", combined);
            StringAssert.DoesNotContain("new NativeArray<Color32>", combined);
            StringAssert.DoesNotContain("new NativeList<TopographyBiomeRecipeDTO>", combined);
            StringAssert.DoesNotContain("density.Dispose()", combined);
            StringAssert.DoesNotContain("bytes.Dispose()", combined);
            StringAssert.DoesNotContain("_recipes.Dispose()", combined);
            StringAssert.DoesNotContain("recipes.Dispose()", topographyWindow);
            StringAssert.DoesNotContain("rifts.Dispose()", topographyWindow);
            StringAssert.DoesNotContain("warped.Dispose()", topographyWindow);
            StringAssert.DoesNotContain("raw.Dispose()", topographyWindow);
            StringAssert.DoesNotContain("terraced.Dispose()", topographyWindow);
            StringAssert.DoesNotContain("final.Dispose()", topographyWindow);
            StringAssert.DoesNotContain("pixels.Dispose()", topographyWindow);
        }

        [Test]
        public void InteriorClutterForgeTracksBakeAtlasAndBlackBoxNativeArrays()
        {
            string forge = ReadProjectFile(InteriorClutterForgePath);
            string support = ReadProjectFile(InteriorClutterForgeSupportPath);
            string jobs = ReadProjectFile(InteriorClutterForgeJobsPath);
            string combined = forge + support + jobs;

            StringAssert.Contains("using System.Reflection;", forge);
            StringAssert.Contains("private const string NativeMemorySentinelTypeName = \"Hecton8.Core.NativeMemorySentinel\";", forge);
            StringAssert.Contains("internal static NativeArray<T> AllocateTrackedNativeArray<T>", forge);
            StringAssert.Contains("DisposeTrackedNativeArray<T>", forge);
            StringAssert.Contains("private static void RegisterTrackedNativeArray<T>", forge);
            StringAssert.Contains("private static void UnregisterTrackedNativeArray(IntPtr trackedPointer)", forge);
            StringAssert.Contains("sentinelType.GetMethod(\"RegisterNativeArray\", BindingFlags.Public | BindingFlags.Static)", forge);
            StringAssert.Contains("sentinelType.GetMethod(\"UnregisterPointer\", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(IntPtr) }, null)", forge);
            StringAssert.Contains("method.Invoke(null, new object[] { trackedPointer })", forge);
            StringAssert.Contains("InteriorClutterForge.AllocateTrackedNativeArray<InteriorClutterTelemetryEntry>", support);
            StringAssert.Contains("InteriorClutterForge.AllocateTrackedNativeArray<InteriorClutterAtlasRect>", support);
            StringAssert.Contains("InteriorClutterForge.DisposeTrackedNativeArray(ref pixels)", support);
            StringAssert.Contains("InteriorClutterForge.AllocateTrackedNativeArray<VertexAttributeDescriptor>", jobs);
            StringAssert.Contains("InteriorClutterForge.DisposeTrackedNativeArray(ref layout)", jobs);
            Assert.AreEqual(1, CountOccurrences(forge, "new NativeArray<T>("), "Interior clutter allocations must stay centralized in InteriorClutterForge.AllocateTrackedNativeArray.");

            StringAssert.DoesNotContain("using Hecton8.Core;", forge);
            StringAssert.DoesNotContain("new NativeArray<InteriorClutterSourceVertex>", combined);
            StringAssert.DoesNotContain("new NativeArray<int>", combined);
            StringAssert.DoesNotContain("new NativeArray<InteriorClutterSegment>", combined);
            StringAssert.DoesNotContain("new NativeArray<InteriorClutterRawVertex>", combined);
            StringAssert.DoesNotContain("new NativeArray<uint>", combined);
            StringAssert.DoesNotContain("new NativeArray<InteriorClutterTelemetryEntry>", combined);
            StringAssert.DoesNotContain("new NativeArray<byte>", combined);
            StringAssert.DoesNotContain("new NativeArray<InteriorClutterAtlasRect>", combined);
            StringAssert.DoesNotContain("new NativeArray<InteriorClutterAtlasColor>", combined);
            StringAssert.DoesNotContain("new NativeArray<VertexAttributeDescriptor>", combined);
            StringAssert.DoesNotContain("sourceVertices.Dispose()", combined);
            StringAssert.DoesNotContain("segmentByVertex.Dispose()", combined);
            StringAssert.DoesNotContain("nativeSegments.Dispose()", combined);
            StringAssert.DoesNotContain("lod0Raw.Dispose()", combined);
            StringAssert.DoesNotContain("lod1Raw.Dispose()", combined);
            StringAssert.DoesNotContain("lod2Raw.Dispose()", combined);
            StringAssert.DoesNotContain("raw.Dispose()", combined);
            StringAssert.DoesNotContain("packed.Dispose()", combined);
            StringAssert.DoesNotContain("indices.Dispose()", combined);
            StringAssert.DoesNotContain("_ring.Dispose()", combined);
            StringAssert.DoesNotContain("bytes.Dispose()", combined);
            StringAssert.DoesNotContain("nativeRects.Dispose()", combined);
            StringAssert.DoesNotContain("nativeColors.Dispose()", combined);
            StringAssert.DoesNotContain("pixels.Dispose()", combined);
            StringAssert.DoesNotContain("layout.Dispose()", combined);
        }

        [Test]
        public void BioForgeGeneratorTracksNativeArraysAndListsThroughSentinel()
        {
            string source = ReadProjectFile(BioForgeGeneratorPath);

            StringAssert.Contains("using Hecton8.Core;", source);
            StringAssert.Contains("private static NativeArray<T> AllocateTrackedNativeArray<T>", source);
            StringAssert.Contains("private static NativeList<T> AllocateTrackedNativeList<T>", source);
            StringAssert.Contains("DisposeTrackedNativeArray<T>", source);
            StringAssert.Contains("private static void DisposeTrackedNativeList<T>", source);
            StringAssert.Contains("NativeMemorySentinel.RegisterNativeArray(array, NativeMemoryOwner, label, ResolveNativeAllocationLifetime(allocator))", source);
            StringAssert.Contains("NativeMemorySentinel.RegisterNativeListInstance(list, NativeMemoryOwner, label, ResolveNativeAllocationLifetime(allocator))", source);
            StringAssert.Contains("NativeMemorySentinel.UnregisterPointer(trackedPointer)", source);
            StringAssert.Contains("NativeMemorySentinel.Unregister(sentinelId)", source);
            StringAssert.Contains("DisposeTrackedNativeList(ref branches, ref branchesSentinelId)", source);
            StringAssert.Contains("DisposeTrackedNativeList(ref stateStack, ref stateStackSentinelId)", source);
            StringAssert.DoesNotContain("NativeMemorySentinel.RegisterNativeList(", source);
            StringAssert.DoesNotContain("NativeMemorySentinel.UnregisterNativeList", source);
            Assert.AreEqual(1, CountOccurrences(source, "new NativeArray<T>("), "BioForge NativeArray allocations must stay centralized in AllocateTrackedNativeArray.");
            Assert.AreEqual(1, CountOccurrences(source, "new NativeList<T>("), "BioForge NativeList allocations must stay centralized in AllocateTrackedNativeList.");

            StringAssert.DoesNotContain("new NativeArray<BioForgeBranch>", source);
            StringAssert.DoesNotContain("new NativeArray<float>", source);
            StringAssert.DoesNotContain("new NativeArray<int>", source);
            StringAssert.DoesNotContain("new NativeArray<BioForgeMeshVertex>", source);
            StringAssert.DoesNotContain("new NativeList<Matrix4x4>", source);
            StringAssert.DoesNotContain("new NativeList<BioForgeBranch>", source);
            StringAssert.DoesNotContain("new NativeList<BioForgeRawVertex>", source);
            StringAssert.DoesNotContain("new NativeList<TurtleState>", source);
            StringAssert.DoesNotContain("emptyBranches.Dispose()", source);
            StringAssert.DoesNotContain("density.Dispose()", source);
            StringAssert.DoesNotContain("rawVertices.Dispose()", source);
            StringAssert.DoesNotContain("overflow.Dispose()", source);
            StringAssert.DoesNotContain("bakedVertices.Dispose()", source);
            StringAssert.DoesNotContain("outputVertices.Dispose()", source);
            StringAssert.DoesNotContain("stateStack.Dispose()", source);
            StringAssert.DoesNotContain("branches.Dispose()", source);
            StringAssert.DoesNotContain("branchMatrices.Dispose()", source);
        }

        [Test]
        public void HlodImpostorBakerTracksCaptureScratchAndPngBytesThroughSentinel()
        {
            string source = ReadProjectFile(HlodImpostorBakerPath);

            StringAssert.Contains("using Hecton8.Core;", source);
            StringAssert.Contains("private const string NativeMemoryOwner = nameof(HectonOctahedralImpostorBaker);", source);
            StringAssert.Contains("private static NativeArray<T> AllocateTrackedNativeArray<T>", source);
            StringAssert.Contains("private static void RegisterTrackedNativeArray<T>", source);
            StringAssert.Contains("DisposeTrackedNativeArray<T>", source);
            StringAssert.Contains("NativeMemorySentinel.RegisterNativeArray(array, NativeMemoryOwner, label, lifetime)", source);
            StringAssert.Contains("NativeMemorySentinel.UnregisterPointer(trackedPointer)", source);
            StringAssert.Contains("RegisterTrackedNativeArray(ref png, NativeAllocationLifetime.TempJob, nameof(png))", source);
            StringAssert.Contains("DisposeTrackedNativeArray(ref records)", source);
            StringAssert.Contains("DisposeTrackedNativeArray(ref png)", source);
            Assert.AreEqual(1, CountOccurrences(source, "new NativeArray<T>("), "HLOD impostor NativeArray allocations must stay centralized in AllocateTrackedNativeArray.");

            StringAssert.DoesNotContain("new NativeArray<HlodImpostorMockPoint>", source);
            StringAssert.DoesNotContain("new NativeArray<HlodImpostorCaptureAngleRecord>", source);
            StringAssert.DoesNotContain("new NativeArray<byte>", source);
            StringAssert.DoesNotContain("points.Dispose()", source);
            StringAssert.DoesNotContain("records.Dispose()", source);
            StringAssert.DoesNotContain("png.Dispose()", source);
        }

        [Test]
        public void HlodImpostorForgeWindowTracksCsvAndPreviewNativeArraysThroughSentinel()
        {
            string source = ReadProjectFile(HlodImpostorForgeWindowPath);

            StringAssert.Contains("using Hecton8.Core;", source);
            StringAssert.Contains("private const string NativeMemoryOwner = nameof(HlodImpostorForgeWindow);", source);
            StringAssert.Contains("private static NativeArray<T> AllocateTrackedNativeArray<T>", source);
            StringAssert.Contains("DisposeTrackedNativeArray<T>", source);
            StringAssert.Contains("NativeMemorySentinel.RegisterNativeArray(array, NativeMemoryOwner, label, ResolveNativeAllocationLifetime(allocator))", source);
            StringAssert.Contains("NativeMemorySentinel.UnregisterPointer(trackedPointer)", source);
            StringAssert.Contains("DisposeTrackedNativeArray(ref bytes)", source);
            StringAssert.Contains("DisposeTrackedNativeArray(ref records)", source);
            Assert.AreEqual(1, CountOccurrences(source, "new NativeArray<T>("), "HLOD forge window native buffers must stay centralized in AllocateTrackedNativeArray.");

            StringAssert.DoesNotContain("new NativeArray<byte>", source);
            StringAssert.DoesNotContain("new NativeArray<HlodImpostorCaptureAngleRecord>", source);
            StringAssert.DoesNotContain("bytes.Dispose()", source);
            StringAssert.DoesNotContain("records.Dispose()", source);
        }

        [Test]
        public void HydraulicErosionForgeTracksBakeAndWeatheringNativeArraysThroughSentinel()
        {
            string baker = ReadProjectFile(HydraulicErosionForgeBakerPath);
            string weathering = ReadProjectFile(HydraulicErosionWeatheringCsvPath);

            StringAssert.Contains("private static NativeArray<T> NewTrackedArray<T>", baker);
            StringAssert.Contains("DisposeTrackedArray<T>", baker);
            StringAssert.Contains("NativeMemorySentinel.RegisterNativeArray(array, NativeMemoryOwner, label, lifetime)", baker);
            StringAssert.Contains("NativeMemorySentinel.UnregisterPointer(trackedPointer)", baker);
            StringAssert.Contains("using Hecton8.Core;", weathering);
            StringAssert.Contains("private const string NativeMemoryOwner = nameof(HydraulicErosionWeatheringCsv);", weathering);
            StringAssert.Contains("bytes = AllocateTrackedArray<byte>(length, Allocator.Temp, NativeArrayOptions.UninitializedMemory, CsvBytesLabel, NativeAllocationLifetime.Temp)", weathering);
            StringAssert.Contains("DisposeTrackedArray(ref bytes)", weathering);
            StringAssert.Contains("NativeMemorySentinel.RegisterNativeArray(array, NativeMemoryOwner, label, lifetime)", weathering);
            StringAssert.Contains("NativeMemorySentinel.UnregisterPointer(trackedPointer)", weathering);
            Assert.AreEqual(1, CountOccurrences(baker, "new NativeArray<T>("), "Hydraulic erosion bake allocations must stay centralized in NewTrackedArray.");
            Assert.AreEqual(1, CountOccurrences(weathering, "new NativeArray<T>("), "Hydraulic erosion weathering CSV byte storage must stay centralized in AllocateTrackedArray.");

            StringAssert.DoesNotContain("new NativeArray<byte>", weathering);
            StringAssert.DoesNotContain("bytes.Dispose()", weathering);
        }

        [Test]
        public void AITextureControlMapBakerSuiteTracksAsyncReadbackPngAndCsvNativeArrays()
        {
            string helper = ReadProjectFile(AITextureNativeMemoryPath);
            string baker = ReadProjectFile(AITextureControlMapBakerPath);
            string blackBox = ReadProjectFile(AITextureBakeBlackBoxPath);
            string mock = ReadProjectFile(AITextureMockMeshJobsPath);
            string profiles = ReadProjectFile(AITextureProfileCsvPath);
            string combined = helper + baker + blackBox + mock + profiles;

            StringAssert.Contains("internal static class AITextureNativeMemory", helper);
            StringAssert.Contains("internal static NativeArray<T> AllocateArray<T>", helper);
            StringAssert.Contains("internal static void RegisterArray<T>", helper);
            StringAssert.Contains("DisposeArray<T>", helper);
            StringAssert.Contains("NativeMemorySentinel.RegisterNativeArray(array, safeOwner, safeLabel, lifetime)", helper);
            StringAssert.Contains("NativeMemorySentinel.UnregisterPointer(trackedPointer)", helper);
            StringAssert.Contains("context.ReadbackData = AITextureNativeMemory.AllocateArray<byte>", baker);
            StringAssert.Contains("AITextureNativeMemory.RegisterArray(ref pngBytes", baker);
            StringAssert.Contains("AITextureNativeMemory.DisposeArray(ref completion.PngBytes)", baker);
            StringAssert.Contains("_ring = AITextureNativeMemory.AllocateArray<AITextureBakeTelemetryEntry>", blackBox);
            StringAssert.Contains("AITextureNativeMemory.AllocateArray<AITextureBakeVertex>", mock);
            StringAssert.Contains("AITextureNativeMemory.AllocateArray<uint>", mock);
            StringAssert.Contains("bytes = AITextureNativeMemory.AllocateArray<byte>", profiles);
            Assert.AreEqual(1, CountOccurrences(helper, "new NativeArray<T>("), "AI texture NativeArray construction must stay centralized in AITextureNativeMemory.");

            StringAssert.DoesNotContain("new NativeArray<AITextureBakeVertex>", combined);
            StringAssert.DoesNotContain("new NativeArray<uint>", combined);
            StringAssert.DoesNotContain("new NativeArray<byte>", combined);
            StringAssert.DoesNotContain("new NativeArray<AITextureBakeTelemetryEntry>", combined);
            StringAssert.DoesNotContain("vertices.Dispose()", combined);
            StringAssert.DoesNotContain("indices.Dispose()", combined);
            StringAssert.DoesNotContain("bytes.Dispose()", combined);
            StringAssert.DoesNotContain("_ring.Dispose()", combined);
            StringAssert.DoesNotContain("context.ReadbackData.Dispose()", combined);
            StringAssert.DoesNotContain("pngBytes.Dispose()", combined);
            StringAssert.DoesNotContain("completion.PngBytes.Dispose()", combined);
        }

        [Test]
        public void WorldProceduralProxySceneBuilderTracksRaycastSnapBuffersThroughSentinel()
        {
            string source = ReadProjectFile(WorldProceduralProxySceneBuilderPath);

            StringAssert.Contains("using Hecton8.Core;", source);
            StringAssert.Contains("private const string NativeMemoryOwner = nameof(WorldProceduralProxySceneBuilder);", source);
            StringAssert.Contains("private static NativeArray<T> AllocateTrackedNativeArray<T>", source);
            StringAssert.Contains("DisposeTrackedNativeArray<T>", source);
            StringAssert.Contains("NativeMemorySentinel.RegisterNativeArray(array, NativeMemoryOwner, label, ResolveNativeAllocationLifetime(allocator))", source);
            StringAssert.Contains("NativeMemorySentinel.UnregisterPointer(trackedPointer)", source);
            StringAssert.Contains("DisposeTrackedNativeArray(ref commands)", source);
            StringAssert.Contains("DisposeTrackedNativeArray(ref hits)", source);
            Assert.AreEqual(1, CountOccurrences(source, "new NativeArray<T>("), "Procedural proxy raycast buffers must stay centralized in AllocateTrackedNativeArray.");

            StringAssert.DoesNotContain("new NativeArray<RaycastCommand>", source);
            StringAssert.DoesNotContain("new NativeArray<RaycastHit>", source);
            StringAssert.DoesNotContain("commands.Dispose()", source);
            StringAssert.DoesNotContain("hits.Dispose()", source);
        }

        [Test]
        public void TexturePackerTracksTempJobScratchAndPersistentBlackBoxArrays()
        {
            string source = ReadProjectFile(TexturePackerPath);

            StringAssert.Contains("private static NativeArray<T> AllocateTrackedTempJobArray<T>", source);
            StringAssert.Contains("DisposeTrackedNativeArray<T>", source);
            StringAssert.Contains("NativeMemorySentinel.RegisterNativeArray(array, NativeMemoryOwner, label, NativeAllocationLifetime.TempJob)", source);
            StringAssert.Contains("NativeMemorySentinel.UnregisterPointer(trackedPointer)", source);
            StringAssert.Contains("_ringSentinelId = NativeMemorySentinel.RegisterNativeArray", source);
            StringAssert.Contains("RingLabel", source);
            StringAssert.Contains("NativeMemorySentinel.Unregister(_ringSentinelId)", source);
            Assert.AreEqual(1, CountOccurrences(source, "Allocator.TempJob"), "Texture packer TempJob allocations must stay centralized in AllocateTrackedTempJobArray.");
            Assert.AreEqual(1, CountOccurrences(source, "new NativeArray<T>("), "Texture packer scratch allocations must use the tracked generic helper.");
            Assert.AreEqual(1, CountOccurrences(source, "new NativeArray<TexturePackerTelemetryEntry>"), "The only direct concrete allocation is the persistent black-box ring.");
            Assert.AreEqual(1, CountOccurrences(source, "NativeAllocationLifetime.TempJob"), "TempJob sentinel lifetime must stay centralized.");

            StringAssert.DoesNotContain("new NativeArray<Color32>", source);
            StringAssert.DoesNotContain("aoPixels.Dispose()", source);
            StringAssert.DoesNotContain("roughnessPixels.Dispose()", source);
            StringAssert.DoesNotContain("metallicPixels.Dispose()", source);
            StringAssert.DoesNotContain("albedoPixels.Dispose()", source);
            StringAssert.DoesNotContain("armPixels.Dispose()", source);
            StringAssert.DoesNotContain("normalPixels.Dispose()", source);
            StringAssert.DoesNotContain("previousArm.Dispose()", source);
            StringAssert.DoesNotContain("previousNormal.Dispose()", source);
            StringAssert.DoesNotContain("previous.Dispose()", source);
            StringAssert.DoesNotContain("ao.Dispose()", source);
            StringAssert.DoesNotContain("normals.Dispose()", source);
        }

        [Test]
        public void TexturePackerWindowTracksPreviewAndCsvNativeArraysThroughSentinel()
        {
            string asmdef = ReadProjectFile(TexturePackerAsmdefPath);
            string source = ReadProjectFile(TexturePackerWindowPath);

            StringAssert.Contains("\"Hecton8.Core\"", asmdef);
            StringAssert.Contains("using Hecton8.Core;", source);
            StringAssert.Contains("internal static class TexturePackerEditorNativeMemory", source);
            StringAssert.Contains("internal static NativeArray<T> AllocateArray<T>", source);
            StringAssert.Contains("DisposeArray<T>", source);
            StringAssert.Contains("NativeMemorySentinel.RegisterNativeArray(array, owner, label, ResolveNativeAllocationLifetime(allocator))", source);
            StringAssert.Contains("NativeMemorySentinel.UnregisterPointer(trackedPointer)", source);
            StringAssert.Contains("TexturePackerEditorNativeMemory.AllocateArray<Color32>", source);
            StringAssert.Contains("TexturePackerEditorNativeMemory.AllocateArray<byte>", source);
            StringAssert.Contains("TexturePackerEditorNativeMemory.DisposeArray(ref ao)", source);
            StringAssert.Contains("TexturePackerEditorNativeMemory.DisposeArray(ref bytes)", source);
            Assert.AreEqual(1, CountOccurrences(source, "new NativeArray<T>("), "Texture packer window NativeArray construction must stay centralized in TexturePackerEditorNativeMemory.");

            StringAssert.DoesNotContain("new NativeArray<Color32>", source);
            StringAssert.DoesNotContain("new NativeArray<byte>", source);
            StringAssert.DoesNotContain("ao.Dispose()", source);
            StringAssert.DoesNotContain("roughness.Dispose()", source);
            StringAssert.DoesNotContain("metallic.Dispose()", source);
            StringAssert.DoesNotContain("bytes.Dispose()", source);
        }

        [Test]
        public void HabitatDamageBakePipelineTracksTempScratchThroughSentinelBridge()
        {
            string source = ReadProjectFile(HabitatDamageBakePipelinePath);

            StringAssert.Contains("internal static class HabitatDamageNativeMemorySentinelBridge", source);
            StringAssert.Contains("internal static void UnregisterPointer(IntPtr trackedPointer)", source);
            StringAssert.Contains("private static NativeArray<T> AllocateTrackedNativeArray<T>", source);
            StringAssert.Contains("DisposeTrackedNativeArray<T>", source);
            StringAssert.Contains("HabitatDamageNativeMemorySentinelBridge.RegisterNativeArray(", source);
            StringAssert.Contains("HabitatDamageNativeMemorySentinelBridge.UnregisterPointer(trackedPointer)", source);
            StringAssert.Contains("HabitatDamageNativeMemorySentinelBridge.Unregister(_telemetryRingSentinelId)", source);
            Assert.AreEqual(1, CountOccurrences(source, "new NativeArray<T>("), "Habitat bake scratch allocations must stay centralized in AllocateTrackedNativeArray.");
            Assert.AreEqual(1, CountOccurrences(source, "new NativeArray<HabitatDamageBakeTelemetryEntry>"), "The only direct concrete allocation is the persistent bake telemetry ring.");

            StringAssert.DoesNotContain("new NativeArray<byte>", source);
            StringAssert.DoesNotContain("new NativeArray<HabitatDamageSourceVertex>", source);
            StringAssert.DoesNotContain("new NativeArray<HabitatDamageWorkingVertex>", source);
            StringAssert.DoesNotContain("new NativeArray<uint>", source);
            StringAssert.DoesNotContain("new NativeArray<HabitatDamageHullDTO>", source);
            StringAssert.DoesNotContain("new NativeArray<HabitatDamageBakedVertex>", source);
            StringAssert.DoesNotContain("new NativeArray<HabitatDamageIndexRangeDTO>", source);
            StringAssert.DoesNotContain("indexRanges.Dispose()", source);
            StringAssert.DoesNotContain("sourceVertices.Dispose()", source);
            StringAssert.DoesNotContain("workingVertices.Dispose()", source);
            StringAssert.DoesNotContain("sourceIndices.Dispose()", source);
            StringAssert.DoesNotContain("outputIndices.Dispose()", source);
            StringAssert.DoesNotContain("hulls.Dispose()", source);
            StringAssert.DoesNotContain("packedVertices.Dispose()", source);
            StringAssert.DoesNotContain("vertices.Dispose()", source);
            StringAssert.DoesNotContain("bytes.Dispose()", source);
        }

        private static string ReadProjectFile(string projectRelativePath)
        {
            string fullPath = Path.Combine(Directory.GetCurrentDirectory(), projectRelativePath.Replace('/', Path.DirectorySeparatorChar));
            return File.ReadAllText(fullPath);
        }

        private static void AssertAsmdefReferencesCoreContracts(string projectRelativePath)
        {
            string asmdef = ReadProjectFile(projectRelativePath);
            StringAssert.Contains("\"Hecton8.Core\"", asmdef);
            StringAssert.Contains("\"Hecton8.Core.Contracts\"", asmdef);
        }

        private static void AssertAsmdefReferencesCoreContractsAndMemory(string projectRelativePath)
        {
            string asmdef = ReadProjectFile(projectRelativePath);
            StringAssert.Contains("\"Hecton8.Core\"", asmdef);
            StringAssert.Contains("\"Hecton8.Core.Contracts\"", asmdef);
            StringAssert.Contains("\"Hecton8.Core.Memory\"", asmdef);
        }

        private static int CountOccurrences(string haystack, string needle)
        {
            int count = 0;
            int searchStart = 0;
            while (searchStart < haystack.Length)
            {
                int index = haystack.IndexOf(needle, searchStart, StringComparison.Ordinal);
                if (index < 0)
                    return count;

                count++;
                searchStart = index + needle.Length;
            }

            return count;
        }
    }
}
