// H8BlackboxWindow.cs — Editor UI for Hecton8 Blackbox Diagnostics
using System;
using UnityEditor;
using UnityEngine;

namespace Hecton8.BlackboxDiagnostics
{
    public class H8BlackboxWindow : EditorWindow
    {
        private H8DiagnosticOptions _opts = new H8DiagnosticOptions();
        private Vector2 _scroll;

        [MenuItem("Tools/Hecton8/Blackbox Diagnostics")]
        public static void ShowWindow()
        {
            var wnd = GetWindow<H8BlackboxWindow>("H8 Blackbox");
            wnd.minSize = new Vector2(400, 500);
            wnd.Show();
        }

        private void OnGUI()
        {
            GUILayout.Space(10);
            EditorGUILayout.LabelField("Hecton8 Blackbox Diagnostics", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Agent-ready factual telemetry tool.", EditorStyles.wordWrappedLabel);
            GUILayout.Space(10);

            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            EditorGUILayout.LabelField("Data Collection Options", EditorStyles.boldLabel);
            _opts.includeInactiveObjects = EditorGUILayout.Toggle("Include Inactive Objects", _opts.includeInactiveObjects);
            _opts.includeReflectionDump = EditorGUILayout.Toggle("Dump Reflected Fields", _opts.includeReflectionDump);
            _opts.includeConsoleLog = EditorGUILayout.Toggle("Include Console Log", _opts.includeConsoleLog);
            _opts.includeEditorLogTail = EditorGUILayout.Toggle("Include Editor.log Tail", _opts.includeEditorLogTail);
            _opts.includeGitDiff = EditorGUILayout.Toggle("Include Git Diff", _opts.includeGitDiff);
            _opts.includePlayModeDiff = EditorGUILayout.Toggle("Include PlayMode Diff", _opts.includePlayModeDiff);
            _opts.playModeWaitSeconds = EditorGUILayout.FloatField("PlayMode Wait (sec)", _opts.playModeWaitSeconds);

            GUILayout.Space(20);

            if (GUILayout.Button("1. Run SelfCheck", GUILayout.Height(30)))
            {
                H8Runner.RunSelfCheck(_opts);
            }

            GUILayout.Space(5);
            if (GUILayout.Button("2. Run EditMode Scan", GUILayout.Height(30)))
            {
                H8Runner.RunEditMode(_opts);
            }

            GUILayout.Space(5);
            if (GUILayout.Button("3. Run PlayMode (Current Scene)", GUILayout.Height(30)))
            {
                H8Runner.RunCurrentScenePlayMode(_opts);
            }

            GUILayout.Space(5);
            if (GUILayout.Button("4. Run PlayMode (From 00_BOOTSTRAP)", GUILayout.Height(30)))
            {
                H8Runner.RunBootstrapScenePlayMode(_opts);
            }

            GUILayout.Space(5);
            bool hasDirty = H8Utils.HasDirtyScenes();
            if (hasDirty)
            {
                var dirtyNames = H8Utils.GetDirtySceneNames();
                EditorGUILayout.HelpBox($"Cannot run Full Comparison. Dirty scenes detected:\n- {string.Join("\n- ", dirtyNames)}\nSave or revert them first.", MessageType.Warning);
                GUI.enabled = false;
            }

            if (GUILayout.Button("5. Full Comparison Direct vs Bootstrap", GUILayout.Height(30)))
            {
                H8Runner.RunFullComparison(_opts);
            }
            GUI.enabled = true;

            GUILayout.Space(5);
            if (GUILayout.Button("6. Open Output Folder", GUILayout.Height(30)))
            {
                string path = System.IO.Path.Combine(H8Utils.GetProjectRoot(), "AI_Diagnostics");
                if (System.IO.Directory.Exists(path))
                    EditorUtility.RevealInFinder(path);
                else
                    Debug.LogWarning("[H8Blackbox] Output folder does not exist yet.");
            }

            EditorGUILayout.HelpBox(
                "Full Comparison is the recommended mode. It compares direct current scene startup with 00_BOOTSTRAP startup.\nFull Comparison requires clean scenes. If dirty scenes exist, it will abort before opening 00_BOOTSTRAP.",
                MessageType.Info
            );

            EditorGUILayout.EndScrollView();
        }

        [MenuItem("Hecton8/Diagnostics/Run Hecton8 Blackbox Diagnostics Full Comparison", priority = 0)]
        public static void RunFullComparisonFromMenu()
        {
            var opts = new H8DiagnosticOptions
            {
                includeInactiveObjects = true,
                includeReflectionDump = true,
                includeConsoleLog = true,
                includeEditorLogTail = true,
                includeGitDiff = true,
                includePlayModeDiff = true,
                playModeWaitSeconds = 15f
            };
            
            H8Runner.RunFullComparison(opts);
        }
    }
}
