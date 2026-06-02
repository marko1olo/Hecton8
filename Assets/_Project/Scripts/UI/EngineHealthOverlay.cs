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
    public sealed class EngineHealthOverlay : MonoBehaviour, ILateFrameTickable, IGlobalRegistryHotSwapListener
    {
        private const int SampleCapacity = 64;
        private const int SampleIntervalFrames = 10;
        private const float DefaultWidth = 192f;
        private const float DefaultHeight = 48f;
        private const float GraphBudgetMilliseconds = 5f;

        [SerializeField] private UIDocument uiDocument;
        [SerializeField] private bool visibleByDefault;

        // COLD ALLOC: float[64] - dispatcher artery flush graph sample cache - owner: EngineHealthOverlay
        private readonly float[] _samples = new float[SampleCapacity];

        private VisualElement _root;
        private GraphElement _graph;
        private bool _registered;
        private bool _hotSwapListenerRegistered;
        private bool _visible;
        private int _nextSampleFrame;
        private INativeInputManagerRuntime _inputManager;

        private void Awake()
        {
            if (uiDocument == null)
                TryGetComponent(out uiDocument);
        }

        private void OnEnable()
        {
            if (uiDocument == null)
                TryGetComponent(out uiDocument);

            if (uiDocument == null)
                return;

            BuildVisualTree();
            SetVisible(visibleByDefault);
            TrySubscribeInput();
            TryRegisterHotSwapListener();
            TryRegister();
        }

        private void Start()
        {
            TrySubscribeInput();
            TryRegisterHotSwapListener();
        }

        private void OnDisable()
        {
            UnsubscribeInput();
            TryUnregisterHotSwapListener();
            Unregister();
            TeardownVisualTree();
        }

        public void LateFrameTick()
        {
            if (!_visible)
                return;

            int frame = Hecton8.Core.SystemDispatcher.CurrentFrameIndex;
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

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot != GlobalRegistryServiceSlot.NativeInputManagerRuntime)
                return;

            UnsubscribeInput();

            if (!isActiveAndEnabled)
                return;

            TrySubscribeInput(currentService as INativeInputManagerRuntime);
        }

        private void TryRegister()
        {
            if (_registered || !Application.isPlaying)
                return;

            _registered = SystemDispatcher.Register((ILateFrameTickable)this, PriorityLayer.UI);
        }

        private void TryRegisterHotSwapListener()
        {
            if (_hotSwapListenerRegistered || !Application.isPlaying)
                return;

            _hotSwapListenerRegistered = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterHotSwapListener()
        {
            if (!_hotSwapListenerRegistered)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _hotSwapListenerRegistered = false;
        }

        private void TrySubscribeInput()
        {
            if (_inputManager != null)
                return;

            TrySubscribeInput(GlobalRegistry.NativeInputRuntime);
        }

        private void TrySubscribeInput(INativeInputManagerRuntime inputManager)
        {
            if (_inputManager != null || inputManager == null)
                return;

            _inputManager = inputManager;
            _inputManager.OnDebugToggleEngineHealthOverlay += HandleDebugToggleEngineHealthOverlay;
        }

        private void UnsubscribeInput()
        {
            if (_inputManager == null)
                return;

            _inputManager.OnDebugToggleEngineHealthOverlay -= HandleDebugToggleEngineHealthOverlay;
            _inputManager = null;
        }

        private void HandleDebugToggleEngineHealthOverlay()
        {
            Toggle();
        }

        private void Unregister()
        {
            if (!_registered)
                return;

            SystemDispatcher.UnregisterLateFrameTickableDirect(this, PriorityLayer.UI);
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
                _budgetMilliseconds = budgetMilliseconds > 0.001f ? budgetMilliseconds : 0.001f;
                generateVisualContent += DrawGraph;
            }

            public void SetSampleCount(int sampleCount)
            {
                if (sampleCount <= 0)
                    _sampleCount = 0;
                else if (sampleCount >= _samples.Length)
                    _sampleCount = _samples.Length;
                else
                    _sampleCount = sampleCount;
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
                float y = rect.yMax - rect.height * 0.5f;
                painter.strokeColor = new Color(0.65f, 1f, 0.65f, 0.5f);
                painter.lineWidth = 1f;
                painter.BeginPath();
                painter.MoveTo(new Vector2(rect.x, y));
                painter.LineTo(new Vector2(rect.xMax, y));
                painter.Stroke();
            }

            private void DrawSamples(Painter2D painter, Rect rect)
            {
                int divisor = _sampleCount > 1 ? _sampleCount - 1 : 1;
                float step = rect.width / divisor;
                float graphMax = _budgetMilliseconds * 2f;
                for (int i = 0; i < _sampleCount; i++)
                {
                    float value = _samples[i];
                    if (value < 0f)
                        value = 0f;
                    else if (value > graphMax)
                        value = graphMax;

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
