#if UNITY_EDITOR
using Hecton8.AI;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Hecton8.EditorTools
{
    public sealed class MesofaunaAiTunerWindow : EditorWindow
    {
        private const int GizmoCapacity = 128;
        // COLD ALLOC: Vector3[GizmoCapacity] - editor-only mesofauna gizmo origins - owner: MesofaunaAiTunerWindow
        private static readonly Vector3[] Origins = new Vector3[GizmoCapacity];
        // COLD ALLOC: Vector3[GizmoCapacity] - editor-only mesofauna desired velocity staging - owner: MesofaunaAiTunerWindow
        private static readonly Vector3[] DesiredVelocities = new Vector3[GizmoCapacity];
        // COLD ALLOC: Vector3[GizmoCapacity] - editor-only mesofauna target vector staging - owner: MesofaunaAiTunerWindow
        private static readonly Vector3[] TargetVectors = new Vector3[GizmoCapacity];
        // COLD ALLOC: byte[GizmoCapacity] - editor-only mesofauna state staging - owner: MesofaunaAiTunerWindow
        private static readonly byte[] States = new byte[GizmoCapacity];
        // COLD ALLOC: uint[GizmoCapacity] - editor-only mesofauna target hash staging - owner: MesofaunaAiTunerWindow
        private static readonly uint[] TargetHashes = new uint[GizmoCapacity];

        private Slider _visionLow;
        private Slider _visionUltra;
        private Slider _scent;
        private Slider _baseSpeed;
        private Slider _timeout;
        private Toggle _drawGizmos;
        private Label _status;
        private Label _telemetry;
        private StatePieElement _pie;
        private bool _suppressCallbacks;

        [MenuItem("Hecton8/AI/Mesofauna AI Tuner")]
        private static void Open()
        {
            GetWindow<MesofaunaAiTunerWindow>("Mesofauna AI");
        }

        public void CreateGUI()
        {
            VisualElement root = rootVisualElement;
            root.style.paddingLeft = 8;
            root.style.paddingRight = 8;
            root.style.paddingTop = 8;
            root.style.paddingBottom = 8;

            _status = new Label("Vault not sampled.");
            _telemetry = new Label("Telemetry unavailable.");
            _pie = new StatePieElement();
            _pie.style.height = 120;
            _pie.style.marginTop = 6;
            _pie.style.marginBottom = 6;

            _visionLow = CreateSlider("Vision Radius Low", 4f, 160f);
            _visionUltra = CreateSlider("Vision Radius Ultra", 4f, 220f);
            _scent = CreateSlider("Scent Sensitivity", 0.05f, 4f);
            _baseSpeed = CreateSlider("Base Speed", 0.5f, 30f);
            _timeout = CreateSlider("State Timeout", 0.1f, 60f);
            _drawGizmos = new Toggle("Draw FSM Gizmos");

            root.Add(_status);
            root.Add(_telemetry);
            root.Add(_pie);
            root.Add(_visionLow);
            root.Add(_visionUltra);
            root.Add(_scent);
            root.Add(_baseSpeed);
            root.Add(_timeout);
            root.Add(_drawGizmos);
            root.Add(new Button(RefreshFromVault) { text = "Refresh" });
            root.Add(new Button(ReloadSpeciesCsv) { text = "Reload mesofauna_species_profiles.csv" });

            RegisterCallbacks();
            SceneView.duringSceneGui -= DrawSceneIntent;
            SceneView.duringSceneGui += DrawSceneIntent;
            RefreshFromVault();
            root.schedule.Execute(RefreshFromVault).Every(500);
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= DrawSceneIntent;
        }

        private static Slider CreateSlider(string label, float low, float high)
        {
            return new Slider(label, low, high)
            {
                showInputField = true
            };
        }

        private void RegisterCallbacks()
        {
            _visionLow.RegisterValueChangedCallback(_ => ApplyTuning());
            _visionUltra.RegisterValueChangedCallback(_ => ApplyTuning());
            _scent.RegisterValueChangedCallback(_ => ApplyTuning());
            _baseSpeed.RegisterValueChangedCallback(_ => ApplyTuning());
            _timeout.RegisterValueChangedCallback(_ => ApplyTuning());
        }

        private void RefreshFromVault()
        {
            if (PredatorCognitionDomain.TryGetMesofaunaTuning(out MesofaunaTuningDTO tuning))
            {
                _suppressCallbacks = true;
                _visionLow.SetValueWithoutNotify(tuning.VisionRadiusLow);
                _visionUltra.SetValueWithoutNotify(tuning.VisionRadiusUltra);
                _scent.SetValueWithoutNotify(tuning.ScentSensitivity);
                _baseSpeed.SetValueWithoutNotify(tuning.BaseSpeedMetersPerSecond);
                _timeout.SetValueWithoutNotify(tuning.StateTimeoutSeconds);
                _suppressCallbacks = false;
                _status.text = "Vault tuning sampled.";
            }
            else
            {
                _status.text = "Mesofauna Vault unavailable.";
            }

            if (PredatorCognitionDomain.TryGetMesofaunaTelemetrySnapshot(out MesofaunaTelemetryEntry entry))
            {
                int active = entry.ActivePredators;
                int hunt = math.min(entry.HuntingPredators, active);
                int flee = math.min(entry.FleeingPredators, math.max(0, active - hunt));
                int idle = math.max(0, active - hunt - flee);
                _pie.SetCounts(idle, hunt, flee);
                _telemetry.text = "Idle " + idle + "  Hunt " + hunt + "  Flee " + flee + "  Slice " + entry.SliceModulo + "  Q " + entry.GlobalQualityWeight.ToString("0.00");
            }
            else
            {
                _pie.SetCounts(0, 0, 0);
                _telemetry.text = "Telemetry unavailable.";
            }

            Repaint();
        }

        private void ApplyTuning()
        {
            if (_suppressCallbacks)
                return;

            MesofaunaTuningDTO tuning = default;
            tuning.VisionRadiusLow = _visionLow.value;
            tuning.VisionRadiusUltra = math.max(_visionUltra.value, _visionLow.value);
            tuning.ScentSensitivity = _scent.value;
            tuning.BaseSpeedMetersPerSecond = _baseSpeed.value;
            tuning.IdleToSearchTicks = 8;
            tuning.SearchToIdleTicks = 120;
            tuning.StateTimeoutSeconds = _timeout.value;
            tuning.Flags = 1u;
            _status.text = PredatorCognitionDomain.TrySetMesofaunaTuning(in tuning)
                ? "Vault tuning updated."
                : "Mesofauna Vault unavailable.";
        }

        private void ReloadSpeciesCsv()
        {
            bool loaded = PredatorCognitionDomain.TryReloadMesofaunaSpeciesProfiles();
            if (loaded && PredatorCognitionDomain.TryGetMesofaunaSpeciesProfileCount(out int count))
                _status.text = "Species CSV loaded. Profiles " + count;
            else
                _status.text = "Species CSV missing or empty.";
        }

        private void DrawSceneIntent(SceneView sceneView)
        {
            if (_drawGizmos == null || !_drawGizmos.value || !EditorApplication.isPlaying)
                return;

            int count = PredatorCognitionDomain.CopyMesofaunaDebugGizmos(
                Origins,
                DesiredVelocities,
                TargetVectors,
                States,
                TargetHashes,
                GizmoCapacity);
            for (int i = 0; i < count; i++)
            {
                Vector3 origin = Origins[i];
                Handles.color = ResolveStateColor(States[i]);
                Handles.DrawLine(origin, origin + DesiredVelocities[i]);
                Handles.DrawWireDisc(origin, Vector3.up, 0.5f);
                Handles.color = Color.white;
                Handles.DrawLine(origin, origin + Vector3.ClampMagnitude(TargetVectors[i], 12f));
            }
        }

        private static Color ResolveStateColor(byte state)
        {
            switch (state)
            {
                case MesofaunaBehaviorConstants.StateHunt:
                    return Color.red;
                case MesofaunaBehaviorConstants.StateFlee:
                    return Color.cyan;
                case MesofaunaBehaviorConstants.StateTrackScent:
                    return Color.green;
                case MesofaunaBehaviorConstants.StateIdle:
                    return Color.blue;
                default:
                    return Color.yellow;
            }
        }

        private sealed class StatePieElement : VisualElement
        {
            private int _idle;
            private int _hunt;
            private int _flee;

            public StatePieElement()
            {
                generateVisualContent += Draw;
            }

            public void SetCounts(int idle, int hunt, int flee)
            {
                _idle = math.max(0, idle);
                _hunt = math.max(0, hunt);
                _flee = math.max(0, flee);
                MarkDirtyRepaint();
            }

            private void Draw(MeshGenerationContext context)
            {
                Rect rect = contentRect;
                if (rect.width <= 1f || rect.height <= 1f)
                    return;

                int total = math.max(1, _idle + _hunt + _flee);
                Vector2 center = rect.center;
                float radius = math.max(8f, math.min(rect.width, rect.height) * 0.45f);
                float angle = -math.PI * 0.5f;
                angle = DrawSlice(context, center, radius, angle, _idle / (float)total, new Color(0.12f, 0.32f, 0.95f, 1f));
                angle = DrawSlice(context, center, radius, angle, _hunt / (float)total, new Color(0.9f, 0.08f, 0.04f, 1f));
                DrawSlice(context, center, radius, angle, _flee / (float)total, new Color(0.05f, 0.85f, 0.65f, 1f));
            }

            private static float DrawSlice(MeshGenerationContext context, Vector2 center, float radius, float startAngle, float fraction, Color color)
            {
                if (fraction <= 0f)
                    return startAngle;

                int segments = math.clamp((int)math.ceil(fraction * 40f), 2, 40);
                MeshWriteData mesh = context.Allocate(segments + 2, segments * 3);
                Vertex centerVertex = default;
                centerVertex.position = new Vector3(center.x, center.y, 0f);
                centerVertex.tint = color;
                centerVertex.uv = Vector2.zero;
                mesh.SetNextVertex(centerVertex);

                float endAngle = startAngle + (fraction * math.PI * 2f);
                for (int i = 0; i <= segments; i++)
                {
                    float t = i / (float)segments;
                    float angle = math.lerp(startAngle, endAngle, t);
                    Vertex vertex = default;
                    vertex.position = new Vector3(center.x + math.cos(angle) * radius, center.y + math.sin(angle) * radius, 0f);
                    vertex.tint = color;
                    vertex.uv = Vector2.zero;
                    mesh.SetNextVertex(vertex);
                }

                for (int i = 0; i < segments; i++)
                {
                    mesh.SetNextIndex((ushort)0);
                    mesh.SetNextIndex((ushort)(i + 1));
                    mesh.SetNextIndex((ushort)(i + 2));
                }

                return endAngle;
            }
        }
    }
}
#endif
