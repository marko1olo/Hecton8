using System;
using UnityEngine;

namespace Hecton8.World
{
    /// <summary>
    /// Identifies the runtime owner that authored a terrain-hole mask.
    /// </summary>
    public enum TerrainHoleSourceType : byte
    {
        CaveEntrance = 0,
        MegaWreckInterior = 1
    }

    /// <summary>
    /// Identifies the high-level runtime structure category used by threat and navigation systems.
    /// </summary>
    public enum StructureType : byte
    {
        BaseModule = 0,
        MegaWreck = 1,
        VoxelCave = 2,
        HazardEmitter = 3
    }

    /// <summary>
    /// Identifies the traversal classification of an abyssal nav node.
    /// </summary>
    public enum NavNodeType : byte
    {
        Water = 0,
        Interior = 1
    }

    /// <summary>
    /// Immutable runtime state describing the currently active streamed artificial interior around the player.
    /// </summary>
    [Serializable]
    public struct ArtificialInteriorState
    {
        public bool IsActive;
        public StructureType Type;
        public int StructureId;
        public Bounds Bounds;
    }

    /// <summary>
    /// Immutable terrain-hole snapshot record published to streaming consumers.
    /// </summary>
    [Serializable]
    public struct TerrainHoleStreamingRecord
    {
        public int HoleId;
        public Vector3 Position;
        public float Radius;
        public TerrainHoleSourceType SourceType;
    }

    /// <summary>
    /// Immutable HLOD registry payload for distant large-structure rendering.
    /// </summary>
    [Serializable]
    public struct HLODData
    {
        public int StructureId;
        public StructureType Type;
        public Vector3 Center;
        public Vector3 Size;
        public float Fade01;
    }
}
