using System.Runtime.InteropServices;
using Hecton8.Core;
using Hecton8.Core.Contracts;
using Hecton8.Gameplay;
using Hecton8.Visor;
using Hecton8.World;
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
    public sealed class SonarHoloCompass : MonoBehaviour, ILateFrameTickable, ISonarPingEventListener, IGlobalRegistryHotSwapListener
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
        private const float ImpactRadarMaxDistanceMetersSq = ImpactRadarMaxDistanceMeters * ImpactRadarMaxDistanceMeters;
        private const float MinimumBlipEnergy = 0.001f;
        private const float DotPositionEpsilonSq = 0.0004f;
        private const float DotSizeEpsilon = 0.025f;
        private const float DotColorEpsilonSq = 0.000001f;
        private const string RootName = "SonarHoloCompass";
        private const string DotName = "Dot";

        private static readonly Color FrameColor = new Color(0.48f, 0.95f, 0.92f, 0.16f);
        private static readonly Color DotFrontColor = new Color(0.70f, 0.98f, 0.96f, 0.94f);
        private static readonly Color DotRearColor = new Color(0.62f, 0.78f, 0.82f, 0.34f);

        [StructLayout(LayoutKind.Explicit, Size = 16)]
        private struct AcousticRadarBlipInput
        {
            [FieldOffset(0)]
            public float3 ListenerRelativePosition;
            [FieldOffset(12)]
            public float Amplitude;
        }

        [StructLayout(LayoutKind.Explicit, Size = 24)]
        private struct AcousticRadarBlipOutput
        {
            [FieldOffset(0)]
            public float2 AnchoredPosition;
            [FieldOffset(8)]
            public float Energy;
            [FieldOffset(12)]
            public float DepthBlend;
            [FieldOffset(16)]
            public int Visible;
            [FieldOffset(20)]
            private uint _pad0;
        }

        private bool _registeredLateFrame;
        private bool _uiBuilt;
        private bool _projectionScheduled;
        private bool _hideDotsQueued;
        private bool _hotSwapListenerRegistered;
        private Canvas _targetCanvas;
        private Camera _viewCamera;
        private IPlayerRuntimeContext _cachedPlayerContext;
        private ISpatialAudioImpactEmitterReadModel _cachedAudioManager;
        private RectTransform _root;
        private CanvasGroup _canvasGroup;
        private RectTransform[] _dotRects;
        private Image[] _dotImages;
        private Vector2[] _lastDotAnchoredPositions;
        private float[] _lastDotSizes;
        private Color[] _lastDotColors;
        private bool[] _dotVisibleFlags;
        private float _pingPulse;
        private float _lastRootAlpha = -1f;
        private int _pendingProjectionCount;
        private bool _dotsHidden = true;

        // COLD ALLOC: SpatialAudioImpactEmitterSample[16] - impact-emitter copy buffer with cached AUP for acoustic radar projection - owner: SonarHoloCompass
        private readonly SpatialAudioImpactEmitterSample[] _impactEmitterSamples =
            new SpatialAudioImpactEmitterSample[MaxDots];
        // COLD ALLOC: AcousticRadarBlipInput[16] - impact radar input scratch for deterministic projection - owner: SonarHoloCompass
        private AcousticRadarBlipInput[] _projectionInputs;
        // COLD ALLOC: AcousticRadarBlipOutput[16] - impact radar output scratch for deterministic projection - owner: SonarHoloCompass
        private AcousticRadarBlipOutput[] _projectionOutputs;

        private void OnEnable()
        {
            CacheRegistryServicesCold();
            EnsureProjectionBuffers();
            ResolveOwners(allowHierarchySearch: true);
            EnsureUiBuilt(allowCreate: true);
            SpectrumEvents.RegisterSonarPingListener(this);
            RegisterToTickManager();
            TryRegisterHotSwapListener();
        }

        private void Start()
        {
            ResolveOwners(allowHierarchySearch: true);
            EnsureUiBuilt(allowCreate: true);
            RegisterToTickManager();
        }

        private void OnDisable()
        {
            SpectrumEvents.UnregisterSonarPingListener(this);
            TryUnregisterHotSwapListener();
            UnregisterFromTickManager();
            HideDots();
            ApplyRootAlpha(0f);
        }

        private void OnDestroy()
        {
            SpectrumEvents.UnregisterSonarPingListener(this);
            TryUnregisterHotSwapListener();
            UnregisterFromTickManager();
            DisposeProjectionBuffers();
        }

        /// <inheritdoc />
        private void AdvanceCompassProjection(float dt)
        {
            ResolveOwners(allowHierarchySearch: false);
            if (!EnsureUiBuilt(allowCreate: false))
            {
                if (_projectionScheduled)
                    return;
                QueueHideDots();
                return;
            }

            if (_pingPulse > 0f)
                _pingPulse = math.max(0f, _pingPulse - (dt * PingDecaySharpness));

            if (_canvasGroup == null || _root == null || _viewCamera == null)
            {
                if (_projectionScheduled)
                    return;
                QueueHideDots();
                return;
            }

            if (_projectionScheduled)
                return;

            ISpatialAudioImpactEmitterReadModel audioManager = _cachedAudioManager;
            if (audioManager == null)
            {
                QueueHideDots();
                return;
            }

            int emitterCount = audioManager.CopyActiveImpactEmitterSamples(_impactEmitterSamples);
            if (emitterCount <= 0)
            {
                QueueHideDots();
                return;
            }

            ScheduleProjection(emitterCount);
        }

        public void LateFrameTick()
        {
            AdvanceCompassProjection(SystemDispatcher.CurrentFrameDeltaTime);

            if (TryCompleteProjectionIfScheduled() && _hideDotsQueued)
            {
                _hideDotsQueued = false;
                HideDots();
                ApplyRootAlpha(0f);
            }
        }

        private void HandleSonarPingSent(float intensity)
        {
            _pingPulse = math.max(_pingPulse, math.saturate(intensity));
        }

        void ISonarPingEventListener.OnSonarPingSent(float intensity)
        {
            HandleSonarPingSent(intensity);
        }

        private void ResolveOwners(bool allowHierarchySearch)
        {
            if (_viewCamera == null)
            {
                IPlayerRuntimeContext playerContext = _cachedPlayerContext;
                if (playerContext != null && playerContext.PlayerCamera != null)
                {
                    _viewCamera = playerContext.PlayerCamera;
                }
                else if (allowHierarchySearch)
                {
                    if (TryGetComponent(out Camera localCamera))
                    {
                        _viewCamera = localCamera;
                    }
                    else
                    {
                        _viewCamera = ComponentReferenceUtility.ResolveOwnedComponent<Camera>(transform);
                    }
                }
            }

            if (_targetCanvas == null)
                _targetCanvas = ResolveTargetCanvas(allowComponentFallback: allowHierarchySearch);
        }

        private void EnsureProjectionBuffers()
        {
            if (_projectionInputs == null)
                _projectionInputs = new AcousticRadarBlipInput[MaxDots];
            if (_projectionOutputs == null)
                _projectionOutputs = new AcousticRadarBlipOutput[MaxDots];
        }

        private void DisposeProjectionBuffers()
        {
            _projectionInputs = null;
            _projectionOutputs = null;
            _projectionScheduled = false;
            _pendingProjectionCount = 0;
        }

        private bool EnsureUiBuilt(bool allowCreate)
        {
            if (_uiBuilt)
                return true;

            if (!allowCreate || _targetCanvas == null)
                return false;

            RectTransform canvasRoot = HectonUIScaler.ResolveContentRoot(_targetCanvas);
            if (canvasRoot == null)
                return false;

            _root = FindExistingChild(canvasRoot, RootName);
            if (_root == null)
            {
                GameObject rootObject = new GameObject(RootName, typeof(RectTransform), typeof(CanvasGroup));
                rootObject.layer = canvasRoot.gameObject.layer;
                rootObject.TryGetComponent(out _root);
                _root.SetParent(canvasRoot, false);
            }

            _root.anchorMin = new Vector2(0.5f, 0f);
            _root.anchorMax = new Vector2(0.5f, 0f);
            _root.pivot = new Vector2(0.5f, 0.5f);
            _root.anchoredPosition = new Vector2(0f, 132f);
            _root.sizeDelta = new Vector2(RootWidth, RootHeight);
            _root.localScale = Vector3.one;
            _root.SetAsLastSibling();

            _root.TryGetComponent(out _canvasGroup);
            if (_canvasGroup == null)
                _canvasGroup = _root.gameObject.AddComponent<CanvasGroup>();
            _canvasGroup.interactable = false;
            _canvasGroup.blocksRaycasts = false;
            _canvasGroup.alpha = 0f;

            ClearChildren(_root);
            CreateFrame();
            CreateDots();
            _uiBuilt = true;
            return true;
        }

        private void CreateFrame()
        {
            Image outerRing = EnsureImage(CreateRect(_root, "RingOuter").gameObject);
            outerRing.sprite = null;
            outerRing.color = FrameColor;
            outerRing.raycastTarget = false;
            outerRing.type = Image.Type.Simple;
            outerRing.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            outerRing.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            outerRing.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            outerRing.rectTransform.sizeDelta = new Vector2(RootWidth - 18f, RootHeight - 18f);

            Image horizontalRule = EnsureImage(CreateRect(_root, "RuleH").gameObject);
            horizontalRule.sprite = null;
            horizontalRule.color = FrameColor;
            horizontalRule.raycastTarget = false;
            horizontalRule.rectTransform.anchorMin = new Vector2(0f, 0.5f);
            horizontalRule.rectTransform.anchorMax = new Vector2(1f, 0.5f);
            horizontalRule.rectTransform.sizeDelta = new Vector2(0f, 1f);

            Image verticalRule = EnsureImage(CreateRect(_root, "RuleV").gameObject);
            verticalRule.sprite = null;
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
            // COLD ALLOC: Vector2[16] - acoustic-radar marker position cache for Canvas dirty-write suppression - owner: SonarHoloCompass
            _lastDotAnchoredPositions = new Vector2[MaxDots];
            // COLD ALLOC: float[16] - acoustic-radar marker size cache for Canvas dirty-write suppression - owner: SonarHoloCompass
            _lastDotSizes = new float[MaxDots];
            // COLD ALLOC: Color[16] - acoustic-radar marker color cache for Canvas dirty-write suppression - owner: SonarHoloCompass
            _lastDotColors = new Color[MaxDots];
            // COLD ALLOC: bool[16] - acoustic-radar marker visibility cache for Canvas dirty-write suppression - owner: SonarHoloCompass
            _dotVisibleFlags = new bool[MaxDots];

            for (int i = 0; i < MaxDots; i++)
            {
                RectTransform dotRect = CreateRect(_root, DotName);
                dotRect.anchorMin = new Vector2(0.5f, 0.5f);
                dotRect.anchorMax = new Vector2(0.5f, 0.5f);
                dotRect.pivot = new Vector2(0.5f, 0.5f);
                dotRect.sizeDelta = new Vector2(DotBaseSize, DotBaseSize);

                Image dotImage = EnsureImage(dotRect.gameObject);
                dotImage.sprite = null;
                dotImage.color = Color.clear;
                dotImage.raycastTarget = false;

                _dotRects[i] = dotRect;
                _dotImages[i] = dotImage;
            }
        }

        private void ScheduleProjection(int emitterCount)
        {
            if (_projectionInputs == null || _projectionOutputs == null)
                return;

            int safeCount = math.clamp(emitterCount, 0, math.min(MaxDots, _impactEmitterSamples.Length));
            safeCount = math.min(safeCount, _projectionInputs.Length);
            safeCount = math.min(safeCount, _projectionOutputs.Length);
            Transform viewTransform = _viewCamera.transform;
            Vector3 viewPosition = viewTransform.position;
            Quaternion viewRotation = viewTransform.rotation;
            float3 viewRight = (float3)(viewRotation * Vector3.right);
            float3 viewUp = (float3)(viewRotation * Vector3.up);
            float3 viewForward = (float3)(viewRotation * Vector3.forward);
            if (!TryResolveViewAup(viewPosition, out AbsoluteUniversePosition listenerAup))
            {
                QueueHideDots();
                return;
            }

            for (int i = 0; i < safeCount; i++)
            {
                SpatialAudioImpactEmitterSample sample = _impactEmitterSamples[i];
                float3 listenerRelativePosition = AupPrecisionMath.LocalDeltaFloat3(
                    sample.PositionAup.ToAbsoluteDouble3(),
                    listenerAup.ToAbsoluteDouble3(),
                    float3.zero);
                _projectionInputs[i] = new AcousticRadarBlipInput
                {
                    ListenerRelativePosition = listenerRelativePosition,
                    Amplitude = sample.Amplitude
                };
            }

            for (int i = 0; i < safeCount; i++)
            {
                _projectionOutputs[i] = ProjectImpactBlip(
                    in _projectionInputs[i],
                    viewRight,
                    viewUp,
                    viewForward);
            }

            _pendingProjectionCount = safeCount;
            _projectionScheduled = true;
            _hideDotsQueued = false;
        }

        private static AcousticRadarBlipOutput ProjectImpactBlip(
            in AcousticRadarBlipInput input,
            float3 cameraRight,
            float3 cameraUp,
            float3 cameraForward)
        {
            float amplitude = math.isfinite(input.Amplitude) ? math.saturate(input.Amplitude) : 0f;
            if (amplitude <= 0f || !math.all(math.isfinite(input.ListenerRelativePosition)))
                return default;

            float3 delta = input.ListenerRelativePosition;
            float distanceSqr = math.lengthsq(delta);
            if (!math.isfinite(distanceSqr) || distanceSqr <= 0.0001f || distanceSqr > ImpactRadarMaxDistanceMetersSq)
                return default;

            float distance = ApproximateMagnitude(delta);
            float inverseDistance = math.rcp(math.max(distance, 0.0001f));
            float3 direction = delta * inverseDistance;
            float x = math.clamp(math.dot(cameraRight, direction) * RingRadius, -RingRadius, RingRadius);
            float y = math.clamp(math.dot(cameraUp, direction) * VerticalRadius, -VerticalRadius, VerticalRadius);
            float depthBlend = math.dot(cameraForward, direction) >= 0f ? 1f : 0.35f;
            float distanceFade = 1f - math.saturate(distanceSqr * math.rcp(math.max(0.0001f, ImpactRadarMaxDistanceMetersSq)));
            float energy = amplitude * distanceFade;
            if (!math.isfinite(x) || !math.isfinite(y) || !math.isfinite(energy))
                return default;

            return new AcousticRadarBlipOutput
            {
                AnchoredPosition = new float2(x, y),
                Energy = energy,
                DepthBlend = depthBlend,
                Visible = energy > MinimumBlipEnergy ? 1 : 0
            };
        }

        private static float ApproximateMagnitude(float3 value)
        {
            float3 delta = math.abs(value);
            float maxAxis = math.max(delta.x, math.max(delta.y, delta.z));
            float minAxis = math.min(delta.x, math.min(delta.y, delta.z));
            float midAxis = delta.x + delta.y + delta.z - maxAxis - minAxis;
            return maxAxis + (midAxis * 0.375f) + (minAxis * 0.125f);
        }

        private bool TryResolveViewAup(Vector3 viewPosition, out AbsoluteUniversePosition viewAup)
        {
            IPlayerRuntimeContext playerContext = _cachedPlayerContext;
            if (playerContext != null &&
                playerContext.TryGetMovementRuntimeState(out PlayerMovementRuntimeState cachedMovementState) &&
                (cachedMovementState.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) != 0u)
            {
                viewAup = OffsetAupLocal(
                    in cachedMovementState.PredictedAup,
                    (Vector3)((float3)viewPosition - cachedMovementState.PredictedWorldPosition));
                return true;
            }

            viewAup = default;
            return false;
        }

        private static AbsoluteUniversePosition OffsetAupLocal(in AbsoluteUniversePosition anchorAup, Vector3 runtimeOffset)
        {
            AbsoluteUniversePosition result = anchorAup;
            result.LocalX += runtimeOffset.x;
            result.LocalY += runtimeOffset.y;
            result.LocalZ += runtimeOffset.z;
            NormalizeAupLocalAxis(ref result.GridX, ref result.LocalX);
            NormalizeAupLocalAxis(ref result.GridY, ref result.LocalY);
            NormalizeAupLocalAxis(ref result.GridZ, ref result.LocalZ);
            return result;
        }

        private static void NormalizeAupLocalAxis(ref long grid, ref float local)
        {
            const float cellSize = AbsoluteUniversePosition.CellSizeMeters;
            if (local >= 0f && local < cellSize)
                return;

            long gridDelta = (long)math.floor(local / cellSize);
            grid += gridDelta;
            local -= gridDelta * cellSize;
            if (local < 0f)
            {
                local += cellSize;
                grid--;
                return;
            }

            if (local >= cellSize)
            {
                local -= cellSize;
                grid++;
            }
        }

        private bool TryCompleteProjectionIfScheduled()
        {
            if (!_projectionScheduled)
                return true;

            _projectionScheduled = false;
            ApplyProjectedDots(_pendingProjectionCount);
            _pendingProjectionCount = 0;
            return true;
        }

        private void QueueHideDots()
        {
            _hideDotsQueued = true;
        }

        private void ApplyProjectedDots(int activeCount)
        {
            if (_dotRects == null ||
                _dotImages == null ||
                _lastDotAnchoredPositions == null ||
                _lastDotSizes == null ||
                _lastDotColors == null ||
                _dotVisibleFlags == null ||
                _projectionOutputs == null)
            {
                return;
            }

            int safeLimit = MaxDots;
            safeLimit = math.min(safeLimit, _projectionOutputs.Length);
            safeLimit = math.min(safeLimit, _dotRects.Length);
            safeLimit = math.min(safeLimit, _dotImages.Length);
            safeLimit = math.min(safeLimit, _lastDotAnchoredPositions.Length);
            safeLimit = math.min(safeLimit, _lastDotSizes.Length);
            safeLimit = math.min(safeLimit, _lastDotColors.Length);
            safeLimit = math.min(safeLimit, _dotVisibleFlags.Length);
            int safeCount = math.clamp(activeCount, 0, safeLimit);
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

                float energy = math.saturate(blip.Energy);
                Vector2 anchoredPosition = new Vector2(blip.AnchoredPosition.x, blip.AnchoredPosition.y);
                if (!_dotVisibleFlags[i] ||
                    Vector2DistanceSq(_lastDotAnchoredPositions[i], anchoredPosition) > DotPositionEpsilonSq)
                {
                    dotRect.anchoredPosition = anchoredPosition;
                    _lastDotAnchoredPositions[i] = anchoredPosition;
                }

                float size = DotBaseSize
                    * math.lerp(0.72f, 1.12f, blip.DepthBlend)
                    * math.lerp(0.78f, 1.36f, energy)
                    * pulseScale;
                if (!_dotVisibleFlags[i] || math.abs(_lastDotSizes[i] - size) > DotSizeEpsilon)
                {
                    dotRect.sizeDelta = new Vector2(size, size);
                    _lastDotSizes[i] = size;
                }

                Color color = new Color(
                    math.lerp(DotRearColor.r, DotFrontColor.r, blip.DepthBlend),
                    math.lerp(DotRearColor.g, DotFrontColor.g, blip.DepthBlend),
                    math.lerp(DotRearColor.b, DotFrontColor.b, blip.DepthBlend),
                    math.lerp(DotRearColor.a, DotFrontColor.a, blip.DepthBlend));
                color.a *= math.lerp(0.25f, 1f, energy);
                if (!_dotVisibleFlags[i] || ColorDistanceSq(_lastDotColors[i], color) > DotColorEpsilonSq)
                {
                    dotImage.color = color;
                    _lastDotColors[i] = color;
                }

                _dotVisibleFlags[i] = true;
                visibleCount++;
            }

            for (int i = safeCount; i < MaxDots; i++)
                HideDot(i);

            _dotsHidden = visibleCount <= 0;
            ApplyRootAlpha(visibleCount > 0 ? 1f : 0f);
        }

        private void HideDots()
        {
            if (_dotImages == null)
                return;

            if (_dotsHidden)
                return;

            for (int i = 0; i < _dotImages.Length; i++)
                HideDot(i);

            _dotsHidden = true;
        }

        private void HideDot(int index)
        {
            if (_dotImages == null || index < 0 || index >= _dotImages.Length)
                return;

            Image image = _dotImages[index];
            if (image != null && image.color.a > HiddenAlphaCutoff)
                image.color = Color.clear;

            if (_dotVisibleFlags != null &&
                _lastDotColors != null &&
                index < _dotVisibleFlags.Length &&
                index < _lastDotColors.Length)
            {
                _dotVisibleFlags[index] = false;
                _lastDotColors[index] = Color.clear;
            }
        }

        private void ApplyRootAlpha(float alpha)
        {
            if (_canvasGroup == null || math.abs(_lastRootAlpha - alpha) <= 0.0001f)
                return;

            _canvasGroup.alpha = alpha;
            _lastRootAlpha = alpha;
        }

        private static float Vector2DistanceSq(Vector2 a, Vector2 b)
        {
            float dx = a.x - b.x;
            float dy = a.y - b.y;
            return (dx * dx) + (dy * dy);
        }

        private static float ColorDistanceSq(Color a, Color b)
        {
            float dr = a.r - b.r;
            float dg = a.g - b.g;
            float db = a.b - b.b;
            float da = a.a - b.a;
            return (dr * dr) + (dg * dg) + (db * db) + (da * da);
        }

        private void RegisterToTickManager()
        {
            if (_registeredLateFrame || !Application.isPlaying)
                return;

            _registeredLateFrame = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.UI);
        }

        private void UnregisterFromTickManager()
        {
            if (_registeredLateFrame)
            {
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.UI);
                _registeredLateFrame = false;
            }

        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot == GlobalRegistryServiceSlot.Player)
            {
                _cachedPlayerContext = currentService as IPlayerRuntimeContext;
                _viewCamera = _cachedPlayerContext != null ? _cachedPlayerContext.PlayerCamera : null;
            }
            else if (serviceSlot == GlobalRegistryServiceSlot.Audio)
            {
                _cachedAudioManager = currentService as ISpatialAudioImpactEmitterReadModel;
            }
            else if (serviceSlot == GlobalRegistryServiceSlot.Dispatcher && isActiveAndEnabled)
            {
                if (currentService == null)
                {
                    _registeredLateFrame = false;
                    return;
                }

                UnregisterFromTickManager();
                RegisterToTickManager();
            }
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

        private void CacheRegistryServicesCold()
        {
            _cachedPlayerContext = GlobalRegistry.Player;
            _cachedAudioManager = GlobalRegistry.Audio as ISpatialAudioImpactEmitterReadModel;
        }

        private static Canvas ResolveTargetCanvas(bool allowComponentFallback)
        {
            SuitHUDV4CanvasOverlay overlay = SuitHUDV4CanvasOverlay.ActiveRuntimeInstance;
            if (overlay != null && overlay.TargetCanvas != null)
                return overlay.TargetCanvas;

            if (!allowComponentFallback || SuitHUDV4CanvasOverlay.ActiveRuntimeInstance == null)
                return null;

            SuitHUDV4CanvasOverlay.ActiveRuntimeInstance.TryGetComponent(out Canvas canvas);
            return canvas;
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
            go.TryGetComponent(out RectTransform rect);
            rect.SetParent(parent, false);
            rect.localScale = Vector3.one;
            return rect;
        }

        private static Image EnsureImage(GameObject target)
        {
            target.TryGetComponent(out Image image);
            if (image == null)
                image = target.AddComponent<Image>();
            return image;
        }
    }
}
