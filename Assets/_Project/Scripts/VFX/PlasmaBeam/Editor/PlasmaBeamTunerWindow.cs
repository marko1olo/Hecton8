#if UNITY_EDITOR
using Hecton8.VFX.PlasmaBeam;
using Unity.Collections;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

namespace Hecton8.VFX.PlasmaBeam.Editor
{
    public sealed class PlasmaBeamTunerWindow : EditorWindow
    {
        private float _radius = 0.045f;
        private float _noiseFrequency = 5.5f;
        private float _noiseAmplitude = 0.028f;
        private int _radialSegments;
        private bool _drawMesh = true;

        [MenuItem("Hecton8/VFX/Plasma Beam Tuner")]
        private static void Open()
        {
            GetWindow<PlasmaBeamTunerWindow>("Plasma Beam Tuner");
        }

        private void OnEnable()
        {
            SceneView.duringSceneGui -= OnDrawGizmos;
            SceneView.duringSceneGui += OnDrawGizmos;
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= OnDrawGizmos;
        }

        private void OnGUI()
        {
            if (ShinobuPlasmaBeamRuntime.TryReadEditorTuning(
                    out float runtimeRadius,
                    out float runtimeNoiseFrequency,
                    out float runtimeNoiseAmplitude,
                    out uint runtimeRadialSegments,
                    out int activeBeams,
                    out int verticesGenerated,
                    out float qualityWeight))
            {
                if (!GUI.changed)
                {
                    _radius = runtimeRadius;
                    _noiseFrequency = runtimeNoiseFrequency;
                    _noiseAmplitude = runtimeNoiseAmplitude;
                    _radialSegments = (int)runtimeRadialSegments;
                }
            }
            else
            {
                activeBeams = 0;
                verticesGenerated = 0;
                qualityWeight = 1.0f;
            }

            EditorGUI.BeginChangeCheck();
            _radius = EditorGUILayout.Slider("Radius", _radius, 0.002f, 0.4f);
            _noiseFrequency = EditorGUILayout.Slider("Noise Frequency", _noiseFrequency, 0.0f, 48.0f);
            _noiseAmplitude = EditorGUILayout.Slider("Noise Amplitude", _noiseAmplitude, 0.0f, 0.35f);
            _radialSegments = EditorGUILayout.IntSlider("Radial Segments", _radialSegments, 0, ShinobuPlasmaBeamRuntime.MaxRadialSegments);
            _drawMesh = EditorGUILayout.Toggle("Draw Mesh", _drawMesh);
            if (EditorGUI.EndChangeCheck())
            {
                uint forcedRadial = _radialSegments > 0 ? (uint)math.clamp(_radialSegments, ShinobuPlasmaBeamRuntime.MinRadialSegments, ShinobuPlasmaBeamRuntime.MaxRadialSegments) : 0u;
                ShinobuPlasmaBeamRuntime.TryWriteEditorTuning(_radius, _noiseFrequency, _noiseAmplitude, forcedRadial);
                SceneView.RepaintAll();
            }

            EditorGUILayout.Space(6.0f);
            EditorGUILayout.LabelField("Active Beams", activeBeams.ToString());
            EditorGUILayout.LabelField("Vertices", verticesGenerated.ToString());
            EditorGUILayout.LabelField("Quality Weight", qualityWeight.ToString("0.000"));
        }

        private void OnDrawGizmos(SceneView sceneView)
        {
            if (!_drawMesh || Event.current == null || Event.current.type != EventType.Repaint)
                return;

            if (!ShinobuPlasmaBeamRuntime.TryGetEditorMeshSnapshot(out NativeArray<BeamVertexDTO> vertices, out int vertexCount, out int activeBeams))
                return;

            int safeCount = math.min(vertexCount, vertices.IsCreated ? vertices.Length : 0);
            int triangleVertexCount = safeCount - safeCount % 3;
            int maxTriangleVertexCount = math.min(triangleVertexCount, 1536);
            if (maxTriangleVertexCount <= 0 || activeBeams <= 0)
                return;

            Handles.color = new Color(0.18f, 0.92f, 1.0f, 0.72f);
            for (int i = 0; i < maxTriangleVertexCount; i += 3)
            {
                Vector3 a = ToVector3(vertices[i].Position);
                Vector3 b = ToVector3(vertices[i + 1].Position);
                Vector3 c = ToVector3(vertices[i + 2].Position);
                Handles.DrawLine(a, b);
                Handles.DrawLine(b, c);
                Handles.DrawLine(c, a);
            }
        }

        private static Vector3 ToVector3(float3 value)
        {
            return new Vector3(value.x, value.y, value.z);
        }
    }
}
#endif
