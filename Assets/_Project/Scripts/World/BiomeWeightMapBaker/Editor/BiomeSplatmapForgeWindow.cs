#if UNITY_EDITOR
using System;
using System.IO;
using Unity.Collections;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Hecton8.World.BiomeWeightMapBaker.Editor
{
    public sealed class BiomeSplatmapForgeWindow : EditorWindow
    {
        private const string CsvPath = "Assets/_SourceData/Terrain/terrain_splatmap_profiles.csv";
        private FixedList4096Bytes<BiomeBlendRuleDTO> _activeRules;
        private IntegerField _resolutionField;
        private FloatField _cellSizeField;
        private FloatField _heightScaleField;
        private Slider _rockSlopeMinSlider;
        private Slider _sandHeightMinSlider;
        private Slider _siltHeightMaxSlider;
        private Slider _noiseStrengthSlider;
        private SliderInt _blurRadiusSlider;
        private Slider _erosionThresholdSlider;
        private Slider _qualityWeightSlider;
        private Label _sourcePathLabel;
        private Label _outputPathLabel;
        private Label _schemaLabel;
        private Label _layoutLabel;
        private Label _statusLabel;
        private Image _previewImage;
        private Texture2D _previewTexture;
        private int _activeRuleSetCount;
        private bool _csvProfileLoaded;
        private bool _bakeInFlight;

        [MenuItem("HECTON-8/Biome Splatmap Forge/Open Forge")]
        public static void Open()
        {
            GetWindow<BiomeSplatmapForgeWindow>("Biome Splatmap Forge");
        }

        public void CreateGUI()
        {
            BiomeWeightMapBakePipeline.FillDefaultRules(ref _activeRules);
            _activeRuleSetCount = 1;
            _csvProfileLoaded = false;
            rootVisualElement.style.paddingLeft = 8;
            rootVisualElement.style.paddingRight = 8;
            rootVisualElement.style.paddingTop = 8;
            rootVisualElement.style.paddingBottom = 8;

            _resolutionField = new IntegerField("Bake Resolution") { value = BiomeWeightMapBakeConstants.DefaultResolution };
            _cellSizeField = new FloatField("Cell Size Meters") { value = 4f };
            _heightScaleField = new FloatField("Height Scale Meters") { value = 2400f };
            _rockSlopeMinSlider = new Slider("Rock Slope Threshold", 0f, 80f) { value = 34f };
            _sandHeightMinSlider = new Slider("Sand Min Height", 0f, 1f) { value = 0.34f };
            _siltHeightMaxSlider = new Slider("Silt Max Height", 0f, 1f) { value = 0.46f };
            _noiseStrengthSlider = new Slider("Noise Perturbation", 0f, 0.5f) { value = 0.16f };
            _blurRadiusSlider = new SliderInt("Blur Radius", 0, 8) { value = 1 };
            _erosionThresholdSlider = new Slider("Erosion Alpha Threshold", 0f, 1f) { value = 0.42f };
            _qualityWeightSlider = new Slider("Global Quality Weight", 0f, 1f) { value = 1f };

            rootVisualElement.Add(_resolutionField);
            rootVisualElement.Add(_cellSizeField);
            rootVisualElement.Add(_heightScaleField);
            rootVisualElement.Add(_rockSlopeMinSlider);
            rootVisualElement.Add(_sandHeightMinSlider);
            rootVisualElement.Add(_siltHeightMaxSlider);
            rootVisualElement.Add(_noiseStrengthSlider);
            rootVisualElement.Add(_blurRadiusSlider);
            rootVisualElement.Add(_erosionThresholdSlider);
            rootVisualElement.Add(_qualityWeightSlider);

            VisualElement buttons = new VisualElement();
            buttons.style.flexDirection = FlexDirection.Row;
            buttons.Add(new Button(LoadCsvProfile) { text = "LOAD CSV PROFILE" });
            buttons.Add(new Button(RefreshPreview) { text = "PREVIEW 1KM PATCH" });
            buttons.Add(new Button(BakeSplatmaps) { text = "BAKE SPLATMAPS" });
            buttons.Add(new Button(Terrain_Shader_Scanner.RunAndWriteReport) { text = "RUN SHADER SCANNER" });
            rootVisualElement.Add(buttons);

            _sourcePathLabel = new Label("CSV Source: " + CsvPath);
            _outputPathLabel = new Label("Output: " + BiomeWeightMapBakePipeline.OutputFolder + "/" + BiomeWeightMapBakePipeline.DefaultAssetName);
            _schemaLabel = new Label("CSV Schema v" + BiomeSplatmapProfileCsvParser.CsvSchemaVersion + ": " + BiomeSplatmapProfileCsvParser.SchemaColumns + " | not loaded");
            _layoutLabel = new Label("DTO Layout: BiomeBlendRuleDTO=32B, BiomeSplatmapBakeConfigDTO=128B, BiomeSplatmapBakeTelemetryEntry=64B");
            rootVisualElement.Add(_sourcePathLabel);
            rootVisualElement.Add(_outputPathLabel);
            rootVisualElement.Add(_schemaLabel);
            rootVisualElement.Add(_layoutLabel);

            _previewImage = new Image();
            _previewImage.style.height = 256;
            _previewImage.style.marginTop = 8;
            _previewImage.scaleMode = ScaleMode.ScaleToFit;
            rootVisualElement.Add(_previewImage);

            _statusLabel = new Label("No bake run in this editor session.");
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

        private void LoadCsvProfile()
        {
            FixedList4096Bytes<BiomeBlendRuleDTO> loadedRules = default;
            if (!BiomeSplatmapProfileCsvParser.TryLoadRules(CsvPath, ref loadedRules, out int ruleCount, out uint schemaHash, out int ruleSetCount, out int validationCode))
            {
                _schemaLabel.text = "CSV Schema v" + BiomeSplatmapProfileCsvParser.CsvSchemaVersion + ": validation failed code " + validationCode.ToString();
                _statusLabel.text = "CSV rejected: " + CsvPath;
                return;
            }

            _activeRules = loadedRules;
            _activeRuleSetCount = math.max(1, ruleSetCount);
            _csvProfileLoaded = true;
            _schemaLabel.text = "CSV Schema v" + BiomeSplatmapProfileCsvParser.CsvSchemaVersion + ": hash 0x" + schemaHash.ToString("X8") + " | rows " + ruleCount + " | rule sets " + ruleSetCount;
            _statusLabel.text = "Loaded CSV rules: " + ruleCount + " | rule sets " + ruleSetCount + " | schema 0x" + schemaHash.ToString("X8");
            RefreshPreview();
        }

        private void RefreshPreview()
        {
            if (_previewTexture != null)
                DestroyImmediate(_previewTexture);

            BiomeSplatmapBakeConfigDTO config = BuildConfig(BiomeWeightMapBakeConstants.PreviewResolution, true);
            FixedList4096Bytes<BiomeBlendRuleDTO> rules = _csvProfileLoaded ? _activeRules : BuildRulesFromSliders();
            _previewTexture = BiomeWeightMapBakePipeline.BakePreviewTexture(config, in rules);
            _previewImage.image = _previewTexture;
            if (_statusLabel != null)
                _statusLabel.text = "Preview refreshed. R=Rock, G=Sand, B=Silt, A=Erosion.";
        }

        private void BakeSplatmaps()
        {
            if (_bakeInFlight)
            {
                _statusLabel.text = "Bake already running.";
                return;
            }

            _bakeInFlight = true;
            try
            {
                BiomeSplatmapBakeConfigDTO config = BuildConfig(_resolutionField.value, false);
                FixedList4096Bytes<BiomeBlendRuleDTO> rules = _csvProfileLoaded ? _activeRules : BuildRulesFromSliders();
                bool baked = BiomeWeightMapBakePipeline.BakeMockSector(config, in rules, "TX_BiomeWeightMap_SHINOBU_243.asset", out BiomeSplatmapBakeResult result);
                _statusLabel.text = baked
                    ? "Baked " + result.AssetPath + " | " + result.Width + "x" + result.Height + " | BC7 " + result.Bc7Compressed + " | warnings 0x" + result.WarningFlags.ToString("X8")
                    : "Bake failed. See Console and Docs/AgentLogs/Dump_SHINOBU_243.bin if emitted.";
            }
            finally
            {
                _bakeInFlight = false;
            }
        }

        private BiomeSplatmapBakeConfigDTO BuildConfig(int resolution, bool previewPatch)
        {
            BiomeSplatmapBakeConfigDTO config = BiomeWeightMapBakePipeline.DefaultConfig(resolution);
            config.CellSizeMeters = math.max(0.001f, _cellSizeField.value);
            config.HeightScaleMeters = math.max(0.001f, _heightScaleField.value);
            config.NoiseStrength = math.max(0f, _noiseStrengthSlider.value);
            config.BlurRadiusPixels = math.max(0, _blurRadiusSlider.value);
            config.ErosionOverrideThreshold = math.saturate(_erosionThresholdSlider.value);
            config.GlobalQualityWeight = math.saturate(_qualityWeightSlider.value);
            config.RulesPerMacro = BiomeWeightMapBakeConstants.DefaultRulesPerMacro;
            config.RuleSetCount = _csvProfileLoaded ? math.max(1, _activeRuleSetCount) : 1;
            config.MacroWidth = _csvProfileLoaded ? math.max(1, _activeRuleSetCount) : 1;
            config.MacroHeight = 1;
            if (previewPatch)
                ApplySceneViewPreviewPatch(ref config);
            return config;
        }

        private static void ApplySceneViewPreviewPatch(ref BiomeSplatmapBakeConfigDTO config)
        {
            const double patchMeters = 1000.0d;
            double halfPatch = patchMeters * 0.5d;
            config.CellSizeMeters = (float)(patchMeters / math.max(1, config.Width));

            double3 centerAup = config.SectorOriginAUP + new double3(halfPatch, 0.0d, halfPatch);
            SceneView sceneView = SceneView.lastActiveSceneView;
            if (sceneView != null && sceneView.camera != null)
            {
                Vector3 cameraPosition = sceneView.camera.transform.position;
                double3 defaultOrigin = BiomeWeightMapBakePipeline.DefaultConfig(config.Width).SectorOriginAUP;
                centerAup = defaultOrigin + new double3(cameraPosition.x, 0.0d, cameraPosition.z);
            }

            config.SectorOriginAUP = new double3(centerAup.x - halfPatch, config.SectorOriginAUP.y, centerAup.z - halfPatch);
        }

        private FixedList4096Bytes<BiomeBlendRuleDTO> BuildRulesFromSliders()
        {
            FixedList4096Bytes<BiomeBlendRuleDTO> rules = default;
            BiomeWeightMapBakePipeline.FillDefaultRules(ref rules);

            BiomeBlendRuleDTO rock = rules[0];
            rock.MinSlope = math.max(0f, _rockSlopeMinSlider.value);
            rock.MaxSlope = 90f;
            rules[0] = rock;

            BiomeBlendRuleDTO sand = rules[1];
            sand.MinHeight = math.saturate(_sandHeightMinSlider.value);
            rules[1] = sand;

            BiomeBlendRuleDTO silt = rules[2];
            silt.MaxHeight = math.saturate(_siltHeightMaxSlider.value);
            rules[2] = silt;

            BiomeBlendRuleDTO erosion = rules[3];
            erosion.MaxHeight = math.saturate(_siltHeightMaxSlider.value + 0.12f);
            rules[3] = erosion;
            return rules;
        }
    }

    public static class BiomeSplatmapProfileCsvParser
    {
        public const int CsvSchemaVersion = 1;
        public const string SchemaColumns = "macro,channel,min_height,max_height,min_slope,max_slope,noise_frequency,blend_softness";
        public const int CsvValidationOk = 0;
        public const int CsvErrorMissing = 1001;
        public const int CsvErrorHeaderMissing = 1002;
        public const int CsvErrorHeaderMismatch = 1003;
        public const int CsvErrorLineOverflow = 1004;
        public const int CsvErrorMalformedRow = 1005;
        public const int CsvErrorNoRules = 1006;

        public static bool TryLoadRules(
            string path,
            ref FixedList4096Bytes<BiomeBlendRuleDTO> output,
            out int ruleCount,
            out uint schemaHash,
            out int ruleSetCount,
            out int validationCode)
        {
            ruleCount = 0;
            ruleSetCount = 0;
            schemaHash = 2166136261u;
            validationCode = CsvValidationOk;
            output.Clear();
            if (!File.Exists(path))
            {
                validationCode = CsvErrorMissing;
                return false;
            }

            int writtenMax = 0;
            Span<byte> lineBuffer = stackalloc byte[4096];
            int lineLength = 0;
            bool lineOverflow = false;
            bool overflowObserved = false;
            bool headerSeen = false;
            bool headerRejected = false;
            bool malformedObserved = false;
            using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                while (true)
                {
                    int read = stream.ReadByte();
                    if (read < 0 || read == (byte)'\n')
                    {
                        if (!lineOverflow)
                        {
                            ReadOnlySpan<byte> line = Trim(SkipUtf8Bom(lineBuffer.Slice(0, lineLength)));
                            if (line.Length != 0 && line[0] != (byte)'#')
                            {
                                if (!headerSeen)
                                {
                                    headerSeen = true;
                                    if (!IsSupportedHeader(line))
                                        headerRejected = true;
                                    else
                                        schemaHash = HashLine(line, schemaHash);
                                }
                                else if (!ProcessCsvRuleLine(line, ref output, ref writtenMax, ref ruleSetCount, ref schemaHash))
                                {
                                    malformedObserved = true;
                                }
                            }
                        }
                        else
                        {
                            overflowObserved = true;
                        }

                        lineLength = 0;
                        lineOverflow = false;
                        if (read < 0)
                            break;

                        continue;
                    }

                    if (lineLength < lineBuffer.Length)
                    {
                        lineBuffer[lineLength] = (byte)read;
                        lineLength++;
                    }
                    else
                    {
                        lineOverflow = true;
                    }
                }
            }

            if (overflowObserved)
            {
                output.Clear();
                ruleCount = 0;
                ruleSetCount = 0;
                validationCode = CsvErrorLineOverflow;
                return false;
            }

            if (!headerSeen)
            {
                validationCode = CsvErrorHeaderMissing;
                return false;
            }

            if (headerRejected)
            {
                output.Clear();
                validationCode = CsvErrorHeaderMismatch;
                return false;
            }

            if (malformedObserved)
            {
                output.Clear();
                validationCode = CsvErrorMalformedRow;
                return false;
            }

            ruleCount = writtenMax;
            if (ruleCount <= 0)
            {
                validationCode = CsvErrorNoRules;
                return false;
            }

            for (int i = 0; i < ruleCount; i++)
            {
                BiomeBlendRuleDTO rule = output[i];
                if (rule.MaxHeight <= 0f && rule.MaxSlope <= 0f)
                    output[i] = BiomeWeightMapBakePipeline.CreateDefaultRule(i);
            }

            ruleSetCount = math.max(1, ruleSetCount);
            validationCode = CsvValidationOk;
            return true;
        }

        private static bool ProcessCsvRuleLine(
            ReadOnlySpan<byte> line,
            ref FixedList4096Bytes<BiomeBlendRuleDTO> output,
            ref int writtenMax,
            ref int ruleSetCount,
            ref uint schemaHash)
        {
            if (line.Length == 0 || line[0] == (byte)'#')
                return true;

            schemaHash = HashLine(line, schemaHash);
            if (!TryParseRule(line, out int macro, out BiomeBlendRuleDTO rule))
                return false;

            int index = macro * BiomeWeightMapBakeConstants.DefaultRulesPerMacro + (int)(rule.ChannelIndex & 3u);
            if ((uint)index >= (uint)BiomeWeightMapBakeConstants.MaxRuleCount)
                return false;

            EnsureRuleSlot(ref output, index);
            output[index] = rule;
            writtenMax = math.max(writtenMax, index + 1);
            ruleSetCount = math.max(ruleSetCount, macro + 1);
            return true;
        }

        private static void EnsureRuleSlot(ref FixedList4096Bytes<BiomeBlendRuleDTO> output, int index)
        {
            while (output.Length <= index && output.Length < BiomeWeightMapBakeConstants.MaxRuleCount)
                output.Add(BiomeWeightMapBakePipeline.CreateDefaultRule(output.Length));
        }

        private static bool TryParseRule(ReadOnlySpan<byte> line, out int macro, out BiomeBlendRuleDTO rule)
        {
            macro = 0;
            rule = default;
            if (!TryParseInt(GetCell(line, 0), out macro))
                return false;

            ReadOnlySpan<byte> channel = GetCell(line, 1);
            if (GetCell(line, 8).Length != 0 || !TryResolveChannel(channel, out uint channelIndex))
                return false;

            if (!TryParseFloat(GetCell(line, 2), out float minHeight) ||
                !TryParseFloat(GetCell(line, 3), out float maxHeight) ||
                !TryParseFloat(GetCell(line, 4), out float minSlope) ||
                !TryParseFloat(GetCell(line, 5), out float maxSlope) ||
                !TryParseFloat(GetCell(line, 6), out float noiseFrequency) ||
                !TryParseFloat(GetCell(line, 7), out float blendSoftness))
            {
                return false;
            }

            rule = new BiomeBlendRuleDTO
            {
                MinHeight = math.saturate(minHeight),
                MaxHeight = math.saturate(maxHeight),
                MinSlope = math.clamp(minSlope, 0f, 90f),
                MaxSlope = math.clamp(maxSlope, 0f, 90f),
                NoiseFrequency = math.max(0f, noiseFrequency),
                BlendSoftness = math.max(0.0001f, blendSoftness),
                ChannelIndex = channelIndex,
                _pad0 = 0u
            };
            return true;
        }

        private static ReadOnlySpan<byte> GetCell(ReadOnlySpan<byte> line, int targetColumn)
        {
            int column = 0;
            int start = 0;
            for (int i = 0; i <= line.Length; i++)
            {
                if (i != line.Length && line[i] != (byte)',')
                    continue;

                if (column == targetColumn)
                    return Trim(line.Slice(start, i - start));

                start = i + 1;
                column++;
            }

            return ReadOnlySpan<byte>.Empty;
        }

        private static bool TryParseInt(ReadOnlySpan<byte> span, out int value)
        {
            value = 0;
            span = Trim(span);
            if (span.Length == 0)
                return false;

            int index = 0;
            int sign = 1;
            if (span[0] == (byte)'-' || span[0] == (byte)'+')
            {
                sign = span[0] == (byte)'-' ? -1 : 1;
                index++;
            }

            bool any = false;
            while (index < span.Length && span[index] >= (byte)'0' && span[index] <= (byte)'9')
            {
                any = true;
                int digit = span[index] - (byte)'0';
                if (value > (int.MaxValue - digit) / 10)
                    return false;
                value = value * 10 + digit;
                index++;
            }

            value *= sign;
            return any && index == span.Length;
        }

        private static bool TryParseFloat(ReadOnlySpan<byte> span, out float value)
        {
            value = 0f;
            span = Trim(span);
            if (span.Length == 0)
                return false;

            int index = 0;
            float sign = 1f;
            if (span[index] == (byte)'-' || span[index] == (byte)'+')
            {
                sign = span[index] == (byte)'-' ? -1f : 1f;
                index++;
            }

            double integer = 0d;
            bool anyDigit = false;
            while (index < span.Length && span[index] >= (byte)'0' && span[index] <= (byte)'9')
            {
                anyDigit = true;
                integer = integer * 10d + span[index] - (byte)'0';
                if (integer > float.MaxValue)
                    return false;
                index++;
            }

            double fraction = 0d;
            double divisor = 1d;
            if (index < span.Length && span[index] == (byte)'.')
            {
                index++;
                while (index < span.Length && span[index] >= (byte)'0' && span[index] <= (byte)'9')
                {
                    anyDigit = true;
                    fraction = fraction * 10d + span[index] - (byte)'0';
                    divisor *= 10d;
                    if (fraction > float.MaxValue || divisor > 1000000000000000d)
                        return false;
                    index++;
                }
            }

            if (!anyDigit || index != span.Length)
                return false;

            double parsed = (integer + fraction / divisor) * sign;
            if (parsed > float.MaxValue || parsed < -float.MaxValue)
                return false;

            value = (float)parsed;
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static bool TryResolveChannel(ReadOnlySpan<byte> value, out uint channel)
        {
            channel = 1u;
            if (EqualsAscii(value, "rock") || EqualsAscii(value, "r"))
            {
                channel = 0u;
                return true;
            }

            if (EqualsAscii(value, "sand") || EqualsAscii(value, "g"))
            {
                channel = 1u;
                return true;
            }

            if (EqualsAscii(value, "silt") || EqualsAscii(value, "b"))
            {
                channel = 2u;
                return true;
            }

            if (EqualsAscii(value, "erosion") || EqualsAscii(value, "a"))
            {
                channel = 3u;
                return true;
            }

            return false;
        }

        private static bool IsSupportedHeader(ReadOnlySpan<byte> value)
        {
            return EqualsAscii(GetCell(value, 0), "macro") &&
                   EqualsAscii(GetCell(value, 1), "channel") &&
                   EqualsAscii(GetCell(value, 2), "min_height") &&
                   EqualsAscii(GetCell(value, 3), "max_height") &&
                   EqualsAscii(GetCell(value, 4), "min_slope") &&
                   EqualsAscii(GetCell(value, 5), "max_slope") &&
                   EqualsAscii(GetCell(value, 6), "noise_frequency") &&
                   EqualsAscii(GetCell(value, 7), "blend_softness") &&
                   GetCell(value, 8).Length == 0;
        }

        private static bool EqualsAscii(ReadOnlySpan<byte> value, string ascii)
        {
            if (value.Length != ascii.Length)
                return false;

            for (int i = 0; i < ascii.Length; i++)
            {
                if (ToLowerAscii(value[i]) != ToLowerAscii((byte)ascii[i]))
                    return false;
            }

            return true;
        }

        private static byte ToLowerAscii(byte value)
        {
            return value >= (byte)'A' && value <= (byte)'Z' ? (byte)(value + 32) : value;
        }

        private static ReadOnlySpan<byte> Trim(ReadOnlySpan<byte> span)
        {
            int start = 0;
            int end = span.Length - 1;
            while (start <= end && IsWhitespace(span[start]))
                start++;
            while (end >= start && IsWhitespace(span[end]))
                end--;
            return start <= end ? span.Slice(start, end - start + 1) : ReadOnlySpan<byte>.Empty;
        }

        private static ReadOnlySpan<byte> SkipUtf8Bom(ReadOnlySpan<byte> span)
        {
            return span.Length >= 3 &&
                   span[0] == 0xEF &&
                   span[1] == 0xBB &&
                   span[2] == 0xBF
                ? span.Slice(3)
                : span;
        }

        private static bool IsWhitespace(byte value)
        {
            return value == (byte)' ' ||
                   value == (byte)'\t' ||
                   value == (byte)'\r' ||
                   value == (byte)'\n';
        }

        private static uint HashLine(ReadOnlySpan<byte> value, uint hash)
        {
            for (int i = 0; i < value.Length; i++)
                hash = BiomeWeightMapBakeMath.Mix(hash ^ value[i]);
            return hash == 0u ? 1u : hash;
        }
    }
}
#endif
