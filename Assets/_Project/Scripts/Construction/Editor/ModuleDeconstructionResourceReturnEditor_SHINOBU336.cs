#if UNITY_EDITOR
using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Hecton8.Core.Memory;
using Unity.Collections;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Hecton8.Construction.Editor
{
    public sealed class ModuleDeconstructionResourceReturnWindowSHINOBU336 : EditorWindow
    {
        private const string RuntimeInactiveText = "Runtime inactive.";
        private Label _status;
        private Label _layout;
        private Label _csv;
        private Toggle _drawGizmo;
        private Slider _qualityPreview;

        [MenuItem("HECTON-8/Construction/SHINOBU 336 Deconstruction")]
        public static void Open()
        {
            GetWindow<ModuleDeconstructionResourceReturnWindowSHINOBU336>("SHINOBU 336 Deconstruction");
        }

        private void OnEnable()
        {
            SceneView.duringSceneGui -= DrawSceneGizmo;
            SceneView.duringSceneGui += DrawSceneGizmo;
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= DrawSceneGizmo;
            EditorApplication.update -= Tick;
        }

        public void CreateGUI()
        {
            rootVisualElement.Clear();
            rootVisualElement.style.paddingLeft = 10;
            rootVisualElement.style.paddingRight = 10;
            rootVisualElement.style.paddingTop = 10;
            rootVisualElement.style.paddingBottom = 10;

            _status = new Label(RuntimeInactiveText);
            _layout = new Label(HabitatDeconstructionTransactionKernel.RuntimeLayoutValid()
                ? "Layout: DeconstructionTransactionDTO=32 RefundCommandDTO=32 LootCacheDTO=64 Telemetry=64"
                : "Layout: INVALID");
            _csv = new Label("CSV profiles: not loaded");
            _drawGizmo = new Toggle("Draw severed graph gizmo") { value = true };
            _qualityPreview = new Slider("GlobalQualityWeight preview", 0f, 1f)
            {
                value = 0.5f,
                showInputField = true
            };

            Button loadCsv = new Button(LoadCsvProfiles) { text = "Load module_deconstruction_refund_profiles.csv" };
            Button scan = new Button(RunScanner) { text = "Run Module Deconstruction Scanner" };

            rootVisualElement.Add(_status);
            rootVisualElement.Add(_layout);
            rootVisualElement.Add(_qualityPreview);
            rootVisualElement.Add(_drawGizmo);
            rootVisualElement.Add(loadCsv);
            rootVisualElement.Add(scan);
            rootVisualElement.Add(_csv);
        }

        private void Tick()
        {
            if (_status == null)
                return;

            if (!ConstructionManager.TryReadShinobu336EditorState(
                    out int refunded,
                    out int overflow,
                    out int severed,
                    out int node,
                    out Vector3 targetPosition,
                    out float burstUs,
                    out uint stateHash,
                    out uint faultFlags))
            {
                _status.text = RuntimeInactiveText;
                return;
            }

            _status.text = "Refund " + refunded +
                           " | Overflow " + overflow +
                           " | CSR severed " + severed +
                           " | Node " + node +
                           " | Burst " + burstUs.ToString("0.000", CultureInfo.InvariantCulture) + " us" +
                           " | State 0x" + stateHash.ToString("X8") +
                           " | Fault 0x" + faultFlags.ToString("X8") +
                           " | Pos " + targetPosition.ToString("F2");
        }

        private void LoadCsvProfiles()
        {
            string defaultPath = Path.Combine(
                Application.dataPath,
                "_Project/Data/Construction/module_deconstruction_refund_profiles.csv");
            string path = EditorUtility.OpenFilePanel(
                "module_deconstruction_refund_profiles.csv",
                Path.GetDirectoryName(defaultPath),
                "csv");
            if (string.IsNullOrEmpty(path))
                path = defaultPath;

            bool loaded = Shinobu336RefundProfileCsvIngestor.TryLoad(path, out int rows);
            _csv.text = loaded
                ? "CSV profiles: " + rows + " rows loaded into BufferID.Shinobu336RefundProfiles"
                : "CSV profiles: rejected";
        }

        private void RunScanner()
        {
            string report = OOP_Module_Deconstruction_Scanner_SHINOBU336.RunAndWriteReport();
            _csv.text = "Scanner wrote " + report;
            Hecton8.Core.H8Debug.Log("SHINOBU_336 scanner wrote " + report);
        }

        private void DrawSceneGizmo(SceneView sceneView)
        {
            if (_drawGizmo == null || !_drawGizmo.value)
                return;

            if (!ConstructionManager.TryReadShinobu336EditorState(
                    out int refunded,
                    out int overflow,
                    out int severed,
                    out int node,
                    out Vector3 targetPosition,
                    out float burstUs,
                    out uint stateHash,
                    out uint faultFlags))
            {
                return;
            }

            if (!float.IsFinite(targetPosition.x) ||
                !float.IsFinite(targetPosition.y) ||
                !float.IsFinite(targetPosition.z))
            {
                return;
            }

            float q = Mathf.Clamp01(_qualityPreview != null ? _qualityPreview.value : 0.5f);
            float radius = Mathf.Lerp(0.45f, 1.25f, q);
            Handles.color = faultFlags == 0u
                ? new Color(0.1f, 0.85f, 0.72f, 0.9f)
                : new Color(1f, 0.35f, 0.2f, 0.9f);
            Handles.DrawWireDisc(targetPosition, Vector3.up, radius);
            Handles.DrawWireDisc(targetPosition, Vector3.right, radius * 0.55f);
            Handles.Label(
                targetPosition + Vector3.up * (radius + 0.25f),
                "S336 node " + node +
                " edges " + severed +
                " refund " + refunded +
                " overflow " + overflow +
                " us " + burstUs.ToString("0.00", CultureInfo.InvariantCulture) +
                " hash 0x" + stateHash.ToString("X8"));
        }
    }

    internal static class Shinobu336RefundProfileCsvIngestor
    {
        public static bool TryLoad(string path, out int rowsLoaded)
        {
            rowsLoaded = 0;
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                return false;

            if (!GlobalDataVault.TryGetLatestCreated(out GlobalDataVault vault) || vault == null)
                return false;

            VaultGenerationHandle<RefundProfileDTO> handle = vault.EnsureGenerationHandle<RefundProfileDTO>(
                BufferID.Shinobu336RefundProfiles,
                HabitatDeconstructionTransactionKernel.RefundProfileCapacity,
                SystemID.Construction,
                NativeArrayOptions.ClearMemory);

            if (handle.Generation == 0u ||
                !vault.TryAcquireWriteLock(in handle, SystemID.Construction, out NativeArray<RefundProfileDTO> profiles) ||
                !profiles.IsCreated)
            {
                return false;
            }

            try
            {
                for (int i = 0; i < profiles.Length; i++)
                    profiles[i] = default;

                string[] lines = File.ReadAllLines(path);
                for (int i = 0; i < lines.Length && rowsLoaded < profiles.Length; i++)
                {
                    if (!TryParseLine(lines[i], out RefundProfileDTO profile))
                        continue;

                    profiles[rowsLoaded++] = profile;
                }

                return rowsLoaded > 0;
            }
            finally
            {
                vault.ReleaseWriteLock(in handle, SystemID.Construction);
            }
        }

        private static bool TryParseLine(string line, out RefundProfileDTO profile)
        {
            profile = default;
            if (string.IsNullOrWhiteSpace(line))
                return false;

            string trimmed = line.Trim();
            if (trimmed.StartsWith("#", StringComparison.Ordinal) ||
                trimmed.StartsWith("profile", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            string[] tokens = trimmed.Split(',');
            if (tokens.Length < 5)
                return false;

            if (!TryParseProfileHash(tokens[0], out uint profileHash) ||
                !TryParseFloat(tokens[1], out float refundScalar) ||
                !TryParseFloat(tokens[2], out float offsetMeters) ||
                !TryParseFloat(tokens[3], out float qualityWeight) ||
                !TryParseUInt(tokens[4], out uint flags))
            {
                return false;
            }

            profile.ProfileHash = profileHash;
            profile.RefundScalar01 = Mathf.Clamp01(refundScalar);
            profile.OverflowOffsetMeters = Mathf.Max(0f, offsetMeters);
            profile.GlobalQualityWeight = Mathf.Clamp01(qualityWeight);
            profile.Flags = flags;
            profile.RowHash = HashRow(trimmed);
            return profile.ProfileHash != 0u;
        }

        private static bool TryParseProfileHash(string token, out uint value)
        {
            if (TryParseUInt(token, out value))
                return true;

            value = HashRow(token != null ? token.Trim() : string.Empty);
            return value != 0u;
        }

        private static bool TryParseUInt(string token, out uint value)
        {
            value = 0u;
            if (string.IsNullOrWhiteSpace(token))
                return false;

            string trimmed = token.Trim();
            if (trimmed.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                return uint.TryParse(trimmed.Substring(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value);

            return uint.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
        }

        private static bool TryParseFloat(string token, out float value)
        {
            value = 0f;
            return !string.IsNullOrWhiteSpace(token) &&
                   float.TryParse(token.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out value) &&
                   float.IsFinite(value);
        }

        private static uint HashRow(string value)
        {
            unchecked
            {
                uint hash = 2166136261u;
                if (string.IsNullOrEmpty(value))
                    return hash;

                for (int i = 0; i < value.Length; i++)
                    hash = (hash ^ value[i]) * 16777619u;
                return hash;
            }
        }
    }

    internal static class OOP_Module_Deconstruction_Scanner_SHINOBU336
    {
        private const string SharedReportPath = "Docs/Reports/CONSTRUCTION_OPTIMIZATION_REPORT.json";
        private const string SidecarReportPath = "Docs/Reports/CONSTRUCTION_OPTIMIZATION_REPORT_SHINOBU_336.json";
        private const string SectionName = "shinobu_336_module_deconstruction_resource_return";

        private static readonly string[] ScanFiles =
        {
            "Assets/_Project/Scripts/ConstructionManager.cs",
            "Assets/_Project/Scripts/Construction/HabitatDeconstructionTransactionKernel.cs",
            "Assets/_Project/Scripts/Construction/HabitatGraphManager.cs",
            "Assets/_Project/Scripts/Construction/BaseModuleCatalogRuntime.cs"
        };

        private static readonly Regex DestroyCall = new Regex(@"\bDestroy\s*\(", RegexOptions.Compiled);

        [MenuItem("HECTON-8/Construction/Run SHINOBU 336 Scanner")]
        private static void RunMenu()
        {
            Hecton8.Core.H8Debug.Log("SHINOBU_336 scanner wrote " + RunAndWriteReport());
        }

        public static string RunAndWriteReport()
        {
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            int filesScanned = 0;
            int destroyHits = 0;
            int oldRetireHelperHits = 0;
            int inventoryPreflightHits = 0;
            int managedRecipeFallbackHits = 0;
            int kernelHits = 0;
            int dtoLayoutHits = 0;
            int csrLaneHits = 0;
            int typedLootSignalHits = 0;
            int legacyLootPublishHits = 0;
            int blackBoxHits = 0;
            StringBuilder findings = new StringBuilder(4096);

            for (int i = 0; i < ScanFiles.Length; i++)
            {
                string path = Path.Combine(root, ScanFiles[i].Replace('/', Path.DirectorySeparatorChar));
                if (!File.Exists(path))
                    continue;

                filesScanned++;
                string source = StripCommentsAndStrings(File.ReadAllText(path));
                string relative = ScanFiles[i];
                AppendDestroyFindings(relative, source, findings, ref destroyHits);
                oldRetireHelperHits += Count(source, "DespawnOrDestroyModuleInstance");
                inventoryPreflightHits += Count(source, "CanAcceptItemQuantityBatch");
                managedRecipeFallbackHits += Count(source, "buildData.buildCost");
                kernelHits += Count(source, "ExecuteModuleTeardownJob");
                dtoLayoutHits += Count(source, "StructLayout(LayoutKind.Explicit, Size = 32)");
                csrLaneHits += Count(source, "TryGetDeconstructionCsrLanes");
                typedLootSignalHits += Count(source, "SignalBus<InventoryDeathLootCacheSignal>.TryPush");
                legacyLootPublishHits += Count(source, "GlobalSignals.Publish(new InventoryDeathLootCacheSignal");
                blackBoxHits += Count(source, "Dump_1306_Construction_DeconstructionTelemetry.bin");
            }

            bool pass = destroyHits == 0 &&
                        oldRetireHelperHits == 0 &&
                        legacyLootPublishHits == 0 &&
                        kernelHits > 0 &&
                        dtoLayoutHits > 0 &&
                        csrLaneHits > 0 &&
                        typedLootSignalHits > 0 &&
                        blackBoxHits > 0;

            string sidecarJson = BuildSidecarJson(
                filesScanned,
                destroyHits,
                oldRetireHelperHits,
                inventoryPreflightHits,
                managedRecipeFallbackHits,
                kernelHits,
                dtoLayoutHits,
                csrLaneHits,
                typedLootSignalHits,
                legacyLootPublishHits,
                blackBoxHits,
                pass,
                findings);

            string sidecar = Path.Combine(root, SidecarReportPath);
            WriteTextAtomic(sidecar, sidecarJson);
            UpsertSharedSection(
                Path.Combine(root, SharedReportPath),
                SectionName,
                BuildSharedSectionJson(
                    filesScanned,
                    destroyHits,
                    oldRetireHelperHits,
                    inventoryPreflightHits,
                    managedRecipeFallbackHits,
                    kernelHits,
                    dtoLayoutHits,
                    csrLaneHits,
                    typedLootSignalHits,
                    legacyLootPublishHits,
                    blackBoxHits,
                    pass));
            AssetDatabase.Refresh();
            return SidecarReportPath;
        }

        private static void AppendDestroyFindings(string file, string source, StringBuilder findings, ref int destroyHits)
        {
            MatchCollection matches = DestroyCall.Matches(source);
            for (int i = 0; i < matches.Count; i++)
            {
                Match match = matches[i];
                destroyHits++;
                if (findings.Length > 0)
                    findings.AppendLine(",");

                findings.Append("    { \"file\": \"")
                    .Append(Escape(file))
                    .Append("\", \"line\": ")
                    .Append(CountLine(source, match.Index))
                    .Append(", \"kind\": \"UNITY_DESTROY_CALL\", \"snippet\": \"")
                    .Append(Escape(ExtractSnippet(source, match.Index)))
                    .Append("\" }");
            }
        }

        private static string BuildSidecarJson(
            int filesScanned,
            int destroyHits,
            int oldRetireHelperHits,
            int inventoryPreflightHits,
            int managedRecipeFallbackHits,
            int kernelHits,
            int dtoLayoutHits,
            int csrLaneHits,
            int typedLootSignalHits,
            int legacyLootPublishHits,
            int blackBoxHits,
            bool pass,
            StringBuilder findings)
        {
            StringBuilder json = new StringBuilder(8192);
            json.AppendLine("{");
            json.AppendLine("  \"agent\": \"SHINOBU_336\",");
            json.AppendLine("  \"domain\": \"MODULE_DECONSTRUCTION_RESOURCE_RETURN\",");
            json.AppendLine("  \"scanner\": \"OOP_Module_Deconstruction_Scanner_SHINOBU336\",");
            json.AppendLine("  \"generated_utc\": \"" + Escape(DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture)) + "\",");
            json.AppendLine("  \"filesScanned\": " + filesScanned + ",");
            json.AppendLine("  \"destroyCallHits\": " + destroyHits + ",");
            json.AppendLine("  \"oldRetireHelperHits\": " + oldRetireHelperHits + ",");
            json.AppendLine("  \"inventoryPreflightHits\": " + inventoryPreflightHits + ",");
            json.AppendLine("  \"managedRecipeFallbackHits\": " + managedRecipeFallbackHits + ",");
            json.AppendLine("  \"kernelHits\": " + kernelHits + ",");
            json.AppendLine("  \"explicitDtoLayoutHits\": " + dtoLayoutHits + ",");
            json.AppendLine("  \"csrLaneHits\": " + csrLaneHits + ",");
            json.AppendLine("  \"typedLootSignalHits\": " + typedLootSignalHits + ",");
            json.AppendLine("  \"legacyLootPublishHits\": " + legacyLootPublishHits + ",");
            json.AppendLine("  \"blackBoxHits\": " + blackBoxHits + ",");
            json.AppendLine("  \"legacyFallbackBoundary\": \"buildData.buildCost is retained only when DataMonolith module-cost lanes are missing; the transaction kernel consumes ModuleCostDTO either way.\",");
            json.AppendLine("  \"verdict\": \"" + (pass ? "PASS_STATIC_ROUTE" : "FAIL_STATIC_ROUTE") + "\",");
            json.AppendLine("  \"findings\": [");
            json.Append(findings);
            if (findings.Length > 0)
                json.AppendLine();
            json.AppendLine("  ]");
            json.AppendLine("}");
            return json.ToString();
        }

        private static string BuildSharedSectionJson(
            int filesScanned,
            int destroyHits,
            int oldRetireHelperHits,
            int inventoryPreflightHits,
            int managedRecipeFallbackHits,
            int kernelHits,
            int dtoLayoutHits,
            int csrLaneHits,
            int typedLootSignalHits,
            int legacyLootPublishHits,
            int blackBoxHits,
            bool pass)
        {
            StringBuilder json = new StringBuilder(3072);
            json.AppendLine("  \"" + SectionName + "\": {");
            json.AppendLine("    \"agent\": \"SHINOBU_336\",");
            json.AppendLine("    \"domain\": \"MODULE_DECONSTRUCTION_RESOURCE_RETURN\",");
            json.AppendLine("    \"filesScanned\": " + filesScanned + ",");
            json.AppendLine("    \"destroyCallHits\": " + destroyHits + ",");
            json.AppendLine("    \"oldRetireHelperHits\": " + oldRetireHelperHits + ",");
            json.AppendLine("    \"inventoryPreflightHits\": " + inventoryPreflightHits + ",");
            json.AppendLine("    \"managedRecipeFallbackHits\": " + managedRecipeFallbackHits + ",");
            json.AppendLine("    \"kernelHits\": " + kernelHits + ",");
            json.AppendLine("    \"explicitDtoLayoutHits\": " + dtoLayoutHits + ",");
            json.AppendLine("    \"csrLaneHits\": " + csrLaneHits + ",");
            json.AppendLine("    \"typedLootSignalHits\": " + typedLootSignalHits + ",");
            json.AppendLine("    \"legacyLootPublishHits\": " + legacyLootPublishHits + ",");
            json.AppendLine("    \"blackBoxHits\": " + blackBoxHits + ",");
            json.AppendLine("    \"authorityRoute\": \"ConstructionManager -> ExecuteModuleTeardownJob -> RefundCommandDTO/LootCacheDTO -> PlayerInventory authority or typed loot-cache signal\",");
            json.AppendLine("    \"csrRoute\": \"HabitatGraphManager exposes native CSR lanes for zero-conductance severing and then marks matching edge records ruptured without GameObject destruction.\",");
            json.AppendLine("    \"verdict\": \"" + (pass ? "PASS_STATIC_ROUTE" : "FAIL_STATIC_ROUTE") + "\"");
            json.Append("  }");
            return json.ToString();
        }

        private static string StripCommentsAndStrings(string source)
        {
            StringBuilder output = new StringBuilder(source.Length);
            bool lineComment = false;
            bool blockComment = false;
            bool stringLiteral = false;
            bool charLiteral = false;
            bool verbatimString = false;

            for (int i = 0; i < source.Length; i++)
            {
                char c = source[i];
                char n = i + 1 < source.Length ? source[i + 1] : '\0';

                if (lineComment)
                {
                    if (c == '\n')
                    {
                        lineComment = false;
                        output.Append(c);
                    }
                    else
                    {
                        output.Append(' ');
                    }

                    continue;
                }

                if (blockComment)
                {
                    if (c == '*' && n == '/')
                    {
                        blockComment = false;
                        output.Append("  ");
                        i++;
                    }
                    else
                    {
                        output.Append(c == '\n' ? '\n' : ' ');
                    }

                    continue;
                }

                if (stringLiteral)
                {
                    if (verbatimString && c == '"' && n == '"')
                    {
                        output.Append("  ");
                        i++;
                        continue;
                    }

                    bool end = (!verbatimString && c == '"' && (i == 0 || source[i - 1] != '\\')) ||
                               (verbatimString && c == '"');
                    output.Append(c == '\n' ? '\n' : ' ');
                    if (end)
                    {
                        stringLiteral = false;
                        verbatimString = false;
                    }

                    continue;
                }

                if (charLiteral)
                {
                    bool end = c == '\'' && (i == 0 || source[i - 1] != '\\');
                    output.Append(c == '\n' ? '\n' : ' ');
                    if (end)
                        charLiteral = false;
                    continue;
                }

                if (c == '/' && n == '/')
                {
                    lineComment = true;
                    output.Append("  ");
                    i++;
                    continue;
                }

                if (c == '/' && n == '*')
                {
                    blockComment = true;
                    output.Append("  ");
                    i++;
                    continue;
                }

                if (c == '@' && n == '"')
                {
                    stringLiteral = true;
                    verbatimString = true;
                    output.Append("  ");
                    i++;
                    continue;
                }

                if (c == '"')
                {
                    stringLiteral = true;
                    output.Append(' ');
                    continue;
                }

                if (c == '\'')
                {
                    charLiteral = true;
                    output.Append(' ');
                    continue;
                }

                output.Append(c);
            }

            return output.ToString();
        }

        private static int Count(string source, string token)
        {
            int count = 0;
            int index = 0;
            while ((index = source.IndexOf(token, index, StringComparison.Ordinal)) >= 0)
            {
                count++;
                index += token.Length;
            }

            return count;
        }

        private static int CountLine(string source, int index)
        {
            int line = 1;
            for (int i = 0; i < index && i < source.Length; i++)
            {
                if (source[i] == '\n')
                    line++;
            }

            return line;
        }

        private static string ExtractSnippet(string source, int index)
        {
            int start = Math.Max(0, index - 64);
            int length = Math.Min(160, source.Length - start);
            return source.Substring(start, length).Replace('\r', ' ').Replace('\n', ' ').Trim();
        }

        private static string Escape(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        private static void WriteTextAtomic(string path, string text)
        {
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            string temp = path + ".tmp";
            File.WriteAllText(temp, text, Encoding.UTF8);
            if (File.Exists(path))
            {
                File.Replace(temp, path, null);
                return;
            }

            File.Move(temp, path);
        }

        private static void UpsertSharedSection(string path, string sectionName, string sectionJson)
        {
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            if (!File.Exists(path))
            {
                WriteTextAtomic(path, "{\n" + sectionJson + "\n}\n");
                return;
            }

            string existing = RemoveExistingTopLevelSection(File.ReadAllText(path, Encoding.UTF8), sectionName).TrimEnd();
            int close = existing.LastIndexOf('}');
            if (close < 0)
            {
                WriteTextAtomic(path, "{\n" + sectionJson + "\n}\n");
                return;
            }

            string body = existing.Substring(0, close).TrimEnd();
            string separator = body.EndsWith("{", StringComparison.Ordinal) ? "\n" : ",\n";
            WriteTextAtomic(path, body + separator + sectionJson + "\n}\n");
        }

        private static string RemoveExistingTopLevelSection(string json, string sectionName)
        {
            string needle = "\"" + sectionName + "\"";
            int nameIndex = json.IndexOf(needle, StringComparison.Ordinal);
            if (nameIndex < 0)
                return json;

            int propertyStart = nameIndex;
            while (propertyStart > 0 && char.IsWhiteSpace(json[propertyStart - 1]))
                propertyStart--;

            bool removeLeadingComma = propertyStart > 0 && json[propertyStart - 1] == ',';
            if (removeLeadingComma)
                propertyStart--;

            int objectStart = json.IndexOf('{', nameIndex + needle.Length);
            if (objectStart < 0)
                return json;

            int depth = 0;
            bool stringLiteral = false;
            for (int i = objectStart; i < json.Length; i++)
            {
                char c = json[i];
                if (c == '"' && (i == 0 || json[i - 1] != '\\'))
                    stringLiteral = !stringLiteral;

                if (stringLiteral)
                    continue;

                if (c == '{')
                    depth++;
                else if (c == '}')
                {
                    depth--;
                    if (depth == 0)
                    {
                        int removeEnd = i + 1;
                        if (!removeLeadingComma)
                        {
                            int comma = removeEnd;
                            while (comma < json.Length && char.IsWhiteSpace(json[comma]))
                                comma++;
                            if (comma < json.Length && json[comma] == ',')
                                removeEnd = comma + 1;
                        }

                        return json.Remove(propertyStart, removeEnd - propertyStart);
                    }
                }
            }

            return json;
        }
    }
}
#endif
