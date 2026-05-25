#if UNITY_EDITOR
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Hecton8.Editor.HydraulicErosionForge
{
    internal static class Terrain_Runtime_Scanner_Erosion
    {
        private static readonly string[] ForbiddenPatterns =
        {
            "SetHeights(",
            "SetHeightsDelayLOD",
            "GetHeights(",
            "class WaterDrop",
            "class Droplet",
            "List<WaterDrop",
            "List<Droplet",
            "HydraulicErosionJob"
        };

        [MenuItem("HECTON-8/Hydraulic Erosion Forge/Scan Runtime Erosion", false, 191)]
        public static void ScanMenu()
        {
            ScanAndWriteReport(out int hitCount);
            Debug.Log("[SHINOBU_242] Runtime erosion scan wrote " + HydraulicErosionForgeConstants.RuntimeScannerReportPath + " hits=" + hitCount + ".");
        }

        public static void ScanAndWriteReport(out int hitCount)
        {
            hitCount = 0;
            string root = "Assets/_Project/Scripts";
            StringBuilder hitBuilder = new StringBuilder(4096);
            if (Directory.Exists(root))
            {
                foreach (string file in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories))
                {
                    string path = file.Replace('\\', '/');
                    if (path.Contains("/Editor/"))
                        continue;

                    ScanFile(path, hitBuilder, ref hitCount);
                }
            }

            WriteReport(hitBuilder, hitCount);
            AssetDatabase.Refresh();
        }

        private static void ScanFile(string path, StringBuilder hitBuilder, ref int hitCount)
        {
            using (StreamReader reader = new StreamReader(path))
            {
                int lineNumber = 0;
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    lineNumber++;
                    for (int i = 0; i < ForbiddenPatterns.Length; i++)
                    {
                        string pattern = ForbiddenPatterns[i];
                        if (line.IndexOf(pattern, System.StringComparison.Ordinal) < 0)
                            continue;

                        if (hitCount > 0)
                            hitBuilder.Append(",\n");
                        hitBuilder.Append("    { \"path\": \"");
                        hitBuilder.Append(Escape(path));
                        hitBuilder.Append("\", \"line\": ");
                        hitBuilder.Append(lineNumber);
                        hitBuilder.Append(", \"pattern\": \"");
                        hitBuilder.Append(Escape(pattern));
                        hitBuilder.Append("\" }");
                        hitCount++;
                    }
                }
            }
        }

        private static void WriteReport(StringBuilder hits, int hitCount)
        {
            string path = HydraulicErosionForgeConstants.RuntimeScannerReportPath;
            string folder = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(folder) && !Directory.Exists(folder))
                Directory.CreateDirectory(folder);

            StringBuilder builder = new StringBuilder(4096);
            builder.Append("{\n");
            builder.Append("  \"agent\": \"SHINOBU_242\",\n");
            builder.Append("  \"status\": \"PENDING_VERIFICATION\",\n");
            builder.Append("  \"summary\": \"");
            builder.Append(hitCount == 0 ? "Runtime Erosion Calculations Eradicated" : "Runtime Erosion Calculations Still Present");
            builder.Append("\",\n");
            builder.Append("  \"hitCount\": ").Append(hitCount).Append(",\n");
            builder.Append("  \"warningFlags\": ").Append(hitCount == 0 ? 0u : HydraulicErosionForgeConstants.WarningScannerRuntimeTerrainMutation).Append(",\n");
            builder.Append("  \"hits\": [\n");
            if (hits.Length > 0)
            {
                builder.Append(hits);
                builder.Append('\n');
            }

            builder.Append("  ]\n");
            builder.Append("}\n");
            WriteAtomicText(path, builder.ToString());
        }

        private static string Escape(string value)
        {
            return string.IsNullOrEmpty(value) ? string.Empty : value.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        private static void WriteAtomicText(string path, string contents)
        {
            string temp = path + ".tmp";
            string backup = path + ".bak";
            if (File.Exists(temp))
                File.Delete(temp);

            try
            {
                File.WriteAllText(temp, contents);
                if (File.Exists(path))
                {
                    if (File.Exists(backup))
                        File.Delete(backup);
                    File.Replace(temp, path, backup);
                }
                else
                {
                    File.Move(temp, path);
                }
            }
            catch
            {
                if (File.Exists(temp))
                    File.Delete(temp);
                throw;
            }
        }
    }
}
#endif
