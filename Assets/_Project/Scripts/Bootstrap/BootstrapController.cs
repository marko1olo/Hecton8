using Hecton8.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hecton8.Bootstrap
{
    /// <summary>
    /// Compatibility shim for legacy bootstrap references. Runtime authority is <see cref="GameBootstrapper"/>.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-30000)]
    public sealed class BootstrapController : MonoBehaviour
    {
        private const string BootstrapSceneName = "00_BOOTSTRAP";

        [Tooltip("Shader variant collections handed to GameBootstrapper for MemoryPreWarm before scene activation.")]
        [SerializeField] private ShaderVariantCollection[] shaderVariantCollections;
        [Tooltip("Explicit shader manifest for ShaderWarmup.WarmupShaderFromCollection. Must match the configured collections.")]
        [SerializeField] private Shader[] shaderWarmupShaders;
        [Tooltip("Legacy-authored shader catalog routed into the GameBootstrapper runtime owner before bootstrap.")]
        [SerializeField] private RuntimeShaderReferenceCatalog runtimeShaderReferenceCatalog;
        [Tooltip("Optional Unity 6 PSO trace file paths. Use StreamingAssets-relative paths for players; Assets/ProjectSettings paths are editor-only.")]
        [SerializeField] private string[] shaderGraphicsStateCollectionPaths;

        private bool _delegatedBoot;

        private static bool IsBootstrapScene(Scene scene)
        {
            return scene.IsValid() &&
                string.Equals(scene.name, BootstrapSceneName, System.StringComparison.Ordinal);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureRuntimeBootstrapOwner()
        {
            if (!Application.isPlaying)
                return;

            Scene activeScene = SceneManager.GetActiveScene();
            if (!IsBootstrapScene(activeScene))
                return;

            GameBootstrapper.EnsureRuntimeInstance()?.BeginBootstrap();
        }

        private void Awake()
        {
            DelegateBoot();
        }

        private void Start()
        {
            DelegateBoot();
        }

        /// <summary>
        /// Returns unified bootstrap readiness.
        /// </summary>
        public static bool AreAllSystemsReady()
        {
            return GameBootstrapper.AreAllSystemsReady();
        }

        private void DelegateBoot()
        {
            if (_delegatedBoot || !Application.isPlaying)
                return;

            Scene currentScene = gameObject.scene;
            if (!IsBootstrapScene(currentScene))
                return;

            _delegatedBoot = true;
            GameBootstrapper bootstrapper = GameBootstrapper.EnsureRuntimeInstance(gameObject);
            if (bootstrapper == null)
                return;

            ApplySerializedShaderVariantCollections(bootstrapper);
            bootstrapper.BeginBootstrap();
        }

        internal void ApplySerializedShaderVariantCollections(GameBootstrapper bootstrapper)
        {
            if (bootstrapper == null)
                return;

            bootstrapper.SetBootstrapShaderVariantCollections(shaderVariantCollections);
            bootstrapper.SetBootstrapShaderWarmupShaders(shaderWarmupShaders);
            bootstrapper.SetBootstrapRuntimeShaderReferenceCatalog(runtimeShaderReferenceCatalog);
            bootstrapper.SetBootstrapShaderGraphicsStateCollectionPaths(shaderGraphicsStateCollectionPaths);
        }
    }
}
