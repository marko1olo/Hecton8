using System.Runtime.InteropServices;
using System.Reflection;
using Hecton8.Core;
using Hecton8.Scavenging;
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
            int magnitudeOffset = ResolveFieldOffset(typeof(DestructibleOrganicManager.FloraDestructionEventDTO), nameof(DestructibleOrganicManager.FloraDestructionEventDTO.MagnitudeBits));
            bool resultValid = TryValidateNestedLayout("FloraDearLieDestructionResult", 128, "ImpactAUP", 0, "OriginalMatrix", 24);
            bool counterValid = TryValidateNestedLayout("FloraDearLieCounter64", 64, "Value", 0, "Value", 0);
            bool claimValid = TryValidateNestedLayout("FloraDearLieClaim64", 64, "Claimed", 0, "Claimed", 0);
            bool regenValid = TryValidateNestedLayout("FloraDearLieRegenRecord", 96, "OriginalMatrix", 0, "Underwater", 88);
            bool telemetryValid = TryValidateNestedLayout("FloraDearLieTelemetryEntry", 64, "QueryMicroseconds", 52, "Flags", 56);
            bool organicHalfValid = TryValidateNestedLayout("OrganicHalfMapEntry", 16, "_key", 0, "_state", 6);
            bool organicByteValid = TryValidateNestedLayout("OrganicByteMapEntry", 16, "_key", 0, "_state", 5);
            bool organicFloatValid = TryValidateNestedLayout("OrganicFloatMapEntry", 16, "_key", 0, "_state", 8);
            bool organicFloat2Valid = TryValidateNestedLayout("OrganicFloat2MapEntry", 16, "_key", 0, "_state", 12);
            bool organicFloat3Valid = TryValidateNestedLayout("OrganicFloat3MapEntry", 24, "_key", 0, "_state", 16);
            bool interactionPointValid =
                UnsafeUtility.SizeOf<FloraHarvestInteractionPoint>() == 96 &&
                ResolveFieldOffset(typeof(FloraHarvestInteractionPoint), nameof(FloraHarvestInteractionPoint.AnchorAup)) == 0 &&
                ResolveFieldOffset(typeof(FloraHarvestInteractionPoint), nameof(FloraHarvestInteractionPoint.RuntimePosition)) == 48 &&
                ResolveFieldOffset(typeof(FloraHarvestInteractionPoint), nameof(FloraHarvestInteractionPoint.MaterialClass)) == 84;
            bool harvestDescriptorValid =
                UnsafeUtility.SizeOf<HarvestableTemplate.RuntimeDescriptor>() == 32 &&
                ResolveFieldOffset(typeof(HarvestableTemplate.RuntimeDescriptor), nameof(HarvestableTemplate.RuntimeDescriptor.StableHashId)) == 0 &&
                ResolveFieldOffset(typeof(HarvestableTemplate.RuntimeDescriptor), nameof(HarvestableTemplate.RuntimeDescriptor.LootStartIndex)) == 12 &&
                ResolveFieldOffset(typeof(HarvestableTemplate.RuntimeDescriptor), nameof(HarvestableTemplate.RuntimeDescriptor.LootCount)) == 16;
            bool harvestLootValid =
                UnsafeUtility.SizeOf<HarvestableTemplate.LootRuntimeEntry>() == 32 &&
                ResolveFieldOffset(typeof(HarvestableTemplate.LootRuntimeEntry), nameof(HarvestableTemplate.LootRuntimeEntry.ItemHashId)) == 0 &&
                ResolveFieldOffset(typeof(HarvestableTemplate.LootRuntimeEntry), nameof(HarvestableTemplate.LootRuntimeEntry.MinimumAmount)) == 4 &&
                ResolveFieldOffset(typeof(HarvestableTemplate.LootRuntimeEntry), nameof(HarvestableTemplate.LootRuntimeEntry.Weight)) == 8;
            bool valid = dtoSize == 32 &&
                         dtoAlign == 8 &&
                         impactOffset == 0 &&
                         typeOffset == 24 &&
                         magnitudeOffset == 28 &&
                         resultValid &&
                         counterValid &&
                         claimValid &&
                         regenValid &&
                         telemetryValid &&
                         organicHalfValid &&
                         organicByteValid &&
                         organicFloatValid &&
                         organicFloat2Valid &&
                         organicFloat3Valid &&
                         interactionPointValid &&
                         harvestDescriptorValid &&
                         harvestLootValid &&
                         HectonVegetationInstanceData.Stride == 64;
            if (!valid)
            {
                throw new FatalArchitectureException(
                    "Agent 1318 flora layout violation: DTO=32 align=8 ImpactAUP@0 FloraTypeHash@24 MagnitudeBits@28, result=128 ImpactAUP@0 OriginalMatrix@24, counter=64 Value@0, claim=64 Claimed@0, regen=96 OriginalMatrix@0 Underwater@88, telemetry=64 QueryMicroseconds@52 Flags@56, harvest point=96 AnchorAup@0 RuntimePosition@48 MaterialClass@84, harvest descriptor=32 StableHashId@0 LootStartIndex@12 LootCount@16, loot entry=32 ItemHashId@0 MinimumAmount@4 Weight@8, organic UID map entries 16/16/16/16/24 with _key@0 and state offsets 6/5/8/12/16, metadata stride 64.");
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
