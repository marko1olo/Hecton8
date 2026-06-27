#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEditor.SceneManagement;

namespace Hecton8.Editor
{
    public static class DeselectGizmosScan
    {
        [MenuItem("Hecton8/Tests/Scan Scene Hierarchy")]
        public static void Scan()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/020_RENDER_SANDBOX_V2.unity");
            var allGo = Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include);
            Debug.Log($"[SCAN] Total GameObjects: {allGo.Length}");
            using (var writer = new System.IO.StreamWriter("C:/Users/Admin/.gemini/antigravity/brain/9412af70-ebf5-491e-80e6-e0b2fcde1017/scene_hierarchy_log.txt"))
            {
                foreach (var go in allGo)
                {
                    string path = GetGameObjectPath(go);
                    writer.WriteLine($"GO: '{path}' (active={go.activeSelf})");
                    var comps = go.GetComponents<Component>();
                    foreach (var c in comps)
                    {
                        if (c == null) continue;
                        writer.WriteLine($"  COMP: {c.GetType().FullName}");
                    }
                }
            }
            Debug.Log("[SCAN] Scene hierarchy written to scene_hierarchy_log.txt");
        }

        private static string GetGameObjectPath(GameObject go)
        {
            string path = go.name;
            while (go.transform.parent != null)
            {
                go = go.transform.parent.gameObject;
                path = go.name + "/" + path;
            }
            return path;
        }
    }
}
#endif
