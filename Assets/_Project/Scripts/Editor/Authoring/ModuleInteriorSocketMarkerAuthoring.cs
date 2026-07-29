// ============================================================================
// HECTON-8 — ModuleInteriorSocketMarkerAuthoring.cs
//
// FIRST_20_MINUTES moment served: "Safe anchor". The player starts inside a
// damaged habitat module and looks at its interior before anything else, and the
// interior bake refuses to run for all six generated modules. This file removes
// one of the three reasons, and measures the other two instead of hiding them.
//
// WHY THIS FILE EXISTS SEPARATELY FROM THE GENERATOR
//   The marker emitter itself is NOT here. It is
//   ModuleArchitect1712.EmitInteriorSocketMarkers, called from the fabrication path
//   so the anchors regenerate with the module and cannot drift from the collider
//   proxies they are derived from. One owner.
//   This file is the second INVOCATION ROUTE for that same method, and it exists
//   because a re-fabricate is not a safe way to deliver the anchors today:
//   ModuleArchitect1712.cs:446 builds a brand-new GameObject every run and :468
//   SaveAsPrefabAssets it over the existing path, so the root fileID of a
//   re-fabricated prefab is not the old one - and five of these six prefabs are
//   bound by a recipe finalPrefab that carries an explicit root fileID:
//     Build_Airlock_Hatch      -> H8_A1712_Airlock_01        3720669919621297738
//     Build_Junction_X         -> H8_A1712_Junction_01       3188614581783020795
//     Build_Junction_T         -> H8_A1712_ServiceCap_01      842066932562619614
//     Build_MultiPurpose_Room  -> H8_A1712_ReactorRoom_01    1475445993294761913
//     Build_Moonpool_Bay       -> H8_A1712_VerticalShaft_01  6378744787296289934
//   A re-fabricate would risk breaking all five at once, and it re-appends the six
//   generated rows to ModuleCatalog_Starter.asset, which
//   ConstructionCatalogRepairAuthoring.cs:90-96 documents as requiring the Adopt
//   pass to be re-run afterwards. So: generator owns the CODE, this pass owns the
//   safe DELIVERY onto prefabs that already exist.
//
// THREE BLOCKERS, ONE OF THEM THIS FILE'S
//   The interior bake's refusal is FailClosedOnFallbackKit
//   (InteriorFinisherStudio1608.cs:1612-1632) and it is TWO conditions ANDed:
//       if (!library.UsedFallbackKit && authoredSockets) return;
//   so clearing the socket half alone leaves the bake refusing on the kit half.
//     1. ZERO marker transforms - this file's. Fixed by Apply below.
//     2. ZERO authored instrument prefabs anywhere in the project. Measured
//        2026-07-29: Assets/_Project/Prefabs/Instruments ABSENT and
//        Assets/_Project/Prefabs/Equipment ABSENT, so EquipmentPropBaker1715 has
//        never been run and there is nothing to point the override at. One loadable
//        prefab clears it. Not this file's to author.
//     3. NO INTERIOR VISUAL SURFACE on any module. ModuleArchitect1712 calls
//        AddManufacturedFaceForSocket exactly six times, once per OUTWARD face
//        (:980-985), with no inner shell, no lining and no winding flip anywhere in
//        the generator; and the interior pipeline's own output is instrument bases,
//        cable bundles and moving handles (InteriorFinisherStudio1608.cs:1957-2014)
//        - never walls. So every interior wall is a culled backface. This is a
//        DESIGN decision about what a HECTON-8 module interior is, not a bug, and it
//        is not bolted on here.
//
// WHY ADDING MARKERS WITHOUT MEASURING #3 WOULD MAKE THINGS WORSE - the reason the
// probe below ships in the SAME change as the markers, not after it.
//   Emitting markers flips CollectSockets' return to true, which satisfies
//   `authoredSockets` and disarms the socket half of the fail-closed gate. Today
//   that gate is the only thing in the project reporting that these modules have no
//   interior. The moment one instrument prefab appears, a STRICT bake would then
//   place instruments onto invisible walls and report Success with
//   faultFlags=0x00000000. That is the silent-degeneracy class AGENTS.md names as
//   dominant here. So the gate being disarmed is REPLACED, in this change, by a
//   direct measurement of the real defect: count triangles whose geometric normal
//   points toward the module centre. Zero for all six today. A proxy for the defect
//   is retired and an instrument for it is installed in the same commit.
//
// GATE SPLIT, AND WHY IT IS NOT AN EXCUSE
//   VerifyModuleInteriorSocketMarkers owns ONLY the marker invariant, so it can
//   genuinely reach PASS after Apply - a gate that can never pass gets switched off.
//   VerifyModuleInteriorBakeReadiness ANDs all three blockers and is EXPECTED to
//   stay red until two other owners act. Neither method can be read as claiming
//   more than it measures, and the readiness gate is the one to wire to "can the
//   interior bake actually run".
//
// PREFAB MUTATION ROUTE — LoadPrefabContents / SaveAsPrefabAsset /
// UnloadPrefabContents, per AGENTS.md `Evidence Law` and hecton8-unity-assets.md.
// Raw YAML editing of a .prefab is banned project-wide. The root local file
// identifier is read before and after every write and a move is reported as a hard
// error naming the assets to check, never as success.
//
// PROOF CLASS: static asset-graph authoring in the Editor. Nothing here claims an
// interior looks correct, or that a bake succeeded. Unity import, Console, a render
// capture and Visual Reference Parity Gate comparison all remain PENDING
// VERIFICATION.
// ============================================================================

#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Text;
using Hecton8.Building;
using Hecton8.Editor.Interiors;
using Hecton8.Editor.Structures;
using UnityEditor;
using UnityEngine;

namespace Hecton8.Editor.Authoring
{
    /// <summary>
    /// Delivers <see cref="ModuleArchitect1712.EmitInteriorSocketMarkers"/> onto the
    /// already-fabricated generated module prefabs, verifies the marker invariant, and
    /// measures the two interior-bake blockers this file does not own.
    /// Idempotent: a second Apply rebuilds the identical hierarchy and reports no change.
    /// </summary>
    public static class ModuleInteriorSocketMarkerAuthoring
    {
        private const string LogPrefix = "[ModuleInteriorSocketMarkers]";
        private const string ReadinessPrefix = "[ModuleInteriorBakeReadiness]";

        /// <summary>
        /// Where ModuleArchitect1712 writes its generated module prefabs
        /// (ModuleArchitect1712Settings.Default.OutputFolder). Relative asset path only -
        /// AGENTS.md bans hardcoded absolute developer paths.
        /// </summary>
        private const string ModulePrefabFolder = "Assets/_Project/Art/Baked/Structures/Agent1712";

        private const string RecipeFolder = "Assets/_Project/Data/Construction";

        /// <summary>
        /// Same switch InteriorFinisherHeadlessBake1608.cs:124 reads, so the readiness gate and
        /// the bake it gates cannot disagree about which instrument folder is in play.
        /// </summary>
        private const string InstrumentFolderArgument = "-h8InteriorInstrumentFolder";

        /// <summary>
        /// Mirrors <c>ColliderOptimizerEngine1716.GeneratedCompoundRootName</c> and
        /// <c>GeneratedConvexRootName</c>. Mirrored rather than referenced to avoid taking an assembly
        /// dependency on the collider engine for two string literals; if either constant moves, the
        /// proxy-piece count drifts by one and the marker gate reports the mismatch rather than hiding
        /// it, which is the failure mode worth having.
        /// </summary>
        private const string ColliderOptimizerCompoundRootName = "COL_CompoundProxy_1716";
        private const string ColliderOptimizerConvexRootName = "COL_ConvexProxy_1716";

        /// <summary>
        /// Minimum <c>dot(faceNormal, directionToCentre)</c> for a triangle to count as interior
        /// facing. 0.5 rather than 0: the outward faces carry recessed panel fields
        /// (ModuleHardSurfaceDetail1712.AddInwardRecessRing), whose recess SIDE walls are roughly
        /// perpendicular to the surface normal and therefore land near dot 0. Counting those would
        /// report an interior that does not exist. A real interior lining faces the room squarely
        /// and lands near dot 1.
        /// </summary>
        private const float InteriorFacingDotThreshold = 0.5f;

        /// <summary>Tokens that would silently reclassify a marker as a MicroStamp.</summary>
        /// <remarks>
        /// InteriorSocketParser1608.ClassifyKind tests Rivet/Seam/Micro FIRST and returns
        /// MicroStamp, and CollectSockets routes MicroStamp markers into a separate list whose
        /// count does NOT satisfy the bake. A marker carrying one of these is invisible to the
        /// socket requirement even though it exists, so the verify gate fails on it explicitly
        /// rather than reporting a healthy count that does not work.
        /// </remarks>
        private static readonly string[] s_MicroStampTokens = { "Rivet", "Seam", "Micro" };

        // COLD ALLOC: List<BoxCollider>[32] - editor-only proxy piece scan scratch - owner: ModuleInteriorSocketMarkerAuthoring
        private static readonly List<BoxCollider> s_ColliderScratch = new List<BoxCollider>(32);
        // COLD ALLOC: List<MeshFilter>[16] - editor-only mesh scan scratch - owner: ModuleInteriorSocketMarkerAuthoring
        private static readonly List<MeshFilter> s_MeshFilterScratch = new List<MeshFilter>(16);

        /// <summary>One module prefab's measured marker state.</summary>
        private readonly struct MarkerAudit
        {
            public MarkerAudit(
                int proxyPieceCount,
                int markerCount,
                int misnamedCount,
                int microTokenCount,
                int outwardFacingCount,
                int scaledCount,
                bool containerPresent)
            {
                ProxyPieceCount = proxyPieceCount;
                MarkerCount = markerCount;
                MisnamedCount = misnamedCount;
                MicroTokenCount = microTokenCount;
                OutwardFacingCount = outwardFacingCount;
                ScaledCount = scaledCount;
                ContainerPresent = containerPresent;
            }

            public int ProxyPieceCount { get; }

            public int MarkerCount { get; }

            public int MisnamedCount { get; }

            public int MicroTokenCount { get; }

            /// <summary>Markers whose +Z does NOT point toward the module centre.</summary>
            public int OutwardFacingCount { get; }

            /// <summary>Markers whose localScale is not one, which silently rescales the socket radius.</summary>
            public int ScaledCount { get; }

            public bool ContainerPresent { get; }

            public bool Passes =>
                ProxyPieceCount > 0 &&
                MarkerCount == ProxyPieceCount &&
                MisnamedCount == 0 &&
                MicroTokenCount == 0 &&
                OutwardFacingCount == 0 &&
                ScaledCount == 0;
        }

        // ══════════════════════════════════════════════════════════
        //  ENTRY POINT 1 — VERIFY THE MARKER INVARIANT (read-only)
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Read-only. Owns ONLY the marker invariant, so it genuinely reaches PASS after Apply.
        /// It does NOT claim the interior bake can run - use
        /// <see cref="VerifyModuleInteriorBakeReadiness"/> for that. Writes nothing: no emit, no
        /// SetDirty, no save, per the automated-runner clause in AGENTS.md `Evidence Law`.
        /// Batchmode:
        /// -executeMethod Hecton8.Editor.Authoring.ModuleInteriorSocketMarkerAuthoring.VerifyModuleInteriorSocketMarkers
        /// </summary>
        [MenuItem("Hecton8/Validation/Verify Module Interior Socket Markers", priority = 247)]
        public static void VerifyModuleInteriorSocketMarkers()
        {
            StringBuilder report = new StringBuilder(8192);
            report.AppendLine($"{LogPrefix} MARKER INVARIANT REPORT");
            report.AppendLine(
                "  INVARIANT: one anchor per COL_*Proxy piece, named with the DecorativeSocket prefix, " +
                "carrying no Rivet/Seam/Micro token, +Z pointing toward the module centre, localScale one. " +
                "This gate does NOT measure whether the interior bake can run.");

            // COLD ALLOC: List<GameObject>[8] - the generated module prefab set - owner: ModuleInteriorSocketMarkerAuthoring
            List<GameObject> modules = new List<GameObject>(8);
            // COLD ALLOC: List<string>[8] - module asset paths, index-parallel to modules - owner: ModuleInteriorSocketMarkerAuthoring
            List<string> modulePaths = new List<string>(8);
            if (!TryCollectModulePrefabs(modules, modulePaths, report))
            {
                report.Append($"{LogPrefix} RESULT: FAIL");
                Debug.LogError(report.ToString());
                if (Application.isBatchMode)
                    EditorApplication.Exit(1);
                return;
            }

            int failureCount = 0;
            int totalMarkers = 0;

            for (int i = 0; i < modules.Count; i++)
            {
                MarkerAudit audit = AuditMarkers(modules[i]);
                totalMarkers += audit.MarkerCount;
                TryReadRootLocalFileId(modules[i], out long rootFileId);

                report.AppendLine(
                    $"  MODULE {modulePaths[i]}: rootFileId={rootFileId}, " +
                    $"proxyPieces={audit.ProxyPieceCount}, anchors={audit.MarkerCount}, " +
                    $"container={(audit.ContainerPresent ? "present" : "ABSENT")}, " +
                    $"misnamed={audit.MisnamedCount}, microToken={audit.MicroTokenCount}, " +
                    $"facingOutward={audit.OutwardFacingCount}, scaled={audit.ScaledCount} => " +
                    $"{(audit.Passes ? "OK" : "FAIL")}.");

                if (audit.Passes)
                    continue;

                failureCount++;

                if (audit.ProxyPieceCount == 0)
                {
                    report.AppendLine(
                        $"  FAIL {modulePaths[i]}: no COL_*Proxy pieces found, so no anchor position is " +
                        "derivable. The anchors are placed at proxy-piece centres projected onto each " +
                        "piece's inner face (ModuleArchitect1712.EmitInteriorSocketMarkers); with no " +
                        "proxies there is nothing to derive from and this tool will not invent positions.");
                    continue;
                }

                if (audit.MarkerCount != audit.ProxyPieceCount)
                {
                    report.AppendLine(
                        $"  FAIL {modulePaths[i]}: {audit.MarkerCount} anchors against " +
                        $"{audit.ProxyPieceCount} proxy pieces. FIX: run " +
                        "Hecton8.Editor.Authoring.ModuleInteriorSocketMarkerAuthoring.ApplyModuleInteriorSocketMarkers.");
                }

                if (audit.MicroTokenCount > 0)
                {
                    report.AppendLine(
                        $"  FAIL {modulePaths[i]}: {audit.MicroTokenCount} anchor(s) carry a " +
                        "Rivet/Seam/Micro token. InteriorSocketParser1608.ClassifyKind tests those FIRST " +
                        "and returns MicroStamp, and CollectSockets routes MicroStamp markers into a " +
                        "separate list that does NOT satisfy the bake's socket requirement - so those " +
                        "anchors exist and still count as zero.");
                }

                if (audit.OutwardFacingCount > 0)
                {
                    report.AppendLine(
                        $"  FAIL {modulePaths[i]}: {audit.OutwardFacingCount} anchor(s) have +Z pointing " +
                        "AWAY from the module centre. BuildSocket assigns LocalNormal = rotate(rotation, " +
                        "(0,0,1)) (InteriorFinisherStudio1608.cs:871), so instruments would be mounted " +
                        "facing into the hull instead of into the room.");
                }

                if (audit.ScaledCount > 0)
                {
                    report.AppendLine(
                        $"  FAIL {modulePaths[i]}: {audit.ScaledCount} anchor(s) do not have localScale " +
                        "one. ResolveSocketRadius multiplies a 0.18 m base radius by the marker's largest " +
                        "axis scale (InteriorFinisherStudio1608.cs:894-907), so a scaled anchor silently " +
                        "resizes the instrument footprint that sits on it.");
                }

                if (audit.MisnamedCount > 0)
                {
                    report.AppendLine(
                        $"  FAIL {modulePaths[i]}: {audit.MisnamedCount} anchor(s) under the container are " +
                        "not named with the DecorativeSocket prefix. The prefix is load-bearing: the bare " +
                        "Socket_ form collides with the construction sockets this project already ships " +
                        "(ConstructionBootstrapAuthoring.cs:91-94).");
                }
            }

            report.AppendLine(
                $"  SUMMARY: failures={failureCount}, modules={modules.Count}, anchors={totalMarkers}. " +
                "Static asset-graph proof only - not Play Mode, not a bake, not profiler proof. This gate " +
                "measures the marker invariant ONLY; two further blockers are measured by " +
                "VerifyModuleInteriorBakeReadiness.");

            if (failureCount > 0)
            {
                report.Append($"{LogPrefix} RESULT: FAIL");
                Debug.LogError(report.ToString());
            }
            else
            {
                report.Append($"{LogPrefix} RESULT: PASS");
                Debug.Log(report.ToString());
            }

            if (Application.isBatchMode)
                EditorApplication.Exit(failureCount > 0 ? 1 : 0);
        }

        // ══════════════════════════════════════════════════════════
        //  ENTRY POINT 2 — APPLY
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Emits the anchors onto every generated module prefab through the generator-owned
        /// <see cref="ModuleArchitect1712.EmitInteriorSocketMarkers"/>. Adds no other component,
        /// touches no collider, no mesh and no material. Batchmode:
        /// -executeMethod Hecton8.Editor.Authoring.ModuleInteriorSocketMarkerAuthoring.ApplyModuleInteriorSocketMarkers
        /// </summary>
        [MenuItem("Hecton8/Authoring/Emit Module Interior Socket Markers", priority = 224)]
        public static void ApplyModuleInteriorSocketMarkers()
        {
            // COLD ALLOC: List<GameObject>[8] - the generated module prefab set - owner: ModuleInteriorSocketMarkerAuthoring
            List<GameObject> modules = new List<GameObject>(8);
            // COLD ALLOC: List<string>[8] - module asset paths, index-parallel to modules - owner: ModuleInteriorSocketMarkerAuthoring
            List<string> modulePaths = new List<string>(8);
            StringBuilder preflight = new StringBuilder(1024);
            if (!TryCollectModulePrefabs(modules, modulePaths, preflight))
            {
                Debug.LogError($"{LogPrefix} APPLY ABORTED: {preflight}");
                return;
            }

            int written = 0;
            int unchanged = 0;
            int declined = 0;
            int anchorsEmitted = 0;

            for (int i = 0; i < modules.Count; i++)
            {
                switch (ApplyToPrefab(modulePaths[i], out int emitted))
                {
                    case ApplyOutcome.Wrote:
                        written++;
                        anchorsEmitted += emitted;
                        break;

                    case ApplyOutcome.AlreadyAuthored:
                        unchanged++;
                        break;

                    default:
                        declined++;
                        break;
                }
            }

            if (written > 0)
            {
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }

            Debug.Log(
                $"{LogPrefix} APPLY COMPLETE: {written} prefabs written, {anchorsEmitted} anchors emitted, " +
                $"{unchanged} already authored, {declined} declined, of {modules.Count} modules. " +
                "Anchors are derived from the COL_*Proxy pieces, so none can land in a doorway. " +
                "THE INTERIOR BAKE IS STILL BLOCKED: zero authored instrument prefabs exist project-wide, " +
                "and the modules still have no interior visual surface. Run " +
                "VerifyModuleInteriorBakeReadiness for the current state of both.");
        }

        private enum ApplyOutcome
        {
            Declined,
            AlreadyAuthored,
            Wrote
        }

        private static ApplyOutcome ApplyToPrefab(string prefabPath, out int emitted)
        {
            emitted = 0;

            GameObject prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefabAsset == null)
            {
                Debug.LogError($"{LogPrefix} DECLINED '{prefabPath}': prefab not found. Nothing written.");
                return ApplyOutcome.Declined;
            }

            if (!TryReadRootLocalFileId(prefabAsset, out long rootFileIdBefore))
            {
                Debug.LogError(
                    $"{LogPrefix} DECLINED '{prefabPath}': could not read the root local file identifier " +
                    "before writing, so the recipe and scatter bindings could not be protected. Nothing written.");
                return ApplyOutcome.Declined;
            }

            MarkerAudit before = AuditMarkers(prefabAsset);
            if (before.ProxyPieceCount == 0)
            {
                Debug.LogError(
                    $"{LogPrefix} DECLINED '{prefabPath}': no COL_*Proxy pieces, so no anchor position is " +
                    "derivable and this tool will not invent one. Re-fabricate the module set first. " +
                    "Nothing written.");
                return ApplyOutcome.Declined;
            }

            if (before.Passes)
            {
                Debug.Log(
                    $"{LogPrefix} NO CHANGE '{prefabPath}': already carries {before.MarkerCount} valid " +
                    $"anchors for {before.ProxyPieceCount} proxy pieces. Prefab not marked dirty, not saved.");
                return ApplyOutcome.AlreadyAuthored;
            }

            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);
            if (prefabRoot == null)
            {
                Debug.LogError(
                    $"{LogPrefix} DECLINED '{prefabPath}': could not be opened as prefab contents. Nothing written.");
                return ApplyOutcome.Declined;
            }

            bool wroteAsset = false;

            try
            {
                Debug.Log(
                    $"{LogPrefix} BEFORE '{prefabPath}': rootFileId={rootFileIdBefore}, " +
                    $"proxyPieces={before.ProxyPieceCount}, anchors={before.MarkerCount}, " +
                    $"container={(before.ContainerPresent ? "present" : "ABSENT")}.");

                emitted = ModuleArchitect1712.EmitInteriorSocketMarkers(prefabRoot);
                if (emitted <= 0)
                {
                    Debug.LogError(
                        $"{LogPrefix} DECLINED '{prefabPath}': EmitInteriorSocketMarkers returned {emitted}. " +
                        "Every proxy piece failed its pose integrity checks - a piece that is nested, " +
                        "rotated, scaled, or sitting on the opposite side from its name is refused rather " +
                        "than guessed. Nothing written.");
                    return ApplyOutcome.Declined;
                }

                EditorUtility.SetDirty(prefabRoot);
                if (PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath) == null)
                {
                    Debug.LogError(
                        $"{LogPrefix} FAILED '{prefabPath}': SaveAsPrefabAsset returned null. The prefab on " +
                        "disk is unchanged.");
                    return ApplyOutcome.Declined;
                }

                wroteAsset = true;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }

            if (!wroteAsset)
                return ApplyOutcome.Declined;

            if (!VerifyRootFileIdSurvived(prefabPath, rootFileIdBefore))
                return ApplyOutcome.Declined;

            GameObject reloaded = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            MarkerAudit after = reloaded != null ? AuditMarkers(reloaded) : default;
            Debug.Log(
                $"{LogPrefix} AFTER '{prefabPath}': anchors {before.MarkerCount} -> {after.MarkerCount} " +
                $"over {after.ProxyPieceCount} proxy pieces, misnamed={after.MisnamedCount}, " +
                $"microToken={after.MicroTokenCount}, facingOutward={after.OutwardFacingCount}, " +
                $"scaled={after.ScaledCount} => {(after.Passes ? "OK" : "STILL FAILING")}.");

            return after.Passes ? ApplyOutcome.Wrote : ApplyOutcome.Declined;
        }

        // ══════════════════════════════════════════════════════════
        //  ENTRY POINT 3 — BAKE READINESS (read-only, expected red)
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// ANDs all three interior-bake blockers and stays red until every one is cleared. This is
        /// the gate to wire to "can a production interior bake actually run"; it is EXPECTED to fail
        /// today, and the failure names the owner of each remaining condition. Writes nothing.
        /// Batchmode:
        /// -executeMethod Hecton8.Editor.Authoring.ModuleInteriorSocketMarkerAuthoring.VerifyModuleInteriorBakeReadiness
        /// </summary>
        [MenuItem("Hecton8/Validation/Verify Module Interior Bake Readiness", priority = 248)]
        public static void VerifyModuleInteriorBakeReadiness()
        {
            StringBuilder report = new StringBuilder(8192);
            report.AppendLine($"{ReadinessPrefix} INTERIOR BAKE READINESS REPORT");
            report.AppendLine(
                "  The STRICT refusal is FailClosedOnFallbackKit (InteriorFinisherStudio1608.cs:1612-1632), " +
                "which is `if (!library.UsedFallbackKit && authoredSockets) return;` - two independent " +
                "conditions ANDed. This gate adds the third condition that neither of them covers: whether " +
                "an interior surface exists for an instrument to sit on.");

            // COLD ALLOC: List<GameObject>[8] - the generated module prefab set - owner: ModuleInteriorSocketMarkerAuthoring
            List<GameObject> modules = new List<GameObject>(8);
            // COLD ALLOC: List<string>[8] - module asset paths, index-parallel to modules - owner: ModuleInteriorSocketMarkerAuthoring
            List<string> modulePaths = new List<string>(8);
            bool modulesFound = TryCollectModulePrefabs(modules, modulePaths, report);

            int blockers = 0;

            // ── Blocker 2: authored instrument prefabs ──
            string instrumentFolder = InteriorFinisherSettings1608.Default.InstrumentPrefabFolder;
            string instrumentOverride = ReadArgument(InstrumentFolderArgument);
            if (!string.IsNullOrEmpty(instrumentOverride))
                instrumentFolder = instrumentOverride;

            bool instrumentFolderValid = !string.IsNullOrEmpty(instrumentFolder) &&
                                         AssetDatabase.IsValidFolder(instrumentFolder);
            int instrumentPrefabs = instrumentFolderValid
                ? AssetDatabase.FindAssets("t:Prefab", new[] { instrumentFolder }).Length
                : 0;

            report.AppendLine(
                $"  BLOCKER 2 instrumentKit: folder='{instrumentFolder}' " +
                $"overridden={!string.IsNullOrEmpty(instrumentOverride)} exists={instrumentFolderValid} " +
                $"authoredPrefabs={instrumentPrefabs} => {(instrumentPrefabs > 0 ? "CLEAR" : "BLOCKED")}.");

            if (instrumentPrefabs == 0)
            {
                blockers++;
                report.AppendLine(
                    "  BLOCKED instrumentKit: InteriorInstrumentLibraryBuilder1608.Build adds one rule per " +
                    "loadable prefab and falls back to six procedural boxes only when the rule list is still " +
                    "empty, so ONE loadable prefab clears this. Nothing in the project writes to the default " +
                    "folder; EquipmentPropBaker1715 generates instrument-class props into " +
                    "Assets/_Project/Prefabs/Equipment and has never been run. Owner: content, not this file. " +
                    $"Point the bake at a real folder with {InstrumentFolderArgument} once one exists.");
            }

            // ── Blockers 1 and 3, per module ──
            int markerBlockedModules = 0;
            int surfaceBlockedModules = 0;

            for (int i = 0; i < modules.Count; i++)
            {
                MarkerAudit audit = AuditMarkers(modules[i]);
                int interiorFacingTriangles = CountInteriorFacingTriangles(
                    modules[i],
                    out int meshesInspected,
                    out int unreadableMeshes,
                    out int totalTriangles);

                bool markerClear = audit.Passes;
                bool surfaceClear = interiorFacingTriangles > 0;

                if (!markerClear)
                    markerBlockedModules++;
                if (!surfaceClear)
                    surfaceBlockedModules++;

                report.AppendLine(
                    $"  MODULE {modulePaths[i]}: anchors={audit.MarkerCount}/{audit.ProxyPieceCount} " +
                    $"markerInvariant={(markerClear ? "CLEAR" : "BLOCKED")}, " +
                    $"meshes={meshesInspected} unreadable={unreadableMeshes} triangles={totalTriangles} " +
                    $"interiorFacingTriangles={interiorFacingTriangles} " +
                    $"interiorSurface={(surfaceClear ? "CLEAR" : "BLOCKED")}.");
            }

            if (markerBlockedModules > 0)
            {
                blockers++;
                report.AppendLine(
                    $"  BLOCKED markers: {markerBlockedModules} module(s) fail the marker invariant. FIX: " +
                    "Hecton8.Editor.Authoring.ModuleInteriorSocketMarkerAuthoring.ApplyModuleInteriorSocketMarkers. " +
                    "Owner: this file.");
            }

            if (surfaceBlockedModules > 0)
            {
                blockers++;
                report.AppendLine(
                    $"  BLOCKED interiorSurface: {surfaceBlockedModules} module(s) have ZERO triangles whose " +
                    "normal points toward the module centre, measured directly rather than inferred. " +
                    "ModuleArchitect1712 calls AddManufacturedFaceForSocket exactly six times, once per " +
                    "OUTWARD face (:980-985), with no inner shell, lining or winding flip anywhere in the " +
                    "generator; and InteriorFinisherPipeline1608 emits only instrument bases, cable bundles " +
                    "and moving handles (InteriorFinisherStudio1608.cs:1957-2014), never walls. So instruments " +
                    "placed on these anchors would mount to culled backfaces. THIS IS A DESIGN DECISION about " +
                    "what a HECTON-8 module interior is - it is deliberately NOT bolted on by this file, " +
                    "because it changes LOD0/1/2 topology, UVs, the triangle budget, materials and the " +
                    "ColliderOptimizerEngine1716 pre-save validation. Owner: module geometry design.");
            }

            if (!modulesFound)
                blockers++;

            report.AppendLine(
                $"  SUMMARY: blockers={blockers} of 3, modules={modules.Count}. Static asset-graph proof only " +
                "- not Play Mode, not a bake, not profiler proof. A CLEAR on all three would mean the bake can " +
                "RUN, never that the result looks correct; that still needs a render capture and the Visual " +
                "Reference Parity Gate.");

            if (blockers > 0)
            {
                report.Append($"{ReadinessPrefix} RESULT: BLOCKED");
                Debug.LogError(report.ToString());
            }
            else
            {
                report.Append($"{ReadinessPrefix} RESULT: READY");
                Debug.Log(report.ToString());
            }

            if (Application.isBatchMode)
                EditorApplication.Exit(blockers > 0 ? 1 : 0);
        }

        // ══════════════════════════════════════════════════════════
        //  MEASUREMENT
        // ══════════════════════════════════════════════════════════

        private static MarkerAudit AuditMarkers(GameObject prefab)
        {
            if (prefab == null)
                return default;

            int proxyPieces = CountColliderProxyPieces(prefab);

            Transform container = prefab.transform.Find(ModuleArchitect1712.InteriorAnchorRootName);
            if (container == null)
                return new MarkerAudit(proxyPieces, 0, 0, 0, 0, 0, false);

            Bounds bounds = ResolveModuleBounds(prefab);
            Vector3 centreLocal = prefab.transform.InverseTransformPoint(bounds.center);

            int markers = 0;
            int misnamed = 0;
            int microToken = 0;
            int outwardFacing = 0;
            int scaled = 0;

            for (int i = 0; i < container.childCount; i++)
            {
                Transform marker = container.GetChild(i);
                if (marker == null)
                    continue;

                markers++;

                if (!marker.name.StartsWith(ModuleArchitect1712.DecorativeSocketNamePrefix, StringComparison.Ordinal))
                    misnamed++;

                for (int t = 0; t < s_MicroStampTokens.Length; t++)
                {
                    if (marker.name.IndexOf(s_MicroStampTokens[t], StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        microToken++;
                        break;
                    }
                }

                Vector3 markerLocal = prefab.transform.InverseTransformPoint(marker.position);
                Vector3 inward = centreLocal - markerLocal;
                Vector3 forward = (Quaternion.Inverse(prefab.transform.rotation) * marker.rotation) * Vector3.forward;

                // A marker at the exact centre has no meaningful inward direction; that is a defect in
                // its own right and is counted as outward-facing rather than silently passed.
                if (inward.sqrMagnitude <= 1e-8f || Vector3.Dot(forward.normalized, inward.normalized) <= 0f)
                    outwardFacing++;

                Vector3 scale = marker.localScale;
                if (Mathf.Abs(scale.x - 1f) > 0.001f ||
                    Mathf.Abs(scale.y - 1f) > 0.001f ||
                    Mathf.Abs(scale.z - 1f) > 0.001f)
                {
                    scaled++;
                }
            }

            return new MarkerAudit(proxyPieces, markers, misnamed, microToken, outwardFacing, scaled, true);
        }

        private static int CountColliderProxyPieces(GameObject prefab)
        {
            s_ColliderScratch.Clear();
            prefab.GetComponentsInChildren(true, s_ColliderScratch);
            try
            {
                int count = 0;
                for (int i = 0; i < s_ColliderScratch.Count; i++)
                {
                    BoxCollider candidate = s_ColliderScratch[i];
                    if (candidate == null || candidate.isTrigger)
                        continue;

                    // Kept as a broad StartsWith("COL_") test rather than a second copy of the emitter's
                    // six-prefix table, ON PURPOSE: a renamed or added surface then shows up as
                    // anchors != proxyPieces, which is a reported failure, instead of vanishing from both
                    // sides of the comparison at once.
                    if (!candidate.name.StartsWith("COL_", StringComparison.Ordinal))
                        continue;

                    // The two ColliderOptimizerEngine1716 aggregate roots are the one known exception:
                    // they are COL_-prefixed but are not module surfaces, so the emitter correctly
                    // produces no anchor for them. Neither exists on any of the six modules today
                    // (censused 2026-07-29 - every COL_ child is one of the six surfaces), but that
                    // engine is referenced from ModuleArchitect1712 and several sibling generators, so
                    // one appearing later would otherwise inflate this count by one or two and hold the
                    // marker gate permanently red for a reason that has nothing to do with markers.
                    if (string.Equals(candidate.name, ColliderOptimizerCompoundRootName, StringComparison.Ordinal) ||
                        string.Equals(candidate.name, ColliderOptimizerConvexRootName, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    count++;
                }

                return count;
            }
            finally
            {
                s_ColliderScratch.Clear();
            }
        }

        /// <summary>
        /// Counts triangles whose geometric normal points toward the module centre - a direct
        /// measurement of "is there a surface visible from inside this module", not a proxy for it.
        /// </summary>
        /// <remarks>
        /// <c>Mesh.vertices</c> and <c>Mesh.triangles</c> allocate copies and are banned in hot paths
        /// by AGENTS.md `Runtime Hot-Path Law`. This is editor-only, cold, once-per-asset measurement
        /// code, which is the documented exception; the arrays are local and dropped immediately.
        /// A mesh with <c>isReadable == false</c> is counted as unreadable and reported rather than
        /// read, because reading it would throw.
        /// </remarks>
        private static int CountInteriorFacingTriangles(
            GameObject prefab,
            out int meshesInspected,
            out int unreadableMeshes,
            out int totalTriangles)
        {
            meshesInspected = 0;
            unreadableMeshes = 0;
            totalTriangles = 0;
            if (prefab == null)
                return 0;

            Bounds bounds = ResolveModuleBounds(prefab);
            Vector3 centreWorld = bounds.center;

            s_MeshFilterScratch.Clear();
            prefab.GetComponentsInChildren(true, s_MeshFilterScratch);
            try
            {
                int interiorFacing = 0;
                for (int m = 0; m < s_MeshFilterScratch.Count; m++)
                {
                    MeshFilter filter = s_MeshFilterScratch[m];
                    Mesh mesh = filter != null ? filter.sharedMesh : null;
                    if (mesh == null)
                        continue;

                    meshesInspected++;
                    if (!mesh.isReadable)
                    {
                        unreadableMeshes++;
                        continue;
                    }

                    // COLD ALLOC: Vector3[] and int[] copies from Mesh - editor-only interior-surface probe - owner: ModuleInteriorSocketMarkerAuthoring
                    Vector3[] vertices = mesh.vertices;
                    int[] triangles = mesh.triangles;
                    if (vertices == null || triangles == null || vertices.Length == 0)
                        continue;

                    Transform meshTransform = filter.transform;
                    totalTriangles += triangles.Length / 3;

                    for (int t = 0; t + 2 < triangles.Length; t += 3)
                    {
                        int i0 = triangles[t];
                        int i1 = triangles[t + 1];
                        int i2 = triangles[t + 2];
                        if ((uint)i0 >= (uint)vertices.Length ||
                            (uint)i1 >= (uint)vertices.Length ||
                            (uint)i2 >= (uint)vertices.Length)
                        {
                            continue;
                        }

                        Vector3 a = meshTransform.TransformPoint(vertices[i0]);
                        Vector3 b = meshTransform.TransformPoint(vertices[i1]);
                        Vector3 c = meshTransform.TransformPoint(vertices[i2]);
                        Vector3 normal = Vector3.Cross(b - a, c - a);
                        if (normal.sqrMagnitude <= 1e-12f)
                            continue;

                        Vector3 centroid = (a + b + c) / 3f;
                        Vector3 toCentre = centreWorld - centroid;
                        if (toCentre.sqrMagnitude <= 1e-12f)
                            continue;

                        if (Vector3.Dot(normal.normalized, toCentre.normalized) >= InteriorFacingDotThreshold)
                            interiorFacing++;
                    }
                }

                return interiorFacing;
            }
            finally
            {
                s_MeshFilterScratch.Clear();
            }
        }

        /// <summary>
        /// Bounds of the module, preferring the collider proxy shell over meshes because the proxies
        /// define the volume the interior actually occupies.
        /// </summary>
        /// <remarks>
        /// Computed from <c>BoxCollider.center</c>/<c>size</c> and <c>Mesh.bounds</c> through the
        /// transform rather than read from <c>Collider.bounds</c> or <c>Renderer.bounds</c>. Those two
        /// properties are resolved by the physics and rendering backends, and these objects are PREFAB
        /// ASSETS - not instances in a loaded scene - so their backend-derived AABBs are not a
        /// dependency worth taking for a value that decides whether a blocker reports CLEAR or BLOCKED.
        /// The manual path is pure serialized data and behaves identically for an asset and an instance.
        /// Degrades to a unit box at the root, which makes the interior probe report "no interior found"
        /// rather than throw.
        /// </remarks>
        private static Bounds ResolveModuleBounds(GameObject prefab)
        {
            bool initialized = false;
            Bounds bounds = new Bounds(prefab.transform.position, Vector3.one);

            s_ColliderScratch.Clear();
            prefab.GetComponentsInChildren(true, s_ColliderScratch);
            try
            {
                for (int i = 0; i < s_ColliderScratch.Count; i++)
                {
                    BoxCollider candidate = s_ColliderScratch[i];
                    if (candidate == null || candidate.isTrigger)
                        continue;

                    if (!candidate.name.StartsWith("COL_", StringComparison.Ordinal))
                        continue;

                    EncapsulateLocalBox(
                        candidate.transform,
                        candidate.center,
                        candidate.size,
                        ref initialized,
                        ref bounds);
                }
            }
            finally
            {
                s_ColliderScratch.Clear();
            }

            if (initialized)
                return bounds;

            s_MeshFilterScratch.Clear();
            prefab.GetComponentsInChildren(true, s_MeshFilterScratch);
            try
            {
                for (int i = 0; i < s_MeshFilterScratch.Count; i++)
                {
                    MeshFilter filter = s_MeshFilterScratch[i];
                    Mesh mesh = filter != null ? filter.sharedMesh : null;
                    if (mesh == null)
                        continue;

                    Bounds meshBounds = mesh.bounds;
                    EncapsulateLocalBox(
                        filter.transform,
                        meshBounds.center,
                        meshBounds.size,
                        ref initialized,
                        ref bounds);
                }
            }
            finally
            {
                s_MeshFilterScratch.Clear();
            }

            return bounds;
        }

        /// <summary>
        /// Encapsulates one local-space box, transformed corner by corner so a rotated child is covered
        /// correctly rather than by its unrotated extents.
        /// </summary>
        private static void EncapsulateLocalBox(
            Transform owner,
            Vector3 localCenter,
            Vector3 localSize,
            ref bool initialized,
            ref Bounds bounds)
        {
            if (owner == null)
                return;

            Vector3 half = localSize * 0.5f;
            for (int corner = 0; corner < 8; corner++)
            {
                Vector3 local = localCenter + new Vector3(
                    (corner & 1) == 0 ? -half.x : half.x,
                    (corner & 2) == 0 ? -half.y : half.y,
                    (corner & 4) == 0 ? -half.z : half.z);
                Vector3 world = owner.TransformPoint(local);

                if (!initialized)
                {
                    bounds = new Bounds(world, Vector3.zero);
                    initialized = true;
                    continue;
                }

                bounds.Encapsulate(world);
            }
        }

        // ══════════════════════════════════════════════════════════
        //  TARGET RESOLUTION AND FILEID PROTECTION
        // ══════════════════════════════════════════════════════════

        private static bool TryCollectModulePrefabs(List<GameObject> modules, List<string> modulePaths, StringBuilder report)
        {
            if (!AssetDatabase.IsValidFolder(ModulePrefabFolder))
            {
                report.AppendLine(
                    $"  BLOCKER: module prefab folder '{ModulePrefabFolder}' does not exist. Fabricate the " +
                    "module set first: -executeMethod " +
                    "Hecton8.Editor.Structures.ModuleArchitect1712.FabricateDefaultSetFromMenu");
                return false;
            }

            // COLD ALLOC: string[] from AssetDatabase.FindAssets - one-shot editor module scan - owner: ModuleInteriorSocketMarkerAuthoring
            string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { ModulePrefabFolder });
            Array.Sort(guids, StringComparer.Ordinal);

            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null)
                    continue;

                modules.Add(prefab);
                modulePaths.Add(path);
            }

            if (modules.Count == 0)
            {
                report.AppendLine(
                    $"  BLOCKER: no prefab under '{ModulePrefabFolder}' could be loaded as a module.");
                return false;
            }

            return true;
        }

        private static bool TryReadRootLocalFileId(GameObject prefabAsset, out long localFileId)
        {
            localFileId = 0L;
            return prefabAsset != null &&
                   AssetDatabase.TryGetGUIDAndLocalFileIdentifier(prefabAsset, out string _, out localFileId);
        }

        /// <summary>
        /// Proves the root fileID did not move across SaveAsPrefabAsset, then re-resolves any recipe
        /// that binds this prefab. Every reference to these prefabs binds (guid, root fileID) rather
        /// than the format-stable 100100000, so this one invariant covers the recipe rows and the
        /// scatter catalogs at once; the recipe re-resolution is the end-to-end confirmation.
        /// </summary>
        private static bool VerifyRootFileIdSurvived(string prefabPath, long rootFileIdBefore)
        {
            GameObject reloaded = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (reloaded == null)
            {
                Debug.LogError(
                    $"{LogPrefix} POST-WRITE CHECK FAILED '{prefabPath}': the prefab no longer loads. " +
                    "Restore it before trusting any recipe binding.");
                return false;
            }

            if (!TryReadRootLocalFileId(reloaded, out long rootFileIdAfter))
            {
                Debug.LogError(
                    $"{LogPrefix} POST-WRITE CHECK FAILED '{prefabPath}': root local file identifier " +
                    "unreadable after the write. Verify the recipe bindings by hand.");
                return false;
            }

            if (rootFileIdAfter != rootFileIdBefore)
            {
                Debug.LogError(
                    $"{LogPrefix} ROOT FILEID MOVED '{prefabPath}': {rootFileIdBefore} -> {rootFileIdAfter}. " +
                    $"Every reference binds the root GameObject fileID, so the recipes under '{RecipeFolder}' " +
                    "and the ProceduralFamily scatter catalogs now point at nothing and the module will spawn " +
                    "as null. Revert this prefab and rebind before proceeding.");
                return false;
            }

            string boundRecipe = ResolveBindingRecipeName(prefabPath);
            Debug.Log(
                $"{LogPrefix} ROOT FILEID STABLE '{prefabPath}': {rootFileIdAfter} unchanged across " +
                $"SaveAsPrefabAsset, boundRecipe={boundRecipe}.");
            return true;
        }

        /// <summary>
        /// Name of the recipe whose finalPrefab is this prefab, or a marker when none binds it.
        /// Compared by asset path, not by object reference: SaveAsPrefabAsset triggers a reimport and
        /// the recipe's resolved instance may legitimately differ while the on-disk binding is intact.
        /// </summary>
        private static string ResolveBindingRecipeName(string prefabPath)
        {
            if (!AssetDatabase.IsValidFolder(RecipeFolder))
                return "<recipe folder missing>";

            // COLD ALLOC: string[] from AssetDatabase.FindAssets - one-shot editor recipe scan - owner: ModuleInteriorSocketMarkerAuthoring
            string[] guids = AssetDatabase.FindAssets("t:BuildableData", new[] { RecipeFolder });
            for (int i = 0; i < guids.Length; i++)
            {
                string recipePath = AssetDatabase.GUIDToAssetPath(guids[i]);
                BuildableData recipe = AssetDatabase.LoadAssetAtPath<BuildableData>(recipePath);
                if (recipe == null || recipe.finalPrefab == null)
                    continue;

                if (string.Equals(AssetDatabase.GetAssetPath(recipe.finalPrefab), prefabPath, StringComparison.Ordinal))
                    return recipe.name;
            }

            return "<unbound>";
        }

        /// <summary>
        /// Reads the value after a named command-line switch, or null. <c>-executeMethod</c> cannot pass
        /// parameters, so overrides come off the process command line.
        /// </summary>
        /// <remarks>
        /// <c>System.Environment</c> is spelled out deliberately: a bare <c>Environment</c> inside a
        /// <c>Hecton8.*</c> namespace binds to <c>Hecton8.Environment</c> and fails CS0234.
        /// </remarks>
        private static string ReadArgument(string switchName)
        {
            string[] args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (string.Equals(args[i], switchName, StringComparison.Ordinal))
                    return args[i + 1];
            }

            return null;
        }
    }
}
#endif
