#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Hecton8.Core;
using Hecton8.Power;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Hecton8.QA.Headless.Editor
{
    public sealed class JacobiStressFuzzerWindow : EditorWindow
    {
        private Label _stateLabel;
        private Label _flagsLabel;
        private Label _residualLabel;
        private Label _perfLabel;
        private Button _runButton;
        private ResidualGraphElement _graphElement;
        private PowerJacobiStressFuzzer.ScheduledRun _pendingRun;

        [MenuItem("Hecton/Power/Solver Fuzzer")]
        public static void Open()
        {
            JacobiStressFuzzerWindow window = GetWindow<JacobiStressFuzzerWindow>();
            window.titleContent = new GUIContent("Jacobi Power Fuzzer");
            window.minSize = new Vector2(560f, 320f);
        }

        public void CreateGUI()
        {
            VisualElement root = rootVisualElement;
            root.style.paddingLeft = 8;
            root.style.paddingRight = 8;
            root.style.paddingTop = 8;
            root.style.paddingBottom = 8;

            _runButton = new Button(RunHostileGraphTest) { text = "RUN 1,000 ITERATION FUZZ TEST" };
            root.Add(_runButton);

            _stateLabel = new Label("PENDING");
            _flagsLabel = new Label("failure flags: 0");
            _residualLabel = new Label("residual: 0");
            _perfLabel = new Label("solver/chain us: 0");
            _graphElement = new ResidualGraphElement();
            _graphElement.style.height = 120f;
            _graphElement.style.marginTop = 8f;
            root.Add(_stateLabel);
            root.Add(_flagsLabel);
            root.Add(_residualLabel);
            root.Add(_perfLabel);
            root.Add(_graphElement);

            Refresh(PowerJacobiStressFuzzerState.LastResult);
        }

        private void RunHostileGraphTest()
        {
            if (_pendingRun != null)
                return;

            if (!PowerJacobiStressFuzzer.TryScheduleDefault(out _pendingRun, out PowerJacobiStressFuzzerResult immediateResult))
            {
                PowerJacobiStressFuzzerState.LastResult = immediateResult;
                PowerJacobiStressFuzzerState.HasFailure = immediateResult.FailureFlags != 0u && immediateResult.FirstFailureNodeHash != 0u;
                PowerJacobiStressFuzzerState.LastFailureNodeHash = immediateResult.FirstFailureNodeHash;
                PowerJacobiStressFuzzerState.LastFailureAup = immediateResult.FirstFailureAup;
                Refresh(immediateResult);
                return;
            }

            if (_runButton != null)
                _runButton.SetEnabled(false);
            _stateLabel.text = "RUNNING";
            _flagsLabel.text = "scheduled background Burst chain";
            EditorApplication.update -= PollPendingRun;
            EditorApplication.update += PollPendingRun;
            EditorUtility.DisplayProgressBar("Jacobi Power Fuzzer", "Background Burst chain scheduled", 0.35f);
        }

        private void PollPendingRun()
        {
            if (_pendingRun == null)
            {
                EditorApplication.update -= PollPendingRun;
                EditorUtility.ClearProgressBar();
                if (_runButton != null)
                    _runButton.SetEnabled(true);
                return;
            }

            EditorUtility.DisplayProgressBar("Jacobi Power Fuzzer", "Waiting for scheduled Burst jobs", _pendingRun.ReadProgress01());
            if (!_pendingRun.IsCompleted())
                return;

            FinishPendingRun();
        }

        private void FinishPendingRun()
        {
            if (_pendingRun == null)
                return;

            PowerJacobiStressFuzzer.ScheduledRun run = _pendingRun;
            PowerJacobiStressFuzzerResult result = default;
            bool completed = false;
            try
            {
                run.Complete(out result);
                completed = true;
            }
            catch (Exception exception)
            {
                result.FailureFlags = PowerJacobiStressFuzzerConstants.FailureFlagMathCorruption;
                result.FirstFailureNodeHash = 1u;
                Debug.LogException(exception);
            }
            finally
            {
                run.Dispose();
                if (ReferenceEquals(_pendingRun, run))
                    _pendingRun = null;
                EditorApplication.update -= PollPendingRun;
                EditorUtility.ClearProgressBar();
                if (_runButton != null)
                    _runButton.SetEnabled(true);
            }

            if (!completed)
            {
                PowerJacobiStressFuzzerState.LastResult = result;
                PowerJacobiStressFuzzerState.HasFailure = true;
                PowerJacobiStressFuzzerState.LastFailureNodeHash = result.FirstFailureNodeHash;
            }

            Refresh(result);
            _graphElement?.MarkDirtyRepaint();
            SceneView.RepaintAll();
        }

        private void DisposePendingRun()
        {
            if (_pendingRun != null)
            {
                _pendingRun.Dispose();
                _pendingRun = null;
            }

            EditorApplication.update -= PollPendingRun;
            EditorUtility.ClearProgressBar();
            if (_runButton != null)
                _runButton.SetEnabled(true);
        }

        private void Refresh(PowerJacobiStressFuzzerResult result)
        {
            if (_stateLabel == null)
                return;

            bool passed = result.FailureFlags == 0u && result.FrameCount > 0;
            _stateLabel.text = passed ? "PASS" : "FAIL";
            _stateLabel.style.color = passed ? new Color(0.1f, 0.85f, 0.45f, 1f) : new Color(1f, 0.15f, 0.05f, 1f);
            _flagsLabel.text = "failure flags: " + result.FailureFlags + "  node: " + result.FirstFailureNodeHash;
            _residualLabel.text = "final residual: " + result.FinalResidual.ToString("0.000000") +
                                  "  max residual: " + result.MaxResidual.ToString("0.000000");
            _perfLabel.text = "solver/chain us: " + result.AverageSolverMicroseconds.ToString("0.000") +
                              "  managed bytes delta: " + result.ManagedBytesDelta;
            _graphElement?.MarkDirtyRepaint();
        }

        private void OnEnable()
        {
            SceneView.duringSceneGui -= DrawFailureSceneMarker;
            SceneView.duringSceneGui += DrawFailureSceneMarker;
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= DrawFailureSceneMarker;
            DisposePendingRun();
        }

        private static void DrawFailureSceneMarker(SceneView sceneView)
        {
            if (!PowerJacobiStressFuzzerState.HasFailure)
                return;

            double3 aup = PowerJacobiStressFuzzerState.LastFailureAup;
            Vector3 position = new Vector3((float)(aup.x % 10000.0), (float)aup.y, (float)(aup.z % 10000.0));
            Vector3 direction = new Vector3(
                PowerJacobiStressFuzzerState.LastFailureDirection.x,
                PowerJacobiStressFuzzerState.LastFailureDirection.y,
                PowerJacobiStressFuzzerState.LastFailureDirection.z);
            if (direction.sqrMagnitude < 0.001f)
                direction = Vector3.up;

            Handles.color = new Color(0.1f, 0.85f, 0.25f, 0.9f);
            Handles.DrawWireDisc(position, Vector3.up, 10f);
            Handles.color = new Color(1f, 0f, 0f, 0.9f);
            Handles.SphereHandleCap(0, position, Quaternion.identity, 8f, EventType.Repaint);
            Handles.color = new Color(1f, 0.9f, 0.05f, 0.95f);
            Handles.ArrowHandleCap(0, position, Quaternion.LookRotation(direction.normalized), 18f, EventType.Repaint);
        }

        private sealed class ResidualGraphElement : VisualElement
        {
            public ResidualGraphElement()
            {
                generateVisualContent += DrawGraph;
            }

            private static void DrawGraph(MeshGenerationContext context)
            {
                Rect rect = context.visualElement.contentRect;
                if (rect.width <= 1f || rect.height <= 1f)
                    return;

                Painter2D painter = context.painter2D;
                painter.lineWidth = 1.5f;
                painter.strokeColor = new Color(0.05f, 0.95f, 0.42f, 1f);
                painter.BeginPath();
                DrawSeries(painter, rect, PowerJacobiStressFuzzerState.ResidualSamples, 0.05f);
                painter.Stroke();

                painter.strokeColor = new Color(1f, 0.78f, 0.1f, 1f);
                painter.BeginPath();
                DrawSeries(painter, rect, PowerJacobiStressFuzzerState.OmegaSamples, PowerJacobiStressFuzzerConstants.OmegaMax);
                painter.Stroke();
            }

            private static void DrawSeries(Painter2D painter, Rect rect, float[] samples, float maxValue)
            {
                int count = samples.Length;
                float safeMax = Mathf.Max(0.0001f, maxValue);
                for (int i = 0; i < count; i++)
                {
                    float x = rect.xMin + (rect.width * i / Mathf.Max(1, count - 1));
                    float y = rect.yMax - Mathf.Clamp01(samples[i] / safeMax) * rect.height;
                    if (i == 0)
                        painter.MoveTo(new Vector2(x, y));
                    else
                        painter.LineTo(new Vector2(x, y));
                }
            }
        }
    }

    [InitializeOnLoad]
    internal static class JacobiFuzzLayoutGuard
    {
        static JacobiFuzzLayoutGuard()
        {
            bool valid = PowerJacobiStressFuzzer.ValidateRequiredLayouts() &&
                         UnsafeUtility.SizeOf<JacobiFuzzPowerNodeDTO>() == 32 &&
                         UnsafeUtility.AlignOf<JacobiFuzzPowerNodeDTO>() == 4 &&
                         UnsafeUtility.SizeOf<JacobiFuzzStateDTO>() == 32 &&
                         UnsafeUtility.AlignOf<JacobiFuzzStateDTO>() == 4 &&
                         UnsafeUtility.SizeOf<PowerJacobiStressDumpHeader>() == 64;
            if (!valid)
            {
                throw new FatalArchitectureException(
                    "SHINOBU_356 Jacobi fuzzer layout drift. Required node Size=32, state Size=32 Align=4, dump header Size=64.");
            }
        }
    }

    public static class OOP_Fuzz_Scanner
    {
        private const string ReportRelativePath = "Docs/Reports/QA_OPTIMIZATION_REPORT.json";
        private const string AgentReportRelativePath = "Docs/Reports/QA_OPTIMIZATION_REPORT_SHINOBU_356_SCANNER.json";
        private const string ReportSectionKey = "shinobu356JacobiPowerFuzzer";

        [MenuItem("Hecton/Power/Run OOP Fuzz Scanner")]
        public static void RunMenu()
        {
            Debug.Log("OOP Fuzz scanner wrote " + RunScan());
        }

        public static string RunScan()
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string reportPath = Path.GetFullPath(Path.Combine(projectRoot, ReportRelativePath));
            string agentReportPath = Path.GetFullPath(Path.Combine(projectRoot, AgentReportRelativePath));
            string scriptsPower = Path.Combine(Application.dataPath, "_Project/Scripts/Power");
            string qaFuzzerRoot = Path.Combine(Application.dataPath, "_Project/Scripts/QA/Headless/JacobiStressFuzzer");
            string qaEditorRoot = Path.Combine(Application.dataPath, "_Project/Scripts/QA/Headless/Editor/JacobiStressFuzzer");
            string testsRoot = Path.Combine(Application.dataPath, "_Project/Tests");
            Directory.CreateDirectory(Path.GetDirectoryName(reportPath));
            Directory.CreateDirectory(Path.GetDirectoryName(agentReportPath));

            int scannedFiles = 0;
            int contextFilteredFiles = 0;
            int oopFuzzerHits = 0;
            int physicsHits = 0;
            int gameObjectHits = 0;
            int ignoredNonOwnedManagedGraphHits = 0;
            int ignoredNonOwnedPhysicsHits = 0;
            int ignoredNonOwnedGameObjectHits = 0;
            int syntaxTreesParsed = 0;
            int syntaxNodesVisited = 0;
            int lexicalFallbackFiles = 0;
            int ownedParseFailureFiles = 0;
            int ignoredNonOwnedParseFailureFiles = 0;
            StringBuilder findings = new StringBuilder(512);
            StringBuilder ownedFiles = new StringBuilder(512);
            ScanRoot(projectRoot, scriptsPower, ref scannedFiles, ref contextFilteredFiles, ref oopFuzzerHits, ref physicsHits, ref gameObjectHits, ref ignoredNonOwnedManagedGraphHits, ref ignoredNonOwnedPhysicsHits, ref ignoredNonOwnedGameObjectHits, ref syntaxTreesParsed, ref syntaxNodesVisited, ref lexicalFallbackFiles, ref ownedParseFailureFiles, ref ignoredNonOwnedParseFailureFiles, findings, ownedFiles);
            ScanRoot(projectRoot, qaFuzzerRoot, ref scannedFiles, ref contextFilteredFiles, ref oopFuzzerHits, ref physicsHits, ref gameObjectHits, ref ignoredNonOwnedManagedGraphHits, ref ignoredNonOwnedPhysicsHits, ref ignoredNonOwnedGameObjectHits, ref syntaxTreesParsed, ref syntaxNodesVisited, ref lexicalFallbackFiles, ref ownedParseFailureFiles, ref ignoredNonOwnedParseFailureFiles, findings, ownedFiles);
            ScanRoot(projectRoot, qaEditorRoot, ref scannedFiles, ref contextFilteredFiles, ref oopFuzzerHits, ref physicsHits, ref gameObjectHits, ref ignoredNonOwnedManagedGraphHits, ref ignoredNonOwnedPhysicsHits, ref ignoredNonOwnedGameObjectHits, ref syntaxTreesParsed, ref syntaxNodesVisited, ref lexicalFallbackFiles, ref ownedParseFailureFiles, ref ignoredNonOwnedParseFailureFiles, findings, ownedFiles);
            ScanRoot(projectRoot, testsRoot, ref scannedFiles, ref contextFilteredFiles, ref oopFuzzerHits, ref physicsHits, ref gameObjectHits, ref ignoredNonOwnedManagedGraphHits, ref ignoredNonOwnedPhysicsHits, ref ignoredNonOwnedGameObjectHits, ref syntaxTreesParsed, ref syntaxNodesVisited, ref lexicalFallbackFiles, ref ownedParseFailureFiles, ref ignoredNonOwnedParseFailureFiles, findings, ownedFiles);

            bool clean = oopFuzzerHits == 0 && physicsHits == 0 && gameObjectHits == 0 && ownedParseFailureFiles == 0;
            StringBuilder section = new StringBuilder(1024);
            section.Append("{\n");
            section.Append("    \"agent\": \"SHINOBU_356\",\n");
            section.Append("    \"scanner\": \"OOP_Fuzz_Scanner\",\n");
            section.Append("    \"evidence_class\": \"STATIC_SOURCE_ROSLYN_AST_PRIMARY\",\n");
            section.Append("    \"status\": \"");
            section.Append(clean ? "OOP_FUZZERS_ERADICATED_OWNED_CONTEXT_AST" : ownedParseFailureFiles > 0 ? "OOP_FUZZ_SCANNER_PARSE_FAILURE_OWNED_CONTEXT" : "OOP_FUZZERS_REMAIN");
            section.Append("\",\n");
            section.Append("    \"scannedRoots\": [\"Assets/_Project/Scripts/Power\", \"Assets/_Project/Scripts/QA/Headless/JacobiStressFuzzer\", \"Assets/_Project/Scripts/QA/Headless/Editor/JacobiStressFuzzer\", \"Assets/_Project/Tests\"],\n");
            section.Append("    \"scannerParserRoute\": \"Roslyn CSharpSyntaxTree AST primary pass; parse failures are fail-closed for owned files and lexical fallback is diagnostic only.\",\n");
            section.Append("    \"residualRisk\": \"Static source AST scanner is not Unity import, Burst Inspector, or profiler proof; compile/profiler proof remains pending until CPU and project-target guards clear.\",\n");
            section.Append("    \"scopeNote\": \"Counts below are explicit allow-list owned files only; broad roots are scanned to discover external noise, and non-owned Power/Test tokens are tracked separately as ignoredNonOwned counts.\",\n");
            section.Append("    \"scannedFiles\": ");
            section.Append(scannedFiles);
            section.Append(",\n    \"contextFilteredFiles\": ");
            section.Append(contextFilteredFiles);
            section.Append(",\n    \"syntaxTreesParsed\": ");
            section.Append(syntaxTreesParsed);
            section.Append(",\n    \"syntaxNodesVisited\": ");
            section.Append(syntaxNodesVisited);
            section.Append(",\n    \"lexicalFallbackFiles\": ");
            section.Append(lexicalFallbackFiles);
            section.Append(",\n    \"ownedParseFailureFiles\": ");
            section.Append(ownedParseFailureFiles);
            section.Append(",\n    \"ignoredNonOwnedParseFailureFiles\": ");
            section.Append(ignoredNonOwnedParseFailureFiles);
            section.Append(",\n    \"ownedFiles\": [");
            section.Append(ownedFiles);
            section.Append("\n    ]");
            section.Append(",\n    \"managedGraphHits\": ");
            section.Append(oopFuzzerHits);
            section.Append(",\n    \"physicsApiHits\": ");
            section.Append(physicsHits);
            section.Append(",\n    \"gameObjectInstantiationHits\": ");
            section.Append(gameObjectHits);
            section.Append(",\n    \"ignoredNonOwnedManagedGraphHits\": ");
            section.Append(ignoredNonOwnedManagedGraphHits);
            section.Append(",\n    \"ignoredNonOwnedPhysicsHits\": ");
            section.Append(ignoredNonOwnedPhysicsHits);
            section.Append(",\n    \"ignoredNonOwnedGameObjectInstantiationHits\": ");
            section.Append(ignoredNonOwnedGameObjectHits);
            section.Append(",\n    \"findings\": [");
            section.Append(findings);
            section.Append("\n    ]\n  }");
            File.WriteAllText(agentReportPath, "{\n  \"shinobu356JacobiPowerFuzzer\": " + section.ToString() + "\n}\n", Encoding.UTF8);
            File.WriteAllText(reportPath, MergeTopLevelSection(reportPath, ReportSectionKey, section.ToString()), Encoding.UTF8);
            return reportPath;
        }

        private static void ScanRoot(
            string projectRoot,
            string root,
            ref int scannedFiles,
            ref int contextFilteredFiles,
            ref int oopFuzzerHits,
            ref int physicsHits,
            ref int gameObjectHits,
            ref int ignoredNonOwnedManagedGraphHits,
            ref int ignoredNonOwnedPhysicsHits,
            ref int ignoredNonOwnedGameObjectHits,
            ref int syntaxTreesParsed,
            ref int syntaxNodesVisited,
            ref int lexicalFallbackFiles,
            ref int ownedParseFailureFiles,
            ref int ignoredNonOwnedParseFailureFiles,
            StringBuilder findings,
            StringBuilder ownedFiles)
        {
            if (!Directory.Exists(root))
                return;

            string[] files = Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories);
            for (int i = 0; i < files.Length; i++)
            {
                string path = files[i];
                string text = File.ReadAllText(path);
                scannedFiles++;
                bool powerFuzzContext = IsOwnedFuzzerFile(path);
                int managedGraph;
                int physics;
                int gameObjects;
                bool parseFailure = false;
                if (!TryCountAstTokens(text, ref syntaxTreesParsed, ref syntaxNodesVisited, out managedGraph, out physics, out gameObjects))
                {
                    parseFailure = true;
                    lexicalFallbackFiles++;
                    string stripped = StripCommentsAndStrings(text);
                    managedGraph = CountManagedGraphTokens(stripped);
                    physics = CountPhysicsTokens(stripped);
                    gameObjects = CountGameObjectTokens(stripped);
                }
                if (!powerFuzzContext)
                {
                    ignoredNonOwnedManagedGraphHits += managedGraph;
                    ignoredNonOwnedPhysicsHits += physics;
                    ignoredNonOwnedGameObjectHits += gameObjects;
                    if (parseFailure)
                        ignoredNonOwnedParseFailureFiles++;
                    continue;
                }

                contextFilteredFiles++;
                if (parseFailure)
                    ownedParseFailureFiles++;
                AppendJsonStringArrayItem(ownedFiles, ToProjectRelative(projectRoot, path));
                oopFuzzerHits += managedGraph;
                physicsHits += physics;
                gameObjectHits += gameObjects;
                if (managedGraph + physics + gameObjects > 0 || parseFailure)
                    AppendFinding(findings, ToProjectRelative(projectRoot, path), managedGraph, physics, gameObjects, parseFailure);
            }
        }

        private static bool IsOwnedFuzzerFile(string path)
        {
            string normalized = path.Replace('\\', '/');
            return normalized.EndsWith("/Assets/_Project/Scripts/QA/Headless/JacobiStressFuzzer/PowerGridJacobiStressFuzzer.cs", StringComparison.OrdinalIgnoreCase) ||
                   normalized.EndsWith("/Assets/_Project/Scripts/QA/Headless/Editor/JacobiStressFuzzer/JacobiStressFuzzerWindow.cs", StringComparison.OrdinalIgnoreCase) ||
                   normalized.EndsWith("/Assets/_Project/Tests/Editor/PowerGridJacobiStressFuzzerEditTests.cs", StringComparison.OrdinalIgnoreCase);
        }

        private static bool TryCountAstTokens(
            string text,
            ref int syntaxTreesParsed,
            ref int syntaxNodesVisited,
            out int managedGraph,
            out int physics,
            out int gameObjects)
        {
            managedGraph = 0;
            physics = 0;
            gameObjects = 0;
            SyntaxTree tree;
            CompilationUnitSyntax root;
            try
            {
                tree = CSharpSyntaxTree.ParseText(text);
                root = tree.GetCompilationUnitRoot();
            }
            catch
            {
                return false;
            }

            syntaxTreesParsed++;
            if (HasSyntaxErrors(tree))
                return false;

            using (IEnumerator<SyntaxNode> nodes = root.DescendantNodes().GetEnumerator())
            {
                while (nodes.MoveNext())
                {
                    SyntaxNode node = nodes.Current;
                    syntaxNodesVisited++;
                    if (node is ClassDeclarationSyntax classDeclaration)
                    {
                        string name = classDeclaration.Identifier.ValueText;
                        if (IsForbiddenManagedGraphTypeName(name))
                            managedGraph++;
                        if (HasForbiddenUnityBase(classDeclaration.BaseList))
                            gameObjects++;
                    }
                    else if (node is GenericNameSyntax genericName)
                    {
                        if (IsForbiddenListGraphType(genericName))
                            managedGraph++;
                    }
                    else if (node is ObjectCreationExpressionSyntax objectCreation)
                    {
                        string type = objectCreation.Type.ToString();
                        if (IsForbiddenUnityObjectType(type))
                            gameObjects++;
                    }
                    else if (node is VariableDeclarationSyntax variableDeclaration)
                    {
                        if (IsForbiddenUnityObjectType(variableDeclaration.Type.ToString()))
                            gameObjects++;
                    }
                    else if (node is ParameterSyntax parameter)
                    {
                        TypeSyntax parameterType = parameter.Type;
                        if (parameterType != null && IsForbiddenUnityObjectType(parameterType.ToString()))
                            gameObjects++;
                    }
                    else if (node is PropertyDeclarationSyntax propertyDeclaration)
                    {
                        if (IsForbiddenUnityObjectType(propertyDeclaration.Type.ToString()))
                            gameObjects++;
                    }
                    else if (node is InvocationExpressionSyntax invocation)
                    {
                        string expression = invocation.Expression.ToString();
                        if (IsPhysicsInvocation(expression))
                            physics++;
                        if (IsGameObjectInvocation(expression))
                            gameObjects++;
                    }
                    else if (node is IdentifierNameSyntax identifier)
                    {
                        if (identifier.Identifier.ValueText == "Raycast" + "Command")
                            physics++;
                    }
                }
            }

            return true;
        }

        private static bool HasSyntaxErrors(SyntaxTree tree)
        {
            using (IEnumerator<Diagnostic> enumerator = tree.GetDiagnostics().GetEnumerator())
            {
                while (enumerator.MoveNext())
                {
                    if (enumerator.Current.Severity == DiagnosticSeverity.Error)
                        return true;
                }
            }

            return false;
        }

        private static bool IsForbiddenListGraphType(GenericNameSyntax genericName)
        {
            if (genericName.Identifier.ValueText != "List")
                return false;

            SeparatedSyntaxList<TypeSyntax> arguments = genericName.TypeArgumentList.Arguments;
            for (int i = 0; i < arguments.Count; i++)
            {
                string type = arguments[i].ToString();
                if (IsForbiddenManagedGraphTypeName(type))
                    return true;
            }

            return false;
        }

        private static bool HasForbiddenUnityBase(BaseListSyntax baseList)
        {
            if (baseList == null)
                return false;

            SeparatedSyntaxList<BaseTypeSyntax> types = baseList.Types;
            for (int i = 0; i < types.Count; i++)
            {
                string type = types[i].Type.ToString();
                if (type == "MonoBehaviour" ||
                    type == "UnityEngine.MonoBehaviour" ||
                    type.EndsWith(".MonoBehaviour", StringComparison.Ordinal) ||
                    type == "Component" ||
                    type == "UnityEngine.Component" ||
                    type.EndsWith(".Component", StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsForbiddenManagedGraphTypeName(string type)
        {
            return type == "Node" ||
                   type == "GraphNode" ||
                   type == "Connection" ||
                   type == "NodeDTO" ||
                   type.EndsWith(".Node", StringComparison.Ordinal) ||
                   type.EndsWith(".GraphNode", StringComparison.Ordinal) ||
                   type.EndsWith(".Connection", StringComparison.Ordinal) ||
                   type.EndsWith(".NodeDTO", StringComparison.Ordinal);
        }

        private static bool IsForbiddenUnityObjectType(string type)
        {
            return type == "GameObject" ||
                   type == "Transform" ||
                   type == "Rigidbody" ||
                   type == "Collider" ||
                   type == "UnityEngine.GameObject" ||
                   type == "UnityEngine.Transform" ||
                   type == "UnityEngine.Rigidbody" ||
                   type == "UnityEngine.Collider" ||
                   type.EndsWith(".GameObject", StringComparison.Ordinal) ||
                   type.EndsWith(".Transform", StringComparison.Ordinal) ||
                   type.EndsWith(".Rigidbody", StringComparison.Ordinal) ||
                   type.EndsWith(".Collider", StringComparison.Ordinal);
        }

        private static bool IsPhysicsInvocation(string expression)
        {
            return expression == "Physics." + "Raycast" ||
                   expression == "Physics." + "RaycastAll" ||
                   expression == "Physics." + "RaycastNonAlloc" ||
                   expression == "Physics." + "SphereCast" ||
                   expression == "Physics." + "OverlapSphere" ||
                   expression == "UnityEngine." + "Physics." + "Raycast" ||
                   expression == "UnityEngine." + "Physics." + "RaycastAll" ||
                   expression == "UnityEngine." + "Physics." + "RaycastNonAlloc" ||
                   expression == "UnityEngine." + "Physics." + "SphereCast" ||
                   expression == "UnityEngine." + "Physics." + "OverlapSphere" ||
                   expression.StartsWith("PhysicsScene.", StringComparison.Ordinal) ||
                   expression.StartsWith("UnityEngine.PhysicsScene.", StringComparison.Ordinal) ||
                   expression.IndexOf(".Physics." + "Raycast", StringComparison.Ordinal) >= 0 ||
                   expression.IndexOf(".PhysicsScene.", StringComparison.Ordinal) >= 0;
        }

        private static bool IsGameObjectInvocation(string expression)
        {
            return expression == "Instantiate" ||
                   expression == "Object." + "Instantiate" ||
                   expression == "UnityEngine.Object." + "Instantiate" ||
                   expression.IndexOf(".Instantiate", StringComparison.Ordinal) >= 0 ||
                   expression.IndexOf(".AddComponent", StringComparison.Ordinal) >= 0;
        }

        private static int CountManagedGraphTokens(string stripped)
        {
            return CountToken(stripped, "class " + "Node") +
                   CountToken(stripped, "class " + "GraphNode") +
                   CountToken(stripped, "List<" + "Connection>") +
                   CountToken(stripped, "List<" + "Node>") +
                   CountToken(stripped, "List<" + "NodeDTO>");
        }

        private static int CountPhysicsTokens(string stripped)
        {
            return CountToken(stripped, "UnityEngine." + "Physics") +
                   CountToken(stripped, "Physics." + "Raycast(") +
                   CountToken(stripped, "Physics." + "RaycastAll(") +
                   CountToken(stripped, "Physics." + "RaycastNonAlloc(") +
                   CountToken(stripped, "Physics." + "SphereCast(") +
                   CountToken(stripped, "Physics." + "Overlap") +
                   CountToken(stripped, "Raycast" + "Command");
        }

        private static int CountGameObjectTokens(string stripped)
        {
            return CountToken(stripped, "new " + "GameObject") +
                   CountToken(stripped, "Object." + "Instantiate") +
                   CountToken(stripped, "UnityEngine.Object." + "Instantiate") +
                   CountToken(stripped, "Instantiate" + "(") +
                   CountToken(stripped, "Add" + "Component<");
        }

        private static string MergeTopLevelSection(string reportPath, string sectionKey, string sectionJson)
        {
            string keyLiteral = "\"" + sectionKey + "\"";
            string body = File.Exists(reportPath) ? File.ReadAllText(reportPath, Encoding.UTF8).Trim() : string.Empty;
            if (body.Length < 2 || body[0] != '{')
                return "{\n  " + keyLiteral + ": " + sectionJson + "\n}\n";

            body = RemoveExistingSection(body, keyLiteral).TrimEnd();
            int close = body.LastIndexOf('}');
            if (close < 0)
                return "{\n  " + keyLiteral + ": " + sectionJson + "\n}\n";

            string prefix = body.Substring(0, close).TrimEnd();
            bool hasExisting = prefix.Length > 1;
            string comma = hasExisting ? ",\n" : "\n";
            return prefix + comma + "  " + keyLiteral + ": " + sectionJson + "\n}\n";
        }

        private static string RemoveExistingSection(string body, string keyLiteral)
        {
            int key = body.IndexOf(keyLiteral, StringComparison.Ordinal);
            if (key < 0)
                return body;

            int start = key;
            while (start > 0 && body[start - 1] != '{' && body[start - 1] != ',')
                start--;
            if (start > 0 && body[start - 1] == ',')
                start--;

            int colon = body.IndexOf(':', key + keyLiteral.Length);
            if (colon < 0)
                return body;

            int index = colon + 1;
            while (index < body.Length && char.IsWhiteSpace(body[index]))
                index++;
            if (index >= body.Length || body[index] != '{')
                return body;

            int depth = 0;
            bool inString = false;
            bool escaped = false;
            for (; index < body.Length; index++)
            {
                char c = body[index];
                if (inString)
                {
                    if (escaped)
                    {
                        escaped = false;
                        continue;
                    }

                    if (c == '\\')
                    {
                        escaped = true;
                        continue;
                    }

                    if (c == '"')
                        inString = false;
                    continue;
                }

                if (c == '"')
                    inString = true;
                else if (c == '{')
                    depth++;
                else if (c == '}')
                {
                    depth--;
                    if (depth == 0)
                    {
                        int end = index + 1;
                        while (end < body.Length && char.IsWhiteSpace(body[end]))
                            end++;
                        if (end < body.Length && body[end] == ',')
                            end++;
                        return body.Remove(start, end - start);
                    }
                }
            }

            return body;
        }

        private static string ToProjectRelative(string projectRoot, string path)
        {
            if (path.StartsWith(projectRoot, StringComparison.OrdinalIgnoreCase))
            {
                int start = projectRoot.Length;
                if (start < path.Length && (path[start] == '\\' || path[start] == '/'))
                    start++;
                return path.Substring(start);
            }

            return path;
        }

        private static void AppendJsonStringArrayItem(StringBuilder builder, string value)
        {
            if (builder.Length > 0)
                builder.Append(",");
            builder.Append("\n      \"");
            builder.Append(Escape(value.Replace('\\', '/')));
            builder.Append("\"");
        }

        private static void AppendFinding(StringBuilder findings, string projectRelative, int managedGraph, int physics, int gameObjects, bool parseFailure)
        {
            if (findings.Length > 0)
                findings.Append(",");
            findings.Append("\n    { \"file\": \"");
            findings.Append(Escape(projectRelative.Replace('\\', '/')));
            findings.Append("\", \"managedGraph\": ");
            findings.Append(managedGraph);
            findings.Append(", \"physics\": ");
            findings.Append(physics);
            findings.Append(", \"gameObject\": ");
            findings.Append(gameObjects);
            findings.Append(", \"parseFailure\": ");
            findings.Append(parseFailure ? "true" : "false");
            findings.Append(" }");
        }

        private static int CountToken(string text, string token)
        {
            int count = 0;
            int index = 0;
            while (index < text.Length)
            {
                int found = text.IndexOf(token, index, StringComparison.Ordinal);
                if (found < 0)
                    break;
                count++;
                index = found + token.Length;
            }

            return count;
        }

        private static string StripCommentsAndStrings(string text)
        {
            char[] chars = text.ToCharArray();
            bool inLineComment = false;
            bool inBlockComment = false;
            bool inString = false;
            bool inChar = false;
            bool escaped = false;
            for (int i = 0; i < chars.Length; i++)
            {
                char c = chars[i];
                char next = i + 1 < chars.Length ? chars[i + 1] : '\0';
                if (inLineComment)
                {
                    if (c == '\n' || c == '\r')
                        inLineComment = false;
                    else
                        chars[i] = ' ';
                    continue;
                }

                if (inBlockComment)
                {
                    if (c == '*' && next == '/')
                    {
                        chars[i] = ' ';
                        chars[i + 1] = ' ';
                        i++;
                        inBlockComment = false;
                    }
                    else
                    {
                        chars[i] = ' ';
                    }
                    continue;
                }

                if (inString || inChar)
                {
                    if (escaped)
                    {
                        escaped = false;
                        chars[i] = ' ';
                        continue;
                    }

                    if (c == '\\')
                    {
                        escaped = true;
                        chars[i] = ' ';
                        continue;
                    }

                    if ((inString && c == '"') || (inChar && c == '\''))
                    {
                        inString = false;
                        inChar = false;
                    }
                    chars[i] = ' ';
                    continue;
                }

                if (c == '/' && next == '/')
                {
                    chars[i] = ' ';
                    chars[i + 1] = ' ';
                    i++;
                    inLineComment = true;
                }
                else if (c == '/' && next == '*')
                {
                    chars[i] = ' ';
                    chars[i + 1] = ' ';
                    i++;
                    inBlockComment = true;
                }
                else if (c == '"')
                {
                    chars[i] = ' ';
                    inString = true;
                }
                else if (c == '\'')
                {
                    chars[i] = ' ';
                    inChar = true;
                }
            }

            return new string(chars);
        }

        private static string Escape(string value)
        {
            return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }
    }
}
#endif
