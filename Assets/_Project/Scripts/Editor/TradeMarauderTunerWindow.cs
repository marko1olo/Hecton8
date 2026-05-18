#if UNITY_EDITOR
using System.IO;
using Hecton8.Economy;
using Unity.Collections;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

namespace Hecton8.Editor
{
    public sealed class TradeMarauderTunerWindow : EditorWindow
    {
        private float _basePriceVolatility = 0.35f;
        private float _marauderSpawnRate = 0.55f;
        private float _theftProbability = 0.18f;
        private float _aggressionScale = 0.35f;
        private int _acceptedRows;
        private int _rejectedRows;

        [MenuItem("Hecton8/Economy/Trade & Marauder Tuner")]
        public static void Open()
        {
            GetWindow<TradeMarauderTunerWindow>("Trade & Marauder Tuner");
        }

        private void OnEnable()
        {
            SceneView.duringSceneGui += OnDrawGizmos;
            PullFromVault();
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= OnDrawGizmos;
        }

        private void OnGUI()
        {
            TradeMarauderDirector director = TradeMarauderDirector.ActiveForEditor;
            using (new EditorGUI.DisabledScope(director == null))
            {
                _basePriceVolatility = EditorGUILayout.Slider("Base Price Volatility", _basePriceVolatility, 0f, 2f);
                _marauderSpawnRate = EditorGUILayout.Slider("Marauder Spawn Rate", _marauderSpawnRate, 0f, 1f);
                _theftProbability = EditorGUILayout.Slider("Theft Probability", _theftProbability, 0f, 1f);
                _aggressionScale = EditorGUILayout.Slider("Aggression Scale", _aggressionScale, 0f, 1f);

                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Pull Vault"))
                    PullFromVault();

                if (GUILayout.Button("Apply Vault") && director != null)
                    director.TrySetTuningFromEditor(_basePriceVolatility, _marauderSpawnRate, _theftProbability, _aggressionScale);
                EditorGUILayout.EndHorizontal();

                if (GUILayout.Button("Load faction_economy.csv") && director != null)
                    LoadCsv(director);
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("CSV Rows Accepted", _acceptedRows.ToString());
            EditorGUILayout.LabelField("CSV Rows Rejected", _rejectedRows.ToString());
            if (TradeMarauderDirector.ActiveForEditor == null)
                EditorGUILayout.HelpBox("No active TradeMarauderDirector in play mode.", MessageType.Info);
        }

        private void PullFromVault()
        {
            TradeMarauderDirector director = TradeMarauderDirector.ActiveForEditor;
            if (director == null || !director.TryGetTuningForEditor(out MarauderTradeTuningDTO tuning))
                return;

            _basePriceVolatility = tuning.BasePriceVolatility;
            _marauderSpawnRate = tuning.MarauderSpawnRate;
            _theftProbability = tuning.TheftProbability;
            _aggressionScale = tuning.AggressionScale;
            Repaint();
        }

        private void LoadCsv(TradeMarauderDirector director)
        {
            string path = EditorUtility.OpenFilePanel("faction_economy.csv", Directory.GetCurrentDirectory(), "csv");
            if (string.IsNullOrEmpty(path))
                return;

            byte[] bytes = File.ReadAllBytes(path);
            director.TryApplyCsvOverride(bytes, out _acceptedRows, out _rejectedRows);
            Repaint();
        }

        private void OnDrawGizmos(SceneView sceneView)
        {
            TradeMarauderDirector director = TradeMarauderDirector.ActiveForEditor;
            if (director == null ||
                !director.TryResolveEditorViews(
                    out NativeArray<MarauderStateDTO> states,
                    out NativeArray<MarauderRouteNodeDTO> routes,
                    out NativeArray<byte> routeCounts))
            {
                return;
            }

            int active = math.min(states.Length, TradeMarauderConstants.MaxMarauders);
            for (int i = 0; i < active; i++)
            {
                Vector3 marauder = ToEditorVector(states[i].AUP);
                Handles.color = Color.red;
                Handles.SphereHandleCap(0, marauder, Quaternion.identity, 220f, EventType.Repaint);

                int routeCount = i < routeCounts.Length ? routeCounts[i] : 0;
                int offset = i * TradeMarauderConstants.RouteNodeStride;
                Vector3 previous = marauder;
                Handles.color = new Color(0.1f, 0.45f, 1f, 0.9f);
                for (int n = 0; n < routeCount && offset + n < routes.Length; n++)
                {
                    Vector3 current = ToEditorVector(routes[offset + n].NodeAup);
                    Vector3 mid = (previous + current) * 0.5f + Vector3.up * 180f;
                    Handles.DrawBezier(previous, current, mid, mid, Handles.color, null, 8f);
                    previous = current;
                }
            }
        }

        private static Vector3 ToEditorVector(double3 aup)
        {
            return new Vector3((float)aup.x, (float)aup.y, (float)aup.z);
        }
    }
}
#endif
