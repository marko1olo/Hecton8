using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Hecton8.AI;
using Hecton8.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Hecton8.EditorTools.Diagnostics
{
    /// <summary>
    /// Answers one question about fauna spawning: does the biome id produced by the terrain bridge
    /// actually land in the id space the authored fauna datasets are keyed by?
    /// <para>
    /// <see cref="MapMagicBridge.TryGetBiomeIndex"/> yields a 0-based dominant alphamap layer.
    /// <see cref="FaunaDirector"/> feeds that value straight into a dictionary keyed by
    /// <see cref="FaunaBiomeData.biomeIndex"/>, which the authoring pass writes from the 1-based
    /// <c>HectonBiomeMatrixProfile.matrixIndex</c>. If those two bases disagree, every miss is
    /// silent: <c>TryGetValue</c> simply fails and that tick spawns nothing. Nothing logs, nothing
    /// throws, and the world reads as "quiet" rather than "broken".
    /// </para>
    /// <para>
    /// This runs in Edit Mode against the serialized scene, so it reports the real wiring rather
    /// than defaults. It deliberately reports BOTH the direct-hit count and the +1-shifted hit
    /// count, so the numbers can refute the off-by-one hypothesis instead of only confirming it.
    /// </para>
    /// </summary>
    public static class H8_FaunaBiomeIdSpaceProbe
    {
        private const string SceneArgument = "-h8Scene";
        private const string DefaultScenePath = "Assets/_Project/Scenes/02_HECTON_WORLD.unity";
        private const int BiomeMatrixLayerCount = 108;

        [MenuItem("Hecton8/Diagnostics/Fauna Biome Id Space")]
        public static void RunFromMenu()
        {
            Execute(DefaultScenePath);
        }

        /// <summary>Batch entry point. Exits non-zero when the two id spaces cannot agree.</summary>
        public static void Run()
        {
            string scenePath = ReadStringArgument(SceneArgument, DefaultScenePath);
            bool consistent = Execute(scenePath);
            EditorApplication.Exit(consistent ? 0 : 1);
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
                    "[H8_FAUNABIOMEID] Could not open scene '{0}': {1}",
                    scenePath,
                    exception.Message));
                return false;
            }

            Debug.Log(string.Format(
                CultureInfo.InvariantCulture,
                "[H8_FAUNABIOMEID] scene={0}",
                scenePath));

            MapMagicRuntimeBridge bridge = UnityEngine.Object.FindAnyObjectByType<MapMagicRuntimeBridge>(
                FindObjectsInactive.Include);
            FaunaDirector director = UnityEngine.Object.FindAnyObjectByType<FaunaDirector>(
                FindObjectsInactive.Include);

            if (bridge == null || director == null)
            {
                Debug.LogError(string.Format(
                    CultureInfo.InvariantCulture,
                    "[H8_FAUNABIOMEID] INCONCLUSIVE - bridge={0} faunaDirector={1} in this scene. " +
                    "Cannot compare id spaces without both.",
                    bridge == null ? "MISSING" : "found",
                    director == null ? "MISSING" : "found"));
                return false;
            }

            // Serialized reads: maxBiomeCount and biomeDatasets are private [SerializeField] with no
            // public accessor, so the scene value is only reachable through SerializedObject.
            SerializedObject bridgeObject = new SerializedObject(bridge);
            int maxBiomeCount = ReadInt(bridgeObject, "maxBiomeCount", 8);
            bool sandboxOnly = ReadBool(bridgeObject, "sandboxProceduralTerrainOnly", false);
            bool sandboxMatrixLayers = ReadBool(bridgeObject, "sandboxUseBiomeMatrixAlphamapLayers", true);

            // Mirrors MapMagicRuntimeBridge.TryGetBiomeIndex search-limit derivation.
            int configuredSearchLimit = sandboxOnly && sandboxMatrixLayers
                ? Mathf.Max(maxBiomeCount, BiomeMatrixLayerCount)
                : maxBiomeCount;
            int producedUpperBound = Mathf.Max(1, configuredSearchLimit);

            Debug.Log(string.Format(
                CultureInfo.InvariantCulture,
                "[H8_FAUNABIOMEID] bridge maxBiomeCount={0} sandboxProceduralTerrainOnly={1} " +
                "sandboxUseBiomeMatrixAlphamapLayers={2} => produced index domain = 0..{3} (0-based layer)",
                maxBiomeCount,
                sandboxOnly,
                sandboxMatrixLayers,
                producedUpperBound - 1));

            int terrainLayers = ResolveMinTerrainAlphamapLayers(out int terrainCount);
            if (terrainCount > 0)
            {
                Debug.Log(string.Format(
                    CultureInfo.InvariantCulture,
                    "[H8_FAUNABIOMEID] scene terrains={0} minAlphamapLayers={1} " +
                    "(runtime searchLimit = min(alphamapLayers, {2}))",
                    terrainCount,
                    terrainLayers,
                    producedUpperBound));
            }
            else
            {
                Debug.Log(
                    "[H8_FAUNABIOMEID] scene terrains=0 - alphamap layer count is generated at runtime, " +
                    "so the effective search limit is not observable in Edit Mode.");
            }

            HashSet<int> authoredKeys = new HashSet<int>();
            int datasetCount = ReadAuthoredBiomeKeys(director, authoredKeys, out int nullSlots, out int minKey, out int maxKey);

            if (datasetCount <= 0)
            {
                Debug.LogError(
                    "[H8_FAUNABIOMEID] INCONCLUSIVE - FaunaDirector.biomeDatasets is empty in this scene, " +
                    "so no fauna can spawn regardless of id space.");
                return false;
            }

            Debug.Log(string.Format(
                CultureInfo.InvariantCulture,
                "[H8_FAUNABIOMEID] faunaDatasets={0} nullSlots={1} distinctKeys={2} keyRange={3}..{4} " +
                "(FaunaBiomeData.biomeIndex, authored from matrixIndex)",
                datasetCount,
                nullSlots,
                authoredKeys.Count,
                minKey,
                maxKey));

            // The comparison. directHits is what the shipping code does today; shiftedHits is what it
            // would do if the documented layer->matrixId (+1) conversion were applied.
            int directHits = 0;
            int shiftedHits = 0;
            for (int producedIndex = 0; producedIndex < producedUpperBound; producedIndex++)
            {
                if (authoredKeys.Contains(producedIndex))
                    directHits++;

                if (authoredKeys.Contains(producedIndex + 1))
                    shiftedHits++;
            }

            Debug.Log(string.Format(
                CultureInfo.InvariantCulture,
                "[H8_FAUNABIOMEID] over produced domain 0..{0}: directHits={1} shiftedHits(+1)={2} " +
                "unreachableAuthoredBiomes={3}/{4}",
                producedUpperBound - 1,
                directHits,
                shiftedHits,
                Mathf.Max(0, authoredKeys.Count - Mathf.Max(directHits, shiftedHits)),
                authoredKeys.Count));

            Debug.Log(string.Format(
                CultureInfo.InvariantCulture,
                "[H8_FAUNABIOMEID] layer 0 (also the documented failure fallback) {0} a fauna dataset today.",
                authoredKeys.Contains(0) ? "MATCHES" : "MATCHES NO"));

            bool offByOne = shiftedHits > directHits;
            if (offByOne)
            {
                Debug.LogError(string.Format(
                    CultureInfo.InvariantCulture,
                    "[H8_FAUNABIOMEID] ID SPACE MISMATCH - the +1 shift wins ({0} vs {1}). " +
                    "FaunaDirector keys authored 1-based matrix ids with a 0-based alphamap layer. " +
                    "Misses are silent: TryGetValue fails and the tick spawns nothing.",
                    shiftedHits,
                    directHits));
            }
            else
            {
                Debug.Log(string.Format(
                    CultureInfo.InvariantCulture,
                    "[H8_FAUNABIOMEID] NO SHIFT ADVANTAGE - direct={0} shifted={1}. " +
                    "The off-by-one hypothesis is NOT supported by this scene's wiring.",
                    directHits,
                    shiftedHits));
            }

            Debug.Log("[H8_FAUNABIOMEID] DONE");
            return !offByOne;
        }

        private static int ReadAuthoredBiomeKeys(
            FaunaDirector director,
            HashSet<int> authoredKeys,
            out int nullSlots,
            out int minKey,
            out int maxKey)
        {
            nullSlots = 0;
            minKey = int.MaxValue;
            maxKey = int.MinValue;

            SerializedObject directorObject = new SerializedObject(director);
            SerializedProperty datasets = directorObject.FindProperty("biomeDatasets");
            if (datasets == null || !datasets.isArray)
                return 0;

            for (int i = 0; i < datasets.arraySize; i++)
            {
                FaunaBiomeData data = datasets.GetArrayElementAtIndex(i).objectReferenceValue as FaunaBiomeData;
                if (data == null)
                {
                    nullSlots++;
                    continue;
                }

                authoredKeys.Add(data.biomeIndex);
                minKey = Mathf.Min(minKey, data.biomeIndex);
                maxKey = Mathf.Max(maxKey, data.biomeIndex);
            }

            if (authoredKeys.Count == 0)
            {
                minKey = 0;
                maxKey = 0;
            }

            return datasets.arraySize;
        }

        private static int ResolveMinTerrainAlphamapLayers(out int terrainCount)
        {
            Terrain[] terrains = UnityEngine.Object.FindObjectsByType<Terrain>(FindObjectsInactive.Include);

            terrainCount = terrains != null ? terrains.Length : 0;
            if (terrainCount == 0)
                return 0;

            int minLayers = int.MaxValue;
            for (int i = 0; i < terrains.Length; i++)
            {
                TerrainData data = terrains[i] != null ? terrains[i].terrainData : null;
                if (data == null)
                    continue;

                minLayers = Mathf.Min(minLayers, data.alphamapLayers);
            }

            return minLayers == int.MaxValue ? 0 : minLayers;
        }

        private static int ReadInt(SerializedObject serializedObject, string propertyPath, int fallbackValue)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyPath);
            return property != null ? property.intValue : fallbackValue;
        }

        private static bool ReadBool(SerializedObject serializedObject, string propertyPath, bool fallbackValue)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyPath);
            return property != null ? property.boolValue : fallbackValue;
        }

        private static string ReadStringArgument(string argumentName, string fallbackValue)
        {
            // Fully qualified: this file sits under the Hecton8 namespace root, which contains a
            // Hecton8.Environment namespace that shadows System.Environment during name lookup.
            // Bare `Environment` here is CS0234 - the standing repo trap 86df04453 fixed once already.
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
