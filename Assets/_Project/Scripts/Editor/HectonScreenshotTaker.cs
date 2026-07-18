using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.Collections;

public static class HectonScreenshotTaker
{
    [MenuItem("Tools/Hecton/Take Underwater Screenshot")]
    public static void TakeScreenshot()
    {
        EditorSceneManager.OpenScene("Assets/_Project/Scenes/02_HECTON_WORLD.unity");
        EditorApplication.isPlaying = true;
        EditorApplication.update += WaitAndCapture;
    }

    private static int framesWaited = 0;
    
    private static void WaitAndCapture()
    {
        if (!Application.isPlaying) return;
        
        framesWaited++;
        if (framesWaited == 200) // Wait for ~200 frames for generation and lighting
        {
            var cam = Camera.main;
            if (cam != null) {
                // Move camera underwater roughly above shelf
                cam.transform.position = new Vector3(0, -60, 0);
            }
        }
        
        if (framesWaited == 400)
        {
            ScreenCapture.CaptureScreenshot("C:/Users/Admin/.gemini/antigravity/brain/7b5d06d2-b333-42a8-ad13-119572c28fd0/underwater_capture.png");
            Debug.Log("[SCREENSHOT] Capture taken!");
        }
        
        if (framesWaited > 450)
        {
            EditorApplication.isPlaying = false;
            EditorApplication.Exit(0);
        }
    }
}
