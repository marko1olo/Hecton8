using System;
using System.Diagnostics;
#if !UNITY_WEBGL || UNITY_EDITOR
using System.IO;
#endif
#if !UNITY_WEBGL && !UNITY_ANDROID && !UNITY_IOS
using System.IO.MemoryMappedFiles;
#endif
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using Hecton8.Core;
using Hecton8.Core.Memory;
using Unity.Burst;
using Unity.Burst.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;
#if !UNITY_WEBGL && !UNITY_ANDROID
using UnityEngine.Networking;
#endif

namespace Hecton8.Data
{
    /// <summary>
    /// Boot-owned static data arena for monolithic baked content blobs.
    /// </summary>
    public static class H8StaticDataArena
    {
        private const long MaxBlobBytes = 256L * 1024L * 1024L;
        private const uint MissingUtf8Offset = uint.MaxValue;
        private const int StaticLocalizationItemsSection = 0;
        private const int StaticLocalizationCreaturesSection = 1;
        private const int StaticLocalizationBiomesSection = 2;
        private const int StaticLocalizationGhostModulesSection = 3;
        private const int StaticLocalizationSopErrorsSection = 4;
        private const int StaticLocalizationAppliedLoreSection = 5;
        private const int StaticLocalizationSectionCount = 6;
        private const uint DefaultAppliedLoreLocalizationLocaleHash = 0x6C199F07u; // en_US
        private const BufferID DataMonolithPayloadBufferId = BufferID.DataMonolithPayload;
        private const BufferID DataMonolithTelemetryRingBufferId = BufferID.DataMonolithTelemetryRing;
        private const BufferID DataMonolithTelemetryCursorBufferId = BufferID.DataMonolithTelemetryCursor;
        private const uint PathFlagManagedFileFallback = 1u;
        private const uint PathFlagMemoryMappedFile = 2u;
        private const uint PathFlagVaultBacked = 4u;
        private const uint PathFlagStreamingUriStaged = 8u;
        private const uint PathFlagNativeFile = 16u;
        private const uint PathFlagStreamingUriRequiresAsync = 32u;
        private const uint PathFlagStreamingUriStagingCancelled = 64u;
        private const uint PathFlagAndroidAssetManager = 128u;
        private const uint PathFlagAndroidJavaAssetManager = 256u;
        private const string DataMonolithTelemetryDumpFileName = "Dump_H8StaticDataArena_Telemetry.bin";
        private const string DataMonolithTelemetryDumpRelativePath = "Docs\\AgentLogs\\Dump_H8StaticDataArena_Telemetry.bin";
#if UNITY_EDITOR
        private const int EditorHotReloadSnapshotChunkBytes = 64 * 1024;
#endif
        private const int DataMonolithWriterReleaseRetryCount = 4;
#if UNITY_ANDROID && !UNITY_EDITOR
        private const int AndroidAssetMissing = -4;
        private const int AndroidAssetCompressed = -6;
        private const int AndroidPersistentPathUtf8Capacity = 1024;
#endif
#if (UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN) && !UNITY_WEBGL && !UNITY_ANDROID && !UNITY_IOS
        private const uint NativeGenericRead = 0x80000000u;
        private const uint NativeGenericWrite = 0x40000000u;
        private const uint NativeFileShareRead = 0x00000001u;
        private const uint NativeFileShareWrite = 0x00000002u;
        private const uint NativeCreateAlways = 2u;
        private const uint NativeOpenExisting = 3u;
        private const uint NativeFileFlagSequentialScan = 0x08000000u;
        private const int NativePathCapacity = 1024;
        private static readonly IntPtr NativeInvalidHandleValue = unchecked((IntPtr)(-1));
#endif

        private static IDataVault _vault;
        private static VaultGenerationHandle<byte> _arenaHandle;
        private static VaultGenerationHandle<H8DataMonolithTelemetryEntry> _telemetryHandle;
        private static VaultGenerationHandle<int> _telemetryCursorHandle;
        private static H8DataBlobHeader _header;
        private static H8DataBlobDirectory _directory;
        private static int _residentBlobBytes;
        private static int _loaded;
        private static int _writeLocked;
        private static int _telemetryFrame;
        private static long _lastReadTicks;
        private static uint _lastReadPathFlags;
        private static uint _lastFailureStage;
        private static uint _lastFailureDetail0;
        private static uint _lastFailureDetail1;
        private static uint _lastFailureDetail2;

        /// <summary>True when a valid blob is resident.</summary>
        public static bool IsLoaded => Volatile.Read(ref _loaded) != 0;

        /// <summary>True when the boot Ready phase has locked the resident arena against writes.</summary>
        public static bool IsWriteLocked => Volatile.Read(ref _writeLocked) != 0;

        /// <summary>Resident blob header.</summary>
        public static H8DataBlobHeader Header => _header;

        /// <summary>Resident blob directory.</summary>
        public static H8DataBlobDirectory Directory => _directory;

        /// <summary>Resident byte count.</summary>
        public static int ByteLength => _residentBlobBytes;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            Shutdown();
        }

        /// <summary>
        /// Loads the default monolith file from StreamingAssets.
        /// </summary>
        /// <param name="expectedWorldSeed">Expected world seed, or zero to accept seed-agnostic blobs.</param>
        /// <param name="expectedAppVersionHash">Expected app-version hash, or zero to skip version matching.</param>
        /// <param name="failIfMissing">If true, a missing file is reported as a boot failure.</param>
        /// <param name="status">Load status.</param>
        /// <returns>True when the blob loaded and passed checksum validation.</returns>
        public static bool TryInitializeFromStreamingAssets(
            uint expectedWorldSeed,
            uint expectedAppVersionHash,
            bool failIfMissing,
            out H8DataBlobLoadStatus status)
        {
            return TryInitializeFromStreamingAssets(
                GlobalRegistry.DataVault,
                expectedWorldSeed,
                expectedAppVersionHash,
                failIfMissing,
                out status);
        }

        /// <summary>
        /// Loads the default monolith file from StreamingAssets using a bootstrap-owned vault instance.
        /// </summary>
        public static bool TryInitializeFromStreamingAssets(
            IDataVault vault,
            uint expectedWorldSeed,
            uint expectedAppVersionHash,
            bool failIfMissing,
            out H8DataBlobLoadStatus status)
        {
#if !UNITY_EDITOR && !DEVELOPMENT_BUILD && UNITY_STANDALONE_WIN && !UNITY_WEBGL && !UNITY_ANDROID && !UNITY_IOS
            return TryInitializeFromWindowsPlayerStreamingAssets(
                vault,
                expectedWorldSeed,
                expectedAppVersionHash,
                failIfMissing,
                out status);
#elif UNITY_ANDROID && !UNITY_EDITOR
            return TryInitializeFromAndroidStreamingAssets(
                vault,
                expectedWorldSeed,
                expectedAppVersionHash,
                failIfMissing,
                out status);
#elif UNITY_WEBGL && !UNITY_EDITOR
            if (!IsLoaded)
                _vault = vault;

            status = H8DataBlobLoadStatus.ReadFailed;
            if (failIfMissing)
                RecordFailureTelemetry(status, 0u);

            return !failIfMissing && IsLoaded;
#else
            string absolutePath = BuildStreamingAssetsLocation(
                Application.streamingAssetsPath,
                H8DataLayoutConstants.DefaultStreamingAssetsRelativePath);

            uint pathFlags = 0u;
            if (!IsFilesystemPath(absolutePath))
            {
                status = H8DataBlobLoadStatus.ReadFailed;
                if (failIfMissing)
                    RecordFailureTelemetry(status, PathFlagStreamingUriRequiresAsync);

                return !failIfMissing && IsLoaded;
            }

            bool loaded = TryInitializeFromFile(
                vault,
                absolutePath,
                expectedWorldSeed,
                expectedAppVersionHash,
                failIfMissing,
                pathFlags,
                out status);
            return loaded;
#endif
        }

        /// <summary>
        /// Loads the default monolith file from StreamingAssets without blocking URL-backed platforms.
        /// </summary>
        public static Awaitable<H8DataBlobLoadResult> TryInitializeFromStreamingAssetsAsync(
            uint expectedWorldSeed,
            uint expectedAppVersionHash,
            bool failIfMissing,
            CancellationToken cancellationToken)
        {
            return TryInitializeFromStreamingAssetsAsync(
                GlobalRegistry.DataVault,
                expectedWorldSeed,
                expectedAppVersionHash,
                failIfMissing,
                cancellationToken);
        }

        /// <summary>
        /// Loads the default monolith file from StreamingAssets using a bootstrap-owned vault instance.
        /// </summary>
        public static async Awaitable<H8DataBlobLoadResult> TryInitializeFromStreamingAssetsAsync(
            IDataVault vault,
            uint expectedWorldSeed,
            uint expectedAppVersionHash,
            bool failIfMissing,
            CancellationToken cancellationToken)
        {
            await Awaitable.MainThreadAsync();
            if (cancellationToken.IsCancellationRequested)
            {
                H8DataBlobLoadStatus cancelledStatus = H8DataBlobLoadStatus.ReadFailed;
                if (failIfMissing)
                    RecordFailureTelemetry(cancelledStatus, PathFlagStreamingUriStagingCancelled);

                return new H8DataBlobLoadResult(!failIfMissing && IsLoaded, cancelledStatus);
            }

#if !UNITY_EDITOR && !DEVELOPMENT_BUILD && UNITY_STANDALONE_WIN && !UNITY_WEBGL && !UNITY_ANDROID && !UNITY_IOS
            bool loaded = TryInitializeFromWindowsPlayerStreamingAssets(
                vault,
                expectedWorldSeed,
                expectedAppVersionHash,
                failIfMissing,
                out H8DataBlobLoadStatus status);
            return new H8DataBlobLoadResult(loaded, status);
#elif UNITY_ANDROID && !UNITY_EDITOR
            bool loaded = TryInitializeFromAndroidStreamingAssets(
                vault,
                expectedWorldSeed,
                expectedAppVersionHash,
                failIfMissing,
                out H8DataBlobLoadStatus status);
            return new H8DataBlobLoadResult(loaded, status);
#elif UNITY_WEBGL && !UNITY_EDITOR
            if (!IsLoaded)
                _vault = vault;

            H8DataBlobLoadStatus status = H8DataBlobLoadStatus.ReadFailed;
            if (failIfMissing)
                RecordFailureTelemetry(status, PathFlagStreamingUriRequiresAsync);

            return new H8DataBlobLoadResult(!failIfMissing && IsLoaded, status);
#else
            string absolutePath = BuildStreamingAssetsLocation(
                Application.streamingAssetsPath,
                H8DataLayoutConstants.DefaultStreamingAssetsRelativePath);

            uint pathFlags = 0u;
            if (!IsFilesystemPath(absolutePath))
            {
#if !UNITY_WEBGL && !UNITY_ANDROID
                string stagedPath = await TryStageStreamingAssetsUriToCacheAsync(absolutePath, cancellationToken);
                if (string.IsNullOrEmpty(stagedPath))
                {
                    H8DataBlobLoadStatus failedStatus = H8DataBlobLoadStatus.ReadFailed;
                    uint failurePathFlags = cancellationToken.IsCancellationRequested
                        ? PathFlagStreamingUriStagingCancelled
                        : PathFlagStreamingUriRequiresAsync;
                    if (failIfMissing)
                        RecordFailureTelemetry(failedStatus, failurePathFlags);

                    return new H8DataBlobLoadResult(!failIfMissing && IsLoaded, failedStatus);
                }

                absolutePath = stagedPath;
                pathFlags |= PathFlagStreamingUriStaged;
#else
                H8DataBlobLoadStatus failedStatus = H8DataBlobLoadStatus.ReadFailed;
                if (failIfMissing)
                    RecordFailureTelemetry(failedStatus, PathFlagStreamingUriRequiresAsync);

                return new H8DataBlobLoadResult(!failIfMissing && IsLoaded, failedStatus);
#endif
            }

            bool loaded = TryInitializeFromFile(
                vault,
                absolutePath,
                expectedWorldSeed,
                expectedAppVersionHash,
                failIfMissing,
                pathFlags,
                out H8DataBlobLoadStatus status);
            return new H8DataBlobLoadResult(loaded, status);
#endif
        }

#if !UNITY_WEBGL || UNITY_EDITOR
        private static string BuildStreamingAssetsLocation(string root, string relativePath)
        {
            if (string.IsNullOrEmpty(root))
                return relativePath;

            if (IsFilesystemPath(root))
                return Path.Combine(root, relativePath);

            string normalizedRoot = root.EndsWith("/", StringComparison.Ordinal) ? root : root + "/";
            return normalizedRoot + relativePath.Replace('\\', '/');
        }

        private static bool IsFilesystemPath(string path)
        {
            return !string.IsNullOrEmpty(path) &&
                   !path.StartsWith("jar:", StringComparison.OrdinalIgnoreCase) &&
                   path.IndexOf("://", StringComparison.Ordinal) < 0;
        }
#endif

#if !UNITY_WEBGL && !UNITY_ANDROID
        private static async Awaitable<string> TryStageStreamingAssetsUriToCacheAsync(
            string streamingUri,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(streamingUri) || IsFilesystemPath(streamingUri))
                return null;

            string cachePath = null;
            string tempPath = null;
            try
            {
                string cacheDirectory = Path.Combine(Application.temporaryCachePath, "Hecton8", "DataMonolith");
                System.IO.Directory.CreateDirectory(cacheDirectory);
                cachePath = Path.Combine(cacheDirectory, "static_data.h8bin");
                tempPath = cachePath + ".tmp";

                TryDeleteFile(tempPath);
                using UnityWebRequest request = new UnityWebRequest(streamingUri, UnityWebRequest.kHttpVerbGET);
                request.downloadHandler = new DownloadHandlerFile(tempPath)
                {
                    removeFileOnAbort = true
                };
                request.disposeDownloadHandlerOnDispose = true;
                request.timeout = 30;

                UnityWebRequestAsyncOperation operation = request.SendWebRequest();
                while (!operation.isDone)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        request.Abort();
                        TryDeleteFile(tempPath);
                        return null;
                    }

                    await Hecton8.Core.AwaitableDebtMonitor.NextFrameAsync();
                }

                if (request.result != UnityWebRequest.Result.Success)
                {
                    TryDeleteFile(tempPath);
                    return null;
                }

                if (!File.Exists(tempPath))
                    return null;

                PromoteTempFileCold(tempPath, cachePath);
                return cachePath;
            }
            catch (OperationCanceledException)
            {
                TryDeleteFile(tempPath);
                return null;
            }
            catch (IOException)
            {
                TryDeleteFile(tempPath);
                return null;
            }
            catch (UnauthorizedAccessException)
            {
                TryDeleteFile(tempPath);
                return null;
            }
            catch (ArgumentException)
            {
                TryDeleteFile(tempPath);
                return null;
            }
            catch (UriFormatException)
            {
                TryDeleteFile(tempPath);
                return null;
            }
            catch (NotSupportedException)
            {
                TryDeleteFile(tempPath);
                return null;
            }
            catch (System.Security.SecurityException)
            {
                TryDeleteFile(tempPath);
                return null;
            }
            catch (InvalidOperationException)
            {
                TryDeleteFile(tempPath);
                return null;
            }
        }
#endif

#if !UNITY_WEBGL || UNITY_EDITOR
        private static void TryDeleteFile(string path)
        {
            if (string.IsNullOrEmpty(path))
                return;

            try
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
            catch (ArgumentException)
            {
            }
            catch (NotSupportedException)
            {
            }
            catch (System.Security.SecurityException)
            {
            }
        }

        private static void PromoteTempFileCold(string tempPath, string finalPath)
        {
            if (File.Exists(finalPath))
                File.Replace(tempPath, finalPath, null, true);
            else
                File.Move(tempPath, finalPath);
        }
#endif

        /// <summary>
        /// Loads one monolith file into a persistent native arena and validates the header checksum.
        /// </summary>
        /// <param name="absolutePath">Absolute path to a `.h8bin` blob.</param>
        /// <param name="expectedWorldSeed">Expected world seed, or zero to accept seed-agnostic blobs.</param>
        /// <param name="expectedAppVersionHash">Expected app-version hash, or zero to skip version matching.</param>
        /// <param name="failIfMissing">If true, missing file is a boot failure.</param>
        /// <param name="status">Load status.</param>
        /// <returns>True when resident data is ready.</returns>
        public static bool TryInitializeFromFile(
            string absolutePath,
            uint expectedWorldSeed,
            uint expectedAppVersionHash,
            bool failIfMissing,
            out H8DataBlobLoadStatus status)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            status = H8DataBlobLoadStatus.ReadFailed;
            if (failIfMissing)
                RecordFailureTelemetry(status, 0u);

            return !failIfMissing && IsLoaded;
#else
            return TryInitializeFromFile(
                GlobalRegistry.DataVault,
                absolutePath,
                expectedWorldSeed,
                expectedAppVersionHash,
                failIfMissing,
                0u,
                out status);
#endif
        }

        public static bool TryInitializeFromFile(
            IDataVault vault,
            string absolutePath,
            uint expectedWorldSeed,
            uint expectedAppVersionHash,
            bool failIfMissing,
            out H8DataBlobLoadStatus status)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            if (!IsLoaded)
                _vault = vault;

            status = H8DataBlobLoadStatus.ReadFailed;
            if (failIfMissing)
                RecordFailureTelemetry(status, 0u);

            return !failIfMissing && IsLoaded;
#else
            return TryInitializeFromFile(
                vault,
                absolutePath,
                expectedWorldSeed,
                expectedAppVersionHash,
                failIfMissing,
                0u,
                out status);
#endif
        }

#if !UNITY_WEBGL || UNITY_EDITOR
        private static bool TryInitializeFromFile(
            IDataVault vault,
            string absolutePath,
            uint expectedWorldSeed,
            uint expectedAppVersionHash,
            bool failIfMissing,
            uint inheritedPathFlags,
            out H8DataBlobLoadStatus status)
        {
            if (IsWriteLocked && IsLoaded)
            {
                status = H8DataBlobLoadStatus.ReadyLocked;
                return false;
            }

            if (!TryAdoptVaultForLoad(vault))
            {
                status = H8DataBlobLoadStatus.ReadFailed;
                SetFailureTelemetry(7u, _arenaHandle.BufferID, _telemetryHandle.BufferID, _telemetryCursorHandle.BufferID);
                RecordFailureTelemetry(status, inheritedPathFlags);
                return false;
            }

            if (!TryProbeExistingBlobLength(absolutePath, out long blobLength, out status))
            {
                SetFailureTelemetry(1u, (uint)status, inheritedPathFlags, 0u);
                if (status == H8DataBlobLoadStatus.Missing)
                {
                    if (failIfMissing)
                        RecordFailureTelemetry(status, inheritedPathFlags);

                    return !failIfMissing && IsLoaded;
                }

                if (status != H8DataBlobLoadStatus.None)
                    RecordFailureTelemetry(status, inheritedPathFlags);

                return false;
            }

            if (blobLength < H8DataLayoutConstants.HeaderSizeBytes + H8DataLayoutConstants.DirectorySizeBytes)
            {
                status = H8DataBlobLoadStatus.FileTooSmall;
                RecordFailureTelemetry(status, inheritedPathFlags);
                return false;
            }

            if (blobLength > MaxBlobBytes || blobLength > int.MaxValue)
            {
                status = H8DataBlobLoadStatus.FileTooLarge;
                RecordFailureTelemetry(status, inheritedPathFlags);
                return false;
            }

            int blobBytes = (int)blobLength;
            IDataVault activeVault = _vault;
            if (!TryShutdownArenaBeforeReplacement(activeVault, inheritedPathFlags, out status))
                return false;

            if (!TryAllocateArena(blobBytes))
            {
                status = H8DataBlobLoadStatus.ReadFailed;
                SetFailureTelemetry(2u, (uint)Math.Max(0, blobBytes), (uint)ComputeArenaCapacity(blobBytes), _arenaHandle.BufferID);
                RecordFailureTelemetry(status, inheritedPathFlags);
                return false;
            }

            if (!TryLoadWholeFileIntoArena(absolutePath, blobBytes, inheritedPathFlags, out status))
            {
                RecordTelemetry(status, _lastReadTicks, _lastReadTicks, _lastReadPathFlags);
                DumpTelemetry(status);
                ShutdownArenaOnly();
                return false;
            }

            _residentBlobBytes = blobBytes;
            if (!TryValidateResidentArena(expectedWorldSeed, expectedAppVersionHash, out status))
            {
                RecordTelemetry(status, _lastReadTicks, _lastReadTicks, _lastReadPathFlags);
                DumpTelemetry(status);
                ShutdownArenaOnly();
                return false;
            }

            Volatile.Write(ref _loaded, 1);
            LockReady();
            status = H8DataBlobLoadStatus.Loaded;
            RecordTelemetry(status, _lastReadTicks, _lastReadTicks, _lastReadPathFlags);
            if (ExceedsTelemetryDumpThreshold(_lastReadTicks))
                DumpTelemetry(status);
            return true;
        }

        private static bool TryProbeExistingBlobLength(
            string absolutePath,
            out long blobLength,
            out H8DataBlobLoadStatus status)
        {
            blobLength = 0L;
            if (string.IsNullOrEmpty(absolutePath))
            {
                status = H8DataBlobLoadStatus.Missing;
                return false;
            }

            try
            {
                if (!File.Exists(absolutePath))
                {
                    status = H8DataBlobLoadStatus.Missing;
                    return false;
                }

                FileInfo info = new FileInfo(absolutePath);
                blobLength = info.Length;
                status = H8DataBlobLoadStatus.None;
                return true;
            }
            catch (IOException)
            {
                status = H8DataBlobLoadStatus.ReadFailed;
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                status = H8DataBlobLoadStatus.ReadFailed;
                return false;
            }
            catch (ArgumentException)
            {
                status = H8DataBlobLoadStatus.ReadFailed;
                return false;
            }
            catch (NotSupportedException)
            {
                status = H8DataBlobLoadStatus.ReadFailed;
                return false;
            }
            catch (System.Security.SecurityException)
            {
                status = H8DataBlobLoadStatus.ReadFailed;
                return false;
            }
        }
#endif

        private static void RecordFailureTelemetry(H8DataBlobLoadStatus status, uint pathFlags)
        {
            _lastReadTicks = 0L;
            _lastReadPathFlags = pathFlags;
            RecordTelemetry(status, 0L, 0L, pathFlags);
            DumpTelemetry(status);
        }

        /// <summary>
        /// Copies an already-read binary blob into the resident native arena with one guarded MemCpy.
        /// </summary>
        /// <param name="source">Source blob pointer.</param>
        /// <param name="sourceBytes">Source byte count.</param>
        /// <param name="expectedWorldSeed">Expected world seed, or zero to accept seed-agnostic blobs.</param>
        /// <param name="expectedAppVersionHash">Expected app-version hash, or zero to skip version matching.</param>
        /// <param name="status">Load status.</param>
        /// <returns>True when resident data is ready.</returns>
        public static unsafe bool TryInitializeFromMemory(
            void* source,
            int sourceBytes,
            uint expectedWorldSeed,
            uint expectedAppVersionHash,
            out H8DataBlobLoadStatus status)
        {
            return TryInitializeFromMemory(
                GlobalRegistry.DataVault,
                source,
                sourceBytes,
                expectedWorldSeed,
                expectedAppVersionHash,
                out status);
        }

        /// <summary>
        /// Copies an already-read binary blob into the resident native arena using a bootstrap-owned vault.
        /// </summary>
        public static unsafe bool TryInitializeFromMemory(
            IDataVault vault,
            void* source,
            int sourceBytes,
            uint expectedWorldSeed,
            uint expectedAppVersionHash,
            out H8DataBlobLoadStatus status)
        {
            if (IsWriteLocked && IsLoaded)
            {
                status = H8DataBlobLoadStatus.ReadyLocked;
                return false;
            }

            if (!TryAdoptVaultForLoad(vault))
            {
                status = H8DataBlobLoadStatus.ReadFailed;
                SetFailureTelemetry(7u, _arenaHandle.BufferID, _telemetryHandle.BufferID, _telemetryCursorHandle.BufferID);
                RecordFailureTelemetry(status, 0u);
                return false;
            }

            if (source == null || sourceBytes < H8DataLayoutConstants.HeaderSizeBytes + H8DataLayoutConstants.DirectorySizeBytes)
            {
                status = H8DataBlobLoadStatus.FileTooSmall;
                RecordFailureTelemetry(status, 0u);
                return false;
            }

            if (sourceBytes > MaxBlobBytes)
            {
                status = H8DataBlobLoadStatus.FileTooLarge;
                RecordFailureTelemetry(status, 0u);
                return false;
            }

            IDataVault activeVault = _vault;
            if (!TryShutdownArenaBeforeReplacement(activeVault, 0u, out status))
                return false;

            if (!TryAllocateArena(sourceBytes))
            {
                status = H8DataBlobLoadStatus.ReadFailed;
                SetFailureTelemetry(2u, (uint)Math.Max(0, sourceBytes), (uint)ComputeArenaCapacity(sourceBytes), _arenaHandle.BufferID);
                RecordFailureTelemetry(status, 0u);
                return false;
            }

            if (!TryAcquireArenaWriteView(out NativeArray<byte> arena))
            {
                status = H8DataBlobLoadStatus.ReadFailed;
                SetFailureTelemetry(3u, _arenaHandle.BufferID, _arenaHandle.Generation, _arenaHandle.SystemID);
                RecordFailureTelemetry(status, 0u);
                ShutdownArenaOnly();
                return false;
            }

            bool copied = false;
            bool writeLockReleased = false;
            try
            {
                if (arena.Length >= sourceBytes)
                {
                    byte* destination = (byte*)NativeArrayUnsafeUtility.GetUnsafePtr(arena);
                    copied = UnsafeMemoryCopyGuard.TryMemCpy(destination, sourceBytes, source, sourceBytes);
                }
            }
            finally
            {
                writeLockReleased = ReleaseArenaWriteView();
            }

            if (!copied || !writeLockReleased)
            {
                status = H8DataBlobLoadStatus.ReadFailed;
                SetFailureTelemetry(writeLockReleased ? 6u : 4u, copied ? 1u : 0u, _arenaHandle.BufferID, _arenaHandle.Generation);
                RecordTelemetry(status, 0L, 0L, 0u);
                DumpTelemetry(status);
                ShutdownArenaOnly();
                return false;
            }

            _residentBlobBytes = sourceBytes;
            if (!TryValidateResidentArena(expectedWorldSeed, expectedAppVersionHash, out status))
            {
                RecordTelemetry(status, 0L, 0L, 0u);
                DumpTelemetry(status);
                ShutdownArenaOnly();
                return false;
            }

            Volatile.Write(ref _loaded, 1);
            LockReady();
            status = H8DataBlobLoadStatus.Loaded;
            RecordTelemetry(status, 0L, 0L, 0u);
            return true;
        }

        /// <summary>
        /// Locks the monolith against writes after boot readiness.
        /// </summary>
        public static void LockReady()
        {
            Interlocked.Exchange(ref _writeLocked, 1);
        }

        /// <summary>
        /// Releases the resident static data arena.
        /// </summary>
        public static void Shutdown()
        {
            Interlocked.Exchange(ref _writeLocked, 0);
            ShutdownArenaOnly();
        }

        /// <summary>
        /// Returns a section entry by numeric section ID.
        /// </summary>
        /// <param name="sectionId">Section identifier.</param>
        /// <param name="section">Resolved section entry.</param>
        /// <returns>True when the section exists and has a valid table entry.</returns>
        public static bool TryGetSection(H8DataSectionId sectionId, out H8DataSectionEntry section)
        {
            section = default;
            if (!IsLoaded || !TryRefreshArenaReadOnly(out NativeArray<byte>.ReadOnly arena))
                return false;

            return TryGetSectionFromArena(arena, sectionId, out section);
        }

        private static unsafe bool TryGetSectionFromArena(
            NativeArray<byte>.ReadOnly arena,
            H8DataSectionId sectionId,
            out H8DataSectionEntry section)
        {
            section = default;
            if (!arena.IsCreated || _directory.SectionCount == 0)
                return false;

            byte* basePtr = (byte*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(arena);
            H8DataSectionEntry* entries = (H8DataSectionEntry*)(basePtr + _directory.SectionTableOffset);
            uint target = (uint)sectionId;

            uint directIndex = target - 1u;
            if (target != 0u && directIndex < _directory.SectionCount)
            {
                H8DataSectionEntry direct = entries[(int)directIndex];
                if (direct.SectionId == target)
                {
                    section = direct;
                    return IsSectionRangeValid(in section);
                }
            }

            for (int i = 0; i < _directory.SectionCount; i++)
            {
                H8DataSectionEntry entry = entries[i];
                if (entry.SectionId != target)
                    continue;

                section = entry;
                return IsSectionRangeValid(in section);
            }

            return false;
        }

        /// <summary>
        /// Returns a direct typed span over a resident section when the baked record size matches <typeparamref name="T"/>.
        /// </summary>
        public static ReadOnlySpan<T> GetSectionSpan<T>(H8DataSectionId sectionId)
            where T : unmanaged
        {
            return TryGetSectionSpan(sectionId, out ReadOnlySpan<T> records) ? records : ReadOnlySpan<T>.Empty;
        }

        public static ReadOnlySpan<T> GetSectionSpan<T>(uint sectionId)
            where T : unmanaged
        {
            return GetSectionSpan<T>((H8DataSectionId)sectionId);
        }

        /// <summary>
        /// Returns a direct typed span over a resident section without allocating or copying.
        /// </summary>
        public static unsafe bool TryGetSectionSpan<T>(H8DataSectionId sectionId, out ReadOnlySpan<T> records)
            where T : unmanaged
        {
            records = ReadOnlySpan<T>.Empty;
            if (!IsLoaded ||
                !TryRefreshArenaReadOnly(out NativeArray<byte>.ReadOnly arena))
            {
                return false;
            }

            return TryGetSectionSpanInArena(arena, sectionId, out records);
        }

        private static unsafe bool TryGetSectionSpanInArena<T>(
            NativeArray<byte>.ReadOnly arena,
            H8DataSectionId sectionId,
            out ReadOnlySpan<T> records) where T : unmanaged
        {
            records = ReadOnlySpan<T>.Empty;
            if (!arena.IsCreated ||
                !TryGetSectionFromArena(arena, sectionId, out H8DataSectionEntry section) ||
                section.Count == 0u ||
                section.Count > (uint)int.MaxValue)
            {
                return false;
            }

            int recordSize = UnsafeUtility.SizeOf<T>();
            if (section.RecordSize != (uint)recordSize)
                return false;

            byte* basePtr = (byte*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(arena);
            ref T firstRecord = ref UnsafeUtility.ArrayElementAsRef<T>(basePtr + section.OffsetBytes, 0);
            records = MemoryMarshal.CreateReadOnlySpan(ref firstRecord, (int)section.Count);
            return true;
        }

        /// <summary>
        /// Resolves one item record by dense record index.
        /// </summary>
        public static bool TryGetItemRecord(uint recordIndex, out H8ItemRecord record)
        {
            record = default;
            ReadOnlySpan<H8ItemRecord> records = GetSectionSpan<H8ItemRecord>(H8DataSectionId.Items);
            if (recordIndex >= (uint)records.Length)
                return false;

            record = records[(int)recordIndex];
            return true;
        }

        /// <summary>
        /// Resolves one compact creature genome block by dense record index.
        /// </summary>
        public static bool TryGetCreatureGenomeBlock(uint recordIndex, out H8CreatureGenomeTraitBlock block)
        {
            block = default;
            ReadOnlySpan<H8CreatureTraitRecord> records = GetSectionSpan<H8CreatureTraitRecord>(H8DataSectionId.Creatures);
            if (recordIndex >= (uint)records.Length)
                return false;

            H8CreatureTraitRecord record = records[(int)recordIndex];
            block.Aggression = H8SoAReconstructMath.FiniteOr(record.Genome.Aggression, 0f);
            block.Metabolism = H8SoAReconstructMath.FiniteOr(record.Genome.Metabolism, 1f);
            block.MaxHealth = H8SoAReconstructMath.FiniteOr(record.Genome.MaxHealth, 1f);
            block.CruiseSpeed = H8SoAReconstructMath.FiniteOr(record.Genome.CruiseSpeed, 0f);
            block.BurstSpeed = H8SoAReconstructMath.FiniteOr(record.Genome.BurstSpeed, 0f);
            block.SpawnCreditCost = H8SoAReconstructMath.FiniteOr(record.Genome.SpawnCreditCost, 0f);
            block.PressureMinMeters = H8SoAReconstructMath.FiniteOr(record.Genome.PressureMinMeters, 0f);
            block.PressureMaxMeters = H8SoAReconstructMath.FiniteOr(record.Genome.PressureMaxMeters, 0f);
            return true;
        }

        /// <summary>
        /// Resolves one item record by FNV-1a hash. Item records are sorted by hash at bake time.
        /// </summary>
        public static bool TryFindItemRecordByHash(uint hashId, out H8ItemRecord record)
        {
            record = default;
            if (hashId == 0u)
                return false;

            ReadOnlySpan<H8ItemRecord> records = GetSectionSpan<H8ItemRecord>(H8DataSectionId.Items);
            if (records.Length <= 0)
                return false;

            return TryFindByHash(records, hashId, out record);
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private static unsafe bool TryFindByHash([NoAlias] H8ItemRecord* records, int count, uint hashId, out H8ItemRecord record)
        {
            record = default;
            if (records == null || count <= 0 || hashId == 0u)
                return false;

            int low = 0;
            int high = count - 1;
            while (low <= high)
            {
                int index = low + ((high - low) >> 1);
                H8ItemRecord candidate = records[index];
                if (candidate.HashId == hashId)
                {
                    record = candidate;
                    return true;
                }

                if (candidate.HashId < hashId)
                    low = index + 1;
                else
                    high = index - 1;
            }

            return false;
        }

        public static unsafe bool TryFindByHash(ReadOnlySpan<H8ItemRecord> records, uint hashId, out H8ItemRecord record)
        {
            fixed (H8ItemRecord* ptr = records)
            {
                return TryFindByHash(ptr, records.Length, hashId, out record);
            }
        }

        /// <summary>
        /// Resolves one localized applied-lore packet by packet and locale hash.
        /// Applied lore records are sorted by PacketHash, then LocaleHash at bake time.
        /// </summary>
        public static bool TryFindAppliedLorePacket(
            uint packetHash,
            uint localeHash,
            out H8AppliedLorePacketRecord record)
        {
            record = default;
            if (packetHash == 0u || localeHash == 0u)
                return false;

            ReadOnlySpan<H8AppliedLorePacketRecord> records = GetSectionSpan<H8AppliedLorePacketRecord>(H8DataSectionId.AppliedLorePackets);
            if (records.Length <= 0)
                return false;

            int low = 0;
            int high = records.Length - 1;
            while (low <= high)
            {
                int index = low + ((high - low) >> 1);
                ref readonly H8AppliedLorePacketRecord candidate = ref records[index];
                int compare = CompareAppliedLoreKey(candidate.PacketHash, candidate.LocaleHash, packetHash, localeHash);
                if (compare == 0)
                {
                    record = candidate;
                    return true;
                }

                if (compare < 0)
                    low = index + 1;
                else
                    high = index - 1;
            }

            return false;
        }

        /// <summary>
        /// Resolves a bounded UTF-8 slice for one applied-lore surface.
        /// </summary>
        public static bool TryGetAppliedLoreUtf8(
            in H8AppliedLorePacketRecord record,
            H8AppliedLoreSurface surface,
            out ReadOnlySpan<byte> utf8Bytes)
        {
            utf8Bytes = ReadOnlySpan<byte>.Empty;
            uint offset;
            uint length;
            uint surfaceBit;
            switch (surface)
            {
                case H8AppliedLoreSurface.Title:
                    surfaceBit = 1u << 0;
                    offset = record.TitleUtf8Offset;
                    length = record.TitleUtf8ByteLength;
                    break;
                case H8AppliedLoreSurface.Scanner:
                    surfaceBit = 1u << 1;
                    offset = record.ScannerUtf8Offset;
                    length = record.ScannerUtf8ByteLength;
                    break;
                case H8AppliedLoreSurface.Terminal:
                    surfaceBit = 1u << 2;
                    offset = record.TerminalUtf8Offset;
                    length = record.TerminalUtf8ByteLength;
                    break;
                case H8AppliedLoreSurface.Audio:
                    surfaceBit = 1u << 3;
                    offset = record.AudioUtf8Offset;
                    length = record.AudioUtf8ByteLength;
                    break;
                case H8AppliedLoreSurface.InGameWiki:
                    surfaceBit = 1u << 4;
                    offset = record.WikiUtf8Offset;
                    length = record.WikiUtf8ByteLength;
                    break;
                case H8AppliedLoreSurface.ExternalSite:
                    surfaceBit = 1u << 5;
                    offset = record.SiteUtf8Offset;
                    length = record.SiteUtf8ByteLength;
                    break;
                case H8AppliedLoreSurface.FieldNote:
                    surfaceBit = 1u << 6;
                    offset = record.FieldNoteUtf8Offset;
                    length = record.FieldNoteUtf8ByteLength;
                    break;
                default:
                    return false;
            }

            if ((record.SurfaceMask & surfaceBit) == 0u)
                return false;

            if (length == 0u || length > int.MaxValue)
                return false;

            return TryGetLocalizedUtf8Span(offset, (int)length, out utf8Bytes);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int CompareAppliedLoreKey(uint leftPacketHash, uint leftLocaleHash, uint rightPacketHash, uint rightLocaleHash)
        {
            if (leftPacketHash < rightPacketHash)
                return -1;
            if (leftPacketHash > rightPacketHash)
                return 1;
            if (leftLocaleHash < rightLocaleHash)
                return -1;
            if (leftLocaleHash > rightLocaleHash)
                return 1;
            return 0;
        }

        /// <summary>
        /// Resolves one baked applied-lore route by route-card hash.
        /// Applied lore routes are sorted by RouteCardHash at bake time.
        /// </summary>
        public static bool TryFindAppliedLoreRoute(uint routeCardHash, out H8AppliedLoreRouteRecord record)
        {
            record = default;
            if (routeCardHash == 0u)
                return false;

            ReadOnlySpan<H8AppliedLoreRouteRecord> records = GetSectionSpan<H8AppliedLoreRouteRecord>(H8DataSectionId.AppliedLoreRoutes);
            if (records.Length <= 0)
                return false;

            int low = 0;
            int high = records.Length - 1;
            while (low <= high)
            {
                int index = low + ((high - low) >> 1);
                ref readonly H8AppliedLoreRouteRecord candidate = ref records[index];
                if (candidate.RouteCardHash == routeCardHash)
                {
                    record = candidate;
                    return true;
                }

                if (candidate.RouteCardHash < routeCardHash)
                    low = index + 1;
                else
                    high = index - 1;
            }

            return false;
        }

        /// <summary>
        /// Returns the current baked applied-lore route count.
        /// </summary>
        public static int GetAppliedLoreRouteCount()
        {
            return GetSectionSpan<H8AppliedLoreRouteRecord>(H8DataSectionId.AppliedLoreRoutes).Length;
        }

        /// <summary>
        /// Resolves one applied-lore route by sorted record index.
        /// </summary>
        public static bool TryGetAppliedLoreRouteAt(int index, out H8AppliedLoreRouteRecord record)
        {
            record = default;
            ReadOnlySpan<H8AppliedLoreRouteRecord> records = GetSectionSpan<H8AppliedLoreRouteRecord>(H8DataSectionId.AppliedLoreRoutes);
            if ((uint)index >= (uint)records.Length)
                return false;

            record = records[index];
            return record.RouteCardHash != 0u;
        }

        /// <summary>
        /// Resolves the first baked applied-lore route that directly references a packet hash.
        /// </summary>
        public static bool TryFindAppliedLoreRouteForPacket(uint packetHash, out H8AppliedLoreRouteRecord record)
        {
            record = default;
            if (packetHash == 0u)
                return false;

            ReadOnlySpan<H8AppliedLoreRouteRecord> records = GetSectionSpan<H8AppliedLoreRouteRecord>(H8DataSectionId.AppliedLoreRoutes);
            for (int i = 0; i < records.Length; i++)
            {
                ref readonly H8AppliedLoreRouteRecord candidate = ref records[i];
                if (candidate.RouteCardHash == 0u)
                    continue;

                if (AppliedLoreRouteContainsPacket(in candidate, packetHash))
                {
                    record = candidate;
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Returns true when a baked applied-lore route directly references the packet hash.
        /// </summary>
        public static bool AppliedLoreRouteContainsPacket(in H8AppliedLoreRouteRecord record, uint packetHash)
        {
            if (packetHash == 0u || record.PacketCount == 0u)
                return false;

            uint count = math.min(record.PacketCount, (uint)H8DataLayoutConstants.AppliedLoreRoutePacketCapacity);
            if (record.PacketHash0 == packetHash)
                return true;
            if (count <= 1u)
                return false;
            if (record.PacketHash1 == packetHash)
                return true;
            if (count <= 2u)
                return false;
            if (record.PacketHash2 == packetHash)
                return true;
            if (count <= 3u)
                return false;
            if (record.PacketHash3 == packetHash)
                return true;
            if (count <= 4u)
                return false;
            if (record.PacketHash4 == packetHash)
                return true;
            if (count <= 5u)
                return false;
            if (record.PacketHash5 == packetHash)
                return true;
            if (count <= 6u)
                return false;
            if (record.PacketHash6 == packetHash)
                return true;
            return count > 7u && record.PacketHash7 == packetHash;
        }

        /// <summary>
        /// Reads one inline packet hash from an applied-lore route record.
        /// </summary>
        public static uint GetAppliedLoreRoutePacketHash(in H8AppliedLoreRouteRecord record, uint index)
        {
            switch (index)
            {
                case 0u: return record.PacketHash0;
                case 1u: return record.PacketHash1;
                case 2u: return record.PacketHash2;
                case 3u: return record.PacketHash3;
                case 4u: return record.PacketHash4;
                case 5u: return record.PacketHash5;
                case 6u: return record.PacketHash6;
                case 7u: return record.PacketHash7;
                default: return 0u;
            }
        }

        /// <summary>
        /// Reads one inline prerequisite packet hash from an applied-lore route record.
        /// </summary>
        public static uint GetAppliedLoreRouteRequiredPacketHash(in H8AppliedLoreRouteRecord record, uint index)
        {
            switch (index)
            {
                case 0u: return record.RequiredPacketHash0;
                case 1u: return record.RequiredPacketHash1;
                case 2u: return record.RequiredPacketHash2;
                case 3u: return record.RequiredPacketHash3;
                default: return 0u;
            }
        }

        /// <summary>
        /// Provides the resident blob as a read-only native array for Burst jobs.
        /// </summary>
        /// <param name="arena">Resident arena.</param>
        /// <returns>True when a blob is loaded.</returns>
        public static bool TryGetArena(out NativeArray<byte>.ReadOnly arena)
        {
            arena = default;
            if (!IsLoaded || !TryRefreshArenaReadOnly(out NativeArray<byte>.ReadOnly readOnlyArena))
                return false;

            arena = readOnlyArena;
            return true;
        }

        /// <summary>
        /// Provides the resident blob and the valid byte count inside the larger static arena.
        /// </summary>
        public static bool TryGetResidentBlob(out NativeArray<byte>.ReadOnly arena, out int blobBytes)
        {
            arena = default;
            blobBytes = _residentBlobBytes;
            if (!IsLoaded || !TryRefreshArenaReadOnly(out NativeArray<byte>.ReadOnly readOnlyArena) || _residentBlobBytes <= 0)
                return false;

            arena = readOnlyArena;
            return true;
        }

        /// <summary>
        /// Resolves a deterministic integer loot CDF entry using a pre-ranged threshold.
        /// </summary>
        public static bool TryResolveLootItem(uint tableHash, uint threshold, out uint itemHash)
        {
            itemHash = 0u;
            ReadOnlySpan<H8LootCdfRecord> records = GetSectionSpan<H8LootCdfRecord>(H8DataSectionId.LootCdf);
            if (tableHash == 0u ||
                records.Length <= 0 ||
                !TryFindLootTableRange(records, tableHash, out int start, out int end, out uint totalWeight) ||
                totalWeight == 0u)
            {
                return false;
            }

            if (threshold >= totalWeight)
                threshold = totalWeight - 1u;

            if ((uint)start >= (uint)records.Length || end > records.Length)
                return false;

            int low = start;
            int high = end - 1;
            while (low <= high)
            {
                int index = low + ((high - low) >> 1);
                H8LootCdfRecord record = records[index];
                if (threshold < record.CumulativeWeight)
                {
                    if (index == start || threshold >= records[index - 1].CumulativeWeight)
                    {
                        itemHash = record.ItemHash;
                        return itemHash != 0u;
                    }

                    high = index - 1;
                }
                else
                {
                    low = index + 1;
                }
            }

            return false;
        }

        /// <summary>
        /// Resolves one 256x256 biome heatmap cell without querying MapMagic.
        /// </summary>
        public static bool TryGetBiomeHeatmapCell(int x, int y, out uint biomeHash)
        {
            biomeHash = 0u;
            ReadOnlySpan<H8BiomeHeatmapCellRecord> records = GetSectionSpan<H8BiomeHeatmapCellRecord>(H8DataSectionId.BiomeHeatmap);
            if (records.Length <= 0)
                return false;

            int clampedX = math.clamp(x, 0, 255);
            int clampedY = math.clamp(y, 0, 255);
            int directIndex = (clampedY << 8) + clampedX;
            if (directIndex >= records.Length)
                return false;

            H8BiomeHeatmapCellRecord direct = records[directIndex];
            if (direct.X != clampedX || direct.Y != clampedY)
                return false;

            biomeHash = direct.BiomeHash;
            return biomeHash != 0u;
        }

        /// <summary>
        /// Resolves total integer weight for a deterministic loot table.
        /// </summary>
        public static bool TryGetLootTableTotalWeight(uint tableHash, out uint totalWeight)
        {
            totalWeight = 0u;
            return tableHash != 0u && TryFindLootTableRange(tableHash, out _, out _, out totalWeight);
        }

        /// <summary>
        /// Resolves static voxel material properties by VoxelID hash.
        /// </summary>
        public static bool TryFindVoxelMaterialRecord(uint voxelHash, out H8VoxelMaterialRecord record)
        {
            record = default;
            ReadOnlySpan<H8VoxelMaterialRecord> records = GetSectionSpan<H8VoxelMaterialRecord>(H8DataSectionId.VoxelMaterials);
            if (records.Length <= 0 || voxelHash == 0u)
                return false;

            int low = 0;
            int high = records.Length - 1;
            while (low <= high)
            {
                int index = low + ((high - low) >> 1);
                H8VoxelMaterialRecord candidate = records[index];
                if (candidate.VoxelHash == voxelHash)
                {
                    record = candidate;
                    return true;
                }

                if (candidate.VoxelHash < voxelHash)
                    low = index + 1;
                else
                    high = index - 1;
            }

            return false;
        }

        /// <summary>
        /// Resolves static audio Addressables registry data by EventID hash.
        /// </summary>
        public static bool TryFindAudioClipRecord(uint eventHash, out H8AudioClipRegistryRecord record)
        {
            record = default;
            ReadOnlySpan<H8AudioClipRegistryRecord> records = GetSectionSpan<H8AudioClipRegistryRecord>(H8DataSectionId.AudioClipRegistry);
            if (records.Length <= 0 || eventHash == 0u)
                return false;

            int low = 0;
            int high = records.Length - 1;
            while (low <= high)
            {
                int index = low + ((high - low) >> 1);
                H8AudioClipRegistryRecord candidate = records[index];
                if (candidate.EventHash == eventHash)
                {
                    record = candidate;
                    return true;
                }

                if (candidate.EventHash < eventHash)
                    low = index + 1;
                else
                    high = index - 1;
            }

            return false;
        }

        /// <summary>
        /// Reads an audio Addressables key into caller-owned char storage.
        /// </summary>
        public static bool TryReadAudioAddressableKey(uint eventHash, Span<char> destination, out ReadOnlySpan<char> key)
        {
            key = default;
            return TryFindAudioClipRecord(eventHash, out H8AudioClipRegistryRecord record) &&
                   TryReadLocalizedText(record.AddressableKeyUtf8Offset, record.AddressableKeyUtf8ByteLength, destination, out key);
        }

        /// <summary>
        /// Resolves one baked depth-pressure LUT sample by index.
        /// </summary>
        public static bool TryGetDepthPressureSample(int sampleIndex, out H8DepthPressureSampleRecord record)
        {
            record = default;
            ReadOnlySpan<H8DepthPressureSampleRecord> records = GetSectionSpan<H8DepthPressureSampleRecord>(H8DataSectionId.DepthPressureCurve);
            if (records.Length <= 0)
                return false;

            int index = math.clamp(sampleIndex, 0, records.Length - 1);
            record = records[index];
            return true;
        }

        /// <summary>
        /// Resolves the nearest baked pressure sample without runtime pow.
        /// </summary>
        public static bool TrySampleDepthPressure(float depthMeters, out H8DepthPressureSampleRecord record)
        {
            record = default;
            depthMeters = H8SoAReconstructMath.FiniteOr(depthMeters, 0f);

            int index = (int)math.round(math.clamp(depthMeters, 0f, 5000f) * (255f / 5000f));
            return TryGetDepthPressureSample(index, out record);
        }

        /// <summary>
        /// Resolves static submarine hull constants by part hash.
        /// </summary>
        public static bool TryFindSubmarineHullConstants(uint partHash, out H8SubmarineHullConstantRecord record)
        {
            record = default;
            ReadOnlySpan<H8SubmarineHullConstantRecord> records = GetSectionSpan<H8SubmarineHullConstantRecord>(H8DataSectionId.SubmarineHullConstants);
            if (records.Length <= 0 || partHash == 0u)
                return false;

            int low = 0;
            int high = records.Length - 1;
            while (low <= high)
            {
                int index = low + ((high - low) >> 1);
                H8SubmarineHullConstantRecord candidate = records[index];
                if (candidate.PartHash == partHash)
                {
                    record = candidate;
                    return true;
                }

                if (candidate.PartHash < partHash)
                    low = index + 1;
                else
                    high = index - 1;
            }

            return false;
        }

        /// <summary>
        /// Resolves static physics material constants by surface hash.
        /// </summary>
        public static bool TryFindPhysicsMaterial(uint surfaceHash, out H8PhysicsMaterialRecord record)
        {
            record = default;
            ReadOnlySpan<H8PhysicsMaterialRecord> records = GetSectionSpan<H8PhysicsMaterialRecord>(H8DataSectionId.PhysicsMaterials);
            if (records.Length <= 0 || surfaceHash == 0u)
                return false;

            int low = 0;
            int high = records.Length - 1;
            while (low <= high)
            {
                int index = low + ((high - low) >> 1);
                H8PhysicsMaterialRecord candidate = records[index];
                if (candidate.SurfaceHash == surfaceHash)
                {
                    record = candidate;
                    return true;
                }

                if (candidate.SurfaceHash < surfaceHash)
                    low = index + 1;
                else
                    high = index - 1;
            }

            return false;
        }

        /// <summary>
        /// Decodes a null-terminated UTF-8 localization entry into caller-owned char storage.
        /// </summary>
        /// <param name="utf8Offset">Offset relative to the localization block.</param>
        /// <param name="destination">Caller-owned char destination.</param>
        /// <param name="text">Span over <paramref name="destination"/> containing decoded text.</param>
        /// <returns>True when text was decoded without allocation.</returns>
        public static bool TryReadLocalizedText(int utf8Offset, Span<char> destination, out ReadOnlySpan<char> text)
        {
            if (utf8Offset < 0)
            {
                text = default;
                return false;
            }

            return TryReadLocalizedText((uint)utf8Offset, destination, out text);
        }

        public static unsafe bool TryReadLocalizedText(uint utf8Offset, Span<char> destination, out ReadOnlySpan<char> text)
        {
            text = default;
            if (!IsLoaded ||
                !TryRefreshArenaReadOnly(out NativeArray<byte>.ReadOnly arena) ||
                utf8Offset == MissingUtf8Offset ||
                destination.Length == 0 ||
                _directory.LocalizationBytes == 0)
            {
                return false;
            }

            if (utf8Offset >= _directory.LocalizationBytes)
                return false;

            byte* basePtr = (byte*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(arena);
            byte* locPtr = basePtr + _directory.LocalizationOffset;
            int offset = (int)utf8Offset;
            int maxBytes = (int)_directory.LocalizationBytes - offset;
            int byteLength = 0;
            bool foundTerminator = false;
            while (byteLength < maxBytes)
            {
                if (locPtr[offset + byteLength] == 0)
                {
                    foundTerminator = true;
                    break;
                }

                byteLength++;
            }

            if (!foundTerminator || byteLength <= 0)
                return false;

            ref byte firstUtf8Byte = ref UnsafeUtility.ArrayElementAsRef<byte>(locPtr + offset, 0);
            ReadOnlySpan<byte> utf8 = MemoryMarshal.CreateReadOnlySpan(ref firstUtf8Byte, byteLength);
            if (!TryDecodeUtf8(utf8, destination, out int charsWritten))
                return false;

            text = destination.Slice(0, charsWritten);
            return true;
        }

        public static bool TryReadLocalizedText(uint utf8Offset, uint byteLength, Span<char> destination, out ReadOnlySpan<char> text)
        {
            text = default;
            if (byteLength == 0u || byteLength > int.MaxValue || destination.Length == 0)
                return false;

            if (!TryGetLocalizedUtf8Span(utf8Offset, (int)byteLength, out ReadOnlySpan<byte> utf8))
                return false;

            if (!TryDecodeUtf8(utf8, destination, out int charsWritten))
                return false;

            text = destination.Slice(0, charsWritten);
            return true;
        }

        private static bool TryDecodeUtf8(ReadOnlySpan<byte> utf8, Span<char> destination, out int charsWritten)
        {
            charsWritten = 0;
            int read = 0;
            while (read < utf8.Length)
            {
                byte b0 = utf8[read++];
                uint scalar;
                if (b0 < 0x80)
                {
                    scalar = b0;
                }
                else if ((b0 & 0xE0) == 0xC0)
                {
                    if (read >= utf8.Length)
                        return false;

                    byte b1 = utf8[read++];
                    if ((b1 & 0xC0) != 0x80)
                        return false;

                    scalar = (uint)(((b0 & 0x1Fu) << 6) | (b1 & 0x3Fu));
                    if (scalar < 0x80u)
                        return false;
                }
                else if ((b0 & 0xF0) == 0xE0)
                {
                    if (read + 1 >= utf8.Length)
                        return false;

                    byte b1 = utf8[read++];
                    byte b2 = utf8[read++];
                    if ((b1 & 0xC0) != 0x80 || (b2 & 0xC0) != 0x80)
                        return false;

                    scalar = (uint)(((b0 & 0x0Fu) << 12) | ((b1 & 0x3Fu) << 6) | (b2 & 0x3Fu));
                    if (scalar < 0x800u || (scalar >= 0xD800u && scalar <= 0xDFFFu))
                        return false;
                }
                else if ((b0 & 0xF8) == 0xF0)
                {
                    if (read + 2 >= utf8.Length)
                        return false;

                    byte b1 = utf8[read++];
                    byte b2 = utf8[read++];
                    byte b3 = utf8[read++];
                    if ((b1 & 0xC0) != 0x80 || (b2 & 0xC0) != 0x80 || (b3 & 0xC0) != 0x80)
                        return false;

                    scalar = (uint)(((b0 & 0x07u) << 18) | ((b1 & 0x3Fu) << 12) | ((b2 & 0x3Fu) << 6) | (b3 & 0x3Fu));
                    if (scalar < 0x10000u || scalar > 0x10FFFFu)
                        return false;
                }
                else
                {
                    return false;
                }

                if (scalar <= 0xFFFFu)
                {
                    if (charsWritten >= destination.Length)
                        return false;

                    destination[charsWritten++] = (char)scalar;
                    continue;
                }

                if (charsWritten + 1 >= destination.Length)
                    return false;

                scalar -= 0x10000u;
                destination[charsWritten++] = (char)(0xD800u + (scalar >> 10));
                destination[charsWritten++] = (char)(0xDC00u + (scalar & 0x3FFu));
            }

            return true;
        }

        /// <summary>
        /// Returns the full resident LocData UTF-8 byte block without decoding or allocation.
        /// </summary>
        public static unsafe bool TryGetLocalizedUtf8Block(out ReadOnlySpan<byte> utf8Bytes)
        {
            utf8Bytes = default;
            if (!IsLoaded || !TryRefreshArenaReadOnly(out NativeArray<byte>.ReadOnly arena) || _directory.LocalizationBytes == 0)
                return false;

            byte* basePtr = (byte*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(arena);
            ref byte firstUtf8Byte = ref UnsafeUtility.ArrayElementAsRef<byte>(basePtr + _directory.LocalizationOffset, 0);
            utf8Bytes = MemoryMarshal.CreateReadOnlySpan(ref firstUtf8Byte, (int)_directory.LocalizationBytes);
            return true;
        }

        /// <summary>
        /// Returns a bounded UTF-8 localization slice without decoding or allocation.
        /// </summary>
        public static bool TryGetLocalizedUtf8Span(int utf8Offset, int byteLength, out ReadOnlySpan<byte> utf8Bytes)
        {
            if (utf8Offset < 0)
            {
                utf8Bytes = default;
                return false;
            }

            return TryGetLocalizedUtf8Span((uint)utf8Offset, byteLength, out utf8Bytes);
        }

        public static unsafe bool TryGetLocalizedUtf8Span(uint utf8Offset, int byteLength, out ReadOnlySpan<byte> utf8Bytes)
        {
            utf8Bytes = default;
            if (!IsLoaded ||
                !TryRefreshArenaReadOnly(out NativeArray<byte>.ReadOnly arena) ||
                utf8Offset == MissingUtf8Offset ||
                byteLength < 0 ||
                _directory.LocalizationBytes == 0)
            {
                return false;
            }

            if (utf8Offset >= _directory.LocalizationBytes ||
                (uint)byteLength > _directory.LocalizationBytes - utf8Offset)
            {
                return false;
            }

            byte* basePtr = (byte*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(arena);
            byte* locPtr = basePtr + _directory.LocalizationOffset;
            ref byte firstUtf8Byte = ref UnsafeUtility.ArrayElementAsRef<byte>(locPtr + (int)utf8Offset, 0);
            utf8Bytes = MemoryMarshal.CreateReadOnlySpan(ref firstUtf8Byte, byteLength);
            return true;
        }

        /// <summary>
        /// Returns one null-terminated UTF-8 localization slice without decoding or allocation.
        /// </summary>
        public static bool TryGetLocalizedUtf8Span(int utf8Offset, out ReadOnlySpan<byte> utf8Bytes)
        {
            if (utf8Offset < 0)
            {
                utf8Bytes = default;
                return false;
            }

            return TryGetLocalizedUtf8Span((uint)utf8Offset, out utf8Bytes);
        }

        public static unsafe bool TryGetLocalizedUtf8Span(uint utf8Offset, out ReadOnlySpan<byte> utf8Bytes)
        {
            utf8Bytes = default;
            if (!IsLoaded ||
                !TryRefreshArenaReadOnly(out NativeArray<byte>.ReadOnly arena) ||
                utf8Offset == MissingUtf8Offset ||
                _directory.LocalizationBytes == 0)
            {
                return false;
            }

            if (utf8Offset >= _directory.LocalizationBytes)
                return false;

            byte* basePtr = (byte*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(arena);
            byte* locPtr = basePtr + _directory.LocalizationOffset;
            int offset = (int)utf8Offset;
            int maxBytes = (int)_directory.LocalizationBytes - offset;
            int byteLength = 0;
            bool foundTerminator = false;
            while (byteLength < maxBytes)
            {
                if (locPtr[offset + byteLength] == 0)
                {
                    foundTerminator = true;
                    break;
                }

                byteLength++;
            }

            if (!foundTerminator || byteLength <= 0)
                return false;

            ref byte firstUtf8Byte = ref UnsafeUtility.ArrayElementAsRef<byte>(locPtr + offset, 0);
            utf8Bytes = MemoryMarshal.CreateReadOnlySpan(ref firstUtf8Byte, byteLength);
            return true;
        }

        /// <summary>
        /// Counts primary static-data hash aliases that resolve to LocData slices.
        /// </summary>
        public static int GetStaticLocalizationReferenceCount()
        {
            if (!IsLoaded || !TryRefreshArenaReadOnly(out _) || _directory.LocalizationBytes == 0)
                return 0;

            int count = 0;
            H8StaticLocalizationCursor cursor = default;
            while (TryGetNextStaticLocalizationReference(ref cursor, out _))
            {
                if (count == int.MaxValue)
                    return count;

                count++;
            }

            return count;
        }

        /// <summary>
        /// Advances a caller-owned cursor over static-data hash aliases without rescanning prior records.
        /// </summary>
        public static bool TryGetNextStaticLocalizationReference(
            ref H8StaticLocalizationCursor cursor,
            out H8StaticLocalizationReference reference)
        {
            reference = default;
            if (!IsLoaded || !TryRefreshArenaReadOnly(out _) || _directory.LocalizationBytes == 0)
                return false;

            if (cursor.Section < 0)
                cursor.Section = 0;

            if (cursor.RecordIndex < 0)
                cursor.RecordIndex = 0;

            while (cursor.Section < StaticLocalizationSectionCount)
            {
                bool found;
                switch (cursor.Section)
                {
                    case StaticLocalizationItemsSection:
                        found = TryGetNextItemLocalizationReference(ref cursor.RecordIndex, out reference);
                        break;

                    case StaticLocalizationCreaturesSection:
                        found = TryGetNextCreatureLocalizationReference(ref cursor.RecordIndex, out reference);
                        break;

                    case StaticLocalizationBiomesSection:
                        found = TryGetNextBiomeLocalizationReference(ref cursor.RecordIndex, out reference);
                        break;

                    case StaticLocalizationGhostModulesSection:
                        found = TryGetNextGhostModuleLocalizationReference(ref cursor.RecordIndex, out reference);
                        break;

                    case StaticLocalizationSopErrorsSection:
                        found = TryGetNextSopErrorLocalizationReference(ref cursor.RecordIndex, out reference);
                        break;

                    case StaticLocalizationAppliedLoreSection:
                        found = TryGetNextAppliedLoreLocalizationReference(ref cursor.RecordIndex, out reference);
                        break;

                    default:
                        return false;
                }

                if (found)
                    return true;

                cursor.Section++;
                cursor.RecordIndex = 0;
            }

            reference = default;
            return false;
        }

        private static bool TryGetNextItemLocalizationReference(
            ref int recordIndex,
            out H8StaticLocalizationReference reference)
        {
            ReadOnlySpan<H8ItemRecord> records = GetSectionSpan<H8ItemRecord>(H8DataSectionId.Items);
            if (records.Length <= 0)
            {
                reference = default;
                return false;
            }

            while (recordIndex < records.Length)
            {
                int index = recordIndex++;
                if (TryBuildStaticLocalizationReference(records[index].HashId, records[index].NameUtf8Offset, records[index].NameUtf8ByteLength, out reference))
                    return true;
            }

            reference = default;
            return false;
        }

        private static bool TryGetNextCreatureLocalizationReference(
            ref int recordIndex,
            out H8StaticLocalizationReference reference)
        {
            ReadOnlySpan<H8CreatureTraitRecord> records = GetSectionSpan<H8CreatureTraitRecord>(H8DataSectionId.Creatures);
            if (records.Length <= 0)
            {
                reference = default;
                return false;
            }

            while (recordIndex < records.Length)
            {
                int index = recordIndex++;
                if (TryBuildStaticLocalizationReference(records[index].SpeciesHash, records[index].DisplayNameUtf8Offset, records[index].DisplayNameUtf8ByteLength, out reference))
                    return true;
            }

            reference = default;
            return false;
        }

        private static bool TryGetNextBiomeLocalizationReference(
            ref int recordIndex,
            out H8StaticLocalizationReference reference)
        {
            ReadOnlySpan<H8BiomeRecord> records = GetSectionSpan<H8BiomeRecord>(H8DataSectionId.Biomes);
            if (records.Length <= 0)
            {
                reference = default;
                return false;
            }

            while (recordIndex < records.Length)
            {
                int index = recordIndex++;
                if (TryBuildStaticLocalizationReference(records[index].BiomeHash, records[index].DisplayNameUtf8Offset, records[index].DisplayNameUtf8ByteLength, out reference))
                    return true;
            }

            reference = default;
            return false;
        }

        private static bool TryGetNextGhostModuleLocalizationReference(
            ref int recordIndex,
            out H8StaticLocalizationReference reference)
        {
            ReadOnlySpan<H8GhostModuleRecord> records = GetSectionSpan<H8GhostModuleRecord>(H8DataSectionId.GhostModules);
            if (records.Length <= 0)
            {
                reference = default;
                return false;
            }

            while (recordIndex < records.Length)
            {
                int index = recordIndex++;
                if (TryBuildStaticLocalizationReference(records[index].ModuleHash, records[index].DisplayNameUtf8Offset, records[index].DisplayNameUtf8ByteLength, out reference))
                    return true;
            }

            reference = default;
            return false;
        }

        private static bool TryGetNextSopErrorLocalizationReference(
            ref int recordIndex,
            out H8StaticLocalizationReference reference)
        {
            ReadOnlySpan<H8SopErrorRecord> records = GetSectionSpan<H8SopErrorRecord>(H8DataSectionId.SopErrors);
            if (records.Length <= 0)
            {
                reference = default;
                return false;
            }

            while (recordIndex < records.Length)
            {
                int index = recordIndex++;
                if (TryBuildStaticLocalizationReference(records[index].ErrorHash, records[index].MessageUtf8Offset, records[index].MessageUtf8ByteLength, out reference))
                    return true;
            }

            reference = default;
            return false;
        }

        private static bool TryGetNextAppliedLoreLocalizationReference(
            ref int recordIndex,
            out H8StaticLocalizationReference reference)
        {
            ReadOnlySpan<H8AppliedLorePacketRecord> records = GetSectionSpan<H8AppliedLorePacketRecord>(H8DataSectionId.AppliedLorePackets);
            if (records.Length <= 0)
            {
                reference = default;
                return false;
            }

            while (recordIndex < records.Length)
            {
                int index = recordIndex++;
                if (records[index].LocaleHash != DefaultAppliedLoreLocalizationLocaleHash)
                    continue;

                if (TryBuildStaticLocalizationReference(records[index].PacketHash, records[index].TitleUtf8Offset, records[index].TitleUtf8ByteLength, out reference))
                    return true;
            }

            reference = default;
            return false;
        }

        private static bool TryBuildStaticLocalizationReference(
            uint keyHash,
            uint utf8Offset,
            uint byteLength,
            out H8StaticLocalizationReference reference)
        {
            reference = default;
            if (keyHash == 0u ||
                byteLength == 0u ||
                byteLength > int.MaxValue ||
                !TryGetLocalizedUtf8Span(utf8Offset, (int)byteLength, out _))
            {
                return false;
            }

            reference.KeyHash = keyHash;
            reference.Utf8Offset = utf8Offset;
            reference.ByteLength = (int)byteLength;
            return true;
        }

#if UNITY_EDITOR
        /// <summary>
        /// Editor-only live balancing hook. It temporarily opens the write lock, reloads the blob, then restores Ready lock.
        /// </summary>
        public static unsafe bool EditorHotReloadFromFile(string absolutePath, out H8DataBlobLoadStatus status)
        {
            IDataVault reloadVault = _vault;
            if (reloadVault == null)
                reloadVault = GlobalRegistry.DataVault;

            string rollbackSnapshotPath = null;
            bool hasRollbackSnapshot = false;
            if (TryGetResidentBlob(out NativeArray<byte>.ReadOnly previousArena, out int previousBytes) &&
                previousBytes > 0)
            {
                if (!TryWriteEditorHotReloadRollbackSnapshot(previousArena, previousBytes, out rollbackSnapshotPath))
                {
                    status = H8DataBlobLoadStatus.ReadFailed;
                    RecordFailureTelemetry(status, PathFlagManagedFileFallback);
                    if (IsLoaded)
                        LockReady();
                    else
                        Interlocked.Exchange(ref _writeLocked, 0);

                    return false;
                }

                hasRollbackSnapshot = true;
            }

            try
            {
                Interlocked.Exchange(ref _writeLocked, 0);
                bool loaded = TryInitializeFromFile(
                    reloadVault,
                    absolutePath,
                    0u,
                    0u,
                    true,
                    out status);

                if (!loaded && hasRollbackSnapshot && !IsLoaded)
                {
                    Interlocked.Exchange(ref _writeLocked, 0);
                    _ = TryInitializeFromFile(
                        reloadVault,
                        rollbackSnapshotPath,
                        0u,
                        0u,
                        true,
                        PathFlagManagedFileFallback,
                        out _);
                }

                if (IsLoaded)
                    LockReady();
                else
                    Interlocked.Exchange(ref _writeLocked, 0);

                return loaded;
            }
            finally
            {
                TryDeleteFile(rollbackSnapshotPath);
            }
        }

        private static unsafe bool TryWriteEditorHotReloadRollbackSnapshot(
            NativeArray<byte>.ReadOnly arena,
            int blobBytes,
            out string snapshotPath)
        {
            snapshotPath = null;
            if (!arena.IsCreated || blobBytes <= 0 || arena.Length < blobBytes)
                return false;

            string tempPath = null;
            string finalPath = null;
            try
            {
                string cacheDirectory = Path.Combine(Application.temporaryCachePath, "Hecton8", "DataMonolith");
                System.IO.Directory.CreateDirectory(cacheDirectory);
                finalPath = Path.Combine(cacheDirectory, "static_data_hot_reload_rollback.h8bin");
                tempPath = finalPath + ".tmp";

                TryDeleteFile(tempPath);
                byte* source = (byte*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(arena);
                using (FileStream stream = new FileStream(
                           tempPath,
                           FileMode.CreateNew,
                           FileAccess.Write,
                           FileShare.Read,
                           EditorHotReloadSnapshotChunkBytes,
                           FileOptions.SequentialScan | FileOptions.WriteThrough))
                {
                    int copiedBytes = 0;
                    while (copiedBytes < blobBytes)
                    {
                        int chunkBytes = Math.Min(EditorHotReloadSnapshotChunkBytes, blobBytes - copiedBytes);
                        ReadOnlySpan<byte> chunk = new ReadOnlySpan<byte>(source + copiedBytes, chunkBytes);
                        stream.Write(chunk);
                        copiedBytes += chunkBytes;
                    }

                    stream.Flush(true);
                }

                PromoteTempFileCold(tempPath, finalPath);
                snapshotPath = finalPath;
                return true;
            }
            catch (IOException)
            {
                TryDeleteFile(tempPath);
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                TryDeleteFile(tempPath);
                return false;
            }
            catch (ArgumentException)
            {
                TryDeleteFile(tempPath);
                return false;
            }
            catch (NotSupportedException)
            {
                TryDeleteFile(tempPath);
                return false;
            }
            catch (System.Security.SecurityException)
            {
                TryDeleteFile(tempPath);
                return false;
            }
            catch (InvalidOperationException)
            {
                TryDeleteFile(tempPath);
                return false;
            }
        }
#endif

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static unsafe ulong ComputeResidentPayloadHash64()
        {
            if (!TryRefreshArenaReadOnly(out NativeArray<byte>.ReadOnly arena) || arena.Length <= H8DataLayoutConstants.HeaderSizeBytes)
                return 0UL;

            byte* basePtr = (byte*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(arena);
            uint2 hash = xxHash3.Hash64(
                basePtr + H8DataLayoutConstants.HeaderSizeBytes,
                _residentBlobBytes - H8DataLayoutConstants.HeaderSizeBytes);
            return ((ulong)hash.y << 32) | hash.x;
        }

#if !UNITY_WEBGL || UNITY_EDITOR
        private static unsafe bool TryLoadWholeFileIntoArena(string absolutePath, int expectedBytes, uint inheritedPathFlags, out H8DataBlobLoadStatus status)
        {
            status = H8DataBlobLoadStatus.None;
            long readStart = Stopwatch.GetTimestamp();
            uint pathFlags = PathFlagVaultBacked | inheritedPathFlags;
            _lastReadTicks = 0L;
            _lastReadPathFlags = pathFlags;
            try
            {
                if (!TryAcquireArenaWriteView(out NativeArray<byte> arena))
                {
                    status = H8DataBlobLoadStatus.ReadFailed;
                    return false;
                }

                bool readSucceeded = false;
                bool writeLockReleased = false;
                try
                {
                    if (arena.Length < expectedBytes)
                    {
                        status = H8DataBlobLoadStatus.ReadFailed;
                    }
                    else
                    {
                        byte* destination = (byte*)NativeArrayUnsafeUtility.GetUnsafePtr(arena);
#if (UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN) && !UNITY_WEBGL && !UNITY_ANDROID && !UNITY_IOS
                        if (TryReadViaNativeFile(absolutePath, destination, arena.Length, expectedBytes))
                        {
                            long nativeElapsedTicks = Stopwatch.GetTimestamp() - readStart;
                            _lastReadTicks = nativeElapsedTicks;
                            _lastReadPathFlags = pathFlags | PathFlagNativeFile;
                            readSucceeded = true;
                        }
#endif
#if !UNITY_WEBGL && !UNITY_ANDROID && !UNITY_IOS
                        if (!readSucceeded && TryReadViaMemoryMappedFile(absolutePath, destination, arena.Length, expectedBytes))
                        {
                            long mmfElapsedTicks = Stopwatch.GetTimestamp() - readStart;
                            _lastReadTicks = mmfElapsedTicks;
                            _lastReadPathFlags = pathFlags | PathFlagMemoryMappedFile;
                            readSucceeded = true;
                        }
#endif
                        if (!readSucceeded)
                        {
                            using FileStream stream = new FileStream(absolutePath, FileMode.Open, FileAccess.Read, FileShare.Read, 64 * 1024, FileOptions.SequentialScan);
                            Span<byte> destinationBytes = new Span<byte>(destination, expectedBytes);
                            int totalRead = 0;
                            while (totalRead < expectedBytes)
                            {
                                int read = stream.Read(destinationBytes.Slice(totalRead));
                                if (read <= 0)
                                    break;

                                totalRead += read;
                            }

                            readSucceeded = totalRead == expectedBytes && stream.Length == expectedBytes;
                            status = readSucceeded ? H8DataBlobLoadStatus.None : H8DataBlobLoadStatus.ReadFailed;
                            long streamElapsedTicks = Stopwatch.GetTimestamp() - readStart;
                            _lastReadTicks = streamElapsedTicks;
                            _lastReadPathFlags = pathFlags | PathFlagManagedFileFallback;
                        }
                    }
                }
                finally
                {
                    writeLockReleased = ReleaseArenaWriteView();
                }

                if (!writeLockReleased)
                {
                    status = H8DataBlobLoadStatus.ReadFailed;
                    _lastReadTicks = Stopwatch.GetTimestamp() - readStart;
                    _lastReadPathFlags = pathFlags;
                    return false;
                }

                return readSucceeded;
            }
            catch (IOException)
            {
                status = H8DataBlobLoadStatus.ReadFailed;
                _lastReadTicks = Stopwatch.GetTimestamp() - readStart;
                _lastReadPathFlags = pathFlags;
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                status = H8DataBlobLoadStatus.ReadFailed;
                _lastReadTicks = Stopwatch.GetTimestamp() - readStart;
                _lastReadPathFlags = pathFlags;
                return false;
            }
            catch (ArgumentException)
            {
                status = H8DataBlobLoadStatus.ReadFailed;
                _lastReadTicks = Stopwatch.GetTimestamp() - readStart;
                _lastReadPathFlags = pathFlags;
                return false;
            }
            catch (NotSupportedException)
            {
                status = H8DataBlobLoadStatus.ReadFailed;
                _lastReadTicks = Stopwatch.GetTimestamp() - readStart;
                _lastReadPathFlags = pathFlags;
                return false;
            }
            catch (System.Security.SecurityException)
            {
                status = H8DataBlobLoadStatus.ReadFailed;
                _lastReadTicks = Stopwatch.GetTimestamp() - readStart;
                _lastReadPathFlags = pathFlags;
                return false;
            }
            catch (InvalidOperationException)
            {
                status = H8DataBlobLoadStatus.ReadFailed;
                _lastReadTicks = Stopwatch.GetTimestamp() - readStart;
                _lastReadPathFlags = pathFlags;
                return false;
            }
        }

#if (UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN) && !UNITY_WEBGL && !UNITY_ANDROID && !UNITY_IOS
        private static unsafe bool TryReadViaNativeFile(string absolutePath, byte* destination, int destinationBytes, int expectedBytes)
        {
            if (string.IsNullOrEmpty(absolutePath) ||
                destination == null ||
                expectedBytes <= 0 ||
                destinationBytes < expectedBytes)
            {
                return false;
            }

            IntPtr handle = CreateFileW(
                absolutePath,
                NativeGenericRead,
                NativeFileShareRead,
                IntPtr.Zero,
                NativeOpenExisting,
                NativeFileFlagSequentialScan,
                IntPtr.Zero);
            if (handle == IntPtr.Zero || handle == NativeInvalidHandleValue)
                return false;

            try
            {
                int totalRead = 0;
                while (totalRead < expectedBytes)
                {
                    uint chunkBytes = (uint)Math.Min(1024 * 1024, expectedBytes - totalRead);
                    if (!ReadFile(handle, destination + totalRead, chunkBytes, out uint read, IntPtr.Zero) || read == 0u)
                        return false;

                    totalRead += (int)read;
                }

                return totalRead == expectedBytes;
            }
            finally
            {
                CloseHandle(handle);
            }
        }

        [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true, CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
        private static unsafe extern IntPtr CreateFileW(
            string lpFileName,
            uint dwDesiredAccess,
            uint dwShareMode,
            IntPtr lpSecurityAttributes,
            uint dwCreationDisposition,
            uint dwFlagsAndAttributes,
            IntPtr hTemplateFile);

        [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)]
        private static unsafe extern bool ReadFile(
            IntPtr hFile,
            void* lpBuffer,
            uint nNumberOfBytesToRead,
            out uint lpNumberOfBytesRead,
            IntPtr lpOverlapped);

        [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr hObject);
#endif

#if !UNITY_WEBGL && !UNITY_ANDROID && !UNITY_IOS
        private static unsafe bool TryReadViaMemoryMappedFile(string absolutePath, byte* destination, int destinationBytes, int expectedBytes)
        {
            if (destination == null || expectedBytes <= 0)
                return false;

            try
            {
                using MemoryMappedFile mappedFile = MemoryMappedFile.CreateFromFile(absolutePath, FileMode.Open, null, expectedBytes, MemoryMappedFileAccess.Read);
                using MemoryMappedViewAccessor accessor = mappedFile.CreateViewAccessor(0L, expectedBytes, MemoryMappedFileAccess.Read);
                byte* source = null;
                accessor.SafeMemoryMappedViewHandle.AcquirePointer(ref source);
                try
                {
                    source += (int)accessor.PointerOffset;
                    return UnsafeMemoryCopyGuard.TryMemCpy(destination, destinationBytes, source, expectedBytes);
                }
                finally
                {
                    accessor.SafeMemoryMappedViewHandle.ReleasePointer();
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
            catch (ArgumentException)
            {
                return false;
            }
            catch (NotSupportedException)
            {
                return false;
            }
            catch (System.Security.SecurityException)
            {
                return false;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        }
#endif
#endif

        private static bool ExceedsTelemetryDumpThreshold(long ticks)
        {
            return ticks > Stopwatch.Frequency / 20L;
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        private static unsafe bool TryInitializeFromAndroidStreamingAssets(
            IDataVault vault,
            uint expectedWorldSeed,
            uint expectedAppVersionHash,
            bool failIfMissing,
            out H8DataBlobLoadStatus status)
        {
            if (IsWriteLocked && IsLoaded)
            {
                status = H8DataBlobLoadStatus.ReadyLocked;
                return false;
            }

            uint pathFlags = PathFlagVaultBacked | PathFlagAndroidAssetManager | PathFlagAndroidJavaAssetManager;
            long readStart = Stopwatch.GetTimestamp();
            bool arenaReplaced = false;
            status = H8DataBlobLoadStatus.ReadFailed;
            _lastReadTicks = 0L;
            _lastReadPathFlags = pathFlags;
            if (!TryAdoptVaultForLoad(vault))
            {
                SetFailureTelemetry(7u, _arenaHandle.BufferID, _telemetryHandle.BufferID, _telemetryCursorHandle.BufferID);
                RecordFailureTelemetry(status, pathFlags);
                return false;
            }

            int assetNameCapacity = H8DataLayoutConstants.DefaultStreamingAssetsRelativePath.Length + 1;
            byte* assetName = stackalloc byte[assetNameCapacity];
            if (!TryWriteAndroidAssetName(assetName, assetNameCapacity))
            {
                RecordFailureTelemetry(status, pathFlags);
                return false;
            }

            try
            {
                IntPtr unityPlayerClass = IntPtr.Zero;
                IntPtr activity = IntPtr.Zero;
                IntPtr activityClass = IntPtr.Zero;
                IntPtr assetManager = IntPtr.Zero;
                try
                {
                    unityPlayerClass = AndroidJNI.FindClass("com/unity3d/player/UnityPlayer");
                    if (TryConsumePendingAndroidJniException() || unityPlayerClass == IntPtr.Zero)
                    {
                        RecordFailureTelemetry(status, pathFlags);
                        return !failIfMissing && IsLoaded;
                    }

                    IntPtr activityField = AndroidJNI.GetStaticFieldID(
                        unityPlayerClass,
                        "currentActivity",
                        "Landroid/app/Activity;");
                    if (TryConsumePendingAndroidJniException() || activityField == IntPtr.Zero)
                    {
                        RecordFailureTelemetry(status, pathFlags);
                        return !failIfMissing && IsLoaded;
                    }

                    activity = AndroidJNI.GetStaticObjectField(unityPlayerClass, activityField);
                    if (TryConsumePendingAndroidJniException() || activity == IntPtr.Zero)
                    {
                        RecordFailureTelemetry(status, pathFlags);
                        return !failIfMissing && IsLoaded;
                    }

                    activityClass = AndroidJNI.GetObjectClass(activity);
                    if (TryConsumePendingAndroidJniException() || activityClass == IntPtr.Zero)
                    {
                        RecordFailureTelemetry(status, pathFlags);
                        return !failIfMissing && IsLoaded;
                    }

                    IntPtr getAssetsMethod = AndroidJNI.GetMethodID(
                        activityClass,
                        "getAssets",
                        "()Landroid/content/res/AssetManager;");
                    if (TryConsumePendingAndroidJniException() || getAssetsMethod == IntPtr.Zero)
                    {
                        RecordFailureTelemetry(status, pathFlags);
                        return !failIfMissing && IsLoaded;
                    }

                    assetManager = AndroidJNI.CallObjectMethodUnsafe(activity, getAssetsMethod, null);
                    if (TryConsumePendingAndroidJniException() || assetManager == IntPtr.Zero)
                    {
                        RecordFailureTelemetry(status, pathFlags);
                        return !failIfMissing && IsLoaded;
                    }

                    IntPtr javaVm = AndroidJNI.GetJavaVM();
                    if (javaVm == IntPtr.Zero)
                    {
                        RecordFailureTelemetry(status, pathFlags);
                        return !failIfMissing && IsLoaded;
                    }

                    int blobBytes = H8_GetAssetSize(javaVm, assetManager, assetName);
                    if (blobBytes == AndroidAssetMissing)
                    {
                        status = H8DataBlobLoadStatus.Missing;
                        if (failIfMissing)
                            RecordFailureTelemetry(status, pathFlags);

                        return !failIfMissing && IsLoaded;
                    }

                    if (blobBytes == AndroidAssetCompressed)
                    {
                        status = H8DataBlobLoadStatus.ReadFailed;
                        RecordFailureTelemetry(status, pathFlags);
                        return false;
                    }

                    if (blobBytes < H8DataLayoutConstants.HeaderSizeBytes + H8DataLayoutConstants.DirectorySizeBytes)
                    {
                        status = blobBytes >= 0 ? H8DataBlobLoadStatus.FileTooSmall : H8DataBlobLoadStatus.ReadFailed;
                        RecordFailureTelemetry(status, pathFlags);
                        return false;
                    }

                    if (blobBytes > MaxBlobBytes)
                    {
                        status = H8DataBlobLoadStatus.FileTooLarge;
                        RecordFailureTelemetry(status, pathFlags);
                        return false;
                    }

                    IDataVault activeVault = _vault;
                    if (!TryShutdownArenaBeforeReplacement(activeVault, pathFlags, out status))
                        return false;

                    arenaReplaced = true;
                    if (!TryAllocateArena(blobBytes))
                    {
                        status = H8DataBlobLoadStatus.ReadFailed;
                        RecordFailureTelemetry(status, pathFlags);
                        return false;
                    }

                    if (!TryAcquireArenaWriteView(out NativeArray<byte> arena))
                    {
                        status = H8DataBlobLoadStatus.ReadFailed;
                        RecordFailureTelemetry(status, pathFlags);
                        ShutdownArenaOnly();
                        return false;
                    }

                    bool loaded = false;
                    bool writeLockReleased = false;
                    try
                    {
                        if (arena.Length >= blobBytes)
                        {
                            byte* destination = (byte*)NativeArrayUnsafeUtility.GetUnsafePtr(arena);
                            loaded = H8_LoadAssetToPointer(javaVm, assetManager, assetName, destination, blobBytes);
                        }
                    }
                    finally
                    {
                        writeLockReleased = ReleaseArenaWriteView();
                    }

                    if (!loaded || !writeLockReleased)
                    {
                        status = H8DataBlobLoadStatus.ReadFailed;
                        _lastReadTicks = Stopwatch.GetTimestamp() - readStart;
                        _lastReadPathFlags = pathFlags;
                        RecordTelemetry(status, _lastReadTicks, _lastReadTicks, _lastReadPathFlags);
                        DumpTelemetry(status);
                        ShutdownArenaOnly();
                        return false;
                    }

                    _residentBlobBytes = blobBytes;
                    _lastReadTicks = Stopwatch.GetTimestamp() - readStart;
                    _lastReadPathFlags = pathFlags;
                    if (!TryValidateResidentArena(expectedWorldSeed, expectedAppVersionHash, out status))
                    {
                        RecordTelemetry(status, _lastReadTicks, _lastReadTicks, _lastReadPathFlags);
                        DumpTelemetry(status);
                        ShutdownArenaOnly();
                        return false;
                    }

                    Volatile.Write(ref _loaded, 1);
                    LockReady();
                    status = H8DataBlobLoadStatus.Loaded;
                    RecordTelemetry(status, _lastReadTicks, _lastReadTicks, _lastReadPathFlags);
                    if (ExceedsTelemetryDumpThreshold(_lastReadTicks))
                        DumpTelemetry(status);

                    return true;
                }
                finally
                {
                    if (assetManager != IntPtr.Zero)
                        AndroidJNI.DeleteLocalRef(assetManager);

                    if (activityClass != IntPtr.Zero)
                        AndroidJNI.DeleteLocalRef(activityClass);

                    if (activity != IntPtr.Zero)
                        AndroidJNI.DeleteLocalRef(activity);

                    if (unityPlayerClass != IntPtr.Zero)
                        AndroidJNI.DeleteLocalRef(unityPlayerClass);
                }
            }
            catch (AndroidJavaException)
            {
                status = H8DataBlobLoadStatus.ReadFailed;
            }
            catch (DllNotFoundException)
            {
                status = H8DataBlobLoadStatus.ReadFailed;
            }
            catch (EntryPointNotFoundException)
            {
                status = H8DataBlobLoadStatus.ReadFailed;
            }
            catch (InvalidOperationException)
            {
                status = H8DataBlobLoadStatus.ReadFailed;
            }

            _lastReadTicks = Stopwatch.GetTimestamp() - readStart;
            _lastReadPathFlags = pathFlags;
            RecordTelemetry(status, _lastReadTicks, _lastReadTicks, _lastReadPathFlags);
            DumpTelemetry(status);
            if (arenaReplaced)
                ShutdownArenaOnly();

            return false;
        }

        private static bool TryConsumePendingAndroidJniException()
        {
            IntPtr exception = AndroidJNI.ExceptionOccurred();
            if (exception == IntPtr.Zero)
                return false;

            AndroidJNI.ExceptionClear();
            AndroidJNI.DeleteLocalRef(exception);
            return true;
        }

        private static unsafe bool TryWriteAndroidAssetName(byte* destination, int capacity)
        {
            ReadOnlySpan<char> relativePath = H8DataLayoutConstants.DefaultStreamingAssetsRelativePath.AsSpan();
            if (destination == null || capacity <= relativePath.Length)
                return false;

            for (int i = 0; i < relativePath.Length; i++)
            {
                char c = relativePath[i];
                if (c > 0x7F)
                    return false;

                destination[i] = (byte)c;
            }

            destination[relativePath.Length] = 0;
            return true;
        }

        [DllImport("__Internal", EntryPoint = "H8_GetAssetSize", CallingConvention = CallingConvention.Cdecl)]
        private static unsafe extern int H8_GetAssetSize(IntPtr javaVm, IntPtr assetManager, byte* filename);

        [DllImport("__Internal", EntryPoint = "H8_LoadAssetToPointer", CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        private static unsafe extern bool H8_LoadAssetToPointer(IntPtr javaVm, IntPtr assetManager, byte* filename, void* destinationBuffer, int bufferSize);

        [DllImport("__Internal", EntryPoint = "H8_WriteTelemetryDump", CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        private static unsafe extern bool H8_WriteTelemetryDump(
            byte* persistentDataPath,
            void* telemetryEntries,
            int entryCount,
            int entrySize,
            uint status,
            int cursor);
#endif

#if !UNITY_EDITOR && !DEVELOPMENT_BUILD && UNITY_STANDALONE_WIN && !UNITY_WEBGL && !UNITY_ANDROID && !UNITY_IOS
        private static unsafe bool TryInitializeFromWindowsPlayerStreamingAssets(
            IDataVault vault,
            uint expectedWorldSeed,
            uint expectedAppVersionHash,
            bool failIfMissing,
            out H8DataBlobLoadStatus status)
        {
            if (IsWriteLocked && IsLoaded)
            {
                status = H8DataBlobLoadStatus.ReadyLocked;
                return false;
            }

            if (!TryAdoptVaultForLoad(vault))
            {
                status = H8DataBlobLoadStatus.ReadFailed;
                SetFailureTelemetry(7u, _arenaHandle.BufferID, _telemetryHandle.BufferID, _telemetryCursorHandle.BufferID);
                RecordFailureTelemetry(status, 0u);
                return false;
            }

            char* path = stackalloc char[NativePathCapacity];
            if (!TryBuildWindowsPlayerMonolithPath(path, NativePathCapacity))
            {
                status = H8DataBlobLoadStatus.Missing;
                if (failIfMissing)
                    RecordFailureTelemetry(status, 0u);

                return !failIfMissing && IsLoaded;
            }

            if (!TryGetNativeFileSize(path, out long blobLength))
            {
                status = H8DataBlobLoadStatus.Missing;
                if (failIfMissing)
                    RecordFailureTelemetry(status, 0u);

                return !failIfMissing && IsLoaded;
            }

            if (blobLength < H8DataLayoutConstants.HeaderSizeBytes + H8DataLayoutConstants.DirectorySizeBytes)
            {
                status = H8DataBlobLoadStatus.FileTooSmall;
                RecordFailureTelemetry(status, 0u);
                return false;
            }

            if (blobLength > MaxBlobBytes || blobLength > int.MaxValue)
            {
                status = H8DataBlobLoadStatus.FileTooLarge;
                RecordFailureTelemetry(status, 0u);
                return false;
            }

            int blobBytes = (int)blobLength;
            IDataVault activeVault = _vault;
            if (!TryShutdownArenaBeforeReplacement(activeVault, 0u, out status))
                return false;

            if (!TryAllocateArena(blobBytes))
            {
                status = H8DataBlobLoadStatus.ReadFailed;
                RecordFailureTelemetry(status, 0u);
                return false;
            }

            if (!TryLoadWholeNativeFileIntoArena(path, blobBytes, out status))
            {
                RecordTelemetry(status, _lastReadTicks, _lastReadTicks, _lastReadPathFlags);
                DumpTelemetry(status);
                ShutdownArenaOnly();
                return false;
            }

            _residentBlobBytes = blobBytes;
            if (!TryValidateResidentArena(expectedWorldSeed, expectedAppVersionHash, out status))
            {
                RecordTelemetry(status, _lastReadTicks, _lastReadTicks, _lastReadPathFlags);
                DumpTelemetry(status);
                ShutdownArenaOnly();
                return false;
            }

            Volatile.Write(ref _loaded, 1);
            LockReady();
            status = H8DataBlobLoadStatus.Loaded;
            RecordTelemetry(status, _lastReadTicks, _lastReadTicks, _lastReadPathFlags);
            if (ExceedsTelemetryDumpThreshold(_lastReadTicks))
                DumpTelemetry(status);

            return true;
        }

        private static unsafe bool TryBuildWindowsPlayerMonolithPath(char* buffer, int capacity)
        {
            if (buffer == null || capacity <= 0)
                return false;

            uint length = GetModuleFileNameW(IntPtr.Zero, buffer, (uint)capacity);
            if (length == 0u || length >= (uint)capacity)
                return false;

            int charCount = (int)length;
            int lastSlash = -1;
            for (int i = 0; i < charCount; i++)
            {
                char c = buffer[i];
                if (c == '\\' || c == '/')
                    lastSlash = i;
            }

            int stemStart = lastSlash + 1;
            int stemEnd = charCount;
            if (stemEnd - stemStart > 4 &&
                buffer[stemEnd - 4] == '.' &&
                IsAsciiEqualIgnoreCase(buffer[stemEnd - 3], 'e') &&
                IsAsciiEqualIgnoreCase(buffer[stemEnd - 2], 'x') &&
                IsAsciiEqualIgnoreCase(buffer[stemEnd - 1], 'e'))
            {
                stemEnd -= 4;
            }

            int stemLength = stemEnd - stemStart;
            if (stemLength <= 0)
                return false;

            int write = stemStart;
            for (int i = 0; i < stemLength; i++)
                buffer[write++] = buffer[stemStart + i];

            if (!AppendLiteral(buffer, capacity, ref write, "_Data\\StreamingAssets\\Hecton8\\DataMonolith\\static_data.h8bin"))
                return false;

            buffer[write] = '\0';
            return true;
        }

        private static bool IsAsciiEqualIgnoreCase(char value, char expectedLower)
        {
            if (value >= 'A' && value <= 'Z')
                value = (char)(value + ('a' - 'A'));

            return value == expectedLower;
        }

        private static unsafe bool AppendLiteral(char* buffer, int capacity, ref int write, ReadOnlySpan<char> literal)
        {
            if (literal.IsEmpty || write < 0 || write + literal.Length >= capacity)
                return false;

            for (int i = 0; i < literal.Length; i++)
                buffer[write++] = literal[i];

            return true;
        }

        private static unsafe bool TryGetNativeFileSize(char* path, out long byteLength)
        {
            byteLength = 0L;
            IntPtr handle = CreateFileWNative(
                path,
                NativeGenericRead,
                NativeFileShareRead,
                IntPtr.Zero,
                NativeOpenExisting,
                NativeFileFlagSequentialScan,
                IntPtr.Zero);
            if (handle == IntPtr.Zero || handle == NativeInvalidHandleValue)
                return false;

            bool ok = GetFileSizeEx(handle, out byteLength);
            CloseHandleNative(handle);
            return ok && byteLength >= 0L;
        }

        private static unsafe bool TryLoadWholeNativeFileIntoArena(char* path, int expectedBytes, out H8DataBlobLoadStatus status)
        {
            status = H8DataBlobLoadStatus.None;
            long readStart = Stopwatch.GetTimestamp();
            uint pathFlags = PathFlagVaultBacked | PathFlagNativeFile;
            _lastReadTicks = 0L;
            _lastReadPathFlags = pathFlags;
            if (!TryAcquireArenaWriteView(out NativeArray<byte> arena))
            {
                status = H8DataBlobLoadStatus.ReadFailed;
                SetFailureTelemetry(3u, _arenaHandle.BufferID, _arenaHandle.Generation, _arenaHandle.SystemID);
                return false;
            }

            bool ok = false;
            bool writeLockReleased = false;
            try
            {
                if (arena.Length < expectedBytes)
                {
                    status = H8DataBlobLoadStatus.ReadFailed;
                }
                else
                {
                    byte* destination = (byte*)NativeArrayUnsafeUtility.GetUnsafePtr(arena);
                    IntPtr handle = CreateFileWNative(
                        path,
                        NativeGenericRead,
                        NativeFileShareRead,
                        IntPtr.Zero,
                        NativeOpenExisting,
                        NativeFileFlagSequentialScan,
                        IntPtr.Zero);
                    if (handle == IntPtr.Zero || handle == NativeInvalidHandleValue)
                    {
                        status = H8DataBlobLoadStatus.ReadFailed;
                        _lastReadTicks = Stopwatch.GetTimestamp() - readStart;
                    }
                    else
                    {
                        try
                        {
                            int totalRead = 0;
                            while (totalRead < expectedBytes)
                            {
                                uint chunkBytes = (uint)Math.Min(1024 * 1024, expectedBytes - totalRead);
                                if (!ReadFileNative(handle, destination + totalRead, chunkBytes, out uint read, IntPtr.Zero) || read == 0u)
                                    break;

                                totalRead += (int)read;
                            }

                            ok = totalRead == expectedBytes;
                            status = ok ? H8DataBlobLoadStatus.None : H8DataBlobLoadStatus.ReadFailed;
                            _lastReadTicks = Stopwatch.GetTimestamp() - readStart;
                            _lastReadPathFlags = pathFlags;
                        }
                        finally
                        {
                            CloseHandleNative(handle);
                        }
                    }
                }
            }
            finally
            {
                writeLockReleased = ReleaseArenaWriteView();
            }

            if (!writeLockReleased)
            {
                status = H8DataBlobLoadStatus.ReadFailed;
                SetFailureTelemetry(4u, ok ? 1u : 0u, _arenaHandle.BufferID, _arenaHandle.Generation);
                _lastReadTicks = Stopwatch.GetTimestamp() - readStart;
                _lastReadPathFlags = pathFlags;
                return false;
            }

            return ok;
        }

        [DllImport("kernel32.dll", EntryPoint = "GetModuleFileNameW", SetLastError = true, CharSet = CharSet.Unicode)]
        private static unsafe extern uint GetModuleFileNameW(IntPtr hModule, char* lpFilename, uint nSize);

        [DllImport("kernel32.dll", EntryPoint = "CreateFileW", SetLastError = true, CharSet = CharSet.Unicode)]
        private static unsafe extern IntPtr CreateFileWNative(
            char* lpFileName,
            uint dwDesiredAccess,
            uint dwShareMode,
            IntPtr lpSecurityAttributes,
            uint dwCreationDisposition,
            uint dwFlagsAndAttributes,
            IntPtr hTemplateFile);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetFileSizeEx(IntPtr hFile, out long lpFileSize);

        [DllImport("kernel32.dll", EntryPoint = "ReadFile", SetLastError = true)]
        private static unsafe extern bool ReadFileNative(
            IntPtr hFile,
            void* lpBuffer,
            uint nNumberOfBytesToRead,
            out uint lpNumberOfBytesRead,
            IntPtr lpOverlapped);

        [DllImport("kernel32.dll", EntryPoint = "WriteFile", SetLastError = true)]
        private static unsafe extern bool WriteFileNative(
            IntPtr hFile,
            void* lpBuffer,
            uint nNumberOfBytesToWrite,
            out uint lpNumberOfBytesWritten,
            IntPtr lpOverlapped);

        [DllImport("kernel32.dll", EntryPoint = "CreateDirectoryW", SetLastError = true, CharSet = CharSet.Unicode)]
        private static unsafe extern bool CreateDirectoryWNative(char* lpPathName, IntPtr lpSecurityAttributes);

        [DllImport("kernel32.dll", EntryPoint = "GetCurrentDirectoryW", SetLastError = true, CharSet = CharSet.Unicode)]
        private static unsafe extern uint GetCurrentDirectoryW(uint nBufferLength, char* lpBuffer);

        [DllImport("kernel32.dll", EntryPoint = "CloseHandle", SetLastError = true)]
        private static extern bool CloseHandleNative(IntPtr hObject);
#endif

        private static unsafe bool TryValidateResidentArena(
            uint expectedWorldSeed,
            uint expectedAppVersionHash,
            out H8DataBlobLoadStatus status)
        {
            status = H8DataBlobLoadStatus.None;
            if (!BitConverter.IsLittleEndian)
            {
                status = H8DataBlobLoadStatus.HeaderMismatch;
                return false;
            }

            if (!H8DataLayoutAudit.ValidateBlittableSizes() ||
                !H8AppliedLoreRuntime.ValidateRuntimeLayout())
            {
                status = H8DataBlobLoadStatus.HeaderMismatch;
                return false;
            }

            if (!TryRefreshArenaReadOnly(out NativeArray<byte>.ReadOnly arena) || _residentBlobBytes < H8DataLayoutConstants.HeaderSizeBytes + H8DataLayoutConstants.DirectorySizeBytes)
            {
                status = H8DataBlobLoadStatus.FileTooSmall;
                return false;
            }

            byte* basePtr = (byte*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(arena);
            _header = UnsafeUtility.ReadArrayElement<H8DataBlobHeader>(basePtr, 0);
            _directory = UnsafeUtility.ReadArrayElement<H8DataBlobDirectory>(basePtr + H8DataLayoutConstants.HeaderSizeBytes, 0);

            if (_header.Magic != H8DataLayoutConstants.BlobMagic ||
                _directory.Magic != H8DataLayoutConstants.BlobMagic)
            {
                status = H8DataBlobLoadStatus.BadMagic;
                return false;
            }

            if (_header.FormatVersion != H8DataLayoutConstants.FormatVersion ||
                _directory.FormatVersion != H8DataLayoutConstants.FormatVersion)
            {
                status = H8DataBlobLoadStatus.UnsupportedVersion;
                return false;
            }

            if (_header.HeaderBytes != H8DataLayoutConstants.HeaderSizeMarker)
            {
                status = H8DataBlobLoadStatus.HeaderMismatch;
                return false;
            }

            if (_header.BlobBytes != _residentBlobBytes ||
                _header.DirectoryOffset != H8DataLayoutConstants.HeaderSizeBytes ||
                _header.DirectoryBytes != H8DataLayoutConstants.DirectorySizeBytes ||
                _header.SectionTableOffset != H8DataLayoutConstants.HeaderSizeBytes + H8DataLayoutConstants.DirectorySizeBytes ||
                _header.SectionTableOffset != _directory.SectionTableOffset ||
                _header.SectionCount != _directory.SectionCount ||
                _header.Flags != H8DataLayoutConstants.BlobFlagLittleEndian ||
                _header.Flags != _directory.Flags ||
                _header.WorldSeed != _directory.WorldSeed ||
                _header.AppVersionHash != _directory.AppVersionHash ||
                _header.SchemaHash != H8DataLayoutConstants.SchemaHash ||
                _header.Reserved0 != 0u ||
                _header.Reserved1 != 0u ||
                _header.Reserved2 != 0u)
            {
                status = H8DataBlobLoadStatus.HeaderMismatch;
                return false;
            }

            if (_directory.BlobBytes != _residentBlobBytes)
            {
                status = H8DataBlobLoadStatus.FileTooSmall;
                return false;
            }

            if (expectedWorldSeed != 0u && _directory.WorldSeed != 0u && _directory.WorldSeed != expectedWorldSeed)
            {
                status = H8DataBlobLoadStatus.HeaderMismatch;
                return false;
            }

            if (expectedAppVersionHash != 0u && _directory.AppVersionHash != 0u && _directory.AppVersionHash != expectedAppVersionHash)
            {
                status = H8DataBlobLoadStatus.HeaderMismatch;
                return false;
            }

            ulong computedHash = ComputeResidentPayloadHash64();
            if (computedHash != _header.Checksum64)
            {
                status = H8DataBlobLoadStatus.BadChecksum;
                return false;
            }

            if (!IsDirectoryValid())
            {
                status = H8DataBlobLoadStatus.InvalidSectionTable;
                return false;
            }

            if (!IsAppliedLoreContractValid())
            {
                status = H8DataBlobLoadStatus.InvalidSectionTable;
                return false;
            }

            status = H8DataBlobLoadStatus.Loaded;
            return true;
        }

        private static unsafe bool IsDirectoryValid()
        {
            const ushort ExpectedSectionCount = (ushort)H8DataSectionId.AppliedLoreRoutes;
            if (_directory.SectionCount != ExpectedSectionCount)
                return false;

            if (_directory.SectionTableOffset != H8DataLayoutConstants.HeaderSizeBytes + H8DataLayoutConstants.DirectorySizeBytes)
                return false;

            if (_directory.Reserved0 != 0u ||
                _directory.Reserved1 != 0u ||
                _directory.Reserved2 != 0u ||
                _directory.Reserved3 != 0u ||
                _directory.Reserved4 != 0u)
            {
                return false;
            }

            if (_directory.SectionTableBytes != (uint)(_directory.SectionCount * UnsafeUtility.SizeOf<H8DataSectionEntry>()))
                return false;

            if (_directory.SectionTableOffset > _directory.BlobBytes ||
                _directory.SectionTableBytes > _directory.BlobBytes - _directory.SectionTableOffset)
            {
                return false;
            }

            uint expectedDataStart = AlignUp(_directory.SectionTableOffset + _directory.SectionTableBytes, (uint)H8DataLayoutConstants.SectionAlignmentBytes);
            if (_directory.DataStartOffset != expectedDataStart ||
                (_directory.DataStartOffset & (H8DataLayoutConstants.SectionAlignmentBytes - 1u)) != 0u)
            {
                return false;
            }

            if (_directory.LocalizationBytes > 0u &&
                (_directory.LocalizationOffset > _directory.BlobBytes ||
                 _directory.LocalizationBytes > _directory.BlobBytes - _directory.LocalizationOffset))
            {
                return false;
            }

            if (!TryRefreshArenaReadOnly(out NativeArray<byte>.ReadOnly arena))
                return false;

            byte* basePtr = (byte*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(arena);
            H8DataSectionEntry* entries = (H8DataSectionEntry*)(basePtr + _directory.SectionTableOffset);
            bool sawLocalization = false;
            ulong expectedSectionOffset = _directory.DataStartOffset;
            for (int i = 0; i < _directory.SectionCount; i++)
            {
                H8DataSectionEntry section = entries[i];
                uint expectedSectionId = (uint)(i + 1);
                if (section.SectionId != expectedSectionId)
                    return false;

                H8DataSectionId sectionId = (H8DataSectionId)section.SectionId;
                uint expectedRecordSize = H8DataLayoutAudit.GetExpectedRecordSize(sectionId);
                if (expectedRecordSize == 0u || section.RecordSize != expectedRecordSize)
                    return false;

                if (!IsSectionRangeValid(in section))
                    return false;

                if (section.Count != 0u)
                {
                    ulong byteCount = (ulong)section.RecordSize * section.Count;
                    if ((ulong)section.OffsetBytes != expectedSectionOffset)
                        return false;

                    expectedSectionOffset = AlignUp((ulong)section.OffsetBytes + byteCount, (uint)H8DataLayoutConstants.SectionAlignmentBytes);
                    if (expectedSectionOffset > (ulong)_directory.BlobBytes + (uint)H8DataLayoutConstants.SectionAlignmentBytes)
                        return false;
                }

                if (sectionId == H8DataSectionId.LocalizationUtf8)
                {
                    sawLocalization = true;
                    if (_directory.LocalizationOffset != section.OffsetBytes ||
                        _directory.LocalizationBytes != section.Count)
                    {
                        return false;
                    }
                }
            }

            return _directory.LocalizationBytes == 0u || sawLocalization;
        }

        private static bool IsAppliedLoreContractValid()
        {
            if (!TryRefreshArenaReadOnly(out NativeArray<byte>.ReadOnly arena))
                return false;

            if (!TryGetSectionSpanInArena(arena, H8DataSectionId.AppliedLorePackets, out ReadOnlySpan<H8AppliedLorePacketRecord> packets) ||
                !TryGetSectionSpanInArena(arena, H8DataSectionId.AppliedLoreRoutes, out ReadOnlySpan<H8AppliedLoreRouteRecord> routes))
            {
                return false;
            }

            if (packets.Length <= 0 || routes.Length <= 0)
                return false;

            H8AppliedLorePacketRecord previousPacket = default;
            for (int i = 0; i < packets.Length; i++)
            {
                H8AppliedLorePacketRecord packet = packets[i];
                if (packet.PacketHash == 0u ||
                    packet.LocaleHash == 0u ||
                    !AreAppliedLorePacketTextRangesValid(in packet))
                {
                    return false;
                }

                if (i > 0 &&
                    CompareAppliedLoreKey(
                        previousPacket.PacketHash,
                        previousPacket.LocaleHash,
                        packet.PacketHash,
                        packet.LocaleHash) >= 0)
                {
                    return false;
                }

                previousPacket = packet;
            }

            uint previousRouteHash = 0u;
            for (int i = 0; i < routes.Length; i++)
            {
                H8AppliedLoreRouteRecord route = routes[i];
                if (route.RouteCardHash == 0u ||
                    route.RouteCardHash <= previousRouteHash ||
                    route.PacketCount > H8DataLayoutConstants.AppliedLoreRoutePacketCapacity ||
                    route.RequiredPacketCount > H8DataLayoutConstants.AppliedLoreRoutePrerequisiteCapacity ||
                    !AreAppliedLoreRouteHashesValid(in route))
                {
                    return false;
                }

                previousRouteHash = route.RouteCardHash;
            }

            return true;
        }

        private static bool AreAppliedLorePacketTextRangesValid(in H8AppliedLorePacketRecord packet)
        {
            return IsLocalizedRangeValid(packet.TitleUtf8Offset, packet.TitleUtf8ByteLength) &&
                   IsLocalizedRangeValid(packet.ScannerUtf8Offset, packet.ScannerUtf8ByteLength) &&
                   IsLocalizedRangeValid(packet.TerminalUtf8Offset, packet.TerminalUtf8ByteLength) &&
                   IsLocalizedRangeValid(packet.AudioUtf8Offset, packet.AudioUtf8ByteLength) &&
                   IsLocalizedRangeValid(packet.WikiUtf8Offset, packet.WikiUtf8ByteLength) &&
                   IsLocalizedRangeValid(packet.SiteUtf8Offset, packet.SiteUtf8ByteLength) &&
                   IsLocalizedRangeValid(packet.FieldNoteUtf8Offset, packet.FieldNoteUtf8ByteLength);
        }

        private static bool IsLocalizedRangeValid(uint utf8Offset, uint byteLength)
        {
            if (byteLength == 0u)
                return true;

            if (utf8Offset == MissingUtf8Offset ||
                byteLength > int.MaxValue ||
                _directory.LocalizationBytes == 0u ||
                utf8Offset >= _directory.LocalizationBytes)
            {
                return false;
            }

            return byteLength <= _directory.LocalizationBytes - utf8Offset;
        }

        private static bool AreAppliedLoreRouteHashesValid(in H8AppliedLoreRouteRecord route)
        {
            uint packetCount = math.min(route.PacketCount, (uint)H8DataLayoutConstants.AppliedLoreRoutePacketCapacity);
            for (uint i = 0u; i < packetCount; i++)
            {
                if (GetAppliedLoreRoutePacketHash(in route, i) == 0u)
                    return false;
            }

            uint requiredCount = math.min(route.RequiredPacketCount, (uint)H8DataLayoutConstants.AppliedLoreRoutePrerequisiteCapacity);
            for (uint i = 0u; i < requiredCount; i++)
            {
                if (GetAppliedLoreRouteRequiredPacketHash(in route, i) == 0u)
                    return false;
            }

            return true;
        }

        private static bool IsSectionRangeValid(in H8DataSectionEntry section)
        {
            if (section.RecordSize == 0u)
                return section.Count == 0u && section.OffsetBytes == 0u;

            if (section.Count == 0u)
                return section.OffsetBytes == 0u;

            if ((section.OffsetBytes & (H8DataLayoutConstants.SectionAlignmentBytes - 1u)) != 0u)
                return false;

            ulong byteCount = (ulong)section.RecordSize * section.Count;
            return section.OffsetBytes >= _directory.DataStartOffset &&
                   section.OffsetBytes <= _directory.BlobBytes &&
                   byteCount <= _directory.BlobBytes - section.OffsetBytes;
        }

        private static bool TryAllocateArena(int blobBytes)
        {
            int capacity = ComputeArenaCapacity(blobBytes);
            IDataVault vault = _vault;
            if (_arenaHandle.BufferID != 0u)
            {
                if (vault == null || !ReleaseVaultHandle(vault, ref _arenaHandle))
                    return false;
            }

            if (vault != null)
            {
                _arenaHandle = vault.EnsureGenerationHandle<byte>(
                    DataMonolithPayloadBufferId,
                    capacity,
                    SystemID.CoreDataVault,
                    NativeArrayOptions.UninitializedMemory);

                if (vault.TryReadOnlyHandle(in _arenaHandle, out NativeArray<byte>.ReadOnly arena) &&
                    arena.IsCreated &&
                    arena.Length >= capacity)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TryAdoptVaultForLoad(IDataVault vault)
        {
            if (vault != null &&
                ((_vault == null && HasResidentHandles()) ||
                 (_vault != null && !ReferenceEquals(vault, _vault))))
            {
                if (_vault != null && !ShutdownArenaOnly())
                    return false;

                ClearResidentHandlesAfterVaultSwitch();
            }

            if (vault != null || !IsLoaded)
                _vault = vault;

            return true;
        }

        private static bool TryShutdownArenaBeforeReplacement(
            IDataVault activeVault,
            uint pathFlags,
            out H8DataBlobLoadStatus status)
        {
            if (ShutdownArenaOnly())
            {
                _vault = activeVault;
                status = H8DataBlobLoadStatus.None;
                return true;
            }

            _vault = activeVault;
            status = H8DataBlobLoadStatus.ReadFailed;
            SetFailureTelemetry(8u, _arenaHandle.BufferID, _telemetryHandle.BufferID, _telemetryCursorHandle.BufferID);
            RecordFailureTelemetry(status, pathFlags);
            return false;
        }

        private static bool HasResidentHandles()
        {
            return _arenaHandle.BufferID != 0u ||
                   _telemetryHandle.BufferID != 0u ||
                   _telemetryCursorHandle.BufferID != 0u;
        }

        private static void ClearResidentHandlesAfterVaultSwitch()
        {
            Volatile.Write(ref _loaded, 0);
            Volatile.Write(ref _writeLocked, 0);
            _arenaHandle = default;
            _telemetryHandle = default;
            _telemetryCursorHandle = default;
            _header = default;
            _directory = default;
            _residentBlobBytes = 0;
            _vault = null;
        }

        private static bool TryAcquireArenaWriteView(out NativeArray<byte> arena)
        {
            arena = default;
            IDataVault vault = _vault;
            if (vault == null ||
                _arenaHandle.BufferID == 0u ||
                !vault.TryAcquireWriteLock(in _arenaHandle, SystemID.CoreDataVault, out arena))
            {
                return false;
            }

            bool lockTransferred = false;
            try
            {
                if (!arena.IsCreated)
                    return false;

                lockTransferred = true;
                return true;
            }
            finally
            {
                if (!lockTransferred)
                {
                    ReleaseWriteLockWithRetry(vault, in _arenaHandle, SystemID.CoreDataVault);
                    arena = default;
                }
            }
        }

        private static bool ReleaseArenaWriteView()
        {
            IDataVault vault = _vault;
            if (vault != null && _arenaHandle.BufferID != 0u)
                return ReleaseWriteLockWithRetry(vault, in _arenaHandle, SystemID.CoreDataVault);

            return false;
        }

        private static bool ReleaseWriteLockWithRetry<T>(
            IDataVault vault,
            in VaultGenerationHandle<T> handle,
            SystemID owner) where T : struct
        {
            if (vault == null || handle.BufferID == 0u || owner == SystemID.Unknown)
                return false;

            for (int attempt = 0; attempt < DataMonolithWriterReleaseRetryCount; attempt++)
            {
                if (vault.ReleaseWriteLock(in handle, owner))
                    return true;

                Thread.Yield();
            }

            return false;
        }

        private static bool TryRefreshArenaReadOnly(out NativeArray<byte>.ReadOnly arena)
        {
            arena = default;
            IDataVault vault = _vault;
            if (vault == null || _arenaHandle.BufferID == 0u)
                return false;

            return vault.TryReadOnlyHandle(in _arenaHandle, out arena) && arena.IsCreated;
        }

        private static bool EnsureTelemetry()
        {
            IDataVault vault = _vault;
            if (vault == null)
                return false;

            if (_telemetryHandle.BufferID == 0u ||
                !vault.TryReadOnlyHandle(in _telemetryHandle, out NativeArray<H8DataMonolithTelemetryEntry>.ReadOnly ring) ||
                !ring.IsCreated ||
                ring.Length < H8DataLayoutConstants.TelemetryRingCapacity)
            {
                _telemetryHandle = vault.EnsureGenerationHandle<H8DataMonolithTelemetryEntry>(
                    DataMonolithTelemetryRingBufferId,
                    H8DataLayoutConstants.TelemetryRingCapacity,
                    SystemID.CoreDataVault,
                    NativeArrayOptions.ClearMemory);
            }

            if (_telemetryCursorHandle.BufferID == 0u ||
                !vault.TryReadOnlyHandle(in _telemetryCursorHandle, out NativeArray<int>.ReadOnly cursor) ||
                !cursor.IsCreated ||
                cursor.Length < 1)
            {
                _telemetryCursorHandle = vault.EnsureGenerationHandle<int>(
                    DataMonolithTelemetryCursorBufferId,
                    1,
                    SystemID.CoreDataVault,
                    NativeArrayOptions.ClearMemory);
            }

            return TryReadTelemetry(out _, out _);
        }

        private static bool TryReadTelemetry(
            out NativeArray<H8DataMonolithTelemetryEntry>.ReadOnly ring,
            out NativeArray<int>.ReadOnly cursor)
        {
            ring = default;
            cursor = default;
            IDataVault vault = _vault;
            return vault != null &&
                   _telemetryHandle.BufferID != 0u &&
                   _telemetryCursorHandle.BufferID != 0u &&
                   vault.TryReadOnlyHandle(in _telemetryHandle, out ring) &&
                   ring.IsCreated &&
                   ring.Length >= H8DataLayoutConstants.TelemetryRingCapacity &&
                   vault.TryReadOnlyHandle(in _telemetryCursorHandle, out cursor) &&
                   cursor.IsCreated &&
                   cursor.Length >= 1;
        }

        private static bool TryReserveTelemetrySlot(out int index)
        {
            index = 0;
            IDataVault vault = _vault;
            if (vault == null ||
                _telemetryCursorHandle.BufferID == 0u ||
                !vault.TryAcquireWriteLock(in _telemetryCursorHandle, SystemID.CoreDataVault, out NativeArray<int> cursor))
            {
                return false;
            }

            bool reserved = false;
            try
            {
                if (cursor.IsCreated && cursor.Length >= 1)
                {
                    index = cursor[0];
                    if ((uint)index >= H8DataLayoutConstants.TelemetryRingCapacity)
                        index = 0;

                    int next = index + 1;
                    if (next >= H8DataLayoutConstants.TelemetryRingCapacity)
                        next = 0;

                    cursor[0] = next;
                    reserved = true;
                }
            }
            finally
            {
                if (!ReleaseWriteLockWithRetry(vault, in _telemetryCursorHandle, SystemID.CoreDataVault))
                    reserved = false;
            }

            return reserved;
        }

        private static bool TryWriteTelemetryEntry(int index, in H8DataMonolithTelemetryEntry entry)
        {
            IDataVault vault = _vault;
            if (vault == null ||
                _telemetryHandle.BufferID == 0u ||
                (uint)index >= H8DataLayoutConstants.TelemetryRingCapacity ||
                !vault.TryAcquireWriteLock(in _telemetryHandle, SystemID.CoreDataVault, out NativeArray<H8DataMonolithTelemetryEntry> ring))
            {
                return false;
            }

            bool written = false;
            try
            {
                if (ring.IsCreated && ring.Length >= H8DataLayoutConstants.TelemetryRingCapacity)
                {
                    ring[index] = entry;
                    written = true;
                }
            }
            finally
            {
                if (!ReleaseWriteLockWithRetry(vault, in _telemetryHandle, SystemID.CoreDataVault))
                    written = false;
            }

            return written;
        }

        private static void RecordTelemetry(H8DataBlobLoadStatus status, long loadTicks, long ioTicks, uint pathFlags)
        {
            if (!EnsureTelemetry() ||
                !TryReserveTelemetrySlot(out int index))
            {
                return;
            }

            uint stateHash = ((uint)_residentBlobBytes * H8DataHash.Fnv1A32Prime) ^
                             ((uint)_directory.SectionCount << 16) ^
                             (uint)status;
            H8DataMonolithTelemetryEntry entry = default;
            entry.Checksum64 = _header.Checksum64;
            entry.LoadTicks = loadTicks;
            entry.IoTicks = ioTicks;
            entry.FrameIndex = (uint)Interlocked.Increment(ref _telemetryFrame);
            entry.BlobBytes = (uint)Math.Max(0, _residentBlobBytes);
            entry.SectionCount = _directory.SectionCount;
            entry.LoadStatus = (uint)status;
            entry.PathFlags = pathFlags;
            entry.StateHash = stateHash;
            entry.Reserved0 = _lastFailureStage;
            entry.Reserved1 = _lastFailureDetail0;
            entry.Reserved2 = _lastFailureDetail1;
            entry.Reserved3 = _lastFailureDetail2;
            TryWriteTelemetryEntry(index, in entry);
        }

        private static void SetFailureTelemetry(uint stage, uint detail0, uint detail1, uint detail2)
        {
            _lastFailureStage = stage;
            _lastFailureDetail0 = detail0;
            _lastFailureDetail1 = detail1;
            _lastFailureDetail2 = detail2;
        }

        private static void DumpTelemetry(H8DataBlobLoadStatus status)
        {
            if (!TryReadTelemetry(out NativeArray<H8DataMonolithTelemetryEntry>.ReadOnly ring, out NativeArray<int>.ReadOnly cursor))
            {
                return;
            }

            int telemetryCursor = NormalizeTelemetryCursor(cursor[0]);
#if !UNITY_EDITOR && !DEVELOPMENT_BUILD && UNITY_STANDALONE_WIN && !UNITY_WEBGL && !UNITY_ANDROID && !UNITY_IOS
            WriteTelemetryDumpsWin32(status, ring, telemetryCursor);
#elif UNITY_ANDROID && !UNITY_EDITOR
            WriteTelemetryDumpAndroid(status, ring, telemetryCursor);
#elif UNITY_EDITOR || DEVELOPMENT_BUILD
            try
            {
                string folder = System.IO.Path.GetFullPath("Docs/AgentLogs");
                System.IO.Directory.CreateDirectory(folder);
                WriteTelemetryDump(System.IO.Path.Combine(folder, DataMonolithTelemetryDumpFileName), status, ring, telemetryCursor);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
            catch (ArgumentException)
            {
            }
            catch (NotSupportedException)
            {
            }
            catch (System.Security.SecurityException)
            {
            }
            catch (InvalidOperationException)
            {
            }
#endif
        }

        private static int NormalizeTelemetryCursor(int cursor)
        {
            return (uint)cursor < H8DataLayoutConstants.TelemetryRingCapacity ? cursor : 0;
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        private static unsafe bool WriteTelemetryDumpAndroid(
            H8DataBlobLoadStatus status,
            NativeArray<H8DataMonolithTelemetryEntry>.ReadOnly ring,
            int cursor)
        {
            string persistentDataPath = Application.persistentDataPath;
            if (string.IsNullOrEmpty(persistentDataPath))
                return false;

            byte* persistentDataPathUtf8 = stackalloc byte[AndroidPersistentPathUtf8Capacity];
            if (!TryWriteUtf8NullTerminated(
                    persistentDataPath.AsSpan(),
                    persistentDataPathUtf8,
                    AndroidPersistentPathUtf8Capacity,
                    out _))
            {
                return false;
            }

            void* ringPtr = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(ring);
            return H8_WriteTelemetryDump(
                persistentDataPathUtf8,
                ringPtr,
                H8DataLayoutConstants.TelemetryRingCapacity,
                UnsafeUtility.SizeOf<H8DataMonolithTelemetryEntry>(),
                (uint)status,
                cursor);
        }

        private static unsafe bool TryWriteUtf8NullTerminated(
            ReadOnlySpan<char> source,
            byte* destination,
            int capacity,
            out int byteCount)
        {
            byteCount = 0;
            if (destination == null || capacity <= 0)
                return false;

            for (int i = 0; i < source.Length; i++)
            {
                uint codePoint = source[i];
                if (codePoint == 0u)
                    return false;

                if (codePoint >= 0xD800u && codePoint <= 0xDBFFu)
                {
                    if (i + 1 >= source.Length)
                        return false;

                    uint low = source[++i];
                    if (low < 0xDC00u || low > 0xDFFFu)
                        return false;

                    codePoint = 0x10000u + ((codePoint - 0xD800u) << 10) + (low - 0xDC00u);
                }
                else if (codePoint >= 0xDC00u && codePoint <= 0xDFFFu)
                {
                    return false;
                }

                if (codePoint <= 0x7Fu)
                {
                    if (byteCount + 1 >= capacity)
                        return false;

                    destination[byteCount++] = (byte)codePoint;
                }
                else if (codePoint <= 0x7FFu)
                {
                    if (byteCount + 2 >= capacity)
                        return false;

                    destination[byteCount++] = (byte)(0xC0u | (codePoint >> 6));
                    destination[byteCount++] = (byte)(0x80u | (codePoint & 0x3Fu));
                }
                else if (codePoint <= 0xFFFFu)
                {
                    if (byteCount + 3 >= capacity)
                        return false;

                    destination[byteCount++] = (byte)(0xE0u | (codePoint >> 12));
                    destination[byteCount++] = (byte)(0x80u | ((codePoint >> 6) & 0x3Fu));
                    destination[byteCount++] = (byte)(0x80u | (codePoint & 0x3Fu));
                }
                else if (codePoint <= 0x10FFFFu)
                {
                    if (byteCount + 4 >= capacity)
                        return false;

                    destination[byteCount++] = (byte)(0xF0u | (codePoint >> 18));
                    destination[byteCount++] = (byte)(0x80u | ((codePoint >> 12) & 0x3Fu));
                    destination[byteCount++] = (byte)(0x80u | ((codePoint >> 6) & 0x3Fu));
                    destination[byteCount++] = (byte)(0x80u | (codePoint & 0x3Fu));
                }
                else
                {
                    return false;
                }
            }

            destination[byteCount] = 0;
            return true;
        }
#endif

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private static void WriteTelemetryDump(
            string path,
            H8DataBlobLoadStatus status,
            NativeArray<H8DataMonolithTelemetryEntry>.ReadOnly ring,
            int cursor)
        {
            string tempPath = path + ".tmp";
            TryDeleteFile(tempPath);
            try
            {
                using FileStream stream = new FileStream(
                    tempPath,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.Read,
                    4096,
                    FileOptions.WriteThrough | FileOptions.SequentialScan);
                using BinaryWriter writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true);
                writer.Write(0x4858444Du);
                writer.Write((uint)status);
                writer.Write(cursor);
                writer.Write(H8DataLayoutConstants.TelemetryRingCapacity);
                writer.Write(UnsafeUtility.SizeOf<H8DataMonolithTelemetryEntry>());
                int start = NormalizeTelemetryCursor(cursor);
                for (int i = 0; i < H8DataLayoutConstants.TelemetryRingCapacity; i++)
                {
                    int ringIndex = start + i;
                    if (ringIndex >= H8DataLayoutConstants.TelemetryRingCapacity)
                        ringIndex -= H8DataLayoutConstants.TelemetryRingCapacity;

                    H8DataMonolithTelemetryEntry entry = ring[ringIndex];
                    writer.Write(entry.Checksum64);
                    writer.Write(entry.LoadTicks);
                    writer.Write(entry.IoTicks);
                    writer.Write(entry.FrameIndex);
                    writer.Write(entry.BlobBytes);
                    writer.Write(entry.SectionCount);
                    writer.Write(entry.LoadStatus);
                    writer.Write(entry.PathFlags);
                    writer.Write(entry.StateHash);
                    writer.Write(entry.Reserved0);
                    writer.Write(entry.Reserved1);
                    writer.Write(entry.Reserved2);
                    writer.Write(entry.Reserved3);
                }

                writer.Flush();
                stream.Flush(true);
                PromoteTempFileCold(tempPath, path);
            }
            catch
            {
                TryDeleteFile(tempPath);
                throw;
            }
        }
#endif

#if !UNITY_EDITOR && !DEVELOPMENT_BUILD && UNITY_STANDALONE_WIN && !UNITY_WEBGL && !UNITY_ANDROID && !UNITY_IOS
        private static unsafe void WriteTelemetryDumpsWin32(
            H8DataBlobLoadStatus status,
            NativeArray<H8DataMonolithTelemetryEntry>.ReadOnly ring,
            int cursor)
        {
            char* path = stackalloc char[NativePathCapacity];
            if (TryBuildCurrentDirectoryPath(path, NativePathCapacity, "Docs"))
                CreateDirectoryWNative(path, IntPtr.Zero);

            if (TryBuildCurrentDirectoryPath(path, NativePathCapacity, "Docs\\AgentLogs"))
                CreateDirectoryWNative(path, IntPtr.Zero);

            if (TryBuildCurrentDirectoryPath(path, NativePathCapacity, DataMonolithTelemetryDumpRelativePath))
                WriteTelemetryDumpWin32(path, status, ring, cursor);
        }

        private static unsafe bool TryBuildCurrentDirectoryPath(char* buffer, int capacity, ReadOnlySpan<char> relativePath)
        {
            if (buffer == null || capacity <= 0)
                return false;

            uint length = GetCurrentDirectoryW((uint)capacity, buffer);
            if (length == 0u || length >= (uint)capacity)
                return false;

            int write = (int)length;
            if (write > 0 && buffer[write - 1] != '\\' && buffer[write - 1] != '/')
                buffer[write++] = '\\';

            if (!AppendLiteral(buffer, capacity, ref write, relativePath))
                return false;

            buffer[write] = '\0';
            return true;
        }

        private static unsafe bool WriteTelemetryDumpWin32(
            char* path,
            H8DataBlobLoadStatus status,
            NativeArray<H8DataMonolithTelemetryEntry>.ReadOnly ring,
            int cursor)
        {
            IntPtr handle = CreateFileWNative(
                path,
                NativeGenericWrite,
                NativeFileShareRead | NativeFileShareWrite,
                IntPtr.Zero,
                NativeCreateAlways,
                NativeFileFlagSequentialScan,
                IntPtr.Zero);
            if (handle == IntPtr.Zero || handle == NativeInvalidHandleValue)
                return false;

            int entrySize = UnsafeUtility.SizeOf<H8DataMonolithTelemetryEntry>();
            byte* header = stackalloc byte[20];
            int headerOffset = 0;
            WriteUInt32Le(header, ref headerOffset, 0x4858444Du);
            WriteUInt32Le(header, ref headerOffset, (uint)status);
            WriteInt32Le(header, ref headerOffset, cursor);
            WriteInt32Le(header, ref headerOffset, H8DataLayoutConstants.TelemetryRingCapacity);
            WriteInt32Le(header, ref headerOffset, entrySize);
            if (!WriteFileNative(handle, header, (uint)headerOffset, out uint headerWritten, IntPtr.Zero) ||
                headerWritten != (uint)headerOffset)
            {
                CloseHandleNative(handle);
                return false;
            }

            byte* entryBytes = stackalloc byte[H8DataLayoutConstants.TelemetryEntrySize];
            int start = NormalizeTelemetryCursor(cursor);
            for (int i = 0; i < H8DataLayoutConstants.TelemetryRingCapacity; i++)
            {
                int ringIndex = start + i;
                if (ringIndex >= H8DataLayoutConstants.TelemetryRingCapacity)
                    ringIndex -= H8DataLayoutConstants.TelemetryRingCapacity;

                H8DataMonolithTelemetryEntry entry = ring[ringIndex];
                int entryOffset = 0;
                WriteUInt64Le(entryBytes, ref entryOffset, entry.Checksum64);
                WriteInt64Le(entryBytes, ref entryOffset, entry.LoadTicks);
                WriteInt64Le(entryBytes, ref entryOffset, entry.IoTicks);
                WriteUInt32Le(entryBytes, ref entryOffset, entry.FrameIndex);
                WriteUInt32Le(entryBytes, ref entryOffset, entry.BlobBytes);
                WriteUInt32Le(entryBytes, ref entryOffset, entry.SectionCount);
                WriteUInt32Le(entryBytes, ref entryOffset, entry.LoadStatus);
                WriteUInt32Le(entryBytes, ref entryOffset, entry.PathFlags);
                WriteUInt32Le(entryBytes, ref entryOffset, entry.StateHash);
                WriteUInt32Le(entryBytes, ref entryOffset, entry.Reserved0);
                WriteUInt32Le(entryBytes, ref entryOffset, entry.Reserved1);
                WriteUInt32Le(entryBytes, ref entryOffset, entry.Reserved2);
                WriteUInt32Le(entryBytes, ref entryOffset, entry.Reserved3);
                if (!WriteFileNative(handle, entryBytes, (uint)entryOffset, out uint entryWritten, IntPtr.Zero) ||
                    entryWritten != (uint)entryOffset)
                {
                    CloseHandleNative(handle);
                    return false;
                }
            }

            CloseHandleNative(handle);
            return true;
        }

        private static unsafe void WriteInt32Le(byte* output, ref int offset, int value)
        {
            WriteUInt32Le(output, ref offset, (uint)value);
        }

        private static unsafe void WriteInt64Le(byte* output, ref int offset, long value)
        {
            WriteUInt64Le(output, ref offset, (ulong)value);
        }

        private static unsafe void WriteUInt32Le(byte* output, ref int offset, uint value)
        {
            output[offset++] = (byte)value;
            output[offset++] = (byte)(value >> 8);
            output[offset++] = (byte)(value >> 16);
            output[offset++] = (byte)(value >> 24);
        }

        private static unsafe void WriteUInt64Le(byte* output, ref int offset, ulong value)
        {
            for (int i = 0; i < 8; i++)
                output[offset++] = (byte)(value >> (i * 8));
        }
#endif

        private static bool TryFindLootTableRange(uint tableHash, out int start, out int end, out uint totalWeight)
        {
            ReadOnlySpan<H8LootCdfRecord> records = GetSectionSpan<H8LootCdfRecord>(H8DataSectionId.LootCdf);
            return TryFindLootTableRange(records, tableHash, out start, out end, out totalWeight);
        }

        private static bool TryFindLootTableRange(
            ReadOnlySpan<H8LootCdfRecord> records,
            uint tableHash,
            out int start,
            out int end,
            out uint totalWeight)
        {
            start = 0;
            end = 0;
            totalWeight = 0u;
            if (records.Length <= 0)
                return false;

            int low = 0;
            int high = records.Length;
            while (low < high)
            {
                int index = low + ((high - low) >> 1);
                if (records[index].TableHash < tableHash)
                    low = index + 1;
                else
                    high = index;
            }

            start = low;
            if (start >= records.Length || records[start].TableHash != tableHash)
                return false;

            low = start + 1;
            high = records.Length;
            while (low < high)
            {
                int index = low + ((high - low) >> 1);
                if (records[index].TableHash <= tableHash)
                    low = index + 1;
                else
                    high = index;
            }

            end = low;
            totalWeight = records[end - 1].TotalWeight;
            return end > start;
        }

        private static int ComputeArenaCapacity(int blobBytes)
        {
            int minimum = H8DataLayoutConstants.DefaultArenaCapacityBytes;
            if (blobBytes <= minimum)
                return minimum;

            return (blobBytes + (H8DataLayoutConstants.SectionAlignmentBytes - 1)) &
                   ~(H8DataLayoutConstants.SectionAlignmentBytes - 1);
        }

        private static uint AlignUp(uint value, uint alignment)
        {
            return (value + (alignment - 1u)) & ~(alignment - 1u);
        }

        private static ulong AlignUp(ulong value, uint alignment)
        {
            ulong mask = alignment - 1UL;
            return (value + mask) & ~mask;
        }

        private static bool ShutdownArenaOnly()
        {
            Volatile.Write(ref _loaded, 0);
            Volatile.Write(ref _writeLocked, 0);
            _header = default;
            _directory = default;
            _residentBlobBytes = 0;

            IDataVault vault = _vault;
            bool releasedAll = true;
            if (vault != null)
            {
                releasedAll &= ReleaseVaultHandle(vault, ref _arenaHandle);
                releasedAll &= ReleaseVaultHandle(vault, ref _telemetryHandle);
                releasedAll &= ReleaseVaultHandle(vault, ref _telemetryCursorHandle);
            }
            else
            {
                _arenaHandle = default;
                _telemetryHandle = default;
                _telemetryCursorHandle = default;
            }

            if (releasedAll &&
                _arenaHandle.BufferID == 0u &&
                _telemetryHandle.BufferID == 0u &&
                _telemetryCursorHandle.BufferID == 0u)
            {
                _vault = null;
            }

            return releasedAll &&
                   _arenaHandle.BufferID == 0u &&
                   _telemetryHandle.BufferID == 0u &&
                   _telemetryCursorHandle.BufferID == 0u;
        }

        private static bool ReleaseVaultHandle<T>(IDataVault vault, ref VaultGenerationHandle<T> handle) where T : struct
        {
            if (handle.BufferID == 0u)
            {
                handle = default;
                return true;
            }

            if (vault.ReleaseBuffer(in handle))
            {
                handle = default;
                return true;
            }

            if (ReleaseWriteLockWithRetry(vault, in handle, SystemID.CoreDataVault) &&
                vault.ReleaseBuffer(in handle))
            {
                handle = default;
                return true;
            }

            return false;
        }
    }
}
