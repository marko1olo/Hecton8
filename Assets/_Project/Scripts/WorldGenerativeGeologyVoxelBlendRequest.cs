using UnityEngine;

namespace Hecton8.World
{
    [System.Serializable]
    public struct WorldGenerativeGeologyVoxelBlendRequest
    {
        public long runtimeKey;
        public string familyId;
        public string geologyProfileId;
        public WorldGenerativeGeologyProfile.ShapeArchetype archetype;
        public Vector3 center;
        public Vector3 size;
        public Quaternion rotation;
        public float weight;
        public float playerDistance;
        public float planWeight;
        public WorldGenerativeGeologyProfile.CaveBlendMode caveBlendMode;
        public WorldChunkCoordinate chunkCoord;
        public bool hasMacroZone;
        public WorldMacroZoneCoordinate macroZoneCoord;
    }
}
