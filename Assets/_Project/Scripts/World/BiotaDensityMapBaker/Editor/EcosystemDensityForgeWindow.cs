#if UNITY_EDITOR
using System;
using System.IO;
using Unity.Collections;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Hecton8.World.BiotaDensityMapBaker.Editor
{
    public sealed class EcosystemDensityForgeWindow : EditorWindow
    {
        private FixedList4096Bytes<BiotaSpawnRuleDTO> _rules;
        private FixedList4096Bytes<BiotaRuleWeightDTO> _weights;
        private IntegerField _resolutionField;
        private Slider _noiseFrequencySlider;
        private Slider _thermalFalloffSlider;
        private Slider _densityMultiplierSlider;
        private Slider _noiseOffsetSlider;
        private Slider _qualityWeightSlider;
        private FloatField _cellSizeField;
        private Label _sourceLabel;
        private Label _outputLabel;
        private Label _schemaLabel;
        private Label _layoutLabel;
        private Label _statusLabel;
        private Image _previewImage;
        private Texture2D _previewTexture;
        private bool _csvLoaded;
        private bool _bakeInFlight;

        [MenuItem("HECTON-8/Ecosystem Density Forge/Open Forge")]
        public static void Open()
        {
            GetWindow<EcosystemDensityForgeWindow>("Ecosystem Density Forge");
        }

        public void CreateGUI()
        {
            BiotaDensityBakePipeline.FillDefaultRules(ref _rules, ref _weights);
            _csvLoaded = false;
            rootVisualElement.style.paddingLeft = 8;
            rootVisualElement.style.paddingRight = 8;
            rootVisualElement.style.paddingTop = 8;
            rootVisualElement.style.paddingBottom = 8;

            _resolutionField = new IntegerField("Bake Resolution") { value = BiotaDensityBakeConstants.DefaultResolution };
            _cellSizeField = new FloatField("Cell Size Meters") { value = BiotaDensityBakeConstants.DefaultCellSizeMeters };
            _noiseFrequencySlider = new Slider("Noise Clustering Frequency", 0.0001f, 0.02f) { value = BiotaDensityBakeConstants.DefaultNoiseFrequency };
            _thermalFalloffSlider = new Slider("Thermal Falloff Distance", 40f, 2000f) { value = BiotaDensityBakeConstants.DefaultThermalFalloffMeters };
            _densityMultiplierSlider = new Slider("Global Density Multiplier", 0f, 4f) { value = BiotaDensityBakeConstants.DefaultDensityMultiplier };
            _noiseOffsetSlider = new Slider("Noise Offset", -0.5f, 1f) { value = BiotaDensityBakeConstants.DefaultNoiseOffset };
            _qualityWeightSlider = new Slider("Global Quality Weight Preview", 0f, 1f) { value = 1f };

            rootVisualElement.Add(_resolutionField);
            rootVisualElement.Add(_cellSizeField);
            rootVisualElement.Add(_noiseFrequencySlider);
            rootVisualElement.Add(_thermalFalloffSlider);
            rootVisualElement.Add(_densityMultiplierSlider);
            rootVisualElement.Add(_noiseOffsetSlider);
            rootVisualElement.Add(_qualityWeightSlider);

            VisualElement buttons = new VisualElement();
            buttons.style.flexDirection = FlexDirection.Row;
            buttons.Add(new Button(LoadCsvRules) { text = "LOAD CSV RULES" });
            buttons.Add(new Button(RefreshPreview) { text = "PREVIEW 1KM PATCH" });
            buttons.Add(new Button(BakeDensityMaps) { text = "BAKE BIOTA HEATMAPS" });
            buttons.Add(new Button(Runtime_Spawner_Scanner.RunAndWriteReport) { text = "RUN SPAWNER SCANNER" });
            rootVisualElement.Add(buttons);

            _sourceLabel = new Label("CSV Source: " + BiotaDensityBakePipeline.DefaultCsvPath);
            _outputLabel = new Label("Output: " + BiotaDensityBakePipeline.OutputFolder + "/" + BiotaDensityBakePipeline.DefaultAssetName);
            _schemaLabel = new Label("CSV Schema v" + BiotaSpawnRuleCsvParser.CsvSchemaVersion + ": " + BiotaSpawnRuleCsvParser.SchemaColumns + " | defaults active");
            _layoutLabel = new Label("DTO Layout: BiotaSpawnRuleDTO=32B, BiotaRuleWeightDTO=32B, RLE Run=8B, Header=128B");
            rootVisualElement.Add(_sourceLabel);
            rootVisualElement.Add(_outputLabel);
            rootVisualElement.Add(_schemaLabel);
            rootVisualElement.Add(_layoutLabel);

            _previewImage = new Image();
            _previewImage.style.height = 256;
            _previewImage.style.marginTop = 8;
            _previewImage.scaleMode = ScaleMode.ScaleToFit;
            rootVisualElement.Add(_previewImage);

            _statusLabel = new Label("No biota density bake run in this editor session.");
            _statusLabel.style.marginTop = 8;
            rootVisualElement.Add(_statusLabel);

            RefreshPreview();
        }

        private void OnDisable()
        {
            if (_previewTexture != null)
                DestroyImmediate(_previewTexture);
            _previewTexture = null;
        }

        private void LoadCsvRules()
        {
            FixedList4096Bytes<BiotaSpawnRuleDTO> loadedRules = default;
            FixedList4096Bytes<BiotaRuleWeightDTO> loadedWeights = default;
            if (!BiotaSpawnRuleCsvParser.TryLoadRules(
                    BiotaDensityBakePipeline.DefaultCsvPath,
                    ref loadedRules,
                    ref loadedWeights,
                    out int ruleCount,
                    out uint schemaHash,
                    out int validationCode))
            {
                _schemaLabel.text = "CSV Schema v" + BiotaSpawnRuleCsvParser.CsvSchemaVersion + ": validation failed code " + validationCode.ToString();
                _statusLabel.text = "CSV rejected: " + BiotaDensityBakePipeline.DefaultCsvPath;
                return;
            }

            _rules = loadedRules;
            _weights = loadedWeights;
            _csvLoaded = true;
            _schemaLabel.text = "CSV Schema v" + BiotaSpawnRuleCsvParser.CsvSchemaVersion + ": hash 0x" + schemaHash.ToString("X8") + " | rows " + ruleCount;
            _statusLabel.text = "Loaded biota rules: " + ruleCount + " | schema 0x" + schemaHash.ToString("X8");
            RefreshPreview();
        }

        private void RefreshPreview()
        {
            if (_previewTexture != null)
                DestroyImmediate(_previewTexture);

            BiotaDensityBakeConfigDTO config = BuildConfig(BiotaDensityBakeConstants.PreviewResolution, true);
            _previewTexture = BiotaDensityBakePipeline.BakePreviewTexture(config, in _rules, in _weights);
            _previewImage.image = _previewTexture;
            if (_statusLabel != null)
                _statusLabel.text = "Preview refreshed " + _previewTexture.width + "x" + _previewTexture.height + ". Green=flora, Blue=fauna, Red=predator/vent pressure.";
        }

        private void BakeDensityMaps()
        {
            _ = BakeDensityMapsAsync();
        }

        private async Awaitable BakeDensityMapsAsync()
        {
            if (_bakeInFlight)
            {
                _statusLabel.text = "Bake already running.";
                return;
            }

            _bakeInFlight = true;
            try
            {
                if (!_csvLoaded && File.Exists(BiotaDensityBakePipeline.DefaultCsvPath))
                    LoadCsvRules();

                BiotaDensityBakeConfigDTO config = BuildConfig(_resolutionField.value, false);
                BiotaDensityBakeResult result = await BiotaDensityBakePipeline.BakeMockSectorAsync(config, _rules, _weights, BiotaDensityBakePipeline.DefaultAssetName);
                await Awaitable.MainThreadAsync();
                if (_statusLabel != null)
                    _statusLabel.text = "Baked " + result.OutputPath + " | " + result.Width + "x" + result.Height + "x" + result.LayerCount + " | RLE " + result.RleRunCount + " | warnings 0x" + result.WarningFlags.ToString("X8");
                AssetDatabase.Refresh();
            }
            catch (Exception exception)
            {
                await Awaitable.MainThreadAsync();
                if (_statusLabel != null)
                    _statusLabel.text = "Bake failed: " + exception.GetType().Name + ". See Console and Docs/AgentLogs/Dump_SHINOBU_308.bin if emitted.";
                Debug.LogError("[SHINOBU_308] Biota density bake failed: " + exception);
            }
            finally
            {
                await Awaitable.MainThreadAsync();
                _bakeInFlight = false;
            }
        }

        private BiotaDensityBakeConfigDTO BuildConfig(int resolution, bool previewPatch)
        {
            BiotaDensityBakeConfigDTO config = BiotaDensityBakePipeline.DefaultConfig(resolution);
            config.CellSizeMeters = math.max(0.001f, _cellSizeField.value);
            config.NoiseFrequency = math.max(0.000001f, _noiseFrequencySlider.value);
            config.ThermalFalloffMeters = math.max(1f, _thermalFalloffSlider.value);
            config.GlobalDensityMultiplier = math.max(0f, _densityMultiplierSlider.value);
            config.NoiseOffset = math.clamp(_noiseOffsetSlider.value, -0.5f, 1f);
            config.GlobalQualityWeight = math.saturate(_qualityWeightSlider.value);
            config.RuleCount = (uint)math.max(1, _rules.Length);
            if (previewPatch)
                ApplySceneViewPreviewPatch(ref config);
            return config;
        }

        private static void ApplySceneViewPreviewPatch(ref BiotaDensityBakeConfigDTO config)
        {
            const double patchMeters = 1000.0d;
            double halfPatch = patchMeters * 0.5d;
            config.CellSizeMeters = (float)(patchMeters / math.max(1, config.Width));
            double3 centerAup = config.SectorOriginAUP + new double3(halfPatch, 0.0d, halfPatch);
            SceneView sceneView = SceneView.lastActiveSceneView;
            if (sceneView != null && sceneView.camera != null)
            {
                Vector3 cameraPosition = sceneView.camera.transform.position;
                double3 defaultOrigin = BiotaDensityBakePipeline.DefaultConfig(config.Width).SectorOriginAUP;
                centerAup = defaultOrigin + new double3(cameraPosition.x, 0.0d, cameraPosition.z);
            }

            config.SectorOriginAUP = new double3(centerAup.x - halfPatch, config.SectorOriginAUP.y, centerAup.z - halfPatch);
        }
    }
}
#endif
