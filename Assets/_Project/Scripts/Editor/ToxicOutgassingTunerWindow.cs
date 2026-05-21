#if UNITY_EDITOR
using Unity.Collections;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

namespace Hecton8.Atmosphere
{
    public sealed class ToxicOutgassingTunerWindow : EditorWindow
    {
        private ToxicOutgassingChemistryRuntime _runtime;
        private bool _drawPlume = true;
        private float _densityDrawThreshold = 0.08f;
        private int _maxWireCells = 512;

        [MenuItem("Hecton8/Atmosphere/Toxic Outgassing Tuner")]
        private static void Open()
        {
            ToxicOutgassingTunerWindow window = GetWindow<ToxicOutgassingTunerWindow>();
            window.titleContent = new GUIContent("Toxic Outgassing Tuner");
            window.Show();
        }

        private void OnEnable()
        {
            RefreshRuntime();
            SceneView.duringSceneGui -= OnDrawGizmos;
            SceneView.duringSceneGui += OnDrawGizmos;
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= OnDrawGizmos;
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Toxic Outgassing Tuner", EditorStyles.boldLabel);
            _runtime = (ToxicOutgassingChemistryRuntime)EditorGUILayout.ObjectField("Runtime", _runtime, typeof(ToxicOutgassingChemistryRuntime), true);
            if (_runtime == null)
            {
                if (GUILayout.Button("Find Runtime"))
                {
                    RefreshRuntime();
                }

                return;
            }

            EditorGUILayout.LabelField("Resolution", _runtime.ActiveResolution.ToString());
            EditorGUILayout.LabelField("Sources", _runtime.SourceCount.ToString());
            EditorGUILayout.LabelField("Entities", _runtime.EntityCount.ToString());
            EditorGUILayout.LabelField("Density Version", _runtime.DensityVersion.ToString());

            if (!_runtime.TryReadConstants(out ToxicOutgassingConstants constants))
            {
                EditorGUILayout.HelpBox("Constants Vault lane is unavailable.", MessageType.Warning);
                return;
            }

            EditorGUI.BeginChangeCheck();
            constants.BaseDiffusionRate = EditorGUILayout.Slider("Base Diffusion Rate", constants.BaseDiffusionRate, 0f, 2f);
            constants.CurrentAdvectionMultiplier = EditorGUILayout.Slider("Current Advection Multiplier", constants.CurrentAdvectionMultiplier, 0f, 4f);
            constants.AcidCorrosionDamage = EditorGUILayout.Slider("Acid Corrosion Damage", constants.AcidCorrosionDamage, 0f, 1f);
            constants.FloraAbsorptionRate = EditorGUILayout.Slider("Flora Absorption Rate", constants.FloraAbsorptionRate, 0f, 1f);
            constants.SourceRadiusMeters = EditorGUILayout.Slider("Source Radius Meters", constants.SourceRadiusMeters, 1f, 200f);
            constants.CausticDensityThreshold = EditorGUILayout.Slider("Caustic Density Threshold", constants.CausticDensityThreshold, 0.01f, 1f);
            constants.BiolumDensityThreshold = EditorGUILayout.Slider("Biolum Density Threshold", constants.BiolumDensityThreshold, 0.01f, 1f);
            constants.MaxDensity = EditorGUILayout.Slider("Max Density", constants.MaxDensity, 0.1f, 8f);
            _drawPlume = EditorGUILayout.Toggle("Draw Plume Grid", _drawPlume);
            _densityDrawThreshold = EditorGUILayout.Slider("Draw Density Threshold", _densityDrawThreshold, 0f, 1f);
            _maxWireCells = EditorGUILayout.IntSlider("Max Wire Cells", _maxWireCells, 32, 2048);
            if (EditorGUI.EndChangeCheck())
            {
                _runtime.TryWriteConstants(in constants);
                SceneView.RepaintAll();
            }

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Reload CSV"))
            {
                _runtime.TryReloadCsvOverrides();
                SceneView.RepaintAll();
            }

            if (GUILayout.Button("Emergency Mock"))
            {
                _runtime.GenerateEmergencyMockChemistry();
                SceneView.RepaintAll();
            }
            EditorGUILayout.EndHorizontal();
        }

        private void RefreshRuntime()
        {
            _runtime = ToxicOutgassingChemistryRuntime.Instance;
            if (_runtime == null)
            {
                _runtime = FindObjectOfType<ToxicOutgassingChemistryRuntime>();
            }
        }

        private void OnDrawGizmos(SceneView sceneView)
        {
            if (!_drawPlume)
            {
                return;
            }

            if (_runtime == null)
            {
                RefreshRuntime();
                if (_runtime == null)
                {
                    return;
                }
            }

            if (!_runtime.TryGetGridReadback(out NativeArray<float> density, out int resolution, out double3 originAup, out float cellSize, out int version))
            {
                return;
            }

            if (!density.IsCreated || resolution <= 0 || cellSize <= 0f)
            {
                return;
            }

            Vector3 basePosition = _runtime.transform.position;
            float extent = resolution * cellSize;
            Handles.color = new Color(0.1f, 0.75f, 0.25f, 0.18f);
            Handles.DrawWireCube(basePosition, Vector3.one * extent);

            int stride = math.max(1, (int)math.ceil(math.pow((resolution * resolution * resolution) / math.max(1f, _maxWireCells), 1f / 3f)));
            int drawn = 0;
            for (int z = 0; z < resolution && drawn < _maxWireCells; z += stride)
            {
                for (int y = 0; y < resolution && drawn < _maxWireCells; y += stride)
                {
                    for (int x = 0; x < resolution && drawn < _maxWireCells; x += stride)
                    {
                        int index = x + resolution * (y + resolution * z);
                        float value = density[index];
                        if (!math.isfinite(value) || value < _densityDrawThreshold)
                        {
                            continue;
                        }

                        float intensity = math.saturate(value);
                        Handles.color = Color.Lerp(new Color(0.95f, 0.95f, 0.1f, 0.25f), new Color(0.0f, 1.0f, 0.18f, 0.75f), intensity);
                        Vector3 local = (new Vector3(x, y, z) - Vector3.one * (resolution * 0.5f)) * cellSize;
                        Handles.DrawWireCube(basePosition + local, Vector3.one * cellSize);
                        drawn++;
                    }
                }
            }
        }
    }
}
#endif
