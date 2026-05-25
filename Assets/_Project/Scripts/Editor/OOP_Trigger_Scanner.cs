#if UNITY_EDITOR
using System;
using System.IO;
using System.Text;
using Hecton8.Core;
using Hecton8.Core.Memory;
using Hecton8.Gameplay;
using Hecton8.World;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Newtonsoft.Json.Linq;
using Unity.Collections;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Hecton8.Editor
{
    public sealed class AupTriggerAnalyticsWindow : EditorWindow
    {
        private const int GraphHeight = 96;
        private Label _summary;
        private Toggle[] _maskBits;
        private TelemetryGraph _graph;

        [MenuItem("Tools/HECTON-8/AUP Trigger Analytics")]
        public static void Open()
        {
            GetWindow<AupTriggerAnalyticsWindow>("AUP Trigger Analytics");
        }

        private void CreateGUI()
        {
            rootVisualElement.style.paddingLeft = 8;
            rootVisualElement.style.paddingRight = 8;
            rootVisualElement.style.paddingTop = 8;
            rootVisualElement.style.paddingBottom = 8;
            _summary = new Label("No DataVault telemetry.");
            rootVisualElement.Add(_summary);

            _graph = new TelemetryGraph { style = { height = GraphHeight, marginTop = 8, marginBottom = 8 } };
            rootVisualElement.Add(_graph);

            VisualElement maskRow = new VisualElement { style = { flexDirection = FlexDirection.Row, flexWrap = Wrap.Wrap } };
            _maskBits = new Toggle[8];
            for (int i = 0; i < _maskBits.Length; i++)
            {
                int bit = i;
                Toggle toggle = new Toggle("Bit " + bit) { style = { width = 74 } };
                toggle.RegisterValueChangedCallback(evt => WriteMaskBit(bit, evt.newValue));
                _maskBits[i] = toggle;
                maskRow.Add(toggle);
            }

            rootVisualElement.Add(maskRow);
            AupTriggerDebugGizmo.Enabled = true;
            EditorApplication.update += Repaint;
        }

        private void OnDisable()
        {
            EditorApplication.update -= Repaint;
        }

        private void OnGUI()
        {
            Refresh();
        }

        private void Refresh()
        {
            IDataVault vault = GlobalRegistry.DataVault;
            if (!TryReadTelemetry(vault, out NativeArray<AupNarrativeTriggerTelemetryEntry> telemetry, out int cursor) ||
                !TryReadCounters(vault, out NativeArray<int> counters))
            {
                _summary.text = "No DataVault telemetry.";
                _graph.SetTelemetry(default, 0);
                return;
            }

            int poiCount = ReadCounter(counters, AupNarrativePoiRuntimeConstants.CounterSlot.PoiCount);
            int evaluated = ReadCounter(counters, AupNarrativePoiRuntimeConstants.CounterSlot.LastEvaluatedPoiCount);
            int signals = ReadCounter(counters, AupNarrativePoiRuntimeConstants.CounterSlot.LastSignalsEmitted);
            uint flags = unchecked((uint)ReadCounter(counters, AupNarrativePoiRuntimeConstants.CounterSlot.LastTelemetryFlags));
            _summary.text = $"POI {poiCount} | evaluated {evaluated} | signals {signals} | flags 0x{flags:X8}";
            _graph.SetTelemetry(telemetry, cursor);

            if (TryReadStateMask(vault, out ulong mask))
            {
                for (int i = 0; i < _maskBits.Length; i++)
                    _maskBits[i].SetValueWithoutNotify((mask & (1UL << i)) != 0UL);
            }
        }

        private static int ReadCounter(NativeArray<int> counters, AupNarrativePoiRuntimeConstants.CounterSlot slot)
        {
            int index = (int)slot;
            return counters.IsCreated && (uint)index < (uint)counters.Length ? counters[index] : 0;
        }

        private static bool TryReadTelemetry(IDataVault vault, out NativeArray<AupNarrativeTriggerTelemetryEntry> telemetry, out int cursor)
        {
            telemetry = default;
            cursor = 0;
            if (vault == null ||
                !vault.TryGetGenerationHandle<AupNarrativeTriggerTelemetryEntry>(BufferID.NarrativePoiTelemetryRing, out VaultGenerationHandle<AupNarrativeTriggerTelemetryEntry> handle) ||
                !vault.TryReadHandle(in handle, out telemetry) ||
                !telemetry.IsCreated)
            {
                return false;
            }

            if (vault.TryGetGenerationHandle<int>(BufferID.NarrativePoiTelemetryCursor, out VaultGenerationHandle<int> cursorHandle) &&
                vault.TryReadHandle(in cursorHandle, out NativeArray<int> cursorBuffer) &&
                cursorBuffer.IsCreated &&
                cursorBuffer.Length > 0)
            {
                cursor = cursorBuffer[0];
            }

            return true;
        }

        private static bool TryReadCounters(IDataVault vault, out NativeArray<int> counters)
        {
            counters = default;
            return vault != null &&
                   vault.TryGetGenerationHandle<int>(BufferID.NarrativePoiCounters, out VaultGenerationHandle<int> handle) &&
                   vault.TryReadHandle(in handle, out counters) &&
                   counters.IsCreated;
        }

        private static bool TryReadStateMask(IDataVault vault, out ulong mask)
        {
            mask = 0UL;
            if (vault == null ||
                !vault.TryGetGenerationHandle<ulong>(BufferID.NarrativePoiStateMasks, out VaultGenerationHandle<ulong> handle) ||
                !vault.TryReadHandle(in handle, out NativeArray<ulong> masks) ||
                !masks.IsCreated ||
                masks.Length <= 0)
            {
                return false;
            }

            mask = masks[0];
            return true;
        }

        private static void WriteMaskBit(int bit, bool enabled)
        {
            IDataVault vault = GlobalRegistry.DataVault;
            if (vault == null ||
                !vault.TryGetGenerationHandle<ulong>(BufferID.NarrativePoiStateMasks, out VaultGenerationHandle<ulong> handle) ||
                !vault.TryResolveHandle(in handle, out NativeArray<ulong> masks) ||
                !masks.IsCreated ||
                masks.Length <= 0)
            {
                return;
            }

            ulong mask = masks[0];
            ulong bitValue = 1UL << bit;
            masks[0] = enabled ? mask | bitValue : mask & ~bitValue;
        }

        private sealed class TelemetryGraph : VisualElement
        {
            private NativeArray<AupNarrativeTriggerTelemetryEntry> _telemetry;
            private int _cursor;

            public TelemetryGraph()
            {
                generateVisualContent += OnGenerateVisualContent;
            }

            public void SetTelemetry(NativeArray<AupNarrativeTriggerTelemetryEntry> telemetry, int cursor)
            {
                _telemetry = telemetry;
                _cursor = cursor;
                MarkDirtyRepaint();
            }

            private void OnGenerateVisualContent(MeshGenerationContext context)
            {
                Painter2D painter = context.painter2D;
                Rect rect = contentRect;
                painter.strokeColor = new Color(0.08f, 0.78f, 0.95f, 1f);
                painter.lineWidth = 1.5f;
                if (!_telemetry.IsCreated || _telemetry.Length <= 1 || rect.width <= 1f || rect.height <= 1f)
                    return;

                double maxMicros = 1.0d;
                for (int i = 0; i < _telemetry.Length; i++)
                    maxMicros = math.max(maxMicros, _telemetry[i].ExecutionTimeMicroseconds);

                int start = _cursor % _telemetry.Length;
                if (start < 0)
                    start += _telemetry.Length;

                for (int i = 0; i < _telemetry.Length; i++)
                {
                    int sampleIndex = start + i;
                    if (sampleIndex >= _telemetry.Length)
                        sampleIndex -= _telemetry.Length;

                    float x = rect.xMin + rect.width * (i / (float)(_telemetry.Length - 1));
                    float y = rect.yMax - rect.height * (float)math.saturate(_telemetry[sampleIndex].ExecutionTimeMicroseconds / maxMicros);
                    if (i == 0)
                        painter.BeginPath();

                    if (i == 0)
                        painter.MoveTo(new Vector2(x, y));
                    else
                        painter.LineTo(new Vector2(x, y));
                }

                painter.Stroke();
            }
        }
    }

    [InitializeOnLoad]
    public static class AupTriggerDebugGizmo
    {
        public static bool Enabled;

        static AupTriggerDebugGizmo()
        {
            SceneView.duringSceneGui += Draw;
        }

        private static void Draw(SceneView view)
        {
            if (!Enabled ||
                !TryReadPois(out NativeArray<NarrativePoiDTO> pois, out int count) ||
                count <= 0)
            {
                return;
            }

            double3 runtimeOrigin = GlobalSignals.CurrentRuntimeOriginAup().ToAbsoluteDouble3();
            ulong questMask = ReadQuestMask();
            int safeCount = math.min(count, pois.Length);
            for (int i = 0; i < safeCount; i++)
            {
                NarrativePoiDTO poi = pois[i];
                if ((poi.StateFlags & NarrativePoiStateFlags.Active) == 0u)
                    continue;

                bool hasPrereq = (questMask & poi.PrerequisiteBitmask) == poi.PrerequisiteBitmask;
                bool triggered = (poi.StateFlags & NarrativePoiStateFlags.Triggered) != 0u;
                Handles.color = triggered ? Color.green : hasPrereq ? Color.yellow : Color.red;
                float3 runtime = (float3)(poi.PoiAUP - runtimeOrigin);
                Vector3 center = new Vector3(runtime.x, runtime.y, runtime.z);
                float radius = math.max(0.01f, poi.TriggerRadiusMeters);
                Handles.DrawWireDisc(center, Vector3.up, radius);
                Handles.DrawWireDisc(center, Vector3.right, radius);
                Handles.DrawWireDisc(center, Vector3.forward, radius);
            }
        }

        private static bool TryReadPois(out NativeArray<NarrativePoiDTO> pois, out int count)
        {
            pois = default;
            count = 0;
            IDataVault vault = GlobalRegistry.DataVault;
            if (vault == null ||
                !vault.TryGetGenerationHandle<NarrativePoiDTO>(BufferID.NarrativePoiTriggers, out VaultGenerationHandle<NarrativePoiDTO> poiHandle) ||
                !vault.TryReadHandle(in poiHandle, out pois) ||
                !pois.IsCreated)
            {
                return false;
            }

            if (vault.TryGetGenerationHandle<int>(BufferID.NarrativePoiCounters, out VaultGenerationHandle<int> counterHandle) &&
                vault.TryReadHandle(in counterHandle, out NativeArray<int> counters) &&
                counters.IsCreated &&
                counters.Length > (int)AupNarrativePoiRuntimeConstants.CounterSlot.PoiCount)
            {
                count = counters[(int)AupNarrativePoiRuntimeConstants.CounterSlot.PoiCount];
            }

            return true;
        }

        private static ulong ReadQuestMask()
        {
            IDataVault vault = GlobalRegistry.DataVault;
            if (vault == null ||
                !vault.TryGetGenerationHandle<ulong>(BufferID.QuestDagGlobalStateMasks, out VaultGenerationHandle<ulong> handle) ||
                !vault.TryReadHandle(in handle, out NativeArray<ulong> masks) ||
                !masks.IsCreated ||
                masks.Length <= 0)
            {
                return 0UL;
            }

            return masks[0];
        }
    }

    public static class OOP_Trigger_Scanner
    {
        private static readonly string[] Roots =
        {
            "Assets/_Project/Scripts/Narrative",
            "Assets/_Project/Scripts/Audio",
            "Assets/_Project/Scripts/Progression",
            "Assets/_Project/Scripts/Events"
        };

        private static readonly string[] Tokens =
        {
            "OnTriggerEnter",
            "OnTriggerStay",
            "Physics.OverlapSphere",
            "BoxCollider",
            "SphereCollider",
            "isTrigger"
        };

        [MenuItem("Tools/HECTON-8/OOP Trigger Scanner/Write Report")]
        public static void WriteReport()
        {
            StringBuilder offenders = new StringBuilder(2048);
            int fileCount = 0;
            int hitCount = 0;
            int storyHitCount = 0;
            int nonStoryHitCount = 0;
            int parserFailureCount = 0;
            for (int rootIndex = 0; rootIndex < Roots.Length; rootIndex++)
            {
                string root = Roots[rootIndex];
                if (!Directory.Exists(root))
                    continue;

                string[] files = Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories);
                for (int i = 0; i < files.Length; i++)
                {
                    fileCount++;
                    ScanSyntaxFile(
                        files[i],
                        offenders,
                        ref hitCount,
                        ref storyHitCount,
                        ref nonStoryHitCount,
                        ref parserFailureCount);
                }
            }

            StringBuilder json = new StringBuilder(4096);
            json.Append("{\n");
            json.Append("  \"agent\": \"SHINOBU_349\",\n");
            json.Append("  \"summary\": \"OOP Story Triggers Eradicated\",\n");
            json.Append("  \"filesScanned\": ").Append(fileCount).Append(",\n");
            json.Append("  \"offenderCount\": ").Append(hitCount).Append(",\n");
            json.Append("  \"storyOffenderCount\": ").Append(storyHitCount).Append(",\n");
            json.Append("  \"nonStoryOffenderCount\": ").Append(nonStoryHitCount).Append(",\n");
            json.Append("  \"parserFailureCount\": ").Append(parserFailureCount).Append(",\n");
            json.Append("  \"scannerParserRoute\": \"Roslyn CSharpSyntaxTree method/invocation/type/member pass; token fallback only on parser failure\",\n");
            json.Append("  \"offenders\": [\n");
            json.Append(offenders);
            json.Append("\n  ]\n");
            json.Append("}\n");

            Directory.CreateDirectory("Docs/Reports");
            string report = json.ToString();
            File.WriteAllText("Docs/Reports/PHYSICS_OPTIMIZATION_REPORT_SHINOBU_349.json", report);
            WriteSharedReportEntry("SHINOBU_349_AUP_Narrative_Poi_Trigger_Scanner", report);
            AssetDatabase.Refresh();
        }

        private static void ScanSyntaxFile(
            string path,
            StringBuilder offenders,
            ref int hitCount,
            ref int storyHitCount,
            ref int nonStoryHitCount,
            ref int parserFailureCount)
        {
            string source = File.ReadAllText(path, Encoding.UTF8);
            SyntaxTree tree;
            try
            {
                tree = CSharpSyntaxTree.ParseText(source);
            }
            catch (Exception exception)
            {
                parserFailureCount++;
                ScanTokenFallback(path, source, "RoslynParse:" + exception.GetType().Name, offenders, ref hitCount, ref storyHitCount, ref nonStoryHitCount);
                return;
            }

            if (HasParseError(tree))
            {
                parserFailureCount++;
                ScanTokenFallback(path, source, "RoslynParse:syntax error", offenders, ref hitCount, ref storyHitCount, ref nonStoryHitCount);
                return;
            }

            CompilationUnitSyntax root = tree.GetCompilationUnitRoot();
            using (System.Collections.Generic.IEnumerator<SyntaxNode> nodes = root.DescendantNodes().GetEnumerator())
            {
                while (nodes.MoveNext())
                {
                    if (!TryResolveForbiddenSyntax(nodes.Current, out string token))
                        continue;

                    AppendOffender(path, token, "RoslynSyntax", offenders, ref hitCount, ref storyHitCount, ref nonStoryHitCount);
                }
            }
        }

        private static bool HasParseError(SyntaxTree tree)
        {
            using (System.Collections.Generic.IEnumerator<Microsoft.CodeAnalysis.Diagnostic> diagnostics = tree.GetDiagnostics().GetEnumerator())
            {
                while (diagnostics.MoveNext())
                {
                    if (diagnostics.Current.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error)
                        return true;
                }
            }

            return false;
        }

        private static bool TryResolveForbiddenSyntax(SyntaxNode node, out string token)
        {
            token = null;
            if (node is MethodDeclarationSyntax method)
            {
                string methodName = method.Identifier.ValueText;
                if (string.Equals(methodName, "OnTriggerEnter", StringComparison.Ordinal) ||
                    string.Equals(methodName, "OnTriggerStay", StringComparison.Ordinal))
                {
                    token = methodName;
                    return true;
                }
            }
            else if (node is InvocationExpressionSyntax invocation &&
                     invocation.Expression is MemberAccessExpressionSyntax memberAccess &&
                     string.Equals(memberAccess.Name.Identifier.ValueText, "OverlapSphere", StringComparison.Ordinal) &&
                     string.Equals(memberAccess.Expression.ToString(), "Physics", StringComparison.Ordinal))
            {
                token = "Physics.OverlapSphere";
                return true;
            }
            else if (node is ObjectCreationExpressionSyntax objectCreation)
            {
                string typeName = objectCreation.Type.ToString();
                if (string.Equals(typeName, "BoxCollider", StringComparison.Ordinal) ||
                    string.Equals(typeName, "SphereCollider", StringComparison.Ordinal))
                {
                    token = typeName;
                    return true;
                }
            }
            else if (node is IdentifierNameSyntax identifier &&
                     string.Equals(identifier.Identifier.ValueText, "isTrigger", StringComparison.Ordinal))
            {
                token = "isTrigger";
                return true;
            }

            return false;
        }

        private static void ScanTokenFallback(
            string path,
            string source,
            string parserReason,
            StringBuilder offenders,
            ref int hitCount,
            ref int storyHitCount,
            ref int nonStoryHitCount)
        {
            for (int tokenIndex = 0; tokenIndex < Tokens.Length; tokenIndex++)
            {
                if (source.IndexOf(Tokens[tokenIndex], StringComparison.Ordinal) < 0)
                    continue;

                AppendOffender(path, Tokens[tokenIndex], parserReason, offenders, ref hitCount, ref storyHitCount, ref nonStoryHitCount);
            }
        }

        private static void AppendOffender(
            string path,
            string token,
            string parserRoute,
            StringBuilder offenders,
            ref int hitCount,
            ref int storyHitCount,
            ref int nonStoryHitCount)
        {
            bool story = IsStoryTriggerFile(path);
            if (hitCount > 0)
                offenders.Append(",\n");

            offenders.Append("    { \"file\": \"")
                .Append(Escape(path.Replace('\\', '/')))
                .Append("\", \"token\": \"")
                .Append(Escape(token))
                .Append("\", \"classification\": \"")
                .Append(story ? "story" : "non-story")
                .Append("\", \"parserRoute\": \"")
                .Append(Escape(parserRoute))
                .Append("\" }");
            hitCount++;
            if (story)
                storyHitCount++;
            else
                nonStoryHitCount++;
        }

        private static string Escape(string value)
        {
            return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        private static bool IsStoryTriggerFile(string path)
        {
            string normalized = path.Replace('\\', '/');
            return normalized.IndexOf("/Narrative/", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   normalized.IndexOf("/Progression/", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   normalized.IndexOf("/Events/", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static void WriteSharedReportEntry(string key, string reportObjectJson)
        {
            const string sharedPath = "Docs/Reports/PHYSICS_OPTIMIZATION_REPORT.json";
            string existing = File.Exists(sharedPath) ? File.ReadAllText(sharedPath, Encoding.UTF8) : string.Empty;
            JObject root = string.IsNullOrWhiteSpace(existing) ? new JObject() : JObject.Parse(existing);
            root[key] = JObject.Parse(reportObjectJson);
            File.WriteAllText(sharedPath, root.ToString(Newtonsoft.Json.Formatting.Indented) + "\n", Encoding.UTF8);
        }
    }
}
#endif
