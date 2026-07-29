using System.Collections.Generic;
using Hecton8.Construction;
using Hecton8.Gameplay;
using UnityEditor;
using UnityEngine;

namespace Hecton8.Editor
{
    /// <summary>
    /// Removes MeshColliders from BaseModule prefabs, guarantees primitive collider coverage, and binds
    /// those primitives onto <see cref="BaseModuleNavModifier"/> for voxel-nav carving.
    /// </summary>
    /// <remarks>
    /// WHY THE OUTPUT EXISTS. This gate previously printed NOTHING - no per-prefab line, no summary, no
    /// result. A silent gate is indistinguishable from a gate that is not running, and on this project that
    /// ambiguity has repeatedly been resolved the optimistic way. Every run now ends with a single
    /// fixed-shape ASCII token, <c>H8_BASEMODULE_INTEGRITY_RESULT=</c>, chosen because build output on this
    /// host is localised to Russian: grepping for "Error" finds nothing on a failing build, so a gate that
    /// relies on locale-dependent words is unreadable in CI. Grep the token, not a word.
    ///
    /// WHY THE SAVE IS CONDITIONAL NOW. The previous body ended with an unconditional <c>dirty = true;</c>
    /// - a plain assignment, not <c>|=</c> - which overwrote the real change computation above it and made
    /// that computation dead code. <c>PrefabUtility.SaveAsPrefabAsset</c> therefore ran on every
    /// BaseModule-bearing prefab on every invocation. That is not free: a rewrite can reassign the root
    /// <c>fileID</c>, and every scene that references this prefab binds to that fileID, so a gate meant to
    /// protect production assets was itself the churn risk. The save is now gated on a measured difference,
    /// and the root fileID is read before and after any write and compared out loud.
    ///
    /// COVERAGE IS NOT COMPLETE, AND NOW SAYS SO. Prefabs without a <see cref="BaseModule"/> component are
    /// skipped, so a clean result here is NOT a statement about them. The skip count is reported explicitly
    /// rather than left implied. The complementary check for final prefabs that SHOULD carry a BaseModule
    /// lives in <c>ConstructionFinalPrefabModuleCoverageGate</c> and is deliberately not duplicated here.
    /// </remarks>
    public static class BaseModulePrefabIntegrityEnforcer
    {
        private const string FinalPrefabFolder = "Assets/_Project/Prefabs/Construction/Final";
        private const string Marker = "[H8_BASEMODULE_INTEGRITY]";

        /// <summary>Fixed ASCII result token. Locale-independent on purpose - grep this, not a word.</summary>
        private const string ResultToken = "H8_BASEMODULE_INTEGRITY_RESULT=";

        private const string ObstacleBoxesField = "obstacleBoxes";
        private const string ObstacleCapsulesField = "obstacleCapsules";

        /// <summary>
        /// Reports what would change and writes nothing. Safe at any time.
        /// </summary>
        [MenuItem("Hecton8/Validation/Audit Base Module Prefab Integrity (REPORT ONLY)", priority = 215)]
        public static void AuditBaseModulePrefabIntegrity()
        {
            Execute(applyChanges: false);
        }

        [MenuItem("Hecton8/Validation/Enforce Base Module Prefab Integrity", priority = 216)]
        public static void EnforceBaseModulePrefabIntegrity()
        {
            Execute(applyChanges: true);
        }

        /// <summary>
        /// Batch REPORT-ONLY entry. Never writes: AGENTS.md `Sandbox Firewall Rule` (AGENTS.md:126) forbids
        /// automated runners from calling <c>PrefabUtility.SaveAsPrefabAsset</c> on production assets, so the
        /// writing path stays behind the deliberate human MenuItem above. Exits non-zero when any prefab
        /// still needs repair, so a queued gate can fail on it.
        /// </summary>
        public static void AuditFromCommandLine()
        {
            EditorApplication.Exit(Execute(applyChanges: false) ? 0 : 1);
        }

        private static bool Execute(bool applyChanges)
        {
            string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { FinalPrefabFolder });

            int scanned = 0;
            int withBaseModule = 0;
            int skippedNoBaseModule = 0;
            int unreadable = 0;
            int pendingRepair = 0;
            int written = 0;
            int rootFileIdChanged = 0;

            for (int i = 0; i < prefabGuids.Length; i++)
            {
                string prefabPath = AssetDatabase.GUIDToAssetPath(prefabGuids[i]);
                scanned++;

                GameObject prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);
                if (prefabRoot == null)
                {
                    unreadable++;
                    Debug.LogError(Marker + " UNREADABLE '" + prefabPath +
                                   "' - LoadPrefabContents returned null. Nothing was proven about this prefab.");
                    continue;
                }

                try
                {
                    if (!prefabRoot.TryGetComponent(out BaseModule baseModule))
                    {
                        skippedNoBaseModule++;
                        Debug.Log(Marker + " SKIP '" + prefabPath +
                                  "' - no BaseModule component, so this gate makes NO claim about it. " +
                                  "Whether it should have one is ConstructionFinalPrefabModuleCoverageGate's question.");
                        continue;
                    }

                    withBaseModule++;

                    int meshCollidersRemoved = RemoveMeshColliders(prefabRoot);
                    bool coverageChanged = EnsurePrimitiveColliderCoverage(
                        prefabRoot,
                        out BoxCollider[] boxes,
                        out CapsuleCollider[] capsules,
                        out bool fallbackBoxAdded);

                    bool navModifierAdded = false;
                    if (!prefabRoot.TryGetComponent(out BaseModuleNavModifier navModifier))
                    {
                        navModifier = prefabRoot.AddComponent<BaseModuleNavModifier>();
                        navModifierAdded = true;
                    }

                    // Compare BEFORE applying, or the comparison always reports equal.
                    bool navSourcesChanged = navModifierAdded ||
                                             NavSourcesDiffer(navModifier, boxes, capsules);
                    if (navSourcesChanged)
                    {
                        navModifier.ConfigureColliderSources(boxes, capsules);
                        EditorUtility.SetDirty(navModifier);
                    }

                    bool dirty = meshCollidersRemoved > 0 || coverageChanged || navSourcesChanged;

                    Debug.Log(Marker + " SCAN '" + prefabPath +
                              "' meshCollidersRemoved=" + meshCollidersRemoved.ToString() +
                              " fallbackBoxAdded=" + (fallbackBoxAdded ? "1" : "0") +
                              " navModifierAdded=" + (navModifierAdded ? "1" : "0") +
                              " navSourcesChanged=" + (navSourcesChanged ? "1" : "0") +
                              " boxes=" + boxes.Length.ToString() +
                              " capsules=" + capsules.Length.ToString() +
                              " verdict=" + (dirty ? (applyChanges ? "WRITING" : "NEEDS-REPAIR") : "CLEAN"));

                    if (!dirty)
                        continue;

                    if (!applyChanges)
                    {
                        pendingRepair++;
                        continue;
                    }

                    long rootFileIdBefore = ReadRootFileId(prefabPath);

                    EditorUtility.SetDirty(prefabRoot);
                    PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);
                    written++;

                    long rootFileIdAfter = ReadRootFileId(prefabPath);
                    if (rootFileIdBefore != rootFileIdAfter)
                    {
                        rootFileIdChanged++;
                        Debug.LogError(Marker + " ROOT FILEID CHANGED '" + prefabPath + "' " +
                                       rootFileIdBefore.ToString() + " -> " + rootFileIdAfter.ToString() +
                                       ". Every scene and prefab that referenced this root now points at an id " +
                                       "that no longer exists, and four scenes in this project are BINARY so a " +
                                       "text search cannot tell you which. Treat this as a break, not a warning.");
                    }
                    else
                    {
                        Debug.Log(Marker + " WROTE '" + prefabPath + "' rootFileId " +
                                  rootFileIdBefore.ToString() + " -> " + rootFileIdAfter.ToString() +
                                  " (unchanged, so existing references still resolve).");
                    }
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(prefabRoot);
                }
            }

            if (applyChanges && written > 0)
            {
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }

            bool passed = unreadable == 0 && rootFileIdChanged == 0 && pendingRepair == 0;
            string summary = ResultToken + (passed ? "PASS" : "FAILED") +
                             " mode=" + (applyChanges ? "ENFORCE" : "REPORT-ONLY") +
                             " folder=" + FinalPrefabFolder +
                             " scanned=" + scanned.ToString() +
                             " withBaseModule=" + withBaseModule.ToString() +
                             " skippedNoBaseModule=" + skippedNoBaseModule.ToString() +
                             " unreadable=" + unreadable.ToString() +
                             " needsRepair=" + pendingRepair.ToString() +
                             " written=" + written.ToString() +
                             " rootFileIdChanged=" + rootFileIdChanged.ToString();

            if (passed)
                Debug.Log(summary);
            else
                Debug.LogError(summary);

            if (skippedNoBaseModule > 0)
            {
                Debug.LogWarning(Marker + " COVERAGE NOTICE - " + skippedNoBaseModule.ToString() + " of " +
                                 scanned.ToString() + " prefab(s) under '" + FinalPrefabFolder +
                                 "' carry no BaseModule and were skipped. A PASS above is a statement about " +
                                 withBaseModule.ToString() + " prefab(s), not about the folder.");
            }

            return passed;
        }

        /// <summary>
        /// True when the serialized nav sources differ from what would be written. Read through
        /// <see cref="SerializedObject"/> because the backing arrays are private <c>[SerializeField]</c>
        /// state; widening them to public just to compare would be a public API change for a diagnostic.
        /// </summary>
        private static bool NavSourcesDiffer(
            BaseModuleNavModifier navModifier,
            BoxCollider[] boxes,
            CapsuleCollider[] capsules)
        {
            SerializedObject serialized = new SerializedObject(navModifier);
            return ArrayDiffers(serialized.FindProperty(ObstacleBoxesField), boxes) ||
                   ArrayDiffers(serialized.FindProperty(ObstacleCapsulesField), capsules);
        }

        /// <summary>
        /// Fails toward "changed" whenever equality cannot be PROVEN - a renamed or missing field must not
        /// silently report a clean prefab.
        /// </summary>
        private static bool ArrayDiffers(SerializedProperty arrayProperty, Component[] desired)
        {
            if (arrayProperty == null || !arrayProperty.isArray)
                return true;

            int desiredLength = desired != null ? desired.Length : 0;
            if (arrayProperty.arraySize != desiredLength)
                return true;

            for (int i = 0; i < desiredLength; i++)
            {
                if (!ReferenceEquals(arrayProperty.GetArrayElementAtIndex(i).objectReferenceValue, desired[i]))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Local file id of the prefab asset's root, or 0 when it cannot be resolved. This is the id scenes
        /// bind to, which is why it is worth printing on both sides of a write.
        /// </summary>
        private static long ReadRootFileId(string prefabPath)
        {
            GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (asset == null)
                return 0L;

            return AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset, out string _, out long localId)
                ? localId
                : 0L;
        }

        /// <summary>Number of MeshColliders destroyed, so the caller can report it instead of a bare bool.</summary>
        private static int RemoveMeshColliders(GameObject prefabRoot)
        {
            int removed = 0;
            MeshCollider[] meshColliders = prefabRoot.GetComponentsInChildren<MeshCollider>(true);
            for (int i = 0; i < meshColliders.Length; i++)
            {
                if (meshColliders[i] == null)
                    continue;

                Object.DestroyImmediate(meshColliders[i], true);
                removed++;
            }

            return removed;
        }

        private static bool EnsurePrimitiveColliderCoverage(
            GameObject prefabRoot,
            out BoxCollider[] boxes,
            out CapsuleCollider[] capsules,
            out bool fallbackBoxAdded)
        {
            bool dirty = false;
            fallbackBoxAdded = false;
            List<BoxCollider> boxList = new List<BoxCollider>(8); // COLD ALLOC: List<BoxCollider>[8] - prefab primitive-collider staging during editor validation - owner: BaseModulePrefabIntegrityEnforcer
            List<CapsuleCollider> capsuleList = new List<CapsuleCollider>(8); // COLD ALLOC: List<CapsuleCollider>[8] - prefab primitive-collider staging during editor validation - owner: BaseModulePrefabIntegrityEnforcer

            CollectPrimitiveColliders(prefabRoot, boxList, capsuleList);
            if (boxList.Count == 0 && capsuleList.Count == 0 && TryBuildFallbackBounds(prefabRoot, out Bounds bounds))
            {
                if (!prefabRoot.TryGetComponent(out BoxCollider fallback))
                {
                    fallback = prefabRoot.AddComponent<BoxCollider>();
                    fallbackBoxAdded = true;
                }

                Vector3 desiredCenter = prefabRoot.transform.InverseTransformPoint(bounds.center);
                Vector3 desiredSize = bounds.size;

                // Only count this as a change when a value actually moves. Re-asserting the same box on
                // every run is what made this gate rewrite every prefab it touched.
                if (fallbackBoxAdded ||
                    fallback.isTrigger ||
                    fallback.center != desiredCenter ||
                    fallback.size != desiredSize)
                {
                    fallback.isTrigger = false;
                    fallback.center = desiredCenter;
                    fallback.size = desiredSize;
                    dirty = true;
                }

                boxList.Clear();
                capsuleList.Clear();
                CollectPrimitiveColliders(prefabRoot, boxList, capsuleList);
            }

            boxes = boxList.ToArray();
            capsules = capsuleList.ToArray();
            return dirty;
        }

        private static void CollectPrimitiveColliders(GameObject prefabRoot, List<BoxCollider> boxList, List<CapsuleCollider> capsuleList)
        {
            BoxCollider[] allBoxes = prefabRoot.GetComponentsInChildren<BoxCollider>(true);
            for (int i = 0; i < allBoxes.Length; i++)
            {
                BoxCollider box = allBoxes[i];
                if (box != null && !box.isTrigger)
                    boxList.Add(box);
            }

            CapsuleCollider[] allCapsules = prefabRoot.GetComponentsInChildren<CapsuleCollider>(true);
            for (int i = 0; i < allCapsules.Length; i++)
            {
                CapsuleCollider capsule = allCapsules[i];
                if (capsule != null && !capsule.isTrigger)
                    capsuleList.Add(capsule);
            }
        }

        private static bool TryBuildFallbackBounds(GameObject prefabRoot, out Bounds bounds)
        {
            bounds = default;
            Renderer[] renderers = prefabRoot.GetComponentsInChildren<Renderer>(true);
            bool initialized = false;
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null)
                    continue;

                if (!initialized)
                {
                    bounds = renderer.bounds;
                    initialized = true;
                    continue;
                }

                bounds.Encapsulate(renderer.bounds);
            }

            return initialized;
        }
    }
}
