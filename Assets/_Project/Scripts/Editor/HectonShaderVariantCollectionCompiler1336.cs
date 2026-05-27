#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Hecton8.EditorTools
{
    internal static class HectonShaderVariantCollectionCompiler1336
    {
        private const string MenuPath = "Hecton/Validation/Rendering/Compile Bootstrap Shader Variant Collection 1336";
        private const string OutputPath = "Assets/_Project/Art/Shaders/Variants/Hecton8MasterVariants.shadervariants";
        private const string ReportPath = "Docs/Reports/BOOTSTRAP_SHADER_VARIANT_COMPILER_1336.json";
        private const string BootstrapScenePath = "Assets/_Project/Scenes/00_BOOTSTRAP.unity";
        private const int MaxCompiledVariants = 512;

        private static readonly string[] BootstrapDependencyRoots =
        {
            BootstrapScenePath,
            "Assets/_Project/Scenes/01_MAIN_MENU.unity",
            "Assets/_Project/Scenes/02_HECTON_WORLD.unity",
            "Assets/_Project/Prefabs/Player.prefab",
            "Assets/_Project/Prefabs/Suit_HUD_Canvas.prefab",
            "Assets/_Project/Prefabs/Tools/Held/Tool_Scanner_Held.prefab",
            "Assets/_Project/Prefabs/Tools/Held/Tool_Repair_Held.prefab",
            "Assets/_Project/Prefabs/Tools/Held/Tool_Flashlight_Held.prefab",
            "Assets/_Project/Prefabs/Items/Tools/Item_Tool_Scanner_World.prefab",
            "Assets/_Project/Prefabs/Items/Tools/Item_Tool_Repair_World.prefab",
            "Assets/_Project/Prefabs/Items/Tools/Item_Tool_Flashlight_World.prefab",
            "Assets/_Project/Art/Materials/VFX",
            "Assets/_Project/Art/Materials/Tools"
        };

        private static readonly PassType[] WarmupPassTypes =
        {
            PassType.Normal,
            PassType.ScriptableRenderPipeline,
            PassType.ScriptableRenderPipelineDefaultUnlit,
            PassType.ShadowCaster
        };

        private static readonly string[] EmptyKeywords = new string[0];

        private static readonly string[] DirectShaderReferenceRoots =
        {
            "Assets/_Project/Art/Shaders/Core/Hecton8_UberNoir.shader",
            "Assets/_Project/Art/Shaders/SuitVisor.shader",
            "Assets/_Project/Shaders/UI/Hecton_IGNDitheredBackground.shader",
            "Assets/_Project/Art/Shaders/Hecton_ScannerMarkerInstanced.shader",
            "Assets/_Project/Art/Shaders/Hecton_ScannerPulseInstanced.shader"
        };

        private static readonly DirectShaderKeywordEntry[] DirectShaderKeywordManifest =
        {
            new DirectShaderKeywordEntry("Assets/_Project/Art/Shaders/Core/Hecton8_UberNoir.shader", "INSTANCING_ON"),
            new DirectShaderKeywordEntry("Assets/_Project/Art/Shaders/Core/Hecton8_UberNoir.shader", "DOTS_INSTANCING_ON"),
            new DirectShaderKeywordEntry("Assets/_Project/Art/Shaders/Core/Hecton8_UberNoir.shader", "DOTS_INSTANCING_ON", "INSTANCING_ON"),
            new DirectShaderKeywordEntry("Assets/_Project/Art/Shaders/SuitVisor.shader", "_HUD_PHOSPHOR_MODE"),
            new DirectShaderKeywordEntry("Assets/_Project/Art/Shaders/Hecton_ScannerMarkerInstanced.shader", "INSTANCING_ON"),
            new DirectShaderKeywordEntry("Assets/_Project/Art/Shaders/Hecton_ScannerPulseInstanced.shader", "INSTANCING_ON")
        };

        private struct DirectShaderKeywordEntry
        {
            public readonly string ShaderPath;
            public readonly string[] Keywords;

            public DirectShaderKeywordEntry(string shaderPath, params string[] keywords)
            {
                ShaderPath = shaderPath;
                if (keywords == null || keywords.Length == 0)
                {
                    Keywords = EmptyKeywords;
                    return;
                }

                Array.Sort(keywords, StringComparer.Ordinal);
                Keywords = keywords;
            }
        }

        [MenuItem(MenuPath, priority = 191)]
        private static void CompileBootstrapShaderVariantCollection()
        {
            EnsureOutputDirectory();
            ShaderVariantCollection collection = AssetDatabase.LoadAssetAtPath<ShaderVariantCollection>(OutputPath);
            if (collection == null)
            {
                collection = new ShaderVariantCollection();
                AssetDatabase.CreateAsset(collection, OutputPath);
            }
            else
            {
                collection.Clear();
            }

            HashSet<string> materialPaths = new HashSet<string>(256, StringComparer.OrdinalIgnoreCase);
            HashSet<string> shaderPaths = new HashSet<string>(128, StringComparer.OrdinalIgnoreCase);
            HashSet<string> priorityShaderPaths = new HashSet<string>(128, StringComparer.OrdinalIgnoreCase);
            HashSet<string> variantKeys = new HashSet<string>(MaxCompiledVariants, StringComparer.Ordinal);
            CollectAssetDependencies(materialPaths, shaderPaths);
            CollectBootstrapSceneShaderManifest(shaderPaths, priorityShaderPaths);
            List<string> sortedDirectShaderPaths = BuildSortedDirectShaderReferenceRoots(shaderPaths, priorityShaderPaths);
            List<string> sortedMaterialPaths = new List<string>(materialPaths);
            sortedMaterialPaths.Sort(StringComparer.OrdinalIgnoreCase);

            int materialCount = 0;
            int variantCount = 0;
            int priorityLoadedShaderCount = 0;
            int explicitKeywordVariantCount = 0;
            AddDirectShaderVariantsFirst(
                collection,
                sortedDirectShaderPaths,
                variantKeys,
                ref variantCount,
                ref priorityLoadedShaderCount,
                ref explicitKeywordVariantCount);

            for (int materialIndex = 0; materialIndex < sortedMaterialPaths.Count; materialIndex++)
            {
                Material material = AssetDatabase.LoadAssetAtPath<Material>(sortedMaterialPaths[materialIndex]);
                if (material == null || material.shader == null)
                    continue;

                materialCount++;
                string shaderPath = AssetDatabase.GetAssetPath(material.shader);
                if (!string.IsNullOrEmpty(shaderPath))
                    shaderPaths.Add(shaderPath);

                string[] filteredKeywords = BuildFilteredKeywordManifest(material);
                AddVariantsForShader(collection, material.shader, shaderPath, filteredKeywords, variantKeys, ref variantCount);
            }

            List<string> sortedShaderPaths = new List<string>(shaderPaths);
            sortedShaderPaths.Sort(StringComparer.OrdinalIgnoreCase);
            for (int shaderIndex = 0; shaderIndex < sortedShaderPaths.Count; shaderIndex++)
            {
                if (variantCount >= MaxCompiledVariants)
                    break;

                string shaderPath = sortedShaderPaths[shaderIndex];
                if (priorityShaderPaths.Contains(shaderPath))
                    continue;

                Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(shaderPath);
                if (shader == null)
                    continue;

                AddVariantsForShader(collection, shader, shaderPath, EmptyKeywords, variantKeys, ref variantCount);
                explicitKeywordVariantCount += AddExplicitKeywordVariants(collection, shader, shaderPath, variantKeys, ref variantCount);
            }

            EditorUtility.SetDirty(collection);
            AssetDatabase.SaveAssets();
            WriteReport(materialCount, variantCount, materialPaths.Count, priorityLoadedShaderCount, explicitKeywordVariantCount, priorityShaderPaths.Count);
            Debug.Log("[HectonShaderVariantCollectionCompiler1336] variants=" + variantCount + " materials=" + materialCount + " output=" + OutputPath);
        }

        private static void EnsureOutputDirectory()
        {
            string outputDirectory = Path.GetDirectoryName(OutputPath);
            if (!string.IsNullOrEmpty(outputDirectory) && !Directory.Exists(outputDirectory))
            {
                Directory.CreateDirectory(outputDirectory);
                AssetDatabase.Refresh();
            }
        }

        private static void CollectAssetDependencies(HashSet<string> materialPaths, HashSet<string> shaderPaths)
        {
            for (int rootIndex = 0; rootIndex < BootstrapDependencyRoots.Length; rootIndex++)
            {
                string root = BootstrapDependencyRoots[rootIndex];
                if (!AssetDatabase.IsValidFolder(root) && AssetDatabase.LoadMainAssetAtPath(root) == null)
                    continue;

                if (AssetDatabase.IsValidFolder(root))
                {
                    string[] guids = AssetDatabase.FindAssets("t:Material", new[] { root });
                    for (int guidIndex = 0; guidIndex < guids.Length; guidIndex++)
                    {
                        string path = AssetDatabase.GUIDToAssetPath(guids[guidIndex]);
                        if (!string.IsNullOrEmpty(path))
                            materialPaths.Add(path);
                    }

                    string[] shaderGuids = AssetDatabase.FindAssets("t:Shader", new[] { root });
                    for (int guidIndex = 0; guidIndex < shaderGuids.Length; guidIndex++)
                    {
                        string path = AssetDatabase.GUIDToAssetPath(shaderGuids[guidIndex]);
                        if (!string.IsNullOrEmpty(path))
                            shaderPaths.Add(path);
                    }

                    continue;
                }

                string[] dependencies = AssetDatabase.GetDependencies(root, true);
                for (int dependencyIndex = 0; dependencyIndex < dependencies.Length; dependencyIndex++)
                {
                    string dependencyPath = dependencies[dependencyIndex];
                    if (dependencyPath.EndsWith(".mat", StringComparison.OrdinalIgnoreCase))
                    {
                        materialPaths.Add(dependencyPath);
                        continue;
                    }

                    if (dependencyPath.EndsWith(".shader", StringComparison.OrdinalIgnoreCase))
                        shaderPaths.Add(dependencyPath);
                }
            }
        }

        private static void CollectBootstrapSceneShaderManifest(HashSet<string> shaderPaths, HashSet<string> priorityShaderPaths)
        {
            if (shaderPaths == null || priorityShaderPaths == null || !File.Exists(BootstrapScenePath))
                return;

            string[] lines = File.ReadAllLines(BootstrapScenePath);
            bool inShaderManifest = false;
            for (int lineIndex = 0; lineIndex < lines.Length; lineIndex++)
            {
                string line = lines[lineIndex].Trim();
                if (!inShaderManifest)
                {
                    if (string.Equals(line, "shaderWarmupShaders:", StringComparison.Ordinal))
                        inShaderManifest = true;

                    continue;
                }

                if (string.Equals(line, "shaderGraphicsStateCollectionPaths:", StringComparison.Ordinal))
                    break;

                string guid;
                if (!TryExtractShaderReferenceGuid(line, out guid))
                    continue;

                string shaderPath = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(shaderPath) || !shaderPath.EndsWith(".shader", StringComparison.OrdinalIgnoreCase))
                    continue;

                shaderPaths.Add(shaderPath);
                priorityShaderPaths.Add(shaderPath);
            }
        }

        private static bool TryExtractShaderReferenceGuid(string line, out string guid)
        {
            const string ShaderFileId = "fileID: 4800000";
            guid = string.Empty;
            if (string.IsNullOrEmpty(line) || line.IndexOf(ShaderFileId, StringComparison.Ordinal) < 0)
                return false;

            return TryExtractGuid(line, out guid);
        }

        private static bool TryExtractGuid(string line, out string guid)
        {
            const string Marker = "guid:";
            guid = string.Empty;
            if (string.IsNullOrEmpty(line))
                return false;

            int markerIndex = line.IndexOf(Marker, StringComparison.Ordinal);
            if (markerIndex < 0)
                return false;

            int start = markerIndex + Marker.Length;
            while (start < line.Length && line[start] == ' ')
                start++;

            if (start + 32 > line.Length)
                return false;

            for (int i = 0; i < 32; i++)
            {
                if (!IsHex(line[start + i]))
                    return false;
            }

            guid = line.Substring(start, 32);
            return true;
        }

        private static bool IsHex(char value)
        {
            return (value >= '0' && value <= '9') ||
                   (value >= 'a' && value <= 'f') ||
                   (value >= 'A' && value <= 'F');
        }

        private static List<string> BuildSortedDirectShaderReferenceRoots(
            HashSet<string> shaderPaths,
            HashSet<string> priorityShaderPaths)
        {
            for (int shaderIndex = 0; shaderIndex < DirectShaderReferenceRoots.Length; shaderIndex++)
            {
                string shaderPath = DirectShaderReferenceRoots[shaderIndex];
                if (AssetDatabase.LoadAssetAtPath<Shader>(shaderPath) != null)
                {
                    shaderPaths.Add(shaderPath);
                    priorityShaderPaths.Add(shaderPath);
                }
            }

            List<string> directShaderPaths = new List<string>(priorityShaderPaths);
            directShaderPaths.Sort(StringComparer.OrdinalIgnoreCase);
            return directShaderPaths;
        }

        private static void AddDirectShaderVariantsFirst(
            ShaderVariantCollection collection,
            List<string> sortedDirectShaderPaths,
            HashSet<string> variantKeys,
            ref int variantCount,
            ref int priorityLoadedShaderCount,
            ref int explicitKeywordVariantCount)
        {
            for (int shaderIndex = 0; shaderIndex < sortedDirectShaderPaths.Count; shaderIndex++)
            {
                if (variantCount >= MaxCompiledVariants)
                    break;

                string shaderPath = sortedDirectShaderPaths[shaderIndex];
                Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(shaderPath);
                if (shader == null)
                    continue;

                priorityLoadedShaderCount++;
                AddVariantsForShader(collection, shader, shaderPath, EmptyKeywords, variantKeys, ref variantCount);
                explicitKeywordVariantCount += AddExplicitKeywordVariants(collection, shader, shaderPath, variantKeys, ref variantCount);
            }
        }

        private static string[] BuildFilteredKeywordManifest(Material material)
        {
            LocalKeyword[] enabledKeywords = material.enabledKeywords;
            List<string> keywords = new List<string>(enabledKeywords.Length);
            for (int keywordIndex = 0; keywordIndex < enabledKeywords.Length; keywordIndex++)
            {
                string keyword = enabledKeywords[keywordIndex].name;
                if (ShouldKeepKeyword(keyword, material))
                    keywords.Add(keyword);
            }

            keywords.Sort(StringComparer.Ordinal);
            return keywords.ToArray();
        }

        private static bool ShouldKeepKeyword(string keyword, Material material)
        {
            if (string.IsNullOrEmpty(keyword))
                return false;

            if (IsInstancingKeyword(keyword))
                return ShouldKeepInstancingKeyword(material, keyword);

            if (keyword.StartsWith("UNITY_", StringComparison.Ordinal) ||
                keyword.StartsWith("STEREO_", StringComparison.Ordinal) ||
                keyword.StartsWith("LIGHTMAP", StringComparison.Ordinal) ||
                keyword.StartsWith("DIRLIGHTMAP", StringComparison.Ordinal) ||
                keyword.StartsWith("DYNAMICLIGHTMAP", StringComparison.Ordinal) ||
                keyword.StartsWith("SHADOWS_", StringComparison.Ordinal) ||
                keyword.StartsWith("FOG_", StringComparison.Ordinal) ||
                keyword.StartsWith("_ADDITIONAL_LIGHT", StringComparison.Ordinal))
            {
                return false;
            }

            return keyword.StartsWith("_", StringComparison.Ordinal) ||
                   keyword.IndexOf("HECTON", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   keyword.IndexOf("QUALITY", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsInstancingKeyword(string keyword)
        {
            return keyword.StartsWith("INSTANCING_", StringComparison.Ordinal) ||
                   string.Equals(keyword, "DOTS_INSTANCING_ON", StringComparison.Ordinal) ||
                   string.Equals(keyword, "PROCEDURAL_INSTANCING_ON", StringComparison.Ordinal);
        }

        private static bool ShouldKeepInstancingKeyword(Material material, string keyword)
        {
            if (material == null || material.shader == null)
                return false;

            string shaderPath = AssetDatabase.GetAssetPath(material.shader);
            string shaderName = material.shader.name;
            bool authoredInstancing = material.enableInstancing ||
                                      HasInstancingSignal(shaderPath) ||
                                      HasInstancingSignal(shaderName);
            if (!authoredInstancing)
                return false;

            if (string.Equals(keyword, "INSTANCING_ON", StringComparison.Ordinal))
                return true;

            return HasIndirectSignal(shaderPath) || HasIndirectSignal(shaderName);
        }

        private static bool HasInstancingSignal(string value)
        {
            if (string.IsNullOrEmpty(value))
                return false;

            return value.IndexOf("Instanced", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   value.IndexOf("Indirect", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   value.IndexOf("GPUI", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool HasIndirectSignal(string value)
        {
            if (string.IsNullOrEmpty(value))
                return false;

            return value.IndexOf("Indirect", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   value.IndexOf("GPUI", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   value.IndexOf("Procedural", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static void AddVariantsForShader(
            ShaderVariantCollection collection,
            Shader shader,
            string shaderPath,
            string[] keywords,
            HashSet<string> variantKeys,
            ref int variantCount)
        {
            if (collection == null || shader == null || variantCount >= MaxCompiledVariants)
                return;

            for (int passIndex = 0; passIndex < WarmupPassTypes.Length; passIndex++)
            {
                if (variantCount >= MaxCompiledVariants)
                    break;

                PassType passType = WarmupPassTypes[passIndex];
                string variantKey = shaderPath + "|" + passType + "|" + BuildKeywordKey(keywords);
                if (!variantKeys.Add(variantKey))
                    continue;

                ShaderVariantCollection.ShaderVariant variant =
                    new ShaderVariantCollection.ShaderVariant(shader, passType, keywords);
                if (collection.Add(variant))
                    variantCount++;
            }
        }

        private static int AddExplicitKeywordVariants(
            ShaderVariantCollection collection,
            Shader shader,
            string shaderPath,
            HashSet<string> variantKeys,
            ref int variantCount)
        {
            int added = 0;
            for (int entryIndex = 0; entryIndex < DirectShaderKeywordManifest.Length; entryIndex++)
            {
                if (variantCount >= MaxCompiledVariants)
                    break;

                DirectShaderKeywordEntry entry = DirectShaderKeywordManifest[entryIndex];
                if (!string.Equals(shaderPath, entry.ShaderPath, StringComparison.OrdinalIgnoreCase))
                    continue;

                string[] keywords = entry.Keywords ?? EmptyKeywords;
                int before = variantCount;
                AddVariantsForShader(collection, shader, shaderPath, keywords, variantKeys, ref variantCount);
                added += variantCount - before;
            }

            return added;
        }

        private static string BuildKeywordKey(string[] keywords)
        {
            if (keywords == null || keywords.Length == 0)
                return string.Empty;

            StringBuilder builder = new StringBuilder(128);
            for (int i = 0; i < keywords.Length; i++)
            {
                if (i > 0)
                    builder.Append(',');

                builder.Append(keywords[i]);
            }

            return builder.ToString();
        }

        private static void WriteReport(
            int materialCount,
            int variantCount,
            int dependencyMaterialCount,
            int priorityLoadedShaderCount,
            int explicitKeywordVariantCount,
            int priorityShaderCount)
        {
            string absoluteReportPath = Path.GetFullPath(ReportPath);
            Directory.CreateDirectory(Path.GetDirectoryName(absoluteReportPath));
            StringBuilder builder = new StringBuilder(512);
            builder.Append("{\n")
                .Append("  \"agent\": \"1336\",\n")
                .Append("  \"output\": \"").Append(OutputPath).Append("\",\n")
                .Append("  \"dependencyMaterialCount\": ").Append(dependencyMaterialCount).Append(",\n")
                .Append("  \"loadedMaterialCount\": ").Append(materialCount).Append(",\n")
                .Append("  \"priorityShaderCount\": ").Append(priorityShaderCount).Append(",\n")
                .Append("  \"priorityLoadedShaderCount\": ").Append(priorityLoadedShaderCount).Append(",\n")
                .Append("  \"directShaderReferenceRootCount\": ").Append(DirectShaderReferenceRoots.Length).Append(",\n")
                .Append("  \"explicitKeywordVariantCount\": ").Append(explicitKeywordVariantCount).Append(",\n")
                .Append("  \"variantCount\": ").Append(variantCount).Append(",\n")
                .Append("  \"maxCompiledVariants\": ").Append(MaxCompiledVariants).Append(",\n")
                .Append("  \"deterministicMaterialSort\": true,\n")
                .Append("  \"directShaderReferencesIncluded\": true,\n")
                .Append("  \"sceneShaderManifestIncluded\": true,\n")
                .Append("  \"directShaderPriorityWarmup\": true,\n")
                .Append("  \"runtimePassBudgetOnly\": true,\n")
                .Append("  \"instancingPolicy\": \"contextual: authored instanced/indirect/GPUI shaders only\",\n")
                .Append("  \"unity6PsoTraceRequired\": true,\n")
                .Append("  \"futureUnity65CacheMissCollectionPending\": true,\n")
                .Append("  \"filter\": \"first-20-minute bootstrap/world/player/tool/vfx roots; Unity stereo/lightmap/fog/additional-light spam rejected; direct shader refs and contextual instancing retained\"\n")
                .Append("}\n");
            File.WriteAllText(absoluteReportPath, builder.ToString(), Encoding.UTF8);
        }
    }
}
#endif
