using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using Hecton8.Core;
using Hecton8.Core.Contracts;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Memory;
using Hecton8.UI.Diegetic.Contracts;
using Hecton8.World;
using TMPro;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
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
    public sealed class DiegeticTooltipSystem : MonoBehaviour, ISlowTickable, ILateFrameTickable, IRenderable, IGlobalRegistryHotSwapListener
    {
        private const int MaxGlyphCount = 96;
        private const int MaxIconCount = 1;
        private const int MaxInstanceCount = MaxGlyphCount + MaxIconCount;
        private const int IndirectArgsCount = 5;
        private const int PromptBufferCapacity = 64;
        private const int AsciiCacheSize = 128;
        private const int UvTableCapacity = 128;
        private const int BlackBoxCapacity = 300;
        private const SystemID VaultOwnerSystemId = SystemID.UI;
        private const BufferID BlackBoxBufferId = BufferID.DiegeticTooltipBlackBox;
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

        [Header("References")]
        [SerializeField, Tooltip("Optional explicit readable SDF font used for diegetic tooltip glyphs.")]
        private TMP_FontAsset fontAsset;
        [SerializeField, Tooltip("Optional explicit TMP sprite asset used for interact binding icons.")]
        private TMP_SpriteAsset spriteAsset;
        [SerializeField, Tooltip("Required authored font material for diegetic tooltip glyphs.")]
        private Material glyphMaterial;
        [SerializeField, Tooltip("Required authored icon material for diegetic tooltip binding icons.")]
        private Material iconMaterial;
        [SerializeField, Tooltip("Required authored quad mesh for tooltip glyph and icon indirect draws. Runtime mesh generation is forbidden.")]
        private Mesh glyphQuadMesh;
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

        private Material _resolvedGlyphMaterial;
        private Material _resolvedIconMaterial;
        private MaterialPropertyBlock _textPropertyBlock;
        private MaterialPropertyBlock _iconPropertyBlock;
        private Texture _boundTextTexture;
        private Texture _boundIconTexture;
        private GraphicsBuffer _boundTextInstanceBuffer;
        private GraphicsBuffer _boundIconInstanceBuffer;
        private GraphicsBuffer _boundTextUvBuffer;
        private GraphicsBuffer _boundIconUvBuffer;
        private Mesh _resolvedQuadMesh;
        private GraphicsBuffer _textInstanceBuffer;
        private GraphicsBuffer _iconInstanceBuffer;
        private GraphicsBuffer _textArgsBuffer;
        private GraphicsBuffer _iconArgsBuffer;
        private GraphicsBuffer _fontUvBuffer;
        private GraphicsBuffer _spriteUvBuffer;
        private VaultGenerationHandle<TooltipBlackBoxEntry> _blackBoxHandle;
        private IDataVault _dataVault;
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
        private bool _registeredSlowTick;
        private bool _registeredRenderable;
        private bool _hotSwapListenerRegistered;
        private bool _fontUvTableDirty;
        private bool _spriteUvTableDirty;
        private bool _resourceObjectsReady;
        private bool _quadMeshSetupPermanentlyFailed;
        private bool _materialResolveAttempted;
        private bool _materialResolveFailed;
        private bool _materialsReady;
        private float _qualityWeight01 = 1f;
        private bool _cachedRenderCameraFromInteraction;
        private bool _textSinkHasPayload;
        private bool _blackBoxDumpQueued;
        private bool _blackBoxDumped;

        public void LateFrameTick()
        {
            float deltaTime = math.max(0f, SystemDispatcher.CurrentFrameDeltaTime);
            RefreshScalabilityPolicy();
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

            if (!_resourceObjectsReady || !_materialsReady || _resolvedQuadMesh == null)
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
                QueueBlackBoxDump();
                ClearTooltipState();
                return;
            }

            Color resolvedColor = _diagnosticActive ? _diagnosticColor : glyphColor;
            Vector4 tint = default;
            tint.x = resolvedColor.r;
            tint.y = resolvedColor.g;
            tint.z = resolvedColor.b;
            tint.w = resolvedColor.a * _visibleAlpha;
            float qualityWeight = math.saturate(_qualityWeight01);
            float ditherEnabled = ResolveDitherWeight(qualityWeight);
            int renderLayer = gameObject.layer;
            Bounds bounds = new Bounds(anchorPosition, _cachedBoundsSize);
            UploadUvTablesIfDirty();

            if (_iconCount > 0 && _resolvedIconMaterial != null)
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
                    _resolvedIconMaterial,
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

            if (_textGlyphCount > 0 && _resolvedGlyphMaterial != null)
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
                    _resolvedGlyphMaterial,
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
            CacheRegistryServicesCold();
            RefreshScalabilityPolicy();
            RefreshInputDeterminismService();
            _activeSchemeHash = ResolveCurrentSchemeHash();
        }

        private void Start()
        {
            TryRegisterRuntime();
            TryRegisterHotSwapListener();
            CacheRegistryServicesCold();
            RefreshScalabilityPolicy();
            RefreshInputDeterminismService();
            _activeSchemeHash = ResolveCurrentSchemeHash();
        }

        public void SlowTick()
        {
            FlushQueuedBlackBoxDump();
        }

        private void OnDisable()
        {
            FlushQueuedBlackBoxDump();
            UnregisterRuntime();
            TryUnregisterHotSwapListener();
            ClearTooltipState();
            _promptLength = 0;
            CacheRenderCamera(null, fromInteraction: false);
            _inputDeterminism = null;
        }

        private void OnDestroy()
        {
            FlushQueuedBlackBoxDump();
            UnregisterRuntime();
            TryUnregisterHotSwapListener();
            ReleaseResources();
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot == GlobalRegistryServiceSlot.DataVault)
            {
                IDataVault previousVault = previousService is IDataVault oldVault ? oldVault : null;
                IDataVault nextVault = currentService is IDataVault vault ? vault : null;
                RebindDataVaultForLifecycle(nextVault, previousVault);
                if (_dataVault != null)
                    EnsureBlackBox();
                return;
            }

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
            BuildGlyphLayout(_promptBuffer.AsSpan(0, _promptLength), includeBindingIcon: false);
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
                _activeRuntimeAnchor = default;
                _activeRuntimeAnchor.x = signal.RuntimeAnchor.x;
                _activeRuntimeAnchor.y = signal.RuntimeAnchor.y;
                _activeRuntimeAnchor.z = signal.RuntimeAnchor.z;
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
                Vector3 shift = default;
                shift.x = signal.ShiftMeters.x;
                shift.y = signal.ShiftMeters.y;
                shift.z = signal.ShiftMeters.z;
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

            if (!_resourceObjectsReady || !_materialsReady)
            {
                _textGlyphCount = 0;
                _iconCount = 0;
                ClearTextSink();
                return;
            }

            BuildGlyphLayout(_promptBuffer.AsSpan(0, _promptLength), includeBindingIcon: true);
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
                Vector2 iconCenter = default;
                iconCenter.x = originX + iconWidth * 0.5f;
                iconCenter.y = IconVerticalBias;
                _iconLocalCenters[0] = iconCenter;
                Vector2 iconScale = default;
                iconScale.x = iconWidth;
                iconScale.y = iconHeight;
                _iconLocalScales[0] = iconScale;
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

                Vector2 localCenter = default;
                localCenter.x = penX + bearingX + width * 0.5f;
                localCenter.y = bearingY - height * 0.5f - baselineOffset;
                _textGlyphLocalCenters[_textGlyphCount] = localCenter;
                Vector2 localScale = default;
                localScale.x = width;
                localScale.y = height;
                _textGlyphLocalScales[_textGlyphCount] = localScale;
                int glyphIndex = c < UvTableCapacity ? c : '?';
                _textGlyphIndices[_textGlyphCount] = glyphIndex;
                Vector4 uvRect = default;
                uvRect.x = rect.x * invAtlasWidth;
                uvRect.y = rect.y * invAtlasHeight;
                uvRect.z = (rect.x + rect.width) * invAtlasWidth;
                uvRect.w = (rect.y + rect.height) * invAtlasHeight;
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
            Vector4 uvRect = default;
            uvRect.x = rect.x * invAtlasWidth;
            uvRect.y = rect.y * invAtlasHeight;
            uvRect.z = (rect.x + rect.width) * invAtlasWidth;
            uvRect.w = (rect.y + rect.height) * invAtlasHeight;
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
            if (font == null || font.characterTable == null)
                return;

            List<TMP_Character> characterTable = font.characterTable;
            for (int i = 0; i < characterTable.Count; i++)
            {
                TMP_Character character = characterTable[i];
                if (character == null || character.unicode >= AsciiCacheSize)
                    continue;

                _asciiCharacters[(int)character.unicode] = character;
            }

            TMP_Character fallback = _asciiCharacters['?'];
            TMP_Character space = _asciiCharacters[' '];
            if (fallback == null)
                fallback = space;

            for (int i = 0; i < AsciiCacheSize; i++)
            {
                if (_asciiCharacters[i] != null)
                    continue;

                _asciiCharacters[i] = i == ' ' ? space : fallback;
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

        /// <summary>
        /// Resolves the authored glyph quad plus the tooltip GraphicsBuffer set, or latches the lane off.
        /// </summary>
        /// <remarks>
        /// This ran on a PER-TICK path with no latch of any kind, which is why it was unbounded.
        /// <c>UnityEngine.Assertions.Assert</c> THROWS in this project - nothing under Assets sets
        /// <c>Assert.raiseExceptions = false</c> - and the throw made <c>_resolvedQuadMesh = glyphQuadMesh</c>
        /// unreachable, so the <c>_resolvedQuadMesh == null</c> guard could never close and every later call
        /// re-entered and re-threw. RepairTool.UsePrimary (RepairTool.cs:752) calls TryHandleModuleRepair
        /// (:804), which calls PublishIntegrityDiagnostic at :861 and :875, which calls
        /// <see cref="ShowDiagnostic"/> (RepairTool.cs:2409) - so it threw on every input tick for as long as
        /// the player held the repair tool on a module. RepairTool.LateFrameTick (:1024) reaches the same
        /// method through ApplyDiagnosticLaserPreview (:1051, :1322).
        ///
        /// The killed statement tails were the real cost, and they were large. Inside this method the throw
        /// killed the mesh assignment, the indexed-submesh validation, the second assert (which therefore
        /// could never report), and the creation of all SEVEN tooltip GraphicsBuffers plus the
        /// <c>_resourceObjectsReady</c> assignment. In <see cref="EnsureResources"/> it killed the
        /// EnsureMaterials call, so the glyph/icon materials and property blocks never resolved either. In
        /// OnEnable it killed EnsureBlackBox, TryRegisterRuntime, TryRegisterHotSwapListener,
        /// CacheRegistryServicesCold, RefreshScalabilityPolicy, RefreshInputDeterminismService and the scheme
        /// hash - Start re-runs all but EnsureBlackBox, so the first boot was partly self-healing, but any
        /// later OnDisable/OnEnable cycle lost the IRenderable/ISlowTickable/ILateFrameTickable registration
        /// permanently because Start never runs again. In ShowDiagnostic it killed the entire diagnostic:
        /// RefreshActiveSchemeHash, the anchor, the colour, <c>_diagnosticActive</c>, StagePrompt and
        /// BuildGlyphLayout.
        ///
        /// The asserts are REPLACED, not reordered. An unassigned or non-indexed quad mesh already degrades
        /// completely and silently by construction: <see cref="Render"/> returns at :238 on
        /// <c>!_resourceObjectsReady || _resolvedQuadMesh == null</c>, RebuildActiveTooltipLayout returns at
        /// :615, RefreshIndirectArgs returns at :1109 and UploadUvTablesIfDirty null-checks its buffers. So
        /// the throw bought no recovery over the one-shot log below - only the amputated tails above.
        ///
        /// The early return also skips the seven GraphicsBuffer allocations, deliberately: with a mesh that
        /// can never resolve, <c>_resourceObjectsReady</c> can never become true, so those buffers would be
        /// allocated and never bound. Every consumer sits behind that gate (:238, :615) and ReleaseResources
        /// null-checks each handle, so skipping them is safe as well as cheaper.
        ///
        /// Scope stays narrow on purpose: the dispatcher lanes are NOT unregistered. Unlike
        /// HectonMarineSnowRenderer, this component's own ticks were never the re-entry source - an external
        /// per-tick caller was - and SlowTick owns the black-box telemetry dump flush, which must keep
        /// running.
        /// </remarks>
        private void EnsureResourceObjects()
        {
            if (_quadMeshSetupPermanentlyFailed)
                return;

            bool argsDirty = false;
            if (_resolvedQuadMesh == null)
            {
                Mesh authoredQuad = glyphQuadMesh;
                bool authoredQuadValid = authoredQuad != null &&
                    authoredQuad.subMeshCount > 0 &&
                    authoredQuad.GetIndexCount(0) > 0u;
                if (!authoredQuadValid)
                {
                    // LATCH FIRST. glyphQuadMesh is serialized and runtime mesh generation is forbidden, so
                    // this gap cannot heal at runtime; refusing to retry is correct, not merely quieter.
                    _quadMeshSetupPermanentlyFailed = true;
                    _resolvedQuadMesh = null;
                    _resourceObjectsReady = false;
                    LogUnusableGlyphQuadMesh(authoredQuad != null);
                    return;
                }

                _resolvedQuadMesh = authoredQuad;
                argsDirty = true;
            }

            if (_textInstanceBuffer == null)
            {
                _textInstanceBuffer = new GraphicsBuffer(
                    GraphicsBuffer.Target.Structured,
                    GraphicsBuffer.UsageFlags.LockBufferForWrite,
                    MaxGlyphCount,
                    TooltipGlyphInstanceStride);
            }

            if (_iconInstanceBuffer == null)
            {
                _iconInstanceBuffer = new GraphicsBuffer(
                    GraphicsBuffer.Target.Structured,
                    GraphicsBuffer.UsageFlags.LockBufferForWrite,
                    MaxIconCount,
                    TooltipGlyphInstanceStride);
            }

            if (_textArgsBuffer == null)
            {
                _textArgsBuffer = new GraphicsBuffer(
                    GraphicsBuffer.Target.IndirectArguments,
                    GraphicsBuffer.UsageFlags.LockBufferForWrite,
                    1,
                    IndirectArgsStride);
                _boundTextArgsCount = -1;
                argsDirty = true;
            }

            if (_iconArgsBuffer == null)
            {
                _iconArgsBuffer = new GraphicsBuffer(
                    GraphicsBuffer.Target.IndirectArguments,
                    GraphicsBuffer.UsageFlags.LockBufferForWrite,
                    1,
                    IndirectArgsStride);
                _boundIconArgsCount = -1;
                argsDirty = true;
            }

            if (_fontUvBuffer == null)
            {
                _fontUvBuffer = new GraphicsBuffer(
                    GraphicsBuffer.Target.Structured,
                    GraphicsBuffer.UsageFlags.LockBufferForWrite,
                    UvTableCapacity,
                    UvRectStride);
            }

            if (_spriteUvBuffer == null)
            {
                _spriteUvBuffer = new GraphicsBuffer(
                    GraphicsBuffer.Target.Structured,
                    GraphicsBuffer.UsageFlags.LockBufferForWrite,
                    UvTableCapacity,
                    UvRectStride);
            }

            if (argsDirty)
            {
                RefreshIndirectArgs(_textArgsBuffer);
                RefreshIndirectArgs(_iconArgsBuffer);
            }

            _resourceObjectsReady = _resolvedQuadMesh != null
                && _textInstanceBuffer != null
                && _iconInstanceBuffer != null
                && _textArgsBuffer != null
                && _iconArgsBuffer != null
                && _fontUvBuffer != null
                && _spriteUvBuffer != null;
        }

        /// <summary>
        /// One-shot report of an unusable authored glyph quad. The latch guarantees single emission, so no
        /// per-tick string work reaches the RepairTool input cadence.
        /// </summary>
        /// <param name="meshAssigned">True when glyphQuadMesh is assigned but lacks an indexed submesh 0.</param>
        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogUnusableGlyphQuadMesh(bool meshAssigned)
        {
            if (!meshAssigned)
            {
                Hecton8.Core.H8Debug.LogError("DiegeticTooltipSystem: serialized field 'glyphQuadMesh' is unassigned. Diegetic interact prompts and repair-tool integrity diagnostics are latched off for this session; Render already gates on this so nothing else degrades. Runtime mesh generation is forbidden - author a unit quad with indexed submesh 0 and assign it in the inspector. This branch used to throw a UnityEngine.Assertions.Assert on every RepairTool input tick, which killed the tooltip GraphicsBuffer allocation, EnsureMaterials, and the whole ShowDiagnostic and OnEnable statement tails.");
                return;
            }

            Hecton8.Core.H8Debug.LogError("DiegeticTooltipSystem: serialized field 'glyphQuadMesh' is assigned but provides no indexed submesh 0, so DrawMeshInstancedIndirect cannot use it. Diegetic interact prompts and repair-tool integrity diagnostics are latched off for this session. Re-author the quad with a real index buffer; runtime mesh generation is forbidden. This assert previously could never even report, because the unassigned-mesh assert above it threw first.");
        }

        private void EnsureMaterials()
        {
            if (!_materialResolveAttempted)
            {
                if (_resolvedGlyphMaterial == null)
                    _resolvedGlyphMaterial = glyphMaterial;

                if (_resolvedIconMaterial == null)
                    _resolvedIconMaterial = iconMaterial;

                _materialResolveAttempted = true;
            }

            if (_resolvedGlyphMaterial == null || _resolvedIconMaterial == null)
            {
                _materialResolveFailed = true;
                return;
            }

            Shader resolvedShader = glyphShader != null ? glyphShader : _resolvedGlyphMaterial.shader;
            if (resolvedShader == null || _resolvedGlyphMaterial.shader != resolvedShader || _resolvedIconMaterial.shader != resolvedShader)
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
                GraphicsBufferUploadUtility.UploadArray(_fontUvBuffer, _fontUvTable, UvTableCapacity);
                _fontUvTableDirty = false;
            }

            if (_spriteUvTableDirty && _spriteUvBuffer != null)
            {
                GraphicsBufferUploadUtility.UploadArray(_spriteUvBuffer, _spriteUvTable, UvTableCapacity);
                _spriteUvTableDirty = false;
            }
        }

        private void RefreshIndirectArgs(GraphicsBuffer argsBuffer)
        {
            if (_resolvedQuadMesh == null || argsBuffer == null)
                return;

            _indirectArgs[0] = _resolvedQuadMesh.GetIndexCount(0);
            _indirectArgs[1] = 0u;
            _indirectArgs[2] = _resolvedQuadMesh.GetIndexStart(0);
            _indirectArgs[3] = _resolvedQuadMesh.GetBaseVertex(0);
            _indirectArgs[4] = 0u;
            GraphicsBufferUploadUtility.UploadArray(argsBuffer, _indirectArgs, _indirectArgs.Length);
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
            GraphicsBuffer instanceBuffer,
            GraphicsBuffer argsBuffer,
            GraphicsBuffer uvBuffer,
            Texture mainTexture,
            MaterialPropertyBlock propertyBlock,
            ref Texture boundTexture,
            ref GraphicsBuffer boundInstanceBuffer,
            ref GraphicsBuffer boundUvBuffer,
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
                TooltipGlyphInstance instance = default;
                instance.LocalToWorld = BuildBillboardMatrix(worldPosition, cameraRight, cameraUp, cameraForward, localScale);
                instance.Tint = tint;
                Vector4 glyphIndex = default;
                glyphIndex.x = glyphIndices[i];
                instance.GlyphIndex = glyphIndex;
                _instancePayloads[i] = instance;
            }

            GraphicsBufferUploadUtility.UploadArray(instanceBuffer, _instancePayloads, count);
            if (boundArgsCount != count)
            {
                _indirectArgs[1] = (uint)count;
                GraphicsBufferUploadUtility.UploadArray(argsBuffer, _indirectArgs, _indirectArgs.Length);
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
                _resolvedQuadMesh,
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
                LightProbeUsage.Off);
        }

        private void BindPropertyBlockIfDirty(
            MaterialPropertyBlock propertyBlock,
            Texture mainTexture,
            GraphicsBuffer instanceBuffer,
            GraphicsBuffer uvBuffer,
            float ditherEnabled,
            ref Texture boundTexture,
            ref GraphicsBuffer boundInstanceBuffer,
            ref GraphicsBuffer boundUvBuffer,
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
            AbsoluteUniversePosition originAup = RuntimeOriginRoute.CurrentRuntimeOriginAup();
            Vector3 aupRuntime = (Vector3)AupPrecisionMath.LocalDeltaFloat3Clamped(
                _activeTargetAup.ToAbsoluteDouble3(),
                originAup.ToAbsoluteDouble3(),
                AupPrecisionMath.DefaultMaxLocalCastMeters,
                default);
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
            float qualityWeight = HomeostasisBrain.GlobalQualityWeight;
            _qualityWeight01 = math.saturate(math.select(_qualityWeight01, qualityWeight, math.isfinite(qualityWeight)));
        }

        private void TryRegisterRuntime()
        {
            if (!_registeredLateFrame)
                _registeredLateFrame = SystemDispatcher.Register((ILateFrameTickable)this, PriorityLayer.UI);

            if (!_registeredSlowTick)
                _registeredSlowTick = SystemDispatcher.Register((ISlowTickable)this, PriorityLayer.UI);

            if (!_registeredRenderable && Application.isPlaying)
                _registeredRenderable = GlobalRegistry.Renderables.TryRegister(this);
        }

        private void UnregisterRuntime()
        {
            if (_registeredLateFrame)
            {
                SystemDispatcher.UnregisterLateFrameTickableDirect(this, PriorityLayer.UI);
                _registeredLateFrame = false;
            }

            if (_registeredSlowTick)
            {
                SystemDispatcher.Unregister((ISlowTickable)this, PriorityLayer.UI);
                _registeredSlowTick = false;
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

        private void EnsureBlackBox()
        {
            IDataVault vault = CacheDataVaultCold();
            if (vault == null)
                return;

            if (!vault.IsCompactionFenceActive &&
                IsBlackBoxHandle(in _blackBoxHandle) &&
                vault.TryReadOnlyHandle(in _blackBoxHandle, out NativeArray<TooltipBlackBoxEntry>.ReadOnly blackBox) &&
                !vault.IsCompactionFenceActive &&
                blackBox.IsCreated &&
                blackBox.Length >= BlackBoxCapacity)
            {
                return;
            }

            if (vault.IsCompactionFenceActive)
                return;

            ReleaseBlackBoxHandle(vault);

            _blackBoxHandle = vault.EnsureGenerationHandle<TooltipBlackBoxEntry>(
                BlackBoxBufferId,
                BlackBoxCapacity,
                VaultOwnerSystemId,
                NativeArrayOptions.ClearMemory);
            if (!IsBlackBoxHandle(in _blackBoxHandle) ||
                vault.IsCompactionFenceActive ||
                !vault.TryReadOnlyHandle(in _blackBoxHandle, out blackBox) ||
                vault.IsCompactionFenceActive ||
                !blackBox.IsCreated ||
                blackBox.Length < BlackBoxCapacity)
            {
                ResetBlackBoxNativeEpochState();
                return;
            }

            _blackBoxCursor = 0;
            _blackBoxWrittenCount = 0;
            _blackBoxDumped = false;
        }

        private IDataVault CacheDataVaultCold()
        {
            IDataVault registryVault = GlobalRegistry.DataVault;
            if (!ReferenceEquals(_dataVault, registryVault))
                RebindDataVaultForLifecycle(registryVault);

            return _dataVault;
        }

        private void RebindDataVaultForLifecycle(IDataVault nextVault, IDataVault fallbackReleaseVault = null)
        {
            if (ReferenceEquals(_dataVault, nextVault))
                return;

            ReleaseBlackBoxHandle(_dataVault ?? fallbackReleaseVault);
            _dataVault = nextVault;
            ResetBlackBoxNativeEpochState();
        }

        private void ReleaseBlackBoxHandle(IDataVault vault)
        {
            if (vault == null ||
                vault.IsCompactionFenceActive ||
                !IsBlackBoxHandle(in _blackBoxHandle) ||
                !vault.TryGetGenerationHandle(BlackBoxBufferId, out VaultGenerationHandle<TooltipBlackBoxEntry> currentHandle) ||
                !IsBlackBoxHandle(in currentHandle) ||
                currentHandle.Generation != _blackBoxHandle.Generation)
            {
                return;
            }

            vault.ReleaseBuffer(in _blackBoxHandle);
        }

        private void ResetBlackBoxNativeEpochState()
        {
            _blackBoxHandle = default;
            _blackBoxCursor = 0;
            _blackBoxWrittenCount = 0;
            _blackBoxDumped = false;
        }

        private static bool IsBlackBoxHandle(in VaultGenerationHandle<TooltipBlackBoxEntry> handle)
        {
            return handle.BufferID == (uint)BlackBoxBufferId &&
                   handle.SystemID == (uint)VaultOwnerSystemId &&
                   handle.Generation != 0u;
        }

        private void RecordBlackBox(Vector3 anchor, Vector4 tint, byte tierFlags)
        {
            IDataVault vault = _dataVault;
            if (vault == null || !IsBlackBoxHandle(in _blackBoxHandle))
                return;

            float3 anchorPayload = default;
            anchorPayload.x = anchor.x;
            anchorPayload.y = anchor.y;
            anchorPayload.z = anchor.z;

            _blackBoxDumped = false;
            bool blackBoxLocked = false;
            try
            {
                if (vault.IsCompactionFenceActive ||
                    !vault.TryAcquireWriteLock(in _blackBoxHandle, VaultOwnerSystemId, out NativeArray<TooltipBlackBoxEntry> blackBox))
                {
                    return;
                }

                blackBoxLocked = true;
                if (vault.IsCompactionFenceActive ||
                    !blackBox.IsCreated ||
                    blackBox.Length < BlackBoxCapacity)
                {
                    return;
                }

                blackBox[_blackBoxCursor] = new TooltipBlackBoxEntry
                {
                    Frame = Hecton8.Core.SystemDispatcher.CurrentFrameId,
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
            finally
            {
                if (blackBoxLocked)
                    vault.ReleaseWriteLock(in _blackBoxHandle, VaultOwnerSystemId);
            }
        }

        private void QueueBlackBoxDump()
        {
            if (!_blackBoxDumped)
                _blackBoxDumpQueued = true;
        }

        private void FlushQueuedBlackBoxDump()
        {
            if (!_blackBoxDumpQueued)
                return;

            _blackBoxDumpQueued = false;
            DumpBlackBox();
        }

        private unsafe void DumpBlackBox()
        {
            IDataVault vault = _dataVault;
            if (_blackBoxDumped ||
                vault == null ||
                vault.IsCompactionFenceActive ||
                !IsBlackBoxHandle(in _blackBoxHandle))
            {
                _blackBoxDumpQueued = !_blackBoxDumped;
                return;
            }

            _blackBoxDumped = true;
            NativeArray<byte> payload = default;
            try
            {
                string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
                string dumpPath = Path.Combine(projectRoot, DumpRelativePath);
                int writtenCount = math.min(_blackBoxWrittenCount, BlackBoxCapacity);
                const int headerBytes = 12;
                const int rowBytes = 32;
                int byteCount = headerBytes + (writtenCount * rowBytes);
                payload = H8Memory.Allocate<byte>(
                    byteCount,
                    VaultOwnerSystemId,
                    Allocator.Temp,
                    NativeArrayOptions.UninitializedMemory);
                if (!payload.IsCreated)
                {
                    _blackBoxDumped = false;
                    _blackBoxDumpQueued = true;
                    return;
                }

                byte* payloadPtr = (byte*)NativeArrayUnsafeUtility.GetUnsafePtr(payload);
                Span<byte> header = new Span<byte>(payloadPtr, headerBytes);
                WriteIntLittleEndian(header.Slice(0, 4), _blackBoxCursor);
                WriteIntLittleEndian(header.Slice(4, 4), BlackBoxCapacity);
                WriteIntLittleEndian(header.Slice(8, 4), _blackBoxWrittenCount);

                int firstIndex = writtenCount >= BlackBoxCapacity ? _blackBoxCursor : 0;
                for (int i = 0; i < writtenCount; i++)
                {
                    int entryIndex = firstIndex + i;
                    if (entryIndex >= BlackBoxCapacity)
                        entryIndex -= BlackBoxCapacity;

                    if (!TryReadBlackBoxEntry(vault, entryIndex, out TooltipBlackBoxEntry entry))
                        entry = default;

                    Span<byte> row = new Span<byte>(payloadPtr + headerBytes + (i * rowBytes), rowBytes);
                    WriteBlackBoxEntry(row, in entry);
                }

                _blackBoxDumped = NativeFaultDumpWriter.TryWriteAll(dumpPath, payload, byteCount);
            }
            catch (IOException)
            {
                _blackBoxDumped = false;
            }
            catch (UnauthorizedAccessException)
            {
                _blackBoxDumped = false;
            }
            catch (ObjectDisposedException)
            {
                _blackBoxDumped = false;
            }
            catch (InvalidOperationException)
            {
                _blackBoxDumped = false;
            }
            catch (ArgumentException)
            {
                _blackBoxDumped = false;
            }
            catch (NotSupportedException)
            {
                _blackBoxDumped = false;
            }
            finally
            {
                if (payload.IsCreated)
                    H8Memory.Release(ref payload, VaultOwnerSystemId);
            }
        }

        private bool TryReadBlackBoxEntry(IDataVault vault, int index, out TooltipBlackBoxEntry entry)
        {
            entry = default;
            if (vault == null ||
                vault.IsCompactionFenceActive ||
                index < 0 ||
                index >= BlackBoxCapacity ||
                !IsBlackBoxHandle(in _blackBoxHandle) ||
                !vault.TryReadOnlyHandle(in _blackBoxHandle, out NativeArray<TooltipBlackBoxEntry>.ReadOnly blackBox) ||
                vault.IsCompactionFenceActive ||
                !blackBox.IsCreated ||
                index >= blackBox.Length)
            {
                return false;
            }

            entry = blackBox[index];
            return !vault.IsCompactionFenceActive;
        }

        private static void WriteBlackBoxEntry(Span<byte> destination, in TooltipBlackBoxEntry entry)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(destination.Slice(0, 4), entry.Frame);
            BinaryPrimitives.WriteUInt32LittleEndian(destination.Slice(4, 4), entry.TargetHash);
            WriteFloatLittleEndian(destination.Slice(8, 4), entry.Anchor.x);
            WriteFloatLittleEndian(destination.Slice(12, 4), entry.Anchor.y);
            WriteFloatLittleEndian(destination.Slice(16, 4), entry.Anchor.z);
            WriteFloatLittleEndian(destination.Slice(20, 4), entry.Alpha);
            BinaryPrimitives.WriteUInt32LittleEndian(destination.Slice(24, 4), entry.SchemeHash);
            BinaryPrimitives.WriteUInt16LittleEndian(destination.Slice(28, 2), entry.GlyphCount);
            destination[30] = entry.Flags;
            destination[31] = entry.TierFlags;
        }

        private static void WriteIntLittleEndian(Span<byte> destination, int value)
        {
            BinaryPrimitives.WriteInt32LittleEndian(destination, value);
        }

        private static void WriteFloatLittleEndian(Span<byte> destination, float value)
        {
            BinaryPrimitives.WriteInt32LittleEndian(destination, BitConverter.SingleToInt32Bits(value));
        }

        private void ReleaseResources()
        {
            _resolvedGlyphMaterial = null;
            _resolvedIconMaterial = null;
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

            _resolvedQuadMesh = null;

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

            IDataVault vault = _dataVault;
            ReleaseBlackBoxHandle(vault);
            ResetBlackBoxNativeEpochState();
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

        [StructLayout(LayoutKind.Explicit, Size = 96)]
        private struct TooltipGlyphInstance
        {
            [FieldOffset(0)]
            public Matrix4x4 LocalToWorld;
            [FieldOffset(64)]
            public Vector4 Tint;
            [FieldOffset(80)]
            public Vector4 GlyphIndex;
        }

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Explicit, Size = 64)]
        private struct TooltipBlackBoxEntry
        {
            [System.Runtime.InteropServices.FieldOffset(0)]
            public uint Frame;
            [System.Runtime.InteropServices.FieldOffset(4)]
            public uint TargetHash;
            [System.Runtime.InteropServices.FieldOffset(8)]
            public float3 Anchor;
            [System.Runtime.InteropServices.FieldOffset(20)]
            public float Alpha;
            [System.Runtime.InteropServices.FieldOffset(24)]
            public uint SchemeHash;
            [System.Runtime.InteropServices.FieldOffset(28)]
            public ushort GlyphCount;
            [System.Runtime.InteropServices.FieldOffset(30)]
            public byte Flags;
            [System.Runtime.InteropServices.FieldOffset(31)]
            public byte TierFlags;
            [System.Runtime.InteropServices.FieldOffset(32)]
            private byte _pad0;
            [System.Runtime.InteropServices.FieldOffset(33)]
            private byte _pad1;
            [System.Runtime.InteropServices.FieldOffset(34)]
            private byte _pad2;
            [System.Runtime.InteropServices.FieldOffset(35)]
            private byte _pad3;
            [System.Runtime.InteropServices.FieldOffset(36)]
            private byte _pad4;
            [System.Runtime.InteropServices.FieldOffset(37)]
            private byte _pad5;
            [System.Runtime.InteropServices.FieldOffset(38)]
            private byte _pad6;
            [System.Runtime.InteropServices.FieldOffset(39)]
            private byte _pad7;
            [System.Runtime.InteropServices.FieldOffset(40)]
            private byte _pad8;
            [System.Runtime.InteropServices.FieldOffset(41)]
            private byte _pad9;
            [System.Runtime.InteropServices.FieldOffset(42)]
            private byte _pad10;
            [System.Runtime.InteropServices.FieldOffset(43)]
            private byte _pad11;
            [System.Runtime.InteropServices.FieldOffset(44)]
            private byte _pad12;
            [System.Runtime.InteropServices.FieldOffset(45)]
            private byte _pad13;
            [System.Runtime.InteropServices.FieldOffset(46)]
            private byte _pad14;
            [System.Runtime.InteropServices.FieldOffset(47)]
            private byte _pad15;
            [System.Runtime.InteropServices.FieldOffset(48)]
            private byte _pad16;
            [System.Runtime.InteropServices.FieldOffset(49)]
            private byte _pad17;
            [System.Runtime.InteropServices.FieldOffset(50)]
            private byte _pad18;
            [System.Runtime.InteropServices.FieldOffset(51)]
            private byte _pad19;
            [System.Runtime.InteropServices.FieldOffset(52)]
            private byte _pad20;
            [System.Runtime.InteropServices.FieldOffset(53)]
            private byte _pad21;
            [System.Runtime.InteropServices.FieldOffset(54)]
            private byte _pad22;
            [System.Runtime.InteropServices.FieldOffset(55)]
            private byte _pad23;
            [System.Runtime.InteropServices.FieldOffset(56)]
            private byte _pad24;
            [System.Runtime.InteropServices.FieldOffset(57)]
            private byte _pad25;
            [System.Runtime.InteropServices.FieldOffset(58)]
            private byte _pad26;
            [System.Runtime.InteropServices.FieldOffset(59)]
            private byte _pad27;
            [System.Runtime.InteropServices.FieldOffset(60)]
            private byte _pad28;
            [System.Runtime.InteropServices.FieldOffset(61)]
            private byte _pad29;
            [System.Runtime.InteropServices.FieldOffset(62)]
            private byte _pad30;
            [System.Runtime.InteropServices.FieldOffset(63)]
            private byte _pad31;
        }
    }
}
