#if UNITY_EDITOR
using Hecton8.Core;
using UnityEditor;
using UnityEngine;

namespace Hecton8.Optimization.Editor
{
    /// <summary>
    /// Editor facade for Addressables heap sanitizer runtime state.
    /// </summary>
    public sealed class HeapSanitizerTunerWindow : EditorWindow
    {
        private const int MaxRows = 64;
        private const int LeakRefCountThreshold = 50;
        private Vector2 _scroll;
        private string _csvPath = "Assets/_Project/Data/asset_cache_rules.csv";

        [MenuItem("HECTON-8/Optimization/Heap Sanitizer Tuner")]
        private static void Open()
        {
            GetWindow<HeapSanitizerTunerWindow>("Heap Sanitizer Tuner");
        }

        private void OnGUI()
        {
            AssetLifecycleGovernor governor = GlobalRegistry.AssetLifecycle;
            if (governor == null)
            {
                EditorGUILayout.HelpBox("AssetLifecycleGovernor is not registered in GlobalRegistry.", MessageType.Warning);
                return;
            }

            DrawTuning(governor);
            EditorGUILayout.Space(8f);
            DrawCsvControls(governor);
            EditorGUILayout.Space(8f);
            DrawTrackerTable(governor);
        }

        private static void DrawTuning(AssetLifecycleGovernor governor)
        {
            EditorGUILayout.LabelField("Runtime", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Active Handles", governor.GetHeapSanitizerActiveHandleCount().ToString());
            EditorGUILayout.LabelField("Cache Hits", governor.GetHeapSanitizerCacheHitCount().ToString());
            EditorGUILayout.LabelField("Cache Misses", governor.GetHeapSanitizerCacheMissCount().ToString());
            EditorGUILayout.LabelField("Orphaned Handles Released", governor.GetHeapSanitizerOrphanedReleaseCount().ToString());

            float ttl = EditorGUILayout.Slider("Base TTL", governor.GetHeapSanitizerBaseTtlSeconds(), 10f, 300f);
            if (!Mathf.Approximately(ttl, governor.GetHeapSanitizerBaseTtlSeconds()))
                governor.SetHeapSanitizerBaseTtlSeconds(ttl);

            float threshold = EditorGUILayout.Slider("VRAM Panic Threshold", governor.GetHeapSanitizerVramPanicThreshold(), 0.5f, 0.99f);
            if (!Mathf.Approximately(threshold, governor.GetHeapSanitizerVramPanicThreshold()))
                governor.SetHeapSanitizerVramPanicThreshold(threshold);
        }

        private void DrawCsvControls(AssetLifecycleGovernor governor)
        {
            EditorGUILayout.LabelField("CSV Override", EditorStyles.boldLabel);
            _csvPath = EditorGUILayout.TextField("asset_cache_rules.csv", _csvPath);
            if (GUILayout.Button("Load Cache Rules"))
                governor.TryParseAssetCacheRulesCsv(_csvPath);
        }

        private void DrawTrackerTable(AssetLifecycleGovernor governor)
        {
            EditorGUILayout.LabelField("Tracked AssetHashes", EditorStyles.boldLabel);
            bool leakWarning = false;
            for (int i = 0; i < MaxRows; i++)
            {
                if (!governor.TryGetHeapSanitizerTrackerAt(i, out AssetTrackerDTO tracker, out _, out _))
                    break;

                if (tracker.ReferenceCount > LeakRefCountThreshold)
                {
                    leakWarning = true;
                    break;
                }
            }

            if (leakWarning)
            {
                GUIStyle style = new GUIStyle(EditorStyles.boldLabel)
                {
                    fontSize = 24,
                    alignment = TextAnchor.MiddleCenter
                };
                Color previous = GUI.color;
                GUI.color = Color.red;
                EditorGUILayout.LabelField("LEAK SUSPECT: REFCOUNT > 50", style, GUILayout.Height(44f));
                GUI.color = previous;
            }

            _scroll = EditorGUILayout.BeginScrollView(_scroll, GUILayout.MinHeight(180f));
            for (int i = 0; i < MaxRows; i++)
            {
                if (!governor.TryGetHeapSanitizerTrackerAt(i, out AssetTrackerDTO tracker, out float ttl, out byte flags))
                    break;

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("0x" + tracker.AssetHash.ToString("X8"), GUILayout.Width(120f));
                EditorGUILayout.LabelField("Ref " + tracker.ReferenceCount.ToString(), GUILayout.Width(70f));
                EditorGUILayout.LabelField("Slot " + tracker.HandlePointer.ToString(), GUILayout.Width(80f));
                EditorGUILayout.LabelField("TTL " + ttl.ToString("0.0"), GUILayout.Width(80f));
                EditorGUILayout.LabelField("Flags 0x" + flags.ToString("X2"), GUILayout.Width(80f));
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndScrollView();
        }
    }
}
#endif
