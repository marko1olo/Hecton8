#if UNITY_EDITOR
using Hecton8.Core;
using UnityEditor;
using UnityEngine;

namespace Hecton8.Editor
{
    public sealed class ExecutionPipelineXRayWindow : EditorWindow
    {
        private const float BudgetMs = 16.6667f;
        private const float PhaseBudgetMs = 4.0f;
        private static readonly float[] _phaseMs = new float[4];
        private static readonly uint[] _bucketLoads = new uint[64];
        private double _nextRepaintTime;

        [MenuItem("Hecton/Diagnostics/Execution Pipeline X-Ray")]
        public static void Open()
        {
            ExecutionPipelineXRayWindow window = GetWindow<ExecutionPipelineXRayWindow>();
            window.titleContent = new GUIContent("Execution Pipeline X-Ray");
            window.minSize = new Vector2(520f, 420f);
            window.Show();
        }

        private void OnEnable()
        {
            EditorApplication.update += TickRepaint;
        }

        private void OnDisable()
        {
            EditorApplication.update -= TickRepaint;
        }

        private void TickRepaint()
        {
            double now = EditorApplication.timeSinceStartup;
            if (now < _nextRepaintTime)
                return;

            _nextRepaintTime = now + 0.25d;
            Repaint();
        }

        private void OnGUI()
        {
            if (!SystemDispatcher.TryGetExecutionPipelineXRaySnapshot(_phaseMs, _bucketLoads, out DispatcherStateDTO state))
            {
                EditorGUILayout.LabelField("SystemDispatcher is not active.");
                return;
            }

            DrawPhaseBars();
            GUILayout.Space(12f);
            DrawStateLine(in state);
            GUILayout.Space(10f);
            DrawBucketGrid();
        }

        private static void DrawPhaseBars()
        {
            DrawPhaseBar("PRE_SIMULATION", _phaseMs[0], PhaseBudgetMs);
            DrawPhaseBar("SIM_WAIT", _phaseMs[1], 8.0f);
            DrawPhaseBar("POST_SIMULATION", _phaseMs[2], PhaseBudgetMs);
            DrawPhaseBar("VISUAL_SYNC", _phaseMs[3], PhaseBudgetMs);
        }

        private static void DrawPhaseBar(string label, float valueMs, float budgetMs)
        {
            Rect row = GUILayoutUtility.GetRect(1f, 28f, GUILayout.ExpandWidth(true));
            Rect labelRect = new Rect(row.x, row.y, 142f, row.height);
            Rect barRect = new Rect(row.x + 148f, row.y + 5f, row.width - 220f, 18f);
            Rect valueRect = new Rect(row.xMax - 66f, row.y, 66f, row.height);

            EditorGUI.LabelField(labelRect, label);
            EditorGUI.DrawRect(barRect, new Color(0.08f, 0.08f, 0.08f, 1f));

            float normalized = Mathf.Clamp01(valueMs / Mathf.Max(0.001f, BudgetMs));
            Color color = valueMs > budgetMs ? new Color(1f, 0.08f, 0.04f, 1f) : new Color(0.1f, 0.65f, 0.95f, 1f);
            Rect fill = new Rect(barRect.x, barRect.y, barRect.width * normalized, barRect.height);
            EditorGUI.DrawRect(fill, color);
            EditorGUI.LabelField(valueRect, valueMs.ToString("0.00") + " ms");
        }

        private static void DrawStateLine(in DispatcherStateDTO state)
        {
            EditorGUILayout.LabelField(
                "Frame " + state.CurrentFrame +
                " | Phase " + state.CurrentPhaseId +
                " | Bucket " + state.ActiveBucket +
                " | Systems " + state.SortedSystemCount +
                " | Disabled " + state.DisabledSystemCount);
        }

        private static void DrawBucketGrid()
        {
            uint maxLoad = 1u;
            for (int i = 0; i < _bucketLoads.Length; i++)
            {
                if (_bucketLoads[i] > maxLoad)
                    maxLoad = _bucketLoads[i];
            }

            float cell = 24f;
            float gap = 4f;
            Rect grid = GUILayoutUtility.GetRect((cell + gap) * 8f, (cell + gap) * 8f);
            for (int y = 0; y < 8; y++)
            {
                for (int x = 0; x < 8; x++)
                {
                    int index = y * 8 + x;
                    float load01 = Mathf.Clamp01(_bucketLoads[index] / (float)maxLoad);
                    Color color = Color.Lerp(new Color(0.05f, 0.16f, 0.16f, 1f), new Color(0.95f, 0.45f, 0.05f, 1f), load01);
                    Rect cellRect = new Rect(grid.x + x * (cell + gap), grid.y + y * (cell + gap), cell, cell);
                    EditorGUI.DrawRect(cellRect, color);
                    EditorGUI.LabelField(cellRect, index.ToString(), EditorStyles.centeredGreyMiniLabel);
                }
            }
        }
    }
}
#endif
