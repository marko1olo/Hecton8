// ============================================================================
// HECTON-8 - SargassumNeutralAbyssalFlowPrefabRepair.cs
//
// Repairs the DANGLING serialized reference that makes SargassumMicroFaunaBoids
// log "Missing authored neutral abyssal-flow Texture3D. Runtime texture
// fallback generation is forbidden." twice per run (once from Awake, once from
// OnEnable - SargassumMicroFaunaBoids.cs:1962 and :1988 both call EnsureBuffers,
// which aborts at the guard before allocating any of its managed caches or vault
// storage, so the swarm runs inert and throws nothing).
//
// THE FIELD IS NOT UNASSIGNED - IT POINTS AT A DELETED ASSET:
//   Assets/_Project/Prefabs/Ocean_Crest.prefab:634
//     neutralAbyssalFlowTexture: {fileID: 11700000,
//                                 guid: 5b18df2e53d2a3f4bbd9eba32746810b,
//                                 type: 2}
//   That GUID resolved to
//     Assets/_Project/Art/TEXTURES/RuntimeFallbacks/TX_H8NeutralAbyssalFlow_1x1x1_1428.asset
//   (Docs/AssetAudit/ASSET_GUID_REFERENCE_MATRIX_20260605.csv:2522, which also
//   records the prefab as its only referrer), and commit 621403ad5
//   "refactor(modding,sky): data-only override mod system & 1428 file cleanup"
//   (2026-06-15) deleted both the .asset and its .meta. The RuntimeFallbacks
//   folder is still on disk and empty. A dangling GUID deserializes to null, so
//   the component's own null guard fires.
//
// WHY IT RE-POINTS AT THE 1728 ASSET INSTEAD OF RESURRECTING THE 1428 ONE:
//   The deleted asset was RGBA32 (m_Format: 8) with four zero bytes. RGBA32 is
//   UNORM, and both shaders that read this volume treat .xyz as a raw signed
//   world-space velocity with no unbias step
//   (Hecton_MarineSnow.compute:394, SargassumMicroFaunaBoids.compute:440), so an
//   RGBA32 flow volume physically cannot carry a negative component. The live
//   asset at the 1728 path is RGBAHalf (m_Format: 48 == R16G16B16A16_SFloat),
//   the TextureFormat twin of what HectonFluidEngine actually publishes, and it
//   is the same asset HectonMarineSnowRenderer.cs:49 already binds. One authored
//   neutral volume for both consumers beats restoring a worse duplicate.
//
// SCOPE, HONESTLY STATED:
//   This repairs the PREFAB ASSET only. 02_HECTON_WORLD.unity is a BINARY scene,
//   so a prefab instance inside it may carry its own override on this field that
//   no text search can see and that saving the prefab asset would not touch.
//   After running this, run the existing probe
//     Hecton8/Diagnostics/Boid Authored Assets  (H8_BoidAuthoredAssetProbe)
//   which opens the world scene and prints the EFFECTIVE value of
//   neutralAbyssalFlowTexture on the live instance. Until that prints non-NULL
//   this repair is PENDING VERIFICATION.
//
//   No texture is created here and none is created at runtime. The refusal to
//   fabricate is deliberate project policy; the defect was always a wiring gap.
// ============================================================================

using System;
using System.Globalization;
using Hecton8.World;
using UnityEditor;
using UnityEngine;

namespace Hecton8.Editor.Authoring
{
    /// <summary>
    /// Re-points the private serialized <c>neutralAbyssalFlowTexture</c> field on every
    /// <see cref="SargassumMicroFaunaBoids"/> inside Ocean_Crest.prefab at the authored 1728 zero-flow
    /// volume. Idempotent: an already-correct reference is reported and left alone.
    /// </summary>
    public static class SargassumNeutralAbyssalFlowPrefabRepair
    {
        private const string Marker = "[H8_SARGASSUM_FLOWREF]";

        /// <summary>Only prefab that references the component (GUID census over Assets/).</summary>
        private const string BoidPrefabPath = "Assets/_Project/Prefabs/Ocean_Crest.prefab";

        /// <summary>
        /// Byte-identical to HectonMarineSnowRenderer.cs:49 and to the constant added to
        /// SargassumMicroFaunaBoids.cs, so all three routes resolve one asset.
        /// </summary>
        private const string NeutralAbyssalFlowAssetPath =
            "Assets/_Project/Art/Textures/VFX/ParticulateFlipbooks1728/TX_MarineSnow_EmptyAbyssalFlow_1x1x1.asset";

        /// <summary>Serialized field name on the component. Renaming it must break this loudly.</summary>
        private const string FieldName = "neutralAbyssalFlowTexture";

        [MenuItem("Hecton8/VFX/Repair Sargassum Neutral Abyssal Flow Reference")]
        public static void RunFromMenu()
        {
            Execute();
        }

        /// <summary>Batch entry point. Exits non-zero when the repair could not be completed.</summary>
        public static void Run()
        {
            EditorApplication.Exit(Execute() ? 0 : 1);
        }

        private static bool Execute()
        {
            Texture3D neutralVolume = AssetDatabase.LoadAssetAtPath<Texture3D>(NeutralAbyssalFlowAssetPath);
            if (neutralVolume == null)
            {
                // Never synthesise one. If the authored asset is absent the correct action is to run
                // Hecton8/VFX/Generate Marine Snow Neutral Volumes, not to invent data here.
                Debug.LogError(string.Format(
                    CultureInfo.InvariantCulture,
                    "{0} ABORT - authored neutral volume not found at '{1}'. Run " +
                    "Hecton8/VFX/Generate Marine Snow Neutral Volumes first; this tool never creates one.",
                    Marker,
                    NeutralAbyssalFlowAssetPath));
                return false;
            }

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
                // COLD ALLOC: SargassumMicroFaunaBoids[] - one editor-only component census - owner: SargassumNeutralAbyssalFlowPrefabRepair
                SargassumMicroFaunaBoids[] components = contents.GetComponentsInChildren<SargassumMicroFaunaBoids>(true);
                if (components == null || components.Length == 0)
                {
                    // Not a pass. The runtime log proves this component boots from this prefab, so finding
                    // none here means the census is wrong and nothing was proven.
                    Debug.LogError(string.Format(
                        CultureInfo.InvariantCulture,
                        "{0} INCONCLUSIVE - no SargassumMicroFaunaBoids inside '{1}'.",
                        Marker,
                        BoidPrefabPath));
                    return false;
                }

                int repaired = 0;
                int alreadyCorrect = 0;
                int fieldMissing = 0;

                for (int i = 0; i < components.Length; i++)
                {
                    SargassumMicroFaunaBoids component = components[i];
                    SerializedObject serialized = new SerializedObject(component);
                    SerializedProperty property = serialized.FindProperty(FieldName);
                    if (property == null)
                    {
                        fieldMissing++;
                        Debug.LogError(string.Format(
                            CultureInfo.InvariantCulture,
                            "{0} field '{1}' not found on '{2}' - renamed or removed; this repair no longer applies.",
                            Marker,
                            FieldName,
                            component.name));
                        continue;
                    }

                    if (ReferenceEquals(property.objectReferenceValue, neutralVolume))
                    {
                        alreadyCorrect++;
                        Debug.Log(string.Format(
                            CultureInfo.InvariantCulture,
                            "{0} KEPT '{1}' - already points at the authored volume.",
                            Marker,
                            component.name));
                        continue;
                    }

                    // objectReferenceValue == null with a non-zero instance id is Unity's signature for a
                    // missing reference rather than an empty field. Both are repaired; the distinction is
                    // logged because only one of them means an asset was deleted underneath the prefab.
                    bool wasDangling = property.objectReferenceValue == null &&
                                       property.objectReferenceInstanceIDValue != 0;
                    string previous = property.objectReferenceValue != null
                        ? property.objectReferenceValue.name
                        : (wasDangling ? "MISSING-REFERENCE" : "NULL");

                    property.objectReferenceValue = neutralVolume;
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                    repaired++;

                    Debug.Log(string.Format(
                        CultureInfo.InvariantCulture,
                        "{0} REPAIRED '{1}': {2} -> {3}",
                        Marker,
                        component.name,
                        previous,
                        neutralVolume.name));
                }

                if (fieldMissing > 0)
                    return false;

                if (repaired == 0)
                {
                    Debug.Log(string.Format(
                        CultureInfo.InvariantCulture,
                        "{0} NO-CHANGE - {1} component(s) already correct.",
                        Marker,
                        alreadyCorrect));
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
                    "{0} SAVED '{1}' - repaired={2} kept={3}. PENDING VERIFICATION: 02_HECTON_WORLD.unity " +
                    "is binary and may hold a prefab-instance override on '{4}' that this does not touch. " +
                    "Run Hecton8/Diagnostics/Boid Authored Assets to read the effective scene value.",
                    Marker,
                    BoidPrefabPath,
                    repaired,
                    alreadyCorrect,
                    FieldName));

                return true;
            }
            finally
            {
                if (contents != null)
                    PrefabUtility.UnloadPrefabContents(contents);
            }
        }
    }
}
