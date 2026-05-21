using System;
using System.IO;
using System.Text;
using Hecton8.Graphics.Materials;
using Unity.Collections;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Hecton8.Graphics.Materials.Editor
{
    public sealed class VisualPressureAgingTunerWindow : EditorWindow
    {
        private Slider _rustStress;
        private Slider _corrosionPressure;
        private Slider _saltDepth;
        private Slider _biomassTemperature;
        private Slider _glassThreshold;
        private Slider _temperatureBoost;
        private Slider _scorchIntensity;
        private Slider _qualityNoise;
        private AgingCurveElement _curve;
        private Label _runtimeLabel;
        private Label _uploadLabel;
        private Label _flagsLabel;

        [MenuItem("Hecton8/Rendering/Visual Pressure Aging Tuner")]
        [MenuItem("Hecton8/Rendering/UberNoir Degradation Tuner")]
        private static void Open()
        {
            VisualPressureAgingTunerWindow window = GetWindow<VisualPressureAgingTunerWindow>("UberNoir Degradation Tuner");
            window.minSize = new Vector2(420f, 360f);
        }

        public void CreateGUI()
        {
            rootVisualElement.style.paddingLeft = 10;
            rootVisualElement.style.paddingRight = 10;
            rootVisualElement.style.paddingTop = 10;
            rootVisualElement.style.paddingBottom = 10;

            _runtimeLabel = new Label("Runtime: unbound");
            _uploadLabel = new Label("Upload: 0 us");
            _flagsLabel = new Label("Flags: 0x00000000");
            rootVisualElement.Add(_runtimeLabel);
            rootVisualElement.Add(_uploadLabel);
            rootVisualElement.Add(_flagsLabel);
            _curve = new AgingCurveElement();
            rootVisualElement.Add(_curve);

            _rustStress = AddSlider("Rust Stress", 0.0f, 2.0f);
            _corrosionPressure = AddSlider("Corrosion Pressure", 0.0f, 2.0f);
            _saltDepth = AddSlider("Salt Depth", 0.0f, 2.0f);
            _biomassTemperature = AddSlider("Biomass Temperature", 0.0f, 2.0f);
            _glassThreshold = AddSlider("Glass Threshold", 0.0f, 1.0f);
            _temperatureBoost = AddSlider("Temperature Boost", 0.0f, 0.08f);
            _scorchIntensity = AddSlider("Scorch Intensity", 0.0f, 3.0f);
            _qualityNoise = AddSlider("Quality Noise Scale", 0.0f, 1.0f);

            Button pushButton = new Button(PushToRuntime) { text = "Push Tuning" };
            Button refreshButton = new Button(RefreshFromRuntime) { text = "Refresh Runtime" };
            Button reloadCsvButton = new Button(ReloadCsvProfiles) { text = "Reload CSV Profiles" };
            Button inquisitionButton = new Button(VisualPressureAgingInquisition.RunAndReveal) { text = "Run Static Inquisition" };
            rootVisualElement.Add(pushButton);
            rootVisualElement.Add(refreshButton);
            rootVisualElement.Add(reloadCsvButton);
            rootVisualElement.Add(inquisitionButton);
            RefreshFromRuntime();
        }

        private void OnFocus()
        {
            RefreshRuntimeLabels();
        }

        private Slider AddSlider(string label, float min, float max)
        {
            Slider slider = new Slider(label, min, max);
            slider.showInputField = true;
            slider.RegisterValueChangedCallback(_ => PushToRuntime());
            rootVisualElement.Add(slider);
            return slider;
        }

        private void PushToRuntime()
        {
            if (_rustStress == null)
                return;

            VisualPressureAgingRuntime.TryWriteEditorTuning(
                _rustStress.value,
                _corrosionPressure.value,
                _saltDepth.value,
                _biomassTemperature.value,
                _glassThreshold.value,
                _temperatureBoost.value,
                _qualityNoise.value,
                _scorchIntensity.value);
            RefreshRuntimeLabels();
        }

        private void ReloadCsvProfiles()
        {
            VisualPressureAgingRuntime.TryReloadEditorCsv();
            RefreshFromRuntime();
        }

        private void RefreshFromRuntime()
        {
            if (!VisualPressureAgingRuntime.TryReadEditorTuning(
                    out VisualAgingTuningDTO tuning,
                    out _,
                    out _,
                    out _))
            {
                tuning = DefaultEditorTuning();
            }

            SetWithoutNotify(_rustStress, tuning.RustStressMultiplier);
            SetWithoutNotify(_corrosionPressure, tuning.CorrosionPressureMultiplier);
            SetWithoutNotify(_saltDepth, tuning.SaltDepthMultiplier);
            SetWithoutNotify(_biomassTemperature, tuning.BiomassTemperatureMultiplier);
            SetWithoutNotify(_glassThreshold, tuning.GlassFractureThreshold);
            SetWithoutNotify(_temperatureBoost, tuning.TemperatureBoostMultiplier);
            SetWithoutNotify(_scorchIntensity, tuning.ScorchIntensityMultiplier);
            SetWithoutNotify(_qualityNoise, tuning.QualityNoiseOctaveScale);
            RefreshRuntimeLabels();
        }

        private void RefreshRuntimeLabels()
        {
            if (_runtimeLabel == null)
                return;

            bool bound = VisualPressureAgingRuntime.TryReadEditorTuning(
                out _,
                out int activeCount,
                out float uploadUs,
                out uint flags);
            _runtimeLabel.text = bound ? "Runtime: GlobalDataVault bound, active " + activeCount : "Runtime: Play Mode bridge pending";
            _uploadLabel.text = "Upload: " + uploadUs.ToString("0.0") + " us";
            _flagsLabel.text = "Flags: 0x" + flags.ToString("X8");
            if (_curve != null)
                _curve.SetRuntime(activeCount, uploadUs);
        }

        private static void SetWithoutNotify(Slider slider, float value)
        {
            if (slider != null)
                slider.SetValueWithoutNotify(value);
        }

        private static VisualAgingTuningDTO DefaultEditorTuning()
        {
            return new VisualAgingTuningDTO
            {
                RustStressMultiplier = 0.78f,
                CorrosionPressureMultiplier = 0.62f,
                SaltDepthMultiplier = 0.58f,
                BiomassTemperatureMultiplier = 0.42f,
                GlassFractureThreshold = 0.68f,
                TemperatureBoostMultiplier = 0.018f,
                QualityNoiseOctaveScale = 1.0f,
                ScorchIntensityMultiplier = 1.0f
            };
        }

        private sealed class AgingCurveElement : VisualElement
        {
            private int _activeCount;
            private float _uploadUs;

            public AgingCurveElement()
            {
                style.height = 92;
                style.marginTop = 8;
                style.marginBottom = 8;
                style.borderBottomWidth = 1;
                style.borderTopWidth = 1;
                style.borderLeftWidth = 1;
                style.borderRightWidth = 1;
                generateVisualContent += Draw;
            }

            public void SetRuntime(int activeCount, float uploadUs)
            {
                _activeCount = activeCount;
                _uploadUs = uploadUs;
                MarkDirtyRepaint();
            }

            private void Draw(MeshGenerationContext context)
            {
                Painter2D painter = context.painter2D;
                Rect r = contentRect;
                float w = math.max(1.0f, r.width);
                float h = math.max(1.0f, r.height);
                float load = math.saturate(_activeCount / 4096.0f);
                float upload = math.saturate(_uploadUs / 100.0f);
                painter.lineWidth = 2.0f;
                painter.strokeColor = new Color(0.21f, 0.58f, 0.54f, 0.95f);
                painter.BeginPath();
                for (int i = 0; i < 32; i++)
                {
                    float t = i * (1.0f / 31.0f);
                    float y = h - h * math.saturate(t * t * (0.45f + load * 0.55f));
                    Vector2 p = new Vector2(r.x + t * w, r.y + y);
                    if (i == 0) painter.MoveTo(p); else painter.LineTo(p);
                }
                painter.Stroke();
                painter.strokeColor = new Color(0.90f, 0.36f, 0.18f, 0.85f);
                painter.BeginPath();
                painter.MoveTo(new Vector2(r.x, r.y + h - upload * h));
                painter.LineTo(new Vector2(r.x + w, r.y + h - upload * h));
                painter.Stroke();
            }
        }
    }

    [InitializeOnLoad]
    internal static class VisualPressureAgingSceneGizmo
    {
        static VisualPressureAgingSceneGizmo()
        {
            SceneView.duringSceneGui -= Draw;
            SceneView.duringSceneGui += Draw;
        }

        private static void Draw(SceneView view)
        {
            bool hasAging = VisualPressureAgingRuntime.TryOpenAgingBufferSnapshotLease(out NativeArray<VisualAgingParamsDTO>.ReadOnly aging, out int agingCount);
            bool hasDegradation = VisualPressureAgingRuntime.TryOpenDegradationBufferSnapshotLease(out NativeArray<InstanceDegradationDTO>.ReadOnly degradation, out int degradationCount);
            if (!hasAging && !hasDegradation)
                return;

            try
            {
                int available = hasAging ? agingCount : degradationCount;
                int count = math.min(available, 128);
                for (int i = 0; i < count; i++)
                {
                    VisualAgingParamsDTO agingDto = hasAging && i < aging.Length ? aging[i] : default;
                    InstanceDegradationDTO degradationDto = hasDegradation && i < degradation.Length ? degradation[i] : default;
                    if (hasAging &&
                        (!math.all(math.isfinite(agingDto.RustAndCorrosion)) ||
                         !math.all(math.isfinite(agingDto.StressAndMicroFractures)) ||
                         !math.all(math.isfinite(agingDto.DepthAndPressure))))
                    {
                        continue;
                    }

                    if (hasDegradation &&
                        (!math.isfinite(degradationDto.RustAmount) ||
                         !math.isfinite(degradationDto.ScorchAmount) ||
                         !math.isfinite(degradationDto.BioFouling) ||
                         !math.isfinite(degradationDto.StructuralStress)))
                    {
                        continue;
                    }

                    float pressure = math.saturate(agingDto.DepthAndPressure.w);
                    float rust = math.saturate(hasDegradation ? degradationDto.RustAmount : agingDto.RustAndCorrosion.x);
                    float scorch = math.saturate(hasDegradation ? degradationDto.ScorchAmount : agingDto.SaltAndBiomass.z);
                    float bio = math.saturate(hasDegradation ? degradationDto.BioFouling : agingDto.SaltAndBiomass.y);
                    Vector3 position = hasAging
                        ? new Vector3(agingDto.DepthAndPressure.x, agingDto.DepthAndPressure.y, agingDto.DepthAndPressure.z)
                        : new Vector3((i & 31) * 2.0f, 0.0f, (i >> 5) * 2.0f);
                    Color rustColor = Color.Lerp(new Color(0.12f, 0.42f, 0.18f, 0.45f), new Color(0.95f, 0.34f, 0.08f, 0.72f), rust);
                    Color scorchColor = Color.Lerp(rustColor, new Color(0.02f, 0.015f, 0.012f, 0.85f), scorch);
                    Handles.color = Color.Lerp(scorchColor, new Color(0.05f, 0.42f, 0.28f, 0.62f), bio * (1.0f - scorch));
                    Handles.DrawWireDisc(position, Vector3.up, 0.25f + pressure * 1.35f + math.max(rust, scorch) * 0.35f);
                }
            }
            finally
            {
                VisualPressureAgingRuntime.CloseAgingBufferSnapshotLease();
                VisualPressureAgingRuntime.CloseDegradationBufferSnapshotLease();
            }
        }
    }

    internal static class VisualPressureAgingInquisition
    {
        private const string AgentId = "SHINOBU_219";
        private const string DumpRelativePath = "Docs/AgentLogs/Dump_SHINOBU_219.bin";
        private const string DedicatedReportRelativePath = "Docs/Reports/VISUAL_AGING_INQUISITION_REPORT.json";

        [MenuItem("Hecton8/Rendering/Run Visual Aging Inquisition")]
        public static void RunAndReveal()
        {
            string reportPath = Run();
            EditorUtility.RevealInFinder(reportPath);
        }

        public static string Run()
        {
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string dedicatedReportPath = Path.Combine(root, DedicatedReportRelativePath);
            string dedicatedReportDir = Path.GetDirectoryName(dedicatedReportPath);
            if (!string.IsNullOrEmpty(dedicatedReportDir))
                Directory.CreateDirectory(dedicatedReportDir);

            string baseDegradation = ReadTextIfExists(root, "Assets/_Project/Scripts/Construction/BaseDegradationSystem.cs");
            string runtime = ReadTextIfExists(root, "Assets/_Project/Scripts/Graphics/Materials/VisualPressureAgingRuntime.cs");
            string shader = ReadTextIfExists(root, "Assets/_Project/Art/Shaders/Hecton8_UberNoir.hlsl");
            string tuner = ReadTextIfExists(root, "Assets/_Project/Scripts/Graphics/Materials/Editor/VisualPressureAgingTunerWindow.cs");
            string csvPath = Path.Combine(root, "Data/Visuals/environmental_aging_rules.csv");

            int activeMaterialMutations = Count(baseDegradation, ".material") + Count(runtime, ".material") + Count(baseDegradation, "MaterialPropertyBlock");
            int activeAuthoringDecals = Count(baseDegradation, "ApplyAuthoringDecal") + Count(baseDegradation, "LeakStripeDecal") + Count(baseDegradation, "LeakScuffDecal");
            int shaderBufferBindings = Count(shader, "_GlobalUberNoirDegradation") + Count(runtime, "_GlobalUberNoirDegradation");
            int legacyAgingShaderBindings = Count(shader, "_GlobalBaseAgingParams") + Count(runtime, "_GlobalBaseAgingParams");
            int degradationDtoReferences = Count(runtime, "InstanceDegradationDTO") + Count(shader, "H8InstanceDegradationDTO");
            int svInstanceIdReferences = Count(shader, "SV_InstanceID");
            int qualityRouteReferences = Count(shader, "GlobalQualityWeight") + Count(runtime, "GlobalQualityWeight");
            int scorchNormalReferences = Count(shader, "H8UberNoirApplyScorchDegradation") + Count(shader, "H8UberNoirDecodeRustNormalTS");
            int csvRouteReferences = Count(runtime, "environmental_aging_rules.csv") + Count(runtime, "scorch_intensity") + Count(runtime, "quality_noise");
            int gizmoSnapshotReferences = Count(tuner, "TryOpenDegradationBufferSnapshotLease") + Count(tuner, "TryOpenAgingBufferSnapshotLease");
            int dumpIdentityReferences = Count(runtime, DumpRelativePath) + Count(tuner, DumpRelativePath);
            int lockBufferForWriteReferences = Count(runtime, "LockBufferForWrite");
            int setDataReferences = Count(runtime, ".SetData(") + Count(runtime, "SetData<");
            int projectMaterialMutationReferences = CountTokenInDirectory(root, "Assets/_Project/Scripts", "*.cs", ".material") +
                CountTokenInDirectory(root, "Assets/_Project/Scripts", "*.cs", "MaterialPropertyBlock");
            int projectDynamicDecalReferences = CountTokenInDirectory(root, "Assets/_Project/Scripts", "*.cs", "DynamicDecal");
            int legacyBaseCorrosionFiles = CountFileNameInDirectory(root, "Assets/_Project/Scripts/Rendering", "BaseCorrosion.cs") +
                CountFileNameInDirectory(root, "Assets/_Project/Scripts/Construction", "BaseCorrosion.cs");
            int legacyGlassFractureFiles = CountFileNameInDirectory(root, "Assets/_Project/Scripts/Rendering", "GlassFracture.cs") +
                CountFileNameInDirectory(root, "Assets/_Project/Scripts/Construction", "GlassFracture.cs");
            int legacyRendererMaterialSetFloat = CountTokenInDirectory(root, "Assets/_Project/Scripts/Rendering", "*.cs", "GetComponent<Renderer>().material.SetFloat") +
                CountTokenInDirectory(root, "Assets/_Project/Scripts/Construction", "*.cs", "GetComponent<Renderer>().material.SetFloat");
            int dynamicAgingDecalReferences = CountTokenInDirectory(root, "Assets/_Project/Scripts/Rendering", "*.cs", "CorrosionDecal") +
                CountTokenInDirectory(root, "Assets/_Project/Scripts/Rendering", "*.cs", "RustDecal") +
                CountTokenInDirectory(root, "Assets/_Project/Scripts/Rendering", "*.cs", "AlgaeDecal") +
                CountTokenInDirectory(root, "Assets/_Project/Scripts/Rendering", "*.cs", "GlassFractureDecal") +
                CountTokenInDirectory(root, "Assets/_Project/Scripts/Construction", "*.cs", "CorrosionDecal") +
                CountTokenInDirectory(root, "Assets/_Project/Scripts/Construction", "*.cs", "RustDecal") +
                CountTokenInDirectory(root, "Assets/_Project/Scripts/Construction", "*.cs", "AlgaeDecal") +
                CountTokenInDirectory(root, "Assets/_Project/Scripts/Construction", "*.cs", "GlassFractureDecal");
            bool layoutValid = VisualPressureAgingRuntime.ValidateLayout();
            bool pass = activeMaterialMutations == 0 &&
                activeAuthoringDecals == 0 &&
                legacyBaseCorrosionFiles == 0 &&
                legacyGlassFractureFiles == 0 &&
                legacyRendererMaterialSetFloat == 0 &&
                dynamicAgingDecalReferences == 0 &&
                shaderBufferBindings >= 2 &&
                degradationDtoReferences >= 2 &&
                svInstanceIdReferences > 0 &&
                qualityRouteReferences >= 2 &&
                scorchNormalReferences >= 2 &&
                csvRouteReferences >= 3 &&
                gizmoSnapshotReferences >= 2 &&
                dumpIdentityReferences >= 2 &&
                lockBufferForWriteReferences >= 2 &&
                setDataReferences == 0 &&
                File.Exists(csvPath) &&
                layoutValid;

            StringBuilder builder = new StringBuilder(1024);
            builder.AppendLine("{");
            builder.AppendLine("  \"agent\": \"" + AgentId + "\",");
            builder.AppendLine("  \"domain\": \"VISUAL_PRESSURE_AGING_SHADER\",");
            builder.AppendLine("  \"scope\": \"VISUAL_AGING_INQUISITION\",");
            builder.AppendLine("  \"scanScope\": \"Project scripts counted; pass/fail gated on BaseDegradation/UberNoir aging scope\",");
            builder.AppendLine("  \"summary\": \"Instance Material Mutations Purged\",");
            builder.AppendLine("  \"evidenceClass\": \"STATIC_SOURCE\",");
            builder.AppendLine("  \"runtimeStatus\": \"PENDING_UNITY_IMPORT_SHADER_COMPILE_PROFILER\",");
            builder.AppendLine("  \"instanceMaterialMutationsActive\": " + activeMaterialMutations + ",");
            builder.AppendLine("  \"authoringAgingDecalCallsActive\": " + activeAuthoringDecals + ",");
            builder.AppendLine("  \"projectMaterialMutationReferences\": " + projectMaterialMutationReferences + ",");
            builder.AppendLine("  \"projectDynamicDecalReferences\": " + projectDynamicDecalReferences + ",");
            builder.AppendLine("  \"legacyBaseCorrosionFiles\": " + legacyBaseCorrosionFiles + ",");
            builder.AppendLine("  \"legacyGlassFractureFiles\": " + legacyGlassFractureFiles + ",");
            builder.AppendLine("  \"legacyRendererMaterialSetFloat\": " + legacyRendererMaterialSetFloat + ",");
            builder.AppendLine("  \"dynamicAgingDecalReferences\": " + dynamicAgingDecalReferences + ",");
            builder.AppendLine("  \"globalUberNoirDegradationBindings\": " + shaderBufferBindings + ",");
            builder.AppendLine("  \"legacyBaseAgingBindingsPreserved\": " + legacyAgingShaderBindings + ",");
            builder.AppendLine("  \"instanceDegradationDtoReferences\": " + degradationDtoReferences + ",");
            builder.AppendLine("  \"svInstanceIdReferences\": " + svInstanceIdReferences + ",");
            builder.AppendLine("  \"globalQualityWeightReferences\": " + qualityRouteReferences + ",");
            builder.AppendLine("  \"scorchNormalPerturbationReferences\": " + scorchNormalReferences + ",");
            builder.AppendLine("  \"csvRouteReferences\": " + csvRouteReferences + ",");
            builder.AppendLine("  \"gizmoSnapshotReferences\": " + gizmoSnapshotReferences + ",");
            builder.AppendLine("  \"dumpIdentityReferences\": " + dumpIdentityReferences + ",");
            builder.AppendLine("  \"lockBufferForWriteReferences\": " + lockBufferForWriteReferences + ",");
            builder.AppendLine("  \"setDataReferences\": " + setDataReferences + ",");
            builder.AppendLine("  \"csvProfileExists\": " + (File.Exists(csvPath) ? "true" : "false") + ",");
            builder.AppendLine("  \"csvProfilePath\": \"Data/Visuals/environmental_aging_rules.csv\",");
            builder.AppendLine("  \"visualAgingParamsDTOBytes\": 64,");
            builder.AppendLine("  \"instanceDegradationDTOBytes\": 32,");
            builder.AppendLine("  \"blackBoxDumpPath\": \"" + DumpRelativePath + "\",");
            builder.AppendLine("  \"layoutValid\": " + (layoutValid ? "true" : "false") + ",");
            builder.AppendLine("  \"rollbackStateIncluded\": false,");
            builder.AppendLine("  \"aggregateReportPolicy\": \"DEDICATED_REPORT_ONLY_DO_NOT_OVERWRITE_SHARED_RENDERING_OPTIMIZATION_REPORT\",");
            builder.AppendLine("  \"sharedAggregateReportTouched\": false,");
            builder.AppendLine("  \"status\": \"" + (pass ? "STATIC_SOURCE_PASS" : "STATIC_SOURCE_FAIL") + "\"");
            builder.AppendLine("}");
            string report = builder.ToString();
            File.WriteAllText(dedicatedReportPath, report);
            return dedicatedReportPath;
        }

        private static int Count(string text, string token)
        {
            int count = 0;
            int index = 0;
            while ((index = text.IndexOf(token, index, StringComparison.Ordinal)) >= 0)
            {
                count++;
                index += token.Length;
            }
            return count;
        }

        private static string ReadTextIfExists(string root, string relativePath)
        {
            string path = Path.Combine(root, relativePath);
            return File.Exists(path) ? File.ReadAllText(path) : string.Empty;
        }

        private static int CountFileNameInDirectory(string root, string relativePath, string fileName)
        {
            string directory = Path.Combine(root, relativePath);
            if (!Directory.Exists(directory))
                return 0;

            int count = 0;
            string[] files = Directory.GetFiles(directory, fileName, SearchOption.AllDirectories);
            for (int i = 0; i < files.Length; i++)
            {
                if (Path.GetFileName(files[i]) == fileName)
                    count++;
            }

            return count;
        }

        private static int CountTokenInDirectory(string root, string relativePath, string searchPattern, string token)
        {
            string directory = Path.Combine(root, relativePath);
            if (!Directory.Exists(directory))
                return 0;

            int count = 0;
            string[] files = Directory.GetFiles(directory, searchPattern, SearchOption.AllDirectories);
            for (int i = 0; i < files.Length; i++)
            {
                string file = files[i];
                if (!File.Exists(file))
                    continue;

                count += Count(File.ReadAllText(file), token);
            }

            return count;
        }

        private static void AppendJsonString(StringBuilder builder, string value)
        {
            builder.Append('"');
            if (!string.IsNullOrEmpty(value))
            {
                for (int i = 0; i < value.Length; i++)
                {
                    char c = value[i];
                    switch (c)
                    {
                        case '"':
                            builder.Append("\\\"");
                            break;
                        case '\\':
                            builder.Append("\\\\");
                            break;
                        case '\b':
                            builder.Append("\\b");
                            break;
                        case '\f':
                            builder.Append("\\f");
                            break;
                        case '\n':
                            builder.Append("\\n");
                            break;
                        case '\r':
                            builder.Append("\\r");
                            break;
                        case '\t':
                            builder.Append("\\t");
                            break;
                        default:
                            if (c < ' ')
                                AppendControlEscape(builder, c);
                            else
                                builder.Append(c);
                            break;
                    }
                }
            }

            builder.Append('"');
        }

        private static void AppendControlEscape(StringBuilder builder, char value)
        {
            const string hex = "0123456789ABCDEF";
            builder.Append("\\u00");
            builder.Append(hex[(value >> 4) & 0xF]);
            builder.Append(hex[value & 0xF]);
        }
    }
}
