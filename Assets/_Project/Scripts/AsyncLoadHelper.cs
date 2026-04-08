// ============================================================================
// HECTON-8 - AsyncLoadHelper.cs
// Legacy compatibility wrapper for the removed runtime Resources load path.
// ============================================================================

using System;
using UnityEngine;

namespace Hecton8.Core
{
    /// <summary>
    /// Single async asset load request.
    /// Struct-based request definition kept for API compatibility.
    /// </summary>
    public struct LoadRequest
    {
        /// <summary>
        /// Resource path or address identifier.
        /// </summary>
        public string path;

        /// <summary>
        /// Asset type to load.
        /// </summary>
        public Type assetType;

        /// <summary>
        /// Load priority metadata.
        /// </summary>
        public int priority;

        /// <summary>
        /// Callback when this request completes.
        /// </summary>
        public Action<UnityEngine.Object> onComplete;

        /// <summary>
        /// Unique request identifier.
        /// </summary>
        public int requestId;
    }

    /// <summary>
    /// Result of a completed load request.
    /// Returned via batch callbacks.
    /// </summary>
    public struct LoadResult
    {
        public string path;
        public Type assetType;
        public UnityEngine.Object asset;
        public int requestId;
        public bool success;
    }

    /// <summary>
    /// Legacy async asset load helper.
    /// Runtime Resources loading is intentionally disabled by project policy.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(7500)]
    public sealed class AsyncLoadHelper : MonoBehaviour, ITickable
    {
        private static AsyncLoadHelper _instance;
        private static bool _unsupportedLoadErrorLogged;
        private static int _nextRequestId = 1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _instance = null;
            _unsupportedLoadErrorLogged = false;
            _nextRequestId = 1;
        }

        /// <summary>
        /// Gets or creates the helper instance for legacy callers that still access the singleton.
        /// </summary>
        public static AsyncLoadHelper Instance
        {
            get
            {
                if (_instance == null)
                {
                    GameObject go = new GameObject("[AsyncLoadHelper]");
                    _instance = go.AddComponent<AsyncLoadHelper>();
                    go.hideFlags = HideFlags.HideInHierarchy;
                    DontDestroyOnLoad(go);
                }

                return _instance;
            }
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
        }

        /// <summary>
        /// Legacy interface method retained for compatibility.
        /// </summary>
        public void Tick(float deltaTime)
        {
            // Intentionally empty. The old polling path depended on forbidden runtime asset loading.
        }

        /// <summary>
        /// Fails immediately because runtime Resources loading is disabled.
        /// </summary>
        public static int LoadAssetAsync<T>(string path, Action<T> onComplete) where T : UnityEngine.Object
        {
            return LoadAssetAsync(path, typeof(T), asset => onComplete?.Invoke(asset as T));
        }

        /// <summary>
        /// Fails immediately because runtime Resources loading is disabled.
        /// </summary>
        public static int LoadAssetAsync(string path, Type assetType, Action<UnityEngine.Object> onComplete)
        {
            int requestId = _nextRequestId++;
            LogUnsupportedLoad(path, assetType, 1);
            onComplete?.Invoke(null);
            return requestId;
        }

        /// <summary>
        /// Fails immediately for every request because runtime Resources loading is disabled.
        /// </summary>
        public static void BatchLoadAsync(LoadRequest[] requests, Action<LoadResult[]> onBatchComplete = null)
        {
            if (requests == null || requests.Length == 0)
            {
                onBatchComplete?.Invoke(Array.Empty<LoadResult>());
                return;
            }

            // COLD ALLOC: mirror caller request count for explicit failure reporting.
            LoadResult[] results = new LoadResult[requests.Length];
            LogUnsupportedLoad("batch", null, requests.Length);

            for (int i = 0; i < requests.Length; i++)
            {
                LoadRequest request = requests[i];
                if (request.requestId == 0)
                {
                    request.requestId = _nextRequestId++;
                }

                results[i] = new LoadResult
                {
                    path = request.path,
                    assetType = request.assetType,
                    asset = null,
                    requestId = request.requestId,
                    success = false
                };

                request.onComplete?.Invoke(null);
            }

            onBatchComplete?.Invoke(results);
        }

        /// <summary>
        /// Legacy cache API retained for compatibility. No cache exists anymore.
        /// </summary>
        public static void UnloadAsset(string path)
        {
        }

        /// <summary>
        /// Legacy cache API retained for compatibility. No cache exists anymore.
        /// </summary>
        public static void ClearCache()
        {
        }

        /// <summary>
        /// Returns zero because the Resources-based cache was removed.
        /// </summary>
        public static int GetCachedAssetCount() => 0;

        /// <summary>
        /// Returns zero because no async runtime load requests are tracked anymore.
        /// </summary>
        public static int GetPendingLoadCount() => 0;

        private static void LogUnsupportedLoad(string path, Type assetType, int requestCount)
        {
            if (_unsupportedLoadErrorLogged)
            {
                return;
            }

            _unsupportedLoadErrorLogged = true;
            string typeName = assetType != null ? assetType.Name : "Unknown";
            Debug.LogError(
                $"AsyncLoadHelper is disabled. Runtime Resources/Addressables loading is not available in this project. " +
                $"Requests: {requestCount}. Path: {path}. Type: {typeName}. " +
                "Use scene-owned references, ObjectPoolManager, or an approved async content pipeline.");
        }
    }
}
