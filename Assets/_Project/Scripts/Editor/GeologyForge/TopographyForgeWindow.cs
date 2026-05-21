#if UNITY_EDITOR
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Hecton8.Editor.GeologyForge
{
    public sealed class TopographyForgeWindow : EditorWindow
    {
        private Slider _ridgeFrequency;
        private Slider _warpStrength;
        private SliderInt _terraceSteps;
        private Slider _terraceStrength;
        private Slider _riftDepth;
        private Slider _qualityWeight;
        private IntegerField _sectorResolution;
        private IntegerField _sectorCountX;
        private IntegerField _sectorCountZ;
        private IntegerField _macroResolution;
        private ProgressBar _progress;
        private Image _previewImage;

        private void OnDisable()
        {
            TopographyForgePreview.Shutdown();
        }

        [MenuItem("HECTON-8/Geology Forge/Global Topography Forge", false, 185)]
        public static void Open()
        {
            TopographyForgeWindow window = GetWindow<TopographyForgeWindow>("Global Topography Forge");
            window.minSize = new Vector2(520f, 640f);
        }

        public void CreateGUI()
        {
            rootVisualElement.style.paddingLeft = 8;
            rootVisualElement.style.paddingRight = 8;
            rootVisualElement.style.paddingTop = 8;
            rootVisualElement.style.paddingBottom = 8;

            TopographyBakeSettings defaults = TopographyForgeGenerator.DefaultSettings();
            _ridgeFrequency = new Slider("Ridged Noise Frequency", 0.00005f, 0.0012f);
            _warpStrength = new Slider("Domain Warping Strength", 0f, 2200f);
            _terraceSteps = new SliderInt("Terracing Steps", 1, 64);
            _terraceStrength = new Slider("Terracing Strength", 0f, 1f);
            _riftDepth = new Slider("Tectonic Rift Depth", 0f, 5000f);
            _qualityWeight = new Slider("Global Quality Weight", 0f, 1f);
            _sectorResolution = new IntegerField("Sector Resolution");
            _sectorCountX = new IntegerField("Sector Count X");
            _sectorCountZ = new IntegerField("Sector Count Z");
            _macroResolution = new IntegerField("Macro Resolution");
            _progress = new ProgressBar { title = "Bake Progress", lowValue = 0f, highValue = 1f, value = 0f };
            _previewImage = new Image();
            _previewImage.scaleMode = ScaleMode.ScaleToFit;
            _previewImage.style.height = 256;
            _previewImage.style.marginTop = 8;
            _previewImage.style.marginBottom = 8;

            rootVisualElement.Add(_ridgeFrequency);
            rootVisualElement.Add(_warpStrength);
            rootVisualElement.Add(_terraceSteps);
            rootVisualElement.Add(_terraceStrength);
            rootVisualElement.Add(_riftDepth);
            rootVisualElement.Add(_qualityWeight);
            rootVisualElement.Add(_sectorResolution);
            rootVisualElement.Add(_sectorCountX);
            rootVisualElement.Add(_sectorCountZ);
            rootVisualElement.Add(_macroResolution);
            rootVisualElement.Add(_progress);
            rootVisualElement.Add(_previewImage);

            Button reload = new Button(ReloadCsvAndPreview) { text = "Reload CSV" };
            Button preview = new Button(BuildPreview) { text = "Preview Heightmap" };
            Button mock = new Button(RunMockBenchmark) { text = "Run 4096 Mock Sector" };
            Button bake = new Button(BakeGlobal) { text = "BAKE GLOBAL HEIGHTMAPS" };
            Button cancel = new Button(TopographyForgeGenerator.CancelAsyncBake) { text = "Cancel Bake" };
            Button graphScan = new Button(LegacyMapMagicGraphInquisition.ScanAndWriteReport) { text = "Scan MapMagic Graphs" };
            Button runtimeScan = new Button(Terrain_Runtime_Scanner.ScanAndWriteReport) { text = "Scan Runtime Terrain" };
            Button audit = new Button(TopographyForgeSelfAudit.RunAndWriteReport) { text = "Run Self Audit" };
            rootVisualElement.Add(reload);
            rootVisualElement.Add(preview);
            rootVisualElement.Add(mock);
            rootVisualElement.Add(bake);
            rootVisualElement.Add(cancel);
            rootVisualElement.Add(graphScan);
            rootVisualElement.Add(runtimeScan);
            rootVisualElement.Add(audit);

            ApplySettings(defaults);
            BuildPreview();
        }

        private void ApplySettings(TopographyBakeSettings settings)
        {
            _ridgeFrequency.SetValueWithoutNotify(settings.RidgeFrequency);
            _warpStrength.SetValueWithoutNotify(settings.WarpStrengthMeters);
            _terraceSteps.SetValueWithoutNotify((int)settings.TerraceSteps);
            _terraceStrength.SetValueWithoutNotify(settings.TerraceStrength);
            _riftDepth.SetValueWithoutNotify(settings.RiftDepthMeters);
            _qualityWeight.SetValueWithoutNotify(settings.GlobalQualityWeight);
            _sectorResolution.SetValueWithoutNotify(settings.SectorResolution);
            _sectorCountX.SetValueWithoutNotify(settings.SectorCountX);
            _sectorCountZ.SetValueWithoutNotify(settings.SectorCountZ);
            _macroResolution.SetValueWithoutNotify(settings.MacroResolution);
        }

        private TopographyBakeSettings ResolveSettings()
        {
            TopographyBakeSettings settings = TopographyForgeGenerator.DefaultSettings();
            settings.RidgeFrequency = _ridgeFrequency != null ? _ridgeFrequency.value : settings.RidgeFrequency;
            settings.WarpStrengthMeters = _warpStrength != null ? _warpStrength.value : settings.WarpStrengthMeters;
            settings.TerraceSteps = _terraceSteps != null ? _terraceSteps.value : settings.TerraceSteps;
            settings.TerraceStrength = _terraceStrength != null ? _terraceStrength.value : settings.TerraceStrength;
            settings.RiftDepthMeters = _riftDepth != null ? _riftDepth.value : settings.RiftDepthMeters;
            settings.GlobalQualityWeight = _qualityWeight != null ? _qualityWeight.value : settings.GlobalQualityWeight;
            settings.SectorResolution = _sectorResolution != null ? _sectorResolution.value : settings.SectorResolution;
            settings.SectorCountX = _sectorCountX != null ? _sectorCountX.value : settings.SectorCountX;
            settings.SectorCountZ = _sectorCountZ != null ? _sectorCountZ.value : settings.SectorCountZ;
            settings.MacroResolution = _macroResolution != null ? _macroResolution.value : settings.MacroResolution;
            return settings;
        }

        private void ReloadCsvAndPreview()
        {
            BuildPreview();
        }

        private void BuildPreview()
        {
            _previewImage.image = TopographyForgePreview.Build(ResolveSettings());
            _previewImage.MarkDirtyRepaint();
        }

        private void RunMockBenchmark()
        {
            TopographyBakeMetrics metrics = TopographyForgeGenerator.RunMockSectorBenchmark(ResolveSettings());
            Debug.Log("[TopographyForge] 4096 mock sector ms=" + metrics.MockSectorMilliseconds.ToString("F3"));
        }

        private void BakeGlobal()
        {
            if (!TopographyForgeGenerator.BakeGlobalHeightmapsAsync(ResolveSettings(), SetBakeProgress))
                Debug.LogWarning("[TopographyForge] Bake request ignored: a bake is already running.");
        }

        private void SetBakeProgress(float value)
        {
            if (_progress == null)
                return;

            _progress.value = math.saturate(value);
            _progress.MarkDirtyRepaint();
            Repaint();
        }
    }

    internal static class TopographyForgePreview
    {
        private static Texture2D _texture;

        static TopographyForgePreview()
        {
            AssemblyReloadEvents.beforeAssemblyReload += Shutdown;
            EditorApplication.quitting += Shutdown;
        }

        public static Texture2D Build(TopographyBakeSettings settings)
        {
            int resolution = ResolvePreviewResolution(settings.GlobalQualityWeight);
            EnsureTexture(resolution);
            int cellCount = resolution * resolution;
            NativeList<TopographyBiomeRecipeDTO> recipeList = default;
            NativeArray<TopographyBiomeKernelDTO> recipes = default;
            NativeArray<TectonicRiftSegmentDTO> rifts = default;
            NativeArray<double2> warped = default;
            NativeArray<float> raw = default;
            NativeArray<float> terraced = default;
            NativeArray<float> final = default;
            NativeArray<Color32> pixels = default;
            try
            {
                recipeList = new NativeList<TopographyBiomeRecipeDTO>(16, Allocator.Temp);
                TopographyBiomeCsv.LoadRecipes(ref recipeList);
                recipes = new NativeArray<TopographyBiomeKernelDTO>(recipeList.Length, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                for (int i = 0; i < recipeList.Length; i++)
                    recipes[i] = ToKernelRecipe(recipeList[i], settings.GlobalQualityWeight);

                rifts = new NativeArray<TectonicRiftSegmentDTO>(1, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                rifts[0] = BuildPreviewRift(settings);
                warped = new NativeArray<double2>(cellCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                raw = new NativeArray<float>(cellCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                terraced = new NativeArray<float>(cellCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                final = new NativeArray<float>(cellCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                pixels = new NativeArray<Color32>(cellCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);

                TopographyBakeConfigDTO config = BuildPreviewConfig(settings, resolution);
                config.RiftCount = rifts.Length;
                new ApplyDomainWarpingJob
                {
                    WarpedAupXZ = warped,
                    Recipes = recipes,
                    Config = config,
                    Warp = BuildWarp(settings)
                }.Run(cellCount);
                new EvaluateMountainRidgesJob
                {
                    WarpedAupXZ = warped,
                    Recipes = recipes,
                    HeightsMeters = raw,
                    Config = config,
                    Ridge = BuildRidge(settings)
                }.Run(cellCount);
                new ApplyStrataTerracingJob
                {
                    InputHeightsMeters = raw,
                    OutputHeightsMeters = terraced,
                    Config = config
                }.Run(cellCount);
                new ApplyTectonicRiftsJob
                {
                    InputHeightsMeters = terraced,
                    Rifts = rifts,
                    OutputHeightsMeters = final,
                    Config = config
                }.Run(cellCount);

                CopyHeightsToTexture(final, pixels, resolution);
                return _texture;
            }
            finally
            {
                if (recipeList.IsCreated) recipeList.Dispose();
                if (recipes.IsCreated) recipes.Dispose();
                if (rifts.IsCreated) rifts.Dispose();
                if (warped.IsCreated) warped.Dispose();
                if (raw.IsCreated) raw.Dispose();
                if (terraced.IsCreated) terraced.Dispose();
                if (final.IsCreated) final.Dispose();
                if (pixels.IsCreated) pixels.Dispose();
            }
        }

        private static int ResolvePreviewResolution(float globalQualityWeight)
        {
            float q = math.smoothstep(0f, 1f, math.saturate(globalQualityWeight));
            int resolution = (int)math.round(math.lerp(64f, TopographyForgeConstants.PreviewResolution, q));
            return math.clamp(resolution, 64, TopographyForgeConstants.PreviewResolution);
        }

        private static void EnsureTexture(int resolution)
        {
            if (_texture == null || _texture.width != resolution || _texture.height != resolution)
            {
                Shutdown();
                _texture = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false, true)
                {
                    filterMode = FilterMode.Point,
                    wrapMode = TextureWrapMode.Clamp,
                    hideFlags = HideFlags.HideAndDontSave
                };
            }
        }

        public static void Shutdown()
        {
            if (_texture == null)
                return;

            if (Application.isPlaying)
                UnityEngine.Object.Destroy(_texture);
            else
                UnityEngine.Object.DestroyImmediate(_texture);

            _texture = null;
        }

        private static TopographyBiomeKernelDTO ToKernelRecipe(TopographyBiomeRecipeDTO recipe, float globalQualityWeight)
        {
            TopographyBiomeKernelDTO kernel = default;
            kernel.CenterAupXZ = recipe.CenterAupXZ;
            kernel.RadiusMeters = math.max(1f, recipe.RadiusMeters);
            kernel.InvRadiusMeters = math.rcp(kernel.RadiusMeters);
            kernel.InvRadiusSqMeters = math.rcp(kernel.RadiusMeters * kernel.RadiusMeters);
            kernel.TerraceSteps = math.max(1f, recipe.TerraceSteps);
            kernel.TerraceStrength = math.saturate(recipe.TerraceStrength);
            kernel.RidgeBlend = math.saturate(recipe.RidgeBlend);
            kernel.RiftDepthMeters = math.max(0f, recipe.RiftDepthMeters);
            kernel.SeedHash = recipe.SeedHash;
            kernel.Ridge = TopographyQualityMath.ApplyRidgeQuality(recipe.Ridge, globalQualityWeight);
            kernel.Warp = TopographyQualityMath.ApplyWarpQuality(recipe.Warp, globalQualityWeight);
            return kernel;
        }

        private static TopographyBakeConfigDTO BuildPreviewConfig(TopographyBakeSettings settings, int resolution)
        {
            TopographyBakeConfigDTO config = default;
            const double previewMeters = 1000.0;
            config.SectorAup = new double3(settings.WorldOriginAup.x - previewMeters * 0.5, settings.WorldOriginAup.y, settings.WorldOriginAup.z - previewMeters * 0.5);
            config.PixelSizeMeters = previewMeters / (resolution - 1);
            config.Width = resolution;
            config.Height = resolution;
            config.HeightMinMeters = settings.HeightMinMeters;
            config.HeightMaxMeters = settings.HeightMaxMeters;
            config.SeaFloorBiasMeters = settings.SeaFloorBiasMeters;
            config.RidgeBlend = 1f;
            float q = TopographyQualityMath.ResolveQuality(settings.GlobalQualityWeight);
            config.TerraceSteps = math.max(1f, math.lerp(4f, settings.TerraceSteps, q));
            config.TerraceStrength = math.saturate(math.lerp(settings.TerraceStrength * 0.35f, settings.TerraceStrength, q));
            config.TerraceSlopeStart = 0.025f;
            config.TerraceSlopeEnd = 0.22f;
            config.RiftDepthMeters = settings.RiftDepthMeters;
            config.RiftWidthMeters = settings.RiftWidthMeters;
            config.WorldSeed = settings.WorldSeed == 0u ? 0x53483234u : settings.WorldSeed;
            config.GlobalQualityWeight = math.saturate(settings.GlobalQualityWeight);
            config.HeightScaleMeters = 1f;
            config.Flags = TopographyForgeConstants.RollbackExcludedFlag;
            return config;
        }

        private static TectonicRiftSegmentDTO BuildPreviewRift(TopographyBakeSettings settings)
        {
            TectonicRiftSegmentDTO rift = default;
            rift.StartAupXZ = new double2(settings.WorldOriginAup.x - 500.0, settings.WorldOriginAup.z - 240.0);
            rift.EndAupXZ = new double2(settings.WorldOriginAup.x + 500.0, settings.WorldOriginAup.z + 210.0);
            rift.WidthMeters = math.max(1f, settings.RiftWidthMeters * 0.18f);
            rift.DepthMeters = math.max(0f, settings.RiftDepthMeters);
            rift.EdgeSharpness = 1f;
            rift.FalloffPower = 2.35f;
            rift.SeedHash = (settings.WorldSeed == 0u ? 0x53483234u : settings.WorldSeed) ^ 0xA24000FFu;
            return rift;
        }

        private static FractalParamsDTO BuildRidge(TopographyBakeSettings settings)
        {
            FractalParamsDTO ridge = default;
            ridge.Frequency = math.max(0.0000001f, settings.RidgeFrequency);
            ridge.Amplitude = math.max(0f, settings.RidgeAmplitude);
            ridge.Lacunarity = math.max(1.0001f, settings.RidgeLacunarity);
            ridge.Persistence = math.saturate(settings.RidgePersistence);
            ridge.Octaves = math.clamp(settings.RidgeOctaves, 1, 12);
            ridge.SeedHash = (settings.WorldSeed == 0u ? 0x53483234u : settings.WorldSeed) ^ 0x52494447u;
            return TopographyQualityMath.ApplyRidgeQuality(ridge, settings.GlobalQualityWeight);
        }

        private static DomainWarpParamsDTO BuildWarp(TopographyBakeSettings settings)
        {
            DomainWarpParamsDTO warp = default;
            warp.Frequency = math.max(0.0000001f, settings.WarpFrequency);
            warp.StrengthMeters = math.max(0f, settings.WarpStrengthMeters);
            warp.Lacunarity = 1.92f;
            warp.Persistence = 0.58f;
            warp.Octaves = 4;
            warp.SeedHash = (settings.WorldSeed == 0u ? 0x53483234u : settings.WorldSeed) ^ 0x57415250u;
            return TopographyQualityMath.ApplyWarpQuality(warp, settings.GlobalQualityWeight);
        }

        private static void CopyHeightsToTexture(NativeArray<float> heights, NativeArray<Color32> pixels, int resolution)
        {
            float min = float.PositiveInfinity;
            float max = float.NegativeInfinity;
            for (int i = 0; i < heights.Length; i++)
            {
                float h = heights[i];
                if (!math.isfinite(h))
                    continue;
                min = math.min(min, h);
                max = math.max(max, h);
            }

            if (!math.isfinite(min) || !math.isfinite(max) || max <= min)
            {
                min = -5200f;
                max = 1800f;
            }

            float invRange = math.rcp(math.max(0.001f, max - min));
            for (int z = 0; z < resolution; z++)
            {
                for (int x = 0; x < resolution; x++)
                {
                    int sourceIndex = x + z * resolution;
                    int targetIndex = x + (resolution - 1 - z) * resolution;
                    float source = heights[sourceIndex];
                    if (!math.isfinite(source))
                        source = min;
                    byte value = (byte)math.clamp(math.round((source - min) * invRange * 255f), 0f, 255f);
                    pixels[targetIndex] = new Color32(value, value, value, 255);
                }
            }

            _texture.SetPixelData(pixels, 0);
            _texture.Apply(false, false);
        }
    }
}
#endif
