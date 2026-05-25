#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Hecton.Localization;
using Hecton8.Core.Data;
using UnityEditor;
using UnityEngine;

namespace Hecton8.UI.Editor
{
    public sealed class BabelLocalizationManagerWindow : EditorWindow
    {
        private const int HeaderSizeBytes = 32;
        private const int EntrySizeBytes = 16;
        private const int PreviewEntryLimit = 64;

        private string _dictionaryPath;
        private string _csvOverridePath;
        private string _savePath;
        private string _status;
        private string _searchFilter;
        private Vector2 _scroll;
        private int _previewIndex;
        private DateTime _lastCsvWriteUtc;
        private double _nextCsvPollTime;
        private bool _autoIngestCsv = true;
        private int _paddingBytes;
        private int _decryptionPreviewIndex;
        private int _decryptionMaskByte;
        private byte[] _bytes;
        private EditorBabelEntry[] _entries;
        private readonly List<EditorBabelText> _csvOverrides = new List<EditorBabelText>(128);

        [MenuItem("Hecton8/UI/Babel Dictionary Diagnostics")]
        public static void Open()
        {
            GetWindow<BabelLocalizationManagerWindow>("Babel Dictionary Diagnostics");
        }

        private void OnEnable()
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            _dictionaryPath = Path.Combine(projectRoot, "Data", "Balance", "Baked", H8StaticDataFormat.BabelDictionaryFileName);
            _csvOverridePath = Path.Combine(projectRoot, "loc_overrides.csv");
            _savePath = _dictionaryPath;
            _status = "No dictionary loaded.";
            SceneView.duringSceneGui -= DrawScenePreview;
            SceneView.duringSceneGui += DrawScenePreview;
            EditorApplication.update -= PollCsvOverride;
            EditorApplication.update += PollCsvOverride;
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= DrawScenePreview;
            EditorApplication.update -= PollCsvOverride;
        }

        private void OnGUI()
        {
            _dictionaryPath = EditorGUILayout.TextField("Dictionary", _dictionaryPath);
            _csvOverridePath = EditorGUILayout.TextField("CSV Override", _csvOverridePath);
            _savePath = EditorGUILayout.TextField("Save Target", _savePath);
            _autoIngestCsv = EditorGUILayout.Toggle("Auto-Ingest CSV", _autoIngestCsv);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Load"))
                LoadDictionary();
            if (GUILayout.Button("Ingest CSV"))
                IngestCsvOverrides();
            using (new EditorGUI.DisabledScope(_entries == null || _entries.Length == 0))
            {
                if (GUILayout.Button("Save .h8bin"))
                    SaveOverrideCopy();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.HelpBox(_status, MessageType.Info);

            if (_entries == null)
                return;

            DrawPaddingDiagnostics();
            DrawDecryptionDebug();

            _searchFilter = EditorGUILayout.TextField("Search", _searchFilter);
            _previewIndex = EditorGUILayout.IntSlider("Preview Entry", _previewIndex, 0, Mathf.Max(0, _entries.Length - 1));
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            int displayed = 0;
            for (int i = 0; i < _entries.Length && displayed < PreviewEntryLimit; i++)
            {
                EditorBabelEntry entry = _entries[i];
                if (!MatchesSearch(in entry, _searchFilter))
                    continue;

                displayed++;
                EditorGUILayout.LabelField(
                    "0x" + entry.Hash.ToString("X8") + "  +" + entry.Offset + "  len " + entry.Length,
                    EditorStyles.boldLabel);
                EditorGUILayout.TextArea(ResolvePreviewText(in entry), GUILayout.MinHeight(36f));
            }
            EditorGUILayout.EndScrollView();
        }

        private void DrawPaddingDiagnostics()
        {
            int length = _bytes != null ? _bytes.Length : 0;
            EditorGUILayout.LabelField("Binary Bytes", length.ToString());
            EditorGUILayout.LabelField("16-Byte Alignment", (length & 15) == 0 ? "OK" : "BROKEN");
            EditorGUILayout.LabelField("Trailing Zero Padding", _paddingBytes.ToString());

            if (_paddingBytes <= 0 || _bytes == null)
                return;

            int start = Mathf.Max(0, _bytes.Length - _paddingBytes);
            int count = Mathf.Min(_paddingBytes, 16);
            StringBuilder builder = new StringBuilder(count * 3);
            for (int i = 0; i < count; i++)
            {
                if (i > 0)
                    builder.Append(' ');

                builder.Append(_bytes[start + i].ToString("X2"));
            }

            EditorGUILayout.LabelField("Padding Bytes", builder.ToString(), EditorStyles.boldLabel);
        }

        private void DrawDecryptionDebug()
        {
            if (_entries == null || _entries.Length == 0)
                return;

            _decryptionPreviewIndex = EditorGUILayout.IntSlider("Decrypt Preview", _decryptionPreviewIndex, 0, Mathf.Max(0, _entries.Length - 1));
            _decryptionMaskByte = EditorGUILayout.IntSlider("XOR Mask", _decryptionMaskByte, 0, 255);

            EditorBabelEntry entry = _entries[Mathf.Clamp(_decryptionPreviewIndex, 0, _entries.Length - 1)];
            string preview = ResolveXorPreview(in entry, (byte)_decryptionMaskByte);
            EditorGUILayout.TextArea(preview, GUILayout.MinHeight(48f));
        }

        private void DrawScenePreview(SceneView sceneView)
        {
            if (_entries == null || _entries.Length == 0)
                return;

            int safeIndex = Mathf.Clamp(_previewIndex, 0, _entries.Length - 1);
            EditorBabelEntry entry = _entries[safeIndex];
            string preview = ResolvePreviewText(in entry);
            Handles.BeginGUI();
            GUI.Box(new Rect(16f, 16f, 520f, 86f), GUIContent.none);
            GUI.Label(new Rect(28f, 24f, 496f, 18f), "Babel Preview 0x" + entry.Hash.ToString("X8"), EditorStyles.boldLabel);
            GUI.Label(new Rect(28f, 46f, 496f, 48f), preview);
            Handles.EndGUI();
        }

        private void PollCsvOverride()
        {
            if (!_autoIngestCsv || string.IsNullOrEmpty(_csvOverridePath) || EditorApplication.timeSinceStartup < _nextCsvPollTime)
                return;

            _nextCsvPollTime = EditorApplication.timeSinceStartup + 1.0;
            if (!File.Exists(_csvOverridePath))
                return;

            DateTime writeTimeUtc = File.GetLastWriteTimeUtc(_csvOverridePath);
            if (writeTimeUtc == _lastCsvWriteUtc)
                return;

            _lastCsvWriteUtc = writeTimeUtc;
            IngestCsvOverrides();
            Repaint();
            SceneView.RepaintAll();
        }

        private void LoadDictionary()
        {
            if (string.IsNullOrEmpty(_dictionaryPath) || !File.Exists(_dictionaryPath))
            {
                _status = "Dictionary file missing.";
                _entries = null;
                _bytes = null;
                _paddingBytes = 0;
                return;
            }

            _bytes = File.ReadAllBytes(_dictionaryPath);
            _paddingBytes = CountTrailingZeroPadding(_bytes);
            if (_bytes.Length < HeaderSizeBytes)
            {
                _status = "Header too small.";
                _entries = null;
                return;
            }

            uint magic = ReadUInt32(_bytes, 0);
            ushort version = ReadUInt16(_bytes, 4);
            ushort headerSize = ReadUInt16(_bytes, 6);
            uint entryCount = ReadUInt32(_bytes, 8);
            uint indexOffset = ReadUInt32(_bytes, 12);
            uint dataOffset = ReadUInt32(_bytes, 16);
            uint fileByteLength = ReadUInt32(_bytes, 20);
            uint payloadCrc = ReadUInt32(_bytes, 24);
            long indexEnd = (long)indexOffset + ((long)entryCount * EntrySizeBytes);

            if (magic != H8StaticDataFormat.BabelMagic ||
                version != H8StaticDataFormat.FormatVersion ||
                headerSize != HeaderSizeBytes ||
                fileByteLength != _bytes.Length ||
                entryCount > int.MaxValue ||
                indexEnd > dataOffset ||
                dataOffset > _bytes.Length)
            {
                _status = "Invalid Babel header.";
                _entries = null;
                return;
            }

            _entries = new EditorBabelEntry[(int)entryCount];
            for (int i = 0; i < _entries.Length; i++)
            {
                int offset = (int)indexOffset + (i * EntrySizeBytes);
                _entries[i] = new EditorBabelEntry
                {
                    Hash = ReadUInt32(_bytes, offset),
                    Offset = ReadUInt32(_bytes, offset + 4),
                    Length = ReadUInt32(_bytes, offset + 8),
                    Flags = ReadUInt32(_bytes, offset + 12)
                };
            }

            uint computedCrc = ComputeCrc32(_bytes, HeaderSizeBytes, _bytes.Length - HeaderSizeBytes);
            _status = "Loaded " + _entries.Length + " entries. CRC " +
                (computedCrc == payloadCrc ? "OK" : "MISMATCH") + " 0x" + payloadCrc.ToString("X8") +
                ". Padding " + _paddingBytes + " bytes. Alignment " + ((_bytes.Length & 15) == 0 ? "OK" : "BROKEN") + ".";
        }

        private void IngestCsvOverrides()
        {
            _csvOverrides.Clear();
            if (string.IsNullOrEmpty(_csvOverridePath) || !File.Exists(_csvOverridePath))
            {
                _status = "CSV override file missing.";
                return;
            }

            foreach (string line in File.ReadLines(_csvOverridePath))
            {
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#", StringComparison.Ordinal))
                    continue;

                if (TryParseCsvLine(line, out uint hash, out string value))
                    UpsertCsvOverride(hash, value);
            }

            if (LocRegistry.TryApplyLocOverridesCsv(_csvOverridePath, out int applied, out int rejected))
            {
                _status = "Ingested " + _csvOverrides.Count + " CSV overrides. Runtime applied=" + applied + " rejected=" + rejected + ".";
                return;
            }

            _status = "Ingested " + _csvOverrides.Count + " CSV overrides. Runtime blob not active.";
        }

        private void SaveOverrideCopy()
        {
            if (_entries == null || _bytes == null)
            {
                _status = "Load a dictionary before saving.";
                return;
            }

            if (_csvOverrides.Count == 0)
                IngestCsvOverrides();

            List<EditorBabelText> texts = new List<EditorBabelText>(_entries.Length);
            for (int i = 0; i < _entries.Length; i++)
            {
                EditorBabelEntry entry = _entries[i];
                string text = TryGetCsvOverride(entry.Hash, out string overrideText)
                    ? overrideText
                    : ResolvePreviewText(in entry);
                texts.Add(new EditorBabelText
                {
                    Hash = entry.Hash,
                    Flags = entry.Flags,
                    Text = text
                });
            }

            byte[] output = BuildDictionaryBytes(texts);
            string directory = Path.GetDirectoryName(_savePath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            File.WriteAllBytes(_savePath, output);
            _status = "Saved override copy: " + _savePath + " bytes=" + output.Length + " alignment=" + ((output.Length & 15) == 0 ? "OK" : "BROKEN") + ".";
        }

        private string ResolvePreviewText(in EditorBabelEntry entry)
        {
            if (TryGetCsvOverride(entry.Hash, out string overrideText))
                return overrideText;

            if (_bytes == null ||
                entry.Offset > int.MaxValue ||
                entry.Length > int.MaxValue ||
                (int)entry.Offset > _bytes.Length ||
                (int)entry.Length > _bytes.Length - (int)entry.Offset)
            {
                return "[BAD_SLICE]";
            }

            return Encoding.UTF8.GetString(_bytes, (int)entry.Offset, (int)entry.Length);
        }

        private string ResolveXorPreview(in EditorBabelEntry entry, byte mask)
        {
            if (_bytes == null ||
                entry.Offset > int.MaxValue ||
                entry.Length > int.MaxValue ||
                (int)entry.Offset > _bytes.Length ||
                (int)entry.Length > _bytes.Length - (int)entry.Offset)
            {
                return "[BAD_SLICE]";
            }

            int length = Mathf.Min((int)entry.Length, 4096);
            byte[] scratch = new byte[length];
            for (int i = 0; i < length; i++)
                scratch[i] = (byte)(_bytes[(int)entry.Offset + i] ^ mask);

            return Encoding.UTF8.GetString(scratch, 0, scratch.Length);
        }

        private bool MatchesSearch(in EditorBabelEntry entry, string search)
        {
            if (string.IsNullOrEmpty(search))
                return true;

            string hash = entry.Hash.ToString("X8");
            if (hash.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0)
                return true;

            return ResolvePreviewText(in entry).IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private bool TryGetCsvOverride(uint hash, out string value)
        {
            for (int i = 0; i < _csvOverrides.Count; i++)
            {
                if (_csvOverrides[i].Hash == hash)
                {
                    value = _csvOverrides[i].Text;
                    return true;
                }
            }

            value = string.Empty;
            return false;
        }

        private void UpsertCsvOverride(uint hash, string value)
        {
            for (int i = 0; i < _csvOverrides.Count; i++)
            {
                EditorBabelText existing = _csvOverrides[i];
                if (existing.Hash != hash)
                    continue;

                existing.Text = value;
                _csvOverrides[i] = existing;
                return;
            }

            _csvOverrides.Add(new EditorBabelText
            {
                Hash = hash,
                Text = value
            });
        }

        private static bool TryParseCsvLine(string line, out uint hash, out string value)
        {
            hash = 0u;
            value = string.Empty;
            int separator = line.IndexOf(',');
            if (separator <= 0)
                return false;

            string key = line.Substring(0, separator).Trim();
            if (string.Equals(key, "hash", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(key, "key", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (key.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                if (!uint.TryParse(key.Substring(2), System.Globalization.NumberStyles.HexNumber, null, out hash))
                    return false;
            }
            else if (!uint.TryParse(key, out hash))
            {
                hash = unchecked((uint)LocHash.Compute(key));
            }

            value = UnquoteCsvValue(line.Substring(separator + 1).Trim());
            return true;
        }

        private static string UnquoteCsvValue(string value)
        {
            if (value.Length >= 2 && value[0] == '"' && value[value.Length - 1] == '"')
                return value.Substring(1, value.Length - 2).Replace("\"\"", "\"");

            return value;
        }

        private static byte[] BuildDictionaryBytes(List<EditorBabelText> texts)
        {
            int indexOffset = HeaderSizeBytes;
            int dataOffset = AlignUp16(indexOffset + (texts.Count * EntrySizeBytes));
            List<byte[]> encodedTexts = new List<byte[]>(texts.Count);
            int cursor = dataOffset;
            for (int i = 0; i < texts.Count; i++)
            {
                cursor = AlignUp16(cursor);
                byte[] encoded = Encoding.UTF8.GetBytes(texts[i].Text ?? string.Empty);
                encodedTexts.Add(encoded);
                cursor += encoded.Length;
            }
            cursor = AlignUp16(cursor);

            byte[] output = new byte[cursor];
            WriteUInt32(output, 0, H8StaticDataFormat.BabelMagic);
            WriteUInt16(output, 4, H8StaticDataFormat.FormatVersion);
            WriteUInt16(output, 6, HeaderSizeBytes);
            WriteUInt32(output, 8, (uint)texts.Count);
            WriteUInt32(output, 12, (uint)indexOffset);
            WriteUInt32(output, 16, (uint)dataOffset);
            WriteUInt32(output, 20, (uint)output.Length);
            WriteUInt32(output, 28, H8StaticDataFormat.LittleEndianFlag);

            cursor = dataOffset;
            for (int i = 0; i < texts.Count; i++)
            {
                cursor = AlignUp16(cursor);
                byte[] encoded = encodedTexts[i];
                Buffer.BlockCopy(encoded, 0, output, cursor, encoded.Length);

                int entryOffset = indexOffset + (i * EntrySizeBytes);
                WriteUInt32(output, entryOffset, texts[i].Hash);
                WriteUInt32(output, entryOffset + 4, (uint)cursor);
                WriteUInt32(output, entryOffset + 8, (uint)encoded.Length);
                WriteUInt32(output, entryOffset + 12, texts[i].Flags);
                cursor += encoded.Length;
            }

            uint crc = ComputeCrc32(output, HeaderSizeBytes, output.Length - HeaderSizeBytes);
            WriteUInt32(output, 24, crc);
            return output;
        }

        private static int AlignUp16(int value)
        {
            return (value + 15) & ~15;
        }

        private static int CountTrailingZeroPadding(byte[] data)
        {
            if (data == null || data.Length == 0 || (data.Length & 15) != 0)
                return 0;

            int count = 0;
            int max = Mathf.Min(16, data.Length);
            for (int i = 0; i < max; i++)
            {
                if (data[data.Length - 1 - i] != 0)
                    break;

                count++;
            }

            return count;
        }

        private static uint ComputeCrc32(byte[] data, int offset, int length)
        {
            uint crc = 0xFFFFFFFFu;
            for (int i = 0; i < length; i++)
            {
                crc ^= data[offset + i];
                for (int bit = 0; bit < 8; bit++)
                {
                    uint mask = 0u - (crc & 1u);
                    crc = (crc >> 1) ^ (0xEDB88320u & mask);
                }
            }

            return ~crc;
        }

        private static ushort ReadUInt16(byte[] data, int offset)
        {
            return (ushort)(data[offset] | (data[offset + 1] << 8));
        }

        private static uint ReadUInt32(byte[] data, int offset)
        {
            return (uint)(data[offset] |
                (data[offset + 1] << 8) |
                (data[offset + 2] << 16) |
                (data[offset + 3] << 24));
        }

        private static void WriteUInt16(byte[] data, int offset, int value)
        {
            data[offset] = (byte)value;
            data[offset + 1] = (byte)(value >> 8);
        }

        private static void WriteUInt32(byte[] data, int offset, uint value)
        {
            data[offset] = (byte)value;
            data[offset + 1] = (byte)(value >> 8);
            data[offset + 2] = (byte)(value >> 16);
            data[offset + 3] = (byte)(value >> 24);
        }

        private struct EditorBabelEntry
        {
            public uint Hash;
            public uint Offset;
            public uint Length;
            public uint Flags;
        }

        private struct EditorBabelText
        {
            public uint Hash;
            public uint Flags;
            public string Text;
        }
    }
}
#endif
