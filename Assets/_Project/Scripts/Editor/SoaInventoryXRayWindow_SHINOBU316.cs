#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Hecton8.Core;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Inventory;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Hecton8.Editor
{
    public sealed class SoaInventoryXRayWindow_SHINOBU316 : EditorWindow
    {
        private Label _status;
        private Label _layout;
        private Label _telemetry;
        private Label _vault;
        private Label _signals;
        private Label _injectStatus;
        private IntegerField _hashField;
        private IntegerField _deltaField;
        private Toggle _drawGizmos;
        private Slider _qualityPreview;

        [MenuItem("Hecton8/Inventory/SoA Inventory X-Ray")]
        public static void Open()
        {
            GetWindow<SoaInventoryXRayWindow_SHINOBU316>("SoA Inventory X-Ray");
        }

        private void OnEnable()
        {
            BuildUi();
            SceneView.duringSceneGui -= DrawSceneGizmos;
            SceneView.duringSceneGui += DrawSceneGizmos;
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= DrawSceneGizmos;
            EditorApplication.update -= Tick;
        }

        public void CreateGUI()
        {
            BuildUi();
        }

        private void BuildUi()
        {
            rootVisualElement.Clear();
            rootVisualElement.style.paddingLeft = 8;
            rootVisualElement.style.paddingRight = 8;
            rootVisualElement.style.paddingTop = 8;
            rootVisualElement.style.paddingBottom = 8;

            _status = new Label("No PlayerInventory runtime found.");
            _layout = new Label(SoaInventoryQueryEngine.RuntimeLayoutValid() ? "Layout: PASS" : "Layout: FAIL");
            _telemetry = new Label("Telemetry: pending");
            _vault = new Label("Vault: pending");
            _signals = new Label("InventoryChangedSignal: pending");
            _injectStatus = new Label("Manual injection: idle");
            _hashField = new IntegerField("Target hash") { value = unchecked((int)0x80000001u) };
            _deltaField = new IntegerField("Quantity delta") { value = 1 };
            _drawGizmos = new Toggle("Draw scene query gizmo") { value = true };
            _qualityPreview = new Slider("GlobalQualityWeight preview", 0f, 1f) { value = 0.5f, showInputField = true };

            Button inject = new Button(InjectHash) { text = "Inject Hash Into Vault Lane" };
            Button dump = new Button(DumpBlackBox) { text = "Dump SHINOBU_316 Black Box" };
            Button scan = new Button(RunScanner) { text = "Run OOP Inventory Scanner" };

            rootVisualElement.Add(_status);
            rootVisualElement.Add(_layout);
            rootVisualElement.Add(_telemetry);
            rootVisualElement.Add(_vault);
            rootVisualElement.Add(_signals);
            rootVisualElement.Add(_qualityPreview);
            rootVisualElement.Add(_hashField);
            rootVisualElement.Add(_deltaField);
            rootVisualElement.Add(inject);
            rootVisualElement.Add(_injectStatus);
            rootVisualElement.Add(_drawGizmos);
            rootVisualElement.Add(dump);
            rootVisualElement.Add(scan);
        }

        private void Tick()
        {
            PlayerInventory inventory = FindInventory();
            if (inventory == null)
            {
                _status.text = "No PlayerInventory runtime found.";
                return;
            }

            _status.text = "InventoryVersion " + inventory.InventoryVersion +
                           " | mass " + inventory.TotalMassKg.ToString("0.00") +
                           " kg | mask 0x" + inventory.CurrentInventoryMask.ToString("X16");
            UpdateSignalSnapshot();
            if (inventory.TryReadSoaInventoryXRay(out SoaInventoryQueryXRaySnapshot snapshot))
            {
                _telemetry.text = "Frame " + snapshot.Frame +
                                  " | active " + snapshot.ActiveSlotCount + "/" + snapshot.Capacity +
                                  " | est " + snapshot.EstimatedMicroseconds.ToString("0.000") + " us" +
                                  " | flags 0x" + snapshot.Flags.ToString("X");
                _vault.text = "Vault slots " + snapshot.VaultSlotCapacity +
                              " | ring cursor " + snapshot.TelemetryCursor +
                              " | q " + snapshot.GlobalQualityWeight.ToString("0.000");
            }
            else
            {
                _telemetry.text = "Telemetry: no owner-phase frame written yet.";
                _vault.text = "Vault: unresolved";
            }
        }

        private void UpdateSignalSnapshot()
        {
            ReadOnlySpan<InventoryChangedSignal> signals = SignalBus<InventoryChangedSignal>.GetFrameSnapshot();
            if (signals.Length == 0)
            {
                _signals.text = "InventoryChangedSignal: empty frame snapshot";
                return;
            }

            ref readonly InventoryChangedSignal signal = ref signals[signals.Length - 1];
            _signals.text = "InventoryChangedSignal rev " + signal.Revision +
                            " | inv 0x" + signal.InventoryHash.ToString("X8") +
                            " | occupied " + signal.OccupiedCells +
                            " | frame " + signal.Frame;
        }

        private void InjectHash()
        {
            PlayerInventory inventory = FindInventory();
            if (inventory == null)
            {
                _injectStatus.text = "Manual injection: no PlayerInventory";
                return;
            }

            uint targetHash = unchecked((uint)_hashField.value);
            int delta = _deltaField.value;
            bool accepted = inventory.TryInjectSoaVaultItemForXRay(targetHash, delta);
            _injectStatus.text = accepted
                ? "Manual injection queued: hash 0x" + targetHash.ToString("X8") + " delta " + delta
                : "Manual injection: rejected";
        }

        private void DumpBlackBox()
        {
            PlayerInventory inventory = FindInventory();
            bool dumped = inventory != null && inventory.TryDumpSoaQueryTelemetry();
            Debug.Log(dumped ? "Wrote Docs/AgentLogs/Dump_SHINOBU_316.bin" : "SHINOBU_316 dump unavailable.");
        }

        private void RunScanner()
        {
            string result = OOP_Inventory_Scanner.RunScan();
            Debug.Log(result);
        }

        private void DrawSceneGizmos(SceneView sceneView)
        {
            if (_drawGizmos == null || !_drawGizmos.value)
                return;

            PlayerInventory inventory = FindInventory();
            if (inventory == null || !inventory.TryReadSoaInventoryXRay(out SoaInventoryQueryXRaySnapshot snapshot))
                return;

            Transform t = inventory.transform;
            Handles.color = snapshot.ActiveSlotCount > 0
                ? new Color(0.05f, 0.85f, 0.9f, 0.85f)
                : new Color(0.9f, 0.2f, 0.1f, 0.85f);
            float radius = math.lerp(0.35f, 1.25f, math.saturate(snapshot.ActiveSlotCount / (float)math.max(1, snapshot.Capacity)));
            Handles.DrawWireDisc(t.position, Vector3.up, radius);
            string label = snapshot.TargetHashID != 0u
                ? "SoA hash 0x" + snapshot.TargetHashID.ToString("X8") + " qty " + snapshot.QuantityTotal
                : "SoA " + snapshot.ActiveSlotCount + "/" + snapshot.Capacity;
            Handles.Label(t.position + Vector3.up * (radius + 0.25f), label);
        }

        private static PlayerInventory FindInventory()
        {
            return UnityEngine.Object.FindObjectOfType<PlayerInventory>();
        }
    }

    public static class OOP_Inventory_Scanner
    {
        private const string ReportRelativePath = "Docs/Reports/LOGISTICS_OPTIMIZATION_REPORT.json";
        private static readonly string[] RuntimeRoots =
        {
            "Assets/_Project/Scripts/Inventory",
            "Assets/_Project/Scripts/Economy",
            "Assets/_Project/Scripts/PlayerInventory.cs",
            "Assets/_Project/Scripts/ItemData.cs"
        };

        private static readonly string[] ForbiddenRuntimeTokens =
        {
            "List<Item>",
            "List <Item>",
            "List<ItemData>",
            "List <ItemData>",
            "FindById(",
            "string itemId"
        };

        [MenuItem("Hecton8/Inventory/OOP Inventory Scanner")]
        public static void RunMenu()
        {
            Debug.Log(RunScan());
        }

        public static string RunScan()
        {
            string root = Directory.GetCurrentDirectory();
            List<Finding> findings = new List<Finding>(64);
            for (int i = 0; i < RuntimeRoots.Length; i++)
            {
                string path = Path.Combine(root, RuntimeRoots[i]);
                if (Directory.Exists(path))
                    ScanDirectory(root, path, findings);
                else if (File.Exists(path))
                    ScanFile(root, path, findings);
            }

            string reportPath = Path.Combine(root, ReportRelativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(reportPath));
            File.WriteAllText(reportPath, BuildJson(root, findings), Encoding.UTF8);
            return "OOP_Inventory_Scanner wrote " + reportPath + " findings=" + findings.Count;
        }

        private static void ScanDirectory(string root, string directory, List<Finding> findings)
        {
            foreach (string path in Directory.EnumerateFiles(directory, "*.cs", SearchOption.AllDirectories))
            {
                string normalized = path.Replace('\\', '/');
                if (normalized.Contains("/Editor/"))
                    continue;

                ScanFile(root, path, findings);
            }
        }

        private static void ScanFile(string root, string path, List<Finding> findings)
        {
            string source = File.ReadAllText(path);
            for (int tokenIndex = 0; tokenIndex < ForbiddenRuntimeTokens.Length; tokenIndex++)
            {
                string token = ForbiddenRuntimeTokens[tokenIndex];
                int index = 0;
                while (index >= 0 && index < source.Length)
                {
                    index = source.IndexOf(token, index, StringComparison.Ordinal);
                    if (index < 0)
                        break;

                    if (!IsInsideComment(source, index) && !IsColdMetadataException(path, token))
                    {
                        findings.Add(new Finding
                        {
                            Path = MakeRelative(root, path),
                            Line = ResolveLine(source, index),
                            Token = token,
                            Method = ResolveMethodContext(source, index)
                        });
                    }

                    index += token.Length;
                }
            }
        }

        private static bool IsColdMetadataException(string path, string token)
        {
            string normalized = path.Replace('\\', '/');
            return normalized.EndsWith("/ItemData.cs", StringComparison.Ordinal);
        }

        private static bool IsInsideComment(string source, int index)
        {
            int lineStart = source.LastIndexOf('\n', math.max(0, index - 1));
            lineStart = lineStart < 0 ? 0 : lineStart + 1;
            int comment = source.IndexOf("//", lineStart, index - lineStart, StringComparison.Ordinal);
            return comment >= 0;
        }

        private static int ResolveLine(string source, int index)
        {
            int line = 1;
            for (int i = 0; i < index && i < source.Length; i++)
            {
                if (source[i] == '\n')
                    line++;
            }

            return line;
        }

        private static string ResolveMethodContext(string source, int index)
        {
            int brace = source.LastIndexOf('{', math.max(0, index - 1));
            if (brace <= 0)
                return string.Empty;

            int lineStart = source.LastIndexOf('\n', brace);
            int start = lineStart >= 0 ? lineStart + 1 : 0;
            string signature = source.Substring(start, brace - start).Trim();
            return signature.Length > 160 ? signature.Substring(signature.Length - 160) : signature;
        }

        private static string BuildJson(string root, List<Finding> findings)
        {
            StringBuilder builder = new StringBuilder(4096);
            builder.AppendLine("{");
            builder.AppendLine("  \"scanner\": \"OOP_Inventory_Scanner\",");
            builder.AppendLine("  \"agent\": \"SHINOBU_316\",");
            builder.AppendLine("  \"domain\": \"SOA_INVENTORY_QUERY_ENGINE\",");
            builder.AppendLine("  \"status\": \"" + (findings.Count == 0 ? "PASS_STATIC_SCAN" : "FAIL_STATIC_SCAN") + "\",");
            builder.AppendLine("  \"runtimeRoute\": \"PlayerInventory partial -> GlobalDataVault BufferID.ShinobuInventoryHashes/Quantities/Durabilities -> SoaInventoryQueryEngine -> existing InventoryChangedSignal snapshot\",");
            builder.AppendLine("  \"forbiddenTokens\": [\"List<Item>\", \"List<ItemData>\", \"FindById(\", \"string itemId\"],");
            builder.AppendLine("  \"coldMetadataException\": \"ItemData remains ScriptableObject authoring metadata only; runtime query/mutation API is uint hash based.\",");
            builder.AppendLine("  \"simdKernel\": \"AVX2 8-lane cmpeq/movemask, SSE2 4-lane cmpeq/movemask, NEON 4-lane vceqq_u32, uint4 fallback, math.tzcnt lane extraction\",");
            builder.AppendLine("  \"defragmentation\": \"Swap-and-pop for dense Vault SoA query lanes; 2D grid owner remains authoritative for UI placement.\",");
            builder.AppendLine("  \"globalQualityWeight\": \"Continuous query admission budget via InventoryRoutingNetwork.ResolveTimeSliceBatchSize.\",");
            builder.AppendLine("  \"blackBox\": \"Docs/AgentLogs/Dump_SHINOBU_316.bin, 300 entries, 64 bytes each\",");
            builder.AppendLine("  \"scanScope\": \"Inventory, Economy, PlayerInventory.cs, ItemData.cs with Editor excluded\",");
            builder.AppendLine("  \"findingsCount\": " + findings.Count + ",");
            builder.AppendLine("  \"findings\": [");
            for (int i = 0; i < findings.Count; i++)
            {
                Finding finding = findings[i];
                builder.AppendLine("    {");
                builder.AppendLine("      \"path\": \"" + Escape(finding.Path) + "\",");
                builder.AppendLine("      \"line\": " + finding.Line + ",");
                builder.AppendLine("      \"token\": \"" + Escape(finding.Token) + "\",");
                builder.AppendLine("      \"method\": \"" + Escape(finding.Method) + "\"");
                builder.Append("    }");
                if (i + 1 < findings.Count)
                    builder.Append(',');
                builder.AppendLine();
            }

            builder.AppendLine("  ]");
            builder.AppendLine("}");
            return builder.ToString();
        }

        private static string Escape(string value)
        {
            return string.IsNullOrEmpty(value)
                ? string.Empty
                : value.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        private static string MakeRelative(string root, string path)
        {
            string normalizedRoot = root.Replace('\\', '/').TrimEnd('/');
            string normalizedPath = path.Replace('\\', '/');
            return normalizedPath.StartsWith(normalizedRoot, StringComparison.Ordinal)
                ? normalizedPath.Substring(normalizedRoot.Length).TrimStart('/')
                : normalizedPath;
        }

        private struct Finding
        {
            public string Path;
            public int Line;
            public string Token;
            public string Method;
        }
    }
}
#endif
