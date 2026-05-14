using System;
using System.IO;
using System.Runtime.InteropServices;
using Hecton8.Bootstrap;
using Hecton8.Core;
using Hecton8.Core.Signals;
using Hecton8.UI.Diegetic.Contracts;
using Hecton8.World;
using TMPro;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TextCore;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Hecton8.UI
{
    /// <summary>
    /// Signal-fed diegetic interact prompt renderer. It draws camera-facing TMP atlas quads in world space without Canvas UI.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/UI/Diegetic Tooltip System")]
    public sealed class DiegeticTooltipSystem : MonoBehaviour, ILateFrameTickable, IRenderable, IGlobalRegistryHotSwapListener
    {
        private const int MaxGlyphCount = 96;
        private const int MaxIconCount = 1;
        private const int MaxInstanceCount = MaxGlyphCount + MaxIconCount;
        private const int IndirectArgsCount = 5;
        private const int PromptBufferCapacity = 64;
        private const int AsciiCacheSize = 128;
        private const int UvTableCapacity = 128;
        private const int BlackBoxCapacity = 300;
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
        private const float CameraResolveRetryIntervalSeconds = 0.5f;
        private const float DefaultFadeDurationSeconds = 0.2f;
        private const string DumpRelativePath = "Docs/AgentLogs/Dump_CONTEXTUAL_UX_PROMPTER.bin";
#if UNITY_EDITOR
        private const string DefaultGlyphShaderPath = "Assets/_Project/Art/Shaders/Hecton_DiegeticTooltipIndirect.shader";
#endif

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
        [SerializeField, Tooltip("Optional explicit tooltip glyph shader. Editor fallback resolves the first-party indirect shader asset path when left null.")]
        private Shader glyphShader;
        [SerializeField, Tooltip("Optional explicit interaction camera. When null, the owner resolves the active player camera.")]
        private Camera interactionCamera;
        [SerializeField, Tooltip("Optional world-space TextMeshPro sink for SetCharArray authoring validation. TextMeshProUGUI is ignored.")]
        private TMP_Text worldSpaceTextSink;

        [Header("Tooltip Layout")]
        [SerializeField, Tooltip("Runtime-space offset above the looked-at AUP anchor.")]
        private Vector3 worldOffset = new Vector3(0f, 0.5f, 0f);
        [SerializeField, Range(0.002f, 0.05f), Tooltip("World height of one tooltip glyph quad.")]
        private float glyphWorldHeight = 0.018f;
        [SerializeField, Range(0.8f, 1.6f), Tooltip("Horizontal advance multiplier applied to glyph metrics.")]
        private float glyphAdvanceScale = 1f;
        [SerializeField, Range(0.05f, 1f), Tooltip("Dither fade duration in seconds. Low tier snaps instead.")]
        private float fadeDurationSeconds = DefaultFadeDurationSeconds;
        [SerializeField, Range(0.02f, 0.25f), Tooltip("VR-only shift toward the camera to avoid stereo clipping.")]
        private float vrDepthOffsetMeters = 0.1f;
        [SerializeField, Range(0.5f, 20f), Tooltip("Maximum camera distance where a diegetic tooltip remains visible.")]
        private float maxVisibleDistance = 6f;
        [SerializeField, Tooltip("Base tooltip tint. Alpha is multiplied by hover fade state.")]
        private Color glyphColor = new Color(0.86f, 0.98f, 1f, 0.96f);

        [Header("Sprite Indices")]
        [SerializeField, Min(0)] private int keyboardInteractSpriteIndex = KeyboardInteractGlyphIndex;
        [SerializeField, Min(0)] private int gamepadInteractSpriteIndex = GamepadInteractGlyphIndex;
        [SerializeField, Min(0)] private int steamDeckInteractSpriteIndex = SteamDeckInteractGlyphIndex;
        [SerializeField, Min(0)] private int xrInteractSpriteIndex = XRInteractGlyphIndex;

        [Header("SDF Tuning")]
        [SerializeField, Range(1f, 24f), Tooltip("Distance-field edge sharpness multiplier forwarded into the tooltip glyph shader.")]
        private float gradientScale = 8f;
        [SerializeField, Range(-1f, 1f), Tooltip("Face dilate forwarded into the tooltip glyph shader for minor legibility tuning.")]
        private float faceDilate;

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
        private Mesh _runtimeQuadMesh;
        private ComputeBuffer _instanceBuffer;
        private ComputeBuffer _argsBuffer;
        private ComputeBuffer _fontUvBuffer;
        private ComputeBuffer _spriteUvBuffer;
        private NativeArray<TooltipBlackBoxEntry> _blackBox;
        private TMP_FontAsset _cachedAsciiFont;
        private TMP_FontAsset _boundFontAsset;
        private TMP_SpriteAsset _boundSpriteAsset;
        private Shader _boundGlyphShader;
        private Camera _cachedRenderCamera;
        private AbsoluteUniversePosition _activeTargetAup;
        private Vector3 _activeRuntimeAnchor;
        private Vector3 _diagnosticWorldAnchor;
        private Color _diagnosticColor = Color.white;
        private uint _activeTargetHash;
        private uint _activePromptHash;
        private uint _activeSchemeHash;
        private uint _lastAupShiftFrame;
        private int _blackBoxCursor;
        private int _promptLength;
        private int _textGlyphCount;
        private int _iconCount;
        private float _visibleAlpha;
        private float _nextCameraResolveTime;
        private bool _hasSignalTarget;
        private bool _diagnosticActive;
        private bool _registeredUpdate;
        private bool _registeredRenderable;
        private bool _hotSwapListenerRegistered;
        private bool _fontUvTableDirty;
        private bool _spriteUvTableDirty;
        private bool _materialBindingsDirty = true;
        private float _boundGradientScale = float.NaN;
        private float _boundFaceDilate = float.NaN;

        public void Tick(float deltaTime)
        {
            ConsumeLookTargetSignals();
            ConsumeAupShiftSignals();

            uint schemeHash = ResolveCurrentSchemeHash();
            if (schemeHash != _activeSchemeHash)
            {
                _activeSchemeHash = schemeHash;
                if (_hasSignalTarget && !_diagnosticActive)
                    RebuildActiveTooltipLayout();
            }

            bool hasVisiblePayload = (_textGlyphCount > 0 || _iconCount > 0) && (_hasSignalTarget || _diagnosticActive);
            float targetAlpha = hasVisiblePayload ? 1f : 0f;
            if (IsLowTier())
            {
                _visibleAlpha = targetAlpha;
                return;
            }

            float fadeDuration = math.max(0.001f, fadeDurationSeconds);
            _visibleAlpha = MoveTowardsFast(_visibleAlpha, targetAlpha, math.max(0f, deltaTime) * math.rcp(fadeDuration));
        }

        public void Render(float deltaTime)
        {
            if (_visibleAlpha <= 0.0001f || (_textGlyphCount == 0 && _iconCount == 0))
                return;

            EnsureResources();
            Camera camera = ResolveCamera();
            if (camera == null || _runtimeQuadMesh == null || _instanceBuffer == null || _argsBuffer == null)
                return;

            Vector3 anchorPosition = ResolveAnchorPosition(camera);
            Vector3 cameraPosition = camera.transform.position;
            float maxDistanceSq = maxVisibleDistance * maxVisibleDistance;
            if ((anchorPosition - cameraPosition).sqrMagnitude > maxDistanceSq)
                return;

            if (!IsFinite(anchorPosition))
            {
                DumpBlackBox();
                ClearTooltipState();
                return;
            }

            Color resolvedColor = _diagnosticActive ? _diagnosticColor : glyphColor;
            Vector4 tint = new Vector4(resolvedColor.r, resolvedColor.g, resolvedColor.b, resolvedColor.a * _visibleAlpha);
            bool lowTier = IsLowTier();
            float ditherEnabled = lowTier ? 0f : 1f;
            Bounds bounds = new Bounds(anchorPosition, Vector3.one * math.max(1f, maxVisibleDistance * 0.35f));

            if (_iconCount > 0 && _runtimeIconMaterial != null)
            {
                DrawBatch(
                    anchorPosition,
                    camera,
                    bounds,
                    _iconLocalCenters,
                    _iconLocalScales,
                    _iconGlyphIndices,
                    _runtimeIconMaterial,
                    _iconCount,
                    tint,
                    ditherEnabled);
            }

            if (_textGlyphCount > 0 && _runtimeGlyphMaterial != null)
            {
                DrawBatch(
                    anchorPosition,
                    camera,
                    bounds,
                    _textGlyphLocalCenters,
                    _textGlyphLocalScales,
                    _textGlyphIndices,
                    _runtimeGlyphMaterial,
                    _textGlyphCount,
                    tint,
                    ditherEnabled);
            }

            RecordBlackBox(anchorPosition, tint, lowTier ? (byte)1 : (byte)0);
        }

        private void OnEnable()
        {
            EnsureResources();
            EnsureBlackBox();
            TryRegisterRuntime();
            TryRegisterHotSwapListener();
            _activeSchemeHash = ResolveCurrentSchemeHash();
        }

        private void Start()
        {
            TryRegisterRuntime();
            TryRegisterHotSwapListener();
            _activeSchemeHash = ResolveCurrentSchemeHash();
        }

        private void OnDisable()
        {
            UnregisterRuntime();
            TryUnregisterHotSwapListener();
            ClearTooltipState();
            _promptLength = 0;
        }

        private void OnDestroy()
        {
            UnregisterRuntime();
            TryUnregisterHotSwapListener();
            ReleaseResources();
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot != GlobalRegistryServiceSlot.Input)
                return;

            _activeSchemeHash = 0u;
            if (_hasSignalTarget && !_diagnosticActive)
                RebuildActiveTooltipLayout();
        }

        public void ShowDiagnostic(Vector3 worldAnchor, ReadOnlySpan<char> text, Color tint)
        {
            if (text.IsEmpty)
            {
                ClearDiagnostic();
                return;
            }

            EnsureResources();
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
                if (signal.ShiftFrameId <= _lastAupShiftFrame)
                    continue;

                _lastAupShiftFrame = signal.ShiftFrameId;
                Vector3 shift = new Vector3(signal.ShiftMeters.x, signal.ShiftMeters.y, signal.ShiftMeters.z);
                if (_hasSignalTarget)
                    _activeRuntimeAnchor += shift;
                if (_diagnosticActive)
                    _diagnosticWorldAnchor += shift;
            }
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
        }

        private void RebuildActiveTooltipLayout()
        {
            if (_promptLength <= 0)
                StageDefaultPrompt();

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
        }

        private void BuildGlyphLayout(ReadOnlySpan<char> prompt, bool includeBindingIcon)
        {
            TMP_FontAsset font = fontAsset != null ? fontAsset : TMP_Settings.defaultFontAsset;
            if (font == null)
            {
                _textGlyphCount = 0;
                _iconCount = 0;
                return;
            }

            RefreshAsciiCharacterCache(font);
            int atlasWidth = Mathf.Max(1, font.atlasWidth);
            int atlasHeight = Mathf.Max(1, font.atlasHeight);
            float glyphScale = glyphWorldHeight * math.rcp(Mathf.Max(1f, font.faceInfo.pointSize));
            float promptAdvance = MeasureAdvance(prompt, glyphScale);

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
                iconAdvance = iconWidth + glyphWorldHeight * IconGapMultiplier;
            }

            float totalAdvance = iconAdvance + promptAdvance;
            float penX = -totalAdvance * 0.5f;
            if (_iconCount > 0)
            {
                _iconLocalCenters[0] = new Vector2(penX + iconWidth * 0.5f, IconVerticalBias);
                _iconLocalScales[0] = new Vector2(iconWidth, iconHeight);
                penX += iconAdvance;
            }

            float baselineOffset = font.faceInfo.ascentLine * glyphScale * 0.36f;
            _textGlyphCount = 0;
            BuildTextRun(atlasWidth, atlasHeight, glyphScale, baselineOffset, penX, prompt);
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
            for (int i = 0; i < text.Length && _textGlyphCount < MaxGlyphCount; i++)
            {
                char c = NormalizeTooltipCharacter(text[i]);
                if (c == '\0')
                    continue;

                TMP_Character character = ResolveCachedCharacter(c);
                if (character == null || character.glyph == null)
                    continue;

                Glyph glyph = character.glyph;
                if (c == ' ')
                {
                    penX += glyph.metrics.horizontalAdvance * glyphScale * glyphAdvanceScale;
                    continue;
                }

                GlyphRect rect = glyph.glyphRect;
                GlyphMetrics metrics = glyph.metrics;
                float width = Mathf.Max(MinimumGlyphScale, metrics.width * glyphScale);
                float height = Mathf.Max(MinimumGlyphScale, metrics.height * glyphScale);
                float bearingX = metrics.horizontalBearingX * glyphScale;
                float bearingY = metrics.horizontalBearingY * glyphScale;

                _textGlyphLocalCenters[_textGlyphCount] = new Vector2(
                    penX + bearingX + width * 0.5f,
                    bearingY - height * 0.5f - baselineOffset);
                _textGlyphLocalScales[_textGlyphCount] = new Vector2(width, height);
                int glyphIndex = c < UvTableCapacity ? c : '?';
                _textGlyphIndices[_textGlyphCount] = glyphIndex;
                _fontUvTable[glyphIndex] = new Vector4(
                    rect.x * invAtlasWidth,
                    rect.y * invAtlasHeight,
                    (rect.x + rect.width) * invAtlasWidth,
                    (rect.y + rect.height) * invAtlasHeight);
                _fontUvTableDirty = true;

                penX += metrics.horizontalAdvance * glyphScale * glyphAdvanceScale;
                _textGlyphCount++;
            }

            return penX;
        }

        private float MeasureAdvance(ReadOnlySpan<char> text, float glyphScale)
        {
            if (text.IsEmpty)
                return 0f;

            float totalAdvance = 0f;
            int visibleGlyphCount = 0;
            for (int i = 0; i < text.Length && visibleGlyphCount < MaxGlyphCount; i++)
            {
                char c = NormalizeTooltipCharacter(text[i]);
                if (c == '\0')
                    continue;

                TMP_Character character = ResolveCachedCharacter(c);
                if (character == null || character.glyph == null)
                    continue;

                totalAdvance += character.glyph.metrics.horizontalAdvance * glyphScale * glyphAdvanceScale;
                visibleGlyphCount++;
            }

            return totalAdvance;
        }

        private bool TryResolveBindingIcon(float glyphScale, out int iconGlyphIndex, out float width, out float height)
        {
            iconGlyphIndex = 0;
            width = 0f;
            height = 0f;
            if (spriteAsset == null || spriteAsset.spriteSheet == null || spriteAsset.spriteCharacterTable == null)
                return false;

            int spriteIndex = ResolveInteractSpriteIndex(_activeSchemeHash != 0u ? _activeSchemeHash : ResolveCurrentSchemeHash());
            if ((uint)spriteIndex >= (uint)spriteAsset.spriteCharacterTable.Count || (uint)spriteIndex >= UvTableCapacity)
                return false;

            TMP_SpriteCharacter spriteCharacter = spriteAsset.spriteCharacterTable[spriteIndex];
            if (spriteCharacter == null || spriteCharacter.glyph == null)
                return false;

            Glyph glyph = spriteCharacter.glyph;
            int atlasWidth = Mathf.Max(1, spriteAsset.spriteSheet.width);
            int atlasHeight = Mathf.Max(1, spriteAsset.spriteSheet.height);
            GlyphRect rect = glyph.glyphRect;
            GlyphMetrics metrics = glyph.metrics;
            float invAtlasWidth = math.rcp(math.max(1f, atlasWidth));
            float invAtlasHeight = math.rcp(math.max(1f, atlasHeight));
            width = Mathf.Max(MinimumGlyphScale, metrics.width * glyphScale * IconScaleMultiplier);
            height = Mathf.Max(MinimumGlyphScale, metrics.height * glyphScale * IconScaleMultiplier);
            _spriteUvTable[spriteIndex] = new Vector4(
                rect.x * invAtlasWidth,
                rect.y * invAtlasHeight,
                (rect.x + rect.width) * invAtlasWidth,
                (rect.y + rect.height) * invAtlasHeight);
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

            bool argsDirty = false;
            if (_runtimeQuadMesh == null)
            {
                _runtimeQuadMesh = CreateQuadMesh();
                argsDirty = true;
            }

            if (_instanceBuffer == null)
            {
                _instanceBuffer = new ComputeBuffer(MaxInstanceCount, Marshal.SizeOf<TooltipGlyphInstance>(), ComputeBufferType.Structured);
                _materialBindingsDirty = true;
            }

            if (_argsBuffer == null)
            {
                _argsBuffer = new ComputeBuffer(1, IndirectArgsCount * sizeof(uint), ComputeBufferType.IndirectArguments);
                argsDirty = true;
            }

            if (_fontUvBuffer == null)
            {
                _fontUvBuffer = new ComputeBuffer(UvTableCapacity, sizeof(float) * 4, ComputeBufferType.Structured);
                _materialBindingsDirty = true;
            }

            if (_spriteUvBuffer == null)
            {
                _spriteUvBuffer = new ComputeBuffer(UvTableCapacity, sizeof(float) * 4, ComputeBufferType.Structured);
                _materialBindingsDirty = true;
            }

            if (argsDirty)
                RefreshIndirectArgs();

            EnsureMaterials();
        }

        private void EnsureMaterials()
        {
            if (_runtimeGlyphMaterial == null || _runtimeIconMaterial == null)
            {
#if UNITY_EDITOR
                if (glyphShader == null)
                    glyphShader = AssetDatabase.LoadAssetAtPath<Shader>(DefaultGlyphShaderPath);
#endif
                if (glyphShader == null)
                    glyphShader = Shader.Find("Hecton8/UI/DiegeticTooltipIndirect");

                if (glyphShader != null && _runtimeGlyphMaterial == null)
                {
                    _runtimeGlyphMaterial = new Material(glyphShader)
                    {
                        enableInstancing = true,
                        hideFlags = HideFlags.DontSave
                    };
                    _materialBindingsDirty = true;
                }

                if (glyphShader != null && _runtimeIconMaterial == null)
                {
                    _runtimeIconMaterial = new Material(glyphShader)
                    {
                        enableInstancing = true,
                        hideFlags = HideFlags.DontSave
                    };
                    _materialBindingsDirty = true;
                }
            }

            if (glyphShader == null && _runtimeGlyphMaterial != null)
                glyphShader = _runtimeGlyphMaterial.shader;
            if (glyphShader == null && _runtimeIconMaterial != null)
                glyphShader = _runtimeIconMaterial.shader;
            if (glyphShader == null)
                return;

            bool needsRebind = _materialBindingsDirty
                || !ReferenceEquals(_boundFontAsset, fontAsset)
                || !ReferenceEquals(_boundSpriteAsset, spriteAsset)
                || !ReferenceEquals(_boundGlyphShader, glyphShader)
                || _boundGradientScale != gradientScale
                || _boundFaceDilate != faceDilate;
            if (!needsRebind)
                return;

            if (_runtimeGlyphMaterial != null && fontAsset != null)
            {
                _runtimeGlyphMaterial.SetTexture(MainTexId, fontAsset.atlasTexture);
                _runtimeGlyphMaterial.SetFloat(GradientScaleId, gradientScale);
                _runtimeGlyphMaterial.SetFloat(FaceDilateId, faceDilate);
                _runtimeGlyphMaterial.SetBuffer(InstanceBufferId, _instanceBuffer);
                _runtimeGlyphMaterial.SetBuffer(UvRectBufferId, _fontUvBuffer);
            }

            if (_runtimeIconMaterial != null && spriteAsset != null && spriteAsset.spriteSheet != null)
            {
                _runtimeIconMaterial.SetTexture(MainTexId, spriteAsset.spriteSheet);
                _runtimeIconMaterial.SetFloat(GradientScaleId, gradientScale);
                _runtimeIconMaterial.SetFloat(FaceDilateId, faceDilate);
                _runtimeIconMaterial.SetBuffer(InstanceBufferId, _instanceBuffer);
                _runtimeIconMaterial.SetBuffer(UvRectBufferId, _spriteUvBuffer);
            }

            _boundFontAsset = fontAsset;
            _boundSpriteAsset = spriteAsset;
            _boundGlyphShader = glyphShader;
            _boundGradientScale = gradientScale;
            _boundFaceDilate = faceDilate;
            _materialBindingsDirty = false;
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

        private void RefreshIndirectArgs()
        {
            if (_runtimeQuadMesh == null || _argsBuffer == null)
                return;

            _indirectArgs[0] = _runtimeQuadMesh.GetIndexCount(0);
            _indirectArgs[1] = 0u;
            _indirectArgs[2] = _runtimeQuadMesh.GetIndexStart(0);
            _indirectArgs[3] = _runtimeQuadMesh.GetBaseVertex(0);
            _indirectArgs[4] = 0u;
            _argsBuffer.SetData(_indirectArgs);
        }

        private void DrawBatch(
            Vector3 anchorPosition,
            Camera camera,
            Bounds bounds,
            Vector2[] localCenters,
            Vector2[] localScales,
            int[] glyphIndices,
            Material material,
            int count,
            Vector4 tint,
            float ditherEnabled)
        {
            UploadUvTablesIfDirty();
            Quaternion faceCameraRotation = Quaternion.LookRotation(camera.transform.forward, camera.transform.up);
            Vector3 cameraRight = camera.transform.right;
            Vector3 cameraUp = camera.transform.up;
            for (int i = 0; i < count; i++)
            {
                Vector2 localCenter = localCenters[i];
                Vector2 localScale = localScales[i];
                Vector3 worldPosition = anchorPosition + cameraRight * localCenter.x + cameraUp * localCenter.y;
                _instancePayloads[i] = new TooltipGlyphInstance
                {
                    LocalToWorld = Matrix4x4.TRS(
                        worldPosition,
                        faceCameraRotation,
                        new Vector3(localScale.x, localScale.y, 1f)),
                    Tint = tint,
                    GlyphIndex = new Vector4(glyphIndices[i], 0f, 0f, 0f)
                };
            }

            _instanceBuffer.SetData(_instancePayloads, 0, 0, count);
            _indirectArgs[1] = (uint)count;
            _argsBuffer.SetData(_indirectArgs);
            material.SetFloat(DitherEnabledId, ditherEnabled);
            material.SetBuffer(InstanceBufferId, _instanceBuffer);
            Graphics.DrawMeshInstancedIndirect(
                _runtimeQuadMesh,
                0,
                material,
                bounds,
                _argsBuffer,
                0,
                null,
                ShadowCastingMode.Off,
                false,
                gameObject.layer,
                camera,
                LightProbeUsage.Off,
                null);
        }

        private Vector3 ResolveAnchorPosition(Camera camera)
        {
            Vector3 anchor = _diagnosticActive
                ? _diagnosticWorldAnchor
                : ResolveSignalAnchorRuntime() + worldOffset;

            uint schemeHash = _activeSchemeHash != 0u ? _activeSchemeHash : ResolveCurrentSchemeHash();
            if (schemeHash == InputSchemeHashXRTouch)
                anchor = ApplyVrDepthOffset(anchor, camera);

            return anchor;
        }

        private Vector3 ResolveSignalAnchorRuntime()
        {
            Vector3 aupRuntime = (Vector3)_activeTargetAup.ToRuntimeFloat3();
            return IsFinite(aupRuntime) ? aupRuntime : _activeRuntimeAnchor;
        }

        private Vector3 ApplyVrDepthOffset(Vector3 anchor, Camera camera)
        {
            if (camera == null)
                return anchor;

            Vector3 toCamera = camera.transform.position - anchor;
            float distanceSq = toCamera.sqrMagnitude;
            if (distanceSq <= 0.0001f)
                return anchor;

            return anchor + toCamera * (vrDepthOffsetMeters * math.rsqrt(distanceSq));
        }

        private Camera ResolveCamera()
        {
            if (interactionCamera != null)
                return interactionCamera;

            if (_cachedRenderCamera != null)
                return _cachedRenderCamera;

            float now = Time.unscaledTime;
            if (now < _nextCameraResolveTime)
                return null;

            _nextCameraResolveTime = now + CameraResolveRetryIntervalSeconds;
            IPlayerRuntimeContext playerContext = GlobalRegistry.Player;
            if (playerContext != null && playerContext.PlayerCamera != null)
            {
                _cachedRenderCamera = playerContext.PlayerCamera;
                interactionCamera = _cachedRenderCamera;
                return _cachedRenderCamera;
            }

            if (GameBootstrapper.TryGetCurrentPlayerTransform(out Transform playerTransform) && playerTransform != null)
                _cachedRenderCamera = playerTransform.GetComponentInChildren<Camera>();

            interactionCamera = _cachedRenderCamera;
            return _cachedRenderCamera;
        }

        private uint ResolveCurrentSchemeHash()
        {
            IInputDeterminismService input = GlobalRegistry.InputDeterminism;
            if (input == null)
                return InputSchemeHashKeyboardMouse;

            PlayerInputState state = input.GetState();
            return state.CurrentInputSchemeHash != 0u
                ? state.CurrentInputSchemeHash
                : InputSchemeHashKeyboardMouse;
        }

        private bool IsLowTier()
        {
            return GlobalRegistry.ScalabilityTierProfileByte == 0;
        }

        private void TryRegisterRuntime()
        {
            if (!_registeredUpdate)
                _registeredUpdate = GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.UI);

            if (!_registeredRenderable && Application.isPlaying)
                _registeredRenderable = GlobalRegistry.Renderables.TryRegister(this);
        }

        private void UnregisterRuntime()
        {
            if (_registeredUpdate)
            {
                GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.UI);
                _registeredUpdate = false;
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
            if (_blackBox.IsCreated)
                return;

            _blackBox = new NativeArray<TooltipBlackBoxEntry>(
                BlackBoxCapacity,
                Allocator.Persistent,
                NativeArrayOptions.ClearMemory);
        }

        private void RecordBlackBox(Vector3 anchor, Vector4 tint, byte tierFlags)
        {
            if (!_blackBox.IsCreated)
                return;

            _blackBox[_blackBoxCursor] = new TooltipBlackBoxEntry
            {
                Frame = unchecked((uint)Time.frameCount),
                TargetHash = _activeTargetHash,
                Anchor = new float3(anchor.x, anchor.y, anchor.z),
                Alpha = tint.w,
                SchemeHash = _activeSchemeHash,
                GlyphCount = (ushort)math.min(ushort.MaxValue, _textGlyphCount + _iconCount),
                Flags = (byte)(_diagnosticActive ? 1 : 0),
                TierFlags = tierFlags
            };
            _blackBoxCursor = (_blackBoxCursor + 1) % BlackBoxCapacity;
        }

        private void DumpBlackBox()
        {
            if (!_blackBox.IsCreated)
                return;

            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string dumpPath = Path.Combine(projectRoot, DumpRelativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(dumpPath));
            using (FileStream stream = new FileStream(dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read))
            using (BinaryWriter writer = new BinaryWriter(stream))
            {
                writer.Write(_blackBoxCursor);
                writer.Write(_blackBox.Length);
                for (int i = 0; i < _blackBox.Length; i++)
                {
                    TooltipBlackBoxEntry entry = _blackBox[i];
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
            if (_runtimeGlyphMaterial != null)
            {
                Destroy(_runtimeGlyphMaterial);
                _runtimeGlyphMaterial = null;
            }

            if (_runtimeIconMaterial != null)
            {
                Destroy(_runtimeIconMaterial);
                _runtimeIconMaterial = null;
            }

            if (_runtimeQuadMesh != null)
            {
                Destroy(_runtimeQuadMesh);
                _runtimeQuadMesh = null;
            }

            if (_instanceBuffer != null)
            {
                _instanceBuffer.Release();
                _instanceBuffer = null;
            }

            if (_argsBuffer != null)
            {
                _argsBuffer.Release();
                _argsBuffer = null;
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
                _blackBox.Dispose();

            _boundFontAsset = null;
            _boundSpriteAsset = null;
            _boundGlyphShader = null;
            _boundGradientScale = float.NaN;
            _boundFaceDilate = float.NaN;
            _materialBindingsDirty = true;
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
            return math.all(math.isfinite(new float3(value.x, value.y, value.z)));
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

        [StructLayout(LayoutKind.Sequential)]
        private struct TooltipGlyphInstance
        {
            public Matrix4x4 LocalToWorld;
            public Vector4 Tint;
            public Vector4 GlyphIndex;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct TooltipBlackBoxEntry
        {
            public uint Frame;
            public uint TargetHash;
            public float3 Anchor;
            public float Alpha;
            public uint SchemeHash;
            public ushort GlyphCount;
            public byte Flags;
            public byte TierFlags;
        }
    }
}
