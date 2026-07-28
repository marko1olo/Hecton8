using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hecton8.EditorTools.Diagnostics
{
    /// <summary>
    /// Timestamps every scene unload and every active-scene change so the player-root destruction seen in
    /// <c>Logs/omega_route*.log</c> can be correlated from the scene side.
    ///
    /// The runtime half of this correlation is the <c>[HectonPlayerMovement-DEBUG] OnDestroy called on
    /// player!</c> line in <c>Assets/_Project/Scripts/HectonPlayerMovement.cs</c>, which now carries the
    /// player's scene name, scene handle, <see cref="Time.frameCount"/> and <c>scene.isLoaded</c>. That line
    /// alone cannot say whether a scene unload was the destroyer, because a deferred
    /// <c>Object.Destroy</c> and a native unload produce the same (caller-free) managed stack. Matching the
    /// player's destruction frame against the frames printed here does say it: same frame as the
    /// 01_MAIN_MENU unload means the unload took the player, a different frame means someone called
    /// Destroy on the player root.
    ///
    /// Editor-only by folder placement (this file compiles into Hecton8.Editor.asmdef). It never enters,
    /// leaves or otherwise touches Play Mode - it only reads <see cref="Time.frameCount"/> and the scene
    /// arguments it is handed - so it is safe to leave installed alongside
    /// <see cref="H8_HeadlessPlayModeProbe"/> and alongside a normal Editor session.
    ///
    /// Subscriptions are hooked when Play Mode is entered and unhooked when it is exited, plus a defensive
    /// unhook before an assembly reload. The project runs Enter Play Mode Options with DisableDomainReload
    /// (ProjectSettings/EditorSettings.asset, m_EnterPlayModeOptions: 1), so these statics survive the
    /// transition and a naked static subscription would double up on the second Play session - hence the
    /// <see cref="_sceneEventsHooked"/> latch and the -= before every += .
    /// </summary>
    [InitializeOnLoad]
    public static class H8_PlayerLifetimeProbe
    {
        private const string Marker = "[H8_LIFETIME]";
        private const string InvalidSceneName = "<invalid>";

        private static bool _sceneEventsHooked;

        static H8_PlayerLifetimeProbe()
        {
            EditorApplication.playModeStateChanged -= HandlePlayModeStateChanged;
            EditorApplication.playModeStateChanged += HandlePlayModeStateChanged;

            AssemblyReloadEvents.beforeAssemblyReload -= UnhookSceneEvents;
            AssemblyReloadEvents.beforeAssemblyReload += UnhookSceneEvents;

            // A domain reload can land while Play Mode is already running; recover the hook without
            // waiting for a state change that has already happened.
            if (EditorApplication.isPlaying)
                HookSceneEvents();
        }

        private static void HandlePlayModeStateChanged(PlayModeStateChange change)
        {
            switch (change)
            {
                case PlayModeStateChange.EnteredPlayMode:
                    HookSceneEvents();
                    break;

                case PlayModeStateChange.ExitingPlayMode:
                    UnhookSceneEvents();
                    break;
            }
        }

        private static void HookSceneEvents()
        {
            if (_sceneEventsHooked)
                return;

            SceneManager.sceneUnloaded -= HandleSceneUnloaded;
            SceneManager.sceneUnloaded += HandleSceneUnloaded;
            SceneManager.activeSceneChanged -= HandleActiveSceneChanged;
            SceneManager.activeSceneChanged += HandleActiveSceneChanged;
            _sceneEventsHooked = true;

            Debug.Log($"{Marker} ARMED frame={Time.frameCount}");
        }

        private static void UnhookSceneEvents()
        {
            if (!_sceneEventsHooked)
                return;

            SceneManager.sceneUnloaded -= HandleSceneUnloaded;
            SceneManager.activeSceneChanged -= HandleActiveSceneChanged;
            _sceneEventsHooked = false;
        }

        private static void HandleSceneUnloaded(Scene scene)
        {
            Debug.Log(
                $"{Marker} SCENEUNLOADED name={DescribeName(scene)} handle={DescribeHandle(scene)} " +
                $"frame={Time.frameCount}");
        }

        private static void HandleActiveSceneChanged(Scene from, Scene to)
        {
            Debug.Log(
                $"{Marker} ACTIVESCENECHANGED from={DescribeName(from)} fromHandle={DescribeHandle(from)} " +
                $"to={DescribeName(to)} toHandle={DescribeHandle(to)} frame={Time.frameCount}");
        }

        /// <summary>
        /// <c>activeSceneChanged</c> hands out a default <see cref="Scene"/> for the "from" side on the very
        /// first activation, and an unloading scene can already be invalid here. Reading names and handles
        /// off those is not useful, so they are reported as such rather than as an empty string.
        /// </summary>
        private static string DescribeName(Scene scene)
        {
            return scene.IsValid() ? scene.name : InvalidSceneName;
        }

        private static string DescribeHandle(Scene scene)
        {
            return scene.IsValid()
                ? scene.handle.GetRawData().ToString()
                : InvalidSceneName;
        }
    }
}
