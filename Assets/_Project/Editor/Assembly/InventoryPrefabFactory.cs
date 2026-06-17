#if UNITY_EDITOR
namespace Hecton8.Editor.Assembly
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Globalization;
    using System.IO;
    using Hecton.Localization;
    using Hecton8.Inventory;
    using Hecton8.Items;
    using Hecton8.World;
    using UnityEditor;
    using UnityEngine;
    using UnityEngine.Rendering;
    using Debug = UnityEngine.Debug;
    using Object = UnityEngine.Object;

    public sealed class InventoryPrefabFactory : EditorWindow
    {
        private const string AgentId = "1739";
        private const string MenuRoot = "Hecton8/Assembly/1739/";
        private const string DefaultSourceDirectory = "Assets/_Project/BakedGeometry/Inventory";
        private const string DefaultMetadataDirectory = "Assets/_Project/BakedGeometry/Inventory/Metadata";
        private const string DefaultMaterialDirectory = "Assets/_Project/Art/Materials";
        private const string DefaultItemDataDirectory = "Assets/_Project/Data/Items";
        private const string DefaultOutputDirectory = "Assets/Prefabs/Items";
        private const string InteractableLayerName = "Interactable";
        private const string IkHandleName = "IK_Handle";
        private const string OpenAnchorName = "ANCHOR_Open";
        private const string LootAnchorName = "ANCHOR_Loot";
        private const string EmissionStrengthProperty = "_EmissionStrength";
        private const float DefaultAuthoredQualityWeight = 1f;
        private const float MinimumColliderSize = 0.05f;
        private const float CapsuleAspectThreshold = 2.05f;
        private const int MaxAcceptedPrefabHierarchyDepth = 8;
        private static readonly int EmissionStrengthId = Shader.PropertyToID(EmissionStrengthProperty);

        [SerializeField] private string sourceDirectory = DefaultSourceDirectory;
        [SerializeField] private string metadataDirectory = DefaultMetadataDirectory;
        [SerializeField] private string materialDirectory = DefaultMaterialDirectory;
        [SerializeField] private string itemDataDirectory = DefaultItemDataDirectory;
        [SerializeField] private string outputDirectory = DefaultOutputDirectory;
        [SerializeField] private float authoredQualityWeight = DefaultAuthoredQualityWeight;
        [SerializeField] private bool dryRun = true;
        [SerializeField] private int maxGroupsPerRun = 512;

        private Vector2 scroll;
        private FactoryReport lastReport;

        private static readonly List<SourceGroup> s_groups = new List<SourceGroup>(512);
        private static readonly List<ItemData> s_itemData = new List<ItemData>(1024);
        private static readonly List<MeshRenderer> s_meshRenderers = new List<MeshRenderer>(128);
        private static readonly List<Renderer> s_renderers = new List<Renderer>(128);
        private static readonly List<Collider> s_colliders = new List<Collider>(128);
        private static readonly List<MeshCollider> s_meshColliders = new List<MeshCollider>(16);
        private static readonly List<ParticleSystem> s_particleSystems = new List<ParticleSystem>(8);
        private static readonly List<LODGroup> s_lodGroups = new List<LODGroup>(8);
        private static readonly List<ItemNodeData> s_itemNodes = new List<ItemNodeData>(8);
        private static readonly List<ContainerMetadata> s_containerMetadata = new List<ContainerMetadata>(8);
        private static readonly List<InventoryEmissionStatePresenter> s_emissionPresenters = new List<InventoryEmissionStatePresenter>(8);
        private static readonly List<Material> s_materialCandidates = new List<Material>(512);
        private static readonly Dictionary<Shader, bool> s_shaderBatcherProof = new Dictionary<Shader, bool>(128);
        private static readonly LOD[] s_singleLodScratch = new LOD[1]; // COLD ALLOC: LOD[1] - editor prefab LODGroup SetLODs scratch - owner: InventoryPrefabFactory.
        private static readonly string[] s_singleFolderSearchScope = new string[1]; // COLD ALLOC: string[1] - AssetDatabase.FindAssets folder scope scratch - owner: InventoryPrefabFactory.

        [MenuItem(MenuRoot + "Open Inventory Prefab Factory", false, 1739)]
        public static void OpenWindow()
        {
            InventoryPrefabFactory window = GetWindow<InventoryPrefabFactory>("Inventory Factory 1739");
            window.minSize = new Vector2(760f, 540f);
            window.Show();
        }

        [MenuItem(MenuRoot + "Dry Run Static Audit", false, 1740)]
        public static void DryRunDefault()
        {
            FactorySettings settings = FactorySettings.Default;
            settings.DryRun = true;
            FactoryReport report = Run(settings);
            Debug.Log("[InventoryPrefabFactory1739] dryRun groups=" + report.sourceGroups.ToString(CultureInfo.InvariantCulture) +
                      " assembled=" + report.prefabsAssembled.ToString(CultureInfo.InvariantCulture) +
                      " failed=" + report.prefabsFailed.ToString(CultureInfo.InvariantCulture) +
                      " violations=" + report.violations.Count.ToString(CultureInfo.InvariantCulture));
        }

        [MenuItem(MenuRoot + "Run Factory", false, 1741)]
        public static void RunDefault()
        {
            FactorySettings settings = FactorySettings.Default;
            settings.DryRun = false;
            Run(settings);
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("HECTON-8 Inventory Prefab Factory 1739", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Offline assembler for loot, lockers, and containers. Bakes ItemNodeData, ContainerMetadata hinge axes, and primitive Interactable colliders.", MessageType.Info);

            sourceDirectory = EditorGUILayout.TextField("Source Mesh/Prefab Folder", sourceDirectory);
            metadataDirectory = EditorGUILayout.TextField("Metadata Folder", metadataDirectory);
            materialDirectory = EditorGUILayout.TextField("Material Folder", materialDirectory);
            itemDataDirectory = EditorGUILayout.TextField("ItemData Folder", itemDataDirectory);
            outputDirectory = EditorGUILayout.TextField("Prefab Output", outputDirectory);
            authoredQualityWeight = EditorGUILayout.Slider("GlobalQualityWeight", authoredQualityWeight, 0f, 1f);
            dryRun = EditorGUILayout.Toggle("Dry Run", dryRun);
            maxGroupsPerRun = EditorGUILayout.IntSlider("Max Groups", maxGroupsPerRun, 1, 4096);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Dry Run", GUILayout.Height(30f)))
                lastReport = Run(BuildSettings(true));
            if (GUILayout.Button("Assemble Prefabs", GUILayout.Height(30f)))
                lastReport = Run(BuildSettings(false));
            EditorGUILayout.EndHorizontal();

            if (lastReport == null)
                return;

            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.LabelField("Groups", lastReport.sourceGroups.ToString(CultureInfo.InvariantCulture));
            EditorGUILayout.LabelField("Existing Prefabs Audited", lastReport.existingPrefabsAudited.ToString(CultureInfo.InvariantCulture));
            EditorGUILayout.LabelField("Existing Prefab Violations", lastReport.existingPrefabViolations.ToString(CultureInfo.InvariantCulture));
            EditorGUILayout.LabelField("Existing MeshColliders", lastReport.existingPrefabMeshColliderViolations.ToString(CultureInfo.InvariantCulture));
            EditorGUILayout.LabelField("Existing Non-Primitive Colliders", lastReport.existingPrefabNonPrimitiveColliderViolations.ToString(CultureInfo.InvariantCulture));
            EditorGUILayout.LabelField("Existing Collider Layer Violations", lastReport.existingPrefabColliderLayerViolations.ToString(CultureInfo.InvariantCulture));
            EditorGUILayout.LabelField("Existing ParticleSystem Violations", lastReport.existingPrefabParticleSystemViolations.ToString(CultureInfo.InvariantCulture));
            EditorGUILayout.LabelField("Existing Material Slots Audited", lastReport.existingPrefabMaterialSlotsAudited.ToString(CultureInfo.InvariantCulture));
            EditorGUILayout.LabelField("Existing Material Violations", lastReport.existingPrefabMaterialViolations.ToString(CultureInfo.InvariantCulture));
            EditorGUILayout.LabelField("Existing Missing LOD Policy", lastReport.existingPrefabMissingLodPolicy.ToString(CultureInfo.InvariantCulture));
            EditorGUILayout.LabelField("Existing Missing Anchors", lastReport.existingPrefabMissingInteractionAnchor.ToString(CultureInfo.InvariantCulture));
            EditorGUILayout.LabelField("Existing Missing Container Metadata", lastReport.existingPrefabMissingContainerMetadata.ToString(CultureInfo.InvariantCulture));
            EditorGUILayout.LabelField("Assembled", lastReport.prefabsAssembled.ToString(CultureInfo.InvariantCulture));
            EditorGUILayout.LabelField("Failed", lastReport.prefabsFailed.ToString(CultureInfo.InvariantCulture));
            EditorGUILayout.LabelField("Item Nodes", lastReport.itemNodeBakes.ToString(CultureInfo.InvariantCulture));
            EditorGUILayout.LabelField("Container Metadata", lastReport.containerMetadataBakes.ToString(CultureInfo.InvariantCulture));
            EditorGUILayout.LabelField("Container Slot Maps", lastReport.containerSlotMapBakes.ToString(CultureInfo.InvariantCulture));
            EditorGUILayout.LabelField("Primitive Colliders", lastReport.primitiveColliderCount.ToString(CultureInfo.InvariantCulture));
            EditorGUILayout.LabelField("Collider Proxy Sources", lastReport.colliderProxySources.ToString(CultureInfo.InvariantCulture));
            EditorGUILayout.LabelField("Orphan Collider Proxies", lastReport.orphanColliderProxySources.ToString(CultureInfo.InvariantCulture));
            EditorGUILayout.LabelField("Mesh Colliders Rejected", lastReport.meshCollidersRejected.ToString(CultureInfo.InvariantCulture));
            EditorGUILayout.LabelField("BRG Materials Audited", lastReport.brgMaterialsAudited.ToString(CultureInfo.InvariantCulture));
            EditorGUILayout.LabelField("Emission Materials Verified", lastReport.emissionMaterialsVerified.ToString(CultureInfo.InvariantCulture));
            EditorGUILayout.LabelField("Emission Presenters", lastReport.emissionPresenterBakes.ToString(CultureInfo.InvariantCulture));
            EditorGUILayout.LabelField("Emission Renderer Bindings", lastReport.emissionRendererBindings.ToString(CultureInfo.InvariantCulture));
            EditorGUILayout.LabelField("Scavenge Targets", lastReport.scavengeTargetBakes.ToString(CultureInfo.InvariantCulture));
            EditorGUILayout.LabelField("Interaction Anchors", lastReport.interactionAnchorBakes.ToString(CultureInfo.InvariantCulture));
            EditorGUILayout.LabelField("LOD Policies", lastReport.lodPolicyBakes.ToString(CultureInfo.InvariantCulture));
            EditorGUILayout.LabelField("Collider Proxies", lastReport.colliderProxyBakes.ToString(CultureInfo.InvariantCulture));
            for (int i = 0; i < lastReport.violations.Count; i++)
                EditorGUILayout.LabelField(lastReport.violations[i]);
            EditorGUILayout.EndScrollView();
        }

        public static FactoryReport Run(FactorySettings settings)
        {
            settings = (settings ?? FactorySettings.Default).Sanitize();
            Stopwatch stopwatch = Stopwatch.StartNew();
            FactoryReport report = new FactoryReport
            {
                agentId = AgentId,
                generatedUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
                sourceDirectory = settings.SourceDirectory,
                metadataDirectory = settings.MetadataDirectory,
                materialDirectory = settings.MaterialDirectory,
                itemDataDirectory = settings.ItemDataDirectory,
                outputDirectory = settings.OutputDirectory,
                dryRun = settings.DryRun,
                authoredQualityWeight = settings.AuthoredQualityWeight
            };

            try
            {
                int interactableLayer = LayerMask.NameToLayer(InteractableLayerName);
                if (interactableLayer < 0)
                {
                    report.violations.Add("FATAL: Missing required layer: " + InteractableLayerName);
                    return FinalizeReport(report, stopwatch, settings);
                }

                if (!InventoryRoutingNetwork.RuntimeLayoutValid() ||
                    !ItemNodeData.ValidateStackLimitDtoLayout() ||
                    !ItemNodeData.ValidatePhysicalConstantsDtoLayout() ||
                    !ContainerMetadata.ValidateContainerRangeDtoLayout())
                {
                    report.violations.Add("FATAL: Inventory routing DTO layout validation failed.");
                    return FinalizeReport(report, stopwatch, settings);
                }

                LoadMaterialCandidates(settings.MaterialDirectory, report);
                Material material = ResolveFallbackMaterial(report);
                LoadItemData(settings.ItemDataDirectory, report);
                AuditExistingOutputPrefabs(settings.OutputDirectory, report);
                DiscoverSourceGroups(settings, report);
                report.sourceGroups = s_groups.Count;

                if (!settings.DryRun)
                    EnsureAssetFolder(settings.OutputDirectory);

                int count = Mathf.Min(s_groups.Count, settings.MaxGroupsPerRun);
                for (int i = 0; i < count; i++)
                {
                    PrefabMetric metric = BuildPrefab(s_groups[i], material, interactableLayer, settings, report);
                    report.prefabs.Add(metric);
                    if (string.Equals(metric.status, "PASS", StringComparison.Ordinal))
                        report.prefabsAssembled++;
                    else
                        report.prefabsFailed++;
                }

                return FinalizeReport(report, stopwatch, settings);
            }
            finally
            {
                s_groups.Clear();
                s_itemData.Clear();
                s_materialCandidates.Clear();
                s_shaderBatcherProof.Clear();
                ClearScratch();
            }
        }

        private FactorySettings BuildSettings(bool dryRunOverride)
        {
            return new FactorySettings
            {
                SourceDirectory = sourceDirectory,
                MetadataDirectory = metadataDirectory,
                MaterialDirectory = materialDirectory,
                ItemDataDirectory = itemDataDirectory,
                OutputDirectory = outputDirectory,
                AuthoredQualityWeight = authoredQualityWeight,
                DryRun = dryRunOverride,
                MaxGroupsPerRun = maxGroupsPerRun
            }.Sanitize();
        }

        private static PrefabMetric BuildPrefab(SourceGroup group, Material material, int interactableLayer, FactorySettings settings, FactoryReport report)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            InventoryMetadataFile metadata = LoadMetadata(settings.MetadataDirectory, group.Name, report);
            InventoryContainerKind kind = ResolveKind(group.Name, metadata);
            string prefabName = ResolvePrefabName(kind, group.Name);
            PrefabMetric metric = new PrefabMetric
            {
                prefabName = prefabName,
                sourcePath = group.SourcePath,
                metadataPath = metadata != null ? metadata.__sourcePath : string.Empty,
                outputPath = settings.OutputDirectory + "/" + prefabName + ".prefab",
                kind = kind.ToString()
            };

            GameObject root = null;
            try
            {
                ItemData itemData = ResolveItemData(group, metadata);
                int itemHash = ResolveItemHash(itemData, metadata);
                if (itemHash == 0)
                    return Fail(metric, report, "Missing item identity. Add ItemData reference/source or metadata itemHashId/itemStableId.");

                float baseWeightKg = ResolveBaseWeight(itemData, metadata);
                float baseVolumeM3 = ResolveBaseVolume(itemData, metadata);
                ushort stackCapacity = ResolveStackCapacity(itemData, metadata);
                ushort itemFlags = ResolveItemFlags(itemData, metadata);

                root = new GameObject(prefabName);
                ResetLocalTransform(root.transform);
                SetLayerRecursive(root, interactableLayer);

                ItemNodeData itemNode = root.AddComponent<ItemNodeData>();
                itemNode.ConfigureEditorBake(
                    itemHash,
                    baseWeightKg,
                    baseVolumeM3,
                    stackCapacity,
                    itemData != null ? (byte)itemData.category : (byte)0,
                    itemData != null ? (byte)itemData.resourceFamily : (byte)0,
                    itemFlags);
                if (!itemNode.TryBuildStackLimit(out InventoryStackLimitDTO stackLimit) || stackLimit.ItemHashID == 0u)
                    return Fail(metric, report, "ItemNodeData failed InventoryStackLimitDTO projection.");
                if (!itemNode.TryBuildPhysicalConstants(out ItemPhysicalConstantsDTO physicalConstants) || physicalConstants.ItemHash == 0u)
                    return Fail(metric, report, "ItemNodeData failed ItemPhysicalConstantsDTO projection.");
                metric.itemHashId = itemHash;
                metric.baseWeightKg = baseWeightKg;
                metric.slotCapacity = stackCapacity;
                report.itemNodeBakes++;

                Material groupMaterial = ResolveGroupMaterial(group, metadata, material);
                Transform visualRoot = CreateVisual(root.transform, group, groupMaterial, metadata, interactableLayer, metric, report);
                if (!string.IsNullOrEmpty(metric.failure))
                    return Fail(metric, report, metric.failure);
                EnsureLodPolicy(root, metric, report);
                StripCopiedColliders(root, report, metric);
                Bounds localBounds = ResolveLocalRendererBounds(root.transform, group);

                Transform lidTransform = ResolveLidTransform(root.transform, metadata);
                if (kind != InventoryContainerKind.Loot)
                {
                    BakeContainerMetadata(
                        root,
                        lidTransform,
                        localBounds,
                        metadata,
                        kind,
                        itemHash,
                        baseWeightKg,
                        settings.AuthoredQualityWeight,
                        metric,
                        report);
                    if (!string.IsNullOrEmpty(metric.failure))
                        return Fail(metric, report, metric.failure);
                }
                else
                {
                    EnsureInteractionAnchor(root.transform, LootAnchorName, localBounds.center, Vector3.up, Vector3.forward, interactableLayer, metric, report);
                }

                BindEmissionStatePresenter(root, group.Name, metadata, settings.AuthoredQualityWeight, metric, report);
                if (!string.IsNullOrEmpty(metric.failure))
                    return Fail(metric, report, metric.failure);

                BindScavengeTarget(root, kind, group.Name, metadata, itemHash, metric, report);
                AttachPrimitiveColliders(root.transform, group, localBounds, metadata, kind, interactableLayer, metric, report);
                ValidatePrefab(root, kind, metric, report);

                if (!string.IsNullOrEmpty(metric.failure))
                    return Fail(metric, report, metric.failure);

                if (!settings.DryRun)
                {
                    bool success;
                    GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(root, metric.outputPath, out success);
                    if (!success || savedPrefab == null)
                    {
                        DeleteInvalidPrefabAsset(metric.outputPath);
                        return Fail(metric, report, "PrefabUtility.SaveAsPrefabAsset returned null or false.");
                    }

                    ValidatePrefab(savedPrefab, kind, metric, report);
                    if (!string.IsNullOrEmpty(metric.failure))
                    {
                        DeleteInvalidPrefabAsset(metric.outputPath);
                        return Fail(metric, report, metric.failure);
                    }
                }

                metric.visualRootName = visualRoot != null ? visualRoot.name : string.Empty;
                metric.prefabHash = HashString(prefabName + "|" + itemHash.ToString(CultureInfo.InvariantCulture) + "|" + metric.colliderCount.ToString(CultureInfo.InvariantCulture));
                metric.editorMicroseconds = ElapsedMicroseconds(stopwatch);
                metric.status = "PASS";
                return metric;
            }
            catch (Exception exception)
            {
                return Fail(metric, report, exception.GetType().Name + ": " + exception.Message);
            }
            finally
            {
                if (root != null)
                    Object.DestroyImmediate(root);
            }
        }

        private static Transform CreateVisual(
            Transform root,
            SourceGroup group,
            Material material,
            InventoryMetadataFile metadata,
            int layer,
            PrefabMetric metric,
            FactoryReport report)
        {
            GameObject visualRoot = null;
            if (group.PrefabSource != null)
            {
                visualRoot = (GameObject)PrefabUtility.InstantiatePrefab(group.PrefabSource);
                if (visualRoot == null)
                    visualRoot = Object.Instantiate(group.PrefabSource);
                visualRoot.name = "VIS_" + SanitizeAssetName(group.Name);
                visualRoot.transform.SetParent(root, false);
                ResetLocalTransform(visualRoot.transform);
                SetLayerRecursive(visualRoot, layer);
            }
            else if (group.Mesh != null)
            {
                visualRoot = new GameObject("VIS_" + SanitizeAssetName(group.Name));
                visualRoot.transform.SetParent(root, false);
                ResetLocalTransform(visualRoot.transform);
                visualRoot.layer = layer;

                MeshFilter filter = visualRoot.AddComponent<MeshFilter>();
                filter.sharedMesh = group.Mesh;
                MeshRenderer renderer = visualRoot.AddComponent<MeshRenderer>();
                renderer.shadowCastingMode = ShadowCastingMode.On;
                renderer.receiveShadows = true;
                if (material != null)
                    renderer.sharedMaterial = material;
            }
            else
            {
                metric.failure = "No mesh or prefab source.";
                return null;
            }

            visualRoot.GetComponentsInChildren(true, s_renderers);
            metric.rendererCount = s_renderers.Count;
            if (s_renderers.Count == 0)
                report.violations.Add(visualRoot.name + ": source has no renderer; collider fallback will use mesh/default bounds.");
            s_renderers.Clear();
            ValidateRendererMaterials(visualRoot.transform, material, metadata, group.Name, metric, report);
            return visualRoot.transform;
        }

        private static void EnsureLodPolicy(GameObject root, PrefabMetric metric, FactoryReport report)
        {
            if (root == null)
                return;

            root.GetComponentsInChildren(true, s_lodGroups);
            if (s_lodGroups.Count > 0)
            {
                metric.lodGroupCount = s_lodGroups.Count;
                s_lodGroups.Clear();
                return;
            }
            s_lodGroups.Clear();

            root.GetComponentsInChildren(true, s_renderers);
            int rendererCount = s_renderers.Count;
            if (rendererCount == 0)
            {
                s_renderers.Clear();
                return;
            }

            Renderer[] rendererRefs = new Renderer[rendererCount]; // COLD ALLOC: serialized one-step LOD policy.
            for (int i = 0; i < rendererCount; i++)
                rendererRefs[i] = s_renderers[i];
            s_renderers.Clear();

            LODGroup lodGroup = root.AddComponent<LODGroup>();
            lodGroup.fadeMode = LODFadeMode.CrossFade;
            lodGroup.animateCrossFading = true;
            s_singleLodScratch[0] = new LOD(1f, rendererRefs);
            lodGroup.SetLODs(s_singleLodScratch);
            s_singleLodScratch[0] = default;
            lodGroup.RecalculateBounds();

            metric.lodGroupCount = 1;
            metric.lodPolicyBakes++;
            report.lodPolicyBakes++;
        }

        private static void ValidateRendererMaterials(
            Transform visualRoot,
            Material fallbackMaterial,
            InventoryMetadataFile metadata,
            string groupName,
            PrefabMetric metric,
            FactoryReport report)
        {
            bool requiresEmission = RequiresEmissionState(metadata, groupName);
            visualRoot.GetComponentsInChildren(true, s_meshRenderers);
            if (s_meshRenderers.Count == 0)
            {
                metric.failure = visualRoot.name + ": no MeshRenderer found for BRG audit.";
                s_meshRenderers.Clear();
                return;
            }

            for (int rendererIndex = 0; rendererIndex < s_meshRenderers.Count; rendererIndex++)
            {
                MeshRenderer renderer = s_meshRenderers[rendererIndex];
                if (renderer == null)
                    continue;

                Material[] materials = renderer.sharedMaterials;
                if ((materials == null || materials.Length == 0) && fallbackMaterial != null)
                {
                    renderer.sharedMaterial = fallbackMaterial;
                    materials = renderer.sharedMaterials;
                }

                if (materials == null || materials.Length == 0)
                {
                    metric.failure = renderer.name + ": MeshRenderer has no shared material.";
                    break;
                }

                bool reassignedNullSlot = false;
                for (int materialIndex = 0; materialIndex < materials.Length; materialIndex++)
                {
                    Material shared = materials[materialIndex];
                    if (shared == null && fallbackMaterial != null)
                    {
                        shared = fallbackMaterial;
                        materials[materialIndex] = shared;
                        reassignedNullSlot = true;
                    }

                    if (!ValidateSharedMaterial(shared, requiresEmission, out string failure))
                    {
                        metric.failure = renderer.name + ": " + failure;
                        break;
                    }

                    metric.brgMaterialsAudited++;
                    report.brgMaterialsAudited++;
                    if (requiresEmission)
                    {
                        metric.emissionMaterialsVerified++;
                        report.emissionMaterialsVerified++;
                    }
                }

                if (reassignedNullSlot)
                    renderer.sharedMaterials = materials;

                if (!string.IsNullOrEmpty(metric.failure))
                    break;
            }

            s_meshRenderers.Clear();
        }

        private static bool ValidateSharedMaterial(Material material, bool requiresEmission, out string failure)
        {
            failure = string.Empty;
            if (material == null)
            {
                failure = "material is null.";
                return false;
            }

            if (!AssetDatabase.Contains(material))
            {
                failure = material.name + " is not an asset-backed shared material.";
                return false;
            }

            if (material.shader == null || !HasSrpBatcherProof(material))
            {
                failure = material.name + " shader lacks CBUFFER_START(UnityPerMaterial) proof.";
                return false;
            }

            if (requiresEmission && !material.HasProperty(EmissionStrengthId))
            {
                failure = material.name + " lacks " + EmissionStrengthProperty + " for locked/sealed state presentation.";
                return false;
            }

            return true;
        }

        private static bool HasSrpBatcherProof(Material material)
        {
            if (material == null || material.shader == null)
                return false;

            Shader shader = material.shader;
            if (s_shaderBatcherProof.TryGetValue(shader, out bool cached))
                return cached;

            bool hasProof;
            string shaderName = shader.name;
            if (!string.IsNullOrEmpty(shaderName) &&
                (shaderName.IndexOf("Universal Render Pipeline/Lit", StringComparison.OrdinalIgnoreCase) >= 0 ||
                 shaderName.IndexOf("HDRP/Lit", StringComparison.OrdinalIgnoreCase) >= 0))
            {
                hasProof = true;
                s_shaderBatcherProof[shader] = hasProof;
                return hasProof;
            }

            string shaderPath = AssetDatabase.GetAssetPath(shader);
            if (string.IsNullOrEmpty(shaderPath))
            {
                s_shaderBatcherProof[shader] = false;
                return false;
            }
            if (shaderPath.EndsWith(".shadergraph", StringComparison.OrdinalIgnoreCase))
            {
                hasProof = true;
                s_shaderBatcherProof[shader] = hasProof;
                return hasProof;
            }
            if (!shaderPath.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
            {
                s_shaderBatcherProof[shader] = false;
                return false;
            }

            string fullPath = Path.GetFullPath(shaderPath);
            if (!File.Exists(fullPath))
            {
                s_shaderBatcherProof[shader] = false;
                return false;
            }

            string source = File.ReadAllText(fullPath);
            hasProof = source.IndexOf("CBUFFER_START(UnityPerMaterial)", StringComparison.Ordinal) >= 0;
            s_shaderBatcherProof[shader] = hasProof;
            return hasProof;
        }

        private static bool RequiresEmissionState(InventoryMetadataFile metadata, string groupName)
        {
            if (metadata != null && metadata.requiresEmission)
                return true;

            string normalized = NormalizeSearch(groupName);
            return normalized.IndexOf("lock", StringComparison.Ordinal) >= 0 ||
                   normalized.IndexOf("sealed", StringComparison.Ordinal) >= 0 ||
                   normalized.IndexOf("electronic", StringComparison.Ordinal) >= 0 ||
                   normalized.IndexOf("security", StringComparison.Ordinal) >= 0 ||
                   normalized.IndexOf("keypad", StringComparison.Ordinal) >= 0 ||
                   normalized.IndexOf("status", StringComparison.Ordinal) >= 0;
        }

        private static void BindEmissionStatePresenter(
            GameObject root,
            string groupName,
            InventoryMetadataFile metadata,
            float authoredGlobalQualityWeight,
            PrefabMetric metric,
            FactoryReport report)
        {
            if (!RequiresEmissionState(metadata, groupName))
                return;

            root.GetComponentsInChildren(true, s_meshRenderers);
            int rendererCount = s_meshRenderers.Count;
            if (rendererCount == 0)
            {
                metric.failure = "Emission state asset has no MeshRenderer bindings.";
                s_meshRenderers.Clear();
                return;
            }

            MeshRenderer[] rendererRefs = new MeshRenderer[rendererCount]; // COLD ALLOC: serialized emission presenter renderer bindings.
            for (int i = 0; i < rendererCount; i++)
                rendererRefs[i] = s_meshRenderers[i];
            s_meshRenderers.Clear();

            InventoryEmissionStatePresenter presenter = root.AddComponent<InventoryEmissionStatePresenter>();
            presenter.ConfigureEditorBake(
                rendererRefs,
                authoredGlobalQualityWeight,
                ResolveEmissionBaseStrength(metadata),
                ResolveEmissionPulseStrength(metadata),
                ResolveEmissionPulseFrequency(metadata),
                ResolveEmissionMinimumQuality(metadata));

            metric.emissionPresenterBakes++;
            metric.emissionRendererBindings += rendererCount;
            report.emissionPresenterBakes++;
            report.emissionRendererBindings += rendererCount;
        }

        private static void StripCopiedColliders(GameObject root, FactoryReport report, PrefabMetric metric)
        {
            root.GetComponentsInChildren(true, s_colliders);
            for (int i = s_colliders.Count - 1; i >= 0; i--)
            {
                Collider collider = s_colliders[i];
                if (collider == null)
                    continue;

                if (collider is MeshCollider)
                    report.meshCollidersRejected++;
                else
                    report.sourcePrimitiveCollidersStripped++;

                Object.DestroyImmediate(collider);
                metric.sourceCollidersStripped++;
            }

            s_colliders.Clear();
        }

        private static void BakeContainerMetadata(
            GameObject root,
            Transform lidTransform,
            Bounds localBounds,
            InventoryMetadataFile metadata,
            InventoryContainerKind kind,
            int itemHash,
            float baseWeightKg,
            float globalQualityWeight,
            PrefabMetric metric,
            FactoryReport report)
        {
            Vector3 axis = ResolveLidAxis(localBounds, metadata, kind);
            Vector3 pivot = ResolveLidPivot(localBounds, axis, metadata, kind);
            Vector3 forward = ResolveClosedForward(axis, metadata);
            float capacityKg = metadata != null && IsFinitePositive(metadata.capacityWeightKg)
                ? metadata.capacityWeightKg
                : Mathf.Max(baseWeightKg * 8f, kind == InventoryContainerKind.Locker ? 80f : 20f);
            ushort slots = metadata != null && metadata.slotCapacity > 0
                ? (ushort)Mathf.Clamp(metadata.slotCapacity, 1, ushort.MaxValue)
                : (ushort)(kind == InventoryContainerKind.Locker ? 16 : 8);
            int[] slotConnectivity = ResolveSlotConnectivity(metadata, slots); // COLD ALLOC: serialized container slot map.

            Transform ikHandle = CreateIkHandle(root.transform, pivot, axis, forward, root.layer);
            ContainerMetadata containerMetadata = root.AddComponent<ContainerMetadata>();
            containerMetadata.ConfigureEditorBake(
                HashString(root.name),
                HashString(root.name + "|" + itemHash.ToString(CultureInfo.InvariantCulture) + "|" + FormatVectorR(axis)),
                itemHash,
                kind,
                lidTransform,
                ikHandle,
                pivot,
                axis,
                forward,
                metadata != null && IsFinite(metadata.minOpenDegrees) ? metadata.minOpenDegrees : 0f,
                metadata != null && IsFinite(metadata.maxOpenDegrees) && metadata.maxOpenDegrees > 1f ? metadata.maxOpenDegrees : 95f,
                baseWeightKg,
                capacityKg,
                slots,
                slotConnectivity,
                metadata != null ? (byte)Mathf.Clamp(metadata.flags, 0, byte.MaxValue) : (byte)0,
                globalQualityWeight);
            if (!containerMetadata.TryBuildContainerRange(0, 0UL, 0, out InventoryContainerRangeDTO range) ||
                range.ContainerHash == 0UL ||
                range.SlotCapacity <= 0)
            {
                metric.failure = "ContainerMetadata failed InventoryContainerRangeDTO projection.";
                return;
            }

            metric.lidAxis = axis;
            metric.lidPivot = pivot;
            metric.slotCapacity = slots;
            metric.slotConnectivityCount = slotConnectivity.Length;
            metric.slotConnectivityHash = HashSlotConnectivity(slotConnectivity);
            metric.capacityWeightKg = capacityKg;
            metric.ikHandleBakes++;
            EnsureInteractionAnchor(root.transform, OpenAnchorName, pivot, axis, forward, root.layer, metric, report);
            report.containerMetadataBakes++;
            report.containerSlotMapBakes++;
        }

        private static Transform CreateIkHandle(Transform root, Vector3 localPivot, Vector3 localAxis, Vector3 localForward, int layer)
        {
            Transform handle = FindChildByName(root, IkHandleName);
            if (handle == null)
                handle = new GameObject(IkHandleName).transform;

            handle.gameObject.layer = layer;
            handle.SetParent(root, false);
            handle.localPosition = IsFiniteVector(localPivot) ? localPivot : Vector3.zero;
            handle.localRotation = BuildHandleRotation(localAxis, localForward);
            handle.localScale = Vector3.one;
            return handle;
        }

        private static Transform EnsureInteractionAnchor(
            Transform root,
            string anchorName,
            Vector3 localPosition,
            Vector3 localAxis,
            Vector3 localForward,
            int layer,
            PrefabMetric metric,
            FactoryReport report)
        {
            Transform existing = FindChildByName(root, anchorName);
            if (existing != null)
            {
                existing.gameObject.layer = layer;
                existing.SetParent(root, false);
                existing.localPosition = SanitizeVector(localPosition, Vector3.zero);
                existing.localRotation = BuildHandleRotation(localAxis, localForward);
                existing.localScale = Vector3.one;
                metric.interactionAnchorCount++;
                return existing;
            }

            GameObject anchor = new GameObject(anchorName);
            anchor.layer = layer;
            anchor.transform.SetParent(root, false);
            anchor.transform.localPosition = SanitizeVector(localPosition, Vector3.zero);
            anchor.transform.localRotation = BuildHandleRotation(localAxis, localForward);
            anchor.transform.localScale = Vector3.one;

            metric.interactionAnchorCount++;
            metric.interactionAnchorBakes++;
            report.interactionAnchorBakes++;
            return anchor.transform;
        }

        private static Quaternion BuildHandleRotation(Vector3 localAxis, Vector3 localForward)
        {
            Vector3 up = IsFiniteVector(localAxis) && localAxis.sqrMagnitude > 0.000001f
                ? localAxis.normalized
                : Vector3.up;
            Vector3 forward = IsFiniteVector(localForward) && localForward.sqrMagnitude > 0.000001f
                ? localForward
                : Vector3.forward;

            forward -= up * Vector3.Dot(forward, up);
            if (forward.sqrMagnitude <= 0.000001f)
            {
                forward = Mathf.Abs(Vector3.Dot(up, Vector3.forward)) < 0.85f ? Vector3.forward : Vector3.right;
                forward -= up * Vector3.Dot(forward, up);
            }

            return Quaternion.LookRotation(forward.normalized, up);
        }

        private static void AttachPrimitiveColliders(
            Transform root,
            SourceGroup group,
            Bounds localBounds,
            InventoryMetadataFile metadata,
            InventoryContainerKind kind,
            int layer,
            PrefabMetric metric,
            FactoryReport report)
        {
            if (metadata != null && metadata.colliders != null && metadata.colliders.Length > 0)
            {
                for (int i = 0; i < metadata.colliders.Length; i++)
                {
                    if (TryAttachAuthoredCollider(root, metadata.colliders[i], layer, metric, report))
                        continue;

                    report.violations.Add(root.name + ": rejected authored collider index " + i.ToString(CultureInfo.InvariantCulture) + "; expected Box or Capsule.");
                }
            }

            if (metric.colliderCount > 0)
                return;

            if (group != null && group.ColliderPrefab != null)
            {
                if (TryAttachColliderProxyPrefab(root, group, layer, metric, report))
                    return;
                if (!string.IsNullOrEmpty(metric.failure))
                    return;
            }

            if (group != null && group.ColliderMesh != null)
            {
                AttachColliderProxyMesh(root, group, kind, layer, metric, report);
                return;
            }

            Vector3 size = SanitizeSize(localBounds.size);
            if (kind == InventoryContainerKind.Loot && ShouldUseCapsule(size))
                AttachCapsule(root, "COL_" + SanitizeAssetName(root.name) + "_Capsule", localBounds.center, size, layer, metric, report);
            else
                AttachBox(root, "COL_" + SanitizeAssetName(root.name) + "_Box", localBounds.center, size, layer, metric, report);
        }

        private static bool TryAttachColliderProxyPrefab(
            Transform root,
            SourceGroup group,
            int layer,
            PrefabMetric metric,
            FactoryReport report)
        {
            GameObject proxyRoot = (GameObject)PrefabUtility.InstantiatePrefab(group.ColliderPrefab);
            if (proxyRoot == null)
                proxyRoot = Object.Instantiate(group.ColliderPrefab);
            if (proxyRoot == null)
            {
                metric.failure = "Collider proxy prefab could not be instantiated: " + group.ColliderProxyPath;
                return false;
            }

            int initialColliderCount = metric.colliderCount;
            try
            {
                proxyRoot.transform.SetParent(root, false);
                ResetLocalTransform(proxyRoot.transform);
                SetLayerRecursive(proxyRoot, layer);

                proxyRoot.GetComponentsInChildren(true, s_colliders);
                if (s_colliders.Count == 0)
                {
                    metric.failure = "Collider proxy prefab has no Box/Capsule colliders: " + group.ColliderProxyPath;
                    return false;
                }

                for (int i = 0; i < s_colliders.Count; i++)
                {
                    Collider collider = s_colliders[i];
                    if (collider == null)
                        continue;

                    BoxCollider box = collider as BoxCollider;
                    if (box != null)
                    {
                        AttachProxyBox(root, box, layer, metric, report);
                        continue;
                    }

                    CapsuleCollider capsule = collider as CapsuleCollider;
                    if (capsule != null)
                    {
                        AttachProxyCapsule(root, capsule, layer, metric, report);
                        continue;
                    }

                    if (collider is MeshCollider)
                        report.meshCollidersRejected++;

                    metric.failure = collider.name + ": collider proxy rejected; expected BoxCollider or CapsuleCollider.";
                    return false;
                }

                if (metric.colliderCount <= initialColliderCount)
                {
                    metric.failure = "Collider proxy prefab produced no usable primitive colliders: " + group.ColliderProxyPath;
                    return false;
                }

                metric.colliderProxyPath = group.ColliderProxyPath;
                metric.colliderProxyBakes++;
                report.colliderProxyBakes++;
                return true;
            }
            finally
            {
                s_colliders.Clear();
                Object.DestroyImmediate(proxyRoot);
            }
        }

        private static void AttachColliderProxyMesh(
            Transform root,
            SourceGroup group,
            InventoryContainerKind kind,
            int layer,
            PrefabMetric metric,
            FactoryReport report)
        {
            Bounds bounds = group.ColliderMesh.bounds;
            Vector3 size = SanitizeSize(bounds.size);
            if (kind == InventoryContainerKind.Loot && ShouldUseCapsule(size))
                AttachCapsule(root, "COL_" + SanitizeAssetName(group.Name) + "_ProxyCapsule", bounds.center, size, layer, metric, report);
            else
                AttachBox(root, "COL_" + SanitizeAssetName(group.Name) + "_ProxyBox", bounds.center, size, layer, metric, report);

            metric.colliderProxyPath = group.ColliderProxyPath;
            metric.colliderProxyBakes++;
            report.colliderProxyBakes++;
        }

        private static void AttachProxyBox(
            Transform root,
            BoxCollider source,
            int layer,
            PrefabMetric metric,
            FactoryReport report)
        {
            GameObject child = CreateColliderChild(root, source.name, layer);
            CopyProxyColliderTransform(child.transform, root, source.transform);
            BoxCollider box = child.AddComponent<BoxCollider>();
            box.center = SanitizeVector(source.center, Vector3.zero);
            box.size = SanitizeSize(source.size);
            box.isTrigger = source.isTrigger;
            metric.colliderCount++;
            report.primitiveColliderCount++;
        }

        private static void AttachProxyCapsule(
            Transform root,
            CapsuleCollider source,
            int layer,
            PrefabMetric metric,
            FactoryReport report)
        {
            GameObject child = CreateColliderChild(root, source.name, layer);
            CopyProxyColliderTransform(child.transform, root, source.transform);
            CapsuleCollider capsule = child.AddComponent<CapsuleCollider>();
            capsule.center = SanitizeVector(source.center, Vector3.zero);
            capsule.direction = source.direction >= 0 && source.direction <= 2 ? source.direction : 1;
            capsule.radius = Mathf.Max(MinimumColliderSize * 0.5f, IsFinitePositive(source.radius) ? source.radius : MinimumColliderSize * 0.5f);
            capsule.height = Mathf.Max(capsule.radius * 2f, IsFinitePositive(source.height) ? source.height : capsule.radius * 2f);
            capsule.isTrigger = source.isTrigger;
            metric.colliderCount++;
            report.primitiveColliderCount++;
        }

        private static void CopyProxyColliderTransform(Transform target, Transform root, Transform source)
        {
            if (target == null || root == null || source == null)
                return;

            target.localPosition = SanitizeVector(root.InverseTransformPoint(source.position), Vector3.zero);
            target.localRotation = SanitizeQuaternion(Quaternion.Inverse(root.rotation) * source.rotation);
            target.localScale = SanitizeScale(source.lossyScale);
        }

        private static void BindScavengeTarget(
            GameObject root,
            InventoryContainerKind kind,
            string groupName,
            InventoryMetadataFile metadata,
            int itemHash,
            PrefabMetric metric,
            FactoryReport report)
        {
            if (!ShouldAttachScavengeTarget(kind, groupName, metadata))
                return;

            int harvestUnits = metadata != null && metadata.harvestUnits > 0 ? metadata.harvestUnits : 1;
            ScavengeTarget target = root.AddComponent<ScavengeTarget>();
            target.ConfigureForEditor(itemHash, harvestUnits);
            metric.scavengeTargetBakes++;
            report.scavengeTargetBakes++;
        }

        private static bool ShouldAttachScavengeTarget(InventoryContainerKind kind, string groupName, InventoryMetadataFile metadata)
        {
            if (kind != InventoryContainerKind.Loot)
                return false;
            if (metadata != null && metadata.harvestUnits > 0)
                return true;

            string normalized = NormalizeSearch(groupName);
            return normalized.IndexOf("resource", StringComparison.Ordinal) >= 0 ||
                   normalized.IndexOf("node", StringComparison.Ordinal) >= 0 ||
                   normalized.IndexOf("ore", StringComparison.Ordinal) >= 0 ||
                   normalized.IndexOf("salvage", StringComparison.Ordinal) >= 0 ||
                   normalized.IndexOf("deposit", StringComparison.Ordinal) >= 0;
        }

        private static bool TryAttachAuthoredCollider(
            Transform root,
            ColliderDescriptor descriptor,
            int layer,
            PrefabMetric metric,
            FactoryReport report)
        {
            if (descriptor == null || string.IsNullOrWhiteSpace(descriptor.type))
                return false;

            if (descriptor.type.Equals("Box", StringComparison.OrdinalIgnoreCase) ||
                descriptor.type.Equals("BoxCollider", StringComparison.OrdinalIgnoreCase))
            {
                AttachBox(root, descriptor.name, SanitizeVector(descriptor.center, Vector3.zero), SanitizeSize(descriptor.size), layer, metric, report);
                return true;
            }

            if (descriptor.type.Equals("Capsule", StringComparison.OrdinalIgnoreCase) ||
                descriptor.type.Equals("CapsuleCollider", StringComparison.OrdinalIgnoreCase))
            {
                Vector3 size = SanitizeSize(descriptor.size);
                int direction = descriptor.direction >= 0 && descriptor.direction <= 2
                    ? descriptor.direction
                    : ResolveLongestAxis(size);
                GameObject child = CreateColliderChild(root, descriptor.name, layer);
                CapsuleCollider capsule = child.AddComponent<CapsuleCollider>();
                capsule.center = SanitizeVector(descriptor.center, Vector3.zero);
                capsule.direction = direction;
                capsule.radius = IsFinitePositive(descriptor.radius)
                    ? descriptor.radius
                    : ResolveCapsuleRadius(size, direction);
                capsule.height = Mathf.Max(capsule.radius * 2f, IsFinitePositive(descriptor.height) ? descriptor.height : ResolveAxisSize(size, direction));
                capsule.isTrigger = descriptor.isTrigger;
                metric.colliderCount++;
                report.primitiveColliderCount++;
                return true;
            }

            return false;
        }

        private static void AttachBox(
            Transform root,
            string name,
            Vector3 center,
            Vector3 size,
            int layer,
            PrefabMetric metric,
            FactoryReport report)
        {
            GameObject child = CreateColliderChild(root, name, layer);
            BoxCollider box = child.AddComponent<BoxCollider>();
            box.center = SanitizeVector(center, Vector3.zero);
            box.size = SanitizeSize(size);
            metric.colliderCount++;
            report.primitiveColliderCount++;
        }

        private static void AttachCapsule(
            Transform root,
            string name,
            Vector3 center,
            Vector3 size,
            int layer,
            PrefabMetric metric,
            FactoryReport report)
        {
            int direction = ResolveLongestAxis(size);
            GameObject child = CreateColliderChild(root, name, layer);
            CapsuleCollider capsule = child.AddComponent<CapsuleCollider>();
            capsule.center = SanitizeVector(center, Vector3.zero);
            capsule.direction = direction;
            capsule.radius = ResolveCapsuleRadius(size, direction);
            capsule.height = Mathf.Max(capsule.radius * 2f, ResolveAxisSize(size, direction));
            metric.colliderCount++;
            report.primitiveColliderCount++;
        }

        private static GameObject CreateColliderChild(Transform root, string authoredName, int layer)
        {
            string safeName = string.IsNullOrWhiteSpace(authoredName) ? "COL_Primitive" : SanitizeAssetName(authoredName);
            if (!safeName.StartsWith("COL_", StringComparison.OrdinalIgnoreCase))
                safeName = "COL_" + safeName;

            GameObject child = new GameObject(safeName);
            child.layer = layer;
            child.transform.SetParent(root, false);
            ResetLocalTransform(child.transform);
            return child;
        }

        private static void ValidatePrefab(GameObject root, InventoryContainerKind kind, PrefabMetric metric, FactoryReport report)
        {
            root.GetComponentsInChildren(true, s_particleSystems);
            if (s_particleSystems.Count > 0)
                SetFailureIfEmpty(metric, "Final prefab contains ParticleSystem count=" + s_particleSystems.Count.ToString(CultureInfo.InvariantCulture));
            s_particleSystems.Clear();

            root.GetComponentsInChildren(true, s_meshColliders);
            if (s_meshColliders.Count > 0)
            {
                report.meshCollidersRejected += s_meshColliders.Count;
                SetFailureIfEmpty(metric, "Final prefab contains MeshCollider count=" + s_meshColliders.Count.ToString(CultureInfo.InvariantCulture));
            }
            s_meshColliders.Clear();

            root.GetComponentsInChildren(true, s_itemNodes);
            if (s_itemNodes.Count != 1 || !s_itemNodes[0].IsValid)
                SetFailureIfEmpty(metric, "Expected exactly one valid ItemNodeData.");
            s_itemNodes.Clear();

            if (kind != InventoryContainerKind.Loot)
            {
                root.GetComponentsInChildren(true, s_containerMetadata);
                ContainerMetadata metadata = s_containerMetadata.Count == 1 ? s_containerMetadata[0] : null;
                if (metadata == null ||
                    !metadata.IsValid ||
                    metadata.IkHandle == null ||
                    !string.Equals(metadata.IkHandle.name, IkHandleName, StringComparison.Ordinal) ||
                    !metadata.TryGetLidAxis(out Vector3 axis) ||
                    !IsFiniteVector(axis))
                {
                    SetFailureIfEmpty(metric, "Expected exactly one valid ContainerMetadata with baked lid axis and IK_Handle.");
                }
                s_containerMetadata.Clear();
            }

            root.GetComponentsInChildren(true, s_colliders);
            int primitiveCount = 0;
            for (int i = 0; i < s_colliders.Count; i++)
            {
                Collider collider = s_colliders[i];
                if (collider.gameObject.layer != root.layer)
                    SetFailureIfEmpty(metric, collider.name + ": collider layer is not Interactable.");

                if (collider is BoxCollider || collider is CapsuleCollider)
                    primitiveCount++;
                else
                    SetFailureIfEmpty(metric, collider.name + ": non Box/Capsule collider rejected.");
            }

            if (primitiveCount == 0)
                SetFailureIfEmpty(metric, "No primitive Box/Capsule collider found.");

            s_colliders.Clear();

            root.GetComponentsInChildren(true, s_renderers);
            bool hasRenderers = s_renderers.Count > 0;
            s_renderers.Clear();

            root.GetComponentsInChildren(true, s_lodGroups);
            metric.lodGroupCount = s_lodGroups.Count;
            if (hasRenderers && s_lodGroups.Count == 0)
                SetFailureIfEmpty(metric, "Final prefab has renderers but no LODGroup policy.");
            s_lodGroups.Clear();

            string requiredAnchorName = kind == InventoryContainerKind.Loot ? LootAnchorName : OpenAnchorName;
            if (FindChildByName(root.transform, requiredAnchorName) == null)
                SetFailureIfEmpty(metric, "Missing required interaction anchor: " + requiredAnchorName);

            root.GetComponentsInChildren(true, s_emissionPresenters);
            if (s_emissionPresenters.Count > 1)
            {
                SetFailureIfEmpty(metric, "Expected at most one InventoryEmissionStatePresenter.");
            }
            else if (s_emissionPresenters.Count == 1 && !s_emissionPresenters[0].HasValidBinding)
            {
                SetFailureIfEmpty(metric, "InventoryEmissionStatePresenter has invalid serialized emission binding.");
            }
            s_emissionPresenters.Clear();
        }

        private static void AuditExistingOutputPrefabs(string outputDirectory, FactoryReport report)
        {
            if (report == null || string.IsNullOrWhiteSpace(outputDirectory) || !AssetDatabase.IsValidFolder(outputDirectory))
                return;

            string[] guids = FindAssetsInFolder("t:Prefab", outputDirectory);
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null)
                    continue;

                report.existingPrefabsAudited++;
                int before = report.existingPrefabViolations;
                AuditExistingPrefab(prefab, path, report);
                if (report.existingPrefabViolations > before)
                    report.violations.Add(path + ": existing prefab fails inventory assembly audit.");
            }
        }

        private static string[] FindAssetsInFolder(string filter, string folder)
        {
            s_singleFolderSearchScope[0] = folder;
            try
            {
                return AssetDatabase.FindAssets(filter, s_singleFolderSearchScope);
            }
            finally
            {
                s_singleFolderSearchScope[0] = null;
            }
        }

        private static void AuditExistingPrefab(GameObject prefab, string path, FactoryReport report)
        {
            InventoryContainerKind kind = ResolveKind(prefab.name, null);
            int interactableLayer = LayerMask.NameToLayer(InteractableLayerName);

            prefab.GetComponentsInChildren(true, s_particleSystems);
            if (s_particleSystems.Count > 0)
            {
                report.existingPrefabParticleSystemViolations += s_particleSystems.Count;
                report.existingPrefabViolations++;
            }
            s_particleSystems.Clear();

            prefab.GetComponentsInChildren(true, s_meshColliders);
            if (s_meshColliders.Count > 0)
            {
                report.existingPrefabMeshColliderViolations += s_meshColliders.Count;
                report.existingPrefabViolations++;
            }
            s_meshColliders.Clear();

            prefab.GetComponentsInChildren(true, s_itemNodes);
            if (s_itemNodes.Count != 1 || !s_itemNodes[0].IsValid)
            {
                report.existingPrefabMissingItemNode++;
                report.existingPrefabViolations++;
            }
            s_itemNodes.Clear();

            prefab.GetComponentsInChildren(true, s_colliders);
            int primitiveCount = 0;
            int nonPrimitiveCount = 0;
            int invalidLayerCount = 0;
            for (int i = 0; i < s_colliders.Count; i++)
            {
                Collider collider = s_colliders[i];
                if (collider is BoxCollider || collider is CapsuleCollider)
                {
                    primitiveCount++;
                }
                else
                {
                    nonPrimitiveCount++;
                }

                if (interactableLayer >= 0 && collider.gameObject.layer != interactableLayer)
                    invalidLayerCount++;
            }

            if (nonPrimitiveCount > 0)
            {
                report.existingPrefabNonPrimitiveColliderViolations += nonPrimitiveCount;
                report.existingPrefabViolations++;
            }

            if (invalidLayerCount > 0)
            {
                report.existingPrefabColliderLayerViolations += invalidLayerCount;
                report.existingPrefabViolations++;
            }

            if (primitiveCount == 0)
            {
                report.existingPrefabMissingPrimitiveCollider++;
                report.existingPrefabViolations++;
            }
            s_colliders.Clear();

            prefab.GetComponentsInChildren(true, s_renderers);
            bool hasRenderers = s_renderers.Count > 0;
            s_renderers.Clear();

            prefab.GetComponentsInChildren(true, s_lodGroups);
            if (hasRenderers && s_lodGroups.Count == 0)
            {
                report.existingPrefabMissingLodPolicy++;
                report.existingPrefabViolations++;
            }
            s_lodGroups.Clear();

            AuditExistingRendererMaterials(prefab, report);

            string requiredAnchorName = kind == InventoryContainerKind.Loot ? LootAnchorName : OpenAnchorName;
            if (FindChildByName(prefab.transform, requiredAnchorName) == null)
            {
                report.existingPrefabMissingInteractionAnchor++;
                report.existingPrefabViolations++;
            }

            if (kind != InventoryContainerKind.Loot)
            {
                prefab.GetComponentsInChildren(true, s_containerMetadata);
                ContainerMetadata metadata = s_containerMetadata.Count == 1 ? s_containerMetadata[0] : null;
                if (metadata == null ||
                    !metadata.IsValid ||
                    metadata.IkHandle == null ||
                    !metadata.TryGetLidAxis(out Vector3 axis) ||
                    !IsFiniteVector(axis))
                {
                    report.existingPrefabMissingContainerMetadata++;
                    report.existingPrefabViolations++;
                }
                s_containerMetadata.Clear();
            }

            int depth = MeasureHierarchyDepth(prefab.transform, 0);
            if (depth > MaxAcceptedPrefabHierarchyDepth)
            {
                report.existingPrefabDeepHierarchyViolations++;
                report.existingPrefabViolations++;
            }

            if (RequiresEmissionState(null, prefab.name))
            {
                prefab.GetComponentsInChildren(true, s_emissionPresenters);
                if (s_emissionPresenters.Count != 1 || !s_emissionPresenters[0].HasValidBinding)
                {
                    report.existingPrefabEmissionBindingViolations++;
                    report.existingPrefabViolations++;
                }
                s_emissionPresenters.Clear();
            }
        }

        private static void AuditExistingRendererMaterials(GameObject prefab, FactoryReport report)
        {
            bool requiresEmission = RequiresEmissionState(null, prefab.name);
            prefab.GetComponentsInChildren(true, s_meshRenderers);
            for (int rendererIndex = 0; rendererIndex < s_meshRenderers.Count; rendererIndex++)
            {
                MeshRenderer renderer = s_meshRenderers[rendererIndex];
                if (renderer == null)
                    continue;

                Material[] materials = renderer.sharedMaterials;
                if (materials == null || materials.Length == 0)
                {
                    report.existingPrefabMaterialViolations++;
                    report.existingPrefabViolations++;
                    continue;
                }

                bool rendererFailed = false;
                for (int materialIndex = 0; materialIndex < materials.Length; materialIndex++)
                {
                    Material shared = materials[materialIndex];
                    report.existingPrefabMaterialSlotsAudited++;
                    if (ValidateSharedMaterial(shared, requiresEmission, out _))
                        continue;

                    if (!rendererFailed)
                    {
                        report.existingPrefabMaterialViolations++;
                        report.existingPrefabViolations++;
                        rendererFailed = true;
                    }
                }
            }

            s_meshRenderers.Clear();
        }

        private static int MeasureHierarchyDepth(Transform root, int depth)
        {
            if (root == null)
                return depth;

            int max = depth;
            for (int i = 0; i < root.childCount; i++)
            {
                int childDepth = MeasureHierarchyDepth(root.GetChild(i), depth + 1);
                if (childDepth > max)
                    max = childDepth;
            }

            return max;
        }

        private static void DiscoverSourceGroups(FactorySettings settings, FactoryReport report)
        {
            s_groups.Clear();
            DiscoverMeshGroups(settings.SourceDirectory);
            DiscoverPrefabGroups(settings.SourceDirectory);
            DiscoverItemWorldPrefabs(report);
            DiscoverColliderProxyAssets(settings.SourceDirectory, report);
            ResolveGroupItemData();

            if (s_groups.Count == 0)
                report.violations.Add("No inventory source meshes/prefabs discovered in " + settings.SourceDirectory + " or ItemData worldPrefab fields.");
        }

        private static void DiscoverMeshGroups(string sourceDirectory)
        {
            if (!AssetDatabase.IsValidFolder(sourceDirectory))
                return;

            string[] guids = FindAssetsInFolder("t:Mesh", sourceDirectory);
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                string name = Path.GetFileNameWithoutExtension(path);
                if (IsCollisionAssetName(name) || IsNonPrimaryLodName(name))
                    continue;

                Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if (mesh == null)
                    continue;

                SourceGroup group = ResolveOrCreateGroup(CleanGroupName(name), path);
                if (group.Mesh == null)
                {
                    group.Mesh = mesh;
                    group.SourcePath = path;
                }
            }
        }

        private static void DiscoverColliderProxyAssets(string sourceDirectory, FactoryReport report)
        {
            if (!AssetDatabase.IsValidFolder(sourceDirectory))
                return;

            DiscoverColliderProxyPrefabs(sourceDirectory, report);
            DiscoverColliderProxyMeshes(sourceDirectory, report);
        }

        private static void DiscoverColliderProxyPrefabs(string sourceDirectory, FactoryReport report)
        {
            string[] guids = FindAssetsInFolder("t:Prefab", sourceDirectory);
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                string name = Path.GetFileNameWithoutExtension(path);
                if (!IsCollisionAssetName(name))
                    continue;

                SourceGroup group = ResolveExistingGroup(CleanColliderProxyGroupName(name));
                if (group == null)
                {
                    report.orphanColliderProxySources++;
                    continue;
                }

                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null)
                    continue;

                group.ColliderPrefab = prefab;
                group.ColliderProxyPath = path;
                report.colliderProxySources++;
            }
        }

        private static void DiscoverColliderProxyMeshes(string sourceDirectory, FactoryReport report)
        {
            string[] guids = FindAssetsInFolder("t:Mesh", sourceDirectory);
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                string name = Path.GetFileNameWithoutExtension(path);
                if (!IsCollisionAssetName(name))
                    continue;

                SourceGroup group = ResolveExistingGroup(CleanColliderProxyGroupName(name));
                if (group == null)
                {
                    report.orphanColliderProxySources++;
                    continue;
                }

                Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if (mesh == null)
                    continue;

                if (group.ColliderPrefab != null)
                    continue;

                group.ColliderMesh = mesh;
                group.ColliderProxyPath = path;
                report.colliderProxySources++;
            }
        }

        private static void DiscoverPrefabGroups(string sourceDirectory)
        {
            if (!AssetDatabase.IsValidFolder(sourceDirectory))
                return;

            string[] guids = FindAssetsInFolder("t:Prefab", sourceDirectory);
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                string name = Path.GetFileNameWithoutExtension(path);
                if (IsCollisionAssetName(name))
                    continue;

                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null)
                    continue;

                SourceGroup group = ResolveOrCreateGroup(CleanGroupName(name), path);
                group.PrefabSource = prefab;
                group.SourcePath = path;
            }
        }

        private static void DiscoverItemWorldPrefabs(FactoryReport report)
        {
            for (int i = 0; i < s_itemData.Count; i++)
            {
                ItemData item = s_itemData[i];
                if (item == null || item.worldPrefab == null)
                    continue;

                string path = AssetDatabase.GetAssetPath(item.worldPrefab);
                if (string.IsNullOrWhiteSpace(path))
                    continue;

                string name = CleanGroupName(item.worldPrefab.name);
                SourceGroup group = ResolveOrCreateGroup(name, path);
                if (group.PrefabSource == null)
                    group.PrefabSource = item.worldPrefab;
                if (string.IsNullOrWhiteSpace(group.SourcePath))
                    group.SourcePath = path;
                group.ItemData = item;
                report.itemWorldPrefabSources++;
            }
        }

        private static void LoadItemData(string itemDataDirectory, FactoryReport report)
        {
            s_itemData.Clear();
            if (!AssetDatabase.IsValidFolder(itemDataDirectory))
            {
                report.violations.Add("ItemData directory missing: " + itemDataDirectory);
                return;
            }

            string[] guids = FindAssetsInFolder("t:ItemData", itemDataDirectory);
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                ItemData item = AssetDatabase.LoadAssetAtPath<ItemData>(path);
                if (item != null)
                    s_itemData.Add(item);
            }

            report.itemDataAssets = s_itemData.Count;
        }

        private static SourceGroup ResolveOrCreateGroup(string groupName, string sourcePath)
        {
            string safe = SanitizeAssetName(groupName);
            for (int i = 0; i < s_groups.Count; i++)
            {
                if (string.Equals(s_groups[i].Name, safe, StringComparison.OrdinalIgnoreCase))
                    return s_groups[i];
            }

            SourceGroup group = new SourceGroup
            {
                Name = safe,
                SourcePath = sourcePath
            };
            s_groups.Add(group);
            return group;
        }

        private static SourceGroup ResolveExistingGroup(string groupName)
        {
            string safe = SanitizeAssetName(groupName);
            for (int i = 0; i < s_groups.Count; i++)
            {
                if (string.Equals(s_groups[i].Name, safe, StringComparison.OrdinalIgnoreCase))
                    return s_groups[i];
            }

            return null;
        }

        private static void ResolveGroupItemData()
        {
            for (int i = 0; i < s_groups.Count; i++)
            {
                SourceGroup group = s_groups[i];
                if (group.ItemData != null)
                    continue;

                string normalizedGroup = NormalizeSearch(group.Name);
                ItemData best = null;
                int bestScore = int.MinValue;
                for (int j = 0; j < s_itemData.Count; j++)
                {
                    ItemData item = s_itemData[j];
                    if (item == null)
                        continue;

                    int score = ScoreItemMatch(normalizedGroup, item);
                    if (score > bestScore)
                    {
                        best = item;
                        bestScore = score;
                    }
                }

                if (bestScore > 0)
                    group.ItemData = best;
            }
        }

        private static int ScoreItemMatch(string normalizedGroup, ItemData item)
        {
            string itemName = NormalizeSearch(item.name);
            string persistent = NormalizeSearch(item.PersistentId);
            int score = 0;
            if (string.Equals(normalizedGroup, itemName, StringComparison.Ordinal))
                score += 100;
            if (string.Equals(normalizedGroup, persistent, StringComparison.Ordinal))
                score += 120;
            if (normalizedGroup.Contains(itemName))
                score += 20;
            if (normalizedGroup.Contains(persistent))
                score += 24;
            return score;
        }

        private static ItemData ResolveItemData(SourceGroup group, InventoryMetadataFile metadata)
        {
            if (metadata != null)
            {
                if (!string.IsNullOrWhiteSpace(metadata.itemStableId))
                {
                    for (int i = 0; i < s_itemData.Count; i++)
                    {
                        ItemData item = s_itemData[i];
                        if (item != null &&
                            string.Equals(item.PersistentId, metadata.itemStableId, StringComparison.OrdinalIgnoreCase))
                        {
                            return item;
                        }
                    }
                }

                if (metadata.itemHashId != 0)
                {
                    for (int i = 0; i < s_itemData.Count; i++)
                    {
                        ItemData item = s_itemData[i];
                        if (item != null && ResolvePersistentHash(item) == metadata.itemHashId)
                            return item;
                    }
                }
            }

            return group.ItemData;
        }

        private static int ResolveItemHash(ItemData itemData, InventoryMetadataFile metadata)
        {
            if (metadata != null && metadata.itemHashId != 0)
                return metadata.itemHashId;

            return itemData != null ? ResolvePersistentHash(itemData) : 0;
        }

        private static int ResolvePersistentHash(ItemData itemData)
        {
            if (itemData == null)
                return 0;

            return itemData.PersistentHashId != 0
                ? itemData.PersistentHashId
                : LocHash.Compute(itemData.PersistentId);
        }

        private static float ResolveBaseWeight(ItemData itemData, InventoryMetadataFile metadata)
        {
            if (metadata != null && IsFinitePositive(metadata.baseWeightKg))
                return metadata.baseWeightKg;

            return itemData != null ? Mathf.Max(0.05f, itemData.MassKg) : 0.05f;
        }

        private static float ResolveBaseVolume(ItemData itemData, InventoryMetadataFile metadata)
        {
            if (metadata != null && IsFinitePositive(metadata.baseVolumeM3))
                return metadata.baseVolumeM3;

            return itemData != null ? Mathf.Max(0.0005f, itemData.VolumeM3) : 0.0005f;
        }

        private static ushort ResolveStackCapacity(ItemData itemData, InventoryMetadataFile metadata)
        {
            if (metadata != null && metadata.stackCapacity > 0)
                return (ushort)Mathf.Clamp(metadata.stackCapacity, 1, ushort.MaxValue);

            return itemData != null ? (ushort)Mathf.Clamp(itemData.maxStack, 1, ushort.MaxValue) : (ushort)1;
        }

        private static ushort ResolveItemFlags(ItemData itemData, InventoryMetadataFile metadata)
        {
            if (metadata != null && metadata.itemFlags >= 0)
                return (ushort)Mathf.Clamp(metadata.itemFlags, 0, ushort.MaxValue);

            ushort flags = 0;
            if (itemData != null)
            {
                if (itemData.stackable)
                    flags |= ItemRuntimeStateFlags.Stackable;
                if (itemData.isConsumable)
                    flags |= ItemRuntimeStateFlags.Consumable;
                if (itemData.category == ItemCategory.Tool)
                    flags |= ItemRuntimeStateFlags.Tool;
                if (itemData.resourceFamily == ResourceFamily.Organic)
                    flags |= ItemRuntimeStateFlags.Biological;
                if (itemData.IsRadioactive)
                    flags |= ItemRuntimeStateFlags.Radioactive;
            }

            return flags;
        }

        private static InventoryMetadataFile LoadMetadata(string metadataDirectory, string groupName, FactoryReport report)
        {
            if (!AssetDatabase.IsValidFolder(metadataDirectory))
                return null;

            string[] guids = FindAssetsInFolder("t:TextAsset", metadataDirectory);
            TextAsset best = null;
            int bestScore = int.MinValue;
            string needle = NormalizeSearch(groupName);
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                string name = NormalizeSearch(Path.GetFileNameWithoutExtension(path));
                int score = 0;
                if (string.Equals(name, needle, StringComparison.Ordinal))
                    score += 100;
                if (name.Contains(needle))
                    score += 20;
                if (name.Contains("inventory") || name.Contains("container") || name.Contains("loot"))
                    score += 5;

                if (score > bestScore)
                {
                    bestScore = score;
                    best = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
                }
            }

            if (best == null || bestScore <= 0)
                return null;

            try
            {
                InventoryMetadataFile file = JsonUtility.FromJson<InventoryMetadataFile>(best.text);
                if (file != null)
                    file.__sourcePath = AssetDatabase.GetAssetPath(best);
                return file;
            }
            catch (Exception exception)
            {
                report.violations.Add(groupName + ": metadata parse failed: " + exception.GetType().Name + " " + exception.Message);
                return null;
            }
        }

        private static void LoadMaterialCandidates(string materialDirectory, FactoryReport report)
        {
            s_materialCandidates.Clear();
            if (!AssetDatabase.IsValidFolder(materialDirectory))
                return;

            string[] guids = FindAssetsInFolder("t:Material", materialDirectory);
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material != null)
                    s_materialCandidates.Add(material);
            }

            if (s_materialCandidates.Count == 0)
                report.violations.Add("No fallback material found under " + materialDirectory + "; mesh-only sources will save with null material.");
        }

        private static Material ResolveFallbackMaterial(FactoryReport report)
        {
            Material best = null;
            int bestScore = int.MinValue;
            for (int i = 0; i < s_materialCandidates.Count; i++)
            {
                Material material = s_materialCandidates[i];
                if (material == null)
                    continue;

                string path = AssetDatabase.GetAssetPath(material);
                string lower = path.ToLowerInvariant();
                int score = 0;
                if (lower.IndexOf("mat_equipment_atlas", StringComparison.Ordinal) >= 0)
                    score += 120;
                if (lower.IndexOf("inventory", StringComparison.Ordinal) >= 0)
                    score += 40;
                if (lower.IndexOf("equipment", StringComparison.Ordinal) >= 0)
                    score += 30;
                if (lower.IndexOf("pbr", StringComparison.Ordinal) >= 0)
                    score += 20;
                if (lower.IndexOf("metal", StringComparison.Ordinal) >= 0 ||
                    lower.IndexOf("salvage", StringComparison.Ordinal) >= 0)
                {
                    score += 10;
                }

                if (score <= bestScore)
                    continue;

                best = material;
                bestScore = score;
            }

            if (best != null && bestScore > 0)
                return best;

            if (s_materialCandidates.Count > 0)
                return s_materialCandidates[0];

            report.violations.Add("No fallback material candidate is available; mesh-only sources will save with null material.");
            return null;
        }

        private static Material ResolveGroupMaterial(SourceGroup group, InventoryMetadataFile metadata, Material fallbackMaterial)
        {
            if (s_materialCandidates.Count == 0)
                return fallbackMaterial;

            Material best = fallbackMaterial;
            int bestScore = fallbackMaterial != null ? 1 : int.MinValue;
            string groupNeedle = NormalizeSearch(group != null ? group.Name : string.Empty);
            string authoredPath = metadata != null ? NormalizeAssetPath(metadata.materialPath) : string.Empty;
            string authoredName = metadata != null ? NormalizeSearch(metadata.materialName) : string.Empty;
            string authoredRole = metadata != null ? NormalizeSearch(metadata.materialRole) : string.Empty;

            for (int i = 0; i < s_materialCandidates.Count; i++)
            {
                Material candidate = s_materialCandidates[i];
                if (candidate == null)
                    continue;

                int score = ScoreMaterialCandidate(candidate, groupNeedle, authoredPath, authoredName, authoredRole);
                if (score > bestScore)
                {
                    bestScore = score;
                    best = candidate;
                }
            }

            return best;
        }

        private static int ScoreMaterialCandidate(
            Material material,
            string groupNeedle,
            string authoredPath,
            string authoredName,
            string authoredRole)
        {
            string path = NormalizeAssetPath(AssetDatabase.GetAssetPath(material));
            string normalizedPath = NormalizeSearch(path);
            string normalizedName = NormalizeSearch(material != null ? material.name : string.Empty);
            int score = 0;

            if (!string.IsNullOrEmpty(authoredPath) &&
                string.Equals(path, authoredPath, StringComparison.OrdinalIgnoreCase))
            {
                score += 1000;
            }

            if (!string.IsNullOrEmpty(authoredName))
            {
                if (string.Equals(normalizedName, authoredName, StringComparison.Ordinal))
                    score += 800;
                if (normalizedName.Contains(authoredName) || normalizedPath.Contains(authoredName))
                    score += 180;
            }

            if (!string.IsNullOrEmpty(authoredRole))
            {
                if (normalizedName.Contains(authoredRole))
                    score += 140;
                if (normalizedPath.Contains(authoredRole))
                    score += 80;
            }

            if (!string.IsNullOrEmpty(groupNeedle))
            {
                if (normalizedName.Contains(groupNeedle))
                    score += 60;
                if (normalizedPath.Contains(groupNeedle))
                    score += 30;
            }

            if (normalizedPath.Contains("matequipmentatlas"))
                score += 20;
            if (normalizedPath.Contains("inventory") || normalizedPath.Contains("equipment"))
                score += 10;

            return score;
        }

        private static Bounds ResolveLocalRendererBounds(Transform root, SourceGroup group)
        {
            root.GetComponentsInChildren(true, s_renderers);
            bool hasBounds = false;
            Bounds localBounds = new Bounds(Vector3.zero, Vector3.one);
            for (int i = 0; i < s_renderers.Count; i++)
            {
                Renderer renderer = s_renderers[i];
                if (renderer == null)
                    continue;

                Bounds bounds = TransformWorldBoundsToLocal(root, renderer.bounds);
                if (!hasBounds)
                {
                    localBounds = bounds;
                    hasBounds = true;
                }
                else
                {
                    localBounds.Encapsulate(bounds.min);
                    localBounds.Encapsulate(bounds.max);
                }
            }

            s_renderers.Clear();
            if (hasBounds)
                return localBounds;

            if (group.Mesh != null)
                return group.Mesh.bounds;

            return new Bounds(Vector3.zero, Vector3.one);
        }

        private static Bounds TransformWorldBoundsToLocal(Transform root, Bounds worldBounds)
        {
            Vector3 min = worldBounds.min;
            Vector3 max = worldBounds.max;
            Bounds bounds = new Bounds(root.InverseTransformPoint(min), Vector3.zero);
            bounds.Encapsulate(root.InverseTransformPoint(new Vector3(min.x, min.y, max.z)));
            bounds.Encapsulate(root.InverseTransformPoint(new Vector3(min.x, max.y, min.z)));
            bounds.Encapsulate(root.InverseTransformPoint(new Vector3(min.x, max.y, max.z)));
            bounds.Encapsulate(root.InverseTransformPoint(new Vector3(max.x, min.y, min.z)));
            bounds.Encapsulate(root.InverseTransformPoint(new Vector3(max.x, min.y, max.z)));
            bounds.Encapsulate(root.InverseTransformPoint(new Vector3(max.x, max.y, min.z)));
            bounds.Encapsulate(root.InverseTransformPoint(max));
            return bounds;
        }

        private static Transform ResolveLidTransform(Transform root, InventoryMetadataFile metadata)
        {
            if (metadata != null && !string.IsNullOrWhiteSpace(metadata.lidTransformName))
            {
                Transform exact = FindChildByName(root, metadata.lidTransformName);
                if (exact != null)
                    return exact;
            }

            Transform scored = null;
            int bestScore = int.MinValue;
            ScoreLidTransform(root, ref scored, ref bestScore);
            return bestScore > 0 ? scored : null;
        }

        private static void ScoreLidTransform(Transform root, ref Transform best, ref int bestScore)
        {
            for (int i = 0; i < root.childCount; i++)
            {
                Transform child = root.GetChild(i);
                string name = NormalizeSearch(child.name);
                int score = 0;
                if (name.Contains("lid"))
                    score += 100;
                if (name.Contains("hatch"))
                    score += 80;
                if (name.Contains("door"))
                    score += 70;
                if (name.Contains("cover"))
                    score += 55;
                if (name.Contains("hinge"))
                    score += 45;

                if (score > bestScore)
                {
                    bestScore = score;
                    best = child;
                }

                ScoreLidTransform(child, ref best, ref bestScore);
            }
        }

        private static Transform FindChildByName(Transform root, string childName)
        {
            if (root == null || string.IsNullOrWhiteSpace(childName))
                return null;

            if (string.Equals(root.name, childName, StringComparison.OrdinalIgnoreCase))
                return root;

            for (int i = 0; i < root.childCount; i++)
            {
                Transform found = FindChildByName(root.GetChild(i), childName);
                if (found != null)
                    return found;
            }

            return null;
        }

        private static Vector3 ResolveLidAxis(Bounds bounds, InventoryMetadataFile metadata, InventoryContainerKind kind)
        {
            if (metadata != null && IsFiniteVector(metadata.lidAxis) && metadata.lidAxis.sqrMagnitude > 0.000001f)
                return metadata.lidAxis.normalized;

            if (kind == InventoryContainerKind.Locker)
                return Vector3.up;

            Vector3 size = SanitizeSize(bounds.size);
            return size.x >= size.z ? Vector3.right : Vector3.forward;
        }

        private static Vector3 ResolveLidPivot(Bounds bounds, Vector3 axis, InventoryMetadataFile metadata, InventoryContainerKind kind)
        {
            if (metadata != null && IsFiniteVector(metadata.lidPivot) && metadata.lidPivot.sqrMagnitude > 0.000001f)
                return metadata.lidPivot;

            Vector3 min = bounds.min;
            Vector3 max = bounds.max;
            Vector3 center = bounds.center;
            Vector3 absAxis = new Vector3(Mathf.Abs(axis.x), Mathf.Abs(axis.y), Mathf.Abs(axis.z));

            if (kind == InventoryContainerKind.Locker || absAxis.y >= absAxis.x && absAxis.y >= absAxis.z)
                return new Vector3(min.x, center.y, center.z);

            if (absAxis.x >= absAxis.z)
                return new Vector3(center.x, max.y, max.z);

            return new Vector3(min.x, max.y, center.z);
        }

        private static Vector3 ResolveClosedForward(Vector3 axis, InventoryMetadataFile metadata)
        {
            if (metadata != null && IsFiniteVector(metadata.lidClosedForward) && metadata.lidClosedForward.sqrMagnitude > 0.000001f)
                return metadata.lidClosedForward.normalized;

            Vector3 forward = Vector3.Cross(axis, Vector3.up);
            if (forward.sqrMagnitude <= 0.000001f)
                forward = Vector3.forward;
            return forward.normalized;
        }

        private static int[] ResolveSlotConnectivity(InventoryMetadataFile metadata, ushort slots)
        {
            int count = Mathf.Max(1, slots);
            int[] connectivity = new int[count]; // COLD ALLOC: serialized container slot map.
            for (int i = 0; i < count; i++)
                connectivity[i] = i;

            if (metadata == null || metadata.slotConnectivity == null || metadata.slotConnectivity.Length != count)
                return connectivity;

            for (int i = 0; i < count; i++)
                connectivity[i] = metadata.slotConnectivity[i];

            if (!IsValidSlotConnectivity(connectivity, count))
            {
                for (int i = 0; i < count; i++)
                    connectivity[i] = i;
                return connectivity;
            }

            return connectivity;
        }

        private static bool IsValidSlotConnectivity(int[] connectivity, int count)
        {
            if (connectivity == null || connectivity.Length != count || count <= 0)
                return false;

            for (int i = 0; i < count; i++)
            {
                int value = connectivity[i];
                if ((uint)value >= (uint)count)
                    return false;

                for (int j = i + 1; j < count; j++)
                {
                    if (connectivity[j] == value)
                        return false;
                }
            }

            return true;
        }

        private static float ResolveEmissionBaseStrength(InventoryMetadataFile metadata)
        {
            return metadata != null && IsFinite(metadata.emissionBaseStrength)
                ? Mathf.Max(0f, metadata.emissionBaseStrength)
                : 0f;
        }

        private static float ResolveEmissionPulseStrength(InventoryMetadataFile metadata)
        {
            return metadata != null && IsFinite(metadata.emissionPulseStrength) && metadata.emissionPulseStrength >= 0f
                ? metadata.emissionPulseStrength
                : 0.65f;
        }

        private static float ResolveEmissionPulseFrequency(InventoryMetadataFile metadata)
        {
            float value = metadata != null && IsFinite(metadata.emissionPulseHz)
                ? metadata.emissionPulseHz
                : 0.55f;
            return Mathf.Clamp(value, 0.05f, 4f);
        }

        private static float ResolveEmissionMinimumQuality(InventoryMetadataFile metadata)
        {
            float value = metadata != null && IsFinite(metadata.emissionMinQuality)
                ? metadata.emissionMinQuality
                : 0.35f;
            return Mathf.Clamp01(value);
        }

        private static InventoryContainerKind ResolveKind(string groupName, InventoryMetadataFile metadata)
        {
            if (metadata != null && !string.IsNullOrWhiteSpace(metadata.kind))
            {
                if (metadata.kind.Equals("Locker", StringComparison.OrdinalIgnoreCase))
                    return InventoryContainerKind.Locker;
                if (metadata.kind.Equals("Container", StringComparison.OrdinalIgnoreCase) ||
                    metadata.kind.Equals("Chest", StringComparison.OrdinalIgnoreCase) ||
                    metadata.kind.Equals("Crate", StringComparison.OrdinalIgnoreCase))
                {
                    return InventoryContainerKind.Container;
                }
            }

            string normalized = NormalizeSearch(groupName);
            if (normalized.Contains("locker") || normalized.Contains("cabinet") || normalized.Contains("wardrobe"))
                return InventoryContainerKind.Locker;
            if (normalized.Contains("container") ||
                normalized.Contains("crate") ||
                normalized.Contains("chest") ||
                normalized.Contains("cache") ||
                normalized.Contains("box") ||
                normalized.Contains("storage"))
            {
                return InventoryContainerKind.Container;
            }

            return InventoryContainerKind.Loot;
        }

        private static string ResolvePrefabName(InventoryContainerKind kind, string groupName)
        {
            string clean = SanitizeAssetName(groupName);
            if (kind == InventoryContainerKind.Locker)
                return "PFB_Locker_" + clean;
            if (kind == InventoryContainerKind.Container)
                return "PFB_Container_" + clean;
            return "PFB_Loot_" + clean;
        }

        private static FactoryReport FinalizeReport(FactoryReport report, Stopwatch stopwatch, FactorySettings settings)
        {
            stopwatch.Stop();
            report.totalEditorMicroseconds = ElapsedMicroseconds(stopwatch);
            if (!settings.DryRun)
                AssetDatabase.SaveAssets();

            Debug.Log("[InventoryPrefabFactory1739] Completed. Groups=" + report.sourceGroups.ToString(CultureInfo.InvariantCulture) +
                      " Assembled=" + report.prefabsAssembled.ToString(CultureInfo.InvariantCulture) +
                      " Failed=" + report.prefabsFailed.ToString(CultureInfo.InvariantCulture) +
                      " us=" + report.totalEditorMicroseconds.ToString(CultureInfo.InvariantCulture));
            return report;
        }

        private static PrefabMetric Fail(PrefabMetric metric, FactoryReport report, string reason)
        {
            metric.failure = reason;
            metric.status = "FAIL";
            if (metric.editorMicroseconds <= 0)
                metric.editorMicroseconds = 1;
            report.violations.Add(metric.prefabName + ": " + reason);
            return metric;
        }

        private static void SetFailureIfEmpty(PrefabMetric metric, string reason)
        {
            if (metric != null && string.IsNullOrEmpty(metric.failure))
                metric.failure = reason;
        }

        private static void DeleteInvalidPrefabAsset(string prefabPath)
        {
            if (string.IsNullOrWhiteSpace(prefabPath))
                return;

            if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) != null)
                AssetDatabase.DeleteAsset(prefabPath);
        }

        private static void EnsureAssetFolder(string folder)
        {
            string normalized = NormalizeAssetPath(folder);
            if (AssetDatabase.IsValidFolder(normalized))
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

        private static void SetLayerRecursive(GameObject root, int layer)
        {
            if (root == null)
                return;

            root.layer = layer;
            Transform transform = root.transform;
            for (int i = 0; i < transform.childCount; i++)
                SetLayerRecursive(transform.GetChild(i).gameObject, layer);
        }

        private static void ResetLocalTransform(Transform transform)
        {
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
            transform.localScale = Vector3.one;
        }

        private static bool ShouldUseCapsule(Vector3 size)
        {
            int axis = ResolveLongestAxis(size);
            float longest = ResolveAxisSize(size, axis);
            float a = axis == 0 ? size.y : size.x;
            float b = axis == 2 ? size.y : size.z;
            float second = Mathf.Max(MinimumColliderSize, Mathf.Max(a, b));
            return longest >= second * CapsuleAspectThreshold;
        }

        private static int ResolveLongestAxis(Vector3 size)
        {
            if (size.x >= size.y && size.x >= size.z)
                return 0;
            return size.y >= size.z ? 1 : 2;
        }

        private static float ResolveAxisSize(Vector3 size, int direction)
        {
            if (direction == 0)
                return Mathf.Max(MinimumColliderSize, Mathf.Abs(size.x));
            if (direction == 1)
                return Mathf.Max(MinimumColliderSize, Mathf.Abs(size.y));
            return Mathf.Max(MinimumColliderSize, Mathf.Abs(size.z));
        }

        private static float ResolveCapsuleRadius(Vector3 size, int direction)
        {
            float a = direction == 0 ? size.y : size.x;
            float b = direction == 2 ? size.y : size.z;
            return Mathf.Max(MinimumColliderSize * 0.5f, Mathf.Min(Mathf.Abs(a), Mathf.Abs(b)) * 0.5f);
        }

        private static Vector3 SanitizeSize(Vector3 size)
        {
            return new Vector3(
                Mathf.Max(MinimumColliderSize, Mathf.Abs(IsFinite(size.x) ? size.x : 1f)),
                Mathf.Max(MinimumColliderSize, Mathf.Abs(IsFinite(size.y) ? size.y : 1f)),
                Mathf.Max(MinimumColliderSize, Mathf.Abs(IsFinite(size.z) ? size.z : 1f)));
        }

        private static Vector3 SanitizeVector(Vector3 value, Vector3 fallback)
        {
            return IsFiniteVector(value) ? value : fallback;
        }

        private static Vector3 SanitizeScale(Vector3 value)
        {
            return new Vector3(
                Mathf.Max(0.0001f, Mathf.Abs(IsFinite(value.x) ? value.x : 1f)),
                Mathf.Max(0.0001f, Mathf.Abs(IsFinite(value.y) ? value.y : 1f)),
                Mathf.Max(0.0001f, Mathf.Abs(IsFinite(value.z) ? value.z : 1f)));
        }

        private static Quaternion SanitizeQuaternion(Quaternion value)
        {
            if (!IsFinite(value.x) || !IsFinite(value.y) || !IsFinite(value.z) || !IsFinite(value.w))
                return Quaternion.identity;

            float lengthSq = value.x * value.x + value.y * value.y + value.z * value.z + value.w * value.w;
            if (lengthSq <= 0.000001f)
                return Quaternion.identity;

            float invLength = 1f / Mathf.Sqrt(lengthSq);
            return new Quaternion(value.x * invLength, value.y * invLength, value.z * invLength, value.w * invLength);
        }

        private static string CleanGroupName(string value)
        {
            string clean = value ?? string.Empty;
            RemovePrefix(ref clean, "PFB_");
            RemovePrefix(ref clean, "GEN_");
            RemovePrefix(ref clean, "MESH_");
            RemovePrefix(ref clean, "SM_");
            RemovePrefix(ref clean, "Item_");
            RemovePrefix(ref clean, "Data_");
            RemoveSuffix(ref clean, "_World");
            RemoveSuffix(ref clean, "_LOD0");
            RemoveSuffix(ref clean, "_Mesh");
            RemoveSuffix(ref clean, "_Visual");
            return SanitizeAssetName(clean);
        }

        private static string CleanColliderProxyGroupName(string value)
        {
            string clean = value ?? string.Empty;
            RemovePrefix(ref clean, "COL_");
            RemovePrefix(ref clean, "COL-");
            RemovePrefix(ref clean, "Collider_");
            RemovePrefix(ref clean, "Collision_");
            RemovePrefix(ref clean, "Proxy_");
            RemoveSuffix(ref clean, "_COL");
            RemoveSuffix(ref clean, "_Collider");
            RemoveSuffix(ref clean, "_Collision");
            RemoveSuffix(ref clean, "_Proxy");
            RemoveSuffix(ref clean, "_PhysicsProxy");
            return CleanGroupName(clean);
        }

        private static bool IsCollisionAssetName(string value)
        {
            string normalized = NormalizeSearch(value);
            return normalized.StartsWith("col", StringComparison.Ordinal) ||
                   normalized.Contains("collision") ||
                   normalized.Contains("collider");
        }

        private static bool IsNonPrimaryLodName(string value)
        {
            string normalized = NormalizeSearch(value);
            return normalized.EndsWith("lod1", StringComparison.Ordinal) ||
                   normalized.EndsWith("lod2", StringComparison.Ordinal) ||
                   normalized.EndsWith("lod3", StringComparison.Ordinal);
        }

        private static string SanitizeAssetName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "Unnamed";

            char[] chars = value.Trim().ToCharArray();
            for (int i = 0; i < chars.Length; i++)
            {
                char c = chars[i];
                if (!(char.IsLetterOrDigit(c) || c == '_' || c == '-'))
                    chars[i] = '_';
            }

            string result = new string(chars);
            while (result.Contains("__"))
                result = result.Replace("__", "_");
            return result.Trim('_');
        }

        private static string NormalizeSearch(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            string clean = value.ToLowerInvariant();
            clean = clean.Replace(" ", string.Empty);
            clean = clean.Replace("_", string.Empty);
            clean = clean.Replace("-", string.Empty);
            clean = clean.Replace(".", string.Empty);
            return clean;
        }

        private static string NormalizeAssetPath(string path)
        {
            return string.IsNullOrWhiteSpace(path) ? string.Empty : path.Replace('\\', '/').TrimEnd('/');
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

        private static bool IsFinitePositive(float value)
        {
            return IsFinite(value) && value > 0f;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static bool IsFiniteVector(Vector3 value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        }

        private static string FormatVectorR(Vector3 value)
        {
            return value.x.ToString("R", CultureInfo.InvariantCulture) + "," +
                   value.y.ToString("R", CultureInfo.InvariantCulture) + "," +
                   value.z.ToString("R", CultureInfo.InvariantCulture);
        }

        private static uint HashString(string value)
        {
            unchecked
            {
                uint hash = 2166136261u;
                string safe = value ?? string.Empty;
                for (int i = 0; i < safe.Length; i++)
                {
                    hash ^= safe[i];
                    hash *= 16777619u;
                }

                return hash == 0u ? 1u : hash;
            }
        }

        private static uint HashSlotConnectivity(int[] connectivity)
        {
            unchecked
            {
                uint hash = 2166136261u;
                if (connectivity != null)
                {
                    for (int i = 0; i < connectivity.Length; i++)
                    {
                        hash ^= (uint)connectivity[i];
                        hash *= 16777619u;
                    }
                }

                return hash == 0u ? 1u : hash;
            }
        }

        private static long ElapsedMicroseconds(Stopwatch stopwatch)
        {
            long ticks = stopwatch.ElapsedTicks;
            long microseconds = ticks * 1000000L / Stopwatch.Frequency;
            return microseconds > 0 ? microseconds : 1;
        }

        private static void ClearScratch()
        {
            s_meshRenderers.Clear();
            s_renderers.Clear();
            s_colliders.Clear();
            s_meshColliders.Clear();
            s_particleSystems.Clear();
            s_lodGroups.Clear();
            s_itemNodes.Clear();
            s_containerMetadata.Clear();
            s_emissionPresenters.Clear();
        }

        [Serializable]
        public sealed class FactorySettings
        {
            public string SourceDirectory;
            public string MetadataDirectory;
            public string MaterialDirectory;
            public string ItemDataDirectory;
            public string OutputDirectory;
            public float AuthoredQualityWeight;
            public bool DryRun;
            public int MaxGroupsPerRun;

            public static FactorySettings Default
            {
                get
                {
                    return new FactorySettings
                    {
                        SourceDirectory = DefaultSourceDirectory,
                        MetadataDirectory = DefaultMetadataDirectory,
                        MaterialDirectory = DefaultMaterialDirectory,
                        ItemDataDirectory = DefaultItemDataDirectory,
                        OutputDirectory = DefaultOutputDirectory,
                        AuthoredQualityWeight = DefaultAuthoredQualityWeight,
                        DryRun = true,
                        MaxGroupsPerRun = 512
                    };
                }
            }

            public FactorySettings Sanitize()
            {
                SourceDirectory = string.IsNullOrWhiteSpace(SourceDirectory) ? DefaultSourceDirectory : NormalizeAssetPath(SourceDirectory);
                MetadataDirectory = string.IsNullOrWhiteSpace(MetadataDirectory) ? DefaultMetadataDirectory : NormalizeAssetPath(MetadataDirectory);
                MaterialDirectory = string.IsNullOrWhiteSpace(MaterialDirectory) ? DefaultMaterialDirectory : NormalizeAssetPath(MaterialDirectory);
                ItemDataDirectory = string.IsNullOrWhiteSpace(ItemDataDirectory) ? DefaultItemDataDirectory : NormalizeAssetPath(ItemDataDirectory);
                OutputDirectory = string.IsNullOrWhiteSpace(OutputDirectory) ? DefaultOutputDirectory : NormalizeAssetPath(OutputDirectory);
                AuthoredQualityWeight = Mathf.Clamp01(IsFinite(AuthoredQualityWeight) ? AuthoredQualityWeight : DefaultAuthoredQualityWeight);
                MaxGroupsPerRun = Mathf.Clamp(MaxGroupsPerRun <= 0 ? 512 : MaxGroupsPerRun, 1, 4096);
                return this;
            }
        }

        [Serializable]
        public sealed class FactoryReport
        {
            public string agentId;
            public string generatedUtc;
            public string sourceDirectory;
            public string metadataDirectory;
            public string materialDirectory;
            public string itemDataDirectory;
            public string outputDirectory;
            public bool dryRun;
            public float authoredQualityWeight;
            public int sourceGroups;
            public int existingPrefabsAudited;
            public int existingPrefabViolations;
            public int existingPrefabMeshColliderViolations;
            public int existingPrefabNonPrimitiveColliderViolations;
            public int existingPrefabColliderLayerViolations;
            public int existingPrefabParticleSystemViolations;
            public int existingPrefabMaterialSlotsAudited;
            public int existingPrefabMaterialViolations;
            public int existingPrefabMissingItemNode;
            public int existingPrefabMissingPrimitiveCollider;
            public int existingPrefabMissingLodPolicy;
            public int existingPrefabMissingInteractionAnchor;
            public int existingPrefabMissingContainerMetadata;
            public int existingPrefabDeepHierarchyViolations;
            public int existingPrefabEmissionBindingViolations;
            public int itemDataAssets;
            public int itemWorldPrefabSources;
            public int colliderProxySources;
            public int orphanColliderProxySources;
            public int prefabsAssembled;
            public int prefabsFailed;
            public int itemNodeBakes;
            public int containerMetadataBakes;
            public int containerSlotMapBakes;
            public int primitiveColliderCount;
            public int sourcePrimitiveCollidersStripped;
            public int meshCollidersRejected;
            public int brgMaterialsAudited;
            public int emissionMaterialsVerified;
            public int emissionPresenterBakes;
            public int emissionRendererBindings;
            public int scavengeTargetBakes;
            public int interactionAnchorBakes;
            public int lodPolicyBakes;
            public int colliderProxyBakes;
            public long totalEditorMicroseconds;
            public string collisionPolicy = "final prefab accepts only BoxCollider/CapsuleCollider on Interactable; copied source colliders are stripped and rebuilt.";
            public string runtimePolicy = "factory writes passive ItemNodeData and ContainerMetadata only; inventory truth stays in ItemData/SOA systems.";
            public List<PrefabMetric> prefabs = new List<PrefabMetric>(512);
            public List<string> violations = new List<string>(256);
        }

        [Serializable]
        public sealed class PrefabMetric
        {
            public string prefabName;
            public string sourcePath;
            public string metadataPath;
            public string outputPath;
            public string visualRootName;
            public string colliderProxyPath;
            public string kind;
            public int itemHashId;
            public float baseWeightKg;
            public float capacityWeightKg;
            public ushort slotCapacity;
            public int slotConnectivityCount;
            public uint slotConnectivityHash;
            public Vector3 lidAxis;
            public Vector3 lidPivot;
            public int ikHandleBakes;
            public int rendererCount;
            public int colliderCount;
            public int sourceCollidersStripped;
            public int brgMaterialsAudited;
            public int emissionMaterialsVerified;
            public int emissionPresenterBakes;
            public int emissionRendererBindings;
            public int scavengeTargetBakes;
            public int interactionAnchorCount;
            public int interactionAnchorBakes;
            public int lodGroupCount;
            public int lodPolicyBakes;
            public int colliderProxyBakes;
            public uint prefabHash;
            public long editorMicroseconds;
            public string status;
            public string failure;
        }

        private sealed class SourceGroup
        {
            public string Name;
            public string SourcePath;
            public Mesh Mesh;
            public Mesh ColliderMesh;
            public GameObject PrefabSource;
            public GameObject ColliderPrefab;
            public string ColliderProxyPath;
            public ItemData ItemData;
        }

        [Serializable]
        private sealed class InventoryMetadataFile
        {
            public string kind;
            public string itemStableId;
            public int itemHashId;
            public float baseWeightKg;
            public float baseVolumeM3;
            public int stackCapacity;
            public int itemFlags = -1;
            public float capacityWeightKg;
            public int slotCapacity;
            public int[] slotConnectivity;
            public string lidTransformName;
            public Vector3 lidPivot;
            public Vector3 lidAxis;
            public Vector3 lidClosedForward;
            public float minOpenDegrees;
            public float maxOpenDegrees = 95f;
            public int flags;
            public bool requiresEmission;
            public float emissionBaseStrength;
            public float emissionPulseStrength = 0.65f;
            public float emissionPulseHz = 0.55f;
            public float emissionMinQuality = 0.35f;
            public string materialPath;
            public string materialName;
            public string materialRole;
            public int harvestUnits;
            public ColliderDescriptor[] colliders;
            [NonSerialized] public string __sourcePath;
        }

        [Serializable]
        private sealed class ColliderDescriptor
        {
            public string name;
            public string type;
            public Vector3 center;
            public Vector3 size = Vector3.one;
            public int direction = -1;
            public float radius;
            public float height;
            public bool isTrigger;
        }
    }
}
#endif
