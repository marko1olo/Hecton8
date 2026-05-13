using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Hecton8.Core;
using Hecton8.Data;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
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
                    HashUtf16CodeUnit(ref hash, value[i]);

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
        /// Compute a byte-wise FNV-1a hash for already encoded UTF-8/static-data slices.
        /// </summary>
        public static uint ComputeAscii(ReadOnlySpan<byte> value)
        {
            if (value.Length == 0)
                return 0u;

            unchecked
            {
                uint hash = FnvOffsetBasis;
                for (int i = 0; i < value.Length; i++)
                {
                    hash ^= value[i];
                    hash *= FnvPrime;
                }

                return hash;
            }
        }

        /// <summary>
        /// Compute the same UTF-16 FNV-1a value as the char-span hash from UTF-8 bytes.
        /// </summary>
        public static uint ComputeUtf8AsUtf16(ReadOnlySpan<byte> value)
        {
            if (value.Length == 0)
                return 0u;

            unchecked
            {
                uint hash = FnvOffsetBasis;
                int cursor = 0;
                while (cursor < value.Length)
                {
                    if (TryReadUtf8Scalar(value, cursor, out int scalar, out int consumed))
                    {
                        if (scalar <= 0xFFFF)
                        {
                            HashUtf16CodeUnit(ref hash, (char)scalar);
                        }
                        else
                        {
                            int supplementary = scalar - 0x10000;
                            HashUtf16CodeUnit(ref hash, (char)(0xD800 + (supplementary >> 10)));
                            HashUtf16CodeUnit(ref hash, (char)(0xDC00 + (supplementary & 0x3FF)));
                        }

                        cursor += consumed;
                        continue;
                    }

                    HashUtf16CodeUnit(ref hash, '\uFFFD');
                    cursor++;
                }

                return hash;
            }
        }

        private static void HashUtf16CodeUnit(ref uint hash, char current)
        {
            hash ^= (byte)current;
            hash *= FnvPrime;
            hash ^= (byte)(current >> 8);
            hash *= FnvPrime;
        }

        private static bool TryReadUtf8Scalar(
            ReadOnlySpan<byte> value,
            int index,
            out int scalar,
            out int consumed)
        {
            scalar = 0;
            consumed = 1;
            byte lead = value[index];
            if (lead < 0x80)
            {
                scalar = lead;
                return true;
            }

            if ((lead & 0xE0) == 0xC0)
            {
                if (index + 1 >= value.Length || !IsUtf8Continuation(value[index + 1]))
                    return false;

                scalar = ((lead & 0x1F) << 6) | (value[index + 1] & 0x3F);
                if (scalar < 0x80)
                    return false;

                consumed = 2;
                return true;
            }

            if ((lead & 0xF0) == 0xE0)
            {
                if (index + 2 >= value.Length ||
                    !IsUtf8Continuation(value[index + 1]) ||
                    !IsUtf8Continuation(value[index + 2]))
                {
                    return false;
                }

                scalar = ((lead & 0x0F) << 12) |
                         ((value[index + 1] & 0x3F) << 6) |
                         (value[index + 2] & 0x3F);
                if (scalar < 0x800 || (scalar >= 0xD800 && scalar <= 0xDFFF))
                    return false;

                consumed = 3;
                return true;
            }

            if ((lead & 0xF8) == 0xF0)
            {
                if (index + 3 >= value.Length ||
                    !IsUtf8Continuation(value[index + 1]) ||
                    !IsUtf8Continuation(value[index + 2]) ||
                    !IsUtf8Continuation(value[index + 3]))
                {
                    return false;
                }

                scalar = ((lead & 0x07) << 18) |
                         ((value[index + 1] & 0x3F) << 12) |
                         ((value[index + 2] & 0x3F) << 6) |
                         (value[index + 3] & 0x3F);
                if (scalar < 0x10000 || scalar > 0x10FFFF)
                    return false;

                consumed = 4;
                return true;
            }

            return false;
        }

        private static bool IsUtf8Continuation(byte value)
        {
            return (value & 0xC0) == 0x80;
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
        private const int MaxDecodedGlyphs = 1024;
        private const int EllipsisGlyphCount = 3;
        private const int BabelTelemetryFrameCapacity = 300;
        private static readonly uint _missingKeyWarningHash = unchecked((uint)LocHash.Compute("LocRegistry.MissingKey"));

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
        private static readonly byte[] _missingHashUtf8 =
        {
            (byte)'[', (byte)'M', (byte)'I', (byte)'S', (byte)'S', (byte)'I', (byte)'N', (byte)'G',
            (byte)'_', (byte)'H', (byte)'A', (byte)'S', (byte)'H', (byte)']'
        };

        private static GameLanguage _activeLanguage = GameLanguage.English;
        private static NativeParallelHashMap<uint, int2> _utf8Offsets;
        private static NativeArray<byte> _utf8Bytes;
        private static NativeArray<BabelTelemetryEntry> _telemetryFrames;
        private static JobHandle _utf8ReaderHandle;
        private static int _utf8ByteLength;
        private static int _telemetryWriteIndex;
        private static bool _utf8OffsetsRegistered;
        private static bool _utf8BytesRegistered;
        private static bool _telemetryRegistered;
        private static bool _utf8ReaderHandleActive;

        [ThreadStatic] private static char[] _decodeBuffer;

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
            DisposeUtf8State();
            DisposeTelemetryState();
        }

        /// <summary>
        /// Reload the runtime hash registry from the string-backed localization owner.
        /// </summary>
        public static void Reload(Dictionary<GameLanguage, Dictionary<string, string>> tables, GameLanguage activeLanguage)
        {
            EnsureTelemetryState();
            _activeLanguage = activeLanguage;
            _core.Clear();
            _world.Clear();
            _narrative.Clear();

            if (tables == null || tables.Count == 0)
            {
                RebuildUtf8Lookup(tables, activeLanguage);
                RecordTelemetry(0u, _utf8ByteLength, _utf8Offsets.IsCreated ? _utf8Offsets.Count() : 0, BabelTelemetryFlags.EmptyReload);
                return;
            }

            LoadLanguage(tables, activeLanguage);
            if (activeLanguage != GameLanguage.English)
                LoadLanguage(tables, GameLanguage.English);

            RebuildUtf8Lookup(tables, activeLanguage);
            RecordTelemetry(0u, _utf8ByteLength, _utf8Offsets.IsCreated ? _utf8Offsets.Count() : 0, BabelTelemetryFlags.Reload);
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
        /// Returns the localized text length for a key without allocating a string.
        /// </summary>
        public static int GetLength(int keyHash)
        {
            return TryResolveEntry(keyHash, out LocEntry entry)
                ? entry.RawLength
                : _missingKeyChars.Length;
        }

        /// <summary>
        /// Returns the localized text length for a uint FNV key without allocating a string.
        /// </summary>
        public static int GetLength(uint keyHash)
        {
            return GetLength(unchecked((int)keyHash));
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
                LogMissingKeyOnce(keyHash);
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

        /// <summary>
        /// Resolve localized UTF-8 bytes by FNV hash without creating a managed string.
        /// </summary>
        public static bool TryGetLocalizedSpan(uint keyHash, out ReadOnlySpan<byte> utf8Bytes)
        {
            if (_utf8Offsets.IsCreated && _utf8Offsets.TryGetValue(keyHash, out int2 slice))
            {
                if (slice.y == 0)
                {
                    utf8Bytes = ReadOnlySpan<byte>.Empty;
                    RecordTelemetry(keyHash, slice.x, 0, BabelTelemetryFlags.Hit);
                    return true;
                }

                if (IsValidUtf8Slice(slice))
                {
                    unsafe
                    {
                        byte* basePtr = (byte*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(_utf8Bytes);
                        utf8Bytes = new ReadOnlySpan<byte>(basePtr + slice.x, slice.y);
                    }

                    RecordTelemetry(keyHash, slice.x, slice.y, BabelTelemetryFlags.Hit);
                    return true;
                }

                DumpTelemetryForCorruption(keyHash, slice);
            }

            LogMissingKeyOnce(unchecked((int)keyHash));
            utf8Bytes = _missingHashUtf8.AsSpan();
            RecordTelemetry(keyHash, -1, _missingHashUtf8.Length, BabelTelemetryFlags.Miss);
            return false;
        }

        /// <summary>
        /// Decode a localized UTF-8 entry into a thread-local char buffer for TMP SetCharArray.
        /// </summary>
        public static bool TryGetVisualBufferFromUtf8(int keyHash, out char[] buffer, out int length)
        {
            bool found = TryGetLocalizedSpan(unchecked((uint)keyHash), out ReadOnlySpan<byte> utf8Bytes);
            return DecodeUtf8VisualBuffer(found, utf8Bytes, out buffer, out length);
        }

        /// <summary>
        /// Decode a localized UTF-8 slice that was prefetched by a Burst offset lookup job.
        /// </summary>
        public static bool TryGetVisualBufferFromUtf8Slice(int keyHash, int2 utf8Slice, out char[] buffer, out int length)
        {
            if (utf8Slice.x >= 0 && utf8Slice.y >= 0 && IsValidUtf8Slice(utf8Slice))
            {
                unsafe
                {
                    byte* basePtr = (byte*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(_utf8Bytes);
                    ReadOnlySpan<byte> utf8Bytes = new ReadOnlySpan<byte>(basePtr + utf8Slice.x, utf8Slice.y);
                    RecordTelemetry(unchecked((uint)keyHash), utf8Slice.x, utf8Slice.y, BabelTelemetryFlags.Hit);
                    return DecodeUtf8VisualBuffer(true, utf8Bytes, out buffer, out length);
                }
            }

            return TryGetVisualBufferFromUtf8(keyHash, out buffer, out length);
        }

        private static bool DecodeUtf8VisualBuffer(bool found, ReadOnlySpan<byte> utf8Bytes, out char[] buffer, out int length)
        {
            bool truncated = false;
            ReadOnlySpan<byte> decodeBytes = utf8Bytes;
            int charCount = 0;
            if (utf8Bytes.Length > 0)
            {
                charCount = Encoding.UTF8.GetCharCount(utf8Bytes);
                if (charCount > MaxDecodedGlyphs)
                {
                    int safeByteLength = ComputeUtf8TruncationByteLength(utf8Bytes, MaxDecodedGlyphs - EllipsisGlyphCount);
                    decodeBytes = safeByteLength > 0 ? utf8Bytes.Slice(0, safeByteLength) : ReadOnlySpan<byte>.Empty;
                    charCount = decodeBytes.Length == 0 ? 0 : Encoding.UTF8.GetCharCount(decodeBytes);
                    truncated = true;
                }
            }

            int required = math.max(1, charCount + (truncated ? EllipsisGlyphCount : 0));
            buffer = GetDecodeBuffer(required);
            length = decodeBytes.Length == 0 ? 0 : Encoding.UTF8.GetChars(decodeBytes, buffer.AsSpan(0, buffer.Length));

            if (found && LocalizationManager.IsRightToLeftLanguage(_activeLanguage))
                RTLProcessor.TryReverseVisualOrderInPlace(buffer, length);

            if (truncated)
                AppendEllipsis(buffer, ref length, MaxDecodedGlyphs);
            else
                TruncateGlyphsWithEllipsis(buffer, ref length, MaxDecodedGlyphs);

            return found;
        }

        /// <summary>
        /// Schedule a Burst-visible lookup pass that maps visible text hashes to byte slices.
        /// </summary>
        public static bool TryScheduleVisibleTextOffsetPrefetch(
            NativeArray<uint> visibleHashes,
            NativeArray<int2> outputSlices,
            JobHandle dependency,
            out JobHandle handle)
        {
            return TryScheduleVisibleTextOffsetPrefetch(
                visibleHashes,
                outputSlices,
                visibleHashes.IsCreated ? visibleHashes.Length : 0,
                dependency,
                out handle);
        }

        /// <summary>
        /// Schedule a Burst-visible lookup pass for a caller-specified dense prefix.
        /// </summary>
        public static bool TryScheduleVisibleTextOffsetPrefetch(
            NativeArray<uint> visibleHashes,
            NativeArray<int2> outputSlices,
            int count,
            JobHandle dependency,
            out JobHandle handle)
        {
            if (!visibleHashes.IsCreated ||
                !outputSlices.IsCreated ||
                count < 0 ||
                count > visibleHashes.Length ||
                count > outputSlices.Length ||
                !_utf8Offsets.IsCreated)
            {
                handle = dependency;
                return false;
            }

            BabelVisibleTextOffsetPrefetchJob job = new BabelVisibleTextOffsetPrefetchJob
            {
                VisibleHashes = visibleHashes,
                Offsets = _utf8Offsets,
                OutputSlices = outputSlices
            };
            handle = job.Schedule(count, 32, dependency);
            RegisterUtf8ReaderHandle(handle);
            return true;
        }

        /// <summary>
        /// Clears the registry-owned read fence after a caller has observed its prefetch job complete.
        /// </summary>
        public static void MarkVisibleTextOffsetPrefetchComplete()
        {
            if (!_utf8ReaderHandleActive || !_utf8ReaderHandle.IsCompleted)
                return;

            _utf8ReaderHandle.Complete();
            _utf8ReaderHandle = default;
            _utf8ReaderHandleActive = false;
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

        private static void RebuildUtf8Lookup(
            Dictionary<GameLanguage, Dictionary<string, string>> tables,
            GameLanguage activeLanguage)
        {
            DisposeUtf8State();
            int entryCapacity = EstimateUtf8EntryCapacity(tables, activeLanguage);
            if (entryCapacity <= 0)
                return;

            int byteCapacity = EstimateUtf8ByteCapacity(tables, activeLanguage);
            _utf8Offsets = new NativeParallelHashMap<uint, int2>(
                math.max(16, entryCapacity << 1),
                Allocator.Persistent); // COLD ALLOC: NativeParallelHashMap<uint,int2> - Babel hash to UTF-8 slice lookup - owner: LocRegistry
            NativeMemorySentinel.RegisterNativeParallelHashMap(
                _utf8Offsets,
                nameof(LocRegistry),
                nameof(_utf8Offsets),
                NativeAllocationLifetime.Session);
            _utf8OffsetsRegistered = true;

            if (byteCapacity > 0)
            {
                _utf8Bytes = new NativeArray<byte>(
                    byteCapacity,
                    Allocator.Persistent,
                    NativeArrayOptions.UninitializedMemory); // COLD ALLOC: NativeArray<byte>[byteCapacity] - Babel contiguous UTF-8 localization blob - owner: LocRegistry
                NativeMemorySentinel.RegisterNativeArray(
                    _utf8Bytes,
                    nameof(LocRegistry),
                    nameof(_utf8Bytes),
                    NativeAllocationLifetime.Session);
                _utf8BytesRegistered = true;
            }

            int writeOffset = 0;
            LoadLanguageUtf8(tables, activeLanguage, ref writeOffset);
            if (activeLanguage != GameLanguage.English)
                LoadLanguageUtf8(tables, GameLanguage.English, ref writeOffset);

            TryLoadStaticArenaUtf8(ref writeOffset);
            _utf8ByteLength = writeOffset;
        }

        private static int EstimateUtf8EntryCapacity(
            Dictionary<GameLanguage, Dictionary<string, string>> tables,
            GameLanguage activeLanguage)
        {
            int count = 0;
            AddLanguageEntryEstimate(tables, activeLanguage, ref count);
            if (activeLanguage != GameLanguage.English)
                AddLanguageEntryEstimate(tables, GameLanguage.English, ref count);

            count = SaturatingAdd(count, EstimateStaticArenaEntryCapacity());
            return count;
        }

        private static int EstimateUtf8ByteCapacity(
            Dictionary<GameLanguage, Dictionary<string, string>> tables,
            GameLanguage activeLanguage)
        {
            int bytes = 0;
            AddLanguageByteEstimate(tables, activeLanguage, ref bytes);
            if (activeLanguage != GameLanguage.English)
                AddLanguageByteEstimate(tables, GameLanguage.English, ref bytes);

            H8DataBlobDirectory directory = H8StaticDataArena.Directory;
            if (H8StaticDataArena.IsLoaded && directory.LocalizationBytes > 0u)
                bytes = SaturatingAdd(bytes, (int)math.min(directory.LocalizationBytes, int.MaxValue));

            return bytes;
        }

        private static void AddLanguageEntryEstimate(
            Dictionary<GameLanguage, Dictionary<string, string>> tables,
            GameLanguage language,
            ref int count)
        {
            if (tables == null)
                return;

            if (tables.TryGetValue(language, out Dictionary<string, string> table) && table != null)
                count = SaturatingAdd(count, table.Count);
        }

        private static void AddLanguageByteEstimate(
            Dictionary<GameLanguage, Dictionary<string, string>> tables,
            GameLanguage language,
            ref int bytes)
        {
            if (tables == null)
                return;

            if (!tables.TryGetValue(language, out Dictionary<string, string> table) || table == null)
                return;

            Dictionary<string, string>.Enumerator enumerator = table.GetEnumerator();
            while (enumerator.MoveNext())
            {
                string value = enumerator.Current.Value ?? string.Empty;
                bytes = SaturatingAdd(bytes, Encoding.UTF8.GetByteCount(value));
            }
        }

        private static void LoadLanguageUtf8(
            Dictionary<GameLanguage, Dictionary<string, string>> tables,
            GameLanguage language,
            ref int writeOffset)
        {
            if (tables == null)
                return;

            if (!tables.TryGetValue(language, out Dictionary<string, string> table) || table == null)
                return;

            Dictionary<string, string>.Enumerator enumerator = table.GetEnumerator();
            while (enumerator.MoveNext())
            {
                string key = enumerator.Current.Key;
                if (string.IsNullOrWhiteSpace(key))
                    continue;

                uint keyHash = unchecked((uint)LocHash.Compute(key));
                if (_utf8Offsets.ContainsKey(keyHash))
                    continue;

                string value = enumerator.Current.Value ?? string.Empty;
                int byteCount = Encoding.UTF8.GetByteCount(value);
                int offset = writeOffset;
                if (byteCount > 0 && !TryWriteUtf8(value, offset, byteCount))
                {
                    DumpTelemetryForCorruption(keyHash, new int2(offset, byteCount));
                    continue;
                }

                _utf8Offsets.TryAdd(keyHash, new int2(offset, byteCount));
                writeOffset = SaturatingAdd(writeOffset, byteCount);
            }
        }

        private static void TryLoadStaticArenaUtf8(ref int writeOffset)
        {
            if (!H8StaticDataArena.TryGetLocalizedUtf8Block(out ReadOnlySpan<byte> locData) || locData.Length == 0)
                return;

            int startOffset = writeOffset;
            if (!TryCopyStaticArenaBytes(locData, startOffset))
                return;

            int cursor = 0;
            while (cursor < locData.Length)
            {
                int entryStart = cursor;
                while (cursor < locData.Length && locData[cursor] != 0)
                    cursor++;

                int byteLength = cursor - entryStart;
                if (byteLength > 0)
                {
                    uint valueHash = LocHash.ComputeUtf8AsUtf16(locData.Slice(entryStart, byteLength));
                    _utf8Offsets.TryAdd(valueHash, new int2(startOffset + entryStart, byteLength));
                }

                cursor++;
            }

            TryLoadStaticArenaReferenceAliases(startOffset);
            writeOffset = SaturatingAdd(writeOffset, locData.Length);
        }

        private static void TryLoadStaticArenaReferenceAliases(int staticStartOffset)
        {
            H8StaticLocalizationCursor cursor = default;
            while (H8StaticDataArena.TryGetNextStaticLocalizationReference(ref cursor, out H8StaticLocalizationReference reference))
            {
                if (reference.KeyHash == 0u || reference.ByteLength <= 0)
                {
                    continue;
                }

                int offset = SaturatingAdd(staticStartOffset, reference.Utf8Offset);
                if (!_utf8Bytes.IsCreated ||
                    offset < staticStartOffset ||
                    offset < 0 ||
                    reference.ByteLength > _utf8Bytes.Length - offset)
                {
                    DumpTelemetryForCorruption(reference.KeyHash, new int2(offset, reference.ByteLength));
                    continue;
                }

                _utf8Offsets.TryAdd(reference.KeyHash, new int2(offset, reference.ByteLength));
            }
        }

        private static int EstimateStaticArenaEntryCapacity()
        {
            if (!H8StaticDataArena.TryGetLocalizedUtf8Block(out ReadOnlySpan<byte> locData) || locData.Length == 0)
                return 0;

            int count = 0;
            bool insideEntry = false;
            for (int i = 0; i < locData.Length; i++)
            {
                if (locData[i] == 0)
                {
                    if (insideEntry)
                    {
                        count = SaturatingAdd(count, 1);
                        insideEntry = false;
                    }

                    continue;
                }

                insideEntry = true;
            }

            if (insideEntry)
                count = SaturatingAdd(count, 1);

            count = SaturatingAdd(count, H8StaticDataArena.GetStaticLocalizationReferenceCount());
            return count;
        }

        private static unsafe bool TryWriteUtf8(string value, int offset, int byteCount)
        {
            if (byteCount <= 0)
                return true;

            if (!_utf8Bytes.IsCreated || offset < 0 || byteCount > _utf8Bytes.Length - offset)
                return false;

            fixed (char* source = value)
            {
                byte* destination = (byte*)NativeArrayUnsafeUtility.GetUnsafePtr(_utf8Bytes) + offset;
                int written = Encoding.UTF8.GetBytes(source, value.Length, destination, byteCount);
                return written == byteCount;
            }
        }

        private static unsafe bool TryCopyStaticArenaBytes(ReadOnlySpan<byte> source, int offset)
        {
            if (source.Length <= 0)
                return true;

            if (!_utf8Bytes.IsCreated || offset < 0 || source.Length > _utf8Bytes.Length - offset)
                return false;

            fixed (byte* sourcePtr = source)
            {
                byte* destination = (byte*)NativeArrayUnsafeUtility.GetUnsafePtr(_utf8Bytes) + offset;
                UnsafeUtility.MemCpy(destination, sourcePtr, source.Length);
            }

            return true;
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

        private static bool IsValidUtf8Slice(int2 slice)
        {
            return _utf8Bytes.IsCreated &&
                   slice.x >= 0 &&
                   slice.y >= 0 &&
                   slice.x <= _utf8ByteLength &&
                   slice.y <= _utf8ByteLength - slice.x &&
                   slice.y <= _utf8Bytes.Length - slice.x;
        }

        private static char[] GetDecodeBuffer(int requiredLength)
        {
            if (requiredLength <= 0)
                requiredLength = 1;

            char[] buffer = _decodeBuffer;
            if (buffer != null && buffer.Length >= requiredLength)
                return buffer;

            int capacity = 128;
            while (capacity < requiredLength)
                capacity <<= 1;

            _decodeBuffer = new char[capacity]; // COLD ALLOC: char[capacity] - thread-local Babel UTF-8 decode buffer - owner: LocRegistry
            return _decodeBuffer;
        }

        private static void TruncateGlyphsWithEllipsis(char[] buffer, ref int length, int maxGlyphs)
        {
            if (buffer == null || length <= maxGlyphs)
                return;

            int safeLength = math.max(0, maxGlyphs - EllipsisGlyphCount);
            if (safeLength > 0 && char.IsHighSurrogate(buffer[safeLength - 1]))
                safeLength--;

            length = safeLength;
            AppendEllipsis(buffer, ref length, maxGlyphs);
        }

        private static void AppendEllipsis(char[] buffer, ref int length, int maxGlyphs)
        {
            if (buffer == null || maxGlyphs < EllipsisGlyphCount)
                return;

            int safeLength = math.min(length, maxGlyphs - EllipsisGlyphCount);
            if (safeLength > 0 && char.IsHighSurrogate(buffer[safeLength - 1]))
                safeLength--;

            buffer[safeLength++] = '.';
            buffer[safeLength++] = '.';
            buffer[safeLength++] = '.';
            length = safeLength;
        }

        private static int ComputeUtf8TruncationByteLength(ReadOnlySpan<byte> utf8Bytes, int maxChars)
        {
            if (utf8Bytes.Length == 0 || maxChars <= 0)
                return 0;

            int cursor = 0;
            int chars = 0;
            while (cursor < utf8Bytes.Length)
            {
                byte leading = utf8Bytes[cursor];
                int sequenceLength;
                int charUnits;
                if (leading < 0x80)
                {
                    sequenceLength = 1;
                    charUnits = 1;
                }
                else if ((leading & 0xE0) == 0xC0)
                {
                    sequenceLength = 2;
                    charUnits = 1;
                }
                else if ((leading & 0xF0) == 0xE0)
                {
                    sequenceLength = 3;
                    charUnits = 1;
                }
                else if ((leading & 0xF8) == 0xF0)
                {
                    sequenceLength = 4;
                    charUnits = 2;
                }
                else
                {
                    break;
                }

                if (sequenceLength > utf8Bytes.Length - cursor || chars + charUnits > maxChars)
                    break;

                bool continuationValid = true;
                for (int i = 1; i < sequenceLength; i++)
                {
                    if ((utf8Bytes[cursor + i] & 0xC0) != 0x80)
                    {
                        continuationValid = false;
                        break;
                    }
                }

                if (!continuationValid)
                    break;

                cursor += sequenceLength;
                chars += charUnits;
            }

            return cursor;
        }

        private static int SaturatingAdd(int left, int right)
        {
            if (right <= 0)
                return left;

            return left > int.MaxValue - right ? int.MaxValue : left + right;
        }

        private static void EnsureTelemetryState()
        {
            if (_telemetryFrames.IsCreated)
                return;

            _telemetryFrames = new NativeArray<BabelTelemetryEntry>(
                BabelTelemetryFrameCapacity,
                Allocator.Persistent,
                NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<BabelTelemetryEntry>[300] - Babel black-box circular telemetry - owner: LocRegistry
            NativeMemorySentinel.RegisterNativeArray(
                _telemetryFrames,
                nameof(LocRegistry),
                nameof(_telemetryFrames),
                NativeAllocationLifetime.Session);
            _telemetryRegistered = true;
            _telemetryWriteIndex = 0;
        }

        private static void RecordTelemetry(uint keyHash, int offset, int length, ushort flags)
        {
            if (!_telemetryFrames.IsCreated)
                return;

            int slot = _telemetryWriteIndex;
            _telemetryWriteIndex++;
            if (_telemetryWriteIndex >= BabelTelemetryFrameCapacity)
                _telemetryWriteIndex = 0;

            _telemetryFrames[slot] = new BabelTelemetryEntry
            {
                Frame = Time.frameCount,
                KeyHash = keyHash,
                Offset = offset,
                Length = length,
                Language = (ushort)_activeLanguage,
                Flags = flags
            };
        }

        private static unsafe void DumpTelemetryForCorruption(uint keyHash, int2 badSlice)
        {
            RecordTelemetry(keyHash, badSlice.x, badSlice.y, BabelTelemetryFlags.CorruptSlice);
            if (!_telemetryFrames.IsCreated)
                return;

            try
            {
                string docsPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Docs", "AgentLogs"));
                Directory.CreateDirectory(docsPath);
                string dumpPath = Path.Combine(docsPath, "Dump_UI_LOCALIZATION_BABEL.bin");
                using FileStream stream = new FileStream(dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read);
                int byteCount = UnsafeUtility.SizeOf<BabelTelemetryEntry>() * _telemetryFrames.Length;
                byte* source = (byte*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(_telemetryFrames);
                stream.Write(new ReadOnlySpan<byte>(source, byteCount));
            }
            catch (Exception)
            {
                // Crash-path telemetry must never create a second failure.
            }
        }

        private static void DisposeUtf8State()
        {
            CompleteUtf8ReadersForMutation();

            if (_utf8Bytes.IsCreated)
            {
                if (_utf8BytesRegistered)
                {
                    NativeMemorySentinel.UnregisterNativeArray(_utf8Bytes);
                    _utf8BytesRegistered = false;
                }

                _utf8Bytes.Dispose();
                _utf8Bytes = default;
            }

            if (_utf8Offsets.IsCreated)
            {
                if (_utf8OffsetsRegistered)
                {
                    NativeMemorySentinel.UnregisterNativeParallelHashMap(nameof(LocRegistry), nameof(_utf8Offsets));
                    _utf8OffsetsRegistered = false;
                }

                _utf8Offsets.Dispose();
                _utf8Offsets = default;
            }

            _utf8ByteLength = 0;
        }

        private static void RegisterUtf8ReaderHandle(JobHandle handle)
        {
            _utf8ReaderHandle = _utf8ReaderHandleActive
                ? JobHandle.CombineDependencies(_utf8ReaderHandle, handle)
                : handle;
            _utf8ReaderHandleActive = true;
        }

        private static void CompleteUtf8ReadersForMutation()
        {
            if (!_utf8ReaderHandleActive)
                return;

            _utf8ReaderHandle.Complete();
            _utf8ReaderHandle = default;
            _utf8ReaderHandleActive = false;
        }

        private static void DisposeTelemetryState()
        {
            if (!_telemetryFrames.IsCreated)
                return;

            if (_telemetryRegistered)
            {
                NativeMemorySentinel.UnregisterNativeArray(_telemetryFrames);
                _telemetryRegistered = false;
            }

            _telemetryFrames.Dispose();
            _telemetryFrames = default;
            _telemetryWriteIndex = 0;
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

            Hecton8.Core.GlobalTelemetryBus.PublishPerformanceWarning(
                _missingKeyWarningHash,
                unchecked((uint)keyHash),
                (float)_activeLanguage);
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

        [StructLayout(LayoutKind.Sequential, Pack = 4)]
        private struct BabelTelemetryEntry
        {
            public int Frame;
            public uint KeyHash;
            public int Offset;
            public int Length;
            public ushort Language;
            public ushort Flags;
        }

        private static class BabelTelemetryFlags
        {
            public const ushort Hit = 1;
            public const ushort Miss = 2;
            public const ushort Reload = 4;
            public const ushort EmptyReload = 8;
            public const ushort CorruptSlice = 16;
        }

        [BurstCompile(FloatPrecision.Low, FloatMode.Fast)]
        private struct BabelVisibleTextOffsetPrefetchJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<uint> VisibleHashes;
            [ReadOnly] public NativeParallelHashMap<uint, int2> Offsets;
            [WriteOnly] public NativeArray<int2> OutputSlices;

            public void Execute(int index)
            {
                uint hash = VisibleHashes[index];
                OutputSlices[index] = Offsets.TryGetValue(hash, out int2 slice)
                    ? slice
                    : new int2(-1, 0);
            }
        }

        [BurstCompile(FloatPrecision.Low, FloatMode.Fast)]
        public struct BabelRtlReverseJob : IJobParallelFor
        {
            public NativeArray<char> Chars;
            public int Length;

            public void Execute(int index)
            {
                int mirror = Length - 1 - index;
                if (index >= mirror)
                    return;

                char left = Chars[index];
                Chars[index] = Chars[mirror];
                Chars[mirror] = left;
            }
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
