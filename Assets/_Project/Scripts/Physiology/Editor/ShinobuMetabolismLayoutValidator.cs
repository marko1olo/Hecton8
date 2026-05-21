#if UNITY_EDITOR
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
                         UnsafeUtility.SizeOf<MetabolicStateDTO>() == 32 &&
                         UnsafeUtility.GetFieldOffset(typeof(MetabolicStateDTO).GetField(nameof(MetabolicStateDTO.Calories))) == 0 &&
                         UnsafeUtility.GetFieldOffset(typeof(MetabolicStateDTO).GetField(nameof(MetabolicStateDTO.Hydration))) == 4 &&
                         UnsafeUtility.GetFieldOffset(typeof(MetabolicStateDTO).GetField(nameof(MetabolicStateDTO.CoreTemperature))) == 8 &&
                         UnsafeUtility.GetFieldOffset(typeof(MetabolicStateDTO).GetField(nameof(MetabolicStateDTO.Toxicity))) == 12 &&
                         UnsafeUtility.GetFieldOffset(typeof(MetabolicStateDTO).GetField(nameof(MetabolicStateDTO.EntityHashID))) == 16 &&
                         UnsafeUtility.GetFieldOffset(typeof(MetabolicStateDTO).GetField(nameof(MetabolicStateDTO.Flags))) == 20 &&
                         UnsafeUtility.GetFieldOffset(typeof(MetabolicStateDTO).GetField(nameof(MetabolicStateDTO._pad0))) == 24 &&
                         UnsafeUtility.GetFieldOffset(typeof(MetabolicStateDTO).GetField(nameof(MetabolicStateDTO._pad1))) == 28 &&
                         UnsafeUtility.SizeOf<GasPhysiologyStateDTO>() == 32 &&
                         UnsafeUtility.GetFieldOffset(typeof(GasPhysiologyStateDTO).GetField(nameof(GasPhysiologyStateDTO.OxygenPartialPressure))) == 0 &&
                         UnsafeUtility.GetFieldOffset(typeof(GasPhysiologyStateDTO).GetField(nameof(GasPhysiologyStateDTO.NitrogenPartialPressure))) == 4 &&
                         UnsafeUtility.GetFieldOffset(typeof(GasPhysiologyStateDTO).GetField(nameof(GasPhysiologyStateDTO.CarbonDioxidePartialPressure))) == 8 &&
                         UnsafeUtility.GetFieldOffset(typeof(GasPhysiologyStateDTO).GetField(nameof(GasPhysiologyStateDTO.CnsToxicity01))) == 12 &&
                         UnsafeUtility.GetFieldOffset(typeof(GasPhysiologyStateDTO).GetField(nameof(GasPhysiologyStateDTO.NarcosisLevel01))) == 16 &&
                         UnsafeUtility.GetFieldOffset(typeof(GasPhysiologyStateDTO).GetField(nameof(GasPhysiologyStateDTO.StaminaDrainRate))) == 20 &&
                         UnsafeUtility.GetFieldOffset(typeof(GasPhysiologyStateDTO).GetField(nameof(GasPhysiologyStateDTO.Flags))) == 24 &&
                         UnsafeUtility.GetFieldOffset(typeof(GasPhysiologyStateDTO).GetField(nameof(GasPhysiologyStateDTO._pad0))) == 28 &&
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
    }
}
#endif
