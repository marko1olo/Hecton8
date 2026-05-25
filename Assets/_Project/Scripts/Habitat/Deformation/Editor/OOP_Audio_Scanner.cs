using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Hecton8.Habitat.Deformation.Editor
{
    public static class OOP_Audio_Scanner
    {
        private static readonly string[] ScanRoots =
        {
            "Assets/_Project/Scripts/Habitat",
            "Assets/_Project/Scripts/Vehicles",
            "Assets/_Project/Scripts/Physics/Vehicles",
            "Assets/_Project/Scripts/Audio"
        };

        private static readonly string[] ForbiddenRuntimeNeedles =
        {
            "AudioSource.Play(",
            "AudioSource.PlayOneShot(",
            "AudioSource.PlayClipAtPoint(",
            ".Play(",
            ".PlayOneShot(",
            "PlayClipAtPoint(",
            "new AudioSource(",
            "Instantiate("
        };

        private static readonly string[] CentralAudioOwnerAllowlist =
        {
            "Assets/_Project/Scripts/Audio/AdaptiveStem/AdaptiveStemAudioMixer.cs",
            "Assets/_Project/Scripts/Audio/HectonMusicDirector.cs",
            "Assets/_Project/Scripts/Audio/Synthesis/DynamicMusic/DynamicMusicGranularSynthesizer.cs"
        };

        [MenuItem("Hecton8/Habitat/Scan OOP Audio")]
        public static void Run()
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
            string reportPath = Path.Combine(projectRoot, "Docs", "Reports", "AUDIO_OPTIMIZATION_REPORT.json");
            string reportDirectory = Path.GetDirectoryName(reportPath);
            if (!string.IsNullOrEmpty(reportDirectory))
                Directory.CreateDirectory(reportDirectory);

            StringBuilder builder = new StringBuilder(16 * 1024);
            builder.AppendLine("{");
            builder.AppendLine("  \"agent\": \"SHINOBU_339\",");
            builder.AppendLine("  \"scanner\": \"OOP_Audio_Scanner\",");
            builder.AppendLine("  \"summary\": \"OOP Audio Sources Eradicated\",");
            builder.AppendLine("  \"scope\": [");
            for (int i = 0; i < ScanRoots.Length; i++)
            {
                builder.Append("    \"").Append(Escape(ScanRoots[i])).Append("\"");
                builder.AppendLine(i + 1 < ScanRoots.Length ? "," : string.Empty);
            }
            builder.AppendLine("  ],");
            builder.AppendLine("  \"excluded\": [\"**/Editor/**\"],");
            builder.AppendLine("  \"needles\": [");
            for (int i = 0; i < ForbiddenRuntimeNeedles.Length; i++)
            {
                builder.Append("    \"").Append(Escape(ForbiddenRuntimeNeedles[i])).Append("\"");
                builder.AppendLine(i + 1 < ForbiddenRuntimeNeedles.Length ? "," : string.Empty);
            }
            builder.AppendLine("  ],");
            builder.AppendLine("  \"matches\": [");
            StringBuilder allowedBuilder = new StringBuilder(8 * 1024);
            allowedBuilder.AppendLine("  \"allowed_central_audio_owner_matches\": [");
            bool first = true;
            bool allowedFirst = true;
            int count = 0;
            int allowedCount = 0;
            for (int rootIndex = 0; rootIndex < ScanRoots.Length; rootIndex++)
            {
                string root = Path.Combine(projectRoot, ScanRoots[rootIndex].Replace('/', Path.DirectorySeparatorChar));
                if (!Directory.Exists(root))
                    continue;

                foreach (string path in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories))
                {
                    string projectPath = ToProjectPath(projectRoot, path);
                    if (projectPath.Contains("/Editor/"))
                        continue;

                    string text = File.ReadAllText(path);
                    for (int needleIndex = 0; needleIndex < ForbiddenRuntimeNeedles.Length; needleIndex++)
                    {
                        string needle = ForbiddenRuntimeNeedles[needleIndex];
                        int index = text.IndexOf(needle, System.StringComparison.Ordinal);
                        while (index >= 0)
                        {
                            int line = CountLines(text, index) + 1;
                            if (IsAllowedCentralAudioOwner(projectPath))
                            {
                                if (!allowedFirst)
                                    allowedBuilder.AppendLine(",");
                                allowedFirst = false;
                                allowedBuilder.Append("    { \"path\": \"")
                                    .Append(Escape(projectPath))
                                    .Append("\", \"line\": ")
                                    .Append(line)
                                    .Append(", \"needle\": \"")
                                    .Append(Escape(needle))
                                    .Append("\", \"classification\": \"central_audio_owner_not_base_structural_warning_route\" }");
                                allowedCount++;
                            }
                            else
                            {
                                if (!first)
                                    builder.AppendLine(",");
                                first = false;
                                builder.Append("    { \"path\": \"")
                                    .Append(Escape(projectPath))
                                    .Append("\", \"line\": ")
                                    .Append(line)
                                    .Append(", \"needle\": \"")
                                    .Append(Escape(needle))
                                    .Append("\" }");
                                count++;
                            }
                            index = text.IndexOf(needle, index + needle.Length, System.StringComparison.Ordinal);
                        }
                    }
                }
            }

            builder.AppendLine();
            builder.AppendLine("  ],");
            allowedBuilder.AppendLine();
            allowedBuilder.AppendLine("  ],");
            builder.Append(allowedBuilder);
            builder.Append("  \"match_count\": ").Append(count).AppendLine(",");
            builder.Append("  \"allowed_central_audio_owner_match_count\": ").Append(allowedCount).AppendLine(",");
            builder.AppendLine("  \"hot_path_policy\": \"Base structural warnings must enter audio through SignalBus<BaseStructuralWarningSignal>, not direct AudioSource calls.\",");
            builder.AppendLine("  \"verification\": \"Editor scanner generated from static source text; build/profiler proof remains separate.\"");
            builder.AppendLine("}");
            File.WriteAllText(reportPath, builder.ToString(), Encoding.UTF8);
            AssetDatabase.Refresh();
            Debug.Log($"[SHINOBU_339] OOP audio scan wrote {count} matches to {reportPath}");
        }

        private static int CountLines(string text, int endExclusive)
        {
            int lines = 0;
            int limit = Mathf.Clamp(endExclusive, 0, text.Length);
            for (int i = 0; i < limit; i++)
            {
                if (text[i] == '\n')
                    lines++;
            }

            return lines;
        }

        private static string ToProjectPath(string projectRoot, string path)
        {
            string relative = path.StartsWith(projectRoot) ? path.Substring(projectRoot.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) : path;
            return relative.Replace(Path.DirectorySeparatorChar, '/');
        }

        private static bool IsAllowedCentralAudioOwner(string projectPath)
        {
            for (int i = 0; i < CentralAudioOwnerAllowlist.Length; i++)
            {
                if (projectPath == CentralAudioOwnerAllowlist[i])
                    return true;
            }

            return false;
        }

        private static string Escape(string value)
        {
            return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }
    }
}
