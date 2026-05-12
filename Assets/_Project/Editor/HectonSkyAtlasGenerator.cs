// ============================================================================
// HECTON-8 - HectonAtlasPacker.cs
// One-click pipeline: AI-generated PNGs -> packed RGBA sky atlas.
//
// WORKFLOW:
//   1. Generate Density + Detail textures via AI (any size, any format)
//   2. Drop PNGs anywhere in Unity project
//   3. Tools -> Hecton -> Pack Sky Atlas
//   4. Pick density PNG, pick detail PNG
//   5. Script handles EVERYTHING:
//      - Resize to 2048^2
//      - Convert to grayscale
//      - Adjust contrast/levels
//      - Generate deterministic triangle-wave flowmap (B+A)
//      - Pack into single RGBA atlas
//      - Save as linear PNG with correct import settings
//
// NO PHOTOSHOP NEEDED. NO MANUAL STEPS.
//
// OUTPUT CHANNELS:
//   R = Cloud density (from AI texture, processed)
//   G = Detail erosion (from AI texture, softened)
//   B = Flowmap X (deterministic triangle-wave flow, 0.5 = neutral)
//   A = Flowmap Y (deterministic triangle-wave flow, 0.5 = neutral)
// ============================================================================

#if UNITY_EDITOR

using System.IO;
using Unity.Collections;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

namespace Hecton8.Editor
{
    public sealed class HectonAtlasPacker : EditorWindow
    {
        // ==========================================================
        //  CONSTANTS
        // ==========================================================

        private const int AtlasSize = 2048;
        private const string OutDir = "Assets/_Project/Art/Textures/Sky";
        private const string OutFile = "HectonSkyAtlas_RGBA.png";

        // ==========================================================
        //  PARAMETERS
        // ==========================================================

        // -- Density processing --
        private float _densityContrast   = 1.8f;
        private float _densityBrightness = -0.1f;
        private float _densityGamma      = 1.3f;

        // -- Detail processing --
        private float _detailOpacity     = 0.4f;
        private float _detailSoftness    = 0.7f;

        // -- Flowmap generation --
        private int   _flowSeed          = 42;
        private float _flowScale         = 0.4f;
        private float _flowIntensity     = 0.6f;
        private float _vortexBias        = 0.25f;
        private int   _flowEpsPx         = 8;

        // -- Source textures --
        private Texture2D _srcDensity;
        private Texture2D _srcDetail;

        // -- Preview --
        private Texture2D _preview;
        private int       _previewCh;
        private Vector2   _scroll;

        // ==========================================================
        //  MENU
        // ==========================================================

        [MenuItem("Tools/Hecton/Pack Sky Atlas", false, 201)]
        private static void Open()
        {
            var w = GetWindow<HectonAtlasPacker>(
                true, "Pack Sky Atlas", true);
            w.minSize = new Vector2(460, 700);
            w.Show();
        }

        // ==========================================================
        //  GUI
        // ==========================================================

        private void OnGUI()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            GUILayout.Space(8);
            EditorGUILayout.LabelField(
                "HECTON-8 ATLAS PACKER",
                EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                "AI textures -> RGBA atlas. No Photoshop.",
                EditorStyles.miniLabel);
            GUILayout.Space(12);

            // -- Source textures --
            EditorGUILayout.LabelField(
                "=== Source Textures ===",
                EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Drop any PNG/JPG from AI generator.\n" +
                "Any size - will be resized to 2048^2.\n" +
                "Any color - will be converted to grayscale.",
                MessageType.Info);

            _srcDensity = (Texture2D)EditorGUILayout.ObjectField(
                new GUIContent("Density (-> R)",
                    "Cloud shapes. White = cloud, black = clear sky."),
                _srcDensity, typeof(Texture2D), false);

            _srcDetail = (Texture2D)EditorGUILayout.ObjectField(
                new GUIContent("Detail (-> G)",
                    "Wispy erosion. Should be very subtle."),
                _srcDetail, typeof(Texture2D), false);

            GUILayout.Space(8);

            // -- Density processing --
            EditorGUILayout.LabelField(
                "=== Density Processing ===",
                EditorStyles.boldLabel);
            _densityContrast = EditorGUILayout.Slider(
                new GUIContent("Contrast",
                    "Sharpens cloud edges.\n1.0 = no change, 2.0 = sharp."),
                _densityContrast, 0.5f, 4f);
            _densityBrightness = EditorGUILayout.Slider(
                new GUIContent("Brightness",
                    "Shifts overall brightness.\n" +
                    "Negative = more black sky."),
                _densityBrightness, -0.5f, 0.5f);
            _densityGamma = EditorGUILayout.Slider(
                new GUIContent("Gamma",
                    "Power curve.\n>1 = denser cores, thinner edges."),
                _densityGamma, 0.3f, 3f);
            GUILayout.Space(6);

            // -- Detail processing --
            EditorGUILayout.LabelField(
                "=== Detail Processing ===",
                EditorStyles.boldLabel);
            _detailOpacity = EditorGUILayout.Slider(
                new GUIContent("Opacity",
                    "How visible the detail is.\n" +
                    "0.4 = subtle edge nibble."),
                _detailOpacity, 0.1f, 1f);
            _detailSoftness = EditorGUILayout.Slider(
                new GUIContent("Softness",
                    "Smoothstep range. Higher = gentler."),
                _detailSoftness, 0.2f, 1f);
            GUILayout.Space(6);

            // -- Flowmap --
            EditorGUILayout.LabelField(
                "=== Flowmap (Procedural) ===",
                EditorStyles.boldLabel);
            _flowSeed = EditorGUILayout.IntField(
                "Seed", _flowSeed);
            _flowScale = EditorGUILayout.Slider(
                new GUIContent("Scale",
                    "KEEP LOW. 0.3-0.5 = large smooth blobs."),
                _flowScale, 0.1f, 1.5f);
            _flowIntensity = EditorGUILayout.Slider(
                new GUIContent("Intensity",
                    "How strong the flow distortion is."),
                _flowIntensity, 0.1f, 2f);
            _vortexBias = EditorGUILayout.Slider(
                new GUIContent("Vortex",
                    "Planetary rotation swirl."),
                _vortexBias, 0f, 1f);
            _flowEpsPx = EditorGUILayout.IntSlider(
                new GUIContent("Smoothness (px)",
                    "Triangle-wave high-frequency damping. 8+ = smooth."),
                _flowEpsPx, 2, 16);
            GUILayout.Space(16);

            // -- Pack button --
            bool canPack = _srcDensity != null && _srcDetail != null;

            EditorGUI.BeginDisabledGroup(!canPack);
            GUI.backgroundColor = canPack
                ? new Color(0.1f, 0.7f, 0.3f)
                : Color.gray;

            if (GUILayout.Button(
                canPack
                    ? "PACK ATLAS"
                    : "Assign both textures above",
                GUILayout.Height(44)))
            {
                PackAtlas();
            }

            GUI.backgroundColor = Color.white;
            EditorGUI.EndDisabledGroup();

            GUILayout.Space(8);

            // -- Preview --
            if (_preview != null)
            {
                string[] ch = {
                    "RGBA", "R Density", "G Detail",
                    "B FlowX", "A FlowY"
                };
                _previewCh = GUILayout.Toolbar(_previewCh, ch);

                GUILayout.Space(4);
                float sz = Mathf.Min(position.width - 40f, 400f);
                Rect r = GUILayoutUtility.GetRect(sz, sz);
                EditorGUI.DrawPreviewTexture(r, _preview);
            }

            EditorGUILayout.EndScrollView();
        }

        private void OnDestroy()
        {
            if (_preview != null)
            {
                DestroyImmediate(_preview);
                _preview = null;
            }
        }

        // ==========================================================
        //  PACK PIPELINE
        // ==========================================================

        private void PackAtlas()
        {
            int N = AtlasSize;

            EditorUtility.DisplayProgressBar("Pack Atlas", "Loading...", 0f);

            // ==============================================
            // STEP 1: Load source textures as readable
            // ==============================================

            Texture2D densityReadable = MakeReadable(_srcDensity);
            Texture2D detailReadable  = MakeReadable(_srcDetail);

            if (densityReadable == null || detailReadable == null)
            {
                Debug.LogError("[AtlasPacker] Failed to read source textures.");
                EditorUtility.ClearProgressBar();
                return;
            }

            // ==============================================
            // STEP 2: Resize to atlas size
            // ==============================================

            EditorUtility.DisplayProgressBar("Pack Atlas", "Resizing...", 0.1f);

            Texture2D densityResized = Resize(densityReadable, N, N);
            Texture2D detailResized  = Resize(detailReadable, N, N);

            DestroyImmediate(densityReadable);
            DestroyImmediate(detailReadable);

            // ==============================================
            // STEP 3: Process + pack
            // ==============================================

            NativeArray<Color32> densityPx = densityResized.GetRawTextureData<Color32>();
            NativeArray<Color32> detailPx = detailResized.GetRawTextureData<Color32>();

            float inv = 1f / N;
            float flowScale = _flowScale * 4f + 0.25f;
            float flowFineWeight = 1f / (1f + _flowEpsPx * 0.12f);
            float2 flowOff = new float2(
                _flowSeed * 137.31f,
                _flowSeed * 271.17f);

            var atlas = new Texture2D(N, N, TextureFormat.RGBA32,
                false, true); // LINEAR
            atlas.name       = "HectonSkyAtlas_RGBA";
            atlas.wrapMode   = TextureWrapMode.Repeat;
            atlas.filterMode = FilterMode.Bilinear;
            NativeArray<Color32> output = atlas.GetRawTextureData<Color32>();

            for (int y = 0; y < N; y++)
            {
                if ((y & 127) == 0)
                {
                    EditorUtility.DisplayProgressBar(
                        "Pack Atlas",
                        $"Processing row {y}/{N}",
                        0.2f + 0.7f * y / N);
                }

                for (int x = 0; x < N; x++)
                {
                    int idx = y * N + x;
                    float u = x * inv;
                    float v = y * inv;

                    // -- R: Density (grayscale + levels) --
                    float r = Luminance(densityPx[idx]);

                    // Brightness
                    r += _densityBrightness;

                    // Contrast (around 0.5 midpoint)
                    r = (r - 0.5f) * _densityContrast + 0.5f;

                    // Gamma (cheap cinematic curve)
                    r = math.saturate(r);
                    r = FastGammaCurve(r, _densityGamma);

                    // -- G: Detail (grayscale + soften) --
                    float g = Luminance(detailPx[idx]);

                    // Smoothstep for soft transitions
                    g = math.smoothstep(0f, _detailSoftness, g);

                    // Reduce intensity
                    g *= _detailOpacity;

                    // -- B+A: Flowmap (deterministic triangle-wave fake) --
                    float2 flow = FastTriangleFlow(
                        u, v, flowScale, flowOff, flowFineWeight);

                    // Vortex bias
                    float2 toCenter = new float2(0.5f - u, 0.5f - v);
                    float dist = math.max(math.abs(toCenter.x), math.abs(toCenter.y));
                    float2 tangent = new float2(-toCenter.y, toCenter.x);
                    float tLen = math.max(math.abs(tangent.x), math.abs(tangent.y));
                    if (tLen > 0.001f) tangent /= tLen;

                    float vMask = math.smoothstep(1f, 0f, dist * 2.5f);
                    flow += tangent * _vortexBias * vMask;

                    // Encode [-1,1] -> [0,1], 0.5 = neutral
                    float b = math.saturate(
                        flow.x * _flowIntensity * 0.5f + 0.5f);
                    float a = math.saturate(
                        flow.y * _flowIntensity * 0.5f + 0.5f);

                    output[idx] = new Color32(
                        Encode01(r),
                        Encode01(g),
                        Encode01(b),
                        Encode01(a));
                }
            }

            DestroyImmediate(densityResized);
            DestroyImmediate(detailResized);

            // ==============================================
            // STEP 4: Save
            // ==============================================

            EditorUtility.DisplayProgressBar("Pack Atlas", "Saving...", 0.95f);

            atlas.Apply(false, false);

            if (!Directory.Exists(OutDir))
                Directory.CreateDirectory(OutDir);

            string path = Path.Combine(OutDir, OutFile);
            File.WriteAllBytes(path, atlas.EncodeToPNG());

            AssetDatabase.Refresh();
            SetImportSettings(path);

            // Preview
            if (_preview != null) DestroyImmediate(_preview);
            _preview = atlas;
            _previewCh = 0;

            EditorUtility.ClearProgressBar();

            Debug.Log(
                $"[AtlasPacker] OK Atlas packed!\n" +
                $"  {path}\n" +
                $"  {N}x{N}, Linear, BC7\n" +
                $"  R=Density G=Detail B=FlowX A=FlowY");

            EditorGUIUtility.PingObject(
                AssetDatabase.LoadAssetAtPath<Texture2D>(path));
        }

        // ==========================================================
        //  TEXTURE UTILITIES
        // ==========================================================

        /// <summary>
        /// Creates a readable copy of any texture.
        /// Handles compressed, non-readable, any format.
        /// </summary>
        private static Texture2D MakeReadable(Texture2D src)
        {
            if (src == null) return null;

            // Force readable via temporary RenderTexture
            RenderTexture tmp = RenderTexture.GetTemporary(
                src.width, src.height, 0,
                RenderTextureFormat.ARGB32,
                RenderTextureReadWrite.Linear);

            Graphics.Blit(src, tmp);

            RenderTexture prev = RenderTexture.active;
            RenderTexture.active = tmp;

            Texture2D readable = new Texture2D(
                src.width, src.height,
                TextureFormat.RGBA32, false, true);
            readable.ReadPixels(
                new Rect(0, 0, src.width, src.height), 0, 0);
            readable.Apply();

            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(tmp);

            return readable;
        }

        /// <summary>
        /// Bilinear resize to target dimensions.
        /// </summary>
        private static Texture2D Resize(
            Texture2D src, int width, int height)
        {
            RenderTexture tmp = RenderTexture.GetTemporary(
                width, height, 0,
                RenderTextureFormat.ARGB32,
                RenderTextureReadWrite.Linear);
            tmp.filterMode = FilterMode.Bilinear;

            Graphics.Blit(src, tmp);

            RenderTexture prev = RenderTexture.active;
            RenderTexture.active = tmp;

            Texture2D result = new Texture2D(
                width, height,
                TextureFormat.RGBA32, false, true);
            result.ReadPixels(
                new Rect(0, 0, width, height), 0, 0);
            result.Apply();

            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(tmp);

            return result;
        }

        /// <summary>
        /// Perceptual luminance (Rec.709).
        /// </summary>
        private static float Luminance(Color c)
        {
            return c.r * 0.2126f + c.g * 0.7152f + c.b * 0.0722f;
        }

        private static float Luminance(Color32 c)
        {
            return ((c.r * 0.2126f) + (c.g * 0.7152f) + (c.b * 0.0722f)) * (1f / 255f);
        }

        private static byte Encode01(float value)
        {
            return (byte)math.clamp((int)math.round(math.saturate(value) * 255f), 0, 255);
        }

        // ==========================================================
        //  IMPORT SETTINGS
        // ==========================================================

        private static void SetImportSettings(string path)
        {
            AssetDatabase.ImportAsset(path);
            var imp = AssetImporter.GetAtPath(path) as TextureImporter;
            if (imp == null) return;

            imp.textureType           = TextureImporterType.Default;
            imp.sRGBTexture           = false; // LINEAR
            imp.alphaSource           = TextureImporterAlphaSource.FromInput;
            imp.alphaIsTransparency   = false;
            imp.isReadable            = false;
            imp.wrapMode              = TextureWrapMode.Repeat;
            imp.filterMode            = FilterMode.Bilinear;
            imp.mipmapEnabled         = true;
            imp.maxTextureSize        = 2048;
            imp.textureCompression    =
                TextureImporterCompression.CompressedHQ;

            var pc = imp.GetPlatformTextureSettings("Standalone");
            pc.overridden         = true;
            pc.maxTextureSize     = 2048;
            pc.format             = TextureImporterFormat.BC7;
            pc.compressionQuality = 100;
            imp.SetPlatformTextureSettings(pc);
            imp.SaveAndReimport();
        }

        // ==========================================================
        //  DETERMINISTIC FLOW CHEATS (for flowmap B+A channels)
        // ==========================================================

        private static float FastGammaCurve(float value, float gamma)
        {
            value = math.saturate(value);
            float squared = value * value;
            float lifted = 1f - (1f - value) * (1f - value);
            float lowGamma = math.saturate(1f - gamma);
            float highGamma = math.saturate(gamma - 1f);
            return math.lerp(math.lerp(value, lifted, lowGamma), squared, highGamma);
        }

        private static float2 FastTriangleFlow(
            float u,
            float v,
            float scale,
            float2 offset,
            float fineWeight)
        {
            float seedA = offset.x * 0.00137f + offset.y * 0.00019f;
            float seedB = offset.y * 0.00191f - offset.x * 0.00023f;
            float baseA = FastSignedTriangle(u * (1f + scale) + v * 0.37f + seedA);
            float baseB = FastSignedTriangle(v * (1.2f + scale) - u * 0.29f + seedB);
            float fineA = FastSignedTriangle((u + v) * (2.3f + scale) + seedA * 0.5f);
            float fineB = FastSignedTriangle((u - v) * (2.7f + scale) + seedB * 0.5f);

            return new float2(
                baseB + fineA * fineWeight,
                -baseA + fineB * fineWeight) * 0.5f;
        }

        private static float FastSignedTriangle(float value)
        {
            float t = math.frac(value);
            return 1f - 4f * math.abs(t - 0.5f);
        }
    }
}

#endif
