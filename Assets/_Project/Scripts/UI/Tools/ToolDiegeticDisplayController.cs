using System;
using Hecton8.Core;
using Hecton8.Core.Signals;
using Hecton8.Gameplay;
using Hecton8.Optimization;
using Hecton8.Tools;
using TMPro;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.UI.Tools
{
    /// <summary>
    /// Drives a held-tool diegetic status screen from the native tool-state signal lane.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ToolDiegeticDisplayController : MonoBehaviour, IUpdatable
    {
        private const int RenderTextureSize = 256;
        private const int TextBufferCapacity = 96;
        private const int InvalidDisplayBucket = int.MinValue;
        private const float TierHysteresisSeconds = 2f;
        private const float PoolRetrySeconds = 2f;
        private const float InvisibleReleaseSeconds = 0.75f;
        private const float PropertyEpsilon = 0.0005f;
        private const string ToolUiLayerName = "ToolUI";
        private const int StatusOk = 0;
        private const int StatusLowPower = 1;
        private const int StatusOverheated = 2;
        private const int StatusBroken = 3;
        private const int StatusDepthFailed = 4;
        private const int StatusDisabled = 5;
        private const uint ScannerToolTuningHash = 0x53434E52u; // SCNR

        private static readonly int _ToolScreenTexId = Shader.PropertyToID("_ToolScreenTex");
        private static readonly int _MainTexId = Shader.PropertyToID("_MainTex");
        private static readonly int _BaseMapId = Shader.PropertyToID("_BaseMap");
        private static readonly int _EmissionMapId = Shader.PropertyToID("_EmissionMap");
        private static readonly int _ToolHeat01Id = Shader.PropertyToID("_ToolHeat01");
        private static readonly int _ToolBattery01Id = Shader.PropertyToID("_ToolBattery01");
        private static readonly int _ToolDistanceMetersId = Shader.PropertyToID("_ToolDistanceMeters");
        private static readonly int _ToolAmmoUnitsId = Shader.PropertyToID("_ToolAmmoUnits");
        private static readonly int _ToolCriticalFlash01Id = Shader.PropertyToID("_ToolCriticalFlash01");
        private static readonly int _ToolLowTierFallback01Id = Shader.PropertyToID("_ToolLowTierFallback01");
        private static readonly int _ToolBatteryNormalizedId = Shader.PropertyToID("_ToolBatteryNormalized");
        private static readonly int _ToolVisualOverkill01Id = Shader.PropertyToID("_ToolVisualOverkill01");
        private static readonly int _ToolFault01Id = Shader.PropertyToID("_ToolFault01");
        private static readonly int _ToolTypeHue01Id = Shader.PropertyToID("_ToolTypeHue01");

        [Header("References")]
        [Tooltip("Orthographic camera that renders only the local tool UI layer into the shared 256 texture.")]
        [SerializeField] private Camera _toolCamera;
        [Tooltip("Primary TMP label on the offscreen tool UI canvas.")]
        [SerializeField] private TMP_Text _primaryLabel;
        [Tooltip("Secondary TMP label on the offscreen tool UI canvas.")]
        [SerializeField] private TMP_Text _secondaryLabel;
        [Tooltip("Renderer for the physical emissive tool screen.")]
        [SerializeField] private Renderer _screenRenderer;
        [Tooltip("Texture used on low tier when the render-texture camera is disabled.")]
        [SerializeField] private Texture _fallbackEmissiveTexture;

        [Header("Filtering")]
        [Tooltip("Optional runtime tool hash. Zero accepts the latest active tool state.")]
        [SerializeField] private uint _toolHashFilter;
        [Tooltip("Fallback culling mask when the ToolUI layer is not configured.")]
        [SerializeField] private LayerMask _fallbackToolUiMask;
        [Tooltip("Require the physical screen renderer to be visible before spending the render pass.")]
        [SerializeField] private bool _requireRendererVisibility = true;

        [Header("Readability")]
        [Tooltip("Minimum intended screen height on the model at arm length, in meters.")]
        [SerializeField, Range(0.05f, 0.18f)] private float _minimumReadableScreenHeightMeters = 0.075f;
        [Tooltip("Local camera orthographic size used by the authoring canvas.")]
        [SerializeField, Range(0.1f, 1.5f)] private float _orthographicSize = 0.5f;

        // COLD ALLOC: MaterialPropertyBlock[1] - UI surface RT binding and low-tier fallback switch - owner: ToolDiegeticDisplayController
        private readonly MaterialPropertyBlock _screenPropertyBlock = new MaterialPropertyBlock();
        // COLD ALLOC: char[96] - primary TMP SetCharArray staging buffer - owner: ToolDiegeticDisplayController
        private readonly char[] _primaryBuffer = new char[TextBufferCapacity];
        // COLD ALLOC: char[96] - secondary TMP SetCharArray staging buffer - owner: ToolDiegeticDisplayController
        private readonly char[] _secondaryBuffer = new char[TextBufferCapacity];
        // COLD ALLOC: char[96] - scanner title cache to avoid hash-registry scans per repaint - owner: ToolDiegeticDisplayController
        private readonly char[] _scannerTitleCache = new char[TextBufferCapacity];

        private RenderTexture _renderTexture;
        private RenderTexturePool _cachedRenderTexturePool;
        private RenderTexturePool _renderTextureOwnerPool;
        private Texture _boundScreenTexture;
        private bool _registered;
        private bool _hasState;
        private bool _stateDirty = true;
        private bool _renderRequested = true;
        private bool _poolUnavailableFallback;
        private float _poolRetrySeconds;
        private float _notRenderableSeconds;
        private bool _lowTierActive;
        private bool _lowTierCandidate;
        private bool _tierInitialized;
        private HectonQualityTier _currentTier = HectonQualityTier.Unknown;
        private float _lowTierCandidateSeconds;
        private int _lastSignalSequence;
        private int _lastScannerSignalSequence;
        private int _lastAmmoBucket = InvalidDisplayBucket;
        private int _lastHeatBucket = InvalidDisplayBucket;
        private int _lastBatteryBucket = InvalidDisplayBucket;
        private int _lastDistanceBucket = InvalidDisplayBucket;
        private int _lastScannerProgressBucket = InvalidDisplayBucket;
        private uint _lastScannerArtifactHash;
        private uint _scannerTitleCacheHash;
        private int _scannerTitleCacheVersion;
        private int _scannerTitleCacheLength;
        private float _heat01;
        private float _battery01;
        private float _distanceMeters;
        private float _scannerProgress01;
        private uint _scannerArtifactHash;
        private ushort _ammoUnits;
        private uint _statusMask;
        private byte _stateFlags;
        private byte _toolTypeId;
        private bool _scannerSignalActive;
        private uint _appliedStatusMask = uint.MaxValue;
        private float _appliedHeat01 = -1f;
        private float _appliedBattery01 = -1f;
        private float _appliedVisorBatteryNormalized = -1f;
        private float _appliedDistanceMeters = -1f;
        private float _appliedAmmoUnits = -1f;
        private float _appliedCriticalFlash = -1f;
        private float _appliedLowTierFallback = -1f;
        private float _appliedVisualOverkill01 = -1f;
        private float _appliedFault01 = -1f;
        private float _appliedToolTypeHue01 = -1f;
        private int _toolUiMask;

        /// <summary>
        /// Last runtime tool hash accepted by this display.
        /// </summary>
        public uint ActiveToolHash { get; private set; }

        private void Awake()
        {
            ResolveLayerMaskCold();
            ConfigureCameraCold();
        }

        private void OnEnable()
        {
            _stateDirty = true;
            _renderRequested = true;
            _notRenderableSeconds = 0f;
            ResolveTierImmediate();
            TryRegisterUpdatable();
            ApplyScreenTexture(_fallbackEmissiveTexture, lowTierFallback: true);
            ApplyCameraRenderState(renderThisFrame: false);
        }

        private void Start()
        {
            TryRegisterUpdatable();
        }

        private void OnDisable()
        {
            TryUnregisterUpdatable();
            ApplyCameraRenderState(renderThisFrame: false);
            ReleaseRenderTexture();
            _poolUnavailableFallback = false;
            _poolRetrySeconds = 0f;
            _notRenderableSeconds = 0f;
            ApplyScreenTexture(_fallbackEmissiveTexture, lowTierFallback: true);
        }

        /// <summary>
        /// Dispatcher tick. Updates only from latest tool-state signal and renders offscreen UI only when dirty.
        /// </summary>
        /// <param name="deltaTime">Scaled dispatcher delta.</param>
        public void Tick(float deltaTime)
        {
            if (!_registered)
                TryRegisterUpdatable();

            float safeDeltaTime = math.max(0f, deltaTime);
            if (_poolRetrySeconds > 0f)
                _poolRetrySeconds = math.max(0f, _poolRetrySeconds - safeDeltaTime);

            ResolveTierHysteresis(safeDeltaTime);
            ReadLatestToolStateSignal();

            if (_stateDirty)
                RefreshTextAndShaderState();

            bool shouldRender = ShouldRenderToolScreen();
            if (_lowTierActive)
            {
                _notRenderableSeconds = 0f;
                ApplyCameraRenderState(renderThisFrame: false);
                ReleaseRenderTexture();
                ApplyScreenTexture(_fallbackEmissiveTexture, lowTierFallback: true);
                return;
            }

            if (shouldRender)
            {
                _notRenderableSeconds = 0f;
                EnsureRenderTexture();
                if (_renderTexture == null)
                {
                    ApplyCameraRenderState(renderThisFrame: false);
                    ApplyScreenTexture(_fallbackEmissiveTexture, lowTierFallback: true);
                    return;
                }

                ApplyScreenTexture(_renderTexture, lowTierFallback: false);
                ApplyCameraRenderState(_renderRequested);
                return;
            }

            ApplyCameraRenderState(renderThisFrame: false);
            if (!IsEquipped() || !IsVisible())
            {
                _notRenderableSeconds = 0f;
                ReleaseRenderTexture();
                ApplyScreenTexture(_fallbackEmissiveTexture, lowTierFallback: true);
                return;
            }

            _notRenderableSeconds = math.min(InvisibleReleaseSeconds, _notRenderableSeconds + safeDeltaTime);
            if (_notRenderableSeconds >= InvisibleReleaseSeconds)
            {
                ReleaseRenderTexture();
                ApplyScreenTexture(_fallbackEmissiveTexture, lowTierFallback: true);
            }
        }

        /// <summary>
        /// Overrides the tool hash filter from an authoring or spawn system without string lookup.
        /// </summary>
        /// <param name="toolHash">Runtime tool hash. Zero resumes accepting latest active tool.</param>
        public void SetToolHashFilter(uint toolHash)
        {
            if (_toolHashFilter == toolHash)
                return;

            _toolHashFilter = toolHash;
            _hasState = false;
            _stateDirty = true;
            _renderRequested = true;
            _notRenderableSeconds = 0f;
        }

        private void ReadLatestToolStateSignal()
        {
            ReadLatestScannerSignal();

            if (!GlobalSignals.TryGetLatestToolStateChangedSignal(out ToolStateChangedSignal signal, out int sequence) ||
                sequence == _lastSignalSequence)
            {
                return;
            }

            if (_toolHashFilter != 0u && signal.ToolHash != _toolHashFilter)
            {
                _lastSignalSequence = sequence;
                return;
            }

            _lastSignalSequence = sequence;
            ActiveToolHash = signal.ToolHash;
            _heat01 = Sanitize01(signal.Heat01);
            _battery01 = Sanitize01(signal.Battery01);
            _distanceMeters = SanitizeMeters(signal.DistanceMeters);
            _ammoUnits = signal.AmmoUnits;
            _statusMask = signal.StatusMask;
            _stateFlags = signal.Flags;
            _toolTypeId = signal.ToolTypeId;
            _hasState = true;
            _stateDirty = true;
        }

        private void ReadLatestScannerSignal()
        {
            if (!GlobalSignals.TryGetLatestScannerToolActiveSignal(out ScannerToolActiveSignal signal, out int sequence) ||
                sequence == _lastScannerSignalSequence)
            {
                return;
            }

            _lastScannerSignalSequence = sequence;
            bool acceptsScanner = _toolHashFilter == 0u || _toolHashFilter == ScannerToolTuningHash || signal.ToolHash == _toolHashFilter;
            bool active = acceptsScanner && signal.Active != 0 && signal.ArtifactHash != 0u;
            if (_scannerSignalActive == active &&
                _scannerArtifactHash == signal.ArtifactHash &&
                math.abs(_scannerProgress01 - signal.Progress01) < 0.005f)
            {
                return;
            }

            _scannerSignalActive = active;
            _scannerArtifactHash = signal.ArtifactHash;
            _scannerProgress01 = math.saturate(signal.Progress01);
            _stateDirty = true;
        }

        private void RefreshTextAndShaderState()
        {
            int ammoBucket = math.clamp((int)_ammoUnits, 0, 999);
            int heatBucket = ToPercentBucket(_heat01);
            int batteryBucket = ToPercentBucket(_battery01);
            int distanceBucket = math.clamp((int)math.round(_distanceMeters), 0, 9999);
            int statusBucket = ResolveStatusBucket(_statusMask);
            int scannerProgressBucket = ToPercentBucket(_scannerProgress01);
            bool textChanged = ammoBucket != _lastAmmoBucket ||
                heatBucket != _lastHeatBucket ||
                batteryBucket != _lastBatteryBucket ||
                distanceBucket != _lastDistanceBucket ||
                statusBucket != ResolveStatusBucket(_appliedStatusMask) ||
                (_lastScannerArtifactHash != 0u && !_scannerSignalActive);

            if (_scannerSignalActive)
            {
                bool scannerTextChanged = scannerProgressBucket != _lastScannerProgressBucket ||
                    _scannerArtifactHash != _lastScannerArtifactHash ||
                    statusBucket != ResolveStatusBucket(_appliedStatusMask);
                if (scannerTextChanged)
                {
                    WriteScannerPrimaryLine(_scannerArtifactHash, scannerProgressBucket);
                    WriteScannerSecondaryLine(scannerProgressBucket, statusBucket);
                    _lastScannerProgressBucket = scannerProgressBucket;
                    _lastScannerArtifactHash = _scannerArtifactHash;
                    _appliedStatusMask = _statusMask;
                    _renderRequested = true;
                }
            }
            else if (textChanged)
            {
                WritePrimaryLine(ammoBucket, heatBucket);
                WriteSecondaryLine(distanceBucket, batteryBucket, statusBucket);
                _lastAmmoBucket = ammoBucket;
                _lastHeatBucket = heatBucket;
                _lastBatteryBucket = batteryBucket;
                _lastDistanceBucket = distanceBucket;
                _lastScannerProgressBucket = InvalidDisplayBucket;
                _lastScannerArtifactHash = 0u;
                _appliedStatusMask = _statusMask;
                _renderRequested = true;
            }

            ApplyGlobalFloat(_ToolHeat01Id, _heat01, ref _appliedHeat01);
            ApplyGlobalFloat(_ToolBattery01Id, _battery01, ref _appliedBattery01);
            ApplyGlobalFloat(_ToolBatteryNormalizedId, _battery01, ref _appliedVisorBatteryNormalized);
            ApplyGlobalFloat(_ToolDistanceMetersId, _distanceMeters, ref _appliedDistanceMeters);
            ApplyGlobalFloat(_ToolAmmoUnitsId, ammoBucket, ref _appliedAmmoUnits);
            float criticalFlash = _heat01 > 0.9f ? 1f : 0f;
            ApplyGlobalFloat(_ToolCriticalFlash01Id, criticalFlash, ref _appliedCriticalFlash);
            ApplyGlobalFloat(_ToolVisualOverkill01Id, ResolveVisualOverkill01(_currentTier), ref _appliedVisualOverkill01);
            ApplyGlobalFloat(_ToolFault01Id, ResolveFault01(_statusMask), ref _appliedFault01);
            ApplyGlobalFloat(_ToolTypeHue01Id, ResolveToolTypeHue01(_toolTypeId), ref _appliedToolTypeHue01);
            _stateDirty = false;
        }

        private void WritePrimaryLine(int ammoBucket, int heatBucket)
        {
            if (_primaryLabel == null)
                return;

            Span<char> span = _primaryBuffer.AsSpan();
            int cursor = 0;
            ZeroGCFormatter.AppendToSpan("AMMO ".AsSpan(), span, ref cursor);
            ZeroGCFormatter.FastIntToChars(ammoBucket, span, ref cursor);
            ZeroGCFormatter.AppendToSpan("  HEAT ".AsSpan(), span, ref cursor);
            ZeroGCFormatter.FastIntToChars(heatBucket, span, ref cursor);
            ZeroGCFormatter.AppendChar('%', span, ref cursor);
            _primaryLabel.SetCharArray(_primaryBuffer, 0, math.max(0, cursor));
        }

        private void WriteSecondaryLine(int distanceBucket, int batteryBucket, int statusBucket)
        {
            if (_secondaryLabel == null)
                return;

            Span<char> span = _secondaryBuffer.AsSpan();
            int cursor = 0;
            ZeroGCFormatter.AppendToSpan("DST ".AsSpan(), span, ref cursor);
            ZeroGCFormatter.FastIntToChars(distanceBucket, span, ref cursor);
            ZeroGCFormatter.AppendToSpan("M  BAT ".AsSpan(), span, ref cursor);
            ZeroGCFormatter.FastIntToChars(batteryBucket, span, ref cursor);
            ZeroGCFormatter.AppendChar('%', span, ref cursor);
            ZeroGCFormatter.AppendToSpan("  ".AsSpan(), span, ref cursor);
            AppendStatusToken(statusBucket, span, ref cursor);
            _secondaryLabel.SetCharArray(_secondaryBuffer, 0, math.max(0, cursor));
        }

        private void WriteScannerPrimaryLine(uint artifactHash, int progressPercent)
        {
            if (_primaryLabel == null)
                return;

            Span<char> span = _primaryBuffer.AsSpan();
            int cursor = 0;
            bool lowTier = IsLowTier(_currentTier);
            if (lowTier ||
                !TryResolveScannerTitle(artifactHash, span, out cursor) ||
                cursor <= 0)
            {
                cursor = 0;
                ZeroGCFormatter.AppendToSpan("SCAN ".AsSpan(), span, ref cursor);
                ZeroGCFormatter.FastIntToChars(math.clamp(progressPercent, 0, 100), span, ref cursor);
                ZeroGCFormatter.AppendChar('%', span, ref cursor);
            }
            else if (progressPercent < 100)
            {
                ScrambleDecryptionSpan(span.Slice(0, cursor), artifactHash, Time.frameCount, progressPercent * 0.01f);
            }

            _primaryLabel.SetCharArray(_primaryBuffer, 0, math.max(0, cursor));
        }

        private void WriteScannerSecondaryLine(int progressPercent, int statusBucket)
        {
            if (_secondaryLabel == null)
                return;

            Span<char> span = _secondaryBuffer.AsSpan();
            int cursor = 0;
            ZeroGCFormatter.AppendToSpan("DECRYPT ".AsSpan(), span, ref cursor);
            ZeroGCFormatter.FastIntToChars(math.clamp(progressPercent, 0, 100), span, ref cursor);
            ZeroGCFormatter.AppendChar('%', span, ref cursor);
            ZeroGCFormatter.AppendToSpan("  ".AsSpan(), span, ref cursor);
            AppendStatusToken(statusBucket, span, ref cursor);
            _secondaryLabel.SetCharArray(_secondaryBuffer, 0, math.max(0, cursor));
        }

        private bool TryResolveScannerTitle(uint artifactHash, Span<char> destination, out int written)
        {
            written = 0;
            if (artifactHash == 0u || destination.Length <= 0)
                return false;

            int titleVersion = ScannableTarget.LoreTitleLookupVersion;
            if (_scannerTitleCacheHash != artifactHash ||
                _scannerTitleCacheVersion != titleVersion ||
                _scannerTitleCacheLength <= 0)
            {
                _scannerTitleCacheHash = 0u;
                _scannerTitleCacheVersion = titleVersion;
                _scannerTitleCacheLength = 0;
                if (!ScannableTarget.TryWriteLoreEntityTitle(
                        artifactHash,
                        _scannerTitleCache.AsSpan(),
                        out int cachedLength) ||
                    cachedLength <= 0)
                {
                    return false;
                }

                _scannerTitleCacheHash = artifactHash;
                _scannerTitleCacheVersion = titleVersion;
                _scannerTitleCacheLength = math.min(cachedLength, _scannerTitleCache.Length);
            }

            int length = math.min(_scannerTitleCacheLength, destination.Length);
            if (length <= 0)
                return false;

            _scannerTitleCache.AsSpan(0, length).CopyTo(destination);
            written = length;
            return true;
        }

        private static void ScrambleDecryptionSpan(Span<char> span, uint hash, int frame, float progress01)
        {
            int revealed = math.clamp((int)math.floor(math.saturate(progress01) * span.Length), 0, span.Length);
            uint seed = hash ^ unchecked((uint)frame * 747796405u) ^ 0xB5297A4Du;
            for (int i = revealed; i < span.Length; i++)
            {
                char source = span[i];
                if (source == ' ' || source == '-' || source == '_' || source == '/')
                    continue;

                seed = seed * 1664525u + 1013904223u;
                span[i] = (char)('A' + (seed % 26u));
            }
        }

        private void EnsureRenderTexture()
        {
            if (_renderTexture != null)
                return;

            if (_poolUnavailableFallback && _poolRetrySeconds > 0f)
                return;

            RenderTexturePool pool = ResolveRenderTexturePool();
            if (pool == null)
            {
                _poolUnavailableFallback = true;
                _poolRetrySeconds = PoolRetrySeconds;
                _renderRequested = false;
                return;
            }

            RenderTextureFormat format = SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.RGB565)
                ? RenderTextureFormat.RGB565
                : RenderTextureFormat.ARGB32;
            _renderTexture = pool.Rent(RenderTextureSize, RenderTextureSize, format, this, 16);
            if (_renderTexture == null)
            {
                _poolUnavailableFallback = true;
                _poolRetrySeconds = PoolRetrySeconds;
                _renderRequested = false;
                return;
            }

            _poolUnavailableFallback = false;
            _poolRetrySeconds = 0f;
            _renderTextureOwnerPool = pool;
            _renderTexture.name = "ToolScreen_RT_256";
            _renderTexture.filterMode = FilterMode.Bilinear;
            _renderTexture.wrapMode = TextureWrapMode.Clamp;
            _renderTexture.useMipMap = false;
            _renderTexture.autoGenerateMips = false;
            _renderTexture.antiAliasing = 1;
            if (!_renderTexture.IsCreated())
                _renderTexture.Create();

            if (_toolCamera != null && !ReferenceEquals(_toolCamera.targetTexture, _renderTexture))
                _toolCamera.targetTexture = _renderTexture;

            _renderRequested = true;
        }

        private void ReleaseRenderTexture()
        {
            if (_renderTexture == null)
                return;

            if (_toolCamera != null && ReferenceEquals(_toolCamera.targetTexture, _renderTexture))
                _toolCamera.targetTexture = null;

            if (ReferenceEquals(_boundScreenTexture, _renderTexture))
                _boundScreenTexture = null;

            RenderTexture released = _renderTexture;
            _renderTexture = null;
            RenderTexturePool ownerPool = _renderTextureOwnerPool;
            _renderTextureOwnerPool = null;

            if (ownerPool != null)
                ownerPool.Return(released);
            else
                DestroyUnownedRenderTexture(released);
        }

        private void DestroyUnownedRenderTexture(RenderTexture rt)
        {
            if (rt == null)
                return;

            rt.Release();
            Destroy(rt);
        }

        private RenderTexturePool ResolveRenderTexturePool()
        {
            if (_cachedRenderTexturePool != null)
                return _cachedRenderTexturePool;

            _cachedRenderTexturePool = GlobalRegistry.RenderTexturePool;
            return _cachedRenderTexturePool;
        }

        private void ApplyScreenTexture(Texture texture, bool lowTierFallback)
        {
            if (_screenRenderer == null || ReferenceEquals(texture, _boundScreenTexture) && NearlyEqual(_appliedLowTierFallback, lowTierFallback ? 1f : 0f))
                return;

            _screenRenderer.GetPropertyBlock(_screenPropertyBlock);
            _screenPropertyBlock.SetTexture(_ToolScreenTexId, texture);
            _screenPropertyBlock.SetTexture(_MainTexId, texture);
            _screenPropertyBlock.SetTexture(_BaseMapId, texture);
            _screenPropertyBlock.SetTexture(_EmissionMapId, texture);
            _screenPropertyBlock.SetFloat(_ToolLowTierFallback01Id, lowTierFallback ? 1f : 0f);
            _screenRenderer.SetPropertyBlock(_screenPropertyBlock);
            _boundScreenTexture = texture;
            _appliedLowTierFallback = lowTierFallback ? 1f : 0f;
        }

        private void ApplyCameraRenderState(bool renderThisFrame)
        {
            if (_toolCamera == null)
                return;

            bool enableCamera = renderThisFrame && _renderTexture != null && !_lowTierActive && !_poolUnavailableFallback;
            if (enableCamera && !ReferenceEquals(_toolCamera.targetTexture, _renderTexture))
                _toolCamera.targetTexture = _renderTexture;

            if (_toolCamera.enabled != enableCamera)
                _toolCamera.enabled = enableCamera;

            if (enableCamera)
                _renderRequested = false;
        }

        private bool ShouldRenderToolScreen()
        {
            if (!_hasState || !IsEquipped() || !IsVisible())
                return false;

            if (!_requireRendererVisibility || _screenRenderer == null)
                return true;

            return _screenRenderer.isVisible;
        }

        private bool IsEquipped()
        {
            return (_stateFlags & ToolStateChangedSignal.FlagEquipped) != 0;
        }

        private bool IsVisible()
        {
            return (_stateFlags & ToolStateChangedSignal.FlagVisible) != 0;
        }

        private void ResolveLayerMaskCold()
        {
            int layer = LayerMask.NameToLayer(ToolUiLayerName);
            _toolUiMask = layer >= 0 ? 1 << layer : _fallbackToolUiMask.value;
        }

        private void ConfigureCameraCold()
        {
            if (_toolCamera == null)
                return;

            _toolCamera.enabled = false;
            _toolCamera.orthographic = true;
            float minimumReadableHalfHeight = _minimumReadableScreenHeightMeters * 0.5f;
            _toolCamera.orthographicSize = math.max(0.1f, math.max(_orthographicSize, minimumReadableHalfHeight));
            _toolCamera.clearFlags = CameraClearFlags.SolidColor;
            _toolCamera.backgroundColor = Color.black;
            _toolCamera.allowHDR = false;
            _toolCamera.allowMSAA = false;
            _toolCamera.depth = -100f;
            _toolCamera.cullingMask = _toolUiMask;
        }

        private void ResolveTierImmediate()
        {
            _currentTier = GlobalRegistry.ScalabilityTier;
            bool lowTier = IsLowTier(_currentTier);
            _lowTierActive = lowTier;
            _lowTierCandidate = lowTier;
            _lowTierCandidateSeconds = 0f;
            _tierInitialized = true;
        }

        private void ResolveTierHysteresis(float deltaTime)
        {
            HectonQualityTier tier = GlobalRegistry.ScalabilityTier;
            if (tier != _currentTier)
            {
                _currentTier = tier;
                _stateDirty = true;
                _renderRequested = true;
            }

            bool requestedLowTier = IsLowTier(tier) ||
                (_stateFlags & ToolStateChangedSignal.FlagLowTierFallback) != 0;

            if (!_tierInitialized)
            {
                _lowTierActive = requestedLowTier;
                _lowTierCandidate = requestedLowTier;
                _tierInitialized = true;
                _stateDirty = true;
                return;
            }

            if (requestedLowTier == _lowTierActive)
            {
                _lowTierCandidate = requestedLowTier;
                _lowTierCandidateSeconds = 0f;
                return;
            }

            if (requestedLowTier != _lowTierCandidate)
            {
                _lowTierCandidate = requestedLowTier;
                _lowTierCandidateSeconds = 0f;
                return;
            }

            _lowTierCandidateSeconds += deltaTime;
            if (_lowTierCandidateSeconds < TierHysteresisSeconds)
                return;

            _lowTierActive = requestedLowTier;
            _lowTierCandidateSeconds = 0f;
            _poolUnavailableFallback = false;
            _stateDirty = true;
            _renderRequested = true;
        }

        private void TryRegisterUpdatable()
        {
            if (_registered || !Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            _registered = GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.UI);
        }

        private void TryUnregisterUpdatable()
        {
            if (!_registered)
                return;

            GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.UI);
            _registered = false;
        }

        private static bool IsLowTier(HectonQualityTier tier)
        {
            return tier == HectonQualityTier.Unknown ||
                tier == HectonQualityTier.Low ||
                tier == HectonQualityTier.Mx350;
        }

        private static float ResolveVisualOverkill01(HectonQualityTier tier)
        {
            switch (tier)
            {
                case HectonQualityTier.Ultra:
                    return 1f;
                case HectonQualityTier.High:
                    return 0.66f;
                case HectonQualityTier.Mid:
                    return 0.33f;
                default:
                    return 0f;
            }
        }

        private static int ResolveStatusBucket(uint statusMask)
        {
            if ((statusMask & ToolRuntimeStatusMasks.Broken) != 0u)
                return StatusBroken;
            if ((statusMask & ToolRuntimeStatusMasks.DepthFailed) != 0u)
                return StatusDepthFailed;
            if ((statusMask & ToolRuntimeStatusMasks.Overheated) != 0u)
                return StatusOverheated;
            if ((statusMask & ToolRuntimeStatusMasks.LowPower) != 0u)
                return StatusLowPower;
            if ((statusMask & ToolRuntimeStatusMasks.Disabled) != 0u)
                return StatusDisabled;

            return StatusOk;
        }

        private static void AppendStatusToken(int statusBucket, Span<char> span, ref int cursor)
        {
            switch (statusBucket)
            {
                case StatusLowPower:
                    ZeroGCFormatter.AppendToSpan("PWR".AsSpan(), span, ref cursor);
                    break;
                case StatusOverheated:
                    ZeroGCFormatter.AppendToSpan("HOT".AsSpan(), span, ref cursor);
                    break;
                case StatusBroken:
                    ZeroGCFormatter.AppendToSpan("BRK".AsSpan(), span, ref cursor);
                    break;
                case StatusDepthFailed:
                    ZeroGCFormatter.AppendToSpan("DPT".AsSpan(), span, ref cursor);
                    break;
                case StatusDisabled:
                    ZeroGCFormatter.AppendToSpan("OFF".AsSpan(), span, ref cursor);
                    break;
                default:
                    ZeroGCFormatter.AppendToSpan("OK".AsSpan(), span, ref cursor);
                    break;
            }
        }

        private static float ResolveFault01(uint statusMask)
        {
            if ((statusMask & (ToolRuntimeStatusMasks.Broken | ToolRuntimeStatusMasks.DepthFailed)) != 0u)
                return 1f;
            if ((statusMask & ToolRuntimeStatusMasks.Overheated) != 0u)
                return 0.85f;
            if ((statusMask & ToolRuntimeStatusMasks.LowPower) != 0u)
                return 0.45f;
            if ((statusMask & ToolRuntimeStatusMasks.Disabled) != 0u)
                return 0.25f;

            return 0f;
        }

        private static float ResolveToolTypeHue01(byte toolTypeId)
        {
            return math.frac(toolTypeId * 0.173f);
        }

        private static float Sanitize01(float value)
        {
            return math.isfinite(value) ? math.saturate(value) : 0f;
        }

        private static float SanitizeMeters(float value)
        {
            return math.isfinite(value) ? math.max(0f, value) : 0f;
        }

        private static int ToPercentBucket(float value)
        {
            return math.clamp((int)math.round(Sanitize01(value) * 100f), 0, 100);
        }

        private static bool NearlyEqual(float lhs, float rhs)
        {
            return math.abs(lhs - rhs) <= PropertyEpsilon;
        }

        private static void ApplyGlobalFloat(int propertyId, float value, ref float cachedValue)
        {
            if (NearlyEqual(cachedValue, value))
                return;

            Shader.SetGlobalFloat(propertyId, value);
            cachedValue = value;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            _orthographicSize = math.max(0.1f, _orthographicSize);
            _minimumReadableScreenHeightMeters = math.max(0.05f, _minimumReadableScreenHeightMeters);
            ResolveLayerMaskCold();
            ConfigureCameraCold();
        }
#endif
    }
}
