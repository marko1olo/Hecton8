using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace Hecton8.Editor.Diagnostics
{
    /// <summary>
    /// SUPERSEDED AND DELIBERATELY INERT. This task used to write a hardcoded terrain height pair and
    /// report success; it now refuses and explains why, because every one of its four behaviours was
    /// wrong in a way that produced a convincing false positive.
    ///
    /// What it did, and why each part was a defect:
    ///
    /// 1. WRONG SCENE. It opened Assets/_Project/Scenes/020_RENDER_SANDBOX_V2.unity - a render sandbox,
    ///    not the shipping world scene 02_HECTON_WORLD - then logged
    ///    "[FixMapMagicHeightTask] Successfully updated MapMagicObject". So it could be run to
    ///    completion, print success, and leave the shipping world untouched at the vendor default of
    ///    250 m. That is the single most likely reason the terrain-height mismatch survived for many
    ///    runs while repeatedly looking fixed. HectonMacroGeologyBaseIntegrator.cs:132-143 documents
    ///    the same trap from the other side.
    ///
    /// 2. HARDCODED SPAN, NOW KNOWN WRONG. It wrote globals.height = 12000 and base Y = -10000. Those
    ///    numbers came from the graph node's lowWorldY/highWorldY, and the graph itself is the thing
    ///    that is wrong: WorldMacroGeologyFields can only produce about 5360 m of relief
    ///    (-4655.98 .. +704.02, clamped by HadalDepthMeters = 4600), while the authored depth zones the
    ///    runtime actually consumes reach -5500 m (Docs/Lore/Lore_Bible.md:229 and the DepthZoneProfile
    ///    assets created by HectonLoreSceneSetupEditor.cs:139-157; a second lock at
    ///    Docs/Lore/Canon_Locks.md:154-159 says -5600 m). Writing 12000 makes the generator fill 44.7%
    ///    of the normalised range, which puts the seafloor at +108.5 m - above the water plane at
    ///    +14.02 m. Applying this task's pair would therefore have surfaced a worse defect than the one
    ///    it was trying to fix.
    ///
    /// 3. SILENT DATA LOSS. OpenScene(..., OpenSceneMode.Single) with no dirty check discards unsaved
    ///    edits in every currently open scene, without asking.
    ///
    /// 4. EXIT 0 ON FAILURE. ExecuteHeadless called EditorApplication.Exit(0) unconditionally, including
    ///    down the branch that logged "Could not find MapMagicObject". Tools/BatchTasks/run_fix.bat
    ///    invokes exactly that entry point, so a batch run that found nothing and changed nothing still
    ///    reported success to whatever read its exit code. It also used FindAnyObjectByType, which skips
    ///    inactive objects - and the world scene's MapMagicObject may well be inactive.
    ///
    /// The correct owner of this write is HectonMacroGeologyBaseIntegrator, which never hardcodes a
    /// span: it reads lowWorldY/highWorldY back out of the graph each MapMagicObject actually generates
    /// from, names the scene it touched in every write line, and ships a REPORT ONLY path that changes
    /// nothing. Use that. This shim stays rather than being deleted only because run_fix.bat references
    /// it by reflection name; its job now is to fail loudly instead of succeeding falsely.
    /// </summary>
    public static class FixMapMagicHeightTask
    {
        private const string RefusalHeader =
            "[FixMapMagicHeightTask] REFUSED - this task is superseded and writes nothing.";

        private const string RefusalBody =
            "It used to open Assets/_Project/Scenes/020_RENDER_SANDBOX_V2.unity (a render sandbox, NOT " +
            "the shipping world scene), write a hardcoded globals.height=12000 / baseY=-10000 pair, and " +
            "log success either way. Three reasons that is wrong now: the scene was never the shipping " +
            "world; the 12000 m span exceeds what the geology generator can produce (about 5360 m) and " +
            "would render the seafloor above the water plane; and the headless entry point exited 0 even " +
            "when it found no MapMagicObject.\n" +
            "Use instead:\n" +
            "  Hecton8/World/MapMagic/Sync Terrain Height To Geology Span - REPORT ONLY   (writes nothing)\n" +
            "  Hecton8/World/MapMagic/Sync Terrain Height To Geology Span                 (applies)\n" +
            "  headless: Hecton8.Editor.HectonMacroGeologyBaseIntegrator.SyncTerrainHeightToGeologySpanHeadless\n" +
            "That tool reads the span out of the graph each MapMagicObject generates from and names the " +
            "scene it touched. Before applying it, the graph node's own lowWorldY/highWorldY must be " +
            "corrected - they currently say -10000/+2000, which no generator output and no authored " +
            "depth zone agrees with.";

        [MenuItem("Hecton8/Diagnostics/Fix MapMagic Height (SUPERSEDED - refuses)")]
        public static void RunFix()
        {
            Debug.LogError(RefusalHeader + "\n" + RefusalBody);
        }

        /// <summary>
        /// Kept for signature compatibility with anything that awaited the old task. Writes nothing.
        /// </summary>
        public static Task Execute()
        {
            Debug.LogError(RefusalHeader + "\n" + RefusalBody);
            return Task.CompletedTask;
        }

        /// <summary>
        /// Headless entry point referenced by Tools/BatchTasks/run_fix.bat.
        ///
        /// Exits NON-ZERO on purpose. The previous implementation exited 0 down every path, including
        /// the one that found nothing to edit, so a batch invocation reported success while changing
        /// nothing in the shipping scene. An honest failure with instructions is worth more than a
        /// success that was never earned.
        /// </summary>
        public static void ExecuteHeadless()
        {
            Debug.LogError(RefusalHeader + "\n" + RefusalBody);
            EditorApplication.Exit(2);
        }
    }
}
