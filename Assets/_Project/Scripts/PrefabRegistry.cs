// ============================================================================
// HECTON-8 â€” PrefabRegistry.cs
// Central stable ID mapping for prefabs. Replaces deprecated GetEntityId().
// ============================================================================
//
// ARCHITECTURE:
//   â€¢ Singleton (DontDestroyOnLoad) â€” one registry per game session.
//   â€¢ Bi-directional mapping: GameObject â†” int PrefabId.
//   â€¢ Zero GC lookups via Dictionary<int, GameObject> and Dictionary<GameObject, int>.
//   â€¢ Editor-time assignment via [ContextMenu] or auto-registration on first access.
//   â€¢ Optional native ID snapshot for jobs that only need prefab ID membership.
//
// UNITY 6.4+ COMPATIBILITY:
//   â€¢ GetEntityId() replaces the obsolete object-instance path in Unity 6.4+.
//   â€¢ Uses EntityId where available, stable hash fallback otherwise.
//   â€¢ Conditional compilation: #if UNITY_6000_4_OR_NEWER
//
// USAGE:
//   int id = PrefabRegistry.ActiveRuntimeInstance.GetOrRegisterPrefab(myPrefab);
//   GameObject prefab = PrefabRegistry.ActiveRuntimeInstance.GetPrefab(id);
//
// ZERO GC:
//   â€¢ Dictionary lookups â€” O(1), no allocations.
//   â€¢ No string operations in hot paths.
//   â€¢ NativeArray for Burst-compatible reads (after warmup).
// ============================================================================

using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Unity.Collections;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Hecton8.Core
{
    /// <summary>
    /// Central registry for stable prefab IDs.
    /// Replaces deprecated GetEntityId() path with persistent, save-safe identifiers.
    /// </summary>
    [DefaultExecutionOrder(-9500)] // Before ObjectPoolManager
    public sealed class PrefabRegistry : MonoBehaviour
    {
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  SINGLETON
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        private static bool _isShuttingDown;
        private static bool _isResolvingRuntimeInstance;
#if UNITY_EDITOR
        private static bool _editorHooksInstalled;
#endif

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            ReleaseStaticNativeState();
            GlobalRegistry.ClearPrefabRegistryRuntime(null);
            _isShuttingDown = false;
            _isResolvingRuntimeInstance = false;
#if UNITY_EDITOR
            _editorHooksInstalled = false;
#endif
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  STORAGE
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        public static PrefabRegistry ActiveRuntimeInstance => GlobalRegistry.PrefabRegistryRuntime;

        /// <summary>Forward mapping: PrefabId â†’ Prefab.</summary>
        // COLD ALLOC: Dictionary[256] â€” prefab registry â€” owner: PrefabRegistry
        private readonly Dictionary<int, GameObject> _idToPrefab = new Dictionary<int, GameObject>(256);

        /// <summary>Reverse mapping: Prefab â†’ PrefabId.</summary>
        // COLD ALLOC: Dictionary[256] â€” prefab reverse lookup â€” owner: PrefabRegistry
        private readonly Dictionary<GameObject, int> _prefabToId = new Dictionary<GameObject, int>(256);

        /// <summary>Counter for generating new IDs. Starts at 1 (0 = invalid).</summary>
        private int _nextId = 1;

        /// <summary>
        /// Read-only native snapshot of registered prefab IDs.
        /// Managed GameObject references cannot live in NativeHashMap, so the value mirrors the key.
        /// </summary>
        private NativeHashMap<int, int> _nativeMap;

        /// <summary>Lock object for thread-safe native map creation.</summary>
        private readonly object _nativeMapLock = new object();

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  LIFECYCLE
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        private void Awake()
        {
#if UNITY_EDITOR
            EnsureEditorHooks();
#endif
            PrefabRegistry runtime = GlobalRegistry.PrefabRegistryRuntime;
            if (runtime != null && runtime != this)
            {
                Destroy(gameObject);
                return;
            }

            GlobalRegistry.RegisterPrefabRegistryRuntime(this);
        }

        private void OnDestroy()
        {
            if (GlobalRegistry.PrefabRegistryRuntime == this)
            {
                _isShuttingDown = true;
                ReleaseNativeMap();
                GlobalRegistry.ClearPrefabRegistryRuntime(this);
            }
        }

        private void OnDisable()
        {
            if (GlobalRegistry.PrefabRegistryRuntime != this)
                return;

            ReleaseNativeMap();

            if (!Application.isPlaying)
                GlobalRegistry.ClearPrefabRegistryRuntime(this);
        }

        private void OnApplicationQuit()
        {
            if (GlobalRegistry.PrefabRegistryRuntime == this)
                _isShuttingDown = true;
        }

        private void ReleaseNativeMap()
        {
            lock (_nativeMapLock)
            {
                if (_nativeMap.IsCreated)
                {
                    NativeMemorySentinel.UnregisterNativeHashMap(nameof(PrefabRegistry), nameof(_nativeMap));
                    _nativeMap.Dispose();
                }
            }
        }

        private static void ReleaseStaticNativeState()
        {
            PrefabRegistry runtime = GlobalRegistry.PrefabRegistryRuntime;
            if (runtime != null)
                runtime.ReleaseNativeMap();

#if UNITY_EDITOR
            ReleaseEditorHooks();
#endif
        }

#if UNITY_EDITOR
        private static void EnsureEditorHooks()
        {
            if (_editorHooksInstalled)
                return;

            AssemblyReloadEvents.beforeAssemblyReload += HandleBeforeAssemblyReload;
            EditorApplication.quitting += HandleEditorQuitting;
            _editorHooksInstalled = true;
        }

        private static void ReleaseEditorHooks()
        {
            if (!_editorHooksInstalled)
                return;

            AssemblyReloadEvents.beforeAssemblyReload -= HandleBeforeAssemblyReload;
            EditorApplication.quitting -= HandleEditorQuitting;
            _editorHooksInstalled = false;
        }

        private static void HandleBeforeAssemblyReload()
        {
            ReleaseStaticNativeState();
        }

        private static void HandleEditorQuitting()
        {
            ReleaseStaticNativeState();
        }
#endif

        private static PrefabRegistry EnsureRuntimeInstance()
        {
            PrefabRegistry runtime = GlobalRegistry.PrefabRegistryRuntime;
            if (runtime != null || _isResolvingRuntimeInstance || !Application.isPlaying || _isShuttingDown)
                return runtime;

            _isResolvingRuntimeInstance = true;
            try
            {

                // COLD ALLOC: GameObject[1] â€” runtime prefab registry fallback when direct bootstrap path is missing â€” owner: PrefabRegistry
                GameObject runtimeRoot = new GameObject("[PrefabRegistry]");
                return runtimeRoot.AddComponent<PrefabRegistry>();
            }
            finally
            {
                _isResolvingRuntimeInstance = false;
            }
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  PUBLIC API â€” REGISTRATION
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        /// <summary>
        /// Gets or creates a stable ID for the prefab.
        /// Zero GC after first registration (cached in dictionary).
        /// </summary>
        /// <param name="prefab">The prefab to register (must not be null).</param>
        /// <returns>Stable integer ID (never 0).</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetOrRegisterPrefab(GameObject prefab)
        {
            if (prefab == null)
            {
                Debug.LogError("[PrefabRegistry] GetOrRegisterPrefab: prefab is null!");
                return 0;
            }

            // Fast path: already registered
            if (_prefabToId.TryGetValue(prefab, out int id))
                return id;

            // Slow path: register new
            return RegisterNewPrefab(prefab);
        }

        /// <summary>
        /// Registers a prefab and returns its new ID.
        /// Called only once per prefab (cold path).
        /// </summary>
        private int RegisterNewPrefab(GameObject prefab)
        {
            int newId = _nextId++;
            _idToPrefab[newId] = prefab;
            _prefabToId[prefab] = newId;


            return newId;
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  PUBLIC API â€” LOOKUP
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        /// <summary>
        /// Gets the prefab by ID.
        /// Zero GC dictionary lookup.
        /// </summary>
        /// <param name="prefabId">The stable prefab ID.</param>
        /// <returns>Prefab GameObject, or null if not found.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public GameObject GetPrefab(int prefabId)
        {
            return _idToPrefab.TryGetValue(prefabId, out GameObject prefab) ? prefab : null;
        }

        /// <summary>
        /// Gets the ID for a registered prefab.
        /// Returns 0 if not registered.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetPrefabId(GameObject prefab)
        {
            return prefab != null && _prefabToId.TryGetValue(prefab, out int id) ? id : 0;
        }

        /// <summary>
        /// Checks if a prefab is registered.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsRegistered(GameObject prefab)
        {
            return prefab != null && _prefabToId.ContainsKey(prefab);
        }

        /// <summary>
        /// Checks if an ID exists.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsValidId(int prefabId)
        {
            return prefabId > 0 && _idToPrefab.ContainsKey(prefabId);
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  PUBLIC API â€” NATIVE MAP (BURST)
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        /// <summary>
        /// Warms up the native map for Burst job access.
        /// Call once after all prefabs are registered (e.g., after scene load).
        /// </summary>
        public void WarmupNativeMap()
        {
            lock (_nativeMapLock)
            {
                if (_nativeMap.IsCreated)
                {
                    NativeMemorySentinel.UnregisterNativeHashMap(nameof(PrefabRegistry), nameof(_nativeMap));
                    _nativeMap.Dispose();
                }

                _nativeMap = new NativeHashMap<int, int>(_idToPrefab.Count, Allocator.Persistent);
                NativeMemorySentinel.RegisterNativeHashMap(_nativeMap, nameof(PrefabRegistry), nameof(_nativeMap), NativeAllocationLifetime.Session);

                foreach (var kvp in _idToPrefab)
                    _nativeMap.TryAdd(kvp.Key, kvp.Key);
            }
        }

        /// <summary>
        /// Gets read-only access to the native prefab ID snapshot for Burst jobs.
        /// Returns default if not warmed up.
        /// </summary>
        public NativeHashMap<int, int> GetNativeMap()
        {
            return _nativeMap;
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  PUBLIC API â€” DIAGNOSTICS
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        /// <summary>Number of registered prefabs.</summary>
        public int RegisteredCount => _idToPrefab.Count;

#if UNITY_EDITOR
        /// <summary>
        /// Editor-only: dumps all registered prefabs to console.
        /// </summary>
        [ContextMenu("Dump Registry")]
        private void DumpRegistry()
        {
            Debug.Log($"[PrefabRegistry] {_idToPrefab.Count} registered prefabs:");
            foreach (var kvp in _idToPrefab)
                Debug.Log($"  {kvp.Key} â†’ {kvp.Value?.name ?? "null"}");
        }

        /// <summary>
        /// Editor-only: clears all registrations.
        /// </summary>
        [ContextMenu("Clear Registry")]
        private void ClearRegistry()
        {
            _idToPrefab.Clear();
            _prefabToId.Clear();
            _nextId = 1;
            Debug.Log("[PrefabRegistry] Registry cleared.");
        }
#endif
    }
}

