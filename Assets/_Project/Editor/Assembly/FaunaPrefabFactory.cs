#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using Hecton8.AI;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Debug = UnityEngine.Debug;

namespace Hecton8.Editor.Assembly
{
    public sealed class FaunaPrefabFactory : EditorWindow
    {
        private const string AgentId = "1733";
        private const string DefaultMeshDirectory = "Assets/_Project/Art/Generated/Fauna";
        private const string DefaultMaterialDirectory = "Assets/_Project/Art/Materials";
        private const string DefaultOutputDirectory = "Assets/_Project/Prefabs/Creatures";
        private const string GeneratedMaterialFolderName = "Materials";
        private const string FaunaHitboxLayerName = "Fauna_Hitbox";
        private const int MaxGpuSkinBones = 96;
        private const int MaxPhysicsCullingColliders = FaunaMetadata.MaxPhysicsCullingColliderCount;
        private const int MaxFineHitboxes = FaunaMetadata.MaxFineHitboxColliderCount;
        private const int MaxBiolumPresentationLights = FaunaMetadata.MaxBiolumPresentationLightCount;
        private const float CapsuleJointOverlapScale = 1.08f;
        private const float MinimumHitboxRadiusMeters = 0.035f;
        private const float DefaultSwarmAggregateRadiusMeters = 0.65f;

        private static readonly string[] VatPositionPropertyNames =
        {
            "_VATPositionTex",
            "_VatPositionTex",
            "_PositionTex",
            "_AnimPositionTex",
            "_VAT_Position"
        };

        private static readonly string[] VatNormalPropertyNames =
        {
            "_VATNormalTex",
            "_VatNormalTex",
            "_NormalTex",
            "_AnimNormalTex",
            "_VAT_Normal"
        };

        private static readonly string[] AssetNamePrefixes =
        {
            "PFB_",
            "GEN_",
            "MESH_",
            "SK_",
            "VAT_",
            "TEX_",
            "MAT_"
        };

        private static readonly string[] AssetNameCuts =
        {
            "_LOD0",
            "_LOD1",
            "_LOD2",
            "_VAT_Position",
            "_VAT_Normal",
            "_VAT_Pos",
            "_VAT_Nrm",
            "_VATPosition",
            "_VATNormal",
            "_Position",
            "_Normal",
            "_Pos",
            "_Nrm"
        };

        private static readonly List<Renderer> s_RendererScratch = new List<Renderer>(64);
        private static readonly List<SkinnedMeshRenderer> s_SkinnedScratch = new List<SkinnedMeshRenderer>(32);
        private static readonly List<Animator> s_AnimatorScratch = new List<Animator>(8);
        private static readonly List<Transform> s_TransformScratch = new List<Transform>(256);
        private static readonly List<Collider> s_ColliderScratch = new List<Collider>(128);
        private static readonly List<Collider> s_CullingColliderScratch = new List<Collider>(8);
        private static readonly List<MeshCollider> s_MeshColliderScratch = new List<MeshCollider>(16);
        private static readonly List<Light> s_LightScratch = new List<Light>(8);
        private static readonly List<Material> s_MaterialScratch = new List<Material>(128);
        private static readonly List<string> s_ViolationScratch = new List<string>(64);
        private static readonly List<Renderer> s_Lod0RendererScratch = new List<Renderer>(16);
        private static readonly List<Renderer> s_Lod1RendererScratch = new List<Renderer>(16);
        private static readonly List<Renderer> s_Lod2RendererScratch = new List<Renderer>(16);
        private static readonly List<LOD> s_LodScratch = new List<LOD>(3);
        private static readonly Dictionary<string, bool> s_ShaderUnityPerMaterialCache =
            new Dictionary<string, bool>(128, StringComparer.Ordinal);

        [SerializeField] private string meshDirectory = DefaultMeshDirectory;
        [SerializeField] private string materialDirectory = DefaultMaterialDirectory;
        [SerializeField] private string outputDirectory = DefaultOutputDirectory;
        [SerializeField] private bool dryRun = true;
        [SerializeField] private bool requireGpuSkinningProjectSetting = true;
        [SerializeField] private bool autoEnableGpuSkinningProjectSetting = true;
        [SerializeField] private bool requireFaunaHitboxLayer = true;

        private Vector2 scroll;
        private FaunaAssemblerReport lastReport;

        [MenuItem("Hecton8/Assembly/Fauna Prefab Factory 1733")]
        public static void OpenWindow()
        {
            GetWindow<FaunaPrefabFactory>("Fauna Factory 1733");
        }

        [MenuItem("Hecton8/Assembly/Dry Run Fauna Prefab Factory 1733")]
        public static void DryRunMenu()
        {
            FaunaPrefabFactory window = CreateInstance<FaunaPrefabFactory>();
            window.dryRun = true;
            window.ExecuteFactory();
            DestroyImmediate(window);
        }

        [MenuItem("Hecton8/Assembly/Run Fauna Prefab Factory 1733")]
        public static void RunMenu()
        {
            FaunaPrefabFactory window = CreateInstance<FaunaPrefabFactory>();
            window.dryRun = false;
            window.ExecuteFactory();
            DestroyImmediate(window);
        }

        private void OnGUI()
        {
            meshDirectory = EditorGUILayout.TextField("Generated fauna", meshDirectory);
            materialDirectory = EditorGUILayout.TextField("Material database", materialDirectory);
            outputDirectory = EditorGUILayout.TextField("Output prefabs", outputDirectory);
            dryRun = EditorGUILayout.Toggle("Dry run", dryRun);
            requireGpuSkinningProjectSetting = EditorGUILayout.Toggle("Require GPU skinning setting", requireGpuSkinningProjectSetting);
            autoEnableGpuSkinningProjectSetting = EditorGUILayout.Toggle("Auto-enable GPU skinning", autoEnableGpuSkinningProjectSetting);
            requireFaunaHitboxLayer = EditorGUILayout.Toggle("Require Fauna_Hitbox layer", requireFaunaHitboxLayer);

            if (GUILayout.Button(dryRun ? "Dry Run" : "Assemble Prefabs"))
                ExecuteFactory();

            if (lastReport == null)
                return;

            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.LabelField("Last Report", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Groups", lastReport.groupsDiscovered.ToString());
            EditorGUILayout.LabelField("Saved", lastReport.prefabsSaved.ToString());
            EditorGUILayout.LabelField("Violations", lastReport.totalViolations.ToString());
            EditorGUILayout.LabelField("Primitive colliders", lastReport.totalPrimitiveColliders.ToString());
            EditorGUILayout.LabelField("Fine colliders", lastReport.totalFineColliders.ToString());
            EditorGUILayout.LabelField("Skipped fine hitboxes", lastReport.totalSkippedFineHitboxCandidates.ToString());
            EditorGUILayout.LabelField("Biolum lights", lastReport.totalBiolumPresentationLights.ToString());
            EditorGUILayout.EndScrollView();
        }

        private void ExecuteFactory()
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            FaunaAssemblerReport report = new FaunaAssemblerReport
            {
                agentId = AgentId,
                generatedUtc = DateTime.UtcNow.ToString("O"),
                dryRun = dryRun,
                meshDirectory = meshDirectory,
                materialDirectory = materialDirectory,
                outputDirectory = outputDirectory
            };

            List<FaunaAssetGroup> groups = DiscoverFaunaAssetGroups(meshDirectory);
            LoadMaterials(materialDirectory, s_MaterialScratch);
            report.groupsDiscovered = groups.Count;
            report.groups = new FaunaAssemblyGroupReport[groups.Count];

            bool hasGpuSkinningSetting = TryReadGpuSkinningProjectSetting(out bool gpuSkinningEnabled);
            if (!dryRun &&
                requireGpuSkinningProjectSetting &&
                autoEnableGpuSkinningProjectSetting &&
                hasGpuSkinningSetting &&
                !gpuSkinningEnabled &&
                ContainsSkinnedFaunaGroup(groups))
            {
                report.gpuSkinningProjectSettingAutoEnabled = TryWriteGpuSkinningProjectSetting(true);
                if (report.gpuSkinningProjectSettingAutoEnabled)
                    gpuSkinningEnabled = TryReadGpuSkinningProjectSetting(out bool refreshedGpuSkinningEnabled) &&
                                         refreshedGpuSkinningEnabled;
            }

            int hitboxLayer = LayerMask.NameToLayer(FaunaHitboxLayerName);

            bool assetDatabaseTouched = false;
            if (!dryRun)
                assetDatabaseTouched |= EnsureAssetFolder(outputDirectory);

            for (int i = 0; i < groups.Count; i++)
            {
                FaunaAssetGroup group = groups[i];
                FaunaAssemblyGroupReport groupReport = AssembleGroup(
                    group,
                    s_MaterialScratch,
                    hitboxLayer,
                    hasGpuSkinningSetting,
                    gpuSkinningEnabled);

                report.groups[i] = groupReport;
                report.totalViolations += groupReport.violationCount;
                report.totalPrimitiveColliders += groupReport.primitiveColliderCount;
                report.totalFineColliders += groupReport.fineColliderCount;
                report.totalSkippedFineHitboxCandidates += groupReport.skippedFineHitboxCandidateCount;
                report.totalBiolumPresentationLights += groupReport.biolumPresentationLightCount;
                report.totalSkinnedRenderers += groupReport.skinnedRendererCount;
                report.totalVatRenderers += groupReport.vatRendererCount;
                if (groupReport.saved)
                    report.prefabsSaved++;
                if (groupReport.isSwarm)
                    report.swarmGroups++;
                assetDatabaseTouched |= groupReport.assetDatabaseTouched;
            }

            stopwatch.Stop();
            report.elapsedMicroseconds = TicksToMicroseconds(stopwatch.ElapsedTicks);
            lastReport = report;
            if (assetDatabaseTouched)
                AssetDatabase.Refresh();
        }

        private FaunaAssemblyGroupReport AssembleGroup(
            FaunaAssetGroup group,
            List<Material> materials,
            int hitboxLayer,
            bool hasGpuSkinningSetting,
            bool gpuSkinningEnabled)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            s_ViolationScratch.Clear();

            FaunaAssemblyGroupReport report = new FaunaAssemblyGroupReport
            {
                groupName = group.name,
                isSwarm = group.IsSwarm,
                inputAssetCount = group.InputAssetCount,
                savedPath = ResolvePrefabPath(outputDirectory, group.name)
            };

            GameObject root = new GameObject("PFB_" + SanitizeAssetName(group.name));
            try
            {
                root.transform.position = Vector3.zero;
                root.transform.rotation = Quaternion.identity;
                root.transform.localScale = Vector3.one;

                Rigidbody rigidbody = root.AddComponent<Rigidbody>();
                rigidbody.useGravity = false;
                rigidbody.interpolation = RigidbodyInterpolation.Interpolate;
                rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

                FaunaMetadata metadata = root.AddComponent<FaunaMetadata>();
                Material material = FindBestMaterial(group.name, group.IsSwarm, materials);
                if (material == null)
                    s_ViolationScratch.Add("No shared fauna material matched group.");
                Material sourceVatMaterial = material;
                string generatedVatMaterialPath = null;
                bool generatedVatMaterialCreated = false;

                if (group.IsSwarm)
                    BuildVatSwarmVisuals(root, group, material, report);
                else
                    BuildSkinnedFaunaVisuals(root, group, material, hasGpuSkinningSetting, gpuSkinningEnabled, report);

                Transform sensoryAnchor = CreateSensoryAnchor(root);
                Collider aggregateCollider = BuildPrimitiveHitboxes(root, group.IsSwarm, hitboxLayer, report);
                if (aggregateCollider == null)
                    s_ViolationScratch.Add("No aggregate primitive hitbox was generated.");

                BuildLodGroup(root, report);
                ConfigureMetadata(root, metadata, sensoryAnchor, aggregateCollider, group, report);
                ValidatePrefab(root, hitboxLayer, hasGpuSkinningSetting, gpuSkinningEnabled, report);

                if (requireFaunaHitboxLayer && hitboxLayer < 0)
                    s_ViolationScratch.Add("Required physics layer Fauna_Hitbox is missing.");
                if (requireGpuSkinningProjectSetting && hasGpuSkinningSetting && !gpuSkinningEnabled && !group.IsSwarm)
                    s_ViolationScratch.Add("PlayerSettings.gpuSkinning is disabled; project setting must be enabled for skinned fauna.");
                if (requireGpuSkinningProjectSetting && !hasGpuSkinningSetting && !group.IsSwarm)
                    s_ViolationScratch.Add("PlayerSettings.gpuSkinning setting was not discoverable by reflection.");

                report.violationCount = s_ViolationScratch.Count;
                report.violations = s_ViolationScratch.ToArray();
                if (report.violationCount == 0 && !dryRun)
                {
                    if (group.IsSwarm)
                    {
                        material = ResolveVatMaterialForGroup(
                            sourceVatMaterial,
                            group.name,
                            out generatedVatMaterialPath,
                            out generatedVatMaterialCreated);
                        if (generatedVatMaterialCreated)
                            report.assetDatabaseTouched = true;

                        if (!PrepareVatMaterialForSave(root, material, sourceVatMaterial, group))
                        {
                            report.violationCount++;
                            report.violations = AppendViolation(report.violations, "VAT material finalization failed.");
                            if (DeleteCreatedVatMaterial(generatedVatMaterialPath, generatedVatMaterialCreated))
                                report.assetDatabaseTouched = true;
                            return report;
                        }

                        if (material != null)
                            report.assetDatabaseTouched = true;
                    }

                    bool success;
                    bool prefabExistedBeforeSave = AssetDatabase.LoadAssetAtPath<GameObject>(report.savedPath) != null;
                    GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, report.savedPath, out success);
                    report.assetDatabaseTouched = true;
                    if (!success || saved == null)
                    {
                        report.saved = false;
                        report.violationCount++;
                        report.violations = AppendViolation(report.violations, "PrefabUtility.SaveAsPrefabAsset returned null or failed.");
                        if (!prefabExistedBeforeSave && AssetDatabase.DeleteAsset(report.savedPath))
                            report.assetDatabaseTouched = true;
                        if (DeleteCreatedVatMaterial(generatedVatMaterialPath, generatedVatMaterialCreated))
                            report.assetDatabaseTouched = true;
                    }
                    else
                    {
                        report.saved = true;
                        EditorUtility.SetDirty(saved);
                    }
                }
                else if (report.violationCount > 0 && !dryRun)
                {
                    if (DeleteCreatedVatMaterial(generatedVatMaterialPath, generatedVatMaterialCreated))
                        report.assetDatabaseTouched = true;
                    Debug.LogError("Fauna Assembly Violation Detected! " + group.name);
                }
            }
            finally
            {
                DestroyImmediate(root);
                stopwatch.Stop();
                report.elapsedMicroseconds = TicksToMicroseconds(stopwatch.ElapsedTicks);
            }

            return report;
        }

        private static List<FaunaAssetGroup> DiscoverFaunaAssetGroups(string directory)
        {
            Dictionary<string, FaunaAssetGroup> groups = new Dictionary<string, FaunaAssetGroup>(64, StringComparer.OrdinalIgnoreCase);
            if (!AssetDatabase.IsValidFolder(directory))
                return new List<FaunaAssetGroup>(0);

            AddMeshAssets(groups, directory);
            AddModelAssets(groups, directory);
            AddVatTextures(groups, directory);
            List<FaunaAssetGroup> result = new List<FaunaAssetGroup>(groups.Count);
            foreach (KeyValuePair<string, FaunaAssetGroup> pair in groups)
                result.Add(pair.Value);

            result.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            return result;
        }

        private static bool ContainsSkinnedFaunaGroup(List<FaunaAssetGroup> groups)
        {
            if (groups == null)
                return false;

            for (int i = 0; i < groups.Count; i++)
            {
                FaunaAssetGroup group = groups[i];
                if (group != null && !group.IsSwarm)
                    return true;
            }

            return false;
        }

        private static void AddMeshAssets(Dictionary<string, FaunaAssetGroup> groups, string directory)
        {
            string[] guids = AssetDatabase.FindAssets("t:Mesh", new[] { directory });
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if (mesh == null)
                    continue;

                string key = NormalizeGroupName(Path.GetFileNameWithoutExtension(path));
                FaunaAssetGroup group = GetOrCreateGroup(groups, key);
                int lod = ResolveLodIndex(path);
                if (lod >= 0 && lod < group.lodMeshes.Length && group.lodMeshes[lod] == null)
                    group.lodMeshes[lod] = mesh;
                if (ContainsIgnoreCase(path, "swarm") || ContainsIgnoreCase(path, "shoal") || ContainsIgnoreCase(path, "vat"))
                    group.forceSwarm = true;
                group.assetPaths.Add(path);
            }
        }

        private static void AddModelAssets(Dictionary<string, FaunaAssetGroup> groups, string directory)
        {
            string[] guids = AssetDatabase.FindAssets("t:GameObject", new[] { directory });
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (model == null)
                    continue;

                string key = NormalizeGroupName(Path.GetFileNameWithoutExtension(path));
                FaunaAssetGroup group = GetOrCreateGroup(groups, key);
                group.modelAssets.Add(model);
                if (ContainsIgnoreCase(path, "swarm") || ContainsIgnoreCase(path, "shoal") || ContainsIgnoreCase(path, "vat"))
                    group.forceSwarm = true;
                group.assetPaths.Add(path);
            }
        }

        private static void AddVatTextures(Dictionary<string, FaunaAssetGroup> groups, string directory)
        {
            string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { directory });
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (!ContainsIgnoreCase(path, "vat"))
                    continue;

                Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (texture == null)
                    continue;

                string key = NormalizeGroupName(Path.GetFileNameWithoutExtension(path));
                FaunaAssetGroup group = GetOrCreateGroup(groups, key);
                if (ContainsIgnoreCase(path, "normal") || ContainsIgnoreCase(path, "_nrm"))
                    group.vatNormalTexture = texture;
                else
                    group.vatPositionTexture = texture;
                group.forceSwarm = true;
                group.assetPaths.Add(path);
            }
        }

        private static FaunaAssetGroup GetOrCreateGroup(Dictionary<string, FaunaAssetGroup> groups, string key)
        {
            if (!groups.TryGetValue(key, out FaunaAssetGroup group))
            {
                group = new FaunaAssetGroup { name = key };
                groups.Add(key, group);
            }

            return group;
        }

        private static void LoadMaterials(string directory, List<Material> materials)
        {
            materials.Clear();
            if (!AssetDatabase.IsValidFolder(directory))
                return;

            string[] guids = AssetDatabase.FindAssets("t:Material", new[] { directory });
            for (int i = 0; i < guids.Length; i++)
            {
                Material material = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guids[i]));
                if (material != null)
                    materials.Add(material);
            }
        }

        private static Material FindBestMaterial(string groupName, bool swarm, List<Material> materials)
        {
            Material fallback = null;
            for (int i = 0; i < materials.Count; i++)
            {
                Material material = materials[i];
                if (material == null)
                    continue;

                string materialName = material.name;
                bool isVat = ContainsIgnoreCase(materialName, "vat");
                if (swarm && isVat && TokenMatch(materialName, groupName))
                    return material;
                if (!swarm && !isVat && TokenMatch(materialName, groupName))
                    return material;
                if (fallback == null && ContainsIgnoreCase(materialName, "fauna") && (swarm == isVat || !swarm))
                    fallback = material;
            }

            return fallback;
        }

        private void BuildSkinnedFaunaVisuals(
            GameObject root,
            FaunaAssetGroup group,
            Material material,
            bool hasGpuSkinningSetting,
            bool gpuSkinningEnabled,
            FaunaAssemblyGroupReport report)
        {
            GameObject modelRoot = null;
            if (group.modelAssets.Count > 0)
            {
                modelRoot = (GameObject)PrefabUtility.InstantiatePrefab(group.modelAssets[0]);
                if (modelRoot != null)
                {
                    modelRoot.name = "Rig_" + group.name;
                    modelRoot.transform.SetParent(root.transform, false);
                }
            }

            if (modelRoot == null)
            {
                Mesh mesh = group.ResolvePrimaryMesh();
                if (mesh == null)
                {
                    s_ViolationScratch.Add("No rig GameObject or mesh was found.");
                    return;
                }

                for (int i = 0; i < group.lodMeshes.Length; i++)
                {
                    Mesh lodMesh = group.lodMeshes[i];
                    if (lodMesh == null && i == 0)
                        lodMesh = mesh;
                    if (lodMesh == null)
                        continue;

                    GameObject lodObject = new GameObject("LOD" + i + "_Skinned_" + group.name);
                    lodObject.transform.SetParent(root.transform, false);
                    SkinnedMeshRenderer renderer = lodObject.AddComponent<SkinnedMeshRenderer>();
                    renderer.sharedMesh = lodMesh;
                }
            }

            root.GetComponentsInChildren(true, s_SkinnedScratch);
            report.skinnedRendererCount = s_SkinnedScratch.Count;
            for (int i = 0; i < s_SkinnedScratch.Count; i++)
            {
                SkinnedMeshRenderer renderer = s_SkinnedScratch[i];
                ConfigureSkinnedRenderer(renderer, material, report);
                if (hasGpuSkinningSetting && !gpuSkinningEnabled)
                    report.gpuSkinningProjectSettingEnabled = false;
            }

            s_SkinnedScratch.Clear();
        }

        private static void ConfigureSkinnedRenderer(SkinnedMeshRenderer renderer, Material material, FaunaAssemblyGroupReport report)
        {
            if (renderer == null)
                return;

            renderer.updateWhenOffscreen = false;
            renderer.skinnedMotionVectors = true;
            renderer.quality = SkinQuality.Auto;
            renderer.allowOcclusionWhenDynamic = true;
            renderer.motionVectorGenerationMode = MotionVectorGenerationMode.Object;
            renderer.shadowCastingMode = ShadowCastingMode.On;
            renderer.receiveShadows = true;

            if (renderer.bones != null)
                report.maxBoneCount = math.max(report.maxBoneCount, renderer.bones.Length);
            if (renderer.bones != null && renderer.bones.Length > MaxGpuSkinBones)
                s_ViolationScratch.Add(renderer.name + " exceeds 96 GPU skin bones.");
            if (renderer.updateWhenOffscreen)
                s_ViolationScratch.Add(renderer.name + " has updateWhenOffscreen enabled.");
            if (!renderer.skinnedMotionVectors)
                s_ViolationScratch.Add(renderer.name + " has skinnedMotionVectors disabled.");

            AssignSharedMaterial(renderer, material);
        }

        private void BuildVatSwarmVisuals(GameObject root, FaunaAssetGroup group, Material material, FaunaAssemblyGroupReport report)
        {
            Mesh primaryMesh = group.ResolvePrimaryMesh();
            if (primaryMesh == null)
            {
                s_ViolationScratch.Add("VAT swarm has no static mesh.");
                return;
            }

            if (material == null || !ContainsIgnoreCase(material.name, "vat"))
                s_ViolationScratch.Add("VAT swarm has no VAT shared material.");

            int rendererCount = 0;
            for (int i = 0; i < group.lodMeshes.Length; i++)
            {
                Mesh lodMesh = group.lodMeshes[i];
                if (lodMesh == null && i == 0)
                    lodMesh = primaryMesh;
                if (lodMesh == null)
                    continue;

                GameObject visual = new GameObject("VAT_LOD" + i + "_" + group.name);
                visual.transform.SetParent(root.transform, false);
                MeshFilter filter = visual.AddComponent<MeshFilter>();
                MeshRenderer renderer = visual.AddComponent<MeshRenderer>();
                filter.sharedMesh = lodMesh;
                AssignSharedMaterial(renderer, material);
                renderer.allowOcclusionWhenDynamic = true;
                renderer.motionVectorGenerationMode = MotionVectorGenerationMode.Object;
                renderer.shadowCastingMode = ShadowCastingMode.On;
                renderer.receiveShadows = true;
                rendererCount++;
            }

            report.vatRendererCount = rendererCount;

            if (group.vatPositionTexture == null || group.vatNormalTexture == null)
                s_ViolationScratch.Add("VAT swarm is missing position or normal EXR texture.");

            if (material != null)
            {
                bool hasPositionProperty = HasAnyMaterialProperty(material, VatPositionPropertyNames);
                bool hasNormalProperty = HasAnyMaterialProperty(material, VatNormalPropertyNames);
                if (!hasPositionProperty)
                    s_ViolationScratch.Add("VAT material lacks a recognized position texture property.");
                if (!hasNormalProperty)
                    s_ViolationScratch.Add("VAT material lacks a recognized normal texture property.");

            }
        }

        private static Transform CreateSensoryAnchor(GameObject root)
        {
            Transform anchorBone = FindBestSensoryBone(root.transform);
            Transform parent = anchorBone != null ? anchorBone : root.transform;
            GameObject anchorObject = new GameObject("Sensory_Anchor");
            anchorObject.transform.SetParent(parent, false);
            if (anchorBone != null)
            {
                anchorObject.transform.localPosition = Vector3.zero;
            }
            else
            {
                Bounds bounds = ComputeRendererBounds(root);
                anchorObject.transform.localPosition = root.transform.InverseTransformPoint(bounds.center);
            }

            anchorObject.transform.localRotation = Quaternion.identity;
            anchorObject.transform.localScale = Vector3.one;
            return anchorObject.transform;
        }

        private static Collider BuildPrimitiveHitboxes(GameObject root, bool swarm, int hitboxLayer, FaunaAssemblyGroupReport report)
        {
            s_ColliderScratch.Clear();
            if (swarm)
            {
                Bounds swarmBounds = ComputeRendererBounds(root);
                float3 extents = math.abs(new float3(swarmBounds.extents.x, swarmBounds.extents.y, swarmBounds.extents.z));
                float radius = math.max(
                    DefaultSwarmAggregateRadiusMeters,
                    math.max(extents.x, math.max(extents.y, extents.z)));
                Vector3 localCenter = root.transform.InverseTransformPoint(swarmBounds.center);
                SphereCollider sphere = CreateSphereHitbox(root.transform, "Fauna_Hitbox_Aggregate", localCenter, radius, hitboxLayer);
                s_ColliderScratch.Add(sphere);
                report.primitiveColliderCount = 1;
                return sphere;
            }

            root.GetComponentsInChildren(true, s_TransformScratch);
            Bounds bounds = ComputeRendererBounds(root);
            Collider aggregate = CreateAggregateCapsule(root.transform, bounds, hitboxLayer);
            if (aggregate != null)
                s_ColliderScratch.Add(aggregate);

            int fineHitboxCount = 0;
            int skippedFineHitboxCandidates = 0;
            for (int i = 0; i < s_TransformScratch.Count; i++)
            {
                Transform bone = s_TransformScratch[i];
                if (bone == null || bone == root.transform || !IsMajorHitboxBone(bone.name))
                    continue;
                if (fineHitboxCount >= MaxFineHitboxes)
                {
                    skippedFineHitboxCandidates++;
                    continue;
                }

                Collider collider = TryCreateBoneHitbox(root.transform, bone, bounds, hitboxLayer);
                if (collider != null)
                {
                    s_ColliderScratch.Add(collider);
                    fineHitboxCount++;
                }
            }

            s_TransformScratch.Clear();
            report.primitiveColliderCount = s_ColliderScratch.Count;
            report.fineColliderCount = fineHitboxCount;
            report.skippedFineHitboxCandidateCount = skippedFineHitboxCandidates;
            return s_ColliderScratch.Count > 0 ? s_ColliderScratch[0] : null;
        }

        private static Collider TryCreateBoneHitbox(Transform root, Transform bone, Bounds rendererBounds, int hitboxLayer)
        {
            string boneName = bone.name;
            if (ContainsIgnoreCase(boneName, "head") || ContainsIgnoreCase(boneName, "eye") || bone.childCount == 0)
            {
                float radius = Mathf.Clamp(rendererBounds.extents.magnitude * 0.12f, MinimumHitboxRadiusMeters, rendererBounds.extents.magnitude * 0.35f);
                return CreateSphereHitbox(bone, "Fauna_Hitbox_" + SanitizeAssetName(boneName), Vector3.zero, radius, hitboxLayer);
            }

            Transform child = SelectLongestChild(bone);
            if (child == null)
                return null;

            Vector3 directionWorld = child.position - bone.position;
            float lengthWorld = directionWorld.magnitude;
            if (lengthWorld <= 0.001f)
                return null;

            GameObject hitbox = new GameObject("Fauna_Hitbox_" + SanitizeAssetName(boneName));
            hitbox.layer = hitboxLayer >= 0 ? hitboxLayer : root.gameObject.layer;
            hitbox.transform.SetParent(bone, false);
            hitbox.transform.localScale = Vector3.one;

            Vector3 centerWorld = bone.position + directionWorld * 0.5f;
            Vector3 localDirection = bone.InverseTransformVector(directionWorld);
            hitbox.transform.localPosition = bone.InverseTransformPoint(centerWorld);
            hitbox.transform.localRotation = localDirection.sqrMagnitude > 0.000001f
                ? Quaternion.FromToRotation(Vector3.up, localDirection.normalized)
                : Quaternion.identity;

            float3 absScale = math.abs(new float3(
                hitbox.transform.lossyScale.x,
                hitbox.transform.lossyScale.y,
                hitbox.transform.lossyScale.z));
            float axisScale = math.max(0.001f, absScale.y);
            float radialScale = math.max(0.001f, math.max(absScale.x, absScale.z));
            float radiusWorld = Mathf.Clamp(lengthWorld * 0.18f, MinimumHitboxRadiusMeters, rendererBounds.extents.magnitude * 0.25f);
            float radiusLocal = radiusWorld / radialScale;
            CapsuleCollider capsule = hitbox.AddComponent<CapsuleCollider>();
            capsule.direction = 1;
            capsule.radius = radiusLocal;
            capsule.height = math.max(radiusLocal * 2f, (lengthWorld * CapsuleJointOverlapScale) / axisScale + radiusLocal * 2f);
            capsule.center = Vector3.zero;
            capsule.isTrigger = false;
            return capsule;
        }

        private static CapsuleCollider CreateAggregateCapsule(Transform root, Bounds bounds, int hitboxLayer)
        {
            GameObject hitbox = new GameObject("Fauna_Hitbox_Aggregate");
            hitbox.layer = hitboxLayer >= 0 ? hitboxLayer : root.gameObject.layer;
            hitbox.transform.SetParent(root, false);
            hitbox.transform.localPosition = root.InverseTransformPoint(bounds.center);
            hitbox.transform.localRotation = Quaternion.identity;
            hitbox.transform.localScale = Vector3.one;

            float3 extents = math.abs(new float3(bounds.extents.x, bounds.extents.y, bounds.extents.z));
            int direction = 0;
            float axisExtent = extents.x;
            float radialA = extents.y;
            float radialB = extents.z;
            if (extents.y > axisExtent && extents.y >= extents.z)
            {
                direction = 1;
                axisExtent = extents.y;
                radialA = extents.x;
                radialB = extents.z;
            }
            else if (extents.z > axisExtent)
            {
                direction = 2;
                axisExtent = extents.z;
                radialA = extents.x;
                radialB = extents.y;
            }

            float radius = math.max(MinimumHitboxRadiusMeters, math.max(radialA, radialB) * 0.55f);
            float height = math.max(radius * 2f, axisExtent * 2f + radius * 2f);
            CapsuleCollider capsule = hitbox.AddComponent<CapsuleCollider>();
            capsule.direction = direction;
            capsule.radius = radius;
            capsule.height = height;
            capsule.center = Vector3.zero;
            capsule.isTrigger = false;
            return capsule;
        }

        private static SphereCollider CreateSphereHitbox(Transform parent, string name, Vector3 localPosition, float radius, int hitboxLayer)
        {
            GameObject hitbox = new GameObject(name);
            hitbox.layer = hitboxLayer >= 0 ? hitboxLayer : parent.gameObject.layer;
            hitbox.transform.SetParent(parent, false);
            hitbox.transform.localPosition = localPosition;
            hitbox.transform.localRotation = Quaternion.identity;
            hitbox.transform.localScale = Vector3.one;
            SphereCollider sphere = hitbox.AddComponent<SphereCollider>();
            sphere.radius = math.max(MinimumHitboxRadiusMeters, radius);
            sphere.center = Vector3.zero;
            sphere.isTrigger = false;
            return sphere;
        }

        private static void ConfigureMetadata(
            GameObject root,
            FaunaMetadata metadata,
            Transform sensoryAnchor,
            Collider aggregateCollider,
            FaunaAssetGroup group,
            FaunaAssemblyGroupReport report)
        {
            s_CullingColliderScratch.Clear();
            if (aggregateCollider != null)
                s_CullingColliderScratch.Add(aggregateCollider);
            for (int i = 0; i < s_ColliderScratch.Count && s_CullingColliderScratch.Count < MaxPhysicsCullingColliders; i++)
            {
                Collider collider = s_ColliderScratch[i];
                if (collider != null && collider != aggregateCollider)
                    s_CullingColliderScratch.Add(collider);
            }

            Collider[] cullingColliders = s_CullingColliderScratch.ToArray();
            Light[] biolumLights = CollectBiolumPresentationLights(root);
            Collider[] fineColliders;
            if (s_ColliderScratch.Count <= 1)
            {
                fineColliders = Array.Empty<Collider>();
            }
            else
            {
                int fineCount = 0;
                for (int i = 0; i < s_ColliderScratch.Count; i++)
                {
                    Collider collider = s_ColliderScratch[i];
                    if (collider != null && collider != aggregateCollider)
                        fineCount++;
                }

                fineColliders = fineCount > 0 ? new Collider[fineCount] : Array.Empty<Collider>();
                int write = 0;
                for (int i = 0; i < s_ColliderScratch.Count; i++)
                {
                    Collider collider = s_ColliderScratch[i];
                    if (collider != null && collider != aggregateCollider)
                        fineColliders[write++] = collider;
                }
            }

            FaunaMetadataFlags flags = aggregateCollider != null
                ? FaunaMetadataFlags.PrimitiveHitboxes
                : FaunaMetadataFlags.None;
            if (fineColliders.Length > 0)
                flags |= FaunaMetadataFlags.FineHitboxCulling;
            if (biolumLights.Length > 0)
                flags |= FaunaMetadataFlags.BiolumPresentationLights;
            flags |= group.IsSwarm ? FaunaMetadataFlags.VatSwarm : FaunaMetadataFlags.GpuSkinned;
            Bounds localRenderBounds = new Bounds(Vector3.zero, Vector3.one);
            if (TryComputeRootLocalRendererBounds(root, out localRenderBounds))
                flags |= FaunaMetadataFlags.RenderBounds;
            metadata.EditorConfigure(
                sensoryAnchor,
                aggregateCollider,
                cullingColliders,
                fineColliders,
                biolumLights,
                group.vatPositionTexture,
                group.vatNormalTexture,
                new Vector4(1f, 1f, 0f, 0f),
                new Vector4(1f, 1f, 0f, 0f),
                new Vector4(0f, 1f, 0f, 0f),
                localRenderBounds,
                InferLocomotionType(group.name, group.IsSwarm),
                InferSensoryChannels(group.name, group.IsSwarm),
                flags);

            report.physicsCullingColliderCount = cullingColliders.Length;
            report.biolumPresentationLightCount = biolumLights.Length;
        }

        private static Light[] CollectBiolumPresentationLights(GameObject root)
        {
            s_LightScratch.Clear();
            root.GetComponentsInChildren(true, s_LightScratch);
            int sourceCount = s_LightScratch.Count;
            int count = 0;
            for (int i = 0; i < sourceCount && count < MaxBiolumPresentationLights; i++)
            {
                if (s_LightScratch[i] != null)
                    count++;
            }

            if (count <= 0)
            {
                s_LightScratch.Clear();
                return Array.Empty<Light>();
            }

            Light[] lights = new Light[count];
            int write = 0;
            for (int i = 0; i < sourceCount && write < count; i++)
            {
                Light light = s_LightScratch[i];
                if (light != null)
                    lights[write++] = light;
            }

            s_LightScratch.Clear();
            return lights;
        }

        private static void BuildLodGroup(GameObject root, FaunaAssemblyGroupReport report)
        {
            root.GetComponentsInChildren(true, s_RendererScratch);
            if (s_RendererScratch.Count == 0)
            {
                s_ViolationScratch.Add("No renderers available for LODGroup.");
                return;
            }

            LODGroup lodGroup = root.GetComponent<LODGroup>();
            if (lodGroup == null)
                lodGroup = root.AddComponent<LODGroup>();
            lodGroup.fadeMode = LODFadeMode.CrossFade;
            lodGroup.animateCrossFading = true;

            s_Lod0RendererScratch.Clear();
            s_Lod1RendererScratch.Clear();
            s_Lod2RendererScratch.Clear();
            s_LodScratch.Clear();
            for (int i = 0; i < s_RendererScratch.Count; i++)
            {
                Renderer renderer = s_RendererScratch[i];
                if (renderer == null)
                    continue;

                if (ContainsIgnoreCase(renderer.name, "LOD2"))
                    s_Lod2RendererScratch.Add(renderer);
                else if (ContainsIgnoreCase(renderer.name, "LOD1"))
                    s_Lod1RendererScratch.Add(renderer);
                else
                    s_Lod0RendererScratch.Add(renderer);
            }

            if (s_Lod0RendererScratch.Count == 0)
            {
                for (int i = 0; i < s_RendererScratch.Count; i++)
                {
                    Renderer renderer = s_RendererScratch[i];
                    if (renderer != null)
                        s_Lod0RendererScratch.Add(renderer);
                }
            }

            s_LodScratch.Add(new LOD(
                s_Lod1RendererScratch.Count > 0 || s_Lod2RendererScratch.Count > 0 ? 0.6f : 0.04f,
                s_Lod0RendererScratch.ToArray()));
            if (s_Lod1RendererScratch.Count > 0)
                s_LodScratch.Add(new LOD(0.24f, s_Lod1RendererScratch.ToArray()));
            if (s_Lod2RendererScratch.Count > 0)
                s_LodScratch.Add(new LOD(0.06f, s_Lod2RendererScratch.ToArray()));

            lodGroup.SetLODs(s_LodScratch.ToArray());
            lodGroup.RecalculateBounds();
            report.lodGroupConfigured = true;
            s_RendererScratch.Clear();
            s_Lod0RendererScratch.Clear();
            s_Lod1RendererScratch.Clear();
            s_Lod2RendererScratch.Clear();
            s_LodScratch.Clear();
        }

        private static void ValidatePrefab(
            GameObject root,
            int hitboxLayer,
            bool hasGpuSkinningSetting,
            bool gpuSkinningEnabled,
            FaunaAssemblyGroupReport report)
        {
            root.GetComponentsInChildren(true, s_RendererScratch);
            for (int i = 0; i < s_RendererScratch.Count; i++)
                ValidateRendererForBrg(s_RendererScratch[i], report);
            s_RendererScratch.Clear();

            root.GetComponentsInChildren(true, s_MeshColliderScratch);
            if (s_MeshColliderScratch.Count > 0)
                s_ViolationScratch.Add("Prefab contains MeshCollider components.");
            s_MeshColliderScratch.Clear();

            root.GetComponentsInChildren(true, s_ColliderScratch);
            for (int i = 0; i < s_ColliderScratch.Count; i++)
            {
                Collider collider = s_ColliderScratch[i];
                if (collider == null)
                    continue;
                if (hitboxLayer >= 0 && collider.gameObject.layer != hitboxLayer)
                    s_ViolationScratch.Add(collider.name + " is not on Fauna_Hitbox layer.");
            }

            root.GetComponentsInChildren(true, s_SkinnedScratch);
            for (int i = 0; i < s_SkinnedScratch.Count; i++)
            {
                SkinnedMeshRenderer renderer = s_SkinnedScratch[i];
                if (renderer.bones == null || renderer.bones.Length == 0)
                    s_ViolationScratch.Add(renderer.name + " has no bones; skinned fauna requires a rigged mesh.");
                if (renderer.bones != null && renderer.bones.Length > MaxGpuSkinBones)
                    s_ViolationScratch.Add(renderer.name + " exceeds bone count gate.");
                if (renderer.updateWhenOffscreen)
                    s_ViolationScratch.Add(renderer.name + " violates updateWhenOffscreen=false.");
                if (!renderer.skinnedMotionVectors)
                    s_ViolationScratch.Add(renderer.name + " violates skinnedMotionVectors=true.");
            }

            if (report.isSwarm && s_SkinnedScratch.Count > 0)
                s_ViolationScratch.Add("VAT swarm contains SkinnedMeshRenderer components; swarms must use MeshRenderer VAT visuals.");
            if (!report.isSwarm && s_SkinnedScratch.Count > 0 && hasGpuSkinningSetting && !gpuSkinningEnabled)
                s_ViolationScratch.Add("GPU skinning project setting is not enabled.");
            s_SkinnedScratch.Clear();

            if (report.isSwarm)
            {
                root.GetComponentsInChildren(true, s_AnimatorScratch);
                if (s_AnimatorScratch.Count > 0)
                    s_ViolationScratch.Add("VAT swarm contains Animator components; swarm animation must be VAT or indirect only.");
                s_AnimatorScratch.Clear();
            }

            FaunaMetadata metadata = root.GetComponent<FaunaMetadata>();
            if (metadata == null)
                s_ViolationScratch.Add("FaunaMetadata missing from prefab root.");
            else
            {
                if (metadata.SensoryAnchor == null)
                    s_ViolationScratch.Add("FaunaMetadata sensory anchor missing.");
                else if (!IsFinite(metadata.SensoryAnchor.position))
                    s_ViolationScratch.Add("FaunaMetadata sensory anchor position is non-finite.");
                if (!metadata.TryGetPhysicsCullingColliders(out Collider[] colliders, out int count) || count == 0 || colliders == null)
                    s_ViolationScratch.Add("FaunaMetadata physics culling colliders missing.");
                else if (metadata.EditorPhysicsCullingColliderSerializedLength > MaxPhysicsCullingColliders)
                    s_ViolationScratch.Add("FaunaMetadata physics culling collider count exceeds authoring cap.");
                if (metadata.EditorFineHitboxColliderSerializedLength > MaxFineHitboxes)
                    s_ViolationScratch.Add("FaunaMetadata fine hitbox count exceeds authoring cap.");
                if (metadata.EditorBiolumPresentationLightSerializedLength > MaxBiolumPresentationLights)
                    s_ViolationScratch.Add("FaunaMetadata biolum light count exceeds authoring cap.");
                if (!metadata.TryGetLocalRenderBounds(out _))
                    s_ViolationScratch.Add("FaunaMetadata root-local render bounds missing.");
                if (metadata.LocomotionType == FaunaLocomotionType.Unknown)
                    s_ViolationScratch.Add("FaunaMetadata locomotion type missing.");
                if (metadata.SensoryChannels == FaunaSensoryChannels.None)
                    s_ViolationScratch.Add("FaunaMetadata sensory channel contract missing.");
            }

            LODGroup lodGroup = root.GetComponent<LODGroup>();
            if (lodGroup == null || lodGroup.fadeMode != LODFadeMode.CrossFade)
                s_ViolationScratch.Add("LODGroup CrossFade configuration missing.");
        }

        private static void ValidateRendererForBrg(Renderer renderer, FaunaAssemblyGroupReport report)
        {
            if (renderer == null)
                return;

            Material[] materials = renderer.sharedMaterials;
            if (materials == null || materials.Length == 0)
            {
                s_ViolationScratch.Add(renderer.name + " has no shared material.");
                return;
            }

            for (int i = 0; i < materials.Length; i++)
            {
                Material material = materials[i];
                if (material == null)
                {
                    s_ViolationScratch.Add(renderer.name + " has null shared material slot.");
                    continue;
                }

                string materialPath = AssetDatabase.GetAssetPath(material);
                if (string.IsNullOrEmpty(materialPath))
                {
                    s_ViolationScratch.Add(renderer.name + " uses a non-asset material instance.");
                    continue;
                }

                Shader shader = material.shader;
                string shaderPath = shader != null ? AssetDatabase.GetAssetPath(shader) : string.Empty;
                if (string.IsNullOrEmpty(shaderPath))
                {
                    s_ViolationScratch.Add(material.name + " has no asset-backed shader.");
                    continue;
                }

                if (!ShaderDeclaresUnityPerMaterial(shaderPath))
                    s_ViolationScratch.Add(material.name + " shader lacks UnityPerMaterial CBUFFER evidence.");
                else
                    report.brgMaterialPasses++;
            }
        }

        private static bool ShaderDeclaresUnityPerMaterial(string shaderAssetPath)
        {
            if (s_ShaderUnityPerMaterialCache.TryGetValue(shaderAssetPath, out bool cached))
                return cached;

            if (shaderAssetPath.EndsWith(".shadergraph", StringComparison.OrdinalIgnoreCase))
            {
                s_ShaderUnityPerMaterialCache[shaderAssetPath] = true;
                return true;
            }

            string fullPath = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), shaderAssetPath));
            if (!File.Exists(fullPath))
            {
                s_ShaderUnityPerMaterialCache[shaderAssetPath] = false;
                return false;
            }

            string source = File.ReadAllText(fullPath);
            bool valid = source.IndexOf("CBUFFER_START(UnityPerMaterial)", StringComparison.Ordinal) >= 0 ||
                         source.IndexOf("UnityPerMaterial", StringComparison.Ordinal) >= 0;
            s_ShaderUnityPerMaterialCache[shaderAssetPath] = valid;
            return valid;
        }

        private static Bounds ComputeRendererBounds(GameObject root)
        {
            root.GetComponentsInChildren(true, s_RendererScratch);
            Bounds bounds = new Bounds(root.transform.position, Vector3.one);
            bool initialized = false;
            for (int i = 0; i < s_RendererScratch.Count; i++)
            {
                Renderer renderer = s_RendererScratch[i];
                if (renderer == null)
                    continue;
                if (!initialized)
                {
                    bounds = renderer.bounds;
                    initialized = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }

            s_RendererScratch.Clear();
            return bounds;
        }

        private static bool TryComputeRootLocalRendererBounds(GameObject root, out Bounds bounds)
        {
            root.GetComponentsInChildren(true, s_RendererScratch);
            Transform rootTransform = root.transform;
            bounds = new Bounds(Vector3.zero, Vector3.one);
            bool initialized = false;
            for (int i = 0; i < s_RendererScratch.Count; i++)
            {
                Renderer renderer = s_RendererScratch[i];
                if (renderer == null)
                    continue;

                Bounds worldBounds = renderer.bounds;
                Vector3 min = worldBounds.min;
                Vector3 max = worldBounds.max;
                EncapsulateRootLocalBounds(rootTransform, ref bounds, ref initialized, new Vector3(min.x, min.y, min.z));
                EncapsulateRootLocalBounds(rootTransform, ref bounds, ref initialized, new Vector3(min.x, min.y, max.z));
                EncapsulateRootLocalBounds(rootTransform, ref bounds, ref initialized, new Vector3(min.x, max.y, min.z));
                EncapsulateRootLocalBounds(rootTransform, ref bounds, ref initialized, new Vector3(min.x, max.y, max.z));
                EncapsulateRootLocalBounds(rootTransform, ref bounds, ref initialized, new Vector3(max.x, min.y, min.z));
                EncapsulateRootLocalBounds(rootTransform, ref bounds, ref initialized, new Vector3(max.x, min.y, max.z));
                EncapsulateRootLocalBounds(rootTransform, ref bounds, ref initialized, new Vector3(max.x, max.y, min.z));
                EncapsulateRootLocalBounds(rootTransform, ref bounds, ref initialized, new Vector3(max.x, max.y, max.z));
            }

            s_RendererScratch.Clear();
            return initialized && IsFinite(bounds.center) && IsFinite(bounds.extents);
        }

        private static void EncapsulateRootLocalBounds(
            Transform rootTransform,
            ref Bounds bounds,
            ref bool initialized,
            Vector3 worldPoint)
        {
            Vector3 localPoint = rootTransform.InverseTransformPoint(worldPoint);
            if (!initialized)
            {
                bounds = new Bounds(localPoint, Vector3.zero);
                initialized = true;
                return;
            }

            bounds.Encapsulate(localPoint);
        }

        private static Transform FindBestSensoryBone(Transform root)
        {
            root.GetComponentsInChildren(true, s_TransformScratch);
            Transform fallback = null;
            for (int i = 0; i < s_TransformScratch.Count; i++)
            {
                Transform transform = s_TransformScratch[i];
                if (transform == null)
                    continue;

                string name = transform.name;
                if (ContainsIgnoreCase(name, "eye"))
                {
                    s_TransformScratch.Clear();
                    return transform;
                }

                if (fallback == null && (ContainsIgnoreCase(name, "head") || ContainsIgnoreCase(name, "jaw")))
                    fallback = transform;
            }

            s_TransformScratch.Clear();
            return fallback;
        }

        private static Transform SelectLongestChild(Transform bone)
        {
            Transform best = null;
            float bestDistanceSq = 0f;
            for (int i = 0; i < bone.childCount; i++)
            {
                Transform child = bone.GetChild(i);
                float distanceSq = (child.position - bone.position).sqrMagnitude;
                if (distanceSq > bestDistanceSq)
                {
                    bestDistanceSq = distanceSq;
                    best = child;
                }
            }

            return best;
        }

        private static bool IsMajorHitboxBone(string boneName)
        {
            return ContainsIgnoreCase(boneName, "spine") ||
                   ContainsIgnoreCase(boneName, "head") ||
                   ContainsIgnoreCase(boneName, "tail") ||
                   ContainsIgnoreCase(boneName, "neck") ||
                   ContainsIgnoreCase(boneName, "jaw") ||
                   ContainsIgnoreCase(boneName, "eye") ||
                   ContainsIgnoreCase(boneName, "fin") ||
                   ContainsIgnoreCase(boneName, "tentacle");
        }

        private static FaunaLocomotionType InferLocomotionType(string groupName, bool swarm)
        {
            if (ContainsIgnoreCase(groupName, "crawler") ||
                ContainsIgnoreCase(groupName, "crab") ||
                ContainsIgnoreCase(groupName, "walker"))
            {
                return FaunaLocomotionType.Crawler;
            }

            if (ContainsIgnoreCase(groupName, "ambush") ||
                ContainsIgnoreCase(groupName, "angler") ||
                ContainsIgnoreCase(groupName, "lurker"))
            {
                return FaunaLocomotionType.Ambush;
            }

            if (ContainsIgnoreCase(groupName, "drift") ||
                ContainsIgnoreCase(groupName, "jelly"))
            {
                return FaunaLocomotionType.Drifting;
            }

            if (ContainsIgnoreCase(groupName, "burrow") ||
                ContainsIgnoreCase(groupName, "worm"))
            {
                return FaunaLocomotionType.Burrowing;
            }

            if (ContainsIgnoreCase(groupName, "tentacle") ||
                ContainsIgnoreCase(groupName, "squid") ||
                ContainsIgnoreCase(groupName, "octo"))
            {
                return FaunaLocomotionType.Tentacled;
            }

            if (ContainsIgnoreCase(groupName, "armor") ||
                ContainsIgnoreCase(groupName, "shell"))
            {
                return FaunaLocomotionType.Armored;
            }

            return FaunaLocomotionType.Swimmer;
        }

        private static FaunaSensoryChannels InferSensoryChannels(string groupName, bool swarm)
        {
            FaunaSensoryChannels channels =
                FaunaSensoryChannels.Sound |
                FaunaSensoryChannels.Light |
                FaunaSensoryChannels.SonarPing;

            if (ContainsIgnoreCase(groupName, "electric") ||
                ContainsIgnoreCase(groupName, "power") ||
                ContainsIgnoreCase(groupName, "angler"))
            {
                channels |= FaunaSensoryChannels.ElectricalPower;
            }

            if (!swarm &&
                (ContainsIgnoreCase(groupName, "pred") ||
                 ContainsIgnoreCase(groupName, "hunter") ||
                 ContainsIgnoreCase(groupName, "shark") ||
                 ContainsIgnoreCase(groupName, "eel") ||
                 ContainsIgnoreCase(groupName, "leviathan")))
            {
                channels |= FaunaSensoryChannels.BloodChemistry;
            }

            if (!swarm &&
                (ContainsIgnoreCase(groupName, "ambush") ||
                 ContainsIgnoreCase(groupName, "territory") ||
                 ContainsIgnoreCase(groupName, "guard") ||
                 ContainsIgnoreCase(groupName, "nest")))
            {
                channels |= FaunaSensoryChannels.Territory;
            }

            return channels;
        }

        private static bool IsFinite(Vector3 value)
        {
            return math.all(math.isfinite(new float3(value.x, value.y, value.z)));
        }

        private static void AssignSharedMaterial(Renderer renderer, Material material)
        {
            if (renderer == null || material == null)
                return;

            Material[] sharedMaterials = renderer.sharedMaterials;
            if (sharedMaterials == null || sharedMaterials.Length == 0)
            {
                renderer.sharedMaterial = material;
                return;
            }

            for (int i = 0; i < sharedMaterials.Length; i++)
                sharedMaterials[i] = material;
            renderer.sharedMaterials = sharedMaterials;
        }

        private Material ResolveVatMaterialForGroup(
            Material sourceMaterial,
            string groupName,
            out string materialPath,
            out bool created)
        {
            materialPath = null;
            created = false;
            if (sourceMaterial == null || dryRun)
                return sourceMaterial;

            string materialFolder = ResolveGeneratedMaterialFolder(outputDirectory);
            EnsureAssetFolder(materialFolder);
            materialPath = materialFolder + "/MAT_" + SanitizeAssetName(groupName) + "_VAT.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null)
            {
                Material transientMaterial = new Material(sourceMaterial);
                AssetDatabase.CreateAsset(transientMaterial, materialPath);
                created = true;
                material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                if (material == null)
                {
                    DestroyImmediate(transientMaterial);
                    s_ViolationScratch.Add("VAT material asset creation failed.");
                    return null;
                }
            }

            return material;
        }

        private static bool PrepareVatMaterialForSave(
            GameObject root,
            Material material,
            Material sourceMaterial,
            FaunaAssetGroup group)
        {
            if (root == null || material == null || sourceMaterial == null || group == null)
                return false;
            if (group.vatPositionTexture == null || group.vatNormalTexture == null)
                return false;

            if (material.shader != sourceMaterial.shader)
                material.shader = sourceMaterial.shader;
            material.CopyPropertiesFromMaterial(sourceMaterial);
            string materialPath = AssetDatabase.GetAssetPath(material);
            if (!string.IsNullOrEmpty(materialPath))
                material.name = Path.GetFileNameWithoutExtension(materialPath);

            if (!HasAnyMaterialProperty(material, VatPositionPropertyNames) ||
                !HasAnyMaterialProperty(material, VatNormalPropertyNames))
                return false;

            bool positionAssigned = AssignTextureIfPropertyExists(material, VatPositionPropertyNames, group.vatPositionTexture);
            bool normalAssigned = AssignTextureIfPropertyExists(material, VatNormalPropertyNames, group.vatNormalTexture);
            if (!positionAssigned || !normalAssigned)
                return false;

            root.GetComponentsInChildren(true, s_RendererScratch);
            for (int i = 0; i < s_RendererScratch.Count; i++)
                AssignSharedMaterial(s_RendererScratch[i], material);
            s_RendererScratch.Clear();

            EditorUtility.SetDirty(material);
            return true;
        }

        private static bool DeleteCreatedVatMaterial(string materialPath, bool created)
        {
            return created && !string.IsNullOrEmpty(materialPath) && AssetDatabase.DeleteAsset(materialPath);
        }

        private static string ResolveGeneratedMaterialFolder(string rootOutputDirectory)
        {
            string directory = string.IsNullOrEmpty(rootOutputDirectory) ? DefaultOutputDirectory : rootOutputDirectory;
            return directory.TrimEnd('/') + "/" + GeneratedMaterialFolderName;
        }

        private static bool AssignTextureIfPropertyExists(Material material, string[] propertyNames, Texture texture)
        {
            if (material == null || texture == null)
                return false;

            for (int i = 0; i < propertyNames.Length; i++)
            {
                string propertyName = propertyNames[i];
                if (material.HasProperty(propertyName))
                {
                    material.SetTexture(propertyName, texture);
                    return true;
                }
            }

            return false;
        }

        private static bool HasAnyMaterialProperty(Material material, string[] propertyNames)
        {
            if (material == null)
                return false;

            for (int i = 0; i < propertyNames.Length; i++)
            {
                if (material.HasProperty(propertyNames[i]))
                    return true;
            }

            return false;
        }

        private static bool TryReadGpuSkinningProjectSetting(out bool enabled)
        {
            enabled = false;
            PropertyInfo property = typeof(PlayerSettings).GetProperty("gpuSkinning", BindingFlags.Public | BindingFlags.Static);
            if (property == null || property.PropertyType != typeof(bool))
                return false;

            enabled = (bool)property.GetValue(null, null);
            return true;
        }

        private static bool TryWriteGpuSkinningProjectSetting(bool enabled)
        {
            PropertyInfo property = typeof(PlayerSettings).GetProperty("gpuSkinning", BindingFlags.Public | BindingFlags.Static);
            if (property == null || property.PropertyType != typeof(bool) || !property.CanWrite)
                return false;

            property.SetValue(null, enabled, null);
            return true;
        }

        private static string ResolvePrefabPath(string outputDirectory, string groupName)
        {
            string directory = string.IsNullOrEmpty(outputDirectory) ? DefaultOutputDirectory : outputDirectory;
            return directory.TrimEnd('/') + "/PFB_" + SanitizeAssetName(groupName) + ".prefab";
        }

        private static string NormalizeGroupName(string rawName)
        {
            string name = rawName;
            for (int i = 0; i < AssetNamePrefixes.Length; i++)
            {
                string prefix = AssetNamePrefixes[i];
                if (name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    name = name.Substring(prefix.Length);
            }

            for (int i = 0; i < AssetNameCuts.Length; i++)
            {
                int index = name.IndexOf(AssetNameCuts[i], StringComparison.OrdinalIgnoreCase);
                if (index > 0)
                    name = name.Substring(0, index);
            }

            return SanitizeAssetName(name);
        }

        private static int ResolveLodIndex(string path)
        {
            if (ContainsIgnoreCase(path, "LOD2"))
                return 2;
            if (ContainsIgnoreCase(path, "LOD1"))
                return 1;
            return 0;
        }

        private static bool TokenMatch(string candidate, string groupName)
        {
            if (ContainsIgnoreCase(candidate, groupName))
                return true;

            if (string.IsNullOrEmpty(candidate) || string.IsNullOrEmpty(groupName))
                return false;

            int tokenStart = 0;
            for (int i = 0; i <= groupName.Length; i++)
            {
                if (i < groupName.Length && groupName[i] != '_')
                    continue;

                int tokenLength = i - tokenStart;
                if (tokenLength >= 4 && ContainsIgnoreCase(candidate, groupName, tokenStart, tokenLength))
                    return true;
                tokenStart = i + 1;
            }

            return false;
        }

        private static bool ContainsIgnoreCase(string text, string value)
        {
            return text != null && value != null && text.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool ContainsIgnoreCase(string text, string value, int valueStart, int valueLength)
        {
            if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(value) || valueLength <= 0)
                return false;
            if (valueStart < 0 || valueStart + valueLength > value.Length || valueLength > text.Length)
                return false;

            int maxStart = text.Length - valueLength;
            for (int i = 0; i <= maxStart; i++)
            {
                if (string.Compare(text, i, value, valueStart, valueLength, StringComparison.OrdinalIgnoreCase) == 0)
                    return true;
            }

            return false;
        }

        private static string SanitizeAssetName(string name)
        {
            if (string.IsNullOrEmpty(name))
                return "UnnamedFauna";

            char[] chars = name.ToCharArray();
            for (int i = 0; i < chars.Length; i++)
            {
                char c = chars[i];
                if (!char.IsLetterOrDigit(c) && c != '_' && c != '-')
                    chars[i] = '_';
            }

            return new string(chars);
        }

        private static string[] AppendViolation(string[] existing, string violation)
        {
            int length = existing != null ? existing.Length : 0;
            string[] next = new string[length + 1];
            for (int i = 0; i < length; i++)
                next[i] = existing[i];
            next[length] = violation;
            return next;
        }

        private static bool EnsureAssetFolder(string assetPath)
        {
            if (AssetDatabase.IsValidFolder(assetPath))
                return false;

            if (string.IsNullOrEmpty(assetPath) ||
                assetPath.Length < 6 ||
                assetPath[0] != 'A' ||
                assetPath[1] != 's' ||
                assetPath[2] != 's' ||
                assetPath[3] != 'e' ||
                assetPath[4] != 't' ||
                assetPath[5] != 's' ||
                (assetPath.Length > 6 && assetPath[6] != '/'))
            {
                throw new InvalidOperationException("Asset folder must start with Assets/: " + assetPath);
            }

            string current = "Assets";
            bool created = false;
            int segmentStart = 7;
            while (segmentStart < assetPath.Length)
            {
                int slashIndex = assetPath.IndexOf('/', segmentStart);
                int segmentLength = slashIndex >= 0 ? slashIndex - segmentStart : assetPath.Length - segmentStart;
                if (segmentLength <= 0)
                {
                    segmentStart++;
                    continue;
                }

                string segment = assetPath.Substring(segmentStart, segmentLength);
                string next = current + "/" + segment;
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, segment);
                    created = true;
                }

                current = next;
                if (slashIndex < 0)
                    break;

                segmentStart = slashIndex + 1;
            }

            return created;
        }

        private static long TicksToMicroseconds(long ticks)
        {
            return (long)(ticks * (1000000.0 / Stopwatch.Frequency));
        }

        [Serializable]
        private sealed class FaunaAssetGroup
        {
            public string name;
            public bool forceSwarm;
            public readonly Mesh[] lodMeshes = new Mesh[3];
            public readonly List<GameObject> modelAssets = new List<GameObject>(4);
            public readonly List<string> assetPaths = new List<string>(8);
            public Texture2D vatPositionTexture;
            public Texture2D vatNormalTexture;

            public int InputAssetCount => assetPaths.Count;

            public bool IsSwarm
            {
                get
                {
                    return forceSwarm ||
                           vatPositionTexture != null ||
                           ContainsIgnoreCase(name, "swarm") ||
                           ContainsIgnoreCase(name, "shoal") ||
                           ContainsIgnoreCase(name, "school");
                }
            }

            public Mesh ResolvePrimaryMesh()
            {
                for (int i = 0; i < lodMeshes.Length; i++)
                {
                    if (lodMeshes[i] != null)
                        return lodMeshes[i];
                }

                for (int i = 0; i < modelAssets.Count; i++)
                {
                    GameObject asset = modelAssets[i];
                    if (asset == null)
                        continue;

                    MeshFilter filter = asset.GetComponentInChildren<MeshFilter>(true);
                    if (filter != null && filter.sharedMesh != null)
                        return filter.sharedMesh;

                    SkinnedMeshRenderer renderer = asset.GetComponentInChildren<SkinnedMeshRenderer>(true);
                    if (renderer != null && renderer.sharedMesh != null)
                        return renderer.sharedMesh;
                }

                return null;
            }
        }

        [Serializable]
        private sealed class FaunaAssemblerReport
        {
            public string agentId;
            public string generatedUtc;
            public bool dryRun;
            public string meshDirectory;
            public string materialDirectory;
            public string outputDirectory;
            public int groupsDiscovered;
            public int prefabsSaved;
            public int swarmGroups;
            public bool gpuSkinningProjectSettingAutoEnabled;
            public int totalViolations;
            public int totalPrimitiveColliders;
            public int totalFineColliders;
            public int totalSkippedFineHitboxCandidates;
            public int totalBiolumPresentationLights;
            public int totalSkinnedRenderers;
            public int totalVatRenderers;
            public long elapsedMicroseconds;
            public FaunaAssemblyGroupReport[] groups;
        }

        [Serializable]
        private sealed class FaunaAssemblyGroupReport
        {
            public string groupName;
            public bool isSwarm;
            public bool saved;
            public bool assetDatabaseTouched;
            public bool gpuSkinningProjectSettingEnabled = true;
            public bool lodGroupConfigured;
            public string savedPath;
            public int inputAssetCount;
            public int skinnedRendererCount;
            public int vatRendererCount;
            public int primitiveColliderCount;
            public int fineColliderCount;
            public int skippedFineHitboxCandidateCount;
            public int physicsCullingColliderCount;
            public int biolumPresentationLightCount;
            public int maxBoneCount;
            public int brgMaterialPasses;
            public int violationCount;
            public long elapsedMicroseconds;
            public string[] violations;
        }
    }
}
#endif
