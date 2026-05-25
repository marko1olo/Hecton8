#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Hecton8.EditorTools
{
    /// <summary>
    /// Moves fundamentally broken assets into an isolated folder so they stop contaminating validation and builds.
    /// </summary>
    internal static class HectonAssetQuarantineUtility
    {
        private const string MenuPath = "Hecton/Validation/Asset Pipeline/Quarantine Fundamentally Broken Assets";
        private const string UnreferencedMenuPath = "Hecton/Validation/Asset Pipeline/Move Unreferenced Textures And Meshes";
        private const string QuarantineRoot = "Assets/_Isolated";
        private const string DeprecatedTrashRoot = "Assets/DEPRECATED_TRASH";

        internal sealed class QuarantineResult
        {
            internal int CandidateCount;
            internal readonly List<string> MovedAssets = new List<string>(16);
            internal readonly List<string> SkippedAssets = new List<string>(16);
        }

        [MenuItem(MenuPath, priority = 194)]
        private static void RunFromMenu()
        {
            QuarantineResult result = RunAuditAndQuarantine();
            Debug.Log(
                $"[HectonAssetQuarantineUtility] Candidates={result.CandidateCount}, " +
                $"Moved={result.MovedAssets.Count}, Skipped={result.SkippedAssets.Count}.");

            for (int i = 0; i < result.MovedAssets.Count; i++)
                Debug.LogWarning($"[HectonAssetQuarantineUtility] moved: {result.MovedAssets[i]}");

            for (int i = 0; i < result.SkippedAssets.Count; i++)
                Debug.LogWarning($"[HectonAssetQuarantineUtility] skipped: {result.SkippedAssets[i]}");
        }

        [MenuItem(UnreferencedMenuPath, priority = 195)]
        private static void RunUnreferencedMonitorFromMenu()
        {
            QuarantineResult result = RunUnreferencedReferenceMonitorAndMove();
            Debug.Log(
                $"[HectonAssetQuarantineUtility] UnreferencedCandidates={result.CandidateCount}, " +
                $"MovedToDeprecatedTrash={result.MovedAssets.Count}, Skipped={result.SkippedAssets.Count}.");

            for (int i = 0; i < result.MovedAssets.Count; i++)
                Debug.LogWarning($"[HectonAssetQuarantineUtility] deprecated: {result.MovedAssets[i]}");

            for (int i = 0; i < result.SkippedAssets.Count; i++)
                Debug.LogWarning($"[HectonAssetQuarantineUtility] skipped: {result.SkippedAssets[i]}");
        }

        internal static QuarantineResult RunAuditAndQuarantine()
        {
            HectonMaterialChannelPackValidator.AuditResult materialResult = HectonMaterialChannelPackValidator.RunAudit();
            HectonLodGroupAudit.AuditResult lodResult = HectonLodGroupAudit.RunAudit();
            return Quarantine(materialResult.QuarantineCandidatePaths, lodResult.QuarantineCandidatePaths);
        }

        internal static QuarantineResult RunUnreferencedReferenceMonitorAndMove()
        {
            List<string> candidates = CollectUnreferencedTextureAndMeshAssets();
            return MoveAssetsToRoot(candidates, DeprecatedTrashRoot);
        }

        internal static QuarantineResult PreviewQuarantine(params List<string>[] candidateSets)
        {
            QuarantineResult result = new QuarantineResult();
            HashSet<string> uniquePaths = CollectUniquePaths(candidateSets);
            result.CandidateCount = uniquePaths.Count;

            foreach (string assetPath in uniquePaths)
                result.MovedAssets.Add($"{assetPath} -> {BuildDestinationPath(assetPath)}");

            return result;
        }

        internal static QuarantineResult Quarantine(params List<string>[] candidateSets)
        {
            QuarantineResult result = new QuarantineResult();
            HashSet<string> uniquePaths = CollectUniquePaths(candidateSets);
            result.CandidateCount = uniquePaths.Count;
            if (uniquePaths.Count <= 0)
                return result;

            EnsureFolder(QuarantineRoot);
            foreach (string assetPath in uniquePaths)
            {
                string absolutePath = Path.Combine(Directory.GetCurrentDirectory(), assetPath);
                if (!File.Exists(absolutePath) && !Directory.Exists(absolutePath))
                {
                    result.SkippedAssets.Add($"{assetPath}: source asset no longer exists.");
                    continue;
                }

                string destinationPath = BuildDestinationPath(assetPath);
                string moveError = AssetDatabase.MoveAsset(assetPath, destinationPath);
                if (string.IsNullOrEmpty(moveError))
                {
                    result.MovedAssets.Add($"{assetPath} -> {destinationPath}");
                    continue;
                }

                result.SkippedAssets.Add($"{assetPath}: move failed ({moveError}).");
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return result;
        }

        private static QuarantineResult MoveAssetsToRoot(List<string> candidatePaths, string destinationRoot)
        {
            QuarantineResult result = new QuarantineResult();
            HashSet<string> uniquePaths = CollectUniquePaths(new[] { candidatePaths });
            result.CandidateCount = uniquePaths.Count;
            if (uniquePaths.Count <= 0)
                return result;

            EnsureFolder(destinationRoot);
            foreach (string assetPath in uniquePaths)
            {
                string absolutePath = Path.Combine(Directory.GetCurrentDirectory(), assetPath);
                if (!File.Exists(absolutePath) && !Directory.Exists(absolutePath))
                {
                    result.SkippedAssets.Add($"{assetPath}: source asset no longer exists.");
                    continue;
                }

                string destinationPath = BuildDestinationPath(assetPath, destinationRoot);
                string moveError = AssetDatabase.MoveAsset(assetPath, destinationPath);
                if (string.IsNullOrEmpty(moveError))
                {
                    result.MovedAssets.Add($"{assetPath} -> {destinationPath}");
                    continue;
                }

                result.SkippedAssets.Add($"{assetPath}: move failed ({moveError}).");
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return result;
        }

        private static List<string> CollectUnreferencedTextureAndMeshAssets()
        {
            HashSet<string> referencedPaths = new HashSet<string>(2048, StringComparer.Ordinal);
            AddDependenciesForAssetsOfType("t:Scene", referencedPaths);
            AddDependenciesForAssetsOfType("t:Prefab", referencedPaths);

            List<string> candidates = new List<string>(256);
            AddUnreferencedAssetsOfType("t:Texture2D", referencedPaths, candidates);
            AddUnreferencedAssetsOfType("t:Mesh", referencedPaths, candidates);
            candidates.Sort(StringComparer.Ordinal);
            return candidates;
        }

        private static void AddDependenciesForAssetsOfType(string filter, HashSet<string> referencedPaths)
        {
            string[] guids = AssetDatabase.FindAssets(filter);
            for (int i = 0; i < guids.Length; i++)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (string.IsNullOrEmpty(assetPath) || IsDeprecatedOrIsolated(assetPath))
                    continue;

                string[] dependencies = AssetDatabase.GetDependencies(assetPath, true);
                for (int dependencyIndex = 0; dependencyIndex < dependencies.Length; dependencyIndex++)
                {
                    string dependencyPath = dependencies[dependencyIndex];
                    if (!string.IsNullOrEmpty(dependencyPath))
                        referencedPaths.Add(dependencyPath);
                }
            }
        }

        private static void AddUnreferencedAssetsOfType(
            string filter,
            HashSet<string> referencedPaths,
            List<string> candidates)
        {
            string[] guids = AssetDatabase.FindAssets(filter);
            for (int i = 0; i < guids.Length; i++)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (string.IsNullOrEmpty(assetPath)
                    || IsDeprecatedOrIsolated(assetPath)
                    || referencedPaths.Contains(assetPath))
                {
                    continue;
                }

                candidates.Add(assetPath);
            }
        }

        private static bool IsDeprecatedOrIsolated(string assetPath)
        {
            return assetPath.StartsWith(DeprecatedTrashRoot + "/", StringComparison.Ordinal)
                || assetPath.StartsWith(QuarantineRoot + "/", StringComparison.Ordinal);
        }

        private static HashSet<string> CollectUniquePaths(List<string>[] candidateSets)
        {
            HashSet<string> uniquePaths = new HashSet<string>(256, StringComparer.Ordinal);
            for (int setIndex = 0; setIndex < candidateSets.Length; setIndex++)
            {
                List<string> candidateSet = candidateSets[setIndex];
                if (candidateSet == null)
                    continue;

                for (int i = 0; i < candidateSet.Count; i++)
                {
                    string assetPath = candidateSet[i];
                    if (string.IsNullOrWhiteSpace(assetPath))
                        continue;

                    uniquePaths.Add(assetPath);
                }
            }

            return uniquePaths;
        }

        private static string BuildDestinationPath(string assetPath)
        {
            return BuildDestinationPath(assetPath, QuarantineRoot);
        }

        private static string BuildDestinationPath(string assetPath, string rootPath)
        {
            string relativePath = assetPath.StartsWith("Assets/", StringComparison.Ordinal)
                ? assetPath.Substring("Assets/".Length)
                : Path.GetFileName(assetPath);
            string rawDestinationPath = $"{rootPath}/{relativePath}";
            string normalizedDestinationPath = rawDestinationPath.Replace('\\', '/');

            int separatorIndex = normalizedDestinationPath.LastIndexOf('/');
            if (separatorIndex > 0)
                EnsureFolder(normalizedDestinationPath.Substring(0, separatorIndex));

            return AssetDatabase.GenerateUniqueAssetPath(normalizedDestinationPath);
        }

        private static void EnsureFolder(string assetPath)
        {
            if (AssetDatabase.IsValidFolder(assetPath))
                return;

            int separatorIndex = assetPath.LastIndexOf('/');
            if (separatorIndex <= 0)
                return;

            string parentPath = assetPath.Substring(0, separatorIndex);
            string folderName = assetPath.Substring(separatorIndex + 1);
            EnsureFolder(parentPath);
            if (!AssetDatabase.IsValidFolder(assetPath))
                AssetDatabase.CreateFolder(parentPath, folderName);
        }
    }
}
#endif
