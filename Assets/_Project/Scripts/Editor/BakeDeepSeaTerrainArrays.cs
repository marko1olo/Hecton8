using UnityEngine;
using UnityEditor;
using System.IO;

namespace Hecton8.Editor
{
    public static class BakeDeepSeaTerrainArrays
    {
        [MenuItem("Hecton8/Terrain/Bake Deep Sea Arrays")]
        public static void BakeArrays()
        {
            string outDir = "Assets/_SourceData/Terrain/TextureArrays/";
            if (!Directory.Exists(outDir)) Directory.CreateDirectory(outDir);

            // Layer 0: Sand (Ground079S)
            // Layer 1: Gravel (Gravel020)
            // Layer 2: Silt/Mud (Ground051)
            // Layer 3: Basalt Rock (Rock031)
            
            string[] albedoPaths = new string[]
            {
                "Assets/_Project/Art/TEXTURES/Terrain Textures/sand/Ground079S_1K-PNG_Color.png",
                "Assets/_Project/Art/TEXTURES/Terrain Textures/gravel/Gravel020_1K-JPG_Color.jpg",
                "Assets/_Project/Art/TEXTURES/Terrain Textures/mud/Ground051_1K-JPG_Color.jpg",
                "Assets/_Project/Art/TEXTURES/Terrain Textures/basalt/Rock031_1K-JPG_Color.jpg"
            };

            string[] normalPaths = new string[]
            {
                "Assets/_Project/Art/TEXTURES/Terrain Textures/sand/NORMAL.png",
                "Assets/_Project/Art/TEXTURES/Terrain Textures/gravel/Gravel020_1K-JPG_NormalGL.jpg",
                "Assets/_Project/Art/TEXTURES/Terrain Textures/mud/Ground051_1K-JPG_NormalGL.jpg",
                "Assets/_Project/Art/TEXTURES/Terrain Textures/basalt/Rock031_1K-JPG_NormalGL.jpg"
            };

            BakeTexture2DArray(albedoPaths, outDir + "DeepSea_AlbedoArray.asset", TextureFormat.BC7, true, false);
            BakeTexture2DArray(normalPaths, outDir + "DeepSea_NormalArray.asset", TextureFormat.BC7, true, true);
            
            Debug.Log("[BakeDeepSeaTerrainArrays] Successfully baked Deep Sea Texture Arrays.");
        }

        private static void BakeTexture2DArray(string[] paths, string outputPath, TextureFormat format, bool mipChains, bool isNormalMap = false)
        {
            if (paths.Length == 0) return;

            Texture2D firstTex = AssetDatabase.LoadAssetAtPath<Texture2D>(paths[0]);
            if (firstTex == null)
            {
                Debug.LogError($"[BakeDeepSeaTerrainArrays] Missing first texture: {paths[0]}");
                return;
            }

            int width = 1024; // Force 1K standard
            int height = 1024;
            int depth = paths.Length;

            // Use RGBA32 for array creation, because SetPixels is not supported on compressed formats like BC7.
            // The asset will be saved as RGBA32 on disk.
            Texture2DArray array = new Texture2DArray(width, height, depth, TextureFormat.RGBA32, mipChains, isNormalMap);
            array.anisoLevel = 16;
            array.filterMode = FilterMode.Trilinear;
            array.wrapMode = TextureWrapMode.Repeat;

            for (int i = 0; i < depth; i++)
            {
                Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(paths[i]);
                if (tex == null)
                {
                    Debug.LogError($"[BakeDeepSeaTerrainArrays] Missing texture: {paths[i]}");
                    continue;
                }

                // Ensure readable
                string assetPath = AssetDatabase.GetAssetPath(tex);
                TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
                if (importer != null)
                {
                    bool needReimport = false;
                    if (!importer.isReadable) { importer.isReadable = true; needReimport = true; }
                    if (isNormalMap && importer.textureType != TextureImporterType.NormalMap) { importer.textureType = TextureImporterType.NormalMap; needReimport = true; }
                    
                    // We need it uncompressed to read pixels if format mismatch
                    if (importer.textureCompression != TextureImporterCompression.Uncompressed || importer.crunchedCompression)
                    {
                        importer.textureCompression = TextureImporterCompression.Uncompressed;
                        importer.crunchedCompression = false;
                        needReimport = true;
                    }

                    if (needReimport)
                    {
                        importer.SaveAndReimport();
                        tex = AssetDatabase.LoadAssetAtPath<Texture2D>(paths[i]);
                    }
                }

                // Use RenderTexture to extract pixels, avoiding all readable/compression issues
                // Better approach: Blit mip 0 into exactly width x height, and use array.Apply(true) to generate mips
                RenderTexture tempRT = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32, isNormalMap ? RenderTextureReadWrite.Linear : RenderTextureReadWrite.sRGB);
                UnityEngine.Graphics.Blit(tex, tempRT);
                
                RenderTexture previous = RenderTexture.active;
                RenderTexture.active = tempRT;
                
                Texture2D tempTex = new Texture2D(width, height, TextureFormat.RGBA32, false, isNormalMap);
                tempTex.ReadPixels(new Rect(0, 0, tempTex.width, tempTex.height), 0, 0);
                tempTex.Apply();
                
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(tempRT);
                
                Color[] pixels = tempTex.GetPixels();
                array.SetPixels(pixels, i, 0); // Only set mip 0
                
                GameObject.DestroyImmediate(tempTex);
            }

            // Apply and generate mipmaps
            array.Apply(true, true);

            // Compress the array after filling it
            // EditorUtility.CompressTexture(array, format, UnityEditor.TextureCompressionQuality.Best);

            AssetDatabase.CreateAsset(array, outputPath);
            AssetDatabase.SaveAssets();
        }
    }
}
