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
        private bool _delegatedBoot;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureRuntimeBootstrapOwner()
        {
            if (!Application.isPlaying)
                return;

            Scene activeScene = SceneManager.GetActiveScene();
            if (!activeScene.IsValid() || !activeScene.name.Contains("00_BOOTSTRAP"))
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

        private void OnDestroy()
        {
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
            if (!currentScene.IsValid() || !currentScene.name.Contains("00_BOOTSTRAP"))
                return;

            _delegatedBoot = true;
            GameBootstrapper.EnsureRuntimeInstance(gameObject)?.BeginBootstrap();
        }
    }
}
