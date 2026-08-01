using Hecton8.Visor;
using Hecton8.Core;
using Hecton8.World;
using UnityEngine;

namespace Hecton8.Audio
{
    /// <summary>
    /// Cold-path installer for player-owned atmospheric audio and scene-level acoustic zone owners.
    /// </summary>
    public static class AtmosphericAudioRuntimeInstaller
    {
        private const string RuntimeRootName = "__HECTON_AUDIO_RUNTIME";

        /// <summary>
        /// Ensures the global acoustic-zone owner exists in the active gameplay scene.
        /// <para>
        /// <see cref="AcousticZoneController"/> is the sole owner of
        /// <see cref="GlobalRegistry.AcousticZone"/> / <see cref="GlobalRegistry.AcousticZoneReadModel"/> /
        /// <see cref="GlobalRegistry.AcousticZoneMadnessCueSink"/> /
        /// <see cref="GlobalRegistry.ToolAcousticCues"/> and it had no construction site of any kind.
        /// No <c>AddComponent</c>, <c>new</c>, or scene/prefab GUID hit for script GUID
        /// 46c4f463f7190a04b9285cb2b4cc7f63 exists under Assets/ (text + nibble-swapped binary sweep of
        /// 02_HECTON_WORLD / 00_BOOTSTRAP / 01_MAIN_MENU / 010_TEST). The slot is therefore permanently
        /// null in a shipped build.
        /// </para>
        /// <para>
        /// Live consumers already read the null:
        /// HectonSurfaceWeatherDirector.cs:836 (<c>GlobalRegistry.AcousticZone</c>),
        /// DeepPsychosisController.cs:340 (<c>AcousticZoneMadnessCueSink</c>),
        /// HectonMusicDirector.cs:1573 (<c>AcousticZoneReadModel</c>),
        /// MantaScooter.cs:2608 (<c>ToolAcousticCues</c>). Surface/interior/underwater snapshot
        /// transitions, madness cues and tool acoustic feedback therefore never arm.
        /// </para>
        /// <para>
        /// Registration happens in <c>AcousticZoneController</c> OnEnable via
        /// <c>TryRegisterService</c> → <c>GlobalRegistry.RegisterAcousticZoneRuntime</c>, which is why
        /// the root must be live before AddComponent - see <see cref="ResolveOrCreateRuntimeRoot"/>.
        /// </para>
        /// <para>
        /// The serialized <c>masterMixer</c> field stays unassigned on a runtime-created owner. That is
        /// the same profile-null-safe shape as <see cref="Hecton8.Environment.EnvironmentRuntimeInstaller"/>
        /// / GlobalWeatherDirector: <c>EnsureSnapshotBindings</c> early-outs when masterMixer is null
        /// (AcousticZoneController.cs:3410), and every mixer SetFloat/TransitionToSnapshots site also
        /// null-guards. The only AssetDatabase-backed mixer assign,
        /// <c>TryAssignEditorAuthoringDefaults</c>, sits inside <c>#if UNITY_EDITOR</c> and is reached
        /// from Awake/OnValidate - authoring convenience, not a player-build dependency. Snapshot
        /// transitions remain degraded until a scene-authored mixer or a future player load path is
        /// wired; the registry slot and non-mixer cue paths become live immediately.
        /// </para>
        /// </summary>
        public static void EnsureRuntimeSystems()
        {
            // Prefer an already-registered or still-alive Instance so bootstrap re-entry is a no-op.
            if (GlobalRegistry.AcousticZone != null)
                return;

            if (AcousticZoneController.Instance != null)
                return;

            GameObject runtimeRoot = ResolveOrCreateRuntimeRoot();

            // Guarded so the installer is idempotent across the multiple passes the bootstrap makes.
            // AddComponent onto a live GameObject runs Awake and OnEnable at the call site and ignores
            // DefaultExecutionOrder - same as EnvironmentRuntimeInstaller / WorldRuntimeInstaller notes.
            if (!runtimeRoot.TryGetComponent<AcousticZoneController>(out _))
                runtimeRoot.AddComponent<AcousticZoneController>();
        }

        /// <summary>
        /// Ensures the active player owns the atmospheric polish systems.
        /// </summary>
        /// <param name="playerObject">Resolved player object published by bootstrap.</param>
        public static void EnsurePlayerSystems(GameObject playerObject)
        {
            if (playerObject == null)
                return;

            EnsureProceduralAudioRenderer(playerObject);

            if (!playerObject.TryGetComponent(out DeepPsychosisController _))
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Hecton8.Core.H8Debug.LogWarning("[AtmosphericAudioRuntimeInstaller] Missing authored DeepPsychosisController on player. Runtime component creation is disabled.", playerObject);
#endif
            }

            if (!playerObject.TryGetComponent(out PlayerStressVFX _))
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Hecton8.Core.H8Debug.LogWarning("[AtmosphericAudioRuntimeInstaller] Missing authored PlayerStressVFX on player. Runtime component creation is disabled.", playerObject);
#endif
            }

            // Projected caustics are shader-only on MX350; no player-owned compute projector is installed.
        }

        /// <summary>
        /// Resolves the audio runtime root across loaded scenes, creating it when no scene owns one,
        /// and guarantees it is live so the install above actually runs Awake/OnEnable.
        /// </summary>
        /// <returns>Active runtime root that hosts the audio owners.</returns>
        private static GameObject ResolveOrCreateRuntimeRoot()
        {
            GameObject runtimeRoot = null;
            WorldRuntimeReferenceUtility.TryResolveScenePath(ref runtimeRoot, RuntimeRootName);
            if (runtimeRoot == null)
                runtimeRoot = new GameObject(RuntimeRootName); // COLD ALLOC: GameObject[1] - one runtime root for audio owners per gameplay scene - owner: AtmosphericAudioRuntimeInstaller

            // A resolved root can come back hidden or deactivated from an earlier scene state.
            // AddComponent on an inactive GameObject never runs Awake/OnEnable, so
            // RegisterAcousticZoneRuntime would never fire and consumers would still cache null.
            // Same handling as EnvironmentRuntimeInstaller / EcosystemRuntimeInstaller.
            runtimeRoot.hideFlags = HideFlags.None;
            if (!runtimeRoot.activeSelf)
                runtimeRoot.SetActive(true);

            return runtimeRoot;
        }

        private static void EnsureProceduralAudioRenderer(GameObject playerObject)
        {
            IPlayerRuntimeContext playerContext = Hecton8.Core.GlobalRegistry.Player;
            Camera playerCamera = playerContext != null ? playerContext.PlayerCamera : null;

            AudioListener listener = null;
            if (playerCamera != null)
                playerCamera.TryGetComponent(out listener);

            if (listener == null)
                playerObject.TryGetComponent(out listener);

            if (listener == null)
            {
                // No authored AudioListener on camera/player - nothing to host the critical
                // audio owners on. Prefer an existing listener anywhere before giving up.
                listener = Object.FindFirstObjectByType<AudioListener>(FindObjectsInactive.Include);
                if (listener == null)
                    return;
            }

            // PlayerCriticalProceduralAudioRenderer is the sole thruster/critical procedural
            // audio owner and had zero scene/prefab GUID hits (d837e0b45d8800643bbc1f384302325a).
            // Installer previously only warned and returned, so BindToPlayer never ran and
            // thruster audio stayed on the legacy PlayerThrusterAudio path (or silent).
            // Construct on the live listener.
            if (!listener.TryGetComponent(out PlayerCriticalProceduralAudioRenderer renderer))
                renderer = listener.gameObject.AddComponent<PlayerCriticalProceduralAudioRenderer>();

            // VocalWarningSystem is the sole vocal-warning owner and had zero scene/prefab
            // GUID hits (36c8bbdca4a5c1b4396cb80c386fba8f). Installer previously only warned.
            // 41 external refs cache null permanently without this AddComponent.
            if (!listener.TryGetComponent(out VocalWarningSystem _))
                listener.gameObject.AddComponent<VocalWarningSystem>();


            renderer.BindToPlayer(playerObject);

            PlayerThrusterAudio legacyThrusterAudio = playerContext != null ? playerContext.ThrusterAudio : null;
            if (legacyThrusterAudio != null)
            {
                if (legacyThrusterAudio.TryGetComponent(out AudioSource legacySource) && legacySource.isPlaying)
                    legacySource.Stop();

                legacyThrusterAudio.enabled = false;
            }
        }

    }
}
