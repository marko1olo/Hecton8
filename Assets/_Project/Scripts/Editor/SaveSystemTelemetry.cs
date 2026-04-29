#if UNITY_EDITOR
namespace Hecton8.Editor
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using Hecton8.SaveSystem;
    using UnityEditor;
    using UnityEngine;

    public sealed class SaveSystemTelemetry : EditorWindow
    {
        private const float HeatCellSize = 18f;
        private const float HeatCellPadding = 2f;

        private readonly List<SaveBinaryStorage.IndexedSectorEntryInfo> _sectorEntries = new List<SaveBinaryStorage.IndexedSectorEntryInfo>(1024);
        private readonly List<SectorTelemetryRow> _sortedRows = new List<SectorTelemetryRow>(1024);
        private Vector2 _scroll;
        private string _savePath = string.Empty;
        private string _lastError = string.Empty;
        private string _fragmentationAscii = string.Empty;
        private long _totalFileBytes;
        private long _totalCompressedBytes;
        private long _usedFileBytes;
        private long _fragmentationBytes;
        private long _largestGapBytes;
        private int _chunkSizeMeters;

        private struct SectorTelemetryRow
        {
            public Vector2Int SectorCoord;
            public long SectorHash;
            public long ByteOffset;
            public int CompressedSize;
            public int DecompressedSize;
            public uint Checksum;
            public long GapBefore;
        }

        [MenuItem("Hecton8/Diagnostics/Save System Telemetry")]
        private static void OpenWindow()
        {
            SaveSystemTelemetry window = GetWindow<SaveSystemTelemetry>("Save Telemetry");
            window.minSize = new Vector2(860f, 540f);
            window.Show();
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Indexed Save Telemetry", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.SelectableLabel(_savePath, EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
                if (GUILayout.Button("Open .sav", GUILayout.Width(100f)))
                    SelectSaveFile();
                if (GUILayout.Button("Refresh", GUILayout.Width(100f)))
                    RefreshTelemetry();
            }

            if (!string.IsNullOrEmpty(_lastError))
                EditorGUILayout.HelpBox(_lastError, MessageType.Error);

            if (_sortedRows.Count <= 0)
                return;

            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField($"Chunk Size: {_chunkSizeMeters} m");
            EditorGUILayout.LabelField($"Sector Blocks: {_sortedRows.Count}");
            EditorGUILayout.LabelField($"Total File Size: {FormatBytes(_totalFileBytes)}");
            EditorGUILayout.LabelField($"Used Space: {FormatBytes(_usedFileBytes)}");
            EditorGUILayout.LabelField($"Fragmentation: {FormatBytes(_fragmentationBytes)}");
            EditorGUILayout.LabelField($"Compressed Payload: {FormatBytes(_totalCompressedBytes)}");
            EditorGUILayout.LabelField($"Largest Gap: {FormatBytes(_largestGapBytes)}");
            if (_totalFileBytes > 0L)
            {
                float usedRatio = Mathf.Clamp01((float)_usedFileBytes / _totalFileBytes);
                Rect usageRect = GUILayoutUtility.GetRect(18f, 18f);
                EditorGUI.ProgressBar(usageRect, usedRatio, $"Used {usedRatio * 100f:0.0}%");
            }
            if (!string.IsNullOrEmpty(_fragmentationAscii))
                EditorGUILayout.SelectableLabel(_fragmentationAscii, EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));

            EditorGUILayout.Space(6f);
            DrawHeatMap();

            EditorGUILayout.Space(6f);
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            DrawRowsTable();
            EditorGUILayout.EndScrollView();
        }

        private void SelectSaveFile()
        {
            string candidate = EditorUtility.OpenFilePanel("Select Hecton-8 save", Application.persistentDataPath, "sav");
            if (string.IsNullOrWhiteSpace(candidate))
                return;

            _savePath = candidate;
            RefreshTelemetry();
        }

        private void RefreshTelemetry()
        {
            _lastError = string.Empty;
            _sectorEntries.Clear();
            _sortedRows.Clear();
            _totalFileBytes = 0L;
            _totalCompressedBytes = 0L;
            _usedFileBytes = 0L;
            _fragmentationBytes = 0L;
            _largestGapBytes = 0L;
            _fragmentationAscii = string.Empty;

            if (string.IsNullOrWhiteSpace(_savePath) || !File.Exists(_savePath))
            {
                _lastError = "Save path is empty or missing.";
                return;
            }

            _totalFileBytes = new FileInfo(_savePath).Length;

            if (!SaveBinaryStorage.TryReadIndexedPersistentWorldDirectory(_savePath, _sectorEntries, out _chunkSizeMeters, out string error))
            {
                _lastError = error;
                return;
            }

            for (int i = 0; i < _sectorEntries.Count; i++)
            {
                SaveBinaryStorage.IndexedSectorEntryInfo entry = _sectorEntries[i];
                _sortedRows.Add(new SectorTelemetryRow
                {
                    SectorCoord = UnpackSectorHash(entry.SectorHash),
                    SectorHash = entry.SectorHash,
                    ByteOffset = entry.ByteOffset,
                    CompressedSize = entry.CompressedSize,
                    DecompressedSize = entry.DecompressedSize,
                    Checksum = entry.Checksum,
                    GapBefore = 0L
                });
            }

            _sortedRows.Sort(static (left, right) => left.ByteOffset.CompareTo(right.ByteOffset));
            long expectedOffset = long.MinValue;
            for (int i = 0; i < _sortedRows.Count; i++)
            {
                SectorTelemetryRow row = _sortedRows[i];
                long gapBefore = expectedOffset == long.MinValue ? 0L : Math.Max(0L, row.ByteOffset - expectedOffset);
                row.GapBefore = gapBefore;
                _sortedRows[i] = row;
                _largestGapBytes = Math.Max(_largestGapBytes, gapBefore);
                _totalCompressedBytes += row.CompressedSize;
                expectedOffset = row.ByteOffset + row.CompressedSize;
            }

            _usedFileBytes = expectedOffset == long.MinValue ? 0L : expectedOffset;
            _fragmentationBytes = Math.Max(0L, _totalFileBytes - _usedFileBytes);

            _fragmentationAscii = BuildFragmentationAscii(_sortedRows);
            Debug.Log($"[SaveSystemTelemetry] Fragmentation {_fragmentationAscii}");
            _sortedRows.Sort(static (left, right) => right.CompressedSize.CompareTo(left.CompressedSize));
        }

        private void DrawHeatMap()
        {
            int minX = int.MaxValue;
            int maxX = int.MinValue;
            int minY = int.MaxValue;
            int maxY = int.MinValue;
            int maxCompressed = 1;

            for (int i = 0; i < _sortedRows.Count; i++)
            {
                SectorTelemetryRow row = _sortedRows[i];
                minX = Math.Min(minX, row.SectorCoord.x);
                maxX = Math.Max(maxX, row.SectorCoord.x);
                minY = Math.Min(minY, row.SectorCoord.y);
                maxY = Math.Max(maxY, row.SectorCoord.y);
                maxCompressed = Math.Max(maxCompressed, row.CompressedSize);
            }

            int width = Math.Max(1, maxX - minX + 1);
            int height = Math.Max(1, maxY - minY + 1);
            Rect rect = GUILayoutUtility.GetRect(
                width * (HeatCellSize + HeatCellPadding),
                height * (HeatCellSize + HeatCellPadding));

            EditorGUI.DrawRect(rect, new Color(0.08f, 0.08f, 0.08f, 1f));
            for (int i = 0; i < _sortedRows.Count; i++)
            {
                SectorTelemetryRow row = _sortedRows[i];
                float intensity = Mathf.Clamp01((float)row.CompressedSize / maxCompressed);
                Color cellColor = Color.Lerp(new Color(0.08f, 0.32f, 0.58f, 1f), new Color(0.88f, 0.2f, 0.16f, 1f), intensity);
                float x = rect.x + (row.SectorCoord.x - minX) * (HeatCellSize + HeatCellPadding);
                float y = rect.y + (maxY - row.SectorCoord.y) * (HeatCellSize + HeatCellPadding);
                Rect cellRect = new Rect(x, y, HeatCellSize, HeatCellSize);
                EditorGUI.DrawRect(cellRect, cellColor);
                if (Event.current.type == EventType.Repaint)
                    GUI.Label(cellRect, row.SectorCoord.x.ToString(), EditorStyles.centeredGreyMiniLabel);
            }
        }

        private void DrawRowsTable()
        {
            EditorGUILayout.LabelField("Largest Blocks", EditorStyles.boldLabel);
            for (int i = 0; i < _sortedRows.Count; i++)
            {
                SectorTelemetryRow row = _sortedRows[i];
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"[{row.SectorCoord.x}, {row.SectorCoord.y}]", GUILayout.Width(90f));
                EditorGUILayout.LabelField($"Offset {row.ByteOffset}", GUILayout.Width(130f));
                EditorGUILayout.LabelField($"Compressed {FormatBytes(row.CompressedSize)}", GUILayout.Width(140f));
                EditorGUILayout.LabelField($"Raw {FormatBytes(row.DecompressedSize)}", GUILayout.Width(120f));
                EditorGUILayout.LabelField($"Gap {FormatBytes(row.GapBefore)}", GUILayout.Width(110f));
                EditorGUILayout.LabelField($"Hash 0x{row.SectorHash:X16}", GUILayout.Width(170f));
                EditorGUILayout.EndHorizontal();
            }
        }

        private static Vector2Int UnpackSectorHash(long sectorHash)
        {
            return new Vector2Int((int)(sectorHash >> 32), (int)(uint)sectorHash);
        }

        private static string FormatBytes(long bytes)
        {
            if (bytes >= 1024L * 1024L)
                return $"{bytes / (1024f * 1024f):0.00} MB";
            if (bytes >= 1024L)
                return $"{bytes / 1024f:0.0} KB";
            return $"{bytes} B";
        }

        private static string BuildFragmentationAscii(List<SectorTelemetryRow> rows)
        {
            if (rows == null || rows.Count <= 0)
                return string.Empty;

            const int SlotCount = 48;
            char[] buffer = new char[SlotCount + 2];
            buffer[0] = '[';
            buffer[buffer.Length - 1] = ']';
            for (int i = 1; i < buffer.Length - 1; i++)
                buffer[i] = '.';

            long maxEnd = 1L;
            for (int i = 0; i < rows.Count; i++)
                maxEnd = Math.Max(maxEnd, rows[i].ByteOffset + rows[i].CompressedSize);

            for (int i = 0; i < rows.Count; i++)
            {
                SectorTelemetryRow row = rows[i];
                int start = Mathf.Clamp((int)((row.ByteOffset * SlotCount) / (double)maxEnd), 0, SlotCount - 1);
                int endExclusive = Mathf.Clamp((int)Math.Ceiling(((row.ByteOffset + row.CompressedSize) * SlotCount) / (double)maxEnd), start + 1, SlotCount);
                for (int slot = start; slot < endExclusive; slot++)
                    buffer[slot + 1] = 'X';
            }

            return new string(buffer);
        }
    }
}
#endif
