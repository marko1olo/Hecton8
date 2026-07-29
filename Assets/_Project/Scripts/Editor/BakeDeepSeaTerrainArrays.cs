using UnityEngine;
using UnityEditor;
using System.IO;

namespace Hecton8.Editor
{
    /// <summary>
    /// Bakes the 4-layer deep-sea terrain Texture2DArray pair consumed by the HectonTerrain
    /// single-pass shader. Route bible: terrain.md "Texture2DArray Baker Rule" names this file and
    /// mandates the Graphics.Blit -> RenderTexture uniform-1024 resize, because source PBR textures
    /// differ in resolution and Texture2DArray.SetPixels crashes on size mismatch.
    ///
    /// 2026-07-29 defect repair. The previous revision serialized 100% uninitialized memory: both
    /// DeepSea_*Array.asset payloads on disk measured 22,369,616 of 22,369,616 bytes equal to 0xCD.
    /// Root cause was Apply(true, true) - the second argument is makeNoLongerReadable, which frees
    /// the CPU-side buffer that AssetDatabase.CreateAsset then serializes. The same failure was
    /// already diagnosed and fixed in HectonTerrainTextureArrayBuilder.cs:192-195 and :228-231; that
    /// proven route (per-mip SetPixelData, then Apply(false, false)) is what this file now uses.
    ///
    /// Secondary repairs: the TextureFormat argument was accepted and ignored because the
    /// CompressTexture call was commented out, so both arrays shipped as uncompressed
    /// R8G8B8A8 (m_Format 4 / 8) against OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt:112
    /// "Format priority: BC7 -> BC3 -> BC1. Never uncompressed in VRAM unless RenderTexture."
    /// Compression is now real: BC7 albedo, BC5 normal, matching the formats the same shader
    /// already consumes from Terrain_AlbedoArray (RGBA_BC7_SRGB) and Terrain_NormalArray
    /// (RG_BC5_UNorm).
    /// </summary>
    public static class BakeDeepSeaTerrainArrays
    {
        private const string OutDir = "Assets/_SourceData/Terrain/TextureArrays";
        private const int Resolution = 1024;

        private static readonly string[] AlbedoPaths =
        {
            "Assets/_Project/Art/TEXTURES/Terrain Textures/sand/Ground079S_1K-PNG_Color.png",
            "Assets/_Project/Art/TEXTURES/Terrain Textures/gravel/Gravel020_1K-JPG_Color.jpg",
            "Assets/_Project/Art/TEXTURES/Terrain Textures/mud/Ground051_1K-JPG_Color.jpg",
            "Assets/_Project/Art/TEXTURES/Terrain Textures/basalt/Rock031_1K-JPG_Color.jpg"
        };

        private static readonly string[] NormalPaths =
        {
            "Assets/_Project/Art/TEXTURES/Terrain Textures/sand/NORMAL.png",
            "Assets/_Project/Art/TEXTURES/Terrain Textures/gravel/Gravel020_1K-JPG_NormalGL.jpg",
            "Assets/_Project/Art/TEXTURES/Terrain Textures/mud/Ground051_1K-JPG_NormalGL.jpg",
            "Assets/_Project/Art/TEXTURES/Terrain Textures/basalt/Rock031_1K-JPG_NormalGL.jpg"
        };

        [MenuItem("Hecton8/Terrain/Bake Deep Sea Arrays")]
        public static void BakeArrays()
        {
            TryBakeArrays();
        }

        /// <summary>
        /// Batchmode entry point. Exits non-zero on failure so a headless run cannot report success
        /// over a garbage or missing asset.
        /// Unity.exe -batchmode -quit -projectPath &lt;proj&gt; -executeMethod Hecton8.Editor.BakeDeepSeaTerrainArrays.ExecuteBatch
        /// -nographics is forbidden here: terrain.md "No -nographics flag ever" - Graphics.Blit
        /// returns zeros without a GPU context, and the resize below is a Blit.
        /// </summary>
        public static void ExecuteBatch()
        {
            bool ok = TryBakeArrays();
            EditorApplication.Exit(ok ? 0 : 1);
        }

        public static bool TryBakeArrays()
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
            {
                Debug.LogError("[BakeDeepSeaTerrainArrays] Aborted: no GPU context (graphicsDeviceType == Null). " +
                               "terrain.md bans -nographics for this bake because Graphics.Blit returns zeros " +
                               "without a device, which would serialize a uniform-fill array.");
                return false;
            }

            if (!Directory.Exists(OutDir))
            {
                Directory.CreateDirectory(OutDir);
            }

            // Layer 0: Sand (Ground079S). Layer 1: Gravel (Gravel020).
            // Layer 2: Silt/Mud (Ground051). Layer 3: Basalt Rock (Rock031).
            bool ok = BakeTexture2DArray(AlbedoPaths, OutDir + "/DeepSea_AlbedoArray.asset", TextureFormat.BC7, false);
            ok &= BakeTexture2DArray(NormalPaths, OutDir + "/DeepSea_NormalArray.asset", TextureFormat.BC5, true);

            if (ok)
            {
                Debug.Log("[BakeDeepSeaTerrainArrays] Baked DeepSea albedo (BC7) and normal (BC5) arrays with verified non-constant payloads.");
            }
            else
            {
                Debug.LogError("[BakeDeepSeaTerrainArrays] Bake FAILED. No array was written for the failing set. Check the errors above.");
            }

            return ok;
        }

        private static bool BakeTexture2DArray(string[] paths, string outputPath, TextureFormat format, bool isNormalMap)
        {
            if (paths.Length == 0)
            {
                Debug.LogError($"[BakeDeepSeaTerrainArrays] {outputPath}: empty source path set.");
                return false;
            }

            int depth = paths.Length;

            // COLD ALLOC: Texture2DArray[1024x1024x4 + mips] - offline terrain layer bake, editor only - owner: BakeDeepSeaTerrainArrays
            // Created directly in the final compressed format so the per-mip SetPixelData copy below
            // is a format-identical blit of the compressed blocks.
            Texture2DArray array = new Texture2DArray(Resolution, Resolution, depth, format, true, isNormalMap);
            array.anisoLevel = 16;
            array.filterMode = FilterMode.Trilinear;
            array.wrapMode = TextureWrapMode.Repeat;

            bool success = true;

            for (int i = 0; i < depth; i++)
            {
                Texture2D source = AssetDatabase.LoadAssetAtPath<Texture2D>(paths[i]);
                if (source == null)
                {
                    Debug.LogError($"[BakeDeepSeaTerrainArrays] Missing source texture: {paths[i]}");
                    success = false;
                    continue;
                }

                if (isNormalMap)
                {
                    EnsureNormalMapImport(paths[i], ref source);
                }

                // Bible-mandated resize route: Blit every source into a uniform 1024 RenderTexture,
                // then ReadPixels. This is what makes mismatched source resolutions legal here and
                // it needs no readable/uncompressed coercion on the source asset.
                Texture2D readable = BlitToReadable(source, isNormalMap);
                if (readable == null)
                {
                    Debug.LogError($"[BakeDeepSeaTerrainArrays] Resize failed for {paths[i]}");
                    success = false;
                    continue;
                }

                EditorUtility.CompressTexture(readable, format, UnityEditor.TextureCompressionQuality.Best);
                success &= TryCopySliceCpu(readable, array, i, paths[i]);
                Object.DestroyImmediate(readable);
            }

            if (!success)
            {
                Object.DestroyImmediate(array);
                return false;
            }

            // Every mip of every slice was written explicitly above, so mips must NOT be
            // regenerated, and the CPU buffer must stay readable - AssetDatabase.CreateAsset
            // serializes that buffer, and makeNoLongerReadable:true is what produced the 0xCD asset.
            array.Apply(false, false);

            int constantSlice = FindConstantFillSlice(array);
            if (constantSlice >= 0)
            {
                Debug.LogError($"[BakeDeepSeaTerrainArrays] Post-bake assert FAILED for {outputPath}: " +
                               $"slice {constantSlice} mip 0 is a single repeated byte value. Refusing to serialize a " +
                               "garbage array. This is the guard that the 0xCD-filled asset shipped without.");
                Object.DestroyImmediate(array);
                return false;
            }

            if (File.Exists(outputPath))
            {
                AssetDatabase.DeleteAsset(outputPath);
            }

            AssetDatabase.CreateAsset(array, outputPath);
            AssetDatabase.SaveAssets();
            return true;
        }

        /// <summary>
        /// Coerces the source asset to NormalMap import type when needed, so the blitted pixels are
        /// the tangent-space encoding the terrain shader expects. Compression and readability of the
        /// source are deliberately left alone: the Blit path does not need either, and forcing
        /// Uncompressed on shared project textures would violate the BC7/BC5 texture default.
        /// </summary>
        private static void EnsureNormalMapImport(string assetPath, ref Texture2D source)
        {
            TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null || importer.textureType == TextureImporterType.NormalMap)
            {
                return;
            }

            importer.textureType = TextureImporterType.NormalMap;
            importer.SaveAndReimport();
            source = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
        }

        private static Texture2D BlitToReadable(Texture2D source, bool isLinear)
        {
            RenderTexture tempRT = RenderTexture.GetTemporary(
                Resolution,
                Resolution,
                0,
                RenderTextureFormat.ARGB32,
                isLinear ? RenderTextureReadWrite.Linear : RenderTextureReadWrite.sRGB);

            UnityEngine.Graphics.Blit(source, tempRT);

            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = tempRT;

            Texture2D readable = new Texture2D(Resolution, Resolution, TextureFormat.RGBA32, true, isLinear);
            readable.ReadPixels(new Rect(0, 0, Resolution, Resolution), 0, 0);
            readable.Apply(true, false);

            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(tempRT);

            return readable;
        }

        /// <summary>
        /// Copies every mip of one compressed slice on the CPU side so the data survives
        /// AssetDatabase serialization. Mirrors HectonTerrainTextureArrayBuilder.TryCopySliceCpu:
        /// GPU-only copies leave the serialized CPU buffer as raw uninitialized memory.
        /// </summary>
        private static bool TryCopySliceCpu(Texture2D source, Texture2DArray target, int slice, string context)
        {
            if (source.width != target.width ||
                source.height != target.height ||
                source.format != target.format ||
                source.mipmapCount < target.mipmapCount)
            {
                Debug.LogError($"[BakeDeepSeaTerrainArrays] slice {slice} ({context}): source " +
                               $"{source.width}x{source.height} {source.format} ({source.mipmapCount} mips) does not match array " +
                               $"{target.width}x{target.height} {target.format} ({target.mipmapCount} mips).");
                return false;
            }

            for (int mip = 0; mip < target.mipmapCount; mip++)
            {
                target.SetPixelData(source.GetPixelData<byte>(mip), mip, slice);
            }

            return true;
        }

        /// <summary>
        /// Returns the index of the first slice whose mip 0 is one repeated byte, or -1 when every
        /// slice carries varying data. A uniform fill is the exact signature of the shipped 0xCD
        /// asset and of a Blit that ran without a GPU context.
        /// </summary>
        private static int FindConstantFillSlice(Texture2DArray array)
        {
            for (int slice = 0; slice < array.depth; slice++)
            {
                var data = array.GetPixelData<byte>(0, slice);
                if (data.Length == 0)
                {
                    return slice;
                }

                byte first = data[0];
                bool varies = false;
                for (int i = 1; i < data.Length; i++)
                {
                    if (data[i] != first)
                    {
                        varies = true;
                        break;
                    }
                }

                if (!varies)
                {
                    return slice;
                }
            }

            return -1;
        }
    }
}
