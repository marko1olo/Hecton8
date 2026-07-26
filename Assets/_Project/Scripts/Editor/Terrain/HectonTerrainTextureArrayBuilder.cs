using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Hecton8.Editor.Terrain
{
    public class HectonTerrainTextureArrayBuilder : EditorWindow
    {
        private List<TerrainLayer> _layers = new List<TerrainLayer>();
        private string _exportPath = "Assets/_SourceData/Terrain/TextureArrays";
        // terrain bible budget: 3 arrays x 8 slices at 1024 = 32.0 MiB VRAM resident.
        // 2048 quadruples that to 128 MiB with no visual gain: the shader tiles at ~14 m,
        // which puts 2048 past the screen Nyquist within a few meters of the camera.
        private const int RequiredResolution = 1024;
        private int _resolution = RequiredResolution;
        private TextureFormat _formatAlbedo = TextureFormat.BC7;
        private TextureFormat _formatNormal = TextureFormat.BC5;
        private TextureFormat _formatMask = TextureFormat.BC7;

        [MenuItem("Hecton8/Terrain/Texture Array Builder")]
        public static void ShowWindow()
        {
            var window = GetWindow<HectonTerrainTextureArrayBuilder>("TexArray Builder");
            window.minSize = new Vector2(400, 550);
            window.Show();
        }

        private void OnGUI()
        {
            GUILayout.Label("Hecton-8 URP Terrain Texture Array Builder", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Packs multiple TerrainLayers into Texture2DArrays for the Single-Pass Custom URP Shader. Resolution is locked to 1024 by the terrain bible VRAM budget (32 MiB for all 3 arrays).", MessageType.Info);

            _resolution = EditorGUILayout.IntField("Target Resolution", _resolution);
            _exportPath = EditorGUILayout.TextField("Export Path", _exportPath);

            GUILayout.Space(10);
            GUILayout.Label("Terrain Layers (Order matters!)", EditorStyles.boldLabel);

            int layerCount = EditorGUILayout.IntField("Layer Count", _layers.Count);
            if (layerCount != _layers.Count)
            {
                while (_layers.Count < layerCount) _layers.Add(null);
                while (_layers.Count > layerCount) _layers.RemoveAt(_layers.Count - 1);
            }

            for (int i = 0; i < _layers.Count; i++)
            {
                _layers[i] = (TerrainLayer)EditorGUILayout.ObjectField($"Layer [{i}]", _layers[i], typeof(TerrainLayer), false);
            }

            GUILayout.Space(20);

            if (GUILayout.Button("Build Texture2D Arrays", GUILayout.Height(40)))
            {
                BuildArrays();
            }
        }

        private void BuildArrays()
        {
            if (_layers == null || _layers.Count == 0)
            {
                Debug.LogError("[TextureArrayBuilder] No layers specified.");
                return;
            }

            if (_resolution != RequiredResolution)
            {
                Debug.LogError($"[TextureArrayBuilder] Resolution must be {RequiredResolution} (bible VRAM budget: 32 MiB for 3 arrays; 2048 costs 128 MiB for detail past the ~14 m tiling Nyquist).");
                return;
            }

            if (!Directory.Exists(_exportPath))
            {
                Directory.CreateDirectory(_exportPath);
            }

            int depth = _layers.Count;
            Texture2DArray albedoArray = new Texture2DArray(_resolution, _resolution, depth, _formatAlbedo, true, false);
            Texture2DArray normalArray = new Texture2DArray(_resolution, _resolution, depth, _formatNormal, true, true);
            Texture2DArray maskArray = new Texture2DArray(_resolution, _resolution, depth, _formatMask, true, true);

            albedoArray.filterMode = FilterMode.Trilinear;
            normalArray.filterMode = FilterMode.Trilinear;
            maskArray.filterMode = FilterMode.Trilinear;
            
            albedoArray.anisoLevel = 8;
            normalArray.anisoLevel = 8;
            maskArray.anisoLevel = 8;
            
            albedoArray.wrapMode = TextureWrapMode.Repeat;
            normalArray.wrapMode = TextureWrapMode.Repeat;
            maskArray.wrapMode = TextureWrapMode.Repeat;

            bool success = true;

            for (int i = 0; i < depth; i++)
            {
                var layer = _layers[i];
                if (layer == null)
                {
                    Debug.LogError($"[TextureArrayBuilder] Layer at index {i} is null!");
                    success = false;
                    continue;
                }

                if (layer.diffuseTexture == null)
                {
                    Debug.LogError($"[TextureArrayBuilder] Layer '{layer.name}' has no Albedo/Diffuse texture.");
                }

                // 1. ALBEDO (sRGB = true)
                Texture2D albedoRead = GetReadableTexture(layer.diffuseTexture, _resolution, false, layer.diffuseRemapMin, layer.diffuseRemapMax);
                if (layer.diffuseTexture == null) Debug.LogError("Diffuse Texture is NULL for layer " + layer.name);
                if (albedoRead != null)
                {
                    EditorUtility.CompressTexture(albedoRead, _formatAlbedo, UnityEditor.TextureCompressionQuality.Best);
                    success &= TryCopySliceCpu(albedoRead, albedoArray, i, "albedo");
                    DestroyImmediate(albedoRead);
                }
                else
                {
                    Texture2D flatAlbedo = new Texture2D(4, 4, TextureFormat.RGBA32, false, false);
                    Color32[] colors = new Color32[16];
                    for (int c = 0; c < colors.Length; c++) colors[c] = new Color32(128, 128, 128, 255);
                    flatAlbedo.SetPixels32(colors);
                    flatAlbedo.Apply();
                    Texture2D albedoFallback = GetReadableTexture(flatAlbedo, _resolution, false, layer.diffuseRemapMin, layer.diffuseRemapMax);
                    EditorUtility.CompressTexture(albedoFallback, _formatAlbedo, UnityEditor.TextureCompressionQuality.Best);
                    success &= TryCopySliceCpu(albedoFallback, albedoArray, i, "albedo fallback");
                    DestroyImmediate(flatAlbedo);
                    DestroyImmediate(albedoFallback);
                }

                // 2. NORMAL (Linear = true)
                if (layer.normalMapTexture != null)
                {
                    Texture2D normalRead = GetReadableTexture(layer.normalMapTexture, _resolution, true, Vector4.zero, Vector4.one);
                    if (normalRead != null)
                    {
                        EditorUtility.CompressTexture(normalRead, _formatNormal, UnityEditor.TextureCompressionQuality.Best);
                        success &= TryCopySliceCpu(normalRead, normalArray, i, "normal");
                        DestroyImmediate(normalRead);
                    }
                }
                else
                {
                    // Fallback normal
                    Texture2D flatNormal = new Texture2D(4, 4, TextureFormat.RGBA32, false, true);
                    Color32[] colors = new Color32[16];
                    Color32 flatColor = new Color32(128, 128, 255, 255);
                    for (int c = 0; c < colors.Length; c++) colors[c] = flatColor;
                    flatNormal.SetPixels32(colors);
                    flatNormal.Apply();
                    Texture2D normalRead = GetReadableTexture(flatNormal, _resolution, true, Vector4.zero, Vector4.one);
                    EditorUtility.CompressTexture(normalRead, _formatNormal, UnityEditor.TextureCompressionQuality.Best);
                    success &= TryCopySliceCpu(normalRead, normalArray, i, "normal fallback");
                    DestroyImmediate(flatNormal);
                    DestroyImmediate(normalRead);
                }
                
                // 3. MASK / HEIGHT (Linear = true)
                if (_formatMask != TextureFormat.Alpha8 && layer.maskMapTexture != null)
                {
                    Texture2D maskRead = GetReadableTexture(layer.maskMapTexture, _resolution, true, Vector4.zero, Vector4.one);
                    if (maskRead != null)
                    {
                        EditorUtility.CompressTexture(maskRead, _formatMask, UnityEditor.TextureCompressionQuality.Best);
                        success &= TryCopySliceCpu(maskRead, maskArray, i, "mask");
                        DestroyImmediate(maskRead);
                    }
                }
                else if (_formatMask != TextureFormat.Alpha8)
                {
                    // Fallback to empty mask
                    Texture2D flatMask = new Texture2D(4, 4, TextureFormat.RGBA32, true, true);
                    Color32[] colors = new Color32[16];
                    for (int j = 0; j < 16; j++) colors[j] = new Color32(0, 0, 128, 0); // Neutral height in B
                    flatMask.SetPixels32(colors);
                    flatMask.Apply();
                    Texture2D maskRead = GetReadableTexture(flatMask, _resolution, true, Vector4.zero, Vector4.one);
                    EditorUtility.CompressTexture(maskRead, _formatMask, UnityEditor.TextureCompressionQuality.Best);
                    success &= TryCopySliceCpu(maskRead, maskArray, i, "mask fallback");
                    DestroyImmediate(flatMask);
                    DestroyImmediate(maskRead);
                }
            }

            if (success)
            {
                // AssetDatabase.CreateAsset serializes the CPU-side buffer, so it must stay
                // readable (makeNoLongerReadable would serialize freed memory) and must be
                // populated — which is why the slices are copied with SetPixelData above,
                // not Graphics.CopyTexture (GPU-only, leaves the CPU buffer uninitialized).
                albedoArray.Apply(false, false);
                normalArray.Apply(false, false);
                maskArray.Apply(false, false);

                if (IsSliceZeroConstant(albedoArray) || IsSliceZeroConstant(normalArray) || IsSliceZeroConstant(maskArray))
                {
                    Debug.LogError("[TextureArrayBuilder] Post-bake assert failed: slice 0 mip 0 is a constant fill in at least one array — refusing to serialize a garbage asset. Check source textures.");
                    return;
                }

                string albedoPath = $"{_exportPath}/Terrain_AlbedoArray.asset";
                string normalPath = $"{_exportPath}/Terrain_NormalArray.asset";
                string maskPath = $"{_exportPath}/Terrain_MaskArray.asset";

                if (File.Exists(albedoPath)) AssetDatabase.DeleteAsset(albedoPath);
                if (File.Exists(normalPath)) AssetDatabase.DeleteAsset(normalPath);
                if (File.Exists(maskPath)) AssetDatabase.DeleteAsset(maskPath);

                AssetDatabase.CreateAsset(albedoArray, albedoPath);
                AssetDatabase.CreateAsset(normalArray, normalPath);
                AssetDatabase.CreateAsset(maskArray, maskPath);
                AssetDatabase.SaveAssets();

                Debug.Log($"[TextureArrayBuilder] Successfully created 3 arrays at {_exportPath}");
            }
            else
            {
                Debug.LogError("[TextureArrayBuilder] Build failed due to missing or invalid textures. Check console.");
            }
        }

        /// <summary>
        /// Copies every mip of one compressed slice into the array on the CPU side, so the
        /// data survives AssetDatabase serialization. Graphics.CopyTexture is GPU-to-GPU and
        /// produced assets whose payload was raw uninitialized memory (0xCD fill).
        /// </summary>
        private static bool TryCopySliceCpu(Texture2D source, Texture2DArray target, int slice, string context)
        {
            if (source == null || target == null)
                return false;

            if (source.width != target.width ||
                source.height != target.height ||
                source.format != target.format ||
                source.mipmapCount < target.mipmapCount)
            {
                Debug.LogError($"[TextureArrayBuilder] {context} slice {slice}: source {source.width}x{source.height} {source.format} ({source.mipmapCount} mips) does not match array {target.width}x{target.height} {target.format} ({target.mipmapCount} mips).");
                return false;
            }

            for (int mip = 0; mip < target.mipmapCount; mip++)
            {
                target.SetPixelData(source.GetPixelData<byte>(mip), mip, slice);
            }

            return true;
        }

        private static bool IsSliceZeroConstant(Texture2DArray array)
        {
            var data = array.GetPixelData<byte>(0, 0);
            if (data.Length == 0)
                return true;

            byte first = data[0];
            for (int i = 1; i < data.Length; i++)
            {
                if (data[i] != first)
                    return false;
            }

            return true;
        }

        private Texture2D GetReadableTexture(Texture2D source, int targetRes, bool isLinear, Vector4 remapMin, Vector4 remapMax)
        {
            if (source == null)
            {
                Texture2D fallback = new Texture2D(targetRes, targetRes, TextureFormat.RGBA32, true, isLinear);
                Color[] colors = new Color[targetRes * targetRes];
                for (int i = 0; i < colors.Length; i++) colors[i] = Color.Lerp(remapMin, remapMax, 0.5f);
                fallback.SetPixels(colors);
                fallback.Apply();
                return fallback;
            }

            RenderTexture rt = RenderTexture.GetTemporary(targetRes, targetRes, 0, RenderTextureFormat.ARGB32, isLinear ? RenderTextureReadWrite.Linear : RenderTextureReadWrite.sRGB);
            UnityEngine.Graphics.Blit(source, rt);
            
            Texture2D readableText = new Texture2D(targetRes, targetRes, TextureFormat.RGBA32, true, isLinear);
            RenderTexture.active = rt;
            readableText.ReadPixels(new Rect(0, 0, targetRes, targetRes), 0, 0);
            readableText.Apply();
            RenderTexture.active = null;
            RenderTexture.ReleaseTemporary(rt);

            return readableText;
        }
    }
}

