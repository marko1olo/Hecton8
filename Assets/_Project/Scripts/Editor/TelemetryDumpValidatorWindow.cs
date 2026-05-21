using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Hecton8.EditorTools
{
    public sealed class TelemetryDumpValidatorWindow : EditorWindow
    {
        private const int MaxDisplayedFrames = 300;
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
            bool indexedLayout = entryCount > 0 &&
                                 entrySize > 0 &&
                                 headerBytes + (long)entryCount * entrySize <= bytes.Length;

            if (!indexedLayout)
            {
                entrySize = ResolveFallbackEntrySize(bytes.Length - headerBytes);
                entryCount = math.max(0, (bytes.Length - headerBytes) / entrySize);
                indexedLayout = entryCount > 0;
            }

            ulong hashFrom16 = ComputeXxHash64(bytes, 16, bytes.Length - 16);
            ulong storedAt8 = bytes.Length >= 16 ? ReadU64(span, 8) : 0UL;
            ulong storedAt16 = bytes.Length >= 24 ? ReadU64(span, 16) : 0UL;
            bool checksumAt8 = storedAt8 != 0UL && storedAt8 == hashFrom16;
            bool checksumAt16 = storedAt16 != 0UL && bytes.Length > 24 && storedAt16 == ComputeXxHash64(bytes, 24, bytes.Length - 24);

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
            builder.Append(" | entries=");
            builder.Append(entryCount.ToString(CultureInfo.InvariantCulture));
            builder.Append(" | entrySize=");
            builder.Append(entrySize.ToString(CultureInfo.InvariantCulture));
            builder.Append(" | xxHash3[16..]=0x");
            builder.Append(hashFrom16.ToString("X16", CultureInfo.InvariantCulture));
            builder.Append(" | checksumMatch=");
            builder.Append(checksumAt8 || checksumAt16 ? "yes" : "no");
            SetSummary(builder.ToString());

            if (!indexedLayout)
                return;

            int shown = math.min(MaxDisplayedFrames, entryCount);
            int first = math.max(0, entryCount - shown);
            for (int i = first; i < entryCount; i++)
            {
                int offset = headerBytes + i * entrySize;
                if (offset < 0 || offset >= bytes.Length)
                    break;

                int available = math.min(entrySize, bytes.Length - offset);
                _rows.Add(BuildEntryLine(i, offset, span.Slice(offset, available)));
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

        private static string BuildEntryLine(int index, int offset, ReadOnlySpan<byte> entry)
        {
            uint frame = entry.Length >= 4 ? ReadU32(entry, 0) : 0u;
            StringBuilder builder = new StringBuilder(160);
            builder.Append('#');
            builder.Append(index.ToString(CultureInfo.InvariantCulture));
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

        private static string ResolveDefaultDumpDirectory()
        {
            string root = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
            return Path.Combine(root, "Docs", "AgentLogs");
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

        private static unsafe ulong ComputeXxHash64(byte[] bytes, int offset, int length)
        {
            if (bytes == null || offset < 0 || length <= 0 || offset + length > bytes.Length)
                return 0UL;

            fixed (byte* ptr = bytes)
            {
                uint2 hash = xxHash3.Hash64(ptr + offset, length);
                return ((ulong)hash.y << 32) | hash.x;
            }
        }
    }
}
