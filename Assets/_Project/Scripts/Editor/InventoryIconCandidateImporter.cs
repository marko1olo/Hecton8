namespace Hecton8.Editor
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using Hecton8.Items;
    using UnityEditor;
    using UnityEngine;

    /// <summary>
    /// Editor-only import prep for generated inventory icon candidates.
    /// Uses individual transparent PNGs for ItemData.icon binding.
    /// Atlases are imported as sprite previews/source sheets; runtime UI does not read IconAtlasIndex yet.
    /// </summary>
    public static class InventoryIconCandidateImporter
    {
        private const string CandidateRoot = "Assets/_Project/Art/Sprites/ui/InventoryGenerated/Batch30/Alpha512";
        private const string Atlas512Path = "Assets/_Project/Art/Sprites/ui/InventoryGenerated/Batch30/Atlas/TX_B30_InventoryGenerated_CandidateAtlas_512xCells.png";
        private const string Atlas256Path = "Assets/_Project/Art/Sprites/ui/InventoryGenerated/Batch30/Atlas/TX_B30_InventoryGenerated_CandidateAtlas_512xCells_256xCells.png";
        private const string AtlasManifestPath = "Assets/_Project/Art/Sprites/ui/InventoryGenerated/Batch30/Atlas/TX_B30_InventoryGenerated_CandidateAtlas_Manifest.json";
        private const string BindingMapPath = "Assets/_Project/Art/Sprites/ui/InventoryGenerated/Batch30/InventoryIconCandidateBindingMap.json";

        private const long MaxManifestBytes = 5242880L; // 5MB limit for JSON files

        [MenuItem("Hecton8/Art/Inventory Icons/Prepare Batch30 Candidate Sprites")]
        public static void PrepareBatch30CandidateSprites()
        {
            int changed = PrepareSpritesUnder(CandidateRoot, maxTextureSize: 512, spriteMode: SpriteImportMode.Single);
            changed += PrepareSingleSprite(Atlas256Path, maxTextureSize: 2048);
            changed += PrepareSingleSprite(Atlas512Path, maxTextureSize: 4096);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[InventoryIconCandidateImporter] Prepared generated inventory sprite candidates. changed={changed}");
        }

        [MenuItem("Hecton8/Art/Inventory Icons/Validate Batch30 Candidate Sprite Paths")]
        public static void ValidateBatch30CandidateSpritePaths()
        {
            int errors = ValidateBatch30CandidateSpritePathsCore(out int warnings, out int pngCount, out int rootPngCount);
            LogCandidateValidationSummary(errors, warnings, pngCount, rootPngCount);
        }

        private static int ValidateBatch30CandidateSpritePathsCore(
            out int warnings,
            out int pngCount,
            out int rootPngCount)
        {
            rootPngCount = CountPngs(CandidateRoot);
            pngCount = rootPngCount +
                           (ProjectFileExists(Atlas512Path) ? 1 : 0) +
                           (ProjectFileExists(Atlas256Path) ? 1 : 0);

            int errors = 0;
            warnings = 0;
            AtlasManifest manifest = LoadAtlasManifest(ref errors);
            ValidateAtlasManifest(manifest, rootPngCount, ref errors, ref warnings);
            ValidateBindingMap(manifest, ref errors, ref warnings);
            return errors;
        }

        private static void LogCandidateValidationSummary(int errors, int warnings, int pngCount, int rootPngCount)
        {
            string summary =
                $"[InventoryIconCandidateImporter] Batch30 candidate validation complete. " +
                $"pngCount={pngCount}, rootPngCount={rootPngCount}, errors={errors}, warnings={warnings}, " +
                $"candidateRoot='{CandidateRoot}', atlas='{Atlas512Path}'.";
            if (errors > 0)
                Debug.LogError(summary);
            else if (warnings > 0)
                Debug.LogWarning(summary);
            else
                Debug.Log(summary);
        }

        [MenuItem("Hecton8/Art/Inventory Icons/Bind Batch30 Mapped Item Icons")]
        public static void BindBatch30MappedItemIcons()
        {
            PrepareBatch30CandidateSprites();

            if (!TryLoadBindingMap(out IconBindingMap map))
                return;

            int changed = 0;
            int skipped = 0;
            for (int i = 0; i < map.bindings.Length; i++)
            {
                IconBinding binding = map.bindings[i];
                if (!binding.enabled)
                {
                    skipped++;
                    continue;
                }

                if (!TryBindIcon(in binding, allowOverwrite: false))
                {
                    skipped++;
                    continue;
                }

                changed++;
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[InventoryIconCandidateImporter] Bound Batch30 mapped item icons. changed={changed}, skipped={skipped}");
        }

        [MenuItem("Hecton8/Art/Inventory Icons/Validate Batch30 Bound Item Icons")]
        public static void ValidateBatch30BoundItemIcons()
        {
            int errors = ValidateBoundIconMap(out int checkedCount, out int skipped);
            string summary =
                $"[InventoryIconCandidateImporter] Batch30 bound icon validation complete. " +
                $"checked={checkedCount}, skipped={skipped}, errors={errors}.";
            if (errors > 0)
                Debug.LogError(summary);
            else
                Debug.Log(summary);
        }

        public static void BatchBindAndValidateBatch30MappedItemIcons()
        {
            int candidateErrors = ValidateBatch30CandidateSpritePathsCore(
                out int candidateWarnings,
                out int pngCount,
                out int rootPngCount);
            LogCandidateValidationSummary(candidateErrors, candidateWarnings, pngCount, rootPngCount);
            if (candidateErrors > 0)
                throw new InvalidOperationException($"Batch30 inventory candidate validation failed before binding. errors={candidateErrors}");

            BindBatch30MappedItemIcons();

            int errors = ValidateBoundIconMap(out int checkedCount, out int skipped);
            Debug.Log(
                $"[InventoryIconCandidateImporter] Batch30 batch bind validation. " +
                $"checked={checkedCount}, skipped={skipped}, errors={errors}.");

            if (errors > 0)
                throw new InvalidOperationException($"Batch30 inventory icon binding validation failed. errors={errors}");
        }

        public static void BatchBindAndValidateInventoryIconsFromArgs()
        {
            string bindingMapPath = ReadCommandLineValue("-h8InventoryIconBindingMap");
            if (string.IsNullOrWhiteSpace(bindingMapPath))
                throw new InvalidOperationException("Missing -h8InventoryIconBindingMap <path>.");

            bindingMapPath = NormalizeAssetPath(bindingMapPath);
            int maxTextureSize = ReadCommandLineInt("-h8InventoryIconMaxTextureSize", 512);
            bool allowOverwrite = ReadCommandLineBool("-h8InventoryIconAllowOverwrite");
            if (!TryLoadBindingMap(bindingMapPath, out IconBindingMap map))
                throw new InvalidOperationException($"Failed to load inventory icon binding map: {bindingMapPath}");

            int validationErrors = ValidateGenericBindingMap(map, bindingMapPath, allowOverwrite, out int validationWarnings);
            Debug.Log(
                $"[InventoryIconCandidateImporter] Generic inventory icon binding map validation. " +
                $"map='{bindingMapPath}', errors={validationErrors}, warnings={validationWarnings}.");
            if (validationErrors > 0)
                throw new InvalidOperationException($"Inventory icon binding map validation failed. errors={validationErrors}");

            int prepared = PrepareSpritesFromBindingMap(map, maxTextureSize);
            prepared += PrepareAtlasesFromBindingMap(bindingMapPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            int changed = BindIconMap(map, allowOverwrite, out int skipped);
            AssetDatabase.SaveAssets();

            int boundErrors = ValidateBoundIconMap(map, out int checkedCount, out int boundSkipped);
            Debug.Log(
                $"[InventoryIconCandidateImporter] Generic inventory icon bind complete. " +
                $"map='{bindingMapPath}', prepared={prepared}, changed={changed}, skipped={skipped}, " +
                $"checked={checkedCount}, boundSkipped={boundSkipped}, errors={boundErrors}.");

            if (boundErrors > 0)
                throw new InvalidOperationException($"Inventory icon binding validation failed. errors={boundErrors}");
        }

        private static int PrepareSpritesUnder(string root, int maxTextureSize, SpriteImportMode spriteMode)
        {
            if (!AssetDatabase.IsValidFolder(root))
            {
                Debug.LogWarning($"[InventoryIconCandidateImporter] Missing folder: {root}");
                return 0;
            }

            string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { root });
            int changed = 0;
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (string.IsNullOrEmpty(path) || !path.EndsWith(".png", System.StringComparison.OrdinalIgnoreCase))
                    continue;

                TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null)
                    continue;

                bool dirty = ConfigureCommonSpriteImporter(importer, maxTextureSize, spriteMode);

                if (!dirty)
                    continue;

                importer.SaveAndReimport();
                changed++;
            }

            return changed;
        }

        private static int PrepareSingleSprite(string path, int maxTextureSize)
        {
            path = NormalizeAssetPath(path);
            if (!ProjectFileExists(path))
            {
                Debug.LogWarning($"[InventoryIconCandidateImporter] Missing atlas preview: {path}");
                return 0;
            }

            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                Debug.LogWarning($"[InventoryIconCandidateImporter] TextureImporter missing: {path}");
                return 0;
            }

            if (!ConfigureCommonSpriteImporter(importer, maxTextureSize, SpriteImportMode.Single))
                return 0;

            importer.SaveAndReimport();
            return 1;
        }

        private static bool ConfigureCommonSpriteImporter(TextureImporter importer, int maxTextureSize, SpriteImportMode spriteMode)
        {
            bool dirty = false;
            if (importer.textureType != TextureImporterType.Sprite)
            {
                importer.textureType = TextureImporterType.Sprite;
                dirty = true;
            }

            if (importer.spriteImportMode != spriteMode)
            {
                importer.spriteImportMode = spriteMode;
                dirty = true;
            }

            if (!importer.alphaIsTransparency)
            {
                importer.alphaIsTransparency = true;
                dirty = true;
            }

            if (importer.mipmapEnabled)
            {
                importer.mipmapEnabled = false;
                dirty = true;
            }

            if (importer.maxTextureSize != maxTextureSize)
            {
                importer.maxTextureSize = maxTextureSize;
                dirty = true;
            }

            if (importer.textureCompression != TextureImporterCompression.CompressedHQ)
            {
                importer.textureCompression = TextureImporterCompression.CompressedHQ;
                dirty = true;
            }

            return dirty;
        }

        private static int CountPngs(string root)
        {
            string physicalRoot = ToProjectFilePath(root);
            if (!Directory.Exists(physicalRoot))
                return 0;

            return Directory.GetFiles(physicalRoot, "*.png", SearchOption.AllDirectories).Length;
        }

        private static AtlasManifest LoadAtlasManifest(ref int errors)
        {
            if (!ProjectFileExists(AtlasManifestPath))
            {
                RecordError(ref errors, $"Missing atlas manifest: {AtlasManifestPath}");
                return null;
            }

            try
            {
                return ReadJsonFileCapped<AtlasManifest>(AtlasManifestPath, MaxManifestBytes, "Atlas Manifest");
            }
            catch (Exception exception)
            {
                RecordError(ref errors, $"Invalid atlas manifest JSON: {AtlasManifestPath}. {exception.Message}");
                return null;
            }
        }

        private static AtlasManifest LoadGenericAtlasManifest(string generatedRoot, ref int errors, ref int warnings)
        {
            string atlasRoot = NormalizeAssetPath(generatedRoot + "/Atlas");
            string physicalAtlasRoot = ToProjectFilePath(atlasRoot);
            if (!Directory.Exists(physicalAtlasRoot))
            {
                RecordError(ref errors, $"Missing generic atlas folder: {atlasRoot}");
                return null;
            }

            string[] manifests = Directory.GetFiles(physicalAtlasRoot, "*_Manifest.json", SearchOption.TopDirectoryOnly);
            if (manifests.Length != 1)
            {
                RecordError(ref errors, $"Generic atlas folder must contain exactly one *_Manifest.json. folder='{atlasRoot}', count={manifests.Length}.");
                return null;
            }

            string manifestPath = NormalizeAssetPath(manifests[0]);
            try
            {
                AtlasManifest manifest = ReadJsonFileCapped<AtlasManifest>(manifestPath, MaxManifestBytes, "Generic Atlas Manifest");
                if (manifest == null)
                {
                    RecordError(ref errors, $"Invalid generic atlas manifest JSON: {manifestPath}");
                    return null;
                }

                string expectedSource = NormalizeAssetPath(generatedRoot + "/Alpha512");
                string actualSource = NormalizeAssetPath(manifest.source);
                if (!string.Equals(actualSource, expectedSource, StringComparison.Ordinal))
                    RecordError(ref errors, $"Generic atlas manifest source mismatch. expected='{expectedSource}', actual='{actualSource}'.");

                ValidateGenericAtlasManifestContent(manifest, expectedSource, ref errors, ref warnings);
                ValidateGenericSourceBakeManifest(manifest, ref errors);
                ValidateScaledAtlases(manifest, ref errors, ref warnings);
                return manifest;
            }
            catch (Exception exception)
            {
                RecordError(ref errors, $"Invalid generic atlas manifest JSON: {manifestPath}. {exception.Message}");
                return null;
            }
        }

        private static void ValidateGenericAtlasManifestContent(
            AtlasManifest manifest,
            string expectedSource,
            ref int errors,
            ref int warnings)
        {
            if (manifest == null)
                return;

            string atlasPath = NormalizeAssetPath(manifest.atlas);
            if (string.IsNullOrWhiteSpace(atlasPath))
                RecordError(ref errors, "Generic atlas manifest has empty atlas path.");
            else if (!ProjectFileExists(atlasPath))
                RecordError(ref errors, $"Generic atlas manifest atlas is missing: {atlasPath}");

            if (manifest.entries == null || manifest.entries.Length == 0)
            {
                RecordError(ref errors, "Generic atlas manifest has no entries.");
                return;
            }

            HashSet<string> names = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < manifest.entries.Length; i++)
            {
                AtlasEntry entry = manifest.entries[i];
                if (entry == null)
                {
                    RecordError(ref errors, $"Generic atlas manifest entry[{i}] is null.");
                    continue;
                }

                string name = entry.name ?? string.Empty;
                if (string.IsNullOrWhiteSpace(name))
                    RecordError(ref errors, $"Generic atlas manifest entry[{i}] has empty name.");
                else if (!names.Add(name))
                    RecordError(ref errors, $"Generic atlas manifest duplicate entry name: {name}");

                string source = NormalizeAssetPath(entry.source);
                if (string.IsNullOrWhiteSpace(source))
                {
                    RecordError(ref errors, $"Generic atlas manifest entry[{i}] has empty source.");
                }
                else
                {
                    if (!IsUnderPath(source, expectedSource))
                        RecordError(ref errors, $"Generic atlas manifest entry[{i}] source is outside expected source root: {source}");

                    if (!ProjectFileExists(source))
                        RecordError(ref errors, $"Generic atlas manifest entry[{i}] source is missing: {source}");
                }

                if (entry.touches_cell_edge)
                    RecordError(ref errors, $"Generic atlas manifest entry[{i}] touches its cell edge: {name}");
            }
        }

        private static void ValidateGenericSourceBakeManifest(AtlasManifest manifest, ref int errors)
        {
            if (manifest == null)
                return;

            string sourceBakeManifestPath = NormalizeAssetPath(manifest.sourceBakeManifest);
            if (string.IsNullOrWhiteSpace(sourceBakeManifestPath))
            {
                RecordError(ref errors, "Generic atlas manifest is missing sourceBakeManifest.");
                return;
            }

            if (!ProjectFileExists(sourceBakeManifestPath))
            {
                RecordError(ref errors, $"Generic source bake manifest is missing: {sourceBakeManifestPath}");
                return;
            }

            SourceBakeManifest sourceManifest;
            try
            {
                sourceManifest = ReadJsonFileCapped<SourceBakeManifest>(sourceBakeManifestPath, MaxManifestBytes, "Source Bake Manifest");
            }
            catch (Exception exception)
            {
                RecordError(ref errors, $"Invalid source bake manifest JSON: {sourceBakeManifestPath}. {exception.Message}");
                return;
            }

            if (sourceManifest == null)
            {
                RecordError(ref errors, $"Invalid source bake manifest JSON: {sourceBakeManifestPath}");
                return;
            }

            string previewPath = NormalizeAssetPath(sourceManifest.sourceGridMarginPreview);
            if (string.IsNullOrWhiteSpace(previewPath))
                RecordError(ref errors, $"Source bake manifest is missing sourceGridMarginPreview: {sourceBakeManifestPath}");
            else if (!ProjectFileExists(previewPath))
                RecordError(ref errors, $"Source grid margin preview is missing: {previewPath}");

            if (sourceManifest.reviewCount > 0)
                RecordError(ref errors, $"Source bake manifest has review items. reviewCount={sourceManifest.reviewCount}, path={sourceBakeManifestPath}");

            if (sourceManifest.items == null)
                return;

            for (int i = 0; i < sourceManifest.items.Length; i++)
            {
                SourceBakeItem item = sourceManifest.items[i];
                if (item == null)
                    continue;

                if (!string.Equals(item.status, "OK", StringComparison.Ordinal))
                    RecordError(ref errors, $"Source bake item is not OK. index={item.index}, name='{item.name}', status='{item.status}'.");
            }
        }

        private static void ValidateAtlasManifest(AtlasManifest manifest, int rootPngCount, ref int errors, ref int warnings)
        {
            if (!AssetDatabase.IsValidFolder(CandidateRoot))
                RecordError(ref errors, $"Missing candidate folder: {CandidateRoot}");

            if (!ProjectFileExists(Atlas512Path))
                RecordError(ref errors, $"Missing atlas texture: {Atlas512Path}");

            if (!ProjectFileExists(Atlas256Path))
                RecordWarning(ref warnings, $"Missing scaled atlas preview: {Atlas256Path}");

            if (manifest == null)
                return;

            if (!string.Equals(NormalizeAssetPath(manifest.atlas), Atlas512Path, StringComparison.Ordinal))
                RecordError(ref errors, $"Manifest atlas path mismatch. expected='{Atlas512Path}', actual='{manifest.atlas}'.");

            if (!string.Equals(NormalizeAssetPath(manifest.source), CandidateRoot, StringComparison.Ordinal))
                RecordError(ref errors, $"Manifest source path mismatch. expected='{CandidateRoot}', actual='{manifest.source}'.");

            if (manifest.cellSizePx <= 0)
                RecordError(ref errors, $"Manifest cellSizePx must be positive. actual={manifest.cellSizePx}.");

            if (manifest.columns <= 0 || manifest.rows <= 0)
                RecordError(ref errors, $"Manifest grid must be positive. columns={manifest.columns}, rows={manifest.rows}.");

            if (manifest.entries == null || manifest.entries.Length == 0)
            {
                RecordError(ref errors, $"Manifest has no entries: {AtlasManifestPath}");
                return;
            }

            if (rootPngCount != manifest.entries.Length)
                RecordError(ref errors, $"Candidate PNG count does not match manifest entries. pngs={rootPngCount}, entries={manifest.entries.Length}.");

            Texture2D atlas = AssetDatabase.LoadAssetAtPath<Texture2D>(Atlas512Path);
            if (atlas != null && manifest.cellSizePx > 0 && manifest.columns > 0 && manifest.rows > 0)
            {
                int expectedWidth = manifest.columns * manifest.cellSizePx;
                int expectedHeight = manifest.rows * manifest.cellSizePx;
                if (atlas.width != expectedWidth || atlas.height != expectedHeight)
                    RecordError(ref errors, $"Atlas dimensions mismatch. expected={expectedWidth}x{expectedHeight}, actual={atlas.width}x{atlas.height}.");
            }
            else if (ProjectFileExists(Atlas512Path))
            {
                RecordWarning(ref warnings, $"Atlas texture exists but is not imported as Texture2D yet: {Atlas512Path}");
            }

            ValidateScaledAtlases(manifest, ref errors, ref warnings);

            HashSet<string> names = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < manifest.entries.Length; i++)
            {
                ValidateAtlasEntry(manifest, manifest.entries[i], i, names, atlas, ref errors, ref warnings);
            }
        }

        private static void ValidateScaledAtlases(AtlasManifest manifest, ref int errors, ref int warnings)
        {
            if (manifest.scaledAtlases == null)
                return;

            for (int i = 0; i < manifest.scaledAtlases.Length; i++)
            {
                string path = NormalizeAssetPath(manifest.scaledAtlases[i]);
                if (string.IsNullOrWhiteSpace(path))
                {
                    RecordWarning(ref warnings, $"Manifest scaledAtlases[{i}] is empty.");
                    continue;
                }

                if (!ProjectFileExists(path))
                {
                    RecordError(ref errors, $"Missing scaled atlas: {path}");
                    continue;
                }

                Texture2D atlas = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (atlas == null)
                    RecordWarning(ref warnings, $"Scaled atlas exists but is not imported as Texture2D yet: {path}");
            }
        }

        private static void ValidateAtlasEntry(
            AtlasManifest manifest,
            AtlasEntry entry,
            int index,
            HashSet<string> names,
            Texture2D atlas,
            ref int errors,
            ref int warnings)
        {
            if (entry == null)
            {
                RecordError(ref errors, $"Manifest entry[{index}] is null.");
                return;
            }

            string name = entry.name ?? string.Empty;
            if (string.IsNullOrWhiteSpace(name))
                RecordError(ref errors, $"Manifest entry[{index}] has empty name.");
            else if (!names.Add(name))
                RecordError(ref errors, $"Manifest entry name is duplicated: {name}");

            string source = NormalizeAssetPath(entry.source);
            if (string.IsNullOrWhiteSpace(source))
            {
                RecordError(ref errors, $"Manifest entry '{name}' has empty source path.");
            }
            else
            {
                if (!IsUnderPath(source, CandidateRoot))
                    RecordError(ref errors, $"Manifest entry '{name}' source is outside candidate root: {source}");

                if (!ProjectFileExists(source))
                    RecordError(ref errors, $"Manifest entry '{name}' source is missing: {source}");

                string sourceName = Path.GetFileNameWithoutExtension(source);
                if (!string.Equals(sourceName, name, StringComparison.Ordinal))
                    RecordError(ref errors, $"Manifest entry '{name}' source filename mismatch: {source}");
            }

            int[] rect = entry.atlas_rect_px;
            if (rect == null || rect.Length != 4)
            {
                RecordError(ref errors, $"Manifest entry '{name}' has invalid atlas_rect_px.");
                return;
            }

            int x = rect[0];
            int y = rect[1];
            int width = rect[2];
            int height = rect[3];
            if (x < 0 || y < 0 || width <= 0 || height <= 0)
                RecordError(ref errors, $"Manifest entry '{name}' rect must be positive and non-negative: [{x},{y},{width},{height}].");

            if (manifest.cellSizePx > 0 && (width != manifest.cellSizePx || height != manifest.cellSizePx))
                RecordError(ref errors, $"Manifest entry '{name}' rect size must match cellSizePx={manifest.cellSizePx}: [{x},{y},{width},{height}].");

            int atlasWidth = atlas != null ? atlas.width : manifest.columns * manifest.cellSizePx;
            int atlasHeight = atlas != null ? atlas.height : manifest.rows * manifest.cellSizePx;
            if (atlasWidth > 0 && atlasHeight > 0 && (x + width > atlasWidth || y + height > atlasHeight))
                RecordError(ref errors, $"Manifest entry '{name}' rect exceeds atlas bounds: [{x},{y},{width},{height}] atlas={atlasWidth}x{atlasHeight}.");

            if (entry.touches_cell_edge)
                RecordError(ref errors, $"Manifest entry '{name}' touches its cell edge.");
        }

        private static void ValidateBindingMap(AtlasManifest manifest, ref int errors, ref int warnings)
        {
            if (!ProjectFileExists(BindingMapPath))
            {
                RecordError(ref errors, $"Missing binding map: {BindingMapPath}");
                return;
            }

            IconBindingMap map;
            try
            {
                map = ReadJsonFileCapped<IconBindingMap>(BindingMapPath, MaxManifestBytes, "Binding Map");
            }
            catch (Exception exception)
            {
                RecordError(ref errors, $"Invalid binding map JSON: {BindingMapPath}. {exception.Message}");
                return;
            }

            if (map == null || map.bindings == null || map.bindings.Length == 0)
            {
                RecordError(ref errors, $"Binding map has no entries: {BindingMapPath}");
                return;
            }

            for (int i = 0; i < map.bindings.Length; i++)
            {
                ValidateBinding(manifest, in map.bindings[i], i, ref errors, ref warnings);
            }
        }

        private static void ValidateBinding(AtlasManifest manifest, in IconBinding binding, int index, ref int errors, ref int warnings)
        {
            string itemAsset = NormalizeAssetPath(binding.itemAsset);
            string spriteAsset = NormalizeAssetPath(binding.spriteAsset);
            string persistentId = binding.persistentId ?? string.Empty;

            if (string.IsNullOrWhiteSpace(spriteAsset))
            {
                if (binding.enabled)
                    RecordError(ref errors, $"Binding[{index}] is enabled but has no spriteAsset.");
            }
            else
            {
                if (!IsKnownGeneratedSpriteAsset(spriteAsset))
                    RecordError(ref errors, $"Binding[{index}] spriteAsset is outside Batch30 generated assets: {spriteAsset}");

                if (!ProjectFileExists(spriteAsset))
                    RecordError(ref errors, $"Binding[{index}] spriteAsset is missing: {spriteAsset}");

                if (string.Equals(spriteAsset, Atlas512Path, StringComparison.Ordinal) &&
                    string.IsNullOrWhiteSpace(binding.spriteName))
                {
                    RecordError(ref errors, $"Binding[{index}] uses the atlas asset but has no spriteName.");
                }

                if (binding.enabled && IsUnderPath(spriteAsset, CandidateRoot) && !ManifestContainsSource(manifest, spriteAsset))
                    RecordError(ref errors, $"Binding[{index}] spriteAsset is not listed in the atlas manifest: {spriteAsset}");
            }

            if (!binding.enabled)
            {
                if (!string.IsNullOrWhiteSpace(itemAsset) || !string.IsNullOrWhiteSpace(persistentId))
                    RecordWarning(ref warnings, $"Binding[{index}] is disabled but still has itemAsset or persistentId.");
                return;
            }

            if (string.IsNullOrWhiteSpace(itemAsset))
            {
                RecordError(ref errors, $"Binding[{index}] is enabled but has no itemAsset.");
                return;
            }

            if (!BindingIsApproved(in binding))
                RecordError(ref errors, $"Binding[{index}] is enabled but not visually approved. persistentId='{persistentId}', spriteAsset='{spriteAsset}'.");

            if (string.IsNullOrWhiteSpace(persistentId))
                RecordError(ref errors, $"Binding[{index}] is enabled but has no persistentId guard.");

            ItemData item = AssetDatabase.LoadAssetAtPath<ItemData>(itemAsset);
            if (item == null)
            {
                RecordError(ref errors, $"Binding[{index}] itemAsset is missing or not ItemData: {itemAsset}");
                return;
            }

            if (!string.IsNullOrWhiteSpace(persistentId) && item.PersistentId != persistentId)
            {
                RecordError(
                    ref errors,
                    $"Binding[{index}] persistentId mismatch for '{itemAsset}'. expected='{persistentId}', actual='{item.PersistentId}'.");
            }
        }

        private static bool TryBindIcon(in IconBinding binding, bool allowOverwrite)
        {
            string itemAsset = NormalizeAssetPath(binding.itemAsset);
            string spriteAsset = NormalizeAssetPath(binding.spriteAsset);
            if (string.IsNullOrWhiteSpace(itemAsset) || string.IsNullOrWhiteSpace(spriteAsset))
            {
                Debug.LogWarning("[InventoryIconCandidateImporter] Binding missing itemAsset or spriteAsset.");
                return false;
            }

            ItemData item = AssetDatabase.LoadAssetAtPath<ItemData>(itemAsset);
            Sprite sprite = ResolveSprite(spriteAsset, binding.spriteName);
            if (item == null || sprite == null)
            {
                Debug.LogWarning($"[InventoryIconCandidateImporter] Binding failed. item='{itemAsset}', sprite='{spriteAsset}', spriteName='{binding.spriteName}'.");
                return false;
            }

            if (!string.IsNullOrWhiteSpace(binding.persistentId) && item.PersistentId != binding.persistentId)
            {
                Debug.LogWarning($"[InventoryIconCandidateImporter] PersistentId mismatch for '{itemAsset}'. expected='{binding.persistentId}', actual='{item.PersistentId}'.");
                return false;
            }

            if (!BindingIsApproved(in binding))
            {
                Debug.LogWarning(
                    $"[InventoryIconCandidateImporter] Refusing to bind visually unapproved inventory icon. " +
                    $"item='{itemAsset}', persistentId='{binding.persistentId}', sprite='{spriteAsset}'.");
                return false;
            }

            if (SpriteMatchesBinding(item.icon, in binding))
                return false;

            if (item.icon != null && !allowOverwrite)
            {
                Debug.LogWarning(
                    $"[InventoryIconCandidateImporter] Refusing to overwrite existing item icon without -h8InventoryIconAllowOverwrite. " +
                    $"item='{itemAsset}', actual='{DescribeSprite(item.icon)}', expected='{spriteAsset}', expectedSpriteName='{binding.spriteName}'.");
                return false;
            }

            item.icon = sprite;
            EditorUtility.SetDirty(item);
            return true;
        }

        private static Sprite ResolveSprite(string spriteAssetPath, string spriteName)
        {
            spriteAssetPath = NormalizeAssetPath(spriteAssetPath);
            if (string.IsNullOrWhiteSpace(spriteName))
                return AssetDatabase.LoadAssetAtPath<Sprite>(spriteAssetPath);

            UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(spriteAssetPath);
            for (int i = 0; i < assets.Length; i++)
            {
                if (assets[i] is Sprite sprite && sprite.name == spriteName)
                    return sprite;
            }

            return null;
        }

        private static bool TryLoadBindingMap(out IconBindingMap map)
        {
            return TryLoadBindingMap(BindingMapPath, out map);
        }

        private static bool TryLoadBindingMap(string bindingMapPath, out IconBindingMap map)
        {
            map = null;
            bindingMapPath = NormalizeAssetPath(bindingMapPath);
            if (!ProjectFileExists(bindingMapPath))
            {
                Debug.LogWarning($"[InventoryIconCandidateImporter] Missing binding map: {bindingMapPath}");
                return false;
            }

            try
            {
                map = ReadJsonFileCapped<IconBindingMap>(bindingMapPath, MaxManifestBytes, "Binding Map");
            }
            catch (Exception exception)
            {
                Debug.LogError($"[InventoryIconCandidateImporter] Invalid binding map JSON: {bindingMapPath}. {exception.Message}");
                return false;
            }

            if (map == null || map.bindings == null || map.bindings.Length == 0)
            {
                Debug.LogWarning($"[InventoryIconCandidateImporter] Binding map has no entries: {bindingMapPath}");
                return false;
            }

            return true;
        }

        private static int PrepareSpritesFromBindingMap(IconBindingMap map, int maxTextureSize)
        {
            if (map == null || map.bindings == null)
                return 0;

            int changed = 0;
            HashSet<string> prepared = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < map.bindings.Length; i++)
            {
                if (!map.bindings[i].enabled)
                    continue;

                string spriteAsset = NormalizeAssetPath(map.bindings[i].spriteAsset);
                if (string.IsNullOrWhiteSpace(spriteAsset) || !prepared.Add(spriteAsset))
                    continue;

                changed += PrepareSingleSprite(spriteAsset, maxTextureSize);
            }

            return changed;
        }

        private static int PrepareAtlasesFromBindingMap(string bindingMapPath)
        {
            string generatedRoot = NormalizeAssetPath(Path.GetDirectoryName(bindingMapPath) ?? string.Empty);
            int errors = 0;
            int warnings = 0;
            AtlasManifest manifest = LoadGenericAtlasManifest(generatedRoot, ref errors, ref warnings);
            if (manifest == null || errors > 0)
                return 0;

            int changed = 0;
            string atlas = NormalizeAssetPath(manifest.atlas);
            if (!string.IsNullOrWhiteSpace(atlas))
                changed += PrepareSingleSprite(atlas, ExpectedAtlasMaxTextureSize(atlas, manifest.cellSizePx));

            if (manifest.scaledAtlases == null)
                return changed;

            for (int i = 0; i < manifest.scaledAtlases.Length; i++)
            {
                string scaled = NormalizeAssetPath(manifest.scaledAtlases[i]);
                if (string.IsNullOrWhiteSpace(scaled))
                    continue;

                changed += PrepareSingleSprite(scaled, ExpectedAtlasMaxTextureSize(scaled, manifest.cellSizePx));
            }

            return changed;
        }

        private static int ExpectedAtlasMaxTextureSize(string path, int cellSizePx)
        {
            if (path.IndexOf("_256xCells", StringComparison.OrdinalIgnoreCase) >= 0)
                return 2048;

            if (path.IndexOf("_512xCells", StringComparison.OrdinalIgnoreCase) >= 0)
                return 4096;

            return Mathf.Clamp(Mathf.Max(512, cellSizePx * 8), 512, 4096);
        }

        private static int BindIconMap(IconBindingMap map, bool allowOverwrite, out int skipped)
        {
            skipped = 0;
            if (map == null || map.bindings == null)
                return 0;

            int changed = 0;
            for (int i = 0; i < map.bindings.Length; i++)
            {
                IconBinding binding = map.bindings[i];
                if (!binding.enabled)
                {
                    skipped++;
                    continue;
                }

                if (!TryBindIcon(in binding, allowOverwrite))
                {
                    skipped++;
                    continue;
                }

                changed++;
            }

            return changed;
        }

        private static int ValidateGenericBindingMap(IconBindingMap map, string bindingMapPath, bool allowOverwrite, out int warnings)
        {
            warnings = 0;
            int errors = 0;
            if (map == null || map.bindings == null || map.bindings.Length == 0)
            {
                RecordError(ref errors, $"Binding map has no entries: {bindingMapPath}");
                return errors;
            }

            HashSet<string> persistentIds = new HashSet<string>(StringComparer.Ordinal);
            HashSet<string> itemAssets = new HashSet<string>(StringComparer.Ordinal);
            HashSet<string> spriteAssets = new HashSet<string>(StringComparer.Ordinal);
            string generatedRoot = NormalizeAssetPath(Path.GetDirectoryName(bindingMapPath) ?? string.Empty);
            string alphaRoot = generatedRoot + "/Alpha512";
            string atlasRoot = generatedRoot + "/Atlas";
            AtlasManifest manifest = LoadGenericAtlasManifest(generatedRoot, ref errors, ref warnings);
            for (int i = 0; i < map.bindings.Length; i++)
            {
                IconBinding binding = map.bindings[i];
                string itemAsset = NormalizeAssetPath(binding.itemAsset);
                string spriteAsset = NormalizeAssetPath(binding.spriteAsset);
                string persistentId = binding.persistentId ?? string.Empty;

                if (!binding.enabled)
                    continue;

                if (!BindingIsApproved(in binding))
                    RecordError(ref errors, $"Binding[{i}] is enabled but not visually approved. persistentId='{persistentId}', spriteAsset='{spriteAsset}'.");

                if (string.IsNullOrWhiteSpace(itemAsset))
                    RecordError(ref errors, $"Binding[{i}] is enabled but has no itemAsset.");
                else if (!itemAssets.Add(itemAsset))
                    RecordError(ref errors, $"Binding[{i}] duplicates itemAsset: {itemAsset}");

                if (string.IsNullOrWhiteSpace(spriteAsset))
                {
                    RecordError(ref errors, $"Binding[{i}] is enabled but has no spriteAsset.");
                }
                else
                {
                    if (!spriteAssets.Add(spriteAsset))
                        RecordError(ref errors, $"Binding[{i}] duplicates spriteAsset: {spriteAsset}");

                    if (!IsUnderPath(spriteAsset, alphaRoot) && !IsUnderPath(spriteAsset, atlasRoot))
                        RecordError(ref errors, $"Binding[{i}] spriteAsset is outside generated binding-map roots: {spriteAsset}");

                    if (!ProjectFileExists(spriteAsset))
                        RecordError(ref errors, $"Binding[{i}] spriteAsset is missing: {spriteAsset}");

                    if (IsUnderPath(spriteAsset, alphaRoot) && !ManifestContainsSource(manifest, spriteAsset))
                        RecordError(ref errors, $"Binding[{i}] spriteAsset is not listed in atlas manifest: {spriteAsset}");
                }

                if (string.IsNullOrWhiteSpace(persistentId))
                    RecordError(ref errors, $"Binding[{i}] is enabled but has no persistentId guard.");
                else if (!persistentIds.Add(persistentId))
                    RecordError(ref errors, $"Binding[{i}] duplicates persistentId: {persistentId}");

                if (string.IsNullOrWhiteSpace(itemAsset))
                    continue;

                ItemData item = AssetDatabase.LoadAssetAtPath<ItemData>(itemAsset);
                if (item == null)
                {
                    RecordError(ref errors, $"Binding[{i}] itemAsset is missing or not ItemData: {itemAsset}");
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(persistentId) && item.PersistentId != persistentId)
                {
                    RecordError(
                        ref errors,
                        $"Binding[{i}] persistentId mismatch for '{itemAsset}'. expected='{persistentId}', actual='{item.PersistentId}'.");
                }

                if (item.icon != null && !SpriteMatchesBinding(item.icon, in binding))
                {
                    string message =
                        $"Binding[{i}] would overwrite existing item icon. item='{itemAsset}', " +
                        $"actual='{DescribeSprite(item.icon)}', expected='{spriteAsset}', expectedSpriteName='{binding.spriteName}'.";
                    if (allowOverwrite)
                        RecordWarning(ref warnings, message);
                    else
                        RecordError(ref errors, message);
                }
            }

            return errors;
        }

        private static int ValidateBoundIconMap(out int checkedCount, out int skipped)
        {
            checkedCount = 0;
            skipped = 0;
            if (!TryLoadBindingMap(out IconBindingMap map))
                return 1;

            return ValidateBoundIconMap(map, out checkedCount, out skipped);
        }

        private static int ValidateBoundIconMap(IconBindingMap map, out int checkedCount, out int skipped)
        {
            checkedCount = 0;
            skipped = 0;
            if (map == null || map.bindings == null)
                return 1;

            int errors = 0;
            for (int i = 0; i < map.bindings.Length; i++)
            {
                IconBinding binding = map.bindings[i];
                if (!binding.enabled)
                {
                    skipped++;
                    continue;
                }

                checkedCount++;
                ValidateBoundIcon(in binding, i, ref errors);
            }

            return errors;
        }

        private static void ValidateBoundIcon(in IconBinding binding, int index, ref int errors)
        {
            string itemAsset = NormalizeAssetPath(binding.itemAsset);
            string spriteAsset = NormalizeAssetPath(binding.spriteAsset);
            ItemData item = AssetDatabase.LoadAssetAtPath<ItemData>(itemAsset);
            Sprite expected = ResolveSprite(spriteAsset, binding.spriteName);
            if (item == null)
            {
                RecordError(ref errors, $"Binding[{index}] itemAsset is missing or not ItemData: {itemAsset}");
                return;
            }

            if (!string.IsNullOrWhiteSpace(binding.persistentId) && item.PersistentId != binding.persistentId)
            {
                RecordError(
                    ref errors,
                    $"Binding[{index}] persistentId mismatch for '{itemAsset}'. expected='{binding.persistentId}', actual='{item.PersistentId}'.");
                return;
            }

            if (expected == null)
            {
                RecordError(ref errors, $"Binding[{index}] expected sprite is not imported/resolvable: {spriteAsset}");
                return;
            }

            if (item.icon == null)
            {
                RecordError(ref errors, $"Binding[{index}] item icon is still empty: {itemAsset}");
                return;
            }

            if (!SpriteMatchesBinding(item.icon, in binding))
            {
                RecordError(
                    ref errors,
                    $"Binding[{index}] item icon points elsewhere. item='{itemAsset}', actual='{DescribeSprite(item.icon)}', expected='{spriteAsset}', expectedSpriteName='{binding.spriteName}'.");
            }
        }

        private static bool SpriteMatchesBinding(Sprite sprite, in IconBinding binding)
        {
            if (sprite == null)
                return false;

            string actualPath = NormalizeAssetPath(AssetDatabase.GetAssetPath(sprite));
            string expectedPath = NormalizeAssetPath(binding.spriteAsset);
            if (!string.Equals(actualPath, expectedPath, StringComparison.Ordinal))
                return false;

            return string.IsNullOrWhiteSpace(binding.spriteName) ||
                   string.Equals(sprite.name, binding.spriteName, StringComparison.Ordinal);
        }

        private static bool BindingIsApproved(in IconBinding binding)
        {
            bool approved = binding.approved ||
                            string.Equals(binding.reviewStatus, "APPROVED", StringComparison.OrdinalIgnoreCase);
            return approved && BindingHasReviewMetadata(in binding);
        }

        private static bool BindingHasReviewMetadata(in IconBinding binding)
        {
            return !string.IsNullOrWhiteSpace(binding.reviewedBy) &&
                   !string.IsNullOrWhiteSpace(binding.reviewedAt) &&
                   !string.IsNullOrWhiteSpace(binding.reviewNote);
        }

        private static string DescribeSprite(Sprite sprite)
        {
            if (sprite == null)
                return "<null>";

            return AssetDatabase.GetAssetPath(sprite) + "#" + sprite.name;
        }

        private static bool ManifestContainsSource(AtlasManifest manifest, string spriteAsset)
        {
            if (manifest == null || manifest.entries == null)
                return true;

            string normalized = NormalizeAssetPath(spriteAsset);
            for (int i = 0; i < manifest.entries.Length; i++)
            {
                AtlasEntry entry = manifest.entries[i];
                if (entry != null && string.Equals(NormalizeAssetPath(entry.source), normalized, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        private static bool IsKnownGeneratedSpriteAsset(string path)
        {
            return IsUnderPath(path, CandidateRoot) ||
                   string.Equals(path, Atlas512Path, StringComparison.Ordinal) ||
                   string.Equals(path, Atlas256Path, StringComparison.Ordinal);
        }

        private static bool IsUnderPath(string path, string root)
        {
            string normalizedPath = NormalizeAssetPath(path);
            string normalizedRoot = NormalizeAssetPath(root).TrimEnd('/');
            return normalizedPath.StartsWith(normalizedRoot + "/", StringComparison.Ordinal);
        }

        private static bool ProjectFileExists(string path)
        {
            return File.Exists(ToProjectFilePath(path));
        }

        private static string ReadTextFileCapped(string path, long maxBytes, string label)
        {
            string physicalPath = ToProjectFilePath(path);
            FileInfo info = new FileInfo(physicalPath);
            if (!info.Exists)
                throw new FileNotFoundException(label + " is missing.", physicalPath);

            if (maxBytes <= 0L || maxBytes > int.MaxValue - 1L)
                throw new InvalidDataException(label + " has invalid byte cap " + maxBytes + ".");

            if (info.Length > maxBytes)
                throw new InvalidDataException(label + " exceeds byte cap " + maxBytes + ".");

            return File.ReadAllText(physicalPath, System.Text.Encoding.UTF8);
        }

        private static T ReadJsonFileCapped<T>(string path, long maxBytes, string label)
        {
            return JsonUtility.FromJson<T>(ReadTextFileCapped(path, maxBytes, label));
        }

        private static string ReadProjectText(string path)
        {
            return File.ReadAllText(ToProjectFilePath(path));
        }

        private static string ToProjectFilePath(string path)
        {
            string normalized = NormalizeAssetPath(path);
            if (string.IsNullOrEmpty(normalized))
                return string.Empty;

            if (Path.IsPathRooted(normalized))
                return normalized;

            string dataPath = Application.dataPath.Replace('\\', '/').TrimEnd('/');
            string projectRoot = dataPath.EndsWith("/Assets", StringComparison.OrdinalIgnoreCase)
                ? dataPath.Substring(0, dataPath.Length - "/Assets".Length)
                : Directory.GetCurrentDirectory().Replace('\\', '/').TrimEnd('/');

            return Path.Combine(projectRoot, normalized.Replace('/', Path.DirectorySeparatorChar));
        }

        private static string NormalizeAssetPath(string path)
        {
            string normalized = (path ?? string.Empty).Replace('\\', '/').Trim();
            if (string.IsNullOrEmpty(normalized))
                return string.Empty;

            string dataPath = Application.dataPath.Replace('\\', '/').TrimEnd('/');
            if (normalized.StartsWith(dataPath + "/", StringComparison.OrdinalIgnoreCase))
                return "Assets/" + normalized.Substring(dataPath.Length + 1);

            string projectRoot = dataPath.EndsWith("/Assets", StringComparison.OrdinalIgnoreCase)
                ? dataPath.Substring(0, dataPath.Length - "/Assets".Length)
                : string.Empty;
            if (!string.IsNullOrEmpty(projectRoot) &&
                normalized.StartsWith(projectRoot + "/", StringComparison.OrdinalIgnoreCase))
            {
                return normalized.Substring(projectRoot.Length + 1);
            }

            return normalized;
        }

        private static string ReadCommandLineValue(string key)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (string.Equals(args[i], key, StringComparison.Ordinal))
                    return args[i + 1];
            }

            return string.Empty;
        }

        private static int ReadCommandLineInt(string key, int fallback)
        {
            string raw = ReadCommandLineValue(key);
            return int.TryParse(raw, out int parsed) && parsed > 0 ? parsed : fallback;
        }

        private static bool ReadCommandLineBool(string key)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (!string.Equals(args[i], key, StringComparison.Ordinal))
                    continue;

                if (i + 1 >= args.Length || args[i + 1].StartsWith("-", StringComparison.Ordinal))
                    return true;

                string raw = args[i + 1];
                return string.Equals(raw, "1", StringComparison.Ordinal) ||
                       string.Equals(raw, "true", StringComparison.OrdinalIgnoreCase) ||
                       string.Equals(raw, "yes", StringComparison.OrdinalIgnoreCase);
            }

            return false;
        }

        private static void RecordError(ref int errors, string message)
        {
            errors++;
            Debug.LogError("[InventoryIconCandidateImporter] " + message);
        }

        private static void RecordWarning(ref int warnings, string message)
        {
            warnings++;
            Debug.LogWarning("[InventoryIconCandidateImporter] " + message);
        }

        [Serializable]
        private sealed class AtlasManifest
        {
            public string atlas;
            public int cellSizePx;
            public int columns;
            public AtlasEntry[] entries;
            public int rows;
            public string[] scaledAtlases;
            public string source;
            public string sourceBakeManifest;
        }

        [Serializable]
        private sealed class AtlasEntry
        {
            public string name;
            public int[] atlas_rect_px;
            public string source;
            public bool touches_cell_edge;
        }

        [Serializable]
        private sealed class IconBindingMap
        {
            public IconBinding[] bindings;
        }

        [Serializable]
        private sealed class SourceBakeManifest
        {
            public string sourceGridMarginPreview;
            public int reviewCount;
            public SourceBakeItem[] items;
        }

        [Serializable]
        private sealed class SourceBakeItem
        {
            public int index;
            public string name;
            public string status;
        }

        [Serializable]
        private struct IconBinding
        {
            public bool enabled;
            public bool approved;
            public string reviewStatus;
            public string reviewedBy;
            public string reviewedAt;
            public string reviewNote;
            public string persistentId;
            public string itemAsset;
            public string spriteAsset;
            public string spriteName;
            public string note;
        }
    }
}
