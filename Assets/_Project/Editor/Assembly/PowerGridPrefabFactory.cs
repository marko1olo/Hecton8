#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using Hecton8.Construction;
using Hecton8.Power;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Hecton8.Editor.Assembly
{
    public sealed class PowerGridPrefabFactory : EditorWindow
    {
        private const string OutputDirectory = "Assets/Prefabs/Construction/Power";
        private const string WorldStaticLayerName = "World_Static";
        private const string InteractableLayerName = "Interactable";
        private const string DefaultMaterialName = "MAT_Equipment_Atlas";
        private const string DefaultEmissionColorProperty = "_EmissionColor";
        private const string DefaultEmissionStrengthProperty = "_EmissionStrength";
        private const string DefaultGlobalQualityProperty = "_H8GlobalQualityWeight";
        private const string AnalyticFallbackSourcePath = "analytic://1740.power-grid";
        private const string BuiltInPrimitiveMeshGuid = "0000000000000000e000000000000000";
        private const int MaxShaderIncludeScanDepth = 4;

        private enum PowerNodeTypeID
        {
            Junction,
            Relay,
            Breaker,
            Battery,
            Generator,
            Rtg,
            Reactor
        }

        private static readonly string[] SourceDirectories =
        {
            "Assets/_Project/BakedGeometry/Power",
            "Assets/_Project/Generated/Power",
            "Assets/_Project/Art/Generated/Power",
            "Assets/_Project/Prefabs/Construction/Power/Sources"
        };

        private static readonly string[] MetadataDirectories =
        {
            "Assets/_Project/BakedGeometry/Power/Metadata",
            "Assets/_Project/Generated/Power/Metadata",
            "Assets/_Project/Art/Generated/Power/Metadata"
        };

        private static readonly string[] CollisionProxyDirectories =
        {
            "Assets/_Project/BakedGeometry/Power/Collision",
            "Assets/_Project/Generated/Power/Collision",
            "Assets/_Project/Art/Generated/Power/Collision"
        };

        private Vector2 _scroll;
        private FactoryReport _lastReport;

        [MenuItem("HECTON-8/Assembly/1740/Power Grid Prefab Factory")]
        public static void Open()
        {
            GetWindow<PowerGridPrefabFactory>("Power Grid Factory 1740");
        }

        [MenuItem("HECTON-8/Assembly/1740/Assemble Power Grid Prefabs")]
        public static void AssembleFromMenu()
        {
            FactoryReport report = RunFactory();
            UnityEngine.Debug.Log(
                "PowerGridPrefabFactory 1740 complete. assembled=" +
                report.assembledCount.ToString(CultureInfo.InvariantCulture) +
                " violations=" +
                report.fatalViolationCount.ToString(CultureInfo.InvariantCulture));
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Power Grid Prefab Factory 1740", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Input", ExistingDirectoriesCsv(SourceDirectories));
            EditorGUILayout.LabelField("Output", OutputDirectory);

            if (GUILayout.Button("Assemble Power Grid Prefabs"))
                _lastReport = RunFactory();

            if (_lastReport == null)
                return;

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            EditorGUILayout.LabelField("Groups", _lastReport.discoveredCount.ToString(CultureInfo.InvariantCulture));
            EditorGUILayout.LabelField("Assembled", _lastReport.assembledCount.ToString(CultureInfo.InvariantCulture));
            EditorGUILayout.LabelField("Fatal Violations", _lastReport.fatalViolationCount.ToString(CultureInfo.InvariantCulture));
            for (int i = 0; i < _lastReport.metrics.Count; i++)
            {
                PrefabMetric metric = _lastReport.metrics[i];
                EditorGUILayout.LabelField(metric.name, metric.failure.Length == 0 ? "OK" : metric.failure);
            }
            EditorGUILayout.EndScrollView();
        }

        public static FactoryReport RunFactory()
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            FactoryReport report = new FactoryReport();
            report.startedUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);
            report.outputDirectory = OutputDirectory;
            report.factoryVersion = "1740.power-grid-prefab-factory.v1";

            EnsureDirectory(OutputDirectory);

            int worldStaticLayer = ResolveLayer(WorldStaticLayerName, 0, report);
            int interactableLayer = ResolveLayer(InteractableLayerName, worldStaticLayer, report);

            List<PowerSourceGroup> groups = DiscoverSourceGroups(report);
            report.discoveredCount = groups.Count;

            for (int i = 0; i < groups.Count; i++)
                AssembleGroup(groups[i], worldStaticLayer, interactableLayer, report);

            stopwatch.Stop();
            report.elapsedMicroseconds = TicksToMicroseconds(stopwatch.ElapsedTicks);
            report.finishedUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);
            AssetDatabase.Refresh();
            return report;
        }

        private static List<PowerSourceGroup> DiscoverSourceGroups(FactoryReport report)
        {
            List<PowerSourceGroup> groups = new List<PowerSourceGroup>(32);
            for (int i = 0; i < SourceDirectories.Length; i++)
            {
                string directory = SourceDirectories[i];
                if (!AssetDatabase.IsValidFolder(directory))
                    continue;

                string[] meshGuids = AssetDatabase.FindAssets("t:Mesh", new[] { directory });
                for (int meshIndex = 0; meshIndex < meshGuids.Length; meshIndex++)
                {
                    string path = AssetDatabase.GUIDToAssetPath(meshGuids[meshIndex]);
                    if (!IsPowerVisualAsset(path))
                        continue;

                    Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                    if (mesh == null)
                        continue;

                    string baseName = NormalizeBaseName(Path.GetFileNameWithoutExtension(path));
                    PowerSourceGroup group = FindOrCreateGroup(groups, baseName);
                    if (group.visualMesh == null)
                    {
                        group.visualMesh = mesh;
                        group.visualPath = path;
                    }
                }

                string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { directory });
                for (int prefabIndex = 0; prefabIndex < prefabGuids.Length; prefabIndex++)
                {
                    string path = AssetDatabase.GUIDToAssetPath(prefabGuids[prefabIndex]);
                    if (!IsPowerVisualAsset(path))
                        continue;

                    GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (prefab == null)
                        continue;

                    string baseName = NormalizeBaseName(Path.GetFileNameWithoutExtension(path));
                    PowerSourceGroup group = FindOrCreateGroup(groups, baseName);
                    if (group.sourcePrefab == null)
                    {
                        group.sourcePrefab = prefab;
                        group.visualPath = path;
                    }
                }
            }

            for (int i = groups.Count - 1; i >= 0; i--)
            {
                PowerSourceGroup group = groups[i];
                if (group.metadata == null)
                    group.metadata = LoadMetadata(group.name, group.visualPath);
                group.nodeTypeId = group.useAnalyticFallback
                    ? group.nodeTypeId
                    : ResolveNodeType(group.name, group.metadata);
                if (group.visualMesh == null && group.sourcePrefab == null && !group.useAnalyticFallback)
                {
                    report.violations.Add("FATAL: " + group.name + " has no visual mesh or prefab source.");
                    groups.RemoveAt(i);
                }
            }

            int missingBaselineNodeTypes = CountMissingBaselineNodeTypes(groups);
            if (missingBaselineNodeTypes > 0)
                report.violations.Add("FATAL: missing " + missingBaselineNodeTypes.ToString(CultureInfo.InvariantCulture) + " power node source group(s). Analytic primitive fallback authoring is blocked for production power prefabs.");

            groups.Sort(CompareGroupsByName);
            return groups;
        }

        private static int CountMissingBaselineNodeTypes(List<PowerSourceGroup> groups)
        {
            int missingCount = 0;
            if (!HasGroupWithNodeType(groups, PowerNodeTypeID.Reactor))
                missingCount++;
            if (!HasGroupWithNodeType(groups, PowerNodeTypeID.Rtg))
                missingCount++;
            if (!HasGroupWithNodeType(groups, PowerNodeTypeID.Battery))
                missingCount++;
            if (!HasGroupWithNodeType(groups, PowerNodeTypeID.Relay))
                missingCount++;
            if (!HasGroupWithNodeType(groups, PowerNodeTypeID.Breaker))
                missingCount++;
            if (!HasGroupWithNodeType(groups, PowerNodeTypeID.Junction))
                missingCount++;

            return missingCount;
        }

        private static int AppendMissingAnalyticFallbackGroups(List<PowerSourceGroup> groups)
        {
            int before = groups.Count;
            AddAnalyticFallbackGroupIfMissing(groups, "Reactor_Core_Analytic", PowerNodeTypeID.Reactor);
            AddAnalyticFallbackGroupIfMissing(groups, "RTG_Cask_Analytic", PowerNodeTypeID.Rtg);
            AddAnalyticFallbackGroupIfMissing(groups, "Battery_Bank_Analytic", PowerNodeTypeID.Battery);
            AddAnalyticFallbackGroupIfMissing(groups, "Relay_Node_Analytic", PowerNodeTypeID.Relay);
            AddAnalyticFallbackGroupIfMissing(groups, "Breaker_Toggle_Analytic", PowerNodeTypeID.Breaker);
            AddAnalyticFallbackGroupIfMissing(groups, "Junction_SixPort_Analytic", PowerNodeTypeID.Junction);
            return groups.Count - before;
        }

        private static void AddAnalyticFallbackGroupIfMissing(List<PowerSourceGroup> groups, string name, PowerNodeTypeID typeId)
        {
            if (!HasGroupWithNodeType(groups, typeId))
                AddAnalyticFallbackGroup(groups, name, typeId);
        }

        private static bool HasGroupWithNodeType(List<PowerSourceGroup> groups, PowerNodeTypeID typeId)
        {
            for (int i = 0; i < groups.Count; i++)
            {
                if (groups[i].nodeTypeId == typeId)
                    return true;
            }

            return false;
        }

        private static void AddAnalyticFallbackGroup(List<PowerSourceGroup> groups, string name, PowerNodeTypeID typeId)
        {
            groups.Add(new PowerSourceGroup
            {
                name = name,
                visualPath = AnalyticFallbackSourcePath + "/" + name,
                metadata = FactoryMetadata.DefaultFor(typeId),
                nodeTypeId = typeId,
                useAnalyticFallback = true
            });
        }

        private static PowerSourceGroup FindOrCreateGroup(List<PowerSourceGroup> groups, string baseName)
        {
            for (int i = 0; i < groups.Count; i++)
            {
                if (string.Equals(groups[i].name, baseName, StringComparison.OrdinalIgnoreCase))
                    return groups[i];
            }

            PowerSourceGroup group = new PowerSourceGroup { name = baseName };
            groups.Add(group);
            return group;
        }

        private static void AssembleGroup(PowerSourceGroup group, int worldStaticLayer, int interactableLayer, FactoryReport report)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            PrefabMetric metric = new PrefabMetric();
            metric.name = group.name;
            metric.nodeType = group.nodeTypeId.ToString();
            metric.sourcePath = group.visualPath ?? string.Empty;
            metric.outputPath = OutputDirectory + "/PFB_" + SanitizeAssetName(group.name) + ".prefab";

            GameObject root = null;
            try
            {
                root = new GameObject("PFB_" + SanitizeAssetName(group.name));
                root.layer = worldStaticLayer;
                root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                root.transform.localScale = Vector3.one;

                PowerNode powerNode = root.AddComponent<PowerNode>();
                NetworkNodeData nodeData = root.AddComponent<NetworkNodeData>();

                FactoryMetadata metadata = group.metadata ?? FactoryMetadata.DefaultFor(group.nodeTypeId);
                float baseWattage = ResolveBaseWattage(group.nodeTypeId, metadata);
                float capacityWatts = ResolveCapacityWatts(group.nodeTypeId, baseWattage, metadata);
                float batteryCapacity = ResolveBatteryCapacity(group.nodeTypeId, metadata);
                float resistance = SanitizeResistance(metadata.baseResistance);
                int priority = math.clamp(metadata.defaultPriority <= 0 ? 50 : metadata.defaultPriority, 0, 100);
                int[] portIds = NormalizePorts(metadata.connectivityPorts, group.nodeTypeId);
                uint nodeHash = StableHash(group.name);
                string emissionColorProperty = ResolvePropertyName(metadata.emissionColorProperty, DefaultEmissionColorProperty);
                string emissionStrengthProperty = ResolvePropertyName(metadata.emissionStrengthProperty, DefaultEmissionStrengthProperty);
                string globalQualityProperty = ResolvePropertyName(metadata.globalQualityProperty, DefaultGlobalQualityProperty);

                Material sharedMaterial = ResolveSharedMaterial(metadata, report);
                Renderer[] renderers = AttachVisuals(root, group, sharedMaterial, metric);
                NetworkPortDescriptor[] portDescriptors = BuildPowerPorts(portIds, renderers);
                nodeData.ConfigureEditorBake(
                    LogisticsNetworkTypeID.PowerDc,
                    ResolveLogisticsNodeType(group.nodeTypeId),
                    math.max(0.001f, capacityWatts),
                    resistance,
                    0.1f,
                    (byte)priority,
                    0,
                    nodeHash,
                    portDescriptors,
                    renderers,
                    batteryCapacity,
                    ResolvePowerNodeFlags(group.nodeTypeId, batteryCapacity),
                    baseWattage);

                PowerStatusEmissiveBinding emissiveBinding = root.AddComponent<PowerStatusEmissiveBinding>();
                emissiveBinding.ConfigureEditorBake(
                    renderers,
                    emissionColorProperty,
                    emissionStrengthProperty,
                    globalQualityProperty,
                    ResolveColor(metadata.normalEmission, new Color(0.04f, 0.35f, 0.22f, 1f)),
                    ResolveColor(metadata.failureEmission, new Color(1f, 0.12f, 0.04f, 1f)),
                    SanitizeNonNegativeFinite(metadata.minEmissionStrength, 0.15f),
                    SanitizeNonNegativeFinite(metadata.maxEmissionStrength, 4f),
                    SanitizeNonNegativeFinite(metadata.pulseStrength, 0.65f));

                BreakerMetadata breakerMetadata = root.AddComponent<BreakerMetadata>();
                Transform ikHandle = CreateIkHandle(root, group, metadata, renderers, interactableLayer);
                Transform[] handleTransforms;
                BreakerHandleData[] handles = BuildBreakerHandles(root.transform, ikHandle, group, metadata, renderers, out handleTransforms);
                breakerMetadata.ConfigureEditorBake(ikHandle, handleTransforms, handles, metadata.defaultClosed);

                bool typedPowerOwnerAttached = AttachTypedRuntimeComponents(root, powerNode, breakerMetadata, emissiveBinding, group, metadata, baseWattage, batteryCapacity, resistance, priority, metric);
                ConfigurePowerNode(powerNode, ResolvePowerNodeFallbackWattage(group.nodeTypeId, baseWattage, typedPowerOwnerAttached), priority);
                AttachCollisionProxy(root, group, renderers, worldStaticLayer, metric);

                ValidateBeforeSave(root, powerNode, nodeData, breakerMetadata, emissiveBinding, emissionColorProperty, emissionStrengthProperty, group.nodeTypeId, baseWattage, metric);

                bool success;
                GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, metric.outputPath, out success);
                if (!success || saved == null)
                    throw new InvalidOperationException("PrefabUtility.SaveAsPrefabAsset failed for " + metric.outputPath);

                ValidateSavedPrefab(metric.outputPath, metric);

                metric.nodeHash = nodeHash;
                metric.baseWattage = baseWattage;
                metric.baseCapacityWatts = capacityWatts;
                metric.batteryCapacityWattSeconds = batteryCapacity;
                metric.portCount = portIds.Length;
                metric.handleCount = handles.Length;
                metric.rendererCount = renderers.Length;
                metric.status = "OK";
                report.assembledCount++;
            }
            catch (Exception exception)
            {
                metric.failure = exception.GetType().Name + ": " + exception.Message;
                metric.status = "FAILED";
                report.fatalViolationCount++;
                report.violations.Add("FATAL: " + metric.name + ": " + metric.failure);
                if (!string.IsNullOrEmpty(metric.outputPath) && AssetDatabase.LoadAssetAtPath<GameObject>(metric.outputPath) != null)
                    AssetDatabase.DeleteAsset(metric.outputPath);
            }
            finally
            {
                if (root != null)
                    Object.DestroyImmediate(root);

                stopwatch.Stop();
                metric.elapsedMicroseconds = TicksToMicroseconds(stopwatch.ElapsedTicks);
                report.metrics.Add(metric);
            }
        }

        private static Renderer[] AttachVisuals(GameObject root, PowerSourceGroup group, Material sharedMaterial, PrefabMetric metric)
        {
            if (sharedMaterial == null)
                throw new InvalidOperationException("Shared material " + DefaultMaterialName + " not found.");

            GameObject visualRoot;
            if (group.sourcePrefab != null)
            {
                Object instance = PrefabUtility.InstantiatePrefab(group.sourcePrefab);
                visualRoot = instance as GameObject;
                if (visualRoot == null)
                    visualRoot = Object.Instantiate(group.sourcePrefab);
                visualRoot.name = "VIS_" + SanitizeAssetName(group.name);
                visualRoot.transform.SetParent(root.transform, false);
                visualRoot.transform.localPosition = Vector3.zero;
                visualRoot.transform.localRotation = Quaternion.identity;
                visualRoot.transform.localScale = Vector3.one;
            }
            else
            {
                if (group.useAnalyticFallback)
                {
                    throw new InvalidOperationException("Analytic primitive fallback visual is blocked for production power prefab: " + group.name);
                }
                else
                {
                    visualRoot = new GameObject("VIS_" + SanitizeAssetName(group.name));
                    visualRoot.transform.SetParent(root.transform, false);
                    visualRoot.transform.localPosition = Vector3.zero;
                    visualRoot.transform.localRotation = Quaternion.identity;
                    visualRoot.transform.localScale = Vector3.one;
                    MeshFilter filter = visualRoot.AddComponent<MeshFilter>();
                    filter.sharedMesh = group.visualMesh;
                    visualRoot.AddComponent<MeshRenderer>();
                }
            }

            metric.strippedColliderCount += StripSourceColliders(visualRoot);
            metric.strippedParticleSystemCount += StripParticleSystems(visualRoot);

            Renderer[] renderers = visualRoot.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                MeshRenderer meshRenderer = renderer as MeshRenderer;
                if (meshRenderer == null)
                    continue;

                int materialSlots = math.max(1, meshRenderer.sharedMaterials != null ? meshRenderer.sharedMaterials.Length : 0);
                Material[] sharedMaterials = new Material[materialSlots];
                for (int materialIndex = 0; materialIndex < materialSlots; materialIndex++)
                    sharedMaterials[materialIndex] = sharedMaterial;
                meshRenderer.sharedMaterials = sharedMaterials;
                metric.materialSlotCount += materialSlots;
            }

            return renderers;
        }

        private static GameObject CreateAnalyticVisualRoot(Transform parent, PowerSourceGroup group)
        {
            GameObject visualRoot = new GameObject("VIS_" + SanitizeAssetName(group.name));
            visualRoot.transform.SetParent(parent, false);
            visualRoot.transform.localPosition = Vector3.zero;
            visualRoot.transform.localRotation = Quaternion.identity;
            visualRoot.transform.localScale = Vector3.one;

            switch (group.nodeTypeId)
            {
                case PowerNodeTypeID.Reactor:
                    AddAnalyticPrimitive(visualRoot.transform, "BODY_ReactorHull", PrimitiveType.Cube, new Vector3(0f, 0.55f, 0f), Quaternion.identity, new Vector3(1.8f, 1.1f, 1.35f));
                    AddAnalyticPrimitive(visualRoot.transform, "CORE_ReactorGlow", PrimitiveType.Cylinder, new Vector3(0f, 0.72f, 0f), Quaternion.identity, new Vector3(0.58f, 0.68f, 0.58f));
                    AddAnalyticPrimitive(visualRoot.transform, "SWITCH_ReactorBreaker", PrimitiveType.Cube, new Vector3(0f, 0.78f, 0.78f), Quaternion.Euler(-18f, 0f, 0f), new Vector3(0.16f, 0.48f, 0.08f));
                    break;
                case PowerNodeTypeID.Rtg:
                    AddAnalyticPrimitive(visualRoot.transform, "CASK_RTGBody", PrimitiveType.Cylinder, new Vector3(0f, 0.45f, 0f), Quaternion.Euler(0f, 0f, 90f), new Vector3(0.42f, 0.82f, 0.42f));
                    AddAnalyticPrimitive(visualRoot.transform, "FIN_RTGLeft", PrimitiveType.Cube, new Vector3(-0.34f, 0.45f, 0f), Quaternion.identity, new Vector3(0.08f, 0.72f, 0.84f));
                    AddAnalyticPrimitive(visualRoot.transform, "FIN_RTGRight", PrimitiveType.Cube, new Vector3(0.34f, 0.45f, 0f), Quaternion.identity, new Vector3(0.08f, 0.72f, 0.84f));
                    break;
                case PowerNodeTypeID.Battery:
                    AddAnalyticPrimitive(visualRoot.transform, "BANK_BatteryCradle", PrimitiveType.Cube, new Vector3(0f, 0.32f, 0f), Quaternion.identity, new Vector3(1.5f, 0.64f, 0.82f));
                    AddAnalyticPrimitive(visualRoot.transform, "CELL_BatteryA", PrimitiveType.Cube, new Vector3(-0.48f, 0.78f, 0f), Quaternion.identity, new Vector3(0.28f, 0.52f, 0.62f));
                    AddAnalyticPrimitive(visualRoot.transform, "CELL_BatteryB", PrimitiveType.Cube, new Vector3(0f, 0.78f, 0f), Quaternion.identity, new Vector3(0.28f, 0.52f, 0.62f));
                    AddAnalyticPrimitive(visualRoot.transform, "CELL_BatteryC", PrimitiveType.Cube, new Vector3(0.48f, 0.78f, 0f), Quaternion.identity, new Vector3(0.28f, 0.52f, 0.62f));
                    break;
                case PowerNodeTypeID.Relay:
                    AddAnalyticPrimitive(visualRoot.transform, "HUB_RelayBox", PrimitiveType.Cube, new Vector3(0f, 0.45f, 0f), Quaternion.identity, new Vector3(0.82f, 0.82f, 0.82f));
                    AddAnalyticPrimitive(visualRoot.transform, "PORT_RelayForward", PrimitiveType.Cube, new Vector3(0f, 0.45f, 0.62f), Quaternion.identity, new Vector3(0.18f, 0.22f, 0.42f));
                    AddAnalyticPrimitive(visualRoot.transform, "PORT_RelayRight", PrimitiveType.Cube, new Vector3(0.62f, 0.45f, 0f), Quaternion.identity, new Vector3(0.42f, 0.22f, 0.18f));
                    AddAnalyticPrimitive(visualRoot.transform, "PORT_RelayBack", PrimitiveType.Cube, new Vector3(0f, 0.45f, -0.62f), Quaternion.identity, new Vector3(0.18f, 0.22f, 0.42f));
                    AddAnalyticPrimitive(visualRoot.transform, "PORT_RelayLeft", PrimitiveType.Cube, new Vector3(-0.62f, 0.45f, 0f), Quaternion.identity, new Vector3(0.42f, 0.22f, 0.18f));
                    break;
                case PowerNodeTypeID.Breaker:
                    AddAnalyticPrimitive(visualRoot.transform, "PANEL_BreakerBackplate", PrimitiveType.Cube, new Vector3(0f, 0.5f, 0f), Quaternion.identity, new Vector3(0.78f, 1.0f, 0.28f));
                    AddAnalyticPrimitive(visualRoot.transform, "LEVER_BreakerHandle", PrimitiveType.Cube, new Vector3(0f, 0.72f, 0.2f), Quaternion.Euler(-24f, 0f, 0f), new Vector3(0.14f, 0.46f, 0.08f));
                    break;
                default:
                    AddAnalyticPrimitive(visualRoot.transform, "HUB_JunctionCore", PrimitiveType.Sphere, new Vector3(0f, 0.5f, 0f), Quaternion.identity, new Vector3(0.64f, 0.64f, 0.64f));
                    AddAnalyticPrimitive(visualRoot.transform, "PORT_JunctionForward", PrimitiveType.Cube, new Vector3(0f, 0.5f, 0.55f), Quaternion.identity, new Vector3(0.16f, 0.18f, 0.42f));
                    AddAnalyticPrimitive(visualRoot.transform, "PORT_JunctionRight", PrimitiveType.Cube, new Vector3(0.55f, 0.5f, 0f), Quaternion.identity, new Vector3(0.42f, 0.18f, 0.16f));
                    AddAnalyticPrimitive(visualRoot.transform, "PORT_JunctionBack", PrimitiveType.Cube, new Vector3(0f, 0.5f, -0.55f), Quaternion.identity, new Vector3(0.16f, 0.18f, 0.42f));
                    AddAnalyticPrimitive(visualRoot.transform, "PORT_JunctionLeft", PrimitiveType.Cube, new Vector3(-0.55f, 0.5f, 0f), Quaternion.identity, new Vector3(0.42f, 0.18f, 0.16f));
                    break;
            }

            return visualRoot;
        }

        private static void AddAnalyticPrimitive(
            Transform parent,
            string name,
            PrimitiveType primitiveType,
            Vector3 localPosition,
            Quaternion localRotation,
            Vector3 localScale)
        {
            GameObject primitive = GameObject.CreatePrimitive(primitiveType);
            primitive.name = name;
            primitive.transform.SetParent(parent, false);
            primitive.transform.localPosition = localPosition;
            primitive.transform.localRotation = localRotation;
            primitive.transform.localScale = localScale;
        }

        private static int StripSourceColliders(GameObject root)
        {
            Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
                Object.DestroyImmediate(colliders[i]);
            return colliders.Length;
        }

        private static int StripParticleSystems(GameObject root)
        {
            ParticleSystem[] particles = root.GetComponentsInChildren<ParticleSystem>(true);
            for (int i = 0; i < particles.Length; i++)
                Object.DestroyImmediate(particles[i]);
            return particles.Length;
        }

        private static void AttachCollisionProxy(GameObject root, PowerSourceGroup group, Renderer[] renderers, int worldStaticLayer, PrefabMetric metric)
        {
            GameObject proxy = InstantiateCollisionProxy(group);
            if (proxy == null)
                proxy = new GameObject("COL_" + SanitizeAssetName(group.name));

            proxy.name = "COL_" + SanitizeAssetName(group.name);
            proxy.transform.SetParent(root.transform, false);
            proxy.transform.localPosition = Vector3.zero;
            proxy.transform.localRotation = Quaternion.identity;
            proxy.transform.localScale = Vector3.one;
            SetLayerRecursively(proxy, worldStaticLayer);

            MeshCollider[] meshColliders = proxy.GetComponentsInChildren<MeshCollider>(true);
            metric.strippedMeshColliderCount += meshColliders.Length;
            for (int i = 0; i < meshColliders.Length; i++)
                Object.DestroyImmediate(meshColliders[i]);

            Collider[] colliders = proxy.GetComponentsInChildren<Collider>(true);
            int primitiveCount = 0;
            for (int i = 0; i < colliders.Length; i++)
            {
                Collider collider = colliders[i];
                if (collider is BoxCollider || collider is CapsuleCollider)
                {
                    primitiveCount++;
                    collider.gameObject.layer = worldStaticLayer;
                    collider.isTrigger = false;
                }
                else
                {
                    Object.DestroyImmediate(collider);
                    metric.strippedColliderCount++;
                }
            }

            if (primitiveCount == 0)
                primitiveCount = CreatePrimitiveCollider(proxy, root.transform, renderers);

            metric.primitiveColliderCount += primitiveCount;
        }

        private static GameObject InstantiateCollisionProxy(PowerSourceGroup group)
        {
            string proxyName = "COL_" + SanitizeAssetName(group.name);
            for (int i = 0; i < CollisionProxyDirectories.Length; i++)
            {
                string directory = CollisionProxyDirectories[i];
                if (!AssetDatabase.IsValidFolder(directory))
                    continue;

                string[] guids = AssetDatabase.FindAssets(proxyName + " t:Prefab", new[] { directory });
                for (int j = 0; j < guids.Length; j++)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guids[j]);
                    GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (prefab == null)
                        continue;

                    Object instance = PrefabUtility.InstantiatePrefab(prefab);
                    GameObject proxy = instance as GameObject;
                    return proxy != null ? proxy : Object.Instantiate(prefab);
                }
            }

            return null;
        }

        private static int CreatePrimitiveCollider(GameObject proxy, Transform root, Renderer[] renderers)
        {
            Bounds bounds = ResolveBounds(renderers);
            Vector3 localCenter = root.InverseTransformPoint(bounds.center);
            Vector3 size = bounds.size;
            if (size.x <= 0.0001f || size.y <= 0.0001f || size.z <= 0.0001f)
                size = Vector3.one;

            float horizontalMax = math.max(size.x, size.z);
            float horizontalMin = math.max(0.0001f, math.min(size.x, size.z));
            if (size.y > horizontalMax * 1.35f && horizontalMax / horizontalMin < 1.65f)
            {
                CapsuleCollider capsule = proxy.AddComponent<CapsuleCollider>();
                capsule.center = localCenter;
                capsule.direction = 1;
                capsule.radius = math.max(0.05f, horizontalMax * 0.5f);
                capsule.height = math.max(capsule.radius * 2f, size.y);
                return 1;
            }

            BoxCollider box = proxy.AddComponent<BoxCollider>();
            box.center = localCenter;
            box.size = new Vector3(math.max(0.05f, size.x), math.max(0.05f, size.y), math.max(0.05f, size.z));
            return 1;
        }

        private static Transform CreateIkHandle(Transform root, PowerSourceGroup group, FactoryMetadata metadata, Renderer[] renderers, int interactableLayer)
        {
            return root == null
                ? null
                : CreateIkHandle(root.gameObject, group, metadata, renderers, interactableLayer);
        }

        private static Transform CreateIkHandle(GameObject root, PowerSourceGroup group, FactoryMetadata metadata, Renderer[] renderers, int interactableLayer)
        {
            GameObject handle = new GameObject("IK_Handle");
            handle.layer = interactableLayer;
            handle.transform.SetParent(root.transform, false);
            Bounds bounds = ResolveBounds(renderers);
            Vector3 fallback = root.transform.InverseTransformPoint(bounds.center + new Vector3(0f, 0f, math.max(0.05f, bounds.extents.z)));
            Vector3 localPosition = metadata.breakerHandles != null && metadata.breakerHandles.Length > 0
                ? ReadVector(metadata.breakerHandles[0].localPosition, fallback)
                : fallback;
            Vector3 forward = metadata.breakerHandles != null && metadata.breakerHandles.Length > 0
                ? NormalizeOrFallback(ReadVector(metadata.breakerHandles[0].localForward, Vector3.forward), Vector3.forward)
                : Vector3.forward;
            Vector3 axis = metadata.breakerHandles != null && metadata.breakerHandles.Length > 0
                ? NormalizeOrFallback(ReadVector(metadata.breakerHandles[0].localRotationAxis, Vector3.up), Vector3.up)
                : ResolveAnalyticBreakerAxis(bounds);
            axis = ResolveSafeRotationAxis(forward, axis);
            handle.transform.localPosition = localPosition;
            handle.transform.localRotation = Quaternion.LookRotation(forward, axis);
            handle.transform.localScale = Vector3.one;
            return handle.transform;
        }

        private static BreakerHandleData[] BuildBreakerHandles(
            Transform root,
            Transform primaryHandle,
            PowerSourceGroup group,
            FactoryMetadata metadata,
            Renderer[] renderers,
            out Transform[] handleTransforms)
        {
            if (metadata.breakerHandles != null && metadata.breakerHandles.Length > 0)
            {
                BreakerHandleData[] result = new BreakerHandleData[metadata.breakerHandles.Length];
                handleTransforms = new Transform[metadata.breakerHandles.Length];
                for (int i = 0; i < metadata.breakerHandles.Length; i++)
                {
                    BreakerHandleMetadata source = metadata.breakerHandles[i];
                    GameObject handleObject = i == 0 ? primaryHandle.gameObject : new GameObject("IK_Handle_" + i.ToString(CultureInfo.InvariantCulture));
                    if (i != 0)
                        handleObject.transform.SetParent(root, false);

                    Vector3 position = ReadVector(source.localPosition, primaryHandle.localPosition);
                    Vector3 forward = NormalizeOrFallback(ReadVector(source.localForward, Vector3.forward), Vector3.forward);
                    Vector3 axis = NormalizeOrFallback(ReadVector(source.localRotationAxis, Vector3.up), Vector3.up);
                    axis = ResolveSafeRotationAxis(forward, axis);
                    handleObject.transform.localPosition = position;
                    handleObject.transform.localRotation = Quaternion.LookRotation(forward, axis);
                    handleObject.transform.localScale = Vector3.one;

                    result[i] = new BreakerHandleData
                    {
                        stableHash = ResolveHandleHash(group.name, source.id, i),
                        minAngleDegrees = math.isfinite(source.minAngleDegrees) ? source.minAngleDegrees : 0f,
                        maxAngleDegrees = math.isfinite(source.maxAngleDegrees) ? source.maxAngleDegrees : 90f,
                        gripRadiusMeters = SanitizeNonNegativeFinite(source.gripRadiusMeters, 0.06f),
                        portIndex = math.max(0, source.portIndex),
                        localPosition = ToFloat3(position),
                        localForward = ToFloat3(forward),
                        localRotationAxis = ToFloat3(axis)
                    };
                    handleTransforms[i] = handleObject.transform;
                }

                SortHandlesByPortThenHash(result, handleTransforms);
                return result;
            }

            Bounds bounds = ResolveBounds(renderers);
            Vector3 localPosition = primaryHandle.localPosition;
            Vector3 axisFallback = ResolveSafeRotationAxis(Vector3.forward, ResolveAnalyticBreakerAxis(bounds));
            handleTransforms = new[] { primaryHandle };
            return new[]
            {
                new BreakerHandleData
                {
                    minAngleDegrees = 0f,
                    maxAngleDegrees = 90f,
                    gripRadiusMeters = 0.06f,
                    portIndex = 0,
                    stableHash = ResolveHandleHash(group.name, null, 0),
                    localPosition = ToFloat3(localPosition),
                    localForward = new float3(0f, 0f, 1f),
                    localRotationAxis = ToFloat3(axisFallback)
                }
            };
        }

        private static bool AttachTypedRuntimeComponents(
            GameObject root,
            PowerNode powerNode,
            BreakerMetadata breakerMetadata,
            PowerStatusEmissiveBinding emissiveBinding,
            PowerSourceGroup group,
            FactoryMetadata metadata,
            float baseWattage,
            float batteryCapacity,
            float resistance,
            int priority,
            PrefabMetric metric)
        {
            PowerRelayNode relay = null;
            BatteryBankModule battery = null;
            Component rtg = null;
            bool typedPowerOwnerAttached = false;

            if (group.nodeTypeId == PowerNodeTypeID.Relay)
            {
                relay = root.AddComponent<PowerRelayNode>();
                SerializedObject serialized = new SerializedObject(relay);
                SetSerializedFloatIfPresent(serialized, "standbyDrain", SanitizeNonNegativeFinite(metadata.standbyDrainWatts, 1.5f));
                SetSerializedFloatIfPresent(serialized, "relayHandoffLoss", SanitizeNonNegativeFinite(metadata.relayHandoffLossWatts, 0.35f));
                SetSerializedFloatIfPresent(serialized, "resistanceLossPerMeter", resistance);
                SetSerializedIntIfPresent(serialized, "powerPriority", priority);
                serialized.ApplyModifiedPropertiesWithoutUndo();
                metric.runtimeComponentCount++;
                typedPowerOwnerAttached = true;
            }

            if (group.nodeTypeId == PowerNodeTypeID.Battery)
            {
                battery = root.AddComponent<BatteryBankModule>();
                SerializedObject serialized = new SerializedObject(battery);
                SetSerializedFloatIfPresent(serialized, "energyCapacityWattSeconds", math.max(1f, batteryCapacity));
                SetSerializedFloatIfPresent(serialized, "initialChargeNormalized", math.saturate(metadata.initialChargeNormalized <= 0f ? 1f : metadata.initialChargeNormalized));
                SetSerializedFloatIfPresent(serialized, "maxChargePowerWatts", math.max(0f, metadata.maxChargePowerWatts <= 0f ? math.max(400f, math.abs(baseWattage)) : metadata.maxChargePowerWatts));
                SetSerializedFloatIfPresent(serialized, "maxDischargePowerWatts", math.max(0f, metadata.maxDischargePowerWatts <= 0f ? math.max(500f, math.abs(baseWattage)) : metadata.maxDischargePowerWatts));
                serialized.ApplyModifiedPropertiesWithoutUndo();
                metric.runtimeComponentCount++;
                typedPowerOwnerAttached = true;
            }

            if (group.nodeTypeId == PowerNodeTypeID.Rtg)
            {
                rtg = AddComponentByTypeName(root, "Hecton8.Power.Generators.RadioisotopeThermalGenerator");
                if (rtg != null)
                {
                    SerializedObject serialized = new SerializedObject(rtg);
                    SetSerializedStringIfPresent(serialized, "stableRtgId", group.name);
                    SetSerializedIntIfPresent(serialized, "sourceIdOverride", unchecked((int)(StableHash(group.name) & 0x7FFFFFFFu)));
                    SetSerializedFloatIfPresent(serialized, "baseOutputWatts", math.max(0f, baseWattage));
                    SetSerializedFloatIfPresent(serialized, "halfLifeHours", SanitizeNonNegativeFinite(metadata.halfLifeHours, 180f));
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                    metric.runtimeComponentCount++;
                    typedPowerOwnerAttached = true;
                }
                else
                {
                    metric.warnings.Add("RTG runtime type not found; prefab keeps NetworkNodeData + PowerNode base output only.");
                }
            }

            PowerBreakerRuntime breakerRuntime = root.AddComponent<PowerBreakerRuntime>();
            breakerRuntime.ConfigureEditorBake(
                powerNode,
                breakerMetadata,
                emissiveBinding,
                metadata.defaultClosed,
                BuildActivationTargets(powerNode, relay, battery, rtg));
            metric.runtimeComponentCount++;
            return typedPowerOwnerAttached;
        }

        private static MonoBehaviour[] BuildActivationTargets(
            PowerNode powerNode,
            PowerRelayNode relay,
            BatteryBankModule battery,
            Component rtg)
        {
            int count = powerNode != null ? 1 : 0;
            if (relay != null)
                count++;
            if (battery != null)
                count++;
            if (rtg is MonoBehaviour)
                count++;

            MonoBehaviour[] targets = new MonoBehaviour[count];
            int index = 0;
            if (powerNode != null)
                targets[index++] = powerNode;
            if (relay != null)
                targets[index++] = relay;
            if (battery != null)
                targets[index++] = battery;
            if (rtg is MonoBehaviour rtgBehaviour)
                targets[index] = rtgBehaviour;
            return targets;
        }

        private static Component AddComponentByTypeName(GameObject root, string fullTypeName)
        {
            Type type = FindType(fullTypeName);
            return type != null && typeof(Component).IsAssignableFrom(type)
                ? root.AddComponent(type)
                : null;
        }

        private static Type FindType(string fullTypeName)
        {
            Type direct = Type.GetType(fullTypeName);
            if (direct != null)
                return direct;

            System.Reflection.Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length; i++)
            {
                Type type = assemblies[i].GetType(fullTypeName);
                if (type != null)
                    return type;
            }

            return null;
        }

        private static void ValidateBeforeSave(
            GameObject root,
            PowerNode powerNode,
            NetworkNodeData nodeData,
            BreakerMetadata breakerMetadata,
            PowerStatusEmissiveBinding emissiveBinding,
            string emissionColorProperty,
            string emissionStrengthProperty,
            PowerNodeTypeID nodeType,
            float baseWattage,
            PrefabMetric metric)
        {
            if (root.GetComponent<MeshCollider>() != null)
                throw new InvalidOperationException("root MeshCollider is forbidden.");
            if (root.GetComponentsInChildren<MeshCollider>(true).Length != 0)
                throw new InvalidOperationException("MeshCollider is forbidden in power prefabs.");
            if (root.GetComponentsInChildren<ParticleSystem>(true).Length != 0)
                throw new InvalidOperationException("ParticleSystem is forbidden in assembled power prefab.");
            if (nodeData == null || nodeData.NetworkTypeID != LogisticsNetworkTypeID.PowerDc)
                throw new InvalidOperationException("NetworkNodeData missing PowerDc graph identity.");
            if (!nodeData.TryBuildNodeBakeDTO(out NetworkNodeBakeDTO nodeDto) || nodeDto.StableNodeHash == 0u)
                throw new InvalidOperationException("NetworkNodeData has invalid primary node row.");
            if (math.abs(nodeDto.BaseWattage - baseWattage) > 0.0001f)
                throw new InvalidOperationException("NetworkNodeData bake row lost base wattage.");
            if (!nodeData.TryBuildPowerNodeDTO(out PowerNodeDTO powerDto) || powerDto.NodeHash == 0u)
                throw new InvalidOperationException("NetworkNodeData cannot build PowerNodeDTO.");
            if (math.abs(nodeData.PowerBaseWattage - baseWattage) > 0.0001f)
                throw new InvalidOperationException("NetworkNodeData lost serialized base wattage.");
            if (!NetworkNodeData.ValidateUnmanagedLayout(out int nodeBytes, out int portBytes, out int fluidPipeBytes, out int powerNodeBytes))
                throw new InvalidOperationException("NetworkNodeData unmanaged layout invalid. node=" + nodeBytes.ToString(CultureInfo.InvariantCulture) + " port=" + portBytes.ToString(CultureInfo.InvariantCulture) + " fluid=" + fluidPipeBytes.ToString(CultureInfo.InvariantCulture) + " power=" + powerNodeBytes.ToString(CultureInfo.InvariantCulture));
            if (!BreakerHandleData.ValidateBreakerHandleDataLayout())
                throw new InvalidOperationException("BreakerHandleData is not 64-byte ARM64-aligned.");
            if (breakerMetadata == null || breakerMetadata.HandleCount <= 0)
                throw new InvalidOperationException("BreakerMetadata has no serialized handle rows.");
            for (int i = 0; i < breakerMetadata.HandleCount; i++)
            {
                if (!breakerMetadata.TryGetHandle(i, out BreakerHandleData handle) || handle.stableHash == 0u)
                    throw new InvalidOperationException("BreakerMetadata handle row is invalid.");
                if (math.abs(math.dot(handle.localForward, handle.localRotationAxis)) > 0.95f)
                    throw new InvalidOperationException("BreakerMetadata handle basis is degenerate.");
                if (!breakerMetadata.TryGetHandleTransform(i, out _))
                    throw new InvalidOperationException("BreakerMetadata handle transform is missing.");
            }
            if (emissiveBinding.RendererCount <= 0)
                throw new InvalidOperationException("PowerStatusEmissiveBinding has no renderer references.");
            ValidatePowerComponentOwnership(root, powerNode, nodeType, baseWattage);

            MeshRenderer[] renderers = root.GetComponentsInChildren<MeshRenderer>(true);
            for (int i = 0; i < renderers.Length; i++)
                ValidateRendererMaterials(renderers[i], emissionColorProperty, emissionStrengthProperty, metric);
        }

        private static void ValidatePowerComponentOwnership(GameObject root, PowerNode powerNode, PowerNodeTypeID nodeType, float baseWattage)
        {
            if (root == null || powerNode == null)
                throw new InvalidOperationException("Power prefab has no PowerNode owner.");

            bool hasTypedPowerOwner =
                root.GetComponent<PowerRelayNode>() != null ||
                root.GetComponent<BatteryBankModule>() != null ||
                HasComponentByTypeName(root, "Hecton8.Power.Generators.RadioisotopeThermalGenerator");

            SerializedObject powerNodeSerialized = new SerializedObject(powerNode);
            SerializedProperty fallback = powerNodeSerialized.FindProperty("fallbackPowerRating");
            float fallbackWatts = fallback != null ? fallback.floatValue : 0f;
            float expectedFallbackWatts = ResolvePowerNodeFallbackWattage(nodeType, baseWattage, hasTypedPowerOwner);
            if (math.abs(fallbackWatts - expectedFallbackWatts) > 0.0001f)
                throw new InvalidOperationException("Power prefab has duplicate base wattage ownership on PowerNode.");
        }

        private static bool HasComponentByTypeName(GameObject root, string fullTypeName)
        {
            Type type = FindType(fullTypeName);
            return type != null && root.GetComponent(type) != null;
        }

        private static void ValidateSavedPrefab(string prefabPath, PrefabMetric metric)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
                throw new InvalidOperationException("Saved prefab failed to load: " + prefabPath);
            if (prefab.GetComponentsInChildren<MeshCollider>(true).Length != 0)
                throw new InvalidOperationException("Saved prefab contains MeshCollider: " + prefabPath);
            if (prefab.GetComponentsInChildren<ParticleSystem>(true).Length != 0)
                throw new InvalidOperationException("Saved prefab contains ParticleSystem: " + prefabPath);
            if (AssetPathUsesUnityBuiltInPrimitiveMesh(prefabPath))
                throw new InvalidOperationException("Saved power prefab contains Unity built-in primitive mesh: " + prefabPath);
            if (prefab.GetComponent<NetworkNodeData>() == null)
                throw new InvalidOperationException("Saved prefab missing NetworkNodeData: " + prefabPath);
            if (prefab.GetComponent<BreakerMetadata>() == null)
                throw new InvalidOperationException("Saved prefab missing BreakerMetadata: " + prefabPath);
            if (prefab.GetComponent<PowerStatusEmissiveBinding>() == null)
                throw new InvalidOperationException("Saved prefab missing PowerStatusEmissiveBinding: " + prefabPath);
            PowerBreakerRuntime breaker = prefab.GetComponent<PowerBreakerRuntime>();
            if (breaker == null || !breaker.HasSerializedRuntimeBindings || !breaker.HasValidActivationTargets)
                throw new InvalidOperationException("Saved prefab missing valid PowerBreakerRuntime bindings: " + prefabPath);
            metric.savedPrefabValidated = true;
        }

        private static bool AssetPathUsesUnityBuiltInPrimitiveMesh(string assetPath)
        {
            if (string.IsNullOrWhiteSpace(assetPath))
                return false;

            string fullPath = ProjectRelativeToFullPath(assetPath);
            if (!File.Exists(fullPath))
                return false;

            string prefabText = File.ReadAllText(fullPath);
            return prefabText.IndexOf(BuiltInPrimitiveMeshGuid, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static void ValidateRendererMaterials(
            MeshRenderer renderer,
            string emissionColorProperty,
            string emissionStrengthProperty,
            PrefabMetric metric)
        {
            Material[] materials = renderer.sharedMaterials;
            if (materials == null || materials.Length == 0)
                throw new InvalidOperationException(renderer.name + " has no shared material.");

            for (int i = 0; i < materials.Length; i++)
            {
                Material material = materials[i];
                if (material == null)
                    throw new InvalidOperationException(renderer.name + " has null material slot.");

                if (!material.HasProperty(emissionColorProperty))
                    throw new InvalidOperationException(material.name + " does not expose " + emissionColorProperty + ".");

                if (!material.HasProperty(emissionStrengthProperty))
                    throw new InvalidOperationException(material.name + " does not expose " + emissionStrengthProperty + ".");

                Shader shader = material.shader;
                if (shader == null)
                    throw new InvalidOperationException(material.name + " has null shader.");

                if (!ShaderSourceContainsUnityPerMaterialCBuffer(shader, out string shaderPath))
                    throw new InvalidOperationException(shader.name + " is not proven SRP Batcher compatible by CBUFFER_START(UnityPerMaterial). path=" + shaderPath);

                metric.brgMaterialAuditCount++;
            }
        }

        private static bool ShaderSourceContainsUnityPerMaterialCBuffer(Shader shader, out string shaderPath)
        {
            shaderPath = AssetDatabase.GetAssetPath(shader);
            if (string.IsNullOrEmpty(shaderPath))
                return false;

            string fullPath = ProjectRelativeToFullPath(shaderPath);
            return FileContainsUnityPerMaterialCBuffer(fullPath, 0);
        }

        private static bool FileContainsUnityPerMaterialCBuffer(string fullPath, int depth)
        {
            if (string.IsNullOrEmpty(fullPath) || depth > MaxShaderIncludeScanDepth)
                return false;

            if (!File.Exists(fullPath))
                return false;

            string source = File.ReadAllText(fullPath);
            if (source.IndexOf("CBUFFER_START(UnityPerMaterial)", StringComparison.Ordinal) >= 0)
                return true;

            string directory = Path.GetDirectoryName(fullPath);
            int searchIndex = 0;
            while (TryReadNextQuotedShaderInclude(source, ref searchIndex, directory, out string includeFullPath))
            {
                if (FileContainsUnityPerMaterialCBuffer(includeFullPath, depth + 1))
                    return true;
            }

            return false;
        }

        private static bool TryReadNextQuotedShaderInclude(
            string source,
            ref int searchIndex,
            string parentDirectory,
            out string includeFullPath)
        {
            includeFullPath = null;
            while (searchIndex < source.Length)
            {
                int includeIndex = source.IndexOf("#include", searchIndex, StringComparison.Ordinal);
                if (includeIndex < 0)
                    return false;

                searchIndex = includeIndex + 8;
                int lineEnd = source.IndexOf('\n', searchIndex);
                if (lineEnd < 0)
                    lineEnd = source.Length;

                int quoteSearchLength = lineEnd - searchIndex;
                int quoteStart = quoteSearchLength > 0
                    ? source.IndexOf('"', searchIndex, quoteSearchLength)
                    : -1;
                if (quoteStart < 0)
                {
                    searchIndex = lineEnd + 1;
                    continue;
                }

                int quoteEndSearchStart = quoteStart + 1;
                int quoteEndSearchLength = lineEnd - quoteEndSearchStart;
                int quoteEnd = quoteEndSearchLength > 0
                    ? source.IndexOf('"', quoteEndSearchStart, quoteEndSearchLength)
                    : -1;
                if (quoteEnd < 0)
                {
                    searchIndex = lineEnd + 1;
                    continue;
                }

                searchIndex = quoteEnd + 1;
                int includeLength = quoteEnd - quoteStart - 1;
                if (includeLength <= 0)
                    continue;

                string includePath = source.Substring(quoteStart + 1, includeLength).Replace('\\', '/');
                if (includePath.StartsWith("Assets/", StringComparison.Ordinal))
                {
                    includeFullPath = ProjectRelativeToFullPath(includePath);
                    return true;
                }

                if (string.IsNullOrEmpty(parentDirectory))
                    continue;

                includeFullPath = Path.GetFullPath(Path.Combine(parentDirectory, includePath));
                return true;
            }

            return false;
        }

        private static Material ResolveSharedMaterial(FactoryMetadata metadata, FactoryReport report)
        {
            string requested = string.IsNullOrEmpty(metadata.materialName) ? DefaultMaterialName : metadata.materialName;
            Material material = FindMaterialExact(requested);
            if (material != null)
                return material;

            material = FindMaterialExact(DefaultMaterialName);
            if (material != null)
                return material;

            material = FindFirstMaterialContaining("Equipment_Atlas");
            if (material != null)
                return material;

            report.violations.Add("FATAL: shared equipment material not found: " + requested);
            return null;
        }

        private static Material FindMaterialExact(string materialName)
        {
            string[] guids = AssetDatabase.FindAssets(materialName + " t:Material");
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material != null && string.Equals(material.name, materialName, StringComparison.OrdinalIgnoreCase))
                    return material;
            }

            return null;
        }

        private static Material FindFirstMaterialContaining(string token)
        {
            string[] guids = AssetDatabase.FindAssets("t:Material");
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material != null && material.name.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0)
                    return material;
            }

            return null;
        }

        private static FactoryMetadata LoadMetadata(string baseName, string visualPath)
        {
            for (int i = 0; i < MetadataDirectories.Length; i++)
            {
                string directory = MetadataDirectories[i];
                if (!AssetDatabase.IsValidFolder(directory))
                    continue;

                string candidate = directory + "/" + baseName + ".json";
                if (File.Exists(ProjectRelativeToFullPath(candidate)))
                    return ReadMetadata(candidate);
            }

            if (!string.IsNullOrEmpty(visualPath))
            {
                string candidate = Path.ChangeExtension(visualPath, ".json");
                if (!string.IsNullOrEmpty(candidate) && File.Exists(ProjectRelativeToFullPath(candidate)))
                    return ReadMetadata(candidate);
            }

            return null;
        }

        private static FactoryMetadata ReadMetadata(string assetPath)
        {
            string fullPath = ProjectRelativeToFullPath(assetPath);
            string json = File.ReadAllText(fullPath);
            FactoryMetadata metadata = JsonUtility.FromJson<FactoryMetadata>(json);
            return metadata ?? new FactoryMetadata();
        }

        private static void ConfigurePowerNode(PowerNode node, float baseWattage, int priority)
        {
            SerializedObject serialized = new SerializedObject(node);
            SetSerializedFloatIfPresent(serialized, "fallbackPowerRating", baseWattage);
            SetSerializedIntIfPresent(serialized, "fallbackPowerPriority", priority);
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetSerializedFloatIfPresent(SerializedObject serialized, string propertyName, float value)
        {
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property != null)
                property.floatValue = value;
        }

        private static void SetSerializedIntIfPresent(SerializedObject serialized, string propertyName, int value)
        {
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property != null)
                property.intValue = value;
        }

        private static void SetSerializedStringIfPresent(SerializedObject serialized, string propertyName, string value)
        {
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property != null)
                property.stringValue = value ?? string.Empty;
        }

        private static PowerNodeTypeID ResolveNodeType(string name, FactoryMetadata metadata)
        {
            string source = metadata != null && !string.IsNullOrEmpty(metadata.nodeTypeId) ? metadata.nodeTypeId : name;
            if (Contains(source, "rtg") || Contains(source, "radioisotope"))
                return PowerNodeTypeID.Rtg;
            if (Contains(source, "reactor"))
                return PowerNodeTypeID.Reactor;
            if (Contains(source, "battery"))
                return PowerNodeTypeID.Battery;
            if (Contains(source, "relay"))
                return PowerNodeTypeID.Relay;
            if (Contains(source, "breaker") || Contains(source, "switch"))
                return PowerNodeTypeID.Breaker;
            if (Contains(source, "junction") || Contains(source, "box"))
                return PowerNodeTypeID.Junction;
            if (Contains(source, "generator"))
                return PowerNodeTypeID.Generator;
            return PowerNodeTypeID.Junction;
        }

        private static float ResolveBaseWattage(PowerNodeTypeID nodeType, FactoryMetadata metadata)
        {
            if (metadata != null && math.isfinite(metadata.baseWattage) && math.abs(metadata.baseWattage) > 0.0001f)
                return metadata.baseWattage;

            switch (nodeType)
            {
                case PowerNodeTypeID.Reactor:
                    return 750000f;
                case PowerNodeTypeID.Rtg:
                    return 180f;
                case PowerNodeTypeID.Generator:
                    return 5000f;
                case PowerNodeTypeID.Relay:
                    return -1.5f;
                case PowerNodeTypeID.Breaker:
                case PowerNodeTypeID.Junction:
                case PowerNodeTypeID.Battery:
                default:
                    return 0f;
            }
        }

        private static float ResolveCapacityWatts(PowerNodeTypeID nodeType, float baseWattage, FactoryMetadata metadata)
        {
            if (metadata != null && metadata.baseCapacityWatts > 0f && math.isfinite(metadata.baseCapacityWatts))
                return metadata.baseCapacityWatts;

            switch (nodeType)
            {
                case PowerNodeTypeID.Battery:
                    float chargeWatts = metadata != null ? SanitizeNonNegativeFinite(metadata.maxChargePowerWatts, 0f) : 0f;
                    float dischargeWatts = metadata != null ? SanitizeNonNegativeFinite(metadata.maxDischargePowerWatts, 0f) : 0f;
                    return math.max(500f, math.max(math.abs(baseWattage), math.max(chargeWatts, dischargeWatts)));
                case PowerNodeTypeID.Relay:
                case PowerNodeTypeID.Breaker:
                case PowerNodeTypeID.Junction:
                    return math.max(1000f, math.abs(baseWattage));
                default:
                    return math.max(0.001f, math.abs(baseWattage));
            }
        }

        private static float ResolveBatteryCapacity(PowerNodeTypeID nodeType, FactoryMetadata metadata)
        {
            if (metadata != null && metadata.batteryCapacityWattSeconds > 0f && math.isfinite(metadata.batteryCapacityWattSeconds))
                return metadata.batteryCapacityWattSeconds;
            return nodeType == PowerNodeTypeID.Battery ? 120000f : 0f;
        }

        private static int[] NormalizePorts(int[] ports, PowerNodeTypeID nodeType)
        {
            if (ports != null && ports.Length > 0)
            {
                int[] copy = new int[ports.Length];
                Array.Copy(ports, copy, ports.Length);
                Array.Sort(copy);
                return copy;
            }

            int count = nodeType == PowerNodeTypeID.Relay || nodeType == PowerNodeTypeID.Junction ? 6 : 2;
            int[] generated = new int[count];
            for (int i = 0; i < generated.Length; i++)
                generated[i] = i;
            return generated;
        }

        private static NetworkPortDescriptor[] BuildPowerPorts(int[] portIds, Renderer[] renderers)
        {
            int count = portIds != null && portIds.Length > 0 ? portIds.Length : 1;
            NetworkPortDescriptor[] descriptors = new NetworkPortDescriptor[count];
            Bounds bounds = ResolveBounds(renderers);
            Vector3 center = bounds.center;
            Vector3 extents = bounds.extents;
            float horizontalRadius = math.max(0.25f, math.max(extents.x, extents.z) + 0.08f);
            float verticalRadius = math.max(0.25f, extents.y + 0.08f);

            for (int i = 0; i < count; i++)
            {
                Vector3 direction = ResolvePortDirection(i, count);
                descriptors[i] = new NetworkPortDescriptor
                {
                    PortID = portIds != null && i < portIds.Length ? portIds[i] : i,
                    PortTypeID = LogisticsNetworkPortTypeID.Power,
                    LocalPosition = center + ScalePortOffset(direction, horizontalRadius, verticalRadius),
                    LocalDirection = direction,
                    CapacityScale = 1f
                };
            }

            return descriptors;
        }

        private static Vector3 ScalePortOffset(Vector3 direction, float horizontalRadius, float verticalRadius)
        {
            return new Vector3(
                direction.x * horizontalRadius,
                direction.y * verticalRadius,
                direction.z * horizontalRadius);
        }

        private static Vector3 ResolvePortDirection(int index, int count)
        {
            if (count <= 1)
                return Vector3.forward;

            if (count == 2)
                return index == 0 ? Vector3.forward : Vector3.back;

            int slot = count <= 6 ? index : index % 6;
            switch (slot)
            {
                case 0:
                    return Vector3.forward;
                case 1:
                    return Vector3.right;
                case 2:
                    return Vector3.back;
                case 3:
                    return Vector3.left;
                case 4:
                    return Vector3.up;
                default:
                    return Vector3.down;
            }
        }

        private static LogisticsNetworkNodeTypeID ResolveLogisticsNodeType(PowerNodeTypeID nodeType)
        {
            if (nodeType == PowerNodeTypeID.Reactor ||
                nodeType == PowerNodeTypeID.Rtg ||
                nodeType == PowerNodeTypeID.Generator)
            {
                return LogisticsNetworkNodeTypeID.Producer;
            }

            if (nodeType == PowerNodeTypeID.Relay || nodeType == PowerNodeTypeID.Battery)
                return LogisticsNetworkNodeTypeID.Relay;

            return LogisticsNetworkNodeTypeID.Junction;
        }

        private static float ResolvePowerNodeFallbackWattage(PowerNodeTypeID nodeType, float baseWattage, bool typedPowerOwnerAttached)
        {
            if (typedPowerOwnerAttached)
                return 0f;

            switch (nodeType)
            {
                case PowerNodeTypeID.Relay:
                case PowerNodeTypeID.Breaker:
                case PowerNodeTypeID.Junction:
                case PowerNodeTypeID.Battery:
                    return 0f;
                default:
                    return baseWattage;
            }
        }

        private static uint ResolvePowerNodeFlags(PowerNodeTypeID nodeType, float batteryCapacity)
        {
            uint flags = 0u;
            if (nodeType == PowerNodeTypeID.Battery || batteryCapacity > 0f)
                flags |= PowerGridJacobiConstants.NodeFlagBattery;
            return flags;
        }

        private static Bounds ResolveBounds(Renderer[] renderers)
        {
            bool hasBounds = false;
            Bounds bounds = new Bounds(Vector3.zero, Vector3.one);
            if (renderers != null)
            {
                for (int i = 0; i < renderers.Length; i++)
                {
                    Renderer renderer = renderers[i];
                    if (renderer == null)
                        continue;

                    if (!hasBounds)
                    {
                        bounds = renderer.bounds;
                        hasBounds = true;
                    }
                    else
                    {
                        bounds.Encapsulate(renderer.bounds);
                    }
                }
            }

            return hasBounds ? bounds : new Bounds(Vector3.zero, Vector3.one);
        }

        private static Vector3 ResolveAnalyticBreakerAxis(Bounds bounds)
        {
            Vector3 size = bounds.size;
            if (size.y >= size.x && size.y >= size.z)
                return Vector3.right;
            return size.x >= size.z ? Vector3.forward : Vector3.up;
        }

        private static Vector3 ResolveSafeRotationAxis(Vector3 forward, Vector3 axis)
        {
            Vector3 safeForward = NormalizeOrFallback(forward, Vector3.forward);
            Vector3 safeAxis = NormalizeOrFallback(axis, Vector3.up);
            if (math.abs(Vector3.Dot(safeForward, safeAxis)) <= 0.95f)
                return safeAxis;

            Vector3 candidate = math.abs(safeForward.y) < 0.75f ? Vector3.up : Vector3.right;
            Vector3 projected = candidate - safeForward * Vector3.Dot(candidate, safeForward);
            return NormalizeOrFallback(projected, candidate);
        }

        private static Vector3 ReadVector(float[] values, Vector3 fallback)
        {
            if (values == null || values.Length < 3)
                return fallback;

            float x = math.select(fallback.x, values[0], math.isfinite(values[0]));
            float y = math.select(fallback.y, values[1], math.isfinite(values[1]));
            float z = math.select(fallback.z, values[2], math.isfinite(values[2]));
            return new Vector3(x, y, z);
        }

        private static Color ResolveColor(float[] values, Color fallback)
        {
            if (values == null || values.Length < 3)
                return fallback;

            float a = values.Length >= 4 ? values[3] : fallback.a;
            return new Color(
                math.saturate(math.select(fallback.r, values[0], math.isfinite(values[0]))),
                math.saturate(math.select(fallback.g, values[1], math.isfinite(values[1]))),
                math.saturate(math.select(fallback.b, values[2], math.isfinite(values[2]))),
                math.saturate(math.select(fallback.a, a, math.isfinite(a))));
        }

        private static Vector3 NormalizeOrFallback(Vector3 value, Vector3 fallback)
        {
            float lengthSq = value.sqrMagnitude;
            if (!math.isfinite(lengthSq) || lengthSq <= 0.000001f)
                return fallback;

            return value / Mathf.Sqrt(lengthSq);
        }

        private static int CompareHandlesByPortThenHash(BreakerHandleData a, BreakerHandleData b)
        {
            int port = a.portIndex.CompareTo(b.portIndex);
            return port != 0 ? port : a.stableHash.CompareTo(b.stableHash);
        }

        private static void SortHandlesByPortThenHash(BreakerHandleData[] handles, Transform[] transforms)
        {
            int count = handles != null ? handles.Length : 0;
            if (count <= 1)
                return;

            for (int i = 1; i < count; i++)
            {
                BreakerHandleData handle = handles[i];
                Transform transform = transforms != null && i < transforms.Length ? transforms[i] : null;
                int j = i - 1;
                while (j >= 0 && CompareHandlesByPortThenHash(handles[j], handle) > 0)
                {
                    handles[j + 1] = handles[j];
                    if (transforms != null && j + 1 < transforms.Length)
                        transforms[j + 1] = transforms[j];
                    j--;
                }

                handles[j + 1] = handle;
                if (transforms != null && j + 1 < transforms.Length)
                    transforms[j + 1] = transform;
            }
        }

        private static uint ResolveHandleHash(string groupName, string metadataId, int index)
        {
            if (!string.IsNullOrEmpty(metadataId))
                return StableHash(metadataId);

            unchecked
            {
                uint hash = StableHash(groupName);
                hash ^= (uint)(index + 1);
                hash *= 16777619u;
                return hash == 0u ? 2166136261u : hash;
            }
        }

        private static uint StableHash(string value)
        {
            unchecked
            {
                uint hash = 2166136261u;
                if (!string.IsNullOrEmpty(value))
                {
                    for (int i = 0; i < value.Length; i++)
                    {
                        hash ^= value[i];
                        hash *= 16777619u;
                    }
                }

                return hash == 0u ? 2166136261u : hash;
            }
        }

        private static int CompareGroupsByName(PowerSourceGroup a, PowerSourceGroup b)
        {
            return string.Compare(a.name, b.name, StringComparison.OrdinalIgnoreCase);
        }

        private static float3 ToFloat3(Vector3 value)
        {
            return new float3(value.x, value.y, value.z);
        }

        private static bool IsPowerVisualAsset(string path)
        {
            if (string.IsNullOrEmpty(path))
                return false;
            string name = Path.GetFileNameWithoutExtension(path);
            if (name.StartsWith("COL_", StringComparison.OrdinalIgnoreCase))
                return false;
            return Contains(name, "rtg") ||
                   Contains(name, "reactor") ||
                   Contains(name, "relay") ||
                   Contains(name, "breaker") ||
                   Contains(name, "battery") ||
                   Contains(name, "generator") ||
                   Contains(name, "power");
        }

        private static string NormalizeBaseName(string raw)
        {
            string name = raw ?? "PowerNode";
            name = StripPrefix(name, "MESH_");
            name = StripPrefix(name, "SM_");
            name = StripPrefix(name, "VIS_");
            name = StripPrefix(name, "PFB_");
            name = StripPrefix(name, "Gen_");
            name = StripSuffix(name, "_LOD0");
            name = StripSuffix(name, "_High");
            name = StripSuffix(name, "_Visual");
            return SanitizeAssetName(name);
        }

        private static string StripPrefix(string value, string prefix)
        {
            return value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ? value.Substring(prefix.Length) : value;
        }

        private static string StripSuffix(string value, string suffix)
        {
            return value.EndsWith(suffix, StringComparison.OrdinalIgnoreCase) ? value.Substring(0, value.Length - suffix.Length) : value;
        }

        private static string SanitizeAssetName(string value)
        {
            if (string.IsNullOrEmpty(value))
                return "PowerNode";

            StringBuilder builder = new StringBuilder(value.Length);
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                builder.Append(char.IsLetterOrDigit(c) || c == '_' || c == '-' ? c : '_');
            }

            return builder.ToString();
        }

        private static bool Contains(string source, string token)
        {
            return source != null && source.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string ResolvePropertyName(string value, string fallback)
        {
            return string.IsNullOrEmpty(value) ? fallback : value;
        }

        private static float SanitizeResistance(float value)
        {
            return math.max(0.0001f, math.select(0.0001f, value, math.isfinite(value)));
        }

        private static float SanitizeNonNegativeFinite(float value, float fallback)
        {
            return math.max(0f, math.select(fallback, value, math.isfinite(value)));
        }

        private static int ResolveLayer(string layerName, int fallback, FactoryReport report)
        {
            int layer = LayerMask.NameToLayer(layerName);
            if (layer < 0)
            {
                report.violations.Add("FATAL: Missing layer " + layerName + "; using fallback index " + fallback.ToString(CultureInfo.InvariantCulture) + " for dry assembly only.");
                return fallback;
            }

            return layer;
        }

        private static void SetLayerRecursively(GameObject root, int layer)
        {
            Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < transforms.Length; i++)
                transforms[i].gameObject.layer = layer;
        }

        private static void EnsureDirectory(string path)
        {
            if (string.IsNullOrEmpty(path))
                return;

            string normalized = path.Replace('\\', '/');
            if (normalized.StartsWith("Assets/", StringComparison.Ordinal))
            {
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
            else
            {
                Directory.CreateDirectory(ProjectRelativeToFullPath(normalized));
            }
        }

        private static string ExistingDirectoriesCsv(string[] directories)
        {
            StringBuilder builder = new StringBuilder(128);
            for (int i = 0; i < directories.Length; i++)
            {
                if (!AssetDatabase.IsValidFolder(directories[i]))
                    continue;
                if (builder.Length > 0)
                    builder.Append(", ");
                builder.Append(directories[i]);
            }

            return builder.Length > 0 ? builder.ToString() : "none";
        }

        private static string ProjectRelativeToFullPath(string path)
        {
            string normalized = path.Replace('\\', '/');
            if (Path.IsPathRooted(normalized))
                return normalized;

            return Path.GetFullPath(Path.Combine(Application.dataPath, "..", normalized));
        }

        private static long TicksToMicroseconds(long ticks)
        {
            return (long)(ticks * (1000000.0 / Stopwatch.Frequency));
        }

        private sealed class PowerSourceGroup
        {
            public string name;
            public string visualPath;
            public Mesh visualMesh;
            public GameObject sourcePrefab;
            public FactoryMetadata metadata;
            public PowerNodeTypeID nodeTypeId;
            public bool useAnalyticFallback;
        }

        [Serializable]
        private sealed class FactoryMetadata
        {
            public string nodeTypeId;
            public string materialName;
            public float baseWattage;
            public float baseCapacityWatts;
            public float baseResistance = 0.05f;
            public int defaultPriority = 50;
            public float batteryCapacityWattSeconds;
            public int[] connectivityPorts;
            public BreakerHandleMetadata[] breakerHandles;
            public string emissionColorProperty = DefaultEmissionColorProperty;
            public string emissionStrengthProperty = DefaultEmissionStrengthProperty;
            public string globalQualityProperty = DefaultGlobalQualityProperty;
            public float[] normalEmission;
            public float[] failureEmission;
            public float minEmissionStrength = 0.15f;
            public float maxEmissionStrength = 4f;
            public float pulseStrength = 0.65f;
            public bool defaultClosed = true;
            public float standbyDrainWatts = 1.5f;
            public float relayHandoffLossWatts = 0.35f;
            public float halfLifeHours = 180f;
            public float initialChargeNormalized = 1f;
            public float maxChargePowerWatts = 400f;
            public float maxDischargePowerWatts = 500f;

            public static FactoryMetadata DefaultFor(PowerNodeTypeID typeId)
            {
                return new FactoryMetadata
                {
                    nodeTypeId = typeId.ToString(),
                    materialName = DefaultMaterialName,
                    baseResistance = typeId == PowerNodeTypeID.Relay ? 0.025f : 0.05f,
                    connectivityPorts = typeId == PowerNodeTypeID.Relay || typeId == PowerNodeTypeID.Junction
                        ? new[] { 0, 1, 2, 3, 4, 5 }
                        : new[] { 0, 1 }
                };
            }
        }

        [Serializable]
        private sealed class BreakerHandleMetadata
        {
            public string id;
            public float[] localPosition;
            public float[] localForward;
            public float[] localRotationAxis;
            public float minAngleDegrees;
            public float maxAngleDegrees = 90f;
            public float gripRadiusMeters = 0.06f;
            public int portIndex;
        }

        public sealed class FactoryReport
        {
            public string factoryVersion;
            public string startedUtc;
            public string finishedUtc;
            public string outputDirectory;
            public int discoveredCount;
            public int assembledCount;
            public int fatalViolationCount;
            public long elapsedMicroseconds;
            public readonly List<string> violations = new List<string>(32);
            public readonly List<PrefabMetric> metrics = new List<PrefabMetric>(32);
        }

        public sealed class PrefabMetric
        {
            public string name = string.Empty;
            public string nodeType = string.Empty;
            public string sourcePath = string.Empty;
            public string outputPath = string.Empty;
            public string status = string.Empty;
            public string failure = string.Empty;
            public uint nodeHash;
            public float baseWattage;
            public float baseCapacityWatts;
            public float batteryCapacityWattSeconds;
            public int portCount;
            public int handleCount;
            public int rendererCount;
            public int materialSlotCount;
            public int primitiveColliderCount;
            public int strippedColliderCount;
            public int strippedMeshColliderCount;
            public int strippedParticleSystemCount;
            public int runtimeComponentCount;
            public int brgMaterialAuditCount;
            public bool savedPrefabValidated;
            public long elapsedMicroseconds;
            public readonly List<string> warnings = new List<string>(4);
        }
    }
}
#endif
