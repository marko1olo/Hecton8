using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
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

        private static ulong ReadU64(ReadOnlySpan<byte> bytes, int offset)
        {
            if (offset < 0 || offset + 8 > bytes.Length)
                return 0UL;
            return BinaryPrimitives.ReadUInt64LittleEndian(bytes.Slice(offset, 8));
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
