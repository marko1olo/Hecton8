using System;
using Hecton8.AtlasSignal;
using Hecton8.Core;
using TMPro;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UI;

namespace Hecton8.UI
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/UI/PDA Decryption Spectrogram Panel")]
    public sealed class PDADecryptionSpectrogramPanel : MonoBehaviour, IUpdatable
    {
        private const float SliderEpsilon = 0.0001f;

        [Header("Carrier Range")]
        [SerializeField, Min(1f)] private float minCarrierFrequencyHz = 20f;
        [SerializeField, Min(1f)] private float maxCarrierFrequencyHz = 240f;

        [Header("Colors")]
        [SerializeField] private Color backgroundColor = new Color(0.015f, 0.025f, 0.03f, 0.92f);
        [SerializeField] private Color trackColor = new Color(0.05f, 0.08f, 0.09f, 1f);
        [SerializeField] private Color inputColor = new Color(0.18f, 0.78f, 0.58f, 1f);
        [SerializeField] private Color targetColor = new Color(0.78f, 0.52f, 0.22f, 1f);
        [SerializeField] private Color textColor = new Color(0.82f, 0.90f, 0.86f, 1f);

        private RectTransform _root;
        private Slider _frequencySlider;
        private Slider _phaseSlider;
        private Image _matchFill;
        private Image _progressFill;
        private TextMeshProUGUI _frequencyValue;
        private TextMeshProUGUI _phaseValue;
        private TextMeshProUGUI _matchValue;
        private float _lastFrequency01 = -1f;
        private float _lastPhase01 = -1f;
        private bool _built;
        private bool _registered;

        // COLD ALLOC: char[24] - PDA spectrogram frequency buffer - owner: PDADecryptionSpectrogramPanel
        private readonly char[] _frequencyBuffer = new char[24];
        // COLD ALLOC: char[16] - PDA spectrogram phase buffer - owner: PDADecryptionSpectrogramPanel
        private readonly char[] _phaseBuffer = new char[16];
        // COLD ALLOC: char[16] - PDA spectrogram match buffer - owner: PDADecryptionSpectrogramPanel
        private readonly char[] _matchBuffer = new char[16];

        private void Awake()
        {
            _root = GetComponent<RectTransform>();
            if (_root == null)
                _root = gameObject.AddComponent<RectTransform>();
        }

        private void OnEnable()
        {
            EnsureBuilt();
            TryRegister();
            _lastFrequency01 = -1f;
            _lastPhase01 = -1f;
        }

        private void OnDisable()
        {
            TryUnregister();
        }

        private void OnDestroy()
        {
            TryUnregister();
        }

        public void Tick(float deltaTime)
        {
            AtlasSignalDecoder decoder = GlobalRegistry.AtlasSignalDecoder;
            if (decoder == null || _frequencySlider == null || _phaseSlider == null)
                return;

            float frequency01 = math.saturate(_frequencySlider.value);
            float phase01 = math.saturate(_phaseSlider.value);
            bool changed =
                math.abs(frequency01 - _lastFrequency01) > SliderEpsilon ||
                math.abs(phase01 - _lastPhase01) > SliderEpsilon;

            if (!changed)
            {
                UpdateProgress(decoder.CurrentDecodeProgress);
                return;
            }

            _lastFrequency01 = frequency01;
            _lastPhase01 = phase01;
            float carrierHz = math.lerp(minCarrierFrequencyHz, math.max(minCarrierFrequencyHz, maxCarrierFrequencyHz), frequency01);
            float match01 = decoder.SubmitWaveMatch(carrierHz, phase01);

            UpdateFrequencyText(carrierHz);
            UpdatePhaseText(phase01);
            UpdateMatchText(match01);
            UpdateMatch(match01);
            UpdateProgress(decoder.CurrentDecodeProgress);
        }

        public void SubmitNormalized(float frequency01, float phase01)
        {
            EnsureBuilt();
            if (_frequencySlider != null)
                _frequencySlider.value = math.saturate(frequency01);
            if (_phaseSlider != null)
                _phaseSlider.value = math.saturate(phase01);
            _lastFrequency01 = -1f;
            _lastPhase01 = -1f;
        }

        private void EnsureBuilt()
        {
            if (_built)
                return;

            _built = true;
            Image background = gameObject.GetComponent<Image>();
            if (background == null)
                background = gameObject.AddComponent<Image>();
            background.color = backgroundColor;

            TextMeshProUGUI title = CreateText("Title", _root, 12f, TextAlignmentOptions.MidlineLeft);
            title.SetText("SPECTROGRAM DECRYPT");
            Anchor(title.rectTransform, new Vector2(0f, 0.82f), new Vector2(1f, 1f), new Vector2(12f, 0f), new Vector2(-12f, 0f));

            CreateLabel("CarrierLabel", "CARRIER", 0.62f, 0.78f);
            _frequencySlider = CreateSlider("CarrierSlider", 0.48f, 0.62f, inputColor);
            _frequencyValue = CreateText("CarrierValue", _root, 9f, TextAlignmentOptions.MidlineRight);
            Anchor(_frequencyValue.rectTransform, new Vector2(0.62f, 0.62f), new Vector2(1f, 0.78f), new Vector2(0f, 0f), new Vector2(-12f, 0f));

            CreateLabel("PhaseLabel", "PHASE", 0.33f, 0.47f);
            _phaseSlider = CreateSlider("PhaseSlider", 0.20f, 0.34f, targetColor);
            _phaseValue = CreateText("PhaseValue", _root, 9f, TextAlignmentOptions.MidlineRight);
            Anchor(_phaseValue.rectTransform, new Vector2(0.62f, 0.33f), new Vector2(1f, 0.47f), new Vector2(0f, 0f), new Vector2(-12f, 0f));

            RectTransform matchTrack = CreateRect("MatchTrack", _root);
            Anchor(matchTrack, new Vector2(0f, 0.07f), new Vector2(1f, 0.17f), new Vector2(12f, 0f), new Vector2(-12f, 0f));
            Image matchTrackImage = matchTrack.gameObject.AddComponent<Image>();
            matchTrackImage.color = trackColor;
            RectTransform matchFill = CreateRect("MatchFill", matchTrack);
            _matchFill = matchFill.gameObject.AddComponent<Image>();
            _matchFill.color = inputColor;
            Anchor(matchFill, Vector2.zero, new Vector2(0f, 1f), Vector2.zero, Vector2.zero);

            RectTransform progressTrack = CreateRect("DecodeProgressTrack", _root);
            Anchor(progressTrack, new Vector2(0f, 0.00f), new Vector2(1f, 0.045f), new Vector2(12f, 0f), new Vector2(-12f, 0f));
            Image progressTrackImage = progressTrack.gameObject.AddComponent<Image>();
            progressTrackImage.color = trackColor;
            RectTransform progressFill = CreateRect("DecodeProgressFill", progressTrack);
            _progressFill = progressFill.gameObject.AddComponent<Image>();
            _progressFill.color = targetColor;
            Anchor(progressFill, Vector2.zero, new Vector2(0f, 1f), Vector2.zero, Vector2.zero);

            _matchValue = CreateText("MatchValue", _root, 9f, TextAlignmentOptions.MidlineRight);
            Anchor(_matchValue.rectTransform, new Vector2(0.62f, 0.17f), new Vector2(1f, 0.30f), new Vector2(0f, 0f), new Vector2(-12f, 0f));
        }

        private void CreateLabel(string name, string text, float yMin, float yMax)
        {
            TextMeshProUGUI label = CreateText(name, _root, 9f, TextAlignmentOptions.MidlineLeft);
            label.SetText(text);
            Anchor(label.rectTransform, new Vector2(0f, yMin), new Vector2(0.38f, yMax), new Vector2(12f, 0f), Vector2.zero);
        }

        private Slider CreateSlider(string name, float yMin, float yMax, Color fillColor)
        {
            RectTransform root = CreateRect(name, _root);
            Anchor(root, new Vector2(0f, yMin), new Vector2(0.58f, yMax), new Vector2(12f, 0f), Vector2.zero);
            Image background = root.gameObject.AddComponent<Image>();
            background.color = trackColor;

            RectTransform fill = CreateRect("Fill", root);
            Image fillImage = fill.gameObject.AddComponent<Image>();
            fillImage.color = fillColor;
            Anchor(fill, Vector2.zero, new Vector2(0.5f, 1f), Vector2.zero, Vector2.zero);

            Slider slider = root.gameObject.AddComponent<Slider>();
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = 0.5f;
            slider.wholeNumbers = false;
            slider.direction = Slider.Direction.LeftToRight;
            slider.targetGraphic = background;
            slider.fillRect = fill;
            return slider;
        }

        private TextMeshProUGUI CreateText(string name, RectTransform parent, float size, TextAlignmentOptions alignment)
        {
            RectTransform rect = CreateRect(name, parent);
            TextMeshProUGUI label = rect.gameObject.AddComponent<TextMeshProUGUI>();
            label.fontSize = size;
            label.color = textColor;
            label.alignment = alignment;
            label.overflowMode = TextOverflowModes.Truncate;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.raycastTarget = false;
            return label;
        }

        private RectTransform CreateRect(string name, RectTransform parent)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            return rect;
        }

        private void UpdateFrequencyText(float carrierHz)
        {
            int length = 0;
            length = AppendInt(_frequencyBuffer, length, Mathf.RoundToInt(carrierHz));
            length = Append(_frequencyBuffer, length, 'H');
            length = Append(_frequencyBuffer, length, 'z');
            SetBuffer(_frequencyValue, _frequencyBuffer, length);
        }

        private void UpdatePhaseText(float phase01)
        {
            int length = 0;
            length = AppendInt(_phaseBuffer, length, Mathf.RoundToInt(phase01 * 360f));
            length = Append(_phaseBuffer, length, 'd');
            length = Append(_phaseBuffer, length, 'e');
            length = Append(_phaseBuffer, length, 'g');
            SetBuffer(_phaseValue, _phaseBuffer, length);
        }

        private void UpdateMatchText(float match01)
        {
            int length = 0;
            length = AppendInt(_matchBuffer, length, Mathf.RoundToInt(match01 * 100f));
            length = Append(_matchBuffer, length, '%');
            SetBuffer(_matchValue, _matchBuffer, length);
        }

        private void UpdateMatch(float match01)
        {
            if (_matchFill == null)
                return;

            RectTransform rect = _matchFill.rectTransform;
            rect.anchorMax = new Vector2(math.saturate(match01), 1f);
            rect.offsetMax = Vector2.zero;
        }

        private void UpdateProgress(float progress01)
        {
            if (_progressFill == null)
                return;

            RectTransform rect = _progressFill.rectTransform;
            rect.anchorMax = new Vector2(math.saturate(progress01), 1f);
            rect.offsetMax = Vector2.zero;
        }

        private void TryRegister()
        {
            if (_registered || !Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            GlobalRegistry.RegisterUpdatable(this, PriorityLayer.UI);
            _registered = GlobalRegistry.Updatables.Contains(this);
        }

        private void TryUnregister()
        {
            if (!_registered)
                return;

            GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.UI);
            _registered = false;
        }

        private static void SetBuffer(TextMeshProUGUI label, char[] buffer, int length)
        {
            if (label == null || buffer == null)
                return;

            int safeLength = Mathf.Clamp(length, 0, buffer.Length);
            label.SetCharArray(buffer, 0, safeLength);
            label.UpdateVertexData(TMP_VertexDataUpdateFlags.All);
        }

        private static int Append(char[] buffer, int index, char value)
        {
            if (buffer == null || index >= buffer.Length)
                return buffer != null ? buffer.Length : 0;

            if (index < 0)
                index = 0;

            buffer[index] = value;
            return index + 1;
        }

        private static int AppendInt(char[] buffer, int index, int value)
        {
            if (buffer == null || index >= buffer.Length)
                return buffer != null ? buffer.Length : 0;

            if (!value.TryFormat(new Span<char>(buffer, index, buffer.Length - index), out int written))
                return index;

            return index + written;
        }

        private static void Anchor(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }
    }
}
