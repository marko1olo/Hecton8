#if UNITY_EDITOR || DEVELOPMENT_BUILD
using Hecton8.Core;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using UnityEngine.UI;

namespace Hecton8.Bootstrap
{
    /// <summary>
    /// Developer-build BIOS strip that exposes bootstrap phase duration as a compact strip texture.
    /// </summary>
    [DisallowMultipleComponent]
    internal sealed class BootstrapHealthMonitor : MonoBehaviour
    {
        private const string RuntimeName = "[BootstrapHealthMonitor]";
        private const string TimelineName = "Timeline";
        private const int TextureWidth = 128;
        private const int TextureHeight = 1;
        private const int PhasePixelOffset = 0;
        private const int PhasePixelWidth = 64;
        private const int ServicePixelOffset = PhasePixelWidth;
        private const int ServicePixelWidth = TextureWidth - ServicePixelOffset;
        private const int StepCapacity = 8;
        private const int ServiceCapacity = 32;
        private const float SlowServiceMilliseconds = 10f;
#if UNITY_EDITOR
        private const string ShowTimelineOverlayEditorPref = "Hecton8.BootstrapHealthMonitor.ShowTimelineOverlay";
#endif

        // COLD ALLOC: Color32[128] - developer bootstrap timeline pixels - owner: BootstrapHealthMonitor
        private static readonly Color32[] _pixels = new Color32[TextureWidth * TextureHeight];
        // COLD ALLOC: float[8] - developer bootstrap phase duration cache - owner: BootstrapHealthMonitor
        private static readonly float[] _phaseDurations = new float[StepCapacity];
        // COLD ALLOC: float[32] - developer bootstrap service duration cache - owner: BootstrapHealthMonitor
        private static readonly float[] _serviceDurations = new float[ServiceCapacity];

        private static BootstrapHealthMonitor _runtime;

        private Texture2D _timelineTexture;
        private RawImage _timelineImage;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _runtime = null;
            System.Array.Clear(_pixels, 0, _pixels.Length);
            System.Array.Clear(_phaseDurations, 0, _phaseDurations.Length);
            System.Array.Clear(_serviceDurations, 0, _serviceDurations.Length);
        }

        /// <summary>
        /// Records a bootstrap phase duration and updates the developer overlay.
        /// </summary>
        public static void RecordPhaseDuration(BootstrapStepToken step, double elapsedMilliseconds)
        {
            int stepIndex = (int)step - 1;
            if ((uint)stepIndex >= (uint)StepCapacity)
                return;

            BootstrapHealthMonitor monitor = EnsureRuntime();
            if (monitor == null)
                return;

            _phaseDurations[stepIndex] = (float)elapsedMilliseconds;
            monitor.RebuildTimelineTexture();
        }

        /// <summary>
        /// Records a single service-node duration for the next phase-boundary texture update.
        /// </summary>
        public static void RecordServiceDuration(int serviceIndex, double elapsedMilliseconds)
        {
            if ((uint)serviceIndex >= (uint)ServiceCapacity)
                return;

            _serviceDurations[serviceIndex] = (float)elapsedMilliseconds;
        }

        private static BootstrapHealthMonitor EnsureRuntime()
        {
            if (!Application.isPlaying || Application.isBatchMode || !ShouldCreateTimelineOverlay())
                return null;

            if (_runtime != null)
                return _runtime;

            GameObject root = new GameObject(RuntimeName); // COLD ALLOC: GameObject[1] - developer bootstrap health strip root - owner: BootstrapHealthMonitor
            BootstrapHealthMonitor monitor = root.AddComponent<BootstrapHealthMonitor>();
            GameBootstrapper.PersistRuntimeService(monitor);
            return monitor;
        }

        private static bool ShouldCreateTimelineOverlay()
        {
#if UNITY_EDITOR
            return EditorPrefs.GetBool(ShowTimelineOverlayEditorPref, false);
#else
            return false;
#endif
        }

        private void Awake()
        {
            if (_runtime != null && _runtime != this)
            {
                Destroy(gameObject);
                return;
            }

            _runtime = this;
            BuildVisualTree();
        }

        private void OnDestroy()
        {
            if (ReferenceEquals(_runtime, this))
                _runtime = null;

            if (_timelineTexture != null)
            {
                Destroy(_timelineTexture);
                _timelineTexture = null;
            }

            _timelineImage = null;
        }

        private void BuildVisualTree()
        {
            if (_timelineImage != null)
                return;

            Canvas canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 32760;

            GameObject timelineRoot = new GameObject(TimelineName); // COLD ALLOC: GameObject[1] - developer bootstrap timeline node - owner: BootstrapHealthMonitor
            timelineRoot.transform.SetParent(transform, false);

            RectTransform rect = timelineRoot.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(12f, -12f);
            rect.sizeDelta = new Vector2(256f, 6f);

            _timelineImage = timelineRoot.AddComponent<RawImage>();
            _timelineImage.raycastTarget = false;
            _timelineTexture = new Texture2D(TextureWidth, TextureHeight, TextureFormat.RGBA32, false, true)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            _timelineImage.texture = _timelineTexture;
        }

        private void RebuildTimelineTexture()
        {
            if (_timelineTexture == null)
                return;

            Color32 background = new Color32(4, 8, 10, 180);
            for (int i = 0; i < _pixels.Length; i++)
                _pixels[i] = background;

            int segmentWidth = PhasePixelWidth / StepCapacity;
            for (int stepIndex = 0; stepIndex < StepCapacity; stepIndex++)
            {
                float elapsed = _phaseDurations[stepIndex];
                if (elapsed <= 0f)
                    continue;

                Color32 color = elapsed > SlowServiceMilliseconds
                    ? new Color32(220, 36, 32, 255)
                    : new Color32(52, 210, 164, 255);

                int xStart = PhasePixelOffset + (stepIndex * segmentWidth);
                int xEnd = stepIndex == StepCapacity - 1 ? PhasePixelOffset + PhasePixelWidth : xStart + segmentWidth;
                FillSpan(xStart + 1, xEnd - 1, color);
            }

            int serviceSegmentWidth = ServicePixelWidth / ServiceCapacity;
            if (serviceSegmentWidth < 1)
                serviceSegmentWidth = 1;
            for (int serviceIndex = 0; serviceIndex < ServiceCapacity; serviceIndex++)
            {
                float elapsed = _serviceDurations[serviceIndex];
                if (elapsed <= 0f)
                    continue;

                Color32 color = elapsed > SlowServiceMilliseconds
                    ? new Color32(255, 80, 48, 255)
                    : new Color32(42, 128, 220, 255);

                int xStart = ServicePixelOffset + (serviceIndex * serviceSegmentWidth);
                int xEnd = serviceIndex == ServiceCapacity - 1 ? TextureWidth : xStart + serviceSegmentWidth;
                FillSpan(xStart, xEnd, color);
            }

            _timelineTexture.SetPixelData(_pixels, 0);
            _timelineTexture.Apply(false, false);
        }

        private static void FillSpan(int xStart, int xEnd, Color32 color)
        {
            if (xStart < 0)
                xStart = 0;
            if (xEnd > TextureWidth)
                xEnd = TextureWidth;
            if (xEnd <= xStart)
                return;

            for (int x = xStart; x < xEnd; x++)
                _pixels[x] = color;
        }
    }
}
#endif
