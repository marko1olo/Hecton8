#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hecton8.Editor
{
    /// <summary>
    /// Scans prefabs and scenes for missing prefab parents and other irrecoverable prefab-state failures.
    /// </summary>
    internal static class HectonPrefabIntegrityScanner
    {
        private const string MenuPath = "Hecton/Validation/Asset Pipeline/Scan Prefab Integrity And Repair";
        private const string ErrorMaterialPath = "Assets/_Project/Art/Materials/Diagnostics/MAT_ErrorCube.mat";
        private const string ErrorPrefabPath = "Assets/_Project/Prefabs/Diagnostics/PFB_ErrorCube.prefab";
        private static readonly string[] AssetRoots = { "Assets" };
        private static readonly string[] ReadOnlyScenePrefixes =
        {
            "Assets/_Recovery/",
            "Assets/sandbox/",
            "Assets/Sandbox/"
        };

        internal sealed class ScanResult
        {
            internal int ScannedPrefabAssetCount;
            internal int ScannedSceneCount;
            internal readonly List<string> BrokenVariantAssets = new List<string>(16);
            internal readonly List<string> ReplacedPrefabAssets = new List<string>(16);
            internal readonly List<string> SkippedPrefabAssetRepairs = new List<string>(16);
            internal readonly List<string> UnpackedInstances = new List<string>(16);
            internal readonly List<string> ReplacedInstances = new List<string>(16);
            internal readonly List<string> BrokenReferences = new List<string>(64);
            internal readonly List<string> SkippedSceneRepairs = new List<string>(16);
        }

        [MenuItem(MenuPath, priority = 190)]
        private static void RunFromMenu()
        {
            ScanResult result = ScanAndRepair();
            Debug.Log(
                $"[HectonPrefabIntegrityScanner] ScannedPrefabAssets={result.ScannedPrefabAssetCount}, " +
                $"ScannedScenes={result.ScannedSceneCount}, BrokenVariantAssets={result.BrokenVariantAssets.Count}, " +
                $"ReplacedPrefabAssets={result.ReplacedPrefabAssets.Count}, SkippedPrefabAssetRepairs={result.SkippedPrefabAssetRepairs.Count}, " +
                $"UnpackedInstances={result.UnpackedInstances.Count}, " +
                $"ReplacedInstances={result.ReplacedInstances.Count}, BrokenReferences={result.BrokenReferences.Count}.");
        }

        internal static ScanResult ScanAndRepair()
        {
            GameObject errorCubePrefab = EnsureErrorCubePrefab();
            ScanResult result = new ScanResult();

            ScanPrefabAssets(result, errorCubePrefab);
            ScanSceneAssets(result, errorCubePrefab);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return result;
        }

        private static void ScanPrefabAssets(ScanResult result, GameObject errorCubePrefab)
        {
            string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", AssetRoots);

            try
            {
                for (int i = 0; i < prefabGuids.Length; i++)
                {
                    string prefabPath = AssetDatabase.GUIDToAssetPath(prefabGuids[i]);
                    if (string.IsNullOrWhiteSpace(prefabPath))
                        continue;

                    result.ScannedPrefabAssetCount++;
                    EditorUtility.DisplayProgressBar(
                        "HECTON-8 Prefab Integrity",
                        prefabPath,
                        prefabGuids.Length > 0 ? (i + 1f) / prefabGuids.Length : 1f);

                    GameObject prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                    if (prefabAsset == null)
                    {
                        result.BrokenReferences.Add($"{prefabPath}: prefab asset failed to load.");
                        TryRepairNullPrefabAsset(prefabPath, errorCubePrefab, result);
                        continue;
                    }

                    PrefabAssetType assetType = PrefabUtility.GetPrefabAssetType(prefabAsset);
                    if (assetType == PrefabAssetType.Variant)
                    {
                        GameObject variantSource = PrefabUtility.GetCorrespondingObjectFromOriginalSource(prefabAsset);
                        if (variantSource == null)
                        {
                            result.BrokenVariantAssets.Add($"{prefabPath}: missing prefab variant parent.");
                            ReplaceBrokenPrefabAsset(prefabPath, errorCubePrefab, result);
                            continue;
                        }
                    }

                    GameObject prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);
                    if (prefabRoot == null)
                    {
                        result.BrokenReferences.Add($"{prefabPath}: PrefabUtility.LoadPrefabContents returned null.");
                        continue;
                    }

                    bool prefabChanged = false;
                    try
                    {
                        prefabChanged = ScanHierarchy(prefabPath, prefabRoot.scene, prefabRoot, result, errorCubePrefab, allowRepair: false);
                        if (prefabChanged)
                            PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);
                    }
                    finally
                    {
                        PrefabUtility.UnloadPrefabContents(prefabRoot);
                    }
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        private static void ScanSceneAssets(ScanResult result, GameObject errorCubePrefab)
        {
            string[] sceneGuids = AssetDatabase.FindAssets("t:Scene", AssetRoots);
            List<string> originallyLoadedScenes = new List<string>(SceneManager.sceneCount);
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene loadedScene = SceneManager.GetSceneAt(i);
                if (loadedScene.IsValid() && loadedScene.isLoaded)
                    originallyLoadedScenes.Add(loadedScene.path);
            }

            try
            {
                for (int i = 0; i < sceneGuids.Length; i++)
                {
                    string scenePath = AssetDatabase.GUIDToAssetPath(sceneGuids[i]);
                    if (string.IsNullOrWhiteSpace(scenePath))
                        continue;

                    if (IsReadOnlyScene(scenePath))
                    {
                        result.SkippedSceneRepairs.Add($"{scenePath}: scan-skipped non-production scene.");
                        continue;
                    }

                    result.ScannedSceneCount++;
                    EditorUtility.DisplayProgressBar(
                        "HECTON-8 Scene Integrity",
                        scenePath,
                        sceneGuids.Length > 0 ? (i + 1f) / sceneGuids.Length : 1f);

                    bool alreadyLoaded = originallyLoadedScenes.Contains(scenePath);
                    Scene scene = alreadyLoaded
                        ? SceneManager.GetSceneByPath(scenePath)
                        : EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);

                    bool allowRepair = true;
                    bool sceneChanged = false;
                    try
                    {
                        if (!scene.IsValid() || !scene.isLoaded)
                        {
                            result.BrokenReferences.Add($"{scenePath}: scene failed to load for integrity scan.");
                            continue;
                        }

                        sceneChanged = ScanSceneHierarchy(scenePath, scene, result, errorCubePrefab, allowRepair);
                        if (sceneChanged && allowRepair)
                            EditorSceneManager.SaveScene(scene);
                    }
                    finally
                    {
                        if (!alreadyLoaded && scene.IsValid() && scene.isLoaded)
                            EditorSceneManager.CloseScene(scene, removeScene: true);
                    }
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        private static bool ScanSceneHierarchy(
            string scenePath,
            Scene scene,
            ScanResult result,
            GameObject errorCubePrefab,
            bool allowRepair)
        {
            bool changed = false;
            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                GameObject root = roots[i];
                if (root == null)
                    continue;

                if (ScanHierarchy(scenePath, scene, root, result, errorCubePrefab, allowRepair))
                    changed = true;
            }

            if (changed && allowRepair)
                EditorSceneManager.MarkSceneDirty(scene);

            return changed;
        }

        private static bool ScanHierarchy(
            string ownerPath,
            Scene ownerScene,
            GameObject root,
            ScanResult result,
            GameObject errorCubePrefab,
            bool allowRepair)
        {
            List<GameObject> objects = new List<GameObject>(64);
            CollectGameObjects(root.transform, objects);

            bool changed = false;
            for (int i = 0; i < objects.Count; i++)
            {
                GameObject current = objects[i];
                if (current == null)
                    continue;

                if (InspectGameObject(ownerPath, ownerScene, current, result, errorCubePrefab, allowRepair))
                    changed = true;
            }

            return changed;
        }

        private static bool InspectGameObject(
            string ownerPath,
            Scene ownerScene,
            GameObject target,
            ScanResult result,
            GameObject errorCubePrefab,
            bool allowRepair)
        {
            bool changed = false;
            string hierarchyPath = BuildTransformPath(target.transform);

            if (PrefabUtility.IsAnyPrefabInstanceRoot(target) && PrefabUtility.IsPrefabAssetMissing(target))
            {
                string entry = $"{ownerPath}: {hierarchyPath}: missing prefab asset.";
                if (!allowRepair)
                {
                    AddSkippedRepair(ownerPath, result, entry);
                }
                else if (TryUnpackMissingPrefabInstance(target))
                {
                    result.UnpackedInstances.Add(entry + " repaired=unpacked");
                    changed = true;
                }
                else if (TryReplaceWithErrorCube(target, ownerScene, errorCubePrefab, out GameObject replacement))
                {
                    result.ReplacedInstances.Add(entry + " repaired=replaced-with-error-cube");
                    target = replacement;
                    hierarchyPath = BuildTransformPath(target.transform);
                    changed = true;
                }
                else
                {
                    result.BrokenReferences.Add(entry + " repair=failed");
                }
            }

            int missingScriptCount = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(target);
            if (missingScriptCount > 0)
                result.BrokenReferences.Add($"{ownerPath}: {hierarchyPath}: missing script components={missingScriptCount}.");

            if (target.TryGetComponent(out MeshFilter meshFilter) && meshFilter.sharedMesh == null)
                result.BrokenReferences.Add($"{ownerPath}: {hierarchyPath}: MeshFilter missing sharedMesh.");

            if (target.TryGetComponent(out SkinnedMeshRenderer skinnedMeshRenderer) && skinnedMeshRenderer.sharedMesh == null)
                result.BrokenReferences.Add($"{ownerPath}: {hierarchyPath}: SkinnedMeshRenderer missing sharedMesh.");

            if (target.TryGetComponent(out Renderer renderer))
            {
                Material[] sharedMaterials = renderer.sharedMaterials;
                for (int materialIndex = 0; materialIndex < sharedMaterials.Length; materialIndex++)
                {
                    if (sharedMaterials[materialIndex] == null)
                    {
                        result.BrokenReferences.Add(
                            $"{ownerPath}: {hierarchyPath}: Renderer '{renderer.name}' has null shared material slot {materialIndex}.");
                    }
                }
            }

            return changed;
        }

        private static void ReplaceBrokenPrefabAsset(string prefabPath, GameObject errorCubePrefab, ScanResult result)
        {
            GameObject replacementSource = ResolveBrokenVariantReplacementSource(prefabPath);
            string replacementPath = replacementSource != null
                ? AssetDatabase.GetAssetPath(replacementSource)
                : ErrorPrefabPath;
            string primitiveState = replacementSource != null &&
                                    WorldProceduralFinalPrefabQualityGate.UsesUnityBuiltInPrimitiveMesh(replacementSource)
                ? " primitive_candidate=true"
                : string.Empty;

            result.SkippedPrefabAssetRepairs.Add(
                $"{prefabPath}: destructive prefab asset repair blocked; candidate replacement='{replacementPath}'{primitiveState}.");
        }

        private static bool TryRepairNullPrefabAsset(string prefabPath, GameObject errorCubePrefab, ScanResult result)
        {
            GameObject replacementSource = ResolveBrokenVariantReplacementSource(prefabPath);
            string replacementPath = replacementSource != null
                ? AssetDatabase.GetAssetPath(replacementSource)
                : ErrorPrefabPath;
            string primitiveState = replacementSource != null &&
                                    WorldProceduralFinalPrefabQualityGate.UsesUnityBuiltInPrimitiveMesh(replacementSource)
                ? " primitive_candidate=true"
                : string.Empty;

            result.SkippedPrefabAssetRepairs.Add(
                $"{prefabPath}: destructive null-prefab rebuild blocked; candidate replacement='{replacementPath}'{primitiveState}.");
            return false;
        }

        private static GameObject ResolveBrokenVariantReplacementSource(string prefabPath)
        {
            string normalizedPrefabPath = prefabPath.Replace('\\', '/');
            string searchRoot = Path.GetDirectoryName(normalizedPrefabPath);
            if (string.IsNullOrWhiteSpace(searchRoot))
                return null;

            string normalizedBrokenName = NormalizeVariantFamilyName(Path.GetFileNameWithoutExtension(normalizedPrefabPath));
            string[] candidateGuids = AssetDatabase.FindAssets("t:Model", new[] { searchRoot.Replace('\\', '/') });

            for (int i = 0; i < candidateGuids.Length; i++)
            {
                string candidatePath = AssetDatabase.GUIDToAssetPath(candidateGuids[i]);
                if (string.IsNullOrWhiteSpace(candidatePath))
                    continue;

                string normalizedCandidateName = NormalizeVariantFamilyName(Path.GetFileNameWithoutExtension(candidatePath));
                if (!string.Equals(normalizedBrokenName, normalizedCandidateName, StringComparison.OrdinalIgnoreCase))
                    continue;

                GameObject candidatePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(candidatePath);
                if (candidatePrefab != null)
                    return candidatePrefab;
            }

            return null;
        }

        private static string NormalizeVariantFamilyName(string assetName)
        {
            if (string.IsNullOrWhiteSpace(assetName))
                return string.Empty;

            string[] segments = assetName.Split(new[] { '_' }, StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length < 3)
                return assetName;

            int tokenIndex = segments.Length - 2;
            string token = segments[tokenIndex];
            if (!LooksLikeAssetToken(token))
                return assetName;

            segments[tokenIndex] = string.Empty;
            return string.Join("_", segments).Replace("__", "_").Trim('_');
        }

        private static bool LooksLikeAssetToken(string token)
        {
            if (string.IsNullOrEmpty(token) || token.Length < 6 || token.Length > 12)
                return false;

            for (int i = 0; i < token.Length; i++)
            {
                char current = token[i];
                if (!char.IsLetterOrDigit(current))
                    return false;
            }

            return true;
        }

        private static bool TryUnpackMissingPrefabInstance(GameObject instanceRoot)
        {
            try
            {
                PrefabUtility.UnpackPrefabInstance(instanceRoot, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static bool TryReplaceWithErrorCube(
            GameObject brokenRoot,
            Scene ownerScene,
            GameObject errorCubePrefab,
            out GameObject replacement)
        {
            replacement = null;
            if (brokenRoot == null || errorCubePrefab == null)
                return false;

            Transform sourceTransform = brokenRoot.transform;
            Transform parent = sourceTransform.parent;
            int siblingIndex = sourceTransform.GetSiblingIndex();
            bool activeSelf = brokenRoot.activeSelf;
            int layer = brokenRoot.layer;
            string tag = brokenRoot.tag;
            Vector3 position = sourceTransform.position;
            Quaternion rotation = sourceTransform.rotation;
            Vector3 lossyScale = sourceTransform.lossyScale;
            Vector3 localPosition = sourceTransform.localPosition;
            Quaternion localRotation = sourceTransform.localRotation;
            Vector3 localScale = sourceTransform.localScale;

            replacement = PrefabUtility.InstantiatePrefab(errorCubePrefab, ownerScene) as GameObject;
            if (replacement == null)
            {
                replacement = UnityEngine.Object.Instantiate(errorCubePrefab);
                if (ownerScene.IsValid() && ownerScene.isLoaded)
                    SceneManager.MoveGameObjectToScene(replacement, ownerScene);
            }

            replacement.name = "ERROR_" + brokenRoot.name;
            replacement.layer = layer;
            replacement.tag = tag;
            replacement.SetActive(activeSelf);

            if (parent != null)
            {
                replacement.transform.SetParent(parent, false);
                replacement.transform.localPosition = localPosition;
                replacement.transform.localRotation = localRotation;
                replacement.transform.localScale = localScale;
                replacement.transform.SetSiblingIndex(siblingIndex);
            }
            else
            {
                replacement.transform.SetPositionAndRotation(position, rotation);
                replacement.transform.localScale = lossyScale;
            }

            UnityEngine.Object.DestroyImmediate(brokenRoot);
            return true;
        }

        private static GameObject EnsureErrorCubePrefab()
        {
            EnsureFolder("Assets/_Project/Art");
            EnsureFolder("Assets/_Project/Art/Materials");
            EnsureFolder("Assets/_Project/Art/Materials/Diagnostics");
            EnsureFolder("Assets/_Project/Prefabs");
            EnsureFolder("Assets/_Project/Prefabs/Diagnostics");

            Material errorMaterial = AssetDatabase.LoadAssetAtPath<Material>(ErrorMaterialPath);
            if (errorMaterial == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                errorMaterial = new Material(shader)
                {
                    name = "MAT_ErrorCube"
                };

                if (errorMaterial.HasProperty("_BaseColor"))
                    errorMaterial.SetColor("_BaseColor", new Color(1f, 0f, 1f, 1f));
                if (errorMaterial.HasProperty("_Color"))
                    errorMaterial.SetColor("_Color", new Color(1f, 0f, 1f, 1f));
                if (errorMaterial.HasProperty("_EmissionColor"))
                {
                    errorMaterial.EnableKeyword("_EMISSION");
                    errorMaterial.SetColor("_EmissionColor", new Color(1f, 0.1f, 1f, 1f) * 0.25f);
                }

                AssetDatabase.CreateAsset(errorMaterial, ErrorMaterialPath);
            }

            GameObject errorPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ErrorPrefabPath);
            if (errorPrefab != null)
                return errorPrefab;

            GameObject temporaryRoot = GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                temporaryRoot.name = "PFB_ErrorCube";
                if (temporaryRoot.TryGetComponent(out Collider collider))
                    UnityEngine.Object.DestroyImmediate(collider);

                if (temporaryRoot.TryGetComponent(out MeshRenderer renderer))
                    renderer.sharedMaterial = errorMaterial;

                PrefabUtility.SaveAsPrefabAsset(temporaryRoot, ErrorPrefabPath);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(temporaryRoot);
            }

            return AssetDatabase.LoadAssetAtPath<GameObject>(ErrorPrefabPath);
        }

        private static bool IsReadOnlyScene(string scenePath)
        {
            if (string.IsNullOrWhiteSpace(scenePath))
                return true;

            string normalizedPath = scenePath.Replace('\\', '/');
            for (int i = 0; i < ReadOnlyScenePrefixes.Length; i++)
            {
                if (normalizedPath.StartsWith(ReadOnlyScenePrefixes[i], StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        private static void AddSkippedRepair(string ownerPath, ScanResult result, string entry)
        {
            if (IsPrefabAssetPath(ownerPath))
                result.SkippedPrefabAssetRepairs.Add(entry);
            else
                result.SkippedSceneRepairs.Add(entry);
        }

        private static bool IsPrefabAssetPath(string path)
        {
            return !string.IsNullOrWhiteSpace(path) &&
                   path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase);
        }

        private static void CollectGameObjects(Transform root, List<GameObject> destination)
        {
            if (root == null || destination == null)
                return;

            destination.Add(root.gameObject);
            for (int i = 0; i < root.childCount; i++)
                CollectGameObjects(root.GetChild(i), destination);
        }

        private static string BuildTransformPath(Transform target)
        {
            if (target == null)
                return "<null>";

            Stack<string> segments = new Stack<string>(8);
            Transform current = target;
            while (current != null)
            {
                segments.Push(current.name);
                current = current.parent;
            }

            return string.Join("/", segments);
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
