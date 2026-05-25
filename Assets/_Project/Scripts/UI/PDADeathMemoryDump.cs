using Hecton.Localization;
using Hecton8.Core;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Gameplay;
using System;
using TMPro;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UI;

namespace Hecton8.UI
{
    /// <summary>
    /// Player-owned fatal-pressure death overlay that renders a high-speed Hecton-OS memory dump instead of an immediate black cut.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/UI/PDA Death Memory Dump")]
    public sealed class PDADeathMemoryDump : MonoBehaviour, ILateFrameTickable, IGlobalRegistryHotSwapListener
    {
        private enum DumpState : byte
        {
            Hidden = 0,
            Revealing = 1,
            Hold = 2,
            Fade = 3
        }

        private const int LibraryLineCount = 192;
        private const int SequenceLineCount = 180;
        private const float LinesPerSecond = 60f;
        private const float ScrollPixelsPerLine = 10.5f;
        private const float HoldDuration = 0.45f;
        private const float FadeSharpness = 5.2f;
        private const float HiddenAlphaCutoff = 0.01f;
        private const int DumpPayloadCharCapacity = 16384;
        private const string OverlayName = "PDADeathMemoryDumpOverlay";
        private const string DefaultFinalLine = "LOCALIZATION MODULE... DESTROYED.";
        private static readonly int FinalLineKeyHash = LocHash.Compute(LocalizationKeys.PDA_DEATH_DUMP_FINAL);

        private static readonly char[] s_emptyDumpChars = System.Array.Empty<char>();
        private static readonly Color BackgroundColor = new Color(0f, 0f, 0f, 0.96f);
        private static readonly Color DumpTextColor = new Color(0.72f, 1f, 0.82f, 0.96f);
        // COLD ALLOC: string[12] — death-dump module token table — owner: PDADeathMemoryDump
        private static readonly string[] DumpModules =
        {
            "MEMBUS", "PRESSURE CORE", "ABYSSAL CACHE", "LOCALIZATION", "HULL TRACE", "VECTOR STACK",
            "BLACKBOX", "ROUTE HASH", "SUIT SHELL", "SONAR BUS", "ARCHIVE", "RECOVERY MAP"
        };

        // COLD ALLOC: string[12] — death-dump operation token table — owner: PDADeathMemoryDump
        private static readonly string[] DumpOperations =
        {
            "SECTOR LOCK", "PAGE FLUSH", "STACK REWIND", "CRC CHECK", "INDEX SWEEP", "SIGNAL STITCH",
            "KERNEL MAP", "BIOFORM TRACE", "ANCHOR MOUNT", "PRESSURE SAMPLE", "HELMET CACHE", "FAULT REPLAY"
        };

        // COLD ALLOC: string[12] — death-dump status token table — owner: PDADeathMemoryDump
        private static readonly string[] DumpStates =
        {
            "OK", "STALE", "CORRUPTED", "REPLAY", "NULL", "OVERRUN",
            "DESYNC", "DROPPED", "COMPROMISED", "JITTER", "FROZEN", "WIPED"
        };

        // COLD ALLOC: string[192] — reusable per-session memory-dump line library — owner: PDADeathMemoryDump
        private readonly char[][] _dumpLineLibrary = new char[LibraryLineCount][];
        private readonly int[] _dumpLineLibraryLengths = new int[LibraryLineCount];
        // COLD ALLOC: int[180] — visible-character thresholds for line-based reveal without per-frame string rebuilds — owner: PDADeathMemoryDump
        private readonly int[] _lineCharacterThresholds = new int[SequenceLineCount];
        // COLD ALLOC: char[16384] — TMP payload staging buffer for death dump SetCharArray path — owner: PDADeathMemoryDump
        private readonly char[] _dumpPayloadBuffer = new char[DumpPayloadCharCapacity];

        private struct DumpTextWriter
        {
            private readonly char[] _buffer;
            public int Length;

            public DumpTextWriter(char[] buffer)
            {
                _buffer = buffer;
                Length = 0;
            }

            public void Clear()
            {
                Length = 0;
            }

            public void Append(ReadOnlySpan<char> text)
            {
                if (_buffer == null || text.Length <= 0 || Length >= _buffer.Length)
                    return;

                int writable = math.min(text.Length, _buffer.Length - Length);
                text.Slice(0, writable).CopyTo(_buffer.AsSpan(Length, writable));
                Length += writable;
            }

            public void Append(char value)
            {
                if (_buffer == null || Length >= _buffer.Length)
                    return;

                _buffer[Length++] = value;
            }

            public void AppendInt(int value)
            {
                if (value == int.MinValue)
                {
                    Append("-2147483648".AsSpan());
                    return;
                }

                if (value < 0)
                {
                    Append('-');
                    value = -value;
                }

                Span<char> digits = stackalloc char[10];
                int count = 0;
                do
                {
                    digits[count++] = (char)('0' + (value % 10));
                    value /= 10;
                }
                while (value > 0 && count < digits.Length);

                for (int i = count - 1; i >= 0; i--)
                    Append(digits[i]);
            }

            public void AppendHex(uint value)
            {
                const string Hex = "0123456789ABCDEF";
                for (int shift = 28; shift >= 0; shift -= 4)
                    Append(Hex[(int)((value >> shift) & 0xFu)]);
            }
        }

        [Header("── Font ──────────────────")]
        [Tooltip("Optional readable font override for the death memory dump overlay.")]
        [SerializeField] private TMP_FontAsset dumpFont;

        private bool _uiBuilt;
        private bool _tickRegistered;
        private bool _libraryBuilt;
        private Canvas _targetCanvas;
        private RectTransform _overlayRoot;
        private RectTransform _textRoot;
        private CanvasGroup _overlayGroup;
        private Image _background;
        private TextMeshProUGUI _dumpLabel;
        private DumpState _state;
        private float _lineProgress;
        private float _holdTimer;
        private int _visibleLineTarget;
        private int _lastSurvivalDeathSignalSequence;
        private bool _hotSwapListenerRegistered;
        private ILocalizationTextReadModel _cachedLocalization;
        private IPlayerRuntimeContext _cachedPlayerContext;

        private void OnEnable()
        {
            dumpFont = LocalizedFontResolver.ResolveReadableFont(dumpFont);
            CacheRegistryServicesCold();
            EnsureLineLibrary();
            EnsureUiBuilt();
            TryRegisterHotSwapListener();
            RegisterToTickManager();
        }

        private void OnDisable()
        {
            TryUnregisterHotSwapListener();
            UnregisterFromTickManager();
            HideOverlay();
        }

        private void OnDestroy()
        {
            TryUnregisterHotSwapListener();
            UnregisterFromTickManager();
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot == GlobalRegistryServiceSlot.Dispatcher)
            {
                if (currentService == null)
                {
                    _tickRegistered = false;
                }
                else if (isActiveAndEnabled)
                {
                    UnregisterFromTickManager();
                    RegisterToTickManager();
                }

                return;
            }

            if (serviceSlot == GlobalRegistryServiceSlot.LocalizationRuntime)
            {
                _cachedLocalization = currentService as ILocalizationTextReadModel;
                _libraryBuilt = false;
                EnsureLineLibrary();
            }
            else if (serviceSlot == GlobalRegistryServiceSlot.Player)
            {
                _cachedPlayerContext = currentService as IPlayerRuntimeContext;
            }
        }

        /// <inheritdoc />
        public void LateFrameTick()
        {
            ConsumeSurvivalDeathSignal();

            if (_dumpLabel == null || _overlayGroup == null || _state == DumpState.Hidden)
                return;

            float dt = math.max(0f, SystemDispatcher.CurrentFrameDeltaTime);
            switch (_state)
            {
                case DumpState.Revealing:
                    _lineProgress += dt * LinesPerSecond;
                    int visibleLines = math.min(_visibleLineTarget, (int)math.floor(_lineProgress));
                    int visibleCharacters = visibleLines > 0
                        ? _lineCharacterThresholds[visibleLines - 1]
                        : 0;
                    if (_dumpLabel.maxVisibleCharacters != visibleCharacters)
                        _dumpLabel.maxVisibleCharacters = visibleCharacters;

                    if (_textRoot != null)
                        _textRoot.anchoredPosition = new Vector2(28f, -28f + (_lineProgress * ScrollPixelsPerLine));

                    if (visibleLines >= _visibleLineTarget)
                    {
                        _state = DumpState.Hold;
                        _holdTimer = HoldDuration;
                    }
                    break;

                case DumpState.Hold:
                    _holdTimer -= dt;
                    if (_holdTimer <= 0f)
                        _state = DumpState.Fade;
                    break;

                case DumpState.Fade:
                    _overlayGroup.alpha = math.lerp(_overlayGroup.alpha, 0f, FastDecayBlend(FadeSharpness, dt));
                    if (_overlayGroup.alpha <= HiddenAlphaCutoff)
                    {
                        HideOverlay();
                    }
                    break;
            }
        }

        private void ConsumeSurvivalDeathSignal()
        {
            if (!SurvivalSignalRoute.TryGetLatestDeath(out SurvivalVitalsChangedSignal signal, out int sequence))
                return;

            if (sequence == _lastSurvivalDeathSignalSequence)
                return;

            _lastSurvivalDeathSignalSequence = sequence;
            if (signal.DeathCause != (byte)SurvivalDeathCause.PressureCollapse)
                return;

            IPlayerRuntimeContext playerContext = _cachedPlayerContext;
            HectonSurvivalSystem survival = playerContext != null ? playerContext.SurvivalSystem : null;
            if (survival == null)
                return;

            uint sourceId = RuntimeOriginRoute.FoldEntityIdToSourceId(EntityId.ToULong(survival.GetEntityId()));
            if (signal.SourceId != sourceId ||
                !survival.TryGetLastDeathRecord(out SurvivalDeathRecord deathRecord))
            {
                return;
            }

            StartDump(deathRecord);
        }

        private void StartDump(SurvivalDeathRecord record)
        {
            EnsureLineLibrary();
            EnsureUiBuilt();
            if (_dumpLabel == null || _overlayGroup == null)
                return;

            int seed = ComputeDumpSeed(record);
            int writeIndex = 0;
            DumpTextWriter writer = new DumpTextWriter(_dumpPayloadBuffer);

            for (int i = 0; i < SequenceLineCount - 1; i++)
            {
                switch (i)
                {
                    case 22:
                        AppendTelemetryLine(ref writer, "PRESSURE VECTOR".AsSpan(), (int)math.round((float)record.PeakDepthMeters), "M".AsSpan());
                        break;

                    case 61:
                        AppendTelemetryLine(ref writer, "INTEGRITY TRACE".AsSpan(), (int)math.round(record.LowestIntegrityNormalized * 100f), "%".AsSpan());
                        break;

                    case 118:
                        AppendTelemetryLine(ref writer, "LIFE TRACE".AsSpan(), (int)math.round((float)record.LifeDurationSeconds), "S".AsSpan());
                        break;

                    default:
                        int lineIndex = (seed + (i * 11)) % LibraryLineCount;
                        char[] line = _dumpLineLibrary[lineIndex];
                        int lineLength = _dumpLineLibraryLengths[lineIndex];
                        if (line != null && lineLength > 0)
                            writer.Append(line.AsSpan(0, math.min(lineLength, line.Length)));
                        break;
                }

                writer.Append('\n');
                _lineCharacterThresholds[writeIndex] = writer.Length;
                writeIndex++;
            }

            writer.Append(ResolveLocalizedSpan(FinalLineKeyHash, DefaultFinalLine.AsSpan()));
            _lineCharacterThresholds[writeIndex] = writer.Length;
            _visibleLineTarget = writeIndex + 1;

            int payloadLength = math.min(writer.Length, _dumpPayloadBuffer.Length);
            for (int i = 0; i < _visibleLineTarget; i++)
                _lineCharacterThresholds[i] = math.min(_lineCharacterThresholds[i], payloadLength);

            _dumpLabel.SetCharArray(_dumpPayloadBuffer, 0, payloadLength);
            _dumpLabel.maxVisibleCharacters = 0;
            if (_textRoot != null)
                _textRoot.anchoredPosition = new Vector2(28f, -28f);

            _overlayGroup.alpha = 1f;
            _overlayGroup.blocksRaycasts = false;
            _overlayGroup.interactable = false;
            _lineProgress = 0f;
            _holdTimer = 0f;
            _state = DumpState.Revealing;
        }

        private void EnsureLineLibrary()
        {
            if (_libraryBuilt)
                return;

            DumpTextWriter writer = new DumpTextWriter(_dumpPayloadBuffer);
            for (int i = 0; i < LibraryLineCount; i++)
            {
                writer.Clear();
                writer.AppendHex(0x91F0A000u + (uint)(i * 0x31 + 0x17));
                writer.Append(" // ".AsSpan());
                writer.Append(DumpModules[i % DumpModules.Length].AsSpan());
                writer.Append(" :: ".AsSpan());
                writer.Append(DumpOperations[(i * 5 + 3) % DumpOperations.Length].AsSpan());
                writer.Append(" -> ".AsSpan());
                writer.Append(DumpStates[(i * 7 + 1) % DumpStates.Length].AsSpan());
                _dumpLineLibrary[i] = CreateCharsFromBuffer(_dumpPayloadBuffer, writer.Length, out _dumpLineLibraryLengths[i]);
            }

            _libraryBuilt = true;
        }

        private static float FastDecayBlend(float speed, float deltaTime)
        {
            float x = math.max(0f, speed) * math.max(0f, deltaTime);
            if (x >= 3.5f)
                return 1f;

            return math.saturate((12f * x) / (12f + (6f * x) + (x * x)));
        }

        private static void AppendTelemetryLine(ref DumpTextWriter writer, ReadOnlySpan<char> label, int value, ReadOnlySpan<char> suffix)
        {
            writer.Append("0x".AsSpan());
            writer.AppendHex(0xE11D0000u + (uint)(value & 0xFFFF));
            writer.Append(" // ".AsSpan());
            writer.Append(label);
            writer.Append(" :: ".AsSpan());
            writer.AppendInt(value);
            writer.Append(suffix);
            writer.Append(" -> COMPROMISED".AsSpan());
        }

        private static char[] CreateCharsFromBuffer(char[] source, int sourceLength, out int length)
        {
            length = 0;
            if (source == null || sourceLength <= 0)
                return s_emptyDumpChars;

            length = math.min(sourceLength, math.min(DumpPayloadCharCapacity, source.Length));
            char[] buffer = new char[length]; // COLD ALLOC: char[length] - death-dump line library entry - owner: PDADeathMemoryDump
            source.AsSpan(0, length).CopyTo(buffer);
            return buffer;
        }

        private static int ComputeDumpSeed(SurvivalDeathRecord record)
        {
            unchecked
            {
                int hash = 17;
                hash = (hash * 31) + (int)math.round((float)record.PeakDepthMeters);
                hash = (hash * 31) + (int)math.round((float)record.LifeDurationSeconds);
                hash = (hash * 31) + (int)math.round(record.LowestIntegrityNormalized * 1000f);
                hash = (hash * 31) + (int)math.round(record.Position.x * 10f);
                hash = (hash * 31) + (int)math.round(record.Position.y * 10f);
                hash = (hash * 31) + (int)math.round(record.Position.z * 10f);
                return (hash & int.MaxValue) % LibraryLineCount;
            }
        }

        private void EnsureUiBuilt()
        {
            if (_uiBuilt)
                return;

            _targetCanvas = ResolveTargetCanvas();
            if (_targetCanvas == null)
                return;

            RectTransform canvasRoot = HectonUIScaler.ResolveContentRoot(_targetCanvas);
            if (canvasRoot == null)
                return;

            _overlayRoot = FindExistingChild(canvasRoot, OverlayName);
            if (_overlayRoot == null)
            {
                // COLD ALLOC: GameObject[1] — fatal-pressure death dump overlay host — owner: PDADeathMemoryDump
                GameObject overlayObject = new GameObject(OverlayName, typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
                overlayObject.layer = canvasRoot.gameObject.layer;
                overlayObject.TryGetComponent(out _overlayRoot);
                _overlayRoot.SetParent(canvasRoot, false);
            }

            _overlayRoot.anchorMin = Vector2.zero;
            _overlayRoot.anchorMax = Vector2.one;
            _overlayRoot.offsetMin = Vector2.zero;
            _overlayRoot.offsetMax = Vector2.zero;
            _overlayRoot.localScale = Vector3.one;
            _overlayRoot.SetAsLastSibling();

            if (!_overlayRoot.TryGetComponent(out _overlayGroup))
                _overlayGroup = _overlayRoot.gameObject.AddComponent<CanvasGroup>();
            _overlayGroup.alpha = 0f;
            _overlayGroup.interactable = false;
            _overlayGroup.blocksRaycasts = false;

            if (!_overlayRoot.TryGetComponent(out _background))
                _background = _overlayRoot.gameObject.AddComponent<Image>();
            _background.color = BackgroundColor;
            _background.raycastTarget = false;

            ClearChildren(_overlayRoot);

            _textRoot = CreateRect(_overlayRoot, "DumpTextRoot");
            _textRoot.anchorMin = new Vector2(0f, 1f);
            _textRoot.anchorMax = new Vector2(0f, 1f);
            _textRoot.pivot = new Vector2(0f, 1f);
            _textRoot.anchoredPosition = new Vector2(28f, -28f);
            _textRoot.sizeDelta = new Vector2(1720f, 3200f);

            _dumpLabel = _textRoot.gameObject.AddComponent<TextMeshProUGUI>();
            _dumpLabel.font = dumpFont;
            _dumpLabel.fontSize = 18f;
            _dumpLabel.fontStyle = FontStyles.Bold;
            _dumpLabel.color = DumpTextColor;
            _dumpLabel.alignment = TextAlignmentOptions.TopLeft;
            _dumpLabel.raycastTarget = false;
            _dumpLabel.textWrappingMode = TextWrappingModes.NoWrap;
            _dumpLabel.overflowMode = TextOverflowModes.Overflow;
            _dumpLabel.lineSpacing = -18f;
            _dumpLabel.maxVisibleCharacters = int.MaxValue;
            TMP_TextRegistry.EnsureRegistered(_dumpLabel);

            _uiBuilt = true;
        }

        private void HideOverlay()
        {
            _state = DumpState.Hidden;
            _lineProgress = 0f;
            _holdTimer = 0f;
            _visibleLineTarget = 0;

            if (_overlayGroup != null)
            {
                _overlayGroup.alpha = 0f;
                _overlayGroup.interactable = false;
                _overlayGroup.blocksRaycasts = false;
            }

            if (_dumpLabel != null)
            {
                _dumpLabel.SetCharArray(s_emptyDumpChars, 0, 0);
                _dumpLabel.maxVisibleCharacters = int.MaxValue;
            }
        }

        private void RegisterToTickManager()
        {
            if (_tickRegistered || !Application.isPlaying)
                return;

            _tickRegistered = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.UI);
        }

        private void UnregisterFromTickManager()
        {
            if (!_tickRegistered)
                return;

            GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.UI);
            _tickRegistered = false;
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

        private static Canvas ResolveTargetCanvas()
        {
            SuitHUDV4CanvasOverlay overlay = SuitHUDV4CanvasOverlay.ActiveRuntimeInstance;
            if (overlay != null && overlay.TargetCanvas != null)
                return overlay.TargetCanvas;

            return overlay != null && overlay.TryGetComponent(out Canvas canvas) ? canvas : null;
        }

        private ReadOnlySpan<char> ResolveLocalizedSpan(int keyHash, ReadOnlySpan<char> fallback)
        {
            ILocalizationTextReadModel manager = _cachedLocalization;
            return manager != null
                ? manager.GetRawSpanOrFallback(keyHash, fallback)
                : fallback;
        }

        private void CacheRegistryServicesCold()
        {
            _cachedLocalization = Hecton8.Core.GlobalRegistry.LocalizationText;
            _cachedPlayerContext = Hecton8.Core.PlayerRuntimeContextService.ActiveRuntimeContext;
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

        private static void ClearChildren(Transform parent)
        {
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                Transform child = parent.GetChild(i);
                if (Application.isPlaying)
                    UnityEngine.Object.Destroy(child.gameObject);
                else
                    UnityEngine.Object.DestroyImmediate(child.gameObject);
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
    }
}
