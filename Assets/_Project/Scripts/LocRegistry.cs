using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using System.Text;
using Hecton8.Core;
using Hecton8.Core.Data;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Memory;
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

    [StructLayout(LayoutKind.Sequential, Size = 16)]
    public struct LocalizationEntryDTO
    {
        public uint StringHash;
        public uint ByteOffset;
        public uint ByteLength;
        public uint _pad0;
    }

    [StructLayout(LayoutKind.Sequential, Size = 16)]
    public struct SubtitleCommandDTO
    {
        public uint SpeakerHash;
        public uint TextHash;
        public float Duration;
        public uint _pad0;
    }

    [StructLayout(LayoutKind.Sequential, Size = 16)]
    public struct SubtitleStateDTO
    {
        public uint SpeakerHash;
        public uint TextHash;
        public float TimeRemaining;
        public ushort VisibleCharacters;
        public ushort Flags;
    }

    [StructLayout(LayoutKind.Sequential, Size = 24)]
    public struct BabelFormatArgs
    {
        public int Value0;
        public int Value1;
        public int Value2;
        public int Value3;
        public byte Count;
        public byte _pad0;
        public ushort _pad1;
        public uint _pad2;

        public static BabelFormatArgs None()
        {
            return default;
        }

        public static BabelFormatArgs One(int value0)
        {
            return new BabelFormatArgs
            {
                Value0 = value0,
                Count = 1
            };
        }

        public static BabelFormatArgs Two(int value0, int value1)
        {
            return new BabelFormatArgs
            {
                Value0 = value0,
                Value1 = value1,
                Count = 2
            };
        }

        public static BabelFormatArgs Four(int value0, int value1, int value2, int value3)
        {
            return new BabelFormatArgs
            {
                Value0 = value0,
                Value1 = value1,
                Value2 = value2,
                Value3 = value3,
                Count = 4
            };
        }

        public bool TryGet(int index, out int value)
        {
            value = 0;
            if ((uint)index >= Count || (uint)index >= 4u)
                return false;

            switch (index)
            {
                case 0:
                    value = Value0;
                    return true;
                case 1:
                    value = Value1;
                    return true;
                case 2:
                    value = Value2;
                    return true;
                default:
                    value = Value3;
                    return true;
            }
        }
    }

    [StructLayout(LayoutKind.Sequential, Size = 16)]
    public struct MockTranslationRequestSignal
    {
        public uint StringHash;
        public uint LocaleHash;
        public uint OutputHandle;
        public uint _pad0;
    }

    [StructLayout(LayoutKind.Sequential, Size = 16)]
    public struct MockUiRefreshSignal
    {
        public uint PanelHash;
        public uint ReasonHash;
        public ushort DirtyFlags;
        public ushort _pad0;
        public uint _pad1;
    }

    [StructLayout(LayoutKind.Sequential, Size = 16)]
    public struct MockTextMeshProText
    {
        public uint InstanceHash;
        public uint TextHash;
        public ushort MaxVisibleCharacters;
        public ushort Flags;
        public uint _pad0;
    }

    [StructLayout(LayoutKind.Sequential, Size = 16)]
    public struct LocalizationLanguageChangedSignal : ISignal
    {
        public uint LanguageHash;
        public uint Revision;
        public ushort Language;
        public ushort Flags;
        public uint _pad0;
    }

    [StructLayout(LayoutKind.Sequential, Size = 32)]
    public struct BabelDictionaryStage
    {
        public IntPtr Destination;
        public int ByteLength;
        public uint Generation;
        public int BufferId;
        public ushort Language;
        public ushort Flags;
        public int SourceByteLength;
        public uint _pad1;
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
        private const float SlowDecodeDumpThresholdMs = 0.5f;
        private const uint SlowSearchDumpThresholdNs = 100000u;
        private const BufferID BabelUtf8BlobBufferId = BufferID.BabelUtf8Blob;
        private const BufferID BabelTelemetryBufferId = BufferID.BabelTelemetryRing;
        private const BufferID BabelStagedLocaleBufferId = BufferID.BabelStagedLocale;
        private const BufferID BabelIndexTableBufferId = BufferID.BabelIndexTable;
        private const BufferID BabelDecryptionMaskBufferId = BufferID.BabelDecryptionMask;
        private const BufferID BabelOverrideCsvScratchBufferId = BufferID.BabelOverrideCsvScratch;
        private const BufferID BabelErrorUtf8BufferId = BufferID.BabelErrorUtf8;
        private const ulong BabelOverrideMutationGuardMask = 1UL << 27;
        private const int MaxBabelDictionaryBytes = 16 * 1024 * 1024;
        private const int OverrideCsvScratchBytes = 1024 * 1024;
        private const int BabelDictionaryHeaderBytes = 32;
        private const int BabelDictionaryEntryBytes = 16;
        private static readonly uint _missingKeyWarningHash = unchecked((uint)LocHash.Compute("LocRegistry.MissingKey"));
        private static readonly uint _emergencyMockErrorHash = unchecked((uint)LocHash.Compute("ERROR"));

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
        private const int ErrorUtf8Length = 5;

        private static GameLanguage _activeLanguage = GameLanguage.English;
        private static NativeArray<LocalizationEntryDTO> _utf8Index;
        private static NativeArray<byte> _utf8Bytes;
        private static NativeArray<byte> _errorUtf8;
        private static NativeArray<byte> _decryptionMask;
        private static NativeArray<byte> _overrideCsvScratch;
        private static NativeArray<BabelTelemetryEntry> _telemetryFrames;
        private static IDataVault _babelVault;
        private static IDataVault _stagedLocaleVault;
        private static VaultBufferHandle<byte> _utf8BytesHandle;
        private static VaultBufferHandle<byte> _stagedLocaleBytesHandle;
        private static VaultBufferHandle<LocalizationEntryDTO> _utf8IndexHandle;
        private static VaultBufferHandle<byte> _errorUtf8Handle;
        private static VaultBufferHandle<byte> _decryptionMaskHandle;
        private static VaultBufferHandle<byte> _overrideCsvScratchHandle;
        private static VaultBufferHandle<BabelTelemetryEntry> _telemetryFramesHandle;
        private static JobHandle _utf8ReaderHandle;
        private static NativeArray<byte> _stagedLocaleBytes;
        private static int _utf8IndexLength;
        private static int _utf8ByteLength;
        private static int _stagedLocaleByteLength;
        private static int _stagedLocaleSourceByteLength;
        private static int _telemetryWriteIndex;
        private static int _telemetryFrameIndex = -1;
        private static int _translationsThisFrame;
        private static int _bufferPoolLeasesActive;
        private static uint _lookupSearchNsThisFrame;
        private static uint _missingHashCountThisFrame;
        private static uint _csvOverrideAppliedThisFrame;
        private static uint _csvOverrideRejectedThisFrame;
        private static uint _stagedLocaleGeneration;
        private static uint _languageSignalRevision;
        private static bool _utf8IndexRegistered;
        private static bool _utf8BytesRegistered;
        private static bool _errorUtf8Registered;
        private static bool _decryptionMaskRegistered;
        private static bool _overrideCsvScratchRegistered;
        private static bool _telemetryRegistered;
        private static bool _utf8IndexVaultBacked;
        private static bool _utf8BytesVaultBacked;
        private static bool _errorUtf8VaultBacked;
        private static bool _decryptionMaskVaultBacked;
        private static bool _overrideCsvScratchVaultBacked;
        private static bool _telemetryVaultBacked;
        private static bool _stagedLocaleLocked;
        private static bool _utf8ReaderHandleActive;
        private static bool _languageSignalLaneReady;

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
            AbortBabelDictionaryStage();
            DisposeUtf8State();
            DisposeErrorUtf8State();
            DisposeOverrideCsvScratch();
            DisposeDecryptionMaskState();
            DisposeTelemetryState();
            _telemetryFrameIndex = -1;
            _translationsThisFrame = 0;
            _bufferPoolLeasesActive = 0;
            _lookupSearchNsThisFrame = 0u;
            _missingHashCountThisFrame = 0u;
            _csvOverrideAppliedThisFrame = 0u;
            _csvOverrideRejectedThisFrame = 0u;
            _languageSignalRevision = 0u;
            _languageSignalLaneReady = false;
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
                RecordTelemetry(0u, _utf8ByteLength, _utf8IndexLength, BabelTelemetryFlags.EmptyReload);
                PublishLanguageChangedSignal(activeLanguage);
                return;
            }

            LoadLanguage(tables, activeLanguage);
            if (activeLanguage != GameLanguage.English)
                LoadLanguage(tables, GameLanguage.English);

            RebuildUtf8Lookup(tables, activeLanguage);
            RecordTelemetry(0u, _utf8ByteLength, _utf8IndexLength, BabelTelemetryFlags.Reload);
            PublishLanguageChangedSignal(activeLanguage);
        }

        /// <summary>
        /// Locks a Vault-owned staging byte buffer so a background file reader can fill it without main-thread I/O.
        /// </summary>
        public static unsafe bool TryBeginBabelDictionaryStage(
            int byteLength,
            GameLanguage language,
            out BabelDictionaryStage stage)
        {
            stage = default;
            EnsureTelemetryState();
            int sourceByteLength = byteLength;
            int paddedByteLength = H8StaticDataFormat.AlignUp16(byteLength);

            if (!BitConverter.IsLittleEndian ||
                sourceByteLength < BabelDictionaryHeaderBytes ||
                paddedByteLength > MaxBabelDictionaryBytes ||
                _stagedLocaleLocked)
            {
                RecordTelemetry(0u, 0, sourceByteLength, BabelTelemetryFlags.AsyncStageRejected);
                return false;
            }

            if (!TryResolveBabelVault(out IDataVault vault))
            {
                RecordTelemetry(0u, 0, sourceByteLength, BabelTelemetryFlags.AsyncStageRejected);
                return false;
            }

            _stagedLocaleBytesHandle = vault.GetBufferHandle<byte>(
                BabelStagedLocaleBufferId,
                paddedByteLength,
                SystemID.UI,
                NativeArrayOptions.UninitializedMemory);
            _stagedLocaleBytes = _stagedLocaleBytesHandle.Resolve(vault);
            if (!_stagedLocaleBytes.IsCreated || _stagedLocaleBytes.Length < paddedByteLength)
            {
                _stagedLocaleBytesHandle = default;
                _stagedLocaleBytes = default;
                RecordTelemetry(0u, 0, sourceByteLength, BabelTelemetryFlags.AsyncStageRejected);
                return false;
            }

            if (!vault.TryLockBuffer(BabelStagedLocaleBufferId, SystemID.UI))
            {
                _stagedLocaleBytesHandle = default;
                _stagedLocaleBytes = default;
                RecordTelemetry(0u, 0, sourceByteLength, BabelTelemetryFlags.AsyncStageRejected);
                return false;
            }

            void* destination = NativeArrayUnsafeUtility.GetUnsafePtr(_stagedLocaleBytes);
            if (destination == null)
            {
                vault.TryUnlockBuffer(BabelStagedLocaleBufferId, SystemID.UI);
                _stagedLocaleBytesHandle = default;
                _stagedLocaleBytes = default;
                RecordTelemetry(0u, 0, sourceByteLength, BabelTelemetryFlags.AsyncStageRejected);
                return false;
            }

            _stagedLocaleVault = vault;
            _stagedLocaleByteLength = paddedByteLength;
            _stagedLocaleSourceByteLength = sourceByteLength;
            _stagedLocaleGeneration++;
            _stagedLocaleLocked = true;

            stage.Destination = (IntPtr)destination;
            stage.ByteLength = paddedByteLength;
            stage.Generation = _stagedLocaleGeneration;
            stage.BufferId = (int)BabelStagedLocaleBufferId;
            stage.Language = (ushort)language;
            stage.SourceByteLength = sourceByteLength;
            RecordTelemetry(0u, paddedByteLength, sourceByteLength, BabelTelemetryFlags.AsyncStageBegin);
            return true;
        }

        /// <summary>
        /// Aborts a staged language file read and unlocks its Vault buffer.
        /// </summary>
        public static void AbortBabelDictionaryStage(in BabelDictionaryStage stage)
        {
            if (stage.Generation != 0u && stage.Generation != _stagedLocaleGeneration)
                return;

            AbortBabelDictionaryStage();
        }

        /// <summary>
        /// Validates and commits a fully loaded Babel dictionary during the dispatcher POST_SIMULATION swap window.
        /// </summary>
        public static unsafe bool TryCommitStagedBabelDictionary(in BabelDictionaryStage stage)
        {
            EnsureTelemetryState();
            if (!_stagedLocaleLocked ||
                stage.Generation == 0u ||
                stage.Generation != _stagedLocaleGeneration ||
                stage.ByteLength != _stagedLocaleByteLength ||
                stage.SourceByteLength != _stagedLocaleSourceByteLength ||
                stage.BufferId != (int)BabelStagedLocaleBufferId)
            {
                RecordTelemetry(0u, 0, stage.ByteLength, BabelTelemetryFlags.AsyncCommitRejected);
                return false;
            }

            NativeArray<byte> staged = _stagedLocaleBytesHandle.Resolve(_stagedLocaleVault);
            if (!staged.IsCreated || staged.Length < _stagedLocaleByteLength)
            {
                AbortBabelDictionaryStage();
                RecordTelemetry(0u, 0, stage.ByteLength, BabelTelemetryFlags.AsyncCommitRejected);
                return false;
            }

            byte* basePtr = (byte*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(staged);
            if (basePtr == null ||
                !TryValidateBabelDictionary(basePtr, _stagedLocaleByteLength, _stagedLocaleSourceByteLength, out H8BabelDictionaryHeader header))
            {
                DumpTelemetryForCorruption(0u, new int2(0, _stagedLocaleByteLength));
                AbortBabelDictionaryStage();
                return false;
            }

            int entryCount = (int)header.EntryCount;
            byte* indexBase = basePtr + header.IndexOffset;
            for (int i = 0; i < entryCount; i++)
            {
                H8BabelDictionaryEntry entry = UnsafeUtility.ReadArrayElement<H8BabelDictionaryEntry>(indexBase, i);
                if (!IsValidBabelEntry(in entry, in header, _stagedLocaleSourceByteLength))
                {
                    DumpTelemetryForCorruption(entry.Hash, new int2((int)entry.Offset, (int)entry.Length));
                    AbortBabelDictionaryStage();
                    return false;
                }
            }

            DisposeUtf8State();
            AcquireUtf8IndexBuffer(math.max(1, entryCount));
            if (!_utf8Index.IsCreated || _utf8Index.Length < math.max(1, entryCount))
            {
                GenerateEmergencyMockLocale();
                AbortBabelDictionaryStage();
                return false;
            }

            int dataBytes = (int)(header.FileByteLength - header.DataOffset);
            AcquireUtf8ByteBuffer(math.max(1, dataBytes));
            if (!_utf8Bytes.IsCreated || _utf8Bytes.Length < math.max(1, dataBytes))
            {
                DisposeUtf8State();
                GenerateEmergencyMockLocale();
                AbortBabelDictionaryStage();
                return false;
            }

            if (dataBytes > 0)
            {
                byte* destination = (byte*)NativeArrayUnsafeUtility.GetUnsafePtr(_utf8Bytes);
                UnsafeUtility.MemCpy(destination, basePtr + header.DataOffset, dataBytes);
            }

            uint previousHash = 0u;
            for (int i = 0; i < entryCount; i++)
            {
                H8BabelDictionaryEntry entry = UnsafeUtility.ReadArrayElement<H8BabelDictionaryEntry>(indexBase, i);
                int localOffset = (int)(entry.Offset - header.DataOffset);
                if ((i > 0 && entry.Hash <= previousHash) ||
                    !TryWriteUtf8Index(i, entry.Hash, localOffset, (int)entry.Length))
                {
                    DumpTelemetryForCorruption(entry.Hash, new int2(localOffset, (int)entry.Length));
                    DisposeUtf8State();
                    GenerateEmergencyMockLocale();
                    AbortBabelDictionaryStage();
                    return false;
                }

                previousHash = entry.Hash;
            }

            _utf8IndexLength = entryCount;
            _utf8ByteLength = dataBytes;
            _activeLanguage = (GameLanguage)stage.Language;
            RecordTelemetry(0u, dataBytes, entryCount, BabelTelemetryFlags.AsyncBinarySwap);
            PublishLanguageChangedSignal(_activeLanguage);
            AbortBabelDictionaryStage();
            return true;
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
            RefreshLookupTelemetryFrame();
            uint searchNs = 0u;
            if (TryFindUtf8Slice(keyHash, out int2 slice, out searchNs))
            {
                if (slice.y == 0)
                {
                    utf8Bytes = ReadOnlySpan<byte>.Empty;
                    RecordTelemetry(keyHash, slice.x, 0, BabelTelemetryFlags.Hit, searchNs);
                    return true;
                }

                if (IsValidUtf8Slice(slice))
                {
                    unsafe
                    {
                        byte* basePtr = (byte*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(_utf8Bytes);
                        utf8Bytes = new ReadOnlySpan<byte>(basePtr + slice.x, slice.y);
                    }

                    RecordTelemetry(keyHash, slice.x, slice.y, BabelTelemetryFlags.Hit, searchNs);
                    return true;
                }

                DumpTelemetryForCorruption(keyHash, slice);
            }

            LogMissingKeyOnce(unchecked((int)keyHash));
            utf8Bytes = GetErrorUtf8Span();
            _missingHashCountThisFrame++;
            RecordTelemetry(keyHash, -1, ErrorUtf8Length, BabelTelemetryFlags.Miss, searchNs);
            return false;
        }

        /// <summary>
        /// Writes the player-progress XOR mask used by lore decryption. Full progress produces a zero mask.
        /// </summary>
        /// <param name="collectedKeyMask">Bitfield for lore keys already owned by the player.</param>
        /// <param name="requiredKeyMask">Bitfield required by the lore fragment family.</param>
        /// <param name="loreSaltHash">Stable salt hash for the fragment family.</param>
        public static bool TrySetLoreDecryptionMask(
            uint collectedKeyMask,
            uint requiredKeyMask,
            uint loreSaltHash)
        {
            EnsureDecryptionMaskBuffer();
            bool written = BabelDictionaryStore.TryBuildProgressDecryptionMask(
                _decryptionMask,
                collectedKeyMask,
                requiredKeyMask,
                loreSaltHash);
            if (!written)
                return false;

            uint missingBits = requiredKeyMask & ~collectedKeyMask;
            RecordTelemetry(loreSaltHash, (int)missingBits, 16, BabelTelemetryFlags.Hit);
            return true;
        }

        /// <summary>
        /// Schedules Burst XOR decryption for a localized lore UTF-8 slice into caller-owned native bytes.
        /// </summary>
        /// <remarks>
        /// The output remains UTF-8 bytes. UI/PDA presentation owns any later glyph decoding.
        /// </remarks>
        public static bool TryScheduleLoreDecryption(
            uint keyHash,
            NativeArray<byte> outputBytes,
            JobHandle dependency,
            out JobHandle handle,
            out int byteLength)
        {
            handle = dependency;
            byteLength = 0;
            RefreshLookupTelemetryFrame();

            if (!outputBytes.IsCreated || !_utf8Bytes.IsCreated || _utf8IndexLength <= 0)
            {
                RecordTelemetry(keyHash, -1, 0, BabelTelemetryFlags.Miss);
                return false;
            }

            uint searchNs = 0u;
            if (!TryFindUtf8Slice(keyHash, out int2 slice, out searchNs))
            {
                LogMissingKeyOnce(unchecked((int)keyHash));
                _missingHashCountThisFrame++;
                RecordTelemetry(keyHash, -1, ErrorUtf8Length, BabelTelemetryFlags.Miss, searchNs);
                return false;
            }

            if (!IsValidUtf8Slice(slice) || slice.y < 0 || slice.y > outputBytes.Length)
            {
                DumpTelemetryForCorruption(keyHash, slice);
                return false;
            }

            if (slice.y == 0)
            {
                RecordTelemetry(keyHash, slice.x, 0, BabelTelemetryFlags.Hit, searchNs);
                return true;
            }

            EnsureDecryptionMaskBuffer();
            if (!_decryptionMask.IsCreated)
            {
                RecordTelemetry(keyHash, slice.x, slice.y, BabelTelemetryFlags.Miss, searchNs);
                return false;
            }

            BabelDictionaryStore.BabelLoreXorDecryptJob job = new BabelDictionaryStore.BabelLoreXorDecryptJob
            {
                SourceBytes = _utf8Bytes,
                DecryptionMask = _decryptionMask,
                OutputBytes = outputBytes,
                SourceOffset = (uint)slice.x,
                ByteLength = (uint)slice.y
            };

            byteLength = slice.y;
            handle = job.Schedule(slice.y, 64, dependency);
            RegisterUtf8ReaderHandle(handle);
            RecordTelemetry(keyHash, slice.x, slice.y, BabelTelemetryFlags.Hit, searchNs);
            return true;
        }

        /// <summary>
        /// Decode a localized UTF-8 entry into a thread-local char buffer for TMP SetCharArray.
        /// </summary>
        public static bool TryGetVisualBufferFromUtf8(int keyHash, out char[] buffer, out int length)
        {
            bool found = TryGetLocalizedSpan(unchecked((uint)keyHash), out ReadOnlySpan<byte> utf8Bytes);
            return DecodeUtf8VisualBuffer(unchecked((uint)keyHash), found, utf8Bytes, out buffer, out length);
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
                    return DecodeUtf8VisualBuffer(unchecked((uint)keyHash), true, utf8Bytes, out buffer, out length);
                }
            }

            return TryGetVisualBufferFromUtf8(keyHash, out buffer, out length);
        }

        /// <summary>
        /// Decode localized UTF-8 directly into caller-owned storage for TMP SetCharArray.
        /// </summary>
        public static bool TryWriteVisualSpanFromUtf8(
            int keyHash,
            Span<char> destination,
            out int length,
            bool stripRichText = false)
        {
            return TryWriteVisualSpanFromUtf8(
                unchecked((uint)keyHash),
                destination,
                out length,
                default,
                stripRichText);
        }

        /// <summary>
        /// Decode localized UTF-8 directly into caller-owned storage for TMP SetCharArray.
        /// </summary>
        public static bool TryWriteVisualSpanFromUtf8(
            uint keyHash,
            Span<char> destination,
            out int length,
            bool stripRichText = false)
        {
            return TryWriteVisualSpanFromUtf8(
                keyHash,
                destination,
                out length,
                default,
                stripRichText);
        }

        /// <summary>
        /// Decode localized UTF-8 directly into caller-owned storage and patch ^0..^3 numeric placeholders.
        /// </summary>
        public static bool TryWriteVisualSpanFromUtf8(
            uint keyHash,
            Span<char> destination,
            out int length,
            BabelFormatArgs formatArgs,
            bool stripRichText = false)
        {
            bool found = TryGetLocalizedSpan(keyHash, out ReadOnlySpan<byte> utf8Bytes);
            return DecodeUtf8VisualSpan(keyHash, found, utf8Bytes, destination, out length, formatArgs, stripRichText);
        }

        /// <summary>
        /// Decode a Burst-prefetched UTF-8 slice directly into caller-owned storage.
        /// </summary>
        public static bool TryWriteVisualSpanFromUtf8Slice(
            int keyHash,
            int2 utf8Slice,
            Span<char> destination,
            out int length,
            bool stripRichText = false)
        {
            return TryWriteVisualSpanFromUtf8Slice(
                unchecked((uint)keyHash),
                utf8Slice,
                destination,
                out length,
                default,
                stripRichText);
        }

        /// <summary>
        /// Decode a Burst-prefetched UTF-8 slice directly into caller-owned storage and patch ^0..^3 numeric placeholders.
        /// </summary>
        public static bool TryWriteVisualSpanFromUtf8Slice(
            uint keyHash,
            int2 utf8Slice,
            Span<char> destination,
            out int length,
            BabelFormatArgs formatArgs,
            bool stripRichText = false)
        {
            if (utf8Slice.x >= 0 && utf8Slice.y >= 0 && IsValidUtf8Slice(utf8Slice))
            {
                unsafe
                {
                    byte* basePtr = (byte*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(_utf8Bytes);
                    ReadOnlySpan<byte> utf8Bytes = new ReadOnlySpan<byte>(basePtr + utf8Slice.x, utf8Slice.y);
                    RecordTelemetry(keyHash, utf8Slice.x, utf8Slice.y, BabelTelemetryFlags.Hit);
                    return DecodeUtf8VisualSpan(keyHash, true, utf8Bytes, destination, out length, formatArgs, stripRichText);
                }
            }

            return TryWriteVisualSpanFromUtf8(keyHash, destination, out length, formatArgs, stripRichText);
        }

        private static bool DecodeUtf8VisualBuffer(
            uint keyHash,
            bool found,
            ReadOnlySpan<byte> utf8Bytes,
            out char[] buffer,
            out int length)
        {
            buffer = GetDecodeBuffer(MaxDecodedGlyphs);
            return DecodeUtf8VisualSpan(
                keyHash,
                found,
                utf8Bytes,
                buffer.AsSpan(0, math.min(buffer.Length, MaxDecodedGlyphs)),
                out length,
                default,
                false);
        }

        private static bool DecodeUtf8VisualSpan(
            uint keyHash,
            bool found,
            ReadOnlySpan<byte> utf8Bytes,
            Span<char> destination,
            out int length,
            BabelFormatArgs formatArgs,
            bool stripRichText)
        {
            float decodeStart = Time.realtimeSinceStartup;
            length = 0;
            if (destination.Length <= 0)
            {
                RecordTelemetry(keyHash, -1, utf8Bytes.Length, BabelTelemetryFlags.Truncated);
                return found;
            }

            bool truncated = false;
            bool malformed = false;
            bool injectedVariable = false;
            int byteCursor = 0;
            int charCursor = 0;
            int maxGlyphs = math.min(destination.Length, MaxDecodedGlyphs);
            while (byteCursor < utf8Bytes.Length)
            {
                byte current = utf8Bytes[byteCursor];
                if (stripRichText && current == (byte)'<')
                {
                    int tagEnd = byteCursor + 1;
                    while (tagEnd < utf8Bytes.Length && utf8Bytes[tagEnd] != (byte)'>')
                        tagEnd++;

                    if (tagEnd < utf8Bytes.Length)
                    {
                        byteCursor = tagEnd + 1;
                        continue;
                    }
                }

                if (current == (byte)'^' &&
                    byteCursor + 1 < utf8Bytes.Length &&
                    TryWriteFormatPlaceholder(utf8Bytes[byteCursor + 1], in formatArgs, destination.Slice(0, maxGlyphs), ref charCursor))
                {
                    injectedVariable = true;
                    if (charCursor > maxGlyphs)
                    {
                        truncated = true;
                        charCursor = maxGlyphs;
                        break;
                    }

                    byteCursor += 2;
                    continue;
                }

                if (!TryReadUtf8Scalar(utf8Bytes, byteCursor, out int scalar, out int consumed))
                {
                    scalar = '\uFFFD';
                    consumed = 1;
                    malformed = true;
                }

                int requiredChars = scalar <= 0xFFFF ? 1 : 2;
                if (charCursor + requiredChars > maxGlyphs)
                {
                    truncated = true;
                    break;
                }

                if (scalar <= 0xFFFF)
                {
                    destination[charCursor++] = (char)scalar;
                }
                else
                {
                    int supplementary = scalar - 0x10000;
                    destination[charCursor++] = (char)(0xD800 + (supplementary >> 10));
                    destination[charCursor++] = (char)(0xDC00 + (supplementary & 0x3FF));
                }

                byteCursor += consumed;
            }

            length = charCursor;
            if (found && LocalizationManager.IsRightToLeftLanguage(_activeLanguage))
                ReverseSpanInPlace(destination.Slice(0, length));

            if (truncated)
                AppendEllipsis(destination, ref length, maxGlyphs);

            ushort flags = found ? BabelTelemetryFlags.Hit : BabelTelemetryFlags.Miss;
            if (truncated)
                flags = (ushort)(flags | BabelTelemetryFlags.Truncated);
            if (malformed)
                flags = (ushort)(flags | BabelTelemetryFlags.MalformedUtf8);
            if (stripRichText)
                flags = (ushort)(flags | BabelTelemetryFlags.RichTextStripped);
            if (injectedVariable)
                flags = (ushort)(flags | BabelTelemetryFlags.VariableInjected);

            float spanConversionTimeMs = math.max(0f, (Time.realtimeSinceStartup - decodeStart) * 1000f);
            RecordTelemetry(keyHash, found ? 0 : -1, length, flags, spanConversionTimeMs);
            if (spanConversionTimeMs > SlowDecodeDumpThresholdMs)
                DumpTelemetryForSlowDecode(keyHash, length, spanConversionTimeMs);

            return found;
        }

        private static bool TryWriteFormatPlaceholder(
            byte token,
            in BabelFormatArgs formatArgs,
            Span<char> destination,
            ref int charCursor)
        {
            int tokenIndex = token - (byte)'0';
            if ((uint)tokenIndex >= 4u || !formatArgs.TryGet(tokenIndex, out int value))
                return false;

            return ZeroGCFormatter.FastIntToChars(value, destination, ref charCursor);
        }

        private static bool TryReadUtf8Scalar(
            ReadOnlySpan<byte> value,
            int index,
            out int scalar,
            out int consumed)
        {
            scalar = 0;
            consumed = 1;
            if ((uint)index >= (uint)value.Length)
                return false;

            byte lead = value[index];
            if (lead < 0x80)
            {
                scalar = lead;
                return true;
            }

            if ((lead & 0xE0) == 0xC0)
            {
                if (index + 1 >= value.Length || !IsUtf8ContinuationByte(value[index + 1]))
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
                    !IsUtf8ContinuationByte(value[index + 1]) ||
                    !IsUtf8ContinuationByte(value[index + 2]))
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
                    !IsUtf8ContinuationByte(value[index + 1]) ||
                    !IsUtf8ContinuationByte(value[index + 2]) ||
                    !IsUtf8ContinuationByte(value[index + 3]))
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

        private static bool IsUtf8ContinuationByte(byte value)
        {
            return (value & 0xC0) == 0x80;
        }

        private static void ReverseSpanInPlace(Span<char> buffer)
        {
            int left = 0;
            int right = buffer.Length - 1;
            while (left < right)
            {
                char temp = buffer[left];
                buffer[left] = buffer[right];
                buffer[right] = temp;
                left++;
                right--;
            }
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
                !_utf8Index.IsCreated ||
                _utf8IndexLength <= 0)
            {
                handle = dependency;
                return false;
            }

            int scheduledCount = ResolveLookupBudgetForCurrentQuality(count);
            if (scheduledCount <= 0)
            {
                handle = dependency;
                return true;
            }

            BabelVisibleTextOffsetPrefetchJob job = new BabelVisibleTextOffsetPrefetchJob
            {
                VisibleHashes = visibleHashes,
                IndexTable = _utf8Index,
                IndexCount = _utf8IndexLength,
                OutputSlices = outputSlices
            };
            handle = job.Schedule(scheduledCount, 32, dependency);
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

            // [BLOCKING_SYNC_POINT] Non-blocking fence clear: IsCompleted was true before Complete().
            _utf8ReaderHandle.Complete();
            _utf8ReaderHandle = default;
            _utf8ReaderHandleActive = false;
        }

        /// <summary>
        /// Dump the 300-frame Babel telemetry ring for caller-detected invalid presentation state.
        /// </summary>
        public static void DumpTelemetryForFault(uint keyHash)
        {
            DumpTelemetryForCorruption(keyHash, new int2(-1, 0));
        }

        /// <summary>
        /// Applies project-root loc_overrides.csv into the active UTF-8 blob without rebaking.
        /// Longer replacements append to the Vault UTF-8 blob and update the active index slice.
        /// </summary>
        public static unsafe bool TryApplyLocOverridesCsv(string path, out int applied, out int rejected)
        {
            applied = 0;
            rejected = 0;
            if (string.IsNullOrEmpty(path) ||
                !File.Exists(path) ||
                !_utf8Bytes.IsCreated ||
                !_utf8Index.IsCreated ||
                _utf8IndexLength <= 0)
            {
                return false;
            }

            CompleteUtf8ReadersForMutation();
            EnsureOverrideCsvScratch();
            if (!_overrideCsvScratch.IsCreated)
                return false;

            int bytesRead;
            bool scratchLocked = false;
            bool keepScratchLocked = false;
            bool mutationGuarded = false;
            try
            {
                int maxBytes = math.min(_overrideCsvScratch.Length, OverrideCsvScratchBytes);
                FileInfo info = new FileInfo(path);
                if (!info.Exists || info.Length <= 0L || info.Length > maxBytes)
                    return false;

                if (_overrideCsvScratchVaultBacked && _babelVault != null)
                {
                    scratchLocked = _babelVault.TryLockBuffer(BabelOverrideCsvScratchBufferId, SystemID.UI);
                    if (!scratchLocked)
                        return false;

                    NativeArray<byte> resolved = _overrideCsvScratchHandle.Resolve(_babelVault);
                    if (resolved.IsCreated && resolved.Length >= maxBytes)
                        _overrideCsvScratch = resolved;
                }

                byte* scratchPtr = (byte*)NativeArrayUnsafeUtility.GetUnsafePtr(_overrideCsvScratch);
                if (scratchPtr == null)
                    return false;

                using FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                bytesRead = 0;
                while (bytesRead < maxBytes)
                {
                    int chunk = stream.Read(new Span<byte>(scratchPtr + bytesRead, maxBytes - bytesRead));
                    if (chunk <= 0)
                        break;

                    bytesRead += chunk;
                }

                if (_babelVault != null)
                {
                    mutationGuarded = _babelVault.TryAcquireMutationGuard(BabelOverrideMutationGuardMask);
                    if (!mutationGuarded)
                        return false;
                }

                keepScratchLocked = true;
            }
            catch (IOException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
            catch (Exception)
            {
                return false;
            }
            finally
            {
                if (scratchLocked && !keepScratchLocked && _babelVault != null)
                    _babelVault.TryUnlockBuffer(BabelOverrideCsvScratchBufferId, SystemID.UI);
            }

            int cursor = 0;
            try
            {
                while (TryReadCsvLine(_overrideCsvScratch, bytesRead, ref cursor, out int lineStart, out int lineEnd))
                {
                    if (lineStart == 0 && lineEnd - lineStart >= 3 &&
                        _overrideCsvScratch[lineStart] == 0xEF &&
                        _overrideCsvScratch[lineStart + 1] == 0xBB &&
                        _overrideCsvScratch[lineStart + 2] == 0xBF)
                    {
                        lineStart += 3;
                    }

                    TrimAscii(_overrideCsvScratch, ref lineStart, ref lineEnd);
                    if (lineStart >= lineEnd || _overrideCsvScratch[lineStart] == (byte)'#')
                        continue;

                    int separator = FindCsvSeparator(_overrideCsvScratch, lineStart, lineEnd);
                    if (separator <= lineStart)
                    {
                        rejected++;
                        continue;
                    }

                    int keyStart = lineStart;
                    int keyEnd = separator;
                    int valueStart = separator + 1;
                    int valueEnd = lineEnd;
                    TrimAscii(_overrideCsvScratch, ref keyStart, ref keyEnd);
                    TrimAscii(_overrideCsvScratch, ref valueStart, ref valueEnd);

                    if (IsCsvHeaderKey(_overrideCsvScratch, keyStart, keyEnd))
                        continue;

                    if (!TryParseCsvHash(_overrideCsvScratch, keyStart, keyEnd, out uint hash) ||
                        !TryApplyUtf8Override(hash, valueStart, valueEnd))
                    {
                        rejected++;
                        continue;
                    }

                    applied++;
                }
            }
            finally
            {
                if (mutationGuarded && _babelVault != null)
                    _babelVault.ReleaseMutationGuard(BabelOverrideMutationGuardMask);
                if (scratchLocked && _babelVault != null)
                    _babelVault.TryUnlockBuffer(BabelOverrideCsvScratchBufferId, SystemID.UI);
            }

            if (applied > 0 || rejected > 0)
            {
                RefreshLookupTelemetryFrame();
                _csvOverrideAppliedThisFrame += (uint)math.max(0, applied);
                _csvOverrideRejectedThisFrame += (uint)math.max(0, rejected);
                RecordTelemetry(0u, applied, rejected, BabelTelemetryFlags.CsvOverride);
            }

            return true;
        }

        /// <summary>
        /// Alias for tooling that names the hot override path directly.
        /// </summary>
        public static bool TryApplyOverrideCsv(string path, out int applied, out int rejected)
        {
            return TryApplyLocOverridesCsv(path, out applied, out rejected);
        }

        /// <summary>
        /// Updates the localization black-box with the active CharBufferPool lease count.
        /// </summary>
        public static void ReportBufferPoolLeasesActive(int activeLeases)
        {
            _bufferPoolLeasesActive = math.max(0, activeLeases);
        }

        private static void PublishLanguageChangedSignal(GameLanguage language)
        {
            EnsureLanguageSignalLane();
            LocalizationLanguageChangedSignal signal = new LocalizationLanguageChangedSignal
            {
                LanguageHash = 0x4C4F434Cu ^ (uint)language,
                Revision = ++_languageSignalRevision,
                Language = (ushort)language
            };
            SignalBus<LocalizationLanguageChangedSignal>.Push(in signal);
        }

        private static void EnsureLanguageSignalLane()
        {
            if (_languageSignalLaneReady)
                return;

            SignalBus<LocalizationLanguageChangedSignal>.Configure(
                expectedCapacity: 8,
                maxFrameSignals: 8,
                lowTierFrameSignals: 4,
                laneHash: 0xBABA0039u);
            SignalBus<LocalizationLanguageChangedSignal>.EnsureInitialized();
            _languageSignalLaneReady = true;
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
            {
                GenerateEmergencyMockLocale();
                return;
            }

            int byteCapacity = EstimateUtf8ByteCapacity(tables, activeLanguage);
            AcquireUtf8IndexBuffer(entryCapacity);
            AcquireUtf8ByteBuffer(byteCapacity);
            if (!_utf8Index.IsCreated || !_utf8Bytes.IsCreated)
            {
                GenerateEmergencyMockLocale();
                return;
            }

            int writeOffset = 0;
            int indexWrite = 0;
            LoadLanguageUtf8(tables, activeLanguage, ref writeOffset, ref indexWrite);
            if (activeLanguage != GameLanguage.English)
                LoadLanguageUtf8(tables, GameLanguage.English, ref writeOffset, ref indexWrite);

            TryLoadStaticArenaUtf8(ref writeOffset, ref indexWrite);
            _utf8IndexLength = indexWrite;
            SortUtf8Index(_utf8IndexLength);
            _utf8ByteLength = writeOffset;
        }

        private static void GenerateEmergencyMockLocale()
        {
            AcquireUtf8IndexBuffer(1);
            AcquireUtf8ByteBuffer(16);
            EnsureErrorUtf8();
            if (!_utf8Index.IsCreated || !_utf8Bytes.IsCreated || !_errorUtf8.IsCreated)
            {
                _utf8IndexLength = 0;
                _utf8ByteLength = 0;
                return;
            }

            for (int i = 0; i < ErrorUtf8Length; i++)
                _utf8Bytes[i] = _errorUtf8[i];

            TryWriteUtf8Index(0, _emergencyMockErrorHash, 0, ErrorUtf8Length);
            _utf8IndexLength = 1;
            _utf8ByteLength = ErrorUtf8Length;
            RecordTelemetry(_emergencyMockErrorHash, 0, _utf8ByteLength, BabelTelemetryFlags.EmergencyMock);
        }

        private static void EnsureErrorUtf8()
        {
            if (_errorUtf8.IsCreated)
                return;

            if (TryResolveBabelVault(out IDataVault vault))
            {
                _errorUtf8Handle = vault.GetBufferHandle<byte>(
                    BabelErrorUtf8BufferId,
                    16,
                    SystemID.UI,
                    NativeArrayOptions.ClearMemory);
                NativeArray<byte> resolved = _errorUtf8Handle.Resolve(vault);
                if (resolved.IsCreated && resolved.Length >= 16)
                {
                    _babelVault = vault;
                    _errorUtf8 = resolved;
                    _errorUtf8VaultBacked = true;
                    WriteErrorUtf8Bytes();
                    return;
                }

                _errorUtf8Handle = default;
            }
        }

        private static void WriteErrorUtf8Bytes()
        {
            _errorUtf8[0] = (byte)'E';
            _errorUtf8[1] = (byte)'R';
            _errorUtf8[2] = (byte)'R';
            _errorUtf8[3] = (byte)'O';
            _errorUtf8[4] = (byte)'R';
        }

        private static unsafe ReadOnlySpan<byte> GetErrorUtf8Span()
        {
            EnsureErrorUtf8();
            if (!_errorUtf8.IsCreated)
                return ReadOnlySpan<byte>.Empty;

            byte* ptr = (byte*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(_errorUtf8);
            return ptr == null ? ReadOnlySpan<byte>.Empty : new ReadOnlySpan<byte>(ptr, ErrorUtf8Length);
        }

        private static void AcquireUtf8IndexBuffer(int entryCapacity)
        {
            _utf8Index = default;
            _utf8IndexHandle = default;
            _utf8IndexRegistered = false;
            _utf8IndexVaultBacked = false;

            if (entryCapacity <= 0)
                return;

            if (TryResolveBabelVault(out IDataVault vault))
            {
                _utf8IndexHandle = vault.GetBufferHandle<LocalizationEntryDTO>(
                    BabelIndexTableBufferId,
                    entryCapacity,
                    SystemID.UI,
                    NativeArrayOptions.UninitializedMemory);
                NativeArray<LocalizationEntryDTO> resolved = _utf8IndexHandle.Resolve(vault);
                if (resolved.IsCreated && resolved.Length >= entryCapacity)
                {
                    _babelVault = vault;
                    _utf8Index = resolved;
                    _utf8IndexVaultBacked = true;
                    return;
                }

                _utf8IndexHandle = default;
            }
        }

        private static void EnsureDecryptionMaskBuffer()
        {
            if (_decryptionMask.IsCreated)
                return;

            if (TryResolveBabelVault(out IDataVault vault))
            {
                _decryptionMaskHandle = vault.GetBufferHandle<byte>(
                    BabelDecryptionMaskBufferId,
                    16,
                    SystemID.UI,
                    NativeArrayOptions.ClearMemory);
                NativeArray<byte> resolved = _decryptionMaskHandle.Resolve(vault);
                if (resolved.IsCreated && resolved.Length >= 16)
                {
                    _babelVault = vault;
                    _decryptionMask = resolved;
                    _decryptionMaskVaultBacked = true;
                    return;
                }
            }
        }

        private static void EnsureOverrideCsvScratch()
        {
            if (_overrideCsvScratch.IsCreated)
                return;

            if (TryResolveBabelVault(out IDataVault vault))
            {
                _overrideCsvScratchHandle = vault.GetBufferHandle<byte>(
                    BabelOverrideCsvScratchBufferId,
                    OverrideCsvScratchBytes,
                    SystemID.UI,
                    NativeArrayOptions.UninitializedMemory);
                NativeArray<byte> resolved = _overrideCsvScratchHandle.Resolve(vault);
                if (resolved.IsCreated && resolved.Length >= OverrideCsvScratchBytes)
                {
                    _babelVault = vault;
                    _overrideCsvScratch = resolved;
                    _overrideCsvScratchVaultBacked = true;
                    return;
                }
            }
        }

        private static void AcquireUtf8ByteBuffer(int byteCapacity)
        {
            _utf8Bytes = default;
            _utf8BytesHandle = default;
            _utf8BytesRegistered = false;
            _utf8BytesVaultBacked = false;

            if (byteCapacity <= 0)
                return;

            if (TryResolveBabelVault(out IDataVault vault))
            {
                _utf8BytesHandle = vault.GetBufferHandle<byte>(
                    BabelUtf8BlobBufferId,
                    byteCapacity,
                    SystemID.UI,
                    NativeArrayOptions.UninitializedMemory);
                NativeArray<byte> resolved = _utf8BytesHandle.Resolve(vault);
                if (resolved.IsCreated && resolved.Length >= byteCapacity)
                {
                    _babelVault = vault;
                    _utf8Bytes = resolved;
                    _utf8BytesVaultBacked = true;
                    return;
                }

                _utf8BytesHandle = default;
            }
        }

        private static bool TryResolveBabelVault(out IDataVault vault)
        {
            vault = GlobalRegistry.DataVault;
            if (vault != null)
                return true;

            if (GlobalDataVault.TryGetLatestCreated(out GlobalDataVault latest))
            {
                vault = latest;
                return true;
            }

            return false;
        }

        private static void AbortBabelDictionaryStage()
        {
            if (_stagedLocaleLocked && _stagedLocaleVault != null)
                _stagedLocaleVault.TryUnlockBuffer(BabelStagedLocaleBufferId, SystemID.UI);

            _stagedLocaleLocked = false;
            _stagedLocaleVault = null;
            _stagedLocaleBytesHandle = default;
            _stagedLocaleBytes = default;
            _stagedLocaleByteLength = 0;
            _stagedLocaleSourceByteLength = 0;
        }

        private static unsafe bool TryValidateBabelDictionary(
            byte* basePtr,
            int byteLength,
            int sourceByteLength,
            out H8BabelDictionaryHeader header)
        {
            header = default;
            if (basePtr == null || byteLength < BabelDictionaryHeaderBytes || sourceByteLength < BabelDictionaryHeaderBytes)
                return false;

            header = UnsafeUtility.ReadArrayElement<H8BabelDictionaryHeader>(basePtr, 0);
            if (header.Magic == ReverseUInt32(H8StaticDataFormat.BabelMagic))
            {
                header.Magic = ReverseUInt32(header.Magic);
                header.FormatVersion = ReverseUInt16(header.FormatVersion);
                header.HeaderSizeBytes = ReverseUInt16(header.HeaderSizeBytes);
                header.EntryCount = ReverseUInt32(header.EntryCount);
                header.IndexOffset = ReverseUInt32(header.IndexOffset);
                header.DataOffset = ReverseUInt32(header.DataOffset);
                header.FileByteLength = ReverseUInt32(header.FileByteLength);
                header.PayloadCrc32 = ReverseUInt32(header.PayloadCrc32);
                header.Flags = ReverseUInt32(header.Flags);
            }

            if (header.Magic != H8StaticDataFormat.BabelMagic ||
                header.FormatVersion != H8StaticDataFormat.FormatVersion ||
                header.HeaderSizeBytes != BabelDictionaryHeaderBytes ||
                (header.FileByteLength != (uint)sourceByteLength && header.FileByteLength != (uint)byteLength) ||
                header.FileByteLength > (uint)byteLength ||
                (header.Flags & H8StaticDataFormat.LittleEndianFlag) == 0u)
            {
                return false;
            }

            if (header.EntryCount > int.MaxValue ||
                header.IndexOffset < header.HeaderSizeBytes ||
                header.DataOffset < header.IndexOffset ||
                header.DataOffset > header.FileByteLength ||
                (header.IndexOffset & 15u) != 0u ||
                (header.DataOffset & 15u) != 0u)
            {
                return false;
            }

            long indexBytes = (long)header.EntryCount * BabelDictionaryEntryBytes;
            if (indexBytes < 0L ||
                header.IndexOffset + indexBytes > header.DataOffset ||
                header.DataOffset > header.FileByteLength)
            {
                return false;
            }

            uint crc = H8Crc32.Compute(basePtr + header.HeaderSizeBytes, (int)header.FileByteLength - header.HeaderSizeBytes);
            return crc == header.PayloadCrc32;
        }

        private static ushort ReverseUInt16(ushort value)
        {
            return (ushort)((value << 8) | (value >> 8));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint ReverseUInt32(uint value)
        {
            return ((value & 0x000000FFu) << 24) |
                   ((value & 0x0000FF00u) << 8) |
                   ((value & 0x00FF0000u) >> 8) |
                   ((value & 0xFF000000u) >> 24);
        }

        private static bool IsValidBabelEntry(
            in H8BabelDictionaryEntry entry,
            in H8BabelDictionaryHeader header,
            int byteLength)
        {
            if (entry.Hash == 0u ||
                entry.Offset < header.DataOffset ||
                entry.Offset > (uint)byteLength ||
                (entry.Offset & 15u) != 0u)
            {
                return false;
            }

            long availableBytes = (long)byteLength - entry.Offset;
            return availableBytes >= 0L && entry.Length <= availableBytes;
        }

        private static void RefreshUtf8BytesFromVault()
        {
            if (!_utf8BytesVaultBacked)
                return;

            NativeArray<byte> resolved = _utf8BytesHandle.Resolve(_babelVault);
            if (resolved.IsCreated && resolved.Length >= _utf8ByteLength)
                _utf8Bytes = resolved;
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
            ref int writeOffset,
            ref int indexWrite)
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
                if (ContainsUtf8IndexKey(keyHash, indexWrite))
                    continue;

                string value = enumerator.Current.Value ?? string.Empty;
                int byteCount = Encoding.UTF8.GetByteCount(value);
                int offset = writeOffset;
                if (byteCount > 0 && !TryWriteUtf8(value, offset, byteCount))
                {
                    DumpTelemetryForCorruption(keyHash, new int2(offset, byteCount));
                    continue;
                }

                if (TryWriteUtf8Index(indexWrite, keyHash, offset, byteCount))
                    indexWrite++;
                writeOffset = SaturatingAdd(writeOffset, byteCount);
            }
        }

        private static void TryLoadStaticArenaUtf8(ref int writeOffset, ref int indexWrite)
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
                    if (!ContainsUtf8IndexKey(valueHash, indexWrite) &&
                        TryWriteUtf8Index(indexWrite, valueHash, startOffset + entryStart, byteLength))
                    {
                        indexWrite++;
                    }
                }

                cursor++;
            }

            TryLoadStaticArenaReferenceAliases(startOffset, ref indexWrite);
            writeOffset = SaturatingAdd(writeOffset, locData.Length);
        }

        private static void TryLoadStaticArenaReferenceAliases(int staticStartOffset, ref int indexWrite)
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

                if (!ContainsUtf8IndexKey(reference.KeyHash, indexWrite) &&
                    TryWriteUtf8Index(indexWrite, reference.KeyHash, offset, reference.ByteLength))
                {
                    indexWrite++;
                }
            }
        }

        private static bool TryWriteUtf8Index(int index, uint keyHash, int offset, int byteLength)
        {
            if (!_utf8Index.IsCreated ||
                (uint)index >= (uint)_utf8Index.Length ||
                offset < 0 ||
                byteLength < 0)
            {
                return false;
            }

            _utf8Index[index] = new LocalizationEntryDTO
            {
                StringHash = keyHash,
                ByteOffset = (uint)offset,
                ByteLength = (uint)byteLength
            };
            return true;
        }

        private static unsafe bool TryApplyUtf8Override(uint keyHash, int valueStart, int valueEnd)
        {
            int byteLength = CountCsvValueBytes(_overrideCsvScratch, valueStart, valueEnd);
            if (byteLength < 0 ||
                !_utf8Bytes.IsCreated ||
                !_overrideCsvScratch.IsCreated ||
                !TryFindUtf8Index(keyHash, out int index))
            {
                return false;
            }

            LocalizationEntryDTO entry = _utf8Index[index];
            int offset = (int)entry.ByteOffset;
            int capacity = (int)entry.ByteLength;
            int clearAfterBytes = 0;
            if (entry.ByteOffset > int.MaxValue ||
                entry.ByteLength > int.MaxValue ||
                offset < 0 ||
                capacity < 0 ||
                offset > _utf8ByteLength - capacity)
            {
                return false;
            }

            if (byteLength > capacity)
            {
                int appendOffset = H8StaticDataFormat.AlignUp16(_utf8ByteLength);
                int requiredLength = H8StaticDataFormat.AlignUp16(SaturatingAdd(appendOffset, byteLength));
                if (requiredLength < appendOffset ||
                    !TryEnsureUtf8ByteCapacity(requiredLength))
                {
                    return false;
                }

                if (appendOffset > _utf8ByteLength)
                {
                    byte* gapDestination = (byte*)NativeArrayUnsafeUtility.GetUnsafePtr(_utf8Bytes);
                    if (gapDestination == null)
                        return false;

                    UnsafeUtility.MemClear(gapDestination + _utf8ByteLength, appendOffset - _utf8ByteLength);
                }

                offset = appendOffset;
                capacity = byteLength;
                clearAfterBytes = requiredLength - (appendOffset + byteLength);
            }

            byte* destination = (byte*)NativeArrayUnsafeUtility.GetUnsafePtr(_utf8Bytes);
            if (destination == null)
                return false;

            if (!CopyCsvValueBytes(_overrideCsvScratch, valueStart, valueEnd, destination + offset, byteLength))
                return false;

            int clearBytes = math.max(capacity - byteLength, clearAfterBytes);
            if (clearBytes > 0)
                UnsafeUtility.MemClear(destination + offset + byteLength, clearBytes);

            if (byteLength > (int)entry.ByteLength)
                _utf8ByteLength = math.max(_utf8ByteLength, offset + byteLength);

            entry.ByteLength = (uint)byteLength;
            entry.ByteOffset = (uint)offset;
            _utf8Index[index] = entry;
            return true;
        }

        private static bool TryEnsureUtf8ByteCapacity(int requiredLength)
        {
            if (!_utf8Bytes.IsCreated || requiredLength <= _utf8Bytes.Length)
                return _utf8Bytes.IsCreated;

            if (!_utf8BytesVaultBacked || _babelVault == null)
                return false;

            _utf8BytesHandle = _babelVault.GetBufferHandle<byte>(
                BabelUtf8BlobBufferId,
                requiredLength,
                SystemID.UI,
                NativeArrayOptions.UninitializedMemory);
            NativeArray<byte> resolved = _utf8BytesHandle.Resolve(_babelVault);
            if (!resolved.IsCreated || resolved.Length < requiredLength)
                return false;

            _utf8Bytes = resolved;
            _utf8BytesVaultBacked = true;
            _utf8BytesRegistered = false;
            return true;
        }

        private static bool TryFindUtf8Index(uint keyHash, out int index)
        {
            int low = 0;
            int high = _utf8Index.IsCreated ? _utf8IndexLength - 1 : -1;
            while (low <= high)
            {
                int mid = (int)(((uint)low + (uint)high) >> 1);
                uint hash = _utf8Index[mid].StringHash;
                if (hash == keyHash)
                {
                    index = mid;
                    return true;
                }

                if (hash < keyHash)
                    low = mid + 1;
                else
                    high = mid - 1;
            }

            index = -1;
            return false;
        }

        private static bool ContainsUtf8IndexKey(uint keyHash, int count)
        {
            if (!_utf8Index.IsCreated || count <= 0)
                return false;

            int safeCount = math.min(count, _utf8Index.Length);
            for (int i = 0; i < safeCount; i++)
            {
                if (_utf8Index[i].StringHash == keyHash)
                    return true;
            }

            return false;
        }

        private static bool TryReadCsvLine(
            NativeArray<byte> bytes,
            int length,
            ref int cursor,
            out int lineStart,
            out int lineEnd)
        {
            lineStart = cursor;
            lineEnd = cursor;
            if (!bytes.IsCreated || cursor >= length)
                return false;

            while (cursor < length)
            {
                byte c = bytes[cursor];
                if (c == (byte)'\n' || c == (byte)'\r')
                    break;
                cursor++;
            }

            lineEnd = cursor;
            while (cursor < length && (bytes[cursor] == (byte)'\n' || bytes[cursor] == (byte)'\r'))
                cursor++;
            return true;
        }

        private static void TrimAscii(NativeArray<byte> bytes, ref int start, ref int end)
        {
            while (start < end && IsAsciiWhitespace(bytes[start]))
                start++;
            while (end > start && IsAsciiWhitespace(bytes[end - 1]))
                end--;
        }

        private static bool IsAsciiWhitespace(byte value)
        {
            return value == (byte)' ' || value == (byte)'\t';
        }

        private static bool IsCsvHeaderKey(NativeArray<byte> bytes, int start, int end)
        {
            return EqualsAsciiIgnoreCase(bytes, start, end, "hash") ||
                EqualsAsciiIgnoreCase(bytes, start, end, "key");
        }

        private static bool EqualsAsciiIgnoreCase(NativeArray<byte> bytes, int start, int end, string literal)
        {
            if (!bytes.IsCreated || literal == null || end - start != literal.Length)
                return false;

            for (int i = 0; i < literal.Length; i++)
            {
                byte left = bytes[start + i];
                if (left >= (byte)'A' && left <= (byte)'Z')
                    left = (byte)(left + 32);

                byte right = (byte)literal[i];
                if (right >= (byte)'A' && right <= (byte)'Z')
                    right = (byte)(right + 32);

                if (left != right)
                    return false;
            }

            return true;
        }

        private static int FindCsvSeparator(NativeArray<byte> bytes, int start, int end)
        {
            bool quoted = false;
            for (int i = start; i < end; i++)
            {
                byte c = bytes[i];
                if (c == (byte)'"')
                {
                    if (quoted && i + 1 < end && bytes[i + 1] == (byte)'"')
                    {
                        i++;
                        continue;
                    }

                    quoted = !quoted;
                    continue;
                }

                if (!quoted && (c == (byte)',' || c == (byte)'=' || c == (byte)';'))
                    return i;
            }

            return -1;
        }

        private static void UnquoteCsvValue(NativeArray<byte> bytes, ref int start, ref int end)
        {
            if (end - start >= 2 && bytes[start] == (byte)'"' && bytes[end - 1] == (byte)'"')
            {
                start++;
                end--;
            }
        }

        private static unsafe bool TryParseCsvHash(NativeArray<byte> bytes, int start, int end, out uint hash)
        {
            hash = 0u;
            if (start >= end)
                return false;

            if (end - start >= 2 && bytes[start] == (byte)'0' && (bytes[start + 1] == (byte)'x' || bytes[start + 1] == (byte)'X'))
                return TryParseHexUInt(bytes, start + 2, end, out hash);

            if (TryParseDecimalUInt(bytes, start, end, out hash))
                return true;

            byte* source = (byte*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(bytes);
            if (source == null)
                return false;

            hash = LocHash.ComputeUtf8AsUtf16(new ReadOnlySpan<byte>(source + start, end - start));
            return hash != 0u;
        }

        private static int CountCsvValueBytes(NativeArray<byte> bytes, int start, int end)
        {
            TrimAscii(bytes, ref start, ref end);
            if (start > end)
                return -1;

            bool quoted = end - start >= 2 && bytes[start] == (byte)'"' && bytes[end - 1] == (byte)'"';
            int cursor = quoted ? start + 1 : start;
            int limit = quoted ? end - 1 : end;
            int count = 0;
            while (cursor < limit)
            {
                if (quoted && bytes[cursor] == (byte)'"' && cursor + 1 < limit && bytes[cursor + 1] == (byte)'"')
                    cursor++;

                count++;
                cursor++;
            }

            return count;
        }

        private static unsafe bool CopyCsvValueBytes(NativeArray<byte> bytes, int start, int end, byte* destination, int expectedLength)
        {
            if (!bytes.IsCreated || destination == null || expectedLength < 0)
                return false;

            TrimAscii(bytes, ref start, ref end);
            bool quoted = end - start >= 2 && bytes[start] == (byte)'"' && bytes[end - 1] == (byte)'"';
            int cursor = quoted ? start + 1 : start;
            int limit = quoted ? end - 1 : end;
            int write = 0;
            while (cursor < limit)
            {
                byte value = bytes[cursor];
                if (quoted && value == (byte)'"' && cursor + 1 < limit && bytes[cursor + 1] == (byte)'"')
                    cursor++;

                if (write >= expectedLength)
                    return false;

                destination[write++] = value;
                cursor++;
            }

            return write == expectedLength;
        }

        private static bool TryParseHexUInt(NativeArray<byte> bytes, int start, int end, out uint value)
        {
            value = 0u;
            if (start >= end)
                return false;

            for (int i = start; i < end; i++)
            {
                byte c = bytes[i];
                uint nibble;
                if (c >= (byte)'0' && c <= (byte)'9')
                    nibble = (uint)(c - (byte)'0');
                else if (c >= (byte)'a' && c <= (byte)'f')
                    nibble = (uint)(10 + c - (byte)'a');
                else if (c >= (byte)'A' && c <= (byte)'F')
                    nibble = (uint)(10 + c - (byte)'A');
                else
                    return false;

                value = (value << 4) | nibble;
            }

            return true;
        }

        private static bool TryParseDecimalUInt(NativeArray<byte> bytes, int start, int end, out uint value)
        {
            value = 0u;
            if (start >= end)
                return false;

            for (int i = start; i < end; i++)
            {
                byte c = bytes[i];
                if (c < (byte)'0' || c > (byte)'9')
                    return false;

                value = (value * 10u) + (uint)(c - (byte)'0');
            }

            return true;
        }

        private static uint HashAsciiLower(NativeArray<byte> bytes, int start, int end)
        {
            uint hash = LocHash.FnvOffsetBasis;
            for (int i = start; i < end; i++)
            {
                byte c = bytes[i];
                if (c >= (byte)'A' && c <= (byte)'Z')
                    c = (byte)(c + 32);

                hash = unchecked((hash ^ c) * LocHash.FnvPrime);
            }

            return hash == 0u ? LocHash.FnvOffsetBasis : hash;
        }

        private static bool TryFindUtf8Slice(uint keyHash, out int2 slice, out uint searchComputeTimeNs)
        {
            long start = System.Diagnostics.Stopwatch.GetTimestamp();
            int low = 0;
            int high = _utf8Index.IsCreated ? _utf8IndexLength - 1 : -1;
            while (low <= high)
            {
                int mid = (int)(((uint)low + (uint)high) >> 1);
                LocalizationEntryDTO entry = _utf8Index[mid];
                if (entry.StringHash == keyHash)
                {
                    searchComputeTimeNs = StopwatchTicksToUIntNs(System.Diagnostics.Stopwatch.GetTimestamp() - start);
                    _lookupSearchNsThisFrame += searchComputeTimeNs;
                    slice = new int2((int)entry.ByteOffset, (int)entry.ByteLength);
                    return true;
                }

                if (entry.StringHash < keyHash)
                    low = mid + 1;
                else
                    high = mid - 1;
            }

            searchComputeTimeNs = StopwatchTicksToUIntNs(System.Diagnostics.Stopwatch.GetTimestamp() - start);
            _lookupSearchNsThisFrame += searchComputeTimeNs;
            slice = new int2(-1, 0);
            return false;
        }

        private static uint StopwatchTicksToUIntNs(long ticks)
        {
            if (ticks <= 0L)
                return 0u;

            double ns = ticks * 1000000000.0 / System.Diagnostics.Stopwatch.Frequency;
            if (ns <= 0.0)
                return 0u;
            if (ns >= uint.MaxValue)
                return uint.MaxValue;
            return (uint)ns;
        }

        private static void RefreshLookupTelemetryFrame()
        {
            int frame = Time.frameCount;
            if (_telemetryFrameIndex == frame)
                return;

            _lookupSearchNsThisFrame = 0u;
            _missingHashCountThisFrame = 0u;
            _csvOverrideAppliedThisFrame = 0u;
            _csvOverrideRejectedThisFrame = 0u;
        }

        private static void SortUtf8Index(int count)
        {
            if (!_utf8Index.IsCreated || count <= 1)
                return;

            QuickSortUtf8Index(0, math.min(count, _utf8Index.Length) - 1);
        }

        private static void QuickSortUtf8Index(int left, int right)
        {
            while (left < right)
            {
                int i = left;
                int j = right;
                uint pivot = _utf8Index[(left + right) >> 1].StringHash;
                while (i <= j)
                {
                    while (_utf8Index[i].StringHash < pivot)
                        i++;
                    while (_utf8Index[j].StringHash > pivot)
                        j--;

                    if (i <= j)
                    {
                        LocalizationEntryDTO temp = _utf8Index[i];
                        _utf8Index[i] = _utf8Index[j];
                        _utf8Index[j] = temp;
                        i++;
                        j--;
                    }
                }

                if (j - left < right - i)
                {
                    if (left < j)
                        QuickSortUtf8Index(left, j);
                    left = i;
                }
                else
                {
                    if (i < right)
                        QuickSortUtf8Index(i, right);
                    right = j;
                }
            }
        }

        private static int ResolveLookupBudgetForCurrentQuality(int requestedCount)
        {
            return BabelLookupScalability.ResolveFrameLookupBudget(
                ResolveGlobalQualityWeight(),
                math.max(0, requestedCount));
        }

        private static float ResolveGlobalQualityWeight()
        {
            float weight = HomeostasisBrain.GlobalQualityWeight;
            if (math.isfinite(weight) && weight >= 0f)
                return math.saturate(weight);

            float stress = SignalBusRegistry.SystemStress01;
            return math.saturate(1f - (math.isfinite(stress) ? stress : 1f));
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
            RefreshUtf8BytesFromVault();
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

        private static void AppendEllipsis(Span<char> buffer, ref int length, int maxGlyphs)
        {
            int limit = math.min(buffer.Length, maxGlyphs);
            if (limit < EllipsisGlyphCount)
                return;

            int safeLength = math.min(length, limit - EllipsisGlyphCount);
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
            if (_telemetryVaultBacked)
            {
                NativeArray<BabelTelemetryEntry> resolved = _telemetryFramesHandle.Resolve(_babelVault);
                if (resolved.IsCreated && resolved.Length >= BabelTelemetryFrameCapacity)
                {
                    _telemetryFrames = resolved;
                    return;
                }

                _telemetryFrames = default;
                _telemetryFramesHandle = default;
                _telemetryVaultBacked = false;
            }

            if (_telemetryFrames.IsCreated)
            {
                if (!TryResolveBabelVault(out IDataVault existingVault))
                    return;

                DisposeTelemetryState();
                if (TryAcquireTelemetryBuffer(existingVault))
                    return;
            }

            if (TryResolveBabelVault(out IDataVault vault) && TryAcquireTelemetryBuffer(vault))
                return;
        }

        private static bool TryAcquireTelemetryBuffer(IDataVault vault)
        {
            if (vault == null)
                return false;

            _telemetryFramesHandle = vault.GetBufferHandle<BabelTelemetryEntry>(
                BabelTelemetryBufferId,
                BabelTelemetryFrameCapacity,
                SystemID.UI,
                NativeArrayOptions.ClearMemory);
            NativeArray<BabelTelemetryEntry> resolved = _telemetryFramesHandle.Resolve(vault);
            if (!resolved.IsCreated || resolved.Length < BabelTelemetryFrameCapacity)
            {
                _telemetryFramesHandle = default;
                return false;
            }

            _babelVault = vault;
            _telemetryFrames = resolved;
            _telemetryVaultBacked = true;
            _telemetryRegistered = false;
            _telemetryWriteIndex = 0;
            return true;
        }

        private static void RecordTelemetry(uint keyHash, int offset, int length, ushort flags)
        {
            RecordTelemetry(keyHash, offset, length, flags, 0f, 0u);
        }

        private static void RecordTelemetry(uint keyHash, int offset, int length, ushort flags, float spanConversionTimeMs)
        {
            RecordTelemetry(keyHash, offset, length, flags, spanConversionTimeMs, 0u);
        }

        private static void RecordTelemetry(uint keyHash, int offset, int length, ushort flags, uint searchComputeTimeNs)
        {
            RecordTelemetry(keyHash, offset, length, flags, 0f, searchComputeTimeNs);
        }

        private static void RecordTelemetry(uint keyHash, int offset, int length, ushort flags, float spanConversionTimeMs, uint searchComputeTimeNs)
        {
            if (_telemetryVaultBacked)
            {
                NativeArray<BabelTelemetryEntry> resolved = _telemetryFramesHandle.Resolve(_babelVault);
                if (resolved.IsCreated && resolved.Length >= BabelTelemetryFrameCapacity)
                    _telemetryFrames = resolved;
            }

            if (!_telemetryFrames.IsCreated)
                return;

            int frame = Time.frameCount;
            if (_telemetryFrameIndex == frame)
            {
                _translationsThisFrame++;
            }
            else
            {
                _telemetryFrameIndex = frame;
                _translationsThisFrame = 1;
            }

            int slot = _telemetryWriteIndex;
            _telemetryWriteIndex++;
            if (_telemetryWriteIndex >= BabelTelemetryFrameCapacity)
                _telemetryWriteIndex = 0;

            _telemetryFrames[slot] = new BabelTelemetryEntry
            {
                Frame = frame,
                KeyHash = keyHash,
                Offset = offset,
                Length = length,
                TranslationsPerFrame = _translationsThisFrame,
                BufferPoolLeasesActive = _bufferPoolLeasesActive,
                SpanConversionTimeMs = spanConversionTimeMs,
                DictionaryLookupsPerFrame = (uint)math.max(0, _translationsThisFrame),
                MissingHashCount = _missingHashCountThisFrame,
                SearchComputeTimeNs = searchComputeTimeNs != 0u ? searchComputeTimeNs : _lookupSearchNsThisFrame,
                Language = (ushort)_activeLanguage,
                Flags = flags,
                CsvOverrideAppliedCount = _csvOverrideAppliedThisFrame,
                CsvOverrideRejectedCount = _csvOverrideRejectedThisFrame
            };

            uint effectiveSearchNs = searchComputeTimeNs != 0u ? searchComputeTimeNs : _lookupSearchNsThisFrame;
            if (effectiveSearchNs > SlowSearchDumpThresholdNs)
                WriteTelemetryDumpFiles();
        }

        private static void DumpTelemetryForSlowDecode(uint keyHash, int length, float spanConversionTimeMs)
        {
            RecordTelemetry(keyHash, -1, length, BabelTelemetryFlags.SlowDecode, spanConversionTimeMs);
            WriteTelemetryDumpFiles();
        }

        private static unsafe void DumpTelemetryForCorruption(uint keyHash, int2 badSlice)
        {
            RecordTelemetry(keyHash, badSlice.x, badSlice.y, BabelTelemetryFlags.CorruptSlice);
            WriteTelemetryDumpFiles();
        }

        private static unsafe void WriteTelemetryDumpFiles()
        {
            if (_telemetryVaultBacked)
            {
                NativeArray<BabelTelemetryEntry> resolved = _telemetryFramesHandle.Resolve(_babelVault);
                if (resolved.IsCreated && resolved.Length >= BabelTelemetryFrameCapacity)
                    _telemetryFrames = resolved;
            }

            if (!_telemetryFrames.IsCreated)
                return;

            try
            {
                string docsPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Docs", "AgentLogs"));
                Directory.CreateDirectory(docsPath);
                int byteCount = UnsafeUtility.SizeOf<BabelTelemetryEntry>() * _telemetryFrames.Length;
                byte* source = (byte*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(_telemetryFrames);
                WriteTelemetryDump(Path.Combine(docsPath, "Dump_SHINOBU_39.bin"), source, byteCount);
                WriteTelemetryDump(Path.Combine(docsPath, "Dump_BABEL_SYSTEM.bin"), source, byteCount);
                WriteTelemetryDump(Path.Combine(docsPath, "Dump_BABEL_FIXER.bin"), source, byteCount);
            }
            catch (Exception)
            {
                // Crash-path telemetry must never create a second failure.
            }
        }

        private static unsafe void WriteTelemetryDump(string dumpPath, byte* source, int byteCount)
        {
            using FileStream stream = new FileStream(dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read);
            stream.Write(new ReadOnlySpan<byte>(source, byteCount));
        }

        private static void DisposeUtf8State()
        {
            CompleteUtf8ReadersForMutation();

            if (_utf8Bytes.IsCreated)
            {
                if (_utf8BytesVaultBacked)
                {
                    _utf8Bytes = default;
                    _utf8BytesHandle = default;
                    _utf8BytesVaultBacked = false;
                    _utf8BytesRegistered = false;
                }
                else
                {
                    if (_utf8BytesRegistered)
                    {
                        NativeMemorySentinel.UnregisterNativeArray(_utf8Bytes);
                        _utf8BytesRegistered = false;
                    }

                    _utf8Bytes.Dispose();
                    _utf8Bytes = default;
                }
            }

            if (_utf8Index.IsCreated)
            {
                if (_utf8IndexVaultBacked)
                {
                    _utf8Index = default;
                    _utf8IndexHandle = default;
                    _utf8IndexVaultBacked = false;
                    _utf8IndexRegistered = false;
                }
                else
                {
                    if (_utf8IndexRegistered)
                    {
                        NativeMemorySentinel.UnregisterNativeArray(_utf8Index);
                        _utf8IndexRegistered = false;
                    }

                    _utf8Index.Dispose();
                    _utf8Index = default;
                }
            }

            _utf8IndexLength = 0;
            _utf8ByteLength = 0;
            _utf8BytesHandle = default;
            _utf8IndexHandle = default;
            _utf8BytesVaultBacked = false;
            _utf8IndexVaultBacked = false;
        }

        private static void DisposeErrorUtf8State()
        {
            if (!_errorUtf8.IsCreated)
            {
                _errorUtf8Handle = default;
                _errorUtf8VaultBacked = false;
                _errorUtf8Registered = false;
                return;
            }

            if (_errorUtf8VaultBacked)
            {
                _errorUtf8 = default;
                _errorUtf8Handle = default;
                _errorUtf8VaultBacked = false;
                _errorUtf8Registered = false;
                return;
            }

            if (_errorUtf8Registered)
            {
                NativeMemorySentinel.UnregisterNativeArray(_errorUtf8);
                _errorUtf8Registered = false;
            }

            _errorUtf8.Dispose();
            _errorUtf8 = default;
            _errorUtf8Handle = default;
        }

        private static void DisposeOverrideCsvScratch()
        {
            if (!_overrideCsvScratch.IsCreated)
                return;

            if (_overrideCsvScratchVaultBacked)
            {
                _overrideCsvScratch = default;
                _overrideCsvScratchHandle = default;
                _overrideCsvScratchVaultBacked = false;
                _overrideCsvScratchRegistered = false;
                return;
            }

            if (_overrideCsvScratchRegistered)
            {
                NativeMemorySentinel.UnregisterNativeArray(_overrideCsvScratch);
                _overrideCsvScratchRegistered = false;
            }

            _overrideCsvScratch.Dispose();
            _overrideCsvScratch = default;
            _overrideCsvScratchHandle = default;
        }

        private static void DisposeDecryptionMaskState()
        {
            if (!_decryptionMask.IsCreated)
                return;

            if (_decryptionMaskVaultBacked)
            {
                _decryptionMask = default;
                _decryptionMaskHandle = default;
                _decryptionMaskVaultBacked = false;
                _decryptionMaskRegistered = false;
                return;
            }

            if (_decryptionMaskRegistered)
            {
                NativeMemorySentinel.UnregisterNativeArray(_decryptionMask);
                _decryptionMaskRegistered = false;
            }

            _decryptionMask.Dispose();
            _decryptionMask = default;
            _decryptionMaskHandle = default;
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

            // [BLOCKING_SYNC_POINT] Structural mutation gate for CSV/staged locale writes. Callers must
            // reach this only outside lookup/decryption jobs so the active UTF-8 blob cannot be mutated
            // while reader jobs hold slices.
            _utf8ReaderHandle.Complete();
            _utf8ReaderHandle = default;
            _utf8ReaderHandleActive = false;
        }

        private static void DisposeTelemetryState()
        {
            if (!_telemetryFrames.IsCreated)
            {
                _telemetryFramesHandle = default;
                _telemetryVaultBacked = false;
                return;
            }

            if (_telemetryVaultBacked)
            {
                _telemetryFrames = default;
                _telemetryFramesHandle = default;
                _telemetryVaultBacked = false;
                _telemetryRegistered = false;
                _telemetryWriteIndex = 0;
                return;
            }

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

        [StructLayout(LayoutKind.Sequential, Size = 64)]
        private struct BabelTelemetryEntry
        {
            public int Frame;
            public uint KeyHash;
            public int Offset;
            public int Length;
            public int TranslationsPerFrame;
            public int BufferPoolLeasesActive;
            public float SpanConversionTimeMs;
            public uint DictionaryLookupsPerFrame;
            public uint MissingHashCount;
            public uint SearchComputeTimeNs;
            public ushort Language;
            public ushort Flags;
            public uint CsvOverrideAppliedCount;
            public uint CsvOverrideRejectedCount;
            public uint _pad2;
            public uint _pad3;
            public uint _pad4;
        }

        private static class BabelTelemetryFlags
        {
            public const ushort Hit = 1;
            public const ushort Miss = 2;
            public const ushort Reload = 4;
            public const ushort EmptyReload = 8;
            public const ushort CorruptSlice = 16;
            public const ushort Truncated = 32;
            public const ushort MalformedUtf8 = 64;
            public const ushort RichTextStripped = 128;
            public const ushort VariableInjected = 256;
            public const ushort SlowDecode = 512;
            public const ushort EmergencyMock = 1024;
            public const ushort AsyncStageBegin = 2048;
            public const ushort AsyncStageRejected = 4096;
            public const ushort AsyncCommitRejected = 8192;
            public const ushort AsyncBinarySwap = 16384;
            public const ushort CsvOverride = 32768;
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private struct BabelVisibleTextOffsetPrefetchJob : IJobParallelFor
        {
            [ReadOnly, NoAlias] public NativeArray<uint> VisibleHashes;
            [ReadOnly, NoAlias] public NativeArray<LocalizationEntryDTO> IndexTable;
            public int IndexCount;
            [WriteOnly, NoAlias] public NativeArray<int2> OutputSlices;

            public void Execute(int index)
            {
                uint hash = VisibleHashes[index];
                int low = 0;
                int high = math.min(IndexCount, IndexTable.Length) - 1;
                while (low <= high)
                {
                    int mid = (int)(((uint)low + (uint)high) >> 1);
                    LocalizationEntryDTO entry = IndexTable[mid];
                    if (entry.StringHash == hash)
                    {
                        OutputSlices[index] = new int2((int)entry.ByteOffset, (int)entry.ByteLength);
                        return;
                    }

                    if (entry.StringHash < hash)
                        low = mid + 1;
                    else
                        high = mid - 1;
                }

                OutputSlices[index] = new int2(-1, 0);
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        public struct BabelBinarySearchJob : IJobParallelFor
        {
            [ReadOnly, NoAlias] public NativeArray<LocalizationEntryDTO> IndexTable;
            [ReadOnly, NoAlias] public NativeArray<uint> QueryHashes;
            [WriteOnly, NoAlias] public NativeArray<int2> OutputSlices;
            public int IndexCount;

            public void Execute(int index)
            {
                uint target = QueryHashes[index];
                int low = 0;
                int high = math.min(IndexCount, IndexTable.Length) - 1;
                while (low <= high)
                {
                    int mid = (int)(((uint)low + (uint)high) >> 1);
                    LocalizationEntryDTO entry = IndexTable[mid];
                    if (entry.StringHash == target)
                    {
                        OutputSlices[index] = new int2((int)entry.ByteOffset, (int)entry.ByteLength);
                        return;
                    }

                    if (entry.StringHash < target)
                        low = mid + 1;
                    else
                        high = mid - 1;
                }

                OutputSlices[index] = new int2(-1, 0);
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        public struct MockTranslationRequestJob : IJobParallelFor
        {
            [ReadOnly, NoAlias] public NativeArray<uint> CandidateHashes;
            [WriteOnly, NoAlias] public NativeArray<MockTranslationRequestSignal> OutputSignals;
            public uint LocaleHash;
            public uint Seed;

            public void Execute(int index)
            {
                uint hash = Seed ^ ((uint)index * 747796405u);
                hash ^= hash >> 16;
                hash *= 2246822519u;
                int candidateIndex = CandidateHashes.Length > 0 ? (int)(hash % (uint)CandidateHashes.Length) : 0;
                OutputSignals[index] = new MockTranslationRequestSignal
                {
                    StringHash = CandidateHashes.Length > 0 ? CandidateHashes[candidateIndex] : 0u,
                    LocaleHash = LocaleHash,
                    OutputHandle = (uint)index
                };
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        public struct BabelRtlReverseJob : IJobParallelFor
        {
            [NoAlias] public NativeArray<char> Chars;
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
