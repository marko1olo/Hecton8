using System;
using System.IO;
using System.Threading;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Networking;

namespace Hecton8.Core
{
    /// <summary>
    /// Cold loader for the packed Beer-Lambert water extinction LUT.
    /// </summary>
    public static class LutArrayResolver
    {
        private const int MatrixSize = 4096;
        private const int MatrixTexelCount = MatrixSize * MatrixSize;
        private const int MatrixByteCount = MatrixTexelCount * 2;
        private const float DefaultMaxDepthMeters = 1500f;
        private const float DefaultMaxTurbidity = 2.5f;
        private const float DefaultStrength = 1f;
        private const int StreamingReadChunkBytes = 128 * 1024;
        private const int StreamingUriTimeoutSeconds = 30;
        private const string MatrixProjectRelativePath = "Data/Visuals/Water_Extinction_Matrix.bin";
        private const string MatrixStreamingRelativePath = "Data/Visuals/Water_Extinction_Matrix.bin";
        private const string MatrixCacheDirectoryName = "Hecton8/WaterExtinction";
        private const string MatrixFileName = "Water_Extinction_Matrix.bin";

        private static readonly int _ExtinctionLutId = Shader.PropertyToID("_ExtinctionLUT");

        private static Texture2D _extinctionTexture;
        private static byte[] _streamScratch;
        private static bool _loaded;
        private static bool _usingFallbackFormat;

        /// <summary>
        /// Loads and globally binds the extinction LUT before gameplay scenes start.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void EnsureLoadedAndBound()
        {
            if (_loaded)
                return;

            _loaded = true;
            PublishFallbackGlobals();

            string path = ResolveMatrixPath();
            if (string.IsNullOrEmpty(path))
            {
                LogMissingMatrix();
                return;
            }

            if (!TryGetMatrixFileByteCount(path, out long byteCount))
                return;

            if (byteCount != MatrixByteCount)
            {
                LogInvalidByteCount(byteCount);
                return;
            }

            bool supportsR16 = SystemInfo.SupportsTextureFormat(TextureFormat.RHalf) &&
                SystemInfo.IsFormatSupported(GraphicsFormat.R16_SFloat, GraphicsFormatUsage.Sample);
            bool supportsRgba16 = SystemInfo.IsFormatSupported(
                GraphicsFormat.R16G16B16A16_SFloat,
                GraphicsFormatUsage.Sample);
            _usingFallbackFormat = !supportsR16;
            _extinctionTexture = _usingFallbackFormat
                ? BuildArgb32Fallback(path)
                : BuildRHalfTexture(path);

            if (_extinctionTexture == null)
                return;

            Shader.SetGlobalTexture(_ExtinctionLutId, _extinctionTexture);
            HectonShaderGlobalDataVaultBridge.PublishWaterExtinctionParams(
                new Vector4(DefaultMaxDepthMeters, DefaultMaxTurbidity, DefaultStrength, 1f));
            HectonShaderGlobalDataVaultBridge.PublishWaterExtinctionRuntime(new Vector4(0f, 1f, 1f, 1f));
            HectonShaderGlobalDataVaultBridge.PublishWaterExtinctionWeather(Vector4.zero);

            if (!supportsRgba16)
                LogRgbaHalfUnsupported();
        }

        /// <summary>
        /// Clears static state when Unity reloads subsystems.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            if (_extinctionTexture != null)
                UnityEngine.Object.Destroy(_extinctionTexture);

            _extinctionTexture = null;
            _streamScratch = null;
            _loaded = false;
            _usingFallbackFormat = false;
            PublishFallbackGlobals();
        }

        private static Texture2D BuildRHalfTexture(string matrixPath)
        {
            Texture2D texture = new Texture2D(MatrixSize, MatrixSize, TextureFormat.RHalf, false, true)
            {
                name = "TX_Water_Extinction_Matrix_R16F",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                anisoLevel = 0,
                hideFlags = HideFlags.DontSave
            };

            NativeArray<half> halfData = texture.GetRawTextureData<half>();
            if (halfData.Length != MatrixTexelCount)
            {
                UnityEngine.Object.Destroy(texture);
                LogInvalidTexelCount(halfData.Length);
                return null;
            }

            NativeArray<byte> rawBytes = texture.GetRawTextureData<byte>();
            if (!TryStreamFileIntoRawTexture(matrixPath, rawBytes, MatrixByteCount))
            {
                UnityEngine.Object.Destroy(texture);
                return null;
            }

            texture.Apply(false, true);
            return texture;
        }

        private static Texture2D BuildArgb32Fallback(string matrixPath)
        {
            Texture2D texture = new Texture2D(MatrixSize, MatrixSize, TextureFormat.RGBA32, false, true)
            {
                name = "TX_Water_Extinction_Matrix_ARGB32_Fallback",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                anisoLevel = 0,
                hideFlags = HideFlags.DontSave
            };

            NativeArray<Color32> pixels = texture.GetRawTextureData<Color32>();
            if (pixels.Length != MatrixTexelCount)
            {
                UnityEngine.Object.Destroy(texture);
                LogInvalidTexelCount(pixels.Length);
                return null;
            }

            if (!TryStreamFileIntoArgb32Fallback(matrixPath, pixels))
            {
                UnityEngine.Object.Destroy(texture);
                return null;
            }

            texture.Apply(false, true);
            return texture;
        }

        private static string ResolveMatrixPath()
        {
            string streamingLocation = BuildStreamingAssetLocation();
            if (IsFilesystemPath(streamingLocation))
            {
                if (File.Exists(streamingLocation))
                    return streamingLocation;
            }
            else if (TryStageStreamingUriToCache(streamingLocation, out string cachedStreamingPath))
            {
                return cachedStreamingPath;
            }

            string persistentPath = Path.Combine(Application.persistentDataPath, MatrixStreamingRelativePath);
            if (File.Exists(persistentPath))
                return persistentPath;

            string projectPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", MatrixProjectRelativePath));
            return File.Exists(projectPath) ? projectPath : null;
        }

        private static string BuildStreamingAssetLocation()
        {
            string root = Application.streamingAssetsPath;
            if (string.IsNullOrEmpty(root))
                return null;

            if (IsFilesystemPath(root))
                return Path.Combine(root, MatrixStreamingRelativePath);

            string normalizedRoot = root.EndsWith("/", StringComparison.Ordinal) ? root : root + "/";
            return normalizedRoot + MatrixStreamingRelativePath.Replace('\\', '/');
        }

        private static bool TryStageStreamingUriToCache(string streamingUri, out string cachedPath)
        {
            cachedPath = null;
            if (string.IsNullOrEmpty(streamingUri) || IsFilesystemPath(streamingUri))
                return false;

            string cachePath = null;
            string tempPath = null;
            try
            {
                string cacheDirectory = Path.Combine(Application.temporaryCachePath, MatrixCacheDirectoryName);
                Directory.CreateDirectory(cacheDirectory);
                cachePath = Path.Combine(cacheDirectory, MatrixFileName);
                tempPath = cachePath + ".tmp";

                if (File.Exists(cachePath) &&
                    TryGetMatrixFileByteCount(cachePath, out long cachedByteCount) &&
                    cachedByteCount == MatrixByteCount)
                {
                    cachedPath = cachePath;
                    return true;
                }

                TryDeleteFile(tempPath);
                using UnityWebRequest request = new UnityWebRequest(streamingUri, UnityWebRequest.kHttpVerbGET);
                request.downloadHandler = new DownloadHandlerFile(tempPath)
                {
                    removeFileOnAbort = true
                };
                request.disposeDownloadHandlerOnDispose = true;
                request.timeout = StreamingUriTimeoutSeconds;

                UnityWebRequestAsyncOperation operation = request.SendWebRequest();
                while (!operation.isDone)
                    Thread.Sleep(1);

                if (request.result != UnityWebRequest.Result.Success)
                {
                    LogStreamingUriFailure(request.error);
                    TryDeleteFile(tempPath);
                    return false;
                }

                if (!TryGetMatrixFileByteCount(tempPath, out long stagedByteCount) ||
                    stagedByteCount != MatrixByteCount)
                {
                    LogInvalidByteCount(stagedByteCount);
                    TryDeleteFile(tempPath);
                    return false;
                }

                TryDeleteFile(cachePath);
                File.Move(tempPath, cachePath);
                cachedPath = cachePath;
                return true;
            }
            catch (Exception exception)
            {
                LogLoadException(exception);
                TryDeleteFile(tempPath);
                return false;
            }
        }

        private static bool TryGetMatrixFileByteCount(string matrixPath, out long byteCount)
        {
            byteCount = 0L;
            try
            {
                byteCount = new FileInfo(matrixPath).Length;
                return true;
            }
            catch (Exception exception)
            {
                LogLoadException(exception);
                return false;
            }
        }

        private static bool TryStreamFileIntoRawTexture(string matrixPath, NativeArray<byte> destination, int byteCount)
        {
            if (!destination.IsCreated || destination.Length < byteCount)
            {
                LogInvalidByteCount(destination.IsCreated ? destination.Length : 0);
                return false;
            }

            byte[] scratch = GetStreamingScratch();
            try
            {
                using FileStream stream = new FileStream(
                    matrixPath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read,
                    StreamingReadChunkBytes,
                    FileOptions.SequentialScan);

                if (stream.Length != byteCount)
                {
                    LogInvalidByteCount(stream.Length);
                    return false;
                }

                int destinationOffset = 0;
                while (destinationOffset < byteCount)
                {
                    int bytesToRead = math.min(scratch.Length, byteCount - destinationOffset);
                    if (!TryReadExact(stream, scratch, bytesToRead))
                    {
                        LogInvalidByteCount(destinationOffset);
                        return false;
                    }

                    NativeArray<byte>.Copy(scratch, 0, destination, destinationOffset, bytesToRead);
                    destinationOffset += bytesToRead;
                }

                return true;
            }
            catch (Exception exception)
            {
                LogLoadException(exception);
                return false;
            }
        }

        private static bool TryStreamFileIntoArgb32Fallback(string matrixPath, NativeArray<Color32> pixels)
        {
            if (!pixels.IsCreated || pixels.Length < MatrixTexelCount)
            {
                LogInvalidTexelCount(pixels.IsCreated ? pixels.Length : 0);
                return false;
            }

            byte[] scratch = GetStreamingScratch();
            int maxSamplesPerChunk = scratch.Length >> 1;
            try
            {
                using FileStream stream = new FileStream(
                    matrixPath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read,
                    StreamingReadChunkBytes,
                    FileOptions.SequentialScan);

                if (stream.Length != MatrixByteCount)
                {
                    LogInvalidByteCount(stream.Length);
                    return false;
                }

                int pixelIndex = 0;
                while (pixelIndex < MatrixTexelCount)
                {
                    int sampleCount = math.min(maxSamplesPerChunk, MatrixTexelCount - pixelIndex);
                    int bytesToRead = sampleCount << 1;
                    if (!TryReadExact(stream, scratch, bytesToRead))
                    {
                        LogInvalidByteCount(pixelIndex << 1);
                        return false;
                    }

                    for (int i = 0, byteIndex = 0; i < sampleCount; i++, byteIndex += 2, pixelIndex++)
                    {
                        ushort halfBits = (ushort)(scratch[byteIndex] | (scratch[byteIndex + 1] << 8));
                        byte value = HalfToByte01(halfBits);
                        pixels[pixelIndex] = new Color32(value, value, value, 255);
                    }
                }

                return true;
            }
            catch (Exception exception)
            {
                LogLoadException(exception);
                return false;
            }
        }

        private static bool TryReadExact(FileStream stream, byte[] scratch, int byteCount)
        {
            int offset = 0;
            while (offset < byteCount)
            {
                int read = stream.Read(scratch, offset, byteCount - offset);
                if (read <= 0)
                    return false;

                offset += read;
            }

            return true;
        }

        private static byte[] GetStreamingScratch()
        {
            if (_streamScratch == null)
                _streamScratch = new byte[StreamingReadChunkBytes]; // COLD ALLOC: byte[131072] - sequential LUT file staging window - owner: LutArrayResolver

            return _streamScratch;
        }

        private static bool IsFilesystemPath(string path)
        {
            return !string.IsNullOrEmpty(path) && path.IndexOf("://", StringComparison.Ordinal) < 0;
        }

        private static void TryDeleteFile(string path)
        {
            if (string.IsNullOrEmpty(path))
                return;

            try
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch (Exception exception)
            {
                LogLoadException(exception);
            }
        }

        private static byte HalfToByte01(ushort halfBits)
        {
            float value = HalfToFloat(halfBits);
            value = math.isfinite(value) ? math.saturate(value) : 1f;
            return (byte)math.clamp((int)math.round(value * 255f), 0, 255);
        }

        private static float HalfToFloat(ushort halfBits)
        {
            int sign = (halfBits & 0x8000) << 16;
            int exponent = (halfBits >> 10) & 0x1F;
            int mantissa = halfBits & 0x03FF;
            int bits;
            if (exponent == 0)
            {
                if (mantissa == 0)
                {
                    bits = sign;
                }
                else
                {
                    exponent = 1;
                    while ((mantissa & 0x0400) == 0)
                    {
                        mantissa <<= 1;
                        exponent--;
                    }

                    mantissa &= 0x03FF;
                    bits = sign | ((exponent + 112) << 23) | (mantissa << 13);
                }
            }
            else if (exponent == 31)
            {
                bits = sign | 0x7F800000 | (mantissa << 13);
            }
            else
            {
                bits = sign | ((exponent + 112) << 23) | (mantissa << 13);
            }

            return math.asfloat((uint)bits);
        }

        private static void PublishFallbackGlobals()
        {
            Shader.SetGlobalTexture(_ExtinctionLutId, Texture2D.blackTexture);
            HectonShaderGlobalDataVaultBridge.ResetWaterExtinctionGlobals();
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogMissingMatrix()
        {
            Debug.LogWarning("[LutArrayResolver] Water_Extinction_Matrix.bin not found. Extinction LUT disabled.");
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogLoadException(Exception exception)
        {
            Debug.LogWarning("[LutArrayResolver] Failed to load Water_Extinction_Matrix.bin: " + exception.Message);
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogInvalidByteCount(long byteCount)
        {
            Debug.LogWarning("[LutArrayResolver] Invalid Water_Extinction_Matrix.bin byte count: " + byteCount);
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogInvalidTexelCount(int texelCount)
        {
            Debug.LogWarning("[LutArrayResolver] Invalid extinction LUT texel count: " + texelCount);
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogRgbaHalfUnsupported()
        {
            Debug.LogWarning("[LutArrayResolver] R16G16B16A16_SFloat sampling is unsupported; packed R16 path remains active when available.");
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogStreamingUriFailure(string error)
        {
            Debug.LogWarning("[LutArrayResolver] StreamingAssets URI staging failed: " + error);
        }

        /// <summary>
        /// True when ARGB32 quantization replaced the native R16F texture path.
        /// </summary>
        public static bool UsingFallbackFormat => _usingFallbackFormat;
    }
}
