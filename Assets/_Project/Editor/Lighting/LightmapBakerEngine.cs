using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace Hecton8.Editor.Lighting
{
    /// <summary>
    /// Editor-only lighting conveyor for static GI, static reflection probes, and dense light-probe authoring.
    /// Runtime player builds must not reference this class.
    /// </summary>
    public sealed class LightmapBakerEngine : EditorWindow
    {
        private const string MenuPath = "Hecton8/Lighting/Lightmap Baker Engine 1730";
        private const string OutputLightingPath = "Assets/_Project/Art/Textures/Lighting";
        private const string EditorSettingsPath = "Assets/_Project/Editor/Lighting/Settings";
        private const string ProbeGroupName = "H8_LightProbeGrid_Baked_1730";

        private static readonly string[] TargetScenes =
        {
            "Assets/_Project/Scenes/01_ORBIT.unity",
            "Assets/_Project/Scenes/01_MAIN_MENU.unity",
            "Assets/_Project/Scenes/02_HECTON_WORLD.unity"
        };

        [SerializeField] private float _globalQualityWeight = 0.72f;
        [SerializeField] private bool _validateUvBeforeBake = true;
        [SerializeField] private bool _bakeReflectionProbes = true;
        [SerializeField] private bool _generateProbeGrid = true;
        [SerializeField] private bool _copyBakedLightmapAssets = true;
        [SerializeField] private int _maximumProbeCount = 24000;

        private string _status = "Idle.";
        private Label _profileLightmapLabel;
        private Label _profileReflectionLabel;
        private Label _profileNearSpacingLabel;
        private Label _profileOpenSpacingLabel;
        private Label _statusLabel;

        [MenuItem(MenuPath)]
        public static void Open()
        {
            LightmapBakerEngine window = GetWindow<LightmapBakerEngine>();
            window.titleContent = new GUIContent("H8 Light Baker");
            window.minSize = new Vector2(480f, 340f);
            window.Show();
        }

        public void CreateGUI()
        {
            VisualElement root = rootVisualElement;
            root.Clear();
            root.style.paddingLeft = 10f;
            root.style.paddingRight = 10f;
            root.style.paddingTop = 8f;

            Label title = new Label("HECTON-8 Offline Lightmap / Reflection Probe Baker");
            title.style.marginBottom = 6f;
            root.Add(title);

            Slider quality = new Slider("_H8GlobalQualityWeight", 0f, 1f)
            {
                value = _globalQualityWeight,
                showInputField = true
            };
            quality.RegisterValueChangedCallback(OnQualityWeightChanged);
            root.Add(quality);

            SliderInt maximumProbes = new SliderInt("Maximum Probe Count", 512, 64000)
            {
                value = _maximumProbeCount,
                showInputField = true
            };
            maximumProbes.RegisterValueChangedCallback(OnMaximumProbeCountChanged);
            root.Add(maximumProbes);

            Toggle validateUv = new Toggle("Validate Lightmap UVs") { value = _validateUvBeforeBake };
            validateUv.RegisterValueChangedCallback(evt => _validateUvBeforeBake = evt.newValue);
            root.Add(validateUv);

            Toggle generateProbes = new Toggle("Generate Dense Light Probes") { value = _generateProbeGrid };
            generateProbes.RegisterValueChangedCallback(evt => _generateProbeGrid = evt.newValue);
            root.Add(generateProbes);

            Toggle bakeReflections = new Toggle("Bake Reflection Probes") { value = _bakeReflectionProbes };
            bakeReflections.RegisterValueChangedCallback(evt => _bakeReflectionProbes = evt.newValue);
            root.Add(bakeReflections);

            Toggle copyLightmaps = new Toggle("Copy Baked Lightmaps To Lighting Folder") { value = _copyBakedLightmapAssets };
            copyLightmaps.RegisterValueChangedCallback(evt => _copyBakedLightmapAssets = evt.newValue);
            root.Add(copyLightmaps);

            Label profileTitle = new Label("Resolved Offline Profile");
            profileTitle.style.marginTop = 8f;
            profileTitle.style.marginBottom = 2f;
            root.Add(profileTitle);

            _profileLightmapLabel = new Label();
            _profileReflectionLabel = new Label();
            _profileNearSpacingLabel = new Label();
            _profileOpenSpacingLabel = new Label();
            root.Add(_profileLightmapLabel);
            root.Add(_profileReflectionLabel);
            root.Add(_profileNearSpacingLabel);
            root.Add(_profileOpenSpacingLabel);

            VisualElement commandGroup = new VisualElement();
            commandGroup.style.marginTop = 10f;
            commandGroup.Add(CreateCommandButton("Dry Run Active Scene", RunDryActiveScene));
            commandGroup.Add(CreateCommandButton("Bake Active Scene", RunBakeActiveScene));
            commandGroup.Add(CreateCommandButton("Dry Run Target Scenes", RunDryTargetScenes));
            commandGroup.Add(CreateCommandButton("Bake Target Scenes", RunBakeTargetScenes));
            root.Add(commandGroup);

            _statusLabel = new Label(_status);
            _statusLabel.style.marginTop = 10f;
            _statusLabel.style.whiteSpace = WhiteSpace.Normal;
            root.Add(_statusLabel);

            RefreshProfileLabels();
        }

        private void OnDisable()
        {
            TryCancelEditorLightmappingRun();
            EditorUtility.ClearProgressBar();
        }

        private void OnQualityWeightChanged(ChangeEvent<float> evt)
        {
            _globalQualityWeight = Mathf.Clamp01(evt.newValue);
            RefreshProfileLabels();
        }

        private void OnMaximumProbeCountChanged(ChangeEvent<int> evt)
        {
            _maximumProbeCount = Mathf.Clamp(evt.newValue, 512, 64000);
            RefreshProfileLabels();
        }

        private static Button CreateCommandButton(string label, Action action)
        {
            Button button = new Button(action) { text = label };
            button.style.marginTop = 3f;
            return button;
        }

        private void RunDryActiveScene()
        {
            ExecuteActiveScene(dryRun: true);
        }

        private void RunBakeActiveScene()
        {
            ExecuteActiveScene(dryRun: false);
        }

        private void RunDryTargetScenes()
        {
            ExecuteTargetScenes(dryRun: true);
        }

        private void RunBakeTargetScenes()
        {
            bool accepted = EditorUtility.DisplayDialog(
                "Bake target scenes",
                "This opens target scenes, writes lighting/probe assets, and saves scene changes. Continue?",
                "Bake",
                "Cancel");
            if (accepted)
                ExecuteTargetScenes(dryRun: false);
        }

        private void RefreshProfileLabels()
        {
            if (_profileLightmapLabel == null ||
                _profileReflectionLabel == null ||
                _profileNearSpacingLabel == null ||
                _profileOpenSpacingLabel == null)
            {
                return;
            }

            BakeQualityProfile profile = BakeQualityProfile.FromWeight(_globalQualityWeight, _maximumProbeCount);
            _profileLightmapLabel.text = "Lightmap atlas: " + profile.LightmapResolution.ToString(CultureInfo.InvariantCulture);
            _profileReflectionLabel.text = "Reflection probe: " + profile.ReflectionResolution.ToString(CultureInfo.InvariantCulture);
            _profileNearSpacingLabel.text = "Near structure spacing: " + profile.NearStructureSpacingMeters.ToString("0.00", CultureInfo.InvariantCulture) + " m";
            _profileOpenSpacingLabel.text = "Open water spacing: " + profile.OpenWaterSpacingMeters.ToString("0.00", CultureInfo.InvariantCulture) + " m";
        }

        private void ExecuteActiveScene(bool dryRun)
        {
            BakeReport report = BakeReport.Create(_globalQualityWeight, dryRun);
            BakeQualityProfile profile = BakeQualityProfile.FromWeight(_globalQualityWeight, _maximumProbeCount);
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || string.IsNullOrEmpty(scene.path))
            {
                report.AddFatal("Active scene is invalid or unsaved.");
                FinishReport(report);
                return;
            }

            ExecuteOpenScene(scene.path, profile, report, dryRun);
            FinishReport(report);
        }

        private void ExecuteTargetScenes(bool dryRun)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            BakeReport report = BakeReport.Create(_globalQualityWeight, dryRun);
            BakeQualityProfile profile = BakeQualityProfile.FromWeight(_globalQualityWeight, _maximumProbeCount);
            for (int i = 0; i < TargetScenes.Length; i++)
            {
                string scenePath = TargetScenes[i];
                if (!File.Exists(ToAbsolutePath(scenePath)))
                {
                    report.AddFatal("Missing target scene: " + scenePath);
                    continue;
                }

                EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                ExecuteOpenScene(scenePath, profile, report, dryRun);
            }

            FinishReport(report);
        }

        private void ExecuteOpenScene(string scenePath, BakeQualityProfile profile, BakeReport report, bool dryRun)
        {
            Stopwatch sceneWatch = Stopwatch.StartNew();
            Scene scene = SceneManager.GetActiveScene();
            string sceneName = SanitizeName(scene.name);
            report.BeginScene(scenePath, sceneName);

            if (_validateUvBeforeBake && !ValidateLightmapUvs(report))
            {
                report.AddFatal("Lightmap UV overlap violation detected!");
                sceneWatch.Stop();
                report.EndScene(sceneWatch.ElapsedTicks);
                return;
            }

            if (dryRun)
            {
                AuditSceneLightingInputs(report);
                if (_generateProbeGrid)
                    GenerateLightProbeGrid(profile, report, dryRun: true);

                sceneWatch.Stop();
                report.EndScene(sceneWatch.ElapsedTicks);
                return;
            }

            ConfigureLightmapping(sceneName, profile, report);
            ConfigureStaticRenderers(profile, report);
            ConfigureLights(report);
            ConfigureReflectionProbeRuntimePolicy(profile, report);

            if (_generateProbeGrid)
                GenerateLightProbeGrid(profile, report, dryRun);

            bool baked = InvokeLightmappingBake(report);
            if (!baked)
                report.AddWarning("Lightmapping bake API did not execute. Settings and validators still ran.");

            if (_copyBakedLightmapAssets)
                CopyBakedLightmaps(sceneName, profile, report);

            if (_bakeReflectionProbes)
                BakeReflectionProbes(sceneName, profile, report);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();

            sceneWatch.Stop();
            report.EndScene(sceneWatch.ElapsedTicks);
        }

        private static void ConfigureLightmapping(string sceneName, BakeQualityProfile profile, BakeReport report)
        {
            SetStaticProperty(typeof(LightmapEditorSettings), "realtimeGI", false, report);
            SetStaticProperty(typeof(LightmapEditorSettings), "enableRealtimeLightmaps", false, report);
            SetStaticProperty(typeof(LightmapEditorSettings), "bakedGI", true, report);
            SetStaticProperty(typeof(LightmapEditorSettings), "enableBakedLightmaps", true, report);
            SetStaticEnumProperty(typeof(LightmapEditorSettings), "lightmapper", "ProgressiveGPU", report);
            SetStaticEnumProperty(typeof(LightmapEditorSettings), "bakeBackend", "GPU", report);
            SetStaticFloatProperty(typeof(LightmapEditorSettings), "bakeResolution", profile.TexelsPerUnit, report);
            SetStaticFloatProperty(typeof(LightmapEditorSettings), "realtimeResolution", 0.05f, report);
            SetStaticIntProperty(typeof(LightmapEditorSettings), "maxAtlasSize", profile.LightmapResolution, report);
            SetStaticIntProperty(typeof(LightmapEditorSettings), "padding", profile.LightmapPadding, report);
            SetStaticIntProperty(typeof(LightmapEditorSettings), "bounces", profile.Bounces, report);
            SetStaticIntProperty(typeof(LightmapEditorSettings), "directSampleCount", profile.DirectSamples, report);
            SetStaticIntProperty(typeof(LightmapEditorSettings), "indirectSampleCount", profile.IndirectSamples, report);
            SetStaticIntProperty(typeof(LightmapEditorSettings), "environmentSampleCount", profile.EnvironmentSamples, report);
            SetStaticFloatProperty(typeof(LightmapEditorSettings), "aoMaxDistance", profile.AoMaxDistance, report);
            SetStaticFloatProperty(typeof(LightmapEditorSettings), "compAOExponent", profile.AoExponent, report);
            SetStaticFloatProperty(typeof(LightmapEditorSettings), "compAOExponentDirect", profile.AoExponentDirect, report);
            SetStaticProperty(typeof(LightmapEditorSettings), "extractAO", true, report);
            SetStaticEnumProperty(typeof(LightmapEditorSettings), "textureCompression", "HighQuality", report);
            SetStaticProperty(typeof(LightmapEditorSettings), "seamStitching", true, report);

            UnityEngine.Object lightingSettings = ResolveOrCreateLightingSettings(sceneName, report);
            if (lightingSettings != null)
            {
                SetInstanceProperty(lightingSettings, "bakedGI", true, report);
                SetInstanceProperty(lightingSettings, "realtimeGI", false, report);
                SetInstanceEnumProperty(lightingSettings, "lightmapper", "ProgressiveGPU", report);
                SetInstanceEnumProperty(lightingSettings, "bakeBackend", "GPU", report);
                SetInstanceProperty(lightingSettings, "lightmapMaxSize", profile.LightmapResolution, report);
                SetInstanceProperty(lightingSettings, "lightmapResolution", profile.TexelsPerUnit, report);
                SetInstanceProperty(lightingSettings, "ao", true, report);
                SetInstanceProperty(lightingSettings, "aoMaxDistance", profile.AoMaxDistance, report);
                SetInstanceProperty(lightingSettings, "padding", profile.LightmapPadding, report);
            }

            LightmapSettings.lightmapsMode = LightmapsMode.CombinedDirectional;
            report.LightmapResolution = profile.LightmapResolution;
            report.ReflectionResolution = profile.ReflectionResolution;
        }

        private static UnityEngine.Object ResolveOrCreateLightingSettings(string sceneName, BakeReport report)
        {
            PropertyInfo property = typeof(Lightmapping).GetProperty(
                "lightingSettings",
                BindingFlags.Public | BindingFlags.Static);
            if (property == null || !property.CanWrite)
                return null;

            UnityEngine.Object existing = property.GetValue(null, null) as UnityEngine.Object;
            if (existing != null)
                return existing;

            Type settingsType = Type.GetType("UnityEngine.LightingSettings, UnityEngine.CoreModule");
            if (settingsType == null)
                settingsType = Type.GetType("UnityEngine.LightingSettings, UnityEngine");
            if (settingsType == null)
                return null;

            EnsureAssetFolder(EditorSettingsPath);
            string assetPath = EditorSettingsPath + "/LGT_" + sceneName + "_Baked_1730.lighting";
            UnityEngine.Object loaded = AssetDatabase.LoadAssetAtPath(assetPath, settingsType);
            if (loaded == null)
            {
                loaded = ScriptableObject.CreateInstance(settingsType);
                AssetDatabase.CreateAsset(loaded, assetPath);
                report.AddGeneratedAsset(assetPath);
            }

            property.SetValue(null, loaded, null);
            return loaded;
        }

        private static void ConfigureStaticRenderers(BakeQualityProfile profile, BakeReport report)
        {
            MeshRenderer[] renderers = UnityEngine.Object.FindObjectsByType<MeshRenderer>(
                FindObjectsInactive.Include);

            for (int i = 0; i < renderers.Length; i++)
            {
                MeshRenderer renderer = renderers[i];
                if (!IsStaticBakeCandidate(renderer))
                    continue;

                StaticEditorFlags flags = GameObjectUtility.GetStaticEditorFlags(renderer.gameObject);
                flags |= StaticEditorFlags.ContributeGI | StaticEditorFlags.BatchingStatic;
                GameObjectUtility.SetStaticEditorFlags(renderer.gameObject, flags);

                renderer.receiveGI = ReceiveGI.Lightmaps;
                renderer.scaleInLightmap = ResolveRendererLightmapScale(renderer, profile);
                renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.BlendProbes;
                report.StaticRendererCount++;
            }
        }

        private static float ResolveRendererLightmapScale(Renderer renderer, BakeQualityProfile profile)
        {
            Bounds bounds = renderer.bounds;
            float extent = Mathf.Max(bounds.extents.x, Mathf.Max(bounds.extents.y, bounds.extents.z));
            float nearField = Mathf.InverseLerp(28f, 1f, extent);
            float scale = Mathf.Lerp(profile.BackgroundLightmapScale, profile.HeroLightmapScale, nearField);
            return Mathf.Clamp(scale, 0.2f, 2.5f);
        }

        private static void ConfigureLights(BakeReport report)
        {
            Light[] lights = UnityEngine.Object.FindObjectsByType<Light>(
                FindObjectsInactive.Include);

            for (int i = 0; i < lights.Length; i++)
            {
                Light light = lights[i];
                if (!IsStaticBakeLightCandidate(light))
                    continue;

                if (light.type == LightType.Directional || light.type == LightType.Spot || light.type == LightType.Point)
                {
                    light.lightmapBakeType = LightmapBakeType.Baked;
                }

                light.bounceIntensity = Mathf.Max(0f, light.bounceIntensity);
                report.LightCount++;
            }
        }

        private static void AuditSceneLightingInputs(BakeReport report)
        {
            MeshRenderer[] renderers = UnityEngine.Object.FindObjectsByType<MeshRenderer>(
                FindObjectsInactive.Include);
            for (int i = 0; i < renderers.Length; i++)
            {
                if (IsStaticBakeCandidate(renderers[i]))
                    report.StaticRendererCount++;
            }

            Light[] lights = UnityEngine.Object.FindObjectsByType<Light>(
                FindObjectsInactive.Include);
            for (int i = 0; i < lights.Length; i++)
            {
                if (IsStaticBakeLightCandidate(lights[i]))
                    report.LightCount++;
            }

            ReflectionProbe[] probes = UnityEngine.Object.FindObjectsByType<ReflectionProbe>(
                FindObjectsInactive.Include);
            report.ReflectionProbeCount = probes.Length;
        }

        private static void ConfigureReflectionProbeRuntimePolicy(BakeQualityProfile profile, BakeReport report)
        {
            ReflectionProbe[] probes = UnityEngine.Object.FindObjectsByType<ReflectionProbe>(
                FindObjectsInactive.Include);

            for (int i = 0; i < probes.Length; i++)
            {
                ReflectionProbe probe = probes[i];
                if (probe == null)
                    continue;

                probe.mode = ReflectionProbeMode.Baked;
                probe.refreshMode = ReflectionProbeRefreshMode.ViaScripting;
                probe.timeSlicingMode = ReflectionProbeTimeSlicingMode.NoTimeSlicing;
                probe.resolution = profile.ReflectionResolution;
                probe.hdr = true;
                probe.boxProjection = true;
                report.ReflectionProbeCount++;
            }
        }

        private static bool ValidateLightmapUvs(BakeReport report)
        {
            MeshRenderer[] renderers = UnityEngine.Object.FindObjectsByType<MeshRenderer>(
                FindObjectsInactive.Include);

            List<Vector2> uv2 = new List<Vector2>(4096);
            List<int> triangles = new List<int>(12288);
            Dictionary<long, UvCellOccupancy> occupied = new Dictionary<long, UvCellOccupancy>(16384);
            bool valid = true;

            for (int i = 0; i < renderers.Length; i++)
            {
                MeshRenderer renderer = renderers[i];
                if (!IsStaticBakeCandidate(renderer))
                    continue;

                MeshFilter meshFilter = renderer.GetComponent<MeshFilter>();
                Mesh mesh = meshFilter != null ? meshFilter.sharedMesh : null;
                if (mesh == null)
                    continue;

                bool rendererValid = true;
                uv2.Clear();
                mesh.GetUVs(1, uv2);
                if (uv2.Count == 0)
                {
                    report.AddUvViolation(renderer.name, "Missing UV2 lightmap channel.");
                    valid = false;
                    continue;
                }

                for (int uvIndex = 0; uvIndex < uv2.Count; uvIndex++)
                {
                    Vector2 uv = uv2[uvIndex];
                    if (!IsFinite01(uv.x) || !IsFinite01(uv.y))
                    {
                        report.AddUvViolation(renderer.name, "UV2 outside normalized 0..1 range.");
                        valid = false;
                        rendererValid = false;
                        break;
                    }
                }

                if (!rendererValid)
                    continue;

                occupied.Clear();
                int subMeshCount = mesh.subMeshCount;
                for (int subMesh = 0; subMesh < subMeshCount; subMesh++)
                {
                    triangles.Clear();
                    uint indexCount = mesh.GetIndexCount(subMesh);
                    if (indexCount > int.MaxValue)
                    {
                        report.AddUvViolation(renderer.name, "Submesh index count exceeds editor validator capacity.");
                        valid = false;
                        rendererValid = false;
                        break;
                    }

                    int requiredIndexCapacity = (int)indexCount;
                    if (triangles.Capacity < requiredIndexCapacity)
                        triangles.Capacity = requiredIndexCapacity;

                    mesh.GetTriangles(triangles, subMesh);
                    for (int tri = 0; tri + 2 < triangles.Count; tri += 3)
                    {
                        int a = triangles[tri];
                        int b = triangles[tri + 1];
                        int c = triangles[tri + 2];
                        if (a < 0 || b < 0 || c < 0 || a >= uv2.Count || b >= uv2.Count || c >= uv2.Count)
                            continue;

                        Vector2 min = Vector2.Min(uv2[a], Vector2.Min(uv2[b], uv2[c]));
                        Vector2 max = Vector2.Max(uv2[a], Vector2.Max(uv2[b], uv2[c]));
                        int minX = Mathf.Clamp(Mathf.FloorToInt(min.x * 128f), 0, 127);
                        int minY = Mathf.Clamp(Mathf.FloorToInt(min.y * 128f), 0, 127);
                        int maxX = Mathf.Clamp(Mathf.CeilToInt(max.x * 128f), 0, 127);
                        int maxY = Mathf.Clamp(Mathf.CeilToInt(max.y * 128f), 0, 127);

                        for (int y = minY; y <= maxY; y++)
                        {
                            for (int x = minX; x <= maxX; x++)
                            {
                                long key = ((long)y << 32) | (uint)x;
                                UvCellOccupancy previous;
                                if (occupied.TryGetValue(key, out previous))
                                {
                                    if (!previous.SharesAnyVertex(a, b, c))
                                    {
                                        report.AddUvViolation(renderer.name, "Approximate UV2 overlap cell collision.");
                                        valid = false;
                                        rendererValid = false;
                                        y = maxY + 1;
                                        break;
                                    }
                                }
                                else
                                {
                                    occupied.Add(key, new UvCellOccupancy(a, b, c));
                                }
                            }
                        }

                        if (!rendererValid)
                            break;
                    }

                    if (!rendererValid)
                        break;
                }
            }

            return valid;
        }

        private static bool IsFinite01(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0f && value <= 1f;
        }

        private static void GenerateLightProbeGrid(BakeQualityProfile profile, BakeReport report, bool dryRun)
        {
            if (!TryResolveStaticSceneBounds(out Bounds sceneBounds))
            {
                report.AddWarning("No static renderer bounds found. Light Probe grid skipped.");
                return;
            }

            List<Vector3> probes = new List<Vector3>(profile.MaximumProbeCount);
            HashSet<Vector3Int> quantized = new HashSet<Vector3Int>(profile.MaximumProbeCount);
            Bounds expanded = sceneBounds;
            expanded.Expand(new Vector3(10f, 6f, 10f));

            AddProbeGrid(expanded, profile.OpenWaterSpacingMeters, profile.MaximumProbeCount, probes, quantized);

            MeshRenderer[] renderers = UnityEngine.Object.FindObjectsByType<MeshRenderer>(
                FindObjectsInactive.Exclude);
            for (int i = 0; i < renderers.Length && probes.Count < profile.MaximumProbeCount; i++)
            {
                MeshRenderer renderer = renderers[i];
                if (!IsStaticBakeCandidate(renderer))
                    continue;

                Bounds nearBounds = renderer.bounds;
                nearBounds.Expand(new Vector3(2f, 2f, 2f));
                AddProbeGrid(nearBounds, profile.NearStructureSpacingMeters, profile.MaximumProbeCount, probes, quantized);
            }

            AddNavigationMarkerProbes(profile, probes, quantized, report);

            report.LightProbeCount = probes.Count;
            report.LightProbeCellMeters = profile.NearStructureSpacingMeters;
            if (probes.Count >= profile.MaximumProbeCount)
                report.AddWarning("Probe generation hit maximum probe count.");

            if (dryRun)
                return;

            LightProbeGroup group = ResolveLightProbeGroup();
            group.probePositions = probes.ToArray();
            EditorUtility.SetDirty(group);
            report.GeneratedProbeGroup = ProbeGroupName;
        }

        private static bool TryResolveStaticSceneBounds(out Bounds sceneBounds)
        {
            MeshRenderer[] renderers = UnityEngine.Object.FindObjectsByType<MeshRenderer>(
                FindObjectsInactive.Exclude);

            sceneBounds = default;
            bool hasBounds = false;
            for (int i = 0; i < renderers.Length; i++)
            {
                MeshRenderer renderer = renderers[i];
                if (!IsStaticBakeCandidate(renderer))
                    continue;

                if (!hasBounds)
                {
                    sceneBounds = renderer.bounds;
                    hasBounds = true;
                }
                else
                {
                    sceneBounds.Encapsulate(renderer.bounds);
                }
            }

            return hasBounds;
        }

        private static bool IsStaticBakeCandidate(MeshRenderer renderer)
        {
            if (renderer == null || !renderer.enabled || renderer.sharedMaterial == null)
                return false;

            GameObject gameObject = renderer.gameObject;
            StaticEditorFlags flags = GameObjectUtility.GetStaticEditorFlags(gameObject);
            return gameObject.isStatic ||
                   (flags & (StaticEditorFlags.ContributeGI |
                             StaticEditorFlags.BatchingStatic |
                             StaticEditorFlags.OccluderStatic |
                             StaticEditorFlags.OccludeeStatic)) != 0;
        }

        private static bool IsStaticBakeLightCandidate(Light light)
        {
            if (light == null || !light.enabled)
                return false;

            GameObject gameObject = light.gameObject;
            StaticEditorFlags flags = GameObjectUtility.GetStaticEditorFlags(gameObject);
            return gameObject.isStatic ||
                   light.lightmapBakeType != LightmapBakeType.Realtime ||
                   (flags & (StaticEditorFlags.ContributeGI |
                             StaticEditorFlags.BatchingStatic |
                             StaticEditorFlags.OccluderStatic)) != 0;
        }

        private static void AddNavigationMarkerProbes(
            BakeQualityProfile profile,
            List<Vector3> probes,
            HashSet<Vector3Int> quantized,
            BakeReport report)
        {
            Transform[] transforms = UnityEngine.Object.FindObjectsByType<Transform>(
                FindObjectsInactive.Exclude);

            int markerCount = 0;
            for (int i = 0; i < transforms.Length && probes.Count < profile.MaximumProbeCount; i++)
            {
                Transform transform = transforms[i];
                if (transform == null || !LooksLikeNavigationLightingMarker(transform.name))
                    continue;

                Bounds markerBounds = new Bounds(transform.position, new Vector3(4f, 4f, 4f));
                AddProbeGrid(markerBounds, profile.NearStructureSpacingMeters, profile.MaximumProbeCount, probes, quantized);
                markerCount++;
            }

            report.NavigationMarkerProbeAnchorCount = markerCount;
        }

        private static bool LooksLikeNavigationLightingMarker(string name)
        {
            if (string.IsNullOrEmpty(name))
                return false;

            return name.IndexOf("Player", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   name.IndexOf("Spawn", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   name.IndexOf("Waypoint", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   name.IndexOf("Route", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   name.IndexOf("Nav", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   name.IndexOf("Path", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   name.IndexOf("Fish", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   name.IndexOf("Fauna", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static void AddProbeGrid(
            Bounds bounds,
            float spacing,
            int maximumProbeCount,
            List<Vector3> probes,
            HashSet<Vector3Int> quantized)
        {
            Vector3 min = bounds.min;
            Vector3 max = bounds.max;
            float safeSpacing = Mathf.Max(0.25f, spacing);
            for (float y = min.y; y <= max.y && probes.Count < maximumProbeCount; y += safeSpacing)
            {
                for (float z = min.z; z <= max.z && probes.Count < maximumProbeCount; z += safeSpacing)
                {
                    for (float x = min.x; x <= max.x && probes.Count < maximumProbeCount; x += safeSpacing)
                    {
                        Vector3 position = new Vector3(x, y, z);
                        Vector3Int key = new Vector3Int(
                            Mathf.RoundToInt(position.x * 10f),
                            Mathf.RoundToInt(position.y * 10f),
                            Mathf.RoundToInt(position.z * 10f));
                        if (quantized.Add(key))
                            probes.Add(position);
                    }
                }
            }
        }

        private static LightProbeGroup ResolveLightProbeGroup()
        {
            GameObject existing = GameObject.Find(ProbeGroupName);
            if (existing == null)
                existing = new GameObject(ProbeGroupName);

            LightProbeGroup group = existing.GetComponent<LightProbeGroup>();
            if (group == null)
                group = existing.AddComponent<LightProbeGroup>();

            existing.transform.position = Vector3.zero;
            existing.transform.rotation = Quaternion.identity;
            existing.transform.localScale = Vector3.one;
            StaticEditorFlags flags = GameObjectUtility.GetStaticEditorFlags(existing);
            flags |= StaticEditorFlags.ContributeGI;
            GameObjectUtility.SetStaticEditorFlags(existing, flags);
            return group;
        }

        private static bool InvokeLightmappingBake(BakeReport report)
        {
            MethodInfo bake = typeof(Lightmapping).GetMethod("Bake", BindingFlags.Public | BindingFlags.Static, null, Type.EmptyTypes, null);
            if (bake != null)
            {
                object result = bake.Invoke(null, null);
                report.LightmapBakeInvoked = true;
                return result is bool value ? value : true;
            }

            MethodInfo bakeAsync = typeof(Lightmapping).GetMethod("BakeAsync", BindingFlags.Public | BindingFlags.Static, null, Type.EmptyTypes, null);
            if (bakeAsync != null)
            {
                object result = bakeAsync.Invoke(null, null);
                report.LightmapBakeInvoked = true;
                return result is bool value ? value : true;
            }

            return false;
        }

        private static void TryCancelEditorLightmappingRun()
        {
            try
            {
                PropertyInfo isRunning = typeof(Lightmapping).GetProperty(
                    "isRunning",
                    BindingFlags.Public | BindingFlags.Static);
                bool running = isRunning != null &&
                               isRunning.PropertyType == typeof(bool) &&
                               (bool)isRunning.GetValue(null, null);
                if (!running)
                    return;

                MethodInfo cancel = typeof(Lightmapping).GetMethod(
                    "Cancel",
                    BindingFlags.Public | BindingFlags.Static,
                    null,
                    Type.EmptyTypes,
                    null);
                cancel?.Invoke(null, null);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("LightmapBakerEngine cleanup could not cancel active lightmapping run: " + ex.Message);
            }
        }

        private static void CopyBakedLightmaps(string sceneName, BakeQualityProfile profile, BakeReport report)
        {
            LightmapData[] lightmaps = LightmapSettings.lightmaps;
            if (lightmaps == null || lightmaps.Length == 0)
            {
                report.AddWarning("No LightmapSettings.lightmaps were available after bake.");
                return;
            }

            EnsureAssetFolder(OutputLightingPath);
            for (int i = 0; i < lightmaps.Length; i++)
            {
                Texture2D lightmap = lightmaps[i] != null ? lightmaps[i].lightmapColor : null;
                if (lightmap == null)
                    continue;

                if (!ValidateTexturePixelCount(lightmap, profile.LightmapResolution, report, "lightmap " + i.ToString(CultureInfo.InvariantCulture)))
                    continue;

                string sourceAssetPath = AssetDatabase.GetAssetPath(lightmap);
                if (string.IsNullOrEmpty(sourceAssetPath))
                    continue;

                string extension = Path.GetExtension(sourceAssetPath);
                if (string.IsNullOrEmpty(extension))
                    extension = ".exr";

                string targetAssetPath = OutputLightingPath + "/TX_Lightmap_" + sceneName + "_" + i.ToString("00", CultureInfo.InvariantCulture) + extension;
                CopyAssetFileAsBytes(sourceAssetPath, targetAssetPath, report);
                ConfigureHdrTextureImporter(targetAssetPath, profile.LightmapResolution, TextureImporterShape.Texture2D, report);
            }
        }

        private static void BakeReflectionProbes(string sceneName, BakeQualityProfile profile, BakeReport report)
        {
            ReflectionProbe[] probes = UnityEngine.Object.FindObjectsByType<ReflectionProbe>(
                FindObjectsInactive.Include);
            if (probes.Length == 0)
                return;

            EnsureAssetFolder(OutputLightingPath);
            List<string> bakedProbeAssets = new List<string>(probes.Length);
            for (int i = 0; i < probes.Length; i++)
            {
                ReflectionProbe probe = probes[i];
                if (probe == null)
                    continue;

                string targetAssetPath = OutputLightingPath + "/TX_ReflectionProbe_" + sceneName + "_" + i.ToString("00", CultureInfo.InvariantCulture) + ".exr";
                if (TryBakeReflectionProbe(probe, targetAssetPath, report))
                {
                    bakedProbeAssets.Add(targetAssetPath);
                    ConfigureHdrTextureImporter(targetAssetPath, profile.ReflectionResolution, TextureImporterShape.TextureCube, report);
                    report.BakedReflectionProbeCount++;
                }
            }

            CreateReflectionCubemapArrayAtlas(sceneName, bakedProbeAssets, profile.ReflectionResolution, report);
            DeleteTemporaryReflectionProbeAssets(bakedProbeAssets, report);
        }

        private static bool TryBakeReflectionProbe(ReflectionProbe probe, string targetAssetPath, BakeReport report)
        {
            MethodInfo[] methods = typeof(Lightmapping).GetMethods(BindingFlags.Public | BindingFlags.Static);
            for (int i = 0; i < methods.Length; i++)
            {
                MethodInfo method = methods[i];
                if (method.Name != "BakeReflectionProbe")
                    continue;

                ParameterInfo[] parameters = method.GetParameters();
                try
                {
                    if (parameters.Length == 2 &&
                        parameters[0].ParameterType.IsAssignableFrom(typeof(ReflectionProbe)) &&
                        parameters[1].ParameterType == typeof(string))
                    {
                        object result = method.Invoke(null, new object[] { probe, targetAssetPath });
                        return result is bool value ? value : true;
                    }

                    if (parameters.Length == 1 &&
                        parameters[0].ParameterType.IsAssignableFrom(typeof(ReflectionProbe)))
                    {
                        object result = method.Invoke(null, new object[] { probe });
                        Texture baked = probe.bakedTexture;
                        if (baked != null)
                        {
                            string sourcePath = AssetDatabase.GetAssetPath(baked);
                            if (!string.IsNullOrEmpty(sourcePath))
                                CopyAssetFileAsBytes(sourcePath, targetAssetPath, report, registerGeneratedAsset: false);
                        }

                        return result is bool value ? value : true;
                    }
                }
                catch (TargetInvocationException ex)
                {
                    report.AddWarning("Reflection probe bake failed for " + probe.name + ": " + ex.GetBaseException().Message);
                    return false;
                }
            }

            report.AddWarning("No compatible Lightmapping.BakeReflectionProbe overload found.");
            return false;
        }

        private static void CreateReflectionCubemapArrayAtlas(string sceneName, List<string> probeAssets, int resolution, BakeReport report)
        {
            if (probeAssets == null || probeAssets.Count == 0)
                return;

            CubemapArray atlas = new CubemapArray(resolution, probeAssets.Count, TextureFormat.BC6H, true)
            {
                name = "TX_ReflectionProbeAtlas_" + sceneName + "_1730"
            };

            bool copiedAny = false;
            for (int probeIndex = 0; probeIndex < probeAssets.Count; probeIndex++)
            {
                Cubemap cubemap = AssetDatabase.LoadAssetAtPath<Cubemap>(probeAssets[probeIndex]);
                if (cubemap == null)
                {
                    report.AddWarning("Reflection cubemap import unavailable for atlas: " + probeAssets[probeIndex]);
                    continue;
                }

                if (!ValidateTexturePixelCount(cubemap, resolution, report, "reflection cubemap " + probeIndex.ToString(CultureInfo.InvariantCulture)))
                    continue;

                bool copiedProbe = true;
                for (int face = 0; face < 6; face++)
                {
                    try
                    {
                        UnityEngine.Graphics.CopyTexture(cubemap, face, 0, atlas, probeIndex * 6 + face, 0);
                    }
                    catch (Exception ex)
                    {
                        report.AddWarning("Reflection cubemap atlas copy failed for " + probeAssets[probeIndex] + ": " + ex.Message);
                        copiedProbe = false;
                        break;
                    }
                }

                copiedAny |= copiedProbe;
            }

            if (!copiedAny)
            {
                UnityEngine.Object.DestroyImmediate(atlas);
                return;
            }

            string atlasPath = OutputLightingPath + "/TX_ReflectionProbeAtlas_" + sceneName + "_1730.asset";
            AssetDatabase.DeleteAsset(atlasPath);
            AssetDatabase.CreateAsset(atlas, atlasPath);
            AssetDatabase.ImportAsset(atlasPath, ImportAssetOptions.ForceUpdate);
            report.ReflectionAtlasPath = atlasPath;
            report.AddGeneratedAsset(atlasPath);
        }

        private static void DeleteTemporaryReflectionProbeAssets(List<string> probeAssets, BakeReport report)
        {
            if (probeAssets == null)
                return;

            for (int i = 0; i < probeAssets.Count; i++)
            {
                string assetPath = probeAssets[i];
                if (string.IsNullOrEmpty(assetPath))
                    continue;

                if (AssetDatabase.DeleteAsset(assetPath))
                    report.DeletedTemporaryReflectionProbeAssetCount++;
            }
        }

        private static bool ValidateTexturePixelCount(Texture texture, int expectedResolution, BakeReport report, string label)
        {
            if (texture == null)
                return false;

            long expectedPixels = (long)expectedResolution * expectedResolution;
            long actualPixels = (long)texture.width * texture.height;
            if (actualPixels != expectedPixels)
            {
                report.AddFatal(
                    label + " pixel count mismatch. expected=" +
                    expectedPixels.ToString(CultureInfo.InvariantCulture) +
                    " actual=" +
                    actualPixels.ToString(CultureInfo.InvariantCulture));
                return false;
            }

            return true;
        }

        private static void CopyAssetFileAsBytes(
            string sourceAssetPath,
            string targetAssetPath,
            BakeReport report,
            bool registerGeneratedAsset = true)
        {
            string source = ToAbsolutePath(sourceAssetPath);
            string target = ToAbsolutePath(targetAssetPath);
            if (!File.Exists(source))
            {
                report.AddWarning("Source lighting asset missing: " + sourceAssetPath);
                return;
            }

            File.Copy(source, target, true);
            AssetDatabase.ImportAsset(targetAssetPath, ImportAssetOptions.ForceUpdate);
            if (registerGeneratedAsset)
                report.AddGeneratedAsset(targetAssetPath);
        }

        private static void ConfigureHdrTextureImporter(string assetPath, int resolution, TextureImporterShape shape, BakeReport report)
        {
            TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null)
            {
                report.AddWarning("TextureImporter unavailable for " + assetPath);
                return;
            }

            importer.textureType = TextureImporterType.Default;
            importer.textureShape = shape;
            importer.sRGBTexture = false;
            importer.mipmapEnabled = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;

            TextureImporterPlatformSettings platform = importer.GetDefaultPlatformTextureSettings();
            platform.overridden = true;
            platform.format = TextureImporterFormat.BC6H;
            platform.maxTextureSize = Mathf.Max(256, resolution);
            importer.SetPlatformTextureSettings(platform);
            importer.SaveAndReimport();
            report.Bc6hImportCount++;
        }

        private void FinishReport(BakeReport report)
        {
            report.FinalizeReport();
            _status = report.BuildStatusLine();
            if (_statusLabel != null)
                _statusLabel.text = _status;
            Repaint();
        }

        private static void EnsureAssetFolder(string assetFolder)
        {
            string absolute = ToAbsolutePath(assetFolder);
            if (!Directory.Exists(absolute))
                Directory.CreateDirectory(absolute);
            AssetDatabase.ImportAsset(assetFolder, ImportAssetOptions.ForceUpdate);
        }

        private static string ToAbsolutePath(string projectRelativePath)
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string normalized = projectRelativePath.Replace('/', Path.DirectorySeparatorChar);
            return Path.Combine(projectRoot, normalized);
        }

        private static string SanitizeName(string name)
        {
            if (string.IsNullOrEmpty(name))
                return "UNNAMED";

            StringBuilder builder = new StringBuilder(name.Length);
            for (int i = 0; i < name.Length; i++)
            {
                char c = name[i];
                builder.Append(char.IsLetterOrDigit(c) || c == '_' ? c : '_');
            }

            return builder.ToString();
        }

        private static void SetStaticProperty(Type type, string propertyName, object value, BakeReport report)
        {
            PropertyInfo property = type.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Static);
            if (property == null || !property.CanWrite)
                return;

            try
            {
                object converted = ConvertValue(value, property.PropertyType);
                property.SetValue(null, converted, null);
                report.AddSetting(type.Name + "." + propertyName, converted);
            }
            catch (Exception ex)
            {
                report.AddWarning("Could not set " + type.Name + "." + propertyName + ": " + ex.Message);
            }
        }

        private static void SetStaticIntProperty(Type type, string propertyName, int value, BakeReport report)
        {
            SetStaticProperty(type, propertyName, value, report);
        }

        private static void SetStaticFloatProperty(Type type, string propertyName, float value, BakeReport report)
        {
            SetStaticProperty(type, propertyName, value, report);
        }

        private static void SetStaticEnumProperty(Type type, string propertyName, string enumValue, BakeReport report)
        {
            PropertyInfo property = type.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Static);
            if (property == null || !property.CanWrite || !property.PropertyType.IsEnum)
                return;

            object parsed = ParseEnumFallback(property.PropertyType, enumValue);
            if (parsed == null)
                return;

            try
            {
                property.SetValue(null, parsed, null);
                report.AddSetting(type.Name + "." + propertyName, parsed);
            }
            catch (Exception ex)
            {
                report.AddWarning("Could not set " + type.Name + "." + propertyName + ": " + ex.Message);
            }
        }

        private static void SetInstanceProperty(UnityEngine.Object target, string propertyName, object value, BakeReport report)
        {
            PropertyInfo property = target.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
            if (property == null || !property.CanWrite)
                return;

            try
            {
                object converted = ConvertValue(value, property.PropertyType);
                property.SetValue(target, converted, null);
                EditorUtility.SetDirty(target);
                report.AddSetting(target.GetType().Name + "." + propertyName, converted);
            }
            catch (Exception ex)
            {
                report.AddWarning("Could not set " + target.GetType().Name + "." + propertyName + ": " + ex.Message);
            }
        }

        private static void SetInstanceEnumProperty(UnityEngine.Object target, string propertyName, string enumValue, BakeReport report)
        {
            PropertyInfo property = target.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
            if (property == null || !property.CanWrite || !property.PropertyType.IsEnum)
                return;

            object parsed = ParseEnumFallback(property.PropertyType, enumValue);
            if (parsed == null)
                return;

            try
            {
                property.SetValue(target, parsed, null);
                EditorUtility.SetDirty(target);
                report.AddSetting(target.GetType().Name + "." + propertyName, parsed);
            }
            catch (Exception ex)
            {
                report.AddWarning("Could not set " + target.GetType().Name + "." + propertyName + ": " + ex.Message);
            }
        }

        private static object ParseEnumFallback(Type enumType, string preferredValue)
        {
            string[] names = Enum.GetNames(enumType);
            for (int i = 0; i < names.Length; i++)
            {
                if (string.Equals(names[i], preferredValue, StringComparison.OrdinalIgnoreCase))
                    return Enum.Parse(enumType, names[i]);
            }

            for (int i = 0; i < names.Length; i++)
            {
                if (names[i].IndexOf("GPU", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    names[i].IndexOf("Progressive", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    names[i].IndexOf("High", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return Enum.Parse(enumType, names[i]);
                }
            }

            return null;
        }

        private static object ConvertValue(object value, Type targetType)
        {
            if (targetType == typeof(int))
                return Convert.ToInt32(value, CultureInfo.InvariantCulture);
            if (targetType == typeof(float))
                return Convert.ToSingle(value, CultureInfo.InvariantCulture);
            if (targetType == typeof(bool))
                return Convert.ToBoolean(value, CultureInfo.InvariantCulture);
            if (targetType == typeof(double))
                return Convert.ToDouble(value, CultureInfo.InvariantCulture);
            return value;
        }

        private readonly struct BakeQualityProfile
        {
            public readonly float QualityWeight;
            public readonly int LightmapResolution;
            public readonly int ReflectionResolution;
            public readonly int MaximumProbeCount;
            public readonly int LightmapPadding;
            public readonly int Bounces;
            public readonly int DirectSamples;
            public readonly int IndirectSamples;
            public readonly int EnvironmentSamples;
            public readonly float TexelsPerUnit;
            public readonly float NearStructureSpacingMeters;
            public readonly float OpenWaterSpacingMeters;
            public readonly float HeroLightmapScale;
            public readonly float BackgroundLightmapScale;
            public readonly float AoMaxDistance;
            public readonly float AoExponent;
            public readonly float AoExponentDirect;

            private BakeQualityProfile(
                float qualityWeight,
                int lightmapResolution,
                int reflectionResolution,
                int maximumProbeCount,
                int lightmapPadding,
                int bounces,
                int directSamples,
                int indirectSamples,
                int environmentSamples,
                float texelsPerUnit,
                float nearStructureSpacingMeters,
                float openWaterSpacingMeters,
                float heroLightmapScale,
                float backgroundLightmapScale,
                float aoMaxDistance,
                float aoExponent,
                float aoExponentDirect)
            {
                QualityWeight = qualityWeight;
                LightmapResolution = lightmapResolution;
                ReflectionResolution = reflectionResolution;
                MaximumProbeCount = maximumProbeCount;
                LightmapPadding = lightmapPadding;
                Bounces = bounces;
                DirectSamples = directSamples;
                IndirectSamples = indirectSamples;
                EnvironmentSamples = environmentSamples;
                TexelsPerUnit = texelsPerUnit;
                NearStructureSpacingMeters = nearStructureSpacingMeters;
                OpenWaterSpacingMeters = openWaterSpacingMeters;
                HeroLightmapScale = heroLightmapScale;
                BackgroundLightmapScale = backgroundLightmapScale;
                AoMaxDistance = aoMaxDistance;
                AoExponent = aoExponent;
                AoExponentDirect = aoExponentDirect;
            }

            public static BakeQualityProfile FromWeight(float weight, int maximumProbeCount)
            {
                float q = Mathf.Clamp01(float.IsNaN(weight) || float.IsInfinity(weight) ? 0f : weight);
                float smooth = q * q * (3f - 2f * q);
                int lightmapResolution = Mathf.RoundToInt(Mathf.Lerp(1024f, 4096f, smooth));
                lightmapResolution = Mathf.ClosestPowerOfTwo(Mathf.Clamp(lightmapResolution, 1024, 4096));
                int reflectionResolution = Mathf.RoundToInt(Mathf.Lerp(256f, 1024f, smooth));
                reflectionResolution = Mathf.ClosestPowerOfTwo(Mathf.Clamp(reflectionResolution, 256, 1024));
                int probeBudget = Mathf.RoundToInt(Mathf.Lerp(1024f, maximumProbeCount, smooth));
                return new BakeQualityProfile(
                    q,
                    lightmapResolution,
                    reflectionResolution,
                    Mathf.Clamp(probeBudget, 512, maximumProbeCount),
                    Mathf.RoundToInt(Mathf.Lerp(2f, 8f, smooth)),
                    Mathf.RoundToInt(Mathf.Lerp(1f, 4f, smooth)),
                    Mathf.RoundToInt(Mathf.Lerp(64f, 512f, smooth)),
                    Mathf.RoundToInt(Mathf.Lerp(128f, 2048f, smooth)),
                    Mathf.RoundToInt(Mathf.Lerp(32f, 512f, smooth)),
                    Mathf.Lerp(4f, 10f, smooth),
                    2f,
                    5f,
                    Mathf.Lerp(0.75f, 1.7f, smooth),
                    Mathf.Lerp(0.35f, 0.9f, smooth),
                    Mathf.Lerp(1.5f, 5f, smooth),
                    Mathf.Lerp(0.7f, 1.4f, smooth),
                    Mathf.Lerp(0.4f, 1.1f, smooth));
            }
        }

        private readonly struct UvCellOccupancy
        {
            private readonly int _a;
            private readonly int _b;
            private readonly int _c;

            public UvCellOccupancy(int a, int b, int c)
            {
                _a = a;
                _b = b;
                _c = c;
            }

            public bool SharesAnyVertex(int a, int b, int c)
            {
                return a == _a || a == _b || a == _c ||
                       b == _a || b == _b || b == _c ||
                       c == _a || c == _b || c == _c;
            }
        }

        private sealed class BakeReport
        {
            private readonly Stopwatch _watch = Stopwatch.StartNew();

            public float QualityWeight;
            public bool DryRun;
            public bool HasFatal;
            public int WarningCount;
            public int FatalCount;
            public int UvViolationCount;
            public int GeneratedAssetCount;
            public int SettingCount;
            public int LightmapResolution;
            public int ReflectionResolution;
            public int StaticRendererCount;
            public int LightCount;
            public int ReflectionProbeCount;
            public int BakedReflectionProbeCount;
            public int DeletedTemporaryReflectionProbeAssetCount;
            public int LightProbeCount;
            public int NavigationMarkerProbeAnchorCount;
            public int Bc6hImportCount;
            public float LightProbeCellMeters;
            public bool LightmapBakeInvoked;
            public string CurrentScenePath;
            public string CurrentSceneName;
            public string GeneratedProbeGroup;
            public string ReflectionAtlasPath;
            public string FirstWarning;
            public string FirstFatal;
            public string FirstUvViolation;
            public long TotalMicroseconds;
            public long LastSceneMicroseconds;

            public static BakeReport Create(float qualityWeight, bool dryRun)
            {
                return new BakeReport
                {
                    QualityWeight = Mathf.Clamp01(qualityWeight),
                    DryRun = dryRun
                };
            }

            public void BeginScene(string scenePath, string sceneName)
            {
                CurrentScenePath = scenePath;
                CurrentSceneName = sceneName;
            }

            public void EndScene(long elapsedTicks)
            {
                LastSceneMicroseconds = TicksToMicroseconds(elapsedTicks);
            }

            public void AddWarning(string message)
            {
                WarningCount++;
                if (string.IsNullOrEmpty(FirstWarning))
                    FirstWarning = message;
                Debug.LogWarning(message);
            }

            public void AddFatal(string message)
            {
                HasFatal = true;
                FatalCount++;
                if (string.IsNullOrEmpty(FirstFatal))
                    FirstFatal = message;
                Debug.LogError(message);
            }

            public void AddUvViolation(string rendererName, string reason)
            {
                UvViolationCount++;
                if (string.IsNullOrEmpty(FirstUvViolation))
                    FirstUvViolation = rendererName + ": " + reason;
            }

            public void AddGeneratedAsset(string path)
            {
                GeneratedAssetCount++;
            }

            public void AddSetting(string key, object value)
            {
                SettingCount++;
            }

            public void FinalizeReport()
            {
                _watch.Stop();
                TotalMicroseconds = TicksToMicroseconds(_watch.ElapsedTicks);
            }

            private static long TicksToMicroseconds(long ticks)
            {
                return ticks * 1000000L / Stopwatch.Frequency;
            }

            public string BuildStatusLine()
            {
                StringBuilder builder = new StringBuilder(320);
                builder.Append(HasFatal ? "Bake validation failed" : "Bake pass completed");
                builder.Append(DryRun ? " (dry run)." : ".");
                builder.Append(" Scene=").Append(CurrentSceneName ?? "none");
                builder.Append(" Lightmaps=").Append(LightmapResolution.ToString(CultureInfo.InvariantCulture));
                builder.Append(" Reflections=").Append(ReflectionResolution.ToString(CultureInfo.InvariantCulture));
                builder.Append(" Probes=").Append(LightProbeCount.ToString(CultureInfo.InvariantCulture));
                builder.Append(" NavAnchors=").Append(NavigationMarkerProbeAnchorCount.ToString(CultureInfo.InvariantCulture));
                builder.Append(" Assets=").Append(GeneratedAssetCount.ToString(CultureInfo.InvariantCulture));
                builder.Append(" TmpProbeAssetsDeleted=").Append(DeletedTemporaryReflectionProbeAssetCount.ToString(CultureInfo.InvariantCulture));
                builder.Append(" BC6H=").Append(Bc6hImportCount.ToString(CultureInfo.InvariantCulture));
                builder.Append(" Warnings=").Append(WarningCount.ToString(CultureInfo.InvariantCulture));
                builder.Append(" Fatal=").Append(FatalCount.ToString(CultureInfo.InvariantCulture));
                builder.Append(" TimeUs=").Append(TotalMicroseconds.ToString(CultureInfo.InvariantCulture));
                if (!string.IsNullOrEmpty(FirstFatal))
                {
                    builder.Append(" FirstFatal=");
                    builder.Append(FirstFatal);
                }
                else if (!string.IsNullOrEmpty(FirstWarning))
                {
                    builder.Append(" FirstWarning=");
                    builder.Append(FirstWarning);
                }

                return builder.ToString();
            }
        }
    }
}
