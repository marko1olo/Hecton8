#if UNITY_EDITOR
// ============================================================================
// HECTON-8 - EquipmentPrefabFactory.cs
// Agent 1734 offline assembler for interactive tools and console prefabs.
// ============================================================================

namespace Hecton8.Editor.Assembly
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Globalization;
    using System.IO;
    using System.Reflection;
    using Hecton.Localization;
    using Hecton8.Interaction;
    using Hecton8.UI;
    using TMPro;
    using Unity.Collections.LowLevel.Unsafe;
    using Unity.Mathematics;
    using UnityEditor;
    using UnityEngine;
    using UnityEngine.Rendering;
    using UnityEngine.TextCore.LowLevel;
    using Debug = UnityEngine.Debug;
    using Object = UnityEngine.Object;

    public sealed class EquipmentPrefabFactory : EditorWindow
    {
        private const string AgentId = "1734";
        private const string DefaultMeshDirectory = "Assets/_Project/Art/Baked/Equipment";
        private const string DefaultAlternateMeshDirectory = "Assets/_Project/Art/Generated/Equipment";
        private const string DefaultMaterialDirectory = "Assets/_Project/Art/Materials";
        private const string DefaultMetadataDirectory = "Assets/_Project/Data/Equipment";
        private const string DefaultAlternateMetadataDirectory = "Assets/_Project/Prefabs/Equipment";
        private const string DefaultCollisionDirectory = "Assets/_Project/Art/Baked/Equipment";
        private const string DefaultAlternateCollisionDirectory = "Assets/_Project/Prefabs/Equipment";
        private const string DefaultFontDirectory = "Assets/_Project/Art/Materials/Fonts";
        private const string DefaultOutputDirectory = "Assets/Prefabs/Equipment";
        private const string InteractableLayerName = "Interactable";
        private const float TextSurfaceOffsetMeters = 0.0015f;
        private const float DefaultTextWidthMeters = 0.18f;
        private const float DefaultTextHeightMeters = 0.06f;
        private const float DefaultFontSizeMin = 0.025f;
        private const float DefaultFontSizeMax = 0.095f;
        private const float MinTextSurfaceExtentMeters = 0.01f;
        private const float MaxTextSurfaceExtentMeters = 2.0f;
        private const float TextPlaneOrthogonalityTolerance = 0.025f;
        private const float MaxTextPlaneLocalDistanceMeters = 25.0f;
        private const int MinimumMaterialResolveScore = 100;
        private const int MaxEquipmentLodSlots = 3;

        private static readonly List<Renderer> s_RendererScratch = new List<Renderer>(64);
        private static readonly List<MeshRenderer> s_MeshRendererScratch = new List<MeshRenderer>(64);
        private static readonly List<Collider> s_ColliderScratch = new List<Collider>(64);
        private static readonly List<MeshCollider> s_MeshColliderScratch = new List<MeshCollider>(4);
        private static readonly List<Canvas> s_CanvasScratch = new List<Canvas>(4);
        private static readonly List<CanvasRenderer> s_CanvasRendererScratch = new List<CanvasRenderer>(4);
        private static readonly List<TextMeshPro> s_TextScratch = new List<TextMeshPro>(32);
        private static readonly List<TextMeshProUGUI> s_TextUgUiScratch = new List<TextMeshProUGUI>(4);
        private static readonly List<Material> s_MaterialScratch = new List<Material>(128);
        private static readonly List<TMP_FontAsset> s_FontScratch = new List<TMP_FontAsset>(16);
        private static readonly LOD[] s_LodScratch = new LOD[MaxEquipmentLodSlots];
        private static readonly LOD[] s_Lod2Scratch = new LOD[2];
        private static readonly LOD[] s_Lod3Scratch = new LOD[3];
        private static readonly Renderer[] s_Lod0RendererScratch = new Renderer[1];
        private static readonly Renderer[] s_Lod1RendererScratch = new Renderer[1];
        private static readonly Renderer[] s_Lod2RendererScratch = new Renderer[1];
        private static readonly Renderer[][] s_LodRendererScratch =
        {
            s_Lod0RendererScratch,
            s_Lod1RendererScratch,
            s_Lod2RendererScratch
        };
        private static readonly string[] s_HotMethodNames =
        {
            "Tick",
            "FixedTick",
            "LateFrameTick",
            "Update",
            "FixedUpdate",
            "LateUpdate",
            "Execute"
        };

        private static readonly string[] s_HotMethodForbiddenTokens =
        {
            "GlobalRegistry.Get<",
            "GetComponent<",
            "GetComponent(",
            "GetComponents<",
            "GetComponents(",
            "GetComponentInChildren",
            "GetComponentInParent",
            "GetComponentsInChildren",
            "GetComponentsInParent",
            "TryGetComponent(",
            "GameObject.Find",
            "Object.Find",
            "FindObjectOfType",
            "FindFirstObjectByType",
            "FindObjectsOfType",
            "FindObjectsByType",
            "AddComponent(",
            ".Select(",
            ".Where(",
            ".Any(",
            "FirstOrDefault",
            "string.Format",
            ".ToString("
        };

        private static readonly string[] s_HotMethodForbiddenInvocationNames =
        {
            "GetComponent",
            "GetComponents",
            "GetComponentInChildren",
            "GetComponentInParent",
            "GetComponentsInChildren",
            "GetComponentsInParent",
            "TryGetComponent",
            "FindObjectOfType",
            "FindObjectsOfType",
            "FindFirstObjectByType",
            "FindObjectsByType",
            "AddComponent",
            "Select",
            "Where",
            "Any",
            "FirstOrDefault",
            "ToString",
            "WaitForCompletion"
        };

        [SerializeField] private string meshDirectory = DefaultMeshDirectory;
        [SerializeField] private string materialDirectory = DefaultMaterialDirectory;
        [SerializeField] private string metadataDirectory = DefaultMetadataDirectory;
        [SerializeField] private string collisionDirectory = DefaultCollisionDirectory;
        [SerializeField] private string fontDirectory = DefaultFontDirectory;
        [SerializeField] private string outputDirectory = DefaultOutputDirectory;
        [SerializeField] private bool dryRun = true;
        [SerializeField] private bool requireTextSurfaces = true;
        [SerializeField] private bool requireRuntimeScriptBindings = false;
        [SerializeField] private int maxGroupsPerRun = 256;

        private Vector2 scroll;
        private FactoryReport lastReport;

        [MenuItem("Hecton8/Assembly/Equipment Prefab Factory 1734")]
        public static void OpenWindow()
        {
            EquipmentPrefabFactory window = GetWindow<EquipmentPrefabFactory>("Equipment Factory 1734");
            window.minSize = new Vector2(720f, 520f);
            window.Show();
        }

        [MenuItem("Hecton8/Assembly/Dry Run Equipment Prefab Factory 1734")]
        public static void DryRunDefault()
        {
            FactorySettings settings = FactorySettings.Default;
            settings.DryRun = true;
            Run(settings);
        }

        [MenuItem("Hecton8/Assembly/Run Equipment Prefab Factory 1734")]
        public static void RunDefault()
        {
            FactorySettings settings = FactorySettings.Default;
            settings.DryRun = false;
            Run(settings);
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("HECTON-8 Equipment Prefab Factory 1734", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Offline-only assembly: generated equipment meshes, flat 3D TextMeshPro, SDF fonts, InteractionAnchorData, primitive COL_ proxies, and strict prefab validation.", MessageType.Info);

            meshDirectory = EditorGUILayout.TextField("Mesh Directory", meshDirectory);
            materialDirectory = EditorGUILayout.TextField("Material Directory", materialDirectory);
            metadataDirectory = EditorGUILayout.TextField("Metadata Directory", metadataDirectory);
            collisionDirectory = EditorGUILayout.TextField("Collision Directory", collisionDirectory);
            fontDirectory = EditorGUILayout.TextField("Font Directory", fontDirectory);
            outputDirectory = EditorGUILayout.TextField("Output Directory", outputDirectory);
            dryRun = EditorGUILayout.Toggle("Dry Run", dryRun);
            requireTextSurfaces = EditorGUILayout.Toggle("Require Text Surfaces", requireTextSurfaces);
            requireRuntimeScriptBindings = EditorGUILayout.Toggle("Require Runtime Script Bindings", requireRuntimeScriptBindings);
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
            EditorGUILayout.LabelField("Canvas Count", lastReport.CanvasComponentsFound.ToString(CultureInfo.InvariantCulture));
            EditorGUILayout.LabelField("TMP 3D Count", lastReport.TextMeshPro3DCount.ToString(CultureInfo.InvariantCulture));
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
                CollisionDirectory = collisionDirectory,
                FontDirectory = fontDirectory,
                OutputDirectory = outputDirectory,
                DryRun = dryRunOverride,
                RequireTextSurfaces = requireTextSurfaces,
                RequireRuntimeScriptBindings = requireRuntimeScriptBindings,
                MaxGroupsPerRun = maxGroupsPerRun
            }.Sanitize();
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
                CollisionDirectory = settings.CollisionDirectory,
                FontDirectory = settings.FontDirectory,
                OutputDirectory = settings.OutputDirectory,
                DryRun = settings.DryRun
            };

            try
            {
                if (!EquipmentMetadata.ValidateStaticLayout())
                {
                    AddViolation(report, "FATAL: InteractionAnchorData unmanaged layout invalid.");
                    return report;
                }

                report.CanvasComponentsFound = CountExistingCanvasComponents(settings.OutputDirectory);
                report.ExistingEquipmentPrefabCount = CountExistingPrefabs(settings.OutputDirectory);

                TMP_FontAsset primaryFont = ResolvePrimarySdfFont(settings.FontDirectory, report);
                if (primaryFont == null)
                    AddViolation(report, "FATAL: No 1729 SDF TMP_FontAsset found under " + settings.FontDirectory + " or project font fallback folders.");
                else if (!ValidateSdfFallbackCoverage(primaryFont, out string fallbackFailure))
                {
                    AddViolation(report, "FATAL: " + fallbackFailure);
                    primaryFont = null;
                }

                MaterialPalette palette = MaterialPalette.Build(settings.MaterialDirectory, report);
                int interactableLayer = LayerMask.NameToLayer(InteractableLayerName);
                if (interactableLayer < 0)
                    AddViolation(report, "FATAL: Missing required layer: " + InteractableLayerName);

                Dictionary<string, EquipmentMeshGroup> groups = DiscoverMeshGroups(settings, report);
                report.GroupsDiscovered = groups.Count;
                if (groups.Count == 0)
                    AddViolation(report, "No generated equipment mesh groups found in " + settings.MeshDirectory + " or " + DefaultAlternateMeshDirectory + ".");

                if (!settings.DryRun)
                    EnsureAssetFolder(settings.OutputDirectory);

                int processed = 0;
                foreach (KeyValuePair<string, EquipmentMeshGroup> pair in groups)
                {
                    if (processed >= settings.MaxGroupsPerRun)
                        break;

                    processed++;
                    AssembleGroup(pair.Value, palette, primaryFont, interactableLayer, settings, report);
                }
            }
            catch (Exception exception)
            {
                AddViolation(report, "FATAL: EquipmentPrefabFactory exception: " + exception.GetType().Name + " " + exception.Message);
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

            Debug.Log("[EquipmentPrefabFactory1734] Completed. Groups=" + report.GroupsDiscovered +
                      " Assembled=" + report.PrefabsAssembled +
                      " DryRun=" + report.PrefabsDryRunPassed +
                      " Failed=" + report.PrefabsFailed +
                      " us=" + report.ExecutionMicroseconds.ToString(CultureInfo.InvariantCulture));
            return report;
        }

        private static void AssembleGroup(
            EquipmentMeshGroup group,
            MaterialPalette palette,
            TMP_FontAsset primaryFont,
            int interactableLayer,
            FactorySettings settings,
            FactoryReport report)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            GroupReport groupReport = new GroupReport
            {
                EquipmentName = group.EquipmentName,
                SourceMesh = group.PrimaryMeshPath,
                OutputPrefab = BuildOutputPath(settings.OutputDirectory, group.EquipmentName)
            };

            GameObject root = null;
            try
            {
                if (primaryFont == null)
                {
                    FailGroup(groupReport, "Missing SDF TMP font.");
                    return;
                }

                if (interactableLayer < 0)
                {
                    FailGroup(groupReport, "Interactable layer missing.");
                    return;
                }

                if (group.PrimaryMesh == null)
                {
                    FailGroup(groupReport, "Missing primary LOD0/detail mesh.");
                    return;
                }

                if (!TryLoadAuthoringMetadata(group, settings, out EquipmentAuthoringData authoringData, out string metadataFailure))
                {
                    FailGroup(groupReport, metadataFailure);
                    return;
                }

                if (settings.RequireTextSurfaces && (authoringData.TextSurfaces == null || authoringData.TextSurfaces.Length == 0))
                {
                    FailGroup(groupReport, "No text surface metadata found.");
                    return;
                }

                if (!ValidateTextSurfaceSet(authoringData.TextSurfaces, out string textSurfaceFailure))
                {
                    FailGroup(groupReport, textSurfaceFailure);
                    return;
                }

                if (!EquipmentMetadata.ValidateAnchorSet(authoringData.Anchors, out string anchorFailure))
                {
                    FailGroup(groupReport, anchorFailure);
                    return;
                }

                root = new GameObject("PFB_" + SanitizeAssetName(group.EquipmentName));
                ResetLocalTransform(root.transform);
                root.layer = interactableLayer;

                EquipmentMetadata metadata = root.AddComponent<EquipmentMetadata>();
                metadata.SetEditorBakeData(
                    authoringData.EquipmentId != 0u ? authoringData.EquipmentId : HashString(group.EquipmentName),
                    authoringData.BakeHash != 0u ? authoringData.BakeHash : HashAuthoringBake(group, authoringData),
                    authoringData.GlobalQualityWeight,
                    authoringData.Anchors);
                groupReport.AnchorCount = authoringData.Anchors.Length;

                Renderer primaryRenderer = CreateVisualHierarchy(root.transform, group, palette, authoringData, report, groupReport);
                if (primaryRenderer == null)
                {
                    FailGroup(groupReport, "Visual hierarchy construction failed.");
                    return;
                }

                TextMeshPro[] textComponents = CreateTextSurfaces(root.transform, authoringData, primaryFont, report, groupReport);
                report.TextMeshPro3DCount += textComponents.Length;

                if (!AttachCollisionProxy(root, group, settings, interactableLayer, report, groupReport))
                {
                    FailGroup(groupReport, string.IsNullOrEmpty(groupReport.Failure) ? "Collision proxy attachment failed." : groupReport.Failure);
                    return;
                }

                BindRuntimeComponents(root, group, authoringData, textComponents, primaryRenderer, settings, report, groupReport);
                if (settings.RequireRuntimeScriptBindings && groupReport.RuntimeComponentsBound == 0)
                {
                    FailGroup(groupReport, "No runtime component binding succeeded.");
                    return;
                }

                if (!ValidatePrefabInstance(root, primaryFont, interactableLayer, report, groupReport, out string validationFailure))
                {
                    FailGroup(groupReport, validationFailure);
                    return;
                }

                if (settings.DryRun)
                {
                    groupReport.Status = "DRY_RUN_OK";
                    report.PrefabsDryRunPassed++;
                    return;
                }

                GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(root, groupReport.OutputPrefab, out bool success);
                if (!success || savedPrefab == null)
                {
                    AssetDatabase.DeleteAsset(groupReport.OutputPrefab);
                    FailGroup(groupReport, "PrefabUtility.SaveAsPrefabAsset returned null or success=false.");
                    return;
                }

                if (!ValidateSavedPrefab(groupReport.OutputPrefab, primaryFont, interactableLayer, report, groupReport, out validationFailure))
                {
                    AssetDatabase.DeleteAsset(groupReport.OutputPrefab);
                    FailGroup(groupReport, validationFailure);
                    return;
                }

                groupReport.Status = "ASSEMBLED";
                groupReport.Saved = true;
                report.PrefabsAssembled++;
            }
            catch (Exception exception)
            {
                AssetDatabase.DeleteAsset(groupReport.OutputPrefab);
                FailGroup(groupReport, exception.GetType().Name + ": " + exception.Message);
                Debug.LogError("Equipment Assembly Violation Detected! " + group.EquipmentName + " " + groupReport.Failure);
            }
            finally
            {
                stopwatch.Stop();
                groupReport.ElapsedMicroseconds = TicksToMicroseconds(stopwatch.ElapsedTicks);
                if (groupReport.ElapsedMicroseconds <= 0)
                    groupReport.ElapsedMicroseconds = 1;
                report.GroupReports.Add(groupReport);
                if (root != null)
                    Object.DestroyImmediate(root);
            }

            void FailGroup(GroupReport target, string failure)
            {
                target.Status = "FAILED";
                target.Failure = failure;
                AddViolation(report, group.EquipmentName + ": " + failure);
            }
        }

        private static Renderer CreateVisualHierarchy(
            Transform root,
            EquipmentMeshGroup group,
            MaterialPalette palette,
            EquipmentAuthoringData authoringData,
            FactoryReport report,
            GroupReport groupReport)
        {
            Material material = palette.ResolveMaterial(group.EquipmentName, authoringData.MaterialName);
            if (material == null)
            {
                groupReport.Failure = "No shared material resolved.";
                AddViolation(report, group.EquipmentName + ": " + groupReport.Failure);
                return null;
            }

            GameObject visualRoot = new GameObject("VIS_" + group.EquipmentName);
            visualRoot.transform.SetParent(root, false);
            ResetLocalTransform(visualRoot.transform);

            Renderer primaryRenderer = null;
            for (int i = 0; i < group.Lods.Length; i++)
            {
                Mesh lodMesh = group.Lods[i];
                if (lodMesh == null)
                    continue;

                MeshRenderer renderer = CreateMeshChild(visualRoot.transform, "LOD" + i, lodMesh, material);
                if (primaryRenderer == null)
                    primaryRenderer = renderer;
                groupReport.RendererCount++;
            }

            for (int i = 0; i < group.DetailMeshes.Count; i++)
            {
                Mesh detailMesh = group.DetailMeshes[i];
                if (detailMesh == null)
                    continue;

                MeshRenderer renderer = CreateMeshChild(visualRoot.transform, "DETAIL_" + i.ToString(CultureInfo.InvariantCulture), detailMesh, material);
                if (primaryRenderer == null)
                    primaryRenderer = renderer;
                groupReport.RendererCount++;
            }

            BuildLodGroupIfPresent(visualRoot, group, groupReport);
            return primaryRenderer;
        }

        private static MeshRenderer CreateMeshChild(Transform parent, string name, Mesh mesh, Material material)
        {
            GameObject child = new GameObject(name);
            child.transform.SetParent(parent, false);
            ResetLocalTransform(child.transform);

            MeshFilter filter = child.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;

            MeshRenderer renderer = child.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = BuildSharedMaterialSlots(mesh, material);
            renderer.shadowCastingMode = ShadowCastingMode.On;
            renderer.receiveShadows = true;
            renderer.motionVectorGenerationMode = MotionVectorGenerationMode.Object;
            renderer.lightProbeUsage = LightProbeUsage.BlendProbes;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.BlendProbes;
            return renderer;
        }

        private static void BuildLodGroupIfPresent(GameObject visualRoot, EquipmentMeshGroup group, GroupReport groupReport)
        {
            int lodCount = 0;
            for (int i = 0; i < group.Lods.Length; i++)
            {
                if (group.Lods[i] != null)
                    lodCount++;
            }

            if (lodCount <= 1)
                return;

            visualRoot.GetComponentsInChildren(true, s_MeshRendererScratch);
            int lodWriteIndex = 0;
            for (int lodIndex = 0; lodIndex < group.Lods.Length && lodIndex < MaxEquipmentLodSlots; lodIndex++)
            {
                Renderer renderer = FindLodRenderer(lodIndex);
                if (renderer == null)
                    continue;

                Renderer[] renderers = s_LodRendererScratch[lodWriteIndex];
                renderers[0] = renderer;
                float height = lodIndex == 0 ? 0.55f : lodIndex == 1 ? 0.22f : 0.06f;
                s_LodScratch[lodWriteIndex] = new LOD(height, renderers);
                lodWriteIndex++;
            }

            if (lodWriteIndex > 1)
            {
                LODGroup lodGroup = visualRoot.AddComponent<LODGroup>();
                lodGroup.fadeMode = LODFadeMode.CrossFade;
                lodGroup.animateCrossFading = true;
                lodGroup.SetLODs(CopyLodScratchToExactBuffer(lodWriteIndex));
                lodGroup.RecalculateBounds();
                groupReport.LodGroupConfigured = true;
            }

            ClearLodScratch();
            s_MeshRendererScratch.Clear();
        }

        private static Renderer FindLodRenderer(int lodIndex)
        {
            for (int rendererIndex = 0; rendererIndex < s_MeshRendererScratch.Count; rendererIndex++)
            {
                MeshRenderer renderer = s_MeshRendererScratch[rendererIndex];
                if (renderer != null && IsLodRendererName(renderer.name, lodIndex))
                    return renderer;
            }

            return null;
        }

        private static bool IsLodRendererName(string rendererName, int lodIndex)
        {
            if (string.IsNullOrEmpty(rendererName))
                return false;

            if (rendererName.Length != 4 || rendererName[0] != 'L' || rendererName[1] != 'O' || rendererName[2] != 'D')
                return false;

            return rendererName[3] == (char)('0' + lodIndex);
        }

        private static void ClearLodScratch()
        {
            for (int i = 0; i < s_LodScratch.Length; i++)
            {
                s_LodScratch[i] = default;
                Renderer[] renderers = s_LodRendererScratch[i];
                renderers[0] = null;
            }

            for (int i = 0; i < s_Lod2Scratch.Length; i++)
                s_Lod2Scratch[i] = default;
            for (int i = 0; i < s_Lod3Scratch.Length; i++)
                s_Lod3Scratch[i] = default;
        }

        private static LOD[] CopyLodScratchToExactBuffer(int lodCount)
        {
            LOD[] target = lodCount == 2 ? s_Lod2Scratch : s_Lod3Scratch;
            for (int i = 0; i < lodCount && i < target.Length; i++)
                target[i] = s_LodScratch[i];
            return target;
        }

        private static TextMeshPro[] CreateTextSurfaces(
            Transform root,
            EquipmentAuthoringData authoringData,
            TMP_FontAsset primaryFont,
            FactoryReport report,
            GroupReport groupReport)
        {
            TextSurfaceData[] surfaces = authoringData.TextSurfaces ?? Array.Empty<TextSurfaceData>();
            TextMeshPro[] texts = new TextMeshPro[surfaces.Length];
            for (int i = 0; i < surfaces.Length; i++)
            {
                TextSurfaceData surface = surfaces[i];
                GameObject textObject = new GameObject("UI_Text_" + SanitizeAssetName(surface.Name));
                textObject.transform.SetParent(root, false);
                ApplySurfaceTransform(textObject.transform, surface);

                TextMeshPro text = textObject.AddComponent<TextMeshPro>();
                text.font = primaryFont;
                text.fontSharedMaterial = primaryFont.material;
                text.text = string.IsNullOrEmpty(surface.Text) ? surface.Name : surface.Text;
                text.alignment = TextAlignmentOptions.Center;
                text.raycastTarget = false;
                text.richText = false;
                text.parseCtrlCharacters = false;
                text.overflowMode = TextOverflowModes.Truncate;
                text.textWrappingMode = TextWrappingModes.NoWrap;
                text.enableAutoSizing = true;
                text.fontSizeMin = surface.FontSizeMin > 0f ? surface.FontSizeMin : DefaultFontSizeMin;
                text.fontSizeMax = math.max(text.fontSizeMin, surface.FontSizeMax > 0f ? surface.FontSizeMax : DefaultFontSizeMax);
                text.enableCulling = true;
                text.margin = new Vector4(0.005f, 0.005f, 0.005f, 0.005f);
                TMP_TextRegistry.EnsureRegistered(text);
                text.ForceMeshUpdate(ignoreActiveState: true, forceTextReparsing: true);

                texts[i] = text;
                groupReport.TextSurfaceCount++;
            }

            return texts;
        }

        private static void ApplySurfaceTransform(Transform transform, TextSurfaceData surface)
        {
            float3 normal = NormalizeOr(surface.Normal, new float3(0f, 0f, 1f));
            float3 up = ResolveOrthonormalUp(normal, surface.Up);
            float width = surface.WidthMeters > 0f ? surface.WidthMeters : DefaultTextWidthMeters;
            float height = surface.HeightMeters > 0f ? surface.HeightMeters : DefaultTextHeightMeters;

            transform.localPosition = ToVector3(surface.LocalPosition + normal * TextSurfaceOffsetMeters);
            transform.localRotation = Quaternion.LookRotation(ToVector3(normal), ToVector3(up));
            transform.localScale = new Vector3(width, height, 1f);
        }

        private static bool AttachCollisionProxy(
            GameObject root,
            EquipmentMeshGroup group,
            FactorySettings settings,
            int interactableLayer,
            FactoryReport report,
            GroupReport groupReport)
        {
            GameObject proxySource = ResolveCollisionProxyPrefab(group, settings);
            if (proxySource != null)
            {
                GameObject instance = PrefabUtility.InstantiatePrefab(proxySource) as GameObject;
                if (instance == null)
                {
                    groupReport.Failure = "Failed to instantiate COL_ proxy prefab.";
                    return false;
                }

                instance.name = "COL_" + group.EquipmentName;
                instance.transform.SetParent(root.transform, false);
                ResetLocalTransform(instance.transform);
                AssignLayerRecursive(instance, interactableLayer);
                groupReport.CollisionProxy = AssetDatabase.GetAssetPath(proxySource);
                return ValidateCollisionProxy(instance, interactableLayer, report, groupReport);
            }

            GameObject sourcePrefab = ResolveSourcePrefab(group);
            if (sourcePrefab != null && CopyPrimitiveCollidersFromSourcePrefab(sourcePrefab, root.transform, interactableLayer, report, groupReport))
                return true;

            groupReport.Failure = "Missing COL_" + group.EquipmentName + " primitive proxy.";
            return false;
        }

        private static bool CopyPrimitiveCollidersFromSourcePrefab(
            GameObject sourcePrefab,
            Transform root,
            int interactableLayer,
            FactoryReport report,
            GroupReport groupReport)
        {
            sourcePrefab.GetComponentsInChildren(true, s_ColliderScratch);
            if (s_ColliderScratch.Count == 0)
                return false;

            GameObject proxyRoot = new GameObject("COL_" + groupReport.EquipmentName);
            proxyRoot.transform.SetParent(root, false);
            ResetLocalTransform(proxyRoot.transform);
            proxyRoot.layer = interactableLayer;

            for (int i = 0; i < s_ColliderScratch.Count; i++)
            {
                Collider source = s_ColliderScratch[i];
                if (source == null)
                    continue;

                if (!IsPrimitiveCollider(source))
                {
                    Object.DestroyImmediate(proxyRoot);
                    s_ColliderScratch.Clear();
                    groupReport.Failure = "Source prefab contains non-primitive collider: " + source.GetType().Name;
                    return false;
                }

                GameObject child = new GameObject(source.name);
                child.layer = interactableLayer;
                child.transform.SetParent(proxyRoot.transform, false);
                child.transform.localPosition = source.transform.localPosition;
                child.transform.localRotation = source.transform.localRotation;
                child.transform.localScale = source.transform.localScale;
                CopyCollider(source, child);
                groupReport.ColliderCount++;
            }

            s_ColliderScratch.Clear();
            groupReport.CollisionProxy = AssetDatabase.GetAssetPath(sourcePrefab) + "#copiedPrimitiveColliders";
            return ValidateCollisionProxy(proxyRoot, interactableLayer, report, groupReport);
        }

        private static void CopyCollider(Collider source, GameObject target)
        {
            if (source is BoxCollider box)
            {
                BoxCollider copy = target.AddComponent<BoxCollider>();
                copy.center = box.center;
                copy.size = box.size;
                copy.isTrigger = box.isTrigger;
                copy.sharedMaterial = box.sharedMaterial;
                return;
            }

            if (source is CapsuleCollider capsule)
            {
                CapsuleCollider copy = target.AddComponent<CapsuleCollider>();
                copy.center = capsule.center;
                copy.radius = capsule.radius;
                copy.height = capsule.height;
                copy.direction = capsule.direction;
                copy.isTrigger = capsule.isTrigger;
                copy.sharedMaterial = capsule.sharedMaterial;
                return;
            }

            if (source is SphereCollider sphere)
            {
                SphereCollider copy = target.AddComponent<SphereCollider>();
                copy.center = sphere.center;
                copy.radius = sphere.radius;
                copy.isTrigger = sphere.isTrigger;
                copy.sharedMaterial = sphere.sharedMaterial;
            }
        }

        private static bool ValidateCollisionProxy(GameObject proxyRoot, int interactableLayer, FactoryReport report, GroupReport groupReport)
        {
            proxyRoot.GetComponentsInChildren(true, s_ColliderScratch);
            if (s_ColliderScratch.Count == 0)
            {
                s_ColliderScratch.Clear();
                groupReport.Failure = "Collision proxy has no colliders.";
                return false;
            }

            int primitiveCount = 0;
            for (int i = 0; i < s_ColliderScratch.Count; i++)
            {
                Collider collider = s_ColliderScratch[i];
                if (collider == null)
                    continue;

                if (!IsPrimitiveCollider(collider))
                {
                    s_ColliderScratch.Clear();
                    groupReport.Failure = "Collision proxy contains non-primitive collider: " + collider.GetType().Name;
                    return false;
                }

                if (collider.gameObject.layer != interactableLayer)
                {
                    s_ColliderScratch.Clear();
                    groupReport.Failure = collider.name + " is not on Interactable layer.";
                    return false;
                }

                primitiveCount++;
            }

            groupReport.ColliderCount = primitiveCount;
            report.PrimitiveColliderCount += primitiveCount;
            s_ColliderScratch.Clear();
            return true;
        }

        private static void BindRuntimeComponents(
            GameObject root,
            EquipmentMeshGroup group,
            EquipmentAuthoringData authoringData,
            TextMeshPro[] texts,
            Renderer primaryRenderer,
            FactorySettings settings,
            FactoryReport report,
            GroupReport groupReport)
        {
            RuntimeComponentData[] components = authoringData.RuntimeComponents ?? Array.Empty<RuntimeComponentData>();
            for (int i = 0; i < components.Length; i++)
            {
                RuntimeComponentData componentData = components[i];
                string componentTypeName = componentData.ResolvedTypeName;
                Type type = ResolveComponentType(componentTypeName);
                if (type == null || !typeof(Component).IsAssignableFrom(type) || type.IsAbstract || type.ContainsGenericParameters)
                {
                    AddViolation(report, group.EquipmentName + ": runtime component type unresolved: " + componentTypeName);
                    continue;
                }

                Component component = root.GetComponent(type);
                if (component == null)
                    component = root.AddComponent(type);
                if (component == null)
                    continue;

                if (!ValidateRuntimeComponentSource(component, out string sourceFailure))
                {
                    Object.DestroyImmediate(component);
                    AddViolation(report, group.EquipmentName + ": runtime component rejected: " + sourceFailure);
                    continue;
                }

                int referencesBound = BindSerializedReferences(component, componentData, texts, primaryRenderer);
                groupReport.RuntimeComponentsBound++;
                groupReport.SerializedReferencesBound += referencesBound;
                EditorUtility.SetDirty(component);
            }
        }

        private static int BindSerializedReferences(
            Component component,
            RuntimeComponentData componentData,
            TextMeshPro[] texts,
            Renderer primaryRenderer)
        {
            int bound = 0;
            Type type = component.GetType();

            string[] primaryTextFields = componentData.ResolvedPrimaryTextFields;
            for (int i = 0; i < primaryTextFields.Length && texts.Length > 0; i++)
                bound += TrySetField(type, component, primaryTextFields[i], texts[0]) ? 1 : 0;

            string[] secondaryTextFields = componentData.ResolvedSecondaryTextFields;
            for (int i = 0; i < secondaryTextFields.Length && texts.Length > 1; i++)
                bound += TrySetField(type, component, secondaryTextFields[i], texts[1]) ? 1 : 0;

            string[] allTextFields = componentData.ResolvedTextFields;
            for (int i = 0; i < allTextFields.Length && i < texts.Length; i++)
                bound += TrySetField(type, component, allTextFields[i], texts[i]) ? 1 : 0;

            string[] rendererFields = componentData.ResolvedRendererFields;
            for (int i = 0; i < rendererFields.Length && primaryRenderer != null; i++)
                bound += TrySetField(type, component, rendererFields[i], primaryRenderer) ? 1 : 0;

            if (texts.Length > 0)
            {
                bound += TrySetField(type, component, "_primaryLabel", texts[0]) ? 1 : 0;
                bound += TrySetField(type, component, "primaryLabel", texts[0]) ? 1 : 0;
            }

            if (texts.Length > 1)
            {
                bound += TrySetField(type, component, "_secondaryLabel", texts[1]) ? 1 : 0;
                bound += TrySetField(type, component, "secondaryLabel", texts[1]) ? 1 : 0;
            }

            if (primaryRenderer != null)
            {
                bound += TrySetField(type, component, "_screenRenderer", primaryRenderer) ? 1 : 0;
                bound += TrySetField(type, component, "screenRenderer", primaryRenderer) ? 1 : 0;
            }

            return bound;
        }

        private static bool TrySetField(Type type, object instance, string fieldName, Object value)
        {
            if (string.IsNullOrWhiteSpace(fieldName) || value == null)
                return false;

            FieldInfo field = type.GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field == null || !field.FieldType.IsAssignableFrom(value.GetType()))
                return false;

            field.SetValue(instance, value);
            return true;
        }

        private static bool ValidateRuntimeComponentSource(Component component, out string failure)
        {
            failure = string.Empty;
            if (component == null)
                return true;

            MonoBehaviour behaviour = component as MonoBehaviour;
            if (behaviour == null)
                return true;

            string typeName = component.GetType().FullName;
            MonoScript script = MonoScript.FromMonoBehaviour(behaviour);
            if (script == null)
            {
                failure = typeName + " has no MonoScript source asset.";
                return false;
            }

            string path = AssetDatabase.GetAssetPath(script);
            if (string.IsNullOrEmpty(path) || !path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
            {
                failure = typeName + " has no project C# source path.";
                return false;
            }

            string fullPath = ResolveFullPath(path);
            if (!File.Exists(fullPath))
            {
                failure = typeName + " source file is missing: " + path;
                return false;
            }

            if (!ContainsForbiddenHotMethodTokenInRuntimeTypeSources(component.GetType(), path, out string violatingPath, out string proof))
                return true;

            failure = typeName + " has hot-path source violation in " + violatingPath + ": " + proof;
            return false;
        }

        private static bool ContainsForbiddenHotMethodTokenInRuntimeTypeSources(
            Type componentType,
            string primaryAssetPath,
            out string violatingPath,
            out string proof)
        {
            violatingPath = string.Empty;
            proof = string.Empty;
            if (componentType == null || string.IsNullOrEmpty(primaryAssetPath))
                return false;

            string primaryFullPath = ResolveFullPath(primaryAssetPath);
            if (File.Exists(primaryFullPath))
            {
                string primarySource = StripCommentsAndStringLiteralsForScan(File.ReadAllText(primaryFullPath));
                if (ContainsForbiddenHotMethodToken(primarySource, out proof))
                {
                    violatingPath = primaryAssetPath;
                    return true;
                }
            }

            string[] scriptGuids = AssetDatabase.FindAssets("t:MonoScript", new[] { "Assets" });
            for (int i = 0; i < scriptGuids.Length; i++)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(scriptGuids[i]);
                if (string.IsNullOrEmpty(assetPath) ||
                    !assetPath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(assetPath, primaryAssetPath, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string fullPath = ResolveFullPath(assetPath);
                if (!File.Exists(fullPath))
                    continue;

                string source = StripCommentsAndStringLiteralsForScan(File.ReadAllText(fullPath));
                if (!IsPotentialPartialTypeSource(source, componentType))
                    continue;

                if (ContainsForbiddenHotMethodToken(source, out proof))
                {
                    violatingPath = assetPath;
                    return true;
                }
            }

            return false;
        }

        private static bool IsPotentialPartialTypeSource(string source, Type componentType)
        {
            if (string.IsNullOrEmpty(source) || componentType == null)
                return false;

            string typeName = componentType.Name;
            if (string.IsNullOrEmpty(typeName) ||
                source.IndexOf("partial", StringComparison.Ordinal) < 0)
            {
                return false;
            }

            int typeIndex = source.IndexOf(typeName, StringComparison.Ordinal);
            while (typeIndex >= 0)
            {
                if (HasIdentifierBoundary(source, typeIndex - 1) &&
                    HasIdentifierBoundary(source, typeIndex + typeName.Length) &&
                    HasDeclarationKeywordBefore(source, typeIndex, "partial") &&
                    (HasDeclarationKeywordBefore(source, typeIndex, "class") ||
                     HasDeclarationKeywordBefore(source, typeIndex, "struct")))
                {
                    return true;
                }

                typeIndex = source.IndexOf(typeName, typeIndex + typeName.Length, StringComparison.Ordinal);
            }

            return false;
        }

        private static bool HasDeclarationKeywordBefore(string source, int typeIndex, string keyword)
        {
            int start = math.max(0, typeIndex - 160);
            int keywordIndex = source.IndexOf(keyword, start, typeIndex - start, StringComparison.Ordinal);
            while (keywordIndex >= 0)
            {
                if (HasIdentifierBoundary(source, keywordIndex - 1) &&
                    HasIdentifierBoundary(source, keywordIndex + keyword.Length))
                {
                    return true;
                }

                int nextStart = keywordIndex + keyword.Length;
                if (nextStart >= typeIndex)
                    return false;

                keywordIndex = source.IndexOf(keyword, nextStart, typeIndex - nextStart, StringComparison.Ordinal);
            }

            return false;
        }

        private static bool ContainsForbiddenHotMethodToken(string source, out string proof)
        {
            proof = string.Empty;
            if (string.IsNullOrEmpty(source))
                return false;

            for (int methodIndex = 0; methodIndex < s_HotMethodNames.Length; methodIndex++)
            {
                string methodName = s_HotMethodNames[methodIndex];
                int searchIndex = 0;
                while (TryFindMethodBody(source, methodName, searchIndex, out int bodyStart, out int bodyEnd, out int nextSearchIndex))
                {
                    searchIndex = nextSearchIndex;
                    for (int tokenIndex = 0; tokenIndex < s_HotMethodForbiddenTokens.Length; tokenIndex++)
                    {
                        string token = s_HotMethodForbiddenTokens[tokenIndex];
                        int tokenIndexInBody = source.IndexOf(token, bodyStart, bodyEnd - bodyStart, StringComparison.Ordinal);
                        if (tokenIndexInBody >= 0)
                        {
                            proof = methodName + " contains " + token;
                            return true;
                        }
                    }

                    if (ContainsForbiddenHotInvocation(source, bodyStart, bodyEnd, out string invocationProof))
                    {
                        proof = methodName + " contains " + invocationProof;
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool ContainsForbiddenHotInvocation(string source, int bodyStart, int bodyEnd, out string proof)
        {
            proof = string.Empty;
            for (int nameIndex = 0; nameIndex < s_HotMethodForbiddenInvocationNames.Length; nameIndex++)
            {
                string invocationName = s_HotMethodForbiddenInvocationNames[nameIndex];
                int searchIndex = bodyStart;
                while (searchIndex < bodyEnd)
                {
                    int invocationIndex = source.IndexOf(invocationName, searchIndex, bodyEnd - searchIndex, StringComparison.Ordinal);
                    if (invocationIndex < 0)
                        break;

                    searchIndex = invocationIndex + invocationName.Length;
                    if (!HasIdentifierBoundary(source, invocationIndex - 1) ||
                        !HasIdentifierBoundary(source, invocationIndex + invocationName.Length))
                    {
                        continue;
                    }

                    int marker = SkipWhitespace(source, invocationIndex + invocationName.Length);
                    if (marker < bodyEnd && (source[marker] == '(' || source[marker] == '<'))
                    {
                        proof = invocationName + " invocation";
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool TryFindMethodBody(
            string source,
            string methodName,
            int startIndex,
            out int bodyStart,
            out int bodyEnd,
            out int nextSearchIndex)
        {
            bodyStart = -1;
            bodyEnd = -1;
            nextSearchIndex = source.Length;
            int nameIndex = source.IndexOf(methodName, startIndex, StringComparison.Ordinal);
            while (nameIndex >= 0)
            {
                nextSearchIndex = nameIndex + methodName.Length;
                if (!HasIdentifierBoundary(source, nameIndex - 1) ||
                    !HasIdentifierBoundary(source, nameIndex + methodName.Length))
                {
                    nameIndex = source.IndexOf(methodName, nextSearchIndex, StringComparison.Ordinal);
                    continue;
                }

                int parenIndex = SkipWhitespace(source, nameIndex + methodName.Length);
                if (parenIndex >= source.Length || source[parenIndex] != '(')
                {
                    nameIndex = source.IndexOf(methodName, nextSearchIndex, StringComparison.Ordinal);
                    continue;
                }

                int closeParen = FindMatchingParen(source, parenIndex);
                if (closeParen < 0)
                    return false;

                int bodyMarker = SkipWhitespace(source, closeParen + 1);
                if (bodyMarker >= source.Length)
                    return false;

                if (source[bodyMarker] == '=' && bodyMarker + 1 < source.Length && source[bodyMarker + 1] == '>')
                {
                    int expressionStart = SkipWhitespace(source, bodyMarker + 2);
                    int semicolon = source.IndexOf(';', expressionStart);
                    if (semicolon < 0)
                        return false;

                    bodyStart = expressionStart;
                    bodyEnd = semicolon;
                    nextSearchIndex = semicolon + 1;
                    return true;
                }

                if (source[bodyMarker] != '{')
                {
                    nameIndex = source.IndexOf(methodName, nextSearchIndex, StringComparison.Ordinal);
                    continue;
                }

                int closeBrace = FindMatchingBrace(source, bodyMarker);
                if (closeBrace < 0)
                    return false;

                bodyStart = bodyMarker + 1;
                bodyEnd = closeBrace;
                nextSearchIndex = closeBrace + 1;
                return true;
            }

            return false;
        }

        private static string StripCommentsAndStringLiteralsForScan(string source)
        {
            if (string.IsNullOrEmpty(source))
                return string.Empty;

            char[] buffer = source.ToCharArray();
            bool lineComment = false;
            bool blockComment = false;
            bool stringLiteral = false;
            bool verbatimString = false;
            bool charLiteral = false;

            for (int i = 0; i < buffer.Length; i++)
            {
                char current = buffer[i];
                char next = i + 1 < buffer.Length ? buffer[i + 1] : '\0';

                if (lineComment)
                {
                    if (current == '\r' || current == '\n')
                    {
                        lineComment = false;
                        continue;
                    }

                    buffer[i] = ' ';
                    continue;
                }

                if (blockComment)
                {
                    if (current == '*' && next == '/')
                    {
                        buffer[i] = ' ';
                        buffer[i + 1] = ' ';
                        i++;
                        blockComment = false;
                        continue;
                    }

                    if (current != '\r' && current != '\n')
                        buffer[i] = ' ';
                    continue;
                }

                if (stringLiteral)
                {
                    if (verbatimString)
                    {
                        if (current == '"' && next == '"')
                        {
                            buffer[i] = ' ';
                            buffer[i + 1] = ' ';
                            i++;
                            continue;
                        }

                        if (current == '"')
                        {
                            buffer[i] = ' ';
                            stringLiteral = false;
                            verbatimString = false;
                            continue;
                        }
                    }
                    else if (current == '\\')
                    {
                        buffer[i] = ' ';
                        if (i + 1 < buffer.Length)
                        {
                            buffer[i + 1] = ' ';
                            i++;
                        }

                        continue;
                    }
                    else if (current == '"')
                    {
                        buffer[i] = ' ';
                        stringLiteral = false;
                        continue;
                    }

                    if (current != '\r' && current != '\n')
                        buffer[i] = ' ';
                    continue;
                }

                if (charLiteral)
                {
                    if (current == '\\')
                    {
                        buffer[i] = ' ';
                        if (i + 1 < buffer.Length)
                        {
                            buffer[i + 1] = ' ';
                            i++;
                        }

                        continue;
                    }

                    if (current == '\'')
                    {
                        buffer[i] = ' ';
                        charLiteral = false;
                        continue;
                    }

                    if (current != '\r' && current != '\n')
                        buffer[i] = ' ';
                    continue;
                }

                if (current == '/' && next == '/')
                {
                    buffer[i] = ' ';
                    buffer[i + 1] = ' ';
                    i++;
                    lineComment = true;
                    continue;
                }

                if (current == '/' && next == '*')
                {
                    buffer[i] = ' ';
                    buffer[i + 1] = ' ';
                    i++;
                    blockComment = true;
                    continue;
                }

                if (current == '@' && next == '"')
                {
                    buffer[i] = ' ';
                    buffer[i + 1] = ' ';
                    i++;
                    stringLiteral = true;
                    verbatimString = true;
                    continue;
                }

                if (current == '"')
                {
                    buffer[i] = ' ';
                    stringLiteral = true;
                    verbatimString = false;
                    continue;
                }

                if (current == '\'')
                {
                    buffer[i] = ' ';
                    charLiteral = true;
                }
            }

            return new string(buffer);
        }

        private static bool HasIdentifierBoundary(string source, int index)
        {
            if (index < 0 || index >= source.Length)
                return true;

            char value = source[index];
            return !char.IsLetterOrDigit(value) && value != '_';
        }

        private static int SkipWhitespace(string source, int index)
        {
            while (index < source.Length && char.IsWhiteSpace(source[index]))
                index++;
            return index;
        }

        private static int FindMatchingBrace(string source, int openBrace)
        {
            int depth = 0;
            for (int i = openBrace; i < source.Length; i++)
            {
                char value = source[i];
                if (value == '{')
                {
                    depth++;
                    continue;
                }

                if (value == '}')
                {
                    depth--;
                    if (depth == 0)
                        return i;
                }
            }

            return -1;
        }

        private static int FindMatchingParen(string source, int openParen)
        {
            int depth = 0;
            for (int i = openParen; i < source.Length; i++)
            {
                char value = source[i];
                if (value == '(')
                {
                    depth++;
                    continue;
                }

                if (value == ')')
                {
                    depth--;
                    if (depth == 0)
                        return i;
                }
            }

            return -1;
        }

        private static bool ValidateSavedPrefab(
            string prefabPath,
            TMP_FontAsset primaryFont,
            int interactableLayer,
            FactoryReport report,
            GroupReport groupReport,
            out string failure)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
            {
                failure = "Saved prefab cannot be loaded: " + prefabPath;
                return false;
            }

            return ValidatePrefabInstance(prefab, primaryFont, interactableLayer, report, groupReport, out failure);
        }

        private static bool ValidatePrefabInstance(
            GameObject root,
            TMP_FontAsset primaryFont,
            int interactableLayer,
            FactoryReport report,
            GroupReport groupReport,
            out string failure)
        {
            failure = string.Empty;
            if (root == null)
            {
                failure = "Prefab root is null.";
                return false;
            }

            if (root.GetComponent<MeshCollider>() != null)
            {
                failure = "Root MeshCollider is forbidden.";
                return false;
            }

            EquipmentMetadata metadata = root.GetComponent<EquipmentMetadata>();
            if (metadata == null)
            {
                failure = "EquipmentMetadata missing on root.";
                return false;
            }

            if (!ValidateVisualMeshTransforms(root, out failure))
                return false;

            InteractionAnchorData[] anchors = CopyAnchors(metadata);
            if (!EquipmentMetadata.ValidateAnchorSet(anchors, out string anchorFailure))
            {
                failure = "EquipmentMetadata anchor set invalid: " + anchorFailure;
                return false;
            }

            root.GetComponentsInChildren(true, s_CanvasScratch);
            if (s_CanvasScratch.Count != 0)
            {
                failure = "Canvas hierarchy detected.";
                s_CanvasScratch.Clear();
                return false;
            }
            s_CanvasScratch.Clear();

            root.GetComponentsInChildren(true, s_CanvasRendererScratch);
            if (s_CanvasRendererScratch.Count != 0)
            {
                failure = "CanvasRenderer hierarchy detected.";
                s_CanvasRendererScratch.Clear();
                return false;
            }
            s_CanvasRendererScratch.Clear();

            root.GetComponentsInChildren(true, s_TextUgUiScratch);
            if (s_TextUgUiScratch.Count != 0)
            {
                failure = "TextMeshProUGUI hierarchy detected.";
                s_TextUgUiScratch.Clear();
                return false;
            }
            s_TextUgUiScratch.Clear();

            root.GetComponentsInChildren(true, s_TextScratch);
            if (s_TextScratch.Count == 0)
            {
                failure = "No direct 3D TextMeshPro components were injected.";
                s_TextScratch.Clear();
                return false;
            }

            for (int i = 0; i < s_TextScratch.Count; i++)
            {
                TextMeshPro text = s_TextScratch[i];
                if (text == null)
                    continue;

                if (text.transform.parent != root.transform)
                {
                    failure = text.name + " is not a direct root child.";
                    s_TextScratch.Clear();
                    return false;
                }

                if (text.GetComponent<CanvasRenderer>() != null)
                {
                    failure = text.name + " has CanvasRenderer.";
                    s_TextScratch.Clear();
                    return false;
                }

                if (text.font == null || !IsSdfFontAsset(text.font))
                {
                    failure = text.name + " does not use a 1729 SDF TMP font.";
                    s_TextScratch.Clear();
                    return false;
                }

                if (primaryFont != null && text.font != primaryFont && !FontHasFallback(primaryFont, text.font))
                {
                    failure = text.name + " does not use primary SDF font or its fallback chain.";
                    s_TextScratch.Clear();
                    return false;
                }

                if (text.raycastTarget)
                {
                    failure = text.name + " has raycastTarget=true.";
                    s_TextScratch.Clear();
                    return false;
                }

                if (!IsFinite(text.transform.localPosition) ||
                    !IsFinite(text.transform.localRotation) ||
                    !IsFinite(text.transform.localScale))
                {
                    failure = text.name + " has non-finite text plane transform.";
                    s_TextScratch.Clear();
                    return false;
                }

                string textPlaneFailure = string.Empty;
                if (!ValidateTextPlaneTransform(text.transform, out textPlaneFailure))
                {
                    failure = text.name + " has invalid text plane transform: " + textPlaneFailure;
                    s_TextScratch.Clear();
                    return false;
                }

                if (!ValidateSrpBatcherMaterial(text.fontSharedMaterial, true, out string textMaterialProof))
                {
                    failure = text.name + " font material rejected: " + textMaterialProof;
                    s_TextScratch.Clear();
                    return false;
                }

            }
            s_TextScratch.Clear();

            root.GetComponentsInChildren(true, s_MeshColliderScratch);
            if (s_MeshColliderScratch.Count != 0)
            {
                failure = "MeshCollider exists in equipment hierarchy.";
                s_MeshColliderScratch.Clear();
                return false;
            }
            s_MeshColliderScratch.Clear();

            root.GetComponentsInChildren(true, s_ColliderScratch);
            if (s_ColliderScratch.Count == 0)
            {
                failure = "No primitive interaction colliders found.";
                s_ColliderScratch.Clear();
                return false;
            }

            for (int i = 0; i < s_ColliderScratch.Count; i++)
            {
                Collider collider = s_ColliderScratch[i];
                if (collider == null)
                    continue;

                if (!IsPrimitiveCollider(collider))
                {
                    failure = collider.name + " is not a primitive collider.";
                    s_ColliderScratch.Clear();
                    return false;
                }

                if (collider.gameObject.layer != interactableLayer)
                {
                    failure = collider.name + " is not on Interactable layer.";
                    s_ColliderScratch.Clear();
                    return false;
                }
            }
            s_ColliderScratch.Clear();

            root.GetComponentsInChildren(true, s_RendererScratch);
            for (int i = 0; i < s_RendererScratch.Count; i++)
            {
                Renderer renderer = s_RendererScratch[i];
                if (renderer == null)
                    continue;

                bool rendererIsTextMeshPro = renderer.GetComponent<TextMeshPro>() != null;
                Material[] materials = renderer.sharedMaterials;
                if (materials == null || materials.Length == 0)
                {
                    failure = renderer.name + " has no shared materials.";
                    s_RendererScratch.Clear();
                    return false;
                }

                for (int materialIndex = 0; materialIndex < materials.Length; materialIndex++)
                {
                    Material material = materials[materialIndex];
                    if (!ValidateSrpBatcherMaterial(material, rendererIsTextMeshPro, out string materialProof))
                    {
                        failure = renderer.name + " material rejected: " + materialProof;
                        s_RendererScratch.Clear();
                        return false;
                    }

                }
            }
            s_RendererScratch.Clear();

            groupReport.ValidatorPasses++;
            report.PrefabValidatorPasses++;
            return true;
        }

        private static bool ValidateTextSurfaceSet(TextSurfaceData[] surfaces, out string failure)
        {
            failure = string.Empty;
            if (surfaces == null || surfaces.Length == 0)
                return true;

            for (int i = 0; i < surfaces.Length; i++)
            {
                if (!ValidateTextSurface(surfaces[i], i, out failure))
                    return false;
            }

            return true;
        }

        private static bool ValidateTextSurface(TextSurfaceData surface, int index, out string failure)
        {
            failure = string.Empty;
            float3 normal = surface.Normal;
            float3 up = surface.Up;
            float normalLengthSq = math.lengthsq(normal);
            float upLengthSq = math.lengthsq(up);
            if (!math.all(math.isfinite(surface.LocalPosition)) ||
                !math.all(math.isfinite(normal)) ||
                !math.all(math.isfinite(up)) ||
                !math.isfinite(normalLengthSq) ||
                !math.isfinite(upLengthSq) ||
                normalLengthSq <= 0.000001f ||
                upLengthSq <= 0.000001f)
            {
                failure = "Text surface " + index.ToString(CultureInfo.InvariantCulture) + " has non-finite or degenerate plane vectors.";
                return false;
            }

            float3 n = normal * math.rsqrt(normalLengthSq);
            float3 u = up * math.rsqrt(upLengthSq);
            if (math.abs(math.dot(n, u)) > TextPlaneOrthogonalityTolerance)
            {
                failure = "Text surface " + index.ToString(CultureInfo.InvariantCulture) + " normal/up axes are not orthogonal.";
                return false;
            }

            if (!IsTextSurfaceExtentValid(surface.WidthMeters) ||
                !IsTextSurfaceExtentValid(surface.HeightMeters))
            {
                failure = "Text surface " + index.ToString(CultureInfo.InvariantCulture) + " has invalid physical extents.";
                return false;
            }

            if (!IsFinite(surface.FontSizeMin) ||
                !IsFinite(surface.FontSizeMax) ||
                surface.FontSizeMin <= 0f ||
                surface.FontSizeMax < surface.FontSizeMin)
            {
                failure = "Text surface " + index.ToString(CultureInfo.InvariantCulture) + " has invalid TMP autosize bounds.";
                return false;
            }

            return true;
        }

        private static bool ValidateTextPlaneTransform(Transform transform, out string failure)
        {
            failure = string.Empty;
            Vector3 localPosition = transform.localPosition;
            Vector3 localScale = transform.localScale;
            if (localPosition.sqrMagnitude > MaxTextPlaneLocalDistanceMeters * MaxTextPlaneLocalDistanceMeters)
            {
                failure = "local position is outside authored equipment bounds.";
                return false;
            }

            if (!IsTextSurfaceExtentValid(localScale.x) ||
                !IsTextSurfaceExtentValid(localScale.y) ||
                math.abs(localScale.z - 1f) > 0.0001f)
            {
                failure = "local scale is not a bounded flat XY text plane.";
                return false;
            }

            Vector3 forward = transform.localRotation * Vector3.forward;
            Vector3 up = transform.localRotation * Vector3.up;
            if (!IsFinite(forward) ||
                !IsFinite(up) ||
                forward.sqrMagnitude <= 0.000001f ||
                up.sqrMagnitude <= 0.000001f ||
                math.abs(Vector3.Dot(forward.normalized, up.normalized)) > TextPlaneOrthogonalityTolerance)
            {
                failure = "local rotation does not define a stable orthogonal text plane.";
                return false;
            }

            return true;
        }

        private static bool ValidateVisualMeshTransforms(GameObject root, out string failure)
        {
            failure = string.Empty;
            Transform rootTransform = root.transform;
            if (!IsZeroPosition(rootTransform.localPosition) ||
                !IsIdentityRotation(rootTransform.localRotation) ||
                !IsOneScale(rootTransform.localScale))
            {
                failure = root.name + " root transform is not identity-aligned.";
                return false;
            }

            root.GetComponentsInChildren(true, s_MeshRendererScratch);
            for (int i = 0; i < s_MeshRendererScratch.Count; i++)
            {
                MeshRenderer renderer = s_MeshRendererScratch[i];
                if (renderer == null || renderer.GetComponent<TextMeshPro>() != null)
                    continue;

                Transform rendererTransform = renderer.transform;
                if (!IsEquipmentVisualRenderer(rendererTransform))
                    continue;

                if (!IsZeroPosition(rendererTransform.localPosition) ||
                    !IsIdentityRotation(rendererTransform.localRotation) ||
                    !IsOneScale(rendererTransform.localScale))
                {
                    failure = renderer.name + " visual mesh transform is not identity-aligned.";
                    s_MeshRendererScratch.Clear();
                    return false;
                }
            }

            s_MeshRendererScratch.Clear();
            return true;
        }

        private static bool IsEquipmentVisualRenderer(Transform rendererTransform)
        {
            if (rendererTransform == null)
                return false;

            string name = rendererTransform.name;
            if (!name.StartsWith("LOD", StringComparison.Ordinal) &&
                !name.StartsWith("DETAIL_", StringComparison.Ordinal))
            {
                return false;
            }

            Transform parent = rendererTransform.parent;
            return parent != null && parent.name.StartsWith("VIS_", StringComparison.Ordinal);
        }

        private static Dictionary<string, EquipmentMeshGroup> DiscoverMeshGroups(FactorySettings settings, FactoryReport report)
        {
            Dictionary<string, EquipmentMeshGroup> groups = new Dictionary<string, EquipmentMeshGroup>(128, StringComparer.OrdinalIgnoreCase);
            string[] roots = ResolveExistingRoots(settings.MeshDirectory, DefaultAlternateMeshDirectory);
            if (roots.Length == 0)
                return groups;

            string[] guids = AssetDatabase.FindAssets("t:Mesh", roots);
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (IsCollisionName(Path.GetFileNameWithoutExtension(path)))
                    continue;

                Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if (mesh == null)
                    continue;

                string baseName = NormalizeEquipmentName(Path.GetFileNameWithoutExtension(path), out int lodIndex, out bool isDetail);
                if (string.IsNullOrWhiteSpace(baseName))
                    continue;

                if (!groups.TryGetValue(baseName, out EquipmentMeshGroup group))
                {
                    group = new EquipmentMeshGroup(baseName);
                    groups.Add(baseName, group);
                }

                if (lodIndex >= 0 && lodIndex < group.Lods.Length)
                {
                    if (group.Lods[lodIndex] == null)
                    {
                        group.Lods[lodIndex] = mesh;
                        group.LodPaths[lodIndex] = path;
                    }
                    else
                    {
                        report.Violations.Add(baseName + ": duplicate LOD" + lodIndex.ToString(CultureInfo.InvariantCulture) + " mesh: " + path);
                    }
                }
                else if (isDetail)
                {
                    group.DetailMeshes.Add(mesh);
                    group.DetailMeshPaths.Add(path);
                }
                else if (group.Lods[0] == null)
                {
                    group.Lods[0] = mesh;
                    group.LodPaths[0] = path;
                }
                else
                {
                    group.DetailMeshes.Add(mesh);
                    group.DetailMeshPaths.Add(path);
                }
            }

            return groups;
        }

        private static bool TryLoadAuthoringMetadata(
            EquipmentMeshGroup group,
            FactorySettings settings,
            out EquipmentAuthoringData data,
            out string failure)
        {
            data = default;
            failure = string.Empty;

            if (TryLoadJsonMetadata(group, settings, out data, out string jsonPath))
            {
                data.SourcePath = jsonPath;
                return true;
            }

            if (TryLoadBinaryMetadata(group, settings, out data, out string binaryPath))
            {
                data.SourcePath = binaryPath;
                return true;
            }

            GameObject sourcePrefab = ResolveSourcePrefab(group);
            if (sourcePrefab != null && TryLoadPrefabMetadata(sourcePrefab, out data))
            {
                data.SourcePath = AssetDatabase.GetAssetPath(sourcePrefab);
                return true;
            }

            failure = "Missing JSON/binary/prefab metadata for anchors and text surfaces.";
            return false;
        }

        private static bool TryLoadJsonMetadata(
            EquipmentMeshGroup group,
            FactorySettings settings,
            out EquipmentAuthoringData data,
            out string metadataPath)
        {
            data = default;
            metadataPath = string.Empty;
            string[] roots = ResolveExistingRoots(settings.MetadataDirectory, DefaultMetadataDirectory);
            if (roots.Length == 0)
                return false;

            string[] guids = AssetDatabase.FindAssets(group.EquipmentName, roots);
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (!path.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                    continue;

                string json = File.ReadAllText(ResolveFullPath(path));
                EquipmentMetadataFile metadata = JsonUtility.FromJson<EquipmentMetadataFile>(json);
                if (metadata == null)
                    continue;

                data = BuildAuthoringData(group, metadata);
                metadataPath = path;
                return true;
            }

            return false;
        }

        private static bool TryLoadBinaryMetadata(
            EquipmentMeshGroup group,
            FactorySettings settings,
            out EquipmentAuthoringData data,
            out string metadataPath)
        {
            data = default;
            metadataPath = string.Empty;
            string[] roots = ResolveExistingRoots(settings.MetadataDirectory, DefaultMetadataDirectory);
            if (roots.Length == 0)
                return false;

            string[] guids = AssetDatabase.FindAssets(group.EquipmentName, roots);
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (!path.EndsWith(".bytes", StringComparison.OrdinalIgnoreCase) &&
                    !path.EndsWith(".bin", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                byte[] bytes = File.ReadAllBytes(ResolveFullPath(path));
                if (TryParseBinaryMetadata(group, bytes, out data))
                {
                    metadataPath = path;
                    return true;
                }
            }

            return false;
        }

        private static bool TryParseBinaryMetadata(EquipmentMeshGroup group, byte[] bytes, out EquipmentAuthoringData data)
        {
            data = default;
            if (bytes == null || bytes.Length < 20)
                return false;

            int offset = 0;
            int anchorCount = ReadInt32(bytes, ref offset);
            if (anchorCount <= 0 || anchorCount > 64)
                return false;

            int anchorStride = UnsafeUtility.SizeOf<InteractionAnchorData>();
            int compactAnchorStride = 56;
            int selectedAnchorStride = ResolveBinaryAnchorStride(bytes, offset, anchorCount, anchorStride, compactAnchorStride);
            if (selectedAnchorStride <= 0)
                return false;

            InteractionAnchorData[] anchors = new InteractionAnchorData[anchorCount];
            for (int i = 0; i < anchorCount; i++)
            {
                if (offset + compactAnchorStride > bytes.Length ||
                    selectedAnchorStride < compactAnchorStride ||
                    offset + selectedAnchorStride > bytes.Length)
                {
                    return false;
                }

                int anchorStartOffset = offset;
                anchors[i] = new InteractionAnchorData
                {
                    LocalPosition = ReadFloat3(bytes, ref offset),
                    LocalForward = NormalizeOr(ReadFloat3(bytes, ref offset), new float3(0f, 0f, 1f)),
                    LocalUp = NormalizeOr(ReadFloat3(bytes, ref offset), new float3(0f, 1f, 0f)),
                    SnapRadiusMeters = ReadSingle(bytes, ref offset),
                    AnchorId = ReadUInt32(bytes, ref offset),
                    Flags = ReadUInt32(bytes, ref offset),
                    HandMask = ReadByte(bytes, ref offset),
                    SurfaceKind = ReadByte(bytes, ref offset)
                };
                offset = anchorStartOffset + selectedAnchorStride;
            }

            TextSurfaceData[] surfaces = Array.Empty<TextSurfaceData>();
            if (offset + 4 <= bytes.Length)
            {
                int surfaceCount = ReadInt32(bytes, ref offset);
                if (surfaceCount > 0 && surfaceCount <= 128)
                {
                    surfaces = new TextSurfaceData[surfaceCount];
                    for (int i = 0; i < surfaceCount; i++)
                    {
                        if (offset + 44 > bytes.Length)
                            return false;

                        surfaces[i] = new TextSurfaceData
                        {
                            Name = "Surface_" + i.ToString(CultureInfo.InvariantCulture),
                            Text = "STATUS",
                            LocalPosition = ReadFloat3(bytes, ref offset),
                            Normal = NormalizeOr(ReadFloat3(bytes, ref offset), new float3(0f, 0f, 1f)),
                            Up = NormalizeOr(ReadFloat3(bytes, ref offset), new float3(0f, 1f, 0f)),
                            WidthMeters = math.max(0.01f, ReadSingle(bytes, ref offset)),
                            HeightMeters = math.max(0.01f, ReadSingle(bytes, ref offset)),
                            FontSizeMin = DefaultFontSizeMin,
                            FontSizeMax = DefaultFontSizeMax
                        };
                    }
                }
            }

            data = new EquipmentAuthoringData
            {
                EquipmentId = HashString(group.EquipmentName),
                BakeHash = HashString(group.PrimaryMeshPath),
                GlobalQualityWeight = 1f,
                Anchors = anchors,
                TextSurfaces = surfaces,
                RuntimeComponents = Array.Empty<RuntimeComponentData>(),
                SourcePath = "binary"
            };
            return true;
        }

        private static int ResolveBinaryAnchorStride(byte[] bytes, int anchorStartOffset, int anchorCount, int rawStride, int compactStride)
        {
            int rawEnd = anchorStartOffset + rawStride * anchorCount;
            int compactEnd = anchorStartOffset + compactStride * anchorCount;
            bool rawValid = rawStride >= compactStride && rawEnd <= bytes.Length && HasPlausibleSurfaceBlock(bytes, rawEnd);
            bool compactValid = compactEnd <= bytes.Length && HasPlausibleSurfaceBlock(bytes, compactEnd);
            if (rawValid)
                return rawStride;
            if (compactValid)
                return compactStride;
            return 0;
        }

        private static bool HasPlausibleSurfaceBlock(byte[] bytes, int offset)
        {
            if (offset == bytes.Length)
                return true;
            if (offset < 0 || offset + 4 > bytes.Length)
                return false;

            int surfaceCount = BitConverter.ToInt32(bytes, offset);
            if (surfaceCount < 0 || surfaceCount > 128)
                return false;

            const int surfaceStride = 44;
            int payloadStart = offset + 4;
            return payloadStart + surfaceCount * surfaceStride <= bytes.Length;
        }

        private static bool TryLoadPrefabMetadata(GameObject sourcePrefab, out EquipmentAuthoringData data)
        {
            data = default;
            EquipmentMetadata metadata = sourcePrefab.GetComponent<EquipmentMetadata>();
            if (metadata == null)
                return false;

            InteractionAnchorData[] anchors = CopyAnchors(metadata);
            data = new EquipmentAuthoringData
            {
                EquipmentId = metadata.EquipmentId,
                BakeHash = metadata.BakeHash,
                GlobalQualityWeight = metadata.AuthoredQualityWeight,
                Anchors = anchors,
                TextSurfaces = Array.Empty<TextSurfaceData>(),
                RuntimeComponents = Array.Empty<RuntimeComponentData>(),
                MaterialName = string.Empty
            };
            return true;
        }

        private static EquipmentAuthoringData BuildAuthoringData(EquipmentMeshGroup group, EquipmentMetadataFile metadata)
        {
            AnchorRecord[] anchorRecords = metadata.anchors ?? Array.Empty<AnchorRecord>();
            InteractionAnchorData[] anchors = new InteractionAnchorData[anchorRecords.Length];
            for (int i = 0; i < anchorRecords.Length; i++)
                anchors[i] = BuildAnchor(anchorRecords[i], i);

            TextSurfaceRecord[] surfaceRecords = metadata.textSurfaces ?? metadata.surfaces ?? Array.Empty<TextSurfaceRecord>();
            TextSurfaceData[] surfaces = new TextSurfaceData[surfaceRecords.Length];
            for (int i = 0; i < surfaceRecords.Length; i++)
                surfaces[i] = BuildTextSurface(surfaceRecords[i], i);

            return new EquipmentAuthoringData
            {
                EquipmentId = metadata.equipmentId != 0u ? metadata.equipmentId : HashString(string.IsNullOrEmpty(metadata.equipmentName) ? group.EquipmentName : metadata.equipmentName),
                BakeHash = metadata.bakeHash,
                GlobalQualityWeight = math.saturate(math.isfinite(metadata.globalQualityWeight) ? metadata.globalQualityWeight : 1f),
                Anchors = anchors,
                TextSurfaces = surfaces,
                RuntimeComponents = metadata.runtimeComponents ?? Array.Empty<RuntimeComponentData>(),
                MaterialName = metadata.materialName
            };
        }

        private static InteractionAnchorData BuildAnchor(AnchorRecord record, int index)
        {
            string key = !string.IsNullOrEmpty(record.id) ? record.id : !string.IsNullOrEmpty(record.name) ? record.name : "ANCHOR_" + index.ToString(CultureInfo.InvariantCulture);
            float3 forward = SelectVector(record.forward, record.normal, new float3(0f, 0f, 1f));
            float3 up = SelectVector(record.up, default, ResolveOrthonormalUp(forward, new float3(0f, 1f, 0f)));
            uint flags = record.flags;
            if (flags == 0u || record.active)
                flags |= InteractionAnchorData.FlagActive;
            if (record.twoHanded)
                flags |= InteractionAnchorData.FlagTwoHanded;

            return new InteractionAnchorData
            {
                LocalPosition = SelectPosition(record.localPosition, record.position),
                LocalForward = NormalizeOr(forward, new float3(0f, 0f, 1f)),
                LocalUp = ResolveOrthonormalUp(forward, up),
                SnapRadiusMeters = record.snapRadiusMeters > 0f ? record.snapRadiusMeters : record.snapRadius > 0f ? record.snapRadius : 0.06f,
                AnchorId = record.anchorId != 0u ? record.anchorId : HashString(key),
                Flags = flags,
                HandMask = ResolveHandMask(record),
                SurfaceKind = ResolveSurfaceKind(record)
            };
        }

        private static TextSurfaceData BuildTextSurface(TextSurfaceRecord record, int index)
        {
            string name = !string.IsNullOrEmpty(record.name) ? record.name : "Surface_" + index.ToString(CultureInfo.InvariantCulture);
            float width = record.widthMeters > 0f ? record.widthMeters : record.width > 0f ? record.width : DefaultTextWidthMeters;
            float height = record.heightMeters > 0f ? record.heightMeters : record.height > 0f ? record.height : DefaultTextHeightMeters;
            float3 normal = SelectVector(record.normal, record.forward, new float3(0f, 0f, 1f));
            float3 up = SelectVector(record.up, default, new float3(0f, 1f, 0f));

            return new TextSurfaceData
            {
                Name = name,
                Text = string.IsNullOrEmpty(record.text) ? name : record.text,
                LocalPosition = SelectPosition(record.localPosition, record.position),
                Normal = NormalizeOr(normal, new float3(0f, 0f, 1f)),
                Up = ResolveOrthonormalUp(normal, up),
                WidthMeters = width,
                HeightMeters = height,
                FontSizeMin = record.fontSizeMin > 0f ? record.fontSizeMin : DefaultFontSizeMin,
                FontSizeMax = record.fontSizeMax > 0f ? record.fontSizeMax : DefaultFontSizeMax
            };
        }

        private static TMP_FontAsset ResolvePrimarySdfFont(string fontDirectory, FactoryReport report)
        {
            s_FontScratch.Clear();
            string[] roots = ResolveExistingRoots(fontDirectory, DefaultFontDirectory, "Assets/_Project/Art/Generated/SdfFontAtlas1729", "Assets/_Project/Data");
            if (roots.Length == 0)
                return null;

            string[] guids = AssetDatabase.FindAssets("t:TMP_FontAsset", roots);
            TMP_FontAsset best = null;
            int bestScore = int.MinValue;
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
                if (font == null || !IsSdfFontAsset(font))
                    continue;

                s_FontScratch.Add(font);
                int score = ScoreFont(font, path);
                if (score > bestScore)
                {
                    bestScore = score;
                    best = font;
                }
            }

            if (best != null)
            {
                report.PrimaryFontAsset = AssetDatabase.GetAssetPath(best);
                report.FontFallbackCount = best.fallbackFontAssetTable != null ? best.fallbackFontAssetTable.Count : 0;
            }

            return best;
        }

        private static bool ValidateSdfFallbackCoverage(TMP_FontAsset primaryFont, out string failure)
        {
            failure = string.Empty;
            if (primaryFont == null)
            {
                failure = "Primary SDF font is null.";
                return false;
            }

            bool arabicAvailable = false;
            bool arabicCovered = IsArabicFont(primaryFont);
            bool cjkAvailable = false;
            bool cjkCovered = IsCjkFont(primaryFont);
            for (int i = 0; i < s_FontScratch.Count; i++)
            {
                TMP_FontAsset font = s_FontScratch[i];
                if (font == null || font == primaryFont)
                    continue;

                if (IsArabicFont(font))
                {
                    arabicAvailable = true;
                    arabicCovered |= FontChainContains(primaryFont, font, 0);
                }

                if (IsCjkFont(font))
                {
                    cjkAvailable = true;
                    cjkCovered |= FontChainContains(primaryFont, font, 0);
                }
            }

            if (arabicAvailable && !arabicCovered)
            {
                failure = primaryFont.name + " is missing Arabic SDF fallback coverage.";
                return false;
            }

            if (cjkAvailable && !cjkCovered)
            {
                failure = primaryFont.name + " is missing CJK SDF fallback coverage.";
                return false;
            }

            return true;
        }

        private static bool FontChainContains(TMP_FontAsset primary, TMP_FontAsset candidate, int depth)
        {
            if (primary == null || candidate == null || depth > 8)
                return false;

            List<TMP_FontAsset> fallback = primary.fallbackFontAssetTable;
            if (fallback == null)
                return false;

            for (int i = 0; i < fallback.Count; i++)
            {
                TMP_FontAsset font = fallback[i];
                if (font == null)
                    continue;

                if (font == candidate || FontChainContains(font, candidate, depth + 1))
                    return true;
            }

            return false;
        }

        private static int ScoreFont(TMP_FontAsset font, string path)
        {
            int score = 0;
            string name = font.name;
            if (ContainsIgnoreCase(name, "NotoSans-Regular"))
                score += 1000;
            if (ContainsIgnoreCase(name, "h8") || ContainsIgnoreCase(path, "SdfFontAtlas1729"))
                score += 500;
            if (ContainsIgnoreCase(name, "SDF"))
                score += 100;
            if (font.fallbackFontAssetTable != null)
                score += font.fallbackFontAssetTable.Count * 20;
            if (ContainsIgnoreCase(name, "Arabic") || ContainsIgnoreCase(name, "CJK"))
                score -= 10;
            return score;
        }

        private static GameObject ResolveCollisionProxyPrefab(EquipmentMeshGroup group, FactorySettings settings)
        {
            string[] roots = ResolveExistingRoots(settings.CollisionDirectory, DefaultAlternateCollisionDirectory);
            if (roots.Length == 0)
                return null;

            string[] queries =
            {
                "COL_" + group.EquipmentName + " t:Prefab",
                group.EquipmentName + " COL_ t:Prefab"
            };

            for (int queryIndex = 0; queryIndex < queries.Length; queryIndex++)
            {
                string[] guids = AssetDatabase.FindAssets(queries[queryIndex], roots);
                for (int i = 0; i < guids.Length; i++)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                    GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (prefab != null && ContainsIgnoreCase(prefab.name, "COL"))
                        return prefab;
                }
            }

            return null;
        }

        private static GameObject ResolveSourcePrefab(EquipmentMeshGroup group)
        {
            string[] roots = ResolveExistingRoots(DefaultAlternateMetadataDirectory, DefaultAlternateCollisionDirectory);
            if (roots.Length == 0)
                return null;

            string[] guids = AssetDatabase.FindAssets(group.EquipmentName + " t:Prefab", roots);
            GameObject best = null;
            int bestScore = int.MinValue;
            string groupKey = NormalizeSearch(group.EquipmentName);
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null)
                    continue;

                int score = ScoreNameMatch(groupKey, NormalizeSearch(prefab.name));
                if (score > bestScore)
                {
                    best = prefab;
                    bestScore = score;
                }
            }

            return best;
        }

        private static string[] ResolveExistingRoots(params string[] candidates)
        {
            List<string> roots = new List<string>(candidates.Length);
            for (int i = 0; i < candidates.Length; i++)
            {
                string candidate = NormalizeAssetPath(candidates[i]);
                if (string.IsNullOrWhiteSpace(candidate))
                    continue;

                if (AssetDatabase.IsValidFolder(candidate) && !roots.Contains(candidate))
                    roots.Add(candidate);
            }

            return roots.ToArray();
        }

        private static Material[] BuildSharedMaterialSlots(Mesh mesh, Material material)
        {
            int subMeshCount = Mathf.Max(1, mesh != null ? mesh.subMeshCount : 1);
            Material[] slots = new Material[subMeshCount];
            for (int i = 0; i < slots.Length; i++)
                slots[i] = material;
            return slots;
        }

        private static bool ValidateSrpBatcherMaterial(Material material, bool allowTransparent, out string proof)
        {
            proof = "material is null";
            if (material == null)
                return false;

            if (!AssetDatabase.Contains(material))
            {
                proof = material.name + " is not an asset-backed shared material.";
                return false;
            }

            Shader shader = material.shader;
            if (shader == null)
            {
                proof = material.name + " has no shader.";
                return false;
            }

            if (!allowTransparent &&
                (material.IsKeywordEnabled("_ALPHABLEND_ON") ||
                 material.IsKeywordEnabled("_SURFACE_TYPE_TRANSPARENT") ||
                 material.renderQueue >= 3000))
            {
                proof = material.name + " is transparent.";
                return false;
            }

            string shaderName = shader.name;
            if (shaderName.IndexOf("Universal Render Pipeline/Lit", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                proof = "URP Lit built-in UnityPerMaterial CBUFFER";
                return true;
            }

            string shaderPath = AssetDatabase.GetAssetPath(shader);
            if (string.IsNullOrEmpty(shaderPath))
            {
                proof = shaderName + " has no shader asset path.";
                return false;
            }

            if (shaderPath.EndsWith(".shadergraph", StringComparison.OrdinalIgnoreCase))
            {
                proof = "ShaderGraph SRP batcher path";
                return true;
            }

            string fullPath = ResolveFullPath(shaderPath);
            if (!File.Exists(fullPath))
            {
                proof = shaderPath + " not found on disk.";
                return false;
            }

            string source = File.ReadAllText(fullPath);
            if (source.IndexOf("CBUFFER_START(UnityPerMaterial)", StringComparison.Ordinal) >= 0 ||
                source.IndexOf("UnityPerMaterial", StringComparison.Ordinal) >= 0)
            {
                proof = "shader source declares UnityPerMaterial";
                return true;
            }

            proof = shaderName + " lacks UnityPerMaterial CBUFFER proof.";
            return false;
        }

        private static bool IsSdfFontAsset(TMP_FontAsset font)
        {
            if (font == null)
                return false;

            string name = font.name;
            string path = AssetDatabase.GetAssetPath(font);
            return ContainsIgnoreCase(name, "SDF") ||
                   ContainsIgnoreCase(path, "SdfFontAtlas1729") ||
                   IsSdfGlyphRenderMode(font.atlasRenderMode);
        }

        private static bool IsSdfGlyphRenderMode(GlyphRenderMode mode)
        {
            return mode == GlyphRenderMode.SDF ||
                   mode == GlyphRenderMode.SDF8 ||
                   mode == GlyphRenderMode.SDF16 ||
                   mode == GlyphRenderMode.SDF32 ||
                   mode == GlyphRenderMode.SDFAA ||
                   mode == GlyphRenderMode.SDFAA_HINTED;
        }

        private static bool FontHasFallback(TMP_FontAsset primary, TMP_FontAsset candidate)
        {
            if (primary == null || candidate == null)
                return false;

            return FontChainContains(primary, candidate, 0);
        }

        private static bool IsArabicFont(TMP_FontAsset font)
        {
            if (font == null)
                return false;

            string path = AssetDatabase.GetAssetPath(font);
            return ContainsIgnoreCase(font.name, "Arabic") ||
                   ContainsIgnoreCase(path, "Arabic");
        }

        private static bool IsCjkFont(TMP_FontAsset font)
        {
            if (font == null)
                return false;

            string path = AssetDatabase.GetAssetPath(font);
            return ContainsIgnoreCase(font.name, "CJK") ||
                   ContainsIgnoreCase(path, "CJK") ||
                   ContainsIgnoreCase(font.name, "CJKjp") ||
                   ContainsIgnoreCase(font.name, "CJKsc");
        }

        private static bool IsPrimitiveCollider(Collider collider)
        {
            return collider is BoxCollider || collider is CapsuleCollider || collider is SphereCollider;
        }

        private static int CountExistingCanvasComponents(string outputPath)
        {
            string[] roots = ResolveExistingRoots(outputPath);
            if (roots.Length == 0)
                return 0;

            int count = 0;
            string[] guids = AssetDatabase.FindAssets("t:Prefab", roots);
            for (int i = 0; i < guids.Length; i++)
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guids[i]));
                if (prefab == null)
                    continue;
                prefab.GetComponentsInChildren(true, s_CanvasScratch);
                count += s_CanvasScratch.Count;
                s_CanvasScratch.Clear();
            }

            return count;
        }

        private static int CountExistingPrefabs(string outputPath)
        {
            string[] roots = ResolveExistingRoots(outputPath);
            if (roots.Length == 0)
                return 0;

            return AssetDatabase.FindAssets("t:Prefab", roots).Length;
        }

        private static InteractionAnchorData[] CopyAnchors(EquipmentMetadata metadata)
        {
            if (metadata == null)
                return Array.Empty<InteractionAnchorData>();

            ReadOnlySpan<InteractionAnchorData> source = metadata.InteractionAnchors;
            InteractionAnchorData[] anchors = new InteractionAnchorData[source.Length];
            for (int i = 0; i < source.Length; i++)
                anchors[i] = source[i];
            return anchors;
        }

        private static Type ResolveComponentType(string typeName)
        {
            if (string.IsNullOrWhiteSpace(typeName))
                return null;

            Type direct = Type.GetType(typeName, throwOnError: false);
            if (direct != null)
                return direct;

            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int assemblyIndex = 0; assemblyIndex < assemblies.Length; assemblyIndex++)
            {
                Type[] types;
                try
                {
                    types = assemblies[assemblyIndex].GetTypes();
                }
                catch (ReflectionTypeLoadException exception)
                {
                    types = exception.Types;
                }

                if (types == null)
                    continue;

                for (int i = 0; i < types.Length; i++)
                {
                    Type type = types[i];
                    if (type == null)
                        continue;

                    if (string.Equals(type.FullName, typeName, StringComparison.Ordinal) ||
                        string.Equals(type.Name, typeName, StringComparison.Ordinal))
                    {
                        return type;
                    }
                }
            }

            return null;
        }

        private static string NormalizeEquipmentName(string rawName, out int lodIndex, out bool isDetail)
        {
            lodIndex = -1;
            isDetail = false;
            string name = rawName;
            RemovePrefix(ref name, "GEN_");
            RemovePrefix(ref name, "MESH_");
            RemovePrefix(ref name, "SM_");
            RemoveSuffix(ref name, "_Mesh");

            int lodMarker = name.LastIndexOf("_LOD", StringComparison.OrdinalIgnoreCase);
            if (lodMarker >= 0 && lodMarker + 4 < name.Length)
            {
                char lodChar = name[lodMarker + 4];
                if (lodChar >= '0' && lodChar <= '2')
                {
                    lodIndex = lodChar - '0';
                    name = name.Substring(0, lodMarker);
                }
            }

            int detailMarker = name.LastIndexOf("_Detail", StringComparison.OrdinalIgnoreCase);
            if (detailMarker > 0)
            {
                isDetail = true;
                name = name.Substring(0, detailMarker);
            }

            return SanitizeAssetName(name);
        }

        private static byte ResolveHandMask(AnchorRecord record)
        {
            if (record.handMask >= 0 && record.handMask <= InteractionAnchorData.HandMaskBoth)
                return (byte)record.handMask;

            string hand = record.hand ?? string.Empty;
            if (hand.IndexOf("left", StringComparison.OrdinalIgnoreCase) >= 0)
                return InteractionAnchorData.HandMaskLeft;
            if (hand.IndexOf("right", StringComparison.OrdinalIgnoreCase) >= 0)
                return InteractionAnchorData.HandMaskRight;
            return InteractionAnchorData.HandMaskBoth;
        }

        private static byte ResolveSurfaceKind(AnchorRecord record)
        {
            if (record.surfaceKind == InteractionAnchorData.SurfaceKindLever ||
                record.surfaceKind == InteractionAnchorData.SurfaceKindValve ||
                record.surfaceKind == InteractionAnchorData.SurfaceKindToggle)
            {
                return (byte)record.surfaceKind;
            }

            string surface = record.surface ?? record.kind ?? string.Empty;
            if (surface.IndexOf("valve", StringComparison.OrdinalIgnoreCase) >= 0 ||
                surface.IndexOf("wheel", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return InteractionAnchorData.SurfaceKindValve;
            }

            if (surface.IndexOf("lever", StringComparison.OrdinalIgnoreCase) >= 0 ||
                surface.IndexOf("grip", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return InteractionAnchorData.SurfaceKindLever;
            }

            return InteractionAnchorData.SurfaceKindToggle;
        }

        private static float3 SelectVector(Vector3 primary, Vector3 secondary, float3 fallback)
        {
            float3 value = ToFloat3(primary);
            if (math.lengthsq(value) > 0.000001f && math.all(math.isfinite(value)))
                return value;

            value = ToFloat3(secondary);
            if (math.lengthsq(value) > 0.000001f && math.all(math.isfinite(value)))
                return value;

            return fallback;
        }

        private static float3 SelectPosition(Vector3 localPosition, Vector3 fallbackPosition)
        {
            float3 local = ToFloat3(localPosition);
            if (!math.all(math.isfinite(local)))
                local = default;

            float3 fallback = ToFloat3(fallbackPosition);
            if (!math.all(math.isfinite(fallback)))
                fallback = default;

            return math.lengthsq(local) > 0.000001f || math.lengthsq(fallback) <= 0.000001f
                ? local
                : fallback;
        }

        private static float3 NormalizeOr(float3 value, float3 fallback)
        {
            if (!math.all(math.isfinite(value)))
                return fallback;

            float lengthSq = math.lengthsq(value);
            if (!math.isfinite(lengthSq) || lengthSq <= 0.000001f)
                return fallback;

            return value * math.rsqrt(lengthSq);
        }

        private static float3 ResolveOrthonormalUp(float3 forward, float3 up)
        {
            float3 f = NormalizeOr(forward, new float3(0f, 0f, 1f));
            float3 u = NormalizeOr(up, new float3(0f, 1f, 0f));
            float3 projected = u - f * math.dot(u, f);
            if (math.lengthsq(projected) <= 0.000001f || !math.all(math.isfinite(projected)))
            {
                float3 helper = math.abs(f.y) > 0.92f ? new float3(1f, 0f, 0f) : new float3(0f, 1f, 0f);
                projected = helper - f * math.dot(helper, f);
            }

            return NormalizeOr(projected, new float3(0f, 1f, 0f));
        }

        private static void AssignLayerRecursive(GameObject root, int layer)
        {
            if (root == null)
                return;

            root.layer = layer;
            Transform transform = root.transform;
            for (int i = 0; i < transform.childCount; i++)
                AssignLayerRecursive(transform.GetChild(i).gameObject, layer);
        }

        private static void ResetLocalTransform(Transform transform)
        {
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
            transform.localScale = Vector3.one;
        }

        private static string BuildOutputPath(string outputDirectory, string equipmentName)
        {
            return outputDirectory.TrimEnd('/', '\\') + "/PFB_" + SanitizeAssetName(equipmentName) + ".prefab";
        }

        private static void EnsureAssetFolder(string assetFolder)
        {
            string normalized = NormalizeAssetPath(assetFolder).Trim('/');
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

        private static void AddViolation(FactoryReport report, string violation)
        {
            if (string.IsNullOrWhiteSpace(violation))
                return;

            report.Violations.Add(violation);
            if (IsReadinessGap(violation))
                Debug.LogWarning("Equipment Assembly Readiness Gap: " + violation);
            else
                Debug.LogError("Equipment Assembly Violation Detected! " + violation);
        }

        private static bool IsReadinessGap(string violation)
        {
            return violation.StartsWith("No generated equipment mesh groups found", StringComparison.Ordinal);
        }

        private static void ClearScratch()
        {
            s_RendererScratch.Clear();
            s_MeshRendererScratch.Clear();
            s_ColliderScratch.Clear();
            s_MeshColliderScratch.Clear();
            s_CanvasScratch.Clear();
            s_CanvasRendererScratch.Clear();
            s_TextScratch.Clear();
            s_TextUgUiScratch.Clear();
            s_MaterialScratch.Clear();
            s_FontScratch.Clear();
        }

        private static string ResolveFullPath(string projectRelativePath)
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string normalized = NormalizeAssetPath(projectRelativePath).Replace('/', Path.DirectorySeparatorChar);
            return Path.GetFullPath(Path.Combine(projectRoot, normalized));
        }

        private static string NormalizeAssetPath(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Replace('\\', '/').Trim();
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

        private static bool IsCollisionName(string name)
        {
            return name.StartsWith("COL_", StringComparison.OrdinalIgnoreCase) ||
                   name.EndsWith("_COL", StringComparison.OrdinalIgnoreCase);
        }

        private static bool ContainsIgnoreCase(string text, string value)
        {
            return text != null && value != null && text.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static int ScoreNameMatch(string left, string right)
        {
            if (string.IsNullOrEmpty(left) || string.IsNullOrEmpty(right))
                return 0;

            if (left == right)
                return 1000 + left.Length;
            if (left.Contains(right) || right.Contains(left))
                return 100 + Math.Min(left.Length, right.Length);
            return 0;
        }

        private static string NormalizeSearch(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
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

        private static string SanitizeAssetName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "UnnamedEquipment";

            char[] invalid = Path.GetInvalidFileNameChars();
            char[] chars = value.Trim().ToCharArray();
            for (int i = 0; i < chars.Length; i++)
            {
                bool bad = char.IsWhiteSpace(chars[i]) || chars[i] == '.';
                for (int j = 0; j < invalid.Length && !bad; j++)
                    bad = chars[i] == invalid[j];
                if (bad)
                    chars[i] = '_';
            }

            return new string(chars);
        }

        private static uint HashAuthoringBake(EquipmentMeshGroup group, EquipmentAuthoringData data)
        {
            unchecked
            {
                uint hash = HashString(group.EquipmentName);
                hash ^= HashString(group.PrimaryMeshPath) + 0x9E3779B9u + (hash << 6) + (hash >> 2);
                hash ^= (uint)(data.Anchors != null ? data.Anchors.Length : 0) + 0x9E3779B9u + (hash << 6) + (hash >> 2);
                hash ^= (uint)(data.TextSurfaces != null ? data.TextSurfaces.Length : 0) + 0x9E3779B9u + (hash << 6) + (hash >> 2);
                return hash;
            }
        }

        private static uint HashString(string value)
        {
            if (string.IsNullOrEmpty(value))
                return 0u;

            unchecked
            {
                uint hash = 2166136261u;
                for (int i = 0; i < value.Length; i++)
                {
                    char c = value[i];
                    hash ^= c <= 0x7F ? (byte)c : (byte)'?';
                    hash *= 16777619u;
                }

                return hash;
            }
        }

        private static long TicksToMicroseconds(long ticks)
        {
            return (long)(ticks * (1000000.0 / Stopwatch.Frequency));
        }

        private static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        }

        private static bool IsFinite(Quaternion value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z) && IsFinite(value.w);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static bool IsZeroPosition(Vector3 value)
        {
            return IsFinite(value) && value.sqrMagnitude <= 0.00000001f;
        }

        private static bool IsOneScale(Vector3 value)
        {
            if (!IsFinite(value))
                return false;

            Vector3 delta = value - Vector3.one;
            return delta.sqrMagnitude <= 0.00000001f;
        }

        private static bool IsIdentityRotation(Quaternion value)
        {
            if (!IsFinite(value))
                return false;

            return math.abs(value.x) <= 0.0001f &&
                   math.abs(value.y) <= 0.0001f &&
                   math.abs(value.z) <= 0.0001f &&
                   math.abs(math.abs(value.w) - 1f) <= 0.0001f;
        }

        private static bool IsTextSurfaceExtentValid(float value)
        {
            return IsFinite(value) &&
                   value >= MinTextSurfaceExtentMeters &&
                   value <= MaxTextSurfaceExtentMeters;
        }

        private static float3 ToFloat3(Vector3 value)
        {
            return new float3(value.x, value.y, value.z);
        }

        private static Vector3 ToVector3(float3 value)
        {
            return new Vector3(value.x, value.y, value.z);
        }

        private static int ReadInt32(byte[] bytes, ref int offset)
        {
            int value = BitConverter.ToInt32(bytes, offset);
            offset += 4;
            return value;
        }

        private static uint ReadUInt32(byte[] bytes, ref int offset)
        {
            uint value = BitConverter.ToUInt32(bytes, offset);
            offset += 4;
            return value;
        }

        private static float ReadSingle(byte[] bytes, ref int offset)
        {
            float value = BitConverter.ToSingle(bytes, offset);
            offset += 4;
            return value;
        }

        private static byte ReadByte(byte[] bytes, ref int offset)
        {
            byte value = bytes[offset];
            offset++;
            return value;
        }

        private static float3 ReadFloat3(byte[] bytes, ref int offset)
        {
            return new float3(ReadSingle(bytes, ref offset), ReadSingle(bytes, ref offset), ReadSingle(bytes, ref offset));
        }

        [Serializable]
        public struct FactorySettings
        {
            public string MeshDirectory;
            public string MaterialDirectory;
            public string MetadataDirectory;
            public string CollisionDirectory;
            public string FontDirectory;
            public string OutputDirectory;
            public bool DryRun;
            public bool RequireTextSurfaces;
            public bool RequireRuntimeScriptBindings;
            public int MaxGroupsPerRun;

            public static FactorySettings Default => new FactorySettings
            {
                MeshDirectory = DefaultMeshDirectory,
                MaterialDirectory = DefaultMaterialDirectory,
                MetadataDirectory = DefaultMetadataDirectory,
                CollisionDirectory = DefaultCollisionDirectory,
                FontDirectory = DefaultFontDirectory,
                OutputDirectory = DefaultOutputDirectory,
                DryRun = true,
                RequireTextSurfaces = true,
                RequireRuntimeScriptBindings = false,
                MaxGroupsPerRun = 256
            };

            public FactorySettings Sanitize()
            {
                return new FactorySettings
                {
                    MeshDirectory = string.IsNullOrWhiteSpace(MeshDirectory) ? DefaultMeshDirectory : NormalizeAssetPath(MeshDirectory),
                    MaterialDirectory = string.IsNullOrWhiteSpace(MaterialDirectory) ? DefaultMaterialDirectory : NormalizeAssetPath(MaterialDirectory),
                    MetadataDirectory = string.IsNullOrWhiteSpace(MetadataDirectory) ? DefaultMetadataDirectory : NormalizeAssetPath(MetadataDirectory),
                    CollisionDirectory = string.IsNullOrWhiteSpace(CollisionDirectory) ? DefaultCollisionDirectory : NormalizeAssetPath(CollisionDirectory),
                    FontDirectory = string.IsNullOrWhiteSpace(FontDirectory) ? DefaultFontDirectory : NormalizeAssetPath(FontDirectory),
                    OutputDirectory = string.IsNullOrWhiteSpace(OutputDirectory) ? DefaultOutputDirectory : NormalizeAssetPath(OutputDirectory),
                    DryRun = DryRun,
                    RequireTextSurfaces = RequireTextSurfaces,
                    RequireRuntimeScriptBindings = RequireRuntimeScriptBindings,
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
            public string MetadataDirectory;
            public string CollisionDirectory;
            public string FontDirectory;
            public string OutputDirectory;
            public string PrimaryFontAsset;
            public bool DryRun;
            public int ExistingEquipmentPrefabCount;
            public int GroupsDiscovered;
            public int PrefabsAssembled;
            public int PrefabsDryRunPassed;
            public int PrefabsFailed;
            public int PrefabValidatorPasses;
            public int PrimitiveColliderCount;
            public int CanvasComponentsFound;
            public int TextMeshPro3DCount;
            public int FontFallbackCount;
            public long ExecutionMicroseconds;
            public List<string> Violations = new List<string>(64);
            public List<GroupReport> GroupReports = new List<GroupReport>(64);
        }

        [Serializable]
        public sealed class GroupReport
        {
            public string EquipmentName;
            public string Status;
            public string Failure;
            public string SourceMesh;
            public string CollisionProxy;
            public string OutputPrefab;
            public bool Saved;
            public bool LodGroupConfigured;
            public int RendererCount;
            public int TextSurfaceCount;
            public int AnchorCount;
            public int ColliderCount;
            public int RuntimeComponentsBound;
            public int SerializedReferencesBound;
            public int ValidatorPasses;
            public long ElapsedMicroseconds;
        }

        private sealed class EquipmentMeshGroup
        {
            public readonly string EquipmentName;
            public readonly Mesh[] Lods = new Mesh[3];
            public readonly string[] LodPaths = new string[3];
            public readonly List<Mesh> DetailMeshes = new List<Mesh>(8);
            public readonly List<string> DetailMeshPaths = new List<string>(8);

            public EquipmentMeshGroup(string equipmentName)
            {
                EquipmentName = equipmentName;
            }

            public Mesh PrimaryMesh => Lods[0] != null ? Lods[0] : DetailMeshes.Count > 0 ? DetailMeshes[0] : null;
            public string PrimaryMeshPath => !string.IsNullOrEmpty(LodPaths[0]) ? LodPaths[0] : DetailMeshPaths.Count > 0 ? DetailMeshPaths[0] : string.Empty;
        }

        private sealed class MaterialPalette
        {
            private readonly List<Material> materials;

            private MaterialPalette(List<Material> materials)
            {
                this.materials = materials;
            }

            public static MaterialPalette Build(string materialDirectory, FactoryReport report)
            {
                s_MaterialScratch.Clear();
                string[] roots = ResolveExistingRoots(materialDirectory, DefaultMaterialDirectory);
                if (roots.Length == 0)
                {
                    AddViolation(report, "FATAL: Material directory not found: " + materialDirectory);
                    return new MaterialPalette(new List<Material>(0));
                }

                string[] guids = AssetDatabase.FindAssets("t:Material", roots);
                for (int i = 0; i < guids.Length; i++)
                {
                    Material material = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guids[i]));
                    if (material != null)
                        s_MaterialScratch.Add(material);
                }

                return new MaterialPalette(new List<Material>(s_MaterialScratch));
            }

            public Material ResolveMaterial(string equipmentName, string authoredMaterialName)
            {
                Material best = null;
                int bestScore = int.MinValue;
                string equipmentKey = NormalizeSearch(equipmentName);
                string authoredKey = NormalizeSearch(authoredMaterialName);
                for (int i = 0; i < materials.Count; i++)
                {
                    Material material = materials[i];
                    if (material == null)
                        continue;

                    string materialKey = NormalizeSearch(material.name);
                    int score = 0;
                    if (!string.IsNullOrEmpty(authoredKey) && materialKey == authoredKey)
                        score += 2000;
                    if (materialKey.Contains("equipmentatlas") || materialKey.Contains("matequipmentatlas"))
                        score += 1000;
                    if (materialKey.Contains("tool") || materialKey.Contains("console") || materialKey.Contains("equipment"))
                        score += 200;
                    score += ScoreNameMatch(equipmentKey, materialKey);
                    if (material.renderQueue >= 3000)
                        score -= 1000;

                    if (score > bestScore)
                    {
                        bestScore = score;
                        best = material;
                    }
                }

                return bestScore >= MinimumMaterialResolveScore ? best : null;
            }
        }

        private struct EquipmentAuthoringData
        {
            public uint EquipmentId;
            public uint BakeHash;
            public float GlobalQualityWeight;
            public string SourcePath;
            public string MaterialName;
            public InteractionAnchorData[] Anchors;
            public TextSurfaceData[] TextSurfaces;
            public RuntimeComponentData[] RuntimeComponents;
        }

        private struct TextSurfaceData
        {
            public string Name;
            public string Text;
            public float3 LocalPosition;
            public float3 Normal;
            public float3 Up;
            public float WidthMeters;
            public float HeightMeters;
            public float FontSizeMin;
            public float FontSizeMax;
        }

        [Serializable]
        private sealed class EquipmentMetadataFile
        {
            public string equipmentName;
            public uint equipmentId;
            public uint bakeHash;
            public float globalQualityWeight = 1f;
            public string materialName;
            public AnchorRecord[] anchors;
            public TextSurfaceRecord[] textSurfaces;
            public TextSurfaceRecord[] surfaces;
            public RuntimeComponentData[] runtimeComponents;
        }

        [Serializable]
        private struct AnchorRecord
        {
            public string id;
            public string name;
            public uint anchorId;
            public Vector3 localPosition;
            public Vector3 position;
            public Vector3 forward;
            public Vector3 normal;
            public Vector3 up;
            public float snapRadiusMeters;
            public float snapRadius;
            public uint flags;
            public int handMask;
            public string hand;
            public int surfaceKind;
            public string surface;
            public string kind;
            public bool active;
            public bool twoHanded;
        }

        [Serializable]
        private struct TextSurfaceRecord
        {
            public string name;
            public string text;
            public Vector3 localPosition;
            public Vector3 position;
            public Vector3 normal;
            public Vector3 forward;
            public Vector3 up;
            public float widthMeters;
            public float width;
            public float heightMeters;
            public float height;
            public float fontSizeMin;
            public float fontSizeMax;
        }

        [Serializable]
        private struct RuntimeComponentData
        {
            public string TypeName;
            public string typeName;
            public string[] PrimaryTextFields;
            public string[] primaryTextFields;
            public string[] SecondaryTextFields;
            public string[] secondaryTextFields;
            public string[] TextFields;
            public string[] textFields;
            public string[] RendererFields;
            public string[] rendererFields;

            public string ResolvedTypeName => !string.IsNullOrWhiteSpace(TypeName) ? TypeName : typeName;
            public string[] ResolvedPrimaryTextFields => PrimaryTextFields ?? primaryTextFields ?? Array.Empty<string>();
            public string[] ResolvedSecondaryTextFields => SecondaryTextFields ?? secondaryTextFields ?? Array.Empty<string>();
            public string[] ResolvedTextFields => TextFields ?? textFields ?? Array.Empty<string>();
            public string[] ResolvedRendererFields => RendererFields ?? rendererFields ?? Array.Empty<string>();
        }
    }
}
#endif
