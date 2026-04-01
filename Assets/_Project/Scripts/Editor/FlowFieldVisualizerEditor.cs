// ============================================================================
// HECTON-8 — FlowFieldVisualizerEditor.cs
// Редакторские утилиты для FlowFieldVisualizer.
//
// Добавляет кнопки в Inspector для быстрого управления
// и пункт меню для создания визуализатора.
// ============================================================================

#if UNITY_EDITOR
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
        EditorGUILayout.LabelField($"Area: {visualizer.AreaSize.x:F1} x {visualizer.AreaSize.y:F1} meters", EditorStyles.miniLabel);
    }
}

public static class FlowFieldVisualizerMenu
{
    [MenuItem("Hecton/Tools/Create Flow Field Visualizer", false, 100)]
    private static void CreateVisualizer()
    {
        GameObject go = new GameObject("FlowFieldVisualizer");
        go.transform.position = SceneView.lastActiveSceneView?.camera?.transform?.position ?? Vector3.zero;
        go.AddComponent<FlowFieldVisualizer>();

        Selection.activeGameObject = go;
        Undo.RegisterCreatedObjectUndo(go, "Create Flow Field Visualizer");
    }

    [MenuItem("Hecton/Tools/Create Flow Field Profile", false, 101)]
    private static void CreateProfile()
    {
        FlowFieldProfile profile = ScriptableObject.CreateInstance<FlowFieldProfile>();
        string path = "Assets/_Project/Data/FlowFieldProfile.asset";

        // Создаём папку, если не существует
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