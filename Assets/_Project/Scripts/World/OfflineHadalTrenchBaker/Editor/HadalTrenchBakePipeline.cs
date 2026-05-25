using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using Hecton8.Core.Memory;
using Hecton8.World.OfflineHadalTrenchBaker;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEditor;

namespace Hecton8.World.OfflineHadalTrenchBaker.Editor
{
    public struct HadalTrenchBakeResult
    {
        public string H8BinPath;
        public int VoxelCount;
        public int FaultCount;
        public int RleRunCount;
        public int VentCount;
        public int AdaptiveBlockCount;
        public int AdaptiveBlockSizeVoxels;
        public int UncompressedDensityBytes;
        public int CompressedDensityBytes;
        public uint CompressionMode;
        public uint WarningFlags;
        public uint PayloadValidationFlags;
        public ulong PayloadHash;
        public long OutputFileBytes;
        public double ExcavatedCubicMeters;
        public float MaxDepthMeters;
        public float CarvingMilliseconds;
        public float SerializationMilliseconds;
    }

    public static class HadalTrenchBakePipeline
    {
        private const string OutputFolder = "Assets/StreamingAssets/Hecton8/HadalTrenches";
        private const string DefaultOutputFile = "hadal_trench_sector_0000.h8bin";
        private const string ReportPath = "Docs/Reports/TRENCH_BAKE_REPORT.json";
        private const string DumpPath = "Docs/AgentLogs/Dump_SHINOBU_241.bin";
        private static AsyncTrenchBakeSession s_activeSession;

        static HadalTrenchBakePipeline()
        {
            AssemblyReloadEvents.beforeAssemblyReload += CancelActiveBake;
            EditorApplication.quitting += CancelActiveBake;
        }

        public static bool BakeAsync(
            HadalTrenchBakeConfigDTO config,
            Action<HadalTrenchBakeResult> onCompleted,
            Action<Exception> onFailed)
        {
            if (s_activeSession != null)
                return false;

            s_activeSession = new AsyncTrenchBakeSession(config, onCompleted, onFailed);
            if (!s_activeSession.TryStart())
            {
                s_activeSession.Dispose();
                s_activeSession = null;
                return false;
            }

            EditorApplication.update += UpdateActiveBake;
            return true;
        }

        public static HadalTrenchBakeConfigDTO DefaultConfig()
        {
            return new HadalTrenchBakeConfigDTO
            {
                SectorOriginAUP = new double3(-50000.0d, -6200.0d, -50000.0d),
                WorldMinAUP = new double3(-50000.0d, -6200.0d, -50000.0d),
                WorldMaxAUP = new double3(50000.0d, 0.0d, 50000.0d),
                SeaFloorAUPY = -1800.0d,
                Resolution = new int3(128, 128, 128),
                VoxelSizeMeters = 48f,
                VoronoiCellSizeMeters = 3200f,
                DefaultDepthMeters = 5000f,
                DefaultWidthMeters = 420f,
                NoiseIntensity = 96f,
                NoiseFrequency = 0.0025f,
                GlobalQualityWeight = 0.7f,
                Seed = 0x5348494Eu,
                FaultGridX = 32,
                FaultGridZ = 32,
                FaultCount = 32 * 32 * 2,
                MaxVentCount = 32 * 32 * 2,
                Flags = HadalTrenchBakeConstants.RollbackExcludedFlag,
                _pad0 = 0ul,
                _pad1 = 0ul
            };
        }

        private static void UpdateActiveBake()
        {
            if (s_activeSession == null)
            {
                EditorApplication.update -= UpdateActiveBake;
                return;
            }

            if (!s_activeSession.Update())
                return;

            EditorApplication.update -= UpdateActiveBake;
            s_activeSession.Dispose();
            s_activeSession = null;
        }

        private static void CancelActiveBake()
        {
            if (s_activeSession == null)
                return;

            EditorApplication.update -= UpdateActiveBake;
            s_activeSession.Cancel();
            s_activeSession.Dispose();
            s_activeSession = null;
        }

        private static HadalTrenchBakeConfigDTO SanitizeConfig(HadalTrenchBakeConfigDTO config)
        {
            int rx = math.clamp(config.Resolution.x <= 0 ? HadalTrenchBakeConstants.DefaultVoxelResolution : config.Resolution.x, 32, 256);
            int ry = math.clamp(config.Resolution.y <= 0 ? rx : config.Resolution.y, 32, 256);
            int rz = math.clamp(config.Resolution.z <= 0 ? rx : config.Resolution.z, 32, 256);
            config.Resolution = new int3(rx, ry, rz);
            config.SectorOriginAUP = ClampAup(config.SectorOriginAUP);
            config.VoxelSizeMeters = ClampFinite(config.VoxelSizeMeters, 0.5f, 128f, 48f);
            config.VoronoiCellSizeMeters = ClampFinite(config.VoronoiCellSizeMeters, config.VoxelSizeMeters * 16f, 20000f, 3200f);
            config.DefaultDepthMeters = ClampFinite(config.DefaultDepthMeters, config.VoxelSizeMeters * 8f, 10000f, 5000f);
            config.DefaultWidthMeters = ClampFinite(config.DefaultWidthMeters, config.VoxelSizeMeters * 4f, 5000f, 420f);
            config.NoiseIntensity = ClampFinite(config.NoiseIntensity, 0f, 512f, 96f);
            config.NoiseFrequency = ClampFinite(config.NoiseFrequency, 0.00001f, 0.05f, 0.0025f);
            config.GlobalQualityWeight = ClampFinite(config.GlobalQualityWeight, 0f, 1f, 0.7f);
            config.Seed = config.Seed == 0u ? 0x5348494Eu : config.Seed;
            config.FaultGridX = math.clamp(config.FaultGridX <= 0 ? HadalTrenchBakeConstants.DefaultFaultGridX : config.FaultGridX, 1, 128);
            config.FaultGridZ = math.clamp(config.FaultGridZ <= 0 ? HadalTrenchBakeConstants.DefaultFaultGridZ : config.FaultGridZ, 1, 128);
            config.FaultCount = config.FaultGridX * config.FaultGridZ * 2;
            config.MaxVentCount = math.max(config.FaultCount, config.MaxVentCount);
            config.Flags |= HadalTrenchBakeConstants.RollbackExcludedFlag;
            return config;
        }

        private static double3 ClampAup(double3 value)
        {
            return new double3(
                ClampFinite(value.x, -100000.0d, 100000.0d),
                ClampFinite(value.y, -100000.0d, 100000.0d),
                ClampFinite(value.z, -100000.0d, 100000.0d));
        }

        private static double ClampFinite(double value, double min, double max)
        {
            if (!math.isfinite(value))
                return 0.0d;
            if (value < min)
                return min;
            return value > max ? max : value;
        }

        private static float ClampFinite(float value, float min, float max, float fallback)
        {
            if (!math.isfinite(value))
                return fallback;
            if (value < min)
                return min;
            return value > max ? max : value;
        }

        private static string EnsureOutputPath()
        {
            Directory.CreateDirectory(OutputFolder);
            return Path.Combine(OutputFolder, DefaultOutputFile).Replace('\\', '/');
        }

        private static void WriteReport(in HadalTrenchBakeResult result, in HadalTrenchBakeConfigDTO config, double totalMs, int nonFiniteCount)
        {
            Directory.CreateDirectory("Docs/Reports");
            StringBuilder builder = new StringBuilder(1536);
            builder.Append("{\n");
            builder.Append("  \"version\": ").Append(HadalTrenchBakeConstants.ReportVersion).Append(",\n");
            builder.Append("  \"agent\": \"SHINOBU_241\",\n");
            builder.Append("  \"output\": \"").Append(result.H8BinPath).Append("\",\n");
            builder.Append("  \"sectorsCarved\": 1,\n");
            builder.Append("  \"resolution\": [").Append(config.Resolution.x).Append(", ").Append(config.Resolution.y).Append(", ").Append(config.Resolution.z).Append("],\n");
            builder.Append("  \"voxelSizeMeters\": ").Append(config.VoxelSizeMeters.ToString("0.####", CultureInfo.InvariantCulture)).Append(",\n");
            builder.Append("  \"faultSegments\": ").Append(result.FaultCount).Append(",\n");
            builder.Append("  \"thermalVentRecords\": ").Append(result.VentCount).Append(",\n");
            builder.Append("  \"adaptiveBlocks\": ").Append(result.AdaptiveBlockCount).Append(",\n");
            builder.Append("  \"adaptiveBlockSizeVoxels\": ").Append(result.AdaptiveBlockSizeVoxels).Append(",\n");
            builder.Append("  \"rleRuns\": ").Append(result.RleRunCount).Append(",\n");
            builder.Append("  \"compressionMode\": ").Append(result.CompressionMode).Append(",\n");
            builder.Append("  \"uncompressedDensityBytes\": ").Append(result.UncompressedDensityBytes).Append(",\n");
            builder.Append("  \"compressedDensityBytes\": ").Append(result.CompressedDensityBytes).Append(",\n");
            builder.Append("  \"payloadHash\": \"0x").Append(result.PayloadHash.ToString("X16", CultureInfo.InvariantCulture)).Append("\",\n");
            builder.Append("  \"maxDepthMeters\": ").Append(result.MaxDepthMeters.ToString("0.###", CultureInfo.InvariantCulture)).Append(",\n");
            builder.Append("  \"excavatedCubicMeters\": ").Append(result.ExcavatedCubicMeters.ToString("0.###", CultureInfo.InvariantCulture)).Append(",\n");
            builder.Append("  \"timingsMs\": { \"carving\": ").Append(result.CarvingMilliseconds.ToString("0.###", CultureInfo.InvariantCulture));
            builder.Append(", \"serialization\": ").Append(result.SerializationMilliseconds.ToString("0.###", CultureInfo.InvariantCulture));
            builder.Append(", \"total\": ").Append(totalMs.ToString("0.###", CultureInfo.InvariantCulture)).Append(" },\n");
            builder.Append("  \"criticalWarning\": ").Append(nonFiniteCount > 0 ? "\"CRITICAL_WARNING\"" : "null").Append(",\n");
            builder.Append("  \"nonFiniteDensityCount\": ").Append(nonFiniteCount).Append(",\n");
            builder.Append("  \"rollbackExcluded\": true,\n");
            builder.Append("  \"payloadAlignmentBytes\": ").Append(HadalTrenchBakeConstants.PayloadSectionAlignmentBytes).Append(",\n");
            builder.Append("  \"interSectionPadding\": \"explicit zero padding, excluded from payload hash\",\n");
            bool densityPreludeValidated = (result.PayloadValidationFlags & HadalTrenchPayloadValidationFlags.PreludeMismatch) == 0u;
            builder.Append("  \"densityPreludeValidated\": ").Append(densityPreludeValidated ? "true" : "false").Append(",\n");
            builder.Append("  \"payloadValidationFlags\": ").Append(result.PayloadValidationFlags).Append(",\n");
            builder.Append("  \"outputFileBytes\": ").Append(result.OutputFileBytes).Append(",\n");
            builder.Append("  \"dataMonolithStatus\": \"OUTSIDE_DATAMONOLITH_SUBTREE_NOT_STATIC_DATA_MONOLITH\",\n");
            builder.Append("  \"validationState\": \"PENDING_COMPILE_PUBLISHED_BAKE_REQUIRED_FOR_RUNTIME_PROOF\",\n");
            builder.Append("  \"warningFlags\": ").Append(result.WarningFlags).Append("\n");
            builder.Append("}\n");
            File.WriteAllText(ReportPath, builder.ToString(), new UTF8Encoding(false));
        }

        private static HadalTrenchBakeTelemetryEntry BuildTelemetry(
            in HadalTrenchBakeResult result,
            in HadalTrenchBakeConfigDTO config,
            uint stage)
        {
            return new HadalTrenchBakeTelemetryEntry
            {
                SectorOriginAUP = config.SectorOriginAUP,
                Frame = 0u,
                FaultCount = result.FaultCount,
                VoxelCount = result.VoxelCount,
                RleRunCount = result.RleRunCount,
                CarvingMilliseconds = result.CarvingMilliseconds,
                SerializationMilliseconds = result.SerializationMilliseconds,
                WarningFlags = result.WarningFlags,
                StateHash = HadalTrenchBakeMath.Mix((uint)result.VoxelCount ^ ((uint)result.RleRunCount << 1) ^ config.Seed),
                DumpReason = 0u,
                Stage = stage
            };
        }

        private static void DumpBlackBox(NativeArray<HadalTrenchBakeTelemetryEntry> telemetry, uint reason)
        {
            Directory.CreateDirectory("Docs/AgentLogs");
            using (FileStream stream = new FileStream(DumpPath, FileMode.Create, FileAccess.Write, FileShare.Read))
            using (BinaryWriter writer = new BinaryWriter(stream))
            {
                writer.Write(HadalTrenchBakeConstants.DumpMagic);
                writer.Write(reason);
                writer.Write(telemetry.Length);
                for (int i = 0; i < telemetry.Length; i++)
                {
                    HadalTrenchBakeTelemetryEntry entry = telemetry[i];
                    writer.Write(entry.SectorOriginAUP.x);
                    writer.Write(entry.SectorOriginAUP.y);
                    writer.Write(entry.SectorOriginAUP.z);
                    writer.Write(entry.Frame);
                    writer.Write(entry.FaultCount);
                    writer.Write(entry.VoxelCount);
                    writer.Write(entry.RleRunCount);
                    writer.Write(entry.CarvingMilliseconds);
                    writer.Write(entry.SerializationMilliseconds);
                    writer.Write(entry.WarningFlags);
                    writer.Write(entry.StateHash);
                    writer.Write(reason != 0u ? reason : entry.DumpReason);
                    writer.Write(entry.Stage);
                }
            }
        }

        private enum AsyncPhase
        {
            NetworkAndBase = 0,
            Carving = 1,
            QuantizeAndAux = 2,
            Rle = 3,
            Serializing = 4
        }

        private sealed class AsyncTrenchBakeSession : IDisposable
        {
            private const SystemID BakeSessionMemoryOwner = SystemID.ContentAuthority;

            private readonly Action<HadalTrenchBakeResult> _onCompleted;
            private readonly Action<Exception> _onFailed;
            private readonly Stopwatch _totalStopwatch = new Stopwatch();
            private readonly Stopwatch _stageStopwatch = new Stopwatch();

            private HadalTrenchBakeConfigDTO _config;
            private NativeArray<float> _densities;
            private NativeArray<float> _excavatedMeters3;
            private NativeArray<byte> _nonFiniteFlags;
            private NativeArray<sbyte> _quantized;
            private NativeArray<FaultLineParamsDTO> _faults;
            private NativeArray<ThermalVentSpawnDTO> _vents;
            private NativeArray<HadalTrenchAdaptiveBlockDTO> _adaptiveBlocks;
            private NativeList<HadalTrenchRleRunDTO> _rleRuns;
            private NativeArray<HadalTrenchBakeTelemetryEntry> _telemetry;
            private JobHandle _activeHandle;
            private AsyncPhase _phase;
            private AsyncPayloadWriteSession _writeSession;
            private HadalTrenchBakeResult _result;
            private int _voxelCount;
            private int3 _blockGrid;
            private int _blockSize;
            private int _nonFiniteCount;
            private uint _telemetryCursor;
            private bool _completed;

            public AsyncTrenchBakeSession(
                HadalTrenchBakeConfigDTO config,
                Action<HadalTrenchBakeResult> onCompleted,
                Action<Exception> onFailed)
            {
                _config = config;
                _onCompleted = onCompleted;
                _onFailed = onFailed;
            }

            public bool TryStart()
            {
                try
                {
                    Directory.CreateDirectory(OutputFolder);
                    Directory.CreateDirectory("Docs/Reports");
                    Directory.CreateDirectory("Docs/AgentLogs");
                    _config = SanitizeConfig(_config);
                    _voxelCount = _config.Resolution.x * _config.Resolution.y * _config.Resolution.z;
                    int faultCount = _config.FaultCount;
                    _blockSize = math.clamp((int)math.round(math.lerp(16f, 4f, _config.GlobalQualityWeight)), 4, 16);
                    _blockGrid = (int3)math.ceil((float3)_config.Resolution / _blockSize);
                    int adaptiveCount = _blockGrid.x * _blockGrid.y * _blockGrid.z;

                    _densities = H8Memory.Allocate<float>(_voxelCount, BakeSessionMemoryOwner, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
                    _excavatedMeters3 = H8Memory.Allocate<float>(_voxelCount, BakeSessionMemoryOwner, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
                    _nonFiniteFlags = H8Memory.Allocate<byte>(_voxelCount, BakeSessionMemoryOwner, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
                    _quantized = H8Memory.Allocate<sbyte>(_voxelCount, BakeSessionMemoryOwner, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
                    _faults = H8Memory.Allocate<FaultLineParamsDTO>(faultCount, BakeSessionMemoryOwner, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
                    _vents = H8Memory.Allocate<ThermalVentSpawnDTO>(math.max(1, faultCount), BakeSessionMemoryOwner, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
                    _adaptiveBlocks = H8Memory.Allocate<HadalTrenchAdaptiveBlockDTO>(math.max(1, adaptiveCount), BakeSessionMemoryOwner, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
                    _telemetry = H8Memory.Allocate<HadalTrenchBakeTelemetryEntry>(HadalTrenchBakeConstants.TelemetryFrames, BakeSessionMemoryOwner, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
                    if (!AreNativeArrayBuffersCreated())
                    {
                        Dispose();
                        return false;
                    }

                    _rleRuns = new NativeList<HadalTrenchRleRunDTO>(_voxelCount, Allocator.Persistent);
                    InitializeTelemetryRing();

                    _totalStopwatch.Restart();
                    _stageStopwatch.Restart();
                    _phase = AsyncPhase.NetworkAndBase;
                    EditorUtility.DisplayProgressBar("Hadal Trench Forge", "Generating Voronoi faults and mock solid voxel block", 0.08f);
                    JobHandle network = new GenerateTectonicNetworkJob { Faults = _faults, Config = _config }.Schedule(_config.FaultGridX * _config.FaultGridZ, 32);
                    JobHandle mock = new GenerateMockTrenchJob { Densities = _densities, Config = _config }.Schedule(_voxelCount, 64);
                    _activeHandle = JobHandle.CombineDependencies(network, mock);
                    return true;
                }
                catch (Exception ex)
                {
                    Fail(ex);
                    return false;
                }
            }

            public bool Update()
            {
                try
                {
                    EditorUtility.DisplayProgressBar("Hadal Trench Forge", ResolveProgressText(), ResolveProgress());
                    if (_phase == AsyncPhase.Serializing)
                        return PollSerialization();

                    if (!_activeHandle.IsCompleted)
                        return false;

                    _activeHandle.Complete();
                    if (_phase == AsyncPhase.NetworkAndBase)
                    {
                        _phase = AsyncPhase.Carving;
                        _stageStopwatch.Restart();
                        _activeHandle = new ExecuteTrenchSubtractionJob
                        {
                            Densities = _densities,
                            ExcavatedMeters3 = _excavatedMeters3,
                            NonFiniteFlags = _nonFiniteFlags,
                            Faults = _faults,
                            Config = _config
                        }.Schedule(_voxelCount, 64);
                        return false;
                    }

                    if (_phase == AsyncPhase.Carving)
                    {
                        PushTelemetry(1u);
                        _result.CarvingMilliseconds = (float)_stageStopwatch.Elapsed.TotalMilliseconds;
                        _phase = AsyncPhase.QuantizeAndAux;
                        JobHandle quantize = new QuantizeTrenchDensityJob { Densities = _densities, Quantized = _quantized }.Schedule(_voxelCount, 64);
                        JobHandle vents = new GenerateThermalVentNodesJob { Faults = _faults, Vents = _vents, Config = _config }.Schedule(_faults.Length, 32);
                        JobHandle blocks = new BuildTrenchAdaptiveBlocksJob
                        {
                            Quantized = _quantized,
                            Blocks = _adaptiveBlocks,
                            Config = _config,
                            BlockSize = _blockSize,
                            BlockGrid = _blockGrid
                        }.Schedule(_adaptiveBlocks.Length, 32, quantize);
                        _activeHandle = JobHandle.CombineDependencies(quantize, vents, blocks);
                        return false;
                    }

                    if (_phase == AsyncPhase.QuantizeAndAux)
                    {
                        PushTelemetry(2u);
                        _phase = AsyncPhase.Rle;
                        _activeHandle = new RleCompressTrenchDensityJob { Quantized = _quantized, Runs = _rleRuns }.Schedule();
                        return false;
                    }

                    BeginSerialization();
                    return false;
                }
                catch (Exception ex)
                {
                    Fail(ex);
                    return true;
                }
            }

            public void Cancel()
            {
                _activeHandle.Complete();
                if (_writeSession != null)
                    _writeSession.WaitAndDispose(250);
            }

            public void Dispose()
            {
                _activeHandle.Complete();
                EditorUtility.ClearProgressBar();
                if (_writeSession != null)
                    _writeSession.WaitAndDispose(250);
                ReleaseTracked(ref _densities);
                ReleaseTracked(ref _excavatedMeters3);
                ReleaseTracked(ref _nonFiniteFlags);
                ReleaseTracked(ref _quantized);
                ReleaseTracked(ref _faults);
                ReleaseTracked(ref _vents);
                ReleaseTracked(ref _adaptiveBlocks);
                if (_rleRuns.IsCreated) _rleRuns.Dispose();
                ReleaseTracked(ref _telemetry);
            }

            private bool AreNativeArrayBuffersCreated()
            {
                return _densities.IsCreated &&
                       _excavatedMeters3.IsCreated &&
                       _nonFiniteFlags.IsCreated &&
                       _quantized.IsCreated &&
                       _faults.IsCreated &&
                       _vents.IsCreated &&
                       _adaptiveBlocks.IsCreated &&
                       _telemetry.IsCreated;
            }

            private static void ReleaseTracked<T>(ref NativeArray<T> array) where T : struct
            {
                if (array.IsCreated)
                    H8Memory.Release(ref array, BakeSessionMemoryOwner);
            }

            private void BeginSerialization()
            {
                _phase = AsyncPhase.Serializing;
                _stageStopwatch.Restart();
                _nonFiniteCount = CountNonFiniteFlags();
                _result.VoxelCount = _voxelCount;
                _result.FaultCount = _faults.Length;
                _result.RleRunCount = _rleRuns.Length;
                _result.VentCount = _vents.Length;
                _result.AdaptiveBlockCount = _adaptiveBlocks.Length;
                _result.AdaptiveBlockSizeVoxels = _blockSize;
                _result.ExcavatedCubicMeters = SumExcavatedVolume();
                _result.MaxDepthMeters = ResolveMaxFaultDepth();
                _result.WarningFlags = _nonFiniteCount > 0 ? HadalTrenchBakeConstants.WarningNonFiniteDensity : 0u;
                string path = EnsureOutputPath();
                HadalTrenchPayloadParts payload = BuildH8BinPayload(path, ref _result);
                PushTelemetry(4u);
                if (_nonFiniteCount > 0)
                    DumpBlackBox(_telemetry, 2u);

                _writeSession = AsyncPayloadWriteSession.Start(path, payload);
            }

            private bool PollSerialization()
            {
                if (_writeSession == null || !_writeSession.IsCompleted)
                    return false;

                if (!_writeSession.TryFinish())
                    return false;

                Exception writeException = _writeSession.Exception;
                string tempPath = _writeSession.TempPath;
                string finalPath = _writeSession.FinalPath;
                if (writeException != null)
                {
                    _writeSession.Dispose();
                    _writeSession = null;
                    Fail(writeException);
                    return true;
                }

                _result.SerializationMilliseconds = (float)_stageStopwatch.Elapsed.TotalMilliseconds;
                if (!HadalTrenchPayloadValidator.ValidateFile(tempPath, out HadalTrenchPayloadValidationResult validation))
                {
                    string invalidPath = PreserveInvalidTempPayload(tempPath);
                    _writeSession.MarkCommitted();
                    _result.WarningFlags |= HadalTrenchBakeConstants.WarningLayoutMismatch;
                    _result.PayloadValidationFlags = validation.Flags;
                    _result.OutputFileBytes = validation.FileBytes;
                    _result.H8BinPath = invalidPath;
                    PushTelemetry(5u);
                    WriteReport(in _result, in _config, _totalStopwatch.Elapsed.TotalMilliseconds, _nonFiniteCount);
                    HadalTrenchSelfAudit.WriteAudit(in _result, in _config, _nonFiniteCount);
                    Fail(new InvalidDataException("Hadal trench temp payload validation failed."));
                    _writeSession.Dispose();
                    _writeSession = null;
                    _completed = true;
                    return true;
                }

                ReplaceTempPayload(tempPath, finalPath);
                _writeSession.MarkCommitted();
                _writeSession.Dispose();
                _writeSession = null;
                _result.H8BinPath = finalPath;
                _result.PayloadValidationFlags = validation.Flags;
                _result.OutputFileBytes = validation.FileBytes;
                PushTelemetry(5u);
                WriteReport(in _result, in _config, _totalStopwatch.Elapsed.TotalMilliseconds, _nonFiniteCount);
                HadalTrenchSelfAudit.WriteAudit(in _result, in _config, _nonFiniteCount);
                AssetDatabase.Refresh();
                InvokeCompleted();
                _completed = true;
                return true;
            }

            private static void ReplaceTempPayload(string tempPath, string finalPath)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(finalPath) ?? OutputFolder);
                if (File.Exists(finalPath))
                {
                    string backupPath = finalPath + ".bak";
                    File.Replace(tempPath, finalPath, backupPath, true);
                    if (File.Exists(backupPath))
                        File.Delete(backupPath);
                    return;
                }

                File.Move(tempPath, finalPath);
            }

            private static string PreserveInvalidTempPayload(string tempPath)
            {
                if (!File.Exists(tempPath))
                    return tempPath;

                string invalidPath = tempPath + ".invalid";
                if (File.Exists(invalidPath))
                    File.Delete(invalidPath);

                File.Move(tempPath, invalidPath);
                return invalidPath;
            }

            private void Fail(Exception exception)
            {
                if (_telemetry.IsCreated)
                    DumpBlackBox(_telemetry, 1u);
                InvokeFailed(exception);
            }

            private void InvokeCompleted()
            {
                try
                {
                    _onCompleted?.Invoke(_result);
                }
                catch (Exception ex)
                {
                    UnityEngine.Debug.LogException(ex);
                }
            }

            private void InvokeFailed(Exception exception)
            {
                try
                {
                    _onFailed?.Invoke(exception);
                }
                catch (Exception ex)
                {
                    UnityEngine.Debug.LogException(ex);
                }
            }

            private void InitializeTelemetryRing()
            {
                if (!_telemetry.IsCreated)
                    return;

                HadalTrenchBakeResult result = default;
                _telemetryCursor = 0u;
                for (int i = 0; i < _telemetry.Length; i++)
                {
                    HadalTrenchBakeTelemetryEntry entry = BuildTelemetry(in result, in _config, 0u);
                    entry.Frame = 0u;
                    entry.DumpReason = 0u;
                    _telemetry[i] = entry;
                }
            }

            private void PushTelemetry(uint stage)
            {
                if (!_telemetry.IsCreated || _telemetry.Length == 0)
                    return;

                uint frame = _telemetryCursor++;
                int index = (int)(frame % (uint)_telemetry.Length);
                HadalTrenchBakeTelemetryEntry entry = BuildTelemetry(in _result, in _config, stage);
                entry.Frame = frame;
                _telemetry[index] = entry;
            }

            private int CountNonFiniteFlags()
            {
                int count = 0;
                for (int i = 0; i < _nonFiniteFlags.Length; i++)
                    count += _nonFiniteFlags[i] != 0 ? 1 : 0;
                return count;
            }

            private double SumExcavatedVolume()
            {
                double sum = 0.0d;
                for (int i = 0; i < _excavatedMeters3.Length; i++)
                    sum += _excavatedMeters3[i];
                return sum;
            }

            private float ResolveMaxFaultDepth()
            {
                float depth = 0f;
                for (int i = 0; i < _faults.Length; i++)
                    depth = math.max(depth, _faults[i].Depth);
                return depth;
            }

            private HadalTrenchPayloadParts BuildH8BinPayload(string path, ref HadalTrenchBakeResult result)
            {
                byte[] rleBytes = CopyNativeArrayToBytes(_rleRuns.AsArray());
                byte[] lz4Bytes = HadalTrenchLz4BlockCodec.Compress(rleBytes);
                bool useLz4 = lz4Bytes.Length > 0 && lz4Bytes.Length < rleBytes.Length;
                byte[] densityPayload = useLz4 ? lz4Bytes : rleBytes;
                if (!useLz4)
                    result.WarningFlags |= HadalTrenchBakeConstants.WarningCompressionExpanded;

                byte[] ventsBytes = CopyNativeArrayToBytes(_vents);
                byte[] blocksBytes = CopyNativeArrayToBytes(_adaptiveBlocks);
                const int densityPreludeBytes = 8;
                ulong densityOffset = (ulong)HadalTrenchBakeConstants.HeaderBytes + (ulong)densityPreludeBytes;
                ulong densityEnd = densityOffset + (ulong)densityPayload.Length;
                ulong ventOffset = AlignUp(densityEnd, HadalTrenchBakeConstants.PayloadSectionAlignmentBytes);
                ulong ventEnd = ventOffset + (ulong)ventsBytes.Length;
                ulong adaptiveOffset = AlignUp(ventEnd, HadalTrenchBakeConstants.PayloadSectionAlignmentBytes);
                ulong totalFileBytes = adaptiveOffset + (ulong)blocksBytes.Length;
                byte[] densityPadding = BuildPaddingBytes((int)(ventOffset - densityEnd));
                byte[] ventPadding = BuildPaddingBytes((int)(adaptiveOffset - ventEnd));
                ulong hash = HashPayload(densityPayload, ventsBytes, blocksBytes);
                uint compressionMode = (uint)(useLz4 ? HadalTrenchCompressionMode.RleLz4Block : HadalTrenchCompressionMode.Rle);
                HadalTrenchChunkHeaderDTO header = new HadalTrenchChunkHeaderDTO
                {
                    Magic = HadalTrenchBakeConstants.H8BinMagic,
                    Version = HadalTrenchBakeConstants.FileVersion,
                    Flags = result.WarningFlags | HadalTrenchBakeConstants.RollbackExcludedFlag,
                    Resolution = _config.Resolution,
                    SectorOriginAUP = _config.SectorOriginAUP,
                    VoxelSizeMeters = _config.VoxelSizeMeters,
                    CompressionMode = compressionMode,
                    CompressedBytes = densityPayload.Length,
                    RleRunCount = _rleRuns.Length,
                    VentCount = _vents.Length,
                    AdaptiveBlockCount = _adaptiveBlocks.Length,
                    MaxDepthMeters = result.MaxDepthMeters,
                    ExcavatedCubicMeters = result.ExcavatedCubicMeters,
                    DensityPayloadOffset = densityOffset,
                    VentPayloadOffset = ventOffset,
                    AdaptivePayloadOffset = adaptiveOffset,
                    PayloadHash = hash,
                    HeaderBytes = HadalTrenchBakeConstants.HeaderBytes,
                    EndianMarker = HadalTrenchBakeConstants.PayloadEndianMarker,
                    UncompressedBytes = rleBytes.Length,
                    DensityPreludeBytes = densityPreludeBytes,
                    TotalFileBytes = totalFileBytes,
                    SectionAlignmentBytes = HadalTrenchBakeConstants.PayloadSectionAlignmentBytes,
                    ChecksumType = HadalTrenchBakeConstants.PayloadChecksumFnv1A64,
                    SchemaHash = HadalTrenchBakeConstants.PayloadSchemaHash,
                    _pad0 = 0u
                };

                result.H8BinPath = path;
                result.CompressionMode = compressionMode;
                result.UncompressedDensityBytes = rleBytes.Length;
                result.CompressedDensityBytes = densityPayload.Length;
                result.PayloadHash = hash;
                return new HadalTrenchPayloadParts
                {
                    Header = BuildHeaderBytes(in header),
                    DensityPrelude = BuildDensityPreludeBytes(rleBytes.Length, densityPayload.Length),
                    DensityPayload = densityPayload,
                    DensityPadding = densityPadding,
                    VentPayload = ventsBytes,
                    VentPadding = ventPadding,
                    AdaptivePayload = blocksBytes
                };
            }

            private string ResolveProgressText()
            {
                if (_phase == AsyncPhase.Carving)
                    return "Subtracting hadal trench SDF from voxel field";
                if (_phase == AsyncPhase.QuantizeAndAux)
                    return "Quantizing voxels, vent DTOs, and adaptive blocks";
                if (_phase == AsyncPhase.Rle)
                    return "RLE compressing carved voxel density";
                if (_phase == AsyncPhase.Serializing)
                    return "Writing .h8bin payload asynchronously";
                return "Generating Voronoi faults and mock solid voxel block";
            }

            private float ResolveProgress()
            {
                if (_phase == AsyncPhase.Carving)
                    return 0.35f;
                if (_phase == AsyncPhase.QuantizeAndAux)
                    return 0.62f;
                if (_phase == AsyncPhase.Rle)
                    return 0.78f;
                if (_phase == AsyncPhase.Serializing)
                    return _completed ? 1f : 0.9f;
                return 0.08f;
            }
        }

        private static unsafe byte[] CopyNativeArrayToBytes<T>(NativeArray<T> source) where T : struct
        {
            int elementSize = UnsafeUtility.SizeOf<T>();
            int byteCount = source.Length * elementSize;
            byte[] bytes = new byte[byteCount];
            if (byteCount == 0)
                return bytes;

            void* src = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(source);
            fixed (byte* dst = bytes)
                UnsafeUtility.MemCpy(dst, src, byteCount);
            return bytes;
        }

        private static byte[] BuildHeaderBytes(in HadalTrenchChunkHeaderDTO header)
        {
            byte[] bytes = new byte[(int)HadalTrenchBakeConstants.HeaderBytes];
            using (MemoryStream memory = new MemoryStream(bytes))
            using (BinaryWriter writer = new BinaryWriter(memory))
            {
                WriteHeader(writer, in header);
                writer.Flush();
            }

            return bytes;
        }

        private static byte[] BuildDensityPreludeBytes(int uncompressedBytes, int compressedBytes)
        {
            byte[] bytes = new byte[8];
            WriteInt32LittleEndian(bytes, 0, uncompressedBytes);
            WriteInt32LittleEndian(bytes, 4, compressedBytes);
            return bytes;
        }

        private static byte[] BuildPaddingBytes(int byteCount)
        {
            return byteCount <= 0 ? Array.Empty<byte>() : new byte[byteCount];
        }

        private static ulong AlignUp(ulong value, uint alignment)
        {
            ulong safeAlignment = alignment == 0u ? 1ul : alignment;
            ulong mask = safeAlignment - 1ul;
            return (value + mask) & ~mask;
        }

        private static void WriteInt32LittleEndian(byte[] bytes, int offset, int value)
        {
            uint raw = unchecked((uint)value);
            bytes[offset] = (byte)raw;
            bytes[offset + 1] = (byte)(raw >> 8);
            bytes[offset + 2] = (byte)(raw >> 16);
            bytes[offset + 3] = (byte)(raw >> 24);
        }

        private sealed class HadalTrenchPayloadParts
        {
            public byte[] Header;
            public byte[] DensityPrelude;
            public byte[] DensityPayload;
            public byte[] DensityPadding;
            public byte[] VentPayload;
            public byte[] VentPadding;
            public byte[] AdaptivePayload;

            public byte[] ResolveBuffer(int index)
            {
                if (index == 0)
                    return Header;
                if (index == 1)
                    return DensityPrelude;
                if (index == 2)
                    return DensityPayload;
                if (index == 3)
                    return DensityPadding;
                if (index == 4)
                    return VentPayload;
                if (index == 5)
                    return VentPadding;
                if (index == 6)
                    return AdaptivePayload;
                return null;
            }
        }

        private sealed class AsyncPayloadWriteSession : IDisposable
        {
            private readonly HadalTrenchPayloadParts _parts;
            private FileStream _stream;
            private IAsyncResult _asyncResult;
            private int _bufferIndex;
            private bool _finished;
            private bool _disposed;
            private bool _committed;

            public Exception Exception;
            public string FinalPath;
            public string TempPath;

            private AsyncPayloadWriteSession(HadalTrenchPayloadParts parts)
            {
                _parts = parts;
            }

            public bool IsCompleted
            {
                get { return _finished || _asyncResult == null || _asyncResult.IsCompleted; }
            }

            public static AsyncPayloadWriteSession Start(string path, HadalTrenchPayloadParts parts)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path) ?? OutputFolder);
                AsyncPayloadWriteSession session = new AsyncPayloadWriteSession(parts);
                session.FinalPath = path;
                session.TempPath = path + ".tmp";
                session._stream = new FileStream(session.TempPath, FileMode.Create, FileAccess.Write, FileShare.Read, 131072, true);
                session.BeginNextWrite();
                return session;
            }

            public bool TryFinish()
            {
                if (_finished)
                    return true;

                if (_asyncResult == null)
                {
                    _finished = true;
                    DisposeStream();
                    return true;
                }

                if (!_asyncResult.IsCompleted)
                    return false;

                try
                {
                    _stream.EndWrite(_asyncResult);
                    _bufferIndex++;
                    _asyncResult = null;
                    if (BeginNextWrite())
                        return false;
                }
                catch (Exception ex)
                {
                    Exception = ex;
                    _finished = true;
                    DisposeStream();
                }

                return true;
            }

            private bool BeginNextWrite()
            {
                while (true)
                {
                    byte[] buffer = _parts.ResolveBuffer(_bufferIndex);
                    if (buffer == null)
                    {
                        _stream.Flush();
                        _finished = true;
                        DisposeStream();
                        return false;
                    }

                    if (buffer.Length == 0)
                    {
                        _bufferIndex++;
                        continue;
                    }

                    _asyncResult = _stream.BeginWrite(buffer, 0, buffer.Length, null, null);
                    return true;
                }
            }

            public void WaitAndDispose(int milliseconds)
            {
                if (!_finished && _asyncResult != null)
                {
                    _asyncResult.AsyncWaitHandle.WaitOne(milliseconds);
                    TryFinish();
                    if (!_finished && Exception == null)
                        Exception = new TimeoutException("Hadal trench async payload write did not finish before disposal.");
                }

                Dispose();
            }

            public void MarkCommitted()
            {
                _committed = true;
            }

            public void Dispose()
            {
                if (_disposed)
                    return;

                _disposed = true;
                DisposeStream();
                if (!_committed && !string.IsNullOrEmpty(TempPath) && File.Exists(TempPath))
                {
                    try
                    {
                        File.Delete(TempPath);
                    }
                    catch (Exception ex)
                    {
                        if (Exception == null)
                            Exception = ex;
                    }
                }
            }

            private void DisposeStream()
            {
                if (_stream == null)
                    return;

                try
                {
                    _stream.Dispose();
                }
                catch (Exception ex)
                {
                    if (Exception == null)
                        Exception = ex;
                }

                _stream = null;
            }
        }

        private static void WriteHeader(BinaryWriter writer, in HadalTrenchChunkHeaderDTO header)
        {
            writer.Write(header.Magic);
            writer.Write(header.Version);
            writer.Write(header.Flags);
            writer.Write(header.Resolution.x);
            writer.Write(header.Resolution.y);
            writer.Write(header.Resolution.z);
            writer.Write(header.SectorOriginAUP.x);
            writer.Write(header.SectorOriginAUP.y);
            writer.Write(header.SectorOriginAUP.z);
            writer.Write(header.VoxelSizeMeters);
            writer.Write(header.CompressionMode);
            writer.Write(header.CompressedBytes);
            writer.Write(header.RleRunCount);
            writer.Write(header.VentCount);
            writer.Write(header.AdaptiveBlockCount);
            writer.Write(header.MaxDepthMeters);
            writer.Write(header.ExcavatedCubicMeters);
            writer.Write(header.DensityPayloadOffset);
            writer.Write(header.VentPayloadOffset);
            writer.Write(header.AdaptivePayloadOffset);
            writer.Write(header.PayloadHash);
            writer.Write(header.HeaderBytes);
            writer.Write(header.EndianMarker);
            writer.Write(header.UncompressedBytes);
            writer.Write(header.DensityPreludeBytes);
            writer.Write(header.TotalFileBytes);
            writer.Write(header.SectionAlignmentBytes);
            writer.Write(header.ChecksumType);
            writer.Write(header.SchemaHash);
            writer.Write(header._pad0);
        }

        private static ulong HashPayload(byte[] densityPayload, byte[] ventPayload, byte[] adaptivePayload)
        {
            ulong hash = 1469598103934665603ul;
            hash = HashBytes(densityPayload, hash);
            hash = HashBytes(ventPayload, hash);
            hash = HashBytes(adaptivePayload, hash);
            return hash == 0ul ? 1ul : hash;
        }

        private static ulong HashBytes(byte[] bytes, ulong hash)
        {
            for (int i = 0; i < bytes.Length; i++)
            {
                hash ^= bytes[i];
                hash *= 1099511628211ul;
            }

            return hash;
        }
    }

    internal static class HadalTrenchLz4BlockCodec
    {
        private const int MinMatch = 4;
        private const int HashBits = 16;
        private const int HashSize = 1 << HashBits;
        private const int MaxOffset = 65535;

        public static byte[] Compress(byte[] input)
        {
            if (input == null || input.Length < MinMatch + 12)
                return input ?? Array.Empty<byte>();

            NativeArray<int> table = default;
            try
            {
                table = new NativeArray<int>(HashSize, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
                for (int i = 0; i < table.Length; i++)
                    table[i] = -1;

                byte[] output = new byte[input.Length + input.Length / 255 + 16];
                int anchor = 0;
                int ip = 0;
                int op = 0;
                int matchLimit = input.Length - MinMatch;

                while (ip <= matchLimit)
                {
                    int hash = HashSequence(input, ip);
                    int reference = table[hash];
                    table[hash] = ip;
                    if (reference < 0 || ip - reference > MaxOffset || !SequenceEqual4(input, reference, ip))
                    {
                        ip++;
                        continue;
                    }

                    int literalLength = ip - anchor;
                    int tokenIndex = op++;
                    if (op >= output.Length)
                        return input;

                    byte token = 0;
                    if (literalLength >= 15)
                    {
                        token = 15 << 4;
                        op = WriteLength(output, op, literalLength - 15);
                    }
                    else
                    {
                        token = (byte)(literalLength << 4);
                    }

                    if (op + literalLength + 2 >= output.Length)
                        return input;

                    Buffer.BlockCopy(input, anchor, output, op, literalLength);
                    op += literalLength;
                    int offset = ip - reference;
                    output[op++] = (byte)offset;
                    output[op++] = (byte)(offset >> 8);
                    ip += MinMatch;
                    reference += MinMatch;
                    int matchLength = 0;
                    while (ip < input.Length && input[ip] == input[reference])
                    {
                        ip++;
                        reference++;
                        matchLength++;
                    }

                    if (matchLength >= 15)
                    {
                        token |= 15;
                        op = WriteLength(output, op, matchLength - 15);
                    }
                    else
                    {
                        token |= (byte)matchLength;
                    }

                    output[tokenIndex] = token;
                    anchor = ip;
                }

                int lastLiterals = input.Length - anchor;
                if (op + lastLiterals + 16 >= output.Length)
                    return input;

                int lastTokenIndex = op++;
                if (lastLiterals >= 15)
                {
                    output[lastTokenIndex] = 15 << 4;
                    op = WriteLength(output, op, lastLiterals - 15);
                }
                else
                {
                    output[lastTokenIndex] = (byte)(lastLiterals << 4);
                }

                Buffer.BlockCopy(input, anchor, output, op, lastLiterals);
                op += lastLiterals;
                byte[] compact = new byte[op];
                Buffer.BlockCopy(output, 0, compact, 0, op);
                return compact;
            }
            finally
            {
                if (table.IsCreated)
                    table.Dispose();
            }
        }

        private static int WriteLength(byte[] output, int op, int length)
        {
            int remaining = length;
            while (remaining >= 255)
            {
                if (op >= output.Length)
                    return output.Length;
                output[op++] = 255;
                remaining -= 255;
            }

            if (op < output.Length)
                output[op++] = (byte)remaining;
            return op;
        }

        private static int HashSequence(byte[] input, int index)
        {
            uint value = (uint)(input[index] | (input[index + 1] << 8) | (input[index + 2] << 16) | (input[index + 3] << 24));
            return (int)((value * 2654435761u) >> (32 - HashBits));
        }

        private static bool SequenceEqual4(byte[] input, int a, int b)
        {
            return input[a] == input[b] &&
                   input[a + 1] == input[b + 1] &&
                   input[a + 2] == input[b + 2] &&
                   input[a + 3] == input[b + 3];
        }
    }
}
