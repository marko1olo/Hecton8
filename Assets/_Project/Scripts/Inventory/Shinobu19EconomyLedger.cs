using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using Hecton8.Core;
using Hecton8.Core.Contracts;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Memory;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace Hecton8.Inventory
{
    public enum ShinobuTransactionStatus : byte
    {
        Success = 0,
        InvalidInput = 1,
        MissingIngredient = 2,
        MaskMissing = 3,
        OutputFull = 4,
        AtomicConflict = 5,
        InsufficientQuantity = 6,
        RleBufferTooSmall = 7,
        BinaryContractMismatch = 8,
        DestinationTooSmall = 9
    }

    public enum ShinobuHardwareTier : byte
    {
        Low = 0,
        Middle = 1,
        High = 2,
        Ultra = 3
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct CraftingRecipeDTO
    {
        [FieldOffset(0)]
        public uint ResultHash;
        [FieldOffset(4)]
        public uint ComponentA;
        [FieldOffset(8)]
        public int QuantityA;
        [FieldOffset(12)]
        public uint ComponentB;
        [FieldOffset(16)]
        public int QuantityB;
        [FieldOffset(20)]
        public uint Reserved0;
        [FieldOffset(24)]
        public uint Reserved1;
        [FieldOffset(28)]
        public uint Reserved2;
    }

    [StructLayout(LayoutKind.Explicit, Size = 16)]
    public struct CraftingRecipeMaskDTO
    {
        [FieldOffset(0)]
        public ulong RequirementMask;
        [FieldOffset(8)]
        public uint ResultHash;
        [FieldOffset(12)]
        public uint RecipeIndex;
    }

    [StructLayout(LayoutKind.Explicit, Size = 16)]
    public struct CraftingIngredientDTO
    {
        [FieldOffset(0)]
        public uint ItemHash;
        [FieldOffset(4)]
        public ushort Quantity;
        [FieldOffset(6)]
        public ushort Reserved0;
        [FieldOffset(8)]
        public uint UnitMassGrams;
        [FieldOffset(12)]
        public uint TotalMassGrams;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct ItemPhysicalConstantsDTO
    {
        [FieldOffset(0)]
        public uint ItemHash;
        [FieldOffset(4)]
        public float MassKg;
        [FieldOffset(8)]
        public float VolumeLiters;
        [FieldOffset(12)]
        public int MaxStack;
        [FieldOffset(16)]
        public float BaseDurability01;
        [FieldOffset(20)]
        public uint Flags;
        [FieldOffset(24)]
        public uint Reserved0;
        [FieldOffset(28)]
        public uint Reserved1;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct EconomyTelemetryEntry
    {
        [FieldOffset(0)]
        public long TimestampTicks;
        [FieldOffset(8)]
        public ulong InventoryMask;
        [FieldOffset(16)]
        public float InventoryTransactionTimeMs;
        [FieldOffset(20)]
        public float MassKg;
        [FieldOffset(24)]
        public float VolumeLiters;
        [FieldOffset(28)]
        public float ReservedFloat;
        [FieldOffset(32)]
        public uint FrameIndex;
        [FieldOffset(36)]
        public uint LastItemHash;
        [FieldOffset(40)]
        public uint LastRecipeHash;
        [FieldOffset(44)]
        public uint Flags;
        [FieldOffset(48)]
        public int TotalItemsCrafted;
        [FieldOffset(52)]
        public int TotalItemsTransferred;
        [FieldOffset(56)]
        public int TransactionResult;
        [FieldOffset(60)]
        public int SlotIndex;
    }

    [StructLayout(LayoutKind.Explicit, Size = 40)]
    public struct ShinobuCarryTotalsDTO
    {
        [FieldOffset(0)]
        public long TimestampTicks;
        [FieldOffset(8)]
        public float TotalMassKg;
        [FieldOffset(12)]
        public float TotalVolumeLiters;
        [FieldOffset(16)]
        public float MaxCarryMassKg;
        [FieldOffset(20)]
        public float MaxCarryVolumeLiters;
        [FieldOffset(24)]
        public float Load01;
        [FieldOffset(28)]
        public float MovementMultiplier;
        [FieldOffset(32)]
        public uint FrameIndex;
        [FieldOffset(36)]
        public uint Reserved0;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public partial struct MockItemAcquiredSignal : ISignal
    {
        public const int ExpectedCapacity = 64;
        public const int MaxFrameSignals = 64;
        public const int LowTierFrameSignals = 16;
        public const uint LaneHash = 0x53493141u; // SI1A

        [FieldOffset(0)] public ulong Sequence;
        [FieldOffset(8)] public uint ItemHash;
        [FieldOffset(12)] public uint FrameIndex;
        [FieldOffset(16)] public int Quantity;
        [FieldOffset(20)] public int SourceEntityIndex;
        [FieldOffset(24)] public uint Flags;
        [FieldOffset(28)] public uint Reserved0;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public partial struct MockCraftingRequestSignal : ISignal
    {
        public const int ExpectedCapacity = 64;
        public const int MaxFrameSignals = 64;
        public const int LowTierFrameSignals = 16;
        public const uint LaneHash = 0x53493143u; // SI1C

        [FieldOffset(0)] public ulong Sequence;
        [FieldOffset(8)] public uint RecipeHash;
        [FieldOffset(12)] public uint ActorHash;
        [FieldOffset(16)] public uint FrameIndex;
        [FieldOffset(20)] public int RequestedQuantity;
        [FieldOffset(24)] public uint Flags;
        [FieldOffset(28)] public uint Reserved0;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public partial struct MockConsumeSignal : ISignal
    {
        public const int ExpectedCapacity = 32;
        public const int MaxFrameSignals = 32;
        public const int LowTierFrameSignals = 8;
        public const uint LaneHash = 0x5349314Eu; // SI1N

        [FieldOffset(0)] public ulong Sequence;
        [FieldOffset(8)] public uint ItemHash;
        [FieldOffset(12)] public uint ActorHash;
        [FieldOffset(16)] public uint FrameIndex;
        [FieldOffset(20)] public int Quantity;
        [FieldOffset(24)] public uint Flags;
        [FieldOffset(28)] public uint Reserved0;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public partial struct MockToolUsedSignal : ISignal
    {
        public const int ExpectedCapacity = 32;
        public const int MaxFrameSignals = 32;
        public const int LowTierFrameSignals = 8;
        public const uint LaneHash = 0x53493154u; // SI1T

        [FieldOffset(0)] public ulong Sequence;
        [FieldOffset(8)] public uint ToolHash;
        [FieldOffset(12)] public uint FrameIndex;
        [FieldOffset(16)] public float Wear01;
        [FieldOffset(20)] public int SlotIndex;
        [FieldOffset(24)] public uint Flags;
        [FieldOffset(28)] public uint Reserved0;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public partial struct ToolBrokenSignal : ISignal
    {
        public const int ExpectedCapacity = 32;
        public const int MaxFrameSignals = 32;
        public const int LowTierFrameSignals = 8;
        public const uint LaneHash = 0x53493142u; // SI1B

        [FieldOffset(0)] public ulong Sequence;
        [FieldOffset(8)] public uint ToolHash;
        [FieldOffset(12)] public uint FrameIndex;
        [FieldOffset(16)] public int SlotIndex;
        [FieldOffset(20)] public uint Flags;
        [FieldOffset(24)] public uint Reserved0;
        [FieldOffset(28)] public uint Reserved1;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public partial struct EncumbranceSignal : ISignal
    {
        public const int ExpectedCapacity = 16;
        public const int MaxFrameSignals = 16;
        public const int LowTierFrameSignals = 4;
        public const uint LaneHash = 0x53493145u; // SI1E

        [FieldOffset(0)] public float Load01;
        [FieldOffset(4)] public float MassKg;
        [FieldOffset(8)] public float VolumeLiters;
        [FieldOffset(12)] public float MovementMultiplier;
        [FieldOffset(16)] public uint FrameIndex;
        [FieldOffset(20)] public uint Flags;
        [FieldOffset(24)] public uint Reserved0;
        [FieldOffset(28)] public uint Reserved1;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public partial struct EquipItemSignal : ISignal
    {
        public const int ExpectedCapacity = 16;
        public const int MaxFrameSignals = 16;
        public const int LowTierFrameSignals = 4;
        public const uint LaneHash = 0x53493151u; // SI1Q

        [FieldOffset(0)] public ulong Sequence;
        [FieldOffset(8)] public uint ItemHash;
        [FieldOffset(12)] public uint FrameIndex;
        [FieldOffset(16)] public int InventorySlot;
        [FieldOffset(20)] public int HotbarSlot;
        [FieldOffset(24)] public uint Flags;
        [FieldOffset(28)] public uint Reserved0;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public partial struct MockHotbarSelectSignal : ISignal
    {
        public const int ExpectedCapacity = 16;
        public const int MaxFrameSignals = 16;
        public const int LowTierFrameSignals = 4;
        public const uint LaneHash = 0x53493148u; // SI1H

        [FieldOffset(0)] public ulong Sequence;
        [FieldOffset(8)] public uint ActorHash;
        [FieldOffset(12)] public uint FrameIndex;
        [FieldOffset(16)] public int HotbarSlot;
        [FieldOffset(20)] public uint Flags;
        [FieldOffset(24)] public uint Reserved0;
        [FieldOffset(28)] public uint Reserved1;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public partial struct DebrisDestroyedSignal : ISignal
    {
        public const int ExpectedCapacity = 64;
        public const int MaxFrameSignals = 64;
        public const int LowTierFrameSignals = 16;
        public const uint LaneHash = 0x53493144u; // SI1D

        [FieldOffset(0)] public ulong Sequence;
        [FieldOffset(8)] public uint LootHash;
        [FieldOffset(12)] public uint FrameIndex;
        [FieldOffset(16)] public int Quantity;
        [FieldOffset(20)] public int DebrisIndex;
        [FieldOffset(24)] public uint Flags;
        [FieldOffset(28)] public uint Reserved0;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct DebrisSpatialEntry
    {
        [FieldOffset(0)]
        public float3 LocalPosition;
        [FieldOffset(12)]
        public uint LootHash;
        [FieldOffset(16)]
        public int Quantity;
        [FieldOffset(20)]
        public int DebrisIndex;
        [FieldOffset(24)]
        public uint Flags;
        [FieldOffset(28)]
        private uint _pad0;
    }

    [StructLayout(LayoutKind.Explicit, Size = 16)]
    public struct EconomyCsvMonitorState
    {
        [FieldOffset(0)]
        public long LastWriteTicks;
        [FieldOffset(8)]
        public int AppliedLineCount;
        [FieldOffset(12)]
        public int RejectedLineCount;
    }

    public static unsafe class Shinobu19EconomyLedger
    {
        private static int s_x001Shinobu19EconomyLedgerSignalPushDropCount;
        public const int BlackBoxCapacity = 300;
        public const int CraftingRecipeDtoSizeBytes = 32;
        public const int CraftingIngredientDtoSizeBytes = 16;
        public const int EconomyTelemetryEntrySizeBytes = 64;
        public const uint RleMagic = 0x31494853u;
        public const uint EconomyDumpMagic = 0x504D3848u;
        public const uint EconomyDumpVersion = 2u;
        public const uint TelemetryFlagSpike = 1u << 0;
        public const uint TelemetryFlagFatal = 1u << 1;
        public const string DefaultDumpPath = "Docs/AgentLogs/Dump_ECONOMY.bin";
        public const string DefaultH8DumpPath = "Docs/AgentLogs/Dump_ECONOMY.h8dump";
        private const int EconomyDumpHeaderBytes = 16;
        private const int EconomyOrderedDumpHeaderBytes = 32;

        public const int OshinoCraftHeaderBytes = 80;
        public const int OshinoCraftRecipeStride = 64;
        public const int OshinoCraftIngredientStride = 16;
        public const int OshinoCraftToolStride = 16;
        public const int OshinoCraftGodModeVisualStride = 16;
        public const int OshinoCraftAlignment = 16;
        public const uint OshinoCraftMagic = 0x52433848u;
        public const uint OshinoCraftVersion = 2u;
        public const uint OshinoEndianProbe = 0x01020304u;

        private const float DefaultDurability01 = 1f;
        private const int RleHeaderBytes = 12;
        private const int RleRecordBytes = 16;
        private const uint Crc32Polynomial = 0xEDB88320u;
        private const int SlotCasRetryLimit = 16;
        private const int EmptySlotClaimSentinel = int.MinValue;
        private const int MinRecipeBatchLimit = 16;
        private const int MaxRecipeBatchLimit = 256;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static CraftingRecipeDTO BuildRecipe(uint resultHash, uint componentA, int quantityA, uint componentB, int quantityB)
        {
            return new CraftingRecipeDTO
            {
                ResultHash = resultHash,
                ComponentA = componentA,
                QuantityA = math.max(0, quantityA),
                ComponentB = componentB,
                QuantityB = math.max(0, quantityB)
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static CraftingRecipeMaskDTO BuildRecipeMask(in CraftingRecipeDTO recipe, uint recipeIndex)
        {
            return new CraftingRecipeMaskDTO
            {
                RequirementMask = ComputeRequirementMask(in recipe),
                ResultHash = recipe.ResultHash,
                RecipeIndex = recipeIndex
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static CraftingRecipeMaskDTO BuildRecipeMask(
            in CraftingRecipeDTO recipe,
            uint recipeIndex,
            NativeArray<CraftingIngredientDTO> ingredients)
        {
            int ingredientCursor = unchecked((int)recipe.Reserved1);
            int ingredientCount = unchecked((int)recipe.Reserved2);
            ulong mask = ingredients.IsCreated && ingredientCount > 0
                ? ComputeRequirementMask(ingredients, ingredientCursor, ingredientCount)
                : ComputeRequirementMask(in recipe);
            return new CraftingRecipeMaskDTO
            {
                RequirementMask = mask,
                ResultHash = recipe.ResultHash,
                RecipeIndex = recipeIndex
            };
        }

        public static bool RuntimeLayoutValid()
        {
            return UnsafeUtility.SizeOf<CraftingRecipeDTO>() == CraftingRecipeDtoSizeBytes &&
                   UnsafeUtility.SizeOf<EconomyTelemetryEntry>() == EconomyTelemetryEntrySizeBytes &&
                   UnsafeUtility.SizeOf<ShinobuCarryTotalsDTO>() == 40 &&
                   UnsafeUtility.SizeOf<CraftingRecipeMaskDTO>() == 16 &&
                   UnsafeUtility.SizeOf<CraftingIngredientDTO>() == CraftingIngredientDtoSizeBytes &&
                   UnsafeUtility.SizeOf<ItemPhysicalConstantsDTO>() == 32 &&
                   UnsafeUtility.SizeOf<DebrisSpatialEntry>() == 32;
        }

        private static bool TryResolveVaultLedger(
            IDataVault vault,
            int capacity,
            out NativeArray<uint> hashes,
            out NativeArray<int> quantities,
            out NativeArray<float> durabilities,
            out int resolvedCapacity)
        {
            hashes = default;
            quantities = default;
            durabilities = default;
            resolvedCapacity = 0;
            if (vault == null || capacity <= 0)
                return false;

            if (!OpenOrAcquireEconomyVaultBuffer(
                BufferID.ShinobuInventoryHashes,
                capacity,
                NativeArrayOptions.UninitializedMemory,
                vault,
                out hashes) ||
                !OpenOrAcquireEconomyVaultBuffer(
                BufferID.ShinobuInventoryQuantities,
                capacity,
                NativeArrayOptions.UninitializedMemory,
                vault,
                out quantities) ||
                !OpenOrAcquireEconomyVaultBuffer(
                BufferID.ShinobuInventoryDurabilities,
                capacity,
                NativeArrayOptions.UninitializedMemory,
                vault,
                out durabilities))
            {
                hashes = default;
                quantities = default;
                durabilities = default;
                return false;
            }

            resolvedCapacity = hashes.IsCreated && quantities.IsCreated && durabilities.IsCreated
                ? math.min(hashes.Length, math.min(quantities.Length, durabilities.Length))
                : 0;
            return resolvedCapacity >= capacity;
        }

        private static bool TryResolveRecipeBuffers(
            IDataVault vault,
            int recipeCapacity,
            out NativeArray<CraftingRecipeDTO> recipes,
            out NativeArray<CraftingRecipeMaskDTO> masks)
        {
            recipes = default;
            masks = default;
            if (vault == null || recipeCapacity <= 0)
                return false;

            if (!OpenOrAcquireEconomyVaultBuffer(
                BufferID.ShinobuRecipeDtos,
                recipeCapacity,
                NativeArrayOptions.UninitializedMemory,
                vault,
                out recipes) ||
                !OpenOrAcquireEconomyVaultBuffer(
                BufferID.ShinobuRecipeMasks,
                recipeCapacity,
                NativeArrayOptions.UninitializedMemory,
                vault,
                out masks))
            {
                recipes = default;
                masks = default;
                return false;
            }

            return recipes.IsCreated && masks.IsCreated && recipes.Length >= recipeCapacity && masks.Length >= recipeCapacity;
        }

        private static bool TryResolveRecipeIngredientBuffer(
            IDataVault vault,
            int ingredientCapacity,
            out NativeArray<CraftingIngredientDTO> ingredients)
        {
            ingredients = default;
            if (vault == null || ingredientCapacity <= 0)
                return false;

            return OpenOrAcquireEconomyVaultBuffer(
                BufferID.ShinobuRecipeIngredients,
                ingredientCapacity,
                NativeArrayOptions.UninitializedMemory,
                vault,
                out ingredients);
        }

        private static bool TryResolvePhysicalConstants(
            IDataVault vault,
            int itemCapacity,
            out NativeArray<ItemPhysicalConstantsDTO> constants)
        {
            constants = default;
            if (vault == null || itemCapacity <= 0)
                return false;

            return OpenOrAcquireEconomyVaultBuffer(
                BufferID.ShinobuPhysicalConstants,
                itemCapacity,
                NativeArrayOptions.UninitializedMemory,
                vault,
                out constants);
        }

        public static bool TryResolveCarryTotals(IDataVault vault, out NativeArray<ShinobuCarryTotalsDTO>.ReadOnly totals)
        {
            totals = default;
            if (vault == null)
                return false;

            if (!OpenOrAcquireEconomyVaultBuffer(
                    BufferID.ShinobuInventoryCarryTotals,
                    1,
                    NativeArrayOptions.ClearMemory,
                    vault,
                    out NativeArray<ShinobuCarryTotalsDTO> mutableTotals))
            {
                return false;
            }

            totals = mutableTotals.AsReadOnly();
            return true;
        }

        public static bool TryResolveHotbarRoutes(IDataVault vault, int hotbarCapacity, out NativeArray<int>.ReadOnly hotbarIndices)
        {
            hotbarIndices = default;
            if (vault == null || hotbarCapacity <= 0)
                return false;

            if (!OpenOrAcquireEconomyVaultBuffer(
                    BufferID.ShinobuHotbarRoutes,
                    hotbarCapacity,
                    NativeArrayOptions.UninitializedMemory,
                    vault,
                    out NativeArray<int> mutableHotbarIndices))
            {
                return false;
            }

            hotbarIndices = mutableHotbarIndices.AsReadOnly();
            return true;
        }

        public static bool TryResolveTelemetry(IDataVault vault, out NativeArray<EconomyTelemetryEntry>.ReadOnly telemetry)
        {
            telemetry = default;
            if (vault == null)
                return false;

            if (!OpenOrAcquireEconomyVaultBuffer(
                BufferID.ShinobuEconomyTelemetryRing,
                BlackBoxCapacity,
                NativeArrayOptions.ClearMemory,
                vault,
                out NativeArray<EconomyTelemetryEntry> mutableTelemetry))
            {
                return false;
            }

            telemetry = mutableTelemetry.AsReadOnly();
            return true;
        }

        private static bool OpenOrAcquireEconomyVaultBuffer<T>(
            BufferID bufferId,
            int requiredLength,
            NativeArrayOptions options,
            IDataVault vault,
            out NativeArray<T> buffer) where T : struct
        {
            buffer = default;
            if (vault == null || requiredLength <= 0)
                return false;

            VaultGenerationHandle<T> handle;
            if (vault.IsAllocationLocked)
            {
                if (!vault.TryGetGenerationHandle(bufferId, out handle))
                    return false;

                return TryOpenEconomyVaultBuffer(vault, in handle, bufferId, requiredLength, out buffer);
            }

            handle = vault.EnsureGenerationHandle<T>(
                bufferId,
                requiredLength,
                SystemID.GameplayPlayer,
                options);
            return TryOpenEconomyVaultBuffer(vault, in handle, bufferId, requiredLength, out buffer);
        }

        private static bool TryOpenEconomyVaultBuffer<T>(
            IDataVault vault,
            in VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength,
            out NativeArray<T> buffer) where T : struct
        {
            buffer = default;
            if (vault == null ||
                requiredLength <= 0 ||
                !IsEconomyVaultHandle(in handle, bufferId) ||
                !vault.TryResolveHandle(in handle, out buffer) ||
                !buffer.IsCreated ||
                buffer.Length < requiredLength)
            {
                buffer = default;
                return false;
            }

            return true;
        }

        private static bool IsEconomyVaultHandle<T>(
            in VaultGenerationHandle<T> handle,
            BufferID bufferId) where T : struct
        {
            return handle.BufferID == (uint)bufferId &&
                   handle.SystemID == (uint)SystemID.GameplayPlayer &&
                   handle.Generation != 0u;
        }

        public static int GenerateEmergencyMockRecipes(
            NativeArray<CraftingRecipeDTO> recipes,
            NativeArray<CraftingRecipeMaskDTO> masks,
            int requestedCount)
        {
            if (!recipes.IsCreated || !masks.IsCreated)
                return 0;

            int count = math.min(math.max(0, requestedCount), math.min(16, math.min(recipes.Length, masks.Length)));
            for (int index = 0; index < count; index++)
            {
                uint componentA = 0x1000u + (uint)(index * 2);
                uint componentB = 0x1001u + (uint)(index * 2);
                CraftingRecipeDTO recipe = BuildRecipe(0x2000u + (uint)index, componentA, 1 + (index & 3), componentB, 1);
                recipes[index] = recipe;
                masks[index] = BuildRecipeMask(in recipe, (uint)index);
            }

            return count;
        }

        public static ShinobuTransactionStatus HydrateCraftingRecipesFromH8Cr(
            NativeArray<byte> binary,
            NativeArray<CraftingRecipeDTO> recipes,
            NativeArray<CraftingRecipeMaskDTO> masks,
            NativeArray<CraftingIngredientDTO> ingredients,
            out int hydratedRecipeCount,
            out int hydratedIngredientCount)
        {
            hydratedRecipeCount = 0;
            hydratedIngredientCount = 0;
            if (!binary.IsCreated || !recipes.IsCreated || !masks.IsCreated || !ingredients.IsCreated)
                return ShinobuTransactionStatus.InvalidInput;

            if (binary.Length < OshinoCraftHeaderBytes)
                return ShinobuTransactionStatus.BinaryContractMismatch;

            byte* src = (byte*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(binary);
            uint magic = ReadUInt32LittleEndian(src, 0);
            uint version = ReadUInt32LittleEndian(src, 4);
            uint endianProbe = ReadUInt32LittleEndian(src, 8);
            int headerBytes = ReadInt32LittleEndian(src, 12);
            int recipeCount = ReadInt32LittleEndian(src, 16);
            int recipeStride = ReadInt32LittleEndian(src, 20);
            int ingredientCount = ReadInt32LittleEndian(src, 24);
            int ingredientStride = ReadInt32LittleEndian(src, 28);
            int toolCount = ReadInt32LittleEndian(src, 32);
            int toolStride = ReadInt32LittleEndian(src, 36);
            int godModeVisualCount = ReadInt32LittleEndian(src, 40);
            int godModeVisualStride = ReadInt32LittleEndian(src, 44);
            int recipeOffset = ReadInt32LittleEndian(src, 48);
            int ingredientOffset = ReadInt32LittleEndian(src, 52);
            int toolOffset = ReadInt32LittleEndian(src, 56);
            int godModeVisualOffset = ReadInt32LittleEndian(src, 60);
            int fileSize = ReadInt32LittleEndian(src, 64);
            uint payloadCrc32 = ReadUInt32LittleEndian(src, 68);
            uint reserved0 = ReadUInt32LittleEndian(src, 72);
            uint reserved1 = ReadUInt32LittleEndian(src, 76);

            if (magic != OshinoCraftMagic ||
                version != OshinoCraftVersion ||
                endianProbe != OshinoEndianProbe ||
                headerBytes != OshinoCraftHeaderBytes ||
                recipeStride != OshinoCraftRecipeStride ||
                ingredientStride != OshinoCraftIngredientStride ||
                toolStride != OshinoCraftToolStride ||
                godModeVisualStride != OshinoCraftGodModeVisualStride ||
                reserved0 != 0u ||
                reserved1 != 0u ||
                recipeCount < 0 ||
                ingredientCount < 0 ||
                toolCount < 0 ||
                godModeVisualCount < 0 ||
                fileSize < OshinoCraftHeaderBytes ||
                fileSize > binary.Length ||
                !IsAligned(fileSize, OshinoCraftAlignment) ||
                !IsAligned(recipeOffset, OshinoCraftAlignment) ||
                !IsAligned(ingredientOffset, OshinoCraftAlignment) ||
                !IsAligned(toolOffset, OshinoCraftAlignment) ||
                !IsAligned(godModeVisualOffset, OshinoCraftAlignment) ||
                !RangeInside(fileSize, recipeOffset, recipeCount, recipeStride) ||
                !RangeInside(fileSize, ingredientOffset, ingredientCount, ingredientStride) ||
                !RangeInside(fileSize, toolOffset, toolCount, toolStride) ||
                !RangeInside(fileSize, godModeVisualOffset, godModeVisualCount, godModeVisualStride))
            {
                return ShinobuTransactionStatus.BinaryContractMismatch;
            }

            if (ComputeCrc32(src + recipeOffset, fileSize - recipeOffset) != payloadCrc32)
                return ShinobuTransactionStatus.BinaryContractMismatch;

            if (recipes.Length < recipeCount || masks.Length < recipeCount || ingredients.Length < ingredientCount)
                return ShinobuTransactionStatus.DestinationTooSmall;

            for (int ingredientIndex = 0; ingredientIndex < ingredientCount; ingredientIndex++)
            {
                int offset = ingredientOffset + ingredientIndex * ingredientStride;
                ingredients[ingredientIndex] = new CraftingIngredientDTO
                {
                    ItemHash = ReadUInt32LittleEndian(src, offset),
                    Quantity = ReadUInt16LittleEndian(src, offset + 4),
                    Reserved0 = ReadUInt16LittleEndian(src, offset + 6),
                    UnitMassGrams = ReadUInt32LittleEndian(src, offset + 8),
                    TotalMassGrams = ReadUInt32LittleEndian(src, offset + 12)
                };
            }

            for (int recipeIndex = 0; recipeIndex < recipeCount; recipeIndex++)
            {
                int offset = recipeOffset + recipeIndex * recipeStride;
                uint recipeHash = ReadUInt32LittleEndian(src, offset);
                uint resultHash = ReadUInt32LittleEndian(src, offset + 4);
                int ingredientInRecipeCount = ReadUInt16LittleEndian(src, offset + 18);
                int ingredientCursor = ReadInt32LittleEndian(src, offset + 24);
                if (ingredientCursor < 0 ||
                    ingredientInRecipeCount < 0 ||
                    ingredientCursor > ingredientCount - ingredientInRecipeCount)
                {
                    return ShinobuTransactionStatus.BinaryContractMismatch;
                }

                uint componentA = 0u;
                uint componentB = 0u;
                int quantityA = 0;
                int quantityB = 0;
                if (ingredientInRecipeCount > 0)
                {
                    CraftingIngredientDTO ingredient = ingredients[ingredientCursor];
                    componentA = ingredient.ItemHash;
                    quantityA = ingredient.Quantity;
                }

                if (ingredientInRecipeCount > 1)
                {
                    CraftingIngredientDTO ingredient = ingredients[ingredientCursor + 1];
                    componentB = ingredient.ItemHash;
                    quantityB = ingredient.Quantity;
                }

                CraftingRecipeDTO recipe = BuildRecipe(resultHash, componentA, quantityA, componentB, quantityB);
                recipe.Reserved0 = recipeHash;
                recipe.Reserved1 = unchecked((uint)ingredientCursor);
                recipe.Reserved2 = unchecked((uint)ingredientInRecipeCount);
                recipes[recipeIndex] = recipe;
                masks[recipeIndex] = new CraftingRecipeMaskDTO
                {
                    RequirementMask = ComputeRequirementMask(ingredients, ingredientCursor, ingredientInRecipeCount),
                    ResultHash = resultHash,
                    RecipeIndex = unchecked((uint)recipeIndex)
                };
            }

            hydratedRecipeCount = recipeCount;
            hydratedIngredientCount = ingredientCount;
            return ShinobuTransactionStatus.Success;
        }

        public static int IndexOf(NativeArray<uint> hashes, NativeArray<int> quantities, uint itemHash)
        {
            if (!hashes.IsCreated || !quantities.IsCreated || itemHash == 0u)
                return -1;

            int capacity = math.min(hashes.Length, quantities.Length);
            for (int index = 0; index < capacity; index++)
            {
                if (hashes[index] == itemHash && quantities[index] > 0)
                    return index;
            }

            return -1;
        }

        public static void WarmSignalLanes()
        {
            SignalBus<MockItemAcquiredSignal>.Configure(MockItemAcquiredSignal.ExpectedCapacity, MockItemAcquiredSignal.MaxFrameSignals, MockItemAcquiredSignal.LowTierFrameSignals, MockItemAcquiredSignal.LaneHash);
            SignalBus<MockItemAcquiredSignal>.EnsureInitialized();
            SignalBus<MockCraftingRequestSignal>.Configure(MockCraftingRequestSignal.ExpectedCapacity, MockCraftingRequestSignal.MaxFrameSignals, MockCraftingRequestSignal.LowTierFrameSignals, MockCraftingRequestSignal.LaneHash);
            SignalBus<MockCraftingRequestSignal>.EnsureInitialized();
            SignalBus<MockConsumeSignal>.Configure(MockConsumeSignal.ExpectedCapacity, MockConsumeSignal.MaxFrameSignals, MockConsumeSignal.LowTierFrameSignals, MockConsumeSignal.LaneHash);
            SignalBus<MockConsumeSignal>.EnsureInitialized();
            SignalBus<MockToolUsedSignal>.Configure(MockToolUsedSignal.ExpectedCapacity, MockToolUsedSignal.MaxFrameSignals, MockToolUsedSignal.LowTierFrameSignals, MockToolUsedSignal.LaneHash);
            SignalBus<MockToolUsedSignal>.EnsureInitialized();
            SignalBus<ToolBrokenSignal>.Configure(ToolBrokenSignal.ExpectedCapacity, ToolBrokenSignal.MaxFrameSignals, ToolBrokenSignal.LowTierFrameSignals, ToolBrokenSignal.LaneHash);
            SignalBus<ToolBrokenSignal>.EnsureInitialized();
            SignalBus<EncumbranceSignal>.Configure(EncumbranceSignal.ExpectedCapacity, EncumbranceSignal.MaxFrameSignals, EncumbranceSignal.LowTierFrameSignals, EncumbranceSignal.LaneHash);
            SignalBus<EncumbranceSignal>.EnsureInitialized();
            SignalBus<EquipItemSignal>.Configure(EquipItemSignal.ExpectedCapacity, EquipItemSignal.MaxFrameSignals, EquipItemSignal.LowTierFrameSignals, EquipItemSignal.LaneHash);
            SignalBus<EquipItemSignal>.EnsureInitialized();
            SignalBus<MockHotbarSelectSignal>.Configure(MockHotbarSelectSignal.ExpectedCapacity, MockHotbarSelectSignal.MaxFrameSignals, MockHotbarSelectSignal.LowTierFrameSignals, MockHotbarSelectSignal.LaneHash);
            SignalBus<MockHotbarSelectSignal>.EnsureInitialized();
            SignalBus<DebrisDestroyedSignal>.Configure(DebrisDestroyedSignal.ExpectedCapacity, DebrisDestroyedSignal.MaxFrameSignals, DebrisDestroyedSignal.LowTierFrameSignals, DebrisDestroyedSignal.LaneHash);
            SignalBus<DebrisDestroyedSignal>.EnsureInitialized();
        }

        public static void ClearLedgerSlots(NativeArray<uint> hashes, NativeArray<int> quantities, NativeArray<float> durabilities, int startIndex, int count)
        {
            if (!TryResolveCapacity(hashes, quantities, durabilities, out int capacity) ||
                startIndex < 0 ||
                count < 0 ||
                startIndex > capacity - count)
            {
                return;
            }

            for (int index = startIndex; index < startIndex + count; index++)
            {
                hashes[index] = 0u;
                quantities[index] = 0;
                durabilities[index] = 0f;
            }
        }

        public static int ScrubGhostSlots(NativeArray<uint> hashes, NativeArray<int> quantities, NativeArray<float> durabilities)
        {
            if (!TryResolveCapacity(hashes, quantities, durabilities, out int capacity))
                return 0;

            int scrubbed = 0;
            int* hashPtr = (int*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(hashes);
            int* quantityPtr = (int*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(quantities);
            for (int index = 0; index < capacity; index++)
            {
                ref int hashRef = ref UnsafeUtility.AsRef<int>(hashPtr + index);
                ref int quantityRef = ref UnsafeUtility.AsRef<int>(quantityPtr + index);
                int hashBits = Interlocked.CompareExchange(ref hashRef, 0, 0);
                int quantity = Interlocked.CompareExchange(ref quantityRef, 0, 0);
                float durability = durabilities[index];

                if (hashBits == 0)
                {
                    if (quantity != 0 || durability != 0f)
                    {
                        Interlocked.Exchange(ref quantityRef, 0);
                        durabilities[index] = 0f;
                        scrubbed++;
                    }

                    continue;
                }

                if (quantity <= 0)
                {
                    Interlocked.Exchange(ref quantityRef, 0);
                    Interlocked.Exchange(ref hashRef, 0);
                    durabilities[index] = 0f;
                    scrubbed++;
                    continue;
                }

                if (!math.isfinite(durability))
                {
                    durabilities[index] = DefaultDurability01;
                    scrubbed++;
                }
            }

            return scrubbed;
        }

        public static bool CanAcceptDelta(NativeArray<uint> hashes, NativeArray<int> quantities, uint itemHash, int deltaQuantity)
        {
            if (!hashes.IsCreated || !quantities.IsCreated || itemHash == 0u || deltaQuantity == 0)
                return false;

            int capacity = math.min(hashes.Length, quantities.Length);
            if (capacity <= 0)
                return false;

            if (deltaQuantity < 0)
                return deltaQuantity != int.MinValue && CountQuantity(hashes, quantities, itemHash) >= -deltaQuantity;

            for (int index = 0; index < capacity; index++)
            {
                int quantity = quantities[index];
                uint hash = hashes[index];
                if (hash != itemHash || quantity <= 0)
                    continue;

                long nextQuantity = (long)quantity + deltaQuantity;
                if (nextQuantity >= 0L && nextQuantity <= int.MaxValue)
                    return true;
            }

            for (int index = 0; index < capacity; index++)
            {
                if (hashes[index] == 0u && quantities[index] == 0)
                    return true;
            }

            return false;
        }

        public static int CountQuantity(NativeArray<uint> hashes, NativeArray<int> quantities, uint itemHash)
        {
            if (!hashes.IsCreated || !quantities.IsCreated || itemHash == 0u)
                return 0;

            int total = 0;
            int capacity = math.min(hashes.Length, quantities.Length);
            for (int index = 0; index < capacity; index++)
            {
                if (hashes[index] != itemHash)
                    continue;

                int quantity = quantities[index];
                if (quantity <= 0)
                    continue;

                total = total > int.MaxValue - quantity ? int.MaxValue : total + quantity;
            }

            return total;
        }

        public static bool TryTransactItem(
            NativeArray<uint> hashes,
            NativeArray<int> quantities,
            NativeArray<float> durabilities,
            uint itemHash,
            int deltaQuantity,
            float defaultDurability01,
            out int slotIndex)
        {
            slotIndex = -1;
            if (!TryResolveCapacity(hashes, quantities, durabilities, out int capacity) ||
                itemHash == 0u ||
                deltaQuantity == 0 ||
                deltaQuantity == int.MinValue)
            {
                return false;
            }

            if (deltaQuantity < 0)
                return TryApplyNegativeDeltaAcrossSlots(hashes, quantities, durabilities, capacity, itemHash, -deltaQuantity, out slotIndex);

            return TryApplyPositiveDelta(hashes, quantities, durabilities, capacity, itemHash, deltaQuantity, defaultDurability01, out slotIndex);
        }

        public static ShinobuTransactionStatus TryCraftAtomicRollback(
            NativeArray<uint> hashes,
            NativeArray<int> quantities,
            NativeArray<float> durabilities,
            in CraftingRecipeDTO recipe,
            ulong currentInventoryMask,
            ulong requirementMask,
            out int resultSlot)
        {
            resultSlot = -1;
            if (!TryResolveCapacity(hashes, quantities, durabilities, out _) || recipe.ResultHash == 0u)
                return ShinobuTransactionStatus.InvalidInput;

            ulong requiredMask = requirementMask != 0UL ? requirementMask : ComputeRequirementMask(in recipe);
            if (requiredMask != 0UL && (currentInventoryMask & requiredMask) != requiredMask)
                return ShinobuTransactionStatus.MaskMissing;

            BuildMergedRequirements(in recipe, out uint reqAHash, out int reqAQty, out uint reqBHash, out int reqBQty);
            if (reqAQty <= 0 && reqBQty <= 0)
                return ShinobuTransactionStatus.InvalidInput;

            if ((reqAQty > 0 && CountQuantity(hashes, quantities, reqAHash) < reqAQty) ||
                (reqBQty > 0 && CountQuantity(hashes, quantities, reqBHash) < reqBQty))
            {
                return ShinobuTransactionStatus.MissingIngredient;
            }

            if (!CanAcceptDelta(hashes, quantities, recipe.ResultHash, 1))
                return ShinobuTransactionStatus.OutputFull;

            bool removedA = false;
            bool removedB = false;
            if (reqAQty > 0)
            {
                if (!TryTransactItem(hashes, quantities, durabilities, reqAHash, -reqAQty, DefaultDurability01, out _))
                    return ShinobuTransactionStatus.AtomicConflict;

                removedA = true;
            }

            if (reqBQty > 0)
            {
                if (!TryTransactItem(hashes, quantities, durabilities, reqBHash, -reqBQty, DefaultDurability01, out _))
                {
                    if (removedA)
                        TryTransactItem(hashes, quantities, durabilities, reqAHash, reqAQty, DefaultDurability01, out _);
                    return ShinobuTransactionStatus.AtomicConflict;
                }

                removedB = true;
            }

            if (TryTransactItem(hashes, quantities, durabilities, recipe.ResultHash, 1, DefaultDurability01, out resultSlot))
                return ShinobuTransactionStatus.Success;

            if (removedB)
                TryTransactItem(hashes, quantities, durabilities, reqBHash, reqBQty, DefaultDurability01, out _);
            if (removedA)
                TryTransactItem(hashes, quantities, durabilities, reqAHash, reqAQty, DefaultDurability01, out _);

            return ShinobuTransactionStatus.OutputFull;
        }

        public static ShinobuTransactionStatus TryCraftAtomicRollback(
            NativeArray<uint> hashes,
            NativeArray<int> quantities,
            NativeArray<float> durabilities,
            in CraftingRecipeDTO recipe,
            NativeArray<CraftingIngredientDTO> ingredients,
            ulong currentInventoryMask,
            ulong requirementMask,
            out int resultSlot)
        {
            resultSlot = -1;
            if (!TryResolveCapacity(hashes, quantities, durabilities, out _) || recipe.ResultHash == 0u)
                return ShinobuTransactionStatus.InvalidInput;

            int ingredientCursor = unchecked((int)recipe.Reserved1);
            int ingredientCount = unchecked((int)recipe.Reserved2);
            if (!ingredients.IsCreated || ingredientCount <= 0)
            {
                return TryCraftAtomicRollback(
                    hashes,
                    quantities,
                    durabilities,
                    in recipe,
                    currentInventoryMask,
                    requirementMask,
                    out resultSlot);
            }

            if (ingredientCursor < 0 ||
                ingredientCount < 0 ||
                ingredientCursor > ingredients.Length - ingredientCount)
            {
                return ShinobuTransactionStatus.InvalidInput;
            }

            ulong requiredMask = requirementMask != 0UL
                ? requirementMask
                : ComputeRequirementMask(ingredients, ingredientCursor, ingredientCount);
            if (requiredMask != 0UL && (currentInventoryMask & requiredMask) != requiredMask)
                return ShinobuTransactionStatus.MaskMissing;

            bool hasRequirement = false;
            for (int offset = 0; offset < ingredientCount; offset++)
            {
                CraftingIngredientDTO ingredient = ingredients[ingredientCursor + offset];
                if (ingredient.ItemHash == 0u || ingredient.Quantity == 0)
                    return ShinobuTransactionStatus.InvalidInput;

                if (!IsFirstIngredientOccurrence(ingredients, ingredientCursor, offset, ingredient.ItemHash))
                    continue;

                int totalQuantity = SumIngredientQuantity(ingredients, ingredientCursor, ingredientCount, ingredient.ItemHash);
                if (totalQuantity <= 0)
                    return ShinobuTransactionStatus.InvalidInput;

                hasRequirement = true;
                if (CountQuantity(hashes, quantities, ingredient.ItemHash) < totalQuantity)
                    return ShinobuTransactionStatus.MissingIngredient;
            }

            if (!hasRequirement)
                return ShinobuTransactionStatus.InvalidInput;

            if (!CanAcceptDelta(hashes, quantities, recipe.ResultHash, 1))
                return ShinobuTransactionStatus.OutputFull;

            for (int offset = 0; offset < ingredientCount; offset++)
            {
                CraftingIngredientDTO ingredient = ingredients[ingredientCursor + offset];
                if (!IsFirstIngredientOccurrence(ingredients, ingredientCursor, offset, ingredient.ItemHash))
                    continue;

                int totalQuantity = SumIngredientQuantity(ingredients, ingredientCursor, ingredientCount, ingredient.ItemHash);
                if (!TryTransactItem(hashes, quantities, durabilities, ingredient.ItemHash, -totalQuantity, DefaultDurability01, out _))
                {
                    RollbackIngredientDeductions(
                        hashes,
                        quantities,
                        durabilities,
                        ingredients,
                        ingredientCursor,
                        ingredientCount,
                        offset);
                    return ShinobuTransactionStatus.AtomicConflict;
                }
            }

            if (TryTransactItem(hashes, quantities, durabilities, recipe.ResultHash, 1, DefaultDurability01, out resultSlot))
                return ShinobuTransactionStatus.Success;

            RollbackIngredientDeductions(
                hashes,
                quantities,
                durabilities,
                ingredients,
                ingredientCursor,
                ingredientCount,
                ingredientCount);
            return ShinobuTransactionStatus.OutputFull;
        }

        public static ulong BuildInventoryMask(NativeArray<uint> hashes, NativeArray<int> quantities)
        {
            if (!hashes.IsCreated || !quantities.IsCreated)
                return 0UL;

            ulong mask = 0UL;
            int capacity = math.min(hashes.Length, quantities.Length);
            for (int index = 0; index < capacity; index++)
            {
                if (quantities[index] <= 0)
                    continue;

                mask |= InventoryMaterialMask.ResolveBit(hashes[index]);
            }

            return mask;
        }

        public static ulong ComputeRequirementMask(in CraftingRecipeDTO recipe)
        {
            ulong mask = 0UL;
            if (recipe.ComponentA != 0u && recipe.QuantityA > 0)
                mask |= InventoryMaterialMask.ResolveBit(recipe.ComponentA);
            if (recipe.ComponentB != 0u && recipe.QuantityB > 0)
                mask |= InventoryMaterialMask.ResolveBit(recipe.ComponentB);
            return mask;
        }

        public static ulong ComputeRequirementMask(NativeArray<CraftingIngredientDTO> ingredients, int ingredientCursor, int ingredientCount)
        {
            if (!ingredients.IsCreated ||
                ingredientCursor < 0 ||
                ingredientCount <= 0 ||
                ingredientCursor > ingredients.Length - ingredientCount)
            {
                return 0UL;
            }

            ulong mask = 0UL;
            for (int offset = 0; offset < ingredientCount; offset++)
            {
                CraftingIngredientDTO ingredient = ingredients[ingredientCursor + offset];
                if (ingredient.ItemHash != 0u && ingredient.Quantity > 0)
                    mask |= InventoryMaterialMask.ResolveBit(ingredient.ItemHash);
            }

            return mask;
        }

        public static bool HasRequiredIngredientQuantities(
            NativeArray<uint> hashes,
            NativeArray<int> quantities,
            NativeArray<CraftingIngredientDTO> ingredients,
            int ingredientCursor,
            int ingredientCount)
        {
            if (!hashes.IsCreated ||
                !quantities.IsCreated ||
                !ingredients.IsCreated ||
                ingredientCursor < 0 ||
                ingredientCount <= 0 ||
                ingredientCursor > ingredients.Length - ingredientCount)
            {
                return false;
            }

            for (int offset = 0; offset < ingredientCount; offset++)
            {
                CraftingIngredientDTO ingredient = ingredients[ingredientCursor + offset];
                if (ingredient.ItemHash == 0u || ingredient.Quantity == 0)
                    return false;

                if (!IsFirstIngredientOccurrence(ingredients, ingredientCursor, offset, ingredient.ItemHash))
                    continue;

                int totalQuantity = SumIngredientQuantity(ingredients, ingredientCursor, ingredientCount, ingredient.ItemHash);
                if (CountQuantity(hashes, quantities, ingredient.ItemHash) < totalQuantity)
                    return false;
            }

            return true;
        }

        public static int ResolveRecipeBatchLimit(ShinobuHardwareTier tier, int pendingRecipeCount)
        {
            return ResolveRecipeBatchLimit(HardwareTierToQualityWeight(tier), pendingRecipeCount);
        }

        public static int ResolveRecipeBatchLimit(float globalQualityWeight, int pendingRecipeCount)
        {
            if (pendingRecipeCount <= 0)
                return 0;

            float weight = math.saturate(math.isfinite(globalQualityWeight) ? globalQualityWeight : 0f);
            float smoothWeight = weight * weight * (3f - 2f * weight);
            int limit = (int)math.round(math.lerp(MinRecipeBatchLimit, MaxRecipeBatchLimit, smoothWeight));
            return math.min(pendingRecipeCount, math.clamp(limit, MinRecipeBatchLimit, MaxRecipeBatchLimit));
        }

        private static float HardwareTierToQualityWeight(ShinobuHardwareTier tier)
        {
            switch (tier)
            {
                case ShinobuHardwareTier.Ultra:
                    return 1f;
                case ShinobuHardwareTier.High:
                    return 0.72f;
                case ShinobuHardwareTier.Middle:
                    return 0.38f;
                default:
                    return 0f;
            }
        }

        public static ShinobuTransactionStatus ExportRle(
            NativeArray<uint> hashes,
            NativeArray<int> quantities,
            NativeArray<float> durabilities,
            NativeArray<byte> destination,
            out int bytesWritten)
        {
            bytesWritten = 0;
            if (!TryResolveCapacity(hashes, quantities, durabilities, out int capacity) || !destination.IsCreated)
                return ShinobuTransactionStatus.InvalidInput;

            byte* dst = (byte*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(destination);
            int offset = 0;
            int recordCountOffset;
            if (!TryWriteUInt(dst, destination.Length, ref offset, RleMagic) ||
                !TryWriteInt(dst, destination.Length, ref offset, capacity))
            {
                return ShinobuTransactionStatus.RleBufferTooSmall;
            }

            recordCountOffset = offset;
            if (!TryWriteInt(dst, destination.Length, ref offset, 0))
                return ShinobuTransactionStatus.RleBufferTooSmall;

            int recordCount = 0;
            int index = 0;
            while (index < capacity)
            {
                uint hash = quantities[index] > 0 ? hashes[index] : 0u;
                int quantity = hash != 0u ? quantities[index] : 0;
                float durability = hash != 0u ? durabilities[index] : 0f;
                int runLength = 1;

                if (hash == 0u)
                {
                    while (index + runLength < capacity &&
                           (hashes[index + runLength] == 0u || quantities[index + runLength] <= 0))
                    {
                        runLength++;
                    }
                }

                if (offset > destination.Length - RleRecordBytes ||
                    !TryWriteUInt(dst, destination.Length, ref offset, hash) ||
                    !TryWriteInt(dst, destination.Length, ref offset, quantity) ||
                    !TryWriteInt(dst, destination.Length, ref offset, runLength) ||
                    !TryWriteFloat(dst, destination.Length, ref offset, durability))
                {
                    return ShinobuTransactionStatus.RleBufferTooSmall;
                }

                recordCount++;
                index += runLength;
            }

            WriteIntAt(dst, destination.Length, recordCountOffset, recordCount);
            bytesWritten = math.max(RleHeaderBytes, offset);
            return ShinobuTransactionStatus.Success;
        }

        private static ShinobuTransactionStatus ExportRleToVaultScratch(
            IDataVault vault,
            NativeArray<uint> hashes,
            NativeArray<int> quantities,
            NativeArray<float> durabilities,
            int byteCapacity,
            out NativeArray<byte> destination,
            out int bytesWritten)
        {
            destination = default;
            bytesWritten = 0;
            if (vault == null || byteCapacity <= 0)
                return ShinobuTransactionStatus.InvalidInput;

            if (!OpenOrAcquireEconomyVaultBuffer(
                BufferID.ShinobuRleScratch,
                byteCapacity,
                NativeArrayOptions.UninitializedMemory,
                vault,
                out destination))
            {
                return ShinobuTransactionStatus.DestinationTooSmall;
            }

            return ExportRle(hashes, quantities, durabilities, destination, out bytesWritten);
        }

#if UNITY_EDITOR
        public static bool TryApplyCsvOverrideLine(ReadOnlySpan<char> line, NativeArray<ItemPhysicalConstantsDTO> constants)
        {
            if (!constants.IsCreated)
                return false;

            line = Trim(line);
            if (line.Length == 0 || line[0] == '#')
                return true;

            int cursor = 0;
            if (!TryReadField(line, ref cursor, out ReadOnlySpan<char> hashField) ||
                !TryReadField(line, ref cursor, out ReadOnlySpan<char> massField) ||
                !TryReadField(line, ref cursor, out ReadOnlySpan<char> volumeField))
            {
                return false;
            }

            TryReadField(line, ref cursor, out ReadOnlySpan<char> stackField);
            uint itemHash = TryParseUInt(hashField, out uint parsedHash) ? parsedHash : Fnv1A32(hashField);
            if (itemHash == 0u ||
                !TryParseFloat(massField, out float massKg) ||
                !TryParseFloat(volumeField, out float volumeLiters))
            {
                return false;
            }

            int maxStack = TryParseInt(stackField, out int parsedStack) ? math.max(1, parsedStack) : 1;
            int firstEmpty = -1;
            for (int index = 0; index < constants.Length; index++)
            {
                ItemPhysicalConstantsDTO dto = constants[index];
                if (dto.ItemHash == itemHash)
                {
                    dto.MassKg = math.max(0f, massKg);
                    dto.VolumeLiters = math.max(0f, volumeLiters);
                    dto.MaxStack = maxStack;
                    constants[index] = dto;
                    return true;
                }

                if (firstEmpty < 0 && dto.ItemHash == 0u)
                    firstEmpty = index;
            }

            if (firstEmpty < 0)
                return false;

            constants[firstEmpty] = new ItemPhysicalConstantsDTO
            {
                ItemHash = itemHash,
                MassKg = math.max(0f, massKg),
                VolumeLiters = math.max(0f, volumeLiters),
                MaxStack = maxStack,
                BaseDurability01 = DefaultDurability01
            };
            return true;
        }
#endif

        public static void RecordTelemetry(
            NativeArray<EconomyTelemetryEntry> telemetry,
            int cursor,
            in EconomyTelemetryEntry entry)
        {
            if (!telemetry.IsCreated || telemetry.Length <= 0)
                return;

            int count = math.min(telemetry.Length, BlackBoxCapacity);
            int index = NormalizeRingCursor(cursor, count);
            telemetry[index] = entry;
        }

        public static void DumpTelemetryRing(NativeArray<EconomyTelemetryEntry>.ReadOnly telemetry, string relativePath = DefaultDumpPath)
        {
            TryWriteTelemetryRing(telemetry, relativePath);
        }

        public static void DumpTelemetryRingH8Dump(NativeArray<EconomyTelemetryEntry>.ReadOnly telemetry)
        {
            DumpTelemetryRing(telemetry, DefaultH8DumpPath);
        }

        public static void DumpTelemetryRingOrdered(
            NativeArray<EconomyTelemetryEntry>.ReadOnly telemetry,
            int latestCursor,
            string relativePath = DefaultDumpPath)
        {
            TryWriteTelemetryRingOrdered(telemetry, latestCursor, relativePath);
        }

        public static bool TryDumpTelemetryOnFault(
            NativeArray<EconomyTelemetryEntry>.ReadOnly telemetry,
            int latestCursor,
            float spikeThresholdMs = 0.5f,
            string relativePath = DefaultH8DumpPath)
        {
            if (!telemetry.IsCreated || telemetry.Length <= 0)
                return false;

            int count = math.min(telemetry.Length, BlackBoxCapacity);
            int start = NormalizeRingCursor(latestCursor, count);
            float threshold = math.max(0.0001f, spikeThresholdMs);
            bool faulted = false;
            for (int offset = 0; offset < count; offset++)
            {
                int index = (start + offset) % count;
                EconomyTelemetryEntry entry = telemetry[index];
                if ((entry.Flags & (TelemetryFlagSpike | TelemetryFlagFatal)) != 0u ||
                    entry.InventoryTransactionTimeMs > threshold)
                {
                    faulted = true;
                    break;
                }
            }

            if (!faulted)
                return false;

            return TryWriteTelemetryRingOrdered(telemetry, latestCursor, relativePath);
        }

        public static void PublishBrokenSignals(NativeArray<ToolBrokenSignal> brokenSignals)
        {
            if (!brokenSignals.IsCreated)
                return;

            for (int index = 0; index < brokenSignals.Length; index++)
            {
                ToolBrokenSignal signal = brokenSignals[index];
                if (signal.ToolHash == 0u || signal.Flags == 0u)
                    continue;

                SignalBus<ToolBrokenSignal>.TryPushTracked(in signal, ref s_x001Shinobu19EconomyLedgerSignalPushDropCount);
            }
        }

        public static void PublishEquipSignals(NativeArray<EquipItemSignal> equipSignals)
        {
            if (!equipSignals.IsCreated)
                return;

            for (int index = 0; index < equipSignals.Length; index++)
            {
                EquipItemSignal signal = equipSignals[index];
                if (signal.ItemHash == 0u)
                    continue;

                SignalBus<EquipItemSignal>.TryPushTracked(in signal, ref s_x001Shinobu19EconomyLedgerSignalPushDropCount);
            }
        }

        public static JobHandle ScheduleTransfer(
            NativeArray<uint> sourceHashes,
            NativeArray<int> sourceQuantities,
            NativeArray<float> sourceDurabilities,
            NativeArray<uint> targetHashes,
            NativeArray<int> targetQuantities,
            NativeArray<float> targetDurabilities,
            NativeArray<int> result,
            int sourceStartIndex,
            int slotCount,
            JobHandle sourceDependency,
            JobHandle targetDependency)
        {
            ShinobuContainerTransferJob job = new ShinobuContainerTransferJob
            {
                SourceHashes = sourceHashes,
                SourceQuantities = sourceQuantities,
                SourceDurabilities = sourceDurabilities,
                TargetHashes = targetHashes,
                TargetQuantities = targetQuantities,
                TargetDurabilities = targetDurabilities,
                Result = result,
                SourceStartIndex = sourceStartIndex,
                SlotCount = slotCount
            };

            return job.Schedule(JobHandle.CombineDependencies(sourceDependency, targetDependency));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool TryResolveCapacity(
            NativeArray<uint> hashes,
            NativeArray<int> quantities,
            NativeArray<float> durabilities,
            out int capacity)
        {
            capacity = 0;
            if (!hashes.IsCreated || !quantities.IsCreated || !durabilities.IsCreated)
                return false;

            capacity = math.min(hashes.Length, math.min(quantities.Length, durabilities.Length));
            return capacity > 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int NormalizeRingCursor(int cursor, int capacity)
        {
            if (capacity <= 0 || cursor == int.MinValue)
                return 0;

            int positiveCursor = cursor < 0 ? -cursor : cursor;
            return positiveCursor % capacity;
        }

        private static bool TryWriteTelemetryRing(
            NativeArray<EconomyTelemetryEntry>.ReadOnly telemetry,
            string relativePath)
        {
            if (!telemetry.IsCreated || telemetry.Length <= 0)
                return false;

            int count = math.min(telemetry.Length, BlackBoxCapacity);
            int byteCount = EconomyDumpHeaderBytes + count * EconomyTelemetryEntrySizeBytes;
            NativeArray<byte> payload = new NativeArray<byte>(byteCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            try
            {
                byte* destination = (byte*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(payload);
                int offset = 0;
                if (!TryWriteUInt32LittleEndian(destination, byteCount, ref offset, EconomyDumpMagic) ||
                    !TryWriteUInt32LittleEndian(destination, byteCount, ref offset, EconomyDumpVersion) ||
                    !TryWriteInt32LittleEndian(destination, byteCount, ref offset, count) ||
                    !TryWriteInt32LittleEndian(destination, byteCount, ref offset, EconomyTelemetryEntrySizeBytes))
                {
                    return false;
                }

                for (int index = 0; index < count; index++)
                {
                    EconomyTelemetryEntry entry = telemetry[index];
                    if (!TryWriteTelemetryEntry(destination, byteCount, ref offset, in entry))
                        return false;
                }

                return offset == byteCount &&
                       NativeFaultDumpWriter.TryWriteAll(ResolveDumpPath(relativePath, DefaultDumpPath), payload, byteCount);
            }
            finally
            {
                payload.Dispose();
            }
        }

        private static bool TryWriteTelemetryRingOrdered(
            NativeArray<EconomyTelemetryEntry>.ReadOnly telemetry,
            int latestCursor,
            string relativePath)
        {
            if (!telemetry.IsCreated || telemetry.Length <= 0)
                return false;

            int count = math.min(telemetry.Length, BlackBoxCapacity);
            int byteCount = EconomyOrderedDumpHeaderBytes + count * EconomyTelemetryEntrySizeBytes;
            NativeArray<byte> payload = new NativeArray<byte>(byteCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            try
            {
                int cursor = NormalizeRingCursor(latestCursor, count);
                int first = (cursor + 1) % count;
                byte* destination = (byte*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(payload);
                int offset = 0;
                if (!TryWriteUInt32LittleEndian(destination, byteCount, ref offset, EconomyDumpMagic) ||
                    !TryWriteUInt32LittleEndian(destination, byteCount, ref offset, EconomyDumpVersion) ||
                    !TryWriteInt32LittleEndian(destination, byteCount, ref offset, count) ||
                    !TryWriteInt32LittleEndian(destination, byteCount, ref offset, EconomyTelemetryEntrySizeBytes) ||
                    !TryWriteInt32LittleEndian(destination, byteCount, ref offset, cursor) ||
                    !TryWriteInt32LittleEndian(destination, byteCount, ref offset, first) ||
                    !TryWriteInt32LittleEndian(destination, byteCount, ref offset, 0) ||
                    !TryWriteInt32LittleEndian(destination, byteCount, ref offset, 0))
                {
                    return false;
                }

                for (int index = 0; index < count; index++)
                {
                    EconomyTelemetryEntry entry = telemetry[(first + index) % count];
                    if (!TryWriteTelemetryEntry(destination, byteCount, ref offset, in entry))
                        return false;
                }

                return offset == byteCount &&
                       NativeFaultDumpWriter.TryWriteAll(ResolveDumpPath(relativePath, DefaultDumpPath), payload, byteCount);
            }
            finally
            {
                payload.Dispose();
            }
        }

        private static bool TryWriteTelemetryEntry(byte* destination, int capacity, ref int offset, in EconomyTelemetryEntry entry)
        {
            return TryWriteInt64LittleEndian(destination, capacity, ref offset, entry.TimestampTicks) &&
                   TryWriteUInt64LittleEndian(destination, capacity, ref offset, entry.InventoryMask) &&
                   TryWriteFloat32LittleEndian(destination, capacity, ref offset, entry.InventoryTransactionTimeMs) &&
                   TryWriteFloat32LittleEndian(destination, capacity, ref offset, entry.MassKg) &&
                   TryWriteFloat32LittleEndian(destination, capacity, ref offset, entry.VolumeLiters) &&
                   TryWriteFloat32LittleEndian(destination, capacity, ref offset, entry.ReservedFloat) &&
                   TryWriteUInt32LittleEndian(destination, capacity, ref offset, entry.FrameIndex) &&
                   TryWriteUInt32LittleEndian(destination, capacity, ref offset, entry.LastItemHash) &&
                   TryWriteUInt32LittleEndian(destination, capacity, ref offset, entry.LastRecipeHash) &&
                   TryWriteUInt32LittleEndian(destination, capacity, ref offset, entry.Flags) &&
                   TryWriteInt32LittleEndian(destination, capacity, ref offset, entry.TotalItemsCrafted) &&
                   TryWriteInt32LittleEndian(destination, capacity, ref offset, entry.TotalItemsTransferred) &&
                   TryWriteInt32LittleEndian(destination, capacity, ref offset, entry.TransactionResult) &&
                   TryWriteInt32LittleEndian(destination, capacity, ref offset, entry.SlotIndex);
        }

        private static string ResolveDumpPath(string relativePath, string fallbackPath)
        {
            string path = string.IsNullOrWhiteSpace(relativePath) ? fallbackPath : relativePath;
            if (Path.IsPathRooted(path))
                return path;

            return Path.Combine(Directory.GetCurrentDirectory(), path);
        }

        private static bool TryWriteUInt64LittleEndian(byte* destination, int capacity, ref int offset, ulong value)
        {
            if (offset > capacity - 8)
                return false;

            destination[offset] = (byte)value;
            destination[offset + 1] = (byte)(value >> 8);
            destination[offset + 2] = (byte)(value >> 16);
            destination[offset + 3] = (byte)(value >> 24);
            destination[offset + 4] = (byte)(value >> 32);
            destination[offset + 5] = (byte)(value >> 40);
            destination[offset + 6] = (byte)(value >> 48);
            destination[offset + 7] = (byte)(value >> 56);
            offset += 8;
            return true;
        }

        private static bool TryWriteInt64LittleEndian(byte* destination, int capacity, ref int offset, long value)
        {
            return TryWriteUInt64LittleEndian(destination, capacity, ref offset, unchecked((ulong)value));
        }

        private static bool TryWriteUInt32LittleEndian(byte* destination, int capacity, ref int offset, uint value)
        {
            if (offset > capacity - 4)
                return false;

            destination[offset] = (byte)value;
            destination[offset + 1] = (byte)(value >> 8);
            destination[offset + 2] = (byte)(value >> 16);
            destination[offset + 3] = (byte)(value >> 24);
            offset += 4;
            return true;
        }

        private static bool TryWriteInt32LittleEndian(byte* destination, int capacity, ref int offset, int value)
        {
            return TryWriteUInt32LittleEndian(destination, capacity, ref offset, unchecked((uint)value));
        }

        private static bool TryWriteFloat32LittleEndian(byte* destination, int capacity, ref int offset, float value)
        {
            return TryWriteUInt32LittleEndian(destination, capacity, ref offset, math.asuint(value));
        }

        private static bool TryApplyPositiveDelta(
            NativeArray<uint> hashes,
            NativeArray<int> quantities,
            NativeArray<float> durabilities,
            int capacity,
            uint itemHash,
            int deltaQuantity,
            float defaultDurability01,
            out int slotIndex)
        {
            slotIndex = -1;
            if (deltaQuantity <= 0)
                return false;

            if (TryApplyDeltaToExisting(hashes, quantities, durabilities, capacity, itemHash, deltaQuantity, out slotIndex))
                return true;

            float durability = math.saturate(math.isfinite(defaultDurability01) ? defaultDurability01 : DefaultDurability01);
            int itemHashBits = unchecked((int)itemHash);
            int* hashPtr = (int*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(hashes);
            int* quantityPtr = (int*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(quantities);

            for (int index = 0; index < capacity; index++)
            {
                ref int hashRef = ref UnsafeUtility.AsRef<int>(hashPtr + index);
                ref int quantityRef = ref UnsafeUtility.AsRef<int>(quantityPtr + index);
                int observedHash = Interlocked.CompareExchange(ref hashRef, 0, 0);
                if (observedHash == 0)
                {
                    if (Interlocked.CompareExchange(ref quantityRef, EmptySlotClaimSentinel, 0) != 0)
                        continue;

                    observedHash = Interlocked.CompareExchange(ref hashRef, 0, 0);
                    if (observedHash != 0)
                    {
                        Interlocked.Exchange(ref quantityRef, 0);
                        if (unchecked((uint)observedHash) == itemHash &&
                            TryApplyDeltaToExisting(hashes, quantities, durabilities, capacity, itemHash, deltaQuantity, out slotIndex))
                        {
                            return true;
                        }

                        continue;
                    }

                    Interlocked.Exchange(ref hashRef, itemHashBits);
                    durabilities[index] = durability;
                    Interlocked.Exchange(ref quantityRef, deltaQuantity);
                    slotIndex = index;
                    return true;
                }

                if (unchecked((uint)observedHash) == itemHash &&
                    TryApplyDeltaToExisting(hashes, quantities, durabilities, capacity, itemHash, deltaQuantity, out slotIndex))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TryApplyDeltaToExisting(
            NativeArray<uint> hashes,
            NativeArray<int> quantities,
            NativeArray<float> durabilities,
            int capacity,
            uint itemHash,
            int deltaQuantity,
            out int slotIndex)
        {
            slotIndex = -1;
            int* hashPtr = (int*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(hashes);
            int* quantityPtr = (int*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(quantities);

            for (int index = 0; index < capacity; index++)
            {
                ref int hashRef = ref UnsafeUtility.AsRef<int>(hashPtr + index);
                if (unchecked((uint)Interlocked.CompareExchange(ref hashRef, 0, 0)) != itemHash)
                    continue;

                ref int quantityRef = ref UnsafeUtility.AsRef<int>(quantityPtr + index);
                if (!TryApplyQuantityDelta(ref quantityRef, deltaQuantity, out int newQuantity))
                    continue;

                if (newQuantity <= 0)
                {
                    durabilities[index] = 0f;
                    Interlocked.Exchange(ref quantityRef, 0);
                    Interlocked.Exchange(ref hashRef, 0);
                }

                slotIndex = index;
                return true;
            }

            return false;
        }

        private static bool TryApplyQuantityDelta(ref int quantityRef, int deltaQuantity, out int newQuantity)
        {
            newQuantity = 0;
            for (int attempt = 0; attempt < SlotCasRetryLimit; attempt++)
            {
                int current = Interlocked.CompareExchange(ref quantityRef, 0, 0);
                if (current == 0)
                    return false;
                if (current < 0)
                    continue;

                if (Interlocked.CompareExchange(ref quantityRef, -current, current) != current)
                    continue;

                long nextLong = (long)current + deltaQuantity;
                if (nextLong < 0L || nextLong > int.MaxValue)
                {
                    Interlocked.Exchange(ref quantityRef, current);
                    return false;
                }

                int next = (int)nextLong;
                Interlocked.Exchange(ref quantityRef, next);
                newQuantity = next;
                return true;
            }

            return false;
        }

        private static bool TryApplyNegativeDeltaAcrossSlots(
            NativeArray<uint> hashes,
            NativeArray<int> quantities,
            NativeArray<float> durabilities,
            int capacity,
            uint itemHash,
            int removeQuantity,
            out int slotIndex)
        {
            slotIndex = -1;
            if (removeQuantity <= 0)
                return false;

            int itemHashBits = unchecked((int)itemHash);
            int* hashPtr = (int*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(hashes);
            int* quantityPtr = (int*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(quantities);
            int remaining = removeQuantity;
            int removed = 0;

            for (int index = 0; index < capacity && remaining > 0; index++)
            {
                ref int hashRef = ref UnsafeUtility.AsRef<int>(hashPtr + index);
                if (Interlocked.CompareExchange(ref hashRef, 0, 0) != itemHashBits)
                    continue;

                ref int quantityRef = ref UnsafeUtility.AsRef<int>(quantityPtr + index);
                for (int attempt = 0; attempt < SlotCasRetryLimit; attempt++)
                {
                    int current = Interlocked.CompareExchange(ref quantityRef, 0, 0);
                    if (current == 0)
                        break;
                    if (current < 0)
                        continue;

                    if (Interlocked.CompareExchange(ref quantityRef, -current, current) != current)
                        continue;

                    int deducted = math.min(current, remaining);
                    int nextQuantity = current - deducted;
                    if (nextQuantity <= 0)
                    {
                        durabilities[index] = 0f;
                        Interlocked.Exchange(ref quantityRef, 0);
                        Interlocked.Exchange(ref hashRef, 0);
                    }
                    else
                    {
                        Interlocked.Exchange(ref quantityRef, nextQuantity);
                    }

                    remaining -= deducted;
                    removed += deducted;
                    slotIndex = index;
                    break;
                }
            }

            if (remaining == 0)
                return true;

            if (removed > 0)
                TryApplyPositiveDelta(hashes, quantities, durabilities, capacity, itemHash, removed, DefaultDurability01, out _);

            slotIndex = -1;
            return false;
        }

        private static void BuildMergedRequirements(
            in CraftingRecipeDTO recipe,
            out uint reqAHash,
            out int reqAQty,
            out uint reqBHash,
            out int reqBQty)
        {
            reqAHash = recipe.ComponentA;
            reqAQty = recipe.ComponentA != 0u ? math.max(0, recipe.QuantityA) : 0;
            reqBHash = recipe.ComponentB;
            reqBQty = recipe.ComponentB != 0u ? math.max(0, recipe.QuantityB) : 0;

            if (reqAHash != 0u && reqAHash == reqBHash)
            {
                reqAQty = reqAQty > int.MaxValue - reqBQty ? int.MaxValue : reqAQty + reqBQty;
                reqBHash = 0u;
                reqBQty = 0;
            }
        }

        private static bool IsFirstIngredientOccurrence(
            NativeArray<CraftingIngredientDTO> ingredients,
            int ingredientCursor,
            int offset,
            uint itemHash)
        {
            for (int prior = 0; prior < offset; prior++)
            {
                if (ingredients[ingredientCursor + prior].ItemHash == itemHash)
                    return false;
            }

            return true;
        }

        private static int SumIngredientQuantity(
            NativeArray<CraftingIngredientDTO> ingredients,
            int ingredientCursor,
            int ingredientCount,
            uint itemHash)
        {
            int total = 0;
            for (int offset = 0; offset < ingredientCount; offset++)
            {
                CraftingIngredientDTO ingredient = ingredients[ingredientCursor + offset];
                if (ingredient.ItemHash != itemHash)
                    continue;

                int quantity = ingredient.Quantity;
                total = total > int.MaxValue - quantity ? int.MaxValue : total + quantity;
            }

            return total;
        }

        private static void RollbackIngredientDeductions(
            NativeArray<uint> hashes,
            NativeArray<int> quantities,
            NativeArray<float> durabilities,
            NativeArray<CraftingIngredientDTO> ingredients,
            int ingredientCursor,
            int ingredientCount,
            int completedOffsetExclusive)
        {
            int limit = math.clamp(completedOffsetExclusive, 0, ingredientCount);
            for (int offset = 0; offset < limit; offset++)
            {
                CraftingIngredientDTO ingredient = ingredients[ingredientCursor + offset];
                if (ingredient.ItemHash == 0u ||
                    !IsFirstIngredientOccurrence(ingredients, ingredientCursor, offset, ingredient.ItemHash))
                {
                    continue;
                }

                int totalQuantity = SumIngredientQuantity(ingredients, ingredientCursor, ingredientCount, ingredient.ItemHash);
                if (totalQuantity > 0)
                    TryTransactItem(hashes, quantities, durabilities, ingredient.ItemHash, totalQuantity, DefaultDurability01, out _);
            }
        }

        private static bool RangeInside(int fileSize, int offset, int count, int stride)
        {
            if (fileSize < 0 || offset < 0 || count < 0 || stride <= 0 || offset > fileSize)
                return false;

            long end = (long)offset + (long)count * stride;
            return end <= fileSize;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsAligned(int value, int alignment)
        {
            return alignment > 0 && (value & (alignment - 1)) == 0;
        }

        private static uint ComputeCrc32(byte* source, int byteCount)
        {
            uint crc = 0xFFFFFFFFu;
            for (int index = 0; index < byteCount; index++)
            {
                crc ^= source[index];
                for (int bit = 0; bit < 8; bit++)
                {
                    uint mask = 0u - (crc & 1u);
                    crc = (crc >> 1) ^ (Crc32Polynomial & mask);
                }
            }

            return ~crc;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint ReadUInt32LittleEndian(byte* source, int offset)
        {
            return (uint)source[offset] |
                   ((uint)source[offset + 1] << 8) |
                   ((uint)source[offset + 2] << 16) |
                   ((uint)source[offset + 3] << 24);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int ReadInt32LittleEndian(byte* source, int offset)
        {
            return unchecked((int)ReadUInt32LittleEndian(source, offset));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ushort ReadUInt16LittleEndian(byte* source, int offset)
        {
            return (ushort)(source[offset] | (source[offset + 1] << 8));
        }

        private static bool TryWriteUInt(byte* destination, int capacity, ref int offset, uint value)
        {
            if (offset > capacity - 4)
                return false;

            UnsafeUtility.MemCpy(destination + offset, &value, 4);
            offset += 4;
            return true;
        }

        private static bool TryWriteInt(byte* destination, int capacity, ref int offset, int value)
        {
            if (offset > capacity - 4)
                return false;

            UnsafeUtility.MemCpy(destination + offset, &value, 4);
            offset += 4;
            return true;
        }

        private static bool TryWriteFloat(byte* destination, int capacity, ref int offset, float value)
        {
            if (offset > capacity - 4)
                return false;

            UnsafeUtility.MemCpy(destination + offset, &value, 4);
            offset += 4;
            return true;
        }

        private static void WriteIntAt(byte* destination, int capacity, int offset, int value)
        {
            if (offset < 0 || offset > capacity - 4)
                return;

            UnsafeUtility.MemCpy(destination + offset, &value, 4);
        }

        private static bool TryReadField(ReadOnlySpan<char> line, ref int cursor, out ReadOnlySpan<char> field)
        {
            field = default;
            if (cursor > line.Length)
                return false;

            int start = cursor;
            int end = start;
            while (end < line.Length && line[end] != ',')
                end++;

            field = Trim(line.Slice(start, end - start));
            cursor = end < line.Length ? end + 1 : line.Length + 1;
            return true;
        }

        private static ReadOnlySpan<char> Trim(ReadOnlySpan<char> value)
        {
            int start = 0;
            int end = value.Length - 1;
            while (start < value.Length && char.IsWhiteSpace(value[start]))
                start++;
            while (end >= start && char.IsWhiteSpace(value[end]))
                end--;

            return start <= end ? value.Slice(start, end - start + 1) : ReadOnlySpan<char>.Empty;
        }

        private static bool TryParseUInt(ReadOnlySpan<char> value, out uint result)
        {
            result = 0u;
            if (value.Length == 0)
                return false;

            for (int index = 0; index < value.Length; index++)
            {
                char c = value[index];
                if (c < '0' || c > '9')
                    return false;

                uint digit = (uint)(c - '0');
                if (result > (uint.MaxValue - digit) / 10u)
                    return false;

                result = result * 10u + digit;
            }

            return true;
        }

        private static bool TryParseInt(ReadOnlySpan<char> value, out int result)
        {
            result = 0;
            if (value.Length == 0)
                return false;

            int cursor = 0;
            int sign = 1;
            if (value[0] == '-')
            {
                sign = -1;
                cursor = 1;
            }

            long parsed = 0L;
            for (; cursor < value.Length; cursor++)
            {
                char c = value[cursor];
                if (c < '0' || c > '9')
                    return false;

                parsed = parsed * 10L + (c - '0');
                if (parsed > int.MaxValue)
                    return false;
            }

            result = (int)(parsed * sign);
            return true;
        }

        private static bool TryParseFloat(ReadOnlySpan<char> value, out float result)
        {
            result = 0f;
            if (value.Length == 0)
                return false;

            int cursor = 0;
            float sign = 1f;
            if (value[0] == '-')
            {
                sign = -1f;
                cursor = 1;
            }

            double whole = 0d;
            while (cursor < value.Length && value[cursor] >= '0' && value[cursor] <= '9')
            {
                whole = whole * 10d + (value[cursor] - '0');
                cursor++;
            }

            double fraction = 0d;
            double scale = 1d;
            if (cursor < value.Length && value[cursor] == '.')
            {
                cursor++;
                while (cursor < value.Length && value[cursor] >= '0' && value[cursor] <= '9')
                {
                    fraction = fraction * 10d + (value[cursor] - '0');
                    scale *= 10d;
                    cursor++;
                }
            }

            if (cursor != value.Length)
                return false;

            result = (float)(sign * (whole + fraction / scale));
            return math.isfinite(result);
        }

        private static uint Fnv1A32(ReadOnlySpan<char> value)
        {
            if (value.Length == 0)
                return 0u;

            uint hash = 2166136261u;
            for (int index = 0; index < value.Length; index++)
            {
                char current = value[index];
                hash ^= (byte)current;
                hash *= 16777619u;
                hash ^= (byte)(current >> 8);
                hash *= 16777619u;
            }

            return hash;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct ShinobuIndexOfJob : IJob
    {
        [ReadOnly, NoAlias] public NativeArray<uint> Hashes;
        [ReadOnly, NoAlias] public NativeArray<int> Quantities;
        [WriteOnly, NoAlias] public NativeArray<int> Result;
        public uint ItemHash;

        public void Execute()
        {
            if (Result.IsCreated && Result.Length > 0)
                Result[0] = Shinobu19EconomyLedger.IndexOf(Hashes, Quantities, ItemHash);
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct ShinobuGhostSlotScrubJob : IJob
    {
        [NoAlias, NativeDisableParallelForRestriction] public NativeArray<uint> Hashes;
        [NoAlias, NativeDisableParallelForRestriction] public NativeArray<int> Quantities;
        [NoAlias, NativeDisableParallelForRestriction] public NativeArray<float> Durabilities;
        [WriteOnly, NoAlias] public NativeArray<int> Result;

        public void Execute()
        {
            int scrubbed = Shinobu19EconomyLedger.ScrubGhostSlots(Hashes, Quantities, Durabilities);
            if (Result.IsCreated && Result.Length > 0)
                Result[0] = scrubbed;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public unsafe struct ShinobuZeroMemClearJob : IJob
    {
        [NoAlias, NativeDisableParallelForRestriction] public NativeArray<uint> Hashes;
        [NoAlias, NativeDisableParallelForRestriction] public NativeArray<int> Quantities;
        [NoAlias, NativeDisableParallelForRestriction] public NativeArray<float> Durabilities;

        public void Execute()
        {
            if (Hashes.IsCreated)
                UnsafeUtility.MemClear(NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(Hashes), (long)Hashes.Length * UnsafeUtility.SizeOf<uint>());
            if (Quantities.IsCreated)
                UnsafeUtility.MemClear(NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(Quantities), (long)Quantities.Length * UnsafeUtility.SizeOf<int>());
            if (Durabilities.IsCreated)
                UnsafeUtility.MemClear(NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(Durabilities), (long)Durabilities.Length * UnsafeUtility.SizeOf<float>());
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct ShinobuLedgerTransactionJob : IJob
    {
        [NoAlias, NativeDisableParallelForRestriction] public NativeArray<uint> Hashes;
        [NoAlias, NativeDisableParallelForRestriction] public NativeArray<int> Quantities;
        [NoAlias, NativeDisableParallelForRestriction] public NativeArray<float> Durabilities;
        [WriteOnly, NoAlias] public NativeArray<int> Result;
        public uint ItemHash;
        public int DeltaQuantity;
        public float DefaultDurability01;

        public void Execute()
        {
            bool success = Shinobu19EconomyLedger.TryTransactItem(
                Hashes,
                Quantities,
                Durabilities,
                ItemHash,
                DeltaQuantity,
                DefaultDurability01,
                out int slotIndex);

            if (Result.IsCreated && Result.Length >= 2)
            {
                Result[0] = success ? (int)ShinobuTransactionStatus.Success : (int)ShinobuTransactionStatus.AtomicConflict;
                Result[1] = slotIndex;
            }
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct ShinobuMockConsumeSignalJob : IJob
    {
        [NoAlias, NativeDisableParallelForRestriction] public NativeArray<uint> Hashes;
        [NoAlias, NativeDisableParallelForRestriction] public NativeArray<int> Quantities;
        [NoAlias, NativeDisableParallelForRestriction] public NativeArray<float> Durabilities;
        [WriteOnly, NoAlias, NativeDisableParallelForRestriction] public NativeArray<MockConsumeSignal> Signals;
        public uint Seed;
        public uint ActorHash;
        public uint FrameIndex;

        public void Execute()
        {
            if (!Hashes.IsCreated || !Quantities.IsCreated || !Durabilities.IsCreated || !Signals.IsCreated || Signals.Length <= 0)
                return;

            int capacity = math.min(Hashes.Length, Quantities.Length);
            if (capacity <= 0)
                return;

            uint rng = Seed != 0u ? Seed : 0x9E3779B9u;
            rng ^= FrameIndex * 747796405u;
            int start = (int)(rng % (uint)capacity);
            for (int scan = 0; scan < capacity; scan++)
            {
                int index = (start + scan) % capacity;
                uint hash = Hashes[index];
                if (hash == 0u || Quantities[index] <= 0)
                    continue;

                bool consumed = Shinobu19EconomyLedger.TryTransactItem(Hashes, Quantities, Durabilities, hash, -1, 1f, out _);
                Signals[0] = new MockConsumeSignal
                {
                    Sequence = ((ulong)FrameIndex << 32) | (uint)index,
                    ItemHash = hash,
                    ActorHash = ActorHash,
                    FrameIndex = FrameIndex,
                    Quantity = consumed ? 1 : 0,
                    Flags = consumed ? 1u : 0u
                };
                return;
            }
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct ShinobuCraftTransactionJob : IJob
    {
        [NoAlias, NativeDisableParallelForRestriction] public NativeArray<uint> Hashes;
        [NoAlias, NativeDisableParallelForRestriction] public NativeArray<int> Quantities;
        [NoAlias, NativeDisableParallelForRestriction] public NativeArray<float> Durabilities;
        [ReadOnly, NoAlias] public NativeArray<CraftingRecipeDTO> Recipes;
        [ReadOnly, NoAlias] public NativeArray<CraftingRecipeMaskDTO> RecipeMasks;
        [ReadOnly, NoAlias] public NativeArray<CraftingIngredientDTO> RecipeIngredients;
        [WriteOnly, NoAlias] public NativeArray<int> Result;
        public int RecipeIndex;
        public ulong InventoryMask;

        public void Execute()
        {
            ShinobuTransactionStatus status = ShinobuTransactionStatus.InvalidInput;
            int resultSlot = -1;
            if (Recipes.IsCreated && (uint)RecipeIndex < (uint)Recipes.Length)
            {
                ulong mask = 0UL;
                if (RecipeMasks.IsCreated && (uint)RecipeIndex < (uint)RecipeMasks.Length)
                    mask = RecipeMasks[RecipeIndex].RequirementMask;

                CraftingRecipeDTO recipe = Recipes[RecipeIndex];
                status = RecipeIngredients.IsCreated
                    ? Shinobu19EconomyLedger.TryCraftAtomicRollback(
                        Hashes,
                        Quantities,
                        Durabilities,
                        in recipe,
                        RecipeIngredients,
                        InventoryMask,
                        mask,
                        out resultSlot)
                    : Shinobu19EconomyLedger.TryCraftAtomicRollback(
                        Hashes,
                        Quantities,
                        Durabilities,
                        in recipe,
                        InventoryMask,
                        mask,
                        out resultSlot);
            }

            if (Result.IsCreated && Result.Length >= 2)
            {
                Result[0] = (int)status;
                Result[1] = resultSlot;
            }
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct ShinobuCraftingDagClosureJob : IJobParallelFor
    {
        [ReadOnly, NoAlias] public NativeArray<CraftingRecipeDTO> Recipes;
        [ReadOnly, NoAlias] public NativeArray<CraftingRecipeMaskDTO> RecipeMasks;
        [ReadOnly, NoAlias] public NativeArray<byte> DirectCraftable;
        [NoAlias, NativeDisableParallelForRestriction] public NativeArray<byte> DagCraftable;
        public int IterationCount;

        public void Execute(int index)
        {
            if (!Recipes.IsCreated ||
                !DagCraftable.IsCreated ||
                (uint)index >= (uint)Recipes.Length ||
                (uint)index >= (uint)DagCraftable.Length)
            {
                return;
            }

            byte craftable = DirectCraftable.IsCreated && (uint)index < (uint)DirectCraftable.Length
                ? DirectCraftable[index]
                : (byte)0;
            if (craftable != 0)
            {
                DagCraftable[index] = 1;
                return;
            }

            CraftingRecipeDTO recipe = Recipes[index];
            ulong requirement = RecipeMasks.IsCreated && (uint)index < (uint)RecipeMasks.Length
                ? RecipeMasks[index].RequirementMask
                : Shinobu19EconomyLedger.ComputeRequirementMask(in recipe);

            int scanLimit = math.min(math.max(1, IterationCount), Recipes.Length);
            ulong producibleMask = 0UL;
            for (int scan = 0; scan < scanLimit; scan++)
            {
                if ((uint)scan >= (uint)DagCraftable.Length || DagCraftable[scan] == 0)
                    continue;

                uint resultHash = Recipes[scan].ResultHash;
                producibleMask |= InventoryMaterialMask.ResolveBit(resultHash);
            }

            DagCraftable[index] = requirement != 0UL && (producibleMask & requirement) == requirement ? (byte)1 : (byte)0;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct ShinobuRecipeFastFailJob : IJobParallelFor
    {
        [ReadOnly, NoAlias] public NativeArray<CraftingRecipeDTO> Recipes;
        [ReadOnly, NoAlias] public NativeArray<CraftingRecipeMaskDTO> RecipeMasks;
        [ReadOnly, NoAlias] public NativeArray<CraftingIngredientDTO> RecipeIngredients;
        [ReadOnly, NoAlias] public NativeArray<uint> Hashes;
        [ReadOnly, NoAlias] public NativeArray<int> Quantities;
        [WriteOnly, NoAlias, NativeDisableParallelForRestriction] public NativeArray<byte> Craftable;
        public ulong InventoryMask;

        public void Execute(int index)
        {
            if (!Recipes.IsCreated ||
                !Craftable.IsCreated ||
                (uint)index >= (uint)Recipes.Length ||
                (uint)index >= (uint)Craftable.Length)
            {
                return;
            }

            CraftingRecipeDTO recipe = Recipes[index];
            ulong mask = RecipeMasks.IsCreated && (uint)index < (uint)RecipeMasks.Length
                ? RecipeMasks[index].RequirementMask
                : Shinobu19EconomyLedger.ComputeRequirementMask(in recipe);

            if (mask != 0UL && (InventoryMask & mask) != mask)
            {
                Craftable[index] = 0;
                return;
            }

            bool enough = true;
            int ingredientCursor = unchecked((int)recipe.Reserved1);
            int ingredientCount = unchecked((int)recipe.Reserved2);
            if (RecipeIngredients.IsCreated && ingredientCount > 0)
            {
                enough = Shinobu19EconomyLedger.HasRequiredIngredientQuantities(
                    Hashes,
                    Quantities,
                    RecipeIngredients,
                    ingredientCursor,
                    ingredientCount);
            }
            else
            {
                if (recipe.ComponentA != 0u && recipe.QuantityA > 0)
                    enough &= Shinobu19EconomyLedger.CountQuantity(Hashes, Quantities, recipe.ComponentA) >= recipe.QuantityA;
                if (recipe.ComponentB != 0u && recipe.QuantityB > 0)
                    enough &= Shinobu19EconomyLedger.CountQuantity(Hashes, Quantities, recipe.ComponentB) >= recipe.QuantityB;
            }

            Craftable[index] = enough ? (byte)1 : (byte)0;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct ShinobuEconomyTelemetryJob : IJob
    {
        [WriteOnly, NoAlias, NativeDisableParallelForRestriction] public NativeArray<EconomyTelemetryEntry> Telemetry;
        public EconomyTelemetryEntry Entry;
        public int Cursor;
        public float SpikeThresholdMs;

        public void Execute()
        {
            EconomyTelemetryEntry entry = Entry;
            if (entry.InventoryTransactionTimeMs > math.max(0.0001f, SpikeThresholdMs))
                entry.Flags |= Shinobu19EconomyLedger.TelemetryFlagSpike;

            Shinobu19EconomyLedger.RecordTelemetry(Telemetry, Cursor, in entry);
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct ShinobuDurabilityDegradationJob : IJobParallelFor
    {
        [NoAlias, NativeDisableParallelForRestriction] public NativeArray<uint> Hashes;
        [NoAlias, NativeDisableParallelForRestriction] public NativeArray<int> Quantities;
        [NoAlias, NativeDisableParallelForRestriction] public NativeArray<float> Durabilities;
        [WriteOnly, NoAlias, NativeDisableParallelForRestriction] public NativeArray<ToolBrokenSignal> BrokenSignals;
        public float WearDelta01;
        public uint FrameIndex;

        public void Execute(int index)
        {
            if ((uint)index >= (uint)Hashes.Length ||
                (uint)index >= (uint)Quantities.Length ||
                (uint)index >= (uint)Durabilities.Length ||
                Quantities[index] <= 0 ||
                Hashes[index] == 0u)
            {
                return;
            }

            float next = math.saturate(Durabilities[index] - math.max(0f, WearDelta01));
            Durabilities[index] = next;
            if (next > 0f)
                return;

            uint brokenHash = Hashes[index];
            Quantities[index] = 0;
            Hashes[index] = 0u;

            if (BrokenSignals.IsCreated && (uint)index < (uint)BrokenSignals.Length)
            {
                BrokenSignals[index] = new ToolBrokenSignal
                {
                    Sequence = ((ulong)FrameIndex << 32) | (uint)index,
                    ToolHash = brokenHash,
                    SlotIndex = index,
                    FrameIndex = FrameIndex,
                    Flags = 1u
                };
            }
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct ShinobuContainerTransferJob : IJob
    {
        [NoAlias, NativeDisableParallelForRestriction] public NativeArray<uint> SourceHashes;
        [NoAlias, NativeDisableParallelForRestriction] public NativeArray<int> SourceQuantities;
        [NoAlias, NativeDisableParallelForRestriction] public NativeArray<float> SourceDurabilities;
        [NoAlias, NativeDisableParallelForRestriction] public NativeArray<uint> TargetHashes;
        [NoAlias, NativeDisableParallelForRestriction] public NativeArray<int> TargetQuantities;
        [NoAlias, NativeDisableParallelForRestriction] public NativeArray<float> TargetDurabilities;
        [WriteOnly, NoAlias] public NativeArray<int> Result;
        public int SourceStartIndex;
        public int SlotCount;

        public void Execute()
        {
            int moved = 0;
            ShinobuTransactionStatus status = ShinobuTransactionStatus.Success;
            int sourceCapacity = math.min(SourceHashes.Length, math.min(SourceQuantities.Length, SourceDurabilities.Length));
            int end = math.min(sourceCapacity, SourceStartIndex + math.max(0, SlotCount));
            for (int index = math.max(0, SourceStartIndex); index < end; index++)
            {
                uint hash = SourceHashes[index];
                int quantity = SourceQuantities[index];
                if (hash == 0u || quantity <= 0)
                    continue;

                if (!Shinobu19EconomyLedger.TryTransactItem(TargetHashes, TargetQuantities, TargetDurabilities, hash, quantity, SourceDurabilities[index], out _))
                {
                    status = ShinobuTransactionStatus.OutputFull;
                    break;
                }

                if (!Shinobu19EconomyLedger.TryTransactItem(SourceHashes, SourceQuantities, SourceDurabilities, hash, -quantity, SourceDurabilities[index], out _))
                {
                    Shinobu19EconomyLedger.TryTransactItem(TargetHashes, TargetQuantities, TargetDurabilities, hash, -quantity, SourceDurabilities[index], out _);
                    status = ShinobuTransactionStatus.AtomicConflict;
                    break;
                }

                moved += quantity;
            }

            if (Result.IsCreated && Result.Length >= 2)
            {
                Result[0] = (int)status;
                Result[1] = moved;
            }
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct ShinobuEncumbranceResolveJob : IJob
    {
        [ReadOnly, NoAlias] public NativeArray<uint> Hashes;
        [ReadOnly, NoAlias] public NativeArray<int> Quantities;
        [ReadOnly, NoAlias] public NativeArray<ItemPhysicalConstantsDTO> PhysicalConstants;
        [WriteOnly, NoAlias] public NativeArray<EncumbranceSignal> Result;
        [WriteOnly, NoAlias, NativeDisableParallelForRestriction] public NativeArray<ShinobuCarryTotalsDTO> CarryTotals;
        public float MaxMassKg;
        public float MaxVolumeLiters;
        public uint FrameIndex;

        public void Execute()
        {
            float massKg = 0f;
            float volumeLiters = 0f;
            int capacity = math.min(Hashes.Length, Quantities.Length);
            for (int slot = 0; slot < capacity; slot++)
            {
                uint hash = Hashes[slot];
                int quantity = Quantities[slot];
                if (hash == 0u || quantity <= 0)
                    continue;

                if (!TryFindPhysical(hash, PhysicalConstants, out ItemPhysicalConstantsDTO constants))
                    continue;

                massKg += math.max(0f, constants.MassKg) * quantity;
                volumeLiters += math.max(0f, constants.VolumeLiters) * quantity;
            }

            massKg = math.isfinite(massKg) ? math.max(0f, massKg) : 0f;
            volumeLiters = math.isfinite(volumeLiters) ? math.max(0f, volumeLiters) : 0f;
            float maxMassKg = math.max(0.0001f, math.isfinite(MaxMassKg) ? MaxMassKg : 0f);
            float maxVolumeLiters = math.max(0.0001f, math.isfinite(MaxVolumeLiters) ? MaxVolumeLiters : 0f);
            float massLoad = massKg * math.rcp(maxMassKg);
            float volumeLoad = volumeLiters * math.rcp(maxVolumeLiters);
            float load01 = math.saturate(math.max(massLoad, volumeLoad));
            float movementMultiplier = math.lerp(1f, 0.5f, load01);
            if (CarryTotals.IsCreated && CarryTotals.Length > 0)
            {
                CarryTotals[0] = new ShinobuCarryTotalsDTO
                {
                    TimestampTicks = 0L,
                    TotalMassKg = massKg,
                    TotalVolumeLiters = volumeLiters,
                    MaxCarryMassKg = maxMassKg,
                    MaxCarryVolumeLiters = maxVolumeLiters,
                    Load01 = load01,
                    MovementMultiplier = movementMultiplier,
                    FrameIndex = FrameIndex,
                    Reserved0 = 0u
                };
            }

            if (Result.IsCreated && Result.Length > 0)
            {
                Result[0] = new EncumbranceSignal
                {
                    Load01 = load01,
                    MassKg = massKg,
                    VolumeLiters = volumeLiters,
                    MovementMultiplier = movementMultiplier,
                    FrameIndex = FrameIndex
                };
            }
        }

        private static bool TryFindPhysical(uint hash, NativeArray<ItemPhysicalConstantsDTO> physicalConstants, out ItemPhysicalConstantsDTO result)
        {
            result = default;
            if (!physicalConstants.IsCreated)
                return false;

            for (int index = 0; index < physicalConstants.Length; index++)
            {
                ItemPhysicalConstantsDTO candidate = physicalConstants[index];
                if (candidate.ItemHash != hash)
                    continue;

                result = candidate;
                return true;
            }

            return false;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct ShinobuLootMagnetSpatialQueryJob : IJob
    {
        [ReadOnly, NoAlias] public NativeParallelMultiHashMap<int, DebrisSpatialEntry> SpatialHash;
        [NoAlias, NativeDisableParallelForRestriction] public NativeArray<uint> Hashes;
        [NoAlias, NativeDisableParallelForRestriction] public NativeArray<int> Quantities;
        [NoAlias, NativeDisableParallelForRestriction] public NativeArray<float> Durabilities;
        [WriteOnly, NoAlias, NativeDisableParallelForRestriction] public NativeArray<DebrisDestroyedSignal> DestroyedSignals;
        [WriteOnly, NoAlias] public NativeArray<int> DestroyedCount;
        public double3 PlayerAup;
        public double3 SectorOriginAup;
        public float RadiusMeters;
        public float CellSizeMeters;
        public uint FrameIndex;

        public void Execute()
        {
            if (!SpatialHash.IsCreated ||
                !Hashes.IsCreated ||
                !Quantities.IsCreated ||
                !Durabilities.IsCreated ||
                !DestroyedSignals.IsCreated ||
                RadiusMeters <= 0f ||
                CellSizeMeters <= 0f ||
                !math.isfinite(RadiusMeters) ||
                !math.isfinite(CellSizeMeters))
            {
                return;
            }

            float3 localPlayer = AupPrecisionMath.LocalDeltaFloat3(PlayerAup, SectorOriginAup, float3.zero);
            if (!math.all(math.isfinite(localPlayer)))
                return;

            float radiusSq = RadiusMeters * RadiusMeters;
            float inverseCell = math.rcp(math.max(0.0001f, CellSizeMeters));
            int3 center = (int3)math.floor(localPlayer * inverseCell);
            int write = 0;

            for (int z = -1; z <= 1; z++)
            {
                for (int y = -1; y <= 1; y++)
                {
                    for (int x = -1; x <= 1; x++)
                    {
                        int cellKey = HashCell(center + new int3(x, y, z));
                        if (!SpatialHash.TryGetFirstValue(cellKey, out DebrisSpatialEntry entry, out NativeParallelMultiHashMapIterator<int> iterator))
                            continue;

                        do
                        {
                            float distanceSq = math.lengthsq(entry.LocalPosition - localPlayer);
                            if (distanceSq > radiusSq ||
                                !Shinobu19EconomyLedger.TryTransactItem(Hashes, Quantities, Durabilities, entry.LootHash, math.max(0, entry.Quantity), 1f, out _))
                            {
                                continue;
                            }

                            if (write < DestroyedSignals.Length)
                            {
                                DestroyedSignals[write] = new DebrisDestroyedSignal
                                {
                                    Sequence = ((ulong)FrameIndex << 32) | (uint)write,
                                    LootHash = entry.LootHash,
                                    FrameIndex = FrameIndex,
                                    Quantity = entry.Quantity,
                                    DebrisIndex = entry.DebrisIndex,
                                    Flags = 1u
                                };
                                write++;
                            }
                        }
                        while (SpatialHash.TryGetNextValue(out entry, ref iterator));
                    }
                }
            }

            if (DestroyedCount.IsCreated && DestroyedCount.Length > 0)
                DestroyedCount[0] = write;
        }

        private static int HashCell(int3 cell)
        {
            uint hash = 2166136261u;
            hash = (hash ^ unchecked((uint)cell.x)) * 16777619u;
            hash = (hash ^ unchecked((uint)cell.y)) * 16777619u;
            hash = (hash ^ unchecked((uint)cell.z)) * 16777619u;
            return unchecked((int)hash);
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct ShinobuHotbarRouteJob : IJobParallelFor
    {
        [ReadOnly, NoAlias] public NativeArray<int> HotbarSlotToInventorySlot;
        [ReadOnly, NoAlias] public NativeArray<uint> Hashes;
        [WriteOnly, NoAlias, NativeDisableParallelForRestriction] public NativeArray<EquipItemSignal> EquipSignals;
        public uint FrameIndex;

        public void Execute(int hotbarSlot)
        {
            if ((uint)hotbarSlot >= (uint)HotbarSlotToInventorySlot.Length ||
                (uint)hotbarSlot >= (uint)EquipSignals.Length)
            {
                return;
            }

            int inventorySlot = HotbarSlotToInventorySlot[hotbarSlot];
            uint hash = (uint)inventorySlot < (uint)Hashes.Length ? Hashes[inventorySlot] : 0u;
            EquipSignals[hotbarSlot] = new EquipItemSignal
            {
                Sequence = ((ulong)FrameIndex << 32) | (uint)hotbarSlot,
                ItemHash = hash,
                InventorySlot = inventorySlot,
                HotbarSlot = hotbarSlot,
                FrameIndex = FrameIndex
            };
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct ShinobuLootMagnetInsertJob : IJobParallelFor
    {
        [ReadOnly, NoAlias] public NativeArray<DebrisDestroyedSignal> DebrisSignals;
        [NoAlias, NativeDisableParallelForRestriction] public NativeArray<uint> Hashes;
        [NoAlias, NativeDisableParallelForRestriction] public NativeArray<int> Quantities;
        [NoAlias, NativeDisableParallelForRestriction] public NativeArray<float> Durabilities;
        [WriteOnly, NoAlias, NativeDisableParallelForRestriction] public NativeArray<byte> Accepted;

        public void Execute(int index)
        {
            if ((uint)index >= (uint)DebrisSignals.Length ||
                (Accepted.IsCreated && (uint)index >= (uint)Accepted.Length))
            {
                return;
            }

            DebrisDestroyedSignal signal = DebrisSignals[index];
            bool accepted = Shinobu19EconomyLedger.TryTransactItem(
                Hashes,
                Quantities,
                Durabilities,
                signal.LootHash,
                math.max(0, signal.Quantity),
                1f,
                out _);

            if (Accepted.IsCreated)
                Accepted[index] = accepted ? (byte)1 : (byte)0;
        }
    }
}
