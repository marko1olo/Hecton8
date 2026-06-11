using UnityEditor;
using UnityEngine;

namespace Hecton8.EditorTools
{
    public class ScreenshotBehaviour : MonoBehaviour
    {
        private float _startTime;
        private int _state = 0;

        void Start()
        {
            _startTime = Time.realtimeSinceStartup;
        }

        void Update()
        {
            if (_state == 0)
            {
                if (Time.realtimeSinceStartup - _startTime < 5.0f)
                    return;
                
                _state = 1;
                Debug.Log("[ScreenshotTaker] Capturing screenshot 1...");
                CaptureScreenshot("terrain_runtime_1.png", new Vector3(0, 200, -200), Quaternion.Euler(45, 0, 0));
                
                _startTime = Time.realtimeSinceStartup;
            }
            else if (_state == 1)
            {
                if (Time.realtimeSinceStartup - _startTime < 1.0f)
                    return;

                _state = 2;
                Debug.Log("[ScreenshotTaker] Capturing screenshot 2...");
                CaptureScreenshot("terrain_runtime_2.png", new Vector3(500, 50, 500), Quaternion.Euler(15, 45, 0));
                
                _startTime = Time.realtimeSinceStartup;
            }
            else if (_state == 2)
            {
                if (Time.realtimeSinceStartup - _startTime < 1.0f)
                    return;

                Debug.Log("[ScreenshotTaker] Done. Exiting.");
                EditorPrefs.SetBool("H8_ScreenshotTaker_Active", false);
                EditorApplication.Exit(0);
            }
        }

        private void CaptureScreenshot(string filename, Vector3 pos, Quaternion rot)
        {
            Camera cam = Camera.main;
            if (cam == null) cam = GameObject.FindObjectOfType<Camera>();
            if (cam != null)
            {
                cam.transform.position = pos;
                cam.transform.rotation = rot;
            }
            else
            {
                Debug.LogWarning("[ScreenshotTaker] No camera found!");
                return;
            }

            int width = 1920;
            int height = 1080;
            RenderTexture rt = new RenderTexture(width, height, 24);
            cam.targetTexture = rt;
            
            Texture2D screenShot = new Texture2D(width, height, TextureFormat.RGB24, false);
            cam.Render();
            RenderTexture.active = rt;
            screenShot.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            screenShot.Apply();
            cam.targetTexture = null;
            RenderTexture.active = null; 
            Destroy(rt);

            byte[] bytes = screenShot.EncodeToPNG();
            string path = System.IO.Path.Combine(Application.dataPath, "../" + filename);
            System.IO.File.WriteAllBytes(path, bytes);
            Debug.Log($"[ScreenshotTaker] Saved {bytes.Length} bytes to {path}");
        }
    }

    public static class H8_ScreenshotTaker
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        public static void OnRuntimeLoad()
        {
            if (EditorPrefs.GetBool("H8_ScreenshotTaker_Active", false))
            {
                Debug.Log("[ScreenshotTaker] Runtime detected. Creating behaviour.");
                GameObject go = new GameObject("ScreenshotTakerObj");
                go.AddComponent<ScreenshotBehaviour>();
            }
        }

        public static void TakeScreenshotAndExit()
        {
            EditorPrefs.SetBool("H8_ScreenshotTaker_Active", true);
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/_Project/Scenes/02_HECTON_WORLD.unity");
            Debug.Log("[ScreenshotTaker] Entering PlayMode...");
            EditorApplication.isPlaying = true;
        }
    }
}
