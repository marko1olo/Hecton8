using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.IO;

public static class FixAndShoot
{
    static float timer = 0;
    static Camera cam;
    static string outDir;

    public static void Execute()
    {
        Debug.Log("[FAS] Execute started.");
        EditorSceneManager.OpenScene("Assets/_Project/Scenes/02_HECTON_WORLD.unity");

        cam = GameObject.Find("Main Camera")?.GetComponent<Camera>();
        if (cam == null) cam = Object.FindFirstObjectByType<Camera>();
        if (cam == null) cam = new GameObject("VerifyCam").AddComponent<Camera>();

        cam.clearFlags = CameraClearFlags.Skybox;
        var uacam = cam.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
        if (uacam == null) uacam = cam.gameObject.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
        uacam.renderPostProcessing = true;

        cam.farClipPlane = 10000f;
        cam.nearClipPlane = 0.3f;
        cam.fieldOfView = 60f;

        outDir = System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile) + "/.gemini/antigravity/brain/389e4a53-b1e6-440c-b190-0f5c509fa8c4";

        EditorApplication.update += OnUpdate;
    }

    static void OnUpdate()
    {
        timer += Time.unscaledDeltaTime;
        if (timer > 20f)
        {
            EditorApplication.update -= OnUpdate;
            Debug.Log("[FAS] 20 seconds passed, taking screenshot.");

            cam.transform.position = new Vector3(0, 150f, -300f);
            cam.transform.LookAt(new Vector3(0, 50f, 0));
            CaptureFromCamera(cam, outDir + "/Fixed_0.png");

            cam.transform.position = new Vector3(0, 30f, 0);
            cam.transform.rotation = Quaternion.Euler(0, 45, 0);
            CaptureFromCamera(cam, outDir + "/Fixed_1.png");

            Debug.Log("[FAS] Done.");
            EditorApplication.Exit(0);
        }
    }

    static void CaptureFromCamera(Camera cam, string path)
    {
        int w = 1920, h = 1080;
        var rt = new RenderTexture(w, h, 24);
        cam.targetTexture = rt;
        cam.Render();
        RenderTexture.active = rt;
        var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
        tex.Apply();
        cam.targetTexture = null;
        RenderTexture.active = null;
        Object.DestroyImmediate(rt);
        var bytes = tex.EncodeToPNG();
        Object.DestroyImmediate(tex);
        File.WriteAllBytes(path, bytes);
        Debug.Log("[FAS] Saved: " + path);
    }
}
