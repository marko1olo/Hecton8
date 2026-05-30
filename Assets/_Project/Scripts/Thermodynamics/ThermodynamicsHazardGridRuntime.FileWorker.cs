using System;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Threading;
using Unity.Mathematics;

namespace Hecton8.Thermodynamics
{
    public sealed unsafe partial class ThermodynamicsHazardGridRuntime
    {
        private const int ConfigWorkerSleepMs = 64;
        private const int ConfigWorkerIdle = 0;
        private const int ConfigWorkerRequested = 1;
        private const int ConfigWorkerBusy = 2;
        private const int ConfigWorkerReady = 3;
        private const int ConfigWorkerFault = -1;

        // SPSC handoff: ConfigWorkerLoop is the only producer; Tick/SlowTick on the main thread is the only consumer/requester.
        // Volatile/Interlocked barriers publish staged config bytes before state flips to ConfigWorkerReady on ARM64.
        private Thread _configWorkerThread;
        private string _binaryConstantsPath;
        private string _csvOverridePath;
        private byte[] _binaryConstantsWorkerBytes;
        private byte[] _csvWorkerBytes;
        private int _configWorkerRun;
        private int _binaryRequestState;
        private int _csvRequestState;
        private int _binaryWorkerReadCount;
        private int _csvWorkerReadCount;
        private long _binaryWorkerWriteTicks;
        private long _csvWorkerWriteTicks;
        private long _binaryAppliedWriteTicks;
        private long _csvAppliedWriteTicks;

        private void StartConfigWorkerIfNeeded()
        {
            if (_configWorkerThread != null ||
                !HasHandle(in _constants) ||
                !HasHandle(in _binaryConstantBytes))
                return;

            _binaryConstantsPath = Path.Combine(UnityEngine.Application.streamingAssetsPath, "thermodynamic_constants.h8bin");
#if UNITY_EDITOR
            _csvOverridePath = Path.Combine(UnityEngine.Application.dataPath, "_SourceData", "Thermodynamics", "hazard_profiles.csv");
#else
            _csvOverridePath = null;
#endif
            _binaryConstantsWorkerBytes ??= new byte[BinaryConstantsBytes]; // COLD ALLOC: byte[16] - config worker staging, parsed before constants commit - owner: ThermodynamicsHazardGridRuntime
#if UNITY_EDITOR
            _csvWorkerBytes ??= new byte[CsvBufferBytes]; // COLD ALLOC: byte[4096] - CSV worker staging, parsed before constants commit - owner: ThermodynamicsHazardGridRuntime
#endif
            Volatile.Write(ref _configWorkerRun, 1);

            _configWorkerThread = new Thread(ConfigWorkerLoop)
            {
                IsBackground = true,
                Name = "H8 Thermodynamics Config IO"
            }; // COLD ALLOC: Thread[1] - persistent MMF config reader - owner: ThermodynamicsHazardGridRuntime
            _configWorkerThread.Start();
        }

        private void StopConfigWorker()
        {
            Thread worker = _configWorkerThread;
            if (worker == null)
                return;

            Volatile.Write(ref _configWorkerRun, 0);
            if (worker.IsAlive)
                worker.Join(250);

            _configWorkerThread = null;
        }

        private void RequestBinaryConstantsLoad()
        {
            RequestConfigLoad(ref _binaryRequestState);
        }

        private void RequestCsvOverrideLoad()
        {
#if UNITY_EDITOR
            RequestConfigLoad(ref _csvRequestState);
#endif
        }

        private static void RequestConfigLoad(ref int state)
        {
            int observed = Volatile.Read(ref state);
            if (observed == ConfigWorkerIdle || observed == ConfigWorkerFault)
                Interlocked.CompareExchange(ref state, ConfigWorkerRequested, observed);
        }

        private void ApplyPendingConfigLoads()
        {
            if (HasHandle(in _constants) && Volatile.Read(ref _binaryRequestState) == ConfigWorkerReady)
            {
                int read = Volatile.Read(ref _binaryWorkerReadCount);
                if (read >= BinaryConstantsBytes &&
                    _binaryConstantsWorkerBytes != null &&
                    TryBuildBinaryConstants(_binaryConstantsWorkerBytes, read, out ThermodynamicsHazardConstants parsed))
                {
                    if (TryWriteConstantsWithOwner(in parsed, MemoryOwner))
                        Volatile.Write(ref _binaryAppliedWriteTicks, Volatile.Read(ref _binaryWorkerWriteTicks));
                }

                Volatile.Write(ref _binaryRequestState, ConfigWorkerIdle);
            }

#if UNITY_EDITOR
            if (HasHandle(in _constants) && Volatile.Read(ref _csvRequestState) == ConfigWorkerReady)
            {
                int read = Volatile.Read(ref _csvWorkerReadCount);
                long ticks = Volatile.Read(ref _csvWorkerWriteTicks);
                if (read > 0 && ticks != Volatile.Read(ref _csvAppliedWriteTicks))
                {
                    int safeRead = _csvWorkerBytes == null ? 0 : Math.Min(read, _csvWorkerBytes.Length);
                    if (safeRead > 0)
                    {
                        ThermodynamicsHazardConstants parsed = TryReadConstants(out ThermodynamicsHazardConstants existing)
                            ? existing
                            : GenerateEmergencyMockConstants();
                        ParseCsvConstants(_csvWorkerBytes, safeRead, ref parsed);
                        parsed = SanitizeConstants(parsed);
                        if (TryWriteConstantsWithOwner(in parsed, MemoryOwner))
                        {
                            Volatile.Write(ref _csvAppliedWriteTicks, ticks);
                            _csvLastWriteUtc = new DateTime(ticks, DateTimeKind.Utc);
                        }
                    }
                }

                Volatile.Write(ref _csvRequestState, ConfigWorkerIdle);
            }
#endif
        }

        private void ConfigWorkerLoop()
        {
            while (Volatile.Read(ref _configWorkerRun) != 0)
            {
                bool worked = false;
                if (Interlocked.CompareExchange(ref _binaryRequestState, ConfigWorkerBusy, ConfigWorkerRequested) == ConfigWorkerRequested)
                {
                    worked = true;
                    RunBinaryConstantsLoad();
                }

#if UNITY_EDITOR
                if (Interlocked.CompareExchange(ref _csvRequestState, ConfigWorkerBusy, ConfigWorkerRequested) == ConfigWorkerRequested)
                {
                    worked = true;
                    RunCsvOverrideLoad();
                }
#endif

                if (!worked)
                    Thread.Sleep(ConfigWorkerSleepMs);
            }
        }

        private void RunBinaryConstantsLoad()
        {
            int nextState = ConfigWorkerIdle;
            try
            {
                long writeTicks;
                int read = ReadConfigFile(_binaryConstantsPath, _binaryConstantsWorkerBytes, BinaryConstantsBytes, Volatile.Read(ref _binaryAppliedWriteTicks), out writeTicks);
                if (read >= BinaryConstantsBytes)
                {
                    Volatile.Write(ref _binaryWorkerReadCount, read);
                    Volatile.Write(ref _binaryWorkerWriteTicks, writeTicks);
                    nextState = ConfigWorkerReady;
                }
            }
            catch (Exception)
            {
                Volatile.Write(ref _binaryWorkerReadCount, 0);
                nextState = ConfigWorkerFault;
            }

            Volatile.Write(ref _binaryRequestState, nextState);
        }

#if UNITY_EDITOR
        private void RunCsvOverrideLoad()
        {
            int nextState = ConfigWorkerIdle;
            try
            {
                long writeTicks;
                int read = ReadConfigFile(_csvOverridePath, _csvWorkerBytes, CsvBufferBytes, Volatile.Read(ref _csvAppliedWriteTicks), out writeTicks);
                if (read > 0)
                {
                    Volatile.Write(ref _csvWorkerReadCount, read);
                    Volatile.Write(ref _csvWorkerWriteTicks, writeTicks);
                    nextState = ConfigWorkerReady;
                }
            }
            catch (Exception)
            {
                Volatile.Write(ref _csvWorkerReadCount, 0);
                nextState = ConfigWorkerFault;
            }

            Volatile.Write(ref _csvRequestState, nextState);
        }
#endif

        private static bool TryBuildBinaryConstants(byte[] bytes, int length, out ThermodynamicsHazardConstants constants)
        {
            constants = default;
            if (bytes == null || length < BinaryConstantsBytes || bytes.Length < BinaryConstantsBytes)
            {
                return false;
            }

            constants.BaseWaterTempCelsius = ReadFloatLe(bytes, 0);
            constants.HeatDiffusionRate = ReadFloatLe(bytes, 4);
            constants.RadiationDiffusionRate = ReadFloatLe(bytes, 8);
            constants.RadiationDecayCoefficient = ReadFloatLe(bytes, 12);
            constants.RockShieldingFactor = 0.05f;
            constants.VerticalHeatBias = 1.25f;
            constants.HeatDamageThresholdCelsius = 100f;
            constants.RadiationDamageThreshold = 0.35f;
            constants = SanitizeConstants(constants);
            return true;
        }

#if UNITY_EDITOR
        private static void ParseCsvConstants(byte[] bytes, int length, ref ThermodynamicsHazardConstants constants)
        {
            if (bytes == null || length <= 0)
                return;

            int safeLength = Math.Min(length, bytes.Length);
            int cursor = 0;
            while (cursor < safeLength)
            {
                uint keyHash = 2166136261u;
                while (cursor < safeLength)
                {
                    byte c = bytes[cursor++];
                    if (c == (byte)',' || c == (byte)'=' || c == (byte)';')
                        break;
                    if (c == (byte)'\r' || c == (byte)'\n')
                        goto NextLine;
                    keyHash = (keyHash ^ ToLowerAscii(c)) * 16777619u;
                }

                float value = ParseFloat(bytes, ref cursor, safeLength);
                ApplyCsvValue(keyHash, value, ref constants);

            NextLine:
                while (cursor < safeLength && bytes[cursor] != (byte)'\n')
                    cursor++;
                if (cursor < safeLength)
                    cursor++;
            }
        }

        private static float ParseFloat(byte[] bytes, ref int cursor, int length)
        {
            while (cursor < length && (bytes[cursor] == (byte)' ' || bytes[cursor] == (byte)'\t'))
                cursor++;

            float sign = 1f;
            if (cursor < length && bytes[cursor] == (byte)'-')
            {
                sign = -1f;
                cursor++;
            }

            float value = 0f;
            while (cursor < length)
            {
                byte c = bytes[cursor];
                if (c < (byte)'0' || c > (byte)'9')
                    break;
                value = value * 10f + (c - (byte)'0');
                cursor++;
            }

            if (cursor < length && bytes[cursor] == (byte)'.')
            {
                cursor++;
                float scale = 0.1f;
                while (cursor < length)
                {
                    byte c = bytes[cursor];
                    if (c < (byte)'0' || c > (byte)'9')
                        break;
                    value += (c - (byte)'0') * scale;
                    scale *= 0.1f;
                    cursor++;
                }
            }

            return value * sign;
        }
#endif

        private static float ReadFloatLe(byte[] bytes, int offset)
        {
            int raw = bytes[offset] |
                      (bytes[offset + 1] << 8) |
                      (bytes[offset + 2] << 16) |
                      (bytes[offset + 3] << 24);
            return math.asfloat(raw);
        }

        private static int ReadConfigFile(string path, byte[] destination, int capacity, long skipWriteTicks, out long writeTicks)
        {
            writeTicks = 0L;
            if (destination == null || capacity <= 0 || destination.Length == 0 || string.IsNullOrEmpty(path) || !File.Exists(path))
                return 0;

            writeTicks = File.GetLastWriteTimeUtc(path).Ticks;
            if (writeTicks == skipWriteTicks)
                return 0;

            int safeCapacity = Math.Min(capacity, destination.Length);
            fixed (byte* destinationPtr = destination)
            {
                return ReadConfigFilePinned(path, destinationPtr, safeCapacity);
            }
        }

        private static int ReadConfigFilePinned(string path, byte* destination, int capacity)
        {
            try
            {
                return ReadConfigFileMapped(path, destination, capacity);
            }
            catch (PlatformNotSupportedException)
            {
                return ReadConfigFileStreamed(path, destination, capacity);
            }
            catch (NotSupportedException)
            {
                return ReadConfigFileStreamed(path, destination, capacity);
            }
            catch (UnauthorizedAccessException)
            {
                return 0;
            }
        }

        private static int ReadConfigFileMapped(string path, byte* destination, int capacity)
        {
            using FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, ConfigFileStreamBufferBytes, FileOptions.SequentialScan);
            long length = stream.Length;
            if (length <= 0L)
                return 0;

            int readLength = (int)Math.Min(capacity, length);
            using MemoryMappedFile mappedFile = MemoryMappedFile.CreateFromFile(stream, null, 0L, MemoryMappedFileAccess.Read, HandleInheritability.None, false);
            using MemoryMappedViewAccessor accessor = mappedFile.CreateViewAccessor(0L, readLength, MemoryMappedFileAccess.Read);
            byte* source = null;
            accessor.SafeMemoryMappedViewHandle.AcquirePointer(ref source);
            try
            {
                source += (int)accessor.PointerOffset;
                for (int i = 0; i < readLength; i++)
                    destination[i] = source[i];
            }
            finally
            {
                accessor.SafeMemoryMappedViewHandle.ReleasePointer();
            }

            return readLength;
        }

        private static int ReadConfigFileStreamed(string path, byte* destination, int capacity)
        {
            using FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, ConfigFileStreamBufferBytes, FileOptions.SequentialScan);
            int read = 0;
            Span<byte> remaining = new Span<byte>(destination, capacity);
            while (!remaining.IsEmpty)
            {
                int bytesRead = stream.Read(remaining);
                if (bytesRead <= 0)
                    break;

                read += bytesRead;
                remaining = remaining.Slice(bytesRead);
            }

            return read;
        }
    }
}
