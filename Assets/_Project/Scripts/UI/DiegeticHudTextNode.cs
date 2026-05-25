using System;
using System.Runtime.CompilerServices;
using Hecton8.Core;
using TMPro;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.UI
{
    /// <summary>
    /// Zero-GC TMP lane for diegetic visor labels. Runtime writes must enter through spans.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(TMP_Text), typeof(HectonTextNode))]
    public sealed class DiegeticHudTextNode : MonoBehaviour
    {
        private const int DefaultCapacity = 256;
        private const uint FnvOffset = 2166136261u;
        private const uint FnvPrime = 16777619u;

        [SerializeField] private TMP_Text target;
        [SerializeField, Min(8)] private int capacity = DefaultCapacity;
        [SerializeField] private bool registerWithTextRegistry = true;

        // COLD ALLOC: char[capacity] - persistent TMP SetCharArray backing store - owner: DiegeticHudTextNode
        private char[] _buffer;
        private uint _lastHash;
        private int _lastLength = -1;
        private int _lastOxygenPercent = int.MinValue;

        public TMP_Text Target => target;
        public int Capacity => _buffer != null ? _buffer.Length : 0;

        private void Awake()
        {
            EnsureInitialized();
        }

        private void OnEnable()
        {
            EnsureInitialized();
            if (registerWithTextRegistry && target != null && target.TryGetComponent(out HectonTextNode _))
                TMP_TextRegistry.EnsureRegistered(target);
        }

        public bool SetSpan(ReadOnlySpan<char> value)
        {
            if (!EnsureInitialized() || value.Length > _buffer.Length)
                return false;

            uint hash = Hash(value);
            if (_lastLength == value.Length && _lastHash == hash)
                return true;

            value.CopyTo(_buffer.AsSpan(0, value.Length));
            target.SetCharArray(_buffer, 0, value.Length);
            _lastLength = value.Length;
            _lastHash = hash;
            return true;
        }

        public bool SetFormattedInt(ReadOnlySpan<char> prefix, int value, ReadOnlySpan<char> suffix)
        {
            if (!EnsureInitialized())
                return false;

            Span<char> destination = _buffer.AsSpan();
            int cursor = 0;
            if (!ZeroGCFormatter.AppendToSpan(prefix, destination, ref cursor) ||
                !ZeroGCFormatter.FastIntToChars(value, destination, ref cursor) ||
                !ZeroGCFormatter.AppendToSpan(suffix, destination, ref cursor))
            {
                return false;
            }

            return Commit(cursor);
        }

        public bool SetFormattedFloat(ReadOnlySpan<char> prefix, float value, int decimals, ReadOnlySpan<char> suffix)
        {
            if (!EnsureInitialized())
                return false;

            Span<char> destination = _buffer.AsSpan();
            int cursor = 0;
            if (!ZeroGCFormatter.AppendToSpan(prefix, destination, ref cursor) ||
                !ZeroGCFormatter.FastFloatToChars(value, decimals, destination, ref cursor) ||
                !ZeroGCFormatter.AppendToSpan(suffix, destination, ref cursor))
            {
                return false;
            }

            return Commit(cursor);
        }

        public bool SetOxygenPercent(int oxygenPercent)
        {
            oxygenPercent = math.clamp(oxygenPercent, 0, 100);
            if (oxygenPercent == _lastOxygenPercent)
                return true;

            if (!EnsureInitialized())
                return false;

            Span<char> destination = _buffer.AsSpan();
            int cursor = 0;
            if (!ZeroGCFormatter.AppendToSpan("O2 ".AsSpan(), destination, ref cursor) ||
                !ZeroGCFormatter.FastIntToChars(oxygenPercent, destination, ref cursor) ||
                !ZeroGCFormatter.AppendChar('%', destination, ref cursor))
            {
                return false;
            }

            if (!Commit(cursor))
                return false;

            _lastOxygenPercent = oxygenPercent;
            return true;
        }

        private bool Commit(int length)
        {
            if (!EnsureInitialized() || length < 0 || length > _buffer.Length)
                return false;

            ReadOnlySpan<char> value = _buffer.AsSpan(0, length);
            uint hash = Hash(value);
            if (_lastLength == length && _lastHash == hash)
                return true;

            target.SetCharArray(_buffer, 0, length);
            _lastLength = length;
            _lastHash = hash;
            return true;
        }

        private bool EnsureInitialized()
        {
            if (target == null && !TryGetComponent(out target))
                return false;

            int resolvedCapacity = math.max(8, capacity);
            if (_buffer == null || _buffer.Length != resolvedCapacity)
                _buffer = new char[resolvedCapacity]; // COLD ALLOC: char[resolvedCapacity] - rebuilt authoring-sized text buffer - owner: DiegeticHudTextNode

            return target != null;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint Hash(ReadOnlySpan<char> value)
        {
            uint hash = FnvOffset;
            for (int i = 0; i < value.Length; i++)
            {
                hash ^= value[i];
                hash *= FnvPrime;
            }

            return hash;
        }
    }
}
