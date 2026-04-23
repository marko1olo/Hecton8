using UnityEngine;
using Hecton8.Core;

namespace Hecton8.World
{
    [System.Serializable]
    public struct WorldGenerativeGeologySeamPlan
    {
        public long runtimeKey;
        public string familyId;
        public string geologyProfileId;
        public string composition;
        public WorldGenerativeGeologyProfile.ShapeArchetype archetype;
        public WorldGenerativeGeologyProfile.TerrainSeamMode terrainSeamMode;
        public WorldGenerativeGeologyProfile.CaveBlendMode caveBlendMode;
        public WorldStreamingLayer streamingLayer;
        public int chunkX;
        public int chunkZ;
        public bool hasMacroZone;
        public int macroZoneX;
        public int macroZoneZ;
        public Vector3 absoluteUniversePosition;
        public Quaternion worldRotation;
        public Vector3 worldScale;
        public float playerDistance;
        public bool hasTerrainSample;
        public float absoluteTerrainHeight;
        public float terrainDelta;
        public float seamBlendRadius;
        public float suggestedTerrainRaise;
        public float suggestedTerrainCut;
        public int suggestedDebrisCount;
        public float slopeDegrees;
        public float curvature;
        public float caveProximity;
        public float ridgeSignal;
        public float canyonSignal;
        public float compositionPotential;
        public float terrainBlendWeight;
        public float caveBlendWeight;
        public float debrisWeight;
        public float planWeight;
        public Vector3 absoluteVoxelVolumeCenter;
        public Vector3 voxelVolumeSize;

        public bool RequiresTerrainBlend => terrainSeamMode != WorldGenerativeGeologyProfile.TerrainSeamMode.None && terrainBlendWeight > 0.01f;
        public bool RequiresVoxelBlend => caveBlendMode != WorldGenerativeGeologyProfile.CaveBlendMode.None && caveBlendWeight > 0.01f;
        public bool RequiresDebrisSeam => suggestedDebrisCount > 0 && debrisWeight > 0.01f;
        public WorldChunkCoordinate ChunkCoord => new WorldChunkCoordinate(chunkX, chunkZ);
        public WorldMacroZoneCoordinate MacroZoneCoord => new WorldMacroZoneCoordinate(macroZoneX, macroZoneZ);
        public Vector3 RuntimeWorldPosition => HectonFloatingOrigin.ToRuntimePosition(absoluteUniversePosition);
        public Vector3 RuntimeVoxelVolumeCenter => HectonFloatingOrigin.ToRuntimePosition(absoluteVoxelVolumeCenter);
        public float RuntimeTerrainHeight => hasTerrainSample
            ? absoluteTerrainHeight - HectonFloatingOrigin.CurrentTotalOffset.y
            : RuntimeWorldPosition.y;
        public Vector3 TerrainContactPosition => new Vector3(RuntimeWorldPosition.x, RuntimeTerrainHeight, RuntimeWorldPosition.z);
    }
}
