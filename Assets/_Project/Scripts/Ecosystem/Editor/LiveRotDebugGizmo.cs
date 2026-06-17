#if UNITY_EDITOR
using Hecton8.Core;
using Hecton8.Ecosystem;
using Hecton8.World;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

namespace Hecton8.Editor
{
    [InitializeOnLoad]
    public static class LiveRotDebugGizmo
    {
        private const int MaxDrawnCarrion = 256;
        private static bool s_enabled;

        static LiveRotDebugGizmo()
        {
            SceneView.duringSceneGui -= DrawScene;
            SceneView.duringSceneGui += DrawScene;
        }

        [MenuItem("Hecton8/Ecosystem/Toggle Live Rot Debug Gizmo")]
        public static void Toggle()
        {
            s_enabled = !s_enabled;
            SceneView.RepaintAll();
        }

        [MenuItem("Hecton8/Ecosystem/Toggle Live Rot Debug Gizmo", true)]
        public static bool ValidateToggle()
        {
            Menu.SetChecked("HECTON-8/Ecosystem/Toggle Live Rot Debug Gizmo", s_enabled);
            return true;
        }

        private static void DrawScene(SceneView sceneView)
        {
            if (!s_enabled)
                return;

            AbsoluteUniversePosition origin = GlobalSignals.CurrentRuntimeOriginAup();
            if (!origin.IsFinite())
                return;

            double3 originAup = origin.ToAbsoluteDouble3();
            int drawn = 0;
            for (int i = 0; i < NutrientDriftRuntime.CarrionCapacity && drawn < MaxDrawnCarrion; i++)
            {
                if (!NutrientDriftRuntime.TryReadCarrionState(i, out CarrionStateDTO state) ||
                    (state.Flags & CarrionStateDTO.FlagActive) == 0u ||
                    !math.all(math.isfinite(state.CorpseAUP)))
                {
                    continue;
                }

                double3 delta = state.CorpseAUP - originAup;
                if (!math.all(math.isfinite(delta)))
                    continue;

                Vector3 basePosition = new Vector3((float)delta.x, (float)delta.y, (float)delta.z);
                if (!float.IsFinite(basePosition.x) || !float.IsFinite(basePosition.y) || !float.IsFinite(basePosition.z))
                    continue;

                float biomass01 = math.saturate(state.CurrentBiomass * math.rcp(math.max(0.0001f, state.InitialBiomass)));
                float toxicity01 = math.saturate(state.ToxicityEmissionRate);
                float height = math.lerp(0.15f, 3.0f, biomass01);
                Vector3 top = basePosition + Vector3.up * height;

                Handles.color = Color.Lerp(new Color(0.14f, 0.92f, 0.24f, 0.82f), new Color(0.62f, 0.08f, 0.86f, 0.88f), toxicity01);
                Handles.DrawAAPolyLine(4f, basePosition, top);
                Handles.DrawWireDisc(basePosition, Vector3.up, math.max(0.25f, state.CurrentBiomass * 0.006f));
                Handles.color = new Color(1f, 1f, 1f, 0.62f);
                Handles.DrawWireCube(top, Vector3.one * 0.18f);
                drawn++;
            }
        }
    }
}
#endif
