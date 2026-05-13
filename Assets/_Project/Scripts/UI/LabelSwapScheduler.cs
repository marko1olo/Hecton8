using Hecton.Localization;
using TMPro;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.UI
{
    /// <summary>
    /// Fixed-capacity staged TMP font swap queue with a hard 18-label drain budget.
    /// </summary>
    public sealed class LabelSwapScheduler
    {
        public const int MaxPerTick = 18;

        // COLD ALLOC: PendingSwap[512] - staged TMP swap ring buffer with optional prefetched UTF-8 slice - owner: LabelSwapScheduler
        private readonly PendingSwap[] _pending = new PendingSwap[512];
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

            _pending[_tail] = new PendingSwap
            {
                Entry = entry,
                Utf8Slice = new int2(-1, 0),
                HasPrefetchedSlice = 0
            };
            _tail++;
            if (_tail >= _pending.Length)
                _tail = 0;

            _count++;
            return true;
        }

        /// <summary>
        /// Applies prefetched UTF-8 byte slices to the pending queue without changing queue order.
        /// </summary>
        public void ApplyPrefetchSlices(NativeArray<int2> slices, int count)
        {
            if (!slices.IsCreated || count <= 0 || _count <= 0)
                return;

            int applyCount = count < _count ? count : _count;
            for (int i = 0; i < applyCount; i++)
            {
                int pendingIndex = _head + i;
                if (pendingIndex >= _pending.Length)
                    pendingIndex -= _pending.Length;

                PendingSwap pending = _pending[pendingIndex];
                pending.Utf8Slice = slices[i];
                pending.HasPrefetchedSlice = 1;
                _pending[pendingIndex] = pending;
            }
        }

        /// <summary>
        /// Drain up to 18 labels this tick.
        /// </summary>
        public int DrainTick(TMP_FontAsset newFont, Material newMaterial)
        {
            int processed = 0;
            while (_count > 0 && processed < MaxPerTick)
            {
                PendingSwap pending = _pending[_head];
                _pending[_head] = default;
                _head++;
                if (_head >= _pending.Length)
                    _head = 0;

                _count--;
                processed++;
                ApplyEntry(in pending, newFont, newMaterial);
            }

            return processed;
        }

        private static void ApplyEntry(in PendingSwap pending, TMP_FontAsset newFont, Material newMaterial)
        {
            TMP_TextEntry entry = pending.Entry;
            TMP_Text text = entry.Text;
            if (text == null)
                return;

            if (newFont != null)
                text.font = newFont;

            if (newMaterial != null)
                text.fontSharedMaterial = newMaterial;

            if (!entry.IsUserInput && entry.HasLocalizationKey)
            {
                text.isRightToLeftText = false;
                char[] buffer;
                int length;
                if (pending.HasPrefetchedSlice != 0)
                    LocRegistry.TryGetVisualBufferFromUtf8Slice(entry.LocalizationKeyHash, pending.Utf8Slice, out buffer, out length);
                else
                    LocRegistry.TryGetVisualBufferFromUtf8(entry.LocalizationKeyHash, out buffer, out length);

                text.SetCharArray(buffer, 0, length);
            }

            text.SetMaterialDirty();
            text.SetVerticesDirty();
        }

        private struct PendingSwap
        {
            public TMP_TextEntry Entry;
            public int2 Utf8Slice;
            public byte HasPrefetchedSlice;
        }
    }
}
