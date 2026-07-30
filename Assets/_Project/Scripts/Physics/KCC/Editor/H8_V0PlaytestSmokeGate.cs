#if UNITY_EDITOR
using System;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Hecton8.Physics.KCC.Editor
{
    /// <summary>
    /// Single V0 merge-gate entry that wires existing harnesses.
    /// Batch: -executeMethod Hecton8.Physics.KCC.Editor.H8_V0PlaytestSmokeGate.RunFromCommandLine
    /// Menu: Hecton8/QA/V0 Playtest Smoke Gate (KCC headless)
    ///
    /// Runs: Shinobu355KccSmokeRunner.Run (headless KCC + PrecisionDrift lane).
    /// Does NOT load 02_HECTON_WORLD or claim player-route proof.
    /// PlayMode menu+sandbox, save roundtrip, fauna/tools remain separate owed proofs.
    /// </summary>
    public static class H8_V0PlaytestSmokeGate
    {
        private const string ResultRelativePath = "Docs/AgentLogs/H8_V0_PLAYTEST_SMOKE_GATE.json";
        private const string GateId = "H8_V0PlaytestSmokeGate";
        private const string GateVersion = "2026-07-30";

        // HydrodynamicKccRuntime.KccSmokeFailurePrecisionDrift = 1u << 3 (keep local to avoid asm churn).
        private const uint KccSmokeFailurePrecisionDrift = 1u << 3;

        [MenuItem("Hecton8/QA/V0 Playtest Smoke Gate (KCC headless)", priority = 50)]
        public static void RunFromMenu()
        {
            RunInternal(exitEditor: false);
        }

        /// <summary>
        /// Batchmode entry. Exit 0 = KCC pass; non-zero = fail or exception.
        /// </summary>
        public static void RunFromCommandLine()
        {
            RunInternal(exitEditor: true);
        }

        private static void RunInternal(bool exitEditor)
        {
            int exitCode = 1;
            var sb = new StringBuilder(2048);
            sb.Append("{\n");
            AppendJson(sb, "gate", GateId, true);
            AppendJson(sb, "version", GateVersion, true);
            AppendJson(sb, "utc", DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture), true);
            AppendJson(sb, "claimsWorldPlayable", "false", true);
            AppendJson(sb, "notes",
                "KCC headless only. WORLD boot / save / fauna / tools / death not executed by this gate.",
                true);

            try
            {
                bool coneOk = Shinobu355KccSmokeRunner.ValidateApexConeFallContract(
                    out float coneDisp, out float coneMaxSpeed);
                AppendJson(sb, "kccConeContractPass", coneOk ? "true" : "false", true);
                AppendJsonNumber(sb, "kccConeDisplacementPerFrameM", coneDisp, true);
                AppendJsonNumber(sb, "kccConeTuningMaxSpeedMps", coneMaxSpeed, true);

                bool kccPass = Shinobu355KccSmokeRunner.Run(out Shinobu355KccSmokeSummary summary);
                uint flags = summary.ErrorFlags;
                bool precisionDrift = (flags & KccSmokeFailurePrecisionDrift) != 0u;

                AppendJson(sb, "kccRunReturnedPass", kccPass ? "true" : "false", true);
                AppendJson(sb, "kccErrorFlagsHex", "0x" + flags.ToString("X8", CultureInfo.InvariantCulture), true);
                AppendJsonNumber(sb, "kccFailureCount", summary.FailureCount, true);
                AppendJsonNumber(sb, "kccDriftErrorMm", summary.DriftErrorMillimeters, true);
                AppendJsonNumber(sb, "kccAvgUsPerFrame", summary.AverageMicrosecondsPerFrame, true);
                AppendJsonNumber(sb, "kccManagedBytes", summary.ManagedBytesAllocated, true);
                AppendJson(sb, "kccPrecisionDriftClear", precisionDrift ? "false" : "true", true);

                bool overall = coneOk && kccPass && !precisionDrift && flags == 0u;
                AppendJson(sb, "overallPass", overall ? "true" : "false", true);
                AppendJson(sb, "status", overall ? "PASS" : "FAIL", false);
                sb.Append("\n}\n");

                WriteResult(sb.ToString());

                if (overall)
                {
                    Debug.Log($"[{GateId}] PASS — KCC headless clean (PrecisionDrift clear). Result: {ResultRelativePath}");
                    exitCode = 0;
                }
                else
                {
                    Debug.LogError(
                        $"[{GateId}] FAIL — coneOk={coneOk} kccPass={kccPass} precisionDrift={precisionDrift} flags=0x{flags:X8}. Result: {ResultRelativePath}");
                    exitCode = 2;
                }
            }
            catch (Exception ex)
            {
                AppendJson(sb, "overallPass", "false", true);
                AppendJson(sb, "status", "EXCEPTION", true);
                AppendJson(sb, "exception", ex.GetType().Name + ": " + ex.Message, false);
                sb.Append("\n}\n");
                try { WriteResult(sb.ToString()); } catch { /* best effort */ }
                Debug.LogError($"[{GateId}] EXCEPTION: {ex}");
                exitCode = 3;
            }

            if (exitEditor)
            {
                EditorApplication.Exit(exitCode);
            }
        }

        private static void WriteResult(string json)
        {
            string root = Shinobu355KccSmokeRunner.ProjectRoot;
            string path = Path.Combine(root, ResultRelativePath.Replace('/', Path.DirectorySeparatorChar));
            string dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            File.WriteAllText(path, json, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        }

        private static void AppendJson(StringBuilder sb, string key, string value, bool trailingComma)
        {
            sb.Append("  \"");
            sb.Append(key);
            sb.Append("\": \"");
            sb.Append(Escape(value));
            sb.Append('"');
            if (trailingComma) sb.Append(',');
            sb.Append('\n');
        }

        private static void AppendJsonNumber(StringBuilder sb, string key, double value, bool trailingComma)
        {
            sb.Append("  \"");
            sb.Append(key);
            sb.Append("\": ");
            if (double.IsNaN(value) || double.IsInfinity(value))
                sb.Append("null");
            else
                sb.Append(value.ToString("G17", CultureInfo.InvariantCulture));
            if (trailingComma) sb.Append(',');
            sb.Append('\n');
        }

        private static void AppendJsonNumber(StringBuilder sb, string key, uint value, bool trailingComma)
        {
            sb.Append("  \"");
            sb.Append(key);
            sb.Append("\": ");
            sb.Append(value.ToString(CultureInfo.InvariantCulture));
            if (trailingComma) sb.Append(',');
            sb.Append('\n');
        }

        private static void AppendJsonNumber(StringBuilder sb, string key, long value, bool trailingComma)
        {
            sb.Append("  \"");
            sb.Append(key);
            sb.Append("\": ");
            sb.Append(value.ToString(CultureInfo.InvariantCulture));
            if (trailingComma) sb.Append(',');
            sb.Append('\n');
        }

        private static string Escape(string s)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r");
        }
    }
}
#endif
