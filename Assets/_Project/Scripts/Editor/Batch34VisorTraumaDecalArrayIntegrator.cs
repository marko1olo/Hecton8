using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Hecton8.EditorTools
{
    /// <summary>
    /// Bakes promoted Batch34 alpha candidates into the Texture2DArray consumed by DeferredDecalPass.
    /// </summary>
    public static class Batch34VisorTraumaDecalArrayIntegrator
    {
        private const int AtlasSize = 1024;
        private const int AtlasSliceCount = 16;
        private const string AlphaCandidateManifestPath = "Assets/_Project/Art/TEXTURES/Generated/GeminiBatch34SourceAtlases_20260608/AlphaCandidates/GeminiBatch34AlphaCandidates_Manifest.json";
        private const string OutputArrayPath = "Assets/_Project/Art/TEXTURES/Generated/GeminiBatch34SourceAtlases_20260608/TextureArrays/TX_B34_VisorTrauma_DecalArray.asset";

        private static readonly string[] RendererDataPaths =
        {
            "Assets/_Project/Data/PC_Renderer.asset",
            "Assets/_Project/Data/PC_High_Renderer.asset"
        };

        private static readonly SliceBinding[] SliceBindings =
        {
            new SliceBinding(0, "B34-3429", "Scorch/cutter burn"),
            new SliceBinding(1, "B34-3432", "Contamination organic stain"),
            new SliceBinding(2, "B34-3431", "Wetness/acid-like rivulet"),
            new SliceBinding(3, "B34-3423", "Leak rust hull scuff"),
            new SliceBinding(4, "B34-3427", "Pressure glass crack"),
            new SliceBinding(5, "B34-3428", "Warning stripe abrasion"),
            new SliceBinding(6, "B34-3425", "Salt crust mineral deposit"),
            new SliceBinding(7, "B34-3426", "Instrument glass smudge"),
            new SliceBinding(8, "B34-3430", "Barnacle colony decal"),
            new SliceBinding(9, "B34-3433", "Brine vane organic smear"),
            new SliceBinding(10, "B34-3436", "Sponge pore organic"),
            new SliceBinding(11, "B34-3439", "Spore pod translucent smear"),
            new SliceBinding(12, "B34-3445", "Larva egg sac membrane"),
            new SliceBinding(13, "B34-3446", "Scavenged carcass trace"),
            new SliceBinding(14, "B34-3448", "Resource nodule pickup trace"),
            new SliceBinding(15, "B34-3450", "Data core circuit trace")
        };

        [MenuItem("Hecton8/Art/Bake Batch34 Visor Trauma Decal Array")]
        public static void ExecuteMenu()
        {
            BakeAndBindVisorTraumaArray();
        }

        public static void BakeAndBindVisorTraumaArray()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            Dictionary<string, AlphaCandidateEntry> entries = LoadAlphaCandidates();
            if (entries.Count == 0)
            {
                throw new InvalidOperationException($"No alpha candidates found: {AlphaCandidateManifestPath}");
            }

            Texture2DArray decalArray = BakeTextureArray(entries);
            if (decalArray == null)
                throw new InvalidOperationException("Batch34 visor trauma Texture2DArray bake returned null.");

            Texture2DArray savedArray = SaveTextureArray(decalArray);
            if (savedArray == null)
                throw new InvalidOperationException($"Batch34 visor trauma Texture2DArray save failed: {OutputArrayPath}");

            int bound = BindRendererFeatures(savedArray);
            if (bound <= 0)
                throw new InvalidOperationException("No DeferredDecalPass renderer features were bound to the Batch34 visor trauma decal array.");

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[Batch34VisorTraumaDecalArrayIntegrator] Baked {AtlasSliceCount} slices, boundRenderers={bound}, array={OutputArrayPath}");
        }

        private static Dictionary<string, AlphaCandidateEntry> LoadAlphaCandidates()
        {
            Dictionary<string, AlphaCandidateEntry> result = new Dictionary<string, AlphaCandidateEntry>(StringComparer.Ordinal);
            string manifestFilePath = ResolveProjectFilePath(AlphaCandidateManifestPath);
            if (!File.Exists(manifestFilePath))
                throw new InvalidOperationException($"Missing Batch34 visor trauma alpha manifest: {AlphaCandidateManifestPath}");

            AlphaCandidateManifest manifest = JsonUtility.FromJson<AlphaCandidateManifest>(File.ReadAllText(manifestFilePath));
            if (manifest == null || manifest.entries == null || manifest.entries.Length == 0)
                throw new InvalidOperationException($"No alpha candidates found: {AlphaCandidateManifestPath}");

            for (int i = 0; i < manifest.entries.Length; i++)
            {
                AlphaCandidateEntry entry = manifest.entries[i];
                if (entry == null || string.IsNullOrWhiteSpace(entry.id) || string.IsNullOrWhiteSpace(entry.alphaCandidate))
                    throw new InvalidOperationException($"Invalid Batch34 visor trauma alpha manifest entry at index {i}: {AlphaCandidateManifestPath}");

                entry.alphaCandidate = NormalizeAssetPath(entry.alphaCandidate);
                if (!IsProjectAssetPath(entry.alphaCandidate) || !File.Exists(ResolveProjectFilePath(entry.alphaCandidate)))
                    throw new InvalidOperationException($"Missing Batch34 visor trauma alpha source for {entry.id}: {entry.alphaCandidate}");
                if (result.ContainsKey(entry.id))
                    throw new InvalidOperationException($"Duplicate Batch34 visor trauma alpha source id: {entry.id}");

                result[entry.id] = entry;
            }

            return result;
        }

        private static Texture2DArray BakeTextureArray(Dictionary<string, AlphaCandidateEntry> entries)
        {
            Texture2DArray decalArray = new Texture2DArray(AtlasSize, AtlasSize, AtlasSliceCount, TextureFormat.RGBA32, true, false)
            {
                name = "TX_B34_VisorTrauma_DecalArray",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Trilinear,
                anisoLevel = 4
            };

            for (int i = 0; i < SliceBindings.Length; i++)
            {
                SliceBinding binding = SliceBindings[i];
                if (!entries.TryGetValue(binding.SourceId, out AlphaCandidateEntry entry) ||
                    !TryLoadReadableSource(entry.alphaCandidate, out Texture2D source, out bool destroySourceOnExit))
                {
                    throw new InvalidOperationException($"Missing Batch34 visor trauma alpha source for slice {binding.Slice} sourceId={binding.SourceId}");
                }

                try
                {
                    if (source.width != AtlasSize || source.height != AtlasSize)
                    {
                        throw new InvalidOperationException($"Batch34 visor trauma slice {binding.Slice} {binding.SourceId} expected {AtlasSize}x{AtlasSize}, got {source.width}x{source.height}");
                    }

                    decalArray.SetPixels32(source.GetPixels32(0), binding.Slice, 0);
                }
                finally
                {
                    if (destroySourceOnExit && source != null)
                        UnityEngine.Object.DestroyImmediate(source);
                }
            }

            decalArray.Apply(true, true);
            return decalArray;
        }

        private static bool TryLoadReadableSource(string assetPath, out Texture2D texture, out bool destroySourceOnExit)
        {
            texture = null;
            destroySourceOnExit = false;
            string normalized = NormalizeAssetPath(assetPath);
            string projectFilePath = ResolveProjectFilePath(normalized);
            if (!IsProjectAssetPath(normalized) || !File.Exists(projectFilePath))
                return false;

            byte[] bytes;
            try
            {
                bytes = File.ReadAllBytes(projectFilePath);
            }
            catch (Exception exception)
            {
                Debug.LogError($"[Batch34VisorTraumaDecalArrayIntegrator] Failed to read alpha source bytes: path={normalized}\n{exception}");
                return false;
            }

            texture = new Texture2D(2, 2, TextureFormat.RGBA32, false, false)
            {
                name = Path.GetFileNameWithoutExtension(normalized),
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Trilinear
            };

            if (!ImageConversion.LoadImage(texture, bytes, false))
            {
                Debug.LogError($"[Batch34VisorTraumaDecalArrayIntegrator] Failed to decode alpha source image: path={normalized}");
                UnityEngine.Object.DestroyImmediate(texture);
                texture = null;
                return false;
            }

            destroySourceOnExit = true;
            return texture.isReadable;
        }

        private static Texture2DArray SaveTextureArray(Texture2DArray decalArray)
        {
            EnsureFolder(Path.GetDirectoryName(OutputArrayPath)?.Replace("\\", "/"));
            Texture2DArray existing = AssetDatabase.LoadAssetAtPath<Texture2DArray>(OutputArrayPath);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(decalArray, OutputArrayPath);
                return AssetDatabase.LoadAssetAtPath<Texture2DArray>(OutputArrayPath);
            }

            EditorUtility.CopySerialized(decalArray, existing);
            EditorUtility.SetDirty(existing);
            UnityEngine.Object.DestroyImmediate(decalArray);
            return existing;
        }

        private static int BindRendererFeatures(Texture2DArray decalArray)
        {
            int bound = 0;
            for (int i = 0; i < RendererDataPaths.Length; i++)
            {
                UnityEngine.Object[] subAssets = AssetDatabase.LoadAllAssetsAtPath(RendererDataPaths[i]);
                for (int j = 0; j < subAssets.Length; j++)
                {
                    UnityEngine.Object subAsset = subAssets[j];
                    if (subAsset == null || subAsset.GetType().FullName != "Hecton8.Visor.DeferredDecalPass")
                        continue;

                    SerializedObject serialized = new SerializedObject(subAsset);
                    SerializedProperty settings = serialized.FindProperty("settings");
                    SerializedProperty decalAtlas = settings?.FindPropertyRelative("decalAtlas");
                    SerializedProperty atlasSlices = settings?.FindPropertyRelative("atlasSlices");
                    if (decalAtlas == null)
                        continue;

                    decalAtlas.objectReferenceValue = decalArray;
                    if (atlasSlices != null)
                        atlasSlices.intValue = AtlasSliceCount;
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(subAsset);
                    bound++;
                }
            }

            return bound;
        }

        private static void EnsureFolder(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || AssetDatabase.IsValidFolder(path))
                return;

            string parent = Path.GetDirectoryName(path)?.Replace("\\", "/");
            string name = Path.GetFileName(path);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, name);
        }

        private static string NormalizeAssetPath(string path)
        {
            return string.IsNullOrWhiteSpace(path) ? string.Empty : path.Replace("\\", "/").Trim();
        }

        private static bool IsProjectAssetPath(string path)
        {
            return path.StartsWith("Assets/", StringComparison.Ordinal) || path == "Assets";
        }

        private static string ResolveProjectFilePath(string assetPath)
        {
            if (Path.IsPathRooted(assetPath))
                return assetPath;

            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            return Path.GetFullPath(Path.Combine(projectRoot, assetPath));
        }

        private readonly struct SliceBinding
        {
            public readonly int Slice;
            public readonly string SourceId;
            public readonly string Reason;

            public SliceBinding(int slice, string sourceId, string reason)
            {
                Slice = slice;
                SourceId = sourceId;
                Reason = reason;
            }
        }

        [Serializable]
        private sealed class AlphaCandidateManifest
        {
            public AlphaCandidateEntry[] entries;
        }

        [Serializable]
        private sealed class AlphaCandidateEntry
        {
            public string id;
            public string alphaCandidate;
        }
    }
}
