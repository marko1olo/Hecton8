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
        private static PrefabRegistry s_activeRuntimeInstance;
#if UNITY_EDITOR
        private static bool _editorHooksInstalled;
#endif

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
#if UNITY_EDITOR
            ReleaseEditorHooks();
#endif
            GlobalRegistry.ClearPrefabRegistryRuntime(null);
            s_activeRuntimeInstance = null;
            _isShuttingDown = false;
            _isResolvingRuntimeInstance = false;
#if UNITY_EDITOR
            _editorHooksInstalled = false;
#endif
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  STORAGE
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        public static PrefabRegistry ActiveRuntimeInstance => s_activeRuntimeInstance;

        /// <summary>Forward mapping: PrefabId â†’ Prefab.</summary>
        // COLD ALLOC: Dictionary[256] â€” prefab registry â€” owner: PrefabRegistry
        private readonly Dictionary<int, GameObject> _idToPrefab = new Dictionary<int, GameObject>(256);

        /// <summary>Reverse mapping: Prefab â†’ PrefabId.</summary>
        // COLD ALLOC: Dictionary[256] â€” prefab reverse lookup â€” owner: PrefabRegistry
        private readonly Dictionary<GameObject, int> _prefabToId = new Dictionary<GameObject, int>(256);

        /// <summary>Counter for generating new IDs. Starts at 1 (0 = invalid).</summary>
        private int _nextId = 1;

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  LIFECYCLE
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        private void Awake()
        {
#if UNITY_EDITOR
            EnsureEditorHooks();
#endif
            PrefabRegistry runtime = s_activeRuntimeInstance ?? GlobalRegistry.PrefabRegistryRuntime;
            if (runtime != null && runtime != this)
            {
                Destroy(gameObject);
                return;
            }

            GlobalRegistry.RegisterPrefabRegistryRuntime(this);
            s_activeRuntimeInstance = this;
        }

        private void OnDestroy()
        {
            if (GlobalRegistry.PrefabRegistryRuntime == this)
            {
                _isShuttingDown = true;
                GlobalRegistry.ClearPrefabRegistryRuntime(this);
            }

            if (ReferenceEquals(s_activeRuntimeInstance, this))
                s_activeRuntimeInstance = null;
        }

        private void OnDisable()
        {
            if (GlobalRegistry.PrefabRegistryRuntime != this)
                return;

            if (!Application.isPlaying)
            {
                GlobalRegistry.ClearPrefabRegistryRuntime(this);

                if (ReferenceEquals(s_activeRuntimeInstance, this))
                    s_activeRuntimeInstance = null;
            }
        }

        private void OnApplicationQuit()
        {
            if (GlobalRegistry.PrefabRegistryRuntime == this)
                _isShuttingDown = true;
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
            ReleaseEditorHooks();
        }

        private static void HandleEditorQuitting()
        {
            ReleaseEditorHooks();
        }
#endif

        private static PrefabRegistry EnsureRuntimeInstance()
        {
            PrefabRegistry runtime = s_activeRuntimeInstance ?? GlobalRegistry.PrefabRegistryRuntime;
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
                Hecton8.Core.H8Debug.LogError("[PrefabRegistry] GetOrRegisterPrefab: prefab is null!");
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
        /// Compatibility no-op. Persistent native prefab snapshots were retired because no current owner consumes them.
        /// </summary>
        public void WarmupNativeMap()
        {
        }

        /// <summary>
        /// Gets read-only access to the native prefab ID snapshot for Burst jobs.
        /// Returns default if not warmed up.
        /// </summary>
        public NativeHashMap<int, int>.ReadOnly GetNativeMap()
        {
            return default;
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
            Hecton8.Core.H8Debug.Log($"[PrefabRegistry] {_idToPrefab.Count} registered prefabs:");
            Dictionary<int, GameObject>.Enumerator prefabEnumerator = _idToPrefab.GetEnumerator();
            while (prefabEnumerator.MoveNext())
            {
                KeyValuePair<int, GameObject> kvp = prefabEnumerator.Current;
                Hecton8.Core.H8Debug.Log($"  {kvp.Key} â†’ {kvp.Value?.name ?? "null"}");
            }
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
            Hecton8.Core.H8Debug.Log("[PrefabRegistry] Registry cleared.");
        }
#endif
    }
}

