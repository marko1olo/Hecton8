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
    public sealed class CognitionUtilityTunerWindow : EditorWindow
    {
        private UtilityAICognitionVaultHandles _handles;
        private CognitionUtilityTuningDTO _tuning;
        private Label _statusLabel;
        private CognitionActionChartElement _actionChart;
        private bool _drawGizmos = true;
        private double _nextCsvPollTime;
        private uint _editorFrameCounter;

        [MenuItem("Hecton8/AI/Utility Cognition Tuner")]
        private static void Open()
        {
            GetWindow<CognitionUtilityTunerWindow>("Utility Cognition");
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

            root.Add(new Label("Utility AI Cognition"));
            _actionChart = new CognitionActionChartElement();
            root.Add(_actionChart);
            Slider quality = CreateSlider("Global Quality Weight", 0f, 1f, _tuning.Runtime.x, value =>
            {
                _tuning.Runtime.x = value;
                WriteTuning();
            });
            root.Add(quality);
            root.Add(CreateSlider("Hunger Curve Cubic", -2f, 2f, _tuning.HungerPolynomial.x, value =>
            {
                _tuning.HungerPolynomial.x = value;
                WriteTuning();
            }));
            root.Add(CreateSlider("Hunger Curve Quadratic", -2f, 2f, _tuning.HungerPolynomial.y, value =>
            {
                _tuning.HungerPolynomial.y = value;
                WriteTuning();
            }));
            root.Add(CreateSlider("Hunger Curve Linear", -2f, 2f, _tuning.HungerPolynomial.z, value =>
            {
                _tuning.HungerPolynomial.z = value;
                WriteTuning();
            }));
            root.Add(CreateSlider("Fear Curve Cubic", -2f, 2f, _tuning.FearPolynomial.x, value =>
            {
                _tuning.FearPolynomial.x = value;
                WriteTuning();
            }));
            root.Add(CreateSlider("Fear Curve Quadratic", -2f, 2f, _tuning.FearPolynomial.y, value =>
            {
                _tuning.FearPolynomial.y = value;
                WriteTuning();
            }));
            root.Add(CreateSlider("Fear Curve Linear", -2f, 2f, _tuning.FearPolynomial.z, value =>
            {
                _tuning.FearPolynomial.z = value;
                WriteTuning();
            }));
            root.Add(CreateSlider("Aggression Curve Cubic", -2f, 2f, _tuning.AggressionPolynomial.x, value =>
            {
                _tuning.AggressionPolynomial.x = value;
                WriteTuning();
            }));
            root.Add(CreateSlider("Aggression Curve Quadratic", -2f, 2f, _tuning.AggressionPolynomial.y, value =>
            {
                _tuning.AggressionPolynomial.y = value;
                WriteTuning();
            }));
            root.Add(CreateSlider("Aggression Curve Linear", -2f, 2f, _tuning.AggressionPolynomial.z, value =>
            {
                _tuning.AggressionPolynomial.z = value;
                WriteTuning();
            }));
            root.Add(CreateSlider("Flee Bias", -1f, 1f, _tuning.ActionBiases.x, value =>
            {
                _tuning.ActionBiases.x = value;
                WriteTuning();
            }));
            root.Add(CreateSlider("Hunt Bias", -1f, 1f, _tuning.ActionBiases.y, value =>
            {
                _tuning.ActionBiases.y = value;
                WriteTuning();
            }));
            root.Add(CreateSlider("Patrol Bias", -1f, 1f, _tuning.ActionBiases.z, value =>
            {
                _tuning.ActionBiases.z = value;
                WriteTuning();
            }));
            root.Add(CreateSlider("Rest Bias", -1f, 1f, _tuning.ActionBiases.w, value =>
            {
                _tuning.ActionBiases.w = value;
                WriteTuning();
            }));
            root.Add(CreateSlider("Acoustic Fear Gain", 0f, 2f, _tuning.SignalGains.x, value =>
            {
                _tuning.SignalGains.x = value;
                WriteTuning();
            }));
            root.Add(CreateSlider("Damage Fear Gain", 0f, 2f, _tuning.SignalGains.y, value =>
            {
                _tuning.SignalGains.y = value;
                WriteTuning();
            }));
            root.Add(CreateSlider("Hunger Gain / Second", 0f, 0.25f, _tuning.SignalGains.z, value =>
            {
                _tuning.SignalGains.z = value;
                WriteTuning();
            }));
            root.Add(CreateSlider("Aggression Damage Gain", 0f, 2f, _tuning.SignalGains.w, value =>
            {
                _tuning.SignalGains.w = value;
                WriteTuning();
            }));
            root.Add(CreateSlider("Threat Radius", 16f, 600f, _tuning.DistanceMeters.x, value =>
            {
                _tuning.DistanceMeters.x = value;
                WriteTuning();
            }));
            root.Add(CreateSlider("Food Radius", 16f, 420f, _tuning.DistanceMeters.y, value =>
            {
                _tuning.DistanceMeters.y = value;
                WriteTuning();
            }));

            Button reloadCsv = new Button(ReloadCsv) { text = "Reload fauna_cognition_profiles.csv" };
            Button generateMock = new Button(GenerateMockData) { text = "Generate Mock Cognition Data" };
            Button runTick = new Button(RunOneEditorTick) { text = "Run Utility Tick" };
            Button dump = new Button(DumpBlackBox) { text = "Dump Black Box" };
            Button scan = new Button(() =>
            {
                string path = OOP_FSM_Scanner.RunAndWriteReport();
                SetStatus(string.IsNullOrEmpty(path) ? "Scanner failed." : "Scanner report written.");
            })
            { text = "Run FSM Scanner" };
            Toggle gizmoToggle = new Toggle("Draw Motive Gizmos") { value = _drawGizmos };
            gizmoToggle.RegisterValueChangedCallback(evt =>
            {
                _drawGizmos = evt.newValue;
                SceneView.RepaintAll();
            });

            root.Add(reloadCsv);
            root.Add(generateMock);
            root.Add(runTick);
            root.Add(dump);
            root.Add(scan);
            root.Add(gizmoToggle);
            _statusLabel = new Label("Vault unavailable.");
            root.Add(_statusLabel);
            RefreshFromVault();
            RefreshActionChart();
        }

        private void Update()
        {
            if (!EditorApplication.isPlaying || !TryEnsureVault())
                return;

            double now = EditorApplication.timeSinceStartup;
            if (now < _nextCsvPollTime)
                return;

            _nextCsvPollTime = now + 0.75d;
            if (UtilityAICognitionVault.TryPollCsvProfiles(ResolveEditorVault(), ref _handles, Application.dataPath + "/.."))
            {
                RefreshFromVault();
                SetStatus("CSV profiles auto-applied.");
            }

            RefreshActionChart();
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

            if (!_handles.IsCreated() && !UtilityAICognitionVault.TryAcquireHandles(vault, out _handles))
            {
                SetStatus("Utility cognition vault unavailable.");
                return false;
            }

            return true;
        }

        private void RefreshFromVault()
        {
            if (!TryEnsureVault())
                return;

            if (UtilityAICognitionVault.TryGetTuning(ResolveEditorVault(), ref _handles, out _tuning))
                SetStatus("Vault tuning sampled.");
            else
                SetStatus("Vault tuning unreadable.");
        }

        private void WriteTuning()
        {
            if (!TryEnsureVault())
                return;

            _tuning = UtilityAICognitionJobMath.SanitizeTuning(in _tuning);
            SetStatus(UtilityAICognitionVault.TrySetTuning(ResolveEditorVault(), ref _handles, in _tuning)
                ? "Vault tuning updated."
                : "Vault tuning write failed.");
            SceneView.RepaintAll();
        }

        private void ReloadCsv()
        {
            if (!TryEnsureVault())
                return;

            bool loaded = UtilityAICognitionVault.TryLoadCsvProfiles(ResolveEditorVault(), ref _handles, Application.dataPath + "/..");
            RefreshFromVault();
            SetStatus(loaded ? "CSV profiles applied." : "CSV profiles missing or unchanged.");
        }

        private void GenerateMockData()
        {
            if (!TryEnsureVault() ||
                !UtilityAICognitionVault.TryResolveViews(ResolveEditorVault(), ref _handles, out UtilityAICognitionVaultBuffers buffers))
            {
                SetStatus("Mock data buffers unavailable.");
                return;
            }

            uint frame = NextEditorFrame(in buffers);
            if (!UtilityAICognitionVault.TryScheduleMockData(in buffers, frame, default, out JobHandle handle))
            {
                SetStatus("Mock data schedule failed.");
                return;
            }

            handle.Complete();
            SetStatus("Mock cognition data generated.");
            RefreshActionChart();
            SceneView.RepaintAll();
        }

        private void RunOneEditorTick()
        {
            if (!TryEnsureVault() ||
                !UtilityAICognitionVault.TryResolveViews(ResolveEditorVault(), ref _handles, out UtilityAICognitionVaultBuffers buffers))
            {
                SetStatus("Cognition buffers unavailable.");
                return;
            }

            uint frame = NextEditorFrame(in buffers);
            Stopwatch stopwatch = Stopwatch.StartNew();
            NativeArray<CognitionMovementAcousticSignalDTO>.ReadOnly movementSignals = default;
            NativeArray<CognitionCombatDamageSignalDTO>.ReadOnly damageSignals = default;
            float simulationDelta = ResolveSimulationDelta(in buffers);
            bool scheduled = UtilityAICognitionVault.TryScheduleCognitionPass(
                in buffers,
                frame,
                simulationDelta,
                0f,
                movementSignals,
                0,
                damageSignals,
                0,
                default,
                out JobHandle handle);
            if (!scheduled)
            {
                SetStatus("Utility tick schedule failed.");
                return;
            }

            handle.Complete();
            stopwatch.Stop();
            float microseconds = (float)(stopwatch.Elapsed.TotalMilliseconds * 1000.0);
            bool dumped = UtilityAICognitionVault.TryPatchTelemetryExecutionTimeAndDump(buffers, frame, microseconds, Application.dataPath + "/..");
            SetStatus(dumped ? "Utility tick fault dumped." : "Utility tick complete.");
            RefreshActionChart();
            SceneView.RepaintAll();
        }

        private void DumpBlackBox()
        {
            if (!TryEnsureVault() ||
                !UtilityAICognitionVault.TryResolveViews(ResolveEditorVault(), ref _handles, out UtilityAICognitionVaultBuffers buffers))
            {
                SetStatus("Telemetry buffers unavailable.");
                return;
            }

            SetStatus(UtilityAICognitionVault.TryDumpBlackBox(in buffers, Application.dataPath + "/..", NextEditorFrame(in buffers))
                ? "Black box dumped."
                : "Black box dump failed.");
        }

        private uint NextEditorFrame(in UtilityAICognitionVaultBuffers buffers)
        {
            uint next = _editorFrameCounter + 1u;
            if (next == 0u)
                next = 1u;

            if (buffers.Tuning.IsCreated && buffers.Tuning.Length > 0)
            {
                uint vaultFrame = buffers.Tuning[0].Frame;
                if (vaultFrame >= next)
                    next = vaultFrame + 1u;
                if (next == 0u)
                    next = 1u;
            }

            _editorFrameCounter = next;
            return next;
        }

        private void DrawSceneGizmos(SceneView sceneView)
        {
            if (!_drawGizmos || !EditorApplication.isPlaying || !TryEnsureVault())
                return;

            if (!UtilityAICognitionVault.TryResolveViews(ResolveEditorVault(), ref _handles, out UtilityAICognitionVaultBuffers buffers) ||
                !buffers.Outputs.IsCreated ||
                !buffers.Aups.IsCreated)
            {
                return;
            }

            int count = math.min(buffers.Outputs.Length, math.min(buffers.Aups.Length, 256));
            for (int i = 0; i < count; i++)
            {
                CognitionActionOutputDTO output = buffers.Outputs[i];
                if ((output.Flags & UtilityAICognitionActionFlags.Active) == 0)
                    continue;

                float3 local = AupPrecisionMath.DowncastLocalDeltaClamped(buffers.Aups[i].AUP, 2048f, float3.zero);
                Vector3 center = new Vector3(local.x, local.y, local.z);
                Handles.color = ResolveActionColor(output.ActionHash);
                Handles.CubeHandleCap(0, center, Quaternion.identity, 2.0f + output.MaxUtility, EventType.Repaint);
                Vector3 desired = new Vector3(output.DesiredLocalDirection.x, output.DesiredLocalDirection.y, output.DesiredLocalDirection.z) * 8f;
                Handles.DrawLine(center, center + desired);
                if (TryResolveTargetDelta(in buffers, i, output.TargetEntityHash, out float3 targetDelta))
                {
                    Vector3 target = center + new Vector3(targetDelta.x, targetDelta.y, targetDelta.z);
                    Handles.DrawWireCube(target, Vector3.one * 1.25f);
                    Handles.DrawLine(center, target);
                }
            }
        }

        private static float ResolveSimulationDelta(in UtilityAICognitionVaultBuffers buffers)
        {
            if (buffers.Tuning.IsCreated && buffers.Tuning.Length > 0)
            {
                CognitionUtilityTuningDTO source = buffers.Tuning[0];
                CognitionUtilityTuningDTO tuning = UtilityAICognitionJobMath.SanitizeTuning(in source);
                return tuning.Runtime.y;
            }

            return 1f / 30f;
        }

        private static bool TryResolveTargetDelta(
            in UtilityAICognitionVaultBuffers buffers,
            int sourceIndex,
            uint targetHash,
            out float3 delta)
        {
            delta = default;
            if (targetHash == 0u ||
                !buffers.Aups.IsCreated ||
                !buffers.Targets.IsCreated ||
                (uint)sourceIndex >= (uint)buffers.Aups.Length)
            {
                return false;
            }

            double3 selfAup = buffers.Aups[sourceIndex].AUP;
            int count = math.min(buffers.Targets.Length, 512);
            for (int i = 0; i < count; i++)
            {
                CognitionTargetCandidateDTO target = buffers.Targets[i];
                if (target.EntityHash != targetHash)
                    continue;

                double3 deltaD = AupPrecisionMath.LocalDeltaDouble(target.AUP, selfAup);
                delta = AupPrecisionMath.DowncastLocalDeltaClamped(deltaD, 2048f, float3.zero);
                return math.all(math.isfinite(delta));
            }

            return false;
        }

        private void RefreshActionChart()
        {
            if (_actionChart == null ||
                !TryEnsureVault() ||
                !UtilityAICognitionVault.TryResolveViews(ResolveEditorVault(), ref _handles, out UtilityAICognitionVaultBuffers buffers) ||
                !buffers.Outputs.IsCreated)
            {
                return;
            }

            int flee = 0;
            int hunt = 0;
            int patrol = 0;
            int rest = 0;
            int count = math.min(buffers.Outputs.Length, 1024);
            for (int i = 0; i < count; i++)
            {
                CognitionActionOutputDTO output = buffers.Outputs[i];
                if ((output.Flags & UtilityAICognitionActionFlags.Active) == 0)
                    continue;

                flee += output.ActionHash == UtilityAICognitionConstants.ActionFleeHash ? 1 : 0;
                hunt += output.ActionHash == UtilityAICognitionConstants.ActionHuntHash ? 1 : 0;
                patrol += output.ActionHash == UtilityAICognitionConstants.ActionPatrolHash ? 1 : 0;
                rest += output.ActionHash == UtilityAICognitionConstants.ActionRestHash ? 1 : 0;
            }

            _actionChart.SetCounts(flee, hunt, patrol, rest);
        }

        private static Color ResolveActionColor(uint actionHash)
        {
            if (actionHash == UtilityAICognitionConstants.ActionFleeHash)
                return Color.red;
            if (actionHash == UtilityAICognitionConstants.ActionHuntHash)
                return Color.yellow;
            if (actionHash == UtilityAICognitionConstants.ActionPatrolHash)
                return Color.cyan;
            return Color.blue;
        }

        private void SetStatus(string status)
        {
            if (_statusLabel != null)
                _statusLabel.text = status;
        }

        private sealed class CognitionActionChartElement : VisualElement
        {
            private int _flee;
            private int _hunt;
            private int _patrol;
            private int _rest;

            public CognitionActionChartElement()
            {
                style.height = 28;
                style.marginBottom = 8;
                generateVisualContent += DrawChart;
            }

            public void SetCounts(int flee, int hunt, int patrol, int rest)
            {
                _flee = math.max(0, flee);
                _hunt = math.max(0, hunt);
                _patrol = math.max(0, patrol);
                _rest = math.max(0, rest);
                MarkDirtyRepaint();
            }

            private void DrawChart(MeshGenerationContext context)
            {
                Rect rect = contentRect;
                Painter2D painter = context.painter2D;
                int total = math.max(1, _flee + _hunt + _patrol + _rest);
                float cursor = rect.xMin;
                DrawSegment(painter, ref cursor, rect, _flee, total, new Color(0.85f, 0.12f, 0.08f, 1f));
                DrawSegment(painter, ref cursor, rect, _hunt, total, new Color(0.92f, 0.76f, 0.12f, 1f));
                DrawSegment(painter, ref cursor, rect, _patrol, total, new Color(0.1f, 0.75f, 0.85f, 1f));
                DrawSegment(painter, ref cursor, rect, _rest, total, new Color(0.18f, 0.34f, 0.9f, 1f));
            }

            private static void DrawSegment(Painter2D painter, ref float cursor, Rect rect, int count, int total, Color color)
            {
                if (count <= 0)
                    return;

                float width = rect.width * (count * math.rcp((float)math.max(1, total)));
                painter.fillColor = color;
                painter.BeginPath();
                painter.MoveTo(new Vector2(cursor, rect.yMin));
                painter.LineTo(new Vector2(cursor + width, rect.yMin));
                painter.LineTo(new Vector2(cursor + width, rect.yMax));
                painter.LineTo(new Vector2(cursor, rect.yMax));
                painter.ClosePath();
                painter.Fill();
                cursor += width;
            }
        }
    }
}
#endif
