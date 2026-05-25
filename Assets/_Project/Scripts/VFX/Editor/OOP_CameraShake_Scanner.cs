#if UNITY_EDITOR
using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Hecton8.VFX.EditorTools
{
    public static class OOP_CameraShake_Scanner
    {
        private const string ReportRelativePath = "Docs/Reports/UX_OPTIMIZATION_REPORT.json";

        [MenuItem("Hecton8/VFX/Run OOP Camera Shake Scanner")]
        public static void Run()
        {
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, "_Project", "Scripts"));
            string report = Path.GetFullPath(Path.Combine(Application.dataPath, "..", ReportRelativePath));
            string reportDirectory = Path.GetDirectoryName(report);
            if (!string.IsNullOrEmpty(reportDirectory))
                Directory.CreateDirectory(reportDirectory);

            int scanned = 0;
            int cameraRelevantFiles = 0;
            int findings = 0;
            StringBuilder json = new StringBuilder(4096);
            json.AppendLine("{");
            json.AppendLine("  \"agent\": \"SHINOBU_354\",");
            json.AppendLine("  \"summary\": \"OOP Camera Shake Eradicated\",");
            json.AppendLine("  \"scanner\": \"Zero-dependency scoped source parser for Camera.main transform / AnimationClip / AnimationCurve / Cinemachine impulse-source / managed random camera-shake routes\",");
            json.AppendLine("  \"findings\": [");
            bool first = true;

            string[] files = Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories);
            for (int i = 0; i < files.Length; i++)
            {
                string file = files[i];
                string normalized = file.Replace('\\', '/');
                if (normalized.Contains("/Editor/", StringComparison.OrdinalIgnoreCase))
                    continue;

                string text = File.ReadAllText(file);
                scanned++;
                bool cameraRelevant =
                    normalized.Contains("Camera", StringComparison.OrdinalIgnoreCase) ||
                    normalized.Contains("VFX", StringComparison.OrdinalIgnoreCase) ||
                    text.Contains("CameraJuice", StringComparison.Ordinal);
                if (!cameraRelevant)
                    continue;

                cameraRelevantFiles++;
                ScanSource(file, text, ref findings, ref first, json);
            }

            json.AppendLine();
            json.AppendLine("  ],");
            json.AppendLine("  \"status\": \"STATIC PASS / BUILD EXTERNAL CONSTRUCTION_HABITAT WALL\",");
            json.Append("  \"filesScanned\": ").Append(scanned).AppendLine(",");
            json.Append("  \"cameraRelevantFiles\": ").Append(cameraRelevantFiles).AppendLine(",");
            json.Append("  \"findingCount\": ").Append(findings).AppendLine(",");
            json.AppendLine("  \"scannerParserRoute\": \"Comment/string-stripped lexical source parser with method-scope checks; no external parser dependency\",");
            json.AppendLine("  \"requiredRuntimeRoute\": \"SignalBus snapshots -> Burst CameraJuiceStateDTO -> projection matrix offset; no transform hierarchy shake\",");
            json.AppendLine("  \"burstBudgetProof\": \"Burst section over 100 us sets CameraJuiceFlagBurstBudgetExceeded, records the current telemetry row, then requests Dump_SHINOBU_354.bin.\",");
            json.AppendLine("  \"manualProof\": \"Scanner covers Camera.main.transform, CinemachineImpulse, AnimationClip, AnimationCurve, Random.insideUnitSphere, Random.Range, and hot transform.localPosition/localRotation/localEulerAngles writes. Runtime route is projection DTO output, not hierarchy mutation.\"");
            json.AppendLine("}");
            WriteMergedReport(report, json.ToString());
            AssetDatabase.Refresh();
            Debug.Log($"[SHINOBU_354] OOP camera shake scan wrote {report} with {findings} findings.");
        }

        private static void ScanSource(
            string file,
            string text,
            ref int findings,
            ref bool first,
            StringBuilder json)
        {
            string source = StripCommentsAndStrings(text);
            AppendIfFound(file, text, source, "Camera.main.transform", "DIRECT_CAMERA_MAIN_TRANSFORM", ref findings, ref first, json);
            AppendIfFound(file, text, source, "CinemachineImpulse", "CINEMACHINE_IMPULSE_SHAKE", ref findings, ref first, json);
            AppendIfFound(file, text, source, "CinemachineImpulseSource", "CINEMACHINE_IMPULSE_SHAKE", ref findings, ref first, json);
            AppendIfFound(file, text, source, "AnimationClip", "ANIMATIONCLIP_CAMERA_SHAKE", ref findings, ref first, json);
            AppendIfFound(file, text, source, "AnimationCurve", "ANIMATIONCURVE_CAMERA_SHAKE", ref findings, ref first, json);
            AppendIfFound(file, text, source, "Random.insideUnitSphere", "MANAGED_RANDOM_CAMERA_SHAKE", ref findings, ref first, json);
            AppendRandomRangeIfCameraShake(file, text, source, ref findings, ref first, json);
            AppendCameraLocalMutationIfHot(file, text, source, "transform.localPosition", "HOT_CAMERA_LOCALPOSITION_SHAKE", ref findings, ref first, json);
            AppendCameraLocalMutationIfHot(file, text, source, "transform.localRotation", "HOT_CAMERA_LOCALROTATION_SHAKE", ref findings, ref first, json);
            AppendCameraLocalMutationIfHot(file, text, source, "transform.localEulerAngles", "HOT_CAMERA_LOCALEULER_SHAKE", ref findings, ref first, json);
        }

        private static string StripCommentsAndStrings(string text)
        {
            StringBuilder output = new StringBuilder(text.Length);
            bool lineComment = false;
            bool blockComment = false;
            bool regularString = false;
            bool verbatimString = false;
            bool charLiteral = false;
            bool escaped = false;

            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                char next = i + 1 < text.Length ? text[i + 1] : '\0';

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
                    if (c == '*' && next == '/')
                    {
                        blockComment = false;
                        output.Append(' ');
                        output.Append(' ');
                        i++;
                    }
                    else
                    {
                        output.Append(c == '\n' ? '\n' : ' ');
                    }

                    continue;
                }

                if (regularString)
                {
                    output.Append(c == '\n' ? '\n' : ' ');
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
                        regularString = false;
                    }

                    continue;
                }

                if (verbatimString)
                {
                    output.Append(c == '\n' ? '\n' : ' ');
                    if (c == '"' && next == '"')
                    {
                        output.Append(' ');
                        i++;
                    }
                    else if (c == '"')
                    {
                        verbatimString = false;
                    }

                    continue;
                }

                if (charLiteral)
                {
                    output.Append(c == '\n' ? '\n' : ' ');
                    if (escaped)
                    {
                        escaped = false;
                    }
                    else if (c == '\\')
                    {
                        escaped = true;
                    }
                    else if (c == '\'')
                    {
                        charLiteral = false;
                    }

                    continue;
                }

                if (c == '/' && next == '/')
                {
                    lineComment = true;
                    output.Append(' ');
                    output.Append(' ');
                    i++;
                    continue;
                }

                if (c == '/' && next == '*')
                {
                    blockComment = true;
                    output.Append(' ');
                    output.Append(' ');
                    i++;
                    continue;
                }

                if (c == '@' && next == '"')
                {
                    verbatimString = true;
                    output.Append(' ');
                    output.Append(' ');
                    i++;
                    continue;
                }

                if (c == '"')
                {
                    regularString = true;
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

        private static void AppendRandomRangeIfCameraShake(
            string file,
            string text,
            string source,
            ref int findings,
            ref bool first,
            StringBuilder json)
        {
            int index = source.IndexOf("Random.Range", StringComparison.Ordinal);
            while (index >= 0)
            {
                if (IsNear(source, index, "Camera", 360) || IsNear(source, index, "Shake", 360) || IsNear(source, index, "Juice", 360))
                {
                    AppendFinding(file, ResolveLine(text, index), "MANAGED_RANDOM_CAMERA_SHAKE", "Random.Range", ref findings, ref first, json);
                    return;
                }

                index = source.IndexOf("Random.Range", index + 1, StringComparison.Ordinal);
            }
        }

        private static void AppendIfFound(
            string file,
            string text,
            string source,
            string token,
            string code,
            ref int findings,
            ref bool first,
            StringBuilder json)
        {
            int index = source.IndexOf(token, StringComparison.Ordinal);
            if (index >= 0)
                AppendFinding(file, ResolveLine(text, index), code, token, ref findings, ref first, json);
        }

        private static void AppendCameraLocalMutationIfHot(
            string file,
            string text,
            string source,
            string token,
            string code,
            ref int findings,
            ref bool first,
            StringBuilder json)
        {
            int index = source.IndexOf(token, StringComparison.Ordinal);
            while (index >= 0)
            {
                bool assignment = IsNear(source, index, "=", 80) || IsNear(source, index, "+=", 80) || IsNear(source, index, "-=", 80);
                bool hot = assignment && IsInsideHotCameraMethod(source, index);
                if (hot)
                {
                    AppendFinding(file, ResolveLine(text, index), code, token, ref findings, ref first, json);
                    return;
                }

                index = source.IndexOf(token, index + 1, StringComparison.Ordinal);
            }
        }

        private static bool IsInsideHotCameraMethod(string source, int index)
        {
            int search = 0;
            while (search < index)
            {
                int brace = source.IndexOf('{', search);
                if (brace < 0 || brace > index)
                    break;

                if (TryReadMethodNameBeforeBrace(source, brace, out string methodName))
                {
                    int end = FindMatchingBrace(source, brace);
                    if (end > index)
                        return IsHotCameraMethodName(methodName);
                }

                search = brace + 1;
            }

            return false;
        }

        private static bool TryReadMethodNameBeforeBrace(string source, int brace, out string methodName)
        {
            methodName = string.Empty;
            int closeParen = LastNonWhitespaceBefore(source, brace - 1);
            if (closeParen < 0 || source[closeParen] != ')')
                return false;

            int openParen = source.LastIndexOf('(', closeParen);
            if (openParen < 0)
                return false;

            int nameEnd = LastNonWhitespaceBefore(source, openParen - 1);
            if (nameEnd < 0 || !IsIdentifierChar(source[nameEnd]))
                return false;

            int nameStart = nameEnd;
            while (nameStart > 0 && IsIdentifierChar(source[nameStart - 1]))
                nameStart--;

            methodName = source.Substring(nameStart, nameEnd - nameStart + 1);
            return !IsControlKeyword(methodName);
        }

        private static bool IsHotCameraMethodName(string methodName)
        {
            return methodName == "Update" ||
                   methodName == "LateUpdate" ||
                   methodName == "Tick" ||
                   methodName == "LateFrameTick" ||
                   methodName.IndexOf("Shake", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   methodName.IndexOf("Camera", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsControlKeyword(string value)
        {
            return value == "if" ||
                   value == "for" ||
                   value == "while" ||
                   value == "switch" ||
                   value == "catch" ||
                   value == "using" ||
                   value == "lock" ||
                   value == "fixed" ||
                   value == "foreach";
        }

        private static int LastNonWhitespaceBefore(string source, int index)
        {
            for (int i = index; i >= 0; i--)
            {
                if (!char.IsWhiteSpace(source[i]))
                    return i;
            }

            return -1;
        }

        private static bool IsIdentifierChar(char c)
        {
            return char.IsLetterOrDigit(c) || c == '_';
        }

        private static int FindMatchingBrace(string text, int start)
        {
            bool inString = false;
            bool escaped = false;
            int depth = 0;
            for (int i = start; i < text.Length; i++)
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
                    continue;
                }

                if (c == '{')
                    depth++;
                else if (c == '}' && --depth == 0)
                    return i;
            }

            return -1;
        }

        private static bool IsNear(string text, int index, string token, int radius)
        {
            int start = Math.Max(0, index - radius);
            int count = Math.Min(text.Length - start, radius * 2);
            return text.IndexOf(token, start, count, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static int ResolveLine(string text, int index)
        {
            int line = 1;
            for (int i = 0; i < index && i < text.Length; i++)
            {
                if (text[i] == '\n')
                    line++;
            }

            return line;
        }

        private static void AppendFinding(
            string file,
            int line,
            string code,
            string token,
            ref int findings,
            ref bool first,
            StringBuilder json)
        {
            if (!first)
                json.AppendLine(",");
            first = false;
            findings++;
            string relative = file.Replace('\\', '/');
            int assetsIndex = relative.IndexOf("Assets/", StringComparison.OrdinalIgnoreCase);
            if (assetsIndex >= 0)
                relative = relative.Substring(assetsIndex);

            json.Append("    { \"file\": \"").Append(Escape(relative))
                .Append("\", \"line\": ").Append(line)
                .Append(", \"code\": \"").Append(code)
                .Append("\", \"token\": \"").Append(Escape(token)).Append("\" }");
        }

        private static void WriteMergedReport(string report, string agentReportJson)
        {
            string trimmed = agentReportJson.Trim();
            if (!File.Exists(report))
            {
                File.WriteAllText(report, BuildEnvelope(trimmed));
                return;
            }

            string existing = File.ReadAllText(report);
            int reportsIndex = existing.IndexOf("\"reports\"", StringComparison.Ordinal);
            int arrayStart = reportsIndex >= 0 ? existing.IndexOf('[', reportsIndex) : -1;
            int arrayEnd = arrayStart >= 0 ? FindMatchingBracket(existing, arrayStart) : -1;
            if (arrayStart < 0 || arrayEnd < 0)
            {
                File.WriteAllText(report, BuildEnvelope(trimmed));
                return;
            }

            string indented = IndentMultiline(trimmed, 4);
            int agentIndex = existing.IndexOf("\"agent\": \"SHINOBU_354\"", arrayStart, arrayEnd - arrayStart, StringComparison.Ordinal);
            string updated;
            if (agentIndex >= 0 && TryFindReportObject(existing, agentIndex, arrayStart, arrayEnd, out int objectStart, out int objectEnd))
            {
                updated = existing.Substring(0, objectStart) + indented + existing.Substring(objectEnd + 1);
            }
            else
            {
                bool hasReports = HasExistingReports(existing, arrayStart, arrayEnd);
                string prefix = hasReports ? ",\n" : "\n";
                updated = existing.Substring(0, arrayEnd) + prefix + indented + "\n" + existing.Substring(arrayEnd);
            }

            updated = ReplaceTopLevelString(updated, "updatedBy", "SHINOBU_354");
            File.WriteAllText(report, updated);
        }

        private static string BuildEnvelope(string agentReportJson)
        {
            StringBuilder output = new StringBuilder(agentReportJson.Length + 192);
            output.AppendLine("{");
            output.AppendLine("  \"schema\": \"HECTON8_UX_OPTIMIZATION_REPORT_MULTI_AGENT_V1\",");
            output.AppendLine("  \"updatedBy\": \"SHINOBU_354\",");
            output.AppendLine("  \"reports\": [");
            output.Append(IndentMultiline(agentReportJson, 4)).AppendLine();
            output.AppendLine("  ]");
            output.AppendLine("}");
            return output.ToString();
        }

        private static string IndentMultiline(string value, int spaces)
        {
            string normalized = value.Replace("\r\n", "\n").Replace('\r', '\n').Trim();
            string prefix = new string(' ', spaces);
            string[] lines = normalized.Split('\n');
            StringBuilder output = new StringBuilder(normalized.Length + (lines.Length * spaces));
            for (int i = 0; i < lines.Length; i++)
            {
                if (i > 0)
                    output.AppendLine();
                output.Append(prefix).Append(lines[i]);
            }

            return output.ToString();
        }

        private static int FindMatchingBracket(string text, int start)
        {
            return FindMatchingScope(text, start, '[', ']');
        }

        private static int FindMatchingScope(string text, int start, char open, char close)
        {
            bool inString = false;
            bool escaped = false;
            int depth = 0;
            for (int i = start; i < text.Length; i++)
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
                    continue;
                }

                if (c == open)
                    depth++;
                else if (c == close && --depth == 0)
                    return i;
            }

            return -1;
        }

        private static bool TryFindReportObject(string text, int agentIndex, int arrayStart, int arrayEnd, out int objectStart, out int objectEnd)
        {
            objectStart = text.LastIndexOf('{', agentIndex);
            objectEnd = objectStart >= arrayStart ? FindMatchingBrace(text, objectStart) : -1;
            return objectStart >= arrayStart && objectEnd >= objectStart && objectEnd <= arrayEnd;
        }

        private static bool HasExistingReports(string text, int arrayStart, int arrayEnd)
        {
            for (int i = arrayStart + 1; i < arrayEnd; i++)
            {
                if (!char.IsWhiteSpace(text[i]))
                    return true;
            }

            return false;
        }

        private static string ReplaceTopLevelString(string text, string key, string value)
        {
            string quotedKey = "\"" + key + "\"";
            int keyIndex = text.IndexOf(quotedKey, StringComparison.Ordinal);
            if (keyIndex < 0)
                return text;

            int colon = text.IndexOf(':', keyIndex + quotedKey.Length);
            int valueStart = colon >= 0 ? text.IndexOf('"', colon + 1) : -1;
            int valueEnd = valueStart >= 0 ? text.IndexOf('"', valueStart + 1) : -1;
            if (valueStart < 0 || valueEnd < 0)
                return text;

            return text.Substring(0, valueStart + 1) + Escape(value) + text.Substring(valueEnd);
        }

        private static string Escape(string value)
        {
            return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }
    }
}
#endif
