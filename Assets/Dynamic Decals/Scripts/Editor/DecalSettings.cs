using UnityEngine;
using UnityEditor;
using System.Collections;
using System.Collections.Generic;

using UnityEditorInternal;
using LlockhamIndustries.ExtensionMethods;

namespace LlockhamIndustries.Decals
{
    public class DecalSettings : EditorWindow
    {
        [MenuItem("Window/Decals/Setting")]
        private static void Init()
        {
            DecalSettings window = (DecalSettings)GetWindow(typeof(DecalSettings));
            window.titleContent = new GUIContent("Decal Settings");
            window.minSize = new Vector2(300, 180);
            window.Show();
        }

        //Cached variables
        private string assetPath = "Assets/Dynamic Decals/Resources/Settings.asset";

        //Quality Settings Tab
        private int qualitySetting = 0;

        //ScrollView
        private Vector2 scrollPosition;

        //Generic methods
        private void OnEnable()
        {
            //Register undo/redo callback
            Undo.undoRedoPerformed += UndoRedo;
        }
        private void OnDisable()
        {
            //De-register undo/redo callback
            Undo.undoRedoPerformed -= UndoRedo;
        }

        private void OnGUI()
        {
            //Grab our settings
            DynamicDecalSettings settings = DynamicDecals.System.Settings;

            //Calculate required rect height
            float maskHeight = 120;
            float debugHeight = (Application.isPlaying) ? 360 : 80;
            float totalHeight = LlockhamEditorUtility.TabHeight * (settings.pools.Length + 4) + maskHeight + debugHeight + 54;

            //Begin change check & scrollView
            EditorGUI.BeginChangeCheck();
            Rect scrollRect = new Rect(0, 0, position.width - 20, totalHeight);
            scrollPosition = GUI.BeginScrollView(new Rect(10, 10, position.width - 20, position.height - 20), scrollPosition, scrollRect, GUIStyle.none, GUIStyle.none);

            //Mask settings
            MaskSettings(new Rect(0, 44 + (LlockhamEditorUtility.TabHeight * (settings.pools.Length + 4)), scrollRect.width, maskHeight), settings);

            //Pool settings
            PoolHeader(new Rect(0, 10, scrollRect.width, 24), settings);
            QualityTabs(new Rect(0, 34, scrollRect.width, LlockhamEditorUtility.TabHeight));
            PoolSettings(new Rect(0, 34 + LlockhamEditorUtility.TabHeight, scrollRect.width, LlockhamEditorUtility.TabHeight * (settings.pools.Length + 3)), settings);

            //Debug settings
            DebugSettings(new Rect(0, 54 + (LlockhamEditorUtility.TabHeight * (settings.pools.Length + 4)) + maskHeight, scrollRect.width, debugHeight));

            //End change check & scrollView
            GUI.EndScrollView();
            if (EditorGUI.EndChangeCheck())
            {
                //If the asset already exists, mark it to be saved
                if (Resources.Load<DynamicDecalSettings>("Settings") != null) EditorUtility.SetDirty(settings);

                //If the asset doen't exist, create it
                else AssetDatabase.CreateAsset(settings, assetPath);

                //Apply changes to system immediately
                DynamicDecals.ApplySettings();
            }
        }
        private void OnInspectorUpdate()
        {
            Repaint();
        }

        private void UndoRedo()
        {
            //Grab our settings
            DynamicDecalSettings settings = DynamicDecals.System.Settings;

            //Recalculate passes
            settings.CalculatePasses();

            //Update renderers
            DynamicDecals.System.UpdateRenderers();

            //Repaint the window to show changes immediately
            Repaint();

            //Apply changes to system immediately
            DynamicDecals.ApplySettings();
        }

        private void MaskSettings(Rect Area, DynamicDecalSettings Settings)
        {
            GUI.BeginGroup(Area);

            //Header
            EditorGUI.DrawRect(new Rect(0, 0, Area.width, 24), LlockhamEditorUtility.HeaderColor);
            EditorGUI.LabelField(new Rect(8, 4, Area.width - 32, 16), "Masking", EditorStyles.boldLabel);

            //Reset
            Rect Reset = new Rect(Area.width - 20, 6, 12, 12);
            if (Event.current.type == EventType.MouseDown && Event.current.button == 0 && Reset.Contains(Event.current.mousePosition))
            {
                Undo.RecordObject(Settings, "Reset Masking");
                Settings.ResetMasking();
                Event.current.Use();
            }
            GUI.DrawTexture(Reset, LlockhamEditorUtility.Reset);

            //Draw Background
            EditorGUI.DrawRect(new Rect(0, 24, Area.width, Area.height - 24), LlockhamEditorUtility.MidgroundColor);

            //Begin Layout Area
            GUILayout.BeginArea(new Rect(4, 32, Area.width - 20, Area.height - 32));

            //Generate layer options
            for (int i = 0; i < Settings.Layers.Length; i++)
            {
                EditorGUI.BeginChangeCheck();

                EditorGUILayout.BeginHorizontal();
                string layerName = EditorGUILayout.TextField(new GUIContent(""), Settings.Layers[i].name, GUILayout.Width(Area.width - 200));
                GUILayout.FlexibleSpace();
                LayerMask layerMask = EditorGUILayout.MaskField(new GUIContent(""), InternalEditorUtility.LayerMaskToConcatenatedLayersMask(Settings.Layers[i].layers), InternalEditorUtility.layers, GUILayout.Width(160));
                EditorGUILayout.EndHorizontal();

                if (EditorGUI.EndChangeCheck())
                {
                    //Record state for undo
                    Undo.RecordObject(Settings, "Layer name");

                    //Change layer name
                    Settings.Layers[i].name = layerName;
                    Settings.Layers[i].layers = InternalEditorUtility.ConcatenatedLayersMaskToLayerMask(layerMask);

                    //Recalculate passes
                    Settings.CalculatePasses();
                }
            }
            EditorGUILayout.Space();

            GUILayout.EndArea();
            GUI.EndGroup();
        }
        private void DebugSettings(Rect Area)
        {
            if (Application.isPlaying && DynamicDecals.Initialized)
            {
                GUI.BeginGroup(Area);

                //Header
                EditorGUI.DrawRect(new Rect(0, 0, Area.width, 24), LlockhamEditorUtility.HeaderColor);
                EditorGUI.LabelField(new Rect(8, 4, Area.width - 32, 16), "Debug", EditorStyles.boldLabel);

                //Draw Background
                EditorGUI.DrawRect(new Rect(0, 24, Area.width, Area.height - 24), LlockhamEditorUtility.MidgroundColor);
            
                //Begin Layout Area
                GUILayout.BeginArea(new Rect(6, 32, Area.width - 12, Area.height - 32));

            
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Projections : " + DynamicDecals.System.ProjectionCount, GUILayout.Width(120));
                GUILayout.FlexibleSpace();
                EditorGUILayout.LabelField("Renderers : " + DynamicDecals.System.RendererCount, GUILayout.Width(120));
                EditorGUILayout.EndHorizontal();

                //Enabled
                EditorGUI.BeginChangeCheck();
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(new GUIContent("Enabled", "Toggles the system on and off."), GUILayout.Width(150));
                GUILayout.FlexibleSpace();
                bool enabled = EditorGUILayout.Toggle(new GUIContent(""), DynamicDecals.System.isActiveAndEnabled, GUILayout.Width(Area.width - 180));
                EditorGUILayout.EndHorizontal();

                if (EditorGUI.EndChangeCheck())
                {
                    //Record state for undo
                    Undo.RecordObject(DynamicDecals.System, "Enabled/Disable");

                    //Change forward depth locking
                    DynamicDecals.System.enabled = enabled;
                }

                //Shader replacement
                EditorGUI.BeginChangeCheck();
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(new GUIContent("Shader Replacement", "Toggles the systems requisite shader replacement on and off."), GUILayout.Width(150));
                GUILayout.FlexibleSpace();
                bool shaderReplacement = EditorGUILayout.Toggle(new GUIContent(""), DynamicDecals.System.ShaderReplacement, GUILayout.Width(Area.width - 180));
                EditorGUILayout.EndHorizontal();

                if (EditorGUI.EndChangeCheck())
                {
                    //Record state for undo
                    Undo.RecordObject(DynamicDecals.System, "Toggle shader replacement \r\n");

                    //Change forward depth locking
                    DynamicDecals.System.ShaderReplacement = shaderReplacement;
                }

                EditorGUILayout.Space();

                string debug = DynamicDecals.DebugLog;
                debug = EditorGUILayout.TextArea(debug, GUILayout.MaxHeight(250));

                GUILayout.EndArea();
            GUI.EndGroup();
            }
        }

        private void PoolHeader(Rect Area, DynamicDecalSettings Settings)
        {
            GUI.BeginGroup(Area);

            //Header
            EditorGUI.DrawRect(new Rect(0, 0, Area.width, 24), LlockhamEditorUtility.HeaderColor);
            EditorGUI.LabelField(new Rect(8, 4, Area.width - 32, 16), "Pools", EditorStyles.boldLabel);

            //Reset
            Rect Reset = new Rect(Area.width - 20, 6, 12, 12);
            if (Event.current.type == EventType.MouseDown && Event.current.button == 0 && Reset.Contains(Event.current.mousePosition))
            {
                Undo.RecordObject(Settings, "Reset Pools");
                Settings.ResetPools();
                Event.current.Use();
            }
            GUI.DrawTexture(Reset, LlockhamEditorUtility.Reset);

            GUI.EndGroup();
        }
        private void QualityTabs(Rect Area)
        {
            EditorGUI.DrawRect(Area, LlockhamEditorUtility.MidgroundColor);
            GUI.BeginGroup(Area, new GUIContent(""));

            //Quality Tabs
            int tabCount = QualitySettings.names.Length;
            float tabWidth = Area.width / tabCount;

            EditorGUI.DrawRect(new Rect(0, 0, Area.width, LlockhamEditorUtility.TabHeight), LlockhamEditorUtility.BackgroundColor);
            for (int i = 0; i < tabCount; i++)
            {
                //Tab Logic
                Rect Tab = new Rect((i * tabWidth), 2, tabWidth, LlockhamEditorUtility.TabHeight - 2);
                if (Event.current.type == EventType.MouseDown && Event.current.button == 0 && Tab.Contains(Event.current.mousePosition))
                {
                    //Switch to selected quality setting
                    qualitySetting = i;

                    //Unfocus controls on tab switch
                    GUIUtility.keyboardControl = 0;

                    //Use event
                    Event.current.Use();
                }

                //Draw Tab
                if (i == qualitySetting) EditorGUI.DrawRect(Tab, LlockhamEditorUtility.HeaderColor);
                else EditorGUI.DrawRect(new Rect(Tab.x + 1, Tab.y + 2, Tab.width - 2, Tab.height - 2), LlockhamEditorUtility.MidgroundColor);
                GUI.Label(new Rect((i * tabWidth), 1, tabWidth, LlockhamEditorUtility.TabHeight - 1), new GUIContent(QualitySettings.names[i]), LlockhamEditorUtility.MiniTabLabel);
            }

            GUI.EndGroup();
        }
        private void PoolSettings(Rect Area, DynamicDecalSettings Settings)
        {
            EditorGUI.DrawRect(Area, LlockhamEditorUtility.MidgroundColor);
            GUI.BeginGroup(Area, new GUIContent(""));

            //Collumn width
            float poolCollumnWidth = Area.width / 3;

            //Button Dimensions
            float buttonHeight = 16;
            float buttonMajorWidth = 80;


            //Header
            EditorGUI.DrawRect(new Rect(0, 0, Area.width, LlockhamEditorUtility.TabHeight), LlockhamEditorUtility.HeaderColor);
            GUI.Label(new Rect(4, 2, poolCollumnWidth, LlockhamEditorUtility.TabHeight), "Title", EditorStyles.miniBoldLabel);
            GUI.Label(new Rect(4 + poolCollumnWidth, 2, poolCollumnWidth, LlockhamEditorUtility.TabHeight), "Limit", EditorStyles.miniBoldLabel);

            //Pool Content
            for (int i = 0; i < Settings.pools.Length; i++)
            {
                Rect poolRect = new Rect(0, LlockhamEditorUtility.TabHeight * (1 + i), Area.width, LlockhamEditorUtility.TabHeight);
                PoolItem(poolRect, Settings, Settings.pools[i], i);
            }

            //New Pool
            EditorGUI.DrawRect(new Rect(0, LlockhamEditorUtility.TabHeight * (1 + Settings.pools.Length), Area.width, LlockhamEditorUtility.TabHeight), (Settings.pools.Length % 2 != 0) ? LlockhamEditorUtility.MidgroundColor : LlockhamEditorUtility.ForegroundColor);
            if (GUI.Button(new Rect((Area.width - buttonMajorWidth) / 2, LlockhamEditorUtility.TabHeight * (1 + Settings.pools.Length) + ((LlockhamEditorUtility.TabHeight - buttonHeight) / 2), buttonMajorWidth, buttonHeight), "+"))
            {
                //Record state for undo
                Undo.RecordObject(Settings, "Add Pool");

                //Add pool
                NewPool(Settings);
            }

            //Total
            EditorGUI.DrawRect(new Rect(0, LlockhamEditorUtility.TabHeight * (2 + Settings.pools.Length), Area.width, LlockhamEditorUtility.TabHeight), LlockhamEditorUtility.HeaderColor);
            GUI.Label(new Rect(4, LlockhamEditorUtility.TabHeight * (2 + Settings.pools.Length), poolCollumnWidth, LlockhamEditorUtility.TabHeight), "Total", EditorStyles.miniBoldLabel);

            //Calculate total
            float total = 0;
            for (int i = 0; i < Settings.pools.Length; i++) total += Settings.pools[i].limits[qualitySetting];
            GUI.Label(new Rect(4 + poolCollumnWidth, LlockhamEditorUtility.TabHeight * (2 + Settings.pools.Length), poolCollumnWidth, LlockhamEditorUtility.TabHeight), total.ToString(), EditorStyles.miniBoldLabel);

            GUI.EndGroup();
        }
        private void PoolItem(Rect Area, DynamicDecalSettings Settings, PoolInstance Instance, int Index)
        {
            float collumnWidth = Area.width / 3;
            float buttonSize = 16;

            //Background
            EditorGUI.DrawRect(Area, (Index % 2 != 0) ? LlockhamEditorUtility.MidgroundColor : LlockhamEditorUtility.ForegroundColor);
            GUI.BeginGroup(Area);

            //Title
            if (Index == 0)
            {
                GUI.Label(new Rect(4, 2, collumnWidth, 16), "Default", LlockhamEditorUtility.MiniLabel);
                if (Instance.title != "Default") Instance.title = "Default";
            }
            else
            {
                EditorGUI.BeginChangeCheck();
                string title = EditorGUI.TextField(new Rect(4, 2, collumnWidth, 16), Instance.title, LlockhamEditorUtility.MiniLabel);
                if (EditorGUI.EndChangeCheck())
                {
                    //Record state for undo
                    Undo.RecordObject(Settings, "Rename pool");

                    //Rename pool
                    Instance.title = title;
                }
            }

            //Limit
            EditorGUI.BeginChangeCheck();
            int limit = EditorGUI.IntField(new Rect(4 + collumnWidth, 2, collumnWidth, 16), Instance.limits[qualitySetting]);
            if (EditorGUI.EndChangeCheck())
            {
                //Record state for undo
                Undo.RecordObject(Settings, "Resize pool");

                //Adjust pool size
                Instance.limits[qualitySetting] = limit;
            }

            //Utility Buttons
            Rect utilityRect = new Rect(Area.width - (buttonSize * 3 + 16) - 12, 2, (buttonSize * 3 + 16), 16);
            GUI.BeginGroup(utilityRect);

            //Cache GUI.enabled
            bool GUIEnabled = GUI.enabled;

            //Up
            if (Index < 2) GUI.enabled = false;
            if (GUI.Button(new Rect(4, (utilityRect.height - buttonSize) / 2, buttonSize, buttonSize), "↑"))
            {
                //Record state for undo
                Undo.RecordObject(Settings, "Pool Up");

                //Move pool up
                Swap(Settings, Index, Index - 1);
            }
            //Restore GUI state
            GUI.enabled = GUIEnabled;

            //Down
            if (Index == 0 || Index == Settings.pools.Length - 1) GUI.enabled = false;
            if (GUI.Button(new Rect(buttonSize + 8, (utilityRect.height - buttonSize) / 2, buttonSize, buttonSize), "↓"))
            {
                //Record state for undo
                Undo.RecordObject(Settings, "Pool Down");

                //Move pool down
                Swap(Settings, Index, Index + 1);
            }
            //Restore GUI state
            GUI.enabled = GUIEnabled;

            //Remove
            if (Index == 0) GUI.enabled = false;
            if (GUI.Button(new Rect(2 * buttonSize + 12, (utilityRect.height - buttonSize) / 2, buttonSize, buttonSize), "-"))
            {
                //Record state for undo
                Undo.RecordObject(Settings, "Pool Down");

                //Remove pool
                RemoveAt(Settings, Index);
            }
            //Restore GUI state
            GUI.enabled = GUIEnabled;

            GUI.EndGroup();
            GUI.EndGroup();
        }

        //Pool functionality
        private void NewPool(DynamicDecalSettings Settings)
        {
            //Cache old pool
            PoolInstance[] oldPool = Settings.pools;

            //Extend array
            Settings.pools = new PoolInstance[oldPool.Length + 1];

            //Add back our old pools
            for (int i = 0; i < oldPool.Length; i++)
            {
                Settings.pools[i] = oldPool[i];
            }

            //Add our new pool
            Settings.pools[Settings.pools.Length - 1] = new PoolInstance("New", oldPool);
        }
        private void RemoveAt(DynamicDecalSettings Settings, int Index)
        {
            //Make sure index is valid
            if (Index > 0 && Index < Settings.pools.Length)
            {
                //Cache old pool
                PoolInstance[] oldPool = Settings.pools;

                //Reduce array size
                Settings.pools = new PoolInstance[oldPool.Length - 1];

                //Add back our old pools without the element
                int j = 0;
                for (int i = 0; i < oldPool.Length; i++)
                {
                    if (i != Index)
                    {
                        Settings.pools[j] = oldPool[i];
                        j++;
                    }
                }
            }
            else
            {
                Debug.LogError("Index Invalid");
            }
        }
        private void Swap(DynamicDecalSettings Settings, int IndexA, int IndexB)
        {
            PoolInstance temp = Settings.pools[IndexA];
            Settings.pools[IndexA] = Settings.pools[IndexB];
            Settings.pools[IndexB] = temp;
        }
    }
}