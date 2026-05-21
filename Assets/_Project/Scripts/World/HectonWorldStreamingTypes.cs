using System;
using System.Runtime.InteropServices;
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
        public byte IsActive;
        public StructureType Type;
        public int StructureId;
        public Bounds Bounds;
    }

    /// <summary>
    /// Immutable terrain-hole snapshot record published to streaming consumers.
    /// </summary>
    [Serializable]
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct TerrainHoleStreamingRecord
    {
        [FieldOffset(0)]
        public Vector3 Position;
        [FieldOffset(12)]
        public float Radius;
        [FieldOffset(16)]
        public int HoleId;
        [FieldOffset(20)]
        public TerrainHoleSourceType SourceType;
        [FieldOffset(21)]
        private byte _pad0;
        [FieldOffset(22)]
        private byte _pad1;
        [FieldOffset(23)]
        private byte _pad2;
        [FieldOffset(24)]
        private byte _pad3;
        [FieldOffset(25)]
        private byte _pad4;
        [FieldOffset(26)]
        private byte _pad5;
        [FieldOffset(27)]
        private byte _pad6;
        [FieldOffset(28)]
        private byte _pad7;
        [FieldOffset(29)]
        private byte _pad8;
        [FieldOffset(30)]
        private byte _pad9;
        [FieldOffset(31)]
        private byte _pad10;
    }

    /// <summary>
    /// Immutable HLOD registry payload for distant large-structure rendering.
    /// </summary>
    [Serializable]
    [StructLayout(LayoutKind.Explicit, Size = 48)]
    public struct HLODData
    {
        [FieldOffset(0)]
        public Vector3 Center;
        [FieldOffset(12)]
        public Vector3 Size;
        [FieldOffset(24)]
        public float Fade01;
        [FieldOffset(28)]
        public int StructureId;
        [FieldOffset(32)]
        public StructureType Type;
        [FieldOffset(33)]
        private byte _pad0;
        [FieldOffset(34)]
        private byte _pad1;
        [FieldOffset(35)]
        private byte _pad2;
        [FieldOffset(36)]
        private byte _pad3;
        [FieldOffset(37)]
        private byte _pad4;
        [FieldOffset(38)]
        private byte _pad5;
        [FieldOffset(39)]
        private byte _pad6;
        [FieldOffset(40)]
        private byte _pad7;
        [FieldOffset(41)]
        private byte _pad8;
        [FieldOffset(42)]
        private byte _pad9;
        [FieldOffset(43)]
        private byte _pad10;
        [FieldOffset(44)]
        private byte _pad11;
        [FieldOffset(45)]
        private byte _pad12;
        [FieldOffset(46)]
        private byte _pad13;
        [FieldOffset(47)]
        private byte _pad14;
    }
}
