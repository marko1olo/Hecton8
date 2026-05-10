// ----------------------------------------------------------------------------
// HECTON-8 - RockDataBakerWindow.cs
// Editor window for exporting scene socket data to a ScriptableObject and
// preparing rock prefabs for Mesh Baker.
//
// Modes:
//   1. Extract & Save - capture socket data and save it to a .asset.
//   2. Prepare for Mesh Baker - disable sockets before baking.
//
// Operates on the selected Hierarchy object, treating it as the rock-group root.
// Scans child HectonSocketHelper components and stores their local coordinates.
// ----------------------------------------------------------------------------

#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.IO;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

namespace Hecton8.Building.Editor
{
    public sealed class RockDataBakerWindow : EditorWindow
    {
        // ----------------------------------------------------------
        //  CONSTANTS
        // ----------------------------------------------------------

        private const string SAVE_FOLDER = "Assets/_Project/Data/RockSockets";
        private const string SOCKET_PREFIX = "SOCKET_";

        // ----------------------------------------------------------
        //  STATE
        // ----------------------------------------------------------

        private GameObject _selectedRoot;
        private RockAttachmentData _lastExportedAsset;
        private Vector2 _scrollPos;
        private string _statusMessage = "";
        private MessageType _statusType = MessageType.None;

        // Preview data
        private int _previewTotal;
        private int _previewTop;
        private int _previewSide;
        private int _previewUnder;

        // ----------------------------------------------------------
        //  MENU ITEM
        // ----------------------------------------------------------

        [MenuItem("Hecton/Building/Rock Data Baker", false, 100)]
        public static void ShowWindow()
        {
            var window = GetWindow<RockDataBakerWindow>();
            window.titleContent = new GUIContent("Rock Data Baker");
            window.minSize = new Vector2(350f, 400f);
        }

        // ----------------------------------------------------------
        //  GUI
        // ----------------------------------------------------------

        private void OnGUI()
        {
            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("ROCK DATA BAKER",
                EditorStyles.boldLabel);
            EditorGUILayout.Space(5);

            // Selection
            EditorGUILayout.LabelField("Step 0: Select root group in Hierarchy",
                EditorStyles.miniLabel);

            EditorGUI.BeginChangeCheck();
            _selectedRoot = (GameObject)EditorGUILayout.ObjectField(
                "Root Group (GRP_...)",
                _selectedRoot,
                typeof(GameObject),
                true);

            if (EditorGUI.EndChangeCheck() && _selectedRoot != null)
            {
                RefreshPreview();
            }

            // Auto-fill from selection
            if (GUILayout.Button("Use Current Selection"))
            {
                if (Selection.activeGameObject != null)
                {
                    _selectedRoot = Selection.activeGameObject;
                    RefreshPreview();
                }
            }

            EditorGUILayout.Space(10);

            // Preview
            if (_selectedRoot != null)
            {
                EditorGUILayout.LabelField("Preview", EditorStyles.boldLabel);

                EditorGUI.indentLevel++;
                EditorGUILayout.LabelField($"Root: {_selectedRoot.name}");
                EditorGUILayout.LabelField($"Total sockets: {_previewTotal}");

                EditorGUI.BeginDisabledGroup(true);
                EditorGUILayout.IntField("Top (green)", _previewTop);
                EditorGUILayout.IntField("Side (yellow)", _previewSide);
                EditorGUILayout.IntField("Under (red)", _previewUnder);
                EditorGUI.EndDisabledGroup();

                EditorGUI.indentLevel--;

                EditorGUILayout.Space(15);

                // Button 1: Extract & Save
                EditorGUILayout.LabelField("Step 1: Export socket data to asset",
                    EditorStyles.miniLabel);

                GUI.backgroundColor = new Color(0.3f, 0.8f, 0.3f);
                if (GUILayout.Button("1. Extract & Save Data", GUILayout.Height(35)))
                {
                    ExtractAndSave();
                }
                GUI.backgroundColor = Color.white;

                EditorGUILayout.Space(10);

                // Button 2: Prepare for Mesh Baker
                EditorGUILayout.LabelField("Step 2: Deactivate sockets before baking",
                    EditorStyles.miniLabel);

                GUI.backgroundColor = new Color(0.8f, 0.6f, 0.2f);
                if (GUILayout.Button("2. Prepare for Mesh Baker", GUILayout.Height(35)))
                {
                    PrepareForMeshBaker();
                }
                GUI.backgroundColor = Color.white;

                EditorGUILayout.Space(10);

                // Button 3: Restore sockets
                EditorGUILayout.LabelField("Restore deactivated sockets",
                    EditorStyles.miniLabel);

                if (GUILayout.Button("Restore Sockets"))
                {
                    RestoreSockets();
                }
            }
            else
            {
                EditorGUILayout.HelpBox(
                    "Select a root group object (e.g. GRP_Rock_01) in Hierarchy.\n" +
                    "The tool will find all child objects with HectonSocketHelper.",
                    MessageType.Info);
            }

            // Status message
            if (!string.IsNullOrEmpty(_statusMessage))
            {
                EditorGUILayout.Space(10);
                EditorGUILayout.HelpBox(_statusMessage, _statusType);
            }

            // Last exported asset
            if (_lastExportedAsset != null)
            {
                EditorGUILayout.Space(5);
                EditorGUI.BeginDisabledGroup(true);
                EditorGUILayout.ObjectField("Last Export",
                    _lastExportedAsset, typeof(RockAttachmentData), false);
                EditorGUI.EndDisabledGroup();
            }

            EditorGUILayout.EndScrollView();
        }

        // ----------------------------------------------------------
        //  PREVIEW
        // ----------------------------------------------------------

        private void RefreshPreview()
        {
            _previewTotal = 0;
            _previewTop = 0;
            _previewSide = 0;
            _previewUnder = 0;

            if (_selectedRoot == null) return;

            var helpers = _selectedRoot.GetComponentsInChildren<HectonSocketHelper>(true);
            _previewTotal = helpers.Length;

            for (int i = 0; i < helpers.Length; i++)
            {
                var type = GetSocketType(helpers[i]);
                switch (type)
                {
                    case HectonSocketHelper.SocketType.Top:   _previewTop++;   break;
                    case HectonSocketHelper.SocketType.Side:  _previewSide++;  break;
                    case HectonSocketHelper.SocketType.Under: _previewUnder++; break;
                }
            }
        }

        // ----------------------------------------------------------
        //  EXTRACT & SAVE
        // ----------------------------------------------------------

        private void ExtractAndSave()
        {
            if (_selectedRoot == null)
            {
                SetStatus("No root selected!", MessageType.Error);
                return;
            }

            var helpers = _selectedRoot.GetComponentsInChildren<HectonSocketHelper>(true);

            if (helpers.Length == 0)
            {
                SetStatus(
                    $"No HectonSocketHelper found on children of {_selectedRoot.name}",
                    MessageType.Warning);
                return;
            }

            // Ensure save folder exists
            if (!AssetDatabase.IsValidFolder(SAVE_FOLDER))
            {
                CreateFolderRecursive(SAVE_FOLDER);
            }

            // Create or update asset
            string assetName = SanitizeFileName(_selectedRoot.name);
            string assetPath = $"{SAVE_FOLDER}/{assetName}_Sockets.asset";

            RockAttachmentData data = AssetDatabase.LoadAssetAtPath<RockAttachmentData>(assetPath);
            bool isNew = data == null;

            if (isNew)
            {
                data = ScriptableObject.CreateInstance<RockAttachmentData>();
            }
            else
            {
                Undo.RecordObject(data, "Update Rock Socket Data");
            }

            // Fill data
            data.sockets.Clear();
            data.sourceGroupName = _selectedRoot.name;
            data.exportTimestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

            Transform root = _selectedRoot.transform;

            for (int i = 0; i < helpers.Length; i++)
            {
                var helper = helpers[i];
                Transform t = helper.transform;

                // Compute local position/rotation relative to root
                Vector3 localPos = root.InverseTransformPoint(t.position);
                Quaternion localRot = Quaternion.Inverse(root.rotation) * t.rotation;

                var socketData = new RockAttachmentData.SocketData
                {
                    localPos = (float3)localPos,
                    localRot = (quaternion)localRot,
                    type = GetSocketType(helper)
                };

                data.sockets.Add(socketData);
            }

            // Save asset
            if (isNew)
            {
                AssetDatabase.CreateAsset(data, assetPath);
            }
            else
            {
                EditorUtility.SetDirty(data);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            _lastExportedAsset = data;

            SetStatus(
                $"Saved {helpers.Length} sockets to {assetPath}\n" +
                $"Top: {_previewTop}, Side: {_previewSide}, Under: {_previewUnder}",
                MessageType.Info);

            // Ping in project window
            EditorGUIUtility.PingObject(data);
        }

        // ----------------------------------------------------------
        //  PREPARE FOR MESH BAKER
        // ----------------------------------------------------------

        private void PrepareForMeshBaker()
        {
            if (_selectedRoot == null)
            {
                SetStatus("No root selected!", MessageType.Error);
                return;
            }

            var helpers = _selectedRoot.GetComponentsInChildren<HectonSocketHelper>(true);

            if (helpers.Length == 0)
            {
                SetStatus("No sockets to deactivate.", MessageType.Warning);
                return;
            }

            int count = 0;

            Undo.SetCurrentGroupName("Prepare Sockets for Mesh Baker");

            for (int i = 0; i < helpers.Length; i++)
            {
                GameObject go = helpers[i].gameObject;
                if (go.activeSelf)
                {
                    Undo.RecordObject(go, "Deactivate Socket");
                    go.SetActive(false);
                    count++;
                }
            }

            SetStatus(
                $"Deactivated {count} socket objects.\n" +
                "Ready for Mesh Baker. Use Undo (Ctrl+Z) or Restore button to bring them back.",
                MessageType.Info);
        }

        // ----------------------------------------------------------
        //  RESTORE SOCKETS
        // ----------------------------------------------------------

        private void RestoreSockets()
        {
            if (_selectedRoot == null)
            {
                SetStatus("No root selected!", MessageType.Error);
                return;
            }

            // GetComponentsInChildren(true) finds inactive too
            var helpers = _selectedRoot.GetComponentsInChildren<HectonSocketHelper>(true);

            int count = 0;

            Undo.SetCurrentGroupName("Restore Sockets");

            for (int i = 0; i < helpers.Length; i++)
            {
                GameObject go = helpers[i].gameObject;
                if (!go.activeSelf)
                {
                    Undo.RecordObject(go, "Activate Socket");
                    go.SetActive(true);
                    count++;
                }
            }

            SetStatus($"Restored {count} socket objects.", MessageType.Info);
            RefreshPreview();
        }

        // ----------------------------------------------------------
        //  UTILITY
        // ----------------------------------------------------------

        /// <summary>
        /// Reads the socket type from HectonSocketHelper through SerializedObject.
        /// Required because socketType is a private [SerializeField].
        /// </summary>
        private static HectonSocketHelper.SocketType GetSocketType(HectonSocketHelper helper)
        {
            var so = new SerializedObject(helper);
            var prop = so.FindProperty("socketType");
            return (HectonSocketHelper.SocketType)prop.enumValueIndex;
        }

        private static string SanitizeFileName(string name)
        {
            char[] invalid = Path.GetInvalidFileNameChars();
            for (int i = 0; i < invalid.Length; i++)
            {
                name = name.Replace(invalid[i], '_');
            }
            return name;
        }

        private static void CreateFolderRecursive(string fullPath)
        {
            string[] parts = fullPath.Split('/');
            string current = parts[0]; // "Assets"

            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }
                current = next;
            }
        }

        private void SetStatus(string message, MessageType type)
        {
            _statusMessage = message;
            _statusType = type;
            Repaint();
        }
    }
}

#endif
