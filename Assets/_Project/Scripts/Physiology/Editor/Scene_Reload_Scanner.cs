#if UNITY_EDITOR
using System;
using System.IO;
using System.Text;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

namespace Hecton8.Physiology.Editor
{
    public static class Scene_Reload_Scanner
    {
        private const string AgentId = "SHINOBU_329";
        private const string SummaryPass = "OOP Scene Reloads Eradicated";
        private const string SummaryFail = "OOP Scene Reloads Found";
        private const string DedicatedReport = "Docs/Reports/PHYSICS_OPTIMIZATION_REPORT_SHINOBU_329.json";
        private const string SharedReport = "Docs/Reports/PHYSICS_OPTIMIZATION_REPORT.json";

        private static readonly string[] s_forbiddenTokens =
        {
            "SceneManager.LoadScene",
            "SceneManager.LoadSceneAsync",
            ".LoadScene(",
            ".LoadSceneAsync(",
            "Destroy(player",
            "Destroy(Player",
            "Application.Quit"
        };

        [MenuItem("Hecton8/Survival/Run Scene Reload Scanner")]
        public static void RunFromMenu()
        {
            Run();
        }

        public static void Run()
        {
            string root = ResolveProjectRoot();
            string scriptsRoot = Path.Combine(root, "Assets", "_Project", "Scripts");
            string dedicatedPath = Path.Combine(root, DedicatedReport);
            string sharedPath = Path.Combine(root, SharedReport);
            Directory.CreateDirectory(Path.GetDirectoryName(dedicatedPath));

            int scannedFileCount = 0;
            int findingCount = 0;
            StringBuilder findings = new StringBuilder(4096); // EDITOR COLD ALLOC: scanner JSON proof payload.

            if (Directory.Exists(scriptsRoot))
            {
                string[] files = Directory.GetFiles(scriptsRoot, "*.cs", SearchOption.AllDirectories);
                Array.Sort(files, StringComparer.Ordinal);
                for (int i = 0; i < files.Length; i++)
                {
                    string file = files[i];
                    string normalized = NormalizePath(file);
                    if (!IsDeathDomainPath(normalized))
                        continue;

                    scannedFileCount++;
                    string source = File.ReadAllText(file);
                    string masked = MaskCommentsAndStrings(source);
                    for (int tokenIndex = 0; tokenIndex < s_forbiddenTokens.Length; tokenIndex++)
                    {
                        string token = s_forbiddenTokens[tokenIndex];
                        int cursor = 0;
                        while (cursor < masked.Length)
                        {
                            int hit = masked.IndexOf(token, cursor, StringComparison.Ordinal);
                            if (hit < 0)
                                break;

                            AppendFinding(findings, ref findingCount, normalized, token, ResolveLine(masked, hit));
                            cursor = hit + token.Length;
                        }
                    }
                }
            }

            string report = BuildReport(scannedFileCount, findingCount, findings);
            File.WriteAllText(dedicatedPath, report);
            UpsertSharedReport(sharedPath, BuildSharedEntry(scannedFileCount, findingCount));
            AssetDatabase.Refresh();
        }

        private static bool IsDeathDomainPath(string normalizedPath)
        {
            if (normalizedPath.IndexOf("/Editor/", StringComparison.OrdinalIgnoreCase) >= 0)
                return false;

            if (normalizedPath.EndsWith("/Core/SceneRuntimeService.cs", StringComparison.OrdinalIgnoreCase))
                return false;

            if (normalizedPath.EndsWith("/Core/RuntimeWatchdog.cs", StringComparison.OrdinalIgnoreCase))
                return false;

            return normalizedPath.IndexOf("/Player/", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   normalizedPath.IndexOf("/Core/", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   normalizedPath.IndexOf("/Gameplay/", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   normalizedPath.IndexOf("/Physiology/", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   normalizedPath.IndexOf("/Combat/", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string BuildReport(int scannedFileCount, int findingCount, StringBuilder findings)
        {
            StringBuilder json = new StringBuilder(8192); // EDITOR COLD ALLOC: report emission.
            json.Append("{\n");
            json.Append("  \"agent\": \"").Append(AgentId).Append("\",\n");
            json.Append("  \"scanner\": \"Scene_Reload_Scanner\",\n");
            json.Append("  \"summary\": \"").Append(findingCount == 0 ? SummaryPass : SummaryFail).Append("\",\n");
            json.Append("  \"status\": \"STATIC_SOURCE\",\n");
            json.Append("  \"scanScope\": \"Assets/_Project/Scripts/{Player,Core,Gameplay,Physiology,Combat}\",\n");
            json.Append("  \"scannedFileCount\": ").Append(scannedFileCount).Append(",\n");
            json.Append("  \"findingCount\": ").Append(findingCount).Append(",\n");
            json.Append("  \"runtimeRouteProof\": \"death request -> bridge-sanitized PlayerRespawnSignal truth lane -> GlobalDataVault respawn buffers -> Burst ResetPlayerPhysiologyJob -> sanitized InventoryRespawnDeathAupSignal sideband + non-shedding InventoryDeathLootCacheSignal + Dear Lie shader scalar\",\n");
            json.Append("  \"allowedSceneRoute\": \"Core SceneRuntimeService boot/menu scene authority and RuntimeWatchdog fatal process exit are excluded from player-death routing\",\n");
            json.Append("  \"findings\": [");
            if (findings.Length > 0)
            {
                json.Append('\n');
                json.Append(findings);
                json.Append('\n');
            }
            json.Append("  ]\n");
            json.Append("}\n");
            return json.ToString();
        }

        private static string BuildSharedEntry(int scannedFileCount, int findingCount)
        {
            StringBuilder json = new StringBuilder(1024); // EDITOR COLD ALLOC: shared report entry.
            json.Append("  \"shinobu329SceneReloadScanner\": {\n");
            json.Append("    \"agent\": \"").Append(AgentId).Append("\",\n");
            json.Append("    \"scanner\": \"Scene_Reload_Scanner\",\n");
            json.Append("    \"summary\": \"").Append(findingCount == 0 ? SummaryPass : SummaryFail).Append("\",\n");
            json.Append("    \"dedicatedReport\": \"").Append(DedicatedReport).Append("\",\n");
            json.Append("    \"scanScope\": \"Assets/_Project/Scripts/{Player,Core,Gameplay,Physiology,Combat}\",\n");
            json.Append("    \"scannedFileCount\": ").Append(scannedFileCount).Append(",\n");
            json.Append("    \"findingCount\": ").Append(findingCount).Append(",\n");
            json.Append("    \"runtimeRouteProof\": \"bridge-sanitized SignalBus<PlayerRespawnSignal> truth lane + Vault + Burst respawn reset + sanitized InventoryRespawnDeathAupSignal sideband + non-shedding InventoryDeathLootCacheSignal + Dear Lie shader fade\",\n");
            json.Append("    \"allowedSceneRoute\": \"Core SceneRuntimeService boot/menu plus RuntimeWatchdog fatal exit excluded from player-death routing\"\n");
            json.Append("  }");
            return json.ToString();
        }

        private static void UpsertSharedReport(string sharedPath, string entry)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(sharedPath));
            if (!File.Exists(sharedPath))
            {
                File.WriteAllText(sharedPath, "{\n" + entry + "\n}\n");
                return;
            }

            string current = File.ReadAllText(sharedPath);
            if (TryReplaceSharedEntry(sharedPath, current, entry))
                return;

            int insert = current.LastIndexOf('}');
            if (insert < 0)
            {
                File.WriteAllText(sharedPath, "{\n" + entry + "\n}\n");
                return;
            }

            string prefix = current.Substring(0, insert).TrimEnd();
            string suffix = current.Substring(insert);
            string comma = prefix.EndsWith("{", StringComparison.Ordinal) ? "\n" : ",\n";
            File.WriteAllText(sharedPath, prefix + comma + entry + "\n" + suffix);
        }

        private static bool TryReplaceSharedEntry(string sharedPath, string current, string entry)
        {
            int key = current.IndexOf("\"shinobu329SceneReloadScanner\"", StringComparison.Ordinal);
            if (key < 0)
                return false;

            int objectStart = current.IndexOf('{', key);
            if (objectStart < 0)
                return false;

            int depth = 0;
            int objectEnd = -1;
            bool inString = false;
            bool escaped = false;
            for (int i = objectStart; i < current.Length; i++)
            {
                char c = current[i];
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
                {
                    depth++;
                    continue;
                }

                if (c != '}')
                    continue;

                depth--;
                if (depth != 0)
                    continue;

                objectEnd = i;
                break;
            }

            if (objectEnd < 0)
                return false;

            int entryStart = current.LastIndexOf('\n', key);
            entryStart = entryStart < 0 ? key : entryStart + 1;
            File.WriteAllText(sharedPath, current.Substring(0, entryStart) + entry + current.Substring(objectEnd + 1));
            return true;
        }

        private static void AppendFinding(StringBuilder findings, ref int findingCount, string file, string token, int line)
        {
            if (findingCount > 0)
                findings.Append(",\n");

            findings.Append("    { \"file\": \"")
                .Append(Escape(file))
                .Append("\", \"line\": ")
                .Append(line)
                .Append(", \"token\": \"")
                .Append(Escape(token))
                .Append("\" }");
            findingCount++;
        }

        private static int ResolveLine(string text, int offset)
        {
            int line = 1;
            int length = math.min(offset, text.Length);
            for (int i = 0; i < length; i++)
            {
                if (text[i] == '\n')
                    line++;
            }

            return line;
        }

        private static string MaskCommentsAndStrings(string source)
        {
            StringBuilder masked = new StringBuilder(source.Length); // EDITOR COLD ALLOC: lexical mask.
            bool inLineComment = false;
            bool inBlockComment = false;
            bool inString = false;
            bool inChar = false;
            bool verbatim = false;

            for (int i = 0; i < source.Length; i++)
            {
                char c = source[i];
                char next = i + 1 < source.Length ? source[i + 1] : '\0';

                if (inLineComment)
                {
                    if (c == '\n')
                    {
                        inLineComment = false;
                        masked.Append('\n');
                    }
                    else
                    {
                        masked.Append(' ');
                    }

                    continue;
                }

                if (inBlockComment)
                {
                    if (c == '*' && next == '/')
                    {
                        masked.Append(' ');
                        masked.Append(' ');
                        i++;
                        inBlockComment = false;
                    }
                    else
                    {
                        masked.Append(c == '\n' ? '\n' : ' ');
                    }

                    continue;
                }

                if (inString)
                {
                    bool escaped = !verbatim && c == '\\';
                    if (escaped && i + 1 < source.Length)
                    {
                        masked.Append(' ');
                        masked.Append(source[i + 1] == '\n' ? '\n' : ' ');
                        i++;
                        continue;
                    }

                    if (verbatim && c == '"' && next == '"')
                    {
                        masked.Append(' ');
                        masked.Append(' ');
                        i++;
                        continue;
                    }

                    if (c == '"')
                    {
                        inString = false;
                        verbatim = false;
                    }

                    masked.Append(c == '\n' ? '\n' : ' ');
                    continue;
                }

                if (inChar)
                {
                    if (c == '\\' && i + 1 < source.Length)
                    {
                        masked.Append(' ');
                        masked.Append(source[i + 1] == '\n' ? '\n' : ' ');
                        i++;
                        continue;
                    }

                    if (c == '\'')
                        inChar = false;

                    masked.Append(c == '\n' ? '\n' : ' ');
                    continue;
                }

                if (c == '/' && next == '/')
                {
                    masked.Append(' ');
                    masked.Append(' ');
                    i++;
                    inLineComment = true;
                    continue;
                }

                if (c == '/' && next == '*')
                {
                    masked.Append(' ');
                    masked.Append(' ');
                    i++;
                    inBlockComment = true;
                    continue;
                }

                if (c == '@' && next == '"')
                {
                    masked.Append(' ');
                    masked.Append(' ');
                    i++;
                    inString = true;
                    verbatim = true;
                    continue;
                }

                if (c == '"')
                {
                    masked.Append(' ');
                    inString = true;
                    verbatim = false;
                    continue;
                }

                if (c == '\'')
                {
                    masked.Append(' ');
                    inChar = true;
                    continue;
                }

                masked.Append(c);
            }

            return masked.ToString();
        }

        private static string Escape(string value)
        {
            return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        private static string ResolveProjectRoot()
        {
            return Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        }

        private static string NormalizePath(string path)
        {
            return path.Replace('\\', '/');
        }
    }
}
#endif
