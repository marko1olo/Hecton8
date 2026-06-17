#if UNITY_EDITOR
using System.IO;
using System.Text;
using UnityEditor;

namespace Hecton8.World.BiomeWeightMapBaker.Editor
{
    public static class Terrain_Shader_Scanner
    {
        private const string ReportPath = "Docs/Reports/RENDERING_OPTIMIZATION_REPORT.json";

        [MenuItem("Hecton8/Biome Splatmap Forge/Run Terrain Shader Scanner")]
        public static void RunAndWriteReport()
        {
            Directory.CreateDirectory("Docs/Reports");
            int scanned = 0;
            int offenders = 0;
            int estimatedAluOps = 0;
            StringBuilder files = new StringBuilder(2048);
            files.Append("[");
            ScanDirectory("Assets/_Project/Shaders", ref scanned, ref offenders, ref estimatedAluOps, files);
            ScanDirectory("Assets/_Project/Art/Shaders", ref scanned, ref offenders, ref estimatedAluOps, files);
            ScanDirectory("Assets/_Project/Scripts/Environment", ref scanned, ref offenders, ref estimatedAluOps, files);
            files.Append("]");

            StringBuilder report = new StringBuilder(3072);
            report.Append("{\n");
            report.Append("  \"schema\": \"hecton8.rendering_optimization_report.v1\",\n");
            report.Append("  \"agent\": \"SHINOBU_243\",\n");
            report.Append("  \"scanner\": \"Terrain_Shader_Scanner\",\n");
            report.Append("  \"shaderFilesScanned\": ").Append(scanned).Append(",\n");
            report.Append("  \"runtimeSplatMathOffenders\": ").Append(offenders).Append(",\n");
            report.Append("  \"estimatedSplatAluOpsRemovedOrFlagged\": ").Append(estimatedAluOps).Append(",\n");
            report.Append("  \"status\": \"").Append(offenders == 0 ? "Runtime Splat Math Eradicated" : "Runtime Splat Math Still Present").Append("\",\n");
            report.Append("  \"offenders\": ").Append(files).Append('\n');
            report.Append("}\n");
            File.WriteAllText(ReportPath, report.ToString(), new UTF8Encoding(false));
            UnityEngine.Debug.Log("[SHINOBU_243] Terrain shader scanner wrote " + ReportPath + " offenders=" + offenders);
        }

        private static void ScanDirectory(
            string directory,
            ref int scanned,
            ref int offenders,
            ref int estimatedAluOps,
            StringBuilder files)
        {
            if (!Directory.Exists(directory))
                return;

            foreach (string shaderFile in Directory.EnumerateFiles(directory, "*.*", SearchOption.AllDirectories))
            {
                string path = shaderFile.Replace('\\', '/');
                if (!IsShaderLike(path))
                    continue;

                scanned++;
                int fileAlu = EstimateForbiddenSplatAlu(path);
                if (fileAlu <= 0)
                    continue;

                if (offenders > 0)
                    files.Append(", ");

                files.Append("{ \"path\": \"").Append(path).Append("\", \"estimatedAluOps\": ").Append(fileAlu).Append(" }");
                offenders++;
                estimatedAluOps += fileAlu;
            }
        }

        private static bool IsShaderLike(string path)
        {
            return EndsWithAscii(path, ".shader") || EndsWithAscii(path, ".hlsl") || EndsWithAscii(path, ".compute");
        }

        private static int EstimateForbiddenSplatAlu(string path)
        {
            if (!File.Exists(path))
                return 0;

            int dotInNormalMatch = 0;
            int dotNormalMatch = 0;
            int dotNormalUpMatch = 0;
            int ndotUpMatch = 0;
            int rockWeightMatch = 0;
            int sandWeightMatch = 0;
            int siltWeightMatch = 0;
            int worldHeightMatch = 0;
            int height01Match = 0;
            int normalizedHeightMatch = 0;
            int slopeBlendMatch = 0;
            int slopeSharpnessMatch = 0;
            bool dotInNormalFound = false;
            bool dotNormalFound = false;
            bool dotNormalUpFound = false;
            bool ndotUpFound = false;
            bool rockWeightFound = false;
            bool sandWeightFound = false;
            bool siltWeightFound = false;
            bool worldHeightFound = false;
            bool height01Found = false;
            bool normalizedHeightFound = false;
            bool slopeBlendFound = false;
            bool slopeSharpnessFound = false;

            using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                while (true)
                {
                    int read = stream.ReadByte();
                    if (read < 0)
                        break;

                    byte value = ToLowerAscii((byte)read);
                    AdvancePattern(value, "dot(IN.normalWS, float3(0.0, 1.0, 0.0))", ref dotInNormalMatch, ref dotInNormalFound);
                    AdvancePattern(value, "dot(normalWS, float3(0.0, 1.0, 0.0))", ref dotNormalMatch, ref dotNormalFound);
                    AdvancePattern(value, "dot(normal, up)", ref dotNormalUpMatch, ref dotNormalUpFound);
                    AdvancePattern(value, "NdotUp", ref ndotUpMatch, ref ndotUpFound);
                    AdvancePattern(value, "rockWeight", ref rockWeightMatch, ref rockWeightFound);
                    AdvancePattern(value, "sandWeight", ref sandWeightMatch, ref sandWeightFound);
                    AdvancePattern(value, "siltWeight", ref siltWeightMatch, ref siltWeightFound);
                    AdvancePattern(value, "worldHeight", ref worldHeightMatch, ref worldHeightFound);
                    AdvancePattern(value, "height01", ref height01Match, ref height01Found);
                    AdvancePattern(value, "normalizedHeight", ref normalizedHeightMatch, ref normalizedHeightFound);
                    AdvancePattern(value, "slopeBlend", ref slopeBlendMatch, ref slopeBlendFound);
                    AdvancePattern(value, "SlopeSharpness", ref slopeSharpnessMatch, ref slopeSharpnessFound);
                }
            }

            int score = 0;
            bool fragmentNormalUp = dotInNormalFound || dotNormalFound || dotNormalUpFound || ndotUpFound;
            bool materialWeights = rockWeightFound || sandWeightFound || siltWeightFound;
            bool heightTerms = worldHeightFound || height01Found || normalizedHeightFound;
            bool slopeTerms = slopeBlendFound || slopeSharpnessFound;

            if (fragmentNormalUp && materialWeights)
            {
                score += 6;
            }

            if (slopeTerms && materialWeights)
            {
                score += 18;
            }

            if (heightTerms && materialWeights)
                score += 8;

            return score;
        }

        private static bool EndsWithAscii(string value, string suffix)
        {
            if (string.IsNullOrEmpty(value) || string.IsNullOrEmpty(suffix) || value.Length < suffix.Length)
                return false;

            int start = value.Length - suffix.Length;
            for (int i = 0; i < suffix.Length; i++)
            {
                if (ToLowerAscii((byte)value[start + i]) != ToLowerAscii((byte)suffix[i]))
                    return false;
            }

            return true;
        }

        private static void AdvancePattern(byte value, string pattern, ref int matched, ref bool found)
        {
            if (found || string.IsNullOrEmpty(pattern))
                return;

            byte expected = ToLowerAscii((byte)pattern[matched]);
            if (value == expected)
            {
                matched++;
                found = matched == pattern.Length;
                return;
            }

            matched = value == ToLowerAscii((byte)pattern[0]) ? 1 : 0;
        }

        private static byte ToLowerAscii(byte value)
        {
            return value >= (byte)'A' && value <= (byte)'Z' ? (byte)(value + 32) : value;
        }
    }
}
#endif
