#if false
// ============================================================================
// HECTON-8 — HectonScatterOutput.cs
// Custom MapMagic 2.1.18 Output Node — Scatter Output for ScavengePopulator.
//
// PURPOSE:
//   Replaces standard ObjectsOutput / TreesOutput in the MapMagic graph.
//   Instead of writing to TerrainData (which causes GC spikes when read back),
//   this node feeds spawn positions directly to ScavengePopulator via
//   zero-allocation RegisterSpawnPoint() calls.
//
// HOW IT WORKS:
//   1. Receives TransitionsList from upstream scatter/adjust nodes.
//   2. On Apply, iterates all positions WITHOUT creating arrays.
//   3. Calls GlobalRegistry.ScavengePopulator.RegisterSpawnPoint() per position.
//   4. Nothing is written to TerrainData — this is a terminal node.
//
// INTEGRATION:
//   In MapMagic Graph Editor:
//     • Right-click → Hecton → Scatter Output
//     • Connect any scatter-producing node to the "Transitions In" inlet.
//     • The node appears as an output (pinned to right side of graph).
//
// ZERO GC:
//   • No array creation — iterates TransitionsList in-place.
//   • RegisterSpawnPoint accepts structs — stack allocated.
//   • Only GC cost is string generation for unique IDs (inside ScavengePopulator).
//
// MapMagic 2.1.18 COMPATIBILITY:
//   • Inherits from OutputGenerator (terminal node archetype).
//   • Implements IInlet<TransitionsList> (single input port).
//   • Provides nested ApplyData class implementing IApplyData.
//   • Uses TileData.area.active.worldPos/worldSize for coordinate mapping.
// ============================================================================

using UnityEngine;
using MapMagic.Core;
using MapMagic.Products;
using MapMagic.Nodes;
using Den.Tools;
using Den.Tools.Matrices;

namespace Hecton8.MapMagicIntegration
{
    [System.Serializable]
    [GeneratorMenu(
        menu     = "Hecton",
        name     = "Scatter Output",
        iconName = null,
        disengageable = true,
        helpLink = null,
        colorType = typeof(TransitionsList))]
    public sealed class HectonScatterOutput : OutputGenerator, IInlet<TransitionsList>
    {
        // ══════════════════════════════════════════════════════════
        //  OUTPUT GENERATOR OVERRIDES
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Called by MapMagic after generation is complete.
        /// Creates the ApplyData object that will be executed on the main thread.
        /// 
        /// This runs on a WORKER THREAD — do not access Unity API here.
        /// We only package data for later application.
        /// </summary>
        public override IApplyData GetApplyData(TileData data, StopToken stop)
        {
            if (stop != null && stop.stop) return null;

            // ── Read generated transitions from the product ──
            TransitionsList transitions = data.ReadInletProduct(this);

            if (transitions == null || transitions.count == 0)
                return null;

            // ── Determine chunk coordinate from tile data ──
            // data.area.active gives us the active rect in world units
            Vector2Int coord = new Vector2Int(
                Mathf.FloorToInt(data.area.active.worldPos.x / data.area.active.worldSize.x),
                Mathf.FloorToInt(data.area.active.worldPos.z / data.area.active.worldSize.z));

            // ── Package for main-thread apply ──
            return new HectonScatterApplyData(transitions, coord);
        }

        /// <summary>
        /// Output layer index. Since we don't write to TerrainData at all,
        /// we use a high number to avoid conflicts with built-in outputs.
        /// Each output type in MapMagic needs a unique layer number.
        /// </summary>
        public override int OutputLevel { get => 20; }

        /// <summary>
        /// Called when this output should be purged from the terrain.
        /// Since we don't write to TerrainData, we notify ScavengePopulator
        /// to despawn the chunk instead.
        /// </summary>
        public override void Purge(TileData data, Terrain terrain)
        {
            // ScavengePopulator handles its own culling via CullDistantChunks.
            // If explicit purge is needed per-tile, we could call:
            //   GlobalRegistry.ScavengePopulator?.ReloadChunk(coord);
            // But we don't have coord here easily, and culling handles it.
        }

        // ══════════════════════════════════════════════════════════
        //  APPLY DATA — Executes on Main Thread
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Carries transition data from worker thread to main thread.
        /// Apply() iterates all positions and feeds them to ScavengePopulator.
        /// 
        /// IMPORTANT: TransitionsList stores positions as arrays internally:
        ///   trns.list[i].pos   — Vector3 (world-space position)
        ///   trns.list[i].rotation — float (Y-axis rotation in degrees)  
        ///   trns.list[i].scale — Vector3 (scale)
        ///   trns.list[i].id    — int (prototype index)
        ///
        /// We iterate with direct index access — no foreach, no LINQ,
        /// no temporary arrays.
        /// </summary>
        private sealed class HectonScatterApplyData : IApplyData
        {
            private readonly TransitionsList _transitions;
            private readonly Vector2Int      _chunkCoord;

            public HectonScatterApplyData(TransitionsList transitions, Vector2Int chunkCoord)
            {
                _transitions = transitions;
                _chunkCoord  = chunkCoord;
            }

            /// <summary>
            /// Called on MAIN THREAD by MapMagic tile application pipeline.
            /// 
            /// Iterates all scatter positions and registers them with
            /// ScavengePopulator for time-sliced spawning.
            /// 
            /// ZERO GC: struct SpawnRequest, direct array indexing.
            /// </summary>
            public void Apply(Terrain terrain)
            {
                ScavengePopulator populator = GlobalRegistry.ScavengePopulator;
                if (populator == null)
                {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    Debug.LogError(
                        "[HectonScatterOutput] ScavengePopulator registry slot is null! " +
                        "Ensure ScavengePopulator exists in the scene.");
#endif
                    return;
                }

                if (_transitions == null) return;

                int count = _transitions.count;
                if (count == 0) return;

                // ── Pre-notify ScavengePopulator about incoming chunk ──
                // If this chunk was previously loaded, despawn old nodes first
                populator.PrepareChunkForReload(_chunkCoord, count);

                // ── Iterate all transitions ──
                // TransitionsList stores data in parallel arrays for SoA layout:
                //   _transitions.posArr    — Vector3[]
                //   _transitions.rotArr    — float[] (Y rotation in degrees)  
                //   _transitions.scaleArr  — Vector3[]
                for (int i = 0; i < count; i++)
                {
                    Vector3 pos = _transitions.posArr[i];

                    // Rotation: TransitionsList stores Y-axis rotation
                    float rotY = _transitions.rotArr != null && i < _transitions.rotArr.Length
                        ? _transitions.rotArr[i]
                        : 0f;
                    Quaternion rotation = Quaternion.Euler(0f, rotY, 0f);

                    // Scale
                    Vector3 scale = _transitions.scaleArr != null && i < _transitions.scaleArr.Length
                        ? _transitions.scaleArr[i]
                        : Vector3.one;

                    populator.RegisterSpawnPoint(
                        pos,
                        rotation,
                        scale,
                        _chunkCoord,
                        i);
                }
            }

            /// <summary>
            /// Cleanup. TransitionsList is managed by MapMagic,
            /// we don't own it — nothing to dispose.
            /// </summary>
            public int Resolution { get => 0; }
        }
    }
}
#endif
