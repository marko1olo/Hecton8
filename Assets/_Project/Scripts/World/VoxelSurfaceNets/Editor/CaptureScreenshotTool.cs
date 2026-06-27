using UnityEditor;
using UnityEngine;
using UnityEditor.SceneManagement;

[InitializeOnLoad]
public static class CaptureScreenshotTool
{
    private const string WaitingPref = "ScreenshotTool_Waiting";
    private static float? playModeStartTime = null;

    static CaptureScreenshotTool()
    {
        EditorApplication.update += EditorUpdate;
    }

    public static void RunCapture()
    {
        Debug.Log("RunCapture invoked. Loading scene 010_TEST...");
        EditorSceneManager.OpenScene("Assets/_Project/Scenes/010_TEST.unity");
        EditorPrefs.SetBool(WaitingPref, true);
        EditorApplication.isPlaying = true;
    }

    private static GameObject captureCamGo = null;

    private static void EditorUpdate()
    {
        if (!EditorPrefs.GetBool(WaitingPref, false))
            return;

        if (!EditorApplication.isPlaying)
            return;

        if (playModeStartTime == null)
        {
            playModeStartTime = Time.realtimeSinceStartup;
            Debug.Log($"Entered play mode. playModeStartTime set to {playModeStartTime}");
        }

        float elapsed = Time.realtimeSinceStartup - playModeStartTime.Value;
        
        // At 40 seconds, configure the capture camera
        if (elapsed > 40f && captureCamGo == null)
        {
            Debug.Log("--- CONFIGURING CAPTURE CAMERA ---");
            
            // Disable original main camera
            var oldCam = GameObject.FindWithTag("MainCamera");
            if (oldCam != null)
            {
                oldCam.SetActive(false);
                Debug.Log($"Disabled old main camera: {oldCam.name}");
            }
            
            // Create a new camera
            captureCamGo = new GameObject("CaptureCamera");
            var cam = captureCamGo.AddComponent<Camera>();
            captureCamGo.tag = "MainCamera";
            cam.clearFlags = CameraClearFlags.Skybox;
            
            // Try to find a tile
            Vector3 camPos = new Vector3(0, 300, 0);
            var transforms = GameObject.FindObjectsByType<Transform>();
            bool foundTile = false;
            foreach (var t in transforms)
            {
                if (t.name.StartsWith("Tile"))
                {
                    camPos = t.position + new Vector3(0, 250, 0);
                    Debug.Log($"Found tile {t.name} at {t.position}. Setting camera position to: {camPos}");
                    foundTile = true;
                    break;
                }
            }
            
            if (!foundTile)
            {
                Debug.LogWarning("No Tile GameObject found! Using default position (0, 300, 0).");
            }
            
            captureCamGo.transform.position = camPos;
            captureCamGo.transform.rotation = Quaternion.Euler(60, 45, 0);
            Debug.Log($"Created CaptureCamera at {camPos} looking down.");
        }

        // At 42 seconds, take the screenshot
        if (elapsed > 42f)
        {
            EditorPrefs.SetBool(WaitingPref, false);
            
            string path = "Docs/GeneratedAssets/Terrain/RuntimeScreenshot.png";
            System.IO.Directory.CreateDirectory("Docs/GeneratedAssets/Terrain");
            ScreenCapture.CaptureScreenshot(path);
            Debug.Log("Captured screenshot to: " + path);
            
            EditorApplication.isPlaying = false;
            
            // Give it 1 second to write file, then exit
            float exitTime = Time.realtimeSinceStartup + 1.0f;
            EditorApplication.update += ExitOnTimeout;
            
            void ExitOnTimeout()
            {
                if (Time.realtimeSinceStartup > exitTime)
                {
                    EditorApplication.update -= ExitOnTimeout;
                    if (captureCamGo != null)
                    {
                        GameObject.DestroyImmediate(captureCamGo);
                        captureCamGo = null;
                    }
                    EditorApplication.Exit(0);
                }
            }
        }
    }
}
