#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

public static class HectonMeshSaver
{
    private const string RootFolder = "Assets/_Project";
    private const string ArtFolder = "Assets/_Project/Art";
    private const string ModelsFolder = "Assets/_Project/Art/Models";
    private const string BakedFolder = "Assets/_Project/Art/Models/Baked";

    [MenuItem("Hecton8/Save Selected Mesh", priority = 200)]
    private static void SaveSelectedMesh()
    {
        GameObject selectedObject = Selection.activeGameObject;

        if (selectedObject == null)
        {
            EditorUtility.DisplayDialog(
                "Hecton Mesh Saver",
                "No GameObject selected.\n\nSelect a scene object with a MeshFilter and try again.",
                "OK");
            return;
        }

        if (!selectedObject.TryGetComponent(out MeshFilter meshFilter))
        {
            if (selectedObject.TryGetComponent(out SkinnedMeshRenderer _))
            {
                EditorUtility.DisplayDialog(
                    "Hecton Mesh Saver",
                    $"Selected object '{selectedObject.name}' uses SkinnedMeshRenderer.\n\n" +
                    "This saver currently supports MeshFilter.sharedMesh only.",
                    "OK");
            }
            else
            {
                EditorUtility.DisplayDialog(
                    "Hecton Mesh Saver",
                    $"Selected object '{selectedObject.name}' does not have a MeshFilter.",
                    "OK");
            }

            return;
        }

        Mesh sourceMesh = meshFilter.sharedMesh;
        if (sourceMesh == null)
        {
            EditorUtility.DisplayDialog(
                "Hecton Mesh Saver",
                $"Selected object '{selectedObject.name}' has no mesh in MeshFilter.sharedMesh.",
                "OK");
            return;
        }

        EnsureFolderHierarchyExists();

        string safeName = SanitizeFileName(selectedObject.name);
        string targetPath = $"{BakedFolder}/{safeName}.asset";
        targetPath = AssetDatabase.GenerateUniqueAssetPath(targetPath);

        Mesh meshCopy = CreateDeepMeshCopy(sourceMesh, selectedObject.name);
        if (meshCopy == null)
        {
            EditorUtility.DisplayDialog(
                "Hecton Mesh Saver",
                "Failed to create mesh copy.",
                "OK");
            return;
        }

        AssetDatabase.CreateAsset(meshCopy, targetPath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Mesh savedAssetMesh = AssetDatabase.LoadAssetAtPath<Mesh>(targetPath);
        if (savedAssetMesh == null)
        {
            EditorUtility.DisplayDialog(
                "Hecton Mesh Saver",
                $"Mesh asset was created but could not be reloaded.\n\nPath:\n{targetPath}",
                "OK");
            return;
        }

        Undo.RecordObject(meshFilter, "Assign Saved Mesh Asset");
        meshFilter.sharedMesh = savedAssetMesh;

        EditorUtility.SetDirty(meshFilter);
        EditorUtility.SetDirty(selectedObject);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log(
            $"[HectonMeshSaver] Mesh saved successfully.\n" +
            $"Object: {selectedObject.name}\n" +
            $"Source Mesh: {sourceMesh.name}\n" +
            $"Saved Path: {targetPath}",
            selectedObject);

        EditorGUIUtility.PingObject(savedAssetMesh);
    }

    [MenuItem("Hecton8/Save Selected Mesh", true)]
    private static bool ValidateSaveSelectedMesh()
    {
        GameObject selectedObject = Selection.activeGameObject;
        if (selectedObject == null)
            return false;

        if (!selectedObject.TryGetComponent(out MeshFilter meshFilter))
            return false;

        return meshFilter.sharedMesh != null;
    }

    private static Mesh CreateDeepMeshCopy(Mesh sourceMesh, string newMeshName)
    {
        if (sourceMesh == null)
            return null;

        Mesh meshCopy = Object.Instantiate(sourceMesh);
        meshCopy.name = newMeshName;

        // Dopolnitelnaya strahovka: garantiruem otvyazku dannyh.
        // V bolshinstve sluchaev Instantiate uzhe dostatochno,
        // no etot vyzov polezen dlya redaktorskogo payplayna.
        meshCopy.hideFlags = HideFlags.None;

        return meshCopy;
    }

    private static void EnsureFolderHierarchyExists()
    {
        EnsureFolderExists("Assets", "_Project");
        EnsureFolderExists(RootFolder, "Art");
        EnsureFolderExists(ArtFolder, "Models");
        EnsureFolderExists(ModelsFolder, "Baked");
    }

    private static void EnsureFolderExists(string parentFolder, string childFolder)
    {
        string fullPath = $"{parentFolder}/{childFolder}";
        if (AssetDatabase.IsValidFolder(fullPath))
            return;

        AssetDatabase.CreateFolder(parentFolder, childFolder);
    }

    private static string SanitizeFileName(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return "SavedMesh";

        char[] invalidChars = Path.GetInvalidFileNameChars();
        string result = input;

        for (int i = 0; i < invalidChars.Length; i++)
        {
            result = result.Replace(invalidChars[i], '_');
        }

        result = result.Trim();

        if (string.IsNullOrEmpty(result))
            result = "SavedMesh";

        return result;
    }
}
#endif
