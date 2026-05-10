#if UNITY_EDITOR
using System;
using System.IO;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEditor;
using UnityEngine;

namespace Hecton8.EditorTools
{
    /// <summary>
    /// Packs Metallic, AO, Smoothness, and Emissive Mask grayscale maps into one linear RGBA mask texture.
    /// </summary>
    internal static class HectonMaskChannelPacker
    {
        private const string MenuPath = "Hecton/Art Optimization/Pack Selected M.A.S.K.";
        private const string OutputFolder = "Assets/_Project/Art/TEXTURES/PackedMasks";
        private const int MaxPackedMaskSize = 2048;

        [MenuItem(MenuPath, priority = 210)]
        private static void PackSelectedMasks()
        {
            Texture2D metallic = null;
            Texture2D ao = null;
            Texture2D smoothness = null;
            Texture2D emissive = null;

            UnityEngine.Object[] selected = Selection.objects;
            for (int i = 0; i < selected.Length; i++)
            {
                Texture2D texture = selected[i] as Texture2D;
                if (texture == null)
                    continue;

                string path = AssetDatabase.GetAssetPath(texture);
                string lowerPath = path.ToLowerInvariant();
                if (lowerPath.Contains("metal"))
                    metallic = texture;
                else if (lowerPath.Contains("occlusion") || lowerPath.Contains("_ao") || lowerPath.Contains("ambient"))
                    ao = texture;
                else if (lowerPath.Contains("smooth") || lowerPath.Contains("rough"))
                    smoothness = texture;
                else if (lowerPath.Contains("emiss") || lowerPath.Contains("emit"))
                    emissive = texture;
            }

            if (metallic == null || ao == null || smoothness == null || emissive == null)
            {
                Debug.LogError("[HectonMaskChannelPacker] Select four textures named with metallic, AO/occlusion, smoothness/roughness, and emissive tokens.");
                return;
            }

            string outputPath = PackMasks(metallic, ao, smoothness, emissive);
            Debug.Log("[HectonMaskChannelPacker] Packed M.A.S.K. texture: " + outputPath);
        }

        internal static string PackMasks(Texture2D metallic, Texture2D ao, Texture2D smoothness, Texture2D emissive)
        {
            int width = ResolvePackDimension(metallic.width, ao.width, smoothness.width, emissive.width);
            int height = ResolvePackDimension(metallic.height, ao.height, smoothness.height, emissive.height);

            Texture2D packed = null;
            string outputPath;

            try
            {
                packed = new Texture2D(width, height, TextureFormat.RGBA32, true, true);
                NativeArray<Color32> packedPixels = packed.GetRawTextureData<Color32>();
                CopySourceRedToPackedChannel(metallic, width, height, packedPixels, 0);
                CopySourceRedToPackedChannel(ao, width, height, packedPixels, 1);
                CopySourceRedToPackedChannel(smoothness, width, height, packedPixels, 2);
                CopySourceRedToPackedChannel(emissive, width, height, packedPixels, 3);

                packed.Apply(true, false);

                EnsureFolder(OutputFolder);
                string outputName = "TX_MASK_" + HectonEditorMeshUtility.SanitizeAssetToken(metallic.name) + ".png";
                outputPath = AssetDatabase.GenerateUniqueAssetPath(OutputFolder + "/" + outputName);
                File.WriteAllBytes(outputPath, packed.EncodeToPNG());
            }
            finally
            {
                if (packed != null)
                    UnityEngine.Object.DestroyImmediate(packed);
            }

            AssetDatabase.ImportAsset(outputPath, ImportAssetOptions.ForceUpdate);
            TextureImporter importer = AssetImporter.GetAtPath(outputPath) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Default;
                importer.sRGBTexture = false;
                importer.mipmapEnabled = true;
                importer.isReadable = false;
                importer.textureCompression = TextureImporterCompression.Compressed;
                importer.maxTextureSize = MaxPackedMaskSize;

                TextureImporterPlatformSettings standalone = importer.GetPlatformTextureSettings("Standalone");
                standalone.overridden = true;
                standalone.format = TextureImporterFormat.BC7;
                standalone.maxTextureSize = MaxPackedMaskSize;
                standalone.textureCompression = TextureImporterCompression.Compressed;
                standalone.crunchedCompression = false;
                importer.SetPlatformTextureSettings(standalone);
                importer.SaveAndReimport();
            }

            return outputPath;
        }

        private static unsafe void CopySourceRedToPackedChannel(Texture source, int width, int height, NativeArray<Color32> packedPixels, int channel)
        {
            Texture2D readable = null;
            try
            {
                readable = CaptureReadableTexture(source, width, height);
                NativeArray<Color32> sourcePixels = readable.GetRawTextureData<Color32>();
                Color32* packedPtr = (Color32*)NativeArrayUnsafeUtility.GetUnsafePtr(packedPixels);
                Color32* sourcePtr = (Color32*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(sourcePixels);
                int pixelCount = packedPixels.Length;

                for (int i = 0; i < pixelCount; i++)
                {
                    byte value = sourcePtr[i].r;
                    Color32 pixel = packedPtr[i];
                    if (channel == 0)
                        pixel.r = value;
                    else if (channel == 1)
                        pixel.g = value;
                    else if (channel == 2)
                        pixel.b = value;
                    else
                        pixel.a = value;

                    packedPtr[i] = pixel;
                }
            }
            finally
            {
                if (readable != null)
                    UnityEngine.Object.DestroyImmediate(readable);
            }
        }

        private static Texture2D CaptureReadableTexture(Texture texture, int width, int height)
        {
            RenderTexture temp = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            RenderTexture previous = RenderTexture.active;
            Texture2D readable = new Texture2D(width, height, TextureFormat.RGBA32, false, true);
            bool returned = false;

            try
            {
                Graphics.Blit(texture, temp);
                RenderTexture.active = temp;
                readable.ReadPixels(new Rect(0f, 0f, width, height), 0, 0, false);
                readable.Apply(false, false);
                returned = true;
                return readable;
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(temp);
                if (!returned)
                    UnityEngine.Object.DestroyImmediate(readable);
            }
        }

        private static int ResolvePackDimension(params int[] dimensions)
        {
            int max = 1;
            for (int i = 0; i < dimensions.Length; i++)
                max = Mathf.Max(max, dimensions[i]);

            return Mathf.Min(MaxPackedMaskSize, Mathf.NextPowerOfTwo(max));
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;

            int slash = path.LastIndexOf('/');
            if (slash <= 0)
                return;

            string parent = path.Substring(0, slash);
            string folder = path.Substring(slash + 1);
            EnsureFolder(parent);
            if (!AssetDatabase.IsValidFolder(path))
                AssetDatabase.CreateFolder(parent, folder);
        }
    }
}
#endif
