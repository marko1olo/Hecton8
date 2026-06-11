using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using System.IO;

public static class DestroyWaterBatch {
    public static void Run() {
        var scene = EditorSceneManager.OpenScene("Assets/_Project/Scenes/010_TEST.unity", OpenSceneMode.Single);

        bool changed = false;

        var allGos = Resources.FindObjectsOfTypeAll<GameObject>();
        for (int i = 0; i < allGos.Length; i++) {
            var go = allGos[i];
            if (go == null || go.scene != scene) continue;
            
            string name = go.name.ToLower();
            if (name.Contains("crest") || name.Contains("aegir") || name.Contains("ocean") || name.Contains("water") || name.Contains("photic")) {
                Debug.Log("Antigravity: Destroying water object -> " + go.name);
                Object.DestroyImmediate(go, true);
                changed = true;
            }
        }

        if (changed) {
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("Antigravity: Saved scene 010_TEST after destroying water.");
        } else {
            Debug.Log("Antigravity: No water found in 010_TEST.");
        }
    }
}
