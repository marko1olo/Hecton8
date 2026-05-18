using System;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Threading;
using Unity.Collections;

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
        // Volatile/Interlocked barriers publish staged Vault bytes before state flips to ConfigWorkerReady on ARM64.
        private Thread _configWorkerThread;
        private string _binaryConstantsPath;
        private string _csvOverridePath;
        private byte* _binaryConstantsWorkerPtr;
        private byte* _csvWorkerPtr;
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
            if (_configWorkerThread != null || !_binaryConstantBytes.IsCreated || !_csvBytes.IsCreated)
                return;

            _binaryConstantsPath = Path.Combine(UnityEngine.Application.streamingAssetsPath, "thermodynamic_constants.h8bin");
            _csvOverridePath = Path.Combine(UnityEngine.Application.streamingAssetsPath, "hazard_profiles.csv");
            _binaryConstantsWorkerPtr = (byte*)ResolvePointer(ref _binaryConstantBytes);
            _csvWorkerPtr = (byte*)ResolvePointer(ref _csvBytes);
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
            _binaryConstantsWorkerPtr = null;
            _csvWorkerPtr = null;
        }

        private void RequestBinaryConstantsLoad()
        {
            RequestConfigLoad(ref _binaryRequestState);
        }

        private void RequestCsvOverrideLoad()
        {
            RequestConfigLoad(ref _csvRequestState);
        }

        private static void RequestConfigLoad(ref int state)
        {
            int observed = Volatile.Read(ref state);
            if (observed == ConfigWorkerIdle || observed == ConfigWorkerFault)
                Interlocked.CompareExchange(ref state, ConfigWorkerRequested, observed);
        }

        private void ApplyPendingConfigLoads()
        {
            if (_constants.IsCreated && Volatile.Read(ref _binaryRequestState) == ConfigWorkerReady)
            {
                int read = Volatile.Read(ref _binaryWorkerReadCount);
                if (read >= BinaryConstantsBytes)
                {
                    NativeArray<byte> bytes = ResolveArray(ref _binaryConstantBytes);
                    float waterConductivity = ReadFloatLe(bytes, 0);
                    float heatDiffusion = ReadFloatLe(bytes, 4);
                    float radiationDiffusion = ReadFloatLe(bytes, 8);
                    float decay = ReadFloatLe(bytes, 12);
                    ref ThermodynamicsHazardConstants constants = ref _constants.GetElementAsRef(EnsureVault(), 0);
                    constants = SanitizeConstants(new ThermodynamicsHazardConstants
                    {
                        BaseWaterTempCelsius = waterConductivity,
                        HeatDiffusionRate = heatDiffusion,
                        RadiationDiffusionRate = radiationDiffusion,
                        RadiationDecayCoefficient = decay,
                        RockShieldingFactor = 0.05f,
                        VerticalHeatBias = 1.25f,
                        HeatDamageThresholdCelsius = 100f,
                        RadiationDamageThreshold = 0.35f
                    });
                    Volatile.Write(ref _binaryAppliedWriteTicks, Volatile.Read(ref _binaryWorkerWriteTicks));
                }

                Volatile.Write(ref _binaryRequestState, ConfigWorkerIdle);
            }

            if (_constants.IsCreated && Volatile.Read(ref _csvRequestState) == ConfigWorkerReady)
            {
                int read = Volatile.Read(ref _csvWorkerReadCount);
                long ticks = Volatile.Read(ref _csvWorkerWriteTicks);
                if (read > 0 && ticks != Volatile.Read(ref _csvAppliedWriteTicks))
                {
                    NativeArray<byte> bytes = ResolveArray(ref _csvBytes);
                    ref ThermodynamicsHazardConstants constants = ref _constants.GetElementAsRef(EnsureVault(), 0);
                    ThermodynamicsHazardConstants parsed = constants;
                    ParseCsvConstants(bytes, read, ref parsed);
                    constants = SanitizeConstants(parsed);
                    Volatile.Write(ref _csvAppliedWriteTicks, ticks);
                    _csvLastWriteUtc = new DateTime(ticks, DateTimeKind.Utc);
                }

                Volatile.Write(ref _csvRequestState, ConfigWorkerIdle);
            }
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

                if (Interlocked.CompareExchange(ref _csvRequestState, ConfigWorkerBusy, ConfigWorkerRequested) == ConfigWorkerRequested)
                {
                    worked = true;
                    RunCsvOverrideLoad();
                }

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
                int read = ReadConfigFile(_binaryConstantsPath, _binaryConstantsWorkerPtr, BinaryConstantsBytes, Volatile.Read(ref _binaryAppliedWriteTicks), out writeTicks);
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

        private void RunCsvOverrideLoad()
        {
            int nextState = ConfigWorkerIdle;
            try
            {
                long writeTicks;
                int read = ReadConfigFile(_csvOverridePath, _csvWorkerPtr, CsvBufferBytes, Volatile.Read(ref _csvAppliedWriteTicks), out writeTicks);
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

        private static int ReadConfigFile(string path, byte* destination, int capacity, long skipWriteTicks, out long writeTicks)
        {
            writeTicks = 0L;
            if (destination == null || capacity <= 0 || string.IsNullOrEmpty(path) || !File.Exists(path))
                return 0;

            writeTicks = File.GetLastWriteTimeUtc(path).Ticks;
            if (writeTicks == skipWriteTicks)
                return 0;

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
            using FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, CsvBufferBytes, FileOptions.SequentialScan);
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
            using FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, CsvBufferBytes, FileOptions.SequentialScan);
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
