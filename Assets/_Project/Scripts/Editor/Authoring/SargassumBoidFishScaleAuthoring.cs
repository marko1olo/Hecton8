// ============================================================================================
//  SargassumBoidFishScaleAuthoring
//
//  ONE RULING, ENCODED WITH ITS DERIVATION AND ITS GATE:
//      MAT_SargassumMicroFaunaBoids.mat  _FishScale  0.28 -> 0.18
//
//  0.18 = Ocean_Crest boidBodyRadius, a RADIUS, against a 2-unit centred mesh whose scaled
//  half-length equals this value. It is a computed constant, not a taste number, and both halves
//  of that sentence are verified below rather than asserted.
//
//  WHY THIS IS A SEPARATE FILE FROM ForgeGeneratedMaterialAuthoring.cs. That class binds offline
//  Blender FORGE PACKAGE materials: it is driven by MANIFEST_*.json, by law.py naming templates,
//  and by the four-channel vertex-colour contract, and it writes into
//  Art/Materials/Generated/Forge. This is none of those things - it is a single scalar on one
//  runtime fauna-swarm material that a MonoBehaviour on Ocean_Crest.prefab renders through
//  BoidFishInstanced.shader. It shares no shader, no naming template, no manifest and no folder
//  with the forge binder, and folding it in would mean a verb named "Forge Materials" mutates an
//  unrelated asset. Separate verb, separate file.
//
//  ════════════════════════════════════════════════════════════════════════════════════════════
//  PREMISE 1 - "boidBodyRadius is a RADIUS, not a diameter".  VERIFIED.
//  ════════════════════════════════════════════════════════════════════════════════════════════
//  Ocean_Crest.prefab:644 authors `boidBodyRadius: 0.18`, and SargassumMicroFaunaBoids.cs:1132
//  declares the same 0.18 as the C# default. Radius semantics are established at five sites, and
//  the decisive one is NOT in the simulation C# at all:
//
//    * SargassumMicroFaunaBoids.compute:1525-1526, inside the PBD contact solver:
//          float minDistance = max(_BoidBodyRadius * 2.0, 0.001);
//          float minDistanceSq = minDistance * minDistance;
//      The minimum permitted CENTRE-TO-CENTRE distance between two boids is 2 * r. Two spheres of
//      radius r touch exactly when their centres are 2r apart, so this line IS the definition of a
//      radius. Were the field a diameter the constraint would read `* 1.0`. This is stronger than
//      the four C# sites because it is a physical constraint rather than a scale factor.
//    * SargassumMicroFaunaBoids.cs:2742 clamps it to `separationRadius * 0.5f` - a body radius
//      cannot exceed half the separation distance.
//    * SargassumMicroFaunaBoids.cs:5972 sizes the neighbour cell range from `boidBodyRadius * 2f`,
//      i.e. the body's full extent is 2r.
//    * SargassumMicroFaunaBoids.cs:6532 and :6620 consume it as an InnerRadius (`* 2f`, `* 1.5f`).
//
//  So the simulation models a body 0.36 m across. CORROBORATION: Ocean_Crest.prefab:643 authors
//  `separationRadius: 0.85`. At the superseded 0.28 the drawn bodies are 0.56 m and nearly touch
//  at the separation distance; at 0.36 m a visible gap survives.
//
//  ════════════════════════════════════════════════════════════════════════════════════════════
//  PREMISE 2 - "the fish mesh is exactly 2 units long and CENTRED".  TRUE OF THE FORGE FISH,
//  FALSE OF THE MESH CURRENTLY BOUND. This is why Apply is gated instead of unconditional.
//  ════════════════════════════════════════════════════════════════════════════════════════════
//  MEASURED in Assets/_Project/Art/Generated/Forge/Fauna/MANIFEST_Fauna_Fish_2207_00.json,
//  MESH_Fauna_Fish_2207_00_LOD0:
//      boundsMin.z = -1.0        boundsMax.z = +1.0        -> exactly 2 units, exactly centred
//      boundsMin.x = -0.2079     boundsMax.x = +0.2134
//      boundsMin.y = -0.4103     boundsMax.y = +0.3667     identity.scaleMeters = 2.0
//  Exact to the serialised digit. For THAT mesh the scaled half-length is numerically _FishScale,
//  the ruling's arithmetic holds, and 0.18 is right.
//
//  BUT Ocean_Crest.prefab:615 does not bind it:
//      boidMesh: {fileID: 10209, guid: 0000000000000000e000000000000000, type: 0}
//  That GUID is Unity's built-in default-resources library, so the bound asset is a BUILT-IN
//  PRIMITIVE, not the forge fish - a different GUID entirely, not a stale import. The same
//  fileID 10209 also sits on three ordinary MeshFilters in the same prefab (:44, :151, :269).
//
//  fileID 10209 is the built-in PLANE, and that is established from this repository rather than
//  from engine folklore - three independent first-party records say so:
//      Docs/Reports/AssetSystem_20260605/PRIMITIVE_NULL_DEFAULT_20260605.csv:986
//          10209,...,Unity built-in resource/Plane
//      Docs/Tasks/Status_1883.md:25   "micro-fauna boidMesh remains built-in plane 10209"
//      FaunaSwarmVatPrefabBinder.cs:54-58 already flags this exact prefab line as a built-in
//          primitive.
//  A built-in Plane is a flat 10x10 sheet in XZ with no thickness. So today `_FishScale` 0.28
//  draws a 2.8 m flat card, not a 0.56 m fish, and `saturate(-localPos.z)` saturates across the
//  entire aft half of it. Against that, 0.18 is not a 56 percent correction - it is a 36 percent
//  shrink of a 10-unit card that was never the calibration subject, and it would leave a number
//  that LOOKS derived sitting on an asset the derivation does not describe.
//
//  So ApplyBoidFishScale MEASURES the bound mesh and refuses unless it really is ~2 units and
//  centred on Z. The gate clears itself the moment a forge fish LOD is bound; nothing in this
//  file needs editing then.
//
//  ONE MORE LINK IN THAT CHAIN, so the refusal is not read as "just bind the fish and re-run":
//  MESH_Fauna_Fish_2207_00.fbx.meta currently carries stock importer defaults, not the contract
//  its own manifest mandates - `weldVertices: 1` (:56) where the manifest requires false,
//  `meshOptimizationFlags: -1` (:68) where it requires PolygonOrder, `preserveHierarchy: 0`
//  (:58) and `sortHierarchyByName: 1` (:44) both inverted. Welding can move the imported vertex
//  count off the authored 278, which is the gate FaunaSwarmVatPrefabBinder refuses on. Binding
//  the fish therefore needs the forge import contract applied first. Neither this file's business
//  nor this ruling's, but it is the reason the mesh gate may keep failing after a naive bind.
//
//  ════════════════════════════════════════════════════════════════════════════════════════════
//  PREMISE 3 - "does not invalidate a VAT".  VERIFIED, and the ordering is the reason.
//  ════════════════════════════════════════════════════════════════════════════════════════════
//  BoidFishInstanced.shader:504   localPos += (vatPosition - localPos) * aggressiveAmplitudeScale;
//  BoidFishInstanced.shader:517   float tailFactor = saturate(-localPos.z);
//  BoidFishInstanced.shader:561   localPos *= _FishScale * scaleVariation * aupScaleJitter * ...;
//  BoidFishInstanced.shader:847   float3 localPos = input.positionOS.xyz * _FishScale * ...;
//  Both the VAT displacement and the tail wave act on UNSCALED object space, and _FishScale
//  multiplies afterwards, so pose and displacement scale together and proportions survive. It
//  also means tailFactor reaches 1.0 only at z = -1, which is exactly why the mesh must stay
//  2 units: a 1-unit centred fish would cap the tail at half amplitude. Re-authoring the mesh
//  shorter instead of changing this scalar is therefore the wrong fix, and it would additionally
//  hit the bake's `amplitudeMeters = max(0.01, length * 0.035)` floor below 0.2857 units.
//
//  ════════════════════════════════════════════════════════════════════════════════════════════
//  PREMISE 4 - "safe to change, _FishScale is never written from C#".  TRUE HERE, WITH A SCOPE
//  CORRECTION THAT MATTERS.
//  ════════════════════════════════════════════════════════════════════════════════════════════
//  For THIS material it holds. Ocean_Crest.prefab:616 assigns it to the `boidMaterial` field of
//  SargassumMicroFaunaBoids (field declared at SargassumMicroFaunaBoids.cs:1006), that class
//  contains ZERO `_FishScale` references, and it takes the material BY REFERENCE without cloning
//  or mutating it (:2143-2155, `_boidRenderMaterialSource = source`). A project-wide GUID search
//  for a3e4cd9f5f99492cc5705fb11b348966 finds exactly one consumer, Ocean_Crest.prefab. So the
//  serialised .mat value is the live value on this path.
//
//  THE CORRECTION: a SECOND boid renderer exists on the SAME shader and it does override the
//  property. HectonBoidController.cs:1740 writes
//      _renderMaterialProperties.SetFloat(ShaderProps.FishScale, ClampFinite(fishScale, ...))
//  into a MaterialPropertyBlock bound through RenderParams.matProps (:1152-1158), sourced from
//  `[SerializeField] private float fishScale = 0.3f` (:274). An MPB beats the material, so on that
//  path the .mat value is dead. It uses a DIFFERENT field - `fishMaterial` (:182), not
//  `boidMaterial` - and no text reference to this material's GUID reaches it, so this edit is
//  still correct and sufficient for the Sargassum swarm. But if the same radius reasoning applies
//  to that controller, its 0.3 is a SECOND change, in C#/prefab rather than in a .mat, and it is
//  outside this ruling's scope. Flagged, not silently fixed.
//
//  ════════════════════════════════════════════════════════════════════════════════════════════
//  WHAT NO STATIC ANALYSIS CAN SETTLE - THE BINARY SCENE, MEASURED RATHER THAN ASSERTED
//  ════════════════════════════════════════════════════════════════════════════════════════════
//  Every number above is the PREFAB-ASSET value. Assets/_Project/Scenes/02_HECTON_WORLD.unity is
//  serialised BINARY (6 270 260 bytes, no `%YAML` header), and a per-instance override of
//  boidBodyRadius, boidMaterial or boidMesh lives in that scene's modification list where no text
//  search can see it. Quantified so the limitation is not merely claimed:
//      grep -acE '[0-9a-f]{32}'  02_HECTON_WORLD.unity   ->    0
//      grep -acE '[0-9a-f]{32}'  00_BOOTSTRAP.unity      ->  146   (text scene, control)
//  ZERO ASCII GUIDs in 6.27 MB. So "the override is not in the scene" is not a finding, it is the
//  absence of a finding, and only an editor can distinguish them. If the scene overrides the
//  radius, 0.18 is calibrated against the wrong figure.
//  Related nuance worth keeping: binary is not uniformly opaque. 020_RENDER_SANDBOX.unity is also
//  binary but keeps an ASCII "External References" table (24 GUIDs), which is how Ocean_Crest's
//  own GUID 0a7f97b6028cb014e80782578e9bf734 is provably referenced from THAT scene. 02_HECTON_
//  WORLD exposes no such table, so it yields nothing either way.
//
//  ════════════════════════════════════════════════════════════════════════════════════════════
//  Every entry point is idempotent, ASCII-tagged, and reachable by -executeMethod. Build output
//  on this host is localised to Russian, so no verdict here relies on the words error/warning.
// ============================================================================================
#if UNITY_EDITOR
using System;
using System.Globalization;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Hecton8.Editor.Authoring
{
    /// <summary>
    /// Sets <c>_FishScale</c> on <c>MAT_SargassumMicroFaunaBoids.mat</c> to the value derived from
    /// <c>Ocean_Crest</c>'s authored <c>boidBodyRadius</c>, and refuses to do so while the bound
    /// boid mesh is not the 2-unit Z-centred mesh the derivation assumes.
    /// </summary>
    public static class SargassumBoidFishScaleAuthoring
    {
        // ══════════════════════════════════════════════════════════
        //  ASCII REPORT TOKENS
        // ══════════════════════════════════════════════════════════

        private const string LogPrefix = "H8BOIDSCALE";
        private const string TokenPass = "PASS";
        private const string TokenFail = "FAIL";
        private const string TokenRefused = "REFUSED";

        // ══════════════════════════════════════════════════════════
        //  SUBJECTS
        // ══════════════════════════════════════════════════════════

        private const string MaterialPath =
            "Assets/_Project/Art/Materials/MAT_SargassumMicroFaunaBoids.mat";

        /// <summary>
        /// The single consumer. A project-wide search for the material GUID
        /// a3e4cd9f5f99492cc5705fb11b348966 returns this prefab and nothing else that renders it.
        /// </summary>
        private const string ConsumerPrefabPath = "Assets/_Project/Prefabs/Ocean_Crest.prefab";

        /// <summary>
        /// The mesh the derivation assumes, measured at boundsMin.z -1.0 / boundsMax.z +1.0 in
        /// MANIFEST_Fauna_Fish_2207_00.json. Named in the refusal so the fix is actionable.
        /// </summary>
        private const string ExpectedMeshAssetPath =
            "Assets/_Project/Art/Generated/Forge/Fauna/MESH_Fauna_Fish_2207_00.fbx";

        private const string FishScaleProperty = "_FishScale";

        // Serialized field names on the consuming MonoBehaviour. Probed through SerializedObject
        // rather than by referencing the type: SargassumMicroFaunaBoids lives in a runtime
        // assembly that Hecton8.Editor does not reference, and adding an asmdef reference to read
        // one float would widen this assembly's dependency graph for no benefit. SerializedObject
        // reaches private [SerializeField] members by name and mutates nothing on read.
        private const string FieldBoidMesh = "boidMesh";
        private const string FieldBoidMaterial = "boidMaterial";
        private const string FieldBoidBodyRadius = "boidBodyRadius";
        private const string FieldSeparationRadius = "separationRadius";

        // ══════════════════════════════════════════════════════════
        //  THE RULING
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Ocean_Crest.prefab:644 <c>boidBodyRadius</c>, verified as a radius by the PBD contact
        /// constraint at SargassumMicroFaunaBoids.compute:1525. Against a mesh whose local Z spans
        /// -1..+1, the drawn half-length is numerically this value, so the drawn body is
        /// 2 * 0.18 = 0.36 m across and matches the simulated body exactly.
        /// This is the authored prefab value, NOT a taste number, and the verify step re-reads it
        /// from the prefab on every run so a change there shows up as a mismatch instead of
        /// silently diverging from this constant.
        /// </summary>
        private const float TargetFishScale = 0.18f;

        /// <summary>
        /// The superseded value. Retained so the report can state what changed and why, per the
        /// no-loss convention: 0.28 drew a 0.56 m body against a 0.36 m simulated one, a 56 percent
        /// over-draw, and at separationRadius 0.85 the bodies nearly touched.
        /// </summary>
        private const float SupersededFishScale = 0.28f;

        /// <summary>Local-space Z extent the derivation requires: -1..+1.</summary>
        private const float RequiredMeshZExtent = 2.0f;

        /// <summary>
        /// Tolerance on the measured Z extent. Set from the MEASURED spread across the three forge
        /// LODs, not picked: LOD0 spans exactly 2.0, LOD1 spans 1.98230, LOD2 spans 1.97262
        /// (MANIFEST_Fauna_Fish_2207_00.json). LOD2's deviation is 0.0274, so a 0.02 tolerance would
        /// wrongly refuse a legitimately decimated level; 0.05 admits all three and still cannot
        /// admit a 1-unit Cube/Quad or the 10-unit built-in Plane. The generator enforces the same
        /// contract by ABORT at fauna_fish.py:1512-1515 with a +/-1.02 band per end, so this gate
        /// and the generator's gate agree.
        /// </summary>
        private const float MeshExtentTolerance = 0.05f;

        /// <summary>
        /// Tolerance on the measured Z centre. Tighter than the extent because the measured drift is
        /// smaller: LOD0 centre 0.0, LOD1 +0.00952, LOD2 +0.01511.
        /// </summary>
        private const float MeshCentreTolerance = 0.02f;

        // ══════════════════════════════════════════════════════════
        //  ENTRY POINTS - Verify / Apply / Verify
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Step 1 of 3. Read-only. Re-derives the ruling from the live prefab and the live bound
        /// mesh, and reports whether the premise holds. Writes nothing, refuses nothing.
        /// </summary>
        [MenuItem("Hecton8/Authoring/Sargassum Boids/1 - Verify Fish Scale Premise", priority = 240)]
        public static void VerifyBoidFishScalePremise()
        {
            StringBuilder report = NewReport("VERIFY-PREMISE");
            Findings findings = Measure(report);
            AppendScopeNotes(report);

            report.Append(LogPrefix).Append(" RESULT ")
                  .Append(findings.PremiseHolds ? TokenPass : TokenFail)
                  .Append(" phase=VERIFY-PREMISE premiseHolds=").Append(findings.PremiseHolds ? 1 : 0)
                  .Append(" meshGate=").Append(findings.MeshGateVerdict)
                  .Append(" authoredRadius=").Append(F(findings.AuthoredBodyRadius))
                  .Append(" derivedFishScale=").Append(F(findings.DerivedFishScale))
                  .Append(" currentFishScale=").Append(F(findings.CurrentFishScale))
                  .Append(" targetFishScale=").Append(F(TargetFishScale));
            Emit(report, findings.PremiseHolds);
        }

        /// <summary>
        /// Step 2 of 3. Writes <c>_FishScale</c> only when the premise holds. When the bound mesh is
        /// not 2 units and Z-centred it REFUSES and prints the exact reason and the exact fix,
        /// because against any other mesh the constant is not a correction, it is an arbitrary
        /// shrink that would look calibrated.
        /// </summary>
        [MenuItem("Hecton8/Authoring/Sargassum Boids/2 - Apply Fish Scale", priority = 241)]
        public static void ApplyBoidFishScale()
        {
            StringBuilder report = NewReport("APPLY");
            Findings findings = Measure(report);

            if (!findings.PremiseHolds)
            {
                report.Append("  ").Append(TokenRefused)
                      .Append(" nothing written. The ruling's derivation does not hold against the ")
                      .Append("live asset state:").AppendLine();
                for (int i = 0; i < findings.BlockingReasons.Length; i++)
                    report.Append("      - ").Append(findings.BlockingReasons[i]).AppendLine();
                report.Append("      FIX: bind a 2-unit Z-centred boid mesh to the '")
                      .Append(FieldBoidMesh).Append("' field on ").Append(ConsumerPrefabPath)
                      .Append(" - ").Append(ExpectedMeshAssetPath)
                      .Append(" measures boundsMin.z -1.0 / boundsMax.z +1.0 in its manifest - ")
                      .Append("then re-run this step. No edit to this file is needed.").AppendLine();
                AppendScopeNotes(report);
                report.Append(LogPrefix).Append(" RESULT ").Append(TokenRefused)
                      .Append(" phase=APPLY written=0 meshGate=").Append(findings.MeshGateVerdict)
                      .Append(" blockingReasons=").Append(findings.BlockingReasons.Length);
                Emit(report, false);
                return;
            }

            Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
            {
                report.Append("  ").Append(TokenFail).Append(" material not found at ")
                      .Append(MaterialPath).AppendLine();
                report.Append(LogPrefix).Append(" RESULT ").Append(TokenFail)
                      .Append(" phase=APPLY written=0 reason=MATERIAL_MISSING");
                Emit(report, false);
                return;
            }

            if (!material.HasProperty(FishScaleProperty))
            {
                report.Append("  ").Append(TokenFail).Append(' ').Append(FishScaleProperty)
                      .Append(" is not declared by shader '")
                      .Append(material.shader != null ? material.shader.name : "<null>")
                      .Append("'.").AppendLine();
                report.Append(LogPrefix).Append(" RESULT ").Append(TokenFail)
                      .Append(" phase=APPLY written=0 reason=PROPERTY_NOT_DECLARED");
                Emit(report, false);
                return;
            }

            float before = material.GetFloat(FishScaleProperty);
            bool alreadyCorrect = Mathf.Abs(before - TargetFishScale) <= 0.0001f;

            if (alreadyCorrect)
            {
                report.Append("  ALREADY  ").Append(FishScaleProperty).Append(" = ").Append(F(before))
                      .Append(" - idempotent, nothing written.").AppendLine();
            }
            else
            {
                material.SetFloat(FishScaleProperty, TargetFishScale);
                EditorUtility.SetDirty(material);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                report.Append("  WRITE    ").Append(MaterialPath).Append(' ')
                      .Append(FishScaleProperty).Append(": ").Append(F(before))
                      .Append(" -> ").Append(F(TargetFishScale))
                      .Append("  (drawn body ").Append(F(before * 2f)).Append(" m -> ")
                      .Append(F(TargetFishScale * 2f)).Append(" m against ")
                      .Append(F(findings.AuthoredBodyRadius * 2f)).Append(" m simulated)")
                      .AppendLine();
            }

            AppendScopeNotes(report);
            report.Append(LogPrefix).Append(" RESULT ").Append(TokenPass)
                  .Append(" phase=APPLY written=").Append(alreadyCorrect ? 0 : 1)
                  .Append(" fishScale=").Append(F(TargetFishScale))
                  .Append(" meshGate=").Append(findings.MeshGateVerdict);
            Emit(report, true);
        }

        /// <summary>
        /// Step 3 of 3. Strict post-check. Asserts the material now carries the derived value, that
        /// the value still equals what the prefab's authored radius implies, and that the mesh gate
        /// still holds. Emits exactly one greppable RESULT line.
        /// </summary>
        [MenuItem("Hecton8/Authoring/Sargassum Boids/3 - Verify Fish Scale", priority = 242)]
        public static void VerifyBoidFishScale()
        {
            StringBuilder report = NewReport("VERIFY");
            Findings findings = Measure(report);

            bool valueCorrect = Mathf.Abs(findings.CurrentFishScale - TargetFishScale) <= 0.0001f;
            // The constant must still agree with the live prefab. If somebody re-authors
            // boidBodyRadius, this catches the divergence instead of leaving a stale literal.
            bool derivationStillAgrees =
                findings.AuthoredBodyRadiusFound &&
                Mathf.Abs(findings.DerivedFishScale - TargetFishScale) <= 0.0001f;
            bool pass = valueCorrect && derivationStillAgrees && findings.PremiseHolds;

            if (!derivationStillAgrees && findings.AuthoredBodyRadiusFound)
            {
                report.Append("  ").Append(TokenFail)
                      .Append(" DERIVATION DRIFT: the prefab now authors boidBodyRadius=")
                      .Append(F(findings.AuthoredBodyRadius)).Append(", which implies _FishScale=")
                      .Append(F(findings.DerivedFishScale)).Append(", but this file's constant is ")
                      .Append(F(TargetFishScale))
                      .Append(". The constant is stale - re-derive it, do not overwrite the prefab.")
                      .AppendLine();
            }

            AppendScopeNotes(report);
            report.Append(LogPrefix).Append(" RESULT ").Append(pass ? TokenPass : TokenFail)
                  .Append(" phase=VERIFY fishScale=").Append(F(findings.CurrentFishScale))
                  .Append(" expected=").Append(F(TargetFishScale))
                  .Append(" valueCorrect=").Append(valueCorrect ? 1 : 0)
                  .Append(" derivationAgrees=").Append(derivationStillAgrees ? 1 : 0)
                  .Append(" meshGate=").Append(findings.MeshGateVerdict)
                  .Append(" premiseHolds=").Append(findings.PremiseHolds ? 1 : 0)
                  .Append(" sceneOverrideCheck=IMPOSSIBLE-BINARY-SCENE");
            Emit(report, pass);
        }

        // ══════════════════════════════════════════════════════════
        //  MEASUREMENT
        // ══════════════════════════════════════════════════════════

        private struct Findings
        {
            public bool PremiseHolds;
            public string MeshGateVerdict;
            public bool AuthoredBodyRadiusFound;
            public float AuthoredBodyRadius;
            public float AuthoredSeparationRadius;
            public float DerivedFishScale;
            public float CurrentFishScale;
            public string[] BlockingReasons;
        }

        private static Findings Measure(StringBuilder report)
        {
            Findings findings = default;
            findings.MeshGateVerdict = "UNKNOWN";
            findings.BlockingReasons = Array.Empty<string>();
            // COLD ALLOC: string[6] scratch - one slot per possible blocking reason -
            // owner: SargassumBoidFishScaleAuthoring
            string[] blocking = new string[6];
            int blockingCount = 0;

            // ---- material ---------------------------------------------------------------
            Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
            {
                report.Append("  ").Append(TokenFail).Append(" material MISSING ").Append(MaterialPath)
                      .AppendLine();
                blocking[blockingCount++] = "material missing at " + MaterialPath;
            }
            else
            {
                findings.CurrentFishScale = material.HasProperty(FishScaleProperty)
                    ? material.GetFloat(FishScaleProperty)
                    : float.NaN;
                report.Append("  MATERIAL ").Append(MaterialPath)
                      .Append(" shader=").Append(material.shader != null ? material.shader.name : "<null>")
                      .Append(' ').Append(FishScaleProperty).Append('=').Append(F(findings.CurrentFishScale))
                      .Append(" superseded=").Append(F(SupersededFishScale))
                      .Append(" target=").Append(F(TargetFishScale))
                      .AppendLine();
            }

            // ---- consumer prefab: authored radii + bound mesh ---------------------------
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ConsumerPrefabPath);
            if (prefab == null)
            {
                report.Append("  ").Append(TokenFail).Append(" consumer prefab MISSING ")
                      .Append(ConsumerPrefabPath).AppendLine();
                blocking[blockingCount++] = "consumer prefab missing at " + ConsumerPrefabPath;
                findings.MeshGateVerdict = "NO-PREFAB";
            }
            else
            {
                Mesh boundMesh = null;
                bool componentFound = false;

                MonoBehaviour[] behaviours = prefab.GetComponentsInChildren<MonoBehaviour>(true);
                for (int i = 0; i < behaviours.Length; i++)
                {
                    if (behaviours[i] == null)
                        continue;

                    // Identify the swarm component by the SHAPE of its serialized data rather than
                    // by its type, so this file needs no assembly reference and survives a rename.
                    SerializedObject serialized = new SerializedObject(behaviours[i]);
                    SerializedProperty meshProperty = serialized.FindProperty(FieldBoidMesh);
                    SerializedProperty materialProperty = serialized.FindProperty(FieldBoidMaterial);
                    if (meshProperty == null || materialProperty == null)
                    {
                        serialized.Dispose();
                        continue;
                    }

                    componentFound = true;
                    boundMesh = meshProperty.objectReferenceValue as Mesh;
                    Material boundMaterial = materialProperty.objectReferenceValue as Material;

                    SerializedProperty radiusProperty = serialized.FindProperty(FieldBoidBodyRadius);
                    SerializedProperty separationProperty = serialized.FindProperty(FieldSeparationRadius);
                    if (radiusProperty != null)
                    {
                        findings.AuthoredBodyRadiusFound = true;
                        findings.AuthoredBodyRadius = radiusProperty.floatValue;
                    }
                    if (separationProperty != null)
                        findings.AuthoredSeparationRadius = separationProperty.floatValue;

                    report.Append("  CONSUMER ").Append(behaviours[i].GetType().Name)
                          .Append(" on '").Append(behaviours[i].gameObject.name).Append('\'')
                          .Append(' ').Append(FieldBoidBodyRadius).Append('=')
                          .Append(F(findings.AuthoredBodyRadius))
                          .Append(' ').Append(FieldSeparationRadius).Append('=')
                          .Append(F(findings.AuthoredSeparationRadius))
                          .AppendLine();

                    string boundMaterialPath = boundMaterial != null
                        ? AssetDatabase.GetAssetPath(boundMaterial)
                        : "<none>";
                    bool materialMatches = string.Equals(
                        boundMaterialPath, MaterialPath, StringComparison.Ordinal);
                    report.Append("      boundMaterial=").Append(boundMaterialPath)
                          .Append(materialMatches ? " (matches)" : " " + TokenFail + "-MATERIAL-MISMATCH")
                          .AppendLine();
                    if (!materialMatches)
                    {
                        blocking[blockingCount++] =
                            "the consumer's " + FieldBoidMaterial + " is '" + boundMaterialPath +
                            "', not " + MaterialPath + " - editing that material would not affect this swarm";
                    }

                    serialized.Dispose();
                    break;
                }

                if (!componentFound)
                {
                    report.Append("  ").Append(TokenFail)
                          .Append(" no component on the prefab exposes both '").Append(FieldBoidMesh)
                          .Append("' and '").Append(FieldBoidMaterial).Append("'.").AppendLine();
                    blocking[blockingCount++] = "swarm component not found on " + ConsumerPrefabPath;
                    findings.MeshGateVerdict = "NO-COMPONENT";
                }
                else
                {
                    findings.MeshGateVerdict = EvaluateMeshGate(boundMesh, report, blocking, ref blockingCount);
                }
            }

            // ---- radius sanity + derivation --------------------------------------------
            if (findings.AuthoredBodyRadiusFound)
            {
                // The derivation IS this line: on a mesh whose local Z spans -1..+1 the drawn
                // half-length equals _FishScale, so _FishScale = radius. Written as an identity
                // rather than a literal so a re-authored prefab is detected, not silently ignored.
                findings.DerivedFishScale = findings.AuthoredBodyRadius;

                // Cross-check the clamp the simulation itself applies at
                // SargassumMicroFaunaBoids.cs:2742: radius <= separationRadius * 0.5.
                if (findings.AuthoredSeparationRadius > 0f &&
                    findings.AuthoredBodyRadius > findings.AuthoredSeparationRadius * 0.5f)
                {
                    report.Append("      ").Append(TokenFail)
                          .Append(" authored radius ").Append(F(findings.AuthoredBodyRadius))
                          .Append(" exceeds separationRadius*0.5 = ")
                          .Append(F(findings.AuthoredSeparationRadius * 0.5f))
                          .Append("; the runtime clamp would silently reduce it, so the derived ")
                          .Append("scale would not match what the simulation uses.").AppendLine();
                    blocking[blockingCount++] = "authored radius exceeds the runtime clamp ceiling";
                }

                report.Append("      derivation: drawn half-length = _FishScale (2-unit centred mesh)")
                      .Append("; _FishScale := boidBodyRadius = ").Append(F(findings.DerivedFishScale))
                      .Append(" -> drawn body ").Append(F(findings.DerivedFishScale * 2f))
                      .Append(" m vs simulated ").Append(F(findings.AuthoredBodyRadius * 2f)).Append(" m")
                      .AppendLine();

                if (Mathf.Abs(findings.DerivedFishScale - TargetFishScale) > 0.0001f)
                {
                    blocking[blockingCount++] =
                        "this file's constant " + F(TargetFishScale) +
                        " no longer matches the prefab-derived " + F(findings.DerivedFishScale);
                }
            }
            else
            {
                blocking[blockingCount++] = "could not read " + FieldBoidBodyRadius + " from the prefab";
            }

            string[] reasons = new string[blockingCount];
            Array.Copy(blocking, reasons, blockingCount);
            findings.BlockingReasons = reasons;
            findings.PremiseHolds = blockingCount == 0;
            return findings;
        }

        /// <summary>
        /// The gate the whole ruling rests on: is the bound mesh really 2 units long on Z and
        /// centred there? Measured from <c>Mesh.bounds</c>, which is serialized metadata and
        /// available regardless of the Read/Write import flag - unlike the vertex buffer.
        /// </summary>
        private static string EvaluateMeshGate(
            Mesh mesh,
            StringBuilder report,
            string[] blocking,
            ref int blockingCount)
        {
            if (mesh == null)
            {
                report.Append("      boundMesh=<none> ").Append(TokenFail)
                      .Append(" nothing is bound, so no scale can be calibrated.").AppendLine();
                blocking[blockingCount++] = "no mesh is bound to " + FieldBoidMesh;
                return "NO-MESH";
            }

            string meshPath = AssetDatabase.GetAssetPath(mesh);
            Bounds bounds = mesh.bounds;
            float zExtent = bounds.size.z;
            float zCentre = bounds.center.z;

            bool builtIn = !string.IsNullOrEmpty(meshPath) &&
                           meshPath.IndexOf("unity default resources", StringComparison.OrdinalIgnoreCase) >= 0;
            bool extentOk = Mathf.Abs(zExtent - RequiredMeshZExtent) <= MeshExtentTolerance;
            bool centreOk = Mathf.Abs(zCentre) <= MeshCentreTolerance;

            report.Append("      boundMesh='").Append(mesh.name).Append("' at ")
                  .Append(string.IsNullOrEmpty(meshPath) ? "<no asset path>" : meshPath)
                  .Append(builtIn ? "  [UNITY BUILT-IN PRIMITIVE]" : string.Empty)
                  .AppendLine();
            report.Append("      bounds size=(").Append(F(bounds.size.x)).Append(", ")
                  .Append(F(bounds.size.y)).Append(", ").Append(F(bounds.size.z))
                  .Append(")  center=(").Append(F(bounds.center.x)).Append(", ")
                  .Append(F(bounds.center.y)).Append(", ").Append(F(bounds.center.z)).Append(')')
                  .AppendLine();
            report.Append("      gate: zExtent ").Append(F(zExtent)).Append(" vs required ")
                  .Append(F(RequiredMeshZExtent)).Append(" +/- ").Append(F(MeshExtentTolerance))
                  .Append(" -> ").Append(extentOk ? TokenPass : TokenFail)
                  .Append(" | zCentre ").Append(F(zCentre)).Append(" -> ")
                  .Append(centreOk ? TokenPass : TokenFail)
                  .AppendLine();

            if (builtIn)
            {
                blocking[blockingCount++] =
                    "the bound mesh is a Unity BUILT-IN PRIMITIVE ('" + meshPath +
                    "'), not the forge fish; the 2-unit centred premise cannot hold for it";
            }

            if (!extentOk)
            {
                blocking[blockingCount++] =
                    "bound mesh Z extent is " + F(zExtent) + ", not " + F(RequiredMeshZExtent) +
                    " - the drawn half-length is then " + F(zExtent * 0.5f) +
                    " * _FishScale, so _FishScale is NOT the half-length and 0.18 is the wrong number";
            }

            if (!centreOk)
            {
                blocking[blockingCount++] =
                    "bound mesh is not centred on Z (center.z = " + F(zCentre) +
                    "); BoidFishInstanced.shader:517 tailFactor = saturate(-localPos.z) assumes the " +
                    "body spans -1..+1, so both the tail amplitude and the half-length are off";
            }

            if (builtIn || !extentOk || !centreOk)
                return "FAIL";

            return "PASS";
        }

        // ══════════════════════════════════════════════════════════
        //  SCOPE NOTES - printed every run so neither limit goes quiet
        // ══════════════════════════════════════════════════════════

        private static void AppendScopeNotes(StringBuilder report)
        {
            report.Append("  SCOPE: a SECOND boid renderer shares this shader and OVERRIDES this ")
                  .Append("property. HectonBoidController.cs:1740 pushes _FishScale into a ")
                  .Append("MaterialPropertyBlock from its own [SerializeField] fishScale = 0.3f ")
                  .Append("(:274), and an MPB beats the material. It reads a different field ")
                  .Append("(fishMaterial :182, not boidMaterial), and no reference to this ")
                  .Append("material's GUID reaches it, so this edit is correct and sufficient for ")
                  .Append("the Sargassum swarm - but if the same radius reasoning applies there, ")
                  .Append("its 0.3 is a SECOND change in C#/prefab, outside this ruling.")
                  .AppendLine();
            report.Append("  CONSEQUENCE: MANIFEST_Fauna_Fish_2207_00.json records a consumerContract ")
                  .Append("with \"fishScaleAtMaterial\": 0.28 and ")
                  .Append("\"renderedLengthMetresAtThatScale\": 0.56, and fauna_fish.py:37-47 ")
                  .Append("documents the same 0.28 as the intended metre conversion. Applying 0.18 ")
                  .Append("makes both stale. That is NOT a conflict of intent - fauna_fish.py:46 ")
                  .Append("explicitly defers the choice ('the lead owns _FishScale') - but the ")
                  .Append("manifest is written by the Blender exporter and no Unity asset can ")
                  .Append("update it, so the generator must be re-run or the contract re-recorded ")
                  .Append("by whoever owns that generator.").AppendLine();
            report.Append("  LIMIT: 02_HECTON_WORLD.unity is BINARY and contains ZERO ASCII GUIDs ")
                  .Append("in 6.27 MB (a text scene has 146), so a per-instance scene override of ")
                  .Append("boidBodyRadius, boidMaterial or boidMesh is invisible to any text ")
                  .Append("search. Everything above is the PREFAB-ASSET value. If the scene ")
                  .Append("overrides the radius, this scale is calibrated against the wrong ")
                  .Append("figure and only an editor can tell. This verb reads the prefab asset, ")
                  .Append("not the scene instance.").AppendLine();
        }

        // ══════════════════════════════════════════════════════════
        //  SMALL HELPERS
        // ══════════════════════════════════════════════════════════

        private static StringBuilder NewReport(string phase)
        {
            StringBuilder report = new StringBuilder(4096);
            report.Append(LogPrefix).Append(' ').Append(phase).Append(" begin subject=")
                  .Append(MaterialPath).AppendLine();
            return report;
        }

        private static void Emit(StringBuilder report, bool pass)
        {
            if (pass)
                Debug.Log(report.ToString());
            else
                Debug.LogError(report.ToString());
        }

        private static string F(float value)
        {
            return value.ToString("0.####", CultureInfo.InvariantCulture);
        }
    }
}
#endif
