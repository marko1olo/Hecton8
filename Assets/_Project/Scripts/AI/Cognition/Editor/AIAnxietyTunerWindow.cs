#if UNITY_EDITOR
using System.Diagnostics;
using Hecton8.Core.Contracts;
using Hecton8.Core.Memory;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Hecton8.AI.Cognition.Editor
{
    public sealed class AIAnxietyTunerWindow : EditorWindow
    {
        private UtilityAICognitionVaultHandles _cognitionHandles;
        private UtilityAIAnxietyVaultHandles _anxietyHandles;
        private AnxietyRuntimeTuningDTO _tuning;
        private AnxietyTelemetryChartElement _chart;
        private Label _statusLabel;
        private bool _drawGizmos = true;
        private uint _editorFrameCounter;
        private double _nextCsvPollTime;

        [MenuItem("Hecton8/AI/AI Anxiety Tuner")]
        private static void Open()
        {
            GetWindow<AIAnxietyTunerWindow>("AI Anxiety");
        }

        private void OnEnable()
        {
            SceneView.duringSceneGui -= DrawSceneGizmos;
            SceneView.duringSceneGui += DrawSceneGizmos;
            RefreshFromVault();
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= DrawSceneGizmos;
        }

        public void CreateGUI()
        {
            VisualElement root = rootVisualElement;
            root.Clear();
            root.style.paddingLeft = 8;
            root.style.paddingRight = 8;
            root.style.paddingTop = 8;
            root.style.paddingBottom = 8;

            root.Add(new Label("AI Anxiety Cooling"));
            _chart = new AnxietyTelemetryChartElement();
            root.Add(_chart);

            root.Add(CreateSlider("BaseFearDecayRate", 0.01f, 4f, _tuning.BaseFearDecayRate, value =>
            {
                _tuning.BaseFearDecayRate = value;
                WriteTuning();
            }));
            root.Add(CreateSlider("BaseAggressionDecayRate", 0.01f, 3f, _tuning.BaseAggressionDecayRate, value =>
            {
                _tuning.BaseAggressionDecayRate = value;
                WriteTuning();
            }));
            root.Add(CreateSlider("ShelterCoolingMultiplier", 1f, 6f, _tuning.ShelterCoolingMultiplier, value =>
            {
                _tuning.ShelterCoolingMultiplier = value;
                WriteTuning();
            }));
            root.Add(CreateSlider("CalmingThreshold", 0.001f, 0.25f, _tuning.CalmingThreshold, value =>
            {
                _tuning.CalmingThreshold = value;
                WriteTuning();
            }));
            root.Add(CreateSlider("GlobalQualityWeight", 0f, 1f, _tuning.GlobalQualityWeight, value =>
            {
                _tuning.GlobalQualityWeight = value;
                WriteTuning();
            }));
            root.Add(CreateSlider("ThermalPressure01", 0f, 1f, _tuning.ThermalPressure01, value =>
            {
                _tuning.ThermalPressure01 = value;
                WriteTuning();
            }));

            root.Add(new Button(ReloadCsv) { text = "Reload fauna_psychology_profiles.csv" });
            root.Add(new Button(GenerateMockAnxiety) { text = "Generate 5000 Mock Anxiety Spikes" });
            root.Add(new Button(RunFrostTick) { text = "Run Anxiety FrostTick" });
            root.Add(new Button(DumpBlackBox) { text = "Dump Anxiety Black Box" });
            root.Add(new Button(() =>
            {
                string report = OOP_Timer_Scanner.RunAndWriteReport();
                SetStatus(string.IsNullOrEmpty(report) ? "Timer scanner failed." : "Timer scanner report written.");
            })
            { text = "Run Coroutine Timer Scanner" });

            Toggle gizmoToggle = new Toggle("Draw Anxiety Bars") { value = _drawGizmos };
            gizmoToggle.RegisterValueChangedCallback(evt =>
            {
                _drawGizmos = evt.newValue;
                SceneView.RepaintAll();
            });
            root.Add(gizmoToggle);

            _statusLabel = new Label("Vault unavailable.");
            root.Add(_statusLabel);
            RefreshFromVault();
            RefreshChart();
        }

        private void Update()
        {
            if (!TryEnsureVault())
                return;

            double now = EditorApplication.timeSinceStartup;
            if (now >= _nextCsvPollTime)
            {
                _nextCsvPollTime = now + 0.75d;
                if (UtilityAICognitionVault.TryPollPsychologyProfiles(ResolveEditorVault(), ref _anxietyHandles, Application.dataPath + "/.."))
                {
                    RefreshFromVault();
                    SetStatus("Psychology CSV applied.");
                }
            }

            RefreshChart();
        }

        private static Slider CreateSlider(string label, float low, float high, float value, System.Action<float> changed)
        {
            Slider slider = new Slider(label, low, high) { value = value, showInputField = true };
            slider.RegisterValueChangedCallback(evt => changed(evt.newValue));
            return slider;
        }

        private static IDataVault ResolveEditorVault()
        {
            return GlobalDataVault.TryGetLatestCreated(out GlobalDataVault vault) ? vault : null;
        }

        private bool TryEnsureVault()
        {
            IDataVault vault = ResolveEditorVault();
            if (vault == null)
            {
                SetStatus("GlobalDataVault unavailable.");
                return false;
            }

            if (!_cognitionHandles.IsCreated() && !UtilityAICognitionVault.TryAcquireHandles(vault, out _cognitionHandles))
            {
                SetStatus("Cognition vault unavailable.");
                return false;
            }

            if (!_anxietyHandles.IsCreated() && !UtilityAICognitionVault.TryAcquireAnxietyHandles(vault, out _anxietyHandles))
            {
                SetStatus("Anxiety vault unavailable.");
                return false;
            }

            return true;
        }

        private void RefreshFromVault()
        {
            if (!TryEnsureVault())
                return;

            if (UtilityAICognitionVault.TryGetAnxietyTuning(ResolveEditorVault(), ref _anxietyHandles, out _tuning))
                SetStatus("Anxiety tuning sampled.");
            else
                SetStatus("Anxiety tuning unreadable.");
        }

        private void WriteTuning()
        {
            if (!TryEnsureVault())
                return;

            _tuning = AnxietyDecayJobMath.SanitizeTuning(in _tuning);
            SetStatus(UtilityAICognitionVault.TrySetAnxietyTuning(ResolveEditorVault(), ref _anxietyHandles, in _tuning)
                ? "Anxiety tuning updated."
                : "Anxiety tuning write failed.");
            SceneView.RepaintAll();
        }

        private void ReloadCsv()
        {
            if (!TryEnsureVault())
                return;

            bool loaded = UtilityAICognitionVault.TryLoadPsychologyProfiles(ResolveEditorVault(), ref _anxietyHandles, Application.dataPath + "/..");
            RefreshFromVault();
            SetStatus(loaded ? "Psychology profiles applied." : "Psychology CSV missing or invalid.");
        }

        private void GenerateMockAnxiety()
        {
            if (!TryResolveAllBuffers(out UtilityAICognitionVaultBuffers cognitionBuffers, out UtilityAIAnxietyVaultBuffers anxietyBuffers))
            {
                SetStatus("Mock anxiety buffers unavailable.");
                return;
            }

            uint frame = NextEditorFrame(in anxietyBuffers);
            if (!UtilityAICognitionVault.TryScheduleMockData(in cognitionBuffers, frame, default, out JobHandle cognitionMockHandle) ||
                !UtilityAICognitionVault.TryScheduleMockAnxietyEnvironment(in cognitionBuffers, in anxietyBuffers, frame, 5000, cognitionMockHandle, out JobHandle anxietyMockHandle))
            {
                SetStatus("Mock anxiety schedule failed.");
                return;
            }

            anxietyMockHandle.Complete();
            SetStatus("Mock anxiety spikes generated.");
            RefreshChart();
            SceneView.RepaintAll();
        }

        private void RunFrostTick()
        {
            if (!TryResolveAllBuffers(out UtilityAICognitionVaultBuffers cognitionBuffers, out UtilityAIAnxietyVaultBuffers anxietyBuffers))
            {
                SetStatus("Anxiety buffers unavailable.");
                return;
            }

            uint frame = NextEditorFrame(in anxietyBuffers);
            float deltaSeconds = ResolveDeltaSeconds(in anxietyBuffers);
            Stopwatch stopwatch = Stopwatch.StartNew();
            if (!UtilityAICognitionVault.TryScheduleAnxietyFrostTick(
                    in cognitionBuffers,
                    in anxietyBuffers,
                    frame,
                    deltaSeconds,
                    0f,
                    default,
                    out JobHandle handle))
            {
                SetStatus("Anxiety FrostTick schedule failed.");
                return;
            }

            handle.Complete();
            stopwatch.Stop();
            float microseconds = (float)(stopwatch.Elapsed.TotalMilliseconds * 1000.0);
            bool dumped = UtilityAICognitionVault.TryPatchAnxietyTelemetryExecutionTimeAndDump(anxietyBuffers, frame, microseconds, Application.dataPath + "/..");
            SetStatus(dumped ? "Anxiety FrostTick fault dumped." : "Anxiety FrostTick complete.");
            RefreshChart();
            SceneView.RepaintAll();
        }

        private void DumpBlackBox()
        {
            if (!TryResolveAllBuffers(out _, out UtilityAIAnxietyVaultBuffers anxietyBuffers))
            {
                SetStatus("Anxiety telemetry unavailable.");
                return;
            }

            SetStatus(UtilityAICognitionVault.TryDumpAnxietyBlackBox(in anxietyBuffers, Application.dataPath + "/..", NextEditorFrame(in anxietyBuffers))
                ? "Anxiety black box dumped."
                : "Anxiety black box dump failed.");
        }

        private bool TryResolveAllBuffers(out UtilityAICognitionVaultBuffers cognitionBuffers, out UtilityAIAnxietyVaultBuffers anxietyBuffers)
        {
            cognitionBuffers = default;
            anxietyBuffers = default;
            IDataVault vault = ResolveEditorVault();
            return TryEnsureVault() &&
                   UtilityAICognitionVault.TryResolveViews(vault, ref _cognitionHandles, out cognitionBuffers) &&
                   UtilityAICognitionVault.TryResolveAnxietyViews(vault, ref _anxietyHandles, out anxietyBuffers);
        }

        private uint NextEditorFrame(in UtilityAIAnxietyVaultBuffers buffers)
        {
            uint next = _editorFrameCounter + 1u;
            if (next == 0u)
                next = 1u;

            if (buffers.Tuning.IsCreated && buffers.Tuning.Length > 0)
            {
                uint frame = buffers.Tuning[0].Frame;
                if (frame >= next)
                    next = frame + 1u;
                if (next == 0u)
                    next = 1u;
            }

            _editorFrameCounter = next;
            return next;
        }

        private static float ResolveDeltaSeconds(in UtilityAIAnxietyVaultBuffers buffers)
        {
            if (buffers.Tuning.IsCreated && buffers.Tuning.Length > 0)
            {
                AnxietyRuntimeTuningDTO source = buffers.Tuning[0];
                AnxietyRuntimeTuningDTO tuning = AnxietyDecayJobMath.SanitizeTuning(in source);
                return tuning.SimulationDeltaSeconds;
            }

            return 1f / 30f;
        }

        private void RefreshChart()
        {
            if (_chart == null ||
                !TryEnsureVault() ||
                !UtilityAICognitionVault.TryResolveAnxietyViews(ResolveEditorVault(), ref _anxietyHandles, out UtilityAIAnxietyVaultBuffers buffers))
            {
                return;
            }

            int cursor = buffers.TelemetryCursor.IsCreated && buffers.TelemetryCursor.Length > 0 ? buffers.TelemetryCursor[0] : 0;
            _chart.SetTelemetry(buffers.TelemetryRing, cursor);
        }

        private void DrawSceneGizmos(SceneView sceneView)
        {
            if (!_drawGizmos || !TryResolveAllBuffers(out UtilityAICognitionVaultBuffers cognitionBuffers, out UtilityAIAnxietyVaultBuffers anxietyBuffers))
                return;

            AnxietyRuntimeTuningDTO tuning = AnxietyDecayDefaults.BuildTuning();
            if (anxietyBuffers.Tuning.IsCreated && anxietyBuffers.Tuning.Length > 0)
            {
                AnxietyRuntimeTuningDTO source = anxietyBuffers.Tuning[0];
                tuning = AnxietyDecayJobMath.SanitizeTuning(in source);
            }
            int count = math.min(256, math.min(cognitionBuffers.States.Length, cognitionBuffers.Aups.Length));
            for (int i = 0; i < count; i++)
            {
                CognitionStateDTO state = cognitionBuffers.States[i];
                float fear = AnxietyDecayJobMath.Sanitize01(state.Fear01);
                float aggression = AnxietyDecayJobMath.Sanitize01(state.Aggression01);
                if (fear <= tuning.CalmingThreshold && aggression <= tuning.CalmingThreshold)
                    continue;

                float3 local = AupPrecisionMath.DowncastLocalDeltaClamped(cognitionBuffers.Aups[i].AUP, 2048f, float3.zero);
                Vector3 basePosition = new Vector3(local.x, local.y + 2.0f, local.z);
                DrawBar(basePosition + Vector3.left * 0.7f, fear, new Color(1f, 0.82f, 0.12f, 0.9f));
                DrawBar(basePosition + Vector3.right * 0.7f, aggression, new Color(1f, 0.22f, 0.12f, 0.9f));
            }
        }

        private static void DrawBar(Vector3 basePosition, float value, Color color)
        {
            float height = math.max(0.1f, value * 5f);
            Handles.color = color;
            Handles.DrawLine(basePosition, basePosition + Vector3.up * height);
            Handles.CubeHandleCap(0, basePosition + Vector3.up * height, Quaternion.identity, 0.35f, EventType.Repaint);
        }

        private void SetStatus(string status)
        {
            if (_statusLabel != null)
                _statusLabel.text = status;
        }

        private sealed class AnxietyTelemetryChartElement : VisualElement
        {
            private NativeArray<AnxietyTelemetryEntry> _ring;
            private int _cursor;

            public AnxietyTelemetryChartElement()
            {
                style.height = 96;
                style.marginBottom = 8;
                generateVisualContent += DrawChart;
            }

            public void SetTelemetry(NativeArray<AnxietyTelemetryEntry> ring, int cursor)
            {
                _ring = ring;
                _cursor = cursor;
                MarkDirtyRepaint();
            }

            private void DrawChart(MeshGenerationContext context)
            {
                Rect rect = contentRect;
                Painter2D painter = context.painter2D;
                painter.strokeColor = new Color(0.12f, 0.12f, 0.12f, 1f);
                painter.lineWidth = 1f;
                painter.BeginPath();
                painter.MoveTo(new Vector2(rect.xMin, rect.yMax));
                painter.LineTo(new Vector2(rect.xMax, rect.yMax));
                painter.LineTo(new Vector2(rect.xMax, rect.yMin));
                painter.Stroke();

                if (!_ring.IsCreated || _ring.Length <= 1)
                    return;

                DrawLine(painter, rect, true, new Color(1f, 0.82f, 0.12f, 1f));
                DrawLine(painter, rect, false, new Color(1f, 0.22f, 0.12f, 1f));
            }

            private void DrawLine(Painter2D painter, Rect rect, bool fear, Color color)
            {
                painter.strokeColor = color;
                painter.lineWidth = 2f;
                painter.BeginPath();
                int length = math.min(_ring.Length, AnxietyDecayConstants.TelemetryFrames);
                for (int i = 0; i < length; i++)
                {
                    int index = (_cursor + 1 + i) % length;
                    AnxietyTelemetryEntry entry = _ring[index];
                    float value = fear ? entry.AverageFear01 : entry.AverageAggression01;
                    float x = rect.xMin + (rect.width * (i * math.rcp(math.max(1f, length - 1))));
                    float y = rect.yMax - (rect.height * AnxietyDecayJobMath.Sanitize01(value));
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
#endif
