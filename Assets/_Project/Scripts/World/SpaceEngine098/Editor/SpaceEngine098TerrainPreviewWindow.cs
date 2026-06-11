using UnityEditor;
using UnityEngine;
using Unity.Mathematics;
using Unity.Collections;
using Unity.Jobs;
using Hecton8.World;

namespace Hecton8.SpaceEngine098.Editor
{
    public class SpaceEngine098TerrainPreviewWindow : EditorWindow
    {
        private Texture2D _previewTexture;
        private SpaceEngine098RidgedMultifractalParams _parameters;
        private int _resolution = 256;
        private float _zoom = 100f;
        private uint _seed = 12345;
        private float _offsetZ = 0f;
        private float _offsetX = 0f;

        [MenuItem("Window/Hecton-8/SpaceEngine 0.9.8 Preview")]
        public static void ShowWindow()
        {
            var window = GetWindow<SpaceEngine098TerrainPreviewWindow>("SpaceEngine Preview");
            window.minSize = new Vector2(400, 500);
            window.Show();
        }

        private void OnEnable()
        {
            _parameters = new SpaceEngine098RidgedMultifractalParams
            {
                Frequency = 1f,
                Strength01 = 1f,
                Gain = 2f,
                Warp = 0.5f,
                FirstOctaveValue = 1f,
                Lacunarity = 2.218f,
                H = 0.5f,
                Offset = 0.8f,
                RidgeSmooth = 0.0001f,
                Octaves = 8
            };
            GeneratePreview();
        }

        private void OnGUI()
        {
            GUILayout.Label("SpaceEngine 0.9.8 Ridged Multifractal Preview", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            
            _resolution = EditorGUILayout.IntSlider("Resolution", _resolution, 64, 512);
            _zoom = EditorGUILayout.Slider("Zoom (Scale)", _zoom, 10f, 1000f);
            _offsetX = EditorGUILayout.Slider("Offset X", _offsetX, -10000f, 10000f);
            _offsetZ = EditorGUILayout.Slider("Offset Z", _offsetZ, -10000f, 10000f);
            _seed = (uint)Mathf.Max(0, EditorGUILayout.IntField("Seed", (int)_seed));

            EditorGUILayout.Space();
            GUILayout.Label("Noise Parameters", EditorStyles.boldLabel);

            _parameters.Octaves = EditorGUILayout.IntSlider("Octaves", _parameters.Octaves, 1, 12);
            _parameters.Gain = EditorGUILayout.Slider("Gain", _parameters.Gain, 0f, 5f);
            _parameters.Warp = EditorGUILayout.Slider("Warp", _parameters.Warp, 0f, 2f);
            _parameters.Lacunarity = EditorGUILayout.Slider("Lacunarity", _parameters.Lacunarity, 1.0001f, 4f);
            _parameters.H = EditorGUILayout.Slider("H (Spectral Roughness)", _parameters.H, 0.001f, 2f);
            _parameters.Offset = EditorGUILayout.Slider("Ridge Offset", _parameters.Offset, -2f, 2f);
            _parameters.RidgeSmooth = EditorGUILayout.Slider("Ridge Smooth", _parameters.RidgeSmooth, 0f, 1f);
            _parameters.FirstOctaveValue = EditorGUILayout.Slider("First Octave Value", _parameters.FirstOctaveValue, 0f, 5f);
            _parameters.Strength01 = EditorGUILayout.Slider("Strength 01", _parameters.Strength01, 0f, 1f);
            _parameters.Frequency = EditorGUILayout.Slider("Frequency Multiplier", _parameters.Frequency, 0.01f, 10f);

            if (EditorGUI.EndChangeCheck())
            {
                GeneratePreview();
            }

            EditorGUILayout.Space();

            if (_previewTexture != null)
            {
                float size = Mathf.Min(position.width - 20, position.height - 350);
                if (size > 50)
                {
                    Rect rect = GUILayoutUtility.GetRect(size, size, GUILayout.ExpandWidth(false), GUILayout.ExpandHeight(false));
                    rect.x = (position.width - size) * 0.5f;
                    EditorGUI.DrawPreviewTexture(rect, _previewTexture);
                }
            }
            else
            {
                GUILayout.Label("Preview not available.");
            }
        }

        private void GeneratePreview()
        {
            if (_previewTexture == null || _previewTexture.width != _resolution || _previewTexture.height != _resolution)
            {
                if (_previewTexture != null)
                    DestroyImmediate(_previewTexture);
                _previewTexture = new Texture2D(_resolution, _resolution, TextureFormat.R8, false, true);
            }

            int length = _resolution * _resolution;
            NativeArray<float> heights = new NativeArray<float>(length, Allocator.TempJob, NativeArrayOptions.ClearMemory);

            var job = new SpaceEngine098RidgedMultifractalJob
            {
                InputHeights01 = heights,
                OutputHeights01 = heights,
                Width = _resolution,
                WorldOriginXZ = new double2(_offsetX, _offsetZ),
                CellSizeMeters = _zoom / _resolution,
                Parameters = _parameters,
                Seed = _seed
            };

            JobHandle handle = job.Schedule(length, 64);
            handle.Complete();

            Color32[] pixels = new Color32[length];
            for (int i = 0; i < length; i++)
            {
                byte c = (byte)(math.saturate(heights[i]) * 255f);
                pixels[i] = new Color32(c, c, c, 255);
            }

            heights.Dispose();

            _previewTexture.SetPixels32(pixels);
            _previewTexture.Apply();
            Repaint();
        }

        private void OnDestroy()
        {
            if (_previewTexture != null)
                DestroyImmediate(_previewTexture);
        }
    }
}
