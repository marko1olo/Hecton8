#if UNITY_EDITOR
using Hecton8.Core;
using Hecton8.Physics;
using Hecton8.World;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

namespace Hecton8.EditorTools
{
    public sealed class PhysicsCullingTunerWindow : EditorWindow
    {
        private const float GizmoCubeSizeMeters = 1.5f;
        private bool _drawGizmos = true;

        [MenuItem("HECTON-8/Physics/Physics Culling Tuner")]
        public static void Open()
        {
            GetWindow<PhysicsCullingTunerWindow>("Physics Culling Tuner");
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
            GlobalPhysicsStateManager manager = ResolveManager();
            if (manager == null)
            {
                EditorGUILayout.HelpBox("Global physics culling overseer is not registered.", MessageType.Warning);
                return;
            }

            if (!manager.TryGetPhysicsCullingTuning(out PhysicsCullingTuningDTO tuning))
            {
                EditorGUILayout.HelpBox("Physics culling vault buffers are not available.", MessageType.Warning);
                return;
            }

            EditorGUI.BeginChangeCheck();
            tuning.DebrisWakeRadiusMeters = EditorGUILayout.Slider("Debris Wake Radius", tuning.DebrisWakeRadiusMeters, 1f, 500f);
            tuning.VehicleWakeRadiusMeters = EditorGUILayout.Slider("Vehicle Wake Radius", tuning.VehicleWakeRadiusMeters, 1f, 1000f);
            tuning.FrustumClampDistanceMeters = EditorGUILayout.Slider("Frustum Clamp Distance", tuning.FrustumClampDistanceMeters, 20f, 1000f);
            tuning.HysteresisDelaySeconds = EditorGUILayout.Slider("Hysteresis Delay", tuning.HysteresisDelaySeconds, 0.1f, 10f);
            tuning.MockShockwaveRadiusMeters = EditorGUILayout.Slider("Mock Shockwave Radius", tuning.MockShockwaveRadiusMeters, 4f, 180f);
            _drawGizmos = EditorGUILayout.Toggle("Draw Sleep X-Ray", _drawGizmos);
            if (EditorGUI.EndChangeCheck())
                manager.SetPhysicsCullingTuning(in tuning);

            EditorGUILayout.Space();
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Generate 1000 Mock Bodies"))
                    manager.GenerateMockPhysicsBodies();
                if (GUILayout.Button("Fire Mock Seismic Wake"))
                    manager.FireMockSeismicShockwave((uint)math.max(1, Time.frameCount));
            }

            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.IntField("Tracked Bodies", manager.TrackedBodyCount);
                EditorGUILayout.IntField("Culled Bodies", manager.CulledBodyCount);
            }
        }

        private void OnDrawGizmos(SceneView sceneView)
        {
            if (!_drawGizmos)
                return;

            GlobalPhysicsStateManager manager = ResolveManager();
            if (manager == null)
                return;

            int count = manager.PhysicsCullingDebugBodyCount;
            for (int i = 0; i < count; i++)
            {
                if (!manager.TryGetPhysicsCullingDebugBody(i, out PhysicsCullingDebugBody body))
                    continue;

                AbsoluteUniversePosition aup = AbsoluteUniversePosition.FromAbsolutePosition(body.Aup);
                float3 runtime = aup.ToRuntimeFloat3();
                Vector3 position = new Vector3(runtime.x, runtime.y, runtime.z);
                Handles.color = body.IsHysteresisLocked != 0
                    ? Color.yellow
                    : body.IsAsleep != 0
                        ? Color.red
                        : Color.green;
                Handles.DrawWireCube(position, Vector3.one * GizmoCubeSizeMeters);
            }
        }

        private static GlobalPhysicsStateManager ResolveManager()
        {
            IPhysicsCullingOverseer overseer = GlobalRegistry.PhysicsCullingOverseer;
            if (overseer is GlobalPhysicsStateManager manager)
                return manager;

            return GlobalRegistry.PhysicsStateManager;
        }
    }
}
#endif
