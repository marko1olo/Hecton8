#if UNITY_EDITOR
using System;
using System.Globalization;
using Hecton8.Core;
using Hecton8.Core.Persistence.Paging;
using Hecton8.SaveSystem;
using UnityEditor;
using UnityEngine;

namespace Hecton8.Editor
{
    public sealed class WalXRayWindow : EditorWindow
    {
        private string _walPath;
        private string _lastError;
        private H8WalInspectionSnapshot _snapshot;

        [MenuItem("Hecton8/Diagnostics/WAL X-Ray")]
        private static void OpenWindow()
        {
            WalXRayWindow window = GetWindow<WalXRayWindow>("WAL X-Ray");
            window.minSize = new Vector2(620f, 320f);
            window.Show();
        }

        private void OnEnable()
        {
            _walPath = H8WalInspector.ResolveDefaultWalPath();
            Refresh();
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Write-Ahead Log", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                _walPath = EditorGUILayout.TextField(_walPath);
                if (GUILayout.Button("Refresh", GUILayout.Width(90f)))
                    Refresh();
            }

            if (!string.IsNullOrEmpty(_lastError))
                EditorGUILayout.HelpBox(_lastError, MessageType.Error);

            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("File Size", FormatBytes(_snapshot.FileBytes));
            EditorGUILayout.LabelField("Pending Transactions", _snapshot.PendingTransactions.ToString());
            EditorGUILayout.LabelField("Corrupt Records", _snapshot.CorruptRecords.ToString());
            EditorGUILayout.LabelField("Raw Payload", FormatBytes(_snapshot.RawPayloadBytes));
            EditorGUILayout.LabelField("Stored Payload", FormatBytes(_snapshot.StoredPayloadBytes));
            EditorGUILayout.LabelField("Hot State", FormatBytes(_snapshot.HotStateBytes));
            EditorGUILayout.LabelField("Last Sector", "0x" + _snapshot.LastSectorHash.ToString("X16"));
            EditorGUILayout.LabelField("Last Payload Type", "0x" + _snapshot.LastPayloadType.ToString("X8"));
            EditorGUILayout.LabelField("Last Frame", _snapshot.LastFrame.ToString());

            float commitRatio = H8WalInspector.CommitThresholdBytes <= 0L
                ? 0f
                : Mathf.Clamp01((float)_snapshot.FileBytes / H8WalInspector.CommitThresholdBytes);
            Rect commitRect = GUILayoutUtility.GetRect(18f, 18f);
            EditorGUI.ProgressBar(commitRect, commitRatio, "4 MB commit threshold " + (commitRatio * 100f).ToString("0.0", CultureInfo.InvariantCulture) + "%");

            float stallRatio = H8WalInspector.MicroStallThresholdBytes <= 0L
                ? 0f
                : Mathf.Clamp01((float)_snapshot.FileBytes / H8WalInspector.MicroStallThresholdBytes);
            Rect stallRect = GUILayoutUtility.GetRect(18f, 18f);
            EditorGUI.ProgressBar(stallRect, stallRatio, "16 MB micro-stall threshold " + (stallRatio * 100f).ToString("0.0", CultureInfo.InvariantCulture) + "%");

            DrawRleEfficiency(_snapshot.RawPayloadBytes, _snapshot.StoredPayloadBytes);

            EditorGUILayout.Space(8f);
            using (new EditorGUI.DisabledScope(!Application.isPlaying || _snapshot.FileBytes <= 0L))
            {
                if (GUILayout.Button("Corrupt Tail Bytes"))
                {
                    if (!H8WalInspector.TryCorruptTailBytes(_walPath, out _lastError))
                        _lastError = string.IsNullOrEmpty(_lastError) ? "Tail corruption failed." : _lastError;
                    Refresh();
                }
            }
        }

        private void Refresh()
        {
            _lastError = string.Empty;
            if (!H8WalInspector.TryInspect(_walPath, out _snapshot, out _lastError))
                _snapshot.WalPath = _walPath;
        }

        internal static void DrawRleEfficiency(long rawBytes, long storedBytes)
        {
            if (rawBytes <= 0L)
            {
                EditorGUILayout.LabelField("RLE Ratio", "No payload");
                return;
            }

            float savedRatio = Mathf.Clamp01(1f - ((float)storedBytes / rawBytes));
            Color previous = GUI.color;
            if (savedRatio < 0.2f)
                GUI.color = Color.red;

            Rect rect = GUILayoutUtility.GetRect(18f, 18f);
            EditorGUI.ProgressBar(rect, savedRatio, "Compression Ratio " + (savedRatio * 100f).ToString("0.0", CultureInfo.InvariantCulture) + "% saved");
            GUI.color = previous;
        }

        private static string FormatBytes(long bytes)
        {
            if (bytes >= 1024L * 1024L)
                return (bytes / (1024f * 1024f)).ToString("0.00", CultureInfo.InvariantCulture) + " MB";
            if (bytes >= 1024L)
                return (bytes / 1024f).ToString("0.0", CultureInfo.InvariantCulture) + " KB";
            return bytes + " B";
        }
    }

    public sealed class StateDeltaXRayWindow : EditorWindow
    {
        private string _walPath;
        private string _backupPath;
        private string _lastError;
        private SaveMerkleEditorSnapshot _snapshot;
        private int _snapshotVersion;

        [MenuItem("Hecton8/Diagnostics/State Delta X-Ray")]
        private static void OpenWindow()
        {
            StateDeltaXRayWindow window = GetWindow<StateDeltaXRayWindow>("State Delta X-Ray");
            window.minSize = new Vector2(720f, 420f);
            window.Show();
        }

        private void OnEnable()
        {
            _walPath = HectonPersistentPathPolicy.CombineFile(SaveStateMerkleTree.DefaultMerkleWalFileName);
            _backupPath = _walPath + ".bak";
            Refresh();
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("State Delta X-Ray", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                _walPath = EditorGUILayout.TextField(_walPath);
                if (GUILayout.Button("Refresh", GUILayout.Width(90f)))
                    Refresh();
            }

            if (!string.IsNullOrEmpty(_lastError))
                EditorGUILayout.HelpBox(_lastError, MessageType.Warning);

            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("Snapshot Version", _snapshotVersion.ToString());
            EditorGUILayout.LabelField("Root Hash", "0x" + _snapshot.RootHashHi.ToString("X16") + _snapshot.RootHashLo.ToString("X16"));
            EditorGUILayout.LabelField("Changed Leaves", _snapshot.ChangedLeafCount + " / " + SaveStateMerkleTree.LeafCount);
            EditorGUILayout.LabelField("Last Changed Sector", "0x" + _snapshot.LastChangedSectorKey.ToString("X8"));
            EditorGUILayout.LabelField("Raw Delta Bytes", FormatBytes(_snapshot.RawBytes));
            EditorGUILayout.LabelField("Stored Delta Bytes", FormatBytes(_snapshot.StoredBytes));
            EditorGUILayout.LabelField("Corrupt LZ4 Blocks", _snapshot.CorruptBlockCount.ToString());

            DrawMerkleGrid(_snapshot);

            EditorGUILayout.Space(8f);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Validate WAL"))
                {
                    if (!SaveStateMerkleTree.TryValidateWalAndRollback(_walPath, _backupPath, out _lastError) &&
                        string.IsNullOrEmpty(_lastError))
                    {
                        _lastError = "Merkle WAL validation failed.";
                    }
                }

                using (new EditorGUI.DisabledScope(!Application.isPlaying && !FileExists(_walPath)))
                {
                    if (GUILayout.Button("Corrupt Sector") &&
                        !H8WalInspector.TryCorruptSectorBytes(_walPath, out _lastError))
                    {
                        _lastError = string.IsNullOrEmpty(_lastError) ? "Sector corruption failed." : _lastError;
                    }
                }
            }
        }

        private void Refresh()
        {
            _lastError = string.Empty;
            if (!SaveStateMerkleTree.TryReadLastEditorSnapshot(out _snapshot, out _snapshotVersion))
                _lastError = "No published Merkle NativeArray snapshot yet.";
        }

        private static void DrawMerkleGrid(SaveMerkleEditorSnapshot snapshot)
        {
            const int columns = 16;
            const float cellSize = 18f;
            Rect grid = GUILayoutUtility.GetRect(columns * cellSize, columns * cellSize);
            uint changed = snapshot.ChangedLeafCount;
            uint lastSector = snapshot.LastChangedSectorKey;
            for (int y = 0; y < columns; y++)
            {
                for (int x = 0; x < columns; x++)
                {
                    int index = (y * columns) + x;
                    Rect cell = new Rect(grid.x + (x * cellSize), grid.y + (y * cellSize), cellSize - 2f, cellSize - 2f);
                    bool hot = IsChangedBranch(snapshot, index) || ((uint)index == (lastSector & 0xFFu) && changed > 0u);
                    EditorGUI.DrawRect(cell, hot ? new Color(1f, 0.05f, 0.02f, 1f) : new Color(0.12f, 0.14f, 0.16f, 1f));
                }
            }
        }

        private static bool IsChangedBranch(SaveMerkleEditorSnapshot snapshot, int branchIndex)
        {
            if ((uint)branchIndex >= 256u)
                return false;

            int lane = branchIndex >> 6;
            ulong bit = 1UL << (branchIndex & 63);
            if (lane == 0)
                return (snapshot.ChangedBranchBits0 & bit) != 0UL;
            if (lane == 1)
                return (snapshot.ChangedBranchBits1 & bit) != 0UL;
            if (lane == 2)
                return (snapshot.ChangedBranchBits2 & bit) != 0UL;

            return (snapshot.ChangedBranchBits3 & bit) != 0UL;
        }

        private static bool FileExists(string path)
        {
            return !string.IsNullOrEmpty(path) && System.IO.File.Exists(path);
        }

        private static string FormatBytes(uint bytes)
        {
            if (bytes >= 1024U * 1024U)
                return (bytes / (1024f * 1024f)).ToString("0.00", CultureInfo.InvariantCulture) + " MB";
            if (bytes >= 1024U)
                return (bytes / 1024f).ToString("0.0", CultureInfo.InvariantCulture) + " KB";
            return bytes + " B";
        }
    }

    [CustomEditor(typeof(SaveManager))]
    public sealed class SaveManagerWalInspector : UnityEditor.Editor
    {
        private H8WalInspectionSnapshot _snapshot;
        private string _lastError;

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("WAL / RLE", EditorStyles.boldLabel);

            string walPath = H8WalInspector.ResolveDefaultWalPath();
            if (!H8WalInspector.TryInspect(walPath, out _snapshot, out _lastError))
                EditorGUILayout.HelpBox(_lastError, MessageType.Error);

            EditorGUILayout.LabelField("Pending Transactions", _snapshot.PendingTransactions.ToString());
            EditorGUILayout.LabelField("WAL Size", WalXRayWindowFormatBytes(_snapshot.FileBytes));
            EditorGUILayout.LabelField("Hot State", WalXRayWindowFormatBytes(_snapshot.HotStateBytes));
            WalXRayWindow.DrawRleEfficiency(_snapshot.RawPayloadBytes, _snapshot.StoredPayloadBytes);

            using (new EditorGUI.DisabledScope(!Application.isPlaying || _snapshot.FileBytes <= 0L))
            {
                if (GUILayout.Button("Corrupt Tail Bytes") &&
                    !H8WalInspector.TryCorruptTailBytes(walPath, out _lastError))
                {
                    EditorGUILayout.HelpBox(_lastError, MessageType.Error);
                }
            }
        }

        private static string WalXRayWindowFormatBytes(long bytes)
        {
            if (bytes >= 1024L * 1024L)
                return (bytes / (1024f * 1024f)).ToString("0.00", CultureInfo.InvariantCulture) + " MB";
            if (bytes >= 1024L)
                return (bytes / 1024f).ToString("0.0", CultureInfo.InvariantCulture) + " KB";
            return bytes + " B";
        }
    }
}
#endif
