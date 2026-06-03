#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using Hecton8.Construction;
using Hecton8.Core;
using Hecton8.Interaction;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

namespace Hecton8.Editor.Assembly
{
    public sealed class LogisticsPrefabFactory : EditorWindow
    {
        private const string AgentId = "1737";
        private const string MenuRoot = "HECTON-8/Assembly/1737/";
        private const string DefaultSourceDirectory = "Assets/_Project/BakedGeometry/Logistics";
        private const string DefaultMetadataDirectory = "Assets/_Project/BakedGeometry/Logistics/Metadata";
        private const string DefaultCollisionDirectory = "Assets/_Project/BakedGeometry/Logistics/Collision";
        private const string DefaultMaterialDirectory = "Assets/_Project/Art/Materials";
        private const string DefaultOutputDirectory = "Assets/_Project/Prefabs/Construction/Final";
        private const string EquipmentAtlasToken = "Equipment_Atlas";
        private const string RustedMetalToken = "Metal_Rusted";
        private const string InteractableLayerName = "Interactable";
        private const string PumpRelayEmissionProperty = "_EmissionStrength";
        private const int MaxMaterialSlots = 4;
        private const float DefaultBaseCapacity = 1f;
        private const float DefaultMaxPressureKPa = 160f;
        private const float DefaultBaseResistance = 1f;
        private const float DefaultValveInteractionRadiusMeters = 0.18f;

        [SerializeField] private string sourceDirectory = DefaultSourceDirectory;
        [SerializeField] private string metadataDirectory = DefaultMetadataDirectory;
        [SerializeField] private string collisionDirectory = DefaultCollisionDirectory;
        [SerializeField] private string materialDirectory = DefaultMaterialDirectory;
        [SerializeField] private string outputDirectory = DefaultOutputDirectory;
        [SerializeField] private bool dryRun = true;
        [SerializeField] private int maxGroupsPerRun = 512;

        private Vector2 _scroll;
        private FactoryReport _lastReport;

        private static readonly List<SourceGroup> s_groups = new List<SourceGroup>(256);
        private static readonly List<string> s_assetPaths = new List<string>(1024);
        private static readonly List<string> s_violations = new List<string>(256);
        private static readonly List<Renderer> s_rendererScratch = new List<Renderer>(64);
        private static readonly List<Collider> s_colliderScratch = new List<Collider>(64);
        private static readonly List<MeshCollider> s_meshColliderScratch = new List<MeshCollider>(8);
        private static readonly List<ParticleSystem> s_particleScratch = new List<ParticleSystem>(8);
        private static readonly List<NetworkNodeData> s_nodeScratch = new List<NetworkNodeData>(8);
        private static readonly List<ValveMetadata> s_valveScratch = new List<ValveMetadata>(4);
        private static readonly List<ValveWheelInteractable> s_valveInteractableScratch = new List<ValveWheelInteractable>(4);
        private static readonly List<Material> s_materialScratch = new List<Material>(MaxMaterialSlots);

        [Serializable]
        public sealed class FactoryReport
        {
            public string agentId = AgentId;
            public string generatedUtc;
            public string sourceDirectory;
            public string metadataDirectory;
            public string collisionDirectory;
            public string materialDirectory;
            public string outputDirectory;
            public bool dryRun;
            public int sourceGroups;
            public int prefabsAssembled;
            public int prefabsFailed;
            public int looseMeshCollidersFound;
            public int meshCollidersRejected;
            public int primitiveColliderCount;
            public int nodeMetadataValidations;
            public int valveMetadataValidations;
            public int srpBatcherProofCount;
            public int emissionStrengthProofCount;
            public long totalEditorMicroseconds;
            public string collisionPolicy = "primitive-only final prefab gate: BoxCollider/CapsuleCollider/SphereCollider accepted; MeshCollider rejected.";
            public string runtimePolicy = "factory serializes NetworkNodeData/ValveMetadata only; Jacobi graph truth remains runtime service-owned.";
            public List<PrefabMetric> prefabs = new List<PrefabMetric>(256);
            public List<string> violations = new List<string>(256);
        }

        [Serializable]
        public sealed class PrefabMetric
        {
            public string prefabName;
            public string sourcePath;
            public string metadataPath;
            public string collisionPath;
            public string outputPath;
            public string networkTypeID;
            public string nodeTypeID;
            public float baseCapacity;
            public float baseResistance;
            public float maxPressureKPa;
            public int portCount;
            public int colliderCount;
            public int rendererCount;
            public int srpBatcherProofCount;
            public int emissionStrengthProofCount;
            public int valveHandleCount;
            public uint prefabHash;
            public long editorMicroseconds;
            public string status;
            public string failure;
        }

        [Serializable]
        public sealed class FactorySettings
        {
            public string SourceDirectory;
            public string MetadataDirectory;
            public string CollisionDirectory;
            public string MaterialDirectory;
            public string OutputDirectory;
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
                        CollisionDirectory = DefaultCollisionDirectory,
                        MaterialDirectory = DefaultMaterialDirectory,
                        OutputDirectory = DefaultOutputDirectory,
                        DryRun = true,
                        MaxGroupsPerRun = 512
                    };
                }
            }

            public FactorySettings Sanitize()
            {
                SourceDirectory = SanitizeAssetPath(SourceDirectory, DefaultSourceDirectory);
                MetadataDirectory = SanitizeAssetPath(MetadataDirectory, DefaultMetadataDirectory);
                CollisionDirectory = SanitizeAssetPath(CollisionDirectory, DefaultCollisionDirectory);
                MaterialDirectory = SanitizeAssetPath(MaterialDirectory, DefaultMaterialDirectory);
                OutputDirectory = SanitizeAssetPath(OutputDirectory, DefaultOutputDirectory);
                MaxGroupsPerRun = math.clamp(MaxGroupsPerRun, 1, 4096);
                return this;
            }
        }

        private sealed class SourceGroup
        {
            public string Name;
            public string SourcePath;
            public Mesh Mesh;
            public GameObject PrefabSource;
        }

        private sealed class MaterialSet
        {
            public Material EquipmentAtlas;
            public Material MetalRusted;
        }

        [Serializable]
        private sealed class LogisticsMetadataFile
        {
            public string nodeTypeID;
            public string networkTypeID;
            public float baseCapacity = DefaultBaseCapacity;
            public float baseResistance = DefaultBaseResistance;
            public float maxPressureKPa = DefaultMaxPressureKPa;
            public int priority;
            public int flags;
            public float initialVisualLoad01;
            public float baseEmissionStrength = 0.35f;
            public float maxEmissionStrength = 2.5f;
            public LogisticsPortJson[] ports;
            public ValveHandleJson[] valveHandles;
        }

        [Serializable]
        private sealed class LogisticsPortJson
        {
            public int portID;
            public string portTypeID;
            public Vector3 localPosition;
            public Vector3 localDirection = Vector3.forward;
            public float capacityScale = 1f;
        }

        [Serializable]
        private sealed class ValveHandleJson
        {
            public string wheelVisualName;
            public string ikHandleName;
            public Vector3 localPosition;
            public Vector3 localAxis = Vector3.forward;
            public float minAngleDegrees;
            public float maxAngleDegrees = 90f;
        }

        [MenuItem(MenuRoot + "Open Logistics Prefab Factory", false, 1737)]
        public static void OpenWindow()
        {
            LogisticsPrefabFactory window = GetWindow<LogisticsPrefabFactory>("Logistics Factory 1737");
            window.minSize = new Vector2(720f, 520f);
            window.Show();
        }

        [MenuItem(MenuRoot + "Dry Run Static Audit", false, 1738)]
        public static void DryRunDefault()
        {
            FactorySettings settings = FactorySettings.Default;
            settings.DryRun = true;
            FactoryReport report = Run(settings);
            Debug.Log("[LogisticsPrefabFactory1737] dryRun groups=" + report.sourceGroups.ToString(CultureInfo.InvariantCulture) +
                      " assembled=" + report.prefabsAssembled.ToString(CultureInfo.InvariantCulture) +
                      " failed=" + report.prefabsFailed.ToString(CultureInfo.InvariantCulture) +
                      " violations=" + report.violations.Count.ToString(CultureInfo.InvariantCulture));
        }

        [MenuItem(MenuRoot + "Run Factory", false, 1739)]
        public static void RunDefault()
        {
            FactorySettings settings = FactorySettings.Default;
            settings.DryRun = false;
            Run(settings);
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("HECTON-8 Logistics Prefab Factory 1737", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Offline assembler for pipe, valve, pump, relay and junction prefabs. Writes NetworkNodeData + ValveMetadata, primitive collider proxies, and SRP/material proof metrics.", MessageType.Info);
            sourceDirectory = EditorGUILayout.TextField("Source Mesh/Prefab Folder", sourceDirectory);
            metadataDirectory = EditorGUILayout.TextField("Wave 2 Metadata Folder", metadataDirectory);
            collisionDirectory = EditorGUILayout.TextField("COL_ Proxy Folder", collisionDirectory);
            materialDirectory = EditorGUILayout.TextField("Material Folder", materialDirectory);
            outputDirectory = EditorGUILayout.TextField("Output Folder", outputDirectory);
            dryRun = EditorGUILayout.Toggle("Dry Run", dryRun);
            maxGroupsPerRun = EditorGUILayout.IntSlider("Max Groups", maxGroupsPerRun, 1, 4096);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Dry Run", GUILayout.Height(30f)))
                _lastReport = Run(BuildSettings(true));
            if (GUILayout.Button("Assemble Prefabs", GUILayout.Height(30f)))
                _lastReport = Run(BuildSettings(false));
            EditorGUILayout.EndHorizontal();

            if (_lastReport == null)
                return;

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            EditorGUILayout.LabelField("Groups", _lastReport.sourceGroups.ToString(CultureInfo.InvariantCulture));
            EditorGUILayout.LabelField("Assembled", _lastReport.prefabsAssembled.ToString(CultureInfo.InvariantCulture));
            EditorGUILayout.LabelField("Failed", _lastReport.prefabsFailed.ToString(CultureInfo.InvariantCulture));
            EditorGUILayout.LabelField("Primitive Colliders", _lastReport.primitiveColliderCount.ToString(CultureInfo.InvariantCulture));
            EditorGUILayout.LabelField("Mesh Colliders Rejected", _lastReport.meshCollidersRejected.ToString(CultureInfo.InvariantCulture));
            EditorGUILayout.LabelField("SRP Proofs", _lastReport.srpBatcherProofCount.ToString(CultureInfo.InvariantCulture));
            for (int i = 0; i < _lastReport.violations.Count; i++)
                EditorGUILayout.LabelField(_lastReport.violations[i]);
            EditorGUILayout.EndScrollView();
        }

        public static FactoryReport Run(FactorySettings settings)
        {
            settings = (settings ?? FactorySettings.Default).Sanitize();
            Stopwatch stopwatch = Stopwatch.StartNew();
            FactoryReport report = new FactoryReport
            {
                generatedUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
                sourceDirectory = settings.SourceDirectory,
                metadataDirectory = settings.MetadataDirectory,
                collisionDirectory = settings.CollisionDirectory,
                materialDirectory = settings.MaterialDirectory,
                outputDirectory = settings.OutputDirectory,
                dryRun = settings.DryRun
            };

            try
            {
                if (!TryResolveMaterialSet(settings.MaterialDirectory, report, out MaterialSet materialSet))
                    return FinalizeReport(report, stopwatch);

                EnsureAssetFolder(settings.OutputDirectory);
                DiscoverSourceGroups(settings.SourceDirectory, settings.MaxGroupsPerRun, report);
                report.sourceGroups = s_groups.Count;

                for (int i = 0; i < s_groups.Count; i++)
                {
                    PrefabMetric metric = BuildPrefab(s_groups[i], materialSet, settings, report);
                    report.prefabs.Add(metric);
                    if (string.Equals(metric.status, "PASS", StringComparison.Ordinal))
                        report.prefabsAssembled++;
                    else
                        report.prefabsFailed++;
                }

                RunStaticAudit(report);
                return FinalizeReport(report, stopwatch);
            }
            finally
            {
                s_groups.Clear();
                s_assetPaths.Clear();
                s_violations.Clear();
                ClearScratch();
            }
        }

        private FactorySettings BuildSettings(bool dryRunOverride)
        {
            return new FactorySettings
            {
                SourceDirectory = sourceDirectory,
                MetadataDirectory = metadataDirectory,
                CollisionDirectory = collisionDirectory,
                MaterialDirectory = materialDirectory,
                OutputDirectory = outputDirectory,
                DryRun = dryRunOverride,
                MaxGroupsPerRun = maxGroupsPerRun
            }.Sanitize();
        }

        private static PrefabMetric BuildPrefab(SourceGroup group, MaterialSet materials, FactorySettings settings, FactoryReport report)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            string prefabName = "PFB_" + SanitizeAssetName(group.Name);
            PrefabMetric metric = new PrefabMetric
            {
                prefabName = prefabName,
                sourcePath = group.SourcePath,
                outputPath = settings.OutputDirectory + "/" + prefabName + ".prefab",
                collisionPath = ResolveCollisionPath(settings.CollisionDirectory, group.Name)
            };

            GameObject root = null;
            try
            {
                root = new GameObject(prefabName);
                root.layer = HectonLayerMasks.BaseModule;
                root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                root.transform.localScale = Vector3.one;

                LogisticsMetadataFile metadata = LoadMetadata(settings.MetadataDirectory, group.Name, metric, report);
                LogisticsNetworkNodeTypeID nodeType = ResolveNodeType(group.Name, metadata.nodeTypeID);
                LogisticsNetworkTypeID networkType = ResolveNetworkType(group.Name, metadata.networkTypeID);
                metric.nodeTypeID = nodeType.ToString();
                metric.networkTypeID = networkType.ToString();
                metric.baseCapacity = SanitizePositiveFinite(metadata.baseCapacity, DefaultBaseCapacity);
                metric.baseResistance = SanitizeNonNegativeFinite(metadata.baseResistance, DefaultBaseResistance);
                metric.maxPressureKPa = SanitizePositiveFinite(metadata.maxPressureKPa, DefaultMaxPressureKPa);

                Renderer visualRenderer = CreateVisualChild(root.transform, group, materials, nodeType, metric, report);
                Renderer[] stateRenderers = BuildStateRendererRefs(root.transform, nodeType);
                NetworkPortDescriptor[] ports = BuildPorts(metadata, nodeType, metric);

                NetworkNodeData nodeData = root.AddComponent<NetworkNodeData>();
                nodeData.ConfigureEditorBake(
                    networkType,
                    nodeType,
                    metric.baseCapacity,
                    metric.baseResistance,
                    metric.maxPressureKPa,
                    (byte)math.clamp(metadata.priority, 0, 255),
                    (byte)math.clamp(metadata.flags, 0, 255),
                    HashString(prefabName),
                    ports,
                    stateRenderers);

                if (nodeType == LogisticsNetworkNodeTypeID.Valve)
                    BakeValveMetadata(root, visualRenderer != null ? visualRenderer.transform : root.transform, metadata, stateRenderers, metric, report, nodeData);

                AttachCollisionProxy(root, group, metric, report);
                ValidatePrefab(root, nodeType, metric, report);

                if (string.IsNullOrEmpty(metric.failure) && !settings.DryRun)
                {
                    bool success;
                    PrefabUtility.SaveAsPrefabAsset(root, metric.outputPath, out success);
                    if (!success)
                        metric.failure = "PrefabUtility.SaveAsPrefabAsset returned false.";
                }

                DeleteFailedPrefabIfPresent(metric, settings);
                metric.prefabHash = HashString(prefabName + "|" + metric.nodeTypeID + "|" + metric.portCount.ToString(CultureInfo.InvariantCulture));
                metric.status = string.IsNullOrEmpty(metric.failure) ? "PASS" : "FAIL";
                return metric;
            }
            catch (Exception exception)
            {
                metric.failure = exception.GetType().Name + ": " + exception.Message;
                metric.status = "FAIL";
                report.violations.Add(prefabName + ": " + metric.failure);
                DeleteFailedPrefabIfPresent(metric, settings);
                return metric;
            }
            finally
            {
                metric.editorMicroseconds = ElapsedMicroseconds(stopwatch);
                if (root != null)
                    Object.DestroyImmediate(root);
            }
        }

        private static Renderer CreateVisualChild(
            Transform root,
            SourceGroup group,
            MaterialSet materials,
            LogisticsNetworkNodeTypeID nodeType,
            PrefabMetric metric,
            FactoryReport report)
        {
            if (group.PrefabSource != null)
            {
                GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(group.PrefabSource);
                if (instance == null)
                    instance = Object.Instantiate(group.PrefabSource);
                instance.name = "VIS_" + SanitizeAssetName(group.Name);
                instance.transform.SetParent(root, false);
                instance.layer = root.gameObject.layer;
                instance.GetComponentsInChildren(true, s_rendererScratch);
                for (int i = 0; i < s_rendererScratch.Count; i++)
                {
                    Renderer renderer = s_rendererScratch[i];
                    renderer.gameObject.layer = root.gameObject.layer;
                    ApplySharedMaterials(renderer, materials, nodeType, metric, report);
                    renderer.shadowCastingMode = ShadowCastingMode.On;
                    renderer.receiveShadows = true;
                }

                Renderer first = s_rendererScratch.Count > 0 ? s_rendererScratch[0] : null;
                metric.rendererCount += s_rendererScratch.Count;
                s_rendererScratch.Clear();
                return first;
            }

            if (group.Mesh == null)
            {
                metric.failure = "No mesh or prefab source loaded.";
                return null;
            }

            GameObject visual = new GameObject("VIS_" + SanitizeAssetName(group.Name));
            visual.layer = root.gameObject.layer;
            visual.transform.SetParent(root, false);
            MeshFilter filter = visual.AddComponent<MeshFilter>();
            filter.sharedMesh = group.Mesh;
            MeshRenderer meshRenderer = visual.AddComponent<MeshRenderer>();
            meshRenderer.shadowCastingMode = ShadowCastingMode.On;
            meshRenderer.receiveShadows = true;
            ApplySharedMaterials(meshRenderer, materials, nodeType, metric, report);
            metric.rendererCount++;
            return meshRenderer;
        }

        private static void DeleteFailedPrefabIfPresent(PrefabMetric metric, FactorySettings settings)
        {
            if (metric == null || settings == null || settings.DryRun || string.IsNullOrEmpty(metric.failure) || string.IsNullOrEmpty(metric.outputPath))
                return;

            GameObject existingPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(metric.outputPath);
            if (existingPrefab == null)
                return;

            AssetDatabase.DeleteAsset(metric.outputPath);
        }

        private static Renderer[] BuildStateRendererRefs(Transform root, LogisticsNetworkNodeTypeID nodeType)
        {
            if (root == null || !RequiresStatusEmission(nodeType))
                return Array.Empty<Renderer>();

            root.GetComponentsInChildren(true, s_rendererScratch);
            int statusRendererCount = 0;
            for (int i = 0; i < s_rendererScratch.Count; i++)
            {
                Renderer candidate = s_rendererScratch[i];
                if (!RendererHasEmissionStrength(candidate))
                    continue;

                s_rendererScratch[statusRendererCount] = candidate;
                statusRendererCount++;
            }

            if (statusRendererCount == 0)
            {
                s_rendererScratch.Clear();
                return Array.Empty<Renderer>();
            }

            Renderer[] renderers = new Renderer[statusRendererCount]; // COLD ALLOC: Renderer[count] - editor-only status renderer reference payload - owner: LogisticsPrefabFactory
            for (int i = 0; i < statusRendererCount; i++)
                renderers[i] = s_rendererScratch[i];
            s_rendererScratch.Clear();
            return renderers;
        }

        private static bool RendererHasEmissionStrength(Renderer renderer)
        {
            if (renderer == null)
                return false;

            bool hasEmissionStrength = false;
            renderer.GetSharedMaterials(s_materialScratch);
            for (int i = 0; i < s_materialScratch.Count; i++)
            {
                Material material = s_materialScratch[i];
                if (material != null && material.HasProperty(PumpRelayEmissionProperty))
                {
                    hasEmissionStrength = true;
                    break;
                }
            }

            s_materialScratch.Clear();
            return hasEmissionStrength;
        }

        private static void BakeValveMetadata(
            GameObject root,
            Transform visualRoot,
            LogisticsMetadataFile metadata,
            Renderer[] stateRenderers,
            PrefabMetric metric,
            FactoryReport report,
            NetworkNodeData nodeData)
        {
            ValveHandleJson[] source = metadata.valveHandles;
            int handleCount = source != null && source.Length > 0 ? source.Length : 1;
            ValveHandleDescriptor[] handles = new ValveHandleDescriptor[handleCount];

            for (int i = 0; i < handleCount; i++)
            {
                ValveHandleJson json = source != null && i < source.Length ? source[i] : null;
                Vector3 pivot = json != null ? json.localPosition : Vector3.zero;
                Vector3 axis = NormalizeAxis(json != null ? json.localAxis : Vector3.forward);
                float minAngle = json != null && math.isfinite(json.minAngleDegrees) ? json.minAngleDegrees : 0f;
                float maxAngle = json != null && math.isfinite(json.maxAngleDegrees) ? json.maxAngleDegrees : 90f;
                if (maxAngle < minAngle + 1f)
                    maxAngle = minAngle + 90f;

                Transform wheelVisual = visualRoot;
                if (json != null && !string.IsNullOrEmpty(json.wheelVisualName))
                {
                    Transform resolved = FindChildByName(root.transform, json.wheelVisualName);
                    if (resolved != null)
                        wheelVisual = resolved;
                }

                GameObject handleObject = new GameObject(string.IsNullOrEmpty(json != null ? json.ikHandleName : null) ? "IK_Handle" : json.ikHandleName);
                handleObject.layer = root.layer;
                handleObject.transform.SetParent(root.transform, false);
                handleObject.transform.localPosition = pivot;
                handleObject.transform.localRotation = BuildAxisRotation(axis);
                handleObject.transform.localScale = Vector3.one;

                handles[i] = new ValveHandleDescriptor
                {
                    IKHandle = handleObject.transform,
                    WheelVisual = wheelVisual,
                    LocalPivot = pivot,
                    LocalAxis = axis,
                    MinAngleDegrees = minAngle,
                    MaxAngleDegrees = maxAngle,
                    HandleHash = HashString(root.name + "_handle_" + i.ToString(CultureInfo.InvariantCulture))
                };
            }

            ValveMetadata valveMetadata = root.AddComponent<ValveMetadata>();
            valveMetadata.ConfigureEditorBake(handles);
            metric.valveHandleCount = handleCount;
            report.valveMetadataValidations++;

            VRValveWheelHandle valveWheel = root.AddComponent<VRValveWheelHandle>();
            ConfigureValveWheelHandle(valveWheel, handles[0]);

            FluidValveRuntime runtime = root.AddComponent<FluidValveRuntime>();
            runtime.ConfigureEditorBake(
                nodeData,
                valveMetadata,
                valveWheel,
                stateRenderers,
                metadata.initialVisualLoad01,
                metadata.baseEmissionStrength,
                metadata.maxEmissionStrength);

            AttachValveInteractionContract(handles[0], valveWheel, runtime, valveMetadata, metric, report);
        }

        private static void ConfigureValveWheelHandle(VRValveWheelHandle valveWheel, ValveHandleDescriptor descriptor)
        {
            valveWheel.ConfigureEditorBake(
                descriptor.WheelVisual,
                descriptor.IKHandle,
                descriptor.LocalAxis,
                math.max(1f, descriptor.MaxAngleDegrees - descriptor.MinAngleDegrees),
                0f);
        }

        private static void AttachValveInteractionContract(
            ValveHandleDescriptor descriptor,
            VRValveWheelHandle valveWheel,
            FluidValveRuntime runtime,
            ValveMetadata valveMetadata,
            PrefabMetric metric,
            FactoryReport report)
        {
            Transform handle = descriptor.IKHandle;
            if (handle == null)
            {
                metric.failure = "Valve IK handle missing for interaction contract.";
                return;
            }

            int interactableLayer = HectonLayerMasks.Interactable;
            if (!string.Equals(LayerMask.LayerToName(interactableLayer), InteractableLayerName, StringComparison.Ordinal))
            {
                metric.failure = "HectonLayerMasks.Interactable does not match TagManager Interactable layer.";
                report.violations.Add("FATAL: HectonLayerMasks.Interactable does not resolve to layer " + InteractableLayerName);
                return;
            }

            GameObject handleObject = handle.gameObject;
            handleObject.layer = interactableLayer;
            SphereCollider activationCollider = handleObject.GetComponent<SphereCollider>();
            if (activationCollider == null)
                activationCollider = handleObject.AddComponent<SphereCollider>();

            activationCollider.center = Vector3.zero;
            activationCollider.radius = DefaultValveInteractionRadiusMeters;
            activationCollider.isTrigger = false;

            ValveWheelInteractable interactable = handleObject.GetComponent<ValveWheelInteractable>();
            if (interactable == null)
                interactable = handleObject.AddComponent<ValveWheelInteractable>();

            interactable.ConfigureEditorBake(
                valveWheel,
                runtime,
                valveMetadata,
                activationCollider,
                handle);
        }

        private static void AttachCollisionProxy(GameObject root, SourceGroup group, PrefabMetric metric, FactoryReport report)
        {
            GameObject proxySource = !string.IsNullOrEmpty(metric.collisionPath)
                ? AssetDatabase.LoadAssetAtPath<GameObject>(metric.collisionPath)
                : null;

            GameObject proxyRoot;
            if (proxySource != null)
            {
                proxyRoot = (GameObject)PrefabUtility.InstantiatePrefab(proxySource);
                if (proxyRoot == null)
                    proxyRoot = Object.Instantiate(proxySource);
                proxyRoot.name = "COL_" + SanitizeAssetName(group.Name);
                proxyRoot.transform.SetParent(root.transform, false);
            }
            else
            {
                proxyRoot = new GameObject("COL_" + SanitizeAssetName(group.Name) + "_BoundsProxy");
                proxyRoot.transform.SetParent(root.transform, false);
                BoxCollider collider = proxyRoot.AddComponent<BoxCollider>();
                Bounds bounds = ResolveMeshBounds(root, group);
                collider.center = bounds.center;
                collider.size = new Vector3(
                    math.max(0.05f, bounds.size.x),
                    math.max(0.05f, bounds.size.y),
                    math.max(0.05f, bounds.size.z));
                report.violations.Add(root.name + ": no explicit COL_ proxy found; generated primitive bounds proxy.");
            }

            proxyRoot.layer = root.layer;
            proxyRoot.GetComponentsInChildren(true, s_colliderScratch);
            for (int i = 0; i < s_colliderScratch.Count; i++)
            {
                Collider collider = s_colliderScratch[i];
                collider.gameObject.layer = root.layer;
                if (collider is MeshCollider)
                {
                    report.looseMeshCollidersFound++;
                    metric.failure = "MeshCollider present in collision proxy; final prefab gate rejects it.";
                }
            }

            metric.colliderCount += s_colliderScratch.Count;
            report.primitiveColliderCount += CountPrimitiveColliders(s_colliderScratch);
            s_colliderScratch.Clear();
        }

        private static void ValidatePrefab(GameObject root, LogisticsNetworkNodeTypeID nodeType, PrefabMetric metric, FactoryReport report)
        {
            root.GetComponentsInChildren(true, s_meshColliderScratch);
            if (s_meshColliderScratch.Count > 0)
            {
                report.meshCollidersRejected += s_meshColliderScratch.Count;
                metric.failure = "Final prefab contains MeshCollider count=" + s_meshColliderScratch.Count.ToString(CultureInfo.InvariantCulture);
            }
            s_meshColliderScratch.Clear();

            root.GetComponentsInChildren(true, s_particleScratch);
            if (s_particleScratch.Count > 0)
                metric.failure = "Final prefab contains ParticleSystem; logistics prefabs must serialize static data only.";
            s_particleScratch.Clear();

            root.GetComponentsInChildren(true, s_nodeScratch);
            if (s_nodeScratch.Count != 1)
            {
                metric.failure = "Expected exactly one NetworkNodeData on root hierarchy; found " + s_nodeScratch.Count.ToString(CultureInfo.InvariantCulture);
            }
            else
            {
                NetworkNodeData nodeData = s_nodeScratch[0];
                if (nodeData.BaseCapacity <= 0f || nodeData.PortCount <= 0 || !nodeData.TryBuildNodeBakeDTO(out NetworkNodeBakeDTO nodeDto))
                    metric.failure = "NetworkNodeData capacity/ports invalid.";
                if (!NetworkNodeData.ValidateUnmanagedLayout(out int nodeDtoBytes, out int portDtoBytes, out int fluidPipeDtoBytes, out int fluidRegistrationDtoBytes, out int powerNodeDtoBytes))
                    metric.failure = "NetworkNodeData unmanaged layout invalid: node=" +
                                     nodeDtoBytes.ToString(CultureInfo.InvariantCulture) +
                                     " port=" + portDtoBytes.ToString(CultureInfo.InvariantCulture) +
                                     " fluid=" + fluidPipeDtoBytes.ToString(CultureInfo.InvariantCulture) +
                                     " fluidRegistration=" + fluidRegistrationDtoBytes.ToString(CultureInfo.InvariantCulture) +
                                     " power=" + powerNodeDtoBytes.ToString(CultureInfo.InvariantCulture);
                if (nodeData.NetworkTypeID == LogisticsNetworkTypeID.FluidPressure ||
                    nodeData.NetworkTypeID == LogisticsNetworkTypeID.OxygenPressure)
                {
                    if (!nodeData.TryBuildFluidPipeBakeDTO(out _))
                        metric.failure = "NetworkNodeData fluid pipe bake DTO invalid.";
                }
                else if (nodeData.NetworkTypeID == LogisticsNetworkTypeID.PowerDc)
                {
                    if (!nodeData.TryBuildPowerNodeDTO(out _))
                        metric.failure = "NetworkNodeData power node bake DTO invalid.";
                }
                else
                {
                    metric.failure = "NetworkNodeData network type has no current runtime graph projection.";
                }
                report.nodeMetadataValidations++;
            }
            s_nodeScratch.Clear();

            if (nodeType == LogisticsNetworkNodeTypeID.Valve)
            {
                root.GetComponentsInChildren(true, s_valveScratch);
                if (s_valveScratch.Count != 1 || !s_valveScratch[0].ValidateHandlesForBake())
                    metric.failure = "ValveMetadata handle serialization invalid.";
                if (!ValveMetadata.ValidateUnmanagedLayout(out int valveHandleBytes))
                    metric.failure = "ValveMetadata unmanaged layout invalid: handle=" + valveHandleBytes.ToString(CultureInfo.InvariantCulture);
                if (!FluidValveRuntime.ValidateUnmanagedLayout(out int valveVisualBytes))
                    metric.failure = "FluidValveRuntime visual-state layout invalid: visual=" + valveVisualBytes.ToString(CultureInfo.InvariantCulture);
                s_valveScratch.Clear();

                root.GetComponentsInChildren(true, s_valveInteractableScratch);
                if (s_valveInteractableScratch.Count != 1 || !s_valveInteractableScratch[0].ValidateEditorBindingForBake())
                    metric.failure = "ValveWheelInteractable interaction collider binding invalid.";
                s_valveInteractableScratch.Clear();
            }

            root.GetComponentsInChildren(true, s_rendererScratch);
            for (int i = 0; i < s_rendererScratch.Count; i++)
                ValidateRendererMaterials(s_rendererScratch[i], nodeType, metric, report);
            s_rendererScratch.Clear();

            if (RequiresStatusEmission(nodeType) &&
                metric.emissionStrengthProofCount <= 0)
            {
                metric.failure = nodeType.ToString() + " status material lacks " + PumpRelayEmissionProperty + ".";
            }
        }

        private static void ValidateRendererMaterials(Renderer renderer, LogisticsNetworkNodeTypeID nodeType, PrefabMetric metric, FactoryReport report)
        {
            if (renderer == null)
                return;

            renderer.GetSharedMaterials(s_materialScratch);
            for (int i = 0; i < s_materialScratch.Count; i++)
            {
                Material material = s_materialScratch[i];
                if (material == null)
                {
                    metric.failure = "Renderer has null shared material.";
                    continue;
                }

                if (HasSrpBatcherProof(material))
                {
                    metric.srpBatcherProofCount++;
                    report.srpBatcherProofCount++;
                }
                else
                {
                    metric.failure = "Material lacks SRP batcher CBUFFER proof: " + material.name;
                }

                if (RequiresStatusEmission(nodeType) &&
                    material.HasProperty(PumpRelayEmissionProperty))
                {
                    metric.emissionStrengthProofCount++;
                    report.emissionStrengthProofCount++;
                }
            }

            s_materialScratch.Clear();
        }

        private static void ApplySharedMaterials(Renderer renderer, MaterialSet materials, LogisticsNetworkNodeTypeID nodeType, PrefabMetric metric, FactoryReport report)
        {
            if (renderer == null)
                return;

            Material primary = ResolveRequiredMaterial(materials, nodeType, out string requiredToken);
            if (primary == null)
            {
                metric.failure = "Missing required shared material: " + requiredToken;
                return;
            }

            renderer.GetSharedMaterials(s_materialScratch);
            int slotCount = s_materialScratch.Count;
            if (slotCount <= 1)
            {
                renderer.sharedMaterial = primary;
                s_materialScratch.Clear();
                return;
            }

            Material[] slots = new Material[slotCount]; // COLD ALLOC: Material[slotCount] - editor-only shared material slot normalization - owner: LogisticsPrefabFactory
            for (int i = 0; i < slotCount; i++)
                slots[i] = primary;

            renderer.sharedMaterials = slots;
            s_materialScratch.Clear();
        }

        private static Material ResolveRequiredMaterial(MaterialSet materials, LogisticsNetworkNodeTypeID nodeType, out string requiredToken)
        {
            if (nodeType == LogisticsNetworkNodeTypeID.Pipe || nodeType == LogisticsNetworkNodeTypeID.Junction)
            {
                requiredToken = RustedMetalToken;
                return materials.MetalRusted;
            }

            requiredToken = EquipmentAtlasToken;
            return materials.EquipmentAtlas;
        }

        private static bool RequiresStatusEmission(LogisticsNetworkNodeTypeID nodeType)
        {
            return nodeType == LogisticsNetworkNodeTypeID.Pump ||
                   nodeType == LogisticsNetworkNodeTypeID.Relay ||
                   nodeType == LogisticsNetworkNodeTypeID.Valve;
        }

        private static NetworkPortDescriptor[] BuildPorts(LogisticsMetadataFile metadata, LogisticsNetworkNodeTypeID nodeType, PrefabMetric metric)
        {
            LogisticsPortJson[] jsonPorts = metadata.ports;
            int count = jsonPorts != null && jsonPorts.Length > 0 ? jsonPorts.Length : ResolveDefaultPortCount(nodeType);
            NetworkPortDescriptor[] ports = new NetworkPortDescriptor[count];

            for (int i = 0; i < count; i++)
            {
                LogisticsPortJson json = jsonPorts != null && i < jsonPorts.Length ? jsonPorts[i] : null;
                ports[i] = new NetworkPortDescriptor
                {
                    PortID = json != null && json.portID >= 0 ? json.portID : i,
                    PortTypeID = ResolvePortType(nodeType, i, json != null ? json.portTypeID : null),
                    LocalPosition = json != null ? json.localPosition : DefaultPortPosition(nodeType, i, count),
                    LocalDirection = NormalizeAxis(json != null ? json.localDirection : DefaultPortDirection(nodeType, i, count)),
                    CapacityScale = json != null ? SanitizePositiveFinite(json.capacityScale, 1f) : 1f
                };
            }

            metric.portCount = ports.Length;
            return ports;
        }

        private static LogisticsMetadataFile LoadMetadata(string metadataDirectory, string name, PrefabMetric metric, FactoryReport report)
        {
            LogisticsMetadataFile metadata = new LogisticsMetadataFile();
            string path = ResolveMetadataPath(metadataDirectory, name);
            metric.metadataPath = path;
            if (string.IsNullOrEmpty(path))
            {
                report.violations.Add(name + ": no Wave 2 metadata JSON found; using deterministic default ports.");
                return metadata;
            }

            try
            {
                string fullPath = AssetPathToFullPath(path);
                string json = File.ReadAllText(fullPath);
                LogisticsMetadataFile parsed = JsonUtility.FromJson<LogisticsMetadataFile>(json);
                if (parsed != null)
                    metadata = parsed;
            }
            catch (Exception exception)
            {
                metric.failure = "Metadata parse failed: " + exception.Message;
            }

            return metadata;
        }

        private static void DiscoverSourceGroups(string directory, int maxGroups, FactoryReport report)
        {
            s_groups.Clear();
            s_assetPaths.Clear();
            if (!AssetDatabase.IsValidFolder(directory))
            {
                report.violations.Add("Source directory missing: " + directory);
                return;
            }

            string[] searchFolders = { directory };
            string[] meshGuids = AssetDatabase.FindAssets("t:Mesh", searchFolders);
            for (int i = 0; i < meshGuids.Length && s_groups.Count < maxGroups; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(meshGuids[i]);
                if (ShouldSkipSource(path))
                    continue;

                Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if (mesh == null)
                    continue;

                string name = NormalizeSourceName(Path.GetFileNameWithoutExtension(path));
                if (FindGroupIndex(name) >= 0)
                    continue;

                s_groups.Add(new SourceGroup { Name = name, SourcePath = path, Mesh = mesh });
            }

            string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", searchFolders);
            for (int i = 0; i < prefabGuids.Length && s_groups.Count < maxGroups; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(prefabGuids[i]);
                if (ShouldSkipSource(path))
                    continue;

                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null)
                    continue;

                string name = NormalizeSourceName(Path.GetFileNameWithoutExtension(path));
                if (FindGroupIndex(name) >= 0)
                    continue;

                s_groups.Add(new SourceGroup { Name = name, SourcePath = path, PrefabSource = prefab });
            }
        }

        private static bool TryResolveMaterialSet(string materialDirectory, FactoryReport report, out MaterialSet materialSet)
        {
            materialSet = new MaterialSet();
            if (!AssetDatabase.IsValidFolder(materialDirectory))
            {
                report.violations.Add("Material directory missing: " + materialDirectory);
                return false;
            }

            string[] folders = { materialDirectory };
            string[] guids = AssetDatabase.FindAssets("t:Material", folders);
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null)
                    continue;

                string lower = material.name.ToLowerInvariant();
                if (materialSet.EquipmentAtlas == null && lower.Contains(EquipmentAtlasToken.ToLowerInvariant()))
                    materialSet.EquipmentAtlas = material;
                if (materialSet.MetalRusted == null && lower.Contains(RustedMetalToken.ToLowerInvariant()))
                    materialSet.MetalRusted = material;
            }

            if (materialSet.EquipmentAtlas == null)
                report.violations.Add("Missing shared material token: " + EquipmentAtlasToken);
            if (materialSet.MetalRusted == null)
                report.violations.Add("Missing shared material token: " + RustedMetalToken);

            return materialSet.EquipmentAtlas != null || materialSet.MetalRusted != null;
        }

        private static void RunStaticAudit(FactoryReport report)
        {
            string factoryPath = "Assets/_Project/Editor/Assembly/LogisticsPrefabFactory.cs";
            string nodePath = "Assets/_Project/Scripts/Construction/NetworkNodeData.cs";
            string valvePath = "Assets/_Project/Scripts/Construction/ValveMetadata.cs";
            string valveInteractablePath = "Assets/_Project/Scripts/Construction/ValveWheelInteractable.cs";
            string runtimePath = "Assets/_Project/Scripts/Construction/FluidValveRuntime.cs";
            string factory = ReadAssetText(factoryPath);
            string node = ReadAssetText(nodePath);
            string valve = ReadAssetText(valvePath);
            string valveInteractable = ReadAssetText(valveInteractablePath);
            string runtime = ReadAssetText(runtimePath);

            RequireSourceContains(factory, "PrefabUtility.SaveAsPrefabAsset", factoryPath, report);
            RequireSourceContains(factory, "AssetDatabase.DeleteAsset", factoryPath, report);
            RequireSourceContains(factory, "NetworkNodeData", factoryPath, report);
            RequireSourceContains(factory, "ValveMetadata", factoryPath, report);
            RequireSourceContains(factory, "ValveWheelInteractable", factoryPath, report);
            RequireSourceContains(node, "NetworkPortDescriptor[]", nodePath, report);
            RequireSourceContains(node, "FluidPipeNodeBakeDTO", nodePath, report);
            RequireSourceContains(node, "FluidPipeRegistrationDTO", nodePath, report);
            RequireSourceContains(node, "TryRegisterFluidPipeNode", nodePath, report);
            RequireSourceContains(node, "TryBuildPowerNodeDTO", nodePath, report);
            RequireSourceContains(node, "PowerNodeDTO", nodePath, report);
            RequireSourceContains(valve, "ValveHandleDescriptor[]", valvePath, report);
            RequireSourceContains(valveInteractable, "IPhysicalPanelButtonReceiver", valveInteractablePath, report);
            RequireSourceContains(valveInteractable, "PhysicalHandReceiverRegistry.TryRegister", valveInteractablePath, report);
            RequireSourceContains(valveInteractable, "InteractableRegistry.RegisterTree", valveInteractablePath, report);
            RequireSourceContains(runtime, "ValveVisualStateDTO", runtimePath, report);
            RequireSourceContains(runtime, "SyncVisualLoadFromValveWheel", runtimePath, report);
            RequireSourceContains(runtime, "GlobalQualityWeight", runtimePath, report);
            RequireSourceContains(ReadAssetText("Assets/_Project/Scripts/Interaction/VRValveWheelHandle.cs"), "grabPivot", "Assets/_Project/Scripts/Interaction/VRValveWheelHandle.cs", report);
            RequireSourceContains(node, "UnsafeUtility.SizeOf<NetworkNodeBakeDTO>", nodePath, report);
            RequireSourceContains(node, "UnsafeUtility.SizeOf<FluidPipeNodeBakeDTO>", nodePath, report);
            RequireSourceContains(node, "UnsafeUtility.SizeOf<FluidPipeRegistrationDTO>", nodePath, report);
            RequireSourceContains(node, "UnsafeUtility.SizeOf<PowerNodeDTO>", nodePath, report);
            RequireSourceContains(valve, "UnsafeUtility.SizeOf<ValveHandleKinematicDTO>", valvePath, report);
            RequireSourceContains(runtime, "UnsafeUtility.SizeOf<ValveVisualStateDTO>", runtimePath, report);
            ForbidSourceContains(runtime, "MaterialPropertyBlock", runtimePath, report);
            ForbidSourceContains(runtime, "GetComponentInChildren", runtimePath, report);
            ForbidSourceContains(runtime, "Update" + "()", runtimePath, report);
            ForbidSourceContains(runtime, "GlobalRegistry.Get", runtimePath, report);
            ForbidSourceContains(factory, ".material =", factoryPath, report);
        }

        private static FactoryReport FinalizeReport(FactoryReport report, Stopwatch stopwatch)
        {
            report.totalEditorMicroseconds = ElapsedMicroseconds(stopwatch);
            for (int i = 0; i < s_violations.Count; i++)
                report.violations.Add(s_violations[i]);

            if (!report.dryRun)
            {
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh(ImportAssetOptions.Default);
            }
            return report;
        }

        private static void EnsureAssetFolder(string assetPath)
        {
            string normalized = SanitizeAssetPath(assetPath, DefaultOutputDirectory);
            string[] parts = normalized.Split('/');
            if (parts.Length == 0 || parts[0] != "Assets")
                return;

            string current = "Assets";
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }

        private static string ResolveMetadataPath(string metadataDirectory, string name)
        {
            if (!AssetDatabase.IsValidFolder(metadataDirectory))
                return string.Empty;

            string exact = metadataDirectory + "/" + SanitizeAssetName(name) + ".json";
            if (File.Exists(AssetPathToFullPath(exact)))
                return exact;

            string[] guids = AssetDatabase.FindAssets(SanitizeAssetName(name), new[] { metadataDirectory });
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (path.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                    return path;
            }

            return string.Empty;
        }

        private static string ResolveCollisionPath(string collisionDirectory, string name)
        {
            if (!AssetDatabase.IsValidFolder(collisionDirectory))
                return string.Empty;

            string token = "COL_" + SanitizeAssetName(name);
            string[] guids = AssetDatabase.FindAssets(token + " t:Prefab", new[] { collisionDirectory });
            if (guids.Length > 0)
                return AssetDatabase.GUIDToAssetPath(guids[0]);

            return string.Empty;
        }

        private static Bounds ResolveMeshBounds(GameObject root, SourceGroup group)
        {
            if (group.Mesh != null)
                return group.Mesh.bounds;

            root.GetComponentsInChildren(true, s_rendererScratch);
            if (s_rendererScratch.Count == 0)
                return new Bounds(Vector3.zero, Vector3.one);

            Bounds bounds = s_rendererScratch[0].bounds;
            for (int i = 1; i < s_rendererScratch.Count; i++)
                bounds.Encapsulate(s_rendererScratch[i].bounds);
            s_rendererScratch.Clear();
            bounds.center = root.transform.InverseTransformPoint(bounds.center);
            return bounds;
        }

        private static bool HasSrpBatcherProof(Material material)
        {
            if (material == null || material.shader == null)
                return false;

            string shaderName = material.shader.name;
            if (!string.IsNullOrEmpty(shaderName) &&
                (shaderName.Contains("Universal Render Pipeline/Lit") || shaderName.Contains("HDRP/Lit")))
            {
                return true;
            }

            string path = AssetDatabase.GetAssetPath(material.shader);
            if (string.IsNullOrEmpty(path))
                return false;
            if (path.EndsWith(".shadergraph", StringComparison.OrdinalIgnoreCase))
                return true;

            string fullPath = AssetPathToFullPath(path);
            if (!File.Exists(fullPath))
                return false;

            string source = File.ReadAllText(fullPath);
            return source.Contains("CBUFFER_START(UnityPerMaterial)");
        }

        private static LogisticsNetworkNodeTypeID ResolveNodeType(string name, string metadataValue)
        {
            if (!string.IsNullOrEmpty(metadataValue) &&
                Enum.TryParse(metadataValue, true, out LogisticsNetworkNodeTypeID parsed))
            {
                return parsed;
            }

            string lower = name.ToLowerInvariant();
            if (lower.Contains("valve"))
                return LogisticsNetworkNodeTypeID.Valve;
            if (lower.Contains("pump"))
                return LogisticsNetworkNodeTypeID.Pump;
            if (lower.Contains("relay"))
                return LogisticsNetworkNodeTypeID.Relay;
            if (lower.Contains("junction") || lower.Contains("cross") || lower.Contains("_t_") || lower.Contains("-t-"))
                return LogisticsNetworkNodeTypeID.Junction;

            return LogisticsNetworkNodeTypeID.Pipe;
        }

        private static LogisticsNetworkTypeID ResolveNetworkType(string name, string metadataValue)
        {
            if (!string.IsNullOrEmpty(metadataValue) &&
                Enum.TryParse(metadataValue, true, out LogisticsNetworkTypeID parsed))
            {
                return parsed;
            }

            string lower = name.ToLowerInvariant();
            if (lower.Contains("oxygen") || lower.Contains("o2"))
                return LogisticsNetworkTypeID.OxygenPressure;
            if (lower.Contains("power") || lower.Contains("relay"))
                return LogisticsNetworkTypeID.PowerDc;
            if (lower.Contains("coolant") || lower.Contains("thermal"))
                return LogisticsNetworkTypeID.ThermalCoolant;
            if (lower.Contains("fuel"))
                return LogisticsNetworkTypeID.FuelLiquid;

            return LogisticsNetworkTypeID.FluidPressure;
        }

        private static LogisticsNetworkPortTypeID ResolvePortType(LogisticsNetworkNodeTypeID nodeType, int index, string metadataValue)
        {
            if (!string.IsNullOrEmpty(metadataValue) &&
                Enum.TryParse(metadataValue, true, out LogisticsNetworkPortTypeID parsed))
            {
                return parsed;
            }

            if (nodeType == LogisticsNetworkNodeTypeID.Pump)
                return index == 0 ? LogisticsNetworkPortTypeID.Inlet : LogisticsNetworkPortTypeID.Outlet;
            if (nodeType == LogisticsNetworkNodeTypeID.Relay)
                return index == 0 ? LogisticsNetworkPortTypeID.Power : LogisticsNetworkPortTypeID.Data;

            return LogisticsNetworkPortTypeID.Bidirectional;
        }

        private static int ResolveDefaultPortCount(LogisticsNetworkNodeTypeID nodeType)
        {
            switch (nodeType)
            {
                case LogisticsNetworkNodeTypeID.Junction:
                    return 4;
                case LogisticsNetworkNodeTypeID.Relay:
                    return 2;
                case LogisticsNetworkNodeTypeID.Pipe:
                case LogisticsNetworkNodeTypeID.Valve:
                case LogisticsNetworkNodeTypeID.Pump:
                    return 2;
                default:
                    return 1;
            }
        }

        private static Vector3 DefaultPortPosition(LogisticsNetworkNodeTypeID nodeType, int index, int count)
        {
            if (count <= 1)
                return Vector3.zero;
            if (nodeType == LogisticsNetworkNodeTypeID.Junction && count >= 4)
            {
                switch (index & 3)
                {
                    case 0:
                        return Vector3.left * 0.5f;
                    case 1:
                        return Vector3.right * 0.5f;
                    case 2:
                        return Vector3.forward * 0.5f;
                    default:
                        return Vector3.back * 0.5f;
                }
            }

            return index == 0 ? Vector3.left * 0.5f : Vector3.right * 0.5f;
        }

        private static Vector3 DefaultPortDirection(LogisticsNetworkNodeTypeID nodeType, int index, int count)
        {
            Vector3 position = DefaultPortPosition(nodeType, index, count);
            if (position.sqrMagnitude <= 0.000001f)
                return Vector3.forward;

            return position.normalized;
        }

        private static Transform FindChildByName(Transform root, string childName)
        {
            if (root == null || string.IsNullOrEmpty(childName))
                return null;
            if (string.Equals(root.name, childName, StringComparison.Ordinal))
                return root;

            for (int i = 0; i < root.childCount; i++)
            {
                Transform child = root.GetChild(i);
                Transform resolved = FindChildByName(child, childName);
                if (resolved != null)
                    return resolved;
            }

            return null;
        }

        private static int CountPrimitiveColliders(List<Collider> colliders)
        {
            int count = 0;
            for (int i = 0; i < colliders.Count; i++)
            {
                Collider collider = colliders[i];
                if (collider is BoxCollider || collider is CapsuleCollider || collider is SphereCollider)
                    count++;
            }

            return count;
        }

        private static int FindGroupIndex(string name)
        {
            for (int i = 0; i < s_groups.Count; i++)
            {
                if (string.Equals(s_groups[i].Name, name, StringComparison.Ordinal))
                    return i;
            }

            return -1;
        }

        private static bool ShouldSkipSource(string path)
        {
            string name = Path.GetFileNameWithoutExtension(path);
            return string.IsNullOrEmpty(name) ||
                   name.StartsWith("COL_", StringComparison.OrdinalIgnoreCase) ||
                   name.StartsWith("PFB_", StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizeSourceName(string value)
        {
            string name = SanitizeAssetName(value);
            if (name.StartsWith("VIS_", StringComparison.OrdinalIgnoreCase))
                name = name.Substring(4);
            if (name.StartsWith("MESH_", StringComparison.OrdinalIgnoreCase))
                name = name.Substring(5);
            return name;
        }

        private static string SanitizeAssetName(string value)
        {
            if (string.IsNullOrEmpty(value))
                return "Unnamed";

            char[] chars = value.ToCharArray();
            for (int i = 0; i < chars.Length; i++)
            {
                char c = chars[i];
                if (!char.IsLetterOrDigit(c) && c != '_' && c != '-')
                    chars[i] = '_';
            }

            string sanitized = new string(chars).Trim('_');
            return string.IsNullOrEmpty(sanitized) ? "Unnamed" : sanitized;
        }

        private static string SanitizeAssetPath(string value, string fallback)
        {
            if (string.IsNullOrWhiteSpace(value))
                return fallback;

            return value.Replace('\\', '/').TrimEnd('/');
        }

        private static Vector3 NormalizeAxis(Vector3 value)
        {
            float3 axis = new float3(value.x, value.y, value.z);
            if (!math.all(math.isfinite(axis)) || math.lengthsq(axis) <= 0.000001f)
                return Vector3.forward;

            return value.normalized;
        }

        private static Quaternion BuildAxisRotation(Vector3 axis)
        {
            Vector3 forward = NormalizeAxis(axis);
            Vector3 up = math.abs(Vector3.Dot(forward, Vector3.up)) > 0.95f ? Vector3.right : Vector3.up;
            return Quaternion.LookRotation(forward, up);
        }

        private static float SanitizePositiveFinite(float value, float fallback)
        {
            if (!math.isfinite(value) || value < fallback)
                return fallback;

            return value;
        }

        private static float SanitizeNonNegativeFinite(float value, float fallback)
        {
            if (!math.isfinite(value) || value < 0f)
                return fallback;

            return value;
        }

        private static uint HashString(string value)
        {
            uint hash = 2166136261u;
            if (string.IsNullOrEmpty(value))
                return hash;

            for (int i = 0; i < value.Length; i++)
                hash = (hash ^ value[i]) * 16777619u;

            return hash;
        }

        private static long ElapsedMicroseconds(Stopwatch stopwatch)
        {
            return (long)(stopwatch.ElapsedTicks * 1000000.0 / Stopwatch.Frequency);
        }

        private static string AssetPathToFullPath(string assetPath)
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            return Path.Combine(projectRoot, assetPath.Replace('/', Path.DirectorySeparatorChar));
        }

        private static string ReadAssetText(string assetPath)
        {
            string fullPath = AssetPathToFullPath(assetPath);
            return File.Exists(fullPath) ? File.ReadAllText(fullPath) : string.Empty;
        }

        private static void RequireSourceContains(string source, string token, string path, FactoryReport report)
        {
            if (string.IsNullOrEmpty(source) || !source.Contains(token))
                report.violations.Add(path + ": missing required token `" + token + "`.");
        }

        private static void ForbidSourceContains(string source, string token, string path, FactoryReport report)
        {
            if (!string.IsNullOrEmpty(source) && source.Contains(token))
                report.violations.Add(path + ": forbidden token `" + token + "`.");
        }

        private static void ClearScratch()
        {
            s_rendererScratch.Clear();
            s_colliderScratch.Clear();
            s_meshColliderScratch.Clear();
            s_particleScratch.Clear();
            s_nodeScratch.Clear();
            s_valveScratch.Clear();
            s_materialScratch.Clear();
        }
    }
}
#endif
