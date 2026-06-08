using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hecton8.Audio
{
    /// <summary>
    /// Scene-level authored anchor that exposes the active music config without runtime scene searches.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-3950)]
    public sealed class HectonMusicDirectorAnchor : MonoBehaviour
    {
        [Tooltip("Authored config used by HectonMusicDirector in the current scene.")]
        [SerializeField] private HectonMusicDirectorConfig _config;

        // COLD ALLOC: List[4] — active scene-local music anchors — owner: HectonMusicDirectorAnchor
        private static readonly List<HectonMusicDirectorAnchor> _activeAnchors = new List<HectonMusicDirectorAnchor>(4);
        private static HectonMusicDirectorAnchor _activeRuntimeInstance;

        /// <summary>
        /// Currently active runtime anchor.
        /// </summary>
        public static HectonMusicDirectorAnchor ActiveRuntimeInstance => _activeRuntimeInstance;

        public static bool TryResolveActiveRuntime(ref HectonMusicDirectorAnchor target)
        {
            HectonMusicDirectorAnchor active = _activeRuntimeInstance;
            if (!IsLiveAnchor(active))
                active = FindFirstLiveAnchor();

            if (!IsLiveAnchor(active))
            {
                target = null;
                return false;
            }

            if (!ReferenceEquals(target, active))
                target = active;

            return true;
        }

        /// <summary>
        /// Authored config used by the music director in the current scene.
        /// </summary>
        public HectonMusicDirectorConfig Config => _config;

        /// <summary>
        /// Resolves the authored config belonging to the requested scene.
        /// </summary>
        public static bool TryResolveConfigForScene(Scene scene, out HectonMusicDirectorConfig config)
        {
            config = null;
            if (!scene.IsValid())
                return false;

            for (int i = 0; i < _activeAnchors.Count; i++)
            {
                HectonMusicDirectorAnchor anchor = _activeAnchors[i];
                if (!IsLiveAnchor(anchor))
                    continue;

                if (anchor.gameObject.scene != scene)
                    continue;

                if (anchor._config == null)
                    continue;

                config = anchor._config;
                return true;
            }

            return false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _activeAnchors.Clear();
            _activeRuntimeInstance = null;
        }

        private void Awake()
        {
            _activeRuntimeInstance = this;
        }

        private void OnEnable()
        {
            RegisterAnchor(this);
            _activeRuntimeInstance = this;
        }

        private void Start()
        {
            HectonMusicDirector.EnsureRuntimeInstanceForScene(gameObject.scene);
        }

        private void OnDisable()
        {
            UnregisterAnchor(this);
            if (ReferenceEquals(_activeRuntimeInstance, this))
                RefreshActiveRuntimeInstance();
        }

        private void OnDestroy()
        {
            UnregisterAnchor(this);
            if (ReferenceEquals(_activeRuntimeInstance, this))
                RefreshActiveRuntimeInstance();
        }

        private static void RegisterAnchor(HectonMusicDirectorAnchor anchor)
        {
            if (anchor == null)
                return;

            for (int i = 0; i < _activeAnchors.Count; i++)
            {
                if (_activeAnchors[i] == anchor)
                    return;
            }

            _activeAnchors.Add(anchor);
        }

        private static void UnregisterAnchor(HectonMusicDirectorAnchor anchor)
        {
            if (anchor == null)
                return;

            for (int i = _activeAnchors.Count - 1; i >= 0; i--)
            {
                if (_activeAnchors[i] == anchor)
                    _activeAnchors.RemoveAt(i);
            }
        }

        private static void RefreshActiveRuntimeInstance()
        {
            _activeRuntimeInstance = FindFirstLiveAnchor();
        }

        private static HectonMusicDirectorAnchor FindFirstLiveAnchor()
        {
            for (int i = _activeAnchors.Count - 1; i >= 0; i--)
            {
                HectonMusicDirectorAnchor anchor = _activeAnchors[i];
                if (IsLiveAnchor(anchor))
                    return anchor;
            }

            return null;
        }

        private static bool IsLiveAnchor(HectonMusicDirectorAnchor anchor)
        {
            return anchor != null && anchor.gameObject != null && anchor.isActiveAndEnabled;
        }
    }
}
