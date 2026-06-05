#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using Hecton8.Construction;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

namespace Hecton8.Editor.Assembly
{
    public sealed class DronePrefabFactory : EditorWindow
    {
        private const string AgentId = "1738";
        private const string DefaultSourcePrefabDirectory = "Assets/_Project/Data/AI/GeneratedProxies/Prefabs";
        private const string DefaultMeshDirectory = "Assets/_Project/Data/AI/GeneratedProxies";
        private const string DefaultMaterialDirectory = "Assets/_Project/Data/AI/GeneratedProxies/Materials";
        private const string DefaultMetadataDirectory = "Assets/_Project/Data/Drones";
        private const string DefaultPhysicsProxyDirectory = "Assets/_Project/Data/AI/GeneratedProxies/Colliders";
        private const string DefaultGeneratedMeshDirectory = "Assets/_Project/BakedGeometry/Drones/PrefabFactory1738";
        private const string DefaultOutputDirectory = "Assets/Prefabs/Vehicles/Drones";
        private const string DynamicWorldLayerName = "World_Dynamic";
        private const string EmissionColorPropertyName = "_EmissionColor";
        private const float MinimumDroneMassKg = 4f;
        private const float MaximumDroneMassKg = 90f;
        private const float MinimumBoundsExtent = 0.08f;
        private const float DefaultAuthoredQualityWeight = 0.68f;
        private const int MaxGroupsPerRunDefault = 256;

        private static readonly List<MeshFilter> s_meshFilterScratch = new List<MeshFilter>(64); // COLD ALLOC: List<MeshFilter>[64] - editor source mesh discovery scratch - owner: DronePrefabFactory
        private static readonly List<MeshRenderer> s_meshRendererScratch = new List<MeshRenderer>(64); // COLD ALLOC: List<MeshRenderer>[64] - editor source material validation scratch - owner: DronePrefabFactory
        private static readonly List<SkinnedMeshRenderer> s_skinnedRendererScratch = new List<SkinnedMeshRenderer>(16); // COLD ALLOC: List<SkinnedMeshRenderer>[16] - editor skinned mesh rejection/harvest scratch - owner: DronePrefabFactory
        private static readonly List<Renderer> s_rendererScratch = new List<Renderer>(64); // COLD ALLOC: List<Renderer>[64] - editor final renderer validation scratch - owner: DronePrefabFactory
        private static readonly List<Collider> s_colliderScratch = new List<Collider>(32); // COLD ALLOC: List<Collider>[32] - editor primitive collider validation scratch - owner: DronePrefabFactory
        private static readonly List<Rigidbody> s_rigidbodyScratch = new List<Rigidbody>(4); // COLD ALLOC: List<Rigidbody>[4] - editor chassis rigidbody validation scratch - owner: DronePrefabFactory
        private static readonly List<MeshCollider> s_meshColliderScratch = new List<MeshCollider>(4); // COLD ALLOC: List<MeshCollider>[4] - editor forbidden collider detection scratch - owner: DronePrefabFactory
        private static readonly List<ParticleSystem> s_particleScratch = new List<ParticleSystem>(4); // COLD ALLOC: List<ParticleSystem>[4] - editor forbidden particle detection scratch - owner: DronePrefabFactory
        private static readonly List<Transform> s_transformScratch = new List<Transform>(64); // COLD ALLOC: List<Transform>[64] - editor hierarchy/layer propagation scratch - owner: DronePrefabFactory
        private static readonly List<Material> s_materialScratch = new List<Material>(128); // COLD ALLOC: List<Material>[128] - editor shared material database scratch - owner: DronePrefabFactory
        private static readonly List<string> s_assetPathScratch = new List<string>(512); // COLD ALLOC: List<string>[512] - editor AssetDatabase path scratch - owner: DronePrefabFactory
        private static readonly List<VisualSegment> s_segmentScratch = new List<VisualSegment>(128); // COLD ALLOC: List<VisualSegment>[128] - editor visual source segment scratch - owner: DronePrefabFactory
        private static readonly List<BoneBuildData> s_boneScratch = new List<BoneBuildData>(16); // COLD ALLOC: List<BoneBuildData>[16] - editor bone metadata authoring scratch - owner: DronePrefabFactory
        private static readonly List<AttachmentBuildData> s_attachmentScratch = new List<AttachmentBuildData>(8); // COLD ALLOC: List<AttachmentBuildData>[8] - editor socket/VFX anchor authoring scratch - owner: DronePrefabFactory
        private static readonly List<CombineBucket> s_combineBuckets = new List<CombineBucket>(16); // COLD ALLOC: List<CombineBucket>[16] - editor per-bone mesh combine buckets - owner: DronePrefabFactory
        private static readonly List<Mesh> s_tempMeshes = new List<Mesh>(16); // COLD ALLOC: List<Mesh>[16] - editor temporary combined mesh cleanup list - owner: DronePrefabFactory
        private static readonly List<Mesh> s_dryRunCombinedMeshes = new List<Mesh>(16); // COLD ALLOC: List<Mesh>[16] - editor dry-run mesh cleanup list - owner: DronePrefabFactory
        private static readonly Dictionary<string, SourceGroup> s_groupMap =
            new Dictionary<string, SourceGroup>(128, StringComparer.Ordinal); // COLD ALLOC: Dictionary<string, SourceGroup>[128] - editor source group lookup - owner: DronePrefabFactory
        private static readonly Dictionary<string, bool> s_shaderCbufferCache =
            new Dictionary<string, bool>(128, StringComparer.Ordinal); // COLD ALLOC: Dictionary<string, bool>[128] - editor shader CBUFFER proof cache - owner: DronePrefabFactory

        [SerializeField] private string sourcePrefabDirectory = DefaultSourcePrefabDirectory;
        [SerializeField] private string meshDirectory = DefaultMeshDirectory;
        [SerializeField] private string materialDirectory = DefaultMaterialDirectory;
        [SerializeField] private string metadataDirectory = DefaultMetadataDirectory;
        [SerializeField] private string physicsProxyDirectory = DefaultPhysicsProxyDirectory;
        [SerializeField] private string generatedMeshDirectory = DefaultGeneratedMeshDirectory;
        [SerializeField] private string outputDirectory = DefaultOutputDirectory;
        [SerializeField] private float authoredQualityWeight = DefaultAuthoredQualityWeight;
        [SerializeField] private bool dryRun = true;
        [SerializeField] private int maxGroupsPerRun = MaxGroupsPerRunDefault;

        private Vector2 scroll;
        private FactoryReport lastReport;

        [MenuItem("HECTON-8/Assembly/Drone Prefab Factory 1738")]
        public static void OpenWindow()
        {
            DronePrefabFactory window = GetWindow<DronePrefabFactory>("Drone Factory 1738");
            window.minSize = new Vector2(760f, 520f);
            window.Show();
        }

        [MenuItem("HECTON-8/Assembly/Dry Run Drone Prefab Factory 1738")]
        public static void DryRunDefault()
        {
            FactorySettings settings = FactorySettings.Default;
            settings.DryRun = true;
            Run(settings);
        }

        [MenuItem("HECTON-8/Assembly/Run Drone Prefab Factory 1738")]
        public static void RunDefault()
        {
            FactorySettings settings = FactorySettings.Default;
            settings.DryRun = false;
            Run(settings);
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("HECTON-8 Drone Prefab Factory 1738", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Offline assembly: generated drone visual meshes, explicit bone hierarchy, primitive chassis colliders, and DroneBoneMetadata SOA joint table for Burst IK. No runtime hierarchy search.", MessageType.Info);

            sourcePrefabDirectory = EditorGUILayout.TextField("Source Prefab Directory", sourcePrefabDirectory);
            meshDirectory = EditorGUILayout.TextField("Mesh Directory", meshDirectory);
            materialDirectory = EditorGUILayout.TextField("Material Directory", materialDirectory);
            metadataDirectory = EditorGUILayout.TextField("Metadata Directory", metadataDirectory);
            physicsProxyDirectory = EditorGUILayout.TextField("Physics Proxy Directory", physicsProxyDirectory);
            generatedMeshDirectory = EditorGUILayout.TextField("Combined Mesh Output", generatedMeshDirectory);
            outputDirectory = EditorGUILayout.TextField("Prefab Output", outputDirectory);
            authoredQualityWeight = EditorGUILayout.Slider("GlobalQualityWeight", authoredQualityWeight, 0f, 1f);
            dryRun = EditorGUILayout.Toggle("Dry Run", dryRun);
            maxGroupsPerRun = EditorGUILayout.IntSlider("Max Groups", maxGroupsPerRun, 1, 4096);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Dry Run"))
                lastReport = Run(BuildSettings(true));
            if (GUILayout.Button("Assemble Prefabs"))
                lastReport = Run(BuildSettings(false));
            EditorGUILayout.EndHorizontal();

            if (lastReport == null)
                return;

            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.LabelField("Report", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Groups Discovered", lastReport.GroupsDiscovered.ToString(CultureInfo.InvariantCulture));
            EditorGUILayout.LabelField("Prefabs Assembled", lastReport.PrefabsAssembled.ToString(CultureInfo.InvariantCulture));
            EditorGUILayout.LabelField("Dry Run Passes", lastReport.PrefabsDryRunPassed.ToString(CultureInfo.InvariantCulture));
            EditorGUILayout.LabelField("Failed", lastReport.PrefabsFailed.ToString(CultureInfo.InvariantCulture));
            EditorGUILayout.LabelField("Combined Meshes", lastReport.CombinedMeshesCreated.ToString(CultureInfo.InvariantCulture));
            EditorGUILayout.LabelField("Primitive Colliders", lastReport.PrimitiveCollidersCreated.ToString(CultureInfo.InvariantCulture));
            EditorGUILayout.LabelField("Joints Serialized", lastReport.JointsSerialized.ToString(CultureInfo.InvariantCulture));
            EditorGUILayout.LabelField("Attachments Serialized", lastReport.AttachmentsSerialized.ToString(CultureInfo.InvariantCulture));
            for (int i = 0; i < lastReport.Violations.Count; i++)
                EditorGUILayout.LabelField(lastReport.Violations[i]);
            EditorGUILayout.EndScrollView();
        }

        private FactorySettings BuildSettings(bool dryRunOverride)
        {
            return new FactorySettings
            {
                SourcePrefabDirectory = sourcePrefabDirectory,
                MeshDirectory = meshDirectory,
                MaterialDirectory = materialDirectory,
                MetadataDirectory = metadataDirectory,
                PhysicsProxyDirectory = physicsProxyDirectory,
                GeneratedMeshDirectory = generatedMeshDirectory,
                OutputDirectory = outputDirectory,
                AuthoredQualityWeight = Mathf.Clamp01(authoredQualityWeight),
                DryRun = dryRunOverride,
                MaxGroupsPerRun = Mathf.Max(1, maxGroupsPerRun)
            }.Sanitize();
        }

        public static FactoryReport Run(FactorySettings settings)
        {
            settings = settings.Sanitize();
            Stopwatch stopwatch = Stopwatch.StartNew();
            FactoryReport report = new FactoryReport
            {
                AgentId = AgentId,
                SourcePrefabDirectory = settings.SourcePrefabDirectory,
                MeshDirectory = settings.MeshDirectory,
                MaterialDirectory = settings.MaterialDirectory,
                MetadataDirectory = settings.MetadataDirectory,
                PhysicsProxyDirectory = settings.PhysicsProxyDirectory,
                GeneratedMeshDirectory = settings.GeneratedMeshDirectory,
                OutputDirectory = settings.OutputDirectory,
                AuthoredQualityWeight = settings.AuthoredQualityWeight,
                DryRun = settings.DryRun
            };

            try
            {
                if (!DroneBoneMetadata.ValidateStaticLayout())
                    AddViolation(report, "FATAL: DroneBoneJointRuntimeData layout is not ARM64-safe.");
                if (!DroneAttachmentMetadata.ValidateStaticLayout())
                    AddViolation(report, "FATAL: DroneAttachmentRuntimeData layout is not ARM64-safe.");

                LoadMaterialDatabase(settings.MaterialDirectory, report);
                DiscoverSourceGroups(settings, report);
                report.GroupsDiscovered = s_groupMap.Count;
                if (s_groupMap.Count == 0)
                    AddViolation(report, "No drone source mesh or prefab groups found.");

                if (!settings.DryRun)
                {
                    EnsureAssetFolder(settings.OutputDirectory);
                    EnsureAssetFolder(settings.GeneratedMeshDirectory);
                }

                int processed = 0;
                foreach (KeyValuePair<string, SourceGroup> pair in s_groupMap)
                {
                    if (processed >= settings.MaxGroupsPerRun)
                        break;

                    processed++;
                    AssembleGroup(pair.Value, settings, report);
                }
            }
            catch (Exception exception)
            {
                AddViolation(report, "FATAL: DronePrefabFactory exception: " + exception.GetType().Name + " " + exception.Message);
                Debug.LogException(exception);
            }
            finally
            {
                stopwatch.Stop();
                report.ExecutionMicroseconds = TicksToMicroseconds(stopwatch.ElapsedTicks);
                report.PrefabsFailed = report.GroupReports.Count - report.PrefabsAssembled - report.PrefabsDryRunPassed;
                ClearScratch();
                if (!settings.DryRun)
                    AssetDatabase.SaveAssets();
            }

            Debug.Log("[DronePrefabFactory1738] Completed. Groups=" + report.GroupsDiscovered +
                      " Assembled=" + report.PrefabsAssembled +
                      " DryRun=" + report.PrefabsDryRunPassed +
                      " Failed=" + report.PrefabsFailed +
                      " us=" + report.ExecutionMicroseconds.ToString(CultureInfo.InvariantCulture));
            return report;
        }

        private static void AssembleGroup(SourceGroup group, FactorySettings settings, FactoryReport report)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            GroupReport groupReport = new GroupReport
            {
                DroneName = group.Name,
                SourcePath = group.SourcePath,
                OutputPath = ResolvePrefabPath(settings.OutputDirectory, group.Name),
                Status = "FAIL",
                SourceSegmentCount = group.Segments.Count
            };

            GameObject root = null;
            try
            {
                if (group.Segments.Count == 0)
                {
                    FailGroup(groupReport, report, "No visual mesh segments.");
                    return;
                }

                Material fallbackMaterial = ResolveBestMaterial(group.Name);
                if (fallbackMaterial == null)
                {
                    FailGroup(groupReport, report, "No shared drone material found.");
                    return;
                }

                root = new GameObject("PFB_" + SanitizeAssetName(group.Name));
                root.transform.position = Vector3.zero;
                root.transform.rotation = Quaternion.identity;
                root.transform.localScale = Vector3.one;

                Transform rigRoot = CreateChildTransform(root.transform, "RIG_Root", Vector3.zero, Quaternion.identity);
                Bounds visualBounds = ComputeGroupBounds(group);
                BuildBoneDefinitions(group, visualBounds, settings, s_boneScratch);
                Transform[] bones = CreateBoneHierarchy(rigRoot, s_boneScratch);
                DroneBoneJointDescriptor[] jointDescriptors = BuildJointDescriptors(s_boneScratch);
                groupReport.BoneCount = bones.Length;
                groupReport.JointCount = jointDescriptors.Length;
                report.JointsSerialized += jointDescriptors.Length;

                CombineVisualMeshesUnderBones(root.transform, group, s_boneScratch, bones, fallbackMaterial, settings, report, groupReport);
                Transform[] anchors = CreateAttachmentAnchors(
                    rigRoot,
                    bones,
                    s_boneScratch,
                    group,
                    visualBounds,
                    settings,
                    out DroneAttachmentAnchorDescriptor[] attachmentDescriptors,
                    groupReport);
                Renderer[] emissionRenderers = CollectEmissionRenderers(root);
                report.AttachmentsSerialized += attachmentDescriptors.Length;
                if (!AttachCollisionProxy(root.transform, group, visualBounds, settings, ResolveDroneLayer(), groupReport, out string collisionFailure))
                {
                    FailGroup(groupReport, report, collisionFailure);
                    return;
                }
                report.PrimitiveCollidersCreated += groupReport.PrimitiveColliderCount;
                ConfigureMetadata(root, rigRoot, bones, jointDescriptors, anchors, attachmentDescriptors, emissionRenderers, group, settings);

                if (!ValidatePrefabInstance(root, out string failure))
                {
                    FailGroup(groupReport, report, failure);
                    return;
                }

                if (settings.DryRun)
                {
                    groupReport.Status = "DRY_RUN_PASS";
                    report.PrefabsDryRunPassed++;
                    return;
                }

                GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(root, groupReport.OutputPath, out bool success);
                if (!success || savedPrefab == null)
                {
                    FailGroup(groupReport, report, "PrefabUtility.SaveAsPrefabAsset failed.");
                    DeletePrefabAsset(groupReport.OutputPath);
                    return;
                }

                if (!ValidateSavedPrefab(groupReport.OutputPath, out failure))
                {
                    FailGroup(groupReport, report, "Saved prefab validation failed: " + failure);
                    DeletePrefabAsset(groupReport.OutputPath);
                    return;
                }

                groupReport.Status = "PASS";
                report.PrefabsAssembled++;
            }
            finally
            {
                stopwatch.Stop();
                groupReport.EditorMicroseconds = TicksToMicroseconds(stopwatch.ElapsedTicks);
                report.GroupReports.Add(groupReport);
                if (root != null)
                    DestroyImmediate(root);
                ClearPerGroupScratch();
            }
        }

        private static void DiscoverSourceGroups(FactorySettings settings, FactoryReport report)
        {
            DiscoverPrefabGroups(settings.SourcePrefabDirectory, report);
            DiscoverMeshGroups(settings.MeshDirectory, report);
        }

        private static void DiscoverPrefabGroups(string prefabDirectory, FactoryReport report)
        {
            if (string.IsNullOrWhiteSpace(prefabDirectory) || !AssetDatabase.IsValidFolder(prefabDirectory))
                return;

            string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { prefabDirectory });
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                GameObject root = null;
                try
                {
                    root = PrefabUtility.LoadPrefabContents(path);
                    if (root == null)
                        continue;

                    string groupName = ResolveGroupName(Path.GetFileNameWithoutExtension(path));
                    SourceGroup group = ResolveGroup(groupName, path);
                    root.GetComponentsInChildren(true, s_meshFilterScratch);
                    for (int filterIndex = 0; filterIndex < s_meshFilterScratch.Count; filterIndex++)
                    {
                        MeshFilter filter = s_meshFilterScratch[filterIndex];
                        if (filter == null || filter.sharedMesh == null)
                            continue;

                        MeshRenderer renderer = filter.GetComponent<MeshRenderer>();
                        AddSegment(group, filter.sharedMesh, renderer != null ? renderer.sharedMaterials : null, root.transform, filter.transform, path);
                    }

                    root.GetComponentsInChildren(true, s_skinnedRendererScratch);
                    for (int rendererIndex = 0; rendererIndex < s_skinnedRendererScratch.Count; rendererIndex++)
                    {
                        SkinnedMeshRenderer renderer = s_skinnedRendererScratch[rendererIndex];
                        if (renderer == null || renderer.sharedMesh == null)
                            continue;

                        AddSegment(group, renderer.sharedMesh, renderer.sharedMaterials, root.transform, renderer.transform, path);
                    }
                }
                catch (Exception exception)
                {
                    AddViolation(report, "Prefab source read failed: " + path + " " + exception.GetType().Name + " " + exception.Message);
                }
                finally
                {
                    s_meshFilterScratch.Clear();
                    s_skinnedRendererScratch.Clear();
                    if (root != null)
                        PrefabUtility.UnloadPrefabContents(root);
                }
            }
        }

        private static void DiscoverMeshGroups(string meshDirectory, FactoryReport report)
        {
            if (string.IsNullOrWhiteSpace(meshDirectory) || !AssetDatabase.IsValidFolder(meshDirectory))
                return;

            string[] guids = AssetDatabase.FindAssets("t:Mesh", new[] { meshDirectory });
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if (mesh == null)
                    continue;

                string fileName = Path.GetFileNameWithoutExtension(path);
                string groupName = ResolveGroupName(fileName);
                SourceGroup group = ResolveGroup(groupName, path);
                AddSegment(group, mesh, null, null, null, path);
            }
        }

        private static SourceGroup ResolveGroup(string groupName, string sourcePath)
        {
            string safeGroup = string.IsNullOrWhiteSpace(groupName) ? "RepairDrone" : SanitizeAssetName(groupName);
            if (!s_groupMap.TryGetValue(safeGroup, out SourceGroup group))
            {
                group = new SourceGroup
                {
                    Name = safeGroup,
                    SourcePath = sourcePath
                };
                s_groupMap.Add(safeGroup, group);
            }

            return group;
        }

        private static void AddSegment(
            SourceGroup group,
            Mesh mesh,
            Material[] materials,
            Transform root,
            Transform segmentTransform,
            string sourcePath)
        {
            if (group == null || mesh == null)
                return;

            Matrix4x4 localMatrix = Matrix4x4.identity;
            if (root != null && segmentTransform != null)
                localMatrix = root.worldToLocalMatrix * segmentTransform.localToWorldMatrix;

            group.Segments.Add(new VisualSegment
            {
                Mesh = mesh,
                Materials = materials,
                LocalMatrix = localMatrix,
                BoneName = ResolveBoneName(mesh.name + "_" + Path.GetFileNameWithoutExtension(sourcePath)),
                SourceName = mesh.name,
                SourcePath = sourcePath
            });
        }

        private static void BuildBoneDefinitions(
            SourceGroup group,
            Bounds visualBounds,
            FactorySettings settings,
            List<BoneBuildData> output)
        {
            output.Clear();
            if (TryLoadAuthoringBones(group.Name, settings.MetadataDirectory, output))
                return;

            Vector3 extents = SanitizeExtents(visualBounds.extents);
            output.Add(new BoneBuildData
            {
                Name = "BONE_Chassis",
                ParentName = string.Empty,
                LocalPosition = Vector3.zero,
                LocalRotation = Quaternion.identity,
                LocalAxis = Vector3.up,
                LimitPlaneNormal = Vector3.forward,
                MinAngleDegrees = 0f,
                MaxAngleDegrees = 0f,
                Flags = DroneBoneSolverFlags.Active | DroneBoneSolverFlags.Chassis,
                TierMask = DroneBoneTierMask.All,
                Stiffness = 1f,
                Damping = 0.45f,
                SolverWeight = 1f
            });
            output.Add(new BoneBuildData
            {
                Name = "BONE_ServiceArm_L",
                ParentName = "BONE_Chassis",
                LocalPosition = new Vector3(-extents.x * 0.72f, -extents.y * 0.18f, extents.z * 0.18f),
                LocalRotation = Quaternion.identity,
                LocalAxis = Vector3.right,
                LimitPlaneNormal = Vector3.forward,
                MinAngleDegrees = -65f,
                MaxAngleDegrees = 58f,
                Flags = DroneBoneSolverFlags.Active | DroneBoneSolverFlags.ServiceArm,
                TierMask = DroneBoneTierMask.All,
                Stiffness = 0.82f,
                Damping = 0.32f,
                SolverWeight = 1f
            });
            output.Add(new BoneBuildData
            {
                Name = "BONE_ServiceArm_R",
                ParentName = "BONE_Chassis",
                LocalPosition = new Vector3(extents.x * 0.72f, -extents.y * 0.18f, extents.z * 0.18f),
                LocalRotation = Quaternion.identity,
                LocalAxis = Vector3.left,
                LimitPlaneNormal = Vector3.forward,
                MinAngleDegrees = -58f,
                MaxAngleDegrees = 65f,
                Flags = DroneBoneSolverFlags.Active | DroneBoneSolverFlags.ServiceArm,
                TierMask = DroneBoneTierMask.All,
                Stiffness = 0.82f,
                Damping = 0.32f,
                SolverWeight = 1f
            });
            output.Add(new BoneBuildData
            {
                Name = "BONE_ToolMount",
                ParentName = "BONE_Chassis",
                LocalPosition = new Vector3(0f, -extents.y * 0.62f, extents.z * 0.48f),
                LocalRotation = Quaternion.identity,
                LocalAxis = Vector3.right,
                LimitPlaneNormal = Vector3.up,
                MinAngleDegrees = -82f,
                MaxAngleDegrees = 82f,
                Flags = DroneBoneSolverFlags.Active | DroneBoneSolverFlags.ServiceArm,
                TierMask = DroneBoneTierMask.All,
                Stiffness = 0.9f,
                Damping = 0.28f,
                SolverWeight = 1f
            });
            output.Add(new BoneBuildData
            {
                Name = "BONE_SensorMast",
                ParentName = "BONE_Chassis",
                LocalPosition = new Vector3(0f, extents.y * 0.7f, -extents.z * 0.25f),
                LocalRotation = Quaternion.identity,
                LocalAxis = Vector3.up,
                LimitPlaneNormal = Vector3.forward,
                MinAngleDegrees = -35f,
                MaxAngleDegrees = 35f,
                Flags = DroneBoneSolverFlags.Active | DroneBoneSolverFlags.Sensor,
                TierMask = DroneBoneTierMask.Middle | DroneBoneTierMask.High | DroneBoneTierMask.Ultra,
                Stiffness = 0.68f,
                Damping = 0.38f,
                SolverWeight = 0.75f
            });
            output.Add(new BoneBuildData
            {
                Name = "BONE_ThrusterRing",
                ParentName = "BONE_Chassis",
                LocalPosition = new Vector3(0f, 0f, -extents.z * 0.82f),
                LocalRotation = Quaternion.identity,
                LocalAxis = Vector3.forward,
                LimitPlaneNormal = Vector3.up,
                MinAngleDegrees = -22f,
                MaxAngleDegrees = 22f,
                Flags = DroneBoneSolverFlags.Active | DroneBoneSolverFlags.Thruster,
                TierMask = DroneBoneTierMask.High | DroneBoneTierMask.Ultra,
                Stiffness = 0.55f,
                Damping = 0.5f,
                SolverWeight = 0.55f,
                VisualOverkillOffset = new Vector3(0f, 0.015f, 0f)
            });
        }

        private static Transform[] CreateBoneHierarchy(Transform rigRoot, List<BoneBuildData> bones)
        {
            Transform[] result = new Transform[bones.Count]; // COLD ALLOC: Transform[bones.Count] - serialized prefab bone table - owner: DronePrefabFactory
            for (int i = 0; i < bones.Count; i++)
            {
                BoneBuildData bone = bones[i];
                Transform parent = rigRoot;
                int parentIndex = FindBoneIndex(bones, bone.ParentName);
                if (parentIndex >= 0 && parentIndex < i && result[parentIndex] != null)
                    parent = result[parentIndex];

                Transform created = CreateChildTransform(parent, bone.Name, bone.LocalPosition, bone.LocalRotation);
                result[i] = created;
            }

            return result;
        }

        private static DroneBoneJointDescriptor[] BuildJointDescriptors(List<BoneBuildData> bones)
        {
            DroneBoneJointDescriptor[] descriptors = new DroneBoneJointDescriptor[bones.Count]; // COLD ALLOC: DroneBoneJointDescriptor[bones.Count] - serialized prefab joint table - owner: DronePrefabFactory
            for (int i = 0; i < bones.Count; i++)
            {
                BoneBuildData bone = bones[i];
                int parentIndex = FindBoneIndex(bones, bone.ParentName);
                descriptors[i] = new DroneBoneJointDescriptor
                {
                    BoneIndex = i,
                    ParentIndex = parentIndex,
                    BoneHash = HashString(bone.Name),
                    SolverFlags = bone.Flags == 0 ? DroneBoneSolverFlags.Active : bone.Flags,
                    TierMask = bone.TierMask == 0 ? DroneBoneTierMask.All : bone.TierMask,
                    BindLocalPosition = bone.LocalPosition,
                    BindLocalRotation = bone.LocalRotation,
                    LocalAxis = SanitizeDirection(bone.LocalAxis, Vector3.up),
                    LimitPlaneNormal = SanitizeDirection(bone.LimitPlaneNormal, Vector3.forward),
                    MinAngleDegrees = bone.MinAngleDegrees,
                    MaxAngleDegrees = bone.MaxAngleDegrees,
                    Stiffness = Mathf.Max(0f, bone.Stiffness),
                    Damping = Mathf.Max(0f, bone.Damping),
                    SolverWeight = Mathf.Clamp01(bone.SolverWeight <= 0f ? 1f : bone.SolverWeight),
                    VisualOverkillOffset = SanitizeVector(bone.VisualOverkillOffset, Vector3.zero)
                };
            }

            return descriptors;
        }

        private static void CombineVisualMeshesUnderBones(
            Transform root,
            SourceGroup group,
            List<BoneBuildData> boneDefinitions,
            Transform[] bones,
            Material fallbackMaterial,
            FactorySettings settings,
            FactoryReport report,
            GroupReport groupReport)
        {
            int boneCount = Mathf.Min(boneDefinitions.Count, bones.Length);
            for (int boneIndex = 0; boneIndex < boneCount; boneIndex++)
            {
                string boneName = boneDefinitions[boneIndex].Name;
                Transform bone = bones[boneIndex];
                if (string.IsNullOrEmpty(boneName) || bone == null)
                    continue;

                s_segmentScratch.Clear();
                for (int i = 0; i < group.Segments.Count; i++)
                {
                    VisualSegment segment = group.Segments[i];
                    if (string.Equals(segment.BoneName, boneName, StringComparison.Ordinal))
                        s_segmentScratch.Add(segment);
                }

                if (s_segmentScratch.Count == 0 && string.Equals(boneName, "BONE_Chassis", StringComparison.Ordinal))
                {
                    for (int i = 0; i < group.Segments.Count; i++)
                        s_segmentScratch.Add(group.Segments[i]);
                }

                if (s_segmentScratch.Count == 0)
                    continue;

                Mesh combined = BuildCombinedMeshForBone(root, bone, boneName, group.Name, fallbackMaterial, s_segmentScratch, settings, out Material[] materials);
                if (combined == null)
                    continue;

                GameObject visual = new GameObject("VIS_" + boneName.Substring("BONE_".Length));
                visual.transform.SetParent(bone, false);
                visual.transform.localPosition = Vector3.zero;
                visual.transform.localRotation = Quaternion.identity;
                visual.transform.localScale = Vector3.one;
                MeshFilter filter = visual.AddComponent<MeshFilter>();
                filter.sharedMesh = combined;
                MeshRenderer renderer = visual.AddComponent<MeshRenderer>();
                renderer.shadowCastingMode = ShadowCastingMode.On;
                renderer.receiveShadows = true;
                renderer.sharedMaterials = materials;
                groupReport.CombinedMeshCount++;
                report.CombinedMeshesCreated++;
            }
        }

        private static Mesh BuildCombinedMeshForBone(
            Transform root,
            Transform bone,
            string boneName,
            string groupName,
            Material fallbackMaterial,
            List<VisualSegment> segments,
            FactorySettings settings,
            out Material[] materials)
        {
            materials = Array.Empty<Material>();
            ClearCombineScratch();
            bool requiresNormalRebuild = false;
            for (int i = 0; i < segments.Count; i++)
            {
                VisualSegment segment = segments[i];
                Mesh mesh = segment.Mesh;
                if (mesh == null)
                    continue;

                if (!mesh.HasVertexAttribute(VertexAttribute.Normal))
                    requiresNormalRebuild = true;

                int subMeshCount = Mathf.Max(1, mesh.subMeshCount);
                for (int subMesh = 0; subMesh < subMeshCount; subMesh++)
                {
                    Material material = ResolveSegmentMaterial(segment, subMesh, fallbackMaterial);
                    CombineBucket bucket = ResolveBucket(material);
                    bucket.Instances.Add(new CombineInstance
                    {
                        mesh = mesh,
                        subMeshIndex = Mathf.Min(subMesh, mesh.subMeshCount - 1),
                        transform = bone.worldToLocalMatrix * root.localToWorldMatrix * segment.LocalMatrix
                    });
                }
            }

            if (s_combineBuckets.Count == 0)
                return null;

            CombineInstance[] finalInstances = new CombineInstance[s_combineBuckets.Count]; // COLD ALLOC: CombineInstance[s_combineBuckets.Count] - editor mesh combine array - owner: DronePrefabFactory
            materials = new Material[s_combineBuckets.Count]; // COLD ALLOC: Material[s_combineBuckets.Count] - serialized renderer material slots - owner: DronePrefabFactory
            for (int i = 0; i < s_combineBuckets.Count; i++)
            {
                CombineBucket bucket = s_combineBuckets[i];
                Mesh subMesh = new Mesh
                {
                    name = "TMP_" + groupName + "_" + boneName + "_" + i.ToString(CultureInfo.InvariantCulture),
                    indexFormat = IndexFormat.UInt32
                };
                CombineInstance[] bucketInstances = new CombineInstance[bucket.Instances.Count]; // COLD ALLOC: CombineInstance[bucket.Instances.Count] - exact editor CombineMeshes input - owner: DronePrefabFactory
                bucket.Instances.CopyTo(bucketInstances);
                subMesh.CombineMeshes(bucketInstances, true, true);
                s_tempMeshes.Add(subMesh);
                finalInstances[i] = new CombineInstance
                {
                    mesh = subMesh,
                    subMeshIndex = 0,
                    transform = Matrix4x4.identity
                };
                materials[i] = bucket.Material;
            }

            Mesh combined = new Mesh
            {
                name = "MESH_" + SanitizeAssetName(groupName) + "_" + SanitizeAssetName(boneName),
                indexFormat = IndexFormat.UInt32
            };
            combined.CombineMeshes(finalInstances, false, false);
            combined.RecalculateBounds();
            if (requiresNormalRebuild)
                combined.RecalculateNormals();

            if (!settings.DryRun)
            {
                string path = ResolveCombinedMeshPath(settings.GeneratedMeshDirectory, groupName, boneName);
                if (AssetDatabase.LoadAssetAtPath<Mesh>(path) != null)
                    AssetDatabase.DeleteAsset(path);
                AssetDatabase.CreateAsset(combined, path);
                combined = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            }
            else
            {
                combined.hideFlags = HideFlags.DontSave;
                s_dryRunCombinedMeshes.Add(combined);
            }

            for (int i = 0; i < s_tempMeshes.Count; i++)
                DestroyImmediate(s_tempMeshes[i]);

            s_tempMeshes.Clear();
            return combined;
        }

        private static CombineBucket ResolveBucket(Material material)
        {
            for (int i = 0; i < s_combineBuckets.Count; i++)
            {
                if (ReferenceEquals(s_combineBuckets[i].Material, material))
                    return s_combineBuckets[i];
            }

            CombineBucket bucket = new CombineBucket { Material = material };
            s_combineBuckets.Add(bucket);
            return bucket;
        }

        private static Transform[] CreateAttachmentAnchors(
            Transform rigRoot,
            Transform[] bones,
            List<BoneBuildData> boneDefinitions,
            SourceGroup group,
            Bounds visualBounds,
            FactorySettings settings,
            out DroneAttachmentAnchorDescriptor[] descriptors,
            GroupReport groupReport)
        {
            s_attachmentScratch.Clear();
            if (!TryLoadAuthoringAttachments(group.Name, settings.MetadataDirectory, s_attachmentScratch))
                BuildDefaultAttachments(visualBounds, s_attachmentScratch);
            else
                EnsureMandatoryAttachmentAnchors(visualBounds, s_attachmentScratch);

            Transform[] anchors = new Transform[s_attachmentScratch.Count]; // COLD ALLOC: Transform[attachmentCount] - serialized drone tool/vfx anchor refs - owner: DronePrefabFactory
            descriptors = new DroneAttachmentAnchorDescriptor[s_attachmentScratch.Count]; // COLD ALLOC: DroneAttachmentAnchorDescriptor[attachmentCount] - serialized drone anchor table - owner: DronePrefabFactory
            for (int i = 0; i < s_attachmentScratch.Count; i++)
            {
                AttachmentBuildData attachment = s_attachmentScratch[i];
                int boneIndex = FindBoneIndex(boneDefinitions, attachment.BoneName);
                Transform parent = boneIndex >= 0 && boneIndex < bones.Length && bones[boneIndex] != null
                    ? bones[boneIndex]
                    : rigRoot;

                Transform anchor = CreateChildTransform(
                    parent,
                    SanitizeAttachmentName(attachment.Name, attachment.Kind),
                    attachment.LocalPosition,
                    ResolveAttachmentRotation(attachment.LocalForward, attachment.LocalUp));
                anchors[i] = anchor;
                descriptors[i] = new DroneAttachmentAnchorDescriptor
                {
                    AnchorIndex = i,
                    BoneIndex = boneIndex,
                    AnchorHash = HashString(anchor.name),
                    Kind = attachment.Kind,
                    TierMask = attachment.TierMask == 0 ? DroneBoneTierMask.All : attachment.TierMask,
                    Flags = attachment.Flags == 0 ? DroneAttachmentFlags.Active : attachment.Flags,
                    LocalPosition = anchor.localPosition,
                    LocalForward = SanitizeDirection(attachment.LocalForward, Vector3.forward),
                    LocalUp = SanitizeDirection(attachment.LocalUp, Vector3.up),
                    MinQualityWeight = Mathf.Clamp01(attachment.MinQualityWeight)
                };
            }

            groupReport.AttachmentCount = anchors.Length;
            return anchors;
        }

        private static Renderer[] CollectEmissionRenderers(GameObject root)
        {
            root.GetComponentsInChildren(true, s_rendererScratch);
            int count = 0;
            for (int i = 0; i < s_rendererScratch.Count; i++)
            {
                Renderer renderer = s_rendererScratch[i];
                if (renderer == null)
                    continue;

                Material[] materials = renderer.sharedMaterials;
                bool acceptsEmission = false;
                for (int materialIndex = 0; materialIndex < materials.Length; materialIndex++)
                {
                    Material material = materials[materialIndex];
                    if (material != null && material.HasProperty(EmissionColorPropertyName))
                    {
                        acceptsEmission = true;
                        break;
                    }
                }

                if (acceptsEmission)
                    count++;
            }

            Renderer[] result = new Renderer[count]; // COLD ALLOC: Renderer[emissionCount] - direct drone emission renderer refs - owner: DronePrefabFactory
            int cursor = 0;
            for (int i = 0; i < s_rendererScratch.Count; i++)
            {
                Renderer renderer = s_rendererScratch[i];
                if (renderer == null)
                    continue;

                Material[] materials = renderer.sharedMaterials;
                bool acceptsEmission = false;
                for (int materialIndex = 0; materialIndex < materials.Length; materialIndex++)
                {
                    Material material = materials[materialIndex];
                    if (material != null && material.HasProperty(EmissionColorPropertyName))
                    {
                        acceptsEmission = true;
                        break;
                    }
                }

                if (acceptsEmission)
                    result[cursor++] = renderer;
            }

            s_rendererScratch.Clear();
            return result;
        }

        private static bool TryLoadAuthoringAttachments(string groupName, string metadataDirectory, List<AttachmentBuildData> output)
        {
            output.Clear();
            if (string.IsNullOrWhiteSpace(metadataDirectory) || !AssetDatabase.IsValidFolder(metadataDirectory))
                return false;

            TextAsset metadata = FindBestMetadata(groupName, metadataDirectory);
            if (metadata == null || string.IsNullOrWhiteSpace(metadata.text))
                return false;

            try
            {
                DroneAuthoringFile file = JsonUtility.FromJson<DroneAuthoringFile>(metadata.text);
                if (file == null)
                    return false;

                AppendAuthoringAttachments(file.attachments, DroneAttachmentKind.None, output);
                AppendAuthoringAttachments(file.sockets, DroneAttachmentKind.ToolSocket, output);
                AppendAuthoringAttachments(file.thrusters, DroneAttachmentKind.Thruster, output);
                return output.Count > 0;
            }
            catch (Exception exception)
            {
                Debug.LogError("[DronePrefabFactory1738] Attachment metadata parse failed for " + groupName + ": " + exception.GetType().Name + " " + exception.Message);
                output.Clear();
                return false;
            }
        }

        private static void AppendAuthoringAttachments(
            DroneAttachmentAuthoringRecord[] records,
            DroneAttachmentKind fallbackKind,
            List<AttachmentBuildData> output)
        {
            if (records == null)
                return;

            for (int i = 0; i < records.Length; i++)
            {
                DroneAttachmentAuthoringRecord record = records[i];
                DroneAttachmentKind kind = ResolveAttachmentKind(record.kind, fallbackKind);
                Vector3 forward = SanitizeDirection(FirstNonZero(record.localForward, record.forward, record.normal, Vector3.forward), Vector3.forward);
                Vector3 up = SanitizeDirection(FirstNonZero(record.localUp, record.up, Vector3.up, Vector3.up), Vector3.up);
                output.Add(new AttachmentBuildData
                {
                    Name = string.IsNullOrWhiteSpace(record.name) ? DefaultAttachmentName(kind) : SanitizeAssetName(record.name),
                    BoneName = NormalizeBoneName(record.bone),
                    Kind = kind,
                    LocalPosition = SanitizeVector(record.localPosition, Vector3.zero),
                    LocalForward = forward,
                    LocalUp = up,
                    TierMask = record.tierMask == 0 ? DroneBoneTierMask.All : (DroneBoneTierMask)record.tierMask,
                    Flags = record.flags == 0 ? DefaultAttachmentFlags(kind) : (DroneAttachmentFlags)record.flags,
                    MinQualityWeight = Mathf.Clamp01(SanitizeFinite(record.minQualityWeight, 0f))
                });
            }
        }

        private static void BuildDefaultAttachments(Bounds visualBounds, List<AttachmentBuildData> output)
        {
            output.Clear();
            AppendDefaultToolSocket(visualBounds, output);
            AppendDefaultThruster(visualBounds, output);
        }

        private static void EnsureMandatoryAttachmentAnchors(Bounds visualBounds, List<AttachmentBuildData> output)
        {
            bool hasToolSocket = false;
            bool hasThruster = false;
            for (int i = 0; i < output.Count; i++)
            {
                AttachmentBuildData attachment = output[i];
                hasToolSocket |= attachment.Kind == DroneAttachmentKind.ToolSocket;
                hasThruster |= attachment.Kind == DroneAttachmentKind.Thruster;
            }

            if (!hasToolSocket)
                AppendDefaultToolSocket(visualBounds, output);
            if (!hasThruster)
                AppendDefaultThruster(visualBounds, output);
        }

        private static void AppendDefaultToolSocket(Bounds visualBounds, List<AttachmentBuildData> output)
        {
            Bounds bounds = SanitizeBounds(visualBounds);
            Vector3 extents = bounds.extents;
            output.Add(new AttachmentBuildData
            {
                Name = "Socket_Tool",
                BoneName = "BONE_ToolMount",
                Kind = DroneAttachmentKind.ToolSocket,
                LocalPosition = new Vector3(0f, -extents.y * 0.12f, extents.z * 0.12f),
                LocalForward = Vector3.forward,
                LocalUp = Vector3.up,
                TierMask = DroneBoneTierMask.All,
                Flags = DroneAttachmentFlags.Active | DroneAttachmentFlags.ToolSnap,
                MinQualityWeight = 0f
            });
        }

        private static void AppendDefaultThruster(Bounds visualBounds, List<AttachmentBuildData> output)
        {
            Bounds bounds = SanitizeBounds(visualBounds);
            Vector3 extents = bounds.extents;
            output.Add(new AttachmentBuildData
            {
                Name = "VFX_Thruster",
                BoneName = "BONE_ThrusterRing",
                Kind = DroneAttachmentKind.Thruster,
                LocalPosition = new Vector3(0f, 0f, -extents.z * 0.16f),
                LocalForward = Vector3.back,
                LocalUp = Vector3.up,
                TierMask = DroneBoneTierMask.Middle | DroneBoneTierMask.High | DroneBoneTierMask.Ultra,
                Flags = DroneAttachmentFlags.Active | DroneAttachmentFlags.EmitsVfx | DroneAttachmentFlags.VisualOnly,
                MinQualityWeight = 0.15f
            });
        }

        private static Quaternion ResolveAttachmentRotation(Vector3 forward, Vector3 up)
        {
            Vector3 safeForward = SanitizeDirection(forward, Vector3.forward);
            Vector3 safeUp = SanitizeDirection(up, Vector3.up);
            if (Vector3.Cross(safeForward, safeUp).sqrMagnitude <= 0.000001f)
                safeUp = Vector3.up;

            if (Vector3.Cross(safeForward, safeUp).sqrMagnitude <= 0.000001f)
                safeUp = Vector3.right;

            return Quaternion.LookRotation(safeForward, safeUp);
        }

        private static string SanitizeAttachmentName(string name, DroneAttachmentKind kind)
        {
            string sanitized = SanitizeAssetName(name);
            if (kind == DroneAttachmentKind.ToolSocket)
                return "Socket_Tool";
            if (kind == DroneAttachmentKind.Thruster)
                return "VFX_Thruster";

            return string.IsNullOrEmpty(sanitized) ? DefaultAttachmentName(kind) : sanitized;
        }

        private static string DefaultAttachmentName(DroneAttachmentKind kind)
        {
            switch (kind)
            {
                case DroneAttachmentKind.Thruster: return "VFX_Thruster";
                case DroneAttachmentKind.Sensor: return "Socket_Sensor";
                case DroneAttachmentKind.StatusLight: return "Socket_StatusLight";
                default: return "Socket_Tool";
            }
        }

        private static DroneAttachmentKind ResolveAttachmentKind(string value, DroneAttachmentKind fallback)
        {
            string normalized = NormalizeSearch(value);
            if (normalized.Contains("thruster") || normalized.Contains("exhaust") || normalized.Contains("vfx"))
                return DroneAttachmentKind.Thruster;
            if (normalized.Contains("sensor") || normalized.Contains("camera") || normalized.Contains("scanner"))
                return DroneAttachmentKind.Sensor;
            if (normalized.Contains("status") || normalized.Contains("light") || normalized.Contains("eye"))
                return DroneAttachmentKind.StatusLight;
            if (normalized.Contains("tool") || normalized.Contains("socket") || normalized.Contains("welder") || normalized.Contains("laser"))
                return DroneAttachmentKind.ToolSocket;

            return fallback == DroneAttachmentKind.None ? DroneAttachmentKind.ToolSocket : fallback;
        }

        private static DroneAttachmentFlags DefaultAttachmentFlags(DroneAttachmentKind kind)
        {
            if (kind == DroneAttachmentKind.Thruster)
                return DroneAttachmentFlags.Active | DroneAttachmentFlags.EmitsVfx | DroneAttachmentFlags.VisualOnly;
            if (kind == DroneAttachmentKind.ToolSocket)
                return DroneAttachmentFlags.Active | DroneAttachmentFlags.ToolSnap;

            return DroneAttachmentFlags.Active;
        }

        private static Vector3 FirstNonZero(Vector3 a, Vector3 b, Vector3 c, Vector3 fallback)
        {
            if (IsFinite(a) && a.sqrMagnitude > 0.000001f)
                return a;
            if (IsFinite(b) && b.sqrMagnitude > 0.000001f)
                return b;
            if (IsFinite(c) && c.sqrMagnitude > 0.000001f)
                return c;

            return fallback;
        }

        private static bool AttachCollisionProxy(
            Transform root,
            SourceGroup group,
            Bounds visualBounds,
            FactorySettings settings,
            int droneLayer,
            GroupReport groupReport,
            out string failure)
        {
            failure = string.Empty;
            root.gameObject.layer = droneLayer;
            Transform collisionRoot = CreateChildTransform(root, "COL_Proxy", Vector3.zero, Quaternion.identity);
            collisionRoot.gameObject.layer = droneLayer;
            Rigidbody body = collisionRoot.gameObject.AddComponent<Rigidbody>();
            body.mass = ResolveDroneMassKg(visualBounds);
            body.useGravity = false;
            body.isKinematic = true;
            body.interpolation = RigidbodyInterpolation.None;
            body.collisionDetectionMode = CollisionDetectionMode.Discrete;

            GameObject proxy = TryInstantiatePhysicsProxy(group.Name, settings.PhysicsProxyDirectory);
            if (proxy != null)
            {
                proxy.name = "COL_SourceProxy";
                proxy.transform.SetParent(collisionRoot, false);
                proxy.transform.localPosition = Vector3.zero;
                proxy.transform.localRotation = Quaternion.identity;
                proxy.transform.localScale = Vector3.one;
                AssignLayerRecursive(proxy.transform, droneLayer);

                if (!ValidateCollisionPrimitiveTree(proxy, droneLayer, out int proxyColliderCount, out failure))
                {
                    DestroyImmediate(proxy);
                    return false;
                }

                groupReport.PrimitiveColliderCount += proxyColliderCount;
                groupReport.ProxyColliderCount = proxyColliderCount;
                return true;
            }

            AttachPrimitiveChassisColliders(collisionRoot, visualBounds, droneLayer, groupReport);
            return true;
        }

        private static void AttachPrimitiveChassisColliders(
            Transform collisionRoot,
            Bounds visualBounds,
            int droneLayer,
            GroupReport groupReport)
        {
            Bounds bounds = SanitizeBounds(visualBounds);
            Vector3 center = bounds.center;
            Vector3 size = bounds.size;
            size.x = Mathf.Max(size.x, MinimumBoundsExtent * 2f);
            size.y = Mathf.Max(size.y, MinimumBoundsExtent * 2f);
            size.z = Mathf.Max(size.z, MinimumBoundsExtent * 2f);

            GameObject chassis = new GameObject("COL_Chassis");
            chassis.layer = droneLayer;
            chassis.transform.SetParent(collisionRoot, false);
            chassis.transform.localPosition = center;
            chassis.transform.localRotation = Quaternion.identity;
            chassis.transform.localScale = Vector3.one;
            BoxCollider box = chassis.AddComponent<BoxCollider>();
            box.center = Vector3.zero;
            box.size = new Vector3(size.x * 0.82f, size.y * 0.72f, size.z * 0.72f);
            groupReport.PrimitiveColliderCount++;

            CreateArmBox(collisionRoot, "COL_ServiceArm_L", new Vector3(center.x - size.x * 0.38f, center.y, center.z), size, droneLayer, groupReport);
            CreateArmBox(collisionRoot, "COL_ServiceArm_R", new Vector3(center.x + size.x * 0.38f, center.y, center.z), size, droneLayer, groupReport);

            collisionRoot.root.gameObject.layer = droneLayer;
        }

        private static void CreateArmBox(
            Transform parent,
            string name,
            Vector3 localPosition,
            Vector3 boundsSize,
            int layer,
            GroupReport groupReport)
        {
            GameObject arm = new GameObject(name);
            arm.layer = layer;
            arm.transform.SetParent(parent, false);
            arm.transform.localPosition = localPosition;
            arm.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            arm.transform.localScale = Vector3.one;
            BoxCollider box = arm.AddComponent<BoxCollider>();
            box.center = Vector3.zero;
            box.size = new Vector3(
                Mathf.Max(MinimumBoundsExtent, boundsSize.x * 0.42f),
                Mathf.Max(MinimumBoundsExtent, boundsSize.y * 0.22f),
                Mathf.Max(MinimumBoundsExtent, boundsSize.z * 0.18f));
            groupReport.PrimitiveColliderCount++;
        }

        private static GameObject TryInstantiatePhysicsProxy(string groupName, string physicsProxyDirectory)
        {
            GameObject proxyAsset = FindPhysicsProxyAsset(groupName, physicsProxyDirectory);
            if (proxyAsset == null)
                return null;

            Object instance = PrefabUtility.InstantiatePrefab(proxyAsset);
            GameObject proxy = instance as GameObject;
            if (proxy != null)
                return proxy;

            return Object.Instantiate(proxyAsset);
        }

        private static GameObject FindPhysicsProxyAsset(string groupName, string physicsProxyDirectory)
        {
            if (string.IsNullOrWhiteSpace(physicsProxyDirectory) || !AssetDatabase.IsValidFolder(physicsProxyDirectory))
                return null;

            string expectedName = "COL_" + SanitizeAssetName(groupName);
            string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { physicsProxyDirectory });
            GameObject best = null;
            int bestScore = int.MinValue;
            string expectedNeedle = NormalizeSearch(expectedName);
            string groupNeedle = NormalizeSearch(groupName);
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                string fileName = Path.GetFileNameWithoutExtension(path);
                string normalized = NormalizeSearch(fileName);
                int score = 0;
                if (string.Equals(fileName, expectedName, StringComparison.Ordinal))
                    score += 200;
                if (normalized.Contains(expectedNeedle))
                    score += 100;
                if (normalized.Contains(groupNeedle))
                    score += 40;
                if (normalized.Contains("col") || normalized.Contains("collision") || normalized.Contains("proxy"))
                    score += 10;

                if (score > bestScore)
                {
                    GameObject candidate = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (candidate != null)
                    {
                        best = candidate;
                        bestScore = score;
                    }
                }
            }

            return bestScore > 0 ? best : null;
        }

        private static void AssignLayerRecursive(Transform root, int layer)
        {
            if (root == null)
                return;

            root.gameObject.layer = layer;
            for (int i = 0; i < root.childCount; i++)
                AssignLayerRecursive(root.GetChild(i), layer);
        }

        private static bool ValidateCollisionPrimitiveTree(GameObject root, int layer, out int primitiveCount, out string failure)
        {
            primitiveCount = 0;
            failure = string.Empty;
            if (root == null)
            {
                failure = "collision proxy root is null.";
                return false;
            }

            root.GetComponentsInChildren(true, s_meshColliderScratch);
            if (s_meshColliderScratch.Count > 0)
            {
                s_meshColliderScratch.Clear();
                failure = "collision proxy contains MeshCollider.";
                return false;
            }
            s_meshColliderScratch.Clear();

            root.GetComponentsInChildren(true, s_particleScratch);
            if (s_particleScratch.Count > 0)
            {
                s_particleScratch.Clear();
                failure = "collision proxy contains ParticleSystem.";
                return false;
            }
            s_particleScratch.Clear();

            root.GetComponentsInChildren(true, s_colliderScratch);
            for (int i = 0; i < s_colliderScratch.Count; i++)
            {
                Collider collider = s_colliderScratch[i];
                if (collider is BoxCollider || collider is SphereCollider)
                {
                    primitiveCount++;
                    collider.gameObject.layer = layer;
                    continue;
                }

                failure = "collision proxy contains non Box/Sphere collider: " + collider.GetType().Name;
                s_colliderScratch.Clear();
                return false;
            }

            s_colliderScratch.Clear();
            if (primitiveCount <= 0)
            {
                failure = "collision proxy contains no primitive colliders.";
                return false;
            }

            return true;
        }

        private static float ResolveDroneMassKg(Bounds visualBounds)
        {
            Bounds bounds = SanitizeBounds(visualBounds);
            Vector3 size = bounds.size;
            float volume = Mathf.Max(0.001f, size.x * size.y * size.z);
            return Mathf.Clamp(volume * 18f, MinimumDroneMassKg, MaximumDroneMassKg);
        }

        private static void ConfigureMetadata(
            GameObject root,
            Transform rigRoot,
            Transform[] bones,
            DroneBoneJointDescriptor[] descriptors,
            Transform[] anchors,
            DroneAttachmentAnchorDescriptor[] attachmentDescriptors,
            Renderer[] emissionRenderers,
            SourceGroup group,
            FactorySettings settings)
        {
            DroneBoneMetadata metadata = root.AddComponent<DroneBoneMetadata>();
            metadata.ConfigureEditorBake(
                HashString(group.Name),
                HashString(group.SourcePath + "|" + group.Segments.Count.ToString(CultureInfo.InvariantCulture)),
                settings.AuthoredQualityWeight,
                rigRoot,
                bones,
                descriptors);

            DroneAttachmentMetadata attachmentMetadata = root.AddComponent<DroneAttachmentMetadata>();
            attachmentMetadata.ConfigureEditorBake(
                HashString(group.Name),
                HashString(group.SourcePath + "|attachments|" + group.Segments.Count.ToString(CultureInfo.InvariantCulture)),
                settings.AuthoredQualityWeight,
                rigRoot,
                anchors,
                attachmentDescriptors,
                emissionRenderers,
                new Color(0.05f, 0.75f, 1f, 1f),
                new Color(1f, 0.55f, 0.08f, 1f),
                new Color(1f, 0.05f, 0.02f, 1f));
        }

        private static bool ValidatePrefabInstance(GameObject root, out string failure)
        {
            failure = string.Empty;
            if (root == null)
            {
                failure = "root is null.";
                return false;
            }

            if (root.GetComponent<MeshFilter>() != null || root.GetComponent<MeshRenderer>() != null)
            {
                failure = "root carries visual renderer components.";
                return false;
            }

            DroneBoneMetadata metadata = root.GetComponent<DroneBoneMetadata>();
            if (metadata == null)
            {
                failure = "DroneBoneMetadata missing.";
                return false;
            }

            Transform[] boneRefs = new Transform[metadata.BoneCount]; // COLD ALLOC: Transform[metadata.BoneCount] - editor prefab validation table - owner: DronePrefabFactory
            for (int i = 0; i < boneRefs.Length; i++)
            {
                if (!metadata.TryGetBoneTransform(i, out boneRefs[i]))
                {
                    failure = "DroneBoneMetadata bone ref missing.";
                    return false;
                }
            }

            DroneBoneJointDescriptor[] descriptors = new DroneBoneJointDescriptor[metadata.JointCount]; // COLD ALLOC: DroneBoneJointDescriptor[metadata.JointCount] - editor prefab validation table - owner: DronePrefabFactory
            for (int i = 0; i < descriptors.Length; i++)
            {
                if (!metadata.TryGetJoint(i, out descriptors[i]))
                {
                    failure = "DroneBoneMetadata joint descriptor missing.";
                    return false;
                }
            }

            if (!DroneBoneMetadata.ValidateDescriptorSet(boneRefs, descriptors, out failure))
                return false;

            DroneAttachmentMetadata attachmentMetadata = root.GetComponent<DroneAttachmentMetadata>();
            if (attachmentMetadata == null)
            {
                failure = "DroneAttachmentMetadata missing.";
                return false;
            }

            Transform[] anchorRefs = new Transform[attachmentMetadata.AnchorCount]; // COLD ALLOC: Transform[attachmentCount] - editor prefab validation table - owner: DronePrefabFactory
            for (int i = 0; i < anchorRefs.Length; i++)
            {
                if (!attachmentMetadata.TryGetAnchorTransform(i, out anchorRefs[i]))
                {
                    failure = "DroneAttachmentMetadata anchor ref missing.";
                    return false;
                }
            }

            DroneAttachmentAnchorDescriptor[] attachmentDescriptors = new DroneAttachmentAnchorDescriptor[attachmentMetadata.DescriptorCount]; // COLD ALLOC: DroneAttachmentAnchorDescriptor[descriptorCount] - editor prefab validation table - owner: DronePrefabFactory
            for (int i = 0; i < attachmentDescriptors.Length; i++)
            {
                if (!attachmentMetadata.TryGetDescriptor(i, out attachmentDescriptors[i]))
                {
                    failure = "DroneAttachmentMetadata descriptor missing.";
                    return false;
                }
            }

            Renderer[] emissionRenderers = new Renderer[attachmentMetadata.EmissionRendererCount]; // COLD ALLOC: Renderer[emissionCount] - editor prefab validation table - owner: DronePrefabFactory
            for (int i = 0; i < emissionRenderers.Length; i++)
            {
                if (!attachmentMetadata.TryGetEmissionRenderer(i, out emissionRenderers[i]))
                {
                    failure = "DroneAttachmentMetadata emission renderer missing.";
                    return false;
                }
            }

            if (!DroneAttachmentMetadata.ValidateDescriptorSet(anchorRefs, attachmentDescriptors, emissionRenderers, out failure))
                return false;

            root.GetComponentsInChildren(true, s_meshColliderScratch);
            if (s_meshColliderScratch.Count > 0)
            {
                s_meshColliderScratch.Clear();
                failure = "MeshCollider is forbidden in drone chassis prefab.";
                return false;
            }
            s_meshColliderScratch.Clear();

            root.GetComponentsInChildren(true, s_particleScratch);
            if (s_particleScratch.Count > 0)
            {
                s_particleScratch.Clear();
                failure = "ParticleSystem is forbidden in drone prefab.";
                return false;
            }
            s_particleScratch.Clear();

            root.GetComponentsInChildren(true, s_colliderScratch);
            int primitiveCount = 0;
            for (int i = 0; i < s_colliderScratch.Count; i++)
            {
                Collider collider = s_colliderScratch[i];
                if (collider is BoxCollider || collider is SphereCollider)
                {
                    if (collider.gameObject.layer != ResolveDroneLayer())
                    {
                        failure = collider.name + " collider is not on drone collision layer.";
                        s_colliderScratch.Clear();
                        return false;
                    }

                    primitiveCount++;
                }
                else
                {
                    failure = "Non Box/Sphere collider found: " + collider.GetType().Name;
                    s_colliderScratch.Clear();
                    return false;
                }
            }
            s_colliderScratch.Clear();
            if (primitiveCount <= 0)
            {
                failure = "No primitive chassis collider.";
                return false;
            }

            root.GetComponentsInChildren(true, s_rigidbodyScratch);
            if (s_rigidbodyScratch.Count != 1)
            {
                failure = "Drone prefab must contain exactly one Rigidbody on the collision proxy root.";
                s_rigidbodyScratch.Clear();
                return false;
            }

            Rigidbody body = s_rigidbodyScratch[0];
            if (body == null || !body.isKinematic || body.useGravity || body.mass < MinimumDroneMassKg || body.mass > MaximumDroneMassKg)
            {
                failure = "Drone Rigidbody is not configured as bounded kinematic collision authority.";
                s_rigidbodyScratch.Clear();
                return false;
            }
            s_rigidbodyScratch.Clear();

            root.GetComponentsInChildren(true, s_rendererScratch);
            for (int rendererIndex = 0; rendererIndex < s_rendererScratch.Count; rendererIndex++)
            {
                Renderer renderer = s_rendererScratch[rendererIndex];
                if (renderer == null)
                    continue;

                Material[] materials = renderer.sharedMaterials;
                for (int materialIndex = 0; materialIndex < materials.Length; materialIndex++)
                {
                    if (!ValidateSharedMaterial(materials[materialIndex], out failure))
                    {
                        failure = renderer.name + " material slot " + materialIndex.ToString(CultureInfo.InvariantCulture) + ": " + failure;
                        s_rendererScratch.Clear();
                        return false;
                    }
                }
            }
            s_rendererScratch.Clear();

            return true;
        }

        private static bool ValidateSavedPrefab(string prefabPath, out string failure)
        {
            failure = string.Empty;
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
            {
                failure = "saved prefab cannot be loaded.";
                return false;
            }

            return ValidatePrefabInstance(prefab, out failure);
        }

        private static Bounds ComputeGroupBounds(SourceGroup group)
        {
            Bounds bounds = new Bounds(Vector3.zero, Vector3.zero);
            bool hasBounds = false;
            for (int i = 0; i < group.Segments.Count; i++)
            {
                VisualSegment segment = group.Segments[i];
                if (segment.Mesh == null)
                    continue;

                Bounds local = TransformBounds(segment.Mesh.bounds, segment.LocalMatrix);
                if (!hasBounds)
                {
                    bounds = local;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(local);
                }
            }

            return hasBounds ? SanitizeBounds(bounds) : new Bounds(Vector3.zero, Vector3.one * 0.4f);
        }

        private static Bounds TransformBounds(Bounds bounds, Matrix4x4 matrix)
        {
            Vector3 center = matrix.MultiplyPoint3x4(bounds.center);
            Vector3 extents = bounds.extents;
            Vector3 axisX = matrix.MultiplyVector(new Vector3(extents.x, 0f, 0f));
            Vector3 axisY = matrix.MultiplyVector(new Vector3(0f, extents.y, 0f));
            Vector3 axisZ = matrix.MultiplyVector(new Vector3(0f, 0f, extents.z));
            extents.x = Mathf.Abs(axisX.x) + Mathf.Abs(axisY.x) + Mathf.Abs(axisZ.x);
            extents.y = Mathf.Abs(axisX.y) + Mathf.Abs(axisY.y) + Mathf.Abs(axisZ.y);
            extents.z = Mathf.Abs(axisX.z) + Mathf.Abs(axisY.z) + Mathf.Abs(axisZ.z);
            return new Bounds(center, extents * 2f);
        }

        private static bool TryLoadAuthoringBones(string groupName, string metadataDirectory, List<BoneBuildData> output)
        {
            output.Clear();
            if (string.IsNullOrWhiteSpace(metadataDirectory) || !AssetDatabase.IsValidFolder(metadataDirectory))
                return false;

            TextAsset metadata = FindBestMetadata(groupName, metadataDirectory);
            if (metadata == null || string.IsNullOrWhiteSpace(metadata.text))
                return false;

            try
            {
                DroneAuthoringFile file = JsonUtility.FromJson<DroneAuthoringFile>(metadata.text);
                if (file == null || file.bones == null || file.bones.Length == 0)
                    return false;

                for (int i = 0; i < file.bones.Length; i++)
                {
                    DroneBoneAuthoringRecord record = file.bones[i];
                    string name = string.IsNullOrWhiteSpace(record.name)
                        ? "BONE_" + i.ToString(CultureInfo.InvariantCulture)
                        : NormalizeBoneName(record.name);
                    output.Add(new BoneBuildData
                    {
                        Name = name,
                        ParentName = NormalizeBoneName(record.parent),
                        LocalPosition = SanitizeVector(record.localPosition, Vector3.zero),
                        LocalRotation = SanitizeQuaternion(record.localRotation),
                        LocalAxis = SanitizeDirection(record.localAxis, Vector3.up),
                        LimitPlaneNormal = SanitizeDirection(record.limitPlaneNormal, Vector3.forward),
                        MinAngleDegrees = SanitizeFinite(record.minAngleDegrees, -45f),
                        MaxAngleDegrees = SanitizeFinite(record.maxAngleDegrees, 45f),
                        Flags = (DroneBoneSolverFlags)record.solverFlags,
                        TierMask = record.tierMask == 0 ? DroneBoneTierMask.All : (DroneBoneTierMask)record.tierMask,
                        Stiffness = SanitizeFinite(record.stiffness, 1f),
                        Damping = SanitizeFinite(record.damping, 0.25f),
                        SolverWeight = Mathf.Clamp01(SanitizeFinite(record.solverWeight, 1f)),
                        VisualOverkillOffset = SanitizeVector(record.visualOverkillOffset, Vector3.zero)
                    });
                }

                if (!NormalizeBoneBuildOrder(output))
                {
                    output.Clear();
                    return false;
                }

                return output.Count > 0;
            }
            catch (Exception exception)
            {
                Debug.LogError("[DronePrefabFactory1738] Metadata parse failed for " + groupName + ": " + exception.GetType().Name + " " + exception.Message);
                output.Clear();
                return false;
            }
        }

        private static bool NormalizeBoneBuildOrder(List<BoneBuildData> bones)
        {
            if (bones == null || bones.Count == 0)
                return false;

            for (int targetIndex = 0; targetIndex < bones.Count; targetIndex++)
            {
                int selectedIndex = -1;
                for (int candidateIndex = targetIndex; candidateIndex < bones.Count; candidateIndex++)
                {
                    string parentName = bones[candidateIndex].ParentName;
                    if (string.IsNullOrEmpty(parentName))
                    {
                        selectedIndex = candidateIndex;
                        break;
                    }

                    int parentIndex = FindBoneIndex(bones, parentName);
                    if (parentIndex < 0)
                        return false;

                    if (parentIndex < targetIndex)
                    {
                        selectedIndex = candidateIndex;
                        break;
                    }
                }

                if (selectedIndex < 0)
                    return false;

                if (selectedIndex == targetIndex)
                    continue;

                BoneBuildData swap = bones[targetIndex];
                bones[targetIndex] = bones[selectedIndex];
                bones[selectedIndex] = swap;
            }

            return true;
        }

        private static TextAsset FindBestMetadata(string groupName, string metadataDirectory)
        {
            string[] guids = AssetDatabase.FindAssets("t:TextAsset", new[] { metadataDirectory });
            TextAsset best = null;
            int bestScore = int.MinValue;
            string needle = NormalizeSearch(groupName);
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                string name = NormalizeSearch(Path.GetFileNameWithoutExtension(path));
                int score = 0;
                if (name.Contains(needle))
                    score += 100;
                if (name.Contains("drone"))
                    score += 10;
                if (name.Contains("bone") || name.Contains("rig"))
                    score += 10;
                if (name.Contains("socket") || name.Contains("attachment") || name.Contains("thruster"))
                    score += 10;

                if (score > bestScore)
                {
                    bestScore = score;
                    best = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
                }
            }

            return bestScore > 0 ? best : null;
        }

        private static void LoadMaterialDatabase(string materialDirectory, FactoryReport report)
        {
            s_materialScratch.Clear();
            s_assetPathScratch.Clear();
            string[] roots = ResolveMaterialRoots(materialDirectory);
            for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
            {
                string root = roots[rootIndex];
                if (string.IsNullOrWhiteSpace(root) || !AssetDatabase.IsValidFolder(root))
                    continue;

                string[] guids = AssetDatabase.FindAssets("t:Material", new[] { root });
                for (int i = 0; i < guids.Length; i++)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                    if (s_assetPathScratch.Contains(path))
                        continue;

                    Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
                    if (material == null)
                        continue;

                    s_assetPathScratch.Add(path);
                    s_materialScratch.Add(material);
                }
            }

            if (s_materialScratch.Count == 0)
                AddViolation(report, "No drone materials found under configured material roots.");
        }

        private static string[] ResolveMaterialRoots(string materialDirectory)
        {
            if (!string.IsNullOrWhiteSpace(materialDirectory))
                return new[] { materialDirectory, "Assets/_Project/Art/Materials", "Assets/_Project/Art/Materials/Construction" };

            return new[] { DefaultMaterialDirectory, "Assets/_Project/Art/Materials", "Assets/_Project/Art/Materials/Construction" };
        }

        private static Material ResolveBestMaterial(string groupName)
        {
            Material best = null;
            int bestScore = int.MinValue;
            string groupNeedle = NormalizeSearch(groupName);
            for (int i = 0; i < s_materialScratch.Count; i++)
            {
                Material material = s_materialScratch[i];
                if (material == null)
                    continue;

                string name = NormalizeSearch(material.name);
                int score = 0;
                if (name.Contains(groupNeedle))
                    score += 100;
                if (name.Contains("drone") || name.Contains("proxy") || name.Contains("automation"))
                    score += 40;
                if (name.Contains("mat"))
                    score += 2;
                if (score > bestScore && ValidateSharedMaterial(material, out _))
                {
                    bestScore = score;
                    best = material;
                }
            }

            return best;
        }

        private static Material ResolveSegmentMaterial(VisualSegment segment, int subMeshIndex, Material fallback)
        {
            Material[] materials = segment.Materials;
            if (materials != null &&
                subMeshIndex >= 0 &&
                subMeshIndex < materials.Length &&
                materials[subMeshIndex] != null &&
                ValidateSharedMaterial(materials[subMeshIndex], out _))
            {
                return materials[subMeshIndex];
            }

            return fallback;
        }

        private static bool ValidateSharedMaterial(Material material, out string failure)
        {
            failure = string.Empty;
            if (material == null)
            {
                failure = "material is null.";
                return false;
            }

            if (!AssetDatabase.Contains(material))
            {
                failure = material.name + " is not asset-backed.";
                return false;
            }

            if (material.shader == null)
            {
                failure = material.name + " has no shader.";
                return false;
            }

            if (!ShaderHasUnityPerMaterialCbuffer(material.shader))
            {
                failure = material.name + " shader lacks UnityPerMaterial CBUFFER proof.";
                return false;
            }

            if (!material.HasProperty(EmissionColorPropertyName))
            {
                failure = material.name + " lacks _EmissionColor for MPB/buffer driven drone state.";
                return false;
            }

            return true;
        }

        private static bool ShaderHasUnityPerMaterialCbuffer(Shader shader)
        {
            if (shader == null)
                return false;

            string shaderName = shader.name;
            if (string.IsNullOrEmpty(shaderName))
                return false;

            if (s_shaderCbufferCache.TryGetValue(shaderName, out bool cached))
                return cached;

            bool result = false;
            string path = AssetDatabase.GetAssetPath(shader);
            if (!string.IsNullOrEmpty(path) && File.Exists(ResolveProjectPath(path)))
            {
                string source = File.ReadAllText(ResolveProjectPath(path));
                result = source.Contains("CBUFFER_START(UnityPerMaterial)") ||
                         source.Contains("UnityPerMaterial");
            }
            else
            {
                result = shaderName.IndexOf("Universal Render Pipeline", StringComparison.Ordinal) >= 0 ||
                         shaderName.IndexOf("Shader Graph", StringComparison.Ordinal) >= 0 ||
                         shaderName.IndexOf("Hecton", StringComparison.Ordinal) >= 0;
            }

            s_shaderCbufferCache[shaderName] = result;
            return result;
        }

        private static int ResolveDroneLayer()
        {
            int layer = LayerMask.NameToLayer(DynamicWorldLayerName);
            return layer >= 0 ? layer : 0;
        }

        private static Transform CreateChildTransform(Transform parent, string name, Vector3 localPosition, Quaternion localRotation)
        {
            GameObject child = new GameObject(name);
            child.transform.SetParent(parent, false);
            child.transform.localPosition = localPosition;
            child.transform.localRotation = localRotation;
            child.transform.localScale = Vector3.one;
            return child.transform;
        }

        private static int FindBoneIndex(List<BoneBuildData> bones, string boneName)
        {
            if (string.IsNullOrEmpty(boneName))
                return -1;

            for (int i = 0; i < bones.Count; i++)
            {
                if (string.Equals(bones[i].Name, boneName, StringComparison.Ordinal))
                    return i;
            }

            return -1;
        }

        private static string ResolveBoneName(string sourceName)
        {
            string normalized = NormalizeSearch(sourceName);
            if (normalized.Contains("arm_l") || normalized.Contains("leftarm") || normalized.Contains("left_arm") || normalized.Contains("service_l"))
                return "BONE_ServiceArm_L";
            if (normalized.Contains("arm_r") || normalized.Contains("rightarm") || normalized.Contains("right_arm") || normalized.Contains("service_r"))
                return "BONE_ServiceArm_R";
            if (normalized.Contains("tool") || normalized.Contains("welder") || normalized.Contains("torch") || normalized.Contains("probe"))
                return "BONE_ToolMount";
            if (normalized.Contains("sensor") || normalized.Contains("camera") || normalized.Contains("antenna") || normalized.Contains("mast"))
                return "BONE_SensorMast";
            if (normalized.Contains("thruster") || normalized.Contains("prop") || normalized.Contains("fan") || normalized.Contains("ring"))
                return "BONE_ThrusterRing";

            return "BONE_Chassis";
        }

        private static string ResolveGroupName(string sourceName)
        {
            string name = SanitizeAssetName(sourceName);
            string[] prefixes = { "PFB_", "DRN_", "Drone_", "MESH_", "Mesh_", "SM_" };
            for (int i = 0; i < prefixes.Length; i++)
            {
                if (name.StartsWith(prefixes[i], StringComparison.OrdinalIgnoreCase))
                    name = name.Substring(prefixes[i].Length);
            }

            string[] splitTokens = { "_BONE_", "_Bone_", "_LOD", "_lod", "_VIS_", "_COL_" };
            for (int i = 0; i < splitTokens.Length; i++)
            {
                int index = name.IndexOf(splitTokens[i], StringComparison.Ordinal);
                if (index > 0)
                    name = name.Substring(0, index);
            }

            if (string.IsNullOrWhiteSpace(name))
                name = "RepairDrone";

            return SanitizeAssetName(name);
        }

        private static string NormalizeBoneName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            string name = SanitizeAssetName(value);
            return name.StartsWith("BONE_", StringComparison.Ordinal) ? name : "BONE_" + name;
        }

        private static string SanitizeAssetName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "Unnamed";

            char[] buffer = value.ToCharArray();
            for (int i = 0; i < buffer.Length; i++)
            {
                char c = buffer[i];
                if (!char.IsLetterOrDigit(c) && c != '_' && c != '-')
                    buffer[i] = '_';
            }

            return new string(buffer).Trim('_');
        }

        private static string NormalizeSearch(string value)
        {
            return string.IsNullOrEmpty(value)
                ? string.Empty
                : value.Replace("-", "_").Replace(" ", "_").ToLowerInvariant();
        }

        private static Bounds SanitizeBounds(Bounds bounds)
        {
            Vector3 center = SanitizeVector(bounds.center, Vector3.zero);
            Vector3 extents = SanitizeExtents(bounds.extents);
            return new Bounds(center, extents * 2f);
        }

        private static Vector3 SanitizeExtents(Vector3 extents)
        {
            extents = SanitizeVector(extents, Vector3.one * 0.2f);
            extents.x = Mathf.Max(MinimumBoundsExtent, Mathf.Abs(extents.x));
            extents.y = Mathf.Max(MinimumBoundsExtent, Mathf.Abs(extents.y));
            extents.z = Mathf.Max(MinimumBoundsExtent, Mathf.Abs(extents.z));
            return extents;
        }

        private static Vector3 SanitizeVector(Vector3 value, Vector3 fallback)
        {
            return IsFinite(value) ? value : fallback;
        }

        private static Vector3 SanitizeDirection(Vector3 value, Vector3 fallback)
        {
            if (!IsFinite(value) || value.sqrMagnitude <= 0.000001f)
                return fallback;

            return value.normalized;
        }

        private static Quaternion SanitizeQuaternion(Quaternion value)
        {
            if (!IsFinite(value))
                return Quaternion.identity;

            return Quaternion.Normalize(value);
        }

        private static float SanitizeFinite(float value, float fallback)
        {
            return IsFinite(value) ? value : fallback;
        }

        private static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        }

        private static bool IsFinite(Quaternion value)
        {
            return IsFinite(value.x) &&
                   IsFinite(value.y) &&
                   IsFinite(value.z) &&
                   IsFinite(value.w) &&
                   (value.x * value.x + value.y * value.y + value.z * value.z + value.w * value.w) > 0.000001f;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static string ResolvePrefabPath(string outputDirectory, string groupName)
        {
            return NormalizeAssetPath(outputDirectory) + "/PFB_" + SanitizeAssetName(groupName) + ".prefab";
        }

        private static string ResolveCombinedMeshPath(string outputDirectory, string groupName, string boneName)
        {
            return NormalizeAssetPath(outputDirectory) + "/MESH_" + SanitizeAssetName(groupName) + "_" + SanitizeAssetName(boneName) + ".asset";
        }

        private static string NormalizeAssetPath(string assetPath)
        {
            if (string.IsNullOrWhiteSpace(assetPath))
                return "Assets";

            return assetPath.Replace('\\', '/').TrimEnd('/');
        }

        private static void DeletePrefabAsset(string prefabPath)
        {
            if (!string.IsNullOrEmpty(prefabPath) && AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) != null)
                AssetDatabase.DeleteAsset(prefabPath);
        }

        private static void EnsureAssetFolder(string assetFolder)
        {
            if (string.IsNullOrWhiteSpace(assetFolder) || AssetDatabase.IsValidFolder(assetFolder))
                return;

            Directory.CreateDirectory(ResolveProjectPath(assetFolder));
            AssetDatabase.Refresh();
        }

        private static string ResolveProjectPath(string assetOrProjectPath)
        {
            string normalized = assetOrProjectPath.Replace('\\', '/');
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            if (Path.IsPathRooted(normalized))
                return normalized;

            return Path.Combine(projectRoot, normalized);
        }

        private static void FailGroup(GroupReport groupReport, FactoryReport report, string failure)
        {
            groupReport.Failure = failure;
            groupReport.Status = "FAIL";
            AddViolation(report, groupReport.DroneName + ": " + failure);
        }

        private static void AddViolation(FactoryReport report, string violation)
        {
            report.Violations.Add(violation);
            Debug.LogError("[DronePrefabFactory1738] " + violation);
        }

        private static void ClearScratch()
        {
            s_meshFilterScratch.Clear();
            s_meshRendererScratch.Clear();
            s_skinnedRendererScratch.Clear();
            s_rendererScratch.Clear();
            s_colliderScratch.Clear();
            s_rigidbodyScratch.Clear();
            s_meshColliderScratch.Clear();
            s_particleScratch.Clear();
            s_transformScratch.Clear();
            s_materialScratch.Clear();
            s_assetPathScratch.Clear();
            s_segmentScratch.Clear();
            s_boneScratch.Clear();
            s_attachmentScratch.Clear();
            s_groupMap.Clear();
            ClearCombineScratch();
        }

        private static void ClearPerGroupScratch()
        {
            s_segmentScratch.Clear();
            s_boneScratch.Clear();
            s_attachmentScratch.Clear();
            ClearCombineScratch();
            for (int i = 0; i < s_dryRunCombinedMeshes.Count; i++)
            {
                if (s_dryRunCombinedMeshes[i] != null)
                    DestroyImmediate(s_dryRunCombinedMeshes[i]);
            }

            s_dryRunCombinedMeshes.Clear();
        }

        private static void ClearCombineScratch()
        {
            for (int i = 0; i < s_combineBuckets.Count; i++)
                s_combineBuckets[i].Instances.Clear();

            s_combineBuckets.Clear();
            for (int i = 0; i < s_tempMeshes.Count; i++)
            {
                if (s_tempMeshes[i] != null)
                    DestroyImmediate(s_tempMeshes[i]);
            }

            s_tempMeshes.Clear();
        }

        private static uint HashString(string value)
        {
            unchecked
            {
                uint hash = 2166136261u;
                if (string.IsNullOrEmpty(value))
                    return hash;

                for (int i = 0; i < value.Length; i++)
                {
                    hash ^= value[i];
                    hash *= 16777619u;
                }

                return hash == 0u ? 2166136261u : hash;
            }
        }

        private static long TicksToMicroseconds(long ticks)
        {
            return ticks <= 0 ? 0 : ticks * 1000000L / Stopwatch.Frequency;
        }

        [Serializable]
        public struct FactorySettings
        {
            public string SourcePrefabDirectory;
            public string MeshDirectory;
            public string MaterialDirectory;
            public string MetadataDirectory;
            public string PhysicsProxyDirectory;
            public string GeneratedMeshDirectory;
            public string OutputDirectory;
            public float AuthoredQualityWeight;
            public bool DryRun;
            public int MaxGroupsPerRun;

            public static FactorySettings Default => new FactorySettings
            {
                SourcePrefabDirectory = DefaultSourcePrefabDirectory,
                MeshDirectory = DefaultMeshDirectory,
                MaterialDirectory = DefaultMaterialDirectory,
                MetadataDirectory = DefaultMetadataDirectory,
                PhysicsProxyDirectory = DefaultPhysicsProxyDirectory,
                GeneratedMeshDirectory = DefaultGeneratedMeshDirectory,
                OutputDirectory = DefaultOutputDirectory,
                AuthoredQualityWeight = DefaultAuthoredQualityWeight,
                DryRun = true,
                MaxGroupsPerRun = MaxGroupsPerRunDefault
            };

            public FactorySettings Sanitize()
            {
                SourcePrefabDirectory = string.IsNullOrWhiteSpace(SourcePrefabDirectory) ? DefaultSourcePrefabDirectory : NormalizeAssetPath(SourcePrefabDirectory);
                MeshDirectory = string.IsNullOrWhiteSpace(MeshDirectory) ? DefaultMeshDirectory : NormalizeAssetPath(MeshDirectory);
                MaterialDirectory = string.IsNullOrWhiteSpace(MaterialDirectory) ? DefaultMaterialDirectory : NormalizeAssetPath(MaterialDirectory);
                MetadataDirectory = string.IsNullOrWhiteSpace(MetadataDirectory) ? DefaultMetadataDirectory : NormalizeAssetPath(MetadataDirectory);
                PhysicsProxyDirectory = string.IsNullOrWhiteSpace(PhysicsProxyDirectory) ? DefaultPhysicsProxyDirectory : NormalizeAssetPath(PhysicsProxyDirectory);
                GeneratedMeshDirectory = string.IsNullOrWhiteSpace(GeneratedMeshDirectory) ? DefaultGeneratedMeshDirectory : NormalizeAssetPath(GeneratedMeshDirectory);
                OutputDirectory = string.IsNullOrWhiteSpace(OutputDirectory) ? DefaultOutputDirectory : NormalizeAssetPath(OutputDirectory);
                AuthoredQualityWeight = Mathf.Clamp01(IsFinite(AuthoredQualityWeight) ? AuthoredQualityWeight : DefaultAuthoredQualityWeight);
                MaxGroupsPerRun = Mathf.Max(1, MaxGroupsPerRun <= 0 ? MaxGroupsPerRunDefault : MaxGroupsPerRun);
                return this;
            }
        }

        [Serializable]
        public sealed class FactoryReport
        {
            public string AgentId;
            public string SourcePrefabDirectory;
            public string MeshDirectory;
            public string MaterialDirectory;
            public string MetadataDirectory;
            public string PhysicsProxyDirectory;
            public string GeneratedMeshDirectory;
            public string OutputDirectory;
            public float AuthoredQualityWeight;
            public bool DryRun;
            public int GroupsDiscovered;
            public int PrefabsAssembled;
            public int PrefabsDryRunPassed;
            public int PrefabsFailed;
            public int CombinedMeshesCreated;
            public int PrimitiveCollidersCreated;
            public int JointsSerialized;
            public int AttachmentsSerialized;
            public long ExecutionMicroseconds;
            public List<GroupReport> GroupReports = new List<GroupReport>(128); // COLD ALLOC: List<GroupReport>[128] - editor run report rows - owner: DronePrefabFactory
            public List<string> Violations = new List<string>(128); // COLD ALLOC: List<string>[128] - editor run violation messages - owner: DronePrefabFactory
        }

        [Serializable]
        public sealed class GroupReport
        {
            public string DroneName;
            public string SourcePath;
            public string OutputPath;
            public int SourceSegmentCount;
            public int BoneCount;
            public int JointCount;
            public int AttachmentCount;
            public int CombinedMeshCount;
            public int PrimitiveColliderCount;
            public int ProxyColliderCount;
            public long EditorMicroseconds;
            public string Status;
            public string Failure;
        }

        private sealed class SourceGroup
        {
            public string Name;
            public string SourcePath;
            public readonly List<VisualSegment> Segments = new List<VisualSegment>(32); // COLD ALLOC: List<VisualSegment>[32] - editor grouped source segments - owner: SourceGroup
        }

        private sealed class VisualSegment
        {
            public Mesh Mesh;
            public Material[] Materials;
            public Matrix4x4 LocalMatrix;
            public string BoneName;
            public string SourceName;
            public string SourcePath;
        }

        private sealed class CombineBucket
        {
            public Material Material;
            public readonly List<CombineInstance> Instances = new List<CombineInstance>(64); // COLD ALLOC: List<CombineInstance>[64] - editor mesh combine instances by material - owner: CombineBucket
        }

        private struct BoneBuildData
        {
            public string Name;
            public string ParentName;
            public Vector3 LocalPosition;
            public Quaternion LocalRotation;
            public Vector3 LocalAxis;
            public Vector3 LimitPlaneNormal;
            public float MinAngleDegrees;
            public float MaxAngleDegrees;
            public DroneBoneSolverFlags Flags;
            public DroneBoneTierMask TierMask;
            public float Stiffness;
            public float Damping;
            public float SolverWeight;
            public Vector3 VisualOverkillOffset;
        }

        private struct AttachmentBuildData
        {
            public string Name;
            public string BoneName;
            public DroneAttachmentKind Kind;
            public Vector3 LocalPosition;
            public Vector3 LocalForward;
            public Vector3 LocalUp;
            public DroneBoneTierMask TierMask;
            public DroneAttachmentFlags Flags;
            public float MinQualityWeight;
        }

        [Serializable]
        private sealed class DroneAuthoringFile
        {
            public string droneName;
            public DroneBoneAuthoringRecord[] bones;
            public DroneAttachmentAuthoringRecord[] attachments;
            public DroneAttachmentAuthoringRecord[] sockets;
            public DroneAttachmentAuthoringRecord[] thrusters;
        }

        [Serializable]
        private struct DroneBoneAuthoringRecord
        {
            public string name;
            public string parent;
            public Vector3 localPosition;
            public Quaternion localRotation;
            public Vector3 localAxis;
            public Vector3 limitPlaneNormal;
            public float minAngleDegrees;
            public float maxAngleDegrees;
            public byte solverFlags;
            public byte tierMask;
            public float stiffness;
            public float damping;
            public float solverWeight;
            public Vector3 visualOverkillOffset;
        }

        [Serializable]
        private struct DroneAttachmentAuthoringRecord
        {
            public string name;
            public string bone;
            public string kind;
            public Vector3 localPosition;
            public Vector3 localForward;
            public Vector3 forward;
            public Vector3 normal;
            public Vector3 localUp;
            public Vector3 up;
            public byte tierMask;
            public byte flags;
            public float minQualityWeight;
        }
    }
}
#endif
