using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Hecton8.Bootstrap;
using Hecton8.Core;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Optimization;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

namespace Hecton8.SaveSystem
{
    /// <summary>
    /// Handles save-slot thumbnail capture requests and cached thumbnail loading.
    /// Capture is routed through URP and AsyncGPUReadback via SaveThumbnailCaptureFeature.
    /// </summary>
    public static class SaveThumbnailSystem
    {
        private const int Width = 256;
        private const int Height = 144;
        private const string Extension = ".jpg";
        private const string LegacyExtension = ".png";
        private const int JpegQuality = 82;
        private const int MaxCachedTextures = 12;
        private const int MaxCaptureWaitFrames = 90;
        private const int CompletionHistoryCapacity = 8;
        private const float MinPoseCaptureDistanceMeters = 5f;
        private const float MinPoseCaptureAngleDegrees = 5f;
        private const float MinPoseCaptureDistanceSq = MinPoseCaptureDistanceMeters * MinPoseCaptureDistanceMeters;
        private const string NativeMemoryOwner = nameof(SaveThumbnailSystem);
        private static readonly float MinPoseCaptureQuaternionDot =
            Mathf.Cos(MinPoseCaptureAngleDegrees * Mathf.Deg2Rad * 0.5f);

        public enum CaptureStatus : byte
        {
            None = 0,
            Queued = 1,
            Completed = 2,
            LowTierSkipped = 3,
            ReusedExisting = 4,
            Failed = 5,
            TimedOut = 6,
            Cancelled = 7
        }

        public readonly struct CaptureTicket
        {
            public CaptureTicket(
                int sequenceId,
                uint operationId,
                uint slotHash,
                CaptureStatus status,
                int byteLength = 0,
                uint byteHash = 0u)
            {
                SequenceId = sequenceId;
                OperationId = operationId;
                SlotHash = slotHash;
                InitialStatus = status;
                ByteLength = byteLength;
                ByteHash = byteHash;
                IsValid = sequenceId > 0 ? (byte)1 : (byte)0;
                IsTerminal = status != CaptureStatus.Queued && status != CaptureStatus.None ? (byte)1 : (byte)0;
            }

            public readonly int SequenceId;
            public readonly uint OperationId;
            public readonly uint SlotHash;
            public readonly CaptureStatus InitialStatus;
            public readonly int ByteLength;
            public readonly uint ByteHash;
            public readonly byte IsValid;
            public readonly byte IsTerminal;
        }

        public readonly struct CaptureCompletion
        {
            public CaptureCompletion(int sequenceId, uint operationId, uint slotHash, int byteLength, uint byteHash, CaptureStatus status)
            {
                SequenceId = sequenceId;
                OperationId = operationId;
                SlotHash = slotHash;
                ByteLength = byteLength;
                ByteHash = byteHash;
                Status = status;
                Succeeded = status == CaptureStatus.Completed || status == CaptureStatus.LowTierSkipped || status == CaptureStatus.ReusedExisting
                    ? (byte)1
                    : (byte)0;
            }

            public readonly int SequenceId;
            public readonly uint OperationId;
            public readonly uint SlotHash;
            public readonly int ByteLength;
            public readonly uint ByteHash;
            public readonly CaptureStatus Status;
            public readonly byte Succeeded;
        }

        private struct CaptureRequest
        {
            public string SlotName;
            public Camera Camera;
            public int SequenceId;
            public uint OperationId;
            public uint SlotHash;
        }

        internal readonly struct RenderRequest
        {
            public RenderRequest(Camera camera, int sequenceId)
            {
                Camera = camera;
                SequenceId = sequenceId;
            }

            public readonly Camera Camera;
            public readonly int SequenceId;
        }

        private static readonly Dictionary<string, Texture2D> _textureCache =
            new Dictionary<string, Texture2D>(MaxCachedTextures, StringComparer.OrdinalIgnoreCase);
        private static readonly List<string> _textureCacheOrder = new List<string>(MaxCachedTextures);
        private static readonly Action<AsyncGPUReadbackRequest> s_readbackCompleted = HandleReadbackCompleted;
        private static readonly CaptureCompletion[] s_completionHistory = new CaptureCompletion[CompletionHistoryCapacity]; // COLD ALLOC: fixed completion ring for overlapping save/UI requests - owner: SaveThumbnailSystem

        private static Camera _cachedCaptureCamera;
        private static CaptureRequest _pendingRequest;
        private static CaptureRequest _inflightRequest;
        private static int _completionHistoryWriteIndex;
        private static bool _hasPendingRequest;
        private static bool _hasInflightRequest;
        private static bool _hasLastCapturePose;
        private static Vector3 _lastCapturePosition;
        private static Quaternion _lastCaptureRotation;
        private static int _requestSequence;
        private static NativeArray<byte> _readbackRgbaBuffer;
        private static NativeArray<Color32> _fallbackNoisePixels;
        private static Texture2D _fallbackNoiseTexture;
        private static bool _thumbnailWriteInProgress;
        private static bool _disposeReadbackBufferWhenIdle;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            ClearCache();
            DisposeFallbackNoise();
            if (_thumbnailWriteInProgress)
                _disposeReadbackBufferWhenIdle = true;
            else
                DisposeReadbackBuffer();
            _cachedCaptureCamera = null;
            _pendingRequest = default;
            _inflightRequest = default;
            ResetCompletionHistory();
            _hasPendingRequest = false;
            _hasInflightRequest = false;
            _hasLastCapturePose = false;
            _lastCapturePosition = default;
            _lastCaptureRotation = default;
            _requestSequence = 0;
        }

        internal static int CaptureWidth => Width;
        internal static int CaptureHeight => Height;
        internal static Action<AsyncGPUReadbackRequest> ReadbackCompletedCallback => s_readbackCompleted;

        public static string GetThumbnailPath(string slotName)
        {
            return HectonPersistentPathPolicy.CombineFile(ResolveThumbnailFileStem(slotName) + Extension);
        }

        public static string GetTempThumbnailPath(string slotName)
        {
            return GetThumbnailPath(slotName) + ".tmp";
        }

        /// <summary>
        /// Requests a thumbnail capture from the active player camera or an explicit override camera.
        /// The actual readback is executed by SaveThumbnailCaptureFeature during the next camera render.
        /// </summary>
        public static void CaptureThumbnail(string slotName, Camera overrideCamera = null)
        {
            _ = CaptureThumbnailForSave(slotName, byte.MaxValue, 0u, overrideCamera);
        }

        /// <summary>
        /// Requests a save-owned thumbnail capture and returns a ticket that can be joined after persistence I/O.
        /// </summary>
        public static CaptureTicket CaptureThumbnailForSave(
            string slotName,
            byte slotIndex,
            uint operationId,
            Camera overrideCamera = null)
        {
            uint slotHash = ComputeSlotHash(slotName);
            int sequenceId = NextSequenceId();
            if (!SaveManager.TryResolveSafeSlotName(slotName, out slotName))
            {
                CompleteRequest(new CaptureCompletion(sequenceId, operationId, slotHash, 0, 0u, CaptureStatus.Failed));
                return new CaptureTicket(sequenceId, operationId, slotHash, CaptureStatus.Failed);
            }

            slotHash = ResolveSlotHash(slotName, slotIndex);

            if (ShouldSkipScreenshotForCurrentTier())
            {
                TryDeleteThumbnailNoThrow(slotName);
                CompleteRequest(new CaptureCompletion(sequenceId, operationId, slotHash, 0, 0u, CaptureStatus.LowTierSkipped));
                return new CaptureTicket(sequenceId, operationId, slotHash, CaptureStatus.LowTierSkipped);
            }

            if (_thumbnailWriteInProgress ||
                !TryResolveCaptureCamera(overrideCamera, out Camera captureCamera))
            {
                CompleteRequest(new CaptureCompletion(sequenceId, operationId, slotHash, 0, 0u, CaptureStatus.Failed));
                return new CaptureTicket(sequenceId, operationId, slotHash, CaptureStatus.Failed);
            }

            if ((_hasPendingRequest && string.Equals(_pendingRequest.SlotName, slotName, StringComparison.OrdinalIgnoreCase)) ||
                (_hasInflightRequest && string.Equals(_inflightRequest.SlotName, slotName, StringComparison.OrdinalIgnoreCase)))
            {
                if (_hasPendingRequest)
                {
                    CaptureRequest pending = _pendingRequest;
                    if (operationId != 0u)
                    {
                        pending.OperationId = operationId;
                        pending.SlotHash = slotHash;
                        _pendingRequest = pending;
                    }

                    return new CaptureTicket(pending.SequenceId, pending.OperationId, pending.SlotHash, CaptureStatus.Queued);
                }

                CaptureRequest inflight = _inflightRequest;
                if (operationId != 0u)
                {
                    inflight.OperationId = operationId;
                    inflight.SlotHash = slotHash;
                    _inflightRequest = inflight;
                }

                return new CaptureTicket(inflight.SequenceId, inflight.OperationId, inflight.SlotHash, CaptureStatus.Queued);
            }

            if (!HasCapturePoseChanged(captureCamera))
            {
                TryGetExistingThumbnailStats(slotName, out int existingBytes, out uint existingHash);
                CompleteRequest(new CaptureCompletion(sequenceId, operationId, slotHash, existingBytes, existingHash, CaptureStatus.ReusedExisting));
                return new CaptureTicket(sequenceId, operationId, slotHash, CaptureStatus.ReusedExisting, existingBytes, existingHash);
            }

            ClearCacheEntry(slotName);
            _pendingRequest = new CaptureRequest
            {
                SlotName = slotName,
                Camera = captureCamera,
                SequenceId = sequenceId,
                OperationId = operationId,
                SlotHash = slotHash
            };
            Transform captureTransform = captureCamera.transform;
            _lastCapturePosition = captureTransform.position;
            _lastCaptureRotation = captureTransform.rotation;
            _hasLastCapturePose = true;
            _hasPendingRequest = true;
            return new CaptureTicket(sequenceId, operationId, slotHash, CaptureStatus.Queued);
        }

        /// <summary>
        /// Joins a save thumbnail ticket without blocking the frame or GPU readback.
        /// </summary>
        public static async Awaitable<CaptureCompletion> WaitForCompletionAsync(CaptureTicket ticket, CancellationToken cancellationToken = default)
        {
            if (ticket.IsValid == 0)
                return default;

            if (TryGetCompletion(ticket.SequenceId, out CaptureCompletion completion))
                return completion;

            if (ticket.IsTerminal != 0)
                return new CaptureCompletion(ticket.SequenceId, ticket.OperationId, ticket.SlotHash, ticket.ByteLength, ticket.ByteHash, ticket.InitialStatus);

            int startFrame = Time.frameCount;
            while (!TryGetCompletion(ticket.SequenceId, out completion))
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    CaptureCompletion cancelled = new CaptureCompletion(ticket.SequenceId, ticket.OperationId, ticket.SlotHash, 0, 0u, CaptureStatus.Cancelled);
                    ClearRequestIfMatching(ticket.SequenceId);
                    CompleteRequest(cancelled);
                    return cancelled;
                }

                bool waitingForGpuSubmit =
                    (_hasPendingRequest && _pendingRequest.SequenceId == ticket.SequenceId) ||
                    (_hasInflightRequest && _inflightRequest.SequenceId == ticket.SequenceId);

                if (Time.frameCount - startFrame > MaxCaptureWaitFrames)
                {
                    CaptureCompletion timedOut = new CaptureCompletion(ticket.SequenceId, ticket.OperationId, ticket.SlotHash, 0, 0u, CaptureStatus.TimedOut);
                    if (waitingForGpuSubmit)
                        ClearRequestIfMatching(ticket.SequenceId);

                    CompleteRequest(timedOut);
                    return timedOut;
                }

                try
                {
                    await Hecton8.Core.AwaitableDebtMonitor.NextFrameAsync(cancellationToken: cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    CaptureCompletion cancelled = new CaptureCompletion(ticket.SequenceId, ticket.OperationId, ticket.SlotHash, 0, 0u, CaptureStatus.Cancelled);
                    ClearRequestIfMatching(ticket.SequenceId);
                    CompleteRequest(cancelled);
                    return cancelled;
                }
            }

            return completion;
        }

        internal static bool TryAcquireRenderRequest(Camera renderCamera, out RenderRequest request)
        {
            if (_hasPendingRequest &&
                renderCamera != null &&
                ReferenceEquals(renderCamera, _pendingRequest.Camera))
            {
                request = new RenderRequest(renderCamera, _pendingRequest.SequenceId);
                return true;
            }

            request = default;
            return false;
        }

        internal static bool TrySubmitGpuReadback(int sequenceId)
        {
            if (!_hasPendingRequest || _hasInflightRequest || _pendingRequest.SequenceId != sequenceId)
                return false;

            _inflightRequest = _pendingRequest;
            _hasInflightRequest = true;
            _hasPendingRequest = false;
            return true;
        }

        internal static void NotifyCaptureFeatureDisposed()
        {
            if (_hasPendingRequest)
            {
                CaptureRequest pending = _pendingRequest;
                _pendingRequest = default;
                _hasPendingRequest = false;
                CompleteRequest(new CaptureCompletion(pending.SequenceId, pending.OperationId, pending.SlotHash, 0, 0u, CaptureStatus.Cancelled));
            }

            if (_hasInflightRequest)
            {
                CaptureRequest inflight = _inflightRequest;
                _inflightRequest = default;
                _hasInflightRequest = false;
                CompleteRequest(new CaptureCompletion(inflight.SequenceId, inflight.OperationId, inflight.SlotHash, 0, 0u, CaptureStatus.Cancelled));
            }
        }

        /// <summary>
        /// Loads a thumbnail texture for the specified slot. Uses cache safely to avoid repeated texture churn.
        /// </summary>
        public static Texture2D LoadThumbnailTexture(string slotName)
        {
            AssetLoadDispatcher.ForceEvaluateUiMipBiasGate();

            if (!SaveManager.TryResolveSafeSlotName(slotName, out slotName))
                return null;

            if (_textureCache.TryGetValue(slotName, out Texture2D cached))
            {
                if (cached != null)
                {
                    MarkCacheEntryAsMostRecent(_textureCacheOrder, slotName);
                    return cached;
                }

                RemoveCacheEntry(slotName);
            }

            string path = ResolveExistingThumbnailPath(slotName);
            if (!File.Exists(path))
                return null;

            byte[] bytes = File.ReadAllBytes(path);
            return LoadThumbnailTextureFromBytes(slotName, bytes);
        }

        /// <summary>
        /// Reads thumbnail bytes off the main thread and decodes only the visible slot on the UI thread.
        /// </summary>
        public static async Awaitable<Texture2D> LoadThumbnailTextureAsync(string slotName, CancellationToken cancellationToken = default)
        {
            AssetLoadDispatcher.ForceEvaluateUiMipBiasGate();

            if (!SaveManager.TryResolveSafeSlotName(slotName, out slotName))
                return null;

            if (_textureCache.TryGetValue(slotName, out Texture2D cached) && cached != null)
            {
                MarkCacheEntryAsMostRecent(_textureCacheOrder, slotName);
                return cached;
            }

            string path = ResolveExistingThumbnailPath(slotName);
            if (!File.Exists(path))
                return null;

            byte[] bytes = null;
            bool readFailed = false;
            await Awaitable.BackgroundThreadAsync();
            try
            {
                bytes = File.ReadAllBytes(path);
            }
            catch (Exception)
            {
                readFailed = true;
            }

            await Awaitable.MainThreadAsync();
            if (cancellationToken.IsCancellationRequested)
                return null;

            return readFailed || bytes == null || bytes.Length == 0
                ? GetFallbackNoiseTexture()
                : LoadThumbnailTextureFromBytes(slotName, bytes);
        }

        private static Texture2D LoadThumbnailTextureFromBytes(string slotName, byte[] bytes)
        {
            Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            texture.hideFlags = HideFlags.HideAndDontSave;
            if (texture.LoadImage(bytes, true))
            {
                AddCacheEntry(slotName, texture);
                return texture;
            }

            UnityEngine.Object.Destroy(texture);
            return GetFallbackNoiseTexture();
        }

        /// <summary>
        /// Legacy sprite wrapper for editor smoke checks and old UI callers.
        /// </summary>
        public static Sprite LoadThumbnail(string slotName)
        {
            Texture2D texture = LoadThumbnailTexture(slotName);
            if (texture == null)
                return null;

            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }

        public static Texture2D GetFallbackNoiseTexture()
        {
            if (_fallbackNoiseTexture != null)
                return _fallbackNoiseTexture;

            EnsureFallbackNoisePixels();
            _fallbackNoiseTexture = new Texture2D(Width, Height, TextureFormat.RGBA32, false, true); // COLD ALLOC: static fallback thumbnail texture - owner: SaveThumbnailSystem
            _fallbackNoiseTexture.hideFlags = HideFlags.HideAndDontSave;
            _fallbackNoiseTexture.LoadRawTextureData(_fallbackNoisePixels);
            _fallbackNoiseTexture.Apply(false, true);
            return _fallbackNoiseTexture;
        }

        public static void DeleteThumbnail(string slotName)
        {
            if (!SaveManager.TryResolveSafeSlotName(slotName, out slotName))
                return;

            ClearCacheEntry(slotName);

            string path = GetThumbnailPath(slotName);
            if (File.Exists(path))
                File.Delete(path);

            string tempPath = GetTempThumbnailPath(slotName);
            if (File.Exists(tempPath))
                File.Delete(tempPath);

            string legacyPath = GetLegacyThumbnailPath(slotName);
            if (File.Exists(legacyPath))
                File.Delete(legacyPath);
        }

        private static void TryDeleteThumbnailNoThrow(string slotName)
        {
            try
            {
                DeleteThumbnail(slotName);
            }
            catch (Exception)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogWarning("[SaveThumbnailSystem] Low-tier thumbnail purge failed.");
#endif
            }
        }

        /// <summary>
        /// Purges cached runtime thumbnails to free memory.
        /// </summary>
        public static void ClearCache()
        {
            Dictionary<string, Texture2D>.Enumerator enumerator = _textureCache.GetEnumerator();
            while (enumerator.MoveNext())
            {
                KeyValuePair<string, Texture2D> kvp = enumerator.Current;
                if (kvp.Value == null)
                    continue;

                UnityEngine.Object.Destroy(kvp.Value);
            }

            _textureCache.Clear();
            _textureCacheOrder.Clear();
        }

        private static bool TryResolveCaptureCamera(Camera overrideCamera, out Camera captureCamera)
        {
            if (overrideCamera != null &&
                overrideCamera.isActiveAndEnabled &&
                overrideCamera.gameObject.activeInHierarchy)
            {
                _cachedCaptureCamera = overrideCamera;
                captureCamera = overrideCamera;
                return true;
            }

            if (_cachedCaptureCamera != null &&
                _cachedCaptureCamera.isActiveAndEnabled &&
                _cachedCaptureCamera.gameObject.activeInHierarchy)
            {
                captureCamera = _cachedCaptureCamera;
                return true;
            }

            if (GameBootstrapper.TryGetCurrentPlayerTransform(out Transform playerTransform) &&
                playerTransform != null)
            {
                _cachedCaptureCamera = Hecton8.Core.PlayerRuntimeContextService.ActiveRuntimeContext != null && Hecton8.Core.PlayerRuntimeContextService.ActiveRuntimeContext.PlayerCamera != null
                    ? Hecton8.Core.PlayerRuntimeContextService.ActiveRuntimeContext.PlayerCamera
                    : ResolveCaptureCamera(playerTransform);
            }

            captureCamera = _cachedCaptureCamera;
            return captureCamera != null;
        }

        private static Camera ResolveCaptureCamera(Transform playerTransform)
        {
            if (playerTransform == null)
                return null;

            return playerTransform.TryGetComponent(out Camera captureCamera) ? captureCamera : null;
        }

        private static bool HasCapturePoseChanged(Camera captureCamera)
        {
            if (!_hasLastCapturePose || captureCamera == null)
                return true;

            Transform captureTransform = captureCamera.transform;
            Vector3 delta = captureTransform.position - _lastCapturePosition;
            if (delta.sqrMagnitude > MinPoseCaptureDistanceSq)
                return true;

            float rotationDot = Mathf.Abs(Quaternion.Dot(_lastCaptureRotation, captureTransform.rotation));
            return rotationDot < MinPoseCaptureQuaternionDot;
        }

        private static void HandleReadbackCompleted(AsyncGPUReadbackRequest request)
        {
            if (!_hasInflightRequest)
                return;

            CaptureRequest inflightRequest = _inflightRequest;
            _inflightRequest = default;
            _hasInflightRequest = false;

            if (!Application.isPlaying || inflightRequest.Camera == null)
            {
                CompleteRequest(new CaptureCompletion(inflightRequest.SequenceId, inflightRequest.OperationId, inflightRequest.SlotHash, 0, 0u, CaptureStatus.Cancelled));
                return;
            }

            if (request.hasError)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogError("[SaveThumbnailSystem] AsyncGPUReadback failed.");
#endif
                CompleteRequest(new CaptureCompletion(inflightRequest.SequenceId, inflightRequest.OperationId, inflightRequest.SlotHash, 0, 0u, CaptureStatus.Failed));
                return;
            }

            int expectedLength = Width * Height * 4;
            NativeArray<byte> readbackData = request.GetData<byte>();
            if (!readbackData.IsCreated || readbackData.Length < expectedLength)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogError("[SaveThumbnailSystem] AsyncGPUReadback returned invalid thumbnail data.");
#endif
                CompleteRequest(new CaptureCompletion(inflightRequest.SequenceId, inflightRequest.OperationId, inflightRequest.SlotHash, 0, 0u, CaptureStatus.Failed));
                return;
            }

            if (!EnsureReadbackBuffer(expectedLength))
            {
                CompleteRequest(new CaptureCompletion(inflightRequest.SequenceId, inflightRequest.OperationId, inflightRequest.SlotHash, 0, 0u, CaptureStatus.Failed));
                return;
            }

            NativeArray<byte>.Copy(readbackData, _readbackRgbaBuffer, expectedLength);
            _thumbnailWriteInProgress = true;
            _ = PersistThumbnailAsync(inflightRequest, _readbackRgbaBuffer, Width, Height);
        }

        private static async Awaitable PersistThumbnailAsync(CaptureRequest request, NativeArray<byte> rgbaBytes, int width, int height)
        {
            string slotName = request.SlotName;
            if (!SaveManager.TryResolveSafeSlotName(slotName, out slotName))
            {
                ReleaseWriteInProgress();
                CompleteRequest(new CaptureCompletion(request.SequenceId, request.OperationId, request.SlotHash, 0, 0u, CaptureStatus.Failed));
                return;
            }

            string path = GetThumbnailPath(slotName);
            string tempPath = GetTempThumbnailPath(slotName);
            int encodedByteLength = 0;
            uint encodedByteHash = 0u;
            CaptureStatus finalStatus = CaptureStatus.Completed;

            try
            {
                await Awaitable.BackgroundThreadAsync();

                string directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                    Directory.CreateDirectory(directory);

                if (File.Exists(tempPath))
                    File.Delete(tempPath);

                NativeArray<byte> encodedJpg = default;
                bool encodedJpgRegistered = false;
                try
                {
                    encodedJpg = ImageConversion.EncodeNativeArrayToJPG(
                        rgbaBytes,
                        GraphicsFormat.R8G8B8A8_SRGB,
                        (uint)width,
                        (uint)height,
                        0u,
                        JpegQuality);

                    if (!encodedJpg.IsCreated || encodedJpg.Length <= 0)
                        throw new IOException("JPG encoder returned no thumbnail bytes.");

                    encodedByteLength = encodedJpg.Length;
                    encodedByteHash = ComputeNativeByteHash(encodedJpg);

                    NativeMemorySentinel.RegisterNativeArray(
                        encodedJpg,
                        NativeMemoryOwner,
                        "thumbnailEncodedJpg",
                        NativeAllocationLifetime.TransientArena);
                    encodedJpgRegistered = true;

                    unsafe
                    {
                        void* dataPtr = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(encodedJpg);
                        if (!AsyncWriteManager.WriteAll(tempPath, dataPtr, encodedJpg.Length, out string writeError))
                            throw new IOException(writeError);
                    }
                }
                finally
                {
                    if (encodedJpg.IsCreated)
                    {
                        if (encodedJpgRegistered)
                            NativeMemorySentinel.UnregisterNativeArray(encodedJpg);

                        encodedJpg.Dispose();
                    }
                }

                if (File.Exists(path))
                    File.Delete(path);

                File.Move(tempPath, path);
                await Awaitable.MainThreadAsync();
            }
            catch (Exception)
            {
                finalStatus = CaptureStatus.Failed;
                try
                {
                    if (File.Exists(tempPath))
                        File.Delete(tempPath);
                }
                catch
                {
                }

                await Awaitable.MainThreadAsync();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogError("[SaveThumbnailSystem] Failed to persist thumbnail.");
#endif
            }
            finally
            {
                CaptureCompletion completion = new CaptureCompletion(
                    request.SequenceId,
                    request.OperationId,
                    request.SlotHash,
                    finalStatus == CaptureStatus.Completed ? encodedByteLength : 0,
                    finalStatus == CaptureStatus.Completed ? encodedByteHash : 0u,
                    finalStatus);

                ReleaseWriteInProgress();
                CompleteRequest(completion);
            }
        }

        private static void ReleaseWriteInProgress()
        {
            _thumbnailWriteInProgress = false;
            if (_disposeReadbackBufferWhenIdle)
            {
                _disposeReadbackBufferWhenIdle = false;
                DisposeReadbackBuffer();
            }
        }

        private static bool EnsureReadbackBuffer(int byteLength)
        {
            if (_readbackRgbaBuffer.IsCreated && _readbackRgbaBuffer.Length >= byteLength)
                return true;

            DisposeReadbackBuffer();
            if (byteLength <= 0)
                return false;

            _readbackRgbaBuffer = new NativeArray<byte>(byteLength, Allocator.Persistent, NativeArrayOptions.UninitializedMemory); // COLD ALLOC: NativeArray<byte>[Width * Height * 4] — persistent thumbnail GPU readback shadow buffer — owner: SaveThumbnailSystem
            NativeMemorySentinel.RegisterNativeArray(
                _readbackRgbaBuffer,
                NativeMemoryOwner,
                nameof(_readbackRgbaBuffer),
                NativeAllocationLifetime.Session);
            return true;
        }

        private static void DisposeReadbackBuffer()
        {
            if (!_readbackRgbaBuffer.IsCreated)
                return;

            NativeMemorySentinel.UnregisterNativeArray(_readbackRgbaBuffer);
            _readbackRgbaBuffer.Dispose();
            _readbackRgbaBuffer = default;
        }

        private static void EnsureFallbackNoisePixels()
        {
            int pixelCount = Width * Height;
            if (_fallbackNoisePixels.IsCreated && _fallbackNoisePixels.Length == pixelCount)
                return;

            DisposeFallbackNoise();
            _fallbackNoisePixels = new NativeArray<Color32>(pixelCount, Allocator.Persistent, NativeArrayOptions.UninitializedMemory); // COLD ALLOC: static fallback thumbnail pixels - owner: SaveThumbnailSystem
            NativeMemorySentinel.RegisterNativeArray(
                _fallbackNoisePixels,
                NativeMemoryOwner,
                nameof(_fallbackNoisePixels),
                NativeAllocationLifetime.Session);

            uint state = 0x8A77C0DEu;
            for (int i = 0; i < pixelCount; i++)
            {
                state ^= state << 13;
                state ^= state >> 17;
                state ^= state << 5;
                byte value = (byte)(48 + (state & 0x7Fu));
                _fallbackNoisePixels[i] = new Color32(value, value, value, 255);
            }
        }

        private static void DisposeFallbackNoise()
        {
            if (_fallbackNoiseTexture != null)
            {
                UnityEngine.Object.Destroy(_fallbackNoiseTexture);
                _fallbackNoiseTexture = null;
            }

            if (!_fallbackNoisePixels.IsCreated)
                return;

            NativeMemorySentinel.UnregisterNativeArray(_fallbackNoisePixels);
            _fallbackNoisePixels.Dispose();
            _fallbackNoisePixels = default;
        }

        private static int NextSequenceId()
        {
            unchecked
            {
                _requestSequence++;
                if (_requestSequence == 0)
                    _requestSequence = 1;

                return _requestSequence;
            }
        }

        private static bool ShouldSkipScreenshotForCurrentTier()
        {
            HectonQualityTier tier = GlobalRegistry.ScalabilityTier;
            return tier == HectonQualityTier.Low || tier == HectonQualityTier.Mx350;
        }

        private static uint ResolveSlotHash(string slotName, byte slotIndex)
        {
            return slotIndex < SaveEvents.ManualSlotCount
                ? ComputeSlotHash(SaveEvents.ResolveManualSlotName(slotIndex))
                : ComputeSlotHash(slotName);
        }

        private static uint ComputeSlotHash(string slotName)
        {
            const uint fnvOffset = 2166136261u;
            const uint fnvPrime = 16777619u;
            uint hash = fnvOffset;
            if (!string.IsNullOrEmpty(slotName))
            {
                for (int i = 0; i < slotName.Length; i++)
                {
                    hash ^= slotName[i];
                    hash *= fnvPrime;
                }
            }

            return hash == 0u ? 1u : hash;
        }

        private static uint ComputeNativeByteHash(NativeArray<byte> bytes)
        {
            const uint fnvOffset = 2166136261u;
            const uint fnvPrime = 16777619u;
            uint hash = fnvOffset;
            for (int i = 0; i < bytes.Length; i++)
            {
                hash ^= bytes[i];
                hash *= fnvPrime;
            }

            return hash == 0u ? 1u : hash;
        }

        private static bool TryGetExistingThumbnailStats(string slotName, out int byteLength, out uint metadataHash)
        {
            byteLength = 0;
            metadataHash = 0u;
            string path = ResolveExistingThumbnailPath(slotName);
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                return false;

            try
            {
                FileInfo info = new FileInfo(path);
                long length = info.Length;
                byteLength = length <= 0L ? 0 : length > int.MaxValue ? int.MaxValue : (int)length;
                metadataHash = ComputeMetadataHash((uint)byteLength, info.LastWriteTimeUtc.Ticks);
                return byteLength > 0;
            }
            catch (Exception)
            {
                byteLength = 0;
                metadataHash = 0u;
                return false;
            }
        }

        private static uint ComputeMetadataHash(uint byteLength, long ticks)
        {
            const uint fnvOffset = 2166136261u;
            const uint fnvPrime = 16777619u;
            uint hash = fnvOffset;
            hash ^= byteLength;
            hash *= fnvPrime;
            unchecked
            {
                hash ^= (uint)ticks;
                hash *= fnvPrime;
                hash ^= (uint)(ticks >> 32);
                hash *= fnvPrime;
            }

            return hash == 0u ? 1u : hash;
        }

        private static void CompleteRequest(CaptureCompletion completion)
        {
            StoreCompletion(completion);
            if (completion.OperationId != 0u)
                PublishMetadataReady(completion);
        }

        private static void StoreCompletion(CaptureCompletion completion)
        {
            s_completionHistory[_completionHistoryWriteIndex] = completion;
            _completionHistoryWriteIndex++;
            if (_completionHistoryWriteIndex >= s_completionHistory.Length)
                _completionHistoryWriteIndex = 0;
        }

        private static bool TryGetCompletion(int sequenceId, out CaptureCompletion completion)
        {
            for (int i = 0; i < s_completionHistory.Length; i++)
            {
                completion = s_completionHistory[i];
                if (completion.SequenceId == sequenceId)
                    return true;
            }

            completion = default;
            return false;
        }

        private static void ResetCompletionHistory()
        {
            for (int i = 0; i < s_completionHistory.Length; i++)
                s_completionHistory[i] = default;

            _completionHistoryWriteIndex = 0;
        }

        private static void PublishMetadataReady(CaptureCompletion completion)
        {
            SaveMetadataReadySignal signal = new SaveMetadataReadySignal
            {
                SlotHash = completion.SlotHash,
                OperationId = completion.OperationId,
                ScreenshotBytes = completion.ByteLength <= 0 ? 0u : (uint)completion.ByteLength,
                ScreenshotHash = completion.ByteHash,
                Frame = unchecked((uint)Time.frameCount),
                Result = ToMetadataResult(completion.Status),
                Flags = ToMetadataFlags(completion.Status)
            };
            SignalBus<SaveMetadataReadySignal>.Push(in signal);
        }

        private static byte ToMetadataResult(CaptureStatus status)
        {
            switch (status)
            {
                case CaptureStatus.Completed:
                    return SaveMetadataReadySignal.Completed;
                case CaptureStatus.LowTierSkipped:
                    return SaveMetadataReadySignal.SkippedLowTier;
                case CaptureStatus.ReusedExisting:
                    return SaveMetadataReadySignal.ReusedExisting;
                case CaptureStatus.TimedOut:
                    return SaveMetadataReadySignal.TimedOut;
                default:
                    return SaveMetadataReadySignal.Failed;
            }
        }

        private static byte ToMetadataFlags(CaptureStatus status)
        {
            switch (status)
            {
                case CaptureStatus.LowTierSkipped:
                    return SaveMetadataReadySignal.LowTierFlag;
                case CaptureStatus.ReusedExisting:
                    return SaveMetadataReadySignal.ReusedExistingFlag;
                case CaptureStatus.Completed:
                    return 0;
                default:
                    return SaveMetadataReadySignal.FailureFlag;
            }
        }

        private static void ClearRequestIfMatching(int sequenceId)
        {
            if (_hasPendingRequest && _pendingRequest.SequenceId == sequenceId)
            {
                _pendingRequest = default;
                _hasPendingRequest = false;
            }

            if (_hasInflightRequest && _inflightRequest.SequenceId == sequenceId)
            {
                _inflightRequest = default;
                _hasInflightRequest = false;
            }
        }

        private static string ResolveExistingThumbnailPath(string slotName)
        {
            string primaryPath = GetThumbnailPath(slotName);
            if (File.Exists(primaryPath))
                return primaryPath;

            return GetLegacyThumbnailPath(slotName);
        }

        private static string GetLegacyThumbnailPath(string slotName)
        {
            return HectonPersistentPathPolicy.CombineFile(ResolveThumbnailFileStem(slotName) + LegacyExtension);
        }

        private static string ResolveThumbnailFileStem(string slotName)
        {
            return SaveManager.ResolveSafeSlotFileStem(slotName);
        }

        private static void ClearCacheEntry(string slotName)
        {
            if (_textureCache.TryGetValue(slotName, out Texture2D cached))
            {
                if (cached != null)
                    UnityEngine.Object.Destroy(cached);

                RemoveCacheEntry(slotName);
            }
        }

        private static void AddCacheEntry(string slotName, Texture2D texture)
        {
            if (_textureCache.TryGetValue(slotName, out Texture2D existing))
            {
                if (existing != null && existing != texture)
                    UnityEngine.Object.Destroy(existing);
            }

            _textureCache[slotName] = texture;
            MarkCacheEntryAsMostRecent(_textureCacheOrder, slotName);
            TrimCacheToLimit();
        }

        private static void RemoveCacheEntry(string slotName)
        {
            _textureCache.Remove(slotName);

            for (int i = 0; i < _textureCacheOrder.Count; i++)
            {
                if (string.Equals(_textureCacheOrder[i], slotName, StringComparison.OrdinalIgnoreCase))
                {
                    _textureCacheOrder.RemoveAt(i);
                    return;
                }
            }
        }

        private static void TrimCacheToLimit()
        {
            while (_textureCacheOrder.Count > MaxCachedTextures)
            {
                string oldestSlotName = _textureCacheOrder[0];
                _textureCacheOrder.RemoveAt(0);

                if (!_textureCache.TryGetValue(oldestSlotName, out Texture2D cached))
                    continue;

                _textureCache.Remove(oldestSlotName);
                if (cached == null)
                    continue;

                UnityEngine.Object.Destroy(cached);
            }
        }

        private static void MarkCacheEntryAsMostRecent(List<string> cacheOrder, string slotName)
        {
            for (int i = 0; i < cacheOrder.Count; i++)
            {
                if (!string.Equals(cacheOrder[i], slotName, StringComparison.OrdinalIgnoreCase))
                    continue;

                cacheOrder.RemoveAt(i);
                break;
            }

            cacheOrder.Add(slotName);
        }
    }
}
