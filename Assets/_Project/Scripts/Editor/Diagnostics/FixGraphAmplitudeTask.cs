using UnityEditor;
using UnityEngine;
using MapMagic.Nodes;
using MapMagic.Nodes.MatrixGenerators;
using System.Linq;

namespace MapMagic.Editor.Diagnostics
{
    /// <summary>
    /// One-shot surgical mutation: flips a single authored Blend200 layer from `max` to `add` in the
    /// sandbox biome graph. It is reachable only by reflection from a batch script - there is no MenuItem -
    /// and it changes the authored world's terrain amplitude, so every branch now speaks and the exit code
    /// distinguishes "changed it", "already correct", and "could not find what I was told to change".
    ///
    /// What it used to do wrong, all four of which produced a success report:
    ///   - if the Blend200 with the hardcoded id was not found, it did nothing at all, logged nothing, and
    ///     exited 0. A silent no-op is indistinguishable from a successful mutation.
    ///   - if the graph failed to load, `graph.generators` threw, the exception went to a file under
    ///     another agent's private brain directory that no Unity log reader sees, and the finally block
    ///     still exited 0.
    ///   - `layers[1]` was indexed with no length check.
    ///   - it called AssetDatabase.SaveAssets(), which commits EVERY dirty asset in the project, not just
    ///     this graph. A concurrent authoring session's in-flight asset edits would be saved along with it.
    ///     Now uses SaveAssetIfDirty on the one graph it touched.
    /// </summary>
    public static class FixGraphAmplitudeTask
    {
        private const string GraphPath =
            "Assets/_Project/Data/World/Sandbox/HECTON_SANDBOX_BIOMES_MAPMAGIC_GRAPH.asset";

        private const ulong TargetBlendId = 17008246414020444181UL;
        private const int TargetLayerIndex = 1;

        private const int ExitChanged = 0;
        private const int ExitFailed = 2;

        public static void Fix()
        {
            try
            {
                Graph graph = AssetDatabase.LoadAssetAtPath<Graph>(GraphPath);
                if (graph == null)
                {
                    Debug.LogError(
                        $"[FixGraphAmplitudeTask] REFUSED: no Graph asset at '{GraphPath}'. Nothing was " +
                        "changed. If the graph moved, this task's hardcoded path is stale.");
                    EditorApplication.Exit(ExitFailed);
                    return;
                }

                Blend200 blend = graph.generators.FirstOrDefault(g => g.id == TargetBlendId) as Blend200;
                if (blend == null)
                {
                    Debug.LogError(
                        $"[FixGraphAmplitudeTask] REFUSED: no Blend200 with id {TargetBlendId} in " +
                        $"'{GraphPath}'. Nothing was changed. This is the branch that used to exit 0 in " +
                        "silence, so a stale node id looked exactly like a successful fix.");
                    EditorApplication.Exit(ExitFailed);
                    return;
                }

                if (blend.layers == null || blend.layers.Length <= TargetLayerIndex)
                {
                    int actual = blend.layers?.Length ?? 0;
                    Debug.LogError(
                        $"[FixGraphAmplitudeTask] REFUSED: Blend200 {TargetBlendId} has {actual} layer(s), " +
                        $"so layer index {TargetLayerIndex} does not exist. Nothing was changed.");
                    EditorApplication.Exit(ExitFailed);
                    return;
                }

                Blend200.BlendAlgorithm before = blend.layers[TargetLayerIndex].algorithm;
                if (before == Blend200.BlendAlgorithm.add)
                {
                    Debug.Log(
                        $"[FixGraphAmplitudeTask] ALREADY CORRECT: Blend200 {TargetBlendId} layer " +
                        $"{TargetLayerIndex} is already '{Blend200.BlendAlgorithm.add}'. Nothing was " +
                        "written, and no asset was marked dirty.");
                    EditorApplication.Exit(ExitChanged);
                    return;
                }

                blend.layers[TargetLayerIndex].algorithm = Blend200.BlendAlgorithm.add;
                EditorUtility.SetDirty(graph);

                // Deliberately NOT AssetDatabase.SaveAssets(): that flushes every dirty asset in the
                // project, including another session's unfinished authoring work.
                AssetDatabase.SaveAssetIfDirty(graph);

                Debug.Log(
                    $"[FixGraphAmplitudeTask] CHANGED Blend200 {TargetBlendId} layer {TargetLayerIndex} " +
                    $"algorithm '{before}' -> '{Blend200.BlendAlgorithm.add}' in '{GraphPath}'. This alters " +
                    "authored terrain amplitude; regenerate before judging any height evidence.");
                EditorApplication.Exit(ExitChanged);
            }
            catch (System.Exception ex)
            {
                Debug.LogError("[FixGraphAmplitudeTask] FAILED mid-mutation, graph state is unverified: " + ex);
                EditorApplication.Exit(ExitFailed);
            }
        }
    }
}
