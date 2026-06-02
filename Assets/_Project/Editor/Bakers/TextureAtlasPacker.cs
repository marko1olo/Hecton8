using System;
using System.Collections.Generic;
using System.IO;
using Hecton8.Core;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Hecton8.Editor.Bakers
{
    public static class TextureAtlasPacker
    {
        public const int MinimumAtlasSize = 512;
        public const int DefaultAtlasSize = 4096;
        public const int DefaultPadding = 4;
        public const int MaxTextureSetsPerAtlas = 256;

        private const string MraoAtlasShaderName = "Hecton8/Bakers/MraoAtlasLit";
        private const string NormalMapKeyword = "_NORMALMAP";
        private const int RequiredUv0ByteWidth = 8;
        private const long AtlasScratchBytesPerPixel = 4L;
        private const long MaxAtlasScratchBytes = 96L * 1024L * 1024L;
        private const long MaxAtlasSourcePixels = (long)DefaultAtlasSize * DefaultAtlasSize;
        private const long MaxAtlasEncodedPngBytes = 128L * 1024L * 1024L;
        private const long MaxMeshUvRollbackBytes = 128L * 1024L * 1024L;

        public readonly struct TextureRect
        {
            public readonly int Width;
            public readonly int Height;

            public TextureRect(int width, int height)
            {
                Width = width;
                Height = height;
            }
        }

        public readonly struct PackedRect
        {
            public readonly int SourceIndex;
            public readonly RectInt PaddedRect;
            public readonly RectInt UvRect;

            public PackedRect(int sourceIndex, RectInt paddedRect, RectInt uvRect)
            {
                SourceIndex = sourceIndex;
                PaddedRect = paddedRect;
                UvRect = uvRect;
            }
        }

        public readonly struct TextureSetInput
        {
            public readonly string Name;
            public readonly Texture2D Albedo;
            public readonly Texture2D Normal;
            public readonly Texture2D Mask;
            public readonly Mesh Mesh;

            public TextureSetInput(string name, Texture2D albedo, Texture2D normal, Texture2D mask, Mesh mesh)
            {
                Name = name;
                Albedo = albedo;
                Normal = normal;
                Mask = mask;
                Mesh = mesh;
            }
        }

        public readonly struct AtlasBuildResult
        {
            public readonly string AlbedoAtlasPath;
            public readonly string NormalAtlasPath;
            public readonly string MaskAtlasPath;
            public readonly string MaterialPath;
            public readonly float PackingEfficiency01;

            public AtlasBuildResult(string albedoAtlasPath, string normalAtlasPath, string maskAtlasPath, string materialPath, float packingEfficiency01)
            {
                AlbedoAtlasPath = albedoAtlasPath;
                NormalAtlasPath = normalAtlasPath;
                MaskAtlasPath = maskAtlasPath;
                MaterialPath = materialPath;
                PackingEfficiency01 = packingEfficiency01;
            }
        }

        private readonly struct MeshUvRollbackSnapshot
        {
            public readonly Mesh Mesh;
            public readonly int Stream;
            public readonly byte[] VertexBytes;

            public MeshUvRollbackSnapshot(Mesh mesh, int stream, byte[] vertexBytes)
            {
                Mesh = mesh;
                Stream = stream;
                VertexBytes = vertexBytes;
            }
        }

        private readonly struct MaterialRollbackSnapshot
        {
            public readonly bool Captured;
            public readonly Material Material;
            public readonly Shader Shader;
            public readonly string Name;
            public readonly bool HasBaseMap;
            public readonly Texture BaseMap;
            public readonly bool HasMainTex;
            public readonly Texture MainTex;
            public readonly bool HasNormalMap;
            public readonly Texture NormalMap;
            public readonly bool HasBumpMap;
            public readonly Texture BumpMap;
            public readonly bool HasMraoMap;
            public readonly Texture MraoMap;
            public readonly bool HasMetallicScale;
            public readonly float MetallicScale;
            public readonly bool HasRoughnessScale;
            public readonly float RoughnessScale;
            public readonly bool HasOcclusionStrength;
            public readonly float OcclusionStrength;
            public readonly bool HasEmissionStrength;
            public readonly float EmissionStrength;
            public readonly bool HasNormalScale;
            public readonly float NormalScale;
            public readonly bool NormalMapKeywordEnabled;

            public MaterialRollbackSnapshot(
                Material material,
                Shader shader,
                string name,
                bool hasBaseMap,
                Texture baseMap,
                bool hasMainTex,
                Texture mainTex,
                bool hasNormalMap,
                Texture normalMap,
                bool hasBumpMap,
                Texture bumpMap,
                bool hasMraoMap,
                Texture mraoMap,
                bool hasMetallicScale,
                float metallicScale,
                bool hasRoughnessScale,
                float roughnessScale,
                bool hasOcclusionStrength,
                float occlusionStrength,
                bool hasEmissionStrength,
                float emissionStrength,
                bool hasNormalScale,
                float normalScale,
                bool normalMapKeywordEnabled)
            {
                Captured = true;
                Material = material;
                Shader = shader;
                Name = name;
                HasBaseMap = hasBaseMap;
                BaseMap = baseMap;
                HasMainTex = hasMainTex;
                MainTex = mainTex;
                HasNormalMap = hasNormalMap;
                NormalMap = normalMap;
                HasBumpMap = hasBumpMap;
                BumpMap = bumpMap;
                HasMraoMap = hasMraoMap;
                MraoMap = mraoMap;
                HasMetallicScale = hasMetallicScale;
                MetallicScale = metallicScale;
                HasRoughnessScale = hasRoughnessScale;
                RoughnessScale = roughnessScale;
                HasOcclusionStrength = hasOcclusionStrength;
                OcclusionStrength = occlusionStrength;
                HasEmissionStrength = hasEmissionStrength;
                EmissionStrength = emissionStrength;
                HasNormalScale = hasNormalScale;
                NormalScale = normalScale;
                NormalMapKeywordEnabled = normalMapKeywordEnabled;
            }
        }

        private struct PackCandidate
        {
            public int SourceIndex;
            public int Width;
            public int Height;
            public int Area;
        }

        private enum SelectedTextureRole
        {
            Unknown,
            Albedo,
            Normal,
            Mask
        }

        private struct SelectionTextureSetBuilder
        {
            public string Name;
            public Texture2D Albedo;
            public Texture2D Normal;
            public Texture2D Mask;
            public Mesh Mesh;

            public SelectionTextureSetBuilder(string name)
            {
                Name = name;
                Albedo = null;
                Normal = null;
                Mask = null;
                Mesh = null;
            }

            public bool IsComplete => Albedo != null && Normal != null && Mask != null;

            public bool TryAssign(SelectedTextureRole role, Texture2D texture, out string failure)
            {
                failure = string.Empty;
                switch (role)
                {
                    case SelectedTextureRole.Albedo:
                        if (Albedo != null)
                        {
                            failure = "duplicate Albedo texture for set " + Name;
                            return false;
                        }

                        Albedo = texture;
                        return true;
                    case SelectedTextureRole.Normal:
                        if (Normal != null)
                        {
                            failure = "duplicate Normal texture for set " + Name;
                            return false;
                        }

                        Normal = texture;
                        return true;
                    case SelectedTextureRole.Mask:
                        if (Mask != null)
                        {
                            failure = "duplicate M.R.A.O. texture for set " + Name;
                            return false;
                        }

                        Mask = texture;
                        return true;
                    default:
                        failure = "unknown texture role for set " + Name;
                        return false;
                }
            }

            public bool TryAssignMesh(Mesh mesh, out string failure)
            {
                failure = string.Empty;
                if (Mesh != null)
                {
                    failure = "duplicate Mesh asset for set " + Name;
                    return false;
                }

                Mesh = mesh;
                return true;
            }

            public TextureSetInput ToInput()
            {
                return new TextureSetInput(Name, Albedo, Normal, Mask, Mesh);
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        public struct RemapMeshUVsJob : IJobParallelFor
        {
            public NativeArray<float2> Uvs;
            public float2 Scale;
            public float2 Offset;

            public void Execute(int index)
            {
                Uvs[index] = Uvs[index] * Scale + Offset;
            }
        }

        [MenuItem("HECTON-8/Bakers/1605/Pack Selected Readable Textures", false, 206)]
        public static void PackSelectedReadableTextures()
        {
            UnityEngine.Object[] selected = Selection.objects;
            if (selected == null || selected.Length == 0)
            {
                Debug.LogWarning("[TextureBaker1605] Select matching Texture2D assets named *_Albedo, *_Normal, *_MRAO, and optional Mesh/*_Mesh assets.");
                return;
            }

            if (!TryBuildTextureSetsFromSelection(selected, out TextureSetInput[] inputs, out string selectionFailure))
            {
                Debug.LogWarning("[TextureBaker1605] Selection is not a complete texture set group: " + selectionFailure);
                return;
            }

            int safeAtlasSize = ResolveSafeAtlasSize(DefaultAtlasSize);
            if (!TryPackTextureSets(inputs, ProceduralTextureBaker.DefaultOutputRoot + "/Atlases", "TXA_selected_1605", safeAtlasSize, DefaultPadding, out AtlasBuildResult result, out string failure))
                Debug.LogError("[TextureBaker1605] Atlas pack failed: " + failure);
            else
                Debug.Log("[TextureBaker1605] Atlas packed. Efficiency=" + (result.PackingEfficiency01 * 100f).ToString("F2") + "%");
        }

        public static int ResolveSafeAtlasSize(int requestedAtlasSize)
        {
            return ResolveSafeAtlasSize(requestedAtlasSize, 1f);
        }

        public static int ResolveSafeAtlasSize(int requestedAtlasSize, float globalQualityWeight)
        {
            int safeSize = ProceduralTextureBaker.ResolveSafeTextureSize(requestedAtlasSize, globalQualityWeight);
            if (safeSize < MinimumAtlasSize)
                return MinimumAtlasSize;
            if (safeSize > DefaultAtlasSize)
                return DefaultAtlasSize;
            return safeSize;
        }

        internal static bool TryBuildTextureSetsFromSelection(UnityEngine.Object[] selected, out TextureSetInput[] inputs, out string failure)
        {
            inputs = new TextureSetInput[0];
            failure = string.Empty;

            if (selected == null || selected.Length == 0)
            {
                failure = "selection is empty";
                return false;
            }

            Dictionary<string, SelectionTextureSetBuilder> sets = new Dictionary<string, SelectionTextureSetBuilder>(selected.Length, StringComparer.OrdinalIgnoreCase);
            List<string> orderedKeys = new List<string>(selected.Length);

            for (int i = 0; i < selected.Length; i++)
            {
                Texture2D texture = selected[i] as Texture2D;
                if (texture != null)
                {
                    if (!TryResolveAssetObjectName(texture, out string textureName, out failure))
                        return false;

                    if (!TryParseTextureRoleSuffix(textureName, out string textureSetKey, out SelectedTextureRole role))
                    {
                        failure = "texture name must end with _Albedo, _Normal, _MRAO, or _Mask: " + textureName;
                        return false;
                    }

                    if (!TryGetOrCreateSelectionSet(sets, orderedKeys, textureSetKey, textureName, out SelectionTextureSetBuilder textureBuilder, out failure))
                        return false;

                    if (!textureBuilder.TryAssign(role, texture, out failure))
                        return false;

                    sets[textureSetKey] = textureBuilder;
                    continue;
                }

                Mesh mesh = selected[i] as Mesh;
                if (mesh != null)
                {
                    if (!TryResolveAssetObjectName(mesh, out string meshName, out failure))
                        return false;

                    if (!TryParseMeshSetKey(meshName, out string meshSetKey))
                    {
                        failure = "mesh asset name is empty";
                        return false;
                    }

                    if (!TryGetOrCreateSelectionSet(sets, orderedKeys, meshSetKey, meshName, out SelectionTextureSetBuilder meshBuilder, out failure))
                        return false;

                    if (!meshBuilder.TryAssignMesh(mesh, out failure))
                        return false;

                    sets[meshSetKey] = meshBuilder;
                    continue;
                }

                failure = "selection item " + i + " is not a Texture2D or Mesh";
                return false;
            }

            inputs = new TextureSetInput[orderedKeys.Count];
            for (int i = 0; i < orderedKeys.Count; i++)
            {
                SelectionTextureSetBuilder builder = sets[orderedKeys[i]];
                if (!builder.IsComplete)
                {
                    failure = "texture set " + builder.Name + " is missing " + BuildMissingRoleList(in builder);
                    inputs = new TextureSetInput[0];
                    return false;
                }

                inputs[i] = builder.ToInput();
            }

            return true;
        }

        private static bool TryGetOrCreateSelectionSet(
            Dictionary<string, SelectionTextureSetBuilder> sets,
            List<string> orderedKeys,
            string setKey,
            string sourceName,
            out SelectionTextureSetBuilder builder,
            out string failure)
        {
            failure = string.Empty;
            string safeSetName = ProceduralTextureBaker.SanitizeAssetNameForPath(setKey);
            if (string.IsNullOrEmpty(safeSetName))
            {
                failure = "texture set has no valid asset name: " + sourceName;
                builder = default;
                return false;
            }

            if (sets.TryGetValue(setKey, out builder))
                return true;

            builder = new SelectionTextureSetBuilder(safeSetName);
            sets.Add(setKey, builder);
            orderedKeys.Add(setKey);
            return true;
        }

        private static bool TryResolveAssetObjectName(UnityEngine.Object assetObject, out string assetName, out string failure)
        {
            assetName = string.Empty;
            failure = string.Empty;
            if (assetObject == null)
            {
                failure = "selected asset is null";
                return false;
            }

            try
            {
                string assetPath = AssetDatabase.GetAssetPath(assetObject);
                assetName = !string.IsNullOrEmpty(assetPath)
                    ? Path.GetFileNameWithoutExtension(assetPath)
                    : assetObject.name;

                if (!string.IsNullOrEmpty(assetName))
                    return true;

                failure = "selected asset has no name";
                return false;
            }
            catch (Exception ex) when (ex is UnityException || ex is InvalidOperationException || ex is ArgumentException || ex is IOException || ex is UnauthorizedAccessException || ex is NotSupportedException)
            {
                failure = "selection asset name lookup failed: " + ex.GetType().Name + ": " + ex.Message;
                return false;
            }
        }

        private static bool TryParseTextureRoleSuffix(string textureName, out string setKey, out SelectedTextureRole role)
        {
            setKey = string.Empty;
            role = SelectedTextureRole.Unknown;

            if (string.IsNullOrEmpty(textureName))
                return false;

            if (TryStripRoleSuffix(textureName, "_Albedo", out setKey))
            {
                role = SelectedTextureRole.Albedo;
                return true;
            }

            if (TryStripRoleSuffix(textureName, "_Normal", out setKey))
            {
                role = SelectedTextureRole.Normal;
                return true;
            }

            if (TryStripRoleSuffix(textureName, "_MRAO", out setKey) || TryStripRoleSuffix(textureName, "_Mask", out setKey))
            {
                role = SelectedTextureRole.Mask;
                return true;
            }

            return false;
        }

        private static bool TryParseMeshSetKey(string meshName, out string setKey)
        {
            setKey = string.Empty;
            if (string.IsNullOrEmpty(meshName))
                return false;

            if (TryStripRoleSuffix(meshName, "_Mesh", out setKey))
                return true;

            setKey = meshName;
            return true;
        }

        private static bool TryStripRoleSuffix(string textureName, string suffix, out string setKey)
        {
            setKey = string.Empty;
            if (!textureName.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                return false;

            setKey = textureName.Substring(0, textureName.Length - suffix.Length);
            return !string.IsNullOrEmpty(setKey);
        }

        private static string BuildMissingRoleList(in SelectionTextureSetBuilder builder)
        {
            string missing = string.Empty;
            AppendMissingRole(builder.Albedo == null, "Albedo", ref missing);
            AppendMissingRole(builder.Normal == null, "Normal", ref missing);
            AppendMissingRole(builder.Mask == null, "M.R.A.O.", ref missing);
            return missing;
        }

        private static void AppendMissingRole(bool isMissing, string roleName, ref string missing)
        {
            if (!isMissing)
                return;

            if (missing.Length > 0)
                missing += "/";

            missing += roleName;
        }

        public static bool TryPackTextureSets(
            TextureSetInput[] inputs,
            string outputFolder,
            string atlasName,
            int atlasSize,
            int padding,
            out AtlasBuildResult result,
            out string failure)
        {
            return TryPackTextureSets(inputs, outputFolder, atlasName, atlasSize, padding, 1f, out result, out failure);
        }

        public static bool TryPackTextureSets(
            TextureSetInput[] inputs,
            string outputFolder,
            string atlasName,
            int atlasSize,
            int padding,
            float globalQualityWeight,
            out AtlasBuildResult result,
            out string failure)
        {
            result = default;
            failure = string.Empty;

            if (inputs == null || inputs.Length == 0)
            {
                failure = "no input texture sets";
                return false;
            }

            if (inputs.Length > MaxTextureSetsPerAtlas)
            {
                failure = "too many texture sets for one atlas: " + inputs.Length + " > " + MaxTextureSetsPerAtlas;
                return false;
            }

            if (atlasSize <= 0)
            {
                failure = "atlas size must be positive";
                return false;
            }

            int safeAtlasSize = ResolveSafeAtlasSize(atlasSize, globalQualityWeight);
            if (!IsSupportedAtlasSize(safeAtlasSize))
            {
                failure = "resolved atlas size is unsupported: requested=" + atlasSize + " safe=" + safeAtlasSize;
                return false;
            }

            atlasSize = safeAtlasSize;

            TextureRect[] rects = new TextureRect[inputs.Length];
            for (int i = 0; i < inputs.Length; i++)
            {
                if (inputs[i].Albedo == null || inputs[i].Normal == null || inputs[i].Mask == null)
                {
                    failure = "input set " + i + " has null textures";
                    return false;
                }

                int width = inputs[i].Albedo.width;
                int height = inputs[i].Albedo.height;
                if (inputs[i].Normal.width != width || inputs[i].Normal.height != height || inputs[i].Mask.width != width || inputs[i].Mask.height != height)
                {
                    failure = "input set " + i + " texture dimensions mismatch";
                    return false;
                }

                rects[i] = new TextureRect(width, height);
            }

            PackedRect[] packed = new PackedRect[inputs.Length];
            if (!TryPackRectangles(rects, atlasSize, padding, packed, out float efficiency))
            {
                failure = "MaxRects could not fit " + inputs.Length + " sets into " + atlasSize + " atlas";
                return false;
            }

            if (!TryValidateAllMeshUvsBeforeAssetWrites(inputs, out failure))
                return false;

            if (!ProceduralTextureBaker.TryEnsureAssetFolder(outputFolder, out string normalizedOutputFolder, out failure))
                return false;

            string safeAtlasName = ProceduralTextureBaker.SanitizeAssetNameForPath(atlasName);
            if (string.IsNullOrEmpty(safeAtlasName))
            {
                failure = "atlas name has no valid asset filename characters";
                return false;
            }

            string albedoPath = normalizedOutputFolder + "/" + safeAtlasName + "_Albedo.png";
            string normalPath = normalizedOutputFolder + "/" + safeAtlasName + "_Normal.png";
            string maskPath = normalizedOutputFolder + "/" + safeAtlasName + "_MRAO.png";
            string materialPath = normalizedOutputFolder + "/MAT_" + safeAtlasName + ".mat";
            bool albedoExisted = AssetPathExists(albedoPath);
            bool normalExisted = AssetPathExists(normalPath);
            bool maskExisted = AssetPathExists(maskPath);
            bool materialExisted = AssetPathExists(materialPath);
            if (!TryCaptureMaterialRollbackSnapshot(materialPath, out MaterialRollbackSnapshot materialRollback, out failure))
                return false;
            if (!ProceduralTextureBaker.TryCaptureAssetFileRollbackSnapshots(albedoPath, normalPath, maskPath, materialPath, out ProceduralTextureBaker.AssetFileRollbackSnapshot[] assetRollback, out failure))
                return false;

            if (!TryCreateAtlasScratch(atlasSize, out Color32[] atlasPixels, out failure))
                return false;

            if (!TryBuildAtlas(inputs, packed, atlasSize, ProceduralTextureBaker.TextureRole.Albedo, albedoPath, atlasPixels, out failure))
            {
                ProceduralTextureBaker.TryRestoreAssetFileRollbackSnapshots(assetRollback);
                TryDeleteCreatedAtlasOutputs(albedoPath, normalPath, maskPath, materialPath, albedoExisted, normalExisted, maskExisted, materialExisted);
                return false;
            }
            if (!TryBuildAtlas(inputs, packed, atlasSize, ProceduralTextureBaker.TextureRole.Normal, normalPath, atlasPixels, out failure))
            {
                ProceduralTextureBaker.TryRestoreAssetFileRollbackSnapshots(assetRollback);
                TryDeleteCreatedAtlasOutputs(albedoPath, normalPath, maskPath, materialPath, albedoExisted, normalExisted, maskExisted, materialExisted);
                return false;
            }
            if (!TryBuildAtlas(inputs, packed, atlasSize, ProceduralTextureBaker.TextureRole.Mask, maskPath, atlasPixels, out failure))
            {
                ProceduralTextureBaker.TryRestoreAssetFileRollbackSnapshots(assetRollback);
                TryDeleteCreatedAtlasOutputs(albedoPath, normalPath, maskPath, materialPath, albedoExisted, normalExisted, maskExisted, materialExisted);
                return false;
            }

            if (!TryCreateOrUpdateMaterial(materialPath, albedoPath, normalPath, maskPath, in materialRollback, out failure))
            {
                ProceduralTextureBaker.TryRestoreAssetFileRollbackSnapshots(assetRollback);
                TryDeleteCreatedAtlasOutputs(albedoPath, normalPath, maskPath, materialPath, albedoExisted, normalExisted, maskExisted, materialExisted);
                return false;
            }

            if (!TryRemapMeshesAndFinalizeWithRollback(inputs, packed, atlasSize, out failure))
            {
                TryRestoreMaterialRollbackSnapshot(materialRollback);
                ProceduralTextureBaker.TryRestoreAssetFileRollbackSnapshots(assetRollback);
                TryDeleteCreatedAtlasOutputs(albedoPath, normalPath, maskPath, materialPath, albedoExisted, normalExisted, maskExisted, materialExisted);
                return false;
            }

            result = new AtlasBuildResult(albedoPath, normalPath, maskPath, materialPath, efficiency);
            return true;
        }

        public static bool TryPackRectangles(TextureRect[] inputs, int atlasSize, int padding, PackedRect[] output, out float efficiency01)
        {
            efficiency01 = 0f;
            if (inputs == null || output == null || inputs.Length == 0 || output.Length < inputs.Length || !IsSupportedAtlasSize(atlasSize) || padding < 0)
                return false;

            if (inputs.Length > MaxTextureSetsPerAtlas)
                return false;

            PackCandidate[] candidates = new PackCandidate[inputs.Length];
            long sourceArea = 0L;
            long totalPaddingLong = (long)padding * 2L;
            if (totalPaddingLong >= atlasSize)
                return false;

            int totalPadding = (int)totalPaddingLong;
            int maxSourceDimension = atlasSize - totalPadding;
            for (int i = 0; i < inputs.Length; i++)
            {
                int width = inputs[i].Width;
                int height = inputs[i].Height;
                if (width <= 0 || height <= 0)
                    return false;

                if (width > maxSourceDimension || height > maxSourceDimension)
                    return false;

                int paddedWidth = width + totalPadding;
                int paddedHeight = height + totalPadding;
                candidates[i] = new PackCandidate
                {
                    SourceIndex = i,
                    Width = paddedWidth,
                    Height = paddedHeight,
                    Area = paddedWidth * paddedHeight
                };
                sourceArea += (long)width * height;
            }

            SortCandidatesByAreaDescending(candidates);

            List<RectInt> freeRects = new List<RectInt>(inputs.Length * 4 + 1)
            {
                new RectInt(0, 0, atlasSize, atlasSize)
            };

            for (int candidateIndex = 0; candidateIndex < candidates.Length; candidateIndex++)
            {
                PackCandidate candidate = candidates[candidateIndex];
                if (!TryPlaceCandidate(candidate, freeRects, out RectInt placed))
                    return false;

                RectInt uvRect = new RectInt(
                    placed.x + padding,
                    placed.y + padding,
                    candidate.Width - padding * 2,
                    candidate.Height - padding * 2);
                output[candidate.SourceIndex] = new PackedRect(candidate.SourceIndex, placed, uvRect);
                SplitFreeRectangles(freeRects, placed);
                PruneContainedRectangles(freeRects);
            }

            efficiency01 = (float)((double)sourceArea / ((double)atlasSize * atlasSize));
            return true;
        }

        public static bool TryRemapMeshUvs(Mesh mesh, RectInt uvRect, int atlasSize, out string failure)
        {
            failure = string.Empty;
            if (atlasSize <= 0 || uvRect.width <= 0 || uvRect.height <= 0)
            {
                failure = "invalid atlas rect";
                return false;
            }

            if (!TryResolveMeshUv0RemapLayout(mesh, out int vertexCount, out int stream, out int stride, out int offset, out failure))
                return false;

            NativeArray<byte> vertexBytes = default;
            NativeArray<float2> uvData = default;
            try
            {
                if (!TryComputeVertexBufferByteLength(vertexCount, stride, out int vertexByteLength, out failure))
                    return false;

                vertexBytes = new NativeArray<byte>(vertexByteLength, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                uvData = new NativeArray<float2>(vertexCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);

                CopyVertexBufferFromMesh(mesh, stream, vertexBytes);
                CopyUvFromVertexBuffer(vertexBytes, uvData, vertexCount, stride, offset);

                float invAtlas = 1f / atlasSize;
                RemapMeshUVsJob job = new RemapMeshUVsJob
                {
                    Uvs = uvData,
                    Scale = new float2(uvRect.width * invAtlas, uvRect.height * invAtlas),
                    Offset = new float2(uvRect.x * invAtlas, uvRect.y * invAtlas)
                };
                job.Run(vertexCount);

                CopyUvToVertexBuffer(uvData, vertexBytes, vertexCount, stride, offset);
                mesh.SetVertexBufferData(vertexBytes, 0, 0, vertexBytes.Length, stream, MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices | MeshUpdateFlags.DontNotifyMeshUsers);
                EditorUtility.SetDirty(mesh);
                // Persist once in TryFinalizeAtlasTransaction after every mesh remap succeeds.
                return true;
            }
            catch (Exception ex) when (IsRecoverableEditorException(ex))
            {
                failure = ex.GetType().Name + ": " + ex.Message;
                return false;
            }
            finally
            {
                if (uvData.IsCreated)
                    uvData.Dispose();
                if (vertexBytes.IsCreated)
                    vertexBytes.Dispose();
            }
        }

        private static bool TryRemapMeshesAndFinalizeWithRollback(TextureSetInput[] inputs, PackedRect[] packed, int atlasSize, out string failure)
        {
            failure = string.Empty;
            List<MeshUvRollbackSnapshot> rollbackSnapshots = new List<MeshUvRollbackSnapshot>(inputs.Length);
            for (int i = 0; i < inputs.Length; i++)
            {
                Mesh mesh = inputs[i].Mesh;
                if (mesh == null)
                    continue;

                if (!TryCaptureMeshUvRollbackSnapshot(mesh, out MeshUvRollbackSnapshot snapshot, out failure))
                {
                    TryRestoreMeshUvRollbackSnapshots(rollbackSnapshots);
                    return false;
                }

                rollbackSnapshots.Add(snapshot);
                if (!TryFindPackedRectForSource(packed, i, out PackedRect sourceRect, out failure))
                {
                    TryRestoreMeshUvRollbackSnapshots(rollbackSnapshots);
                    return false;
                }

                if (!TryRemapMeshUvs(mesh, sourceRect.UvRect, atlasSize, out failure))
                {
                    TryRestoreMeshUvRollbackSnapshots(rollbackSnapshots);
                    return false;
                }
            }

            if (!TryFinalizeAtlasTransaction(out failure))
            {
                TryRestoreMeshUvRollbackSnapshots(rollbackSnapshots);
                return false;
            }

            return true;
        }

        private static bool TryFinalizeAtlasTransaction(out string failure)
        {
            return ProceduralTextureBaker.TryFinalizeAssetDatabase("atlas transaction", out failure);
        }

        private static bool TryCaptureMeshUvRollbackSnapshot(Mesh mesh, out MeshUvRollbackSnapshot snapshot, out string failure)
        {
            snapshot = default;
            if (!TryResolveMeshUv0RemapLayout(mesh, out int vertexCount, out int stream, out int stride, out int ignoredOffset, out failure))
                return false;

            NativeArray<byte> vertexBytes = default;
            try
            {
                if (!TryComputeVertexBufferByteLength(vertexCount, stride, out int vertexByteLength, out failure))
                    return false;

                vertexBytes = new NativeArray<byte>(vertexByteLength, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                CopyVertexBufferFromMesh(mesh, stream, vertexBytes);
                byte[] rollbackBytes = new byte[vertexByteLength];
                vertexBytes.CopyTo(rollbackBytes);
                snapshot = new MeshUvRollbackSnapshot(mesh, stream, rollbackBytes);
                return true;
            }
            catch (Exception ex) when (IsRecoverableEditorException(ex))
            {
                failure = "mesh UV rollback capture failed: " + ex.GetType().Name + ": " + ex.Message;
                return false;
            }
            finally
            {
                if (vertexBytes.IsCreated)
                    vertexBytes.Dispose();
            }
        }

        private static void TryRestoreMeshUvRollbackSnapshots(List<MeshUvRollbackSnapshot> snapshots)
        {
            for (int i = snapshots.Count - 1; i >= 0; i--)
            {
                MeshUvRollbackSnapshot snapshot = snapshots[i];
                if (snapshot.Mesh == null || snapshot.VertexBytes == null || snapshot.VertexBytes.Length == 0)
                    continue;

                NativeArray<byte> vertexBytes = default;
                try
                {
                    vertexBytes = new NativeArray<byte>(snapshot.VertexBytes.Length, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                    for (int j = 0; j < snapshot.VertexBytes.Length; j++)
                        vertexBytes[j] = snapshot.VertexBytes[j];

                    snapshot.Mesh.SetVertexBufferData(vertexBytes, 0, 0, vertexBytes.Length, snapshot.Stream, MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices | MeshUpdateFlags.DontNotifyMeshUsers);
                    EditorUtility.SetDirty(snapshot.Mesh);
                }
                catch (Exception ex) when (ex is UnityException || ex is InvalidOperationException || ex is ArgumentException || ex is IOException || ex is UnauthorizedAccessException || ex is NotSupportedException)
                {
                    Debug.LogWarning("[TextureBaker1605] Failed to restore mesh UV rollback snapshot for " + GetObjectNameNoThrow(snapshot.Mesh) + ": " + ex.Message);
                }
                finally
                {
                    if (vertexBytes.IsCreated)
                        vertexBytes.Dispose();
                }
            }

            try
            {
                AssetDatabase.SaveAssets();
            }
            catch (Exception ex) when (ex is UnityException || ex is InvalidOperationException || ex is ArgumentException || ex is IOException || ex is UnauthorizedAccessException || ex is NotSupportedException)
            {
                Debug.LogWarning("[TextureBaker1605] Failed to save mesh UV rollback snapshots: " + ex.Message);
            }
        }

        private static string GetObjectNameNoThrow(UnityEngine.Object target)
        {
            try
            {
                return target != null ? target.name : "<null>";
            }
            catch (Exception ex) when (ex is UnityException || ex is InvalidOperationException || ex is ArgumentException || ex is NotSupportedException)
            {
                return "<unavailable:" + ex.GetType().Name + ">";
            }
        }

        private static bool TryValidateAllMeshUvsBeforeAssetWrites(TextureSetInput[] inputs, out string failure)
        {
            failure = string.Empty;
            for (int i = 0; i < inputs.Length; i++)
            {
                Mesh mesh = inputs[i].Mesh;
                if (mesh == null)
                    continue;

                for (int previous = 0; previous < i; previous++)
                {
                    if (ReferenceEquals(inputs[previous].Mesh, mesh))
                    {
                        failure = "mesh assigned to multiple atlas sets";
                        return false;
                    }
                }

                if (!TryValidateMeshUv0ReadableForRemap(mesh, out string meshFailure))
                {
                    failure = "input set " + i + " mesh preflight failed: " + meshFailure;
                    return false;
                }
            }

            return true;
        }

        private static bool TryValidateMeshUv0ReadableForRemap(Mesh mesh, out string failure)
        {
            if (!TryResolveMeshUv0RemapLayout(mesh, out int vertexCount, out int stream, out int stride, out int ignoredOffset, out failure))
                return false;

            NativeArray<byte> vertexBytes = default;
            try
            {
                if (!TryComputeVertexBufferByteLength(vertexCount, stride, out int vertexByteLength, out failure))
                    return false;

                vertexBytes = new NativeArray<byte>(vertexByteLength, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                CopyVertexBufferFromMesh(mesh, stream, vertexBytes);
                return true;
            }
            catch (Exception ex) when (IsRecoverableEditorException(ex))
            {
                failure = ex.GetType().Name + ": " + ex.Message;
                return false;
            }
            finally
            {
                if (vertexBytes.IsCreated)
                    vertexBytes.Dispose();
            }
        }

        private static bool TryComputeVertexBufferByteLength(int vertexCount, int stride, out int byteLength, out string failure)
        {
            byteLength = 0;
            failure = string.Empty;
            long byteLengthLong = (long)vertexCount * stride;
            if (byteLengthLong <= 0L || byteLengthLong > int.MaxValue)
            {
                failure = "mesh vertex buffer byte length is invalid";
                return false;
            }

            if (byteLengthLong > MaxMeshUvRollbackBytes)
            {
                failure = "mesh vertex buffer byte length exceeds rollback ceiling";
                return false;
            }

            byteLength = (int)byteLengthLong;
            return true;
        }

        private static bool IsRecoverableEditorException(Exception ex)
        {
            return ex is UnityException
                || ex is InvalidOperationException
                || ex is ArgumentException
                || ex is IOException
                || ex is UnauthorizedAccessException
                || ex is NotSupportedException;
        }

        private static bool TryCreateAtlasScratch(int atlasSize, out Color32[] atlasPixels, out string failure)
        {
            atlasPixels = null;
            failure = string.Empty;
            long pixelCountLong = (long)atlasSize * atlasSize;
            if (pixelCountLong <= 0L || pixelCountLong > int.MaxValue)
            {
                failure = "atlas scratch pixel count is invalid";
                return false;
            }

            long scratchBytesLong = pixelCountLong * AtlasScratchBytesPerPixel;
            if (scratchBytesLong > MaxAtlasScratchBytes)
            {
                failure = "atlas scratch byte ceiling exceeded";
                return false;
            }

            atlasPixels = new Color32[(int)pixelCountLong];
            return true;
        }

        private static bool TryBuildAtlas(TextureSetInput[] inputs, PackedRect[] packed, int atlasSize, ProceduralTextureBaker.TextureRole role, string assetPath, Color32[] atlasPixels, out string failure)
        {
            failure = string.Empty;
            long requiredPixelCountLong = (long)atlasSize * atlasSize;
            if (atlasPixels == null || requiredPixelCountLong <= 0L || requiredPixelCountLong > atlasPixels.Length)
            {
                failure = "atlas scratch buffer is too small";
                return false;
            }

            FillAtlasBackground(atlasPixels, role);

            for (int i = 0; i < inputs.Length; i++)
            {
                Texture2D source = role == ProceduralTextureBaker.TextureRole.Albedo
                    ? inputs[i].Albedo
                    : role == ProceduralTextureBaker.TextureRole.Normal
                        ? inputs[i].Normal
                        : inputs[i].Mask;
                if (!TryFindPackedRectForSource(packed, i, out PackedRect packedRect, out failure))
                    return false;

                if (!TryReadTexturePixels(source, out Color32[] sourcePixels, out failure))
                    return false;
                if (!TryValidateSourcePixelBuffer(source, sourcePixels, out failure))
                    return false;

                BlitWithEdgePadding(atlasPixels, atlasSize, sourcePixels, source.width, source.height, packedRect.UvRect, packedRect.PaddedRect);
            }

            Texture2D atlas = null;
            try
            {
                atlas = new Texture2D(atlasSize, atlasSize, TextureFormat.RGBA32, true, role != ProceduralTextureBaker.TextureRole.Albedo)
                {
                    name = Path.GetFileNameWithoutExtension(assetPath)
                };
                atlas.SetPixels32(atlasPixels);
                atlas.Apply(true, false);

                if (role == ProceduralTextureBaker.TextureRole.Mask && !ProceduralTextureBaker.VerifyMraoChannels(atlas, out failure))
                    return false;

                byte[] png = ImageConversion.EncodeToPNG(atlas);
                if (png == null || png.Length == 0)
                {
                    failure = "PNG encode returned no bytes for atlas";
                    return false;
                }

                if (png.LongLength > MaxAtlasEncodedPngBytes)
                {
                    failure = "atlas PNG byte ceiling exceeded for " + assetPath;
                    return false;
                }

                if (!ProceduralTextureBaker.TryWriteBytesAtomic(assetPath, png, out failure))
                    return false;

                return ProceduralTextureBaker.TryEnforceTextureImportSettings(assetPath, role, atlasSize, out failure);
            }
            catch (Exception ex) when (ex is UnityException || ex is InvalidOperationException || ex is ArgumentException || ex is IOException || ex is UnauthorizedAccessException || ex is NotSupportedException)
            {
                failure = "atlas build failed for " + assetPath + ": " + ex.GetType().Name + ": " + ex.Message;
                return false;
            }
            finally
            {
                if (atlas != null)
                    UnityEngine.Object.DestroyImmediate(atlas);
            }
        }

        private static bool TryReadTexturePixels(Texture2D source, out Color32[] pixels, out string failure)
        {
            pixels = null;
            failure = string.Empty;
            if (source == null)
            {
                failure = "texture is null";
                return false;
            }

            if (!TryValidateSourceTextureReadDimensions(source, out int expectedPixelCount, out failure))
                return false;

            string directReadFailure = string.Empty;
            try
            {
                pixels = source.GetPixels32();
                if (pixels != null && pixels.Length == expectedPixelCount)
                    return true;

                directReadFailure = pixels == null || pixels.Length == 0
                    ? "direct texture read returned no pixels"
                    : "direct texture read pixel count mismatch";
            }
            catch (Exception ex) when (ex is UnityException || ex is ArgumentException || ex is InvalidOperationException || ex is NotSupportedException)
            {
                directReadFailure = ex.GetType().Name + ": " + ex.Message;
            }

            if (!TryResolveTextureImporterForReadableBridge(source, directReadFailure, out string assetPath, out TextureImporter importer, out failure))
                return false;

            bool previousReadable = importer.isReadable;
            bool restoreReadableState = !previousReadable;
            bool readSucceeded = false;
            string readFailure = string.Empty;
            try
            {
                if (restoreReadableState)
                {
                    importer.isReadable = true;
                    importer.SaveAndReimport();
                }

                source = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
                if (source == null)
                {
                    readFailure = "texture readable bridge produced null asset: " + assetPath;
                }
                else if (!TryValidateSourceTextureReadDimensions(source, out expectedPixelCount, out string dimensionFailure))
                {
                    readFailure = dimensionFailure;
                }
                else
                {
                    pixels = source.GetPixels32();
                    if (pixels == null || pixels.Length != expectedPixelCount)
                        readFailure = "texture pixel count mismatch after readable import: " + assetPath;
                    else
                        readSucceeded = true;
                }
            }
            catch (Exception ex) when (ex is UnityException || ex is IOException || ex is UnauthorizedAccessException || ex is InvalidOperationException || ex is ArgumentException || ex is NotSupportedException)
            {
                readFailure = "texture read failed after readable import: " + assetPath + " / " + ex.Message;
            }

            if (restoreReadableState && !TryRestoreTextureReadableState(importer, false, assetPath, out string restoreFailure))
            {
                failure = readSucceeded
                    ? restoreFailure
                    : readFailure + " / readable restore failed: " + restoreFailure;
                return false;
            }

            if (!readSucceeded)
            {
                failure = readFailure;
                return false;
            }

            return true;
        }

        private static bool TryResolveTextureImporterForReadableBridge(
            Texture2D source,
            string directReadFailure,
            out string assetPath,
            out TextureImporter importer,
            out string failure)
        {
            assetPath = string.Empty;
            importer = null;
            failure = string.Empty;
            try
            {
                assetPath = AssetDatabase.GetAssetPath(source);
                importer = string.IsNullOrEmpty(assetPath) ? null : AssetImporter.GetAtPath(assetPath) as TextureImporter;
            }
            catch (Exception ex) when (ex is UnityException || ex is InvalidOperationException || ex is ArgumentException || ex is NotSupportedException)
            {
                failure = "texture importer lookup failed for atlas packing: " + source.name + " / " + ex.GetType().Name + ": " + ex.Message + " / " + directReadFailure;
                return false;
            }

            if (importer != null)
                return true;

            failure = "texture must be readable for atlas packing and has no TextureImporter: " + source.name + " / " + directReadFailure;
            return false;
        }

        private static bool TryValidateSourceTextureReadDimensions(Texture2D source, out int expectedPixelCount, out string failure)
        {
            expectedPixelCount = 0;
            failure = string.Empty;
            if (source == null)
            {
                failure = "source texture is null";
                return false;
            }

            long expectedPixelCountLong = (long)source.width * source.height;
            if (expectedPixelCountLong <= 0L || expectedPixelCountLong > int.MaxValue)
            {
                failure = "source texture pixel count is invalid: " + source.name;
                return false;
            }

            if (expectedPixelCountLong > MaxAtlasSourcePixels)
            {
                failure = "source texture pixel count exceeds atlas read ceiling: " + source.name;
                return false;
            }

            expectedPixelCount = (int)expectedPixelCountLong;
            return true;
        }

        private static bool TryValidateSourcePixelBuffer(Texture2D source, Color32[] pixels, out string failure)
        {
            if (!TryValidateSourceTextureReadDimensions(source, out int expectedPixelCount, out failure))
                return false;

            if (pixels == null || pixels.Length != expectedPixelCount)
            {
                failure = "source texture pixel buffer mismatch: " + source.name;
                return false;
            }

            return true;
        }

        private static bool TryRestoreTextureReadableState(TextureImporter importer, bool readable, string assetPath, out string failure)
        {
            failure = string.Empty;
            if (importer == null || importer.isReadable == readable)
                return true;

            try
            {
                importer.isReadable = readable;
                importer.SaveAndReimport();
                return true;
            }
            catch (Exception ex) when (ex is UnityException || ex is IOException || ex is UnauthorizedAccessException || ex is InvalidOperationException || ex is ArgumentException || ex is NotSupportedException)
            {
                failure = "failed to restore texture readability for " + assetPath + ": " + ex.GetType().Name + ": " + ex.Message;
                Debug.LogWarning("[TextureBaker1605] " + failure);
                return false;
            }
        }

        private static void FillAtlasBackground(Color32[] atlasPixels, ProceduralTextureBaker.TextureRole role)
        {
            Color32 clearColor = role == ProceduralTextureBaker.TextureRole.Normal
                ? new Color32(128, 128, 255, 255)
                : role == ProceduralTextureBaker.TextureRole.Mask
                    ? new Color32(0, 255, 255, 0)
                    : new Color32(128, 128, 128, 255);

            for (int i = 0; i < atlasPixels.Length; i++)
                atlasPixels[i] = clearColor;
        }

        private static void BlitWithEdgePadding(
            Color32[] atlasPixels,
            int atlasSize,
            Color32[] sourcePixels,
            int sourceWidth,
            int sourceHeight,
            RectInt uvRect,
            RectInt paddedRect)
        {
            for (int y = paddedRect.y; y < paddedRect.yMax; y++)
            {
                int sourceY = Mathf.Clamp(y - uvRect.y, 0, sourceHeight - 1);
                int atlasRow = y * atlasSize;
                int sourceRow = sourceY * sourceWidth;
                for (int x = paddedRect.x; x < paddedRect.xMax; x++)
                {
                    int sourceX = Mathf.Clamp(x - uvRect.x, 0, sourceWidth - 1);
                    atlasPixels[atlasRow + x] = sourcePixels[sourceRow + sourceX];
                }
            }
        }

        private static void SortCandidatesByAreaDescending(PackCandidate[] candidates)
        {
            for (int i = 1; i < candidates.Length; i++)
            {
                PackCandidate candidate = candidates[i];
                int j = i - 1;
                while (j >= 0 && candidates[j].Area < candidate.Area)
                {
                    candidates[j + 1] = candidates[j];
                    j--;
                }

                candidates[j + 1] = candidate;
            }
        }

        private static bool TryPlaceCandidate(PackCandidate candidate, List<RectInt> freeRects, out RectInt placed)
        {
            placed = default;
            int bestIndex = -1;
            int bestShortSide = int.MaxValue;
            int bestAreaFit = int.MaxValue;

            for (int i = 0; i < freeRects.Count; i++)
            {
                RectInt free = freeRects[i];
                if (candidate.Width > free.width || candidate.Height > free.height)
                    continue;

                int leftoverX = free.width - candidate.Width;
                int leftoverY = free.height - candidate.Height;
                int shortSide = Mathf.Min(leftoverX, leftoverY);
                int areaFit = free.width * free.height - candidate.Area;
                if (shortSide < bestShortSide || (shortSide == bestShortSide && areaFit < bestAreaFit))
                {
                    bestIndex = i;
                    bestShortSide = shortSide;
                    bestAreaFit = areaFit;
                }
            }

            if (bestIndex < 0)
                return false;

            RectInt target = freeRects[bestIndex];
            placed = new RectInt(target.x, target.y, candidate.Width, candidate.Height);
            return true;
        }

        private static void SplitFreeRectangles(List<RectInt> freeRects, RectInt used)
        {
            for (int i = freeRects.Count - 1; i >= 0; i--)
            {
                RectInt free = freeRects[i];
                if (!Intersects(free, used))
                    continue;

                freeRects.RemoveAt(i);

                if (used.x > free.x && used.x < free.xMax)
                    freeRects.Add(new RectInt(free.x, free.y, used.x - free.x, free.height));

                if (used.xMax < free.xMax)
                    freeRects.Add(new RectInt(used.xMax, free.y, free.xMax - used.xMax, free.height));

                if (used.y > free.y && used.y < free.yMax)
                    freeRects.Add(new RectInt(free.x, free.y, free.width, used.y - free.y));

                if (used.yMax < free.yMax)
                    freeRects.Add(new RectInt(free.x, used.yMax, free.width, free.yMax - used.yMax));
            }
        }

        private static void PruneContainedRectangles(List<RectInt> freeRects)
        {
            for (int i = 0; i < freeRects.Count; i++)
            {
                RectInt a = freeRects[i];
                if (a.width <= 0 || a.height <= 0)
                {
                    freeRects.RemoveAt(i);
                    i--;
                    continue;
                }

                bool removed = false;
                for (int j = 0; j < freeRects.Count; j++)
                {
                    if (i == j)
                        continue;

                    if (Contains(freeRects[j], a))
                    {
                        freeRects.RemoveAt(i);
                        i--;
                        removed = true;
                        break;
                    }
                }

                if (removed)
                    continue;
            }
        }

        private static bool Intersects(RectInt a, RectInt b)
        {
            return a.x < b.xMax && a.xMax > b.x && a.y < b.yMax && a.yMax > b.y;
        }

        private static bool Contains(RectInt outer, RectInt inner)
        {
            return inner.x >= outer.x && inner.y >= outer.y && inner.xMax <= outer.xMax && inner.yMax <= outer.yMax;
        }

        private static bool IsSupportedAtlasSize(int atlasSize)
        {
            return atlasSize >= MinimumAtlasSize &&
                   atlasSize <= DefaultAtlasSize &&
                   (atlasSize & (atlasSize - 1)) == 0;
        }

        private static bool TryFindPackedRectForSource(PackedRect[] packed, int sourceIndex, out PackedRect rect, out string failure)
        {
            rect = default;
            failure = string.Empty;
            if (packed == null)
            {
                failure = "packed rect array is null";
                return false;
            }

            for (int i = 0; i < packed.Length; i++)
            {
                if (packed[i].SourceIndex == sourceIndex)
                {
                    rect = packed[i];
                    return true;
                }
            }

            failure = "missing packed rect for source " + sourceIndex;
            return false;
        }

        private static bool TryResolveMeshUv0RemapLayout(Mesh mesh, out int vertexCount, out int stream, out int stride, out int offset, out string failure)
        {
            vertexCount = 0;
            stream = 0;
            stride = 0;
            offset = 0;
            failure = string.Empty;

            if (mesh == null)
            {
                failure = "mesh is null";
                return false;
            }

            vertexCount = mesh.vertexCount;
            if (vertexCount <= 0)
            {
                failure = "mesh has no vertices";
                return false;
            }

            if (!TryResolveUv0Layout(mesh, out stream, out stride, out offset))
            {
                failure = "mesh UV0 is missing or not Float32x2+";
                return false;
            }

            return true;
        }

        private static bool TryResolveUv0Layout(Mesh mesh, out int stream, out int stride, out int offset)
        {
            stream = 0;
            stride = 0;
            offset = 0;

            if (!mesh.HasVertexAttribute(VertexAttribute.TexCoord0))
                return false;

            if (mesh.GetVertexAttributeFormat(VertexAttribute.TexCoord0) != VertexAttributeFormat.Float32 ||
                mesh.GetVertexAttributeDimension(VertexAttribute.TexCoord0) < 2)
                return false;

            stream = mesh.GetVertexAttributeStream(VertexAttribute.TexCoord0);
            stride = mesh.GetVertexBufferStride(stream);
            offset = mesh.GetVertexAttributeOffset(VertexAttribute.TexCoord0);
            return stride > 0 && offset >= 0 && offset + RequiredUv0ByteWidth <= stride;
        }

        private static void CopyVertexBufferFromMesh(Mesh mesh, int stream, NativeArray<byte> vertexBytes)
        {
            Mesh.MeshDataArray meshDataArray = MeshUtility.AcquireReadOnlyMeshData(mesh);
            try
            {
                NativeArray<byte> source = meshDataArray[0].GetVertexData<byte>(stream);
                if (source.Length < vertexBytes.Length)
                    throw new InvalidOperationException("Mesh vertex buffer shorter than expected for UV remap.");

                NativeArray<byte>.Copy(source, vertexBytes, vertexBytes.Length);
            }
            finally
            {
                meshDataArray.Dispose();
            }
        }

        private static unsafe void CopyUvFromVertexBuffer(NativeArray<byte> vertexBytes, NativeArray<float2> uvs, int vertexCount, int stride, int offset)
        {
            byte* src = (byte*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(vertexBytes);
            for (int i = 0; i < vertexCount; i++)
            {
                float* uv = (float*)(src + i * stride + offset);
                uvs[i] = new float2(uv[0], uv[1]);
            }
        }

        private static unsafe void CopyUvToVertexBuffer(NativeArray<float2> uvs, NativeArray<byte> vertexBytes, int vertexCount, int stride, int offset)
        {
            byte* dst = (byte*)NativeArrayUnsafeUtility.GetUnsafePtr(vertexBytes);
            for (int i = 0; i < vertexCount; i++)
            {
                float2 uvValue = uvs[i];
                float* uv = (float*)(dst + i * stride + offset);
                uv[0] = uvValue.x;
                uv[1] = uvValue.y;
            }
        }

        private static bool TryCreateOrUpdateMaterial(
            string materialPath,
            string albedoPath,
            string normalPath,
            string maskPath,
            in MaterialRollbackSnapshot rollback,
            out string failure)
        {
            failure = string.Empty;
            Material material = null;
            bool createdMaterialAsset = false;
            try
            {
                Shader mraoShader = Shader.Find(MraoAtlasShaderName);
                if (mraoShader == null)
                {
                    failure = "missing required shader " + MraoAtlasShaderName;
                    return false;
                }

                Texture2D albedo = AssetDatabase.LoadAssetAtPath<Texture2D>(albedoPath);
                Texture2D normal = AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath);
                Texture2D mask = AssetDatabase.LoadAssetAtPath<Texture2D>(maskPath);
                if (albedo == null || normal == null || mask == null)
                {
                    failure = "atlas texture import missing after write: albedo=" + (albedo != null) + " normal=" + (normal != null) + " mask=" + (mask != null);
                    return false;
                }

                material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                if (material == null)
                {
                    material = new Material(mraoShader);
                    AssetDatabase.CreateAsset(material, materialPath);
                    createdMaterialAsset = true;
                }
                else if (material.shader != mraoShader)
                {
                    material.shader = mraoShader;
                }

                SetTextureIfPresent(material, "_BaseMap", albedo);
                SetTextureIfPresent(material, "_MainTex", albedo);
                SetTextureIfPresent(material, "_NormalMap", normal);
                SetTextureIfPresent(material, "_BumpMap", normal);
                SetTextureIfPresent(material, "_MraoMap", mask);
                SetFloatIfPresent(material, "_MetallicScale", 1f);
                SetFloatIfPresent(material, "_RoughnessScale", 1f);
                SetFloatIfPresent(material, "_OcclusionStrength", 1f);
                SetFloatIfPresent(material, "_EmissionStrength", 1f);
                SetFloatIfPresent(material, "_NormalScale", 1f);

                material.EnableKeyword(NormalMapKeyword);
                material.name = Path.GetFileNameWithoutExtension(materialPath);
                EditorUtility.SetDirty(material);
                return true;
            }
            catch (Exception ex) when (ex is UnityException || ex is InvalidOperationException || ex is ArgumentException || ex is IOException || ex is UnauthorizedAccessException || ex is NotSupportedException)
            {
                if (createdMaterialAsset)
                    TryDeleteNewMaterialAsset(materialPath);
                else if (rollback.Captured)
                    TryRestoreMaterialRollbackSnapshot(rollback);
                else
                    TryDestroyTransientMaterial(material);

                failure = "material creation failed for " + materialPath + ": " + ex.GetType().Name + ": " + ex.Message;
                return false;
            }
        }

        private static bool TryCaptureMaterialRollbackSnapshot(string materialPath, out MaterialRollbackSnapshot snapshot, out string failure)
        {
            snapshot = default;
            failure = string.Empty;
            try
            {
                Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                if (material == null)
                {
                    if (AssetPathExists(materialPath))
                    {
                        failure = "existing material path is not an imported Material asset: " + materialPath;
                        return false;
                    }

                    return true;
                }

                bool hasBaseMap = TryGetTextureProperty(material, "_BaseMap", out Texture baseMap);
                bool hasMainTex = TryGetTextureProperty(material, "_MainTex", out Texture mainTex);
                bool hasNormalMap = TryGetTextureProperty(material, "_NormalMap", out Texture normalMap);
                bool hasBumpMap = TryGetTextureProperty(material, "_BumpMap", out Texture bumpMap);
                bool hasMraoMap = TryGetTextureProperty(material, "_MraoMap", out Texture mraoMap);
                bool hasMetallicScale = TryGetFloatProperty(material, "_MetallicScale", out float metallicScale);
                bool hasRoughnessScale = TryGetFloatProperty(material, "_RoughnessScale", out float roughnessScale);
                bool hasOcclusionStrength = TryGetFloatProperty(material, "_OcclusionStrength", out float occlusionStrength);
                bool hasEmissionStrength = TryGetFloatProperty(material, "_EmissionStrength", out float emissionStrength);
                bool hasNormalScale = TryGetFloatProperty(material, "_NormalScale", out float normalScale);
                bool normalMapKeywordEnabled = material.IsKeywordEnabled(NormalMapKeyword);

                snapshot = new MaterialRollbackSnapshot(
                    material,
                    material.shader,
                    material.name,
                    hasBaseMap,
                    baseMap,
                    hasMainTex,
                    mainTex,
                    hasNormalMap,
                    normalMap,
                    hasBumpMap,
                    bumpMap,
                    hasMraoMap,
                    mraoMap,
                    hasMetallicScale,
                    metallicScale,
                    hasRoughnessScale,
                    roughnessScale,
                    hasOcclusionStrength,
                    occlusionStrength,
                    hasEmissionStrength,
                    emissionStrength,
                    hasNormalScale,
                    normalScale,
                    normalMapKeywordEnabled);
                return true;
            }
            catch (Exception ex) when (ex is UnityException || ex is InvalidOperationException || ex is ArgumentException || ex is IOException || ex is UnauthorizedAccessException || ex is NotSupportedException)
            {
                failure = "material rollback capture failed for " + materialPath + ": " + ex.GetType().Name + ": " + ex.Message;
                return false;
            }
        }

        private static void TryRestoreMaterialRollbackSnapshot(in MaterialRollbackSnapshot snapshot)
        {
            if (!snapshot.Captured || snapshot.Material == null)
                return;

            try
            {
                snapshot.Material.shader = snapshot.Shader;
                snapshot.Material.name = snapshot.Name;
                RestoreTextureProperty(snapshot.Material, "_BaseMap", snapshot.HasBaseMap, snapshot.BaseMap);
                RestoreTextureProperty(snapshot.Material, "_MainTex", snapshot.HasMainTex, snapshot.MainTex);
                RestoreTextureProperty(snapshot.Material, "_NormalMap", snapshot.HasNormalMap, snapshot.NormalMap);
                RestoreTextureProperty(snapshot.Material, "_BumpMap", snapshot.HasBumpMap, snapshot.BumpMap);
                RestoreTextureProperty(snapshot.Material, "_MraoMap", snapshot.HasMraoMap, snapshot.MraoMap);
                RestoreFloatProperty(snapshot.Material, "_MetallicScale", snapshot.HasMetallicScale, snapshot.MetallicScale);
                RestoreFloatProperty(snapshot.Material, "_RoughnessScale", snapshot.HasRoughnessScale, snapshot.RoughnessScale);
                RestoreFloatProperty(snapshot.Material, "_OcclusionStrength", snapshot.HasOcclusionStrength, snapshot.OcclusionStrength);
                RestoreFloatProperty(snapshot.Material, "_EmissionStrength", snapshot.HasEmissionStrength, snapshot.EmissionStrength);
                RestoreFloatProperty(snapshot.Material, "_NormalScale", snapshot.HasNormalScale, snapshot.NormalScale);
                RestoreKeyword(snapshot.Material, NormalMapKeyword, snapshot.NormalMapKeywordEnabled);
                EditorUtility.SetDirty(snapshot.Material);
                TrySaveMaterialRollbackSnapshot(snapshot.Material);
            }
            catch (Exception ex) when (ex is UnityException || ex is InvalidOperationException || ex is ArgumentException || ex is IOException || ex is UnauthorizedAccessException || ex is NotSupportedException)
            {
                Debug.LogWarning("[TextureBaker1605] Failed to restore material rollback snapshot for " + GetObjectNameNoThrow(snapshot.Material) + ": " + ex.Message);
            }
        }

        private static void TryDeleteNewMaterialAsset(string materialPath)
        {
            TryDeleteCreatedAsset(materialPath, false);
        }

        private static void TryDeleteCreatedAtlasOutputs(
            string albedoPath,
            string normalPath,
            string maskPath,
            string materialPath,
            bool albedoExisted,
            bool normalExisted,
            bool maskExisted,
            bool materialExisted)
        {
            TryDeleteCreatedAsset(albedoPath, albedoExisted);
            TryDeleteCreatedAsset(normalPath, normalExisted);
            TryDeleteCreatedAsset(maskPath, maskExisted);
            TryDeleteCreatedAsset(materialPath, materialExisted);
        }

        private static void TryDeleteCreatedAsset(string assetPath, bool existedBefore)
        {
            if (existedBefore || string.IsNullOrEmpty(assetPath))
                return;

            try
            {
                bool deletedByAssetDatabase = AssetDatabase.DeleteAsset(assetPath);
                if (!deletedByAssetDatabase && TryResolveAbsoluteAssetPathNoThrow(assetPath, out string absolutePath))
                {
                    if (File.Exists(absolutePath))
                        File.Delete(absolutePath);

                    string metaPath = absolutePath + ".meta";
                    if (File.Exists(metaPath))
                        File.Delete(metaPath);
                }
            }
            catch (Exception ex) when (ex is UnityException || ex is InvalidOperationException || ex is ArgumentException || ex is IOException || ex is UnauthorizedAccessException || ex is NotSupportedException)
            {
                Debug.LogWarning("[TextureBaker1605] Failed to delete newly created atlas output " + assetPath + ": " + ex.Message);
            }
        }

        private static bool AssetPathExists(string assetPath)
        {
            try
            {
                if (string.IsNullOrEmpty(assetPath))
                    return false;

                if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath) != null)
                    return true;

                return TryResolveAbsoluteAssetPathNoThrow(assetPath, out string absolutePath) && File.Exists(absolutePath);
            }
            catch (Exception ex) when (ex is UnityException || ex is InvalidOperationException || ex is ArgumentException || ex is IOException || ex is UnauthorizedAccessException || ex is NotSupportedException)
            {
                Debug.LogWarning("[TextureBaker1605] Could not prove prior asset state for " + assetPath + ": " + ex.Message);
                return true;
            }
        }

        private static bool TryResolveAbsoluteAssetPathNoThrow(string assetPath, out string absolutePath)
        {
            absolutePath = string.Empty;
            try
            {
                if (string.IsNullOrWhiteSpace(assetPath))
                    return false;

                string normalized = assetPath.Replace('\\', '/');
                if (!normalized.StartsWith("Assets/", StringComparison.Ordinal))
                    return false;

                string projectRoot = Path.GetFullPath(Directory.GetParent(Application.dataPath).FullName);
                string assetsRoot = Path.GetFullPath(Application.dataPath);
                absolutePath = Path.GetFullPath(Path.Combine(projectRoot, normalized.Replace('/', Path.DirectorySeparatorChar)));
                return absolutePath.StartsWith(assetsRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is ArgumentException || ex is NotSupportedException)
            {
                Debug.LogWarning("[TextureBaker1605] Could not resolve atlas output path " + assetPath + ": " + ex.Message);
                absolutePath = string.Empty;
                return false;
            }
        }

        private static void TryDestroyTransientMaterial(Material material)
        {
            if (material == null)
                return;

            try
            {
                if (string.IsNullOrEmpty(AssetDatabase.GetAssetPath(material)))
                    UnityEngine.Object.DestroyImmediate(material);
            }
            catch (Exception ex) when (ex is UnityException || ex is InvalidOperationException || ex is ArgumentException || ex is IOException || ex is UnauthorizedAccessException || ex is NotSupportedException)
            {
                Debug.LogWarning("[TextureBaker1605] Failed to destroy transient material after atlas failure: " + ex.Message);
            }
        }

        private static bool TryGetTextureProperty(Material material, string propertyName, out Texture texture)
        {
            texture = null;
            if (material == null || !material.HasProperty(propertyName))
                return false;

            texture = material.GetTexture(propertyName);
            return true;
        }

        private static bool TryGetFloatProperty(Material material, string propertyName, out float value)
        {
            value = 0f;
            if (material == null || !material.HasProperty(propertyName))
                return false;

            value = material.GetFloat(propertyName);
            return true;
        }

        private static void RestoreTextureProperty(Material material, string propertyName, bool hadProperty, Texture texture)
        {
            if (hadProperty && material.HasProperty(propertyName))
                material.SetTexture(propertyName, texture);
        }

        private static void RestoreFloatProperty(Material material, string propertyName, bool hadProperty, float value)
        {
            if (hadProperty && material.HasProperty(propertyName))
                material.SetFloat(propertyName, value);
        }

        private static void RestoreKeyword(Material material, string keyword, bool enabled)
        {
            if (enabled)
                material.EnableKeyword(keyword);
            else
                material.DisableKeyword(keyword);
        }

        private static void TrySaveMaterialRollbackSnapshot(Material material)
        {
            try
            {
                AssetDatabase.SaveAssets();
            }
            catch (Exception ex) when (ex is UnityException || ex is InvalidOperationException || ex is ArgumentException || ex is IOException || ex is UnauthorizedAccessException || ex is NotSupportedException)
            {
                Debug.LogWarning("[TextureBaker1605] Failed to save restored material rollback snapshot for " + GetObjectNameNoThrow(material) + ": " + ex.Message);
            }
        }

        private static void SetTextureIfPresent(Material material, string propertyName, Texture texture)
        {
            if (material.HasProperty(propertyName))
                material.SetTexture(propertyName, texture);
        }

        private static void SetFloatIfPresent(Material material, string propertyName, float value)
        {
            if (material.HasProperty(propertyName))
                material.SetFloat(propertyName, value);
        }

    }
}
