using Hecton8.UI;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Scripting;

namespace Hecton8.Core.Diagnostics.Visuals
{
    [Preserve]
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/Diagnostics/Architect Eye PDA Command Console")]
    public sealed class ArchitectEyePdaCommandConsole : MonoBehaviour, IPanelInteractable
    {
        private const int KeyColumnCount = 10;
        private const int KeyRowCount = 4;
        private const int KeyCount = KeyColumnCount * KeyRowCount;
        private const int TextCapacity = 96;
        private const float DefaultKeyboardWidth = 480f;
        private const float DefaultKeyboardHeight = 168f;

        private static readonly char[] s_keyMap =
        {
            '1', '2', '3', '4', '5', '6', '7', '8', '9', '0',
            'Q', 'W', 'E', 'R', 'T', 'Y', 'U', 'I', 'O', 'P',
            'A', 'S', 'D', 'F', 'G', 'H', 'J', 'K', 'L', '\b',
            '+', '-', 'X', ' ', 'B', 'N', 'M', '.', '/', '\n'
        };

        [SerializeField] private int panelId = 1;
        [SerializeField] private ArchitectEyeVisualizer visualizer;
        [SerializeField] private Vector2 keyboardMin = new Vector2(16f, 42f);
        [SerializeField] private Vector2 keyboardSize = new Vector2(480f, 168f);

        private readonly char[] _textBuffer = new char[TextCapacity]; // COLD ALLOC: char[96] - fixed PDA command input buffer - owner: ArchitectEyePdaCommandConsole
        private int _textLength;
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

            if (DiegeticPanelInputEvent.ResolvePrimaryPointerAction(inputEvent.EventType) != DiegeticPanelInputEventType.Down)
                return;

            CacheLayout();
            int keyIndex = ResolveKeyIndex(inputEvent.CanvasHitPoint);
            if ((uint)keyIndex >= (uint)KeyCount)
                return;

            ApplyKey(s_keyMap[keyIndex]);
        }

        public void ClearText()
        {
            for (int i = 0; i < _textLength; i++)
                _textBuffer[i] = '\0';
            _textLength = 0;
        }

        public bool SubmitCurrent()
        {
            ArchitectEyeVisualizer target = ResolveTarget();
            if (target == null || _textLength <= 0)
                return false;

            bool accepted = target.SubmitCommand(new System.ReadOnlySpan<char>(_textBuffer, 0, _textLength));
            if (accepted)
                ClearText();
            return accepted;
        }

        private ArchitectEyeVisualizer ResolveTarget()
        {
            return visualizer;
        }

        private void ApplyKey(char key)
        {
            if (key == '\n')
            {
                SubmitCurrent();
                return;
            }

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
            _keyWidth = safeKeyboardSize.x * (1f / KeyColumnCount);
            _keyHeight = safeKeyboardSize.y * (1f / KeyRowCount);
            _invKeyWidth = SafeRcp(_keyWidth);
            _invKeyHeight = SafeRcp(_keyHeight);
            _layoutCached = true;
        }

        private float2 ResolveSafeKeyboardMin()
        {
            return new float2(
                SanitizeFinite(keyboardMin.x, 16f),
                SanitizeFinite(keyboardMin.y, 42f));
        }

        private float2 ResolveSafeKeyboardSize()
        {
            return new float2(
                math.max(1f, SanitizeFinite(keyboardSize.x, DefaultKeyboardWidth)),
                math.max(1f, SanitizeFinite(keyboardSize.y, DefaultKeyboardHeight)));
        }

        private static float SanitizeFinite(float value, float fallback)
        {
            return math.isfinite(value) ? value : fallback;
        }

        private static float SafeRcp(float value)
        {
            return value > 0.0001f && math.isfinite(value) ? 1f / value : 0f;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            _layoutCached = false;
        }
#endif
    }
}
