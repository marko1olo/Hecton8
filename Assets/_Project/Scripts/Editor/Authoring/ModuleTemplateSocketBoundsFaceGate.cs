// ============================================================================
// HECTON-8 — ModuleTemplateSocketBoundsFaceGate.cs
//
// FIRST_20_MINUTES moment served: "Craft/repair/build". A socket that does not
// sit on the face it points out of places the next module with a gap or an
// overlap at the seam, so the second thing the player builds does not meet the
// first. That is a route blocker, not hygiene.
//
// THE DEFECT, and how the correct rule was actually established
// ---------------------------------------------------------------------------
// BaseModuleTemplate_Foundation.asset authors socketDefinitions[0] as
// localPosition (0, 1, 0) with direction Top, over proxyBoundsCenter (0, -1, 0)
// and proxyBoundsSize (8, 2, 8). Its top face is therefore
//   center.y + size.y * 0.5f = -1 + 1 = 0
// and the socket sits 1 m above it. It is the only socket in
// Data/Construction/StandardModuleTemplates that misses its own face.
//
// THE SIBLING TEMPLATES DO NOT PROVE THE RULE. This has to be said plainly,
// because deriving the rule from them alone is how the wrong fix gets authored.
// Nine of the ten templates author proxyBoundsCenter (0, 0, 0) - the three
// socketless ones (CurrentTurbine, ServicePump, UtilityPylon) carry float dust
// around 6e-8 and no sockets at all. For a zero centre these two statements are
// numerically identical:
//   RULE A  socket[axis] == size[axis] * 0.5f          (centre-blind)
//   RULE B  socket[axis] == centre[axis] + size[axis] * 0.5f
// Foundation is the ONLY template with a non-zero centre, so the siblings are
// silent on exactly the axis in dispute. Under RULE A the Foundation socket
// (y = size.y/2 = 1) is already correct and there is no defect at all.
// ModuleArchitect1712.cs:231-236 states RULE A in prose - "each socket's
// localPosition is exactly proxyBoundsSize/2 on its axis" - and is wrong about
// the hand-authored kit for this one asset for exactly this reason.
//
// THE TIE IS BROKEN BY THE CONSUMERS, not by the data. Every live geometry
// consumer honours proxyBoundsCenter:
//   • PlayerBuilder.cs:3330-3332 - terrain clearance sampler takes the BOTTOM
//     face as GridToLocal(...).y + ProxyBoundsCenter.y - ProxyBoundsSize.y*0.5f.
//     That is RULE B applied to the opposite face of the same box.
//   • PlayerBuilder.cs:3594-3605 - the eight-corner voxel SDF probe samples
//     center +/- extents.
//   • PlayerBuilder.cs:3325-3328 - ModularBaseConstructionValidator.BuildBounds
//     is handed (ProxyBoundsCenter, ProxyBoundsSize).
//   • PlayerBuilder.cs:3476, :3494, :3515 - the ghost preview DTO carries
//     BoundsCenter (ShinobuSocketConstructionData.cs:176, :185) alongside the
//     scale.
//   • PrefabAssemblerEngine.cs:859-870 - the interior trigger is placed at
//     ProxyBoundsCenter.
// The ONE path that drops the centre is the baked catalog:
// BaseModuleCatalogRuntime.TryBuildModuleFromTemplate (:909-931) writes
// ModuleDefinitionDTO.BoundingBoxExtents from ProxyBoundsSize alone, and
// ModuleDefinitionDTO has no centre field at all (:20-33). If that DTO drove
// placement, RULE A would win and this gate would be wrong. It does not:
// BoundingBoxExtents has NO geometry consumer anywhere in the project. It is
// written at :921, printed by a debug dump at BaseModuleCatalogEditorTools.cs:765,
// and asserted by one edit test. Nothing reads it to place, overlap, or snap.
// So RULE B is the live contract, the Foundation socket is genuinely 1 m off,
// and the fix is the socket rather than the bounds.
//
// WHY NOT MOVE proxyBoundsCenter TO ZERO INSTEAD - the other arithmetic fix.
//   Setting centre.y = 0 also makes 1 the top face, and is REJECTED. It moves
//   localBottomY (PlayerBuilder.cs:3330-3332) from -2 to -1 and lifts all eight
//   SDF corners by 1 m while the mesh stays where it is, so the terrain and
//   voxel proxies would stop matching the geometry. That trades an off-face
//   socket for a placement lie about the seabed contact, which is worse.
//   The template's own physics authoring independently confirms which of the two
//   envelopes is real: buoyancyDisplacementVolumeCubicMeters is 6, and 8x2x8 is
//   128 m3 of envelope, so 6 m3 of displacement is admissible. Against the bound
//   prefab's actual 4 x 0.35 x 4 collider that envelope is 5.6 m3 and a 6 m3
//   displacement is physically impossible. The 8 x 2 x 8 box is the coherent
//   artifact; it is not the field to move.
//
// WHAT THIS GATE DELIBERATELY DOES NOT DO
//   It does not touch proxyBoundsCenter, proxyBoundsSize, direction,
//   compatibleType, vfxSockets, or any simulation field. It does not add or
//   remove sockets. It writes ONE float per offending socket - the component on
//   the socket's own normal axis - plus the matching legacy snapPoints entry,
//   and it declines rather than guessing whenever the pairing is not provable.
//
//   It also does not reconcile the template with its bound prefab. That
//   divergence is real and reported (see ReportPrefabSocketTopologyDivergence),
//   and for Foundation it is total: the template authors one Top socket over an
//   8 x 2 x 8 box, while PFB_Module_Foundation is a 4 x 0.35 x 4 plate carrying
//   four ModuleSocket children at (0,0,+/-2.05) and (+/-2.05,0,0), every one of
//   them serialised `direction: 0` (North) with an empty lane. Choosing which
//   side ships is a design call under construction.md, not a derivation, and
//   ConstructionCatalogRepairAuthoring.cs:45-50 records that this prefab family
//   is legacy primitive geometry behind
//   WorldProceduralFinalPrefabQualityGate.AllowLegacyPrimitiveFinalAuthoring.
//   Inventing either side here would be authoring geometry truth, so the gate
//   reports it and stops.
//
// SCOPE - WHY THE ABANDONED SET IS REPORT-ONLY
//   Data/Construction/AbandonedModuleTemplates predates socketDefinitions,
//   proxyBoundsCenter and proxyBoundsSize entirely: those keys are absent from
//   the YAML, so the fields load at their C# defaults (centre zero, size
//   (4,4,4) from BaseModuleTemplate.cs:90) and BaseModuleTemplate.OnValidate
//   derives sockets from the legacy snapPoints (:233-234, :257-267). Their
//   snapPoints sit at +/-3 against a defaulted +/-2 face, so EVERY socket in
//   that set reads as off-face against bounds nobody authored. Rewriting them
//   would move the ruin kit's geometry to satisfy a default. They are reported
//   as INFO and never written - the same exclusion, for the same reason, that
//   ModuleSocketLaneVocabularyGate.cs:32-39 already applies to this folder.
//
// PERSISTED IDENTITY IS NOT AT RISK. templateHashId is LocHash over stableId
// alone (BaseModuleTemplate.cs:242), so no geometric field reaches any persisted
// hash and no save migration follows. The gate still logs the asset guid, its
// local file identifier and templateHashId before and after every write, because
// "no hash moved" is a claim that should be printed rather than assumed.
//
// A RE-BAKE IS REQUIRED AFTER APPLY. SocketDefinitionDTO.LocalOffset is baked
// into Data/Construction/BaseModuleCatalog.h8bin
// (BaseModuleCatalogRuntime.BuildSocketDTO :884-892, written by the
// Hecton8/Construction/Base Module Catalog window). Until that re-bake the
// binary keeps the old offset. No save migration: SocketDefinitionDTO appears
// nowhere in SaveData.cs.
//
// PROOF CLASS: static asset-graph authoring in the Editor. A PASS means every
// production socket lies on the face its direction points out of. It is not
// Play Mode, placement, or profiler proof.
// ============================================================================

#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using Hecton.Localization;
using Hecton8.Building;
using Hecton8.Construction;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

namespace Hecton8.Editor.Authoring
{
    /// <summary>
    /// Verifies and repairs the one geometric invariant that decides whether two
    /// placed modules meet at their seam: every authored
    /// <see cref="BaseModuleTemplate.SocketDefinition"/> must lie on the proxy
    /// bounds face its <see cref="ModuleSocketDirection"/> points out of, where
    /// that face is <c>proxyBoundsCenter[axis] +/- proxyBoundsSize[axis] * 0.5</c>.
    /// Idempotent: a second run detects the authored state and writes nothing.
    /// </summary>
    public static class ModuleTemplateSocketBoundsFaceGate
    {
        private const string LogPrefix = "[ModuleSocketBoundsFaceGate]";

        /// <summary>
        /// The production kit. Only templates under this folder are ever written.
        /// Same literal ConstructionCatalogRepairAuthoring.cs:174 pins.
        /// </summary>
        private const string ProductionTemplateFolder =
            "Assets/_Project/Data/Construction/StandardModuleTemplates";

        /// <summary>
        /// Procedural ruin set. Reported, never written - see SCOPE in the header.
        /// </summary>
        private const string AbandonedTemplateFolder =
            "Assets/_Project/Data/Construction/AbandonedModuleTemplates";

        private const string ConstructionDataFolder = "Assets/_Project/Data/Construction";

        // BaseModuleTemplate private serialized fields (BaseModuleTemplate.cs:80-90).
        private const string SocketDefinitionsField = "socketDefinitions";   // :83
        private const string SnapPointsField = "snapPoints";                 // :80
        private const string SocketLocalPositionField = "localPosition";     // :50, a real Vector3
        private const string ProxyBoundsCenterField = "proxyBoundsCenter";   // :87
        private const string ProxyBoundsSizeField = "proxyBoundsSize";       // :90

        /// <summary>
        /// Deviation, in metres, above which a socket counts as off its face.
        /// One millimetre: four orders of magnitude below the smallest authored
        /// feature in the kit (the 0.35 m foundation plate) and four orders above
        /// the ~6e-8 float dust in the socketless templates' bounds centres.
        /// </summary>
        private const float FaceToleranceMeters = 0.001f;

        /// <summary>
        /// Per-axis floor below which proxy bounds are refused as degenerate.
        /// Mirrors ContentSanityValidator.cs:2876-2877 (&lt;= 0.01 is an error) and
        /// BaseModuleTemplate.cs:239-240, which silently replaces a degenerate size
        /// with a 4 m cube. A face computed from a size that is about to be
        /// overwritten is not a face.
        /// </summary>
        private const float MinimumBoundsAxisMeters = 0.01f;

        /// <summary>
        /// Magnitude below which a socket-normal component counts as zero. Unitless,
        /// deliberately NOT the metre tolerance above: the normals come from
        /// BaseModuleCatalogRuntime.DirectionToNormal (:955-967), which returns
        /// exact 0 and +/-1, so anything in between means the direction table or the
        /// enum value is malformed rather than imprecise.
        /// </summary>
        private const float NormalComponentEpsilon = 1e-4f;

        /// <summary>One template's read state, resolved once and reused by verify and apply.</summary>
        private readonly struct TemplateRow
        {
            public TemplateRow(string assetPath, BaseModuleTemplate template, bool writable)
            {
                AssetPath = assetPath;
                Template = template;
                Writable = writable;
            }

            public string AssetPath { get; }

            public BaseModuleTemplate Template { get; }

            /// <summary>True only for the production folder. The ruin set is report-only.</summary>
            public bool Writable { get; }
        }

        /// <summary>One socket's face evaluation.</summary>
        private readonly struct SocketFaceEvaluation
        {
            public SocketFaceEvaluation(
                int socketIndex,
                ModuleSocketDirection direction,
                int normalAxis,
                float authoredOnAxis,
                float expectedOnAxis,
                float tangentialDeviation,
                bool boundsUsable)
            {
                SocketIndex = socketIndex;
                Direction = direction;
                NormalAxis = normalAxis;
                AuthoredOnAxis = authoredOnAxis;
                ExpectedOnAxis = expectedOnAxis;
                TangentialDeviation = tangentialDeviation;
                BoundsUsable = boundsUsable;
            }

            public int SocketIndex { get; }

            public ModuleSocketDirection Direction { get; }

            /// <summary>0 = x, 1 = y, 2 = z. -1 when the direction has no single axis.</summary>
            public int NormalAxis { get; }

            public float AuthoredOnAxis { get; }

            public float ExpectedOnAxis { get; }

            /// <summary>
            /// Largest deviation on the two axes that are NOT the socket normal,
            /// measured against the bounds centre. Every socket in the shipped kit
            /// is centred on its face, so a non-zero value here is unauthored
            /// intent and blocks the write instead of being silently squared up.
            /// </summary>
            public float TangentialDeviation { get; }

            public bool BoundsUsable { get; }

            public float AxisDeviation => AuthoredOnAxis - ExpectedOnAxis;

            public bool OnFace => BoundsUsable && Mathf.Abs(AxisDeviation) <= FaceToleranceMeters;
        }

        // ══════════════════════════════════════════════════════════
        //  ENTRY POINT — VERIFY (read-only)
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Read-only gate. Writes nothing at all - no SerializedObject apply, no
        /// SetDirty, no SaveAssets - per the automated-runner clause in AGENTS.md
        /// `Evidence Law`. Batch usage:
        /// -executeMethod Hecton8.Editor.Authoring.ModuleTemplateSocketBoundsFaceGate.VerifyModuleTemplateSocketBoundsFaces
        /// </summary>
        [MenuItem("Hecton8/Validation/Verify Module Template Socket Bounds Faces", priority = 245)]
        public static void VerifyModuleTemplateSocketBoundsFaces()
        {
            StringBuilder report = new StringBuilder(8192);
            report.AppendLine($"{LogPrefix} SOCKET BOUNDS FACE REPORT");
            report.AppendLine(
                "  RULE (established from the live consumers, not from the sibling assets): a socket must lie on " +
                "proxyBoundsCenter[axis] + sign * proxyBoundsSize[axis] * 0.5 for the axis its direction points " +
                "out of. PlayerBuilder.cs:3330-3332 applies that same expression to the opposite face; " +
                "ModuleDefinitionDTO.BoundingBoxExtents drops the centre but has no geometry consumer.");

            // COLD ALLOC: List<TemplateRow>[32] - one row per BaseModuleTemplate asset in the project - owner: ModuleTemplateSocketBoundsFaceGate
            List<TemplateRow> rows = new List<TemplateRow>(32);
            CollectTemplates(rows);

            int failureCount = 0;
            int warningCount = 0;
            int socketCount = 0;
            int offFaceCount = 0;

            for (int i = 0; i < rows.Count; i++)
            {
                TemplateRow row = rows[i];
                BaseModuleTemplate.SocketDefinition[] definitions = row.Template.SocketDefinitions;
                int definitionCount = definitions != null ? definitions.Length : 0;

                Vector3 center = row.Template.ProxyBoundsCenter;
                Vector3 size = row.Template.ProxyBoundsSize;
                float3[] snapPoints = row.Template.SnapPoints;
                int snapCount = snapPoints != null ? snapPoints.Length : 0;

                report.AppendLine(
                    $"  ASSET {row.AssetPath}: scope={(row.Writable ? "PRODUCTION" : "REPORT-ONLY")}, " +
                    $"hash={row.Template.ResolvePersistentHashId()}, center={FormatVector(center)}, " +
                    $"size={FormatVector(size)}, sockets={definitionCount}, snapPoints={snapCount}.");

                if (definitionCount == 0)
                    continue;

                if (!row.Writable)
                {
                    warningCount++;
                    report.AppendLine(
                        $"  INFO {row.AssetPath}: outside the production folder, so it is measured and never " +
                        "written. The AbandonedModuleTemplates set has no authored socketDefinitions, " +
                        "proxyBoundsCenter or proxyBoundsSize keys at all - those load at the C# defaults " +
                        "(centre zero, size 4,4,4 from BaseModuleTemplate.cs:90) and OnValidate derives sockets " +
                        "from legacy snapPoints (BaseModuleTemplate.cs:233-234). Any off-face reading below is " +
                        "against defaults nobody authored, exactly the exclusion " +
                        "ModuleSocketLaneVocabularyGate.cs:32-39 already applies here.");
                }

                bool boundsUsable = EvaluateBoundsUsable(size);
                if (!boundsUsable)
                {
                    failureCount++;
                    report.AppendLine(
                        $"  FAIL {row.AssetPath}: proxyBoundsSize {FormatVector(size)} has an axis at or below " +
                        $"{MinimumBoundsAxisMeters} m. ContentSanityValidator.cs:2876-2877 already rejects this and " +
                        "BaseModuleTemplate.cs:239-240 silently replaces it with a 4 m cube, so no face can be " +
                        "computed. Fix the bounds before the sockets.");
                }

                if (snapCount != definitionCount)
                {
                    warningCount++;
                    report.AppendLine(
                        $"  WARN {row.AssetPath}: snapPoints holds {snapCount} entries against {definitionCount} " +
                        "socketDefinitions. BaseModuleTemplate.BuildSnapPointsFromSocketDefinitions " +
                        "(BaseModuleTemplate.cs:272-279) produces one snap point per socket in order, so the " +
                        "index pairing is not provable here and Apply will decline this asset rather than guess " +
                        "which legacy point belongs to which socket.");
                }

                for (int s = 0; s < definitionCount; s++)
                {
                    socketCount++;
                    SocketFaceEvaluation evaluation = EvaluateSocket(definitions[s], s, center, size, boundsUsable);

                    if (evaluation.NormalAxis < 0)
                    {
                        failureCount++;
                        report.AppendLine(
                            $"  FAIL {row.AssetPath}[socket {s}]: direction {evaluation.Direction} does not resolve " +
                            "to a single axis normal through BaseModuleCatalogRuntime.DirectionToNormal " +
                            "(BaseModuleCatalogRuntime.cs:955-967), so it has no face. The enum value is outside " +
                            "ModuleSocketDirection (ModuleSocket.cs:10-18).");
                        continue;
                    }

                    if (evaluation.TangentialDeviation > FaceToleranceMeters)
                    {
                        warningCount++;
                        report.AppendLine(
                            $"  WARN {row.AssetPath}[socket {s}]: direction {evaluation.Direction} sits " +
                            $"{evaluation.TangentialDeviation:0.####} m off the centre of its face on the two " +
                            "tangential axes. Every socket in the shipped kit is face-centred, so this is either " +
                            "deliberate offset authoring or a second defect. Apply corrects the normal axis only " +
                            "and declines this socket, because squaring up a deliberately offset hatch would " +
                            "invent placement geometry.");
                    }

                    if (evaluation.OnFace)
                        continue;

                    if (!boundsUsable)
                        continue;

                    offFaceCount++;
                    string severity = row.Writable ? "FAIL" : "INFO";
                    if (row.Writable)
                        failureCount++;

                    report.AppendLine(
                        $"  {severity} {row.AssetPath}[socket {s}]: direction {evaluation.Direction} authored at " +
                        $"{AxisName(evaluation.NormalAxis)}={evaluation.AuthoredOnAxis:0.####} but its own bounds " +
                        $"face is {AxisName(evaluation.NormalAxis)}={evaluation.ExpectedOnAxis:0.####} " +
                        $"(deviation {evaluation.AxisDeviation:+0.####;-0.####}). Two modules butted at this seam " +
                        "would meet with that exact gap or overlap: the habitat graph joins modules only when two " +
                        "socket world positions quantise to the same cell on opposite axes " +
                        "(HabitatConstructionManager.IndexCandidateSocket, :925-943), so the mesh contact and the " +
                        "graph connection cannot both be right.");
                }
            }

            warningCount += ReportPrefabSocketTopologyDivergence(report);

            report.AppendLine(
                $"  SUMMARY: failures={failureCount}, warnings={warningCount}, templates={rows.Count}, " +
                $"socketsInspected={socketCount}, offFaceSockets={offFaceCount}. Static asset-graph proof only - " +
                "not Play Mode, not placement proof, not profiler proof.");

            if (failureCount > 0)
            {
                report.Append($"{LogPrefix} RESULT: FAIL");
                Debug.LogError(report.ToString());
            }
            else if (warningCount > 0)
            {
                report.Append($"{LogPrefix} RESULT: PASS WITH WARNINGS");
                Debug.LogWarning(report.ToString());
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
        //  ENTRY POINT — APPLY
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Moves every off-face production socket onto its own bounds face along
        /// the socket normal, and nothing else. Batch usage:
        /// -executeMethod Hecton8.Editor.Authoring.ModuleTemplateSocketBoundsFaceGate.ApplyModuleTemplateSocketBoundsFaces
        /// </summary>
        [MenuItem("Hecton8/Authoring/Align Module Template Sockets To Bounds Faces", priority = 222)]
        public static void ApplyModuleTemplateSocketBoundsFaces()
        {
            // COLD ALLOC: List<TemplateRow>[32] - one row per BaseModuleTemplate asset in the project - owner: ModuleTemplateSocketBoundsFaceGate
            List<TemplateRow> rows = new List<TemplateRow>(32);
            CollectTemplates(rows);

            int written = 0;
            int unchanged = 0;
            int declined = 0;
            int socketsMoved = 0;

            for (int i = 0; i < rows.Count; i++)
            {
                TemplateRow row = rows[i];
                if (!row.Writable)
                    continue;

                switch (ApplyToTemplate(row, out int movedInThisAsset))
                {
                    case ApplyOutcome.Wrote:
                        written++;
                        socketsMoved += movedInThisAsset;
                        break;

                    case ApplyOutcome.AlreadyAligned:
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
                $"{LogPrefix} APPLY COMPLETE: {written} templates written, {socketsMoved} sockets moved onto their " +
                $"bounds face, {unchanged} already aligned, {declined} declined. Bounds, direction, lane and " +
                "stableId were not touched, so templateHashId did not move (BaseModuleTemplate.cs:242 folds " +
                "stableId alone) and no save migration follows. REQUIRED NEXT STEP: re-bake " +
                "Data/Construction/BaseModuleCatalog.h8bin through Hecton8/Construction/Base Module Catalog, " +
                "because SocketDefinitionDTO.LocalOffset is baked from these values " +
                "(BaseModuleCatalogRuntime.cs:884-892).");
        }

        private enum ApplyOutcome
        {
            Declined,
            AlreadyAligned,
            Wrote
        }

        private static ApplyOutcome ApplyToTemplate(TemplateRow row, out int socketsMoved)
        {
            socketsMoved = 0;

            BaseModuleTemplate template = row.Template;
            Vector3 center = template.ProxyBoundsCenter;
            Vector3 size = template.ProxyBoundsSize;

            if (!EvaluateBoundsUsable(size))
            {
                Debug.LogError(
                    $"{LogPrefix} DECLINED '{row.AssetPath}': proxyBoundsSize {FormatVector(size)} is degenerate, " +
                    $"so the face is undefined. BaseModuleTemplate.cs:239-240 would overwrite it with a 4 m cube " +
                    "on the next import. Fix the bounds first. Nothing written.");
                return ApplyOutcome.Declined;
            }

            BaseModuleTemplate.SocketDefinition[] definitions = template.SocketDefinitions;
            int definitionCount = definitions != null ? definitions.Length : 0;
            if (definitionCount == 0)
                return ApplyOutcome.AlreadyAligned;

            float3[] snapPoints = template.SnapPoints;
            int snapCount = snapPoints != null ? snapPoints.Length : 0;
            if (snapCount != definitionCount)
            {
                Debug.LogError(
                    $"{LogPrefix} DECLINED '{row.AssetPath}': snapPoints holds {snapCount} entries against " +
                    $"{definitionCount} socketDefinitions, so which legacy point pairs with which socket is not " +
                    "provable. Writing the sockets alone would leave the two arrays disagreeing, and " +
                    "PrefabAssemblerEngine.cs:898-914 falls back to snapPoints whenever socketDefinitions is " +
                    "empty. Reconcile the arrays by hand first. Nothing written.");
                return ApplyOutcome.Declined;
            }

            if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(template, out string guidBefore, out long localIdBefore))
            {
                Debug.LogError(
                    $"{LogPrefix} DECLINED '{row.AssetPath}': could not read the asset guid and local file " +
                    "identifier before writing, so the recipe bindings that point at this template could not be " +
                    "protected. Nothing written.");
                return ApplyOutcome.Declined;
            }

            int hashBefore = template.ResolvePersistentHashId();

            // Pre-flight the persisted identity rather than discovering a stale hash in
            // the post-write check, which would leave a committed write beside a reported
            // failure. OnValidate rewrites templateHashId from stableId on every apply
            // (BaseModuleTemplate.cs:242), so a template whose stored hash already
            // disagrees with LocHash(stableId) will have its identity MOVED by this
            // tool's save even though this tool never touches either field. That is a
            // pre-existing defect ContentSanityValidator.cs:2866-2873 already reports;
            // it is not this tool's to fix, and it is not this tool's to trigger either.
            int canonicalHash = string.IsNullOrWhiteSpace(template.PersistentId)
                ? 0
                : LocHash.Compute(template.PersistentId);
            if (hashBefore != canonicalHash)
            {
                Debug.LogError(
                    $"{LogPrefix} DECLINED '{row.AssetPath}': stored templateHashId {hashBefore} already disagrees " +
                    $"with LocHash over PersistentId '{template.PersistentId}' ({canonicalHash}). Saving this asset " +
                    "for any reason makes OnValidate (BaseModuleTemplate.cs:242) rewrite the hash, which moves a " +
                    "persisted module identity - BuildableData.ModuleHashId returns it (BuildableData.cs:213-225). " +
                    "Repair that identity first (ContentSanityValidator.cs:2866-2873 reports it). Nothing written.");
                return ApplyOutcome.Declined;
            }

            SerializedObject templateObject = new SerializedObject(template);
            SerializedProperty definitionsProperty = templateObject.FindProperty(SocketDefinitionsField);
            SerializedProperty snapProperty = templateObject.FindProperty(SnapPointsField);
            SerializedProperty centerProperty = templateObject.FindProperty(ProxyBoundsCenterField);
            SerializedProperty sizeProperty = templateObject.FindProperty(ProxyBoundsSizeField);

            if (definitionsProperty == null || snapProperty == null || centerProperty == null || sizeProperty == null)
            {
                Debug.LogError(
                    $"{LogPrefix} DECLINED '{row.AssetPath}': one of the serialized fields " +
                    $"'{SocketDefinitionsField}', '{SnapPointsField}', '{ProxyBoundsCenterField}', " +
                    $"'{ProxyBoundsSizeField}' was not found. They were renamed in BaseModuleTemplate.cs " +
                    "(:80-90) and this tool is stale. Nothing written.");
                return ApplyOutcome.Declined;
            }

            if (definitionsProperty.arraySize != definitionCount || snapProperty.arraySize != definitionCount)
            {
                Debug.LogError(
                    $"{LogPrefix} DECLINED '{row.AssetPath}': serialized array sizes " +
                    $"(socketDefinitions {definitionsProperty.arraySize}, snapPoints {snapProperty.arraySize}) " +
                    $"disagree with the loaded arrays ({definitionCount}). The asset is mid-import. Nothing written.");
                return ApplyOutcome.Declined;
            }

            bool changed = false;
            bool anyDecline = false;

            for (int s = 0; s < definitionCount; s++)
            {
                SocketFaceEvaluation evaluation = EvaluateSocket(definitions[s], s, center, size, true);

                if (evaluation.NormalAxis < 0)
                {
                    anyDecline = true;
                    Debug.LogError(
                        $"{LogPrefix} SOCKET DECLINED '{row.AssetPath}'[socket {s}]: direction " +
                        $"{evaluation.Direction} has no single-axis normal, so it has no face to sit on. " +
                        "Fix the direction enum before the position.");
                    continue;
                }

                if (evaluation.OnFace)
                    continue;

                if (evaluation.TangentialDeviation > FaceToleranceMeters)
                {
                    anyDecline = true;
                    Debug.LogError(
                        $"{LogPrefix} SOCKET DECLINED '{row.AssetPath}'[socket {s}]: it is off its face on the " +
                        $"normal axis AND {evaluation.TangentialDeviation:0.####} m off centre on the two " +
                        "tangential axes. Only the normal axis is derivable from the bounds; the tangential " +
                        "offset could be a deliberately offset hatch. Author the tangential position, then " +
                        "re-run. Nothing written for this socket.");
                    continue;
                }

                Vector3 authored = definitions[s].LocalPosition;
                Vector3 corrected = authored;
                corrected[evaluation.NormalAxis] = evaluation.ExpectedOnAxis;

                if (!IsFinite(corrected))
                {
                    anyDecline = true;
                    Debug.LogError(
                        $"{LogPrefix} SOCKET DECLINED '{row.AssetPath}'[socket {s}]: the corrected position " +
                        $"{FormatVector(corrected)} is not finite, which means proxyBoundsCenter or " +
                        "proxyBoundsSize carries NaN or infinity. Fix the bounds first.");
                    continue;
                }

                SerializedProperty element = definitionsProperty.GetArrayElementAtIndex(s);
                SerializedProperty localPosition = element != null
                    ? element.FindPropertyRelative(SocketLocalPositionField)
                    : null;
                if (localPosition == null)
                {
                    anyDecline = true;
                    Debug.LogError(
                        $"{LogPrefix} SOCKET DECLINED '{row.AssetPath}'[socket {s}]: serialized field " +
                        $"'{SocketLocalPositionField}' not found on the socket element (BaseModuleTemplate.cs:50).");
                    continue;
                }

                // socketDefinitions[i].localPosition is a real UnityEngine.Vector3
                // (BaseModuleTemplate.cs:50), so vector3Value is valid here - the same
                // write ModuleArchitect1712.WriteSocketDefinitions (:854-865) performs.
                localPosition.vector3Value = corrected;

                // snapPoints is float3[] (BaseModuleTemplate.cs:80). SerializedProperty
                // has no vector3Value for Unity.Mathematics types, so the three float
                // components are written individually - the pattern proven by
                // ModuleArchitect1712.WriteSnapPoints (:867-877) and
                // AbandonedHabitatModuleAuthoring.WriteFloat3 (:182).
                SerializedProperty snapElement = snapProperty.GetArrayElementAtIndex(s);
                SerializedProperty snapX = snapElement != null ? snapElement.FindPropertyRelative("x") : null;
                SerializedProperty snapY = snapElement != null ? snapElement.FindPropertyRelative("y") : null;
                SerializedProperty snapZ = snapElement != null ? snapElement.FindPropertyRelative("z") : null;
                if (snapX == null || snapY == null || snapZ == null)
                {
                    anyDecline = true;
                    Debug.LogError(
                        $"{LogPrefix} SOCKET DECLINED '{row.AssetPath}'[socket {s}]: snapPoints element has no " +
                        "x/y/z float components, so the legacy array cannot be kept in step. Nothing written for " +
                        "this socket; the socketDefinitions write is abandoned with it.");
                    continue;
                }

                snapX.floatValue = corrected.x;
                snapY.floatValue = corrected.y;
                snapZ.floatValue = corrected.z;

                Debug.Log(
                    $"{LogPrefix} BEFORE/AFTER '{row.AssetPath}'[socket {s}] direction={evaluation.Direction}: " +
                    $"localPosition {FormatVector(authored)} -> {FormatVector(corrected)} " +
                    $"(face {AxisName(evaluation.NormalAxis)}={evaluation.ExpectedOnAxis:0.####} from center " +
                    $"{FormatVector(center)} and size {FormatVector(size)}); snapPoints[{s}] moved with it. " +
                    "direction, compatibleType, proxyBoundsCenter and proxyBoundsSize untouched.");

                changed = true;
                socketsMoved++;
            }

            if (anyDecline && !changed)
                return ApplyOutcome.Declined;

            if (!changed)
                return ApplyOutcome.AlreadyAligned;

            templateObject.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(template);
            AssetDatabase.SaveAssets();

            // Re-read through the AssetDatabase rather than trusting the in-memory
            // object: OnValidate runs on apply (BaseModuleTemplate.cs:212-243) and is
            // the one thing that could silently overwrite what was just written.
            BaseModuleTemplate reloaded = AssetDatabase.LoadAssetAtPath<BaseModuleTemplate>(row.AssetPath);
            if (reloaded == null)
            {
                Debug.LogError(
                    $"{LogPrefix} POST-WRITE CHECK FAILED '{row.AssetPath}': the template no longer loads. " +
                    "Restore it before trusting any recipe binding.");
                return ApplyOutcome.Declined;
            }

            if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(reloaded, out string guidAfter, out long localIdAfter))
            {
                Debug.LogError(
                    $"{LogPrefix} POST-WRITE CHECK FAILED '{row.AssetPath}': asset guid and local file identifier " +
                    "unreadable after the write. Verify the recipe bindings by hand.");
                return ApplyOutcome.Declined;
            }

            int hashAfter = reloaded.ResolvePersistentHashId();
            bool identityStable = localIdAfter == localIdBefore &&
                                  string.Equals(guidAfter, guidBefore, System.StringComparison.Ordinal) &&
                                  hashAfter == hashBefore;

            if (!identityStable)
            {
                Debug.LogError(
                    $"{LogPrefix} IDENTITY MOVED '{row.AssetPath}': guid {guidBefore} -> {guidAfter}, " +
                    $"localFileId {localIdBefore} -> {localIdAfter}, templateHashId {hashBefore} -> {hashAfter}. " +
                    "Every BuildableData.moduleTemplate reference binds (guid, localFileId) and " +
                    "BuildableData.ModuleHashId returns this template's hash (BuildableData.cs:213-225), so a " +
                    "moved identity breaks the recipe binding and fuses or orphans save identities. Revert this " +
                    "asset.");
                return ApplyOutcome.Declined;
            }

            Vector3 centerAfter = reloaded.ProxyBoundsCenter;
            Vector3 sizeAfter = reloaded.ProxyBoundsSize;
            if (!Approximately(centerAfter, center) || !Approximately(sizeAfter, size))
            {
                Debug.LogError(
                    $"{LogPrefix} BOUNDS MOVED '{row.AssetPath}': center {FormatVector(center)} -> " +
                    $"{FormatVector(centerAfter)}, size {FormatVector(size)} -> {FormatVector(sizeAfter)}. " +
                    "This tool never writes either field, so OnValidate " +
                    "(BaseModuleTemplate.cs:239-240) re-derived them. The sockets are now aligned to bounds that " +
                    "changed under them. Revert this asset.");
                return ApplyOutcome.Declined;
            }

            Debug.Log(
                $"{LogPrefix} WROTE '{row.AssetPath}': {socketsMoved} socket(s) aligned. IDENTITY STABLE - " +
                $"guid={guidAfter}, localFileId={localIdAfter}, templateHashId={hashAfter}, all unchanged. " +
                $"BOUNDS STABLE - center={FormatVector(centerAfter)}, size={FormatVector(sizeAfter)}.");
            return ApplyOutcome.Wrote;
        }

        // ══════════════════════════════════════════════════════════
        //  PRIVATE — EVALUATION
        // ══════════════════════════════════════════════════════════

        private static void CollectTemplates(List<TemplateRow> rows)
        {
            // COLD ALLOC: string[] from AssetDatabase.FindAssets - one-shot editor template scan - owner: ModuleTemplateSocketBoundsFaceGate
            string[] guids = AssetDatabase.FindAssets("t:BaseModuleTemplate");
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                BaseModuleTemplate template = AssetDatabase.LoadAssetAtPath<BaseModuleTemplate>(path);
                if (template == null)
                    continue;

                bool writable = path.StartsWith(ProductionTemplateFolder, System.StringComparison.Ordinal);
                rows.Add(new TemplateRow(path, template, writable));
            }

            rows.Sort(CompareByPath);
        }

        private static int CompareByPath(TemplateRow lhs, TemplateRow rhs)
        {
            return string.Compare(lhs.AssetPath, rhs.AssetPath, System.StringComparison.Ordinal);
        }

        /// <summary>
        /// Resolves the socket's normal axis through the public owner
        /// <c>BaseModuleCatalogRuntime.DirectionToNormal</c> (:955-967) rather than
        /// reimplementing the direction table. ModuleSocketTopology is internal to
        /// Hecton8.Core and invisible here, and a private copy of a six-case switch
        /// is exactly how two comparators end up disagreeing.
        /// </summary>
        private static SocketFaceEvaluation EvaluateSocket(
            BaseModuleTemplate.SocketDefinition definition,
            int socketIndex,
            Vector3 center,
            Vector3 size,
            bool boundsUsable)
        {
            float3 normal = BaseModuleCatalogRuntime.DirectionToNormal(definition.Direction);
            int axis = ResolveSingleAxis(normal, out float sign);
            Vector3 authored = definition.LocalPosition;

            if (axis < 0 || !boundsUsable)
            {
                return new SocketFaceEvaluation(
                    socketIndex,
                    definition.Direction,
                    axis,
                    axis >= 0 ? authored[axis] : 0f,
                    0f,
                    0f,
                    false);
            }

            float expected = center[axis] + (sign * size[axis] * 0.5f);

            float tangential = 0f;
            for (int a = 0; a < 3; a++)
            {
                if (a == axis)
                    continue;

                tangential = Mathf.Max(tangential, Mathf.Abs(authored[a] - center[a]));
            }

            return new SocketFaceEvaluation(
                socketIndex,
                definition.Direction,
                axis,
                authored[axis],
                expected,
                tangential,
                true);
        }

        /// <summary>
        /// True when exactly one component of the normal is +/-1 and the other two
        /// are zero. Anything else - a zero normal, a diagonal, a NaN - returns -1
        /// rather than picking the largest component, because a socket whose face
        /// was guessed from a malformed normal would report as aligned while
        /// pointing nowhere. That is the silent-degeneracy shape this gate exists
        /// to catch, so it fails loudly instead.
        /// </summary>
        private static int ResolveSingleAxis(float3 normal, out float sign)
        {
            sign = 0f;
            if (!math.all(math.isfinite(normal)))
                return -1;

            int axis = -1;
            for (int a = 0; a < 3; a++)
            {
                float component = normal[a];
                if (Mathf.Abs(component) <= NormalComponentEpsilon)
                    continue;

                if (axis >= 0)
                    return -1;

                if (!Mathf.Approximately(Mathf.Abs(component), 1f))
                    return -1;

                axis = a;
                sign = component > 0f ? 1f : -1f;
            }

            return axis;
        }

        private static bool EvaluateBoundsUsable(Vector3 size)
        {
            return IsFinite(size) &&
                   size.x > MinimumBoundsAxisMeters &&
                   size.y > MinimumBoundsAxisMeters &&
                   size.z > MinimumBoundsAxisMeters;
        }

        // ══════════════════════════════════════════════════════════
        //  PRIVATE — TEMPLATE VS PREFAB SOCKET TOPOLOGY
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Reports every recipe whose template socket set and whose bound prefab's
        /// authored <see cref="ModuleSocket"/> children describe different modules.
        /// Nothing in the project checks this today:
        /// ConstructionCatalogRepairAuthoring.ReportProxyBoundsAgreement (:1741)
        /// compares bounds SIZE against render bounds and never looks at sockets,
        /// and ModuleSocketLaneVocabularyGate collects both socket sets but only
        /// compares their lane STRINGS (:274-300).
        /// Warning, not failure: which side is authoritative is a design call. The
        /// template wins at placement - BaseModule.ApplyBuildableTemplate
        /// (BaseModule.cs:4802-4816) stamps the recipe's template over the prefab's
        /// on both ConstructionManager.cs:825 and :2873 - but the ModuleSocket
        /// children are what a level designer sees and what
        /// DeepReachStationModuleLibrary.cs:490 reads.
        /// </summary>
        private static int ReportPrefabSocketTopologyDivergence(StringBuilder report)
        {
            // COLD ALLOC: string[] from AssetDatabase.FindAssets - one-shot editor recipe scan - owner: ModuleTemplateSocketBoundsFaceGate
            string[] recipeGuids = AssetDatabase.FindAssets("t:BuildableData", new[] { ConstructionDataFolder });
            int warnings = 0;

            for (int i = 0; i < recipeGuids.Length; i++)
            {
                string recipePath = AssetDatabase.GUIDToAssetPath(recipeGuids[i]);
                BuildableData recipe = AssetDatabase.LoadAssetAtPath<BuildableData>(recipePath);
                if (recipe == null || recipe.ModuleTemplate == null || recipe.finalPrefab == null)
                    continue;

                BaseModuleTemplate template = recipe.ModuleTemplate;
                BaseModuleTemplate.SocketDefinition[] definitions = template.SocketDefinitions;
                int templateSockets = definitions != null ? definitions.Length : 0;

                ModuleSocket[] prefabSockets = recipe.finalPrefab.GetComponentsInChildren<ModuleSocket>(true);
                int prefabSocketCount = prefabSockets != null ? prefabSockets.Length : 0;

                if (templateSockets == prefabSocketCount && templateSockets == 0)
                    continue;

                bool countDiverges = templateSockets != prefabSocketCount;
                bool directionsCollapsed = prefabSocketCount > 1 && AllSameDirection(prefabSockets);

                if (!countDiverges && !directionsCollapsed)
                    continue;

                warnings++;
                report.AppendLine(
                    $"  WARN {recipe.name}: template '{template.name}' authors {templateSockets} socket(s) while " +
                    $"its bound prefab '{recipe.finalPrefab.name}' carries {prefabSocketCount} ModuleSocket " +
                    $"child(ren){(directionsCollapsed ? ", every one of them on the SAME direction enum value" : string.Empty)}. " +
                    "The two socket sets describe different modules. The template wins at placement " +
                    "(BaseModule.ApplyBuildableTemplate, BaseModule.cs:4802-4816, from ConstructionManager.cs:825 " +
                    "and :2873), so the prefab's own sockets are decoration the snapper never consults through " +
                    "this path - but they are what a level designer places against and what " +
                    "DeepReachStationModuleLibrary.cs:490 reads. Deciding which side is the real module is a " +
                    "construction.md design call and is NOT derivable here, so nothing is written for it.");
            }

            return warnings;
        }

        private static bool AllSameDirection(ModuleSocket[] sockets)
        {
            ModuleSocketDirection first = ModuleSocketDirection.North;
            bool initialized = false;

            for (int i = 0; i < sockets.Length; i++)
            {
                if (sockets[i] == null)
                    continue;

                if (!initialized)
                {
                    first = sockets[i].Direction;
                    initialized = true;
                    continue;
                }

                if (sockets[i].Direction != first)
                    return false;
            }

            return initialized;
        }

        // ══════════════════════════════════════════════════════════
        //  PRIVATE — FORMATTING
        // ══════════════════════════════════════════════════════════

        private static bool IsFinite(Vector3 value)
        {
            return float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z);
        }

        private static bool Approximately(Vector3 lhs, Vector3 rhs)
        {
            return Mathf.Abs(lhs.x - rhs.x) <= FaceToleranceMeters &&
                   Mathf.Abs(lhs.y - rhs.y) <= FaceToleranceMeters &&
                   Mathf.Abs(lhs.z - rhs.z) <= FaceToleranceMeters;
        }

        private static string AxisName(int axis)
        {
            switch (axis)
            {
                case 0: return "x";
                case 1: return "y";
                case 2: return "z";
                default: return "<no axis>";
            }
        }

        private static string FormatVector(Vector3 value)
        {
            return $"({value.x:0.####}, {value.y:0.####}, {value.z:0.####})";
        }
    }
}
#endif
