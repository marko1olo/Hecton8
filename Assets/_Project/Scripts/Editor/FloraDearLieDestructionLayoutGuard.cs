using System.Runtime.InteropServices;
using System.Reflection;
using Hecton8.Core;
using Hecton8.World;
using Unity.Collections.LowLevel.Unsafe;
using UnityEditor;

namespace Hecton8.Editor
{
    [InitializeOnLoad]
    internal static class FloraDearLieDestructionLayoutGuard
    {
        static FloraDearLieDestructionLayoutGuard()
        {
            Validate();
        }

        [MenuItem("Hecton8/Diagnostics/Validate Flora Dear Lie Layout")]
        private static void ValidateMenu()
        {
            Validate();
        }

        private static void Validate()
        {
            int dtoSize = UnsafeUtility.SizeOf<DestructibleOrganicManager.FloraDestructionEventDTO>();
            int dtoAlign = UnsafeUtility.AlignOf<DestructibleOrganicManager.FloraDestructionEventDTO>();
            int impactOffset = ResolveFieldOffset(typeof(DestructibleOrganicManager.FloraDestructionEventDTO), nameof(DestructibleOrganicManager.FloraDestructionEventDTO.ImpactAUP));
            int typeOffset = ResolveFieldOffset(typeof(DestructibleOrganicManager.FloraDestructionEventDTO), nameof(DestructibleOrganicManager.FloraDestructionEventDTO.FloraTypeHash));
            int padOffset = ResolveFieldOffset(typeof(DestructibleOrganicManager.FloraDestructionEventDTO), nameof(DestructibleOrganicManager.FloraDestructionEventDTO._pad0));
            bool resultValid = TryValidateNestedLayout("FloraDearLieDestructionResult", 128, "ImpactAUP", 64, "VfxQuantity", 104);
            bool counterValid = TryValidateNestedLayout("FloraDearLieCounter64", 64, "Value", 0, "Value", 0);
            bool claimValid = TryValidateNestedLayout("FloraDearLieClaim64", 64, "Claimed", 0, "Claimed", 0);
            bool regenValid = TryValidateNestedLayout("FloraDearLieRegenRecord", 96, "OriginalMatrix", 0, "Underwater", 88);
            bool telemetryValid = TryValidateNestedLayout("FloraDearLieTelemetryEntry", 64, "Flags", 52, "QueryMicroseconds", 56);
            bool valid = dtoSize == 32 &&
                         dtoAlign == 8 &&
                         impactOffset == 0 &&
                         typeOffset == 24 &&
                         padOffset == 28 &&
                         resultValid &&
                         counterValid &&
                         claimValid &&
                         regenValid &&
                         telemetryValid &&
                         HectonVegetationInstanceData.Stride == 64;
            if (!valid)
            {
                throw new FatalArchitectureException(
                    "SHINOBU_268 flora Dear Lie layout violation: DTO=32 align=8 ImpactAUP@0 FloraTypeHash@24 _pad0@28, result=128 ImpactAUP@64 VfxQuantity@104, counter=64 Value@0, claim=64 Claimed@0, regen=96 OriginalMatrix@0 Underwater@88, telemetry=64 Flags@52 QueryMicroseconds@56, metadata stride 64.");
            }
        }

        private static bool TryValidateNestedLayout(string typeName, int expectedSize, string firstField, int firstOffset, string secondField, int secondOffset)
        {
            System.Type type = typeof(DestructibleOrganicManager).GetNestedType(typeName, BindingFlags.NonPublic);
            if (type == null)
                return false;

            return ResolveUnsafeSize(type) == expectedSize &&
                   ResolveFieldOffset(type, firstField) == firstOffset &&
                   ResolveFieldOffset(type, secondField) == secondOffset;
        }

        private static int ResolveUnsafeSize(System.Type type)
        {
            MethodInfo method = typeof(FloraDearLieDestructionLayoutGuard).GetMethod(nameof(UnsafeSizeOfGeneric), BindingFlags.NonPublic | BindingFlags.Static);
            if (method == null)
                return -1;

            return (int)method.MakeGenericMethod(type).Invoke(null, null);
        }

        private static int ResolveFieldOffset(System.Type type, string fieldName)
        {
            FieldInfo field = type.GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field == null)
                return -1;

            try
            {
                return UnsafeUtility.GetFieldOffset(field);
            }
            catch
            {
                FieldOffsetAttribute offset = field.GetCustomAttribute<FieldOffsetAttribute>();
                return offset != null ? offset.Value : -1;
            }
        }

        private static int UnsafeSizeOfGeneric<T>() where T : struct
        {
            return UnsafeUtility.SizeOf<T>();
        }
    }
}
