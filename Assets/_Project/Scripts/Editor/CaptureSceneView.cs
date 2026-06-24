using UnityEditor;
using UnityEngine;
using System.IO;

[InitializeOnLoad]
public class CaptureSceneViewOnLoad
{
    static CaptureSceneViewOnLoad()
    {
        EditorApplication.delayCall += Capture;
    }

    static void Capture()
    {
        string outPath = "C:\\Users\\danat\\.gemini\\antigravity\\brain\\389e4a53-b1e6-440c-b190-0f5c509fa8c4\\SceneViewScreenshot.png";
        if (File.Exists(outPath)) {
            // Already captured this session, to prevent infinite loop just return.
            // But wait, if we delete it first before writing this script, it will capture.
        }

        var sceneView = SceneView.lastActiveSceneView;
        if (sceneView != null)
        {
            var cam = sceneView.camera;
            if (cam != null)
            {
                RenderTexture rt = new RenderTexture(1920, 1080, 24);
                cam.targetTexture = rt;
                cam.Render();
                RenderTexture.active = rt;
                Texture2D tex = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0);
                tex.Apply();
                cam.targetTexture = null;
                RenderTexture.active = null;
                GameObject.DestroyImmediate(rt);

                byte[] bytes = tex.EncodeToPNG();
                File.WriteAllBytes(outPath, bytes);
                Debug.Log($"[AgentCapture] Captured SceneView to {outPath}");

                // Now delete this script so it doesn't run again
                EditorApplication.delayCall += () => {
                    AssetDatabase.DeleteAsset("Assets/_Project/Scripts/Editor/CaptureSceneView.cs");
                };
            }
        }
    }
}
