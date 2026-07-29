#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Hecton8.EditorTools.Generators.Flora
{
    /// <summary>
    /// Headless entry point for the Flora Topology 1711 static seed pack.
    /// </summary>
    /// <remarks>
    /// Before this gate existed the flora domain had no way to run outside the Unity GUI. Both
    /// studios expose their work only through <c>[MenuItem]</c> methods that return <c>void</c> and
    /// report failure with <c>Debug.LogError</c>, so a `-batchmode -quit -executeMethod` run against
    /// either of them exits <c>0</c> whether it baked three prefabs or nothing at all. Root
    /// AGENTS.md `Never Trust Automated Assertions Alone` rejects exactly that shape: a runner must
    /// fail explicitly, not return success because the process happened to shut down cleanly.
    ///
    /// This class is the only publicly reachable surface of the flora generators. It lives in the
    /// generator folder rather than under `Scripts/Editor/Authoring` because the studios are
    /// <c>internal</c> to the `Hecton8.Project.Editor` assembly
    /// (`Assets/_Project/Editor/Hecton8.Project.Editor.asmdef`), while `Scripts/Editor/Authoring`
    /// compiles into `Hecton8.Editor`. Reaching them from there would require promoting
    /// <c>FloraTopologyStudio1711</c> and its parameter types to <c>public</c>, which is a public API
    /// widening that AGENTS.md gates behind a dependency list, explicit approval and compile proof.
    ///
    /// Batchmode invocation:
    /// <code>
    /// Unity.exe -batchmode -quit -projectPath C:\hades\Hecton8 ^
    ///   -logFile C:\hades\Hecton8\Logs\FloraSeedPack.log ^
    ///   -executeMethod Hecton8.EditorTools.Generators.Flora.FloraSeedPackHeadlessGate.GenerateStaticSeedPackFromCommandLine
    /// </code>
    ///
    /// Do NOT add <c>-nographics</c>. The bake calls <c>Mesh.UploadMeshData</c> and saves prefabs
    /// through <c>PrefabUtility</c>, and root AGENTS.md `MapMagic &amp; Batchmode Graphics Protocol`
    /// bans headless generation runs without a GPU context for this asset family. A `-nographics`
    /// run is not proof of anything here.
    /// </remarks>
    public static class FloraSeedPackHeadlessGate
    {
        private const int SuccessExitCode = 0;
        private const int FailureExitCode = 3;

        /// <summary>
        /// Batchmode entry point. Bakes the 1711 static seed pack and, when running headless, sets a
        /// non-zero process exit code on any contract failure so a caller can distinguish a real
        /// bake from a silent no-op.
        /// </summary>
        public static void GenerateStaticSeedPackFromCommandLine()
        {
            bool generated;
            try
            {
                generated = FloraTopologyStudio1711.TryGenerateStaticSeedPack();
            }
            catch (System.Exception exception)
            {
                // An exception from the mesh writer is a hard failure, not a warning. Log it with the
                // stack and fail the process; swallowing it would restore the exit-code-0 lie this
                // gate exists to remove.
                Debug.LogError("[FloraSeedPackHeadlessGate] Seed pack bake threw. " + exception);
                Exit(FailureExitCode);
                return;
            }

            if (!generated)
            {
                Debug.LogError("[FloraSeedPackHeadlessGate] FLORA_SEED_PACK_FAILED."
                    + " No Topology1711 mesh or prefab output can be trusted from this run."
                    + " Read the preceding [FloraTopology1711] and [FloraTopology1604] errors for the"
                    + " first gate that rejected.");
                Exit(FailureExitCode);
                return;
            }

            Debug.Log("[FloraSeedPackHeadlessGate] FLORA_SEED_PACK_OK."
                + " Mesh and prefab assets exist under Assets/_Project/Art/Generated/Flora/Topology1711"
                + " and Assets/_Project/Prefabs/Nature/Flora/Topology1711."
                + " STATUS=PENDING UNITY IMPORT/RENDER/PROFILER VERIFICATION.");
            Exit(SuccessExitCode);
        }

        /// <summary>
        /// Same bake, reachable from the Unity menu, with the process-exit behaviour suppressed so a
        /// human click cannot terminate an interactive editor session.
        /// </summary>
        [MenuItem("Hecton8/Authoring/Flora Topology 1711/Run Headless Gate (In-Editor)", priority = 194)]
        public static void RunHeadlessGateInEditor()
        {
            if (FloraTopologyStudio1711.TryGenerateStaticSeedPack())
                Debug.Log("[FloraSeedPackHeadlessGate] FLORA_SEED_PACK_OK (interactive run, no process exit).");
            else
                Debug.LogError("[FloraSeedPackHeadlessGate] FLORA_SEED_PACK_FAILED (interactive run, no process exit).");
        }

        private static void Exit(int exitCode)
        {
            // Only a genuinely headless process may be terminated. In an interactive editor this
            // would close the application under the user, and the menu route above relies on that.
            if (!Application.isBatchMode)
                return;

            EditorApplication.Exit(exitCode);
        }
    }
}
#endif
