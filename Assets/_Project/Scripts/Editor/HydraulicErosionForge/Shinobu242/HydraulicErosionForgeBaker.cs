#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using Hecton8.Core;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Hecton8.Editor.HydraulicErosionForge
{
    internal static class HydraulicErosionForgeBaker
    {
        private const string NativeMemoryOwner = "SHINOBU_242_HydraulicErosionForge";
        private const string HeightsLabel = "Heights";
        private const string SiltLabel = "SiltMask";
        private const string MacroLabel = "MacroErosion";
        private const string DropletsLabel = "Droplets";
        private const string MetricsLabel = "Metrics";
        private const string ScanMetricsLabel = "ScanMetrics";
        private const string PixelsLabel = "PreviewPixels";
        private const string TelemetryLabel = "BlackBoxTelemetry";
        private const string TelemetryCursorLabel = "BlackBoxTelemetryCursor";
        private const string PreviewNorthQueueLabel = "Preview.NorthTransferQueue";
        private const string PreviewSouthQueueLabel = "Preview.SouthTransferQueue";
        private const string PreviewEastQueueLabel = "Preview.EastTransferQueue";
        private const string PreviewWestQueueLabel = "Preview.WestTransferQueue";
        private const string BakeNorthQueueLabel = "Bake.NorthTransferQueue";
        private const string BakeSouthQueueLabel = "Bake.SouthTransferQueue";
        private const string BakeEastQueueLabel = "Bake.EastTransferQueue";
        private const string BakeWestQueueLabel = "Bake.WestTransferQueue";
        private const string SeamScratchLabel = "SeamTransferScratch";
        private static readonly Stopwatch _Stopwatch = new Stopwatch();
        private static bool _bakeRunning;

        private struct SeamTransferCaptureDTO
        {
            public int NorthOffset;
            public int NorthCount;
            public int SouthOffset;
            public int SouthCount;
            public int EastOffset;
            public int EastCount;
            public int WestOffset;
            public int WestCount;
            public uint WarningFlags;
        }

        [MenuItem("HECTON-8/Hydraulic Erosion Forge/Bake Mock Sector", false, 190)]
        public static void BakeMockSectorMenu()
        {
            if (!StartMockSectorBake(DefaultSettings(), null))
                Debug.LogWarning("[SHINOBU_242] Hydraulic erosion bake ignored: bake already running.");
        }

        public static bool IsBusy => _bakeRunning;

        public static bool StartMockSectorBake(HydraulicErosionSettingsDTO settings, Action<float> progress)
        {
            if (IsBusy)
                return false;

            _bakeRunning = true;
            _ = RunMockSectorBakeAsync(settings, progress);
            return true;
        }

        public static HydraulicErosionSettingsDTO DefaultSettings()
        {
            WeatheringProfileDTO profile = HydraulicErosionWeatheringCsv.DefaultProfile();
            return BuildSettingsFromProfile(profile, HydraulicErosionForgeConstants.MockResolution, HydraulicErosionForgeConstants.DefaultDropletCount, double3.zero, 0, 0, 0.75f);
        }

        public static HydraulicErosionSettingsDTO BuildSettingsFromProfile(
            in WeatheringProfileDTO profile,
            int resolution,
            int droplets,
            double3 sectorAup,
            int sectorX,
            int sectorZ,
            float quality)
        {
            HydraulicErosionSettingsDTO settings = default;
            settings.SectorAup = sectorAup;
            settings.CellSizeMeters = HydraulicErosionForgeConstants.DefaultSectorSizeMeters / (double)math.max(1, resolution - 1);
            settings.Width = math.max(8, resolution);
            settings.Height = math.max(8, resolution);
            settings.SectorX = sectorX;
            settings.SectorZ = sectorZ;
            settings.DropletCount = math.max(0, droplets);
            settings.MaxLifetime = HydraulicErosionForgeConstants.MaxDropletLifetime;
            settings.WorldSeed = 0x48594552u ^ profile.SeedSalt;
            float qualityWeight = math.saturate(quality);
            float qualityCurve = qualityWeight * qualityWeight * (3f - 2f * qualityWeight);
            settings.Inertia = math.lerp(0.58f, 0.78f, qualityCurve);
            settings.CapacityFactor = math.max(0.001f, profile.SedimentCapacity * math.lerp(0.7f, 1.22f, qualityCurve));
            settings.MinSedimentCapacity = 0.0001f;
            settings.ErosionRate = math.saturate(profile.ErosionAggressiveness * math.lerp(0.72f, 1.18f, qualityCurve));
            settings.DepositRate = math.lerp(0.24f, 0.15f, qualityCurve);
            settings.EvaporationRate = math.saturate(profile.EvaporationSpeed * math.lerp(1.28f, 0.72f, qualityCurve));
            settings.Gravity = math.lerp(3.1f, 5.4f, qualityCurve);
            settings.InitialWater = math.max(0.001f, profile.RainRate * math.lerp(0.78f, 1.18f, qualityCurve));
            settings.InitialVelocity = 1f;
            settings.MinWater = 0.01f;
            settings.HeightScaleMeters = 180f;
            settings.SiltMaskGain = math.lerp(2.4f, 6.2f, qualityCurve);
            settings.GlobalQualityWeight = qualityWeight;
            settings.Flags = HydraulicErosionForgeConstants.PayloadFlagRollbackExcluded;
            return settings;
        }

        public static void LoadWeatheringProfiles(List<WeatheringProfileDTO> profiles)
        {
            HydraulicErosionWeatheringCsv.LoadProfiles(profiles);
        }

        public static Texture2D BuildPreviewTexture(HydraulicErosionSettingsDTO settings)
        {
            int resolution = math.clamp(settings.Width, 16, HydraulicErosionForgeConstants.PreviewResolution);
            settings.Width = resolution;
            settings.Height = resolution;
            settings.DropletCount = math.min(settings.DropletCount, HydraulicErosionForgeConstants.PreviewDropletCount);
            int count = resolution * resolution;
            NativeArray<float> heights = default;
            NativeArray<float> silt = default;
            NativeArray<ErosionDropletDTO> droplets = default;
            NativeArray<float> metrics = default;
            NativeArray<uint> pixels = default;
            NativeArray<ErosionBakeTelemetryEntry> telemetry = default;
            NativeArray<int> telemetryCursor = default;
            NativeQueue<ErosionDropletDTO> north = default;
            NativeQueue<ErosionDropletDTO> south = default;
            NativeQueue<ErosionDropletDTO> east = default;
            NativeQueue<ErosionDropletDTO> west = default;
            try
            {
                heights = NewTrackedArray<float>(count, Allocator.TempJob, NativeArrayOptions.UninitializedMemory, HeightsLabel, NativeAllocationLifetime.TempJob);
                silt = NewTrackedArray<float>(count, Allocator.TempJob, NativeArrayOptions.UninitializedMemory, SiltLabel, NativeAllocationLifetime.TempJob);
                droplets = NewTrackedArray<ErosionDropletDTO>(settings.DropletCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory, DropletsLabel, NativeAllocationLifetime.TempJob);
                metrics = NewTrackedArray<float>(4, Allocator.TempJob, NativeArrayOptions.UninitializedMemory, MetricsLabel, NativeAllocationLifetime.TempJob);
                pixels = NewTrackedArray<uint>(count, Allocator.TempJob, NativeArrayOptions.UninitializedMemory, PixelsLabel, NativeAllocationLifetime.TempJob);
                telemetry = NewTrackedArray<ErosionBakeTelemetryEntry>(HydraulicErosionForgeConstants.BlackBoxFrameCount, Allocator.TempJob, NativeArrayOptions.ClearMemory, TelemetryLabel, NativeAllocationLifetime.TempJob);
                telemetryCursor = NewTrackedArray<int>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory, TelemetryCursorLabel, NativeAllocationLifetime.TempJob);
                north = NewTrackedQueue<ErosionDropletDTO>(math.max(1, settings.DropletCount >> 2), PreviewNorthQueueLabel, NativeAllocationLifetime.TempJob);
                south = NewTrackedQueue<ErosionDropletDTO>(math.max(1, settings.DropletCount >> 2), PreviewSouthQueueLabel, NativeAllocationLifetime.TempJob);
                east = NewTrackedQueue<ErosionDropletDTO>(math.max(1, settings.DropletCount >> 2), PreviewEastQueueLabel, NativeAllocationLifetime.TempJob);
                west = NewTrackedQueue<ErosionDropletDTO>(math.max(1, settings.DropletCount >> 2), PreviewWestQueueLabel, NativeAllocationLifetime.TempJob);

                JobHandle handle = ScheduleCore(settings, heights, silt, droplets, metrics, telemetry, telemetryCursor, north, south, east, west);
                handle = new ErosionPreviewRgbaJob
                {
                    Heights = heights,
                    Silt = silt,
                    Rgba = pixels,
                    Width = resolution,
                    Height = resolution
                }.Schedule(count, 64, handle);
                // COLD SYNC JOB: editor preview needs Texture2D data immediately; no runtime frame path.
                handle.Complete();

                Texture2D texture = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false, true);
                texture.SetPixelData(pixels, 0);
                texture.Apply(false, false);
                return texture;
            }
            finally
            {
                DisposeTrackedQueue(ref west, PreviewWestQueueLabel);
                DisposeTrackedQueue(ref east, PreviewEastQueueLabel);
                DisposeTrackedQueue(ref south, PreviewSouthQueueLabel);
                DisposeTrackedQueue(ref north, PreviewNorthQueueLabel);
                DisposeTrackedArray(ref telemetryCursor);
                DisposeTrackedArray(ref telemetry);
                DisposeTrackedArray(ref pixels);
                DisposeTrackedArray(ref metrics);
                DisposeTrackedArray(ref droplets);
                DisposeTrackedArray(ref silt);
                DisposeTrackedArray(ref heights);
            }
        }

        private static async Awaitable RunMockSectorBakeAsync(HydraulicErosionSettingsDTO settings, Action<float> progress)
        {
            try
            {
                await RunMockSectorBakeInternalAsync(settings, progress);
            }
            catch (Exception ex)
            {
                try
                {
                    await Awaitable.MainThreadAsync();
                }
                catch
                {
                }

                Debug.LogException(ex);
            }
            finally
            {
                _bakeRunning = false;
            }
        }

        private static async Awaitable RunMockSectorBakeInternalAsync(HydraulicErosionSettingsDTO settings, Action<float> progress)
        {
            progress?.Invoke(0f);
            EnsureFolder(HydraulicErosionForgeConstants.OutputFolder);
            int width = math.max(8, settings.Width);
            int height = math.max(8, settings.Height);
            int count = width * height;
            int macroCount = HydraulicErosionForgeConstants.MacroResolution * HydraulicErosionForgeConstants.MacroResolution;
            NativeArray<float> heights = default;
            NativeArray<float> silt = default;
            NativeArray<float> macro = default;
            NativeArray<ErosionDropletDTO> droplets = default;
            NativeArray<float> simMetrics = default;
            NativeArray<float> scanMetrics = default;
            NativeArray<ErosionBakeTelemetryEntry> telemetry = default;
            NativeArray<int> telemetryCursor = default;
            NativeArray<ErosionDropletDTO> seamScratch = default;
            NativeQueue<ErosionDropletDTO> north = default;
            NativeQueue<ErosionDropletDTO> south = default;
            NativeQueue<ErosionDropletDTO> east = default;
            NativeQueue<ErosionDropletDTO> west = default;
            ErosionBakeMetrics metrics = default;
            SeamTransferCaptureDTO seamCapture = default;

            try
            {
                heights = NewTrackedArray<float>(count, Allocator.Persistent, NativeArrayOptions.UninitializedMemory, HeightsLabel, NativeAllocationLifetime.Session);
                silt = NewTrackedArray<float>(count, Allocator.Persistent, NativeArrayOptions.UninitializedMemory, SiltLabel, NativeAllocationLifetime.Session);
                macro = NewTrackedArray<float>(macroCount, Allocator.Persistent, NativeArrayOptions.UninitializedMemory, MacroLabel, NativeAllocationLifetime.Session);
                droplets = NewTrackedArray<ErosionDropletDTO>(settings.DropletCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory, DropletsLabel, NativeAllocationLifetime.TempJob);
                simMetrics = NewTrackedArray<float>(2, Allocator.TempJob, NativeArrayOptions.UninitializedMemory, MetricsLabel, NativeAllocationLifetime.TempJob);
                scanMetrics = NewTrackedArray<float>(4, Allocator.TempJob, NativeArrayOptions.UninitializedMemory, ScanMetricsLabel, NativeAllocationLifetime.TempJob);
                telemetry = NewTrackedArray<ErosionBakeTelemetryEntry>(HydraulicErosionForgeConstants.BlackBoxFrameCount, Allocator.Persistent, NativeArrayOptions.ClearMemory, TelemetryLabel, NativeAllocationLifetime.Session);
                telemetryCursor = NewTrackedArray<int>(1, Allocator.Persistent, NativeArrayOptions.ClearMemory, TelemetryCursorLabel, NativeAllocationLifetime.Session);
                int transferSoftCapacity = math.max(1, settings.DropletCount >> 2);
                north = NewTrackedQueue<ErosionDropletDTO>(transferSoftCapacity, BakeNorthQueueLabel, NativeAllocationLifetime.TempJob);
                south = NewTrackedQueue<ErosionDropletDTO>(transferSoftCapacity, BakeSouthQueueLabel, NativeAllocationLifetime.TempJob);
                east = NewTrackedQueue<ErosionDropletDTO>(transferSoftCapacity, BakeEastQueueLabel, NativeAllocationLifetime.TempJob);
                west = NewTrackedQueue<ErosionDropletDTO>(transferSoftCapacity, BakeWestQueueLabel, NativeAllocationLifetime.TempJob);

                _Stopwatch.Restart();
                JobHandle handle = ScheduleCore(settings, heights, silt, droplets, simMetrics, telemetry, telemetryCursor, north, south, east, west);
                handle = new ErosionMetricScanJob
                {
                    Heights = heights,
                    Silt = silt,
                    Metrics = scanMetrics
                }.Schedule(handle);
                // COLD SYNC JOB: editor bake must finish native mutation before async file IO reads payload pointers.
                handle.Complete();
                _Stopwatch.Stop();
                metrics.MockHeightmapMilliseconds = 0d;
                metrics.DropletMilliseconds = _Stopwatch.Elapsed.TotalMilliseconds;
                metrics.DropletsSimulated = math.max(0, settings.DropletCount);
                metrics.MaxDepthCarved = simMetrics[0];
                metrics.TotalSedimentTransported = simMetrics[1] + scanMetrics[2];
                metrics.NaNSectors = scanMetrics[3] > 0f ? 1 : 0;
                metrics.WarningFlags = metrics.NaNSectors > 0 ? HydraulicErosionForgeConstants.WarningNonFiniteHeight : 0u;
                progress?.Invoke(0.58f);

                _Stopwatch.Restart();
                JobHandle macroHandle = new GenerateMacroErosionMapJob
                {
                    Source = heights,
                    Macro = macro,
                    SourceWidth = width,
                    SourceHeight = height,
                    MacroWidth = HydraulicErosionForgeConstants.MacroResolution,
                    MacroHeight = HydraulicErosionForgeConstants.MacroResolution
                }.Schedule(macroCount, 64);
                // COLD SYNC JOB: macro data is an editor bake artifact and serialization needs stable completed payloads.
                macroHandle.Complete();
                _Stopwatch.Stop();
                metrics.MacroMilliseconds = _Stopwatch.Elapsed.TotalMilliseconds;
                progress?.Invoke(0.68f);

                if (metrics.NaNSectors > 0)
                    unsafe
                    {
                        TryDumpBlackBox(telemetry, telemetryCursor.IsCreated ? telemetryCursor[0] : 0, HydraulicErosionForgeConstants.DumpReasonNaN);
                    }

                JobHandle sanitizeHandle = new SanitizeFloatPayloadJob { Payload = heights }.Schedule(count, 128);
                sanitizeHandle = new SanitizeFloatPayloadJob { Payload = silt }.Schedule(count, 128, sanitizeHandle);
                sanitizeHandle = new SanitizeFloatPayloadJob { Payload = macro }.Schedule(macroCount, 128, sanitizeHandle);
                // COLD SYNC JOB: header checksums and raw payload bytes must describe the same finite terrain truth.
                sanitizeHandle.Complete();

                seamScratch = NewTrackedArray<ErosionDropletDTO>(math.max(1, settings.DropletCount), Allocator.Persistent, NativeArrayOptions.UninitializedMemory, SeamScratchLabel, NativeAllocationLifetime.Session);
                seamCapture = CaptureSeamTransfers(seamScratch, north, south, east, west, transferSoftCapacity);
                metrics.WarningFlags |= seamCapture.WarningFlags;
                metrics.SeamNorthTransfers = seamCapture.NorthCount;
                metrics.SeamSouthTransfers = seamCapture.SouthCount;
                metrics.SeamEastTransfers = seamCapture.EastCount;
                metrics.SeamWestTransfers = seamCapture.WestCount;

                DisposeTrackedQueue(ref west, BakeWestQueueLabel);
                DisposeTrackedQueue(ref east, BakeEastQueueLabel);
                DisposeTrackedQueue(ref south, BakeSouthQueueLabel);
                DisposeTrackedQueue(ref north, BakeNorthQueueLabel);
                DisposeTrackedArray(ref scanMetrics);
                DisposeTrackedArray(ref simMetrics);
                DisposeTrackedArray(ref droplets);

                _Stopwatch.Restart();
                string stem = "sector_" + settings.SectorX.ToString("D4") + "_" + settings.SectorZ.ToString("D4");
                await WriteSeamTransfersAsync(settings, seamScratch, seamCapture, metrics);
                await WritePayloadAsync(Path.Combine(HydraulicErosionForgeConstants.OutputFolder, stem + "_height.h8bin"), heights, settings, HydraulicErosionForgeConstants.PayloadKindHeight, metrics);
                await WritePayloadAsync(Path.Combine(HydraulicErosionForgeConstants.OutputFolder, stem + "_silt.h8bin"), silt, settings, HydraulicErosionForgeConstants.PayloadKindSilt, metrics);
                await WritePayloadAsync(HydraulicErosionForgeConstants.MacroOutputPath, macro, settings, HydraulicErosionForgeConstants.PayloadKindMacro, metrics);
                _Stopwatch.Stop();
                metrics.SerializationMilliseconds = _Stopwatch.Elapsed.TotalMilliseconds;
                metrics.SectorCount = 1;
                metrics.CompletedSectors = 1;
                progress?.Invoke(0.9f);

                Terrain_Runtime_Scanner_Erosion.ScanAndWriteReport(out int scannerHits);
                metrics.RuntimeScannerHits = scannerHits;
                WriteBakeReport(metrics);
                progress?.Invoke(1f);
                AssetDatabase.Refresh();
            }
            catch
            {
                if (telemetry.IsCreated)
                    unsafe
                    {
                        TryDumpBlackBox(telemetry, telemetryCursor.IsCreated ? telemetryCursor[0] : 0, HydraulicErosionForgeConstants.DumpReasonException);
                    }
                throw;
            }
            finally
            {
                DisposeTrackedQueue(ref west, BakeWestQueueLabel);
                DisposeTrackedQueue(ref east, BakeEastQueueLabel);
                DisposeTrackedQueue(ref south, BakeSouthQueueLabel);
                DisposeTrackedQueue(ref north, BakeNorthQueueLabel);
                DisposeTrackedArray(ref seamScratch);
                DisposeTrackedArray(ref telemetryCursor);
                DisposeTrackedArray(ref telemetry);
                DisposeTrackedArray(ref scanMetrics);
                DisposeTrackedArray(ref simMetrics);
                DisposeTrackedArray(ref droplets);
                DisposeTrackedArray(ref macro);
                DisposeTrackedArray(ref silt);
                DisposeTrackedArray(ref heights);
            }
        }

        private static JobHandle ScheduleCore(
            HydraulicErosionSettingsDTO settings,
            NativeArray<float> heights,
            NativeArray<float> silt,
            NativeArray<ErosionDropletDTO> droplets,
            NativeArray<float> metrics,
            NativeArray<ErosionBakeTelemetryEntry> telemetry,
            NativeArray<int> telemetryCursor,
            NativeQueue<ErosionDropletDTO> north,
            NativeQueue<ErosionDropletDTO> south,
            NativeQueue<ErosionDropletDTO> east,
            NativeQueue<ErosionDropletDTO> west)
        {
            int count = settings.Width * settings.Height;
            JobHandle handle = new GenerateMockHeightmapJob
            {
                Heights = heights,
                SiltMask = silt,
                Width = settings.Width,
                Height = settings.Height,
                ConeHeight01 = 0.92f,
                BasinDepth01 = 0.22f
            }.Schedule(count, 64);

            int dropletInitCount = math.clamp(settings.DropletCount, 0, droplets.IsCreated ? droplets.Length : 0);
            if (dropletInitCount > 0)
            {
                handle = new InitializeErosionDropletsJob
                {
                    Droplets = droplets,
                    Settings = settings,
                    SeedSalt = 0x242242u
                }.Schedule(dropletInitCount, 64, handle);
            }

            handle = new SimulateHydraulicErosionJob
            {
                Heightmap = heights,
                SiltMask = silt,
                Droplets = droplets,
                NorthTransfers = north,
                SouthTransfers = south,
                EastTransfers = east,
                WestTransfers = west,
                Telemetry = telemetry,
                TelemetryCursor = telemetryCursor,
                Metrics = metrics,
                Settings = settings
            }.Schedule(handle);

            return handle;
        }

        private static async Awaitable WritePayloadAsync(
            string relativePath,
            NativeArray<float> payload,
            HydraulicErosionSettingsDTO settings,
            uint payloadKind,
            ErosionBakeMetrics metrics)
        {
            EnsureFileFolder(relativePath);
            string tempPath = relativePath + ".tmp";
            DeleteIfExists(tempPath);
            ErosionHeightmapFileHeaderDTO header = BuildHeader(payload, settings, payloadKind, metrics);
            Exception failure = null;
            try
            {
                await Awaitable.BackgroundThreadAsync();
                unsafe
                {
                    WritePayloadBlocking(tempPath, payload, header);
                }
                await Awaitable.MainThreadAsync();
                ReplacePayloadFile(tempPath, relativePath, true);
            }
            catch (Exception ex)
            {
                failure = ex;
                DeleteIfExists(tempPath);
            }

            if (failure != null)
            {
                await Awaitable.MainThreadAsync();
                throw failure;
            }
        }

        private static unsafe void WritePayloadBlocking(string tempPath, NativeArray<float> payload, in ErosionHeightmapFileHeaderDTO header)
        {
            using (FileStream stream = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 131072, FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                WriteStruct(stream, header);
                byte* ptr = (byte*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(payload);
                long byteCount = (long)payload.Length * UnsafeUtility.SizeOf<float>();
                stream.Write(new ReadOnlySpan<byte>(ptr, checked((int)byteCount)));
                stream.Flush(true);
            }
        }

        private static ErosionHeightmapFileHeaderDTO BuildHeader(
            NativeArray<float> payload,
            in HydraulicErosionSettingsDTO settings,
            uint payloadKind,
            in ErosionBakeMetrics metrics)
        {
            float min = 1f;
            float max = 0f;
            uint checksum = 2166136261u;
            for (int i = 0; i < payload.Length; i++)
            {
                float value = payload[i];
                if (!math.isfinite(value))
                    value = 0f;
                min = math.min(min, value);
                max = math.max(max, value);
                checksum ^= math.asuint(value);
                checksum *= 16777619u;
            }

            return new ErosionHeightmapFileHeaderDTO
            {
                Magic = HydraulicErosionForgeConstants.HeightmapMagic,
                Version = HydraulicErosionForgeConstants.HeightmapVersion,
                HeaderBytes = HydraulicErosionForgeConstants.HeightmapHeaderBytes,
                PayloadKind = payloadKind,
                Flags = HydraulicErosionForgeConstants.PayloadFlagRollbackExcluded,
                Width = payloadKind == HydraulicErosionForgeConstants.PayloadKindMacro ? HydraulicErosionForgeConstants.MacroResolution : settings.Width,
                Height = payloadKind == HydraulicErosionForgeConstants.PayloadKindMacro ? HydraulicErosionForgeConstants.MacroResolution : settings.Height,
                SectorX = settings.SectorX,
                SectorZ = settings.SectorZ,
                SectorAup = ErosionDeterminismHash.QuantizeAupToMillimeters(settings.SectorAup),
                CellSizeMeters = settings.CellSizeMeters,
                MinValue = min,
                MaxValue = max,
                WorldSeed = settings.WorldSeed,
                DataChecksum = checksum,
                PayloadBytes = checked((uint)((long)payload.Length * UnsafeUtility.SizeOf<float>())),
                ElementStrideBytes = (uint)UnsafeUtility.SizeOf<float>(),
                DropletCount = (uint)math.max(0, settings.DropletCount),
                MaxLifetime = (uint)math.max(0, settings.MaxLifetime),
                GlobalQualityWeight = settings.GlobalQualityWeight,
                MaxCarvedDepth = metrics.MaxDepthCarved,
                SedimentTransported = metrics.TotalSedimentTransported,
                WarningFlags = metrics.WarningFlags,
                EndianMarker = HydraulicErosionForgeConstants.LittleEndianMarker
            };
        }

        private static unsafe void WriteStruct<T>(FileStream stream, in T value) where T : unmanaged
        {
            int size = UnsafeUtility.SizeOf<T>();
            byte* buffer = stackalloc byte[size];
            UnsafeUtility.WriteArrayElement(buffer, 0, value);
            stream.Write(new ReadOnlySpan<byte>(buffer, size));
        }

        private static unsafe void TryDumpBlackBox(NativeArray<ErosionBakeTelemetryEntry> telemetry, int cursor, uint reason)
        {
            try
            {
                EnsureFileFolder(HydraulicErosionForgeConstants.DumpPath);
                using (FileStream stream = new FileStream(HydraulicErosionForgeConstants.DumpPath, FileMode.Create, FileAccess.Write, FileShare.Read))
                {
                    ErosionBakeDumpHeader header = new ErosionBakeDumpHeader
                    {
                        Magic = HydraulicErosionForgeConstants.DumpMagic,
                        EntryCount = (uint)(telemetry.IsCreated ? telemetry.Length : 0),
                        EntrySize = (uint)UnsafeUtility.SizeOf<ErosionBakeTelemetryEntry>(),
                        Cursor = (uint)math.max(0, cursor),
                        Reason = reason
                    };
                    WriteStruct(stream, header);
                    if (telemetry.IsCreated)
                    {
                        byte* ptr = (byte*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(telemetry);
                        stream.Write(new ReadOnlySpan<byte>(ptr, telemetry.Length * UnsafeUtility.SizeOf<ErosionBakeTelemetryEntry>()));
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
        }

        private static void WriteBakeReport(in ErosionBakeMetrics metrics)
        {
            EnsureFileFolder(HydraulicErosionForgeConstants.BakeReportPath);
            StringBuilder builder = new StringBuilder(2048);
            builder.Append("{\n  \"agent\": \"SHINOBU_242\",\n");
            builder.Append("  \"status\": \"PENDING_VERIFICATION\",\n");
            builder.Append("  \"totalDropletsSimulated\": ").Append(metrics.DropletsSimulated).Append(",\n");
            builder.Append("  \"maximumDepthCarved\": ").Append(metrics.MaxDepthCarved.ToString("0.######", System.Globalization.CultureInfo.InvariantCulture)).Append(",\n");
            builder.Append("  \"totalSedimentTransported\": ").Append(metrics.TotalSedimentTransported.ToString("0.######", System.Globalization.CultureInfo.InvariantCulture)).Append(",\n");
            builder.Append("  \"burstExtractionMilliseconds\": ").Append(metrics.DropletMilliseconds.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)).Append(",\n");
            builder.Append("  \"macroMilliseconds\": ").Append(metrics.MacroMilliseconds.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)).Append(",\n");
            builder.Append("  \"serializationMilliseconds\": ").Append(metrics.SerializationMilliseconds.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)).Append(",\n");
            builder.Append("  \"runtimeScannerHits\": ").Append(metrics.RuntimeScannerHits).Append(",\n");
            builder.Append("  \"seamNorthTransfers\": ").Append(metrics.SeamNorthTransfers).Append(",\n");
            builder.Append("  \"seamSouthTransfers\": ").Append(metrics.SeamSouthTransfers).Append(",\n");
            builder.Append("  \"seamEastTransfers\": ").Append(metrics.SeamEastTransfers).Append(",\n");
            builder.Append("  \"seamWestTransfers\": ").Append(metrics.SeamWestTransfers).Append(",\n");
            builder.Append("  \"warningFlags\": ").Append(metrics.WarningFlags).Append(",\n");
            builder.Append("  \"warning\": \"").Append(metrics.NaNSectors > 0 ? "CRITICAL_WARNING" : "NONE").Append("\"\n");
            builder.Append("}\n");
            WriteAtomicText(HydraulicErosionForgeConstants.BakeReportPath, builder.ToString(), true);
        }

        private static void WriteAtomicText(string path, string contents, bool keepBackup)
        {
            EnsureFileFolder(path);
            string tempPath = path + ".tmp";
            DeleteIfExists(tempPath);
            try
            {
                File.WriteAllText(tempPath, contents);
                ReplacePayloadFile(tempPath, path, keepBackup);
            }
            catch
            {
                DeleteIfExists(tempPath);
                throw;
            }
        }

        private static void ReplacePayloadFile(string tempPath, string finalPath, bool keepBackup)
        {
            if (File.Exists(finalPath))
            {
                string backup = keepBackup ? finalPath + ".bak" : null;
                if (backup != null)
                    DeleteIfExists(backup);
                File.Replace(tempPath, finalPath, backup);
                return;
            }

            File.Move(tempPath, finalPath);
        }

        private static SeamTransferCaptureDTO CaptureSeamTransfers(
            NativeArray<ErosionDropletDTO> scratch,
            NativeQueue<ErosionDropletDTO> north,
            NativeQueue<ErosionDropletDTO> south,
            NativeQueue<ErosionDropletDTO> east,
            NativeQueue<ErosionDropletDTO> west,
            int transferSoftCapacity)
        {
            SeamTransferCaptureDTO capture = default;
            int cursor = 0;
            capture.NorthOffset = cursor;
            capture.NorthCount = CaptureOneSeamTransfer(north, scratch, ref cursor, transferSoftCapacity, ref capture.WarningFlags);
            capture.SouthOffset = cursor;
            capture.SouthCount = CaptureOneSeamTransfer(south, scratch, ref cursor, transferSoftCapacity, ref capture.WarningFlags);
            capture.EastOffset = cursor;
            capture.EastCount = CaptureOneSeamTransfer(east, scratch, ref cursor, transferSoftCapacity, ref capture.WarningFlags);
            capture.WestOffset = cursor;
            capture.WestCount = CaptureOneSeamTransfer(west, scratch, ref cursor, transferSoftCapacity, ref capture.WarningFlags);
            return capture;
        }

        private static int CaptureOneSeamTransfer(
            NativeQueue<ErosionDropletDTO> queue,
            NativeArray<ErosionDropletDTO> scratch,
            ref int cursor,
            int transferSoftCapacity,
            ref uint warningFlags)
        {
            int remaining = math.max(0, scratch.Length - cursor);
            int count = HydraulicErosionChunkTransferBridge.ConsumeIncomingQueue(queue, scratch, cursor, remaining);
            cursor += count;
            if (count > math.max(1, transferSoftCapacity))
                warningFlags |= HydraulicErosionForgeConstants.WarningQueueOverflow;

            ErosionDropletDTO overflow;
            if (queue.IsCreated && queue.TryDequeue(out overflow))
            {
                warningFlags |= HydraulicErosionForgeConstants.WarningQueueOverflow;
                while (queue.TryDequeue(out overflow))
                {
                }
            }

            return count;
        }

        private static async Awaitable WriteSeamTransfersAsync(
            HydraulicErosionSettingsDTO settings,
            NativeArray<ErosionDropletDTO> scratch,
            SeamTransferCaptureDTO capture,
            ErosionBakeMetrics metrics)
        {
            string stem = "sector_" + settings.SectorX.ToString("D4") + "_" + settings.SectorZ.ToString("D4");
            await WriteOneSeamTransferAsync(Path.Combine(HydraulicErosionForgeConstants.OutputFolder, stem + "_north.h8seam"), settings, scratch, capture.NorthOffset, capture.NorthCount, 0, 1, metrics);
            await WriteOneSeamTransferAsync(Path.Combine(HydraulicErosionForgeConstants.OutputFolder, stem + "_south.h8seam"), settings, scratch, capture.SouthOffset, capture.SouthCount, 0, -1, metrics);
            await WriteOneSeamTransferAsync(Path.Combine(HydraulicErosionForgeConstants.OutputFolder, stem + "_east.h8seam"), settings, scratch, capture.EastOffset, capture.EastCount, 1, 0, metrics);
            await WriteOneSeamTransferAsync(Path.Combine(HydraulicErosionForgeConstants.OutputFolder, stem + "_west.h8seam"), settings, scratch, capture.WestOffset, capture.WestCount, -1, 0, metrics);
        }

        private static async Awaitable WriteOneSeamTransferAsync(
            string relativePath,
            HydraulicErosionSettingsDTO settings,
            NativeArray<ErosionDropletDTO> scratch,
            int offset,
            int count,
            int directionX,
            int directionZ,
            ErosionBakeMetrics metrics)
        {
            int safeOffset = math.clamp(offset, 0, scratch.Length);
            int safeCount = math.clamp(count, 0, scratch.Length - safeOffset);

            EnsureFileFolder(relativePath);
            string tempPath = relativePath + ".tmp";
            DeleteIfExists(tempPath);
            ErosionSeamTransferFileHeaderDTO header;
            unsafe
            {
                header = BuildSeamHeader(scratch, safeOffset, safeCount, settings, directionX, directionZ, metrics);
            }
            Exception failure = null;
            try
            {
                await Awaitable.BackgroundThreadAsync();
                unsafe
                {
                    WriteSeamPayloadBlocking(tempPath, scratch, safeOffset, safeCount, header);
                }
                await Awaitable.MainThreadAsync();
                ReplacePayloadFile(tempPath, relativePath, true);
            }
            catch (Exception ex)
            {
                failure = ex;
                DeleteIfExists(tempPath);
            }

            if (failure != null)
            {
                await Awaitable.MainThreadAsync();
                throw failure;
            }
        }

        private static unsafe ErosionSeamTransferFileHeaderDTO BuildSeamHeader(
            NativeArray<ErosionDropletDTO> payload,
            int offset,
            int count,
            in HydraulicErosionSettingsDTO settings,
            int directionX,
            int directionZ,
            in ErosionBakeMetrics metrics)
        {
            uint checksum = 2166136261u;
            ErosionDropletDTO* dropletPtr = (ErosionDropletDTO*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(payload) + offset;
            uint* words = (uint*)dropletPtr;
            int wordCount = count * (UnsafeUtility.SizeOf<ErosionDropletDTO>() / UnsafeUtility.SizeOf<uint>());
            for (int i = 0; i < wordCount; i++)
            {
                checksum ^= words[i];
                checksum *= 16777619u;
            }

            double3 sourceAup = ErosionDeterminismHash.QuantizeAupToMillimeters(settings.SectorAup);
            double3 neighborAup = HydraulicErosionChunkTransferBridge.ResolveNeighborSectorAup(sourceAup, directionX, directionZ);
            return new ErosionSeamTransferFileHeaderDTO
            {
                Magic = HydraulicErosionForgeConstants.SeamTransferMagic,
                Version = HydraulicErosionForgeConstants.SeamTransferVersion,
                HeaderBytes = HydraulicErosionForgeConstants.SeamTransferHeaderBytes,
                Flags = HydraulicErosionForgeConstants.PayloadFlagRollbackExcluded,
                DirectionX = directionX,
                DirectionZ = directionZ,
                SourceSectorX = settings.SectorX,
                SourceSectorZ = settings.SectorZ,
                NeighborSectorX = settings.SectorX + directionX,
                NeighborSectorZ = settings.SectorZ + directionZ,
                DropletCount = (uint)math.max(0, count),
                ElementStrideBytes = (uint)UnsafeUtility.SizeOf<ErosionDropletDTO>(),
                PayloadBytes = checked((uint)((long)math.max(0, count) * UnsafeUtility.SizeOf<ErosionDropletDTO>())),
                DataChecksum = checksum,
                WarningFlags = metrics.WarningFlags,
                SourceAup = sourceAup,
                NeighborAup = neighborAup,
                MaxCarvedDepth = metrics.MaxDepthCarved,
                SedimentTransported = metrics.TotalSedimentTransported,
                GlobalQualityWeight = settings.GlobalQualityWeight,
                EndianMarker = HydraulicErosionForgeConstants.LittleEndianMarker
            };
        }

        private static unsafe void WriteSeamPayloadBlocking(
            string tempPath,
            NativeArray<ErosionDropletDTO> payload,
            int offset,
            int count,
            in ErosionSeamTransferFileHeaderDTO header)
        {
            using (FileStream stream = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 131072, FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                WriteStruct(stream, header);
                byte* ptr = (byte*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(payload) + (offset * UnsafeUtility.SizeOf<ErosionDropletDTO>());
                int byteCount = checked(count * UnsafeUtility.SizeOf<ErosionDropletDTO>());
                stream.Write(new ReadOnlySpan<byte>(ptr, byteCount));
                stream.Flush(true);
            }
        }

        private static NativeArray<T> NewTrackedArray<T>(
            int length,
            Allocator allocator,
            NativeArrayOptions options,
            string label,
            NativeAllocationLifetime lifetime) where T : struct
        {
            NativeArray<T> array = new NativeArray<T>(math.max(0, length), allocator, options);
            try
            {
                int sentinelId = NativeMemorySentinel.RegisterNativeArray(array, NativeMemoryOwner, label, lifetime);
                if (sentinelId <= 0)
                    throw new InvalidOperationException($"Native memory sentinel registration failed for {label}.");
            }
            catch
            {
                array.Dispose();
                throw;
            }

            return array;
        }

        private static NativeQueue<T> NewTrackedQueue<T>(
            int expectedCapacity,
            string label,
            NativeAllocationLifetime lifetime) where T : unmanaged
        {
            NativeQueue<T> queue = new NativeQueue<T>(Allocator.TempJob);
            int capacity = math.max(1, expectedCapacity);
            try
            {
                int sentinelId = NativeMemorySentinel.RegisterNativeQueue(queue, capacity, NativeMemoryOwner, label, lifetime);
                if (sentinelId <= 0)
                    throw new InvalidOperationException($"Native memory sentinel registration failed for {label}.");

                PrewarmQueue(ref queue, capacity);
            }
            catch
            {
                queue.Dispose();
                throw;
            }

            return queue;
        }

        private static void PrewarmQueue<T>(ref NativeQueue<T> queue, int capacity) where T : unmanaged
        {
            if (!queue.IsCreated || capacity <= 0)
                return;

            for (int i = 0; i < capacity; i++)
                queue.Enqueue(default);

            while (queue.TryDequeue(out _))
            {
            }
        }

        private static void DisposeTrackedArray<T>(ref NativeArray<T> array) where T : struct
        {
            if (!array.IsCreated)
                return;

            NativeMemorySentinel.UnregisterNativeArray(array);
            array.Dispose();
            array = default;
        }

        private static void DisposeTrackedQueue<T>(ref NativeQueue<T> queue, string label) where T : unmanaged
        {
            if (!queue.IsCreated)
                return;

            NativeMemorySentinel.UnregisterNativeQueue(NativeMemoryOwner, label);
            queue.Dispose();
            queue = default;
        }

        private static void EnsureFolder(string relativeFolder)
        {
            if (!Directory.Exists(relativeFolder))
                Directory.CreateDirectory(relativeFolder);
        }

        private static void EnsureFileFolder(string relativePath)
        {
            string folder = Path.GetDirectoryName(relativePath);
            if (!string.IsNullOrEmpty(folder) && !Directory.Exists(folder))
                Directory.CreateDirectory(folder);
        }

        private static void DeleteIfExists(string path)
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }
}
#endif
