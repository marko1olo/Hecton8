using UnityEditor;
using UnityEngine;
using Unity.Mathematics;
using Hecton8.World;

namespace Hecton8.Editor.Generators
{
    public class SpaceEngineTerrainPreviewWindow : EditorWindow
    {
        private Texture2D _previewTexture;
        private int _resolution = 256;
        private float _scale = 0.01f;
        private uint _seed = 1337;

        // Fractal Parameters
        private int _octaves = 8;
        private float _gain = 0.5f;
        private float _warp = 0.1f;
        private float _firstOctaveValue = 1f;
        private float _lacunarity = SpaceEngineNoise098.DefaultLacunarity;
        private float _h = SpaceEngineNoise098.DefaultH;
        private float _offset = SpaceEngineNoise098.DefaultOffset;
        private float _ridgeSmooth = SpaceEngineNoise098.DefaultRidgeSmooth;
        private float _strength01 = 1f;
        private float _frequency = 1f;

        // Crater Parameters
        private bool _enableCraters = false;
        private float _craterRadius = 0.5f;
        private SpaceEngine098CraterProfile _craterProfile = SpaceEngine098CraterProfile.OldDefault();

        [MenuItem("Window/Hecton-8/SpaceEngine 0.9.8 Preview")]
        public static void ShowWindow()
        {
            var window = GetWindow<SpaceEngineTerrainPreviewWindow>("SpaceEngine 0.9.8 Preview");
            window.minSize = new Vector2(400, 600);
        }

        private void OnGUI()
        {
            GUILayout.Label("SpaceEngine Ridged Multifractal Preview", EditorStyles.boldLabel);

            _resolution = EditorGUILayout.IntSlider("Resolution", _resolution, 64, 512);
            _scale = EditorGUILayout.Slider("View Scale", _scale, 0.001f, 0.5f);
            _seed = (uint)EditorGUILayout.IntField("Seed", (int)_seed);

            GUILayout.Space(10);
            GUILayout.Label("Fractal Parameters", EditorStyles.boldLabel);

            _octaves = EditorGUILayout.IntSlider("Octaves", _octaves, 1, 12);
            _gain = EditorGUILayout.Slider("Gain", _gain, 0f, 1f);
            _warp = EditorGUILayout.Slider("Warp", _warp, 0f, 2f);
            _firstOctaveValue = EditorGUILayout.Slider("First Octave", _firstOctaveValue, 0f, 2f);
            _lacunarity = EditorGUILayout.Slider("Lacunarity", _lacunarity, 1.0001f, 4f);
            _h = EditorGUILayout.Slider("H (Fractal Dim)", _h, 0.0001f, 1f);
            _offset = EditorGUILayout.Slider("Offset", _offset, -1f, 1f);
            _ridgeSmooth = EditorGUILayout.Slider("Ridge Smooth", _ridgeSmooth, 0.0001f, 1f);
            _strength01 = EditorGUILayout.Slider("Strength", _strength01, 0f, 1f);
            _frequency = EditorGUILayout.Slider("Frequency", _frequency, 0.1f, 10f);

            GUILayout.Space(10);
            _enableCraters = EditorGUILayout.BeginToggleGroup("Enable Demo Crater", _enableCraters);
            _craterRadius = EditorGUILayout.Slider("Demo Crater Radius", _craterRadius, 0.01f, 2f);
            _craterProfile.HeightPeak = EditorGUILayout.Slider("Peak Height", _craterProfile.HeightPeak, 0f, 2f);
            _craterProfile.HeightRim = EditorGUILayout.Slider("Rim Height", _craterProfile.HeightRim, 0f, 2f);
            _craterProfile.HeightFloor = EditorGUILayout.Slider("Floor Height", _craterProfile.HeightFloor, -1f, 0f);
            EditorGUILayout.EndToggleGroup();

            GUILayout.Space(10);
            if (GUILayout.Button("Generate Preview", GUILayout.Height(30)))
            {
                GeneratePreview();
            }

            if (_previewTexture != null)
            {
                GUILayout.Space(10);
                Rect rect = GUILayoutUtility.GetAspectRect(1f);
                GUI.DrawTexture(rect, _previewTexture, ScaleMode.ScaleToFit);
            }
        }

        private void GeneratePreview()
        {
            if (_previewTexture == null || _previewTexture.width != _resolution || _previewTexture.height != _resolution)
            {
                _previewTexture = new Texture2D(_resolution, _resolution, TextureFormat.RGBA32, false);
            }

            Color[] pixels = new Color[_resolution * _resolution];

            var param = new SpaceEngine098RidgedMultifractalParams
            {
                Frequency = _frequency,
                Strength01 = _strength01,
                Gain = _gain,
                Warp = _warp,
                FirstOctaveValue = _firstOctaveValue,
                Lacunarity = _lacunarity,
                H = _h,
                Offset = _offset,
                RidgeSmooth = _ridgeSmooth,
                Octaves = _octaves
            };

            float minH = float.MaxValue;
            float maxH = float.MinValue;
            float[] heights = new float[_resolution * _resolution];

            // Center pixel for demo crater
            float2 center = new float2(_resolution * 0.5f * _scale, _resolution * 0.5f * _scale);

            for (int y = 0; y < _resolution; y++)
            {
                for (int x = 0; x < _resolution; x++)
                {
                    float worldX = x * _scale;
                    float worldY = y * _scale;
                    float3 point = new float3(worldX, 0, worldY) * param.Frequency;

                    float height = SpaceEngineNoise098.RidgedMultifractalErodedDetail(point, in param, _seed);

                    if (_enableCraters)
                    {
                        float r = SpaceEngineNoise098.Cell2F1F2(new float2(worldX, worldY) - center, _seed).x / _craterRadius;
                        if (r < 1f)
                        {
                            height += SpaceEngineNoise098.CraterHeightFuncSE(0f, 0f, 1f, r, in _craterProfile);
                        }
                    }

                    heights[y * _resolution + x] = height;

                    if (height < minH) minH = height;
                    if (height > maxH) maxH = height;
                }
            }

            // Normalize and apply colors
            float range = math.max(0.001f, maxH - minH);
            for (int i = 0; i < heights.Length; i++)
            {
                float normalized = (heights[i] - minH) / range;
                pixels[i] = new Color(normalized, normalized, normalized, 1f);
            }

            _previewTexture.SetPixels(pixels);
            _previewTexture.Apply();
        }
    }
}
