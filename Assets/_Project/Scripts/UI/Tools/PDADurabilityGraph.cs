// ============================================================================
// HECTON-8 — PDADurabilityGraph.cs
// LineRenderer-based zero-GC durability history graph.
// ============================================================================

using UnityEngine;

namespace Hecton8.UI
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(LineRenderer))]
    public sealed class PDADurabilityGraph : MonoBehaviour
    {
        private const int HistoryCapacity = 10;

        [SerializeField] private LineRenderer lineRenderer;

        private readonly float[] _historyValues = new float[HistoryCapacity];
        private readonly Vector3[] _linePositions = new Vector3[HistoryCapacity];
        private int _currentIndex = 0;
        private int _activeCount = 0;

        private void Awake()
        {
            if (lineRenderer == null)
            {
                lineRenderer = GetComponent<LineRenderer>();
            }
            lineRenderer.positionCount = HistoryCapacity;
            lineRenderer.useWorldSpace = false;
        }

        public void AddDurabilityValue(float value)
        {
            _historyValues[_currentIndex] = Mathf.Clamp01(value);
            _currentIndex = (_currentIndex + 1) % HistoryCapacity;

            if (_activeCount < HistoryCapacity)
                _activeCount++;

            UpdateDurabilityGraph();
        }

        public void UpdateDurabilityGraph()
        {
            if (_activeCount == 0) return;

            float width = 100f; // Could be exposed or calculated based on RectTransform
            float height = 50f;

            float stepX = width / Mathf.Max(1, HistoryCapacity - 1);

            for (int i = 0; i < HistoryCapacity; i++)
            {
                // Unroll circular buffer starting from oldest value
                int index = (_currentIndex - _activeCount + i + HistoryCapacity) % HistoryCapacity;

                // If not enough history, pad with the oldest known value
                float value = (i < HistoryCapacity - _activeCount) ? _historyValues[(_currentIndex - _activeCount + HistoryCapacity) % HistoryCapacity] : _historyValues[index];

                _linePositions[i] = new Vector3(i * stepX, value * height, 0f);
            }

            lineRenderer.SetPositions(_linePositions);
        }
    }
}
