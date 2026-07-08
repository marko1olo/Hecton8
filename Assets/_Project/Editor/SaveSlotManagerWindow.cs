using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Hecton8.SaveSystem;

namespace Hecton8.Editor
{
    /// <summary>
    /// Professional Editor Window for managing Hecton8 save slots.
    /// Allows developers to inspect metadata, view thumbnails, and test redundancy.
    /// </summary>
    public class SaveSlotManagerWindow : EditorWindow
    {
        private const int MaxCachedThumbnails = 12;

        private List<SaveSlotInfo> _slots = new List<SaveSlotInfo>(16);
        private List<SaveSlotAuditResult> _auditResults = new List<SaveSlotAuditResult>(16);
        private List<SaveSlotRepairResult> _repairResults = new List<SaveSlotRepairResult>(16);
        private Dictionary<string, SaveSlotMaintenanceRecord> _maintenanceRecords = new Dictionary<string, SaveSlotMaintenanceRecord>(32, StringComparer.OrdinalIgnoreCase);
        private Vector2 _scrollPos;
        private bool _autoRefresh = true;
        private string _lastAuditSummary = string.Empty;
        private string _lastRepairSummary = string.Empty;
        private Dictionary<string, Texture2D> _thumbnailCache = new Dictionary<string, Texture2D>(MaxCachedThumbnails, StringComparer.OrdinalIgnoreCase);
        private List<string> _thumbnailCacheOrder = new List<string>(MaxCachedThumbnails);
        private readonly string[] _artifactPathScratch = new string[SaveManager.MaxKnownArtifactPathCount];

        [MenuItem("Tools/Hecton/Save Slot Manager", false, 1)]
        public static void ShowWindow()
        {
            GetWindow<SaveSlotManagerWindow>("Save Manager");
        }

        private void OnEnable()
        {
            RefreshSlots();
        }

        private void OnDisable()
        {
            ClearThumbnailCache();
            SaveThumbnailSystem.ClearCache();
        }

        private void OnFocus()
        {
            if (_autoRefresh) RefreshSlots();
        }

        private void RefreshSlots()
        {
            SaveManager.CollectAvailableSaveSlotInfos(_slots);
            _maintenanceRecords.Clear();
            for (int i = 0; i < _slots.Count; i++)
            {
                SaveSlotMaintenanceRecord record = SaveSlotMaintenanceRecord.Load(_slots[i].slotName);
                if (record != null)
                    _maintenanceRecords[_slots[i].slotName] = record;
            }
        }

        private void OnGUI()
        {
            DrawHeader();
            
            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);
            
            if (_slots.Count == 0)
            {
                EditorGUILayout.HelpBox("No save slots found in PersistentDataPath.", MessageType.Info);
            }
            else
            {
                foreach (var slot in _slots)
                {
                    DrawSlot(slot);
                }
            }
            
            EditorGUILayout.EndScrollView();
            
            DrawFooter();
        }

        private void DrawHeader()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label("Available Save Slots", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Audit All", EditorStyles.toolbarButton))
            {
                AuditAllSlots();
            }
            if (GUILayout.Button("Repair All", EditorStyles.toolbarButton))
            {
                RepairAllSlots();
            }
            _autoRefresh = GUILayout.Toggle(_autoRefresh, "Auto-Refresh", EditorStyles.toolbarButton);
            if (GUILayout.Button("Refresh Now", EditorStyles.toolbarButton)) RefreshSlots();
            EditorGUILayout.EndHorizontal();

            if (!string.IsNullOrEmpty(_lastAuditSummary))
            {
                EditorGUILayout.HelpBox(_lastAuditSummary, MessageType.None);
            }

            if (!string.IsNullOrEmpty(_lastRepairSummary))
            {
                EditorGUILayout.HelpBox(_lastRepairSummary, MessageType.Info);
            }
        }

        private void DrawSlot(SaveSlotInfo slot)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            EditorGUILayout.BeginHorizontal();
            
            // Thumbnail
            Texture2D thumb = LoadThumbnail(slot.slotName);
            if (thumb != null)
            {
                GUILayout.Label(thumb, GUILayout.Width(120), GUILayout.Height(67));
            }
            else
            {
                GUILayout.Box("NO THUMB", GUILayout.Width(120), GUILayout.Height(67));
            }

            // Info
            EditorGUILayout.BeginVertical();
            EditorGUILayout.LabelField($"Slot: {slot.slotName}", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"Status: {slot.GetStatusLabel()}");
            EditorGUILayout.LabelField($"Scene: {slot.metadata.sceneName}");
            EditorGUILayout.LabelField($"Date: {slot.metadata.timestamp}");
            EditorGUILayout.LabelField($"Playtime: {FormatTime(slot.metadata.totalPlayTime)}");
            EditorGUILayout.LabelField($"Version: {slot.metadata.version}");
            EditorGUILayout.LabelField($"Primary Save: {(slot.HasPrimarySave ? "yes" : "no")}");
            EditorGUILayout.LabelField($"Backup Save: {(slot.HasBackupSave ? "yes" : "no")}");
            EditorGUILayout.LabelField($"Metadata: {(slot.HasPrimaryMetadata ? "primary" : slot.HasBackupMetadata ? "backup" : "generated")}");

            SaveSlotAuditResult audit = FindAuditResult(slot.slotName);
            if (audit != null)
            {
                EditorGUILayout.Space(4f);
                EditorGUILayout.LabelField($"Audit: {audit.message}", EditorStyles.wordWrappedMiniLabel);
                string auditSource = audit.SelectedBackupSource
                    ? $"backup g{Mathf.Max(1, audit.SelectedBackupGeneration)}"
                    : "primary";
                EditorGUILayout.LabelField(
                    $"Audit Source: {auditSource} | readable={(audit.SlotReadable ? "yes" : "no")} | migration={(audit.RequiresMigration ? "yes" : "no")}",
                    EditorStyles.wordWrappedMiniLabel);
            }

            SaveSlotMaintenanceRecord maintenance = FindMaintenanceRecord(slot.slotName);
            if (maintenance != null)
            {
                EditorGUILayout.Space(4f);
                EditorGUILayout.LabelField($"Last Save: {FormatTimestamp(maintenance.LastSuccessfulSaveTicksUtc)}", EditorStyles.miniLabel);
                EditorGUILayout.LabelField($"Last Load: {FormatTimestamp(maintenance.LastSuccessfulLoadTicksUtc)}", EditorStyles.miniLabel);
                EditorGUILayout.LabelField($"Last Audit: {FormatTimestamp(maintenance.LastAuditTicksUtc)}", EditorStyles.miniLabel);
                EditorGUILayout.LabelField($"Last Repair: {FormatTimestamp(maintenance.LastRepairTicksUtc)}", EditorStyles.miniLabel);
                EditorGUILayout.LabelField($"Counts: save={maintenance.SuccessfulSaveCount}, load={maintenance.SuccessfulLoadCount}, audit={maintenance.AuditCount}, repair={maintenance.RepairCount}, fail={maintenance.FailureCount}", EditorStyles.wordWrappedMiniLabel);
                string lastLoadSource = maintenance.LastLoadUsedBackup
                    ? $"backup g{Mathf.Max(1, maintenance.LastLoadBackupGeneration)}"
                    : "primary";
                EditorGUILayout.LabelField(
                    $"Last Load Source: {lastLoadSource} | legacy={(maintenance.LastLoadUsedLegacyCompression ? "yes" : "no")} | self-repair={(maintenance.LastLoadSelfRepaired ? "yes" : "no")}",
                    EditorStyles.wordWrappedMiniLabel);

                if (!string.IsNullOrEmpty(maintenance.LastFailureMessage))
                    EditorGUILayout.LabelField($"Last Failure: {maintenance.LastFailureContext} | {maintenance.LastFailureMessage}", EditorStyles.wordWrappedMiniLabel);
            }
            EditorGUILayout.EndVertical();

            // Actions
            EditorGUILayout.BeginVertical(GUILayout.Width(100));
            if (GUILayout.Button("Audit", GUILayout.Height(24)))
            {
                AuditSingleSlot(slot.slotName);
            }

            if (GUILayout.Button("Repair", GUILayout.Height(24)))
            {
                RepairSingleSlot(slot.slotName);
            }

            if (GUILayout.Button("Delete", GUILayout.Height(30)))
            {
                if (EditorUtility.DisplayDialog("Delete Save", $"Are you sure you want to delete '{slot.slotName}'?", "Yes", "Cancel"))
                {
                    if (Hecton8.Core.GlobalRegistry.SaveRuntime != null)
                    {
                        Hecton8.Core.GlobalRegistry.SaveRuntime.DeleteSave(slot.slotName);
                    }
                    else
                    {
                        DeleteSlotFiles(slot.slotName);
                    }

                    RefreshSlots();
                }
            }
            
            if (GUILayout.Button("Corrupt (Test)"))
            {
                SimulateCorruption(slot.slotName);
            }
            EditorGUILayout.EndVertical();

            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.EndVertical();
            GUILayout.Space(5);
        }

        private void DrawFooter()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
            if (GUILayout.Button("Open Save Folder"))
            {
                EditorUtility.RevealInFinder(Application.persistentDataPath);
            }
            GUILayout.FlexibleSpace();
            GUILayout.Label($"Last updated: {DateTime.Now:HH:mm:ss}");
            EditorGUILayout.EndHorizontal();
        }

        private void AuditSingleSlot(string slotName)
        {
            if (SaveManager.TryAuditSaveSlotArtifacts(slotName, out SaveSlotAuditResult result))
            {
                string source = result.SelectedBackupSource
                    ? $"backup g{Mathf.Max(1, result.SelectedBackupGeneration)}"
                    : "primary";
                StoreAuditResult(result);
                _lastAuditSummary =
                    $"{result.slotName}: {result.message} " +
                    $"[integrity={result.IntegrityState}, source={source}]";
            }
            else
            {
                _lastAuditSummary = $"{slotName}: audit failed.";
            }

            Repaint();
        }

        private void RepairSingleSlot(string slotName)
        {
            if (SaveManager.TryRepairSaveSlotArtifacts(slotName, out SaveSlotRepairResult result))
            {
                string source = result.UsedBackupSource
                    ? $"backup g{Mathf.Max(1, result.SourceBackupGeneration)}"
                    : "primary";
                _lastRepairSummary =
                    $"{result.slotName}: {result.message} " +
                    $"[{result.IntegrityBefore} -> {result.IntegrityAfter}, source={source}]";
            }
            else
            {
                _lastRepairSummary = $"{slotName}: repair failed.";
            }

            RefreshSlots();
            Repaint();
        }

        private void AuditAllSlots()
        {
            SaveManager.CollectAuditResults(_auditResults);

            int readable = 0;
            int recommendedRepair = 0;
            int migrationNeeded = 0;

            for (int i = 0; i < _auditResults.Count; i++)
            {
                if (_auditResults[i].SlotReadable)
                    readable++;

                if (_auditResults[i].RecommendedRepair)
                    recommendedRepair++;

                if (_auditResults[i].RequiresMigration)
                    migrationNeeded++;
            }

            _lastAuditSummary =
                $"Audit pass complete. Slots: {_auditResults.Count}, " +
                $"readable: {readable}, repair recommended: {recommendedRepair}, " +
                $"migration needed: {migrationNeeded}.";

            Repaint();
        }

        private void RepairAllSlots()
        {
            SaveManager.CollectRepairResults(_repairResults);
            int changed = 0;
            int success = 0;

            for (int i = 0; i < _repairResults.Count; i++)
            {
                if (_repairResults[i].Success)
                    success++;

                if (_repairResults[i].ChangedAnything)
                    changed++;
            }

            _lastRepairSummary =
                $"Repair pass complete. Slots processed: {_repairResults.Count}, " +
                $"successful: {success}, changed: {changed}.";

            RefreshSlots();
            Repaint();
        }

        private SaveSlotAuditResult FindAuditResult(string slotName)
        {
            for (int i = 0; i < _auditResults.Count; i++)
            {
                if (string.Equals(_auditResults[i].slotName, slotName, StringComparison.OrdinalIgnoreCase))
                    return _auditResults[i];
            }

            return null;
        }

        private SaveSlotMaintenanceRecord FindMaintenanceRecord(string slotName)
        {
            if (string.IsNullOrEmpty(slotName))
                return null;

            _maintenanceRecords.TryGetValue(slotName, out SaveSlotMaintenanceRecord record);
            return record;
        }

        private void StoreAuditResult(SaveSlotAuditResult result)
        {
            if (result == null)
                return;

            for (int i = 0; i < _auditResults.Count; i++)
            {
                if (string.Equals(_auditResults[i].slotName, result.slotName, StringComparison.OrdinalIgnoreCase))
                {
                    _auditResults[i] = result;
                    return;
                }
            }

            _auditResults.Add(result);
        }

        private void ClearThumbnailCache()
        {
            foreach (var kvp in _thumbnailCache)
            {
                if (kvp.Value != null)
                {
                    DestroyImmediate(kvp.Value);
                }
            }
            _thumbnailCache.Clear();
            _thumbnailCacheOrder.Clear();
        }

        private Texture2D LoadThumbnail(string slotName)
        {
            if (_thumbnailCache.TryGetValue(slotName, out Texture2D cached) && cached != null)
            {
                MarkThumbnailAsMostRecent(slotName);
                return cached;
            }

            string path = SaveThumbnailSystem.GetThumbnailPath(slotName);
            if (!File.Exists(path)) return null;

            byte[] bytes = File.ReadAllBytes(path);
            Texture2D tex = new Texture2D(2, 2);
            tex.LoadImage(bytes);
            
            AddThumbnailToCache(slotName, tex);
            return tex;
        }

        private void SimulateCorruption(string slotName)
        {
            string path = Path.Combine(Application.persistentDataPath, SaveManager.GetPrimarySaveFilePath(slotName));
            if (File.Exists(path))
            {
                File.WriteAllText(path, "CORRUPTED_DATA_TEST_REDUNDANCY");
                Debug.LogWarning($"[SaveManagerTest] Simulated corruption on {slotName}. Primary file is now invalid.");
            }
        }

        private void DeleteSlotFiles(string slotName)
        {
            int relativePathCount = SaveManager.CollectAllKnownArtifactPaths(slotName, _artifactPathScratch);

            for (int i = 0; i < relativePathCount; i++)
            {
                string path = Path.Combine(Application.persistentDataPath, _artifactPathScratch[i]);
                if (File.Exists(path))
                    File.Delete(path);

                _artifactPathScratch[i] = null;
            }

            SaveThumbnailSystem.DeleteThumbnail(slotName);
            if (_thumbnailCache != null && _thumbnailCache.TryGetValue(slotName, out Texture2D thumb))
            {
                if (thumb != null) DestroyImmediate(thumb);
                RemoveThumbnailFromCache(slotName);
            }
        }

        private void AddThumbnailToCache(string slotName, Texture2D texture)
        {
            if (_thumbnailCache.TryGetValue(slotName, out Texture2D existing) && existing != null && existing != texture)
            {
                DestroyImmediate(existing);
            }

            _thumbnailCache[slotName] = texture;
            MarkThumbnailAsMostRecent(slotName);
            TrimThumbnailCacheToLimit();
        }

        private void RemoveThumbnailFromCache(string slotName)
        {
            _thumbnailCache.Remove(slotName);

            for (int i = 0; i < _thumbnailCacheOrder.Count; i++)
            {
                if (string.Equals(_thumbnailCacheOrder[i], slotName, StringComparison.OrdinalIgnoreCase))
                {
                    _thumbnailCacheOrder.RemoveAt(i);
                    return;
                }
            }
        }

        private void TrimThumbnailCacheToLimit()
        {
            while (_thumbnailCacheOrder.Count > MaxCachedThumbnails)
            {
                string oldestSlotName = _thumbnailCacheOrder[0];
                _thumbnailCacheOrder.RemoveAt(0);

                if (!_thumbnailCache.TryGetValue(oldestSlotName, out Texture2D cached))
                    continue;

                _thumbnailCache.Remove(oldestSlotName);
                if (cached != null)
                    DestroyImmediate(cached);
            }
        }

        private void MarkThumbnailAsMostRecent(string slotName)
        {
            for (int i = 0; i < _thumbnailCacheOrder.Count; i++)
            {
                if (!string.Equals(_thumbnailCacheOrder[i], slotName, StringComparison.OrdinalIgnoreCase))
                    continue;

                _thumbnailCacheOrder.RemoveAt(i);
                break;
            }

            _thumbnailCacheOrder.Add(slotName);
        }

        private static string FormatTimestamp(long ticksUtc)
        {
            if (ticksUtc <= 0L)
                return "never";

            return new DateTime(ticksUtc, DateTimeKind.Utc).ToLocalTime().ToString("g");
        }

        private string FormatTime(float seconds)
        {
            TimeSpan t = TimeSpan.FromSeconds(seconds);
            return string.Format("{0:D2}h {1:D2}m {2:D2}s", t.Hours + t.Days * 24, t.Minutes, t.Seconds);
        }
    }
}
