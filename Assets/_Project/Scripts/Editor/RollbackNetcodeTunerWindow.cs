using Hecton8.Networking;
using Hecton8.Core;
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
        private Label _capacityLabel;
        private RollbackHashGraphElement _graph;
        private RollbackTelemetryStripElement _telemetryStrip;
        private int _hashCursor;
        private double _nextTextReadoutTime;
        private uint _lastTextFrame = uint.MaxValue;
        private uint _lastTextFlags = uint.MaxValue;
        private uint _lastTextResimFrames = uint.MaxValue;
        private uint _lastTextDroppedPackets = uint.MaxValue;
        private uint _lastTextDuplicatedPackets = uint.MaxValue;
        private uint _lastTextRedundancyCount = uint.MaxValue;
        private uint _lastTextDearLieCount = uint.MaxValue;
        private ulong _lastTextHash = ulong.MaxValue;
        private ulong _lastTextRemoteHash = ulong.MaxValue;
        private float _lastTextResimMs = float.NaN;
        private int _lastTextCapacity = -1;

        [MenuItem("Hecton8/Networking/Rollback Netcode Tuner")]
        private static void Open()
        {
            RollbackNetcodeTunerWindow window = GetWindow<RollbackNetcodeTunerWindow>();
            window.titleContent = new GUIContent("Cooperative Input Tuner");
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
            SliderInt redundancy = CreateIntSlider("Redundancy", 1, 5);
            SliderInt extrapolationDecay = CreateIntSlider("Extrapolation decay", 1, 2000);
            SliderInt predictionWindow = CreateIntSlider("Active buffer capacity", 5, 30);
            Slider visualSeconds = CreateSlider("Visual seconds", 0.016f, 0.25f);
            Slider inputPrediction = CreateSlider("Prediction", 0f, 1f);
            Slider lookQuality = CreateSlider("Look severity", 0f, 1f);

            rollback.RegisterValueChangedCallback(_ => PushTuningFromControls(rollback, latency, loss, duplicate, redundancy, extrapolationDecay, predictionWindow, visualSeconds, inputPrediction, lookQuality));
            latency.RegisterValueChangedCallback(_ => PushTuningFromControls(rollback, latency, loss, duplicate, redundancy, extrapolationDecay, predictionWindow, visualSeconds, inputPrediction, lookQuality));
            loss.RegisterValueChangedCallback(_ => PushTuningFromControls(rollback, latency, loss, duplicate, redundancy, extrapolationDecay, predictionWindow, visualSeconds, inputPrediction, lookQuality));
            duplicate.RegisterValueChangedCallback(_ => PushTuningFromControls(rollback, latency, loss, duplicate, redundancy, extrapolationDecay, predictionWindow, visualSeconds, inputPrediction, lookQuality));
            redundancy.RegisterValueChangedCallback(_ => PushTuningFromControls(rollback, latency, loss, duplicate, redundancy, extrapolationDecay, predictionWindow, visualSeconds, inputPrediction, lookQuality));
            extrapolationDecay.RegisterValueChangedCallback(_ => PushTuningFromControls(rollback, latency, loss, duplicate, redundancy, extrapolationDecay, predictionWindow, visualSeconds, inputPrediction, lookQuality));
            predictionWindow.RegisterValueChangedCallback(_ => PushTuningFromControls(rollback, latency, loss, duplicate, redundancy, extrapolationDecay, predictionWindow, visualSeconds, inputPrediction, lookQuality));
            visualSeconds.RegisterValueChangedCallback(_ => PushTuningFromControls(rollback, latency, loss, duplicate, redundancy, extrapolationDecay, predictionWindow, visualSeconds, inputPrediction, lookQuality));
            inputPrediction.RegisterValueChangedCallback(_ => PushTuningFromControls(rollback, latency, loss, duplicate, redundancy, extrapolationDecay, predictionWindow, visualSeconds, inputPrediction, lookQuality));
            lookQuality.RegisterValueChangedCallback(_ => PushTuningFromControls(rollback, latency, loss, duplicate, redundancy, extrapolationDecay, predictionWindow, visualSeconds, inputPrediction, lookQuality));

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
            _capacityLabel = new Label("Physical ring capacity: -");
            _telemetryStrip = new RollbackTelemetryStripElement();
            _telemetryStrip.style.height = 44f;
            _telemetryStrip.style.marginTop = 8f;
            _graph = new RollbackHashGraphElement(_hashHistory);
            _graph.style.height = 92f;
            _graph.style.marginTop = 8f;

            rootVisualElement.Add(rollback);
            rootVisualElement.Add(latency);
            rootVisualElement.Add(loss);
            rootVisualElement.Add(duplicate);
            rootVisualElement.Add(redundancy);
            rootVisualElement.Add(extrapolationDecay);
            rootVisualElement.Add(predictionWindow);
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
            rootVisualElement.Add(_capacityLabel);
            rootVisualElement.Add(_telemetryStrip);
            rootVisualElement.Add(_graph);

            PullTuningIntoControls(rollback, latency, loss, duplicate, redundancy, extrapolationDecay, predictionWindow, visualSeconds, inputPrediction, lookQuality);
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

            bool hasState = HectonRollbackNetcodeRuntime.TryGetRuntimeState(out RollbackRuntimeStateDTO state);
            if (hasState)
            {
                bool divergent = (state.Flags & (RollbackNetcodeFlags.HashMismatch | RollbackNetcodeFlags.HardResyncRequired)) != 0u;
                rootVisualElement.style.backgroundColor = divergent ? DivergenceColor : NormalColor;
                _hashHistory[_hashCursor++ & 127] = state.LastFrameHash64;
                _graph.MarkDirtyRepaint();
            }

            bool hasPacketTelemetry = false;
            uint droppedPackets = 0u;
            uint duplicatedPackets = 0u;
            if (HectonRollbackNetcodeRuntime.TryGetTelemetry(out Unity.Collections.NativeArray<NetTelemetryEntry64>.ReadOnly telemetry) && telemetry.Length > 0)
            {
                NetTelemetryEntry64 entry = telemetry[(int)(math.max(0, _hashCursor - 1) % telemetry.Length)];
                droppedPackets = entry.DroppedPackets;
                duplicatedPackets = entry.DuplicatedPackets;
                hasPacketTelemetry = true;
            }

            bool hasInputTelemetry = false;
            uint packetRedundancyCount = 0u;
            uint dearLieCount = 0u;
            if (HectonRollbackNetcodeRuntime.TryGetInputPredictionTelemetry(out Unity.Collections.NativeArray<InputPredictionTelemetryEntry>.ReadOnly inputTelemetry) && inputTelemetry.Length > 0)
            {
                InputPredictionTelemetryEntry entry = inputTelemetry[(int)(math.max(0, _hashCursor - 1) % inputTelemetry.Length)];
                packetRedundancyCount = entry.PacketRedundancyCount;
                dearLieCount = entry.ExtrapolatedInputCount;
                hasInputTelemetry = true;
            }

            int physicalCapacity = -1;
            if (_capacityLabel != null &&
                HectonRollbackNetcodeRuntime.TryGetPredictedInputCapacity(out int predictedCapacity))
            {
                physicalCapacity = predictedCapacity;
            }

            if (_telemetryStrip != null)
            {
                _telemetryStrip.SetMetrics(
                    hasState ? state.Flags : 0u,
                    hasState ? state.MismatchSeverity01 : 0f,
                    hasState ? state.ResimComputeTimeMs : 0f,
                    hasState ? state.GlobalQualityWeight : 0f,
                    droppedPackets,
                    duplicatedPackets,
                    packetRedundancyCount,
                    dearLieCount);
            }

            double now = EditorApplication.timeSinceStartup;
            if (now >= _nextTextReadoutTime)
            {
                _nextTextReadoutTime = now + 0.25d;
                RefreshTextReadout(
                    hasState,
                    in state,
                    hasPacketTelemetry,
                    droppedPackets,
                    duplicatedPackets,
                    hasInputTelemetry,
                    packetRedundancyCount,
                    dearLieCount,
                    physicalCapacity);
            }
        }

        private void RefreshTextReadout(
            bool hasState,
            in RollbackRuntimeStateDTO state,
            bool hasPacketTelemetry,
            uint droppedPackets,
            uint duplicatedPackets,
            bool hasInputTelemetry,
            uint packetRedundancyCount,
            uint dearLieCount,
            int physicalCapacity)
        {
            if (hasState)
            {
                if (_lastTextFrame != state.CurrentFrame)
                {
                    _lastTextFrame = state.CurrentFrame;
                    _frameLabel.text = "Frame: " + state.CurrentFrame;
                }

                if (_lastTextHash != state.LastFrameHash64)
                {
                    _lastTextHash = state.LastFrameHash64;
                    _hashLabel.text = "Hash: " + state.LastFrameHash64.ToString("X16");
                }

                if (_lastTextRemoteHash != state.LastRemoteHash64)
                {
                    _lastTextRemoteHash = state.LastRemoteHash64;
                    _remoteHashLabel.text = "Remote: " + state.LastRemoteHash64.ToString("X16");
                }

                if (_lastTextFlags != state.Flags)
                {
                    _lastTextFlags = state.Flags;
                    _flagsLabel.text = "Flags: " + state.Flags.ToString("X8");
                }

                if (_lastTextResimFrames != state.FramesResimulated || math.abs(_lastTextResimMs - state.ResimComputeTimeMs) > 0.001f)
                {
                    _lastTextResimFrames = state.FramesResimulated;
                    _lastTextResimMs = state.ResimComputeTimeMs;
                    _resimLabel.text = "Resim: " + state.ResimComputeTimeMs.ToString("0.000") + " ms / " + state.FramesResimulated + " ticks";
                }
            }

            if ((hasPacketTelemetry || hasInputTelemetry) &&
                (_lastTextDroppedPackets != droppedPackets ||
                 _lastTextDuplicatedPackets != duplicatedPackets ||
                 _lastTextRedundancyCount != packetRedundancyCount ||
                 _lastTextDearLieCount != dearLieCount))
            {
                _lastTextDroppedPackets = droppedPackets;
                _lastTextDuplicatedPackets = duplicatedPackets;
                _lastTextRedundancyCount = packetRedundancyCount;
                _lastTextDearLieCount = dearLieCount;
                _packetLabel.text = "Packets: drop " + droppedPackets + " / dup " + duplicatedPackets + " / red " + packetRedundancyCount + " / dear " + dearLieCount;
            }

            if (physicalCapacity >= 0 && _lastTextCapacity != physicalCapacity)
            {
                _lastTextCapacity = physicalCapacity;
                _capacityLabel.text = "Physical ring capacity: " + physicalCapacity;
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
            SliderInt extrapolationDecay,
            SliderInt predictionWindow,
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
            uint decay = tuning.ExtrapolationDecayPermille == 0u ? RollbackNetcodeConstants.DefaultExtrapolationDecayPermille : tuning.ExtrapolationDecayPermille;
            uint window = tuning.PredictionWindowTicks == 0u ? 30u : tuning.PredictionWindowTicks;
            extrapolationDecay.SetValueWithoutNotify((int)math.min(math.max(decay, 1u), 2000u));
            predictionWindow.SetValueWithoutNotify((int)math.min(math.max(window, 5u), 30u));
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
            SliderInt extrapolationDecay,
            SliderInt predictionWindow,
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
            tuning.ExtrapolationDecayPermille = (uint)math.clamp(extrapolationDecay.value, 1, 2000);
            tuning.PredictionWindowTicks = (uint)math.clamp(predictionWindow.value, 5, 30);
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
            if (!HectonRollbackNetcodeRuntime.TryGetVisualStates(out Unity.Collections.NativeArray<VisualStateDTO>.ReadOnly states) || states.Length <= 0)
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

        private sealed class RollbackTelemetryStripElement : VisualElement
        {
            private uint _flags;
            private uint _droppedPackets;
            private uint _duplicatedPackets;
            private uint _packetRedundancyCount;
            private uint _dearLieCount;
            private float _mismatchSeverity01;
            private float _resimComputeTimeMs;
            private float _qualityWeight;

            public RollbackTelemetryStripElement()
            {
                generateVisualContent += DrawStrip;
            }

            public void SetMetrics(
                uint flags,
                float mismatchSeverity01,
                float resimComputeTimeMs,
                float qualityWeight,
                uint droppedPackets,
                uint duplicatedPackets,
                uint packetRedundancyCount,
                uint dearLieCount)
            {
                mismatchSeverity01 = Sanitize01(mismatchSeverity01);
                resimComputeTimeMs = SanitizePositive(resimComputeTimeMs);
                qualityWeight = Sanitize01(qualityWeight);
                if (_flags == flags &&
                    _droppedPackets == droppedPackets &&
                    _duplicatedPackets == duplicatedPackets &&
                    _packetRedundancyCount == packetRedundancyCount &&
                    _dearLieCount == dearLieCount &&
                    math.abs(_mismatchSeverity01 - mismatchSeverity01) < 0.001f &&
                    math.abs(_resimComputeTimeMs - resimComputeTimeMs) < 0.001f &&
                    math.abs(_qualityWeight - qualityWeight) < 0.001f)
                {
                    return;
                }

                _flags = flags;
                _droppedPackets = droppedPackets;
                _duplicatedPackets = duplicatedPackets;
                _packetRedundancyCount = packetRedundancyCount;
                _dearLieCount = dearLieCount;
                _mismatchSeverity01 = mismatchSeverity01;
                _resimComputeTimeMs = resimComputeTimeMs;
                _qualityWeight = qualityWeight;
                MarkDirtyRepaint();
            }

            private void DrawStrip(MeshGenerationContext context)
            {
                Rect rect = contentRect;
                if (rect.width <= 4f || rect.height <= 4f)
                    return;

                Painter2D painter = context.painter2D;
                DrawRect(painter, rect, new Color(0.015f, 0.02f, 0.024f, 1f));

                float pad = 4f;
                float width = math.max(1f, rect.width - pad * 2f);
                float rowHeight = math.max(2f, (rect.height - pad * 5f) * 0.25f);
                float y = rect.yMin + pad;

                DrawBar(painter, rect.xMin + pad, y, width, rowHeight, _qualityWeight, new Color(0.08f, 0.42f, 0.9f, 0.85f));
                y += rowHeight + pad;
                DrawBar(painter, rect.xMin + pad, y, width, rowHeight, _mismatchSeverity01, new Color(0.95f, 0.18f, 0.08f, 0.9f));
                y += rowHeight + pad;
                DrawBar(painter, rect.xMin + pad, y, width, rowHeight, math.saturate(_resimComputeTimeMs * 10f), new Color(0.9f, 0.62f, 0.12f, 0.9f));
                y += rowHeight + pad;

                float loss01 = math.saturate(((float)_droppedPackets + (float)_duplicatedPackets + (float)_dearLieCount) * 0.05f);
                DrawBar(painter, rect.xMin + pad, y, width, rowHeight, loss01, new Color(0.55f, 0.15f, 0.85f, 0.9f));

                uint activeSegments = math.min(5u, _packetRedundancyCount);
                uint segmentCount = math.max(1u, activeSegments);
                float segmentWidth = width / (float)segmentCount;
                for (uint i = 0u; i < activeSegments; i++)
                {
                    DrawRect(
                        painter,
                        new Rect(rect.xMin + pad + (float)i * segmentWidth + 1f, rect.yMax - pad - 3f, math.max(1f, segmentWidth - 2f), 2f),
                        new Color(0.12f, 0.9f, 0.58f, 0.9f));
                }

                if ((_flags & (RollbackNetcodeFlags.HashMismatch | RollbackNetcodeFlags.HardResyncRequired)) != 0u)
                    DrawRect(painter, new Rect(rect.xMax - 8f, rect.yMin + pad, 4f, rect.height - pad * 2f), RollbackNetcodeTunerWindow.DivergenceColor);
            }

            private static void DrawBar(Painter2D painter, float x, float y, float width, float height, float value01, Color color)
            {
                DrawRect(painter, new Rect(x, y, width, height), new Color(0.045f, 0.055f, 0.065f, 1f));
                DrawRect(painter, new Rect(x, y, width * math.saturate(value01), height), color);
            }

            private static float Sanitize01(float value)
            {
                return math.isfinite(value) ? math.saturate(value) : 0f;
            }

            private static float SanitizePositive(float value)
            {
                return math.isfinite(value) ? math.max(0f, value) : 0f;
            }

            private static void DrawRect(Painter2D painter, Rect rect, Color color)
            {
                painter.fillColor = color;
                painter.BeginPath();
                painter.MoveTo(new Vector2(rect.xMin, rect.yMin));
                painter.LineTo(new Vector2(rect.xMax, rect.yMin));
                painter.LineTo(new Vector2(rect.xMax, rect.yMax));
                painter.LineTo(new Vector2(rect.xMin, rect.yMax));
                painter.ClosePath();
                painter.Fill();
            }
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
