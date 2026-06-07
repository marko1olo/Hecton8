using System;
using Hecton8.Core;
using Hecton8.World;
using Hecton8.Vehicles.Automation;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Hecton8.Vehicles.Automation.Editor
{
    public sealed class SubmarineAutopilotTunerWindow : EditorWindow
    {
        private const double TelemetryRefreshIntervalSeconds = 0.25d;

        private IntegerField _submarineIndex;
        private Slider _feelerLength;
        private Slider _repulsionWeight;
        private Slider _maxTurnRate;
        private Slider _acceptanceRadius;
        private Slider _flowCompensation;
        private Slider _qualityCap;
        private Label _status;
        private IntegerField _activeAutopilotsReadout;
        private IntegerField _feelerCountReadout;
        private FloatField _repulsionReadout;
        private FloatField _burstMicrosecondsReadout;
        private FloatField _resolvedQualityReadout;
        private IntegerField _flagsReadout;
        private Toggle _sceneClickInjection;
        private Toggle _sceneClickRouteInjection;
        private double _nextTelemetryRefreshTime;
        private bool _suppressCallbacks;

        [MenuItem("Hecton8/Vehicles/Submarine Autopilot Tuner")]
        public static void Open()
        {
            SubmarineAutopilotTunerWindow window = GetWindow<SubmarineAutopilotTunerWindow>();
            window.titleContent = new GUIContent("Submarine Autopilot");
            window.minSize = new Vector2(360f, 260f);
            window.Show();
        }

        public void CreateGUI()
        {
            VisualElement root = rootVisualElement;
            root.style.paddingLeft = 10f;
            root.style.paddingRight = 10f;
            root.style.paddingTop = 8f;
            root.style.paddingBottom = 8f;

            _status = new Label("Runtime unavailable");
            _status.style.unityFontStyleAndWeight = FontStyle.Bold;
            root.Add(_status);

            _activeAutopilotsReadout = BuildReadoutInteger("Active");
            _feelerCountReadout = BuildReadoutInteger("Feelers");
            _repulsionReadout = BuildReadoutFloat("Repulsion");
            _burstMicrosecondsReadout = BuildReadoutFloat("Burst us");
            _resolvedQualityReadout = BuildReadoutFloat("Resolved Quality");
            _flagsReadout = BuildReadoutInteger("Flags");
            root.Add(_activeAutopilotsReadout);
            root.Add(_feelerCountReadout);
            root.Add(_repulsionReadout);
            root.Add(_burstMicrosecondsReadout);
            root.Add(_resolvedQualityReadout);
            root.Add(_flagsReadout);

            _submarineIndex = new IntegerField("Submarine Index");
            _submarineIndex.value = 0;
            root.Add(_submarineIndex);

            Button profileDefault = new Button(() => ApplyHandlingProfile(SubmarineAutopilotConstants.HandlingProfileDefaultHash)) { text = "Profile Default" };
            Button profileScout = new Button(() => ApplyHandlingProfile(SubmarineAutopilotConstants.HandlingProfileScoutHash)) { text = "Profile Scout" };
            Button profileFreighter = new Button(() => ApplyHandlingProfile(SubmarineAutopilotConstants.HandlingProfileFreighterHash)) { text = "Profile Freighter" };
            root.Add(profileDefault);
            root.Add(profileScout);
            root.Add(profileFreighter);

            _feelerLength = BuildSlider("Feeler Length", 8f, 160f);
            _repulsionWeight = BuildSlider("Repulsion Weight", 0f, 12f);
            _maxTurnRate = BuildSlider("Max Turn Rate", 0.02f, 1.6f);
            _acceptanceRadius = BuildSlider("Waypoint Acceptance", 1f, 60f);
            _flowCompensation = BuildSlider("Flow Compensation", 0f, 2.5f);
            _qualityCap = BuildSlider("Quality Cap", 0f, 1f);
            root.Add(_feelerLength);
            root.Add(_repulsionWeight);
            root.Add(_maxTurnRate);
            root.Add(_acceptanceRadius);
            root.Add(_flowCompensation);
            root.Add(_qualityCap);

            _sceneClickInjection = new Toggle("Scene Click Target");
            _sceneClickRouteInjection = new Toggle("Scene Click Route");
            root.Add(_sceneClickInjection);
            root.Add(_sceneClickRouteInjection);

            Button refresh = new Button(RefreshFromRuntime) { text = "Refresh Vault" };
            root.Add(refresh);

            RegisterSlider(_feelerLength, 0);
            RegisterSlider(_repulsionWeight, 1);
            RegisterSlider(_maxTurnRate, 2);
            RegisterSlider(_acceptanceRadius, 3);
            RegisterSlider(_flowCompensation, 4);
            RegisterSlider(_qualityCap, 5);
            RefreshFromRuntime();
        }

        private void OnEnable()
        {
            SceneView.duringSceneGui -= OnSceneGui;
            SceneView.duringSceneGui += OnSceneGui;
            EditorApplication.update -= OnEditorPulse;
            EditorApplication.update += OnEditorPulse;
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= OnSceneGui;
            EditorApplication.update -= OnEditorPulse;
        }

        private void OnEditorPulse()
        {
            double now = EditorApplication.timeSinceStartup;
            if (now < _nextTelemetryRefreshTime)
                return;

            _nextTelemetryRefreshTime = now + TelemetryRefreshIntervalSeconds;
            RefreshTelemetryOnly();
        }

        private static Slider BuildSlider(string label, float min, float max)
        {
            Slider slider = new Slider(label, min, max);
            slider.showInputField = true;
            return slider;
        }

        private static IntegerField BuildReadoutInteger(string label)
        {
            IntegerField field = new IntegerField(label);
            field.SetEnabled(false);
            return field;
        }

        private static FloatField BuildReadoutFloat(string label)
        {
            FloatField field = new FloatField(label);
            field.SetEnabled(false);
            return field;
        }

        private void RegisterSlider(Slider slider, int field)
        {
            slider.RegisterValueChangedCallback(evt =>
            {
                if (_suppressCallbacks)
                    return;
                ApplyTuningValue(field, evt.newValue);
            });
        }

        private void RefreshFromRuntime()
        {
            _suppressCallbacks = true;
            if (SubmarineAutopilotSdfNavigator.TryGetLatest(out SubmarineAutopilotSdfNavigator runtime) &&
                runtime.TryReadTuning(out AutopilotTuningDTO tuning))
            {
                _feelerLength.value = tuning.FeelerLength;
                _repulsionWeight.value = tuning.RepulsionWeight;
                _maxTurnRate.value = tuning.MaxTurnRateRadians;
                _acceptanceRadius.value = tuning.WaypointAcceptanceRadius;
                _flowCompensation.value = tuning.FlowCompensationWeight;
                _qualityCap.value = tuning.GlobalQualityWeight;
            }
            _suppressCallbacks = false;
            RefreshTelemetryOnly();
        }

        private void RefreshTelemetryOnly()
        {
            if (_status == null)
                return;

            if (!SubmarineAutopilotSdfNavigator.TryGetLatest(out SubmarineAutopilotSdfNavigator runtime))
            {
                _status.text = "Runtime unavailable";
                ClearTelemetryReadouts();
                return;
            }

            if (runtime.TryReadLatestTelemetry(out AutopilotTelemetryEntry telemetry))
            {
                _status.text = "Runtime telemetry";
                _activeAutopilotsReadout.SetValueWithoutNotify((int)telemetry.ActiveAutopilots);
                _feelerCountReadout.SetValueWithoutNotify((int)telemetry.FeelerCount);
                _repulsionReadout.SetValueWithoutNotify(telemetry.AverageRepulsionMagnitude);
                _burstMicrosecondsReadout.SetValueWithoutNotify(telemetry.EstimatedBurstMicroseconds);
                if (runtime.TryReadTuning(out AutopilotTuningDTO tuning))
                    _resolvedQualityReadout.SetValueWithoutNotify(tuning.ResolvedQualityWeight);
                else
                    _resolvedQualityReadout.SetValueWithoutNotify(0f);
                _flagsReadout.SetValueWithoutNotify(unchecked((int)telemetry.Flags));
            }
            else
            {
                _status.text = "Runtime ready; telemetry empty";
                ClearTelemetryReadouts();
            }
        }

        private void ClearTelemetryReadouts()
        {
            _activeAutopilotsReadout?.SetValueWithoutNotify(0);
            _feelerCountReadout?.SetValueWithoutNotify(0);
            _repulsionReadout?.SetValueWithoutNotify(0f);
            _burstMicrosecondsReadout?.SetValueWithoutNotify(0f);
            _resolvedQualityReadout?.SetValueWithoutNotify(0f);
            _flagsReadout?.SetValueWithoutNotify(0);
        }

        private void ApplyTuningValue(int field, float value)
        {
            if (!SubmarineAutopilotSdfNavigator.TryGetLatest(out SubmarineAutopilotSdfNavigator runtime) ||
                !runtime.TryReadTuning(out AutopilotTuningDTO tuning))
            {
                return;
            }

            switch (field)
            {
                case 0:
                    tuning.FeelerLength = value;
                    break;
                case 1:
                    tuning.RepulsionWeight = value;
                    break;
                case 2:
                    tuning.MaxTurnRateRadians = value;
                    break;
                case 3:
                    tuning.WaypointAcceptanceRadius = value;
                    break;
                case 4:
                    tuning.FlowCompensationWeight = value;
                    break;
                case 5:
                    tuning.GlobalQualityWeight = math.saturate(value);
                    break;
            }

            runtime.TryWriteTuning(in tuning);
        }

        private void ApplyHandlingProfile(uint profileHash)
        {
            if (!SubmarineAutopilotSdfNavigator.TryGetLatest(out SubmarineAutopilotSdfNavigator runtime))
                return;

            int index = math.max(0, _submarineIndex != null ? _submarineIndex.value : 0);
            runtime.TryWriteHandlingProfileHash(index, profileHash);
        }

        private void OnSceneGui(SceneView view)
        {
            bool targetMode = _sceneClickInjection != null && _sceneClickInjection.value;
            bool routeMode = _sceneClickRouteInjection != null && _sceneClickRouteInjection.value;
            if (!targetMode && !routeMode)
                return;

            Event current = Event.current;
            if (current == null)
                return;

            HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));
            if (current.type != EventType.MouseDown || current.button != 0 || current.alt)
                return;

            if (!SubmarineAutopilotSdfNavigator.TryGetLatest(out SubmarineAutopilotSdfNavigator runtime))
                return;

            int index = math.max(0, _submarineIndex != null ? _submarineIndex.value : 0);
            if (!runtime.TryReadAutopilotState(index, out AutopilotStateDTO state))
                return;

            Ray ray = HandleUtility.GUIPointToWorldRay(current.mousePosition);
            double runtimeY = state.TargetAUP.y - HectonFloatingOrigin.CurrentTotalOffsetDouble.y;
            float planeY = math.isfinite(runtimeY) ? (float)runtimeY : 0f;
            Plane plane = new Plane(Vector3.up, new Vector3(0f, planeY, 0f));
            if (!plane.Raycast(ray, out float enter))
                return;

            Vector3 point = ray.GetPoint(enter);
            double3 target = AbsoluteUniversePosition.FromRuntimePosition(point).ToAbsoluteDouble3();
            if (routeMode)
                InjectDoglegRoute(runtime, index, state, target);
            else
                runtime.TryWriteTargetAup(index, target, state.TargetSpeed);

            current.Use();
            SceneView.RepaintAll();
        }

        private void InjectDoglegRoute(SubmarineAutopilotSdfNavigator runtime, int index, AutopilotStateDTO state, double3 target)
        {
            Span<AutopilotWaypointDTO> route = stackalloc AutopilotWaypointDTO[3];
            double3 origin = math.all(math.isfinite(state.TargetAUP)) ? state.TargetAUP : target;
            double3 delta = target - origin;
            double3 side = ResolveDoglegSide(delta);
            double acceptance = math.max(1.0, _acceptanceRadius != null ? _acceptanceRadius.value : 10.0f);

            route[0] = BuildWaypoint(origin + delta * 0.33d + side, acceptance);
            route[1] = BuildWaypoint(origin + delta * 0.66d - side * 0.5d, acceptance);
            route[2] = BuildWaypoint(target, acceptance);
            runtime.TryWriteRoute(index, route, (float)acceptance, HashRoute(index, target));
        }

        private static AutopilotWaypointDTO BuildWaypoint(double3 target, double acceptance)
        {
            AutopilotWaypointDTO waypoint = default;
            waypoint.TargetAUP = target;
            waypoint.AcceptanceRadius = (float)math.max(1.0, acceptance);
            waypoint.Flags = SubmarineAutopilotConstants.WaypointFlagActive;
            return waypoint;
        }

        private static double3 ResolveDoglegSide(double3 delta)
        {
            double2 flat = new double2(delta.x, delta.z);
            double lenSq = math.lengthsq(flat);
            if (!math.isfinite(lenSq) || lenSq <= 0.0001d)
                return new double3(24.0d, 0.0d, 0.0d);

            double invLen = math.rsqrt(math.max(0.0001d, lenSq));
            double2 normal = new double2(-flat.y, flat.x) * invLen;
            double magnitude = math.clamp((lenSq * invLen) * 0.18d, 12.0d, 48.0d);
            return new double3(normal.x * magnitude, 0.0d, normal.y * magnitude);
        }

        private static uint HashRoute(int index, double3 target)
        {
            uint hash = 2166136261u;
            hash = (hash ^ (uint)math.max(0, index)) * 16777619u;
            hash = (hash ^ math.asuint((float)target.x)) * 16777619u;
            hash = (hash ^ math.asuint((float)target.y)) * 16777619u;
            hash = (hash ^ math.asuint((float)target.z)) * 16777619u;
            return hash != 0u ? hash : SubmarineAutopilotConstants.SourceHashAutopilot;
        }
    }
}
