#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Hecton8.Core;
using Hecton8.Core.Memory;
using Hecton8.Gameplay;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Hecton8.Gameplay.Editor
{
    public sealed class ScannerLoreDatabaseSyncTunerWindow : EditorWindow
    {
        private TextField _tokenField;
        private IntegerField _loreIndexField;
        private Slider _progressSlider;
        private Label _layoutLabel;
        private Label _vaultLabel;
        private LongField _mask0Field;
        private LongField _mask1Field;
        private LongField _mask2Field;
        private LongField _mask3Field;
        private LongField _telemetryFrameField;
        private LongField _telemetryHashField;
        private FloatField _telemetryProgressField;
        private LongField _telemetryUnlocksField;
        private Label _resultLabel;

        [MenuItem("Hecton8/Scanner/Lore Database Sync Tuner")]
        public static void Open()
        {
            GetWindow<ScannerLoreDatabaseSyncTunerWindow>("Scanner Lore Sync");
        }

        [MenuItem("Hecton8/Scanner/Run String Inquisition")]
        public static void RunStringInquisitionMenu()
        {
            ScannerStringInquisitionValidator.RunAndWriteReport();
        }

        public void CreateGUI()
        {
            VisualElement root = rootVisualElement;
            root.Clear();
            root.style.paddingLeft = 8;
            root.style.paddingRight = 8;
            root.style.paddingTop = 8;
            root.style.paddingBottom = 8;

            _layoutLabel = new Label();
            _layoutLabel.style.whiteSpace = WhiteSpace.Normal;
            root.Add(_layoutLabel);

            _tokenField = new TextField("Target token/hash") { value = "moss_sample" };
            _loreIndexField = new IntegerField("Lore bit index") { value = 130 };
            _progressSlider = new Slider("Progress", 0f, 1f) { value = 1f };
            root.Add(_tokenField);
            root.Add(_loreIndexField);
            root.Add(_progressSlider);

            root.Add(new Button(RefreshLayout) { text = "Validate Layout" });
            root.Add(new Button(SimulateUnlock) { text = "Simulate Hash Unlock" });
            root.Add(new Button(() => SetAllLoreBits(ulong.MaxValue)) { text = "Unlock All" });
            root.Add(new Button(() => SetAllLoreBits(0UL)) { text = "Lock All" });
            root.Add(new Button(ScannerStringInquisitionValidator.RunAndWriteReport) { text = "Run String Inquisition" });

            _vaultLabel = new Label();
            _vaultLabel.style.whiteSpace = WhiteSpace.Normal;
            root.Add(_vaultLabel);

            _mask0Field = CreateReadOnlyLongField("Mask word 0");
            _mask1Field = CreateReadOnlyLongField("Mask word 1");
            _mask2Field = CreateReadOnlyLongField("Mask word 2");
            _mask3Field = CreateReadOnlyLongField("Mask word 3");
            _telemetryFrameField = CreateReadOnlyLongField("Telemetry frame");
            _telemetryHashField = CreateReadOnlyLongField("Telemetry hash");
            _telemetryProgressField = new FloatField("Telemetry progress");
            _telemetryProgressField.SetEnabled(false);
            _telemetryUnlocksField = CreateReadOnlyLongField("Telemetry unlocks");
            root.Add(_mask0Field);
            root.Add(_mask1Field);
            root.Add(_mask2Field);
            root.Add(_mask3Field);
            root.Add(_telemetryFrameField);
            root.Add(_telemetryHashField);
            root.Add(_telemetryProgressField);
            root.Add(_telemetryUnlocksField);

            _resultLabel = new Label();
            _resultLabel.style.whiteSpace = WhiteSpace.Normal;
            root.Add(_resultLabel);
            RefreshLayout();
            RefreshVaultReadout();
        }

        private void OnInspectorUpdate()
        {
            RefreshVaultReadout();
        }

        private void RefreshLayout()
        {
            bool ok = ScannerDataMiningRouter.ValidateScanProgressLayout(
                out int sizeBytes,
                out int targetHashOffset,
                out int progressOffset,
                out int scanRateOffset,
                out int flagsOffset,
                out int scannerAupOffset,
                out int completedHashOffset);
            _layoutLabel.text =
                $"ScanProgressDTO layout: {(ok ? "OK" : "FAIL")} size={sizeBytes} " +
                $"hash@{targetHashOffset} progress@{progressOffset} rate@{scanRateOffset} " +
                $"flags@{flagsOffset} scannerAUP@{scannerAupOffset} completed@{completedHashOffset}";
        }

        private void SimulateUnlock()
        {
            uint hash = TryParseHash(_tokenField.value, out uint parsedHash)
                ? parsedHash
                : ComputeFnv1a32Ascii(_tokenField.value);
            uint loreIndex = (uint)math.clamp(_loreIndexField.value, 0, 1023);

            using (NativeArray<ScanProgressDTO> progress = new NativeArray<ScanProgressDTO>(1, Allocator.TempJob))
            using (NativeArray<ScannerLoreIndexDTO> index = new NativeArray<ScannerLoreIndexDTO>(32, Allocator.TempJob))
            using (NativeArray<ScannerEncyclopediaStateDTO> state = new NativeArray<ScannerEncyclopediaStateDTO>(1, Allocator.TempJob))
            using (NativeArray<ScannerTelemetryEntry> telemetry = new NativeArray<ScannerTelemetryEntry>(4, Allocator.TempJob))
            {
                ScannerDataMiningRouter.InsertLoreIndex(index, hash, loreIndex);
                progress[0] = new ScanProgressDTO
                {
                    TargetHashID = hash,
                    CurrentProgress01 = math.saturate(_progressSlider.value),
                    ScanRate = 0f,
                    Flags = ScannerDataMiningRouter.ScanProgressFlagActive | ScannerDataMiningRouter.ScanProgressFlagCompleted,
                    ScannerAUP = double3.zero,
                    LastFrame = 1u,
                    CompletedHash = hash
                };
                new EvaluateScanCompletionJob
                {
                    Progress = progress,
                    LoreIndex = index,
                    EncyclopediaState = state,
                    Telemetry = telemetry,
                    Frame = 1u,
                    CompletionCount = 1u
                }.Run();

                ScannerEncyclopediaStateDTO mask = state[0];
                _resultLabel.text =
                    $"Hash=0x{hash:X8} LoreBit={loreIndex} " +
                    $"Mask0={mask.Mask0:X16} Mask1={mask.Mask1:X16} Mask2={mask.Mask2:X16} Mask3={mask.Mask3:X16}";
            }
        }

        private void SetAllLoreBits(ulong maskValue)
        {
            IDataVault vault = GlobalRegistry.DataVault;
            if (vault == null)
            {
                _resultLabel.text = "Vault unavailable.";
                return;
            }

            if (!vault.TryGetGenerationHandle(BufferID.ShinobuScannerEncyclopediaState, out VaultGenerationHandle<ScannerEncyclopediaStateDTO> handle) ||
                !vault.TryResolveHandle(in handle, out NativeArray<ScannerEncyclopediaStateDTO> state) ||
                !state.IsCreated ||
                state.Length == 0)
            {
                _resultLabel.text = "Encyclopedia state unavailable.";
                return;
            }

            state[0] = CreateMaskState(maskValue);
            _resultLabel.text = maskValue == 0UL
                ? "All scanner lore bits locked."
                : "All scanner lore bits unlocked.";
            RefreshVaultReadout();
        }

        private void RefreshVaultReadout()
        {
            if (_vaultLabel == null)
                return;

            IDataVault vault = GlobalRegistry.DataVault;
            if (vault == null)
            {
                _vaultLabel.text = "Vault: unavailable.";
                return;
            }

            bool stateLive = false;
            if (vault.TryGetGenerationHandle(BufferID.ShinobuScannerEncyclopediaState, out VaultGenerationHandle<ScannerEncyclopediaStateDTO> stateHandle) &&
                vault.TryResolveHandle(in stateHandle, out NativeArray<ScannerEncyclopediaStateDTO> state) &&
                state.IsCreated &&
                state.Length > 0)
            {
                ScannerEncyclopediaStateDTO mask = state[0];
                stateLive = true;
                _mask0Field.SetValueWithoutNotify(unchecked((long)mask.Mask0));
                _mask1Field.SetValueWithoutNotify(unchecked((long)mask.Mask1));
                _mask2Field.SetValueWithoutNotify(unchecked((long)mask.Mask2));
                _mask3Field.SetValueWithoutNotify(unchecked((long)mask.Mask3));
            }

            bool telemetryLive = false;
            if (vault.TryGetGenerationHandle(BufferID.ShinobuScannerTelemetryRing, out VaultGenerationHandle<ScannerTelemetryEntry> telemetryHandle) &&
                vault.TryResolveHandle(in telemetryHandle, out NativeArray<ScannerTelemetryEntry> telemetry) &&
                telemetry.IsCreated &&
                telemetry.Length > 0)
            {
                int index = math.max(0, (int)(EditorApplication.timeSinceStartup % telemetry.Length));
                ScannerTelemetryEntry entry = telemetry[index];
                telemetryLive = true;
                _telemetryFrameField.SetValueWithoutNotify(entry.Frame);
                _telemetryHashField.SetValueWithoutNotify(entry.TargetHash);
                _telemetryProgressField.SetValueWithoutNotify(entry.Progress01);
                _telemetryUnlocksField.SetValueWithoutNotify(entry.CompletedCount);
            }

            _vaultLabel.text = stateLive && telemetryLive
                ? "Vault: live"
                : "Vault: unavailable";
        }

        private static LongField CreateReadOnlyLongField(string label)
        {
            LongField field = new LongField(label);
            field.SetEnabled(false);
            return field;
        }

        private static ScannerEncyclopediaStateDTO CreateMaskState(ulong value)
        {
            return new ScannerEncyclopediaStateDTO
            {
                Mask0 = value,
                Mask1 = value,
                Mask2 = value,
                Mask3 = value,
                Mask4 = value,
                Mask5 = value,
                Mask6 = value,
                Mask7 = value,
                Mask8 = value,
                Mask9 = value,
                Mask10 = value,
                Mask11 = value,
                Mask12 = value,
                Mask13 = value,
                Mask14 = value,
                Mask15 = value
            };
        }

        private static bool TryParseHash(string value, out uint hash)
        {
            hash = 0u;
            if (string.IsNullOrWhiteSpace(value))
                return false;

            string trimmed = value.Trim();
            if (trimmed.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                return uint.TryParse(trimmed.Substring(2), System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out hash);

            return uint.TryParse(trimmed, out hash);
        }

        private static uint ComputeFnv1a32Ascii(string value)
        {
            if (string.IsNullOrEmpty(value))
                return 0u;

            uint hash = 2166136261u;
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                uint b = c <= 0x7F ? c : (uint)(byte)'?';
                hash ^= b;
                hash *= 16777619u;
            }

            return hash;
        }
    }

    public static class ScannerStringInquisitionValidator
    {
        private static readonly string[] ScanRoots =
        {
            "Assets/_Project/Scripts/ScannerTool.cs",
            "Assets/_Project/Scripts/ScannableTarget.cs",
            "Assets/_Project/Scripts/Tools/Scanner/Contracts/ScannerLoreContracts.cs",
            "Assets/_Project/Scripts/Gameplay/ScannerDataMiningRouter.cs",
            "Assets/_Project/Scripts/UI/PDAEncyclopediaStreamer.cs",
            "Assets/_Project/Scripts/UI/PdaH8lrLoreStore.cs"
        };

        private static readonly string[] ForbiddenHotPatterns =
        {
            ".name ==",
            "== target.name",
            "GetComponent<ItemData>",
            "GetComponent<ScannableTarget>",
            "GetComponent<ScannableFragment>",
            "transform.forward",
            "transform.position",
            "transform.right",
            "Time.frameCount",
            "Time.deltaTime",
            "UnityEngine.Random"
        };

        public static void RunAndWriteReport()
        {
            string projectRoot = ResolveProjectRoot();
            List<Finding> findings = new List<Finding>(32);
            for (int i = 0; i < ScanRoots.Length; i++)
            {
                string path = Path.Combine(projectRoot, ScanRoots[i]);
                if (!File.Exists(path))
                    continue;

                string[] lines = File.ReadAllLines(path);
                for (int lineIndex = 0; lineIndex < lines.Length; lineIndex++)
                {
                    string line = lines[lineIndex];
                    for (int patternIndex = 0; patternIndex < ForbiddenHotPatterns.Length; patternIndex++)
                    {
                        string pattern = ForbiddenHotPatterns[patternIndex];
                        if (!ShouldEvaluatePattern(ScanRoots[i], pattern))
                            continue;

                        if (line.IndexOf(pattern, StringComparison.Ordinal) < 0)
                            continue;

                        findings.Add(new Finding(ScanRoots[i], lineIndex + 1, pattern, line.Trim()));
                    }
                }
            }

            string reportPath = Path.Combine(projectRoot, "Docs", "Reports", "CONSTRUCTION_OPTIMIZATION_REPORT.json");
            Directory.CreateDirectory(Path.GetDirectoryName(reportPath) ?? projectRoot);
            File.WriteAllText(reportPath, BuildJson(findings), Encoding.UTF8);
            Debug.Log($"SHINOBU_226 scanner string inquisition wrote {findings.Count} findings to {reportPath}");
        }

        private static bool ShouldEvaluatePattern(string scanRoot, string pattern)
        {
            if (!pattern.StartsWith("transform.", StringComparison.Ordinal))
                return true;

            return scanRoot.EndsWith("ScannerDataMiningRouter.cs", StringComparison.Ordinal);
        }

        private static string BuildJson(List<Finding> findings)
        {
            StringBuilder builder = new StringBuilder(2048 + findings.Count * 256);
            builder.AppendLine("{");
            builder.Append("  \"generated_utc\": \"").Append(DateTime.UtcNow.ToString("O")).AppendLine("\",");
            builder.AppendLine("  \"scanner\": \"SHINOBU_226_SCANNER_LORE_DATABASE_SYNC\",");
            builder.AppendLine("  \"summary\": \"Scanner hot path string lookup inquisition\",");
            builder.Append("  \"blocked_findings\": ").Append(findings.Count).AppendLine(",");
            builder.AppendLine("  \"forbidden_patterns\": [\".name ==\", \"== target.name\", \"GetComponent<ItemData>\", \"GetComponent<ScannableTarget>\", \"GetComponent<ScannableFragment>\", \"transform.forward\", \"transform.position\", \"transform.right\", \"Time.frameCount\", \"Time.deltaTime\", \"UnityEngine.Random\"],");
            builder.AppendLine("  \"findings\": [");
            for (int i = 0; i < findings.Count; i++)
            {
                Finding finding = findings[i];
                builder.AppendLine("    {");
                builder.Append("      \"file\": \"").Append(Escape(finding.File)).AppendLine("\",");
                builder.Append("      \"line\": ").Append(finding.Line).AppendLine(",");
                builder.Append("      \"pattern\": \"").Append(Escape(finding.Pattern)).AppendLine("\",");
                builder.Append("      \"snippet\": \"").Append(Escape(finding.Snippet)).AppendLine("\"");
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
            return (value ?? string.Empty).Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        private static string ResolveProjectRoot()
        {
            string dataPath = Application.dataPath;
            return string.IsNullOrEmpty(dataPath)
                ? Directory.GetCurrentDirectory()
                : Path.GetDirectoryName(dataPath) ?? Directory.GetCurrentDirectory();
        }

        private readonly struct Finding
        {
            public Finding(string file, int line, string pattern, string snippet)
            {
                File = file;
                Line = line;
                Pattern = pattern;
                Snippet = snippet;
            }

            public readonly string File;
            public readonly int Line;
            public readonly string Pattern;
            public readonly string Snippet;
        }
    }
}
#endif
