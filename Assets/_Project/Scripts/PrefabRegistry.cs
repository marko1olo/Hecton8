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
//
// UNITY 6.4+ COMPATIBILITY:
//   â€¢ GetEntityId() replaces the obsolete object-instance path in Unity 6.4+.
//   â€¢ Uses EntityId where available, stable hash fallback otherwise.
//   â€¢ Conditional compilation: #if UNITY_6000_4_OR_NEWER
//
// ZERO GC:
//   â€¢ Dictionary lookups â€” O(1), no allocations.
//   â€¢ No string operations in hot paths.
// ============================================================================

using System.Collections.Generic;
using System.Runtime.CompilerServices;
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

        public static PrefabRegistry ActiveRuntimeInstance =>
            IsPrefabRegistryRuntimeUsable(s_activeRuntimeInstance)
                ? s_activeRuntimeInstance
                : ResolveUsableRuntime();

        internal static bool TryResolveActiveRuntime(ref PrefabRegistry target)
        {
            PrefabRegistry runtime = ActiveRuntimeInstance;
            if (runtime == null)
            {
                target = null;
                return false;
            }

            if (!ReferenceEquals(target, runtime))
                target = runtime;

            return true;
        }

        /// <summary>Forward mapping: PrefabId â†’ Prefab.</summary>
        // COLD ALLOC: Dictionary[256] â€” prefab registry â€” owner: PrefabRegistry
        private readonly Dictionary<int, GameObject> _idToPrefab = new Dictionary<int, GameObject>(256);

        /// <summary>Reverse mapping: Prefab â†’ PrefabId.</summary>
        // COLD ALLOC: Dictionary[256] â€” prefab reverse lookup â€” owner: PrefabRegistry
        private readonly Dictionary<GameObject, int> _prefabToId = new Dictionary<GameObject, int>(256);

        /// <summary>Counter for generating new IDs. Starts at 1 (0 = invalid).</summary>
        private int _nextId = 1;
        private bool _runtimeOwnerAborted;

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  LIFECYCLE
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        private void Awake()
        {
#if UNITY_EDITOR
            EnsureEditorHooks();
#endif
            if (!EnsureRuntimeOwnership())
                return;
        }

        private void OnDestroy()
        {
            if (_runtimeOwnerAborted)
            {
                ClearRuntimeMirrorIfOwnedBy(this);
                return;
            }

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
            if (_runtimeOwnerAborted)
            {
                ClearRuntimeMirrorIfOwnedBy(this);
                return;
            }

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

            AssemblyReloadEvents.beforeAssemblyReload -= HandleBeforeAssemblyReload;
            AssemblyReloadEvents.beforeAssemblyReload += HandleBeforeAssemblyReload;
            EditorApplication.quitting -= HandleEditorQuitting;
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

        internal static PrefabRegistry EnsureRuntimeInstance()
        {
            PrefabRegistry runtime = ResolveUsableRuntime();
            if (runtime != null || _isResolvingRuntimeInstance || !Application.isPlaying || _isShuttingDown)
                return runtime;

            _isResolvingRuntimeInstance = true;
            try
            {

                // COLD ALLOC: GameObject[1] — runtime prefab registry fallback when direct bootstrap path is missing — owner: PrefabRegistry
                // Player-build construction path: no authored/bootstrap instance reachable.
                // Prefab catalog owns cold prefab lookups; without create, spawn/resolve
                // paths miss the registry when bootstrap reorders or skips EnsurePrefabRegistry.
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
        /// Ensures this component owns both prefab registry runtime mirrors.
        /// </summary>
        private bool EnsureRuntimeOwnership()
        {
            if (_runtimeOwnerAborted)
                return false;

            if (TryAbortForUsableExistingRuntime())
                return false;

            // Ask before aborting anyone. PrefabRegistry has no GlobalRegistryServiceSlot, so its slot
            // resolves to Unknown, which is never scene-runtime hot-swappable. Once the registry is
            // ready-locked the registration below is guaranteed to throw, and both abort blocks between
            // here and there would already have retired the live registry and cleared its mirrors -
            // leaving no prefab registry at all, which strands every prefab lookup behind it.
            //
            // Only when a real takeover is needed: if this instance already owns the registry slot, the
            // registration early-returns on reference equality and never reaches the guard.
            if (!ReferenceEquals(GlobalRegistry.PrefabRegistryRuntime, this) &&
                !GlobalRegistry.IsRuntimeServicePublicationOpen<PrefabRegistry>())
            {
                _runtimeOwnerAborted = true;
                return false;
            }

            PrefabRegistry runtime = s_activeRuntimeInstance;
            if (!ReferenceEquals(runtime, null) && !ReferenceEquals(runtime, this))
            {
                runtime._runtimeOwnerAborted = true;
                ClearRuntimeMirrorIfOwnedBy(runtime);
            }

            runtime = GlobalRegistry.PrefabRegistryRuntime;
            if (!ReferenceEquals(runtime, null) && !ReferenceEquals(runtime, this))
            {
                runtime._runtimeOwnerAborted = true;
                ClearRuntimeMirrorIfOwnedBy(runtime);
            }

            if (TryAbortForUsableExistingRuntime())
                return false;

            GlobalRegistry.RegisterPrefabRegistryRuntime(this);
            if (ReferenceEquals(GlobalRegistry.PrefabRegistryRuntime, this))
                s_activeRuntimeInstance = this;

            bool ownsRuntime =
                ReferenceEquals(s_activeRuntimeInstance, this) &&
                ReferenceEquals(GlobalRegistry.PrefabRegistryRuntime, this);
            _runtimeOwnerAborted = !ownsRuntime;
            if (_runtimeOwnerAborted)
                Destroy(gameObject);
            return ownsRuntime;
        }

        private bool TryAbortForUsableExistingRuntime()
        {
            PrefabRegistry runtime = s_activeRuntimeInstance;
            if (!ReferenceEquals(runtime, null) && !ReferenceEquals(runtime, this))
            {
                if (IsPrefabRegistryRuntimeUsable(runtime))
                {
                    _runtimeOwnerAborted = true;
                    Destroy(gameObject);
                    return true;
                }

                runtime._runtimeOwnerAborted = true;
                ClearRuntimeMirrorIfOwnedBy(runtime);
            }

            runtime = GlobalRegistry.PrefabRegistryRuntime;
            if (!ReferenceEquals(runtime, null) && !ReferenceEquals(runtime, this))
            {
                if (IsPrefabRegistryRuntimeUsable(runtime))
                {
                    _runtimeOwnerAborted = true;
                    Destroy(gameObject);
                    return true;
                }

                runtime._runtimeOwnerAborted = true;
                ClearRuntimeMirrorIfOwnedBy(runtime);
            }

            return false;
        }

        private static PrefabRegistry ResolveUsableRuntime()
        {
            PrefabRegistry runtime = s_activeRuntimeInstance;
            if (IsPrefabRegistryRuntimeUsable(runtime))
                return runtime;

            ClearRuntimeMirrorIfOwnedBy(runtime);

            runtime = GlobalRegistry.PrefabRegistryRuntime;
            if (IsPrefabRegistryRuntimeUsable(runtime))
            {
                s_activeRuntimeInstance = runtime;
                return runtime;
            }

            ClearRuntimeMirrorIfOwnedBy(runtime);
            return null;
        }

        private static bool IsPrefabRegistryRuntimeUsable(PrefabRegistry runtime)
        {
            return runtime != null &&
                   runtime.isActiveAndEnabled &&
                   !runtime._runtimeOwnerAborted;
        }

        private static void ClearRuntimeMirrorIfOwnedBy(PrefabRegistry runtime)
        {
            if (ReferenceEquals(runtime, null))
                return;

            GlobalRegistry.ClearPrefabRegistryRuntime(runtime);
            if (ReferenceEquals(s_activeRuntimeInstance, runtime))
                s_activeRuntimeInstance = null;
        }

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
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

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

