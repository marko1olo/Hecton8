#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using Hecton8.World;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

namespace Hecton8.Editor.Assembly
{
    public sealed class FloraPrefabFactory : EditorWindow
    {
        private const string AgentId = "1732";
        private const string DefaultMeshDirectory = "Assets/_Project/Art/Generated/Flora";
        private const string DefaultMaterialDirectory = "Assets/_Project/Art/Materials/WorldProceduralProxy";
        private const string DefaultMetadataDirectory = "Assets/_Project/Data/World/FloraTemplates";
        private const string DefaultOutputDirectory = "Assets/Prefabs/Environment/Flora";
        private const string FloraTriggerLayerName = "Flora_NonColliding";
        private const string ImpostorMaterialName = "MAT_Flora_ImpostorAtlas";
        private const float SmallFloraVolumeCubicMeters = 1f;
        private const float MinimumLodGroupSize = 0.05f;
        private const int MaxValidatedFloraMeshVertices = 9600;
        private const int MaxDiscoveredFloraGroups = 512;
        private const int MaxMaterialCandidates = 512;
        private const int MaxFloraTemplates = 256;
        private const int MaxFactoryViolations = 256;
        private const int ShaderFeatureCacheCapacity = 64;

        private static readonly int SwayAmplitudeId = Shader.PropertyToID("_SwayAmplitude");
        private static readonly int BiolumPhaseId = Shader.PropertyToID("_BiolumPhase");

        // COLD ALLOC: editor-only scratch containers for offline prefab construction.
        private static readonly List<Vector3> s_VertexScratch = new List<Vector3>(MaxValidatedFloraMeshVertices);
        private static readonly List<Vector2> s_UvScratch = new List<Vector2>(MaxValidatedFloraMeshVertices);
        private static readonly List<Color> s_ColorScratch = new List<Color>(MaxValidatedFloraMeshVertices);
        private static readonly List<MeshCollider> s_MeshColliderScratch = new List<MeshCollider>(2);
        private static readonly List<MeshRenderer> s_RendererScratch = new List<MeshRenderer>(8);
        private static readonly List<Material> s_MaterialScratch = new List<Material>(MaxMaterialCandidates);
        private static readonly List<FloraDataTemplate> s_TemplateScratch = new List<FloraDataTemplate>(MaxFloraTemplates);
        private static readonly Dictionary<Shader, ShaderFeatureFlags> s_ShaderFeatureCache = new Dictionary<Shader, ShaderFeatureFlags>(ShaderFeatureCacheCapacity);

        [SerializeField] private string meshDirectory = DefaultMeshDirectory;
        [SerializeField] private string materialDirectory = DefaultMaterialDirectory;
        [SerializeField] private string metadataDirectory = DefaultMetadataDirectory;
        [SerializeField] private string outputDirectory = DefaultOutputDirectory;
        [SerializeField] private bool dryRun = true;
        [SerializeField] private int maxGroupsPerRun = 512;

        private Vector2 scroll;
        private FactoryReport lastReport;

        [MenuItem("HECTON-8/Assembly/Flora Prefab Factory 1732")]
        public static void OpenWindow()
        {
            FloraPrefabFactory window = GetWindow<FloraPrefabFactory>("Flora Factory 1732");
            window.minSize = new Vector2(680f, 440f);
            window.Show();
        }

        [MenuItem("HECTON-8/Assembly/Dry Run Flora Prefab Factory 1732")]
        public static void RunDefaultDryRun()
        {
            FactorySettings settings = FactorySettings.Default;
            settings.DryRun = true;
            Run(settings);
        }

        [MenuItem("HECTON-8/Assembly/Run Flora Prefab Factory 1732")]
        public static void RunDefault()
        {
            FactorySettings settings = FactorySettings.Default;
            settings.DryRun = false;
            Run(settings);
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("HECTON-8 Flora Prefab Factory 1732", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Offline-only assembly: LOD0/1/2 meshes, shared vertex-sway materials, CrossFade LODGroup, impostor atlas, and trigger-only harvest anchors.", MessageType.Info);

            meshDirectory = EditorGUILayout.TextField("Mesh Directory", meshDirectory);
            materialDirectory = EditorGUILayout.TextField("Material Directory", materialDirectory);
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
                MetadataDirectory = settings.MetadataDirectory,
                OutputDirectory = settings.OutputDirectory,
                DryRun = settings.DryRun
            };

            try
            {
                Dictionary<string, FloraMeshGroup> groups = DiscoverMeshGroups(settings, report);
                report.GroupsDiscovered = groups.Count;
                LoadFloraTemplates(settings.MetadataDirectory, report);
                LoadMaterials(settings.MaterialDirectory, report);

                Material impostorAtlasMaterial = ResolveImpostorAtlasMaterial();
                if (impostorAtlasMaterial == null)
                    AddViolation(report, "FATAL: " + ImpostorMaterialName + " not found. LOD2 impostor binding will fail closed.");

                if (!settings.DryRun)
                    EnsureAssetFolder(settings.OutputDirectory);

                int processed = 0;
                foreach (KeyValuePair<string, FloraMeshGroup> pair in groups)
                {
                    if (processed >= settings.MaxGroupsPerRun)
                        break;

                    processed++;
                    ProcessGroup(pair.Value, impostorAtlasMaterial, settings, report);
                }
            }
            catch (Exception exception)
            {
                AddViolation(report, "FATAL: Factory exception: " + exception.GetType().Name + " " + exception.Message);
                Debug.LogException(exception);
            }
            finally
            {
                stopwatch.Stop();
                report.ExecutionMicroseconds = stopwatch.Elapsed.TotalMilliseconds * 1000.0;
                ClearScratch();
            }

            Debug.Log("[FloraPrefabFactory1732] Completed. Groups=" + report.GroupsDiscovered +
                      " Assembled=" + report.PrefabsAssembled +
                      " Failed=" + report.PrefabsFailed +
                      " us=" + report.ExecutionMicroseconds.ToString("F1", CultureInfo.InvariantCulture));
            return report;
        }

        private static Dictionary<string, FloraMeshGroup> DiscoverMeshGroups(FactorySettings settings, FactoryReport report)
        {
            Dictionary<string, FloraMeshGroup> groups = new Dictionary<string, FloraMeshGroup>(MaxDiscoveredFloraGroups, StringComparer.Ordinal);
            string[] guids = AssetDatabase.FindAssets("t:Mesh", new[] { settings.MeshDirectory });
            bool capacityViolationReported = false;
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if (mesh == null)
                    continue;

                if (!TryExtractLod(path, out string baseName, out int lodIndex))
                    continue;

                if (!groups.TryGetValue(baseName, out FloraMeshGroup group))
                {
                    if (groups.Count >= MaxDiscoveredFloraGroups)
                    {
                        if (!capacityViolationReported)
                        {
                            AddViolation(report, "FATAL: Flora mesh group discovery exceeded fixed capacity " + MaxDiscoveredFloraGroups.ToString(CultureInfo.InvariantCulture) + ".");
                            capacityViolationReported = true;
                        }

                        continue;
                    }

                    group = new FloraMeshGroup(baseName);
                    groups.Add(baseName, group);
                }

                if (group.Lods[lodIndex] != null)
                {
                    AddViolation(report, "Duplicate LOD" + lodIndex.ToString(CultureInfo.InvariantCulture) + " mesh for " + baseName + ": " + path);
                    continue;
                }

                group.Lods[lodIndex] = mesh;
                group.LodPaths[lodIndex] = path;
            }

            return groups;
        }

        private static bool TryExtractLod(string assetPath, out string baseName, out int lodIndex)
        {
            baseName = string.Empty;
            lodIndex = -1;
            string fileName = Path.GetFileNameWithoutExtension(assetPath);
            if (string.IsNullOrEmpty(fileName))
                return false;

            int lodMarker = fileName.LastIndexOf("_LOD", StringComparison.OrdinalIgnoreCase);
            if (lodMarker < 0 || lodMarker + 4 >= fileName.Length)
                return false;

            char lodChar = fileName[lodMarker + 4];
            if (lodChar < '0' || lodChar > '2')
                return false;

            lodIndex = lodChar - '0';
            baseName = fileName.Substring(0, lodMarker);
            if (baseName.StartsWith("GEN_", StringComparison.OrdinalIgnoreCase))
                baseName = baseName.Substring(4);

            return !string.IsNullOrWhiteSpace(baseName);
        }

        private static void ProcessGroup(FloraMeshGroup group, Material impostorAtlasMaterial, FactorySettings settings, FactoryReport report)
        {
            if (!group.HasRequiredLods)
            {
                report.PrefabsFailed++;
                AddViolation(report, "FATAL: " + group.BaseName + " missing one of LOD0/LOD1/LOD2.");
                return;
            }

            Material floraMaterial = ResolveFloraMaterial(group.BaseName);
            if (floraMaterial == null)
            {
                report.PrefabsFailed++;
                AddViolation(report, "FATAL: No BRG-compliant vertex-sway material found for " + group.BaseName + ".");
                return;
            }

            string impostorFailure = string.Empty;
            if (impostorAtlasMaterial == null ||
                !ValidateImpostorMaterial(impostorAtlasMaterial, out impostorFailure))
            {
                report.PrefabsFailed++;
                AddViolation(report, "FATAL: " + group.BaseName + " missing valid shared impostor atlas material. " + impostorFailure);
                return;
            }

            if (!settings.DryRun)
            {
                ConfigureSharedMaterialForAssembly(floraMaterial);
                ConfigureSharedMaterialForAssembly(impostorAtlasMaterial);
            }

            GameObject root = null;
            string prefabPath = settings.OutputDirectory + "/PFB_" + SanitizeFileName(group.BaseName) + ".prefab";
            try
            {
                root = new GameObject("PFB_" + SanitizeFileName(group.BaseName));
                if (!TryAssemblePrefabRoot(root, group, floraMaterial, impostorAtlasMaterial, settings, report, out Bounds combinedBounds, out bool hasHarvestTrigger))
                {
                    report.PrefabsFailed++;
                    return;
                }

                if (!ValidatePrefabInstance(root, hasHarvestTrigger, out string instanceFailure))
                {
                    report.PrefabsFailed++;
                    AddViolation(report, "Flora Assembly Violation Detected! " + group.BaseName + ": " + instanceFailure);
                    return;
                }

                report.LodGroupsValidated++;
                root.GetComponentsInChildren(true, s_RendererScratch);
                report.RenderersValidated += s_RendererScratch.Count;
                s_RendererScratch.Clear();
                if (hasHarvestTrigger)
                    report.HarvestTriggersValidated++;

                if (settings.DryRun)
                {
                    report.PrefabsAssembled++;
                    return;
                }

                GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                if (savedPrefab == null)
                {
                    report.PrefabsFailed++;
                    AddViolation(report, "Flora Assembly Violation Detected! SaveAsPrefabAsset returned null for " + prefabPath + ".");
                    DeletePrefabAsset(prefabPath);
                    return;
                }

                if (!ValidateSavedPrefab(prefabPath, out string savedFailure))
                {
                    report.PrefabsFailed++;
                    AddViolation(report, "Flora Assembly Violation Detected! " + prefabPath + ": " + savedFailure);
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
            FloraMeshGroup group,
            Material floraMaterial,
            Material impostorAtlasMaterial,
            FactorySettings settings,
            FactoryReport report,
            out Bounds combinedBounds,
            out bool hasHarvestTrigger)
        {
            combinedBounds = new Bounds(Vector3.zero, Vector3.one * MinimumLodGroupSize);
            hasHarvestTrigger = false;
            if (!TryValidateMeshContracts(group, out string meshFailure))
            {
                AddViolation(report, "FATAL: " + group.BaseName + " mesh contract failed: " + meshFailure);
                return false;
            }

            if (!TryComputeLowestVertexY(group, out float lowestVertexY, out string lowestFailure))
            {
                AddViolation(report, "FATAL: " + group.BaseName + " pivot solve failed: " + lowestFailure);
                return false;
            }

            Vector3 childOffset = new Vector3(0f, -lowestVertexY, 0f);
            Renderer lod0Renderer = CreateLodChild(root.transform, "LOD0", group.Lods[0], floraMaterial, childOffset, false, false);
            Renderer lod1Renderer = CreateLodChild(root.transform, "LOD1", group.Lods[1], floraMaterial, childOffset, false, false);
            Renderer lod2Renderer = CreateLodChild(root.transform, "LOD2_Impostor", group.Lods[2], impostorAtlasMaterial, childOffset, true, false);
            if (lod0Renderer == null || lod1Renderer == null || lod2Renderer == null)
            {
                AddViolation(report, "FATAL: " + group.BaseName + " renderer construction failed.");
                return false;
            }

            combinedBounds = ComputeCombinedBounds(group, childOffset);
            bool smallFlora = ComputeBoundsVolume(combinedBounds) < SmallFloraVolumeCubicMeters;
            ConfigureRendererShadowPolicy(lod0Renderer, false, smallFlora);
            ConfigureRendererShadowPolicy(lod1Renderer, false, smallFlora);
            ConfigureRendererShadowPolicy(lod2Renderer, true, smallFlora);

            LODGroup lodGroup = root.AddComponent<LODGroup>();
            lodGroup.fadeMode = LODFadeMode.CrossFade;
            lodGroup.animateCrossFading = true;
            lodGroup.SetLODs(CreateLods(combinedBounds, lod0Renderer, lod1Renderer, lod2Renderer));
            lodGroup.localReferencePoint = combinedBounds.center;
            lodGroup.size = Mathf.Max(MinimumLodGroupSize, Mathf.Max(combinedBounds.size.x, Mathf.Max(combinedBounds.size.y, combinedBounds.size.z)));
            lodGroup.RecalculateBounds();

            if (TryResolveHarvestMetadata(group.BaseName, out int itemId, out int harvestUnits))
            {
                if (!TryAddHarvestTrigger(root.transform, combinedBounds, itemId, harvestUnits, report))
                    return false;
                hasHarvestTrigger = true;
            }

            return true;
        }

        private static Renderer CreateLodChild(Transform parent, string name, Mesh mesh, Material material, Vector3 localPosition, bool impostor, bool smallFlora)
        {
            if (mesh == null || material == null)
                return null;

            GameObject child = new GameObject(name);
            child.transform.SetParent(parent, false);
            child.transform.localPosition = localPosition;
            MeshFilter filter = child.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;
            MeshRenderer renderer = child.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            ConfigureRendererShadowPolicy(renderer, impostor, smallFlora);
            return renderer;
        }

        private static void ConfigureRendererShadowPolicy(Renderer renderer, bool impostor, bool smallFlora)
        {
            if (renderer == null)
                return;

            bool allowShadows = !impostor && !smallFlora;
            renderer.shadowCastingMode = allowShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            renderer.receiveShadows = allowShadows;
        }

        private static LOD[] CreateLods(Bounds bounds, Renderer lod0, Renderer lod1, Renderer lod2)
        {
            float volume = Mathf.Max(0.0001f, ComputeBoundsVolume(bounds));
            float inverseSize = Mathf.Clamp(1f / Mathf.Sqrt(volume), 0.25f, 1.75f);
            float lod0Height = Mathf.Clamp(0.42f * inverseSize, 0.20f, 0.58f);
            float lod1Height = Mathf.Clamp(0.16f * inverseSize, 0.06f, Mathf.Min(0.24f, lod0Height - 0.04f));
            float lod2Height = Mathf.Clamp(0.025f * inverseSize, 0.012f, Mathf.Min(0.055f, lod1Height - 0.01f));
            return new[]
            {
                new LOD(lod0Height, new[] { lod0 }),
                new LOD(lod1Height, new[] { lod1 }),
                new LOD(lod2Height, new[] { lod2 })
            };
        }

        private static bool TryValidateMeshContracts(FloraMeshGroup group, out string failure)
        {
            failure = string.Empty;
            for (int lod = 0; lod < 3; lod++)
            {
                Mesh mesh = group.Lods[lod];
                if (!TryValidateMeshContract(mesh, lod, out failure))
                    return false;
            }

            return true;
        }

        private static bool TryValidateMeshContract(Mesh mesh, int lod, out string failure)
        {
            failure = string.Empty;
            if (mesh == null)
            {
                failure = "LOD" + lod.ToString(CultureInfo.InvariantCulture) + " mesh is null.";
                return false;
            }

            if (!mesh.isReadable)
            {
                failure = "LOD" + lod.ToString(CultureInfo.InvariantCulture) + " mesh is not readable.";
                return false;
            }

            if (mesh.vertexCount <= 0)
            {
                failure = "LOD" + lod.ToString(CultureInfo.InvariantCulture) + " has zero vertices.";
                return false;
            }

            if (mesh.vertexCount > MaxValidatedFloraMeshVertices)
            {
                failure = "LOD" + lod.ToString(CultureInfo.InvariantCulture) + " exceeds flora mesh vertex budget.";
                return false;
            }

            if (mesh.subMeshCount != 1)
            {
                failure = "LOD" + lod.ToString(CultureInfo.InvariantCulture) + " must be one non-empty triangle submesh.";
                return false;
            }

            ulong indexCount = mesh.GetIndexCount(0);
            MeshTopology topology = mesh.GetTopology(0);
            if (indexCount == 0ul ||
                indexCount % 3ul != 0ul ||
                topology != MeshTopology.Triangles)
            {
                failure = "LOD" + lod.ToString(CultureInfo.InvariantCulture) + " must be one non-empty triangle submesh.";
                return false;
            }

            if (!mesh.HasVertexAttribute(VertexAttribute.Position) ||
                !mesh.HasVertexAttribute(VertexAttribute.TexCoord0))
            {
                failure = "LOD" + lod.ToString(CultureInfo.InvariantCulture) + " lacks position or UV0 attributes.";
                return false;
            }

            if (lod < 2 &&
                (!mesh.HasVertexAttribute(VertexAttribute.Normal) ||
                 !mesh.HasVertexAttribute(VertexAttribute.Tangent) ||
                 !mesh.HasVertexAttribute(VertexAttribute.Color)))
            {
                failure = "LOD" + lod.ToString(CultureInfo.InvariantCulture) + " lacks normal, tangent, or vertex color attributes required by ProceduralBio.";
                return false;
            }

            if (!TryValidateUv0(mesh, lod, out failure))
                return false;

            if (lod < 2 && !TryValidateVertexColorGradient(mesh, lod, out failure))
                return false;

            Bounds bounds = mesh.bounds;
            if (!IsFinite(bounds.center) ||
                !IsFinite(bounds.extents) ||
                bounds.extents.sqrMagnitude <= 0.000001f)
            {
                failure = "LOD" + lod.ToString(CultureInfo.InvariantCulture) + " has invalid bounds.";
                return false;
            }

            return true;
        }

        private static bool TryValidateUv0(Mesh mesh, int lod, out string failure)
        {
            failure = string.Empty;
            s_UvScratch.Clear();
            mesh.GetUVs(0, s_UvScratch);
            if (s_UvScratch.Count != mesh.vertexCount)
            {
                failure = "LOD" + lod.ToString(CultureInfo.InvariantCulture) + " UV0 count does not match vertex count.";
                s_UvScratch.Clear();
                return false;
            }

            for (int i = 0; i < s_UvScratch.Count; i++)
            {
                Vector2 uv = s_UvScratch[i];
                if (!IsFinite(uv))
                {
                    failure = "LOD" + lod.ToString(CultureInfo.InvariantCulture) + " contains non-finite UV0.";
                    s_UvScratch.Clear();
                    return false;
                }
            }

            s_UvScratch.Clear();
            return true;
        }

        private static bool TryValidateVertexColorGradient(Mesh mesh, int lod, out string failure)
        {
            failure = string.Empty;
            s_ColorScratch.Clear();
            mesh.GetColors(s_ColorScratch);
            if (s_ColorScratch.Count != mesh.vertexCount)
            {
                failure = "LOD" + lod.ToString(CultureInfo.InvariantCulture) + " vertex color count does not match vertex count.";
                s_ColorScratch.Clear();
                return false;
            }

            float minR = 1f;
            float maxR = 0f;
            for (int i = 0; i < s_ColorScratch.Count; i++)
            {
                Color color = s_ColorScratch[i];
                if (!IsFinite(color))
                {
                    failure = "LOD" + lod.ToString(CultureInfo.InvariantCulture) + " contains non-finite vertex color.";
                    s_ColorScratch.Clear();
                    return false;
                }

                minR = Mathf.Min(minR, color.r);
                maxR = Mathf.Max(maxR, color.r);
            }

            s_ColorScratch.Clear();
            if (minR > 0.08f || maxR < 0.82f)
            {
                failure = "LOD" + lod.ToString(CultureInfo.InvariantCulture) + " vertex color R gradient is too weak for sway/root-tip shading.";
                return false;
            }

            return true;
        }

        private static bool TryComputeLowestVertexY(FloraMeshGroup group, out float lowestY, out string failure)
        {
            lowestY = float.MaxValue;
            failure = string.Empty;
            bool found = false;
            for (int lod = 0; lod < 3; lod++)
            {
                Mesh mesh = group.Lods[lod];
                if (mesh == null)
                {
                    failure = "LOD" + lod.ToString(CultureInfo.InvariantCulture) + " mesh is null.";
                    return false;
                }

                try
                {
                    s_VertexScratch.Clear();
                    mesh.GetVertices(s_VertexScratch);
                }
                catch (Exception exception)
                {
                    failure = "LOD" + lod.ToString(CultureInfo.InvariantCulture) + " vertices are not readable: " + exception.Message;
                    return false;
                }

                if (s_VertexScratch.Count == 0)
                {
                    failure = "LOD" + lod.ToString(CultureInfo.InvariantCulture) + " has zero vertices.";
                    return false;
                }

                for (int i = 0; i < s_VertexScratch.Count; i++)
                {
                    Vector3 vertex = s_VertexScratch[i];
                    if (!IsFinite(vertex))
                        continue;
                    lowestY = Mathf.Min(lowestY, vertex.y);
                    found = true;
                }
            }

            if (!found)
            {
                failure = "all LOD vertices were non-finite.";
                return false;
            }

            return true;
        }

        private static Bounds ComputeCombinedBounds(FloraMeshGroup group, Vector3 childOffset)
        {
            bool initialized = false;
            Bounds combined = new Bounds(Vector3.zero, Vector3.one * MinimumLodGroupSize);
            for (int lod = 0; lod < 3; lod++)
            {
                Mesh mesh = group.Lods[lod];
                if (mesh == null)
                    continue;

                Bounds shifted = mesh.bounds;
                shifted.center += childOffset;
                if (!initialized)
                {
                    combined = shifted;
                    initialized = true;
                }
                else
                {
                    combined.Encapsulate(shifted);
                }
            }

            return combined;
        }

        private static bool TryAddHarvestTrigger(Transform root, Bounds combinedBounds, int itemId, int harvestUnits, FactoryReport report)
        {
            int layer = LayerMask.NameToLayer(FloraTriggerLayerName);
            if (layer < 0)
            {
                AddViolation(report, "FATAL: Layer " + FloraTriggerLayerName + " does not exist.");
                return false;
            }

            GameObject trigger = new GameObject("TRIG_HarvestNode");
            trigger.layer = layer;
            trigger.transform.SetParent(root, false);
            trigger.transform.localPosition = Vector3.zero;

            SphereCollider collider = trigger.AddComponent<SphereCollider>();
            collider.isTrigger = true;
            collider.center = combinedBounds.center;
            collider.radius = Mathf.Clamp(combinedBounds.extents.magnitude * 0.35f, 0.2f, 2.5f);

            ScavengeTarget target = trigger.AddComponent<ScavengeTarget>();
            target.ConfigureForEditor(itemId, harvestUnits);
            return true;
        }

        private static bool TryResolveHarvestMetadata(string baseName, out int itemId, out int harvestUnits)
        {
            itemId = 0;
            harvestUnits = 1;
            FloraDataTemplate template = FindBestTemplate(baseName);
            if (template != null && template.HarvestTemplate != null)
            {
                itemId = template.LootHashId != 0 ? template.LootHashId : template.CultivationSeedHashId;
                if (itemId == 0)
                    itemId = ComputeStableHash(baseName);
                return true;
            }

            string normalized = NormalizeSearch(baseName);
            bool nameIndicatesHarvest = normalized.IndexOf("seedpod", StringComparison.Ordinal) >= 0 ||
                                        normalized.IndexOf("kelpseed", StringComparison.Ordinal) >= 0 ||
                                        normalized.IndexOf("harvest", StringComparison.Ordinal) >= 0;
            if (!nameIndicatesHarvest)
                return false;

            itemId = ComputeStableHash(baseName);
            return true;
        }

        private static void LoadFloraTemplates(string metadataDirectory, FactoryReport report)
        {
            s_TemplateScratch.Clear();
            if (string.IsNullOrWhiteSpace(metadataDirectory) || !AssetDatabase.IsValidFolder(metadataDirectory))
                return;

            string[] guids = AssetDatabase.FindAssets("t:FloraDataTemplate", new[] { metadataDirectory });
            bool capacityViolationReported = false;
            for (int i = 0; i < guids.Length; i++)
            {
                FloraDataTemplate template = AssetDatabase.LoadAssetAtPath<FloraDataTemplate>(AssetDatabase.GUIDToAssetPath(guids[i]));
                if (template == null)
                    continue;

                if (s_TemplateScratch.Count >= MaxFloraTemplates)
                {
                    if (!capacityViolationReported)
                    {
                        AddViolation(report, "FATAL: Flora template discovery exceeded fixed capacity " + MaxFloraTemplates.ToString(CultureInfo.InvariantCulture) + ".");
                        capacityViolationReported = true;
                    }

                    continue;
                }

                s_TemplateScratch.Add(template);
            }
        }

        private static FloraDataTemplate FindBestTemplate(string baseName)
        {
            string normalizedBase = NormalizeSearch(baseName);
            FloraDataTemplate best = null;
            int bestScore = 0;
            for (int i = 0; i < s_TemplateScratch.Count; i++)
            {
                FloraDataTemplate template = s_TemplateScratch[i];
                if (template == null)
                    continue;

                string templateName = NormalizeSearch(template.name.Replace("FloraDataTemplate", string.Empty));
                if (string.IsNullOrEmpty(templateName))
                    continue;

                int score = normalizedBase.IndexOf(templateName, StringComparison.Ordinal) >= 0 ? templateName.Length : 0;
                if (score > bestScore)
                {
                    bestScore = score;
                    best = template;
                }
            }

            return best;
        }

        private static Material ResolveFloraMaterial(string baseName)
        {
            Material best = null;
            int bestScore = int.MinValue;
            string normalizedBase = NormalizeSearch(baseName);
            for (int i = 0; i < s_MaterialScratch.Count; i++)
            {
                Material material = s_MaterialScratch[i];
                if (material == null || !ValidateFloraMaterial(material, out _))
                    continue;

                int score = ScoreFloraMaterial(normalizedBase, NormalizeSearch(material.name));
                if (score > bestScore)
                {
                    bestScore = score;
                    best = material;
                }
            }

            return best;
        }

        private static int ScoreFloraMaterial(string normalizedBase, string normalizedMaterial)
        {
            int score = 0;
            if (normalizedBase.IndexOf(normalizedMaterial, StringComparison.Ordinal) >= 0)
                score += normalizedMaterial.Length;
            if (normalizedBase.IndexOf("kelp", StringComparison.Ordinal) >= 0 && normalizedMaterial.IndexOf("kelp", StringComparison.Ordinal) >= 0)
                score += 50;
            if (normalizedBase.IndexOf("coral", StringComparison.Ordinal) >= 0 && normalizedMaterial.IndexOf("coral", StringComparison.Ordinal) >= 0)
                score += 50;
            if (normalizedBase.IndexOf("sargassum", StringComparison.Ordinal) >= 0 && normalizedMaterial.IndexOf("sargassum", StringComparison.Ordinal) >= 0)
                score += 50;
            if (normalizedMaterial.IndexOf("proceduralbio", StringComparison.Ordinal) >= 0)
                score += 10;
            return score;
        }

        private static Material ResolveImpostorAtlasMaterial()
        {
            for (int i = 0; i < s_MaterialScratch.Count; i++)
            {
                Material material = s_MaterialScratch[i];
                if (material == null)
                    continue;

                if (string.Equals(material.name, ImpostorMaterialName, StringComparison.Ordinal))
                    return material;
            }

            return null;
        }

        private static void LoadMaterials(string materialDirectory, FactoryReport report)
        {
            s_MaterialScratch.Clear();
            string[] searchFolders = AssetDatabase.IsValidFolder(materialDirectory)
                ? new[] { materialDirectory }
                : new[] { "Assets/_Project/Art/Materials" };
            string[] guids = AssetDatabase.FindAssets("t:Material", searchFolders);
            bool capacityViolationReported = false;
            for (int i = 0; i < guids.Length; i++)
            {
                Material material = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guids[i]));
                if (material == null)
                    continue;

                if (s_MaterialScratch.Count >= MaxMaterialCandidates)
                {
                    if (!capacityViolationReported)
                    {
                        AddViolation(report, "FATAL: Flora material discovery exceeded fixed capacity " + MaxMaterialCandidates.ToString(CultureInfo.InvariantCulture) + ".");
                        capacityViolationReported = true;
                    }

                    continue;
                }

                s_MaterialScratch.Add(material);
            }
        }

        private static bool ValidateFloraMaterial(Material material, out string failure)
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

            string normalizedName = NormalizeSearch(material.name);
            if (normalizedName.IndexOf("placeholder", StringComparison.Ordinal) >= 0 ||
                normalizedName.IndexOf("debug", StringComparison.Ordinal) >= 0)
            {
                failure = material.name + " is a placeholder/debug material.";
                return false;
            }

            if (material.shader == null || !ShaderHasFeature(material.shader, ShaderFeatureFlags.UnityPerMaterialCbuffer))
            {
                failure = material.name + " shader lacks CBUFFER_START(UnityPerMaterial).";
                return false;
            }

            if (!ShaderHasFeature(material.shader, ShaderFeatureFlags.LodCrossFade))
            {
                failure = material.name + " shader lacks LOD_FADE_CROSSFADE and LODFadeCrossFade.";
                return false;
            }

            if (!material.HasProperty(SwayAmplitudeId) || !material.HasProperty(BiolumPhaseId))
            {
                failure = material.name + " lacks _SwayAmplitude or _BiolumPhase.";
                return false;
            }

            return true;
        }

        private static bool ValidateImpostorMaterial(Material material, out string failure)
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

            if (material.name.IndexOf("Impostor", StringComparison.OrdinalIgnoreCase) < 0)
            {
                failure = material.name + " name does not contain Impostor.";
                return false;
            }

            if (!string.Equals(material.name, ImpostorMaterialName, StringComparison.Ordinal))
            {
                failure = material.name + " is not the authored flora impostor atlas material.";
                return false;
            }

            if (material.shader == null || !ShaderHasFeature(material.shader, ShaderFeatureFlags.UnityPerMaterialCbuffer))
            {
                failure = material.name + " shader lacks CBUFFER_START(UnityPerMaterial).";
                return false;
            }

            if (!ShaderHasFeature(material.shader, ShaderFeatureFlags.LodCrossFade))
            {
                failure = material.name + " shader lacks LOD_FADE_CROSSFADE and LODFadeCrossFade.";
                return false;
            }

            return true;
        }

        private static void ConfigureSharedMaterialForAssembly(Material material)
        {
            if (material == null || material.enableInstancing)
                return;

            material.enableInstancing = true;
            EditorUtility.SetDirty(material);
        }

        private static bool ShaderHasFeature(Shader shader, ShaderFeatureFlags feature)
        {
            if (shader == null)
                return false;

            ShaderFeatureFlags flags = ResolveShaderFeatureFlags(shader);
            return (flags & feature) == feature;
        }

        private static ShaderFeatureFlags ResolveShaderFeatureFlags(Shader shader)
        {
            if (shader == null)
                return ShaderFeatureFlags.None;

            if (s_ShaderFeatureCache.TryGetValue(shader, out ShaderFeatureFlags cachedFlags))
                return cachedFlags;

            ShaderFeatureFlags flags = ShaderFeatureFlags.None;
            string shaderPath = AssetDatabase.GetAssetPath(shader);
            if (!string.IsNullOrEmpty(shaderPath))
            {
                string fullPath = ResolveFullPath(shaderPath);
                if (File.Exists(fullPath))
                {
                    string source = File.ReadAllText(fullPath);
                    if (source.IndexOf("CBUFFER_START(UnityPerMaterial)", StringComparison.Ordinal) >= 0)
                        flags |= ShaderFeatureFlags.UnityPerMaterialCbuffer;
                    if (source.IndexOf("LOD_FADE_CROSSFADE", StringComparison.Ordinal) >= 0 &&
                        source.IndexOf("LODFadeCrossFade", StringComparison.Ordinal) >= 0)
                    {
                        flags |= ShaderFeatureFlags.LodCrossFade;
                    }
                }
            }

            if (s_ShaderFeatureCache.ContainsKey(shader) ||
                s_ShaderFeatureCache.Count < ShaderFeatureCacheCapacity)
            {
                s_ShaderFeatureCache[shader] = flags;
            }

            return flags;
        }

        private static bool ValidatePrefabInstance(GameObject root, bool hasHarvestTrigger, out string failure)
        {
            failure = string.Empty;
            if (root == null)
            {
                failure = "root is null.";
                return false;
            }

            if (root.GetComponent<MeshFilter>() != null)
            {
                failure = "root has MeshFilter.";
                return false;
            }

            LODGroup lodGroup = root.GetComponent<LODGroup>();
            if (lodGroup == null)
            {
                failure = "root lacks LODGroup.";
                return false;
            }

            LOD[] lods = lodGroup.GetLODs();
            if (lods == null || lods.Length != 3)
            {
                failure = "LODGroup must contain exactly 3 levels.";
                return false;
            }

            if (!ValidateLod2Material(lods, out failure))
                return false;

            root.GetComponentsInChildren(true, s_MeshColliderScratch);
            if (s_MeshColliderScratch.Count > 0)
            {
                failure = "MeshCollider exists in flora hierarchy.";
                s_MeshColliderScratch.Clear();
                return false;
            }
            s_MeshColliderScratch.Clear();

            if (hasHarvestTrigger)
            {
                Transform trigger = root.transform.Find("TRIG_HarvestNode");
                if (trigger == null ||
                    trigger.GetComponent<SphereCollider>() == null ||
                    trigger.GetComponent<ScavengeTarget>() == null)
                {
                    failure = "harvest metadata requested but TRIG_HarvestNode is incomplete.";
                    return false;
                }
            }

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

            return ValidatePrefabInstance(prefab, prefab.transform.Find("TRIG_HarvestNode") != null, out failure);
        }

        private static bool ValidateLod2Material(LOD[] lods, out string failure)
        {
            failure = string.Empty;
            if (lods.Length < 3 || lods[2].renderers == null || lods[2].renderers.Length == 0 || lods[2].renderers[0] == null)
            {
                failure = "LOD2 renderer is missing.";
                return false;
            }

            Material material = lods[2].renderers[0].sharedMaterial;
            if (material == null || !string.Equals(material.name, ImpostorMaterialName, StringComparison.Ordinal))
            {
                failure = "LOD2 material must be " + ImpostorMaterialName + ".";
                return false;
            }

            return true;
        }

        private static void AddViolation(FactoryReport report, string message)
        {
            if (report == null || string.IsNullOrEmpty(message))
                return;

            if (report.Violations.Count < MaxFactoryViolations)
                report.Violations.Add(message);
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

        private static void ClearScratch()
        {
            s_VertexScratch.Clear();
            s_UvScratch.Clear();
            s_ColorScratch.Clear();
            s_MeshColliderScratch.Clear();
            s_RendererScratch.Clear();
            s_MaterialScratch.Clear();
            s_TemplateScratch.Clear();
            s_ShaderFeatureCache.Clear();
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
                return "UnnamedFlora";

            char[] invalid = Path.GetInvalidFileNameChars();
            string safe = value.Trim().Replace(' ', '_');
            for (int i = 0; i < invalid.Length; i++)
                safe = safe.Replace(invalid[i], '_');
            return safe;
        }

        private static string NormalizeAssetFolder(string value, string fallback)
        {
            string normalized = string.IsNullOrWhiteSpace(value)
                ? fallback
                : value.Trim().Replace('\\', '/');
            while (normalized.IndexOf("//", StringComparison.Ordinal) >= 0)
                normalized = normalized.Replace("//", "/");
            normalized = normalized.TrimEnd('/');
            if (string.IsNullOrEmpty(normalized) ||
                normalized.IndexOf("..", StringComparison.Ordinal) >= 0 ||
                (!string.Equals(normalized, "Assets", StringComparison.Ordinal) &&
                 !normalized.StartsWith("Assets/", StringComparison.Ordinal)))
            {
                return fallback;
            }

            return normalized;
        }

        private static string ResolveFullPath(string projectRelativePath)
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string normalized = projectRelativePath.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
            return Path.GetFullPath(Path.Combine(projectRoot, normalized));
        }

        private static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        }

        private static bool IsFinite(Vector2 value)
        {
            return IsFinite(value.x) && IsFinite(value.y);
        }

        private static bool IsFinite(Color value)
        {
            return IsFinite(value.r) &&
                   IsFinite(value.g) &&
                   IsFinite(value.b) &&
                   IsFinite(value.a);
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

        private static int ComputeStableHash(string value)
        {
            unchecked
            {
                uint hash = 2166136261u;
                for (int i = 0; i < value.Length; i++)
                {
                    hash ^= value[i];
                    hash *= 16777619u;
                }

                return (int)(hash & 0x7fffffffu);
            }
        }

        [Serializable]
        public struct FactorySettings
        {
            public string MeshDirectory;
            public string MaterialDirectory;
            public string MetadataDirectory;
            public string OutputDirectory;
            public bool DryRun;
            public int MaxGroupsPerRun;

            public static FactorySettings Default => new FactorySettings
            {
                MeshDirectory = DefaultMeshDirectory,
                MaterialDirectory = DefaultMaterialDirectory,
                MetadataDirectory = DefaultMetadataDirectory,
                OutputDirectory = DefaultOutputDirectory,
                DryRun = true,
                MaxGroupsPerRun = 512
            };

            public FactorySettings Sanitize()
            {
                return new FactorySettings
                {
                    MeshDirectory = NormalizeAssetFolder(MeshDirectory, DefaultMeshDirectory),
                    MaterialDirectory = NormalizeAssetFolder(MaterialDirectory, DefaultMaterialDirectory),
                    MetadataDirectory = NormalizeAssetFolder(MetadataDirectory, DefaultMetadataDirectory),
                    OutputDirectory = NormalizeAssetFolder(OutputDirectory, DefaultOutputDirectory),
                    DryRun = DryRun,
                    MaxGroupsPerRun = Mathf.Clamp(MaxGroupsPerRun <= 0 ? 512 : MaxGroupsPerRun, 1, 4096)
                };
            }
        }

        [Serializable]
        public sealed class FactoryReport
        {
            public string AgentId;
            public string MeshDirectory;
            public string MaterialDirectory;
            public string MetadataDirectory;
            public string OutputDirectory;
            public bool DryRun;
            public int GroupsDiscovered;
            public int PrefabsAssembled;
            public int PrefabsFailed;
            public int LodGroupsValidated;
            public int RenderersValidated;
            public int HarvestTriggersValidated;
            public double ExecutionMicroseconds;
            public float BoundsVolumeCubicMeters;
            public List<string> Violations = new List<string>(MaxFactoryViolations);
        }

        [Flags]
        private enum ShaderFeatureFlags : byte
        {
            None = 0,
            UnityPerMaterialCbuffer = 1,
            LodCrossFade = 2
        }

        private sealed class FloraMeshGroup
        {
            public readonly string BaseName;
            public readonly Mesh[] Lods = new Mesh[3];
            public readonly string[] LodPaths = new string[3];

            public FloraMeshGroup(string baseName)
            {
                BaseName = baseName;
            }

            public bool HasRequiredLods => Lods[0] != null && Lods[1] != null && Lods[2] != null;
        }
    }
}
#endif
