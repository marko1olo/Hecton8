// ============================================================================
// HECTON-8 - WorldLODSceneBootstrap.cs
// Scene-level integration bridge for LODSystemManager.
// Registers authored LODGroup components present in the active scene.
// ============================================================================

using System.Collections.Generic;
using Hecton8.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hecton8.World
{
    /// <summary>
    /// Bridges scene-authored <see cref="LODGroup"/> components into <see cref="LODSystemManager"/>.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(LODSystemManager))]
    public sealed class WorldLODSceneBootstrap : MonoBehaviour
    {
        [Header("Scene Registration")]
        [SerializeField, Tooltip("Register scene LODGroups automatically on Start.")]
        private bool _autoRegisterOnStart = true;

        [SerializeField, Tooltip("Include inactive scene LODGroups in the startup registration scan.")]
        private bool _includeInactiveLODGroups;

        [SerializeField, Tooltip("Also register tracked LODGroup roots with CullingManager when available.")]
        private bool _registerWithCullingManager = true;

        private LODSystemManager _lodSystemManager;
        private CullingManager _cullingManager;
        private string _authoringScenePath;
        private string _authoringSceneName;

        // COLD ALLOC: List<LODGroup>[256] - tracked scene registrations for clean unregister - owner: WorldLODSceneBootstrap
        private readonly List<LODGroup> _trackedLODGroups = new List<LODGroup>(256);
        // COLD ALLOC: List<GameObject>[128] - root-object scan buffer for target scene traversal - owner: WorldLODSceneBootstrap
        private readonly List<GameObject> _sceneRootBuffer = new List<GameObject>(128);
        // COLD ALLOC: List<LODGroup>[512] - reusable scene scan buffer to avoid global FindObjects allocations - owner: WorldLODSceneBootstrap
        private readonly List<LODGroup> _sceneLODGroupBuffer = new List<LODGroup>(512);

        /// <summary>
        /// Number of scene-authored LOD groups currently tracked by this bootstrap.
        /// </summary>
        public int RegisteredSceneLODGroupCount => _trackedLODGroups.Count;

        private void Awake()
        {
            TryGetComponent(out _lodSystemManager);
            Scene authoringScene = gameObject.scene;
            _authoringScenePath = authoringScene.path;
            _authoringSceneName = authoringScene.name;
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        private void Start()
        {
            if (_autoRegisterOnStart)
            {
                RebuildSceneRegistration();
            }
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            UnregisterTrackedLODGroups();
        }

        /// <summary>
        /// Rebuilds scene LOD registration by scanning the current scene for authored <see cref="LODGroup"/> components.
        /// </summary>
        public void RebuildSceneRegistration()
        {
            if (_lodSystemManager == null && !TryGetComponent(out _lodSystemManager))
            {
                Debug.LogError("[WorldLODSceneBootstrap] LODSystemManager missing. Bootstrap disabled.");
                enabled = false;
                return;
            }

            UnregisterTrackedLODGroups();

            Scene currentScene = ResolveTargetScene();
            if (!currentScene.IsValid())
            {
                Debug.LogError("[WorldLODSceneBootstrap] Target scene is invalid. Registration aborted.");
                return;
            }

            _sceneRootBuffer.Clear();
            currentScene.GetRootGameObjects(_sceneRootBuffer);

            for (int rootIndex = 0; rootIndex < _sceneRootBuffer.Count; rootIndex++)
            {
                GameObject root = _sceneRootBuffer[rootIndex];
                if (root == null)
                    continue;

                _sceneLODGroupBuffer.Clear();
                root.GetComponentsInChildren(_includeInactiveLODGroups, _sceneLODGroupBuffer);

                for (int i = 0; i < _sceneLODGroupBuffer.Count; i++)
                {
                    LODGroup lodGroup = _sceneLODGroupBuffer[i];
                    if (lodGroup == null)
                        continue;

                    if (lodGroup.gameObject.scene != currentScene)
                        continue;

                    if (WorldShippingContentFilter.IsSuppressedByHierarchy(lodGroup.transform))
                        continue;

                    int beforeCount = _lodSystemManager.RegisteredLODGroupCount;
                    _lodSystemManager.RegisterLODGroup(lodGroup);
                    if (_lodSystemManager.RegisteredLODGroupCount > beforeCount)
                    {
                        _trackedLODGroups.Add(lodGroup);

                        if (_registerWithCullingManager)
                        {
                            _cullingManager ??= GlobalRegistry.Culling;
                            if (_cullingManager != null)
                            {
                                _cullingManager.RegisterCullableObject(lodGroup.gameObject);
                            }
                        }
                    }
                }
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Hecton8.Core.H8Debug.Log($"[WorldLODSceneBootstrap] Registered {_trackedLODGroups.Count} LODGroup components for scene '{currentScene.name}'.");
#endif
        }

        private void UnregisterTrackedLODGroups()
        {
            if (_trackedLODGroups.Count == 0)
                return;

            if (_lodSystemManager != null)
            {
                for (int i = _trackedLODGroups.Count - 1; i >= 0; i--)
                {
                    LODGroup lodGroup = _trackedLODGroups[i];
                    if (lodGroup != null)
                    {
                        _lodSystemManager.UnregisterLODGroup(lodGroup);

                        if (_registerWithCullingManager)
                        {
                            _cullingManager ??= GlobalRegistry.Culling;
                            if (_cullingManager != null)
                            {
                                _cullingManager.UnregisterCullableObject(lodGroup.gameObject);
                            }
                        }
                    }
                }
            }

            _trackedLODGroups.Clear();
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!_autoRegisterOnStart)
                return;

            if (!SceneMatchesAuthoringScene(scene))
                return;

            RebuildSceneRegistration();
        }

        private Scene ResolveTargetScene()
        {
            if (!string.IsNullOrEmpty(_authoringScenePath))
            {
                Scene sceneByPath = SceneManager.GetSceneByPath(_authoringScenePath);
                if (sceneByPath.IsValid() && sceneByPath.isLoaded)
                {
                    return sceneByPath;
                }
            }

            if (!string.IsNullOrEmpty(_authoringSceneName))
            {
                Scene sceneByName = SceneManager.GetSceneByName(_authoringSceneName);
                if (sceneByName.IsValid() && sceneByName.isLoaded)
                {
                    return sceneByName;
                }
            }

            return SceneManager.GetActiveScene();
        }

        private bool SceneMatchesAuthoringScene(Scene scene)
        {
            if (!string.IsNullOrEmpty(_authoringScenePath))
            {
                return string.Equals(scene.path, _authoringScenePath, System.StringComparison.Ordinal);
            }

            return string.Equals(scene.name, _authoringSceneName, System.StringComparison.Ordinal);
        }
    }
}
