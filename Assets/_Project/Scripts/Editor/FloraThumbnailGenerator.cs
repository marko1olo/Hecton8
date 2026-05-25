using System;
using System.IO;
using Hecton8.World;
using Unity.Collections;
using UnityEditor;
using UnityEngine;

namespace Hecton8.EditorTools
{
    public static class FloraThumbnailGenerator
    {
        private const string TemplateRoot = "Assets/_Project/Data/World/FloraTemplates";
        private const string ThumbnailRoot = "Assets/_Project/Art/Sprites/FloraThumbnails";
        private const int ThumbnailSize = 128;

        [MenuItem("Hecton/Authoring/Generate Flora Template Thumbnails", priority = 219)]
        public static void GenerateThumbnails()
        {
            EnsureFolder("Assets/_Project/Art", "Sprites");
            EnsureFolder("Assets/_Project/Art/Sprites", "FloraThumbnails");

            string[] templateGuids = AssetDatabase.FindAssets("t:FloraDataTemplate", new[] { TemplateRoot });
            Array.Sort(templateGuids, StringComparer.Ordinal);
            for (int i = 0; i < templateGuids.Length; i++)
            {
                string templatePath = AssetDatabase.GUIDToAssetPath(templateGuids[i]);
                FloraDataTemplate template = AssetDatabase.LoadAssetAtPath<FloraDataTemplate>(templatePath);
                if (template == null)
                    continue;

                Texture2D thumbnail = BuildThumbnail(template);
                if (thumbnail == null)
                    continue;

                string thumbnailPath = $"{ThumbnailRoot}/{Path.GetFileNameWithoutExtension(templatePath)}_Thumb.png";
                WritePngBytes(thumbnailPath, thumbnail.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(thumbnail);
            }

            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            string[] thumbnailGuids = AssetDatabase.FindAssets("t:Texture2D", new[] { ThumbnailRoot });
            Array.Sort(thumbnailGuids, StringComparer.Ordinal);
            for (int i = 0; i < thumbnailGuids.Length; i++)
                ConfigureThumbnailImporter(AssetDatabase.GUIDToAssetPath(thumbnailGuids[i]));

            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            for (int i = 0; i < templateGuids.Length; i++)
            {
                string templatePath = AssetDatabase.GUIDToAssetPath(templateGuids[i]);
                FloraDataTemplate template = AssetDatabase.LoadAssetAtPath<FloraDataTemplate>(templatePath);
                if (template == null)
                    continue;

                string thumbnailPath = $"{ThumbnailRoot}/{Path.GetFileNameWithoutExtension(templatePath)}_Thumb.png";
                Texture2D thumbnailAsset = AssetDatabase.LoadAssetAtPath<Texture2D>(thumbnailPath);
                if (thumbnailAsset == null)
                    continue;

                EditorGUIUtility.SetIconForObject(template, thumbnailAsset);
                EditorUtility.SetDirty(template);
            }

            AssetDatabase.SaveAssets();
        }

        private static void EnsureFolder(string parentPath, string folderName)
        {
            string combinedPath = $"{parentPath}/{folderName}";
            if (AssetDatabase.IsValidFolder(combinedPath))
                return;

            AssetDatabase.CreateFolder(parentPath, folderName);
        }

        private static void ConfigureThumbnailImporter(string assetPath)
        {
            TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null)
                return;

            importer.textureType = TextureImporterType.Sprite;
            importer.spritePixelsPerUnit = ThumbnailSize;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Compressed;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;

            TextureImporterPlatformSettings standalone = importer.GetPlatformTextureSettings("Standalone");
            standalone.overridden = true;
            standalone.format = TextureImporterFormat.BC7;
            standalone.maxTextureSize = ThumbnailSize;
            standalone.textureCompression = TextureImporterCompression.Compressed;
            standalone.crunchedCompression = false;
            importer.SetPlatformTextureSettings(standalone);

            importer.SaveAndReimport();
        }

        private static Texture2D BuildThumbnail(FloraDataTemplate template)
        {
            Texture2D texture = new Texture2D(ThumbnailSize, ThumbnailSize, TextureFormat.RGBA32, false, false)
            {
                name = $"{template.name}_ThumbRuntime"
            };

            NativeArray<Color32> pixels = texture.GetRawTextureData<Color32>();
            FillBackground(pixels);
            DrawAttachmentBase(pixels, template.AttachmentSurfaceType);
            DrawFloraSilhouette(pixels, template);
            ApplyBiolumHalo(pixels, template.BioluminescenceColor);

            texture.Apply(false, false);
            return texture;
        }

        private static void FillBackground(NativeArray<Color32> pixels)
        {
            Color top = new Color(0.03f, 0.08f, 0.14f, 1f);
            Color bottom = new Color(0.0f, 0.01f, 0.03f, 1f);
            for (int y = 0; y < ThumbnailSize; y++)
            {
                float t = y / (float)(ThumbnailSize - 1);
                Color row = Color.Lerp(bottom, top, t);
                Color32 rowColor = row;
                int rowStart = y * ThumbnailSize;
                for (int x = 0; x < ThumbnailSize; x++)
                    pixels[rowStart + x] = rowColor;
            }
        }

        private static void DrawAttachmentBase(NativeArray<Color32> pixels, FloraDataTemplate.AttachmentSurface attachmentSurface)
        {
            switch (attachmentSurface)
            {
                case FloraDataTemplate.AttachmentSurface.Metal:
                    DrawRect(pixels, 18, 12, 92, 16, new Color(0.19f, 0.24f, 0.29f, 1f));
                    DrawRect(pixels, 16, 10, 96, 2, new Color(0.42f, 0.47f, 0.52f, 1f));
                    break;
                case FloraDataTemplate.AttachmentSurface.Rock:
                    DrawDisc(pixels, 64f, 18f, 26f, new Color(0.18f, 0.16f, 0.14f, 1f));
                    DrawDisc(pixels, 46f, 12f, 12f, new Color(0.24f, 0.22f, 0.2f, 1f));
                    DrawDisc(pixels, 82f, 12f, 14f, new Color(0.22f, 0.2f, 0.18f, 1f));
                    break;
                case FloraDataTemplate.AttachmentSurface.Seabed:
                    DrawDisc(pixels, 64f, 12f, 30f, new Color(0.29f, 0.22f, 0.12f, 1f));
                    DrawDisc(pixels, 44f, 10f, 14f, new Color(0.34f, 0.28f, 0.16f, 1f));
                    DrawDisc(pixels, 84f, 10f, 15f, new Color(0.32f, 0.25f, 0.15f, 1f));
                    break;
                default:
                    DrawDisc(pixels, 64f, 16f, 18f, new Color(0.08f, 0.16f, 0.22f, 0.65f));
                    break;
            }
        }

        private static void DrawFloraSilhouette(NativeArray<Color32> pixels, FloraDataTemplate template)
        {
            Color glow = template.BioluminescenceColor;
            Color stalk = Color.Lerp(glow, Color.white, 0.32f);

            switch (template.VegetationType)
            {
                case HectonVegetationInstanceType.Grass:
                    DrawBladeFan(pixels, stalk, 5, 0.72f, 0.12f);
                    break;
                case HectonVegetationInstanceType.GiantKelp:
                    DrawBladeFan(pixels, stalk, 3, 0.94f, 0.26f);
                    DrawBladeFan(pixels, Color.Lerp(stalk, glow, 0.45f), 2, 0.62f, -0.22f);
                    break;
                case HectonVegetationInstanceType.Sargassum:
                    DrawCanopyCluster(pixels, stalk, glow);
                    break;
                default:
                    DrawBladeFan(pixels, stalk, 4, 0.8f, 0f);
                    break;
            }
        }

        private static void ApplyBiolumHalo(NativeArray<Color32> pixels, Color biolumColor)
        {
            Color halo = new Color(biolumColor.r, biolumColor.g, biolumColor.b, Mathf.Clamp01(biolumColor.a * 0.18f));
            for (int y = 24; y < 116; y++)
            {
                for (int x = 20; x < 108; x++)
                {
                    float dx = (x - 64f) / 38f;
                    float dy = (y - 72f) / 44f;
                    float falloff = Mathf.Clamp01(1f - ((dx * dx) + (dy * dy)));
                    if (falloff <= 0f)
                        continue;

                    BlendPixel(pixels, x, y, halo * (falloff * 0.42f));
                }
            }
        }

        private static void DrawBladeFan(NativeArray<Color32> pixels, Color color, int bladeCount, float heightScale, float bendDirection)
        {
            float baseY = 20f;
            float apexY = Mathf.Lerp(74f, 116f, Mathf.Clamp01(heightScale));
            for (int bladeIndex = 0; bladeIndex < bladeCount; bladeIndex++)
            {
                float bladeOffset = bladeCount <= 1 ? 0f : Mathf.Lerp(-18f, 18f, bladeIndex / (float)(bladeCount - 1));
                Vector2 start = new Vector2(64f + bladeOffset * 0.3f, baseY);
                Vector2 control = new Vector2(64f + bladeOffset + (bendDirection * 22f), Mathf.Lerp(52f, 94f, heightScale));
                Vector2 end = new Vector2(64f + bladeOffset * 1.1f + (bendDirection * 16f), apexY);
                DrawQuadraticCurve(pixels, start, control, end, Mathf.Lerp(3.5f, 6.5f, heightScale), color);
            }
        }

        private static void DrawCanopyCluster(NativeArray<Color32> pixels, Color stalkColor, Color canopyColor)
        {
            DrawQuadraticCurve(pixels, new Vector2(64f, 22f), new Vector2(58f, 54f), new Vector2(60f, 84f), 4.5f, stalkColor);
            DrawQuadraticCurve(pixels, new Vector2(70f, 18f), new Vector2(76f, 48f), new Vector2(74f, 76f), 4f, stalkColor);
            DrawDisc(pixels, 54f, 86f, 11f, canopyColor * 1.1f);
            DrawDisc(pixels, 70f, 90f, 13f, canopyColor * 1.08f);
            DrawDisc(pixels, 62f, 102f, 10f, canopyColor * 0.92f);
            DrawDisc(pixels, 80f, 82f, 8f, Color.Lerp(stalkColor, canopyColor, 0.65f));
        }

        private static void DrawQuadraticCurve(NativeArray<Color32> pixels, Vector2 start, Vector2 control, Vector2 end, float thickness, Color color)
        {
            const int steps = 42;
            Vector2 previous = start;
            for (int i = 1; i <= steps; i++)
            {
                float t = i / (float)steps;
                float invT = 1f - t;
                Vector2 point = (invT * invT * start) + (2f * invT * t * control) + (t * t * end);
                DrawLine(pixels, previous, point, thickness, color);
                previous = point;
            }
        }

        private static void DrawLine(NativeArray<Color32> pixels, Vector2 start, Vector2 end, float thickness, Color color)
        {
            float length = FastLineLength(start, end);
            int steps = Mathf.Max(2, Mathf.CeilToInt(length * 1.5f));
            for (int i = 0; i <= steps; i++)
            {
                float t = i / (float)steps;
                Vector2 point = Vector2.Lerp(start, end, t);
                DrawDisc(pixels, point.x, point.y, thickness * Mathf.Lerp(1f, 0.52f, t), color);
            }
        }

        private static void DrawRect(NativeArray<Color32> pixels, int xMin, int yMin, int width, int height, Color color)
        {
            int xMax = Mathf.Min(ThumbnailSize, xMin + width);
            int yMax = Mathf.Min(ThumbnailSize, yMin + height);
            for (int y = Mathf.Max(0, yMin); y < yMax; y++)
            {
                for (int x = Mathf.Max(0, xMin); x < xMax; x++)
                    BlendPixel(pixels, x, y, color);
            }
        }

        private static void DrawDisc(NativeArray<Color32> pixels, float centerX, float centerY, float radius, Color color)
        {
            int xMin = Mathf.Max(0, Mathf.FloorToInt(centerX - radius));
            int xMax = Mathf.Min(ThumbnailSize - 1, Mathf.CeilToInt(centerX + radius));
            int yMin = Mathf.Max(0, Mathf.FloorToInt(centerY - radius));
            int yMax = Mathf.Min(ThumbnailSize - 1, Mathf.CeilToInt(centerY + radius));
            float radiusSq = radius * radius;
            for (int y = yMin; y <= yMax; y++)
            {
                float dy = y - centerY;
                for (int x = xMin; x <= xMax; x++)
                {
                    float dx = x - centerX;
                    float distanceSq = (dx * dx) + (dy * dy);
                    if (distanceSq > radiusSq)
                        continue;

                    float softness = 1f - Mathf.Clamp01(distanceSq / radiusSq);
                    BlendPixel(pixels, x, y, color * Mathf.Lerp(0.38f, 1f, softness));
                }
            }
        }

        private static void BlendPixel(NativeArray<Color32> pixels, int x, int y, Color source)
        {
            if ((uint)x >= ThumbnailSize || (uint)y >= ThumbnailSize)
                return;

            int index = (y * ThumbnailSize) + x;
            Color destination = pixels[index];
            float alpha = Mathf.Clamp01(source.a);
            Color blended = Color.Lerp(destination, new Color(source.r, source.g, source.b, 1f), alpha);
            pixels[index] = blended;
        }

        private static float FastLineLength(Vector2 start, Vector2 end)
        {
            float dx = Mathf.Abs(end.x - start.x);
            float dy = Mathf.Abs(end.y - start.y);
            float max = Mathf.Max(dx, dy);
            float min = Mathf.Min(dx, dy);
            return max + min * 0.375f;
        }

        private static void WritePngBytes(string thumbnailPath, byte[] pngBytes)
        {
            using FileStream stream = new FileStream(thumbnailPath, FileMode.Create, FileAccess.Write, FileShare.None);
            stream.Write(pngBytes, 0, pngBytes.Length);
            stream.Flush(true);
        }
    }
}
