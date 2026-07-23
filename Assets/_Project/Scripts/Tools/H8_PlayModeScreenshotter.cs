#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using System.IO;

namespace Hecton8.Tools
{
    public class H8_PlayModeScreenshotter : MonoBehaviour
    {
        private float _timeoutWait = 0f;
        private int _waitFrames = 0;
        private GameObject _cachedPlayer;

        // Prevent running every frame indefinitely by spacing out the search
        private float _searchTimer = 0f;

        void Update()
        {
            _timeoutWait += Time.unscaledDeltaTime;

            var player = _cachedPlayer;
            if (player == null)
            {
                _searchTimer -= Time.unscaledDeltaTime;
                if (_searchTimer <= 0f)
                {
                    player = GameObject.FindWithTag("Player");
                    if (player == null) player = UnityEngine.Object.FindAnyObjectByType<Hecton8.Gameplay.HectonPlayerMovement>()?.gameObject;
                    if (player != null) _cachedPlayer = player;

                    // Only check every 1 second instead of every frame
                    _searchTimer = 1.0f;
                }
            }

            // Wait up to 180s for massive scenes to boot
            if (_cachedPlayer != null || _timeoutWait > 180f)
            {
                _waitFrames++;
                if (_waitFrames > 600) // wait an extra 600 frames (10s) for bootstrap and physics/water to settle
                {
                    enabled = false;
                    CaptureAndExit(_cachedPlayer);
                }
            }
        }

        private void CaptureAndExit(GameObject player)
        {
            Debug.Log($"[H8PlayModeScreenshotter] Capture started. Player spawned: {player != null}");

            Camera targetCam = null;
            if (player != null)
            {
                targetCam = player.GetComponentInChildren<Camera>();
            }

            if (targetCam == null)
            {
                targetCam = Camera.main;
            }

            if (targetCam == null)
            {
                var camGO = new GameObject("Fallback Camera");
                targetCam = camGO.AddComponent<Camera>();
                targetCam.transform.position = new Vector3(0, 10, 0);
            }

            // Capture
            int W = 1920, H_RES = 1080;
            var rt = new RenderTexture(W, H_RES, 32, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            rt.Create();

            var urpPipeline = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (urpPipeline != null)
            {
                var request = new UniversalRenderPipeline.SingleCameraRequest();
                request.destination = RTHandles.Alloc(rt);
                if (RenderPipeline.SupportsRenderRequest(targetCam, request))
                {
                    RenderPipeline.SubmitRenderRequest(targetCam, request);
                    request.destination.Release();
                }
                else
                {
                    targetCam.targetTexture = rt;
                    targetCam.Render();
                    targetCam.targetTexture = null;
                }
            }
            else
            {
                targetCam.targetTexture = rt;
                targetCam.Render();
                targetCam.targetTexture = null;
            }

            var tex = new Texture2D(W, H_RES, TextureFormat.RGB24, false);
            RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, W, H_RES), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            rt.Release();
            DestroyImmediate(rt);

            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string outputDir = Path.Combine(projectRoot, "Logs", "Screenshots");
            Directory.CreateDirectory(outputDir);
            string outPath = Path.Combine(outputDir, "shot_02_PLAYMODE_ACTUAL.png");
            File.WriteAllBytes(outPath, tex.EncodeToPNG());
            DestroyImmediate(tex);

            Debug.Log($"[H8PlayModeScreenshotter] Saved -> {outPath}");

            UnityEditor.EditorApplication.isPlaying = false;
            UnityEditor.EditorApplication.Exit(0);
        }
    }
}
#endif
