#if UNITY_EDITOR
using System.IO;
using Hecton8.Core;
using Hecton8.Core.Memory;
using Hecton8.Gameplay;
using Unity.Collections;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Hecton8.EditorTools
{
    public sealed class SomaticTunerWindow : EditorWindow
    {
        private const float VectorScale = 0.35f;
        private const string ComfortCsvPath = "Data/UX/vr_comfort_profiles.csv";

        // COLD ALLOC: Vector3[300] - editor-only comfort telemetry graph scratch - owner: SomaticTunerWindow
        private static readonly Vector3[] s_graphPoints = new Vector3[300];
        private IMGUIContainer _uiToolkitComfortPanel;

        [MenuItem("HECTON-8/Somatic Tuner")]
        private static void Open()
        {
            GetWindow<SomaticTunerWindow>("Somatic Tuner");
        }

        [MenuItem("HECTON-8/Somatic Comfort Tuner")]
        private static void OpenComfort()
        {
            GetWindow<SomaticTunerWindow>("Somatic Comfort Tuner");
        }

        private void OnEnable()
        {
            SceneView.duringSceneGui -= OnSceneGui;
            SceneView.duringSceneGui += OnSceneGui;
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= OnSceneGui;
        }

        private void CreateGUI()
        {
            rootVisualElement.Clear();
            _uiToolkitComfortPanel = new IMGUIContainer(() => DrawComfortTuner(GlobalRegistry.DataVault));
            rootVisualElement.Add(_uiToolkitComfortPanel);
            rootVisualElement.schedule.Execute(() =>
            {
                if (_uiToolkitComfortPanel != null)
                    _uiToolkitComfortPanel.MarkDirtyRepaint();
            }).Every(250);
        }

        private void OnGUI()
        {
            IDataVault vault = GlobalRegistry.DataVault;
            if (vault == null ||
                !vault.TryGetBuffer(BufferID.ShinobuSomaticTuning, out NativeArray<SomaticKinematicsTuningData> tuningBuffer) ||
                !tuningBuffer.IsCreated ||
                tuningBuffer.Length == 0)
            {
                EditorGUILayout.LabelField("No SHINOBU kinematic tuning buffer.");
                DrawComfortTuner(vault);
                return;
            }

            SomaticKinematicsTuningData tuning = tuningBuffer[0];
            EditorGUI.BeginChangeCheck();
            tuning.BaseDrag = EditorGUILayout.Slider("Base Drag", tuning.BaseDrag, 0.01f, 8.0f);
            tuning.StrokeMultiplier = EditorGUILayout.Slider("Stroke Multiplier", tuning.StrokeMultiplier, 0.1f, 30.0f);
            tuning.SeaglideAcceleration = EditorGUILayout.Slider("Seaglide Acceleration", tuning.SeaglideAcceleration, 0.1f, 40.0f);
            tuning.SurfaceBuoyancy = EditorGUILayout.Slider("Surface Buoyancy", tuning.SurfaceBuoyancy, 0.1f, 40.0f);
            if (EditorGUI.EndChangeCheck())
                tuningBuffer[0] = tuning;

            if (vault.TryGetBuffer(BufferID.ShinobuSomaticBlackBoxCursor, out NativeArray<int> cursor) &&
                vault.TryGetBuffer(BufferID.ShinobuSomaticBlackBox, out NativeArray<SomaticKinematicBlackBoxEntry> ring) &&
                cursor.IsCreated &&
                ring.IsCreated &&
                cursor.Length > 0 &&
                ring.Length > 0)
            {
                int index = PositiveModulo(cursor[0] - 1, ring.Length);
                SomaticKinematicBlackBoxEntry entry = ring[index];
                EditorGUILayout.LabelField("Frame", entry.Frame.ToString());
                EditorGUILayout.LabelField("Velocity", ToVector3(entry.Velocity).ToString("F3"));
                EditorGUILayout.LabelField("Thrust", ToVector3(entry.RequestedThrust).ToString("F3"));
                EditorGUILayout.LabelField("Push-Out", ToVector3(entry.SdfPushOut).ToString("F3"));
            }

            DrawComfortTuner(vault);
        }

        private void OnSceneGui(SceneView sceneView)
        {
            IDataVault vault = GlobalRegistry.DataVault;
            if (vault == null ||
                !vault.TryGetBuffer(BufferID.ShinobuSomaticBlackBoxCursor, out NativeArray<int> cursor) ||
                !vault.TryGetBuffer(BufferID.ShinobuSomaticBlackBox, out NativeArray<SomaticKinematicBlackBoxEntry> ring) ||
                !cursor.IsCreated ||
                !ring.IsCreated ||
                cursor.Length == 0 ||
                ring.Length == 0)
            {
                return;
            }

            SomaticKinematicBlackBoxEntry entry = ring[PositiveModulo(cursor[0] - 1, ring.Length)];
            Vector3 origin = ToVector3(entry.LocalPosition);
            Handles.color = Color.blue;
            Handles.DrawLine(origin, origin + (ToVector3(entry.RequestedThrust) * VectorScale), 2.0f);
            Handles.color = Color.red;
            Handles.DrawLine(origin, origin + (ToVector3(entry.SdfPushOut) * 2.0f), 2.0f);
            Handles.color = Color.green;
            Handles.DrawLine(origin, origin + (ToVector3(entry.Velocity) * VectorScale), 2.0f);
        }

        private static void DrawComfortTuner(IDataVault vault)
        {
            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("VR Somatic Comfort", EditorStyles.boldLabel);
            if (vault == null)
            {
                EditorGUILayout.LabelField("No DataVault.");
                return;
            }

            if (vault.TryGetBuffer(BufferID.ShinobuVRSomaticProfile, out NativeArray<VrComfortProfileDTO> profiles) &&
                profiles.IsCreated &&
                profiles.Length > 0)
            {
                VrComfortProfileDTO profile = profiles[0];
                EditorGUI.BeginChangeCheck();
                profile.UserComfortWeight01 = EditorGUILayout.Slider("Comfort Weight", profile.UserComfortWeight01, 0f, 1f);
                profile.FovAggressiveness = EditorGUILayout.Slider("Tunneling Aggressiveness", profile.FovAggressiveness, 0f, 2f);
                profile.HorizonLockSpeed = EditorGUILayout.Slider("Horizon Lock Speed", profile.HorizonLockSpeed, 0f, 32f);
                profile.FoveatedBaseline = EditorGUILayout.Slider("Foveated Baseline", profile.FoveatedBaseline, 0f, 0.5f);
                profile.EwmaSharpness = EditorGUILayout.Slider("EWMA Sharpness", profile.EwmaSharpness, 0.1f, 40f);
                if (EditorGUI.EndChangeCheck())
                    profiles[0] = profile;

                if (GUILayout.Button("Import vr_comfort_profiles.csv"))
                    ImportComfortCsv(vault, profiles);
            }
            else
            {
                EditorGUILayout.LabelField("No VR comfort profile buffer.");
            }

            if (vault.TryGetBuffer(BufferID.ShinobuVRSomaticComfortRead, out NativeArray<SomaticComfortStateDTO> stateBuffer) &&
                stateBuffer.IsCreated &&
                stateBuffer.Length > 0)
            {
                SomaticComfortStateDTO state = stateBuffer[0];
                EditorGUILayout.FloatField("FOV Tunnel", state.FovTunnelingIntensity);
                EditorGUILayout.FloatField("Horizon Lock", state.HorizonLockBlend);
                EditorGUILayout.FloatField("Foveated Scale", state.FoveatedScaleMultiplier);
            }

            if (vault.TryGetBuffer(BufferID.ShinobuVRSomaticComfortTelemetry, out NativeArray<ComfortTelemetryEntry> telemetry) &&
                telemetry.IsCreated &&
                telemetry.Length > 1)
            {
                Rect rect = GUILayoutUtility.GetRect(320f, 96f, GUILayout.ExpandWidth(true));
                DrawComfortGraph(rect, telemetry);
            }
        }

        private static void ImportComfortCsv(IDataVault vault, NativeArray<VrComfortProfileDTO> profiles)
        {
            if (!File.Exists(ComfortCsvPath))
                return;

            byte[] bytes = File.ReadAllBytes(ComfortCsvPath);
            if (vault != null &&
                vault.TryGetBuffer(BufferID.ShinobuVRSomaticProfileLookup, out NativeArray<VrComfortProfileLookupSlotDTO> lookup) &&
                lookup.IsCreated)
            {
                VRSomaticProvider.ParseComfortProfilesCsv(bytes, profiles, lookup);
                return;
            }

            VRSomaticProvider.ParseComfortProfilesCsv(bytes, profiles);
        }

        private static void DrawComfortGraph(Rect rect, NativeArray<ComfortTelemetryEntry> telemetry)
        {
            EditorGUI.DrawRect(rect, new Color(0.06f, 0.07f, 0.08f, 1f));
            int count = Mathf.Min(s_graphPoints.Length, telemetry.Length);
            if (count < 2)
                return;

            BuildGraphPoints(rect, telemetry, count, true);
            Handles.color = new Color(0.25f, 0.65f, 1f, 1f);
            Handles.DrawAAPolyLine(2f, count, s_graphPoints);
            BuildGraphPoints(rect, telemetry, count, false);
            Handles.color = new Color(1f, 0.35f, 0.22f, 1f);
            Handles.DrawAAPolyLine(2f, count, s_graphPoints);
        }

        private static void BuildGraphPoints(Rect rect, NativeArray<ComfortTelemetryEntry> telemetry, int count, bool angularVelocity)
        {
            float step = rect.width / Mathf.Max(1, count - 1);
            for (int i = 0; i < count; i++)
            {
                ComfortTelemetryEntry entry = telemetry[i];
                float value = angularVelocity
                    ? Mathf.Clamp01(entry.PeakAngularVelocityRadS / 16f)
                    : Mathf.Clamp01(entry.FovTunnelingIntensity);
                s_graphPoints[i] = new Vector3(rect.x + (step * i), rect.yMax - (value * rect.height), 0f);
            }
        }

        private static int PositiveModulo(int value, int length)
        {
            int modulo = value % length;
            return modulo < 0 ? modulo + length : modulo;
        }

        private static Vector3 ToVector3(Unity.Mathematics.float3 value)
        {
            return new Vector3(value.x, value.y, value.z);
        }
    }
}
#endif
