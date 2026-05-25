#if UNITY_EDITOR
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Hecton8.Equipment.Auxiliary.Editor
{
    public static class AuxiliaryEquipmentLayoutValidator
    {
        [MenuItem("HECTON-8/Auxiliary/Validate Auxiliary ABI")]
        public static void ValidateLayoutMenu()
        {
            ValidateLayout();
        }

        public static bool ValidateLayout()
        {
            bool ok = true;
            ok &= UnsafeUtility.SizeOf<DeployedAuxiliaryDTO>() == 64;
            ok &= UnsafeUtility.AlignOf<DeployedAuxiliaryDTO>() >= 8;
            ok &= OffsetOf<DeployedAuxiliaryDTO>(nameof(DeployedAuxiliaryDTO.AUP_Position)) == 0;
            ok &= OffsetOf<DeployedAuxiliaryDTO>(nameof(DeployedAuxiliaryDTO.PrefabHashID)) == 24;
            ok &= OffsetOf<DeployedAuxiliaryDTO>(nameof(DeployedAuxiliaryDTO.RemainingLifetime)) == 28;
            ok &= UnsafeUtility.SizeOf<AuxiliaryStateDTO>() == 16;
            ok &= UnsafeUtility.SizeOf<AuxiliaryFlareLightSignal>() == 64;
            ok &= UnsafeUtility.SizeOf<AuxiliarySonarRequestSignal>() == 64;
            ok &= UnsafeUtility.SizeOf<AuxiliaryTetherConnectionSignal>() == 64;
            if (!ok)
                Debug.LogError("[SHINOBU_229] Auxiliary ABI validation failed.");
            return ok;
        }

        private static int OffsetOf<T>(string fieldName)
        {
            return (int)Marshal.OffsetOf(typeof(T), fieldName);
        }
    }

    public sealed class AuxiliarySystemsXRayWindow : EditorWindow
    {
        private const int HistogramBars = 64;
        private readonly VisualElement[] _bars = new VisualElement[HistogramBars];
        private Label _statusLabel;
        private Slider _qualitySlider;
        private Slider _flareLifetimeSlider;
        private Slider _pingRateSlider;
        private bool _slidersBound;

        [MenuItem("HECTON-8/Auxiliary/Auxiliary Systems X-Ray")]
        public static void Open()
        {
            GetWindow<AuxiliarySystemsXRayWindow>("Auxiliary Systems X-Ray");
        }

        public void CreateGUI()
        {
            rootVisualElement.style.paddingLeft = 8;
            rootVisualElement.style.paddingRight = 8;
            rootVisualElement.style.paddingTop = 8;
            rootVisualElement.style.paddingBottom = 8;

            _statusLabel = new Label("PENDING VERIFICATION");
            rootVisualElement.Add(_statusLabel);

            _qualitySlider = new Slider("GlobalQualityWeight Override", 0f, 1f);
            _flareLifetimeSlider = new Slider("FlareBaseLifetime", 5f, 180f);
            _pingRateSlider = new Slider("PingExpansionRate", 1f, 80f);
            rootVisualElement.Add(_qualitySlider);
            rootVisualElement.Add(_flareLifetimeSlider);
            rootVisualElement.Add(_pingRateSlider);
            _qualitySlider.RegisterValueChangedCallback(_ => ApplyTuningFromSliders());
            _flareLifetimeSlider.RegisterValueChangedCallback(_ => ApplyTuningFromSliders());
            _pingRateSlider.RegisterValueChangedCallback(_ => ApplyTuningFromSliders());

            VisualElement histogram = new VisualElement();
            histogram.style.flexDirection = FlexDirection.Row;
            histogram.style.height = 96;
            histogram.style.marginTop = 8;
            rootVisualElement.Add(histogram);
            for (int i = 0; i < HistogramBars; i++)
            {
                VisualElement bar = new VisualElement();
                bar.style.width = 5;
                bar.style.marginRight = 1;
                bar.style.alignSelf = Align.FlexEnd;
                bar.style.backgroundColor = new Color(0.2f, 0.75f, 1f, 0.95f);
                histogram.Add(bar);
                _bars[i] = bar;
            }

            Button mockButton = new Button(GenerateMock) { text = "Generate 500 Mock Deployments" };
            Button validateButton = new Button(() => AuxiliaryEquipmentLayoutValidator.ValidateLayout()) { text = "Validate ABI" };
            Button scanButton = new Button(() => AuxiliaryOopScanner.RunScan()) { text = "Run OOP Scanner" };
            rootVisualElement.Add(mockButton);
            rootVisualElement.Add(validateButton);
            rootVisualElement.Add(scanButton);
        }

        private void OnInspectorUpdate()
        {
            Refresh();
        }

        private void Refresh()
        {
            if (_statusLabel == null)
                return;

            bool hasTelemetry = AuxiliaryEquipmentRouterRuntime.TryReadTelemetry(out AuxiliaryTelemetryEntry latest);
            _statusLabel.text = hasTelemetry
                ? "Active " + latest.ActiveCount + " | Flare " + latest.FlareSignals + " | Ping " + latest.PingSignals + " | Tether " + latest.TetherSignals + " | Drop " + latest.DroppedSignals + " | Wallus " + latest.CpuMicroseconds.ToString("0.0")
                : "No runtime telemetry";

            if (AuxiliaryEquipmentRouterRuntime.TryReadTuning(out AuxiliaryTuningDTO tuning) && !_slidersBound)
            {
                _qualitySlider.SetValueWithoutNotify(math.saturate(tuning.GlobalQualityWeight));
                _flareLifetimeSlider.SetValueWithoutNotify(math.max(5f, tuning.FlareBaseLifetime));
                _pingRateSlider.SetValueWithoutNotify(math.max(1f, tuning.PingExpansionRate));
                _slidersBound = true;
            }

            if (!AuxiliaryEquipmentRouterRuntime.TryReadDeployments(out var deployments, out int activeCount) || !deployments.IsCreated)
                return;

            int stride = math.max(1, deployments.Length / HistogramBars);
            for (int i = 0; i < HistogramBars; i++)
            {
                int sum = 0;
                int start = i * stride;
                int end = math.min(deployments.Length, start + stride);
                for (int j = start; j < end; j++)
                {
                    DeployedAuxiliaryDTO deployment = deployments[j];
                    if (deployment.PrefabHashID != 0u && deployment.RemainingLifetime > 0f)
                        sum++;
                }

                float h = math.clamp(sum * 8f, 2f, 96f);
                _bars[i].style.height = h;
            }
        }

        private void ApplyTuningFromSliders()
        {
            if (!_slidersBound || !AuxiliaryEquipmentRouterRuntime.TryReadTuning(out AuxiliaryTuningDTO tuning))
                return;

            tuning.GlobalQualityWeight = math.saturate(_qualitySlider.value);
            tuning.Flags |= AuxiliaryTuningFlags.OverrideGlobalQualityWeight;
            tuning.FlareBaseLifetime = math.max(5f, _flareLifetimeSlider.value);
            tuning.PingExpansionRate = math.max(1f, _pingRateSlider.value);
            AuxiliaryEquipmentRouterRuntime.TryWriteTuning(in tuning);
        }

        private static void GenerateMock()
        {
            if (AuxiliaryEquipmentRouterRuntime.TryGetActiveRuntime(out AuxiliaryEquipmentRouterRuntime runtime))
                runtime.GenerateMockDeployments();
        }
    }

    public static class AuxiliaryOopScanner
    {
        private static readonly string[] TargetFiles =
        {
            "Assets/_Project/Scripts/Equipment/Auxiliary",
            "Assets/_Project/Scripts/Gameplay/DeployableFlare.cs",
            "Assets/_Project/Scripts/Gameplay/GravTrap.cs",
            "Assets/_Project/Scripts/GravityTetherTool.cs",
            "Assets/_Project/Scripts/ScannerTool.cs",
            "Assets/_Project/Scripts/TetherManager.cs"
        };

        private static readonly string[] ForbiddenPatterns =
        {
            "Update(",
            "LateUpdate(",
            "FixedUpdate(",
            "new GameObject",
            "AddComponent<Light",
            "SpringJoint",
            "SphereCollider",
            "Light ",
            "ParticleSystem",
            "OverlapSphere"
        };

        [MenuItem("HECTON-8/Auxiliary/Run OOP Scanner")]
        public static void RunScan()
        {
            int findings = 0;
            StringBuilder json = new StringBuilder(4096);
            json.Append("{\n  \"agent\":\"SHINOBU_229\",\n  \"status\":\"");
            int statusInsert = json.Length;
            json.Append("PENDING");
            json.Append("\",\n  \"findings\":[\n");
            bool first = true;
            string root = Directory.GetCurrentDirectory();
            for (int i = 0; i < TargetFiles.Length; i++)
            {
                string target = Path.Combine(root, TargetFiles[i].Replace('/', Path.DirectorySeparatorChar));
                if (Directory.Exists(target))
                {
                    foreach (string file in Directory.EnumerateFiles(target, "*.cs", SearchOption.AllDirectories))
                        ScanFile(file, root, ref findings, ref first, json);
                }
                else if (File.Exists(target))
                {
                    ScanFile(target, root, ref findings, ref first, json);
                }
            }

            json.Append("\n  ]\n}\n");
            string status = findings == 0 ? "Managed Tool Scripts Eradicated" : "Still Present";
            json.Remove(statusInsert, "PENDING".Length);
            json.Insert(statusInsert, status);

            string outputPath = Path.Combine(root, "Docs/Reports/EQUIPMENT_OPTIMIZATION_REPORT.json");
            WriteMergedReport(outputPath, json.ToString());
            AssetDatabase.Refresh();
        }

        private static void ScanFile(string path, string root, ref int findings, ref bool first, StringBuilder json)
        {
            string text = File.ReadAllText(path);
            for (int i = 0; i < ForbiddenPatterns.Length; i++)
            {
                string pattern = ForbiddenPatterns[i];
                int offset = text.IndexOf(pattern, StringComparison.Ordinal);
                while (offset >= 0)
                {
                    if (!IsAllowedEditorTool(path, text, offset))
                    {
                        if (!first)
                            json.Append(",\n");
                        first = false;
                        findings++;
                        json.Append("    {\"file\":\"")
                            .Append(ToProjectPath(path, root))
                            .Append("\",\"pattern\":\"")
                            .Append(Escape(pattern))
                            .Append("\",\"line\":")
                            .Append(ResolveLine(text, offset))
                            .Append('}');
                    }

                    offset = text.IndexOf(pattern, offset + pattern.Length, StringComparison.Ordinal);
                }
            }
        }

        private static bool IsAllowedEditorTool(string path, string text, int offset)
        {
            if (path.IndexOf(Path.DirectorySeparatorChar + "Editor" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) >= 0)
                return true;

            int guardStart = math.max(0, offset - 256);
            string guard = text.Substring(guardStart, offset - guardStart);
            return guard.Contains("#if UNITY_EDITOR");
        }

        private static int ResolveLine(string text, int offset)
        {
            int line = 1;
            for (int i = 0; i < offset && i < text.Length; i++)
            {
                if (text[i] == '\n')
                    line++;
            }

            return line;
        }

        private static string ToProjectPath(string path, string root)
        {
            if (path.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                return path.Substring(root.Length + 1).Replace('\\', '/');
            return path.Replace('\\', '/');
        }

        private static string Escape(string value)
        {
            return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        private static void WriteMergedReport(string outputPath, string reportObject)
        {
            string directory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            string trimmedReport = reportObject.Trim();
            if (File.Exists(outputPath))
            {
                string existing = File.ReadAllText(outputPath);
                int reportsIndex = existing.IndexOf("\"reports\"", StringComparison.Ordinal);
                int arrayStart = reportsIndex >= 0 ? existing.IndexOf('[', reportsIndex) : -1;
                int arrayEnd = existing.LastIndexOf(']');
                if (arrayStart >= 0 && arrayEnd > arrayStart)
                {
                    bool hasExistingItems = existing.IndexOf('{', arrayStart, arrayEnd - arrayStart) >= 0;
                    StringBuilder merged = new StringBuilder(existing.Length + trimmedReport.Length + 32);
                    merged.Append(existing, 0, arrayEnd);
                    if (hasExistingItems)
                        merged.Append(",\n");
                    AppendIndented(merged, trimmedReport, 4);
                    merged.Append(existing, arrayEnd, existing.Length - arrayEnd);
                    File.WriteAllText(outputPath, merged.ToString());
                    return;
                }
            }

            StringBuilder fresh = new StringBuilder(trimmedReport.Length + 32);
            fresh.Append("{\n  \"reports\": [\n");
            AppendIndented(fresh, trimmedReport, 4);
            fresh.Append("\n  ]\n}\n");
            File.WriteAllText(outputPath, fresh.ToString());
        }

        private static void AppendIndented(StringBuilder builder, string value, int spaces)
        {
            for (int i = 0; i < spaces; i++)
                builder.Append(' ');

            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                builder.Append(c);
                if (c == '\n' && i + 1 < value.Length)
                {
                    for (int s = 0; s < spaces; s++)
                        builder.Append(' ');
                }
            }
        }
    }
}
#endif
