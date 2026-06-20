using System;
using System.IO;
using Hecton8.Core;
using Hecton8.Core.Memory;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Networking;

namespace Hecton8.Rendering.Scatter
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/Rendering/Abyssal Scatter BRG DataVault Bootstrap")]
    public sealed class AbyssalScatterBrgDataVaultBootstrap : MonoBehaviour, ISlowTickable, IGlobalRegistryHotSwapListener, IGlobalRegistryHotSwapRefListener
    {
        private const uint FileMagic = 0x47524248u;
        private const uint FileVersion = 1u;
        private const int HeaderSizeBytes = 64;
        private const int MatrixStrideBytes = 64;
        private const int MetadataStrideBytes = 64;
        private const int QualityIndexStrideBytes = 4;
        private const int MaxRuntimeInstanceCount = 1048576;
        private const double UriLoadTimeoutSeconds = 30.0d;
        private const uint FileFlagHasQualityIndex = 1u << 0;
        private const uint FileFlagHasMetadata = 1u << 1;
        private const uint RequiredFileFlags = FileFlagHasQualityIndex | FileFlagHasMetadata;
        private const string StreamingAssetsPrefix = "Assets/StreamingAssets/";
        private const string UriCacheDirectoryName = "ScatterBrg";
        private const string UriCacheFileExtension = ".h8brg";

        [SerializeField] private GpuScatterLodManager targetRenderer;
        [SerializeField] private string brgDataAssetPath;
        [SerializeField] private uint expectedContentHash;
        [SerializeField] private uint expectedHeaderHash;
        [SerializeField] private int expectedMatrixCount;
        [SerializeField] private int expectedMetadataCount;
        [SerializeField] private int expectedQualityIndexCount;
        [SerializeField] private Bounds bakedDrawBounds;
        [SerializeField] private bool loadOnEnable = true;

        private IDataVault _dataVault;
        private bool _hotSwapRegistered;
        private bool _slowTickRegistered;
        private bool _loadRequested;
        private bool _loaded;
        private UnityWebRequest _uriRequest;
        private UnityWebRequestAsyncOperation _uriOperation;
        private double _uriRequestStartTime;
        private string _uriCachePathCold;
        private string _uriTempPathCold;

        public void ConfigureCold(
            GpuScatterLodManager renderer,
            string binaryAssetPath,
            uint contentHash,
            uint headerHash,
            int matrixCount,
            int metadataCount,
            int qualityIndexCount,
            Bounds drawBounds)
        {
            targetRenderer = renderer;
            brgDataAssetPath = binaryAssetPath ?? string.Empty;
            expectedContentHash = contentHash;
            expectedHeaderHash = headerHash;
            expectedMatrixCount = Math.Max(0, matrixCount);
            expectedMetadataCount = Math.Max(0, metadataCount);
            expectedQualityIndexCount = Math.Max(0, qualityIndexCount);
            bakedDrawBounds = drawBounds;
        }

        private void OnEnable()
        {
            RegisterHotSwapCold();
            _dataVault = GlobalRegistry.DataVault;
            if (loadOnEnable)
                RequestLoadCold();
        }

        private void Start()
        {
            if (loadOnEnable && !_loaded)
                RequestLoadCold();
        }

        private void OnDisable()
        {
            DisposeUriRequestCold();
            UnregisterSlowTickCold();

            if (_hotSwapRegistered)
            {
                GlobalRegistry.TryUnregisterHotSwapListener(this);
                _hotSwapRegistered = false;
            }

            _dataVault = null;
            _loadRequested = false;
        }

        public void SlowTick()
        {
            if (_uriOperation == null)
                return;

            if (!_uriOperation.isDone)
            {
                if (Time.realtimeSinceStartupAsDouble - _uriRequestStartTime >= UriLoadTimeoutSeconds)
                {
                    DisposeUriRequestCold();
                    UnregisterSlowTickCold();
                    _loadRequested = false;
                    LogWarningCold(this, "[1614] BRG scatter bootstrap URI load timed out.");
                }

                return;
            }

            CompleteUriLoadCold();
        }

        public void OnGlobalRegistryServiceRebound(GlobalRegistryServiceSlot serviceSlot, ref object currentService)
        {
            if (serviceSlot != GlobalRegistryServiceSlot.DataVault)
                return;

            _dataVault = currentService as IDataVault;
            if (_dataVault != null && !_loaded && loadOnEnable)
                RequestLoadCold();
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot != GlobalRegistryServiceSlot.DataVault)
                return;

            _dataVault = currentService as IDataVault;
            if (!ReferenceEquals(previousService, currentService))
                _loaded = false;

            if (_dataVault != null && loadOnEnable)
                RequestLoadCold();
        }

        private void RegisterHotSwapCold()
        {
            if (_hotSwapRegistered)
                return;

            _hotSwapRegistered = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void RequestLoadCold()
        {
            if (_loadRequested || _loaded || targetRenderer == null || string.IsNullOrEmpty(brgDataAssetPath))
                return;

            _loadRequested = true;
            string resolvedPath = ResolveStreamingAssetPath(brgDataAssetPath);
            if (IsUriPath(resolvedPath))
            {
                BeginUriLoadCold(resolvedPath);
                return;
            }

            if (!TryLoadFromFileCold(resolvedPath, out string failure))
                LogWarningCold(this, "[1614] BRG scatter bootstrap failed: " + failure);
        }

        private void BeginUriLoadCold(string uri)
        {
            DisposeUriRequestCold();
            try
            {
                if (!TryPrepareUriCachePathsCold(out string cachePath, out string tempPath, out string pathFailure))
                {
                    _loadRequested = false;
                    LogWarningCold(this, "[1614] BRG scatter bootstrap URI cache path failed: " + pathFailure);
                    return;
                }

                TryDeleteFileCold(tempPath);
                _uriCachePathCold = cachePath;
                _uriTempPathCold = tempPath;
                _uriRequest = new UnityWebRequest(uri, UnityWebRequest.kHttpVerbGET);
                _uriRequest.downloadHandler = new DownloadHandlerFile(tempPath)
                {
                    removeFileOnAbort = true
                };
                _uriRequest.disposeDownloadHandlerOnDispose = true;
                _uriRequest.timeout = (int)UriLoadTimeoutSeconds;
                _uriRequestStartTime = Time.realtimeSinceStartupAsDouble;
                _uriOperation = _uriRequest.SendWebRequest();
                if (!RegisterSlowTickCold())
                {
                    DisposeUriRequestCold();
                    _loadRequested = false;
                    LogWarningCold(this, "[1614] BRG scatter bootstrap URI slow tick registration failed.");
                }
            }
            catch (Exception exception) when (exception is InvalidOperationException || exception is UriFormatException || exception is ArgumentException)
            {
                DisposeUriRequestCold();
                _loadRequested = false;
                LogWarningCold(this, "[1614] BRG scatter bootstrap URI request failed: " + exception.Message);
            }
        }

        private void CompleteUriLoadCold()
        {
            UnityWebRequest request = _uriRequest;
            _uriRequest = null;
            _uriOperation = null;
            UnregisterSlowTickCold();

            if (request == null)
            {
                _loadRequested = false;
                return;
            }

            try
            {
                if (request.result != UnityWebRequest.Result.Success)
                {
                    LogWarningCold(this, "[1614] BRG scatter bootstrap URI load failed: " + request.error);
                    _loadRequested = false;
                    return;
                }

                string tempPath = _uriTempPathCold;
                string cachePath = _uriCachePathCold;
                if (string.IsNullOrEmpty(tempPath) || string.IsNullOrEmpty(cachePath) || !File.Exists(tempPath))
                {
                    LogWarningCold(this, "[1614] BRG scatter bootstrap URI returned no file payload.");
                    _loadRequested = false;
                    return;
                }

                request.Dispose();
                request = null;

                CommitUriCacheCold(tempPath, cachePath);
                _uriTempPathCold = null;

                if (!TryLoadFromFileCold(cachePath, out string failure))
                    LogWarningCold(this, "[1614] BRG scatter bootstrap failed: " + failure);
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException || exception is ArgumentException || exception is NotSupportedException)
            {
                TryDeleteFileCold(_uriTempPathCold);
                _loadRequested = false;
                LogWarningCold(this, "[1614] BRG scatter bootstrap URI cache commit failed: " + exception.GetType().Name);
            }
            finally
            {
                if (request != null)
                    request.Dispose();
                _uriRequestStartTime = 0d;
                _uriCachePathCold = null;
                _uriTempPathCold = null;
            }
        }

        private bool RegisterSlowTickCold()
        {
            if (_slowTickRegistered)
                return true;

            _slowTickRegistered = GlobalRegistry.TryRegisterSlowTickable(this, PriorityLayer.Environment);
            return _slowTickRegistered;
        }

        private void UnregisterSlowTickCold()
        {
            if (!_slowTickRegistered)
                return;

            GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Environment);
            _slowTickRegistered = false;
        }

        private void DisposeUriRequestCold()
        {
            _uriOperation = null;
            if (_uriRequest == null)
            {
                _uriRequestStartTime = 0d;
                TryDeleteFileCold(_uriTempPathCold);
                _uriCachePathCold = null;
                _uriTempPathCold = null;
                return;
            }

            _uriRequest.Abort();
            _uriRequest.Dispose();
            _uriRequest = null;
            _uriRequestStartTime = 0d;
            TryDeleteFileCold(_uriTempPathCold);
            _uriCachePathCold = null;
            _uriTempPathCold = null;
        }

        private bool TryPrepareUriCachePathsCold(out string cachePath, out string tempPath, out string failure)
        {
            cachePath = null;
            tempPath = null;
            failure = string.Empty;

            try
            {
                string cacheDirectory = Path.Combine(Application.temporaryCachePath, "Hecton8", UriCacheDirectoryName);
                Directory.CreateDirectory(cacheDirectory);
                string fileName = "scatter_" +
                    expectedContentHash.ToString("X8") +
                    "_" +
                    expectedHeaderHash.ToString("X8") +
                    UriCacheFileExtension;
                cachePath = Path.Combine(cacheDirectory, fileName);
                tempPath = cachePath + ".tmp";
                return true;
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException || exception is ArgumentException || exception is NotSupportedException)
            {
                failure = exception.GetType().Name;
                return false;
            }
        }

        private static void TryDeleteFileCold(string path)
        {
            if (string.IsNullOrEmpty(path))
                return;

            try
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException || exception is ArgumentException || exception is NotSupportedException)
            {
                LogWarningCold(null, "[1614] BRG scatter bootstrap failed to delete file: " + exception.GetType().Name);
            }
        }

        private static void CommitUriCacheCold(string tempPath, string cachePath)
        {
            if (File.Exists(cachePath))
                File.Replace(tempPath, cachePath, null, true);
            else
                File.Move(tempPath, cachePath);
        }

        private bool TryLoadFromFileCold(string filePath, out string failure)
        {
            failure = string.Empty;
            if (!File.Exists(filePath))
            {
                failure = "missing file " + filePath;
                _loadRequested = false;
                return false;
            }

            try
            {
                FileInfo info = new FileInfo(filePath);
                using (FileStream stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 65536, FileOptions.SequentialScan))
                using (BinaryReader reader = new BinaryReader(stream))
                {
                    return TryReadAndPublishPayloadCold(reader, info.Length, out failure);
                }
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                failure = exception.GetType().Name + " " + exception.Message;
                _loadRequested = false;
                return false;
            }
        }

        private bool TryReadAndPublishPayloadCold(BinaryReader reader, long byteLength, out string failure)
        {
            failure = string.Empty;
            LoadedBrgPayload payload = default;
            try
            {
                if (!TryReadPayloadCold(reader, byteLength, out payload, out failure))
                {
                    _loadRequested = false;
                    return false;
                }

                if (!TryPublishPayloadCold(payload, out failure))
                {
                    _loadRequested = false;
                    return false;
                }

                _loaded = true;
                _loadRequested = false;
                return true;
            }
            catch (Exception exception) when (exception is IOException || exception is EndOfStreamException || exception is InvalidOperationException || exception is OverflowException)
            {
                failure = exception.GetType().Name + " " + exception.Message;
                _loadRequested = false;
                return false;
            }
            finally
            {
                payload.Dispose();
            }
        }

        private bool TryReadPayloadCold(BinaryReader reader, long byteLength, out LoadedBrgPayload payload, out string failure)
        {
            payload = default;
            failure = string.Empty;
            BrgRuntimeHeader header = ReadHeader(reader);
            if (!ValidateHeader(header, byteLength, out failure))
                return false;

            payload.Header = header;
            if (!TryAllocateScratch(header.MatrixCount, NativeArrayOptions.UninitializedMemory, out payload.Matrices, out failure) ||
                !TryAllocateScratch(header.MetadataCount, NativeArrayOptions.UninitializedMemory, out payload.Metadata, out failure) ||
                !TryAllocateScratch(header.QualityIndexCount, NativeArrayOptions.UninitializedMemory, out payload.QualityIndices, out failure))
            {
                payload.Dispose();
                return false;
            }

            reader.BaseStream.Seek(header.MatrixOffsetBytes, SeekOrigin.Begin);
            for (int i = 0; i < header.MatrixCount; i++)
                payload.Matrices[i] = ReadMatrix(reader);

            reader.BaseStream.Seek(header.MetadataOffsetBytes, SeekOrigin.Begin);
            for (int i = 0; i < header.MetadataCount; i++)
                payload.Metadata[i] = ReadMetadata(reader);

            reader.BaseStream.Seek(header.QualityIndexOffsetBytes, SeekOrigin.Begin);
            for (int i = 0; i < header.QualityIndexCount; i++)
                payload.QualityIndices[i] = reader.ReadInt32();

            return ValidateQualityMap(payload.QualityIndices, header.MatrixCount, out failure) &&
                   ApplyQualityMapCold(ref payload, out failure);
        }

        private bool TryPublishPayloadCold(LoadedBrgPayload payload, out string failure)
        {
            failure = string.Empty;
            IDataVault vault = _dataVault;
            if (vault == null || vault.IsCompactionFenceActive)
            {
                failure = "DataVault unavailable";
                return false;
            }

            targetRenderer.MarkVaultDirty(0);
            if (!TryWriteMetadataCold(vault, payload, out failure))
                return false;

            if (!TryWriteMatricesCold(vault, payload, out failure))
                return false;

            targetRenderer.PublishVaultInstanceRange(payload.Header.MatrixCount, bakedDrawBounds);
            return true;
        }

        private static bool TryWriteMatricesCold(IDataVault vault, LoadedBrgPayload payload, out string failure)
        {
            failure = string.Empty;
            VaultGenerationHandle<Matrix4x4> handle = vault.EnsureGenerationHandle<Matrix4x4>(
                BufferID.FloraScatterMatrices,
                payload.Header.MatrixCount,
                SystemID.Vfx,
                NativeArrayOptions.UninitializedMemory);

            bool lockAcquired = false;
            try
            {
                if (!vault.TryAcquireWriteLock(in handle, SystemID.Vfx, out NativeArray<Matrix4x4> buffer) ||
                    !buffer.IsCreated ||
                    buffer.Length < payload.Header.MatrixCount)
                {
                    failure = "matrix DataVault write lock unavailable";
                    return false;
                }

                lockAcquired = true;
                NativeArray<Matrix4x4>.Copy(payload.Matrices, 0, buffer, 0, payload.Header.MatrixCount);

                return true;
            }
            finally
            {
                if (lockAcquired)
                    vault.ReleaseWriteLock(in handle, SystemID.Vfx);
            }
        }

        private static bool TryWriteMetadataCold(IDataVault vault, LoadedBrgPayload payload, out string failure)
        {
            failure = string.Empty;
            VaultGenerationHandle<GpuScatterFloraInstanceData> handle = vault.EnsureGenerationHandle<GpuScatterFloraInstanceData>(
                BufferID.FloraScatterMetadata,
                payload.Header.MetadataCount,
                SystemID.Vfx,
                NativeArrayOptions.ClearMemory);

            bool lockAcquired = false;
            try
            {
                if (!vault.TryAcquireWriteLock(in handle, SystemID.Vfx, out NativeArray<GpuScatterFloraInstanceData> buffer) ||
                    !buffer.IsCreated ||
                    buffer.Length < payload.Header.MetadataCount)
                {
                    failure = "metadata DataVault write lock unavailable";
                    return false;
                }

                lockAcquired = true;
                NativeArray<GpuScatterFloraInstanceData>.Copy(payload.Metadata, 0, buffer, 0, payload.Header.MetadataCount);

                return true;
            }
            finally
            {
                if (lockAcquired)
                    vault.ReleaseWriteLock(in handle, SystemID.Vfx);
            }
        }

        private bool ValidateHeader(BrgRuntimeHeader header, long byteLength, out string failure)
        {
            failure = string.Empty;
            if (header.Magic != FileMagic ||
                header.Version != FileVersion ||
                header.HeaderBytes != HeaderSizeBytes ||
                header.Flags != RequiredFileFlags ||
                header.MatrixStrideBytes != MatrixStrideBytes ||
                header.MetadataStrideBytes != MetadataStrideBytes ||
                header.QualityIndexStrideBytes != QualityIndexStrideBytes)
            {
                failure = "header ABI mismatch";
                return false;
            }

            if (header.MatrixCount <= 0 ||
                header.MetadataCount != header.MatrixCount ||
                header.QualityIndexCount != header.MatrixCount)
            {
                failure = "header count mismatch";
                return false;
            }

            if (header.MatrixCount > MaxRuntimeInstanceCount)
            {
                failure = "header instance cap exceeded";
                return false;
            }

            if (expectedMatrixCount > 0 && header.MatrixCount != expectedMatrixCount ||
                expectedMetadataCount > 0 && header.MetadataCount != expectedMetadataCount ||
                expectedQualityIndexCount > 0 && header.QualityIndexCount != expectedQualityIndexCount ||
                expectedContentHash != 0u && header.ContentHash != expectedContentHash ||
                expectedHeaderHash != 0u && header.HeaderHash != expectedHeaderHash)
            {
                failure = "header hash/count does not match prefab metadata";
                return false;
            }

            uint expectedMatrixOffset = HeaderSizeBytes;
            uint expectedMetadataOffset = checked(expectedMatrixOffset + (uint)((long)header.MatrixCount * MatrixStrideBytes));
            uint expectedQualityOffset = checked(expectedMetadataOffset + (uint)((long)header.MetadataCount * MetadataStrideBytes));
            long expectedBytes = checked((long)expectedQualityOffset + (long)header.QualityIndexCount * QualityIndexStrideBytes);
            if (header.MatrixOffsetBytes != expectedMatrixOffset ||
                header.MetadataOffsetBytes != expectedMetadataOffset ||
                header.QualityIndexOffsetBytes != expectedQualityOffset ||
                byteLength != expectedBytes)
            {
                failure = "payload byte layout mismatch";
                return false;
            }

            return true;
        }

        private static bool ValidateQualityMap(NativeArray<int> qualityIndices, int count, out string failure)
        {
            failure = string.Empty;
            if (!TryAllocateScratch(count, NativeArrayOptions.ClearMemory, out NativeArray<byte> seen, out failure))
                return false;

            try
            {
                for (int i = 0; i < qualityIndices.Length; i++)
                {
                    int sourceIndex = qualityIndices[i];
                    if ((uint)sourceIndex >= (uint)count)
                    {
                        failure = "quality index out of range";
                        return false;
                    }

                    if (seen[sourceIndex] != 0)
                    {
                        failure = "quality index duplicate";
                        return false;
                    }

                    seen[sourceIndex] = 1;
                }
            }
            finally
            {
                ReleaseScratch(ref seen);
            }

            return true;
        }

        private static bool ApplyQualityMapCold(ref LoadedBrgPayload payload, out string failure)
        {
            failure = string.Empty;
            int count = payload.Header.MatrixCount;
            if (!payload.Matrices.IsCreated ||
                !payload.Metadata.IsCreated ||
                !payload.QualityIndices.IsCreated ||
                payload.Matrices.Length < count ||
                payload.Metadata.Length < count ||
                payload.QualityIndices.Length < count)
            {
                failure = "quality map payload buffers invalid";
                return false;
            }

            NativeArray<Matrix4x4> matrices = default;
            NativeArray<GpuScatterFloraInstanceData> metadata = default;
            if (!TryAllocateScratch(count, NativeArrayOptions.UninitializedMemory, out matrices, out failure) ||
                !TryAllocateScratch(count, NativeArrayOptions.UninitializedMemory, out metadata, out failure))
            {
                ReleaseScratch(ref matrices);
                ReleaseScratch(ref metadata);
                return false;
            }

            bool transferred = false;
            try
            {
                for (int dst = 0; dst < count; dst++)
                {
                    int src = payload.QualityIndices[dst];
                    matrices[dst] = payload.Matrices[src];
                    metadata[dst] = payload.Metadata[src];
                }

                ReleaseScratch(ref payload.Matrices);
                ReleaseScratch(ref payload.Metadata);
                ReleaseScratch(ref payload.QualityIndices);
                payload.Matrices = matrices;
                payload.Metadata = metadata;
                payload.QualityIndices = default;
                transferred = true;
                return true;
            }
            finally
            {
                if (!transferred)
                {
                    ReleaseScratch(ref matrices);
                    ReleaseScratch(ref metadata);
                }
            }
        }

        private static bool TryAllocateScratch<T>(
            int length,
            NativeArrayOptions options,
            out NativeArray<T> array,
            out string failure) where T : struct
        {
            failure = string.Empty;
            array = H8Memory.Allocate<T>(length, SystemID.Vfx, Allocator.TempJob, options);
            if (array.IsCreated)
                return true;

            failure = "native scratch allocation failed";
            return false;
        }

        private static void ReleaseScratch<T>(ref NativeArray<T> array) where T : struct
        {
            if (!array.IsCreated)
                return;

            H8Memory.Release(ref array, SystemID.Vfx);
        }

        private static BrgRuntimeHeader ReadHeader(BinaryReader reader)
        {
            return new BrgRuntimeHeader
            {
                Magic = reader.ReadUInt32(),
                Version = reader.ReadUInt32(),
                HeaderBytes = reader.ReadUInt32(),
                Flags = reader.ReadUInt32(),
                MatrixCount = reader.ReadInt32(),
                MetadataCount = reader.ReadInt32(),
                QualityIndexCount = reader.ReadInt32(),
                MatrixStrideBytes = reader.ReadInt32(),
                MetadataStrideBytes = reader.ReadInt32(),
                QualityIndexStrideBytes = reader.ReadInt32(),
                MatrixOffsetBytes = reader.ReadUInt32(),
                MetadataOffsetBytes = reader.ReadUInt32(),
                QualityIndexOffsetBytes = reader.ReadUInt32(),
                ChunkHash = reader.ReadUInt32(),
                ContentHash = reader.ReadUInt32(),
                HeaderHash = reader.ReadUInt32()
            };
        }

        private static Matrix4x4 ReadMatrix(BinaryReader reader)
        {
            Matrix4x4 matrix = new Matrix4x4();
            matrix.SetColumn(0, ReadVector4(reader));
            matrix.SetColumn(1, ReadVector4(reader));
            matrix.SetColumn(2, ReadVector4(reader));
            matrix.SetColumn(3, ReadVector4(reader));
            return matrix;
        }

        private static GpuScatterFloraInstanceData ReadMetadata(BinaryReader reader)
        {
            return new GpuScatterFloraInstanceData
            {
                Type = reader.ReadSingle(),
                HeightScale = reader.ReadSingle(),
                WidthScale = reader.ReadSingle(),
                Variation = reader.ReadSingle(),
                TemplateIndex = reader.ReadSingle(),
                RuntimeState = reader.ReadSingle(),
                RuntimeFlags = reader.ReadSingle(),
                PulseFrequency = reader.ReadSingle(),
                BioluminescenceColor = ReadVector4(reader),
                SwaySpeed = reader.ReadSingle(),
                BendAmplitude = reader.ReadSingle(),
                HealthNormalized = reader.ReadSingle(),
                Reserved0 = reader.ReadSingle()
            };
        }

        private static Vector4 ReadVector4(BinaryReader reader)
        {
            return new Vector4(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
        }

        private static string ResolveStreamingAssetPath(string assetPath)
        {
            string normalized = (assetPath ?? string.Empty).Replace('\\', '/');
            if (normalized.StartsWith(StreamingAssetsPrefix, StringComparison.Ordinal))
            {
                string relative = normalized.Substring(StreamingAssetsPrefix.Length);
                return CombineStreamingPath(Application.streamingAssetsPath, relative);
            }

            if (Path.IsPathRooted(normalized) || IsUriPath(normalized))
                return normalized;

            return CombineStreamingPath(Application.streamingAssetsPath, normalized);
        }

        private static string CombineStreamingPath(string root, string relative)
        {
            if (IsUriPath(root))
                return root.TrimEnd('/') + "/" + relative.TrimStart('/');

            return Path.Combine(root, relative);
        }

        private static bool IsUriPath(string path)
        {
            return path.IndexOf("://", StringComparison.Ordinal) >= 0 ||
                   path.StartsWith("jar:", StringComparison.Ordinal);
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogWarningCold(AbyssalScatterBrgDataVaultBootstrap context, string message)
        {
            Debug.LogWarning(message, context);
        }

        private struct BrgRuntimeHeader
        {
            public uint Magic;
            public uint Version;
            public uint HeaderBytes;
            public uint Flags;
            public int MatrixCount;
            public int MetadataCount;
            public int QualityIndexCount;
            public int MatrixStrideBytes;
            public int MetadataStrideBytes;
            public int QualityIndexStrideBytes;
            public uint MatrixOffsetBytes;
            public uint MetadataOffsetBytes;
            public uint QualityIndexOffsetBytes;
            public uint ChunkHash;
            public uint ContentHash;
            public uint HeaderHash;
        }

        private struct LoadedBrgPayload : IDisposable
        {
            public BrgRuntimeHeader Header;
            public NativeArray<Matrix4x4> Matrices;
            public NativeArray<GpuScatterFloraInstanceData> Metadata;
            public NativeArray<int> QualityIndices;

            public void Dispose()
            {
                ReleaseScratch(ref Matrices);
                ReleaseScratch(ref Metadata);
                ReleaseScratch(ref QualityIndices);
            }
        }
    }
}
