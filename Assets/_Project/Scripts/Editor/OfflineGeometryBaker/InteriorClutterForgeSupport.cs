#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Hecton8.Editor.OfflineGeometry
{
    internal struct InteriorClutterBlackBoxSession : IDisposable
    {
        private NativeArray<InteriorClutterTelemetryEntry> _ring;
        private int _cursor;
        private int _written;

        internal static InteriorClutterBlackBoxSession Create()
        {
            var session = new InteriorClutterBlackBoxSession
            {
                // COLD ALLOC: NativeArray<InteriorClutterTelemetryEntry>[300] - per-bake editor black-box session - owner: InteriorClutterForge
                _ring = new NativeArray<InteriorClutterTelemetryEntry>(InteriorClutterForgeConstants.TelemetryFrames, Allocator.TempJob, NativeArrayOptions.UninitializedMemory),
                _cursor = 0,
                _written = 0
            };
            session.ResetRing();
            return session;
        }

        internal void Record(in InteriorClutterBakeMetric metric)
        {
            if (!_ring.IsCreated)
                return;

            int frameIndex = _cursor;
            int index = frameIndex % _ring.Length;
            _cursor = frameIndex + 1;
            if (_written < _ring.Length)
                _written++;

            _ring[index] = new InteriorClutterTelemetryEntry
            {
                FrameIndex = (uint)frameIndex,
                RoomHash = InteriorClutterForge.StableHash(metric.SourcePath),
                StaticRendererCount = metric.StaticRenderers,
                InteractiveRendererCount = metric.InteractiveRenderers,
                Lod0Triangles = metric.Lod0Triangles,
                Lod1Triangles = metric.Lod1Triangles,
                Lod2Triangles = metric.Lod2Triangles,
                WarningFlags = (uint)metric.WarningFlags,
                BurstTransformMilliseconds = metric.BurstTransformMilliseconds,
                SerializationMilliseconds = metric.SerializationMilliseconds,
                VertexHash = ((ulong)(uint)metric.Lod0Triangles << 32) | (uint)math.max(0, metric.Lod2Triangles)
            };
        }

        internal void RecordFailure(string sourcePath, InteriorClutterWarningFlags flags)
        {
            InteriorClutterBakeMetric metric = default;
            metric.SourcePath = sourcePath;
            metric.WarningFlags = flags;
            Record(in metric);
        }

        internal unsafe void Dump(string reason)
        {
            if (!_ring.IsCreated)
                return;

            string path = "Docs/AgentLogs/Dump_SHINOBU_211.bin";
            InteriorClutterForge.EnsureFileFolder(path);
            int entrySize = UnsafeUtility.SizeOf<InteriorClutterTelemetryEntry>();
            int ringLength = _ring.Length;
            byte* basePtr = (byte*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(_ring);
            using (var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))
            {
                if (_written < ringLength)
                {
                    WriteRange(stream, basePtr, entrySize, 0, _written);
                }
                else
                {
                    int start = _cursor % ringLength;
                    WriteRange(stream, basePtr, entrySize, start, ringLength - start);
                    WriteRange(stream, basePtr, entrySize, 0, start);
                }
            }

            File.WriteAllText("Docs/AgentLogs/Dump_SHINOBU_211.reason.txt", (reason ?? "UNSPECIFIED") + " entries=" + _written, new UTF8Encoding(false));
        }

        private static unsafe void WriteRange(FileStream stream, byte* basePtr, int entrySize, int start, int count)
        {
            if (count <= 0)
                return;

            stream.Write(new ReadOnlySpan<byte>(basePtr + start * entrySize, count * entrySize));
        }

        private void ResetRing()
        {
            if (!_ring.IsCreated)
                return;

            for (int i = 0; i < _ring.Length; i++)
                _ring[i] = default;
        }

        public void Dispose()
        {
            if (_ring.IsCreated)
                _ring.Dispose();
            _ring = default;
        }
    }

    internal static class InteriorAtlasProfileCsv
    {
        internal static unsafe List<InteriorAtlasProfile> LoadProfiles()
        {
            var profiles = new List<InteriorAtlasProfile>(8);
            string projectRoot = Application.dataPath.Substring(0, Application.dataPath.Length - "/Assets".Length);
            string fullPath = Path.Combine(projectRoot, InteriorClutterForgeConstants.AtlasProfileCsvPath);
            if (!File.Exists(fullPath))
            {
                profiles.Add(DefaultProfile());
                return profiles;
            }

            NativeArray<byte> bytes = default;
            int length = 0;
            try
            {
                using (FileStream stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    if (stream.Length <= 0L || stream.Length > int.MaxValue)
                    {
                        profiles.Add(DefaultProfile());
                        return profiles;
                    }

                    length = (int)stream.Length;
                    // COLD ALLOC: NativeArray<byte>[csvLength] - editor CSV staging for atlas profiles - owner: InteriorAtlasProfileCsv
                    bytes = new NativeArray<byte>(length, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
                    byte* ptr = (byte*)NativeArrayUnsafeUtility.GetUnsafePtr(bytes);
                    Span<byte> span = new Span<byte>(ptr, length);
                    int totalRead = 0;
                    while (totalRead < length)
                    {
                        int read = stream.Read(span.Slice(totalRead));
                        if (read <= 0)
                            break;
                        totalRead += read;
                    }

                    length = totalRead;
                }

                int cursor = 0;
                SkipLine(bytes, length, ref cursor);
                while (cursor < length)
                {
                    SkipBlank(bytes, length, ref cursor);
                    if (cursor >= length)
                        break;

                    InteriorAtlasProfile profile = DefaultProfile();
                    profile.Name = ReadFixedString(bytes, length, ref cursor, profile.Name);
                    profile.AtlasSize = math.clamp(ReadInt(bytes, length, ref cursor, profile.AtlasSize), 256, 8192);
                    profile.MaxTileSize = math.clamp(ReadInt(bytes, length, ref cursor, profile.MaxTileSize), 16, profile.AtlasSize);
                    profile.Lod1Ratio = math.saturate(ReadFloat(bytes, length, ref cursor, profile.Lod1Ratio));
                    profile.Lod2Ratio = math.saturate(ReadFloat(bytes, length, ref cursor, profile.Lod2Ratio));
                    profile.GlobalQualityWeight = math.saturate(ReadFloat(bytes, length, ref cursor, profile.GlobalQualityWeight));
                    SkipLine(bytes, length, ref cursor);
                    profiles.Add(profile);
                }
            }
            finally
            {
                if (bytes.IsCreated)
                    bytes.Dispose();
            }

            if (profiles.Count == 0)
                profiles.Add(DefaultProfile());
            return profiles;
        }

        internal static InteriorAtlasProfile DefaultProfile()
        {
            return new InteriorAtlasProfile
            {
                Name = new FixedString64Bytes("MX350_Interior_4K"),
                AtlasSize = 4096,
                MaxTileSize = 512,
                Lod1Ratio = 0.48f,
                Lod2Ratio = 0.12f,
                GlobalQualityWeight = 0.42f
            };
        }

        private static FixedString64Bytes ReadFixedString(NativeArray<byte> bytes, int length, ref int cursor, FixedString64Bytes fallback)
        {
            FixedString64Bytes value = default;
            while (cursor < length)
            {
                byte b = bytes[cursor++];
                if (b == ',' || b == '\n' || b == '\r')
                    break;
                if (value.Length < FixedString64Bytes.UTF8MaxLengthInBytes)
                    value.Add(b);
            }

            return value.Length == 0 ? fallback : value;
        }

        private static int ReadInt(NativeArray<byte> bytes, int length, ref int cursor, int fallback)
        {
            float value = ReadFloat(bytes, length, ref cursor, fallback);
            return math.isfinite(value) ? (int)math.round(value) : fallback;
        }

        private static float ReadFloat(NativeArray<byte> bytes, int length, ref int cursor, float fallback)
        {
            SkipColumnWhitespace(bytes, length, ref cursor);
            bool negative = false;
            if (cursor < length && bytes[cursor] == '-')
            {
                negative = true;
                cursor++;
            }

            bool hasDigit = false;
            double value = 0d;
            while (cursor < length)
            {
                byte b = bytes[cursor];
                if (b < '0' || b > '9')
                    break;
                hasDigit = true;
                value = value * 10d + (b - '0');
                cursor++;
            }

            if (cursor < length && bytes[cursor] == '.')
            {
                cursor++;
                double scale = 0.1d;
                while (cursor < length)
                {
                    byte b = bytes[cursor];
                    if (b < '0' || b > '9')
                        break;
                    hasDigit = true;
                    value += (b - '0') * scale;
                    scale *= 0.1d;
                    cursor++;
                }
            }

            SkipToNextColumn(bytes, length, ref cursor);
            if (!hasDigit)
                return fallback;

            float result = (float)(negative ? -value : value);
            return math.isfinite(result) ? result : fallback;
        }

        private static void SkipColumnWhitespace(NativeArray<byte> bytes, int length, ref int cursor)
        {
            while (cursor < length && (bytes[cursor] == ' ' || bytes[cursor] == '\t'))
                cursor++;
        }

        private static void SkipToNextColumn(NativeArray<byte> bytes, int length, ref int cursor)
        {
            while (cursor < length)
            {
                byte b = bytes[cursor++];
                if (b == ',' || b == '\n')
                    return;
                if (b == '\r')
                {
                    if (cursor < length && bytes[cursor] == '\n')
                        cursor++;
                    return;
                }
            }
        }

        private static void SkipLine(NativeArray<byte> bytes, int length, ref int cursor)
        {
            while (cursor < length)
            {
                byte b = bytes[cursor++];
                if (b == '\n')
                    break;
            }
        }

        private static void SkipBlank(NativeArray<byte> bytes, int length, ref int cursor)
        {
            while (cursor < length && (bytes[cursor] == '\n' || bytes[cursor] == '\r'))
                cursor++;
        }
    }

    internal readonly struct InteriorMaterialAtlas
    {
        public readonly List<InteriorClutterAtlasRect> Rects;
        public readonly Material Material;

        public InteriorMaterialAtlas(List<InteriorClutterAtlasRect> rects, Material material)
        {
            Rects = rects;
            Material = material;
        }
    }

    internal enum InteriorAtlasTextureRole
    {
        Albedo,
        Normal,
        Mask
    }

    internal static class InteriorMaterialAtlasBuilder
    {
        private struct FreeRect
        {
            public int X;
            public int Y;
            public int Width;
            public int Height;
        }

        internal static InteriorMaterialAtlas Build(string prefabPath, List<Material> materials, InteriorAtlasProfile profile, ref InteriorClutterBakeMetric metric)
        {
            string token = InteriorClutterForge.SanitizeToken(Path.GetFileNameWithoutExtension(prefabPath));
            int atlasSize = math.max(256, profile.AtlasSize);
            var rects = new List<InteriorClutterAtlasRect>(materials.Count);
            PackRects(materials, profile, rects);
            NativeArray<InteriorClutterAtlasRect> nativeRects = default;
            NativeArray<InteriorClutterAtlasColor> nativeColors = default;
            Texture2D albedo;
            Texture2D normal;
            Texture2D mask;
            try
            {
                // COLD ALLOC: NativeArray<InteriorClutterAtlasRect>[materials.Count] - editor atlas rect feed - owner: InteriorMaterialAtlasBuilder
                nativeRects = new NativeArray<InteriorClutterAtlasRect>(rects.Count, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                // COLD ALLOC: NativeArray<InteriorClutterAtlasColor>[materials.Count] - editor atlas color feed - owner: InteriorMaterialAtlasBuilder
                nativeColors = new NativeArray<InteriorClutterAtlasColor>(rects.Count, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                for (int i = 0; i < rects.Count; i++)
                {
                    Material material = i < materials.Count ? materials[i] : null;
                    nativeRects[i] = rects[i];
                    nativeColors[i] = new InteriorClutterAtlasColor
                    {
                        AlbedoRgba = PackColor32(ResolveBaseColor(material)),
                        NormalRgba = PackColor32(new Color32(128, 128, 255, 255)),
                        MaskRgba = PackColor32(ResolveMaskColor(material))
                    };
                }

                albedo = CreateAtlasTexture("TX_" + token + "_Interior_Albedo", atlasSize, PackColor32(new Color32(128, 128, 128, 255)), nativeRects, nativeColors, InteriorClutterForgeConstants.AtlasChannelAlbedo, false);
                normal = CreateAtlasTexture("TX_" + token + "_Interior_Normal", atlasSize, PackColor32(new Color32(128, 128, 255, 255)), nativeRects, nativeColors, InteriorClutterForgeConstants.AtlasChannelNormal, true);
                mask = CreateAtlasTexture("TX_" + token + "_Interior_Mask", atlasSize, PackColor32(new Color32(0, 255, 0, 115)), nativeRects, nativeColors, InteriorClutterForgeConstants.AtlasChannelMask, true);
            }
            finally
            {
                if (nativeColors.IsCreated) nativeColors.Dispose();
                if (nativeRects.IsCreated) nativeRects.Dispose();
            }

            bool copiedAny = false;
            bool scaledAny = false;
            bool directCopyFallbackAny = false;
            bool copyFailure = false;
            for (int i = 0; i < materials.Count; i++)
            {
                Material material = materials[i];
                InteriorClutterAtlasRect rect = rects[i];
                Texture albedoSource = ResolveTexture(material, "_BaseMap", "_MainTex");
                if (albedoSource != null)
                {
                    Color32 tint = ResolveBaseColor(material);
                    bool tintMultiply = !HasWhiteBaseTint(tint);
                    bool copied = TryCopyTexture(albedoSource, albedo, rect, false, tint, tintMultiply, ref scaledAny, ref directCopyFallbackAny, ref copyFailure);
                    copiedAny |= copied;
                    if (!copied && tintMultiply)
                        metric.WarningFlags |= InteriorClutterWarningFlags.AtlasTintFallback;
                }

                copiedAny |= TryCopyTexture(ResolveTexture(material, "_BumpMap", "_NormalMap", "_Normal"), normal, rect, true, WhiteTint, false, ref scaledAny, ref directCopyFallbackAny, ref copyFailure);
                copiedAny |= TryCopyTexture(ResolveMaskTexture(material), mask, rect, true, WhiteTint, false, ref scaledAny, ref directCopyFallbackAny, ref copyFailure);
            }

            if (!copiedAny)
                metric.WarningFlags |= InteriorClutterWarningFlags.AtlasFallbackSolidColor;
            else
            {
                CommitGpuAtlasForSerialization(albedo, false);
                CommitGpuAtlasForSerialization(normal, true);
                CommitGpuAtlasForSerialization(mask, true);
                metric.WarningFlags |= InteriorClutterWarningFlags.AtlasGpuSerializationSync;
            }

            if (scaledAny)
                metric.WarningFlags |= InteriorClutterWarningFlags.AtlasScaledTexture;
            if (directCopyFallbackAny)
                metric.WarningFlags |= InteriorClutterWarningFlags.AtlasDirectCopyFallback;
            if (copyFailure)
                metric.WarningFlags |= InteriorClutterWarningFlags.AtlasCopyFailure;

            string textureRoot = InteriorClutterForgeConstants.TextureOutputFolder;
            InteriorClutterForge.EnsureAssetFolder(textureRoot);
            albedo = SaveOrReplaceTexture(albedo, textureRoot + "/" + albedo.name + ".asset", InteriorAtlasTextureRole.Albedo, ref metric);
            normal = SaveOrReplaceTexture(normal, textureRoot + "/" + normal.name + ".asset", InteriorAtlasTextureRole.Normal, ref metric);
            mask = SaveOrReplaceTexture(mask, textureRoot + "/" + mask.name + ".asset", InteriorAtlasTextureRole.Mask, ref metric);

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
                shader = Shader.Find("Standard");
            if (shader == null)
                shader = Shader.Find("Hidden/InternalErrorShader");
            if (shader == null)
                throw new InvalidOperationException("Interior atlas shader resolution failed.");

            Material atlasMaterial = new Material(shader)
            {
                name = "MAT_" + token + "_InteriorAtlas"
            };
            SetTexture(atlasMaterial, "_BaseMap", albedo);
            SetTexture(atlasMaterial, "_MainTex", albedo);
            SetTexture(atlasMaterial, "_MaskMap", mask);
            SetTexture(atlasMaterial, "_MetallicGlossMap", mask);
            SetColor(atlasMaterial, "_BaseColor", Color.white);
            SetFloat(atlasMaterial, "_Metallic", 0f);
            SetFloat(atlasMaterial, "_Smoothness", 0.45f);
            atlasMaterial.EnableKeyword("_METALLICSPECGLOSSMAP");
            atlasMaterial.EnableKeyword("_METALLICGLOSSMAP");
            atlasMaterial.EnableKeyword("_MASKMAP");
            atlasMaterial.enableInstancing = true;
            return new InteriorMaterialAtlas(rects, atlasMaterial);
        }

        private static void PackRects(List<Material> materials, InteriorAtlasProfile profile, List<InteriorClutterAtlasRect> output)
        {
            int atlas = math.max(256, profile.AtlasSize);
            int fallbackTile = math.clamp(InteriorClutterForgeConstants.OverflowFallbackTileSize, 1, atlas);
            var free = new List<FreeRect>(64);
            if (atlas - fallbackTile > 0)
                free.Add(new FreeRect { X = fallbackTile, Y = 0, Width = atlas - fallbackTile, Height = fallbackTile });
            if (atlas - fallbackTile > 0)
                free.Add(new FreeRect { X = 0, Y = fallbackTile, Width = atlas, Height = atlas - fallbackTile });
            for (int i = 0; i < materials.Count; i++)
            {
                int tile = ResolveTileSize(materials[i], profile);
                int chosen = -1;
                int bestWaste = int.MaxValue;
                for (int f = 0; f < free.Count; f++)
                {
                    FreeRect r = free[f];
                    if (r.Width < tile || r.Height < tile)
                        continue;

                    int waste = r.Width * r.Height - tile * tile;
                    if (waste < bestWaste)
                    {
                        bestWaste = waste;
                        chosen = f;
                    }
                }

                if (chosen < 0)
                {
                    output.Add(new InteriorClutterAtlasRect
                    {
                        X = 0,
                        Y = 0,
                        Width = fallbackTile,
                        Height = fallbackTile,
                        UvRect = new float4(0f, 0f, (float)fallbackTile / atlas, (float)fallbackTile / atlas),
                        MaterialHash = InteriorClutterForge.StableHash(materials[i] != null ? materials[i].name : "NULL"),
                        Flags = (uint)InteriorClutterWarningFlags.MaterialOverflow
                    });
                    continue;
                }

                FreeRect slot = free[chosen];
                free.RemoveAt(chosen);
                output.Add(new InteriorClutterAtlasRect
                {
                    X = slot.X,
                    Y = slot.Y,
                    Width = tile,
                    Height = tile,
                    UvRect = new float4((float)slot.X / atlas, (float)slot.Y / atlas, (float)tile / atlas, (float)tile / atlas),
                    MaterialHash = InteriorClutterForge.StableHash(materials[i] != null ? materials[i].name : "NULL"),
                    Flags = 0u
                });

                if (slot.Width - tile > 0)
                    free.Add(new FreeRect { X = slot.X + tile, Y = slot.Y, Width = slot.Width - tile, Height = tile });
                if (slot.Height - tile > 0)
                    free.Add(new FreeRect { X = slot.X, Y = slot.Y + tile, Width = slot.Width, Height = slot.Height - tile });
            }
        }

        private static int ResolveTileSize(Material material, InteriorAtlasProfile profile)
        {
            Texture texture = ResolveTexture(material, "_BaseMap", "_MainTex");
            int size = texture != null ? math.max(texture.width, texture.height) : 64;
            int tile = 32;
            while (tile < size && tile < profile.MaxTileSize)
                tile <<= 1;
            return math.clamp(tile, 16, math.max(16, profile.MaxTileSize));
        }

        private static Texture2D CreateAtlasTexture(string name, int size, uint defaultColor, NativeArray<InteriorClutterAtlasRect> rects, NativeArray<InteriorClutterAtlasColor> colors, int channel, bool linear)
        {
            int pixelCount = checked(size * size);
            NativeArray<uint> pixels = default;
            try
            {
                // COLD ALLOC: NativeArray<uint>[atlas pixels] - editor atlas texel staging, one channel at a time - owner: InteriorMaterialAtlasBuilder
                pixels = new NativeArray<uint>(pixelCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                JobHandle solidFillHandle = new FillAtlasSolidJob
                {
                    Pixels = pixels,
                    PackedColorRgba = defaultColor
                }.Schedule(pixelCount, 1024);
                JobHandle rectFillHandle = new FillAtlasRectColorsJob
                {
                    Pixels = pixels,
                    Rects = rects,
                    Colors = colors,
                    AtlasSize = size,
                    Channel = channel
                }.Schedule(solidFillHandle);
                rectFillHandle.Complete();

                Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, true, linear)
                {
                    name = name,
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear
                };
                texture.SetPixelData(pixels, 0);
                texture.Apply(false, false);
                return texture;
            }
            finally
            {
                if (pixels.IsCreated)
                    pixels.Dispose();
            }
        }

        private static uint PackColor32(Color32 color)
        {
            return (uint)(color.r | (color.g << 8) | (color.b << 16) | (color.a << 24));
        }

        private static bool TryCopyTexture(
            Texture source,
            Texture2D atlas,
            InteriorClutterAtlasRect rect,
            bool linear,
            Color32 tint,
            bool tintMultiply,
            ref bool scaledAny,
            ref bool directCopyFallbackAny,
            ref bool copyFailure)
        {
            if (source == null || atlas == null)
                return false;

            if ((rect.Flags & (uint)InteriorClutterWarningFlags.MaterialOverflow) != 0u)
                return false;

            int width = rect.Width;
            int height = rect.Height;
            if (width <= 0 || height <= 0 || rect.X < 0 || rect.Y < 0)
                return false;

            bool exactSize = source.width == width && source.height == height;
            if (exactSize && !tintMultiply)
            {
                try
                {
                    UnityEngine.Graphics.CopyTexture(source, 0, 0, 0, 0, width, height, atlas, 0, 0, rect.X, rect.Y);
                    return true;
                }
                catch (Exception)
                {
                    directCopyFallbackAny = true;
                }
            }

            if (!exactSize)
                scaledAny = true;

            try
            {
                if (tintMultiply)
                    CopyTextureViaTintedBlit(source, atlas, rect, linear, tint);
                else
                    CopyTextureViaBlit(source, atlas, rect, linear);

                return true;
            }
            catch (Exception)
            {
                copyFailure = true;
                return false;
            }
        }

        private static void CopyTextureViaBlit(Texture source, Texture2D atlas, InteriorClutterAtlasRect rect, bool linear)
        {
            RenderTexture temp = RenderTexture.GetTemporary(rect.Width, rect.Height, 0, RenderTextureFormat.ARGB32, ResolveReadWrite(linear));
            try
            {
                UnityEngine.Graphics.Blit(source, temp);
                UnityEngine.Graphics.CopyTexture(temp, 0, 0, 0, 0, rect.Width, rect.Height, atlas, 0, 0, rect.X, rect.Y);
            }
            finally
            {
                RenderTexture.ReleaseTemporary(temp);
            }
        }

        private static void CopyTextureViaTintedBlit(Texture source, Texture2D atlas, InteriorClutterAtlasRect rect, bool linear, Color32 tint)
        {
            RenderTexture previous = RenderTexture.active;
            RenderTexture temp = RenderTexture.GetTemporary(rect.Width, rect.Height, 0, RenderTextureFormat.ARGB32, ResolveReadWrite(linear));
            Texture2D tile = null;
            try
            {
                UnityEngine.Graphics.Blit(source, temp);
                tile = new Texture2D(rect.Width, rect.Height, TextureFormat.RGBA32, false, linear)
                {
                    name = atlas.name + "_TintTile",
                    hideFlags = HideFlags.HideAndDontSave,
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear
                };
                RenderTexture.active = temp;
                tile.ReadPixels(new Rect(0f, 0f, rect.Width, rect.Height), 0, 0, false);
                NativeArray<uint> pixels = tile.GetRawTextureData<uint>();
                JobHandle tintHandle = new TintAtlasTileJob
                {
                    Pixels = pixels,
                    TintRgba = PackColor32(tint)
                }.Schedule(pixels.Length, 1024);
                tintHandle.Complete();
                tile.Apply(false, false);
                UnityEngine.Graphics.CopyTexture(tile, 0, 0, 0, 0, rect.Width, rect.Height, atlas, 0, 0, rect.X, rect.Y);
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(temp);
                if (tile != null)
                    Object.DestroyImmediate(tile);
            }
        }

        private static void CommitGpuAtlasForSerialization(Texture2D atlas, bool linear)
        {
            if (atlas == null || atlas.width <= 0 || atlas.height <= 0)
                return;

            RenderTexture previous = RenderTexture.active;
            RenderTexture temp = RenderTexture.GetTemporary(atlas.width, atlas.height, 0, RenderTextureFormat.ARGB32, ResolveReadWrite(linear));
            try
            {
                UnityEngine.Graphics.Blit(atlas, temp);
                RenderTexture.active = temp;
                atlas.ReadPixels(new Rect(0f, 0f, atlas.width, atlas.height), 0, 0, false);
                atlas.Apply(true, false);
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(temp);
            }
        }

        private static RenderTextureReadWrite ResolveReadWrite(bool linear)
        {
            return linear ? RenderTextureReadWrite.Linear : RenderTextureReadWrite.sRGB;
        }

        private static Texture ResolveTexture(Material material, string first, string second)
        {
            if (material == null)
                return null;

            if (material.HasProperty(first))
                return material.GetTexture(first);
            if (material.HasProperty(second))
                return material.GetTexture(second);

            return null;
        }

        private static Texture ResolveTexture(Material material, string first, string second, string third)
        {
            if (material == null)
                return null;

            if (material.HasProperty(first))
                return material.GetTexture(first);
            if (material.HasProperty(second))
                return material.GetTexture(second);
            if (material.HasProperty(third))
                return material.GetTexture(third);

            return null;
        }

        private static Texture ResolveMaskTexture(Material material)
        {
            if (material != null && material.HasProperty("_MaskMap"))
                return material.GetTexture("_MaskMap");
            return null;
        }

        private static Color32 ResolveBaseColor(Material material)
        {
            Color color = Color.gray;
            if (material != null)
            {
                if (material.HasProperty("_BaseColor"))
                    color = material.GetColor("_BaseColor");
                else if (material.HasProperty("_Color"))
                    color = material.GetColor("_Color");
            }

            return color;
        }

        private static readonly Color32 WhiteTint = new Color32(255, 255, 255, 255);

        private static bool HasWhiteBaseTint(Color32 color)
        {
            return color.r >= 250 && color.g >= 250 && color.b >= 250 && color.a >= 250;
        }

        private static Color32 ResolveMaskColor(Material material)
        {
            float metallic = material != null && material.HasProperty("_Metallic") ? material.GetFloat("_Metallic") : 0f;
            float smoothness = 0.45f;
            if (material != null)
            {
                if (material.HasProperty("_Smoothness"))
                    smoothness = material.GetFloat("_Smoothness");
                else if (material.HasProperty("_Glossiness"))
                    smoothness = material.GetFloat("_Glossiness");
            }

            float occlusion = material != null && material.HasProperty("_OcclusionStrength") ? material.GetFloat("_OcclusionStrength") : 1f;
            byte metal = (byte)math.round(math.saturate(metallic) * 255f);
            byte ao = (byte)math.round(math.saturate(occlusion) * 255f);
            byte smooth = (byte)math.round(math.saturate(smoothness) * 255f);
            return new Color32(metal, ao, 0, smooth);
        }

        private static Texture2D SaveOrReplaceTexture(Texture2D texture, string path, InteriorAtlasTextureRole role, ref InteriorClutterBakeMetric metric)
        {
            CompressAtlasTexture(texture, role, ref metric);

            Texture2D existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (existing != null)
            {
                EditorUtility.CopySerialized(texture, existing);
                EditorUtility.SetDirty(existing);
                Object.DestroyImmediate(texture);
                return existing;
            }

            AssetDatabase.CreateAsset(texture, path);
            EditorUtility.SetDirty(texture);
            return texture;
        }

        private static void CompressAtlasTexture(Texture2D texture, InteriorAtlasTextureRole role, ref InteriorClutterBakeMetric metric)
        {
            if (texture == null)
                return;

            TextureFormat format = ResolveCompressionFormat(role, ref metric);
            if (texture.format == format)
                return;

            try
            {
                EditorUtility.CompressTexture(texture, format, TextureCompressionQuality.Best);
                texture.Apply(false, false);
                metric.WarningFlags |= InteriorClutterWarningFlags.AtlasCompressedTexture;
            }
            catch (Exception)
            {
                metric.WarningFlags |= InteriorClutterWarningFlags.AtlasCompressionFallback;
            }
        }

        private static TextureFormat ResolveCompressionFormat(InteriorAtlasTextureRole role, ref InteriorClutterBakeMetric metric)
        {
            if (role == InteriorAtlasTextureRole.Normal && SystemInfo.SupportsTextureFormat(TextureFormat.BC5))
                return TextureFormat.BC5;

            if (SystemInfo.SupportsTextureFormat(TextureFormat.BC7))
                return TextureFormat.BC7;

            if (SystemInfo.SupportsTextureFormat(TextureFormat.DXT5))
                return TextureFormat.DXT5;

            metric.WarningFlags |= InteriorClutterWarningFlags.AtlasCompressionFallback;
            return TextureFormat.RGBA32;
        }

        private static void SetTexture(Material material, string property, Texture texture)
        {
            if (material != null && material.HasProperty(property))
                material.SetTexture(property, texture);
        }

        private static void SetColor(Material material, string property, Color color)
        {
            if (material != null && material.HasProperty(property))
                material.SetColor(property, color);
        }

        private static void SetFloat(Material material, string property, float value)
        {
            if (material != null && material.HasProperty(property))
                material.SetFloat(property, value);
        }
    }

    internal struct InteriorClutterExcludeFilter
    {
        private FixedList512Bytes<uint> _tagHashes;
        private FixedList512Bytes<int> _layers;

        internal static InteriorClutterExcludeFilter Default()
        {
            return Parse("Player,Interactable,Door,Fabricator,Terminal", "Player,Interaction,UI");
        }

        internal static InteriorClutterExcludeFilter Parse(string tags, string layers)
        {
            InteriorClutterExcludeFilter filter = default;
            ParseTagCsv(tags, ref filter._tagHashes);
            ParseLayerCsv(layers, ref filter._layers);
            return filter;
        }

        internal bool IsInteractiveOrExcluded(GameObject go, List<Component> componentScratch)
        {
            return go == null || TryFindExclusionRoot(go.transform, null, componentScratch, out _);
        }

        internal bool TryFindExclusionRoot(Transform start, Transform stopInclusive, List<Component> componentScratch, out Transform excluded)
        {
            excluded = null;
            for (Transform current = start; current != null; current = current.parent)
            {
                if (IsDirectlyExcluded(current.gameObject, componentScratch))
                {
                    excluded = current;
                    return true;
                }

                if (current == stopInclusive)
                    break;
            }

            return false;
        }

        private bool IsDirectlyExcluded(GameObject go, List<Component> componentScratch)
        {
            if (go == null)
                return true;

            if (MatchesTag(go) || MatchesLayer(go.layer))
                return true;

            if (componentScratch == null)
                return true;

            componentScratch.Clear();
            go.GetComponents<Component>(componentScratch);
            for (int i = 0; i < componentScratch.Count; i++)
            {
                Component component = componentScratch[i];
                if (component == null)
                    return true;

                Type type = component.GetType();
                string name = type.Name;
                if (ContainsToken(name, "Interact") ||
                    ContainsToken(name, "Door") ||
                    ContainsToken(name, "Fabricator") ||
                    ContainsToken(name, "Terminal") ||
                    ContainsToken(name, "Power") ||
                    ContainsToken(name, "Inventory") ||
                    ContainsToken(name, "Saveable") ||
                    component is Rigidbody ||
                    component is Joint)
                {
                    return true;
                }
            }

            return false;
        }

        private bool MatchesTag(GameObject go)
        {
            uint hash = InteriorClutterForge.StableHash(go.tag);
            for (int i = 0; i < _tagHashes.Length; i++)
            {
                if (_tagHashes[i] == hash)
                    return true;
            }

            return false;
        }

        private bool MatchesLayer(int layer)
        {
            for (int i = 0; i < _layers.Length; i++)
            {
                if (_layers[i] == layer)
                    return true;
            }

            return false;
        }

        private static bool ContainsToken(string value, string token)
        {
            return value != null && value.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static void ParseTagCsv(string value, ref FixedList512Bytes<uint> tags)
        {
            if (string.IsNullOrWhiteSpace(value))
                return;

            FixedString64Bytes token = default;
            int pendingSpaces = 0;
            bool started = false;
            for (int i = 0; i <= value.Length; i++)
            {
                char c = i < value.Length ? value[i] : ',';
                if (c == ',')
                {
                    AddTagToken(ref token, ref tags);
                    token = default;
                    pendingSpaces = 0;
                    started = false;
                    continue;
                }

                if (IsTokenWhitespace(c))
                {
                    if (started)
                        pendingSpaces++;
                    continue;
                }

                while (pendingSpaces > 0)
                {
                    AddAscii(ref token, ' ');
                    pendingSpaces--;
                }

                AddAscii(ref token, c);
                started = true;
            }
        }

        private static void ParseLayerCsv(string value, ref FixedList512Bytes<int> layers)
        {
            if (string.IsNullOrWhiteSpace(value))
                return;

            FixedString64Bytes token = default;
            int pendingSpaces = 0;
            bool started = false;
            for (int i = 0; i <= value.Length; i++)
            {
                char c = i < value.Length ? value[i] : ',';
                if (c == ',')
                {
                    AddLayerToken(ref token, ref layers);
                    token = default;
                    pendingSpaces = 0;
                    started = false;
                    continue;
                }

                if (IsTokenWhitespace(c))
                {
                    if (started)
                        pendingSpaces++;
                    continue;
                }

                while (pendingSpaces > 0)
                {
                    AddAscii(ref token, ' ');
                    pendingSpaces--;
                }

                AddAscii(ref token, c);
                started = true;
            }
        }

        private static void AddTagToken(ref FixedString64Bytes token, ref FixedList512Bytes<uint> tags)
        {
            if (token.Length == 0 || tags.Length >= tags.Capacity)
                return;

            uint hash = StableHash(in token);
            for (int i = 0; i < tags.Length; i++)
            {
                if (tags[i] == hash)
                    return;
            }

            tags.Add(hash);
        }

        private static void AddLayerToken(ref FixedString64Bytes token, ref FixedList512Bytes<int> layers)
        {
            if (token.Length == 0 || layers.Length >= layers.Capacity)
                return;

            int layer = LayerMask.NameToLayer(token.ToString());
            if (layer < 0)
                return;

            for (int i = 0; i < layers.Length; i++)
            {
                if (layers[i] == layer)
                    return;
            }

            layers.Add(layer);
        }

        private static bool IsTokenWhitespace(char c)
        {
            return c == ' ' || c == '\t' || c == '\r' || c == '\n';
        }

        private static void AddAscii(ref FixedString64Bytes token, char c)
        {
            if (token.Length >= FixedString64Bytes.UTF8MaxLengthInBytes || (uint)c > 0x7fu)
                return;

            token.Add((byte)c);
        }

        private static uint StableHash(in FixedString64Bytes value)
        {
            unchecked
            {
                uint hash = 2166136261u;
                for (int i = 0; i < value.Length; i++)
                {
                    hash ^= value[i];
                    hash *= 16777619u;
                }

                return hash;
            }
        }
    }

    internal struct InteriorClutterPrefabFinding
    {
        public string Path;
        public int StaticChildRenderers;
        public int InteractiveChildRenderers;
        public int MaterialCount;
        public int EstimatedDrawCalls;
        public InteriorClutterWarningFlags Flags;
    }

    internal static class Hierarchy_Bloat_Scanner
    {
        internal static List<InteriorClutterPrefabFinding> ScanProject(string requestedRoot, InteriorClutterExcludeFilter filter)
        {
            string root = AssetDatabase.IsValidFolder(requestedRoot)
                ? requestedRoot
                : AssetDatabase.IsValidFolder(InteriorClutterForgeConstants.FallbackConstructionRoot)
                    ? InteriorClutterForgeConstants.FallbackConstructionRoot
                    : "Assets/_Project/Prefabs";
            string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { root });
            var findings = new List<InteriorClutterPrefabFinding>(guids.Length);
            var filters = new List<MeshFilter>(256);
            var materials = new List<Material>(32);
            var sharedMaterials = new List<Material>(8);
            var componentScratch = new List<Component>(32);
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null)
                    continue;

                filters.Clear();
                materials.Clear();
                prefab.GetComponentsInChildren<MeshFilter>(true, filters);
                InteriorClutterPrefabFinding finding = default;
                finding.Path = path;
                for (int f = 0; f < filters.Count; f++)
                {
                    MeshFilter meshFilter = filters[f];
                    if (meshFilter == null || meshFilter.sharedMesh == null)
                        continue;

                    if (!InteriorClutterForge.IsActiveInPrefabHierarchy(meshFilter.transform, prefab.transform) || !meshFilter.TryGetComponent(out MeshRenderer renderer) || !renderer.enabled)
                        continue;

                    if (filter.IsInteractiveOrExcluded(meshFilter.gameObject, componentScratch))
                    {
                        finding.InteractiveChildRenderers++;
                        continue;
                    }

                    finding.StaticChildRenderers++;
                    sharedMaterials.Clear();
                    renderer.GetSharedMaterials(sharedMaterials);
                    for (int m = 0; m < sharedMaterials.Count; m++)
                        AddUnique(materials, sharedMaterials[m]);
                }

                finding.MaterialCount = materials.Count;
                finding.EstimatedDrawCalls = math.max(1, finding.StaticChildRenderers) + finding.InteractiveChildRenderers;
                if (!AssetDatabase.IsValidFolder(InteriorClutterForgeConstants.DefaultHabitatRoot))
                    finding.Flags |= InteriorClutterWarningFlags.MissingHabitatRoot;
                if (finding.StaticChildRenderers > 10 || finding.MaterialCount > 1)
                    findings.Add(finding);
            }

            return findings;
        }

        internal static void WriteReport(List<InteriorClutterPrefabFinding> findings)
        {
            InteriorClutterForge.EnsureFileFolder(InteriorClutterForgeConstants.RenderingOptimizationReportPath);
            var builder = new StringBuilder(4096);
            builder.Append("{\n  \"agent\": \"SHINOBU_211\",\n  \"status\": \"PENDING_VERIFICATION\",\n  \"unoptimizedPrefabsDetected\": ");
            builder.Append(findings != null ? findings.Count : 0);
            builder.Append(",\n  \"items\": [\n");
            if (findings != null)
            {
                for (int i = 0; i < findings.Count; i++)
                {
                    InteriorClutterPrefabFinding f = findings[i];
                    if (i > 0)
                        builder.Append(",\n");
                    builder.Append("    { \"path\": \"").Append(InteriorClutterForge.Escape(f.Path));
                    builder.Append("\", \"staticChildren\": ").Append(f.StaticChildRenderers);
                    builder.Append(", \"interactiveIgnored\": ").Append(f.InteractiveChildRenderers);
                    builder.Append(", \"materialCount\": ").Append(f.MaterialCount);
                    builder.Append(", \"estimatedDrawCallsBefore\": ").Append(f.EstimatedDrawCalls);
                    builder.Append(", \"estimatedStaticDrawCallsAfterForge\": 1");
                    builder.Append(", \"estimatedDrawCallsAfterForge\": ").Append(1 + f.InteractiveChildRenderers);
                    builder.Append(", \"flags\": \"").Append(f.Flags == InteriorClutterWarningFlags.None ? "NONE" : f.Flags.ToString());
                    builder.Append("\" }");
                }
            }

            builder.Append("\n  ]\n}\n");
            File.WriteAllText(InteriorClutterForgeConstants.RenderingOptimizationReportPath, builder.ToString(), new UTF8Encoding(false));
        }

        private static void AddUnique(List<Material> materials, Material material)
        {
            for (int i = 0; i < materials.Count; i++)
            {
                if (materials[i] == material)
                    return;
            }

            materials.Add(material);
        }
    }

    internal static class InteriorClutterPreviewOverlay
    {
        private static readonly List<Bounds> _StaticBounds = new List<Bounds>(256);
        private static readonly List<Bounds> _InteractiveBounds = new List<Bounds>(64);
        private static bool _hooked;

        internal static void EnsureHook()
        {
            if (_hooked)
                return;

            SceneView.duringSceneGui += Draw;
            _hooked = true;
        }

        internal static void BuildPreview(string prefabPath, InteriorClutterExcludeFilter filter)
        {
            EnsureHook();
            _StaticBounds.Clear();
            _InteractiveBounds.Clear();
            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                var filterScratch = new List<MeshFilter>(256);
                var componentScratch = new List<Component>(32);
                root.GetComponentsInChildren<MeshFilter>(true, filterScratch);
                for (int i = 0; i < filterScratch.Count; i++)
                {
                    MeshFilter meshFilter = filterScratch[i];
                    if (meshFilter == null || !InteriorClutterForge.IsActiveInPrefabHierarchy(meshFilter.transform, root.transform) || !meshFilter.TryGetComponent(out MeshRenderer renderer) || !renderer.enabled)
                        continue;

                    if (filter.IsInteractiveOrExcluded(meshFilter.gameObject, componentScratch))
                        _InteractiveBounds.Add(renderer.bounds);
                    else
                        _StaticBounds.Add(renderer.bounds);
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            SceneView.RepaintAll();
        }

        private static void Draw(SceneView sceneView)
        {
            Handles.zTest = CompareFunction.LessEqual;
            Handles.color = Color.green;
            for (int i = 0; i < _StaticBounds.Count; i++)
                Handles.DrawWireCube(_StaticBounds[i].center, _StaticBounds[i].size);

            Handles.color = Color.red;
            for (int i = 0; i < _InteractiveBounds.Count; i++)
                Handles.DrawWireCube(_InteractiveBounds[i].center, _InteractiveBounds[i].size);
        }
    }
}
#endif
