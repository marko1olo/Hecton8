using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Hecton8.Editor.ColliderOptimization1716;
using NUnit.Framework;
using UnityEngine;

namespace Hecton8.Tests.Editor
{
    public sealed class ColliderOptimizer1716EditTests
    {
        private const string EnginePath = "Assets/_Project/Editor/Physics/ColliderOptimizerEngine1716.cs";
        private const string CullingManagerPath = "Assets/_Project/Scripts/World/CullingManager.cs";
        private const string RuntimeBakerPath = "Assets/_Project/Scripts/Physics/RuntimePhysicsBaker1609.cs";
        private const string WorldGeneratorPath = "Assets/_Project/Scripts/HectonWorldGenerator.cs";
        private const string VoxelEnginePath = "Assets/_Project/Scripts/HectonVoxelEngine.cs";
        private const string VoxelVolumePath = "Assets/_Project/Scripts/HectonVoxelVolume.cs";
        private const string PhysicsSkinGeneratorPath = "Assets/_Project/Editor/HectonPhysicsSkinGenerator.cs";
        private const string FloraTopology1604Path = "Assets/_Project/Editor/Generators/Flora/FloraTopologyStudio1604.cs";
        private const string FloraTopology1711Path = "Assets/_Project/Editor/Generators/Flora/FloraTopologyStudio1711.cs";
        private const string RockSculptorPath = "Assets/_Project/Editor/Generators/Geology/RockSculptorEngine1713.cs";
        private const string RockSculptorAsmdefPath = "Assets/_Project/Editor/Generators/Geology/Hecton8.AbyssalGeology1606.Editor.asmdef";
        private const string ModuleArchitectPath = "Assets/_Project/Editor/Generators/Structures/ModuleArchitect1712.cs";
        private const string DeepReachStationFabricatorPath = "Assets/_Project/Editor/Generators/Structures/DeepReachStationFabricator.cs";
        private const string EquipmentPropBakerPath = "Assets/_Project/Editor/Generators/Interiors/EquipmentPropBaker1715.cs";
        private const string InteriorFinisherPath = "Assets/_Project/Editor/Generators/Interiors/InteriorFinisherStudio1608.cs";
        private const string GeologyForgePath = "Assets/_Project/Scripts/Editor/GeologyForge/GeologyForgeGenerator.cs";
        private const string GeologyForgeSelfAuditPath = "Assets/_Project/Scripts/Editor/GeologyForge/GeologyForgeSelfAudit.cs";
        private const string GeologyForgeAsmdefPath = "Assets/_Project/Scripts/Editor/GeologyForge/Hecton8.World.OfflineGeology.Editor.asmdef";
        private const string BioForgePath = "Assets/_Project/Scripts/Editor/ProceduralGen/BioForgeGenerator.cs";
        private const string ShallowsBioForgeBatchBakerPath = "Assets/_Project/Scripts/Editor/ProceduralGen/ShallowsBioForgeBatchBaker.cs";
        private const string BioForgeAsmdefPath = "Assets/_Project/Scripts/Editor/ProceduralGen/Hecton8.Editor.ProceduralGen.asmdef";
        private const string WorldProceduralGeologyFinalPath = "Assets/_Project/Scripts/Editor/WorldProceduralGeologyFinalAuthoring.cs";
        private const string HectonEditorAsmdefPath = "Assets/_Project/Scripts/Editor/Hecton8.Editor.asmdef";
        private const string HadalArchBakePipelinePath = "Assets/_Project/Scripts/World/OfflineHadalArchBaker/Editor/HadalArchBakePipeline.cs";
        private const string HadalArchBakeAsmdefPath = "Assets/_Project/Scripts/World/OfflineHadalArchBaker/Editor/Hecton8.World.OfflineHadalArchBaker.Editor.asmdef";
        private const string ContentAuthorityAssetPostprocessorPath = "Assets/_Project/Scripts/Core/Content/Editor/ContentAuthorityAssetPostprocessor.cs";
        private const string ContentAuthorityEditorAsmdefPath = "Assets/_Project/Scripts/Core/Content/Editor/Hecton8.Core.Content.Editor.asmdef";

        [Test]
        public void EngineOwnsOfflineProxyGenerationContract()
        {
            string source = ReadProjectFile(EnginePath);

            StringAssert.Contains("ColliderOptimizerEngine1716", source);
            StringAssert.Contains("ProxyMeshTriangleLimit = 200", source);
            StringAssert.Contains("MeshColliderFatalTriangleLimit = 500", source);
            StringAssert.Contains("COL_CompoundProxy_1716", source);
            StringAssert.Contains("COL_ConvexProxy_1716", source);
            StringAssert.Contains("SphereCollider", source);
            StringAssert.Contains("ShouldUseSphere", source);
            StringAssert.Contains("SphereCollidersGenerated", source);
            StringAssert.Contains("SerializeGeneratedProxyMeshes", source);
            StringAssert.Contains("AssetDatabase.CreateAsset(mesh, meshPath)", source);
            StringAssert.Contains("ValidatePrefabColliderBudget", source);
            StringAssert.Contains("ValidatePrefabAssetTopology", source);
            StringAssert.Contains("ValidateGeneratedColliderRoots", source);
            StringAssert.Contains("IsPrimaryVisualMeshCollider", source);
            StringAssert.Contains("IsPrimaryVisualMeshReference", source);
            StringAssert.Contains("NavMeshObstacle", source);
            StringAssert.Contains("material.frictionCombine = frictionCombine", source);
            StringAssert.Contains("PhysicsMaterialCombine.Minimum", source);
            Assert.IsFalse(source.Contains("DistanceCollider" + "Culler", StringComparison.Ordinal));
            Assert.IsFalse(source.Contains("ConfigureDistance" + "Culler", StringComparison.Ordinal));
        }

        [Test]
        public void PhysicsSkinGeneratorUses1716ConvexProxyContract()
        {
            string source = ReadProjectFile(PhysicsSkinGeneratorPath);
            string generateChunked = ExtractMethodBody(source, "private void GenerateChunked()");
            string applyToScene = ExtractMethodBody(source, "private bool ApplyToScene(GameObject target, Mesh resultMesh)");

            StringAssert.Contains("MaxProxyTriangles1716 = ColliderOptimizerEngine1716.ProxyMeshTriangleLimit", source);
            StringAssert.Contains("IntSlider(\"Target Triangles\", targetTriCount, 50, MaxProxyTriangles1716)", source);
            StringAssert.Contains("COL_ConvexProxy_1716", source);
            StringAssert.Contains("COL_CompoundProxy_1716", source);
            StringAssert.Contains("COL_Skin_", source);
            StringAssert.Contains("ValidateProxyTriangleBudget(result.tris.Count / 3, target.name)", source);
            StringAssert.Contains("ColliderOptimizerEngine1716.ValidateProxyMesh(resultMesh", applyToScene);
            StringAssert.Contains("ColliderOptimizerEngine1716.ValidatePrefabColliderBudget(target", applyToScene);
            StringAssert.Contains("StripTargetVisualMeshCollider(target);", applyToScene);
            StringAssert.Contains("mc.convex = true;", applyToScene);
            Assert.IsFalse(source.Contains("mc.convex = false", StringComparison.Ordinal));
            StringAssert.Contains("Chunked non-convex MeshCollider generation is disabled by 1716", generateChunked);
            Assert.IsFalse(generateChunked.Contains("AddComponent<MeshCollider>", StringComparison.Ordinal));
            Assert.IsFalse(generateChunked.Contains("MeshColliderCookingOptions", StringComparison.Ordinal));
            Assert.IsFalse(generateChunked.Contains("sharedMesh = chunkMesh", StringComparison.Ordinal));
            StringAssert.Contains("AddComponent<BoxCollider>()", generateChunked);
        }

        [Test]
        public void FirstPartyGeneratorsGateSavedPrefabsThrough1716Validation()
        {
            string floraTopology1604 = ReadProjectFile(FloraTopology1604Path);
            string floraTopology1711 = ReadProjectFile(FloraTopology1711Path);
            string rockSculptor = ReadProjectFile(RockSculptorPath);
            string rockSculptorAsmdef = ReadProjectFile(RockSculptorAsmdefPath);
            string moduleArchitect = ReadProjectFile(ModuleArchitectPath);
            string deepReachStationFabricator = ReadProjectFile(DeepReachStationFabricatorPath);
            string equipmentBaker = ReadProjectFile(EquipmentPropBakerPath);
            string interiorFinisher = ReadProjectFile(InteriorFinisherPath);
            string geologyForge = ReadProjectFile(GeologyForgePath);
            string geologyForgeSelfAudit = ReadProjectFile(GeologyForgeSelfAuditPath);
            string geologyForgeAsmdef = ReadProjectFile(GeologyForgeAsmdefPath);
            string bioForge = ReadProjectFile(BioForgePath);
            string shallowsBioForgeBatchBaker = ReadProjectFile(ShallowsBioForgeBatchBakerPath);
            string bioForgeAsmdef = ReadProjectFile(BioForgeAsmdefPath);
            string worldGeologyFinal = ReadProjectFile(WorldProceduralGeologyFinalPath);
            string hectonEditorAsmdef = ReadProjectFile(HectonEditorAsmdefPath);
            string hadalArchBakePipeline = ReadProjectFile(HadalArchBakePipelinePath);
            string hadalArchBakeAsmdef = ReadProjectFile(HadalArchBakeAsmdefPath);
            string contentAuthorityAssetPostprocessor = ReadProjectFile(ContentAuthorityAssetPostprocessorPath);
            string contentAuthorityEditorAsmdef = ReadProjectFile(ContentAuthorityEditorAsmdefPath);

            StringAssert.Contains("ColliderOptimizerEngine1716.ValidatePrefabColliderBudget(root", floraTopology1604);
            StringAssert.Contains("ColliderOptimizerEngine1716.ValidatePrefabColliderBudget(root", floraTopology1711);
            StringAssert.Contains("ColliderOptimizerEngine1716.ValidatePrefabColliderBudget(root", rockSculptor);
            StringAssert.Contains("ColliderOptimizerEngine1716.ValidatePrefabColliderBudget(root", moduleArchitect);
            StringAssert.Contains("ColliderOptimizerEngine1716.ValidatePrefabColliderBudget(root", deepReachStationFabricator);
            StringAssert.Contains("ColliderOptimizerEngine1716.ValidatePrefabColliderBudget(root", equipmentBaker);
            StringAssert.Contains("ColliderOptimizerEngine1716.ValidatePrefabColliderBudget(root", interiorFinisher);
            StringAssert.Contains("ColliderOptimizerEngine1716.ValidatePrefabColliderBudget(root", geologyForge);
            StringAssert.Contains("ColliderOptimizerEngine1716.ValidatePrefabColliderBudget(root", bioForge);
            StringAssert.Contains("ColliderOptimizerEngine1716.ValidatePrefabColliderBudget(root", worldGeologyFinal);
            StringAssert.Contains("ColliderOptimizerEngine1716.ValidatePrefabColliderBudget(root", hadalArchBakePipeline);
            StringAssert.Contains("ColliderOptimizerEngine1716.ValidatePrefabAssetTopology(prefabPath", floraTopology1604);
            StringAssert.Contains("ColliderOptimizerEngine1716.ValidatePrefabAssetTopology(path", floraTopology1711);
            StringAssert.Contains("ColliderOptimizerEngine1716.ValidatePrefabAssetTopology(prefabPath", rockSculptor);
            StringAssert.Contains("ColliderOptimizerEngine1716.ValidatePrefabAssetTopology(prefabPath", moduleArchitect);
            StringAssert.Contains("ColliderOptimizerEngine1716.ValidatePrefabAssetTopology(prefabPath", deepReachStationFabricator);
            StringAssert.Contains("ColliderOptimizerEngine1716.ValidatePrefabAssetTopology(prefabPath", equipmentBaker);
            StringAssert.Contains("ColliderOptimizerEngine1716.ValidatePrefabAssetTopology(prefabPath", interiorFinisher);
            StringAssert.Contains("ColliderOptimizerEngine1716.ValidatePrefabAssetTopology(prefabPath", geologyForge);
            StringAssert.Contains("ColliderOptimizerEngine1716.ValidatePrefabAssetTopology(prefabPath", bioForge);
            StringAssert.Contains("ColliderOptimizerEngine1716.ValidatePrefabAssetTopology(prefabPath", worldGeologyFinal);
            StringAssert.Contains("ColliderOptimizerEngine1716.ValidatePrefabAssetTopology(prefabPath", hadalArchBakePipeline);
            StringAssert.Contains("1716 collider validation failed before save", floraTopology1604);
            StringAssert.Contains("Collider topology rejected before prefab save", floraTopology1711);
            StringAssert.Contains("1716 collider validation failed before save", rockSculptor);
            StringAssert.Contains("1716 collider validation failed before save", moduleArchitect);
            StringAssert.Contains("Collider topology rejected before station prefab save", deepReachStationFabricator);
            StringAssert.Contains("1716 collider validation failed before save", equipmentBaker);
            StringAssert.Contains("Collider topology rejected before interior prefab save", interiorFinisher);
            StringAssert.Contains("1716 collider validation failed before save", geologyForge);
            StringAssert.Contains("1716 collider validation failed before save", bioForge);
            StringAssert.Contains("1716 collider validation failed before save", worldGeologyFinal);
            StringAssert.Contains("1716 collider validation failed before save", hadalArchBakePipeline);
            StringAssert.Contains("1716 collider validation failed after save", floraTopology1604);
            StringAssert.Contains("Collider topology rejected after prefab save", floraTopology1711);
            StringAssert.Contains("1716 collider validation failed after save", rockSculptor);
            StringAssert.Contains("1716 collider validation failed after save", moduleArchitect);
            StringAssert.Contains("Collider topology rejected after station prefab save", deepReachStationFabricator);
            StringAssert.Contains("1716 collider validation failed after save", equipmentBaker);
            StringAssert.Contains("Collider topology rejected after interior prefab save", interiorFinisher);
            StringAssert.Contains("1716 collider validation failed after save", geologyForge);
            StringAssert.Contains("1716 collider validation failed after save", bioForge);
            StringAssert.Contains("1716 collider validation failed after save", worldGeologyFinal);
            StringAssert.Contains("1716 collider validation failed after save", hadalArchBakePipeline);
            StringAssert.Contains("GeneratedCompoundRootName = \"COL_CompoundProxy_1716\"", floraTopology1604);
            StringAssert.Contains("new GameObject(GeneratedCompoundRootName)", floraTopology1604);
            StringAssert.Contains("colliderObject.AddComponent<SphereCollider>()", floraTopology1604);
            StringAssert.Contains("collider.isTrigger = preset == FloraTopologyPreset.ThermalTubeWorm", floraTopology1604);
            StringAssert.Contains("ResolveFloraCollisionLayer(collider.isTrigger)", floraTopology1604);
            Assert.IsFalse(floraTopology1604.Contains("root.AddComponent<SphereCollider>()", StringComparison.Ordinal));
            StringAssert.Contains("new GameObject(\"COL_ConvexProxy_1716\")", geologyForge);
            StringAssert.Contains("prefab.transform.Find(\"COL_ConvexProxy_1716\")", geologyForgeSelfAudit);
            StringAssert.Contains("PREFAB_MISSING_COL_CONVEX_PROXY_1716", geologyForgeSelfAudit);
            Assert.IsFalse(geologyForgeSelfAudit.Contains("prefab.GetComponent<MeshCollider>()", StringComparison.Ordinal));
            StringAssert.Contains("new GameObject(\"COL_CompoundProxy_1716\")", bioForge);
            StringAssert.Contains("AddComponent<BoxCollider>()", bioForge);
            Assert.IsFalse(bioForge.Contains("AddComponent<MeshCollider>()", StringComparison.Ordinal));
            Assert.IsFalse(bioForge.Contains("collider.sharedMesh = lodMeshes", StringComparison.Ordinal));
            StringAssert.Contains("\"COL_CompoundProxy_1716\", StringComparison.Ordinal", shallowsBioForgeBatchBaker);
            Assert.IsFalse(shallowsBioForgeBatchBaker.Contains("\"Collision_LOD2\", StringComparison.Ordinal", StringComparison.Ordinal));
            StringAssert.Contains("new GameObject(\"COL_ConvexProxy_1716\")", worldGeologyFinal);
            StringAssert.Contains("new GameObject(\"COL_CompoundProxy_1716\")", hadalArchBakePipeline);
            StringAssert.Contains("CreateCompoundArchColliderRoot", hadalArchBakePipeline);
            StringAssert.Contains("AddBoxCollider(colliderRoot", hadalArchBakePipeline);
            Assert.IsFalse(hadalArchBakePipeline.Contains("AddComponent<MeshCollider>()", StringComparison.Ordinal));
            Assert.IsFalse(hadalArchBakePipeline.Contains("collider.sharedMesh = lod2", StringComparison.Ordinal));
            Assert.IsFalse(hadalArchBakePipeline.Contains("collider.sharedMesh = lod2 != null ? lod2 : lod1 != null ? lod1 : lod0", StringComparison.Ordinal));
            Assert.IsFalse(hadalArchBakePipeline.Contains("collider.convex = false", StringComparison.Ordinal));
            StringAssert.Contains("new GameObject(ColliderOptimizerEngine1716.GeneratedConvexRootName)", contentAuthorityAssetPostprocessor);
            StringAssert.Contains("COL_ContentProxyHull_1716", contentAuthorityAssetPostprocessor);
            StringAssert.Contains("ColliderOptimizerEngine1716.ValidateProxyMesh(mesh", contentAuthorityAssetPostprocessor);
            StringAssert.Contains("ColliderOptimizerEngine1716.ValidatePrefabColliderBudget(root", contentAuthorityAssetPostprocessor);
            StringAssert.Contains("TryCollectLocalBoxBounds(root.transform, boxes", contentAuthorityAssetPostprocessor);
            Assert.IsFalse(contentAuthorityAssetPostprocessor.Contains("new GameObject(\"GEN_PhysicsProxyHull\")", StringComparison.Ordinal));
            Assert.IsFalse(contentAuthorityAssetPostprocessor.Contains("MeshAssetFolder + \"/\" + safeName + \"_PhysicsProxyHull.asset\"", StringComparison.Ordinal));
            StringAssert.Contains("Hecton8.Project.Editor", rockSculptorAsmdef);
            StringAssert.Contains("Hecton8.Project.Editor", geologyForgeAsmdef);
            StringAssert.Contains("Hecton8.Project.Editor", bioForgeAsmdef);
            StringAssert.Contains("Hecton8.Project.Editor", hectonEditorAsmdef);
            StringAssert.Contains("Hecton8.Project.Editor", hadalArchBakeAsmdef);
            StringAssert.Contains("Hecton8.Project.Editor", contentAuthorityEditorAsmdef);
        }

        [Test]
        public void WorldProceduralGeologyFinalAuthoringPreservesUpdatedMeshAssetShape()
        {
            string source = ReadProjectFile(WorldProceduralGeologyFinalPath);
            string saveMesh = ExtractMethodBody(source, "private static Mesh SaveMesh(");
            string copyMeshData = ExtractMethodBody(source, "private static void CopyMeshData(");

            StringAssert.Contains("CopyMeshData(mesh, existing)", saveMesh);
            StringAssert.Contains("ReleaseTemporaryMesh(mesh)", saveMesh);
            StringAssert.Contains("target.indexFormat = source.indexFormat", copyMeshData);
            StringAssert.Contains("target.subMeshCount = subMeshCount", copyMeshData);
            StringAssert.Contains("source.GetTriangles(triangles, subMeshIndex)", copyMeshData);
            StringAssert.Contains("target.SetTriangles(triangles, subMeshIndex, false)", copyMeshData);
        }

        [Test]
        public void ModuleArchitectCutsVerticalSocketVisualsAndColliderSlabs()
        {
            string source = ReadProjectFile(ModuleArchitectPath);
            string beveledBox = ExtractMethodBody(source, "private static void AddBeveledBox(");
            string collisionProxies = ExtractMethodBody(source, "private static void AddCollisionProxies(");
            string ySlabProxy = ExtractMethodBody(source, "private static void AddYSlabProxy(");

            // Argument-order invariants are asserted against a whitespace-collapsed copy, not the raw
            // source. Three of the assertions below failed on REFLOW alone: the Airlock and
            // VerticalShaft specs grew a socket-lane override and a comment, so their constructor
            // arguments moved onto separate lines while every value stayed identical. A test that
            // reports a defect because an argument list wrapped is a test that gets suppressed.
            // Collapsing whitespace keeps the ordering guarantee and drops the formatting coupling.
            string flat = System.Text.RegularExpressions.Regex.Replace(source, @"\s+", " ");

            StringAssert.Contains("H8_A1712_Airlock_01", source);
            StringAssert.Contains("BuildableFamily.Structure, -18f, 15, false, true", flat);
            StringAssert.Contains("H8_A1712_ReactorRoom_01", source);
            StringAssert.Contains("BuildableFamily.Utility, 450f, 5, true, false", flat);
            StringAssert.Contains("H8_A1712_VerticalShaft_01", source);
            StringAssert.Contains("Vertical = Top | Bottom", source);

            // Bottom only, and the absence of Top is the point. SocketMask.Vertical is Top | Bottom,
            // so the previous spec cut a ceiling opening in both the visual mesh and the collider for
            // a socket that has no authored partner anywhere in the kit - BaseModuleTemplate_Moonpool
            // declares North, South and one Bottom socket on lane Dock. This test's own name is about
            // cutting vertical socket visuals and collider slabs, so it was asserting the defect it
            // exists to catch. Both directions are checked: the mask that must be there, and the one
            // that must not come back.
            StringAssert.Contains("SocketMask.NorthSouth | SocketMask.Bottom", flat);
            Assert.IsFalse(
                flat.Contains("SocketMask.NorthSouth | SocketMask.Vertical", StringComparison.Ordinal),
                "VerticalShaft must not request SocketMask.Vertical: the Top half has no authored " +
                "inverse, so it becomes a permanent unresolvable snap candidate plus a hole in the " +
                "ceiling mesh and collider.");

            StringAssert.Contains("float powerDrawKW = math.max(0f, -spec.PowerRatingWatts) * 0.001f", source);
            StringAssert.Contains("RequireProperty(so, \"family\").enumValueIndex = (int)spec.Family", source);
            StringAssert.Contains("RequireProperty(so, \"powerRating\").floatValue = spec.PowerRatingWatts", source);

            // AddYFaceWithOptionalCutout was replaced by AddManufacturedFaceForSocket, which takes the
            // axis and direction as separate arguments so one method serves all six faces instead of
            // the Y pair only. The invariant under test is unchanged and is what these two assert: a
            // Top or Bottom socket opens its face, and nothing else does.
            StringAssert.Contains("AddManufacturedFaceForSocket", source);
            StringAssert.Contains("AddManufacturedFaceForSocket(buffers, e, b, 1, 1, (socketMask & SocketMask.Top) != 0", beveledBox);
            StringAssert.Contains("AddManufacturedFaceForSocket(buffers, e, b, 1, -1, (socketMask & SocketMask.Bottom) != 0", beveledBox);
            StringAssert.Contains("AddYSlabProxy(root, layer, \"COL_FloorProxy\", safeExtents, thickness, -1, (socketMask & SocketMask.Bottom) != 0)", collisionProxies);
            StringAssert.Contains("AddYSlabProxy(root, layer, \"COL_CeilingProxy\", safeExtents, thickness, 1, (socketMask & SocketMask.Top) != 0)", collisionProxies);
            StringAssert.Contains("name + \"_WestFrame\"", ySlabProxy);
            StringAssert.Contains("name + \"_EastFrame\"", ySlabProxy);
            StringAssert.Contains("name + \"_SouthFrame\"", ySlabProxy);
            StringAssert.Contains("name + \"_NorthFrame\"", ySlabProxy);
            Assert.IsFalse(collisionProxies.Contains("new Vector3(safeExtents.x * 2f, thickness, safeExtents.z * 2f)", StringComparison.Ordinal));
        }

        [Test]
        public void ModuleArchitectPrewarmsThreeSegmentBevelBuffers()
        {
            string source = ReadProjectFile(ModuleArchitectPath);

            StringAssert.Contains("MaxSocketFaceQuadCount = 6 * 6", source);
            StringAssert.Contains("MaxEdgeBevelQuadCount = 3 * 4 * MaxBevelSegments", source);
            StringAssert.Contains("MaxCornerBevelTriangleCount = 8 * MaxBevelSegments * MaxBevelSegments", source);
            StringAssert.Contains("MaxSocketFaceQuadCount * 4", source);
            StringAssert.Contains("MaxEdgeBevelQuadCount * 4", source);
            StringAssert.Contains("MaxCornerBevelTriangleCount * 3", source);
            StringAssert.Contains("MaxSocketFaceQuadCount * 6", source);
            StringAssert.Contains("MaxEdgeBevelQuadCount * 6", source);
            StringAssert.Contains("new List<Vector3>(GeneratedVertexCapacity)", source);
            StringAssert.Contains("new List<int>(GeneratedIndexCapacity)", source);
        }

        [Test]
        public void ModuleArchitectCatalogRegistrationDeduplicatesPersistentIds()
        {
            string source = ReadProjectFile(ModuleArchitectPath);
            string register = ExtractMethodBody(source, "private static void RegisterGeneratedBuildablesInCatalog(");

            StringAssert.Contains("TryFindBuildableIndexByPersistentId(modules, buildable, out int existingIndex)", register);
            StringAssert.Contains("existingElement.objectReferenceValue = buildable", register);
            const string LegacyObjectReferenceRoute = "ContainsObjectReference";
            Assert.IsFalse(register.Contains(LegacyObjectReferenceRoute, StringComparison.Ordinal));
            StringAssert.Contains("private static bool TryFindBuildableIndexByPersistentId(", source);
            StringAssert.Contains("existing.MatchesPersistentId(persistentId)", source);
        }

        [Test]
        public void QualityWeightScalesOfflineFidelityContinuously()
        {
            ColliderOptimizerSettings1716 low = ColliderOptimizerSettings1716.FromGlobalQualityWeight(0f);
            ColliderOptimizerSettings1716 mid = ColliderOptimizerSettings1716.FromGlobalQualityWeight(0.5f);
            ColliderOptimizerSettings1716 high = ColliderOptimizerSettings1716.FromGlobalQualityWeight(1f);
            ColliderOptimizerSettings1716 nan = ColliderOptimizerSettings1716.FromGlobalQualityWeight(float.NaN);

            Assert.AreEqual(0f, low.GlobalQualityWeight);
            Assert.AreEqual(0.5f, mid.GlobalQualityWeight);
            Assert.AreEqual(1f, high.GlobalQualityWeight);
            Assert.AreEqual(ColliderOptimizerEngine1716.DefaultGlobalQualityWeight, nan.GlobalQualityWeight);
            Assert.AreEqual(ColliderOptimizerEngine1716.MinPrimitiveCollidersPerPrefab, low.MaxPrimitiveCollidersPerPrefab);
            Assert.AreEqual(ColliderOptimizerEngine1716.MaxPrimitiveCollidersPerPrefab, high.MaxPrimitiveCollidersPerPrefab);
            Assert.Greater(mid.MaxPrimitiveCollidersPerPrefab, low.MaxPrimitiveCollidersPerPrefab);
            Assert.Less(mid.MaxPrimitiveCollidersPerPrefab, high.MaxPrimitiveCollidersPerPrefab);
            Assert.Greater(high.HullSupportDirectionCount, low.HullSupportDirectionCount);
            Assert.Greater(low.ProxyPaddingMeters, high.ProxyPaddingMeters);
        }

        [Test]
        public void CoralFamilyPathsUseConvexProxyInsteadOfFloraStripOnly()
        {
            MethodInfo resolveMode = typeof(ColliderOptimizerEngine1716).GetMethod(
                "ResolveMode",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(resolveMode);

            GameObject root = new GameObject("PFB_MassiveCoralCluster");
            try
            {
                object result = resolveMode.Invoke(
                    null,
                    new object[]
                    {
                        root,
                        "Assets/_Project/Prefabs/Flora/family_coral/PFB_MassiveCoralCluster.prefab",
                        ColliderOptimizerMode1716.Auto
                    });

                Assert.AreEqual(ColliderOptimizerMode1716.ConvexProxy, (ColliderOptimizerMode1716)result);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void KelpFloraPathsUseCompoundTriggerPrimitives()
        {
            MethodInfo resolveMode = typeof(ColliderOptimizerEngine1716).GetMethod(
                "ResolveMode",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(resolveMode);

            GameObject root = new GameObject("PFB_AbyssalKelpFrond");
            try
            {
                object result = resolveMode.Invoke(
                    null,
                    new object[]
                    {
                        root,
                        "Assets/_Project/Prefabs/Flora/Kelp/PFB_AbyssalKelpFrond.prefab",
                        ColliderOptimizerMode1716.Auto
                    });

                Assert.AreEqual(ColliderOptimizerMode1716.CompoundPrimitives, (ColliderOptimizerMode1716)result);

                object nameOnlyResult = resolveMode.Invoke(
                    null,
                    new object[]
                    {
                        root,
                        "Assets/_Project/Prefabs/World/PFB_AbyssalKelpFrond.prefab",
                        ColliderOptimizerMode1716.Auto
                    });

                Assert.AreEqual(ColliderOptimizerMode1716.CompoundPrimitives, (ColliderOptimizerMode1716)nameOnlyResult);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }

            string source = ReadProjectFile(EnginePath);
            StringAssert.Contains("IsFloraAsset(prefabPath, root.name)", source);
            StringAssert.Contains("collider.isTrigger = true", source);
            StringAssert.Contains("ContainsOrdinalIgnoreCase(sourceName, \"kelp\")", source);
        }

        [Test]
        public void BurstHullAndTelemetryArePresentButEditorScoped()
        {
            string source = ReadProjectFile(EnginePath);

            StringAssert.Contains("[BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard, CompileSynchronously = true)]", source);
            StringAssert.Contains("HullSupportPointJob1716 : IJobParallelFor", source);
            StringAssert.Contains("NativeArray<OptimizerTelemetryEntry1716>", source);
            StringAssert.Contains("TelemetryCapacity = 300", source);
            StringAssert.Contains("ValidateEditorStructLayouts", source);
            StringAssert.Contains("UnsafeUtility.SizeOf<T>()", source);
            Assert.IsFalse(source.Contains("Dump" + "Telemetry", StringComparison.Ordinal));
            Assert.IsFalse(source.Contains("Dump_" + "1716.bin", StringComparison.Ordinal));
            StringAssert.Contains("job.Schedule(directionCount, 4).Complete()", source);
            AssertPathContainsEditorSegment(EnginePath);
        }

        [Test]
        public void ConvexProxyMeshSerializationIsDeferredUntilAfterValidation()
        {
            string source = ReadProjectFile(EnginePath);
            string optimizeBody = ExtractMethodBody(
                source,
                "private static void OptimizePrefabAsset(string prefabPath, ColliderOptimizerMode1716 requestedMode, ColliderOptimizerSettings1716 settings, bool dryRun, ref ColliderOptimizerReport1716 report)");
            string convexBody = ExtractMethodBody(
                source,
                "private static bool GenerateConvexProxy(GameObject root, string prefabPath, ColliderOptimizerSettings1716 settings, ref ColliderOptimizerReport1716 report)");

            int validationIndex = optimizeBody.IndexOf("ValidatePrefabColliderBudget(root, out string failure)", StringComparison.Ordinal);
            int serializeIndex = optimizeBody.IndexOf("SerializeGeneratedProxyMeshes(root, prefabPath, settings, ref report, out failure)", StringComparison.Ordinal);
            int saveIndex = optimizeBody.IndexOf("PrefabUtility.SaveAsPrefabAsset(root, prefabPath)", StringComparison.Ordinal);

            Assert.GreaterOrEqual(validationIndex, 0);
            Assert.Greater(serializeIndex, validationIndex);
            Assert.Greater(saveIndex, serializeIndex);
            Assert.IsFalse(convexBody.Contains("AssetDatabase.CreateAsset", StringComparison.Ordinal));
        }

        [Test]
        public void HullContainmentGateRejectsUnderCoveringProxy()
        {
            MethodInfo containment = typeof(ColliderOptimizerEngine1716).GetMethod(
                "HullContainsSourceVertices",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(containment);

            List<Vector3> cubePoints = new List<Vector3>(8)
            {
                new Vector3(0f, 0f, 0f),
                new Vector3(1f, 0f, 0f),
                new Vector3(1f, 0f, 1f),
                new Vector3(0f, 0f, 1f),
                new Vector3(0f, 1f, 0f),
                new Vector3(1f, 1f, 0f),
                new Vector3(1f, 1f, 1f),
                new Vector3(0f, 1f, 1f)
            };
            List<int> cubeTriangles = new List<int>(36)
            {
                0, 2, 1, 0, 3, 2,
                4, 5, 6, 4, 6, 7,
                0, 1, 5, 0, 5, 4,
                1, 2, 6, 1, 6, 5,
                2, 3, 7, 2, 7, 6,
                3, 0, 4, 3, 4, 7
            };
            List<Vector3> insideSources = new List<Vector3>(2)
            {
                new Vector3(0.5f, 0.5f, 0.5f),
                new Vector3(1f, 1f, 1f)
            };
            List<Vector3> outsideSources = new List<Vector3>(1)
            {
                new Vector3(1.25f, 0.5f, 0.5f)
            };

            Assert.IsTrue((bool)containment.Invoke(null, new object[] { cubePoints, cubeTriangles, insideSources, 0.001f }));
            Assert.IsFalse((bool)containment.Invoke(null, new object[] { cubePoints, cubeTriangles, outsideSources, 0.001f }));
        }

        [Test]
        public void PrimitiveFitterRejectsNonTriangleSubmeshes()
        {
            MethodInfo fitter = typeof(ColliderOptimizerEngine1716).GetMethod(
                "TryFitPrimitive",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(fitter);

            Type fitType = typeof(ColliderOptimizerEngine1716).Assembly.GetType(
                "Hecton8.Editor.ColliderOptimization1716.ColliderPrimitiveFit1716");
            Assert.IsNotNull(fitType);

            Mesh mesh = new Mesh { name = "Line_Helper_Submesh" };
            try
            {
                mesh.vertices = new[]
                {
                    Vector3.zero,
                    Vector3.right,
                    Vector3.up,
                    new Vector3(1f, 1f, 0f)
                };
                mesh.SetIndices(new[] { 0, 1, 2, 3 }, MeshTopology.Lines, 0);

                object fit = Activator.CreateInstance(fitType);
                object[] args =
                {
                    mesh,
                    0,
                    Matrix4x4.identity,
                    "Line_Helper_Submesh",
                    fit
                };

                Assert.IsFalse((bool)fitter.Invoke(null, args));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(mesh);
            }
        }

        [Test]
        public void CullingManagerOwnsGeneratedColliderPresentationPhase()
        {
            string source = ReadProjectFile(CullingManagerPath);

            StringAssert.Contains("ISlowTickable", source);
            StringAssert.Contains("ILateFrameTickable", source);
            StringAssert.Contains("GlobalRegistry.TryRegisterSlowTickable(this, PriorityLayer.Environment)", source);
            StringAssert.Contains("GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Environment)", source);
            StringAssert.Contains("GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Environment)", source);
            StringAssert.Contains("ManagedCullColliders", source);
            StringAssert.Contains("ColliderStateDirty", source);
            StringAssert.Contains("CollectGeneratedCollidersNonAlloc", source);
            StringAssert.Contains("ApplyCullColliderState", source);
            StringAssert.Contains("name.StartsWith(\"COL_\", System.StringComparison.OrdinalIgnoreCase)", source);
            StringAssert.Contains("float sqrDist = delta.x * delta.x + delta.y * delta.y + delta.z * delta.z;", source);

            string slowTick = ExtractMethodBody(source, "public void SlowTick()");
            StringAssert.Contains("_cullingEvaluationRequested = true;", slowTick);
            Assert.IsFalse(slowTick.Contains("GetComponent", StringComparison.Ordinal));
            Assert.IsFalse(slowTick.Contains("TryGetComponent", StringComparison.Ordinal));
            Assert.IsFalse(slowTick.Contains("GlobalRegistry." + "Get<", StringComparison.Ordinal));
            Assert.IsFalse(slowTick.Contains("collider.enabled", StringComparison.Ordinal));
            Assert.IsFalse(slowTick.Contains("new ", StringComparison.Ordinal));

            string visualSync = ExtractMethodBody(source, "private void RunCullingEvaluationVisualSync()");
            StringAssert.Contains("SetCullState(ref obj", visualSync);
            StringAssert.Contains("CalculateCachedRendererBounds", visualSync);
            Assert.IsFalse(visualSync.Contains("ApplyCullColliderState", StringComparison.Ordinal));
            Assert.IsFalse(visualSync.Contains("collider.enabled", StringComparison.Ordinal));
            Assert.IsFalse(visualSync.Contains("GetComponent", StringComparison.Ordinal));
            Assert.IsFalse(visualSync.Contains("TryGetComponent", StringComparison.Ordinal));
            Assert.IsFalse(visualSync.Contains("GlobalRegistry." + "Get<", StringComparison.Ordinal));
            Assert.IsFalse(visualSync.Contains("new ", StringComparison.Ordinal));

            string lateFrameTick = ExtractMethodBody(source, "public void LateFrameTick()");
            StringAssert.Contains("RunCullingEvaluationVisualSync", lateFrameTick);
            StringAssert.Contains("_cullStateApplyRequested", lateFrameTick);
            StringAssert.Contains("ApplyCullColliderState", lateFrameTick);
            Assert.IsFalse(lateFrameTick.Contains("CalculateCachedRendererBounds", StringComparison.Ordinal));
            Assert.IsFalse(lateFrameTick.Contains("GlobalRegistry." + "Get<", StringComparison.Ordinal));

            Assert.IsFalse(source.Contains("void " + "Update(", StringComparison.Ordinal));
            Assert.IsFalse(source.Contains("void " + "FixedUpdate(", StringComparison.Ordinal));
            Assert.IsFalse(source.Contains("void " + "LateUpdate(", StringComparison.Ordinal));
        }

        [Test]
        public void RuntimeBakeCallsAreGoneFromPlayerScripts()
        {
            string scriptsRoot = Path.Combine(Directory.GetCurrentDirectory(), "Assets", "_Project", "Scripts");
            string[] files = Directory.GetFiles(scriptsRoot, "*.cs", SearchOption.AllDirectories);
            for (int i = 0; i < files.Length; i++)
            {
                string normalized = files[i].Replace('\\', '/');
                if (normalized.IndexOf("/Editor/", StringComparison.OrdinalIgnoreCase) >= 0)
                    continue;

                string source = File.ReadAllText(files[i]);
                Assert.IsFalse(source.Contains("Physics." + "BakeMesh", StringComparison.Ordinal), normalized);
                Assert.IsFalse(source.Contains("UnityEngine.Physics." + "BakeMesh", StringComparison.Ordinal), normalized);
            }
        }

        [Test]
        public void VoxelRuntimeColliderCookingFailsClosedToPrimitiveProxies()
        {
            string engine = ReadProjectFile(VoxelEnginePath);
            string volume = ReadProjectFile(VoxelVolumePath);

            Assert.IsFalse(engine.Contains("UnityEngine.Physics." + "BakeMesh", StringComparison.Ordinal));
            Assert.IsFalse(engine.Contains("Physics." + "BakeMesh", StringComparison.Ordinal));
            Assert.IsFalse(engine.Contains("job.TryScheduleAdmitted(JobAdmissionLane.Lane2_Voxel", StringComparison.Ordinal));
            Assert.AreEqual(0, CountOccurrences(engine, "TryScheduleVoxelPhysicsBake("));
            Assert.IsFalse(engine.Contains("PublishColliderChunkMesh(0)", StringComparison.Ordinal));
            Assert.IsFalse(engine.Contains("PublishColliderChunkMesh(chunkIndex)", StringComparison.Ordinal));
            StringAssert.Contains("EnableColliderChunkProxy(chunkIndex)", engine);
            Assert.IsFalse(engine.Contains("collider.sharedMesh = mesh", StringComparison.Ordinal));
            Assert.IsFalse(volume.Contains("EnqueueDeferredVoxelColliderUpload(this, index)", StringComparison.Ordinal));
            Assert.IsFalse(volume.Contains("collider.sharedMesh = stagedMesh", StringComparison.Ordinal));
            Assert.IsFalse(engine.Contains("_deferredVoxelColliderUploads.Add(new DeferredVoxelColliderUpload", StringComparison.Ordinal));
            StringAssert.Contains("TryConfigureChthonicPillarRuntimeProxy", engine);
            StringAssert.Contains("volume.ConfigureColliderChunkBakeProxy(chunkIndex, proxyCenter, proxySize);", engine);
            StringAssert.Contains("proxyCollider.enabled = true;", engine);

            string rootEnqueue = ExtractMethodBody(
                engine,
                "internal static bool EnqueueDeferredVoxelColliderUpload(MeshCollider collider, Mesh mesh, BoxCollider proxyCollider)");
            string volumeEnqueue = ExtractMethodBody(
                engine,
                "internal static bool EnqueueDeferredVoxelColliderUpload(Hecton8.Caves.HectonVoxelVolume volume, int chunkIndex)");
            string volumeCommit = ExtractMethodBody(
                volume,
                "internal bool CommitDeferredColliderChunkUpload(int index)");
            string volumeTryUsePrewarmed = ExtractMethodBody(
                volume,
                "public bool TryUsePrewarmedColliderChunkCapacity(int chunkCount)");
            string volumeReady = ExtractMethodBody(
                volume,
                "internal bool IsDeferredColliderChunkUploadReady(int index)");
            string volumeAssign = ExtractMethodBody(
                volume,
                "internal bool AssignColliderChunkBakeMesh(int index, Mesh mesh)");
            string volumeConfigureProxy = ExtractMethodBody(
                volume,
                "internal void ConfigureColliderChunkBakeProxy(int index, Vector3 center, Vector3 size)");
            string volumeGetOrCreate = ExtractMethodBody(
                volume,
                "public Mesh GetOrCreateColliderChunkMesh(int index)");
            string volumeGetOrCreateBake = ExtractMethodBody(
                volume,
                "internal Mesh GetOrCreateColliderChunkBakeMesh(int index)");
            string cancelDeferredUpload = ExtractMethodBody(
                engine,
                "private static void CancelDeferredVoxelColliderUpload(ref DeferredVoxelColliderUpload pending, bool publishRetryDropWarning)");
            string finalizeBakeTeardown = ExtractMethodBody(
                engine,
                "private static void FinalizeDeferredVoxelPhysicsBakeTeardown(ref DeferredVoxelPhysicsBakeTeardown pending)");
            string forceReleaseBakeTeardown = ExtractMethodBody(
                engine,
                "private static void ForceReleaseDeferredVoxelPhysicsBakeTeardownForShutdownOnly");
            string enableVoxelProxy = ExtractMethodBody(
                engine,
                "private static void EnableVoxelProxyCollider(BoxCollider proxyCollider)");
            string volumeDetachBakeMesh = ExtractMethodBody(
                volume,
                "internal void DetachColliderChunkBakeMesh(int index)");
            string volumeReleaseBakeMesh = ExtractMethodBody(
                volume,
                "internal void ReleaseColliderChunkBakeMesh(int index)");
            string volumeResetChunks = ExtractMethodBody(
                volume,
                "public void ResetColliderChunks(bool destroyMeshes)");
            string volumeGetColliderChunk = ExtractMethodBody(
                volume,
                "public MeshCollider GetColliderChunkCollider(int index)");
            string volumeCinematicFake = ExtractMethodBody(
                volume,
                "internal void DisableColliderChunksForCinematicFake()");
            string volumeSetActiveChunks = ExtractMethodBody(
                volume,
                "public void SetActiveColliderChunkCount(int activeCount)");
            string volumeResetProxyShape = ExtractMethodBody(
                volume,
                "private void ResetColliderChunkBakeProxyShape(int index)");
            string volumeDisableProxy = ExtractMethodBody(
                volume,
                "internal void DisableColliderChunkBakeProxy(int index)");
            string volumeDisableProxies = ExtractMethodBody(
                volume,
                "internal void DisableColliderChunkBakeProxies()");
            string volumeEnableProxy = ExtractMethodBody(
                volume,
                "internal bool EnableColliderChunkProxy(int index)");
            string volumeClearBakeMeshes = ExtractMethodBody(
                volume,
                "internal void ClearColliderChunkBakeMeshes()");
            string volumeRefreshBakePresentation = ExtractMethodBody(
                volume,
                "private void RefreshBakePresentation()");
            Assert.IsFalse(rootEnqueue.Contains("_deferredVoxelColliderUploads.Add", StringComparison.Ordinal));
            Assert.IsFalse(volumeEnqueue.Contains("_deferredVoxelColliderUploads.Add", StringComparison.Ordinal));
            Assert.IsFalse(volumeCommit.Contains("RefreshBakePresentation", StringComparison.Ordinal));
            StringAssert.Contains("EnableColliderChunkProxy(index);", volumeCommit);
            StringAssert.Contains("int colliderCount = _colliderChunkColliders != null ? _colliderChunkColliders.Length : 0;", volumeCommit);
            StringAssert.Contains("int bakeMeshCount = _colliderChunkBakeMeshes != null ? _colliderChunkBakeMeshes.Length : 0;", volumeCommit);
            StringAssert.Contains("if (index < 0)", volumeCommit);
            StringAssert.Contains("if (index < bakeMeshCount)", volumeCommit);
            StringAssert.Contains("if (index < colliderCount)", volumeCommit);
            StringAssert.Contains("if (_colliderChunkBakeMeshes == null)", volumeClearBakeMeshes);
            StringAssert.Contains("return;", volumeClearBakeMeshes);
            Assert.IsFalse(volumeAssign.Contains("AcquireVoxelPhysicsBakeMesh", StringComparison.Ordinal));
            Assert.IsFalse(volumeGetOrCreate.Contains("AcquireVoxelPhysicsBakeMesh", StringComparison.Ordinal));
            Assert.IsFalse(volumeGetOrCreateBake.Contains("AcquireVoxelPhysicsBakeMesh", StringComparison.Ordinal));
            StringAssert.Contains("_colliderChunkBakeProxies.Length < clampedCount", volumeTryUsePrewarmed);
            StringAssert.Contains("_colliderChunkColliders.Length < clampedCount", volumeTryUsePrewarmed);
            StringAssert.Contains("bool withinRequestedCapacity = i < clampedCount;", volumeTryUsePrewarmed);
            StringAssert.Contains("proxy.center = SanitizeColliderChunkProxyCenter(proxy.center, Vector3.zero);", volumeTryUsePrewarmed);
            StringAssert.Contains("proxy.size = SanitizeColliderChunkProxySize(proxy.size, Vector3.one * MinColliderChunkProxySize);", volumeTryUsePrewarmed);
            StringAssert.Contains("if (!withinRequestedCapacity && proxy.enabled)", volumeTryUsePrewarmed);
            StringAssert.Contains("_colliderChunkBakeProxies == null", volumeConfigureProxy);
            StringAssert.Contains("SanitizeColliderChunkProxyCenter(center, proxy.center)", volumeConfigureProxy);
            StringAssert.Contains("SanitizeColliderChunkProxySize(size, proxy.size)", volumeConfigureProxy);
            StringAssert.Contains("proxy.gameObject.layer = HectonLayerMasks.VoxelProxy;", volumeConfigureProxy);
            StringAssert.Contains("proxy.gameObject.SetActive(true);", volumeConfigureProxy);
            StringAssert.Contains("MinColliderChunkProxySize", volume);
            StringAssert.Contains("_colliderChunkBakeProxies == null", volumeDisableProxy);
            StringAssert.Contains("ResetColliderChunkBakeProxyShape(index);", volumeDisableProxy);
            StringAssert.Contains("if (_colliderChunkBakeProxies == null)", volumeDisableProxies);
            StringAssert.Contains("DisableColliderChunkBakeProxy(i);", volumeDisableProxies);
            StringAssert.Contains("_colliderChunkBakeProxies == null", volumeEnableProxy);
            StringAssert.Contains("int colliderCount = _colliderChunkColliders != null ? _colliderChunkColliders.Length : 0;", volumeEnableProxy);
            StringAssert.Contains("if (index < colliderCount)", volumeEnableProxy);
            StringAssert.Contains("proxy.gameObject.layer = HectonLayerMasks.VoxelProxy;", volumeEnableProxy);
            StringAssert.Contains("proxy.center = SanitizeColliderChunkProxyCenter(proxy.center, Vector3.zero);", volumeEnableProxy);
            StringAssert.Contains("proxy.size = SanitizeColliderChunkProxySize(proxy.size, Vector3.one * MinColliderChunkProxySize);", volumeEnableProxy);
            StringAssert.Contains("DisableColliderChunkBakeProxy(i);", volumeResetChunks);
            StringAssert.Contains("ResetColliderChunkBakeProxyShape(i);", volumeResetChunks);
            StringAssert.Contains("int proxyCount = _colliderChunkBakeProxies != null ? _colliderChunkBakeProxies.Length : 0;", volumeCinematicFake);
            StringAssert.Contains("int chunkCount = Mathf.Max(colliderCount, proxyCount);", volumeCinematicFake);
            StringAssert.Contains("MeshCollider collider = i < colliderCount ? _colliderChunkColliders[i] : null;", volumeCinematicFake);
            StringAssert.Contains("ResetColliderChunkBakeProxyShape(i);", volumeCinematicFake);
            StringAssert.Contains("proxy.gameObject.layer = HectonLayerMasks.VoxelProxy;", volumeSetActiveChunks);
            StringAssert.Contains("if (!shouldBeActive && proxy.enabled)", volumeSetActiveChunks);
            StringAssert.Contains("proxy.enabled = false;", volumeSetActiveChunks);
            StringAssert.Contains("if (_colliderChunkBakeProxies == null)", volumeSetActiveChunks);
            StringAssert.Contains("int colliderCount = _colliderChunkColliders != null ? _colliderChunkColliders.Length : 0;", volumeSetActiveChunks);
            StringAssert.Contains("if (i < colliderCount)", volumeSetActiveChunks);
            StringAssert.Contains("_colliderChunkColliders == null", volumeGetColliderChunk);
            StringAssert.Contains("(uint)index >= (uint)_colliderChunkColliders.Length", volumeGetColliderChunk);
            StringAssert.Contains("_colliderChunkBakeProxies == null", volumeResetProxyShape);
            StringAssert.Contains("(uint)index >= (uint)_colliderChunkBakeProxies.Length", volumeResetProxyShape);
            StringAssert.Contains("proxy.gameObject.layer = HectonLayerMasks.VoxelProxy;", volumeResetProxyShape);
            StringAssert.Contains("proxy.center = Vector3.zero;", volumeResetProxyShape);
            StringAssert.Contains("proxy.size = Vector3.one * MinColliderChunkProxySize;", volumeResetProxyShape);
            StringAssert.Contains("EnsureVoxelProxyLayerFiltering();", enableVoxelProxy);
            StringAssert.Contains("proxyCollider.gameObject.layer = HectonLayerMasks.VoxelProxy;", enableVoxelProxy);
            StringAssert.Contains("proxyCollider.gameObject.SetActive(true);", enableVoxelProxy);
            StringAssert.Contains("proxyCollider.enabled = true;", enableVoxelProxy);
            StringAssert.Contains("EnableVoxelProxyCollider(proxyCollider);", rootEnqueue);
            StringAssert.Contains("EnableVoxelProxyCollider(pending.ProxyCollider);", cancelDeferredUpload);
            StringAssert.Contains("EnableVoxelProxyCollider(pending.ProxyCollider);", finalizeBakeTeardown);
            StringAssert.Contains("EnableVoxelProxyCollider(proxyCollider);", forceReleaseBakeTeardown);
            Assert.IsFalse(cancelDeferredUpload.Contains("DisableColliderChunkBakeProxy", StringComparison.Ordinal));
            Assert.IsFalse(cancelDeferredUpload.Contains("pending.ProxyCollider.enabled = false", StringComparison.Ordinal));
            Assert.IsFalse(finalizeBakeTeardown.Contains("pending.ProxyCollider.enabled = false", StringComparison.Ordinal));
            Assert.IsFalse(forceReleaseBakeTeardown.Contains("proxyCollider.enabled = false", StringComparison.Ordinal));
            Assert.IsFalse(volumeDetachBakeMesh.Contains("DisableColliderChunkBakeProxy", StringComparison.Ordinal));
            Assert.IsFalse(volumeReleaseBakeMesh.Contains("DisableColliderChunkBakeProxy", StringComparison.Ordinal));
            StringAssert.Contains("int bakeMeshCount = _colliderChunkBakeMeshes != null ? _colliderChunkBakeMeshes.Length : 0;", volumeDetachBakeMesh);
            StringAssert.Contains("int colliderCount = _colliderChunkColliders != null ? _colliderChunkColliders.Length : 0;", volumeDetachBakeMesh);
            StringAssert.Contains("int bakeMeshCount = _colliderChunkBakeMeshes != null ? _colliderChunkBakeMeshes.Length : 0;", volumeReleaseBakeMesh);
            StringAssert.Contains("int colliderCount = _colliderChunkColliders != null ? _colliderChunkColliders.Length : 0;", volumeReleaseBakeMesh);
            StringAssert.Contains("EnableColliderChunkProxy(index);", volumeDetachBakeMesh);
            StringAssert.Contains("EnableColliderChunkProxy(index);", volumeReleaseBakeMesh);
            StringAssert.Contains("if (index < bakeMeshCount)", volumeDetachBakeMesh);
            StringAssert.Contains("if (index >= bakeMeshCount)", volumeReleaseBakeMesh);
            Assert.Greater(
                volumeDetachBakeMesh.IndexOf("int bakeMeshCount = _colliderChunkBakeMeshes != null ? _colliderChunkBakeMeshes.Length : 0;", StringComparison.Ordinal),
                volumeDetachBakeMesh.IndexOf("EnableColliderChunkProxy(index);", StringComparison.Ordinal));
            Assert.Greater(
                volumeReleaseBakeMesh.IndexOf("if (index >= bakeMeshCount)", StringComparison.Ordinal),
                volumeReleaseBakeMesh.IndexOf("EnableColliderChunkProxy(index);", StringComparison.Ordinal));
            StringAssert.Contains("int colliderCount = _colliderChunkColliders != null ? _colliderChunkColliders.Length : 0;", volumeResetChunks);
            StringAssert.Contains("int proxyCount = _colliderChunkBakeProxies != null ? _colliderChunkBakeProxies.Length : 0;", volumeResetChunks);
            StringAssert.Contains("int meshCount = _colliderChunkMeshes != null ? _colliderChunkMeshes.Length : 0;", volumeResetChunks);
            StringAssert.Contains("int bakeMeshCount = _colliderChunkBakeMeshes != null ? _colliderChunkBakeMeshes.Length : 0;", volumeResetChunks);
            StringAssert.Contains("int chunkCount = Mathf.Max(Mathf.Max(colliderCount, proxyCount), Mathf.Max(meshCount, bakeMeshCount));", volumeResetChunks);
            StringAssert.Contains("MeshCollider collider = i < colliderCount ? _colliderChunkColliders[i] : null;", volumeResetChunks);
            StringAssert.Contains("int colliderCount = _colliderChunkColliders != null ? _colliderChunkColliders.Length : 0;", volumeRefreshBakePresentation);
            StringAssert.Contains("for (int i = 0; i < colliderCount; i++)", volumeRefreshBakePresentation);
            StringAssert.Contains("return false;", rootEnqueue);
            StringAssert.Contains("return false;", volumeEnqueue);
            StringAssert.Contains("return false;", volumeCommit);
            StringAssert.Contains("return false;", volumeReady);
            StringAssert.Contains("return false;", volumeAssign);
            StringAssert.Contains("return null;", volumeGetOrCreate);
            StringAssert.Contains("return null;", volumeGetOrCreateBake);
            StringAssert.Contains("pending.Volume.EnableColliderChunkProxy(pending.ChunkIndex);", cancelDeferredUpload);
        }

        [Test]
        public void RuntimeSharedMeshAuditDetectsTerseColliderAssignmentsOnly()
        {
            MethodInfo scanner = typeof(ColliderOptimizerEngine1716).GetMethod(
                "IsRuntimeMeshColliderCommitLine",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(scanner);

            Assert.IsTrue((bool)scanner.Invoke(null, new object[] { "col.sharedMesh = collisionProxyMesh;" }));
            Assert.IsTrue((bool)scanner.Invoke(null, new object[] { "mc.sharedMesh = collisionProxyMesh;" }));
            Assert.IsTrue((bool)scanner.Invoke(null, new object[] { "targetCollider.sharedMesh = collisionProxyMesh;" }));
            Assert.IsFalse((bool)scanner.Invoke(null, new object[] { "meshFilter.sharedMesh = visualMesh;" }));
            Assert.IsFalse((bool)scanner.Invoke(null, new object[] { "renderer.sharedMesh = visualMesh;" }));
            Assert.IsFalse((bool)scanner.Invoke(null, new object[] { "pending.Collider.sharedMesh = null;" }));
            Assert.IsFalse((bool)scanner.Invoke(null, new object[] { "if (targetCollider.sharedMesh == null) return;" }));
            Assert.IsFalse((bool)scanner.Invoke(null, new object[] { "if (targetCollider.sharedMesh != collisionProxyMesh) return;" }));
            Assert.IsFalse((bool)scanner.Invoke(null, new object[] { "// col.sharedMesh = collisionProxyMesh;" }));
        }

        [Test]
        public void ValidatorRejectsSiblingColliderReferencingPrimaryVisualMesh()
        {
            GameObject root = new GameObject("PFB_SiblingPrimaryVisualReference");
            Mesh mesh = new Mesh { name = "VisualLOD0SharedMesh" };
            try
            {
                mesh.vertices = new[]
                {
                    Vector3.zero,
                    Vector3.right,
                    Vector3.up
                };
                mesh.triangles = new[] { 0, 1, 2 };
                mesh.RecalculateBounds();

                GameObject visual = new GameObject("Visual_LOD0");
                visual.transform.SetParent(root.transform, false);
                MeshFilter filter = visual.AddComponent<MeshFilter>();
                filter.sharedMesh = mesh;

                GameObject colliderCarrier = new GameObject("COL_IllegalSibling");
                colliderCarrier.transform.SetParent(root.transform, false);
                MeshCollider collider = colliderCarrier.AddComponent<MeshCollider>();
                collider.sharedMesh = mesh;

                Assert.IsFalse(ColliderOptimizerEngine1716.ValidatePrefabColliderBudget(root, out string failure));
                StringAssert.Contains("primary visual LOD0 mesh", failure);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
                UnityEngine.Object.DestroyImmediate(mesh);
            }
        }

        [Test]
        public void ValidatorRejectsEmptyGeneratedColliderRoots()
        {
            GameObject root = new GameObject("PFB_EmptyGeneratedColliderRoot");
            try
            {
                GameObject generated = new GameObject("COL_CompoundProxy_1716");
                generated.transform.SetParent(root.transform, false);

                Assert.IsFalse(ColliderOptimizerEngine1716.ValidatePrefabColliderBudget(root, out string failure));
                StringAssert.Contains("generated collider root has no Collider components", failure);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void ValidatorRejectsNonConvexGeneratedProxyRoot()
        {
            GameObject root = new GameObject("PFB_NonConvexGeneratedProxy");
            Mesh mesh = new Mesh { name = "COL_NonConvexGeneratedProxy_1716" };
            try
            {
                mesh.vertices = new[]
                {
                    Vector3.zero,
                    Vector3.right,
                    Vector3.up
                };
                mesh.triangles = new[] { 0, 1, 2 };
                mesh.RecalculateBounds();

                GameObject generated = new GameObject("COL_ConvexProxy_1716");
                generated.transform.SetParent(root.transform, false);
                MeshCollider collider = generated.AddComponent<MeshCollider>();
                collider.sharedMesh = mesh;
                collider.convex = false;

                Assert.IsFalse(ColliderOptimizerEngine1716.ValidatePrefabColliderBudget(root, out string failure));
                StringAssert.Contains("convex proxy MeshCollider is not convex", failure);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
                UnityEngine.Object.DestroyImmediate(mesh);
            }
        }

        [Test]
        public void LegacyRuntimeBakerCannotCookOrAssignMeshAtRuntime()
        {
            string source = ReadProjectFile(RuntimeBakerPath);
            string worldGenerator = ReadProjectFile(WorldGeneratorPath);
            string voxelEngine = ReadProjectFile(VoxelEnginePath);
            string voxelVolume = ReadProjectFile(VoxelVolumePath);
            string runtimeCombined = source + "\n" + worldGenerator + "\n" + voxelEngine + "\n" + voxelVolume;

            Assert.IsFalse(source.Contains("RuntimePhysicsBakeJob1609", StringComparison.Ordinal));
            Assert.IsFalse(runtimeCombined.Contains("Physics." + "BakeMesh", StringComparison.Ordinal));
            Assert.IsFalse(source.Contains("MeshColliderCookingOptions", StringComparison.Ordinal));
            Assert.IsFalse(runtimeCombined.Contains("MeshColliderCookingOptions", StringComparison.Ordinal));
            Assert.IsFalse(runtimeCombined.Contains("AddComponent<MeshCollider>", StringComparison.Ordinal));
            Assert.IsFalse(worldGenerator.Contains("TerrainColliderBakeJob", StringComparison.Ordinal));
            Assert.IsFalse(worldGenerator.Contains("ScheduleAsyncPhysicsBake", StringComparison.Ordinal));
            Assert.IsFalse(worldGenerator.Contains("PhysicsBakeStateScheduled", StringComparison.Ordinal));
            Assert.IsFalse(source.Contains("targetCollider.sharedMesh = collisionProxyMesh", StringComparison.Ordinal));
            StringAssert.Contains("targetCollider.sharedMesh != collisionProxyMesh", source);
            StringAssert.Contains("return false;", ExtractMethodBody(source, "public bool TryResolveBakeRequest"));
        }

        [Test]
        public void GeneratedProxyMeshBudgetValidatorRejectsOverBudgetMesh()
        {
            Mesh mesh = new Mesh { name = "COL_TestOverBudget1716" };
            Vector3[] vertices = new Vector3[603];
            int[] indices = new int[603];
            for (int i = 0; i < vertices.Length; i++)
            {
                vertices[i] = new Vector3(i % 3, (i / 3) * 0.001f, i % 7);
                indices[i] = i;
            }

            mesh.vertices = vertices;
            mesh.triangles = indices;
            mesh.RecalculateBounds();

            Assert.IsFalse(ColliderOptimizerEngine1716.ValidateProxyMesh(mesh, out string failure));
            StringAssert.Contains("exceeds 200 proxy triangles", failure);
            UnityEngine.Object.DestroyImmediate(mesh);
        }

        [Test]
        public void GeneratedProxyMeshValidatorRejectsEmptyOrFlatMeshes()
        {
            Mesh empty = new Mesh { name = "COL_EmptyProxy1716" };
            Mesh flat = new Mesh { name = "COL_FlatProxy1716" };
            try
            {
                flat.vertices = new[]
                {
                    Vector3.zero,
                    Vector3.right,
                    Vector3.up,
                    Vector3.one
                };
                flat.triangles = new[]
                {
                    0, 1, 2,
                    1, 3, 2
                };
                flat.RecalculateBounds();

                Assert.IsFalse(ColliderOptimizerEngine1716.ValidateProxyMesh(empty, out string emptyFailure));
                StringAssert.Contains("fewer than 4 proxy vertices", emptyFailure);

                Assert.IsFalse(ColliderOptimizerEngine1716.ValidateProxyMesh(flat, out string flatFailure));
                StringAssert.Contains("non-volumetric proxy bounds", flatFailure);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(empty);
                UnityEngine.Object.DestroyImmediate(flat);
            }
        }

        private static void AssertPathContainsEditorSegment(string projectRelativePath)
        {
            string normalized = projectRelativePath.Replace('\\', '/');
            Assert.IsTrue(normalized.Contains("/Editor/"), projectRelativePath);
        }

        private static string ReadProjectFile(string projectRelativePath)
        {
            string fullPath = Path.Combine(Directory.GetCurrentDirectory(), projectRelativePath.Replace('/', Path.DirectorySeparatorChar));
            return File.ReadAllText(fullPath);
        }

        private static int CountOccurrences(string source, string token)
        {
            int count = 0;
            int index = 0;
            while (index < source.Length)
            {
                index = source.IndexOf(token, index, StringComparison.Ordinal);
                if (index < 0)
                    return count;

                count++;
                index += token.Length;
            }

            return count;
        }

        private static string ExtractMethodBody(string source, string signature)
        {
            int signatureIndex = source.IndexOf(signature, StringComparison.Ordinal);
            Assert.GreaterOrEqual(signatureIndex, 0, signature);

            int openBrace = source.IndexOf((char)123, signatureIndex);
            Assert.GreaterOrEqual(openBrace, 0, signature);

            int depth = 0;
            for (int i = openBrace; i < source.Length; i++)
            {
                if (source[i] == (char)123)
                {
                    depth++;
                    continue;
                }

                if (source[i] != (char)125)
                    continue;

                depth--;
                if (depth == 0)
                    return source.Substring(openBrace + 1, i - openBrace - 1);
            }

            Assert.Fail("Unclosed method body: " + signature);
            return string.Empty;
        }
    }
}
