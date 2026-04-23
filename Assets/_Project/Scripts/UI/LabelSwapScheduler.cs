using Hecton.Localization;
using TMPro;
using UnityEngine;

namespace Hecton8.UI
{
    /// <summary>
    /// Fixed-capacity staged TMP font swap queue with a hard 18-label drain budget.
    /// </summary>
    public sealed class LabelSwapScheduler
    {
        public const int MaxPerTick = 18;

        // COLD ALLOC: TMP_TextEntry[512] — staged TMP swap ring buffer — owner: LabelSwapScheduler
        private readonly TMP_TextEntry[] _pending = new TMP_TextEntry[512];
        private int _head;
        private int _tail;
        private int _count;

        /// <summary>
        /// Pending entry count.
        /// </summary>
        public int PendingCount => _count;

        /// <summary>
        /// True when staged font swaps remain queued.
        /// </summary>
        public bool HasPending => _count > 0;

        /// <summary>
        /// Reset the scheduler.
        /// </summary>
        public void Clear()
        {
            for (int i = 0; i < _pending.Length; i++)
                _pending[i] = default;

            _head = 0;
            _tail = 0;
            _count = 0;
        }

        /// <summary>
        /// Queue one TMP entry for staged update.
        /// </summary>
        public bool Enqueue(TMP_TextEntry entry)
        {
            if (_count >= _pending.Length || entry.Text == null)
                return false;

            _pending[_tail] = entry;
            _tail++;
            if (_tail >= _pending.Length)
                _tail = 0;

            _count++;
            return true;
        }

        /// <summary>
        /// Drain up to 18 labels this tick.
        /// </summary>
        public int DrainTick(TMP_FontAsset newFont, Material newMaterial)
        {
            int processed = 0;
            while (_count > 0 && processed < MaxPerTick)
            {
                TMP_TextEntry entry = _pending[_head];
                _pending[_head] = default;
                _head++;
                if (_head >= _pending.Length)
                    _head = 0;

                _count--;
                processed++;
                ApplyEntry(entry, newFont, newMaterial);
            }

            return processed;
        }

        private static void ApplyEntry(TMP_TextEntry entry, TMP_FontAsset newFont, Material newMaterial)
        {
            TMP_Text text = entry.Text;
            if (text == null)
                return;

            if (newFont != null)
                text.font = newFont;

            if (newMaterial != null)
                text.fontSharedMaterial = newMaterial;

            if (!entry.IsUserInput && entry.HasLocalizationKey)
            {
                bool rtl = LocalizedMeasurementFormatter.IsRightToLeft(LocRegistry.ActiveLanguage);
                char[] buffer;
                int length;
                text.isRightToLeftText = false;
                if (rtl)
                    LocRegistry.TryGetVisualBuffer(entry.LocalizationKeyHash, out buffer, out length);
                else
                    LocRegistry.TryGetRawBuffer(entry.LocalizationKeyHash, out buffer, out length);
                text.SetCharArray(buffer, 0, length);
            }

            text.SetMaterialDirty();
            text.SetVerticesDirty();
        }
    }
}
