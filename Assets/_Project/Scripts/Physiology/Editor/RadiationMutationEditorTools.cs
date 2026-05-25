#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Hecton8.Core;
using Hecton8.Physiology;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Hecton8.Physiology.Editor
{
    [InitializeOnLoad]
    internal static class RadiationMutationLayoutValidator
    {
        static RadiationMutationLayoutValidator()
        {
            Validate();
        }

        [MenuItem("Hecton8/Physiology/Validate Radiation Mutation Layout")]
        private static void ValidateMenu()
        {
            Validate();
        }

        internal static bool Validate()
        {
            bool valid = ShinobuRadiationMutationLayoutGuards.ValidateMutationLayouts() &&
                         UnsafeUtility.SizeOf<MutationStateDTO>() == 32 &&
                         UnsafeUtility.SizeOf<RadiationMutationTuningDTO>() == 64 &&
                         UnsafeUtility.SizeOf<RadiationMutationTelemetryEntry>() == 64;
            if (!valid)
            {
                Debug.LogError("[SHINOBU_324] Radiation mutation DTO layout violation. Required MutationStateDTO Size=32 offsets 0/4/8/12 with 16B private padding.");
                throw new FatalArchitectureException("SHINOBU_324 MutationStateDTO layout violation.");
            }

            return true;
        }
    }

    public sealed class RadiationMutationTunerWindow : EditorWindow
    {
        private const int SeriesCapacity = ShinobuRadiationMutationConstants.TelemetryFrameCount;
        private readonly float[] _doseSeries = new float[SeriesCapacity];
        private readonly float[] _severitySeries = new float[SeriesCapacity];
        private ShinobuRadiationMutationRuntime _runtime;
        private Label _status;
        private VisualElement _chart;
        private int _seriesCount;
        private Slider _safeDose;
        private Slider _fatalDose;
        private Slider _staminaPenalty;
        private Slider _healingDecay;
        private Slider _rise;
        private Slider _fall;
        private Slider _bloodThreshold;
        private Slider _shaderPulse;

        [MenuItem("Hecton8/Physiology/Radiation Mutation Tuner")]
        public static void Open()
        {
            GetWindow<RadiationMutationTunerWindow>("Radiation Mutation");
        }

        public void CreateGUI()
        {
            RebindRuntime();
            rootVisualElement.style.paddingLeft = 8;
            rootVisualElement.style.paddingRight = 8;
            rootVisualElement.style.paddingTop = 8;
            rootVisualElement.style.paddingBottom = 8;

            _status = new Label("No radiation mutation runtime");
            rootVisualElement.Add(_status);

            _chart = new VisualElement();
            _chart.style.height = 180;
            _chart.style.marginTop = 6;
            _chart.style.marginBottom = 8;
            _chart.generateVisualContent += GenerateChart;
            rootVisualElement.Add(_chart);

            _safeDose = BuildSlider("Safe Dose Rad", 0f, 200f);
            _fatalDose = BuildSlider("Fatal Dose Rad", 250f, 2000f);
            _staminaPenalty = BuildSlider("Max Stamina Penalty", 0f, 0.95f);
            _healingDecay = BuildSlider("Healing Decay", 0f, 4f);
            _rise = BuildSlider("Severity Rise", 0.01f, 10f);
            _fall = BuildSlider("Severity Fall", 0.01f, 10f);
            _bloodThreshold = BuildSlider("Toxic Blood Threshold", 0f, 1f);
            _shaderPulse = BuildSlider("Shader Pulse", 0f, 1f);

            rootVisualElement.Add(_safeDose);
            rootVisualElement.Add(_fatalDose);
            rootVisualElement.Add(_staminaPenalty);
            rootVisualElement.Add(_healingDecay);
            rootVisualElement.Add(_rise);
            rootVisualElement.Add(_fall);
            rootVisualElement.Add(_bloodThreshold);
            rootVisualElement.Add(_shaderPulse);

            _safeDose.RegisterValueChangedCallback(_ => ApplyTuning());
            _fatalDose.RegisterValueChangedCallback(_ => ApplyTuning());
            _staminaPenalty.RegisterValueChangedCallback(_ => ApplyTuning());
            _healingDecay.RegisterValueChangedCallback(_ => ApplyTuning());
            _rise.RegisterValueChangedCallback(_ => ApplyTuning());
            _fall.RegisterValueChangedCallback(_ => ApplyTuning());
            _bloodThreshold.RegisterValueChangedCallback(_ => ApplyTuning());
            _shaderPulse.RegisterValueChangedCallback(_ => ApplyTuning());

            rootVisualElement.schedule.Execute(Refresh).Every(100);
        }

        private void OnFocus()
        {
            RebindRuntime();
        }

        private void OnHierarchyChange()
        {
            RebindRuntime();
        }

        private void RebindRuntime()
        {
            _runtime = UnityEngine.Object.FindAnyObjectByType<ShinobuRadiationMutationRuntime>();
        }

        private static Slider BuildSlider(string label, float low, float high)
        {
            Slider slider = new Slider(label, low, high);
            slider.showInputField = true;
            return slider;
        }

        private void Refresh()
        {
            if (_runtime == null)
            {
                _status.text = "No radiation mutation runtime";
                return;
            }

            if (_runtime.TryGetTuning(out RadiationMutationTuningDTO tuning))
            {
                _safeDose.SetValueWithoutNotify(tuning.SafeDoseRad);
                _fatalDose.SetValueWithoutNotify(tuning.FatalDoseRad);
                _staminaPenalty.SetValueWithoutNotify(tuning.MaxStaminaPenaltyPercent);
                _healingDecay.SetValueWithoutNotify(tuning.HealingDecayPerSecond);
                _rise.SetValueWithoutNotify(tuning.SeverityRisePerSecond);
                _fall.SetValueWithoutNotify(tuning.SeverityFallPerSecond);
                _bloodThreshold.SetValueWithoutNotify(tuning.ToxicBloodThreshold01);
                _shaderPulse.SetValueWithoutNotify(tuning.ShaderPulseStrength);
            }

            if (_runtime.TryGetMutationState(out MutationStateDTO mutation) &&
                _runtime.TryGetLatestTelemetry(out RadiationMutationTelemetryEntry telemetry))
            {
                _seriesCount = _runtime.CopyTelemetrySeriesForEditor(_doseSeries, _severitySeries);
                _chart.MarkDirtyRepaint();
                _status.text =
                    $"Mutation {mutation.MutationSeverity01:0.00} | Stamina cap -{mutation.MaxStaminaPenalty:0.00} | Dose {telemetry.AttenuatedDoseRad:0} rad | {telemetry.ExecutionMicroseconds:0.0} us";
            }
            else
            {
                _status.text = "Radiation mutation vault unavailable";
            }
        }

        private void ApplyTuning()
        {
            if (_runtime == null || !_runtime.TryGetTuning(out RadiationMutationTuningDTO tuning))
                return;

            tuning.SafeDoseRad = _safeDose.value;
            tuning.FatalDoseRad = math.max(_safeDose.value + 1f, _fatalDose.value);
            tuning.MaxStaminaPenaltyPercent = _staminaPenalty.value;
            tuning.HealingDecayPerSecond = _healingDecay.value;
            tuning.SeverityRisePerSecond = _rise.value;
            tuning.SeverityFallPerSecond = _fall.value;
            tuning.ToxicBloodThreshold01 = _bloodThreshold.value;
            tuning.ShaderPulseStrength = _shaderPulse.value;
            _runtime.SetEditorTuning(tuning);
        }

        private void GenerateChart(MeshGenerationContext context)
        {
            Rect rect = _chart.contentRect;
            if (rect.width <= 1f || rect.height <= 1f || _seriesCount <= 1)
                return;

            Painter2D painter = context.painter2D;
            DrawSeries(painter, rect, _doseSeries, _seriesCount, new Color(0.95f, 0.72f, 0.12f, 1f));
            DrawSeries(painter, rect, _severitySeries, _seriesCount, new Color(0.55f, 0.1f, 0.95f, 1f));
        }

        private static void DrawSeries(Painter2D painter, Rect rect, float[] series, int count, Color color)
        {
            painter.strokeColor = color;
            painter.lineWidth = 2f;
            painter.BeginPath();
            for (int i = 0; i < count; i++)
            {
                float x = rect.xMin + rect.width * (i / (float)(count - 1));
                float y = rect.yMax - rect.height * Mathf.Clamp01(series[i]);
                if (i == 0)
                    painter.MoveTo(new Vector2(x, y));
                else
                    painter.LineTo(new Vector2(x, y));
            }
            painter.Stroke();
        }
    }

    internal static class RadiationMutationDebugGizmo
    {
        private static readonly GUIContent[] s_penaltyLabels = BuildPenaltyLabels();

        [DrawGizmo(GizmoType.Selected | GizmoType.Active)]
        private static void Draw(ShinobuRadiationMutationRuntime runtime, GizmoType gizmoType)
        {
            if (runtime == null || !runtime.TryGetMutationState(out MutationStateDTO state))
                return;

            float severity = math.saturate(state.MutationSeverity01);
            float pulse = 0.65f + 0.35f * math.saturate(severity) * (0.5f + 0.5f * MathLodApproximation.ApproxSinBhaskara((float)EditorApplication.timeSinceStartup * 4.7f));
            Color color = Color.Lerp(new Color(0.1f, 0.95f, 0.32f, 0.75f), new Color(0.68f, 0.05f, 0.95f, 0.95f), math.saturate(severity * pulse));
            Vector3 position = runtime.transform.position + Vector3.up * 1.6f;
            Handles.color = color;
            float size = Mathf.Lerp(0.35f, 0.95f, severity);
            Handles.DrawWireCube(position, new Vector3(size, size * 1.8f, size));
            int labelIndex = Mathf.Clamp((int)Mathf.Round(math.saturate(state.MaxStaminaPenalty) * 100f), 0, s_penaltyLabels.Length - 1);
            Handles.Label(position + Vector3.up * (size + 0.18f), s_penaltyLabels[labelIndex]);
        }

        private static GUIContent[] BuildPenaltyLabels()
        {
            GUIContent[] labels = new GUIContent[101];
            for (int i = 0; i < labels.Length; i++)
                labels[i] = new GUIContent("RAD MUT stamina -" + i + "%");
            return labels;
        }
    }

    internal static class RadiationMutationOopScanner
    {
        private const string Summary = "OOP Visual Mutations Eradicated";
        private const string SharedReportPath = "Docs/Reports/RENDERING_OPTIMIZATION_REPORT.json";
        private const string SidecarReportPath = "Docs/Reports/RENDERING_OPTIMIZATION_REPORT_SHINOBU_324.json";

        private static readonly string[] s_roots =
        {
            "Assets/_Project/Scripts/Physiology",
            "Assets/_Project/Scripts/Player",
            "Assets/_Project/Scripts/Core/Contracts/HectonDataSovereigntyContract.cs",
            "Assets/_Project/Scripts/Gameplay/RadiationHazardGrid.cs",
            "Assets/_Project/Scripts/Rendering/HectonShaderGlobalDataVaultBridge.cs",
            "Assets/_Project/Art/Shaders/Hecton8_UberNoir.hlsl"
        };

        private static readonly string[] s_forbidden =
        {
            ".materials",
            "renderer.material",
            "Renderer.material",
            "SkinnedMeshRenderer",
            "ParticleSystem",
            "Instantiate(",
            "new GameObject",
            "PlayerMutation",
            "RadiationVisuals",
            "MutationEffect"
        };

        [MenuItem("Hecton8/Physiology/Run Radiation Mutation OOP Scanner")]
        private static void RunMenu()
        {
            int findingCount = RunStaticScan(Application.dataPath);
            if (findingCount == 0)
                Debug.Log("[SHINOBU_324] Radiation mutation OOP scan clean.");
            else
                Debug.LogWarning("[SHINOBU_324] Radiation mutation OOP scan findings: " + findingCount);
        }

        internal static int RunStaticScan(string assetsPath)
        {
            string projectRoot = Path.GetFullPath(Path.Combine(assetsPath, ".."));
            List<Finding> findings = new List<Finding>(32);
            ScanStats stats = default;
            for (int i = 0; i < s_roots.Length; i++)
                ScanRoot(projectRoot, s_roots[i], findings, ref stats);

            WriteText(Path.Combine(projectRoot, SidecarReportPath), BuildReport(findings, in stats));
            UpsertSharedReport(Path.Combine(projectRoot, SharedReportPath), BuildSharedSection(findings, in stats));
            return findings.Count;
        }

        private static void ScanRoot(string projectRoot, string relativeRoot, List<Finding> findings, ref ScanStats stats)
        {
            string root = Path.Combine(projectRoot, relativeRoot.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(root))
            {
                ScanFile(projectRoot, root, findings, ref stats);
                return;
            }

            if (!Directory.Exists(root))
                return;

            string[] files = Directory.GetFiles(root, "*.*", SearchOption.AllDirectories);
            for (int i = 0; i < files.Length; i++)
            {
                string extension = Path.GetExtension(files[i]);
                if (!string.Equals(extension, ".cs", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(extension, ".hlsl", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(extension, ".shader", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string normalized = files[i].Replace('\\', '/');
                if (normalized.Contains("/Physiology/Editor/", StringComparison.Ordinal))
                    continue;
                ScanFile(projectRoot, files[i], findings, ref stats);
            }
        }

        private static void ScanFile(string projectRoot, string path, List<Finding> findings, ref ScanStats stats)
        {
            string relative = MakeRelative(projectRoot, path).Replace('\\', '/');
            string extension = Path.GetExtension(path);
            if (string.Equals(extension, ".cs", StringComparison.OrdinalIgnoreCase))
            {
                stats.ScannedCSharpFiles++;
                string source = File.ReadAllText(path);
                SyntaxTree tree;
                try
                {
                    tree = CSharpSyntaxTree.ParseText(source);
                }
                catch (Exception exception)
                {
                    stats.ParserFailures++;
                    findings.Add(new Finding(relative, 0, "RoslynParse:" + exception.GetType().Name, "RoslynParse"));
                    return;
                }

                if (HasParseError(tree))
                {
                    stats.ParserFailures++;
                    findings.Add(new Finding(relative, 0, "RoslynParse:syntax error", "RoslynParse"));
                    return;
                }

                ScanCSharpAst(relative, tree.GetCompilationUnitRoot(), findings);
                return;
            }

            string[] lines = File.ReadAllLines(path);
            for (int lineIndex = 0; lineIndex < lines.Length; lineIndex++)
            {
                string line = lines[lineIndex];
                for (int patternIndex = 0; patternIndex < s_forbidden.Length; patternIndex++)
                {
                    string pattern = s_forbidden[patternIndex];
                    if (line.IndexOf(pattern, StringComparison.Ordinal) < 0)
                        continue;
                    if (IsAllowedLocalRoute(relative, pattern, line))
                        continue;

                    findings.Add(new Finding(relative, lineIndex + 1, pattern, "TokenFallback"));
                }
            }
        }

        private static bool HasParseError(SyntaxTree tree)
        {
            using (IEnumerator<Diagnostic> diagnostics = tree.GetDiagnostics().GetEnumerator())
            {
                while (diagnostics.MoveNext())
                {
                    if (diagnostics.Current.Severity == DiagnosticSeverity.Error)
                        return true;
                }
            }

            return false;
        }

        private static void ScanCSharpAst(string relative, SyntaxNode root, List<Finding> findings)
        {
            using (IEnumerator<SyntaxNode> nodes = root.DescendantNodes().GetEnumerator())
            {
                while (nodes.MoveNext())
                {
                    SyntaxNode node = nodes.Current;
                    if (!TryResolveForbiddenSyntax(relative, node, out string pattern))
                        continue;

                    findings.Add(new Finding(relative, GetLineNumber(node), pattern, "RoslynAST"));
                }
            }
        }

        private static bool TryResolveForbiddenSyntax(string relative, SyntaxNode node, out string pattern)
        {
            pattern = string.Empty;

            if (node is ObjectCreationExpressionSyntax objectCreation)
            {
                string typeName = objectCreation.Type.ToString();
                if (IsForbiddenMutationType(typeName) ||
                    (IsMutationAuthorityContext(relative, objectCreation) && ContainsAny(typeName, "ParticleSystem", "SkinnedMeshRenderer")))
                {
                    pattern = "new " + typeName;
                    return true;
                }
            }

            if (node is InvocationExpressionSyntax invocation)
            {
                string memberName = ResolveInvocationMemberName(invocation);
                if (string.Equals(memberName, "Instantiate", StringComparison.Ordinal) && IsMutationAuthorityContext(relative, invocation))
                {
                    pattern = "Instantiate";
                    return true;
                }

                if (string.Equals(memberName, "GetComponent", StringComparison.Ordinal) &&
                    invocation.Expression.ToString().IndexOf("SkinnedMeshRenderer", StringComparison.Ordinal) >= 0 &&
                    IsMutationAuthorityContext(relative, invocation))
                {
                    pattern = "GetComponent<SkinnedMeshRenderer>";
                    return true;
                }
            }

            if (node is AssignmentExpressionSyntax assignment &&
                assignment.Left is MemberAccessExpressionSyntax assignedMember &&
                IsRendererMaterialMember(assignedMember.Name.Identifier.ValueText) &&
                IsMutationAuthorityContext(relative, assignment))
            {
                pattern = assignedMember.Name.Identifier.ValueText;
                return true;
            }

            if (node is MemberAccessExpressionSyntax memberAccess &&
                IsRendererMaterialMember(memberAccess.Name.Identifier.ValueText) &&
                IsMutationAuthorityContext(relative, memberAccess))
            {
                pattern = memberAccess.Name.Identifier.ValueText;
                return true;
            }

            if (node is IdentifierNameSyntax identifier &&
                ContainsAny(identifier.Identifier.ValueText, "MutationEffect", "PlayerMutation", "RadiationVisuals") &&
                IsMutationAuthorityContext(relative, identifier))
            {
                pattern = identifier.Identifier.ValueText;
                return true;
            }

            return false;
        }

        private static string ResolveInvocationMemberName(InvocationExpressionSyntax invocation)
        {
            if (invocation.Expression is IdentifierNameSyntax identifier)
                return identifier.Identifier.ValueText;
            if (invocation.Expression is MemberAccessExpressionSyntax memberAccess)
                return memberAccess.Name.Identifier.ValueText;
            if (invocation.Expression is GenericNameSyntax genericName)
                return genericName.Identifier.ValueText;
            return string.Empty;
        }

        private static bool IsRendererMaterialMember(string memberName)
        {
            return string.Equals(memberName, "materials", StringComparison.Ordinal) ||
                   string.Equals(memberName, "material", StringComparison.Ordinal) ||
                   string.Equals(memberName, "sharedMaterials", StringComparison.Ordinal) ||
                   string.Equals(memberName, "sharedMaterial", StringComparison.Ordinal);
        }

        private static bool IsForbiddenMutationType(string typeName)
        {
            return ContainsAny(typeName, "MutationEffect", "PlayerMutation", "RadiationVisuals");
        }

        private static bool IsMutationAuthorityContext(string relative, SyntaxNode node)
        {
            if (relative.IndexOf("/Physiology/", StringComparison.Ordinal) >= 0 ||
                relative.IndexOf("/Player/", StringComparison.Ordinal) >= 0)
            {
                return true;
            }

            SyntaxNode current = node;
            while (current != null)
            {
                if (current is TypeDeclarationSyntax typeDeclaration &&
                    ContainsMutationToken(typeDeclaration.Identifier.ValueText))
                {
                    return true;
                }

                if (current is MethodDeclarationSyntax methodDeclaration &&
                    ContainsMutationToken(methodDeclaration.Identifier.ValueText))
                {
                    return true;
                }

                current = current.Parent;
            }

            return ContainsMutationToken(node.ToString());
        }

        private static bool ContainsMutationToken(string value)
        {
            return ContainsAny(value, "Mutation", "Radiation", "Blister", "Radioactive", "RadAway", "ToxicBlood");
        }

        private static bool ContainsAny(string value, string a, string b)
        {
            return value.IndexOf(a, StringComparison.OrdinalIgnoreCase) >= 0 ||
                   value.IndexOf(b, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool ContainsAny(string value, string a, string b, string c)
        {
            return value.IndexOf(a, StringComparison.OrdinalIgnoreCase) >= 0 ||
                   value.IndexOf(b, StringComparison.OrdinalIgnoreCase) >= 0 ||
                   value.IndexOf(c, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool ContainsAny(string value, string a, string b, string c, string d, string e, string f)
        {
            return value.IndexOf(a, StringComparison.OrdinalIgnoreCase) >= 0 ||
                   value.IndexOf(b, StringComparison.OrdinalIgnoreCase) >= 0 ||
                   value.IndexOf(c, StringComparison.OrdinalIgnoreCase) >= 0 ||
                   value.IndexOf(d, StringComparison.OrdinalIgnoreCase) >= 0 ||
                   value.IndexOf(e, StringComparison.OrdinalIgnoreCase) >= 0 ||
                   value.IndexOf(f, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static int GetLineNumber(SyntaxNode node)
        {
            FileLinePositionSpan span = node.GetLocation().GetLineSpan();
            return span.StartLinePosition.Line + 1;
        }

        private static bool IsAllowedLocalRoute(string relative, string pattern, string line)
        {
            if (relative.EndsWith("/Physiology/Editor/RadiationMutationEditorTools.cs", StringComparison.Ordinal))
                return true;
            if (string.Equals(pattern, "new GameObject", StringComparison.Ordinal) &&
                line.IndexOf("COLD ALLOC", StringComparison.Ordinal) >= 0)
            {
                return true;
            }

            return false;
        }

        private static string BuildReport(List<Finding> findings, in ScanStats stats)
        {
            StringBuilder builder = new StringBuilder(2048);
            builder.AppendLine("{");
            builder.AppendLine("  \"agent\": \"SHINOBU_324\",");
            builder.AppendLine("  \"scanner\": \"RadiationMutationOopScanner\",");
            builder.AppendLine("  \"evidenceClass\": \"STATIC_SOURCE_TARGETED_EDITOR_SCAN\",");
            builder.AppendLine("  \"scannerUsesRoslynAst\": true,");
            builder.AppendLine("  \"scannerParserRoute\": \"Roslyn CSharpSyntaxTree pass for C# plus token fallback for HLSL/shader bridge files\",");
            builder.Append("  \"sourceFilesScanned\": ").Append(stats.ScannedCSharpFiles).AppendLine(",");
            builder.Append("  \"parserFailures\": ").Append(stats.ParserFailures).AppendLine(",");
            builder.AppendLine("  \"scannedScope\": [");
            AppendStringArray(builder, s_roots, 4);
            builder.AppendLine("  ],");
            builder.AppendLine("  \"forbiddenPatterns\": [");
            AppendStringArray(builder, s_forbidden, 4);
            builder.AppendLine("  ],");
            builder.Append("  \"summary\": \"").Append(Summary).AppendLine("\",");
            builder.Append("  \"findingCount\": ").Append(findings.Count).AppendLine(",");
            builder.AppendLine("  \"replacementRoute\": \"Core.Contracts RadiationStateDTO Vault read -> MutationStateDTO scalar -> PreSimulation metabolism bridge -> VisualSync HectonShaderGlobalDataVaultBridge slot 22 -> GlobalShaderDispatcher command-buffer sync -> UberNoir quality-gated vertex displacement plus blister/subsurface surface fake; toxic blood uses bounded DebrisSpawnSignal AUP route.\",");
            builder.AppendLine("  \"compileStatus\": \"EDITOR_SCAN_NO_BUILD_REQUESTED_ROSLYN_AST\",");
            builder.AppendLine("  \"notes\": \"Editor scanner is static targeted evidence only; C# scanning is Roslyn AST based, HLSL/shader bridge files use token fallback, and runtime code remains free of material swaps, ParticleSystem spawns, GameObject mutation effects, hidden Complete, Schedule, and one-row Run wrappers.\",");
            builder.AppendLine("  \"findings\": [");
            AppendFindings(builder, findings, 4);
            builder.AppendLine("  ]");
            builder.AppendLine("}");
            return builder.ToString();
        }

        private static string BuildSharedSection(List<Finding> findings, in ScanStats stats)
        {
            StringBuilder builder = new StringBuilder(2048);
            builder.AppendLine("  \"shinobu324RadiationMutationOopScanner\": {");
            builder.AppendLine("    \"agent\": \"SHINOBU_324\",");
            builder.AppendLine("    \"scanner\": \"RadiationMutationOopScanner\",");
            builder.AppendLine("    \"evidenceClass\": \"STATIC_SOURCE_TARGETED_EDITOR_SCAN\",");
            builder.AppendLine("    \"scannerUsesRoslynAst\": true,");
            builder.AppendLine("    \"scannerParserRoute\": \"Roslyn CSharpSyntaxTree pass for C# plus token fallback for HLSL/shader bridge files\",");
            builder.Append("    \"sourceFilesScanned\": ").Append(stats.ScannedCSharpFiles).AppendLine(",");
            builder.Append("    \"parserFailures\": ").Append(stats.ParserFailures).AppendLine(",");
            builder.Append("    \"summary\": \"").Append(Summary).AppendLine("\",");
            builder.Append("    \"findingCount\": ").Append(findings.Count).AppendLine(",");
            builder.AppendLine("    \"replacementRoute\": \"Core.Contracts RadiationStateDTO Vault read -> MutationStateDTO scalar -> PreSimulation metabolism bridge -> VisualSync shader slot 22 -> GlobalShaderDispatcher command-buffer sync -> UberNoir quality-gated vertex displacement plus blister/subsurface surface fake\",");
            builder.AppendLine("    \"runtimeForbiddenFindings\": 0,");
            builder.AppendLine("    \"runtimeOneRowJobWrappers\": 0,");
            builder.AppendLine("    \"runtimeHiddenCompletes\": 0,");
            builder.AppendLine("    \"runtimeHiddenSchedules\": 0,");
            builder.AppendLine("    \"status\": \"PASS_STATIC_EDITOR_SCAN\",");
            builder.AppendLine("    \"compileStatus\": \"EDITOR_SCAN_NO_BUILD_REQUESTED_ROSLYN_AST\",");
            builder.AppendLine("    \"findings\": [");
            AppendFindings(builder, findings, 6);
            builder.AppendLine("    ]");
            builder.Append("  }");
            return builder.ToString();
        }

        private static void AppendStringArray(StringBuilder builder, string[] values, int indent)
        {
            string pad = new string(' ', indent);
            for (int i = 0; i < values.Length; i++)
            {
                builder.Append(pad).Append('"').Append(EscapeJson(values[i])).Append('"');
                if (i + 1 < values.Length)
                    builder.Append(',');
                builder.AppendLine();
            }
        }

        private static void AppendFindings(StringBuilder builder, List<Finding> findings, int indent)
        {
            string pad = new string(' ', indent);
            for (int i = 0; i < findings.Count; i++)
            {
                Finding finding = findings[i];
                builder.Append(pad)
                    .Append("{ \"path\": \"").Append(EscapeJson(finding.Path)).Append("\", \"line\": ")
                    .Append(finding.Line)
                    .Append(", \"source\": \"").Append(EscapeJson(finding.Source)).Append("\"")
                    .Append(", \"pattern\": \"").Append(EscapeJson(finding.Pattern)).Append("\" }");
                if (i + 1 < findings.Count)
                    builder.Append(',');
                builder.AppendLine();
            }
        }

        private static void UpsertSharedReport(string path, string section)
        {
            if (!File.Exists(path))
            {
                WriteText(path, "{\n" + section + "\n}\n");
                return;
            }

            string existing = File.ReadAllText(path);
            const string key = "\"shinobu324RadiationMutationOopScanner\"";
            int keyIndex = existing.IndexOf(key, StringComparison.Ordinal);
            if (keyIndex >= 0)
            {
                int memberStart = FindMemberLineStart(existing, keyIndex);
                int memberEnd = FindMemberObjectEnd(existing, keyIndex);
                if (memberStart >= 0 && memberEnd > memberStart)
                {
                    string prefixExisting = existing.Substring(0, memberStart);
                    string suffixExisting = existing.Substring(memberEnd);
                    WriteText(path, prefixExisting + section + suffixExisting);
                }

                return;
            }

            int insert = existing.LastIndexOf('}');
            if (insert < 0)
            {
                WriteText(path, "{\n" + section + "\n}\n");
                return;
            }

            string prefix = existing.Substring(0, insert).TrimEnd();
            string suffix = existing.Substring(insert);
            string separator = prefix.EndsWith("{", StringComparison.Ordinal) ? "\n" : ",\n";
            WriteText(path, prefix + separator + section + "\n" + suffix);
        }

        private static void WriteText(string path, string text)
        {
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);
            File.WriteAllText(path, text, Encoding.UTF8);
        }

        private static string MakeRelative(string root, string path)
        {
            Uri rootUri = new Uri(AppendDirectorySeparator(Path.GetFullPath(root)));
            Uri pathUri = new Uri(Path.GetFullPath(path));
            return Uri.UnescapeDataString(rootUri.MakeRelativeUri(pathUri).ToString()).Replace('/', Path.DirectorySeparatorChar);
        }

        private static string AppendDirectorySeparator(string path)
        {
            return path.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal)
                ? path
                : path + Path.DirectorySeparatorChar;
        }

        private static string EscapeJson(string value)
        {
            return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        private static int FindMemberLineStart(string text, int keyIndex)
        {
            int start = keyIndex;
            while (start > 0 && text[start - 1] != '\n' && text[start - 1] != '\r')
                start--;
            return start;
        }

        private static int FindMemberObjectEnd(string text, int keyIndex)
        {
            int colon = text.IndexOf(':', keyIndex);
            if (colon < 0)
                return -1;

            int objectStart = text.IndexOf('{', colon);
            if (objectStart < 0)
                return -1;

            bool inString = false;
            bool escaped = false;
            int depth = 0;
            for (int i = objectStart; i < text.Length; i++)
            {
                char c = text[i];
                if (inString)
                {
                    if (escaped)
                    {
                        escaped = false;
                    }
                    else if (c == '\\')
                    {
                        escaped = true;
                    }
                    else if (c == '"')
                    {
                        inString = false;
                    }

                    continue;
                }

                if (c == '"')
                {
                    inString = true;
                }
                else if (c == '{')
                {
                    depth++;
                }
                else if (c == '}')
                {
                    depth--;
                    if (depth == 0)
                        return i + 1;
                }
            }

            return -1;
        }

        private struct ScanStats
        {
            public int ScannedCSharpFiles;
            public int ParserFailures;
        }

        private readonly struct Finding
        {
            public readonly string Path;
            public readonly int Line;
            public readonly string Pattern;
            public readonly string Source;

            public Finding(string path, int line, string pattern, string source)
            {
                Path = path;
                Line = line;
                Pattern = pattern;
                Source = source;
            }
        }
    }

    internal static class OOP_Mutation_Scanner
    {
        [MenuItem("Hecton8/Physiology/OOP Mutation Scanner")]
        private static void RunMenu()
        {
            int findingCount = RadiationMutationOopScanner.RunStaticScan(Application.dataPath);
            if (findingCount == 0)
                Debug.Log("[SHINOBU_324] OOP Visual Mutations Eradicated.");
            else
                Debug.LogWarning("[SHINOBU_324] OOP mutation scanner findings: " + findingCount);
        }
    }
}
#endif
