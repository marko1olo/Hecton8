using System;
using Hecton8.Bootstrap;
using Hecton8.Core;
using Hecton8.Input;
using Hecton8.Interaction;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TextCore;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Hecton8.UI
{
    /// <summary>
    /// Renders a single zero-GC diegetic tooltip over the currently hovered interactable
    /// using instanced glyph quads instead of per-target world-space canvases.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/UI/Diegetic Tooltip System")]
    public sealed class DiegeticTooltipSystem : MonoBehaviour, ITickable, IUpdatable, IInteractionEventListener
    {
        private const int MaxGlyphCount = 96;
        private const int MaxIconCount = 1;
        private const float MinimumGlyphScale = 0.0001f;
        private const float IconScaleMultiplier = 1.06f;
        private const float IconVerticalBias = -0.002f;
        private const float IconGapMultiplier = 0.42f;
#if UNITY_EDITOR
        private const string DefaultGlyphShaderPath = "Assets/_Project/Art/Shaders/Hecton_DiegeticTooltipGlyph.shader";
#endif

        private static readonly int MainTexId = Shader.PropertyToID("_MainTex");
        private static readonly int GradientScaleId = Shader.PropertyToID("_GradientScale");
        private static readonly int FaceDilateId = Shader.PropertyToID("_FaceDilate");
        private static readonly int GlyphUvRectId = Shader.PropertyToID("_GlyphUvRect");
        private static readonly int GlyphTintId = Shader.PropertyToID("_GlyphTint");

        [Header("References")]
        [SerializeField, Tooltip("Optional explicit readable SDF font used for diegetic tooltip glyphs.")]
        private TMP_FontAsset fontAsset;
        [SerializeField, Tooltip("Optional explicit TMP sprite asset used for interact binding icons.")]
        private TMP_SpriteAsset spriteAsset;
        [SerializeField, Tooltip("Optional explicit tooltip glyph shader. Editor fallback resolves the first-party shader asset path when left null.")]
        private Shader glyphShader;
        [SerializeField, Tooltip("Optional explicit interaction camera. When null, the owner resolves the active player camera.")]
        private Camera interactionCamera;

        [Header("Tooltip Layout")]
        [SerializeField, Tooltip("World-space offset applied above the hovered interactable anchor before the tooltip faces the camera.")]
        private Vector3 worldOffset = new Vector3(0f, 0.42f, 0f);
        [SerializeField, Range(0.002f, 0.05f), Tooltip("World height of one tooltip glyph quad.")]
        private float glyphWorldHeight = 0.018f;
        [SerializeField, Range(0.8f, 1.6f), Tooltip("Horizontal advance multiplier applied to glyph metrics so prompts can breathe without extra canvases.")]
        private float glyphAdvanceScale = 1f;
        [SerializeField, Range(0.1f, 6f), Tooltip("Tooltip fade speed used when hover acquisition changes.")]
        private float fadeSpeed = 4f;
        [SerializeField, Range(0.5f, 20f), Tooltip("Maximum camera distance where a diegetic tooltip remains visible.")]
        private float maxVisibleDistance = 6f;
        [SerializeField, Tooltip("Base tooltip tint. Alpha is multiplied by the hover fade state.")]
        private Color glyphColor = new Color(0.86f, 0.98f, 1f, 0.96f);

        [Header("SDF Tuning")]
        [SerializeField, Range(1f, 24f), Tooltip("Distance-field edge sharpness multiplier forwarded into the tooltip glyph shader.")]
        private float gradientScale = 8f;
        [SerializeField, Range(-1f, 1f), Tooltip("Face dilate forwarded into the tooltip glyph shader for minor legibility tuning.")]
        private float faceDilate;

        // COLD ALLOC: Matrix4x4[96] — per-glyph tooltip instance transforms — owner: DiegeticTooltipSystem
        private readonly Matrix4x4[] _textGlyphMatrices = new Matrix4x4[MaxGlyphCount];
        // COLD ALLOC: Vector4[96] — per-glyph atlas UV rectangles — owner: DiegeticTooltipSystem
        private readonly Vector4[] _textGlyphUvRects = new Vector4[MaxGlyphCount];
        // COLD ALLOC: Vector4[96] — per-glyph tint payloads — owner: DiegeticTooltipSystem
        private readonly Vector4[] _textGlyphTints = new Vector4[MaxGlyphCount];
        // COLD ALLOC: Vector2[96] — camera-facing local glyph centers — owner: DiegeticTooltipSystem
        private readonly Vector2[] _textGlyphLocalCenters = new Vector2[MaxGlyphCount];
        // COLD ALLOC: Vector2[96] — per-glyph world-space quad scales — owner: DiegeticTooltipSystem
        private readonly Vector2[] _textGlyphLocalScales = new Vector2[MaxGlyphCount];
        // COLD ALLOC: Matrix4x4[1] — per-icon tooltip instance transforms — owner: DiegeticTooltipSystem
        private readonly Matrix4x4[] _iconMatrices = new Matrix4x4[MaxIconCount];
        // COLD ALLOC: Vector4[1] — per-icon atlas UV rectangles — owner: DiegeticTooltipSystem
        private readonly Vector4[] _iconUvRects = new Vector4[MaxIconCount];
        // COLD ALLOC: Vector4[1] — per-icon tint payloads — owner: DiegeticTooltipSystem
        private readonly Vector4[] _iconTints = new Vector4[MaxIconCount];
        // COLD ALLOC: Vector2[1] — camera-facing local icon centers — owner: DiegeticTooltipSystem
        private readonly Vector2[] _iconLocalCenters = new Vector2[MaxIconCount];
        // COLD ALLOC: Vector2[1] — per-icon world-space quad scales — owner: DiegeticTooltipSystem
        private readonly Vector2[] _iconLocalScales = new Vector2[MaxIconCount];

        private MaterialPropertyBlock _mpb; // COLD ALLOC: MaterialPropertyBlock[1] — tooltip instancing payload — owner: DiegeticTooltipSystem
        private Material _runtimeGlyphMaterial;
        private Material _runtimeIconMaterial;
        private Mesh _runtimeQuadMesh;
        private Vector3 _diagnosticWorldAnchor;
        private Color _diagnosticColor = Color.white;
        private IInteractable _activeTarget;
        private Transform _activeAnchor;
        private string _activePrompt;
        private int _textGlyphCount;
        private int _iconCount;
        private float _visibleAlpha;
        private bool _diagnosticActive;
        private bool _registeredToDispatcher;

        /// <summary>
        /// Active runtime tooltip owner used by diegetic diagnostic systems.
        /// </summary>
        public static DiegeticTooltipSystem ActiveRuntimeInstance { get; private set; }

        private void Awake()
        {
            _mpb = new MaterialPropertyBlock(); // COLD ALLOC: MaterialPropertyBlock[1] — tooltip instancing payload — owner: DiegeticTooltipSystem
        }

        private void OnEnable()
        {
            EnsureResources();
            InteractionEvents.Register(this);
            if (InputManager.Instance != null)
                InputManager.Instance.OnInputDisplayStyleChanged += HandleInputDisplayStyleChanged;
            ActiveRuntimeInstance = this;

            if (_registeredToDispatcher || !Application.isPlaying)
                return;

            if (GlobalRegistry.Dispatcher == null)
                return;

            GlobalRegistry.RegisterUpdatable(this, PriorityLayer.UI);
            _registeredToDispatcher = true;
        }

        private void OnDisable()
        {
            InteractionEvents.Unregister(this);
            if (InputManager.Instance != null)
                InputManager.Instance.OnInputDisplayStyleChanged -= HandleInputDisplayStyleChanged;
            if (ReferenceEquals(ActiveRuntimeInstance, this))
                ActiveRuntimeInstance = null;

            ClearTooltip();

            if (!_registeredToDispatcher)
                return;

            GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.UI);
            _registeredToDispatcher = false;
        }

        private void OnDestroy()
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
        }

        /// <summary>
        /// Draws the current hovered tooltip using camera-facing instanced glyph quads.
        /// </summary>
        public void Tick(float dt)
        {
            bool hasVisiblePayload = (_textGlyphCount > 0 || _iconCount > 0) &&
                                     (_diagnosticActive || _activeAnchor != null);
            float targetAlpha = hasVisiblePayload ? 1f : 0f;
            _visibleAlpha = Mathf.MoveTowards(_visibleAlpha, targetAlpha, fadeSpeed * Mathf.Max(0f, dt));
            if (_visibleAlpha <= 0.0001f || !hasVisiblePayload)
                return;

            Camera camera = ResolveCamera();
            if (camera == null || _runtimeQuadMesh == null)
                return;

            Vector3 cameraPosition = camera.transform.position;
            Vector3 anchorPosition = _diagnosticActive
                ? _diagnosticWorldAnchor
                : _activeAnchor.position + _activeAnchor.TransformVector(worldOffset);
            if ((anchorPosition - cameraPosition).sqrMagnitude > maxVisibleDistance * maxVisibleDistance)
                return;

            Color resolvedColor = _diagnosticActive ? _diagnosticColor : glyphColor;
            Vector4 tint = new Vector4(resolvedColor.r, resolvedColor.g, resolvedColor.b, resolvedColor.a * _visibleAlpha);
            if (_iconCount > 0 && _runtimeIconMaterial != null)
            {
                DrawBatch(
                    anchorPosition,
                    camera,
                    _iconLocalCenters,
                    _iconLocalScales,
                    _iconUvRects,
                    _iconMatrices,
                    _iconTints,
                    _runtimeIconMaterial,
                    _iconCount,
                    tint);
            }

            if (_textGlyphCount > 0 && _runtimeGlyphMaterial != null)
            {
                DrawBatch(
                    anchorPosition,
                    camera,
                    _textGlyphLocalCenters,
                    _textGlyphLocalScales,
                    _textGlyphUvRects,
                    _textGlyphMatrices,
                    _textGlyphTints,
                    _runtimeGlyphMaterial,
                    _textGlyphCount,
                    tint);
            }
        }

        private void HandleHoverChanged(IInteractable target)
        {
            if (ReferenceEquals(_activeTarget, target))
                return;

            _activeTarget = target;
            _activeAnchor = target as Component != null ? ((Component)target).transform : null;
            _activePrompt = target != null ? target.GetInteractText() : null;

            if (_diagnosticActive)
                return;

            if (_activeAnchor == null || string.IsNullOrEmpty(_activePrompt))
            {
                ClearTooltip();
                return;
            }

            RebuildActiveTooltipLayout();
        }

        public void OnInteractionEvent(in InteractionEventPayload payload)
        {
            if ((InteractionEventType)payload.EventType != InteractionEventType.HoverChanged)
                return;

            InteractionEvents.TryResolveTarget(in payload, out IInteractable target);
            HandleHoverChanged(target);
        }

        private void HandleInputDisplayStyleChanged(InputDisplayStyle _)
        {
            if (_diagnosticActive || _activeTarget == null || string.IsNullOrEmpty(_activePrompt))
                return;

            RebuildActiveTooltipLayout();
        }

        private void RebuildActiveTooltipLayout()
        {
            if (_activeAnchor == null || string.IsNullOrEmpty(_activePrompt))
            {
                ClearTooltip();
                return;
            }

            EnsureResources();
            BuildGlyphLayout(_activePrompt.AsSpan(), includeBindingPrefix: true);
        }

        private void ClearTooltip()
        {
            _activeTarget = null;
            _activeAnchor = null;
            _activePrompt = null;
            _diagnosticActive = false;
            _textGlyphCount = 0;
            _iconCount = 0;
        }

        /// <summary>
        /// Renders one external diagnostic tooltip at a fixed world position without going through interactable hover text.
        /// </summary>
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
            BuildGlyphLayout(text, includeBindingPrefix: false);
        }

        /// <summary>
        /// Clears the external diagnostic tooltip and restores hover-driven layout when available.
        /// </summary>
        public void ClearDiagnostic()
        {
            if (!_diagnosticActive)
                return;

            _diagnosticActive = false;
            if (_activeAnchor != null && !string.IsNullOrEmpty(_activePrompt))
            {
                RebuildActiveTooltipLayout();
                return;
            }

            _textGlyphCount = 0;
            _iconCount = 0;
        }

        private void BuildGlyphLayout(ReadOnlySpan<char> prompt, bool includeBindingPrefix)
        {
            TMP_FontAsset font = fontAsset != null ? fontAsset : TMP_Settings.defaultFontAsset;
            if (font == null)
            {
                _textGlyphCount = 0;
                _iconCount = 0;
                return;
            }

            int atlasWidth = Mathf.Max(1, font.atlasWidth);
            int atlasHeight = Mathf.Max(1, font.atlasHeight);
            float glyphScale = glyphWorldHeight / Mathf.Max(1f, font.faceInfo.pointSize);
            string bindingPrefix = includeBindingPrefix ? BuildBindingPrefix() : string.Empty;
            float prefixAdvance = MeasureAdvance(font, bindingPrefix.AsSpan(), glyphScale);
            float promptAdvance = MeasureAdvance(font, prompt, glyphScale);

            float iconWidth = 0f;
            float iconHeight = 0f;
            float iconAdvance = 0f;
            _iconCount = TryResolveBindingIcon(glyphScale, out Vector4 iconUvRect, out iconWidth, out iconHeight)
                ? 1
                : 0;
            if (_iconCount > 0)
            {
                _iconUvRects[0] = iconUvRect;
                iconAdvance = iconWidth + glyphWorldHeight * IconGapMultiplier;
            }

            float totalAdvance = iconAdvance + prefixAdvance + promptAdvance;
            float penX = -totalAdvance * 0.5f;
            if (_iconCount > 0)
            {
                _iconLocalCenters[0] = new Vector2(penX + iconWidth * 0.5f, IconVerticalBias);
                _iconLocalScales[0] = new Vector2(iconWidth, iconHeight);
                penX += iconAdvance;
            }

            float baselineOffset = font.faceInfo.ascentLine * glyphScale * 0.36f;
            _textGlyphCount = 0;
            penX = BuildTextRun(font, atlasWidth, atlasHeight, glyphScale, baselineOffset, penX, bindingPrefix.AsSpan());
            BuildTextRun(font, atlasWidth, atlasHeight, glyphScale, baselineOffset, penX, prompt);
        }

        private float BuildTextRun(
            TMP_FontAsset font,
            int atlasWidth,
            int atlasHeight,
            float glyphScale,
            float baselineOffset,
            float penX,
            ReadOnlySpan<char> text)
        {
            if (text.IsEmpty)
                return penX;

            for (int i = 0; i < text.Length && _textGlyphCount < MaxGlyphCount; i++)
            {
                char c = NormalizeTooltipCharacter(text[i]);
                if (c == '\0')
                    continue;

                if (!TryResolveCharacter(font, c, out TMP_Character character))
                    continue;

                Glyph glyph = character.glyph;
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
                _textGlyphUvRects[_textGlyphCount] = new Vector4(
                    rect.x / (float)atlasWidth,
                    rect.y / (float)atlasHeight,
                    (rect.x + rect.width) / (float)atlasWidth,
                    (rect.y + rect.height) / (float)atlasHeight);

                penX += metrics.horizontalAdvance * glyphScale * glyphAdvanceScale;
                _textGlyphCount++;
            }

            return penX;
        }

        private float MeasureAdvance(TMP_FontAsset font, ReadOnlySpan<char> text, float glyphScale)
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

                if (!TryResolveCharacter(font, c, out TMP_Character character))
                    continue;

                totalAdvance += character.glyph.metrics.horizontalAdvance * glyphScale * glyphAdvanceScale;
                visibleGlyphCount++;
            }

            return totalAdvance;
        }

        private bool TryResolveBindingIcon(float glyphScale, out Vector4 iconUvRect, out float width, out float height)
        {
            iconUvRect = default;
            width = 0f;
            height = 0f;
            if (spriteAsset == null || spriteAsset.spriteSheet == null)
                return false;

            InputManager inputManager = InputManager.Instance;
            if (inputManager == null || !inputManager.TryGetPreferredBindingPath("Interact", "Player", out string bindingPath))
                return false;

            if (!InputManager.GlyphProvider.TryGetBindingSpriteName(bindingPath, out string spriteName) ||
                !TryResolveSpriteCharacter(spriteAsset, spriteName, out TMP_SpriteCharacter spriteCharacter))
            {
                return false;
            }

            Glyph glyph = spriteCharacter.glyph;
            if (glyph == null)
                return false;

            int atlasWidth = Mathf.Max(1, spriteAsset.spriteSheet.width);
            int atlasHeight = Mathf.Max(1, spriteAsset.spriteSheet.height);
            GlyphRect rect = glyph.glyphRect;
            GlyphMetrics metrics = glyph.metrics;
            width = Mathf.Max(MinimumGlyphScale, metrics.width * glyphScale * IconScaleMultiplier);
            height = Mathf.Max(MinimumGlyphScale, metrics.height * glyphScale * IconScaleMultiplier);
            iconUvRect = new Vector4(
                rect.x / (float)atlasWidth,
                rect.y / (float)atlasHeight,
                (rect.x + rect.width) / (float)atlasWidth,
                (rect.y + rect.height) / (float)atlasHeight);
            return true;
        }

        private string BuildBindingPrefix()
        {
            InputManager inputManager = InputManager.Instance;
            if (inputManager == null)
                return string.Empty;

            string bindingDisplay = inputManager.GetBindingDisplayString("Interact", "Player", -1);
            if (string.IsNullOrWhiteSpace(bindingDisplay))
                return string.Empty;

            bindingDisplay = bindingDisplay.Trim().ToUpperInvariant();
            return string.Concat("[", bindingDisplay, "] ");
        }

        private void EnsureResources()
        {
            if (fontAsset == null)
                fontAsset = TMP_Settings.defaultFontAsset;

            if (spriteAsset == null)
                spriteAsset = TMP_Settings.defaultSpriteAsset;

            if (_runtimeQuadMesh == null)
                _runtimeQuadMesh = CreateQuadMesh();

            if (_runtimeGlyphMaterial == null || _runtimeIconMaterial == null)
            {
#if UNITY_EDITOR
                if (glyphShader == null)
                    glyphShader = AssetDatabase.LoadAssetAtPath<Shader>(DefaultGlyphShaderPath);
#endif
                if (glyphShader == null)
                    glyphShader = Shader.Find("Hecton8/UI/DiegeticTooltipGlyph");

                if (glyphShader != null)
                {
                    if (_runtimeGlyphMaterial == null)
                    {
                        _runtimeGlyphMaterial = new Material(glyphShader)
                        {
                            enableInstancing = true,
                            hideFlags = HideFlags.DontSave
                        };
                    }

                    if (_runtimeIconMaterial == null)
                    {
                        _runtimeIconMaterial = new Material(glyphShader)
                        {
                            enableInstancing = true,
                            hideFlags = HideFlags.DontSave
                        };
                    }
                }
            }

            if (_runtimeGlyphMaterial != null && fontAsset != null)
            {
                _runtimeGlyphMaterial.SetTexture(MainTexId, fontAsset.atlasTexture);
                _runtimeGlyphMaterial.SetFloat(GradientScaleId, gradientScale);
                _runtimeGlyphMaterial.SetFloat(FaceDilateId, faceDilate);
            }

            if (_runtimeIconMaterial != null && spriteAsset != null && spriteAsset.spriteSheet != null)
            {
                _runtimeIconMaterial.SetTexture(MainTexId, spriteAsset.spriteSheet);
                _runtimeIconMaterial.SetFloat(GradientScaleId, gradientScale);
                _runtimeIconMaterial.SetFloat(FaceDilateId, faceDilate);
            }
        }

        private void DrawBatch(
            Vector3 anchorPosition,
            Camera camera,
            Vector2[] localCenters,
            Vector2[] localScales,
            Vector4[] uvRects,
            Matrix4x4[] matrices,
            Vector4[] tints,
            Material material,
            int count,
            Vector4 tint)
        {
            Quaternion faceCameraRotation = Quaternion.LookRotation(camera.transform.forward, camera.transform.up);
            Vector3 cameraRight = camera.transform.right;
            Vector3 cameraUp = camera.transform.up;
            for (int i = 0; i < count; i++)
            {
                Vector2 localCenter = localCenters[i];
                Vector2 localScale = localScales[i];
                Vector3 worldPosition = anchorPosition + cameraRight * localCenter.x + cameraUp * localCenter.y;
                matrices[i] = Matrix4x4.TRS(
                    worldPosition,
                    faceCameraRotation,
                    new Vector3(localScale.x, localScale.y, 1f));
                tints[i] = tint;
            }

            _mpb.Clear();
            _mpb.SetVectorArray(GlyphUvRectId, uvRects);
            _mpb.SetVectorArray(GlyphTintId, tints);
            Graphics.DrawMeshInstanced(
                _runtimeQuadMesh,
                0,
                material,
                matrices,
                count,
                _mpb,
                ShadowCastingMode.Off,
                false,
                gameObject.layer,
                null,
                LightProbeUsage.Off,
                null);
        }

        private Camera ResolveCamera()
        {
            if (interactionCamera != null)
                return interactionCamera;

            IPlayerRuntimeContext playerContext = GlobalRegistry.Player;
            if (playerContext != null && playerContext.PlayerCamera != null)
            {
                interactionCamera = playerContext.PlayerCamera;
                return interactionCamera;
            }

            if (SceneBootstrap.TryGetCurrentPlayerTransform(out Transform playerTransform) &&
                playerTransform != null)
            {
                interactionCamera = playerTransform.GetComponentInChildren<Camera>();
            }

            return interactionCamera;
        }

        private static char NormalizeTooltipCharacter(char value)
        {
            if (value == '\n' || value == '\r' || value == '\t')
                return ' ';

            return value;
        }

        private static bool TryResolveCharacter(TMP_FontAsset font, char value, out TMP_Character character)
        {
            if (font.characterLookupTable != null &&
                font.characterLookupTable.TryGetValue(value, out character))
                return true;

            if (font.characterLookupTable != null &&
                font.characterLookupTable.TryGetValue('?', out character))
                return true;

            if (font.characterLookupTable != null &&
                font.characterLookupTable.TryGetValue(' ', out character))
                return true;

            character = null;
            return false;
        }

        private static bool TryResolveSpriteCharacter(TMP_SpriteAsset targetSpriteAsset, string spriteName, out TMP_SpriteCharacter character)
        {
            character = null;
            if (targetSpriteAsset == null || string.IsNullOrEmpty(spriteName) || targetSpriteAsset.spriteCharacterTable == null)
                return false;

            for (int i = 0; i < targetSpriteAsset.spriteCharacterTable.Count; i++)
            {
                TMP_SpriteCharacter candidate = targetSpriteAsset.spriteCharacterTable[i];
                if (candidate == null || string.IsNullOrEmpty(candidate.name))
                    continue;

                if (!string.Equals(candidate.name, spriteName, StringComparison.OrdinalIgnoreCase))
                    continue;

                character = candidate;
                return true;
            }

            return false;
        }

        private static Mesh CreateQuadMesh()
        {
            Mesh mesh = new Mesh
            {
                name = "DiegeticTooltipGlyphQuad",
                hideFlags = HideFlags.DontSave
            };

            mesh.SetVertices(new[]
            {
                new Vector3(-0.5f, -0.5f, 0f),
                new Vector3(-0.5f, 0.5f, 0f),
                new Vector3(0.5f, 0.5f, 0f),
                new Vector3(0.5f, -0.5f, 0f)
            });
            mesh.SetUVs(0, new[]
            {
                new Vector2(0f, 0f),
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(1f, 0f)
            });
            mesh.SetTriangles(new[] { 0, 1, 2, 0, 2, 3 }, 0, true);
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
