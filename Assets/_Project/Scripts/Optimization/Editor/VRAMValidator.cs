using System;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Profiling;

namespace Hecton8.Optimization.Editor
{
    /// <summary>
    /// Build gate for the MX350 texture memory ceiling.
    /// </summary>
    public sealed class VRAMValidator : IPreprocessBuildWithReport
    {
        private const long TextureBudgetBytes = 900L * 1024L * 1024L;
        private const string ProjectAssetRoot = "Assets/_Project";
        private const string WorldScenePath = "Assets/_Project/Scenes/02_HECTON_WORLD.unity";
        private const string WorldSceneReportPath = "Library/Hecton8/world_scene_texture_budget_report.csv";
        private const int MaxSceneReportRows = 256;

        // COLD ALLOC: string[1] — editor asset root filter — owner: VRAMValidator
        private static readonly string[] s_projectRoots = { ProjectAssetRoot };
        // COLD ALLOC: object[1] — reflection scratch args for texture size calls — owner: VRAMValidator
        private static readonly object[] s_textureUtilArgs = new object[1];
        private static readonly Type s_textureUtilType = Type.GetType("UnityEditor.TextureUtil,UnityEditor");
        private static readonly MethodInfo s_storageMemoryLongMethod = ResolveStorageMemoryMethod();
        private static readonly MethodInfo s_storageMemoryIntMethod = ResolveStorageMemoryIntMethod();

        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report)
        {
            ValidateOrThrow();
            ValidateWorldSceneTextureBudgetOrThrow();
        }

        [MenuItem("HECTON-8/Validation/Run VRAM Validator")]
        public static void RunFromMenu()
        {
            ValidateOrThrow();
            Debug.Log("[VRAMValidator] Texture budget gate passed.");
        }

        [MenuItem("HECTON-8/Validation/Run 02_HECTON_WORLD Texture Budget Gate")]
        public static void RunWorldSceneFromMenu()
        {
            SceneTextureBudgetResult result = ValidateWorldSceneTextureBudgetOrThrow();
            Debug.Log(
                "[VRAMValidator] 02_HECTON_WORLD texture budget passed. Total=" +
                BytesToMegabytes(result.TotalBytes).ToString("F2", CultureInfo.InvariantCulture) +
                "MB Textures=" +
                result.TextureCount.ToString(CultureInfo.InvariantCulture) +
                " Report=" +
                result.ReportPath);
        }

        public static long ValidateOrThrow()
        {
            string[] textureGuids = AssetDatabase.FindAssets("t:Texture", s_projectRoots);
            long totalBytes = 0L;
            long largestBytes = 0L;
            string largestPath = string.Empty;

            for (int i = 0; i < textureGuids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(textureGuids[i]);
                Texture texture = AssetDatabase.LoadAssetAtPath<Texture>(path);
                if (texture == null)
                    continue;

                long textureBytes = GetTextureStorageBytes(texture);
                if (textureBytes <= 0L)
                    continue;

                totalBytes += textureBytes;
                if (textureBytes > largestBytes)
                {
                    largestBytes = textureBytes;
                    largestPath = path;
                }
            }

            if (totalBytes > TextureBudgetBytes)
            {
                string message =
                    "[VRAMValidator] Texture budget exceeded. Total=" +
                    BytesToMegabytes(totalBytes).ToString("F2", CultureInfo.InvariantCulture) +
                    "MB Budget=" +
                    BytesToMegabytes(TextureBudgetBytes).ToString("F2", CultureInfo.InvariantCulture) +
                    "MB Largest=" +
                    BytesToMegabytes(largestBytes).ToString("F2", CultureInfo.InvariantCulture) +
                    "MB Path=" +
                    largestPath;
                throw new BuildFailedException(message);
            }

            return totalBytes;
        }

        public static SceneTextureBudgetResult ValidateWorldSceneTextureBudgetOrThrow()
        {
            SceneAsset sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(WorldScenePath);
            if (sceneAsset == null)
                throw new BuildFailedException("[VRAMValidator] 02_HECTON_WORLD scene not found at " + WorldScenePath);

            string[] dependencies = AssetDatabase.GetDependencies(WorldScenePath, true);
            long totalBytes = 0L;
            long largestBytes = 0L;
            int textureCount = 0;
            int reportRows = 0;
            string largestPath = string.Empty;
            StringBuilder report = new StringBuilder(16384);
            report.AppendLine("asset_path,storage_bytes,storage_mb,width,height");

            for (int i = 0; i < dependencies.Length; i++)
            {
                string path = dependencies[i];
                if (string.IsNullOrEmpty(path))
                    continue;

                Texture texture = AssetDatabase.LoadAssetAtPath<Texture>(path);
                if (texture == null)
                    continue;

                long textureBytes = GetTextureStorageBytes(texture);
                if (textureBytes <= 0L)
                    continue;

                totalBytes += textureBytes;
                textureCount++;
                if (textureBytes > largestBytes)
                {
                    largestBytes = textureBytes;
                    largestPath = path;
                }

                if (reportRows < MaxSceneReportRows)
                {
                    AppendTextureReportRow(report, path, texture, textureBytes);
                    reportRows++;
                }
            }

            WriteReport(WorldSceneReportPath, report);

            if (totalBytes > TextureBudgetBytes)
            {
                string message =
                    "[VRAMValidator] 02_HECTON_WORLD texture budget exceeded. Total=" +
                    BytesToMegabytes(totalBytes).ToString("F2", CultureInfo.InvariantCulture) +
                    "MB Budget=" +
                    BytesToMegabytes(TextureBudgetBytes).ToString("F2", CultureInfo.InvariantCulture) +
                    "MB Textures=" +
                    textureCount.ToString(CultureInfo.InvariantCulture) +
                    " Largest=" +
                    BytesToMegabytes(largestBytes).ToString("F2", CultureInfo.InvariantCulture) +
                    "MB Path=" +
                    largestPath +
                    " Report=" +
                    WorldSceneReportPath;
                throw new BuildFailedException(message);
            }

            return new SceneTextureBudgetResult(
                totalBytes,
                textureCount,
                largestBytes,
                largestPath,
                WorldSceneReportPath);
        }

        private static long GetTextureStorageBytes(Texture texture)
        {
            if (texture == null)
                return 0L;

            if (s_storageMemoryLongMethod != null)
            {
                try
                {
                    s_textureUtilArgs[0] = texture;
                    object value = s_storageMemoryLongMethod.Invoke(null, s_textureUtilArgs);
                    if (value is long longValue)
                        return longValue;
                }
                finally
                {
                    s_textureUtilArgs[0] = null;
                }
            }

            if (s_storageMemoryIntMethod != null)
            {
                try
                {
                    s_textureUtilArgs[0] = texture;
                    object value = s_storageMemoryIntMethod.Invoke(null, s_textureUtilArgs);
                    if (value is int intValue)
                        return intValue;
                }
                finally
                {
                    s_textureUtilArgs[0] = null;
                }
            }

            return Profiler.GetRuntimeMemorySizeLong(texture);
        }

        private static MethodInfo ResolveStorageMemoryMethod()
        {
            if (s_textureUtilType == null)
                return null;

            return s_textureUtilType.GetMethod(
                "GetStorageMemorySizeLong",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic,
                null,
                new[] { typeof(Texture) },
                null);
        }

        private static MethodInfo ResolveStorageMemoryIntMethod()
        {
            if (s_textureUtilType == null)
                return null;

            return s_textureUtilType.GetMethod(
                "GetStorageMemorySize",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic,
                null,
                new[] { typeof(Texture) },
                null);
        }

        private static double BytesToMegabytes(long bytes)
        {
            return bytes / (1024d * 1024d);
        }

        private static void AppendTextureReportRow(StringBuilder report, string path, Texture texture, long textureBytes)
        {
            report.Append(path.Replace(',', '_'))
                .Append(',')
                .Append(textureBytes.ToString(CultureInfo.InvariantCulture))
                .Append(',')
                .Append(BytesToMegabytes(textureBytes).ToString("F4", CultureInfo.InvariantCulture))
                .Append(',')
                .Append(texture.width.ToString(CultureInfo.InvariantCulture))
                .Append(',')
                .Append(texture.height.ToString(CultureInfo.InvariantCulture))
                .AppendLine();
        }

        private static void WriteReport(string path, StringBuilder report)
        {
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            File.WriteAllText(path, report.ToString(), new UTF8Encoding(false));
        }

        public readonly struct SceneTextureBudgetResult
        {
            public long TotalBytes { get; }
            public int TextureCount { get; }
            public long LargestBytes { get; }
            public string LargestPath { get; }
            public string ReportPath { get; }

            public SceneTextureBudgetResult(
                long totalBytes,
                int textureCount,
                long largestBytes,
                string largestPath,
                string reportPath)
            {
                TotalBytes = totalBytes;
                TextureCount = textureCount;
                LargestBytes = largestBytes;
                LargestPath = largestPath;
                ReportPath = reportPath;
            }
        }
    }
}
