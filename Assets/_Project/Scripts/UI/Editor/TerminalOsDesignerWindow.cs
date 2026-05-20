#if UNITY_EDITOR
using Hecton8.UI;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

namespace Hecton8.UI.Editor
{
    public sealed class TerminalOsDesignerWindow : EditorWindow
    {
        private const string WindowTitle = "Terminal OS Designer";
        private static readonly int TerminalTextureArrayId = Shader.PropertyToID("_TerminalTextureArray");
        private static readonly int TerminalSliceId = Shader.PropertyToID("_TerminalSlice");

        private TerminalOsRuntime _runtime;
        private Material _previewMaterial;
        private int _selectedSlice;

        [MenuItem("Tools/HECTON-8/Terminal OS Designer")]
        public static void Open()
        {
            GetWindow<TerminalOsDesignerWindow>(WindowTitle);
        }

        private void OnDisable()
        {
            if (_previewMaterial != null)
            {
                DestroyImmediate(_previewMaterial);
                _previewMaterial = null;
            }
        }

        private void OnGUI()
        {
            DrawRuntimeSelector();
            if (_runtime == null)
                return;

            int count = Mathf.Max(0, _runtime.GetTerminalCount());
            if (count <= 0)
            {
                EditorGUILayout.HelpBox("Runtime has no terminal state allocated.", MessageType.Warning);
                return;
            }

            _selectedSlice = EditorGUILayout.IntSlider("Texture Slice", _selectedSlice, 0, count - 1);
            DrawStateEditor(_selectedSlice);
            DrawPreview(_selectedSlice);
        }

        private void DrawRuntimeSelector()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                _runtime = (TerminalOsRuntime)EditorGUILayout.ObjectField("Runtime", _runtime, typeof(TerminalOsRuntime), true);
                if (GUILayout.Button("Find", GUILayout.Width(64f)))
                    _runtime = FindFirstObjectByType<TerminalOsRuntime>();
            }
        }

        private void DrawStateEditor(int index)
        {
            if (!_runtime.TryGetScreenCommandCopy(index, out ScreenCommandDTO command))
                return;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Layout", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            float textX = EditorGUILayout.Slider("Text X", command.Position.x, 0f, 1f);
            float textY = EditorGUILayout.Slider("Text Y", command.Position.y, 0f, 1f);
            float textScale = EditorGUILayout.Slider("Text Scale", command.Scale, 0.025f, 0.25f);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(_runtime, "Terminal OS Layout");
                _runtime.SetScreenCommand(index, new float2(textX, textY), textScale);
                EditorUtility.SetDirty(_runtime);
            }

            if (_runtime.TryGetTerminalStateCopy(index, out TerminalStateDTO state))
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Mock State", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("State", "DTO loaded");
                EditorGUI.BeginChangeCheck();
                float barWidth = EditorGUILayout.Slider("Bar Width", state.Value1, 0f, 1f);
                float damage = EditorGUILayout.Slider("Damage", state.Value2, 0f, 1f);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(_runtime, "Terminal OS Mock State");
                    if (_runtime.TrySetTerminalMockState(index, barWidth, damage))
                        EditorUtility.SetDirty(_runtime);
                }
            }
        }

        private void DrawPreview(int index)
        {
            EditorGUILayout.Space();
            Rect previewRect = EditorGUILayout.GetControlRect(false, Mathf.Min(position.width * 0.62f, 360f));
            RenderTexture textureArray = _runtime.GetTerminalTextureArray();
            if (textureArray == null)
            {
                EditorGUI.HelpBox(previewRect, "No Texture2DArray allocated. Enter Play Mode or wake the runtime.", MessageType.Info);
                return;
            }

            Material material = EnsurePreviewMaterial();
            if (material == null)
            {
                EditorGUI.HelpBox(previewRect, "Preview material shader not found.", MessageType.Warning);
                return;
            }

            material.SetTexture(TerminalTextureArrayId, textureArray);
            material.SetFloat(TerminalSliceId, index);
            if (Event.current.type == EventType.Repaint)
                UnityEngine.Graphics.DrawTexture(previewRect, textureArray, material);
        }

        private Material EnsurePreviewMaterial()
        {
            if (_previewMaterial != null)
                return _previewMaterial;

            Shader shader = Shader.Find("HECTON/UI/Diegetic Terminal");
            if (shader == null)
                shader = Shader.Find("HECTON/UI/Terminal TextureArray Panel");
            if (shader == null)
                return null;

            _previewMaterial = new Material(shader)
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            return _previewMaterial;
        }
    }
}
#endif
