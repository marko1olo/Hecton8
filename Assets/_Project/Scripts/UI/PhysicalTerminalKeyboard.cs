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
    public sealed class PhysicalTerminalKeyboard : MonoBehaviour, IPanelInteractable, IKinematicTerminalButtonResolver, IGlobalRegistryHotSwapListener
    {
        private const int KeyColumnCount = 10;
        private const int KeyRowCount = 4;
        private const int KeyCount = KeyColumnCount * KeyRowCount;
        private const int TextCapacity = 128;
        private const byte TerminalHapticPriority = 1;
        private const byte RightMotorMask = 0b0010;
        private const float DefaultReferenceWidth = 512f;
        private const float DefaultReferenceHeight = 256f;
        private const float DefaultKeyboardMinX = 16f;
        private const float DefaultKeyboardMinY = 42f;
        private const float DefaultKeyboardWidth = 480f;
        private const float DefaultKeyboardHeight = 168f;
        private const float DefaultPressAudioPitch = 1f;

        // COLD ALLOC: char[40] - fixed physical keyboard key map - owner: PhysicalTerminalKeyboard
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

        // COLD ALLOC: char[128] - terminal keyboard input buffer - owner: PhysicalTerminalKeyboard
        private readonly char[] _textBuffer = new char[TextCapacity];

        private int _textLength;
        private int _hoverKeyIndex = -1;
        private int _pressedKeyIndex = -1;
        private byte _highlightState;
        private float _keyWidth;
        private float _keyHeight;
        private float _invKeyWidth;
        private float _invKeyHeight;
        private IAudioService _cachedAudioService;
        private bool _layoutCached;
        private bool _hotSwapListenerRegistered;

        public int TextLength => _textLength;

        private void OnEnable()
        {
            CacheAudioService(GlobalRegistry.Audio);
            TryRegisterHotSwapListener();
        }

        private void OnDisable()
        {
            TryUnregisterHotSwapListener();
        }

        private void OnDestroy()
        {
            TryUnregisterHotSwapListener();
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot == GlobalRegistryServiceSlot.Audio)
                CacheAudioService(currentService as IAudioService);
        }

        public void ReceiveCanvasInput(in DiegeticPanelInputEvent inputEvent)
        {
            if (inputEvent.PanelId != panelId)
                return;

            DiegeticPanelInputEventType pointerAction = DiegeticPanelInputEvent.ResolvePrimaryPointerAction(inputEvent.EventType);
            if (pointerAction == DiegeticPanelInputEventType.None)
                return;

            CacheLayout();
            int keyIndex = ResolveKeyIndex(inputEvent.CanvasHitPoint);
            _hoverKeyIndex = keyIndex;
            bool pressed = pointerAction == DiegeticPanelInputEventType.Down;
            bool held = pointerAction == DiegeticPanelInputEventType.Hold;
            _pressedKeyIndex = pressed || held ? keyIndex : -1;
            _highlightState = ResolveButtonHighlightState(keyIndex >= 0, pressed || held);

            if (pointerAction != DiegeticPanelInputEventType.Down || keyIndex < 0 || keyIndex >= KeyCount)
                return;

            ApplyKey(s_keyMap[keyIndex]);
            if (outputLabel != null)
                outputLabel.SetCharArray(_textBuffer, 0, _textLength);

            if (emitPressHaptics)
            {
                ToolHapticsRuntime.TryEnqueueSinusoidalCommand(
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
            float2 safeKeyboardMin = ResolveSafeKeyboardMin();
            canvasSnapPosition = new float2(
                safeKeyboardMin.x + (column + 0.5f) * _keyWidth,
                safeKeyboardMin.y + (row + 0.5f) * _keyHeight);
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
            if (!math.all(math.isfinite(canvasPosition)))
                return -1;

            float2 safeKeyboardMin = ResolveSafeKeyboardMin();
            float2 safeKeyboardSize = ResolveSafeKeyboardSize();
            float x = canvasPosition.x - safeKeyboardMin.x;
            float y = canvasPosition.y - safeKeyboardMin.y;
            if (x < 0f || y < 0f || x > safeKeyboardSize.x || y > safeKeyboardSize.y)
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

            float2 safeKeyboardSize = ResolveSafeKeyboardSize();
            float safeWidth = safeKeyboardSize.x;
            float safeHeight = safeKeyboardSize.y;
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
            IAudioService audio = ResolveAudioService();
            if (!emitPressAudio || pressAudioEventId == 0u || audio == null)
                return;

            Vector3 sourcePosition = audioOrigin != null ? audioOrigin.position : transform.position;
            if (!math.all(math.isfinite((float3)sourcePosition)))
                return;

            AudioEvent audioEvent = new AudioEvent(
                pressAudioEventId,
                sourcePosition,
                ResolveSafePressAudioVolume(),
                ResolveSafePressAudioPitch());
            audio.QueueAudioEvent(in audioEvent);
        }

        private void CacheAudioService(IAudioService audioService)
        {
            _cachedAudioService = IsAudioServiceUsable(audioService) ? audioService : null;
        }

        private IAudioService ResolveAudioService()
        {
            IAudioService audioService = _cachedAudioService;
            if (IsAudioServiceUsable(audioService))
                return audioService;

            _cachedAudioService = null;
            return null;
        }

        private static bool IsAudioServiceUsable(IAudioService audioService)
        {
            if (audioService == null || !audioService.IsInitialized)
                return false;

            if (audioService is Behaviour behaviour)
                return behaviour != null && behaviour.isActiveAndEnabled;

            return true;
        }

        private static float SanitizeFinite(float value, float fallback)
        {
            return math.isfinite(value) ? value : fallback;
        }

        private float2 ResolveSafeReferenceResolution()
        {
            return new float2(
                math.max(1f, SanitizeFinite(referenceResolution.x, DefaultReferenceWidth)),
                math.max(1f, SanitizeFinite(referenceResolution.y, DefaultReferenceHeight)));
        }

        private float2 ResolveSafeKeyboardMin()
        {
            return new float2(
                SanitizeFinite(keyboardMin.x, DefaultKeyboardMinX),
                SanitizeFinite(keyboardMin.y, DefaultKeyboardMinY));
        }

        private float2 ResolveSafeKeyboardSize()
        {
            float2 safeReferenceResolution = ResolveSafeReferenceResolution();
            return new float2(
                math.max(1f, SanitizeFinite(keyboardSize.x, math.min(DefaultKeyboardWidth, safeReferenceResolution.x))),
                math.max(1f, SanitizeFinite(keyboardSize.y, math.min(DefaultKeyboardHeight, safeReferenceResolution.y))));
        }

        private float ResolveSafePressAudioVolume()
        {
            return math.saturate(SanitizeFinite(pressAudioVolume, 0f));
        }

        private float ResolveSafePressAudioPitch()
        {
            return math.clamp(
                SanitizeFinite(pressAudioPitch, DefaultPressAudioPitch),
                0.25f,
                2.5f);
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

#if UNITY_EDITOR
        private void OnValidate()
        {
            float2 safeReferenceResolution = ResolveSafeReferenceResolution();
            referenceResolution = new Vector2(safeReferenceResolution.x, safeReferenceResolution.y);
            float2 safeKeyboardMin = ResolveSafeKeyboardMin();
            keyboardMin = new Vector2(safeKeyboardMin.x, safeKeyboardMin.y);
            float2 safeKeyboardSize = ResolveSafeKeyboardSize();
            keyboardSize = new Vector2(safeKeyboardSize.x, safeKeyboardSize.y);
            pressAudioVolume = ResolveSafePressAudioVolume();
            pressAudioPitch = ResolveSafePressAudioPitch();
        }
#endif
    }
}
