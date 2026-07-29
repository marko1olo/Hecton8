// ============================================================================
// HECTON-8 - FaunaSwarmVatPrefabBinder.cs
//
// Closes the LAST authoring link between the fauna VAT baker and the only
// runtime system that can draw a VAT-animated creature in this project.
//
// THE CHAIN, AS MEASURED ON 2026-07-29 (static read, Unity slot held by another
// owner, so every runtime statement below is PENDING VERIFICATION):
//
//   Assets/_Project/Editor/Generators/Fauna/AbyssalAnatomyStudio1610.cs
//     -> bakes GEN_FaunaVAT1610_<token>_Position.asset  (Texture2D, RGBAFloat)
//     -> bakes GEN_FaunaVAT1610_<token>_Normal.asset    (Texture2D, RGBAFloat)
//     -> writes both onto a CLONED MATERIAL, MAT_FaunaVAT1610_<token>.mat
//   Assets/_Project/Scripts/World/SargassumMicroFaunaBoids.cs
//     -> is the ONLY runtime consumer of BoidFishInstanced.shader's VAT branch
//     -> DOES NOT READ THAT MATERIAL ASSET'S TEXTURES. It pushes its own two
//        [SerializeField] private Texture2D fields onto boidMaterial at runtime:
//          :1011 boidVatPositionTexture
//          :1015 boidVatNormalTexture
//          :8895-8896 renderMaterial.SetTexture(_VatPositionTexId, ...)
//     -> and it refuses to raise _VatEnabled unless ALL THREE hold (:8914-8916):
//          boidVatPositionTexture != null
//          boidVatNormalTexture   != null
//          boidVatFrameCount      >  1
//
// So the generator's material output is architecturally orphaned: a perfect bake
// changes nothing at runtime until those three serialized values are set on the
// component. That assignment is what this file does, and nothing else in the
// project does it. Ocean_Crest.prefab:617-619 currently reads
//   boidVatPositionTexture: {fileID: 0}
//   boidVatNormalTexture: {fileID: 0}
//   boidVatFrameCount: 1
// i.e. all three gate conditions are false and the shader's procedural tail-wag
// fallback (BoidFishInstanced.shader:507-522) is the only thing that ever runs.
//
// WHY THE VAT WIDTH CHECK IS A HARD REFUSAL, NOT A WARNING:
//   BoidFishInstanced.shader:493 indexes the VAT by vertex id,
//     vertexU = (vertexID + 0.5) * rcp(max(_VatVertexCount, 1.0))
//   and the runtime feeds _VatVertexCount from the MESH, not the texture:
//     SargassumMicroFaunaBoids.cs:8868  vatVertexCount = _boidMeshVertexCount
//   If VAT width != boidMesh.vertexCount every vertex samples the wrong column
//   and the swarm deforms into garbage while every null check still passes. That
//   is the silent-degeneracy class the project rules single out, so it is
//   rejected here instead of being discovered in a capture.
//
// SCOPE, HONESTLY STATED:
//   1. This binds the PREFAB ASSET only. 02_HECTON_WORLD.unity is a BINARY scene
//      and its Ocean_Crest instance may carry per-instance overrides on these
//      fields that no file scan can see. Until a scene-side probe prints the
//      EFFECTIVE values, this binding is PENDING VERIFICATION.
//   2. It never creates a texture. If no baked page exists it aborts and names
//      the bake command. Fabricating VAT data would be worse than no VAT.
//   3. boidMesh: REPORTED by default, REBOUND only on explicit opt-in.
//      Ocean_Crest.prefab:615 boidMesh is a Unity BUILT-IN primitive
//      ({fileID: 10209, guid: 0000000000000000e000000000000000}). Vertex
//      animation on a built-in primitive is meaningless, and you cannot bake a
//      VAT from one either - so while that value stands, the width gate below
//      can NEVER pass however many times the bake runs. Treating the mesh as
//      purely somebody else's problem therefore made this binder unclosable
//      rather than merely unfed, which is why the opt-in exists: the bake's own
//      companion mesh (Assets/_Project/Art/Generated/Fauna/Rigged1610/
//      GEN_FaunaVAT1610_<token>_Mesh.asset, an Object.Instantiate of the source
//      mesh) has the matching vertex count by construction. The default entries
//      still leave boidMesh alone and still refuse; -h8FaunaBindMesh, or the
//      "Bind Swarm VAT Pages + Companion Mesh To Prefab" menu entry, rebinds it
//      and clears the gate. Whether the resulting silhouette reads as a creature
//      is still 3DMODEL_FAUNA.md section 1's call and is PENDING VERIFICATION.
//   4. One blocker it does NOT touch, because it belongs to another owner and it
//      only REPORTS it:
//        - Ocean_Crest.prefab:634 neutralAbyssalFlowTexture points at GUID
//          5b18df2e53d2a3f4bbd9eba32746810b, which owns no .meta anywhere under
//          Assets/. In the EDITOR SargassumMicroFaunaBoids.cs:2867-2875 silently
//          re-resolves an authored volume, but that block is #if UNITY_EDITOR,
//          so in a PLAYER BUILD the guard at :2885-2890 fires
//          DisableComputeDispatch and the whole swarm is dead. The existing
//          repair for that is SargassumNeutralAbyssalFlowPrefabRepair, and it
//          has not been run.
//   5. Running this mutates a production prefab. AGENTS.md `Unity And Build
//      Gates` requires explicit instruction for that, so the default entry is a
//      deliberate human MenuItem plus a named batch method - never an automatic
//      or test-runner path.
//
// CONTENT PRECONDITION, MEASURED 2026-07-29: none of this can fire yet.
// Assets/_Project/Art/Generated/Fauna/VAT1610 does not exist because the bake has
// never run, and the bake cannot run because FaunaOfflineRigger1610.RawInputFolder
// ("Assets/_Project/Art/Fauna/Raw", AbyssalAnatomyStudio1610.cs:485) does not exist
// and the project contains ZERO fauna source geometry - every .fbx under
// Assets/_Project is flora, geology, rock, small-prop or prologue architecture. So
// the ABORT in TryResolveVatPages is the correct and expected answer today, and the
// blocker is CONTENT (one fish mesh), not code.
// ============================================================================

using System;
using System.Globalization;
using Hecton8.World;
using UnityEditor;
using UnityEngine;

namespace Hecton8.Editor.Authoring
{
    /// <summary>
    /// Binds the baked fauna swarm VAT pages onto every <see cref="SargassumMicroFaunaBoids"/> inside
    /// <c>Ocean_Crest.prefab</c> and sets the frame count that gates the shader's VAT branch. Idempotent:
    /// an already-correct component is reported and left alone.
    /// </summary>
    public static class FaunaSwarmVatPrefabBinder
    {
        private const string Marker = "[H8_FAUNA_VATBIND]";

        /// <summary>Only prefab that carries the component (GUID census over <c>Assets/</c>, 2026-07-29).</summary>
        private const string BoidPrefabPath = "Assets/_Project/Prefabs/Ocean_Crest.prefab";

        /// <summary>
        /// Source of truth is <c>FaunaOfflineRigger1610.VatOutputRoot</c> in
        /// <c>Assets/_Project/Editor/Generators/Fauna/AbyssalAnatomyStudio1610.cs</c>. It is duplicated as a
        /// literal because <c>Hecton8.Editor.asmdef</c> does not reference
        /// <c>Hecton8.Editor.Generators.Fauna</c>, and adding that reference is a wider change than this
        /// binder needs. If the generator's output root moves, this constant must move with it - the
        /// mismatch surfaces as an explicit ABORT below, not as silence.
        /// </summary>
        private const string VatOutputRoot = "Assets/_Project/Art/Generated/Fauna/VAT1610";

        /// <summary>
        /// Companion mesh root. Mirrors <c>FaunaOfflineRigger1610.MeshOutputRoot</c>
        /// (<c>AbyssalAnatomyStudio1610.cs:486</c>) as a literal for exactly the same reason as
        /// <see cref="VatOutputRoot"/> - <c>Hecton8.Editor.asmdef</c> does not reference
        /// <c>Hecton8.Editor.Generators.Fauna</c>.
        ///
        /// THIS IS THE CONSTANT THAT CLOSES THE CHAIN. The bake writes the companion mesh as
        /// <c>Object.Instantiate(sourceMesh)</c> (AbyssalAnatomyStudio1610.cs:1064-1065) under this root,
        /// so its vertexCount equals the VAT page width BY CONSTRUCTION. It is therefore the only mesh in
        /// the project that can satisfy the width gate in <see cref="TryValidateVatAgainstMesh"/>. Before
        /// this existed the binder validated <c>boidMesh</c> but never bound it, and
        /// Ocean_Crest.prefab:615 holds a Unity BUILT-IN primitive
        /// (<c>{fileID: 10209, guid: 0000000000000000e000000000000000}</c>), so the gate could never pass
        /// no matter how many times the bake ran. The chain was unclosable, not merely unfed.
        /// </summary>
        private const string RiggedMeshOutputRoot = "Assets/_Project/Art/Generated/Fauna/Rigged1610";

        private const string PositionPageSuffix = "_Position";
        private const string NormalPageSuffix = "_Normal";
        private const string MeshAssetSuffix = "_Mesh";
        private const string GeneratedPagePrefix = "GEN_FaunaVAT1610_";

        /// <summary>Opt-in for the companion-mesh write. Absent means the historical page-only behaviour.</summary>
        private const string BindMeshFlag = "-h8FaunaBindMesh";

        private const string PositionFieldName = "boidVatPositionTexture";
        private const string NormalFieldName = "boidVatNormalTexture";
        private const string FrameCountFieldName = "boidVatFrameCount";
        private const string MeshFieldName = "boidMesh";
        private const string FlowFieldName = "neutralAbyssalFlowTexture";

        /// <summary>
        /// Reports what would change and why, and writes nothing. Safe to run at any time.
        /// </summary>
        [MenuItem("Hecton8/Fauna/Audit Swarm VAT Prefab Binding", false, 400)]
        public static void AuditFromMenu()
        {
            Execute(applyChanges: false, bindMesh: false);
        }

        /// <summary>
        /// Deliberate human apply. Mutates <c>Ocean_Crest.prefab</c>, so it is separated from the audit and
        /// is never invoked by a test runner. Binds the two VAT pages and the frame count only; it leaves
        /// <c>boidMesh</c> alone, so it still REFUSES while that field holds a built-in primitive. Use the
        /// companion-mesh entry below to close the chain in one step.
        /// </summary>
        [MenuItem("Hecton8/Fauna/Bind Swarm VAT Pages To Prefab (writes)", false, 401)]
        public static void ApplyFromMenu()
        {
            Execute(applyChanges: true, bindMesh: false);
        }

        /// <summary>
        /// Deliberate human apply that ALSO rebinds <c>boidMesh</c> to the companion mesh from the same
        /// bake token. Separated from the page-only apply because swapping the drawn mesh is a visible
        /// authoring change, not a wiring change: it replaces the built-in primitive at
        /// Ocean_Crest.prefab:615 with generated geometry, and the swarm will look different afterwards.
        /// That swap is nevertheless the ONLY way the width gate can pass, which is why it is offered here
        /// rather than left as an unreachable refusal.
        /// </summary>
        [MenuItem("Hecton8/Fauna/Bind Swarm VAT Pages + Companion Mesh To Prefab (writes)", false, 402)]
        public static void ApplyWithCompanionMeshFromMenu()
        {
            Execute(applyChanges: true, bindMesh: true);
        }

        /// <summary>Batch audit. Exits non-zero when the binding is not complete and correct.</summary>
        public static void AuditFromCommandLine()
        {
            EditorApplication.Exit(Execute(applyChanges: false, bindMesh: HasBindMeshFlag()) ? 0 : 1);
        }

        /// <summary>
        /// Batch apply. Exits non-zero when the binding could not be completed. Pass
        /// <c>-h8FaunaBindMesh</c> to also rebind <c>boidMesh</c> to the companion mesh from the same bake
        /// token; without it this refuses on any prefab whose <c>boidMesh</c> width does not already match.
        /// </summary>
        public static void ApplyFromCommandLine()
        {
            EditorApplication.Exit(Execute(applyChanges: true, bindMesh: HasBindMeshFlag()) ? 0 : 1);
        }

        /// <summary>
        /// <c>System.Environment</c> is spelled out: a bare <c>Environment</c> inside a <c>Hecton8.*</c>
        /// namespace binds to <c>Hecton8.Environment</c> and fails CS0234.
        /// </summary>
        private static bool HasBindMeshFlag()
        {
            string[] args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (string.Equals(args[i], BindMeshFlag, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        private static bool Execute(bool applyChanges, bool bindMesh)
        {
            if (!TryResolveVatPages(out Texture2D positionPage, out Texture2D normalPage, out Mesh companionMesh))
                return false;

            if (AssetDatabase.LoadAssetAtPath<GameObject>(BoidPrefabPath) == null)
            {
                Debug.LogError(string.Format(
                    CultureInfo.InvariantCulture,
                    "{0} ABORT - prefab not found at '{1}'.",
                    Marker,
                    BoidPrefabPath));
                return false;
            }

            GameObject contents = null;
            try
            {
                contents = PrefabUtility.LoadPrefabContents(BoidPrefabPath);
            }
            catch (Exception exception) when (exception is UnityException ||
                                             exception is InvalidOperationException ||
                                             exception is ArgumentException)
            {
                Debug.LogError(string.Format(
                    CultureInfo.InvariantCulture,
                    "{0} ABORT - could not load prefab contents for '{1}': {2}: {3}",
                    Marker,
                    BoidPrefabPath,
                    exception.GetType().Name,
                    exception.Message));
                return false;
            }

            try
            {
                // COLD ALLOC: SargassumMicroFaunaBoids[] - one editor-only component census - owner: FaunaSwarmVatPrefabBinder
                SargassumMicroFaunaBoids[] components = contents.GetComponentsInChildren<SargassumMicroFaunaBoids>(true);
                if (components == null || components.Length == 0)
                {
                    Debug.LogError(string.Format(
                        CultureInfo.InvariantCulture,
                        "{0} INCONCLUSIVE - no SargassumMicroFaunaBoids inside '{1}'. The GUID census says it is " +
                        "there, so finding none means the census is wrong and nothing was proven.",
                        Marker,
                        BoidPrefabPath));
                    return false;
                }

                int changed = 0;
                int alreadyCorrect = 0;
                int rejected = 0;

                for (int i = 0; i < components.Length; i++)
                {
                    SargassumMicroFaunaBoids component = components[i];
                    SerializedObject serialized = new SerializedObject(component);

                    SerializedProperty positionProperty = serialized.FindProperty(PositionFieldName);
                    SerializedProperty normalProperty = serialized.FindProperty(NormalFieldName);
                    SerializedProperty frameCountProperty = serialized.FindProperty(FrameCountFieldName);
                    if (positionProperty == null || normalProperty == null || frameCountProperty == null)
                    {
                        rejected++;
                        Debug.LogError(string.Format(
                            CultureInfo.InvariantCulture,
                            "{0} REJECT '{1}' - one of '{2}', '{3}', '{4}' is missing on the component. Renamed or " +
                            "removed; this binder no longer applies and must be updated against live source.",
                            Marker,
                            component.name,
                            PositionFieldName,
                            NormalFieldName,
                            FrameCountFieldName));
                        continue;
                    }

                    ReportUntouchedBlockers(component.name, serialized);

                    SerializedProperty meshProperty = serialized.FindProperty(MeshFieldName);
                    Mesh currentMesh = meshProperty != null ? meshProperty.objectReferenceValue as Mesh : null;

                    // The mesh the width gate must be measured against is the one that WILL be bound, not
                    // the one currently serialized. Measuring the current mesh while intending to replace
                    // it would reject a pair that is about to become correct.
                    bool willBindMesh = bindMesh && companionMesh != null;
                    Mesh effectiveMesh = willBindMesh ? companionMesh : currentMesh;

                    if (!TryValidateVatAgainstMesh(
                            component.name,
                            effectiveMesh,
                            currentMesh,
                            companionMesh,
                            positionPage,
                            normalPage,
                            willBindMesh,
                            out int frameCount))
                    {
                        rejected++;
                        continue;
                    }

                    bool positionMatches = ReferenceEquals(positionProperty.objectReferenceValue, positionPage);
                    bool normalMatches = ReferenceEquals(normalProperty.objectReferenceValue, normalPage);
                    bool frameCountMatches = frameCountProperty.intValue == frameCount;
                    bool meshMatches = !willBindMesh || ReferenceEquals(currentMesh, companionMesh);
                    if (positionMatches && normalMatches && frameCountMatches && meshMatches)
                    {
                        alreadyCorrect++;
                        Debug.Log(string.Format(
                            CultureInfo.InvariantCulture,
                            "{0} KEPT '{1}' - already bound. frameCount={2}",
                            Marker,
                            component.name,
                            frameCount.ToString(CultureInfo.InvariantCulture)));
                        continue;
                    }

                    Debug.Log(string.Format(
                        CultureInfo.InvariantCulture,
                        "{0} {1} '{2}': position {3} -> {4}; normal {5} -> {6}; {7} {8} -> {9}; {10} {11}",
                        Marker,
                        applyChanges ? "BIND" : "WOULD BIND",
                        component.name,
                        DescribeReference(positionProperty),
                        positionPage.name,
                        DescribeReference(normalProperty),
                        normalPage.name,
                        FrameCountFieldName,
                        frameCountProperty.intValue.ToString(CultureInfo.InvariantCulture),
                        frameCount.ToString(CultureInfo.InvariantCulture),
                        MeshFieldName,
                        willBindMesh
                            ? (currentMesh != null ? currentMesh.name : "NULL") + " -> " + companionMesh.name
                            : "UNCHANGED"));

                    if (!applyChanges)
                    {
                        changed++;
                        continue;
                    }

                    positionProperty.objectReferenceValue = positionPage;
                    normalProperty.objectReferenceValue = normalPage;
                    frameCountProperty.intValue = frameCount;
                    if (willBindMesh && meshProperty != null)
                        meshProperty.objectReferenceValue = companionMesh;
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                    changed++;
                }

                if (rejected > 0)
                    return false;

                if (!applyChanges)
                {
                    Debug.Log(string.Format(
                        CultureInfo.InvariantCulture,
                        "{0} AUDIT-ONLY - pendingChanges={1} alreadyCorrect={2}. Nothing was written. Run " +
                        "Hecton8/Fauna/Bind Swarm VAT Pages To Prefab (writes) to apply.",
                        Marker,
                        changed.ToString(CultureInfo.InvariantCulture),
                        alreadyCorrect.ToString(CultureInfo.InvariantCulture)));
                    return changed == 0;
                }

                if (changed == 0)
                {
                    Debug.Log(string.Format(
                        CultureInfo.InvariantCulture,
                        "{0} NO-CHANGE - {1} component(s) already bound.",
                        Marker,
                        alreadyCorrect.ToString(CultureInfo.InvariantCulture)));
                    return true;
                }

                PrefabUtility.SaveAsPrefabAsset(contents, BoidPrefabPath, out bool saved);
                if (!saved)
                {
                    Debug.LogError(string.Format(
                        CultureInfo.InvariantCulture,
                        "{0} ABORT - SaveAsPrefabAsset reported failure for '{1}'; nothing was written.",
                        Marker,
                        BoidPrefabPath));
                    return false;
                }

                AssetDatabase.SaveAssets();

                Debug.Log(string.Format(
                    CultureInfo.InvariantCulture,
                    "{0} SAVED '{1}' - bound={2} kept={3}. PENDING VERIFICATION: 02_HECTON_WORLD.unity is binary " +
                    "and its Ocean_Crest instance may hold overrides on '{4}', '{5}' or '{6}' that this does not " +
                    "touch, and no Frame Debugger or Play Mode capture has confirmed _VatEnabled actually rises.",
                    Marker,
                    BoidPrefabPath,
                    changed.ToString(CultureInfo.InvariantCulture),
                    alreadyCorrect.ToString(CultureInfo.InvariantCulture),
                    PositionFieldName,
                    NormalFieldName,
                    FrameCountFieldName));

                return true;
            }
            finally
            {
                if (contents != null)
                    PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        /// <summary>
        /// Finds the newest baked position/normal page pair. Refuses on a lone position page, because the
        /// runtime gate needs both and a half-bound component would silently keep the fallback path.
        /// </summary>
        private static bool TryResolveVatPages(
            out Texture2D positionPage,
            out Texture2D normalPage,
            out Mesh companionMesh)
        {
            positionPage = null;
            normalPage = null;
            companionMesh = null;

            if (!AssetDatabase.IsValidFolder(VatOutputRoot))
            {
                Debug.LogError(string.Format(
                    CultureInfo.InvariantCulture,
                    "{0} ABORT - '{1}' does not exist, so the fauna VAT baker has never produced an artifact. " +
                    "This binder never fabricates one. Produce a page first with: Unity.exe -batchmode -quit " +
                    "-projectPath <project> -executeMethod " +
                    "Hecton8.EditorTools.Generators.Fauna.FaunaHeadlessBake1610.BakeFromCommandLine " +
                    "-h8FaunaMesh <meshAssetPath> -h8FaunaMaterial <materialAssetPath> -h8FaunaPreset VatSwarm " +
                    "-h8FaunaVatFrames 30 -logFile - (do NOT pass -nographics).",
                    Marker,
                    VatOutputRoot));
                return false;
            }

            // COLD ALLOC: string[] - one AssetDatabase texture census of the VAT output root - owner: FaunaSwarmVatPrefabBinder
            string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { VatOutputRoot });
            string bestToken = null;
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (texture == null || !texture.name.StartsWith(GeneratedPagePrefix, StringComparison.Ordinal))
                    continue;

                if (!texture.name.EndsWith(PositionPageSuffix, StringComparison.Ordinal))
                    continue;

                string token = texture.name.Substring(0, texture.name.Length - PositionPageSuffix.Length);
                Texture2D pairedNormal = AssetDatabase.LoadAssetAtPath<Texture2D>(
                    VatOutputRoot + "/" + token + NormalPageSuffix + ".asset");
                if (pairedNormal == null)
                {
                    Debug.LogWarning(string.Format(
                        CultureInfo.InvariantCulture,
                        "{0} SKIP '{1}' - no matching '{2}{3}' page. The runtime gate at " +
                        "SargassumMicroFaunaBoids.cs:8914-8916 needs both pages, so a position-only bake is unusable.",
                        Marker,
                        texture.name,
                        token,
                        NormalPageSuffix));
                    continue;
                }

                // Deterministic selection: ordinal-last token wins, so repeated runs pick the same pair
                // regardless of AssetDatabase enumeration order.
                if (bestToken == null || string.CompareOrdinal(token, bestToken) > 0)
                {
                    bestToken = token;
                    positionPage = texture;
                    normalPage = pairedNormal;
                }
            }

            if (positionPage == null || normalPage == null)
            {
                Debug.LogError(string.Format(
                    CultureInfo.InvariantCulture,
                    "{0} ABORT - no complete '{1}*{2}' + '{1}*{3}' page pair under '{4}'. candidates={5}",
                    Marker,
                    GeneratedPagePrefix,
                    PositionPageSuffix,
                    NormalPageSuffix,
                    VatOutputRoot,
                    guids.Length.ToString(CultureInfo.InvariantCulture)));
                return false;
            }

            // Same token, sibling root. Absence is reported but is NOT an abort: a caller that only wants
            // the page binding on an already-correct mesh does not need it, and inventing a mesh would be
            // worse than naming the gap.
            string companionMeshPath = RiggedMeshOutputRoot + "/" + bestToken + MeshAssetSuffix + ".asset";
            companionMesh = AssetDatabase.LoadAssetAtPath<Mesh>(companionMeshPath);

            Debug.Log(string.Format(
                CultureInfo.InvariantCulture,
                "{0} selected page pair '{1}' position={2}x{3} normal={4}x{5} companionMesh={6}",
                Marker,
                bestToken,
                positionPage.width.ToString(CultureInfo.InvariantCulture),
                positionPage.height.ToString(CultureInfo.InvariantCulture),
                normalPage.width.ToString(CultureInfo.InvariantCulture),
                normalPage.height.ToString(CultureInfo.InvariantCulture),
                companionMesh == null
                    ? "<absent at '" + companionMeshPath + "'>"
                    : companionMesh.name + " vertexCount=" +
                      companionMesh.vertexCount.ToString(CultureInfo.InvariantCulture)));
            return true;
        }

        /// <summary>
        /// Rejects a page pair that cannot possibly sample correctly against the mesh that will be bound.
        /// </summary>
        /// <param name="effectiveMesh">
        /// The mesh the width gate is measured against: the companion mesh when
        /// <paramref name="willBindMesh"/> is set, otherwise whatever is currently serialized. Measuring the
        /// current mesh while intending to replace it would reject a pair that is about to become correct.
        /// </param>
        /// <param name="currentMesh">Serialized value, used only for the swap warning.</param>
        /// <param name="companionMesh">
        /// Same-token mesh from the bake, or null. Used to turn an otherwise unactionable width refusal into
        /// a named fix when its vertex count happens to match the page pair.
        /// </param>
        private static bool TryValidateVatAgainstMesh(
            string componentName,
            Mesh effectiveMesh,
            Mesh currentMesh,
            Mesh companionMesh,
            Texture2D positionPage,
            Texture2D normalPage,
            bool willBindMesh,
            out int frameCount)
        {
            frameCount = 0;

            if (positionPage.width != normalPage.width || positionPage.height != normalPage.height)
            {
                Debug.LogError(string.Format(
                    CultureInfo.InvariantCulture,
                    "{0} REJECT '{1}' - page dimensions disagree. position={2}x{3} normal={4}x{5}. Both pages are " +
                    "indexed by the same (vertexU, frame) pair, so a mismatch guarantees wrong normals.",
                    Marker,
                    componentName,
                    positionPage.width.ToString(CultureInfo.InvariantCulture),
                    positionPage.height.ToString(CultureInfo.InvariantCulture),
                    normalPage.width.ToString(CultureInfo.InvariantCulture),
                    normalPage.height.ToString(CultureInfo.InvariantCulture)));
                return false;
            }

            if (positionPage.height <= 1)
            {
                Debug.LogError(string.Format(
                    CultureInfo.InvariantCulture,
                    "{0} REJECT '{1}' - page height is {2}. SargassumMicroFaunaBoids.cs:8916 requires " +
                    "boidVatFrameCount > 1 and BoidFishInstanced.shader:488 requires _VatFrameCount > 1.0, so a " +
                    "single-frame page can never switch the VAT branch on.",
                    Marker,
                    componentName,
                    positionPage.height.ToString(CultureInfo.InvariantCulture)));
                return false;
            }

            if (effectiveMesh == null)
            {
                Debug.LogError(string.Format(
                    CultureInfo.InvariantCulture,
                    "{0} REJECT '{1}' - '{2}' is null or unreadable and no companion mesh is available, so VAT " +
                    "width cannot be checked against vertex count. SargassumMicroFaunaBoids.cs:8868 feeds " +
                    "_VatVertexCount from this mesh. Either author '{2}' or re-run with {3} once the bake has " +
                    "written '{4}/<token>{5}.asset'.",
                    Marker,
                    componentName,
                    MeshFieldName,
                    BindMeshFlag,
                    RiggedMeshOutputRoot,
                    MeshAssetSuffix));
                return false;
            }

            if (positionPage.width != effectiveMesh.vertexCount)
            {
                // The actionable half of this refusal. A plain "re-bake from THIS mesh" was unactionable
                // while boidMesh held a Unity built-in primitive: you cannot bake a VAT from a built-in
                // primitive, so the operator had no move that could ever clear the gate. If the bake's own
                // companion mesh fits, name it and the flag that binds it.
                bool companionWouldFit = !willBindMesh
                                         && companionMesh != null
                                         && companionMesh.vertexCount == positionPage.width;

                Debug.LogError(string.Format(
                    CultureInfo.InvariantCulture,
                    "{0} REJECT '{1}' - VAT width {2} != {3}.vertexCount {4} ('{5}'). BoidFishInstanced.shader:493 " +
                    "indexes columns by vertexID over _VatVertexCount, and the runtime takes that count from the " +
                    "mesh, not the texture. Binding this pair would deform every vertex from the wrong column while " +
                    "all null checks still pass. {6}",
                    Marker,
                    componentName,
                    positionPage.width.ToString(CultureInfo.InvariantCulture),
                    MeshFieldName,
                    effectiveMesh.vertexCount.ToString(CultureInfo.InvariantCulture),
                    effectiveMesh.name,
                    companionWouldFit
                        ? "FIX AVAILABLE: the companion mesh '" + companionMesh.name + "' from the same bake token " +
                          "has vertexCount " + companionMesh.vertexCount.ToString(CultureInfo.InvariantCulture) +
                          ", which matches this page pair exactly. Re-run with " + BindMeshFlag +
                          " (or the 'Bind Swarm VAT Pages + Companion Mesh To Prefab' menu entry) to rebind '" +
                          MeshFieldName + "' and clear this gate. That REPLACES the drawn mesh, so it is opt-in."
                        : "Re-bake the VAT from THIS mesh, or bake from a mesh you are willing to bind and re-run " +
                          "with " + BindMeshFlag + "."));
                return false;
            }

            if (willBindMesh && !ReferenceEquals(currentMesh, companionMesh))
            {
                Debug.LogWarning(string.Format(
                    CultureInfo.InvariantCulture,
                    "{0} MESH SWAP '{1}' - '{2}' will change from '{3}' to the generated companion mesh '{4}' " +
                    "(vertexCount={5}). This is a visible authoring change, not a wiring change: the swarm's " +
                    "silhouette is replaced. PENDING VERIFICATION - no capture has confirmed the new silhouette " +
                    "reads as a creature, and 3DMODEL_FAUNA.md section 1 owns that judgement.",
                    Marker,
                    componentName,
                    MeshFieldName,
                    currentMesh != null ? currentMesh.name : "NULL",
                    companionMesh.name,
                    companionMesh.vertexCount.ToString(CultureInfo.InvariantCulture)));
            }

            frameCount = positionPage.height;
            return true;
        }

        /// <summary>
        /// Logs the two known blockers this binder deliberately leaves alone so they cannot be forgotten
        /// once the VAT fields read as bound.
        /// </summary>
        private static void ReportUntouchedBlockers(string componentName, SerializedObject serialized)
        {
            SerializedProperty meshProperty = serialized.FindProperty(MeshFieldName);
            Mesh boidMesh = meshProperty != null ? meshProperty.objectReferenceValue as Mesh : null;
            if (boidMesh != null)
            {
                string meshAssetPath = AssetDatabase.GetAssetPath(boidMesh);
                if (string.IsNullOrEmpty(meshAssetPath) ||
                    meshAssetPath.IndexOf("unity_builtin_extra", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    meshAssetPath.IndexOf("unity default resources", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    Debug.LogWarning(string.Format(
                        CultureInfo.InvariantCulture,
                        "{0} BLOCKER (not fixed here) '{1}' - '{2}' is the Unity built-in primitive '{3}' " +
                        "(vertexCount={4}, assetPath='{5}'). Vertex animation on a built-in primitive carries no " +
                        "creature silhouette; 3DMODEL_FAUNA.md section 1 rejects it outright. A real fish mesh is an " +
                        "ASSET-owner task, not a VAT task.",
                        Marker,
                        componentName,
                        MeshFieldName,
                        boidMesh.name,
                        boidMesh.vertexCount.ToString(CultureInfo.InvariantCulture),
                        string.IsNullOrEmpty(meshAssetPath) ? "<none>" : meshAssetPath));
                }
            }

            SerializedProperty flowProperty = serialized.FindProperty(FlowFieldName);
            if (flowProperty != null && flowProperty.objectReferenceValue == null)
            {
                Debug.LogWarning(string.Format(
                    CultureInfo.InvariantCulture,
                    "{0} BLOCKER (not fixed here) '{1}' - '{2}' deserialises to null. In the Editor " +
                    "SargassumMicroFaunaBoids.cs:2867-2875 re-resolves an authored volume, but that block is " +
                    "#if UNITY_EDITOR, so in a PLAYER BUILD the guard at :2885-2890 calls DisableComputeDispatch " +
                    "and the entire swarm - VAT or not - never draws. Run " +
                    "Hecton8/VFX/Repair Sargassum Neutral Abyssal Flow Reference.",
                    Marker,
                    componentName,
                    FlowFieldName));
            }
        }

        private static string DescribeReference(SerializedProperty property)
        {
            if (property == null)
                return "<missing>";

            return property.objectReferenceValue != null ? property.objectReferenceValue.name : "NULL";
        }
    }
}
