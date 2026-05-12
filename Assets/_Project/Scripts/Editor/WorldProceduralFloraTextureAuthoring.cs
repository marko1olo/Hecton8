using System.IO;
using System.Text;
using Unity.Collections;
using UnityEditor;
using UnityEngine;

namespace Hecton8.EditorTools
{
    /// <summary>
    /// Owns editor-only procedural texture generation for flora starter and baked-final materials.
    /// </summary>
    public static class WorldProceduralFloraTextureAuthoring
    {
        private const string TextureRoot = "Assets/_Project/Art/Textures/WorldProceduralFlora";
        private const string ImportedTextureRoot = TextureRoot + "/Imported";
        private const string SeaGrassAtlasAssetPath = TextureRoot + "/TX_SeaGrass_Albedo_BC7_Atlas.png";
        private const string TextureLibraryReportFileName = "PROCEDURAL_FLORA_TEXTURE_LIBRARY_REPORT.md";
        private const string TextureRequestPacketFileName = "PROCEDURAL_FLORA_TEXTURE_REQUEST_PACKET.md";
        private const int SeaGrassAtlasSize = 1024;
        private const string FamilyKelpTall = "family.kelp.tall";
        private const string FamilyKelpPatchDense = "family.kelp.patch.dense";
        private const string FamilyKelpCanopy = "family.kelp.canopy";
        private const string FamilyKelpAbyssal = "family.kelp.abyssal";
        private const string FamilyCoralLow = "family.coral.low";
        private const string FamilyCoralBranching = "family.coral.branching";
        private const string FamilyCoralMassive = "family.coral.massive";
        private const string FamilyCoralPlate = "family.coral.plate";
        private const string FamilyCoralBrittle = "family.coral.brittle";
        private static readonly string[] SupportedFamilyIds =
        {
            FamilyKelpTall,
            FamilyKelpPatchDense,
            FamilyKelpCanopy,
            FamilyKelpAbyssal,
            FamilyCoralLow,
            FamilyCoralBranching,
            FamilyCoralMassive,
            FamilyCoralPlate,
            FamilyCoralBrittle
        };
        private static readonly string[] SeaGrassFamilyIds =
        {
            FamilyKelpTall,
            FamilyKelpPatchDense,
            FamilyKelpCanopy,
            FamilyKelpAbyssal
        };
        private static readonly string[] RequiredMapTokens = { "albedo", "detail", "normal", "mask" };
        private const double TextureMemoryRedThresholdMb = 900.0;

        [MenuItem("Hecton/Authoring/Generate Procedural Flora Textures", priority = 175)]
        public static void Apply()
        {
            EnsureFolder("Assets/_Project/Art");
            EnsureFolder("Assets/_Project/Art/Textures");
            EnsureFolder(TextureRoot);

            int touchedTextures = 0;
            touchedTextures += CreateOrUpdateBaseTexture(TextureRoot + "/TX_KelpTall_Base.asset", new Color(0.18f, 0.44f, 0.21f), new Color(0.22f, 0.58f, 0.28f), new Color(0.36f, 0.72f, 0.42f), 0.18f) ? 1 : 0;
            touchedTextures += CreateOrUpdateBaseTexture(TextureRoot + "/TX_KelpPatch_Base.asset", new Color(0.12f, 0.34f, 0.18f), new Color(0.18f, 0.46f, 0.24f), new Color(0.28f, 0.60f, 0.32f), 0.14f) ? 1 : 0;
            touchedTextures += CreateOrUpdateBaseTexture(TextureRoot + "/TX_KelpCanopy_Base.asset", new Color(0.22f, 0.50f, 0.24f), new Color(0.28f, 0.66f, 0.32f), new Color(0.44f, 0.82f, 0.48f), 0.24f) ? 1 : 0;
            touchedTextures += CreateOrUpdateBaseTexture(TextureRoot + "/TX_KelpAbyssal_Base.asset", new Color(0.04f, 0.07f, 0.08f), new Color(0.07f, 0.12f, 0.14f), new Color(0.16f, 0.24f, 0.28f), 0.12f) ? 1 : 0;
            touchedTextures += CreateOrUpdateDetailTexture(TextureRoot + "/TX_KelpTall_Detail.asset", 11) ? 1 : 0;
            touchedTextures += CreateOrUpdateDetailTexture(TextureRoot + "/TX_KelpPatch_Detail.asset", 23) ? 1 : 0;
            touchedTextures += CreateOrUpdateDetailTexture(TextureRoot + "/TX_KelpCanopy_Detail.asset", 37) ? 1 : 0;
            touchedTextures += CreateOrUpdateDetailTexture(TextureRoot + "/TX_KelpAbyssal_Detail.asset", 91) ? 1 : 0;
            touchedTextures += CreateOrUpdateNormalTexture(TextureRoot + "/TX_KelpTall_Normal.asset", 11, 0.72f) ? 1 : 0;
            touchedTextures += CreateOrUpdateNormalTexture(TextureRoot + "/TX_KelpPatch_Normal.asset", 23, 0.58f) ? 1 : 0;
            touchedTextures += CreateOrUpdateNormalTexture(TextureRoot + "/TX_KelpCanopy_Normal.asset", 37, 0.86f) ? 1 : 0;
            touchedTextures += CreateOrUpdateNormalTexture(TextureRoot + "/TX_KelpAbyssal_Normal.asset", 91, 0.94f) ? 1 : 0;
            touchedTextures += CreateOrUpdateMaskTexture(TextureRoot + "/TX_KelpTall_Mask.asset", 11, 0.62f, 0.94f) ? 1 : 0;
            touchedTextures += CreateOrUpdateMaskTexture(TextureRoot + "/TX_KelpPatch_Mask.asset", 23, 0.54f, 0.88f) ? 1 : 0;
            touchedTextures += CreateOrUpdateMaskTexture(TextureRoot + "/TX_KelpCanopy_Mask.asset", 37, 0.68f, 0.98f) ? 1 : 0;
            touchedTextures += CreateOrUpdateMaskTexture(TextureRoot + "/TX_KelpAbyssal_Mask.asset", 91, 0.72f, 0.98f) ? 1 : 0;
            touchedTextures += CreateOrUpdateBaseTexture(TextureRoot + "/TX_CoralLow_Base.asset", new Color(0.48f, 0.28f, 0.26f), new Color(0.70f, 0.42f, 0.34f), new Color(0.88f, 0.64f, 0.48f), 0.12f) ? 1 : 0;
            touchedTextures += CreateOrUpdateBaseTexture(TextureRoot + "/TX_CoralBranching_Base.asset", new Color(0.42f, 0.24f, 0.30f), new Color(0.68f, 0.40f, 0.48f), new Color(0.90f, 0.72f, 0.52f), 0.16f) ? 1 : 0;
            touchedTextures += CreateOrUpdateBaseTexture(TextureRoot + "/TX_CoralMassive_Base.asset", new Color(0.54f, 0.30f, 0.22f), new Color(0.78f, 0.48f, 0.34f), new Color(0.94f, 0.72f, 0.56f), 0.10f) ? 1 : 0;
            touchedTextures += CreateOrUpdateBaseTexture(TextureRoot + "/TX_CoralPlate_Base.asset", new Color(0.30f, 0.34f, 0.40f), new Color(0.50f, 0.54f, 0.62f), new Color(0.82f, 0.78f, 0.62f), 0.14f) ? 1 : 0;
            touchedTextures += CreateOrUpdateBaseTexture(TextureRoot + "/TX_CoralBrittle_Base.asset", new Color(0.08f, 0.10f, 0.12f), new Color(0.18f, 0.24f, 0.26f), new Color(0.46f, 0.58f, 0.60f), 0.14f) ? 1 : 0;
            touchedTextures += CreateOrUpdateDetailTexture(TextureRoot + "/TX_CoralLow_Detail.asset", 41) ? 1 : 0;
            touchedTextures += CreateOrUpdateDetailTexture(TextureRoot + "/TX_CoralBranching_Detail.asset", 53) ? 1 : 0;
            touchedTextures += CreateOrUpdateDetailTexture(TextureRoot + "/TX_CoralMassive_Detail.asset", 67) ? 1 : 0;
            touchedTextures += CreateOrUpdateDetailTexture(TextureRoot + "/TX_CoralPlate_Detail.asset", 79) ? 1 : 0;
            touchedTextures += CreateOrUpdateDetailTexture(TextureRoot + "/TX_CoralBrittle_Detail.asset", 97) ? 1 : 0;
            touchedTextures += CreateOrUpdateCoralNormalTexture(TextureRoot + "/TX_CoralLow_Normal.asset", 41, 0.62f) ? 1 : 0;
            touchedTextures += CreateOrUpdateCoralNormalTexture(TextureRoot + "/TX_CoralBranching_Normal.asset", 53, 0.84f) ? 1 : 0;
            touchedTextures += CreateOrUpdateCoralNormalTexture(TextureRoot + "/TX_CoralMassive_Normal.asset", 67, 0.70f) ? 1 : 0;
            touchedTextures += CreateOrUpdateCoralNormalTexture(TextureRoot + "/TX_CoralPlate_Normal.asset", 79, 0.58f) ? 1 : 0;
            touchedTextures += CreateOrUpdateCoralNormalTexture(TextureRoot + "/TX_CoralBrittle_Normal.asset", 97, 0.92f) ? 1 : 0;
            touchedTextures += CreateOrUpdateCoralMaskTexture(TextureRoot + "/TX_CoralLow_Mask.asset", 41, 0.44f, 0.78f) ? 1 : 0;
            touchedTextures += CreateOrUpdateCoralMaskTexture(TextureRoot + "/TX_CoralBranching_Mask.asset", 53, 0.36f, 0.86f) ? 1 : 0;
            touchedTextures += CreateOrUpdateCoralMaskTexture(TextureRoot + "/TX_CoralMassive_Mask.asset", 67, 0.52f, 0.74f) ? 1 : 0;
            touchedTextures += CreateOrUpdateCoralMaskTexture(TextureRoot + "/TX_CoralPlate_Mask.asset", 79, 0.34f, 0.92f) ? 1 : 0;
            touchedTextures += CreateOrUpdateCoralMaskTexture(TextureRoot + "/TX_CoralBrittle_Mask.asset", 97, 0.68f, 0.96f) ? 1 : 0;

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[WorldProceduralFloraTextureAuthoring] Applied flora textures. TouchedTextures={touchedTextures}.");
        }

        [MenuItem("Hecton/Validation/Fix Imported Flora Texture Import Settings", priority = 272)]
        public static void FixImportedTextureImportSettings()
        {
            int updated = 0;
            int skipped = 0;
            string[] textureGuids = AssetDatabase.FindAssets("t:Texture2D", new[] { ImportedTextureRoot });

            for (int i = 0; i < textureGuids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(textureGuids[i]);
                if (string.IsNullOrWhiteSpace(path))
                {
                    skipped++;
                    continue;
                }

                string fileName = System.IO.Path.GetFileNameWithoutExtension(path);
                int separatorIndex = fileName.IndexOf("___", System.StringComparison.Ordinal);
                if (separatorIndex <= 0)
                {
                    skipped++;
                    continue;
                }

                string mapToken = fileName.Substring(0, separatorIndex);
                if (!IsSupportedMapToken(mapToken))
                {
                    skipped++;
                    continue;
                }

                TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null)
                {
                    skipped++;
                    continue;
                }

                string familyId = ResolveFamilyIdFromImportedPath(path);
                int resolvedMaxTextureSize = ResolveImportedMaxTextureSize(familyId, mapToken);
                importer.wrapMode = TextureWrapMode.Repeat;
                importer.mipmapEnabled = true;
                importer.isReadable = false;
                importer.textureCompression = TextureImporterCompression.Compressed;
                importer.crunchedCompression = false;

                switch (mapToken)
                {
                    case "albedo":
                        importer.textureType = TextureImporterType.Default;
                        importer.sRGBTexture = true;
                        importer.maxTextureSize = resolvedMaxTextureSize;
                        break;
                    case "detail":
                        importer.textureType = TextureImporterType.Default;
                        importer.sRGBTexture = false;
                        importer.maxTextureSize = resolvedMaxTextureSize;
                        break;
                    case "normal":
                        importer.textureType = TextureImporterType.NormalMap;
                        importer.sRGBTexture = false;
                        importer.maxTextureSize = resolvedMaxTextureSize;
                        break;
                    case "mask":
                        importer.textureType = TextureImporterType.Default;
                        importer.sRGBTexture = false;
                        importer.maxTextureSize = resolvedMaxTextureSize;
                        break;
                }

                TextureImporterPlatformSettings standalone = importer.GetPlatformTextureSettings("Standalone");
                standalone.overridden = true;
                standalone.format = string.Equals(mapToken, "normal", System.StringComparison.Ordinal)
                    ? TextureImporterFormat.BC5
                    : TextureImporterFormat.BC7;
                standalone.maxTextureSize = resolvedMaxTextureSize;
                standalone.textureCompression = TextureImporterCompression.Compressed;
                standalone.crunchedCompression = false;
                importer.SetPlatformTextureSettings(standalone);

                importer.SaveAndReimport();
                updated++;
            }

            Debug.Log($"[WorldProceduralFloraTextureAuthoring] Fixed imported texture import settings. Updated={updated}, Skipped={skipped}.");
        }

        [MenuItem("Hecton/Validation/Build Sea-Grass 1024 BC7 Atlas", priority = 274)]
        public static void BuildSeaGrassBc7Atlas()
        {
            EnsureFolder("Assets/_Project/Art");
            EnsureFolder("Assets/_Project/Art/Textures");
            EnsureFolder(TextureRoot);

            string[] sourcePaths = new string[SeaGrassFamilyIds.Length];
            bool[] restoreReadability = new bool[SeaGrassFamilyIds.Length];
            Texture2D[] sourceTextures = new Texture2D[SeaGrassFamilyIds.Length];

            for (int i = 0; i < SeaGrassFamilyIds.Length; i++)
            {
                sourcePaths[i] = ResolveBaseTexturePath(SeaGrassFamilyIds[i]);
                sourceTextures[i] = AssetDatabase.LoadAssetAtPath<Texture2D>(sourcePaths[i]);
                if (sourceTextures[i] == null)
                {
                    Debug.LogError($"[WorldProceduralFloraTextureAuthoring] Missing sea-grass atlas source '{sourcePaths[i]}'.");
                    return;
                }

                if (!TrySetTextureReadable(sourcePaths[i], true, out restoreReadability[i]))
                    return;

                sourceTextures[i] = AssetDatabase.LoadAssetAtPath<Texture2D>(sourcePaths[i]);
            }

            Texture2D atlas = new Texture2D(SeaGrassAtlasSize, SeaGrassAtlasSize, TextureFormat.RGBA32, true, false)
            {
                name = "TX_SeaGrass_Albedo_BC7_Atlas"
            };

            Rect[] rects = atlas.PackTextures(sourceTextures, 0, SeaGrassAtlasSize, makeNoLongerReadable: false);
            if (rects == null || rects.Length != sourceTextures.Length)
            {
                UnityEngine.Object.DestroyImmediate(atlas);
                RestoreTextureReadability(sourcePaths, restoreReadability);
                Debug.LogError("[WorldProceduralFloraTextureAuthoring] Sea-grass atlas pack failed.");
                return;
            }

            File.WriteAllBytes(SeaGrassAtlasAssetPath, atlas.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(atlas);
            RestoreTextureReadability(sourcePaths, restoreReadability);
            AssetDatabase.ImportAsset(SeaGrassAtlasAssetPath, ImportAssetOptions.ForceUpdate);
            ConfigureSeaGrassAtlasImporter();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[WorldProceduralFloraTextureAuthoring] Built sea-grass atlas '{SeaGrassAtlasAssetPath}' at {SeaGrassAtlasSize}x{SeaGrassAtlasSize} BC7.");
        }

        [MenuItem("Hecton/Validation/Scaffold Missing Flora Texture Import Folders", priority = 269)]
        public static void ScaffoldMissingImportedTextureFolders()
        {
            EnsureFolder("Assets/_Project/Art");
            EnsureFolder("Assets/_Project/Art/Textures");
            EnsureFolder(TextureRoot);
            EnsureFolder(ImportedTextureRoot);

            int createdFolders = 0;
            int missingFamilies = 0;

            for (int familyIndex = 0; familyIndex < SupportedFamilyIds.Length; familyIndex++)
            {
                string familyId = SupportedFamilyIds[familyIndex];
                ImportedFamilyStatus status = EvaluateImportedFamilyStatus(familyId);
                if (status.CoverageComplete)
                    continue;

                missingFamilies++;
                string folderPath = ImportedTextureRoot + "/" + familyId;
                if (AssetDatabase.IsValidFolder(folderPath))
                    continue;

                EnsureFolder(folderPath);
                createdFolders++;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[WorldProceduralFloraTextureAuthoring] Scaffolded missing imported flora texture folders. CreatedFolders={createdFolders}, MissingFamilies={missingFamilies}.");
        }

        [MenuItem("Hecton/Validation/Generate Missing Flora Texture Request Packet", priority = 270)]
        public static void GenerateMissingTextureRequestPacket()
        {
            StringBuilder markdown = new StringBuilder(8192);
            markdown.AppendLine("# Procedural Flora Texture Request Packet");
            markdown.AppendLine();
            markdown.AppendLine($"Generated: {System.DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            markdown.AppendLine();
            markdown.AppendLine("- Contract source: `Docs/PROCEDURAL_ASSET_PIPELINE.md`");
            markdown.AppendLine("- Prompt rule: output prompts only. Do not generate placeholder production textures in code.");
            markdown.AppendLine("- Imported target root: `" + ImportedTextureRoot + "`");
            markdown.AppendLine("- Current gap: missing imported sets block final photoreal texture proof for the families listed below.");

            int missingFamilyCount = 0;
            for (int familyIndex = 0; familyIndex < SupportedFamilyIds.Length; familyIndex++)
            {
                string familyId = SupportedFamilyIds[familyIndex];
                ImportedFamilyStatus status = EvaluateImportedFamilyStatus(familyId);
                if (status.CoverageComplete)
                    continue;

                TextureRequestFamilySpec requestSpec = ResolveTextureRequestFamilySpec(familyId);
                if (!requestSpec.IsValid)
                    continue;

                missingFamilyCount++;
                markdown.AppendLine();
                markdown.Append("## ").Append(familyId).AppendLine();
                markdown.AppendLine();
                markdown.Append("- Current imported coverage: `").Append(status.PresentMapCount).Append("/").Append(RequiredMapTokens.Length).AppendLine("`");
                markdown.Append("- Current report note: `").Append(status.PrimaryNote).AppendLine("`");
                markdown.Append("- Biome token: `").Append(requestSpec.BiomeToken).AppendLine("`");
                markdown.Append("- Resolution target: `2048x2048`").AppendLine();
                markdown.Append("- Import folder: `").Append(ImportedTextureRoot).Append('/').Append(familyId).AppendLine("`");
                markdown.AppendLine();
                markdown.AppendLine("### Missing Map Prompts");
                markdown.AppendLine();

                for (int mapIndex = 0; mapIndex < RequiredMapTokens.Length; mapIndex++)
                {
                    string mapToken = RequiredMapTokens[mapIndex];
                    string importedPath = GetImportedTexturePath(familyId, mapToken);
                    Texture2D importedTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(importedPath);
                    if (importedTexture != null)
                        continue;

                    markdown.Append("- `").Append(mapToken).Append("` -> `").Append(importedPath).AppendLine("`");
                    markdown.Append("  Prompt: `").Append(BuildMasterPrompt(requestSpec, mapToken)).AppendLine("`");
                }
            }

            markdown.AppendLine();
            markdown.AppendLine("## Unity Import Settings");
            markdown.AppendLine();
            markdown.AppendLine("- Texture Type: `Default` for `albedo` and `mask`");
            markdown.AppendLine("- Texture Type: `Normal Map` for `normal` and `detail`");
            markdown.AppendLine("- Wrap Mode: `Repeat`");
            markdown.AppendLine("- Generate Mip Maps: `On`");
            markdown.AppendLine("- Compression: `BC7` for color, `BC5/DXT5nm` for normals");
            markdown.AppendLine("- Max Size: `2048`");
            markdown.AppendLine("- sRGB: `On` for `albedo`, `Off` for `detail`, `normal`, `mask`");
            markdown.AppendLine("- Read/Write: `Off`");
            markdown.AppendLine();
            markdown.AppendLine("## Validation After Import");
            markdown.AppendLine();
            markdown.AppendLine("- Apply imported maps to the matching family material.");
            markdown.AppendLine("- Optional: run `Hecton/Validation/Scaffold Missing Flora Texture Import Folders` before copying files.");
            markdown.AppendLine("- Run `Hecton/Validation/Fix Imported Flora Texture Import Settings`.");
            markdown.AppendLine("- Run `Hecton/Validation/Report Imported Flora Texture Library`.");
            markdown.AppendLine("- Run `Hecton/Validation/Reconcile Imported Flora Coverage` for the full post-import chain.");
            markdown.AppendLine("- Set tiling to `4x4` in Unity and inspect for visible seams.");
            markdown.AppendLine("- If seams remain, regenerate with: `FIX SEAMS: previous output had visible tile boundaries at 2x repetition, ensure exact edge matching on all four sides`.");

            string reportPath = Path.Combine(Directory.GetCurrentDirectory(), TextureRequestPacketFileName);
            File.WriteAllText(reportPath, markdown.ToString(), Encoding.UTF8);
            AssetDatabase.Refresh();
            Debug.Log($"[WorldProceduralFloraTextureAuthoring] Wrote missing flora texture request packet to '{reportPath}'. MissingFamilies={missingFamilyCount}.");
        }

        [MenuItem("Hecton/Validation/Report Imported Flora Texture Library", priority = 271)]
        public static void ReportImportedTextureLibrary()
        {
            StringBuilder markdown = new StringBuilder(4096);
            markdown.AppendLine("# Procedural Flora Texture Library Report");
            markdown.AppendLine();
            markdown.AppendLine($"Generated: {System.DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            markdown.AppendLine();
            markdown.AppendLine("| Family | Coverage | Contract | Est. GPU MB | Notes |");
            markdown.AppendLine("| --- | --- | --- | --- | --- |");

            int completeFamilies = 0;
            int contractCleanFamilies = 0;
            int missingFamilies = 0;
            double totalEstimatedGpuMb = 0.0;
            double cleanEstimatedGpuMb = 0.0;

            for (int familyIndex = 0; familyIndex < SupportedFamilyIds.Length; familyIndex++)
            {
                string familyId = SupportedFamilyIds[familyIndex];
                ImportedFamilyStatus status = EvaluateImportedFamilyStatus(familyId);
                if (status.CoverageComplete)
                    completeFamilies++;
                else
                    missingFamilies++;

                if (status.ContractClean)
                {
                    contractCleanFamilies++;
                    cleanEstimatedGpuMb += status.EstimatedGpuMb;
                }

                totalEstimatedGpuMb += status.EstimatedGpuMb;

                markdown.Append("| ");
                markdown.Append(familyId);
                markdown.Append(" | ");
                markdown.Append(status.PresentMapCount);
                markdown.Append("/");
                markdown.Append(RequiredMapTokens.Length);
                markdown.Append(" | ");
                markdown.Append(ResolveContractLabel(status));
                markdown.Append(" | ");
                markdown.Append(status.EstimatedGpuMb.ToString("0.0"));
                markdown.Append(" | ");
                markdown.Append(status.PrimaryNote);
                markdown.AppendLine(" |");
            }

            markdown.AppendLine();
            markdown.AppendLine("## Summary");
            markdown.AppendLine();
            markdown.AppendLine($"- Family coverage complete: `{completeFamilies}/{SupportedFamilyIds.Length}`");
            markdown.AppendLine($"- Family contract clean: `{contractCleanFamilies}/{SupportedFamilyIds.Length}`");
            markdown.AppendLine($"- Families still missing imported sets: `{missingFamilies}`");
            markdown.AppendLine($"- Estimated imported flora GPU memory: `{totalEstimatedGpuMb:0.0} MB`");
            markdown.AppendLine($"- Estimated clean-contract flora GPU memory: `{cleanEstimatedGpuMb:0.0} MB`");
            markdown.AppendLine($"- Texture red threshold reference: `{TextureMemoryRedThresholdMb:0} MB`");
            markdown.AppendLine($"- Imported root: `{ImportedTextureRoot}`");
            markdown.AppendLine($"- Request packet: `{TextureRequestPacketFileName}`");
            Texture2D seaGrassAtlas = AssetDatabase.LoadAssetAtPath<Texture2D>(SeaGrassAtlasAssetPath);
            markdown.AppendLine($"- Sea-grass atlas: `{SeaGrassAtlasAssetPath}` => `{(seaGrassAtlas != null ? "present" : "missing; run Hecton/Validation/Build Sea-Grass 1024 BC7 Atlas")}`");
            markdown.AppendLine($"- Sea-grass import contract: kelp-family maps capped to `{SeaGrassAtlasSize}`; non-normal maps use `BC7`; normal maps retain `BC5`.");

            string reportPath = Path.Combine(Directory.GetCurrentDirectory(), TextureLibraryReportFileName);
            File.WriteAllText(reportPath, markdown.ToString(), Encoding.UTF8);
            AssetDatabase.Refresh();
            Debug.Log($"[WorldProceduralFloraTextureAuthoring] Wrote imported flora texture report to '{reportPath}'.");
        }

        [MenuItem("Hecton/Validation/Reconcile Imported Flora Coverage", priority = 273)]
        public static void ReconcileImportedCoverage()
        {
            ScaffoldMissingImportedTextureFolders();
            FixImportedTextureImportSettings();
            ReportImportedTextureLibrary();
            WorldProceduralFloraMaterialAuthoring.Apply();
            WorldProceduralFloraFinalVariantValidator.Validate();
            WorldProceduralFloraFinalStatusReport.GenerateReport();

            int completeFamilies = 0;
            int missingFamilies = 0;
            int contractCleanFamilies = 0;

            for (int familyIndex = 0; familyIndex < SupportedFamilyIds.Length; familyIndex++)
            {
                ImportedFamilyStatus status = EvaluateImportedFamilyStatus(SupportedFamilyIds[familyIndex]);
                if (status.CoverageComplete)
                    completeFamilies++;
                else
                    missingFamilies++;

                if (status.ContractClean)
                    contractCleanFamilies++;
            }

            Debug.Log($"[WorldProceduralFloraTextureAuthoring] Reconciled imported flora coverage. CoverageComplete={completeFamilies}/{SupportedFamilyIds.Length}, ContractClean={contractCleanFamilies}/{SupportedFamilyIds.Length}, MissingFamilies={missingFamilies}.");
        }

        public static Texture2D LoadKelpBaseTexture(string familyId)
        {
            return AssetDatabase.LoadAssetAtPath<Texture2D>(ResolveBaseTexturePath(familyId));
        }

        public static Texture2D LoadKelpDetailTexture(string familyId)
        {
            return AssetDatabase.LoadAssetAtPath<Texture2D>(ResolveDetailTexturePath(familyId));
        }

        public static Texture2D LoadKelpNormalTexture(string familyId)
        {
            return AssetDatabase.LoadAssetAtPath<Texture2D>(ResolveNormalTexturePath(familyId));
        }

        public static Texture2D LoadKelpMaskTexture(string familyId)
        {
            return AssetDatabase.LoadAssetAtPath<Texture2D>(ResolveMaskTexturePath(familyId));
        }

        public static Texture2D LoadCoralBaseTexture(string familyId)
        {
            return AssetDatabase.LoadAssetAtPath<Texture2D>(ResolveCoralBaseTexturePath(familyId));
        }

        public static Texture2D LoadCoralDetailTexture(string familyId)
        {
            return AssetDatabase.LoadAssetAtPath<Texture2D>(ResolveCoralDetailTexturePath(familyId));
        }

        public static Texture2D LoadCoralNormalTexture(string familyId)
        {
            return AssetDatabase.LoadAssetAtPath<Texture2D>(ResolveCoralNormalTexturePath(familyId));
        }

        public static Texture2D LoadCoralMaskTexture(string familyId)
        {
            return AssetDatabase.LoadAssetAtPath<Texture2D>(ResolveCoralMaskTexturePath(familyId));
        }

        internal static bool IsGeneratedProceduralTexture(Texture texture)
        {
            if (texture == null)
                return false;

            string assetPath = AssetDatabase.GetAssetPath(texture);
            if (string.IsNullOrWhiteSpace(assetPath))
                return false;

            string normalizedAssetPath = assetPath.Replace('\\', '/');
            string normalizedTextureRoot = TextureRoot.Replace('\\', '/');
            return normalizedAssetPath.StartsWith(normalizedTextureRoot + "/", System.StringComparison.OrdinalIgnoreCase)
                && normalizedAssetPath.EndsWith(".asset", System.StringComparison.OrdinalIgnoreCase);
        }

        internal static bool IsImportedFloraTexture(Texture texture)
        {
            if (texture == null)
                return false;

            string assetPath = AssetDatabase.GetAssetPath(texture);
            if (string.IsNullOrWhiteSpace(assetPath))
                return false;

            string normalizedAssetPath = assetPath.Replace('\\', '/');
            string normalizedImportedRoot = ImportedTextureRoot.Replace('\\', '/');
            return normalizedAssetPath.StartsWith(normalizedImportedRoot + "/", System.StringComparison.OrdinalIgnoreCase)
                && normalizedAssetPath.EndsWith(".png", System.StringComparison.OrdinalIgnoreCase);
        }

        internal static bool IsManagedFloraTexture(Texture texture)
        {
            return IsImportedFloraTexture(texture) || IsGeneratedProceduralTexture(texture);
        }

        internal static bool TryGetImportedTextureContractFailure(Texture texture, string familyId, string mapToken, out string failureLabel)
        {
            failureLabel = string.Empty;
            if (!IsImportedFloraTexture(texture))
                return false;

            string assetPath = AssetDatabase.GetAssetPath(texture);
            string normalizedAssetPath = assetPath.Replace('\\', '/');
            string expectedSuffix = "/" + familyId + "/" + mapToken + "___" + familyId + ".png";
            if (!normalizedAssetPath.EndsWith(expectedSuffix, System.StringComparison.OrdinalIgnoreCase))
            {
                failureLabel = "path-naming-mismatch";
                return true;
            }

            TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null)
            {
                failureLabel = "missing-texture-importer";
                return true;
            }

            int expectedMaxTextureSize = ResolveImportedMaxTextureSize(familyId, mapToken);

            if (importer.wrapMode != TextureWrapMode.Repeat)
            {
                failureLabel = "wrap-not-repeat";
                return true;
            }

            if (!importer.mipmapEnabled)
            {
                failureLabel = "mipmaps-off";
                return true;
            }

            if (importer.isReadable)
            {
                failureLabel = "readwrite-on";
                return true;
            }

            switch (mapToken)
            {
                case "albedo":
                    if (importer.textureType != TextureImporterType.Default)
                    {
                        failureLabel = "albedo-type-not-default";
                        return true;
                    }

                    if (!importer.sRGBTexture)
                    {
                        failureLabel = "albedo-srgb-off";
                        return true;
                    }

                    if (importer.maxTextureSize > expectedMaxTextureSize)
                    {
                        failureLabel = "albedo-maxsize-too-high";
                        return true;
                    }

                    return false;

                case "detail":
                    if (importer.textureType != TextureImporterType.Default)
                    {
                        failureLabel = "detail-type-not-default";
                        return true;
                    }

                    if (importer.sRGBTexture)
                    {
                        failureLabel = "detail-srgb-on";
                        return true;
                    }

                    if (importer.maxTextureSize > expectedMaxTextureSize)
                    {
                        failureLabel = "detail-maxsize-too-high";
                        return true;
                    }

                    return false;

                case "normal":
                    if (importer.textureType != TextureImporterType.NormalMap)
                    {
                        failureLabel = "normal-type-not-normalmap";
                        return true;
                    }

                    if (importer.sRGBTexture)
                    {
                        failureLabel = "normal-srgb-on";
                        return true;
                    }

                    if (importer.maxTextureSize > expectedMaxTextureSize)
                    {
                        failureLabel = "normal-maxsize-too-high";
                        return true;
                    }

                    return false;

                case "mask":
                    if (importer.textureType != TextureImporterType.Default)
                    {
                        failureLabel = "mask-type-not-default";
                        return true;
                    }

                    if (importer.sRGBTexture)
                    {
                        failureLabel = "mask-srgb-on";
                        return true;
                    }

                    if (importer.maxTextureSize > expectedMaxTextureSize)
                    {
                        failureLabel = "mask-maxsize-too-high";
                        return true;
                    }

                    return false;
            }

            failureLabel = "unknown-map-token";
            return true;
        }

        internal static bool TryGetUnexpectedTextureSourceFailure(Texture texture, string familyId, string mapToken, out string failureLabel)
        {
            failureLabel = string.Empty;
            if (texture == null)
                return false;

            if (IsManagedFloraTexture(texture))
                return false;

            string assetPath = AssetDatabase.GetAssetPath(texture);
            if (string.IsNullOrWhiteSpace(assetPath))
            {
                failureLabel = mapToken + ":non-asset-texture";
                return true;
            }

            string normalizedAssetPath = assetPath.Replace('\\', '/');
            failureLabel = mapToken + ":outside-imported-root:" + normalizedAssetPath;
            return true;
        }

        internal static bool TryGetTextureStackSourceFailure(Texture baseTexture, Texture detailTexture, Texture normalTexture, Texture maskTexture, out string failureLabel)
        {
            int importedCount = 0;
            int generatedCount = 0;
            int managedCount = 0;
            int assignedCount = 0;

            CountTextureSource(baseTexture, ref importedCount, ref generatedCount, ref managedCount, ref assignedCount);
            CountTextureSource(detailTexture, ref importedCount, ref generatedCount, ref managedCount, ref assignedCount);
            CountTextureSource(normalTexture, ref importedCount, ref generatedCount, ref managedCount, ref assignedCount);
            CountTextureSource(maskTexture, ref importedCount, ref generatedCount, ref managedCount, ref assignedCount);

            if (importedCount > 0 && generatedCount > 0)
            {
                failureLabel = $"mixed-imported-generated-stack:{importedCount}i/{generatedCount}g";
                return true;
            }

            if (importedCount > 0 && managedCount != assignedCount)
            {
                failureLabel = $"mixed-imported-external-stack:{importedCount}i/{assignedCount - managedCount}x";
                return true;
            }

            failureLabel = string.Empty;
            return false;
        }

        private static bool IsSupportedMapToken(string mapToken)
        {
            return string.Equals(mapToken, "albedo", System.StringComparison.Ordinal)
                || string.Equals(mapToken, "detail", System.StringComparison.Ordinal)
                || string.Equals(mapToken, "normal", System.StringComparison.Ordinal)
                || string.Equals(mapToken, "mask", System.StringComparison.Ordinal);
        }

        private static bool IsSeaGrassFamily(string familyId)
        {
            for (int i = 0; i < SeaGrassFamilyIds.Length; i++)
            {
                if (string.Equals(familyId, SeaGrassFamilyIds[i], System.StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        private static int ResolveImportedMaxTextureSize(string familyId, string mapToken)
        {
            if (IsSeaGrassFamily(familyId))
                return SeaGrassAtlasSize;

            return string.Equals(mapToken, "detail", System.StringComparison.Ordinal) ? 1024 : 2048;
        }

        private static string ResolveFamilyIdFromImportedPath(string assetPath)
        {
            if (string.IsNullOrWhiteSpace(assetPath))
                return string.Empty;

            string normalizedPath = assetPath.Replace('\\', '/');
            int fileSlash = normalizedPath.LastIndexOf('/');
            if (fileSlash <= 0)
                return string.Empty;

            int folderSlash = normalizedPath.LastIndexOf('/', fileSlash - 1);
            return folderSlash >= 0
                ? normalizedPath.Substring(folderSlash + 1, fileSlash - folderSlash - 1)
                : string.Empty;
        }

        private static bool TrySetTextureReadable(string assetPath, bool readable, out bool restoreReadability)
        {
            restoreReadability = false;
            TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null)
            {
                Debug.LogError($"[WorldProceduralFloraTextureAuthoring] Missing texture importer for '{assetPath}'.");
                return false;
            }

            if (importer.isReadable == readable)
                return true;

            restoreReadability = !importer.isReadable;
            importer.isReadable = readable;
            importer.SaveAndReimport();
            return true;
        }

        private static void RestoreTextureReadability(string[] assetPaths, bool[] restoreReadability)
        {
            int count = Mathf.Min(assetPaths != null ? assetPaths.Length : 0, restoreReadability != null ? restoreReadability.Length : 0);
            for (int i = 0; i < count; i++)
            {
                if (!restoreReadability[i])
                    continue;

                TextureImporter importer = AssetImporter.GetAtPath(assetPaths[i]) as TextureImporter;
                if (importer == null)
                    continue;

                importer.isReadable = false;
                importer.SaveAndReimport();
            }
        }

        private static void ConfigureSeaGrassAtlasImporter()
        {
            TextureImporter importer = AssetImporter.GetAtPath(SeaGrassAtlasAssetPath) as TextureImporter;
            if (importer == null)
                return;

            importer.textureType = TextureImporterType.Default;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.mipmapEnabled = true;
            importer.isReadable = false;
            importer.sRGBTexture = true;
            importer.maxTextureSize = SeaGrassAtlasSize;
            importer.textureCompression = TextureImporterCompression.Compressed;
            importer.crunchedCompression = false;

            TextureImporterPlatformSettings standalone = importer.GetPlatformTextureSettings("Standalone");
            standalone.overridden = true;
            standalone.maxTextureSize = SeaGrassAtlasSize;
            standalone.format = TextureImporterFormat.BC7;
            standalone.textureCompression = TextureImporterCompression.Compressed;
            standalone.crunchedCompression = false;
            importer.SetPlatformTextureSettings(standalone);
            importer.SaveAndReimport();
        }

        private static void CountTextureSource(Texture texture, ref int importedCount, ref int generatedCount, ref int managedCount, ref int assignedCount)
        {
            if (texture == null)
                return;

            assignedCount++;
            if (IsImportedFloraTexture(texture))
            {
                importedCount++;
                managedCount++;
                return;
            }

            if (IsGeneratedProceduralTexture(texture))
            {
                generatedCount++;
                managedCount++;
            }
        }

        private static bool CreateOrUpdateBaseTexture(string path, Color lowColor, Color midColor, Color highColor, float bandStrength)
        {
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (texture == null)
            {
                texture = new Texture2D(64, 256, TextureFormat.RGBA32, false, true)
                {
                    name = System.IO.Path.GetFileNameWithoutExtension(path),
                    wrapMode = TextureWrapMode.Repeat,
                    filterMode = FilterMode.Bilinear,
                    anisoLevel = 1
                };
                AssetDatabase.CreateAsset(texture, path);
            }

            int width = texture.width;
            int height = texture.height;
            NativeArray<Color32> pixels = texture.GetRawTextureData<Color32>();
            for (int y = 0; y < height; y++)
            {
                float v = y / (float)(height - 1);
                Color gradient = v < 0.55f
                    ? Color.Lerp(lowColor, midColor, v / 0.55f)
                    : Color.Lerp(midColor, highColor, (v - 0.55f) / 0.45f);

                for (int x = 0; x < width; x++)
                {
                    float u = x / (float)(width - 1);
                    float centerRib = 1.0f - Mathf.Abs(u * 2.0f - 1.0f);
                    float edgeMask = FastEdgePower(Mathf.Abs(u * 2.0f - 1.0f));
                    float stripe = FastSignedWavePi(u * 8.0f + v * 5.5f);
                    float veinA = FastSignedWavePi(u * 34.0f - v * 16.0f);
                    float veinB = FastSignedWavePi(u * 18.0f + v * 24.0f);
                    float mottled = FastSignedWavePi(u * 23.0f + v * 13.0f) * 0.5f + 0.5f;
                    float band = 1.0f + stripe * bandStrength + (mottled - 0.5f) * 0.08f;
                    Color baseTint = gradient * band;
                    Color ribTint = Color.Lerp(baseTint, highColor * 1.08f, centerRib * 0.24f);
                    Color edgeTint = Color.Lerp(ribTint, lowColor * 0.88f + new Color(0.08f, 0.06f, 0.02f), edgeMask * 0.22f);
                    float veinMask = Mathf.Clamp01(0.5f + veinA * 0.16f + veinB * 0.08f);
                    pixels[y * width + x] = Color.Lerp(edgeTint, edgeTint * (0.92f + veinMask * 0.16f), centerRib * 0.42f);
                }
            }

            texture.Apply(updateMipmaps: false, makeNoLongerReadable: false);
            EditorUtility.SetDirty(texture);
            return true;
        }

        private static bool CreateOrUpdateDetailTexture(string path, int seed)
        {
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (texture == null)
            {
                texture = new Texture2D(128, 128, TextureFormat.RGBA32, false, true)
                {
                    name = System.IO.Path.GetFileNameWithoutExtension(path),
                    wrapMode = TextureWrapMode.Repeat,
                    filterMode = FilterMode.Bilinear,
                    anisoLevel = 1
                };
                AssetDatabase.CreateAsset(texture, path);
            }

            int width = texture.width;
            int height = texture.height;
            NativeArray<Color32> pixels = texture.GetRawTextureData<Color32>();
            for (int y = 0; y < height; y++)
            {
                float v = y / (float)(height - 1);
                for (int x = 0; x < width; x++)
                {
                    float u = x / (float)(width - 1);
                    float a = FastSignedWavePi(u * (9 + seed * 0.1f) + v * 5.1f);
                    float b = FastSignedWavePi(u * 17.0f - v * (7 + seed * 0.05f));
                    float c = FastSignedWavePi((u + v) * (11 + seed * 0.07f));
                    float centerRib = 1.0f - Mathf.Abs(u * 2.0f - 1.0f);
                    float longitudinal = FastSignedWavePi(v * (26.0f + seed * 0.03f) + u * 3.5f);
                    float edgeWear = FastEdgePower(Mathf.Abs(u * 2.0f - 1.0f));
                    float value = Mathf.Clamp01(0.5f + a * 0.24f + b * 0.18f + c * 0.12f + longitudinal * 0.08f + centerRib * 0.12f - edgeWear * 0.08f);
                    pixels[y * width + x] = new Color(value, value, value, 1f);
                }
            }

            texture.Apply(updateMipmaps: false, makeNoLongerReadable: false);
            EditorUtility.SetDirty(texture);
            return true;
        }

        private static bool CreateOrUpdateNormalTexture(string path, int seed, float normalScale)
        {
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (texture == null)
            {
                texture = new Texture2D(128, 128, TextureFormat.RGBA32, false, true)
                {
                    name = System.IO.Path.GetFileNameWithoutExtension(path),
                    wrapMode = TextureWrapMode.Repeat,
                    filterMode = FilterMode.Bilinear,
                    anisoLevel = 1
                };
                AssetDatabase.CreateAsset(texture, path);
            }

            int width = texture.width;
            int height = texture.height;
            NativeArray<Color32> pixels = texture.GetRawTextureData<Color32>();
            for (int y = 0; y < height; y++)
            {
                float v = y / (float)(height - 1);
                for (int x = 0; x < width; x++)
                {
                    float u = x / (float)(width - 1);
                    float center = SampleLeafHeight(u, v, seed);
                    float sampleX = SampleLeafHeight(Mathf.Repeat(u + 1.0f / width, 1.0f), v, seed);
                    float sampleY = SampleLeafHeight(u, Mathf.Repeat(v + 1.0f / height, 1.0f), seed);
                    EncodeDominantAxisNormal((sampleX - center) * normalScale, (sampleY - center) * normalScale, out Color32 normal);
                    pixels[y * width + x] = normal;
                }
            }

            texture.Apply(updateMipmaps: false, makeNoLongerReadable: false);
            EditorUtility.SetDirty(texture);
            return true;
        }

        private static bool CreateOrUpdateMaskTexture(string path, int seed, float thicknessBase, float thicknessTip)
        {
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (texture == null)
            {
                texture = new Texture2D(128, 256, TextureFormat.RGBA32, false, true)
                {
                    name = System.IO.Path.GetFileNameWithoutExtension(path),
                    wrapMode = TextureWrapMode.Repeat,
                    filterMode = FilterMode.Bilinear,
                    anisoLevel = 1
                };
                AssetDatabase.CreateAsset(texture, path);
            }

            int width = texture.width;
            int height = texture.height;
            NativeArray<Color32> pixels = texture.GetRawTextureData<Color32>();
            for (int y = 0; y < height; y++)
            {
                float v = y / (float)(height - 1);
                float thickness = Mathf.Lerp(thicknessBase, thicknessTip, FastTipCurve(v));
                for (int x = 0; x < width; x++)
                {
                    float u = x / (float)(width - 1);
                    float centerRib = 1.0f - Mathf.Abs(u * 2.0f - 1.0f);
                    float edgeMask = FastEdgePower(Mathf.Abs(u * 2.0f - 1.0f));
                    float gloss = Mathf.Clamp01(0.44f + FastSignedWavePi(u * (7.0f + seed * 0.08f) + v * 3.1f) * 0.20f + centerRib * 0.22f - edgeMask * 0.10f);
                    float ambientLift = Mathf.Clamp01(0.40f + centerRib * 0.38f + FastSignedWavePi((u + v) * (5.0f + seed * 0.04f)) * 0.08f);
                    float causticBias = Mathf.Clamp01(0.46f + FastSignedWavePi(u * 13.0f - v * (9.0f + seed * 0.03f)) * 0.22f + edgeMask * 0.06f);
                    float thicknessValue = Mathf.Clamp01(thickness + centerRib * 0.12f - edgeMask * 0.14f);
                    pixels[y * width + x] = new Color(thicknessValue, gloss, ambientLift, causticBias);
                }
            }

            texture.Apply(updateMipmaps: false, makeNoLongerReadable: false);
            EditorUtility.SetDirty(texture);
            return true;
        }

        private static bool CreateOrUpdateCoralNormalTexture(string path, int seed, float normalScale)
        {
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (texture == null)
            {
                texture = new Texture2D(128, 128, TextureFormat.RGBA32, false, true)
                {
                    name = System.IO.Path.GetFileNameWithoutExtension(path),
                    wrapMode = TextureWrapMode.Repeat,
                    filterMode = FilterMode.Bilinear,
                    anisoLevel = 1
                };
                AssetDatabase.CreateAsset(texture, path);
            }

            int width = texture.width;
            int height = texture.height;
            NativeArray<Color32> pixels = texture.GetRawTextureData<Color32>();
            for (int y = 0; y < height; y++)
            {
                float v = y / (float)(height - 1);
                for (int x = 0; x < width; x++)
                {
                    float u = x / (float)(width - 1);
                    float center = SampleCoralHeight(u, v, seed);
                    float sampleX = SampleCoralHeight(Mathf.Repeat(u + 1.0f / width, 1.0f), v, seed);
                    float sampleY = SampleCoralHeight(u, Mathf.Repeat(v + 1.0f / height, 1.0f), seed);
                    EncodeDominantAxisNormal((sampleX - center) * normalScale, (sampleY - center) * normalScale, out Color32 normal);
                    pixels[y * width + x] = normal;
                }
            }

            texture.Apply(updateMipmaps: false, makeNoLongerReadable: false);
            EditorUtility.SetDirty(texture);
            return true;
        }

        private static bool CreateOrUpdateCoralMaskTexture(string path, int seed, float cavityBase, float thicknessBase)
        {
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (texture == null)
            {
                texture = new Texture2D(128, 128, TextureFormat.RGBA32, false, true)
                {
                    name = System.IO.Path.GetFileNameWithoutExtension(path),
                    wrapMode = TextureWrapMode.Repeat,
                    filterMode = FilterMode.Bilinear,
                    anisoLevel = 1
                };
                AssetDatabase.CreateAsset(texture, path);
            }

            int width = texture.width;
            int height = texture.height;
            NativeArray<Color32> pixels = texture.GetRawTextureData<Color32>();
            for (int y = 0; y < height; y++)
            {
                float v = y / (float)(height - 1);
                for (int x = 0; x < width; x++)
                {
                    float u = x / (float)(width - 1);
                    float ridge = Mathf.Clamp01(0.5f + FastSignedWavePi(u * (8.0f + seed * 0.05f) + v * 5.2f) * 0.34f);
                    float cavity = Mathf.Clamp01(cavityBase + FastSignedWavePi(u * 17.0f - v * (9.0f + seed * 0.03f)) * 0.22f);
                    float gloss = Mathf.Clamp01(0.42f + ridge * 0.34f + FastSignedWavePi((u + v) * (7.0f + seed * 0.02f)) * 0.12f);
                    float thickness = Mathf.Clamp01(thicknessBase + ridge * 0.18f + FastSignedWavePi(u * 5.0f + v * 11.0f) * 0.08f);
                    pixels[y * width + x] = new Color(ridge, gloss, cavity, thickness);
                }
            }

            texture.Apply(updateMipmaps: false, makeNoLongerReadable: false);
            EditorUtility.SetDirty(texture);
            return true;
        }

        private static string ResolveBaseTexturePath(string familyId)
        {
            switch (familyId)
            {
                case FamilyKelpTall:
                    return ResolvePreferredTexturePath(familyId, "albedo", TextureRoot + "/TX_KelpTall_Base.asset");
                case FamilyKelpPatchDense:
                    return ResolvePreferredTexturePath(familyId, "albedo", TextureRoot + "/TX_KelpPatch_Base.asset");
                case FamilyKelpCanopy:
                    return ResolvePreferredTexturePath(familyId, "albedo", TextureRoot + "/TX_KelpCanopy_Base.asset");
                case FamilyKelpAbyssal:
                    return ResolvePreferredTexturePath(familyId, "albedo", TextureRoot + "/TX_KelpAbyssal_Base.asset");
                default:
                    return string.Empty;
            }
        }

        private static string ResolveDetailTexturePath(string familyId)
        {
            switch (familyId)
            {
                case FamilyKelpTall:
                    return ResolvePreferredTexturePath(familyId, "detail", TextureRoot + "/TX_KelpTall_Detail.asset");
                case FamilyKelpPatchDense:
                    return ResolvePreferredTexturePath(familyId, "detail", TextureRoot + "/TX_KelpPatch_Detail.asset");
                case FamilyKelpCanopy:
                    return ResolvePreferredTexturePath(familyId, "detail", TextureRoot + "/TX_KelpCanopy_Detail.asset");
                case FamilyKelpAbyssal:
                    return ResolvePreferredTexturePath(familyId, "detail", TextureRoot + "/TX_KelpAbyssal_Detail.asset");
                default:
                    return string.Empty;
            }
        }

        private static string ResolveNormalTexturePath(string familyId)
        {
            switch (familyId)
            {
                case FamilyKelpTall:
                    return ResolvePreferredTexturePath(familyId, "normal", TextureRoot + "/TX_KelpTall_Normal.asset");
                case FamilyKelpPatchDense:
                    return ResolvePreferredTexturePath(familyId, "normal", TextureRoot + "/TX_KelpPatch_Normal.asset");
                case FamilyKelpCanopy:
                    return ResolvePreferredTexturePath(familyId, "normal", TextureRoot + "/TX_KelpCanopy_Normal.asset");
                case FamilyKelpAbyssal:
                    return ResolvePreferredTexturePath(familyId, "normal", TextureRoot + "/TX_KelpAbyssal_Normal.asset");
                default:
                    return string.Empty;
            }
        }

        private static string ResolveMaskTexturePath(string familyId)
        {
            switch (familyId)
            {
                case FamilyKelpTall:
                    return ResolvePreferredTexturePath(familyId, "mask", TextureRoot + "/TX_KelpTall_Mask.asset");
                case FamilyKelpPatchDense:
                    return ResolvePreferredTexturePath(familyId, "mask", TextureRoot + "/TX_KelpPatch_Mask.asset");
                case FamilyKelpCanopy:
                    return ResolvePreferredTexturePath(familyId, "mask", TextureRoot + "/TX_KelpCanopy_Mask.asset");
                case FamilyKelpAbyssal:
                    return ResolvePreferredTexturePath(familyId, "mask", TextureRoot + "/TX_KelpAbyssal_Mask.asset");
                default:
                    return string.Empty;
            }
        }

        private static string ResolveCoralBaseTexturePath(string familyId)
        {
            switch (familyId)
            {
                case FamilyCoralLow:
                    return ResolvePreferredTexturePath(familyId, "albedo", TextureRoot + "/TX_CoralLow_Base.asset");
                case FamilyCoralBranching:
                    return ResolvePreferredTexturePath(familyId, "albedo", TextureRoot + "/TX_CoralBranching_Base.asset");
                case FamilyCoralMassive:
                    return ResolvePreferredTexturePath(familyId, "albedo", TextureRoot + "/TX_CoralMassive_Base.asset");
                case FamilyCoralPlate:
                    return ResolvePreferredTexturePath(familyId, "albedo", TextureRoot + "/TX_CoralPlate_Base.asset");
                case FamilyCoralBrittle:
                    return ResolvePreferredTexturePath(familyId, "albedo", TextureRoot + "/TX_CoralBrittle_Base.asset");
                default:
                    return string.Empty;
            }
        }

        private static string ResolveCoralDetailTexturePath(string familyId)
        {
            switch (familyId)
            {
                case FamilyCoralLow:
                    return ResolvePreferredTexturePath(familyId, "detail", TextureRoot + "/TX_CoralLow_Detail.asset");
                case FamilyCoralBranching:
                    return ResolvePreferredTexturePath(familyId, "detail", TextureRoot + "/TX_CoralBranching_Detail.asset");
                case FamilyCoralMassive:
                    return ResolvePreferredTexturePath(familyId, "detail", TextureRoot + "/TX_CoralMassive_Detail.asset");
                case FamilyCoralPlate:
                    return ResolvePreferredTexturePath(familyId, "detail", TextureRoot + "/TX_CoralPlate_Detail.asset");
                case FamilyCoralBrittle:
                    return ResolvePreferredTexturePath(familyId, "detail", TextureRoot + "/TX_CoralBrittle_Detail.asset");
                default:
                    return string.Empty;
            }
        }

        private static string ResolveCoralNormalTexturePath(string familyId)
        {
            switch (familyId)
            {
                case FamilyCoralLow:
                    return ResolvePreferredTexturePath(familyId, "normal", TextureRoot + "/TX_CoralLow_Normal.asset");
                case FamilyCoralBranching:
                    return ResolvePreferredTexturePath(familyId, "normal", TextureRoot + "/TX_CoralBranching_Normal.asset");
                case FamilyCoralMassive:
                    return ResolvePreferredTexturePath(familyId, "normal", TextureRoot + "/TX_CoralMassive_Normal.asset");
                case FamilyCoralPlate:
                    return ResolvePreferredTexturePath(familyId, "normal", TextureRoot + "/TX_CoralPlate_Normal.asset");
                case FamilyCoralBrittle:
                    return ResolvePreferredTexturePath(familyId, "normal", TextureRoot + "/TX_CoralBrittle_Normal.asset");
                default:
                    return string.Empty;
            }
        }

        private static string ResolveCoralMaskTexturePath(string familyId)
        {
            switch (familyId)
            {
                case FamilyCoralLow:
                    return ResolvePreferredTexturePath(familyId, "mask", TextureRoot + "/TX_CoralLow_Mask.asset");
                case FamilyCoralBranching:
                    return ResolvePreferredTexturePath(familyId, "mask", TextureRoot + "/TX_CoralBranching_Mask.asset");
                case FamilyCoralMassive:
                    return ResolvePreferredTexturePath(familyId, "mask", TextureRoot + "/TX_CoralMassive_Mask.asset");
                case FamilyCoralPlate:
                    return ResolvePreferredTexturePath(familyId, "mask", TextureRoot + "/TX_CoralPlate_Mask.asset");
                case FamilyCoralBrittle:
                    return ResolvePreferredTexturePath(familyId, "mask", TextureRoot + "/TX_CoralBrittle_Mask.asset");
                default:
                    return string.Empty;
            }
        }

        private static string ResolvePreferredTexturePath(string familyId, string mapToken, string fallbackPath)
        {
            string importedPath = GetImportedTexturePath(familyId, mapToken);
            Texture2D importedTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(importedPath);
            return importedTexture != null ? importedPath : fallbackPath;
        }

        private static string GetImportedTexturePath(string familyId, string mapToken)
        {
            return $"{ImportedTextureRoot}/{familyId}/{mapToken}___{familyId}.png";
        }

        private static ImportedFamilyStatus EvaluateImportedFamilyStatus(string familyId)
        {
            int presentMapCount = 0;
            bool contractOk = true;
            string firstNote = "ok";
            double familyEstimatedGpuMb = 0.0;
            string latestRevisionFolderName;
            bool hasRevisionCandidate = TryGetLatestImportedRevisionFolderName(familyId, out latestRevisionFolderName);

            for (int mapIndex = 0; mapIndex < RequiredMapTokens.Length; mapIndex++)
            {
                string mapToken = RequiredMapTokens[mapIndex];
                string importedPath = GetImportedTexturePath(familyId, mapToken);
                Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(importedPath);
                if (texture == null)
                {
                    if (string.Equals(firstNote, "ok", System.StringComparison.Ordinal))
                        firstNote = "missing-" + mapToken;

                    continue;
                }

                presentMapCount++;
                familyEstimatedGpuMb += EstimateImportedTextureGpuMb(mapToken);
                string failureLabel;
                if (TryGetImportedTextureContractFailure(texture, familyId, mapToken, out failureLabel))
                {
                    contractOk = false;
                    if (string.Equals(firstNote, "ok", System.StringComparison.Ordinal))
                        firstNote = mapToken + ":" + failureLabel;
                }
            }

            if (hasRevisionCandidate)
            {
                if (string.Equals(firstNote, "ok", System.StringComparison.Ordinal))
                    firstNote = "alt-revision:" + latestRevisionFolderName;
                else
                    firstNote += " | alt-revision:" + latestRevisionFolderName;
            }

            bool coverageComplete = presentMapCount == RequiredMapTokens.Length;
            bool contractClean = coverageComplete && contractOk;
            return new ImportedFamilyStatus(presentMapCount, familyEstimatedGpuMb, coverageComplete, contractClean, firstNote);
        }

        private static string ResolveContractLabel(ImportedFamilyStatus status)
        {
            if (!status.CoverageComplete)
                return "missing";

            return status.ContractClean ? "ok" : "stale";
        }

        private static TextureRequestFamilySpec ResolveTextureRequestFamilySpec(string familyId)
        {
            switch (familyId)
            {
                case FamilyCoralMassive:
                    return new TextureRequestFamilySpec(
                        familyId,
                        "ORGANIC_shallow",
                        "sunlit underwater, turquoise tones, silicon-based coral textures",
                        "massive bulbous coral head surface, porous calcified cavities, weathered reef erosion, warm coral limestone tones");

                case FamilyCoralPlate:
                    return new TextureRequestFamilySpec(
                        familyId,
                        "ORGANIC_shallow",
                        "sunlit underwater, turquoise tones, silicon-based coral textures",
                        "thin layered plate coral surface, stacked shelf disks, laminar calcified ridges, subtle reef wear");

                case FamilyCoralBrittle:
                    return new TextureRequestFamilySpec(
                        familyId,
                        "ORGANIC_abyss",
                        "bioluminescent deep sea, dark basalt substrate, pale translucent growths, chemosynthetic textures",
                        "deep brittle coral skeleton surface, thin chalky branching lattice, mineral staining, chemosynthetic abyss wear");

                default:
                    return default;
            }
        }

        private static string BuildMasterPrompt(TextureRequestFamilySpec requestSpec, string mapToken)
        {
            string texturePhrase;
            switch (mapToken)
            {
                case "albedo":
                    texturePhrase = "albedo/diffuse surface color, natural variation, calcified coral tones, zero specular highlights";
                    break;

                case "normal":
                    texturePhrase = "normal map visualization, coral ridges, pores, grooves, and calcified branch scars, neutral purple-blue base, green-up convention, no color variation";
                    break;

                case "mask":
                    texturePhrase = "mask texture, grayscale distribution of roughness and AO, calcified coral micro-surface variation, neutral gray base";
                    break;

                case "detail":
                    texturePhrase = "fine detail normal map, coral pores, mineral grain, chalky micro-scratches, sub-millimeter surface noise, high frequency only";
                    break;

                default:
                    texturePhrase = mapToken + " texture";
                    break;
            }

            return $"Seamless tiling PBR {mapToken} texture, {texturePhrase}, {requestSpec.SubjectDescription}, {requestSpec.BiomeContext}, top-down orthographic view, uniform lighting, no shadows, no perspective distortion, photorealistic, 4K resolution, edge-perfect seamless tile pattern, --tile --v 6 --ar 1:1";
        }

        private static bool TryGetLatestImportedRevisionFolderName(string familyId, out string folderName)
        {
            folderName = string.Empty;
            if (string.IsNullOrWhiteSpace(familyId))
                return false;

            string importedRootPath = Path.Combine(Directory.GetCurrentDirectory(), ImportedTextureRoot.Replace('/', Path.DirectorySeparatorChar));
            if (!Directory.Exists(importedRootPath))
                return false;

            string[] candidateDirectories = Directory.GetDirectories(importedRootPath, familyId + ".v*");
            int bestRevision = -1;
            for (int i = 0; i < candidateDirectories.Length; i++)
            {
                string candidateFolderName = Path.GetFileName(candidateDirectories[i]);
                if (!TryParseImportedRevisionFolderName(candidateFolderName, familyId, out int revision))
                    continue;

                if (revision <= bestRevision)
                    continue;

                bestRevision = revision;
                folderName = candidateFolderName;
            }

            return bestRevision > 0;
        }

        private static bool TryParseImportedRevisionFolderName(string folderName, string familyId, out int revision)
        {
            revision = 0;
            if (string.IsNullOrWhiteSpace(folderName) || string.IsNullOrWhiteSpace(familyId))
                return false;

            string prefix = familyId + ".v";
            if (!folderName.StartsWith(prefix, System.StringComparison.OrdinalIgnoreCase))
                return false;

            string revisionToken = folderName.Substring(prefix.Length);
            return int.TryParse(revisionToken, out revision) && revision > 0;
        }

        private static double EstimateImportedTextureGpuMb(string mapToken)
        {
            switch (mapToken)
            {
                case "albedo":
                case "mask":
                case "normal":
                    return 5.3;
                case "detail":
                    return 1.3;
                default:
                    return 0.0;
            }
        }

        private static float SampleLeafHeight(float u, float v, int seed)
        {
            float stripeA = FastSignedWavePi(u * (8.0f + seed * 0.05f) + v * 4.8f);
            float stripeB = FastSignedWavePi(u * 21.0f - v * (6.0f + seed * 0.03f));
            float curl = FastSignedWavePi((u * 0.75f + v) * (12.0f + seed * 0.02f));
            float centerRib = 1.0f - Mathf.Abs(u * 2.0f - 1.0f);
            float edgeWear = FastEdgePower(Mathf.Abs(u * 2.0f - 1.0f));
            float microVein = FastSignedWavePi(u * 31.0f + v * (17.0f + seed * 0.03f));
            return stripeA * 0.18f + stripeB * 0.10f + curl * 0.08f + centerRib * 0.18f + microVein * 0.05f - edgeWear * 0.04f;
        }

        private static float SampleCoralHeight(float u, float v, int seed)
        {
            float cells = FastSignedWavePi(u * (10.0f + seed * 0.06f) + v * 7.0f);
            float ridges = FastSignedWavePi(u * 19.0f - v * (11.0f + seed * 0.04f));
            float pores = FastSignedWavePi((u + v * 0.85f) * (15.0f + seed * 0.03f));
            return cells * 0.16f + ridges * 0.12f + pores * 0.10f;
        }

        private static float FastSignedWavePi(float halfTurns)
        {
            float cycle = (halfTurns * 0.5f) - Mathf.Floor(halfTurns * 0.5f);
            return 1f - Mathf.Abs(cycle * 2f - 1f) * 2f;
        }

        private static float FastEdgePower(float value)
        {
            float x = Mathf.Clamp01(value);
            return x * (0.68f + x * 0.32f);
        }

        private static float FastTipCurve(float value)
        {
            float x = Mathf.Clamp01(value);
            return x * (1.32f - x * 0.32f);
        }

        private static void EncodeDominantAxisNormal(float dx, float dy, out Color32 normal)
        {
            float invDominant = 1f / Mathf.Max(1f, Mathf.Abs(dx), Mathf.Abs(dy));
            normal = new Color32(
                EncodeSignedNormalChannel(-dx * invDominant),
                EncodeSignedNormalChannel(-dy * invDominant),
                EncodeSignedNormalChannel(invDominant),
                255);
        }

        private static byte EncodeSignedNormalChannel(float value)
        {
            return (byte)Mathf.Clamp(Mathf.RoundToInt((value * 0.5f + 0.5f) * 255f), 0, 255);
        }

        private static void EnsureFolder(string assetPath)
        {
            if (AssetDatabase.IsValidFolder(assetPath))
                return;

            int lastSeparator = assetPath.LastIndexOf('/');
            if (lastSeparator <= 0)
                return;

            string parentPath = assetPath.Substring(0, lastSeparator);
            string folderName = assetPath.Substring(lastSeparator + 1);
            EnsureFolder(parentPath);

            if (!AssetDatabase.IsValidFolder(assetPath))
                AssetDatabase.CreateFolder(parentPath, folderName);
        }

        private readonly struct ImportedFamilyStatus
        {
            public ImportedFamilyStatus(int presentMapCount, double estimatedGpuMb, bool coverageComplete, bool contractClean, string primaryNote)
            {
                PresentMapCount = presentMapCount;
                EstimatedGpuMb = estimatedGpuMb;
                CoverageComplete = coverageComplete;
                ContractClean = contractClean;
                PrimaryNote = primaryNote ?? string.Empty;
            }

            public int PresentMapCount { get; }
            public double EstimatedGpuMb { get; }
            public bool CoverageComplete { get; }
            public bool ContractClean { get; }
            public string PrimaryNote { get; }
        }

        private readonly struct TextureRequestFamilySpec
        {
            public TextureRequestFamilySpec(string familyId, string biomeToken, string biomeContext, string subjectDescription)
            {
                FamilyId = familyId ?? string.Empty;
                BiomeToken = biomeToken ?? string.Empty;
                BiomeContext = biomeContext ?? string.Empty;
                SubjectDescription = subjectDescription ?? string.Empty;
            }

            public string FamilyId { get; }
            public string BiomeToken { get; }
            public string BiomeContext { get; }
            public string SubjectDescription { get; }
            public bool IsValid => !string.IsNullOrWhiteSpace(FamilyId)
                && !string.IsNullOrWhiteSpace(BiomeToken)
                && !string.IsNullOrWhiteSpace(BiomeContext)
                && !string.IsNullOrWhiteSpace(SubjectDescription);
        }
    }
}
