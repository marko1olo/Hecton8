#if UNITY_EDITOR
using System.Reflection;
using Hecton8.Core;
using Hecton8.Core.Memory;
using Unity.Collections.LowLevel.Unsafe;
using UnityEditor;
using UnityEngine;

namespace Hecton8.Crafting.Editor
{
    internal static class FabricatorMemorySovereigntyValidator1329
    {
        [MenuItem("Hecton8/Validation/Fabricator Memory Sovereignty 1329")]
        private static void ValidateFromMenu()
        {
            if (!Validate())
                throw new FatalArchitectureException("1329 fabricator memory sovereignty validator failed.");

            Debug.Log("Fabricator memory sovereignty validator 1329 passed.");
        }

        [InitializeOnLoadMethod]
        private static void ValidateOnEditorLoad()
        {
            if (!Validate())
                throw new FatalArchitectureException("1329 fabricator memory sovereignty validator failed.");
        }

        internal static bool Validate()
        {
            return Fabricator.ValidateFabricatorMemoryTelemetryLayout() &&
                   UnsafeUtility.SizeOf<Fabricator.FabricatorMemoryTelemetryEntry>() == 64 &&
                   UnsafeOffset<Fabricator.FabricatorMemoryTelemetryEntry>(nameof(Fabricator.FabricatorMemoryTelemetryEntry.Sequence)) == 0 &&
                   UnsafeOffset<Fabricator.FabricatorMemoryTelemetryEntry>(nameof(Fabricator.FabricatorMemoryTelemetryEntry.StateHash)) == 8 &&
                   UnsafeOffset<Fabricator.FabricatorMemoryTelemetryEntry>(nameof(Fabricator.FabricatorMemoryTelemetryEntry.Frame)) == 16 &&
                   UnsafeOffset<Fabricator.FabricatorMemoryTelemetryEntry>(nameof(Fabricator.FabricatorMemoryTelemetryEntry.BufferId)) == 20 &&
                   UnsafeOffset<Fabricator.FabricatorMemoryTelemetryEntry>(nameof(Fabricator.FabricatorMemoryTelemetryEntry.HandleGeneration)) == 24 &&
                   UnsafeOffset<Fabricator.FabricatorMemoryTelemetryEntry>(nameof(Fabricator.FabricatorMemoryTelemetryEntry.VaultGeneration)) == 28 &&
                   UnsafeOffset<Fabricator.FabricatorMemoryTelemetryEntry>(nameof(Fabricator.FabricatorMemoryTelemetryEntry.Flags)) == 32 &&
                   UnsafeOffset<Fabricator.FabricatorMemoryTelemetryEntry>(nameof(Fabricator.FabricatorMemoryTelemetryEntry.Capacity)) == 36 &&
                   UnsafeOffset<Fabricator.FabricatorMemoryTelemetryEntry>(nameof(Fabricator.FabricatorMemoryTelemetryEntry.FailureStreak)) == 40 &&
                   UnsafeOffset<Fabricator.FabricatorMemoryTelemetryEntry>(nameof(Fabricator.FabricatorMemoryTelemetryEntry.GlobalQualityWeight)) == 44 &&
                   UnsafeOffset<Fabricator.FabricatorMemoryTelemetryEntry>(nameof(Fabricator.FabricatorMemoryTelemetryEntry.CpuMicroseconds)) == 48 &&
                   UnsafeOffset<Fabricator.FabricatorMemoryTelemetryEntry>(nameof(Fabricator.FabricatorMemoryTelemetryEntry.GpuMicroseconds)) == 52 &&
                   UnsafeOffset<Fabricator.FabricatorMemoryTelemetryEntry>(nameof(Fabricator.FabricatorMemoryTelemetryEntry.SystemId)) == 56 &&
                   UnsafeOffset<Fabricator.FabricatorMemoryTelemetryEntry>("_pad0") == 60 &&
                   UnsafeOffset<Fabricator.FabricatorMemoryTelemetryEntry>("_pad1") == 61 &&
                   UnsafeOffset<Fabricator.FabricatorMemoryTelemetryEntry>("_pad2") == 62 &&
                   UnsafeOffset<Fabricator.FabricatorMemoryTelemetryEntry>("_pad3") == 63 &&
                   (uint)BufferID.ShinobuFabricatorInventoryCountPairs == 71144u &&
                   (uint)BufferID.ShinobuFabricatorRecipeCosts == 71148u &&
                   (uint)BufferID.ShinobuFabricatorRecipeEvaluationResult == 71149u &&
                   (uint)BufferID.ShinobuFabricatorDeconstructionRecipeOutputs == 71169u &&
                   (uint)BufferID.ShinobuFabricatorDeconstructionOutputCount == 71170u &&
                   (uint)BufferID.ShinobuFabricatorComplexRecipeGraphNodes == 71171u &&
                   (uint)BufferID.ShinobuFabricatorComplexRecipeGraphEdges == 71172u &&
                   (uint)BufferID.ShinobuFabricatorComplexRecipeGraphInDegrees == 71173u &&
                   (uint)BufferID.ShinobuFabricatorComplexRecipeGraphQueue == 71174u &&
                   (uint)BufferID.ShinobuFabricatorComplexRecipeRawCosts == 71175u &&
                   (uint)BufferID.ShinobuFabricatorComplexRecipeRawCostCount == 71176u &&
                   (uint)BufferID.ShinobuFabricatorComplexRecipeGraphStatus == 71177u &&
                   (uint)BufferID.ShinobuFabricatorUnlockedRecipes == 71178u &&
                   (uint)BufferID.ShinobuFabricatorMemoryTelemetryRing == 71179u;
        }

        private static int UnsafeOffset<T>(string fieldName) where T : struct
        {
            FieldInfo field = typeof(T).GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (field == null)
                return -1;

            return UnsafeUtility.GetFieldOffset(field);
        }
    }
}
#endif
