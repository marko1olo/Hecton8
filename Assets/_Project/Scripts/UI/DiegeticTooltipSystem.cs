using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using Hecton8.Core;
using Hecton8.Core.Contracts.Signals;
using ScalabilityChangedEvent = Hecton8.Core.Contracts.Signals.ScalabilityChangedEvent;
using Hecton8.UI.Diegetic.Contracts;
using Hecton8.World;
using TMPro;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TextCore;

namespace Hecton8.UI
{
    /// <summary>
    /// Signal-fed diegetic interact prompt renderer. It draws camera-facing TMP atlas quads in world space without Canvas UI.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/UI/Diegetic Tooltip System")]
    public sealed class DiegeticTooltipSystem : MonoBehaviour, ILateFrameTickable, IRenderable, IGlobalRegistryHotSwapListener, IScalabilityChangedEventListener
    {
        private const int MaxGlyphCount = 96;
        private const int MaxIconCount = 1;
        private const int MaxInstanceCount = MaxGlyphCount + MaxIconCount;
        private const int IndirectArgsCount = 5;
        private const int PromptBufferCapacity = 64;
        private const int AsciiCacheSize = 128;
        private const int UvTableCapacity = 128;
        private const int BlackBoxCapacity = 300;
        private const int TooltipGlyphInstanceStride = 96;
        private const int UvRectStride = 16;
        private const int IndirectArgsStride = IndirectArgsCount * 4;
        private const uint InputSchemeHashKeyboardMouse = DiegeticTooltipInputSchemeHashes.KeyboardMouse;
        private const uint InputSchemeHashGamepad = DiegeticTooltipInputSchemeHashes.Gamepad;
        private const uint InputSchemeHashSteamDeck = DiegeticTooltipInputSchemeHashes.SteamDeck;
        private const uint InputSchemeHashXRTouch = DiegeticTooltipInputSchemeHashes.XRTouch;
        private const int KeyboardInteractGlyphIndex = DiegeticTooltipGlyphIndices.KeyboardInteract;
        private const int GamepadInteractGlyphIndex = DiegeticTooltipGlyphIndices.GamepadInteract;
        private const int SteamDeckInteractGlyphIndex = DiegeticTooltipGlyphIndices.SteamDeckInteract;
        private const int XRInteractGlyphIndex = DiegeticTooltipGlyphIndices.XRInteract;
        private const float MinimumGlyphScale = 0.0001f;
        private const float IconScaleMultiplier = 1.06f;
        private const float IconVerticalBias = -0.002f;
        private const float IconGapMultiplier = 0.42f;
        private const float DefaultGlyphWorldHeight = 0.018f;
        private const float DefaultGlyphAdvanceScale = 1f;
        private const float DefaultFadeDurationSeconds = 0.2f;
        private const float MinGlyphWorldHeight = 0.002f;
        private const float MaxGlyphWorldHeight = 0.05f;
        private const float MinGlyphAdvanceScale = 0.8f;
        private const float MaxGlyphAdvanceScale = 1.6f;
        private const float MinFadeDurationSeconds = 0.05f;
        private const float MaxFadeDurationSeconds = 1f;
        private const float MinVisibleDistanceMeters = 0.5f;
        private const float MaxVisibleDistanceMeters = 20f;
        private const float MinGradientScale = 1f;
        private const float MaxGradientScale = 24f;
        private const float DefaultGradientScale = 8f;
        private const float MinFaceDilate = -1f;
        private const float MaxFaceDilate = 1f;
        private const float DefaultFaceDilate = 0f;
        private const string DumpRelativePath = "Docs/AgentLogs/Dump_CONTEXTUAL_UX_PROMPTER.bin";

        private static readonly int MainTexId = Shader.PropertyToID("_MainTex");
        private static readonly int GradientScaleId = Shader.PropertyToID("_GradientScale");
        private static readonly int FaceDilateId = Shader.PropertyToID("_FaceDilate");
        private static readonly int InstanceBufferId = Shader.PropertyToID("_TooltipInstances");
        private static readonly int UvRectBufferId = Shader.PropertyToID("_TooltipUvRects");
        private static readonly int DitherEnabledId = Shader.PropertyToID("_DitherEnabled");
        private static readonly char[] s_defaultPrompt =
        {
            'O', 'P', 'E', 'N', ' ', 'H', 'A', 'T', 'C', 'H'
        };
        private static readonly Vector3[] s_quadVertices =
        {
            new Vector3(-0.5f, -0.5f, 0f),
            new Vector3(-0.5f, 0.5f, 0f),
            new Vector3(0.5f, 0.5f, 0f),
            new Vector3(0.5f, -0.5f, 0f)
        };
        private static readonly Vector2[] s_quadUvs =
        {
            new Vector2(0f, 0f),
            new Vector2(0f, 1f),
            new Vector2(1f, 1f),
            new Vector2(1f, 0f)
        };
        private static readonly int[] s_quadIndices = { 0, 1, 2, 0, 2, 3 };

        [Header("References")]
        [SerializeField, Tooltip("Optional explicit readable SDF font used for diegetic tooltip glyphs.")]
        private TMP_FontAsset fontAsset;
        [SerializeField, Tooltip("Optional explicit TMP sprite asset used for interact binding icons.")]
        private TMP_SpriteAsset spriteAsset;
        [SerializeField, Tooltip("Required authored font material for diegetic tooltip glyphs.")]
        private Material glyphMaterial;
        [SerializeField, Tooltip("Required authored icon material for diegetic tooltip binding icons.")]
        private Material iconMaterial;
        [SerializeField, Tooltip("Optional tooltip shader contract reference. Runtime uses authored material assets only.")]
        private Shader glyphShader;
        [SerializeField, Tooltip("Optional explicit interaction camera. When null, the owner resolves the active player camera.")]
        private Camera interactionCamera;
        [SerializeField, Tooltip("Optional world-space TextMeshPro sink for SetCharArray authoring validation. TextMeshProUGUI is ignored.")]
        private TMP_Text worldSpaceTextSink;

        [Header("Tooltip Layout")]
        [SerializeField, Tooltip("Runtime-space offset above the looked-at AUP anchor.")]
        private Vector3 worldOffset = new Vector3(0f, 0.5f, 0f);
        [SerializeField, Range(MinGlyphWorldHeight, MaxGlyphWorldHeight), Tooltip("World height of one tooltip glyph quad.")]
        private float glyphWorldHeight = DefaultGlyphWorldHeight;
        [SerializeField, Range(MinGlyphAdvanceScale, MaxGlyphAdvanceScale), Tooltip("Horizontal advance multiplier applied to glyph metrics.")]
        private float glyphAdvanceScale = DefaultGlyphAdvanceScale;
        [SerializeField, Range(MinFadeDurationSeconds, MaxFadeDurationSeconds), Tooltip("Dither fade duration in seconds before continuous quality scaling.")]
        private float fadeDurationSeconds = DefaultFadeDurationSeconds;
        [SerializeField, Range(0.02f, 0.25f), Tooltip("VR-only shift toward the camera to avoid stereo clipping.")]
        private float vrDepthOffsetMeters = 0.1f;
        [SerializeField, Range(MinVisibleDistanceMeters, MaxVisibleDistanceMeters), Tooltip("Maximum camera distance where a diegetic tooltip remains visible.")]
        private float maxVisibleDistance = 6f;
        [SerializeField, Tooltip("Base tooltip tint. Alpha is multiplied by hover fade state.")]
        private Color glyphColor = new Color(0.86f, 0.98f, 1f, 0.96f);

        [Header("Sprite Indices")]
        [SerializeField, Min(0)] private int keyboardInteractSpriteIndex = KeyboardInteractGlyphIndex;
        [SerializeField, Min(0)] private int gamepadInteractSpriteIndex = GamepadInteractGlyphIndex;
        [SerializeField, Min(0)] private int steamDeckInteractSpriteIndex = SteamDeckInteractGlyphIndex;
        [SerializeField, Min(0)] private int xrInteractSpriteIndex = XRInteractGlyphIndex;

        [Header("SDF Tuning")]
        [SerializeField, Range(MinGradientScale, MaxGradientScale), Tooltip("Distance-field edge sharpness multiplier forwarded into the tooltip glyph shader.")]
        private float gradientScale = DefaultGradientScale;
        [SerializeField, Range(MinFaceDilate, MaxFaceDilate), Tooltip("Face dilate forwarded into the tooltip glyph shader for minor legibility tuning.")]
        private float faceDilate = DefaultFaceDilate;

        private readonly Vector2[] _textGlyphLocalCenters = new Vector2[MaxGlyphCount];
        private readonly Vector2[] _textGlyphLocalScales = new Vector2[MaxGlyphCount];
        private readonly int[] _textGlyphIndices = new int[MaxGlyphCount];
        private readonly Vector2[] _iconLocalCenters = new Vector2[MaxIconCount];
        private readonly Vector2[] _iconLocalScales = new Vector2[MaxIconCount];
        private readonly int[] _iconGlyphIndices = new int[MaxIconCount];
        private readonly TooltipGlyphInstance[] _instancePayloads = new TooltipGlyphInstance[MaxInstanceCount];
        private readonly uint[] _indirectArgs = new uint[IndirectArgsCount];
        private readonly Vector4[] _fontUvTable = new Vector4[UvTableCapacity];
        private readonly Vector4[] _spriteUvTable = new Vector4[UvTableCapacity];
        private readonly TMP_Character[] _asciiCharacters = new TMP_Character[AsciiCacheSize];
        private readonly char[] _promptBuffer = new char[PromptBufferCapacity];

        private Material _runtimeGlyphMaterial;
        private Material _runtimeIconMaterial;
        private MaterialPropertyBlock _textPropertyBlock;
        private MaterialPropertyBlock _iconPropertyBlock;
        private Texture _boundTextTexture;
        private Texture _boundIconTexture;
        private ComputeBuffer _boundTextInstanceBuffer;
        private ComputeBuffer _boundIconInstanceBuffer;
        private ComputeBuffer _boundTextUvBuffer;
        private ComputeBuffer _boundIconUvBuffer;
        private Mesh _runtimeQuadMesh;
        private ComputeBuffer _textInstanceBuffer;
        private ComputeBuffer _iconInstanceBuffer;
        private ComputeBuffer _textArgsBuffer;
        private ComputeBuffer _iconArgsBuffer;
        private ComputeBuffer _fontUvBuffer;
        private ComputeBuffer _spriteUvBuffer;
        private NativeArray<TooltipBlackBoxEntry> _blackBox;
        private TMP_FontAsset _cachedAsciiFont;
        private Texture _runtimeFontAtlasTexture;
        private Texture _runtimeSpriteAtlasTexture;
        private TMP_Text _textSinkWithPayload;
        private Camera _cachedRenderCamera;
        private Transform _cachedRenderCameraTransform;
        private IPlayerRuntimeContext _cachedPlayerContext;
        private IInputDeterminismService _inputDeterminism;
        private AbsoluteUniversePosition _activeTargetAup;
        private Vector3 _activeRuntimeAnchor;
        private Vector3 _diagnosticWorldAnchor;
        private Color _diagnosticColor = Color.white;
        private uint _activeTargetHash;
        private uint _activePromptHash;
        private uint _activeSchemeHash;
        private uint _lastAupShiftFrame;
        private int _blackBoxCursor;
        private int _blackBoxWrittenCount;
        private int _promptLength;
        private int _textGlyphCount;
        private int _iconCount;
        private int _boundTextArgsCount = -1;
        private int _boundIconArgsCount = -1;
        private float _visibleAlpha;
        private float _boundTextGradientScale = float.NaN;
        private float _boundIconGradientScale = float.NaN;
        private float _boundTextFaceDilate = float.NaN;
        private float _boundIconFaceDilate = float.NaN;
        private float _boundTextDitherEnabled = float.NaN;
        private float _boundIconDitherEnabled = float.NaN;
        private float _cachedMaxVisibleDistance = float.NaN;
        private float _cachedMaxVisibleDistanceSq;
        private Vector3 _cachedBoundsSize;
        private bool _hasSignalTarget;
        private bool _diagnosticActive;
        private bool _registeredLateFrame;
        private bool _registeredRenderable;
        private bool _hotSwapListenerRegistered;
        private bool _scalabilityListenerRegistered;
        private bool _fontUvTableDirty;
        private bool _spriteUvTableDirty;
        private bool _resourceObjectsReady;
        private bool _materialResolveAttempted;
        private bool _materialResolveFailed;
        private bool _materialsReady;
        private float _qualityWeight01 = 1f;
        private bool _cachedRenderCameraFromInteraction;
        private bool _textSinkHasPayload;
        private bool _blackBoxDumped;

        public void LateFrameTick()
        {
            float deltaTime = math.max(0f, SystemDispatcher.CurrentFrameDeltaTime);
            ConsumeLookTargetSignals();
            ConsumeAupShiftSignals();

            if (_hasSignalTarget && !_diagnosticActive && RefreshActiveSchemeHash())
                RebuildActiveTooltipLayout(refreshScheme: false);

            bool hasVisiblePayload = (_textGlyphCount > 0 || _iconCount > 0) && (_hasSignalTarget || _diagnosticActive);
            if (!hasVisiblePayload)
                ClearTextSink();

            float targetAlpha = hasVisiblePayload ? 1f : 0f;
            float fadeDuration = ResolveQualityFadeDurationSeconds();
            _visibleAlpha = MoveTowardsFast(_visibleAlpha, targetAlpha, math.max(0f, deltaTime) * math.rcp(fadeDuration));
        }

        public void Render(float deltaTime)
        {
            if (_visibleAlpha <= 0.0001f || (_textGlyphCount == 0 && _iconCount == 0))
                return;

            EnsureResources();
            if (!_resourceObjectsReady || !_materialsReady || _runtimeQuadMesh == null)
                return;

            Camera camera = ResolveRenderCamera();
            if (camera == null)
                return;

            Transform cameraTransform = _cachedRenderCameraTransform;
            if (cameraTransform == null)
                return;

            Vector3 cameraPosition = cameraTransform.position;
            Vector3 cameraRight = cameraTransform.right;
            Vector3 cameraUp = cameraTransform.up;
            Vector3 cameraForward = cameraTransform.forward;
            Vector3 anchorPosition = ResolveAnchorPosition(cameraPosition);
            RefreshVisibleDistanceCache();
            if ((anchorPosition - cameraPosition).sqrMagnitude > _cachedMaxVisibleDistanceSq)
                return;

            if (!IsFinite(anchorPosition))
            {
                DumpBlackBox();
                ClearTooltipState();
                return;
            }

            Color resolvedColor = _diagnosticActive ? _diagnosticColor : glyphColor;
            Vector4 tint = new Vector4(resolvedColor.r, resolvedColor.g, resolvedColor.b, resolvedColor.a * _visibleAlpha);
            float qualityWeight = math.saturate(_qualityWeight01);
            float ditherEnabled = ResolveDitherWeight(qualityWeight);
            int renderLayer = gameObject.layer;
            Bounds bounds = new Bounds(anchorPosition, _cachedBoundsSize);
            UploadUvTablesIfDirty();

            if (_iconCount > 0 && _runtimeIconMaterial != null)
            {
                DrawBatch(
                    anchorPosition,
                    camera,
                    cameraRight,
                    cameraUp,
                    cameraForward,
                    bounds,
                    _iconLocalCenters,
                    _iconLocalScales,
                    _iconGlyphIndices,
                    _runtimeIconMaterial,
                    _iconInstanceBuffer,
                    _iconArgsBuffer,
                    _spriteUvBuffer,
                    _runtimeSpriteAtlasTexture,
                    _iconPropertyBlock,
                    ref _boundIconTexture,
                    ref _boundIconInstanceBuffer,
                    ref _boundIconUvBuffer,
                    ref _boundIconGradientScale,
                    ref _boundIconFaceDilate,
                    ref _boundIconDitherEnabled,
                    ref _boundIconArgsCount,
                    _iconCount,
                    renderLayer,
                    tint,
                    ditherEnabled);
            }

            if (_textGlyphCount > 0 && _runtimeGlyphMaterial != null)
            {
                DrawBatch(
                    anchorPosition,
                    camera,
                    cameraRight,
                    cameraUp,
                    cameraForward,
                    bounds,
                    _textGlyphLocalCenters,
                    _textGlyphLocalScales,
                    _textGlyphIndices,
                    _runtimeGlyphMaterial,
                    _textInstanceBuffer,
                    _textArgsBuffer,
                    _fontUvBuffer,
                    _runtimeFontAtlasTexture,
                    _textPropertyBlock,
                    ref _boundTextTexture,
                    ref _boundTextInstanceBuffer,
                    ref _boundTextUvBuffer,
                    ref _boundTextGradientScale,
                    ref _boundTextFaceDilate,
                    ref _boundTextDitherEnabled,
                    ref _boundTextArgsCount,
                    _textGlyphCount,
                    renderLayer,
                    tint,
                    ditherEnabled);
            }

            byte qualityByte = (byte)math.clamp((int)math.round(qualityWeight * 255f), 0, 255);
            RecordBlackBox(anchorPosition, tint, qualityByte);
        }

        private void OnEnable()
        {
            _blackBoxDumped = false;
            EnsureResources();
            EnsureBlackBox();
            TryRegisterRuntime();
            TryRegisterHotSwapListener();
            TryRegisterScalabilityListener();
            CacheRegistryServicesCold();
            RefreshScalabilityPolicy();
            RefreshInputDeterminismService();
            _activeSchemeHash = ResolveCurrentSchemeHash();
        }

        private void Start()
        {
            TryRegisterRuntime();
            TryRegisterHotSwapListener();
            TryRegisterScalabilityListener();
            CacheRegistryServicesCold();
            RefreshScalabilityPolicy();
            RefreshInputDeterminismService();
            _activeSchemeHash = ResolveCurrentSchemeHash();
        }

        private void OnDisable()
        {
            UnregisterRuntime();
            TryUnregisterHotSwapListener();
            TryUnregisterScalabilityListener();
            ClearTooltipState();
            _promptLength = 0;
            CacheRenderCamera(null, fromInteraction: false);
            _inputDeterminism = null;
        }

        private void OnDestroy()
        {
            UnregisterRuntime();
            TryUnregisterHotSwapListener();
            TryUnregisterScalabilityListener();
            ReleaseResources();
        }

        public void OnScalabilityChanged(in ScalabilityChangedEvent payload)
        {
            RefreshScalabilityPolicy();
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot == GlobalRegistryServiceSlot.Player)
            {
                _cachedPlayerContext = currentService as IPlayerRuntimeContext;
                Camera playerCamera = interactionCamera == null && _cachedPlayerContext != null
                    ? _cachedPlayerContext.PlayerCamera
                    : null;
                CacheRenderCamera(playerCamera != null && playerCamera.isActiveAndEnabled ? playerCamera : null, fromInteraction: false);
            }

            if (serviceSlot != GlobalRegistryServiceSlot.Input)
                return;

            _inputDeterminism = currentService as IInputDeterminismService;
            if (_inputDeterminism == null)
                RefreshInputDeterminismService();

            _activeSchemeHash = 0u;
            RefreshActiveSchemeHash();
            if (_hasSignalTarget && !_diagnosticActive)
                RebuildActiveTooltipLayout(refreshScheme: false);
        }

        public void ShowDiagnostic(Vector3 worldAnchor, ReadOnlySpan<char> text, Color tint)
        {
            if (text.IsEmpty)
            {
                ClearDiagnostic();
                return;
            }

            EnsureResources();
            RefreshActiveSchemeHash();
            _diagnosticWorldAnchor = worldAnchor;
            _diagnosticColor = tint;
            _diagnosticActive = true;
            StagePrompt(text);
            BuildGlyphLayout(new ReadOnlySpan<char>(_promptBuffer, 0, _promptLength), includeBindingIcon: false);
        }

        public void ClearDiagnostic()
        {
            if (!_diagnosticActive)
                return;

            _diagnosticActive = false;
            if (_hasSignalTarget)
            {
                RebuildActiveTooltipLayout();
                return;
            }

            _textGlyphCount = 0;
            _iconCount = 0;
            ClearTextSink();
        }

        private void ConsumeLookTargetSignals()
        {
            ReadOnlySpan<PlayerLookTargetSignal> signals = SignalBus<PlayerLookTargetSignal>.GetFrameSnapshot();
            for (int i = 0; i < signals.Length; i++)
            {
                PlayerLookTargetSignal signal = signals[i];
                if (signal.State == PlayerLookTargetSignalStates.Cleared)
                {
                    _hasSignalTarget = false;
                    _activeTargetHash = 0u;
                    _activePromptHash = 0u;
                    continue;
                }

                _hasSignalTarget = true;
                _activeTargetAup = signal.TargetAup;
                _activeRuntimeAnchor = new Vector3(signal.RuntimeAnchor.x, signal.RuntimeAnchor.y, signal.RuntimeAnchor.z);
                _activeTargetHash = signal.TargetHash;
                if (signal.PromptHash != _activePromptHash || _promptLength == 0)
                {
                    _activePromptHash = signal.PromptHash;
                    StagePromptFromHash(signal.PromptHash);
                    if (!_diagnosticActive)
                        RebuildActiveTooltipLayout();
                }
            }
        }

        private void ConsumeAupShiftSignals()
        {
            ReadOnlySpan<AupShiftSignal> shifts = SignalBus<AupShiftSignal>.GetFrameSnapshot();
            for (int i = 0; i < shifts.Length; i++)
            {
                AupShiftSignal signal = shifts[i];
                if (!IsNewAupShift(signal.ShiftFrameId, _lastAupShiftFrame))
                    continue;

                _lastAupShiftFrame = signal.ShiftFrameId;
                Vector3 shift = new Vector3(signal.ShiftMeters.x, signal.ShiftMeters.y, signal.ShiftMeters.z);
                if (_hasSignalTarget)
                    _activeRuntimeAnchor += shift;
                if (_diagnosticActive)
                    _diagnosticWorldAnchor += shift;
            }
        }

        private static bool IsNewAupShift(uint shiftFrameId, uint lastAppliedFrameId)
        {
            return shiftFrameId != 0u &&
                   shiftFrameId != lastAppliedFrameId &&
                   unchecked(shiftFrameId - lastAppliedFrameId) < 0x80000000u;
        }

        private void StagePromptFromHash(uint promptHash)
        {
            if (!PlayerLookTargetPromptCache.TryCopyTo(promptHash, _promptBuffer, PromptBufferCapacity, out int sourceLength) || sourceLength <= 0)
            {
                StageDefaultPrompt();
                return;
            }

            _promptLength = 0;
            for (int i = 0; i < sourceLength; i++)
            {
                char c = NormalizeTooltipCharacter(_promptBuffer[i]);
                if (c == '\0')
                    continue;

                _promptBuffer[_promptLength++] = c;
            }

            if (_promptLength <= 0)
                StageDefaultPrompt();
            else
                PushPromptToTextSink();
        }

        private void StagePrompt(ReadOnlySpan<char> prompt)
        {
            if (prompt.IsEmpty)
            {
                StageDefaultPrompt();
                return;
            }

            int sourceLength = math.min(prompt.Length, PromptBufferCapacity);
            _promptLength = 0;
            for (int i = 0; i < sourceLength; i++)
            {
                char c = NormalizeTooltipCharacter(prompt[i]);
                if (c == '\0')
                    continue;

                _promptBuffer[_promptLength++] = c;
            }

            if (_promptLength <= 0)
                StageDefaultPrompt();
            else
                PushPromptToTextSink();
        }

        private void StageDefaultPrompt()
        {
            _promptLength = s_defaultPrompt.Length;
            for (int i = 0; i < s_defaultPrompt.Length; i++)
                _promptBuffer[i] = s_defaultPrompt[i];

            PushPromptToTextSink();
        }

        private void PushPromptToTextSink()
        {
            TMP_Text sink = worldSpaceTextSink;
            if (sink == null || sink is TextMeshProUGUI || _promptLength <= 0)
                return;

            sink.SetCharArray(_promptBuffer, 0, _promptLength);
            _textSinkWithPayload = sink;
            _textSinkHasPayload = true;
        }

        private void ClearTextSink()
        {
            if (!_textSinkHasPayload)
                return;

            TMP_Text sink = _textSinkWithPayload != null ? _textSinkWithPayload : worldSpaceTextSink;
            if (sink != null && !(sink is TextMeshProUGUI))
                sink.SetCharArray(_promptBuffer, 0, 0);

            _textSinkWithPayload = null;
            _textSinkHasPayload = false;
        }

        private void RebuildActiveTooltipLayout()
        {
            RebuildActiveTooltipLayout(refreshScheme: true);
        }

        private void RebuildActiveTooltipLayout(bool refreshScheme)
        {
            if (_promptLength <= 0)
                StageDefaultPrompt();

            if (refreshScheme)
                RefreshActiveSchemeHash();

            EnsureResources();
            BuildGlyphLayout(new ReadOnlySpan<char>(_promptBuffer, 0, _promptLength), includeBindingIcon: true);
        }

        private void ClearTooltipState()
        {
            _hasSignalTarget = false;
            _diagnosticActive = false;
            _activeTargetHash = 0u;
            _activePromptHash = 0u;
            _textGlyphCount = 0;
            _iconCount = 0;
            _visibleAlpha = 0f;
            ClearTextSink();
        }

        private void BuildGlyphLayout(ReadOnlySpan<char> prompt, bool includeBindingIcon)
        {
            TMP_FontAsset font = fontAsset != null ? fontAsset : TMP_Settings.defaultFontAsset;
            if (font == null)
            {
                _runtimeFontAtlasTexture = null;
                _runtimeSpriteAtlasTexture = null;
                _textGlyphCount = 0;
                _iconCount = 0;
                ClearTextSink();
                return;
            }

            _runtimeFontAtlasTexture = font.atlasTexture;
            _runtimeSpriteAtlasTexture = null;
            RefreshAsciiCharacterCache(font);
            int atlasWidth = math.max(1, font.atlasWidth);
            int atlasHeight = math.max(1, font.atlasHeight);
            float resolvedGlyphWorldHeight = ResolveGlyphWorldHeight();
            float glyphScale = resolvedGlyphWorldHeight * math.rcp(math.max(1f, font.faceInfo.pointSize));

            float iconWidth = 0f;
            float iconHeight = 0f;
            float iconAdvance = 0f;
            int iconGlyphIndex = 0;
            _iconCount = includeBindingIcon && TryResolveBindingIcon(glyphScale, out iconGlyphIndex, out iconWidth, out iconHeight)
                ? 1
                : 0;
            if (_iconCount > 0)
            {
                _iconGlyphIndices[0] = iconGlyphIndex;
                iconAdvance = iconWidth + resolvedGlyphWorldHeight * IconGapMultiplier;
            }

            float baselineOffset = font.faceInfo.ascentLine * glyphScale * 0.36f;
            _textGlyphCount = 0;
            float promptAdvance = BuildTextRun(atlasWidth, atlasHeight, glyphScale, baselineOffset, 0f, prompt);
            float totalAdvance = iconAdvance + promptAdvance;
            float originX = -totalAdvance * 0.5f;
            if (_iconCount > 0)
            {
                _iconLocalCenters[0] = new Vector2(originX + iconWidth * 0.5f, IconVerticalBias);
                _iconLocalScales[0] = new Vector2(iconWidth, iconHeight);
            }

            OffsetTextGlyphCenters(originX + iconAdvance);
        }

        private float BuildTextRun(
            int atlasWidth,
            int atlasHeight,
            float glyphScale,
            float baselineOffset,
            float penX,
            ReadOnlySpan<char> text)
        {
            if (text.IsEmpty)
                return penX;

            float invAtlasWidth = math.rcp(math.max(1f, atlasWidth));
            float invAtlasHeight = math.rcp(math.max(1f, atlasHeight));
            float advanceScale = glyphScale * ResolveGlyphAdvanceScale();
            for (int i = 0; i < text.Length && _textGlyphCount < MaxGlyphCount; i++)
            {
                char c = text[i];
                TMP_Character character = ResolveCachedCharacter(c);
                if (character == null || character.glyph == null)
                    continue;

                Glyph glyph = character.glyph;
                GlyphMetrics metrics = glyph.metrics;
                if (c == ' ')
                {
                    penX += metrics.horizontalAdvance * advanceScale;
                    continue;
                }

                GlyphRect rect = glyph.glyphRect;
                float width = math.max(MinimumGlyphScale, metrics.width * glyphScale);
                float height = math.max(MinimumGlyphScale, metrics.height * glyphScale);
                float bearingX = metrics.horizontalBearingX * glyphScale;
                float bearingY = metrics.horizontalBearingY * glyphScale;

                _textGlyphLocalCenters[_textGlyphCount] = new Vector2(
                    penX + bearingX + width * 0.5f,
                    bearingY - height * 0.5f - baselineOffset);
                _textGlyphLocalScales[_textGlyphCount] = new Vector2(width, height);
                int glyphIndex = c < UvTableCapacity ? c : '?';
                _textGlyphIndices[_textGlyphCount] = glyphIndex;
                Vector4 uvRect = new Vector4(
                    rect.x * invAtlasWidth,
                    rect.y * invAtlasHeight,
                    (rect.x + rect.width) * invAtlasWidth,
                    (rect.y + rect.height) * invAtlasHeight);
                if (WriteUvRectIfChanged(_fontUvTable, glyphIndex, uvRect))
                    _fontUvTableDirty = true;

                penX += metrics.horizontalAdvance * advanceScale;
                _textGlyphCount++;
            }

            return penX;
        }

        private void OffsetTextGlyphCenters(float offsetX)
        {
            if (offsetX == 0f)
                return;

            for (int i = 0; i < _textGlyphCount; i++)
            {
                Vector2 center = _textGlyphLocalCenters[i];
                center.x += offsetX;
                _textGlyphLocalCenters[i] = center;
            }
        }

        private static bool WriteUvRectIfChanged(Vector4[] table, int index, Vector4 uvRect)
        {
            Vector4 current = table[index];
            if (current.x == uvRect.x
                && current.y == uvRect.y
                && current.z == uvRect.z
                && current.w == uvRect.w)
                return false;

            table[index] = uvRect;
            return true;
        }

        private bool TryResolveBindingIcon(float glyphScale, out int iconGlyphIndex, out float width, out float height)
        {
            iconGlyphIndex = 0;
            width = 0f;
            height = 0f;
            TMP_SpriteAsset sprite = spriteAsset;
            if (sprite == null)
                return false;

            Texture spriteSheet = sprite.spriteSheet;
            List<TMP_SpriteCharacter> spriteCharacters = sprite.spriteCharacterTable;
            if (spriteSheet == null || spriteCharacters == null)
                return false;

            _runtimeSpriteAtlasTexture = spriteSheet;
            int spriteIndex = ResolveInteractSpriteIndex(_activeSchemeHash);
            if ((uint)spriteIndex >= (uint)spriteCharacters.Count || (uint)spriteIndex >= UvTableCapacity)
                return false;

            TMP_SpriteCharacter spriteCharacter = spriteCharacters[spriteIndex];
            if (spriteCharacter == null || spriteCharacter.glyph == null)
                return false;

            Glyph glyph = spriteCharacter.glyph;
            int atlasWidth = math.max(1, spriteSheet.width);
            int atlasHeight = math.max(1, spriteSheet.height);
            GlyphRect rect = glyph.glyphRect;
            GlyphMetrics metrics = glyph.metrics;
            float invAtlasWidth = math.rcp(math.max(1f, atlasWidth));
            float invAtlasHeight = math.rcp(math.max(1f, atlasHeight));
            float iconScale = glyphScale * IconScaleMultiplier;
            width = math.max(MinimumGlyphScale, metrics.width * iconScale);
            height = math.max(MinimumGlyphScale, metrics.height * iconScale);
            Vector4 uvRect = new Vector4(
                rect.x * invAtlasWidth,
                rect.y * invAtlasHeight,
                (rect.x + rect.width) * invAtlasWidth,
                (rect.y + rect.height) * invAtlasHeight);
            if (WriteUvRectIfChanged(_spriteUvTable, spriteIndex, uvRect))
                _spriteUvTableDirty = true;
            iconGlyphIndex = spriteIndex;
            return true;
        }

        private int ResolveInteractSpriteIndex(uint schemeHash)
        {
            switch (schemeHash)
            {
                case InputSchemeHashSteamDeck:
                    return steamDeckInteractSpriteIndex;
                case InputSchemeHashGamepad:
                    return gamepadInteractSpriteIndex;
                case InputSchemeHashXRTouch:
                    return xrInteractSpriteIndex;
                default:
                    return keyboardInteractSpriteIndex;
            }
        }

        private void RefreshAsciiCharacterCache(TMP_FontAsset font)
        {
            if (ReferenceEquals(_cachedAsciiFont, font))
                return;

            Array.Clear(_asciiCharacters, 0, _asciiCharacters.Length);
            _cachedAsciiFont = font;
            if (font == null || font.characterLookupTable == null)
                return;

            font.characterLookupTable.TryGetValue('?', out TMP_Character fallback);
            font.characterLookupTable.TryGetValue(' ', out TMP_Character space);
            for (int i = 0; i < AsciiCacheSize; i++)
            {
                char c = (char)i;
                if (font.characterLookupTable.TryGetValue(c, out TMP_Character character))
                    _asciiCharacters[i] = character;
                else
                    _asciiCharacters[i] = c == ' ' ? space : fallback;
            }
        }

        private TMP_Character ResolveCachedCharacter(char value)
        {
            return value < AsciiCacheSize ? _asciiCharacters[value] : _asciiCharacters['?'];
        }

        private void EnsureResources()
        {
            if (fontAsset == null)
                fontAsset = TMP_Settings.defaultFontAsset;

            if (spriteAsset == null)
                spriteAsset = TMP_Settings.defaultSpriteAsset;

            if (!_resourceObjectsReady)
                EnsureResourceObjects();

            if (!_materialsReady && !_materialResolveFailed)
                EnsureMaterials();
        }

        private void EnsureResourceObjects()
        {
            bool argsDirty = false;
            if (_runtimeQuadMesh == null)
            {
                _runtimeQuadMesh = CreateQuadMesh();
                argsDirty = true;
            }

            if (_textInstanceBuffer == null)
            {
                _textInstanceBuffer = new ComputeBuffer(MaxGlyphCount, TooltipGlyphInstanceStride, ComputeBufferType.Structured);
            }

            if (_iconInstanceBuffer == null)
            {
                _iconInstanceBuffer = new ComputeBuffer(MaxIconCount, TooltipGlyphInstanceStride, ComputeBufferType.Structured);
            }

            if (_textArgsBuffer == null)
            {
                _textArgsBuffer = new ComputeBuffer(1, IndirectArgsStride, ComputeBufferType.IndirectArguments);
                _boundTextArgsCount = -1;
                argsDirty = true;
            }

            if (_iconArgsBuffer == null)
            {
                _iconArgsBuffer = new ComputeBuffer(1, IndirectArgsStride, ComputeBufferType.IndirectArguments);
                _boundIconArgsCount = -1;
                argsDirty = true;
            }

            if (_fontUvBuffer == null)
            {
                _fontUvBuffer = new ComputeBuffer(UvTableCapacity, UvRectStride, ComputeBufferType.Structured);
            }

            if (_spriteUvBuffer == null)
            {
                _spriteUvBuffer = new ComputeBuffer(UvTableCapacity, UvRectStride, ComputeBufferType.Structured);
            }

            if (argsDirty)
            {
                RefreshIndirectArgs(_textArgsBuffer);
                RefreshIndirectArgs(_iconArgsBuffer);
            }

            _resourceObjectsReady = _runtimeQuadMesh != null
                && _textInstanceBuffer != null
                && _iconInstanceBuffer != null
                && _textArgsBuffer != null
                && _iconArgsBuffer != null
                && _fontUvBuffer != null
                && _spriteUvBuffer != null;
        }

        private void EnsureMaterials()
        {
            if (!_materialResolveAttempted)
            {
                if (_runtimeGlyphMaterial == null)
                    _runtimeGlyphMaterial = glyphMaterial;

                if (_runtimeIconMaterial == null)
                    _runtimeIconMaterial = iconMaterial;

                _materialResolveAttempted = true;
            }

            if (_runtimeGlyphMaterial == null || _runtimeIconMaterial == null)
            {
                _materialResolveFailed = true;
                return;
            }

            Shader resolvedShader = glyphShader != null ? glyphShader : _runtimeGlyphMaterial.shader;
            if (resolvedShader == null || _runtimeGlyphMaterial.shader != resolvedShader || _runtimeIconMaterial.shader != resolvedShader)
            {
                _materialResolveFailed = true;
                return;
            }

            glyphShader = resolvedShader;

            EnsurePropertyBlocks();
            _materialsReady = _textPropertyBlock != null && _iconPropertyBlock != null;
        }

        private void EnsurePropertyBlocks()
        {
            if (_textPropertyBlock == null)
                _textPropertyBlock = new MaterialPropertyBlock();

            if (_iconPropertyBlock == null)
                _iconPropertyBlock = new MaterialPropertyBlock();
        }

        private void UploadUvTablesIfDirty()
        {
            if (_fontUvTableDirty && _fontUvBuffer != null)
            {
                _fontUvBuffer.SetData(_fontUvTable);
                _fontUvTableDirty = false;
            }

            if (_spriteUvTableDirty && _spriteUvBuffer != null)
            {
                _spriteUvBuffer.SetData(_spriteUvTable);
                _spriteUvTableDirty = false;
            }
        }

        private void RefreshIndirectArgs(ComputeBuffer argsBuffer)
        {
            if (_runtimeQuadMesh == null || argsBuffer == null)
                return;

            _indirectArgs[0] = _runtimeQuadMesh.GetIndexCount(0);
            _indirectArgs[1] = 0u;
            _indirectArgs[2] = _runtimeQuadMesh.GetIndexStart(0);
            _indirectArgs[3] = _runtimeQuadMesh.GetBaseVertex(0);
            _indirectArgs[4] = 0u;
            argsBuffer.SetData(_indirectArgs);
        }

        private void DrawBatch(
            Vector3 anchorPosition,
            Camera camera,
            Vector3 cameraRight,
            Vector3 cameraUp,
            Vector3 cameraForward,
            Bounds bounds,
            Vector2[] localCenters,
            Vector2[] localScales,
            int[] glyphIndices,
            Material material,
            ComputeBuffer instanceBuffer,
            ComputeBuffer argsBuffer,
            ComputeBuffer uvBuffer,
            Texture mainTexture,
            MaterialPropertyBlock propertyBlock,
            ref Texture boundTexture,
            ref ComputeBuffer boundInstanceBuffer,
            ref ComputeBuffer boundUvBuffer,
            ref float boundGradientScale,
            ref float boundFaceDilate,
            ref float boundDitherEnabled,
            ref int boundArgsCount,
            int count,
            int renderLayer,
            Vector4 tint,
            float ditherEnabled)
        {
            if (instanceBuffer == null || argsBuffer == null || uvBuffer == null || mainTexture == null || propertyBlock == null)
                return;

            for (int i = 0; i < count; i++)
            {
                Vector2 localCenter = localCenters[i];
                Vector2 localScale = localScales[i];
                Vector3 worldPosition = anchorPosition + cameraRight * localCenter.x + cameraUp * localCenter.y;
                _instancePayloads[i] = new TooltipGlyphInstance
                {
                    LocalToWorld = BuildBillboardMatrix(worldPosition, cameraRight, cameraUp, cameraForward, localScale),
                    Tint = tint,
                    GlyphIndex = new Vector4(glyphIndices[i], 0f, 0f, 0f)
                };
            }

            instanceBuffer.SetData(_instancePayloads, 0, 0, count);
            if (boundArgsCount != count)
            {
                _indirectArgs[1] = (uint)count;
                argsBuffer.SetData(_indirectArgs);
                boundArgsCount = count;
            }

            BindPropertyBlockIfDirty(
                propertyBlock,
                mainTexture,
                instanceBuffer,
                uvBuffer,
                ditherEnabled,
                ref boundTexture,
                ref boundInstanceBuffer,
                ref boundUvBuffer,
                ref boundGradientScale,
                ref boundFaceDilate,
                ref boundDitherEnabled);

            UnityEngine.Graphics.DrawMeshInstancedIndirect(
                _runtimeQuadMesh,
                0,
                material,
                bounds,
                argsBuffer,
                0,
                propertyBlock,
                ShadowCastingMode.Off,
                false,
                renderLayer,
                camera,
                LightProbeUsage.Off,
                null);
        }

        private void BindPropertyBlockIfDirty(
            MaterialPropertyBlock propertyBlock,
            Texture mainTexture,
            ComputeBuffer instanceBuffer,
            ComputeBuffer uvBuffer,
            float ditherEnabled,
            ref Texture boundTexture,
            ref ComputeBuffer boundInstanceBuffer,
            ref ComputeBuffer boundUvBuffer,
            ref float boundGradientScale,
            ref float boundFaceDilate,
            ref float boundDitherEnabled)
        {
            float resolvedGradientScale = ResolveGradientScale();
            float resolvedFaceDilate = ResolveFaceDilate();
            if (ReferenceEquals(boundTexture, mainTexture)
                && ReferenceEquals(boundInstanceBuffer, instanceBuffer)
                && ReferenceEquals(boundUvBuffer, uvBuffer)
                && boundGradientScale == resolvedGradientScale
                && boundFaceDilate == resolvedFaceDilate
                && boundDitherEnabled == ditherEnabled)
                return;

            propertyBlock.Clear();
            propertyBlock.SetTexture(MainTexId, mainTexture);
            propertyBlock.SetFloat(GradientScaleId, resolvedGradientScale);
            propertyBlock.SetFloat(FaceDilateId, resolvedFaceDilate);
            propertyBlock.SetFloat(DitherEnabledId, ditherEnabled);
            propertyBlock.SetBuffer(InstanceBufferId, instanceBuffer);
            propertyBlock.SetBuffer(UvRectBufferId, uvBuffer);
            boundTexture = mainTexture;
            boundInstanceBuffer = instanceBuffer;
            boundUvBuffer = uvBuffer;
            boundGradientScale = resolvedGradientScale;
            boundFaceDilate = resolvedFaceDilate;
            boundDitherEnabled = ditherEnabled;
        }

        private float ResolveGradientScale()
        {
            return math.isfinite(gradientScale) ? math.clamp(gradientScale, MinGradientScale, MaxGradientScale) : DefaultGradientScale;
        }

        private float ResolveFaceDilate()
        {
            return math.isfinite(faceDilate) ? math.clamp(faceDilate, MinFaceDilate, MaxFaceDilate) : DefaultFaceDilate;
        }

        private float ResolveGlyphWorldHeight()
        {
            return math.isfinite(glyphWorldHeight) ? math.clamp(glyphWorldHeight, MinGlyphWorldHeight, MaxGlyphWorldHeight) : DefaultGlyphWorldHeight;
        }

        private float ResolveGlyphAdvanceScale()
        {
            return math.isfinite(glyphAdvanceScale) ? math.clamp(glyphAdvanceScale, MinGlyphAdvanceScale, MaxGlyphAdvanceScale) : DefaultGlyphAdvanceScale;
        }

        private float ResolveFadeDurationSeconds()
        {
            return math.isfinite(fadeDurationSeconds) ? math.clamp(fadeDurationSeconds, MinFadeDurationSeconds, MaxFadeDurationSeconds) : DefaultFadeDurationSeconds;
        }

        private Vector3 ResolveAnchorPosition(Vector3 cameraPosition)
        {
            Vector3 anchor = _diagnosticActive
                ? _diagnosticWorldAnchor
                : ResolveSignalAnchorRuntime() + worldOffset;

            if (_activeSchemeHash == InputSchemeHashXRTouch)
                anchor = ApplyVrDepthOffset(anchor, cameraPosition);

            return anchor;
        }

        private Vector3 ResolveSignalAnchorRuntime()
        {
            Vector3 aupRuntime = (Vector3)_activeTargetAup.ToRuntimeFloat3();
            return IsFinite(aupRuntime) ? aupRuntime : _activeRuntimeAnchor;
        }

        private Vector3 ApplyVrDepthOffset(Vector3 anchor, Vector3 cameraPosition)
        {
            Vector3 toCamera = cameraPosition - anchor;
            float distanceSq = toCamera.sqrMagnitude;
            if (distanceSq <= 0.0001f)
                return anchor;

            float depthOffset = math.isfinite(vrDepthOffsetMeters) ? math.max(0f, vrDepthOffsetMeters) : 0f;
            return anchor + toCamera * (depthOffset * math.rsqrt(distanceSq));
        }

        private Camera ResolveCamera()
        {
            if (interactionCamera != null && interactionCamera.isActiveAndEnabled)
            {
                if (!_cachedRenderCameraFromInteraction || !ReferenceEquals(_cachedRenderCamera, interactionCamera))
                    CacheRenderCamera(interactionCamera, fromInteraction: true);

                return _cachedRenderCamera;
            }

            if (_cachedRenderCameraFromInteraction)
                CacheRenderCamera(null, fromInteraction: false);

            if (_cachedRenderCamera != null && _cachedRenderCamera.isActiveAndEnabled)
                return _cachedRenderCamera;

            if (_cachedRenderCamera != null)
                CacheRenderCamera(null, fromInteraction: false);

            IPlayerRuntimeContext playerContext = _cachedPlayerContext;
            Camera playerCamera = playerContext != null ? playerContext.PlayerCamera : null;
            CacheRenderCamera(playerCamera != null && playerCamera.isActiveAndEnabled ? playerCamera : null, fromInteraction: false);
            return _cachedRenderCamera;
        }

        private void CacheRenderCamera(Camera camera, bool fromInteraction)
        {
            _cachedRenderCamera = camera;
            _cachedRenderCameraTransform = camera != null ? camera.transform : null;
            _cachedRenderCameraFromInteraction = fromInteraction;
        }

        private Camera ResolveRenderCamera()
        {
            Camera targetCamera = ResolveCamera();
            if (targetCamera == null)
                return null;

            Camera currentCamera = GlobalRenderContext.CurrentCamera;
            if (currentCamera == null)
                return targetCamera;

            if (targetCamera != null && !ReferenceEquals(currentCamera, targetCamera))
                return null;

            return currentCamera;
        }

        private uint ResolveCurrentSchemeHash()
        {
            IInputDeterminismService input = _inputDeterminism;
            if (input == null)
                return InputSchemeHashKeyboardMouse;

            PlayerInputState state = input.GetState();
            return state.CurrentInputSchemeHash != 0u
                ? state.CurrentInputSchemeHash
                : InputSchemeHashKeyboardMouse;
        }

        private bool RefreshActiveSchemeHash()
        {
            uint schemeHash = ResolveCurrentSchemeHash();
            if (schemeHash == _activeSchemeHash)
                return false;

            _activeSchemeHash = schemeHash;
            return true;
        }

        private void RefreshInputDeterminismService()
        {
            IInputService registeredInput = GlobalRegistry.RegisteredInput;
            if (registeredInput != null)
            {
                _inputDeterminism = registeredInput;
                return;
            }

            _inputDeterminism = GlobalRegistry.InputDeterminism;
        }

        private void CacheRegistryServicesCold()
        {
            _cachedPlayerContext = GlobalRegistry.Player;
        }

        private float ResolveQualityFadeDurationSeconds()
        {
            float authoredDuration = ResolveFadeDurationSeconds();
            float quality = math.saturate(_qualityWeight01);
            float curve = quality * quality * (3f - (2f * quality));
            return math.max(0.0001f, math.lerp(0.0001f, authoredDuration, curve));
        }

        private static float ResolveDitherWeight(float qualityWeight01)
        {
            float quality = math.saturate(qualityWeight01);
            return math.saturate((quality - 0.2f) * 2.5f);
        }

        private void RefreshVisibleDistanceCache()
        {
            float authoredDistance = math.isfinite(maxVisibleDistance) ? maxVisibleDistance : MinVisibleDistanceMeters;
            float distance = math.clamp(authoredDistance, MinVisibleDistanceMeters, MaxVisibleDistanceMeters);
            if (_cachedMaxVisibleDistance == distance)
                return;

            _cachedMaxVisibleDistance = distance;
            _cachedMaxVisibleDistanceSq = distance * distance;
            _cachedBoundsSize = Vector3.one * math.max(1f, distance * 0.35f);
        }

        private void RefreshScalabilityPolicy()
        {
            _qualityWeight01 = math.saturate(HomeostasisBrain.GlobalQualityWeight);
        }

        private void TryRegisterRuntime()
        {
            if (!_registeredLateFrame)
                _registeredLateFrame = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.UI);

            if (!_registeredRenderable && Application.isPlaying)
                _registeredRenderable = GlobalRegistry.Renderables.TryRegister(this);
        }

        private void UnregisterRuntime()
        {
            if (_registeredLateFrame)
            {
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.UI);
                _registeredLateFrame = false;
            }

            if (_registeredRenderable)
            {
                GlobalRegistry.Renderables.Unregister(this);
                _registeredRenderable = false;
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

        private void TryRegisterScalabilityListener()
        {
            if (_scalabilityListenerRegistered || !Application.isPlaying)
                return;

            ScalabilityEvents.Register(this);
            _scalabilityListenerRegistered = true;
        }

        private void TryUnregisterScalabilityListener()
        {
            if (!_scalabilityListenerRegistered)
                return;

            ScalabilityEvents.Unregister(this);
            _scalabilityListenerRegistered = false;
        }

        private void EnsureBlackBox()
        {
            if (_blackBox.IsCreated)
                return;

            _blackBox = new NativeArray<TooltipBlackBoxEntry>(
                BlackBoxCapacity,
                Allocator.Persistent,
                NativeArrayOptions.ClearMemory);
            NativeMemorySentinel.RegisterNativeArray(_blackBox, nameof(DiegeticTooltipSystem), nameof(_blackBox), NativeAllocationLifetime.Scene);
        }

        private void RecordBlackBox(Vector3 anchor, Vector4 tint, byte tierFlags)
        {
            if (!_blackBox.IsCreated)
                return;

            float3 anchorPayload = default;
            anchorPayload.x = anchor.x;
            anchorPayload.y = anchor.y;
            anchorPayload.z = anchor.z;

            _blackBoxDumped = false;
            _blackBox[_blackBoxCursor] = new TooltipBlackBoxEntry
            {
                Frame = unchecked((uint)Time.frameCount),
                TargetHash = _activeTargetHash,
                Anchor = anchorPayload,
                Alpha = tint.w,
                SchemeHash = _activeSchemeHash,
                GlyphCount = (ushort)math.min(ushort.MaxValue, _textGlyphCount + _iconCount),
                Flags = (byte)(_diagnosticActive ? 1 : 0),
                TierFlags = tierFlags
            };
            _blackBoxCursor++;
            if (_blackBoxCursor >= BlackBoxCapacity)
                _blackBoxCursor = 0;

            if (_blackBoxWrittenCount < BlackBoxCapacity)
                _blackBoxWrittenCount++;
        }

        private void DumpBlackBox()
        {
            if (_blackBoxDumped || !_blackBox.IsCreated)
                return;

            _blackBoxDumped = true;
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string dumpPath = Path.Combine(projectRoot, DumpRelativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(dumpPath));
            using (FileStream stream = new FileStream(dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read))
            using (BinaryWriter writer = new BinaryWriter(stream))
            {
                writer.Write(_blackBoxCursor);
                writer.Write(_blackBox.Length);
                writer.Write(_blackBoxWrittenCount);

                int firstIndex = _blackBoxWrittenCount >= _blackBox.Length ? _blackBoxCursor : 0;
                for (int i = 0; i < _blackBoxWrittenCount; i++)
                {
                    int entryIndex = firstIndex + i;
                    if (entryIndex >= _blackBox.Length)
                        entryIndex -= _blackBox.Length;

                    TooltipBlackBoxEntry entry = _blackBox[entryIndex];
                    writer.Write(entry.Frame);
                    writer.Write(entry.TargetHash);
                    writer.Write(entry.Anchor.x);
                    writer.Write(entry.Anchor.y);
                    writer.Write(entry.Anchor.z);
                    writer.Write(entry.Alpha);
                    writer.Write(entry.SchemeHash);
                    writer.Write(entry.GlyphCount);
                    writer.Write(entry.Flags);
                    writer.Write(entry.TierFlags);
                }
            }
        }

        private void ReleaseResources()
        {
            _runtimeGlyphMaterial = null;
            _runtimeIconMaterial = null;
            _textPropertyBlock = null;
            _iconPropertyBlock = null;
            _boundTextTexture = null;
            _boundIconTexture = null;
            _boundTextInstanceBuffer = null;
            _boundIconInstanceBuffer = null;
            _boundTextUvBuffer = null;
            _boundIconUvBuffer = null;
            _boundTextGradientScale = float.NaN;
            _boundIconGradientScale = float.NaN;
            _boundTextFaceDilate = float.NaN;
            _boundIconFaceDilate = float.NaN;
            _boundTextDitherEnabled = float.NaN;
            _boundIconDitherEnabled = float.NaN;
            _boundTextArgsCount = -1;
            _boundIconArgsCount = -1;
            _materialResolveAttempted = false;
            _materialResolveFailed = false;
            _materialsReady = false;

            if (_runtimeQuadMesh != null)
            {
                Destroy(_runtimeQuadMesh);
                _runtimeQuadMesh = null;
            }

            if (_textInstanceBuffer != null)
            {
                _textInstanceBuffer.Release();
                _textInstanceBuffer = null;
            }

            if (_iconInstanceBuffer != null)
            {
                _iconInstanceBuffer.Release();
                _iconInstanceBuffer = null;
            }

            if (_textArgsBuffer != null)
            {
                _textArgsBuffer.Release();
                _textArgsBuffer = null;
            }

            if (_iconArgsBuffer != null)
            {
                _iconArgsBuffer.Release();
                _iconArgsBuffer = null;
            }

            if (_fontUvBuffer != null)
            {
                _fontUvBuffer.Release();
                _fontUvBuffer = null;
            }

            if (_spriteUvBuffer != null)
            {
                _spriteUvBuffer.Release();
                _spriteUvBuffer = null;
            }

            if (_blackBox.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeArray(_blackBox);
                _blackBox.Dispose();
            }
            _blackBox = default;

            _blackBoxCursor = 0;
            _blackBoxWrittenCount = 0;
            _blackBoxDumped = false;
            _resourceObjectsReady = false;
        }

        private static float MoveTowardsFast(float current, float target, float maxDelta)
        {
            float delta = target - current;
            float safeDelta = math.max(0f, maxDelta);
            if (math.abs(delta) <= safeDelta)
                return target;

            return current + math.sign(delta) * safeDelta;
        }

        private static char NormalizeTooltipCharacter(char value)
        {
            if (value == '\n' || value == '\r' || value == '\t')
                return ' ';

            if (value >= 'a' && value <= 'z')
                return (char)(value - 32);

            return value < AsciiCacheSize ? value : '?';
        }

        private static bool IsFinite(Vector3 value)
        {
            return math.isfinite(value.x)
                && math.isfinite(value.y)
                && math.isfinite(value.z);
        }

        private static Mesh CreateQuadMesh()
        {
            Mesh mesh = new Mesh
            {
                name = "DiegeticTooltipGlyphQuad",
                hideFlags = HideFlags.DontSave
            };

            mesh.SetVertices(s_quadVertices);
            mesh.SetUVs(0, s_quadUvs);
            mesh.SetTriangles(s_quadIndices, 0, true);
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Matrix4x4 BuildBillboardMatrix(
            Vector3 position,
            Vector3 right,
            Vector3 up,
            Vector3 forward,
            Vector2 scale)
        {
            Matrix4x4 matrix = default;
            matrix.m00 = right.x * scale.x;
            matrix.m10 = right.y * scale.x;
            matrix.m20 = right.z * scale.x;
            matrix.m30 = 0f;

            matrix.m01 = up.x * scale.y;
            matrix.m11 = up.y * scale.y;
            matrix.m21 = up.z * scale.y;
            matrix.m31 = 0f;

            matrix.m02 = forward.x;
            matrix.m12 = forward.y;
            matrix.m22 = forward.z;
            matrix.m32 = 0f;

            matrix.m03 = position.x;
            matrix.m13 = position.y;
            matrix.m23 = position.z;
            matrix.m33 = 1f;
            return matrix;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct TooltipGlyphInstance
        {
            public Matrix4x4 LocalToWorld;
            public Vector4 Tint;
            public Vector4 GlyphIndex;
        }

        [StructLayout(LayoutKind.Explicit, Size = 32)]
        private struct TooltipBlackBoxEntry
        {
            [FieldOffset(0)] public uint Frame;
            [FieldOffset(4)] public uint TargetHash;
            [FieldOffset(8)] public float3 Anchor;
            [FieldOffset(20)] public float Alpha;
            [FieldOffset(24)] public uint SchemeHash;
            [FieldOffset(28)] public ushort GlyphCount;
            [FieldOffset(30)] public byte Flags;
            [FieldOffset(31)] public byte TierFlags;
        }
    }
}
