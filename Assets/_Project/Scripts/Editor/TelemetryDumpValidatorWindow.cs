using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Hecton8.Core;
using Hecton8.Core.Memory;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Hecton8.EditorTools
{
    public sealed class TelemetryDumpValidatorWindow : EditorWindow
    {
        private const int MaxDisplayedFrames = 300;
        private const uint HazardZoneDumpMagic = 0x4838485Au; // H8HZ
        private const int HazardZoneDumpHeaderBytes = 24;
        private const uint GlobalTelemetryDumpMagic = 0x4838444Du; // H8DM
        private const int GlobalTelemetryMetadataOffset = 16;
        private const int GlobalTelemetryDumpHeaderBytes = 1024;
        private const int GlobalTelemetryHashHistoryOffsetBytes = 64;
        private const int GlobalTelemetryHashHistoryCount = 100;
        private const int GlobalTelemetrySourcePayloadOffsetBytes = 512;
        private const int GlobalTelemetrySourceDescriptorWords = 4;
        private const int GlobalTelemetrySourcePayloadStrideBytes = 64;
        private const uint GlobalTelemetrySurvivalSourceHash = 0x53555256u; // SURV
        private const int GlobalTelemetrySurvivalDeathCauseShift = 24;
        private const ulong CrashTelemetryDumpMagic = 0x00384E4F54434548UL; // HECTON8\0
        private const int CrashTelemetryDumpHeaderBytes = 16;
        private const int CrashTelemetryDumpEntrySizeBytes = 64;
        private const float CrashTelemetrySpikeFrameTimeSeconds = 0.033f;
        private const uint CrashTelemetryMemoryFaultMask =
            (1u << 23) |
            (1u << 24) |
            (1u << 25) |
            (1u << 26) |
            (1u << 27) |
            (1u << 28);
        private const ulong JobAdmissionDumpMagic = 0x00384E4F54434548UL; // HECTON8\0
        private const int JobAdmissionDumpHeaderBytes = 32;
        private const int JobAdmissionDumpEntrySizeBytes = 64;
        private const uint JobAdmissionDumpMinVersion = 1u;
        private const uint JobAdmissionDumpMaxVersion = 2u;
        private const ulong SimulationBucketDumpMagic = 0x00384E4F54434548UL; // HECTON8\0
        private const int SimulationBucketDumpHeaderBytes = 32;
        private const int SimulationBucketDumpEntrySizeBytes = 64;
        private const uint SimulationBucketDumpVersion = 1u;
        private const ulong TerrainStreamingDumpMagic = 0x00384E4F54434548UL; // HECTON8\0
        private const int TerrainStreamingLegacyPagerDumpHeaderBytes = 24;
        private const int TerrainStreamingPagerDumpHeaderBytes = 32;
        private const int TerrainStreamingDumpEntrySizeBytes = 64;
        private const uint TerrainStreamingPagerDumpVersion = 1305u;
        private const uint TerrainStreamingPagerDumpLayoutHash = 0x44504354u; // TCPD
        private const int WorldChunkResidencyDumpHeaderBytes = 32;
        private const uint WorldChunkResidencyDumpVersion = 1u;
        private const uint WorldChunkResidencyDumpLayoutHash = 0x44524357u; // WCRD
        private const string TerrainStreamingLegacyDumpFileName = "Dump_1305_Streaming.bin";
        private const string TerrainStreamingPagerDumpFileName = "Dump_1305_TerrainChunkPager.bin";
        private const string WorldChunkResidencyDumpFileName = "Dump_1305_WorldChunkResidency.bin";
        private const string WorldChunkResidencyBackpressureDumpFileName = "Dump_1305_WorldChunkResidency_Backpressure.bin";
        private const string WorldChunkResidencyHlodDumpFileName = "Dump_1305_WorldChunkResidency_HLOD.bin";
        private readonly List<string> _rows = new List<string>(MaxDisplayedFrames);
        private TextField _pathField;
        private Label _summaryLabel;
        private ListView _listView;

        [MenuItem("Hecton8/Diagnostics/Telemetry Dump Validator")]
        public static void Open()
        {
            TelemetryDumpValidatorWindow window = GetWindow<TelemetryDumpValidatorWindow>();
            window.titleContent = new GUIContent("Dump Validator");
            window.minSize = new Vector2(720f, 420f);
        }

        private void CreateGUI()
        {
            VisualElement root = rootVisualElement;
            root.style.paddingLeft = 8f;
            root.style.paddingRight = 8f;
            root.style.paddingTop = 8f;
            root.style.paddingBottom = 8f;

            _pathField = new TextField("Dump Path");
            _pathField.value = ResolveDefaultDumpDirectory();
            root.Add(_pathField);

            VisualElement controls = new VisualElement { style = { flexDirection = FlexDirection.Row } };
            Button browse = new Button(Browse) { text = "Browse" };
            Button validate = new Button(ValidateCurrentPath) { text = "Validate" };
            controls.Add(browse);
            controls.Add(validate);
            root.Add(controls);

            _summaryLabel = new Label("No dump loaded.");
            _summaryLabel.style.whiteSpace = WhiteSpace.Normal;
            root.Add(_summaryLabel);

            _listView = new ListView(_rows, 20, MakeRow, BindRow);
            _listView.style.flexGrow = 1f;
            root.Add(_listView);
        }

        private void Browse()
        {
            string selected = EditorUtility.OpenFilePanel("Select HECTON-8 dump", ResolveDefaultDumpDirectory(), "bin,h8dump");
            if (string.IsNullOrEmpty(selected))
                return;

            _pathField.value = selected;
            ValidateCurrentPath();
        }

        private void ValidateCurrentPath()
        {
            _rows.Clear();
            string path = _pathField != null ? _pathField.value : string.Empty;
            path = ResolveReadableDumpPath(path);
            if (_pathField != null && !string.IsNullOrEmpty(path))
                _pathField.value = path;

            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                SetSummary("Missing dump file.");
                RefreshRows();
                return;
            }

            byte[] bytes;
            try
            {
                bytes = File.ReadAllBytes(path);
            }
            catch (Exception ex)
            {
                SetSummary(ex.GetType().Name + ": " + ex.Message);
                RefreshRows();
                return;
            }

            ParseDump(path, bytes);
            RefreshRows();
        }

        private void ParseDump(string path, byte[] bytes)
        {
            if (bytes == null || bytes.Length < 16)
            {
                SetSummary("Invalid dump: header shorter than 16 bytes.");
                return;
            }

            ReadOnlySpan<byte> span = bytes;
            uint magic = ReadU32(span, 0);
            uint version = ReadU32(span, 4);
            uint field2 = ReadU32(span, 8);
            uint field3 = ReadU32(span, 12);
            int headerBytes = 16;
            int entryCount = IsSaneCount(field2) ? (int)field2 : 0;
            int entrySize = IsSaneEntrySize(field3) ? (int)field3 : 0;
            uint hazardWriteIndex = 0u;
            uint hazardSequence = 0u;
            uint globalFrameNumber = 0u;
            uint globalFatalHash = 0u;
            uint globalSourceCount = 0u;
            uint globalEventCursor = 0u;
            uint globalSourcePayloadOffset = 0u;
            uint globalSourceDescriptorIndex = 0u;
            uint globalSourceDescriptorStride = 0u;
            uint globalSourceDescriptorCapacity = 0u;
            int globalSurvivalSourceSlot = -1;
            int globalSourcePayloadOffsetBytes = GlobalTelemetrySourcePayloadOffsetBytes;
            string layoutName = "indexed";
            bool indexedLayout = false;

            if (TryParseCrashTelemetryDump(path, bytes, span))
                return;
            if (TryParseJobAdmissionDump(path, bytes, span))
                return;
            if (TryParseSimulationBucketDump(path, bytes, span))
                return;
            if (TryParseTerrainStreamingDump(path, bytes, span))
                return;

            uint metadataMagic = bytes.Length >= GlobalTelemetryMetadataOffset + 4
                ? ReadU32(span, GlobalTelemetryMetadataOffset)
                : 0u;
            if (metadataMagic == GlobalTelemetryDumpMagic &&
                bytes.Length >= GlobalTelemetryDumpHeaderBytes)
            {
                uint globalVersion = ReadU32(span, GlobalTelemetryMetadataOffset + 4);
                uint globalHeaderBytes = ReadU32(span, GlobalTelemetryMetadataOffset + 8);
                uint globalEntryCount = ReadU32(span, GlobalTelemetryMetadataOffset + 12);
                uint globalEntrySize = ReadU32(span, GlobalTelemetryMetadataOffset + 16);
                if (globalHeaderBytes >= GlobalTelemetryDumpHeaderBytes &&
                    globalHeaderBytes <= bytes.Length &&
                    IsSaneCount(globalEntryCount) &&
                    IsSaneEntrySize(globalEntrySize) &&
                    globalHeaderBytes + (long)globalEntryCount * globalEntrySize <= bytes.Length)
                {
                    magic = metadataMagic;
                    version = globalVersion;
                    headerBytes = (int)globalHeaderBytes;
                    entryCount = (int)globalEntryCount;
                    entrySize = (int)globalEntrySize;
                    globalFrameNumber = ReadU32(span, 8);
                    globalFatalHash = ReadU32(span, 12);
                    globalSourceCount = ReadU32(span, GlobalTelemetryMetadataOffset + 32);
                    globalEventCursor = ReadU32(span, GlobalTelemetryMetadataOffset + 36);
                    globalSourcePayloadOffset = ReadU32(span, GlobalTelemetryMetadataOffset + 44);
                    globalSourcePayloadOffsetBytes = globalSourcePayloadOffset > 0u &&
                                                     globalSourcePayloadOffset <= 2147483647u
                        ? (int)globalSourcePayloadOffset
                        : GlobalTelemetrySourcePayloadOffsetBytes;
                    globalSourceDescriptorIndex = ReadU32(span, GlobalTelemetryMetadataOffset + 76);
                    globalSourceDescriptorStride = ReadU32(span, GlobalTelemetryMetadataOffset + 80);
                    globalSourceDescriptorCapacity = ReadU32(span, GlobalTelemetryMetadataOffset + 84);
                    globalSurvivalSourceSlot = ResolveGlobalTelemetrySourceSlot(
                        span,
                        globalSourceCount,
                        globalSourceDescriptorIndex,
                        globalSourceDescriptorStride,
                        globalSourceDescriptorCapacity,
                        GlobalTelemetrySurvivalSourceHash);
                    layoutName = "global-telemetry-blackbox";
                    indexedLayout = true;
                }
            }

            if (!indexedLayout)
            {
                if (magic == HazardZoneDumpMagic &&
                    bytes.Length >= HazardZoneDumpHeaderBytes &&
                    IsSaneEntrySize(field2) &&
                    IsSaneCount(field3) &&
                    HazardZoneDumpHeaderBytes + (long)field3 * field2 <= bytes.Length)
                {
                    headerBytes = HazardZoneDumpHeaderBytes;
                    entrySize = (int)field2;
                    entryCount = (int)field3;
                    hazardWriteIndex = ReadU32(span, 16);
                    hazardSequence = ReadU32(span, 20);
                    layoutName = "hazard-zone";
                    indexedLayout = true;
                }
                else
                {
                    indexedLayout = entryCount > 0 &&
                                    entrySize > 0 &&
                                    headerBytes + (long)entryCount * entrySize <= bytes.Length;
                }
            }

            if (!indexedLayout)
            {
                entrySize = ResolveFallbackEntrySize(bytes.Length - headerBytes);
                entryCount = math.max(0, (bytes.Length - headerBytes) / entrySize);
                indexedLayout = entryCount > 0;
                layoutName = "fallback";
            }

            ulong payloadHash = ComputeXxHash64(bytes, headerBytes, bytes.Length - headerBytes);
            ulong hashFrom16 = ComputeXxHash64(bytes, 16, bytes.Length - 16);
            ulong storedAt8 = bytes.Length >= 16 ? ReadU64(span, 8) : 0UL;
            ulong storedAt16 = bytes.Length >= 24 ? ReadU64(span, 16) : 0UL;
            bool checksumAt8 = storedAt8 != 0UL && storedAt8 == hashFrom16;
            bool checksumAt16 = storedAt16 != 0UL && bytes.Length > 24 && storedAt16 == ComputeXxHash64(bytes, 24, bytes.Length - 24);
            int displayedEntryCount = ResolveDisplayedEntryCount(
                magic,
                entryCount,
                hazardSequence,
                span,
                headerBytes,
                entrySize);

            StringBuilder builder = new StringBuilder(384);
            builder.Append(Path.GetFileName(path));
            builder.Append(" | bytes=");
            builder.Append(bytes.Length.ToString(CultureInfo.InvariantCulture));
            builder.Append(" | magic=0x");
            builder.Append(magic.ToString("X8", CultureInfo.InvariantCulture));
            builder.Append(" | version=");
            builder.Append(version.ToString(CultureInfo.InvariantCulture));
            builder.Append(" | littleEndian=");
            builder.Append(BitConverter.IsLittleEndian ? "yes" : "no");
            builder.Append(" | layout=");
            builder.Append(layoutName);
            builder.Append(" | entries=");
            builder.Append(entryCount.ToString(CultureInfo.InvariantCulture));
            if (displayedEntryCount != entryCount)
            {
                builder.Append(" | displayed=");
                builder.Append(displayedEntryCount.ToString(CultureInfo.InvariantCulture));
            }

            builder.Append(" | entrySize=");
            builder.Append(entrySize.ToString(CultureInfo.InvariantCulture));
            if (magic == HazardZoneDumpMagic)
            {
                builder.Append(" | writeIndex=");
                builder.Append(hazardWriteIndex.ToString(CultureInfo.InvariantCulture));
                builder.Append(" | sequence=");
                builder.Append(hazardSequence.ToString(CultureInfo.InvariantCulture));
            }
            else if (magic == GlobalTelemetryDumpMagic)
            {
                builder.Append(" | frame=");
                builder.Append(globalFrameNumber.ToString(CultureInfo.InvariantCulture));
                builder.Append(" | fatal=0x");
                builder.Append(globalFatalHash.ToString("X8", CultureInfo.InvariantCulture));
                builder.Append(" | sources=");
                builder.Append(globalSourceCount.ToString(CultureInfo.InvariantCulture));
                builder.Append(" | eventCursor=");
                builder.Append(globalEventCursor.ToString(CultureInfo.InvariantCulture));
                builder.Append(" | sourcePayloadOffset=");
                builder.Append(globalSourcePayloadOffset.ToString(CultureInfo.InvariantCulture));
                builder.Append(" | sourceDescriptors=");
                builder.Append(globalSourceDescriptorIndex.ToString(CultureInfo.InvariantCulture));
                builder.Append('/');
                builder.Append(globalSourceDescriptorStride.ToString(CultureInfo.InvariantCulture));
                builder.Append('/');
                builder.Append(globalSourceDescriptorCapacity.ToString(CultureInfo.InvariantCulture));
                builder.Append(" | survSlot=");
                builder.Append(globalSurvivalSourceSlot.ToString(CultureInfo.InvariantCulture));
            }

            builder.Append(" | xxHash3[payload]=0x");
            builder.Append(payloadHash.ToString("X16", CultureInfo.InvariantCulture));
            builder.Append(" | checksumMatch=");
            builder.Append(checksumAt8 || checksumAt16 ? "yes" : "no");
            SetSummary(builder.ToString());

            if (!indexedLayout)
                return;

            if (magic == GlobalTelemetryDumpMagic)
            {
                AppendGlobalTelemetrySourceDescriptorRows(
                    span,
                    globalSourceCount,
                    globalSourceDescriptorIndex,
                    globalSourceDescriptorStride,
                    globalSourceDescriptorCapacity);
            }

            int shown = math.min(MaxDisplayedFrames, displayedEntryCount);
            int first = math.max(0, displayedEntryCount - shown);
            for (int i = first; i < displayedEntryCount; i++)
            {
                int sourceIndex = ResolveSourceEntryIndex(
                    magic,
                    i,
                    entryCount,
                    displayedEntryCount,
                    hazardWriteIndex);
                int offset = headerBytes + sourceIndex * entrySize;
                if (offset < 0 || offset >= bytes.Length)
                    break;

                int available = math.min(entrySize, bytes.Length - offset);
                _rows.Add(BuildEntryLine(
                    magic,
                    i,
                    sourceIndex,
                    offset,
                    span.Slice(offset, available),
                    globalSurvivalSourceSlot,
                    globalSourcePayloadOffsetBytes));
            }
        }

        private static VisualElement MakeRow()
        {
            return new Label();
        }

        private void BindRow(VisualElement element, int index)
        {
            if (element is Label label && (uint)index < (uint)_rows.Count)
                label.text = _rows[index];
        }

        private void RefreshRows()
        {
            if (_listView == null)
                return;

            _listView.itemsSource = _rows;
            _listView.Rebuild();
        }

        private void SetSummary(string text)
        {
            if (_summaryLabel != null)
                _summaryLabel.text = text;
        }

        private static string BuildEntryLine(
            uint magic,
            int displayIndex,
            int sourceIndex,
            int offset,
            ReadOnlySpan<byte> entry,
            int globalSurvivalSourceSlot,
            int globalSourcePayloadOffsetBytes)
        {
            if (magic == HazardZoneDumpMagic && entry.Length >= 64)
                return BuildHazardZoneEntryLine(displayIndex, sourceIndex, offset, entry);
            if (magic == GlobalTelemetryDumpMagic && entry.Length >= 64)
                return BuildGlobalTelemetryFrameLine(
                    displayIndex,
                    sourceIndex,
                    offset,
                    entry,
                    globalSurvivalSourceSlot,
                    globalSourcePayloadOffsetBytes);

            return BuildGenericEntryLine(displayIndex, sourceIndex, offset, entry);
        }

        private bool TryParseCrashTelemetryDump(string path, byte[] bytes, ReadOnlySpan<byte> span)
        {
            if (!IsCrashTelemetryDumpPath(path))
            {
                return false;
            }

            if (span.Length < CrashTelemetryDumpHeaderBytes ||
                ReadU64(span, 0) != CrashTelemetryDumpMagic)
            {
                SetSummary(BuildInvalidCrashTelemetryHeaderSummary(path, span.Length, 0u, 0u));
                return true;
            }

            uint entryCountRaw = ReadU32(span, 8);
            uint entrySizeRaw = ReadU32(span, 12);
            bool valid =
                IsSaneCount(entryCountRaw) &&
                entrySizeRaw == CrashTelemetryDumpEntrySizeBytes &&
                CrashTelemetryDumpHeaderBytes + (long)entryCountRaw * entrySizeRaw <= span.Length;

            if (!valid)
            {
                SetSummary(BuildInvalidCrashTelemetryHeaderSummary(
                    path,
                    span.Length,
                    entryCountRaw,
                    entrySizeRaw));
                return true;
            }

            int entryCount = (int)entryCountRaw;
            int entrySize = (int)entrySizeRaw;
            int displayedEntryCount = math.min(entryCount, MaxDisplayedFrames);
            int skip = math.max(0, entryCount - displayedEntryCount);
            int payloadBytes = entryCount * entrySize;
            ulong payloadHash = ComputeXxHash64(bytes, CrashTelemetryDumpHeaderBytes, payloadBytes);
            StringBuilder builder = new StringBuilder(256);
            builder.Append(Path.GetFileName(path));
            builder.Append(" | bytes=");
            builder.Append(span.Length.ToString(CultureInfo.InvariantCulture));
            builder.Append(" | magic=HECTON8");
            builder.Append(" | layout=crash-telemetry-buffer");
            builder.Append(" | entries=");
            builder.Append(entryCount.ToString(CultureInfo.InvariantCulture));
            builder.Append(" | displayed=");
            builder.Append(displayedEntryCount.ToString(CultureInfo.InvariantCulture));
            builder.Append(" | entrySize=");
            builder.Append(entrySize.ToString(CultureInfo.InvariantCulture));
            builder.Append(" | xxHash3[payload]=0x");
            builder.Append(payloadHash.ToString("X16", CultureInfo.InvariantCulture));
            SetSummary(builder.ToString());

            for (int i = skip; i < entryCount; i++)
            {
                int offset = CrashTelemetryDumpHeaderBytes + i * entrySize;
                ReadOnlySpan<byte> entry = span.Slice(offset, entrySize);
                _rows.Add(BuildCrashTelemetryEntryLine(i - skip, i, offset, entry));
            }

            return true;
        }

        private static string BuildInvalidCrashTelemetryHeaderSummary(
            string path,
            int byteCount,
            uint entryCount,
            uint entrySize)
        {
            StringBuilder builder = new StringBuilder(160);
            builder.Append(Path.GetFileName(path));
            builder.Append(" | invalid crash-telemetry-buffer header");
            builder.Append(" | bytes=");
            builder.Append(byteCount.ToString(CultureInfo.InvariantCulture));
            builder.Append(" | entries=");
            builder.Append(entryCount.ToString(CultureInfo.InvariantCulture));
            builder.Append(" | entrySize=");
            builder.Append(entrySize.ToString(CultureInfo.InvariantCulture));
            return builder.ToString();
        }

        private static bool IsCrashTelemetryDumpPath(string path)
        {
            string fileName = Path.GetFileName(path) ?? string.Empty;
            return string.Equals(
                       fileName,
                       "Dump_CRASH_TELEMETRY_BUFFER.bin",
                       StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(
                       fileName,
                       "BLACKBOX_CRASH.bin",
                       StringComparison.OrdinalIgnoreCase);
        }

        private static string BuildCrashTelemetryEntryLine(
            int displayIndex,
            int sourceIndex,
            int offset,
            ReadOnlySpan<byte> entry)
        {
            uint frame = ReadU32(entry, 0);
            uint systemMask = ReadU32(entry, 4);
            float deltaTimeSeconds = ReadF32(entry, 8);
            float latencyMs = ReadF32(entry, 12);
            float gpuFrameTimeMs = ReadF32(entry, 16);
            float memoryUsedMb = ReadF32(entry, 20);
            float playerX = ReadF32(entry, 24);
            float playerY = ReadF32(entry, 28);
            float playerZ = ReadF32(entry, 32);
            uint activeChunks = ReadU32(entry, 36);
            uint errorFlags = ReadU32(entry, 40);
            uint exportReason = ReadU32(entry, 44);
            uint aupShiftSequence = ReadU32(entry, 48);
            uint payload0 = ReadU32(entry, 52);
            uint payload1 = ReadU32(entry, 56);
            uint lastOriginShiftFrame = ReadU32(entry, 60);

            StringBuilder builder = new StringBuilder(260);
            builder.Append('#');
            builder.Append(displayIndex.ToString(CultureInfo.InvariantCulture));
            builder.Append(" slot=");
            builder.Append(sourceIndex.ToString(CultureInfo.InvariantCulture));
            builder.Append(" @");
            builder.Append(offset.ToString(CultureInfo.InvariantCulture));
            builder.Append(" frame=");
            builder.Append(frame.ToString(CultureInfo.InvariantCulture));
            builder.Append(" dtMs=");
            builder.Append((deltaTimeSeconds * 1000f).ToString("0.###", CultureInfo.InvariantCulture));
            builder.Append(" latency=");
            builder.Append(latencyMs.ToString("0.###", CultureInfo.InvariantCulture));
            builder.Append(" gpu=");
            builder.Append(gpuFrameTimeMs.ToString("0.###", CultureInfo.InvariantCulture));
            builder.Append(" mem=");
            builder.Append(memoryUsedMb.ToString("0.###", CultureInfo.InvariantCulture));
            builder.Append(" chunks=");
            builder.Append(activeChunks.ToString(CultureInfo.InvariantCulture));
            builder.Append(" system=0x");
            builder.Append(systemMask.ToString("X8", CultureInfo.InvariantCulture));
            builder.Append(" errors=0x");
            builder.Append(errorFlags.ToString("X8", CultureInfo.InvariantCulture));
            builder.Append(" reason=0x");
            builder.Append(exportReason.ToString("X8", CultureInfo.InvariantCulture));
            builder.Append(" aupSeq=");
            builder.Append(aupShiftSequence.ToString(CultureInfo.InvariantCulture));
            builder.Append(" originFrame=");
            builder.Append(lastOriginShiftFrame.ToString(CultureInfo.InvariantCulture));
            builder.Append(" pos=");
            builder.Append(playerX.ToString("0.###", CultureInfo.InvariantCulture));
            builder.Append('/');
            builder.Append(playerY.ToString("0.###", CultureInfo.InvariantCulture));
            builder.Append('/');
            builder.Append(playerZ.ToString("0.###", CultureInfo.InvariantCulture));
            builder.Append(" payload=0x");
            builder.Append(payload0.ToString("X8", CultureInfo.InvariantCulture));
            builder.Append('/');
            builder.Append(payload1.ToString("X8", CultureInfo.InvariantCulture));
            builder.Append(" spike=");
            builder.Append(deltaTimeSeconds >= CrashTelemetrySpikeFrameTimeSeconds ? "1" : "0");
            builder.Append(" memFault=");
            builder.Append((errorFlags & CrashTelemetryMemoryFaultMask) != 0u ? "1" : "0");
            return builder.ToString();
        }

        private bool TryParseSimulationBucketDump(string path, byte[] bytes, ReadOnlySpan<byte> span)
        {
            if (!IsSimulationBucketDumpPath(path))
            {
                return false;
            }

            if (span.Length < SimulationBucketDumpHeaderBytes ||
                ReadU64(span, 0) != SimulationBucketDumpMagic)
            {
                SetSummary(BuildInvalidSimulationBucketHeaderSummary(path, span.Length, 0u, 0, 0, 0, 0, 0u));
                return true;
            }

            uint version = ReadU32(span, 8);
            int entryCount = ReadI32(span, 12);
            int entrySize = ReadI32(span, 16);
            int cursor = ReadI32(span, 20);
            int frame = ReadI32(span, 24);
            uint rebalanceSequence = ReadU32(span, 28);
            bool valid =
                version == SimulationBucketDumpVersion &&
                entryCount >= 0 &&
                entryCount <= 100000 &&
                entrySize == SimulationBucketDumpEntrySizeBytes &&
                cursor >= 0 &&
                (entryCount == 0 || cursor < entryCount) &&
                SimulationBucketDumpHeaderBytes + (long)entryCount * entrySize <= span.Length;

            if (!valid)
            {
                SetSummary(BuildInvalidSimulationBucketHeaderSummary(
                    path,
                    span.Length,
                    version,
                    entryCount,
                    entrySize,
                    cursor,
                    frame,
                    rebalanceSequence));
                return true;
            }

            int nonEmptyEntryCount = CountSimulationBucketEntriesWithPayload(span, entryCount);
            int payloadBytes = entryCount * entrySize;
            ulong payloadHash = ComputeXxHash64(bytes, SimulationBucketDumpHeaderBytes, payloadBytes);
            StringBuilder builder = new StringBuilder(256);
            builder.Append(Path.GetFileName(path));
            builder.Append(" | bytes=");
            builder.Append(span.Length.ToString(CultureInfo.InvariantCulture));
            builder.Append(" | magic=HECTON8");
            builder.Append(" | version=");
            builder.Append(version.ToString(CultureInfo.InvariantCulture));
            builder.Append(" | layout=simulation-bucket-blackbox");
            builder.Append(" | entries=");
            builder.Append(entryCount.ToString(CultureInfo.InvariantCulture));
            builder.Append(" | displayed=");
            builder.Append(math.min(nonEmptyEntryCount, MaxDisplayedFrames).ToString(CultureInfo.InvariantCulture));
            builder.Append('/');
            builder.Append(nonEmptyEntryCount.ToString(CultureInfo.InvariantCulture));
            builder.Append(" | entrySize=");
            builder.Append(entrySize.ToString(CultureInfo.InvariantCulture));
            builder.Append(" | cursor=");
            builder.Append(cursor.ToString(CultureInfo.InvariantCulture));
            builder.Append(" | frame=");
            builder.Append(frame.ToString(CultureInfo.InvariantCulture));
            builder.Append(" | rebalance=");
            builder.Append(rebalanceSequence.ToString(CultureInfo.InvariantCulture));
            builder.Append(" | xxHash3[payload]=0x");
            builder.Append(payloadHash.ToString("X16", CultureInfo.InvariantCulture));
            SetSummary(builder.ToString());

            int skip = math.max(0, nonEmptyEntryCount - MaxDisplayedFrames);
            int seen = 0;
            for (int i = 0; i < entryCount; i++)
            {
                int offset = SimulationBucketDumpHeaderBytes + i * entrySize;
                ReadOnlySpan<byte> entry = span.Slice(offset, entrySize);
                if (IsEmptySimulationBucketEntry(entry))
                    continue;

                if (seen++ < skip)
                    continue;

                _rows.Add(BuildSimulationBucketEntryLine(seen - 1, i, offset, entry));
            }

            return true;
        }

        private static string BuildInvalidSimulationBucketHeaderSummary(
            string path,
            int byteCount,
            uint version,
            int entryCount,
            int entrySize,
            int cursor,
            int frame,
            uint rebalanceSequence)
        {
            StringBuilder builder = new StringBuilder(192);
            builder.Append(Path.GetFileName(path));
            builder.Append(" | invalid simulation-bucket blackbox header");
            builder.Append(" | bytes=");
            builder.Append(byteCount.ToString(CultureInfo.InvariantCulture));
            builder.Append(" | version=");
            builder.Append(version.ToString(CultureInfo.InvariantCulture));
            builder.Append(" | entries=");
            builder.Append(entryCount.ToString(CultureInfo.InvariantCulture));
            builder.Append(" | entrySize=");
            builder.Append(entrySize.ToString(CultureInfo.InvariantCulture));
            builder.Append(" | cursor=");
            builder.Append(cursor.ToString(CultureInfo.InvariantCulture));
            builder.Append(" | frame=");
            builder.Append(frame.ToString(CultureInfo.InvariantCulture));
            builder.Append(" | rebalance=");
            builder.Append(rebalanceSequence.ToString(CultureInfo.InvariantCulture));
            return builder.ToString();
        }

        private static bool IsSimulationBucketDumpPath(string path)
        {
            string fileName = Path.GetFileName(path) ?? string.Empty;
            return string.Equals(
                fileName,
                "Dump_SIMULATION_BUCKET_DISTRIBUTOR.bin",
                StringComparison.OrdinalIgnoreCase);
        }

        private static int CountSimulationBucketEntriesWithPayload(ReadOnlySpan<byte> bytes, int entryCount)
        {
            int count = 0;
            for (int i = 0; i < entryCount; i++)
            {
                int offset = SimulationBucketDumpHeaderBytes + i * SimulationBucketDumpEntrySizeBytes;
                if (offset < 0 || offset + SimulationBucketDumpEntrySizeBytes > bytes.Length)
                    break;

                if (!IsEmptySimulationBucketEntry(bytes.Slice(offset, SimulationBucketDumpEntrySizeBytes)))
                    count++;
            }

            return count;
        }

        private static bool IsEmptySimulationBucketEntry(ReadOnlySpan<byte> entry)
        {
            int scanned = math.min(SimulationBucketDumpEntrySizeBytes, entry.Length);
            for (int i = 0; i < scanned; i++)
            {
                if (entry[i] != 0)
                    return false;
            }

            return true;
        }

        private static string BuildSimulationBucketEntryLine(
            int displayIndex,
            int sourceIndex,
            int offset,
            ReadOnlySpan<byte> entry)
        {
            int frame = ReadI32(entry, 0);
            int fast = ReadI32(entry, 4);
            int slow = ReadI32(entry, 8);
            int cold = ReadI32(entry, 12);
            int slowCount = ReadI32(entry, 16);
            int debt = ReadI32(entry, 20);
            uint pacingFlags = ReadU32(entry, 24);
            uint rebalanceSequence = ReadU32(entry, 28);
            float activeLoadMs = ReadF32(entry, 32);
            float jitterMs = ReadF32(entry, 36);
            float expectedMaxMs = ReadF32(entry, 40);
            float expectedMeanMs = ReadF32(entry, 44);
            float preSimulationCostMs = ReadF32(entry, 48);
            float interpolationAlpha = ReadF32(entry, 52);
            byte activeSlowBucketCount = entry.Length > 56 ? entry[56] : (byte)0;
            byte aupBarrierActive = entry.Length > 57 ? entry[57] : (byte)0;
            ushort reserved = ReadU16(entry, 58);
            uint stateHash = ReadU32(entry, 60);

            StringBuilder builder = new StringBuilder(240);
            builder.Append('#');
            builder.Append(displayIndex.ToString(CultureInfo.InvariantCulture));
            builder.Append(" slot=");
            builder.Append(sourceIndex.ToString(CultureInfo.InvariantCulture));
            builder.Append(" @");
            builder.Append(offset.ToString(CultureInfo.InvariantCulture));
            builder.Append(" frame=");
            builder.Append(frame.ToString(CultureInfo.InvariantCulture));
            builder.Append(" buckets=");
            builder.Append(fast.ToString(CultureInfo.InvariantCulture));
            builder.Append('/');
            builder.Append(slow.ToString(CultureInfo.InvariantCulture));
            builder.Append('/');
            builder.Append(cold.ToString(CultureInfo.InvariantCulture));
            builder.Append(" slowCount=");
            builder.Append(slowCount.ToString(CultureInfo.InvariantCulture));
            builder.Append(" activeSlow=");
            builder.Append(activeSlowBucketCount.ToString(CultureInfo.InvariantCulture));
            builder.Append(" debt=");
            builder.Append(debt.ToString(CultureInfo.InvariantCulture));
            builder.Append(" flags=0x");
            builder.Append(pacingFlags.ToString("X8", CultureInfo.InvariantCulture));
            builder.Append(' ');
            builder.Append(ResolveSimulationBucketFlagsLabel(pacingFlags));
            builder.Append(" rebalance=");
            builder.Append(rebalanceSequence.ToString(CultureInfo.InvariantCulture));
            builder.Append(" load=");
            builder.Append(activeLoadMs.ToString("0.###", CultureInfo.InvariantCulture));
            builder.Append(" jitter=");
            builder.Append(jitterMs.ToString("0.###", CultureInfo.InvariantCulture));
            builder.Append(" expected=");
            builder.Append(expectedMeanMs.ToString("0.###", CultureInfo.InvariantCulture));
            builder.Append('/');
            builder.Append(expectedMaxMs.ToString("0.###", CultureInfo.InvariantCulture));
            builder.Append(" pre=");
            builder.Append(preSimulationCostMs.ToString("0.###", CultureInfo.InvariantCulture));
            builder.Append(" alpha=");
            builder.Append(interpolationAlpha.ToString("0.###", CultureInfo.InvariantCulture));
            builder.Append(" aup=");
            builder.Append(aupBarrierActive != 0 ? "1" : "0");
            builder.Append(" reserved=0x");
            builder.Append(reserved.ToString("X4", CultureInfo.InvariantCulture));
            builder.Append(" state=0x");
            builder.Append(stateHash.ToString("X8", CultureInfo.InvariantCulture));
            return builder.ToString();
        }

        private static string ResolveSimulationBucketFlagsLabel(uint flags)
        {
            if (flags == 0u)
                return "none";

            StringBuilder builder = new StringBuilder(128);
            AppendFlagLabel(builder, (flags & SimulationBucketPacingFlags.Impossible60Fps) != 0u, "impossible-60fps");
            AppendFlagLabel(builder, (flags & SimulationBucketPacingFlags.PreSimulationOverBudget) != 0u, "pre-sim-over-budget");
            AppendFlagLabel(builder, (flags & SimulationBucketPacingFlags.NonFiniteCost) != 0u, "nonfinite-cost");
            AppendFlagLabel(builder, (flags & SimulationBucketPacingFlags.RebalancePending) != 0u, "rebalance-pending");
            AppendFlagLabel(builder, (flags & SimulationBucketPacingFlags.SurvivalStaticDistribution) != 0u, "survival-static");
            AppendFlagLabel(builder, (flags & SimulationBucketPacingFlags.HomeostasisKillRequested) != 0u, "homeostasis-kill");
            AppendFlagLabel(builder, (flags & SimulationBucketPacingFlags.VisualOverkillBudgetAvailable) != 0u, "visual-overkill-room");

            const uint knownFlags =
                SimulationBucketPacingFlags.Impossible60Fps |
                SimulationBucketPacingFlags.PreSimulationOverBudget |
                SimulationBucketPacingFlags.NonFiniteCost |
                SimulationBucketPacingFlags.RebalancePending |
                SimulationBucketPacingFlags.SurvivalStaticDistribution |
                SimulationBucketPacingFlags.HomeostasisKillRequested |
                SimulationBucketPacingFlags.VisualOverkillBudgetAvailable;
            uint unknownFlags = flags & ~knownFlags;
            if (unknownFlags != 0u)
            {
                if (builder.Length != 0)
                    builder.Append('|');

                builder.Append("unknown=0x");
                builder.Append(unknownFlags.ToString("X8", CultureInfo.InvariantCulture));
            }

            return builder.Length == 0 ? "unknown" : builder.ToString();
        }

        private bool TryParseTerrainStreamingDump(string path, byte[] bytes, ReadOnlySpan<byte> span)
        {
            if (!IsTerrainStreamingDumpPath(path))
            {
                return false;
            }

            string fileName = Path.GetFileName(path) ?? string.Empty;
            bool pagerFile = IsTerrainStreamingPagerDumpFileName(fileName);
            bool rawResidencyFile = IsWorldChunkResidencyDumpFileName(fileName);
            bool legacyFile = string.Equals(
                fileName,
                TerrainStreamingLegacyDumpFileName,
                StringComparison.OrdinalIgnoreCase);

            if (span.Length >= TerrainStreamingLegacyPagerDumpHeaderBytes &&
                ReadU64(span, 0) == TerrainStreamingDumpMagic)
            {
                uint version = ReadU32(span, 8);
                uint layoutHash = span.Length >= WorldChunkResidencyDumpHeaderBytes
                    ? ReadU32(span, 24)
                    : 0u;
                if (rawResidencyFile ||
                    (legacyFile &&
                     version == WorldChunkResidencyDumpVersion &&
                     layoutHash == WorldChunkResidencyDumpLayoutHash))
                {
                    if (span.Length < WorldChunkResidencyDumpHeaderBytes)
                    {
                        SetSummary(BuildInvalidWorldChunkResidencyHeaderSummary(
                            path,
                            span.Length,
                            version,
                            0,
                            0,
                            0u,
                            0u,
                            0u));
                        return true;
                    }

                    return ParseWorldChunkResidencyHeaderDump(path, bytes, span);
                }

                if (pagerFile || legacyFile)
                {
                    int headerBytes = pagerFile
                        ? TerrainStreamingPagerDumpHeaderBytes
                        : TerrainStreamingLegacyPagerDumpHeaderBytes;
                    bool requiresLayoutHash = pagerFile;
                    return ParseTerrainStreamingPagerDump(path, bytes, span, headerBytes, requiresLayoutHash);
                }
            }

            if (legacyFile &&
                span.Length > 0 &&
                span.Length % TerrainStreamingDumpEntrySizeBytes == 0)
            {
                return ParseWorldChunkResidencyRawDump(path, bytes, span);
            }

            SetSummary(BuildInvalidTerrainStreamingHeaderSummary(
                path,
                span.Length,
                0u,
                0,
                0,
                0u));
            return true;
        }

        private bool ParseTerrainStreamingPagerDump(
            string path,
            byte[] bytes,
            ReadOnlySpan<byte> span,
            int headerBytes,
            bool requiresLayoutHash)
        {
            uint version = ReadU32(span, 8);
            int entryCount = ReadI32(span, 12);
            int entrySize = ReadI32(span, 16);
            uint faultFlags = ReadU32(span, 20);
            uint layoutHash = span.Length >= TerrainStreamingPagerDumpHeaderBytes
                ? ReadU32(span, 24)
                : 0u;
            uint reserved = span.Length >= TerrainStreamingPagerDumpHeaderBytes
                ? ReadU32(span, 28)
                : 0u;
            bool valid =
                version == TerrainStreamingPagerDumpVersion &&
                entryCount > 0 &&
                entryCount <= 100000 &&
                entrySize == TerrainStreamingDumpEntrySizeBytes &&
                (!requiresLayoutHash ||
                 (layoutHash == TerrainStreamingPagerDumpLayoutHash && reserved == 0u)) &&
                headerBytes + (long)entryCount * entrySize <= span.Length;

            if (!valid)
            {
                SetSummary(BuildInvalidTerrainStreamingHeaderSummary(
                    path,
                    span.Length,
                    version,
                    entryCount,
                    entrySize,
                    faultFlags));
                return true;
            }

            int nonEmptyEntryCount = CountTerrainStreamingEntriesWithPayload(
                span,
                headerBytes,
                entryCount);
            int payloadBytes = entryCount * entrySize;
            ulong payloadHash = ComputeXxHash64(bytes, headerBytes, payloadBytes);
            StringBuilder builder = new StringBuilder(300);
            builder.Append(Path.GetFileName(path));
            builder.Append(" | bytes=");
            builder.Append(span.Length.ToString(CultureInfo.InvariantCulture));
            builder.Append(" | magic=HECTON8");
            builder.Append(" | version=");
            builder.Append(version.ToString(CultureInfo.InvariantCulture));
            builder.Append(" | layout=terrain-chunk-pager-blackbox");
            builder.Append(" | entries=");
            builder.Append(entryCount.ToString(CultureInfo.InvariantCulture));
            builder.Append(" | displayed=");
            builder.Append(math.min(nonEmptyEntryCount, MaxDisplayedFrames).ToString(CultureInfo.InvariantCulture));
            builder.Append('/');
            builder.Append(nonEmptyEntryCount.ToString(CultureInfo.InvariantCulture));
            builder.Append(" | entrySize=");
            builder.Append(entrySize.ToString(CultureInfo.InvariantCulture));
            builder.Append(" | faults=0x");
            builder.Append(faultFlags.ToString("X8", CultureInfo.InvariantCulture));
            builder.Append(' ');
            builder.Append(ResolveTerrainStreamingPagerFaultLabels(faultFlags));
            if (requiresLayoutHash)
            {
                builder.Append(" | layoutHash=0x");
                builder.Append(layoutHash.ToString("X8", CultureInfo.InvariantCulture));
            }
            builder.Append(" | xxHash3[payload]=0x");
            builder.Append(payloadHash.ToString("X16", CultureInfo.InvariantCulture));
            SetSummary(builder.ToString());

            int skip = math.max(0, nonEmptyEntryCount - MaxDisplayedFrames);
            int seen = 0;
            for (int i = 0; i < entryCount; i++)
            {
                int offset = headerBytes + i * entrySize;
                ReadOnlySpan<byte> entry = span.Slice(offset, entrySize);
                if (IsEmptyTerrainStreamingEntry(entry))
                    continue;

                if (seen++ < skip)
                    continue;

                _rows.Add(BuildTerrainStreamingPagerEntryLine(seen - 1, i, offset, entry));
            }

            return true;
        }

        private bool ParseWorldChunkResidencyRawDump(string path, byte[] bytes, ReadOnlySpan<byte> span)
        {
            int entryCount = span.Length / TerrainStreamingDumpEntrySizeBytes;
            int nonEmptyEntryCount = CountTerrainStreamingEntriesWithPayload(span, 0, entryCount);
            ulong payloadHash = ComputeXxHash64(bytes, 0, span.Length);
            StringBuilder builder = new StringBuilder(256);
            builder.Append(Path.GetFileName(path));
            builder.Append(" | bytes=");
            builder.Append(span.Length.ToString(CultureInfo.InvariantCulture));
            builder.Append(" | layout=world-chunk-residency-blackbox");
            builder.Append(" | entries=");
            builder.Append(entryCount.ToString(CultureInfo.InvariantCulture));
            builder.Append(" | displayed=");
            builder.Append(math.min(nonEmptyEntryCount, MaxDisplayedFrames).ToString(CultureInfo.InvariantCulture));
            builder.Append('/');
            builder.Append(nonEmptyEntryCount.ToString(CultureInfo.InvariantCulture));
            builder.Append(" | entrySize=");
            builder.Append(TerrainStreamingDumpEntrySizeBytes.ToString(CultureInfo.InvariantCulture));
            builder.Append(" | xxHash3[payload]=0x");
            builder.Append(payloadHash.ToString("X16", CultureInfo.InvariantCulture));
            SetSummary(builder.ToString());

            int skip = math.max(0, nonEmptyEntryCount - MaxDisplayedFrames);
            int seen = 0;
            for (int i = 0; i < entryCount; i++)
            {
                int offset = i * TerrainStreamingDumpEntrySizeBytes;
                ReadOnlySpan<byte> entry = span.Slice(offset, TerrainStreamingDumpEntrySizeBytes);
                if (IsEmptyTerrainStreamingEntry(entry))
                    continue;

                if (seen++ < skip)
                    continue;

                _rows.Add(BuildWorldChunkResidencyEntryLine(seen - 1, i, offset, entry));
            }

            return true;
        }

        private bool ParseWorldChunkResidencyHeaderDump(string path, byte[] bytes, ReadOnlySpan<byte> span)
        {
            uint version = ReadU32(span, 8);
            int entryCount = ReadI32(span, 12);
            int entrySize = ReadI32(span, 16);
            uint reasonFlags = ReadU32(span, 20);
            uint layoutHash = ReadU32(span, 24);
            uint reserved = ReadU32(span, 28);
            bool valid =
                version == WorldChunkResidencyDumpVersion &&
                entryCount > 0 &&
                entryCount <= 100000 &&
                entrySize == TerrainStreamingDumpEntrySizeBytes &&
                layoutHash == WorldChunkResidencyDumpLayoutHash &&
                reserved == 0u &&
                WorldChunkResidencyDumpHeaderBytes + (long)entryCount * entrySize <= span.Length;

            if (!valid)
            {
                SetSummary(BuildInvalidWorldChunkResidencyHeaderSummary(
                    path,
                    span.Length,
                    version,
                    entryCount,
                    entrySize,
                    reasonFlags,
                    layoutHash,
                    reserved));
                return true;
            }

            int nonEmptyEntryCount = CountTerrainStreamingEntriesWithPayload(
                span,
                WorldChunkResidencyDumpHeaderBytes,
                entryCount);
            int payloadBytes = entryCount * entrySize;
            ulong payloadHash = ComputeXxHash64(bytes, WorldChunkResidencyDumpHeaderBytes, payloadBytes);
            StringBuilder builder = new StringBuilder(300);
            builder.Append(Path.GetFileName(path));
            builder.Append(" | bytes=");
            builder.Append(span.Length.ToString(CultureInfo.InvariantCulture));
            builder.Append(" | magic=HECTON8");
            builder.Append(" | version=");
            builder.Append(version.ToString(CultureInfo.InvariantCulture));
            builder.Append(" | layout=world-chunk-residency-blackbox");
            builder.Append(" | entries=");
            builder.Append(entryCount.ToString(CultureInfo.InvariantCulture));
            builder.Append(" | displayed=");
            builder.Append(math.min(nonEmptyEntryCount, MaxDisplayedFrames).ToString(CultureInfo.InvariantCulture));
            builder.Append('/');
            builder.Append(nonEmptyEntryCount.ToString(CultureInfo.InvariantCulture));
            builder.Append(" | entrySize=");
            builder.Append(entrySize.ToString(CultureInfo.InvariantCulture));
            builder.Append(" | reason=0x");
            builder.Append(reasonFlags.ToString("X8", CultureInfo.InvariantCulture));
            builder.Append(' ');
            builder.Append(ResolveWorldChunkResidencyFlagsLabel(reasonFlags));
            builder.Append(" | layoutHash=0x");
            builder.Append(layoutHash.ToString("X8", CultureInfo.InvariantCulture));
            builder.Append(" | xxHash3[payload]=0x");
            builder.Append(payloadHash.ToString("X16", CultureInfo.InvariantCulture));
            SetSummary(builder.ToString());

            int skip = math.max(0, nonEmptyEntryCount - MaxDisplayedFrames);
            int seen = 0;
            for (int i = 0; i < entryCount; i++)
            {
                int offset = WorldChunkResidencyDumpHeaderBytes + i * entrySize;
                ReadOnlySpan<byte> entry = span.Slice(offset, entrySize);
                if (IsEmptyTerrainStreamingEntry(entry))
                    continue;

                if (seen++ < skip)
                    continue;

                _rows.Add(BuildWorldChunkResidencyEntryLine(seen - 1, i, offset, entry));
            }

            return true;
        }

        private static string BuildInvalidTerrainStreamingHeaderSummary(
            string path,
            int byteCount,
            uint version,
            int entryCount,
            int entrySize,
            uint faultFlags)
        {
            StringBuilder builder = new StringBuilder(192);
            builder.Append(Path.GetFileName(path));
            builder.Append(" | invalid terrain-streaming blackbox header");
            builder.Append(" | bytes=");
            builder.Append(byteCount.ToString(CultureInfo.InvariantCulture));
            builder.Append(" | version=");
            builder.Append(version.ToString(CultureInfo.InvariantCulture));
            builder.Append(" | entries=");
            builder.Append(entryCount.ToString(CultureInfo.InvariantCulture));
            builder.Append(" | entrySize=");
            builder.Append(entrySize.ToString(CultureInfo.InvariantCulture));
            builder.Append(" | faults=0x");
            builder.Append(faultFlags.ToString("X8", CultureInfo.InvariantCulture));
            return builder.ToString();
        }

        private static string BuildInvalidWorldChunkResidencyHeaderSummary(
            string path,
            int byteCount,
            uint version,
            int entryCount,
            int entrySize,
            uint reasonFlags,
            uint layoutHash,
            uint reserved)
        {
            StringBuilder builder = new StringBuilder(224);
            builder.Append(Path.GetFileName(path));
            builder.Append(" | invalid world-chunk-residency blackbox header");
            builder.Append(" | bytes=");
            builder.Append(byteCount.ToString(CultureInfo.InvariantCulture));
            builder.Append(" | version=");
            builder.Append(version.ToString(CultureInfo.InvariantCulture));
            builder.Append(" | entries=");
            builder.Append(entryCount.ToString(CultureInfo.InvariantCulture));
            builder.Append(" | entrySize=");
            builder.Append(entrySize.ToString(CultureInfo.InvariantCulture));
            builder.Append(" | reason=0x");
            builder.Append(reasonFlags.ToString("X8", CultureInfo.InvariantCulture));
            builder.Append(" | layoutHash=0x");
            builder.Append(layoutHash.ToString("X8", CultureInfo.InvariantCulture));
            builder.Append(" | reserved=0x");
            builder.Append(reserved.ToString("X8", CultureInfo.InvariantCulture));
            return builder.ToString();
        }

        private static bool IsTerrainStreamingDumpPath(string path)
        {
            string fileName = Path.GetFileName(path) ?? string.Empty;
            return IsTerrainStreamingPagerDumpFileName(fileName) ||
                   IsWorldChunkResidencyDumpFileName(fileName) ||
                   string.Equals(
                       fileName,
                       TerrainStreamingLegacyDumpFileName,
                       StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsTerrainStreamingPagerDumpFileName(string fileName)
        {
            return string.Equals(
                       fileName,
                       TerrainStreamingPagerDumpFileName,
                       StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsWorldChunkResidencyDumpFileName(string fileName)
        {
            return string.Equals(
                       fileName,
                       WorldChunkResidencyDumpFileName,
                       StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(
                       fileName,
                       WorldChunkResidencyBackpressureDumpFileName,
                       StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(
                       fileName,
                       WorldChunkResidencyHlodDumpFileName,
                       StringComparison.OrdinalIgnoreCase);
        }

        private static int CountTerrainStreamingEntriesWithPayload(
            ReadOnlySpan<byte> bytes,
            int headerBytes,
            int entryCount)
        {
            int count = 0;
            for (int i = 0; i < entryCount; i++)
            {
                int offset = headerBytes + i * TerrainStreamingDumpEntrySizeBytes;
                if (offset < 0 || offset + TerrainStreamingDumpEntrySizeBytes > bytes.Length)
                    break;

                if (!IsEmptyTerrainStreamingEntry(bytes.Slice(offset, TerrainStreamingDumpEntrySizeBytes)))
                    count++;
            }

            return count;
        }

        private static bool IsEmptyTerrainStreamingEntry(ReadOnlySpan<byte> entry)
        {
            int scanned = math.min(TerrainStreamingDumpEntrySizeBytes, entry.Length);
            for (int i = 0; i < scanned; i++)
            {
                if (entry[i] != 0)
                    return false;
            }

            return true;
        }

        private static string BuildTerrainStreamingPagerEntryLine(
            int displayIndex,
            int sourceIndex,
            int offset,
            ReadOnlySpan<byte> entry)
        {
            double cameraX = ReadF64(entry, 0);
            double cameraY = ReadF64(entry, 8);
            double cameraZ = ReadF64(entry, 16);
            uint frame = ReadU32(entry, 24);
            uint stateHash = ReadU32(entry, 28);
            ushort activeChunks = ReadU16(entry, 32);
            ushort loadingChunks = ReadU16(entry, 34);
            ushort staleChunks = ReadU16(entry, 36);
            ushort pendingLoads = ReadU16(entry, 38);
            float latencyEwmaMs = ReadF32(entry, 40);
            uint residencyEvalMicros = ReadU32(entry, 44);
            float effectiveRingRadius = ReadF32(entry, 48);
            uint flags = ReadU32(entry, 52);
            uint missingFileCount = ReadU32(entry, 56);
            uint workerSequence = ReadU32(entry, 60);

            StringBuilder builder = new StringBuilder(300);
            builder.Append('#');
            builder.Append(displayIndex.ToString(CultureInfo.InvariantCulture));
            builder.Append(" slot=");
            builder.Append(sourceIndex.ToString(CultureInfo.InvariantCulture));
            builder.Append(" @");
            builder.Append(offset.ToString(CultureInfo.InvariantCulture));
            builder.Append(" frame=");
            builder.Append(frame.ToString(CultureInfo.InvariantCulture));
            builder.Append(" chunks=");
            builder.Append(activeChunks.ToString(CultureInfo.InvariantCulture));
            builder.Append('/');
            builder.Append(loadingChunks.ToString(CultureInfo.InvariantCulture));
            builder.Append('/');
            builder.Append(staleChunks.ToString(CultureInfo.InvariantCulture));
            builder.Append(" pending=");
            builder.Append(pendingLoads.ToString(CultureInfo.InvariantCulture));
            builder.Append(" latency=");
            builder.Append(latencyEwmaMs.ToString("0.###", CultureInfo.InvariantCulture));
            builder.Append(" evalUs=");
            builder.Append(residencyEvalMicros.ToString(CultureInfo.InvariantCulture));
            builder.Append(" ring=");
            builder.Append(effectiveRingRadius.ToString("0.###", CultureInfo.InvariantCulture));
            builder.Append(" faults=0x");
            builder.Append(flags.ToString("X8", CultureInfo.InvariantCulture));
            builder.Append(' ');
            builder.Append(ResolveTerrainStreamingPagerFaultLabels(flags));
            builder.Append(" missing=");
            builder.Append(missingFileCount.ToString(CultureInfo.InvariantCulture));
            builder.Append(" worker=");
            builder.Append(workerSequence.ToString(CultureInfo.InvariantCulture));
            builder.Append(" aup=");
            builder.Append(cameraX.ToString("0.###", CultureInfo.InvariantCulture));
            builder.Append('/');
            builder.Append(cameraY.ToString("0.###", CultureInfo.InvariantCulture));
            builder.Append('/');
            builder.Append(cameraZ.ToString("0.###", CultureInfo.InvariantCulture));
            builder.Append(" state=0x");
            builder.Append(stateHash.ToString("X8", CultureInfo.InvariantCulture));
            return builder.ToString();
        }

        private static string BuildWorldChunkResidencyEntryLine(
            int displayIndex,
            int sourceIndex,
            int offset,
            ReadOnlySpan<byte> entry)
        {
            long focusChunkId = ReadI64(entry, 0);
            long gridX = ReadI64(entry, 8);
            long gridY = ReadI64(entry, 16);
            long gridZ = ReadI64(entry, 24);
            float localX = ReadF32(entry, 32);
            float localY = ReadF32(entry, 36);
            float localZ = ReadF32(entry, 40);
            uint frame = ReadU32(entry, 44);
            uint packedFlags = ReadU32(entry, 48);
            uint flags = packedFlags & 0x0000FFFFu;
            uint activeImpostorCount = packedFlags >> 16;
            uint stateHash = ReadU32(entry, 52);
            ushort pendingLoads = ReadU16(entry, 56);
            ushort residentCount = ReadU16(entry, 58);
            ushort loadingCount = ReadU16(entry, 60);
            ushort evictingCount = ReadU16(entry, 62);

            StringBuilder builder = new StringBuilder(300);
            builder.Append('#');
            builder.Append(displayIndex.ToString(CultureInfo.InvariantCulture));
            builder.Append(" slot=");
            builder.Append(sourceIndex.ToString(CultureInfo.InvariantCulture));
            builder.Append(" @");
            builder.Append(offset.ToString(CultureInfo.InvariantCulture));
            builder.Append(" frame=");
            builder.Append(frame.ToString(CultureInfo.InvariantCulture));
            builder.Append(" focus=");
            builder.Append(focusChunkId.ToString(CultureInfo.InvariantCulture));
            builder.Append(" grid=");
            builder.Append(gridX.ToString(CultureInfo.InvariantCulture));
            builder.Append('/');
            builder.Append(gridY.ToString(CultureInfo.InvariantCulture));
            builder.Append('/');
            builder.Append(gridZ.ToString(CultureInfo.InvariantCulture));
            builder.Append(" local=");
            builder.Append(localX.ToString("0.###", CultureInfo.InvariantCulture));
            builder.Append('/');
            builder.Append(localY.ToString("0.###", CultureInfo.InvariantCulture));
            builder.Append('/');
            builder.Append(localZ.ToString("0.###", CultureInfo.InvariantCulture));
            builder.Append(" pending=");
            builder.Append(pendingLoads.ToString(CultureInfo.InvariantCulture));
            builder.Append(" counts=");
            builder.Append(residentCount.ToString(CultureInfo.InvariantCulture));
            builder.Append('/');
            builder.Append(loadingCount.ToString(CultureInfo.InvariantCulture));
            builder.Append('/');
            builder.Append(evictingCount.ToString(CultureInfo.InvariantCulture));
            builder.Append(" impostors=");
            builder.Append(activeImpostorCount.ToString(CultureInfo.InvariantCulture));
            builder.Append(" flags=0x");
            builder.Append(flags.ToString("X4", CultureInfo.InvariantCulture));
            builder.Append(' ');
            builder.Append(ResolveWorldChunkResidencyFlagsLabel(flags));
            builder.Append(" state=0x");
            builder.Append(stateHash.ToString("X8", CultureInfo.InvariantCulture));
            return builder.ToString();
        }

        private static string ResolveTerrainStreamingPagerFaultLabels(uint flags)
        {
            if (flags == 0u)
                return "none";

            StringBuilder builder = new StringBuilder(160);
            AppendFlagLabel(builder, (flags & (1u << 0)) != 0u, "missing-file");
            AppendFlagLabel(builder, (flags & (1u << 1)) != 0u, "io");
            AppendFlagLabel(builder, (flags & (1u << 2)) != 0u, "queue-overflow");
            AppendFlagLabel(builder, (flags & (1u << 3)) != 0u, "lz4");
            AppendFlagLabel(builder, (flags & (1u << 4)) != 0u, "layout");
            AppendFlagLabel(builder, (flags & (1u << 5)) != 0u, "nonfinite-aup");
            AppendFlagLabel(builder, (flags & (1u << 6)) != 0u, "vault");
            AppendFlagLabel(builder, (flags & (1u << 7)) != 0u, "invalid-header");
            AppendFlagLabel(builder, (flags & (1u << 8)) != 0u, "checksum");
            AppendFlagLabel(builder, (flags & (1u << 9)) != 0u, "capacity");

            const uint knownFlags =
                (1u << 0) |
                (1u << 1) |
                (1u << 2) |
                (1u << 3) |
                (1u << 4) |
                (1u << 5) |
                (1u << 6) |
                (1u << 7) |
                (1u << 8) |
                (1u << 9);
            uint unknownFlags = flags & ~knownFlags;
            if (unknownFlags != 0u)
            {
                if (builder.Length != 0)
                    builder.Append('|');

                builder.Append("unknown=0x");
                builder.Append(unknownFlags.ToString("X8", CultureInfo.InvariantCulture));
            }

            return builder.Length == 0 ? "unknown" : builder.ToString();
        }

        private static string ResolveWorldChunkResidencyFlagsLabel(uint flags)
        {
            if (flags == 0u)
                return "none";

            StringBuilder builder = new StringBuilder(192);
            AppendFlagLabel(builder, (flags & (1u << 0)) != 0u, "invalid-aup");
            AppendFlagLabel(builder, (flags & (1u << 1)) != 0u, "shift");
            AppendFlagLabel(builder, (flags & (1u << 2)) != 0u, "memory-breach");
            AppendFlagLabel(builder, (flags & (1u << 3)) != 0u, "teleport");
            AppendFlagLabel(builder, (flags & (1u << 4)) != 0u, "predictive-suspended");
            AppendFlagLabel(builder, (flags & (1u << 5)) != 0u, "predictive-prewarm-fault");
            AppendFlagLabel(builder, (flags & (1u << 6)) != 0u, "activation-overflow");
            AppendFlagLabel(builder, (flags & (1u << 7)) != 0u, "duplicate-chunk");
            AppendFlagLabel(builder, (flags & (1u << 8)) != 0u, "additive-scene-fault");
            AppendFlagLabel(builder, (flags & (1u << 9)) != 0u, "release-all-reset");
            AppendFlagLabel(builder, (flags & (1u << 10)) != 0u, "addressables-fault");
            AppendFlagLabel(builder, (flags & (1u << 11)) != 0u, "activation-fault");
            AppendFlagLabel(builder, (flags & (1u << 12)) != 0u, "hydration-copy-spike");

            const uint knownFlags =
                (1u << 0) |
                (1u << 1) |
                (1u << 2) |
                (1u << 3) |
                (1u << 4) |
                (1u << 5) |
                (1u << 6) |
                (1u << 7) |
                (1u << 8) |
                (1u << 9) |
                (1u << 10) |
                (1u << 11) |
                (1u << 12);
            uint unknownFlags = flags & ~knownFlags;
            if (unknownFlags != 0u)
            {
                if (builder.Length != 0)
                    builder.Append('|');

                builder.Append("unknown=0x");
                builder.Append(unknownFlags.ToString("X8", CultureInfo.InvariantCulture));
            }

            return builder.Length == 0 ? "unknown" : builder.ToString();
        }

        private bool TryParseJobAdmissionDump(string path, byte[] bytes, ReadOnlySpan<byte> span)
        {
            if (!IsJobAdmissionDumpPath(path))
            {
                return false;
            }

            if (span.Length < JobAdmissionDumpHeaderBytes ||
                ReadU64(span, 0) != JobAdmissionDumpMagic)
            {
                SetSummary(BuildInvalidJobAdmissionHeaderSummary(path, span.Length, 0u, 0, 0, 0, 0u, 0u));
                return true;
            }

            uint version = ReadU32(span, 8);
            int entryCount = ReadI32(span, 12);
            int entrySize = ReadI32(span, 16);
            int cursor = ReadI32(span, 20);
            uint frameSequence = ReadU32(span, 24);
            uint reserved = ReadU32(span, 28);
            bool valid =
                version >= JobAdmissionDumpMinVersion &&
                version <= JobAdmissionDumpMaxVersion &&
                entryCount >= 0 &&
                entryCount <= 100000 &&
                entrySize == JobAdmissionDumpEntrySizeBytes &&
                cursor >= 0 &&
                (entryCount == 0 || cursor < entryCount) &&
                reserved == 0u &&
                JobAdmissionDumpHeaderBytes + (long)entryCount * entrySize <= span.Length;

            if (!valid)
            {
                SetSummary(BuildInvalidJobAdmissionHeaderSummary(
                    path,
                    span.Length,
                    version,
                    entryCount,
                    entrySize,
                    cursor,
                    frameSequence,
                    reserved));
                return true;
            }

            int nonEmptyEntryCount = CountJobAdmissionEntriesWithPayload(span, entryCount);
            int payloadBytes = entryCount * entrySize;
            ulong payloadHash = ComputeXxHash64(bytes, JobAdmissionDumpHeaderBytes, payloadBytes);
            StringBuilder builder = new StringBuilder(256);
            builder.Append(Path.GetFileName(path));
            builder.Append(" | bytes=");
            builder.Append(span.Length.ToString(CultureInfo.InvariantCulture));
            builder.Append(" | magic=HECTON8");
            builder.Append(" | version=");
            builder.Append(version.ToString(CultureInfo.InvariantCulture));
            builder.Append(" | layout=job-admission-blackbox");
            builder.Append(" | entries=");
            builder.Append(entryCount.ToString(CultureInfo.InvariantCulture));
            builder.Append(" | displayed=");
            builder.Append(math.min(nonEmptyEntryCount, MaxDisplayedFrames).ToString(CultureInfo.InvariantCulture));
            builder.Append('/');
            builder.Append(nonEmptyEntryCount.ToString(CultureInfo.InvariantCulture));
            builder.Append(" | entrySize=");
            builder.Append(entrySize.ToString(CultureInfo.InvariantCulture));
            builder.Append(" | cursor=");
            builder.Append(cursor.ToString(CultureInfo.InvariantCulture));
            builder.Append(" | frame=");
            builder.Append(frameSequence.ToString(CultureInfo.InvariantCulture));
            builder.Append(" | xxHash3[payload]=0x");
            builder.Append(payloadHash.ToString("X16", CultureInfo.InvariantCulture));
            SetSummary(builder.ToString());

            int skip = math.max(0, nonEmptyEntryCount - MaxDisplayedFrames);
            int seen = 0;
            for (int i = 0; i < entryCount; i++)
            {
                int offset = JobAdmissionDumpHeaderBytes + i * entrySize;
                ReadOnlySpan<byte> entry = span.Slice(offset, entrySize);
                if (IsEmptyJobAdmissionEntry(entry))
                    continue;

                if (seen++ < skip)
                    continue;

                _rows.Add(BuildJobAdmissionEntryLine(version, seen - 1, i, offset, entry));
            }

            return true;
        }

        private static string BuildInvalidJobAdmissionHeaderSummary(
            string path,
            int byteCount,
            uint version,
            int entryCount,
            int entrySize,
            int cursor,
            uint frameSequence,
            uint reserved)
        {
            StringBuilder builder = new StringBuilder(192);
            builder.Append(Path.GetFileName(path));
            builder.Append(" | invalid job-admission blackbox header");
            builder.Append(" | bytes=");
            builder.Append(byteCount.ToString(CultureInfo.InvariantCulture));
            builder.Append(" | version=");
            builder.Append(version.ToString(CultureInfo.InvariantCulture));
            builder.Append(" | entries=");
            builder.Append(entryCount.ToString(CultureInfo.InvariantCulture));
            builder.Append(" | entrySize=");
            builder.Append(entrySize.ToString(CultureInfo.InvariantCulture));
            builder.Append(" | cursor=");
            builder.Append(cursor.ToString(CultureInfo.InvariantCulture));
            builder.Append(" | frame=");
            builder.Append(frameSequence.ToString(CultureInfo.InvariantCulture));
            builder.Append(" | reserved=0x");
            builder.Append(reserved.ToString("X8", CultureInfo.InvariantCulture));
            return builder.ToString();
        }

        private static bool IsJobAdmissionDumpPath(string path)
        {
            string fileName = Path.GetFileName(path) ?? string.Empty;
            return fileName.IndexOf("JobAdmission", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static int CountJobAdmissionEntriesWithPayload(ReadOnlySpan<byte> bytes, int entryCount)
        {
            int count = 0;
            for (int i = 0; i < entryCount; i++)
            {
                int offset = JobAdmissionDumpHeaderBytes + i * JobAdmissionDumpEntrySizeBytes;
                if (offset < 0 || offset + JobAdmissionDumpEntrySizeBytes > bytes.Length)
                    break;

                if (!IsEmptyJobAdmissionEntry(bytes.Slice(offset, JobAdmissionDumpEntrySizeBytes)))
                    count++;
            }

            return count;
        }

        private static bool IsEmptyJobAdmissionEntry(ReadOnlySpan<byte> entry)
        {
            int scanned = math.min(32, entry.Length);
            for (int i = 0; i < scanned; i++)
            {
                if (entry[i] != 0)
                    return false;
            }

            return true;
        }

        private static string BuildJobAdmissionEntryLine(
            uint version,
            int displayIndex,
            int sourceIndex,
            int offset,
            ReadOnlySpan<byte> entry)
        {
            uint frameSequence = ReadU32(entry, 0);
            uint jobHash = ReadU32(entry, 4);
            float estimatedCostMs = ReadF32(entry, 8);
            float remainingBudgetMs = ReadF32(entry, 12);
            int criticalDebtFrames = ReadI32(entry, 16);
            uint killSwitchMask = ReadU32(entry, 20);
            byte lane = entry.Length > 24 ? entry[24] : (byte)0;
            byte flags = entry.Length > 25 ? entry[25] : (byte)0;
            uint stateHash = ReadU32(entry, 28);
            uint computedStateHash = ComputeJobAdmissionStateHash(
                frameSequence,
                jobHash,
                estimatedCostMs,
                remainingBudgetMs,
                criticalDebtFrames,
                killSwitchMask,
                flags);

            StringBuilder builder = new StringBuilder(220);
            builder.Append('#');
            builder.Append(displayIndex.ToString(CultureInfo.InvariantCulture));
            builder.Append(" slot=");
            builder.Append(sourceIndex.ToString(CultureInfo.InvariantCulture));
            builder.Append(" @");
            builder.Append(offset.ToString(CultureInfo.InvariantCulture));
            builder.Append(" frame=");
            builder.Append(frameSequence.ToString(CultureInfo.InvariantCulture));
            builder.Append(" lane=");
            builder.Append(lane.ToString(CultureInfo.InvariantCulture));
            builder.Append(" job=0x");
            builder.Append(jobHash.ToString("X8", CultureInfo.InvariantCulture));
            builder.Append(" est=");
            builder.Append(estimatedCostMs.ToString("0.###", CultureInfo.InvariantCulture));
            builder.Append(" rem=");
            builder.Append(remainingBudgetMs.ToString("0.###", CultureInfo.InvariantCulture));
            builder.Append(" debt=");
            builder.Append(criticalDebtFrames.ToString(CultureInfo.InvariantCulture));
            builder.Append(" kill=0x");
            builder.Append(killSwitchMask.ToString("X8", CultureInfo.InvariantCulture));
            builder.Append(" flags=0x");
            builder.Append(flags.ToString("X2", CultureInfo.InvariantCulture));
            builder.Append(' ');
            builder.Append(ResolveJobAdmissionFlagsLabel(version, flags));
            builder.Append(" hash=0x");
            builder.Append(stateHash.ToString("X8", CultureInfo.InvariantCulture));
            builder.Append(" hashOk=");
            builder.Append(stateHash == computedStateHash ? "yes" : "no");
            if (stateHash != computedStateHash)
            {
                builder.Append(" calc=0x");
                builder.Append(computedStateHash.ToString("X8", CultureInfo.InvariantCulture));
            }

            return builder.ToString();
        }

        private static string ResolveJobAdmissionFlagsLabel(uint version, byte flags)
        {
            if (flags == 0)
                return "none";

            StringBuilder builder = new StringBuilder(80);
            if (version >= 2u)
            {
                AppendFlagLabel(builder, (flags & (1 << 0)) != 0, "admitted");
                AppendFlagLabel(builder, (flags & (1 << 1)) != 0, "denied");
                AppendFlagLabel(builder, (flags & (1 << 2)) != 0, "aup");
                AppendFlagLabel(builder, (flags & (1 << 3)) != 0, "kill");
                AppendFlagLabel(builder, (flags & (1 << 4)) != 0, "budget");
                AppendFlagLabel(builder, (flags & (1 << 5)) != 0, "nonfinite");
            }
            else
            {
                AppendFlagLabel(builder, (flags & (1 << 0)) != 0, "legacy-starved");
                AppendFlagLabel(builder, (flags & (1 << 1)) != 0, "legacy-nonfinite");
            }

            return builder.Length == 0 ? "unknown" : builder.ToString();
        }

        private static void AppendFlagLabel(StringBuilder builder, bool active, string label)
        {
            if (!active)
                return;

            if (builder.Length != 0)
                builder.Append('|');

            builder.Append(label);
        }

        private static uint ComputeJobAdmissionStateHash(
            uint frameSequence,
            uint jobHash,
            float estimatedCostMs,
            float remainingBudgetMs,
            int criticalDebtFrames,
            uint killSwitchMask,
            byte flags)
        {
            unchecked
            {
                uint hash = 2166136261u;
                hash = (hash ^ frameSequence) * 16777619u;
                hash = (hash ^ jobHash) * 16777619u;
                hash = (hash ^ FloatBitsOrZero(estimatedCostMs)) * 16777619u;
                hash = (hash ^ FloatBitsOrZero(remainingBudgetMs)) * 16777619u;
                hash = (hash ^ (uint)criticalDebtFrames) * 16777619u;
                hash = (hash ^ killSwitchMask) * 16777619u;
                hash = (hash ^ flags) * 16777619u;
                return hash;
            }
        }

        private static uint FloatBitsOrZero(float value)
        {
            return float.IsNaN(value) || float.IsInfinity(value)
                ? 0u
                : math.asuint(value);
        }

        private static int ResolveGlobalTelemetrySourceSlot(
            ReadOnlySpan<byte> bytes,
            uint sourceCount,
            uint descriptorIndex,
            uint descriptorStride,
            uint descriptorCapacity,
            uint sourceHash)
        {
            if (sourceHash == 0u ||
                descriptorIndex == 0u ||
                descriptorStride < GlobalTelemetrySourceDescriptorWords)
            {
                return -1;
            }

            uint rows = math.min(sourceCount, descriptorCapacity);
            rows = math.min(rows, 50u);
            for (uint i = 0u; i < rows; i++)
            {
                if (!TryReadGlobalTelemetrySourceDescriptor(
                        bytes,
                        descriptorIndex,
                        descriptorStride,
                        i,
                        out uint descriptorSourceHash,
                        out _,
                        out _,
                        out uint descriptorSlot,
                        out _))
                {
                    break;
                }

                if (descriptorSourceHash == sourceHash)
                    return (int)descriptorSlot;
            }

            return -1;
        }

        private void AppendGlobalTelemetrySourceDescriptorRows(
            ReadOnlySpan<byte> bytes,
            uint sourceCount,
            uint descriptorIndex,
            uint descriptorStride,
            uint descriptorCapacity)
        {
            if (descriptorIndex == 0u || descriptorStride < GlobalTelemetrySourceDescriptorWords)
                return;

            uint rows = math.min(sourceCount, descriptorCapacity);
            rows = math.min(rows, 50u);
            for (uint i = 0u; i < rows; i++)
            {
                if (!TryReadGlobalTelemetrySourceDescriptor(
                        bytes,
                        descriptorIndex,
                        descriptorStride,
                        i,
                        out uint sourceHash,
                        out uint flags,
                        out uint payloadBytes,
                        out uint slot,
                        out int descriptorOffset))
                {
                    break;
                }

                if (sourceHash == 0u && flags == 0u && payloadBytes == 0u)
                    continue;

                StringBuilder builder = new StringBuilder(128);
                builder.Append("source slot=");
                builder.Append(slot.ToString(CultureInfo.InvariantCulture));
                builder.Append(" hash=0x");
                builder.Append(sourceHash.ToString("X8", CultureInfo.InvariantCulture));
                if (sourceHash == GlobalTelemetrySurvivalSourceHash)
                    builder.Append(" name=SURV");
                builder.Append(" flags=0x");
                builder.Append(flags.ToString("X8", CultureInfo.InvariantCulture));
                builder.Append(" bytes=");
                builder.Append(payloadBytes.ToString(CultureInfo.InvariantCulture));
                builder.Append(" descriptorOffset=");
                builder.Append(descriptorOffset.ToString(CultureInfo.InvariantCulture));
                _rows.Add(builder.ToString());
            }
        }

        private static bool TryReadGlobalTelemetrySourceDescriptor(
            ReadOnlySpan<byte> bytes,
            uint descriptorIndex,
            uint descriptorStride,
            uint row,
            out uint sourceHash,
            out uint flags,
            out uint payloadBytes,
            out uint slot,
            out int descriptorOffset)
        {
            sourceHash = 0u;
            flags = 0u;
            payloadBytes = 0u;
            slot = 0u;
            descriptorOffset = 0;
            long descriptorOffset64 =
                GlobalTelemetryMetadataOffset +
                ((long)descriptorIndex + (long)row * descriptorStride) * 4L;
            if (descriptorOffset64 < GlobalTelemetryMetadataOffset ||
                descriptorOffset64 + 16L > GlobalTelemetryDumpHeaderBytes ||
                descriptorOffset64 + 16L > bytes.Length)
            {
                return false;
            }

            descriptorOffset = (int)descriptorOffset64;
            sourceHash = ReadU32(bytes, descriptorOffset);
            flags = ReadU32(bytes, descriptorOffset + 4);
            payloadBytes = ReadU32(bytes, descriptorOffset + 8);
            slot = ReadU32(bytes, descriptorOffset + 12);
            return true;
        }

        private static string BuildGenericEntryLine(
            int displayIndex,
            int sourceIndex,
            int offset,
            ReadOnlySpan<byte> entry)
        {
            uint frame = entry.Length >= 4 ? ReadU32(entry, 0) : 0u;
            StringBuilder builder = new StringBuilder(160);
            builder.Append('#');
            builder.Append(displayIndex.ToString(CultureInfo.InvariantCulture));
            if (sourceIndex != displayIndex)
            {
                builder.Append(" slot=");
                builder.Append(sourceIndex.ToString(CultureInfo.InvariantCulture));
            }

            builder.Append(" @");
            builder.Append(offset.ToString(CultureInfo.InvariantCulture));
            builder.Append(" frame=");
            builder.Append(frame.ToString(CultureInfo.InvariantCulture));
            builder.Append(" bytes=");
            int preview = math.min(32, entry.Length);
            for (int i = 0; i < preview; i++)
            {
                if (i != 0)
                    builder.Append(' ');
                builder.Append(entry[i].ToString("X2", CultureInfo.InvariantCulture));
            }

            return builder.ToString();
        }

        private static string BuildGlobalTelemetryFrameLine(
            int displayIndex,
            int sourceIndex,
            int offset,
            ReadOnlySpan<byte> entry,
            int survivalSourceSlot,
            int sourcePayloadOffsetBytes)
        {
            ulong timestampTicks = ReadU64(entry, 0);
            uint frame = ReadU32(entry, 8);
            uint fatalHash = ReadU32(entry, 12);
            uint lastEventHash = entry.Length >= GlobalTelemetryHashHistoryOffsetBytes + GlobalTelemetryHashHistoryCount * 4
                ? ReadU32(entry, GlobalTelemetryHashHistoryOffsetBytes + (GlobalTelemetryHashHistoryCount - 1) * 4)
                : 0u;
            uint firstSourceHash = sourcePayloadOffsetBytes >= 0 && entry.Length >= sourcePayloadOffsetBytes + 4
                ? ReadU32(entry, sourcePayloadOffsetBytes)
                : 0u;

            StringBuilder builder = new StringBuilder(220);
            builder.Append('#');
            builder.Append(displayIndex.ToString(CultureInfo.InvariantCulture));
            if (sourceIndex != displayIndex)
            {
                builder.Append(" slot=");
                builder.Append(sourceIndex.ToString(CultureInfo.InvariantCulture));
            }

            builder.Append(" @");
            builder.Append(offset.ToString(CultureInfo.InvariantCulture));
            builder.Append(" frame=");
            builder.Append(frame.ToString(CultureInfo.InvariantCulture));
            builder.Append(" fatal=0x");
            builder.Append(fatalHash.ToString("X8", CultureInfo.InvariantCulture));
            builder.Append(" last=0x");
            builder.Append(lastEventHash.ToString("X8", CultureInfo.InvariantCulture));
            builder.Append(" source0=0x");
            builder.Append(firstSourceHash.ToString("X8", CultureInfo.InvariantCulture));
            builder.Append(" ticks=");
            builder.Append(timestampTicks.ToString(CultureInfo.InvariantCulture));
            AppendGlobalTelemetrySurvivalPayload(builder, entry, survivalSourceSlot, sourcePayloadOffsetBytes);
            return builder.ToString();
        }

        private static void AppendGlobalTelemetrySurvivalPayload(
            StringBuilder builder,
            ReadOnlySpan<byte> entry,
            int survivalSourceSlot,
            int sourcePayloadOffsetBytes)
        {
            if (builder == null || survivalSourceSlot < 0)
                return;

            int payloadOffset =
                sourcePayloadOffsetBytes +
                survivalSourceSlot * GlobalTelemetrySourcePayloadStrideBytes;
            if (payloadOffset < 0 ||
                payloadOffset + GlobalTelemetrySourcePayloadStrideBytes > entry.Length ||
                ReadU32(entry, payloadOffset) != GlobalTelemetrySurvivalSourceHash)
            {
                return;
            }

            builder.Append(" survFrame=");
            builder.Append(ReadU32(entry, payloadOffset + 4).ToString(CultureInfo.InvariantCulture));
            builder.Append(" o2=");
            builder.Append(ReadF32(entry, payloadOffset + 12).ToString("0.000", CultureInfo.InvariantCulture));
            builder.Append(" integrity=");
            builder.Append(ReadF32(entry, payloadOffset + 16).ToString("0.000", CultureInfo.InvariantCulture));
            builder.Append(" depth=");
            builder.Append(ReadF32(entry, payloadOffset + 20).ToString("0.0", CultureInfo.InvariantCulture));
            builder.Append(" atm=");
            builder.Append(ReadF32(entry, payloadOffset + 24).ToString("0.00", CultureInfo.InvariantCulture));
            builder.Append(" deco=");
            builder.Append(ReadF32(entry, payloadOffset + 48).ToString("0.000", CultureInfo.InvariantCulture));
            builder.Append(" status=0x");
            builder.Append(ReadU32(entry, payloadOffset + 56).ToString("X8", CultureInfo.InvariantCulture));
            uint flags = ReadU32(entry, payloadOffset + 60);
            builder.Append(" death=");
            builder.Append(ResolveSurvivalDeathCauseLabel(flags));
            builder.Append(" flags=0x");
            builder.Append(flags.ToString("X8", CultureInfo.InvariantCulture));
        }

        private static string ResolveSurvivalDeathCauseLabel(uint flags)
        {
            uint cause = (flags >> GlobalTelemetrySurvivalDeathCauseShift) & 0xFFu;
            switch (cause)
            {
                case 0u:
                    return "none";
                case 1u:
                    return "oxygen";
                case 2u:
                    return "pressure";
                case 3u:
                    return "thermal";
                case 4u:
                    return "radiation";
                case 5u:
                    return "starvation";
                case 6u:
                    return "dehydration";
                case 7u:
                    return "integrity";
                default:
                    return cause.ToString(CultureInfo.InvariantCulture);
            }
        }

        private static string BuildHazardZoneEntryLine(
            int displayIndex,
            int sourceIndex,
            int offset,
            ReadOnlySpan<byte> entry)
        {
            ulong packedOwner = ReadU64(entry, 0);
            uint frame = ReadU32(entry, 8);
            uint sequence = ReadU32(entry, 12);
            uint stateHash = ReadU32(entry, 16);
            uint flags = ReadU32(entry, 20);
            int activeZones = ReadI32(entry, 24);
            int pendingMutations = ReadI32(entry, 28);
            int publishedMask = ReadI32(entry, 32);
            uint generation = ReadU32(entry, 36);
            float toxicityDose = ReadF32(entry, 40);
            float toxicityPulse = ReadF32(entry, 44);
            float playerToxicity = ReadF32(entry, 48);
            float vehicleToxicity = ReadF32(entry, 52);
            float playerRadiation = ReadF32(entry, 56);
            float vehicleRadiation = ReadF32(entry, 60);
            uint computedStateHash = ComputeHazardZoneTelemetryStateHash(
                sequence,
                flags,
                activeZones,
                pendingMutations,
                publishedMask,
                ReadU32(entry, 40),
                ReadU32(entry, 44),
                ReadU32(entry, 48),
                ReadU32(entry, 52),
                ReadU32(entry, 56),
                ReadU32(entry, 60));

            StringBuilder builder = new StringBuilder(220);
            builder.Append('#');
            builder.Append(displayIndex.ToString(CultureInfo.InvariantCulture));
            builder.Append(" slot=");
            builder.Append(sourceIndex.ToString(CultureInfo.InvariantCulture));
            builder.Append(" @");
            builder.Append(offset.ToString(CultureInfo.InvariantCulture));
            builder.Append(" frame=");
            builder.Append(frame.ToString(CultureInfo.InvariantCulture));
            builder.Append(" seq=");
            builder.Append(sequence.ToString(CultureInfo.InvariantCulture));
            builder.Append(" flags=0x");
            builder.Append(flags.ToString("X8", CultureInfo.InvariantCulture));
            builder.Append(" active=");
            builder.Append(activeZones.ToString(CultureInfo.InvariantCulture));
            builder.Append(" pending=");
            builder.Append(pendingMutations.ToString(CultureInfo.InvariantCulture));
            builder.Append(" mask=0x");
            builder.Append(publishedMask.ToString("X8", CultureInfo.InvariantCulture));
            builder.Append(" gen=");
            builder.Append(generation.ToString(CultureInfo.InvariantCulture));
            builder.Append(" owner=0x");
            builder.Append(packedOwner.ToString("X16", CultureInfo.InvariantCulture));
            builder.Append(" dose=");
            builder.Append(toxicityDose.ToString("0.###", CultureInfo.InvariantCulture));
            builder.Append(" pulse=");
            builder.Append(toxicityPulse.ToString("0.###", CultureInfo.InvariantCulture));
            builder.Append(" tox=");
            builder.Append(playerToxicity.ToString("0.###", CultureInfo.InvariantCulture));
            builder.Append('/');
            builder.Append(vehicleToxicity.ToString("0.###", CultureInfo.InvariantCulture));
            builder.Append(" rad=");
            builder.Append(playerRadiation.ToString("0.###", CultureInfo.InvariantCulture));
            builder.Append('/');
            builder.Append(vehicleRadiation.ToString("0.###", CultureInfo.InvariantCulture));
            builder.Append(" hash=0x");
            builder.Append(stateHash.ToString("X8", CultureInfo.InvariantCulture));
            builder.Append(" hashOk=");
            builder.Append(stateHash == computedStateHash ? "yes" : "no");
            if (stateHash != computedStateHash)
            {
                builder.Append(" calc=0x");
                builder.Append(computedStateHash.ToString("X8", CultureInfo.InvariantCulture));
            }

            return builder.ToString();
        }

        private static uint ComputeHazardZoneTelemetryStateHash(
            uint sequence,
            uint flags,
            int activeZoneCount,
            int pendingMutationCount,
            int publishedExposureMask,
            uint toxicityDoseBits,
            uint toxicityPulseBits,
            uint playerToxicityBits,
            uint vehicleToxicityBits,
            uint playerRadiationBits,
            uint vehicleRadiationBits)
        {
            uint hash = 2166136261u;
            hash = MixHazardTelemetryHash(hash, sequence);
            hash = MixHazardTelemetryHash(hash, flags);
            hash = MixHazardTelemetryHash(hash, unchecked((uint)activeZoneCount));
            hash = MixHazardTelemetryHash(hash, unchecked((uint)pendingMutationCount));
            hash = MixHazardTelemetryHash(hash, unchecked((uint)publishedExposureMask));
            hash = MixHazardTelemetryHash(hash, toxicityDoseBits);
            hash = MixHazardTelemetryHash(hash, toxicityPulseBits);
            hash = MixHazardTelemetryHash(hash, playerToxicityBits);
            hash = MixHazardTelemetryHash(hash, vehicleToxicityBits);
            hash = MixHazardTelemetryHash(hash, playerRadiationBits);
            hash = MixHazardTelemetryHash(hash, vehicleRadiationBits);
            return hash;
        }

        private static uint MixHazardTelemetryHash(uint hash, uint value)
        {
            return unchecked((hash ^ value) * 16777619u);
        }

        private static int ResolveDisplayedEntryCount(
            uint magic,
            int entryCount,
            uint hazardSequence,
            ReadOnlySpan<byte> bytes,
            int headerBytes,
            int entrySize)
        {
            if (magic != HazardZoneDumpMagic || entryCount <= 0)
                return entryCount;

            if (hazardSequence >= (uint)entryCount)
                return entryCount;

            int nonEmptyEntryCount = CountHazardTelemetryEntriesWithPayload(bytes, headerBytes, entrySize, entryCount);
            return math.clamp(math.max((int)hazardSequence, nonEmptyEntryCount), 0, entryCount);
        }

        private static int CountHazardTelemetryEntriesWithPayload(
            ReadOnlySpan<byte> bytes,
            int headerBytes,
            int entrySize,
            int entryCount)
        {
            if (headerBytes < 0 || entrySize < 64 || entryCount <= 0)
                return 0;

            int nonEmptyEntryCount = 0;
            for (int i = 0; i < entryCount; i++)
            {
                long offset64 = headerBytes + (long)i * entrySize;
                if (offset64 < 0L || offset64 + 16L > bytes.Length)
                    break;

                int offset = (int)offset64;
                uint frame = ReadU32(bytes, offset + 8);
                uint sequence = ReadU32(bytes, offset + 12);
                if (frame != 0u || sequence != 0u)
                    nonEmptyEntryCount++;
            }

            return nonEmptyEntryCount;
        }

        private static int ResolveSourceEntryIndex(
            uint magic,
            int logicalIndex,
            int entryCount,
            int displayedEntryCount,
            uint hazardWriteIndex)
        {
            if (magic != HazardZoneDumpMagic ||
                entryCount <= 0 ||
                displayedEntryCount < entryCount)
            {
                return logicalIndex;
            }

            int startIndex = (int)(hazardWriteIndex % (uint)entryCount);
            return (startIndex + logicalIndex) % entryCount;
        }

        private static string ResolveDefaultDumpDirectory()
        {
            string root = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
            return Path.Combine(root, "Docs", "AgentLogs");
        }

        private static string ResolveReadableDumpPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || File.Exists(path))
                return path;

            if (!Directory.Exists(path))
                return path;

            string newest = null;
            DateTime newestWriteTimeUtc = DateTime.MinValue;
            SelectNewestDump(path, "*.bin", ref newest, ref newestWriteTimeUtc);
            SelectNewestDump(path, "*.h8dump", ref newest, ref newestWriteTimeUtc);
            return newest ?? path;
        }

        private static void SelectNewestDump(
            string directory,
            string searchPattern,
            ref string newest,
            ref DateTime newestWriteTimeUtc)
        {
            string[] files;
            try
            {
                files = Directory.GetFiles(directory, searchPattern, SearchOption.TopDirectoryOnly);
            }
            catch (IOException)
            {
                return;
            }
            catch (UnauthorizedAccessException)
            {
                return;
            }

            for (int i = 0; i < files.Length; i++)
            {
                DateTime writeTimeUtc;
                try
                {
                    writeTimeUtc = File.GetLastWriteTimeUtc(files[i]);
                }
                catch (IOException)
                {
                    continue;
                }
                catch (UnauthorizedAccessException)
                {
                    continue;
                }

                if (writeTimeUtc <= newestWriteTimeUtc)
                    continue;

                newestWriteTimeUtc = writeTimeUtc;
                newest = files[i];
            }
        }

        private static bool IsSaneCount(uint value)
        {
            return value > 0u && value <= 100000u;
        }

        private static bool IsSaneEntrySize(uint value)
        {
            return value >= 16u && value <= 1024u && (value & 3u) == 0u;
        }

        private static int ResolveFallbackEntrySize(int payloadBytes)
        {
            if (payloadBytes >= 300 * 64 && payloadBytes % 64 == 0)
                return 64;
            if (payloadBytes >= 300 * 128 && payloadBytes % 128 == 0)
                return 128;
            return 32;
        }

        private static uint ReadU32(ReadOnlySpan<byte> bytes, int offset)
        {
            if (offset < 0 || offset + 4 > bytes.Length)
                return 0u;
            return BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(offset, 4));
        }

        private static ushort ReadU16(ReadOnlySpan<byte> bytes, int offset)
        {
            if (offset < 0 || offset + 2 > bytes.Length)
                return 0;
            return BinaryPrimitives.ReadUInt16LittleEndian(bytes.Slice(offset, 2));
        }

        private static ulong ReadU64(ReadOnlySpan<byte> bytes, int offset)
        {
            if (offset < 0 || offset + 8 > bytes.Length)
                return 0UL;
            return BinaryPrimitives.ReadUInt64LittleEndian(bytes.Slice(offset, 8));
        }

        private static long ReadI64(ReadOnlySpan<byte> bytes, int offset)
        {
            if (offset < 0 || offset + 8 > bytes.Length)
                return 0L;
            return BinaryPrimitives.ReadInt64LittleEndian(bytes.Slice(offset, 8));
        }

        private static int ReadI32(ReadOnlySpan<byte> bytes, int offset)
        {
            if (offset < 0 || offset + 4 > bytes.Length)
                return 0;
            return BinaryPrimitives.ReadInt32LittleEndian(bytes.Slice(offset, 4));
        }

        private static float ReadF32(ReadOnlySpan<byte> bytes, int offset)
        {
            return BitConverter.Int32BitsToSingle(ReadI32(bytes, offset));
        }

        private static double ReadF64(ReadOnlySpan<byte> bytes, int offset)
        {
            return BitConverter.Int64BitsToDouble(ReadI64(bytes, offset));
        }

        private static unsafe ulong ComputeXxHash64(byte[] bytes, int offset, int length)
        {
            if (bytes == null || offset < 0 || length <= 0 || offset + length > bytes.Length)
                return 0UL;

            fixed (byte* ptr = bytes)
            {
                return MemorySentinelMath.ComputeXXHash3Full64(new ReadOnlySpan<byte>(ptr + offset, length));
            }
        }
    }
}
