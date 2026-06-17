// HECTON-8 - MetaFileGenerator.cs
// Editor-only first-party script meta recovery. Excludes Plugins and third-party roots.

#if UNITY_EDITOR
using System.IO;
using System.Text;
using UnityEditor;

namespace Hecton8.Editor.Build
{
    internal static class MetaFileGenerator
    {
        private const string ScriptRoot = "Assets/_Project/Scripts";
        private const string ProjectPluginsRoot = "Assets/_Project/Scripts/Plugins/";
        private const string ThirdPartyRoot = "Assets/_ThirdParty/";
        private const string PluginsRoot = "Assets/Plugins/";

        // COLD ALLOC: UTF8Encoding[1] - editor-only deterministic .meta writer - owner: MetaFileGenerator
        private static readonly UTF8Encoding _utf8NoBom = new UTF8Encoding(false);

        [InitializeOnLoadMethod]
        private static void RegisterDelayedScan()
        {
            EditorApplication.delayCall -= GenerateMissingMetas;
            EditorApplication.delayCall += GenerateMissingMetas;
        }

        [MenuItem("Hecton8/Build/Generate Missing Script Metas")]
        public static void GenerateMissingMetas()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || !Directory.Exists(ScriptRoot))
                return;

            int generatedCount = 0;
            foreach (string scriptPath in Directory.EnumerateFiles(ScriptRoot, "*.cs", SearchOption.AllDirectories))
            {
                string assetPath = NormalizeAssetPath(scriptPath);
                if (ShouldSkip(assetPath))
                    continue;

                string metaPath = assetPath + ".meta";
                if (File.Exists(metaPath))
                    continue;

                AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
                if (!File.Exists(metaPath))
                    WriteScriptMeta(metaPath);

                generatedCount++;
            }

            if (generatedCount > 0)
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        }

        private static bool ShouldSkip(string assetPath)
        {
            return assetPath.StartsWith(ProjectPluginsRoot, System.StringComparison.Ordinal) ||
                   assetPath.StartsWith(ThirdPartyRoot, System.StringComparison.Ordinal) ||
                   assetPath.StartsWith(PluginsRoot, System.StringComparison.Ordinal);
        }

        private static string NormalizeAssetPath(string path)
        {
            return path.Replace('\\', '/');
        }

        private static void WriteScriptMeta(string metaPath)
        {
            string guid = System.Guid.NewGuid().ToString("N");
            using StreamWriter writer = new StreamWriter(metaPath, false, _utf8NoBom);
            writer.WriteLine("fileFormatVersion: 2");
            writer.Write("guid: ");
            writer.WriteLine(guid);
            writer.WriteLine("MonoImporter:");
            writer.WriteLine("  externalObjects: {}");
            writer.WriteLine("  serializedVersion: 2");
            writer.WriteLine("  defaultReferences: []");
            writer.WriteLine("  executionOrder: 0");
            writer.WriteLine("  icon: {instanceID: 0}");
            writer.WriteLine("  userData: ");
            writer.WriteLine("  assetBundleName: ");
            writer.WriteLine("  assetBundleVariant: ");
        }
    }
}
#endif
