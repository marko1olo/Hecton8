#if UNITY_EDITOR
using System;
using System.IO;
using System.Text;
using Hecton8.Construction;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Hecton8.Construction.Editor
{
    public sealed class BulkheadContainmentTunerWindow : EditorWindow
    {
        private const string RuntimeInactiveText = "Runtime inactive.";

        private readonly StringBuilder _statusBuilder = new StringBuilder(256);
        private Label _statusLabel;
        private Slider _closeSpeed;
        private Slider _openSpeed;
        private Slider _overrideDistance;
        private Slider _catastrophicIntegrity;
        private bool _lastRuntimeActive;
        private int _lastActiveCount = int.MinValue;
        private float _lastQuality = float.NaN;
        private float _lastCadenceHz = float.NaN;
        private float _lastScheduleUs = float.NaN;
        private uint _lastTelemetryFrame = uint.MaxValue;
        private float _lastAverageClosure = float.NaN;
        private uint _lastCollisionEdgeHash = uint.MaxValue;
        private float _lastCollisionDepthMeters = float.NaN;
        private int _lastShaderUploadCount = int.MinValue;

        [MenuItem("HECTON-8/Construction/SHINOBU 220 Bulkhead Tuner")]
        public static void Open()
        {
            GetWindow<BulkheadContainmentTunerWindow>("SHINOBU 220 Bulkheads");
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

            _closeSpeed = MakeSlider("Close speed", 0.05f, 8f, 2.4f);
            _openSpeed = MakeSlider("Open speed", 0.05f, 8f, 3f);
            _overrideDistance = MakeSlider("Override distance", 0.5f, 8f, 3.2f);
            _catastrophicIntegrity = MakeSlider("Catastrophic integrity", 0.01f, 0.99f, 0.18f);
            rootVisualElement.Add(_closeSpeed);
            rootVisualElement.Add(_openSpeed);
            rootVisualElement.Add(_overrideDistance);
            rootVisualElement.Add(_catastrophicIntegrity);

            Button applyButton = new Button(ApplyTuning) { text = "Apply Runtime Tuning" };
            rootVisualElement.Add(applyButton);

            Button csvButton = new Button(LoadCsvProfiles) { text = "Load bulkhead_profiles.csv" };
            rootVisualElement.Add(csvButton);

            Button inquisitionButton = new Button(DoorPhysicsInquisition.Run) { text = "Run Door Physics Inquisition" };
            rootVisualElement.Add(inquisitionButton);
        }

        private void OnInspectorUpdate()
        {
            if (_statusLabel == null)
                return;

            if (BulkheadContainmentRuntime.TryReadEditorState(
                    out int activeCount,
                    out float quality,
                    out float cadenceHz,
                    out float lastScheduleUs,
                    out uint telemetryFrame,
                    out float averageClosure,
                    out uint collisionEdgeHash,
                    out float collisionDepthMeters,
                    out int shaderUploadCount))
            {
                if (_lastRuntimeActive &&
                    _lastActiveCount == activeCount &&
                    NearlyEqual(_lastQuality, quality, 0.0005f) &&
                    NearlyEqual(_lastCadenceHz, cadenceHz, 0.005f) &&
                    NearlyEqual(_lastScheduleUs, lastScheduleUs, 0.005f) &&
                    _lastTelemetryFrame == telemetryFrame &&
                    NearlyEqual(_lastAverageClosure, averageClosure, 0.0005f) &&
                    _lastCollisionEdgeHash == collisionEdgeHash &&
                    NearlyEqual(_lastCollisionDepthMeters, collisionDepthMeters, 0.0005f) &&
                    _lastShaderUploadCount == shaderUploadCount)
                {
                    return;
                }

                _statusBuilder.Clear();
                _statusBuilder.Append("Active: ").Append(activeCount)
                    .Append(" | Closure: ").Append(averageClosure.ToString("0.00"))
                    .Append(" | Quality: ").Append(quality.ToString("0.00"))
                    .Append(" | Cadence: ").Append(cadenceHz.ToString("0.0")).Append(" Hz")
                    .Append(" | Schedule: ").Append(lastScheduleUs.ToString("0.00")).Append(" us")
                    .Append(" | Hit: ").Append(collisionEdgeHash).Append('/').Append(collisionDepthMeters.ToString("0.000"))
                    .Append(" | Upload: ").Append(shaderUploadCount)
                    .Append(" | Frame: ").Append(telemetryFrame);
                _statusLabel.text = _statusBuilder.ToString();
                _lastRuntimeActive = true;
                _lastActiveCount = activeCount;
                _lastQuality = quality;
                _lastCadenceHz = cadenceHz;
                _lastScheduleUs = lastScheduleUs;
                _lastTelemetryFrame = telemetryFrame;
                _lastAverageClosure = averageClosure;
                _lastCollisionEdgeHash = collisionEdgeHash;
                _lastCollisionDepthMeters = collisionDepthMeters;
                _lastShaderUploadCount = shaderUploadCount;
            }
            else
            {
                if (!_lastRuntimeActive && string.Equals(_statusLabel.text, RuntimeInactiveText, StringComparison.Ordinal))
                    return;

                _statusLabel.text = RuntimeInactiveText;
                _lastRuntimeActive = false;
            }
        }

        private static bool NearlyEqual(float left, float right, float epsilon)
        {
            return float.IsNaN(left) && float.IsNaN(right) ||
                   Math.Abs(left - right) <= epsilon;
        }

        private static Slider MakeSlider(string label, float min, float max, float value)
        {
            Slider slider = new Slider(label, min, max) { value = value };
            slider.showInputField = true;
            return slider;
        }

        private void ApplyTuning()
        {
            BulkheadContainmentRuntime.TryApplyEditorTuning(
                _closeSpeed.value,
                _openSpeed.value,
                _overrideDistance.value,
                _catastrophicIntegrity.value);
        }

        private void LoadCsvProfiles()
        {
            string path = EditorUtility.OpenFilePanel("bulkhead_profiles.csv", Application.dataPath, "csv");
            if (string.IsNullOrEmpty(path))
                return;

            BulkheadContainmentRuntime.TryLoadProfilesFromCsvFile(path);
        }
    }

    public static class DoorPhysicsInquisition
    {
        private const string AggregateReportPath = "Docs/Reports/CONSTRUCTION_OPTIMIZATION_REPORT.json";
        private const string SidecarJsonPath = "Docs/Reports/CONSTRUCTION_OPTIMIZATION_REPORT_SHINOBU_220.json";
        private const string MarkdownReportPath = "Docs/Reports/Door_Physics_Inquisition_SHINOBU_220.md";
        private const string AggregateKey = "shinobu_220_bulkhead_dod";

        [MenuItem("HECTON-8/Construction/Door Physics Inquisition")]
        public static void Run()
        {
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, "_Project/Scripts"));
            int physicalDoorHits = 0;
            int colliderDoorHits = 0;
            int transformDoorMotionHits = 0;
            int ownedRuntimePhysicsHits = 0;
            StringBuilder filesJson = new StringBuilder(4096);
            StringBuilder filesMarkdown = new StringBuilder(4096);

            ScanDirectory(
                Path.Combine(root, "Construction"),
                ref physicalDoorHits,
                ref colliderDoorHits,
                ref transformDoorMotionHits,
                ref ownedRuntimePhysicsHits,
                filesJson,
                filesMarkdown);
            ScanDirectory(
                Path.Combine(root, "Gameplay"),
                ref physicalDoorHits,
                ref colliderDoorHits,
                ref transformDoorMotionHits,
                ref ownedRuntimePhysicsHits,
                filesJson,
                filesMarkdown);

            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string aggregateFullPath = Path.GetFullPath(Path.Combine(projectRoot, AggregateReportPath));
            string sidecarFullPath = Path.GetFullPath(Path.Combine(projectRoot, SidecarJsonPath));
            string markdownFullPath = Path.GetFullPath(Path.Combine(projectRoot, MarkdownReportPath));
            string generatedUtc = DateTime.UtcNow.ToString("O");
            string sidecarJson = BuildSidecarJson(
                generatedUtc,
                physicalDoorHits,
                colliderDoorHits,
                transformDoorMotionHits,
                ownedRuntimePhysicsHits,
                filesJson);
            string markdown = BuildMarkdownReport(
                generatedUtc,
                physicalDoorHits,
                colliderDoorHits,
                transformDoorMotionHits,
                ownedRuntimePhysicsHits,
                filesMarkdown);
            WriteTextAtomic(sidecarFullPath, sidecarJson);
            WriteTextAtomic(markdownFullPath, markdown);
            UpsertAggregateReport(aggregateFullPath, BuildAggregateJson(
                generatedUtc,
                physicalDoorHits,
                colliderDoorHits,
                transformDoorMotionHits,
                ownedRuntimePhysicsHits));
            AssetDatabase.Refresh();
            Debug.Log("SHINOBU_220 Door Physics Inquisition wrote " + sidecarFullPath);
        }

        private static void ScanDirectory(
            string directory,
            ref int physicalDoorHits,
            ref int colliderDoorHits,
            ref int transformDoorMotionHits,
            ref int ownedRuntimePhysicsHits,
            StringBuilder filesJson,
            StringBuilder filesMarkdown)
        {
            if (!Directory.Exists(directory))
                return;

            foreach (string file in Directory.EnumerateFiles(directory, "*.cs", SearchOption.AllDirectories))
            {
                string normalizedPath = file.Replace('\\', '/');
                if (normalizedPath.IndexOf("/Editor/", StringComparison.Ordinal) >= 0)
                    continue;
                string reportPath = ToProjectRelativePath(normalizedPath);

                if (!ScanFileForDoorPhysics(
                        file,
                        out bool door,
                        out bool collider,
                        out bool transformMotion,
                        out int evidenceLine,
                        out string evidenceSnippet) ||
                    !door)
                {
                    continue;
                }

                bool ownedRuntime =
                    normalizedPath.EndsWith("/BulkheadContainmentRuntime.cs", StringComparison.Ordinal) ||
                    normalizedPath.EndsWith("/BulkheadContainmentJobs.cs", StringComparison.Ordinal) ||
                    normalizedPath.EndsWith("/BulkheadContainmentContracts.cs", StringComparison.Ordinal) ||
                    normalizedPath.EndsWith("/BaseAirlock.cs", StringComparison.Ordinal);
                physicalDoorHits++;
                if (collider)
                    colliderDoorHits++;
                if (transformMotion)
                    transformDoorMotionHits++;
                if (ownedRuntime && (collider || transformMotion))
                    ownedRuntimePhysicsHits++;

                if (filesJson.Length > 0)
                    filesJson.AppendLine(",");
                filesJson.Append("    { \"path\": \"")
                    .Append(Escape(reportPath))
                    .Append("\", \"collider\": ")
                    .Append(collider ? "true" : "false")
                    .Append(", \"transformMotion\": ")
                    .Append(transformMotion ? "true" : "false")
                    .Append(", \"ownedRuntime\": ")
                    .Append(ownedRuntime ? "true" : "false")
                    .Append(", \"line\": ")
                    .Append(evidenceLine)
                    .Append(", \"snippet\": \"")
                    .Append(Escape(evidenceSnippet))
                    .Append("\"")
                    .Append(" }");

                filesMarkdown.Append("- `")
                    .Append(reportPath)
                    .Append("` collider=")
                    .Append(collider ? "true" : "false")
                    .Append(" transformMotion=")
                    .Append(transformMotion ? "true" : "false")
                    .Append(" ownedRuntime=")
                    .Append(ownedRuntime ? "true" : "false")
                    .Append(" line=")
                    .Append(evidenceLine)
                    .Append(" snippet=`")
                    .Append(evidenceSnippet)
                    .Append("`")
                    .AppendLine();
            }
        }

        private static bool ScanFileForDoorPhysics(
            string file,
            out bool door,
            out bool collider,
            out bool transformMotion,
            out int evidenceLine,
            out string evidenceSnippet)
        {
            door = false;
            collider = false;
            transformMotion = false;
            evidenceLine = 0;
            evidenceSnippet = string.Empty;

            string[] lines = File.ReadAllLines(file);
            for (int i = 0; i < lines.Length; i++)
            {
                string line = StripLineComment(lines[i]);
                bool doorLine =
                    ContainsToken(line, "Door") ||
                    ContainsToken(line, "door") ||
                    ContainsToken(line, "Bulkhead") ||
                    ContainsToken(line, "bulkhead");
                bool colliderLine =
                    ContainsToken(line, "BoxCollider") ||
                    ContainsToken(line, "MeshCollider") ||
                    (ContainsToken(line, "Collider") && line.IndexOf(".enabled", StringComparison.Ordinal) >= 0);
                bool transformLine =
                    ContainsTransformWrite(line, ".localPosition") ||
                    ContainsTransformWrite(line, ".position") ||
                    ContainsToken(line, "Animator");

                door |= doorLine;
                collider |= colliderLine;
                transformMotion |= transformLine;
                if (evidenceLine == 0 && (doorLine || colliderLine || transformLine))
                {
                    evidenceLine = i + 1;
                    evidenceSnippet = TrimSnippet(line);
                }
            }

            return true;
        }

        private static string StripLineComment(string line)
        {
            int comment = line.IndexOf("//", StringComparison.Ordinal);
            return comment >= 0 ? line.Substring(0, comment) : line;
        }

        private static bool ContainsToken(string value, string token)
        {
            int index = value.IndexOf(token, StringComparison.Ordinal);
            while (index >= 0)
            {
                int before = index - 1;
                int after = index + token.Length;
                bool beforeBoundary = before < 0 || IsTokenBoundary(value[before]);
                bool afterBoundary = after >= value.Length || IsTokenBoundary(value[after]);
                if (beforeBoundary && afterBoundary)
                    return true;

                index = value.IndexOf(token, index + token.Length, StringComparison.Ordinal);
            }

            return false;
        }

        private static bool IsTokenBoundary(char value)
        {
            return !char.IsLetterOrDigit(value) && value != '_';
        }

        private static bool ContainsTransformWrite(string value, string property)
        {
            int index = value.IndexOf(property, StringComparison.Ordinal);
            while (index >= 0)
            {
                int assignment = value.IndexOf('=', index + property.Length);
                if (assignment >= 0)
                {
                    int statementEnd = value.IndexOf(';', index + property.Length);
                    if (statementEnd >= 0 && statementEnd < assignment)
                    {
                        index = value.IndexOf(property, statementEnd + 1, StringComparison.Ordinal);
                        continue;
                    }

                    bool comparison =
                        (assignment + 1 < value.Length && value[assignment + 1] == '=') ||
                        (assignment > 0 && (value[assignment - 1] == '!' || value[assignment - 1] == '<' || value[assignment - 1] == '>'));
                    if (!comparison)
                        return true;

                    index = value.IndexOf(property, assignment + 1, StringComparison.Ordinal);
                    continue;
                }

                index = value.IndexOf(property, index + property.Length, StringComparison.Ordinal);
            }

            return false;
        }

        private static string TrimSnippet(string value)
        {
            string trimmed = value.Trim();
            return trimmed.Length <= 120 ? trimmed : trimmed.Substring(0, 120);
        }

        private static string BuildSidecarJson(
            string generatedUtc,
            int physicalDoorHits,
            int colliderDoorHits,
            int transformDoorMotionHits,
            int ownedRuntimePhysicsHits,
            StringBuilder filesJson)
        {
            StringBuilder json = new StringBuilder(8192);
            json.AppendLine("{");
            json.AppendLine("  \"agent\": \"SHINOBU_220\",");
            json.AppendLine("  \"domain\": \"ECHELON 6 HABITAT & VEHICLES / BASE CONTAINMENT\",");
            json.Append("  \"generated_utc\": \"").Append(Escape(generatedUtc)).AppendLine("\",");
            json.AppendLine("  \"scanner\": \"DoorPhysicsInquisition\",");
            json.Append("  \"physicalDoorMentions\": ").Append(physicalDoorHits).AppendLine(",");
            json.Append("  \"colliderDoorMentions\": ").Append(colliderDoorHits).AppendLine(",");
            json.Append("  \"transformDoorMotionMentions\": ").Append(transformDoorMotionHits).AppendLine(",");
            json.Append("  \"ownedRuntimePhysicsHits\": ").Append(ownedRuntimePhysicsHits).AppendLine(",");
            json.AppendLine("  \"mathematicalBulkheadRuntime\": \"BulkheadContainmentRuntime\",");
            json.AppendLine("  \"notes\": \"Emergency BaseAirlock bulkhead state is DataVault/CSR/KCC plane driven; visual closure is shader-side.\",");
            json.AppendLine("  \"files\": [");
            json.Append(filesJson);
            json.AppendLine();
            json.AppendLine("  ]");
            json.AppendLine("}");
            return json.ToString();
        }

        private static string BuildAggregateJson(
            string generatedUtc,
            int physicalDoorHits,
            int colliderDoorHits,
            int transformDoorMotionHits,
            int ownedRuntimePhysicsHits)
        {
            StringBuilder json = new StringBuilder(2048);
            json.AppendLine("{");
            json.AppendLine("  \"agent\": \"SHINOBU_220\",");
            json.AppendLine("  \"domain\": \"ECHELON 6 HABITAT & VEHICLES / BASE CONTAINMENT\",");
            json.Append("  \"generated_utc\": \"").Append(Escape(generatedUtc)).AppendLine("\",");
            json.AppendLine("  \"scanner\": \"DoorPhysicsInquisition\",");
            json.AppendLine("  \"sidecar_report\": \"Docs/Reports/CONSTRUCTION_OPTIMIZATION_REPORT_SHINOBU_220.json\",");
            json.AppendLine("  \"markdown_report\": \"Docs/Reports/Door_Physics_Inquisition_SHINOBU_220.md\",");
            json.Append("  \"physical_door_mentions\": ").Append(physicalDoorHits).AppendLine(",");
            json.Append("  \"collider_door_mentions\": ").Append(colliderDoorHits).AppendLine(",");
            json.Append("  \"transform_door_motion_mentions\": ").Append(transformDoorMotionHits).AppendLine(",");
            json.Append("  \"owned_runtime_physics_hits\": ").Append(ownedRuntimePhysicsHits).AppendLine(",");
            json.AppendLine("  \"owned_route\": \"BulkheadContainmentRuntime/BulkheadContainmentJobs/BulkheadContainmentContracts/BaseAirlock\",");
            json.AppendLine("  \"authority_route\": \"BaseAirlock publishes typed bulkhead intents; Construction owns Vault CSR/KCC plane state; shader owns visual door fake.\",");
            json.AppendLine("  \"verdict\": \"PASS when owned_runtime_physics_hits is 0; unrelated door files remain inventory only.\"");
            json.Append("}");
            return json.ToString();
        }

        private static string BuildMarkdownReport(
            string generatedUtc,
            int physicalDoorHits,
            int colliderDoorHits,
            int transformDoorMotionHits,
            int ownedRuntimePhysicsHits,
            StringBuilder filesMarkdown)
        {
            StringBuilder report = new StringBuilder(8192);
            report.AppendLine("# Door Physics Inquisition - SHINOBU_220");
            report.AppendLine();
            report.Append("- Generated UTC: ").AppendLine(generatedUtc);
            report.Append("- Domain: ").AppendLine("ECHELON 6 HABITAT & VEHICLES / BASE CONTAINMENT");
            report.Append("- Physical door mention files: ").Append(physicalDoorHits).AppendLine();
            report.Append("- Collider door mention files: ").Append(colliderDoorHits).AppendLine();
            report.Append("- Transform/Animator door mention files: ").Append(transformDoorMotionHits).AppendLine();
            report.Append("- Owned route physics hits: ").Append(ownedRuntimePhysicsHits).AppendLine();
            report.AppendLine();
            report.AppendLine("Verdict: SHINOBU-owned runtime route is compliant when owned route physics hits remain 0. Wider door mentions are inventory for neighboring legacy files, not authority for emergency bulkhead closure.");
            report.AppendLine();
            report.AppendLine("Cinematic cheat: `BaseAirlock` publishes a typed intent, `BulkheadContainmentRuntime` maintains CSR/KCC mathematical closure planes, and `Hecton8_UberNoir` deforms the visual panel in shader. No GameObject door body, collider door slab, or Animator state machine is required for the SHINOBU-owned emergency seal.");
            report.AppendLine();
            report.AppendLine("Files:");
            report.Append(filesMarkdown);
            return report.ToString();
        }

        private static string ToProjectRelativePath(string normalizedPath)
        {
            int assetsIndex = normalizedPath.IndexOf("/Assets/", StringComparison.Ordinal);
            return assetsIndex >= 0 ? normalizedPath.Substring(assetsIndex + 1) : normalizedPath;
        }

        private static void UpsertAggregateReport(string aggregateFullPath, string objectJson)
        {
            string directory = Path.GetDirectoryName(aggregateFullPath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            if (!File.Exists(aggregateFullPath))
            {
                StringBuilder created = new StringBuilder(objectJson.Length + 64);
                created.AppendLine("{");
                created.Append("    \"").Append(AggregateKey).Append("\":  ");
                created.Append(IndentContinuation(objectJson, 4));
                created.AppendLine();
                created.AppendLine("}");
                WriteTextAtomic(aggregateFullPath, created.ToString());
                return;
            }

            string json = File.ReadAllText(aggregateFullPath, Encoding.UTF8);
            if (TryFindRootPropertyValue(json, AggregateKey, out int valueStart, out int valueEnd))
            {
                string updated = json.Substring(0, valueStart) +
                    IndentContinuation(objectJson, ResolveLineIndent(json, valueStart)) +
                    json.Substring(valueEnd);
                WriteTextAtomic(aggregateFullPath, updated);
                return;
            }

            int insertAt = json.LastIndexOf('}');
            if (insertAt < 0)
                return;

            bool hasRootPayload = HasRootPayload(json, insertAt);
            StringBuilder builder = new StringBuilder(json.Length + objectJson.Length + 64);
            builder.Append(json, 0, insertAt);
            if (hasRootPayload)
                builder.AppendLine(",");
            else
                builder.AppendLine();
            builder.Append("    \"").Append(AggregateKey).Append("\":  ");
            builder.Append(IndentContinuation(objectJson, 4));
            builder.AppendLine();
            builder.Append(json, insertAt, json.Length - insertAt);
            WriteTextAtomic(aggregateFullPath, builder.ToString());
        }

        private static void WriteTextAtomic(string path, string text)
        {
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            string tempPath = path + ".tmp";
            File.WriteAllText(tempPath, text, Encoding.UTF8);
            if (File.Exists(path))
            {
                File.Replace(tempPath, path, null);
                return;
            }

            File.Move(tempPath, path);
        }

        private static bool TryFindRootPropertyValue(string json, string propertyName, out int valueStart, out int valueEnd)
        {
            valueStart = -1;
            valueEnd = -1;
            int objectDepth = 0;
            int arrayDepth = 0;
            bool inString = false;
            bool escape = false;
            for (int i = 0; i < json.Length; i++)
            {
                char c = json[i];
                if (inString)
                {
                    if (escape)
                    {
                        escape = false;
                    }
                    else if (c == '\\')
                    {
                        escape = true;
                    }
                    else if (c == '"')
                    {
                        inString = false;
                    }
                    continue;
                }

                if (c == '"')
                {
                    if (objectDepth == 1 && arrayDepth == 0 && IsQuotedToken(json, i, propertyName))
                    {
                        int tokenEnd = i + propertyName.Length + 2;
                        int colon = SkipWhitespace(json, tokenEnd);
                        if (colon < json.Length && json[colon] == ':')
                        {
                            valueStart = SkipWhitespace(json, colon + 1);
                            valueEnd = FindJsonValueEnd(json, valueStart);
                            return valueEnd > valueStart;
                        }
                    }

                    inString = true;
                    continue;
                }

                if (c == '{')
                    objectDepth++;
                else if (c == '}')
                    objectDepth--;
                else if (c == '[')
                    arrayDepth++;
                else if (c == ']')
                    arrayDepth--;
            }

            return false;
        }

        private static int FindJsonValueEnd(string json, int start)
        {
            bool inString = false;
            bool escape = false;
            int objectDepth = 0;
            int arrayDepth = 0;
            for (int i = start; i < json.Length; i++)
            {
                char c = json[i];
                if (inString)
                {
                    if (escape)
                        escape = false;
                    else if (c == '\\')
                        escape = true;
                    else if (c == '"')
                        inString = false;
                    continue;
                }

                if (c == '"')
                {
                    inString = true;
                    continue;
                }

                if (c == '{')
                    objectDepth++;
                else if (c == '}')
                {
                    if (objectDepth == 0 && arrayDepth == 0)
                        return i;
                    objectDepth--;
                    if (objectDepth < 0)
                        return i;
                }
                else if (c == '[')
                    arrayDepth++;
                else if (c == ']')
                    arrayDepth--;
                else if (c == ',' && objectDepth == 0 && arrayDepth == 0)
                    return i;
            }

            return json.Length;
        }

        private static bool IsQuotedToken(string json, int quoteIndex, string token)
        {
            if (quoteIndex + token.Length + 1 >= json.Length)
                return false;
            if (json[quoteIndex] != '"' || json[quoteIndex + token.Length + 1] != '"')
                return false;
            for (int i = 0; i < token.Length; i++)
            {
                if (json[quoteIndex + 1 + i] != token[i])
                    return false;
            }
            return true;
        }

        private static int SkipWhitespace(string value, int index)
        {
            while (index < value.Length && char.IsWhiteSpace(value[index]))
                index++;
            return index;
        }

        private static bool HasRootPayload(string json, int rootCloseIndex)
        {
            for (int i = rootCloseIndex - 1; i >= 0; i--)
            {
                char c = json[i];
                if (char.IsWhiteSpace(c))
                    continue;
                return c != '{';
            }
            return false;
        }

        private static int ResolveLineIndent(string json, int index)
        {
            int lineStart = json.LastIndexOf('\n', Math.Max(0, index - 1));
            if (lineStart < 0)
                lineStart = 0;
            else
                lineStart++;
            int indent = 0;
            while (lineStart + indent < json.Length && json[lineStart + indent] == ' ')
                indent++;
            return indent;
        }

        private static string IndentContinuation(string value, int spaces)
        {
            string indent = new string(' ', spaces);
            return value.Replace("\r\n", "\n").Replace("\n", "\n" + indent);
        }

        private static string Escape(string value)
        {
            return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }
    }
}
#endif
