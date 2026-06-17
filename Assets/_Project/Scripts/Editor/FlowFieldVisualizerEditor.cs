// ============================================================================
// HECTON-8 — FlowFieldVisualizerEditor.cs
// Redaktorskie utility dlya FlowFieldVisualizer.
//
// Dobavlyaet knopki v Inspector dlya bystrogo upravleniya
// i punkt menyu dlya sozdaniya vizualizatora.
// ============================================================================

#if UNITY_EDITOR
using System.Globalization;
using UnityEditor;
using UnityEngine;
using Hecton8.Physics;

[CustomEditor(typeof(FlowFieldVisualizer))]
public sealed class FlowFieldVisualizerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        FlowFieldVisualizer visualizer = (FlowFieldVisualizer)target;

        DrawDefaultInspector();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Quick Actions", EditorStyles.boldLabel);

        if (GUILayout.Button("Recalculate Flow Field"))
        {
            visualizer.Recalculate();
            SceneView.RepaintAll();
        }

        if (GUILayout.Button("Capture Current Settings to Profile"))
        {
            if (visualizer.Profile != null)
            {
                visualizer.Profile.CaptureFrom(visualizer);
                EditorUtility.SetDirty(visualizer.Profile);
                AssetDatabase.SaveAssets();
                Debug.Log("Settings captured to profile: " + visualizer.Profile.name);
            }
            else
            {
                Debug.LogWarning("No profile assigned to capture settings to.");
            }
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Info", EditorStyles.miniBoldLabel);
        EditorGUILayout.LabelField($"Grid Points: {visualizer.GridResolution.x * visualizer.GridResolution.y}", EditorStyles.miniLabel);
        EditorGUILayout.LabelField(
            "Area: " +
            visualizer.AreaSize.x.ToString("F1", CultureInfo.InvariantCulture) +
            " x " +
            visualizer.AreaSize.y.ToString("F1", CultureInfo.InvariantCulture) +
            " meters",
            EditorStyles.miniLabel);
    }
}

public static class FlowFieldVisualizerMenu
{
    [MenuItem("Hecton8/Tools/Create Flow Field Visualizer", false, 100)]
    private static void CreateVisualizer()
    {
        GameObject go = new GameObject("FlowFieldVisualizer");
        go.transform.position = SceneView.lastActiveSceneView?.camera?.transform?.position ?? Vector3.zero;
        go.AddComponent<FlowFieldVisualizer>();

        Selection.activeGameObject = go;
        Undo.RegisterCreatedObjectUndo(go, "Create Flow Field Visualizer");
    }

    [MenuItem("Hecton8/Tools/Create Flow Field Profile", false, 101)]
    private static void CreateProfile()
    {
        FlowFieldProfile profile = ScriptableObject.CreateInstance<FlowFieldProfile>();
        const string FolderPath = "Assets/_Project/Data";
        string path = AssetDatabase.GenerateUniqueAssetPath(FolderPath + "/FlowFieldProfile.asset");

        // Sozdaem papku, esli ne suschestvuet
        string dir = System.IO.Path.GetDirectoryName(path);
        if (!AssetDatabase.IsValidFolder(dir))
            AssetDatabase.CreateFolder("Assets/_Project", "Data");

        AssetDatabase.CreateAsset(profile, path);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Selection.activeObject = profile;
        EditorGUIUtility.PingObject(profile);
    }
}
#endif
