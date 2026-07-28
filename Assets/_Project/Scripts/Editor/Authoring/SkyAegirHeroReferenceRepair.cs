// ============================================================================
// HECTON-8 - SkyAegirHeroReferenceRepair.cs
//
// Repairs the two DEAD serialized asset references left on the sky/celestial
// hero prefabs by commit 621403ad5 "refactor(modding,sky): data-only override
// mod system & 1428 file cleanup" (marko1olo, 2026-06-15), which deleted 523
// files including the whole *_1428 material/mesh family.
//
// DEFECT 1 - SKY DOME MESH IS GONE (rebindable, fixed here)
//   Assets/_Project/Prefabs/Sky_System.prefab:43
//     m_Mesh: {fileID: 4300000, guid: f75a1c4016b005f4588059e3dddbd8a1, type: 2}
//   That GUID owned Assets/_Project/Art/Meshes/Generated/MESH_SurfaceSkyDomeNoir_1428.asset
//   (Docs/AssetAudit/ASSET_GUID_REFERENCE_MATRIX_20260605.csv:174). No .meta in
//   Assets/ or Packages/ defines it any more, and none defines it inside
//   .claude/worktrees/** either, so it is dead everywhere and not merely
//   worktree-shadowed. The GameObject is "Sphere" at m_LocalScale
//   25000,25000,25000 - a dangling m_Mesh deserializes to null, so the 25 km
//   dome renders nothing at all.
//
//   WHY IT REBINDS TO SkyDome_Inverted.asset RATHER THAN RESURRECTING THE 1428 MESH:
//   Sky_System.prefab already carries Mat_HectonSky.mat
//   (guid c94a1beef2372b8458941c2ed9d05d5e, shader Hecton_AlienSky_Master).
//   The live production world scene 02_HECTON_WORLD.unity is a BINARY scene, and
//   a byte-level GUID probe (Unity stores reference GUIDs nibble-swapped, an
//   encoding validated in that same probe against four controls that are
//   provably present) shows it references BOTH Mat_HectonSky.mat AND
//   Assets/_Project/Art/Models/SkyDome_Inverted.asset - and does NOT reference
//   the dead 1428 mesh. So production already pairs this exact material with
//   this exact dome mesh. Rebinding the prefab to it restores the pairing the
//   shipping scene already proves, instead of resurrecting a mesh the owner
//   deliberately deleted.
//
//   The filename claiming "Inverted" is not accepted as proof. VerifyInwardFacing
//   below measures the actual normal orientation and REFUSES to bind an
//   outward-facing mesh, because an outward dome is invisible from inside and
//   would look exactly like the bug it was meant to fix.
//
//   SEVERITY - THIS ONE IS IN THE SHIPPING WORLD SCENE. The same byte probe finds
//   Sky_System.prefab instantiated in BOTH 02_HECTON_WORLD.unity and
//   010_TEST.unity. A text search finds neither, because both scenes are binary;
//   that false negative is exactly the trap this comment exists to record.
//   Because 02_HECTON_WORLD.unity also references SkyDome_Inverted.asset, its
//   Sky_System instance may ALREADY carry a prefab-instance override onto the
//   correct mesh - in which case the scene renders and only the prefab asset is
//   rotten. That cannot be resolved offline in a binary scene, so after running
//   this, confirm the EFFECTIVE m_Mesh on the scene instance in Unity before
//   claiming the dome is fixed, per the AGENTS.md prefab/scene consistency guard.
//
// DEFECT 2 - AEGIR IMPOSTOR MATERIAL *AND* ITS SHADER ARE BOTH GONE (not rebindable)
//   Assets/_Project/Prefabs/GasGiant_Aegir.prefab:69
//   Assets/_Project/_PROLOGUE_CONTENT/Prefabs/GasGiant_Aegir.prefab:69
//     - {fileID: 2100000, guid: ab7b03af667690149bdc7be9a1ae023c, type: 2}
//   That GUID owned MAT_AegirGasGiant_Impostor_1428.mat. The same commit ALSO
//   deleted the only shader it used, H8_AegirGasGiantImpostor_1428.shader, so
//   restoring the material from history would restore a material bound to a dead
//   shader - still magenta. There is no surviving gas-giant material in
//   Art/Materials/Celestial/ (six MAT_CelestialMoon_* only) and binding the
//   skybox material MAT_AegirSky_Master.mat to a MeshRenderer would be a
//   category error, so NO REBIND IS HONEST HERE.
//
//   The mesh GUID on both prefabs (fc0e817a..., Art/Models/gasgiant.asset) is
//   ALIVE, so these renderers draw real sphere geometry with a null material -
//   Unity's magenta error material - in the editor and anywhere the celestial
//   engine has not run.
//
//   WHAT THIS DOES INSTEAD: disables the MeshRenderer, which is exactly the
//   decision the runtime owner already makes for itself at
//   HectonCelestialEngine.cs:3018 ("disabling mesh presentation and keeping sky
//   projection globals authoritative", :3014) when
//   ValidateAegirRendererMaterialCold (:2999) finds a null sharedMaterial and a
//   null aegirFallbackMaterial (:1035). Aegir is still drawn: the surviving
//   Hecton_AegirSky.shader draws it analytically in DrawAegir (:374) with the
//   canonical band texture, limb darkening (:435) and atmosphere scatter (:439),
//   and MAT_AegirSky_Master.mat is the skybox of 01_ORBIT.unity - the only live
//   scene that instantiates this prefab. Removing a duplicate celestial
//   presentation owner is also what celestial.md "Truth Ownership" requires.
//
//   THE DEAD GUID IS DELIBERATELY LEFT IN THE MATERIAL SLOT. It is the only
//   surviving record of the original binding; if the impostor route is
//   re-authored under the same GUID every reference re-links with zero
//   rebinding. Disabling the renderer already removes the magenta, so nulling
//   the slot would destroy information and buy nothing.
//
// NOT REPAIRED HERE, AND WHY - report-only, printed by the same run:
//   * Assets/_Project/Prefabs/WorldRuntime/PFB_FieldBeacon_Runtime.prefab:73
//     dead material 037109139403897409ccbec64138f6a3. This one is genuinely
//     player-visible (Player.prefab and Tool_BeaconDeployer_Held.prefab both
//     reference the beacon), but the only candidate on disk is
//     Mat_Tool_BeaconDeployer_Placeholder.mat. AGENTS.md bans placeholders in
//     production, so this needs authoring, not a rebind.
//   * Ocean_Crest.prefab:634 dead Texture3D 5b18df2e53d2a3f4bbd9eba32746810b
//     already has a written repair that has never been run:
//     SargassumNeutralAbyssalFlowPrefabRepair.Run. Not duplicated here.
//   * Five dead MeshCollider meshes on three GOTOVYE_PREFABY_KAMNEY rocks are
//     Technie.PhysicsCreator.RigidColliderCreator leftovers on the ROOT
//     GameObject. Each of those rocks still has an intact PHYSICS_SKIN
//     GameObject carrying a live MeshCollider, so collision is NOT lost and
//     deleting third-party components on the project's best art set is not this
//     tool's scope.
//
// This tool is explicit-invocation only: no [InitializeOnLoad], no auto-run, and
// it touches prefab assets only - never a scene.
// ============================================================================

using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEditor;
using UnityEngine;

namespace Hecton8.Editor.Authoring
{
    /// <summary>
    /// Repairs the dead sky-dome mesh reference on Sky_System.prefab and neutralises the magenta
    /// Aegir impostor renderers whose material and shader were both deleted. Idempotent: an
    /// already-correct prefab is reported and left alone.
    /// </summary>
    public static class SkyAegirHeroReferenceRepair
    {
        private const string Marker = "[H8_SKYAEGIR_REFREPAIR]";

        /// <summary>Prefab whose MeshFilter points at the deleted 1428 sky dome mesh.</summary>
        private const string SkySystemPrefabPath = "Assets/_Project/Prefabs/Sky_System.prefab";

        /// <summary>
        /// Dome mesh that 02_HECTON_WORLD.unity already pairs with Mat_HectonSky.mat, proven by a
        /// byte-level GUID probe over that binary scene. Not chosen by filename.
        /// </summary>
        private const string SkyDomeMeshPath = "Assets/_Project/Art/Models/SkyDome_Inverted.asset";

        /// <summary>Both prefabs carrying the dead Aegir impostor material GUID.</summary>
        private static readonly string[] AegirPrefabPaths =
        {
            "Assets/_Project/Prefabs/GasGiant_Aegir.prefab",
            "Assets/_Project/_PROLOGUE_CONTENT/Prefabs/GasGiant_Aegir.prefab",
        };

        /// <summary>Dead material GUID, kept for the log so the re-authoring task has the exact value.</summary>
        private const string DeadAegirMaterialGuid = "ab7b03af667690149bdc7be9a1ae023c";

        /// <summary>Dead sky dome mesh GUID, logged for the same reason.</summary>
        private const string DeadSkyDomeMeshGuid = "f75a1c4016b005f4588059e3dddbd8a1";

        /// <summary>
        /// Fraction of sampled vertices whose normal must face the dome centre before the mesh is
        /// accepted as an inside-viewed dome. Well clear of 0.5 so a mixed-orientation mesh fails.
        /// </summary>
        private const float InwardNormalMajority = 0.85f;

        [MenuItem("Hecton8/Celestial/Repair Sky And Aegir Hero References")]
        public static void RunFromMenu()
        {
            Execute();
        }

        /// <summary>Batch entry point. Exits non-zero when any repair could not be completed.</summary>
        public static void Run()
        {
            EditorApplication.Exit(Execute() ? 0 : 1);
        }

        private static bool Execute()
        {
            bool ok = RepairSkyDome();

            for (int i = 0; i < AegirPrefabPaths.Length; i++)
                ok &= NeutraliseAegirImpostor(AegirPrefabPaths[i]);

            ReportUnrepairedDeadReferences();
            return ok;
        }

        // ─────────────────────────────────────────────────────────────────────
        // Defect 1 - sky dome mesh rebind
        // ─────────────────────────────────────────────────────────────────────

        private static bool RepairSkyDome()
        {
            Mesh dome = AssetDatabase.LoadAssetAtPath<Mesh>(SkyDomeMeshPath);
            if (dome == null)
            {
                Debug.LogError(string.Format(
                    CultureInfo.InvariantCulture,
                    "{0} ABORT - dome mesh not found at '{1}'. This tool never generates geometry; " +
                    "author a dome through Tools/Blender/h8forge instead.",
                    Marker,
                    SkyDomeMeshPath));
                return false;
            }

            if (!VerifyInwardFacing(dome, out string orientationReason))
            {
                Debug.LogError(string.Format(
                    CultureInfo.InvariantCulture,
                    "{0} ABORT - '{1}' rejected as a sky dome: {2}. Binding an outward-facing dome " +
                    "would be invisible from inside and would look identical to the missing-mesh bug.",
                    Marker,
                    SkyDomeMeshPath,
                    orientationReason));
                return false;
            }

            if (AssetDatabase.LoadAssetAtPath<GameObject>(SkySystemPrefabPath) == null)
            {
                Debug.LogError(string.Format(
                    CultureInfo.InvariantCulture,
                    "{0} ABORT - prefab not found at '{1}'.",
                    Marker,
                    SkySystemPrefabPath));
                return false;
            }

            GameObject contents = null;
            try
            {
                contents = PrefabUtility.LoadPrefabContents(SkySystemPrefabPath);
            }
            catch (Exception exception) when (exception is UnityException ||
                                             exception is InvalidOperationException ||
                                             exception is ArgumentException)
            {
                Debug.LogError(string.Format(
                    CultureInfo.InvariantCulture,
                    "{0} ABORT - could not load prefab contents for '{1}': {2}: {3}",
                    Marker,
                    SkySystemPrefabPath,
                    exception.GetType().Name,
                    exception.Message));
                return false;
            }

            try
            {
                // COLD ALLOC: MeshFilter[] - one editor-only component census - owner: SkyAegirHeroReferenceRepair
                MeshFilter[] filters = contents.GetComponentsInChildren<MeshFilter>(true);
                if (filters == null || filters.Length == 0)
                {
                    Debug.LogError(string.Format(
                        CultureInfo.InvariantCulture,
                        "{0} INCONCLUSIVE - no MeshFilter inside '{1}'; the prefab shape moved and this " +
                        "repair no longer applies.",
                        Marker,
                        SkySystemPrefabPath));
                    return false;
                }

                int repaired = 0;
                int alreadyCorrect = 0;

                for (int i = 0; i < filters.Length; i++)
                {
                    MeshFilter filter = filters[i];
                    if (ReferenceEquals(filter.sharedMesh, dome))
                    {
                        alreadyCorrect++;
                        Debug.Log(string.Format(
                            CultureInfo.InvariantCulture,
                            "{0} KEPT '{1}' - already bound to the authored dome.",
                            Marker,
                            filter.name));
                        continue;
                    }

                    // A dangling reference is null-valued but keeps a non-zero instance id. That is the
                    // only signature that separates "asset was deleted underneath us" from "never set",
                    // and only the former is the defect this tool exists for.
                    SerializedObject serialized = new SerializedObject(filter);
                    SerializedProperty meshProperty = serialized.FindProperty("m_Mesh");
                    bool wasDangling = meshProperty != null &&
                                       meshProperty.objectReferenceValue == null &&
                                       meshProperty.objectReferenceEntityIdValue != 0;

                    if (filter.sharedMesh != null)
                    {
                        // Some other live mesh is bound. Not our defect - do not overwrite authored work.
                        Debug.Log(string.Format(
                            CultureInfo.InvariantCulture,
                            "{0} SKIPPED '{1}' - holds a live mesh '{2}'; only dangling references are repaired.",
                            Marker,
                            filter.name,
                            filter.sharedMesh.name));
                        continue;
                    }

                    filter.sharedMesh = dome;
                    repaired++;

                    Debug.Log(string.Format(
                        CultureInfo.InvariantCulture,
                        "{0} REPAIRED '{1}': {2} (dead guid {3}) -> '{4}' ({5} verts, {6}).",
                        Marker,
                        filter.name,
                        wasDangling ? "MISSING-REFERENCE" : "NULL",
                        DeadSkyDomeMeshGuid,
                        dome.name,
                        dome.vertexCount.ToString(CultureInfo.InvariantCulture),
                        orientationReason));
                }

                if (repaired == 0)
                {
                    Debug.Log(string.Format(
                        CultureInfo.InvariantCulture,
                        "{0} NO-CHANGE - Sky_System.prefab: {1} filter(s) already correct.",
                        Marker,
                        alreadyCorrect.ToString(CultureInfo.InvariantCulture)));
                    return true;
                }

                PrefabUtility.SaveAsPrefabAsset(contents, SkySystemPrefabPath, out bool saved);
                if (!saved)
                {
                    Debug.LogError(string.Format(
                        CultureInfo.InvariantCulture,
                        "{0} ABORT - SaveAsPrefabAsset reported failure for '{1}'; nothing was written.",
                        Marker,
                        SkySystemPrefabPath));
                    return false;
                }

                AssetDatabase.SaveAssets();

                Debug.Log(string.Format(
                    CultureInfo.InvariantCulture,
                    "{0} SAVED '{1}' - repaired={2}. PENDING VERIFICATION: this writes the PREFAB ASSET " +
                    "only. 010_TEST.unity is a binary scene that still references the dead mesh guid {3} " +
                    "directly and is NOT fixed by this; it needs its own explicit pass.",
                    Marker,
                    SkySystemPrefabPath,
                    repaired.ToString(CultureInfo.InvariantCulture),
                    DeadSkyDomeMeshGuid));

                return true;
            }
            finally
            {
                if (contents != null)
                    PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        /// <summary>
        /// Measures whether the mesh's normals face its own centre, so a dome viewed from inside is
        /// actually visible. Uses the list overloads rather than the property-copy accessors.
        /// </summary>
        private static bool VerifyInwardFacing(Mesh mesh, out string reason)
        {
            if (mesh.vertexCount <= 0)
            {
                reason = "mesh has no vertices";
                return false;
            }

            if (mesh.subMeshCount <= 0 || mesh.GetIndexCount(0) < 3)
            {
                reason = "mesh has no triangles in submesh 0";
                return false;
            }

            // COLD ALLOC: List<Vector3>[vertexCount] x2 - one editor-only orientation measurement - owner: SkyAegirHeroReferenceRepair
            List<Vector3> vertices = new List<Vector3>(mesh.vertexCount);
            List<Vector3> normals = new List<Vector3>(mesh.vertexCount);
            mesh.GetVertices(vertices);
            mesh.GetNormals(normals);

            if (normals.Count != vertices.Count || normals.Count == 0)
            {
                reason = "mesh has no per-vertex normals, so facing cannot be measured";
                return false;
            }

            Vector3 centre = mesh.bounds.center;
            int inward = 0;
            int measured = 0;

            for (int i = 0; i < vertices.Count; i++)
            {
                Vector3 radial = vertices[i] - centre;
                float radialLength = radial.magnitude;
                if (radialLength <= 1e-4f)
                    continue;

                measured++;
                if (Vector3.Dot(normals[i], radial / radialLength) < 0f)
                    inward++;
            }

            if (measured == 0)
            {
                reason = "every vertex sits on the mesh centre; not a dome";
                return false;
            }

            float ratio = (float)inward / measured;
            if (ratio < InwardNormalMajority)
            {
                reason = string.Format(
                    CultureInfo.InvariantCulture,
                    "only {0:0.#}% of {1} vertex normals face the centre, below the {2:0.#}% inward gate",
                    ratio * 100f,
                    measured.ToString(CultureInfo.InvariantCulture),
                    InwardNormalMajority * 100f);
                return false;
            }

            reason = string.Format(
                CultureInfo.InvariantCulture,
                "{0:0.#}% of {1} vertex normals face inward",
                ratio * 100f,
                measured.ToString(CultureInfo.InvariantCulture));
            return true;
        }

        // ─────────────────────────────────────────────────────────────────────
        // Defect 2 - Aegir impostor magenta
        // ─────────────────────────────────────────────────────────────────────

        private static bool NeutraliseAegirImpostor(string prefabPath)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) == null)
            {
                Debug.LogError(string.Format(
                    CultureInfo.InvariantCulture,
                    "{0} ABORT - prefab not found at '{1}'.",
                    Marker,
                    prefabPath));
                return false;
            }

            GameObject contents = null;
            try
            {
                contents = PrefabUtility.LoadPrefabContents(prefabPath);
            }
            catch (Exception exception) when (exception is UnityException ||
                                             exception is InvalidOperationException ||
                                             exception is ArgumentException)
            {
                Debug.LogError(string.Format(
                    CultureInfo.InvariantCulture,
                    "{0} ABORT - could not load prefab contents for '{1}': {2}: {3}",
                    Marker,
                    prefabPath,
                    exception.GetType().Name,
                    exception.Message));
                return false;
            }

            try
            {
                // COLD ALLOC: MeshRenderer[] - one editor-only component census - owner: SkyAegirHeroReferenceRepair
                MeshRenderer[] renderers = contents.GetComponentsInChildren<MeshRenderer>(true);
                if (renderers == null || renderers.Length == 0)
                {
                    Debug.LogError(string.Format(
                        CultureInfo.InvariantCulture,
                        "{0} INCONCLUSIVE - no MeshRenderer inside '{1}'; the prefab shape moved and this " +
                        "repair no longer applies.",
                        Marker,
                        prefabPath));
                    return false;
                }

                int disabled = 0;
                int alreadyHandled = 0;
                int healthy = 0;

                for (int i = 0; i < renderers.Length; i++)
                {
                    MeshRenderer renderer = renderers[i];
                    if (!HasDanglingMaterialSlot(renderer, out int deadSlotCount))
                    {
                        healthy++;
                        continue;
                    }

                    if (!renderer.enabled)
                    {
                        alreadyHandled++;
                        Debug.Log(string.Format(
                            CultureInfo.InvariantCulture,
                            "{0} KEPT '{1}' - already disabled; {2} dead material slot(s) preserved for re-authoring.",
                            Marker,
                            renderer.name,
                            deadSlotCount.ToString(CultureInfo.InvariantCulture)));
                        continue;
                    }

                    renderer.enabled = false;
                    disabled++;

                    Debug.LogWarning(string.Format(
                        CultureInfo.InvariantCulture,
                        "{0} AEGIR_IMPOSTOR_ROUTE_NEEDS_AUTHORING - disabled MeshRenderer on '{1}' in '{2}'. " +
                        "{3} dead material slot(s) (guid {4}) left intact on purpose. Both the material " +
                        "MAT_AegirGasGiant_Impostor_1428.mat AND its only shader " +
                        "H8_AegirGasGiantImpostor_1428.shader were deleted by commit 621403ad5, so no rebind " +
                        "is possible; the near-field impostor must be re-authored (shader + material + " +
                        "horizon-veil term) before this renderer is switched back on. Aegir itself is still " +
                        "drawn analytically by Hecton_AegirSky.shader DrawAegir().",
                        Marker,
                        renderer.name,
                        prefabPath,
                        deadSlotCount.ToString(CultureInfo.InvariantCulture),
                        DeadAegirMaterialGuid));
                }

                if (disabled == 0)
                {
                    Debug.Log(string.Format(
                        CultureInfo.InvariantCulture,
                        "{0} NO-CHANGE - '{1}': alreadyDisabled={2} healthy={3}.",
                        Marker,
                        prefabPath,
                        alreadyHandled.ToString(CultureInfo.InvariantCulture),
                        healthy.ToString(CultureInfo.InvariantCulture)));
                    return true;
                }

                PrefabUtility.SaveAsPrefabAsset(contents, prefabPath, out bool saved);
                if (!saved)
                {
                    Debug.LogError(string.Format(
                        CultureInfo.InvariantCulture,
                        "{0} ABORT - SaveAsPrefabAsset reported failure for '{1}'; nothing was written.",
                        Marker,
                        prefabPath));
                    return false;
                }

                AssetDatabase.SaveAssets();

                Debug.Log(string.Format(
                    CultureInfo.InvariantCulture,
                    "{0} SAVED '{1}' - disabled={2}. PENDING VERIFICATION: 01_ORBIT.unity instantiates this " +
                    "prefab and may hold its own m_Enabled override that saving the prefab asset does not " +
                    "touch; confirm the effective value on the scene instance.",
                    Marker,
                    prefabPath,
                    disabled.ToString(CultureInfo.InvariantCulture)));

                return true;
            }
            finally
            {
                if (contents != null)
                    PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        /// <summary>
        /// True when any material slot holds a dangling reference - null value with a non-zero instance
        /// id, Unity's signature for an asset deleted underneath the prefab. An empty slot that never
        /// held anything is not counted, because that is authoring intent rather than rot.
        /// </summary>
        private static bool HasDanglingMaterialSlot(MeshRenderer renderer, out int deadSlotCount)
        {
            deadSlotCount = 0;

            SerializedObject serialized = new SerializedObject(renderer);
            SerializedProperty materials = serialized.FindProperty("m_Materials");
            if (materials == null || !materials.isArray)
                return false;

            for (int i = 0; i < materials.arraySize; i++)
            {
                SerializedProperty slot = materials.GetArrayElementAtIndex(i);
                if (slot == null)
                    continue;

                if (slot.objectReferenceValue == null && slot.objectReferenceEntityIdValue != 0)
                    deadSlotCount++;
            }

            return deadSlotCount > 0;
        }

        // ─────────────────────────────────────────────────────────────────────
        // Report-only surface
        // ─────────────────────────────────────────────────────────────────────

        private static void ReportUnrepairedDeadReferences()
        {
            Debug.LogWarning(string.Format(
                CultureInfo.InvariantCulture,
                "{0} STILL-DEAD, NOT FIXED BY THIS TOOL:\n" +
                "  1. PFB_FieldBeacon_Runtime.prefab:73 material guid 037109139403897409ccbec64138f6a3 - " +
                "NEEDS AUTHORING. Player-visible (deployable beacon) but the only candidate on disk is " +
                "Mat_Tool_BeaconDeployer_Placeholder.mat and AGENTS.md bans placeholders.\n" +
                "  2. Ocean_Crest.prefab:634 Texture3D guid 5b18df2e53d2a3f4bbd9eba32746810b - ALREADY HAS " +
                "A REPAIR THAT WAS NEVER RUN: Hecton8/VFX/Repair Sargassum Neutral Abyssal Flow Reference.\n" +
                "  3. 010_TEST.unity references the dead sky dome mesh guid {1} directly (binary scene, " +
                "found by byte probe, not by text search) - needs an explicit scene pass.\n" +
                "  4. Tools/ValidateAegirGasGiantSourceContract.py:23,33,47 still hard-code the deleted " +
                "material and shader as CANONICAL, so that validator is red and asserts assets that no " +
                "longer exist. It must be rewritten to the surviving sky-shader route.\n" +
                "  5. Five MeshCollider meshes on three GOTOVYE_PREFABY_KAMNEY rocks are dead, but they are " +
                "Technie.PhysicsCreator leftovers on the ROOT GameObject and every one of those rocks still " +
                "has a live PHYSICS_SKIN MeshCollider - collision is NOT lost. Cruft, not a gameplay bug.",
                Marker,
                DeadSkyDomeMeshGuid));
        }
    }
}
