using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hecton.Localization
{
    /// <summary>
    /// Localization residency layer used by the zero-GC runtime registry.
    /// </summary>
    public enum LocLayer
    {
        Core = 0,
        World = 1,
        Narrative = 2
    }

    /// <summary>
    /// FNV-1a hashing for runtime localization keys and deterministic UI registry ids.
    /// </summary>
    public static class LocHash
    {
        public const uint FnvOffsetBasis = 2166136261u;
        public const uint FnvPrime = 16777619u;

        /// <summary>
        /// Compute a stable FNV-1a hash for the provided string.
        /// </summary>
        public static int Compute(string value)
        {
            return string.IsNullOrEmpty(value)
                ? 0
                : Compute(value.AsSpan());
        }

        /// <summary>
        /// Compute a stable FNV-1a hash for the provided span.
        /// </summary>
        public static int Compute(ReadOnlySpan<char> value)
        {
            if (value.Length == 0)
                return 0;

            unchecked
            {
                uint hash = FnvOffsetBasis;
                for (int i = 0; i < value.Length; i++)
                {
                    char current = value[i];
                    hash ^= (byte)current;
                    hash *= FnvPrime;
                    hash ^= (byte)(current >> 8);
                    hash *= FnvPrime;
                }

                return (int)hash;
            }
        }
    }

    /// <summary>
    /// Zero-allocation right-to-left visual reorderer for Arabic and Hebrew glyph runs.
    /// </summary>
    public static class RTLProcessor
    {
        [ThreadStatic] private static char[] _stagingBuffer;

        /// <summary>
        /// True when the provided span contains Arabic or Hebrew glyphs.
        /// </summary>
        public static bool ContainsRightToLeftGlyph(ReadOnlySpan<char> logical)
        {
            for (int i = 0; i < logical.Length; i++)
            {
                if (IsRightToLeftGlyph(logical[i]))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Reorders logical RTL text into visual order without heap allocation.
        /// </summary>
        public static ReadOnlySpan<char> ToVisualOrder(ReadOnlySpan<char> logical)
        {
            if (logical.Length == 0)
                return ReadOnlySpan<char>.Empty;

            ToVisualBuffer(logical, out char[] buffer, out int length);
            return buffer.AsSpan(0, length);
        }

        /// <summary>
        /// Reorders logical RTL text into a thread-static char buffer consumable by TMP SetCharArray.
        /// </summary>
        public static void ToVisualBuffer(ReadOnlySpan<char> logical, out char[] buffer, out int length)
        {
            if (logical.Length == 0)
            {
                buffer = Array.Empty<char>();
                length = 0;
                return;
            }

            buffer = GetBuffer(logical.Length);
            logical.CopyTo(buffer.AsSpan(0, logical.Length));
            length = logical.Length;
            ReverseRightToLeftRunInPlace(buffer, length);
            ReverseLeftToRightRuns(buffer, length);
        }

        private static char[] GetBuffer(int requiredLength)
        {
            char[] buffer = _stagingBuffer;
            if (buffer != null && buffer.Length >= requiredLength)
                return buffer;

            int capacity = buffer == null ? 128 : buffer.Length;
            while (capacity < requiredLength)
                capacity <<= 1;

            _stagingBuffer = new char[capacity]; // COLD ALLOC: char[capacity] — RTL staging buffer per thread — owner: RTLProcessor
            return _stagingBuffer;
        }

        private static void ReverseRightToLeftRunInPlace(char[] buffer, int length)
        {
            int left = 0;
            int right = length - 1;
            while (left < right)
            {
                if (IsCombiningMark(buffer[right]))
                {
                    right--;
                    continue;
                }

                if (IsCombiningMark(buffer[left]))
                {
                    left++;
                    continue;
                }

                char swap = buffer[left];
                buffer[left] = buffer[right];
                buffer[right] = swap;
                left++;
                right--;
            }
        }

        private static void ReverseLeftToRightRuns(char[] buffer, int length)
        {
            int cursor = 0;
            while (cursor < length)
            {
                if (!IsLeftToRightRunCharacter(buffer[cursor]))
                {
                    cursor++;
                    continue;
                }

                int start = cursor;
                cursor++;
                while (cursor < length && IsLeftToRightRunCharacter(buffer[cursor]))
                    cursor++;

                int left = start;
                int right = cursor - 1;
                while (left < right)
                {
                    char swap = buffer[left];
                    buffer[left] = buffer[right];
                    buffer[right] = swap;
                    left++;
                    right--;
                }
            }
        }

        private static bool IsLeftToRightRunCharacter(char value)
        {
            return (value >= '0' && value <= '9') ||
                   (value >= 'A' && value <= 'Z') ||
                   (value >= 'a' && value <= 'z') ||
                   value == '.' ||
                   value == ',' ||
                   value == ':' ||
                   value == ';' ||
                   value == '/' ||
                   value == '\\' ||
                   value == '-' ||
                   value == '+' ||
                   value == '%' ||
                   value == '(' ||
                   value == ')' ||
                   value == '[' ||
                   value == ']';
        }

        private static bool IsRightToLeftGlyph(char value)
        {
            return (value >= '\u0590' && value <= '\u08FF') ||
                   (value >= '\uFB1D' && value <= '\uFEFC');
        }

        private static bool IsCombiningMark(char value)
        {
            return (value >= '\u0591' && value <= '\u05BD') ||
                   value == '\u05BF' ||
                   (value >= '\u05C1' && value <= '\u05C7') ||
                   (value >= '\u0610' && value <= '\u061A') ||
                   (value >= '\u064B' && value <= '\u065F') ||
                   value == '\u0670' ||
                   (value >= '\u06D6' && value <= '\u06ED') ||
                   (value >= '\u08D3' && value <= '\u08FF');
        }
    }

    /// <summary>
    /// Zero-allocation runtime localization registry keyed by FNV-1a hashes.
    /// </summary>
    public static class LocRegistry
    {
        private const string MissingKeyLiteral = "[ERR_MISSING_KEY]";

        // COLD ALLOC: Dictionary[512] — core localization pool keyed by FNV-1a hash — owner: LocRegistry
        private static readonly LocPool _core = new LocPool(512);
        // COLD ALLOC: Dictionary[1024] — world localization pool keyed by FNV-1a hash — owner: LocRegistry
        private static readonly LocPool _world = new LocPool(2048);
        // COLD ALLOC: Dictionary[2048] — narrative localization pool keyed by FNV-1a hash — owner: LocRegistry
        private static readonly LocPool _narrative = new LocPool(16384);
        // COLD ALLOC: HashSet[64] — log-once missing localization key guard — owner: LocRegistry
        private static readonly HashSet<int> _missingKeysLogged = new HashSet<int>(64);
        // COLD ALLOC: char[17] — static missing localization literal — owner: LocRegistry
        private static readonly char[] _missingKeyChars = MissingKeyLiteral.ToCharArray();

        private static GameLanguage _activeLanguage = GameLanguage.English;

        /// <summary>
        /// Active language loaded into the runtime hash registry.
        /// </summary>
        public static GameLanguage ActiveLanguage => _activeLanguage;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _activeLanguage = GameLanguage.English;
            _core.Clear();
            _world.Clear();
            _narrative.Clear();
            _missingKeysLogged.Clear();
        }

        /// <summary>
        /// Reload the runtime hash registry from the string-backed localization owner.
        /// </summary>
        public static void Reload(Dictionary<GameLanguage, Dictionary<string, string>> tables, GameLanguage activeLanguage)
        {
            _activeLanguage = activeLanguage;
            _core.Clear();
            _world.Clear();
            _narrative.Clear();

            if (tables == null || tables.Count == 0)
                return;

            LoadLanguage(tables, activeLanguage);
            if (activeLanguage != GameLanguage.English)
                LoadLanguage(tables, GameLanguage.English);
        }

        /// <summary>
        /// Resolve a localized entry as a span without heap allocation.
        /// </summary>
        public static ReadOnlySpan<char> Resolve(int keyHash)
        {
            return ResolveRaw(keyHash);
        }

        /// <summary>
        /// Resolve the logical/raw localized entry as a span without heap allocation.
        /// </summary>
        public static ReadOnlySpan<char> ResolveRaw(int keyHash)
        {
            return TryResolveEntry(keyHash, out LocEntry entry)
                ? entry.RawBuffer.AsSpan(0, entry.RawLength)
                : _missingKeyChars.AsSpan();
        }

        /// <summary>
        /// Resolve the visual-order localized entry as a span without heap allocation.
        /// </summary>
        public static ReadOnlySpan<char> ResolveVisual(int keyHash)
        {
            if (!TryResolveEntry(keyHash, out LocEntry entry))
                return _missingKeyChars.AsSpan();

            return entry.HasVisualBuffer
                ? entry.VisualBuffer.AsSpan(0, entry.VisualLength)
                : entry.RawBuffer.AsSpan(0, entry.RawLength);
        }

        /// <summary>
        /// Resolve a raw char buffer for TMP SetCharArray without heap allocation.
        /// </summary>
        public static bool TryGetRawBuffer(int keyHash, out char[] buffer, out int length)
        {
            if (TryResolveEntry(keyHash, out LocEntry entry))
            {
                buffer = entry.RawBuffer;
                length = entry.RawLength;
                return true;
            }

            LogMissingKeyOnce(keyHash);
            buffer = _missingKeyChars;
            length = _missingKeyChars.Length;
            return false;
        }

        /// <summary>
        /// Resolve a visual-order char buffer for TMP SetCharArray without heap allocation.
        /// </summary>
        public static bool TryGetVisualBuffer(int keyHash, out char[] buffer, out int length)
        {
            if (TryResolveEntry(keyHash, out LocEntry entry))
            {
                if (entry.HasVisualBuffer)
                {
                    buffer = entry.VisualBuffer;
                    length = entry.VisualLength;
                    return true;
                }

                buffer = entry.RawBuffer;
                length = entry.RawLength;
                return true;
            }

            LogMissingKeyOnce(keyHash);
            buffer = _missingKeyChars;
            length = _missingKeyChars.Length;
            return false;
        }

        private static void LoadLanguage(
            Dictionary<GameLanguage, Dictionary<string, string>> tables,
            GameLanguage language)
        {
            if (!tables.TryGetValue(language, out Dictionary<string, string> table) || table == null)
                return;

            Dictionary<string, string>.Enumerator enumerator = table.GetEnumerator();
            while (enumerator.MoveNext())
            {
                string key = enumerator.Current.Key;
                if (string.IsNullOrWhiteSpace(key))
                    continue;

                int keyHash = LocHash.Compute(key);
                LocPool pool = ResolvePool(ClassifyLayer(key));
                if (pool.ContainsKey(keyHash))
                    continue;

                string rawValue = enumerator.Current.Value ?? string.Empty;
                LocEntry entry = CreateEntry(language, rawValue);
                pool.Set(keyHash, entry);
            }
        }

        private static LocEntry CreateEntry(GameLanguage language, string value)
        {
            string safeValue = value ?? string.Empty;
            char[] rawChars = safeValue.ToCharArray();
            if (!RequiresVisualCache(language, safeValue.AsSpan()))
                return new LocEntry(rawChars, rawChars.Length, null, 0);

            RTLProcessor.ToVisualBuffer(safeValue.AsSpan(), out char[] stagingBuffer, out int visualLength);
            char[] visualChars = new char[visualLength]; // COLD ALLOC: char[visualLength] — persisted RTL visual cache for localization entry — owner: LocRegistry
            stagingBuffer.AsSpan(0, visualLength).CopyTo(visualChars);
            return new LocEntry(rawChars, rawChars.Length, visualChars, visualLength);
        }

        private static bool RequiresVisualCache(GameLanguage language, ReadOnlySpan<char> value)
        {
            return LocalizationManager.IsRightToLeftLanguage(language) ||
                   RTLProcessor.ContainsRightToLeftGlyph(value);
        }

        private static LocPool ResolvePool(LocLayer layer)
        {
            switch (layer)
            {
                case LocLayer.World:
                    return _world;

                case LocLayer.Narrative:
                    return _narrative;

                default:
                    return _core;
            }
        }

        private static LocLayer ClassifyLayer(string key)
        {
            if (key.StartsWith("WORLD_", StringComparison.Ordinal) ||
                key.StartsWith("DEPTH_ZONE_", StringComparison.Ordinal) ||
                key.StartsWith("RELAY_", StringComparison.Ordinal))
            {
                return LocLayer.World;
            }

            if (key.StartsWith("AUDIOLOG_", StringComparison.Ordinal) ||
                key.StartsWith("PDA_", StringComparison.Ordinal) ||
                key.StartsWith("LORE_", StringComparison.Ordinal) ||
                key.StartsWith("MADNESS_", StringComparison.Ordinal))
            {
                return LocLayer.Narrative;
            }

            return LocLayer.Core;
        }

        private static bool TryResolveEntry(int keyHash, out LocEntry entry)
        {
            if (_core.TryGet(keyHash, out entry))
                return true;

            if (_world.TryGet(keyHash, out entry))
                return true;

            if (_narrative.TryGet(keyHash, out entry))
                return true;

            LogMissingKeyOnce(keyHash);
            entry = default;
            return false;
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogMissingKeyOnce(int keyHash)
        {
            if (!_missingKeysLogged.Add(keyHash))
                return;

            Debug.LogWarning($"[LOC-REGISTRY] Missing localization hash 0x{keyHash:X8} for {_activeLanguage}.");
        }

        private readonly struct LocEntry
        {
            public LocEntry(char[] rawBuffer, int rawLength, char[] visualBuffer, int visualLength)
            {
                RawBuffer = rawBuffer;
                RawLength = rawLength;
                VisualBuffer = visualBuffer;
                VisualLength = visualLength;
            }

            public char[] RawBuffer { get; }
            public int RawLength { get; }
            public char[] VisualBuffer { get; }
            public int VisualLength { get; }
            public bool HasVisualBuffer => VisualBuffer != null && VisualLength > 0;
        }

        private sealed class LocPool
        {
            private readonly Dictionary<int, LocEntry> _entries;

            public LocPool(int capacity)
            {
                _entries = new Dictionary<int, LocEntry>(capacity);
            }

            public bool ContainsKey(int keyHash)
            {
                return _entries.ContainsKey(keyHash);
            }

            public void Set(int keyHash, LocEntry entry)
            {
                _entries[keyHash] = entry;
            }

            public bool TryGet(int keyHash, out LocEntry entry)
            {
                return _entries.TryGetValue(keyHash, out entry);
            }

            public void Clear()
            {
                _entries.Clear();
            }
        }
    }
}
