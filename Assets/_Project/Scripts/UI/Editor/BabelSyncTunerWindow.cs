#if UNITY_EDITOR
using System.Globalization;
using Hecton.Localization;
using Hecton8.Core;
using UnityEditor;
using UnityEngine.UIElements;

namespace Hecton8.UI.Editor
{
    public sealed class BabelSyncTunerWindow : EditorWindow
    {
        private const int PreviewCapacity = 2048;
        private const int HexCapacity = 4096;

        private readonly char[] _decodedPreview = new char[PreviewCapacity];
        private readonly char[] _hexPreview = new char[HexCapacity];

        private Label _stateLabel;
        private Label _decodedLabel;
        private Label _hexLabel;
        private TextField _hashField;
        private IntegerField _audioFrameOffsetField;
        private Slider _qualitySlider;
        private FloatField _durationField;

        [MenuItem("Tools/HECTON-8/Babel Sync Tuner")]
        public static void Open()
        {
            GetWindow<BabelSyncTunerWindow>("Babel Sync");
        }

        public void CreateGUI()
        {
            VisualElement root = rootVisualElement;
            root.Clear();
            root.style.paddingLeft = 8;
            root.style.paddingRight = 8;
            root.style.paddingTop = 8;
            root.style.paddingBottom = 8;

            _stateLabel = new Label("Babel runtime not sampled.");
            _stateLabel.style.whiteSpace = WhiteSpace.Normal;
            root.Add(_stateLabel);

            _hashField = new TextField("Token hash");
            _hashField.value = "0xBA150150";
            root.Add(_hashField);

            _durationField = new FloatField("Cue duration");
            _durationField.value = 3.25f;
            root.Add(_durationField);

            _audioFrameOffsetField = new IntegerField("Audio frame offset");
            _audioFrameOffsetField.value = BabelSubtitleSyncRuntime.EditorAudioFrameOffset;
            _audioFrameOffsetField.RegisterValueChangedCallback(evt =>
            {
                BabelSubtitleSyncRuntime.SetEditorAudioFrameOffset(evt.newValue);
                Refresh();
            });
            root.Add(_audioFrameOffsetField);

            _qualitySlider = new Slider("Global quality", 0f, 1f);
            _qualitySlider.value = HomeostasisBrain.GlobalQualityWeight;
            _qualitySlider.RegisterValueChangedCallback(evt =>
            {
                HomeostasisBrain.SetForcedGlobalQualityWeightForTuner(evt.newValue, true);
                Refresh();
            });
            root.Add(_qualitySlider);

            VisualElement row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.Add(new Button(Refresh) { text = "Refresh" });
            row.Add(new Button(PreviewHash) { text = "Preview Hash" });
            row.Add(new Button(PublishCue) { text = "Publish Cue" });
            row.Add(new Button(ClearQualityOverride) { text = "Clear Quality Override" });
            root.Add(row);

            _decodedLabel = new Label("Decoded preview empty.");
            _decodedLabel.style.whiteSpace = WhiteSpace.Normal;
            root.Add(_decodedLabel);

            _hexLabel = new Label("Raw UTF-8 hex empty.");
            _hexLabel.style.whiteSpace = WhiteSpace.Normal;
            root.Add(_hexLabel);

            EditorApplication.update -= Poll;
            EditorApplication.update += Poll;
            Refresh();
        }

        private void OnDisable()
        {
            EditorApplication.update -= Poll;
        }

        private void Poll()
        {
            if (!BabelSubtitleSyncRuntime.TryGetLatestTelemetry(out _))
                return;

            Repaint();
        }

        private void Refresh()
        {
            bool initialized = BabelSubtitleSyncRuntime.EnsureInitialized();
            bool hasTelemetry = BabelSubtitleSyncRuntime.TryGetLatestTelemetry(out LocalizationTelemetryEntry entry);
            _stateLabel.text =
                "Initialized: " + initialized +
                "\nLayout valid: " + BabelSubtitleSyncRuntime.LayoutValid +
                "\nRollback excluded: " + BabelSubtitleSyncRuntime.RollbackStateExcluded +
                "\nAudio frame: " + BabelSubtitleSyncRuntime.CurrentAudioFrame +
                "\nSample rate: " + BabelSubtitleSyncRuntime.CurrentSampleRate +
                "\nActive cues: " + BabelSubtitleSyncRuntime.ActiveCueCount +
                "\nTelemetry: " + (hasTelemetry ? "ready" : "empty") +
                "\nLast token: 0x" + (hasTelemetry ? entry.LastTokenHash : 0u).ToString("X8") +
                "\nDecoded chars: " + (hasTelemetry ? entry.DecodedCharacterCount : 0u) +
                "\nDecode ms: " + (hasTelemetry ? entry.Utf8DecodeMilliseconds : 0f).ToString("0.000", CultureInfo.InvariantCulture) +
                "\nMissing hashes: " + (hasTelemetry ? entry.MissingTokenHashCount : 0u) +
                "\nDropped cues: " + (hasTelemetry ? entry.DroppedCueCount : 0u);
        }

        private void PreviewHash()
        {
            Refresh();
            if (!TryParseHash(_hashField.value, out uint hash))
            {
                _decodedLabel.text = "Hash parse failed.";
                _hexLabel.text = "Raw UTF-8 hex unavailable.";
                return;
            }

            bool found = LocRegistry.TryWriteVisualSpanFromUtf8(
                hash,
                _decodedPreview.AsSpan(),
                out int decodedLength,
                false);
            _decodedLabel.text = found && decodedLength > 0
                ? new string(_decodedPreview, 0, decodedLength)
                : "Decoded preview unavailable for 0x" + hash.ToString("X8") + ".";

            if (!LocRegistry.TryGetLocalizedSpan(hash, out System.ReadOnlySpan<byte> bytes) || bytes.Length <= 0)
            {
                _hexLabel.text = "Raw UTF-8 hex unavailable for 0x" + hash.ToString("X8") + ".";
                return;
            }

            int hexLength = WriteHexPreview(bytes, _hexPreview);
            _hexLabel.text = hexLength > 0
                ? new string(_hexPreview, 0, hexLength)
                : "Raw UTF-8 hex truncated.";
        }

        private void PublishCue()
        {
            if (!TryParseHash(_hashField.value, out uint hash))
                return;

            float duration = _durationField != null ? _durationField.value : 3.25f;
            uint startFrame = BabelSubtitleSyncRuntime.CurrentAudioFrame;
            BabelSubtitleSyncRuntime.PublishCue(
                hash,
                startFrame,
                duration,
                BabelSubtitleSyncRuntime.FlagVisualOnlyNoRollback,
                priority: 0);
            Refresh();
        }

        private void ClearQualityOverride()
        {
            HomeostasisBrain.SetForcedGlobalQualityWeightForTuner(-1f, false);
            _qualitySlider.value = HomeostasisBrain.GlobalQualityWeight;
            Refresh();
        }

        private static int WriteHexPreview(System.ReadOnlySpan<byte> bytes, char[] destination)
        {
            int cursor = 0;
            int count = bytes.Length < 256 ? bytes.Length : 256;
            for (int i = 0; i < count; i++)
            {
                if (cursor + 3 >= destination.Length)
                    break;

                byte value = bytes[i];
                destination[cursor++] = ToHex((value >> 4) & 0xF);
                destination[cursor++] = ToHex(value & 0xF);
                destination[cursor++] = ' ';
            }

            return cursor;
        }

        private static char ToHex(int value)
        {
            return (char)(value < 10 ? '0' + value : 'A' + (value - 10));
        }

        private static bool TryParseHash(string value, out uint hash)
        {
            hash = 0u;
            if (string.IsNullOrWhiteSpace(value))
                return false;

            string trimmed = value.Trim();
            if (trimmed.StartsWith("0x", System.StringComparison.OrdinalIgnoreCase))
                return uint.TryParse(trimmed.Substring(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out hash);

            return uint.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out hash);
        }
    }
}
#endif
