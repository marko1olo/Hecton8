using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Hecton8.Data;

namespace Hecton8.Tools.DataMonolithBakeCli
{
    internal static class DataMonolithSourceInventoryProbe
    {
        private const string AgentId = "X_002";
        private const string AgentId1330 = "1330";
        private const string ReportRelativePath = "Docs/Reports/DATA_MONOLITH_SOURCE_INVENTORY_X_002.json";
        private const string ReportRelativePath1330 = "Docs/Reports/DATA_MONOLITH_SOURCE_INVENTORY_1330.json";
        private const string BlobRelativePath = "Assets/StreamingAssets/Hecton8/DataMonolith/static_data.h8bin";
        private const int FileProbeOk = 0;
        private const int FileProbeMissing = 1;
        private const int FileProbeReadFailed = 2;

        public static bool Run(string projectRoot)
        {
            if (string.IsNullOrEmpty(projectRoot) || !Directory.Exists(projectRoot))
                return false;

            string blobPath = Path.Combine(projectRoot, BlobRelativePath);
            BlobInventory blob = ReadBlobInventory(blobPath);
            List<CsvEntry> entries = BuildCsvInventory(projectRoot);
            Dictionary<string, int> totals = BuildTotals(entries, blob);

            var report = new SourceInventoryReport
            {
                Schema = "HECTON8_DATA_MONOLITH_SOURCE_INVENTORY_V5",
                Agent = AgentId,
                GeneratedUtc = DateTime.UtcNow.ToString("O"),
                ActiveBlob = blob,
                Totals = totals,
                BlockGroups = BuildBlockGroups(blob.Sections),
                CsvInventory = entries
            };

            string reportPath = Path.Combine(projectRoot, ReportRelativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(reportPath)!);
            string reportPath1330 = Path.Combine(projectRoot, ReportRelativePath1330);
            Directory.CreateDirectory(Path.GetDirectoryName(reportPath1330)!);
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                IncludeFields = false
            };
            File.WriteAllText(reportPath, JsonSerializer.Serialize(report, options));
            report.Agent = AgentId1330;
            File.WriteAllText(reportPath1330, JsonSerializer.Serialize(report, options));
            return blob.Exists && blob.HeaderValid && blob.DirectoryValid && blob.SectionTableValid;
        }

        private static BlobInventory ReadBlobInventory(string blobPath)
        {
            var blob = new BlobInventory
            {
                Path = BlobRelativePath,
                Exists = File.Exists(blobPath)
            };

            if (!blob.Exists)
            {
                blob.ReadFailureCode = FileProbeMissing;
                return blob;
            }

            if (!TryGetFileLength(blobPath, out long blobLength, out int readFailureCode))
            {
                blob.ReadFailureCode = readFailureCode;
                return blob;
            }

            blob.Readable = true;
            blob.ReadFailureCode = FileProbeOk;
            blob.Bytes = blobLength;
            if (blobLength < H8DataLayoutConstants.HeaderSizeBytes + H8DataLayoutConstants.DirectorySizeBytes)
                return blob;

            byte[] headerBuffer = new byte[H8DataLayoutConstants.HeaderSizeBytes];
            if (!TryReadExact(blobPath, 0L, headerBuffer, out readFailureCode))
            {
                blob.ReadFailureCode = readFailureCode;
                return blob;
            }

            ReadOnlySpan<byte> header = headerBuffer;
            uint magic = BinaryPrimitives.ReadUInt32LittleEndian(header.Slice(0, 4));
            ushort format = BinaryPrimitives.ReadUInt16LittleEndian(header.Slice(4, 2));
            ushort headerBytes = BinaryPrimitives.ReadUInt16LittleEndian(header.Slice(6, 2));
            ulong checksum = BinaryPrimitives.ReadUInt64LittleEndian(header.Slice(8, 8));
            uint blobBytes = BinaryPrimitives.ReadUInt32LittleEndian(header.Slice(16, 4));
            uint directoryOffset = BinaryPrimitives.ReadUInt32LittleEndian(header.Slice(20, 4));
            uint directoryBytes = BinaryPrimitives.ReadUInt32LittleEndian(header.Slice(24, 4));
            uint sectionTableOffset = BinaryPrimitives.ReadUInt32LittleEndian(header.Slice(28, 4));
            uint sectionCount = BinaryPrimitives.ReadUInt32LittleEndian(header.Slice(32, 4));
            uint flags = BinaryPrimitives.ReadUInt32LittleEndian(header.Slice(36, 4));
            uint schemaHash = BinaryPrimitives.ReadUInt32LittleEndian(header.Slice(48, 4));
            uint reserved0 = BinaryPrimitives.ReadUInt32LittleEndian(header.Slice(52, 4));
            uint reserved1 = BinaryPrimitives.ReadUInt32LittleEndian(header.Slice(56, 4));
            uint reserved2 = BinaryPrimitives.ReadUInt32LittleEndian(header.Slice(60, 4));

            blob.Header = new HeaderInventory
            {
                MagicHex = ToHex(magic),
                FormatVersion = format,
                HeaderBytes = headerBytes,
                Checksum64Hex = ToHex(checksum),
                BlobBytes = blobBytes,
                DirectoryOffset = directoryOffset,
                DirectoryBytes = directoryBytes,
                SectionTableOffset = sectionTableOffset,
                SectionCount = sectionCount,
                FlagsHex = ToHex(flags),
                SchemaHashHex = ToHex(schemaHash),
                Reserved0 = reserved0,
                Reserved1 = reserved1,
                Reserved2 = reserved2,
                LittleEndian = flags == H8DataLayoutConstants.BlobFlagLittleEndian
            };

            blob.HeaderValid =
                magic == H8DataLayoutConstants.BlobMagic &&
                format == H8DataLayoutConstants.FormatVersion &&
                headerBytes == H8DataLayoutConstants.HeaderSizeBytes &&
                blobBytes == blobLength &&
                directoryOffset == H8DataLayoutConstants.HeaderSizeBytes &&
                directoryBytes == H8DataLayoutConstants.DirectorySizeBytes &&
                sectionTableOffset == H8DataLayoutConstants.HeaderSizeBytes + H8DataLayoutConstants.DirectorySizeBytes &&
                flags == H8DataLayoutConstants.BlobFlagLittleEndian &&
                schemaHash == H8DataLayoutConstants.SchemaHash &&
                reserved0 == 0u &&
                reserved1 == 0u &&
                reserved2 == 0u;

            if ((ulong)directoryOffset + H8DataLayoutConstants.DirectorySizeBytes > (ulong)blobLength)
                return blob;

            byte[] directoryBuffer = new byte[H8DataLayoutConstants.DirectorySizeBytes];
            if (!TryReadExact(blobPath, directoryOffset, directoryBuffer, out readFailureCode))
            {
                blob.ReadFailureCode = readFailureCode;
                return blob;
            }

            ReadOnlySpan<byte> directory = directoryBuffer;
            uint directoryMagic = BinaryPrimitives.ReadUInt32LittleEndian(directory.Slice(0, 4));
            ushort directoryFormat = BinaryPrimitives.ReadUInt16LittleEndian(directory.Slice(4, 2));
            ushort directorySectionCount = BinaryPrimitives.ReadUInt16LittleEndian(directory.Slice(6, 2));
            uint directorySectionTableOffset = BinaryPrimitives.ReadUInt32LittleEndian(directory.Slice(8, 4));
            uint directorySectionTableBytes = BinaryPrimitives.ReadUInt32LittleEndian(directory.Slice(12, 4));
            uint directoryBlobBytes = BinaryPrimitives.ReadUInt32LittleEndian(directory.Slice(16, 4));
            uint dataStart = BinaryPrimitives.ReadUInt32LittleEndian(directory.Slice(20, 4));
            uint localizationOffset = BinaryPrimitives.ReadUInt32LittleEndian(directory.Slice(24, 4));
            uint localizationBytes = BinaryPrimitives.ReadUInt32LittleEndian(directory.Slice(28, 4));
            uint directoryFlags = BinaryPrimitives.ReadUInt32LittleEndian(directory.Slice(32, 4));
            uint directoryReserved0 = BinaryPrimitives.ReadUInt32LittleEndian(directory.Slice(44, 4));
            uint directoryReserved1 = BinaryPrimitives.ReadUInt32LittleEndian(directory.Slice(48, 4));
            uint directoryReserved2 = BinaryPrimitives.ReadUInt32LittleEndian(directory.Slice(52, 4));
            uint directoryReserved3 = BinaryPrimitives.ReadUInt32LittleEndian(directory.Slice(56, 4));
            uint directoryReserved4 = BinaryPrimitives.ReadUInt32LittleEndian(directory.Slice(60, 4));

            blob.Directory = new DirectoryInventory
            {
                MagicHex = ToHex(directoryMagic),
                FormatVersion = directoryFormat,
                SectionCount = directorySectionCount,
                SectionTableOffset = directorySectionTableOffset,
                SectionTableBytes = directorySectionTableBytes,
                BlobBytes = directoryBlobBytes,
                DataStartOffset = dataStart,
                LocalizationOffset = localizationOffset,
                LocalizationBytes = localizationBytes,
                FlagsHex = ToHex(directoryFlags),
                Reserved0 = directoryReserved0,
                Reserved1 = directoryReserved1,
                Reserved2 = directoryReserved2,
                Reserved3 = directoryReserved3,
                Reserved4 = directoryReserved4
            };

            blob.DirectoryValid =
                directoryMagic == H8DataLayoutConstants.BlobMagic &&
                directoryFormat == H8DataLayoutConstants.FormatVersion &&
                directorySectionCount == sectionCount &&
                directorySectionTableOffset == sectionTableOffset &&
                directorySectionTableBytes == sectionCount * 16u &&
                directoryBlobBytes == blobLength &&
                dataStart >= sectionTableOffset + directorySectionTableBytes &&
                dataStart % H8DataLayoutConstants.SectionAlignmentBytes == 0u &&
                directoryFlags == flags &&
                directoryReserved0 == 0u &&
                directoryReserved1 == 0u &&
                directoryReserved2 == 0u &&
                directoryReserved3 == 0u &&
                directoryReserved4 == 0u;

            ulong tableEnd = (ulong)sectionTableOffset + ((ulong)sectionCount * 16UL);
            if (sectionTableOffset > blobLength || tableEnd > (ulong)blobLength || sectionCount > 4096u)
                return blob;

            byte[] sectionTableBuffer = new byte[(int)sectionCount * 16];
            if (sectionTableBuffer.Length > 0 && !TryReadExact(blobPath, sectionTableOffset, sectionTableBuffer, out readFailureCode))
            {
                blob.ReadFailureCode = readFailureCode;
                return blob;
            }

            ReadOnlySpan<byte> sectionTable = sectionTableBuffer;
            uint expectedCursor = AlignUp(dataStart, H8DataLayoutConstants.SectionAlignmentBytes);
            bool sectionTableValid = true;
            var sections = new List<SectionInventory>((int)Math.Min(sectionCount, 256u));
            for (uint i = 0; i < sectionCount; i++)
            {
                int entryOffset = (int)i * 16;
                uint sectionId = BinaryPrimitives.ReadUInt32LittleEndian(sectionTable.Slice(entryOffset, 4));
                uint recordSize = BinaryPrimitives.ReadUInt32LittleEndian(sectionTable.Slice(entryOffset + 4, 4));
                uint count = BinaryPrimitives.ReadUInt32LittleEndian(sectionTable.Slice(entryOffset + 8, 4));
                uint offset = BinaryPrimitives.ReadUInt32LittleEndian(sectionTable.Slice(entryOffset + 12, 4));
                ulong payloadBytes = (ulong)recordSize * count;
                ulong end = (ulong)offset + payloadBytes;
                bool emptySection = payloadBytes == 0UL;
                bool aligned64 = offset % H8DataLayoutConstants.SectionAlignmentBytes == 0u;
                bool recordAligned16 =
                    sectionId == (uint)H8DataSectionId.LocalizationUtf8 ||
                    recordSize == 0u ||
                    recordSize % H8DataLayoutConstants.RecordAlignmentBytes == 0u;
                bool rangeValid = end <= (ulong)blobLength;
                bool canonical = emptySection ? offset == 0u : offset == expectedCursor;

                if (!aligned64 || !recordAligned16 || !rangeValid || !canonical)
                    sectionTableValid = false;

                sections.Add(new SectionInventory
                {
                    Index = i,
                    SectionId = sectionId,
                    Name = Enum.IsDefined(typeof(H8DataSectionId), sectionId) ? ((H8DataSectionId)sectionId).ToString() : "Unknown",
                    RecordSize = recordSize,
                    Count = count,
                    OffsetBytes = offset,
                    PayloadBytes = payloadBytes,
                    EndExclusive = end,
                    OffsetAligned64 = aligned64,
                    RecordAligned16 = recordAligned16,
                    RangeValid = rangeValid,
                    CanonicalCursor = canonical
                });

                if (!emptySection)
                    expectedCursor = AlignUp((uint)Math.Min(end, uint.MaxValue), H8DataLayoutConstants.SectionAlignmentBytes);
            }

            blob.Sections = sections;
            blob.SectionTableValid = sectionTableValid;
            return blob;
        }

        private static List<CsvEntry> BuildCsvInventory(string projectRoot)
        {
            string scriptsRoot = Path.Combine(projectRoot, "Assets", "_Project", "Scripts");
            string[] scriptFiles = Directory.Exists(scriptsRoot)
                ? Directory.GetFiles(scriptsRoot, "*.cs", SearchOption.AllDirectories)
                : Array.Empty<string>();

            var entries = new List<CsvEntry>(256);
            foreach (string csvPath in Directory.GetFiles(projectRoot, "*.csv", SearchOption.AllDirectories))
            {
                string relativePath = ToProjectPath(Path.GetRelativePath(projectRoot, csvPath));
                if (ShouldSkip(relativePath))
                    continue;

                string fileName = Path.GetFileName(csvPath);
                CsvReferenceSummary references = FindCodeReferences(scriptFiles, fileName, projectRoot);
                string classification = ClassifyCsv(relativePath, references);
                string authority = ResolveAuthority(classification);
                long bytes = GetFileLengthOrZero(csvPath, out int fileReadFailureCode);
                entries.Add(new CsvEntry
                {
                    Path = relativePath,
                    Bytes = bytes,
                    FileReadFailureCode = fileReadFailureCode,
                    Classification = classification,
                    Authority = authority,
                    CodeReferenceCount = references.AllReferenceCount,
                    PlayerActiveCodeReferenceCount = references.PlayerActiveReferenceCount,
                    ReleaseActiveCodeReferenceCount = references.ReleaseActiveReferenceCount,
                    DevelopmentActiveCodeReferenceCount = references.DevelopmentActiveReferenceCount,
                    EditorOnlyCodeReferenceCount = references.EditorOnlyReferenceCount,
                    CodeReferences = references.AllReferences.ToArray(),
                    PlayerActiveCodeReferences = references.PlayerActiveReferences.ToArray()
                });
            }

            entries.Sort((left, right) => string.CompareOrdinal(left.Path, right.Path));
            return entries;
        }

        private static Dictionary<string, int> BuildTotals(List<CsvEntry> entries, BlobInventory blob)
        {
            return new Dictionary<string, int>
            {
                ["csvAll"] = entries.Count,
                ["repoRootCsvStaticConfigRisk"] = entries.Count(e => e.Classification == "repo_root_csv_static_config_risk"),
                ["streamingAssetsCsvStaticConfigRisk"] = entries.Count(e => e.Classification == "streaming_assets_csv_static_config_risk"),
                ["repoRootUnreferencedLegacyCsv"] = entries.Count(e => e.Classification == "repo_root_unref_legacy_csv"),
                ["editorFencedRepoRootAuthoringCsv"] = entries.Count(e => e.Classification == "editor_fenced_repo_root_authoring_csv"),
                ["editorFencedStreamingAssetsAuthoringCsv"] = entries.Count(e => e.Classification == "editor_fenced_streaming_assets_authoring_csv"),
                ["monolithSourceCsv"] = entries.Count(e => e.Classification == "monolith_source_csv"),
                ["editorOnlyAuthoringCsv"] = entries.Count(e => e.Classification == "editor_only_authoring_csv"),
                ["crossDomainAuthoringSource"] = entries.Count(e => e.Classification == "cross_domain_authoring_source"),
                ["docsArchiveReportCsv"] = entries.Count(e => e.Classification == "docs_archive_report_csv"),
                ["otherCsv"] = entries.Count(e => e.Classification == "other_csv"),
                ["playerActiveCsvCodeReferences"] = entries.Sum(e => e.PlayerActiveCodeReferenceCount),
                ["activeBlobBytes"] = blob.Bytes > int.MaxValue ? int.MaxValue : (int)blob.Bytes,
                ["activeBlobSections"] = blob.Sections.Count,
                ["activeBlobCacheLineAlignedSections"] = blob.Sections.Count(s => s.OffsetAligned64),
                ["activeBlobRecordAlignedSections"] = blob.Sections.Count(s => s.RecordAligned16)
            };
        }

        private static List<BlockGroupInventory> BuildBlockGroups(List<SectionInventory> sections)
        {
            return new List<BlockGroupInventory>
            {
                BuildBlockGroup("Ecology", sections, H8DataSectionId.Creatures, H8DataSectionId.Biomes, H8DataSectionId.BiomeHeatmap, H8DataSectionId.SpawnCreditCosts),
                BuildBlockGroup("Crafting", sections, H8DataSectionId.Items, H8DataSectionId.Recipes, H8DataSectionId.ToolHeatCapacity, H8DataSectionId.SubmarineHullConstants, H8DataSectionId.GhostModules),
                BuildBlockGroup("Audio", sections, H8DataSectionId.AudioClipRegistry, H8DataSectionId.SopErrors),
                BuildBlockGroup("Physiology", sections, H8DataSectionId.DepthPressureCurve, H8DataSectionId.RadiationIntensityMap, H8DataSectionId.PhysicsMaterials, H8DataSectionId.PhysicsConstants)
            };
        }

        private static BlockGroupInventory BuildBlockGroup(string name, List<SectionInventory> sections, params H8DataSectionId[] ids)
        {
            var selected = sections
                .Where(s => s.PayloadBytes > 0UL && ids.Any(id => s.SectionId == (uint)id))
                .OrderBy(s => s.OffsetBytes)
                .ToList();
            if (selected.Count == 0)
                return new BlockGroupInventory { Name = name, Present = false };

            ulong start = selected.Min(s => (ulong)s.OffsetBytes);
            ulong end = selected.Max(s => s.EndExclusive);
            return new BlockGroupInventory
            {
                Name = name,
                Present = true,
                StartOffset = start,
                EndExclusive = end,
                Bytes = end >= start ? end - start : 0UL,
                CacheLineAligned = selected.All(s => s.OffsetAligned64),
                Sections = selected.Select(s => s.Name).ToArray()
            };
        }

        private static string ClassifyCsv(string relativePath, CsvReferenceSummary references)
        {
            if (relativePath.StartsWith("Docs/", StringComparison.OrdinalIgnoreCase) ||
                relativePath.Contains("/Docs/", StringComparison.OrdinalIgnoreCase) ||
                relativePath.Contains("/Reports/", StringComparison.OrdinalIgnoreCase) ||
                relativePath.Contains("/Archive/", StringComparison.OrdinalIgnoreCase))
            {
                return "docs_archive_report_csv";
            }

            if (relativePath.StartsWith("Data/Balance/", StringComparison.OrdinalIgnoreCase) ||
                relativePath.StartsWith("Assets/_SourceData/DataMonolith/", StringComparison.OrdinalIgnoreCase))
            {
                return "monolith_source_csv";
            }

            if (relativePath.StartsWith("Assets/StreamingAssets/", StringComparison.OrdinalIgnoreCase))
            {
                return references.PlayerActiveReferenceCount == 0 && references.AllReferenceCount > 0
                    ? "editor_fenced_streaming_assets_authoring_csv"
                    : "streaming_assets_csv_static_config_risk";
            }

            if (!relativePath.Contains("/", StringComparison.Ordinal))
            {
                if (references.PlayerActiveReferenceCount == 0)
                {
                    return references.AllReferenceCount > 0
                        ? "editor_fenced_repo_root_authoring_csv"
                        : "repo_root_unref_legacy_csv";
                }

                return "repo_root_csv_static_config_risk";
            }

            if (relativePath.Contains("/Editor/", StringComparison.OrdinalIgnoreCase) ||
                relativePath.EndsWith("/Editor", StringComparison.OrdinalIgnoreCase))
            {
                return "editor_only_authoring_csv";
            }

            if (relativePath.StartsWith("Assets/_Project/Data/", StringComparison.OrdinalIgnoreCase) ||
                relativePath.StartsWith("Assets/_Project/", StringComparison.OrdinalIgnoreCase))
            {
                return "cross_domain_authoring_source";
            }

            return "other_csv";
        }

        private static string ResolveAuthority(string classification)
        {
            return classification switch
            {
                "monolith_source_csv" => "compiled_into_static_data_h8bin",
                "docs_archive_report_csv" => "documentation_only",
                "editor_only_authoring_csv" => "editor_only_authoring",
                "editor_fenced_repo_root_authoring_csv" => "editor_only_authoring_bridge_player_inactive",
                "editor_fenced_streaming_assets_authoring_csv" => "editor_only_authoring_bridge_player_inactive",
                "repo_root_unref_legacy_csv" => "owner_disposition_required_no_player_reference",
                "repo_root_csv_static_config_risk" => "owner_migration_or_editor_fence_required",
                "streaming_assets_csv_static_config_risk" => "owner_migration_or_editor_fence_required",
                _ => "owner_migration_required"
            };
        }

        private static CsvReferenceSummary FindCodeReferences(string[] scriptFiles, string fileName, string projectRoot)
        {
            var summary = new CsvReferenceSummary();
            if (string.IsNullOrEmpty(fileName))
                return summary;

            foreach (string scriptPath in scriptFiles)
            {
                string[] lines;
                try
                {
                    lines = File.ReadAllLines(scriptPath);
                }
                catch (IOException)
                {
                    continue;
                }

                string relativePath = ToProjectPath(Path.GetRelativePath(projectRoot, scriptPath));
                bool editorPath = IsEditorOrTestPath(relativePath);
                bool fileHasReference = false;
                bool releaseActive = false;
                bool developmentActive = false;
                bool editorOnlyReference = false;
                var releaseStack = new List<PreprocessorFrame>(8);
                var developmentStack = new List<PreprocessorFrame>(8);

                for (int i = 0; i < lines.Length; i++)
                {
                    string sourceLine = StripLineComment(lines[i]);
                    string trimmed = sourceLine.TrimStart();
                    if (trimmed.StartsWith("#", StringComparison.Ordinal))
                    {
                        ApplyPreprocessorDirective(trimmed, releaseStack, developmentBuild: false);
                        ApplyPreprocessorDirective(trimmed, developmentStack, developmentBuild: true);
                        continue;
                    }

                    if (lines[i].IndexOf(fileName, StringComparison.OrdinalIgnoreCase) < 0)
                        continue;

                    fileHasReference = true;
                    bool lineReleaseActive = !editorPath && IsPlayerLineActive(releaseStack);
                    bool lineDevelopmentActive = !editorPath && IsPlayerLineActive(developmentStack);
                    releaseActive |= lineReleaseActive;
                    developmentActive |= lineDevelopmentActive;
                    editorOnlyReference |= editorPath || (!lineReleaseActive && !lineDevelopmentActive);
                }

                if (!fileHasReference)
                    continue;

                summary.AllReferenceCount++;
                if (summary.AllReferences.Count < 8)
                    summary.AllReferences.Add(relativePath);

                if (releaseActive)
                    summary.ReleaseActiveReferenceCount++;
                if (developmentActive)
                    summary.DevelopmentActiveReferenceCount++;

                if (releaseActive || developmentActive)
                {
                    summary.PlayerActiveReferenceCount++;
                    if (summary.PlayerActiveReferences.Count < 8)
                        summary.PlayerActiveReferences.Add(relativePath);
                }

                if (editorOnlyReference && !releaseActive && !developmentActive)
                    summary.EditorOnlyReferenceCount++;
            }

            return summary;
        }

        private static bool IsEditorOrTestPath(string relativePath)
        {
            string normalized = relativePath.Replace('\\', '/');
            return normalized.IndexOf("/Editor/", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   normalized.IndexOf("/Tests/", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   normalized.IndexOf("/EditorValidation/", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   normalized.EndsWith(".Editor.cs", StringComparison.OrdinalIgnoreCase);
        }

        private static void ApplyPreprocessorDirective(string trimmed, List<PreprocessorFrame> stack, bool developmentBuild)
        {
            if (trimmed.StartsWith("#if", StringComparison.Ordinal))
            {
                string expression = trimmed.Length > 3 ? trimmed.Substring(3).Trim() : string.Empty;
                stack.Add(new PreprocessorFrame(EvaluateForPlayer(expression, developmentBuild)));
                return;
            }

            if (trimmed.StartsWith("#elif", StringComparison.Ordinal))
            {
                if (stack.Count == 0)
                    return;

                PreprocessorFrame frame = stack[stack.Count - 1];
                if (frame.BranchTaken)
                {
                    frame.CurrentActive = false;
                }
                else
                {
                    string expression = trimmed.Length > 5 ? trimmed.Substring(5).Trim() : string.Empty;
                    bool active = EvaluateForPlayer(expression, developmentBuild);
                    frame.CurrentActive = active;
                    frame.BranchTaken = active;
                }

                stack[stack.Count - 1] = frame;
                return;
            }

            if (trimmed.StartsWith("#else", StringComparison.Ordinal))
            {
                if (stack.Count == 0)
                    return;

                PreprocessorFrame frame = stack[stack.Count - 1];
                frame.CurrentActive = !frame.BranchTaken;
                frame.BranchTaken = true;
                stack[stack.Count - 1] = frame;
                return;
            }

            if (trimmed.StartsWith("#endif", StringComparison.Ordinal) && stack.Count > 0)
                stack.RemoveAt(stack.Count - 1);
        }

        private static bool IsPlayerLineActive(List<PreprocessorFrame> stack)
        {
            for (int i = 0; i < stack.Count; i++)
            {
                if (!stack[i].CurrentActive)
                    return false;
            }

            return true;
        }

        private static bool EvaluateForPlayer(string expression, bool developmentBuild)
        {
            if (string.IsNullOrWhiteSpace(expression))
                return false;

            return new PreprocessorExpressionParser(expression, developmentBuild).ParseExpression();
        }

        private static string StripLineComment(string line)
        {
            bool inString = false;
            bool verbatim = false;
            for (int i = 0; i < line.Length - 1; i++)
            {
                char c = line[i];
                if (!inString && c == '@' && line[i + 1] == '"')
                {
                    inString = true;
                    verbatim = true;
                    i++;
                    continue;
                }

                if (c == '"' && (i == 0 || line[i - 1] != '\\' || verbatim))
                {
                    if (inString && verbatim && i + 1 < line.Length && line[i + 1] == '"')
                    {
                        i++;
                        continue;
                    }

                    inString = !inString;
                    if (!inString)
                        verbatim = false;
                    continue;
                }

                if (!inString && c == '/' && line[i + 1] == '/')
                    return line.Substring(0, i);
            }

            return line;
        }

        private static bool ShouldSkip(string relativePath)
        {
            return relativePath.StartsWith(".git/", StringComparison.OrdinalIgnoreCase) ||
                   relativePath.StartsWith("Library/", StringComparison.OrdinalIgnoreCase) ||
                   relativePath.StartsWith("Temp/", StringComparison.OrdinalIgnoreCase) ||
                   relativePath.StartsWith("Obj/", StringComparison.OrdinalIgnoreCase) ||
                   relativePath.StartsWith("Logs/", StringComparison.OrdinalIgnoreCase) ||
                   relativePath.StartsWith(".codexbuild/", StringComparison.OrdinalIgnoreCase);
        }

        private static bool TryGetFileLength(string path, out long length, out int failureCode)
        {
            length = 0L;
            failureCode = FileProbeOk;
            try
            {
                length = new FileInfo(path).Length;
                return true;
            }
            catch (ArgumentException)
            {
                failureCode = FileProbeReadFailed;
                return false;
            }
            catch (NotSupportedException)
            {
                failureCode = FileProbeReadFailed;
                return false;
            }
            catch (PathTooLongException)
            {
                failureCode = FileProbeReadFailed;
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                failureCode = FileProbeReadFailed;
                return false;
            }
            catch (IOException)
            {
                failureCode = FileProbeReadFailed;
                return false;
            }
            catch (System.Security.SecurityException)
            {
                failureCode = FileProbeReadFailed;
                return false;
            }
        }

        private static long GetFileLengthOrZero(string path, out int failureCode)
        {
            return TryGetFileLength(path, out long length, out failureCode) ? length : 0L;
        }

        private static bool TryReadExact(string path, long offset, byte[] buffer, out int failureCode)
        {
            failureCode = FileProbeOk;
            try
            {
                using FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                if (offset != 0L)
                    stream.Seek(offset, SeekOrigin.Begin);

                int total = 0;
                while (total < buffer.Length)
                {
                    int read = stream.Read(buffer, total, buffer.Length - total);
                    if (read <= 0)
                    {
                        failureCode = FileProbeReadFailed;
                        return false;
                    }

                    total += read;
                }

                return true;
            }
            catch (ArgumentException)
            {
                failureCode = FileProbeReadFailed;
                return false;
            }
            catch (NotSupportedException)
            {
                failureCode = FileProbeReadFailed;
                return false;
            }
            catch (PathTooLongException)
            {
                failureCode = FileProbeReadFailed;
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                failureCode = FileProbeReadFailed;
                return false;
            }
            catch (IOException)
            {
                failureCode = FileProbeReadFailed;
                return false;
            }
            catch (System.Security.SecurityException)
            {
                failureCode = FileProbeReadFailed;
                return false;
            }
        }

        private static uint AlignUp(uint value, int alignment)
        {
            uint mask = (uint)(alignment - 1);
            return (value + mask) & ~mask;
        }

        private static string ToProjectPath(string path)
        {
            return path.Replace('\\', '/');
        }

        private static string ToHex(uint value)
        {
            return "0x" + value.ToString("X8");
        }

        private static string ToHex(ulong value)
        {
            return "0x" + value.ToString("X16");
        }

        private sealed class SourceInventoryReport
        {
            public string Schema { get; set; } = string.Empty;
            public string Agent { get; set; } = string.Empty;
            public string GeneratedUtc { get; set; } = string.Empty;
            public BlobInventory ActiveBlob { get; set; } = new BlobInventory();
            public Dictionary<string, int> Totals { get; set; } = new Dictionary<string, int>();
            public List<BlockGroupInventory> BlockGroups { get; set; } = new List<BlockGroupInventory>();
            public List<CsvEntry> CsvInventory { get; set; } = new List<CsvEntry>();
        }

        private sealed class CsvEntry
        {
            public string Path { get; set; } = string.Empty;
            public long Bytes { get; set; }
            public int FileReadFailureCode { get; set; }
            public string Classification { get; set; } = string.Empty;
            public string Authority { get; set; } = string.Empty;
            public int CodeReferenceCount { get; set; }
            public int PlayerActiveCodeReferenceCount { get; set; }
            public int ReleaseActiveCodeReferenceCount { get; set; }
            public int DevelopmentActiveCodeReferenceCount { get; set; }
            public int EditorOnlyCodeReferenceCount { get; set; }
            public string[] CodeReferences { get; set; } = Array.Empty<string>();
            public string[] PlayerActiveCodeReferences { get; set; } = Array.Empty<string>();
        }

        private sealed class CsvReferenceSummary
        {
            public int AllReferenceCount;
            public int PlayerActiveReferenceCount;
            public int ReleaseActiveReferenceCount;
            public int DevelopmentActiveReferenceCount;
            public int EditorOnlyReferenceCount;
            public List<string> AllReferences { get; } = new List<string>(8);
            public List<string> PlayerActiveReferences { get; } = new List<string>(8);
        }

        private sealed class BlobInventory
        {
            public string Path { get; set; } = BlobRelativePath;
            public bool Exists { get; set; }
            public bool Readable { get; set; }
            public int ReadFailureCode { get; set; }
            public long Bytes { get; set; }
            public bool HeaderValid { get; set; }
            public bool DirectoryValid { get; set; }
            public bool SectionTableValid { get; set; }
            public HeaderInventory Header { get; set; } = new HeaderInventory();
            public DirectoryInventory Directory { get; set; } = new DirectoryInventory();
            public List<SectionInventory> Sections { get; set; } = new List<SectionInventory>();
        }

        private sealed class HeaderInventory
        {
            public string MagicHex { get; set; } = string.Empty;
            public ushort FormatVersion { get; set; }
            public ushort HeaderBytes { get; set; }
            public string Checksum64Hex { get; set; } = string.Empty;
            public uint BlobBytes { get; set; }
            public uint DirectoryOffset { get; set; }
            public uint DirectoryBytes { get; set; }
            public uint SectionTableOffset { get; set; }
            public uint SectionCount { get; set; }
            public string FlagsHex { get; set; } = string.Empty;
            public string SchemaHashHex { get; set; } = string.Empty;
            public bool LittleEndian { get; set; }
            public uint Reserved0 { get; set; }
            public uint Reserved1 { get; set; }
            public uint Reserved2 { get; set; }
        }

        private sealed class DirectoryInventory
        {
            public string MagicHex { get; set; } = string.Empty;
            public ushort FormatVersion { get; set; }
            public ushort SectionCount { get; set; }
            public uint SectionTableOffset { get; set; }
            public uint SectionTableBytes { get; set; }
            public uint BlobBytes { get; set; }
            public uint DataStartOffset { get; set; }
            public uint LocalizationOffset { get; set; }
            public uint LocalizationBytes { get; set; }
            public string FlagsHex { get; set; } = string.Empty;
            public uint Reserved0 { get; set; }
            public uint Reserved1 { get; set; }
            public uint Reserved2 { get; set; }
            public uint Reserved3 { get; set; }
            public uint Reserved4 { get; set; }
        }

        private sealed class SectionInventory
        {
            public uint Index { get; set; }
            public uint SectionId { get; set; }
            public string Name { get; set; } = string.Empty;
            public uint RecordSize { get; set; }
            public uint Count { get; set; }
            public uint OffsetBytes { get; set; }
            public ulong PayloadBytes { get; set; }
            public ulong EndExclusive { get; set; }
            public bool OffsetAligned64 { get; set; }
            public bool RecordAligned16 { get; set; }
            public bool RangeValid { get; set; }
            public bool CanonicalCursor { get; set; }
        }

        private sealed class BlockGroupInventory
        {
            public string Name { get; set; } = string.Empty;
            public bool Present { get; set; }
            public ulong StartOffset { get; set; }
            public ulong EndExclusive { get; set; }
            public ulong Bytes { get; set; }
            public bool CacheLineAligned { get; set; }
            public string[] Sections { get; set; } = Array.Empty<string>();
        }

        private struct PreprocessorFrame
        {
            public bool CurrentActive;
            public bool BranchTaken;

            public PreprocessorFrame(bool active)
            {
                CurrentActive = active;
                BranchTaken = active;
            }
        }

        private struct PreprocessorExpressionParser
        {
            private readonly string _text;
            private readonly bool _developmentBuild;
            private int _index;

            public PreprocessorExpressionParser(string text, bool developmentBuild)
            {
                _text = text;
                _developmentBuild = developmentBuild;
                _index = 0;
            }

            public bool ParseExpression()
            {
                return ParseOr();
            }

            private bool ParseOr()
            {
                bool value = ParseAnd();
                while (true)
                {
                    SkipWhite();
                    if (!TryConsume("||"))
                        return value;

                    bool right = ParseAnd();
                    value = value || right;
                }
            }

            private bool ParseAnd()
            {
                bool value = ParseUnary();
                while (true)
                {
                    SkipWhite();
                    if (!TryConsume("&&"))
                        return value;

                    bool right = ParseUnary();
                    value = value && right;
                }
            }

            private bool ParseUnary()
            {
                SkipWhite();
                if (TryConsume("!"))
                    return !ParseUnary();

                if (TryConsume("("))
                {
                    bool value = ParseOr();
                    SkipWhite();
                    TryConsume(")");
                    return value;
                }

                return SymbolValueForPlayer(ReadSymbol());
            }

            private string ReadSymbol()
            {
                SkipWhite();
                int start = _index;
                while (_index < _text.Length)
                {
                    char c = _text[_index];
                    if (!char.IsLetterOrDigit(c) && c != '_')
                        break;
                    _index++;
                }

                return start == _index ? string.Empty : _text.Substring(start, _index - start);
            }

            private bool TryConsume(string token)
            {
                if (_index + token.Length > _text.Length)
                    return false;

                for (int i = 0; i < token.Length; i++)
                {
                    if (_text[_index + i] != token[i])
                        return false;
                }

                _index += token.Length;
                return true;
            }

            private void SkipWhite()
            {
                while (_index < _text.Length && char.IsWhiteSpace(_text[_index]))
                    _index++;
            }

            private bool SymbolValueForPlayer(string symbol)
            {
                if (string.Equals(symbol, "UNITY_EDITOR", StringComparison.Ordinal))
                    return false;

                if (string.Equals(symbol, "DEVELOPMENT_BUILD", StringComparison.Ordinal) ||
                    string.Equals(symbol, "DEBUG", StringComparison.Ordinal))
                {
                    return _developmentBuild;
                }

                if (string.Equals(symbol, "true", StringComparison.OrdinalIgnoreCase))
                    return true;

                if (string.Equals(symbol, "false", StringComparison.OrdinalIgnoreCase) || string.IsNullOrEmpty(symbol))
                    return false;

                return true;
            }
        }
    }
}
