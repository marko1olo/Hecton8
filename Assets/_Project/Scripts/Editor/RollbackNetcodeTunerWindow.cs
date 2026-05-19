using Hecton8.Networking;
using Unity.Mathematics;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Hecton8.Editor
{
    public sealed class RollbackNetcodeTunerWindow : EditorWindow
    {
        private static readonly Color TrueMathColor = new Color(1f, 0.12f, 0.08f, 0.9f);
        private static readonly Color InterpolatedColor = new Color(0.05f, 1f, 0.32f, 0.9f);
        private static readonly Color DivergenceColor = new Color(0.45f, 0.04f, 0.03f, 1f);
        private static readonly Color NormalColor = new Color(0.08f, 0.09f, 0.1f, 1f);

        private readonly ulong[] _hashHistory = new ulong[128];
        private Label _frameLabel;
        private Label _hashLabel;
        private Label _remoteHashLabel;
        private Label _flagsLabel;
        private Label _resimLabel;
        private Label _packetLabel;
        private RollbackHashGraphElement _graph;
        private int _hashCursor;

        [MenuItem("Hecton8/Networking/Rollback Netcode Tuner")]
        private static void Open()
        {
            RollbackNetcodeTunerWindow window = GetWindow<RollbackNetcodeTunerWindow>();
            window.titleContent = new GUIContent("Rollback Netcode Tuner");
            window.Show();
        }

        public void CreateGUI()
        {
            rootVisualElement.style.backgroundColor = NormalColor;
            rootVisualElement.style.paddingLeft = 8f;
            rootVisualElement.style.paddingRight = 8f;
            rootVisualElement.style.paddingTop = 8f;
            rootVisualElement.style.paddingBottom = 8f;

            SliderInt rollback = CreateIntSlider("Max rollback", 1, RollbackNetcodeConstants.MaxRollbackFrames);
            SliderInt latency = CreateIntSlider("Latency frames", 0, 30);
            SliderInt loss = CreateIntSlider("Loss permille", 0, 1000);
            SliderInt duplicate = CreateIntSlider("Dup permille", 0, 1000);
            SliderInt redundancy = CreateIntSlider("Redundancy", 1, 4);
            Slider visualSeconds = CreateSlider("Visual seconds", 0.016f, 0.25f);
            Slider inputPrediction = CreateSlider("Prediction", 0f, 1f);
            Slider lookQuality = CreateSlider("Look quality", 0f, 1f);

            rollback.RegisterValueChangedCallback(_ => PushTuningFromControls(rollback, latency, loss, duplicate, redundancy, visualSeconds, inputPrediction, lookQuality));
            latency.RegisterValueChangedCallback(_ => PushTuningFromControls(rollback, latency, loss, duplicate, redundancy, visualSeconds, inputPrediction, lookQuality));
            loss.RegisterValueChangedCallback(_ => PushTuningFromControls(rollback, latency, loss, duplicate, redundancy, visualSeconds, inputPrediction, lookQuality));
            duplicate.RegisterValueChangedCallback(_ => PushTuningFromControls(rollback, latency, loss, duplicate, redundancy, visualSeconds, inputPrediction, lookQuality));
            redundancy.RegisterValueChangedCallback(_ => PushTuningFromControls(rollback, latency, loss, duplicate, redundancy, visualSeconds, inputPrediction, lookQuality));
            visualSeconds.RegisterValueChangedCallback(_ => PushTuningFromControls(rollback, latency, loss, duplicate, redundancy, visualSeconds, inputPrediction, lookQuality));
            inputPrediction.RegisterValueChangedCallback(_ => PushTuningFromControls(rollback, latency, loss, duplicate, redundancy, visualSeconds, inputPrediction, lookQuality));
            lookQuality.RegisterValueChangedCallback(_ => PushTuningFromControls(rollback, latency, loss, duplicate, redundancy, visualSeconds, inputPrediction, lookQuality));

            Button pingButton = new Button(() => HectonRollbackNetcodeRuntime.Simulate200MsPing()) { text = "Simulate 200ms ping" };
            Button divergenceButton = new Button(InjectDivergence) { text = "Inject divergence" };
            VisualElement buttonRow = new VisualElement { style = { flexDirection = FlexDirection.Row } };
            buttonRow.Add(pingButton);
            buttonRow.Add(divergenceButton);

            _frameLabel = new Label("Frame: -");
            _hashLabel = new Label("Hash: -");
            _remoteHashLabel = new Label("Remote: -");
            _flagsLabel = new Label("Flags: -");
            _resimLabel = new Label("Resim: -");
            _packetLabel = new Label("Packets: -");
            _graph = new RollbackHashGraphElement(_hashHistory);
            _graph.style.height = 92f;
            _graph.style.marginTop = 8f;

            rootVisualElement.Add(rollback);
            rootVisualElement.Add(latency);
            rootVisualElement.Add(loss);
            rootVisualElement.Add(duplicate);
            rootVisualElement.Add(redundancy);
            rootVisualElement.Add(visualSeconds);
            rootVisualElement.Add(inputPrediction);
            rootVisualElement.Add(lookQuality);
            rootVisualElement.Add(buttonRow);
            rootVisualElement.Add(_frameLabel);
            rootVisualElement.Add(_hashLabel);
            rootVisualElement.Add(_remoteHashLabel);
            rootVisualElement.Add(_flagsLabel);
            rootVisualElement.Add(_resimLabel);
            rootVisualElement.Add(_packetLabel);
            rootVisualElement.Add(_graph);

            PullTuningIntoControls(rollback, latency, loss, duplicate, redundancy, visualSeconds, inputPrediction, lookQuality);
        }

        private void OnEnable()
        {
            EditorApplication.update += EditorTick;
            SceneView.duringSceneGui += DrawSceneGizmos;
        }

        private void OnDisable()
        {
            EditorApplication.update -= EditorTick;
            SceneView.duringSceneGui -= DrawSceneGizmos;
        }

        private void EditorTick()
        {
            if (_frameLabel == null || _graph == null)
                return;

            if (HectonRollbackNetcodeRuntime.TryGetRuntimeState(out RollbackRuntimeStateDTO state))
            {
                _frameLabel.text = "Frame: " + state.CurrentFrame;
                _hashLabel.text = "Hash: " + state.LastFrameHash64.ToString("X16");
                _remoteHashLabel.text = "Remote: " + state.LastRemoteHash64.ToString("X16");
                _flagsLabel.text = "Flags: " + state.Flags.ToString("X8");
                _resimLabel.text = "Resim: " + state.ResimComputeTimeMs.ToString("0.000") + " ms / " + state.FramesResimulated + " ticks";
                bool divergent = (state.Flags & (RollbackNetcodeFlags.HashMismatch | RollbackNetcodeFlags.HardResyncRequired)) != 0u;
                rootVisualElement.style.backgroundColor = divergent ? DivergenceColor : NormalColor;
                _hashHistory[_hashCursor++ & 127] = state.LastFrameHash64;
                _graph.MarkDirtyRepaint();
            }

            if (HectonRollbackNetcodeRuntime.TryGetTelemetry(out Unity.Collections.NativeArray<NetTelemetryEntry64> telemetry) && telemetry.IsCreated && telemetry.Length > 0)
            {
                NetTelemetryEntry64 entry = telemetry[(int)(math.max(0, _hashCursor - 1) % telemetry.Length)];
                _packetLabel.text = "Packets: drop " + entry.DroppedPackets + " / dup " + entry.DuplicatedPackets;
            }
        }

        private static SliderInt CreateIntSlider(string label, int low, int high)
        {
            SliderInt slider = new SliderInt(label, low, high);
            slider.showInputField = true;
            return slider;
        }

        private static Slider CreateSlider(string label, float low, float high)
        {
            Slider slider = new Slider(label, low, high);
            slider.showInputField = true;
            return slider;
        }

        private static void PullTuningIntoControls(
            SliderInt rollback,
            SliderInt latency,
            SliderInt loss,
            SliderInt duplicate,
            SliderInt redundancy,
            Slider visualSeconds,
            Slider inputPrediction,
            Slider lookQuality)
        {
            if (!HectonRollbackNetcodeRuntime.TryGetTuning(out RollbackTuningDTO tuning))
                return;

            rollback.SetValueWithoutNotify(tuning.MaxRollbackFrames);
            latency.SetValueWithoutNotify((int)tuning.InputDelayFrames);
            loss.SetValueWithoutNotify((int)tuning.PacketLossPermille);
            duplicate.SetValueWithoutNotify((int)tuning.DuplicatePermille);
            redundancy.SetValueWithoutNotify((int)tuning.RedundancyCount);
            visualSeconds.SetValueWithoutNotify(tuning.VisualInterpolationSeconds);
            inputPrediction.SetValueWithoutNotify(tuning.InputPredictionAggressiveness);
            lookQuality.SetValueWithoutNotify(tuning.MinQualityForLookRollback);
        }

        private static void PushTuningFromControls(
            SliderInt rollback,
            SliderInt latency,
            SliderInt loss,
            SliderInt duplicate,
            SliderInt redundancy,
            Slider visualSeconds,
            Slider inputPrediction,
            Slider lookQuality)
        {
            if (!HectonRollbackNetcodeRuntime.TryGetTuning(out RollbackTuningDTO tuning))
                return;

            tuning.MaxRollbackFrames = rollback.value;
            tuning.InputDelayFrames = (uint)latency.value;
            tuning.PingSimulatedFrames = (uint)latency.value;
            tuning.PacketLossPermille = (uint)loss.value;
            tuning.DuplicatePermille = (uint)duplicate.value;
            tuning.RedundancyCount = (uint)math.max(1, redundancy.value);
            tuning.VisualInterpolationSeconds = visualSeconds.value;
            tuning.InputPredictionAggressiveness = inputPrediction.value;
            tuning.MinQualityForLookRollback = lookQuality.value;
            HectonRollbackNetcodeRuntime.TrySetTuning(tuning);
            HectonRollbackNetcodeRuntime.TrySetMockJitter(tuning.InputDelayFrames, tuning.PacketLossPermille, tuning.DuplicatePermille);
        }

        private static void InjectDivergence()
        {
            if (!HectonRollbackNetcodeRuntime.TryGetRuntimeState(out RollbackRuntimeStateDTO state))
                return;

            HectonRollbackNetcodeRuntime.InjectRemoteFrameHash(state.CurrentFrame, state.LastFrameHash64 ^ 0xD15EA5E5D15EA5E5UL);
        }

        private static void DrawSceneGizmos(SceneView view)
        {
            if (!HectonRollbackNetcodeRuntime.TryGetVisualStates(out Unity.Collections.NativeArray<VisualStateDTO> states) || !states.IsCreated)
                return;

            for (int i = 0; i < states.Length; i++)
            {
                VisualStateDTO state = states[i];
                if ((state.Flags & 1u) == 0u)
                    continue;

                Vector3 truePosition = ToVector3(state.TrueLocalMeters);
                Vector3 interpolatedPosition = ToVector3(state.InterpolatedLocalMeters);

                Handles.color = TrueMathColor;
                Handles.SphereHandleCap(0, truePosition, Quaternion.identity, 0.32f, EventType.Repaint);
                Handles.color = InterpolatedColor;
                Handles.SphereHandleCap(0, interpolatedPosition, Quaternion.identity, 0.24f, EventType.Repaint);
                Handles.DrawLine(interpolatedPosition, truePosition);
            }
        }

        private static Vector3 ToVector3(float3 value)
        {
            return new Vector3(value.x, value.y, value.z);
        }

        private sealed class RollbackHashGraphElement : VisualElement
        {
            private readonly ulong[] _values;

            public RollbackHashGraphElement(ulong[] values)
            {
                _values = values;
                generateVisualContent += DrawGraph;
            }

            private void DrawGraph(MeshGenerationContext context)
            {
                Rect rect = contentRect;
                if (rect.width <= 2f || rect.height <= 2f)
                    return;

                Painter2D painter = context.painter2D;
                painter.lineWidth = 1.5f;
                painter.strokeColor = new Color(0.15f, 0.9f, 0.62f, 1f);
                painter.BeginPath();
                for (int i = 0; i < _values.Length; i++)
                {
                    float x = rect.xMin + (rect.width * i / math.max(1, _values.Length - 1));
                    float normalized = ((_values[i] >> 8) & 0xFFFF) * (1f / 65535f);
                    float y = rect.yMax - (normalized * rect.height);
                    if (i == 0)
                        painter.MoveTo(new Vector2(x, y));
                    else
                        painter.LineTo(new Vector2(x, y));
                }
                painter.Stroke();
            }
        }
    }
}
