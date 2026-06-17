using System;
using System.Globalization;
using System.IO;
using System.Text;
using Hecton8.World.OfflineHadalTrenchBaker;
using UnityEditor;
using UnityEngine;

namespace Hecton8.World.OfflineHadalTrenchBaker.Editor
{
    public static class Manual_Trench_Scanner
    {
        private const string EnvironmentPath = "Assets/_Project/Prefabs/Environment";
        private const string ProjectPath = "Assets/_Project";
        private const string SharedReportPath = "Docs/Reports/WORLD_OPTIMIZATION_REPORT.json";
        private const string AgentReportPath = "Docs/Reports/WORLD_OPTIMIZATION_REPORT_SHINOBU_241.json";

        [MenuItem("Hecton8/Hadal Trench Forge/Scan Manual Trench Geometry")]
        public static void ScanAndReportMenu()
        {
            Scan(deleteForbiddenAssets: false, logToConsole: true);
        }

        [MenuItem("Hecton8/Hadal Trench Forge/Scan And Delete Manual Trench Geometry")]
        public static void ScanAndDeleteMenu()
        {
            Scan(deleteForbiddenAssets: true, logToConsole: true);
        }

        public static int Scan(bool deleteForbiddenAssets, bool logToConsole)
        {
            Directory.CreateDirectory("Docs/Reports");
            int forbiddenCount = 0;
            int deletedCount = 0;
            long bytes = 0L;
            StringBuilder assets = new StringBuilder(2048);
            ScanDirectory(EnvironmentPath, true, deleteForbiddenAssets, ref forbiddenCount, ref deletedCount, ref bytes, assets);
            if (!Directory.Exists(EnvironmentPath))
                ScanDirectory(ProjectPath, false, deleteForbiddenAssets, ref forbiddenCount, ref deletedCount, ref bytes, assets);

            string status = forbiddenCount == 0 ? "Manual Geometry Eradicated" : "Manual Geometry Debt Found";
            StringBuilder json = new StringBuilder(4096);
            json.Append("{\n");
            json.Append("  \"version\": ").Append(HadalTrenchBakeConstants.ReportVersion).Append(",\n");
            json.Append("  \"agent\": \"SHINOBU_241\",\n");
            json.Append("  \"scanner\": \"Manual_Trench_Scanner\",\n");
            json.Append("  \"status\": \"").Append(status).Append("\",\n");
            json.Append("  \"environmentPathExists\": ").Append(Directory.Exists(EnvironmentPath) ? "true" : "false").Append(",\n");
            json.Append("  \"forbiddenAssetsFound\": ").Append(forbiddenCount).Append(",\n");
            json.Append("  \"deletedAssets\": ").Append(deletedCount).Append(",\n");
            json.Append("  \"estimatedForbiddenBytes\": ").Append(bytes).Append(",\n");
            json.Append("  \"scanUtc\": \"").Append(DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture)).Append("\",\n");
            json.Append("  \"assets\": [\n");
            json.Append(assets);
            json.Append("  ]\n");
            json.Append("}\n");
            File.WriteAllText(SharedReportPath, json.ToString());
            File.WriteAllText(AgentReportPath, json.ToString());
            AssetDatabase.Refresh();
            if (logToConsole)
                Debug.Log("[SHINOBU_241] " + status + ". Forbidden assets: " + forbiddenCount + ". Deleted: " + deletedCount + ".");
            return forbiddenCount;
        }

        private static void ScanDirectory(
            string root,
            bool strictEnvironmentScope,
            bool deleteForbiddenAssets,
            ref int forbiddenCount,
            ref int deletedCount,
            ref long bytes,
            StringBuilder assets)
        {
            if (!Directory.Exists(root))
                return;

            foreach (string discoveredFile in Directory.EnumerateFiles(root, "*.*", SearchOption.AllDirectories))
            {
                string path = discoveredFile.Replace('\\', '/');
                if (!IsForbiddenAsset(path, strictEnvironmentScope))
                    continue;

                if (forbiddenCount > 0)
                    assets.Append(",\n");
                long size = ResolveSize(path);
                bytes += size;
                assets.Append("    { \"path\": \"").Append(EscapeJson(path)).Append("\", \"bytes\": ").Append(size).Append(" }");
                forbiddenCount++;
                if (!deleteForbiddenAssets)
                    continue;

                if (AssetDatabase.DeleteAsset(path))
                    deletedCount++;
            }
        }

        private static bool IsForbiddenAsset(string path, bool strictEnvironmentScope)
        {
            string extension = Path.GetExtension(path);
            if (!extension.Equals(".fbx", StringComparison.OrdinalIgnoreCase) &&
                !extension.Equals(".prefab", StringComparison.OrdinalIgnoreCase))
                return false;

            string file = Path.GetFileNameWithoutExtension(path);
            if (ContainsToken(file, "trench") ||
                ContainsToken(file, "canyon") ||
                ContainsToken(file, "fault") ||
                ContainsToken(file, "chasm"))
            {
                return true;
            }

            if (strictEnvironmentScope)
                return ContainsToken(file, "abyss") || ContainsToken(file, "rift");

            return extension.Equals(".fbx", StringComparison.OrdinalIgnoreCase) &&
                   (ContainsToken(file, "abyss") || ContainsToken(file, "rift"));
        }

        private static bool ContainsToken(string value, string token)
        {
            return value.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static long ResolveSize(string path)
        {
            try
            {
                FileInfo info = new FileInfo(path);
                return info.Exists ? info.Length : 0L;
            }
            catch (IOException)
            {
                return 0L;
            }
            catch (UnauthorizedAccessException)
            {
                return 0L;
            }
        }

        private static string EscapeJson(string value)
        {
            StringBuilder builder = new StringBuilder(value.Length + 8);
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (c == '\\' || c == '"')
                    builder.Append('\\').Append(c);
                else
                    builder.Append(c);
            }

            return builder.ToString();
        }
    }
}
