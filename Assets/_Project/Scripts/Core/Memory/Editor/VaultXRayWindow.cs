#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Hecton8.Core.Memory.Editor
{
    /// <summary>
    /// Editor-only block-map view for GlobalDataVault fragmentation.
    /// </summary>
    public sealed class VaultXRayWindow : EditorWindow
    {
        private const double RefreshIntervalSeconds = 0.5d;
        private const float HeaderHeight = 22f;
        private const float RowHeight = 14f;
        private const float Padding = 6f;
        private static readonly Color _freeColor = new Color(0.45f, 0.08f, 0.08f, 1f);
        private static readonly Color _activeColor = new Color(0.05f, 0.45f, 0.16f, 1f);
        private static readonly Color _lockedColor = new Color(0.76f, 0.57f, 0.06f, 1f);
        private static readonly Color _backgroundColor = new Color(0.08f, 0.08f, 0.08f, 1f);

        private readonly List<VaultMemoryBlockSnapshot> _blocks = new List<VaultMemoryBlockSnapshot>(256);
        private double _nextRefreshTime;
        private long _totalBytes;
        private string _overrideStatus = "CSV idle.";

        /// <summary>Opens the Vault X-Ray window.</summary>
        [MenuItem("Hecton8/Core/Vault X-Ray")]
        public static void Open()
        {
            VaultXRayWindow window = GetWindow<VaultXRayWindow>("Vault X-Ray");
            window.minSize = new Vector2(420f, 240f);
            window.RefreshSnapshot();
        }

        private void OnEnable()
        {
            EditorApplication.update -= OnEditorUpdate;
            EditorApplication.update += OnEditorUpdate;
            RefreshSnapshot();
        }

        private void OnDisable()
        {
            EditorApplication.update -= OnEditorUpdate;
        }

        private void OnEditorUpdate()
        {
            double now = EditorApplication.timeSinceStartup;
            if (now < _nextRefreshTime)
                return;

            _nextRefreshTime = now + RefreshIntervalSeconds;
            RefreshSnapshot();
            Repaint();
        }

        private void OnGUI()
        {
            Rect full = new Rect(0f, 0f, position.width, position.height);
            EditorGUI.DrawRect(full, _backgroundColor);

            Rect header = new Rect(Padding, Padding, position.width - (Padding * 2f) - 116f, HeaderHeight);
            EditorGUI.LabelField(header, "GlobalDataVault Blocks: " + _blocks.Count);
            Rect reloadButton = new Rect(position.width - Padding - 110f, Padding, 110f, HeaderHeight);
            if (GUI.Button(reloadButton, "Reload CSV"))
                ReloadCsvOverride();

            Rect status = new Rect(Padding, Padding + HeaderHeight, position.width - (Padding * 2f), HeaderHeight);
            EditorGUI.LabelField(status, _overrideStatus);

            if (_blocks.Count == 0 || _totalBytes <= 0L)
            {
                EditorGUI.LabelField(new Rect(Padding, (HeaderHeight * 2f) + Padding, position.width - Padding * 2f, RowHeight), "No active vault snapshot.");
                return;
            }

            float y = (HeaderHeight * 2f) + Padding * 2f;
            float width = Mathf.Max(1f, position.width - Padding * 2f);
            for (int i = 0; i < _blocks.Count; i++)
            {
                VaultMemoryBlockSnapshot block = _blocks[i];
                float fraction = Mathf.Clamp01((float)((double)block.Bytes / _totalBytes));
                float blockWidth = Mathf.Max(1f, width * fraction);
                Rect rect = new Rect(Padding, y, blockWidth, RowHeight);
                Color color = block.State == GlobalDataVault.BlockStateOccupied ? _activeColor : _freeColor;
                if (block.LockCount != 0)
                    color = _lockedColor;

                EditorGUI.DrawRect(rect, color);
                y += RowHeight + 2f;
                if (y > position.height - RowHeight)
                    break;
            }
        }

        private void RefreshSnapshot()
        {
            _blocks.Clear();
            _totalBytes = 0L;
            if (!GlobalDataVault.TryGetLatestCreated(out GlobalDataVault vault))
                return;

            int count = vault.MemoryBlockSnapshotCount;
            for (int i = 0; i < count; i++)
            {
                if (!vault.TryGetMemoryBlockSnapshot(i, out VaultMemoryBlockSnapshot block))
                    continue;

                _blocks.Add(block);
                if (block.Bytes > 0L)
                    _totalBytes += block.Bytes;
            }
        }

        private void ReloadCsvOverride()
        {
            if (!GlobalDataVault.TryGetLatestCreated(out GlobalDataVault vault))
            {
                _overrideStatus = "No active vault.";
                return;
            }

            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            if (string.IsNullOrEmpty(projectRoot))
            {
                _overrideStatus = "Project root unavailable.";
                return;
            }

            string path = Path.Combine(projectRoot, "memory_overrides.csv");
            _overrideStatus = VaultLegacyBinaryArchaeology.TryApplyMemoryOverridesCsv(vault, path)
                ? "CSV applied: memory_overrides.csv"
                : "CSV missing or rejected.";
            RefreshSnapshot();
        }
    }
}
#endif
