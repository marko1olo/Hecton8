#if UNITY_EDITOR
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
                         UnsafeUtility.SizeOf<MetabolicStateDTO>() == 32 &&
                         UnsafeUtility.GetFieldOffset(typeof(MetabolicStateDTO).GetField(nameof(MetabolicStateDTO.Calories))) == 0 &&
                         UnsafeUtility.GetFieldOffset(typeof(MetabolicStateDTO).GetField(nameof(MetabolicStateDTO.Hydration))) == 4 &&
                         UnsafeUtility.GetFieldOffset(typeof(MetabolicStateDTO).GetField(nameof(MetabolicStateDTO.CoreTemperature))) == 8 &&
                         UnsafeUtility.GetFieldOffset(typeof(MetabolicStateDTO).GetField(nameof(MetabolicStateDTO.Toxicity))) == 12 &&
                         UnsafeUtility.GetFieldOffset(typeof(MetabolicStateDTO).GetField(nameof(MetabolicStateDTO.EntityHashID))) == 16 &&
                         UnsafeUtility.GetFieldOffset(typeof(MetabolicStateDTO).GetField(nameof(MetabolicStateDTO.Flags))) == 20 &&
                         UnsafeUtility.GetFieldOffset(typeof(MetabolicStateDTO).GetField(nameof(MetabolicStateDTO._pad0))) == 24 &&
                         UnsafeUtility.GetFieldOffset(typeof(MetabolicStateDTO).GetField(nameof(MetabolicStateDTO._pad1))) == 28;

            if (!valid)
                Debug.LogError("[SHINOBU_145] MetabolicStateDTO layout violation. Required explicit size=32, offsets 0/4/8/12/16/20/24/28.");

            return valid;
        }
    }
}
#endif
