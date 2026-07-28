// ============================================================================
// HECTON-8 - H8_NarrativeSceneWiring.cs
//
// Non-interactive, batchmode-safe wiring of the three narrative runtime owners into the
// production world scene, plus a read-only gate that reports the same fact without mutating
// anything.
//
// WHY THIS FILE EXISTS
//   ContentSanityValidator.ValidateFirstHourRuntimeSceneOwners() requires
//   Assets/_Project/Scenes/02_HECTON_WORLD.unity to serialize three scripts:
//     Assets/_Project/Scripts/Bootstrap/HectonLoreSystemsRoot.cs
//     Assets/_Project/Scripts/Quest/QuestManager.cs
//     Assets/_Project/Scripts/Gameplay/FirstHourDirector.cs
//   HectonLoreSystemsRoot is a plain MonoBehaviour (Awake/OnEnable, no
//   RuntimeInitializeOnLoadMethod), so with no scene or prefab reference the lore, quest and
//   first-hour spine cannot run at all.
//   The two pre-existing setup paths do not close that gap unattended:
//     * HectonLoreSceneSetupEditor.SetupLoreSystemsInScene() calls EditorUtility.DisplayDialog
//       and sets Selection, so it cannot run under -batchmode, and its dialog tells the operator
//       to finish the wiring by hand.
//     * LoreSystemsBootstrapUtility.BootstrapProductionWorldSceneBatch() is non-interactive but
//       resolves its host objects with GameObject.Find, which never sees INACTIVE objects, and it
//       never verifies afterwards that the three required owners are actually serialized - so it
//       can log success while the validator gate still fails.
//   This file is additive: it does not modify either of those, it converges on the same
//   "--- SYSTEMS ---/LoreSystems" topology they use, and it re-uses the setup entry points
//   HectonLoreSystemsRoot already exposes (SetupAllSystems / ValidateSystems).
//
// EXACT COMMAND LINE - wire the scene and save it (mutating):
//   "C:\Program Files\Unity\Hub\Editor\6000.5.0f1\Editor\Unity.exe" -batchmode -quit ^
//     -projectPath "C:\hades\Hecton8" ^
//     -logFile "C:\hades\Hecton8\Logs\h8_narrative_wire.log" ^
//     -executeMethod Hecton8.Editor.H8_NarrativeSceneWiring.WireProductionWorldSceneBatch
//
// EXACT COMMAND LINE - gate only, mutates nothing (read-only):
//   "C:\Program Files\Unity\Hub\Editor\6000.5.0f1\Editor\Unity.exe" -batchmode -quit ^
//     -projectPath "C:\hades\Hecton8" ^
//     -logFile "C:\hades\Hecton8\Logs\h8_narrative_verify.log" ^
//     -executeMethod Hecton8.Editor.H8_NarrativeSceneWiring.VerifyProductionWorldSceneBatch
//
// Same two commands in git-bash form:
//   "/c/Program Files/Unity/Hub/Editor/6000.5.0f1/Editor/Unity.exe" -batchmode -quit \
//     -projectPath "C:/hades/Hecton8" \
//     -logFile "C:/hades/Hecton8/Logs/h8_narrative_wire.log" \
//     -executeMethod Hecton8.Editor.H8_NarrativeSceneWiring.WireProductionWorldSceneBatch
//   "/c/Program Files/Unity/Hub/Editor/6000.5.0f1/Editor/Unity.exe" -batchmode -quit \
//     -projectPath "C:/hades/Hecton8" \
//     -logFile "C:/hades/Hecton8/Logs/h8_narrative_verify.log" \
//     -executeMethod Hecton8.Editor.H8_NarrativeSceneWiring.VerifyProductionWorldSceneBatch
//
// -nographics is deliberately absent: the project bans it, and it is not needed here.
// Run the AGENTS.md process preflight first - one Unity per project lock.
//
// READING THE RESULT
//   Every line is prefixed H8NARRATIVEWIRE. Grep the log for "H8NARRATIVEWIRE RESULT=".
//   Exit code 0  - all three owners are hosted in the scene (RESULT=OK).
//   Exit code 1  - failure; the reason is on the RESULT=FAIL line. Failure calls
//                  EditorApplication.Exit(1), which overrides the implicit 0 that -quit would
//                  otherwise return.
//   No dialogs, no Selection, no EditorUtility.DisplayDialog anywhere in this file.
//
// SCOPE NOTE: this wires SCENE AUTHORING. It does not claim runtime proof - that needs a play
// session or a headless probe that observes the quest spine actually transitioning.
// ============================================================================

#if UNITY_EDITOR
using Hecton8.Bootstrap;
using Hecton8.EditorTools.Diagnostics;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hecton8.Editor
{
    /// <summary>
    /// Batchmode-safe wiring and verification of the first-hour narrative runtime owners in
    /// the production world scene. Editor-only, cold path: allocation here is free.
    /// </summary>
    public static class H8_NarrativeSceneWiring
    {
        // Kept byte-identical to ContentSanityValidator's constants on purpose: this utility has
        // to answer the same question the gate asks, against the same paths.
        private const string ProductionWorldScenePath = "Assets/_Project/Scenes/02_HECTON_WORLD.unity";
        private const string LoreSystemsRootScriptPath = "Assets/_Project/Scripts/Bootstrap/HectonLoreSystemsRoot.cs";
        private const string QuestManagerScriptPath = "Assets/_Project/Scripts/Quest/QuestManager.cs";
        private const string FirstHourDirectorScriptPath = "Assets/_Project/Scripts/Gameplay/FirstHourDirector.cs";

        // Topology shared with LoreSystemsBootstrapUtility so the two tools converge on ONE root
        // instead of each creating its own. HectonLoreSystemsRoot resolves its systems with
        // transform.Find(name), so the child names below are a contract, not cosmetics.
        private const string SystemsRootName = "--- SYSTEMS ---";
        private const string LoreSystemsName = "LoreSystems";
        private const string QuestManagerChildName = "QuestManager";
        private const string FirstHourDirectorChildName = "FirstHourDirector";

        private const string QuestDataFolder = "Assets/_Project/Data/Lore/Quests";
        private const string AllQuestsFieldName = "allQuests";
        private const string QuestDataFilter = "t:QuestData";

        private const string LogPrefix = "H8NARRATIVEWIRE ";
        private const int ExternalSystemReportCap = 8;

        // FirstHourDirector's serialized defaults already name these four quests, so wiring it
        // needs no object references - only existence plus a populated QuestManager registry.
        // Reported as a warning when an id is missing so a silent dead spine cannot pass as OK.
        private static readonly string[] RequiredFirstHourQuestIds =
        {
            "quest_arrival",
            "quest_starter_drill",
            "quest_copper_sample",
            "quest_first_breath"
        };

        // Type NAMES, not types, on purpose: naming all 17 system classes would bind this editor
        // assembly to every assembly that declares one. The census below only needs to know
        // whether a lore system already lives OUTSIDE the lore root, and a name answers that.
        // Source of truth: HectonLoreSystemsRoot.SetupAllSystems().
        private static readonly string[] LoreSystemTypeNames =
        {
            "AudioLogSystem",
            "LoreDatabaseManager",
            "QuestManager",
            "AtlasSignalSystem",
            "SuitUpgradeManager",
            "DepthZoneDirector",
            "EclipseGameplaySystem",
            "SpectrumSystem",
            "AtlasSignalDecoder",
            "HectonBiolumController",
            "Atlas6DirectiveSystem",
            "CorporateOrderSystem",
            "RandomEventSystem",
            "FirstHourDirector",
            "SoundscapeSystem",
            "BaseIntegrityHUD",
            "EndingSystem"
        };

        // ------------------------------------------------------------------------------------
        // Entry points
        // ------------------------------------------------------------------------------------

        /// <summary>
        /// -executeMethod entry point. Opens the production world scene, ensures the three
        /// narrative owners exist, saves, verifies, and exits non-zero on failure.
        /// </summary>
        public static void WireProductionWorldSceneBatch()
        {
            RunGuarded(WireInternal, true);
        }

        /// <summary>
        /// -executeMethod entry point. Reports whether all three owners are serialized in the
        /// production world scene. Mutates nothing: no scene is opened, no asset is written.
        /// Exits non-zero when an owner is missing, so it can be used as a gate.
        /// </summary>
        public static void VerifyProductionWorldSceneBatch()
        {
            RunGuarded(VerifyInternal, true);
        }

        [MenuItem("Hecton8/Lore/Wire Narrative Owners Into Production Scene", false, 40)]
        public static void WireProductionWorldSceneMenu()
        {
            RunGuarded(WireInternal, false);
        }

        [MenuItem("Hecton8/Lore/Verify Narrative Owners In Production Scene (Report Only)", false, 41)]
        public static void VerifyProductionWorldSceneMenu()
        {
            RunGuarded(VerifyInternal, false);
        }

        private delegate bool WiringStep();

        private static void RunGuarded(WiringStep step, bool allowExitCode)
        {
            bool succeeded;
            try
            {
                succeeded = step();
            }
            catch (System.Exception exception)
            {
                Debug.LogError(
                    LogPrefix + "RESULT=FAIL reason=exception type=" + exception.GetType().Name +
                    " message=" + exception.Message + "\n" + exception.StackTrace);
                succeeded = false;
            }

            if (succeeded)
            {
                // Success deliberately does NOT call EditorApplication.Exit(0): -quit exits 0 on
                // its own after pending imports flush, and forcing an exit here can truncate the
                // log the caller is about to read.
                return;
            }

            if (allowExitCode && Application.isBatchMode)
            {
                EditorApplication.Exit(1);
                return;
            }

            Debug.LogError(LogPrefix + "FAILED. No process exit code was set (not a batchmode run).");
        }

        // ------------------------------------------------------------------------------------
        // Wiring
        // ------------------------------------------------------------------------------------

        private static bool WireInternal()
        {
            if (!OwnerScriptAssetsResolve())
                return false;

            // No dialogs are allowed here, so an interactive run must never silently discard an
            // operator's unsaved scene. Refuse instead.
            if (!Application.isBatchMode && TryDescribeUnsavedScenes(out string unsavedScenes))
            {
                Debug.LogError(
                    LogPrefix + "RESULT=FAIL reason=unsaved_open_scenes scenes=" + unsavedScenes +
                    ". Save or discard them first; this utility is dialog-free by contract and will not decide for you.");
                return false;
            }

            Scene scene = EditorSceneManager.OpenScene(ProductionWorldScenePath, OpenSceneMode.Single);
            if (!scene.IsValid() || !scene.isLoaded)
            {
                Debug.LogError(LogPrefix + "RESULT=FAIL reason=scene_open_failed scene=" + ProductionWorldScenePath);
                return false;
            }

            int changes = 0;

            HectonLoreSystemsRoot root = UnityEngine.Object.FindAnyObjectByType<HectonLoreSystemsRoot>(FindObjectsInactive.Include);
            if (root == null)
            {
                root = CreateLoreSystemsRoot(scene, ref changes);
                if (root == null)
                {
                    Debug.LogError(LogPrefix + "RESULT=FAIL reason=lore_root_creation_failed scene=" + ProductionWorldScenePath);
                    return false;
                }

                Debug.Log(LogPrefix + "CREATED HectonLoreSystemsRoot host=" + DescribeHierarchy(root.transform));
            }
            else
            {
                Debug.Log(LogPrefix + "ALREADY PRESENT HectonLoreSystemsRoot host=" + DescribeHierarchy(root.transform));
            }

            int foundBefore = root.GetFoundSystemCount();

            Hecton8.Quest.QuestManager quest =
                UnityEngine.Object.FindAnyObjectByType<Hecton8.Quest.QuestManager>(FindObjectsInactive.Include);
            Hecton8.Gameplay.FirstHourDirector director =
                UnityEngine.Object.FindAnyObjectByType<Hecton8.Gameplay.FirstHourDirector>(FindObjectsInactive.Include);

            if (quest == null || director == null)
            {
                int externalSystems = CountLoreSystemsOutsideRoot(root.transform, out string externalSummary);
                if (externalSystems == 0)
                {
                    // Sanctioned path: the root's own setup entry point creates one named child per
                    // system, QuestManager and FirstHourDirector included, and is idempotent because
                    // every EnsureSystem call first resolves transform.Find(name).
                    root.SetupAllSystems();
                    EditorUtility.SetDirty(root);
                    changes++;
                    Debug.Log(LogPrefix + "CALLED HectonLoreSystemsRoot.SetupAllSystems()");
                }
                else
                {
                    // SetupAllSystems resolves systems ONLY by child name under the root, so with a
                    // lore system already living elsewhere in the scene it would happily create a
                    // second one. Six contributors share this scene; duplicating their objects is
                    // worse than wiring narrowly.
                    Debug.LogWarning(
                        LogPrefix + "SKIPPED SetupAllSystems: " + externalSystems +
                        " lore system component(s) already live outside the lore root (" + externalSummary +
                        "). SetupAllSystems resolves by child name under the root only, so calling it would duplicate them. " +
                        "Wiring the first-hour owners individually instead.");

                    if (quest == null)
                        changes += EnsureOwnerUnderRoot<Hecton8.Quest.QuestManager>(root, QuestManagerChildName);

                    if (director == null)
                        changes += EnsureOwnerUnderRoot<Hecton8.Gameplay.FirstHourDirector>(root, FirstHourDirectorChildName);
                }

                quest = UnityEngine.Object.FindAnyObjectByType<Hecton8.Quest.QuestManager>(FindObjectsInactive.Include);
                director = UnityEngine.Object.FindAnyObjectByType<Hecton8.Gameplay.FirstHourDirector>(FindObjectsInactive.Include);
            }
            else
            {
                Debug.Log(LogPrefix + "ALREADY PRESENT QuestManager host=" + DescribeHierarchy(quest.transform));
                Debug.Log(LogPrefix + "ALREADY PRESENT FirstHourDirector host=" + DescribeHierarchy(director.transform));
            }

            if (quest == null || director == null)
            {
                Debug.LogError(
                    LogPrefix + "RESULT=FAIL reason=owner_missing_after_setup questManager=" +
                    (quest != null ? "present" : "MISSING") + " firstHourDirector=" +
                    (director != null ? "present" : "MISSING"));
                return false;
            }

            if (PopulateQuestRegistryWhenEmpty(quest, out int assignedQuestCount))
            {
                changes++;
                Debug.Log(LogPrefix + "ASSIGNED QuestManager." + AllQuestsFieldName + " count=" + assignedQuestCount);
            }

            WarnOnMissingFirstHourQuestIds(quest);

            // Existing exposed reporting entry point; logs the 17-system status and the
            // NarrativeDiscovery / AudioLogPickup content counts.
            root.ValidateSystems();
            int foundAfter = root.GetFoundSystemCount();
            if (foundAfter != foundBefore)
            {
                EditorUtility.SetDirty(root);
                changes++;
            }

            Debug.Log(
                LogPrefix + "SYSTEMS " + foundAfter + "/" + HectonLoreSystemsRoot.ExpectedSystemCount +
                " missing=" + root.GetMissingSystemsSummary());

            bool saved = false;
            if (changes > 0)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene))
                {
                    Debug.LogError(LogPrefix + "RESULT=FAIL reason=scene_save_failed scene=" + ProductionWorldScenePath);
                    return false;
                }

                AssetDatabase.SaveAssets();

                // Force a synchronous reimport so the dependency graph reflects the save instead of
                // the pre-save state before the gate predicate below reads it.
                AssetDatabase.ImportAsset(
                    ProductionWorldScenePath,
                    ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
                saved = true;
            }
            else
            {
                // Idempotent second run: nothing was created, so the scene is left untouched on disk.
                Debug.Log(LogPrefix + "NO CHANGES: all three owners were already present; scene not saved.");
            }

            // Object-model truth first: are the components actually in the loaded scene.
            bool objectModelOk = ReportObjectModelPresence(out int objectModelPresent);

            // Then the exact predicate ContentSanityValidator's gate uses.
            bool dependencyOk = ReportDependencyPresence(out int dependencyPresent);

            if (!objectModelOk)
            {
                Debug.LogError(
                    LogPrefix + "RESULT=FAIL reason=object_model_missing_owner objectModel=" +
                    objectModelPresent + "/3 dependencyGraph=" + dependencyPresent + "/3 saved=" +
                    (saved ? "1" : "0") + " scene=" + ProductionWorldScenePath);
                return false;
            }

            if (!dependencyOk)
            {
                // Not a hard failure: the object model is authoritative and the importer can lag a
                // save inside the same editor session. A fresh verify run settles it.
                Debug.LogWarning(
                    LogPrefix + "dependency graph disagrees with the object model (dependencyGraph=" +
                    dependencyPresent + "/3). Re-run VerifyProductionWorldSceneBatch in a FRESH Unity invocation " +
                    "before claiming the ContentSanityValidator gate passes.");
            }

            Debug.Log(
                LogPrefix + "RESULT=OK changes=" + changes + " note=" + (changes > 0 ? "wired" : "already_present") +
                " objectModel=" + objectModelPresent + "/3 dependencyGraph=" + dependencyPresent +
                "/3 saved=" + (saved ? "1" : "0") + " scene=" + ProductionWorldScenePath);
            return true;
        }

        private static HectonLoreSystemsRoot CreateLoreSystemsRoot(Scene scene, ref int changes)
        {
            // scene.GetRootGameObjects() sees inactive roots; GameObject.Find does not. That single
            // difference is why LoreSystemsBootstrapUtility can create a second systems root.
            GameObject systemsRoot = FindRootByName(scene, SystemsRootName);
            if (systemsRoot == null)
            {
                systemsRoot = new GameObject(SystemsRootName);
                MoveToSceneIfNeeded(systemsRoot, scene);
                Undo.RegisterCreatedObjectUndo(systemsRoot, "Create Systems Root");
                changes++;
            }
            else if (!systemsRoot.activeInHierarchy)
            {
                Debug.LogWarning(
                    LogPrefix + "existing '" + SystemsRootName + "' is INACTIVE. Re-used it, but " +
                    "LoreSystemsBootstrapUtility uses GameObject.Find and will not see it.");
            }

            Transform existingLoreSystems = systemsRoot.transform.Find(LoreSystemsName);
            GameObject loreSystemsGo;
            if (existingLoreSystems != null)
            {
                loreSystemsGo = existingLoreSystems.gameObject;
            }
            else
            {
                loreSystemsGo = new GameObject(LoreSystemsName);
                loreSystemsGo.transform.SetParent(systemsRoot.transform, false);
                Undo.RegisterCreatedObjectUndo(loreSystemsGo, "Create LoreSystems");
                changes++;
            }

            if (!loreSystemsGo.TryGetComponent(out HectonLoreSystemsRoot root))
            {
                root = Undo.AddComponent<HectonLoreSystemsRoot>(loreSystemsGo);
                changes++;
            }

            return root;
        }

        /// <summary>
        /// Ensures one host named <paramref name="childName"/> under the lore root carries
        /// <typeparamref name="T"/>. The name matters: HectonLoreSystemsRoot resolves every system
        /// through transform.Find(name), so a differently named host reads as missing and its next
        /// SetupAllSystems call would create a duplicate.
        /// </summary>
        private static int EnsureOwnerUnderRoot<T>(HectonLoreSystemsRoot root, string childName)
            where T : MonoBehaviour
        {
            T existing = UnityEngine.Object.FindAnyObjectByType<T>(FindObjectsInactive.Include);
            if (existing != null)
            {
                Debug.Log(LogPrefix + "ALREADY PRESENT " + typeof(T).Name + " host=" + DescribeHierarchy(existing.transform));
                return 0;
            }

            int changes = 0;
            Transform child = root.transform.Find(childName);
            GameObject host;
            if (child != null)
            {
                host = child.gameObject;
            }
            else
            {
                host = new GameObject(childName);
                host.transform.SetParent(root.transform, false);
                Undo.RegisterCreatedObjectUndo(host, "Create " + childName);
                changes++;
            }

            Undo.AddComponent<T>(host);
            changes++;
            Debug.Log(LogPrefix + "CREATED " + typeof(T).Name + " host=" + DescribeHierarchy(host.transform));
            return changes;
        }

        /// <summary>
        /// Assigns every authored QuestData under <see cref="QuestDataFolder"/> to the manager's
        /// serialized registry, but only when that registry is empty. An empty registry is the
        /// difference between a QuestManager that exists and a quest spine that runs: the manager
        /// builds its lookup and state graph from this array in Awake.
        /// </summary>
        private static bool PopulateQuestRegistryWhenEmpty(Hecton8.Quest.QuestManager quest, out int assignedCount)
        {
            assignedCount = 0;
            if (quest == null)
                return false;

            SerializedObject serializedManager = new SerializedObject(quest);
            SerializedProperty questArray = serializedManager.FindProperty(AllQuestsFieldName);
            if (questArray == null)
            {
                Debug.LogError(
                    LogPrefix + "QuestManager serialized field '" + AllQuestsFieldName +
                    "' not found. The field was renamed; this utility and LoreSystemsBootstrapUtility both need updating.");
                return false;
            }

            if (questArray.arraySize > 0)
            {
                assignedCount = questArray.arraySize;
                Debug.Log(LogPrefix + "ALREADY PRESENT QuestManager." + AllQuestsFieldName + " count=" + assignedCount);
                return false;
            }

            if (!AssetDatabase.IsValidFolder(QuestDataFolder))
            {
                Debug.LogWarning(
                    LogPrefix + "quest data folder missing: " + QuestDataFolder +
                    ". QuestManager exists but its registry stays empty, so no quest can activate.");
                return false;
            }

            string[] guids = AssetDatabase.FindAssets(QuestDataFilter, new[] { QuestDataFolder });
            if (guids == null || guids.Length <= 0)
            {
                Debug.LogWarning(
                    LogPrefix + "no QuestData assets under " + QuestDataFolder +
                    ". QuestManager exists but its registry stays empty, so no quest can activate.");
                return false;
            }

            // COLD ALLOC: string[guids.Length] - editor-only quest asset path sort buffer - owner: H8_NarrativeSceneWiring
            string[] paths = new string[guids.Length];
            for (int i = 0; i < guids.Length; i++)
                paths[i] = AssetDatabase.GUIDToAssetPath(guids[i]);

            // Deterministic order: FindAssets order is not stable across machines, and an unstable
            // order would rewrite the scene diff on every run.
            System.Array.Sort(paths, System.StringComparer.Ordinal);

            // COLD ALLOC: List<QuestData>[guids.Length] - editor-only authored quest staging - owner: H8_NarrativeSceneWiring
            System.Collections.Generic.List<Hecton8.Quest.QuestData> quests =
                new System.Collections.Generic.List<Hecton8.Quest.QuestData>(paths.Length);

            for (int i = 0; i < paths.Length; i++)
            {
                if (string.IsNullOrEmpty(paths[i]))
                    continue;

                Hecton8.Quest.QuestData questData = AssetDatabase.LoadAssetAtPath<Hecton8.Quest.QuestData>(paths[i]);
                if (questData != null)
                    quests.Add(questData);
            }

            if (quests.Count <= 0)
            {
                Debug.LogWarning(LogPrefix + "QuestData assets found but none loaded from " + QuestDataFolder + ".");
                return false;
            }

            questArray.arraySize = quests.Count;
            for (int i = 0; i < quests.Count; i++)
                questArray.GetArrayElementAtIndex(i).objectReferenceValue = quests[i];

            serializedManager.ApplyModifiedProperties();
            EditorUtility.SetDirty(quest);
            assignedCount = quests.Count;
            return true;
        }

        /// <summary>
        /// FirstHourDirector needs no object references - every serialized field is a float or a
        /// string id, and the shipped defaults name the drill/copper/first-breath chain. What it DOES
        /// need is those ids to exist in the QuestManager registry, or its route is inert while
        /// looking wired.
        /// </summary>
        private static void WarnOnMissingFirstHourQuestIds(Hecton8.Quest.QuestManager quest)
        {
            SerializedObject serializedManager = new SerializedObject(quest);
            SerializedProperty questArray = serializedManager.FindProperty(AllQuestsFieldName);
            if (questArray == null || questArray.arraySize <= 0)
            {
                Debug.LogWarning(
                    LogPrefix + "QuestManager registry is empty: FirstHourDirector's arrival/drill/copper/first-breath " +
                    "route cannot advance even though all three owners are present.");
                return;
            }

            for (int required = 0; required < RequiredFirstHourQuestIds.Length; required++)
            {
                string requiredId = RequiredFirstHourQuestIds[required];
                bool found = false;
                for (int i = 0; i < questArray.arraySize && !found; i++)
                {
                    Hecton8.Quest.QuestData questData =
                        questArray.GetArrayElementAtIndex(i).objectReferenceValue as Hecton8.Quest.QuestData;
                    found = questData != null &&
                            string.Equals(questData.questId, requiredId, System.StringComparison.Ordinal);
                }

                if (!found)
                {
                    Debug.LogWarning(
                        LogPrefix + "first-hour quest id '" + requiredId +
                        "' is not in the assigned QuestManager registry. FirstHourDirector references it by id, " +
                        "so that step of the first-hour spine is dead until the asset exists.");
                }
            }
        }

        // ------------------------------------------------------------------------------------
        // Verification (mutates nothing)
        // ------------------------------------------------------------------------------------

        private static bool VerifyInternal()
        {
            if (!OwnerScriptAssetsResolve())
                return false;

            if (string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(ProductionWorldScenePath)))
            {
                Debug.LogError(LogPrefix + "RESULT=FAIL reason=scene_asset_missing scene=" + ProductionWorldScenePath);
                return false;
            }

            bool ok = ReportDependencyPresence(out int present);
            if (!ok)
            {
                Debug.LogError(
                    LogPrefix + "RESULT=FAIL reason=owner_not_serialized_in_scene dependencyGraph=" + present +
                    "/3 scene=" + ProductionWorldScenePath +
                    " fix=Unity.exe -batchmode -quit -projectPath . -executeMethod Hecton8.Editor.H8_NarrativeSceneWiring.WireProductionWorldSceneBatch");
                return false;
            }

            Debug.Log(
                LogPrefix + "RESULT=OK dependencyGraph=" + present + "/3 mutated=0 scene=" + ProductionWorldScenePath);
            return true;
        }

        /// <summary>
        /// Same predicate ContentSanityValidator's gate uses: a DIRECT importer dependency from the
        /// scene onto the MonoScript, which is format-agnostic. The production scene is BINARY
        /// serialized, so a hex GUID text search over it answers nothing either way.
        /// </summary>
        private static bool ReportDependencyPresence(out int presentCount)
        {
            presentCount = 0;
            presentCount += ReportOneDependency(LoreSystemsRootScriptPath, "HectonLoreSystemsRoot") ? 1 : 0;
            presentCount += ReportOneDependency(QuestManagerScriptPath, "QuestManager") ? 1 : 0;
            presentCount += ReportOneDependency(FirstHourDirectorScriptPath, "FirstHourDirector") ? 1 : 0;
            return presentCount == 3;
        }

        private static bool ReportOneDependency(string scriptAssetPath, string label)
        {
            bool present = H8_FormatAgnosticTypeCensus.AssetDirectlyReferencesScript(
                ProductionWorldScenePath, scriptAssetPath);

            Debug.Log(
                LogPrefix + "DEPENDENCY " + label + "=" + (present ? "PRESENT" : "ABSENT") +
                " script=" + scriptAssetPath);
            return present;
        }

        private static bool ReportObjectModelPresence(out int presentCount)
        {
            HectonLoreSystemsRoot root =
                UnityEngine.Object.FindAnyObjectByType<HectonLoreSystemsRoot>(FindObjectsInactive.Include);
            Hecton8.Quest.QuestManager quest =
                UnityEngine.Object.FindAnyObjectByType<Hecton8.Quest.QuestManager>(FindObjectsInactive.Include);
            Hecton8.Gameplay.FirstHourDirector director =
                UnityEngine.Object.FindAnyObjectByType<Hecton8.Gameplay.FirstHourDirector>(FindObjectsInactive.Include);

            presentCount = 0;
            presentCount += root != null ? 1 : 0;
            presentCount += quest != null ? 1 : 0;
            presentCount += director != null ? 1 : 0;

            Debug.Log(
                LogPrefix + "OBJECTMODEL HectonLoreSystemsRoot=" + (root != null ? "PRESENT" : "ABSENT") +
                " QuestManager=" + (quest != null ? "PRESENT" : "ABSENT") +
                " FirstHourDirector=" + (director != null ? "PRESENT" : "ABSENT"));
            return presentCount == 3;
        }

        private static bool OwnerScriptAssetsResolve()
        {
            bool ok = true;
            ok &= OneScriptAssetResolves(LoreSystemsRootScriptPath);
            ok &= OneScriptAssetResolves(QuestManagerScriptPath);
            ok &= OneScriptAssetResolves(FirstHourDirectorScriptPath);
            if (!ok)
                Debug.LogError(LogPrefix + "RESULT=FAIL reason=owner_script_asset_missing");

            return ok;
        }

        private static bool OneScriptAssetResolves(string scriptAssetPath)
        {
            if (!string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(scriptAssetPath)))
                return true;

            Debug.LogError(LogPrefix + "script asset does not resolve: " + scriptAssetPath);
            return false;
        }

        // ------------------------------------------------------------------------------------
        // Scene helpers
        // ------------------------------------------------------------------------------------

        /// <summary>
        /// Counts lore system components that already live outside the lore root subtree. Matching by
        /// type NAME keeps this assembly free of a reference to every system's assembly; a name
        /// collision would only make the caller more conservative, never less.
        /// </summary>
        private static int CountLoreSystemsOutsideRoot(Transform loreRoot, out string summary)
        {
            MonoBehaviour[] behaviours =
                UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None);

            // COLD ALLOC: StringBuilder[256] - editor-only external system report buffer - owner: H8_NarrativeSceneWiring
            System.Text.StringBuilder builder = new System.Text.StringBuilder(256);
            int count = 0;

            for (int i = 0; i < behaviours.Length; i++)
            {
                MonoBehaviour behaviour = behaviours[i];
                if (behaviour == null)
                    continue;

                if (!IsLoreSystemTypeName(behaviour.GetType().Name))
                    continue;

                if (IsSelfOrDescendantOf(behaviour.transform, loreRoot))
                    continue;

                count++;
                if (count > ExternalSystemReportCap)
                    continue;

                if (builder.Length > 0)
                    builder.Append(", ");

                builder.Append(behaviour.GetType().Name).Append('@').Append(DescribeHierarchy(behaviour.transform));
            }

            if (count > ExternalSystemReportCap)
                builder.Append(", ... +").Append(count - ExternalSystemReportCap).Append(" more");

            summary = builder.ToString();
            return count;
        }

        private static bool IsLoreSystemTypeName(string typeName)
        {
            for (int i = 0; i < LoreSystemTypeNames.Length; i++)
            {
                if (string.Equals(LoreSystemTypeNames[i], typeName, System.StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        private static bool IsSelfOrDescendantOf(Transform candidate, Transform ancestor)
        {
            if (candidate == null || ancestor == null)
                return false;

            Transform cursor = candidate;
            while (cursor != null)
            {
                if (cursor == ancestor)
                    return true;

                cursor = cursor.parent;
            }

            return false;
        }

        private static GameObject FindRootByName(Scene scene, string rootName)
        {
            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                if (roots[i] != null && string.Equals(roots[i].name, rootName, System.StringComparison.Ordinal))
                    return roots[i];
            }

            return null;
        }

        private static void MoveToSceneIfNeeded(GameObject go, Scene scene)
        {
            if (go == null || !scene.IsValid())
                return;

            if (string.Equals(go.scene.path, scene.path, System.StringComparison.Ordinal))
                return;

            SceneManager.MoveGameObjectToScene(go, scene);
        }

        private static bool TryDescribeUnsavedScenes(out string summary)
        {
            // COLD ALLOC: StringBuilder[128] - editor-only dirty scene report buffer - owner: H8_NarrativeSceneWiring
            System.Text.StringBuilder builder = new System.Text.StringBuilder(128);
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene open = SceneManager.GetSceneAt(i);
                if (!open.isDirty)
                    continue;

                if (builder.Length > 0)
                    builder.Append(", ");

                builder.Append(string.IsNullOrEmpty(open.path) ? open.name : open.path);
            }

            summary = builder.ToString();
            return builder.Length > 0;
        }

        private static string DescribeHierarchy(Transform target)
        {
            if (target == null)
                return "<null>";

            // COLD ALLOC: StringBuilder[128] - editor-only hierarchy path buffer - owner: H8_NarrativeSceneWiring
            System.Text.StringBuilder builder = new System.Text.StringBuilder(128);
            BuildHierarchy(target, builder);
            return builder.ToString();
        }

        private static void BuildHierarchy(Transform target, System.Text.StringBuilder builder)
        {
            if (target.parent != null)
            {
                BuildHierarchy(target.parent, builder);
                builder.Append('/');
            }

            builder.Append(target.name);
        }
    }
}
#endif
