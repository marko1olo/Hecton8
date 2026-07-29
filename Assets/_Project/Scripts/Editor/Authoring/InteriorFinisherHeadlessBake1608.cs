// ============================================================================
// HECTON-8 - InteriorFinisherHeadlessBake1608.cs
//
// FIRST_20_MINUTES moment served: "Safe anchor" / "First exit". The player starts
// inside a damaged habitat module and looks at its interior before anything else in
// the game. InteriorFinisherStudio1608 is the generator that dresses that interior,
// and until this file existed it had no batchmode entry at all: its only no-argument
// public member is InteriorFinisherStudio1608.Open() (InteriorFinisherStudio1608.cs
// :2608), which opens an EditorWindow, and the real pipeline entry
// InteriorFinisherPipeline1608.FinishInterior (InteriorFinisherStudio1608.cs:1106)
// takes a settings struct plus an out parameter, so -executeMethod cannot reach it.
// That is why zero interior assets exist on disk.
//
// BATCHMODE ROUTE (both entries are public static void with no parameters):
//   Unity.exe -batchmode -quit -projectPath C:\hades\Hecton8 \
//     -executeMethod Hecton8.Editor.Authoring.InteriorFinisherHeadlessBake1608.FinishModuleInteriorsNow \
//     -logFile <log>
//   Unity.exe -batchmode -quit -projectPath C:\hades\Hecton8 \
//     -executeMethod Hecton8.Editor.Authoring.InteriorFinisherHeadlessBake1608.FinishModuleInteriorsDiagnosticFallbackNow \
//     -logFile <log>
//
// -nographics IS FORBIDDEN FOR BOTH. The pipeline packs its instrument atlas through
// UnityEngine.Graphics.Blit into a temporary RenderTexture and reads it back with
// Texture2D.ReadPixels (InteriorFinisherStudio1608.cs TryFillAuthoredTextureBlock),
// then encodes PNGs. Without a GPU context Blit returns zeros, which matches the
// AGENTS.md Evidence Law clause "MapMagic & Batchmode Graphics Protocol: Running
// MapMagic/Compute Shader generation tests with -nographics in batchmode is strictly
// banned". A -nographics run would emit black atlases and still exit 0.
//
// WHY TWO ENTRIES INSTEAD OF ONE:
//   FinishModuleInteriorsNow is the production bake. It refuses fallback content, so
//   today it exits non-zero and names the missing input - that refusal is the answer,
//   not a failure to run.
//   FinishModuleInteriorsDiagnosticFallbackNow bakes what the current unfed pipeline
//   actually produces, into a separate folder, so the visual truth can be looked at
//   without contaminating the production folder. PROCEDURAL_ASSET_PIPELINE.md
//   Rejection List rejects "primitive spheres, boxes, cylinders, tubes, ribbons, or
//   blobs sold as final visuals" and "temporary art committed as final generated
//   content", so the diagnostic output is never written next to production assets.
//
// PROOF CLASS: this file is a batchmode command, not proof. Nothing here claims the
// interior looks correct. Unity import, Console, a render capture of the baked prefab,
// and Visual Reference Parity Gate comparison remain PENDING VERIFICATION.
// ============================================================================

#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Text;
using Hecton8.Building;
using Hecton8.Editor.Interiors;
using UnityEditor;
using UnityEngine;

namespace Hecton8.Editor.Authoring
{
    /// <summary>
    /// Batchmode driver for <see cref="InteriorFinisherPipeline1608"/>. Discovers the
    /// generated hard-surface module prefabs on disk, bakes one interior detail pack per
    /// module, and reports the runtime-consumer binding each pack still lacks.
    /// </summary>
    public static class InteriorFinisherHeadlessBake1608
    {
        /// <summary>
        /// Where ModuleArchitect1712 writes its generated module prefabs. Relative asset
        /// path only - AGENTS.md bans hardcoded absolute developer paths.
        /// </summary>
        private const string ModulePrefabFolder = "Assets/_Project/Art/Baked/Structures/Agent1712";

        private const string ProductionOutputFolder = "Assets/_Project/Art/Baked/Interiors";
        private const string DiagnosticOutputFolder = "Assets/_Project/Art/Baked/Interiors/_DiagnosticFallback";
        private const string OutputNamePrefix = "GEN_InteriorDetailPack_1608_";
        private const string DecorativeSocketMarker = "DecorativeSocket";
        private const string SocketMarkerPrefix = "Socket_";

        // COLD ALLOC: List<Transform>[256] - editor-only socket marker census scratch - owner: InteriorFinisherHeadlessBake1608
        private static readonly List<Transform> s_transformScratch = new List<Transform>(256);

        // ────────────────────────────────────────────────────────────────────
        //  ENTRY POINTS
        // ────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Production bake. One interior detail pack per generated module prefab, refusing
        /// the procedural fallback instrument kit and the bounding-box socket grid. Exits
        /// non-zero in batchmode when no module produced an authored interior.
        /// Batchmode entry point for -executeMethod.
        /// </summary>
        [MenuItem("Hecton8/Interiors/Finish Module Interiors Now (STRICT)", false, 1610)]
        public static void FinishModuleInteriorsNow()
        {
            Run(allowFallbackKit: false, outputFolder: ProductionOutputFolder);
        }

        /// <summary>
        /// Diagnostic bake into a separate folder. Accepts the procedural fallback kit and
        /// the bounding-box socket grid so the current unfed output can be inspected. The
        /// result is dev-only and must not be shipped or bound to a runtime consumer.
        /// Batchmode entry point for -executeMethod.
        /// </summary>
        [MenuItem("Hecton8/Interiors/Finish Module Interiors Now (DIAGNOSTIC FALLBACK)", false, 1611)]
        public static void FinishModuleInteriorsDiagnosticFallbackNow()
        {
            Run(allowFallbackKit: true, outputFolder: DiagnosticOutputFolder);
        }

        // ────────────────────────────────────────────────────────────────────
        //  DRIVER
        // ────────────────────────────────────────────────────────────────────

        private static void Run(bool allowFallbackKit, string outputFolder)
        {
            StringBuilder report = new StringBuilder(4096);
            report.Append("[InteriorFinisherHeadlessBake1608] mode=")
                  .Append(allowFallbackKit ? "DIAGNOSTIC_FALLBACK" : "STRICT")
                  .Append(" output=").Append(outputFolder)
                  .AppendLine();

            InteriorFinisherSettings1608 defaults = InteriorFinisherSettings1608.Default;
            report.Append("  instrument folder = ").Append(defaults.InstrumentPrefabFolder)
                  .Append(" exists=").Append(AssetDatabase.IsValidFolder(defaults.InstrumentPrefabFolder))
                  .Append(" authoredPrefabs=").Append(CountPrefabsInFolder(defaults.InstrumentPrefabFolder))
                  .AppendLine();

            if (!AssetDatabase.IsValidFolder(ModulePrefabFolder))
            {
                report.Append("  BLOCKER: module prefab folder '").Append(ModulePrefabFolder)
                      .Append("' does not exist. Bake the module set first: -executeMethod ")
                      .Append("Hecton8.Editor.Structures.ModuleArchitect1712.FabricateDefaultSetFromMenu")
                      .AppendLine();
                Emit(report, failed: true);
                return;
            }

            string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { ModulePrefabFolder });
            Array.Sort(guids, StringComparer.Ordinal);

            int attempted = 0;
            int succeeded = 0;
            int refused = 0;
            int failed = 0;

            // An empty instrument folder guarantees AppendFallbackRules will fire, because
            // InteriorInstrumentLibraryBuilder1608.Build adds exactly one rule per loadable
            // prefab it finds and falls back only when the rule list is still empty.
            int authoredInstrumentPrefabs = CountPrefabsInFolder(defaults.InstrumentPrefabFolder);

            for (int i = 0; i < guids.Length; i++)
            {
                string modulePath = AssetDatabase.GUIDToAssetPath(guids[i]);
                GameObject modulePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(modulePath);
                if (modulePrefab == null)
                    continue;

                int authoredSocketMarkers = CountAuthoredSocketMarkers(modulePrefab);
                attempted++;

                if (!allowFallbackKit && (authoredInstrumentPrefabs == 0 || authoredSocketMarkers == 0))
                {
                    // Skip the pipeline call outright rather than let it create an output folder
                    // and then refuse. Both conditions are sufficient on their own to force the
                    // fallback path, so the refusal is provable without touching the project.
                    refused++;
                    report.Append("  module=").Append(modulePrefab.name)
                          .Append(" authoredSocketMarkers=").Append(authoredSocketMarkers)
                          .Append(" result=REFUSED_BEFORE_BAKE")
                          .AppendLine();
                    report.Append("    refused: authoredInstrumentPrefabs=").Append(authoredInstrumentPrefabs)
                          .Append(" authoredSocketMarkers=").Append(authoredSocketMarkers)
                          .Append(" - the bake would have emitted the procedural box kit and/or an ")
                          .Append("axis-aligned bounding-box socket grid.")
                          .AppendLine();
                    continue;
                }

                InteriorFinisherSettings1608 settings = defaults;
                settings.ModulePrefab = modulePrefab;
                settings.OutputFolder = outputFolder;
                settings.OutputName = OutputNamePrefix + modulePrefab.name;
                settings.AllowFallbackKit = allowFallbackKit;

                bool ok = InteriorFinisherPipeline1608.FinishInterior(settings, out InteriorFinisherResult1608 result);

                report.Append("  module=").Append(modulePrefab.name)
                      .Append(" authoredSocketMarkers=").Append(authoredSocketMarkers)
                      .Append(" result=").Append(ok ? "BAKED" : "REJECTED")
                      .AppendLine();

                if (ok)
                {
                    succeeded++;
                    AppendBakedDetail(report, result);
                    AppendConsumerBindingStatus(report, modulePrefab, result.PrefabPath);
                }
                else if (result.UsedFallbackInstrumentKit || result.UsedFallbackSocketLayout)
                {
                    refused++;
                    report.Append("    refused: ").Append(result.FailureReason).AppendLine();
                }
                else
                {
                    failed++;
                    report.Append("    failed: ").Append(result.FailureReason).AppendLine();
                }
            }

            report.Append("  totals: modules=").Append(attempted)
                  .Append(" baked=").Append(succeeded)
                  .Append(" refusedOnFallbackInput=").Append(refused)
                  .Append(" hardFailures=").Append(failed)
                  .AppendLine();

            if (attempted == 0)
            {
                report.Append("  BLOCKER: no prefab under '").Append(ModulePrefabFolder)
                      .Append("' could be loaded as a module.").AppendLine();
            }
            else if (refused == attempted)
            {
                report.Append("  BLOCKER: every module was refused because the pipeline had no authored input. ")
                      .Append("Two content owners must act before a production interior can exist: ")
                      .Append("(1) author instrument prefabs under '").Append(defaults.InstrumentPrefabFolder)
                      .Append("'; (2) the module generator must emit child transforms named '")
                      .Append(DecorativeSocketMarker).Append("*' or '").Append(SocketMarkerPrefix)
                      .Append("*' on its interior faces - the generated modules currently carry only COL_*, ")
                      .Append("VIS_LOD*, and InteriorTrigger children.").AppendLine();
            }

            Emit(report, failed: succeeded == 0 || failed > 0);
        }

        private static void AppendBakedDetail(StringBuilder report, InteriorFinisherResult1608 result)
        {
            report.Append("    prefab=").Append(result.PrefabPath).AppendLine();
            report.Append("    mesh=").Append(result.MeshPath)
                  .Append(" cableMesh=").Append(string.IsNullOrEmpty(result.CableMeshPath) ? "<none>" : result.CableMeshPath)
                  .AppendLine();
            report.Append("    atlas=").Append(result.AtlasPath)
                  .Append(" normal=").Append(result.NormalPath)
                  .Append(" grime=").Append(result.GrimePath)
                  .AppendLine();
            report.Append("    sockets=").Append(result.SocketCount)
                  .Append(" microSockets=").Append(result.MicroSocketCount)
                  .Append(" placements=").Append(result.Counters.PlacementCount)
                  .Append(" movingParts=").Append(result.Counters.MovingPartCount)
                  .Append(" fusedVerts=").Append(result.Counters.FusedVertexCount)
                  .Append(" fusedIndices=").Append(result.Counters.FusedIndexCount)
                  .AppendLine();
            report.Append("    faultFlags=0x").Append(result.Counters.FaultFlags.ToString("X8"))
                  .Append(" fallbackKit=").Append(result.UsedFallbackInstrumentKit)
                  .Append(" aabbSocketGrid=").Append(result.UsedFallbackSocketLayout)
                  .AppendLine();

            if (result.UsedFallbackInstrumentKit || result.UsedFallbackSocketLayout)
            {
                report.Append("    DEV_ONLY: this pack is fallback content and is rejected as final visuals by ")
                      .Append("PROCEDURAL_ASSET_PIPELINE.md Rejection List. Do not bind it to a runtime consumer.")
                      .AppendLine();
            }
        }

        /// <summary>
        /// Reports the two ways a baked detail pack could reach the runtime, and whether
        /// either is wired. Neither is wired by the pipeline today: the pack is saved as a
        /// standalone prefab asset and nothing references it, so it can never be loaded or
        /// ticked. Naming the gap here is the point - this method does not repair it,
        /// because both repair targets are owned elsewhere.
        /// </summary>
        private static void AppendConsumerBindingStatus(StringBuilder report, GameObject modulePrefab, string detailPackPath)
        {
            GameObject detailPack = AssetDatabase.LoadAssetAtPath<GameObject>(detailPackPath);
            bool nested = detailPack != null && HasChildNamed(modulePrefab, detailPack.name);
            bool boundAsFinalPrefab = detailPack != null && IsBoundAsBuildableFinalPrefab(detailPack);

            report.Append("    runtimeConsumer: nestedUnderModulePrefab=").Append(nested)
                  .Append(" boundAsBuildableFinalPrefab=").Append(boundAsFinalPrefab)
                  .AppendLine();

            if (nested || boundAsFinalPrefab)
                return;

            report.Append("    BLOCKER: the baked pack has no runtime consumer. The module prefab itself is ")
                  .Append("reachable - BuildableData.finalPrefab is spawned by PlayerBuilder.cs:1804 and ")
                  .Append("ConstructionManager.cs:2833 - but the interior pack is a separate root prefab that ")
                  .Append("no BuildableData, scene, or spawner references. It must become a child of the module ")
                  .Append("prefab through PrefabUtility so it rides the existing finalPrefab route.")
                  .AppendLine();
        }

        // ────────────────────────────────────────────────────────────────────
        //  HELPERS
        // ────────────────────────────────────────────────────────────────────

        private static int CountAuthoredSocketMarkers(GameObject prefab)
        {
            if (prefab == null)
                return 0;

            s_transformScratch.Clear();
            prefab.GetComponentsInChildren(true, s_transformScratch);
            try
            {
                int count = 0;
                for (int i = 0; i < s_transformScratch.Count; i++)
                {
                    Transform current = s_transformScratch[i];
                    if (current == null || current == prefab.transform)
                        continue;

                    string markerName = current.name;
                    if (markerName.IndexOf(DecorativeSocketMarker, StringComparison.OrdinalIgnoreCase) >= 0 ||
                        markerName.IndexOf(SocketMarkerPrefix, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        count++;
                    }
                }

                return count;
            }
            finally
            {
                s_transformScratch.Clear();
            }
        }

        private static bool HasChildNamed(GameObject prefab, string childName)
        {
            if (prefab == null || string.IsNullOrEmpty(childName))
                return false;

            s_transformScratch.Clear();
            prefab.GetComponentsInChildren(true, s_transformScratch);
            try
            {
                for (int i = 0; i < s_transformScratch.Count; i++)
                {
                    Transform current = s_transformScratch[i];
                    if (current != null && string.Equals(current.name, childName, StringComparison.Ordinal))
                        return true;
                }

                return false;
            }
            finally
            {
                s_transformScratch.Clear();
            }
        }

        private static bool IsBoundAsBuildableFinalPrefab(GameObject detailPack)
        {
            string[] guids = AssetDatabase.FindAssets("t:BuildableData");
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                BuildableData data = AssetDatabase.LoadAssetAtPath<BuildableData>(path);
                if (data != null && data.finalPrefab == detailPack)
                    return true;
            }

            return false;
        }

        private static int CountPrefabsInFolder(string folder)
        {
            if (string.IsNullOrEmpty(folder) || !AssetDatabase.IsValidFolder(folder))
                return 0;

            return AssetDatabase.FindAssets("t:Prefab", new[] { folder }).Length;
        }

        private static void Emit(StringBuilder report, bool failed)
        {
            string text = report.ToString();
            if (failed)
                UnityEngine.Debug.LogError(text);
            else
                UnityEngine.Debug.Log(text);

            if (!Application.isBatchMode)
                return;

            EditorApplication.Exit(failed ? 1 : 0);
        }
    }
}
#endif
