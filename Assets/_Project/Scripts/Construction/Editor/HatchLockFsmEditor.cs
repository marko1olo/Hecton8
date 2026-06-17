#if UNITY_EDITOR
using System;
using System.IO;
using System.Text;
using Hecton8.Construction;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Hecton8.Construction.Editor
{
    public sealed class ContainmentLockFsmTunerWindow : EditorWindow
    {
        private const string RuntimeInactiveText = "Runtime inactive.";

        private readonly StringBuilder _statusBuilder = new StringBuilder(256);
        private Label _statusLabel;
        private Slider _safePressure;
        private Slider _structuralJam;
        private Slider _catastrophicPressure;
        private HatchTelemetryBarChart _barChart;
        private bool _updatingSliders;
        private bool _lastRuntimeActive;
        private int _lastActiveCount = int.MinValue;
        private int _lastPressureLocked = int.MinValue;
        private int _lastJammed = int.MinValue;
        private int _lastCatastrophic = int.MinValue;
        private uint _lastFrame = uint.MaxValue;
        private float _lastMaxPressure = float.NaN;
        private float _lastAveragePressure = float.NaN;
        private float _lastScheduleUs = float.NaN;

        [MenuItem("Hecton8/Construction/Containment Lock FSM Tuner")]
        public static void Open()
        {
            GetWindow<ContainmentLockFsmTunerWindow>("Containment Lock FSM");
        }

        private void CreateGUI()
        {
            rootVisualElement.Clear();
            rootVisualElement.style.paddingLeft = 10;
            rootVisualElement.style.paddingRight = 10;
            rootVisualElement.style.paddingTop = 10;
            rootVisualElement.style.paddingBottom = 10;

            _statusLabel = new Label(RuntimeInactiveText);
            rootVisualElement.Add(_statusLabel);

            _barChart = new HatchTelemetryBarChart();
            rootVisualElement.Add(_barChart);

            _safePressure = MakeSlider("Safe pressure delta ATM", 0.05f, 3f, HatchLockConstants.DefaultSafePressureDifferentialATM);
            _structuralJam = MakeSlider("Structural jam threshold", 0.01f, 0.95f, HatchLockConstants.DefaultStructuralJamThreshold01);
            _catastrophicPressure = MakeSlider("Catastrophic override ATM", 0.1f, 5f, HatchLockConstants.DefaultCatastrophicPressureDifferentialATM);
            _safePressure.RegisterValueChangedCallback(_ => ApplyTuningFromSliders());
            _structuralJam.RegisterValueChangedCallback(_ => ApplyTuningFromSliders());
            _catastrophicPressure.RegisterValueChangedCallback(_ => ApplyTuningFromSliders());
            rootVisualElement.Add(_safePressure);
            rootVisualElement.Add(_structuralJam);
            rootVisualElement.Add(_catastrophicPressure);

            Button csvButton = new Button(LoadCsvProfiles) { text = "Load hatch_hardware_profiles.csv" };
            rootVisualElement.Add(csvButton);

            Button scannerButton = new Button(OOP_Door_Scanner.Run) { text = "Run OOP Door Scanner" };
            rootVisualElement.Add(scannerButton);
        }

        private void OnInspectorUpdate()
        {
            if (_statusLabel == null)
                return;

            if (BulkheadContainmentRuntime.TryReadHatchEditorState(
                    out int activeCount,
                    out int pressureLocked,
                    out int jammed,
                    out int catastrophic,
                    out float safePressure,
                    out float structuralJam,
                    out float catastrophicPressure,
                    out float maxPressure,
                    out float averagePressure,
                    out float scheduleUs,
                    out uint frame))
            {
                if (_lastRuntimeActive &&
                    _lastActiveCount == activeCount &&
                    _lastPressureLocked == pressureLocked &&
                    _lastJammed == jammed &&
                    _lastCatastrophic == catastrophic &&
                    _lastFrame == frame &&
                    NearlyEqual(_lastMaxPressure, maxPressure, 0.0005f) &&
                    NearlyEqual(_lastAveragePressure, averagePressure, 0.0005f) &&
                    NearlyEqual(_lastScheduleUs, scheduleUs, 0.005f))
                {
                    return;
                }

                _statusBuilder.Clear();
                _statusBuilder.Append("Active: ").Append(activeCount)
                    .Append(" | PressureLocked: ").Append(pressureLocked)
                    .Append(" | Jammed: ").Append(jammed)
                    .Append(" | Flood: ").Append(catastrophic)
                    .Append(" | MaxDelta: ").Append(maxPressure.ToString("0.000"))
                    .Append(" ATM | AvgDelta: ").Append(averagePressure.ToString("0.000"))
                    .Append(" ATM | Schedule: ").Append(scheduleUs.ToString("0.00"))
                    .Append(" us | Frame: ").Append(frame);
                _statusLabel.text = _statusBuilder.ToString();
                _barChart.SetCounts(activeCount, pressureLocked, jammed, catastrophic);
                SetSliderValues(safePressure, structuralJam, catastrophicPressure);
                _lastRuntimeActive = true;
                _lastActiveCount = activeCount;
                _lastPressureLocked = pressureLocked;
                _lastJammed = jammed;
                _lastCatastrophic = catastrophic;
                _lastFrame = frame;
                _lastMaxPressure = maxPressure;
                _lastAveragePressure = averagePressure;
                _lastScheduleUs = scheduleUs;
            }
            else
            {
                if (!_lastRuntimeActive && string.Equals(_statusLabel.text, RuntimeInactiveText, StringComparison.Ordinal))
                    return;

                _statusLabel.text = RuntimeInactiveText;
                _barChart.SetCounts(0, 0, 0, 0);
                _lastRuntimeActive = false;
            }
        }

        private void SetSliderValues(float safePressure, float structuralJam, float catastrophicPressure)
        {
            _updatingSliders = true;
            _safePressure.SetValueWithoutNotify(safePressure);
            _structuralJam.SetValueWithoutNotify(structuralJam);
            _catastrophicPressure.SetValueWithoutNotify(catastrophicPressure);
            _updatingSliders = false;
        }

        private void ApplyTuningFromSliders()
        {
            if (_updatingSliders)
                return;

            BulkheadContainmentRuntime.TryApplyHatchEditorTuning(
                _safePressure.value,
                _structuralJam.value,
                _catastrophicPressure.value);
        }

        private void LoadCsvProfiles()
        {
            string path = EditorUtility.OpenFilePanel("hatch_hardware_profiles.csv", Application.dataPath, "csv");
            if (!string.IsNullOrEmpty(path))
                BulkheadContainmentRuntime.TryLoadHatchProfilesFromCsvFile(path);
        }

        private static Slider MakeSlider(string label, float min, float max, float value)
        {
            Slider slider = new Slider(label, min, max) { value = value };
            slider.showInputField = true;
            return slider;
        }

        private static bool NearlyEqual(float left, float right, float epsilon)
        {
            return float.IsNaN(left) && float.IsNaN(right) ||
                   Math.Abs(left - right) <= epsilon;
        }
    }

    internal sealed class HatchTelemetryBarChart : VisualElement
    {
        private readonly VisualElement _free;
        private readonly VisualElement _pressure;
        private readonly VisualElement _jammed;
        private readonly VisualElement _flood;

        public HatchTelemetryBarChart()
        {
            style.height = 18;
            style.flexDirection = FlexDirection.Row;
            style.marginTop = 8;
            style.marginBottom = 8;
            _free = MakeSegment(new Color(0.05f, 0.72f, 0.28f, 1f));
            _pressure = MakeSegment(new Color(1f, 0.75f, 0.05f, 1f));
            _jammed = MakeSegment(new Color(1f, 0.08f, 0.05f, 1f));
            _flood = MakeSegment(new Color(0.35f, 0.02f, 0.02f, 1f));
            Add(_free);
            Add(_pressure);
            Add(_jammed);
            Add(_flood);
        }

        public void SetCounts(int active, int pressure, int jammed, int flood)
        {
            int lockedTotal = Math.Max(0, pressure + jammed + flood);
            int free = Math.Max(0, active - lockedTotal);
            _free.style.flexGrow = Math.Max(0.001f, free);
            _pressure.style.flexGrow = Math.Max(0.001f, pressure);
            _jammed.style.flexGrow = Math.Max(0.001f, jammed);
            _flood.style.flexGrow = Math.Max(0.001f, flood);
        }

        private static VisualElement MakeSegment(Color color)
        {
            VisualElement segment = new VisualElement();
            segment.style.backgroundColor = color;
            segment.style.height = 18;
            return segment;
        }
    }

    public static class OOP_Door_Scanner
    {
        private const string AggregateReportPath = "Docs/Reports/PHYSICS_OPTIMIZATION_REPORT.json";
        private const string SidecarReportPath = "Docs/Reports/PHYSICS_OPTIMIZATION_REPORT_SHINOBU_343.json";
        private const string MarkdownReportPath = "Docs/Reports/OOP_Door_Scanner_SHINOBU_343.md";
        private const string AggregateKey = "shinobu_343_hatch_lock_fsm";

        [MenuItem("Hecton8/Construction/OOP Door Scanner")]
        public static void Run()
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string scriptsRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "_Project/Scripts"));
            int scannedFiles = 0;
            int suspiciousDoorStateMachines = 0;
            int syntaxNodesVisited = 0;
            int parserFallbackFiles = 0;
            StringBuilder filesJson = new StringBuilder(4096);
            StringBuilder filesMarkdown = new StringBuilder(4096);

            ScanDirectory(Path.Combine(scriptsRoot, "Habitat"), ref scannedFiles, ref suspiciousDoorStateMachines, ref syntaxNodesVisited, ref parserFallbackFiles, filesJson, filesMarkdown);
            ScanDirectory(Path.Combine(scriptsRoot, "Interaction"), ref scannedFiles, ref suspiciousDoorStateMachines, ref syntaxNodesVisited, ref parserFallbackFiles, filesJson, filesMarkdown);
            ScanDirectory(Path.Combine(scriptsRoot, "Construction"), ref scannedFiles, ref suspiciousDoorStateMachines, ref syntaxNodesVisited, ref parserFallbackFiles, filesJson, filesMarkdown);

            string generatedUtc = DateTime.UtcNow.ToString("O");
            string verdict = suspiciousDoorStateMachines == 0
                ? "OOP Door State Machines Eradicated"
                : "OOP Door State Machines Present";
            string sidecarJson = BuildSidecarJson(generatedUtc, verdict, scannedFiles, suspiciousDoorStateMachines, syntaxNodesVisited, parserFallbackFiles, filesJson);
            string markdown = BuildMarkdown(generatedUtc, verdict, scannedFiles, suspiciousDoorStateMachines, syntaxNodesVisited, parserFallbackFiles, filesMarkdown);
            string sidecarFullPath = Path.GetFullPath(Path.Combine(projectRoot, SidecarReportPath));
            string markdownFullPath = Path.GetFullPath(Path.Combine(projectRoot, MarkdownReportPath));
            string aggregateFullPath = Path.GetFullPath(Path.Combine(projectRoot, AggregateReportPath));
            WriteTextAtomic(sidecarFullPath, sidecarJson);
            WriteTextAtomic(markdownFullPath, markdown);
            UpsertAggregateReport(aggregateFullPath, BuildAggregateJson(generatedUtc, verdict, scannedFiles, suspiciousDoorStateMachines, syntaxNodesVisited, parserFallbackFiles));
            AssetDatabase.Refresh();
            Hecton8.Core.H8Debug.Log("SHINOBU_343 OOP Door Scanner verdict: " + verdict);
        }

        private static void ScanDirectory(
            string directory,
            ref int scannedFiles,
            ref int suspiciousDoorStateMachines,
            ref int syntaxNodesVisited,
            ref int parserFallbackFiles,
            StringBuilder filesJson,
            StringBuilder filesMarkdown)
        {
            if (!Directory.Exists(directory))
                return;

            string[] files = Directory.GetFiles(directory, "*.cs", SearchOption.AllDirectories);
            for (int i = 0; i < files.Length; i++)
            {
                string path = files[i];
                string normalized = path.Replace('\\', '/');
                if (normalized.IndexOf("/Editor/", StringComparison.Ordinal) >= 0)
                    continue;

                scannedFiles++;
                string source = File.ReadAllText(path);
                if (!DetectOopDoorStateMachine(source, out int line, out string reason, out int visited, out bool parserFallback))
                {
                    syntaxNodesVisited += visited;
                    if (parserFallback)
                        parserFallbackFiles++;
                    continue;
                }

                syntaxNodesVisited += visited;
                if (parserFallback)
                    parserFallbackFiles++;

                suspiciousDoorStateMachines++;
                string reportPath = ToProjectRelativePath(normalized);
                if (filesJson.Length > 0)
                    filesJson.AppendLine(",");
                filesJson.Append("    { \"path\": \"")
                    .Append(Escape(reportPath))
                    .Append("\", \"line\": ")
                    .Append(line)
                    .Append(", \"reason\": \"")
                    .Append(Escape(reason))
                    .Append("\" }");
                filesMarkdown.Append("- `")
                    .Append(reportPath)
                    .Append("` line=")
                    .Append(line)
                    .Append(" reason=")
                    .Append(reason)
                    .AppendLine();
            }
        }

        private static bool DetectOopDoorStateMachine(
            string source,
            out int line,
            out string reason,
            out int syntaxNodesVisited,
            out bool parserFallback)
        {
            line = 0;
            reason = string.Empty;
            syntaxNodesVisited = 0;
            parserFallback = false;
            if (string.IsNullOrEmpty(source))
                return false;

            try
            {
                SyntaxTree tree = CSharpSyntaxTree.ParseText(source);
                SyntaxNode root = tree.GetRoot();
                using (System.Collections.Generic.IEnumerator<SyntaxNode> nodes = root.DescendantNodes().GetEnumerator())
                {
                    while (nodes.MoveNext())
                    {
                        SyntaxNode node = nodes.Current;
                        syntaxNodesVisited++;
                        if (node is MethodDeclarationSyntax method && IsManagedDoorUpdate(method))
                        {
                            line = LineOf(tree, method);
                            reason = "AST managed Update door/water/lock state machine";
                            return true;
                        }

                        if (node is SwitchStatementSyntax switchStatement && IsDoorStatePressureSwitch(switchStatement))
                        {
                            line = LineOf(tree, switchStatement);
                            reason = "AST DoorState switch coupled to pressure/water";
                            return true;
                        }
                    }
                }

                return false;
            }
            catch (Exception)
            {
                parserFallback = true;
            }

            return DetectOopDoorStateMachineFallback(source, out line, out reason);
        }

        private static bool DetectOopDoorStateMachineFallback(string source, out int line, out string reason)
        {
            line = 0;
            reason = string.Empty;
            bool doorSemantic = ContainsAny(source, "Door", "door", "Hatch", "hatch", "Bulkhead", "bulkhead");
            if (!doorSemantic)
                return false;

            bool updateLoop = ContainsAny(source, "void Update(", "void LateUpdate(", "void FixedUpdate(");
            bool waterOrPressure = ContainsAny(source, "waterLevel", "WaterLevel", "Pressure", "pressure", "FluidCompartment");
            bool lockMutation = ContainsAny(source, "isLocked", "IsLocked", "DoorState", "switch (DoorState", "switch(DoorState");
            if (updateLoop && (waterOrPressure || lockMutation))
            {
                line = ResolveFirstLine(source, "void Update(");
                if (line == 0)
                    line = ResolveFirstLine(source, "void FixedUpdate(");
                reason = "managed Update door/water/lock state machine";
                return true;
            }

            if (ContainsAny(source, "switch (DoorState", "switch(DoorState") && waterOrPressure)
            {
                line = ResolveFirstLine(source, "DoorState");
                reason = "DoorState switch coupled to pressure/water";
                return true;
            }

            return false;
        }

        private static bool IsManagedDoorUpdate(MethodDeclarationSyntax method)
        {
            string methodName = method.Identifier.ValueText;
            bool hotMethod = string.Equals(methodName, "Update", StringComparison.Ordinal) ||
                             string.Equals(methodName, "LateUpdate", StringComparison.Ordinal) ||
                             string.Equals(methodName, "FixedUpdate", StringComparison.Ordinal);
            if (!hotMethod)
                return false;

            if (!HasDoorSemantic(method))
                return false;

            return ContainsToken(method, "waterLevel", "WaterLevel", "Pressure", "pressure", "FluidCompartment", "isLocked", "IsLocked", "DoorState");
        }

        private static bool IsDoorStatePressureSwitch(SwitchStatementSyntax switchStatement)
        {
            string expression = switchStatement.Expression != null ? switchStatement.Expression.ToString() : string.Empty;
            if (expression.IndexOf("DoorState", StringComparison.Ordinal) < 0)
                return false;

            return HasDoorSemantic(switchStatement) &&
                   ContainsToken(switchStatement, "waterLevel", "WaterLevel", "Pressure", "pressure", "FluidCompartment", "isLocked", "IsLocked");
        }

        private static bool HasDoorSemantic(SyntaxNode node)
        {
            SyntaxNode current = node;
            while (current != null)
            {
                if (current is TypeDeclarationSyntax typeDeclaration &&
                    ContainsAny(typeDeclaration.Identifier.ValueText, "Door", "door", "Hatch", "hatch", "Bulkhead", "bulkhead"))
                {
                    return true;
                }

                if (current is MethodDeclarationSyntax methodDeclaration &&
                    ContainsAny(methodDeclaration.Identifier.ValueText, "Door", "door", "Hatch", "hatch", "Bulkhead", "bulkhead"))
                {
                    return true;
                }

                current = current.Parent;
            }

            return ContainsToken(node, "Door", "door", "Hatch", "hatch", "Bulkhead", "bulkhead");
        }

        private static bool ContainsToken(SyntaxNode node, params string[] needles)
        {
            using (System.Collections.Generic.IEnumerator<SyntaxToken> tokens = node.DescendantTokens().GetEnumerator())
            {
                while (tokens.MoveNext())
                {
                    string value = tokens.Current.ValueText;
                    if (string.IsNullOrEmpty(value))
                        continue;

                    for (int i = 0; i < needles.Length; i++)
                    {
                        if (value.IndexOf(needles[i], StringComparison.Ordinal) >= 0)
                            return true;
                    }
                }
            }

            return false;
        }

        private static int LineOf(SyntaxTree tree, SyntaxNode node)
        {
            return tree.GetLineSpan(node.Span).StartLinePosition.Line + 1;
        }

        private static string BuildSidecarJson(string generatedUtc, string verdict, int scannedFiles, int suspiciousCount, int syntaxNodesVisited, int parserFallbackFiles, StringBuilder filesJson)
        {
            StringBuilder json = new StringBuilder(8192);
            json.AppendLine("{");
            json.AppendLine("  \"agent\": \"SHINOBU_343\",");
            json.AppendLine("  \"scanner\": \"OOP_Door_Scanner\",");
            json.Append("  \"generated_utc\": \"").Append(Escape(generatedUtc)).AppendLine("\",");
            json.Append("  \"summary\": \"").Append(Escape(verdict)).AppendLine("\",");
            json.AppendLine("  \"scanner_parser_route\": \"Roslyn CSharpSyntaxTree primary pass; lexical fallback only on parse exception\",");
            json.Append("  \"scanned_files\": ").Append(scannedFiles).AppendLine(",");
            json.Append("  \"suspicious_oop_door_state_machines\": ").Append(suspiciousCount).AppendLine(",");
            json.Append("  \"syntax_nodes_visited\": ").Append(syntaxNodesVisited).AppendLine(",");
            json.Append("  \"parser_fallback_files\": ").Append(parserFallbackFiles).AppendLine(",");
            json.AppendLine("  \"authority_route\": \"BulkheadContainmentRuntime_HatchLocks -> HatchStateDTO.FsmStateMask -> BulkheadStateDTO.AssociatedLock/KCC plane\",");
            json.AppendLine("  \"files\": [");
            json.Append(filesJson);
            json.AppendLine();
            json.AppendLine("  ]");
            json.AppendLine("}");
            return json.ToString();
        }

        private static string BuildAggregateJson(string generatedUtc, string verdict, int scannedFiles, int suspiciousCount, int syntaxNodesVisited, int parserFallbackFiles)
        {
            StringBuilder json = new StringBuilder(2048);
            json.AppendLine("{");
            json.AppendLine("    \"agent\": \"SHINOBU_343\",");
            json.AppendLine("    \"scanner\": \"OOP_Door_Scanner\",");
            json.Append("    \"generated_utc\": \"").Append(Escape(generatedUtc)).AppendLine("\",");
            json.Append("    \"summary\": \"").Append(Escape(verdict)).AppendLine("\",");
            json.AppendLine("    \"scanner_parser_route\": \"Roslyn CSharpSyntaxTree primary pass; lexical fallback only on parse exception\",");
            json.Append("    \"scanned_files\": ").Append(scannedFiles).AppendLine(",");
            json.Append("    \"suspicious_oop_door_state_machines\": ").Append(suspiciousCount).AppendLine(",");
            json.Append("    \"syntax_nodes_visited\": ").Append(syntaxNodesVisited).AppendLine(",");
            json.Append("    \"parser_fallback_files\": ").Append(parserFallbackFiles).AppendLine(",");
            json.AppendLine("    \"sidecar_report\": \"Docs/Reports/PHYSICS_OPTIMIZATION_REPORT_SHINOBU_343.json\",");
            json.AppendLine("    \"markdown_report\": \"Docs/Reports/OOP_Door_Scanner_SHINOBU_343.md\",");
            json.AppendLine("    \"fsm_truth\": \"HatchStateDTO.FsmStateMask\",");
            json.AppendLine("    \"verdict\": \"PASS when suspicious_oop_door_state_machines is 0\"");
            json.Append("  }");
            return json.ToString();
        }

        private static string BuildMarkdown(string generatedUtc, string verdict, int scannedFiles, int suspiciousCount, int syntaxNodesVisited, int parserFallbackFiles, StringBuilder filesMarkdown)
        {
            StringBuilder markdown = new StringBuilder(8192);
            markdown.AppendLine("# SHINOBU_343 OOP Door Scanner");
            markdown.AppendLine();
            markdown.Append("- Generated UTC: ").AppendLine(generatedUtc);
            markdown.Append("- Summary: ").AppendLine(verdict);
            markdown.AppendLine("- Parser route: Roslyn CSharpSyntaxTree primary pass; lexical fallback only on parse exception.");
            markdown.Append("- Scanned files: ").Append(scannedFiles).AppendLine();
            markdown.Append("- Suspicious OOP door state machines: ").Append(suspiciousCount).AppendLine();
            markdown.Append("- Syntax nodes visited: ").Append(syntaxNodesVisited).AppendLine();
            markdown.Append("- Parser fallback files: ").Append(parserFallbackFiles).AppendLine();
            markdown.AppendLine();
            markdown.AppendLine("## Evidence");
            if (filesMarkdown.Length == 0)
                markdown.AppendLine("- None.");
            else
                markdown.Append(filesMarkdown);
            return markdown.ToString();
        }

        private static void UpsertAggregateReport(string aggregateFullPath, string objectJson)
        {
            string current = File.Exists(aggregateFullPath) ? File.ReadAllText(aggregateFullPath) : string.Empty;
            string normalized = current.Trim();
            if (normalized.Length < 2 || normalized[0] != '{' || normalized[normalized.Length - 1] != '}')
            {
                WriteTextAtomic(aggregateFullPath, "{\n  \"" + AggregateKey + "\": " + objectJson + "\n}\n");
                return;
            }

            int keyIndex = normalized.IndexOf("\"" + AggregateKey + "\"", StringComparison.Ordinal);
            if (keyIndex >= 0)
                normalized = RemoveExistingObject(normalized, keyIndex);

            int insert = normalized.LastIndexOf('}');
            string prefix = normalized.Substring(0, insert).TrimEnd();
            bool hasExistingEntries = prefix.Length > 1 && prefix[prefix.Length - 1] != '{';
            string merged = prefix +
                            (hasExistingEntries ? ",\n" : "\n") +
                            "  \"" + AggregateKey + "\": " + objectJson +
                            "\n}\n";
            WriteTextAtomic(aggregateFullPath, merged);
        }

        private static string RemoveExistingObject(string json, int keyIndex)
        {
            int start = keyIndex;
            while (start > 0 && json[start - 1] != '\n' && json[start - 1] != ',')
                start--;
            int colon = json.IndexOf(':', keyIndex);
            int objectStart = json.IndexOf('{', colon);
            if (colon < 0 || objectStart < 0)
                return json;

            int depth = 0;
            for (int i = objectStart; i < json.Length; i++)
            {
                if (json[i] == '{')
                    depth++;
                else if (json[i] == '}')
                {
                    depth--;
                    if (depth == 0)
                    {
                        int end = i + 1;
                        if (end < json.Length && json[end] == ',')
                            end++;
                        else if (start > 0 && json[start - 1] == ',')
                            start--;
                        return json.Remove(start, end - start);
                    }
                }
            }

            return json;
        }

        private static void WriteTextAtomic(string fullPath, string text)
        {
            string directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);
            string temp = fullPath + ".tmp";
            File.WriteAllText(temp, text, Encoding.UTF8);
            if (File.Exists(fullPath))
                File.Replace(temp, fullPath, null);
            else
                File.Move(temp, fullPath);
        }

        private static bool ContainsAny(string value, params string[] needles)
        {
            for (int i = 0; i < needles.Length; i++)
            {
                if (value.IndexOf(needles[i], StringComparison.Ordinal) >= 0)
                    return true;
            }

            return false;
        }

        private static int ResolveFirstLine(string source, string token)
        {
            int index = source.IndexOf(token, StringComparison.Ordinal);
            if (index < 0)
                return 0;

            int line = 1;
            for (int i = 0; i < index; i++)
            {
                if (source[i] == '\n')
                    line++;
            }

            return line;
        }

        private static string ToProjectRelativePath(string fullPath)
        {
            string data = Application.dataPath.Replace('\\', '/');
            string path = fullPath.Replace('\\', '/');
            if (path.StartsWith(data, StringComparison.Ordinal))
                return "Assets" + path.Substring(data.Length);
            return path;
        }

        private static string Escape(string value)
        {
            return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }
    }
}
#endif
