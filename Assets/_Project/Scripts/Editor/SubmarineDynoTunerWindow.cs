#if UNITY_EDITOR
using Hecton8.Core;
using Hecton8.Core.Memory;
using Hecton8.Physics.Vehicles;
using Unity.Collections;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

namespace Hecton8.Editor
{
    public sealed class SubmarineDynoTunerWindow : EditorWindow
    {
        private const double RefreshIntervalSeconds = 0.5d;
        private double _nextRefreshTime;
        private bool _hasVault;
        private SubmarineKinematicState _state;
        private SubmarineKinematicConfig _config;
        private SubmarineForceAccumulator _force;

        [MenuItem("Hecton8/Debug/Submarine Dyno-Tuner")]
        public static void Open()
        {
            GetWindow<SubmarineDynoTunerWindow>("Submarine Dyno-Tuner");
        }

        private void OnEnable()
        {
            SceneView.duringSceneGui -= DrawSceneGizmos;
            SceneView.duringSceneGui += DrawSceneGizmos;
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= DrawSceneGizmos;
        }

        private void OnGUI()
        {
            RefreshSnapshots(false);
            if (!_hasVault)
            {
                EditorGUILayout.HelpBox("GlobalDataVault is not initialized.", MessageType.Warning);
                return;
            }

            EditorGUI.BeginChangeCheck();
            float baseMass = EditorGUILayout.Slider("Base Mass", _config.BaseMassKg, 1000f, 80000f);
            float dragScale = EditorGUILayout.Slider("Drag Coefficient", _config.DragScale, 0.05f, 8f);
            float pidP = EditorGUILayout.Slider("PID P", _config.PidP, 0f, 40000f);
            float pidI = EditorGUILayout.Slider("PID I", _config.PidI, 0f, 8000f);
            float pidD = EditorGUILayout.Slider("PID D", _config.PidD, 0f, 30000f);
            float gyro = EditorGUILayout.Slider("Gyroscopic Strength", _config.GyroStrength, 0f, 160000f);
            float thrust = EditorGUILayout.Slider("Max Thrust", _config.MaxThrustN, 0f, 160000f);

            if (EditorGUI.EndChangeCheck())
            {
                _config.BaseMassKg = math.max(1f, baseMass);
                _config.DragScale = math.max(0.01f, dragScale);
                _config.PidP = math.max(0f, pidP);
                _config.PidI = math.max(0f, pidI);
                _config.PidD = math.max(0f, pidD);
                _config.GyroStrength = math.max(0f, gyro);
                _config.MaxThrustN = math.max(0f, thrust);
                _config.SourceHash = SubmarineDynamicsConstants.SourceHashCsv;
                WriteConfigToVault(in _config);
            }

            EditorGUILayout.Space();
            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.Vector3Field("AUP Local Delta", ToVector3(_state.Aup - _config.LocalOriginAup));
            EditorGUILayout.Vector3Field("Local Position", ToVector3(_state.LocalPosition));
            EditorGUILayout.Vector3Field("Linear Velocity", ToVector3(_state.LinearVelocity));
            EditorGUILayout.Vector3Field("Angular Velocity", ToVector3(_state.AngularVelocity));
            EditorGUILayout.Vector3Field("CoM", ToVector3(_state.CenterOfMassLocal));
            EditorGUILayout.Vector3Field("CoB", ToVector3(_state.CenterOfBuoyancyLocal));
            EditorGUILayout.FloatField("Mass", _state.TotalMassKg);
            EditorGUILayout.FloatField("Cavitation", _force.CavitationIndex);
            EditorGUI.EndDisabledGroup();
        }

        private void Update()
        {
            if (EditorApplication.timeSinceStartup < _nextRefreshTime)
                return;

            _nextRefreshTime = EditorApplication.timeSinceStartup + RefreshIntervalSeconds;
            RefreshSnapshots(true);
        }

        private void DrawSceneGizmos(SceneView sceneView)
        {
            RefreshSnapshots(false);
            if (!_hasVault)
                return;

            Vector3 origin = ToVector3(_state.LocalPosition);
            Quaternion rotation = new Quaternion(_state.Rotation.value.x, _state.Rotation.value.y, _state.Rotation.value.z, _state.Rotation.value.w);
            Vector3 com = origin + (rotation * ToVector3(_state.CenterOfMassLocal));
            Vector3 cob = origin + (rotation * ToVector3(_state.CenterOfBuoyancyLocal));
            Vector3 thrust = ToVector3(_force.LastThrustWorld) * 0.00004f;

            Handles.color = Color.red;
            Handles.SphereHandleCap(0, com, Quaternion.identity, 0.35f, EventType.Repaint);
            Handles.color = Color.green;
            Handles.SphereHandleCap(0, cob, Quaternion.identity, 0.35f, EventType.Repaint);
            Handles.color = Color.blue;
            Handles.DrawAAPolyLine(4f, origin, origin + thrust);
        }

        private void RefreshSnapshots(bool repaint)
        {
            _hasVault = false;
            IDataVault vault = GlobalRegistry.DataVault;
            if (vault == null)
                return;

            if (TryReadFirst(vault, BufferID.SubmarineKinematicStates, out SubmarineKinematicState state))
                _state = state;

            if (TryReadFirst(vault, BufferID.SubmarineKinematicConfig, out SubmarineKinematicConfig config))
            {
                _config = config;
                _hasVault = true;
            }

            if (TryReadFirst(vault, BufferID.SubmarineKinematicForces, out SubmarineForceAccumulator force))
                _force = force;

            if (repaint)
                Repaint();
        }

        private static void WriteConfigToVault(in SubmarineKinematicConfig config)
        {
            IDataVault vault = GlobalRegistry.DataVault;
            if (vault == null)
                return;

            if (!vault.TryGetGenerationHandle(BufferID.SubmarineKinematicConfig, out VaultGenerationHandle<SubmarineKinematicConfig> configHandle))
            {
                return;
            }

            if (!vault.TryAcquireWriteLock(in configHandle, SystemID.CoreDiagnostics, out NativeArray<SubmarineKinematicConfig> configs))
                return;

            try
            {
                if (configs.IsCreated && configs.Length > 0)
                    configs[0] = config;
            }
            finally
            {
                vault.ReleaseWriteLock(in configHandle, SystemID.CoreDiagnostics);
            }
        }

        private static bool TryReadFirst<T>(IDataVault vault, BufferID bufferId, out T value)
            where T : struct
        {
            value = default;
            if (!vault.TryGetGenerationHandle(bufferId, out VaultGenerationHandle<T> handle))
                return false;

            if (!vault.TryReadHandle(in handle, out NativeArray<T> buffer) || !buffer.IsCreated || buffer.Length <= 0)
                return false;

            value = buffer[0];
            return true;
        }

        private static Vector3 ToVector3(float3 value)
        {
            return new Vector3(value.x, value.y, value.z);
        }

        private static Vector3 ToVector3(double3 value)
        {
            return new Vector3((float)value.x, (float)value.y, (float)value.z);
        }
    }
}
#endif
