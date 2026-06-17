#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using Hecton8.Gameplay;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

namespace Hecton8.Editor.Assembly
{
    public sealed class HazardPrefabFactory : EditorWindow
    {
        private const string AgentId = "1736";
        private const string DefaultMeshDirectory = "Assets/_Project/Art/Generated/Flora";
        private const string DefaultMaterialDirectory = "Assets/_Project/Art/Materials";
        private const string DefaultCollisionProxyDirectory = "Assets/_Project/Art/Meshes/Cleaned";
        private const string DefaultMetadataDirectory = "Assets/_Project/Data/Environment/Hazards";
        private const string DefaultOutputDirectory = "Assets/Prefabs/Environment/Hazards";
        private const string ReportPath = "Docs/Reports/HAZARD_ASSEMBLER_REPORT_1736.json";
        private const string HazardTriggerLayerName = "Hazard_Trigger";
        private const string WorldStaticLayerName = "World_Static";
        private const float MinimumLodGroupSize = 0.05f;
        private const float DefaultTriggerPaddingMeters = 1.5f;
        private const float DefaultPresentationCullDistanceMeters = 34f;

        private static readonly List<Material> s_MaterialScratch = new List<Material>(512);
        private static readonly List<Collider> s_ColliderScratch = new List<Collider>(16);
        private static readonly List<MeshCollider> s_MeshColliderScratch = new List<MeshCollider>(4);
        private static readonly List<Rigidbody> s_RigidbodyScratch = new List<Rigidbody>(4);
        private static readonly List<MeshRenderer> s_RendererScratch = new List<MeshRenderer>(16);
        private static readonly List<ParticleSystem> s_ParticleScratch = new List<ParticleSystem>(4);
        private static readonly List<Light> s_LightScratch = new List<Light>(4);
        private static readonly List<DamageRouter> s_DamageRouterScratch = new List<DamageRouter>(4);
        private static readonly List<HazardMetadata> s_MetadataScratch = new List<HazardMetadata>(4);
        private static readonly List<HectonHazardSource> s_HazardSourceScratch = new List<HectonHazardSource>(2);
        private static readonly List<EnvironmentalHazard> s_EnvironmentalHazardScratch = new List<EnvironmentalHazard>(2);
        private static readonly List<DecalProjector> s_DecalScratch = new List<DecalProjector>(4);
        private static readonly List<LightCullingProxy> s_LightCullingScratch = new List<LightCullingProxy>(2);
        private static readonly List<ThermalVentRuntime> s_ThermalRuntimeScratch = new List<ThermalVentRuntime>(2);
        private static readonly List<MonoBehaviour> s_MonoBehaviourScratch = new List<MonoBehaviour>(8);
        private static readonly List<Transform> s_TransformScratch = new List<Transform>(32);
        private static readonly Dictionary<Shader, bool> s_ShaderCbufferCache = new Dictionary<Shader, bool>(64);

        [SerializeField] private string meshDirectory = DefaultMeshDirectory;
        [SerializeField] private string materialDirectory = DefaultMaterialDirectory;
        [SerializeField] private string collisionProxyDirectory = DefaultCollisionProxyDirectory;
        [SerializeField] private string metadataDirectory = DefaultMetadataDirectory;
        [SerializeField] private string outputDirectory = DefaultOutputDirectory;
        [SerializeField] private bool dryRun = true;
        [SerializeField] private int maxGroupsPerRun = 256;

        private Vector2 scroll;
        private FactoryReport lastReport;

        [MenuItem("Hecton8/Assembly/Hazard Prefab Factory 1736")]
        public static void OpenWindow()
        {
            HazardPrefabFactory window = GetWindow<HazardPrefabFactory>("Hazard Factory 1736");
            window.minSize = new Vector2(720f, 480f);
            window.Show();
        }

        [MenuItem("Hecton8/Assembly/Dry Run Hazard Prefab Factory 1736")]
        public static void RunDefaultDryRun()
        {
            FactorySettings settings = FactorySettings.Default;
            settings.DryRun = true;
            Run(settings);
        }

        [MenuItem("Hecton8/Assembly/Run Hazard Prefab Factory 1736")]
        public static void RunDefault()
        {
            FactorySettings settings = FactorySettings.Default;
            settings.DryRun = false;
            Run(settings);
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("HECTON-8 Hazard Prefab Factory 1736", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Offline assembly: toxic coral / vents / flares, primitive damage trigger, VFX_Anchor transforms, decal projector, no-shadow practical light, distance culling proxy, strict save validation.", MessageType.Info);

            meshDirectory = EditorGUILayout.TextField("Mesh Directory", meshDirectory);
            materialDirectory = EditorGUILayout.TextField("Material Directory", materialDirectory);
            collisionProxyDirectory = EditorGUILayout.TextField("Collision Proxy Directory", collisionProxyDirectory);
            metadataDirectory = EditorGUILayout.TextField("Metadata Directory", metadataDirectory);
            outputDirectory = EditorGUILayout.TextField("Output Directory", outputDirectory);
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
            EditorGUILayout.LabelField("Prefabs Failed", lastReport.PrefabsFailed.ToString(CultureInfo.InvariantCulture));
            EditorGUILayout.LabelField("Damage Triggers", lastReport.DamageTriggersValidated.ToString(CultureInfo.InvariantCulture));
            EditorGUILayout.LabelField("VFX Anchors", lastReport.VfxAnchorsValidated.ToString(CultureInfo.InvariantCulture));
            EditorGUILayout.LabelField("Lights", lastReport.LightsValidated.ToString(CultureInfo.InvariantCulture));
            EditorGUILayout.LabelField("Decals", lastReport.DecalsValidated.ToString(CultureInfo.InvariantCulture));
            EditorGUILayout.LabelField("Dry Run", lastReport.DryRun ? "true" : "false");
            for (int i = 0; i < lastReport.Violations.Count; i++)
                EditorGUILayout.LabelField(lastReport.Violations[i]);
            EditorGUILayout.EndScrollView();
        }

        private FactorySettings BuildSettings(bool dryRunOverride)
        {
            return new FactorySettings
            {
                MeshDirectory = meshDirectory,
                MaterialDirectory = materialDirectory,
                CollisionProxyDirectory = collisionProxyDirectory,
                MetadataDirectory = metadataDirectory,
                OutputDirectory = outputDirectory,
                DryRun = dryRunOverride,
                MaxGroupsPerRun = Mathf.Max(1, maxGroupsPerRun)
            };
        }

        public static FactoryReport Run(FactorySettings settings)
        {
            settings = settings.Sanitize();
            Stopwatch stopwatch = Stopwatch.StartNew();
            FactoryReport report = new FactoryReport
            {
                AgentId = AgentId,
                MeshDirectory = settings.MeshDirectory,
                MaterialDirectory = settings.MaterialDirectory,
                CollisionProxyDirectory = settings.CollisionProxyDirectory,
                MetadataDirectory = settings.MetadataDirectory,
                OutputDirectory = settings.OutputDirectory,
                DryRun = settings.DryRun
            };

            try
            {
                LoadMaterials(settings.MaterialDirectory);
                Dictionary<string, HazardMeshGroup> groups = DiscoverMeshGroups(settings, report);
                report.GroupsDiscovered = groups.Count;

                if (!settings.DryRun)
                    EnsureAssetFolder(settings.OutputDirectory);

                int processed = 0;
                foreach (KeyValuePair<string, HazardMeshGroup> pair in groups)
                {
                    if (processed >= settings.MaxGroupsPerRun)
                        break;

                    processed++;
                    ProcessGroup(pair.Value, settings, report);
                }
            }
            catch (Exception exception)
            {
                report.Violations.Add("FATAL: Factory exception: " + exception.GetType().Name + " " + exception.Message);
                Debug.LogException(exception);
            }
            finally
            {
                stopwatch.Stop();
                report.ExecutionMicroseconds = stopwatch.Elapsed.TotalMilliseconds * 1000.0;
                ClearScratch();
                WriteReport(report);
            }

            Debug.Log("[HazardPrefabFactory1736] Completed. Groups=" + report.GroupsDiscovered +
                      " Assembled=" + report.PrefabsAssembled +
                      " Failed=" + report.PrefabsFailed +
                      " us=" + report.ExecutionMicroseconds.ToString("F1", CultureInfo.InvariantCulture));
            return report;
        }

        private static Dictionary<string, HazardMeshGroup> DiscoverMeshGroups(FactorySettings settings, FactoryReport report)
        {
            Dictionary<string, HazardMeshGroup> groups = new Dictionary<string, HazardMeshGroup>(512, StringComparer.Ordinal);
            string[] searchFolders = ResolveSearchFolders(settings.MeshDirectory, DefaultMeshDirectory, "Assets/_Project/Art/Meshes/WorldProceduralGeology");
            if (searchFolders.Length == 0)
            {
                report.Violations.Add("FATAL: no valid mesh folders. Requested " + settings.MeshDirectory + ".");
                return groups;
            }

            string[] guids = AssetDatabase.FindAssets("t:Mesh", searchFolders);
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if (mesh == null)
                    continue;

                if (!TryExtractHazardLod(path, out string baseName, out int lodIndex))
                    continue;

                if (!groups.TryGetValue(baseName, out HazardMeshGroup group))
                {
                    group = new HazardMeshGroup(baseName);
                    groups.Add(baseName, group);
                }

                if (group.Lods[lodIndex] != null)
                {
                    report.Violations.Add("Duplicate hazard LOD" + lodIndex.ToString(CultureInfo.InvariantCulture) + " mesh for " + baseName + ": " + path);
                    continue;
                }

                group.Lods[lodIndex] = mesh;
                group.LodPaths[lodIndex] = path;
            }

            return groups;
        }

        private static bool TryExtractHazardLod(string assetPath, out string baseName, out int lodIndex)
        {
            baseName = string.Empty;
            lodIndex = -1;
            string fileName = Path.GetFileNameWithoutExtension(assetPath);
            if (string.IsNullOrWhiteSpace(fileName))
                return false;

            int lodMarker = fileName.LastIndexOf("_LOD", StringComparison.OrdinalIgnoreCase);
            if (lodMarker < 0 || lodMarker + 4 >= fileName.Length)
                return false;

            char lodChar = fileName[lodMarker + 4];
            if (lodChar < '0' || lodChar > '2')
                return false;

            string candidate = fileName.Substring(0, lodMarker);
            if (candidate.StartsWith("GEN_", StringComparison.OrdinalIgnoreCase))
                candidate = candidate.Substring(4);
            if (candidate.StartsWith("PFB_", StringComparison.OrdinalIgnoreCase))
                candidate = candidate.Substring(4);

            if (!IsHazardCandidate(candidate))
                return false;

            baseName = candidate;
            lodIndex = lodChar - '0';
            return true;
        }

        private static bool IsHazardCandidate(string baseName)
        {
            string normalized = NormalizeSearch(baseName);
            return normalized.IndexOf("hazard", StringComparison.Ordinal) >= 0 ||
                   normalized.IndexOf("toxic", StringComparison.Ordinal) >= 0 ||
                   normalized.IndexOf("coral", StringComparison.Ordinal) >= 0 ||
                   normalized.IndexOf("geyser", StringComparison.Ordinal) >= 0 ||
                   normalized.IndexOf("vent", StringComparison.Ordinal) >= 0 ||
                   normalized.IndexOf("flare", StringComparison.Ordinal) >= 0 ||
                   normalized.IndexOf("fire", StringComparison.Ordinal) >= 0 ||
                   normalized.IndexOf("smoker", StringComparison.Ordinal) >= 0 ||
                   normalized.IndexOf("sulfur", StringComparison.Ordinal) >= 0 ||
                   normalized.IndexOf("brine", StringComparison.Ordinal) >= 0;
        }

        private static void ProcessGroup(HazardMeshGroup group, FactorySettings settings, FactoryReport report)
        {
            if (!group.HasRequiredLods)
            {
                report.PrefabsFailed++;
                report.Violations.Add("FATAL: " + group.BaseName + " missing required LOD0/LOD1/LOD2 meshes.");
                return;
            }

            HazardProfile profile = ResolveHazardProfile(group.BaseName, settings.MetadataDirectory, report);
            Material surfaceMaterial = ResolveSurfaceMaterial(group.BaseName, profile);
            if (surfaceMaterial == null)
            {
                report.PrefabsFailed++;
                report.Violations.Add("FATAL: " + group.BaseName + " no valid shared hazard material. material could not be resolved.");
                return;
            }

            string materialFailure = "material validation failed.";
            if (!ValidateSharedMaterial(surfaceMaterial, out materialFailure))
            {
                report.PrefabsFailed++;
                report.Violations.Add("FATAL: " + group.BaseName + " no valid shared hazard material. " + materialFailure);
                return;
            }

            Material decalMaterial = ResolveDecalMaterial(profile);
            if (decalMaterial == null)
            {
                report.PrefabsFailed++;
                report.Violations.Add("FATAL: " + group.BaseName + " no valid shared decal material. decal material could not be resolved.");
                return;
            }

            string decalFailure = "decal material validation failed.";
            if (!ValidateDecalMaterial(decalMaterial, out decalFailure))
            {
                report.PrefabsFailed++;
                report.Violations.Add("FATAL: " + group.BaseName + " no valid shared decal material. " + decalFailure);
                return;
            }

            string prefabPath = settings.OutputDirectory + "/PFB_Hazard_" + SanitizeFileName(group.BaseName) + ".prefab";
            GameObject root = null;
            try
            {
                root = new GameObject("PFB_Hazard_" + SanitizeFileName(group.BaseName));
                if (!TryAssemblePrefabRoot(root, group, profile, surfaceMaterial, decalMaterial, settings, report, out Bounds combinedBounds))
                {
                    report.PrefabsFailed++;
                    return;
                }

                if (!ValidatePrefabInstance(root, out string instanceFailure))
                {
                    report.PrefabsFailed++;
                    report.Violations.Add("Hazard Assembly Violation Detected! " + group.BaseName + ": " + instanceFailure);
                    return;
                }

                AccumulateValidationCounters(root, report);
                if (settings.DryRun)
                {
                    report.PrefabsAssembled++;
                    report.BoundsVolumeCubicMeters += ComputeBoundsVolume(combinedBounds);
                    return;
                }

                GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath, out bool success);
                if (!success || savedPrefab == null)
                {
                    report.PrefabsFailed++;
                    report.Violations.Add("Hazard Assembly Violation Detected! SaveAsPrefabAsset failed for " + prefabPath + ".");
                    DeletePrefabAsset(prefabPath);
                    return;
                }

                if (!ValidateSavedPrefab(prefabPath, out string savedFailure))
                {
                    report.PrefabsFailed++;
                    report.Violations.Add("Hazard Assembly Violation Detected! " + prefabPath + ": " + savedFailure);
                    DeletePrefabAsset(prefabPath);
                    return;
                }

                report.PrefabsAssembled++;
                report.BoundsVolumeCubicMeters += ComputeBoundsVolume(combinedBounds);
            }
            finally
            {
                if (root != null)
                    Object.DestroyImmediate(root);
            }
        }

        private static bool TryAssemblePrefabRoot(
            GameObject root,
            HazardMeshGroup group,
            HazardProfile profile,
            Material surfaceMaterial,
            Material decalMaterial,
            FactorySettings settings,
            FactoryReport report,
            out Bounds combinedBounds)
        {
            combinedBounds = ComputeCombinedBounds(group);
            Renderer lod0 = CreateLodChild(root.transform, "LOD0", group.Lods[0], surfaceMaterial, false);
            Renderer lod1 = CreateLodChild(root.transform, "LOD1", group.Lods[1], surfaceMaterial, true);
            Renderer lod2 = CreateLodChild(root.transform, "LOD2", group.Lods[2], surfaceMaterial, true);
            if (lod0 == null || lod1 == null || lod2 == null)
            {
                report.Violations.Add("FATAL: " + group.BaseName + " renderer construction failed.");
                return false;
            }

            LODGroup lodGroup = root.AddComponent<LODGroup>();
            lodGroup.fadeMode = LODFadeMode.CrossFade;
            lodGroup.animateCrossFading = true;
            lodGroup.SetLODs(CreateLods(combinedBounds, lod0, lod1, lod2));
            lodGroup.localReferencePoint = combinedBounds.center;
            lodGroup.size = Mathf.Max(MinimumLodGroupSize, Mathf.Max(combinedBounds.size.x, Mathf.Max(combinedBounds.size.y, combinedBounds.size.z)));
            lodGroup.RecalculateBounds();

            HazardAnchorDefinition[] anchors = ResolveAnchors(profile, combinedBounds);
            if (!TryAddDamageTrigger(root.transform, group.BaseName, profile, anchors, combinedBounds, report, out DamageRouter router, out float triggerRadius))
                return false;

            VfxAnchorBinding[] bindings = AddVfxAnchors(root.transform, profile, anchors);
            if (bindings == null || bindings.Length == 0)
            {
                report.Violations.Add("FATAL: " + group.BaseName + " created no VFX_Anchor transforms.");
                return false;
            }

            if (!TryAddPrimitiveCollisionProxy(root.transform, group.BaseName, combinedBounds, settings.CollisionProxyDirectory, report))
                return false;

            DecalProjector decalProjector = AddDecalProjector(root.transform, combinedBounds, decalMaterial, profile);
            Light practicalLight = AddPracticalLight(root.transform, combinedBounds, profile);

            HazardMetadata metadata = root.AddComponent<HazardMetadata>();
            uint hazardHash = ComputeStableHash32(group.BaseName);
            metadata.ConfigureForEditor(
                hazardHash,
                profile.HazardType,
                profile.EffectId,
                profile.EffectHash,
                router,
                bindings,
                practicalLight != null ? new[] { practicalLight } : Array.Empty<Light>(),
                decalProjector != null ? new[] { decalProjector } : Array.Empty<DecalProjector>(),
                triggerRadius,
                profile.CullDistanceMeters);

            if (practicalLight != null)
            {
                LightCullingProxy cullingProxy = practicalLight.gameObject.AddComponent<LightCullingProxy>();
                cullingProxy.ConfigureForEditor(practicalLight, decalProjector, profile.CullDistanceMeters, Mathf.Max(2f, profile.CullDistanceMeters * 0.12f), 0.04f, false);

                ThermalVentRuntime runtime = root.AddComponent<ThermalVentRuntime>();
                runtime.ConfigureForEditor(metadata, practicalLight, decalProjector, cullingProxy, profile.LightIntensity, profile.LightRange, 0.75f, profile.PulseFrequencyHz, profile.PulseAmplitude);
            }

            return true;
        }

        private static Renderer CreateLodChild(Transform parent, string name, Mesh mesh, Material material, bool farLod)
        {
            if (mesh == null || material == null)
                return null;

            GameObject child = new GameObject(name);
            child.transform.SetParent(parent, false);
            child.transform.localPosition = Vector3.zero;
            child.transform.localRotation = Quaternion.identity;
            child.transform.localScale = Vector3.one;

            MeshFilter meshFilter = child.AddComponent<MeshFilter>();
            meshFilter.sharedMesh = mesh;

            MeshRenderer renderer = child.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = BuildSharedMaterialSlots(mesh, material);
            renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return renderer;
        }

        private static Material[] BuildSharedMaterialSlots(Mesh mesh, Material material)
        {
            int subMeshCount = Mathf.Max(1, mesh != null ? mesh.subMeshCount : 1);
            Material[] materials = new Material[subMeshCount];
            for (int i = 0; i < materials.Length; i++)
                materials[i] = material;
            return materials;
        }

        private static LOD[] CreateLods(Bounds bounds, Renderer lod0, Renderer lod1, Renderer lod2)
        {
            float volume = Mathf.Max(0.0001f, ComputeBoundsVolume(bounds));
            float inverseSize = Mathf.Clamp(1f / Mathf.Sqrt(volume), 0.25f, 1.75f);
            float lod0Height = Mathf.Clamp(0.48f * inverseSize, 0.22f, 0.62f);
            float lod1Height = Mathf.Clamp(0.20f * inverseSize, 0.07f, Mathf.Min(0.28f, lod0Height - 0.05f));
            float lod2Height = Mathf.Clamp(0.045f * inverseSize, 0.016f, Mathf.Min(0.075f, lod1Height - 0.012f));
            return new[]
            {
                new LOD(lod0Height, new[] { lod0 }),
                new LOD(lod1Height, new[] { lod1 }),
                new LOD(lod2Height, new[] { lod2 })
            };
        }

        private static bool TryAddDamageTrigger(
            Transform root,
            string baseName,
            HazardProfile profile,
            HazardAnchorDefinition[] anchors,
            Bounds combinedBounds,
            FactoryReport report,
            out DamageRouter router,
            out float triggerRadius)
        {
            router = null;
            triggerRadius = 0f;
            int layer = LayerMask.NameToLayer(HazardTriggerLayerName);
            if (layer < 0)
            {
                report.Violations.Add("FATAL: Layer " + HazardTriggerLayerName + " does not exist. Damage trigger refused for " + baseName + ".");
                return false;
            }

            Vector3 center = anchors != null && anchors.Length > 0 ? anchors[0].LocalPosition : combinedBounds.center;
            float radiusBase = profile.TriggerRadiusMeters > 0f ? profile.TriggerRadiusMeters : combinedBounds.extents.magnitude;
            triggerRadius = Mathf.Clamp(radiusBase + Mathf.Max(0f, profile.TriggerPaddingMeters), 0.2f, 64f);

            GameObject trigger = new GameObject("TRIG_DamageZone");
            trigger.layer = layer;
            trigger.transform.SetParent(root, false);
            trigger.transform.localPosition = Vector3.zero;
            trigger.transform.localRotation = Quaternion.identity;
            trigger.transform.localScale = Vector3.one;

            SphereCollider collider = trigger.AddComponent<SphereCollider>();
            collider.isTrigger = true;
            collider.center = center;
            collider.radius = triggerRadius;

            router = trigger.AddComponent<DamageRouter>();
            router.ConfigureForEditor(
                ComputeStableHash32(baseName),
                profile.DamageType,
                profile.StatusBits,
                profile.BaseDamage,
                triggerRadius,
                profile.StatusDurationSeconds,
                center,
                profile.EffectHash);
            return true;
        }

        private static VfxAnchorBinding[] AddVfxAnchors(Transform root, HazardProfile profile, HazardAnchorDefinition[] anchors)
        {
            if (anchors == null || anchors.Length == 0)
                return Array.Empty<VfxAnchorBinding>();

            VfxAnchorBinding[] bindings = new VfxAnchorBinding[anchors.Length];
            for (int i = 0; i < anchors.Length; i++)
            {
                HazardAnchorDefinition anchor = anchors[i];
                GameObject anchorObject = new GameObject(i == 0 ? "VFX_Anchor" : "VFX_Anchor_" + i.ToString("00", CultureInfo.InvariantCulture));
                anchorObject.transform.SetParent(root, false);
                anchorObject.transform.localPosition = anchor.LocalPosition;
                anchorObject.transform.localRotation = ResolveAnchorRotation(anchor.LocalForward);
                anchorObject.transform.localScale = Vector3.one;

                string effectId = string.IsNullOrWhiteSpace(anchor.EffectId) ? profile.EffectId : anchor.EffectId;
                uint effectHash = ComputeStableHash32(effectId);
                bindings[i] = new VfxAnchorBinding
                {
                    Anchor = anchorObject.transform,
                    LocalPosition = anchor.LocalPosition,
                    LocalForward = ResolveSafeForward(anchor.LocalForward),
                    EffectHash = effectHash,
                    EffectId = effectId
                };
            }

            return bindings;
        }

        private static bool TryAddPrimitiveCollisionProxy(
            Transform root,
            string baseName,
            Bounds combinedBounds,
            string collisionProxyDirectory,
            FactoryReport report)
        {
            int layer = LayerMask.NameToLayer(WorldStaticLayerName);
            if (layer < 0)
            {
                report.Violations.Add("FATAL: Layer " + WorldStaticLayerName + " does not exist. Collision proxy refused for " + baseName + ".");
                return false;
            }

            GameObject proxy = TryInstantiateCollisionProxy(root, baseName, collisionProxyDirectory, layer);
            if (proxy != null)
            {
                if (!ValidatePrimitiveCollisionProxy(proxy, out string failure))
                {
                    report.Violations.Add("FATAL: " + baseName + " collision proxy invalid: " + failure);
                    return false;
                }

                report.CollisionProxyAssetsUsed++;
                return true;
            }

            GameObject colliderObject = new GameObject("COL_PrimitiveBounds");
            colliderObject.layer = layer;
            colliderObject.transform.SetParent(root, false);
            colliderObject.transform.localPosition = Vector3.zero;
            colliderObject.transform.localRotation = Quaternion.identity;
            colliderObject.transform.localScale = Vector3.one;

            BoxCollider collider = colliderObject.AddComponent<BoxCollider>();
            collider.isTrigger = false;
            collider.center = combinedBounds.center;
            collider.size = SanitizeBoundsSize(combinedBounds.size);
            report.PrimitiveCollisionFallbacks++;
            return true;
        }

        private static GameObject TryInstantiateCollisionProxy(Transform root, string baseName, string collisionProxyDirectory, int layer)
        {
            if (string.IsNullOrWhiteSpace(collisionProxyDirectory) || !AssetDatabase.IsValidFolder(collisionProxyDirectory))
                return null;

            string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { collisionProxyDirectory });
            string normalizedBase = NormalizeSearch(baseName);
            GameObject best = null;
            int bestScore = 0;
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                string normalized = NormalizeSearch(Path.GetFileNameWithoutExtension(path));
                if (normalized.IndexOf("col", StringComparison.Ordinal) < 0 &&
                    normalized.IndexOf("collision", StringComparison.Ordinal) < 0 &&
                    normalized.IndexOf("physics", StringComparison.Ordinal) < 0)
                {
                    continue;
                }

                int score = normalized.IndexOf(normalizedBase, StringComparison.Ordinal) >= 0 ? normalizedBase.Length : 0;
                if (score <= bestScore)
                    continue;

                bestScore = score;
                best = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            }

            if (best == null)
                return null;

            GameObject instance = PrefabUtility.InstantiatePrefab(best) as GameObject;
            if (instance == null)
                return null;

            instance.name = "COL_PrimitiveProxy";
            instance.transform.SetParent(root, false);
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale = Vector3.one;
            ApplyLayerRecursively(instance.transform, layer);
            StripRenderers(instance);
            StripMonoBehaviours(instance);
            StripRigidbodies(instance);
            return instance;
        }

        private static DecalProjector AddDecalProjector(Transform root, Bounds combinedBounds, Material decalMaterial, HazardProfile profile)
        {
            GameObject decalObject = new GameObject("DECAL_HazardResidue");
            decalObject.transform.SetParent(root, false);
            decalObject.transform.localPosition = new Vector3(combinedBounds.center.x, combinedBounds.min.y + 0.08f, combinedBounds.center.z);
            decalObject.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            decalObject.transform.localScale = Vector3.one;

            DecalProjector projector = decalObject.AddComponent<DecalProjector>();
            projector.material = decalMaterial;
            float width = Mathf.Clamp(Mathf.Max(combinedBounds.size.x, combinedBounds.size.z) * 1.25f, 1f, 24f);
            projector.size = new Vector3(width, width, Mathf.Clamp(combinedBounds.size.y + 1f, 0.75f, 12f));
            projector.pivot = new Vector3(0f, 0f, projector.size.z * 0.5f);
            projector.fadeFactor = Mathf.Clamp01(profile.DecalFade);
            return projector;
        }

        private static Light AddPracticalLight(Transform root, Bounds combinedBounds, HazardProfile profile)
        {
            GameObject lightObject = new GameObject("LIGHT_Hazard_Practical");
            lightObject.transform.SetParent(root, false);
            lightObject.transform.localPosition = ResolveLightPosition(combinedBounds);
            lightObject.transform.localRotation = Quaternion.identity;
            lightObject.transform.localScale = Vector3.one;

            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = profile.LightColor;
            light.intensity = Mathf.Max(0f, profile.LightIntensity);
            light.range = Mathf.Max(0.5f, profile.LightRange);
            light.shadows = LightShadows.None;
            light.renderMode = LightRenderMode.Auto;
            return light;
        }

        private static HazardProfile ResolveHazardProfile(string baseName, string metadataDirectory, FactoryReport report)
        {
            HazardProfile profile = HazardProfile.FromName(baseName);
            TextAsset metadata = ResolveMetadataAsset(baseName, metadataDirectory);
            if (metadata == null)
                return profile;

            try
            {
                HazardMetadataFile file = JsonUtility.FromJson<HazardMetadataFile>(metadata.text);
                if (file == null)
                    return profile;

                profile.Apply(file);
                return profile;
            }
            catch (Exception exception)
            {
                report.Violations.Add("WARN: Metadata parse failed for " + baseName + ": " + exception.GetType().Name + " " + exception.Message);
                return profile;
            }
        }

        private static TextAsset ResolveMetadataAsset(string baseName, string metadataDirectory)
        {
            if (string.IsNullOrWhiteSpace(metadataDirectory) || !AssetDatabase.IsValidFolder(metadataDirectory))
                return null;

            string[] guids = AssetDatabase.FindAssets("t:TextAsset", new[] { metadataDirectory });
            string normalizedBase = NormalizeSearch(baseName);
            TextAsset best = null;
            int bestScore = 0;
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                string normalizedPath = NormalizeSearch(Path.GetFileNameWithoutExtension(path));
                int score = normalizedPath.IndexOf(normalizedBase, StringComparison.Ordinal) >= 0 ? normalizedBase.Length : 0;
                if (score <= bestScore)
                    continue;

                best = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
                bestScore = score;
            }

            return best;
        }

        private static HazardAnchorDefinition[] ResolveAnchors(HazardProfile profile, Bounds bounds)
        {
            if (profile.Anchors != null && profile.Anchors.Length > 0)
            {
                HazardAnchorDefinition[] resolved = new HazardAnchorDefinition[profile.Anchors.Length];
                for (int i = 0; i < resolved.Length; i++)
                {
                    HazardAnchorFile source = profile.Anchors[i];
                    resolved[i] = new HazardAnchorDefinition
                    {
                        LocalPosition = IsFinite(source.localPosition) ? source.localPosition : ResolveDefaultAnchorPosition(bounds),
                        LocalForward = ResolveSafeForward(source.forward),
                        EffectId = string.IsNullOrWhiteSpace(source.effectId) ? profile.EffectId : source.effectId
                    };
                }

                return resolved;
            }

            return new[]
            {
                new HazardAnchorDefinition
                {
                    LocalPosition = ResolveDefaultAnchorPosition(bounds),
                    LocalForward = Vector3.up,
                    EffectId = profile.EffectId
                }
            };
        }

        private static Material ResolveSurfaceMaterial(string baseName, HazardProfile profile)
        {
            Material best = null;
            int bestScore = int.MinValue;
            string normalizedBase = NormalizeSearch(baseName);
            string normalizedProfileMaterial = NormalizeSearch(profile.SurfaceMaterialName);
            for (int i = 0; i < s_MaterialScratch.Count; i++)
            {
                Material material = s_MaterialScratch[i];
                if (material == null || !ValidateSharedMaterial(material, out _))
                    continue;

                string normalizedMaterial = NormalizeSearch(material.name);
                int score = ScoreSurfaceMaterial(normalizedBase, normalizedMaterial, normalizedProfileMaterial, profile);
                if (score > bestScore)
                {
                    bestScore = score;
                    best = material;
                }
            }

            if (best != null)
                best.enableInstancing = true;
            return best;
        }

        private static int ScoreSurfaceMaterial(string normalizedBase, string normalizedMaterial, string normalizedProfileMaterial, HazardProfile profile)
        {
            int score = 0;
            if (!string.IsNullOrEmpty(normalizedProfileMaterial) && normalizedMaterial.IndexOf(normalizedProfileMaterial, StringComparison.Ordinal) >= 0)
                score += 120;
            if (normalizedBase.IndexOf("coral", StringComparison.Ordinal) >= 0 && normalizedMaterial.IndexOf("coral", StringComparison.Ordinal) >= 0)
                score += 70;
            if (normalizedMaterial.IndexOf("hazard", StringComparison.Ordinal) >= 0)
                score += profile.HazardType == HazardType.Toxicity ? 60 : 35;
            if ((normalizedBase.IndexOf("vent", StringComparison.Ordinal) >= 0 ||
                 normalizedBase.IndexOf("geyser", StringComparison.Ordinal) >= 0 ||
                 normalizedBase.IndexOf("smoker", StringComparison.Ordinal) >= 0 ||
                 normalizedBase.IndexOf("sulfur", StringComparison.Ordinal) >= 0) &&
                normalizedMaterial.IndexOf("rock", StringComparison.Ordinal) >= 0)
            {
                score += 40;
            }
            if (normalizedBase.IndexOf("flare", StringComparison.Ordinal) >= 0 && normalizedMaterial.IndexOf("emiss", StringComparison.Ordinal) >= 0)
                score += 50;
            if (normalizedBase.IndexOf("fire", StringComparison.Ordinal) >= 0 && normalizedMaterial.IndexOf("emiss", StringComparison.Ordinal) >= 0)
                score += 50;
            if (normalizedMaterial.IndexOf("placeholder", StringComparison.Ordinal) >= 0)
                score -= 30;
            if (score == 0 && (normalizedMaterial.IndexOf("worldprocedural", StringComparison.Ordinal) >= 0 || normalizedMaterial.IndexOf("family", StringComparison.Ordinal) >= 0))
                score += 5;
            return score;
        }

        private static Material ResolveDecalMaterial(HazardProfile profile)
        {
            Material best = null;
            int bestScore = int.MinValue;
            string requested = NormalizeSearch(profile.DecalMaterialName);
            for (int i = 0; i < s_MaterialScratch.Count; i++)
            {
                Material material = s_MaterialScratch[i];
                if (material == null || !ValidateDecalMaterial(material, out _))
                    continue;

                string normalized = NormalizeSearch(material.name);
                int score = int.MinValue;
                if (!string.IsNullOrEmpty(requested) && normalized.IndexOf(requested, StringComparison.Ordinal) >= 0)
                    score = 100;
                else if (normalized.IndexOf("decal", StringComparison.Ordinal) >= 0)
                    score = 50;
                else if (normalized.IndexOf("fluid", StringComparison.Ordinal) >= 0 && profile.HazardType == HazardType.Toxicity)
                    score = 20;

                if (score > bestScore)
                {
                    bestScore = score;
                    best = material;
                }
            }

            return best;
        }

        private static void LoadMaterials(string materialDirectory)
        {
            s_MaterialScratch.Clear();
            string[] searchFolders = ResolveSearchFolders(materialDirectory, DefaultMaterialDirectory, "Assets/_Project/Art/Materials/VFX");
            for (int folderIndex = 0; folderIndex < searchFolders.Length; folderIndex++)
            {
                string[] guids = AssetDatabase.FindAssets("t:Material", new[] { searchFolders[folderIndex] });
                for (int i = 0; i < guids.Length; i++)
                {
                    Material material = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guids[i]));
                    if (material != null && !s_MaterialScratch.Contains(material))
                        s_MaterialScratch.Add(material);
                }
            }
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
                failure = material.name + " is not an asset-backed shared material.";
                return false;
            }

            if (material.shader == null)
            {
                failure = material.name + " has no shader.";
                return false;
            }

            if (!ShaderHasUnityPerMaterialCbuffer(material.shader))
            {
                failure = material.name + " shader lacks CBUFFER_START(UnityPerMaterial) or accepted URP/ShaderGraph backing.";
                return false;
            }

            return true;
        }

        private static bool ValidateDecalMaterial(Material material, out string failure)
        {
            if (!ValidateSharedMaterial(material, out failure))
                return false;

            if (!IsDecalShader(material.shader))
            {
                failure = material.name + " shader is not a decal shader.";
                return false;
            }

            return true;
        }

        private static bool ShaderHasUnityPerMaterialCbuffer(Shader shader)
        {
            if (shader == null)
                return false;

            if (s_ShaderCbufferCache.TryGetValue(shader, out bool cached))
                return cached;

            bool result = shader.name.IndexOf("Universal Render Pipeline", StringComparison.OrdinalIgnoreCase) >= 0;
            string shaderPath = AssetDatabase.GetAssetPath(shader);
            if (!result && !string.IsNullOrEmpty(shaderPath))
            {
                string extension = Path.GetExtension(shaderPath);
                if (string.Equals(extension, ".shadergraph", StringComparison.OrdinalIgnoreCase))
                    result = true;
                else
                {
                    string fullPath = ResolveFullPath(shaderPath);
                    if (File.Exists(fullPath))
                    {
                        string source = File.ReadAllText(fullPath);
                        result = source.IndexOf("CBUFFER_START(UnityPerMaterial)", StringComparison.Ordinal) >= 0;
                    }
                }
            }

            if (s_ShaderCbufferCache.ContainsKey(shader) || s_ShaderCbufferCache.Count < 64)
                s_ShaderCbufferCache[shader] = result;
            return result;
        }

        private static bool IsDecalShader(Shader shader)
        {
            if (shader == null)
                return false;

            if (shader.name.IndexOf("Decal", StringComparison.OrdinalIgnoreCase) >= 0)
                return true;

            string shaderPath = AssetDatabase.GetAssetPath(shader);
            return !string.IsNullOrEmpty(shaderPath) &&
                   shaderPath.IndexOf("decal", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool ValidatePrefabInstance(GameObject root, out string failure)
        {
            failure = string.Empty;
            if (root == null)
            {
                failure = "root is null.";
                return false;
            }

            if (!DamageRouter.IsPacketLayoutValid || !DamageRouter.IsCanonicalDamagePacketLayoutValid)
            {
                failure = "Damage packet layout invalid. RouterSize="
                    + DamageRouter.ResolvedPacketSizeBytes.ToString(CultureInfo.InvariantCulture)
                    + " CanonicalSize="
                    + DamageRouter.ResolvedCanonicalDamagePacketSizeBytes.ToString(CultureInfo.InvariantCulture)
                    + ".";
                return false;
            }

            if (!HazardMetadata.IsVfxAnchorRuntimeDataLayoutValid)
            {
                failure = "VFX anchor runtime layout invalid. Size="
                    + HazardMetadata.ResolvedVfxAnchorRuntimeDataSizeBytes.ToString(CultureInfo.InvariantCulture)
                    + ".";
                return false;
            }

            if (root.GetComponent<MeshFilter>() != null || root.GetComponent<MeshRenderer>() != null)
            {
                failure = "root carries renderer components.";
                return false;
            }

            LODGroup lodGroup = root.GetComponent<LODGroup>();
            if (lodGroup == null || lodGroup.GetLODs().Length != 3)
            {
                failure = "LODGroup missing or does not contain exactly 3 levels.";
                return false;
            }

            root.GetComponentsInChildren(true, s_DamageRouterScratch);
            if (s_DamageRouterScratch.Count != 1)
            {
                failure = "expected exactly one DamageRouter, found " + s_DamageRouterScratch.Count.ToString(CultureInfo.InvariantCulture) + ".";
                s_DamageRouterScratch.Clear();
                return false;
            }

            DamageRouter router = s_DamageRouterScratch[0];
            s_DamageRouterScratch.Clear();
            if (router == null || !router.TryReadPacket(out DamageRouterPacket packet) || packet.BaseDamage <= 0f)
            {
                failure = "DamageRouter packet is invalid.";
                return false;
            }

            SphereCollider routerCollider = router.GetComponent<SphereCollider>();
            int hazardLayer = LayerMask.NameToLayer(HazardTriggerLayerName);
            int worldStaticLayer = LayerMask.NameToLayer(WorldStaticLayerName);
            if (hazardLayer < 0 || worldStaticLayer < 0)
            {
                failure = "required hazard physics layers are missing.";
                return false;
            }

            if (routerCollider == null || !routerCollider.isTrigger || router.gameObject.layer != hazardLayer)
            {
                failure = "DamageRouter SphereCollider trigger is missing, non-trigger, or not on " + HazardTriggerLayerName + ".";
                return false;
            }

            root.GetComponentsInChildren(true, s_MeshColliderScratch);
            if (s_MeshColliderScratch.Count > 0)
            {
                failure = "MeshCollider exists in hazard prefab.";
                s_MeshColliderScratch.Clear();
                return false;
            }
            s_MeshColliderScratch.Clear();

            root.GetComponentsInChildren(true, s_ColliderScratch);
            for (int i = 0; i < s_ColliderScratch.Count; i++)
            {
                Collider collider = s_ColliderScratch[i];
                if (collider == null)
                    continue;

                bool primitive = collider is BoxCollider || collider is SphereCollider || collider is CapsuleCollider;
                if (!primitive)
                {
                    failure = "non-primitive collider " + collider.GetType().Name + " found on " + collider.name + ".";
                    s_ColliderScratch.Clear();
                    return false;
                }

                if (collider == routerCollider)
                    continue;

                if (collider.isTrigger)
                {
                    failure = "non-damage collider " + collider.name + " is marked as trigger.";
                    s_ColliderScratch.Clear();
                    return false;
                }

                if (collider.gameObject.layer != worldStaticLayer)
                {
                    failure = "collision proxy collider " + collider.name + " is not on " + WorldStaticLayerName + ".";
                    s_ColliderScratch.Clear();
                    return false;
                }
            }
            s_ColliderScratch.Clear();

            root.GetComponentsInChildren(true, s_RigidbodyScratch);
            if (s_RigidbodyScratch.Count > 0)
            {
                failure = "Rigidbody exists in static hazard prefab.";
                s_RigidbodyScratch.Clear();
                return false;
            }
            s_RigidbodyScratch.Clear();

            root.GetComponentsInChildren(true, s_ParticleScratch);
            if (s_ParticleScratch.Count > 0)
            {
                failure = "ParticleSystem exists in hazard hierarchy; pooled VFX_Anchor only.";
                s_ParticleScratch.Clear();
                return false;
            }
            s_ParticleScratch.Clear();

            root.GetComponentsInChildren(true, s_HazardSourceScratch);
            if (s_HazardSourceScratch.Count > 0)
            {
                failure = "HectonHazardSource exists in generated prefab; DamageRouter is the only trigger payload authority for this factory.";
                s_HazardSourceScratch.Clear();
                return false;
            }
            s_HazardSourceScratch.Clear();

            root.GetComponentsInChildren(true, s_EnvironmentalHazardScratch);
            if (s_EnvironmentalHazardScratch.Count > 0)
            {
                failure = "EnvironmentalHazard exists in generated prefab; DamageRouter is the only trigger payload authority for this factory.";
                s_EnvironmentalHazardScratch.Clear();
                return false;
            }
            s_EnvironmentalHazardScratch.Clear();

            root.GetComponentsInChildren(true, s_LightScratch);
            for (int i = 0; i < s_LightScratch.Count; i++)
            {
                Light light = s_LightScratch[i];
                if (light != null && light.shadows != LightShadows.None)
                {
                    failure = "Light " + light.name + " has shadows enabled.";
                    s_LightScratch.Clear();
                    return false;
                }
            }
            s_LightScratch.Clear();

            root.GetComponentsInChildren(true, s_RendererScratch);
            for (int i = 0; i < s_RendererScratch.Count; i++)
            {
                MeshRenderer renderer = s_RendererScratch[i];
                if (renderer != null && renderer.shadowCastingMode != ShadowCastingMode.Off)
                {
                    failure = "Renderer " + renderer.name + " has shadowCastingMode enabled.";
                    s_RendererScratch.Clear();
                    return false;
                }
            }
            s_RendererScratch.Clear();

            root.GetComponentsInChildren(true, s_DecalScratch);
            if (s_DecalScratch.Count == 0)
            {
                failure = "DecalProjector missing.";
                return false;
            }
            for (int i = 0; i < s_DecalScratch.Count; i++)
            {
                DecalProjector projector = s_DecalScratch[i];
                if (projector == null)
                    continue;

                if (!ValidateDecalMaterial(projector.material, out failure))
                {
                    failure = "DecalProjector " + projector.name + " has invalid material. " + failure;
                    s_DecalScratch.Clear();
                    return false;
                }
            }
            s_DecalScratch.Clear();

            root.GetComponentsInChildren(true, s_LightCullingScratch);
            if (s_LightCullingScratch.Count != 1)
            {
                failure = "expected exactly one LightCullingProxy, found " + s_LightCullingScratch.Count.ToString(CultureInfo.InvariantCulture) + ".";
                s_LightCullingScratch.Clear();
                return false;
            }

            LightCullingProxy cullingProxy = s_LightCullingScratch[0];
            if (cullingProxy == null ||
                !cullingProxy.HasValidFactoryConfiguration ||
                cullingProxy.TargetLight == null ||
                cullingProxy.TargetDecalProjector == null ||
                cullingProxy.ManagesPresentationScalars)
            {
                failure = "LightCullingProxy must own enable-state only and reference both light and decal.";
                s_LightCullingScratch.Clear();
                return false;
            }
            s_LightCullingScratch.Clear();

            root.GetComponentsInChildren(true, s_ThermalRuntimeScratch);
            if (s_ThermalRuntimeScratch.Count != 1)
            {
                failure = "expected exactly one ThermalVentRuntime, found " + s_ThermalRuntimeScratch.Count.ToString(CultureInfo.InvariantCulture) + ".";
                s_ThermalRuntimeScratch.Clear();
                return false;
            }

            ThermalVentRuntime thermalRuntime = s_ThermalRuntimeScratch[0];
            if (thermalRuntime == null ||
                !thermalRuntime.HasValidFactoryConfiguration ||
                thermalRuntime.KeyLight == null ||
                thermalRuntime.PrimaryDecal == null ||
                thermalRuntime.CullingProxy != cullingProxy)
            {
                failure = "ThermalVentRuntime must reference metadata, light, decal, and culling proxy.";
                s_ThermalRuntimeScratch.Clear();
                return false;
            }
            s_ThermalRuntimeScratch.Clear();

            root.GetComponentsInChildren(true, s_MetadataScratch);
            if (s_MetadataScratch.Count != 1 || s_MetadataScratch[0].AnchorCount <= 0)
            {
                failure = "HazardMetadata missing or has no VFX anchors.";
                s_MetadataScratch.Clear();
                return false;
            }
            HazardMetadata metadata = s_MetadataScratch[0];
            if (metadata == null || metadata.Router != router || thermalRuntime.Metadata != metadata)
            {
                failure = "HazardMetadata, DamageRouter, and ThermalVentRuntime references are not a single ownership graph.";
                s_MetadataScratch.Clear();
                return false;
            }
            s_MetadataScratch.Clear();

            if (!ValidateVfxAnchorNames(root, out failure))
                return false;

            if (!ValidateVfxAnchorBindings(metadata, out failure))
                return false;

            return ValidateRendererMaterials(root, out failure);
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

        private static bool ValidateVfxAnchorNames(GameObject root, out string failure)
        {
            failure = string.Empty;
            int anchorCount = 0;
            root.GetComponentsInChildren(true, s_TransformScratch);
            for (int i = 0; i < s_TransformScratch.Count; i++)
            {
                Transform child = s_TransformScratch[i];
                if (child != null && child.name.StartsWith("VFX_Anchor", StringComparison.Ordinal))
                    anchorCount++;
            }
            s_TransformScratch.Clear();

            if (anchorCount <= 0)
            {
                failure = "no VFX_Anchor transforms present.";
                return false;
            }

            return true;
        }

        private static bool ValidateVfxAnchorBindings(HazardMetadata metadata, out string failure)
        {
            failure = string.Empty;
            if (metadata == null || metadata.AnchorCount <= 0)
            {
                failure = "HazardMetadata has no VFX anchor bindings.";
                return false;
            }

            for (int i = 0; i < metadata.AnchorCount; i++)
            {
                if (!metadata.TryGetAnchorRuntimeData(i, out Transform anchor, out VfxAnchorRuntimeData runtimeData))
                {
                    failure = "VFX anchor binding " + i.ToString(CultureInfo.InvariantCulture) + " is invalid or has zero effect hash.";
                    return false;
                }

                if (anchor == null || !anchor.name.StartsWith("VFX_Anchor", StringComparison.Ordinal))
                {
                    failure = "VFX anchor binding " + i.ToString(CultureInfo.InvariantCulture) + " does not reference a VFX_Anchor transform.";
                    return false;
                }

                Vector3 localPosition = new Vector3(runtimeData.LocalPosition.x, runtimeData.LocalPosition.y, runtimeData.LocalPosition.z);
                Vector3 localForward = new Vector3(runtimeData.LocalForward.x, runtimeData.LocalForward.y, runtimeData.LocalForward.z);
                if (!IsFinite(localPosition) || !IsFinite(localForward) || localForward.sqrMagnitude <= 0.0001f || runtimeData.EffectHash == 0u)
                {
                    failure = "VFX anchor runtime data " + i.ToString(CultureInfo.InvariantCulture) + " is non-finite or missing effect hash.";
                    return false;
                }

                if (runtimeData.HazardHash != metadata.HazardHash || runtimeData.AnchorIndex != i)
                {
                    failure = "VFX anchor runtime data " + i.ToString(CultureInfo.InvariantCulture) + " has mismatched hazard hash or index.";
                    return false;
                }

                if (!IsApproximately(localPosition, anchor.localPosition, 0.001f))
                {
                    failure = "VFX anchor " + anchor.name + " metadata local position does not match transform local position.";
                    return false;
                }

                Vector3 transformForward = anchor.localRotation * Vector3.forward;
                if (!IsDirectionAligned(localForward, transformForward))
                {
                    failure = "VFX anchor " + anchor.name + " metadata forward does not match transform local rotation.";
                    return false;
                }

                if (!IsUnitScale(anchor.localScale))
                {
                    failure = "VFX anchor " + anchor.name + " local scale is not identity.";
                    return false;
                }
            }

            return true;
        }

        private static bool ValidateRendererMaterials(GameObject root, out string failure)
        {
            failure = string.Empty;
            root.GetComponentsInChildren(true, s_RendererScratch);
            for (int rendererIndex = 0; rendererIndex < s_RendererScratch.Count; rendererIndex++)
            {
                MeshRenderer renderer = s_RendererScratch[rendererIndex];
                if (renderer == null)
                    continue;

                Material[] materials = renderer.sharedMaterials;
                for (int materialIndex = 0; materialIndex < materials.Length; materialIndex++)
                {
                    Material material = materials[materialIndex];
                    if (!ValidateSharedMaterial(material, out failure))
                    {
                        failure = renderer.name + " material slot " + materialIndex.ToString(CultureInfo.InvariantCulture) + ": " + failure;
                        s_RendererScratch.Clear();
                        return false;
                    }
                }
            }
            s_RendererScratch.Clear();
            return true;
        }

        private static bool ValidatePrimitiveCollisionProxy(GameObject proxy, out string failure)
        {
            failure = string.Empty;
            proxy.GetComponentsInChildren(true, s_ColliderScratch);
            if (s_ColliderScratch.Count == 0)
            {
                failure = "proxy has no collider.";
                s_ColliderScratch.Clear();
                return false;
            }

            for (int i = 0; i < s_ColliderScratch.Count; i++)
            {
                Collider collider = s_ColliderScratch[i];
                if (collider == null)
                    continue;

                bool primitive = collider is BoxCollider || collider is SphereCollider || collider is CapsuleCollider;
                if (!primitive)
                {
                    failure = "proxy collider " + collider.GetType().Name + " is not primitive.";
                    s_ColliderScratch.Clear();
                    return false;
                }

                if (collider.isTrigger)
                {
                    failure = "proxy collider " + collider.name + " is marked as trigger.";
                    s_ColliderScratch.Clear();
                    return false;
                }
            }
            s_ColliderScratch.Clear();
            return true;
        }

        private static void AccumulateValidationCounters(GameObject root, FactoryReport report)
        {
            root.GetComponentsInChildren(true, s_DamageRouterScratch);
            report.DamageTriggersValidated += s_DamageRouterScratch.Count;
            s_DamageRouterScratch.Clear();

            root.GetComponentsInChildren(true, s_LightScratch);
            report.LightsValidated += s_LightScratch.Count;
            s_LightScratch.Clear();

            root.GetComponentsInChildren(true, s_DecalScratch);
            report.DecalsValidated += s_DecalScratch.Count;
            s_DecalScratch.Clear();

            root.GetComponentsInChildren(true, s_TransformScratch);
            for (int i = 0; i < s_TransformScratch.Count; i++)
            {
                Transform child = s_TransformScratch[i];
                if (child != null && child.name.StartsWith("VFX_Anchor", StringComparison.Ordinal))
                    report.VfxAnchorsValidated++;
            }
            s_TransformScratch.Clear();

            root.GetComponentsInChildren(true, s_RendererScratch);
            report.RenderersValidated += s_RendererScratch.Count;
            s_RendererScratch.Clear();
        }

        private static Bounds ComputeCombinedBounds(HazardMeshGroup group)
        {
            Bounds combined = new Bounds(Vector3.zero, Vector3.one * MinimumLodGroupSize);
            bool initialized = false;
            for (int i = 0; i < group.Lods.Length; i++)
            {
                Mesh mesh = group.Lods[i];
                if (mesh == null)
                    continue;

                if (!initialized)
                {
                    combined = mesh.bounds;
                    initialized = true;
                }
                else
                {
                    combined.Encapsulate(mesh.bounds);
                }
            }

            return initialized ? combined : new Bounds(Vector3.zero, Vector3.one * MinimumLodGroupSize);
        }

        private static Vector3 ResolveDefaultAnchorPosition(Bounds bounds)
        {
            return new Vector3(bounds.center.x, bounds.center.y + bounds.extents.y, bounds.center.z);
        }

        private static Vector3 ResolveLightPosition(Bounds bounds)
        {
            return new Vector3(bounds.center.x, bounds.center.y + Mathf.Max(0.5f, bounds.extents.y * 0.65f), bounds.center.z);
        }

        private static Quaternion ResolveAnchorRotation(Vector3 forward)
        {
            Vector3 safeForward = ResolveSafeForward(forward);
            Vector3 up = Mathf.Abs(Vector3.Dot(safeForward, Vector3.up)) > 0.98f ? Vector3.forward : Vector3.up;
            return Quaternion.LookRotation(safeForward, up);
        }

        private static Vector3 ResolveSafeForward(Vector3 value)
        {
            if (!IsFinite(value) || value.sqrMagnitude < 0.0001f)
                return Vector3.up;
            return value.normalized;
        }

        private static Vector3 SanitizeBoundsSize(Vector3 size)
        {
            return new Vector3(
                Mathf.Max(0.1f, IsFinite(size.x) ? size.x : 0.1f),
                Mathf.Max(0.1f, IsFinite(size.y) ? size.y : 0.1f),
                Mathf.Max(0.1f, IsFinite(size.z) ? size.z : 0.1f));
        }

        private static void StripRenderers(GameObject root)
        {
            root.GetComponentsInChildren(true, s_RendererScratch);
            for (int i = 0; i < s_RendererScratch.Count; i++)
            {
                MeshRenderer renderer = s_RendererScratch[i];
                if (renderer != null)
                    Object.DestroyImmediate(renderer);
            }
            s_RendererScratch.Clear();
        }

        private static void StripMonoBehaviours(GameObject root)
        {
            root.GetComponentsInChildren(true, s_MonoBehaviourScratch);
            for (int i = 0; i < s_MonoBehaviourScratch.Count; i++)
            {
                MonoBehaviour behaviour = s_MonoBehaviourScratch[i];
                if (behaviour != null)
                    Object.DestroyImmediate(behaviour);
            }
            s_MonoBehaviourScratch.Clear();
        }

        private static void StripRigidbodies(GameObject root)
        {
            root.GetComponentsInChildren(true, s_RigidbodyScratch);
            for (int i = 0; i < s_RigidbodyScratch.Count; i++)
            {
                Rigidbody rigidbody = s_RigidbodyScratch[i];
                if (rigidbody != null)
                    Object.DestroyImmediate(rigidbody);
            }
            s_RigidbodyScratch.Clear();
        }

        private static void ApplyLayerRecursively(Transform root, int layer)
        {
            if (root == null)
                return;

            root.gameObject.layer = layer;
            for (int i = 0; i < root.childCount; i++)
                ApplyLayerRecursively(root.GetChild(i), layer);
        }

        private static string[] ResolveSearchFolders(string primary, string fallbackA, string fallbackB)
        {
            s_TempFolderScratch.Clear();
            AddFolderIfValid(primary);
            AddFolderIfValid(fallbackA);
            AddFolderIfValid(fallbackB);
            string[] result = s_TempFolderScratch.ToArray();
            s_TempFolderScratch.Clear();
            return result;
        }

        private static readonly List<string> s_TempFolderScratch = new List<string>(4);

        private static void AddFolderIfValid(string folder)
        {
            if (string.IsNullOrWhiteSpace(folder))
                return;

            string normalized = folder.Replace('\\', '/');
            if (!AssetDatabase.IsValidFolder(normalized))
                return;

            if (!s_TempFolderScratch.Contains(normalized))
                s_TempFolderScratch.Add(normalized);
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

            Directory.CreateDirectory(ResolveFullPath(assetFolder));
            AssetDatabase.Refresh();
        }

        private static void WriteReport(FactoryReport report)
        {
            try
            {
                string path = ResolveProjectPath(ReportPath);
                string directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);

                File.WriteAllText(path, JsonUtility.ToJson(report, true));
            }
            catch (Exception exception)
            {
                Debug.LogError("[HazardPrefabFactory1736] Report write failed: " + exception.GetType().Name + " " + exception.Message);
            }
        }

        private static void ClearScratch()
        {
            s_MaterialScratch.Clear();
            s_ColliderScratch.Clear();
            s_MeshColliderScratch.Clear();
            s_RigidbodyScratch.Clear();
            s_RendererScratch.Clear();
            s_ParticleScratch.Clear();
            s_LightScratch.Clear();
            s_DamageRouterScratch.Clear();
            s_MetadataScratch.Clear();
            s_HazardSourceScratch.Clear();
            s_EnvironmentalHazardScratch.Clear();
            s_DecalScratch.Clear();
            s_LightCullingScratch.Clear();
            s_ThermalRuntimeScratch.Clear();
            s_MonoBehaviourScratch.Clear();
            s_TransformScratch.Clear();
            s_TempFolderScratch.Clear();
            s_ShaderCbufferCache.Clear();
        }

        private static string NormalizeSearch(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            char[] chars = value.ToLowerInvariant().ToCharArray();
            int write = 0;
            for (int i = 0; i < chars.Length; i++)
            {
                char c = chars[i];
                if ((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9'))
                    chars[write++] = c;
            }

            return new string(chars, 0, write);
        }

        private static string SanitizeFileName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "UnnamedHazard";

            char[] invalid = Path.GetInvalidFileNameChars();
            string safe = value.Trim().Replace(' ', '_');
            for (int i = 0; i < invalid.Length; i++)
                safe = safe.Replace(invalid[i], '_');
            return safe;
        }

        private static string ResolveFullPath(string projectRelativePath)
        {
            return Path.GetFullPath(Path.Combine(Directory.GetParent(Application.dataPath).FullName, projectRelativePath.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar)));
        }

        private static string ResolveProjectPath(string projectRelativePath)
        {
            return Path.GetFullPath(Path.Combine(Directory.GetParent(Application.dataPath).FullName, projectRelativePath.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar)));
        }

        private static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        }

        private static bool IsUnitScale(Vector3 value)
        {
            return IsFinite(value) &&
                   Mathf.Abs(value.x - 1f) <= 0.0001f &&
                   Mathf.Abs(value.y - 1f) <= 0.0001f &&
                   Mathf.Abs(value.z - 1f) <= 0.0001f;
        }

        private static bool IsApproximately(Vector3 left, Vector3 right, float tolerance)
        {
            if (!IsFinite(left) || !IsFinite(right) || !IsFinite(tolerance))
                return false;

            float safeTolerance = Mathf.Max(0f, tolerance);
            return (left - right).sqrMagnitude <= safeTolerance * safeTolerance;
        }

        private static bool IsDirectionAligned(Vector3 left, Vector3 right)
        {
            if (!IsFinite(left) || !IsFinite(right))
                return false;

            float leftSqr = left.sqrMagnitude;
            float rightSqr = right.sqrMagnitude;
            if (leftSqr <= 0.0001f || rightSqr <= 0.0001f)
                return false;

            float dot = Vector3.Dot(left / Mathf.Sqrt(leftSqr), right / Mathf.Sqrt(rightSqr));
            return dot >= 0.999f;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static float ComputeBoundsVolume(Bounds bounds)
        {
            Vector3 size = bounds.size;
            return Mathf.Max(0f, size.x) * Mathf.Max(0f, size.y) * Mathf.Max(0f, size.z);
        }

        private static uint ComputeStableHash32(string value)
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

                return hash;
            }
        }

        [Serializable]
        public struct FactorySettings
        {
            public string MeshDirectory;
            public string MaterialDirectory;
            public string CollisionProxyDirectory;
            public string MetadataDirectory;
            public string OutputDirectory;
            public bool DryRun;
            public int MaxGroupsPerRun;

            public static FactorySettings Default => new FactorySettings
            {
                MeshDirectory = DefaultMeshDirectory,
                MaterialDirectory = DefaultMaterialDirectory,
                CollisionProxyDirectory = DefaultCollisionProxyDirectory,
                MetadataDirectory = DefaultMetadataDirectory,
                OutputDirectory = DefaultOutputDirectory,
                DryRun = true,
                MaxGroupsPerRun = 256
            };

            public FactorySettings Sanitize()
            {
                return new FactorySettings
                {
                    MeshDirectory = string.IsNullOrWhiteSpace(MeshDirectory) ? DefaultMeshDirectory : MeshDirectory.Replace('\\', '/'),
                    MaterialDirectory = string.IsNullOrWhiteSpace(MaterialDirectory) ? DefaultMaterialDirectory : MaterialDirectory.Replace('\\', '/'),
                    CollisionProxyDirectory = string.IsNullOrWhiteSpace(CollisionProxyDirectory) ? DefaultCollisionProxyDirectory : CollisionProxyDirectory.Replace('\\', '/'),
                    MetadataDirectory = string.IsNullOrWhiteSpace(MetadataDirectory) ? DefaultMetadataDirectory : MetadataDirectory.Replace('\\', '/'),
                    OutputDirectory = string.IsNullOrWhiteSpace(OutputDirectory) ? DefaultOutputDirectory : OutputDirectory.Replace('\\', '/'),
                    DryRun = DryRun,
                    MaxGroupsPerRun = Mathf.Clamp(MaxGroupsPerRun <= 0 ? 256 : MaxGroupsPerRun, 1, 4096)
                };
            }
        }

        [Serializable]
        public sealed class FactoryReport
        {
            public string AgentId;
            public string MeshDirectory;
            public string MaterialDirectory;
            public string CollisionProxyDirectory;
            public string MetadataDirectory;
            public string OutputDirectory;
            public bool DryRun;
            public int GroupsDiscovered;
            public int PrefabsAssembled;
            public int PrefabsFailed;
            public int DamageTriggersValidated;
            public int VfxAnchorsValidated;
            public int PrimitiveCollisionFallbacks;
            public int CollisionProxyAssetsUsed;
            public int LightsValidated;
            public int DecalsValidated;
            public int RenderersValidated;
            public double ExecutionMicroseconds;
            public float BoundsVolumeCubicMeters;
            public List<string> Violations = new List<string>(64);
        }

        private sealed class HazardMeshGroup
        {
            public readonly string BaseName;
            public readonly Mesh[] Lods = new Mesh[3];
            public readonly string[] LodPaths = new string[3];

            public HazardMeshGroup(string baseName)
            {
                BaseName = baseName;
            }

            public bool HasRequiredLods => Lods[0] != null && Lods[1] != null && Lods[2] != null;
        }

        private sealed class HazardProfile
        {
            public HazardType HazardType;
            public uint DamageType;
            public uint StatusBits;
            public float BaseDamage;
            public float StatusDurationSeconds;
            public float TriggerPaddingMeters;
            public float TriggerRadiusMeters;
            public float CullDistanceMeters;
            public float LightIntensity;
            public float LightRange;
            public Color LightColor;
            public float PulseFrequencyHz;
            public float PulseAmplitude;
            public string EffectId;
            public uint EffectHash;
            public string SurfaceMaterialName;
            public string DecalMaterialName;
            public float DecalFade;
            public HazardAnchorFile[] Anchors;

            public static HazardProfile FromName(string baseName)
            {
                string normalized = NormalizeSearch(baseName);
                bool flare = normalized.IndexOf("flare", StringComparison.Ordinal) >= 0 ||
                             normalized.IndexOf("fire", StringComparison.Ordinal) >= 0;
                bool hydrothermal = normalized.IndexOf("vent", StringComparison.Ordinal) >= 0 ||
                                    normalized.IndexOf("geyser", StringComparison.Ordinal) >= 0 ||
                                    normalized.IndexOf("smoker", StringComparison.Ordinal) >= 0 ||
                                    normalized.IndexOf("sulfur", StringComparison.Ordinal) >= 0;
                bool coral = normalized.IndexOf("coral", StringComparison.Ordinal) >= 0;
                bool caustic = normalized.IndexOf("acid", StringComparison.Ordinal) >= 0 ||
                               normalized.IndexOf("brine", StringComparison.Ordinal) >= 0 ||
                               normalized.IndexOf("toxic", StringComparison.Ordinal) >= 0;
                bool toxicTruth = !flare && (coral || caustic);

                HazardProfile profile = new HazardProfile
                {
                    HazardType = toxicTruth ? HazardType.Toxicity : HazardType.Heat,
                    DamageType = toxicTruth ? CombatDamageTypes.Toxic : CombatDamageTypes.Thermal,
                    StatusBits = toxicTruth ? CombatStatusBits.Poisoned : CombatStatusBits.Burning,
                    BaseDamage = toxicTruth ? (caustic ? 6f : 5f) : (flare ? 7.5f : 9f),
                    StatusDurationSeconds = toxicTruth ? (caustic ? 6.5f : 6f) : (flare ? 3f : 4f),
                    TriggerPaddingMeters = DefaultTriggerPaddingMeters,
                    TriggerRadiusMeters = 0f,
                    CullDistanceMeters = toxicTruth ? 28f : (flare ? 42f : DefaultPresentationCullDistanceMeters),
                    LightIntensity = toxicTruth ? (coral ? 1.15f : 0.85f) : (flare ? 4.2f : 3.2f),
                    LightRange = toxicTruth ? (coral ? 8f : 7f) : (flare ? 18f : 15f),
                    LightColor = toxicTruth ? new Color(0.45f, 1f, 0.32f, 1f) : (flare ? new Color(1f, 0.28f, 0.08f, 1f) : new Color(1f, 0.42f, 0.16f, 1f)),
                    PulseFrequencyHz = toxicTruth ? (hydrothermal ? 0.32f : 0.25f) : (flare ? 0.85f : 0.55f),
                    PulseAmplitude = toxicTruth ? (hydrothermal ? 0.11f : 0.08f) : (flare ? 0.24f : 0.18f),
                    EffectId = toxicTruth ? (hydrothermal ? "vfx_toxic_steam" : "vfx_toxic_coral_spore") : (flare ? "vfx_hazard_flare_fire" : "vfx_thermal_steam_fire"),
                    SurfaceMaterialName = toxicTruth ? (coral ? "coral" : "hazard") : (flare ? "emiss" : "rock"),
                    DecalMaterialName = "decal",
                    DecalFade = toxicTruth ? 0.7f : (flare ? 0.82f : 0.75f)
                };
                profile.EffectHash = ComputeStableHash32(profile.EffectId);
                return profile;
            }

            public void Apply(HazardMetadataFile file)
            {
                if (!string.IsNullOrWhiteSpace(file.hazardType))
                {
                    string normalizedType = NormalizeSearch(file.hazardType);
                    if (normalizedType.IndexOf("toxic", StringComparison.Ordinal) >= 0 || normalizedType.IndexOf("bio", StringComparison.Ordinal) >= 0)
                    {
                        HazardType = HazardType.Toxicity;
                        DamageType = CombatDamageTypes.Toxic;
                        StatusBits = CombatStatusBits.Poisoned;
                    }
                    else if (normalizedType.IndexOf("radiation", StringComparison.Ordinal) >= 0)
                    {
                        HazardType = HazardType.Radiation;
                        DamageType = CombatDamageTypes.Radioactive;
                        StatusBits = CombatStatusBits.Irradiated;
                    }
                    else
                    {
                        HazardType = HazardType.Heat;
                        DamageType = CombatDamageTypes.Thermal;
                        StatusBits = CombatStatusBits.Burning;
                    }
                }

                if (!string.IsNullOrWhiteSpace(file.effectId))
                    EffectId = file.effectId;
                if (!string.IsNullOrWhiteSpace(file.surfaceMaterial))
                    SurfaceMaterialName = file.surfaceMaterial;
                if (!string.IsNullOrWhiteSpace(file.decalMaterial))
                    DecalMaterialName = file.decalMaterial;
                if (file.baseDamage > 0f && IsFinite(file.baseDamage))
                    BaseDamage = file.baseDamage;
                if (file.statusDurationSeconds >= 0f && IsFinite(file.statusDurationSeconds))
                    StatusDurationSeconds = file.statusDurationSeconds;
                if (file.triggerPadding >= 0f && IsFinite(file.triggerPadding))
                    TriggerPaddingMeters = file.triggerPadding;
                if (file.triggerRadius > 0f && IsFinite(file.triggerRadius))
                    TriggerRadiusMeters = file.triggerRadius;
                if (file.cullDistance > 0f && IsFinite(file.cullDistance))
                    CullDistanceMeters = file.cullDistance;
                if (file.lightIntensity >= 0f && IsFinite(file.lightIntensity))
                    LightIntensity = file.lightIntensity;
                if (file.lightRange > 0f && IsFinite(file.lightRange))
                    LightRange = file.lightRange;
                Color authoredLightColor = file.lightColor;
                bool hasAuthoredLightColor = authoredLightColor.r != 0f ||
                                             authoredLightColor.g != 0f ||
                                             authoredLightColor.b != 0f ||
                                             authoredLightColor.a != 0f;
                if (hasAuthoredLightColor &&
                    IsFinite(authoredLightColor.r) &&
                    IsFinite(authoredLightColor.g) &&
                    IsFinite(authoredLightColor.b))
                {
                    LightColor = authoredLightColor;
                }
                if (file.pulseFrequencyHz >= 0f && IsFinite(file.pulseFrequencyHz))
                    PulseFrequencyHz = file.pulseFrequencyHz;
                if (file.pulseAmplitude >= 0f && IsFinite(file.pulseAmplitude))
                    PulseAmplitude = Mathf.Clamp01(file.pulseAmplitude);
                if (file.decalFade >= 0f && IsFinite(file.decalFade))
                    DecalFade = Mathf.Clamp01(file.decalFade);
                if (file.anchors != null && file.anchors.Length > 0)
                    Anchors = file.anchors;

                EffectHash = ComputeStableHash32(EffectId);
            }
        }

        [Serializable]
        private sealed class HazardMetadataFile
        {
            public string hazardType;
            public string effectId;
            public string surfaceMaterial;
            public string decalMaterial;
            public float baseDamage;
            public float statusDurationSeconds;
            public float triggerPadding;
            public float triggerRadius;
            public float cullDistance;
            public float lightIntensity;
            public float lightRange;
            public Color lightColor;
            public float pulseFrequencyHz;
            public float pulseAmplitude;
            public float decalFade;
            public HazardAnchorFile[] anchors;
        }

        [Serializable]
        private struct HazardAnchorFile
        {
            public string effectId;
            public Vector3 localPosition;
            public Vector3 forward;
        }

        private struct HazardAnchorDefinition
        {
            public Vector3 LocalPosition;
            public Vector3 LocalForward;
            public string EffectId;
        }
    }
}
#endif
