using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Hecton8.Core;
using Hecton8.Core.Contracts;
using Hecton8.Core.Memory;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.Scripting;

namespace Hecton8.Core.Contracts.Signals
{
    /// <summary>
    /// Cold-path fallback and archaeology table for load-shedding priorities.
    /// </summary>
    [Preserve]
    public static class SignalPriorityTable
    {
        private const int MaxPriorities = 64;
        private const int RecordSizeBytes = 8;
        private const int HeaderSizeBytes = 8;
        private const int DefaultCombatPriority = 100;
        private const int DefaultFatalPriority = 255;
        private const int DefaultAudioPriority = 40;
        private const int DefaultVfxPriority = 10;
        private const uint FnvOffset = 2166136261u;
        private const uint FnvPrime = 16777619u;

        // COLD ALLOC: uint[64] - signal priority lane hashes - owner: SignalPriorityTable
        private static readonly uint[] _laneHashes = new uint[MaxPriorities];
        // COLD ALLOC: int[64] - signal priority values - owner: SignalPriorityTable
        private static readonly int[] _priorities = new int[MaxPriorities];
        private static int _count;
        private static int _initialized;
        private static int _fallbackUsed;

        /// <summary>True when hardcoded priorities were used because archaeology failed.</summary>
        public static bool FallbackUsed => _fallbackUsed != 0;

        /// <summary>Active priority row count.</summary>
        public static int Count => _count;

        /// <summary>Initializes the priority table from archived OSHINO binaries or deterministic fallback rows.</summary>
        public static void InitializeFromDisk()
        {
            if (_initialized != 0)
                return;

            _count = 0;
            _fallbackUsed = 0;
            if (!TryLoadFromArchaeology())
                ConstructFallbackSignalPriorities();

            _initialized = 1;
        }

        /// <summary>Builds safe fallback signal priorities when legacy binaries are absent or corrupt.</summary>
        public static void ConstructFallbackSignalPriorities()
        {
            _count = 0;
            UpsertPriority(ComputeLabelHash(nameof(KillSwitchSignal)), DefaultFatalPriority);
            UpsertPriority(ComputeLabelHash(nameof(SystemGlitchSignal)), DefaultFatalPriority);
            UpsertPriority(ComputeLabelHash(nameof(PlayerFatalPressureSignal)), DefaultFatalPriority);
            UpsertPriority(ComputeLabelHash(nameof(CombatDamageSignal)), DefaultCombatPriority);
            UpsertPriority(ComputeLabelHash(nameof(SignalWardenMockDamageSignal)), DefaultCombatPriority);
            UpsertPriority(ComputeLabelHash(nameof(WakeRequestSignal)), DefaultCombatPriority);
            UpsertPriority(ComputeLabelHash(nameof(CrashTelemetrySignal)), DefaultCombatPriority);
            UpsertPriority(ComputeLabelHash(nameof(InputStateSignal)), DefaultCombatPriority);
            UpsertPriority(ComputeLabelHash(nameof(AcousticPingSignal)), DefaultAudioPriority);
            UpsertPriority(ComputeLabelHash(nameof(MovementAcousticSignal)), DefaultAudioPriority);
            UpsertPriority(ComputeLabelHash(nameof(BulletTimeVisualSignal)), DefaultVfxPriority);
            UpsertPriority(ComputeLabelHash(nameof(CameraJuiceImpactSignal)), DefaultVfxPriority);
            UpsertPriority(ComputeLabelHash(nameof(DebrisSpawnSignal)), DefaultVfxPriority);
            UpsertPriority(ComputeLabelHash(nameof(MockPlayerFootstepSignal)), DefaultVfxPriority);
            UpsertPriority(ComputeLabelHash(nameof(VisualFlareSignal)), DefaultVfxPriority);
            _fallbackUsed = 1;
        }

        /// <summary>Returns the priority for a lane hash, or zero when no row exists.</summary>
        public static int GetPriority(uint laneHash)
        {
            for (int i = 0; i < _count; i++)
            {
                if (_laneHashes[i] == laneHash)
                    return _priorities[i];
            }

            return 0;
        }

        /// <summary>Applies a priority row without allocating. Used by CSV hot-swap and binary archaeology.</summary>
        public static void UpsertPriority(uint laneHash, int priority)
        {
            if (laneHash == 0u)
                return;

            int sanitized = math.clamp(priority, 0, 255);
            for (int i = 0; i < _count; i++)
            {
                if (_laneHashes[i] != laneHash)
                    continue;

                _priorities[i] = sanitized;
                return;
            }

            if (_count >= MaxPriorities)
                return;

            _laneHashes[_count] = laneHash;
            _priorities[_count] = sanitized;
            _count++;
        }

        private static bool TryLoadFromArchaeology()
        {
            bool loaded = false;
            string dataPath = Application.dataPath;
            if (string.IsNullOrEmpty(dataPath))
                return false;

            string projectRoot = Directory.GetParent(dataPath)?.FullName;
            if (string.IsNullOrEmpty(projectRoot))
                return false;

            loaded |= TryLoadDirectory(Path.Combine(projectRoot, "Docs", "Archive"));
            loaded |= TryLoadDirectory(Path.Combine(dataPath, "StreamingAssets"));
            return loaded && _count > 0;
        }

        private static bool TryLoadDirectory(string directory)
        {
            if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
                return false;

            bool loaded = false;
            string[] files;
            try
            {
                files = Directory.GetFiles(directory, "event_definitions_*.h8bin", SearchOption.AllDirectories);
            }
            catch (Exception)
            {
                return false;
            }

            for (int i = 0; i < files.Length; i++)
                loaded |= TryLoadFile(files[i]);

            return loaded;
        }

        private static bool TryLoadFile(string path)
        {
            try
            {
                using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    if (stream.Length < HeaderSizeBytes + RecordSizeBytes)
                        return false;

                    Span<byte> header = stackalloc byte[HeaderSizeBytes];
                    if (stream.Read(header) != HeaderSizeBytes)
                        return false;

                    int recordCount = math.min((int)((stream.Length - HeaderSizeBytes) / RecordSizeBytes), MaxPriorities);
                    Span<byte> record = stackalloc byte[RecordSizeBytes];
                    for (int i = 0; i < recordCount; i++)
                    {
                        if (stream.Read(record) != RecordSizeBytes)
                            break;

                        uint laneHash = ReadUInt32LittleEndian(record, 0);
                        int priority = unchecked((int)ReadUInt32LittleEndian(record, 4));
                        UpsertPriority(laneHash, priority);
                    }

                    return recordCount > 0;
                }
            }
            catch (FileNotFoundException)
            {
                return false;
            }
            catch (IOException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint ReadUInt32LittleEndian(ReadOnlySpan<byte> bytes, int offset)
        {
            return
                (uint)bytes[offset] |
                ((uint)bytes[offset + 1] << 8) |
                ((uint)bytes[offset + 2] << 16) |
                ((uint)bytes[offset + 3] << 24);
        }

        private static uint ComputeLabelHash(string label)
        {
            uint hash = FnvOffset;
            if (!string.IsNullOrEmpty(label))
            {
                for (int i = 0; i < label.Length; i++)
                {
                    hash ^= label[i];
                    hash *= FnvPrime;
                }
            }

            return hash == 0u ? 1u : hash;
        }
    }

    /// <summary>
    /// Designer-authored signal coalescing and frame-cap tuning row. Size: 32 bytes.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct SignalTuningProfile
    {
        [FieldOffset(0)] public uint LaneHash;
        [FieldOffset(4)] public int MinFrameSignals;
        [FieldOffset(8)] public int MaxFrameSignals;
        [FieldOffset(12)] public float CoalescingRadiusMeters;
        [FieldOffset(16)] public int Priority;
        [FieldOffset(20)] public uint Flags;
        [FieldOffset(24)] public ulong Reserved0;
    }

    /// <summary>
    /// Vault-backed table for cold signal_tuning_profiles.csv overrides.
    /// </summary>
    [Preserve]
    public static class SignalTuningTable
    {
        private const int MaxProfiles = 64;
        private const int CsvScratchBytes = 8192;
        private const float DefaultCoalescingRadiusMeters = 1f;
        private const BufferID ProfileBufferId = (BufferID)73040;
        private const BufferID ProfileCountBufferId = (BufferID)73041;
        private const BufferID CsvScratchBufferId = (BufferID)73042;

        private static IDataVault _vault;
        private static VaultGenerationHandle<SignalTuningProfile> _profilesHandle;
        private static VaultGenerationHandle<int> _countHandle;
        private static VaultGenerationHandle<byte> _csvScratchHandle;
        private static int _initialized;

        /// <summary>Initializes the unmanaged tuning DTO table from the global vault.</summary>
        public static void Initialize(IDataVault vault)
        {
            if (vault == null)
                return;

            if (_initialized != 0 && ReferenceEquals(_vault, vault))
            {
                if (TryOpenBuffersForOwner(vault, out _, out _, out _))
                    return;

                _initialized = 0;
            }

            _vault = vault;
            _profilesHandle = vault.EnsureGenerationHandle<SignalTuningProfile>(
                ProfileBufferId,
                MaxProfiles,
                SystemID.CoreDiagnostics,
                NativeArrayOptions.ClearMemory);
            _countHandle = vault.EnsureGenerationHandle<int>(
                ProfileCountBufferId,
                1,
                SystemID.CoreDiagnostics,
                NativeArrayOptions.ClearMemory);
            _csvScratchHandle = vault.EnsureGenerationHandle<byte>(
                CsvScratchBufferId,
                CsvScratchBytes,
                SystemID.CoreDiagnostics,
                NativeArrayOptions.UninitializedMemory);

            if (!TryOpenBuffersForOwner(vault, out _, out NativeArray<int> count, out _))
            {
                _initialized = 0;
                return;
            }

            _initialized = 1;
            count[0] = 0;
            UpsertProfile(ComputeLabelHash(nameof(AcousticPingSignal)), 16, 128, DefaultCoalescingRadiusMeters, 40);
            UpsertProfile(ComputeLabelHash(nameof(CombatDamageSignal)), 16, 128, DefaultCoalescingRadiusMeters, 100);
        }

#if UNITY_EDITOR
        /// <summary>Reads editor CSV bytes into owner scratch and exposes only a span to the parser.</summary>
        public static unsafe bool TryReadCsvBytesForLoad(string path, out ReadOnlySpan<byte> bytes)
        {
            bytes = default;
#if UNITY_EDITOR
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                return false;

            IDataVault vault = GlobalRegistry.DataVault;
            Initialize(vault);
            if (!TryOpenCsvScratchForLoad(out NativeArray<byte> scratch) || !scratch.IsCreated)
                return false;

            int bytesRead;
            try
            {
                using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    long streamLength = stream.Length;
                    if (streamLength <= 0L || streamLength > scratch.Length)
                        return false;

                    int expectedBytes = (int)streamLength;
                    Span<byte> scratchBytes = new Span<byte>(scratch.GetUnsafePtr(), expectedBytes);
                    bytesRead = 0;
                    while (bytesRead < expectedBytes)
                    {
                        int read = stream.Read(scratchBytes.Slice(bytesRead));
                        if (read <= 0)
                            return false;

                        bytesRead += read;
                    }
                }
            }
            catch (IOException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }

            bytes = new ReadOnlySpan<byte>(scratch.GetUnsafeReadOnlyPtr(), bytesRead);
            return true;
#else
            _ = path;
            return false;
#endif
        }

        /// <summary>Opens the mutable scratch buffer used by the zero-string CSV parser.</summary>
        private static bool TryOpenCsvScratchForLoad(out NativeArray<byte> scratch)
        {
            scratch = default;
            if (_initialized == 0 || _vault == null ||
                !_vault.TryResolveHandle(in _csvScratchHandle, out NativeArray<byte> csvScratch) ||
                csvScratch.Length < CsvScratchBytes)
            {
                return false;
            }

            scratch = csvScratch;
            return true;
        }
#endif

        /// <summary>Reads a tuning profile by stable lane hash without touching GlobalRegistry.</summary>
        public static bool TryGetProfile(uint laneHash, out SignalTuningProfile profile)
        {
            profile = default;
            if (laneHash == 0u ||
                _initialized == 0 ||
                !TryReadProfiles(_vault, out NativeArray<SignalTuningProfile>.ReadOnly profiles, out int count))
            {
                return false;
            }

            for (int i = 0; i < count; i++)
            {
                SignalTuningProfile candidate = profiles[i];
                if (candidate.LaneHash != laneHash)
                    continue;

                profile = candidate;
                return true;
            }

            return false;
        }

        /// <summary>Applies or replaces a tuning profile in the vault table.</summary>
        public static bool UpsertProfile(uint laneHash, int minFrameSignals, int maxFrameSignals, float coalescingRadiusMeters, int priority)
        {
            if (laneHash == 0u ||
                _initialized == 0 ||
                !TryOpenBuffersForOwner(_vault, out NativeArray<SignalTuningProfile> profiles, out NativeArray<int> countArray, out _))
            {
                return false;
            }

            int safeMin = math.clamp(minFrameSignals, 1, 4096);
            int safeMax = math.clamp(maxFrameSignals, safeMin, 4096);
            float safeRadius = math.max(0.0001f, math.isfinite(coalescingRadiusMeters) ? coalescingRadiusMeters : DefaultCoalescingRadiusMeters);
            int safePriority = math.clamp(priority, 0, 255);
            int count = math.clamp(countArray[0], 0, math.min(MaxProfiles, profiles.Length));
            for (int i = 0; i < count; i++)
            {
                SignalTuningProfile existing = profiles[i];
                if (existing.LaneHash != laneHash)
                    continue;

                existing.MinFrameSignals = safeMin;
                existing.MaxFrameSignals = safeMax;
                existing.CoalescingRadiusMeters = safeRadius;
                existing.Priority = safePriority;
                profiles[i] = existing;
                SignalPriorityTable.UpsertPriority(laneHash, safePriority);
                return true;
            }

            if (count >= MaxProfiles)
                return false;

            SignalTuningProfile profile = default;
            profile.LaneHash = laneHash;
            profile.MinFrameSignals = safeMin;
            profile.MaxFrameSignals = safeMax;
            profile.CoalescingRadiusMeters = safeRadius;
            profile.Priority = safePriority;
            profiles[count] = profile;
            countArray[0] = count + 1;
            SignalPriorityTable.UpsertPriority(laneHash, safePriority);
            return true;
        }

        private static bool TryReadProfiles(
            IDataVault vault,
            out NativeArray<SignalTuningProfile>.ReadOnly profiles,
            out int count)
        {
            profiles = default;
            count = 0;
            if (vault == null ||
                !vault.TryReadOnlyHandle(in _profilesHandle, out NativeArray<SignalTuningProfile>.ReadOnly profileArray) ||
                !vault.TryReadOnlyHandle(in _countHandle, out NativeArray<int>.ReadOnly countArray) ||
                profileArray.Length < MaxProfiles ||
                countArray.Length < 1)
            {
                return false;
            }

            profiles = profileArray;
            count = math.clamp(countArray[0], 0, math.min(MaxProfiles, profileArray.Length));
            return true;
        }

        private static bool TryOpenBuffersForOwner(
            IDataVault vault,
            out NativeArray<SignalTuningProfile> profiles,
            out NativeArray<int> count,
            out NativeArray<byte> csvScratch)
        {
            profiles = default;
            count = default;
            csvScratch = default;
            return vault != null &&
                   vault.TryResolveHandle(in _profilesHandle, out profiles) &&
                   vault.TryResolveHandle(in _countHandle, out count) &&
                   vault.TryResolveHandle(in _csvScratchHandle, out csvScratch) &&
                   profiles.Length >= MaxProfiles &&
                   count.Length >= 1 &&
                   csvScratch.Length >= CsvScratchBytes;
        }

        internal static uint ComputeLabelHash(ReadOnlySpan<byte> label)
        {
            const uint fnvOffset = 2166136261u;
            const uint fnvPrime = 16777619u;
            uint hash = fnvOffset;
            for (int i = 0; i < label.Length; i++)
            {
                byte c = label[i];
                if (c == (byte)' ' || c == (byte)'\t')
                    continue;

                hash ^= c;
                hash *= fnvPrime;
            }

            return hash == 0u ? 1u : hash;
        }

        private static uint ComputeLabelHash(string label)
        {
            const uint fnvOffset = 2166136261u;
            const uint fnvPrime = 16777619u;
            uint hash = fnvOffset;
            if (!string.IsNullOrEmpty(label))
            {
                for (int i = 0; i < label.Length; i++)
                {
                    hash ^= label[i];
                    hash *= fnvPrime;
                }
            }

            return hash == 0u ? 1u : hash;
        }
    }

#if UNITY_EDITOR
    /// <summary>
    /// Editor/source-data parser for signal_tuning_profiles.csv.
    /// Expected columns: signal,min_frame,max_frame,coalescing_radius,priority.
    /// Player runtime must consume baked binary/Vault data, not StreamingAssets text.
    /// </summary>
    [Preserve]
    public static class SignalTuningCsvHotSwap
    {
        private const string SourceDataRelativePath = "_SourceData/Signals/signal_tuning_profiles.csv";
        private const byte Comma = (byte)',';
        private const byte LineFeed = (byte)'\n';
        private const byte CarriageReturn = (byte)'\r';
        private const byte Period = (byte)'.';

        /// <summary>Loads the editor signal tuning CSV if the vault and source file are available.</summary>
        public static bool TryLoadDefault()
        {
#if UNITY_EDITOR
            string dataPath = Application.dataPath;
            if (string.IsNullOrEmpty(dataPath))
                return false;

            string path = Path.Combine(dataPath, SourceDataRelativePath);
            return TryLoad(path);
#else
            return false;
#endif
        }

        /// <summary>Loads an editor signal tuning CSV into vault-backed DTO rows.</summary>
        public static unsafe bool TryLoad(string path)
        {
#if UNITY_EDITOR
            if (!SignalTuningTable.TryReadCsvBytesForLoad(path, out ReadOnlySpan<byte> bytes))
                return false;

            return Parse(bytes);
#else
            _ = path;
            return false;
#endif
        }

        private static bool Parse(ReadOnlySpan<byte> bytes)
        {
            bool changed = false;
            int rowStart = 0;
            while (rowStart < bytes.Length)
            {
                int rowEnd = rowStart;
                while (rowEnd < bytes.Length && bytes[rowEnd] != LineFeed && bytes[rowEnd] != CarriageReturn)
                    rowEnd++;

                if (TryParseRow(bytes.Slice(rowStart, rowEnd - rowStart)))
                    changed = true;

                rowStart = rowEnd + 1;
                while (rowStart < bytes.Length && (bytes[rowStart] == LineFeed || bytes[rowStart] == CarriageReturn))
                    rowStart++;
            }

            return changed;
        }

        private static bool TryParseRow(ReadOnlySpan<byte> row)
        {
            if (row.Length <= 0 || row[0] == (byte)'#')
                return false;

            int first = IndexOf(row, Comma, 0);
            int second = IndexOf(row, Comma, first + 1);
            int third = IndexOf(row, Comma, second + 1);
            int fourth = IndexOf(row, Comma, third + 1);
            if (first <= 0 || second <= first || third <= second || fourth <= third)
                return false;

            ReadOnlySpan<byte> name = Trim(row.Slice(0, first));
            if (!TryResolveLaneHash(name, out uint laneHash))
                return false;

            if (!TryParseInt(Trim(row.Slice(first + 1, second - first - 1)), out int minSignals) ||
                !TryParseInt(Trim(row.Slice(second + 1, third - second - 1)), out int maxSignals) ||
                !TryParseFloat(Trim(row.Slice(third + 1, fourth - third - 1)), out float radiusMeters) ||
                !TryParseInt(Trim(row.Slice(fourth + 1)), out int priority))
            {
                return false;
            }

            return SignalTuningTable.UpsertProfile(laneHash, minSignals, maxSignals, radiusMeters, priority);
        }

        private static bool TryResolveLaneHash(ReadOnlySpan<byte> name, out uint laneHash)
        {
            laneHash = 0u;
            if (name.Length <= 0)
                return false;

            if (TryParseUInt(name, out uint parsed))
            {
                laneHash = parsed;
                return laneHash != 0u;
            }

            laneHash = SignalTuningTable.ComputeLabelHash(name);
            return laneHash != 0u;
        }

        private static int IndexOf(ReadOnlySpan<byte> bytes, byte target, int start)
        {
            if (start < 0)
                return -1;

            for (int i = start; i < bytes.Length; i++)
            {
                if (bytes[i] == target)
                    return i;
            }

            return -1;
        }

        private static ReadOnlySpan<byte> Trim(ReadOnlySpan<byte> bytes)
        {
            int start = 0;
            int end = bytes.Length;
            while (start < end && (bytes[start] == (byte)' ' || bytes[start] == (byte)'\t'))
                start++;
            while (end > start && (bytes[end - 1] == (byte)' ' || bytes[end - 1] == (byte)'\t'))
                end--;
            return bytes.Slice(start, end - start);
        }

        private static bool TryParseInt(ReadOnlySpan<byte> bytes, out int value)
        {
            value = 0;
            if (!TryParseUInt(bytes, out uint parsed))
                return false;

            value = unchecked((int)math.min(parsed, int.MaxValue));
            return true;
        }

        private static bool TryParseUInt(ReadOnlySpan<byte> bytes, out uint value)
        {
            value = 0u;
            if (bytes.Length <= 0)
                return false;

            int index = 0;
            bool hex = false;
            if (bytes.Length > 2 && bytes[0] == (byte)'0' && (bytes[1] == (byte)'x' || bytes[1] == (byte)'X'))
            {
                hex = true;
                index = 2;
            }

            for (; index < bytes.Length; index++)
            {
                byte c = bytes[index];
                uint digit;
                if (c >= (byte)'0' && c <= (byte)'9')
                    digit = (uint)(c - (byte)'0');
                else if (hex && c >= (byte)'a' && c <= (byte)'f')
                    digit = (uint)(c - (byte)'a' + 10);
                else if (hex && c >= (byte)'A' && c <= (byte)'F')
                    digit = (uint)(c - (byte)'A' + 10);
                else
                    return false;

                value = hex ? (value << 4) | digit : (value * 10u) + digit;
            }

            return true;
        }

        private static bool TryParseFloat(ReadOnlySpan<byte> bytes, out float value)
        {
            value = 0f;
            if (bytes.Length <= 0)
                return false;

            uint whole = 0u;
            uint fraction = 0u;
            uint fractionScale = 1u;
            bool afterPeriod = false;
            for (int i = 0; i < bytes.Length; i++)
            {
                byte c = bytes[i];
                if (c == Period && !afterPeriod)
                {
                    afterPeriod = true;
                    continue;
                }

                if (c < (byte)'0' || c > (byte)'9')
                    return false;

                uint digit = (uint)(c - (byte)'0');
                if (afterPeriod)
                {
                    fraction = (fraction * 10u) + digit;
                    fractionScale *= 10u;
                }
                else
                {
                    whole = (whole * 10u) + digit;
                }
            }

            value = whole + (fractionScale > 1u ? fraction / (float)fractionScale : 0f);
            return math.isfinite(value);
        }
    }
#endif

    /// <summary>
    /// Fixed black-box row for signal-bus throughput snapshots. Size: 64 bytes.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct SignalTelemetryFrame
    {
        [FieldOffset(0)] public uint Frame;
        [FieldOffset(4)] public uint TotalPushedSignals;
        [FieldOffset(8)] public uint PeakSignalsPerFrame;
        [FieldOffset(12)] public uint CoalescedSignals;
        [FieldOffset(16)] public uint DroppedSignals;
        [FieldOffset(20)] public uint CorruptedSignals;
        [FieldOffset(24)] public uint ActiveLaneCount;
        [FieldOffset(28)] public uint Flags;
        [FieldOffset(32)] public uint GlobalQualityMilli;
        [FieldOffset(36)] public uint SystemStressMilli;
        [FieldOffset(40)] public ulong Reserved0;
        [FieldOffset(48)] public ulong Reserved1;
        [FieldOffset(56)] public ulong Reserved2;
    }

    /// <summary>
    /// Dedicated 300-frame signal-bus black box.
    /// </summary>
    [Preserve]
    public static class SignalTelemetryRingBuffer
    {
        private const int Capacity = 300;
        private const int HeaderSizeBytes = 16;
        private const uint DumpMagic0 = 0x48454354u; // HECT
        private const uint DumpMagic1 = 0x4F4E3800u; // ON8\0
        private const string DumpPath = "Docs/AgentLogs/Dump_SIGNAL_CORRIDOR.bin";
        private const BufferID SignalTelemetryRingBufferId = (BufferID)73038;
        private const BufferID SignalTelemetryCursorBufferId = (BufferID)73039;
        private const SystemID OwnerSystemId = SystemID.CoreDiagnostics;

        private static IDataVault _vault;
        private static VaultGenerationHandle<SignalTelemetryFrame> _ringHandle;
        private static VaultGenerationHandle<int> _cursorHandle;
        private static int _initialized;

        /// <summary>Initializes the vault-backed black-box ring.</summary>
        public static void Initialize()
        {
            IDataVault vault = GlobalRegistry.DataVault;
            if (vault == null)
                return;

            if (_initialized != 0 && ReferenceEquals(_vault, vault))
            {
                if (TryReadRingFromVault(vault, out _, out _))
                    return;

                _initialized = 0;
            }

            _vault = vault;
            _ringHandle = vault.EnsureGenerationHandle<SignalTelemetryFrame>(
                SignalTelemetryRingBufferId,
                Capacity,
                OwnerSystemId,
                NativeArrayOptions.ClearMemory);
            _cursorHandle = vault.EnsureGenerationHandle<int>(
                SignalTelemetryCursorBufferId,
                1,
                OwnerSystemId,
                NativeArrayOptions.ClearMemory);

            if (!TryReadRingFromVault(vault, out _, out NativeArray<int> cursor))
            {
                _initialized = 0;
                return;
            }

            cursor[0] = 0;
            _initialized = 1;
        }

        /// <summary>Releases cached vault handles. GlobalDataVault owns backing memory.</summary>
        public static void ReleaseHandlesOnly()
        {
            _vault = null;
            _ringHandle = default;
            _cursorHandle = default;
            _initialized = 0;
        }

        /// <summary>Writes one black-box row. Call cadence is owned by the signal corridor.</summary>
        public static void ReportFrame(
            int frame,
            int totalPushedSignals,
            int peakSignals,
            int coalescedSignals,
            int droppedSignals,
            int corruptedSignals,
            int activeLaneCount,
            int globalQualityMilli,
            int systemStressMilli)
        {
            if (!TryOpenRingForOwnerWrite(out NativeArray<SignalTelemetryFrame> ring, out NativeArray<int> cursor))
                return;

            int index = math.clamp(cursor[0], 0, Capacity - 1);
            SignalTelemetryFrame entry = default;
            entry.Frame = unchecked((uint)math.max(0, frame));
            entry.TotalPushedSignals = unchecked((uint)math.max(0, totalPushedSignals));
            entry.PeakSignalsPerFrame = unchecked((uint)math.max(0, peakSignals));
            entry.CoalescedSignals = unchecked((uint)math.max(0, coalescedSignals));
            entry.DroppedSignals = unchecked((uint)math.max(0, droppedSignals));
            entry.CorruptedSignals = unchecked((uint)math.max(0, corruptedSignals));
            entry.ActiveLaneCount = unchecked((uint)math.max(0, activeLaneCount));
            entry.Flags = (droppedSignals > 0 ? 1u : 0u) |
                          (coalescedSignals > 0 ? 2u : 0u) |
                          (corruptedSignals > 0 ? 4u : 0u);
            entry.GlobalQualityMilli = unchecked((uint)math.clamp(globalQualityMilli, 0, 1000));
            entry.SystemStressMilli = unchecked((uint)math.clamp(systemStressMilli, 0, 1000));
            ring[index] = entry;

            cursor[0] = index + 1 >= Capacity ? 0 : index + 1;
        }

        /// <summary>Dumps the full signal black-box ring to Docs/AgentLogs/Dump_SIGNAL_CORRIDOR.bin.</summary>
        public static bool DumpToDisk()
        {
            if (!TryOpenRingForCrashDump(out NativeArray<SignalTelemetryFrame> ring, out _))
                return false;

            try
            {
                string root = Directory.GetParent(Application.dataPath)?.FullName;
                if (string.IsNullOrEmpty(root))
                    return false;

                string path = Path.Combine(root, DumpPath);
                string directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);

                using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))
                {
                    Span<byte> header = stackalloc byte[HeaderSizeBytes];
                    WriteUInt32LittleEndian(header, 0, DumpMagic0);
                    WriteUInt32LittleEndian(header, 4, DumpMagic1);
                    WriteUInt32LittleEndian(header, 8, Capacity);
                    WriteUInt32LittleEndian(header, 12, unchecked((uint)UnsafeUtility.SizeOf<SignalTelemetryFrame>()));
                    stream.Write(header);
                    unsafe
                    {
                        byte* ptr = (byte*)ring.GetUnsafeReadOnlyPtr();
                        int byteCount = Capacity * UnsafeUtility.SizeOf<SignalTelemetryFrame>();
                        stream.Write(new ReadOnlySpan<byte>(ptr, byteCount));
                    }
                }

                return true;
            }
            catch (IOException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
        }

        /// <summary>Copies the current vault-backed telemetry ring into an editor/runtime diagnostic buffer.</summary>
        public static int CopyFrames(NativeArray<SignalTelemetryFrame> destination)
        {
            if (!destination.IsCreated || !TryReadRing(out NativeArray<SignalTelemetryFrame> ring, out _))
                return 0;

            int count = math.min(destination.Length, Capacity);
            for (int i = 0; i < count; i++)
                destination[i] = ring[i];

            return count;
        }

        private static void WriteUInt32LittleEndian(Span<byte> bytes, int offset, uint value)
        {
            bytes[offset] = (byte)value;
            bytes[offset + 1] = (byte)(value >> 8);
            bytes[offset + 2] = (byte)(value >> 16);
            bytes[offset + 3] = (byte)(value >> 24);
        }

        private static bool TryReadRing(out NativeArray<SignalTelemetryFrame> ring, out NativeArray<int> cursor)
        {
            ring = default;
            cursor = default;
            if (_initialized == 0 || _vault == null)
                return false;

            return TryReadRingFromVault(_vault, out ring, out cursor);
        }

        private static bool TryOpenRingForOwnerWrite(out NativeArray<SignalTelemetryFrame> ring, out NativeArray<int> cursor)
        {
            ring = default;
            cursor = default;
            IDataVault vault = _vault;
            if (vault == null || _initialized == 0)
                return false;

            return TryReadRingFromVault(vault, out ring, out cursor);
        }

        private static bool TryOpenRingForCrashDump(out NativeArray<SignalTelemetryFrame> ring, out NativeArray<int> cursor)
        {
            return TryOpenRingForOwnerWrite(out ring, out cursor);
        }

        private static bool TryReadRingFromVault(IDataVault vault, out NativeArray<SignalTelemetryFrame> ring, out NativeArray<int> cursor)
        {
            ring = default;
            cursor = default;
            return vault != null &&
                   vault.TryResolveHandle(in _ringHandle, out ring) &&
                   vault.TryResolveHandle(in _cursorHandle, out cursor) &&
                   ring.Length >= Capacity &&
                   cursor.Length >= 1;
        }
    }

    /// <summary>
    /// Cold-path CSV hot swap for signal priority overrides.
    /// </summary>
    [Preserve]
    public static class SignalPriorityCsvHotSwap
    {
        private const int ScratchBytes = 4096;
        private const byte Comma = (byte)',';
        private const byte LineFeed = (byte)'\n';
        private const byte CarriageReturn = (byte)'\r';

        /// <summary>Loads signal_priorities.csv into the runtime priority table without per-row managed objects.</summary>
        public static bool TryLoad(string path)
        {
            if (string.IsNullOrEmpty(path))
                return false;

            if (!Application.isEditor && !Debug.isDebugBuild)
                return false;

            if (!File.Exists(path))
                return false;

            Span<byte> scratch = stackalloc byte[ScratchBytes];
            int bytesRead;
            try
            {
                using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    long streamLength = stream.Length;
                    if (streamLength <= 0L || streamLength > ScratchBytes)
                        return false;

                    int expectedBytes = (int)streamLength;
                    bytesRead = 0;
                    while (bytesRead < expectedBytes)
                    {
                        int read = stream.Read(scratch.Slice(bytesRead, expectedBytes - bytesRead));
                        if (read <= 0)
                            return false;

                        bytesRead += read;
                    }
                }
            }
            catch (IOException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }

            return Parse(scratch.Slice(0, bytesRead));
        }

        private static bool Parse(ReadOnlySpan<byte> bytes)
        {
            int length = bytes.Length;
            if (length <= 0)
                return false;

            bool changed = false;
            int rowStart = 0;
            while (rowStart < length)
            {
                int rowEnd = rowStart;
                while (rowEnd < length && bytes[rowEnd] != LineFeed && bytes[rowEnd] != CarriageReturn)
                    rowEnd++;

                int comma = rowStart;
                while (comma < rowEnd && bytes[comma] != Comma)
                    comma++;

                if (comma > rowStart &&
                    comma + 1 < rowEnd &&
                    TryParseUInt(bytes, rowStart, comma - rowStart, out uint laneHash) &&
                    TryParseInt(bytes, comma + 1, rowEnd - comma - 1, out int priority))
                {
                    SignalPriorityTable.UpsertPriority(laneHash, priority);
                    changed = true;
                }

                rowStart = rowEnd + 1;
                while (rowStart < length && (bytes[rowStart] == LineFeed || bytes[rowStart] == CarriageReturn))
                    rowStart++;
            }

            return changed;
        }

        private static bool TryParseInt(ReadOnlySpan<byte> bytes, int start, int length, out int value)
        {
            value = 0;
            if (!TryParseUInt(bytes, start, length, out uint parsed))
                return false;

            value = unchecked((int)math.min(parsed, int.MaxValue));
            return true;
        }

        private static bool TryParseUInt(ReadOnlySpan<byte> bytes, int start, int length, out uint value)
        {
            value = 0u;
            if (length <= 0)
                return false;

            int index = start;
            int end = start + length;
            bool hex = false;
            if (index + 1 < end && bytes[index] == (byte)'0' && (bytes[index + 1] == (byte)'x' || bytes[index + 1] == (byte)'X'))
            {
                hex = true;
                index += 2;
            }

            for (; index < end; index++)
            {
                byte c = bytes[index];
                if (c == (byte)' ' || c == (byte)'\t')
                    continue;

                uint digit;
                if (c >= (byte)'0' && c <= (byte)'9')
                    digit = (uint)(c - (byte)'0');
                else if (hex && c >= (byte)'a' && c <= (byte)'f')
                    digit = (uint)(c - (byte)'a' + 10);
                else if (hex && c >= (byte)'A' && c <= (byte)'F')
                    digit = (uint)(c - (byte)'A' + 10);
                else
                    return false;

                value = hex ? (value << 4) | digit : (value * 10u) + digit;
            }

            return true;
        }
    }

    /// <summary>Implemented by signal payloads that target a single entity id.</summary>
    public interface IEntityAddressedSignal
    {
        uint ReadEntityId();
    }

    /// <summary>Alive-mask filter compatible with the DataVault 64-entity tombstone mask convention.</summary>
    public struct EntityAliveMaskSignalFilter<T> : ISignalSnapshotFilter<T>
        where T : unmanaged, ISignal, IEntityAddressedSignal
    {
        public ulong AliveMask;

        public bool Keep(in T signal)
        {
            int bit = (int)(signal.ReadEntityId() & 63u);
            return (AliveMask & (1UL << bit)) != 0UL;
        }
    }

    /// <summary>Cold facade for applying caller-owned DataVault liveness masks to current snapshots.</summary>
    public static class SignalGhostFiltering
    {
        public static int ApplyAliveMask<T>(ulong aliveMask)
            where T : unmanaged, ISignal, IEntityAddressedSignal
        {
            EntityAliveMaskSignalFilter<T> filter = default;
            filter.AliveMask = aliveMask;
            return SignalBus<T>.FilterSnapshot(filter);
        }
    }

    /// <summary>Mock footstep payload used to prove blind signal splicing without external agents. Size: 128 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 128)]
    public struct MockPlayerFootstepSignal : ISignal, IEntityAddressedSignal
    {
        [FieldOffset(0)] public double3 Aup;
        [FieldOffset(24)] public float3 Normal;
        [FieldOffset(36)] public float Intensity01;
        [FieldOffset(40)] public uint EntityId;
        [FieldOffset(44)] public uint Frame;
        [FieldOffset(48)] public uint SurfaceHash;
        [FieldOffset(52)] private uint _surfacePad0;
        [FieldOffset(56)] private ulong _surfacePad1;
        [FieldOffset(64)] private ulong _surfacePad2;
        [FieldOffset(72)] private ulong _surfacePad3;
        [FieldOffset(80)] private ulong _surfacePad4;
        [FieldOffset(88)] private ulong _surfacePad5;
        [FieldOffset(96)] private ulong _surfacePad6;
        [FieldOffset(104)] private ulong _surfacePad7;
        [FieldOffset(112)] public byte Flags;
        [FieldOffset(113)] private byte _pad0;
        [FieldOffset(114)] private byte _pad1;
        [FieldOffset(115)] private byte _pad2;
        [FieldOffset(116)] private byte _pad3;
        [FieldOffset(117)] private byte _pad4;
        [FieldOffset(118)] private byte _pad5;
        [FieldOffset(119)] private byte _pad6;
        [FieldOffset(120)] private byte _pad7;
        [FieldOffset(121)] private byte _pad8;
        [FieldOffset(122)] private byte _pad9;
        [FieldOffset(123)] private byte _pad10;
        [FieldOffset(124)] private byte _pad11;
        [FieldOffset(125)] private byte _pad12;
        [FieldOffset(126)] private byte _pad13;
        [FieldOffset(127)] private byte _pad14;

        public uint ReadEntityId()
        {
            return EntityId;
        }
    }

    /// <summary>Signal Warden damage payload used when combat/physics producers are absent. Size: 64 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct SignalWardenMockDamageSignal : ISignal, IEntityAddressedSignal
    {
        [FieldOffset(0)] public double3 Aup;
        [FieldOffset(24)] public float3 Normal;
        [FieldOffset(36)] public float Damage;
        [FieldOffset(40)] public uint EntityId;
        [FieldOffset(44)] public byte Flags;
        [FieldOffset(45)] public byte SourceThread;
        [FieldOffset(46)] public ushort BatchId;
        [FieldOffset(48)] public uint Frame;
        [FieldOffset(52)] public uint AupCellHash;
        [FieldOffset(56)] public long OverflowSequence;

        public uint ReadEntityId()
        {
            return EntityId;
        }
    }

    /// <summary>Mock high-frequency rock collision input for the aggregation kernel. Size: 64 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct MockRockCollisionSignal : ISignal
    {
        [FieldOffset(0)] public double3 Aup;
        [FieldOffset(24)] public float Magnitude;
        [FieldOffset(28)] public uint SectorHash;
        [FieldOffset(32)] public uint Frame;
        [FieldOffset(36)] public byte Flags;
        [FieldOffset(37)] public byte SourceThread;
        [FieldOffset(38)] public ushort BatchId;
        [FieldOffset(40)] public uint AupCellHash;
        [FieldOffset(44)] public uint Reserved0;
        [FieldOffset(48)] public ulong Reserved1;
        [FieldOffset(56)] public ulong Reserved2;
    }

    /// <summary>Coalesced collision payload emitted after redundant rock impacts are merged. Size: 64 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct MacroCollisionSignal : ISignal
    {
        [FieldOffset(0)] public double3 Aup;
        [FieldOffset(24)] public float Magnitude;
        [FieldOffset(28)] public uint SectorHash;
        [FieldOffset(32)] public uint Count;
        [FieldOffset(36)] public uint Frame;
        [FieldOffset(40)] public byte Flags;
        [FieldOffset(41)] public byte SourceThread;
        [FieldOffset(42)] public ushort BatchId;
        [FieldOffset(44)] public uint AupCellHash;
        [FieldOffset(48)] public ulong Reserved0;
        [FieldOffset(56)] public ulong Reserved1;
    }

    /// <summary>
    /// Burst aggregation kernel for blind high-frequency collision compression.
    /// </summary>
    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct MockRockCollisionAggregationJob : IJob
    {
        [ReadOnly, NoAlias] public NativeArray<MockRockCollisionSignal> Input;
        [NoAlias] public NativeArray<MacroCollisionSignal> Output;
        [NoAlias] public NativeArray<int> OutputCount;
        public uint Frame;

        public void Execute()
        {
            if (!Input.IsCreated || Input.Length <= 0 || !Output.IsCreated || Output.Length <= 0 || !OutputCount.IsCreated || OutputCount.Length <= 0)
                return;

            MockRockCollisionSignal first = Input[0];
            double3 anchor = first.Aup;
            if (!math.all(math.isfinite(anchor)))
                return;

            float magnitude = 0f;
            uint count = 0u;
            uint sectorHash = first.SectorHash;
            for (int i = 0; i < Input.Length; i++)
            {
                MockRockCollisionSignal signal = Input[i];
                if (signal.SectorHash != sectorHash || !math.all(math.isfinite(signal.Aup)))
                    continue;

                double3 deltaAup = AupPrecisionMath.LocalDeltaDouble(signal.Aup, anchor);
                float3 localDelta = AupPrecisionMath.DowncastLocalDelta(deltaAup, float3.zero);
                float distanceSq = math.lengthsq(localDelta);
                if (distanceSq > 4f)
                    continue;

                magnitude += math.max(0f, signal.Magnitude);
                count++;
            }

            if (count == 0u)
                return;

            MacroCollisionSignal macro = default;
            macro.Aup = anchor;
            macro.Magnitude = magnitude;
            macro.SectorHash = sectorHash;
            macro.Count = count;
            macro.Frame = Frame;
            macro.Flags = 1;
            macro.AupCellHash = first.AupCellHash;
            Output[0] = macro;
            OutputCount[0] = 1;
        }
    }

    /// <summary>Per-worker metadata row. Size: 64 bytes; one row per worker to avoid cache-line sharing.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct SignalThreadLocalHeader64
    {
        [FieldOffset(0)] public uint Frame;
        [FieldOffset(4)] public uint BatchId;
        [FieldOffset(8)] public int WriteCursorBytes;
        [FieldOffset(12)] public int SignalCount;
        [FieldOffset(16)] public int OverflowCount;
        [FieldOffset(20)] public int DroppedCount;
        [FieldOffset(24)] public int NonFiniteCount;
        [FieldOffset(28)] public int Flags;
        [FieldOffset(32)] public int ThreadIndex;
        [FieldOffset(36)] public int ActiveStrideBytes;
        [FieldOffset(40)] public int PeakCursorBytes;
        [FieldOffset(44)] public int OrphanedFrameAge;
        [FieldOffset(48)] public ulong LastAupHash;
        [FieldOffset(56)] public ulong Reserved0;
    }

    /// <summary>300-frame black-box row for SignalBus thread contention. Size: 64 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct SignalThreadContentionTelemetryEntry
    {
        [FieldOffset(0)] public uint Frame;
        [FieldOffset(4)] public uint Flags;
        [FieldOffset(8)] public uint WrittenSignals;
        [FieldOffset(12)] public uint CoalescedSignals;
        [FieldOffset(16)] public uint DroppedSignals;
        [FieldOffset(20)] public uint OverflowSignals;
        [FieldOffset(24)] public uint NonFiniteSignals;
        [FieldOffset(28)] public uint ThreadCount;
        [FieldOffset(32)] public uint ActiveStrideBytes;
        [FieldOffset(36)] public uint PeakThreadWriteBytes;
        [FieldOffset(40)] public uint GlobalQualityMilli;
        [FieldOffset(44)] public uint VramPressureMilli;
        [FieldOffset(48)] public uint BufferIndex;
        [FieldOffset(52)] public uint BatchId;
        [FieldOffset(56)] public uint CommitMicroseconds;
        [FieldOffset(60)] public uint LastAupHashLow;
    }

    /// <summary>Vault-backed tuning row for the Signal Architecture X-Ray controls. Size: 64 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct SignalThreadContentionTuning64
    {
        [FieldOffset(0)] public uint Magic;
        [FieldOffset(4)] public uint Flags;
        [FieldOffset(8)] public float ScratchpadCapacityMultiplier;
        [FieldOffset(12)] public float CoalescenceGridSizeMeters;
        [FieldOffset(16)] public float GlobalQualityOverride01;
        [FieldOffset(20)] public float VramPressureOverride01;
        [FieldOffset(24)] public int MinStrideBytes;
        [FieldOffset(28)] public int MaxStrideBytes;
        [FieldOffset(32)] public int MaxOutputCount;
        [FieldOffset(36)] public uint TargetPlatformHash;
        [FieldOffset(40)] public ulong Reserved0;
        [FieldOffset(48)] public ulong Reserved1;
        [FieldOffset(56)] public ulong Reserved2;
    }

    /// <summary>Single cache-line header for the rare asynchronous overflow lane. Size: 64 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct SignalThreadOverflowHeader64
    {
        [FieldOffset(0)] public long WriteCursor;
        [FieldOffset(8)] public long ReadCursor;
        [FieldOffset(16)] public int DroppedCount;
        [FieldOffset(20)] public int DrainedCount;
        [FieldOffset(24)] public int Capacity;
        [FieldOffset(28)] public uint Frame;
        [FieldOffset(32)] public uint Flags;
        [FieldOffset(36)] public uint LastAupHash;
        [FieldOffset(40)] public ulong Reserved0;
        [FieldOffset(48)] public ulong Reserved1;
        [FieldOffset(56)] public ulong Reserved2;
    }

#if UNITY_EDITOR
    /// <summary>Cold boot layout guard for SHINOBU_200 cache-line DTO contracts.</summary>
    public static class SignalThreadContentionLayoutGuard
    {
        public static bool Validate()
        {
            bool valid = true;
            valid &= ValidateSize<SignalWardenMockDamageSignal>(64);
            valid &= ValidateOffset<SignalWardenMockDamageSignal>(nameof(SignalWardenMockDamageSignal.Aup), 0);
            valid &= ValidateOffset<SignalWardenMockDamageSignal>(nameof(SignalWardenMockDamageSignal.Normal), 24);
            valid &= ValidateOffset<SignalWardenMockDamageSignal>(nameof(SignalWardenMockDamageSignal.Damage), 36);
            valid &= ValidateOffset<SignalWardenMockDamageSignal>(nameof(SignalWardenMockDamageSignal.EntityId), 40);
            valid &= ValidateOffset<SignalWardenMockDamageSignal>(nameof(SignalWardenMockDamageSignal.Flags), 44);
            valid &= ValidateOffset<SignalWardenMockDamageSignal>(nameof(SignalWardenMockDamageSignal.SourceThread), 45);
            valid &= ValidateOffset<SignalWardenMockDamageSignal>(nameof(SignalWardenMockDamageSignal.BatchId), 46);
            valid &= ValidateOffset<SignalWardenMockDamageSignal>(nameof(SignalWardenMockDamageSignal.Frame), 48);
            valid &= ValidateOffset<SignalWardenMockDamageSignal>(nameof(SignalWardenMockDamageSignal.AupCellHash), 52);
            valid &= ValidateOffset<SignalWardenMockDamageSignal>(nameof(SignalWardenMockDamageSignal.OverflowSequence), 56);

            valid &= ValidateSize<MockRockCollisionSignal>(64);
            valid &= ValidateOffset<MockRockCollisionSignal>(nameof(MockRockCollisionSignal.Aup), 0);
            valid &= ValidateOffset<MockRockCollisionSignal>(nameof(MockRockCollisionSignal.Magnitude), 24);
            valid &= ValidateOffset<MockRockCollisionSignal>(nameof(MockRockCollisionSignal.SectorHash), 28);
            valid &= ValidateOffset<MockRockCollisionSignal>(nameof(MockRockCollisionSignal.Frame), 32);
            valid &= ValidateOffset<MockRockCollisionSignal>(nameof(MockRockCollisionSignal.Flags), 36);
            valid &= ValidateOffset<MockRockCollisionSignal>(nameof(MockRockCollisionSignal.SourceThread), 37);
            valid &= ValidateOffset<MockRockCollisionSignal>(nameof(MockRockCollisionSignal.BatchId), 38);
            valid &= ValidateOffset<MockRockCollisionSignal>(nameof(MockRockCollisionSignal.AupCellHash), 40);
            valid &= ValidateOffset<MockRockCollisionSignal>(nameof(MockRockCollisionSignal.Reserved0), 44);
            valid &= ValidateOffset<MockRockCollisionSignal>(nameof(MockRockCollisionSignal.Reserved1), 48);
            valid &= ValidateOffset<MockRockCollisionSignal>(nameof(MockRockCollisionSignal.Reserved2), 56);

            valid &= ValidateSize<MacroCollisionSignal>(64);
            valid &= ValidateOffset<MacroCollisionSignal>(nameof(MacroCollisionSignal.Aup), 0);
            valid &= ValidateOffset<MacroCollisionSignal>(nameof(MacroCollisionSignal.Magnitude), 24);
            valid &= ValidateOffset<MacroCollisionSignal>(nameof(MacroCollisionSignal.SectorHash), 28);
            valid &= ValidateOffset<MacroCollisionSignal>(nameof(MacroCollisionSignal.Count), 32);
            valid &= ValidateOffset<MacroCollisionSignal>(nameof(MacroCollisionSignal.Frame), 36);
            valid &= ValidateOffset<MacroCollisionSignal>(nameof(MacroCollisionSignal.Flags), 40);
            valid &= ValidateOffset<MacroCollisionSignal>(nameof(MacroCollisionSignal.SourceThread), 41);
            valid &= ValidateOffset<MacroCollisionSignal>(nameof(MacroCollisionSignal.BatchId), 42);
            valid &= ValidateOffset<MacroCollisionSignal>(nameof(MacroCollisionSignal.AupCellHash), 44);
            valid &= ValidateOffset<MacroCollisionSignal>(nameof(MacroCollisionSignal.Reserved0), 48);
            valid &= ValidateOffset<MacroCollisionSignal>(nameof(MacroCollisionSignal.Reserved1), 56);

            valid &= ValidateSize<SignalThreadLocalHeader64>(64);
            valid &= ValidateOffset<SignalThreadLocalHeader64>(nameof(SignalThreadLocalHeader64.Frame), 0);
            valid &= ValidateOffset<SignalThreadLocalHeader64>(nameof(SignalThreadLocalHeader64.BatchId), 4);
            valid &= ValidateOffset<SignalThreadLocalHeader64>(nameof(SignalThreadLocalHeader64.WriteCursorBytes), 8);
            valid &= ValidateOffset<SignalThreadLocalHeader64>(nameof(SignalThreadLocalHeader64.SignalCount), 12);
            valid &= ValidateOffset<SignalThreadLocalHeader64>(nameof(SignalThreadLocalHeader64.OverflowCount), 16);
            valid &= ValidateOffset<SignalThreadLocalHeader64>(nameof(SignalThreadLocalHeader64.DroppedCount), 20);
            valid &= ValidateOffset<SignalThreadLocalHeader64>(nameof(SignalThreadLocalHeader64.NonFiniteCount), 24);
            valid &= ValidateOffset<SignalThreadLocalHeader64>(nameof(SignalThreadLocalHeader64.Flags), 28);
            valid &= ValidateOffset<SignalThreadLocalHeader64>(nameof(SignalThreadLocalHeader64.ThreadIndex), 32);
            valid &= ValidateOffset<SignalThreadLocalHeader64>(nameof(SignalThreadLocalHeader64.ActiveStrideBytes), 36);
            valid &= ValidateOffset<SignalThreadLocalHeader64>(nameof(SignalThreadLocalHeader64.PeakCursorBytes), 40);
            valid &= ValidateOffset<SignalThreadLocalHeader64>(nameof(SignalThreadLocalHeader64.OrphanedFrameAge), 44);
            valid &= ValidateOffset<SignalThreadLocalHeader64>(nameof(SignalThreadLocalHeader64.LastAupHash), 48);
            valid &= ValidateOffset<SignalThreadLocalHeader64>(nameof(SignalThreadLocalHeader64.Reserved0), 56);

            valid &= ValidateSize<SignalThreadContentionTelemetryEntry>(64);
            valid &= ValidateOffset<SignalThreadContentionTelemetryEntry>(nameof(SignalThreadContentionTelemetryEntry.Frame), 0);
            valid &= ValidateOffset<SignalThreadContentionTelemetryEntry>(nameof(SignalThreadContentionTelemetryEntry.Flags), 4);
            valid &= ValidateOffset<SignalThreadContentionTelemetryEntry>(nameof(SignalThreadContentionTelemetryEntry.WrittenSignals), 8);
            valid &= ValidateOffset<SignalThreadContentionTelemetryEntry>(nameof(SignalThreadContentionTelemetryEntry.CoalescedSignals), 12);
            valid &= ValidateOffset<SignalThreadContentionTelemetryEntry>(nameof(SignalThreadContentionTelemetryEntry.DroppedSignals), 16);
            valid &= ValidateOffset<SignalThreadContentionTelemetryEntry>(nameof(SignalThreadContentionTelemetryEntry.OverflowSignals), 20);
            valid &= ValidateOffset<SignalThreadContentionTelemetryEntry>(nameof(SignalThreadContentionTelemetryEntry.NonFiniteSignals), 24);
            valid &= ValidateOffset<SignalThreadContentionTelemetryEntry>(nameof(SignalThreadContentionTelemetryEntry.ThreadCount), 28);
            valid &= ValidateOffset<SignalThreadContentionTelemetryEntry>(nameof(SignalThreadContentionTelemetryEntry.ActiveStrideBytes), 32);
            valid &= ValidateOffset<SignalThreadContentionTelemetryEntry>(nameof(SignalThreadContentionTelemetryEntry.PeakThreadWriteBytes), 36);
            valid &= ValidateOffset<SignalThreadContentionTelemetryEntry>(nameof(SignalThreadContentionTelemetryEntry.GlobalQualityMilli), 40);
            valid &= ValidateOffset<SignalThreadContentionTelemetryEntry>(nameof(SignalThreadContentionTelemetryEntry.VramPressureMilli), 44);
            valid &= ValidateOffset<SignalThreadContentionTelemetryEntry>(nameof(SignalThreadContentionTelemetryEntry.BufferIndex), 48);
            valid &= ValidateOffset<SignalThreadContentionTelemetryEntry>(nameof(SignalThreadContentionTelemetryEntry.BatchId), 52);
            valid &= ValidateOffset<SignalThreadContentionTelemetryEntry>(nameof(SignalThreadContentionTelemetryEntry.CommitMicroseconds), 56);
            valid &= ValidateOffset<SignalThreadContentionTelemetryEntry>(nameof(SignalThreadContentionTelemetryEntry.LastAupHashLow), 60);

            valid &= ValidateSize<SignalThreadContentionTuning64>(64);
            valid &= ValidateOffset<SignalThreadContentionTuning64>(nameof(SignalThreadContentionTuning64.Magic), 0);
            valid &= ValidateOffset<SignalThreadContentionTuning64>(nameof(SignalThreadContentionTuning64.Flags), 4);
            valid &= ValidateOffset<SignalThreadContentionTuning64>(nameof(SignalThreadContentionTuning64.ScratchpadCapacityMultiplier), 8);
            valid &= ValidateOffset<SignalThreadContentionTuning64>(nameof(SignalThreadContentionTuning64.CoalescenceGridSizeMeters), 12);
            valid &= ValidateOffset<SignalThreadContentionTuning64>(nameof(SignalThreadContentionTuning64.GlobalQualityOverride01), 16);
            valid &= ValidateOffset<SignalThreadContentionTuning64>(nameof(SignalThreadContentionTuning64.VramPressureOverride01), 20);
            valid &= ValidateOffset<SignalThreadContentionTuning64>(nameof(SignalThreadContentionTuning64.MinStrideBytes), 24);
            valid &= ValidateOffset<SignalThreadContentionTuning64>(nameof(SignalThreadContentionTuning64.MaxStrideBytes), 28);
            valid &= ValidateOffset<SignalThreadContentionTuning64>(nameof(SignalThreadContentionTuning64.MaxOutputCount), 32);
            valid &= ValidateOffset<SignalThreadContentionTuning64>(nameof(SignalThreadContentionTuning64.TargetPlatformHash), 36);
            valid &= ValidateOffset<SignalThreadContentionTuning64>(nameof(SignalThreadContentionTuning64.Reserved0), 40);
            valid &= ValidateOffset<SignalThreadContentionTuning64>(nameof(SignalThreadContentionTuning64.Reserved1), 48);
            valid &= ValidateOffset<SignalThreadContentionTuning64>(nameof(SignalThreadContentionTuning64.Reserved2), 56);

            valid &= ValidateSize<SignalThreadOverflowHeader64>(64);
            valid &= ValidateOffset<SignalThreadOverflowHeader64>(nameof(SignalThreadOverflowHeader64.WriteCursor), 0);
            valid &= ValidateOffset<SignalThreadOverflowHeader64>(nameof(SignalThreadOverflowHeader64.ReadCursor), 8);
            valid &= ValidateOffset<SignalThreadOverflowHeader64>(nameof(SignalThreadOverflowHeader64.DroppedCount), 16);
            valid &= ValidateOffset<SignalThreadOverflowHeader64>(nameof(SignalThreadOverflowHeader64.DrainedCount), 20);
            valid &= ValidateOffset<SignalThreadOverflowHeader64>(nameof(SignalThreadOverflowHeader64.Capacity), 24);
            valid &= ValidateOffset<SignalThreadOverflowHeader64>(nameof(SignalThreadOverflowHeader64.Frame), 28);
            valid &= ValidateOffset<SignalThreadOverflowHeader64>(nameof(SignalThreadOverflowHeader64.Flags), 32);
            valid &= ValidateOffset<SignalThreadOverflowHeader64>(nameof(SignalThreadOverflowHeader64.LastAupHash), 36);
            valid &= ValidateOffset<SignalThreadOverflowHeader64>(nameof(SignalThreadOverflowHeader64.Reserved0), 40);
            valid &= ValidateOffset<SignalThreadOverflowHeader64>(nameof(SignalThreadOverflowHeader64.Reserved1), 48);
            valid &= ValidateOffset<SignalThreadOverflowHeader64>(nameof(SignalThreadOverflowHeader64.Reserved2), 56);

            if (!valid)
            {
                Debug.LogError("[SignalThreadContentionLayoutGuard] layout violation");
#if UNITY_EDITOR
                throw new InvalidOperationException("Signal thread contention DTO layout violation.");
#endif
            }

            return valid;
        }

        private static bool ValidateSize<T>(int expectedBytes)
            where T : unmanaged
        {
            return UnsafeUtility.SizeOf<T>() == expectedBytes &&
                   expectedBytes > 0 &&
                   (expectedBytes & 63) == 0;
        }

        private static bool ValidateOffset<T>(string fieldName, int expectedOffset)
        {
            System.Reflection.FieldInfo field = typeof(T).GetField(
                fieldName,
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.NonPublic);
            return field != null && UnsafeUtility.GetFieldOffset(field) == expectedOffset;
        }
    }
#endif

    /// <summary>Thread-local SignalBus flags shared by jobs, editor tooling, and dump code.</summary>
    public static class SignalThreadContentionFlags
    {
        public const uint Coalesced = 1u << 0;
        public const uint Dropped = 1u << 1;
        public const uint Overflow = 1u << 2;
        public const uint NonFinite = 1u << 3;
        public const uint ExcludedFromRollbackMerkle = 1u << 4;
        public const uint OrphanedProducer = 1u << 5;
    }

    /// <summary>Write context passed into Burst producers. Native arrays are DataVault-owned aliases.</summary>
    public struct SignalThreadLocalWriteContext
    {
        // SAFETY_JUSTIFICATION_PARAGRAPH_1:
        // Bytes is indexed only through threadIndex * ThreadStrideBytes + cursor. Unity cannot infer that each
        // [NativeSetThreadIndex] producer owns a disjoint byte interval, so the parallel-for restriction is a false
        // positive for this fixed-stride worker partition.
        // SAFETY_JUSTIFICATION_PARAGRAPH_2:
        // A single NativeQueue<T>.ParallelWriter was rejected because it serializes producers through CAS. Per-worker
        // NativeArray<T> fields were rejected because the Vault route needs one contiguous byte surface for generation
        // handle resolution, ping-pong swap, and crash dumping.
        // SAFETY_JUSTIFICATION_PARAGRAPH_3:
        // The invariant is MaxThreadCount <= 64, ThreadStrideBytes fixed for the frame, and one thread index writes one
        // slice only. SignalThreadLocalCommitJob reads the previous buffer after the producer dependency, never while
        // this writer mutates Bytes.
        [NativeDisableParallelForRestriction, NoAlias] public NativeArray<byte> Bytes;
        // SAFETY_JUSTIFICATION_PARAGRAPH_1:
        // Headers is indexed by the sanitized worker thread index. Unity cannot prove that each producer writes only
        // its own 64-byte SignalThreadLocalHeader64 row, so the safety system treats the shared NativeArray as aliased.
        // SAFETY_JUSTIFICATION_PARAGRAPH_2:
        // Interlocked cursor reservation was rejected because it concentrates traffic onto one cache line. Per-thread
        // managed header objects were rejected because Burst jobs cannot carry managed references and would violate
        // zero-GC hot-path rules.
        // SAFETY_JUSTIFICATION_PARAGRAPH_3:
        // Each header row is 64 bytes and maps one-to-one with the worker index. Producer jobs write only their own row;
        // commit and telemetry read Headers only after the scheduled producer handle is complete.
        [NativeDisableParallelForRestriction, NoAlias] public NativeArray<SignalThreadLocalHeader64> Headers;
        public int ThreadStrideBytes;
        public int ActivePayloadBytesPerThread;
        public int MaxThreadCount;
        public int BufferIndex;
        public uint Frame;
        public uint BatchId;
        public float GlobalQualityWeight;
        public float VramPressure01;
        public float AupCellMeters;
        public double3 SectorOriginAup;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsValid()
        {
            long requiredBytes = (long)MaxThreadCount * ThreadStrideBytes;
            return Bytes.IsCreated &&
                   Headers.IsCreated &&
                   ThreadStrideBytes >= UnsafeUtility.SizeOf<SignalWardenMockDamageSignal>() &&
                   ActivePayloadBytesPerThread >= UnsafeUtility.SizeOf<SignalWardenMockDamageSignal>() &&
                   MaxThreadCount > 0 &&
                   requiredBytes > 0L &&
                   requiredBytes <= Bytes.Length;
        }
    }

    /// <summary>Cache-line-local writer. The fast path touches one worker-owned header and one worker-owned byte slice.</summary>
    public unsafe struct SignalThreadLocalWriter64
    {
        public SignalThreadLocalWriteContext Context;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryWrite(int rawThreadIndex, in SignalWardenMockDamageSignal signal)
        {
            if (!Context.IsValid())
                return false;

            int threadIndex = math.clamp(rawThreadIndex, 0, Context.MaxThreadCount - 1);
            if ((uint)threadIndex >= (uint)Context.Headers.Length)
                return false;

            SignalThreadLocalHeader64 header = Context.Headers[threadIndex];
            header.Frame = Context.Frame;
            header.BatchId = Context.BatchId;
            header.ThreadIndex = threadIndex;
            header.ActiveStrideBytes = Context.ActivePayloadBytesPerThread;

            SignalWardenMockDamageSignal copy = signal;
            if (!IsFinite(in copy))
            {
                header.NonFiniteCount++;
                header.Flags |= (int)SignalThreadContentionFlags.NonFinite;
                Context.Headers[threadIndex] = header;
                return false;
            }

            int payloadBytes = UnsafeUtility.SizeOf<SignalWardenMockDamageSignal>();
            int cursor = math.max(0, header.WriteCursorBytes);
            int nextCursor = cursor + payloadBytes;
            if (nextCursor > Context.ActivePayloadBytesPerThread || nextCursor > Context.ThreadStrideBytes)
            {
                header.OverflowCount++;
                header.Flags |= (int)SignalThreadContentionFlags.Overflow;
                Context.Headers[threadIndex] = header;
                return false;
            }

            long writeEnd = ((long)threadIndex * Context.ThreadStrideBytes) + nextCursor;
            if (writeEnd <= 0L || writeEnd > Context.Bytes.Length)
            {
                header.DroppedCount++;
                Context.Headers[threadIndex] = header;
                return false;
            }

            byte* basePtr = (byte*)Context.Bytes.GetUnsafePtr();
            byte* writePtr = basePtr + (threadIndex * Context.ThreadStrideBytes) + cursor;
            copy.SourceThread = (byte)math.clamp(threadIndex, 0, byte.MaxValue);
            copy.BatchId = (ushort)(Context.BatchId & ushort.MaxValue);
            copy.Frame = Context.Frame;
            copy.AupCellHash = SignalThreadLocalAupHash.ComputeCellHash(copy.Aup, Context.SectorOriginAup, Context.AupCellMeters);
            UnsafeUtility.CopyStructureToPtr(ref copy, writePtr);

            header.WriteCursorBytes = nextCursor;
            header.SignalCount++;
            header.PeakCursorBytes = math.max(header.PeakCursorBytes, nextCursor);
            header.LastAupHash = copy.AupCellHash;
            Context.Headers[threadIndex] = header;
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsFinite(in SignalWardenMockDamageSignal signal)
        {
            return math.all(math.isfinite(signal.Aup)) &&
                   math.all(math.isfinite(signal.Normal)) &&
                   math.isfinite(signal.Damage);
        }
    }

    /// <summary>AUP cell hash using a sector-relative delta before float3 quantization.</summary>
    public static class SignalThreadLocalAupHash
    {
        private const uint FnvOffset = 2166136261u;
        private const uint FnvPrime = 16777619u;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint ComputeCellHash(double3 aup, double3 sectorOriginAup, float cellMeters)
        {
            if (!math.all(math.isfinite(aup)) || !math.all(math.isfinite(sectorOriginAup)))
                return 1u;

            float safeCell = math.max(0.0001f, math.isfinite(cellMeters) ? cellMeters : 1f);
            double3 sectorRelative = aup - sectorOriginAup;
            float3 local = new float3((float)sectorRelative.x, (float)sectorRelative.y, (float)sectorRelative.z);
            if (!math.all(math.isfinite(local)))
                return 1u;

            int3 cell = (int3)math.floor(local / safeCell);
            uint hash = FnvOffset;
            hash = Fold(hash, cell.x);
            hash = Fold(hash, cell.y);
            hash = Fold(hash, cell.z);
            return hash == 0u ? 1u : hash;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint Fold(uint hash, int value)
        {
            hash ^= unchecked((uint)value);
            hash *= FnvPrime;
            return hash;
        }
    }

    /// <summary>Parallel stress generator proving worker-local writes under pathological producer fan-out.</summary>
    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public unsafe struct GenerateSignalThreadContentionMockJob : IJobParallelFor
    {
        // SAFETY_JUSTIFICATION_PARAGRAPH_1:
        // Bytes is partitioned by [NativeSetThreadIndex]. The safety system sees a shared byte array, but the write
        // address is constrained to threadIndex * ThreadStrideBytes plus that thread's private cursor.
        // SAFETY_JUSTIFICATION_PARAGRAPH_2:
        // NativeQueue<T>.ParallelWriter was rejected because this mock job exists to prove the non-CAS route. A
        // NativeArray<NativeList<T>> design was rejected because nested native containers cannot be scheduled safely
        // and would fragment Vault ownership.
        // SAFETY_JUSTIFICATION_PARAGRAPH_3:
        // The scheduler provides one physical worker thread index per producer lane, ThreadStrideBytes is constant
        // during the job, and commit reads this buffer only through the returned JobHandle dependency.
        [NativeDisableParallelForRestriction, NoAlias] public NativeArray<byte> Bytes;
        // SAFETY_JUSTIFICATION_PARAGRAPH_1:
        // Headers has one 64-byte row per thread. Unity's safety layer cannot derive the thread-index-to-row invariant
        // from [NativeSetThreadIndex], so unrestricted parallel writes are required for the worker-local cursor row.
        // SAFETY_JUSTIFICATION_PARAGRAPH_2:
        // A shared atomic cursor was rejected for false-sharing reasons. A managed per-thread dictionary was rejected
        // because Burst jobs cannot use managed collections and it would allocate.
        // SAFETY_JUSTIFICATION_PARAGRAPH_3:
        // Each Execute call clamps ThreadIndex into [0, MaxThreadCount) and writes exactly Headers[threadIndex]. No
        // other job reads or writes Headers until the producer handle feeds SignalThreadLocalCommitJob.
        [NativeDisableParallelForRestriction, NoAlias] public NativeArray<SignalThreadLocalHeader64> Headers;
        // SAFETY_JUSTIFICATION_PARAGRAPH_1:
        // OverflowSignals is touched only when a private slice is full. The shared ring slot is first reserved through
        // the 64-byte overflow header, so Unity's array-wide alias assumption is stricter than the actual slot claim.
        // SAFETY_JUSTIFICATION_PARAGRAPH_2:
        // A Unity NativeQueue fallback was rejected because it would restore opaque queue ownership outside the Vault.
        // Dropping all saturated rows was rejected because Task 11 requires a low-frequency interrupt fallback path.
        // SAFETY_JUSTIFICATION_PARAGRAPH_3:
        // A producer writes only the slot returned by TryReserveOverflowSlot and publishes OverflowSequence after the
        // 64-byte row copy. Commit drains only sequence-published slots after the producer dependency or a later frame.
        [NativeDisableParallelForRestriction, NoAlias] public NativeArray<SignalWardenMockDamageSignal> OverflowSignals;
        // SAFETY_JUSTIFICATION_PARAGRAPH_1:
        // OverflowHeader is a single 64-byte atomic control row. The safety system cannot model the CAS-protected
        // write/read cursor fields, so the container restriction would be a false positive for this bounded MPSC lane.
        // SAFETY_JUSTIFICATION_PARAGRAPH_2:
        // One header per worker was rejected because overflow must preserve global ticket order. A managed lock was
        // rejected because it is forbidden in hot paths and would stall producers under pressure.
        // SAFETY_JUSTIFICATION_PARAGRAPH_3:
        // Only Interlocked operations mutate WriteCursor, ReadCursor, DroppedCount, and publication sequence state.
        // Normal producer writes remain thread-local; this header is touched only on saturated slow-path overflow.
        [NativeDisableParallelForRestriction, NoAlias] public NativeArray<SignalThreadOverflowHeader64> OverflowHeader;
        public int ThreadStrideBytes;
        public int ActivePayloadBytesPerThread;
        public int MaxThreadCount;
        public int BufferIndex;
        public int OverflowEnabled;
        public int OverflowCapacity;
        public int SignalCount;
        public uint Seed;
        public uint Frame;
        public uint BatchId;
        public float RadiusMeters;
        public float Damage;
        public float AupCellMeters;
        public double3 OriginAup;
        public double3 SectorOriginAup;
        [NativeSetThreadIndex] public int ThreadIndex;

        public void Execute(int index)
        {
            if ((uint)index >= (uint)math.max(0, SignalCount))
                return;

            uint state = Mix(Seed ^ unchecked((uint)index * 0x9E3779B9u));
            float3 offset = new float3(NextSigned01(ref state), NextSigned01(ref state), NextSigned01(ref state)) *
                            math.max(0f, RadiusMeters);
            SignalWardenMockDamageSignal signal = default;
            signal.Aup = OriginAup + new double3(offset.x, offset.y, offset.z);
            signal.Normal = math.normalizesafe(offset, new float3(0f, 1f, 0f));
            signal.Damage = math.max(0f, Damage);
            signal.EntityId = unchecked((uint)(index + 1));
            signal.Flags = 1;

            if (!TryWrite(ThreadIndex, ref signal) && OverflowEnabled != 0)
                TryWriteOverflow(ref signal);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool TryWrite(int rawThreadIndex, ref SignalWardenMockDamageSignal signal)
        {
            if (!Bytes.IsCreated || !Headers.IsCreated)
                return false;

            int payloadBytes = UnsafeUtility.SizeOf<SignalWardenMockDamageSignal>();
            if (ThreadStrideBytes < payloadBytes || ActivePayloadBytesPerThread < payloadBytes || MaxThreadCount <= 0)
                return false;

            int threadIndex = math.clamp(rawThreadIndex, 0, MaxThreadCount - 1);
            if ((uint)threadIndex >= (uint)Headers.Length)
                return false;

            SignalThreadLocalHeader64 header = Headers[threadIndex];
            header.Frame = Frame;
            header.BatchId = BatchId;
            header.ThreadIndex = threadIndex;
            header.ActiveStrideBytes = ActivePayloadBytesPerThread;

            if (!IsFinite(in signal))
            {
                header.NonFiniteCount++;
                header.Flags |= (int)SignalThreadContentionFlags.NonFinite;
                Headers[threadIndex] = header;
                return false;
            }

            int cursor = math.max(0, header.WriteCursorBytes);
            int nextCursor = cursor + payloadBytes;
            if (nextCursor > ActivePayloadBytesPerThread || nextCursor > ThreadStrideBytes)
            {
                header.OverflowCount++;
                header.Flags |= (int)SignalThreadContentionFlags.Overflow;
                Headers[threadIndex] = header;
                return false;
            }

            long writeEnd = ((long)threadIndex * ThreadStrideBytes) + nextCursor;
            if (writeEnd <= 0L || writeEnd > Bytes.Length)
            {
                header.DroppedCount++;
                Headers[threadIndex] = header;
                return false;
            }

            byte* basePtr = (byte*)Bytes.GetUnsafePtr();
            byte* writePtr = basePtr + (threadIndex * ThreadStrideBytes) + cursor;
            signal.SourceThread = (byte)math.clamp(threadIndex, 0, byte.MaxValue);
            signal.BatchId = (ushort)(BatchId & ushort.MaxValue);
            signal.Frame = Frame;
            signal.AupCellHash = SignalThreadLocalAupHash.ComputeCellHash(signal.Aup, SectorOriginAup, AupCellMeters);
            UnsafeUtility.CopyStructureToPtr(ref signal, writePtr);

            header.WriteCursorBytes = nextCursor;
            header.SignalCount++;
            header.PeakCursorBytes = math.max(header.PeakCursorBytes, nextCursor);
            header.LastAupHash = signal.AupCellHash;
            Headers[threadIndex] = header;
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void TryWriteOverflow(ref SignalWardenMockDamageSignal signal)
        {
            if (!OverflowSignals.IsCreated || !OverflowHeader.IsCreated || OverflowHeader.Length <= 0)
                return;

            int capacity = math.min(math.max(0, OverflowCapacity), OverflowSignals.Length);
            if (capacity <= 0)
                return;

            SignalThreadOverflowHeader64* header = (SignalThreadOverflowHeader64*)OverflowHeader.GetUnsafePtr();
            if (!IsFinite(in signal))
            {
                System.Threading.Interlocked.Increment(ref header->DroppedCount);
                return;
            }

            signal.SourceThread = (byte)math.clamp(ThreadIndex, 0, byte.MaxValue);
            signal.Frame = Frame;
            signal.BatchId = (ushort)(BatchId & ushort.MaxValue);
            signal.AupCellHash = signal.AupCellHash != 0u
                ? signal.AupCellHash
                : SignalThreadLocalAupHash.ComputeCellHash(signal.Aup, SectorOriginAup, AupCellMeters);

            if (!TryReserveOverflowSlot(header, capacity, out long ticket, out int slot))
                return;

            signal.OverflowSequence = 0L;
            SignalWardenMockDamageSignal* slots = (SignalWardenMockDamageSignal*)OverflowSignals.GetUnsafePtr();
            SignalWardenMockDamageSignal* target = slots + slot;
            *target = signal;
            System.Threading.Interlocked.Exchange(ref target->OverflowSequence, ticket + 1L);
        }

        private static bool TryReserveOverflowSlot(SignalThreadOverflowHeader64* header, int capacity, out long ticket, out int slot)
        {
            ticket = 0L;
            slot = -1;
            for (int attempt = 0; attempt < 32; attempt++)
            {
                long write = AtomicRead(ref header->WriteCursor);
                long read = AtomicRead(ref header->ReadCursor);
                if (write - read >= capacity)
                {
                    System.Threading.Interlocked.Increment(ref header->DroppedCount);
                    return false;
                }

                long next = write + 1L;
                long observed = System.Threading.Interlocked.CompareExchange(ref header->WriteCursor, next, write);
                if (observed != write)
                    continue;

                ticket = write;
                slot = ModuloSlot(ticket, capacity);
                return true;
            }

            System.Threading.Interlocked.Increment(ref header->DroppedCount);
            return false;
        }

        private static int ModuloSlot(long ticket, int capacity)
        {
            long slot = ticket % capacity;
            return slot < 0L ? (int)(slot + capacity) : (int)slot;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static long AtomicRead(ref long value)
        {
            return System.Threading.Interlocked.CompareExchange(ref value, 0L, 0L);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsFinite(in SignalWardenMockDamageSignal signal)
        {
            return math.all(math.isfinite(signal.Aup)) &&
                   math.all(math.isfinite(signal.Normal)) &&
                   math.isfinite(signal.Damage);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float NextSigned01(ref uint state)
        {
            state = (state * 1664525u) + 1013904223u;
            uint mantissa = (state >> 9) | 0x3F800000u;
            return (math.asfloat(mantissa) - 1f) * 2f - 1f;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint Mix(uint value)
        {
            value ^= value >> 16;
            value *= 0x7FEB352Du;
            value ^= value >> 15;
            value *= 0x846CA68Bu;
            value ^= value >> 16;
            return value == 0u ? 1u : value;
        }
    }

    /// <summary>Serial commit from worker-local slices into a deterministic contiguous snapshot.</summary>
    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public unsafe struct SignalThreadLocalCommitJob : IJob
    {
        [ReadOnly, NoAlias] public NativeArray<byte> ReadBytes;
        [NoAlias] public NativeArray<SignalThreadLocalHeader64> ReadHeaders;
        [NoAlias] public NativeArray<SignalWardenMockDamageSignal> Output;
        [NoAlias] public NativeArray<int> OutputCount;
        [NoAlias] public NativeArray<int> CoalescenceBuckets;
        [NoAlias] public NativeArray<SignalWardenMockDamageSignal> OverflowSignals;
        [NoAlias] public NativeArray<SignalThreadOverflowHeader64> OverflowHeader;
        [NoAlias] public NativeArray<SignalThreadContentionTelemetryEntry> Telemetry;
        [NoAlias] public NativeArray<int> TelemetryCursor;
        public int ThreadStrideBytes;
        public int ActiveStrideBytes;
        public int ThreadCount;
        public int MaxOutputCount;
        public int CoalescenceBucketCount;
        public int OverflowCapacity;
        public int BufferIndex;
        public uint Frame;
        public uint BatchId;
        public float GlobalQualityWeight;
        public float VramPressure01;
        public float AupCellMeters;
        public double3 SectorOriginAup;

        public void Execute()
        {
            if (!ReadBytes.IsCreated || !ReadHeaders.IsCreated || !Output.IsCreated || !OutputCount.IsCreated || OutputCount.Length <= 0)
                return;

            int outputLimit = math.min(math.max(0, MaxOutputCount), Output.Length);
            int threadLimit = math.min(math.max(0, ThreadCount), ReadHeaders.Length);
            int bucketLimit = CoalescenceBuckets.IsCreated ? math.min(math.max(0, CoalescenceBucketCount), CoalescenceBuckets.Length) : 0;
            int payloadBytes = UnsafeUtility.SizeOf<SignalWardenMockDamageSignal>();
            if (ThreadStrideBytes < payloadBytes)
                return;

            long requiredReadBytes = (long)threadLimit * ThreadStrideBytes;
            if (requiredReadBytes <= 0L || requiredReadBytes > ReadBytes.Length)
                return;

            int outCount = 0;
            int totalSignals = 0;
            int coalesced = 0;
            int dropped = 0;
            int overflow = 0;
            int nonFinite = 0;
            int peakWriteBytes = 0;
            ulong lastAupHash = 0UL;
            byte* basePtr = (byte*)ReadBytes.GetUnsafeReadOnlyPtr();
            for (int i = 0; i < bucketLimit; i++)
                CoalescenceBuckets[i] = -1;

            for (int thread = 0; thread < threadLimit; thread++)
            {
                SignalThreadLocalHeader64 header = ReadHeaders[thread];
                int headerStrideLimit = header.ActiveStrideBytes > 0
                    ? math.min(header.ActiveStrideBytes, ThreadStrideBytes)
                    : ThreadStrideBytes;
                int bytesToRead = math.clamp(header.WriteCursorBytes, 0, headerStrideLimit);
                int signalCount = bytesToRead / payloadBytes;
                totalSignals += signalCount;
                overflow += math.max(0, header.OverflowCount);
                nonFinite += math.max(0, header.NonFiniteCount);
                peakWriteBytes = math.max(peakWriteBytes, header.PeakCursorBytes);
                lastAupHash = header.LastAupHash != 0UL ? header.LastAupHash : lastAupHash;

                byte* threadPtr = basePtr + (thread * ThreadStrideBytes);
                for (int i = 0; i < signalCount; i++)
                {
                    SignalWardenMockDamageSignal signal = *((SignalWardenMockDamageSignal*)(threadPtr + (i * payloadBytes)));
                    if (!IsFinite(in signal))
                    {
                        nonFinite++;
                        continue;
                    }

                    uint hash = signal.AupCellHash != 0u
                        ? signal.AupCellHash
                        : SignalThreadLocalAupHash.ComputeCellHash(signal.Aup, SectorOriginAup, AupCellMeters);
                    signal.AupCellHash = hash;
                    int coalescedIndex = FindCoalescedIndex(Output, CoalescenceBuckets, bucketLimit, hash);
                    if (coalescedIndex >= 0)
                    {
                        MergeSignal(Output, coalescedIndex, in signal);
                        coalesced++;
                        continue;
                    }

                    if (outCount >= outputLimit)
                    {
                        dropped++;
                        continue;
                    }

                    Output[outCount] = signal;
                    StoreCoalescenceBucket(Output, CoalescenceBuckets, bucketLimit, hash, outCount);
                    outCount++;
                    lastAupHash = hash;
                }

                header.WriteCursorBytes = 0;
                header.SignalCount = 0;
                header.OverflowCount = 0;
                header.DroppedCount = 0;
                header.NonFiniteCount = 0;
                header.PeakCursorBytes = 0;
                header.Frame = Frame;
                header.BatchId = BatchId;
                ReadHeaders[thread] = header;
            }

            DrainOverflowLane(ref outCount, outputLimit, bucketLimit, ref totalSignals, ref coalesced, ref dropped, ref overflow, ref nonFinite, ref lastAupHash);
            OutputCount[0] = outCount;
            RecordTelemetry(totalSignals, coalesced, dropped, overflow, nonFinite, peakWriteBytes, lastAupHash);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsFinite(in SignalWardenMockDamageSignal signal)
        {
            return math.all(math.isfinite(signal.Aup)) &&
                   math.all(math.isfinite(signal.Normal)) &&
                   math.isfinite(signal.Damage);
        }

        private void DrainOverflowLane(
            ref int outCount,
            int outputLimit,
            int bucketLimit,
            ref int totalSignals,
            ref int coalesced,
            ref int dropped,
            ref int overflow,
            ref int nonFinite,
            ref ulong lastAupHash)
        {
            if (!OverflowSignals.IsCreated || !OverflowHeader.IsCreated || OverflowHeader.Length <= 0)
                return;

            SignalThreadOverflowHeader64* header = (SignalThreadOverflowHeader64*)OverflowHeader.GetUnsafePtr();
            int capacity = math.min(math.max(0, OverflowCapacity), OverflowSignals.Length);
            if (capacity <= 0)
                return;

            long write = AtomicRead(ref header->WriteCursor);
            long read = AtomicRead(ref header->ReadCursor);
            long available = write - read;
            if (available < 0L)
                available = 0L;

            int availableCount = available > capacity ? capacity : (int)available;
            int overflowSlotDrops = available > capacity ? SaturateLongToInt(available - capacity) : 0;
            int recordedDrops = math.max(0, System.Threading.Interlocked.Exchange(ref header->DroppedCount, 0));
            int readCount = 0;
            SignalWardenMockDamageSignal* slots = (SignalWardenMockDamageSignal*)OverflowSignals.GetUnsafePtr();

            for (int i = 0; i < availableCount; i++)
            {
                long ticket = read + i;
                int slot = ModuloSlot(ticket, capacity);
                SignalWardenMockDamageSignal* source = slots + slot;
                long expectedSequence = ticket + 1L;
                if (AtomicRead(ref source->OverflowSequence) != expectedSequence)
                    break;

                SignalWardenMockDamageSignal signal = *source;
                signal.OverflowSequence = 0L;
                System.Threading.Interlocked.Exchange(ref source->OverflowSequence, 0L);
                readCount++;
                if (!IsFinite(in signal))
                {
                    nonFinite++;
                    continue;
                }

                uint hash = signal.AupCellHash != 0u
                    ? signal.AupCellHash
                    : SignalThreadLocalAupHash.ComputeCellHash(signal.Aup, SectorOriginAup, AupCellMeters);
                signal.AupCellHash = hash;
                totalSignals++;

                int coalescedIndex = FindCoalescedIndex(Output, CoalescenceBuckets, bucketLimit, hash);
                if (coalescedIndex >= 0)
                {
                    MergeSignal(Output, coalescedIndex, in signal);
                    coalesced++;
                    continue;
                }

                if (outCount >= outputLimit)
                {
                    dropped++;
                    continue;
                }

                Output[outCount] = signal;
                StoreCoalescenceBucket(Output, CoalescenceBuckets, bucketLimit, hash, outCount);
                outCount++;
                lastAupHash = hash;
            }

            if (readCount > 0)
                System.Threading.Interlocked.Exchange(ref header->ReadCursor, read + readCount);

            dropped += recordedDrops + overflowSlotDrops;
            overflow += readCount;
            header->DrainedCount = readCount;
            header->Capacity = capacity;
            header->Frame = Frame;
            header->Flags = readCount > 0 || overflowSlotDrops > 0 || recordedDrops > 0
                ? SignalThreadContentionFlags.Overflow
                : 0u;
            header->LastAupHash = unchecked((uint)lastAupHash);
        }

        private static int SaturateLongToInt(long value)
        {
            if (value <= 0L)
                return 0;
            return value > int.MaxValue ? int.MaxValue : (int)value;
        }

        private static int ModuloSlot(long ticket, int capacity)
        {
            long slot = ticket % capacity;
            return slot < 0L ? (int)(slot + capacity) : (int)slot;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static long AtomicRead(ref long value)
        {
            return System.Threading.Interlocked.CompareExchange(ref value, 0L, 0L);
        }

        private static int FindCoalescedIndex(NativeArray<SignalWardenMockDamageSignal> output, NativeArray<int> buckets, int bucketCount, uint hash)
        {
            if (!buckets.IsCreated || bucketCount <= 0)
                return -1;

            int slot = (int)(hash % unchecked((uint)bucketCount));
            for (int probe = 0; probe < bucketCount; probe++)
            {
                int outputIndex = buckets[slot];
                if (outputIndex < 0)
                    return -1;

                if ((uint)outputIndex < (uint)output.Length && output[outputIndex].AupCellHash == hash)
                    return outputIndex;

                slot++;
                if (slot >= bucketCount)
                    slot = 0;
            }

            return -1;
        }

        private static void StoreCoalescenceBucket(NativeArray<SignalWardenMockDamageSignal> output, NativeArray<int> buckets, int bucketCount, uint hash, int outputIndex)
        {
            if (!buckets.IsCreated || bucketCount <= 0)
                return;

            int slot = (int)(hash % unchecked((uint)bucketCount));
            for (int probe = 0; probe < bucketCount; probe++)
            {
                int existingIndex = buckets[slot];
                if (existingIndex < 0)
                {
                    buckets[slot] = outputIndex;
                    return;
                }

                if ((uint)existingIndex < (uint)output.Length && output[existingIndex].AupCellHash == hash)
                    return;

                slot++;
                if (slot >= bucketCount)
                    slot = 0;
            }
        }

        private static void MergeSignal(NativeArray<SignalWardenMockDamageSignal> output, int index, in SignalWardenMockDamageSignal signal)
        {
            SignalWardenMockDamageSignal existing = output[index];
            existing.Damage = math.max(0f, existing.Damage) + math.max(0f, signal.Damage);
            existing.Normal = math.normalizesafe(existing.Normal + signal.Normal, new float3(0f, 1f, 0f));
            existing.Flags = (byte)(existing.Flags | signal.Flags);
            existing.Frame = math.max(existing.Frame, signal.Frame);
            output[index] = existing;
        }

        private void RecordTelemetry(int totalSignals, int coalesced, int dropped, int overflow, int nonFinite, int peakWriteBytes, ulong lastAupHash)
        {
            if (!Telemetry.IsCreated || Telemetry.Length <= 0 || !TelemetryCursor.IsCreated || TelemetryCursor.Length <= 0)
                return;

            uint flags = SignalThreadContentionFlags.ExcludedFromRollbackMerkle;
            flags |= coalesced > 0 ? SignalThreadContentionFlags.Coalesced : 0u;
            flags |= dropped > 0 ? SignalThreadContentionFlags.Dropped : 0u;
            flags |= overflow > 0 ? SignalThreadContentionFlags.Overflow : 0u;
            flags |= nonFinite > 0 ? SignalThreadContentionFlags.NonFinite : 0u;
            int cursor = math.clamp(TelemetryCursor[0], 0, Telemetry.Length - 1);
            SignalThreadContentionTelemetryEntry entry = default;
            entry.Frame = Frame;
            entry.Flags = flags;
            entry.WrittenSignals = unchecked((uint)math.max(0, totalSignals));
            entry.CoalescedSignals = unchecked((uint)math.max(0, coalesced));
            entry.DroppedSignals = unchecked((uint)math.max(0, dropped));
            entry.OverflowSignals = unchecked((uint)math.max(0, overflow));
            entry.NonFiniteSignals = unchecked((uint)math.max(0, nonFinite));
            entry.ThreadCount = unchecked((uint)math.max(0, ThreadCount));
            entry.ActiveStrideBytes = unchecked((uint)math.max(0, ActiveStrideBytes));
            entry.PeakThreadWriteBytes = unchecked((uint)math.max(0, peakWriteBytes));
            entry.GlobalQualityMilli = unchecked((uint)math.clamp((int)math.round(math.saturate(GlobalQualityWeight) * 1000f), 0, 1000));
            entry.VramPressureMilli = unchecked((uint)math.clamp((int)math.round(math.saturate(VramPressure01) * 1000f), 0, 1000));
            entry.BufferIndex = unchecked((uint)math.max(0, BufferIndex));
            entry.BatchId = BatchId;
            entry.LastAupHashLow = unchecked((uint)lastAupHash);
            Telemetry[cursor] = entry;
            TelemetryCursor[0] = cursor + 1 >= Telemetry.Length ? 0 : cursor + 1;
        }
    }

    /// <summary>Marks stale worker slices without locks; used when a producer handle is lost across a domain reload.</summary>
    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct SignalThreadLocalOrphanedLockAutopsyJob : IJob
    {
        [NoAlias] public NativeArray<SignalThreadLocalHeader64> Headers;
        public uint Frame;
        public int ThreadCount;
        public int MaxFrameAge;

        public void Execute()
        {
            if (!Headers.IsCreated)
                return;

            int count = math.min(math.max(0, ThreadCount), Headers.Length);
            int maxAge = math.max(1, MaxFrameAge);
            for (int i = 0; i < count; i++)
            {
                SignalThreadLocalHeader64 header = Headers[i];
                int age = unchecked((int)(Frame - header.Frame));
                header.OrphanedFrameAge = math.max(0, age);
                if (header.WriteCursorBytes > 0 && age > maxAge)
                    header.Flags |= (int)SignalThreadContentionFlags.OrphanedProducer;
                Headers[i] = header;
            }
        }
    }

    /// <summary>DataVault-owned thread-local scratchpad for high-contention SignalBus producers.</summary>
    [Preserve]
    public static class SignalThreadLocalScratchpad
    {
        public const int MaxThreadCount = 64;
        private const int TelemetryCapacity = 300;
        private const int HeaderSizeBytes = 16;
        private const int MinThreadStrideBytes = 2048;
        private const int DefaultThreadStrideBytes = 8192;
        private const int MaxThreadStrideBytes = 16384;
        private const int MaxCommittedSignals = 4096;
        internal const int MaxCommittedSignalsForEditor = MaxCommittedSignals;
        private const int MaxOverflowSignals = 1024;
        private const int CsvScratchBytes = 8192;
        private const uint TuningMagic = 0x5343544Eu; // SCTN
        private const uint DumpMagic0 = 0x5348494Eu; // SHIN
        private const uint DumpMagic1 = 0x4F425532u; // OBU2
        private const string DumpPath = "Docs/AgentLogs/Dump_SHINOBU_200.bin";
        private const BufferID FrontBytesBufferId = (BufferID)73043;
        private const BufferID BackBytesBufferId = (BufferID)73044;
        private const BufferID FrontHeadersBufferId = (BufferID)73045;
        private const BufferID BackHeadersBufferId = (BufferID)73046;
        private const BufferID CommittedSignalsBufferId = (BufferID)73047;
        private const BufferID CommittedCountBufferId = (BufferID)73048;
        private const BufferID TelemetryRingBufferId = (BufferID)73049;
        private const BufferID TelemetryCursorBufferId = (BufferID)73050;
        private const BufferID TuningBufferId = (BufferID)73051;
        private const BufferID CoalescenceBucketsBufferId = (BufferID)73052;
        private const BufferID OverflowSignalsBufferId = (BufferID)73053;
        private const BufferID OverflowHeaderBufferId = (BufferID)73054;
        private const BufferID CsvScratchBufferId = (BufferID)73055;
        private const SystemID OwnerSystemId = SystemID.CoreDiagnostics;

        private static IDataVault _vault;
        private static VaultGenerationHandle<byte> _frontBytesHandle;
        private static VaultGenerationHandle<byte> _backBytesHandle;
        private static VaultGenerationHandle<SignalThreadLocalHeader64> _frontHeadersHandle;
        private static VaultGenerationHandle<SignalThreadLocalHeader64> _backHeadersHandle;
        private static VaultGenerationHandle<SignalWardenMockDamageSignal> _committedSignalsHandle;
        private static VaultGenerationHandle<int> _committedCountHandle;
        private static VaultGenerationHandle<SignalThreadContentionTelemetryEntry> _telemetryHandle;
        private static VaultGenerationHandle<int> _telemetryCursorHandle;
        private static VaultGenerationHandle<SignalThreadContentionTuning64> _tuningHandle;
        private static VaultGenerationHandle<int> _coalescenceBucketsHandle;
        private static VaultGenerationHandle<SignalWardenMockDamageSignal> _overflowSignalsHandle;
        private static VaultGenerationHandle<SignalThreadOverflowHeader64> _overflowHeaderHandle;
        private static VaultGenerationHandle<byte> _csvScratchHandle;
        private static int _initialized;
        private static int _writeBufferIndex;
        private static int _activeStrideBytes = DefaultThreadStrideBytes;
        private static int _csvMinStrideBytes = MinThreadStrideBytes;
        private static int _csvMaxStrideBytes = MaxThreadStrideBytes;
        private static int _csvMaxOutputCount = MaxCommittedSignals;
        private static uint _batchId = 1u;

        public static bool Initialize(IDataVault vault, float globalQualityWeight01, float vramPressure01)
        {
            if (vault == null)
                return false;

            int activeStride = ResolveActiveStrideBytes(globalQualityWeight01, vramPressure01);
            if (_initialized != 0 && ReferenceEquals(_vault, vault))
            {
                _activeStrideBytes = activeStride;
                if (AreVaultBuffersReady(vault))
                    return true;

                _initialized = 0;
            }

#if UNITY_EDITOR
            if (!SignalThreadContentionLayoutGuard.Validate())
                return false;
#endif

            _vault = vault;
            int byteCapacity = (MaxThreadCount * MaxThreadStrideBytes) + 64;
            _frontBytesHandle = vault.EnsureGenerationHandle<byte>(FrontBytesBufferId, byteCapacity, OwnerSystemId, NativeArrayOptions.UninitializedMemory);
            _backBytesHandle = vault.EnsureGenerationHandle<byte>(BackBytesBufferId, byteCapacity, OwnerSystemId, NativeArrayOptions.UninitializedMemory);
            _frontHeadersHandle = vault.EnsureGenerationHandle<SignalThreadLocalHeader64>(FrontHeadersBufferId, MaxThreadCount, OwnerSystemId, NativeArrayOptions.ClearMemory);
            _backHeadersHandle = vault.EnsureGenerationHandle<SignalThreadLocalHeader64>(BackHeadersBufferId, MaxThreadCount, OwnerSystemId, NativeArrayOptions.ClearMemory);
            _committedSignalsHandle = vault.EnsureGenerationHandle<SignalWardenMockDamageSignal>(CommittedSignalsBufferId, MaxCommittedSignals, OwnerSystemId, NativeArrayOptions.UninitializedMemory);
            _committedCountHandle = vault.EnsureGenerationHandle<int>(CommittedCountBufferId, 1, OwnerSystemId, NativeArrayOptions.ClearMemory);
            _telemetryHandle = vault.EnsureGenerationHandle<SignalThreadContentionTelemetryEntry>(TelemetryRingBufferId, TelemetryCapacity, OwnerSystemId, NativeArrayOptions.ClearMemory);
            _telemetryCursorHandle = vault.EnsureGenerationHandle<int>(TelemetryCursorBufferId, 1, OwnerSystemId, NativeArrayOptions.ClearMemory);
            _tuningHandle = vault.EnsureGenerationHandle<SignalThreadContentionTuning64>(TuningBufferId, 1, OwnerSystemId, NativeArrayOptions.ClearMemory);
            _coalescenceBucketsHandle = vault.EnsureGenerationHandle<int>(CoalescenceBucketsBufferId, MaxCommittedSignals * 2, OwnerSystemId, NativeArrayOptions.UninitializedMemory);
            _overflowSignalsHandle = vault.EnsureGenerationHandle<SignalWardenMockDamageSignal>(OverflowSignalsBufferId, MaxOverflowSignals, OwnerSystemId, NativeArrayOptions.UninitializedMemory);
            _overflowHeaderHandle = vault.EnsureGenerationHandle<SignalThreadOverflowHeader64>(OverflowHeaderBufferId, 1, OwnerSystemId, NativeArrayOptions.ClearMemory);
            _csvScratchHandle = vault.EnsureGenerationHandle<byte>(CsvScratchBufferId, CsvScratchBytes, OwnerSystemId, NativeArrayOptions.UninitializedMemory);
            _activeStrideBytes = activeStride;
            _writeBufferIndex = 0;
            _initialized = AreVaultBuffersReady(vault) ? 1 : 0;
            if (_initialized == 0)
                return false;

            TryResolve(vault, in _frontHeadersHandle, out NativeArray<SignalThreadLocalHeader64> frontHeaders);
            TryResolve(vault, in _backHeadersHandle, out NativeArray<SignalThreadLocalHeader64> backHeaders);
            TryResolve(vault, in _committedCountHandle, out NativeArray<int> committedCount);
            TryResolve(vault, in _telemetryCursorHandle, out NativeArray<int> telemetryCursor);
            TryResolve(vault, in _overflowSignalsHandle, out NativeArray<SignalWardenMockDamageSignal> overflowSignals);
            TryResolve(vault, in _overflowHeaderHandle, out NativeArray<SignalThreadOverflowHeader64> overflowHeader);
            TryResolve(vault, in _tuningHandle, out NativeArray<SignalThreadContentionTuning64> tuning);
            ClearHeaders(frontHeaders, 0u, _activeStrideBytes);
            ClearHeaders(backHeaders, 0u, _activeStrideBytes);
            if (committedCount.IsCreated && committedCount.Length > 0)
                committedCount[0] = 0;
            if (telemetryCursor.IsCreated && telemetryCursor.Length > 0)
                telemetryCursor[0] = 0;
            ClearOverflowHeader(overflowHeader, overflowSignals, 0u);
            EnsureDefaultTuning(tuning);
            return true;
        }

        public static void ReleaseHandlesOnly()
        {
            _vault = null;
            _frontBytesHandle = default;
            _backBytesHandle = default;
            _frontHeadersHandle = default;
            _backHeadersHandle = default;
            _committedSignalsHandle = default;
            _committedCountHandle = default;
            _telemetryHandle = default;
            _telemetryCursorHandle = default;
            _tuningHandle = default;
            _coalescenceBucketsHandle = default;
            _overflowSignalsHandle = default;
            _overflowHeaderHandle = default;
            _csvScratchHandle = default;
            _initialized = 0;
        }

        public static bool TryAcquireWriteContext(uint frame, double3 sectorOriginAup, out SignalThreadLocalWriteContext context)
        {
            context = default;
            if (!EnsureInitializedForOwnerRoute())
                return false;

            NativeArray<byte> bytes = default;
            NativeArray<SignalThreadLocalHeader64> headers = default;
            bool resolved = _writeBufferIndex == 0
                ? TryResolve(_vault, in _frontBytesHandle, out bytes) &&
                  TryResolve(_vault, in _frontHeadersHandle, out headers)
                : TryResolve(_vault, in _backBytesHandle, out bytes) &&
                  TryResolve(_vault, in _backHeadersHandle, out headers);
            if (!resolved)
                return false;
            if (!bytes.IsCreated || !headers.IsCreated)
                return false;

            context = default;
            context.Bytes = bytes;
            context.Headers = headers;
            context.ThreadStrideBytes = MaxThreadStrideBytes;
            context.ActivePayloadBytesPerThread = _activeStrideBytes;
            context.MaxThreadCount = MaxThreadCount;
            context.BufferIndex = _writeBufferIndex;
            context.Frame = frame;
            context.BatchId = _batchId;
            context.GlobalQualityWeight = ResolveEffectiveQuality01();
            context.VramPressure01 = ResolveEffectiveVramPressure01();
            context.AupCellMeters = ResolveCoalescenceGridMeters();
            context.SectorOriginAup = sectorOriginAup;
            return context.IsValid();
        }

        public static bool ScheduleMockContention(
            int signalCount,
            double3 originAup,
            float radiusMeters,
            float damage,
            uint frame,
            JobHandle dependency,
            out JobHandle handle)
        {
            return ScheduleMockContention(
                signalCount,
                originAup,
                originAup,
                radiusMeters,
                damage,
                frame,
                dependency,
                out handle);
        }

        public static bool ScheduleMockContention(
            int signalCount,
            double3 originAup,
            double3 sectorOriginAup,
            float radiusMeters,
            float damage,
            uint frame,
            JobHandle dependency,
            out JobHandle handle)
        {
            handle = dependency;
            if (!TryAcquireWriteContext(frame, sectorOriginAup, out SignalThreadLocalWriteContext context))
                return false;

            if (!TryResolve(_vault, in _overflowSignalsHandle, out NativeArray<SignalWardenMockDamageSignal> overflowSignals) ||
                !TryResolve(_vault, in _overflowHeaderHandle, out NativeArray<SignalThreadOverflowHeader64> overflowHeader))
            {
                return false;
            }

            GenerateSignalThreadContentionMockJob job = default;
            job.Bytes = context.Bytes;
            job.Headers = context.Headers;
            job.OverflowSignals = overflowSignals;
            job.OverflowHeader = overflowHeader;
            job.ThreadStrideBytes = context.ThreadStrideBytes;
            job.ActivePayloadBytesPerThread = context.ActivePayloadBytesPerThread;
            job.MaxThreadCount = context.MaxThreadCount;
            job.BufferIndex = context.BufferIndex;
            job.OverflowEnabled = 1;
            job.OverflowCapacity = overflowSignals.IsCreated ? overflowSignals.Length : 0;
            job.SignalCount = math.max(0, signalCount);
            job.Seed = frame ^ 0x51A200C8u;
            job.Frame = frame;
            job.BatchId = context.BatchId;
            job.RadiusMeters = math.max(0f, radiusMeters);
            job.Damage = math.max(0f, damage);
            job.AupCellMeters = context.AupCellMeters;
            job.OriginAup = originAup;
            job.SectorOriginAup = context.SectorOriginAup;
            handle = job.Schedule(math.max(0, signalCount), 64, dependency);
            H8Memory.RegisterActiveJob(OwnerSystemId, handle);
            return true;
        }

        public static bool ScheduleCommit(uint frame, JobHandle dependency, out JobHandle handle)
        {
            return ScheduleCommit(frame, double3.zero, dependency, out handle);
        }

        public static bool ScheduleCommit(uint frame, double3 sectorOriginAup, JobHandle dependency, out JobHandle handle)
        {
            handle = dependency;
            if (!EnsureInitializedForOwnerRoute())
                return false;

            int readBufferIndex = _writeBufferIndex;
            _writeBufferIndex ^= 1;
            _batchId = _batchId == uint.MaxValue ? 1u : _batchId + 1u;
            NativeArray<byte> readBytes = default;
            NativeArray<SignalThreadLocalHeader64> readHeaders = default;
            bool readResolved = readBufferIndex == 0
                ? TryResolve(_vault, in _frontBytesHandle, out readBytes) &&
                  TryResolve(_vault, in _frontHeadersHandle, out readHeaders)
                : TryResolve(_vault, in _backBytesHandle, out readBytes) &&
                  TryResolve(_vault, in _backHeadersHandle, out readHeaders);
            NativeArray<SignalThreadLocalHeader64> newWriteHeaders = default;
            bool writeResolved = _writeBufferIndex == 0
                ? TryResolve(_vault, in _frontHeadersHandle, out newWriteHeaders)
                : TryResolve(_vault, in _backHeadersHandle, out newWriteHeaders);
            if (!readResolved ||
                !writeResolved ||
                !TryResolve(_vault, in _committedSignalsHandle, out NativeArray<SignalWardenMockDamageSignal> committedSignals) ||
                !TryResolve(_vault, in _committedCountHandle, out NativeArray<int> committedCount) ||
                !TryResolve(_vault, in _coalescenceBucketsHandle, out NativeArray<int> coalescenceBuckets) ||
                !TryResolve(_vault, in _overflowSignalsHandle, out NativeArray<SignalWardenMockDamageSignal> overflowSignals) ||
                !TryResolve(_vault, in _overflowHeaderHandle, out NativeArray<SignalThreadOverflowHeader64> overflowHeader) ||
                !TryResolve(_vault, in _telemetryHandle, out NativeArray<SignalThreadContentionTelemetryEntry> telemetry) ||
                !TryResolve(_vault, in _telemetryCursorHandle, out NativeArray<int> telemetryCursor))
            {
                return false;
            }

            ClearHeaders(newWriteHeaders, frame, _activeStrideBytes);

            SignalThreadLocalCommitJob job = default;
            job.ReadBytes = readBytes;
            job.ReadHeaders = readHeaders;
            job.Output = committedSignals;
            job.OutputCount = committedCount;
            job.CoalescenceBuckets = coalescenceBuckets;
            job.OverflowSignals = overflowSignals;
            job.OverflowHeader = overflowHeader;
            job.Telemetry = telemetry;
            job.TelemetryCursor = telemetryCursor;
            job.ThreadStrideBytes = MaxThreadStrideBytes;
            job.ActiveStrideBytes = _activeStrideBytes;
            job.ThreadCount = MaxThreadCount;
            job.MaxOutputCount = math.min(_csvMaxOutputCount, MaxCommittedSignals);
            job.CoalescenceBucketCount = math.min(_csvMaxOutputCount * 2, coalescenceBuckets.IsCreated ? coalescenceBuckets.Length : 0);
            job.OverflowCapacity = overflowSignals.IsCreated ? overflowSignals.Length : 0;
            job.BufferIndex = readBufferIndex;
            job.Frame = frame;
            job.BatchId = _batchId;
            job.GlobalQualityWeight = ResolveEffectiveQuality01();
            job.VramPressure01 = ResolveEffectiveVramPressure01();
            job.AupCellMeters = ResolveCoalescenceGridMeters();
            job.SectorOriginAup = sectorOriginAup;
            handle = job.Schedule(dependency);
            H8Memory.RegisterActiveJob(OwnerSystemId, handle);
            return true;
        }

        public static unsafe bool TryPushAsynchronousOverflow(in SignalWardenMockDamageSignal signal, double3 sectorOriginAup)
        {
            if (!EnsureInitializedForOwnerRoute() ||
                !TryResolve(_vault, in _overflowSignalsHandle, out NativeArray<SignalWardenMockDamageSignal> overflowSignals) ||
                !TryResolve(_vault, in _overflowHeaderHandle, out NativeArray<SignalThreadOverflowHeader64> overflowHeader) ||
                overflowHeader.Length <= 0)
            {
                return false;
            }

            int capacity = overflowSignals.Length;
            if (capacity <= 0)
                return false;

            SignalThreadOverflowHeader64* header = (SignalThreadOverflowHeader64*)overflowHeader.GetUnsafePtr();
            if (!IsFinite(in signal))
            {
                System.Threading.Interlocked.Increment(ref header->DroppedCount);
                return false;
            }

            if (!TryReserveOverflowSlot(header, capacity, out long ticket, out int slot))
                return false;

            SignalWardenMockDamageSignal copy = signal;
            copy.AupCellHash = copy.AupCellHash != 0u
                ? copy.AupCellHash
                : SignalThreadLocalAupHash.ComputeCellHash(copy.Aup, sectorOriginAup, ResolveCoalescenceGridMeters());
            copy.OverflowSequence = 0L;
            SignalWardenMockDamageSignal* slots = (SignalWardenMockDamageSignal*)overflowSignals.GetUnsafePtr();
            SignalWardenMockDamageSignal* target = slots + slot;
            *target = copy;
            System.Threading.Interlocked.Exchange(ref target->OverflowSequence, ticket + 1L);
            return true;
        }

        private static unsafe bool TryReserveOverflowSlot(SignalThreadOverflowHeader64* header, int capacity, out long ticket, out int slot)
        {
            ticket = 0L;
            slot = -1;
            for (int attempt = 0; attempt < 32; attempt++)
            {
                long write = AtomicRead(ref header->WriteCursor);
                long read = AtomicRead(ref header->ReadCursor);
                if (write - read >= capacity)
                {
                    System.Threading.Interlocked.Increment(ref header->DroppedCount);
                    return false;
                }

                long next = write + 1L;
                long observed = System.Threading.Interlocked.CompareExchange(ref header->WriteCursor, next, write);
                if (observed != write)
                    continue;

                ticket = write;
                slot = ModuloSlot(ticket, capacity);
                return true;
            }

            System.Threading.Interlocked.Increment(ref header->DroppedCount);
            return false;
        }

        private static int ModuloSlot(long ticket, int capacity)
        {
            long slot = ticket % capacity;
            return slot < 0L ? (int)(slot + capacity) : (int)slot;
        }

        private static long AtomicRead(ref long value)
        {
            return System.Threading.Interlocked.CompareExchange(ref value, 0L, 0L);
        }

        public static bool ScheduleOrphanedLockAutopsy(uint frame, int maxFrameAge, JobHandle dependency, out JobHandle handle)
        {
            handle = dependency;
            if (!EnsureInitializedForOwnerRoute())
                return false;

            NativeArray<SignalThreadLocalHeader64> headers = default;
            bool resolved = _writeBufferIndex == 0
                ? TryResolve(_vault, in _frontHeadersHandle, out headers)
                : TryResolve(_vault, in _backHeadersHandle, out headers);
            if (!resolved)
                return false;

            SignalThreadLocalOrphanedLockAutopsyJob job = default;
            job.Headers = headers;
            job.Frame = frame;
            job.ThreadCount = MaxThreadCount;
            job.MaxFrameAge = math.max(1, maxFrameAge);
            handle = job.Schedule(dependency);
            H8Memory.RegisterActiveJob(OwnerSystemId, handle);
            return true;
        }

        private static bool TryOpenCommittedSignalsForOwner(out NativeArray<SignalWardenMockDamageSignal> signals, out int count)
        {
            return TryOpenCommittedSignalsBufferForOwner(out signals, out count);
        }

        /// <summary>Returns a read-only view of the finalized mock signal snapshot for consumers and editor diagnostics.</summary>
        public static bool TryGetCommittedSignalsReadOnly(out NativeArray<SignalWardenMockDamageSignal>.ReadOnly signals, out int count)
        {
            signals = default;
            count = 0;
            if (!TryReadCommittedSignalsBuffer(out NativeArray<SignalWardenMockDamageSignal>.ReadOnly committedSignals, out count))
                return false;

            signals = committedSignals;
            return true;
        }

        public static bool TryGetLatestTelemetry(out SignalThreadContentionTelemetryEntry entry)
        {
            entry = default;
            if (!IsInitializedForRead() ||
                !TryRead(_vault, in _telemetryHandle, out NativeArray<SignalThreadContentionTelemetryEntry>.ReadOnly telemetry) ||
                !TryRead(_vault, in _telemetryCursorHandle, out NativeArray<int>.ReadOnly telemetryCursor) ||
                telemetryCursor.Length <= 0)
            {
                return false;
            }

            int index = telemetryCursor[0] - 1;
            if (index < 0)
                index = telemetry.Length - 1;
            entry = telemetry[index];
            return entry.Frame != 0u || entry.WrittenSignals != 0u || entry.Flags != 0u;
        }

        /// <summary>Returns a read-only view of the 300-frame contention telemetry ring.</summary>
        public static bool TryGetTelemetryReadOnly(out NativeArray<SignalThreadContentionTelemetryEntry>.ReadOnly telemetry, out int cursor)
        {
            telemetry = default;
            cursor = 0;
            if (!IsInitializedForRead() ||
                !TryRead(_vault, in _telemetryHandle, out NativeArray<SignalThreadContentionTelemetryEntry>.ReadOnly telemetryArray) ||
                !TryRead(_vault, in _telemetryCursorHandle, out NativeArray<int>.ReadOnly telemetryCursor) ||
                telemetryCursor.Length <= 0 ||
                telemetryArray.Length <= 0)
            {
                return false;
            }

            telemetry = telemetryArray;
            cursor = math.clamp(telemetryCursor[0], 0, telemetryArray.Length - 1);
            return true;
        }

        public static void RecordLastCommitMicroseconds(uint microseconds)
        {
            if (!EnsureInitializedForOwnerRoute() ||
                !TryResolve(_vault, in _telemetryHandle, out NativeArray<SignalThreadContentionTelemetryEntry> telemetry) ||
                !TryResolve(_vault, in _telemetryCursorHandle, out NativeArray<int> telemetryCursor) ||
                telemetryCursor.Length <= 0)
            {
                return;
            }

            int index = telemetryCursor[0] - 1;
            if (index < 0)
                index = telemetry.Length - 1;
            SignalThreadContentionTelemetryEntry entry = telemetry[index];
            entry.CommitMicroseconds = microseconds;
            telemetry[index] = entry;
        }

        public static bool TryGetThreadHeader(int threadIndex, out SignalThreadLocalHeader64 header)
        {
            header = default;
            if (!IsInitializedForRead())
                return false;

            NativeArray<SignalThreadLocalHeader64>.ReadOnly headers = default;
            bool resolved = _writeBufferIndex == 0
                ? TryRead(_vault, in _frontHeadersHandle, out headers)
                : TryRead(_vault, in _backHeadersHandle, out headers);
            if (!resolved)
                return false;

            if ((uint)threadIndex >= (uint)headers.Length)
                return false;

            header = headers[threadIndex];
            return true;
        }

        public static bool ApplyCsvTuning(int minStrideBytes, int maxStrideBytes, int maxOutputCount)
        {
            return ApplyCsvTuning(minStrideBytes, maxStrideBytes, maxOutputCount, 0u);
        }

        public static bool ApplyCsvTuning(int minStrideBytes, int maxStrideBytes, int maxOutputCount, uint targetPlatformHash)
        {
            int safeMin = Align64(math.clamp(minStrideBytes, MinThreadStrideBytes, MaxThreadStrideBytes));
            int safeMax = Align64(math.clamp(maxStrideBytes, safeMin, MaxThreadStrideBytes));
            _csvMinStrideBytes = safeMin;
            _csvMaxStrideBytes = safeMax;
            _csvMaxOutputCount = math.clamp(maxOutputCount, 1, MaxCommittedSignals);
            _activeStrideBytes = math.clamp(_activeStrideBytes, _csvMinStrideBytes, _csvMaxStrideBytes);
            MutateTuning(ResolveCapacityMultiplier(), ResolveCoalescenceGridMeters(), ResolveEffectiveQuality01(), ResolveEffectiveVramPressure01(), safeMin, safeMax, _csvMaxOutputCount, targetPlatformHash);
            return true;
        }

        public static unsafe bool MutateTuning(
            float scratchpadCapacityMultiplier,
            float coalescenceGridSizeMeters,
            float globalQualityOverride01,
            float vramPressureOverride01,
            int minStrideBytes,
            int maxStrideBytes,
            int maxOutputCount,
            uint targetPlatformHash)
        {
            if (!EnsureInitializedForOwnerRoute() ||
                !TryResolve(_vault, in _tuningHandle, out NativeArray<SignalThreadContentionTuning64> tuningArray) ||
                tuningArray.Length <= 0)
            {
                return false;
            }

            SignalThreadContentionTuning64* ptr = (SignalThreadContentionTuning64*)tuningArray.GetUnsafePtr();
            ref SignalThreadContentionTuning64 tuning = ref UnsafeUtility.AsRef<SignalThreadContentionTuning64>(ptr);
            tuning.Magic = TuningMagic;
            tuning.ScratchpadCapacityMultiplier = math.max(0.125f, math.isfinite(scratchpadCapacityMultiplier) ? scratchpadCapacityMultiplier : 1f);
            tuning.CoalescenceGridSizeMeters = math.max(0.125f, math.isfinite(coalescenceGridSizeMeters) ? coalescenceGridSizeMeters : 1f);
            tuning.GlobalQualityOverride01 = math.saturate(math.isfinite(globalQualityOverride01) ? globalQualityOverride01 : SignalBusRegistry.GlobalQualityWeight01);
            tuning.VramPressureOverride01 = math.saturate(math.isfinite(vramPressureOverride01) ? vramPressureOverride01 : ResolveEffectiveVramPressure01());
            tuning.MinStrideBytes = Align64(math.clamp(minStrideBytes, MinThreadStrideBytes, MaxThreadStrideBytes));
            tuning.MaxStrideBytes = Align64(math.clamp(maxStrideBytes, tuning.MinStrideBytes, MaxThreadStrideBytes));
            tuning.MaxOutputCount = math.clamp(maxOutputCount, 1, MaxCommittedSignals);
            tuning.TargetPlatformHash = targetPlatformHash;
            _csvMinStrideBytes = tuning.MinStrideBytes;
            _csvMaxStrideBytes = tuning.MaxStrideBytes;
            _csvMaxOutputCount = tuning.MaxOutputCount;
            return true;
        }

        public static bool TryGetTuning(out SignalThreadContentionTuning64 tuning)
        {
            tuning = default;
            if (!IsInitializedForRead() ||
                !TryRead(_vault, in _tuningHandle, out NativeArray<SignalThreadContentionTuning64>.ReadOnly tuningArray) ||
                tuningArray.Length <= 0)
            {
                return false;
            }

            tuning = tuningArray[0];
            return tuning.Magic == TuningMagic;
        }

#if UNITY_EDITOR
        public static unsafe bool TryReadCsvBytesForLoad(string path, out ReadOnlySpan<byte> bytes)
        {
            bytes = default;
#if UNITY_EDITOR
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                return false;

            if (!TryOpenCsvScratchForLoad(out NativeArray<byte> scratch) || !scratch.IsCreated)
                return false;

            int bytesRead;
            try
            {
                using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    long streamLength = stream.Length;
                    if (streamLength <= 0L || streamLength > scratch.Length)
                        return false;

                    int expectedBytes = (int)streamLength;
                    Span<byte> scratchBytes = new Span<byte>(scratch.GetUnsafePtr(), expectedBytes);
                    bytesRead = 0;
                    while (bytesRead < expectedBytes)
                    {
                        int read = stream.Read(scratchBytes.Slice(bytesRead));
                        if (read <= 0)
                            return false;

                        bytesRead += read;
                    }
                }
            }
            catch (IOException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }

            bytes = new ReadOnlySpan<byte>(scratch.GetUnsafeReadOnlyPtr(), bytesRead);
            return true;
#else
            _ = path;
            return false;
#endif
        }

        private static bool TryOpenCsvScratchForLoad(out NativeArray<byte> scratch)
        {
            scratch = default;
            if (!IsInitializedForRead() ||
                !TryResolve(_vault, in _csvScratchHandle, out NativeArray<byte> csvScratch) ||
                csvScratch.Length < CsvScratchBytes)
            {
                return false;
            }

            scratch = csvScratch;
            return true;
        }
#endif

        public static bool DumpToDisk()
        {
            if (!EnsureInitializedForCrashDumpRoute() ||
                !TryResolve(_vault, in _telemetryHandle, out NativeArray<SignalThreadContentionTelemetryEntry> telemetry))
            {
                return false;
            }

            try
            {
                string root = Directory.GetParent(Application.dataPath)?.FullName;
                if (string.IsNullOrEmpty(root))
                    return false;

                string path = Path.Combine(root, DumpPath);
                string directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);

                using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))
                {
                    Span<byte> header = stackalloc byte[HeaderSizeBytes];
                    WriteUInt32LittleEndian(header, 0, DumpMagic0);
                    WriteUInt32LittleEndian(header, 4, DumpMagic1);
                    WriteUInt32LittleEndian(header, 8, TelemetryCapacity);
                    WriteUInt32LittleEndian(header, 12, unchecked((uint)UnsafeUtility.SizeOf<SignalThreadContentionTelemetryEntry>()));
                    stream.Write(header);
                    unsafe
                    {
                        byte* ptr = (byte*)telemetry.GetUnsafeReadOnlyPtr();
                        int byteCount = TelemetryCapacity * UnsafeUtility.SizeOf<SignalThreadContentionTelemetryEntry>();
                        stream.Write(new ReadOnlySpan<byte>(ptr, byteCount));
                    }
                }

                return true;
            }
            catch (IOException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
        }

        public static bool TryDumpOnFault()
        {
            if (!TryGetLatestTelemetry(out SignalThreadContentionTelemetryEntry entry))
                return false;

            return ((entry.Flags & (SignalThreadContentionFlags.NonFinite | SignalThreadContentionFlags.OrphanedProducer)) != 0u ||
                    entry.OverflowSignals > 5u) &&
                   DumpToDisk();
        }

        private static bool IsFinite(in SignalWardenMockDamageSignal signal)
        {
            return math.all(math.isfinite(signal.Aup)) &&
                   math.all(math.isfinite(signal.Normal)) &&
                   math.isfinite(signal.Damage);
        }

        private static bool IsInitializedForRead()
        {
            return _initialized != 0 && _vault != null;
        }

        private static bool TryOpenCommittedSignalsBufferForOwner(out NativeArray<SignalWardenMockDamageSignal> signals, out int count)
        {
            signals = default;
            count = 0;
            if (!IsInitializedForRead() ||
                !TryResolve(_vault, in _committedSignalsHandle, out NativeArray<SignalWardenMockDamageSignal> committedSignals) ||
                !TryResolve(_vault, in _committedCountHandle, out NativeArray<int> committedCount) ||
                committedCount.Length <= 0)
            {
                return false;
            }

            signals = committedSignals;
            count = math.clamp(committedCount[0], 0, committedSignals.Length);
            return true;
        }

        private static bool TryReadCommittedSignalsBuffer(out NativeArray<SignalWardenMockDamageSignal>.ReadOnly signals, out int count)
        {
            signals = default;
            count = 0;
            if (!IsInitializedForRead() ||
                !TryRead(_vault, in _committedSignalsHandle, out NativeArray<SignalWardenMockDamageSignal>.ReadOnly committedSignals) ||
                !TryRead(_vault, in _committedCountHandle, out NativeArray<int>.ReadOnly committedCount) ||
                committedCount.Length <= 0)
            {
                return false;
            }

            signals = committedSignals;
            count = math.clamp(committedCount[0], 0, committedSignals.Length);
            return true;
        }

        private static bool EnsureInitializedForOwnerRoute()
        {
            IDataVault vault = _vault;
            if (vault == null)
                return false;

            return Initialize(vault, SignalBusRegistry.GlobalQualityWeight01, vault.CapacityPressure01);
        }

        private static bool EnsureInitializedForCrashDumpRoute()
        {
            IDataVault vault = _vault;
            if (vault == null && GlobalDataVault.TryGetLatestCreated(out GlobalDataVault latest))
                vault = latest;
            if (vault == null)
                return false;

            return Initialize(vault, SignalBusRegistry.GlobalQualityWeight01, vault.CapacityPressure01);
        }

        private static bool AreVaultBuffersReady(IDataVault vault)
        {
            int byteCapacity = (MaxThreadCount * MaxThreadStrideBytes) + 64;
            if (!TryResolve(vault, in _frontBytesHandle, out NativeArray<byte> frontBytes) || frontBytes.Length < byteCapacity)
                return false;
            if (!TryResolve(vault, in _backBytesHandle, out NativeArray<byte> backBytes) || backBytes.Length < byteCapacity)
                return false;
            if (!TryResolve(vault, in _frontHeadersHandle, out NativeArray<SignalThreadLocalHeader64> frontHeaders) || frontHeaders.Length < MaxThreadCount)
                return false;
            if (!TryResolve(vault, in _backHeadersHandle, out NativeArray<SignalThreadLocalHeader64> backHeaders) || backHeaders.Length < MaxThreadCount)
                return false;
            if (!TryResolve(vault, in _committedSignalsHandle, out NativeArray<SignalWardenMockDamageSignal> committedSignals) || committedSignals.Length < MaxCommittedSignals)
                return false;
            if (!TryResolve(vault, in _committedCountHandle, out NativeArray<int> committedCount) || committedCount.Length < 1)
                return false;
            if (!TryResolve(vault, in _telemetryHandle, out NativeArray<SignalThreadContentionTelemetryEntry> telemetry) || telemetry.Length < TelemetryCapacity)
                return false;
            if (!TryResolve(vault, in _telemetryCursorHandle, out NativeArray<int> telemetryCursor) || telemetryCursor.Length < 1)
                return false;
            if (!TryResolve(vault, in _tuningHandle, out NativeArray<SignalThreadContentionTuning64> tuning) || tuning.Length < 1)
                return false;
            if (!TryResolve(vault, in _coalescenceBucketsHandle, out NativeArray<int> coalescenceBuckets) || coalescenceBuckets.Length < MaxCommittedSignals)
                return false;
            if (!TryResolve(vault, in _overflowSignalsHandle, out NativeArray<SignalWardenMockDamageSignal> overflowSignals) || overflowSignals.Length < MaxOverflowSignals)
                return false;
            if (!TryResolve(vault, in _overflowHeaderHandle, out NativeArray<SignalThreadOverflowHeader64> overflowHeader) || overflowHeader.Length < 1)
                return false;
            if (!TryResolve(vault, in _csvScratchHandle, out NativeArray<byte> csvScratch) || csvScratch.Length < CsvScratchBytes)
                return false;

            return true;
        }

        private static bool TryResolve<T>(IDataVault vault, in VaultGenerationHandle<T> handle, out NativeArray<T> buffer)
            where T : struct
        {
            buffer = default;
            return vault != null && vault.TryResolveHandle(in handle, out buffer) && buffer.IsCreated;
        }

        private static bool TryRead<T>(IDataVault vault, in VaultGenerationHandle<T> handle, out NativeArray<T>.ReadOnly buffer)
            where T : struct
        {
            buffer = default;
            return vault != null && vault.TryReadOnlyHandle(in handle, out buffer) && buffer.Length > 0;
        }

        private static void EnsureDefaultTuning(NativeArray<SignalThreadContentionTuning64> tuningArray)
        {
            if (!tuningArray.IsCreated || tuningArray.Length <= 0)
                return;

            SignalThreadContentionTuning64 tuning = tuningArray[0];
            if (tuning.Magic == TuningMagic)
                return;

            tuning.Magic = TuningMagic;
            tuning.ScratchpadCapacityMultiplier = 1f;
            tuning.CoalescenceGridSizeMeters = 1f;
            tuning.GlobalQualityOverride01 = SignalBusRegistry.GlobalQualityWeight01;
            tuning.VramPressureOverride01 = _vault != null ? _vault.CapacityPressure01 : 0f;
            tuning.MinStrideBytes = MinThreadStrideBytes;
            tuning.MaxStrideBytes = MaxThreadStrideBytes;
            tuning.MaxOutputCount = MaxCommittedSignals;
            tuningArray[0] = tuning;
        }

        private static void ClearHeaders(NativeArray<SignalThreadLocalHeader64> headers, uint frame, int activeStrideBytes)
        {
            if (!headers.IsCreated)
                return;

            for (int i = 0; i < headers.Length; i++)
            {
                SignalThreadLocalHeader64 header = default;
                header.Frame = frame;
                header.BatchId = _batchId;
                header.ThreadIndex = i;
                header.ActiveStrideBytes = activeStrideBytes;
                headers[i] = header;
            }
        }

        private static void ClearOverflowHeader(
            NativeArray<SignalThreadOverflowHeader64> overflowHeader,
            NativeArray<SignalWardenMockDamageSignal> overflowSignals,
            uint frame)
        {
            if (!overflowHeader.IsCreated || overflowHeader.Length <= 0)
                return;

            SignalThreadOverflowHeader64 header = default;
            header.Frame = frame;
            header.Capacity = overflowSignals.IsCreated ? overflowSignals.Length : 0;
            overflowHeader[0] = header;
        }

        private static int ResolveActiveStrideBytes(float globalQualityWeight01, float vramPressure01)
        {
            float quality = math.saturate(math.isfinite(globalQualityWeight01) ? globalQualityWeight01 : 0f);
            float vramPressure = math.saturate(math.isfinite(vramPressure01) ? vramPressure01 : 1f);
            float budget = math.saturate(quality * math.lerp(1f, 0.25f, vramPressure));
            float curved = budget * budget * (3f - (2f * budget));
            float multiplier = ResolveCapacityMultiplier();
            int stride = (int)math.round(math.lerp(_csvMinStrideBytes, _csvMaxStrideBytes, curved) * multiplier);
            return Align64(math.clamp(stride, _csvMinStrideBytes, _csvMaxStrideBytes));
        }

        private static float ResolveEffectiveQuality01()
        {
            if (TryResolve(_vault, in _tuningHandle, out NativeArray<SignalThreadContentionTuning64> tuning) &&
                tuning.Length > 0 &&
                tuning[0].Magic == TuningMagic)
            {
                return math.saturate(tuning[0].GlobalQualityOverride01);
            }

            return SignalBusRegistry.GlobalQualityWeight01;
        }

        private static float ResolveEffectiveVramPressure01()
        {
            if (TryResolve(_vault, in _tuningHandle, out NativeArray<SignalThreadContentionTuning64> tuning) &&
                tuning.Length > 0 &&
                tuning[0].Magic == TuningMagic)
            {
                return math.saturate(tuning[0].VramPressureOverride01);
            }

            return _vault != null ? _vault.CapacityPressure01 : 0f;
        }

        private static float ResolveCapacityMultiplier()
        {
            if (TryResolve(_vault, in _tuningHandle, out NativeArray<SignalThreadContentionTuning64> tuning) &&
                tuning.Length > 0 &&
                tuning[0].Magic == TuningMagic)
            {
                return math.max(0.125f, tuning[0].ScratchpadCapacityMultiplier);
            }

            return 1f;
        }

        private static float ResolveCoalescenceGridMeters()
        {
            if (TryResolve(_vault, in _tuningHandle, out NativeArray<SignalThreadContentionTuning64> tuning) &&
                tuning.Length > 0 &&
                tuning[0].Magic == TuningMagic)
            {
                return math.max(0.125f, tuning[0].CoalescenceGridSizeMeters);
            }

            return 1f;
        }

        private static int Align64(int value)
        {
            return (value + 63) & ~63;
        }

        private static void WriteUInt32LittleEndian(Span<byte> bytes, int offset, uint value)
        {
            bytes[offset] = (byte)value;
            bytes[offset + 1] = (byte)(value >> 8);
            bytes[offset + 2] = (byte)(value >> 16);
            bytes[offset + 3] = (byte)(value >> 24);
        }
    }

#if UNITY_EDITOR
    /// <summary>Editor/source-data parser for signal_corridor_capacities.csv.</summary>
    [Preserve]
    public static class SignalThreadContentionCsvHotSwap
    {
        private const string SourceDataRelativePath = "_SourceData/Signals/signal_corridor_capacities.csv";
        private const byte Comma = (byte)',';
        private const byte LineFeed = (byte)'\n';
        private const byte CarriageReturn = (byte)'\r';

        private struct ParsedCapacityRow
        {
            public int MinStrideBytes;
            public int MaxStrideBytes;
            public int MaxOutputCount;
            public uint PlatformHash;
            public byte IsValid;
        }

        public static bool TryLoadDefault()
        {
#if UNITY_EDITOR
            string dataPath = Application.dataPath;
            if (string.IsNullOrEmpty(dataPath))
                return false;

            return TryLoad(Path.Combine(dataPath, SourceDataRelativePath));
#else
            return false;
#endif
        }

        public static unsafe bool TryLoad(string path)
        {
#if UNITY_EDITOR
            if (!SignalThreadLocalScratchpad.TryReadCsvBytesForLoad(path, out ReadOnlySpan<byte> bytes))
                return false;

            return Parse(bytes);
#else
            _ = path;
            return false;
#endif
        }

        private static bool Parse(ReadOnlySpan<byte> bytes)
        {
            uint targetPlatformHash = ResolveTargetPlatformHash();
            uint fallbackPlatformHash = ComputeHash("pc");
            ParsedCapacityRow selected = default;
            ParsedCapacityRow fallback = default;
            int rowStart = 0;
            while (rowStart < bytes.Length)
            {
                int rowEnd = rowStart;
                while (rowEnd < bytes.Length && bytes[rowEnd] != LineFeed && bytes[rowEnd] != CarriageReturn)
                    rowEnd++;

                if (TryParseRow(bytes.Slice(rowStart, rowEnd - rowStart), out ParsedCapacityRow row))
                {
                    if (row.PlatformHash == targetPlatformHash)
                        selected = row;
                    else if (row.PlatformHash == fallbackPlatformHash)
                        fallback = row;
                }

                rowStart = rowEnd + 1;
                while (rowStart < bytes.Length && (bytes[rowStart] == LineFeed || bytes[rowStart] == CarriageReturn))
                    rowStart++;
            }

            if (selected.IsValid != 0)
                return ApplyRow(in selected);

            return fallback.IsValid != 0 && ApplyRow(in fallback);
        }

        private static bool TryParseRow(ReadOnlySpan<byte> row, out ParsedCapacityRow parsed)
        {
            parsed = default;
            if (row.Length <= 0 || row[0] == (byte)'#')
                return false;

            int first = IndexOf(row, Comma, 0);
            int second = IndexOf(row, Comma, first + 1);
            int third = IndexOf(row, Comma, second + 1);
            if (first <= 0 || second <= first || third <= second)
                return false;

            ReadOnlySpan<byte> platform = Trim(row.Slice(0, first));
            ReadOnlySpan<byte> minSlice = Trim(row.Slice(first + 1, second - first - 1));
            ReadOnlySpan<byte> maxSlice = Trim(row.Slice(second + 1, third - second - 1));
            ReadOnlySpan<byte> outputSlice = Trim(row.Slice(third + 1));

            if (!TryParseInt(minSlice, out int minStride) ||
                !TryParseInt(maxSlice, out int maxStride) ||
                !TryParseInt(outputSlice, out int maxOutput))
            {
                return false;
            }

            parsed.MinStrideBytes = minStride;
            parsed.MaxStrideBytes = maxStride;
            parsed.MaxOutputCount = maxOutput;
            parsed.PlatformHash = ComputeHash(platform);
            parsed.IsValid = 1;
            return parsed.PlatformHash != 0u;
        }

        private static bool ApplyRow(in ParsedCapacityRow row)
        {
            return row.IsValid != 0 &&
                   SignalThreadLocalScratchpad.ApplyCsvTuning(
                       row.MinStrideBytes,
                       row.MaxStrideBytes,
                       row.MaxOutputCount,
                       row.PlatformHash);
        }

        private static int IndexOf(ReadOnlySpan<byte> row, byte target, int start)
        {
            for (int i = math.max(0, start); i < row.Length; i++)
            {
                if (row[i] == target)
                    return i;
            }

            return -1;
        }

        private static ReadOnlySpan<byte> Trim(ReadOnlySpan<byte> value)
        {
            int start = 0;
            int end = value.Length - 1;
            while (start < value.Length && (value[start] == (byte)' ' || value[start] == (byte)'\t'))
                start++;
            while (end >= start && (value[end] == (byte)' ' || value[end] == (byte)'\t'))
                end--;
            return end < start ? ReadOnlySpan<byte>.Empty : value.Slice(start, end - start + 1);
        }

        private static bool TryParseInt(ReadOnlySpan<byte> bytes, out int value)
        {
            value = 0;
            if (bytes.Length <= 0)
                return false;

            for (int i = 0; i < bytes.Length; i++)
            {
                byte c = bytes[i];
                if (c < (byte)'0' || c > (byte)'9')
                    return false;

                value = (value * 10) + (c - (byte)'0');
            }

            return true;
        }

        private static uint ComputeHash(ReadOnlySpan<byte> bytes)
        {
            uint hash = 2166136261u;
            for (int i = 0; i < bytes.Length; i++)
            {
                byte c = bytes[i];
                if (c == (byte)' ' || c == (byte)'\t')
                    continue;

                if (c >= (byte)'A' && c <= (byte)'Z')
                    c = (byte)(c + 32);

                hash ^= c;
                hash *= 16777619u;
            }

            return hash == 0u ? 1u : hash;
        }

        private static uint ComputeHash(string text)
        {
            uint hash = 2166136261u;
            if (!string.IsNullOrEmpty(text))
            {
                for (int i = 0; i < text.Length; i++)
                {
                    char c = text[i];
                    if (c == ' ' || c == '\t')
                        continue;

                    if (c >= 'A' && c <= 'Z')
                        c = (char)(c + 32);

                    hash ^= c;
                    hash *= 16777619u;
                }
            }

            return hash == 0u ? 1u : hash;
        }

        private static uint ResolveTargetPlatformHash()
        {
            string gpuName = SystemInfo.graphicsDeviceName;
            string deviceModel = SystemInfo.deviceModel;
            string deviceName = SystemInfo.deviceName;
            if (ContainsOrdinalIgnoreCase(gpuName, "rtx 4090") ||
                ContainsOrdinalIgnoreCase(gpuName, "rtx4090"))
            {
                return ComputeHash("rtx4090");
            }

            if (ContainsOrdinalIgnoreCase(deviceModel, "steam deck") ||
                ContainsOrdinalIgnoreCase(deviceName, "steam deck") ||
                ContainsOrdinalIgnoreCase(gpuName, "vangogh"))
            {
                return ComputeHash("steamdeck");
            }

            if (ContainsOrdinalIgnoreCase(gpuName, "mx350"))
                return ComputeHash("mx350");

            if (Application.platform == RuntimePlatform.Android ||
                ContainsOrdinalIgnoreCase(deviceModel, "quest 3") ||
                ContainsOrdinalIgnoreCase(deviceModel, "quest3") ||
                ContainsOrdinalIgnoreCase(deviceName, "quest 3") ||
                ContainsOrdinalIgnoreCase(deviceName, "quest3"))
            {
                return ComputeHash("quest3");
            }

            return ComputeHash("pc");
        }

        private static bool ContainsOrdinalIgnoreCase(string text, string needle)
        {
            if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(needle) || needle.Length > text.Length)
                return false;

            int limit = text.Length - needle.Length;
            for (int i = 0; i <= limit; i++)
            {
                int n = 0;
                while (n < needle.Length && ToLowerAscii(text[i + n]) == ToLowerAscii(needle[n]))
                    n++;

                if (n == needle.Length)
                    return true;
            }

            return false;
        }

        private static char ToLowerAscii(char c)
        {
            return c >= 'A' && c <= 'Z' ? (char)(c + 32) : c;
        }
    }
#endif

    [ExecuteAlways]
    [AddComponentMenu("Hecton8/Core/Signal Thread Contention Heatmap")]
    public sealed class SignalThreadContentionHeatmapGizmo : MonoBehaviour
    {
        [SerializeField] private bool drawHeatmap = true;
        [SerializeField] private float cellSizeMeters = 1f;
        [SerializeField] private int maxDrawnCells = 256;

        private void OnDrawGizmos()
        {
            if (!drawHeatmap ||
                !SignalThreadLocalScratchpad.TryGetCommittedSignalsReadOnly(out NativeArray<SignalWardenMockDamageSignal>.ReadOnly signals, out int signalCount) ||
                signalCount <= 0 ||
                signals.Length <= 0)
            {
                return;
            }

            Vector3 origin = transform.position;
            int drawLimit = math.min(math.max(0, maxDrawnCells), math.min(signalCount, signals.Length));
            double3 anchorAup = signals[0].Aup;
            float safeCell = math.max(0.125f, cellSizeMeters);
            for (int i = 0; i < drawLimit; i++)
            {
                SignalWardenMockDamageSignal signal = signals[i];
                if (!math.all(math.isfinite(signal.Aup)))
                    continue;

                uint hash = signal.AupCellHash;
                int density = 0;
                for (int j = 0; j < drawLimit; j++)
                {
                    if (signals[j].AupCellHash == hash)
                        density++;
                }

                double3 deltaAup = AupPrecisionMath.LocalDeltaDouble(signal.Aup, anchorAup);
                float3 local = AupPrecisionMath.DowncastLocalDelta(deltaAup, float3.zero);
                local = math.clamp(local, new float3(-64f, -64f, -64f), new float3(64f, 64f, 64f));
                float pressure = math.saturate(density / 16f);
                Gizmos.color = Color.Lerp(new Color(0.1f, 0.8f, 1f, 0.35f), new Color(1f, 0.2f, 0.05f, 0.75f), pressure);
                Vector3 center = origin + new Vector3(local.x, local.y, local.z);
                Gizmos.DrawWireCube(center, new Vector3(safeCell, safeCell, safeCell));
            }
        }
    }

#if UNITY_EDITOR
    /// <summary>Editor-only tuner for thread-local SignalBus contention diagnostics.</summary>
    public sealed class SignalThreadContentionTunerWindow : UnityEditor.EditorWindow
    {
        private int _signalCount = 4096;
        private float _radiusMeters = 8f;
        private float _damage = 0.1f;
        private float _capacityMultiplier = 1f;
        private float _coalescenceGridMeters = 1f;
        private float _qualityOverride01 = 1f;
        private SignalThreadContentionWaterfallGraph _waterfallGraph;

        [UnityEditor.MenuItem("Hecton8/Diagnostics/Signal Thread Contention")]
        private static void Open()
        {
            GetWindow<SignalThreadContentionTunerWindow>("Signal Contention");
        }

        public void CreateGUI()
        {
            UnityEngine.UIElements.VisualElement root = rootVisualElement;
            root.Clear();

            UnityEngine.UIElements.SliderInt signalSlider = new UnityEngine.UIElements.SliderInt("Signals", 0, 65536) { value = _signalCount };
            signalSlider.RegisterValueChangedCallback(evt => _signalCount = evt.newValue);
            root.Add(signalSlider);

            UnityEngine.UIElements.Slider radiusSlider = new UnityEngine.UIElements.Slider("Radius", 0f, 64f) { value = _radiusMeters };
            radiusSlider.RegisterValueChangedCallback(evt => _radiusMeters = evt.newValue);
            root.Add(radiusSlider);

            UnityEngine.UIElements.Slider damageSlider = new UnityEngine.UIElements.Slider("Damage", 0f, 10f) { value = _damage };
            damageSlider.RegisterValueChangedCallback(evt => _damage = evt.newValue);
            root.Add(damageSlider);

            UnityEngine.UIElements.Slider capacitySlider = new UnityEngine.UIElements.Slider("ScratchpadCapacityMultiplier", 0.125f, 1f) { value = _capacityMultiplier };
            capacitySlider.RegisterValueChangedCallback(evt =>
            {
                _capacityMultiplier = evt.newValue;
                ApplyEditorTuning();
            });
            root.Add(capacitySlider);

            UnityEngine.UIElements.Slider gridSlider = new UnityEngine.UIElements.Slider("CoalescenceGridSize", 0.125f, 8f) { value = _coalescenceGridMeters };
            gridSlider.RegisterValueChangedCallback(evt =>
            {
                _coalescenceGridMeters = evt.newValue;
                ApplyEditorTuning();
            });
            root.Add(gridSlider);

            UnityEngine.UIElements.Slider qualitySlider = new UnityEngine.UIElements.Slider("GlobalQualityWeight", 0f, 1f) { value = _qualityOverride01 };
            qualitySlider.RegisterValueChangedCallback(evt =>
            {
                _qualityOverride01 = evt.newValue;
                ApplyEditorTuning();
            });
            root.Add(qualitySlider);

            root.Add(new UnityEngine.UIElements.Button(() => { SignalThreadContentionCsvHotSwap.TryLoadDefault(); }) { text = "Load CSV" });
            root.Add(new UnityEngine.UIElements.Button(RunMockContentionEditorBlocking) { text = "Run Mock Contention" });
            _waterfallGraph = new SignalThreadContentionWaterfallGraph();
            root.Add(_waterfallGraph);
            RefreshMetrics();
        }

        private void OnInspectorUpdate()
        {
            RefreshMetrics();
        }

        private void ApplyEditorTuning()
        {
            SignalThreadContentionTuning64 tuning;
            if (!SignalThreadLocalScratchpad.TryGetTuning(out tuning))
                tuning = default;

            SignalThreadLocalScratchpad.MutateTuning(
                _capacityMultiplier,
                _coalescenceGridMeters,
                _qualityOverride01,
                tuning.VramPressureOverride01,
                tuning.MinStrideBytes <= 0 ? 2048 : tuning.MinStrideBytes,
                tuning.MaxStrideBytes <= 0 ? 16384 : tuning.MaxStrideBytes,
                tuning.MaxOutputCount <= 0 ? 4096 : tuning.MaxOutputCount,
                tuning.TargetPlatformHash);
        }

        private void RunMockContentionEditorBlocking()
        {
            JobHandle handle;
            uint frame = Hecton8.Core.SystemDispatcher.CurrentFrameId;
            if (!SignalThreadLocalScratchpad.ScheduleMockContention(
                    _signalCount,
                    double3.zero,
                    _radiusMeters,
                    _damage,
                    frame,
                    default,
                    out handle))
            {
                return;
            }

            DispatcherJobFence.TryComplete(ref handle, forceComplete: true);
            if (SignalThreadLocalScratchpad.ScheduleCommit(frame, default, out JobHandle commitHandle))
            {
                long startTicks = System.Diagnostics.Stopwatch.GetTimestamp();
                DispatcherJobFence.TryComplete(ref commitHandle, forceComplete: true);
                long elapsedTicks = System.Diagnostics.Stopwatch.GetTimestamp() - startTicks;
                double elapsedMicroseconds = elapsedTicks * 1000000.0d / System.Diagnostics.Stopwatch.Frequency;
                SignalThreadLocalScratchpad.RecordLastCommitMicroseconds(unchecked((uint)math.max(0, (int)math.round(elapsedMicroseconds))));
                SignalThreadLocalScratchpad.TryDumpOnFault();
            }

            RefreshMetrics();
        }

        private void RefreshMetrics()
        {
            _waterfallGraph?.MarkDirtyRepaint();
        }

        private sealed class SignalThreadContentionWaterfallGraph : UnityEngine.UIElements.VisualElement
        {
            private const float GraphHeightPixels = 112f;
            private const float ColumnPixels = 4f;
            private const float MaxCommitMicroseconds = 2000f;
            private const float MaxOverflowSignals = 64f;
            private const float MaxDroppedSignals = 64f;

            public SignalThreadContentionWaterfallGraph()
            {
                style.height = GraphHeightPixels;
                style.marginTop = 6f;
                generateVisualContent += Draw;
            }

            private void Draw(UnityEngine.UIElements.MeshGenerationContext context)
            {
                Rect rect = contentRect;
                if (rect.width <= 1f || rect.height <= 1f)
                    return;

                UnityEngine.UIElements.Painter2D painter = context.painter2D;
                DrawRect(painter, rect, new Color(0.015f, 0.025f, 0.035f, 0.96f));
                if (!SignalThreadLocalScratchpad.TryGetTelemetryReadOnly(out NativeArray<SignalThreadContentionTelemetryEntry>.ReadOnly telemetry, out int cursor) ||
                    telemetry.Length <= 0)
                {
                    return;
                }

                int columns = math.min(telemetry.Length, math.max(1, (int)math.floor(rect.width / ColumnPixels)));
                float columnWidth = math.max(1f, rect.width / columns);
                int start = cursor - columns;
                for (int i = 0; i < columns; i++)
                {
                    int index = start + i;
                    while (index < 0)
                        index += telemetry.Length;
                    index %= telemetry.Length;

                    SignalThreadContentionTelemetryEntry entry = telemetry[index];
                    if (entry.Frame == 0u && entry.WrittenSignals == 0u && entry.Flags == 0u)
                        continue;

                    float written01 = math.saturate(entry.WrittenSignals / (float)SignalThreadLocalScratchpad.MaxCommittedSignalsForEditor);
                    float overflow01 = math.saturate(entry.OverflowSignals / MaxOverflowSignals);
                    float dropped01 = math.saturate(entry.DroppedSignals / MaxDroppedSignals);
                    float commit01 = math.saturate(entry.CommitMicroseconds / MaxCommitMicroseconds);
                    float pressure01 = math.saturate(math.max(written01, math.max(overflow01, dropped01)) + commit01 * 0.25f);
                    float height = math.max(1f, rect.height * pressure01);
                    float x = rect.xMin + i * columnWidth;
                    float y = rect.yMax - height;
                    Color color = ResolvePressureColor(written01, overflow01, dropped01, commit01);
                    DrawRect(painter, new Rect(x, y, math.max(1f, columnWidth - 1f), height), color);
                }
            }

            private static Color ResolvePressureColor(float written01, float overflow01, float dropped01, float commit01)
            {
                Color calm = new Color(0.08f, 0.55f, 0.82f, 0.82f);
                Color hot = new Color(1f, 0.25f, 0.05f, 0.95f);
                Color fault = new Color(1f, 0.02f, 0.02f, 0.98f);
                float load01 = math.saturate(math.max(written01, commit01));
                Color load = Color.Lerp(calm, hot, load01);
                return Color.Lerp(load, fault, math.saturate(math.max(overflow01, dropped01)));
            }

            private static void DrawRect(UnityEngine.UIElements.Painter2D painter, Rect rect, Color color)
            {
                painter.fillColor = color;
                painter.BeginPath();
                painter.MoveTo(new Vector2(rect.xMin, rect.yMin));
                painter.LineTo(new Vector2(rect.xMax, rect.yMin));
                painter.LineTo(new Vector2(rect.xMax, rect.yMax));
                painter.LineTo(new Vector2(rect.xMin, rect.yMax));
                painter.ClosePath();
                painter.Fill();
            }
        }
    }
#endif

    /// <summary>
    /// IL2CPP AOT preservation anchor for closed generic signal lanes.
    /// </summary>
    [Preserve]
    public static class SignalBusAotPreserve
    {
        /// <summary>Cold method referenced by build/link preservation scans.</summary>
        [Preserve]
        public static void PreserveGenerics()
        {
            PreserveLane<CombatDamageSignal>();
            PreserveLane<AcousticPingSignal>();
            PreserveLane<SignalWardenMockDamageSignal>();
            PreserveLane<MockPlayerFootstepSignal>();
            PreserveLane<MockRockCollisionSignal>();
            PreserveLane<MacroCollisionSignal>();
            PreserveLane<WakeRequestSignal>();
            PreserveLane<WaterlineBreachSignal>();
            PreserveLane<PlayerRespawnSignal>();
            PreserveLane<InventoryRespawnDeathAupSignal>();
            PreserveLane<InventoryDeathLootCacheSignal>();
            PreserveLane<InventoryRespawnPenaltyResultSignal>();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void PreserveLane<T>()
            where T : unmanaged, ISignal
        {
            GC.KeepAlive(typeof(SignalBus<T>));
        }
    }
}
