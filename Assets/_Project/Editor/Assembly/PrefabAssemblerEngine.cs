#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using Hecton8.Building;
using Hecton8.Construction;
using Hecton8.Gameplay;
using Hecton.Localization;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

namespace Hecton8.Editor.Assembly
{
    public sealed class PrefabAssemblerEngine : EditorWindow
    {
        private const string AgentId = "1731";
        private const string DefaultMeshDirectory = "Assets/_Project/Art/Baked/Structures/Agent1712";
        private const string DefaultMaterialDirectory = "Assets/_Project/Art/Materials";
        private const string DefaultCollisionDirectory = "Assets/_Project/Art/Baked/Structures/Agent1712";
        private const string DefaultMetadataDirectory = "Assets/_Project/Data/Construction";
        private const string DefaultOutputDirectory = "Assets/_Project/Prefabs/Construction/Final";
        private const string WorldStaticLayerName = "World_Static";
        private const string PhysicsMaterialName = "MAT_Physics_World_Static_1716";
        private const float MinimumCullHeight = 0.05f;
        private const float SmallModuleMeters = 1f;
        private const float LargeModuleMeters = 10f;
        private const int MaxMaterialSlots = 8;
        private const int MaxSocketCount = 128;
        private const int MaxConsoleViolationsPerRun = 48;
        private const float SocketBoundsToleranceMeters = 0.50f;

        private static readonly List<MeshRenderer> s_RendererScratch = new List<MeshRenderer>(64);
        private static readonly List<Renderer> s_GenericRendererScratch = new List<Renderer>(32);
        private static readonly List<Collider> s_ColliderScratch = new List<Collider>(64);
        private static readonly List<MeshCollider> s_MeshColliderScratch = new List<MeshCollider>(32);
        private static readonly List<MeshFilter> s_MeshFilterScratch = new List<MeshFilter>(32);
        private static readonly List<Rigidbody> s_RigidbodyScratch = new List<Rigidbody>(8);
        private static readonly List<Transform> s_TransformScratch = new List<Transform>(64);
        private static readonly List<ModuleSocket> s_ModuleSocketScratch = new List<ModuleSocket>(32);

        [SerializeField] private string meshDirectory = DefaultMeshDirectory;
        [SerializeField] private string materialDirectory = DefaultMaterialDirectory;
        [SerializeField] private string collisionDirectory = DefaultCollisionDirectory;
        [SerializeField] private string metadataDirectory = DefaultMetadataDirectory;
        [SerializeField] private string outputDirectory = DefaultOutputDirectory;
        [SerializeField] private bool dryRun = true;
        [SerializeField] private bool allowLod2Fallback = true;
        [SerializeField] private bool requireSocketMetadata = true;

        private Vector2 scroll;
        private AssemblerReport lastReport;

        [MenuItem("Hecton8/Assembly/Prefab Assembler Engine 1731")]
        public static void OpenWindow()
        {
            PrefabAssemblerEngine window = GetWindow<PrefabAssemblerEngine>("Prefab Assembler 1731");
            window.minSize = new Vector2(640f, 420f);
            window.Show();
        }

        [MenuItem("Hecton8/Assembly/Run Prefab Assembler 1731")]
        public static void RunDefaultAssembly()
        {
            AssemblerSettings settings = AssemblerSettings.Default;
            settings.DryRun = false;
            Run(settings);
        }

        [MenuItem("Hecton8/Assembly/Dry Run Prefab Assembler 1731")]
        public static void RunDefaultDryRun()
        {
            AssemblerSettings settings = AssemblerSettings.Default;
            settings.DryRun = true;
            Run(settings);
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("HECTON-8 Prefab Assembler 1731", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Offline-only assembly: LOD meshes, shared materials, COL_ proxies, LODGroup, and baked sockets.", MessageType.Info);

            meshDirectory = EditorGUILayout.TextField("Mesh Directory", meshDirectory);
            materialDirectory = EditorGUILayout.TextField("Material Directory", materialDirectory);
            collisionDirectory = EditorGUILayout.TextField("Collision Directory", collisionDirectory);
            metadataDirectory = EditorGUILayout.TextField("Metadata Directory", metadataDirectory);
            outputDirectory = EditorGUILayout.TextField("Output Directory", outputDirectory);
            dryRun = EditorGUILayout.Toggle("Dry Run", dryRun);
            allowLod2Fallback = EditorGUILayout.Toggle("Allow LOD2 Fallback", allowLod2Fallback);
            requireSocketMetadata = EditorGUILayout.Toggle("Require Socket Metadata", requireSocketMetadata);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Dry Run"))
                lastReport = Run(BuildSettings(dryRunOverride: true));
            if (GUILayout.Button("Assemble Prefabs"))
                lastReport = Run(BuildSettings(dryRunOverride: false));
            EditorGUILayout.EndHorizontal();

            if (lastReport == null)
                return;

            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.LabelField("Report", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Groups", lastReport.GroupsDiscovered.ToString(CultureInfo.InvariantCulture));
            EditorGUILayout.LabelField("Assembled", lastReport.PrefabsAssembled.ToString(CultureInfo.InvariantCulture));
            EditorGUILayout.LabelField("Failed", lastReport.PrefabsFailed.ToString(CultureInfo.InvariantCulture));
            EditorGUILayout.LabelField("Dry Run", lastReport.DryRun ? "true" : "false");
            for (int i = 0; i < lastReport.Violations.Count; i++)
                EditorGUILayout.LabelField(lastReport.Violations[i]);
            EditorGUILayout.EndScrollView();
        }

        private AssemblerSettings BuildSettings(bool dryRunOverride)
        {
            return new AssemblerSettings
            {
                MeshDirectory = meshDirectory,
                MaterialDirectory = materialDirectory,
                CollisionDirectory = collisionDirectory,
                MetadataDirectory = metadataDirectory,
                OutputDirectory = outputDirectory,
                DryRun = dryRunOverride,
                AllowLod2Fallback = allowLod2Fallback,
                RequireSocketMetadata = requireSocketMetadata
            };
        }

        public static AssemblerReport Run(AssemblerSettings settings)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            AssemblerReport report = new AssemblerReport
            {
                AgentId = AgentId,
                UtcTimestamp = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
                DryRun = settings.DryRun,
                MeshDirectory = settings.MeshDirectory,
                MaterialDirectory = settings.MaterialDirectory,
                CollisionDirectory = settings.CollisionDirectory,
                MetadataDirectory = settings.MetadataDirectory,
                OutputDirectory = settings.OutputDirectory,
                LodFormula = "size01=sqrt(saturate((diagonal-1)/(10-1))); lod0=lerp(0.45,0.60,size01); lod1=lerp(0.22,0.30,size01); lod2=0.05"
            };

            try
            {
                if (!AssetDatabase.IsValidFolder(settings.MeshDirectory))
                {
                    AddViolation(report, "Mesh directory is invalid: " + settings.MeshDirectory);
                    return report;
                }

                if (!AssetDatabase.IsValidFolder(settings.MaterialDirectory))
                    AddViolation(report, "Material directory is invalid: " + settings.MaterialDirectory);

                if (!AssetDatabase.IsValidFolder(settings.CollisionDirectory))
                    AddViolation(report, "Collision directory is invalid: " + settings.CollisionDirectory);

                MaterialPalette palette = MaterialPalette.Build(settings.MaterialDirectory, report);
                PhysicsMaterial physicsMaterial = ResolvePhysicsMaterial(report);
                int worldStaticLayer = LayerMask.NameToLayer(WorldStaticLayerName);
                if (worldStaticLayer < 0)
                    AddViolation(report, "Missing required layer: " + WorldStaticLayerName);

                List<ModuleMeshGroup> groups = DiscoverMeshGroups(settings.MeshDirectory, report);
                report.GroupsDiscovered = groups.Count;
                if (groups.Count == 0)
                    AddViolation(report, "No _LOD0 mesh groups discovered in " + settings.MeshDirectory);

                for (int i = 0; i < groups.Count; i++)
                    AssembleGroup(groups[i], palette, physicsMaterial, worldStaticLayer, settings, report);
            }
            finally
            {
                stopwatch.Stop();
                FinalizeInMemoryReport(report, stopwatch.ElapsedTicks);
                if (!settings.DryRun)
                    AssetDatabase.SaveAssets();
            }

            return report;
        }

        private static void AssembleGroup(
            ModuleMeshGroup group,
            MaterialPalette palette,
            PhysicsMaterial physicsMaterial,
            int worldStaticLayer,
            AssemblerSettings settings,
            AssemblerReport report)
        {
            PrefabMetric metric = new PrefabMetric
            {
                ModuleName = group.ModuleName,
                SourceLod0 = group.Lod0Path,
                SourceLod1 = group.Lod1Path,
                SourceLod2 = group.Lod2Path
            };

            Stopwatch stopwatch = Stopwatch.StartNew();
            GameObject root = null;
            string outputPath = BuildOutputPath(settings.OutputDirectory, group.ModuleName);
            try
            {
                if (group.Lod0 == null || group.Lod1 == null)
                {
                    metric.Status = "FAILED";
                    metric.Failure = "Missing required LOD0 or LOD1 mesh.";
                    AddPrefabMetric(report, metric);
                    AddViolation(report, group.ModuleName + ": " + metric.Failure);
                    return;
                }

                if (group.DuplicateLodDetected)
                {
                    metric.Status = "FAILED";
                    metric.Failure = "Duplicate LOD mesh assets detected.";
                    AddPrefabMetric(report, metric);
                    AddViolation(report, group.ModuleName + ": " + metric.Failure);
                    return;
                }

                if (group.Lod2 == null)
                {
                    if (!settings.AllowLod2Fallback)
                    {
                        metric.Status = "FAILED";
                        metric.Failure = "Missing LOD2 mesh and fallback disabled.";
                        AddPrefabMetric(report, metric);
                        AddViolation(report, group.ModuleName + ": " + metric.Failure);
                        return;
                    }

                    group.Lod2 = group.Lod1;
                    group.Lod2Path = group.Lod1Path;
                    metric.Lod2Fallback = true;
                }

                if (physicsMaterial == null)
                {
                    metric.Status = "FAILED";
                    metric.Failure = "Required PhysicsMaterial was not found.";
                    AddPrefabMetric(report, metric);
                    return;
                }

                if (worldStaticLayer < 0)
                {
                    metric.Status = "FAILED";
                    metric.Failure = "World_Static layer is missing.";
                    AddPrefabMetric(report, metric);
                    return;
                }

                root = new GameObject("PFB_" + group.ModuleName);
                ResetLocalTransform(root.transform);
                ApplyVisualStaticFlags(root);
                Bounds combinedBounds = CombineBounds(group);
                metric.BoundsDiagonalMeters = combinedBounds.size.magnitude;
                BaseModuleTemplate moduleTemplate = ResolveBaseModuleTemplate(group, settings.MetadataDirectory);
                MaterialManifest materialManifest = ResolveMaterialManifest(group, settings.MetadataDirectory, settings.MaterialDirectory, report, metric);

                MeshRenderer lod0Renderer = CreateLodChild(root.transform, "LOD0", group.Lod0, palette, materialManifest, 0, report, metric);
                MeshRenderer lod1Renderer = CreateLodChild(root.transform, "LOD1", group.Lod1, palette, materialManifest, 1, report, metric);
                MeshRenderer lod2Renderer = CreateLodChild(root.transform, "LOD2", group.Lod2, palette, materialManifest, 2, report, metric);
                if (metric.MaterialContractFailed)
                {
                    metric.Status = "FAILED";
                    if (string.IsNullOrEmpty(metric.Failure))
                        metric.Failure = "Material contract failed.";
                    AddPrefabMetric(report, metric);
                    return;
                }

                ConfigureLodGroup(root, combinedBounds, lod0Renderer, lod1Renderer, lod2Renderer, metric);

                if (!AttachCollisionProxy(root, group, settings.CollisionDirectory, physicsMaterial, worldStaticLayer, report, metric))
                {
                    metric.Status = "FAILED";
                    AddPrefabMetric(report, metric);
                    return;
                }

                if (!BindModuleMetadata(root, group, moduleTemplate, settings.MetadataDirectory, settings.CollisionDirectory, combinedBounds, settings.RequireSocketMetadata, report, metric))
                {
                    metric.Status = "FAILED";
                    AddPrefabMetric(report, metric);
                    return;
                }

                if (!AttachRuntimeContracts(root, group, moduleTemplate, settings.MetadataDirectory, combinedBounds, worldStaticLayer, report, metric))
                {
                    metric.Status = "FAILED";
                    AddPrefabMetric(report, metric);
                    return;
                }

                if (!ValidateRoot(root, group, report, metric, out string validationFailure))
                {
                    metric.Status = "FAILED";
                    metric.Failure = validationFailure;
                    AddPrefabMetric(report, metric);
                    AddViolation(report, group.ModuleName + ": " + validationFailure);
                    return;
                }

                if (settings.DryRun)
                {
                    metric.Status = "DRY_RUN_OK";
                    report.PrefabsDryRunPassed++;
                    AddPrefabMetric(report, metric);
                    return;
                }

                EnsureAssetFolder(settings.OutputDirectory);
                GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(root, outputPath, out bool success);
                if (!success || savedPrefab == null)
                {
                    metric.Status = "FAILED";
                    metric.Failure = "PrefabUtility.SaveAsPrefabAsset returned null or false.";
                    AddPrefabMetric(report, metric);
                    AddViolation(report, group.ModuleName + ": " + metric.Failure);
                    AssetDatabase.DeleteAsset(outputPath);
                    return;
                }

                if (!ValidateSavedPrefab(savedPrefab, group, report, metric, out validationFailure))
                {
                    metric.Status = "FAILED";
                    metric.Failure = validationFailure;
                    AssetDatabase.DeleteAsset(outputPath);
                    AddPrefabMetric(report, metric);
                    AddViolation(report, group.ModuleName + ": " + validationFailure);
                    return;
                }

                metric.Status = "ASSEMBLED";
                metric.OutputPrefab = outputPath;
                report.PrefabsAssembled++;
                AddPrefabMetric(report, metric);
            }
            catch (Exception exception)
            {
                metric.Status = "FAILED";
                metric.Failure = exception.GetType().Name + ": " + exception.Message;
                AddPrefabMetric(report, metric);
                AddViolation(report, group.ModuleName + ": " + metric.Failure);
                AssetDatabase.DeleteAsset(outputPath);
            }
            finally
            {
                metric.EditorMicroseconds = TicksToMicroseconds(stopwatch.ElapsedTicks);
                if (root != null)
                    Object.DestroyImmediate(root);
            }
        }

        private static MeshRenderer CreateLodChild(
            Transform root,
            string name,
            Mesh mesh,
            MaterialPalette palette,
            MaterialManifest materialManifest,
            int lodIndex,
            AssemblerReport report,
            PrefabMetric metric)
        {
            GameObject child = new GameObject(name);
            child.transform.SetParent(root, false);
            ResetLocalTransform(child.transform);
            ApplyVisualStaticFlags(child);

            MeshFilter filter = child.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;

            MeshRenderer renderer = child.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = BuildSharedMaterialArray(mesh, palette, materialManifest, name, report, metric);
            renderer.receiveGI = ReceiveGI.Lightmaps;
            renderer.shadowCastingMode = lodIndex < 2 ? ShadowCastingMode.On : ShadowCastingMode.Off;
            renderer.receiveShadows = true;

            Material[] materials = renderer.sharedMaterials;
            for (int i = 0; i < materials.Length; i++)
            {
                Material material = materials[i];
                if (material == null)
                {
                    metric.MaterialContractFailed = true;
                    if (string.IsNullOrEmpty(metric.Failure))
                        metric.Failure = "Null material slot on " + name + ".";
                    AddViolation(report, metric.ModuleName + ": null material on " + name + " slot " + i.ToString(CultureInfo.InvariantCulture));
                    continue;
                }

                if (!IsSrpBatcherCandidate(material, out string proof))
                {
                    metric.MaterialContractFailed = true;
                    if (string.IsNullOrEmpty(metric.Failure))
                        metric.Failure = "SRP Batcher material rejected on " + name + ".";
                    AddViolation(report, metric.ModuleName + ": material " + material.name + " rejected for SRP Batcher. " + proof);
                }
                else
                    metric.MaterialProofs.Add(material.name + ":" + proof);
            }

            return renderer;
        }

        private static Material[] BuildSharedMaterialArray(
            Mesh mesh,
            MaterialPalette palette,
            MaterialManifest materialManifest,
            string lodName,
            AssemblerReport report,
            PrefabMetric metric)
        {
            int subMeshCount = mesh != null ? mesh.subMeshCount : 1;
            int count = Mathf.Max(1, subMeshCount);
            if (count > MaxMaterialSlots)
            {
                metric.MaterialContractFailed = true;
                if (string.IsNullOrEmpty(metric.Failure))
                    metric.Failure = lodName + " exceeds material slot cap.";
                AddViolation(report, metric.ModuleName + ": " + lodName + " uses " + count.ToString(CultureInfo.InvariantCulture) + " material slots; maximum is " + MaxMaterialSlots.ToString(CultureInfo.InvariantCulture));
                count = MaxMaterialSlots;
            }

            if (materialManifest != null && materialManifest.IsPresent)
                return materialManifest.BuildMaterialArray(count, lodName, report, metric);

            return palette.BuildMaterialArray(count);
        }

        private static void ConfigureLodGroup(GameObject root, Bounds bounds, Renderer lod0, Renderer lod1, Renderer lod2, PrefabMetric metric)
        {
            CalculateLodHeights(bounds, out float lod0Height, out float lod1Height, out float lod2Height);
            LODGroup lodGroup = root.AddComponent<LODGroup>();
            lodGroup.fadeMode = LODFadeMode.CrossFade;
            lodGroup.animateCrossFading = true;
            lodGroup.SetLODs(new[]
            {
                new LOD(lod0Height, new[] { lod0 }),
                new LOD(lod1Height, new[] { lod1 }),
                new LOD(lod2Height, new[] { lod2 })
            });
            lodGroup.RecalculateBounds();

            metric.Lod0Height = lod0Height;
            metric.Lod1Height = lod1Height;
            metric.Lod2Height = lod2Height;
        }

        private static void CalculateLodHeights(Bounds bounds, out float lod0Height, out float lod1Height, out float lod2Height)
        {
            float diagonal = Mathf.Max(SmallModuleMeters, bounds.size.magnitude);
            float size01 = Mathf.Clamp01((diagonal - SmallModuleMeters) / (LargeModuleMeters - SmallModuleMeters));
            size01 = Mathf.Sqrt(size01);
            lod0Height = Mathf.Clamp(Mathf.Lerp(0.45f, 0.60f, size01), 0.30f, 0.60f);
            lod1Height = Mathf.Clamp(Mathf.Lerp(0.22f, 0.30f, size01), 0.12f, 0.30f);
            lod2Height = MinimumCullHeight;
        }

        private static bool AttachCollisionProxy(
            GameObject root,
            ModuleMeshGroup group,
            string collisionDirectory,
            PhysicsMaterial physicsMaterial,
            int worldStaticLayer,
            AssemblerReport report,
            PrefabMetric metric)
        {
            GameObject proxyRoot = ResolveCollisionProxyPrefab(group, collisionDirectory);
            if (proxyRoot != null)
            {
                GameObject instance = PrefabUtility.InstantiatePrefab(proxyRoot) as GameObject;
                if (instance == null)
                {
                    metric.Failure = "Failed to instantiate collision proxy prefab.";
                    AddViolation(report, group.ModuleName + ": " + metric.Failure);
                    return false;
                }

                instance.name = "COL_" + group.ModuleName;
                instance.transform.SetParent(root.transform, false);
                ResetLocalTransform(instance.transform);
                metric.CollisionProxy = AssetDatabase.GetAssetPath(proxyRoot);
                StripRenderableComponentsFromCollisionProxy(instance);
                ApplyCollisionStaticFlagsRecursively(instance, worldStaticLayer);
                EnforceColliderLayerAndMaterial(instance, worldStaticLayer, physicsMaterial);
                return ValidateCollisionProxyAndReport(instance, group, report, metric);
            }

            Mesh collisionMesh = ResolveCollisionProxyMesh(group, collisionDirectory);
            if (collisionMesh == null)
            {
                metric.Failure = "Missing COL_ collision proxy.";
                AddViolation(report, group.ModuleName + ": " + metric.Failure);
                return false;
            }

            GameObject meshProxy = new GameObject("COL_" + group.ModuleName);
            meshProxy.transform.SetParent(root.transform, false);
            ResetLocalTransform(meshProxy.transform);
            MeshCollider collider = meshProxy.AddComponent<MeshCollider>();
            collider.sharedMesh = collisionMesh;
            collider.convex = true;
            collider.sharedMaterial = physicsMaterial;
            meshProxy.layer = worldStaticLayer;
            ApplyCollisionStaticFlagsRecursively(meshProxy, worldStaticLayer);
            metric.CollisionProxy = AssetDatabase.GetAssetPath(collisionMesh);
            return ValidateCollisionProxyAndReport(meshProxy, group, report, metric);
        }

        private static bool ValidateCollisionProxyAndReport(GameObject proxyRoot, ModuleMeshGroup group, AssemblerReport report, PrefabMetric metric)
        {
            bool valid = ValidateCollisionProxy(proxyRoot, group, metric);
            if (!valid && !string.IsNullOrEmpty(metric.Failure))
                AddViolation(report, group.ModuleName + ": " + metric.Failure);
            return valid;
        }

        private static void StripRenderableComponentsFromCollisionProxy(GameObject proxyRoot)
        {
            proxyRoot.GetComponentsInChildren(true, s_GenericRendererScratch);
            try
            {
                for (int i = 0; i < s_GenericRendererScratch.Count; i++)
                {
                    Renderer renderer = s_GenericRendererScratch[i];
                    if (renderer != null)
                        Object.DestroyImmediate(renderer);
                }
            }
            finally
            {
                s_GenericRendererScratch.Clear();
            }

            proxyRoot.GetComponentsInChildren(true, s_MeshFilterScratch);
            try
            {
                for (int i = 0; i < s_MeshFilterScratch.Count; i++)
                {
                    MeshFilter filter = s_MeshFilterScratch[i];
                    if (filter != null)
                        Object.DestroyImmediate(filter);
                }
            }
            finally
            {
                s_MeshFilterScratch.Clear();
            }
        }

        private static void EnforceColliderLayerAndMaterial(GameObject root, int layer, PhysicsMaterial material)
        {
            root.GetComponentsInChildren(true, s_ColliderScratch);
            try
            {
                for (int i = 0; i < s_ColliderScratch.Count; i++)
                {
                    Collider collider = s_ColliderScratch[i];
                    if (collider == null)
                        continue;

                    collider.gameObject.layer = layer;
                    collider.sharedMaterial = material;
                }
            }
            finally
            {
                s_ColliderScratch.Clear();
            }
        }

        private static void ApplyVisualStaticFlags(GameObject gameObject)
        {
            GameObjectUtility.SetStaticEditorFlags(
                gameObject,
                StaticEditorFlags.BatchingStatic |
                StaticEditorFlags.OccludeeStatic |
                StaticEditorFlags.ContributeGI);
        }

        private static void ApplyCollisionStaticFlagsRecursively(GameObject root, int layer)
        {
            root.GetComponentsInChildren(true, s_TransformScratch);
            try
            {
                const StaticEditorFlags collisionFlags = StaticEditorFlags.OccludeeStatic;
                for (int i = 0; i < s_TransformScratch.Count; i++)
                {
                    Transform transform = s_TransformScratch[i];
                    if (transform == null)
                        continue;

                    GameObject gameObject = transform.gameObject;
                    gameObject.layer = layer;
                    GameObjectUtility.SetStaticEditorFlags(gameObject, collisionFlags);
                }
            }
            finally
            {
                s_TransformScratch.Clear();
            }
        }

        private static bool ValidateCollisionProxy(GameObject proxyRoot, ModuleMeshGroup group, PrefabMetric metric)
        {
            if (!IsIdentityLocalTransform(proxyRoot.transform))
            {
                metric.Failure = "Collision proxy root transform is not identity.";
                return false;
            }

            proxyRoot.GetComponentsInChildren(true, s_GenericRendererScratch);
            try
            {
                if (s_GenericRendererScratch.Count > 0)
                {
                    metric.Failure = "Collision proxy contains renderers.";
                    return false;
                }
            }
            finally
            {
                s_GenericRendererScratch.Clear();
            }

            proxyRoot.GetComponentsInChildren(true, s_RigidbodyScratch);
            try
            {
                if (s_RigidbodyScratch.Count > 0)
                {
                    metric.Failure = "Collision proxy contains Rigidbody.";
                    return false;
                }
            }
            finally
            {
                s_RigidbodyScratch.Clear();
            }

            proxyRoot.GetComponentsInChildren(true, s_ColliderScratch);
            try
            {
                if (s_ColliderScratch.Count == 0)
                {
                    metric.Failure = "Collision proxy contains no colliders.";
                    return false;
                }

                metric.ColliderCount = s_ColliderScratch.Count;
                for (int i = 0; i < s_ColliderScratch.Count; i++)
                {
                    Collider collider = s_ColliderScratch[i];
                    if (collider == null)
                        continue;

                    if (collider.gameObject.layer != LayerMask.NameToLayer(WorldStaticLayerName))
                    {
                        metric.Failure = "Collision proxy collider is not on World_Static.";
                        return false;
                    }

                    if (!HasStaticFlags(collider.gameObject, StaticEditorFlags.OccludeeStatic))
                    {
                        metric.Failure = "Collision proxy collider is missing static occludee flag.";
                        return false;
                    }

                    if (collider is BoxCollider || collider is SphereCollider || collider is CapsuleCollider)
                        continue;

                    MeshCollider meshCollider = collider as MeshCollider;
                    if (meshCollider != null)
                    {
                        if (!meshCollider.convex)
                        {
                            metric.Failure = "Collision proxy MeshCollider is not convex.";
                            return false;
                        }

                        if (ReferenceEquals(meshCollider.sharedMesh, group.Lod0) ||
                            ReferenceEquals(meshCollider.sharedMesh, group.Lod1) ||
                            ReferenceEquals(meshCollider.sharedMesh, group.Lod2))
                        {
                            metric.Failure = "Collision proxy references visual LOD mesh.";
                            return false;
                        }

                        continue;
                    }

                    metric.Failure = "Unsupported collider type: " + collider.GetType().Name;
                    return false;
                }
            }
            finally
            {
                s_ColliderScratch.Clear();
            }

            return true;
        }

        private static bool BindModuleMetadata(
            GameObject root,
            ModuleMeshGroup group,
            BaseModuleTemplate moduleTemplate,
            string metadataDirectory,
            string collisionDirectory,
            Bounds bounds,
            bool requireSocketMetadata,
            AssemblerReport report,
            PrefabMetric metric)
        {
            if (!ModuleMetadata.ValidateSocketDataStride(out int socketStride))
            {
                metric.Failure = nameof(ModuleMetadata.ModuleSocketData) + " stride is invalid: " + socketStride.ToString(CultureInfo.InvariantCulture);
                AddViolation(report, group.ModuleName + ": " + metric.Failure);
                return false;
            }

            ModuleMetadata.ModuleSocketData[] sockets = ResolveSocketMetadata(group, moduleTemplate, metadataDirectory, collisionDirectory, bounds, report, metric);
            if ((sockets == null || sockets.Length == 0) && requireSocketMetadata)
            {
                metric.Failure = "Missing socket metadata.";
                AddViolation(report, group.ModuleName + ": " + metric.Failure);
                return false;
            }

            if (!ValidateSocketArray(group, bounds, sockets, out string socketFailure))
            {
                metric.Failure = socketFailure;
                AddViolation(report, group.ModuleName + ": " + metric.Failure);
                return false;
            }

            ModuleMetadata metadata = root.AddComponent<ModuleMetadata>();
            metadata.ConfigureOffline(group.ModuleName, HashAuthoringId(group.ModuleName), bounds, sockets);
            metric.SocketCount = sockets != null ? sockets.Length : 0;
            return true;
        }

        private static ModuleMetadata.ModuleSocketData[] ResolveSocketMetadata(
            ModuleMeshGroup group,
            BaseModuleTemplate template,
            string metadataDirectory,
            string collisionDirectory,
            Bounds bounds,
            AssemblerReport report,
            PrefabMetric metric)
        {
            if (template != null)
            {
                metric.SocketSource = AssetDatabase.GetAssetPath(template);
                return BuildSocketsFromTemplate(group, template);
            }

            SocketMetadataFile file = ResolveJsonSocketMetadata(group, metadataDirectory, report, out string jsonPath);
            if (file != null && file.sockets != null && file.sockets.Length > 0)
            {
                metric.SocketSource = jsonPath;
                return BuildSocketsFromJson(group, file.sockets);
            }

            GameObject proxy = ResolveCollisionProxyPrefab(group, collisionDirectory);
            if (proxy != null)
            {
                proxy.GetComponentsInChildren(true, s_ModuleSocketScratch);
                try
                {
                    if (s_ModuleSocketScratch.Count > 0)
                    {
                        metric.SocketSource = AssetDatabase.GetAssetPath(proxy);
                        return BuildSocketsFromModuleSockets(group, proxy, bounds, s_ModuleSocketScratch);
                    }
                }
                finally
                {
                    s_ModuleSocketScratch.Clear();
                }
            }

            AddViolation(report, group.ModuleName + ": no BaseModuleTemplate/json/ModuleSocket metadata found.");
            return Array.Empty<ModuleMetadata.ModuleSocketData>();
        }

        private static bool AttachRuntimeContracts(
            GameObject root,
            ModuleMeshGroup group,
            BaseModuleTemplate template,
            string metadataDirectory,
            Bounds bounds,
            int worldStaticLayer,
            AssemblerReport report,
            PrefabMetric metric)
        {
            if (template == null)
            {
                AddViolation(report, group.ModuleName + ": no BaseModuleTemplate found; BaseModule runtime contract was not attached.");
                return true;
            }

            BoxCollider interiorTrigger = CreateInteriorTrigger(root, template, bounds, worldStaticLayer);
            BuildableData buildableData = ResolveBuildableData(group, template, metadataDirectory);
            if (buildableData != null)
            {
                ModuleMarker marker = root.AddComponent<ModuleMarker>();
                marker.Initialize(buildableData);
                metric.RuntimeContractSource = AssetDatabase.GetAssetPath(buildableData);
            }
            else
            {
                metric.RuntimeContractSource = AssetDatabase.GetAssetPath(template);
                AddViolation(report, group.ModuleName + ": BaseModuleTemplate found without matching BuildableData; ModuleMarker was not attached.");
            }

            BaseModule baseModule = root.AddComponent<BaseModule>();
            SerializedObject serializedModule = new SerializedObject(baseModule);
            SerializedProperty moduleTemplateProperty = serializedModule.FindProperty("moduleTemplate");
            SerializedProperty interiorTriggerProperty = serializedModule.FindProperty("interiorTrigger");
            SerializedProperty fallbackPowerProperty = serializedModule.FindProperty("fallbackPowerRating");
            SerializedProperty powerPriorityProperty = serializedModule.FindProperty("powerPriority");
            if (moduleTemplateProperty == null || interiorTriggerProperty == null || fallbackPowerProperty == null || powerPriorityProperty == null)
            {
                metric.Failure = "BaseModule serialized contract changed.";
                AddViolation(report, group.ModuleName + ": " + metric.Failure);
                return false;
            }

            moduleTemplateProperty.objectReferenceValue = template;
            interiorTriggerProperty.objectReferenceValue = interiorTrigger;
            if (buildableData != null)
            {
                fallbackPowerProperty.floatValue = buildableData.powerRating;
                powerPriorityProperty.intValue = buildableData.powerPriority;
            }

            serializedModule.ApplyModifiedPropertiesWithoutUndo();
            return true;
        }

        private static BoxCollider CreateInteriorTrigger(GameObject root, BaseModuleTemplate template, Bounds bounds, int worldStaticLayer)
        {
            GameObject trigger = new GameObject("InteriorTrigger");
            trigger.layer = worldStaticLayer >= 0 ? worldStaticLayer : root.layer;
            trigger.transform.SetParent(root.transform, false);
            trigger.transform.localPosition = Vector3.zero;
            trigger.transform.localRotation = Quaternion.identity;
            trigger.transform.localScale = Vector3.one;

            BoxCollider collider = trigger.AddComponent<BoxCollider>();
            Vector3 center = template != null ? template.ProxyBoundsCenter : bounds.center;
            Vector3 size = template != null ? template.ProxyBoundsSize : bounds.size;
            if (!IsFinite(center))
                center = Vector3.zero;
            if (!IsFinite(size) || size.x <= 0.01f || size.y <= 0.01f || size.z <= 0.01f)
                size = Vector3.Max(bounds.size, Vector3.one);

            collider.center = center;
            collider.size = new Vector3(
                Mathf.Max(0.5f, size.x * 0.82f),
                Mathf.Max(0.5f, size.y * 0.78f),
                Mathf.Max(0.5f, size.z * 0.82f));
            collider.isTrigger = true;
            return collider;
        }

        private static ModuleMetadata.ModuleSocketData[] BuildSocketsFromTemplate(ModuleMeshGroup group, BaseModuleTemplate template)
        {
            BaseModuleTemplate.SocketDefinition[] definitions = template.SocketDefinitions;
            if (definitions != null && definitions.Length > 0)
            {
                ModuleMetadata.ModuleSocketData[] result = new ModuleMetadata.ModuleSocketData[definitions.Length];
                for (int i = 0; i < definitions.Length; i++)
                {
                    BaseModuleTemplate.SocketDefinition definition = definitions[i];
                    Vector3 localPosition = definition.LocalPosition;
                    Vector3 forward = DirectionToVector(definition.Direction);
                    result[i] = BuildSocketData(
                        group.ModuleName,
                        (ushort)i,
                        localPosition,
                        forward,
                        BaseModuleCatalogRuntime.ComputeCompatibilityMask(definition.CompatibleType),
                        (byte)definition.Direction);
                }

                return result;
            }

            float3[] snapPoints = template.SnapPoints;
            if (snapPoints == null || snapPoints.Length == 0)
                return Array.Empty<ModuleMetadata.ModuleSocketData>();

            ModuleMetadata.ModuleSocketData[] fallback = new ModuleMetadata.ModuleSocketData[snapPoints.Length];
            for (int i = 0; i < snapPoints.Length; i++)
            {
                Vector3 localPosition = snapPoints[i];
                ModuleSocketDirection direction = QuantizeDirection(localPosition);
                fallback[i] = BuildSocketData(
                    group.ModuleName,
                    (ushort)i,
                    localPosition,
                    DirectionToVector(direction),
                    BaseModuleCatalogRuntime.UniversalConnectionMask,
                    (byte)direction);
            }

            return fallback;
        }

        private static ModuleMetadata.ModuleSocketData[] BuildSocketsFromJson(ModuleMeshGroup group, SocketMetadataEntry[] entries)
        {
            ModuleMetadata.ModuleSocketData[] result = new ModuleMetadata.ModuleSocketData[entries.Length];
            for (int i = 0; i < entries.Length; i++)
            {
                SocketMetadataEntry entry = entries[i];
                Vector3 localPosition = entry.localPosition;
                Vector3 forward = IsFinite(entry.forward) && entry.forward.sqrMagnitude > 0.0001f ? entry.forward.normalized : DirectionToVector((ModuleSocketDirection)entry.direction);
                uint connector = entry.connectorMask != 0u ? entry.connectorMask : BaseModuleCatalogRuntime.ComputeCompatibilityMask(entry.compatibleType);
                byte direction = entry.direction <= (byte)ModuleSocketDirection.Bottom
                    ? entry.direction
                    : (byte)QuantizeDirection(forward);

                result[i] = BuildSocketData(group.ModuleName, (ushort)i, localPosition, forward, connector, direction);
                if (entry.stableHash != 0u)
                    result[i].StableHash = entry.stableHash;
                if (IsFinite(entry.aupX) && IsFinite(entry.aupY) && IsFinite(entry.aupZ))
                {
                    result[i].AupX = entry.aupX;
                    result[i].AupY = entry.aupY;
                    result[i].AupZ = entry.aupZ;
                }
            }

            return result;
        }

        private static ModuleMetadata.ModuleSocketData[] BuildSocketsFromModuleSockets(
            ModuleMeshGroup group,
            GameObject sourceRoot,
            Bounds bounds,
            List<ModuleSocket> sockets)
        {
            ModuleMetadata.ModuleSocketData[] result = new ModuleMetadata.ModuleSocketData[sockets.Count];
            Matrix4x4 rootInverse = sourceRoot.transform.worldToLocalMatrix;
            for (int i = 0; i < sockets.Count; i++)
            {
                ModuleSocket socket = sockets[i];
                if (socket == null)
                    continue;

                Vector3 localPosition = rootInverse.MultiplyPoint3x4(socket.transform.position);
                Vector3 forward = sourceRoot.transform.InverseTransformDirection(socket.transform.forward);
                if (!IsFinite(forward) || forward.sqrMagnitude <= 0.0001f)
                    forward = DirectionToVector(socket.Direction);
                else
                    forward.Normalize();

                result[i] = BuildSocketData(
                    group.ModuleName,
                    (ushort)i,
                    localPosition,
                    forward,
                    BaseModuleCatalogRuntime.ComputeCompatibilityMask(socket.CompatibleType),
                    (byte)socket.Direction);
            }

            return result;
        }

        private static ModuleMetadata.ModuleSocketData BuildSocketData(
            string moduleName,
            ushort ordinal,
            Vector3 localPosition,
            Vector3 forward,
            uint connectorMask,
            byte direction)
        {
            if (!IsFinite(localPosition))
                localPosition = Vector3.zero;

            if (!IsFinite(forward) || forward.sqrMagnitude <= 0.0001f)
                forward = DirectionToVector((ModuleSocketDirection)direction);

            forward.Normalize();
            uint moduleHash = HashAuthoringId(moduleName);
            uint stableHash = moduleHash ^ ((uint)direction * LocHash.FnvPrime) ^ ((uint)ordinal * LocHash.FnvPrime);
            return new ModuleMetadata.ModuleSocketData
            {
                AupX = localPosition.x,
                AupY = localPosition.y,
                AupZ = localPosition.z,
                LocalPosition = localPosition,
                Forward = forward,
                ConnectorMask = connectorMask != 0u ? connectorMask : BaseModuleCatalogRuntime.UniversalConnectionMask,
                StableHash = stableHash,
                ModuleId = (ushort)(moduleHash & ushort.MaxValue),
                Direction = direction,
                Flags = 0,
                Padding = 0u
            };
        }

        private static bool ValidateSocketArray(
            ModuleMeshGroup group,
            Bounds bounds,
            ModuleMetadata.ModuleSocketData[] sockets,
            out string failure)
        {
            failure = string.Empty;
            if (sockets == null || sockets.Length == 0)
                return true;

            if (sockets.Length > MaxSocketCount)
            {
                failure = "Socket metadata exceeds maximum count: " + sockets.Length.ToString(CultureInfo.InvariantCulture);
                return false;
            }

            Bounds expandedBounds = bounds;
            expandedBounds.Expand(SocketBoundsToleranceMeters);
            for (int i = 0; i < sockets.Length; i++)
            {
                ModuleMetadata.ModuleSocketData socket = sockets[i];
                if (!IsFinite(socket.LocalPosition) ||
                    !IsFinite(socket.Forward) ||
                    !IsFinite(socket.AupX) ||
                    !IsFinite(socket.AupY) ||
                    !IsFinite(socket.AupZ))
                {
                    failure = "Socket " + i.ToString(CultureInfo.InvariantCulture) + " contains non-finite data.";
                    return false;
                }

                if (socket.StableHash == 0u)
                {
                    failure = "Socket " + i.ToString(CultureInfo.InvariantCulture) + " has zero stable hash.";
                    return false;
                }

                if (socket.ConnectorMask == 0u)
                {
                    failure = "Socket " + i.ToString(CultureInfo.InvariantCulture) + " has zero connector mask.";
                    return false;
                }

                if (socket.Direction > (byte)ModuleSocketDirection.Bottom)
                {
                    failure = "Socket " + i.ToString(CultureInfo.InvariantCulture) + " direction is out of range.";
                    return false;
                }

                float forwardLengthSq = socket.Forward.sqrMagnitude;
                if (forwardLengthSq < 0.80f || forwardLengthSq > 1.20f)
                {
                    failure = "Socket " + i.ToString(CultureInfo.InvariantCulture) + " forward is not normalized.";
                    return false;
                }

                if (!expandedBounds.Contains(socket.LocalPosition))
                {
                    failure = "Socket " + i.ToString(CultureInfo.InvariantCulture) + " is outside mesh bounds for " + group.ModuleName + ".";
                    return false;
                }

                for (int j = i + 1; j < sockets.Length; j++)
                {
                    if (socket.StableHash == sockets[j].StableHash)
                    {
                        failure = "Duplicate socket stable hash at indices " + i.ToString(CultureInfo.InvariantCulture) + " and " + j.ToString(CultureInfo.InvariantCulture) + ".";
                        return false;
                    }
                }
            }

            return true;
        }

        private static bool ValidateRoot(GameObject root, ModuleMeshGroup group, AssemblerReport report, PrefabMetric metric, out string failure)
        {
            if (root.GetComponent<MeshFilter>() != null)
            {
                failure = "Root owns MeshFilter.";
                return false;
            }

            LODGroup lodGroup = root.GetComponent<LODGroup>();
            if (lodGroup == null)
            {
                failure = "Missing LODGroup.";
                return false;
            }

            if (!IsIdentityLocalTransform(root.transform))
            {
                failure = "Root transform is not identity.";
                return false;
            }

            if (lodGroup.fadeMode != LODFadeMode.CrossFade || !lodGroup.animateCrossFading)
            {
                failure = "LODGroup crossfade is not configured.";
                return false;
            }

            LOD[] lods = lodGroup.GetLODs();
            if (lods == null || lods.Length != 3)
            {
                failure = "LODGroup does not contain exactly 3 active levels.";
                return false;
            }

            if (!(lods[0].screenRelativeTransitionHeight > lods[1].screenRelativeTransitionHeight &&
                  lods[1].screenRelativeTransitionHeight > lods[2].screenRelativeTransitionHeight &&
                  lods[2].screenRelativeTransitionHeight >= MinimumCullHeight - 0.0001f))
            {
                failure = "LODGroup transition heights are not monotonic.";
                return false;
            }

            if (lods[0].renderers == null || lods[0].renderers.Length == 0)
            {
                failure = "LOD0 renderer array is empty.";
                return false;
            }

            metric.LodRendererCounts = lods[0].renderers.Length.ToString(CultureInfo.InvariantCulture) + "/" +
                                       lods[1].renderers.Length.ToString(CultureInfo.InvariantCulture) + "/" +
                                       lods[2].renderers.Length.ToString(CultureInfo.InvariantCulture);

            root.GetComponentsInChildren(true, s_RendererScratch);
            try
            {
                for (int i = 0; i < s_RendererScratch.Count; i++)
                {
                    MeshRenderer renderer = s_RendererScratch[i];
                    if (renderer == null)
                        continue;

                    if (!IsIdentityLocalTransform(renderer.transform))
                    {
                        failure = renderer.name + " transform is not identity.";
                        return false;
                    }

                    const StaticEditorFlags requiredFlags =
                        StaticEditorFlags.BatchingStatic |
                        StaticEditorFlags.OccludeeStatic |
                        StaticEditorFlags.ContributeGI;
                    if (!HasStaticFlags(renderer.gameObject, requiredFlags))
                    {
                        failure = renderer.name + " is missing visual static flags.";
                        return false;
                    }

                    Material[] sharedMaterials = renderer.sharedMaterials;
                    if (sharedMaterials == null || sharedMaterials.Length == 0)
                    {
                        failure = renderer.name + " has no shared materials.";
                        return false;
                    }

                    if (sharedMaterials.Length > MaxMaterialSlots)
                    {
                        failure = renderer.name + " exceeds material slot cap.";
                        return false;
                    }

                    for (int slot = 0; slot < sharedMaterials.Length; slot++)
                    {
                        if (sharedMaterials[slot] == null)
                        {
                            failure = renderer.name + " has null material slot " + slot.ToString(CultureInfo.InvariantCulture);
                            return false;
                        }
                    }

                    if (renderer.receiveGI != ReceiveGI.Lightmaps)
                    {
                        failure = renderer.name + " ReceiveGI is not Lightmaps.";
                        return false;
                    }

                    bool lod2 = renderer.transform.name.IndexOf("LOD2", StringComparison.OrdinalIgnoreCase) >= 0;
                    if (lod2 && renderer.shadowCastingMode != ShadowCastingMode.Off)
                    {
                        failure = renderer.name + " LOD2 casts shadows.";
                        return false;
                    }

                    if (!lod2 && renderer.shadowCastingMode != ShadowCastingMode.On)
                    {
                        failure = renderer.name + " LOD0/LOD1 shadow casting is not On.";
                        return false;
                    }
                }
            }
            finally
            {
                s_RendererScratch.Clear();
            }

            root.GetComponentsInChildren(true, s_MeshColliderScratch);
            try
            {
                for (int i = 0; i < s_MeshColliderScratch.Count; i++)
                {
                    MeshCollider collider = s_MeshColliderScratch[i];
                    if (collider == null)
                        continue;

                    if (!collider.convex)
                    {
                        failure = "Non-convex MeshCollider found on " + collider.name;
                        return false;
                    }

                    if (ReferenceEquals(collider.sharedMesh, group.Lod0) ||
                        ReferenceEquals(collider.sharedMesh, group.Lod1) ||
                        ReferenceEquals(collider.sharedMesh, group.Lod2))
                    {
                        failure = "MeshCollider references visual LOD mesh on " + collider.name;
                        return false;
                    }
                }
            }
            finally
            {
                s_MeshColliderScratch.Clear();
            }

            failure = string.Empty;
            report.LodGroupsValidated++;
            return true;
        }

        private static bool IsIdentityLocalTransform(Transform transform)
        {
            Vector3 position = transform.localPosition;
            Vector3 scale = transform.localScale;
            Quaternion rotation = transform.localRotation;
            return position.sqrMagnitude <= 0.0000001f &&
                   Mathf.Abs(rotation.x) <= 0.000001f &&
                   Mathf.Abs(rotation.y) <= 0.000001f &&
                   Mathf.Abs(rotation.z) <= 0.000001f &&
                   Mathf.Abs(rotation.w - 1f) <= 0.000001f &&
                   Mathf.Abs(scale.x - 1f) <= 0.000001f &&
                   Mathf.Abs(scale.y - 1f) <= 0.000001f &&
                   Mathf.Abs(scale.z - 1f) <= 0.000001f;
        }

        private static bool HasStaticFlags(GameObject gameObject, StaticEditorFlags requiredFlags)
        {
            StaticEditorFlags flags = GameObjectUtility.GetStaticEditorFlags(gameObject);
            return (flags & requiredFlags) == requiredFlags;
        }

        private static bool ValidateSavedPrefab(GameObject prefab, ModuleMeshGroup group, AssemblerReport report, PrefabMetric metric, out string failure)
        {
            bool valid = ValidateRoot(prefab, group, report, metric, out failure);
            if (valid)
                report.PrefabValidatorPasses++;
            return valid;
        }

        private static List<ModuleMeshGroup> DiscoverMeshGroups(string directory, AssemblerReport report)
        {
            var groups = new Dictionary<string, ModuleMeshGroup>(StringComparer.OrdinalIgnoreCase);
            string[] guids = AssetDatabase.FindAssets("t:Mesh", new[] { directory });
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if (mesh == null)
                    continue;

                string assetName = Path.GetFileNameWithoutExtension(path);
                if (!TryExtractLod(assetName, out string baseName, out int lodIndex))
                {
                    if (assetName.LastIndexOf("_LOD", StringComparison.OrdinalIgnoreCase) >= 0)
                        AddViolation(report, "Invalid generated structure mesh name: " + assetName);
                    continue;
                }

                string moduleName = NormalizeModuleName(baseName);
                if (!groups.TryGetValue(moduleName, out ModuleMeshGroup group))
                {
                    group = new ModuleMeshGroup
                    {
                        BaseName = baseName,
                        ModuleName = moduleName
                    };
                    groups.Add(moduleName, group);
                }

                if (lodIndex == 0)
                {
                    if (group.Lod0 != null)
                    {
                        group.DuplicateLodDetected = true;
                        AddViolation(report, moduleName + ": duplicate LOD0 mesh assets.");
                        continue;
                    }

                    group.Lod0 = mesh;
                    group.Lod0Path = path;
                }
                else if (lodIndex == 1)
                {
                    if (group.Lod1 != null)
                    {
                        group.DuplicateLodDetected = true;
                        AddViolation(report, moduleName + ": duplicate LOD1 mesh assets.");
                        continue;
                    }

                    group.Lod1 = mesh;
                    group.Lod1Path = path;
                }
                else if (lodIndex == 2)
                {
                    if (group.Lod2 != null)
                    {
                        group.DuplicateLodDetected = true;
                        AddViolation(report, moduleName + ": duplicate LOD2 mesh assets.");
                        continue;
                    }

                    group.Lod2 = mesh;
                    group.Lod2Path = path;
                }
            }

            var result = new List<ModuleMeshGroup>(groups.Count);
            foreach (KeyValuePair<string, ModuleMeshGroup> pair in groups)
            {
                if (pair.Value.Lod0 != null)
                    result.Add(pair.Value);
                else
                    AddViolation(report, pair.Value.ModuleName + ": LOD group discovered without LOD0.");
            }

            result.Sort((lhs, rhs) => string.Compare(lhs.ModuleName, rhs.ModuleName, StringComparison.OrdinalIgnoreCase));
            return result;
        }

        private static bool TryExtractLod(string assetName, out string baseName, out int lodIndex)
        {
            baseName = string.Empty;
            lodIndex = -1;

            int lodMarker = assetName.LastIndexOf("_LOD", StringComparison.OrdinalIgnoreCase);
            if (assetName.StartsWith("MESH_", StringComparison.OrdinalIgnoreCase))
            {
                if (lodMarker < 0 || lodMarker + 5 != assetName.Length)
                    return false;

                char digit = assetName[lodMarker + 4];
                if (digit < '0' || digit > '2')
                    return false;

                lodIndex = digit - '0';
                baseName = assetName.Substring(0, lodMarker);
                return !string.IsNullOrWhiteSpace(baseName);
            }

            const string moduleArchitectLod1 = "_LOD1_Mesh";
            const string moduleArchitectLod2 = "_LOD2_Mesh";
            const string moduleArchitectLod0 = "_Mesh";
            if (assetName.EndsWith(moduleArchitectLod1, StringComparison.OrdinalIgnoreCase))
            {
                lodIndex = 1;
                baseName = assetName.Substring(0, assetName.Length - moduleArchitectLod1.Length);
                return !string.IsNullOrWhiteSpace(baseName);
            }

            if (assetName.EndsWith(moduleArchitectLod2, StringComparison.OrdinalIgnoreCase))
            {
                lodIndex = 2;
                baseName = assetName.Substring(0, assetName.Length - moduleArchitectLod2.Length);
                return !string.IsNullOrWhiteSpace(baseName);
            }

            if (lodMarker < 0 && assetName.EndsWith(moduleArchitectLod0, StringComparison.OrdinalIgnoreCase))
            {
                lodIndex = 0;
                baseName = assetName.Substring(0, assetName.Length - moduleArchitectLod0.Length);
                return !string.IsNullOrWhiteSpace(baseName);
            }

            return !string.IsNullOrWhiteSpace(baseName);
        }

        private static Bounds CombineBounds(ModuleMeshGroup group)
        {
            Bounds bounds = group.Lod0.bounds;
            if (group.Lod1 != null)
                bounds.Encapsulate(group.Lod1.bounds);
            if (group.Lod2 != null)
                bounds.Encapsulate(group.Lod2.bounds);
            return bounds;
        }

        private static GameObject ResolveCollisionProxyPrefab(ModuleMeshGroup group, string collisionDirectory)
        {
            string[] roots = BuildSearchRoots(collisionDirectory, DefaultCollisionDirectory);
            string[] queries =
            {
                "COL_" + group.ModuleName + " t:Prefab",
                group.ModuleName + "_COL t:Prefab",
                "COL_" + group.BaseName + " t:Prefab",
                group.BaseName + "_COL t:Prefab",
                group.ModuleName + " t:Prefab",
                group.BaseName + " t:Prefab",
                "PFB_" + group.ModuleName + " t:Prefab"
            };

            for (int i = 0; i < queries.Length; i++)
            {
                string[] guids = AssetDatabase.FindAssets(queries[i], roots);
                for (int j = 0; j < guids.Length; j++)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guids[j]);
                    GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (prefab != null && IsCollisionProxyCandidate(prefab, group))
                        return prefab;
                }
            }

            return null;
        }

        private static Mesh ResolveCollisionProxyMesh(ModuleMeshGroup group, string collisionDirectory)
        {
            string[] roots = BuildSearchRoots(collisionDirectory, DefaultCollisionDirectory);
            string[] queries =
            {
                "COL_" + group.ModuleName + " t:Mesh",
                group.ModuleName + "_COL t:Mesh",
                "COL_" + group.BaseName + " t:Mesh",
                group.BaseName + "_COL t:Mesh"
            };

            for (int i = 0; i < queries.Length; i++)
            {
                string[] guids = AssetDatabase.FindAssets(queries[i], roots);
                for (int j = 0; j < guids.Length; j++)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guids[j]);
                    Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                    if (mesh != null && IsCollisionName(mesh.name, group))
                        return mesh;
                }
            }

            return null;
        }

        private static BaseModuleTemplate ResolveBaseModuleTemplate(ModuleMeshGroup group, string metadataDirectory)
        {
            string[] roots = BuildMetadataSearchRoots(group, metadataDirectory);
            string[] guids = AssetDatabase.FindAssets("t:BaseModuleTemplate", roots);
            string moduleKey = NormalizeForMatch(group.ModuleName);
            BaseModuleTemplate best = null;
            int bestScore = -1;
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                BaseModuleTemplate template = AssetDatabase.LoadAssetAtPath<BaseModuleTemplate>(path);
                if (template == null)
                    continue;

                string templateKey = NormalizeForMatch(template.name);
                string stableKey = NormalizeForMatch(template.PersistentId);
                int score = ScoreNameMatch(moduleKey, templateKey, stableKey);
                if (score > bestScore)
                {
                    bestScore = score;
                    best = template;
                }
            }

            return bestScore > 0 ? best : null;
        }

        private static BuildableData ResolveBuildableData(ModuleMeshGroup group, BaseModuleTemplate template, string metadataDirectory)
        {
            string[] roots = BuildMetadataSearchRoots(group, metadataDirectory);
            string[] guids = AssetDatabase.FindAssets("t:BuildableData", roots);
            string moduleKey = NormalizeForMatch(group.ModuleName);
            BuildableData best = null;
            int bestScore = -1;
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                BuildableData data = AssetDatabase.LoadAssetAtPath<BuildableData>(path);
                if (data == null)
                    continue;

                int score = 0;
                if (template != null && data.ModuleTemplate == template)
                    score += 1000;

                score += ScoreNameMatch(
                    moduleKey,
                    NormalizeForMatch(data.name),
                    NormalizeForMatch(data.PersistentId));

                if (score > bestScore)
                {
                    bestScore = score;
                    best = data;
                }
            }

            return bestScore > 0 ? best : null;
        }

        private static SocketMetadataFile ResolveJsonSocketMetadata(ModuleMeshGroup group, string metadataDirectory, AssemblerReport report, out string path)
        {
            path = string.Empty;
            string[] roots = BuildMetadataSearchRoots(group, metadataDirectory);
            string[] guids = AssetDatabase.FindAssets(group.ModuleName, roots);
            string moduleKey = NormalizeForMatch(group.ModuleName);
            for (int i = 0; i < guids.Length; i++)
            {
                string candidate = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (!candidate.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                    continue;

                string nameKey = NormalizeForMatch(Path.GetFileNameWithoutExtension(candidate));
                if (!nameKey.Contains(moduleKey) && !moduleKey.Contains(nameKey))
                    continue;

                SocketMetadataFile file;
                try
                {
                    string fullPath = Path.GetFullPath(candidate);
                    string json = File.ReadAllText(fullPath);
                    file = JsonUtility.FromJson<SocketMetadataFile>(json);
                }
                catch (Exception exception)
                {
                    AddViolation(report, group.ModuleName + ": socket metadata JSON is unreadable at " + candidate + " (" + exception.GetType().Name + ").");
                    continue;
                }

                if (file != null && file.sockets != null && file.sockets.Length > 0)
                {
                    path = candidate;
                    return file;
                }
            }

            return null;
        }

        private static PhysicsMaterial ResolvePhysicsMaterial(AssemblerReport report)
        {
            string[] guids = AssetDatabase.FindAssets(PhysicsMaterialName + " t:PhysicsMaterial", new[] { "Assets/_Project" });
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                PhysicsMaterial material = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(path);
                if (material != null)
                    return material;
            }

            AddViolation(report, "Missing pre-existing PhysicsMaterial: " + PhysicsMaterialName);
            return null;
        }

        private static bool IsSrpBatcherCandidate(Material material, out string proof)
        {
            proof = "null material";
            if (material == null || material.shader == null)
                return false;

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

            string shaderPath = AssetDatabase.GetAssetPath(material.shader);
            if (!string.IsNullOrEmpty(shaderPath))
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

        private static void FinalizeInMemoryReport(AssemblerReport report, long elapsedTicks)
        {
            report.TotalEditorMicroseconds = TicksToMicroseconds(elapsedTicks);
            report.PrefabsFailed = report.PrefabMetrics.Count - report.PrefabsAssembled - report.PrefabsDryRunPassed;
        }

        private static void AddPrefabMetric(AssemblerReport report, PrefabMetric metric)
        {
            if (metric.EditorMicroseconds == 0)
                metric.EditorMicroseconds = 1;
            report.PrefabMetrics.Add(metric);
        }

        private static void AddViolation(AssemblerReport report, string violation)
        {
            if (string.IsNullOrEmpty(violation))
                return;

            report.Violations.Add(violation);
            if (report.ConsoleViolationsLogged < MaxConsoleViolationsPerRun)
            {
                report.ConsoleViolationsLogged++;
                Debug.LogError("Prefab Assembly Violation Detected! " + violation);
            }
        }

        private static string BuildOutputPath(string outputDirectory, string moduleName)
        {
            string cleanName = SanitizeAssetName(moduleName);
            return outputDirectory.TrimEnd('/', '\\') + "/PFB_" + cleanName + ".prefab";
        }

        private static void EnsureAssetFolder(string assetFolder)
        {
            string normalized = assetFolder.Replace('\\', '/').Trim('/');
            string[] parts = normalized.Split('/');
            if (parts.Length == 0 || parts[0] != "Assets")
                throw new InvalidOperationException("Output folder must be inside Assets/: " + assetFolder);

            string current = "Assets";
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }

        private static void ResetLocalTransform(Transform transform)
        {
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
            transform.localScale = Vector3.one;
        }

        private static string NormalizeModuleName(string baseName)
        {
            string value = baseName;
            RemovePrefix(ref value, "MESH_");
            RemovePrefix(ref value, "GEN_");
            RemoveSuffix(ref value, "_Mesh");
            return SanitizeAssetName(value);
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
                return "Unnamed";

            char[] chars = value.ToCharArray();
            char[] invalid = Path.GetInvalidFileNameChars();
            for (int i = 0; i < chars.Length; i++)
            {
                char c = chars[i];
                bool bad = char.IsWhiteSpace(c) || c == '-' || c == '.';
                for (int j = 0; j < invalid.Length && !bad; j++)
                    bad = c == invalid[j];
                if (bad)
                    chars[i] = '_';
            }

            return new string(chars);
        }

        private static string NormalizeForMatch(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            value = value.ToLowerInvariant();
            string[] prefixes = { "basemoduletemplate", "module", "mesh", "gen", "pfb", "pf", "mat" };
            for (int i = 0; i < prefixes.Length; i++)
            {
                if (value.StartsWith(prefixes[i], StringComparison.Ordinal))
                    value = value.Substring(prefixes[i].Length);
            }

            char[] chars = value.ToCharArray();
            int write = 0;
            for (int i = 0; i < chars.Length; i++)
            {
                char c = chars[i];
                if (char.IsLetterOrDigit(c))
                    chars[write++] = c;
            }

            return new string(chars, 0, write);
        }

        private static int ScoreNameMatch(string moduleKey, string templateKey, string stableKey)
        {
            int score = 0;
            if (!string.IsNullOrEmpty(templateKey))
            {
                if (moduleKey == templateKey)
                    score = Math.Max(score, 1000 + templateKey.Length);
                else if (moduleKey.Contains(templateKey) || templateKey.Contains(moduleKey))
                    score = Math.Max(score, 100 + Math.Min(moduleKey.Length, templateKey.Length));
            }

            if (!string.IsNullOrEmpty(stableKey))
            {
                if (moduleKey == stableKey)
                    score = Math.Max(score, 1000 + stableKey.Length);
                else if (moduleKey.Contains(stableKey) || stableKey.Contains(moduleKey))
                    score = Math.Max(score, 100 + Math.Min(moduleKey.Length, stableKey.Length));
            }

            return score;
        }

        private static bool IsCollisionName(string value, ModuleMeshGroup group)
        {
            string name = value ?? string.Empty;
            return name.StartsWith("COL_", StringComparison.OrdinalIgnoreCase) ||
                   name.EndsWith("_COL", StringComparison.OrdinalIgnoreCase) ||
                   name.IndexOf("COL_" + group.ModuleName, StringComparison.OrdinalIgnoreCase) >= 0 ||
                   name.IndexOf(group.ModuleName + "_COL", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsCollisionProxyCandidate(GameObject prefab, ModuleMeshGroup group)
        {
            if (prefab == null)
                return false;

            if (IsCollisionName(prefab.name, group))
                return true;

            if (!IsModuleSourceName(prefab.name, group))
                return false;

            prefab.GetComponentsInChildren(true, s_ColliderScratch);
            try
            {
                for (int i = 0; i < s_ColliderScratch.Count; i++)
                {
                    Collider collider = s_ColliderScratch[i];
                    if (collider != null && IsCollisionName(collider.gameObject.name, group))
                        return true;
                }
            }
            finally
            {
                s_ColliderScratch.Clear();
            }

            return false;
        }

        private static bool IsModuleSourceName(string value, ModuleMeshGroup group)
        {
            string nameKey = NormalizeForMatch(value);
            if (string.IsNullOrEmpty(nameKey))
                return false;

            string moduleKey = NormalizeForMatch(group.ModuleName);
            string baseKey = NormalizeForMatch(group.BaseName);
            return (!string.IsNullOrEmpty(moduleKey) && nameKey == moduleKey) ||
                   (!string.IsNullOrEmpty(baseKey) && nameKey == baseKey);
        }

        private static Vector3 DirectionToVector(ModuleSocketDirection direction)
        {
            float3 normal = BaseModuleCatalogRuntime.DirectionToNormal(direction);
            return new Vector3(normal.x, normal.y, normal.z);
        }

        private static ModuleSocketDirection QuantizeDirection(Vector3 vector)
        {
            float absX = Mathf.Abs(vector.x);
            float absY = Mathf.Abs(vector.y);
            float absZ = Mathf.Abs(vector.z);
            if (absX >= absY && absX >= absZ)
                return vector.x >= 0f ? ModuleSocketDirection.East : ModuleSocketDirection.West;
            if (absY >= absX && absY >= absZ)
                return vector.y >= 0f ? ModuleSocketDirection.Top : ModuleSocketDirection.Bottom;
            return vector.z >= 0f ? ModuleSocketDirection.North : ModuleSocketDirection.South;
        }

        private static uint HashAuthoringId(string value)
        {
            return string.IsNullOrEmpty(value) ? 0u : unchecked((uint)LocHash.Compute(value));
        }

        private static long TicksToMicroseconds(long ticks)
        {
            return (long)((double)ticks * 1000000.0 / Stopwatch.Frequency);
        }

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }

        private static bool IsFinite(Vector3 value)
        {
            return !float.IsNaN(value.x) && !float.IsInfinity(value.x) &&
                   !float.IsNaN(value.y) && !float.IsInfinity(value.y) &&
                   !float.IsNaN(value.z) && !float.IsInfinity(value.z);
        }

        private static MaterialManifest ResolveMaterialManifest(
            ModuleMeshGroup group,
            string metadataDirectory,
            string materialDirectory,
            AssemblerReport report,
            PrefabMetric metric)
        {
            string manifestPath = ResolveMaterialManifestPath(group, metadataDirectory);
            if (string.IsNullOrEmpty(manifestPath))
                return null;

            metric.MaterialSource = manifestPath;
            Material[] materials;
            if (manifestPath.EndsWith(".asset", StringComparison.OrdinalIgnoreCase))
            {
                materials = ResolveManifestAssetMaterials(manifestPath, materialDirectory, report, group);
                if (materials.Length == 0)
                {
                    AddViolation(report, group.ModuleName + ": material manifest contains no slots: " + manifestPath);
                    return MaterialManifest.Invalid(manifestPath);
                }

                if (materials.Length > MaxMaterialSlots)
                    AddViolation(report, group.ModuleName + ": material manifest exceeds slot cap: " + manifestPath);

                return new MaterialManifest(manifestPath, materials);
            }

            string json;
            try
            {
                json = File.ReadAllText(manifestPath);
            }
            catch (Exception exception)
            {
                AddViolation(report, group.ModuleName + ": failed to read material manifest " + manifestPath + ". " + exception.GetType().Name);
                return MaterialManifest.Invalid(manifestPath);
            }

            MaterialManifestFile manifestFile;
            try
            {
                manifestFile = JsonUtility.FromJson<MaterialManifestFile>(json);
            }
            catch (Exception exception)
            {
                AddViolation(report, group.ModuleName + ": failed to parse material manifest " + manifestPath + ". " + exception.GetType().Name);
                return MaterialManifest.Invalid(manifestPath);
            }

            if (manifestFile == null)
            {
                AddViolation(report, group.ModuleName + ": material manifest JSON returned null.");
                return MaterialManifest.Invalid(manifestPath);
            }

            materials = ResolveManifestMaterials(manifestFile, materialDirectory, report, group, manifestPath);
            if (materials.Length == 0)
            {
                AddViolation(report, group.ModuleName + ": material manifest contains no slots: " + manifestPath);
                return MaterialManifest.Invalid(manifestPath);
            }

            if (materials.Length > MaxMaterialSlots)
                AddViolation(report, group.ModuleName + ": material manifest exceeds slot cap: " + manifestPath);

            return new MaterialManifest(manifestPath, materials);
        }

        private static string ResolveMaterialManifestPath(ModuleMeshGroup group, string metadataDirectory)
        {
            string[] roots;
            if (AssetDatabase.IsValidFolder(metadataDirectory))
                roots = new[] { metadataDirectory };
            else if (AssetDatabase.IsValidFolder(DefaultMetadataDirectory))
                roots = new[] { DefaultMetadataDirectory };
            else
                roots = new[] { "Assets/_Project" };

            string[] queries =
            {
                "MANIFEST_" + group.ModuleName,
                "MANIFEST_" + group.BaseName,
                group.ModuleName,
                group.BaseName
            };

            string path = FindMaterialManifestInRoots(group, roots, queries);
            if (!string.IsNullOrEmpty(path))
                return path;

            string lodDirectory = NormalizeAssetDirectory(group.Lod0Path);
            if (!string.IsNullOrEmpty(lodDirectory) && AssetDatabase.IsValidFolder(lodDirectory))
                return FindMaterialManifestInRoots(group, new[] { lodDirectory }, queries);

            return string.Empty;
        }

        private static string FindMaterialManifestInRoots(ModuleMeshGroup group, string[] roots, string[] queries)
        {
            for (int i = 0; i < queries.Length; i++)
            {
                string[] guids = AssetDatabase.FindAssets(queries[i], roots);
                for (int j = 0; j < guids.Length; j++)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guids[j]);
                    if (IsMaterialManifestPath(path, group))
                        return path;
                }
            }

            return string.Empty;
        }

        private static bool IsMaterialManifestPath(string path, ModuleMeshGroup group)
        {
            if (string.IsNullOrEmpty(path) ||
                !(path.EndsWith(".json", StringComparison.OrdinalIgnoreCase) ||
                  path.EndsWith(".asset", StringComparison.OrdinalIgnoreCase)))
                return false;

            string fileName = Path.GetFileNameWithoutExtension(path);
            if (string.IsNullOrEmpty(fileName))
                return false;

            string fileKey = NormalizeForMatch(fileName);
            string moduleKey = NormalizeForMatch(group.ModuleName);
            string baseKey = NormalizeForMatch(group.BaseName);
            bool matchedModule = !string.IsNullOrEmpty(moduleKey) && (fileKey == moduleKey || fileKey.Contains(moduleKey) || moduleKey.Contains(fileKey));
            bool matchedBase = !string.IsNullOrEmpty(baseKey) && (fileKey == baseKey || fileKey.Contains(baseKey) || baseKey.Contains(fileKey));
            return fileName.StartsWith("MANIFEST_", StringComparison.OrdinalIgnoreCase) && (matchedModule || matchedBase);
        }

        private static Material[] ResolveManifestAssetMaterials(
            string manifestPath,
            string materialDirectory,
            AssemblerReport report,
            ModuleMeshGroup group)
        {
            Object asset = AssetDatabase.LoadMainAssetAtPath(manifestPath);
            if (asset == null)
            {
                AddViolation(report, group.ModuleName + ": failed to load material manifest asset " + manifestPath);
                return Array.Empty<Material>();
            }

            SerializedObject serializedObject = new SerializedObject(asset);
            Material[] materials = ResolveSerializedMaterialArray(serializedObject, "slots", materialDirectory);
            if (materials.Length > 0)
                return materials;

            materials = ResolveSerializedMaterialArray(serializedObject, "materialSlots", materialDirectory);
            if (materials.Length > 0)
                return materials;

            materials = ResolveSerializedMaterialArray(serializedObject, "sharedMaterials", materialDirectory);
            if (materials.Length > 0)
                return materials;

            materials = ResolveSerializedMaterialArray(serializedObject, "materialPaths", materialDirectory);
            if (materials.Length > 0)
                return materials;

            materials = ResolveSerializedMaterialArray(serializedObject, "materialGuids", materialDirectory);
            if (materials.Length > 0)
                return materials;

            return ResolveSerializedMaterialArray(serializedObject, "materials", materialDirectory);
        }

        private static Material[] ResolveSerializedMaterialArray(SerializedObject serializedObject, string propertyName, string materialDirectory)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property == null || !property.isArray)
                return Array.Empty<Material>();

            int count = property.arraySize;
            if (count <= 0)
                return Array.Empty<Material>();

            Material[] result = new Material[count];
            for (int i = 0; i < count; i++)
                result[i] = ResolveSerializedMaterialElement(property.GetArrayElementAtIndex(i), materialDirectory);
            return result;
        }

        private static Material ResolveSerializedMaterialElement(SerializedProperty property, string materialDirectory)
        {
            Material material = ResolveSerializedMaterialProperty(property, materialDirectory);
            if (material != null)
                return material;

            if (property == null || property.propertyType != SerializedPropertyType.Generic)
                return null;

            material = ResolveSerializedMaterialProperty(property.FindPropertyRelative("material"), materialDirectory);
            if (material != null)
                return material;
            material = ResolveSerializedMaterialProperty(property.FindPropertyRelative("materialPath"), materialDirectory);
            if (material != null)
                return material;
            material = ResolveSerializedMaterialProperty(property.FindPropertyRelative("assetPath"), materialDirectory);
            if (material != null)
                return material;
            material = ResolveSerializedMaterialProperty(property.FindPropertyRelative("guid"), materialDirectory);
            if (material != null)
                return material;
            material = ResolveSerializedMaterialProperty(property.FindPropertyRelative("name"), materialDirectory);
            if (material != null)
                return material;
            return ResolveSerializedMaterialProperty(property.FindPropertyRelative("role"), materialDirectory);
        }

        private static Material ResolveSerializedMaterialProperty(SerializedProperty property, string materialDirectory)
        {
            if (property == null)
                return null;

            if (property.propertyType == SerializedPropertyType.ObjectReference)
            {
                Material material = property.objectReferenceValue as Material;
                if (material != null)
                    return material;

                string path = AssetDatabase.GetAssetPath(property.objectReferenceValue);
                if (!string.IsNullOrEmpty(path))
                    return AssetDatabase.LoadAssetAtPath<Material>(path);
            }

            if (property.propertyType == SerializedPropertyType.String)
                return ResolveMaterialByReference(property.stringValue, materialDirectory);

            return null;
        }

        private static Material[] ResolveManifestMaterials(
            MaterialManifestFile manifestFile,
            string materialDirectory,
            AssemblerReport report,
            ModuleMeshGroup group,
            string manifestPath)
        {
            if (manifestFile.slots != null && manifestFile.slots.Length > 0)
            {
                Material[] materials = new Material[manifestFile.slots.Length];
                for (int i = 0; i < manifestFile.slots.Length; i++)
                {
                    MaterialManifestSlot slot = manifestFile.slots[i];
                    Material material = ResolveMaterialFromSlot(slot, materialDirectory);
                    materials[i] = material;
                    if (material == null)
                        AddViolation(report, group.ModuleName + ": manifest slot " + i.ToString(CultureInfo.InvariantCulture) + " failed to resolve in " + manifestPath);
                }

                return materials;
            }

            string[] tokens = SelectManifestTokenArray(manifestFile);
            if (tokens == null || tokens.Length == 0)
                return Array.Empty<Material>();

            Material[] resolved = new Material[tokens.Length];
            for (int i = 0; i < tokens.Length; i++)
            {
                resolved[i] = ResolveMaterialByReference(tokens[i], materialDirectory);
                if (resolved[i] == null)
                    AddViolation(report, group.ModuleName + ": manifest material token " + i.ToString(CultureInfo.InvariantCulture) + " failed to resolve in " + manifestPath);
            }

            return resolved;
        }

        private static string[] SelectManifestTokenArray(MaterialManifestFile manifestFile)
        {
            if (manifestFile.materialSlots != null && manifestFile.materialSlots.Length > 0)
                return manifestFile.materialSlots;
            if (manifestFile.sharedMaterials != null && manifestFile.sharedMaterials.Length > 0)
                return manifestFile.sharedMaterials;
            if (manifestFile.materialPaths != null && manifestFile.materialPaths.Length > 0)
                return manifestFile.materialPaths;
            if (manifestFile.materialGuids != null && manifestFile.materialGuids.Length > 0)
                return manifestFile.materialGuids;
            return manifestFile.materials;
        }

        private static Material ResolveMaterialFromSlot(MaterialManifestSlot slot, string materialDirectory)
        {
            Material material = ResolveMaterialByReference(slot.guid, materialDirectory);
            if (material != null)
                return material;
            material = ResolveMaterialByReference(slot.materialPath, materialDirectory);
            if (material != null)
                return material;
            material = ResolveMaterialByReference(slot.assetPath, materialDirectory);
            if (material != null)
                return material;
            material = ResolveMaterialByReference(slot.material, materialDirectory);
            if (material != null)
                return material;
            material = ResolveMaterialByReference(slot.name, materialDirectory);
            if (material != null)
                return material;
            return ResolveMaterialByReference(slot.role, materialDirectory);
        }

        private static Material ResolveMaterialByReference(string reference, string materialDirectory)
        {
            if (string.IsNullOrWhiteSpace(reference))
                return null;

            string token = reference.Trim();
            if (token.StartsWith("guid:", StringComparison.OrdinalIgnoreCase))
                token = token.Substring(5).Trim();

            Material direct = ResolveMaterialDirect(token);
            if (direct != null)
                return direct;

            string[] roots = BuildSearchRoots(materialDirectory, DefaultMaterialDirectory);
            string query = Path.GetFileNameWithoutExtension(token);
            if (string.IsNullOrEmpty(query))
                query = token;

            string[] guids = AssetDatabase.FindAssets(query + " t:Material", roots);
            Material first = null;
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null)
                    continue;

                if (first == null)
                    first = material;
                string materialName = material.name;
                string assetName = Path.GetFileNameWithoutExtension(path);
                if (string.Equals(materialName, query, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(assetName, query, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(path, token, StringComparison.OrdinalIgnoreCase))
                    return material;
            }

            return first;
        }

        private static Material ResolveMaterialDirect(string token)
        {
            if (string.IsNullOrEmpty(token))
                return null;

            if (token.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase) && token.EndsWith(".mat", StringComparison.OrdinalIgnoreCase))
            {
                Material material = AssetDatabase.LoadAssetAtPath<Material>(token);
                if (material != null)
                    return material;
            }

            string assetPath = AssetDatabase.GUIDToAssetPath(token);
            if (!string.IsNullOrEmpty(assetPath))
                return AssetDatabase.LoadAssetAtPath<Material>(assetPath);

            return null;
        }

        private static string NormalizeAssetDirectory(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath))
                return string.Empty;

            string directory = Path.GetDirectoryName(assetPath);
            return string.IsNullOrEmpty(directory) ? string.Empty : directory.Replace('\\', '/');
        }

        private static string[] BuildSearchRoots(string primaryDirectory, string fallbackDirectory)
        {
            if (AssetDatabase.IsValidFolder(primaryDirectory))
                return new[] { primaryDirectory };
            if (AssetDatabase.IsValidFolder(fallbackDirectory))
                return new[] { fallbackDirectory };
            if (AssetDatabase.IsValidFolder("Assets/_Project"))
                return new[] { "Assets/_Project" };
            return new[] { "Assets" };
        }

        private static string[] BuildMetadataSearchRoots(ModuleMeshGroup group, string metadataDirectory)
        {
            string lodDirectory = NormalizeAssetDirectory(group.Lod0Path);
            bool hasMetadata = AssetDatabase.IsValidFolder(metadataDirectory);
            bool hasDefaultMetadata = !string.Equals(metadataDirectory, DefaultMetadataDirectory, StringComparison.Ordinal) &&
                                      AssetDatabase.IsValidFolder(DefaultMetadataDirectory);
            bool hasLodDirectory = AssetDatabase.IsValidFolder(lodDirectory);

            if (hasMetadata && hasDefaultMetadata && hasLodDirectory &&
                !string.Equals(metadataDirectory, lodDirectory, StringComparison.Ordinal) &&
                !string.Equals(DefaultMetadataDirectory, lodDirectory, StringComparison.Ordinal))
                return new[] { metadataDirectory, DefaultMetadataDirectory, lodDirectory };

            if (hasMetadata && hasLodDirectory && !string.Equals(metadataDirectory, lodDirectory, StringComparison.Ordinal))
                return new[] { metadataDirectory, lodDirectory };

            if (hasDefaultMetadata && hasLodDirectory && !string.Equals(DefaultMetadataDirectory, lodDirectory, StringComparison.Ordinal))
                return new[] { DefaultMetadataDirectory, lodDirectory };

            if (hasMetadata)
                return new[] { metadataDirectory };

            if (hasDefaultMetadata)
                return new[] { DefaultMetadataDirectory };

            if (hasLodDirectory)
                return new[] { lodDirectory };

            if (AssetDatabase.IsValidFolder("Assets/_Project"))
                return new[] { "Assets/_Project" };

            return new[] { "Assets" };
        }

        [Serializable]
        public struct AssemblerSettings
        {
            public string MeshDirectory;
            public string MaterialDirectory;
            public string CollisionDirectory;
            public string MetadataDirectory;
            public string OutputDirectory;
            public bool DryRun;
            public bool AllowLod2Fallback;
            public bool RequireSocketMetadata;

            public static AssemblerSettings Default => new AssemblerSettings
            {
                MeshDirectory = DefaultMeshDirectory,
                MaterialDirectory = DefaultMaterialDirectory,
                CollisionDirectory = DefaultCollisionDirectory,
                MetadataDirectory = DefaultMetadataDirectory,
                OutputDirectory = DefaultOutputDirectory,
                DryRun = true,
                AllowLod2Fallback = true,
                RequireSocketMetadata = true
            };
        }

        private sealed class MaterialManifest
        {
            private readonly Material[] slots;

            public MaterialManifest(string sourcePath, Material[] slots)
            {
                SourcePath = sourcePath;
                this.slots = slots ?? Array.Empty<Material>();
            }

            public string SourcePath { get; }

            public bool IsPresent => !string.IsNullOrEmpty(SourcePath);

            public static MaterialManifest Invalid(string sourcePath)
            {
                return new MaterialManifest(sourcePath, Array.Empty<Material>());
            }

            public Material[] BuildMaterialArray(int subMeshCount, string lodName, AssemblerReport report, PrefabMetric metric)
            {
                int count = Mathf.Max(1, subMeshCount);
                Material[] result = new Material[count];
                if (slots.Length == 0)
                {
                    metric.MaterialContractFailed = true;
                    if (string.IsNullOrEmpty(metric.Failure))
                        metric.Failure = "Material manifest has no resolved slots for " + lodName + ".";
                    AddViolation(report, metric.ModuleName + ": material manifest has no resolved slots for " + lodName + ".");
                    return result;
                }

                if (slots.Length < count)
                {
                    metric.MaterialContractFailed = true;
                    if (string.IsNullOrEmpty(metric.Failure))
                        metric.Failure = "Material manifest has too few slots for " + lodName + ".";
                    AddViolation(report, metric.ModuleName + ": material manifest provides " + slots.Length.ToString(CultureInfo.InvariantCulture) + " slots for " + lodName + " requiring " + count.ToString(CultureInfo.InvariantCulture) + ".");
                }

                for (int i = 0; i < count; i++)
                {
                    Material material = i < slots.Length ? slots[i] : null;
                    result[i] = material;
                    if (material == null)
                    {
                        metric.MaterialContractFailed = true;
                        if (string.IsNullOrEmpty(metric.Failure))
                            metric.Failure = "Unresolved material manifest slot on " + lodName + ".";
                        AddViolation(report, metric.ModuleName + ": unresolved material manifest slot " + i.ToString(CultureInfo.InvariantCulture) + " on " + lodName + ".");
                    }
                    else
                        metric.MaterialProofs.Add(lodName + "[" + i.ToString(CultureInfo.InvariantCulture) + "]:" + material.name + ":manifest");
                }

                return result;
            }
        }

        private sealed class MaterialPalette
        {
            private readonly Material structural;
            private readonly Material trim;
            private readonly Material secondary;
            private readonly Material emissive;

            private MaterialPalette(Material structural, Material trim, Material secondary, Material emissive)
            {
                this.structural = structural;
                this.trim = trim ?? structural;
                this.secondary = secondary ?? structural;
                this.emissive = emissive ?? structural;
            }

            public static MaterialPalette Build(string materialDirectory, AssemblerReport report)
            {
                string[] roots = BuildSearchRoots(materialDirectory, DefaultMaterialDirectory);
                string[] guids = AssetDatabase.FindAssets("t:Material", roots);
                Material structural = null;
                Material trim = null;
                Material secondary = null;
                Material emissive = null;

                for (int i = 0; i < guids.Length; i++)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                    Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
                    if (material == null)
                        continue;

                    string name = material.name;
                    if (structural == null && IsStructural(name, material))
                        structural = material;
                    if (trim == null && ContainsAny(name, "Trim", "Wear", "Edge", "Bevel"))
                        trim = material;
                    if (secondary == null && ContainsAny(name, "Interior", "Rubber", "Gasket", "Glass", "Plastic"))
                        secondary = material;
                    if (emissive == null && ContainsAny(name, "Emissive", "Light", "Label", "Screen"))
                        emissive = material;
                }

                if (structural == null && guids.Length > 0)
                {
                    for (int i = 0; i < guids.Length && structural == null; i++)
                    {
                        string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
                        if (material != null && !IsRejectedPrimaryMaterial(material))
                            structural = material;
                    }
                }

                if (structural == null)
                    AddViolation(report, "No structural MAT_ material found in " + materialDirectory);

                return new MaterialPalette(structural, trim, secondary, emissive);
            }

            public Material[] BuildMaterialArray(int subMeshCount)
            {
                int count = Mathf.Max(1, subMeshCount);
                Material[] result = new Material[count];
                for (int i = 0; i < count; i++)
                {
                    switch (i)
                    {
                        case 0:
                            result[i] = structural;
                            break;
                        case 1:
                            result[i] = trim;
                            break;
                        case 2:
                            result[i] = secondary;
                            break;
                        case 3:
                            result[i] = emissive;
                            break;
                        default:
                            result[i] = structural;
                            break;
                    }
                }

                return result;
            }

            private static bool IsStructural(string name, Material material)
            {
                if (IsRejectedPrimaryMaterial(material))
                    return false;

                return ContainsAny(name, "MAT_Outpost_Exterior", "Mat_Module", "MAT_Module", "Construction", "Structural", "Steel", "Metal");
            }

            private static bool ContainsAny(string value, params string[] probes)
            {
                if (string.IsNullOrEmpty(value))
                    return false;

                for (int i = 0; i < probes.Length; i++)
                {
                    if (value.IndexOf(probes[i], StringComparison.OrdinalIgnoreCase) >= 0)
                        return true;
                }

                return false;
            }

            private static bool IsRejectedPrimaryMaterial(Material material)
            {
                if (material == null)
                    return true;

                string name = material.name;
                if (ContainsAny(name, "glass", "transparent", "decal", "label", "emissive", "screen"))
                    return true;

                return material.renderQueue >= 3000 ||
                       material.IsKeywordEnabled("_ALPHABLEND_ON") ||
                       material.IsKeywordEnabled("_SURFACE_TYPE_TRANSPARENT");
            }
        }

        private sealed class ModuleMeshGroup
        {
            public string BaseName;
            public string ModuleName;
            public Mesh Lod0;
            public Mesh Lod1;
            public Mesh Lod2;
            public string Lod0Path;
            public string Lod1Path;
            public string Lod2Path;
            public bool DuplicateLodDetected;
        }

        [Serializable]
        public sealed class AssemblerReport
        {
            public string AgentId;
            public string UtcTimestamp;
            public string MeshDirectory;
            public string MaterialDirectory;
            public string CollisionDirectory;
            public string MetadataDirectory;
            public string OutputDirectory;
            public string LodFormula;
            public bool DryRun;
            public int GroupsDiscovered;
            public int PrefabsAssembled;
            public int PrefabsDryRunPassed;
            public int PrefabsFailed;
            public int PrefabValidatorPasses;
            public int LodGroupsValidated;
            public int ConsoleViolationsLogged;
            public long TotalEditorMicroseconds;
            public List<string> Violations = new List<string>(32);
            public List<PrefabMetric> PrefabMetrics = new List<PrefabMetric>(64);
        }

        [Serializable]
        public sealed class PrefabMetric
        {
            public string ModuleName;
            public string Status;
            public string Failure;
            public string SourceLod0;
            public string SourceLod1;
            public string SourceLod2;
            public string OutputPrefab;
            public string CollisionProxy;
            public string MaterialSource;
            public string SocketSource;
            public string RuntimeContractSource;
            public string LodRendererCounts;
            public bool Lod2Fallback;
            public bool MaterialContractFailed;
            public int ColliderCount;
            public int SocketCount;
            public float BoundsDiagonalMeters;
            public float Lod0Height;
            public float Lod1Height;
            public float Lod2Height;
            public long EditorMicroseconds;
            public List<string> MaterialProofs = new List<string>(8);
        }

        [Serializable]
        private sealed class SocketMetadataFile
        {
            public SocketMetadataEntry[] sockets;
        }

        [Serializable]
        private sealed class MaterialManifestFile
        {
            public string[] materialSlots;
            public string[] sharedMaterials;
            public string[] materialPaths;
            public string[] materialGuids;
            public string[] materials;
            public MaterialManifestSlot[] slots;
        }

        [Serializable]
        private struct MaterialManifestSlot
        {
            public string name;
            public string role;
            public string material;
            public string materialPath;
            public string assetPath;
            public string guid;
        }

        [Serializable]
        private struct SocketMetadataEntry
        {
            public Vector3 localPosition;
            public Vector3 forward;
            public double aupX;
            public double aupY;
            public double aupZ;
            public string compatibleType;
            public uint connectorMask;
            public uint stableHash;
            public byte direction;
        }
    }
}
#endif
