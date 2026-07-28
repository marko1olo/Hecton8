#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using Hecton8.Building;
using Hecton8.Construction;
using Hecton8.Editor.ColliderOptimization1716;
using Hecton8.Gameplay;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Hecton8.Editor.Structures
{
    public struct ModuleArchitect1712Settings
    {
        public string OutputFolder;

        /// <summary>
        /// Diagnostic single-material OVERRIDE. When non-empty, every fabricated module and every LOD
        /// renderer is forced onto this one material, which is the material-ID debug bake
        /// `3DMODEL_HARD_SURFACE_MODULES.md` section 11 asks for. When empty - the production default -
        /// each module uses the material its own <c>ModuleSpec</c> names.
        /// <para>
        /// This field used to default to <c>Mat_Module_Foundation.mat</c> and there was no per-module
        /// lane at all, so the corridor, the airlock, the reactor room and the vertical shaft all
        /// rendered wearing the foundation's texture set. The per-module material identity that
        /// `3dmodel.md` section 0 requires ("believable silhouette, material identity, ...") existed
        /// only in the six authored <c>Mat_Module_*.mat</c> assets and reached none of the prefabs.
        /// </para>
        /// </summary>
        public string MaterialPath;
        public float GlobalQualityWeight;
        public uint Seed;

        public static ModuleArchitect1712Settings Default => new ModuleArchitect1712Settings
        {
            OutputFolder = "Assets/_Project/Art/Baked/Structures/Agent1712",
            MaterialPath = string.Empty,
            GlobalQualityWeight = 0.75f,
            Seed = 1712u
        };
    }

    public struct ModuleArchitect1712Result
    {
        public bool Success;
        public int PrefabCount;
        public int VertexCount;
        public int TriangleCount;
        public string FailureReason;
    }

    public sealed class ModuleArchitect1712 : EditorWindow
    {
        // Socket compatibility lanes, using the authored kit's vocabulary read off the seven
        // production templates in Assets/_Project/Data/Construction/StandardModuleTemplates:
        // "Habitat" on 17 sockets, "Exterior" on exactly one (BaseModuleTemplate_Airlock's South
        // hatch), "Dock" on exactly one (BaseModuleTemplate_Moonpool's Bottom socket).
        //
        // The lane is not cosmetic metadata, it is a hard connectivity gate on both live paths:
        // snapping rejects a pair whose 24-bit lane hashes differ unless one side is empty
        // (ShinobuSocketConstructionData.cs:1156-1161, reached from
        // ShinobuSocketConstructionJobs.cs:133), and habitat-graph adjacency plus placement
        // validation reject a pair whose lane bitmasks do not intersect
        // (BaseModuleCatalogRuntime.cs:862-865, used at HabitatGraphManager.cs:4449 and
        // HabitatConstructionManager.cs:936). The single private lane this generator used to write,
        // "h8.structure.hardsurface", appeared on no authored template, so every fabricated module
        // was unconnectable to every hand-authored one on both paths. Only the empty string is a
        // wildcard (ShinobuSocketConstructionData.cs:1274-1275), and these sockets are not universal.
        private const string HabitatSocketLane = "Habitat";
        private const string ExteriorSocketLane = "Exterior";
        private const string DockSocketLane = "Dock";
        private const string WorldStaticLayerName = "World_Static";
        private const string DefaultModuleCatalogFolder = "Assets/_Project/Data/Construction";
        private const string DefaultModuleCatalogPath = DefaultModuleCatalogFolder + "/ModuleCatalog_Starter.asset";

        // Per-module material identity. These are the authored construction materials, textured and
        // scalar-tuned by ConstructionGeminiMaterialApplier.cs:17-73 and migrated onto
        // Hecton8/Construction/ModuleHardSurfaceLit - the only shader that reads the wear channels
        // this generator bakes - by ModuleHardSurfaceWearMaterialAuthoring.cs:65-73. Every path below
        // is inside that migrated set of six on purpose: a module pointed at a material outside it
        // would bake four wear channels that nothing consumes.
        //
        // Mat_Module_InsulationBacking is deliberately absent. It is torn-insulation backing for cut
        // faces, bound to specific panels by ConstructionInsulationBackingIntegrator.cs:35-70, not a
        // module shell material.
        private const string ConstructionMaterialFolder = "Assets/_Project/Art/Materials/Construction";
        private const string CorridorMaterialPath = ConstructionMaterialFolder + "/Mat_Module_Corridor.mat";
        private const string FoundationMaterialPath = ConstructionMaterialFolder + "/Mat_Module_Foundation.mat";
        private const string ServicePumpMaterialPath = ConstructionMaterialFolder + "/Mat_Module_ServicePump.mat";
        private const string PylonMaterialPath = ConstructionMaterialFolder + "/Mat_Module_Pylon.mat";
        private const string CurrentTurbineMaterialPath = ConstructionMaterialFolder + "/Mat_Module_CurrentTurbine.mat";
        // `3dmodel.md` section 4 fixes the base-module structural bevel band at 0.035 m to 0.12 m.
        // The previous 0.08-0.34 m band was the exterior hull/wreckage macro band and it rounded a
        // 2.7 m tall corridor by 25 percent of its height, which is a pillow, not a machined module.
        private const float MinBevelMeters = 0.035f;
        private const float MaxBevelMeters = 0.12f;
        private const int MinBevelSegments = 1;
        private const int MaxBevelSegments = 3;
        private const int MaxSocketFaceQuadCount = 6 * 6;
        private const int MaxEdgeBevelQuadCount = 3 * 4 * MaxBevelSegments;
        private const int MaxCornerBevelTriangleCount = 8 * MaxBevelSegments * MaxBevelSegments;

        // `3dmodel.md` section 7 per-asset budgets for the "Base module piece" class. These are now
        // asserted per LOD before the mesh is accepted; the generator previously declared no budget
        // at all, so an over-budget mesh would have shipped silently.
        private const int Lod0TriangleBudget = 15000;
        private const int Lod1TriangleBudget = 5000;
        private const int Lod2TriangleBudget = 700;

        // Manufactured-detail triangle bound, per module, sized against the six live specs. Detail
        // density is driven by lattice cell count, and the cell count per face is
        // (ribs + 1) * (belts + 1) where each divisor is
        // ModuleHardSurfaceDetail1712.ResolveDivisions(span, cap) = clamp(ceil(span / 1.45) - 1, 0,
        // cap) with caps MaxRibsPerFace 8 and MaxBeltsPerFace 6. It is therefore bounded by the caps,
        // not by module size, but the caps are not yet saturated at these sizes so a resize still
        // moves the figure.
        //
        // The previous worst case was H8_A1712_ReactorRoom_01 (10.8 x 3.7 x 9.6) at quality 1.0 and
        // detail tier 0 - six faces, each with a perimeter frame, ribs, belts, one recessed sub-panel
        // per lattice cell, a bolted connector flange on four faces, a bolt ring, one service plate
        // and one conduit run - measured at 6,360 triangles, over 202 lattice cells summed across the
        // six faces at those extents.
        //
        // The new worst case is H8_A1712_VerticalShaft_01 at the Moonpool envelope 12 x 8 x 10, whose
        // six faces sum to 262 cells: 1.297x the old worst, so about 8,249 triangles. 9,216 is that
        // figure plus 11.7 percent headroom. Second worst is H8_A1712_ReactorRoom_01 at the
        // MultiPurpose_Room envelope 10 x 6 x 10, at 210 cells. Both stay well clear of
        // Lod0TriangleBudget 15,000.
        //
        // These constants size List<T> capacity only. Unlike a fixed array they cannot truncate or
        // overflow: an underestimate costs one capacity doubling in an editor-only cold path, and any
        // mesh that genuinely exceeds Lod0TriangleBudget is rejected by AssertTriangleBudget before
        // it can be saved. The bound is an allocation target, not a correctness gate. The cell counts
        // above are arithmetic over ResolveDivisions, NOT a measurement; the per-LOD triangle counts
        // the bake prints are the only measured evidence.
        private const int MaxManufacturedDetailTriangleCount = 9216;

        // COLD ALLOC: List<Vector3> x2 + List<Vector4> x2 + List<Vector2> at
        // GeneratedVertexCapacity(28152) - 64 B per vertex across the five parallel streams, so
        // 1.80 MB - plus List<int> at GeneratedIndexCapacity(28296), 0.11 MB. Editor-only bake
        // scratch, freed when the bake returns. Not lazy/streamed because the whole mesh is authored
        // in one pass and a growing buffer would re-copy up to five parallel streams.
        // (The former comment quoted 21756 vertices / 1.39 MB; the formula below evaluates to 22008
        // for a 7,168-triangle detail bound, so that figure was stale by 252 vertices.)
        // - owner: ModuleArchitect1712
        private const int GeneratedVertexCapacity =
            (MaxSocketFaceQuadCount * 4) +
            (MaxEdgeBevelQuadCount * 4) +
            (MaxCornerBevelTriangleCount * 3) +
            (MaxManufacturedDetailTriangleCount * 3);
        private const int GeneratedIndexCapacity =
            (MaxSocketFaceQuadCount * 6) +
            (MaxEdgeBevelQuadCount * 6) +
            (MaxCornerBevelTriangleCount * 3) +
            (MaxManufacturedDetailTriangleCount * 3);
        private const float Lod0ScreenRatio = 0.62f;
        private const float Lod1ScreenRatio = 0.22f;
        private const float Lod2ScreenRatio = 0.06f;
        private const float MinColliderShellThicknessMeters = 0.12f;
        private const float MaxColliderShellThicknessMeters = 0.28f;
        private const float GeneratedModulePowerRatingWatts = -10f;
        private const int GeneratedModulePowerPriority = 50;
        private const float SeawaterDensityKilogramsPerCubicMeter = 1025f;
        private const float GeneratedHullMassDensityKilogramsPerCubicMeter = 86f;
        private const float GeneratedSocketMassKilograms = 850f;

        [SerializeField] private string outputFolder = ModuleArchitect1712Settings.Default.OutputFolder;
        [SerializeField] private string materialPath = ModuleArchitect1712Settings.Default.MaterialPath;
        [SerializeField, Range(0f, 1f)] private float globalQualityWeight = 0.75f;
        [SerializeField] private uint seed = 1712u;

        [MenuItem("Hecton8/Structures/Agent 1712/Fabricate Hard Surface Module Set")]
        public static void FabricateDefaultMenu()
        {
            GetWindow<ModuleArchitect1712>("Module Architect 1712").Show();
        }

        private void OnGUI()
        {
            outputFolder = EditorGUILayout.TextField("Output Folder", outputFolder);
            materialPath = EditorGUILayout.TextField("Material-ID Debug Override", materialPath);
            EditorGUILayout.LabelField(" ", "Empty = per-module authored materials (production).");
            globalQualityWeight = EditorGUILayout.Slider("Global Quality Weight", globalQualityWeight, 0f, 1f);
            seed = (uint)Mathf.Max(1, EditorGUILayout.IntField("Seed", unchecked((int)seed)));

            if (GUILayout.Button("Fabricate Offline Module Set"))
            {
                ModuleArchitect1712Settings settings = new ModuleArchitect1712Settings
                {
                    OutputFolder = outputFolder,
                    MaterialPath = materialPath,
                    GlobalQualityWeight = globalQualityWeight,
                    Seed = seed
                };

                if (FabricateDefaultSet(settings, out ModuleArchitect1712Result result))
                    Debug.Log($"[ModuleArchitect1712] baked prefabs={result.PrefabCount} verts={result.VertexCount} tris={result.TriangleCount}");
                else
                    Debug.LogError("[ModuleArchitect1712] bake failed: " + result.FailureReason);
            }
        }

        [MenuItem("Hecton8/Structures/Agent 1712/Fabricate Default Module Set Now")]
        public static void FabricateDefaultSetFromMenu()
        {
            if (FabricateDefaultSet(ModuleArchitect1712Settings.Default, out ModuleArchitect1712Result result))
                Debug.Log($"[ModuleArchitect1712] baked prefabs={result.PrefabCount} verts={result.VertexCount} tris={result.TriangleCount}");
            else
                Debug.LogError("[ModuleArchitect1712] bake failed: " + result.FailureReason);
        }

        public static bool FabricateDefaultSet(ModuleArchitect1712Settings settings, out ModuleArchitect1712Result result)
        {
            result = default;
            settings.OutputFolder = NormalizeAssetFolder(settings.OutputFolder);
            settings.MaterialPath = NormalizeAssetPath(settings.MaterialPath);
            settings.GlobalQualityWeight = math.saturate(math.isfinite(settings.GlobalQualityWeight) ? settings.GlobalQualityWeight : 1f);
            settings.Seed = settings.Seed == 0u ? 1712u : settings.Seed;

            try
            {
                EnsureAssetFolder(settings.OutputFolder);
                Material overrideMaterial = string.IsNullOrEmpty(settings.MaterialPath)
                    ? null
                    : ResolveMaterial(settings.MaterialPath, "<material-ID debug override>");
                // Extents are HALF sizes. Two halves of one contract depend on them: proxyBoundsSize
                // is written as extents * 2 (CreateOrUpdateTemplate), and every socket is placed on
                // the extent plane of its own axis (BuildSocketDefinitions). The hand-authored kit
                // obeys the identical contract - in all seven production templates each socket's
                // localPosition is exactly proxyBoundsSize/2 on its axis - so the two sides never
                // disagreed about meaning, only about numbers.
                //
                // The numbers below are pinned to the recipe template that OWNS the placed module at
                // runtime. BaseModule.ApplyBuildableTemplate (BaseModule.cs:4802-4816, called from
                // ConstructionManager.cs:825 on placement and :2873 on save restore) and
                // BaseModule.ReadBuildablePower (BaseModule.cs:4793-4794) both overwrite the
                // prefab's own moduleTemplate with the recipe's, so the recipe's proxy bounds and
                // socket planes are what the geometry must match. The prefab's authored template is
                // discarded and cannot win.
                //
                //   bound recipe             recipe template     proxyBoundsSize   extents here
                //   Build_Corridor_Straight  CorridorStraight     4 x 4 x  8       (2,   2,   4)
                //   Build_Junction_X         JunctionX            8 x 4 x  8       (4,   2,   4)
                //   Build_Junction_T         JunctionT            8 x 4 x  8       (4,   2,   4)
                //   Build_Airlock_Hatch      Airlock              6 x 5 x  6       (3,   2.5, 3)
                //   Build_MultiPurpose_Room  MultiPurposeRoom    10 x 6 x 10       (5,   3,   5)
                //   Build_Moonpool_Bay       Moonpool            12 x 8 x 10       (6,   4,   5)
                //
                // The previous extents gave the six modules six different ceiling heights - 2.7, 2.9,
                // 2.5, 2.9, 3.7 and 4.8 m - so the set could not butt against itself without a step
                // at the seam, let alone against the kit. `3DMODEL_HARD_SURFACE_MODULES.md` section 4
                // is explicit on both counts: "Socket boxes may drive placement, but generated visual
                // modules must add ..." and "Socket-compatible modules must share seam dimensions
                // exactly so no cracks appear." The socket box is the given; this generator conforms.
                //
                // H8_A1712_Corridor_01 is pinned to the corridor template even though
                // Build_Corridor_Straight still ships the legacy PFB_Module_Corridor, so the
                // generated family stays seam-compatible with itself and is a drop-in the day that
                // recipe is upgraded to real geometry.
                //
                // Module names are persisted IDENTITY and are deliberately unchanged: stableId and
                // templateHashId are LocHash over spec.Name (CreateOrUpdateTemplate below) and
                // BuildableData.ModuleHashId reads that template hash (BuildableData.cs:213-225).
                // Renaming H8_A1712_ServiceCap_01 to match the T-junction role it now fills would
                // move a persisted identity and needs a save migration; resizing does not, because
                // no geometric field reaches the hash.
                // Material identity per module, not one texture set for the whole family. The role
                // each material was authored for is the `Reason` string in
                // ConstructionGeminiMaterialApplier.cs:17-73; the mapping below follows those roles
                // and the reference frames in
                // `Docs\mandatory if you work on systems that user sees ...`, read directly
                // 2026-07-29:
                //
                //   base.webp        painted habitat shells with dark trim rings and near-black slim
                //                    support legs. The shell field is coated, not chrome; bare metal
                //                    appears only as small trim/hatch/collar accents.
                //   nice_biome.webp  dark painted structural frame carrying an ORANGE segmented safety
                //                    stripe as the readable accent, a tight grazing highlight on the
                //                    outer chamfer, and small genuinely-metallic fittings.
                //
                //   Corridor_01      Mat_Module_Corridor      interior wall trim sheet - the literal role.
                //   Junction_01      Mat_Module_Corridor      SHARED with the corridor on purpose. A
                //                                             junction is corridor-class interior and
                //                                             section 4 of
                //                                             `3DMODEL_HARD_SURFACE_MODULES.md` requires
                //                                             socket-compatible modules to meet without a
                //                                             visible seam; changing wall material across
                //                                             a butt joint reads as a crack even when the
                //                                             geometry is exact. It also keeps the set at
                //                                             five materials instead of six.
                //   ServiceCap_01    Mat_Module_ServicePump   wet service panel with biofilm - the service
                //                                             role in the name, and the T-junction it
                //                                             actually fills is a plant/utility run.
                //   Airlock_01       Mat_Module_Pylon         orange safety composite. This is the one
                //                                             module the player must find from OUTSIDE in
                //                                             murk - its South socket is the only
                //                                             Exterior-lane hatch in the set - and
                //                                             `TASTE.md` Visibility Is A Resource asks for
                //                                             "one readable affordance in the murk".
                //                                             nice_biome.webp uses exactly this move.
                //   ReactorRoom_01   Mat_Module_CurrentTurbine dark anodized machinery metal, matching the
                //                                             only Utility-family spec in the set (+450 W).
                //   VerticalShaft_01 Mat_Module_Foundation    salvage-worn repair metal for heavy base
                //                                             plates; the shaft is the largest structural
                //                                             member and carries the Dock socket.
                //
                // Batching consequence: five shared materials across a player-built base instead of
                // one, so five SRP Batcher groups where there was one. Against the `AGENTS.md`
                // guardrails of SetPass 600 / batches 1800 that is noise, and every renderer still
                // shares a project material asset - no clone, no MaterialPropertyBlock.
                ModuleSpec[] specs =
                {
                    new ModuleSpec("H8_A1712_Corridor_01", CorridorMaterialPath, new float3(2f, 2f, 4f), SocketMask.NorthSouth, 0xC011D012u),
                    new ModuleSpec("H8_A1712_Junction_01", CorridorMaterialPath, new float3(4f, 2f, 4f), SocketMask.Cross, 0xC011D04Au),
                    new ModuleSpec("H8_A1712_ServiceCap_01", ServicePumpMaterialPath, new float3(4f, 2f, 4f), SocketMask.NorthEastWest, 0xC011D0A7u),
                    new ModuleSpec(
                        "H8_A1712_Airlock_01",
                        PylonMaterialPath,
                        new float3(3f, 2.5f, 3f),
                        SocketMask.NorthSouth,
                        0xC011DA11u,
                        BuildableFamily.Structure,
                        -18f,
                        15,
                        false,
                        true,
                        // BaseModuleTemplate_Airlock authors its South socket as the ocean-facing
                        // hatch on lane Exterior, not Habitat.
                        new ModuleSpec.SocketLaneOverride(ModuleSocketDirection.South, ExteriorSocketLane)),
                    new ModuleSpec("H8_A1712_ReactorRoom_01", CurrentTurbineMaterialPath, new float3(5f, 3f, 5f), SocketMask.Cross, 0xC011D9E4u, BuildableFamily.Utility, 450f, 5, true, false),
                    new ModuleSpec(
                        "H8_A1712_VerticalShaft_01",
                        FoundationMaterialPath,
                        new float3(6f, 4f, 5f),
                        // Bottom only, no Top. BaseModuleTemplate_Moonpool declares exactly three
                        // sockets: North, South, and one Bottom socket on lane Dock at y = -4. The
                        // previous SocketMask.Vertical also emitted a Top socket with no authored
                        // inverse anywhere in the kit, and a socket that can never resolve is a
                        // permanent snap candidate plus a hole cut in the ceiling mesh and collider.
                        SocketMask.NorthSouth | SocketMask.Bottom,
                        0xC011D171u,
                        BuildableFamily.Habitat,
                        GeneratedModulePowerRatingWatts,
                        GeneratedModulePowerPriority,
                        false,
                        false,
                        new ModuleSpec.SocketLaneOverride(ModuleSocketDirection.Bottom, DockSocketLane))
                };

                int vertexCount = 0;
                int triangleCount = 0;
                BuildableData[] generatedBuildables = new BuildableData[specs.Length];

                // COLD ALLOC: Dictionary<string, Material>[8] - editor bake scratch, freed when the
                // bake returns. Loading each path once matters for identity, not speed: the corridor
                // and the junction must reference the SAME Material object so the prefabs share one
                // SRP Batcher group instead of two references to one asset - owner: ModuleArchitect1712
                Dictionary<string, Material> materialCache = new Dictionary<string, Material>(8, StringComparer.Ordinal);
                for (int i = 0; i < specs.Length; i++)
                {
                    Material moduleMaterial = ResolveModuleMaterial(specs[i], overrideMaterial, materialCache);
                    BaseModuleTemplate moduleTemplate = CreateOrUpdateTemplate(settings, specs[i]);
                    string prefabPath = $"{settings.OutputFolder}/{specs[i].Name}.prefab";
                    GameObject existingPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                    BuildableData buildableData = CreateOrUpdateBuildableData(settings, specs[i], moduleTemplate, existingPrefab);
                    BuildPrefab(settings, specs[i], moduleMaterial, moduleTemplate, buildableData, prefabPath, out int vertices, out int triangles);
                    generatedBuildables[i] = buildableData;
                    vertexCount += vertices;
                    triangleCount += triangles;
                }

                RegisterGeneratedBuildablesInCatalog(generatedBuildables);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                result.Success = true;
                result.PrefabCount = specs.Length;
                result.VertexCount = vertexCount;
                result.TriangleCount = triangleCount;
                return true;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.FailureReason = ex.Message;
                return false;
            }
        }

        private static void BuildPrefab(
            ModuleArchitect1712Settings settings,
            ModuleSpec spec,
            Material material,
            BaseModuleTemplate moduleTemplate,
            BuildableData buildableData,
            string prefabPath,
            out int vertexCount,
            out int triangleCount)
        {
            // Detail tier and quality weight are SEPARATE axes. The tier removes features for
            // distance (`3DMODEL_HARD_SURFACE_MODULES.md` section 7); the weight scales density
            // inside whichever features are present. Driving LOD off the quality weight alone - as
            // this did, with LOD2 baked at quality 0 - meant a compact-lane player received the
            // stripped far mesh as near-field geometry, which section 4 of `3dmodel.md` forbids.
            float quality = settings.GlobalQualityWeight;
            Mesh lod0Mesh = SaveOrUpdateMeshAsset(
                BuildHardSurfaceMesh(spec, quality, 0, settings.Seed, spec.Name + "_LOD0_Mesh", out int lod0Vertices, out int lod0Triangles),
                $"{settings.OutputFolder}/{spec.Name}_Mesh.asset");
            Mesh lod1Mesh = SaveOrUpdateMeshAsset(
                BuildHardSurfaceMesh(spec, math.saturate(quality * 0.62f), 1, settings.Seed, spec.Name + "_LOD1_Mesh", out int lod1Vertices, out int lod1Triangles),
                $"{settings.OutputFolder}/{spec.Name}_LOD1_Mesh.asset");
            Mesh lod2Mesh = SaveOrUpdateMeshAsset(
                BuildHardSurfaceMesh(spec, math.saturate(quality * 0.30f), 2, settings.Seed, spec.Name + "_LOD2_Mesh", out int lod2Vertices, out int lod2Triangles),
                $"{settings.OutputFolder}/{spec.Name}_LOD2_Mesh.asset");
            vertexCount = lod0Vertices + lod1Vertices + lod2Vertices;
            triangleCount = lod0Triangles + lod1Triangles + lod2Triangles;

            // Per-module, per-LOD counts against the section 7 budget. The only figure this generator
            // used to print was one sum across all six modules and all three LODs, which is why no
            // per-LOD budget could be checked from a log. Predicted counts are not evidence; this line
            // is what makes a bake run produce measured ones.
            Debug.Log(
                "[ModuleArchitect1712] " + spec.Name +
                " q=" + quality.ToString("0.###") +
                " LOD0=" + lod0Triangles + "/" + Lod0TriangleBudget +
                " LOD1=" + lod1Triangles + "/" + Lod1TriangleBudget +
                " LOD2=" + lod2Triangles + "/" + Lod2TriangleBudget +
                " verts=" + vertexCount +
                " openingHalf=(" +
                ModuleHardSurfaceDetail1712.ResolveOpeningHalfMeters(spec.Extents, 0, MaxBevelMeters).ToString("0.###") + "," +
                ModuleHardSurfaceDetail1712.ResolveOpeningHalfMeters(spec.Extents, 1, MaxBevelMeters).ToString("0.###") + "," +
                ModuleHardSurfaceDetail1712.ResolveOpeningHalfMeters(spec.Extents, 2, MaxBevelMeters).ToString("0.###") + ")" +
                " proxyBounds=" + (spec.Extents.x * 2f).ToString("0.###") + "x" +
                (spec.Extents.y * 2f).ToString("0.###") + "x" + (spec.Extents.z * 2f).ToString("0.###") +
                // Which material each module actually got, and which shader is on it. Without this
                // line a bake log cannot distinguish "six modules, six identities" from "six modules,
                // one texture set", which is the defect this lane fixes, and it is also the only place
                // a run reports whether the wear-shader migration has happened yet.
                " material=" + (material != null ? material.name : "<null>") +
                " shader=" + (material != null && material.shader != null ? material.shader.name : "<null>"));

            GameObject root = new GameObject(spec.Name);
            try
            {
                MeshFilter filter = root.AddComponent<MeshFilter>();
                filter.sharedMesh = lod0Mesh;
                MeshRenderer renderer = root.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                ConfigureVisualRenderer(renderer);
                AddCollisionProxies(root, spec.Extents, spec.SocketMask);
                Renderer lod1Renderer = AddLodChildRenderer(root.transform, "VIS_LOD1", lod1Mesh, material, root.layer);
                Renderer lod2Renderer = AddLodChildRenderer(root.transform, "VIS_LOD2", lod2Mesh, material, root.layer);
                AttachLodGroup(root, renderer, lod1Renderer, lod2Renderer);
                BoxCollider interiorTrigger = AddInteriorTrigger(root, spec.Extents);
                AttachRuntimeContracts(root, spec, moduleTemplate, buildableData, interiorTrigger);
                GameObjectUtility.SetStaticEditorFlags(root,
                    StaticEditorFlags.BatchingStatic |
                    StaticEditorFlags.OccluderStatic |
                    StaticEditorFlags.OccludeeStatic |
                    StaticEditorFlags.ContributeGI);
                if (!ColliderOptimizerEngine1716.ValidatePrefabColliderBudget(root, out string colliderFailure))
                    throw new InvalidOperationException("1716 collider validation failed before save: " + colliderFailure);

                PrefabUtility.SaveAsPrefabAsset(root, prefabPath, out bool success);
                if (!success)
                    throw new InvalidOperationException("Prefab serialization failed: " + prefabPath);

                if (!ColliderOptimizerEngine1716.ValidatePrefabAssetTopology(prefabPath, out colliderFailure))
                    throw new InvalidOperationException("1716 collider validation failed after save: " + colliderFailure);

                GameObject prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                CreateOrUpdateBuildableData(settings, spec, moduleTemplate, prefabAsset);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static Mesh SaveOrUpdateMeshAsset(Mesh mesh, string meshPath)
        {
            Mesh existingMesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            if (existingMesh != null)
            {
                EditorUtility.CopySerialized(mesh, existingMesh);
                UnityEngine.Object.DestroyImmediate(mesh);
                return existingMesh;
            }

            AssetDatabase.CreateAsset(mesh, meshPath);
            return mesh;
        }

        private static Mesh BuildHardSurfaceMesh(
            ModuleSpec spec,
            float quality,
            int detailTier,
            uint seed,
            string meshName,
            out int vertexCount,
            out int triangleCount)
        {
            var vertices = new List<Vector3>(GeneratedVertexCapacity);
            var normals = new List<Vector3>(GeneratedVertexCapacity);
            var uvs = new List<Vector2>(GeneratedVertexCapacity);
            var indices = new List<int>(GeneratedIndexCapacity);
            var tangents = new List<Vector4>(GeneratedVertexCapacity);
            var surface = new List<Vector4>(GeneratedVertexCapacity);
            var buffers = new HardSurfaceMeshBuffers1712(vertices, normals, tangents, uvs, surface, indices);
            float bevel = math.lerp(MinBevelMeters, MaxBevelMeters, quality);
            int bevelSegments = ResolveBevelSegments(quality);
            AddBeveledBox(buffers, spec.Extents, bevel, bevelSegments, spec.SocketMask, quality, detailTier, seed ^ spec.Seed);

            vertexCount = vertices.Count;
            triangleCount = indices.Count / 3;
            if (vertexCount <= 0 || triangleCount <= 0)
                throw new InvalidOperationException("Architect mesh produced empty topology.");

            AssertTriangleBudget(spec.Name, detailTier, triangleCount);
            ValidateTopology(vertices, normals, uvs, indices);
            Color32[] colors = BuildVertexColors(normals, surface, quality, seed ^ spec.Seed);
            Mesh mesh = new Mesh
            {
                name = meshName,
                indexFormat = vertexCount > ushort.MaxValue ? IndexFormat.UInt32 : IndexFormat.UInt16
            };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetTangents(tangents);
            mesh.SetUVs(0, uvs);
            mesh.colors32 = colors;
            mesh.SetTriangles(indices, 0, true);
            mesh.RecalculateBounds();
            ValidateMesh(mesh, vertexCount, triangleCount);
            mesh.UploadMeshData(false);
            return mesh;
        }

        /// <summary>
        /// `3dmodel.md` section 7 / section 10: every saved LOD must be inside its family budget, and
        /// a failure aborts the save. Failing loudly is deliberate - the alternative is a silently
        /// over-budget LOD0, which is the exact class of quiet degradation this project treats as the
        /// dominant failure mode.
        /// </summary>
        private static void AssertTriangleBudget(string moduleName, int detailTier, int triangleCount)
        {
            int budget = detailTier <= 0
                ? Lod0TriangleBudget
                : detailTier == 1
                    ? Lod1TriangleBudget
                    : Lod2TriangleBudget;
            if (triangleCount <= budget)
                return;

            throw new InvalidOperationException(
                "Architect mesh " + moduleName + " LOD" + detailTier + " emitted " + triangleCount +
                " triangles against the base-module budget of " + budget + " (3dmodel.md section 7).");
        }

        private static Renderer AddLodChildRenderer(Transform parent, string name, Mesh mesh, Material material, int layer)
        {
            GameObject child = new GameObject(name);
            child.layer = layer;
            child.transform.SetParent(parent, false);
            GameObjectUtility.SetStaticEditorFlags(child,
                StaticEditorFlags.BatchingStatic |
                StaticEditorFlags.OccludeeStatic |
                StaticEditorFlags.ContributeGI);
            MeshFilter filter = child.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;
            MeshRenderer renderer = child.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            ConfigureVisualRenderer(renderer);
            return renderer;
        }

        private static void ConfigureVisualRenderer(MeshRenderer renderer)
        {
            renderer.shadowCastingMode = ShadowCastingMode.On;
            renderer.receiveShadows = true;
            renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
            renderer.lightProbeUsage = LightProbeUsage.BlendProbes;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.BlendProbes;
            renderer.allowOcclusionWhenDynamic = false;
        }

        private static void AttachLodGroup(GameObject root, Renderer lod0, Renderer lod1, Renderer lod2)
        {
            LODGroup lodGroup = root.AddComponent<LODGroup>();
            lodGroup.fadeMode = LODFadeMode.CrossFade;
            lodGroup.animateCrossFading = true;
            lodGroup.SetLODs(new[]
            {
                new LOD(Lod0ScreenRatio, new[] { lod0 }),
                new LOD(Lod1ScreenRatio, new[] { lod1 }),
                new LOD(Lod2ScreenRatio, new[] { lod2 })
            });
            lodGroup.RecalculateBounds();
        }

        private static BaseModuleTemplate CreateOrUpdateTemplate(ModuleArchitect1712Settings settings, ModuleSpec spec)
        {
            string assetPath = $"{settings.OutputFolder}/{spec.Name}_Template.asset";
            BaseModuleTemplate template = AssetDatabase.LoadAssetAtPath<BaseModuleTemplate>(assetPath);
            if (template == null)
            {
                template = ScriptableObject.CreateInstance<BaseModuleTemplate>();
                AssetDatabase.CreateAsset(template, assetPath);
            }

            BaseModuleTemplate.SocketDefinition[] sockets = BuildSocketDefinitions(spec);
            Vector3 proxyBoundsSize = new Vector3(spec.Extents.x * 2f, spec.Extents.y * 2f, spec.Extents.z * 2f);
            float airVolume = math.max(1f, proxyBoundsSize.x * proxyBoundsSize.y * proxyBoundsSize.z * 0.55f);
            float powerDrawKW = math.max(0f, -spec.PowerRatingWatts) * 0.001f;

            SerializedObject so = new SerializedObject(template);
            RequireProperty(so, "stableId").stringValue = spec.Name;
            RequireProperty(so, "templateHashId").intValue = Hecton.Localization.LocHash.Compute(spec.Name);
            RequireProperty(so, "proxyBoundsCenter").vector3Value = Vector3.zero;
            RequireProperty(so, "proxyBoundsSize").vector3Value = proxyBoundsSize;
            RequireProperty(so, "airVolumeM3").floatValue = airVolume;
            RequireProperty(so, "powerDrawKW").floatValue = powerDrawKW;
            RequireProperty(so, "isStructuralAnchor").boolValue = spec.IsStructuralAnchor;
            RequireProperty(so, "isEmergencyAirlock").boolValue = spec.IsEmergencyAirlock;
            WritePhysicalTemplateFields(so, spec, sockets.Length, settings.GlobalQualityWeight);
            WriteSocketDefinitions(RequireProperty(so, "socketDefinitions"), sockets);
            WriteSnapPoints(RequireProperty(so, "snapPoints"), sockets);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(template);
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
            return template;
        }

        private static void WritePhysicalTemplateFields(
            SerializedObject serializedObject,
            ModuleSpec spec,
            int socketCount,
            float quality)
        {
            float q = math.saturate(quality);
            float3 fullSize = math.max(spec.Extents * 2f, new float3(1f));
            float volume = math.max(1f, fullSize.x * fullSize.y * fullSize.z);
            float projectedArea = math.max(
                fullSize.x * fullSize.y,
                math.max(fullSize.z * fullSize.y, fullSize.x * fullSize.z * 0.45f));
            float structuralMass = math.max(
                6000f,
                volume * GeneratedHullMassDensityKilogramsPerCubicMeter +
                socketCount * GeneratedSocketMassKilograms +
                q * 1200f);
            float displacementVolume = math.max(4f, (structuralMass / SeawaterDensityKilogramsPerCubicMeter) * math.lerp(1.04f, 1.12f, q));
            float yieldNewtons = math.max(180000f, structuralMass * 9.81f * math.lerp(16f, 24f, q));
            float breachArea = math.clamp(0.18f + socketCount * 0.16f + q * 0.18f, 0.2f, 1.8f);
            float centerShift = math.clamp(math.cmin(spec.Extents) * 0.38f, 0.12f, 1.15f);

            RequireProperty(serializedObject, "defaultIntegrityState").floatValue = math.lerp(0.86f, 0.96f, q);
            RequireProperty(serializedObject, "floodedBelowIntegrityState").floatValue = 0.42f;
            RequireProperty(serializedObject, "oxygenOfflineBelowIntegrityState").floatValue = 0.32f;
            RequireProperty(serializedObject, "projectedDragAreaSquareMeters").floatValue = projectedArea;
            RequireProperty(serializedObject, "moduleYieldStrengthNewtons").floatValue = yieldNewtons;
            RequireProperty(serializedObject, "breachAreaSquareMeters").floatValue = breachArea;
            RequireProperty(serializedObject, "structuralDryMassKilograms").floatValue = structuralMass;
            RequireProperty(serializedObject, "buoyancyDisplacementVolumeCubicMeters").floatValue = displacementVolume;
            RequireProperty(serializedObject, "maximumUnmooredAccelerationMetersPerSecondSquared").floatValue = math.lerp(10f, 18f, q);
            RequireProperty(serializedObject, "maximumCenterOfMassShiftMeters").floatValue = centerShift;
            RequireProperty(serializedObject, "centerOfMassShiftTauSeconds").floatValue = math.lerp(1.45f, 0.85f, q);
        }

        private static BuildableData CreateOrUpdateBuildableData(
            ModuleArchitect1712Settings settings,
            ModuleSpec spec,
            BaseModuleTemplate moduleTemplate,
            GameObject finalPrefab)
        {
            string assetPath = $"{settings.OutputFolder}/{spec.Name}_Buildable.asset";
            BuildableData data = AssetDatabase.LoadAssetAtPath<BuildableData>(assetPath);
            if (data == null)
            {
                data = ScriptableObject.CreateInstance<BuildableData>();
                AssetDatabase.CreateAsset(data, assetPath);
            }

            SerializedObject so = new SerializedObject(data);
            RequireProperty(so, "moduleName").stringValue = spec.Name;
            RequireProperty(so, "stableId").stringValue = spec.Name;
            RequireProperty(so, "description").stringValue = "Generated pressure-rated hard-surface module baked by ModuleArchitect1712.";
            RequireProperty(so, "family").enumValueIndex = (int)spec.Family;
            // ghostPrefab MUST stay null. BuildableData.cs:89 documents it as a legacy
            // field that "Runtime builder holography ignores", and the static gate
            // BuilderHolographyTools.NoNonZeroGhostPrefabRefs walks every .asset/.prefab
            // /.unity under Assets/_Project and FAILS unless every ghostPrefab line is
            // {fileID: 0}. The placement preview instantiates nothing: it is
            // Graphics.DrawProceduralIndirect over a 36-vertex cube in
            // Hecton_ConstructionDearLieHologram.shader, sized from
            // BaseModuleTemplate.ProxyBoundsSize. Assigning the final prefab here made
            // every fabricated module break that audit.
            RequireProperty(so, "ghostPrefab").objectReferenceValue = null;
            RequireProperty(so, "finalPrefab").objectReferenceValue = finalPrefab;
            RequireProperty(so, "moduleTemplate").objectReferenceValue = moduleTemplate;
            RequireProperty(so, "powerRating").floatValue = spec.PowerRatingWatts;
            RequireProperty(so, "powerPriority").intValue = spec.PowerPriority;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(data);
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
            return data;
        }

        private static void RegisterGeneratedBuildablesInCatalog(BuildableData[] buildables)
        {
            if (buildables == null || buildables.Length == 0)
                return;

            EnsureAssetFolder(DefaultModuleCatalogFolder);
            ModuleCatalog catalog = AssetDatabase.LoadAssetAtPath<ModuleCatalog>(DefaultModuleCatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<ModuleCatalog>();
                AssetDatabase.CreateAsset(catalog, DefaultModuleCatalogPath);
            }

            SerializedObject so = new SerializedObject(catalog);
            SerializedProperty modules = RequireProperty(so, "allModules");
            bool changed = false;
            for (int i = 0; i < buildables.Length; i++)
            {
                BuildableData buildable = buildables[i];
                if (buildable == null)
                    continue;

                if (TryFindBuildableIndexByPersistentId(modules, buildable, out int existingIndex))
                {
                    SerializedProperty existingElement = modules.GetArrayElementAtIndex(existingIndex);
                    if (existingElement.objectReferenceValue != buildable)
                    {
                        existingElement.objectReferenceValue = buildable;
                        changed = true;
                    }

                    continue;
                }

                int index = modules.arraySize;
                modules.InsertArrayElementAtIndex(index);
                modules.GetArrayElementAtIndex(index).objectReferenceValue = buildable;
                changed = true;
            }

            if (!changed)
                return;

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(catalog);
            AssetDatabase.ImportAsset(DefaultModuleCatalogPath, ImportAssetOptions.ForceUpdate);
        }

        private static bool TryFindBuildableIndexByPersistentId(
            SerializedProperty arrayProperty,
            BuildableData target,
            out int index)
        {
            index = -1;
            if (arrayProperty == null || target == null || !arrayProperty.isArray)
                return false;

            string persistentId = target.PersistentId;
            if (string.IsNullOrWhiteSpace(persistentId))
                return false;

            for (int i = 0; i < arrayProperty.arraySize; i++)
            {
                BuildableData existing = arrayProperty.GetArrayElementAtIndex(i).objectReferenceValue as BuildableData;
                if (existing == null)
                    continue;

                if (ReferenceEquals(existing, target) || existing.MatchesPersistentId(persistentId))
                {
                    index = i;
                    return true;
                }
            }

            return false;
        }

        private static void AttachRuntimeContracts(
            GameObject root,
            ModuleSpec spec,
            BaseModuleTemplate template,
            BuildableData buildableData,
            BoxCollider interiorTrigger)
        {
            if (root == null || template == null || buildableData == null || interiorTrigger == null)
                throw new InvalidOperationException("Generated module prefab requires a BaseModuleTemplate socket contract.");

            ModuleMarker marker = root.AddComponent<ModuleMarker>();
            marker.Initialize(buildableData);

            BaseModule baseModule = root.AddComponent<BaseModule>();
            SerializedObject so = new SerializedObject(baseModule);
            RequireProperty(so, "moduleTemplate").objectReferenceValue = template;
            RequireProperty(so, "interiorTrigger").objectReferenceValue = interiorTrigger;
            RequireProperty(so, "fallbackPowerRating").floatValue = spec.PowerRatingWatts;
            RequireProperty(so, "powerPriority").intValue = spec.PowerPriority;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static BaseModuleTemplate.SocketDefinition[] BuildSocketDefinitions(ModuleSpec spec)
        {
            int count = CountSockets(spec.SocketMask);
            BaseModuleTemplate.SocketDefinition[] definitions = new BaseModuleTemplate.SocketDefinition[count];
            int index = 0;
            TryWriteSocketDefinition(definitions, ref index, spec, SocketMask.North, ModuleSocketDirection.North, new Vector3(0f, 0f, spec.Extents.z));
            TryWriteSocketDefinition(definitions, ref index, spec, SocketMask.South, ModuleSocketDirection.South, new Vector3(0f, 0f, -spec.Extents.z));
            TryWriteSocketDefinition(definitions, ref index, spec, SocketMask.East, ModuleSocketDirection.East, new Vector3(spec.Extents.x, 0f, 0f));
            TryWriteSocketDefinition(definitions, ref index, spec, SocketMask.West, ModuleSocketDirection.West, new Vector3(-spec.Extents.x, 0f, 0f));
            TryWriteSocketDefinition(definitions, ref index, spec, SocketMask.Top, ModuleSocketDirection.Top, new Vector3(0f, spec.Extents.y, 0f));
            TryWriteSocketDefinition(definitions, ref index, spec, SocketMask.Bottom, ModuleSocketDirection.Bottom, new Vector3(0f, -spec.Extents.y, 0f));
            return definitions;
        }

        private static int CountSockets(SocketMask mask)
        {
            int count = 0;
            if ((mask & SocketMask.North) != 0) count++;
            if ((mask & SocketMask.South) != 0) count++;
            if ((mask & SocketMask.East) != 0) count++;
            if ((mask & SocketMask.West) != 0) count++;
            if ((mask & SocketMask.Top) != 0) count++;
            if ((mask & SocketMask.Bottom) != 0) count++;
            return count;
        }

        private static void TryWriteSocketDefinition(
            BaseModuleTemplate.SocketDefinition[] definitions,
            ref int index,
            ModuleSpec spec,
            SocketMask mask,
            ModuleSocketDirection direction,
            Vector3 localPosition)
        {
            if ((spec.SocketMask & mask) == 0 || index >= definitions.Length)
                return;

            definitions[index++] = new BaseModuleTemplate.SocketDefinition(
                localPosition,
                direction,
                spec.ResolveSocketLane(direction));
        }

        private static void WriteSocketDefinitions(SerializedProperty property, BaseModuleTemplate.SocketDefinition[] definitions)
        {
            property.arraySize = definitions != null ? definitions.Length : 0;
            for (int i = 0; i < property.arraySize; i++)
            {
                SerializedProperty element = property.GetArrayElementAtIndex(i);
                BaseModuleTemplate.SocketDefinition definition = definitions[i];
                RequireRelativeProperty(element, "localPosition").vector3Value = definition.LocalPosition;
                RequireRelativeProperty(element, "direction").enumValueIndex = (int)definition.Direction;
                RequireRelativeProperty(element, "compatibleType").stringValue = definition.CompatibleType;
            }
        }

        private static void WriteSnapPoints(SerializedProperty property, BaseModuleTemplate.SocketDefinition[] definitions)
        {
            property.arraySize = definitions != null ? definitions.Length : 0;
            for (int i = 0; i < property.arraySize; i++)
            {
                Vector3 position = definitions[i].LocalPosition;
                SerializedProperty element = property.GetArrayElementAtIndex(i);
                RequireRelativeProperty(element, "x").floatValue = position.x;
                RequireRelativeProperty(element, "y").floatValue = position.y;
                RequireRelativeProperty(element, "z").floatValue = position.z;
            }
        }

        /// <summary>
        /// Bakes the four wear channels of `3dmodel.md` section 4 from the surface attributes the
        /// geometry builders emitted, instead of trying to infer surface identity back out of a
        /// vertex position. The previous version derived everything from
        /// <c>length(p / extents)</c>, which is radial distance from the module centre and is not
        /// convexity, and it then wrote rust into BOTH green and blue, so the blue channel carried a
        /// second inverted copy of rust rather than ambient occlusion, and alpha was a constant.
        /// </summary>
        private static Color32[] BuildVertexColors(
            List<Vector3> sourceNormals,
            List<Vector4> sourceSurface,
            float quality,
            uint seed)
        {
            int count = sourceNormals.Count;
            if (sourceSurface.Count != count)
                throw new InvalidOperationException("Architect wear bake requires one surface attribute per vertex.");

            Color32[] colors = new Color32[count];
            NativeArray<float3> normals = default;
            NativeArray<float4> surface = default;
            NativeArray<uint> packed = default;
            try
            {
                normals = new NativeArray<float3>(count, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                surface = new NativeArray<float4>(count, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                packed = new NativeArray<uint>(count, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                for (int i = 0; i < count; i++)
                {
                    Vector3 n = sourceNormals[i];
                    Vector4 s = sourceSurface[i];
                    normals[i] = new float3(n.x, n.y, n.z);
                    surface[i] = new float4(s.x, s.y, s.z, s.w);
                }

                new ModuleArchitect1712WearJob
                {
                    Normals = normals,
                    Surface = surface,
                    Colors = packed,
                    GlobalQualityWeight = quality,
                    Seed = seed
                }.Run(count);

                for (int i = 0; i < count; i++)
                {
                    uint value = packed[i];
                    colors[i] = new Color32(
                        (byte)(value & 0xFFu),
                        (byte)((value >> 8) & 0xFFu),
                        (byte)((value >> 16) & 0xFFu),
                        (byte)((value >> 24) & 0xFFu));
                }
            }
            finally
            {
                if (packed.IsCreated)
                    packed.Dispose();
                if (surface.IsCreated)
                    surface.Dispose();
                if (normals.IsCreated)
                    normals.Dispose();
            }

            return colors;
        }

        /// <summary>
        /// Structural shell: six manufactured faces plus the edge and corner bevel chains. The face
        /// bodies are no longer flat plates with a hole punched in them - each one is built by
        /// <see cref="ModuleHardSurfaceDetail1712.AddManufacturedFace"/> as a recessed panel field
        /// inside a flush perimeter frame, broken by reinforcement ribs and belts, with a bolted
        /// flange, gasket collar and rim cap at every connector face.
        /// <para>
        /// The bevel chains are unchanged in shape and still define the silhouette. Because every
        /// detail is recessed INWARD from the extent plane, the outer envelope is exactly
        /// <c>extents * 2</c> both before and after this change, so
        /// <c>BaseModuleTemplate.proxyBoundsSize</c> and the placement hologram footprint are
        /// untouched.
        /// </para>
        /// </summary>
        private static void AddBeveledBox(
            HardSurfaceMeshBuffers1712 buffers,
            float3 extents,
            float bevel,
            int bevelSegments,
            SocketMask socketMask,
            float quality,
            int detailTier,
            uint seed)
        {
            float3 e = math.max(extents, new float3(0.5f));
            // `3dmodel.md` section 4 step 5: clamp bevel width to 20 percent of the shortest adjacent
            // edge. cmin(e) is the shortest HALF extent, so the shortest adjacent edge is 2*cmin(e)
            // and 20 percent of it is 0.4*cmin(e).
            float b = math.max(0.02f, math.min(bevel, math.cmin(e) * 0.4f));
            int segments = math.clamp(bevelSegments, MinBevelSegments, MaxBevelSegments);
            bool platesRemaining = detailTier <= 0;
            bool conduitRemaining = detailTier <= 0;

            AddManufacturedFaceForSocket(buffers, e, b, 0, 1, (socketMask & SocketMask.East) != 0, quality, detailTier, seed, ref platesRemaining, ref conduitRemaining);
            AddManufacturedFaceForSocket(buffers, e, b, 0, -1, (socketMask & SocketMask.West) != 0, quality, detailTier, seed, ref platesRemaining, ref conduitRemaining);
            AddManufacturedFaceForSocket(buffers, e, b, 1, 1, (socketMask & SocketMask.Top) != 0, quality, detailTier, seed, ref platesRemaining, ref conduitRemaining);
            AddManufacturedFaceForSocket(buffers, e, b, 1, -1, (socketMask & SocketMask.Bottom) != 0, quality, detailTier, seed, ref platesRemaining, ref conduitRemaining);
            AddManufacturedFaceForSocket(buffers, e, b, 2, 1, (socketMask & SocketMask.North) != 0, quality, detailTier, seed, ref platesRemaining, ref conduitRemaining);
            AddManufacturedFaceForSocket(buffers, e, b, 2, -1, (socketMask & SocketMask.South) != 0, quality, detailTier, seed, ref platesRemaining, ref conduitRemaining);

            AddZAxisEdgeBevels(buffers, e, b, segments);
            AddYAxisEdgeBevels(buffers, e, b, segments);
            AddXAxisEdgeBevels(buffers, e, b, segments);
            AddCornerBevels(buffers, e, b, segments);
        }

        private static void AddManufacturedFaceForSocket(
            HardSurfaceMeshBuffers1712 buffers,
            float3 extents,
            float bevel,
            int faceAxis,
            int sign,
            bool hasSocket,
            float quality,
            int detailTier,
            uint seed,
            ref bool platesRemaining,
            ref bool conduitRemaining)
        {
            ModuleHardSurfaceDetail1712.AddManufacturedFace(
                buffers,
                extents,
                bevel,
                MaxBevelMeters,
                faceAxis,
                sign,
                hasSocket,
                quality,
                detailTier,
                seed ^ (uint)((faceAxis * 2) + (sign > 0 ? 1 : 0) + 1),
                ref platesRemaining,
                ref conduitRemaining);
        }

        private static int ResolveBevelSegments(float quality)
        {
            return math.clamp(
                MinBevelSegments + (int)math.round(math.saturate(quality) * (MaxBevelSegments - MinBevelSegments)),
                MinBevelSegments,
                MaxBevelSegments);
        }

        // The three edge-bevel chains now emit an exact cylindrical unwrap - arc length across the
        // strip, edge distance along it - instead of inheriting the old global XZ vertex projection,
        // which produced zero-area UV triangles on every vertical surface. `3dmodel.md` section 6
        // names cylindrical unwrap as the approved route for this shape class.
        private static void AddZAxisEdgeBevels(HardSurfaceMeshBuffers1712 buffers, float3 e, float bevel, int segments)
        {
            for (int sx = -1; sx <= 1; sx += 2)
            for (int sy = -1; sy <= 1; sy += 2)
            for (int k = 0; k < segments; k++)
            {
                float t0 = (math.PI * 0.5f * k) / segments;
                float t1 = (math.PI * 0.5f * (k + 1)) / segments;
                float c0 = math.cos(t0);
                float s0 = math.sin(t0);
                float c1 = math.cos(t1);
                float s1 = math.sin(t1);
                float3 n0 = math.normalize(new float3(sx * c0, sy * s0, 0f));
                float3 n1 = math.normalize(new float3(sx * c1, sy * s1, 0f));
                float3 a = new float3(sx * (e.x - bevel + bevel * c0), sy * (e.y - bevel + bevel * s0), -e.z + bevel);
                float3 b = new float3(sx * (e.x - bevel + bevel * c0), sy * (e.y - bevel + bevel * s0), e.z - bevel);
                float3 c = new float3(sx * (e.x - bevel + bevel * c1), sy * (e.y - bevel + bevel * s1), e.z - bevel);
                float3 d = new float3(sx * (e.x - bevel + bevel * c1), sy * (e.y - bevel + bevel * s1), -e.z + bevel);
                buffers.AddQuadExplicitUv(
                    a, n0, new float2(bevel * t0, a.z),
                    b, n0, new float2(bevel * t0, b.z),
                    c, n1, new float2(bevel * t1, c.z),
                    d, n1, new float2(bevel * t1, d.z),
                    ModuleHardSurfaceDetail1712.BevelAttributes);
            }
        }

        private static void AddYAxisEdgeBevels(HardSurfaceMeshBuffers1712 buffers, float3 e, float bevel, int segments)
        {
            for (int sx = -1; sx <= 1; sx += 2)
            for (int sz = -1; sz <= 1; sz += 2)
            for (int k = 0; k < segments; k++)
            {
                float t0 = (math.PI * 0.5f * k) / segments;
                float t1 = (math.PI * 0.5f * (k + 1)) / segments;
                float c0 = math.cos(t0);
                float s0 = math.sin(t0);
                float c1 = math.cos(t1);
                float s1 = math.sin(t1);
                float3 n0 = math.normalize(new float3(sx * c0, 0f, sz * s0));
                float3 n1 = math.normalize(new float3(sx * c1, 0f, sz * s1));
                float3 a = new float3(sx * (e.x - bevel + bevel * c0), -e.y + bevel, sz * (e.z - bevel + bevel * s0));
                float3 b = new float3(sx * (e.x - bevel + bevel * c0), e.y - bevel, sz * (e.z - bevel + bevel * s0));
                float3 c = new float3(sx * (e.x - bevel + bevel * c1), e.y - bevel, sz * (e.z - bevel + bevel * s1));
                float3 d = new float3(sx * (e.x - bevel + bevel * c1), -e.y + bevel, sz * (e.z - bevel + bevel * s1));
                buffers.AddQuadExplicitUv(
                    a, n0, new float2(bevel * t0, a.y),
                    b, n0, new float2(bevel * t0, b.y),
                    c, n1, new float2(bevel * t1, c.y),
                    d, n1, new float2(bevel * t1, d.y),
                    ModuleHardSurfaceDetail1712.BevelAttributes);
            }
        }

        private static void AddXAxisEdgeBevels(HardSurfaceMeshBuffers1712 buffers, float3 e, float bevel, int segments)
        {
            for (int sy = -1; sy <= 1; sy += 2)
            for (int sz = -1; sz <= 1; sz += 2)
            for (int k = 0; k < segments; k++)
            {
                float t0 = (math.PI * 0.5f * k) / segments;
                float t1 = (math.PI * 0.5f * (k + 1)) / segments;
                float c0 = math.cos(t0);
                float s0 = math.sin(t0);
                float c1 = math.cos(t1);
                float s1 = math.sin(t1);
                float3 n0 = math.normalize(new float3(0f, sy * c0, sz * s0));
                float3 n1 = math.normalize(new float3(0f, sy * c1, sz * s1));
                float3 a = new float3(-e.x + bevel, sy * (e.y - bevel + bevel * c0), sz * (e.z - bevel + bevel * s0));
                float3 b = new float3(e.x - bevel, sy * (e.y - bevel + bevel * c0), sz * (e.z - bevel + bevel * s0));
                float3 c = new float3(e.x - bevel, sy * (e.y - bevel + bevel * c1), sz * (e.z - bevel + bevel * s1));
                float3 d = new float3(-e.x + bevel, sy * (e.y - bevel + bevel * c1), sz * (e.z - bevel + bevel * s1));
                buffers.AddQuadExplicitUv(
                    a, n0, new float2(bevel * t0, a.x),
                    b, n0, new float2(bevel * t0, b.x),
                    c, n1, new float2(bevel * t1, c.x),
                    d, n1, new float2(bevel * t1, d.x),
                    ModuleHardSurfaceDetail1712.BevelAttributes);
            }
        }

        // Corner patches get their own UV island on a plane perpendicular to the corner direction,
        // which keeps distortion low where a dominant-axis projection would compress a (1,1,1) patch
        // by 42 percent - over the 15 percent limit in `3dmodel.md` section 6.
        private static void AddCornerBevels(HardSurfaceMeshBuffers1712 buffers, float3 e, float bevel, int segments)
        {
            for (int sx = -1; sx <= 1; sx += 2)
            for (int sy = -1; sy <= 1; sy += 2)
            for (int sz = -1; sz <= 1; sz += 2)
            {
                float3 cornerDirection = math.normalize(new float3(sx, sy, sz));
                float3 cornerU = HardSurfaceMeshBuffers1712.OrthogonalAxis(cornerDirection);
                HardSurfaceUvFrame1712 cornerUv = ModuleHardSurfaceDetail1712.CreateUvFrame(
                    cornerU,
                    math.cross(cornerDirection, cornerU));

                for (int i = 0; i < segments; i++)
                for (int j = 0; j < segments - i; j++)
                {
                    ResolveCornerPoint(e, bevel, sx, sy, sz, i, j, segments, out float3 p00, out float3 n00);
                    ResolveCornerPoint(e, bevel, sx, sy, sz, i + 1, j, segments, out float3 p10, out float3 n10);
                    ResolveCornerPoint(e, bevel, sx, sy, sz, i, j + 1, segments, out float3 p01, out float3 n01);
                    buffers.AddTriangleSmooth(p00, n00, p10, n10, p01, n01, cornerUv, ModuleHardSurfaceDetail1712.BevelAttributes);

                    if (i + j >= segments - 1)
                        continue;

                    ResolveCornerPoint(e, bevel, sx, sy, sz, i + 1, j + 1, segments, out float3 p11, out float3 n11);
                    buffers.AddTriangleSmooth(p10, n10, p11, n11, p01, n01, cornerUv, ModuleHardSurfaceDetail1712.BevelAttributes);
                }
            }
        }

        private static void ResolveCornerPoint(
            float3 e,
            float bevel,
            int sx,
            int sy,
            int sz,
            int ix,
            int iy,
            int segments,
            out float3 position,
            out float3 normal)
        {
            float inv = 1f / math.max(1, segments);
            float wx = ix * inv;
            float wy = iy * inv;
            float wz = math.max(0f, 1f - wx - wy);
            normal = math.normalizesafe(new float3(sx * wx, sy * wy, sz * wz), math.normalize(new float3(sx, sy, sz)));
            float3 absNormal = math.abs(normal);
            position = new float3(
                sx * (e.x - bevel + bevel * absNormal.x),
                sy * (e.y - bevel + bevel * absNormal.y),
                sz * (e.z - bevel + bevel * absNormal.z));
        }

        private static void AddCollisionProxies(GameObject root, float3 extents, SocketMask socketMask)
        {
            int layer = LayerMask.NameToLayer(WorldStaticLayerName);
            if (layer < 0)
                layer = root.layer;

            root.layer = layer;
            float3 safeExtents = math.max(extents, new float3(0.5f));
            float thickness = math.clamp(
                math.cmin(safeExtents) * 0.08f,
                MinColliderShellThicknessMeters,
                MaxColliderShellThicknessMeters);

            AddYSlabProxy(root, layer, "COL_FloorProxy", safeExtents, thickness, -1, (socketMask & SocketMask.Bottom) != 0);
            AddYSlabProxy(root, layer, "COL_CeilingProxy", safeExtents, thickness, 1, (socketMask & SocketMask.Top) != 0);

            AddXWallProxy(root, layer, "COL_EastWallProxy", safeExtents, thickness, 1, (socketMask & SocketMask.East) != 0);
            AddXWallProxy(root, layer, "COL_WestWallProxy", safeExtents, thickness, -1, (socketMask & SocketMask.West) != 0);
            AddZWallProxy(root, layer, "COL_NorthWallProxy", safeExtents, thickness, 1, (socketMask & SocketMask.North) != 0);
            AddZWallProxy(root, layer, "COL_SouthWallProxy", safeExtents, thickness, -1, (socketMask & SocketMask.South) != 0);
        }

        private static void AddXWallProxy(GameObject root, int layer, string name, float3 extents, float thickness, int sign, bool hasSocket)
        {
            float x = sign * (extents.x - thickness * 0.5f);
            if (!hasSocket)
            {
                AddBoxColliderProxy(
                    root,
                    layer,
                    name,
                    new Vector3(x, 0f, 0f),
                    new Vector3(thickness, extents.y * 2f, extents.z * 2f));
                return;
            }

            // Same helper the visual door cut-out calls. Previously the collider used
            // min(extents*0.55, 0.95) for the lintel while the mesh used
            // 0.42*(fullSpan - 2*bevel), so on H8_A1712_Airlock_01 the collider lintel hung 13 cm
            // into the visible opening and the mesh hole changed size with the quality weight.
            float holeHalfZ = ModuleHardSurfaceDetail1712.ResolveOpeningHalfMeters(extents, 2, MaxBevelMeters);
            float holeHalfY = ModuleHardSurfaceDetail1712.ResolveOpeningHalfMeters(extents, 1, MaxBevelMeters);
            float sideDepth = math.max(thickness, extents.z - holeHalfZ);
            float topHeight = math.max(thickness, extents.y - holeHalfY);
            AddBoxColliderProxy(
                root,
                layer,
                name + "_LeftFrame",
                new Vector3(x, 0f, -holeHalfZ - sideDepth * 0.5f),
                new Vector3(thickness, extents.y * 2f, sideDepth));
            AddBoxColliderProxy(
                root,
                layer,
                name + "_RightFrame",
                new Vector3(x, 0f, holeHalfZ + sideDepth * 0.5f),
                new Vector3(thickness, extents.y * 2f, sideDepth));
            AddBoxColliderProxy(
                root,
                layer,
                name + "_Lintel",
                new Vector3(x, holeHalfY + topHeight * 0.5f, 0f),
                new Vector3(thickness, topHeight, holeHalfZ * 2f));
        }

        private static void AddZWallProxy(GameObject root, int layer, string name, float3 extents, float thickness, int sign, bool hasSocket)
        {
            float z = sign * (extents.z - thickness * 0.5f);
            if (!hasSocket)
            {
                AddBoxColliderProxy(
                    root,
                    layer,
                    name,
                    new Vector3(0f, 0f, z),
                    new Vector3(extents.x * 2f, extents.y * 2f, thickness));
                return;
            }

            float holeHalfX = ModuleHardSurfaceDetail1712.ResolveOpeningHalfMeters(extents, 0, MaxBevelMeters);
            float holeHalfY = ModuleHardSurfaceDetail1712.ResolveOpeningHalfMeters(extents, 1, MaxBevelMeters);
            float sideWidth = math.max(thickness, extents.x - holeHalfX);
            float topHeight = math.max(thickness, extents.y - holeHalfY);
            AddBoxColliderProxy(
                root,
                layer,
                name + "_LeftFrame",
                new Vector3(-holeHalfX - sideWidth * 0.5f, 0f, z),
                new Vector3(sideWidth, extents.y * 2f, thickness));
            AddBoxColliderProxy(
                root,
                layer,
                name + "_RightFrame",
                new Vector3(holeHalfX + sideWidth * 0.5f, 0f, z),
                new Vector3(sideWidth, extents.y * 2f, thickness));
            AddBoxColliderProxy(
                root,
                layer,
                name + "_Lintel",
                new Vector3(0f, holeHalfY + topHeight * 0.5f, z),
                new Vector3(holeHalfX * 2f, topHeight, thickness));
        }

        private static void AddYSlabProxy(GameObject root, int layer, string name, float3 extents, float thickness, int sign, bool hasSocket)
        {
            float y = sign * (extents.y - thickness * 0.5f);
            if (!hasSocket)
            {
                AddBoxColliderProxy(
                    root,
                    layer,
                    name,
                    new Vector3(0f, y, 0f),
                    new Vector3(extents.x * 2f, thickness, extents.z * 2f));
                return;
            }

            float holeHalfX = ModuleHardSurfaceDetail1712.ResolveOpeningHalfMeters(extents, 0, MaxBevelMeters);
            float holeHalfZ = ModuleHardSurfaceDetail1712.ResolveOpeningHalfMeters(extents, 2, MaxBevelMeters);
            float sideWidth = math.max(thickness, extents.x - holeHalfX);
            float sideDepth = math.max(thickness, extents.z - holeHalfZ);
            AddBoxColliderProxy(
                root,
                layer,
                name + "_WestFrame",
                new Vector3(-holeHalfX - sideWidth * 0.5f, y, 0f),
                new Vector3(sideWidth, thickness, holeHalfZ * 2f));
            AddBoxColliderProxy(
                root,
                layer,
                name + "_EastFrame",
                new Vector3(holeHalfX + sideWidth * 0.5f, y, 0f),
                new Vector3(sideWidth, thickness, holeHalfZ * 2f));
            AddBoxColliderProxy(
                root,
                layer,
                name + "_SouthFrame",
                new Vector3(0f, y, -holeHalfZ - sideDepth * 0.5f),
                new Vector3(extents.x * 2f, thickness, sideDepth));
            AddBoxColliderProxy(
                root,
                layer,
                name + "_NorthFrame",
                new Vector3(0f, y, holeHalfZ + sideDepth * 0.5f),
                new Vector3(extents.x * 2f, thickness, sideDepth));
        }

        private static void AddBoxColliderProxy(GameObject root, int layer, string name, Vector3 center, Vector3 size)
        {
            GameObject proxy = new GameObject(name);
            proxy.layer = layer;
            proxy.transform.SetParent(root.transform, false);
            proxy.transform.localPosition = center;
            proxy.transform.localRotation = Quaternion.identity;
            BoxCollider collider = proxy.AddComponent<BoxCollider>();
            collider.center = Vector3.zero;
            collider.size = size;
        }

        private static BoxCollider AddInteriorTrigger(GameObject root, float3 extents)
        {
            GameObject trigger = new GameObject("InteriorTrigger");
            trigger.layer = root.layer;
            trigger.transform.SetParent(root.transform, false);
            trigger.transform.localPosition = Vector3.zero;
            trigger.transform.localRotation = Quaternion.identity;

            BoxCollider collider = trigger.AddComponent<BoxCollider>();
            collider.center = Vector3.zero;
            collider.size = new Vector3(
                math.max(0.5f, extents.x * 1.65f),
                math.max(0.5f, extents.y * 1.55f),
                math.max(0.5f, extents.z * 1.65f));
            collider.isTrigger = true;
            return collider;
        }

        /// <summary>
        /// Resolves the material one module wears. The debug override wins when the caller set one;
        /// otherwise the module's own authored material is loaded, once per distinct path so modules
        /// that intentionally share a material also share the loaded <see cref="Material"/> instance.
        /// A missing material is fatal and names the module - a generator that quietly substituted a
        /// stand-in would reproduce the exact defect this lane was added to remove.
        /// </summary>
        private static Material ResolveModuleMaterial(
            ModuleSpec spec,
            Material overrideMaterial,
            Dictionary<string, Material> cache)
        {
            if (overrideMaterial != null)
                return overrideMaterial;

            string path = NormalizeAssetPath(spec.MaterialPath);
            if (cache.TryGetValue(path, out Material cached) && cached != null)
                return cached;

            Material material = ResolveMaterial(path, spec.Name);
            cache[path] = material;
            return material;
        }

        private static Material ResolveMaterial(string materialPath, string ownerName)
        {
            if (string.IsNullOrEmpty(materialPath) || !materialPath.StartsWith("Assets/", StringComparison.Ordinal))
                throw new InvalidOperationException(
                    "ModuleArchitect1712 material path must be a valid Assets/... path for " + ownerName + ": '" + materialPath + "'");

            Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null)
                throw new InvalidOperationException("Missing authored module material for " + ownerName + ": " + materialPath);

            return material;
        }

        private static void ValidateMesh(Mesh mesh, int vertexCount, int triangleCount)
        {
            if (mesh == null || vertexCount <= 0 || triangleCount <= 0)
                throw new InvalidOperationException("Architect mesh validation rejected empty output.");

            Bounds bounds = mesh.bounds;
            if (!math.all(math.isfinite(new float3(bounds.center.x, bounds.center.y, bounds.center.z))) ||
                !math.all(math.isfinite(new float3(bounds.size.x, bounds.size.y, bounds.size.z))) ||
                bounds.size.sqrMagnitude <= 0.0001f)
            {
                throw new InvalidOperationException("Architect mesh validation rejected non-finite bounds.");
            }
        }

        private static void ValidateTopology(List<Vector3> vertices, List<Vector3> normals, List<Vector2> uvs, List<int> indices)
        {
            if (vertices == null ||
                normals == null ||
                uvs == null ||
                indices == null ||
                vertices.Count <= 0 ||
                vertices.Count != normals.Count ||
                vertices.Count != uvs.Count ||
                indices.Count <= 0 ||
                indices.Count % 3 != 0)
            {
                throw new InvalidOperationException("Architect topology validation rejected malformed buffers.");
            }

            for (int i = 0; i < vertices.Count; i++)
            {
                Vector3 position = vertices[i];
                Vector3 normal = normals[i];
                Vector2 uv = uvs[i];
                if (!IsFinite(position) || !IsFinite(normal) || normal.sqrMagnitude < 0.25f || normal.sqrMagnitude > 2.25f)
                    throw new InvalidOperationException("Architect topology validation rejected non-finite vertex data.");

                if (float.IsNaN(uv.x) || float.IsInfinity(uv.x) || float.IsNaN(uv.y) || float.IsInfinity(uv.y))
                    throw new InvalidOperationException("Architect topology validation rejected non-finite UV data.");
            }

            for (int i = 0; i < indices.Count; i += 3)
            {
                int ia = indices[i];
                int ib = indices[i + 1];
                int ic = indices[i + 2];
                if ((uint)ia >= (uint)vertices.Count ||
                    (uint)ib >= (uint)vertices.Count ||
                    (uint)ic >= (uint)vertices.Count)
                {
                    throw new InvalidOperationException("Architect topology validation rejected out-of-range triangle index.");
                }

                float3 a = (float3)(vertices[ia]);
                float3 b = (float3)(vertices[ib]);
                float3 c = (float3)(vertices[ic]);
                float3 cross = math.cross(b - a, c - a);
                float areaSq = math.lengthsq(cross);
                if (!math.isfinite(areaSq) || areaSq <= 0.00000001f)
                    throw new InvalidOperationException("Architect topology validation rejected degenerate triangle.");

                float3 triangleNormal = math.normalize(cross);
                float3 authoredNormal = math.normalizesafe(
                    (float3)(normals[ia]) + (float3)(normals[ib]) + (float3)(normals[ic]),
                    triangleNormal);
                if (math.dot(triangleNormal, authoredNormal) < 0.25f)
                    throw new InvalidOperationException("Architect topology validation rejected inverted triangle winding.");

                // `3dmodel.md` section 10: no zero-area UV triangle on a textured surface. The
                // material on these modules is URP Lit with _NORMALMAP, _PARALLAXMAP,
                // _METALLICSPECGLOSSMAP and _OCCLUSIONMAP all sampling UV0, so a collapsed UV
                // triangle is four broken maps, not a cosmetic detail. The previous global
                // uv = (position.x, position.z) projection collapsed every vertical surface, which
                // is four of the six module faces.
                Vector2 uvA = uvs[ia];
                Vector2 uvB = uvs[ib];
                Vector2 uvC = uvs[ic];
                float uvArea = math.abs(
                    ((uvB.x - uvA.x) * (uvC.y - uvA.y)) -
                    ((uvC.x - uvA.x) * (uvB.y - uvA.y)));
                if (!math.isfinite(uvArea) || uvArea <= 1e-10f)
                    throw new InvalidOperationException("Architect topology validation rejected zero-area UV triangle.");
            }
        }

        private static bool IsFinite(Vector3 value)
        {
            return !float.IsNaN(value.x) && !float.IsInfinity(value.x) &&
                   !float.IsNaN(value.y) && !float.IsInfinity(value.y) &&
                   !float.IsNaN(value.z) && !float.IsInfinity(value.z);
        }

        private static SerializedProperty RequireProperty(SerializedObject serializedObject, string propertyPath)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyPath);
            if (property == null)
                throw new InvalidOperationException("Missing serialized field " + serializedObject.targetObject.GetType().Name + "." + propertyPath);

            return property;
        }

        private static SerializedProperty RequireRelativeProperty(SerializedProperty parent, string propertyPath)
        {
            SerializedProperty property = parent.FindPropertyRelative(propertyPath);
            if (property == null)
                throw new InvalidOperationException("Missing serialized relative field " + parent.propertyPath + "." + propertyPath);

            return property;
        }

        private static string NormalizeAssetPath(string assetPath)
        {
            return string.IsNullOrWhiteSpace(assetPath) ? string.Empty : assetPath.Replace('\\', '/').Trim();
        }

        private static string NormalizeAssetFolder(string assetFolder)
        {
            string normalized = NormalizeAssetPath(assetFolder).TrimEnd('/');
            if (string.IsNullOrEmpty(normalized) ||
                normalized == "Assets" ||
                !normalized.StartsWith("Assets/", StringComparison.Ordinal) ||
                normalized.IndexOf("//", StringComparison.Ordinal) >= 0)
            {
                throw new InvalidOperationException("ModuleArchitect1712 output folder must be a valid Assets/... path.");
            }

            return normalized;
        }

        private static void EnsureAssetFolder(string assetFolder)
        {
            assetFolder = NormalizeAssetFolder(assetFolder);
            if (AssetDatabase.IsValidFolder(assetFolder))
                return;

            string[] split = assetFolder.Split('/');
            string current = split[0];
            for (int i = 1; i < split.Length; i++)
            {
                string next = current + "/" + split[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, split[i]);
                current = next;
            }
        }

        [Flags]
        private enum SocketMask
        {
            None = 0,
            North = 1 << 0,
            South = 1 << 1,
            East = 1 << 2,
            West = 1 << 3,
            Top = 1 << 4,
            Bottom = 1 << 5,
            NorthSouth = North | South,
            NorthEastWest = North | East | West,
            Vertical = Top | Bottom,
            Cross = North | South | East | West
        }

        private readonly struct ModuleSpec
        {
            /// <summary>
            /// Compatibility lane for one socket direction, for the two sockets in the authored kit
            /// that are not on the Habitat lane. A direction with no override uses
            /// <see cref="HabitatSocketLane"/>, which is what 17 of the 19 authored sockets use.
            /// </summary>
            public readonly struct SocketLaneOverride
            {
                public SocketLaneOverride(ModuleSocketDirection direction, string lane)
                {
                    Direction = direction;
                    Lane = lane;
                }

                public ModuleSocketDirection Direction { get; }

                public string Lane { get; }
            }

            public readonly string Name;

            /// <summary>
            /// The authored construction material this module wears. Not optional: the generator
            /// throws when it is missing, because a shared default was how all six modules ended up
            /// wearing one texture set.
            /// </summary>
            public readonly string MaterialPath;

            public readonly float3 Extents;
            public readonly SocketMask SocketMask;
            public readonly uint Seed;
            public readonly BuildableFamily Family;
            public readonly float PowerRatingWatts;
            public readonly int PowerPriority;
            public readonly bool IsStructuralAnchor;
            public readonly bool IsEmergencyAirlock;
            private readonly SocketLaneOverride[] _socketLaneOverrides;

            public ModuleSpec(string name, string materialPath, float3 extents, SocketMask socketMask, uint seed)
                : this(name, materialPath, extents, socketMask, seed, BuildableFamily.Habitat, GeneratedModulePowerRatingWatts, GeneratedModulePowerPriority, false, false)
            {
            }

            public ModuleSpec(
                string name,
                string materialPath,
                float3 extents,
                SocketMask socketMask,
                uint seed,
                BuildableFamily family,
                float powerRatingWatts,
                int powerPriority,
                bool isStructuralAnchor,
                bool isEmergencyAirlock,
                params SocketLaneOverride[] socketLaneOverrides)
            {
                Name = name;
                MaterialPath = materialPath;
                Extents = extents;
                SocketMask = socketMask;
                Seed = seed;
                Family = family;
                PowerRatingWatts = powerRatingWatts;
                PowerPriority = math.clamp(powerPriority, 0, 100);
                IsStructuralAnchor = isStructuralAnchor;
                IsEmergencyAirlock = isEmergencyAirlock;
                _socketLaneOverrides = socketLaneOverrides;
            }

            /// <summary>
            /// Resolves the authored compatibility lane for one socket direction. Linear scan over at
            /// most one entry, in an editor-only cold bake path called once per socket.
            /// </summary>
            public string ResolveSocketLane(ModuleSocketDirection direction)
            {
                if (_socketLaneOverrides != null)
                {
                    for (int i = 0; i < _socketLaneOverrides.Length; i++)
                    {
                        if (_socketLaneOverrides[i].Direction == direction)
                            return _socketLaneOverrides[i].Lane;
                    }
                }

                return HabitatSocketLane;
            }
        }
    }

    /// <summary>
    /// Offline wear bake. Consumes the per-vertex surface attributes the geometry builders emitted
    /// and applies the channel contract of `3dmodel.md` section 4 and
    /// `3DMODEL_HARD_SURFACE_MODULES.md` section 5 literally:
    /// R = edge wear as <c>convexity * exposureMask * materialWearCoefficient</c>,
    /// G = grime as <c>cavity * downwardBias * wetnessRoute</c>,
    /// B = ambient occlusion, high on exposed faces and low in crevices,
    /// A = decal eligibility, or an emissive seam strip at or above the emissive threshold.
    /// </summary>
    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    internal struct ModuleArchitect1712WearJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<float3> Normals;
        [ReadOnly] public NativeArray<float4> Surface;
        public NativeArray<uint> Colors;
        public float GlobalQualityWeight;
        public uint Seed;

        public void Execute(int index)
        {
            float3 n = math.normalizesafe(Normals[index], new float3(0f, 1f, 0f));
            float4 s = Surface[index];
            float q = math.saturate(GlobalQualityWeight);
            float convexity = math.saturate(s.x);
            float cavity = math.saturate(s.y);
            float decalOrEmissive = math.saturate(s.w);
            float wearCoefficient = ResolveWearCoefficient((int)math.round(s.z));

            // Upward faces are salt-polished and rain-washed; downward faces trap water and silt.
            float exposure = math.saturate(0.35f + (0.65f * ((n.y * 0.5f) + 0.5f)));
            float downwardBias = math.saturate(0.5f - (n.y * 0.5f));
            float noise = Hash01((uint)index ^ Seed);

            float wear = math.saturate((convexity * exposure * wearCoefficient) + ((noise - 0.5f) * 0.10f));
            float grime = math.saturate((cavity * (0.35f + downwardBias) * (0.40f + (0.60f * q))) + ((noise - 0.5f) * 0.08f));
            float occlusion = math.saturate(1f - cavity);

            byte r = ToByte(wear);
            byte g = ToByte(grime);
            byte b = ToByte(occlusion);
            byte a = ToByte(decalOrEmissive);
            Colors[index] = (uint)r |
                            ((uint)g << 8) |
                            ((uint)b << 16) |
                            ((uint)a << 24);
        }

        /// <summary>
        /// `materialWearCoefficient` of the section 5 wear formula, by surface role. Exposed convex
        /// rims wear hardest; recessed step walls barely wear at all. Kept as a switch so Burst
        /// resolves it to a jump table with no managed lookup.
        /// </summary>
        private static float ResolveWearCoefficient(int role)
        {
            switch (role)
            {
                case 0: return 0.35f;   // Panel
                case 1: return 0.75f;   // Frame
                case 2: return 0.85f;   // Rib
                case 3: return 1.00f;   // Chamfer
                case 4: return 0.20f;   // StepWall
                case 5: return 0.90f;   // DoorFlange
                case 6: return 1.00f;   // DoorLip
                case 7: return 0.45f;   // Collar
                case 8: return 0.55f;   // Gasket
                case 9: return 0.95f;   // Bolt
                case 10: return 0.70f;  // Plate
                case 11: return 0.40f;  // Conduit
                case 12: return 1.00f;  // Bevel
                case 13: return 0.80f;  // Rim
                default: return 0.50f;
            }
        }

        private static float Hash01(uint value)
        {
            value ^= value >> 16;
            value *= 0x7FEB352Du;
            value ^= value >> 15;
            value *= 0x846CA68Bu;
            value ^= value >> 16;
            return (value & 0x00FFFFFFu) * (1f / 16777215f);
        }

        private static byte ToByte(float value)
        {
            return (byte)math.clamp((int)math.round(math.saturate(value) * 255f), 0, 255);
        }
    }
}
#endif
