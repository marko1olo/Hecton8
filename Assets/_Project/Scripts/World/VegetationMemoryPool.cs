using System;
using Hecton8.Environment;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.World
{
    public sealed partial class HectonMapMagicVegetationBridge
    {
        private struct PoolBlock
        {
            public int Offset;
            public int Length;
        }

        private struct NativeChunkPool : IDisposable
        {
            public NativeArray<Matrix4x4> Matrices;
            public NativeArray<HectonVegetationInstanceData> Metadata;
            public NativeArray<int> Types;
            public NativeArray<int> SemanticTypes;
            public NativeArray<byte> BiomeLayers;
            public NativeArray<float> EdgeDistances;
            public NativeArray<Vector2> FlowDirections;
            public NativeArray<Vector3> FlowVectors;
            public int Capacity;

            public void Dispose()
            {
                Dispose(default);
            }

            public void Dispose(JobHandle dependency)
            {
                DisposeNativeArray(ref Matrices, dependency);
                DisposeNativeArray(ref Metadata, dependency);
                DisposeNativeArray(ref Types, dependency);
                DisposeNativeArray(ref SemanticTypes, dependency);
                DisposeNativeArray(ref BiomeLayers, dependency);
                DisposeNativeArray(ref EdgeDistances, dependency);
                DisposeNativeArray(ref FlowDirections, dependency);
                DisposeNativeArray(ref FlowVectors, dependency);
                Capacity = 0;
            }
        }

        private struct ActiveAggregateNativeBufferSet : IDisposable
        {
            public NativeArray<Matrix4x4> Matrices;
            public NativeArray<HectonVegetationInstanceData> Metadata;
            public NativeArray<int> Types;
            public NativeArray<int> SemanticTypes;
            public NativeArray<byte> BiomeLayers;
            public NativeArray<Vector2> FlowDirections;
            public NativeArray<Vector3> FlowVectors;

            public void Dispose()
            {
                Dispose(default);
            }

            public void Dispose(JobHandle dependency)
            {
                DisposeNativeArray(ref Matrices, dependency);
                DisposeNativeArray(ref Metadata, dependency);
                DisposeNativeArray(ref Types, dependency);
                DisposeNativeArray(ref SemanticTypes, dependency);
                DisposeNativeArray(ref BiomeLayers, dependency);
                DisposeNativeArray(ref FlowDirections, dependency);
                DisposeNativeArray(ref FlowVectors, dependency);
            }
        }

        private struct VegetationNativeMemory : IDisposable
        {
            public NativeArray<VegetationDensityChunkRecord> DensityQueryChunksNative;
            public NativeArray<float3> DensityQueryGridNative;
            public NativeArray<float2> ThreatAttractorGridNative;
            public NativeArray<VegetationDensityChunkRecord> DensityQueryChunksScratchNative;
            public NativeArray<float3> DensityQueryGridScratchNative;
            public NativeArray<float2> ThreatAttractorGridScratchNative;
            public NativeArray<VegetationDensityChunkRecord> ThreatSamplingChunksNative;
            public NativeArray<float2> ThreatSamplingAttractorGridNative;
            public NativeArray<float3> FlowSamplingDensityGridNative;
            public NativeArray<float> FlowNavSupportGridNative;
            public NativeArray<float> EcosystemThreatGridCurrentNative;
            public NativeArray<float> EcosystemThreatGridNextNative;
            public NativeArray<byte> EcosystemThreatGridCompressedCurrentNative;
            public NativeArray<byte> EcosystemThreatGridCompressedNextNative;
            public NativeArray<byte> EcosystemThreatVoxelCurrentNative;
            public NativeArray<byte> EcosystemThreatVoxelNextNative;
            public NativeArray<byte> EcosystemThreatEchoCurrentNative;
            public NativeArray<byte> EcosystemThreatEchoNextNative;
            public NativeArray<float2> EcosystemFlowFieldCurrentNative;
            public NativeArray<float2> EcosystemFlowFieldNextNative;
            public NativeArray<SwarmWakeImpulse> SwarmWakeImpulseNative;
            public NativeArray<float> AbyssalThermalGridNative;
            public NativeArray<float> AbyssalThermalGridNextNative;
            public NativeArray<float3> AbyssalFlowVolumeCurrentNative;
            public NativeArray<float3> AbyssalFlowVolumeNextNative;
            public NativeArray<float> CanopyHeightGridNative;
            public NativeArray<TerrainHoleRecord> TerrainHoleRecordsNative;
            public NativeArray<TerrainHoleStreamingRecord> TerrainHoleStreamingRecordsNative;
            public NativeArray<ArtificialStructureRecord> ArtificialStructureRecordsNative;
            public NativeParallelMultiHashMap<int, int> ArtificialStructureHashFrontNative;
            public NativeParallelMultiHashMap<int, int> ArtificialStructureHashBackNative;
            public NativeParallelMultiHashMap<int, int> ThreatSamplingChunkHashFrontNative;
            public NativeParallelMultiHashMap<int, int> ThreatSamplingChunkHashBackNative;
            public NativeArray<Vector3> AbyssalAnchorPositionsNative;
            public NativeArray<Vector3> AbyssalNavNodeSnapshotNative;
            public NativeArray<Vector3> AbyssalNavConduitVectorsSnapshotNative;
            public NativeArray<float> AbyssalNavConduitStrengthSnapshotNative;
            public NativeArray<byte> AbyssalNavNodeTypesSnapshotNative;
            public NativeParallelMultiHashMap<int, int> AbyssalNavGraphHashNative;
            public NativeList<Vector3> AbyssalNavNodes;
            public NativeArray<Vector3> AbyssalPathSnapshotNative;
            public NativeList<Vector3> AbyssalPathRawResultNative;
            public NativeList<Vector3> AbyssalPathResultNative;
            public NativeArray<int> AbyssalPathParentsNative;
            public NativeArray<float> AbyssalPathGScoreNative;
            public NativeArray<float> AbyssalPathFScoreNative;
            public NativeArray<byte> AbyssalPathClosedFlagsNative;
            public NativeArray<int> AbyssalPathHeapNodesNative;
            public NativeArray<int> AbyssalPathHeapPositionsNative;
            public NativeArray<PredatorFearNodeSnapshot> PredatorFearNodesSnapshotNative;
            public NativeArray<HLODData> HlodRegistrySnapshotNative;
            public NativeArray<HLODData> VisibleHlodSnapshotNative;
            public NativeArray<byte> HlodVisibleFlagsNative;
            public NativeArray<float4> HlodFrustumPlanesNative;
            public NativeArray<ChunkSliceMoveRecord> SurfaceDefragMovesNative;
            public NativeArray<ChunkSliceMoveRecord> UnderwaterDefragMovesNative;
            public NativeArray<ActiveAggregateCopyRecord> SurfaceAggregateCopyRecordsNative;
            public NativeArray<ActiveAggregateCopyRecord> UnderwaterAggregateCopyRecordsNative;
            public NativeArray<MegaWreckStreamSection> MegaWreckStreamSnapshotNative;

            public void Dispose()
            {
                Dispose(default);
            }

            public void Dispose(JobHandle dependency)
            {
                DisposeNativeArray(ref DensityQueryChunksNative, dependency);
                DisposeNativeArray(ref DensityQueryGridNative, dependency);
                DisposeNativeArray(ref ThreatAttractorGridNative, dependency);
                DisposeNativeArray(ref DensityQueryChunksScratchNative, dependency);
                DisposeNativeArray(ref DensityQueryGridScratchNative, dependency);
                DisposeNativeArray(ref ThreatAttractorGridScratchNative, dependency);
                DisposeNativeArray(ref ThreatSamplingChunksNative, dependency);
                DisposeNativeArray(ref ThreatSamplingAttractorGridNative, dependency);
                DisposeNativeArray(ref FlowSamplingDensityGridNative, dependency);
                DisposeNativeArray(ref FlowNavSupportGridNative, dependency);
                DisposeNativeArray(ref EcosystemThreatGridCurrentNative, dependency);
                DisposeNativeArray(ref EcosystemThreatGridNextNative, dependency);
                DisposeNativeArray(ref EcosystemThreatGridCompressedCurrentNative, dependency);
                DisposeNativeArray(ref EcosystemThreatGridCompressedNextNative, dependency);
                DisposeNativeArray(ref EcosystemThreatVoxelCurrentNative, dependency);
                DisposeNativeArray(ref EcosystemThreatVoxelNextNative, dependency);
                DisposeNativeArray(ref EcosystemThreatEchoCurrentNative, dependency);
                DisposeNativeArray(ref EcosystemThreatEchoNextNative, dependency);
                DisposeNativeArray(ref EcosystemFlowFieldCurrentNative, dependency);
                DisposeNativeArray(ref EcosystemFlowFieldNextNative, dependency);
                DisposeNativeArray(ref SwarmWakeImpulseNative, dependency);
                DisposeNativeArray(ref AbyssalThermalGridNative, dependency);
                DisposeNativeArray(ref AbyssalThermalGridNextNative, dependency);
                DisposeNativeArray(ref AbyssalFlowVolumeCurrentNative, dependency);
                DisposeNativeArray(ref AbyssalFlowVolumeNextNative, dependency);
                DisposeNativeArray(ref CanopyHeightGridNative, dependency);
                DisposeNativeArray(ref TerrainHoleRecordsNative, dependency);
                DisposeNativeArray(ref TerrainHoleStreamingRecordsNative, dependency);
                DisposeNativeArray(ref ArtificialStructureRecordsNative, dependency);
                DisposeNativeParallelMultiHashMap(ref ArtificialStructureHashFrontNative, dependency);
                DisposeNativeParallelMultiHashMap(ref ArtificialStructureHashBackNative, default);
                DisposeNativeParallelMultiHashMap(ref ThreatSamplingChunkHashFrontNative, dependency);
                DisposeNativeParallelMultiHashMap(ref ThreatSamplingChunkHashBackNative, default);
                DisposeNativeArray(ref AbyssalAnchorPositionsNative, dependency);
                DisposeNativeArray(ref AbyssalNavNodeSnapshotNative, dependency);
                DisposeNativeArray(ref AbyssalNavConduitVectorsSnapshotNative, dependency);
                DisposeNativeArray(ref AbyssalNavConduitStrengthSnapshotNative, dependency);
                DisposeNativeArray(ref AbyssalNavNodeTypesSnapshotNative, dependency);
                DisposeNativeParallelMultiHashMap(ref AbyssalNavGraphHashNative, dependency);
                DisposeNativeList(ref AbyssalNavNodes, dependency);
                DisposeNativeArray(ref AbyssalPathSnapshotNative, dependency);
                DisposeNativeList(ref AbyssalPathRawResultNative, dependency);
                DisposeNativeList(ref AbyssalPathResultNative, dependency);
                DisposeNativeArray(ref AbyssalPathParentsNative, dependency);
                DisposeNativeArray(ref AbyssalPathGScoreNative, dependency);
                DisposeNativeArray(ref AbyssalPathFScoreNative, dependency);
                DisposeNativeArray(ref AbyssalPathClosedFlagsNative, dependency);
                DisposeNativeArray(ref AbyssalPathHeapNodesNative, dependency);
                DisposeNativeArray(ref AbyssalPathHeapPositionsNative, dependency);
                DisposeNativeArray(ref PredatorFearNodesSnapshotNative, dependency);
                DisposeNativeArray(ref HlodRegistrySnapshotNative, dependency);
                DisposeNativeArray(ref VisibleHlodSnapshotNative, dependency);
                DisposeNativeArray(ref HlodVisibleFlagsNative, dependency);
                DisposeNativeArray(ref HlodFrustumPlanesNative, dependency);
                DisposeNativeArray(ref SurfaceDefragMovesNative, dependency);
                DisposeNativeArray(ref UnderwaterDefragMovesNative, dependency);
                DisposeNativeArray(ref SurfaceAggregateCopyRecordsNative, dependency);
                DisposeNativeArray(ref UnderwaterAggregateCopyRecordsNative, dependency);
                DisposeNativeArray(ref MegaWreckStreamSnapshotNative, dependency);
            }
        }
    }
}
