// ============================================================================
// HECTON-8 — HectonRockOutput.cs
// Custom MapMagic 2.1.18 Output Node for GPU-instanced rock placement.
//
// PATTERN (copied from ObjectsOutput):
//   Generate() → reads TransitionsList from inlet
//              → stores output + marks finalize
//   Finalize()  → static, aggregates all outputs of this type
//              → converts Transitions to Matrix4x4[]
//              → creates ApplyData, marks apply
//   ApplyData.Apply() → main thread, pushes to HectonRockManager
//   ClearApplied()    → main thread, unregisters chunk
//
// ZERO GAMEOBJECTS: All rendering via GPU Instancer, physics via ProximityColliderSystem.
// ============================================================================

using System;
using System.Collections.Generic;
using Hecton8.Core;
using UnityEngine;

using Den.Tools;
using MapMagic.Core;
using MapMagic.Products;
using MapMagic.Nodes;

namespace Hecton8.World
{
    [System.Serializable]
    [GeneratorMenu(
        menu = "Hecton8",
        name = "Rock Output (GPU)",
        section = 2,
        colorType = typeof(TransitionsList),
        iconName = "GeneratorIcons/ObjectsOut")]
    public sealed class HectonRockOutput : OutputGenerator, IInlet<TransitionsList>
    {
        public override (string, int) GetCodeFileLine() => GetCodeFileLineBase();

        // ══════════════════════════════════════════════════════════
        //  NODE PROPERTIES
        // ══════════════════════════════════════════════════════════

        [Tooltip("Layer ID matching HectonRockManager.RockLayerConfig.layerId.")]
        public int layerID = 0;

        public OutputLevel outputLevel = OutputLevel.Main;
        public override OutputLevel OutputLevel { get { return outputLevel; } }

        // ══════════════════════════════════════════════════════════
        //  GENERATE — runs on worker thread
        // ══════════════════════════════════════════════════════════

        public override void Generate(TileData data, StopToken stop)
        {
            if (stop != null && stop.stop) return;

            TransitionsList trns = data.ReadInletProduct(this);

            if (enabled)
            {
                data.StoreOutput(this, typeof(HectonRockOutput), this, trns);
                data.MarkFinalize(Finalize, stop);
            }
            else
            {
                data.RemoveFinalize(finalizeAction);
            }
        }

        // ══════════════════════════════════════════════════════════
        //  FINALIZE — runs on worker thread, aggregates all outputs
        // ══════════════════════════════════════════════════════════

        public static FinalizeAction finalizeAction = Finalize;

        private struct LayerBuildState
        {
            public int Count;
            public int WriteIndex;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            finalizeAction = Finalize;
        }

        public static void Finalize(TileData data, StopToken stop)
        {
            if (stop != null && stop.stop) return;

            // Count first, then fill exact arrays. This avoids per-layer List staging and ToArray copies.
            Dictionary<int, LayerBuildState> layerBuildStates = new Dictionary<int, LayerBuildState>();

            foreach ((HectonRockOutput output, TransitionsList trns, Den.Tools.Matrices.MatrixWorld biomeMask)
                in data.Outputs<HectonRockOutput, TransitionsList, Den.Tools.Matrices.MatrixWorld>(
                    typeof(HectonRockOutput), inSubs: true))
            {
                if (stop != null && stop.stop) return;
                if (trns == null) continue;
                if (biomeMask != null && biomeMask.IsEmpty()) continue;

                int lid = output.layerID;

                if (!layerBuildStates.TryGetValue(lid, out LayerBuildState state))
                    state = default;

                for (int t = 0; t < trns.count; t++)
                {
                    Transition trn = trns.arr[t];

                    // Skip objects outside active area
                    if (!data.area.active.Contains(trn.pos)) continue;

                    // Skip based on biome mask
                    if (biomeMask != null)
                    {
                        float maskVal = biomeMask.GetWorldValue(trn.pos.x, trn.pos.z);
                        if (maskVal < 0.5f) continue;
                    }

                    state.Count++;
                }

                layerBuildStates[lid] = state;
            }

            if (stop != null && stop.stop) return;

            // Compute chunk coordinate from tile area
            Vector2Int chunkCoord = Vector2Int.zero;
            if (data.area != null)
            {
                Vector2D worldPos = data.area.active.worldPos;
                Vector2D worldSize = data.area.active.worldSize;

                int cx = worldSize.x > 0.01 ? (int)Math.Round(worldPos.x / worldSize.x) : 0;
                int cz = worldSize.z > 0.01 ? (int)Math.Round(worldPos.z / worldSize.z) : 0;

                chunkCoord = new Vector2Int(cx, cz);
            }

            int activeLayerCount = 0;
            Dictionary<int, LayerBuildState>.Enumerator layerStateEnumerator = layerBuildStates.GetEnumerator();
            while (layerStateEnumerator.MoveNext())
            {
                KeyValuePair<int, LayerBuildState> kvp = layerStateEnumerator.Current;
                if (kvp.Value.Count > 0)
                    activeLayerCount++;
            }

            // Create apply data for each non-empty layer.
            HectonRockApplyData applyData = new HectonRockApplyData
            {
                chunkCoord = chunkCoord,
                layerMatrices = new Dictionary<int, Matrix4x4[]>(activeLayerCount)
            };

            layerStateEnumerator = layerBuildStates.GetEnumerator();
            while (layerStateEnumerator.MoveNext())
            {
                KeyValuePair<int, LayerBuildState> kvp = layerStateEnumerator.Current;
                if (kvp.Value.Count > 0)
                    applyData.layerMatrices[kvp.Key] = new Matrix4x4[kvp.Value.Count];
            }

            foreach ((HectonRockOutput output, TransitionsList trns, Den.Tools.Matrices.MatrixWorld biomeMask)
                in data.Outputs<HectonRockOutput, TransitionsList, Den.Tools.Matrices.MatrixWorld>(
                    typeof(HectonRockOutput), inSubs: true))
            {
                if (stop != null && stop.stop) return;
                if (trns == null) continue;
                if (biomeMask != null && biomeMask.IsEmpty()) continue;

                int lid = output.layerID;
                if (!applyData.layerMatrices.TryGetValue(lid, out Matrix4x4[] matrices) ||
                    !layerBuildStates.TryGetValue(lid, out LayerBuildState state) ||
                    matrices.Length == 0)
                {
                    continue;
                }

                for (int t = 0; t < trns.count; t++)
                {
                    Transition trn = trns.arr[t];

                    if (!data.area.active.Contains(trn.pos)) continue;

                    if (biomeMask != null)
                    {
                        float maskVal = biomeMask.GetWorldValue(trn.pos.x, trn.pos.z);
                        if (maskVal < 0.5f) continue;
                    }

                    matrices[state.WriteIndex++] = Matrix4x4.TRS(trn.pos, trn.rotation, trn.scale);
                }

                layerBuildStates[lid] = state;
            }


            data.MarkApply(applyData);
        }

        // ══════════════════════════════════════════════════════════
        //  APPLY DATA — runs on main thread
        // ══════════════════════════════════════════════════════════

        public class HectonRockApplyData : IApplyData
        {
            public Vector2Int chunkCoord;
            public Dictionary<int, Matrix4x4[]> layerMatrices;

            public void Apply(UnityEngine.Terrain terrain)
            {
                HectonRockManager manager = GlobalRegistry.RockManager;
                if (manager == null)
                {
                    Hecton8.Core.H8Debug.LogError("[HectonRockOutput] GlobalRegistry.RockManager is null. " +
                                   "Cannot register rock chunk.");
                    return;
                }

                manager.UnregisterChunk(chunkCoord);

                if (layerMatrices == null || layerMatrices.Count == 0)
                {
                    return;
                }

                Dictionary<int, Matrix4x4[]>.Enumerator layerMatrixEnumerator = layerMatrices.GetEnumerator();
                while (layerMatrixEnumerator.MoveNext())
                {
                    KeyValuePair<int, Matrix4x4[]> kvp = layerMatrixEnumerator.Current;
                    int layerId = kvp.Key;
                    Matrix4x4[] matrices = kvp.Value;

                    if (matrices != null && matrices.Length > 0)
                    {
                        manager.RegisterChunk(layerId, chunkCoord, matrices);
                    }
                }
            }

            public int Resolution { get { return 0; } }
        }

        // ══════════════════════════════════════════════════════════
        //  CLEAR APPLIED — runs on main thread when tile unloads
        // ══════════════════════════════════════════════════════════

        public override void ClearApplied(TileData data, UnityEngine.Terrain terrain)
        {
            HectonRockManager manager = GlobalRegistry.RockManager;
            if (manager == null) return;

            // Compute chunk coordinate same way as in Finalize
            Vector2Int chunkCoord = Vector2Int.zero;
            if (data.area != null)
            {
                Vector2D worldPos = data.area.active.worldPos;
                Vector2D worldSize = data.area.active.worldSize;

                int cx = worldSize.x > 0.01 ? (int)Math.Round(worldPos.x / worldSize.x) : 0;
                int cz = worldSize.z > 0.01 ? (int)Math.Round(worldPos.z / worldSize.z) : 0;

                chunkCoord = new Vector2Int(cx, cz);
            }

            manager.UnregisterChunk(chunkCoord);
        }
    }
}
