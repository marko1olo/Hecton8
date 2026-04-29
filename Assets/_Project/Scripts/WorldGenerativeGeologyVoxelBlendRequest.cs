using UnityEngine;
using Hecton8.Core;

namespace Hecton8.World
{
    [System.Serializable]
    public struct WorldGenerativeGeologyVoxelBlendRequest
    {
        public long runtimeKey;
        public string familyId;
        public string geologyProfileId;
        public WorldGenerativeGeologyProfile.ShapeArchetype archetype;
        public Vector3 absoluteUniverseCenter;
        public Vector3 size;
        public Quaternion rotation;
        public float weight;
        public float playerDistance;
        public float planWeight;
        public bool hasTerrainSample;
        public Vector3 absoluteTerrainContactPosition;
        public float slopeDegrees;
        public float seamBlendRadius;
        public float suggestedTerrainCut;
        public WorldGenerativeGeologyProfile.CaveBlendMode caveBlendMode;
        public WorldChunkCoordinate chunkCoord;
        public bool hasMacroZone;
        public WorldMacroZoneCoordinate macroZoneCoord;
        public Vector3 RuntimeCenter => HectonFloatingOrigin.ToRuntimePosition(absoluteUniverseCenter);
        public Vector3 RuntimeTerrainContactPosition => HectonFloatingOrigin.ToRuntimePosition(absoluteTerrainContactPosition);
    }
}
