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
        private const uint FileFlagHasQualityIndex = 1u << 0;
        private const uint FileFlagHasMetadata = 1u << 1;
        private const uint RequiredFileFlags = FileFlagHasQualityIndex | FileFlagHasMetadata;
        private const string StreamingAssetsPrefix = "Assets/StreamingAssets/";

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
            if (_uriOperation == null || !_uriOperation.isDone)
                return;

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
                _uriRequest = UnityWebRequest.Get(uri);
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

                byte[] payload = request.downloadHandler == null ? null : request.downloadHandler.data;
                if (payload == null || payload.Length == 0)
                {
                    LogWarningCold(this, "[1614] BRG scatter bootstrap URI returned empty payload.");
                    _loadRequested = false;
                    return;
                }

                if (!TryLoadFromBytesCold(payload, out string failure))
                    LogWarningCold(this, "[1614] BRG scatter bootstrap failed: " + failure);
            }
            finally
            {
                request.Dispose();
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
                return;

            _uriRequest.Abort();
            _uriRequest.Dispose();
            _uriRequest = null;
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

        private bool TryLoadFromBytesCold(byte[] bytes, out string failure)
        {
            using (MemoryStream stream = new MemoryStream(bytes, writable: false))
            using (BinaryReader reader = new BinaryReader(stream))
            {
                return TryReadAndPublishPayloadCold(reader, bytes.Length, out failure);
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
            payload.Matrices = new NativeArray<Matrix4x4>(header.MatrixCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            payload.Metadata = new NativeArray<GpuScatterFloraInstanceData>(header.MetadataCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            payload.QualityIndices = new NativeArray<int>(header.QualityIndexCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);

            reader.BaseStream.Seek(header.MatrixOffsetBytes, SeekOrigin.Begin);
            for (int i = 0; i < header.MatrixCount; i++)
                payload.Matrices[i] = ReadMatrix(reader);

            reader.BaseStream.Seek(header.MetadataOffsetBytes, SeekOrigin.Begin);
            for (int i = 0; i < header.MetadataCount; i++)
                payload.Metadata[i] = ReadMetadata(reader);

            reader.BaseStream.Seek(header.QualityIndexOffsetBytes, SeekOrigin.Begin);
            for (int i = 0; i < header.QualityIndexCount; i++)
                payload.QualityIndices[i] = reader.ReadInt32();

            return ValidateQualityMap(payload.QualityIndices, header.MatrixCount, out failure);
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
                for (int dst = 0; dst < payload.Header.MatrixCount; dst++)
                    buffer[dst] = payload.Matrices[payload.QualityIndices[dst]];

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
                for (int dst = 0; dst < payload.Header.MetadataCount; dst++)
                    buffer[dst] = payload.Metadata[payload.QualityIndices[dst]];

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
            NativeArray<byte> seen = new NativeArray<byte>(count, Allocator.TempJob, NativeArrayOptions.ClearMemory);
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
                seen.Dispose();
            }

            return true;
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
                if (Matrices.IsCreated)
                    Matrices.Dispose();
                if (Metadata.IsCreated)
                    Metadata.Dispose();
                if (QualityIndices.IsCreated)
                    QualityIndices.Dispose();
            }
        }
    }
}
