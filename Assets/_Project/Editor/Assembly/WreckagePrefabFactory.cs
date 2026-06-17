#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using Hecton8.Interaction;
using Hecton8.World;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

namespace Hecton8.Editor.Assembly
{
    public sealed class WreckagePrefabFactory : EditorWindow
    {
        private const string AgentId = "1735";
        private const string MenuRoot = "Hecton8/Assembly/1735/";
        private const string DefaultSourceDirectory = "Assets/_Project/BakedGeometry/Wreckage";
        private const string DefaultMaterialDirectory = "Assets/_Project/Art/Materials";
        private const string DefaultGeneratedMeshDirectory = "Assets/_Project/BakedGeometry/Wreckage/PrefabFactory1735";
        private const string DefaultOutputDirectory = "Assets/Prefabs/Environment/Wrecks";
        private const string DefaultReportPath = "Docs/Reports/WRECKAGE_ASSEMBLER_REPORT_1735.json";
        private const bool DefaultWriteReportToDisk = false;
        private const string WreckIndirectShaderName = "Hecton8/World/WreckIndirectLit";
        private const int MaxMaterialSlots = 4;
        private const int MaxVisualRendererCount = 2;
        private const int MaxExpectedSourceGroups = 64;
        private const int MaxExpectedAssetPaths = 512;
        private const int MaxExpectedHullSegmentsPerGroup = 16;
        private const int MaxExpectedDebrisSegmentsPerGroup = 512;
        private const int MaxExpectedCombineInstancesPerMaterial = 512;
        private const int LowestVertexPercent = 20;
        private const float DefaultBurialDepthMeters = 1f;
        private const float MinimumCarveExtentMeters = 0.18f;
        private const float DefaultAuthoredQualityWeight = 0.72f;
        private const byte VoxelSubtractOperation = 0;
        private const byte VoxelBoxShape = 1;

        private string _sourceDirectory = DefaultSourceDirectory;
        private string _materialDirectory = DefaultMaterialDirectory;
        private string _generatedMeshDirectory = DefaultGeneratedMeshDirectory;
        private string _outputDirectory = DefaultOutputDirectory;
        private string _reportPath = DefaultReportPath;
        private float _authoredQualityWeight = DefaultAuthoredQualityWeight;
        private bool _dryRun = true;
        private bool _writeReportToDisk = DefaultWriteReportToDisk;
        private string _lastStatus = "Idle.";

        private static readonly List<SourceGroup> s_groups = new List<SourceGroup>(MaxExpectedSourceGroups);
        private static readonly List<string> s_assetPaths = new List<string>(MaxExpectedAssetPaths);
        private static readonly List<string> s_violations = new List<string>(128);
        private static readonly List<Material> s_materialScratch = new List<Material>(MaxMaterialSlots);
        private static readonly List<CombineBucket> s_combineBuckets = new List<CombineBucket>(MaxMaterialSlots);
        private static readonly List<Mesh> s_tempMeshes = new List<Mesh>(MaxMaterialSlots);
        private static readonly List<Vector3> s_lowVertices = new List<Vector3>(8192);
        private static readonly List<Vector3> s_allHullVertices = new List<Vector3>(32768);
        private static readonly string[] s_exteriorMaterialNames =
        {
            "MAT_Wreckage_Exterior",
            "MAT_Wreckage_Hull_Exterior"
        };

        private static readonly string[] s_burnedMaterialNames =
        {
            "MAT_Wreckage_Burned_Interior",
            "MAT_Wreckage_Burned",
            "MAT_Wreckage_Carbonized_Interior",
            "MAT_Wreckage_Carbonized"
        };

        private static readonly string[] s_debrisMaterialNames =
        {
            "MAT_Wreckage_Debris",
            "MAT_Wreckage_Burned_Debris",
            "MAT_Wreckage_Scrap",
            "MAT_Wreckage_Burned_Scrap"
        };

        private static readonly string[] s_agent1727SuffixTokens =
        {
            "1727",
            "Agent1727",
            "Wave3"
        };

        private static readonly string[] s_debrisNameTokens =
        {
            "Debris",
            "Scatter",
            "Scrap",
            "Shard",
            "Chunklet",
            "Small"
        };

        private static readonly string[] s_collisionNameTokens =
        {
            "COL_",
            "_COL",
            "Collider",
            "CollisionProxy",
            "PhysicsProxy"
        };

        [Serializable]
        public sealed class FactoryReport
        {
            public string agentId = AgentId;
            public string sourceDirectory;
            public string outputDirectory;
            public string generatedMeshDirectory;
            public string reportPath;
            public bool dryRun;
            public bool writeReportToDisk;
            public int sourceGroups;
            public int prefabsAssembled;
            public int prefabsFailed;
            public long totalEditorMicroseconds;
            public List<PrefabMetric> prefabs = new List<PrefabMetric>(MaxExpectedSourceGroups);
            public List<string> violations = new List<string>(128);
        }

        [Serializable]
        public sealed class PrefabMetric
        {
            public string wreckName;
            public string sourcePath;
            public string outputPath;
            public string combinedHullMeshPath;
            public string combinedDebrisMeshPath;
            public int hullMeshCount;
            public int debrisSegmentCount;
            public int combinedHullSubMeshCount;
            public int combinedDebrisSubMeshCount;
            public int colliderCount;
            public int materialSlotCount;
            public int srpBatcherProofCount;
            public Vector3 carveCenter;
            public Vector3 carveHalfExtents;
            public Vector3 carveEuler;
            public float burialDepthMeters;
            public uint prefabHash;
            public long editorMicroseconds;
            public string status;
            public string failure;
        }

        private sealed class SourceGroup
        {
            public string Name;
            public string SourcePath;
            public readonly List<VisualSegment> HullSegments = new List<VisualSegment>(MaxExpectedHullSegmentsPerGroup);
            public readonly List<VisualSegment> DebrisSegments = new List<VisualSegment>(MaxExpectedDebrisSegmentsPerGroup);
            public readonly List<string> CollisionProxyPaths = new List<string>(4);
        }

        private sealed class VisualSegment
        {
            public Mesh Mesh;
            public Matrix4x4 LocalMatrix;
            public Material[] Materials;
            public string SourcePath;
            public string SourceName;
            public bool IsDebris;
        }

        private sealed class MaterialSet
        {
            public Material Exterior;
            public Material Burned;
            public Material Debris;
        }

        private sealed class CombineBucket
        {
            public Material Material;
            public readonly List<CombineInstance> Instances = new List<CombineInstance>(MaxExpectedCombineInstancesPerMaterial);
        }

        private struct CarveObb
        {
            public Vector3 Center;
            public Vector3 HalfExtents;
            public Quaternion Rotation;
        }

        private enum VisualCombineRole
        {
            Hull = 0,
            Debris = 1
        }

        [MenuItem(MenuRoot + "Open Wreckage Prefab Factory", false, 1735)]
        private static void Open()
        {
            WreckagePrefabFactory window = GetWindow<WreckagePrefabFactory>();
            window.titleContent = new GUIContent("Wreck Factory 1735");
            window.minSize = new Vector2(520f, 420f);
        }

        [MenuItem(MenuRoot + "Dry Run Static Audit", false, 1736)]
        private static void DryRunMenu()
        {
            FactorySettings settings = FactorySettings.Default();
            settings.DryRun = true;
            FactoryReport report = Run(settings);
            Debug.Log("[WreckagePrefabFactory1735] dryRun groups=" + report.sourceGroups +
                      " violations=" + report.violations.Count);
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Wreckage Chunk & Debris Prefab Assembler", EditorStyles.boldLabel);
            EditorGUILayout.Space(4f);
            _sourceDirectory = EditorGUILayout.TextField("Source Mesh/Prefab Folder", _sourceDirectory);
            _materialDirectory = EditorGUILayout.TextField("Agent 1727 Material Folder", _materialDirectory);
            _generatedMeshDirectory = EditorGUILayout.TextField("Combined Mesh Output", _generatedMeshDirectory);
            _outputDirectory = EditorGUILayout.TextField("Prefab Output", _outputDirectory);
            _reportPath = EditorGUILayout.TextField("Report Path", _reportPath);
            _authoredQualityWeight = EditorGUILayout.Slider("GlobalQualityWeight", _authoredQualityWeight, 0f, 1f);
            _dryRun = EditorGUILayout.ToggleLeft("Dry Run", _dryRun);
            _writeReportToDisk = EditorGUILayout.ToggleLeft("Write JSON Report To Disk", _writeReportToDisk);
            EditorGUILayout.Space(8f);

            if (GUILayout.Button("Run Factory", GUILayout.Height(32f)))
            {
                FactorySettings settings = BuildSettingsFromWindow();
                FactoryReport report = Run(settings);
                _lastStatus = "groups=" + report.sourceGroups +
                              " assembled=" + report.prefabsAssembled +
                              " failed=" + report.prefabsFailed +
                              " violations=" + report.violations.Count;
            }

            EditorGUILayout.HelpBox(_lastStatus, MessageType.Info);
        }

        public static FactoryReport Run(FactorySettings settings)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            FactoryReport report = new FactoryReport
            {
                sourceDirectory = settings.SourceDirectory,
                outputDirectory = settings.OutputDirectory,
                generatedMeshDirectory = settings.GeneratedMeshDirectory,
                reportPath = settings.ReportPath,
                dryRun = settings.DryRun,
                writeReportToDisk = settings.WriteReportToDisk
            };

            s_violations.Clear();
            try
            {
                if (!TryResolveMaterialSet(settings.MaterialDirectory, out MaterialSet materialSet, report))
                    return FinalizeReport(report, stopwatch);

                DiscoverSourceGroups(settings.SourceDirectory, report);
                report.sourceGroups = s_groups.Count;
                for (int i = 0; i < s_groups.Count; i++)
                {
                    SourceGroup group = s_groups[i];
                    PrefabMetric metric = BuildWreckagePrefab(group, materialSet, settings, report);
                    report.prefabs.Add(metric);
                    if (string.Equals(metric.status, "PASS", StringComparison.Ordinal))
                        report.prefabsAssembled++;
                    else
                        report.prefabsFailed++;
                }

                RunStaticAudit(settings, report);
                return FinalizeReport(report, stopwatch);
            }
            finally
            {
                s_groups.Clear();
                s_assetPaths.Clear();
                s_violations.Clear();
                ClearCombineScratch();
                s_allHullVertices.Clear();
                s_lowVertices.Clear();
            }
        }

        private FactorySettings BuildSettingsFromWindow()
        {
            return new FactorySettings
            {
                SourceDirectory = _sourceDirectory,
                MaterialDirectory = _materialDirectory,
                GeneratedMeshDirectory = _generatedMeshDirectory,
                OutputDirectory = _outputDirectory,
                ReportPath = _reportPath,
                AuthoredQualityWeight = Mathf.Clamp01(_authoredQualityWeight),
                DryRun = _dryRun,
                WriteReportToDisk = _writeReportToDisk
            };
        }

        private static PrefabMetric BuildWreckagePrefab(
            SourceGroup group,
            MaterialSet materialSet,
            FactorySettings settings,
            FactoryReport report)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            PrefabMetric metric = new PrefabMetric
            {
                wreckName = group.Name,
                sourcePath = group.SourcePath,
                outputPath = BuildOutputPath(settings.OutputDirectory, group.Name),
                combinedHullMeshPath = BuildCombinedHullMeshPath(settings.GeneratedMeshDirectory, group.Name),
                combinedDebrisMeshPath = BuildCombinedDebrisMeshPath(settings.GeneratedMeshDirectory, group.Name),
                hullMeshCount = group.HullSegments.Count,
                debrisSegmentCount = group.DebrisSegments.Count,
                status = "FAIL"
            };

            GameObject root = null;
            Mesh combinedHullMesh = null;
            Mesh combinedDebrisMesh = null;
            bool combinedHullMeshPersisted = false;
            bool combinedDebrisMeshPersisted = false;
            try
            {
                if (group.HullSegments.Count == 0)
                    return Fail(metric, report, group.Name + ": no hull mesh segments found.");
                if (group.DebrisSegments.Count == 0)
                    return Fail(metric, report, group.Name + ": no debris segments found for offline combine.");
                if (group.CollisionProxyPaths.Count == 0)
                    return Fail(metric, report, group.Name + ": missing COL_ collision proxy.");

                int worldStaticLayer = LayerMask.NameToLayer("World_Static");
                if (worldStaticLayer < 0)
                    return Fail(metric, report, group.Name + ": required World_Static layer missing.");

                root = new GameObject("PFB_Wreckage_" + group.Name);
                root.layer = worldStaticLayer;
                ResetLocalTransform(root.transform);

                if (!TryBuildCombinedVisualMesh(
                        group,
                        group.HullSegments,
                        materialSet,
                        settings,
                        metric,
                        VisualCombineRole.Hull,
                        metric.combinedHullMeshPath,
                        out combinedHullMesh,
                        out combinedHullMeshPersisted,
                        report))
                {
                    return metric;
                }

                AddHullVisual(root.transform, combinedHullMesh, materialSet, worldStaticLayer, metric);
                Bounds hullBounds = combinedHullMesh.bounds;
                if (!TryBuildCombinedVisualMesh(
                        group,
                        group.DebrisSegments,
                        materialSet,
                        settings,
                        metric,
                        VisualCombineRole.Debris,
                        metric.combinedDebrisMeshPath,
                        out combinedDebrisMesh,
                        out combinedDebrisMeshPersisted,
                        report))
                {
                    return metric;
                }

                MeshRenderer debrisRenderer = AddDebrisVisual(root.transform, combinedDebrisMesh, materialSet, worldStaticLayer, metric);
                if (!AttachCollisionProxy(root.transform, group, worldStaticLayer, metric, report))
                    return metric;

                CarveObb carveObb = ComputeCarveObb(group, hullBounds);
                metric.carveCenter = carveObb.Center;
                metric.carveHalfExtents = carveObb.HalfExtents;
                metric.carveEuler = carveObb.Rotation.eulerAngles;
                metric.burialDepthMeters = DefaultBurialDepthMeters;
                metric.prefabHash = HashAscii(group.Name);
                AttachVoxelCarveVolume(root, carveObb, metric.prefabHash);
                try
                {
                    AttachSalvageMetadata(root, group.Name, hullBounds, metric.prefabHash, settings.AuthoredQualityWeight);
                }
                catch (Exception ex)
                {
                    return Fail(metric, report, group.Name + ": salvage metadata attach failed: " + ex.GetType().Name + ": " + ex.Message);
                }

                MeshRenderer[] renderers = root.GetComponentsInChildren<MeshRenderer>(true); // COLD ALLOC: editor serialization gate only.
                ConfigureMergedLodGroup(root, renderers);
                WreckageScatterManager scatterManager = root.AddComponent<WreckageScatterManager>();
                scatterManager.SetEditorBakeData(
                    new[] { debrisRenderer }, // COLD ALLOC: serialized debris-only presentation target.
                    settings.AuthoredQualityWeight,
                    0.08f,
                    0.86f,
                    metric.prefabHash);

                if (!ValidatePrefabContract(root, metric, report))
                    return metric;

                if (!settings.DryRun)
                {
                    EnsureAssetFolder(settings.OutputDirectory);
                    GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(root, metric.outputPath, out bool success);
                    if (!success || savedPrefab == null)
                        return Fail(metric, report, group.Name + ": PrefabUtility.SaveAsPrefabAsset failed.");
                }

                metric.status = "PASS";
                return metric;
            }
            finally
            {
                metric.editorMicroseconds = TicksToMicroseconds(stopwatch.ElapsedTicks);
                if (root != null)
                    Object.DestroyImmediate(root);
                if (combinedHullMesh != null && !combinedHullMeshPersisted)
                    Object.DestroyImmediate(combinedHullMesh);
                if (combinedDebrisMesh != null && !combinedDebrisMeshPersisted)
                    Object.DestroyImmediate(combinedDebrisMesh);
            }
        }

        public struct FactorySettings
        {
            public string SourceDirectory;
            public string MaterialDirectory;
            public string GeneratedMeshDirectory;
            public string OutputDirectory;
            public string ReportPath;
            public float AuthoredQualityWeight;
            public bool DryRun;
            public bool WriteReportToDisk;

            public static FactorySettings Default()
            {
                return new FactorySettings
                {
                    SourceDirectory = DefaultSourceDirectory,
                    MaterialDirectory = DefaultMaterialDirectory,
                    GeneratedMeshDirectory = DefaultGeneratedMeshDirectory,
                    OutputDirectory = DefaultOutputDirectory,
                    ReportPath = DefaultReportPath,
                    AuthoredQualityWeight = DefaultAuthoredQualityWeight,
                    DryRun = true,
                    WriteReportToDisk = DefaultWriteReportToDisk
                };
            }
        }

        private static MeshRenderer AddHullVisual(
            Transform root,
            Mesh combinedHullMesh,
            MaterialSet materialSet,
            int layer,
            PrefabMetric metric)
        {
            GameObject child = new GameObject("VIS_HullCombined");
            child.layer = layer;
            child.transform.SetParent(root, false);
            ResetLocalTransform(child.transform);

            MeshFilter filter = child.AddComponent<MeshFilter>();
            filter.sharedMesh = combinedHullMesh;
            MeshRenderer renderer = child.AddComponent<MeshRenderer>();
            renderer.receiveGI = ReceiveGI.Lightmaps;
            renderer.shadowCastingMode = ShadowCastingMode.On;
            renderer.receiveShadows = true;
            renderer.sharedMaterials = BuildMaterialSlotsFromBuckets(materialSet, metric.combinedHullSubMeshCount, VisualCombineRole.Hull);
            return renderer;
        }

        private static MeshRenderer AddDebrisVisual(
            Transform root,
            Mesh combinedDebrisMesh,
            MaterialSet materialSet,
            int layer,
            PrefabMetric metric)
        {
            GameObject child = new GameObject("VIS_DebrisScatter");
            child.layer = layer;
            child.transform.SetParent(root, false);
            ResetLocalTransform(child.transform);

            MeshFilter filter = child.AddComponent<MeshFilter>();
            filter.sharedMesh = combinedDebrisMesh;
            MeshRenderer renderer = child.AddComponent<MeshRenderer>();
            renderer.receiveGI = ReceiveGI.Lightmaps;
            renderer.shadowCastingMode = ShadowCastingMode.On;
            renderer.receiveShadows = true;
            renderer.sharedMaterials = BuildMaterialSlotsFromBuckets(materialSet, metric.combinedDebrisSubMeshCount, VisualCombineRole.Debris);
            return renderer;
        }

        private static bool TryBuildCombinedVisualMesh(
            SourceGroup group,
            List<VisualSegment> segments,
            MaterialSet materialSet,
            FactorySettings settings,
            PrefabMetric metric,
            VisualCombineRole role,
            string meshPath,
            out Mesh combinedMesh,
            out bool persisted,
            FactoryReport report)
        {
            combinedMesh = null;
            persisted = false;
            ClearCombineScratch();

            for (int i = 0; i < segments.Count; i++)
            {
                VisualSegment segment = segments[i];
                Mesh mesh = segment.Mesh;
                if (mesh == null)
                    continue;

                int subMeshCount = math.max(1, mesh.subMeshCount);
                for (int subMesh = 0; subMesh < subMeshCount; subMesh++)
                {
                    Material material = ResolveSlotMaterial(segment, subMesh, materialSet, role);
                    CombineBucket bucket = ResolveCombineBucket(material);
                    bucket.Instances.Add(new CombineInstance
                    {
                        mesh = mesh,
                        subMeshIndex = subMesh,
                        transform = segment.LocalMatrix
                    });
                }
            }

            if (s_combineBuckets.Count == 0)
                return FailBool(metric, report, group.Name + ": " + role + " combine bucket is empty.");
            if (s_combineBuckets.Count > MaxMaterialSlots)
                return FailBool(metric, report, group.Name + ": " + role + " material bucket count exceeds " + MaxMaterialSlots.ToString(CultureInfo.InvariantCulture) + ".");

            CombineInstance[] finalCombine = new CombineInstance[s_combineBuckets.Count]; // COLD ALLOC: editor-only Mesh.CombineMeshes submesh bridge.
            for (int i = 0; i < s_combineBuckets.Count; i++)
            {
                CombineBucket bucket = s_combineBuckets[i];
                Mesh bucketMesh = new Mesh
                {
                    name = "TMP_" + group.Name + "_" + role + "Bucket_" + i.ToString(CultureInfo.InvariantCulture),
                    indexFormat = IndexFormat.UInt32
                };
                CombineInstance[] bucketInstances = bucket.Instances.ToArray(); // COLD ALLOC: editor-only Mesh.CombineMeshes input.
                bucketMesh.CombineMeshes(bucketInstances, true, true, false);
                bucketMesh.RecalculateBounds();
                s_tempMeshes.Add(bucketMesh);
                finalCombine[i] = new CombineInstance
                {
                    mesh = bucketMesh,
                    subMeshIndex = 0,
                    transform = Matrix4x4.identity
                };
            }

            combinedMesh = new Mesh
            {
                name = "MESH_Wreckage_" + group.Name + "_" + role + "Combined",
                indexFormat = IndexFormat.UInt32
            };
            combinedMesh.CombineMeshes(finalCombine, false, false, false);
            combinedMesh.RecalculateBounds();
            if (role == VisualCombineRole.Debris)
                metric.combinedDebrisSubMeshCount = s_combineBuckets.Count;
            else
                metric.combinedHullSubMeshCount = s_combineBuckets.Count;

            metric.materialSlotCount = math.max(metric.materialSlotCount, s_combineBuckets.Count);

            if (!settings.DryRun)
            {
                EnsureAssetFolder(settings.GeneratedMeshDirectory);
                if (AssetDatabase.LoadAssetAtPath<Mesh>(meshPath) != null)
                    AssetDatabase.DeleteAsset(meshPath);
                AssetDatabase.CreateAsset(combinedMesh, meshPath);
                persisted = true;
            }

            return true;
        }

        private static CombineBucket ResolveCombineBucket(Material material)
        {
            for (int i = 0; i < s_combineBuckets.Count; i++)
            {
                CombineBucket bucket = s_combineBuckets[i];
                if (ReferenceEquals(bucket.Material, material))
                    return bucket;
            }

            CombineBucket created = new CombineBucket { Material = material };
            s_combineBuckets.Add(created);
            return created;
        }

        private static bool AttachCollisionProxy(
            Transform root,
            SourceGroup group,
            int layer,
            PrefabMetric metric,
            FactoryReport report)
        {
            string proxyPath = group.CollisionProxyPaths[0];
            GameObject proxyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(proxyPath);
            if (proxyPrefab != null)
            {
                GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(proxyPrefab);
                if (instance == null)
                    return FailBool(metric, report, group.Name + ": failed to instantiate collision proxy " + proxyPath);

                instance.name = "COL_" + group.Name;
                instance.transform.SetParent(root, false);
                ResetLocalTransform(instance.transform);
                SetLayerRecursive(instance.transform, layer);
                return ValidateCollisionProxy(instance, layer, metric, report);
            }

            Mesh proxyMesh = AssetDatabase.LoadAssetAtPath<Mesh>(proxyPath);
            if (proxyMesh == null)
                return FailBool(metric, report, group.Name + ": collision proxy is not Mesh or Prefab: " + proxyPath);

            GameObject child = new GameObject("COL_" + group.Name);
            child.layer = layer;
            child.transform.SetParent(root, false);
            ResetLocalTransform(child.transform);
            MeshCollider collider = child.AddComponent<MeshCollider>();
            collider.sharedMesh = proxyMesh;
            collider.convex = true;
            return ValidateCollisionProxy(child, layer, metric, report);
        }

        private static bool ValidateCollisionProxy(
            GameObject proxyRoot,
            int expectedLayer,
            PrefabMetric metric,
            FactoryReport report)
        {
            Collider[] colliders = proxyRoot.GetComponentsInChildren<Collider>(true); // COLD ALLOC: editor prefab gate only.
            if (colliders == null || colliders.Length == 0)
                return FailBool(metric, report, proxyRoot.name + ": no Collider components in COL_ proxy.");

            Renderer[] proxyRenderers = proxyRoot.GetComponentsInChildren<Renderer>(true); // COLD ALLOC: editor proxy purity gate.
            if (proxyRenderers != null && proxyRenderers.Length > 0)
                return FailBool(metric, report, proxyRoot.name + ": COL_ proxy must not contain Renderer components.");

            for (int i = 0; i < colliders.Length; i++)
            {
                Collider collider = colliders[i];
                if (collider.gameObject.layer != expectedLayer)
                    return FailBool(metric, report, collider.name + ": collision proxy layer mismatch.");

                if (collider is BoxCollider || collider is CapsuleCollider)
                {
                    metric.colliderCount++;
                    continue;
                }

                MeshCollider meshCollider = collider as MeshCollider;
                if (meshCollider != null && meshCollider.convex && meshCollider.sharedMesh != null)
                {
                    metric.colliderCount++;
                    continue;
                }

                return FailBool(metric, report, collider.name + ": collision proxy requires BoxCollider, CapsuleCollider, or convex MeshCollider.");
            }

            return true;
        }

        private static void ConfigureMergedLodGroup(GameObject root, MeshRenderer[] renderers)
        {
            if (root == null || renderers == null || renderers.Length == 0)
                return;

            LODGroup lodGroup = root.GetComponent<LODGroup>();
            if (lodGroup == null)
                lodGroup = root.AddComponent<LODGroup>();

            Renderer[] rendererRefs = new Renderer[renderers.Length]; // COLD ALLOC: serialized one-LOD merge exemption.
            for (int i = 0; i < renderers.Length; i++)
                rendererRefs[i] = renderers[i];

            lodGroup.SetLODs(new[] { new LOD(1f, rendererRefs) }); // COLD ALLOC: serialized LOD policy.
            lodGroup.RecalculateBounds();
        }

        private static void AttachVoxelCarveVolume(GameObject root, CarveObb carveObb, uint stableHash)
        {
            if (!VoxelCarveVolume.ValidateDescriptorLayout())
                throw new InvalidOperationException("VoxelCarveVolume descriptor layout invalid.");

            VoxelCarveVolume volume = root.AddComponent<VoxelCarveVolume>();
            volume.SetEditorBakeData(
                carveObb.Center,
                carveObb.HalfExtents,
                carveObb.Rotation,
                DefaultBurialDepthMeters,
                WreckageVoxelCarveInstruction.FlattenAndBury,
                VoxelSubtractOperation,
                VoxelBoxShape,
                stableHash);
        }

        private static void AttachSalvageMetadata(
            GameObject root,
            string safeName,
            Bounds bounds,
            uint stableHash,
            float quality)
        {
            if (!EquipmentMetadata.ValidateStaticLayout())
                throw new InvalidOperationException("EquipmentMetadata unmanaged layout invalid.");

            InteractionAnchorData[] anchors = BuildWreckageAnchors(bounds, stableHash); // COLD ALLOC: serialized prefab anchor set.
            if (!EquipmentMetadata.ValidateAnchorSet(anchors, out string failure))
                throw new InvalidOperationException("EquipmentMetadata anchor validation failed for " + safeName + ": " + failure);

            EquipmentMetadata metadata = root.AddComponent<EquipmentMetadata>();
            metadata.SetEditorBakeData(stableHash, HashAscii(safeName + "_bake"), quality, anchors);

            GameObject trigger = new GameObject("TRIG_SalvageNode");
            trigger.layer = ResolveInteractableLayer();
            trigger.transform.SetParent(root.transform, false);
            ResetLocalTransform(trigger.transform);
            BoxCollider box = trigger.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.center = bounds.center;
            Vector3 size = bounds.size;
            box.size = new Vector3(
                Mathf.Max(0.35f, Mathf.Abs(size.x) * 0.22f),
                Mathf.Max(0.35f, Mathf.Abs(size.y) * 0.28f),
                Mathf.Max(0.35f, Mathf.Abs(size.z) * 0.22f));
        }

        private static InteractionAnchorData[] BuildWreckageAnchors(Bounds bounds, uint seed)
        {
            InteractionAnchorData[] anchors = new InteractionAnchorData[3]; // COLD ALLOC: serialized interaction anchors.
            Vector3 center = bounds.center;
            Vector3 extents = bounds.extents;
            float snap = Mathf.Clamp(Mathf.Min(Mathf.Abs(extents.x), Mathf.Abs(extents.z)) * 0.28f, 0.08f, 0.75f);
            anchors[0] = CreateAnchor("ANCHOR_WreckCoreAccess_1735", center, Vector3.forward, snap, seed);
            anchors[1] = CreateAnchor("ANCHOR_WreckForeSalvage_1735", center + new Vector3(0f, 0f, -extents.z * 0.72f), Vector3.back, snap * 0.85f, seed ^ 0xF0111735u);
            anchors[2] = CreateAnchor("ANCHOR_WreckAftSalvage_1735", center + new Vector3(0f, 0f, extents.z * 0.72f), Vector3.forward, snap * 0.85f, seed ^ 0xAF771735u);
            return anchors;
        }

        private static InteractionAnchorData CreateAnchor(
            string name,
            Vector3 position,
            Vector3 forward,
            float snap,
            uint seed)
        {
            return new InteractionAnchorData
            {
                LocalPosition = new float3(position.x, position.y, position.z),
                LocalForward = math.normalizesafe(new float3(forward.x, forward.y, forward.z), new float3(0f, 0f, 1f)),
                LocalUp = new float3(0f, 1f, 0f),
                SnapRadiusMeters = Mathf.Clamp(snap, 0.05f, 1.25f),
                AnchorId = HashAscii(name) ^ seed,
                Flags = InteractionAnchorData.FlagActive | InteractionAnchorData.FlagTwoHanded,
                HandMask = InteractionAnchorData.HandMaskBoth,
                SurfaceKind = InteractionAnchorData.SurfaceKindValve
            };
        }

        private static bool ValidatePrefabContract(
            GameObject root,
            PrefabMetric metric,
            FactoryReport report)
        {
            if (root.GetComponent<VoxelCarveVolume>() == null)
                return FailBool(metric, report, metric.wreckName + ": missing VoxelCarveVolume.");
            if (root.GetComponent<WreckageScatterManager>() == null)
                return FailBool(metric, report, metric.wreckName + ": missing WreckageScatterManager.");
            if (root.GetComponent<EquipmentMetadata>() == null)
                return FailBool(metric, report, metric.wreckName + ": missing EquipmentMetadata.");

            MeshCollider rootMeshCollider = root.GetComponent<MeshCollider>();
            if (rootMeshCollider != null)
                return FailBool(metric, report, metric.wreckName + ": root MeshCollider forbidden.");

            LODGroup lodGroup = root.GetComponent<LODGroup>();
            if (lodGroup == null || lodGroup.lodCount != 1)
                return FailBool(metric, report, metric.wreckName + ": missing one-step merged LODGroup policy.");

            Transform hull = root.transform.Find("VIS_HullCombined");
            if (hull == null ||
                hull.GetComponent<MeshFilter>() == null ||
                hull.GetComponent<MeshRenderer>() == null ||
                !HasZeroLocalTransform(hull))
            {
                return FailBool(metric, report, metric.wreckName + ": missing zero-local VIS_HullCombined renderer.");
            }

            Transform debris = root.transform.Find("VIS_DebrisScatter");
            if (debris == null ||
                debris.GetComponent<MeshFilter>() == null ||
                debris.GetComponent<MeshRenderer>() == null ||
                !HasZeroLocalTransform(debris))
            {
                return FailBool(metric, report, metric.wreckName + ": missing zero-local VIS_DebrisScatter renderer.");
            }

            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true); // COLD ALLOC: editor prefab validation only.
            if (renderers == null || renderers.Length == 0)
                return FailBool(metric, report, metric.wreckName + ": no renderers.");
            if (renderers.Length > MaxVisualRendererCount)
                return FailBool(metric, report, metric.wreckName + ": visual renderer count exceeds merged wreck budget.");

            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (!IsLightmapReceiveGiCompatible(renderer))
                    return FailBool(metric, report, renderer.name + ": receiveGI must be Lightmaps.");

                renderer.GetSharedMaterials(s_materialScratch); // COLD LIST REUSE: editor prefab validation only.
                if (s_materialScratch.Count == 0 || s_materialScratch.Count > MaxMaterialSlots)
                {
                    s_materialScratch.Clear();
                    return FailBool(metric, report, renderer.name + ": material slots invalid.");
                }

                metric.materialSlotCount = math.max(metric.materialSlotCount, s_materialScratch.Count);
                for (int slot = 0; slot < s_materialScratch.Count; slot++)
                {
                    if (!IsSrpBatcherCandidate(s_materialScratch[slot], out string proof))
                    {
                        s_materialScratch.Clear();
                        return FailBool(metric, report, renderer.name + ": material slot " + slot + " failed SRP Batcher proof: " + proof);
                    }

                    metric.srpBatcherProofCount++;
                }

                s_materialScratch.Clear();
            }

            if (metric.colliderCount <= 0)
                return FailBool(metric, report, metric.wreckName + ": no solid collision proxy.");

            return true;
        }

        private static bool HasZeroLocalTransform(Transform transform)
        {
            if (transform == null)
                return false;

            return transform.localPosition == Vector3.zero &&
                   transform.localRotation == Quaternion.identity &&
                   transform.localScale == Vector3.one;
        }

        private static bool IsLightmapReceiveGiCompatible(Renderer renderer)
        {
            if (renderer == null)
                return false;

            SerializedObject serializedRenderer = new SerializedObject(renderer);
            SerializedProperty receiveGi = serializedRenderer.FindProperty("m_ReceiveGI");
            return receiveGi != null && receiveGi.intValue == (int)ReceiveGI.Lightmaps;
        }

        private static CarveObb ComputeCarveObb(SourceGroup group, Bounds fallbackBounds)
        {
            s_allHullVertices.Clear();
            s_lowVertices.Clear();

            float minY = float.PositiveInfinity;
            float maxY = float.NegativeInfinity;
            for (int i = 0; i < group.HullSegments.Count; i++)
            {
                VisualSegment segment = group.HullSegments[i];
                if (segment.Mesh == null)
                    continue;

                Vector3[] vertices = segment.Mesh.vertices; // COLD ALLOC: editor-only SDF seating analysis.
                for (int vertexIndex = 0; vertexIndex < vertices.Length; vertexIndex++)
                {
                    Vector3 v = segment.LocalMatrix.MultiplyPoint3x4(vertices[vertexIndex]);
                    s_allHullVertices.Add(v);
                    minY = Mathf.Min(minY, v.y);
                    maxY = Mathf.Max(maxY, v.y);
                }
            }

            if (s_allHullVertices.Count == 0 || !IsFinite(minY) || !IsFinite(maxY))
                return ObbFromBounds(fallbackBounds);

            float threshold = minY + (maxY - minY) * (LowestVertexPercent / 100f);
            for (int i = 0; i < s_allHullVertices.Count; i++)
            {
                Vector3 v = s_allHullVertices[i];
                if (v.y <= threshold)
                    s_lowVertices.Add(v);
            }

            if (s_lowVertices.Count < 3)
                return ObbFromBounds(fallbackBounds);

            float meanX = 0f;
            float meanZ = 0f;
            for (int i = 0; i < s_lowVertices.Count; i++)
            {
                meanX += s_lowVertices[i].x;
                meanZ += s_lowVertices[i].z;
            }

            float invCount = 1f / s_lowVertices.Count;
            meanX *= invCount;
            meanZ *= invCount;

            float covXX = 0f;
            float covXZ = 0f;
            float covZZ = 0f;
            for (int i = 0; i < s_lowVertices.Count; i++)
            {
                float dx = s_lowVertices[i].x - meanX;
                float dz = s_lowVertices[i].z - meanZ;
                covXX += dx * dx;
                covXZ += dx * dz;
                covZZ += dz * dz;
            }

            float yaw = 0.5f * Mathf.Atan2(2f * covXZ, covXX - covZZ);
            Quaternion rotation = Quaternion.AngleAxis(yaw * Mathf.Rad2Deg, Vector3.up);
            Quaternion inverse = Quaternion.Inverse(rotation);
            Vector3 min = new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
            Vector3 max = new Vector3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);
            for (int i = 0; i < s_lowVertices.Count; i++)
            {
                Vector3 p = inverse * s_lowVertices[i];
                min = Vector3.Min(min, p);
                max = Vector3.Max(max, p);
            }

            Vector3 localCenter = (min + max) * 0.5f;
            Vector3 halfExtents = (max - min) * 0.5f;
            halfExtents = new Vector3(
                Mathf.Max(MinimumCarveExtentMeters, Mathf.Abs(halfExtents.x)),
                Mathf.Max(MinimumCarveExtentMeters, Mathf.Abs(halfExtents.y) + DefaultBurialDepthMeters * 0.5f),
                Mathf.Max(MinimumCarveExtentMeters, Mathf.Abs(halfExtents.z)));
            Vector3 center = rotation * localCenter;
            center.y -= DefaultBurialDepthMeters * 0.5f;
            return new CarveObb
            {
                Center = center,
                HalfExtents = halfExtents,
                Rotation = rotation
            };
        }

        private static CarveObb ObbFromBounds(Bounds bounds)
        {
            Vector3 extents = bounds.extents;
            return new CarveObb
            {
                Center = bounds.center + new Vector3(0f, -DefaultBurialDepthMeters * 0.5f, 0f),
                HalfExtents = new Vector3(
                    Mathf.Max(MinimumCarveExtentMeters, Mathf.Abs(extents.x)),
                    Mathf.Max(MinimumCarveExtentMeters, Mathf.Abs(extents.y) + DefaultBurialDepthMeters * 0.5f),
                    Mathf.Max(MinimumCarveExtentMeters, Mathf.Abs(extents.z))),
                Rotation = Quaternion.identity
            };
        }

        private static void DiscoverSourceGroups(string sourceDirectory, FactoryReport report)
        {
            s_groups.Clear();
            s_assetPaths.Clear();
            string folder = NormalizeAssetFolder(sourceDirectory);
            if (string.IsNullOrEmpty(folder) || !AssetDatabase.IsValidFolder(folder))
            {
                AddViolation(report, "Invalid source folder: " + sourceDirectory);
                return;
            }

            AppendAssetPaths("t:Prefab", folder, s_assetPaths);
            AppendAssetPaths("t:Mesh", folder, s_assetPaths);
            for (int i = 0; i < s_assetPaths.Count; i++)
            {
                string path = s_assetPaths[i];
                if (path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
                    DiscoverPrefabSource(path, report);
                else
                    DiscoverMeshSource(path, report);
            }
        }

        private static void DiscoverPrefabSource(string path, FactoryReport report)
        {
            string groupName = NormalizeWreckName(Path.GetFileNameWithoutExtension(path));
            SourceGroup group = ResolveGroup(groupName, path);
            GameObject root = null;
            try
            {
                root = PrefabUtility.LoadPrefabContents(path);
                if (root == null)
                {
                    AddViolation(report, "Prefab source failed to load: " + path);
                    return;
                }

                MeshFilter[] filters = root.GetComponentsInChildren<MeshFilter>(true); // COLD ALLOC: editor source discovery.
                for (int i = 0; filters != null && i < filters.Length; i++)
                {
                    MeshFilter filter = filters[i];
                    Mesh mesh = filter.sharedMesh;
                    if (mesh == null)
                        continue;

                    string objectName = filter.gameObject.name;
                    if (IsCollisionName(objectName) || IsCollisionName(mesh.name))
                    {
                        string meshPath = AssetDatabase.GetAssetPath(mesh);
                        if (!string.IsNullOrEmpty(meshPath))
                            AddUniquePath(group.CollisionProxyPaths, meshPath);
                        continue;
                    }

                    MeshRenderer renderer = filter.GetComponent<MeshRenderer>();
                    Material[] materials = renderer != null ? renderer.sharedMaterials : Array.Empty<Material>(); // COLD ALLOC: editor source material snapshot.
                    Matrix4x4 localMatrix = root.transform.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                    VisualSegment segment = new VisualSegment
                    {
                        Mesh = mesh,
                        LocalMatrix = localMatrix,
                        Materials = materials,
                        SourcePath = path,
                        SourceName = objectName,
                        IsDebris = IsDebrisName(objectName) || IsDebrisName(mesh.name)
                    };

                    if (segment.IsDebris)
                        group.DebrisSegments.Add(segment);
                    else
                        group.HullSegments.Add(segment);
                }

                if (IsCollisionName(root.name))
                    AddUniquePath(group.CollisionProxyPaths, path);
            }
            finally
            {
                if (root != null)
                    PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void DiscoverMeshSource(string path, FactoryReport report)
        {
            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh == null)
            {
                AddViolation(report, "Mesh source failed to load: " + path);
                return;
            }

            string fileName = Path.GetFileNameWithoutExtension(path);
            string groupName = NormalizeWreckName(fileName);
            SourceGroup group = ResolveGroup(groupName, path);
            if (IsCollisionName(fileName))
            {
                AddUniquePath(group.CollisionProxyPaths, path);
                return;
            }

            VisualSegment segment = new VisualSegment
            {
                Mesh = mesh,
                LocalMatrix = Matrix4x4.identity,
                Materials = Array.Empty<Material>(),
                SourcePath = path,
                SourceName = fileName,
                IsDebris = IsDebrisName(fileName)
            };

            if (segment.IsDebris)
                group.DebrisSegments.Add(segment);
            else
                group.HullSegments.Add(segment);
        }

        private static SourceGroup ResolveGroup(string groupName, string sourcePath)
        {
            for (int i = 0; i < s_groups.Count; i++)
            {
                SourceGroup group = s_groups[i];
                if (string.Equals(group.Name, groupName, StringComparison.OrdinalIgnoreCase))
                    return group;
            }

            SourceGroup created = new SourceGroup
            {
                Name = SanitizeAssetName(groupName),
                SourcePath = sourcePath
            };
            s_groups.Add(created);
            return created;
        }

        private static bool TryResolveMaterialSet(
            string materialDirectory,
            out MaterialSet materialSet,
            FactoryReport report)
        {
            materialSet = new MaterialSet
            {
                Exterior = FindBestMaterial(materialDirectory, s_exteriorMaterialNames),
                Burned = FindBestMaterial(materialDirectory, s_burnedMaterialNames),
                Debris = FindBestMaterial(materialDirectory, s_debrisMaterialNames)
            };

            if (materialSet.Debris == null)
                materialSet.Debris = materialSet.Burned;

            if (materialSet.Exterior == null || materialSet.Burned == null || materialSet.Debris == null)
            {
                AddViolation(report, "Agent 1727 burned PBR material set missing. Factory refuses to create fallback materials.");
                return false;
            }

            return true;
        }

        private static Material FindBestMaterial(string materialDirectory, string[] exactNames)
        {
            s_assetPaths.Clear();
            string normalized = NormalizeAssetFolder(materialDirectory);
            if (!string.IsNullOrEmpty(normalized) && AssetDatabase.IsValidFolder(normalized))
                AppendAssetPaths("t:Material", normalized, s_assetPaths);
            AppendAssetPaths("t:Material", "Assets/_Project/Art/Materials", s_assetPaths);
            AppendAssetPaths("t:Material", "Assets/_Project/Materials", s_assetPaths);

            for (int nameIndex = 0; nameIndex < exactNames.Length; nameIndex++)
            {
                Material firstValidCandidate = null;
                string exactName = exactNames[nameIndex];
                for (int i = 0; i < s_assetPaths.Count; i++)
                {
                    Material material = AssetDatabase.LoadAssetAtPath<Material>(s_assetPaths[i]);
                    if (material == null || !IsSrpBatcherCandidate(material, out _))
                        continue;

                    string fileName = Path.GetFileNameWithoutExtension(s_assetPaths[i]);
                    if (!MaterialNameMatchesCandidate(material.name, exactName) &&
                        !MaterialNameMatchesCandidate(fileName, exactName))
                    {
                        continue;
                    }

                    if (material.shader != null &&
                        string.Equals(material.shader.name, WreckIndirectShaderName, StringComparison.Ordinal))
                    {
                        return material;
                    }

                    firstValidCandidate = material;
                }

                if (firstValidCandidate != null)
                    return firstValidCandidate;
            }

            return null;
        }

        private static bool MaterialNameMatchesCandidate(string materialName, string exactName)
        {
            if (string.IsNullOrEmpty(materialName) || string.IsNullOrEmpty(exactName))
                return false;
            if (string.Equals(materialName, exactName, StringComparison.OrdinalIgnoreCase))
                return true;
            if (!materialName.StartsWith(exactName + "_", StringComparison.OrdinalIgnoreCase))
                return false;
            return ContainsAnyToken(materialName, s_agent1727SuffixTokens);
        }

        private static Material[] BuildMaterialSlotsFromBuckets(MaterialSet materialSet, int subMeshCount, VisualCombineRole role)
        {
            int slotCount = Mathf.Clamp(math.max(1, subMeshCount), 1, MaxMaterialSlots);
            Material[] materials = new Material[slotCount]; // COLD ALLOC: serialized combined visual material slots.
            for (int i = 0; i < materials.Length; i++)
            {
                Material bucketMaterial = i < s_combineBuckets.Count ? s_combineBuckets[i].Material : null;
                materials[i] = bucketMaterial != null ? bucketMaterial : ResolveFallbackSlotMaterial(i, materialSet, role);
            }
            return materials;
        }

        private static Material ResolveSlotMaterial(VisualSegment segment, int subMesh, MaterialSet materialSet, VisualCombineRole role)
        {
            if (segment.Materials != null &&
                subMesh >= 0 &&
                subMesh < segment.Materials.Length &&
                segment.Materials[subMesh] != null &&
                IsSrpBatcherCandidate(segment.Materials[subMesh], out _))
            {
                return segment.Materials[subMesh];
            }

            return ResolveFallbackSlotMaterial(subMesh, materialSet, role);
        }

        private static Material ResolveFallbackSlotMaterial(int subMesh, MaterialSet materialSet, VisualCombineRole role)
        {
            if (role == VisualCombineRole.Debris)
                return subMesh == 0 ? materialSet.Debris : materialSet.Burned;

            return subMesh == 0 ? materialSet.Exterior : materialSet.Burned;
        }

        private static bool IsSrpBatcherCandidate(Material material, out string proof)
        {
            if (material == null || material.shader == null)
            {
                proof = "missing material or shader";
                return false;
            }

            if (material.IsKeywordEnabled("_ALPHABLEND_ON") ||
                material.IsKeywordEnabled("_SURFACE_TYPE_TRANSPARENT") ||
                material.renderQueue >= 3000)
            {
                proof = "transparent material rejected";
                return false;
            }

            string shaderName = material.shader.name;
            if (shaderName.IndexOf("Universal Render Pipeline/Lit", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                proof = "URP Lit built-in UnityPerMaterial CBUFFER";
                return true;
            }

            if (string.Equals(shaderName, WreckIndirectShaderName, StringComparison.Ordinal))
            {
                proof = "Hecton wreck indirect shader";
                return true;
            }

            string shaderPath = AssetDatabase.GetAssetPath(material.shader);
            if (!string.IsNullOrEmpty(shaderPath) && shaderPath.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
            {
                string fullPath = Path.GetFullPath(shaderPath);
                if (File.Exists(fullPath))
                {
                    string source = File.ReadAllText(fullPath);
                    if (source.IndexOf("CBUFFER_START(UnityPerMaterial)", StringComparison.Ordinal) >= 0)
                    {
                        proof = "shader source declares CBUFFER_START(UnityPerMaterial)";
                        return true;
                    }

                    if (shaderPath.EndsWith(".shadergraph", StringComparison.OrdinalIgnoreCase))
                    {
                        proof = "ShaderGraph SRP Batcher path";
                        return true;
                    }
                }
            }

            proof = "shader lacks UnityPerMaterial CBUFFER proof: " + shaderName;
            return false;
        }

        private static void RunStaticAudit(FactorySettings settings, FactoryReport report)
        {
            string carvePath = "Assets/_Project/Scripts/World/VoxelCarveVolume.cs";
            string scatterPath = "Assets/_Project/Scripts/World/WreckageScatterManager.cs";
            string factoryPath = "Assets/_Project/Editor/Assembly/WreckagePrefabFactory.cs";
            AssertSourceContains(carvePath, "ValidateDescriptorLayout", report);
            AssertSourceContains(carvePath, "TryQueueSpawnCarve", report);
            AssertSourceContains(carvePath, "TryQueueCarveEvent", report);
            AssertSourceDoesNotContain(carvePath, "GlobalRegistry.Get<", report);
            AssertSourceDoesNotContain(carvePath, "GetComponent", report);
            AssertSourceContains(scatterPath, "GlobalQualityWeight", report);
            AssertSourceContains(factoryPath, "Mesh.CombineMeshes", report);
            AssertSourceContains(factoryPath, "PrefabUtility.SaveAsPrefabAsset", report);
            AssertSourceContains(factoryPath, "VoxelCarveVolume", report);
            AssertSourceDoesNotContain(scatterPath, "GetComponentsInChildren", report);
            AssertSourceDoesNotContain(scatterPath, "Update" + "()", report);
            AssertSourceDoesNotContain(scatterPath, "GlobalRegistry.Get<", report);
            AssertSourceContains(scatterPath, "ILateFrameTickable", report);
            AssertSourceContains(scatterPath, "LateFrameTick", report);
            AssertSourceDoesNotContain(factoryPath, "new " + "Material(", report);
            AssertSourceDoesNotContain(factoryPath, "renderer." + "material", report);
        }

        private static void AssertSourceContains(string path, string token, FactoryReport report)
        {
            if (!File.Exists(path))
            {
                AddViolation(report, "Static audit file missing: " + path);
                return;
            }

            string source = File.ReadAllText(path);
            if (source.IndexOf(token, StringComparison.Ordinal) < 0)
                AddViolation(report, "Static audit token missing in " + path + ": " + token);
        }

        private static void AssertSourceDoesNotContain(string path, string token, FactoryReport report)
        {
            if (!File.Exists(path))
                return;

            string source = File.ReadAllText(path);
            if (source.IndexOf(token, StringComparison.Ordinal) >= 0)
                AddViolation(report, "Static audit forbidden token in " + path + ": " + token);
        }

        private static FactoryReport FinalizeReport(FactoryReport report, Stopwatch stopwatch)
        {
            stopwatch.Stop();
            report.totalEditorMicroseconds = TicksToMicroseconds(stopwatch.ElapsedTicks);
            for (int i = 0; i < s_violations.Count; i++)
                report.violations.Add(s_violations[i]);
            if (report.writeReportToDisk)
                WriteReport(report);
            return report;
        }

        private static void WriteReport(FactoryReport report)
        {
            if (report == null)
                return;

            string path = string.IsNullOrWhiteSpace(report.reportPath) ? DefaultReportPath : report.reportPath;
            EnsureDiskFolder(Path.GetDirectoryName(path));
            File.WriteAllText(path, JsonUtility.ToJson(report, true));
        }

        private static PrefabMetric Fail(PrefabMetric metric, FactoryReport report, string failure)
        {
            metric.failure = failure;
            metric.status = "FAIL";
            AddViolation(report, failure);
            return metric;
        }

        private static bool FailBool(PrefabMetric metric, FactoryReport report, string failure)
        {
            metric.failure = failure;
            metric.status = "FAIL";
            AddViolation(report, failure);
            return false;
        }

        private static void AddViolation(FactoryReport report, string violation)
        {
            if (string.IsNullOrEmpty(violation))
                return;

            s_violations.Add(violation);
            Debug.LogError("[WreckagePrefabFactory1735] " + violation);
        }

        private static void AppendAssetPaths(string filter, string folder, List<string> paths)
        {
            if (paths == null || string.IsNullOrEmpty(folder) || !AssetDatabase.IsValidFolder(folder))
                return;

            string[] guids = AssetDatabase.FindAssets(filter, new[] { folder }); // COLD ALLOC: editor AssetDatabase query.
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (!string.IsNullOrEmpty(path))
                    AddUniquePath(paths, path);
            }
        }

        private static void AddUniquePath(List<string> paths, string path)
        {
            for (int i = 0; i < paths.Count; i++)
            {
                if (string.Equals(paths[i], path, StringComparison.OrdinalIgnoreCase))
                    return;
            }

            paths.Add(path);
        }

        private static string BuildOutputPath(string outputDirectory, string name)
        {
            return NormalizeAssetFolder(outputDirectory) + "/PFB_Wreckage_" + SanitizeAssetName(name) + ".prefab";
        }

        private static string BuildCombinedHullMeshPath(string outputDirectory, string name)
        {
            return NormalizeAssetFolder(outputDirectory) + "/MESH_Wreckage_" + SanitizeAssetName(name) + "_HullCombined.asset";
        }

        private static string BuildCombinedDebrisMeshPath(string outputDirectory, string name)
        {
            return NormalizeAssetFolder(outputDirectory) + "/MESH_Wreckage_" + SanitizeAssetName(name) + "_DebrisCombined.asset";
        }

        private static string NormalizeWreckName(string value)
        {
            string clean = value ?? "UnnamedWreck";
            RemovePrefix(ref clean, "PFB_");
            RemovePrefix(ref clean, "MESH_");
            RemovePrefix(ref clean, "GEN_");
            RemovePrefix(ref clean, "COL_");
            RemovePrefix(ref clean, "Wreckage_");
            RemovePrefix(ref clean, "Wreck_");
            RemoveSuffix(ref clean, "_DebrisCombined");
            RemoveSuffix(ref clean, "_Debris");
            RemoveSuffix(ref clean, "_Scatter");
            RemoveSuffix(ref clean, "_Scrap");
            RemoveSuffix(ref clean, "_COLLIDER");
            RemoveSuffix(ref clean, "_COL");
            RemoveSuffix(ref clean, "_Hull");
            RemoveSuffix(ref clean, "_Ruptured");
            RemoveSuffix(ref clean, "_Collapsed");
            RemoveSuffix(ref clean, "_Stressed");
            RemoveSuffix(ref clean, "_LOD0");
            RemoveSuffix(ref clean, "_LOD1");
            RemoveSuffix(ref clean, "_LOD2");
            return SanitizeAssetName(clean);
        }

        private static bool IsDebrisName(string value)
        {
            return ContainsAnyToken(value, s_debrisNameTokens);
        }

        private static bool IsCollisionName(string value)
        {
            return ContainsAnyToken(value, s_collisionNameTokens);
        }

        private static bool ContainsAnyToken(string value, string[] tokens)
        {
            if (string.IsNullOrEmpty(value) || tokens == null)
                return false;

            for (int i = 0; i < tokens.Length; i++)
            {
                string token = tokens[i];
                if (!string.IsNullOrEmpty(token) &&
                    value.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        private static void RemovePrefix(ref string value, string prefix)
        {
            if (value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                value = value.Substring(prefix.Length);
        }

        private static void RemoveSuffix(ref string value, string suffix)
        {
            if (value.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                value = value.Substring(0, value.Length - suffix.Length);
        }

        private static string SanitizeAssetName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "UnnamedWreck";

            char[] chars = value.ToCharArray();
            char[] invalid = Path.GetInvalidFileNameChars();
            for (int i = 0; i < chars.Length; i++)
            {
                bool bad = char.IsWhiteSpace(chars[i]) || chars[i] == '-' || chars[i] == '.';
                for (int j = 0; j < invalid.Length && !bad; j++)
                    bad = chars[i] == invalid[j];
                if (bad)
                    chars[i] = '_';
            }

            return new string(chars);
        }

        private static string NormalizeAssetFolder(string folder)
        {
            if (string.IsNullOrWhiteSpace(folder))
                return string.Empty;

            string normalized = folder.Replace('\\', '/').TrimEnd('/');
            if (!normalized.StartsWith("Assets/", StringComparison.Ordinal) && !string.Equals(normalized, "Assets", StringComparison.Ordinal))
                return string.Empty;
            if (normalized.IndexOf("..", StringComparison.Ordinal) >= 0)
                return string.Empty;
            return normalized;
        }

        private static void EnsureAssetFolder(string folder)
        {
            string normalized = NormalizeAssetFolder(folder);
            if (string.IsNullOrEmpty(normalized) || AssetDatabase.IsValidFolder(normalized))
                return;

            string[] parts = normalized.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }

        private static void EnsureDiskFolder(string folder)
        {
            if (string.IsNullOrWhiteSpace(folder))
                return;

            string full = Path.GetFullPath(folder);
            if (!Directory.Exists(full))
                Directory.CreateDirectory(full);
        }

        private static void ResetLocalTransform(Transform transform)
        {
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
            transform.localScale = Vector3.one;
        }

        private static void SetLayerRecursive(Transform transform, int layer)
        {
            transform.gameObject.layer = layer;
            for (int i = 0; i < transform.childCount; i++)
                SetLayerRecursive(transform.GetChild(i), layer);
        }

        private static int ResolveInteractableLayer()
        {
            int layer = LayerMask.NameToLayer("Interactable");
            return layer >= 0 ? layer : 0;
        }

        private static bool IsFinite(float value)
        {
            return math.isfinite(value);
        }

        private static long TicksToMicroseconds(long ticks)
        {
            return (long)(ticks * 1000000.0d / Stopwatch.Frequency);
        }

        private static uint HashAscii(string value)
        {
            unchecked
            {
                uint hash = 2166136261u;
                if (value != null)
                {
                    for (int i = 0; i < value.Length; i++)
                    {
                        hash ^= value[i];
                        hash *= 16777619u;
                    }
                }

                return hash == 0u ? 1u : hash;
            }
        }

        private static void ClearCombineScratch()
        {
            for (int i = 0; i < s_tempMeshes.Count; i++)
            {
                Mesh mesh = s_tempMeshes[i];
                if (mesh != null)
                    Object.DestroyImmediate(mesh);
            }

            s_tempMeshes.Clear();
            for (int i = 0; i < s_combineBuckets.Count; i++)
                s_combineBuckets[i].Instances.Clear();
            s_combineBuckets.Clear();
        }
    }
}
#endif
