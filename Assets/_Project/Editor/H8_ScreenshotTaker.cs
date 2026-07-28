// H8_ScreenshotTaker.cs
// Editor-only. Renders scene cameras via URP RenderPipeline.SubmitRenderRequest.
// cam.Render() alone does NOT invoke URP in batchmode — SubmitRenderRequest is required.
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using System.IO;

namespace Hecton8.EditorTools
{
    public static class H8_ScreenshotTaker
    {
        private static readonly string OutputDir = "C:/hades/Hecton8/Logs/Screenshots/";

        public static void TakeScreenshotSandboxAndExit()
        {
            TakeSceneScreenshot("020", "Assets/_Project/Scenes/020_RENDER_SANDBOX.unity");
            EditorApplication.Exit(0);
        }

        public static void TakeScreenshotAndExit()
        {
            TakeSceneScreenshot("02", "Assets/_Project/Scenes/02_HECTON_WORLD.unity");
            EditorApplication.Exit(0);
        }

        public static void TakeSceneScreenshot(string suffix, string scenePath)
        {
            Directory.CreateDirectory(OutputDir);
            Debug.Log($"[H8Screenshot] Loading scene: {scenePath}");
            EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

            // Use scene camera or create survey cam
            var cam = Object.FindAnyObjectByType<Camera>();
            bool ownedCam = false;
            GameObject surveyGO = null;

            if (cam == null)
            {
                surveyGO = new GameObject("H8_SurveyCam");
                cam = surveyGO.AddComponent<Camera>();
                cam.transform.position = new Vector3(0f, 150f, -200f);
                cam.transform.rotation = Quaternion.Euler(25f, 0f, 0f);
                cam.nearClipPlane = 1f;
                cam.farClipPlane = 50000f;
                cam.clearFlags = CameraClearFlags.Skybox;
                ownedCam = true;
                Debug.Log("[H8Screenshot] Created survey camera (no cam found in scene).");
            }
            else
            {
                Debug.Log($"[H8Screenshot] Using scene camera: {cam.name} at {cam.transform.position}");
            }

            const int W = 1920, H_RES = 1080;
            var rt = new RenderTexture(W, H_RES, 32, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            rt.Create();

            string outPath = $"{OutputDir}shot_{suffix}.png";

            // URP-aware render: use SubmitRenderRequest — the ONLY correct path in batchmode
            var urpPipeline = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (urpPipeline != null)
            {
                var request = new UniversalRenderPipeline.SingleCameraRequest();
                request.destination = RTHandles.Alloc(rt);
                if (RenderPipeline.SupportsRenderRequest(cam, request))
                {
                    RenderPipeline.SubmitRenderRequest(cam, request);
                    Debug.Log("[H8Screenshot] Used URP SubmitRenderRequest.");
                    // DO NOT Release() the destination here. SingleCameraRequest.destination is
                    // declared `public RenderTexture destination` (URP
                    // Runtime/UniversalRenderPipeline.cs:2706), and RTHandles.Alloc(rt) converts
                    // implicitly to it - so this call was RenderTexture.Release(), freeing the GPU
                    // surface that ReadPixels reads a few lines below. Unity lazily recreates a
                    // released RenderTexture with undefined contents, so the capture returned a
                    // blank frame no matter what the camera saw. The caller already releases and
                    // destroys `rt` after ReadPixels, so this was also a double release.
                }
                else
                {
                    // Fallback: will be clearColor in batchmode but doesn't crash
                    cam.targetTexture = rt;
                    cam.Render();
                    cam.targetTexture = null;
                    Debug.LogWarning("[H8Screenshot] SubmitRenderRequest not supported — fallback to cam.Render().");
                }
            }
            else
            {
                cam.targetTexture = rt;
                cam.Render();
                cam.targetTexture = null;
                Debug.LogWarning("[H8Screenshot] No URP pipeline asset found — using cam.Render() fallback.");
            }

            var tex = new Texture2D(W, H_RES, TextureFormat.RGB24, false);
            RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, W, H_RES), 0, 0);
            tex.Apply();
            RenderTexture.active = null;

            rt.Release();
            Object.DestroyImmediate(rt);

            File.WriteAllBytes(outPath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);

            if (ownedCam && surveyGO != null)
                Object.DestroyImmediate(surveyGO);

            Debug.Log($"[H8Screenshot] Saved -> {outPath}");
        }
    }
}
