#if UNITY_EDITOR
using Hecton8.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace Hecton8.UI
{
    /// <summary>
    /// Development-only UI Toolkit artery flush graph. No OnGUI, no runtime strings in Tick.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(UIDocument))]
    public sealed class EngineHealthOverlay : MonoBehaviour, IUpdatable
    {
        private const int SampleCapacity = 64;
        private const int SampleIntervalFrames = 10;
        private const float DefaultWidth = 192f;
        private const float DefaultHeight = 48f;
        private const float GraphBudgetMilliseconds = 5f;

        [SerializeField] private UIDocument uiDocument;
        [SerializeField] private bool visibleByDefault;
        [SerializeField] private KeyCode toggleKey = KeyCode.F10;
        [SerializeField] private bool requireControlForToggle = true;

        // COLD ALLOC: float[64] - dispatcher artery flush graph sample cache - owner: EngineHealthOverlay
        private readonly float[] _samples = new float[SampleCapacity];

        private VisualElement _root;
        private GraphElement _graph;
        private bool _registered;
        private bool _visible;
        private int _nextSampleFrame;

        private void Awake()
        {
            if (uiDocument == null)
                uiDocument = GetComponent<UIDocument>();
        }

        private void OnEnable()
        {
            if (uiDocument == null)
                uiDocument = GetComponent<UIDocument>();

            if (uiDocument == null)
                return;

            BuildVisualTree();
            SetVisible(visibleByDefault);
            TryRegister();
        }

        private void OnDisable()
        {
            Unregister();
            TeardownVisualTree();
        }

        public void Tick(float deltaTime)
        {
#if ENABLE_LEGACY_INPUT_MANAGER
            if (ShouldToggle())
                SetVisible(!_visible);
#endif
            if (!_visible)
                return;

            int frame = Time.frameCount;
            if (frame < _nextSampleFrame)
                return;

            _nextSampleFrame = frame + SampleIntervalFrames;
            int count = SystemDispatcher.CopyArteryFlushMilliseconds(_samples);
            _graph.SetSampleCount(count);
            _graph.MarkDirtyRepaint();
        }

        public void Toggle()
        {
            SetVisible(!_visible);
        }

        public void SetVisible(bool visible)
        {
            _visible = visible;
            if (_root != null)
                _root.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private bool ShouldToggle()
        {
            global::UnityEngine.InputSystem.Keyboard keyboard = global::UnityEngine.InputSystem.Keyboard.current;
            if (keyboard == null || toggleKey == KeyCode.None || !IsToggleKeyPressed(keyboard, toggleKey))
                return false;

            if (!requireControlForToggle)
                return true;

            return keyboard.leftCtrlKey.isPressed || keyboard.rightCtrlKey.isPressed;
        }

        private static bool IsToggleKeyPressed(global::UnityEngine.InputSystem.Keyboard keyboard, KeyCode key)
        {
            if (keyboard == null)
                return false;

            switch (key)
            {
                case KeyCode.F1: return keyboard.f1Key.wasPressedThisFrame;
                case KeyCode.F2: return keyboard.f2Key.wasPressedThisFrame;
                case KeyCode.F3: return keyboard.f3Key.wasPressedThisFrame;
                case KeyCode.F4: return keyboard.f4Key.wasPressedThisFrame;
                case KeyCode.F5: return keyboard.f5Key.wasPressedThisFrame;
                case KeyCode.F6: return keyboard.f6Key.wasPressedThisFrame;
                case KeyCode.F7: return keyboard.f7Key.wasPressedThisFrame;
                case KeyCode.F8: return keyboard.f8Key.wasPressedThisFrame;
                case KeyCode.F9: return keyboard.f9Key.wasPressedThisFrame;
                case KeyCode.F10: return keyboard.f10Key.wasPressedThisFrame;
                case KeyCode.F11: return keyboard.f11Key.wasPressedThisFrame;
                case KeyCode.F12: return keyboard.f12Key.wasPressedThisFrame;
                default: return false;
            }
        }

        private void TryRegister()
        {
            if (_registered || !Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            GlobalRegistry.RegisterUpdatable(this, PriorityLayer.UI);
            _registered = SystemDispatcher.GetLane(PriorityLayer.UI).Contains(this);
        }

        private void Unregister()
        {
            if (!_registered)
                return;

            GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.UI);
            _registered = false;
        }

        private void BuildVisualTree()
        {
            if (_root != null)
                return;

            VisualElement root = uiDocument.rootVisualElement;
            if (root == null)
                return;

            _root = new VisualElement(); // COLD ALLOC: VisualElement[1] - development engine health overlay root - owner: EngineHealthOverlay
            _root.name = "engine-health-overlay";
            _root.pickingMode = PickingMode.Ignore;
            _root.style.position = Position.Absolute;
            _root.style.left = 8f;
            _root.style.bottom = 8f;
            _root.style.width = DefaultWidth;
            _root.style.height = DefaultHeight;
            _root.style.backgroundColor = new Color(0f, 0f, 0f, 0.88f);
            _root.style.borderTopWidth = 1f;
            _root.style.borderRightWidth = 1f;
            _root.style.borderBottomWidth = 1f;
            _root.style.borderLeftWidth = 1f;
            _root.style.borderTopColor = new Color(0.55f, 1f, 0.55f, 1f);
            _root.style.borderRightColor = new Color(0.55f, 1f, 0.55f, 1f);
            _root.style.borderBottomColor = new Color(0.55f, 1f, 0.55f, 1f);
            _root.style.borderLeftColor = new Color(0.55f, 1f, 0.55f, 1f);

            _graph = new GraphElement(_samples, GraphBudgetMilliseconds); // COLD ALLOC: GraphElement[1] - artery flush 1-bit CRT graph - owner: EngineHealthOverlay
            _graph.style.flexGrow = 1f;
            _graph.pickingMode = PickingMode.Ignore;

            _root.Add(_graph);
            root.Add(_root);
        }

        private void TeardownVisualTree()
        {
            if (_root == null)
                return;

            _root.RemoveFromHierarchy();
            _graph = null;
            _root = null;
        }

        private sealed class GraphElement : VisualElement
        {
            private readonly float[] _samples;
            private readonly float _budgetMilliseconds;
            private int _sampleCount;

            public GraphElement(float[] samples, float budgetMilliseconds)
            {
                _samples = samples;
                _budgetMilliseconds = Mathf.Max(0.001f, budgetMilliseconds);
                generateVisualContent += DrawGraph;
            }

            public void SetSampleCount(int sampleCount)
            {
                _sampleCount = Mathf.Clamp(sampleCount, 0, _samples.Length);
            }

            private void DrawGraph(MeshGenerationContext context)
            {
                Rect rect = contentRect;
                if (_sampleCount <= 0 || rect.width <= 1f || rect.height <= 1f)
                    return;

                Painter2D painter = context.painter2D;
                DrawScanlines(painter, rect);
                DrawBudgetLine(painter, rect);
                DrawSamples(painter, rect);
            }

            private static void DrawScanlines(Painter2D painter, Rect rect)
            {
                painter.strokeColor = new Color(0.08f, 0.35f, 0.08f, 0.38f);
                painter.lineWidth = 1f;
                float y = rect.y + 4f;
                while (y < rect.yMax)
                {
                    painter.BeginPath();
                    painter.MoveTo(new Vector2(rect.x, y));
                    painter.LineTo(new Vector2(rect.xMax, y));
                    painter.Stroke();
                    y += 4f;
                }
            }

            private void DrawBudgetLine(Painter2D painter, Rect rect)
            {
                float y = rect.yMax - Mathf.Clamp01(_budgetMilliseconds / (_budgetMilliseconds * 2f)) * rect.height;
                painter.strokeColor = new Color(0.65f, 1f, 0.65f, 0.5f);
                painter.lineWidth = 1f;
                painter.BeginPath();
                painter.MoveTo(new Vector2(rect.x, y));
                painter.LineTo(new Vector2(rect.xMax, y));
                painter.Stroke();
            }

            private void DrawSamples(Painter2D painter, Rect rect)
            {
                float step = rect.width / Mathf.Max(1, _sampleCount - 1);
                float graphMax = _budgetMilliseconds * 2f;
                for (int i = 0; i < _sampleCount; i++)
                {
                    float value = Mathf.Clamp(_samples[i], 0f, graphMax);
                    float normalized = value / graphMax;
                    float x = rect.x + step * i;
                    float y = rect.yMax - normalized * rect.height;
                    painter.strokeColor = value > _budgetMilliseconds
                        ? new Color(1f, 1f, 1f, 1f)
                        : new Color(0.45f, 1f, 0.45f, 1f);
                    painter.lineWidth = 2f;
                    painter.BeginPath();
                    painter.MoveTo(new Vector2(x, rect.yMax));
                    painter.LineTo(new Vector2(x, y));
                    painter.Stroke();
                }
            }
        }
    }
}
#endif
