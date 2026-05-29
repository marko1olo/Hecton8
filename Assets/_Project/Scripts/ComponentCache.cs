// ============================================================================
// HECTON-8 - ComponentCache.cs
// Explicit cold component caching utility.
//
// Value and HasComponent are pure cached reads. Component discovery is only
// performed by TryRefreshCold/Refresh and must stay in Awake, OnEnable, bootstrap,
// or another cold owner phase.
// ============================================================================

using UnityEngine;

namespace Hecton8.Core
{
    /// <summary>
    /// Zero-allocation cache for a single component reference.
    /// Store as a field; do not use as a transient local.
    /// </summary>
    public struct ComponentCache<T> where T : Component
    {
        private GameObject _gameObject;
        private T _cachedComponent;
        private bool _isCached;

        /// <summary>
        /// Initializes a cache handle for a component on a target GameObject.
        /// Does not query the component; call TryRefreshCold from a cold phase.
        /// </summary>
        public ComponentCache(GameObject target)
        {
            _gameObject = target;
            _cachedComponent = null;
            _isCached = false;
        }

        /// <summary>
        /// Initializes a cache handle for a component on a target transform.
        /// Does not query the component; call TryRefreshCold from a cold phase.
        /// </summary>
        public ComponentCache(Transform target)
            : this(target != null ? target.gameObject : null)
        {
        }

        /// <summary>
        /// Gets the cached component reference.
        /// Pure read: never searches the scene, queries components, or mutates global state.
        /// </summary>
        public T Value
        {
            get { return _isCached ? _cachedComponent : null; }
        }

        /// <summary>
        /// Checks whether a component reference has been explicitly cached.
        /// Pure read.
        /// </summary>
        public bool HasComponent
        {
            get { return _isCached && _cachedComponent != null; }
        }

        /// <summary>
        /// Returns the currently cached component without performing component discovery.
        /// </summary>
        public bool TryGetCached(out T component)
        {
            component = Value;
            return component != null;
        }

        /// <summary>
        /// Manually invalidates the cache after a known component topology change.
        /// Next Value access returns null until TryRefreshCold is called.
        /// </summary>
        public void Invalidate()
        {
            _isCached = false;
            _cachedComponent = null;
        }

        /// <summary>
        /// Performs an explicit cold component query and updates the cached value.
        /// Do not call from Tick, FixedTick, LateFrameTick, Execute, or VISUAL_SYNC.
        /// </summary>
        public bool TryRefreshCold()
        {
            _cachedComponent = null;
            _isCached = true;

            if (_gameObject == null)
                return false;

            return _gameObject.TryGetComponent(out _cachedComponent);
        }

        /// <summary>
        /// Legacy explicit refresh wrapper. Cold phases only.
        /// </summary>
        public void Refresh()
        {
            TryRefreshCold();
        }
    }

    /// <summary>
    /// Extension methods for convenient ComponentCache creation.
    /// </summary>
    public static class ComponentCacheExtensions
    {
        /// <summary>
        /// Creates a ComponentCache for this GameObject.
        /// </summary>
        public static ComponentCache<T> CreateComponentCache<T>(this GameObject go)
            where T : Component
        {
            return new ComponentCache<T>(go);
        }

        /// <summary>
        /// Creates a ComponentCache for this Transform.
        /// </summary>
        public static ComponentCache<T> CreateComponentCache<T>(this Transform tr)
            where T : Component
        {
            return new ComponentCache<T>(tr);
        }

        /// <summary>
        /// Creates a ComponentCache for this Component's GameObject.
        /// </summary>
        public static ComponentCache<T> CreateComponentCache<T>(this Component comp)
            where T : Component
        {
            return new ComponentCache<T>(comp != null ? comp.gameObject : null);
        }
    }
}
