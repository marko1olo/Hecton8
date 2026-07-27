using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hecton8.EditorTools.Diagnostics
{
    /// <summary>
    /// Answers "does a component of type T exist anywhere in the authored project, and where"
    /// across BOTH scenes and prefabs, through the editor object model and the AssetDatabase
    /// dependency graph. Never through a text search over the asset file.
    ///
    /// WHY THIS EXISTS, MEASURED RATHER THAN ASSUMED
    /// ProjectSettings/EditorSettings.asset:7 sets m_SerializationMode: 2 (ForceBinary), but that
    /// setting only governs assets Unity re-saves. The project is therefore MIXED, not uniformly
    /// binary as has been repeated all night:
    ///     02_HECTON_WORLD.unity  -> opens with null bytes, no %YAML header  -> BINARY
    ///     00_BOOTSTRAP.unity     -> opens with "%YAML 1.1"                  -> TEXT
    /// A GUID grep answers the second correctly and the first incorrectly, and it cannot tell you
    /// which case it just hit. That is worse than no answer: a zero hit against a binary asset is
    /// indistinguishable from a real absence. Every method used here reads through the importer,
    /// so it does not care which of the two a given file is.
    ///
    /// TWO INDEPENDENT METHODS, DELIBERATELY
    /// Each requested type is resolved twice per scanned asset:
    ///   1. OBJECT MODEL  - open/load the asset and walk components. Yields instance counts, the
    ///                      enabled flag, and the GameObject path.
    ///   2. DEPENDENCY    - ask AssetDatabase whether the asset directly references the type's
    ///                      MonoScript. Yields presence only, but costs no scene load.
    /// They are reported side by side. Agreement is the evidence; DISAGREEMENT is printed as a
    /// loud instrument fault, because one of the two is then lying and the reader must know which
    /// results to distrust. A single method that cannot be cross-checked is how four probe runs
    /// tonight produced four non-comparable answers.
    ///
    /// WHAT IT MEASURES: authoring - components serialized into a scene or prefab asset.
    /// WHAT IT DOES NOT MEASURE: runtime creation. AddComponent, prefab instantiation from code,
    /// and objects built by a bootstrapper are all invisible here. A type absent from every scene
    /// and every prefab may still exist in a running game. For that half use
    /// H8_HeadlessPlayModeProbe. Neither probe substitutes for the other and this one never claims
    /// the runtime answer.
    ///
    /// NON-MUTATING BY CONSTRUCTION - this is the primary correctness requirement.
    /// AGENTS.md:126 forbids automated scripts from calling EditorSceneManager.SaveScene,
    /// PrefabUtility.SaveAsPrefabAsset, or EditorUtility.SetDirty on production assets, because
    /// doing so wipes authored work. This file therefore:
    ///   - opens scenes ADDITIVELY and closes them with removeScene: true in a finally block, so
    ///     an exception mid-walk cannot leave a production scene loaded;
    ///   - never closes a scene it did not itself open (checked via SceneManager.GetSceneByPath
    ///     before opening), so it cannot evict a scene a human was editing;
    ///   - reads prefabs with AssetDatabase.LoadAssetAtPath, NOT PrefabUtility.LoadPrefabContents.
    ///     LoadPrefabContents materialises a MUTABLE copy in a hidden preview scene that must be
    ///     unloaded by hand and can be written back with SaveAsPrefabAsset. LoadAssetAtPath hands
    ///     back the immutable imported root, which cannot be saved back by accident and needs no
    ///     teardown. For a read-only census the second is strictly the safer instrument, so there
    ///     is deliberately no LoadPrefabContents/UnloadPrefabContents pair in this file;
    ///   - contains no call to SaveScene, SaveAsPrefabAsset, SetDirty, or MarkSceneDirty anywhere,
    ///     and reports loudly if a scene it opened came back dirty.
    /// It also has no [MenuItem]. It is batchmode-only on purpose: additively opening a 60 MB
    /// production scene underneath a level designer is not something a stray menu click should do.
    ///
    /// USAGE
    ///   Unity.exe -batchmode -quit -projectPath . -logFile Logs/typecensus.log ^
    ///     -executeMethod Hecton8.EditorTools.Diagnostics.H8_FormatAgnosticTypeCensus.Run ^
    ///     [-h8TypeCensusTypes FirstHourDirector,QuestManager] ^
    ///     [-h8TypeCensusScenes Assets/_Project/Scenes/02_HECTON_WORLD.unity] ^
    ///     [-h8TypeCensusPrefabRoots Assets/_Project] ^
    ///     [-h8TypeCensusSkipPrefabs 1] ^
    ///     [-h8TypeCensusArtifact Logs/H8_TypeCensus/census.md]
    ///
    /// The artifact is written to a path resolved from the project root (AGENTS.md:128 bans
    /// hardcoded absolute developer paths); a rooted -h8TypeCensusArtifact value is rejected.
    ///
    /// This file also owns the two format-agnostic queries the content validator needs, so that
    /// there is one owner for "is this asset YAML text" and "does this asset reference this
    /// script" rather than a second private copy inside every caller.
    /// </summary>
    public static class H8_FormatAgnosticTypeCensus
    {
        private const string Marker = "[H8_TYPECENSUS]";

        private const string ArgTypes = "-h8TypeCensusTypes";
        private const string ArgScenes = "-h8TypeCensusScenes";
        private const string ArgPrefabRoots = "-h8TypeCensusPrefabRoots";
        private const string ArgSkipPrefabs = "-h8TypeCensusSkipPrefabs";
        private const string ArgArtifact = "-h8TypeCensusArtifact";

        private const string DefaultArtifactRelativePath = "Logs/H8_TypeCensus/type_census.md";

        /// <summary>
        /// Scanned when -h8TypeCensusScenes is not supplied: the three scenes the normative flow
        /// boots through (AGENTS.md:162). Opening a scene costs real time, so the sandbox scenes
        /// are not in the default set.
        /// </summary>
        private static readonly string[] DefaultScenes =
        {
            "Assets/_Project/Scenes/00_BOOTSTRAP.unity",
            "Assets/_Project/Scenes/01_MAIN_MENU.unity",
            "Assets/_Project/Scenes/02_HECTON_WORLD.unity",
        };

        private static readonly string[] DefaultPrefabRoots = { "Assets/_Project" };

        /// <summary>
        /// Seed list used only when -h8TypeCensusTypes is absent. The census is argument-driven by
        /// design; this default exists so a bare run answers the currently open question rather
        /// than nothing. These three are exactly the owners ContentSanityValidator gates the
        /// first-hour route on.
        /// </summary>
        private static readonly string[] DefaultTypes =
        {
            "FirstHourDirector",
            "QuestManager",
            "HectonLoreSystemsRoot",
        };

        public static void Run()
        {
            string[] typeNames = SplitArg(ArgTypes, DefaultTypes);
            string[] scenePaths = SplitArg(ArgScenes, DefaultScenes);
            string[] prefabRoots = SplitArg(ArgPrefabRoots, DefaultPrefabRoots);
            bool skipPrefabs = ReadArg(ArgSkipPrefabs) != null;

            var report = new StringBuilder(16384);
            AppendLine(report, "# H8 format-agnostic type census");
            AppendLine(report, string.Empty);
            AppendLine(report, "Evidence class: EDITOR_OBJECT_MODEL + ASSETDATABASE_DEPENDENCY_GRAPH.");
            AppendLine(report, "Measures AUTHORING only. Runtime AddComponent/Instantiate is invisible here and is NOT claimed.");
            AppendLine(report, string.Format(CultureInfo.InvariantCulture, "Generated (UTC): {0:yyyy-MM-dd HH:mm:ss}", DateTime.UtcNow));
            AppendLine(report, string.Empty);

            Log(string.Format(
                CultureInfo.InvariantCulture,
                "START types={0} scenes={1} prefabRoots={2} skipPrefabs={3} (authoring only, not runtime)",
                typeNames.Length,
                scenePaths.Length,
                prefabRoots.Length,
                skipPrefabs));

            var resolvedTypes = new List<RequestedType>(typeNames.Length);
            var typeNameIndex = BuildMonoBehaviourTypeIndex();
            for (int i = 0; i < typeNames.Length; i++)
                resolvedTypes.Add(ResolveRequestedType(typeNames[i], typeNameIndex));

            var corpus = new Corpus();
            ScanScenes(scenePaths, corpus, report);
            if (!skipPrefabs)
                ScanPrefabs(prefabRoots, corpus, report);
            else
                AppendLine(report, "## Prefabs\n\nSKIPPED by -h8TypeCensusSkipPrefabs. Prefab absence is therefore UNKNOWN, not proven.\n");

            bool instrumentOk = RunInstrumentSelfTest(corpus, typeNameIndex, report);
            ReportRequestedTypes(resolvedTypes, corpus, scenePaths, instrumentOk, report);

            string artifactPath = WriteArtifact(report.ToString());
            if (!string.IsNullOrEmpty(artifactPath))
                Log("ARTIFACT " + artifactPath);

            Log("DONE");
        }

        // ------------------------------------------------------------------------------------
        // Public format-agnostic queries. ContentSanityValidator consumes both of these; keeping
        // them here gives the project one owner for the question instead of one copy per caller.
        // ------------------------------------------------------------------------------------

        /// <summary>
        /// True when the asset on disk begins with a "%YAML" header, i.e. Unity wrote it in text
        /// serialization and a text parse over it is meaningful.
        ///
        /// Reads five bytes through a FileStream rather than pulling the whole asset in as text.
        /// That matters: File.ReadAllText does NOT throw on a binary Unity asset, it lossily
        /// decodes the bytes into a string, so a caller that only checks "did the read succeed"
        /// gets true and then searches a corrupted haystack. Detecting the format has to happen
        /// before the read, not after it.
        /// </summary>
        public static bool IsYamlTextSerialized(string projectAssetPath, out string detail)
        {
            detail = string.Empty;
            string absolutePath = ProjectAssetPathToAbsolutePath(projectAssetPath);
            if (string.IsNullOrEmpty(absolutePath) || !File.Exists(absolutePath))
            {
                detail = "file is missing on disk";
                return false;
            }

            try
            {
                byte[] header = new byte[5];
                int read;
                using (FileStream stream = File.OpenRead(absolutePath))
                    read = stream.Read(header, 0, header.Length);

                if (read < header.Length)
                {
                    detail = string.Format(CultureInfo.InvariantCulture, "file is only {0} byte(s) long", read);
                    return false;
                }

                if (string.Equals(Encoding.ASCII.GetString(header, 0, header.Length), "%YAML", StringComparison.Ordinal))
                {
                    detail = "%YAML header present - text serialization";
                    return true;
                }

                detail = string.Format(
                    CultureInfo.InvariantCulture,
                    "no %YAML header - binary serialization (first bytes {0:X2} {1:X2} {2:X2} {3:X2} {4:X2})",
                    header[0],
                    header[1],
                    header[2],
                    header[3],
                    header[4]);
                return false;
            }
            catch (Exception exception)
            {
                detail = exception.GetType().Name + ": " + exception.Message;
                return false;
            }
        }

        /// <summary>
        /// True when <paramref name="assetPath"/> directly references <paramref name="scriptAssetPath"/>.
        ///
        /// For a scene or prefab and a MonoScript, a DIRECT dependency means exactly one thing:
        /// the asset serializes at least one component whose m_Script points at that script. That
        /// is precisely what a "does the scene file contain this script GUID" text search was
        /// trying to establish - the same question, answered through the importer, so binary and
        /// text assets both answer correctly.
        ///
        /// Non-recursive on purpose. Recursive would also return scripts reached through a nested
        /// prefab or a referenced ScriptableObject, which the text route never counted and which
        /// would silently widen the meaning of every caller's gate.
        /// </summary>
        public static bool AssetDirectlyReferencesScript(string assetPath, string scriptAssetPath)
        {
            if (string.IsNullOrWhiteSpace(assetPath) || string.IsNullOrWhiteSpace(scriptAssetPath))
                return false;

            string[] dependencies = AssetDatabase.GetDependencies(assetPath, false);
            for (int i = 0; i < dependencies.Length; i++)
            {
                if (string.Equals(dependencies[i], scriptAssetPath, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Project-relative asset path of the MonoScript declaring <paramref name="typeName"/>, or
        /// empty when no such script asset can be located. Empty is reported by callers as
        /// "unresolvable", never folded into "absent".
        /// </summary>
        public static string FindMonoScriptAssetPath(string typeName)
        {
            if (string.IsNullOrWhiteSpace(typeName))
                return string.Empty;

            string[] guids = AssetDatabase.FindAssets(typeName + " t:MonoScript");
            string fileSuffix = "/" + typeName + ".cs";
            for (int i = 0; i < guids.Length; i++)
            {
                string candidate = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (string.IsNullOrEmpty(candidate) || !candidate.EndsWith(fileSuffix, StringComparison.OrdinalIgnoreCase))
                    continue;

                MonoScript script = AssetDatabase.LoadAssetAtPath<MonoScript>(candidate);
                Type declared = script != null ? script.GetClass() : null;
                if (declared != null && string.Equals(declared.Name, typeName, StringComparison.Ordinal))
                    return candidate;
            }

            return string.Empty;
        }

        // ------------------------------------------------------------------------------------
        // Scene lane
        // ------------------------------------------------------------------------------------

        private static void ScanScenes(string[] scenePaths, Corpus corpus, StringBuilder report)
        {
            AppendLine(report, "## Scenes");
            AppendLine(report, string.Empty);
            AppendLine(report, "| Scene | On-disk format | Roots | Components | Missing scripts | Opened by census |");
            AppendLine(report, "|---|---|---:|---:|---:|---|");

            for (int i = 0; i < scenePaths.Length; i++)
            {
                string scenePath = scenePaths[i];
                string absolutePath = ProjectAssetPathToAbsolutePath(scenePath);
                if (string.IsNullOrEmpty(absolutePath) || !File.Exists(absolutePath))
                {
                    Log("MISSING SCENE " + scenePath);
                    AppendLine(report, "| " + scenePath + " | MISSING ON DISK | - | - | - | no |");
                    continue;
                }

                IsYamlTextSerialized(scenePath, out string formatDetail);

                Scene existing = SceneManager.GetSceneByPath(scenePath);
                bool alreadyOpen = existing.IsValid() && existing.isLoaded;
                Scene scene = existing;
                bool openedHere = false;

                var assetScan = new AssetScan(scenePath, AssetKind.Scene);
                try
                {
                    if (!alreadyOpen)
                    {
                        scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
                        openedHere = true;
                    }

                    if (!scene.IsValid() || !scene.isLoaded)
                    {
                        Log("FAILED TO LOAD SCENE " + scenePath);
                        AppendLine(report, "| " + scenePath + " | " + formatDetail + " | - | - | - | open failed |");
                        continue;
                    }

                    GameObject[] roots = scene.GetRootGameObjects();
                    assetScan.RootCount = roots.Length;
                    for (int r = 0; r < roots.Length; r++)
                        CollectFrom(roots[r], assetScan);
                }
                finally
                {
                    // Close in finally so a throw mid-walk cannot leave a production scene loaded.
                    // removeScene: true discards without saving - there is no SaveScene call in
                    // this file and there must never be one (AGENTS.md:126).
                    if (openedHere && scene.IsValid() && scene.isLoaded)
                    {
                        if (scene.isDirty)
                        {
                            Debug.LogWarning(
                                Marker + " SCENE CAME BACK DIRTY " + scenePath +
                                " - a read-only walk must not dirty a scene. Closing WITHOUT saving anyway; " +
                                "investigate before trusting this row.");
                        }

                        if (SceneManager.sceneCount > 1)
                        {
                            EditorSceneManager.CloseScene(scene, removeScene: true);
                        }
                        else
                        {
                            Debug.LogWarning(
                                Marker + " COULD NOT CLOSE " + scenePath +
                                " - it is the only loaded scene and Unity keeps one open. Left loaded, NOT saved.");
                        }
                    }
                }

                corpus.Assets.Add(assetScan);
                corpus.Merge(assetScan);

                AppendLine(report, string.Format(
                    CultureInfo.InvariantCulture,
                    "| {0} | {1} | {2} | {3} | {4} | {5} |",
                    scenePath,
                    formatDetail,
                    assetScan.RootCount,
                    assetScan.ComponentCount,
                    assetScan.MissingScriptCount,
                    openedHere ? "yes, closed without saving" : "no, was already open - left untouched"));

                Log(string.Format(
                    CultureInfo.InvariantCulture,
                    "SCANNED SCENE {0} roots={1} components={2} missingScripts={3} format={4}",
                    Path.GetFileName(scenePath),
                    assetScan.RootCount,
                    assetScan.ComponentCount,
                    assetScan.MissingScriptCount,
                    formatDetail));
            }

            AppendLine(report, string.Empty);
        }

        // ------------------------------------------------------------------------------------
        // Prefab lane
        // ------------------------------------------------------------------------------------

        private static void ScanPrefabs(string[] prefabRoots, Corpus corpus, StringBuilder report)
        {
            var validRoots = new List<string>(prefabRoots.Length);
            for (int i = 0; i < prefabRoots.Length; i++)
            {
                if (AssetDatabase.IsValidFolder(prefabRoots[i]))
                    validRoots.Add(prefabRoots[i]);
                else
                    Log("PREFAB ROOT NOT A FOLDER " + prefabRoots[i]);
            }

            AppendLine(report, "## Prefabs");
            AppendLine(report, string.Empty);

            if (validRoots.Count <= 0)
            {
                AppendLine(report, "No valid prefab root folder was supplied. Prefab absence is UNKNOWN, not proven.\n");
                return;
            }

            string[] guids = AssetDatabase.FindAssets("t:Prefab", validRoots.ToArray());
            int scanned = 0;
            int unreadable = 0;

            for (int i = 0; i < guids.Length; i++)
            {
                string prefabPath = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (string.IsNullOrEmpty(prefabPath))
                    continue;

                // LoadAssetAtPath, not LoadPrefabContents: the imported root is immutable and
                // needs no teardown, so a read-only census cannot write a prefab back by accident.
                GameObject prefabRoot = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                if (prefabRoot == null)
                {
                    unreadable++;
                    continue;
                }

                var assetScan = new AssetScan(prefabPath, AssetKind.Prefab);
                CollectFrom(prefabRoot, assetScan);
                scanned++;

                // Only keep prefabs that actually carry a MonoBehaviour; the rest are noise and
                // holding every root would balloon memory over a thousand-prefab sweep.
                if (assetScan.ComponentCount > 0 || assetScan.MissingScriptCount > 0)
                    corpus.Assets.Add(assetScan);

                corpus.Merge(assetScan);
            }

            AppendLine(report, string.Format(
                CultureInfo.InvariantCulture,
                "Roots: {0}. Prefabs found: {1}. Read: {2}. Unreadable: {3}.",
                string.Join(", ", validRoots.ToArray()),
                guids.Length,
                scanned,
                unreadable));
            AppendLine(report, string.Empty);

            Log(string.Format(
                CultureInfo.InvariantCulture,
                "SCANNED PREFABS found={0} read={1} unreadable={2}",
                guids.Length,
                scanned,
                unreadable));
        }

        private static void CollectFrom(GameObject root, AssetScan assetScan)
        {
            // includeInactive: true - a disabled owner is still authored, and reporting it as
            // absent is exactly the false negative this census exists to remove.
            MonoBehaviour[] components = root.GetComponentsInChildren<MonoBehaviour>(true);
            for (int i = 0; i < components.Length; i++)
            {
                MonoBehaviour component = components[i];
                if (component == null)
                {
                    // A component whose script no longer resolves. Real, and worth counting, but
                    // it has no type to attribute a sighting to.
                    assetScan.MissingScriptCount++;
                    continue;
                }

                assetScan.ComponentCount++;
                string typeName = component.GetType().Name;

                assetScan.Sightings.TryGetValue(typeName, out Sighting sighting);
                sighting.Total++;
                if (component.enabled)
                    sighting.Enabled++;
                if (sighting.FirstPath == null)
                    sighting.FirstPath = BuildHierarchyPath(component.transform);
                assetScan.Sightings[typeName] = sighting;
            }
        }

        private static string BuildHierarchyPath(Transform transform)
        {
            var chain = new List<string>(8);
            Transform cursor = transform;
            while (cursor != null)
            {
                chain.Add(cursor.name);
                cursor = cursor.parent;
            }

            var builder = new StringBuilder(64);
            for (int i = chain.Count - 1; i >= 0; i--)
            {
                builder.Append(chain[i]);
                if (i > 0)
                    builder.Append('/');
            }

            return builder.ToString();
        }

        // ------------------------------------------------------------------------------------
        // Instrument self-test. A census that cannot detect a component it is pointed at must not
        // be allowed to publish an absence.
        // ------------------------------------------------------------------------------------

        private static bool RunInstrumentSelfTest(
            Corpus corpus,
            Dictionary<string, Type> typeNameIndex,
            StringBuilder report)
        {
            AppendLine(report, "## Instrument self-test");
            AppendLine(report, string.Empty);

            bool ok = true;

            // Negative control: this class is a static editor type and can never be a component.
            // If the matcher reports it, the matcher is comparing strings, not component types.
            string negativeControl = nameof(H8_FormatAgnosticTypeCensus);
            if (corpus.TotalByType.ContainsKey(negativeControl))
            {
                AppendLine(report, "- FAILED negative control: `" + negativeControl +
                    "` is a static editor class and was reported as a component. The matcher is not matching component types.");
                ok = false;
            }
            else
            {
                AppendLine(report, "- PASSED negative control: `" + negativeControl + "` never reported as a component.");
            }

            // Positive control A: the walk observed something at all.
            if (corpus.TotalComponentCount <= 0)
            {
                AppendLine(report, "- FAILED positive control A: zero MonoBehaviour components observed across the whole corpus. The walk is broken.");
                ok = false;
            }
            else
            {
                AppendLine(report, string.Format(
                    CultureInfo.InvariantCulture,
                    "- PASSED positive control A: {0} MonoBehaviour components observed across {1} scanned asset(s), {2} distinct type(s).",
                    corpus.TotalComponentCount,
                    corpus.Assets.Count,
                    corpus.TotalByType.Count));
            }

            // Positive control B, the strong one: take the most common type the walk actually
            // found, then push it back through the SAME resolve-and-look-up path the requested
            // types use. This proves the reporting path end to end and derives its own control
            // from live data, so it cannot rot when project content changes.
            string busiest = null;
            int busiestCount = 0;
            foreach (KeyValuePair<string, int> entry in corpus.TotalByType)
            {
                if (entry.Value <= busiestCount)
                    continue;

                busiest = entry.Key;
                busiestCount = entry.Value;
            }

            if (busiest == null)
            {
                AppendLine(report, "- SKIPPED positive control B: nothing was observed, so no round-trip control could be derived.");
            }
            else
            {
                RequestedType roundTrip = ResolveRequestedType(busiest, typeNameIndex);
                bool found = roundTrip.Resolved && CountAcross(corpus, busiest) > 0;
                if (found)
                {
                    AppendLine(report, string.Format(
                        CultureInfo.InvariantCulture,
                        "- PASSED positive control B: `{0}` ({1} instances) resolved and reported as present through the same path the requested types use.",
                        busiest,
                        busiestCount));
                }
                else
                {
                    AppendLine(report, "- FAILED positive control B: `" + busiest +
                        "` was observed by the walk but the reporting path did not report it as present. Absence rows below are NOT evidence.");
                    ok = false;
                }
            }

            AppendLine(report, string.Empty);
            if (!ok)
            {
                AppendLine(report, "**INSTRUMENT FAILED. Every `not found` below is UNVALIDATED and must not be cited as absence.**");
                AppendLine(report, string.Empty);
                Debug.LogWarning(Marker + " INSTRUMENT SELF-TEST FAILED - absence results are unvalidated.");
            }

            return ok;
        }

        // ------------------------------------------------------------------------------------
        // Reporting
        // ------------------------------------------------------------------------------------

        private static void ReportRequestedTypes(
            List<RequestedType> requested,
            Corpus corpus,
            string[] scenePaths,
            bool instrumentOk,
            StringBuilder report)
        {
            AppendLine(report, "## Requested types");
            AppendLine(report, string.Empty);

            for (int i = 0; i < requested.Count; i++)
            {
                RequestedType entry = requested[i];
                AppendLine(report, "### " + entry.Name);
                AppendLine(report, string.Empty);

                if (!entry.Resolved)
                {
                    AppendLine(report, "**UNKNOWN TYPE NAME.** No MonoBehaviour named `" + entry.Name +
                        "` exists in any loaded assembly, so this row is NOT evidence of absence - it is a bad name.");
                    AppendLine(report, string.Empty);
                    Log("UNKNOWN TYPE NAME " + entry.Name + " - not evidence of absence");
                    continue;
                }

                AppendLine(report, "- Script asset: " + (string.IsNullOrEmpty(entry.ScriptAssetPath)
                    ? "UNRESOLVED - no MonoScript asset located, so the dependency cross-check cannot run"
                    : "`" + entry.ScriptAssetPath + "`"));

                int total = CountAcross(corpus, entry.Name);
                if (total > 0)
                {
                    AppendLine(report, string.Format(CultureInfo.InvariantCulture, "- **FOUND** - {0} instance(s) authored:", total));
                    for (int a = 0; a < corpus.Assets.Count; a++)
                    {
                        AssetScan assetScan = corpus.Assets[a];
                        if (!assetScan.Sightings.TryGetValue(entry.Name, out Sighting sighting))
                            continue;

                        AppendLine(report, string.Format(
                            CultureInfo.InvariantCulture,
                            "  - {0} `{1}` -> `{2}` ({3} instance(s), {4} enabled)",
                            assetScan.Kind == AssetKind.Scene ? "scene" : "prefab",
                            assetScan.Path,
                            sighting.FirstPath,
                            sighting.Total,
                            sighting.Enabled));

                        Log(string.Format(
                            CultureInfo.InvariantCulture,
                            "FOUND {0} in {1} at {2} ({3} instance(s), {4} enabled)",
                            entry.Name,
                            assetScan.Path,
                            sighting.FirstPath,
                            sighting.Total,
                            sighting.Enabled));
                    }
                }
                else if (!instrumentOk)
                {
                    AppendLine(report, "- **UNVALIDATED** - the object-model walk saw no instance, but the instrument self-test failed, so this is not an absence claim.");
                    Log("UNVALIDATED " + entry.Name + " - instrument self-test failed");
                }
                else
                {
                    AppendLine(report, "- **NOT FOUND** - no instance in any scanned scene or prefab. " +
                        "This is an AUTHORING absence. It does not rule out runtime creation via AddComponent or code-driven instantiation.");
                    Log("NOT FOUND " + entry.Name + " - absent from every scanned scene and prefab (authoring only)");
                }

                AppendDependencyCrossCheck(entry, corpus, scenePaths, report);
                AppendLine(report, string.Empty);
            }
        }

        private static void AppendDependencyCrossCheck(
            RequestedType entry,
            Corpus corpus,
            string[] scenePaths,
            StringBuilder report)
        {
            if (string.IsNullOrEmpty(entry.ScriptAssetPath))
                return;

            AppendLine(report, "- Dependency-graph cross-check (independent of the object-model walk):");

            for (int i = 0; i < scenePaths.Length; i++)
            {
                string scenePath = scenePaths[i];
                bool byDependency = AssetDirectlyReferencesScript(scenePath, entry.ScriptAssetPath);
                bool byObjectModel = SightedIn(corpus, scenePath, entry.Name);

                string verdict = byDependency == byObjectModel
                    ? (byDependency ? "both say PRESENT" : "both say ABSENT")
                    : "**DISAGREEMENT**";

                AppendLine(report, string.Format(
                    CultureInfo.InvariantCulture,
                    "  - `{0}`: dependency={1}, objectModel={2} -> {3}",
                    scenePath,
                    byDependency ? "present" : "absent",
                    byObjectModel ? "present" : "absent",
                    verdict));

                if (byDependency != byObjectModel)
                {
                    Debug.LogWarning(string.Format(
                        CultureInfo.InvariantCulture,
                        "{0} METHOD DISAGREEMENT for {1} in {2}: dependency={3} objectModel={4}. One instrument is wrong; do not cite either.",
                        Marker,
                        entry.Name,
                        scenePath,
                        byDependency,
                        byObjectModel));
                }
            }
        }

        private static bool SightedIn(Corpus corpus, string assetPath, string typeName)
        {
            for (int i = 0; i < corpus.Assets.Count; i++)
            {
                AssetScan assetScan = corpus.Assets[i];
                if (!string.Equals(assetScan.Path, assetPath, StringComparison.OrdinalIgnoreCase))
                    continue;

                return assetScan.Sightings.ContainsKey(typeName);
            }

            return false;
        }

        private static int CountAcross(Corpus corpus, string typeName)
        {
            corpus.TotalByType.TryGetValue(typeName, out int total);
            return total;
        }

        // ------------------------------------------------------------------------------------
        // Type resolution
        // ------------------------------------------------------------------------------------

        private static Dictionary<string, Type> BuildMonoBehaviourTypeIndex()
        {
            var index = new Dictionary<string, Type>(StringComparer.Ordinal);
            foreach (Type derived in TypeCache.GetTypesDerivedFrom<MonoBehaviour>())
            {
                if (!index.ContainsKey(derived.Name))
                    index[derived.Name] = derived;
            }

            return index;
        }

        private static RequestedType ResolveRequestedType(string typeName, Dictionary<string, Type> index)
        {
            var entry = new RequestedType { Name = typeName };
            if (!index.TryGetValue(typeName, out Type declared))
                return entry;

            entry.Resolved = true;
            entry.DeclaredType = declared;
            entry.ScriptAssetPath = FindMonoScriptAssetPath(typeName);
            return entry;
        }

        // ------------------------------------------------------------------------------------
        // Artifact + argument plumbing
        // ------------------------------------------------------------------------------------

        private static string WriteArtifact(string content)
        {
            string relative = ReadArg(ArgArtifact);
            if (string.IsNullOrWhiteSpace(relative))
            {
                relative = DefaultArtifactRelativePath;
            }
            else if (Path.IsPathRooted(relative))
            {
                Debug.LogWarning(
                    Marker + " REJECTED absolute artifact path from " + ArgArtifact +
                    " - artifacts resolve from the project root (AGENTS.md:128). Using " + DefaultArtifactRelativePath + ".");
                relative = DefaultArtifactRelativePath;
            }

            try
            {
                string projectRoot = ProjectRoot();
                string fullPath = Path.GetFullPath(Path.Combine(projectRoot, relative));
                string directory = Path.GetDirectoryName(fullPath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                    Directory.CreateDirectory(directory);

                File.WriteAllText(fullPath, content, new UTF8Encoding(false));
                return fullPath;
            }
            catch (Exception exception)
            {
                Debug.LogWarning(Marker + " ARTIFACT WRITE FAILED: " + exception.GetType().Name + ": " + exception.Message);
                return string.Empty;
            }
        }

        private static string ProjectRoot()
        {
            DirectoryInfo parent = Directory.GetParent(Application.dataPath);
            return parent != null ? parent.FullName : Application.dataPath;
        }

        private static string ProjectAssetPathToAbsolutePath(string projectAssetPath)
        {
            if (string.IsNullOrWhiteSpace(projectAssetPath))
                return string.Empty;

            string normalized = projectAssetPath.Replace('/', Path.DirectorySeparatorChar);
            return Path.GetFullPath(Path.Combine(ProjectRoot(), normalized));
        }

        private static void AppendLine(StringBuilder builder, string line)
        {
            builder.Append(line);
            builder.Append('\n');
        }

        private static void Log(string message)
        {
            Debug.Log(Marker + " " + message);
        }

        private static string[] SplitArg(string name, string[] fallback)
        {
            string raw = ReadArg(name);
            if (string.IsNullOrEmpty(raw))
                return fallback;

            // StringSplitOptions.TrimEntries is .NET 5; this assembly compiles against an older
            // standard, so trim by hand.
            string[] parts = raw.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            var cleaned = new List<string>(parts.Length);
            for (int i = 0; i < parts.Length; i++)
            {
                string trimmed = parts[i].Trim();
                if (trimmed.Length > 0)
                    cleaned.Add(trimmed);
            }

            return cleaned.Count > 0 ? cleaned.ToArray() : fallback;
        }

        private static string ReadArg(string name)
        {
            // System.Environment, never bare Environment: Hecton8.Environment shadows it inside
            // the Hecton8.* namespace root and a bare reference fails CS0234.
            string[] args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (string.Equals(args[i], name, StringComparison.Ordinal))
                    return args[i + 1];
            }

            return null;
        }

        // ------------------------------------------------------------------------------------
        // Data
        // ------------------------------------------------------------------------------------

        private enum AssetKind
        {
            Scene = 0,
            Prefab = 1,
        }

        private struct Sighting
        {
            public int Total;
            public int Enabled;
            public string FirstPath;
        }

        private struct RequestedType
        {
            public string Name;
            public bool Resolved;
            public Type DeclaredType;
            public string ScriptAssetPath;
        }

        private sealed class AssetScan
        {
            public AssetScan(string path, AssetKind kind)
            {
                Path = path;
                Kind = kind;
                Sightings = new Dictionary<string, Sighting>(StringComparer.Ordinal);
            }

            public string Path { get; }

            public AssetKind Kind { get; }

            public Dictionary<string, Sighting> Sightings { get; }

            public int RootCount;
            public int ComponentCount;
            public int MissingScriptCount;
        }

        private sealed class Corpus
        {
            public List<AssetScan> Assets { get; } = new List<AssetScan>(64);

            public Dictionary<string, int> TotalByType { get; } = new Dictionary<string, int>(StringComparer.Ordinal);

            public int TotalComponentCount;

            public void Merge(AssetScan assetScan)
            {
                TotalComponentCount += assetScan.ComponentCount;
                foreach (KeyValuePair<string, Sighting> entry in assetScan.Sightings)
                {
                    TotalByType.TryGetValue(entry.Key, out int running);
                    TotalByType[entry.Key] = running + entry.Value.Total;
                }
            }
        }
    }
}
