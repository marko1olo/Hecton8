using Hecton8.World;
using UnityEngine;

namespace Hecton8.Environment
{
    /// <summary>
    /// Cold-path installer for scene-level environment owners that a live consumer already reads out of
    /// <see cref="Hecton8.Core.GlobalRegistry"/> and receives as null.
    /// </summary>
    public static class EnvironmentRuntimeInstaller
    {
        private const string RuntimeRootName = "__HECTON_ENVIRONMENT_RUNTIME";

        /// <summary>
        /// Ensures the global weather owner exists in the active gameplay scene.
        /// <para>
        /// <see cref="GlobalWeatherDirector"/> is the only <c>IWeatherService</c> implementation in the
        /// project and it had no construction site of any kind. No <c>AddComponent</c>, <c>new</c>,
        /// <c>GetOrAddComponent</c> or <c>CreateInstance</c> for the type exists outside
        /// Assets/_Project/Tests/Editor/GlobalWeatherDirectorEditTests.cs:34, the type declares no static
        /// factory, and a GUID sweep of every .unity, .prefab and .asset file under Assets/ (4093 files,
        /// nibble-swapped byte scan for the binary scenes) found its script GUID
        /// e6c6cdab45ef47aea41e9ad5a4ada5a8 in none of them. The slot is therefore permanently null in a
        /// shipped build.
        /// </para>
        /// <para>
        /// The consumer is live and already reads the null. HectonCelestialEngine caches the slot at
        /// HectonCelestialEngine.cs:2096 (<c>_cachedWeatherService = GlobalRegistry.Weather;</c>) and its
        /// script GUID 86667f9831733ab48aaa2bb3a38047ee is authored into 02_HECTON_WORLD.unity, so the
        /// consumer runs every session while the reference stays null.
        /// </para>
        /// <para>
        /// Registration happens in <c>GlobalWeatherDirector.OnEnable</c> at GlobalWeatherDirector.cs:350,
        /// which is why the root must be live before the AddComponent below - see
        /// <see cref="ResolveOrCreateRuntimeRoot"/>.
        /// </para>
        /// <para>
        /// The six <c>WeatherProfile</c> ScriptableObject fields (GlobalWeatherDirector.cs:122-132) stay
        /// unassigned on a runtime-created owner, and that is safe rather than a silent degradation: every
        /// read site carries an explicit non-null fallback - the <c>if (profile == null)</c> guards at
        /// :1249, :1378 and :1391 plus the <c>profile != null ? ... : literal</c> ternaries around them -
        /// which fall back to the inline <c>PhaseProfile</c> structs at :134, :149 and :164, whose C# field
        /// initializers do run for a component added at runtime. The only AssetDatabase-backed profile
        /// assignment, <c>AssignDefaultProfilesIfMissing</c> at :1465, sits inside <c>#if UNITY_EDITOR</c>
        /// and is reached only from <c>OnValidate</c> (:1462), so it is an authoring convenience and not a
        /// player-build dependency.
        /// </para>
        /// </summary>
        public static void EnsureRuntimeSystems()
        {
            GameObject runtimeRoot = ResolveOrCreateRuntimeRoot();

            // Guarded so the installer is idempotent across the multiple passes the bootstrap makes.
            // GlobalWeatherDirector carries [DefaultExecutionOrder(-4550)] (GlobalWeatherDirector.cs:15),
            // but that attribute is inert for this install: AddComponent onto a live GameObject runs Awake
            // and OnEnable at the call site and ignores execution order entirely, as
            // WorldRuntimeInstaller.cs:117-119 documents. Only one owner is installed on this root, so
            // there is no intra-root ordering left to respect and the deferred-activation idiom that
            // WorldRuntimeInstaller needs for its sibling-coupled voxel pair is not required here -
            // GlobalWeatherDirector contains no GetComponent or TryGetComponent call at all and resolves
            // every dependency through GlobalRegistry (GlobalWeatherDirector.cs ResolveDependencies).
            if (!runtimeRoot.TryGetComponent<GlobalWeatherDirector>(out _))
                runtimeRoot.AddComponent<GlobalWeatherDirector>();
        }

        /// <summary>
        /// Resolves the environment runtime root across loaded scenes, creating it when no scene owns one,
        /// and guarantees it is live so the install above actually runs Awake/OnEnable.
        /// </summary>
        /// <returns>Active runtime root that hosts the environment owners.</returns>
        private static GameObject ResolveOrCreateRuntimeRoot()
        {
            GameObject runtimeRoot = null;
            WorldRuntimeReferenceUtility.TryResolveScenePath(ref runtimeRoot, RuntimeRootName);
            if (runtimeRoot == null)
                runtimeRoot = new GameObject(RuntimeRootName); // COLD ALLOC: GameObject[1] - one runtime root for environment owners per gameplay scene - owner: EnvironmentRuntimeInstaller

            // A resolved root can come back hidden or deactivated from an earlier scene state.
            // AddComponent on an inactive GameObject never runs Awake/OnEnable, so
            // GlobalWeatherDirector.cs:350 would never call RegisterWeatherService and the owner would
            // exist while HectonCelestialEngine.cs:2096 still cached null - the fix would look applied and
            // change nothing. WorldRuntimeReferenceUtility.TryResolveScenePath
            // (WorldRuntimeReferenceUtility.cs:179) resolves scene roots, so activeSelf is the whole
            // hierarchy state here. Same handling as EcosystemRuntimeInstaller.cs:63-71.
            runtimeRoot.hideFlags = HideFlags.None;
            if (!runtimeRoot.activeSelf)
                runtimeRoot.SetActive(true);

            return runtimeRoot;
        }
    }
}
