using Hecton8.Audio;
using Hecton8.Core;
using Hecton8.Visor;
using Hecton8.World;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UI;

namespace Hecton8.UI
{
    /// <summary>
    /// Player-owned acoustic radar overlay that projects recent physics-impact emitters into the HUD as fading blips.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/UI/Sonar Holo Compass")]
    public sealed class SonarHoloCompass : MonoBehaviour, ITickable, ILateFrameTickable, ISonarPingEventListener
    {
        private const int MaxDots = 16;
        private const int ProjectionBatchSize = 4;
        private const float RootWidth = 188f;
        private const float RootHeight = 188f;
        private const float RingRadius = 74f;
        private const float VerticalRadius = 48f;
        private const float DotBaseSize = 8f;
        private const float DotPulseSize = 7f;
        private const float PingDecaySharpness = 4.2f;
        private const float HiddenAlphaCutoff = 0.001f;
        private const float ImpactRadarMaxDistanceMeters = 40f;
        private const float MinimumBlipEnergy = 0.001f;
        private const string RootName = "SonarHoloCompass";

        private static readonly Color FrameColor = new Color(0.48f, 0.95f, 0.92f, 0.16f);
        private static readonly Color DotFrontColor = new Color(0.70f, 0.98f, 0.96f, 0.94f);
        private static readonly Color DotRearColor = new Color(0.62f, 0.78f, 0.82f, 0.34f);
        private static Sprite s_quadSprite;

        private struct AcousticRadarBlipInput
        {
            public float3 AbsolutePosition;
            public float Amplitude;
        }

        private struct AcousticRadarBlipOutput
        {
            public float2 AnchoredPosition;
            public float Energy;
            public float DepthBlend;
            public int Visible;
        }

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private struct ProjectImpactBlipsJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<AcousticRadarBlipInput> Inputs;
            public NativeArray<AcousticRadarBlipOutput> Outputs;
            public float3 ListenerAbsolutePosition;
            public float3 CameraRight;
            public float3 CameraUp;
            public float3 CameraForward;
            public float RingRadius;
            public float VerticalRadius;
            public float MaxDistanceMeters;

            public void Execute(int index)
            {
                AcousticRadarBlipInput input = Inputs[index];
                float amplitude = math.saturate(input.Amplitude);
                if (amplitude <= 0f)
                {
                    Outputs[index] = default;
                    return;
                }

                float3 delta = input.AbsolutePosition - ListenerAbsolutePosition;
                float distanceSqr = math.lengthsq(delta);
                if (distanceSqr <= 0.0001f)
                {
                    Outputs[index] = default;
                    return;
                }

                float distance = math.sqrt(distanceSqr);
                if (distance > MaxDistanceMeters)
                {
                    Outputs[index] = default;
                    return;
                }

                float inverseDistance = math.rsqrt(distanceSqr);
                float3 direction = delta * inverseDistance;
                float x = math.clamp(math.dot(CameraRight, direction) * RingRadius, -RingRadius, RingRadius);
                float y = math.clamp(math.dot(CameraUp, direction) * VerticalRadius, -VerticalRadius, VerticalRadius);
                float depthBlend = math.dot(CameraForward, direction) >= 0f ? 1f : 0.35f;
                float distanceFade = 1f - math.saturate(distance / math.max(0.01f, MaxDistanceMeters));
                float energy = amplitude * distanceFade;

                Outputs[index] = new AcousticRadarBlipOutput
                {
                    AnchoredPosition = new float2(x, y),
                    Energy = energy,
                    DepthBlend = depthBlend,
                    Visible = energy > MinimumBlipEnergy ? 1 : 0
                };
            }
        }

        private bool _registeredToTick;
        private bool _registeredLateFrame;
        private bool _uiBuilt;
        private bool _projectionScheduled;
        private Canvas _targetCanvas;
        private Camera _viewCamera;
        private RectTransform _root;
        private CanvasGroup _canvasGroup;
        private RectTransform[] _dotRects;
        private Image[] _dotImages;
        private float _pingPulse;
        private float _lastRootAlpha = -1f;
        private int _pendingProjectionCount;

        // COLD ALLOC: ActiveEmitterSample[16] — impact-emitter copy buffer for acoustic radar projection — owner: SonarHoloCompass
        private readonly SpatialAudioManager.ActiveEmitterSample[] _impactEmitterSamples =
            new SpatialAudioManager.ActiveEmitterSample[MaxDots];
        // COLD ALLOC: NativeArray<AcousticRadarBlipInput>[16] — persistent Burst input scratch for radar projection — owner: SonarHoloCompass
        private NativeArray<AcousticRadarBlipInput> _projectionInputs;
        // COLD ALLOC: NativeArray<AcousticRadarBlipOutput>[16] — persistent Burst output scratch for radar projection — owner: SonarHoloCompass
        private NativeArray<AcousticRadarBlipOutput> _projectionOutputs;
        private JobHandle _projectionHandle;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            s_quadSprite = null;
        }

        private void OnEnable()
        {
            EnsureProjectionBuffers();
            ResolveOwners();
            EnsureUiBuilt();
            SpectrumEvents.RegisterSonarPingListener(this);
            RegisterToTickManager();
        }

        private void Start()
        {
            RegisterToTickManager();
        }

        private void OnDisable()
        {
            SpectrumEvents.UnregisterSonarPingListener(this);
            UnregisterFromTickManager();
            HideDots();
            ApplyRootAlpha(0f);
        }

        private void OnDestroy()
        {
            SpectrumEvents.UnregisterSonarPingListener(this);
            UnregisterFromTickManager();
            DisposeProjectionBuffers();
        }

        /// <inheritdoc />
        public void Tick(float dt)
        {
            ResolveOwners();
            EnsureUiBuilt();

            if (_pingPulse > 0f)
                _pingPulse = Mathf.Max(0f, _pingPulse - (dt * PingDecaySharpness));

            if (_canvasGroup == null || _root == null || _viewCamera == null)
            {
                if (_projectionScheduled)
                    return;
                HideDots();
                ApplyRootAlpha(0f);
                return;
            }

            if (_projectionScheduled)
                return;

            if (!(Hecton8.Core.GlobalRegistry.Audio is SpatialAudioManager audioManager))
            {
                HideDots();
                ApplyRootAlpha(0f);
                return;
            }

            int emitterCount = audioManager.CopyActiveImpactEmitterSamples(_impactEmitterSamples);
            if (emitterCount <= 0)
            {
                HideDots();
                ApplyRootAlpha(0f);
                return;
            }

            ScheduleProjection(emitterCount);
        }

        public void LateFrameTick()
        {
            TryCompleteProjectionIfScheduled();
        }

        private void HandleSonarPingSent(float intensity)
        {
            _pingPulse = Mathf.Max(_pingPulse, Mathf.Clamp01(intensity));
        }

        void ISonarPingEventListener.OnSonarPingSent(float intensity)
        {
            HandleSonarPingSent(intensity);
        }

        private void ResolveOwners()
        {
            if (_viewCamera == null)
            {
                IPlayerRuntimeContext playerContext = GlobalRegistry.Player;
                if (playerContext != null && playerContext.PlayerCamera != null)
                {
                    _viewCamera = playerContext.PlayerCamera;
                }
                else if (TryGetComponent(out Camera localCamera))
                {
                    _viewCamera = localCamera;
                }
                else
                {
                    _viewCamera = ComponentReferenceUtility.ResolveOwnedComponent<Camera>(transform);
                }
            }

            if (_targetCanvas == null)
                _targetCanvas = ResolveTargetCanvas();
        }

        private void EnsureProjectionBuffers()
        {
            if (!_projectionInputs.IsCreated)
            {
                _projectionInputs = new NativeArray<AcousticRadarBlipInput>(
                    MaxDots,
                    Allocator.Persistent,
                    NativeArrayOptions.ClearMemory);
                NativeMemorySentinel.RegisterNativeArray(
                    _projectionInputs,
                    nameof(SonarHoloCompass),
                    nameof(_projectionInputs),
                    NativeAllocationLifetime.Scene);
            }

            if (!_projectionOutputs.IsCreated)
            {
                _projectionOutputs = new NativeArray<AcousticRadarBlipOutput>(
                    MaxDots,
                    Allocator.Persistent,
                    NativeArrayOptions.ClearMemory);
                NativeMemorySentinel.RegisterNativeArray(
                    _projectionOutputs,
                    nameof(SonarHoloCompass),
                    nameof(_projectionOutputs),
                    NativeAllocationLifetime.Scene);
            }
        }

        private void DisposeProjectionBuffers()
        {
            JobHandle dependency = _projectionScheduled ? _projectionHandle : default;
            if (_projectionInputs.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeArray(_projectionInputs);
                _projectionInputs.Dispose(dependency);
            }

            if (_projectionOutputs.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeArray(_projectionOutputs);
                _projectionOutputs.Dispose(dependency);
            }

            _projectionInputs = default;
            _projectionOutputs = default;
            _projectionScheduled = false;
            _pendingProjectionCount = 0;
        }

        private void EnsureUiBuilt()
        {
            if (_uiBuilt || _targetCanvas == null)
                return;

            RectTransform canvasRoot = HectonUIScaler.ResolveContentRoot(_targetCanvas);
            if (canvasRoot == null)
                return;

            _root = FindExistingChild(canvasRoot, RootName);
            if (_root == null)
            {
                GameObject rootObject = new GameObject(RootName, typeof(RectTransform), typeof(CanvasGroup));
                rootObject.layer = canvasRoot.gameObject.layer;
                _root = rootObject.GetComponent<RectTransform>();
                _root.SetParent(canvasRoot, false);
            }

            _root.anchorMin = new Vector2(0.5f, 0f);
            _root.anchorMax = new Vector2(0.5f, 0f);
            _root.pivot = new Vector2(0.5f, 0.5f);
            _root.anchoredPosition = new Vector2(0f, 132f);
            _root.sizeDelta = new Vector2(RootWidth, RootHeight);
            _root.localScale = Vector3.one;
            _root.SetAsLastSibling();

            _canvasGroup = _root.GetComponent<CanvasGroup>();
            if (_canvasGroup == null)
                _canvasGroup = _root.gameObject.AddComponent<CanvasGroup>();
            _canvasGroup.interactable = false;
            _canvasGroup.blocksRaycasts = false;
            _canvasGroup.alpha = 0f;

            ClearChildren(_root);
            CreateFrame();
            CreateDots();
            _uiBuilt = true;
        }

        private void CreateFrame()
        {
            Image outerRing = EnsureImage(CreateRect(_root, "RingOuter").gameObject);
            outerRing.sprite = ResolveQuadSprite();
            outerRing.color = FrameColor;
            outerRing.raycastTarget = false;
            outerRing.type = Image.Type.Simple;
            outerRing.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            outerRing.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            outerRing.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            outerRing.rectTransform.sizeDelta = new Vector2(RootWidth - 18f, RootHeight - 18f);

            Image horizontalRule = EnsureImage(CreateRect(_root, "RuleH").gameObject);
            horizontalRule.sprite = ResolveQuadSprite();
            horizontalRule.color = FrameColor;
            horizontalRule.raycastTarget = false;
            horizontalRule.rectTransform.anchorMin = new Vector2(0f, 0.5f);
            horizontalRule.rectTransform.anchorMax = new Vector2(1f, 0.5f);
            horizontalRule.rectTransform.sizeDelta = new Vector2(0f, 1f);

            Image verticalRule = EnsureImage(CreateRect(_root, "RuleV").gameObject);
            verticalRule.sprite = ResolveQuadSprite();
            verticalRule.color = FrameColor;
            verticalRule.raycastTarget = false;
            verticalRule.rectTransform.anchorMin = new Vector2(0.5f, 0f);
            verticalRule.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            verticalRule.rectTransform.sizeDelta = new Vector2(1f, 0f);
        }

        private void CreateDots()
        {
            // COLD ALLOC: RectTransform[16] — prebuilt acoustic-radar marker pool — owner: SonarHoloCompass
            _dotRects = new RectTransform[MaxDots];
            // COLD ALLOC: Image[16] — prebuilt acoustic-radar marker visuals — owner: SonarHoloCompass
            _dotImages = new Image[MaxDots];

            for (int i = 0; i < MaxDots; i++)
            {
                RectTransform dotRect = CreateRect(_root, "Dot_" + i);
                dotRect.anchorMin = new Vector2(0.5f, 0.5f);
                dotRect.anchorMax = new Vector2(0.5f, 0.5f);
                dotRect.pivot = new Vector2(0.5f, 0.5f);
                dotRect.sizeDelta = new Vector2(DotBaseSize, DotBaseSize);

                Image dotImage = EnsureImage(dotRect.gameObject);
                dotImage.sprite = ResolveQuadSprite();
                dotImage.color = Color.clear;
                dotImage.raycastTarget = false;

                _dotRects[i] = dotRect;
                _dotImages[i] = dotImage;
            }
        }

        private void ScheduleProjection(int emitterCount)
        {
            EnsureProjectionBuffers();
            if (!_projectionInputs.IsCreated || !_projectionOutputs.IsCreated)
                return;

            int safeCount = Mathf.Clamp(emitterCount, 0, Mathf.Min(MaxDots, _impactEmitterSamples.Length));
            float3 listenerAbsolutePosition =
                HectonFloatingOrigin.ToAbsoluteUniversePosition(_viewCamera.transform.position);
            for (int i = 0; i < safeCount; i++)
            {
                SpatialAudioManager.ActiveEmitterSample sample = _impactEmitterSamples[i];
                _projectionInputs[i] = new AcousticRadarBlipInput
                {
                    AbsolutePosition = HectonFloatingOrigin.ToAbsoluteUniversePosition(sample.Position),
                    Amplitude = sample.Amplitude
                };
            }

            ProjectImpactBlipsJob job = new ProjectImpactBlipsJob
            {
                Inputs = _projectionInputs,
                Outputs = _projectionOutputs,
                ListenerAbsolutePosition = listenerAbsolutePosition,
                CameraRight = _viewCamera.transform.right,
                CameraUp = _viewCamera.transform.up,
                CameraForward = _viewCamera.transform.forward,
                RingRadius = RingRadius,
                VerticalRadius = VerticalRadius,
                MaxDistanceMeters = ImpactRadarMaxDistanceMeters
            };

            _pendingProjectionCount = safeCount;
            _projectionHandle = job.Schedule(safeCount, ProjectionBatchSize);
            _projectionScheduled = true;
        }

        private bool TryCompleteProjectionIfScheduled()
        {
            if (!_projectionScheduled)
                return true;

            if (!DispatcherJobSwap.TryComplete(ref _projectionHandle, false))
                return false;

            _projectionScheduled = false;
            ApplyProjectedDots(_pendingProjectionCount);
            _pendingProjectionCount = 0;
            return true;
        }

        private void ApplyProjectedDots(int activeCount)
        {
            if (_dotRects == null || _dotImages == null || !_projectionOutputs.IsCreated)
                return;

            int safeCount = Mathf.Clamp(activeCount, 0, Mathf.Min(MaxDots, _projectionOutputs.Length));
            float pulseScale = 1f + (_pingPulse * DotPulseSize / DotBaseSize);
            int visibleCount = 0;

            for (int i = 0; i < safeCount; i++)
            {
                RectTransform dotRect = _dotRects[i];
                Image dotImage = _dotImages[i];
                if (dotRect == null || dotImage == null)
                    continue;

                AcousticRadarBlipOutput blip = _projectionOutputs[i];
                if (blip.Visible == 0)
                {
                    HideDot(i);
                    continue;
                }

                float energy = Mathf.Clamp01(blip.Energy);
                dotRect.anchoredPosition = new Vector2(blip.AnchoredPosition.x, blip.AnchoredPosition.y);
                float size = DotBaseSize
                    * Mathf.Lerp(0.72f, 1.12f, blip.DepthBlend)
                    * Mathf.Lerp(0.78f, 1.36f, energy)
                    * pulseScale;
                dotRect.sizeDelta = new Vector2(size, size);

                Color color = Color.Lerp(DotRearColor, DotFrontColor, blip.DepthBlend);
                color.a *= Mathf.Lerp(0.25f, 1f, energy);
                dotImage.color = color;
                visibleCount++;
            }

            for (int i = safeCount; i < MaxDots; i++)
                HideDot(i);

            ApplyRootAlpha(visibleCount > 0 ? 1f : 0f);
        }

        private void HideDots()
        {
            if (_dotImages == null)
                return;

            for (int i = 0; i < _dotImages.Length; i++)
                HideDot(i);
        }

        private void HideDot(int index)
        {
            if (_dotImages == null || index < 0 || index >= _dotImages.Length)
                return;

            Image image = _dotImages[index];
            if (image != null && image.color.a > HiddenAlphaCutoff)
                image.color = Color.clear;
        }

        private void ApplyRootAlpha(float alpha)
        {
            if (_canvasGroup == null || Mathf.Approximately(_lastRootAlpha, alpha))
                return;

            _canvasGroup.alpha = alpha;
            _lastRootAlpha = alpha;
        }

        private void RegisterToTickManager()
        {
            if (_registeredToTick || !Application.isPlaying)
                return;

            if (GlobalRegistry.Dispatcher == null)
                return;

            GlobalRegistry.RegisterUpdatable(this, PriorityLayer.UI);
            _registeredToTick = GlobalRegistry.Updatables.Contains(this);
            GlobalRegistry.RegisterLateFrameTickable(this, PriorityLayer.UI);
            _registeredLateFrame = SystemDispatcher.GetLateFrameLane(PriorityLayer.UI).Contains(this);
        }

        private void UnregisterFromTickManager()
        {
            if (_registeredLateFrame)
            {
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.UI);
                _registeredLateFrame = false;
            }

            if (_registeredToTick)
            {
                GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.UI);
                _registeredToTick = false;
            }
        }

        private static Canvas ResolveTargetCanvas()
        {
            SuitHUDV4CanvasOverlay overlay = SuitHUDV4CanvasOverlay.ActiveRuntimeInstance;
            if (overlay != null && overlay.TargetCanvas != null)
                return overlay.TargetCanvas;

            return SuitHUDV4CanvasOverlay.ActiveRuntimeInstance != null
                ? SuitHUDV4CanvasOverlay.ActiveRuntimeInstance.GetComponent<Canvas>()
                : null;
        }

        private static Sprite ResolveQuadSprite()
        {
            if (s_quadSprite != null)
                return s_quadSprite;

            s_quadSprite = Sprite.Create(
                Texture2D.whiteTexture,
                new Rect(0f, 0f, Texture2D.whiteTexture.width, Texture2D.whiteTexture.height),
                new Vector2(0.5f, 0.5f),
                100f);
            s_quadSprite.name = "SonarHoloCompassQuad";
            return s_quadSprite;
        }

        private static RectTransform FindExistingChild(Transform parent, string name)
        {
            if (parent == null)
                return null;

            for (int i = 0; i < parent.childCount; i++)
            {
                Transform child = parent.GetChild(i);
                if (child.name == name)
                    return child as RectTransform;
            }

            return null;
        }

        private static void ClearChildren(Transform parent)
        {
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                Transform child = parent.GetChild(i);
                if (Application.isPlaying)
                    Object.Destroy(child.gameObject);
                else
                    Object.DestroyImmediate(child.gameObject);
            }
        }

        private static RectTransform CreateRect(Transform parent, string name)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.localScale = Vector3.one;
            return rect;
        }

        private static Image EnsureImage(GameObject target)
        {
            Image image = target.GetComponent<Image>();
            if (image == null)
                image = target.AddComponent<Image>();
            return image;
        }
    }
}
