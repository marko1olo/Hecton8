using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.IO;

public static class FixAndShoot
{
    public static void Execute()
    {
        UnityEditor.EditorApplication.update += OnUpdate;
    }

    private static int frames = 0;
    private static void OnUpdate()
    {
        if (frames == 0)
        {
            EditorSceneManager.OpenScene("C:/hades/Hecton8/Assets/_Project/Scenes/020_RENDER_SANDBOX.unity");
        }
        else if (frames == 120)
        {
            TakeScreenshot();
        }
        else if (frames > 150)
        {
            UnityEditor.EditorApplication.update -= OnUpdate;
            EditorApplication.Exit(0);
        }
        frames++;
    }

    private static void TakeScreenshot()
    {
        // FIND EXISTING CAMERA INSTEAD OF CREATING ONE
        Camera cam = Camera.main;
        if (cam == null) {
            if (Camera.allCamerasCount > 0) { cam = Camera.allCameras[0]; }

        }
        
        if (cam == null) {
            Debug.Log("[FAS] Still no camera, creating one.");
            GameObject camGO = new GameObject("Main Camera");
            cam = camGO.AddComponent<Camera>();
        }

        Terrain t = Terrain.activeTerrain;
        if (t != null && t.terrainData != null)
        {
            Texture2D[] alphamaps = t.terrainData.alphamapTextures;
            MaterialPropertyBlock block = new MaterialPropertyBlock();
            t.GetSplatMaterialPropertyBlock(block);
            if (alphamaps.Length > 0) block.SetTexture("_Control", alphamaps[0]);
            if (alphamaps.Length > 1) block.SetTexture("_Control1", alphamaps[1]);
            t.SetSplatMaterialPropertyBlock(block);

            Vector3 center = t.transform.position + new Vector3(t.terrainData.size.x * 0.5f, 0, t.terrainData.size.z * 0.5f);
            float h = t.SampleHeight(center);
            cam.transform.position = center + new Vector3(0, h + 20f, -20f);
            cam.transform.LookAt(center + new Vector3(0, h, 0));
            
            // Force enable post processing on the camera if it's URP
            var camData = cam.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            if (camData != null) camData.renderPostProcessing = true;
        }

        RenderTexture rt = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32);
        cam.targetTexture = rt;
        cam.Render();
        RenderTexture.active = rt;
        Texture2D tex = new Texture2D(1920, 1080, TextureFormat.RGB24, false, true);
        tex.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0);
        tex.Apply();
        cam.targetTexture = null;
        RenderTexture.active = null;

        byte[] bytes = tex.EncodeToPNG();
        string path = "C:/Users/danat/.gemini/antigravity/brain/389e4a53-b1e6-440c-b190-0f5c509fa8c4/Fixed_Final.png";
        File.WriteAllBytes(path, bytes);
        Debug.Log("[FAS] Dumped " + path);
    }
}
