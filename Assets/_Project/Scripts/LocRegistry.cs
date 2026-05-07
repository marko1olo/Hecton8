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

        /// <summary>
        /// Compute a byte-wise ASCII FNV-1a hash for authored identifiers that are specified as one-byte symbols.
        /// </summary>
        public static uint ComputeAscii(string value)
        {
            return string.IsNullOrEmpty(value)
                ? 0u
                : ComputeAscii(value.AsSpan());
        }

        /// <summary>
        /// Compute a byte-wise ASCII FNV-1a hash for authored identifiers that are specified as one-byte symbols.
        /// </summary>
        public static uint ComputeAscii(ReadOnlySpan<char> value)
        {
            if (value.Length == 0)
                return 0u;

            unchecked
            {
                uint hash = FnvOffsetBasis;
                for (int i = 0; i < value.Length; i++)
                {
                    hash ^= (byte)value[i];
                    hash *= FnvPrime;
                }

                return hash;
            }
        }

        /// <summary>
        /// Compute a byte-wise ASCII FNV-1a hash while folding A-Z to a-z without allocating.
        /// </summary>
        public static int ComputeAsciiLowerInvariant(string value)
        {
            return string.IsNullOrEmpty(value)
                ? 0
                : unchecked((int)ComputeAsciiLowerInvariant(value.AsSpan()));
        }

        /// <summary>
        /// Compute a byte-wise ASCII FNV-1a hash while folding A-Z to a-z without allocating.
        /// </summary>
        public static uint ComputeAsciiLowerInvariant(ReadOnlySpan<char> value)
        {
            if (value.Length == 0)
                return 0u;

            unchecked
            {
                uint hash = FnvOffsetBasis;
                for (int i = 0; i < value.Length; i++)
                {
                    char current = value[i];
                    if ((uint)(current - 'A') <= 'Z' - 'A')
                        current = (char)(current + ('a' - 'A'));

                    hash ^= (byte)current;
                    hash *= FnvPrime;
                }

                return hash;
            }
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
        /// Resolve the localized entry as a span without heap allocation.
        /// </summary>
        public static ReadOnlySpan<char> ResolveVisual(int keyHash)
        {
            ReadOnlySpan<char> raw = ResolveRaw(keyHash);
            return LocalizationManager.IsRightToLeftLanguage(_activeLanguage)
                ? RTLProcessor.ToVisualOrder(raw)
                : raw;
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
        /// Resolve a localized char buffer for TMP SetCharArray without heap allocation.
        /// </summary>
        public static bool TryGetVisualBuffer(int keyHash, out char[] buffer, out int length)
        {
            if (!TryResolveEntry(keyHash, out LocEntry entry))
            {
                buffer = _missingKeyChars;
                length = _missingKeyChars.Length;
                return false;
            }

            if (!LocalizationManager.IsRightToLeftLanguage(_activeLanguage))
            {
                buffer = entry.RawBuffer;
                length = entry.RawLength;
                return true;
            }

            return RTLProcessor.TryGetVisualBuffer(entry.RawBuffer.AsSpan(0, entry.RawLength), out buffer, out length);
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
                LocEntry entry = CreateEntry(rawValue);
                pool.Set(keyHash, entry);
            }
        }

        private static LocEntry CreateEntry(string value)
        {
            string safeValue = value ?? string.Empty;
            char[] rawChars = safeValue.ToCharArray();
            return new LocEntry(rawChars, rawChars.Length);
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
            public LocEntry(char[] rawBuffer, int rawLength)
            {
                RawBuffer = rawBuffer;
                RawLength = rawLength;
            }

            public char[] RawBuffer { get; }
            public int RawLength { get; }
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
