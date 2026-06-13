using UnityEngine;
using UnityEditor;
using System.IO;

[InitializeOnLoad]
public class ScreenshotGrabber {
    static float timer = 0f;
    static bool taken = false;

    static ScreenshotGrabber() {
        EditorApplication.update += OnUpdate;
    }

    static void OnUpdate() {
        if (taken) return;
        timer += Time.unscaledDeltaTime;
        
        // Wait 40 seconds for MapMagic to generate terrain in GUI mode
        if (timer > 40f) {
            taken = true;
            Directory.CreateDirectory("Screenshots");
            string path = "Screenshots/final_geology_direct.png";
            
            // Try to focus Scene View
            var sceneView = SceneView.lastActiveSceneView;
            if (sceneView != null) {
                sceneView.Focus();
                sceneView.camera.transform.position = new Vector3(0, 1500, -2000);
                sceneView.camera.transform.LookAt(new Vector3(0, 0, 0));
                sceneView.AlignViewToObject(sceneView.camera.transform);
            }
            
            ScreenCapture.CaptureScreenshot(path);
            
            Debug.Log("Screenshot grabbed successfully!");
            
            // Exit after a short delay to allow file write
            EditorApplication.delayCall += () => {
                System.Threading.Thread.Sleep(2000);
                EditorApplication.Exit(0);
            };
        }
    }
}
