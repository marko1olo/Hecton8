#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Hecton8.Data;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

namespace Hecton8.EditorValidation
{
    /// <summary>
    /// Editor compiler that bakes authored CSV/JSON source data into one Data Monolith blob.
    /// </summary>
    public static unsafe class H8DataMonolithCompiler
    {
        internal const string SourceFolder = "Assets/_SourceData";
        internal const string BalanceSourceFolder = "Data/Balance";
        internal const string OutputAssetPath = "Assets/StreamingAssets/Hecton8/DataMonolith/static_data.h8bin";
        private const string MenuPath = "Hecton8/Data Monolith/Bake Static Data";
        private const int InitialBlobCapacity = 128 * 1024;
        private const int Utf8ScratchBytes = 2048;

        internal static string LastError;

        private static readonly H8DataSectionId[] SectionOrder =
        {
            H8DataSectionId.Items,
            H8DataSectionId.Creatures,
            H8DataSectionId.Biomes,
            H8DataSectionId.Recipes,
            H8DataSectionId.BiomeHeatmap,
            H8DataSectionId.QuestNodes,
            H8DataSectionId.QuestEdges,
            H8DataSectionId.LootCdf,
            H8DataSectionId.VoxelMaterials,
            H8DataSectionId.AudioClipRegistry,
            H8DataSectionId.VfxScalars,
            H8DataSectionId.DepthPressureCurve,
            H8DataSectionId.ToolHeatCapacity,
            H8DataSectionId.SubmarineHullConstants,
            H8DataSectionId.NarrativeTriggers,
            H8DataSectionId.PhysicsMaterials,
            H8DataSectionId.GhostModules,
            H8DataSectionId.RadiationIntensityMap,
            H8DataSectionId.SpawnCreditCosts,
            H8DataSectionId.LightAttenuationCurve,
            H8DataSectionId.SopErrors,
            H8DataSectionId.HudLayouts,
            H8DataSectionId.LocalizationUtf8,
            H8DataSectionId.SectorPageDirectory,
            H8DataSectionId.Economy,
            H8DataSectionId.PhysicsConstants
        };

        [MenuItem(MenuPath)]
        public static void BakeFromMenu()
        {
            BakeAll(logSummary: true);
        }

        internal static bool BakeAll(bool logSummary)
        {
            LastError = string.Empty;
            try
            {
                if (!H8DataLayoutAudit.ValidateBlittableSizes())
                {
                    LastError = "Blittable layout audit failed.";
                    Debug.LogError("[H8DataMonolithCompiler] " + LastError + " Bake aborted.");
                    return false;
                }

                EnsureLittleEndianEditorHost();
                Directory.CreateDirectory(SourceFolder);
                Directory.CreateDirectory(BalanceSourceFolder);
                Directory.CreateDirectory(Path.GetDirectoryName(OutputAssetPath));

                DataSet dataSet = new DataSet();
                LocalizationPool localizationPool = new LocalizationPool();

                string[] csvFiles = CollectSourceFiles("*.csv");
                Array.Sort(csvFiles, StringComparer.OrdinalIgnoreCase);
                CsvFileRows[] csvSources = ReadCsvSourcesParallel(csvFiles);
                for (int i = 0; i < csvSources.Length; i++)
                    ParseCsv(csvSources[i], dataSet, localizationPool);

                string[] jsonFiles = CollectSourceFiles("*.json");
                Array.Sort(jsonFiles, StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < jsonFiles.Length; i++)
                    ParseJson(jsonFiles[i], dataSet, localizationPool);

                FinalizeGeneratedTables(dataSet);
                ValidateCrossReferences(dataSet);

                byte[] blob = BuildBlob(dataSet, localizationPool);
                File.WriteAllBytes(OutputAssetPath, blob);
                AssetDatabase.ImportAsset(OutputAssetPath, ImportAssetOptions.ForceUpdate);

                H8DataMonolithHotReloadSocket.NotifyBake(OutputAssetPath);
                if (logSummary)
                {
                    Debug.Log(
                        "[H8DataMonolithCompiler] Baked Data Monolith: bytes=" +
                        blob.Length +
                        ", items=" +
                        dataSet.Items.Count +
                        ", creatures=" +
                        dataSet.Creatures.Count +
                        ", biomes=" +
                        dataSet.Biomes.Count +
                        ", economy=" +
                        dataSet.Economy.Count +
                        ", physics=" +
                        dataSet.PhysicsConstants.Count +
                        ", sections=" +
                        SectionOrder.Length +
                        ".");
                }

                return true;
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
                Debug.LogException(ex);
                return false;
            }
        }

        internal static bool IsSourcePath(string assetPath)
        {
            return !string.IsNullOrEmpty(assetPath) &&
                   !IsGeneratedBalancePath(assetPath) &&
                   (IsUnderAbsoluteRoot(assetPath, SourceFolder) ||
                    IsUnderAbsoluteRoot(assetPath, BalanceSourceFolder));
        }

        private static string[] CollectSourceFiles(string searchPattern)
        {
            List<string> files = new List<string>(128); // COLD ALLOC: List<string>[source file count] - editor-only source enumeration - owner: H8DataMonolithCompiler
            AppendSourceFiles(files, SourceFolder, searchPattern);
            AppendSourceFiles(files, BalanceSourceFolder, searchPattern);
            return files.ToArray();
        }

        private static CsvFileRows[] ReadCsvSourcesParallel(string[] csvFiles)
        {
            CsvFileRows[] results = new CsvFileRows[csvFiles.Length]; // COLD ALLOC: CsvFileRows[source file count] - editor-only parallel CSV import results - owner: H8DataMonolithCompiler
            Task[] workers = new Task[csvFiles.Length]; // COLD ALLOC: Task[source file count] - editor-only CSV import workers - owner: H8DataMonolithCompiler
            for (int i = 0; i < csvFiles.Length; i++)
            {
                int workerIndex = i;
                workers[i] = Task.Run(() =>
                {
                    string path = csvFiles[workerIndex];
                    results[workerIndex] = new CsvFileRows(path, ReadCsvRows(path));
                });
            }

            Task.WaitAll(workers);
            return results;
        }

        private static void AppendSourceFiles(List<string> files, string relativeFolder, string searchPattern)
        {
            string absoluteFolder = Path.GetFullPath(relativeFolder);
            if (!Directory.Exists(absoluteFolder))
                return;

            string[] discovered = Directory.GetFiles(absoluteFolder, searchPattern, SearchOption.AllDirectories);
            for (int i = 0; i < discovered.Length; i++)
            {
                if (!IsGeneratedBalancePath(discovered[i]))
                    files.Add(discovered[i]);
            }
        }

        internal static bool IsGeneratedBalancePath(string path)
        {
            if (string.IsNullOrEmpty(path))
                return false;

            return IsUnderAbsoluteRoot(path, Path.Combine(BalanceSourceFolder, "Baked")) ||
                   IsUnderAbsoluteRoot(path, Path.Combine(BalanceSourceFolder, "Schemas"));
        }

        private static bool IsUnderAbsoluteRoot(string path, string relativeRoot)
        {
            string normalizedPath = Path.GetFullPath(path).Replace('\\', '/').TrimEnd('/');
            string normalizedRoot = Path.GetFullPath(relativeRoot).Replace('\\', '/').TrimEnd('/');
            return normalizedPath.StartsWith(normalizedRoot + "/", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(normalizedPath, normalizedRoot, StringComparison.OrdinalIgnoreCase);
        }

        private static byte[] BuildBlob(DataSet dataSet, LocalizationPool localizationPool)
        {
            H8DataSectionEntry[] entries = new H8DataSectionEntry[SectionOrder.Length]; // COLD ALLOC: H8DataSectionEntry[section count] - editor-only section table patch scratch - owner: H8DataMonolithCompiler
            using MemoryStream stream = new MemoryStream(InitialBlobCapacity);
            int sectionTableOffset = H8DataLayoutConstants.HeaderSizeBytes + H8DataLayoutConstants.DirectorySizeBytes;
            int sectionTableBytes = SectionOrder.Length * UnsafeUtility.SizeOf<H8DataSectionEntry>();
            WriteZeros(stream, sectionTableOffset + sectionTableBytes);

            for (int i = 0; i < SectionOrder.Length; i++)
            {
                H8DataSectionId sectionId = SectionOrder[i];
                switch (sectionId)
                {
                    case H8DataSectionId.Items:
                        entries[i] = AppendSection(stream, sectionId, dataSet.Items, H8DataLayoutConstants.ItemRecordSize);
                        break;
                    case H8DataSectionId.Creatures:
                        entries[i] = AppendSection(stream, sectionId, dataSet.Creatures, H8DataLayoutConstants.CreatureTraitRecordSize);
                        break;
                    case H8DataSectionId.Biomes:
                        entries[i] = AppendSection(stream, sectionId, dataSet.Biomes, H8DataLayoutConstants.BiomeRecordSize);
                        break;
                    case H8DataSectionId.Recipes:
                        entries[i] = AppendSection(stream, sectionId, dataSet.Recipes, UnsafeUtility.SizeOf<H8RecipeRecord>());
                        break;
                    case H8DataSectionId.BiomeHeatmap:
                        entries[i] = AppendSection(stream, sectionId, dataSet.BiomeHeatmap, UnsafeUtility.SizeOf<H8BiomeHeatmapCellRecord>());
                        break;
                    case H8DataSectionId.QuestNodes:
                        entries[i] = AppendSection(stream, sectionId, dataSet.QuestNodes, UnsafeUtility.SizeOf<H8QuestNodeRecord>());
                        break;
                    case H8DataSectionId.QuestEdges:
                        entries[i] = AppendSection(stream, sectionId, dataSet.QuestEdges, UnsafeUtility.SizeOf<H8QuestEdgeRecord>());
                        break;
                    case H8DataSectionId.LootCdf:
                        entries[i] = AppendSection(stream, sectionId, dataSet.LootCdf, UnsafeUtility.SizeOf<H8LootCdfRecord>());
                        break;
                    case H8DataSectionId.VoxelMaterials:
                        entries[i] = AppendSection(stream, sectionId, dataSet.VoxelMaterials, UnsafeUtility.SizeOf<H8VoxelMaterialRecord>());
                        break;
                    case H8DataSectionId.AudioClipRegistry:
                        entries[i] = AppendSection(stream, sectionId, dataSet.AudioClips, UnsafeUtility.SizeOf<H8AudioClipRegistryRecord>());
                        break;
                    case H8DataSectionId.VfxScalars:
                        entries[i] = AppendSection(stream, sectionId, dataSet.VfxScalars, UnsafeUtility.SizeOf<H8VfxScalarRecord>());
                        break;
                    case H8DataSectionId.DepthPressureCurve:
                        entries[i] = AppendSection(stream, sectionId, dataSet.DepthPressureCurve, UnsafeUtility.SizeOf<H8DepthPressureSampleRecord>());
                        break;
                    case H8DataSectionId.ToolHeatCapacity:
                        entries[i] = AppendSection(stream, sectionId, dataSet.ToolHeat, UnsafeUtility.SizeOf<H8ToolHeatCapacityRecord>());
                        break;
                    case H8DataSectionId.SubmarineHullConstants:
                        entries[i] = AppendSection(stream, sectionId, dataSet.HullConstants, UnsafeUtility.SizeOf<H8SubmarineHullConstantRecord>());
                        break;
                    case H8DataSectionId.NarrativeTriggers:
                        entries[i] = AppendSection(stream, sectionId, dataSet.NarrativeTriggers, UnsafeUtility.SizeOf<H8NarrativeTriggerRecord>());
                        break;
                    case H8DataSectionId.PhysicsMaterials:
                        entries[i] = AppendSection(stream, sectionId, dataSet.PhysicsMaterials, UnsafeUtility.SizeOf<H8PhysicsMaterialRecord>());
                        break;
                    case H8DataSectionId.GhostModules:
                        entries[i] = AppendSection(stream, sectionId, dataSet.GhostModules, UnsafeUtility.SizeOf<H8GhostModuleRecord>());
                        break;
                    case H8DataSectionId.RadiationIntensityMap:
                        entries[i] = AppendSection(stream, sectionId, dataSet.RadiationCells, UnsafeUtility.SizeOf<H8RadiationIntensityCellRecord>());
                        break;
                    case H8DataSectionId.SpawnCreditCosts:
                        entries[i] = AppendSection(stream, sectionId, dataSet.SpawnCredits, UnsafeUtility.SizeOf<H8SpawnCreditCostRecord>());
                        break;
                    case H8DataSectionId.LightAttenuationCurve:
                        entries[i] = AppendSection(stream, sectionId, dataSet.LightAttenuationCurve, UnsafeUtility.SizeOf<H8LightAttenuationSampleRecord>());
                        break;
                    case H8DataSectionId.SopErrors:
                        entries[i] = AppendSection(stream, sectionId, dataSet.SopErrors, UnsafeUtility.SizeOf<H8SopErrorRecord>());
                        break;
                    case H8DataSectionId.HudLayouts:
                        entries[i] = AppendSection(stream, sectionId, dataSet.HudLayouts, UnsafeUtility.SizeOf<H8HudLayoutRecord>());
                        break;
                    case H8DataSectionId.LocalizationUtf8:
                        entries[i] = AppendLocalizationSection(stream, localizationPool);
                        break;
                    case H8DataSectionId.SectorPageDirectory:
                        entries[i] = AppendSection(stream, sectionId, dataSet.SectorPages, UnsafeUtility.SizeOf<H8SectorPageRecord>());
                        break;
                    case H8DataSectionId.Economy:
                        entries[i] = AppendSection(stream, sectionId, dataSet.Economy, H8DataLayoutConstants.EconomyRecordSize);
                        break;
                    case H8DataSectionId.PhysicsConstants:
                        entries[i] = AppendSection(stream, sectionId, dataSet.PhysicsConstants, H8DataLayoutConstants.PhysicsConstantsRecordSize);
                        break;
                }
            }

            Align16(stream);

            H8DataBlobDirectory directory = new H8DataBlobDirectory
            {
                Magic = H8DataLayoutConstants.BlobMagic,
                FormatVersion = H8DataLayoutConstants.FormatVersion,
                SectionCount = (ushort)SectionOrder.Length,
                SectionTableOffset = (uint)sectionTableOffset,
                SectionTableBytes = (uint)sectionTableBytes,
                BlobBytes = (uint)stream.Length,
                DataStartOffset = (uint)(sectionTableOffset + sectionTableBytes),
                WorldSeed = 0u,
                AppVersionHash = H8DataHash.ComputeFnv1A32(Application.version.AsSpan())
            };

            for (int i = 0; i < entries.Length; i++)
            {
                if (entries[i].SectionId == (uint)H8DataSectionId.LocalizationUtf8)
                {
                    directory.LocalizationOffset = entries[i].OffsetBytes;
                    directory.LocalizationBytes = entries[i].Count;
                    break;
                }
            }

            long previousPosition = stream.Position;
            stream.Position = H8DataLayoutConstants.HeaderSizeBytes;
            WriteDirectory(stream, in directory);
            stream.Position = sectionTableOffset;
            for (int i = 0; i < entries.Length; i++)
                WriteSectionEntry(stream, in entries[i]);
            stream.Position = previousPosition;

            byte[] blob = stream.ToArray();
            H8DataBlobHeader header = new H8DataBlobHeader
            {
                Magic = H8DataLayoutConstants.BlobMagic,
                FormatVersion = H8DataLayoutConstants.FormatVersion,
                HeaderBytes = H8DataLayoutConstants.HeaderSizeMarker,
                Checksum64 = ComputeHash64(blob, H8DataLayoutConstants.HeaderSizeBytes, blob.Length - H8DataLayoutConstants.HeaderSizeBytes)
            };

            WriteHeader(blob, in header);

            return blob;
        }

        private static void FinalizeGeneratedTables(DataSet dataSet)
        {
            dataSet.Items.Sort(CompareItemRecords);
            for (int i = 0; i < dataSet.Items.Count; i++)
            {
                H8ItemRecord record = dataSet.Items[i];
                record.RecordIndex = (uint)i;
                dataSet.Items[i] = record;
                if (i > 0 && dataSet.Items[i - 1].HashId == record.HashId)
                    throw new InvalidOperationException("[H8DataMonolithCompiler] Duplicate item hash detected: 0x" + record.HashId.ToString("X8"));
            }

            dataSet.Creatures.Sort(CompareCreatureRecords);
            for (int i = 0; i < dataSet.Creatures.Count; i++)
            {
                H8CreatureTraitRecord record = dataSet.Creatures[i];
                record.RecordIndex = (uint)i;
                dataSet.Creatures[i] = record;
                if (i > 0 && dataSet.Creatures[i - 1].SpeciesHash == record.SpeciesHash)
                    throw new InvalidOperationException("[H8DataMonolithCompiler] Duplicate creature hash detected: 0x" + record.SpeciesHash.ToString("X8"));
            }

            dataSet.Biomes.Sort(CompareBiomeRecords);
            for (int i = 0; i < dataSet.Biomes.Count; i++)
            {
                H8BiomeRecord record = dataSet.Biomes[i];
                record.RecordIndex = (uint)i;
                dataSet.Biomes[i] = record;
                if (i > 0 && dataSet.Biomes[i - 1].BiomeHash == record.BiomeHash)
                    throw new InvalidOperationException("[H8DataMonolithCompiler] Duplicate biome hash detected: 0x" + record.BiomeHash.ToString("X8"));
            }

            if (dataSet.DepthPressureCurve.Count == 0)
                GenerateDepthPressureCurve(dataSet.DepthPressureCurve);

            if (dataSet.LightAttenuationCurve.Count == 0)
                GenerateLightAttenuationCurve(dataSet.LightAttenuationCurve);

            NormalizeBiomeHeatmap(dataSet);
            RebuildLootCdf(dataSet);
            dataSet.VoxelMaterials.Sort(CompareVoxelMaterialRecords);
            dataSet.AudioClips.Sort(CompareAudioClipRecords);
            dataSet.HullConstants.Sort(CompareHullConstantRecords);
            dataSet.PhysicsMaterials.Sort(ComparePhysicsMaterialRecords);
            dataSet.Economy.Sort(CompareEconomyRecords);
            dataSet.PhysicsConstants.Sort(ComparePhysicsConstantsRecords);

            for (int i = 1; i < dataSet.Economy.Count; i++)
                if (dataSet.Economy[i - 1].HashId == dataSet.Economy[i].HashId)
                    throw new InvalidOperationException("[H8DataMonolithCompiler] Duplicate economy hash detected: 0x" + dataSet.Economy[i].HashId.ToString("X8"));

            for (int i = 1; i < dataSet.PhysicsConstants.Count; i++)
                if (dataSet.PhysicsConstants[i - 1].HashId == dataSet.PhysicsConstants[i].HashId)
                    throw new InvalidOperationException("[H8DataMonolithCompiler] Duplicate physics constant hash detected: 0x" + dataSet.PhysicsConstants[i].HashId.ToString("X8"));
        }

        private static void ValidateCrossReferences(DataSet dataSet)
        {
            HashSet<uint> itemHashes = new HashSet<uint>(); // COLD ALLOC: HashSet<uint>[item count] - editor-only cross-reference validation - owner: H8DataMonolithCompiler
            for (int i = 0; i < dataSet.Items.Count; i++)
                itemHashes.Add(dataSet.Items[i].HashId);

            for (int i = 0; i < dataSet.Recipes.Count; i++)
            {
                H8RecipeRecord recipe = dataSet.Recipes[i];
                if (recipe.OutputHash != 0u && !itemHashes.Contains(recipe.OutputHash))
                    ThrowBrokenReference("recipe.output", recipe.OutputHash);
                ValidateItemReference("recipe.ingredient0", recipe.IngredientHash0, itemHashes);
                ValidateItemReference("recipe.ingredient1", recipe.IngredientHash1, itemHashes);
                ValidateItemReference("recipe.ingredient2", recipe.IngredientHash2, itemHashes);
                ValidateItemReference("recipe.ingredient3", recipe.IngredientHash3, itemHashes);
            }

            for (int i = 0; i < dataSet.LootCdf.Count; i++)
                ValidateItemReference("loot.item", dataSet.LootCdf[i].ItemHash, itemHashes);

            for (int i = 0; i < dataSet.RawEconomyRows.Count; i++)
                ValidateEconomyItemReferences(dataSet.RawEconomyRows[i], itemHashes);
        }

        private static void ValidateItemReference(string owner, uint hash, HashSet<uint> itemHashes)
        {
            if (hash != 0u && !itemHashes.Contains(hash))
                ThrowBrokenReference(owner, hash);
        }

        private static void ValidateEconomyItemReferences(CsvRow row, HashSet<uint> itemHashes)
        {
            ValidateItemReference("economy.item_id", Hash(Get(row, "item_id", string.Empty)), itemHashes);
            ValidateItemReference("economy.item", Hash(Get(row, "item", string.Empty)), itemHashes);
            ValidateItemReference("economy.output_id", Hash(Get(row, "output_id", string.Empty)), itemHashes);
            ValidateItemReference("economy.output", Hash(Get(row, "output", string.Empty)), itemHashes);
            ValidateItemReference("economy.recipe_output_id", Hash(Get(row, "recipe_output_id", string.Empty)), itemHashes);
            ValidateItemReference("economy.recipe_output", Hash(Get(row, "recipe_output", string.Empty)), itemHashes);
            ValidatePackedItemReferences("economy.ingredients", Get(row, "ingredients", string.Empty), itemHashes);
            ValidatePackedItemReferences("economy.ingredient_ids", Get(row, "ingredient_ids", string.Empty), itemHashes);
            ValidatePackedItemReferences("economy.recipe", Get(row, "recipe", string.Empty), itemHashes);
            ValidatePackedItemReferences("economy.recipe_items", Get(row, "recipe_items", string.Empty), itemHashes);
        }

        private static void ValidatePackedItemReferences(string owner, string packedIds, HashSet<uint> itemHashes)
        {
            if (string.IsNullOrWhiteSpace(packedIds))
                return;

            ReadOnlySpan<char> ids = packedIds.AsSpan();
            int start = 0;
            while (start <= ids.Length)
            {
                int separator = start < ids.Length ? ids.Slice(start).IndexOf(';') : -1;
                int length = separator >= 0 ? separator : ids.Length - start;
                ReadOnlySpan<char> token = TrimAscii(ids.Slice(start, length));
                start = separator >= 0 ? start + separator + 1 : ids.Length + 1;
                if (token.Length == 0)
                    continue;

                ValidateItemReference(owner, Hash(token), itemHashes);
            }
        }

        private static void ThrowBrokenReference(string owner, uint hash)
        {
            throw new InvalidOperationException("[H8DataMonolithCompiler] Broken static-data cross-reference: owner=" + owner + ", hash=0x" + hash.ToString("X8"));
        }

        private static void ParseCsv(CsvFileRows source, DataSet dataSet, LocalizationPool localizationPool)
        {
            string tableName = Path.GetFileNameWithoutExtension(source.AbsolutePath).ToLowerInvariant();
            for (int i = 0; i < source.Rows.Count; i++)
            {
                ValidateCsvRowHashes(source.AbsolutePath, i + 2, source.Rows[i], requireHashPairs: false);
                ParseRow(tableName, source.Rows[i], dataSet, localizationPool);
            }
        }

        private static void ParseJson(string absolutePath, DataSet dataSet, LocalizationPool localizationPool)
        {
            string json = File.ReadAllText(absolutePath, Encoding.UTF8);
            JsonRoot root = JsonUtility.FromJson<JsonRoot>(json);
            if (root == null)
                return;

            if (root.items != null)
                for (int i = 0; i < root.items.Length; i++)
                    dataSet.Items.Add(ToItemRecord(root.items[i], localizationPool));

            if (root.creatures != null)
                for (int i = 0; i < root.creatures.Length; i++)
                    dataSet.Creatures.Add(ToCreatureRecord(root.creatures[i], localizationPool));

            if (root.biomes != null)
                for (int i = 0; i < root.biomes.Length; i++)
                    dataSet.Biomes.Add(ToBiomeRecord(root.biomes[i], localizationPool));

            if (root.recipes != null)
                for (int i = 0; i < root.recipes.Length; i++)
                    dataSet.Recipes.Add(ToRecipeRecord(root.recipes[i]));
        }

        private static void ParseRow(string tableName, CsvRow row, DataSet dataSet, LocalizationPool localizationPool)
        {
            switch (tableName)
            {
                case "items":
                case "item":
                    dataSet.Items.Add(ParseItem(row, localizationPool));
                    break;
                case "fauna":
                case "creatures":
                case "creature_traits":
                case "genome":
                    dataSet.Creatures.Add(ParseCreature(row, localizationPool));
                    break;
                case "economy":
                    dataSet.RawEconomyRows.Add(row);
                    dataSet.Economy.Add(ParseEconomy(row, localizationPool));
                    break;
                case "physics":
                case "physics_constants":
                    dataSet.PhysicsConstants.Add(ParsePhysicsConstants(row, localizationPool));
                    break;
                case "biomes":
                    dataSet.Biomes.Add(ParseBiome(row, localizationPool));
                    break;
                case "recipes":
                    dataSet.Recipes.Add(ParseRecipe(row));
                    break;
                case "biome_heatmap":
                    dataSet.BiomeHeatmap.Add(ParseBiomeHeatmapCell(row));
                    break;
                case "quest_nodes":
                    dataSet.QuestNodes.Add(ParseQuestNode(row));
                    break;
                case "quest_edges":
                    dataSet.QuestEdges.Add(ParseQuestEdge(row));
                    break;
                case "loot":
                case "loot_cdf":
                    dataSet.RawLootRows.Add(row);
                    break;
                case "voxel_materials":
                    dataSet.VoxelMaterials.Add(ParseVoxelMaterial(row));
                    break;
                case "audio":
                case "audio_registry":
                    dataSet.AudioClips.Add(ParseAudio(row, localizationPool));
                    break;
                case "vfx":
                case "vfx_scalars":
                    dataSet.VfxScalars.Add(ParseVfx(row));
                    break;
                case "tool_heat":
                    dataSet.ToolHeat.Add(ParseToolHeat(row));
                    break;
                case "hull":
                case "submarine_hull":
                    dataSet.HullConstants.Add(ParseHull(row));
                    break;
                case "narrative_triggers":
                    dataSet.NarrativeTriggers.Add(ParseNarrativeTrigger(row));
                    break;
                case "physics_materials":
                    dataSet.PhysicsMaterials.Add(ParsePhysicsMaterial(row));
                    break;
                case "ghost_modules":
                    dataSet.GhostModules.Add(ParseGhostModule(row, localizationPool));
                    break;
                case "radiation":
                case "radiation_map":
                    dataSet.RadiationCells.Add(ParseRadiation(row));
                    break;
                case "spawn_credits":
                    dataSet.SpawnCredits.Add(ParseSpawnCredit(row));
                    break;
                case "sop_errors":
                    dataSet.SopErrors.Add(ParseSopError(row, localizationPool));
                    break;
                case "hud_layout":
                    dataSet.HudLayouts.Add(ParseHudLayout(row));
                    break;
                case "sector_pages":
                    dataSet.SectorPages.Add(ParseSectorPage(row));
                    break;
            }
        }

        private static H8ItemRecord ParseItem(CsvRow row, LocalizationPool localizationPool)
        {
            string id = Get(row, "id", Get(row, "item_id", string.Empty));
            string name = Get(row, "name", id);
            string description = Get(row, "description", string.Empty);
            ulong mask0 = 0UL;
            ulong mask1 = 0UL;
            int ingredientCount = AddRecipeMask(Get(row, "recipe", string.Empty), ref mask0, ref mask1);
            uint nameOffset = localizationPool.Add(name, out int nameBytes);
            uint descriptionOffset = localizationPool.Add(description, out int descriptionBytes);

            return new H8ItemRecord
            {
                HashId = Hash(id),
                CategoryHash = Hash(Get(row, "category", Get(row, "categoryid", string.Empty))),
                Flags = ParseUInt(row, "flags", 0u),
                MaxStack = (ushort)Mathf.Clamp(ParseInt(row, "max_stack", ParseInt(row, "stackmax", 1)), 0, ushort.MaxValue),
                RecipeIngredientCount = (ushort)Mathf.Clamp(ingredientCount, 0, ushort.MaxValue),
                RecipeMask0 = mask0,
                RecipeMask1 = mask1,
                MassKg = ParseFloat(row, "mass_kg", ParseFloat(row, "masskg", 1f)),
                VolumeM3 = ParseFloat(row, "volume_m3", 0.001f),
                BaseQuality = ParseFloat(row, "quality", 1f),
                HeatCapacity = ParseFloat(row, "heat_capacity", 0f),
                YieldHash = Hash(Get(row, "yield_id", string.Empty)),
                NameUtf8Offset = nameOffset,
                DescriptionUtf8Offset = descriptionOffset,
                NameUtf8ByteLength = (uint)nameBytes,
                DescriptionUtf8ByteLength = (uint)descriptionBytes,
                Cost = ParseUInt(row, "cost", 0u),
                AccessFrequency = ParseFloat(row, "accessfrequency", 0f)
            };
        }

        private static H8CreatureTraitRecord ParseCreature(CsvRow row, LocalizationPool localizationPool)
        {
            string id = Get(row, "id", Get(row, "species_id", string.Empty));
            string displayName = Get(row, "name", id);
            uint displayNameOffset = localizationPool.Add(displayName, out int displayNameBytes);
            float swimSpeed = ParseFloat(row, "swimspeed", ParseFloat(row, "cruise_speed", 1f));
            float turnRate = ParseFloat(row, "turnrate", ParseFloat(row, "metabolism", 1f));
            float aggression = ParseFloat(row, "aggression01", ParseFloat(row, "aggression", 0f));
            float fleeDistance = ParseFloat(row, "fleedistancem", ParseFloat(row, "max_depth", 0f));
            float biolumIntensity = ParseFloat(row, "biolumintensity", 0f);
            return new H8CreatureTraitRecord
            {
                SpeciesHash = Hash(id),
                MateMask = ParseUInt(row, "mate_mask", 0u),
                BiomeMask = ParseUInt(row, "biome_mask", 0u),
                Flags = ParseUInt(row, "flags", 0u),
                Genome = new H8CreatureGenomeTraitBlock
                {
                    Aggression = aggression,
                    Metabolism = turnRate,
                    MaxHealth = ParseFloat(row, "max_health", 1f),
                    CruiseSpeed = swimSpeed,
                    BurstSpeed = ParseFloat(row, "burst_speed", Mathf.Max(swimSpeed * 1.35f, swimSpeed)),
                    SpawnCreditCost = ParseFloat(row, "spawn_credit", ParseFloat(row, "accessfrequency", Mathf.Max(1f, biolumIntensity))),
                    PressureMinMeters = ParseFloat(row, "min_depth", 0f),
                    PressureMaxMeters = ParseFloat(row, "max_depth", fleeDistance)
                },
                DisplayNameUtf8Offset = displayNameOffset,
                LootTableHash = Hash(Get(row, "loot_table", string.Empty)),
                DisplayNameUtf8ByteLength = (uint)displayNameBytes
            };
        }

        private static H8EconomyRecord ParseEconomy(CsvRow row, LocalizationPool localizationPool)
        {
            string id = Get(row, "id", string.Empty);
            string name = Get(row, "name", id);
            string description = Get(row, "description", string.Empty);
            uint nameOffset = localizationPool.Add(name, out int nameBytes);
            uint descriptionOffset = localizationPool.Add(description, out int descriptionBytes);
            return new H8EconomyRecord
            {
                HashId = Hash(id),
                NameUtf8Offset = nameOffset,
                DescriptionUtf8Offset = descriptionOffset,
                BasePrice = ParseFloat(row, "baseprice", ParseFloat(row, "base_price", 0f)),
                Scarcity01 = Saturate(ParseFloat(row, "scarcity01", ParseFloat(row, "scarcity", 0f))),
                Demand01 = Saturate(ParseFloat(row, "demand01", ParseFloat(row, "demand", 0f))),
                SupplyRefreshSeconds = ParseFloat(row, "supplyrefreshseconds", ParseFloat(row, "supply_refresh_seconds", 0f)),
                AccessFrequency = ParseFloat(row, "accessfrequency", 0f),
                NameUtf8ByteLength = (uint)nameBytes,
                DescriptionUtf8ByteLength = (uint)descriptionBytes,
                Flags = ParseUInt(row, "flags", 0u)
            };
        }

        private static H8PhysicsConstantsRecord ParsePhysicsConstants(CsvRow row, LocalizationPool localizationPool)
        {
            string id = Get(row, "id", string.Empty);
            string name = Get(row, "name", id);
            string description = Get(row, "description", string.Empty);
            uint nameOffset = localizationPool.Add(name, out int nameBytes);
            uint descriptionOffset = localizationPool.Add(description, out int descriptionBytes);
            return new H8PhysicsConstantsRecord
            {
                HashId = Hash(id),
                NameUtf8Offset = nameOffset,
                DescriptionUtf8Offset = descriptionOffset,
                NameUtf8ByteLength = (uint)nameBytes,
                DescriptionUtf8ByteLength = (uint)descriptionBytes,
                MassKg = ParseFloat(row, "masskg", ParseFloat(row, "mass_kg", 0f)),
                AddedMass = ParseFloat(row, "addedmass", ParseFloat(row, "added_mass", 0f)),
                LinearDrag = ParseFloat(row, "lineardrag", ParseFloat(row, "linear_drag", 0f)),
                Buoyancy = ParseFloat(row, "buoyancy", 0f),
                CrushDepthM = ParseFloat(row, "crushdepthm", ParseFloat(row, "crush_depth_m", ParseFloat(row, "crush_depth", 0f))),
                AupSectorSizeMeters = ParseFloat(row, "aupsectorsizemeters", ParseFloat(row, "aup_sector_size_meters", 1000f)),
                MaxWorldBoundsMeters = ParseFloat(row, "maxworldboundsmeters", ParseFloat(row, "max_world_bounds_meters", 100000f)),
                AccessFrequency = ParseFloat(row, "accessfrequency", 0f),
                Flags = ParseUInt(row, "flags", 0u)
            };
        }

        private static H8BiomeRecord ParseBiome(CsvRow row, LocalizationPool localizationPool)
        {
            string id = Get(row, "id", Get(row, "biome_id", string.Empty));
            string displayName = Get(row, "name", id);
            uint displayNameOffset = localizationPool.Add(displayName, out int displayNameBytes);
            return new H8BiomeRecord
            {
                BiomeHash = Hash(id),
                Flags = ParseUInt(row, "flags", 0u),
                SurfaceId = Hash(Get(row, "surface_id", string.Empty)),
                MinDepthMeters = ParseFloat(row, "min_depth", 0f),
                MaxDepthMeters = ParseFloat(row, "max_depth", 0f),
                TemperatureCelsius = ParseFloat(row, "temperature_c", 2f),
                PressureScalar = ParseFloat(row, "pressure_scalar", 1f),
                FogDensity = ParseFloat(row, "fog_density", 0f),
                LightScatterR = ParseFloat(row, "scatter_r", 0.08f),
                LightScatterG = ParseFloat(row, "scatter_g", 0.18f),
                LightScatterB = ParseFloat(row, "scatter_b", 0.24f),
                DisplayNameUtf8Offset = displayNameOffset,
                HeatmapId = Hash(Get(row, "heatmap_id", string.Empty)),
                RadiationFieldHash = Hash(Get(row, "radiation_id", string.Empty)),
                DisplayNameUtf8ByteLength = (uint)displayNameBytes
            };
        }

        private static H8RecipeRecord ParseRecipe(CsvRow row)
        {
            ulong mask0 = 0UL;
            ulong mask1 = 0UL;
            string ingredients = Get(row, "ingredients", Get(row, "recipe", string.Empty));
            uint h0 = 0u;
            uint h1 = 0u;
            uint h2 = 0u;
            uint h3 = 0u;
            int count = AddRecipeMaskAndHashes(ingredients, ref mask0, ref mask1, ref h0, ref h1, ref h2, ref h3);
            return new H8RecipeRecord
            {
                OutputHash = Hash(Get(row, "output", Get(row, "output_id", string.Empty))),
                StationHash = Hash(Get(row, "station", string.Empty)),
                Flags = ParseUInt(row, "flags", 0u),
                IngredientCount = (uint)count,
                IngredientMask0 = mask0,
                IngredientMask1 = mask1,
                IngredientHash0 = h0,
                IngredientHash1 = h1,
                IngredientHash2 = h2,
                IngredientHash3 = h3,
                CraftSeconds = ParseFloat(row, "craft_seconds", 1f),
                OutputCount = ParseUInt(row, "output_count", 1u)
            };
        }

        private static H8BiomeHeatmapCellRecord ParseBiomeHeatmapCell(CsvRow row)
        {
            return new H8BiomeHeatmapCellRecord
            {
                BiomeHash = Hash(Get(row, "biome_id", string.Empty)),
                X = (ushort)Mathf.Clamp(ParseInt(row, "x", 0), 0, 255),
                Y = (ushort)Mathf.Clamp(ParseInt(row, "y", 0), 0, 255)
            };
        }

        private static H8QuestNodeRecord ParseQuestNode(CsvRow row)
        {
            uint mask0 = 0u;
            uint mask1 = 0u;
            uint mask2 = 0u;
            uint mask3 = 0u;
            AddRecipeMask(Get(row, "required_flags", string.Empty), ref mask0, ref mask1, ref mask2, ref mask3);
            return new H8QuestNodeRecord
            {
                NodeHash = Hash(Get(row, "id", string.Empty)),
                CompletionFlagId = ParseUInt(row, "completion_flag", 0u),
                FirstEdgeIndex = ParseUInt(row, "first_edge", 0u),
                EdgeCount = (ushort)Mathf.Clamp(ParseInt(row, "edge_count", 0), 0, ushort.MaxValue),
                NodeType = (ushort)Mathf.Clamp(ParseInt(row, "node_type", 0), 0, ushort.MaxValue),
                RequiredMask0 = mask0,
                RequiredMask1 = mask1,
                RequiredMask2 = mask2,
                RequiredMask3 = mask3
            };
        }

        private static H8QuestEdgeRecord ParseQuestEdge(CsvRow row)
        {
            return new H8QuestEdgeRecord
            {
                FromNodeHash = Hash(Get(row, "from", string.Empty)),
                ToNodeHash = Hash(Get(row, "to", string.Empty)),
                GateFlagId = ParseUInt(row, "gate_flag", 0u)
            };
        }

        private static H8VoxelMaterialRecord ParseVoxelMaterial(CsvRow row)
        {
            return new H8VoxelMaterialRecord
            {
                VoxelHash = Hash(Get(row, "id", string.Empty)),
                YieldHash = Hash(Get(row, "yield_id", string.Empty)),
                Hardness = ParseFloat(row, "hardness", 1f),
                MeltingPointCelsius = ParseFloat(row, "melting_point_c", 1000f),
                Density = ParseFloat(row, "density", 1f),
                SurfaceId = Hash(Get(row, "surface_id", string.Empty)),
                Flags = ParseUInt(row, "flags", 0u)
            };
        }

        private static H8AudioClipRegistryRecord ParseAudio(CsvRow row, LocalizationPool localizationPool)
        {
            string addressableKey = Get(row, "addressable_key", string.Empty);
            uint addressableOffset = localizationPool.Add(addressableKey, out int addressableBytes);
            return new H8AudioClipRegistryRecord
            {
                EventHash = Hash(Get(row, "event_id", Get(row, "id", string.Empty))),
                AddressableKeyUtf8Offset = addressableOffset,
                BankHash = Hash(Get(row, "bank", string.Empty)),
                AddressableKeyUtf8ByteLength = (uint)addressableBytes
            };
        }

        private static H8VfxScalarRecord ParseVfx(CsvRow row)
        {
            return new H8VfxScalarRecord
            {
                EffectHash = Hash(Get(row, "id", string.Empty)),
                EmissionRate = ParseFloat(row, "emission_rate", 0f),
                ColorR = ParseFloat(row, "r", 1f),
                ColorG = ParseFloat(row, "g", 1f),
                ColorB = ParseFloat(row, "b", 1f),
                ColorA = ParseFloat(row, "a", 1f),
                Intensity = ParseFloat(row, "intensity", 1f),
                Flags = ParseUInt(row, "flags", 0u)
            };
        }

        private static H8ToolHeatCapacityRecord ParseToolHeat(CsvRow row)
        {
            return new H8ToolHeatCapacityRecord
            {
                ToolHash = Hash(Get(row, "id", string.Empty)),
                HeatCapacity = ParseFloat(row, "heat_capacity", 0f),
                MaxSafeTemperature = ParseFloat(row, "max_safe_temperature", 100f)
            };
        }

        private static H8SubmarineHullConstantRecord ParseHull(CsvRow row)
        {
            return new H8SubmarineHullConstantRecord
            {
                PartHash = Hash(Get(row, "id", string.Empty)),
                MassKg = ParseFloat(row, "mass_kg", 1f),
                DragScalar = ParseFloat(row, "drag", 1f),
                BuoyancyScalar = ParseFloat(row, "buoyancy", 1f),
                CrushDepthMeters = ParseFloat(row, "crush_depth", 0f),
                IntegrityCap = ParseFloat(row, "integrity_cap", 1f),
                Flags = ParseUInt(row, "flags", 0u)
            };
        }

        private static H8NarrativeTriggerRecord ParseNarrativeTrigger(CsvRow row)
        {
            return new H8NarrativeTriggerRecord
            {
                TriggerHash = Hash(Get(row, "id", string.Empty)),
                AupX = ParseDouble(row, "aup_x", 0d),
                AupY = ParseDouble(row, "aup_y", 0d),
                AupZ = ParseDouble(row, "aup_z", 0d),
                RadiusMeters = ParseFloat(row, "radius", 1f)
            };
        }

        private static H8PhysicsMaterialRecord ParsePhysicsMaterial(CsvRow row)
        {
            return new H8PhysicsMaterialRecord
            {
                SurfaceHash = Hash(Get(row, "id", string.Empty)),
                Friction = ParseFloat(row, "friction", 0.5f),
                Restitution = ParseFloat(row, "restitution", 0f),
                Flags = ParseUInt(row, "flags", 0u)
            };
        }

        private static H8GhostModuleRecord ParseGhostModule(CsvRow row, LocalizationPool localizationPool)
        {
            string id = Get(row, "id", string.Empty);
            string displayName = Get(row, "name", id);
            uint displayNameOffset = localizationPool.Add(displayName, out int displayNameBytes);
            return new H8GhostModuleRecord
            {
                ModuleHash = Hash(id),
                Flags = ParseUInt(row, "flags", 0u),
                SnapOffsetX = ParseFloat(row, "snap_x", 0f),
                SnapOffsetY = ParseFloat(row, "snap_y", 0f),
                SnapOffsetZ = ParseFloat(row, "snap_z", 0f),
                PowerRequirement = ParseFloat(row, "power", 0f),
                BuildCostScalar = ParseFloat(row, "build_cost", 1f),
                RecipeHash = Hash(Get(row, "recipe_id", string.Empty)),
                DisplayNameUtf8Offset = displayNameOffset,
                DisplayNameUtf8ByteLength = (uint)displayNameBytes
            };
        }

        private static H8RadiationIntensityCellRecord ParseRadiation(CsvRow row)
        {
            return new H8RadiationIntensityCellRecord
            {
                CellHash = Hash(Get(row, "id", string.Empty)),
                IntensitySv = ParseFloat(row, "intensity_sv", 0f),
                FalloffMeters = ParseFloat(row, "falloff", 1f)
            };
        }

        private static H8SpawnCreditCostRecord ParseSpawnCredit(CsvRow row)
        {
            return new H8SpawnCreditCostRecord
            {
                EntityHash = Hash(Get(row, "id", string.Empty)),
                CreditCost = ParseFloat(row, "credit_cost", 1f),
                DirectorMask = ParseUInt(row, "director_mask", 0u)
            };
        }

        private static H8SopErrorRecord ParseSopError(CsvRow row, LocalizationPool localizationPool)
        {
            string message = Get(row, "message", string.Empty);
            uint messageOffset = localizationPool.Add(message, out int messageBytes);
            return new H8SopErrorRecord
            {
                ErrorHash = Hash(Get(row, "id", string.Empty)),
                MessageUtf8Offset = messageOffset,
                Severity = ParseUInt(row, "severity", 0u),
                MessageUtf8ByteLength = (uint)messageBytes
            };
        }

        private static H8HudLayoutRecord ParseHudLayout(CsvRow row)
        {
            return new H8HudLayoutRecord
            {
                ElementHash = Hash(Get(row, "id", string.Empty)),
                Flags = ParseUInt(row, "flags", 0u),
                M00 = ParseFloat(row, "m00", 1f),
                M01 = ParseFloat(row, "m01", 0f),
                M02 = ParseFloat(row, "m02", 0f),
                M03 = ParseFloat(row, "m03", 0f),
                M10 = ParseFloat(row, "m10", 0f),
                M11 = ParseFloat(row, "m11", 1f),
                M12 = ParseFloat(row, "m12", 0f),
                M13 = ParseFloat(row, "m13", 0f),
                M20 = ParseFloat(row, "m20", 0f),
                M21 = ParseFloat(row, "m21", 0f),
                M22 = ParseFloat(row, "m22", 1f),
                M23 = ParseFloat(row, "m23", 0f),
                M30 = ParseFloat(row, "m30", 0f),
                M31 = ParseFloat(row, "m31", 0f)
            };
        }

        private static H8SectorPageRecord ParseSectorPage(CsvRow row)
        {
            return new H8SectorPageRecord
            {
                SectorHash = Hash(Get(row, "sector_id", Get(row, "id", string.Empty))),
                BiomeHash = Hash(Get(row, "biome_id", string.Empty)),
                FileOffsetBytes = ParseUInt(row, "file_offset", 0u),
                ByteCount = ParseUInt(row, "byte_count", 0u),
                AupX = ParseLong(row, "aup_x", 0L),
                AupZ = ParseLong(row, "aup_z", 0L)
            };
        }

        private static void RebuildLootCdf(DataSet dataSet)
        {
            dataSet.LootCdf.Clear();
            dataSet.RawLootRows.Sort(CompareLootRows);
            uint activeTable = 0u;
            uint cumulative = 0u;
            uint tableTotal = 0u;
            int tableStart = 0;

            for (int i = 0; i < dataSet.RawLootRows.Count; i++)
            {
                CsvRow row = dataSet.RawLootRows[i];
                uint tableHash = Hash(Get(row, "table_id", Get(row, "table", string.Empty)));
                if (i == 0 || tableHash != activeTable)
                {
                    PatchLootTableTotal(dataSet.LootCdf, tableStart, dataSet.LootCdf.Count, tableTotal);
                    activeTable = tableHash;
                    cumulative = 0u;
                    tableTotal = 0u;
                    tableStart = dataSet.LootCdf.Count;
                }

                uint weight = ParseUInt(row, "weight", 0u);
                cumulative += weight;
                tableTotal += weight;
                dataSet.LootCdf.Add(new H8LootCdfRecord
                {
                    TableHash = tableHash,
                    ItemHash = Hash(Get(row, "item_id", Get(row, "item", string.Empty))),
                    CumulativeWeight = cumulative,
                    TotalWeight = tableTotal
                });
            }

            PatchLootTableTotal(dataSet.LootCdf, tableStart, dataSet.LootCdf.Count, tableTotal);
        }

        private static void PatchLootTableTotal(List<H8LootCdfRecord> records, int start, int end, uint total)
        {
            for (int i = start; i < end; i++)
            {
                H8LootCdfRecord record = records[i];
                record.TotalWeight = total;
                records[i] = record;
            }
        }

        private static void NormalizeBiomeHeatmap(DataSet dataSet)
        {
            uint fallbackBiomeHash = dataSet.Biomes.Count > 0 ? dataSet.Biomes[0].BiomeHash : 0u;
            H8BiomeHeatmapCellRecord[] cells = new H8BiomeHeatmapCellRecord[256 * 256]; // COLD ALLOC: H8BiomeHeatmapCellRecord[65536] - editor-only heatmap normalization scratch - owner: H8DataMonolithCompiler
            for (int y = 0; y < 256; y++)
            {
                int rowOffset = y * 256;
                for (int x = 0; x < 256; x++)
                {
                    cells[rowOffset + x] = new H8BiomeHeatmapCellRecord
                    {
                        BiomeHash = fallbackBiomeHash,
                        X = (ushort)x,
                        Y = (ushort)y
                    };
                }
            }

            for (int i = 0; i < dataSet.BiomeHeatmap.Count; i++)
            {
                H8BiomeHeatmapCellRecord source = dataSet.BiomeHeatmap[i];
                int index = (source.Y * 256) + source.X;
                if ((uint)index < (uint)cells.Length)
                    cells[index] = source;
            }

            dataSet.BiomeHeatmap.Clear();
            for (int i = 0; i < cells.Length; i++)
                dataSet.BiomeHeatmap.Add(cells[i]);
        }

        private static void GenerateDepthPressureCurve(List<H8DepthPressureSampleRecord> records)
        {
            for (int i = 0; i < 256; i++)
            {
                float depth = (5000f / 255f) * i;
                records.Add(new H8DepthPressureSampleRecord
                {
                    DepthMeters = depth,
                    PressureAtmospheres = 1f + (depth * 0.1f),
                    Normalized = depth / 5000f
                });
            }
        }

        private static void GenerateLightAttenuationCurve(List<H8LightAttenuationSampleRecord> records)
        {
            for (int i = 0; i < 256; i++)
            {
                float t = i / 255f;
                float depth = t * 5000f;
                records.Add(new H8LightAttenuationSampleRecord
                {
                    DepthMeters = depth,
                    FogDensity = 0.008f + (t * 0.06f),
                    ScatterR = Mathf.Lerp(0.08f, 0.01f, t),
                    ScatterG = Mathf.Lerp(0.18f, 0.04f, t),
                    ScatterB = Mathf.Lerp(0.28f, 0.09f, t),
                    Absorption = 0.02f + (t * 0.18f)
                });
            }
        }

        private static H8DataSectionEntry AppendSection<T>(MemoryStream stream, H8DataSectionId sectionId, List<T> records, int recordSize)
            where T : unmanaged
        {
            Align16(stream);
            uint offset = records.Count > 0 ? (uint)stream.Position : 0u;
            for (int i = 0; i < records.Count; i++)
                WriteStruct(stream, records[i]);

            return new H8DataSectionEntry
            {
                SectionId = (uint)sectionId,
                RecordSize = (uint)recordSize,
                Count = (uint)records.Count,
                OffsetBytes = offset
            };
        }

        private static H8DataSectionEntry AppendLocalizationSection(MemoryStream stream, LocalizationPool localizationPool)
        {
            Align16(stream);
            byte[] bytes = localizationPool.ToArray();
            uint offset = bytes.Length > 0 ? (uint)stream.Position : 0u;
            stream.Write(bytes, 0, bytes.Length);
            return new H8DataSectionEntry
            {
                SectionId = (uint)H8DataSectionId.LocalizationUtf8,
                RecordSize = 1u,
                Count = (uint)bytes.Length,
                OffsetBytes = offset
            };
        }

        private static void Align16(MemoryStream stream)
        {
            long aligned = (stream.Position + 15L) & ~15L;
            while (stream.Position < aligned)
                stream.WriteByte(0);
        }

        private static void WriteZeros(MemoryStream stream, int count)
        {
            for (int i = 0; i < count; i++)
                stream.WriteByte(0);
        }

        private static void EnsureLittleEndianEditorHost()
        {
            if (!BitConverter.IsLittleEndian)
                throw new PlatformNotSupportedException("[H8DataMonolithCompiler] Big-endian editor hosts are not allowed to emit static_data.h8bin without explicit per-record byte swapping.");
        }

        private static void WriteHeader(byte[] blob, in H8DataBlobHeader header)
        {
            WriteUInt32(blob, 0, header.Magic);
            WriteUInt16(blob, 4, header.FormatVersion);
            WriteUInt16(blob, 6, header.HeaderBytes);
            WriteUInt64(blob, 8, header.Checksum64);
        }

        private static void WriteDirectory(MemoryStream stream, in H8DataBlobDirectory directory)
        {
            WriteUInt32(stream, directory.Magic);
            WriteUInt16(stream, directory.FormatVersion);
            WriteUInt16(stream, directory.SectionCount);
            WriteUInt32(stream, directory.SectionTableOffset);
            WriteUInt32(stream, directory.SectionTableBytes);
            WriteUInt32(stream, directory.BlobBytes);
            WriteUInt32(stream, directory.DataStartOffset);
            WriteUInt32(stream, directory.LocalizationOffset);
            WriteUInt32(stream, directory.LocalizationBytes);
            WriteUInt32(stream, directory.Flags);
            WriteUInt32(stream, directory.WorldSeed);
            WriteUInt32(stream, directory.AppVersionHash);
            WriteUInt32(stream, directory.Reserved0);
            WriteUInt32(stream, directory.Reserved1);
            WriteUInt32(stream, directory.Reserved2);
            WriteUInt32(stream, directory.Reserved3);
            WriteUInt32(stream, directory.Reserved4);
        }

        private static void WriteSectionEntry(MemoryStream stream, in H8DataSectionEntry entry)
        {
            WriteUInt32(stream, entry.SectionId);
            WriteUInt32(stream, entry.RecordSize);
            WriteUInt32(stream, entry.Count);
            WriteUInt32(stream, entry.OffsetBytes);
        }

        private static void WriteUInt16(MemoryStream stream, ushort value)
        {
            stream.WriteByte((byte)value);
            stream.WriteByte((byte)(value >> 8));
        }

        private static void WriteUInt32(MemoryStream stream, uint value)
        {
            stream.WriteByte((byte)value);
            stream.WriteByte((byte)(value >> 8));
            stream.WriteByte((byte)(value >> 16));
            stream.WriteByte((byte)(value >> 24));
        }

        private static void WriteUInt16(byte[] bytes, int offset, ushort value)
        {
            bytes[offset] = (byte)value;
            bytes[offset + 1] = (byte)(value >> 8);
        }

        private static void WriteUInt32(byte[] bytes, int offset, uint value)
        {
            bytes[offset] = (byte)value;
            bytes[offset + 1] = (byte)(value >> 8);
            bytes[offset + 2] = (byte)(value >> 16);
            bytes[offset + 3] = (byte)(value >> 24);
        }

        private static void WriteUInt64(byte[] bytes, int offset, ulong value)
        {
            bytes[offset] = (byte)value;
            bytes[offset + 1] = (byte)(value >> 8);
            bytes[offset + 2] = (byte)(value >> 16);
            bytes[offset + 3] = (byte)(value >> 24);
            bytes[offset + 4] = (byte)(value >> 32);
            bytes[offset + 5] = (byte)(value >> 40);
            bytes[offset + 6] = (byte)(value >> 48);
            bytes[offset + 7] = (byte)(value >> 56);
        }

        private static void WriteStruct<T>(MemoryStream stream, T value)
            where T : unmanaged
        {
            EnsureLittleEndianEditorHost();
            int size = UnsafeUtility.SizeOf<T>();
            if (size > 256)
                throw new InvalidOperationException("[H8DataMonolithCompiler] Record struct exceeds stack emission scratch limit: " + typeof(T).Name);

            Span<byte> scratch = stackalloc byte[size];
            fixed (byte* ptr = scratch)
            {
                UnsafeUtility.CopyStructureToPtr(ref value, ptr);
            }

            stream.Write(scratch);
        }

        private static ulong ComputeHash64(byte[] bytes, int offset, int count)
        {
            fixed (byte* ptr = bytes)
            {
                uint2 hash = xxHash3.Hash64(ptr + offset, count);
                return ((ulong)hash.y << 32) | hash.x;
            }
        }

        private static List<CsvRow> ReadCsvRows(string absolutePath)
        {
            string[] lines = File.ReadAllLines(absolutePath, Encoding.UTF8);
            List<CsvRow> rows = new List<CsvRow>(Math.Max(0, lines.Length - 1)); // COLD ALLOC: List<CsvRow>[csv row count] - editor-only source data import - owner: H8DataMonolithCompiler
            if (lines.Length <= 1)
                return rows;

            string[] headers = SplitCsvLine(lines[0]);
            for (int i = 1; i < lines.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(lines[i]) || lines[i].TrimStart().StartsWith("#", StringComparison.Ordinal))
                    continue;

                string[] values = SplitCsvLine(lines[i]);
                CsvRow row = new CsvRow();
                int count = Mathf.Min(headers.Length, values.Length);
                for (int j = 0; j < count; j++)
                    row.Fields[headers[j].Trim()] = values[j].Trim();
                rows.Add(row);
            }

            return rows;
        }

        private static string[] SplitCsvLine(string line)
        {
            List<string> values = new List<string>(16); // COLD ALLOC: List<string>[csv column count] - editor-only CSV parser scratch - owner: H8DataMonolithCompiler
            StringBuilder builder = new StringBuilder(128); // COLD ALLOC: StringBuilder[128] - editor-only CSV cell parser scratch - owner: H8DataMonolithCompiler
            bool quoted = false;
            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (c == '"')
                {
                    if (quoted && i + 1 < line.Length && line[i + 1] == '"')
                    {
                        builder.Append('"');
                        i++;
                    }
                    else
                    {
                        quoted = !quoted;
                    }

                    continue;
                }

                if (c == ',' && !quoted)
                {
                    values.Add(builder.ToString());
                    builder.Length = 0;
                    continue;
                }

                builder.Append(c);
            }

            values.Add(builder.ToString());
            return values.ToArray();
        }

        private static void ValidateCsvRowHashes(string absolutePath, int lineNumber, CsvRow row, bool requireHashPairs)
        {
            int validatedPairs = 0;
            int idFieldCount = 0;
            foreach (KeyValuePair<string, string> field in row.Fields)
            {
                if (!IsAuthoredIdField(field.Key, field.Value, out string hashField))
                    continue;

                idFieldCount++;
                if (!row.Fields.TryGetValue(hashField, out string expectedHashText) ||
                    string.IsNullOrWhiteSpace(expectedHashText))
                {
                    if (requireHashPairs)
                        ThrowMissingCsvHash(absolutePath, lineNumber, field.Key, hashField);
                    continue;
                }

                uint expectedHash = H8DataHash.ComputeFnv1A32(field.Value.AsSpan());
                if (!TryParseUIntFlexible(expectedHashText, out uint authoredHash) || authoredHash != expectedHash)
                    ThrowCsvHashMismatch(absolutePath, lineNumber, field.Key, field.Value, hashField, expectedHashText, expectedHash);

                validatedPairs++;
            }

            if (requireHashPairs && idFieldCount > 0 && validatedPairs == 0)
                ThrowMissingCsvHash(absolutePath, lineNumber, "id", "hash32");
        }

        private static bool IsAuthoredIdField(string fieldName, string value, out string hashField)
        {
            hashField = string.Empty;
            if (string.IsNullOrWhiteSpace(fieldName) || string.IsNullOrWhiteSpace(value))
                return false;

            if (fieldName.EndsWith("_id", StringComparison.OrdinalIgnoreCase))
            {
                hashField = fieldName.Substring(0, fieldName.Length - 3) + "_hash32";
                return true;
            }

            if (string.Equals(fieldName, "id", StringComparison.OrdinalIgnoreCase))
            {
                hashField = "hash32";
                return true;
            }

            if (string.Equals(fieldName, "output", StringComparison.OrdinalIgnoreCase))
            {
                hashField = "output_hash32";
                return true;
            }

            return false;
        }

        private static bool TryParseUIntFlexible(string value, out uint parsed)
        {
            parsed = 0u;
            if (string.IsNullOrWhiteSpace(value))
                return false;

            string trimmed = value.Trim();
            if (trimmed.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                return uint.TryParse(trimmed.Substring(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out parsed);

            return uint.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed);
        }

        private static bool IsBalanceSourceFile(string absolutePath)
        {
            string balanceRoot = Path.GetFullPath(BalanceSourceFolder).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string normalizedPath = Path.GetFullPath(absolutePath);
            return normalizedPath.StartsWith(balanceRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
                   normalizedPath.StartsWith(balanceRoot + Path.AltDirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
        }

        private static void ThrowMissingCsvHash(string absolutePath, int lineNumber, string idField, string hashField)
        {
            throw new InvalidOperationException(
                "[SIGNAL_AUTHORITY_VALIDATOR] CSV row missing FNV-1a hash pair. file=" +
                absolutePath +
                ", line=" +
                lineNumber +
                ", id_field=" +
                idField +
                ", expected_hash_field=" +
                hashField);
        }

        private static void ThrowCsvHashMismatch(
            string absolutePath,
            int lineNumber,
            string idField,
            string idValue,
            string hashField,
            string authoredHash,
            uint expectedHash)
        {
            throw new InvalidOperationException(
                "[SIGNAL_AUTHORITY_VALIDATOR] CSV FNV-1a hash mismatch. file=" +
                absolutePath +
                ", line=" +
                lineNumber +
                ", id_field=" +
                idField +
                ", id=" +
                idValue +
                ", hash_field=" +
                hashField +
                ", authored=" +
                authoredHash +
                ", expected=" +
                expectedHash);
        }

        private static int AddRecipeMask(string packedIds, ref uint mask0, ref uint mask1, ref uint mask2, ref uint mask3)
        {
            uint h0 = 0u;
            uint h1 = 0u;
            uint h2 = 0u;
            uint h3 = 0u;
            return AddRecipeMaskAndHashes(packedIds, ref mask0, ref mask1, ref mask2, ref mask3, ref h0, ref h1, ref h2, ref h3);
        }

        private static int AddRecipeMask(string packedIds, ref ulong mask0, ref ulong mask1)
        {
            uint h0 = 0u;
            uint h1 = 0u;
            uint h2 = 0u;
            uint h3 = 0u;
            return AddRecipeMaskAndHashes(packedIds, ref mask0, ref mask1, ref h0, ref h1, ref h2, ref h3);
        }

        private static int AddRecipeMaskAndHashes(
            string packedIds,
            ref uint mask0,
            ref uint mask1,
            ref uint mask2,
            ref uint mask3,
            ref uint h0,
            ref uint h1,
            ref uint h2,
            ref uint h3)
        {
            if (string.IsNullOrWhiteSpace(packedIds))
                return 0;

            int count = 0;
            ReadOnlySpan<char> ids = packedIds.AsSpan();
            int start = 0;
            while (start <= ids.Length)
            {
                int separator = start < ids.Length ? ids.Slice(start).IndexOf(';') : -1;
                int length = separator >= 0 ? separator : ids.Length - start;
                ReadOnlySpan<char> token = TrimAscii(ids.Slice(start, length));
                start = separator >= 0 ? start + separator + 1 : ids.Length + 1;
                if (token.Length == 0)
                    continue;

                uint hash = Hash(token);
                H8DataHash.AddHashToRecipeMask(hash, ref mask0, ref mask1, ref mask2, ref mask3);
                switch (count)
                {
                    case 0:
                        h0 = hash;
                        break;
                    case 1:
                        h1 = hash;
                        break;
                    case 2:
                        h2 = hash;
                        break;
                    case 3:
                        h3 = hash;
                        break;
                }

                count++;
            }

            return count;
        }

        private static int AddRecipeMaskAndHashes(
            string packedIds,
            ref ulong mask0,
            ref ulong mask1,
            ref uint h0,
            ref uint h1,
            ref uint h2,
            ref uint h3)
        {
            if (string.IsNullOrWhiteSpace(packedIds))
                return 0;

            int count = 0;
            ReadOnlySpan<char> ids = packedIds.AsSpan();
            int start = 0;
            while (start <= ids.Length)
            {
                int separator = start < ids.Length ? ids.Slice(start).IndexOf(';') : -1;
                int length = separator >= 0 ? separator : ids.Length - start;
                ReadOnlySpan<char> token = TrimAscii(ids.Slice(start, length));
                start = separator >= 0 ? start + separator + 1 : ids.Length + 1;
                if (token.Length == 0)
                    continue;

                uint hash = Hash(token);
                H8DataHash.AddHashToRecipeMask(hash, ref mask0, ref mask1);
                switch (count)
                {
                    case 0:
                        h0 = hash;
                        break;
                    case 1:
                        h1 = hash;
                        break;
                    case 2:
                        h2 = hash;
                        break;
                    case 3:
                        h3 = hash;
                        break;
                }

                count++;
            }

            return count;
        }

        private static string Get(CsvRow row, string key, string fallback)
        {
            return row.Fields.TryGetValue(key, out string value) && !string.IsNullOrEmpty(value) ? value : fallback;
        }

        private static uint Hash(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? 0u : H8DataHash.ComputeFnv1A32(value.AsSpan());
        }

        private static uint Hash(ReadOnlySpan<char> value)
        {
            return value.Length == 0 ? 0u : H8DataHash.ComputeFnv1A32(value);
        }

        private static ReadOnlySpan<char> TrimAscii(ReadOnlySpan<char> value)
        {
            int start = 0;
            int end = value.Length - 1;
            while (start <= end && value[start] <= ' ')
                start++;
            while (end >= start && value[end] <= ' ')
                end--;
            return start > end ? ReadOnlySpan<char>.Empty : value.Slice(start, (end - start) + 1);
        }

        private static uint ParseUInt(CsvRow row, string key, uint fallback)
        {
            string value = Get(row, key, string.Empty);
            if (value.StartsWith("0x", StringComparison.OrdinalIgnoreCase) &&
                uint.TryParse(value.Substring(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint hex))
            {
                return hex;
            }

            return uint.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out uint parsed) ? parsed : fallback;
        }

        private static int ParseInt(CsvRow row, string key, int fallback)
        {
            return int.TryParse(Get(row, key, string.Empty), NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed) ? parsed : fallback;
        }

        private static long ParseLong(CsvRow row, string key, long fallback)
        {
            return long.TryParse(Get(row, key, string.Empty), NumberStyles.Integer, CultureInfo.InvariantCulture, out long parsed) ? parsed : fallback;
        }

        private static float ParseFloat(CsvRow row, string key, float fallback)
        {
            return float.TryParse(Get(row, key, string.Empty), NumberStyles.Float, CultureInfo.InvariantCulture, out float parsed) ? parsed : fallback;
        }

        private static double ParseDouble(CsvRow row, string key, double fallback)
        {
            return double.TryParse(Get(row, key, string.Empty), NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed) ? parsed : fallback;
        }

        private static float Saturate(float value)
        {
            if (value <= 0f)
                return 0f;
            return value >= 1f ? 1f : value;
        }

        private static int CompareItemRecords(H8ItemRecord left, H8ItemRecord right)
        {
            return left.HashId.CompareTo(right.HashId);
        }

        private static int CompareCreatureRecords(H8CreatureTraitRecord left, H8CreatureTraitRecord right)
        {
            return left.SpeciesHash.CompareTo(right.SpeciesHash);
        }

        private static int CompareBiomeRecords(H8BiomeRecord left, H8BiomeRecord right)
        {
            return left.BiomeHash.CompareTo(right.BiomeHash);
        }

        private static int CompareLootRows(CsvRow left, CsvRow right)
        {
            int tableCompare = Hash(Get(left, "table_id", Get(left, "table", string.Empty))).CompareTo(
                Hash(Get(right, "table_id", Get(right, "table", string.Empty))));
            if (tableCompare != 0)
                return tableCompare;

            return Hash(Get(left, "item_id", Get(left, "item", string.Empty))).CompareTo(
                Hash(Get(right, "item_id", Get(right, "item", string.Empty))));
        }

        private static int CompareVoxelMaterialRecords(H8VoxelMaterialRecord left, H8VoxelMaterialRecord right)
        {
            return left.VoxelHash.CompareTo(right.VoxelHash);
        }

        private static int CompareAudioClipRecords(H8AudioClipRegistryRecord left, H8AudioClipRegistryRecord right)
        {
            return left.EventHash.CompareTo(right.EventHash);
        }

        private static int CompareHullConstantRecords(H8SubmarineHullConstantRecord left, H8SubmarineHullConstantRecord right)
        {
            return left.PartHash.CompareTo(right.PartHash);
        }

        private static int ComparePhysicsMaterialRecords(H8PhysicsMaterialRecord left, H8PhysicsMaterialRecord right)
        {
            return left.SurfaceHash.CompareTo(right.SurfaceHash);
        }

        private static int CompareEconomyRecords(H8EconomyRecord left, H8EconomyRecord right)
        {
            return left.HashId.CompareTo(right.HashId);
        }

        private static int ComparePhysicsConstantsRecords(H8PhysicsConstantsRecord left, H8PhysicsConstantsRecord right)
        {
            return left.HashId.CompareTo(right.HashId);
        }

        private static H8ItemRecord ToItemRecord(JsonItem item, LocalizationPool localizationPool)
        {
            ulong mask0 = 0UL;
            ulong mask1 = 0UL;
            int ingredientCount = AddRecipeMask(item.recipe, ref mask0, ref mask1);
            uint nameOffset = localizationPool.Add(item.name, out int nameBytes);
            uint descriptionOffset = localizationPool.Add(item.description, out int descriptionBytes);
            return new H8ItemRecord
            {
                HashId = Hash(item.id),
                CategoryHash = Hash(item.category),
                Flags = item.flags,
                MaxStack = (ushort)Mathf.Clamp(item.maxStack, 0, ushort.MaxValue),
                RecipeIngredientCount = (ushort)Mathf.Clamp(ingredientCount, 0, ushort.MaxValue),
                RecipeMask0 = mask0,
                RecipeMask1 = mask1,
                MassKg = item.massKg,
                VolumeM3 = item.volumeM3,
                BaseQuality = item.quality,
                HeatCapacity = item.heatCapacity,
                YieldHash = Hash(item.yieldId),
                NameUtf8Offset = nameOffset,
                DescriptionUtf8Offset = descriptionOffset,
                NameUtf8ByteLength = (uint)nameBytes,
                DescriptionUtf8ByteLength = (uint)descriptionBytes
            };
        }

        private static H8CreatureTraitRecord ToCreatureRecord(JsonCreature creature, LocalizationPool localizationPool)
        {
            uint displayNameOffset = localizationPool.Add(creature.name, out int displayNameBytes);
            return new H8CreatureTraitRecord
            {
                SpeciesHash = Hash(creature.id),
                MateMask = creature.mateMask,
                BiomeMask = creature.biomeMask,
                Flags = creature.flags,
                Genome = new H8CreatureGenomeTraitBlock
                {
                    Aggression = creature.aggression,
                    Metabolism = creature.metabolism,
                    MaxHealth = creature.maxHealth,
                    CruiseSpeed = creature.cruiseSpeed,
                    BurstSpeed = creature.burstSpeed,
                    SpawnCreditCost = creature.spawnCredit,
                    PressureMinMeters = creature.minDepth,
                    PressureMaxMeters = creature.maxDepth
                },
                DisplayNameUtf8Offset = displayNameOffset,
                LootTableHash = Hash(creature.lootTable),
                DisplayNameUtf8ByteLength = (uint)displayNameBytes
            };
        }

        private static H8BiomeRecord ToBiomeRecord(JsonBiome biome, LocalizationPool localizationPool)
        {
            uint displayNameOffset = localizationPool.Add(biome.name, out int displayNameBytes);
            return new H8BiomeRecord
            {
                BiomeHash = Hash(biome.id),
                Flags = biome.flags,
                SurfaceId = Hash(biome.surfaceId),
                MinDepthMeters = biome.minDepth,
                MaxDepthMeters = biome.maxDepth,
                TemperatureCelsius = biome.temperatureC,
                PressureScalar = biome.pressureScalar,
                FogDensity = biome.fogDensity,
                LightScatterR = biome.scatterR,
                LightScatterG = biome.scatterG,
                LightScatterB = biome.scatterB,
                DisplayNameUtf8Offset = displayNameOffset,
                HeatmapId = Hash(biome.heatmapId),
                RadiationFieldHash = Hash(biome.radiationId),
                DisplayNameUtf8ByteLength = (uint)displayNameBytes
            };
        }

        private static H8RecipeRecord ToRecipeRecord(JsonRecipe recipe)
        {
            ulong mask0 = 0UL;
            ulong mask1 = 0UL;
            uint h0 = 0u;
            uint h1 = 0u;
            uint h2 = 0u;
            uint h3 = 0u;
            int count = AddRecipeMaskAndHashes(recipe.ingredients, ref mask0, ref mask1, ref h0, ref h1, ref h2, ref h3);
            return new H8RecipeRecord
            {
                OutputHash = Hash(recipe.output),
                StationHash = Hash(recipe.station),
                Flags = recipe.flags,
                IngredientCount = (uint)count,
                IngredientMask0 = mask0,
                IngredientMask1 = mask1,
                IngredientHash0 = h0,
                IngredientHash1 = h1,
                IngredientHash2 = h2,
                IngredientHash3 = h3,
                CraftSeconds = recipe.craftSeconds,
                OutputCount = recipe.outputCount == 0u ? 1u : recipe.outputCount
            };
        }

        private sealed class LocalizationPool
        {
            private readonly Dictionary<string, uint> _offsetByValue = new Dictionary<string, uint>(StringComparer.Ordinal); // COLD ALLOC: Dictionary<string,uint>[source loc count] - editor-only localization pool de-duplication - owner: H8DataMonolithCompiler
            private readonly MemoryStream _bytes = new MemoryStream(4096); // COLD ALLOC: MemoryStream[4KB] - editor-only UTF-8 string block writer - owner: H8DataMonolithCompiler
            private readonly byte[] _scratch = new byte[Utf8ScratchBytes]; // COLD ALLOC: byte[2048] - editor-only UTF-8 encoding scratch - owner: H8DataMonolithCompiler

            internal uint Add(string value)
            {
                return Add(value, out _);
            }

            internal uint Add(string value, out int byteCount)
            {
                if (string.IsNullOrEmpty(value))
                {
                    byteCount = 0;
                    return uint.MaxValue;
                }

                byteCount = Encoding.UTF8.GetByteCount(value);
                if (byteCount > _scratch.Length)
                    throw new InvalidOperationException("[H8DataMonolithCompiler] UTF-8 localization entry exceeds scratch capacity: bytes=" + byteCount);

                if (_offsetByValue.TryGetValue(value, out uint offset))
                    return offset;

                if (_bytes.Position > uint.MaxValue)
                    throw new InvalidOperationException("[H8DataMonolithCompiler] UTF-8 localization pool exceeded 4GB.");

                offset = (uint)_bytes.Position;
                int written = Encoding.UTF8.GetBytes(value, 0, value.Length, _scratch, 0);
                _bytes.Write(_scratch, 0, written);
                _bytes.WriteByte(0);
                _offsetByValue[value] = offset;
                return offset;
            }

            internal byte[] ToArray()
            {
                return _bytes.ToArray();
            }
        }

        private sealed class CsvRow
        {
            internal readonly Dictionary<string, string> Fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        private sealed class CsvFileRows
        {
            internal readonly string AbsolutePath;
            internal readonly List<CsvRow> Rows;

            internal CsvFileRows(string absolutePath, List<CsvRow> rows)
            {
                AbsolutePath = absolutePath;
                Rows = rows;
            }
        }

        private sealed class DataSet
        {
            internal readonly List<H8ItemRecord> Items = new List<H8ItemRecord>(256);
            internal readonly List<H8CreatureTraitRecord> Creatures = new List<H8CreatureTraitRecord>(128);
            internal readonly List<H8BiomeRecord> Biomes = new List<H8BiomeRecord>(64);
            internal readonly List<H8RecipeRecord> Recipes = new List<H8RecipeRecord>(256);
            internal readonly List<H8BiomeHeatmapCellRecord> BiomeHeatmap = new List<H8BiomeHeatmapCellRecord>(1024);
            internal readonly List<H8QuestNodeRecord> QuestNodes = new List<H8QuestNodeRecord>(128);
            internal readonly List<H8QuestEdgeRecord> QuestEdges = new List<H8QuestEdgeRecord>(256);
            internal readonly List<H8LootCdfRecord> LootCdf = new List<H8LootCdfRecord>(256);
            internal readonly List<CsvRow> RawLootRows = new List<CsvRow>(256);
            internal readonly List<CsvRow> RawEconomyRows = new List<CsvRow>(128);
            internal readonly List<H8VoxelMaterialRecord> VoxelMaterials = new List<H8VoxelMaterialRecord>(128);
            internal readonly List<H8AudioClipRegistryRecord> AudioClips = new List<H8AudioClipRegistryRecord>(256);
            internal readonly List<H8VfxScalarRecord> VfxScalars = new List<H8VfxScalarRecord>(128);
            internal readonly List<H8DepthPressureSampleRecord> DepthPressureCurve = new List<H8DepthPressureSampleRecord>(256);
            internal readonly List<H8ToolHeatCapacityRecord> ToolHeat = new List<H8ToolHeatCapacityRecord>(64);
            internal readonly List<H8SubmarineHullConstantRecord> HullConstants = new List<H8SubmarineHullConstantRecord>(64);
            internal readonly List<H8NarrativeTriggerRecord> NarrativeTriggers = new List<H8NarrativeTriggerRecord>(256);
            internal readonly List<H8PhysicsMaterialRecord> PhysicsMaterials = new List<H8PhysicsMaterialRecord>(64);
            internal readonly List<H8GhostModuleRecord> GhostModules = new List<H8GhostModuleRecord>(128);
            internal readonly List<H8RadiationIntensityCellRecord> RadiationCells = new List<H8RadiationIntensityCellRecord>(256);
            internal readonly List<H8SpawnCreditCostRecord> SpawnCredits = new List<H8SpawnCreditCostRecord>(128);
            internal readonly List<H8LightAttenuationSampleRecord> LightAttenuationCurve = new List<H8LightAttenuationSampleRecord>(256);
            internal readonly List<H8SopErrorRecord> SopErrors = new List<H8SopErrorRecord>(128);
            internal readonly List<H8HudLayoutRecord> HudLayouts = new List<H8HudLayoutRecord>(64);
            internal readonly List<H8SectorPageRecord> SectorPages = new List<H8SectorPageRecord>(64);
            internal readonly List<H8EconomyRecord> Economy = new List<H8EconomyRecord>(128);
            internal readonly List<H8PhysicsConstantsRecord> PhysicsConstants = new List<H8PhysicsConstantsRecord>(64);
        }

        [Serializable] private sealed class JsonRoot { public JsonItem[] items; public JsonCreature[] creatures; public JsonBiome[] biomes; public JsonRecipe[] recipes; }
        [Serializable] private sealed class JsonItem { public string id; public string category; public uint flags; public int maxStack = 1; public string recipe; public float massKg = 1f; public float volumeM3 = 0.001f; public float quality = 1f; public float heatCapacity; public string yieldId; public string name; public string description; }
        [Serializable] private sealed class JsonCreature { public string id; public uint mateMask; public uint biomeMask; public uint flags; public float aggression; public float metabolism = 1f; public float maxHealth = 1f; public float cruiseSpeed = 1f; public float burstSpeed = 1f; public float spawnCredit = 1f; public string name; public string lootTable; public float minDepth; public float maxDepth; }
        [Serializable] private sealed class JsonBiome { public string id; public uint flags; public string surfaceId; public float minDepth; public float maxDepth; public float temperatureC = 2f; public float pressureScalar = 1f; public float fogDensity; public float scatterR = 0.08f; public float scatterG = 0.18f; public float scatterB = 0.24f; public string name; public string heatmapId; public string radiationId; }
        [Serializable] private sealed class JsonRecipe { public string output; public string station; public uint flags; public string ingredients; public float craftSeconds = 1f; public uint outputCount = 1u; }
    }

    internal sealed class H8DataMonolithSourceWatcher : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            if (!TouchesSourceData(importedAssets) &&
                !TouchesSourceData(deletedAssets) &&
                !TouchesSourceData(movedAssets) &&
                !TouchesSourceData(movedFromAssetPaths))
            {
                return;
            }

            H8DataMonolithCompiler.BakeAll(logSummary: false);
        }

        private static bool TouchesSourceData(string[] paths)
        {
            if (paths == null)
                return false;

            for (int i = 0; i < paths.Length; i++)
                if (H8DataMonolithCompiler.IsSourcePath(paths[i]))
                    return true;

            return false;
        }
    }

    [InitializeOnLoad]
    internal static class H8DataMonolithFileSystemWatcher
    {
        private static FileSystemWatcher _sourceWatcher;
        private static FileSystemWatcher _balanceWatcher;
        private static int _pendingBake;

        static H8DataMonolithFileSystemWatcher()
        {
            EditorApplication.update -= DrainPendingBake;
            EditorApplication.update += DrainPendingBake;
            StartWatcher();
        }

        private static void StartWatcher()
        {
            StopWatcher();
            _sourceWatcher = StartWatcherFor(Path.GetFullPath("Assets/_SourceData"));
            _balanceWatcher = StartWatcherFor(Path.GetFullPath("Data/Balance"));
        }

        private static FileSystemWatcher StartWatcherFor(string absoluteSourceFolder)
        {
            Directory.CreateDirectory(absoluteSourceFolder);
            FileSystemWatcher watcher = new FileSystemWatcher(absoluteSourceFolder)
            {
                IncludeSubdirectories = true,
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size
            };
            watcher.Changed += HandleSourceChanged;
            watcher.Created += HandleSourceChanged;
            watcher.Deleted += HandleSourceChanged;
            watcher.Renamed += HandleSourceRenamed;
            watcher.EnableRaisingEvents = true;
            return watcher;
        }

        private static void StopWatcher()
        {
            StopWatcher(ref _sourceWatcher);
            StopWatcher(ref _balanceWatcher);
        }

        private static void StopWatcher(ref FileSystemWatcher watcher)
        {
            if (watcher == null)
                return;

            watcher.EnableRaisingEvents = false;
            watcher.Changed -= HandleSourceChanged;
            watcher.Created -= HandleSourceChanged;
            watcher.Deleted -= HandleSourceChanged;
            watcher.Renamed -= HandleSourceRenamed;
            watcher.Dispose();
            watcher = null;
        }

        private static void HandleSourceChanged(object sender, FileSystemEventArgs args)
        {
            if (IsDataSourcePath(args.FullPath))
                Interlocked.Exchange(ref _pendingBake, 1);
        }

        private static void HandleSourceRenamed(object sender, RenamedEventArgs args)
        {
            if (IsDataSourcePath(args.FullPath) || IsDataSourcePath(args.OldFullPath))
                Interlocked.Exchange(ref _pendingBake, 1);
        }

        private static bool IsDataSourcePath(string path)
        {
            if (string.IsNullOrEmpty(path))
                return false;

            return !H8DataMonolithCompiler.IsGeneratedBalancePath(path) &&
                   (path.EndsWith(".csv", StringComparison.OrdinalIgnoreCase) ||
                    path.EndsWith(".json", StringComparison.OrdinalIgnoreCase));
        }

        private static void DrainPendingBake()
        {
            if (Interlocked.Exchange(ref _pendingBake, 0) == 0)
                return;

            H8DataMonolithCompiler.BakeAll(logSummary: false);
        }
    }

    [InitializeOnLoad]
    internal static class H8DataMonolithHotReloadSocket
    {
        private const int Port = 48088;
        private const string ReloadPrefix = "RELOAD ";
        private static readonly object QueueLock = new object();
        private static TcpListener _listener;
        private static Thread _thread;
        private static string _pendingPath;
        private static int _running;

        static H8DataMonolithHotReloadSocket()
        {
            EditorApplication.playModeStateChanged -= HandlePlayModeStateChanged;
            EditorApplication.playModeStateChanged += HandlePlayModeStateChanged;
            EditorApplication.update -= DrainMainThread;
            EditorApplication.update += DrainMainThread;
        }

        internal static void NotifyBake(string outputAssetPath)
        {
            if (!EditorApplication.isPlaying)
                return;

            string absolutePath = Path.GetFullPath(outputAssetPath);
            if (!TrySendReload(absolutePath))
                QueueReload(absolutePath);
        }

        private static void HandlePlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode)
                Start();
            else if (state == PlayModeStateChange.ExitingPlayMode || state == PlayModeStateChange.EnteredEditMode)
                Stop();
        }

        private static void Start()
        {
            if (Interlocked.Exchange(ref _running, 1) == 1)
                return;

            try
            {
                _listener = new TcpListener(IPAddress.Loopback, Port);
                _listener.Start(4);
                _thread = new Thread(ListenLoop)
                {
                    IsBackground = true,
                    Name = "H8.DataMonolith.HotReload"
                };
                _thread.Start();
            }
            catch (Exception ex)
            {
                Interlocked.Exchange(ref _running, 0);
                Debug.LogWarning("[H8DataMonolithHotReloadSocket] Socket bridge unavailable: " + ex.Message);
            }
        }

        private static void Stop()
        {
            Interlocked.Exchange(ref _running, 0);
            try
            {
                _listener?.Stop();
            }
            catch (Exception)
            {
            }

            _listener = null;
            _thread = null;
            lock (QueueLock)
                _pendingPath = null;
        }

        private static void ListenLoop()
        {
            while (Volatile.Read(ref _running) != 0)
            {
                try
                {
                    TcpClient client = _listener.AcceptTcpClient();
                    using (client)
                    using (NetworkStream stream = client.GetStream())
                    using (StreamReader reader = new StreamReader(stream, Encoding.UTF8, false, 1024, false))
                    {
                        string line = reader.ReadLine();
                        if (!string.IsNullOrEmpty(line) && line.StartsWith(ReloadPrefix, StringComparison.Ordinal))
                            QueueReload(line.Substring(ReloadPrefix.Length));
                    }
                }
                catch (SocketException)
                {
                    return;
                }
                catch (ObjectDisposedException)
                {
                    return;
                }
                catch (Exception ex)
                {
                    Debug.LogWarning("[H8DataMonolithHotReloadSocket] Reload packet rejected: " + ex.Message);
                }
            }
        }

        private static bool TrySendReload(string absolutePath)
        {
            try
            {
                using TcpClient client = new TcpClient();
                client.Connect(IPAddress.Loopback, Port);
                using NetworkStream stream = client.GetStream();
                byte[] payload = Encoding.UTF8.GetBytes(ReloadPrefix + absolutePath + "\n");
                stream.Write(payload, 0, payload.Length);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static void QueueReload(string absolutePath)
        {
            lock (QueueLock)
                _pendingPath = absolutePath;
        }

        private static void DrainMainThread()
        {
            if (!EditorApplication.isPlaying)
                return;

            string path;
            lock (QueueLock)
            {
                path = _pendingPath;
                _pendingPath = null;
            }

            if (string.IsNullOrEmpty(path))
                return;

            if (!H8StaticDataArena.EditorHotReloadFromFile(path, out H8DataBlobLoadStatus status))
                Debug.LogWarning("[H8DataMonolithHotReloadSocket] Hot reload failed: " + status);
        }
    }
}
#endif
