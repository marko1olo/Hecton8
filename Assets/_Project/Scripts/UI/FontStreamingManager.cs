using System;
using Hecton.Localization;
using Hecton8.Core;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Memory;
using TMPro;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

namespace Hecton8.UI
{
    /// <summary>
    /// Staged font-swap owner that spreads localized TMP font reassignment over multiple ticks.
    /// Prevents language-switch spikes when the UI has to swap many labels at once.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/UI/Font Streaming Manager")]
    public sealed class FontStreamingManager : MonoBehaviour, ILateFrameTickable, ILocalizationLanguageChangedListener, IGlobalRegistryHotSwapListener
    {
        private static int s_x001FontStreamingManagerSignalPushDropCount;
        private const string RootName = "FontStreamingStatus";
        private const string DefaultStatusText = "[REBOOTING LANG_MODULE...]";
        private const string BiosFallbackStatusText = "[BIOS FONT FALLBACK ACTIVE]";
        private const float StatusFadeOutSpeed = 6f;
        private const int FontReadinessTimeoutFrames = 2;
        private const ushort UIRescaleReasonLocalizedFontSwap = 1;
        private const SystemID VaultOwnerSystemId = SystemID.UI;
        private const BufferID VisibleHashPrefetchBufferId = BufferID.FontStreamingVisibleHashPrefetch;
        private const BufferID VisibleSlicePrefetchBufferId = BufferID.FontStreamingVisibleSlicePrefetch;

        private static readonly Color StatusTextColor = new Color(0.82f, 0.96f, 0.92f, 0.96f);
        private static readonly Color StatusBackgroundColor = new Color(0.02f, 0.08f, 0.10f, 0.82f);
        private static readonly uint _fontSwapRescaleHash = unchecked((uint)LocHash.Compute("FontStreamingManager.UIRescale"));
        // COLD ALLOC: LabelSwapScheduler[1] â€” staged font swap queue owner for active localized labels â€” owner: FontStreamingManager
        private readonly LabelSwapScheduler _swapScheduler = new LabelSwapScheduler();
        // COLD ALLOC: char[96] â€” status label assembly for staged font streaming â€” owner: FontStreamingManager
        private char[] _statusBuffer = new char[96];
        private VaultGenerationHandle<uint> _visibleHashPrefetchHandle;
        private VaultGenerationHandle<int2> _visibleSlicePrefetchHandle;
        private IDataVault _dataVault;
        private JobHandle _visiblePrefetchHandle;
        private int _visiblePrefetchCapacity;
        private int _visiblePrefetchCount;
        private bool _visiblePrefetchApplyToQueue;
        private bool _visiblePrefetchInFlight;
        private bool _visiblePrefetchBuffersLocked;

        private bool _registered;
        private bool _hotSwapListenerRegistered;
        private bool _uiBuilt;
        private bool _streaming;
        private int _queueCount;
        private int _queueIndex;
        private int _lastStatusPercent = int.MinValue;
        private int _fontReadinessStartFrame = -1;
        private bool _awaitingPrimaryFontReadiness;
        private bool _biosFallbackActive;
        private TMP_FontAsset _primaryFont;
        private TMP_FontAsset _targetFont;
        private Material _targetFontMaterial;
        private Canvas _targetCanvas;
        private RectTransform _root;
        private CanvasGroup _group;
        private TextMeshProUGUI _statusLabel;
        private float _visibleAlpha;

        private void OnEnable()
        {
            TryRegisterHotSwapListener();
            LocalizationEvents.RegisterLanguageListener(this);
            SceneManager.sceneLoaded += HandleSceneLoaded;
            EnsureRegistryNodes(SceneManager.GetActiveScene());
            EnsureUiBuilt(allowCreate: true);
            RegisterToTickManager();
        }

        private void Start()
        {
            TryRegisterHotSwapListener();
            EnsureUiBuilt(allowCreate: true);
            RegisterToTickManager();
        }

        private void OnDisable()
        {
            LocalizationEvents.UnregisterLanguageListener(this);
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            UnregisterFromTickManager();
            TryUnregisterHotSwapListener();
            ResetSwapState();
            DisposePrefetchBuffers();
            ReleaseTrackedFontData();
        }

        private void OnDestroy()
        {
            LocalizationEvents.UnregisterLanguageListener(this);
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            UnregisterFromTickManager();
            TryUnregisterHotSwapListener();
            DisposePrefetchBuffers();
            ReleaseTrackedFontData();
        }

        /// <inheritdoc />
        public void LateFrameTick()
        {
            if (!EnsureUiBuilt(allowCreate: false))
                return;

            float dt = math.max(0f, SystemDispatcher.CurrentFrameDeltaTime);
            TryCompleteVisibleHashPrefetch();

            if (_awaitingPrimaryFontReadiness)
                EvaluatePendingFontReadiness();

            if (_streaming)
            {
                ProcessSwapBatch();
                ApplyVisibleAlpha(1f);
                return;
            }

            if (_awaitingPrimaryFontReadiness)
            {
                ApplyVisibleAlpha(1f);
                return;
            }

            if (_visibleAlpha > 0.001f)
                ApplyVisibleAlpha(MoveTowards(_visibleAlpha, 0f, dt * StatusFadeOutSpeed));
        }

        public void OnLocalizationLanguageChanged(in LocalizationEventPayload payload)

        {

            HandleLanguageChanged((GameLanguage)payload.Language);

        }


        private void HandleLanguageChanged(GameLanguage language)
        {
            TMP_FontAsset targetFont = LocalizedFontResolver.ResolveReadableFontForLanguage(null, language);
            if (targetFont == null)
            {
                ResetSwapState();
                return;
            }

            _primaryFont = targetFont;
            _targetFont = null;
            _streaming = false;
            _biosFallbackActive = false;
            _awaitingPrimaryFontReadiness = true;
            _fontReadinessStartFrame = Hecton8.Core.SystemDispatcher.CurrentFrameIndex;
            _queueCount = 0;
            _queueIndex = 0;
            AbandonVisibleHashPrefetchResults();
            _swapScheduler.Clear();
            _lastStatusPercent = int.MinValue;
            UpdateStatusLabel();
            ApplyVisibleAlpha(1f);
        }

        private void CollectSwapQueue(TMP_FontAsset targetFont)
        {
            AbandonVisibleHashPrefetchResults();
            _swapScheduler.Clear();
            _queueCount = 0;
            int registeredCount = TMP_TextRegistry.Count;
            bool canPrefetch = !_visiblePrefetchInFlight || TryCompleteVisibleHashPrefetch();
            if (canPrefetch)
                EnsurePrefetchCapacity(registeredCount);

            int visibleHashCount = 0;
            NativeArray<uint> visibleHashes = default;
            bool hashWriteLocked = canPrefetch &&
                                   TryAcquireVisibleHashPrefetchWriteBuffer(registeredCount, out visibleHashes);
            try
            {
                for (int i = 0; i < registeredCount; i++)
                {
                    TMP_TextEntry entry = TMP_TextRegistry.GetEntryAt(i);
                    TMP_Text text = entry.Text;
                    if (!IsSwapCandidate(text, targetFont))
                        continue;

                    if (!_swapScheduler.Enqueue(entry))
                        break;

                    if (hashWriteLocked && visibleHashCount < visibleHashes.Length)
                    {
                        visibleHashes[visibleHashCount++] = !entry.IsUserInput && entry.HasLocalizationKey
                            ? unchecked((uint)entry.LocalizationKeyHash)
                            : 0u;
                    }
                }
            }
            finally
            {
                if (hashWriteLocked)
                    ReleaseVisibleHashPrefetchWriteBuffer();
            }

            DispatchVisibleHashPrefetch(visibleHashCount);
            _queueCount = _swapScheduler.PendingCount;
        }

        private void ProcessSwapBatch()
        {
            if (_visiblePrefetchInFlight &&
                !TryCompleteVisibleHashPrefetch() &&
                _visiblePrefetchApplyToQueue)
            {
                ApplyVisibleAlpha(1f);
                return;
            }

            int processed = _swapScheduler.DrainTick(_targetFont, _targetFontMaterial);
            _queueIndex += processed;

            UpdateStatusLabel();
            if (!_swapScheduler.HasPending)
            {
                PublishRescaleRequest();
                _streaming = false;
                _queueCount = 0;
                _queueIndex = 0;
                if (_biosFallbackActive)
                {
                    _awaitingPrimaryFontReadiness = true;
                    _lastStatusPercent = int.MinValue;
                    UpdateStatusLabel();
                }
            }
        }

        private static void PublishRescaleRequest()
        {
            UIRescaleRequestSignal signal = new UIRescaleRequestSignal
            {
                SourceHash = _fontSwapRescaleHash,
                Frame = Hecton8.Core.SystemDispatcher.CurrentFrameId,
                Reason = UIRescaleReasonLocalizedFontSwap,
                Language = (ushort)LocRegistry.ActiveLanguage,
                Flags = 0u,
                FontScale = 1f
            };
            SignalBus<UIRescaleRequestSignal>.TryPushTracked(in signal, ref s_x001FontStreamingManagerSignalPushDropCount);
            DiegeticHudManualLayout.FlushGlobalRescaleRequests();
        }

        private void EnsurePrefetchCapacity(int requiredCapacity)
        {
            if (requiredCapacity <= 0)
                return;

            IDataVault vault = CacheDataVaultCold();
            if (vault == null)
                return;

            if (HasPrefetchCapacity(requiredCapacity))
            {
                return;
            }

            DisposePrefetchBuffers();

            bool hashReady = EnsurePrefetchBuffer(
                ref _visibleHashPrefetchHandle,
                VisibleHashPrefetchBufferId,
                requiredCapacity,
                NativeArrayOptions.UninitializedMemory);
            bool sliceReady = EnsurePrefetchBuffer(
                ref _visibleSlicePrefetchHandle,
                VisibleSlicePrefetchBufferId,
                requiredCapacity,
                NativeArrayOptions.UninitializedMemory);

            if (hashReady && sliceReady)
            {
                _visiblePrefetchCapacity = requiredCapacity;
                return;
            }

            ReleasePrefetchHandle(ref _visibleHashPrefetchHandle);
            ReleasePrefetchHandle(ref _visibleSlicePrefetchHandle);
            _visiblePrefetchCapacity = 0;
        }

        private void DispatchVisibleHashPrefetch(int visibleHashCount)
        {
            if (visibleHashCount <= 0 || _visiblePrefetchInFlight)
                return;

            if (!TryAcquireVisiblePrefetchJobBuffers(
                    visibleHashCount,
                    out NativeArray<uint> visibleHashes,
                    out NativeArray<int2> outputSlices))
            {
                return;
            }

            if (LocRegistry.TryScheduleVisibleTextOffsetPrefetch(
                    visibleHashes,
                    outputSlices,
                    visibleHashCount,
                    default,
                    out JobHandle prefetchHandle))
            {
                _visiblePrefetchHandle = prefetchHandle;
                _visiblePrefetchCount = visibleHashCount;
                _visiblePrefetchApplyToQueue = true;
                _visiblePrefetchInFlight = true;
                return;
            }

            ReleaseVisiblePrefetchJobBufferLocks();
        }

        private bool TryCompleteVisibleHashPrefetch()
        {
            if (!_visiblePrefetchInFlight)
                return true;

            if (!_visiblePrefetchHandle.IsCompleted)
                return false;

            if (!DispatcherJobFence.TryFinalizeCompleted(ref _visiblePrefetchHandle))
                return false;

            LocRegistry.MarkVisibleTextOffsetPrefetchComplete();
            bool applyToQueue = _visiblePrefetchApplyToQueue && _visiblePrefetchCount > 0;
            int prefetchCount = _visiblePrefetchCount;
            ReleaseVisiblePrefetchJobBufferLocks();
            if (applyToQueue &&
                TryReadVisibleSlicePrefetch(prefetchCount, out NativeArray<int2>.ReadOnly slices))
            {
                _swapScheduler.ApplyPrefetchSlices(slices, prefetchCount);
            }

            _visiblePrefetchCount = 0;
            _visiblePrefetchApplyToQueue = false;
            _visiblePrefetchInFlight = false;
            return true;
        }

        private void AbandonVisibleHashPrefetchResults()
        {
            _visiblePrefetchCount = 0;
            _visiblePrefetchApplyToQueue = false;
        }

        private void CompleteVisibleHashPrefetchForTeardown()
        {
            if (!_visiblePrefetchInFlight)
                return;

            DispatcherJobFence.TryComplete(ref _visiblePrefetchHandle, forceComplete: true);
            LocRegistry.MarkVisibleTextOffsetPrefetchComplete();
            ReleaseVisiblePrefetchJobBufferLocks();
            _visiblePrefetchCount = 0;
            _visiblePrefetchApplyToQueue = false;
            _visiblePrefetchInFlight = false;
        }

        private void DisposePrefetchBuffers()
        {
            CompleteVisibleHashPrefetchForTeardown();
            ReleaseVisiblePrefetchJobBufferLocks();
            ReleasePrefetchHandle(ref _visibleHashPrefetchHandle);
            ReleasePrefetchHandle(ref _visibleSlicePrefetchHandle);
            _visiblePrefetchCapacity = 0;
        }

        private IDataVault CacheDataVaultCold()
        {
            if (_dataVault == null)
                _dataVault = GlobalRegistry.DataVault;

            return _dataVault;
        }

        private bool HasPrefetchCapacity(int requiredCapacity)
        {
            if (_visiblePrefetchCapacity < requiredCapacity)
                return false;

            return TryReadVisibleHashPrefetch(requiredCapacity, out _) &&
                   TryReadVisibleSlicePrefetch(requiredCapacity, out _);
        }

        private bool EnsurePrefetchBuffer<T>(
            ref VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredCapacity,
            NativeArrayOptions options) where T : unmanaged
        {
            IDataVault vault = _dataVault;
            if (vault == null)
                return false;

            if (IsExactVaultHandle(in handle, bufferId) &&
                vault.TryReadOnlyHandle(in handle, out NativeArray<T>.ReadOnly existing) &&
                existing.IsCreated &&
                existing.Length >= requiredCapacity)
            {
                return true;
            }

            if (handle.BufferID != 0u && handle.Generation != 0u)
                vault.ReleaseBuffer(in handle);

            handle = vault.EnsureGenerationHandle<T>(
                bufferId,
                requiredCapacity,
                VaultOwnerSystemId,
                options);

            return IsExactVaultHandle(in handle, bufferId) &&
                   vault.TryReadOnlyHandle(in handle, out NativeArray<T>.ReadOnly resolved) &&
                   resolved.IsCreated &&
                   resolved.Length >= requiredCapacity;
        }

        private bool TryAcquireVisibleHashPrefetchWriteBuffer(int requiredCapacity, out NativeArray<uint> visibleHashes)
        {
            visibleHashes = default;
            IDataVault vault = _dataVault;
            if (vault == null ||
                !IsExactVaultHandle(in _visibleHashPrefetchHandle, VisibleHashPrefetchBufferId) ||
                !vault.TryAcquireWriteLock(in _visibleHashPrefetchHandle, VaultOwnerSystemId, out visibleHashes))
            {
                return false;
            }

            if (visibleHashes.IsCreated && visibleHashes.Length >= requiredCapacity)
                return true;

            vault.ReleaseWriteLock(in _visibleHashPrefetchHandle, VaultOwnerSystemId);
            visibleHashes = default;
            return false;
        }

        private void ReleaseVisibleHashPrefetchWriteBuffer()
        {
            IDataVault vault = _dataVault;
            if (vault != null && IsExactVaultHandle(in _visibleHashPrefetchHandle, VisibleHashPrefetchBufferId))
                vault.ReleaseWriteLock(in _visibleHashPrefetchHandle, VaultOwnerSystemId);
        }

        private bool TryAcquireVisiblePrefetchJobBuffers(
            int requiredCapacity,
            out NativeArray<uint> visibleHashes,
            out NativeArray<int2> outputSlices)
        {
            visibleHashes = default;
            outputSlices = default;
            IDataVault vault = _dataVault;
            if (vault == null ||
                _visiblePrefetchBuffersLocked ||
                !TryAcquireVisibleHashPrefetchWriteBuffer(requiredCapacity, out visibleHashes))
            {
                return false;
            }

            if (!IsExactVaultHandle(in _visibleSlicePrefetchHandle, VisibleSlicePrefetchBufferId) ||
                !vault.TryAcquireWriteLock(in _visibleSlicePrefetchHandle, VaultOwnerSystemId, out outputSlices))
            {
                vault.ReleaseWriteLock(in _visibleHashPrefetchHandle, VaultOwnerSystemId);
                visibleHashes = default;
                return false;
            }

            if (outputSlices.IsCreated && outputSlices.Length >= requiredCapacity)
            {
                _visiblePrefetchBuffersLocked = true;
                return true;
            }

            vault.ReleaseWriteLock(in _visibleSlicePrefetchHandle, VaultOwnerSystemId);
            vault.ReleaseWriteLock(in _visibleHashPrefetchHandle, VaultOwnerSystemId);
            visibleHashes = default;
            outputSlices = default;
            return false;
        }

        private bool TryReadVisibleHashPrefetch(int requiredCapacity, out NativeArray<uint>.ReadOnly visibleHashes)
        {
            visibleHashes = default;
            IDataVault vault = _dataVault;
            return vault != null &&
                   IsExactVaultHandle(in _visibleHashPrefetchHandle, VisibleHashPrefetchBufferId) &&
                   vault.TryReadOnlyHandle(in _visibleHashPrefetchHandle, out visibleHashes) &&
                   visibleHashes.IsCreated &&
                   visibleHashes.Length >= requiredCapacity;
        }

        private bool TryReadVisibleSlicePrefetch(int requiredCapacity, out NativeArray<int2>.ReadOnly outputSlices)
        {
            outputSlices = default;
            IDataVault vault = _dataVault;
            return vault != null &&
                   IsExactVaultHandle(in _visibleSlicePrefetchHandle, VisibleSlicePrefetchBufferId) &&
                   vault.TryReadOnlyHandle(in _visibleSlicePrefetchHandle, out outputSlices) &&
                   outputSlices.IsCreated &&
                   outputSlices.Length >= requiredCapacity;
        }

        private void ReleaseVisiblePrefetchJobBufferLocks()
        {
            if (!_visiblePrefetchBuffersLocked)
                return;

            IDataVault vault = _dataVault;
            if (vault != null)
            {
                if (IsExactVaultHandle(in _visibleSlicePrefetchHandle, VisibleSlicePrefetchBufferId))
                    vault.ReleaseWriteLock(in _visibleSlicePrefetchHandle, VaultOwnerSystemId);

                if (IsExactVaultHandle(in _visibleHashPrefetchHandle, VisibleHashPrefetchBufferId))
                    vault.ReleaseWriteLock(in _visibleHashPrefetchHandle, VaultOwnerSystemId);
            }

            _visiblePrefetchBuffersLocked = false;
        }

        private void ReleasePrefetchHandle<T>(ref VaultGenerationHandle<T> handle) where T : unmanaged
        {
            IDataVault vault = _dataVault;
            if (vault != null && handle.BufferID != 0u && handle.Generation != 0u)
                vault.ReleaseBuffer(in handle);

            handle = default;
        }

        private static bool IsExactVaultHandle<T>(in VaultGenerationHandle<T> handle, BufferID expectedBufferId) where T : unmanaged
        {
            return handle.BufferID == unchecked((uint)(int)expectedBufferId) && handle.Generation != 0u;
        }

        private void EvaluatePendingFontReadiness()
        {
            if (_primaryFont == null)
            {
                ResetSwapState();
                return;
            }

            if (LocalizedFontResolver.IsFontReady(_primaryFont))
            {
                _awaitingPrimaryFontReadiness = false;
                BeginSwapQueue(_primaryFont, biosFallbackActive: false);
                return;
            }

            if (_biosFallbackActive)
            {
                UpdateStatusLabel();
                return;
            }

            if (Hecton8.Core.SystemDispatcher.CurrentFrameIndex - _fontReadinessStartFrame < FontReadinessTimeoutFrames)
            {
                UpdateStatusLabel();
                return;
            }

            TMP_FontAsset biosFallback = LocalizedFontResolver.ResolveBiosFallbackFont();
            if (biosFallback == null)
            {
                ResetSwapState();
                return;
            }

            _awaitingPrimaryFontReadiness = false;
            BeginSwapQueue(biosFallback, biosFallbackActive: true);
        }

        private void BeginSwapQueue(TMP_FontAsset targetFont, bool biosFallbackActive)
        {
            _targetFont = targetFont;
            _targetFontMaterial = targetFont != null ? targetFont.material : null;
            _biosFallbackActive = biosFallbackActive;
            CollectSwapQueue(targetFont);
            if (_queueCount <= 0)
            {
                if (_biosFallbackActive)
                {
                    _awaitingPrimaryFontReadiness = true;
                    _lastStatusPercent = int.MinValue;
                    UpdateStatusLabel();
                    ApplyVisibleAlpha(1f);
                    return;
                }

                ResetSwapState();
                return;
            }

            _streaming = true;
            _queueIndex = 0;
            _lastStatusPercent = int.MinValue;
            UpdateStatusLabel();
        }

        private bool EnsureUiBuilt(bool allowCreate)
        {
            if (_uiBuilt)
                return true;

            if (!allowCreate)
                return false;

            if (_targetCanvas == null)
                _targetCanvas = ResolveTargetCanvas();

            if (_targetCanvas == null)
                return false;

            RectTransform canvasRoot = HectonUIScaler.ResolveContentRoot(_targetCanvas);
            if (canvasRoot == null)
                return false;

            _root = FindExistingChild(canvasRoot, RootName);
            if (_root == null)
            {
                GameObject rootObject = new GameObject(RootName, typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
                rootObject.layer = canvasRoot.gameObject.layer;
                rootObject.TryGetComponent(out _root);
                _root.SetParent(canvasRoot, false);
            }

            _root.anchorMin = new Vector2(0.5f, 1f);
            _root.anchorMax = new Vector2(0.5f, 1f);
            _root.pivot = new Vector2(0.5f, 1f);
            _root.anchoredPosition = new Vector2(0f, -94f);
            _root.sizeDelta = new Vector2(348f, 34f);
            _root.SetAsLastSibling();

            if (!_root.TryGetComponent(out _group))
                _group = _root.gameObject.AddComponent<CanvasGroup>(); // COLD ALLOC: CanvasGroup[1] - repairs missing font streaming root component - owner: FontStreamingManager

            _group.alpha = 0f;
            _group.blocksRaycasts = false;
            _group.interactable = false;
            _visibleAlpha = 0f;

            if (!_root.TryGetComponent(out Image background))
                background = _root.gameObject.AddComponent<Image>(); // COLD ALLOC: Image[1] - repairs missing font streaming root component - owner: FontStreamingManager

            background.color = StatusBackgroundColor;
            background.raycastTarget = false;

            if (_statusLabel == null)
                _statusLabel = FindText(_root, "StatusLabel");

            if (_statusLabel == null)
            {
                GameObject labelObject = new GameObject("StatusLabel", typeof(RectTransform));
                labelObject.layer = _root.gameObject.layer;
                labelObject.TryGetComponent(out RectTransform labelRect);
                labelRect.SetParent(_root, false);
                labelRect.anchorMin = Vector2.zero;
                labelRect.anchorMax = Vector2.one;
                labelRect.offsetMin = new Vector2(12f, 4f);
                labelRect.offsetMax = new Vector2(-12f, -4f);

                _statusLabel = labelObject.AddComponent<TextMeshProUGUI>(); // COLD ALLOC: TextMeshProUGUI[1] â€” localized font streaming status label â€” owner: FontStreamingManager
                _statusLabel.font = LocalizedFontResolver.ResolveReadableFont(null);
                _statusLabel.color = StatusTextColor;
                _statusLabel.fontSize = 14f;
                _statusLabel.textWrappingMode = TextWrappingModes.NoWrap;
                _statusLabel.alignment = TextAlignmentOptions.MidlineLeft;
                _statusLabel.raycastTarget = false;
                TMP_TextRegistry.EnsureRegistered(_statusLabel);
            }

            ApplyStatusBuffer(0);
            _uiBuilt = true;
            return true;
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            EnsureRegistryNodes(scene);
            EnsureUiBuilt(allowCreate: true);
        }

        private void UpdateStatusLabel()
        {
            if (_statusLabel == null)
                return;

            if (_awaitingPrimaryFontReadiness && !_streaming)
            {
                if (_biosFallbackActive)
                {
                    if (_lastStatusPercent == 1000)
                        return;

                    _lastStatusPercent = 1000;
                    WriteStatusLiteral(BiosFallbackStatusText.AsSpan());
                    return;
                }

                if (_lastStatusPercent == -1000)
                    return;

                _lastStatusPercent = -1000;
                WriteStatusLiteral(DefaultStatusText.AsSpan());
                return;
            }

            int percent = _queueCount > 0
                ? math.clamp((int)math.round((_queueIndex / (float)_queueCount) * 100f), 0, 100)
                : 100;
            if (percent == _lastStatusPercent)
                return;

            _lastStatusPercent = percent;
            WriteStatusWithPercent(percent);
        }

        private void ResetSwapState()
        {
            _streaming = false;
            _awaitingPrimaryFontReadiness = false;
            _biosFallbackActive = false;
            _primaryFont = null;
            _targetFont = null;
            _targetFontMaterial = null;
            _queueCount = 0;
            _queueIndex = 0;
            _fontReadinessStartFrame = -1;
            _lastStatusPercent = int.MinValue;
            AbandonVisibleHashPrefetchResults();
            _swapScheduler.Clear();
            ApplyVisibleAlpha(0f);
        }

        private void ApplyVisibleAlpha(float alpha)
        {
            alpha = math.saturate(alpha);
            if (_group == null || math.abs(_visibleAlpha - alpha) <= 0.0001f)
                return;

            _visibleAlpha = alpha;
            _group.alpha = alpha;
        }

        private void RegisterToTickManager()
        {
            if (_registered || !Application.isPlaying)
                return;

            if (GlobalRegistry.Dispatcher == null)
                return;

            _registered = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.UI);
        }

        private void UnregisterFromTickManager()
        {
            if (!_registered)
                return;

            GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.UI);
            _registered = false;
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot == GlobalRegistryServiceSlot.DataVault)
            {
                DisposePrefetchBuffers();
                _dataVault = currentService as IDataVault;
                return;
            }

            if (serviceSlot == GlobalRegistryServiceSlot.Dispatcher && currentService != null && isActiveAndEnabled)
            {
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

        private static bool IsSwapCandidate(TMP_Text text, TMP_FontAsset targetFont)
        {
            if (text == null || targetFont == null)
                return false;

            if (text.font == targetFont || LocalizedFontResolver.IsNumericOnlyFont(text.font))
                return false;

            GameObject targetObject = text.gameObject;
            if (!targetObject.scene.IsValid())
                return false;

            return true;
        }

        private void EnsureRegistryNodes(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded)
                return;

            Canvas canvas = ResolveTargetCanvas();
            if (canvas == null)
                return;

            EnsureRegistryNodesInHierarchy(canvas.transform);
        }

        private static void EnsureRegistryNodesInHierarchy(Transform root)
        {
            if (root == null)
                return;

            if (root.TryGetComponent(out TMP_Text text))
                TMP_TextRegistry.EnsureRegistered(text);

            for (int i = 0; i < root.childCount; i++)
                EnsureRegistryNodesInHierarchy(root.GetChild(i));
        }

        private static Canvas ResolveTargetCanvas()
        {
            for (int i = 0; i < SuitHUDV4CanvasOverlay.ActiveOverlayCount; i++)
            {
                SuitHUDV4CanvasOverlay overlay = SuitHUDV4CanvasOverlay.GetActiveOverlay(i);
                if (overlay != null && overlay.TargetCanvas != null)
                    return overlay.TargetCanvas;
            }

            if (SuitHUDV4CanvasOverlay.ActiveRuntimeInstance == null)
                return null;

            SuitHUDV4CanvasOverlay.ActiveRuntimeInstance.TryGetComponent(out Canvas canvas);
            return canvas;
        }

        private static RectTransform FindExistingChild(Transform parent, string childName)
        {
            if (parent == null)
                return null;

            for (int i = 0; i < parent.childCount; i++)
            {
                Transform child = parent.GetChild(i);
                if (child.name == childName)
                    return child as RectTransform;
            }

            return null;
        }

        private static TextMeshProUGUI FindText(Transform parent, string childName)
        {
            if (parent == null)
                return null;

            for (int i = 0; i < parent.childCount; i++)
            {
                Transform child = parent.GetChild(i);
                if (child.name == childName)
                {
                    child.TryGetComponent(out TextMeshProUGUI text);
                    return text;
                }
            }

            return null;
        }

        private void WriteStatusLiteral(ReadOnlySpan<char> source)
        {
            int length = CopyStatusSpan(source);
            ApplyStatusBuffer(length);
        }

        private void WriteStatusWithPercent(int percent)
        {
            ReadOnlySpan<char> prefix = DefaultStatusText.AsSpan();
            int writeIndex = CopyStatusSpan(prefix);
            if (_statusBuffer == null || writeIndex >= _statusBuffer.Length)
            {
                ApplyStatusBuffer(writeIndex);
                return;
            }

            _statusBuffer[writeIndex++] = ' ';
            if (writeIndex >= _statusBuffer.Length)
            {
                ApplyStatusBuffer(writeIndex);
                return;
            }

            Span<char> writableSpan = _statusBuffer.AsSpan(writeIndex, _statusBuffer.Length - writeIndex);
            if (!percent.TryFormat(writableSpan, out int charsWritten))
            {
                ApplyStatusBuffer(0);
                return;
            }

            writeIndex += charsWritten;
            if (writeIndex < _statusBuffer.Length)
                _statusBuffer[writeIndex++] = '%';

            ApplyStatusBuffer(writeIndex);
        }

        private int CopyStatusSpan(ReadOnlySpan<char> source)
        {
            if (_statusBuffer == null || _statusBuffer.Length == 0)
                return 0;

            int length = math.min(source.Length, _statusBuffer.Length);
            for (int i = 0; i < length; i++)
                _statusBuffer[i] = source[i];

            return length;
        }

        private void ApplyStatusBuffer(int length)
        {
            if (_statusLabel == null || _statusBuffer == null)
                return;

            int safeLength = math.clamp(length, 0, _statusBuffer.Length);
            _statusLabel.SetCharArray(_statusBuffer, 0, safeLength);
        }

        private static float MoveTowards(float current, float target, float maxDelta)
        {
            float safeDelta = math.max(0f, maxDelta);
            float delta = target - current;
            if (math.abs(delta) <= safeDelta)
                return target;

            return current + math.sign(delta) * safeDelta;
        }

        private void ReleaseTrackedFontData()
        {
            LocalizedFontResolver.TryClearDynamicFontData(_primaryFont);

            if (!ReferenceEquals(_targetFont, _primaryFont))
                LocalizedFontResolver.TryClearDynamicFontData(_targetFont);

            if (_statusLabel != null)
                LocalizedFontResolver.TryClearDynamicFontData(_statusLabel.font);

            LocalizedFontResolver.ReleaseCachedRuntimeFonts();
        }
    }
}
