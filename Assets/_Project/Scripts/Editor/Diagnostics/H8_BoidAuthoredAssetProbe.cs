using System;
using System.Globalization;
using System.Text;
using Hecton8.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Hecton8.EditorTools.Diagnostics
{
    /// <summary>
    /// Fails when <see cref="SargassumMicroFaunaBoids"/> is missing an authored asset that its own
    /// startup treats as fatal.
    /// <para>
    /// Runtime proof this is needed: in the world boot captured in <c>Logs/gamma_proof.log</c>,
    /// <c>neutralAbyssalFlowTexture</c> was unassigned, so <c>EnsureBuffers</c> logged
    /// "Runtime texture fallback generation is forbidden", called <c>DisableComputeDispatch</c>, and
    /// returned before allocating any of its six managed caches or its vault storage. The swarm was
    /// inert for the whole session and threw nothing - the only symptom was two error lines in a
    /// 1.39 MB log, at Awake and again at OnEnable.
    /// </para>
    /// <para>
    /// Edit Mode only, so it costs a scene open rather than a Play Mode session. Exits non-zero when
    /// a fatal-class asset is missing, which makes it usable as a gate rather than a report.
    /// </para>
    /// </summary>
    public static class H8_BoidAuthoredAssetProbe
    {
        private const string SceneArgument = "-h8Scene";
        private const string DefaultScenePath = "Assets/_Project/Scenes/02_HECTON_WORLD.unity";

        /// <summary>
        /// Serialized fields whose absence provably disables the system, taken from the guards in
        /// SargassumMicroFaunaBoids itself rather than guessed: neutralAbyssalFlowTexture aborts
        /// EnsureBuffers (:2836), and boidCompute gates every dispatch (:2203).
        /// </summary>
        private static readonly string[] FatalAssetFields =
        {
            "neutralAbyssalFlowTexture",
            "boidCompute",
            "boidMesh",
        };

        [MenuItem("Hecton8/Diagnostics/Boid Authored Assets")]
        public static void RunFromMenu()
        {
            Execute(DefaultScenePath);
        }

        /// <summary>Batch entry point. Exits non-zero when a fatal-class authored asset is missing.</summary>
        public static void Run()
        {
            string scenePath = ReadStringArgument(SceneArgument, DefaultScenePath);
            EditorApplication.Exit(Execute(scenePath) ? 0 : 1);
        }

        private static bool Execute(string scenePath)
        {
            try
            {
                EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            }
            catch (Exception exception)
            {
                Debug.LogError(string.Format(
                    CultureInfo.InvariantCulture,
                    "[H8_BOIDASSETS] Could not open scene '{0}': {1}",
                    scenePath,
                    exception.Message));
                return false;
            }

            SargassumMicroFaunaBoids boids = UnityEngine.Object.FindAnyObjectByType<SargassumMicroFaunaBoids>(
                FindObjectsInactive.Include);

            if (boids == null)
            {
                // Not a pass. The runtime log proves this component boots in the world scene, so its
                // absence here means the probe is pointed at the wrong scene and proves nothing.
                Debug.LogError(string.Format(
                    CultureInfo.InvariantCulture,
                    "[H8_BOIDASSETS] INCONCLUSIVE - no SargassumMicroFaunaBoids in '{0}'.",
                    scenePath));
                return false;
            }

            Debug.Log(string.Format(
                CultureInfo.InvariantCulture,
                "[H8_BOIDASSETS] scene={0} component={1}",
                scenePath,
                boids.name));

            SerializedObject serialized = new SerializedObject(boids);
            int missingFatal = 0;
            StringBuilder missingNames = new StringBuilder();

            for (int i = 0; i < FatalAssetFields.Length; i++)
            {
                string fieldName = FatalAssetFields[i];
                SerializedProperty property = serialized.FindProperty(fieldName);
                if (property == null)
                {
                    // A renamed field would silently stop being checked, so say so out loud.
                    Debug.LogWarning(string.Format(
                        CultureInfo.InvariantCulture,
                        "[H8_BOIDASSETS] field '{0}' not found - renamed or removed; this gate no longer covers it.",
                        fieldName));
                    continue;
                }

                bool assigned = property.objectReferenceValue != null;
                Debug.Log(string.Format(
                    CultureInfo.InvariantCulture,
                    "[H8_BOIDASSETS] {0} = {1}",
                    fieldName,
                    assigned ? property.objectReferenceValue.name : "NULL"));

                if (assigned)
                    continue;

                missingFatal++;
                if (missingNames.Length > 0)
                    missingNames.Append(", ");

                missingNames.Append(fieldName);
            }

            int unassignedTotal = CountUnassignedObjectReferences(serialized);
            Debug.Log(string.Format(
                CultureInfo.InvariantCulture,
                "[H8_BOIDASSETS] unassigned object-reference fields on this component (all, incl. optional) = {0}",
                unassignedTotal));

            if (missingFatal > 0)
            {
                Debug.LogError(string.Format(
                    CultureInfo.InvariantCulture,
                    "[H8_BOIDASSETS] FATAL ASSETS MISSING: {0}. EnsureBuffers aborts before allocating its " +
                    "managed caches and vault storage, so the swarm runs inert with no exception. " +
                    "This is an asset/scene-wiring gap, not a code defect - the runtime refusal to " +
                    "fabricate the texture is deliberate policy.",
                    missingNames));
                return false;
            }

            Debug.Log("[H8_BOIDASSETS] OK - every fatal-class authored asset is assigned.");
            return true;
        }

        private static int CountUnassignedObjectReferences(SerializedObject serialized)
        {
            int unassigned = 0;
            SerializedProperty iterator = serialized.GetIterator();

            // Descend into the root once, then walk siblings only. Passing enterChildren:true on
            // every step would recurse through nested serialized data and inflate the count.
            if (!iterator.NextVisible(true))
                return 0;

            do
            {
                if (iterator.propertyType != SerializedPropertyType.ObjectReference)
                    continue;

                if (iterator.objectReferenceValue == null)
                    unassigned++;
            }
            while (iterator.NextVisible(false));

            return unassigned;
        }

        private static string ReadStringArgument(string argumentName, string fallbackValue)
        {
            // Fully qualified: this file sits under the Hecton8 namespace root, which contains a
            // Hecton8.Environment namespace that shadows System.Environment during name lookup.
            // Bare `Environment` here is CS0234 - the standing repo trap.
            string[] arguments = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < arguments.Length - 1; i++)
            {
                if (string.Equals(arguments[i], argumentName, StringComparison.OrdinalIgnoreCase))
                    return arguments[i + 1];
            }

            return fallbackValue;
        }
    }
}
