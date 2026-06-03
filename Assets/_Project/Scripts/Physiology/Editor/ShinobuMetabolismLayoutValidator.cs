#if UNITY_EDITOR
using System.Reflection;
using Hecton8.Core;
using Hecton8.Core.Contracts.Physiology;
using Hecton8.Physiology;
using Unity.Collections.LowLevel.Unsafe;
using UnityEditor;
using UnityEngine;

namespace Hecton8.Physiology.Editor
{
    [InitializeOnLoad]
    internal static class ShinobuMetabolismLayoutValidator
    {
        private const BindingFlags ThermodynamicFlowFieldFlags =
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        static ShinobuMetabolismLayoutValidator()
        {
            Validate();
        }

        [MenuItem("Hecton8/Physiology/Validate Metabolism DTO Layout")]
        private static void ValidateMenu()
        {
            Validate();
        }

        internal static bool Validate()
        {
            bool valid = ShinobuMetabolismLayoutGuards.ValidateMetabolismLayouts() &&
                         ShinobuPhysiologyLayoutGuards.ValidatePhysiologyLayouts() &&
                         ValidateThermodynamicFlowSampleLayout() &&
                         UnsafeUtility.SizeOf<MetabolicStateDTO>() == ShinobuMetabolismVaultContract.MetabolicStateSizeBytes &&
                         (UnsafeUtility.SizeOf<MetabolicStateDTO>() & 7) == 0 &&
                         UnsafeUtility.GetFieldOffset(typeof(MetabolicStateDTO).GetField(nameof(MetabolicStateDTO.Calories))) == 0 &&
                         UnsafeUtility.GetFieldOffset(typeof(MetabolicStateDTO).GetField(nameof(MetabolicStateDTO.Hydration))) == 4 &&
                         UnsafeUtility.GetFieldOffset(typeof(MetabolicStateDTO).GetField(nameof(MetabolicStateDTO.CoreTemperature))) == 8 &&
                         UnsafeUtility.GetFieldOffset(typeof(MetabolicStateDTO).GetField(nameof(MetabolicStateDTO.Toxicity))) == 12 &&
                         UnsafeUtility.GetFieldOffset(typeof(MetabolicStateDTO).GetField(nameof(MetabolicStateDTO.EntityHashID))) == 16 &&
                         UnsafeUtility.GetFieldOffset(typeof(MetabolicStateDTO).GetField(nameof(MetabolicStateDTO.Flags))) == 20 &&
                         UnsafeUtility.GetFieldOffset(typeof(MetabolicStateDTO).GetField(nameof(MetabolicStateDTO.Fatigue01))) == 24 &&
                         UnsafeUtility.GetFieldOffset(typeof(MetabolicStateDTO).GetField(nameof(MetabolicStateDTO.RealO2))) == 28 &&
                         UnsafeUtility.GetFieldOffset(typeof(MetabolicStateDTO).GetField(nameof(MetabolicStateDTO.AgonyTimeRemaining))) == 32 &&
                         UnsafeUtility.GetFieldOffset(typeof(MetabolicStateDTO).GetField(nameof(MetabolicStateDTO.IsInHypoxia))) == 36 &&
                         UnsafeUtility.SizeOf<GasPhysiologyStateDTO>() == 32 &&
                         UnsafeUtility.GetFieldOffset(typeof(GasPhysiologyStateDTO).GetField(nameof(GasPhysiologyStateDTO.OxygenPartialPressure))) == 0 &&
                         UnsafeUtility.GetFieldOffset(typeof(GasPhysiologyStateDTO).GetField(nameof(GasPhysiologyStateDTO.NitrogenPartialPressure))) == 4 &&
                         UnsafeUtility.GetFieldOffset(typeof(GasPhysiologyStateDTO).GetField(nameof(GasPhysiologyStateDTO.CarbonDioxidePartialPressure))) == 8 &&
                         UnsafeUtility.GetFieldOffset(typeof(GasPhysiologyStateDTO).GetField(nameof(GasPhysiologyStateDTO.CnsToxicity01))) == 12 &&
                         UnsafeUtility.GetFieldOffset(typeof(GasPhysiologyStateDTO).GetField(nameof(GasPhysiologyStateDTO.NarcosisLevel01))) == 16 &&
                         UnsafeUtility.GetFieldOffset(typeof(GasPhysiologyStateDTO).GetField(nameof(GasPhysiologyStateDTO.StaminaDrainRate))) == 20 &&
                         UnsafeUtility.GetFieldOffset(typeof(GasPhysiologyStateDTO).GetField(nameof(GasPhysiologyStateDTO.Flags))) == 24 &&
                         UnsafeUtility.GetFieldOffset(typeof(GasPhysiologyStateDTO).GetField(nameof(GasPhysiologyStateDTO.LastWarningFrame))) == 28 &&
                         UnsafeUtility.SizeOf<GasPhysiologyTuningDTO>() == 64 &&
                         UnsafeUtility.GetFieldOffset(typeof(GasPhysiologyTuningDTO).GetField(nameof(GasPhysiologyTuningDTO.HypoxiaPartialPressureAtm))) == 0 &&
                         UnsafeUtility.GetFieldOffset(typeof(GasPhysiologyTuningDTO).GetField(nameof(GasPhysiologyTuningDTO.AnoxiaPartialPressureAtm))) == 4 &&
                         UnsafeUtility.GetFieldOffset(typeof(GasPhysiologyTuningDTO).GetField(nameof(GasPhysiologyTuningDTO.CnsToxicityStartAtm))) == 8 &&
                         UnsafeUtility.GetFieldOffset(typeof(GasPhysiologyTuningDTO).GetField(nameof(GasPhysiologyTuningDTO.CnsToxicityExtremeAtm))) == 12 &&
                         UnsafeUtility.GetFieldOffset(typeof(GasPhysiologyTuningDTO).GetField(nameof(GasPhysiologyTuningDTO.CnsAccumulationRate))) == 16 &&
                         UnsafeUtility.GetFieldOffset(typeof(GasPhysiologyTuningDTO).GetField(nameof(GasPhysiologyTuningDTO.CnsExtremeRate))) == 20 &&
                         UnsafeUtility.GetFieldOffset(typeof(GasPhysiologyTuningDTO).GetField(nameof(GasPhysiologyTuningDTO.CnsRecoveryPerSecond))) == 24 &&
                         UnsafeUtility.GetFieldOffset(typeof(GasPhysiologyTuningDTO).GetField(nameof(GasPhysiologyTuningDTO.CnsRecoveryPressureScale))) == 28 &&
                         UnsafeUtility.GetFieldOffset(typeof(GasPhysiologyTuningDTO).GetField(nameof(GasPhysiologyTuningDTO.NarcosisStartAtm))) == 32 &&
                         UnsafeUtility.GetFieldOffset(typeof(GasPhysiologyTuningDTO).GetField(nameof(GasPhysiologyTuningDTO.NarcosisFullAtm))) == 36 &&
                         UnsafeUtility.GetFieldOffset(typeof(GasPhysiologyTuningDTO).GetField(nameof(GasPhysiologyTuningDTO.CarbonDioxideToxicityStartAtm))) == 40 &&
                         UnsafeUtility.GetFieldOffset(typeof(GasPhysiologyTuningDTO).GetField(nameof(GasPhysiologyTuningDTO.CarbonDioxideToxicityFullAtm))) == 44 &&
                         UnsafeUtility.GetFieldOffset(typeof(GasPhysiologyTuningDTO).GetField(nameof(GasPhysiologyTuningDTO.ToxicDamageStart01))) == 48 &&
                         UnsafeUtility.GetFieldOffset(typeof(GasPhysiologyTuningDTO).GetField(nameof(GasPhysiologyTuningDTO.ToxicDamagePerSecond))) == 52 &&
                         UnsafeUtility.GetFieldOffset(typeof(GasPhysiologyTuningDTO).GetField(nameof(GasPhysiologyTuningDTO.StaminaStressScale))) == 56 &&
                         UnsafeUtility.GetFieldOffset(typeof(GasPhysiologyTuningDTO).GetField(nameof(GasPhysiologyTuningDTO.Version))) == 60;

            if (!valid)
                Debug.LogError("[SHINOBU_145/272] Metabolism or GasPhysiology DTO layout violation. Required explicit 32-byte gas state and 64-byte gas tuning offsets.");

            return valid;
        }

        private static bool ValidateThermodynamicFlowSampleLayout()
        {
            return UnsafeUtility.SizeOf<ThermodynamicFlowSampleDTO>() == 64 &&
                   GetThermodynamicFlowSampleOffset(nameof(ThermodynamicFlowSampleDTO.FlowVelocityWS)) == 0 &&
                   GetThermodynamicFlowSampleOffset(nameof(ThermodynamicFlowSampleDTO.Heat01)) == 12 &&
                   GetThermodynamicFlowSampleOffset(nameof(ThermodynamicFlowSampleDTO.DragMultiplier)) == 16 &&
                   GetThermodynamicFlowSampleOffset(nameof(ThermodynamicFlowSampleDTO.CableAnchorWS)) == 20 &&
                   GetThermodynamicFlowSampleOffset(nameof(ThermodynamicFlowSampleDTO.CableTension01)) == 32 &&
                   GetThermodynamicFlowSampleOffset(nameof(ThermodynamicFlowSampleDTO.CableCutProgress01)) == 36 &&
                   GetThermodynamicFlowSampleOffset(nameof(ThermodynamicFlowSampleDTO.CableEscapeSuppression01)) == 40 &&
                   GetThermodynamicFlowSampleOffset(nameof(ThermodynamicFlowSampleDTO.HasFlow)) == 44 &&
                   GetThermodynamicFlowSampleOffset(nameof(ThermodynamicFlowSampleDTO.IsCableZone)) == 45 &&
                   GetThermodynamicFlowSampleOffset("_pad0") == 46 &&
                   GetThermodynamicFlowSampleOffset("_pad1") == 48 &&
                   GetThermodynamicFlowSampleOffset("_pad2") == 56;
        }

        private static int GetThermodynamicFlowSampleOffset(string fieldName)
        {
            FieldInfo field = typeof(ThermodynamicFlowSampleDTO).GetField(fieldName, ThermodynamicFlowFieldFlags);
            return field != null ? UnsafeUtility.GetFieldOffset(field) : -1;
        }
    }
}
#endif
