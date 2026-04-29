using Hecton.Localization;
using Hecton8.Core;
using Hecton8.Gameplay;
using Hecton8.Modding;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Hecton8.UI
{
    /// <summary>
    /// Player-owned fatal-pressure death overlay that renders a high-speed Hecton-OS memory dump instead of an immediate black cut.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/UI/PDA Death Memory Dump")]
    public sealed class PDADeathMemoryDump : MonoBehaviour, ITickable, IUpdatable
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
        private const string OverlayName = "PDADeathMemoryDumpOverlay";
        private const string DefaultFinalLine = "LOCALIZATION MODULE... DESTROYED.";

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
        private readonly string[] _dumpLineLibrary = new string[LibraryLineCount];
        // COLD ALLOC: int[180] — visible-character thresholds for line-based reveal without per-frame string rebuilds — owner: PDADeathMemoryDump
        private readonly int[] _lineCharacterThresholds = new int[SequenceLineCount];
        // COLD ALLOC: StringBuilder[16384] — fixed-capacity death-dump assembly buffer reused for line library and final payload — owner: PDADeathMemoryDump
        private readonly StringBuilder _dumpBuilder = new StringBuilder(16384);

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
        private HectonEventSubscription _playerDiedSubscription;

        private void OnEnable()
        {
            dumpFont = LocalizedFontResolver.ResolveReadableFont(dumpFont);
            EnsureLineLibrary();
            EnsureUiBuilt();
            Subscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
            UnregisterFromTickManager();
            HideOverlay();
        }

        private void OnDestroy()
        {
            Unsubscribe();
            UnregisterFromTickManager();
        }

        /// <inheritdoc />
        public void Tick(float dt)
        {
            if (_dumpLabel == null || _overlayGroup == null || _state == DumpState.Hidden)
                return;

            switch (_state)
            {
                case DumpState.Revealing:
                    _lineProgress += dt * LinesPerSecond;
                    int visibleLines = Mathf.Min(_visibleLineTarget, Mathf.FloorToInt(_lineProgress));
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
                    _overlayGroup.alpha = Mathf.Lerp(_overlayGroup.alpha, 0f, 1f - Mathf.Exp(-FadeSharpness * dt));
                    if (_overlayGroup.alpha <= HiddenAlphaCutoff)
                    {
                        HideOverlay();
                        UnregisterFromTickManager();
                    }
                    break;
            }
        }

        private void Subscribe()
        {
            if (_playerDiedSubscription == null)
                _playerDiedSubscription = HectonEventBus.Subscribe<PlayerDiedEvent>(HandlePlayerDied, "ui.death-memory-dump");
        }

        private void Unsubscribe()
        {
            _playerDiedSubscription?.Dispose();
            _playerDiedSubscription = null;
        }

        private void HandlePlayerDied(PlayerDiedEvent playerDiedEvent)
        {
            if (playerDiedEvent == null || playerDiedEvent.DeathCause != SurvivalDeathCause.PressureCollapse)
                return;

            StartDump(playerDiedEvent.DeathRecord);
        }

        private void StartDump(SurvivalDeathRecord record)
        {
            EnsureLineLibrary();
            EnsureUiBuilt();
            if (_dumpLabel == null || _overlayGroup == null)
                return;

            int seed = ComputeDumpSeed(record);
            int writeIndex = 0;
            _dumpBuilder.Clear();

            for (int i = 0; i < SequenceLineCount - 1; i++)
            {
                switch (i)
                {
                    case 22:
                        AppendTelemetryLine(_dumpBuilder, "PRESSURE VECTOR", Mathf.RoundToInt((float)record.PeakDepthMeters), "M");
                        break;

                    case 61:
                        AppendTelemetryLine(_dumpBuilder, "INTEGRITY TRACE", Mathf.RoundToInt(record.LowestIntegrityNormalized * 100f), "%");
                        break;

                    case 118:
                        AppendTelemetryLine(_dumpBuilder, "LIFE TRACE", Mathf.RoundToInt((float)record.LifeDurationSeconds), "S");
                        break;

                    default:
                        _dumpBuilder.Append(_dumpLineLibrary[(seed + (i * 11)) % LibraryLineCount]);
                        break;
                }

                _dumpBuilder.Append('\n');
                _lineCharacterThresholds[writeIndex] = _dumpBuilder.Length;
                writeIndex++;
            }

            _dumpBuilder.Append(ResolveLocalized(LocalizationKeys.PDA_DEATH_DUMP_FINAL, DefaultFinalLine));
            _lineCharacterThresholds[writeIndex] = _dumpBuilder.Length;
            _visibleLineTarget = writeIndex + 1;

            _dumpLabel.text = _dumpBuilder.ToString();
            _dumpLabel.ForceMeshUpdate();
            _dumpLabel.maxVisibleCharacters = 0;
            if (_textRoot != null)
                _textRoot.anchoredPosition = new Vector2(28f, -28f);

            _overlayGroup.alpha = 1f;
            _overlayGroup.blocksRaycasts = false;
            _overlayGroup.interactable = false;
            _lineProgress = 0f;
            _holdTimer = 0f;
            _state = DumpState.Revealing;
            RegisterToTickManager();
        }

        private void EnsureLineLibrary()
        {
            if (_libraryBuilt)
                return;

            for (int i = 0; i < LibraryLineCount; i++)
            {
                _dumpBuilder.Clear();
                AppendHex(_dumpBuilder, 0x91F0A000u + (uint)(i * 0x31 + 0x17));
                _dumpBuilder.Append(" // ");
                _dumpBuilder.Append(DumpModules[i % DumpModules.Length]);
                _dumpBuilder.Append(" :: ");
                _dumpBuilder.Append(DumpOperations[(i * 5 + 3) % DumpOperations.Length]);
                _dumpBuilder.Append(" -> ");
                _dumpBuilder.Append(DumpStates[(i * 7 + 1) % DumpStates.Length]);
                _dumpLineLibrary[i] = _dumpBuilder.ToString();
            }

            _dumpBuilder.Clear();
            _libraryBuilt = true;
        }

        private static void AppendTelemetryLine(StringBuilder builder, string label, int value, string suffix)
        {
            builder.Append("0x");
            AppendHex(builder, 0xE11D0000u + (uint)(value & 0xFFFF));
            builder.Append(" // ");
            builder.Append(label);
            builder.Append(" :: ");
            builder.Append(value);
            builder.Append(suffix);
            builder.Append(" -> COMPROMISED");
        }

        private static void AppendHex(StringBuilder builder, uint value)
        {
            const string Hex = "0123456789ABCDEF";
            for (int shift = 28; shift >= 0; shift -= 4)
                builder.Append(Hex[(int)((value >> shift) & 0xFu)]);
        }

        private static int ComputeDumpSeed(SurvivalDeathRecord record)
        {
            unchecked
            {
                int hash = 17;
                hash = (hash * 31) + Mathf.RoundToInt((float)record.PeakDepthMeters);
                hash = (hash * 31) + Mathf.RoundToInt((float)record.LifeDurationSeconds);
                hash = (hash * 31) + Mathf.RoundToInt(record.LowestIntegrityNormalized * 1000f);
                hash = (hash * 31) + Mathf.RoundToInt(record.Position.x * 10f);
                hash = (hash * 31) + Mathf.RoundToInt(record.Position.y * 10f);
                hash = (hash * 31) + Mathf.RoundToInt(record.Position.z * 10f);
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
                _overlayRoot = overlayObject.GetComponent<RectTransform>();
                _overlayRoot.SetParent(canvasRoot, false);
            }

            _overlayRoot.anchorMin = Vector2.zero;
            _overlayRoot.anchorMax = Vector2.one;
            _overlayRoot.offsetMin = Vector2.zero;
            _overlayRoot.offsetMax = Vector2.zero;
            _overlayRoot.localScale = Vector3.one;
            _overlayRoot.SetAsLastSibling();

            _overlayGroup = _overlayRoot.GetComponent<CanvasGroup>();
            if (_overlayGroup == null)
                _overlayGroup = _overlayRoot.gameObject.AddComponent<CanvasGroup>();
            _overlayGroup.alpha = 0f;
            _overlayGroup.interactable = false;
            _overlayGroup.blocksRaycasts = false;

            _background = _overlayRoot.GetComponent<Image>();
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
                _dumpLabel.text = string.Empty;
                _dumpLabel.maxVisibleCharacters = int.MaxValue;
            }
        }

        private void RegisterToTickManager()
        {
            if (_tickRegistered || !Application.isPlaying)
                return;

            if (GlobalRegistry.Dispatcher == null)
                return;

            GlobalRegistry.RegisterUpdatable(this, PriorityLayer.UI);
            _tickRegistered = true;
        }

        private void UnregisterFromTickManager()
        {
            if (!_tickRegistered)
                return;

            GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.UI);
            _tickRegistered = false;
        }

        private static Canvas ResolveTargetCanvas()
        {
            SuitHUDV4CanvasOverlay overlay = SuitHUDV4CanvasOverlay.ActiveRuntimeInstance;
            if (overlay != null && overlay.TargetCanvas != null)
                return overlay.TargetCanvas;

            return (SuitHUDV4CanvasOverlay.ActiveRuntimeInstance != null ? SuitHUDV4CanvasOverlay.ActiveRuntimeInstance.GetComponent<Canvas>() : null);
        }

        private static string ResolveLocalized(string key, string fallback)
        {
            LocalizationManager manager = LocalizationManager.Instance;
            return manager != null
                ? manager.GetOrFallback(manager.CurrentLanguage, key, fallback)
                : fallback;
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
    }
}
