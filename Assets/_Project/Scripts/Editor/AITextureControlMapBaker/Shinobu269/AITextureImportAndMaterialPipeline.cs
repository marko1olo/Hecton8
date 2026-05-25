#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using Unity.Mathematics;
using UnityEngine;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

namespace Hecton8.Editor.AITextureControlMaps
{
    internal sealed class AITextureImportPostprocessor : AssetPostprocessor
    {
        private void OnPreprocessTexture()
        {
            TextureImporter importer = assetImporter as TextureImporter;
            if (importer == null || !AITextureImportPolicy.IsManagedAiTexture(assetPath))
                return;

            AITextureMapKind kind = AITextureImportPolicy.ClassifyMapKind(assetPath);
            TextureImportConfigDTO config = AITextureImportPolicy.BuildConfig(assetPath, kind);
            AITextureImportPolicy.Apply(importer, kind, config);
            importer.userData = AITextureRollbackFence.UserData;
        }

        private void OnPostprocessTexture(Texture2D texture)
        {
            if (texture == null || !AITextureImportPolicy.IsManagedAiTexture(assetPath))
                return;

            AITextureMapKind kind = AITextureImportPolicy.ClassifyMapKind(assetPath);
            TextureImportConfigDTO config = AITextureImportPolicy.BuildConfig(assetPath, kind);
            AITexturePostImportDrain.Enqueue(assetPath, kind, config);
        }
    }

    internal static class AITexturePostImportDrain
    {
        private static readonly object Sync = new object();
        private static readonly List<PendingPostImport> PendingPostImports = new List<PendingPostImport>(64); // COLD ALLOC: editor post-import queue - owner: AITexturePostImportDrain
        private static readonly List<PendingPostImport> ScratchPostImports = new List<PendingPostImport>(64); // COLD ALLOC: editor post-import scratch queue - owner: AITexturePostImportDrain
        private static bool UpdateRegistered;

        static AITexturePostImportDrain()
        {
            AssemblyReloadEvents.beforeAssemblyReload += Clear;
            EditorApplication.quitting += Clear;
        }

        internal static void Enqueue(string assetPath, AITextureMapKind kind, TextureImportConfigDTO config)
        {
            if (string.IsNullOrEmpty(assetPath))
                return;

            PendingPostImport item;
            item.AssetPath = assetPath;
            item.Kind = kind;
            item.Config = config;

            lock (Sync)
                EnqueuePendingPostImport(item);

            if (!UpdateRegistered)
            {
                UpdateRegistered = true;
                EditorApplication.update += DrainPendingPostImports;
            }
        }

        private static void DrainPendingPostImports()
        {
            ScratchPostImports.Clear();
            lock (Sync)
            {
                for (int i = 0; i < PendingPostImports.Count; i++)
                    ScratchPostImports.Add(PendingPostImports[i]);
                PendingPostImports.Clear();
            }

            for (int i = 0; i < ScratchPostImports.Count; i++)
            {
                PendingPostImport item = ScratchPostImports[i];
                Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(item.AssetPath);
                AITextureRollbackFence.MarkAsset(texture, item.AssetPath);
                AITextureMaterialBinder.BindTexture(item.AssetPath, texture);
                AITexturePipelineReport.WriteIngestionReport(item.AssetPath, item.Kind, item.Config);
            }

            ScratchPostImports.Clear();

            bool hasPending;
            lock (Sync)
                hasPending = PendingPostImports.Count > 0;

            if (!hasPending)
            {
                EditorApplication.update -= DrainPendingPostImports;
                UpdateRegistered = false;
            }
        }

        private static void EnqueuePendingPostImport(PendingPostImport item)
        {
            for (int i = 0; i < PendingPostImports.Count; i++)
            {
                PendingPostImport existing = PendingPostImports[i];
                if (!string.Equals(existing.AssetPath, item.AssetPath, StringComparison.OrdinalIgnoreCase))
                    continue;

                existing.Kind = item.Kind;
                existing.Config = item.Config;
                PendingPostImports[i] = existing;
                return;
            }

            PendingPostImports.Add(item);
        }

        private static void Clear()
        {
            lock (Sync)
                PendingPostImports.Clear();
            ScratchPostImports.Clear();
            if (UpdateRegistered)
            {
                EditorApplication.update -= DrainPendingPostImports;
                UpdateRegistered = false;
            }
        }

        private struct PendingPostImport
        {
            public string AssetPath;
            public AITextureMapKind Kind;
            public TextureImportConfigDTO Config;
        }
    }

    internal static class AITextureImportPolicy
    {
        private const uint HashBc7 = 0x37434248u;
        private const uint HashBc5 = 0x35434248u;
        private const uint HashAstc6 = 0x36415354u;

        internal static bool IsManagedAiTexture(string path)
        {
            if (string.IsNullOrEmpty(path))
                return false;

            string normalized = path.Replace('\\', '/');
            if (!normalized.StartsWith(AITextureControlMapConstants.ImportedTextureFolder, StringComparison.OrdinalIgnoreCase))
                return false;

            return normalized.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
                   normalized.EndsWith(".tga", StringComparison.OrdinalIgnoreCase) ||
                   normalized.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
                   normalized.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase);
        }

        internal static AITextureMapKind ClassifyMapKind(string path)
        {
            string lower = BuildLowercasePath(path);
            if (lower.Contains("_arm") || lower.Contains("-arm") || lower.Contains("_mask") || lower.Contains("_packed"))
                return AITextureMapKind.Arm;
            if (lower.Contains("_normal") || lower.Contains("_n.") || lower.Contains("_n_") || lower.Contains("nrm"))
                return AITextureMapKind.Normal;
            if (lower.Contains("_curvature"))
                return AITextureMapKind.Curvature;
            if (lower.Contains("_colorid") || lower.Contains("_color_id"))
                return AITextureMapKind.ColorId;
            if (lower.Contains("_depth"))
                return AITextureMapKind.Depth;
            if (lower.Contains("_albedo") || lower.Contains("_basecolor") || lower.Contains("_base"))
                return AITextureMapKind.Albedo;

            return AITextureMapKind.Albedo;
        }

        internal static TextureImportConfigDTO BuildConfig(string path, AITextureMapKind kind)
        {
            TextureImportConfigDTO config;
            bool normal = kind == AITextureMapKind.Normal;
            bool srgb = kind == AITextureMapKind.Albedo;
            bool hasProfile = AITextureProfileCsv.TrySelectProfileForAsset(path, out AITextureIngestionProfile profile);
            uint standaloneHash = normal ? HashBc5 : NormalizeStandaloneFormatHash(hasProfile ? profile.StandaloneFormatHash : HashBc7);
            uint androidHash = hasProfile ? profile.AndroidFormatHash : HashAstc6;
            if (androidHash != HashAstc6)
                androidHash = HashAstc6;

            uint flags = (uint)AITextureImportFlags.Mipmaps;
            if (srgb)
                flags |= (uint)AITextureImportFlags.Srgb;
            if (normal)
                flags |= (uint)AITextureImportFlags.NormalMap;
            else
                flags |= (uint)AITextureImportFlags.MaskMap;
            flags |= standaloneHash == HashBc5 ? (uint)AITextureImportFlags.StandaloneBc5 : (uint)AITextureImportFlags.StandaloneBc7;
            if (androidHash == HashAstc6)
                flags |= (uint)AITextureImportFlags.AndroidAstc;

            config.FormatHash = standaloneHash;
            config.MaxSize = (uint)(hasProfile ? Mathf.Clamp(profile.Resolution, 64, AITextureControlMapConstants.HeroBakeResolution) : SelectMaxSize(path));
            config.Flags = flags;
            config._pad0 = 0u;
            return config;
        }

        internal static void Apply(TextureImporter importer, AITextureMapKind kind, TextureImportConfigDTO config)
        {
            bool normal = kind == AITextureMapKind.Normal;
            bool srgb = (config.Flags & (uint)AITextureImportFlags.Srgb) != 0u;
            int maxSize = Mathf.Clamp((int)config.MaxSize, 64, AITextureControlMapConstants.HeroBakeResolution);
            importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.sRGBTexture = srgb;
            importer.mipmapEnabled = (config.Flags & (uint)AITextureImportFlags.Mipmaps) != 0u;
            importer.isReadable = false;
            importer.textureCompression = TextureImporterCompression.Compressed;
            importer.crunchedCompression = false;
            importer.maxTextureSize = maxSize;
            importer.npotScale = TextureImporterNPOTScale.ToNearest;
            importer.alphaIsTransparency = false;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.filterMode = FilterMode.Trilinear;

            TextureImporterPlatformSettings standalone = importer.GetPlatformTextureSettings("Standalone");
            standalone.overridden = true;
            standalone.maxTextureSize = maxSize;
            standalone.format = SelectStandaloneTextureFormat(config.FormatHash, normal);
            standalone.textureCompression = TextureImporterCompression.Compressed;
            standalone.crunchedCompression = false;
            importer.SetPlatformTextureSettings(standalone);

            TextureImporterPlatformSettings android = importer.GetPlatformTextureSettings("Android");
            android.overridden = true;
            android.maxTextureSize = maxSize;
            android.format = TextureImporterFormat.ASTC_6x6;
            android.textureCompression = TextureImporterCompression.Compressed;
            android.crunchedCompression = false;
            importer.SetPlatformTextureSettings(android);
        }

        private static TextureImporterFormat SelectStandaloneTextureFormat(uint formatHash, bool normal)
        {
            if (normal || formatHash == HashBc5)
                return TextureImporterFormat.BC5;
            return TextureImporterFormat.BC7;
        }

        private static uint NormalizeStandaloneFormatHash(uint formatHash)
        {
            return formatHash == HashBc5 ? HashBc5 : HashBc7;
        }

        private static int SelectMaxSize(string path)
        {
            string lower = BuildLowercasePath(path);
            float inferredQuality = 0.5f;
            if (lower.Contains("debris") || lower.Contains("scatter") || lower.Contains("small"))
                inferredQuality = 0.0f;
            if (lower.Contains("hero") || lower.Contains("large") || lower.Contains("module"))
                inferredQuality = 1.0f;

            float authoredQuality = SelectAuthoredQualityWeight(lower, inferredQuality);
            float q = authoredQuality * authoredQuality * (3.0f - 2.0f * authoredQuality);
            float size = math.lerp(AITextureControlMapConstants.DebrisBakeResolution, AITextureControlMapConstants.HeroBakeResolution, q);
            int aligned = Mathf.RoundToInt(size * (1.0f / 64.0f)) * 64;
            return Mathf.Clamp(aligned, AITextureControlMapConstants.DebrisBakeResolution, AITextureControlMapConstants.HeroBakeResolution);
        }

        private static float SelectAuthoredQualityWeight(string lower, float fallback)
        {
            int marker = lower.IndexOf("_q", StringComparison.Ordinal);
            if (marker < 0 || marker + 3 >= lower.Length)
                return Mathf.Clamp01(fallback);

            int value = 0;
            int digits = 0;
            for (int i = marker + 2; i < lower.Length && digits < 3; i++)
            {
                int digit = lower[i] - '0';
                if ((uint)digit > 9u)
                    break;

                value = value * 10 + digit;
                digits++;
            }

            if (digits == 0)
                return Mathf.Clamp01(fallback);

            return Mathf.Clamp01(value * 0.01f);
        }

        private static string BuildLowercasePath(string path)
        {
            return string.IsNullOrEmpty(path) ? string.Empty : path.Replace('\\', '/').ToLowerInvariant();
        }
    }

    internal static class AITextureMaterialBinder
    {
        private static readonly string[] AlbedoTokens = { "_Albedo", "_BaseColor", "_BaseMap", "_Base" };
        private static readonly string[] ArmTokens = { "_ARM", "_Mask", "_Packed" };
        private static readonly string[] NormalTokens = { "_Normal", "_Nrm", "_NRM", "_N" };
        private static readonly string[] ControlTokens = { "_Curvature", "_ColorID", "_Color_ID", "_Depth" };

        internal static void BindTexture(string texturePath, Texture2D texture)
        {
            if (texture == null || string.IsNullOrEmpty(texturePath))
                return;

            AITextureMapKind kind = AITextureImportPolicy.ClassifyMapKind(texturePath);
            if (kind == AITextureMapKind.Curvature || kind == AITextureMapKind.ColorId || kind == AITextureMapKind.Depth)
                return;

            string assetKey = BuildAssetKey(texturePath);
            EnsureAssetFolder("Assets/_Project");
            EnsureAssetFolder("Assets/_Project/Materials");
            EnsureAssetFolder(AITextureControlMapConstants.ImportedMaterialFolder);
            string materialPath = AITextureControlMapConstants.ImportedMaterialFolder + "/MAT_" + assetKey + "_UberNoir.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null)
            {
                Shader shader = FindUberNoirShader();
                if (shader == null)
                {
                    Hecton8.Core.H8Debug.LogError("[AITextureMaterialBinder] UberNoir shader missing; material not created for " + materialPath + ".");
                    WritePrefabBindingReport(assetKey, "NONE", "BLOCKED_MISSING_UBERNOIR_SHADER", string.Empty, string.Empty, -1);
                    return;
                }

                material = new Material(shader)
                {
                    name = "MAT_" + assetKey + "_UberNoir"
                };
                AssetDatabase.CreateAsset(material, materialPath);
            }

            AssignTexture(material, texture, kind);
            AITextureRollbackFence.MarkAsset(material, materialPath);
            EditorUtility.SetDirty(material);
            AssignMaterialFromManifest(assetKey, material);
        }

        private static void AssignTexture(Material material, Texture2D texture, AITextureMapKind kind)
        {
            if (kind == AITextureMapKind.Albedo)
            {
                SetTextureIfPossible(material, "_BaseMap", texture);
                SetTextureIfPossible(material, "_MainTex", texture);
            }
            else if (kind == AITextureMapKind.Arm)
            {
                SetTextureIfPossible(material, "_ArmMap", texture);
                SetTextureIfPossible(material, "_MaskMap", texture);
                SetTextureIfPossible(material, "_MetallicGlossMap", texture);
            }
            else if (kind == AITextureMapKind.Normal)
            {
                SetTextureIfPossible(material, "_BumpMap", texture);
                SetTextureIfPossible(material, "_NormalMap", texture);
                material.EnableKeyword("_NORMALMAP");
            }
        }

        private static void SetTextureIfPossible(Material material, string propertyName, Texture texture)
        {
            if (material == null || texture == null)
                return;

            if (material.HasProperty(propertyName))
                material.SetTexture(propertyName, texture);
        }

        private static Shader FindUberNoirShader()
        {
            return Shader.Find("Hecton8/Rendering/UberNoir");
        }

        private static void AssignMaterialFromManifest(string assetKey, Material material)
        {
            if (material == null || string.IsNullOrEmpty(assetKey))
                return;

            string manifestPath = AITextureControlMapConstants.PrefabBindingManifestPath;
            if (!File.Exists(manifestPath))
            {
                WritePrefabBindingReport(assetKey, material.name, "DRY_RUN_NO_MANIFEST", string.Empty, string.Empty, -1);
                return;
            }

            string[] lines = File.ReadAllLines(manifestPath);
            for (int i = 0; i < lines.Length; i++)
            {
                if (!ParseManifestRow(lines[i], out string key, out string prefabPath, out string rendererPath, out int materialSlot))
                    continue;
                if (!string.Equals(key, assetKey, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (!prefabPath.StartsWith("Assets/_Project/Prefabs/", StringComparison.OrdinalIgnoreCase))
                {
                    WritePrefabBindingReport(assetKey, material.name, "REJECTED_OUT_OF_DOMAIN", prefabPath, rendererPath, materialSlot);
                    return;
                }

                if (string.IsNullOrEmpty(rendererPath) || materialSlot < 0)
                {
                    WritePrefabBindingReport(assetKey, material.name, "REJECTED_MANIFEST_REQUIRES_RENDERER_SLOT", prefabPath, rendererPath, materialSlot);
                    return;
                }

                ApplyMaterialToPrefab(assetKey, prefabPath, rendererPath, materialSlot, material);
                return;
            }

            WritePrefabBindingReport(assetKey, material.name, "DRY_RUN_NO_MATCH", string.Empty, string.Empty, -1);
        }

        private static void ApplyMaterialToPrefab(string assetKey, string prefabPath, string rendererPath, int materialSlot, Material material)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) == null)
            {
                WritePrefabBindingReport(assetKey, material.name, "REJECTED_PREFAB_ASSET_MISSING", prefabPath, rendererPath, materialSlot);
                return;
            }

            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
            if (root == null)
            {
                WritePrefabBindingReport(assetKey, material.name, "REJECTED_LOAD_FAILED", prefabPath, rendererPath, materialSlot);
                return;
            }

            try
            {
                Transform rendererTransform = root.transform.Find(rendererPath);
                MeshRenderer renderer = null;
                if (rendererTransform != null)
                {
                    renderer = rendererTransform.TryGetComponent(out MeshRenderer resolvedRenderer) ? resolvedRenderer : null;
                }
                if (renderer == null)
                {
                    WritePrefabBindingReport(assetKey, material.name, "REJECTED_RENDERER_PATH_MISSING", prefabPath, rendererPath, materialSlot);
                    return;
                }

                Material[] materials = renderer.sharedMaterials; // COLD ALLOC: Unity sharedMaterials copy for manifest-approved single renderer-slot mutation - owner: AITextureMaterialBinder
                if (materialSlot >= materials.Length)
                {
                    WritePrefabBindingReport(assetKey, material.name, "REJECTED_MATERIAL_SLOT_OUT_OF_RANGE", prefabPath, rendererPath, materialSlot);
                    return;
                }

                materials[materialSlot] = material;
                renderer.sharedMaterials = materials;

                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                AITextureRollbackFence.WriteExclusionReport(prefabPath, material.name);
                WritePrefabBindingReport(assetKey, material.name, "ASSIGNED_MANIFEST_RENDERER_SLOT", prefabPath, rendererPath, materialSlot);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static bool ParseManifestRow(string row, out string assetKey, out string prefabPath, out string rendererPath, out int materialSlot)
        {
            assetKey = string.Empty;
            prefabPath = string.Empty;
            rendererPath = string.Empty;
            materialSlot = -1;
            if (string.IsNullOrWhiteSpace(row) || row[0] == '#')
                return false;

            int firstComma = row.IndexOf(',');
            if (firstComma <= 0 || firstComma >= row.Length - 1)
                return false;

            int secondComma = row.IndexOf(',', firstComma + 1);
            if (secondComma <= firstComma + 1 || secondComma >= row.Length - 1)
                return false;

            int thirdComma = row.IndexOf(',', secondComma + 1);
            if (thirdComma <= secondComma + 1 || thirdComma >= row.Length - 1)
                return false;

            assetKey = row.Substring(0, firstComma).Trim();
            prefabPath = row.Substring(firstComma + 1, secondComma - firstComma - 1).Trim().Replace('\\', '/');
            rendererPath = row.Substring(secondComma + 1, thirdComma - secondComma - 1).Trim().Replace('\\', '/');
            string slotText = row.Substring(thirdComma + 1).Trim();
            if (!int.TryParse(slotText, NumberStyles.Integer, CultureInfo.InvariantCulture, out materialSlot))
                materialSlot = -1;
            return !string.IsNullOrEmpty(assetKey) && !string.IsNullOrEmpty(prefabPath);
        }

        private static void WritePrefabBindingReport(string assetKey, string materialName, string status, string prefabPath, string rendererPath, int materialSlot)
        {
            EnsureFileFolder(AITextureControlMapConstants.PrefabBindingReportPath);
            StringBuilder builder = new StringBuilder(768); // COLD ALLOC: StringBuilder[768] - editor prefab binding authority report - owner: AITextureMaterialBinder
            builder.Append("{\n");
            AppendJson(builder, "schema", "hecton8.ai_texture_prefab_binding.v1", true);
            AppendJson(builder, "assetKey", assetKey, true);
            AppendJson(builder, "materialName", materialName, true);
            AppendJson(builder, "prefabPath", prefabPath, true);
            AppendJson(builder, "rendererPath", rendererPath, true);
            AppendJson(builder, "materialSlot", materialSlot, true);
            AppendJson(builder, "manifestPath", AITextureControlMapConstants.PrefabBindingManifestPath, true);
            AppendJson(builder, "status", status, false);
            builder.Append("}\n");
            File.WriteAllText(AITextureControlMapConstants.PrefabBindingReportPath, builder.ToString());
        }

        internal static string BuildAssetKey(string texturePath)
        {
            string fileName = Path.GetFileNameWithoutExtension(texturePath);
            if (fileName.StartsWith("TX_", StringComparison.OrdinalIgnoreCase))
                fileName = fileName.Substring(3);

            fileName = StripTokens(fileName, AlbedoTokens);
            fileName = StripTokens(fileName, ArmTokens);
            fileName = StripTokens(fileName, NormalTokens);
            fileName = StripTokens(fileName, ControlTokens);
            return Sanitize(fileName);
        }

        private static string StripTokens(string value, string[] tokens)
        {
            for (int i = 0; i < tokens.Length; i++)
            {
                string token = tokens[i];
                if (value.EndsWith(token, StringComparison.OrdinalIgnoreCase))
                    return value.Substring(0, value.Length - token.Length);
            }

            return value;
        }

        private static string Sanitize(string value)
        {
            if (string.IsNullOrEmpty(value))
                return "Unnamed";

            char[] chars = value.ToCharArray();
            for (int i = 0; i < chars.Length; i++)
            {
                char c = chars[i];
                if (!char.IsLetterOrDigit(c) && c != '_' && c != '-')
                    chars[i] = '_';
            }

            return new string(chars);
        }

        private static void EnsureAssetFolder(string assetPath)
        {
            if (AssetDatabase.IsValidFolder(assetPath))
                return;

            int slash = assetPath.LastIndexOf('/');
            if (slash <= 0)
                return;

            string parent = assetPath.Substring(0, slash);
            string folder = assetPath.Substring(slash + 1);
            EnsureAssetFolder(parent);
            if (!AssetDatabase.IsValidFolder(assetPath))
                AssetDatabase.CreateFolder(parent, folder);
        }

        private static void EnsureFileFolder(string path)
        {
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);
        }

        private static void AppendJson(StringBuilder builder, string key, string value, bool comma)
        {
            builder.Append("  \"").Append(key).Append("\": \"").Append(Escape(value)).Append('"');
            builder.Append(comma ? ",\n" : "\n");
        }

        private static void AppendJson(StringBuilder builder, string key, int value, bool comma)
        {
            builder.Append("  \"").Append(key).Append("\": ").Append(value.ToString(CultureInfo.InvariantCulture));
            builder.Append(comma ? ",\n" : "\n");
        }

        private static string Escape(string value)
        {
            return string.IsNullOrEmpty(value) ? string.Empty : value.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }
    }

    internal static class AITextureRollbackFence
    {
        internal const string UserData = "H8_AI_TEXTURE_PRESENTATION_ONLY;RollbackExclusion=REQUESTED_BY_EDITOR;StateRingBuffer=RUNTIME_OWNER_VERIFICATION_REQUIRED;Merkle=RUNTIME_OWNER_VERIFICATION_REQUIRED";
        private const string LabelAiTexture = "H8_AI_TEXTURE";
        private const string LabelRollbackExcluded = "ROLLBACK_EXCLUSION_REQUEST_PRESENTATION";

        internal static void MarkAsset(Object asset, string assetPath)
        {
            if (asset == null)
                return;

            string[] existing = AssetDatabase.GetLabels(asset);
            bool hasAiTexture = false;
            bool hasRollbackExcluded = false;
            for (int i = 0; i < existing.Length; i++)
            {
                string label = existing[i];
                hasAiTexture |= string.Equals(label, LabelAiTexture, StringComparison.Ordinal);
                hasRollbackExcluded |= string.Equals(label, LabelRollbackExcluded, StringComparison.Ordinal);
            }

            if (!hasAiTexture || !hasRollbackExcluded)
            {
                int extra = (hasAiTexture ? 0 : 1) + (hasRollbackExcluded ? 0 : 1);
                string[] labels = new string[existing.Length + extra]; // COLD ALLOC: string[labelCount] - editor rollback label merge only when labels are missing - owner: AITextureRollbackFence
                for (int i = 0; i < existing.Length; i++)
                    labels[i] = existing[i];

                int cursor = existing.Length;
                if (!hasAiTexture)
                    labels[cursor++] = LabelAiTexture;
                if (!hasRollbackExcluded)
                    labels[cursor] = LabelRollbackExcluded;

                AssetDatabase.SetLabels(asset, labels);
            }

            AssetImporter importer = AssetImporter.GetAtPath(assetPath);
            if (importer != null && importer.userData != UserData)
            {
                importer.userData = UserData;
                EditorUtility.SetDirty(importer);
            }
        }

        internal static void WriteExclusionReport(string prefabPath, string materialName)
        {
            EnsureFileFolder(AITextureControlMapConstants.RollbackExclusionReportPath);
            StringBuilder builder = new StringBuilder(1024); // COLD ALLOC: StringBuilder[1024] - editor rollback exclusion report - owner: AITextureRollbackFence
            builder.Append("{\n");
            AppendJson(builder, "schema", "hecton8.ai_texture_rollback_exclusion.v1", true);
            AppendJson(builder, "prefabPath", prefabPath, true);
            AppendJson(builder, "materialName", materialName, true);
            AppendJson(builder, "proofClass", "EDITOR_PRESENTATION_ROUTE_CARD", true);
            AppendJson(builder, "stateRingBuffer", "RUNTIME_OWNER_VERIFICATION_REQUIRED", true);
            AppendJson(builder, "merkleTree", "RUNTIME_OWNER_VERIFICATION_REQUIRED", true);
            AppendJson(builder, "reason", "Static texture/material assets are immutable presentation data. SHINOBU_269 can mark and report the exclusion request, but runtime StateRingBuffer/Merkle ownership must verify the final hash route.", true);
            AppendJson(builder, "status", "PENDING_RUNTIME_OWNER_VERIFICATION", false);
            builder.Append("}\n");
            File.WriteAllText(AITextureControlMapConstants.RollbackExclusionReportPath, builder.ToString());
        }

        private static void AppendJson(StringBuilder builder, string key, string value, bool comma)
        {
            builder.Append("  \"").Append(key).Append("\": \"").Append(Escape(value)).Append('"');
            builder.Append(comma ? ",\n" : "\n");
        }

        private static string Escape(string value)
        {
            return string.IsNullOrEmpty(value) ? string.Empty : value.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        private static void EnsureFileFolder(string path)
        {
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);
        }
    }

    internal static class AITexturePipelineReport
    {
        internal static void WriteBakeReport(int modelCount, int resolution, int completedPasses, int criticalWarnings, double renderMilliseconds, double encodeMilliseconds, double writeMilliseconds)
        {
            EnsureFileFolder(AITextureControlMapConstants.ReportPath);
            StringBuilder builder = new StringBuilder(1536); // COLD ALLOC: StringBuilder[1536] - editor bake pipeline report - owner: AITexturePipelineReport
            builder.Append("{\n");
            AppendJson(builder, "schema", "hecton8.ai_texture_pipeline_report.v1", true);
            AppendJson(builder, "modelsProcessed", modelCount, true);
            AppendJson(builder, "resolution", resolution, true);
            AppendJson(builder, "passesCompleted", completedPasses, true);
            AppendJson(builder, "renderMilliseconds", renderMilliseconds.ToString("0.000", CultureInfo.InvariantCulture), true);
            AppendJson(builder, "encodeMilliseconds", encodeMilliseconds.ToString("0.000", CultureInfo.InvariantCulture), true);
            AppendJson(builder, "writeMilliseconds", writeMilliseconds.ToString("0.000", CultureInfo.InvariantCulture), true);
            AppendJson(builder, "compressionStandalone", "BC7 color/ARM, BC5 normal", true);
            AppendJson(builder, "compressionAndroid", "ASTC_6x6", true);
            AppendJson(builder, "criticalWarnings", criticalWarnings, true);
            AppendJson(builder, "status", criticalWarnings > 0 ? "CRITICAL_WARNING" : "PENDING_UNITY_VERIFICATION", false);
            builder.Append("}\n");
            File.WriteAllText(AITextureControlMapConstants.ReportPath, builder.ToString());
        }

        internal static void WriteIngestionReport(string assetPath, AITextureMapKind kind, TextureImportConfigDTO config)
        {
            EnsureFileFolder(AITextureControlMapConstants.IngestionReportPath);
            StringBuilder builder = new StringBuilder(1280); // COLD ALLOC: StringBuilder[1280] - editor ingestion pipeline report - owner: AITexturePipelineReport
            builder.Append("{\n");
            AppendJson(builder, "schema", "hecton8.ai_texture_ingestion_report.v1", true);
            AppendJson(builder, "assetPath", assetPath, true);
            AppendJson(builder, "mapKind", SelectMapKindToken(kind), true);
            AppendJson(builder, "maxSize", (int)config.MaxSize, true);
            AppendJson(builder, "formatHash", config.FormatHash.ToString(CultureInfo.InvariantCulture), true);
            AppendJson(builder, "flags", config.Flags.ToString(CultureInfo.InvariantCulture), true);
            AppendJson(builder, "compressionStandalone", kind == AITextureMapKind.Normal ? "BC5" : "BC7", true);
            AppendJson(builder, "compressionAndroid", "ASTC_6x6", true);
            AppendJson(builder, "rollbackFence", "Presentation-only route card emitted; runtime owner verification required", true);
            AppendJson(builder, "status", "PENDING_UNITY_VERIFICATION", false);
            builder.Append("}\n");
            File.WriteAllText(AITextureControlMapConstants.IngestionReportPath, builder.ToString());
        }

        private static string SelectMapKindToken(AITextureMapKind kind)
        {
            switch (kind)
            {
                case AITextureMapKind.Albedo:
                    return "Albedo";
                case AITextureMapKind.Normal:
                    return "Normal";
                case AITextureMapKind.Arm:
                    return "Arm";
                case AITextureMapKind.Curvature:
                    return "Curvature";
                case AITextureMapKind.ColorId:
                    return "ColorId";
                case AITextureMapKind.Depth:
                    return "Depth";
                default:
                    return "Unknown";
            }
        }

        private static void AppendJson(StringBuilder builder, string key, string value, bool comma)
        {
            builder.Append("  \"").Append(key).Append("\": \"").Append(Escape(value)).Append('"');
            builder.Append(comma ? ",\n" : "\n");
        }

        private static void AppendJson(StringBuilder builder, string key, int value, bool comma)
        {
            builder.Append("  \"").Append(key).Append("\": ").Append(value.ToString(CultureInfo.InvariantCulture));
            builder.Append(comma ? ",\n" : "\n");
        }

        private static string Escape(string value)
        {
            return string.IsNullOrEmpty(value) ? string.Empty : value.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        private static void EnsureFileFolder(string path)
        {
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);
        }
    }
}
#endif
