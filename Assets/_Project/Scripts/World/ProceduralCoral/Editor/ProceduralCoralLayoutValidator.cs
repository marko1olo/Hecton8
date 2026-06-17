using System.Reflection;
using Unity.Collections.LowLevel.Unsafe;
using UnityEditor;
using UnityEngine;

namespace Hecton8.World.ProceduralCoral.Editor
{
    [InitializeOnLoad]
    public static class ProceduralCoralLayoutValidator
    {
        private const int BranchStrideBytes = 128;
        private const int LSystemRuleStrideBytes = 64;
        private const int SectorTriggerStrideBytes = 64;
        private const int SectorSaveStrideBytes = 16;
        private const int TuningStrideBytes = 64;
        private const int TurtleStateStrideBytes = 64;
        private const int SpatialCellStrideBytes = 32;
        private const int CapsuleColliderStrideBytes = 64;
        private const int SyncPulseStrideBytes = 32;
        private const int TelemetryStrideBytes = 64;
        private const int DebugSegmentStrideBytes = 64;
        private const int PaddedCounterStrideBytes = 64;
        private const int GpuSwayStrideBytes = 64;
        private const int SelfAuditStrideBytes = 64;
        private const int HzbTileStrideBytes = 16;

        static ProceduralCoralLayoutValidator()
        {
            ValidateLayouts(logSuccess: false);
        }

        [MenuItem("Hecton8/Procedural Coral/Validate Layouts")]
        public static void ValidateLayoutsMenu()
        {
            ValidateLayouts(logSuccess: true);
        }

        public static bool ValidateLayouts(bool logSuccess)
        {
            bool ok = true;
            ok &= ValidateSize<CoralBranchDTO>(BranchStrideBytes);
            ok &= ValidateOffset<CoralBranchDTO>(nameof(CoralBranchDTO.LocalMatrix), 0);
            ok &= ValidateOffset<CoralBranchDTO>(nameof(CoralBranchDTO.PrefabHash), 64);
            ok &= ValidateOffset<CoralBranchDTO>(nameof(CoralBranchDTO.GenerationDepth), 68);
            ok &= ValidateOffset<CoralBranchDTO>(nameof(CoralBranchDTO.SectorAUP), 72);
            ok &= ValidateOffset<CoralBranchDTO>(nameof(CoralBranchDTO.Stiffness), 96);
            ok &= ValidateOffset<CoralBranchDTO>(nameof(CoralBranchDTO.Radius), 100);
            ok &= ValidateOffset<CoralBranchDTO>(nameof(CoralBranchDTO.StateFlags), 104);
            ok &= ValidateOffset<CoralBranchDTO>(nameof(CoralBranchDTO.ParentIndex), 108);
            ok &= ValidateOffset<CoralBranchDTO>(nameof(CoralBranchDTO.StableId), 112);
            ok &= ValidateOffset<CoralBranchDTO>(nameof(CoralBranchDTO.SectorHash), 116);
            ok &= ValidateSize<CoralLSystemRuleDTO>(LSystemRuleStrideBytes);
            ok &= ValidateOffset<CoralLSystemRuleDTO>(nameof(CoralLSystemRuleDTO.SourceOpcode), 0);
            ok &= ValidateOffset<CoralLSystemRuleDTO>(nameof(CoralLSystemRuleDTO.ReplacementCount), 36);
            ok &= ValidateOffset<CoralLSystemRuleDTO>(nameof(CoralLSystemRuleDTO.BranchAngleRadians), 40);
            ok &= ValidateOffset<CoralLSystemRuleDTO>(nameof(CoralLSystemRuleDTO.LengthScale), 44);
            ok &= ValidateOffset<CoralLSystemRuleDTO>(nameof(CoralLSystemRuleDTO.RadiusScale), 48);
            ok &= ValidateOffset<CoralLSystemRuleDTO>(nameof(CoralLSystemRuleDTO.PrefabHash), 52);
            ok &= ValidateOffset<CoralLSystemRuleDTO>(nameof(CoralLSystemRuleDTO.Flags), 56);
            ok &= ValidateOffset<CoralLSystemRuleDTO>(nameof(CoralLSystemRuleDTO.WeightHash), 60);
            ok &= ValidateSize<CoralSectorTriggerDTO>(SectorTriggerStrideBytes);
            ok &= ValidateSize<CoralSectorSaveDTO>(SectorSaveStrideBytes);
            ok &= ValidateSize<CoralTuningDTO>(TuningStrideBytes);
            ok &= ValidateSize<CoralTurtleStateDTO>(TurtleStateStrideBytes);
            ok &= ValidateSize<CoralSpatialCellDTO>(SpatialCellStrideBytes);
            ok &= ValidateSize<CapsuleColliderDTO>(CapsuleColliderStrideBytes);
            ok &= ValidateSize<SyncPulseDTO>(SyncPulseStrideBytes);
            ok &= ValidateSize<CoralGenerationTelemetryEntry>(TelemetryStrideBytes);
            ok &= ValidateOffset<CoralGenerationTelemetryEntry>(nameof(CoralGenerationTelemetryEntry.RootAUP), 0);
            ok &= ValidateOffset<CoralGenerationTelemetryEntry>(nameof(CoralGenerationTelemetryEntry.BurstComputeUs), 40);
            ok &= ValidateOffset<CoralGenerationTelemetryEntry>(nameof(CoralGenerationTelemetryEntry.FaultFlags), 52);
            ok &= ValidateOffset<CoralGenerationTelemetryEntry>(nameof(CoralGenerationTelemetryEntry.MatrixCount), 60);
            ok &= ValidateSize<CoralDebugSegmentDTO>(DebugSegmentStrideBytes);
            ok &= ValidateSize<CoralPaddedCounterDTO>(PaddedCounterStrideBytes);
            ok &= ValidateOffset<CoralPaddedCounterDTO>(nameof(CoralPaddedCounterDTO.BranchCount), 0);
            ok &= ValidateOffset<CoralPaddedCounterDTO>(nameof(CoralPaddedCounterDTO.FaultFlags), 24);
            ok &= ValidateOffset<CoralPaddedCounterDTO>(nameof(CoralPaddedCounterDTO.CollisionProxyCount), 12);
            ok &= ValidateOffset<CoralPaddedCounterDTO>(nameof(CoralPaddedCounterDTO.RenderMatrixCount), 16);
            ok &= ValidateOffset<CoralPaddedCounterDTO>(nameof(CoralPaddedCounterDTO.SyncPulseCount), 20);
            ok &= ValidateOffset<CoralPaddedCounterDTO>(nameof(CoralPaddedCounterDTO.SpatialCellCount), 56);
            ok &= ValidateOffset<CoralPaddedCounterDTO>(nameof(CoralPaddedCounterDTO.EffectiveQualityWeight), 60);
            ok &= ValidateSize<CoralGpuSwayDTO>(GpuSwayStrideBytes);
            ok &= ValidateOffset<CoralGpuSwayDTO>(nameof(CoralGpuSwayDTO.FlowAndAmplitude), 0);
            ok &= ValidateOffset<CoralGpuSwayDTO>(nameof(CoralGpuSwayDTO.BoundsAndDensity), 16);
            ok &= ValidateOffset<CoralGpuSwayDTO>(nameof(CoralGpuSwayDTO.FaultAndFrame), 32);
            ok &= ValidateOffset<CoralGpuSwayDTO>(nameof(CoralGpuSwayDTO.SectorHash), 48);
            ok &= ValidateOffset<CoralGpuSwayDTO>(nameof(CoralGpuSwayDTO.StateHash), 52);
            ok &= ValidateSize<CoralSelfAuditResultDTO>(SelfAuditStrideBytes);
            ok &= ValidateOffset<CoralSelfAuditResultDTO>(nameof(CoralSelfAuditResultDTO.Frame), 0);
            ok &= ValidateOffset<CoralSelfAuditResultDTO>(nameof(CoralSelfAuditResultDTO.SectorHash), 4);
            ok &= ValidateOffset<CoralSelfAuditResultDTO>(nameof(CoralSelfAuditResultDTO.Flags), 8);
            ok &= ValidateOffset<CoralSelfAuditResultDTO>(nameof(CoralSelfAuditResultDTO.MaxOverlapDepth), 32);
            ok &= ValidateOffset<CoralSelfAuditResultDTO>(nameof(CoralSelfAuditResultDTO.BranchUtilization), 36);
            ok &= ValidateSize<CoralHzbTileDTO>(HzbTileStrideBytes);

            if (ok && logSuccess)
                Debug.Log("[SHINOBU_139] Procedural coral DTO layout validated.");

            return ok;
        }

        private static bool ValidateSize<T>(int expected) where T : struct
        {
            int observed = UnsafeUtility.SizeOf<T>();
            if (observed == expected)
                return true;

            Debug.LogError("[SHINOBU_139] Layout size mismatch: " + typeof(T).Name + " expected " + expected + " observed " + observed);
            return false;
        }

        private static bool ValidateOffset<T>(string fieldName, int expected) where T : struct
        {
            FieldInfo field = typeof(T).GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            int observed = field == null ? -1 : UnsafeUtility.GetFieldOffset(field);
            if (observed == expected)
                return true;

            Debug.LogError("[SHINOBU_139] Layout offset mismatch: " + typeof(T).Name + "." + fieldName + " expected " + expected + " observed " + observed);
            return false;
        }
    }
}
