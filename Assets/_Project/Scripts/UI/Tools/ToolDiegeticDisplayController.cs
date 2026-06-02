using System;
using Hecton8.Core;
using Hecton8.Core.Contracts;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Gameplay;
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
    public sealed class ToolDiegeticDisplayController : MonoBehaviour, ISlowTickable, ILateFrameTickable, IGlobalRegistryHotSwapListener
    {
        private const int RenderTextureSize = 256;
        private const int TextBufferCapacity = 96;
        private const int InvalidDisplayBucket = int.MinValue;
        private const int ScannerTitleCacheMiss = -1;
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
        private static readonly int _ToolFallback01Id = Shader.PropertyToID("_ToolFallback01");
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
        [Tooltip("Texture used when the render-texture route is unavailable or the tool screen is not renderable.")]
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

        // COLD ALLOC: MaterialPropertyBlock[1] - UI surface RT binding and fallback switch - owner: ToolDiegeticDisplayController
        private readonly MaterialPropertyBlock _screenPropertyBlock = new MaterialPropertyBlock();
        // COLD ALLOC: char[96] - primary TMP SetCharArray staging buffer - owner: ToolDiegeticDisplayController
        private readonly char[] _primaryBuffer = new char[TextBufferCapacity];
        // COLD ALLOC: char[96] - secondary TMP SetCharArray staging buffer - owner: ToolDiegeticDisplayController
        private readonly char[] _secondaryBuffer = new char[TextBufferCapacity];
        // COLD ALLOC: char[96] - scanner title cache to avoid hash-registry scans per repaint - owner: ToolDiegeticDisplayController
        private readonly char[] _scannerTitleCache = new char[TextBufferCapacity];

        private RenderTexture _renderTexture;
        private IRenderTexturePoolService _cachedRenderTexturePool;
        private IRenderTexturePoolService _renderTextureOwnerPool;
        private Texture _boundScreenTexture;
        private RenderTextureFormat _renderTextureFormat = RenderTextureFormat.ARGB32;
        private bool _hasState;
        private bool _stateDirty = true;
        private bool _renderRequested = true;
        private bool _poolUnavailableFallback;
        private float _poolRetrySeconds;
        private float _notRenderableSeconds;
        private bool _fallbackActive;
        private bool _fallbackCandidate;
        private bool _qualityInitialized;
        private float _fallbackCandidateSeconds;
        private float _qualityWeight01 = 1f;
        private float _visualOverkill01 = 1f;
        private float _qualityFallback01;
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
        private uint _scannerFrame;
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
        private float _appliedFallback01 = -1f;
        private float _appliedVisualOverkill01 = -1f;
        private float _appliedFault01 = -1f;
        private float _appliedToolTypeHue01 = -1f;
        private int _toolUiMask;
        private bool _registeredSlowTick;
        private bool _registeredLateFrame;
        private bool _registeredHotSwapListener;
        private bool _pendingPresentationCommit;
        private bool _pendingEnsureRenderTexture;
        private bool _pendingReleaseRenderTexture;
        private bool _pendingApplyScreenTexture;
        private bool _pendingUseRenderTexture;
        private bool _pendingFallbackActive;
        private bool _pendingRenderThisFrame;
        private bool _pendingPresentationDecision;
        private float _pendingPresentationDecisionDeltaTime;
        private bool _pendingStateRefresh;

        /// <summary>
        /// Last runtime tool hash accepted by this display.
        /// </summary>
        public uint ActiveToolHash { get; private set; }

        private void Awake()
        {
            ResolveLayerMaskCold();
            ResolveRenderTextureFormatCold();
            ConfigureCameraCold();
        }

        private void OnEnable()
        {
            _stateDirty = true;
            _renderRequested = true;
            _notRenderableSeconds = 0f;
            ResolveQualityImmediate();
            CacheRenderTexturePoolCold();
            TryRegisterHotSwapListener();
            TryRegisterSlowTickable();
            TryRegisterLateFrameTickable();
            ApplyScreenTexture(_fallbackEmissiveTexture, fallbackActive: true);
            ApplyCameraRenderState(renderThisFrame: false);
        }

        private void Start()
        {
            CacheRenderTexturePoolCold();
            TryRegisterHotSwapListener();
            TryRegisterSlowTickable();
            TryRegisterLateFrameTickable();
        }

        private void OnDisable()
        {
            TryUnregisterSlowTickable();
            TryUnregisterLateFrameTickable();
            TryUnregisterHotSwapListener();
            ApplyCameraRenderState(renderThisFrame: false);
            ReleaseRenderTexture();
            _cachedRenderTexturePool = null;
            _poolUnavailableFallback = false;
            _poolRetrySeconds = 0f;
            _notRenderableSeconds = 0f;
            ApplyScreenTexture(_fallbackEmissiveTexture, fallbackActive: true);
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot == GlobalRegistryServiceSlot.Dispatcher)
            {
                _registeredSlowTick = false;
                _registeredLateFrame = false;
                if (currentService != null && isActiveAndEnabled)
                {
                    TryRegisterSlowTickable();
                    TryRegisterLateFrameTickable();
                }
                return;
            }

            if (serviceSlot != GlobalRegistryServiceSlot.RenderTexturePoolRuntime)
                return;

            IRenderTexturePoolService newPool = currentService as IRenderTexturePoolService;
            if (_renderTexture != null &&
                _renderTextureOwnerPool != null &&
                !ReferenceEquals(_renderTextureOwnerPool, newPool))
            {
                ReleaseRenderTexture();
                ApplyScreenTexture(_fallbackEmissiveTexture, fallbackActive: true);
            }

            _cachedRenderTexturePool = newPool;
            _poolUnavailableFallback = false;
            _poolRetrySeconds = 0f;
            _stateDirty = true;
            _renderRequested = true;
        }

        /// <summary>
        /// Dispatcher tick. Updates only from latest tool-state signal and renders offscreen UI only when dirty.
        /// </summary>
        /// <param name="deltaTime">Scaled dispatcher delta.</param>
        private void AdvanceToolDisplayPresentationState(float deltaTime)
        {
            float safeDeltaTime = SanitizeSeconds(deltaTime);

            if (_poolRetrySeconds > 0f)
                _poolRetrySeconds = math.max(0f, _poolRetrySeconds - safeDeltaTime);

            ResolveQualityHysteresis(safeDeltaTime);
            ReadLatestToolStateSignal();

            if (_stateDirty)
            {
                _pendingStateRefresh = true;
                _renderRequested = true;
            }

            QueuePresentationDecision(safeDeltaTime);
        }

        private void QueuePresentationDecision(float safeDeltaTime)
        {
            _pendingPresentationDecisionDeltaTime = safeDeltaTime;
            _pendingPresentationDecision = true;
        }

        private void ResolvePresentationDecision()
        {
            _pendingPresentationDecision = false;
            float safeDeltaTime = _pendingPresentationDecisionDeltaTime;
            bool shouldRender = ShouldRenderToolScreen();
            if (_fallbackActive)
            {
                _notRenderableSeconds = 0f;
                QueuePresentationCommit(
                    ensureRenderTexture: false,
                    releaseRenderTexture: true,
                    applyScreenTexture: true,
                    useRenderTexture: false,
                    fallbackActive: true,
                    renderThisFrame: false);
                return;
            }

            if (shouldRender)
            {
                _notRenderableSeconds = 0f;
                QueuePresentationCommit(
                    ensureRenderTexture: true,
                    releaseRenderTexture: false,
                    applyScreenTexture: true,
                    useRenderTexture: true,
                    fallbackActive: false,
                    renderThisFrame: _renderRequested);
                return;
            }

            QueuePresentationCommit(
                ensureRenderTexture: false,
                releaseRenderTexture: false,
                applyScreenTexture: false,
                useRenderTexture: false,
                fallbackActive: false,
                renderThisFrame: false);
            if (!IsEquipped() || !IsVisible())
            {
                _notRenderableSeconds = 0f;
                QueuePresentationCommit(
                    ensureRenderTexture: false,
                    releaseRenderTexture: true,
                    applyScreenTexture: true,
                    useRenderTexture: false,
                    fallbackActive: true,
                    renderThisFrame: false);
                return;
            }

            _notRenderableSeconds = math.min(InvisibleReleaseSeconds, _notRenderableSeconds + safeDeltaTime);
            if (_notRenderableSeconds >= InvisibleReleaseSeconds)
            {
                QueuePresentationCommit(
                    ensureRenderTexture: false,
                    releaseRenderTexture: true,
                    applyScreenTexture: true,
                    useRenderTexture: false,
                    fallbackActive: true,
                    renderThisFrame: false);
            }
        }

        public void SlowTick()
        {
            QueueQualityCandidate(HomeostasisBrain.GlobalQualityWeight);
            FlushPendingRenderTextureResourceState();
        }

        public void LateFrameTick()
        {
            AdvanceToolDisplayPresentationState(SystemDispatcher.CurrentFrameDeltaTime);

            if (_pendingPresentationDecision)
                ResolvePresentationDecision();

            if (!_pendingPresentationCommit && !_pendingStateRefresh && !_stateDirty)
                return;

            bool hasPresentationCommit = _pendingPresentationCommit;
            bool releaseRenderTexture = _pendingReleaseRenderTexture;
            if (_pendingStateRefresh || _stateDirty)
            {
                _pendingStateRefresh = false;
                RefreshTextAndShaderState();
            }

            if (!hasPresentationCommit)
                return;

            bool applyScreenTexture = _pendingApplyScreenTexture;
            bool useRenderTexture = _pendingUseRenderTexture && !releaseRenderTexture;
            bool fallbackActive = _pendingFallbackActive;
            bool renderThisFrame = _pendingRenderThisFrame && !releaseRenderTexture;

            _pendingPresentationCommit = false;
            _pendingApplyScreenTexture = false;
            _pendingUseRenderTexture = false;
            _pendingFallbackActive = false;
            _pendingRenderThisFrame = false;

            if (releaseRenderTexture || _renderTexture == null)
            {
                useRenderTexture = false;
                fallbackActive = true;
                renderThisFrame = false;
            }

            bool hasRenderTexture = useRenderTexture && _renderTexture != null;
            if (applyScreenTexture)
                ApplyScreenTexture(hasRenderTexture ? _renderTexture : _fallbackEmissiveTexture, fallbackActive || !hasRenderTexture);

            ApplyCameraRenderState(renderThisFrame && hasRenderTexture);
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
            _scannerSignalActive = false;
            _scannerArtifactHash = 0u;
            _scannerFrame = 0u;
            _scannerProgress01 = 0f;
            _lastSignalSequence = InvalidDisplayBucket;
            _lastScannerSignalSequence = InvalidDisplayBucket;
            _lastScannerProgressBucket = InvalidDisplayBucket;
            _lastScannerArtifactHash = 0u;
            _stateDirty = true;
            _renderRequested = true;
            _notRenderableSeconds = 0f;
        }

        private void ReadLatestToolStateSignal()
        {
            ReadLatestScannerSignal();

            if (!SignalBus<ToolStateChangedSignal>.TryGetLatest(out ToolStateChangedSignal signal, out int sequence) ||
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
            if (!ScannerSignalRoute.TryGetLatestActive(out ScannerToolActiveSignal signal, out int sequence) ||
                sequence == _lastScannerSignalSequence)
            {
                return;
            }

            _lastScannerSignalSequence = sequence;
            bool acceptsScanner = _toolHashFilter == 0u || _toolHashFilter == ScannerToolTuningHash || signal.ToolHash == _toolHashFilter;
            uint artifactHash = acceptsScanner ? signal.ArtifactHash : 0u;
            bool active = acceptsScanner && signal.Active != 0 && artifactHash != 0u;
            float scannerProgress01 = active ? Sanitize01(signal.Progress01) : 0f;
            uint scannerFrame = active ? signal.Frame : 0u;
            if (_scannerSignalActive == active &&
                _scannerArtifactHash == artifactHash &&
                math.abs(_scannerProgress01 - scannerProgress01) < 0.005f)
            {
                _scannerFrame = scannerFrame;
                return;
            }

            _scannerSignalActive = active;
            _scannerArtifactHash = artifactHash;
            _scannerFrame = scannerFrame;
            _scannerProgress01 = scannerProgress01;
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
                    WriteScannerPrimaryLine(_scannerArtifactHash, _scannerFrame, scannerProgressBucket);
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

            float criticalFlash = _heat01 > 0.9f ? 1f : 0f;
            ApplyScreenScalarState(
                _heat01,
                _battery01,
                _distanceMeters,
                ammoBucket,
                criticalFlash,
                _visualOverkill01,
                ResolveFault01(_statusMask),
                ResolveToolTypeHue01(_toolTypeId));
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

        private void WriteScannerPrimaryLine(uint artifactHash, uint scannerFrame, int progressPercent)
        {
            if (_primaryLabel == null)
                return;

            Span<char> span = _primaryBuffer.AsSpan();
            int cursor = 0;
            bool compactTitle = _fallbackActive;
            if (compactTitle ||
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
                ScrambleDecryptionSpan(span.Slice(0, cursor), artifactHash, unchecked((int)scannerFrame), progressPercent * 0.01f);
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
                _scannerTitleCacheVersion != titleVersion)
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
                    _scannerTitleCacheHash = artifactHash;
                    _scannerTitleCacheVersion = titleVersion;
                    _scannerTitleCacheLength = ScannerTitleCacheMiss;
                    return false;
                }

                _scannerTitleCacheHash = artifactHash;
                _scannerTitleCacheVersion = titleVersion;
                _scannerTitleCacheLength = math.min(cachedLength, _scannerTitleCache.Length);
            }

            if (_scannerTitleCacheLength == ScannerTitleCacheMiss)
                return false;

            int length = math.min(_scannerTitleCacheLength, destination.Length);
            if (length <= 0)
                return false;

            _scannerTitleCache.AsSpan(0, length).CopyTo(destination);
            written = length;
            return true;
        }

        private static void ScrambleDecryptionSpan(Span<char> span, uint hash, int frame, float progress01)
        {
            int revealed = math.clamp((int)math.floor(Sanitize01(progress01) * span.Length), 0, span.Length);
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

            IRenderTexturePoolService pool = _cachedRenderTexturePool;
            if (pool == null)
            {
                _poolUnavailableFallback = true;
                _poolRetrySeconds = PoolRetrySeconds;
                _renderRequested = false;
                return;
            }

            _renderTexture = pool.Rent(RenderTextureSize, RenderTextureSize, _renderTextureFormat, this, 16);
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
            IRenderTexturePoolService ownerPool = _renderTextureOwnerPool;
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

        private IRenderTexturePoolService CacheRenderTexturePoolCold()
        {
            if (_cachedRenderTexturePool != null)
                return _cachedRenderTexturePool;

            _cachedRenderTexturePool = GlobalRegistry.RenderTexturePoolService;
            return _cachedRenderTexturePool;
        }

        private void ApplyScreenTexture(Texture texture, bool fallbackActive)
        {
            float fallback01 = fallbackActive ? 1f : 0f;
            if (_screenRenderer == null || ReferenceEquals(texture, _boundScreenTexture) && NearlyEqual(_appliedFallback01, fallback01))
                return;

            _screenRenderer.GetPropertyBlock(_screenPropertyBlock);
            _screenPropertyBlock.SetTexture(_ToolScreenTexId, texture);
            _screenPropertyBlock.SetTexture(_MainTexId, texture);
            _screenPropertyBlock.SetTexture(_BaseMapId, texture);
            _screenPropertyBlock.SetTexture(_EmissionMapId, texture);
            _screenPropertyBlock.SetFloat(_ToolFallback01Id, fallback01);
            _screenRenderer.SetPropertyBlock(_screenPropertyBlock);
            _boundScreenTexture = texture;
            _appliedFallback01 = fallback01;
        }

        private void ApplyScreenScalarState(
            float heat01,
            float battery01,
            float distanceMeters,
            float ammoUnits,
            float criticalFlash,
            float visualOverkill01,
            float fault01,
            float toolTypeHue01)
        {
            bool changed = !NearlyEqual(_appliedHeat01, heat01) ||
                !NearlyEqual(_appliedBattery01, battery01) ||
                !NearlyEqual(_appliedVisorBatteryNormalized, battery01) ||
                !NearlyEqual(_appliedDistanceMeters, distanceMeters) ||
                !NearlyEqual(_appliedAmmoUnits, ammoUnits) ||
                !NearlyEqual(_appliedCriticalFlash, criticalFlash) ||
                !NearlyEqual(_appliedVisualOverkill01, visualOverkill01) ||
                !NearlyEqual(_appliedFault01, fault01) ||
                !NearlyEqual(_appliedToolTypeHue01, toolTypeHue01);
            if (!changed || _screenRenderer == null)
                return;

            _screenRenderer.GetPropertyBlock(_screenPropertyBlock);
            SetScreenFloat(_ToolHeat01Id, heat01, ref _appliedHeat01);
            SetScreenFloat(_ToolBattery01Id, battery01, ref _appliedBattery01);
            SetScreenFloat(_ToolBatteryNormalizedId, battery01, ref _appliedVisorBatteryNormalized);
            SetScreenFloat(_ToolDistanceMetersId, distanceMeters, ref _appliedDistanceMeters);
            SetScreenFloat(_ToolAmmoUnitsId, ammoUnits, ref _appliedAmmoUnits);
            SetScreenFloat(_ToolCriticalFlash01Id, criticalFlash, ref _appliedCriticalFlash);
            SetScreenFloat(_ToolVisualOverkill01Id, visualOverkill01, ref _appliedVisualOverkill01);
            SetScreenFloat(_ToolFault01Id, fault01, ref _appliedFault01);
            SetScreenFloat(_ToolTypeHue01Id, toolTypeHue01, ref _appliedToolTypeHue01);
            _screenRenderer.SetPropertyBlock(_screenPropertyBlock);
        }

        private void SetScreenFloat(int propertyId, float value, ref float cachedValue)
        {
            if (NearlyEqual(cachedValue, value))
                return;

            _screenPropertyBlock.SetFloat(propertyId, value);
            cachedValue = value;
        }

        private void ApplyCameraRenderState(bool renderThisFrame)
        {
            if (_toolCamera == null)
                return;

            bool enableCamera = renderThisFrame && _renderTexture != null && !_fallbackActive && !_poolUnavailableFallback;
            if (enableCamera && !ReferenceEquals(_toolCamera.targetTexture, _renderTexture))
                _toolCamera.targetTexture = _renderTexture;

            if (_toolCamera.enabled != enableCamera)
                _toolCamera.enabled = enableCamera;

            if (enableCamera)
                _renderRequested = false;
        }

        private void QueuePresentationCommit(
            bool ensureRenderTexture,
            bool releaseRenderTexture,
            bool applyScreenTexture,
            bool useRenderTexture,
            bool fallbackActive,
            bool renderThisFrame)
        {
            _pendingPresentationCommit = true;
            if (releaseRenderTexture)
            {
                _pendingReleaseRenderTexture = true;
                _pendingEnsureRenderTexture = false;
            }
            else if (ensureRenderTexture)
            {
                _pendingEnsureRenderTexture = true;
            }

            _pendingApplyScreenTexture |= applyScreenTexture;
            _pendingUseRenderTexture = useRenderTexture;
            _pendingFallbackActive = fallbackActive;
            _pendingRenderThisFrame = renderThisFrame;
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

        private void ResolveRenderTextureFormatCold()
        {
            _renderTextureFormat = SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.RGB565)
                ? RenderTextureFormat.RGB565
                : RenderTextureFormat.ARGB32;
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

        private void ResolveQualityImmediate()
        {
            InitializeQuality(HomeostasisBrain.GlobalQualityWeight);
        }

        private void InitializeQuality(float qualityWeight01)
        {
            ApplyQualityPolicy(qualityWeight01);
            bool requestedFallback = ResolveRequestedFallback();
            _fallbackActive = requestedFallback;
            _fallbackCandidate = requestedFallback;
            _fallbackCandidateSeconds = 0f;
            _qualityInitialized = true;
        }

        private void ResolveQualityHysteresis(float deltaTime)
        {
            if (!_qualityInitialized)
            {
                InitializeQuality(HomeostasisBrain.GlobalQualityWeight);
                return;
            }

            ApplyQualityPolicy(HomeostasisBrain.GlobalQualityWeight);
            bool requestedFallback = ResolveRequestedFallback();
            if (requestedFallback == _fallbackActive)
            {
                _fallbackCandidate = requestedFallback;
                _fallbackCandidateSeconds = 0f;
                return;
            }

            if (requestedFallback != _fallbackCandidate)
            {
                _fallbackCandidate = requestedFallback;
                _fallbackCandidateSeconds = 0f;
                return;
            }

            _fallbackCandidateSeconds += deltaTime;
            if (_fallbackCandidateSeconds < TierHysteresisSeconds)
                return;

            _fallbackActive = requestedFallback;
            _fallbackCandidateSeconds = 0f;
            _poolUnavailableFallback = false;
            _stateDirty = true;
            _renderRequested = true;
        }

        private void FlushPendingRenderTextureResourceState()
        {
            bool releaseRenderTexture = _pendingReleaseRenderTexture;
            bool ensureRenderTexture = _pendingEnsureRenderTexture && !releaseRenderTexture;

            _pendingReleaseRenderTexture = false;
            _pendingEnsureRenderTexture = false;

            if (releaseRenderTexture)
            {
                ReleaseRenderTexture();
                return;
            }

            if (ensureRenderTexture)
                EnsureRenderTexture();
        }

        private void QueueQualityCandidate(float qualityWeight01)
        {
            if (!_qualityInitialized)
            {
                InitializeQuality(qualityWeight01);
                _stateDirty = true;
                _renderRequested = true;
                return;
            }

            float previousFallback01 = _qualityFallback01;
            float previousOverkill01 = _visualOverkill01;
            ApplyQualityPolicy(qualityWeight01);
            if (!NearlyEqual(previousFallback01, _qualityFallback01) ||
                !NearlyEqual(previousOverkill01, _visualOverkill01))
            {
                _stateDirty = true;
                _renderRequested = true;
            }
        }

        private void ApplyQualityPolicy(float qualityWeight01)
        {
            _qualityWeight01 = math.saturate(math.isfinite(qualityWeight01) ? qualityWeight01 : 1f);
            float qualityCurve = SmoothStep01(_qualityWeight01);
            _qualityFallback01 = 1f - qualityCurve;
            _visualOverkill01 = SmoothStep01(math.saturate((_qualityWeight01 - 0.45f) * 1.8181819f));
        }

        private bool ResolveRequestedFallback()
        {
            return _poolUnavailableFallback;
        }

        private void TryRegisterSlowTickable()
        {
            if (_registeredSlowTick || !Application.isPlaying)
                return;

            _registeredSlowTick = SystemDispatcher.Register((ISlowTickable)this, PriorityLayer.UI);
        }

        private void TryRegisterLateFrameTickable()
        {
            if (_registeredLateFrame || !Application.isPlaying)
                return;

            _registeredLateFrame = SystemDispatcher.Register((ILateFrameTickable)this, PriorityLayer.UI);
        }

        private void TryUnregisterSlowTickable()
        {
            if (!_registeredSlowTick)
                return;

            SystemDispatcher.Unregister((ISlowTickable)this, PriorityLayer.UI);
            _registeredSlowTick = false;
        }

        private void TryUnregisterLateFrameTickable()
        {
            if (!_registeredLateFrame)
                return;

            SystemDispatcher.UnregisterLateFrameTickableDirect(this, PriorityLayer.UI);
            _registeredLateFrame = false;
            _pendingPresentationCommit = false;
            _pendingEnsureRenderTexture = false;
            _pendingReleaseRenderTexture = false;
            _pendingApplyScreenTexture = false;
            _pendingUseRenderTexture = false;
            _pendingFallbackActive = false;
            _pendingRenderThisFrame = false;
        }

        private void TryRegisterHotSwapListener()
        {
            if (_registeredHotSwapListener || !Application.isPlaying)
                return;

            _registeredHotSwapListener = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterHotSwapListener()
        {
            if (!_registeredHotSwapListener)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _registeredHotSwapListener = false;
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

        private static float SanitizeSeconds(float value)
        {
            return math.isfinite(value) ? math.max(0f, value) : 0f;
        }

        private static int ToPercentBucket(float value)
        {
            return math.clamp((int)math.round(Sanitize01(value) * 100f), 0, 100);
        }

        private static float SmoothStep01(float value)
        {
            float x = Sanitize01(value);
            return x * x * (3f - 2f * x);
        }

        private static bool NearlyEqual(float lhs, float rhs)
        {
            return math.abs(lhs - rhs) <= PropertyEpsilon;
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
