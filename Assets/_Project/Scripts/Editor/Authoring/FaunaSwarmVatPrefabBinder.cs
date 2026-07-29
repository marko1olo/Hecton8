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
//   3. Two blockers it deliberately does NOT touch, because they are outside the
//      VAT lane and belong to their own owners - it only REPORTS them:
//        - Ocean_Crest.prefab:615 boidMesh is a Unity BUILT-IN primitive
//          ({fileID: 10209, guid: 0000000000000000e000000000000000}). Vertex
//          animation on a built-in primitive is meaningless; the swarm needs a
//          real fish mesh first. ASSET ownership.
//        - Ocean_Crest.prefab:634 neutralAbyssalFlowTexture points at GUID
//          5b18df2e53d2a3f4bbd9eba32746810b, which owns no .meta anywhere under
//          Assets/. In the EDITOR SargassumMicroFaunaBoids.cs:2867-2875 silently
//          re-resolves an authored volume, but that block is #if UNITY_EDITOR,
//          so in a PLAYER BUILD the guard at :2885-2890 fires
//          DisableComputeDispatch and the whole swarm is dead. The existing
//          repair for that is SargassumNeutralAbyssalFlowPrefabRepair, and it
//          has not been run.
//   4. Running this mutates a production prefab. AGENTS.md `Unity And Build
//      Gates` requires explicit instruction for that, so the default entry is a
//      deliberate human MenuItem plus a named batch method - never an automatic
//      or test-runner path.
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

        private const string PositionPageSuffix = "_Position";
        private const string NormalPageSuffix = "_Normal";
        private const string GeneratedPagePrefix = "GEN_FaunaVAT1610_";

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
            Execute(applyChanges: false);
        }

        /// <summary>
        /// Deliberate human apply. Mutates <c>Ocean_Crest.prefab</c>, so it is separated from the audit and
        /// is never invoked by a test runner.
        /// </summary>
        [MenuItem("Hecton8/Fauna/Bind Swarm VAT Pages To Prefab (writes)", false, 401)]
        public static void ApplyFromMenu()
        {
            Execute(applyChanges: true);
        }

        /// <summary>Batch audit. Exits non-zero when the binding is not complete and correct.</summary>
        public static void AuditFromCommandLine()
        {
            EditorApplication.Exit(Execute(applyChanges: false) ? 0 : 1);
        }

        /// <summary>Batch apply. Exits non-zero when the binding could not be completed.</summary>
        public static void ApplyFromCommandLine()
        {
            EditorApplication.Exit(Execute(applyChanges: true) ? 0 : 1);
        }

        private static bool Execute(bool applyChanges)
        {
            if (!TryResolveVatPages(out Texture2D positionPage, out Texture2D normalPage))
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

                    if (!TryValidateVatAgainstMesh(component.name, serialized, positionPage, normalPage, out int frameCount))
                    {
                        rejected++;
                        continue;
                    }

                    bool positionMatches = ReferenceEquals(positionProperty.objectReferenceValue, positionPage);
                    bool normalMatches = ReferenceEquals(normalProperty.objectReferenceValue, normalPage);
                    bool frameCountMatches = frameCountProperty.intValue == frameCount;
                    if (positionMatches && normalMatches && frameCountMatches)
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
                        "{0} {1} '{2}': position {3} -> {4}; normal {5} -> {6}; {7} {8} -> {9}",
                        Marker,
                        applyChanges ? "BIND" : "WOULD BIND",
                        component.name,
                        DescribeReference(positionProperty),
                        positionPage.name,
                        DescribeReference(normalProperty),
                        normalPage.name,
                        FrameCountFieldName,
                        frameCountProperty.intValue.ToString(CultureInfo.InvariantCulture),
                        frameCount.ToString(CultureInfo.InvariantCulture)));

                    if (!applyChanges)
                    {
                        changed++;
                        continue;
                    }

                    positionProperty.objectReferenceValue = positionPage;
                    normalProperty.objectReferenceValue = normalPage;
                    frameCountProperty.intValue = frameCount;
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
        private static bool TryResolveVatPages(out Texture2D positionPage, out Texture2D normalPage)
        {
            positionPage = null;
            normalPage = null;

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

            Debug.Log(string.Format(
                CultureInfo.InvariantCulture,
                "{0} selected page pair '{1}' position={2}x{3} normal={4}x{5}",
                Marker,
                bestToken,
                positionPage.width.ToString(CultureInfo.InvariantCulture),
                positionPage.height.ToString(CultureInfo.InvariantCulture),
                normalPage.width.ToString(CultureInfo.InvariantCulture),
                normalPage.height.ToString(CultureInfo.InvariantCulture)));
            return true;
        }

        /// <summary>
        /// Rejects a page pair that cannot possibly sample correctly against the component's bound mesh.
        /// </summary>
        private static bool TryValidateVatAgainstMesh(
            string componentName,
            SerializedObject serialized,
            Texture2D positionPage,
            Texture2D normalPage,
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

            SerializedProperty meshProperty = serialized.FindProperty(MeshFieldName);
            Mesh boidMesh = meshProperty != null ? meshProperty.objectReferenceValue as Mesh : null;
            if (boidMesh == null)
            {
                Debug.LogError(string.Format(
                    CultureInfo.InvariantCulture,
                    "{0} REJECT '{1}' - '{2}' is null or unreadable, so VAT width cannot be checked against " +
                    "vertex count. SargassumMicroFaunaBoids.cs:8868 feeds _VatVertexCount from this mesh.",
                    Marker,
                    componentName,
                    MeshFieldName));
                return false;
            }

            if (positionPage.width != boidMesh.vertexCount)
            {
                Debug.LogError(string.Format(
                    CultureInfo.InvariantCulture,
                    "{0} REJECT '{1}' - VAT width {2} != {3}.vertexCount {4} ('{5}'). BoidFishInstanced.shader:493 " +
                    "indexes columns by vertexID over _VatVertexCount, and the runtime takes that count from the " +
                    "mesh, not the texture. Binding this pair would deform every vertex from the wrong column while " +
                    "all null checks still pass. Re-bake the VAT from THIS mesh.",
                    Marker,
                    componentName,
                    positionPage.width.ToString(CultureInfo.InvariantCulture),
                    MeshFieldName,
                    boidMesh.vertexCount.ToString(CultureInfo.InvariantCulture),
                    boidMesh.name));
                return false;
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
