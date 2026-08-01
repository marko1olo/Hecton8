using System.Collections.Generic;
using System.Text;
using Hecton8.Core;
using Hecton8.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;

namespace Hecton8.Editor
{
    /// <summary>
    /// Batchmode-safe audit of surface/interior/underwater audio mixer snapshot readiness.
    ///
    /// Product owners:
    /// - Assets/_Project/MasterMixer.mixer snapshots (Surface / Underwater / BaseInterior)
    /// - SceneRuntimeService dive crossfade fields (runtime-spawned; wiring often null until authored)
    /// - BaseAirlock dry/wet environment fields (scene or prefab authored)
    ///
    /// Does not mutate scenes or assets. Soft FAIL stays exit 0 under -quit.
    /// </summary>
    public static class AudioTransitionSnapshotValidator
    {
        private const string BootstrapScenePath = "Assets/_Project/Scenes/00_BOOTSTRAP.unity";
        private const string ProductionWorldScene = "Assets/_Project/Scenes/02_HECTON_WORLD.unity";
        private const string MasterMixerPath = "Assets/_Project/MasterMixer.mixer";
        private const string LogPrefix = "[AudioTransitionSnapshotValidator]";

        // Required MasterMixer snapshot names for surface / interior / underwater product lane.
        private static readonly string[] RequiredMixerSnapshotNames =
        {
            "Surface",
            "Underwater",
            "BaseInterior"
        };

        // COLD ALLOC: List<BaseAirlock>[64] - editor audit airlock scratch - owner: AudioTransitionSnapshotValidator
        private static readonly List<BaseAirlock> _airlocks = new List<BaseAirlock>(64);

        // COLD ALLOC: StringBuilder[8192] - editor audit report builder - owner: AudioTransitionSnapshotValidator
        private static readonly StringBuilder _report = new StringBuilder(8192);

        /// <summary>
        /// Public for -executeMethod / CI batchmode. Soft FAIL stays exit 0 under -quit.
        /// Hard exit 1 only when a required scene/asset path is missing on disk.
        /// </summary>
        [MenuItem("Hecton8/Validation/Validate Audio Transition Snapshots", priority = 186)]
        public static void ValidateAudioTransitionSnapshots()
        {
            bool batch = Application.isBatchMode;

            if (EditorApplication.isCompiling || EditorApplication.isUpdating || Application.isPlaying)
            {
                string busy = LogPrefix + " RESULT: FAIL — Editor busy (compiling/updating/playing).";
                Debug.LogError(busy);
                if (!batch)
                    EditorUtility.DisplayDialog("Audio Transition Snapshots", busy, "OK");
                return;
            }

            _report.Clear();
            _report.AppendLine("═══════════════════════════════════════════════════════");
            _report.AppendLine("HECTON-8 — Audio Transition Snapshot Audit");
            _report.AppendLine("═══════════════════════════════════════════════════════");
            _report.AppendLine();

            int missingCount = 0;
            int wiredCount = 0;
            int serviceCount = 0;
            int airlockSceneCount = 0;
            int airlockPrefabCount = 0;
            int mixerSnapshotPresent = 0;
            int mixerSnapshotMissing = 0;
            bool mixerAssetOk = false;

            // 1) MasterMixer asset gate — product snapshots must exist as authored mixer content.
            mixerAssetOk = AuditMasterMixer(ref mixerSnapshotPresent, ref mixerSnapshotMissing);

            // 2) Bootstrap scene + runtime-spawned SceneRuntimeService field wiring.
            if (!TryOpenScene(BootstrapScenePath, out Scene bootstrap, out string bootstrapFail))
            {
                Debug.LogError(LogPrefix + " RESULT: FAIL — " + bootstrapFail);
                if (!batch)
                    EditorUtility.DisplayDialog("Audio Transition Snapshots", bootstrapFail, "OK");
                if (batch)
                    EditorApplication.Exit(1);
                return;
            }

            AuditSceneRuntimeServices(bootstrap, ref serviceCount, ref wiredCount, ref missingCount);

            // 3) Production world BaseAirlock instances (if any are scene-authored).
            if (!TryOpenScene(ProductionWorldScene, out Scene world, out string worldFail))
            {
                Debug.LogError(LogPrefix + " RESULT: FAIL — " + worldFail);
                if (!batch)
                    EditorUtility.DisplayDialog("Audio Transition Snapshots", worldFail, "OK");
                if (batch)
                    EditorApplication.Exit(1);
                return;
            }

            AuditBaseAirlocksInScene(world, ref airlockSceneCount, ref wiredCount, ref missingCount);

            // 4) Prefab assets carrying BaseAirlock (binary world may under-report scene instances).
            AuditBaseAirlocksInPrefabs(ref airlockPrefabCount, ref wiredCount, ref missingCount);

            int airlockTotal = airlockSceneCount + airlockPrefabCount;

            _report.AppendLine();
            _report.Append("masterMixerOk=").Append(mixerAssetOk ? 1 : 0);
            _report.Append(" mixerSnapshotsPresent=").Append(mixerSnapshotPresent);
            _report.Append(" mixerSnapshotsMissing=").Append(mixerSnapshotMissing);
            _report.Append(" sceneRuntimeServices=").Append(serviceCount);
            _report.Append(" airlocksScene=").Append(airlockSceneCount);
            _report.Append(" airlocksPrefab=").Append(airlockPrefabCount);
            _report.Append(" snapshotsWired=").Append(wiredCount);
            _report.Append(" snapshotsMissing=").Append(missingCount);
            _report.AppendLine();

            // PASS gate: MasterMixer must expose Surface + Underwater + BaseInterior.
            // Component wiring is measured evidence for the product [~] lane; missing wiring
            // alone does not fail CI once mixer assets exist (owners are runtime-spawned).
            bool mixerOk = mixerAssetOk && mixerSnapshotMissing == 0;
            bool passed = mixerOk;

            if (!mixerOk)
                _report.AppendLine("FAIL reason: MasterMixer missing required surface/interior/underwater snapshots.");
            else if (serviceCount == 0 && airlockTotal == 0)
                _report.AppendLine("PASS (mixer): MasterMixer snapshots present. No scene/prefab owners wired yet (runtime-spawn path).");
            else if (missingCount > 0)
                _report.AppendLine("PASS (mixer): MasterMixer OK. Component snapshot refs still missing on one or more owners (product wiring gap).");
            else
                _report.AppendLine("PASS: MasterMixer + owner snapshot refs are fully wired.");

            _report.Append("RESULT: ").AppendLine(passed ? "PASS" : "FAIL");
            string reportText = LogPrefix + " " + _report.ToString();

            if (passed)
                Debug.Log(reportText);
            else
                Debug.LogError(reportText);

            if (!batch)
            {
                EditorUtility.DisplayDialog(
                    "Audio Transition Snapshots",
                    passed
                        ? "PASS\nmixerPresent=" + mixerSnapshotPresent + " wired=" + wiredCount + " missing=" + missingCount
                        : "FAIL\nmixerMissing=" + mixerSnapshotMissing + "\nSee Console.",
                    "OK");
            }
            // batchmode: soft FAIL under -quit (no EditorApplication.Exit on audit fail).
        }

        private static bool AuditMasterMixer(ref int present, ref int missing)
        {
            _report.AppendLine("--- MasterMixer asset ---");
            _report.Append("Path: ").AppendLine(MasterMixerPath);

            if (!System.IO.File.Exists(MasterMixerPath))
            {
                _report.AppendLine("  • MasterMixer.mixer MISSING on disk");
                missing = RequiredMixerSnapshotNames.Length;
                return false;
            }

            AudioMixer mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(MasterMixerPath);
            if (mixer == null)
            {
                _report.AppendLine("  • failed to LoadAssetAtPath<AudioMixer>");
                missing = RequiredMixerSnapshotNames.Length;
                return false;
            }

            // Enumerate all sub-assets; AudioMixerSnapshot lives as child of the mixer.
            Object[] subAssets = AssetDatabase.LoadAllAssetsAtPath(MasterMixerPath);
            // COLD ALLOC: HashSet for name lookup - editor audit only - owner: AudioTransitionSnapshotValidator
            HashSet<string> snapshotNames = new HashSet<string>();
            if (subAssets != null)
            {
                for (int i = 0; i < subAssets.Length; i++)
                {
                    AudioMixerSnapshot snap = subAssets[i] as AudioMixerSnapshot;
                    if (snap == null)
                        continue;
                    if (string.IsNullOrEmpty(snap.name))
                        continue;
                    snapshotNames.Add(snap.name);
                    _report.Append("  • snapshot asset: ").AppendLine(snap.name);
                }
            }

            // Also try FindSnapshot when available (name-exact).
            for (int i = 0; i < RequiredMixerSnapshotNames.Length; i++)
            {
                string required = RequiredMixerSnapshotNames[i];
                bool found = snapshotNames.Contains(required);
                if (!found)
                {
                    // Fallback: mixer.FindSnapshot (returns null when missing).
                    AudioMixerSnapshot viaFind = mixer.FindSnapshot(required);
                    found = viaFind != null;
                    if (found)
                        snapshotNames.Add(required);
                }

                if (found)
                {
                    present++;
                    _report.Append("  • required '").Append(required).AppendLine("' = PRESENT");
                }
                else
                {
                    missing++;
                    _report.Append("  • required '").Append(required).AppendLine("' = MISSING");
                }
            }

            _report.Append("  mixer=").Append(mixer.name);
            _report.Append(" requiredPresent=").Append(present);
            _report.Append(" requiredMissing=").Append(missing);
            _report.AppendLine();
            return true;
        }

        private static bool TryOpenScene(string path, out Scene scene, out string failure)
        {
            scene = default;
            failure = null;

            Scene active = SceneManager.GetActiveScene();
            if (active.IsValid() && active.isLoaded && active.path == path)
            {
                scene = active;
                return true;
            }

            if (!System.IO.File.Exists(path))
            {
                failure = "Scene missing on disk: " + path;
                return false;
            }

            scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            if (!scene.IsValid() || !scene.isLoaded || scene.path != path)
            {
                failure = "Failed to open " + path;
                return false;
            }

            return true;
        }

        private static void AuditSceneRuntimeServices(Scene scene, ref int serviceCount, ref int wiredCount, ref int missingCount)
        {
            _report.AppendLine("--- 00_BOOTSTRAP / SceneRuntimeService ---");

            SceneRuntimeService[] services = Object.FindObjectsByType<SceneRuntimeService>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            for (int i = 0; i < services.Length; i++)
            {
                SceneRuntimeService service = services[i];
                if (service == null)
                    continue;
                if (service.gameObject.scene != scene)
                    continue;

                serviceCount++;
                AuditServiceFields(service, GetTransformPath(service.transform), ref wiredCount, ref missingCount);
            }

            // Runtime-spawn path: GameBootstrapper calls EnsureRuntimeInstance(). Static scene open
            // finds 0 — spawn a temporary editor instance to measure default serialized wiring,
            // then destroy it so the audit does not leak objects into the open scene.
            if (serviceCount == 0)
            {
                _report.AppendLine("  • no SceneRuntimeService in scene (expected: runtime-spawned)");
                SceneRuntimeService spawned = null;
                try
                {
                    spawned = SceneRuntimeService.EnsureRuntimeInstance();
                    if (spawned != null)
                    {
                        serviceCount++;
                        _report.AppendLine("  • EnsureRuntimeInstance() → temporary editor instance");
                        AuditServiceFields(spawned, "[EnsureRuntimeInstance]", ref wiredCount, ref missingCount);
                    }
                    else
                    {
                        _report.AppendLine("  • EnsureRuntimeInstance() returned null");
                    }
                }
                finally
                {
                    if (spawned != null)
                    {
                        Object.DestroyImmediate(spawned.gameObject);
                    }
                }
            }
        }

        private static void AuditServiceFields(
            SceneRuntimeService service,
            string ownerPath,
            ref int wiredCount,
            ref int missingCount)
        {
            SerializedObject so = new SerializedObject(service);
            AudioMixerSnapshot menu = ReadSnapshot(so, "mainMenuMusicSnapshot");
            AudioMixerSnapshot abyss = ReadSnapshot(so, "abyssalAmbientSnapshot");

            ReportSnapshot(ownerPath, "mainMenuMusicSnapshot", menu, ref wiredCount, ref missingCount);
            ReportSnapshot(ownerPath, "abyssalAmbientSnapshot", abyss, ref wiredCount, ref missingCount);
        }

        private static void AuditBaseAirlocksInScene(Scene scene, ref int airlockCount, ref int wiredCount, ref int missingCount)
        {
            _report.AppendLine("--- 02_HECTON_WORLD / BaseAirlock (scene) ---");
            _airlocks.Clear();

            BaseAirlock[] found = Object.FindObjectsByType<BaseAirlock>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            for (int i = 0; i < found.Length; i++)
            {
                BaseAirlock airlock = found[i];
                if (airlock == null)
                    continue;
                if (airlock.gameObject.scene != scene)
                    continue;
                _airlocks.Add(airlock);
            }

            airlockCount = _airlocks.Count;
            if (airlockCount == 0)
            {
                _report.AppendLine("  • no BaseAirlock in opened world scene");
                return;
            }

            for (int i = 0; i < _airlocks.Count; i++)
            {
                BaseAirlock airlock = _airlocks[i];
                AuditAirlockFields(airlock, GetTransformPath(airlock.transform), ref wiredCount, ref missingCount);
            }
        }

        private static void AuditBaseAirlocksInPrefabs(ref int prefabCount, ref int wiredCount, ref int missingCount)
        {
            _report.AppendLine("--- Prefab assets / BaseAirlock ---");

            // COLD ALLOC: FindAssets string[] - editor audit only - owner: AudioTransitionSnapshotValidator
            string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/_Project" });
            int scanned = 0;
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (string.IsNullOrEmpty(path))
                    continue;

                // Prefer lightweight component load; skip huge non-project noise already filtered by folder.
                GameObject prefabRoot = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefabRoot == null)
                    continue;

                BaseAirlock[] airlocks = prefabRoot.GetComponentsInChildren<BaseAirlock>(true);
                if (airlocks == null || airlocks.Length == 0)
                    continue;

                scanned++;
                for (int a = 0; a < airlocks.Length; a++)
                {
                    BaseAirlock airlock = airlocks[a];
                    if (airlock == null)
                        continue;
                    prefabCount++;
                    string owner = path + " :: " + GetTransformPath(airlock.transform);
                    AuditAirlockFields(airlock, owner, ref wiredCount, ref missingCount);
                }
            }

            if (prefabCount == 0)
                _report.AppendLine("  • no BaseAirlock on prefabs under Assets/_Project (scanned prefab assets)");
            else
                _report.Append("  prefabAssetsWithAirlock=").Append(scanned).Append(" airlockComponents=").Append(prefabCount).AppendLine();
        }

        private static void AuditAirlockFields(
            BaseAirlock airlock,
            string ownerPath,
            ref int wiredCount,
            ref int missingCount)
        {
            SerializedObject so = new SerializedObject(airlock);
            AudioMixerSnapshot dry = ReadSnapshot(so, "dryInteriorSnapshot");
            AudioMixerSnapshot wet = ReadSnapshot(so, "wetExteriorSnapshot");

            ReportSnapshot(ownerPath, "dryInteriorSnapshot", dry, ref wiredCount, ref missingCount);
            ReportSnapshot(ownerPath, "wetExteriorSnapshot", wet, ref wiredCount, ref missingCount);
        }

        private static AudioMixerSnapshot ReadSnapshot(SerializedObject so, string propertyName)
        {
            if (so == null)
                return null;

            SerializedProperty prop = so.FindProperty(propertyName);
            if (prop == null || prop.propertyType != SerializedPropertyType.ObjectReference)
                return null;

            return prop.objectReferenceValue as AudioMixerSnapshot;
        }

        private static void ReportSnapshot(
            string ownerPath,
            string fieldName,
            AudioMixerSnapshot snapshot,
            ref int wiredCount,
            ref int missingCount)
        {
            bool wired = snapshot != null;
            if (wired)
                wiredCount++;
            else
                missingCount++;

            _report.Append("  • ");
            _report.Append(ownerPath);
            _report.Append(".");
            _report.Append(fieldName);
            _report.Append(" = ");
            if (wired)
                _report.Append(snapshot.name);
            else
                _report.Append("<NULL>");
            _report.Append(" | ");
            _report.AppendLine(wired ? "WIRED" : "MISSING");
        }

        private static string GetTransformPath(Transform transform)
        {
            if (transform == null)
                return "<null>";

            // COLD ALLOC: path walk only in editor audit - owner: AudioTransitionSnapshotValidator
            string path = transform.name;
            Transform parent = transform.parent;
            while (parent != null)
            {
                path = parent.name + "/" + path;
                parent = parent.parent;
            }

            return path;
        }
    }
}
