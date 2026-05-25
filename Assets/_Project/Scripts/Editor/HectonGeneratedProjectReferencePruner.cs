#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Linq;
using UnityEditor;

namespace Hecton8.Editor.Validation
{
    /// <summary>
    /// Removes stale Unity-generated project references that point at missing package-cache DLLs.
    /// </summary>
    internal sealed class HectonGeneratedProjectReferencePruner : AssetPostprocessor
    {
        private const string StaleCecilReferenceName = "Unity.Cecil.Awesome";
        private const string StaleEntitiesReferenceName = "Unity.Entities";
        private const string StaleEntitiesPackageMarker = "com.unity.entities@";
        private const string LibraryPackageCacheMarker = "Library/PackageCache";

        private static readonly List<XElement> s_referencesToRemove = new List<XElement>(16); // COLD ALLOC: List<XElement>[16] - generated project item scratch - owner: HectonGeneratedProjectReferencePruner

        private static string OnGeneratedCSProject(string path, string content)
        {
            if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(content))
                return content;

            s_referencesToRemove.Clear();

            try
            {
                XDocument document = XDocument.Parse(content, LoadOptions.PreserveWhitespace);
                XElement root = document.Root;
                if (root == null)
                    return content;

                string projectDirectory = ResolveProjectDirectory(path);
                CollectMissingReferences(root, projectDirectory);
                CollectMissingAnalyzers(root);

                int removalCount = s_referencesToRemove.Count;
                if (removalCount == 0)
                    return content;

                for (int i = 0; i < removalCount; i++)
                    s_referencesToRemove[i].Remove();

                return document.ToString(SaveOptions.DisableFormatting);
            }
            catch (Exception)
            {
                return content;
            }
            finally
            {
                s_referencesToRemove.Clear();
            }
        }

        private static void CollectMissingReferences(XElement root, string projectDirectory)
        {
            IEnumerable<XElement> references = root.Descendants("Reference");
            foreach (XElement reference in references)
            {
                XAttribute include = reference.Attribute("Include");
                if (include == null)
                    continue;

                XElement hintPath = reference.Element("HintPath");
                if (hintPath == null || string.IsNullOrEmpty(hintPath.Value))
                    continue;

                string resolvedHintPath = ResolveHintPath(projectDirectory, hintPath.Value);
                if (File.Exists(resolvedHintPath))
                    continue;

                if (ShouldPruneMissingReference(include.Value, hintPath.Value))
                    s_referencesToRemove.Add(reference);
            }
        }

        private static void CollectMissingAnalyzers(XElement root)
        {
            IEnumerable<XElement> analyzers = root.Descendants("Analyzer");
            foreach (XElement analyzer in analyzers)
            {
                XAttribute include = analyzer.Attribute("Include");
                if (include == null || string.IsNullOrEmpty(include.Value))
                    continue;

                if (include.Value.IndexOf(StaleEntitiesPackageMarker, StringComparison.OrdinalIgnoreCase) < 0)
                    continue;

                if (!File.Exists(include.Value))
                    s_referencesToRemove.Add(analyzer);
            }
        }

        private static string ResolveProjectDirectory(string projectPath)
        {
            string fullPath = Path.IsPathRooted(projectPath)
                ? projectPath
                : Path.GetFullPath(projectPath);

            string directory = Path.GetDirectoryName(fullPath);
            return string.IsNullOrEmpty(directory) ? Directory.GetCurrentDirectory() : directory;
        }

        private static string ResolveHintPath(string projectDirectory, string hintPath)
        {
            if (Path.IsPathRooted(hintPath))
                return hintPath;

            return Path.GetFullPath(Path.Combine(projectDirectory, hintPath));
        }

        private static bool ShouldPruneMissingReference(string include, string hintPath)
        {
            if (include == StaleCecilReferenceName || include == StaleEntitiesReferenceName)
                return true;

            string normalizedHintPath = hintPath.Replace('\\', '/');
            return normalizedHintPath.IndexOf(LibraryPackageCacheMarker, StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
#endif
