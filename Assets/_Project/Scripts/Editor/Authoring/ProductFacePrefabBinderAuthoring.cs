#if UNITY_EDITOR
// ============================================================================
// HECTON-8 - ProductFacePrefabBinderAuthoring.cs
//
// FIRST_20_MINUTES moment served: the "tool interaction" node of the required
// route chain in Docs/ARCHITECTURE/FIRST_20_MINUTES_VERTICAL_SLICE_CONTRACT.md
// ("boot -> world load -> semi-open beautiful shallow exit -> swim -> find
// resource -> tool interaction -> craft/repair/build", line 33) and its
// acceptance row ("| Tool | One tool interaction is useful on that route: scan,
// cut, repair, drill, or harvest. |", line 82). The route blocker removed is
// literal: the object the player finds and the object the player holds are both
// Unity built-in primitive cubes today, so the tool moment cannot pass the
// TASTE.md Pillar 5 product-face bar or 3dmodel.md section 0 Prime 3D Product Law
// no matter how good the interaction code is.
//
// WHAT IS ACTUALLY BROKEN (verified against live assets, not against a report):
//
//   1. 22 generated mesh assets exist and NOTHING references them. Verified by
//      directory listing of Assets/_Project/Art/Generated/ProductFace/Tools
//      (12 GEN_Tool_*_Source_LOD0.asset) and .../PlayerSuit (10
//      GEN_PlayerSuit_*_Source_LOD0.asset).
//   2. All 13 held-tool prefabs under Assets/_Project/Prefabs/Tools/Held and all
//      13 world-tool prefabs under Assets/_Project/Prefabs/Items/Tools still bind
//      the Unity built-in cube (mesh fileID 10202, built-in resources GUID).
//   3. The scale trap is NOT on the visual child alone. It is split across the
//      chain. Measured from the prefab YAML:
//         Tool_Knife_Held        root localScale (0.08, 0.04, 0.70), visual child (1,1,1)
//         Tool_LaserCutter_Held  root (1,1,1),  VisualBody (0.10, 0.08, 0.44)
//         Tool_Propulsion_Held   root (0.18, 0.18, 0.60), VisualBody (1,1,1)
//         Item_Tool_*_World      root (0.8, 0.8, 0.8) and the MeshFilter is ON THE ROOT
//      Setting sharedMesh without neutralising the accumulated scale renders a
//      0.790 m knife as 0.0632 m on X and 0.0088 m on Y - a 12.5x / 25x error.
//
// WHY A COMPENSATING "VIS_" CHILD INSTEAD OF ZEROING THE HOST SCALE:
//
//   Six held visual hosts carry a BoxCollider (Builder/VisualBody,
//   Flashlight/Visual, LaserCutter/VisualBody, Propulsion/VisualBody,
//   Repair/VisualBody, Scanner/VisualBody) and all 13 world roots carry one.
//   3dmodel.md section 9 ("Collision is gameplay truth. Visual mesh detail is
//   player belief.") forbids letting an art pass move collision. Rewriting a
//   host localScale rewrites that collider's world extents. This binder
//   therefore mutates NO existing transform. It adds one child whose localScale
//   is the exact component-wise inverse of the accumulated prefab-asset scale,
//   so the composite root->child scale is identity and the mesh renders at its
//   authored metres with an undistorted normal basis. 3dmodel.md section 9 also
//   fixes the name: "Visual children must start with VIS_ or LOD_".
//
// WHY THE MESH IS YAWED -90 DEGREES:
//
//   Every generated tool mesh is authored long on X (measured m_LocalAABB extents:
//   Knife 0.395/0.110/0.090, HarpoonLauncher 0.457/0.202/0.173) with the working
//   nose at +X (ProductFaceToolMeshSourceAuthoring.cs:250 puts the blade at
//   +0.20 X, :238 puts the flashlight lens at +0.36 X). Every prefab blockout is
//   long on Z (Knife root z=0.70, HarpoonLauncher VisualBody z=0.75,
//   LaserCutter VisualBody z=0.44), and HandAnchor sits at local (0.3,-0.3,0.5)
//   under Main Camera so +Z is the aim axis. Quaternion.Euler(0,-90,0) maps
//   authored +X onto prefab +Z.
//
// PLAYER SUIT IS DECLINED, NOT FORGOTTEN. See DeclinePlayerSuitGroup for the
// full evidence chain. This binder never opens Player.prefab, so the GUID
// asserted at HectonPlayerSpawner.cs:64 cannot be disturbed by it.
//
// LOD CHAIN IS DEFERRED, NOT FAKED. See ToolLodChainExemptionReason.
//
// Editor-only authoring tool, not a test runner. AGENTS.md "Sandbox Firewall
// Rule" scopes its SaveAsPrefabAsset ban to automated test runners; this is an
// explicit, idempotent, operator-invoked authoring pass and it saves only the
// prefabs it actually changed.
// ============================================================================

namespace Hecton8.Editor.ProductFace
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Text;
    using UnityEditor;
    using UnityEngine;

    public static class ProductFacePrefabBinderAuthoring
    {
        private const string HeldPrefabFolder = "Assets/_Project/Prefabs/Tools/Held";
        private const string WorldPrefabFolder = "Assets/_Project/Prefabs/Items/Tools";
        private const string PlayerPrefabPath = "Assets/_Project/Prefabs/Player.prefab";
        private const string ToolMeshFolder = "Assets/_Project/Art/Generated/ProductFace/Tools";

        private const string MicroPanelFolder =
            "Assets/_Project/Art/Materials/Generated/ExternalPBR_20260607/Gemini_Batch20260607_MicroPanel";

        private const string MicroPanelAssetPrefix =
            "MAT_EXT_Gemini_Batch20260607_MicroPanel_gemini_Batch20260607_MicroPanel_";

        private const string LogPrefix = "[ProductFacePrefabBinder] ";
        private const string VisualChildPrefix = "VIS_";
        private const string VisualChildSuffix = "_LOD0";
        private const string VisualHostNamePrefix = "Visual";
        private const string HeldPrefabSuffix = "_Held.prefab";
        private const string WorldPrefabPrefix = "Item_";
        private const string WorldPrefabSuffix = "_World.prefab";
        private const string AssetPathRoot = "Assets/";

        private const int ToolMaterialSlotCount = 4;
        private const float ToolForwardYawDegrees = -90f;
        private const float ScaleIdentityTolerance = 0.0005f;
        private const float MinimumInvertibleScale = 0.0005f;
        private const int MaxHostSearchNodes = 512;

        // 3dmodel.md section 7, "Small prop/equipment" row: LOD0 hard maximum.
        private const int SmallPropLod0TriangleBudget = 6000;

        private const string ToolLodChainExemptionReason =
            "LOD chain DEFERRED to the mesh source generator. PROCEDURAL_ASSET_PIPELINE.md "
            + "\"Generation Order\" places silhouette-preserving decimation at step 8 and prefab "
            + "assembly at step 10, so LOD1/LOD2 belong to ProductFaceToolMeshSourceAuthoring, not to "
            + "this binder. Duplicating LOD0 into LOD1/LOD2 would create the identical-LOD defect and "
            + "is refused. Measured LOD0 triangle counts are 808-2364, inside the 3dmodel.md section 7 "
            + "small-prop LOD0 maximum of 6000, and held tools are viewmodels under 1 m from the camera "
            + "where no lower LOD would ever be selected. This string is the documented HLOD/merge "
            + "exemption required by PROCEDURAL_ASSET_PIPELINE.md \"Prefab Assembly Law\".";

        // Slot order is the generator's own contract, not an invention:
        // ProductFaceToolMeshSourceAuthoring.cs:26-29 declares SlotCasing=0,
        // SlotWear=1, SlotTrim=2, SlotGlassEmission=3. That matches 3dmodel.md
        // section 6 slot semantics (0 structural, 1 exposed cut/bevel/edge,
        // 2 secondary trim/gasket, 3 emissive/detail). Slot 3 reuses the exact
        // material the suit palette already assigns to its ViewportGlass slot
        // (ProductFacePlayerSuitGeminiMaterialApplier.cs:52), so glass identity
        // stays consistent across the product face instead of being re-picked.
        private static readonly string[] ToolSlotMaterialPaths =
        {
            MicroPanelFolder + "/" + MicroPanelAssetPrefix + "clean_graphite_panel.mat",
            MicroPanelFolder + "/" + MicroPanelAssetPrefix + "worn_steel_inset.mat",
            MicroPanelFolder + "/" + MicroPanelAssetPrefix + "fine_ribbed_trim.mat",
            MicroPanelFolder + "/" + MicroPanelAssetPrefix + "smoky_acrylic_glass.mat"
        };

        private static readonly string[] ToolSlotRoles =
        {
            "Slot0_Casing",
            "Slot1_Wear",
            "Slot2_Trim",
            "Slot3_GlassEmission"
        };

        // ────────────────────────────────────────────────────────────────────
        //  ENTRY POINTS
        // ────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Binds every generated ProductFace mesh source that has a geometrically valid
        /// prefab host, and reports every part it declined or skipped and why.
        /// Idempotent: a second run writes nothing.
        /// Batchmode entry point for -executeMethod.
        /// </summary>
        [MenuItem("Hecton8/Product Face/Bind Product Face Meshes To Prefabs", false, 1880)]
        public static void BindProductFaceMeshes()
        {
            BindReport report = Bind();
            EmitBindReport(report);
            ExitBatchmode(report.HasBlockingFailure);
        }

        /// <summary>
        /// Read-only binding gate. Names every part still hosting a non-project mesh,
        /// re-derives the scale compensation and asserts the composite is identity,
        /// and fails with a non-zero batchmode exit code. Writes nothing.
        /// Batchmode entry point for -executeMethod.
        /// </summary>
        [MenuItem("Hecton8/Product Face/Verify Product Face Bindings", false, 1881)]
        public static void VerifyProductFaceBindings()
        {
            VerifyReport report = Verify();
            EmitVerifyReport(report);
            ExitBatchmode(report.FailureCount > 0);
        }

        /// <summary>
        /// Programmatic bind for callers that want the structured result instead of the log.
        /// </summary>
        public static BindReport Bind()
        {
            BindReport report = new BindReport();

            Material[] toolSlotMaterials = ResolveToolSlotMaterials(report);

            ProductFaceToolMeshSourceAuthoring.ToolMeshSpec[] toolSpecs =
                ProductFaceToolMeshSourceAuthoring.GetSpecsForStaticAudit();

            HashSet<string> visitedPrefabPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < toolSpecs.Length; i++)
            {
                ProductFaceToolMeshSourceAuthoring.ToolMeshSpec spec = toolSpecs[i];
                string meshPath = ToolMeshFolder + "/" + spec.MeshAssetName + ".asset";
                Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);

                string heldPath = HeldPrefabFolder + "/" + spec.ToolId + HeldPrefabSuffix;
                string worldPath = WorldPrefabFolder + "/" + WorldPrefabPrefix + spec.ToolId + WorldPrefabSuffix;
                visitedPrefabPaths.Add(heldPath);
                visitedPrefabPaths.Add(worldPath);

                if (mesh == null)
                {
                    report.Skip(heldPath, spec.ToolId, "generated mesh source missing at " + meshPath);
                    report.Skip(worldPath, spec.ToolId, "generated mesh source missing at " + meshPath);
                    continue;
                }

                if (mesh.subMeshCount != ToolMaterialSlotCount)
                {
                    string reason = "mesh declares " + mesh.subMeshCount.ToString(CultureInfo.InvariantCulture)
                        + " submeshes but the tool material slot contract is "
                        + ToolMaterialSlotCount.ToString(CultureInfo.InvariantCulture)
                        + "; refusing to bind a partial material set";
                    report.Skip(heldPath, spec.ToolId, reason);
                    report.Skip(worldPath, spec.ToolId, reason);
                    continue;
                }

                if (toolSlotMaterials == null)
                {
                    report.Skip(heldPath, spec.ToolId, "tool slot material palette incomplete; see palette errors above");
                    report.Skip(worldPath, spec.ToolId, "tool slot material palette incomplete; see palette errors above");
                    continue;
                }

                BindOnePrefab(heldPath, spec.ToolId, mesh, toolSlotMaterials, report);
                BindOnePrefab(worldPath, spec.ToolId, mesh, toolSlotMaterials, report);
            }

            DeclineUnmatchedToolPrefabs(HeldPrefabFolder, visitedPrefabPaths, report);
            DeclineUnmatchedToolPrefabs(WorldPrefabFolder, visitedPrefabPaths, report);
            DeclinePlayerSuitGroup(report);

            if (report.PrefabsSaved > 0)
            {
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }

            return report;
        }

        /// <summary>
        /// Programmatic read-only verification for callers that want the structured result.
        /// </summary>
        public static VerifyReport Verify()
        {
            VerifyReport report = new VerifyReport();

            ProductFaceToolMeshSourceAuthoring.ToolMeshSpec[] toolSpecs =
                ProductFaceToolMeshSourceAuthoring.GetSpecsForStaticAudit();

            Dictionary<string, ExpectedBinding> expected =
                new Dictionary<string, ExpectedBinding>(64, StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < toolSpecs.Length; i++)
            {
                ProductFaceToolMeshSourceAuthoring.ToolMeshSpec spec = toolSpecs[i];
                ExpectedBinding binding = new ExpectedBinding(
                    spec.ToolId,
                    ToolMeshFolder + "/" + spec.MeshAssetName + ".asset");

                expected[HeldPrefabFolder + "/" + spec.ToolId + HeldPrefabSuffix] = binding;
                expected[WorldPrefabFolder + "/" + WorldPrefabPrefix + spec.ToolId + WorldPrefabSuffix] = binding;
            }

            VerifyFolder(HeldPrefabFolder, expected, report);
            VerifyFolder(WorldPrefabFolder, expected, report);
            AuditPlayerPrefabReadOnly(report);

            report.Defer("<all bound tool prefabs>", "lodChain", ToolLodChainExemptionReason);

            return report;
        }

        // ────────────────────────────────────────────────────────────────────
        //  BIND - ONE PREFAB
        // ────────────────────────────────────────────────────────────────────

        private static void BindOnePrefab(
            string prefabPath,
            string toolId,
            Mesh mesh,
            Material[] slotMaterials,
            BindReport report)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) == null)
            {
                report.Skip(prefabPath, toolId, "prefab asset not found");
                return;
            }

            GameObject contents = null;
            try
            {
                contents = PrefabUtility.LoadPrefabContents(prefabPath);
                if (contents == null)
                {
                    report.Skip(prefabPath, toolId, "PrefabUtility.LoadPrefabContents returned null");
                    return;
                }

                Transform host = ResolveVisualHost(contents, out string hostFailure);
                if (host == null)
                {
                    report.Skip(prefabPath, toolId, hostFailure);
                    return;
                }

                if (!TryResolveAccumulatedScale(contents.transform, host, out Vector3 accumulated, out string scaleFailure))
                {
                    report.Skip(prefabPath, toolId, scaleFailure);
                    return;
                }

                Vector3 compensation = new Vector3(
                    1f / accumulated.x,
                    1f / accumulated.y,
                    1f / accumulated.z);

                bool dirty = false;
                string childName = VisualChildPrefix + toolId + VisualChildSuffix;

                Transform visual = FindDirectChild(host, childName);
                if (visual == null)
                {
                    GameObject created = new GameObject(childName);
                    created.transform.SetParent(host, false);
                    created.layer = host.gameObject.layer;
                    created.tag = host.gameObject.tag;
                    visual = created.transform;
                    dirty = true;
                    report.CreatedChildCount++;
                }
                else if (visual.gameObject.layer != host.gameObject.layer
                    || !string.Equals(visual.gameObject.tag, host.gameObject.tag, StringComparison.Ordinal))
                {
                    visual.gameObject.layer = host.gameObject.layer;
                    visual.gameObject.tag = host.gameObject.tag;
                    dirty = true;
                }

                Quaternion targetRotation = Quaternion.Euler(0f, ToolForwardYawDegrees, 0f);
                if (!ApproximatelyEqual(visual.localPosition, Vector3.zero))
                {
                    visual.localPosition = Vector3.zero;
                    dirty = true;
                }

                if (Quaternion.Angle(visual.localRotation, targetRotation) > 0.01f)
                {
                    visual.localRotation = targetRotation;
                    dirty = true;
                }

                if (!ApproximatelyEqual(visual.localScale, compensation))
                {
                    visual.localScale = compensation;
                    dirty = true;
                }

                if (!visual.gameObject.TryGetComponent(out MeshFilter visualFilter))
                {
                    visualFilter = visual.gameObject.AddComponent<MeshFilter>();
                    dirty = true;
                }

                if (visualFilter.sharedMesh != mesh)
                {
                    visualFilter.sharedMesh = mesh;
                    dirty = true;
                    report.MeshesBound++;
                }

                if (!visual.gameObject.TryGetComponent(out MeshRenderer visualRenderer))
                {
                    visualRenderer = visual.gameObject.AddComponent<MeshRenderer>();
                    dirty = true;
                }

                if (CopyRendererPresentationFromHost(host, visualRenderer))
                    dirty = true;

                if (ApplySlotMaterials(visualRenderer, slotMaterials))
                {
                    dirty = true;
                    report.MaterialSlotsBound += ToolMaterialSlotCount;
                }

                int retired = RetireBuiltInPrimitiveMeshes(contents, visual);
                if (retired > 0)
                {
                    dirty = true;
                    report.PrimitivesRetired += retired;
                }

                int triangles = ResolveTriangleCount(mesh);
                if (triangles > SmallPropLod0TriangleBudget)
                {
                    report.Warn(
                        prefabPath,
                        toolId,
                        "LOD0 triangle count " + triangles.ToString(CultureInfo.InvariantCulture)
                        + " exceeds the 3dmodel.md section 7 small-prop budget of "
                        + SmallPropLod0TriangleBudget.ToString(CultureInfo.InvariantCulture));
                }

                if (dirty)
                {
                    PrefabUtility.SaveAsPrefabAsset(contents, prefabPath);
                    report.PrefabsSaved++;
                    report.Change(
                        prefabPath,
                        toolId,
                        "host=" + host.name
                        + " child=" + childName
                        + " accumulatedScale=" + FormatVector(accumulated)
                        + " compensation=" + FormatVector(compensation)
                        + " meshSize=" + FormatVector(mesh.bounds.size)
                        + " meshCentre=" + FormatVector(mesh.bounds.center)
                        + " tris=" + triangles.ToString(CultureInfo.InvariantCulture)
                        + " retiredPrimitives=" + retired.ToString(CultureInfo.InvariantCulture)
                        + " lodChain=DEFERRED_TO_GENERATOR");
                }
                else
                {
                    report.NoOp(prefabPath, toolId, "already bound to " + mesh.name + "; nothing written");
                }
            }
            finally
            {
                if (contents != null)
                    PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        /// <summary>
        /// The visual host is the single object whose name starts with "Visual" when exactly one
        /// exists, otherwise the prefab root when the root itself carries a MeshFilter. Both cases
        /// are real: every held prefab has exactly one "Visual*" child (Flashlight names it
        /// "Visual", the others "VisualBody" or "VisualBody_&lt;Tool&gt;"), and every world prefab
        /// carries its MeshFilter on the root with only "Detail_*" children. Anything else is
        /// refused instead of guessed.
        /// </summary>
        private static Transform ResolveVisualHost(GameObject contents, out string failure)
        {
            failure = string.Empty;

            List<Transform> candidates = new List<Transform>(8);
            int visitedNodeCount = 0;
            CollectVisualHostCandidates(contents.transform, candidates, ref visitedNodeCount);

            if (candidates.Count == 1)
                return candidates[0];

            if (candidates.Count > 1)
            {
                StringBuilder names = new StringBuilder(128);
                for (int i = 0; i < candidates.Count; i++)
                {
                    if (i > 0)
                        names.Append(", ");
                    names.Append(candidates[i].name);
                }

                failure = "ambiguous visual host: " + candidates.Count.ToString(CultureInfo.InvariantCulture)
                    + " objects start with \"" + VisualHostNamePrefix + "\" (" + names + ")";
                return null;
            }

            if (contents.TryGetComponent(out MeshFilter _))
                return contents.transform;

            failure = "no visual host: no object starts with \"" + VisualHostNamePrefix
                + "\" and the prefab root has no MeshFilter";
            return null;
        }

        private static void CollectVisualHostCandidates(
            Transform node,
            List<Transform> candidates,
            ref int visitedNodeCount)
        {
            if (node == null || visitedNodeCount >= MaxHostSearchNodes)
                return;

            int childCount = node.childCount;
            for (int i = 0; i < childCount; i++)
            {
                if (visitedNodeCount >= MaxHostSearchNodes)
                    return;

                visitedNodeCount++;
                Transform child = node.GetChild(i);
                if (child.name.StartsWith(VisualHostNamePrefix, StringComparison.OrdinalIgnoreCase)
                    && !child.name.StartsWith(VisualChildPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    candidates.Add(child);
                }

                CollectVisualHostCandidates(child, candidates, ref visitedNodeCount);
            }
        }

        /// <summary>
        /// Accumulated prefab-asset scale from the prefab root down to and including the host.
        /// Refuses any component too small to invert without exploding the compensation.
        /// </summary>
        private static bool TryResolveAccumulatedScale(
            Transform root,
            Transform host,
            out Vector3 accumulated,
            out string failure)
        {
            accumulated = Vector3.one;
            failure = string.Empty;

            Transform node = host;
            bool reachedRoot = false;
            for (int guard = 0; guard <= MaxHostSearchNodes; guard++)
            {
                if (node == null)
                    break;

                Vector3 local = node.localScale;
                accumulated = new Vector3(
                    accumulated.x * local.x,
                    accumulated.y * local.y,
                    accumulated.z * local.z);

                if (node == root)
                {
                    reachedRoot = true;
                    break;
                }

                node = node.parent;
            }

            if (!reachedRoot)
            {
                failure = "visual host is not parented under the prefab root within "
                    + MaxHostSearchNodes.ToString(CultureInfo.InvariantCulture) + " levels";
                return false;
            }

            if (!IsFinite(accumulated))
            {
                failure = "accumulated prefab scale is not finite: " + FormatVector(accumulated);
                return false;
            }

            if (Mathf.Abs(accumulated.x) < MinimumInvertibleScale
                || Mathf.Abs(accumulated.y) < MinimumInvertibleScale
                || Mathf.Abs(accumulated.z) < MinimumInvertibleScale)
            {
                failure = "accumulated prefab scale " + FormatVector(accumulated)
                    + " has a component below " + MinimumInvertibleScale.ToString("0.0000", CultureInfo.InvariantCulture)
                    + " and cannot be inverted without an unusable compensation";
                return false;
            }

            return true;
        }

        /// <summary>
        /// Copies authored render presentation off the host renderer so the bound mesh inherits the
        /// prefab's existing shadow, probe, and motion-vector decisions instead of new invented
        /// defaults. Returns true when anything changed.
        /// </summary>
        private static bool CopyRendererPresentationFromHost(Transform host, MeshRenderer target)
        {
            if (!host.TryGetComponent(out MeshRenderer source) || source == target)
                return false;

            bool changed = false;

            if (target.shadowCastingMode != source.shadowCastingMode)
            {
                target.shadowCastingMode = source.shadowCastingMode;
                changed = true;
            }

            if (target.receiveShadows != source.receiveShadows)
            {
                target.receiveShadows = source.receiveShadows;
                changed = true;
            }

            if (target.lightProbeUsage != source.lightProbeUsage)
            {
                target.lightProbeUsage = source.lightProbeUsage;
                changed = true;
            }

            if (target.reflectionProbeUsage != source.reflectionProbeUsage)
            {
                target.reflectionProbeUsage = source.reflectionProbeUsage;
                changed = true;
            }

            if (target.motionVectorGenerationMode != source.motionVectorGenerationMode)
            {
                target.motionVectorGenerationMode = source.motionVectorGenerationMode;
                changed = true;
            }

            if (target.allowOcclusionWhenDynamic != source.allowOcclusionWhenDynamic)
            {
                target.allowOcclusionWhenDynamic = source.allowOcclusionWhenDynamic;
                changed = true;
            }

            return changed;
        }

        private static bool ApplySlotMaterials(MeshRenderer renderer, Material[] slotMaterials)
        {
            Material[] current = renderer.sharedMaterials;
            bool changed = current == null || current.Length != slotMaterials.Length;

            if (!changed)
            {
                for (int i = 0; i < slotMaterials.Length; i++)
                {
                    if (current[i] != slotMaterials[i])
                    {
                        changed = true;
                        break;
                    }
                }
            }

            if (!changed)
                return false;

            Material[] assigned = new Material[slotMaterials.Length];
            for (int i = 0; i < slotMaterials.Length; i++)
                assigned[i] = slotMaterials[i];

            renderer.sharedMaterials = assigned;
            EditorUtility.SetDirty(renderer);
            return true;
        }

        /// <summary>
        /// Clears every MeshFilter in the prefab that still points at a mesh outside the project
        /// asset tree - the retired blockout host and every Detail_* stand-in cube whose geometry
        /// the generated mesh already contains. The MeshRenderer, its material array, its enabled
        /// state, and the transform are all left intact so a later art pass can reuse them and so no
        /// runtime code that caches those components loses its reference. Removing the built-in mesh
        /// reference is what clears
        /// WorldProceduralFinalPrefabQualityGate.AssetPathUsesUnityBuiltInPrimitiveMesh.
        /// </summary>
        private static int RetireBuiltInPrimitiveMeshes(GameObject contents, Transform boundVisual)
        {
            int retired = 0;
            MeshFilter[] filters = contents.GetComponentsInChildren<MeshFilter>(true);
            for (int i = 0; i < filters.Length; i++)
            {
                MeshFilter filter = filters[i];
                if (filter == null || filter.transform == boundVisual)
                    continue;

                Mesh mesh = filter.sharedMesh;
                if (mesh == null || IsProjectAssetMesh(mesh))
                    continue;

                filter.sharedMesh = null;
                EditorUtility.SetDirty(filter);
                retired++;
            }

            return retired;
        }

        /// <summary>
        /// A project mesh resolves to an asset path under "Assets/". Unity built-in primitives
        /// resolve outside it. This is a superset of the built-in primitive GUID test and needs no
        /// second copy of the GUID constant that WorldProceduralFinalPrefabQualityGate.cs:9 owns.
        /// </summary>
        private static bool IsProjectAssetMesh(Mesh mesh)
        {
            string path = AssetDatabase.GetAssetPath(mesh);
            return !string.IsNullOrEmpty(path)
                && path.StartsWith(AssetPathRoot, StringComparison.OrdinalIgnoreCase);
        }

        private static Material[] ResolveToolSlotMaterials(BindReport report)
        {
            Material[] materials = new Material[ToolSlotMaterialPaths.Length];
            bool complete = true;

            for (int i = 0; i < ToolSlotMaterialPaths.Length; i++)
            {
                materials[i] = AssetDatabase.LoadAssetAtPath<Material>(ToolSlotMaterialPaths[i]);
                if (materials[i] != null)
                    continue;

                complete = false;
                report.PaletteError(
                    ToolSlotRoles[i],
                    "material missing at " + ToolSlotMaterialPaths[i]
                    + "; run Hecton8/Art/Apply External PBR To Held Tools or the ExternalPbrTexturePackImporter first");
            }

            return complete ? materials : null;
        }

        private static void DeclineUnmatchedToolPrefabs(
            string folder,
            HashSet<string> expectedPrefabPaths,
            BindReport report)
        {
            if (!AssetDatabase.IsValidFolder(folder))
            {
                report.PaletteError(folder, "prefab folder missing");
                return;
            }

            string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { folder });
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (string.IsNullOrEmpty(path) || expectedPrefabPaths.Contains(path))
                    continue;

                report.Decline(
                    path,
                    "no ProductFace tool mesh spec claims this prefab; "
                    + "ProductFaceToolMeshSourceAuthoring declares exactly 12 tool specs "
                    + "(ProductFaceToolMeshSourceAuthoring.cs:187) and this prefab is not one of them. "
                    + "Refusing to substitute another tool's mesh.");
            }
        }

        /// <summary>
        /// The whole player-suit group is declined by contract. Every reason below is measured or
        /// cited, not assumed, and the exact condition that lifts the decline is named.
        /// </summary>
        private static void DeclinePlayerSuitGroup(BindReport report)
        {
            report.PlayerSuitAudited = true;

            ProductFacePlayerSuitMeshSourceAuthoring.SuitPartSpec[] suitSpecs =
                ProductFacePlayerSuitMeshSourceAuthoring.GetSpecsForStaticAudit();

            for (int i = 0; i < suitSpecs.Length; i++)
            {
                ProductFacePlayerSuitMeshSourceAuthoring.SuitPartSpec spec = suitSpecs[i];
                report.Decline(
                    PlayerPrefabPath + " :: " + spec.SourceName,
                    ResolvePlayerSuitDeclineReason(spec));
            }
        }

        private static string ResolvePlayerSuitDeclineReason(
            ProductFacePlayerSuitMeshSourceAuthoring.SuitPartSpec spec)
        {
            switch (spec.Kind)
            {
                case ProductFacePlayerSuitMeshSourceAuthoring.SuitPartKind.LeftThighCalfFin:
                case ProductFacePlayerSuitMeshSourceAuthoring.SuitPartKind.RightThighCalfFin:
                    return "UNBINDABLE HOST. The only scale-safe hosts are the Swim_*ThighAttachment / "
                        + "CalfAttachment / FinAttachment nodes, and PlayerSwimBlockoutRig.Body.cs:934 gives "
                        + "each of them a ResolveLookRotationNoTrig(boneDirection) rotation, applied at :944, so "
                        + "their rest rotation is roughly 90 degrees off identity and it changes every frame. "
                        + "The mesh also spans three bones whose relative angles are driven per frame by "
                        + "ApplyLegPose (PlayerSwimBlockoutRig.Body.cs:767-873), so no rigid child transform can "
                        + "track it. Lift condition: the generator must emit one mesh per bone in that bone's "
                        + "local space, or a SkinnedMeshRenderer with bindposes against the Swim_* transforms.";

                case ProductFacePlayerSuitMeshSourceAuthoring.SuitPartKind.HelmetVisorHousing:
                case ProductFacePlayerSuitMeshSourceAuthoring.SuitPartKind.VisorGlassSupportRim:
                    return "NO CAMERA-PARENTED HEAD NODE. Suit_Visor is a SIBLING of Main Camera under the "
                        + "Player root, not a child of it, so it inherits player yaw but never camera pitch. A "
                        + "helmet housing and visor rim bound there would slide off the view the moment the "
                        + "player looks up or down. Lift condition: a camera-parented head/visor node must exist "
                        + "in Player.prefab before these two meshes have a correct host.";

                default:
                    return "GROUP HANDOFF IS ALL-OR-NOTHING. PlayerSwimBlockoutRig exposes a single serialized "
                        + "showDebugCubes switch for all 16 blockout cubes (PlayerSwimBlockoutRig.cs:94), gated "
                        + "into every renderer decision at :804, :823 and Body.cs:894, :940. Binding only the "
                        + "parts that do have a valid host leaves either double geometry (cubes on) or a "
                        + "headless, legless suit (cubes off) - both worse than the current blockout. This part "
                        + "has a geometrically valid scale-safe host (its Swim_*Attachment node is forced to "
                        + "localScale one at PlayerSwimBlockoutRig.cs:930-931) and a derivable offset "
                        + "(-spec.Center = " + FormatVector(-spec.Center) + "), so it is blocked only by the two "
                        + "unbindable leg meshes and the two homeless visor meshes. Additional measured "
                        + "mismatch: the generated body is a different proportion set from the blockout "
                        + "(generated leg span 0.93 m on Y versus blockout thigh-top to fin-bottom 0.55 m), so "
                        + "the generator output needs a proportion pass as well. Player.prefab is GUID-asserted "
                        + "at HectonPlayerSpawner.cs:64 and this binder never opens it.";
            }
        }

        // ────────────────────────────────────────────────────────────────────
        //  VERIFY
        // ────────────────────────────────────────────────────────────────────

        private static void VerifyFolder(
            string folder,
            Dictionary<string, ExpectedBinding> expected,
            VerifyReport report)
        {
            if (!AssetDatabase.IsValidFolder(folder))
            {
                report.Fail(folder, string.Empty, "prefab folder missing");
                return;
            }

            string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { folder });
            List<string> paths = new List<string>(guids.Length);
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (!string.IsNullOrEmpty(path))
                    paths.Add(path);
            }

            paths.Sort(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < paths.Count; i++)
            {
                string path = paths[i];
                report.PrefabsChecked++;

                if (!expected.TryGetValue(path, out ExpectedBinding binding))
                {
                    report.Defer(
                        path,
                        string.Empty,
                        "no ProductFace tool mesh spec claims this prefab; declined by the binder, not a failure");
                    continue;
                }

                VerifyOnePrefab(path, binding, report);
            }
        }

        private static void VerifyOnePrefab(string prefabPath, ExpectedBinding binding, VerifyReport report)
        {
            string toolId = binding.ToolId;
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
            {
                report.Fail(prefabPath, toolId, "prefab asset could not be loaded");
                return;
            }

            // Authoritative coarse verdict. Reuses the project's own constant instead of
            // declaring a second copy of it (WorldProceduralFinalPrefabQualityGate.cs:9).
            bool coarseBuiltIn = WorldProceduralFinalPrefabQualityGate.AssetPathUsesUnityBuiltInPrimitiveMesh(prefabPath);

            string childName = VisualChildPrefix + toolId + VisualChildSuffix;
            Mesh expectedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(binding.MeshAssetPath);

            int nonProjectMeshCount = 0;
            bool boundChildFound = false;

            MeshFilter[] filters = prefab.GetComponentsInChildren<MeshFilter>(true);
            for (int i = 0; i < filters.Length; i++)
            {
                MeshFilter filter = filters[i];
                if (filter == null)
                    continue;

                Mesh mesh = filter.sharedMesh;
                if (mesh != null && !IsProjectAssetMesh(mesh))
                {
                    nonProjectMeshCount++;
                    report.Fail(
                        prefabPath,
                        toolId,
                        "still on a non-project mesh: transform=" + ResolveTransformPath(prefab.transform, filter.transform)
                        + " mesh=" + mesh.name);
                    continue;
                }

                if (!string.Equals(filter.name, childName, StringComparison.Ordinal))
                    continue;

                boundChildFound = true;
                VerifyBoundChild(prefab, filter, toolId, expectedMesh, report);
            }

            if (coarseBuiltIn && nonProjectMeshCount == 0)
            {
                report.Fail(
                    prefabPath,
                    toolId,
                    "WorldProceduralFinalPrefabQualityGate reports built-in primitive mesh ids in the prefab text "
                    + "but the object graph walk found none. The project gate wins; inspect the prefab manually.");
            }

            if (!boundChildFound)
                report.Fail(prefabPath, toolId, "expected bound visual child \"" + childName + "\" is absent");
        }

        private static void VerifyBoundChild(
            GameObject prefab,
            MeshFilter filter,
            string toolId,
            Mesh expectedMesh,
            VerifyReport report)
        {
            string prefabPath = AssetDatabase.GetAssetPath(prefab);

            if (expectedMesh == null)
            {
                report.Fail(prefabPath, toolId, "expected generated mesh source is missing from disk");
                return;
            }

            if (filter.sharedMesh != expectedMesh)
            {
                report.Fail(
                    prefabPath,
                    toolId,
                    "bound child references " + (filter.sharedMesh == null ? "nothing" : filter.sharedMesh.name)
                    + " instead of " + expectedMesh.name);
                return;
            }

            if (!filter.gameObject.TryGetComponent(out MeshRenderer renderer))
            {
                report.Fail(prefabPath, toolId, "bound child has no MeshRenderer");
                return;
            }

            Material[] materials = renderer.sharedMaterials;
            int expectedSlots = expectedMesh.subMeshCount;
            if (materials == null || materials.Length != expectedSlots)
            {
                report.Fail(
                    prefabPath,
                    toolId,
                    "bound child has " + (materials == null ? 0 : materials.Length).ToString(CultureInfo.InvariantCulture)
                    + " material slots but the mesh declares " + expectedSlots.ToString(CultureInfo.InvariantCulture)
                    + " submeshes");
            }
            else
            {
                for (int i = 0; i < materials.Length; i++)
                {
                    if (materials[i] == null)
                    {
                        report.Fail(
                            prefabPath,
                            toolId,
                            "material slot " + i.ToString(CultureInfo.InvariantCulture) + " is empty");
                    }
                }
            }

            // Re-derive the scale claim on live data. This is the runnable proof that the
            // compensation math is consumed and correct, and it is why no EditMode test was added:
            // Hecton8.EditModeTests.asmdef does reference this assembly ("Hecton8.Editor") but it
            // carries defineConstraints ["NEVER_COMPILE_TESTS"], so any test placed there never
            // compiles and never runs. A gate that re-derives the math against the real prefabs is
            // strictly stronger evidence than a test that cannot execute.
            if (!TryResolveAccumulatedScale(prefab.transform, filter.transform, out Vector3 composite, out string scaleFailure))
            {
                report.Fail(prefabPath, toolId, scaleFailure);
                return;
            }

            if (Mathf.Abs(composite.x - 1f) > ScaleIdentityTolerance
                || Mathf.Abs(composite.y - 1f) > ScaleIdentityTolerance
                || Mathf.Abs(composite.z - 1f) > ScaleIdentityTolerance)
            {
                report.Fail(
                    prefabPath,
                    toolId,
                    "composite root-to-visual scale is " + FormatVector(composite)
                    + ", not identity; the generated mesh would render at the wrong size");
                return;
            }

            Vector3 size = expectedMesh.bounds.size;
            report.Pass(
                prefabPath,
                toolId,
                "mesh=" + expectedMesh.name
                + " compositeScale=" + FormatVector(composite)
                + " renderedSize=" + FormatVector(size)
                + " tris=" + ResolveTriangleCount(expectedMesh).ToString(CultureInfo.InvariantCulture)
                + " slots=" + expectedSlots.ToString(CultureInfo.InvariantCulture));
        }

        /// <summary>
        /// Player.prefab is read, never written. Its primitives are reported under the deferred
        /// bucket with the decline reason so the debt stays loudly visible without turning the gate
        /// permanently red for a route this binder deliberately refused.
        /// </summary>
        private static void AuditPlayerPrefabReadOnly(VerifyReport report)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            if (prefab == null)
            {
                report.Fail(PlayerPrefabPath, string.Empty, "Player.prefab is missing");
                return;
            }

            report.PrefabsChecked++;

            int primitiveCount = 0;
            MeshFilter[] filters = prefab.GetComponentsInChildren<MeshFilter>(true);
            for (int i = 0; i < filters.Length; i++)
            {
                MeshFilter filter = filters[i];
                if (filter == null || filter.sharedMesh == null || IsProjectAssetMesh(filter.sharedMesh))
                    continue;

                primitiveCount++;
                report.Defer(
                    PlayerPrefabPath,
                    filter.name,
                    "SUIT_BINDING_DEFERRED: still on built-in primitive " + filter.sharedMesh.name);
            }

            report.PlayerSuitDeferredPrimitiveCount = primitiveCount;
        }

        // ────────────────────────────────────────────────────────────────────
        //  REPORTING
        // ────────────────────────────────────────────────────────────────────

        private static void EmitBindReport(BindReport report)
        {
            StringBuilder builder = new StringBuilder(4096);
            builder.Append(LogPrefix).Append("BIND SUMMARY").AppendLine();
            builder.Append("  changed=").Append(report.Changes.Count)
                .Append(" unchanged=").Append(report.NoOps.Count)
                .Append(" skipped=").Append(report.Skipped.Count)
                .Append(" declined=").Append(report.Declined.Count)
                .Append(" warnings=").Append(report.Warnings.Count)
                .Append(" paletteErrors=").Append(report.PaletteErrors.Count)
                .AppendLine();
            builder.Append("  prefabsSaved=").Append(report.PrefabsSaved)
                .Append(" childrenCreated=").Append(report.CreatedChildCount)
                .Append(" meshesBound=").Append(report.MeshesBound)
                .Append(" materialSlotsBound=").Append(report.MaterialSlotsBound)
                .Append(" primitivesRetired=").Append(report.PrimitivesRetired)
                .Append(" playerSuitAudited=").Append(report.PlayerSuitAudited)
                .AppendLine();
            builder.Append("  lodChain=DEFERRED_TO_GENERATOR :: ").Append(ToolLodChainExemptionReason).AppendLine();

            AppendSection(builder, "CHANGED", report.Changes);
            AppendSection(builder, "UNCHANGED (idempotent no-op)", report.NoOps);
            AppendSection(builder, "SKIPPED", report.Skipped);
            AppendSection(builder, "DECLINED", report.Declined);
            AppendSection(builder, "WARNINGS", report.Warnings);
            AppendSection(builder, "PALETTE ERRORS", report.PaletteErrors);

            string text = builder.ToString();
            if (report.HasBlockingFailure)
                Debug.LogError(text);
            else if (report.Warnings.Count > 0 || report.Skipped.Count > 0)
                Debug.LogWarning(text);
            else
                Debug.Log(text);
        }

        private static void EmitVerifyReport(VerifyReport report)
        {
            StringBuilder builder = new StringBuilder(4096);
            builder.Append(LogPrefix).Append("VERIFY SUMMARY").AppendLine();
            builder.Append("  prefabsChecked=").Append(report.PrefabsChecked)
                .Append(" passed=").Append(report.Passed.Count)
                .Append(" FAILED=").Append(report.FailureCount)
                .Append(" deferred=").Append(report.Deferred.Count)
                .Append(" playerSuitDeferredPrimitives=").Append(report.PlayerSuitDeferredPrimitiveCount)
                .AppendLine();

            AppendSection(builder, "PASSED", report.Passed);
            AppendSection(builder, "FAILED", report.Failures);
            AppendSection(builder, "DEFERRED (not counted in the exit code)", report.Deferred);

            string text = builder.ToString();
            if (report.FailureCount > 0)
                Debug.LogError(text);
            else
                Debug.Log(text);
        }

        private static void AppendSection(StringBuilder builder, string title, List<string> lines)
        {
            if (lines.Count == 0)
                return;

            builder.Append("  -- ").Append(title).Append(" (").Append(lines.Count).Append(") --").AppendLine();
            for (int i = 0; i < lines.Count; i++)
                builder.Append("    ").Append(lines[i]).AppendLine();
        }

        private static void ExitBatchmode(bool failed)
        {
            if (!Application.isBatchMode)
                return;

            EditorApplication.Exit(failed ? 1 : 0);
        }

        // ────────────────────────────────────────────────────────────────────
        //  MATH AND HELPERS
        // ────────────────────────────────────────────────────────────────────

        private static Transform FindDirectChild(Transform parent, string childName)
        {
            int childCount = parent.childCount;
            for (int i = 0; i < childCount; i++)
            {
                Transform child = parent.GetChild(i);
                if (string.Equals(child.name, childName, StringComparison.Ordinal))
                    return child;
            }

            return null;
        }

        private static string ResolveTransformPath(Transform root, Transform node)
        {
            StringBuilder builder = new StringBuilder(96);
            Transform current = node;
            int guard = 0;
            while (current != null && guard <= MaxHostSearchNodes)
            {
                if (builder.Length > 0)
                    builder.Insert(0, '/');

                builder.Insert(0, current.name);
                if (current == root)
                    break;

                current = current.parent;
                guard++;
            }

            return builder.ToString();
        }

        private static int ResolveTriangleCount(Mesh mesh)
        {
            if (mesh == null)
                return 0;

            int total = 0;
            for (int i = 0; i < mesh.subMeshCount; i++)
                total += (int)mesh.GetIndexCount(i) / 3;

            return total;
        }

        private static bool ApproximatelyEqual(Vector3 a, Vector3 b)
        {
            return Mathf.Abs(a.x - b.x) <= ScaleIdentityTolerance
                && Mathf.Abs(a.y - b.y) <= ScaleIdentityTolerance
                && Mathf.Abs(a.z - b.z) <= ScaleIdentityTolerance;
        }

        private static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static string FormatVector(Vector3 value)
        {
            return "("
                + value.x.ToString("0.#####", CultureInfo.InvariantCulture) + ", "
                + value.y.ToString("0.#####", CultureInfo.InvariantCulture) + ", "
                + value.z.ToString("0.#####", CultureInfo.InvariantCulture) + ")";
        }

        // ────────────────────────────────────────────────────────────────────
        //  REPORT TYPES
        // ────────────────────────────────────────────────────────────────────

        private readonly struct ExpectedBinding
        {
            public readonly string ToolId;
            public readonly string MeshAssetPath;

            public ExpectedBinding(string toolId, string meshAssetPath)
            {
                ToolId = toolId;
                MeshAssetPath = meshAssetPath;
            }
        }

        public sealed class BindReport
        {
            public readonly List<string> Changes = new List<string>(64);
            public readonly List<string> NoOps = new List<string>(64);
            public readonly List<string> Skipped = new List<string>(32);
            public readonly List<string> Declined = new List<string>(32);
            public readonly List<string> Warnings = new List<string>(32);
            public readonly List<string> PaletteErrors = new List<string>(8);

            public int PrefabsSaved;
            public int CreatedChildCount;
            public int MeshesBound;
            public int MaterialSlotsBound;
            public int PrimitivesRetired;
            public bool PlayerSuitAudited;

            /// <summary>
            /// Blocking when the material palette is incomplete, or when the pass ended with
            /// nothing bound at all - a silent zero-work run must not report success.
            /// </summary>
            public bool HasBlockingFailure =>
                PaletteErrors.Count > 0 || (Changes.Count == 0 && NoOps.Count == 0);

            public void Change(string prefabPath, string partId, string detail)
            {
                Changes.Add(prefabPath + " :: " + partId + " :: " + detail);
            }

            public void NoOp(string prefabPath, string partId, string detail)
            {
                NoOps.Add(prefabPath + " :: " + partId + " :: " + detail);
            }

            public void Skip(string prefabPath, string partId, string reason)
            {
                Skipped.Add(prefabPath + " :: " + partId + " :: SKIPPED: " + reason);
            }

            public void Decline(string target, string reason)
            {
                Declined.Add(target + " :: DECLINED: " + reason);
            }

            public void Warn(string prefabPath, string partId, string detail)
            {
                Warnings.Add(prefabPath + " :: " + partId + " :: " + detail);
            }

            public void PaletteError(string target, string reason)
            {
                PaletteErrors.Add(target + " :: " + reason);
            }
        }

        public sealed class VerifyReport
        {
            public readonly List<string> Passed = new List<string>(64);
            public readonly List<string> Failures = new List<string>(64);
            public readonly List<string> Deferred = new List<string>(64);

            public int PrefabsChecked;
            public int PlayerSuitDeferredPrimitiveCount;

            public int FailureCount => Failures.Count;

            public void Pass(string prefabPath, string partId, string detail)
            {
                Passed.Add(prefabPath + " :: " + partId + " :: " + detail);
            }

            public void Fail(string prefabPath, string partId, string reason)
            {
                Failures.Add(prefabPath + " :: " + partId + " :: FAILED: " + reason);
            }

            public void Defer(string prefabPath, string partId, string reason)
            {
                Deferred.Add(prefabPath + " :: " + partId + " :: " + reason);
            }
        }
    }
}
#endif
