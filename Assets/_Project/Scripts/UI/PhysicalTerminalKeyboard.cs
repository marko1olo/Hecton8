using Hecton8.Interaction;
using Hecton8.Tools;
using Hecton8.Core;
using TMPro;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.UI
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/UI/Physical Terminal Keyboard")]
    public sealed class PhysicalTerminalKeyboard : MonoBehaviour, IPanelInteractable, IKinematicTerminalButtonResolver
    {
        private const int KeyColumnCount = 10;
        private const int KeyRowCount = 4;
        private const int KeyCount = KeyColumnCount * KeyRowCount;
        private const int TextCapacity = 128;
        private const byte TerminalHapticPriority = 1;
        private const byte RightMotorMask = 0b0010;

        // COLD ALLOC: char[40] — fixed physical keyboard key map — owner: PhysicalTerminalKeyboard
        private static readonly char[] s_keyMap =
        {
            '1', '2', '3', '4', '5', '6', '7', '8', '9', '0',
            'Q', 'W', 'E', 'R', 'T', 'Y', 'U', 'I', 'O', 'P',
            'A', 'S', 'D', 'F', 'G', 'H', 'J', 'K', 'L', '\b',
            'Z', 'X', 'C', 'V', 'B', 'N', 'M', ' ', '.', '\n'
        };

        [SerializeField] private int panelId = 1;
        [SerializeField] private TMP_Text outputLabel;
        [SerializeField] private Vector2 referenceResolution = new Vector2(512f, 256f);
        [SerializeField] private Vector2 keyboardMin = new Vector2(16f, 42f);
        [SerializeField] private Vector2 keyboardSize = new Vector2(480f, 168f);
        [SerializeField] private bool emitPressHaptics = true;
        [SerializeField] private bool emitPressAudio = true;
        [SerializeField] private uint pressAudioEventId;
        [SerializeField] private Transform audioOrigin;
        [SerializeField, Range(0f, 1f)] private float pressAudioVolume = 0.35f;
        [SerializeField, Range(0.25f, 2.5f)] private float pressAudioPitch = 1f;

        // COLD ALLOC: char[128] — terminal keyboard input buffer — owner: PhysicalTerminalKeyboard
        private readonly char[] _textBuffer = new char[TextCapacity];

        private int _textLength;
        private int _hoverKeyIndex = -1;
        private int _pressedKeyIndex = -1;
        private byte _highlightState;
        private float _keyWidth;
        private float _keyHeight;
        private float _invKeyWidth;
        private float _invKeyHeight;
        private bool _layoutCached;

        public int TextLength => _textLength;

        public void ReceiveCanvasInput(in DiegeticPanelInputEvent inputEvent)
        {
            if (inputEvent.PanelId != panelId)
                return;

            CacheLayout();
            int keyIndex = ResolveKeyIndex(inputEvent.CanvasHitPoint);
            _hoverKeyIndex = keyIndex;
            bool pressed = (inputEvent.EventType & DiegeticPanelInputEventType.Down) != 0;
            bool held = (inputEvent.EventType & DiegeticPanelInputEventType.Hold) != 0;
            _pressedKeyIndex = pressed || held ? keyIndex : -1;
            _highlightState = ResolveButtonHighlightState(keyIndex >= 0, pressed || held);

            if (!pressed || keyIndex < 0 || keyIndex >= KeyCount)
                return;

            ApplyKey(s_keyMap[keyIndex]);
            if (outputLabel != null)
                outputLabel.SetCharArray(_textBuffer, 0, _textLength);

            if (emitPressHaptics)
            {
                ToolHapticsRuntime.EnqueueSinusoidalCommand(
                    0.04f,
                    0.22f,
                    0.05f,
                    54f,
                    TerminalHapticPriority,
                    RightMotorMask);
            }

            QueuePressAudio();
        }

        public bool TryResolveButtonSnap(in KinematicTerminalPointerState pointer, out float2 canvasSnapPosition)
        {
            canvasSnapPosition = pointer.CanvasPosition;
            if (pointer.PanelId != panelId)
                return false;

            CacheLayout();
            int keyIndex = ResolveKeyIndex(pointer.CanvasPosition);
            if (keyIndex < 0)
                return false;

            int row = keyIndex / KeyColumnCount;
            int column = keyIndex - row * KeyColumnCount;
            canvasSnapPosition = new float2(
                keyboardMin.x + (column + 0.5f) * _keyWidth,
                keyboardMin.y + (row + 0.5f) * _keyHeight);
            return true;
        }

        public void ClearText()
        {
            for (int i = 0; i < _textLength; i++)
                _textBuffer[i] = '\0';

            _textLength = 0;
            if (outputLabel != null)
                outputLabel.SetCharArray(_textBuffer, 0, 0);
        }

        private void ApplyKey(char key)
        {
            if (key == '\b')
            {
                if (_textLength > 0)
                {
                    _textLength--;
                    _textBuffer[_textLength] = '\0';
                }
                return;
            }

            if (_textLength >= TextCapacity)
                return;

            _textBuffer[_textLength++] = key;
        }

        private int ResolveKeyIndex(float2 canvasPosition)
        {
            float x = canvasPosition.x - keyboardMin.x;
            float y = canvasPosition.y - keyboardMin.y;
            if (x < 0f || y < 0f || x > keyboardSize.x || y > keyboardSize.y)
                return -1;

            int column = (int)math.floor(x * _invKeyWidth);
            int row = (int)math.floor(y * _invKeyHeight);
            if (column < 0 || column >= KeyColumnCount || row < 0 || row >= KeyRowCount)
                return -1;

            return row * KeyColumnCount + column;
        }

        private void CacheLayout()
        {
            if (_layoutCached)
                return;

            float safeWidth = math.max(1f, math.isfinite(keyboardSize.x) ? keyboardSize.x : referenceResolution.x);
            float safeHeight = math.max(1f, math.isfinite(keyboardSize.y) ? keyboardSize.y : referenceResolution.y);
            _keyWidth = safeWidth * (1f / KeyColumnCount);
            _keyHeight = safeHeight * (1f / KeyRowCount);
            _invKeyWidth = math.rcp(math.max(0.0001f, _keyWidth));
            _invKeyHeight = math.rcp(math.max(0.0001f, _keyHeight));
            _layoutCached = true;
        }

        private static byte ResolveButtonHighlightState(bool hovered, bool pressed)
        {
            return (byte)(math.select(0, 1, hovered) | math.select(0, 2, pressed));
        }

        private void QueuePressAudio()
        {
            IAudioService audio = Hecton8.Audio.SpatialAudioManager.ActiveRuntimeInstance;
            if (!emitPressAudio || pressAudioEventId == 0u || audio == null || !audio.IsInitialized)
                return;

            Vector3 sourcePosition = audioOrigin != null ? audioOrigin.position : transform.position;
            if (!math.all(math.isfinite((float3)sourcePosition)))
                return;

            AudioEvent audioEvent = new AudioEvent(
                pressAudioEventId,
                sourcePosition,
                math.saturate(pressAudioVolume),
                math.clamp(pressAudioPitch, 0.25f, 2.5f));
            audio.QueueAudioEvent(in audioEvent);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            pressAudioVolume = math.saturate(pressAudioVolume);
            pressAudioPitch = math.clamp(pressAudioPitch, 0.25f, 2.5f);
        }
#endif
    }
}
