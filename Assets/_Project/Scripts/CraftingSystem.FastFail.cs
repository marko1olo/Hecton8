using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using Hecton8.Core;
using Hecton8.Core.Memory;
using Hecton8.Inventory;
using Unity.Burst;
using Unity.Burst.CompilerServices;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Crafting
{
    public enum CraftingFastFailStatus : byte
    {
        Success = 0,
        InvalidInput = 1,
        MaskMissing = 2,
        UnlockMissing = 3,
        MissingQuantity = 4,
        AtomicConflict = 5,
        OutputNotHandled = 6
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct RecipeRequirementDTO
    {
        [FieldOffset(0)] public ulong BlueprintUnlockMask;
        [FieldOffset(8)] public uint ResultItemHash;
        [FieldOffset(12)] public uint IngredientHashA;
        [FieldOffset(16)] public uint IngredientHashB;
        [FieldOffset(20)] public uint IngredientHashC;
        [FieldOffset(24)] public uint IngredientHashD;
        [FieldOffset(28)] public uint QuantitiesPacked;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct CraftingFastFailTelemetryEntry
    {
        [FieldOffset(0)] public ulong RequirementMask;
        [FieldOffset(8)] public ulong UnlockMask;
        [FieldOffset(16)] public uint Frame;
        [FieldOffset(20)] public uint RecipeWordIndex;
        [FieldOffset(24)] public uint RecipesEvaluated;
        [FieldOffset(28)] public uint UnlockCullCount;
        [FieldOffset(32)] public uint MaskCullCount;
        [FieldOffset(36)] public uint SimdSuccessCount;
        [FieldOffset(40)] public uint InventoryVersion;
        [FieldOffset(44)] public uint UiPublicationBudget;
        [FieldOffset(48)] public uint StateHash;
        [FieldOffset(52)] public uint Flags;
        [FieldOffset(56)] public float ScheduleMicroseconds;
        [FieldOffset(60)] public float GlobalQualityWeight;
    }

    public static class CraftingFastFailValidator
    {
        public const int MaxIngredientsPerRecipe = 4;
        public const int RecipeRequirementDtoSizeBytes = 32;
        public const int TelemetryEntrySizeBytes = 64;
        public const int RecipeRequirementBlueprintUnlockMaskOffset = 0;
        public const int RecipeRequirementResultItemHashOffset = 8;
        public const int RecipeRequirementIngredientHashAOffset = 12;
        public const int RecipeRequirementIngredientHashBOffset = 16;
        public const int RecipeRequirementIngredientHashCOffset = 20;
        public const int RecipeRequirementIngredientHashDOffset = 24;
        public const int RecipeRequirementQuantitiesPackedOffset = 28;
        public const int TelemetryRequirementMaskOffset = 0;
        public const int TelemetryUnlockMaskOffset = 8;
        public const int TelemetryFrameOffset = 16;
        public const int TelemetryRecipeWordIndexOffset = 20;
        public const int TelemetryRecipesEvaluatedOffset = 24;
        public const int TelemetryUnlockCullCountOffset = 28;
        public const int TelemetryMaskCullCountOffset = 32;
        public const int TelemetrySimdSuccessCountOffset = 36;
        public const int TelemetryInventoryVersionOffset = 40;
        public const int TelemetryUiPublicationBudgetOffset = 44;
        public const int TelemetryStateHashOffset = 48;
        public const int TelemetryFlagsOffset = 52;
        public const int TelemetryScheduleMicrosecondsOffset = 56;
        public const int TelemetryGlobalQualityWeightOffset = 60;
        public const int TelemetryCapacity = 300;
        public const int RecipesPerWord = 64;
        public const float SlowFrameDumpThresholdMicroseconds = 200f;
        public const string DefaultDumpPath = "Docs/AgentLogs/Dump_SHINOBU_317.bin";
        public const ulong AlwaysUnlockedMask = 1UL;

        private const uint UIntSignBit = 0x80000000u;
        internal const int SlotCasRetryLimit = 8;

        public static bool RuntimeLayoutValid()
        {
            return UnsafeUtility.SizeOf<RecipeRequirementDTO>() == RecipeRequirementDtoSizeBytes &&
                   UnsafeUtility.SizeOf<CraftingFastFailTelemetryEntry>() == TelemetryEntrySizeBytes &&
                   Marshal.OffsetOf<RecipeRequirementDTO>(nameof(RecipeRequirementDTO.BlueprintUnlockMask)).ToInt32() == RecipeRequirementBlueprintUnlockMaskOffset &&
                   Marshal.OffsetOf<RecipeRequirementDTO>(nameof(RecipeRequirementDTO.ResultItemHash)).ToInt32() == RecipeRequirementResultItemHashOffset &&
                   Marshal.OffsetOf<RecipeRequirementDTO>(nameof(RecipeRequirementDTO.IngredientHashA)).ToInt32() == RecipeRequirementIngredientHashAOffset &&
                   Marshal.OffsetOf<RecipeRequirementDTO>(nameof(RecipeRequirementDTO.IngredientHashB)).ToInt32() == RecipeRequirementIngredientHashBOffset &&
                   Marshal.OffsetOf<RecipeRequirementDTO>(nameof(RecipeRequirementDTO.IngredientHashC)).ToInt32() == RecipeRequirementIngredientHashCOffset &&
                   Marshal.OffsetOf<RecipeRequirementDTO>(nameof(RecipeRequirementDTO.IngredientHashD)).ToInt32() == RecipeRequirementIngredientHashDOffset &&
                   Marshal.OffsetOf<RecipeRequirementDTO>(nameof(RecipeRequirementDTO.QuantitiesPacked)).ToInt32() == RecipeRequirementQuantitiesPackedOffset &&
                   Marshal.OffsetOf<CraftingFastFailTelemetryEntry>(nameof(CraftingFastFailTelemetryEntry.RequirementMask)).ToInt32() == TelemetryRequirementMaskOffset &&
                   Marshal.OffsetOf<CraftingFastFailTelemetryEntry>(nameof(CraftingFastFailTelemetryEntry.UnlockMask)).ToInt32() == TelemetryUnlockMaskOffset &&
                   Marshal.OffsetOf<CraftingFastFailTelemetryEntry>(nameof(CraftingFastFailTelemetryEntry.Frame)).ToInt32() == TelemetryFrameOffset &&
                   Marshal.OffsetOf<CraftingFastFailTelemetryEntry>(nameof(CraftingFastFailTelemetryEntry.RecipeWordIndex)).ToInt32() == TelemetryRecipeWordIndexOffset &&
                   Marshal.OffsetOf<CraftingFastFailTelemetryEntry>(nameof(CraftingFastFailTelemetryEntry.RecipesEvaluated)).ToInt32() == TelemetryRecipesEvaluatedOffset &&
                   Marshal.OffsetOf<CraftingFastFailTelemetryEntry>(nameof(CraftingFastFailTelemetryEntry.UnlockCullCount)).ToInt32() == TelemetryUnlockCullCountOffset &&
                   Marshal.OffsetOf<CraftingFastFailTelemetryEntry>(nameof(CraftingFastFailTelemetryEntry.MaskCullCount)).ToInt32() == TelemetryMaskCullCountOffset &&
                   Marshal.OffsetOf<CraftingFastFailTelemetryEntry>(nameof(CraftingFastFailTelemetryEntry.SimdSuccessCount)).ToInt32() == TelemetrySimdSuccessCountOffset &&
                   Marshal.OffsetOf<CraftingFastFailTelemetryEntry>(nameof(CraftingFastFailTelemetryEntry.InventoryVersion)).ToInt32() == TelemetryInventoryVersionOffset &&
                   Marshal.OffsetOf<CraftingFastFailTelemetryEntry>(nameof(CraftingFastFailTelemetryEntry.UiPublicationBudget)).ToInt32() == TelemetryUiPublicationBudgetOffset &&
                   Marshal.OffsetOf<CraftingFastFailTelemetryEntry>(nameof(CraftingFastFailTelemetryEntry.StateHash)).ToInt32() == TelemetryStateHashOffset &&
                   Marshal.OffsetOf<CraftingFastFailTelemetryEntry>(nameof(CraftingFastFailTelemetryEntry.Flags)).ToInt32() == TelemetryFlagsOffset &&
                   Marshal.OffsetOf<CraftingFastFailTelemetryEntry>(nameof(CraftingFastFailTelemetryEntry.ScheduleMicroseconds)).ToInt32() == TelemetryScheduleMicrosecondsOffset &&
                   Marshal.OffsetOf<CraftingFastFailTelemetryEntry>(nameof(CraftingFastFailTelemetryEntry.GlobalQualityWeight)).ToInt32() == TelemetryGlobalQualityWeightOffset;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint PackQuantities(uint quantityA, uint quantityB, uint quantityC, uint quantityD)
        {
            return (math.min(quantityA, 255u) & 0xFFu) |
                   ((math.min(quantityB, 255u) & 0xFFu) << 8) |
                   ((math.min(quantityC, 255u) & 0xFFu) << 16) |
                   ((math.min(quantityD, 255u) & 0xFFu) << 24);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static RecipeRequirementDTO BuildRequirement(
            uint resultItemHash,
            uint ingredientHashA,
            uint ingredientHashB,
            uint ingredientHashC,
            uint ingredientHashD,
            uint quantityA,
            uint quantityB,
            uint quantityC,
            uint quantityD,
            ulong blueprintUnlockMask)
        {
            return new RecipeRequirementDTO
            {
                ResultItemHash = resultItemHash,
                IngredientHashA = ingredientHashA,
                IngredientHashB = ingredientHashB,
                IngredientHashC = ingredientHashC,
                IngredientHashD = ingredientHashD,
                QuantitiesPacked = PackQuantities(quantityA, quantityB, quantityC, quantityD),
                BlueprintUnlockMask = blueprintUnlockMask == 0UL ? AlwaysUnlockedMask : blueprintUnlockMask
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ulong NormalizePlayerUnlockMask(ulong playerUnlockMask)
        {
            return playerUnlockMask | AlwaysUnlockedMask;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ulong ResolveBlueprintUnlockMask(uint requiredScanHash)
        {
            return requiredScanHash == 0u ? AlwaysUnlockedMask : InventoryMaterialMask.ResolveBit(requiredScanHash);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ResolveWordCount(int recipeCount)
        {
            return recipeCount <= 0 ? 0 : (recipeCount + RecipesPerWord - 1) >> 6;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryReadCraftableBit(
            NativeArray<ulong> craftableWords,
            int recipeIndex,
            int publishedRecipeBudget,
            out bool craftable)
        {
            craftable = false;
            if (!craftableWords.IsCreated ||
                recipeIndex < 0 ||
                recipeIndex >= publishedRecipeBudget)
            {
                return false;
            }

            int wordIndex = recipeIndex >> 6;
            if ((uint)wordIndex >= (uint)craftableWords.Length)
                return false;

            craftable = (craftableWords[wordIndex] & (1UL << (recipeIndex & 63))) != 0UL;
            return true;
        }

        public static int ResolveUiPublicationBudget(int pendingRecipeCount, float globalQualityWeight)
        {
            if (pendingRecipeCount <= 0)
                return 0;

            float weight = math.saturate(math.isfinite(globalQualityWeight) ? globalQualityWeight : 0f);
            float curved = weight * weight * (3f - (2f * weight));
            int minimum = math.min(pendingRecipeCount, 64);
            int maximum = pendingRecipeCount;
            return math.clamp((int)math.ceil(math.lerp(minimum, maximum, curved)), minimum, maximum);
        }

        public static JobHandle ScheduleAvailability(
            NativeArray<RecipeRequirementDTO> recipes,
            NativeArray<uint> inventoryHashes,
            NativeArray<uint> inventoryQuantities,
            NativeArray<ulong> craftableWords,
            NativeArray<CraftingFastFailTelemetryEntry> telemetry,
            NativeArray<int> telemetryCursor,
            int recipeCount,
            int inventoryCount,
            ulong currentInventoryMask,
            ulong playerUnlockMask,
            uint frame,
            uint inventoryVersion,
            float globalQualityWeight,
            float scheduleMicroseconds,
            JobHandle dependency)
        {
            if (!recipes.IsCreated ||
                !inventoryHashes.IsCreated ||
                !inventoryQuantities.IsCreated ||
                !craftableWords.IsCreated ||
                recipeCount <= 0 ||
                craftableWords.Length <= 0)
            {
                return dependency;
            }

            int uiPublicationBudget = ResolveUiPublicationBudget(recipeCount, globalQualityWeight);
            int scheduledWordCount = math.min(craftableWords.Length, ResolveWordCount(uiPublicationBudget));
            if (scheduledWordCount <= 0)
                return dependency;

            EvaluateCraftingAvailabilityJob job = new EvaluateCraftingAvailabilityJob
            {
                Recipes = recipes,
                InventoryHashes = inventoryHashes,
                InventoryQuantities = inventoryQuantities,
                CraftableWords = craftableWords,
                Telemetry = telemetry,
                TelemetryCursor = telemetryCursor,
                RecipeCount = recipeCount,
                UiPublicationRecipeBudget = uiPublicationBudget,
                InventoryCount = inventoryCount,
                CurrentInventoryMask = currentInventoryMask,
                PlayerUnlockMask = playerUnlockMask,
                Frame = frame,
                InventoryVersion = inventoryVersion,
                GlobalQualityWeight = globalQualityWeight,
                ScheduleMicroseconds = scheduleMicroseconds
            };
            JobHandle handle = job.Schedule(scheduledWordCount, 1, dependency);
            H8Memory.RegisterActiveJob(SystemID.Crafting, handle);
            return handle;
        }

        public static bool AcquireFastFailVaultBuffersCold(
            IDataVault vault,
            int recipeCapacity,
            out NativeArray<RecipeRequirementDTO> requirements,
            out NativeArray<ulong> craftableWords,
            out NativeArray<CraftingFastFailTelemetryEntry> telemetry,
            out NativeArray<int> telemetryCursor,
            out NativeArray<int> transactionResults)
        {
            requirements = default;
            craftableWords = default;
            telemetry = default;
            telemetryCursor = default;
            transactionResults = default;

            int wordCapacity = ResolveWordCount(recipeCapacity);
            if (vault == null || recipeCapacity <= 0 || wordCapacity <= 0)
                return false;

            return OpenOrAcquireFastFailBuffer(BufferID.ShinobuFastFailRequirementDtos, recipeCapacity, NativeArrayOptions.UninitializedMemory, vault, out requirements) &&
                   OpenOrAcquireFastFailBuffer(BufferID.ShinobuFastFailCraftableWords, wordCapacity, NativeArrayOptions.UninitializedMemory, vault, out craftableWords) &&
                   OpenOrAcquireFastFailBuffer(BufferID.ShinobuFastFailTelemetryRing, TelemetryCapacity, NativeArrayOptions.ClearMemory, vault, out telemetry) &&
                   OpenOrAcquireFastFailBuffer(BufferID.ShinobuFastFailTelemetryCursor, 1, NativeArrayOptions.ClearMemory, vault, out telemetryCursor) &&
                   OpenOrAcquireFastFailBuffer(BufferID.ShinobuFastFailTransactionResults, 1, NativeArrayOptions.ClearMemory, vault, out transactionResults);
        }

        public static bool TryReadFastFailVaultBuffers(
            IDataVault vault,
            out NativeArray<RecipeRequirementDTO>.ReadOnly requirements,
            out NativeArray<ulong>.ReadOnly craftableWords,
            out NativeArray<CraftingFastFailTelemetryEntry>.ReadOnly telemetry,
            out NativeArray<int>.ReadOnly telemetryCursor,
            out NativeArray<int>.ReadOnly transactionResults)
        {
            requirements = default;
            craftableWords = default;
            telemetry = default;
            telemetryCursor = default;
            transactionResults = default;

            return TryReadFastFailBufferReadOnly<RecipeRequirementDTO>(vault, BufferID.ShinobuFastFailRequirementDtos, out requirements) &&
                   TryReadFastFailBufferReadOnly<ulong>(vault, BufferID.ShinobuFastFailCraftableWords, out craftableWords) &&
                   TryReadFastFailBufferReadOnly<CraftingFastFailTelemetryEntry>(vault, BufferID.ShinobuFastFailTelemetryRing, out telemetry) &&
                   TryReadFastFailBufferReadOnly<int>(vault, BufferID.ShinobuFastFailTelemetryCursor, out telemetryCursor) &&
                   TryReadFastFailBufferReadOnly<int>(vault, BufferID.ShinobuFastFailTransactionResults, out transactionResults);
        }

        [System.Obsolete("Use the NativeArray<T>.ReadOnly overload; legacy mutable wrapper retained for compatibility.", false)]
        public static bool TryReadFastFailVaultBuffers(
            IDataVault vault,
            out NativeArray<RecipeRequirementDTO> requirements,
            out NativeArray<ulong> craftableWords,
            out NativeArray<CraftingFastFailTelemetryEntry> telemetry,
            out NativeArray<int> telemetryCursor,
            out NativeArray<int> transactionResults)
        {
            requirements = default;
            craftableWords = default;
            telemetry = default;
            telemetryCursor = default;
            transactionResults = default;

            return TryReadFastFailBuffer(vault, BufferID.ShinobuFastFailRequirementDtos, out requirements) &&
                   TryReadFastFailBuffer(vault, BufferID.ShinobuFastFailCraftableWords, out craftableWords) &&
                   TryReadFastFailBuffer(vault, BufferID.ShinobuFastFailTelemetryRing, out telemetry) &&
                   TryReadFastFailBuffer(vault, BufferID.ShinobuFastFailTelemetryCursor, out telemetryCursor) &&
                   TryReadFastFailBuffer(vault, BufferID.ShinobuFastFailTransactionResults, out transactionResults);
        }

#if UNITY_EDITOR
        public static bool TryIngestRecipeCsvLine(ReadOnlySpan<char> line, out RecipeRequirementDTO requirement)
        {
            requirement = default;
            int cursor = 0;
            if (!TryReadUIntToken(line, ref cursor, out uint resultHash) ||
                !TryReadUIntToken(line, ref cursor, out uint hashA) ||
                !TryReadByteUIntToken(line, ref cursor, out uint quantityA) ||
                !TryReadUIntToken(line, ref cursor, out uint hashB) ||
                !TryReadByteUIntToken(line, ref cursor, out uint quantityB) ||
                !TryReadUIntToken(line, ref cursor, out uint hashC) ||
                !TryReadByteUIntToken(line, ref cursor, out uint quantityC) ||
                !TryReadUIntToken(line, ref cursor, out uint hashD) ||
                !TryReadByteUIntToken(line, ref cursor, out uint quantityD) ||
                !TryReadULongToken(line, ref cursor, out ulong unlockMask))
            {
                return false;
            }

            requirement = BuildRequirement(resultHash, hashA, hashB, hashC, hashD, quantityA, quantityB, quantityC, quantityD, unlockMask);
            return requirement.ResultItemHash != 0u;
        }

        public static bool TryIngestRecipeCsvLine(ReadOnlySpan<byte> line, out RecipeRequirementDTO requirement)
        {
            requirement = default;
            int cursor = 0;
            if (!TryReadHashToken(line, ref cursor, out uint resultHash) ||
                !TryReadHashToken(line, ref cursor, out uint hashA) ||
                !TryReadByteUIntToken(line, ref cursor, out uint quantityA) ||
                !TryReadHashToken(line, ref cursor, out uint hashB) ||
                !TryReadByteUIntToken(line, ref cursor, out uint quantityB) ||
                !TryReadHashToken(line, ref cursor, out uint hashC) ||
                !TryReadByteUIntToken(line, ref cursor, out uint quantityC) ||
                !TryReadHashToken(line, ref cursor, out uint hashD) ||
                !TryReadByteUIntToken(line, ref cursor, out uint quantityD) ||
                !TryReadByteUnlockToken(line, ref cursor, out ulong unlockMask))
            {
                return false;
            }

            requirement = BuildRequirement(resultHash, hashA, hashB, hashC, hashD, quantityA, quantityB, quantityC, quantityD, unlockMask);
            return requirement.ResultItemHash != 0u;
        }
#endif

        public static unsafe bool TryDumpTelemetryToFile(NativeArray<CraftingFastFailTelemetryEntry> telemetry, string path)
        {
            if (!telemetry.IsCreated || telemetry.Length <= 0 || string.IsNullOrWhiteSpace(path))
                return false;

            void* ptr = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(telemetry);
            int byteLength = telemetry.Length * UnsafeUtility.SizeOf<CraftingFastFailTelemetryEntry>();
            return NativeFaultDumpWriter.TryWriteAll(path, new ReadOnlySpan<byte>(ptr, byteLength), byteLength);
        }

        public static bool TryDumpSlowFrameTelemetry(
            NativeArray<CraftingFastFailTelemetryEntry> telemetry,
            float elapsedMicroseconds,
            string path)
        {
            if (!math.isfinite(elapsedMicroseconds) || elapsedMicroseconds <= SlowFrameDumpThresholdMicroseconds)
                return false;

            return TryDumpTelemetryToFile(telemetry, path);
        }

        public static bool TryDumpSlowFrameTelemetry(
            NativeArray<CraftingFastFailTelemetryEntry> telemetry,
            float elapsedMicroseconds)
        {
            return TryDumpSlowFrameTelemetry(telemetry, elapsedMicroseconds, DefaultDumpPath);
        }

        // Cold bake bridge only; SIM/VISUAL validation must consume RecipeRequirementDTO.
        public static bool TryBuildRequirementFromRecipeData(RecipeData recipe, int multiplier, out RecipeRequirementDTO requirement)
        {
            requirement = default;
            if (recipe == null || recipe.resultItem == null || recipe.ingredients == null)
                return false;

            uint resultHash = unchecked((uint)recipe.resultItem.PersistentHashId);
            if (resultHash == 0u)
                return false;

            uint hashA = 0u;
            uint hashB = 0u;
            uint hashC = 0u;
            uint hashD = 0u;
            uint quantityA = 0u;
            uint quantityB = 0u;
            uint quantityC = 0u;
            uint quantityD = 0u;
            int safeMultiplier = math.max(1, multiplier);
            int emitted = 0;

            for (int i = 0; i < recipe.ingredients.Count; i++)
            {
                Hecton8.Building.InventoryCost cost = recipe.ingredients[i];
                if (cost == null || cost.item == null || cost.amount <= 0)
                    continue;

                uint hash = unchecked((uint)cost.item.PersistentHashId);
                long scaledAmount = (long)cost.amount * safeMultiplier;
                if (scaledAmount > 255L)
                    return false;

                uint quantity = (uint)scaledAmount;
                if (hash == 0u || quantity == 0u)
                    continue;

                if (emitted >= MaxIngredientsPerRecipe)
                    return false;

                if (emitted == 0)
                {
                    hashA = hash;
                    quantityA = quantity;
                }
                else if (emitted == 1)
                {
                    hashB = hash;
                    quantityB = quantity;
                }
                else if (emitted == 2)
                {
                    hashC = hash;
                    quantityC = quantity;
                }
                else
                {
                    hashD = hash;
                    quantityD = quantity;
                }

                emitted++;
            }

            if (emitted == 0)
                return false;

            requirement = BuildRequirement(
                resultHash,
                hashA,
                hashB,
                hashC,
                hashD,
                quantityA,
                quantityB,
                quantityC,
                quantityD,
                ResolveBlueprintUnlockMask(recipe.RequiredScanEntryHash));
            return true;
        }

        public static bool TryEvaluateRecipeAvailability(
            in RecipeRequirementDTO recipe,
            NativeArray<uint> inventoryHashes,
            NativeArray<uint> inventoryQuantities,
            int inventoryCount,
            ulong currentInventoryMask,
            ulong playerUnlockMask,
            out CraftingFastFailStatus status,
            out uint simdLaneMask)
        {
            return TryEvaluateRecipeAvailability(
                in recipe,
                inventoryHashes.IsCreated ? inventoryHashes.AsReadOnly() : default,
                inventoryQuantities.IsCreated ? inventoryQuantities.AsReadOnly() : default,
                inventoryCount,
                currentInventoryMask,
                playerUnlockMask,
                out status,
                out simdLaneMask);
        }

        public static bool TryEvaluateRecipeAvailability(
            in RecipeRequirementDTO recipe,
            NativeArray<uint>.ReadOnly inventoryHashes,
            NativeArray<uint>.ReadOnly inventoryQuantities,
            int inventoryCount,
            ulong currentInventoryMask,
            ulong playerUnlockMask,
            out CraftingFastFailStatus status,
            out uint simdLaneMask)
        {
            simdLaneMask = 0u;
            status = CraftingFastFailStatus.InvalidInput;
            if (!inventoryHashes.IsCreated || !inventoryQuantities.IsCreated)
                return false;

            ulong requirementMask = BuildRequirementMask(in recipe);
            if (recipe.ResultItemHash == 0u || requirementMask == 0UL)
                return false;

            if ((NormalizePlayerUnlockMask(playerUnlockMask) & recipe.BlueprintUnlockMask) == 0UL)
            {
                status = CraftingFastFailStatus.UnlockMissing;
                return false;
            }

            if ((currentInventoryMask & requirementMask) != requirementMask)
            {
                status = CraftingFastFailStatus.MaskMissing;
                return false;
            }

            uint hashA = recipe.IngredientHashA;
            uint hashB = recipe.IngredientHashB;
            uint hashC = recipe.IngredientHashC;
            uint hashD = recipe.IngredientHashD;
            UnpackQuantities(recipe.QuantitiesPacked, out uint reqA, out uint reqB, out uint reqC, out uint reqD);
            NormalizeDuplicateRequirements(ref hashA, ref reqA, ref hashB, ref reqB, ref hashC, ref reqC, ref hashD, ref reqD);
            ResolveAvailableQuantities4(
                inventoryHashes,
                inventoryQuantities,
                inventoryCount,
                hashA,
                hashB,
                hashC,
                hashD,
                out uint haveA,
                out uint haveB,
                out uint haveC,
                out uint haveD);

            if (!CompareQuantities4(haveA, haveB, haveC, haveD, reqA, reqB, reqC, reqD, out simdLaneMask))
            {
                status = CraftingFastFailStatus.MissingQuantity;
                return false;
            }

            status = CraftingFastFailStatus.Success;
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ulong BuildRequirementMask(in RecipeRequirementDTO recipe)
        {
            ulong mask = 0UL;
            uint packed = recipe.QuantitiesPacked;
            if (recipe.IngredientHashA != 0u && (packed & 0xFFu) != 0u)
                mask |= InventoryMaterialMask.ResolveBit(recipe.IngredientHashA);
            if (recipe.IngredientHashB != 0u && ((packed >> 8) & 0xFFu) != 0u)
                mask |= InventoryMaterialMask.ResolveBit(recipe.IngredientHashB);
            if (recipe.IngredientHashC != 0u && ((packed >> 16) & 0xFFu) != 0u)
                mask |= InventoryMaterialMask.ResolveBit(recipe.IngredientHashC);
            if (recipe.IngredientHashD != 0u && ((packed >> 24) & 0xFFu) != 0u)
                mask |= InventoryMaterialMask.ResolveBit(recipe.IngredientHashD);
            return mask;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void UnpackQuantities(uint packed, out uint quantityA, out uint quantityB, out uint quantityC, out uint quantityD)
        {
            quantityA = packed & 0xFFu;
            quantityB = (packed >> 8) & 0xFFu;
            quantityC = (packed >> 16) & 0xFFu;
            quantityD = (packed >> 24) & 0xFFu;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void NormalizeDuplicateRequirements(
            ref uint hashA,
            ref uint quantityA,
            ref uint hashB,
            ref uint quantityB,
            ref uint hashC,
            ref uint quantityC,
            ref uint hashD,
            ref uint quantityD)
        {
            MergeDuplicate(ref hashA, ref quantityA, ref hashB, ref quantityB);
            MergeDuplicate(ref hashA, ref quantityA, ref hashC, ref quantityC);
            MergeDuplicate(ref hashA, ref quantityA, ref hashD, ref quantityD);
            MergeDuplicate(ref hashB, ref quantityB, ref hashC, ref quantityC);
            MergeDuplicate(ref hashB, ref quantityB, ref hashD, ref quantityD);
            MergeDuplicate(ref hashC, ref quantityC, ref hashD, ref quantityD);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [IgnoreWarning(1305)]
        internal static bool CompareQuantities4(
            uint haveA,
            uint haveB,
            uint haveC,
            uint haveD,
            uint requiredA,
            uint requiredB,
            uint requiredC,
            uint requiredD,
            out uint simdLaneMask)
        {
            if (X86.Sse2.IsSse2Supported)
            {
                v128 have = new v128(
                    unchecked((int)(haveA ^ UIntSignBit)),
                    unchecked((int)(haveB ^ UIntSignBit)),
                    unchecked((int)(haveC ^ UIntSignBit)),
                    unchecked((int)(haveD ^ UIntSignBit)));
                v128 required = new v128(
                    unchecked((int)(requiredA ^ UIntSignBit)),
                    unchecked((int)(requiredB ^ UIntSignBit)),
                    unchecked((int)(requiredC ^ UIntSignBit)),
                    unchecked((int)(requiredD ^ UIntSignBit)));
                v128 missing = X86.Sse2.cmpgt_epi32(required, have);
                simdLaneMask = (uint)CollapseLaneMask4(X86.Sse2.movemask_epi8(missing));
                return simdLaneMask == 0u;
            }

            bool4 enough = new bool4(haveA >= requiredA, haveB >= requiredB, haveC >= requiredC, haveD >= requiredD);
            simdLaneMask = (uint)(~math.bitmask(enough)) & 0xFu;
            return simdLaneMask == 0u;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [IgnoreWarning(1305)]
        internal static void ResolveAvailableQuantities4(
            NativeArray<uint> inventoryHashes,
            NativeArray<uint> inventoryQuantities,
            int inventoryCount,
            uint hashA,
            uint hashB,
            uint hashC,
            uint hashD,
            out uint haveA,
            out uint haveB,
            out uint haveC,
            out uint haveD)
        {
            ResolveAvailableQuantities4(
                inventoryHashes.AsReadOnly(),
                inventoryQuantities.AsReadOnly(),
                inventoryCount,
                hashA,
                hashB,
                hashC,
                hashD,
                out haveA,
                out haveB,
                out haveC,
                out haveD);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [IgnoreWarning(1305)]
        internal static void ResolveAvailableQuantities4(
            NativeArray<uint>.ReadOnly inventoryHashes,
            NativeArray<uint>.ReadOnly inventoryQuantities,
            int inventoryCount,
            uint hashA,
            uint hashB,
            uint hashC,
            uint hashD,
            out uint haveA,
            out uint haveB,
            out uint haveC,
            out uint haveD)
        {
            haveA = 0u;
            haveB = 0u;
            haveC = 0u;
            haveD = 0u;

            int capacity = math.min(inventoryCount, math.min(inventoryHashes.Length, inventoryQuantities.Length));
            if (X86.Sse2.IsSse2Supported)
            {
                v128 wanted = new v128(unchecked((int)hashA), unchecked((int)hashB), unchecked((int)hashC), unchecked((int)hashD));
                for (int i = 0; i < capacity; i++)
                {
                    uint slotHash = inventoryHashes[i];
                    uint quantity = inventoryQuantities[i];
                    if (slotHash == 0u || quantity == 0u || (quantity & UIntSignBit) != 0u)
                        continue;

                    v128 slot = X86.Sse2.set1_epi32(unchecked((int)slotHash));
                    int hitMask = CollapseLaneMask4(X86.Sse2.movemask_epi8(X86.Sse2.cmpeq_epi32(slot, wanted)));
                    if (hashA != 0u && (hitMask & 1) != 0)
                        haveA = SaturatingAdd(haveA, quantity);
                    if (hashB != 0u && (hitMask & 2) != 0)
                        haveB = SaturatingAdd(haveB, quantity);
                    if (hashC != 0u && (hitMask & 4) != 0)
                        haveC = SaturatingAdd(haveC, quantity);
                    if (hashD != 0u && (hitMask & 8) != 0)
                        haveD = SaturatingAdd(haveD, quantity);
                }

                return;
            }

            for (int i = 0; i < capacity; i++)
            {
                uint slotHash = inventoryHashes[i];
                uint quantity = inventoryQuantities[i];
                if (slotHash == 0u || quantity == 0u || (quantity & UIntSignBit) != 0u)
                    continue;

                if (hashA != 0u && slotHash == hashA)
                    haveA = SaturatingAdd(haveA, quantity);
                if (hashB != 0u && slotHash == hashB)
                    haveB = SaturatingAdd(haveB, quantity);
                if (hashC != 0u && slotHash == hashC)
                    haveC = SaturatingAdd(haveC, quantity);
                if (hashD != 0u && slotHash == hashD)
                    haveD = SaturatingAdd(haveD, quantity);
            }
        }

        private static bool OpenOrAcquireFastFailBuffer<T>(
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
            }
            else
            {
                handle = vault.EnsureGenerationHandle<T>(bufferId, requiredLength, SystemID.Crafting, options);
            }

            return vault.TryResolveHandle(in handle, out buffer) &&
                   buffer.IsCreated &&
                   buffer.Length >= requiredLength;
        }

        private static bool TryReadFastFailBuffer<T>(IDataVault vault, BufferID bufferId, out NativeArray<T> buffer)
            where T : struct
        {
            buffer = default;
            return vault != null &&
                   vault.TryGetGenerationHandle(bufferId, out VaultGenerationHandle<T> handle) &&
                   vault.TryReadHandle(in handle, out buffer) &&
                   buffer.IsCreated;
        }

        private static bool TryReadFastFailBufferReadOnly<T>(IDataVault vault, BufferID bufferId, out NativeArray<T>.ReadOnly buffer)
            where T : struct
        {
            buffer = default;
            return vault != null &&
                   vault.TryGetGenerationHandle(bufferId, out VaultGenerationHandle<T> handle) &&
                   vault.TryReadOnlyHandle(in handle, out buffer) &&
                   buffer.IsCreated;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void MergeDuplicate(ref uint primaryHash, ref uint primaryQuantity, ref uint duplicateHash, ref uint duplicateQuantity)
        {
            if (primaryHash == 0u || duplicateHash == 0u || primaryHash != duplicateHash)
                return;

            primaryQuantity = SaturatingAdd(primaryQuantity, duplicateQuantity);
            duplicateHash = 0u;
            duplicateQuantity = 0u;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint SaturatingAdd(uint left, uint right)
        {
            return left > uint.MaxValue - right ? uint.MaxValue : left + right;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int CollapseLaneMask4(int byteMask)
        {
            int mask = 0;
            mask |= (byteMask & 0x000F) != 0 ? 1 : 0;
            mask |= (byteMask & 0x00F0) != 0 ? 2 : 0;
            mask |= (byteMask & 0x0F00) != 0 ? 4 : 0;
            mask |= (byteMask & 0xF000) != 0 ? 8 : 0;
            return mask;
        }

        private static bool TryReadUIntToken(ReadOnlySpan<char> line, ref int cursor, out uint value)
        {
            if (!TryReadToken(line, ref cursor, out ReadOnlySpan<char> token))
            {
                value = 0u;
                return false;
            }

            return TryParseUInt(token, out value);
        }

        private static bool TryReadByteUIntToken(ReadOnlySpan<char> line, ref int cursor, out uint value)
        {
            if (!TryReadToken(line, ref cursor, out ReadOnlySpan<char> token))
            {
                value = 0u;
                return false;
            }

            return TryParseUInt(token, out value) && value <= 255u;
        }

        private static bool TryReadHashToken(ReadOnlySpan<byte> line, ref int cursor, out uint value)
        {
            if (!TryReadToken(line, ref cursor, out ReadOnlySpan<byte> token))
            {
                value = 0u;
                return false;
            }

            return TryParseUInt(token, out value) || TryHashToken(token, out value);
        }

        private static bool TryReadByteUIntToken(ReadOnlySpan<byte> line, ref int cursor, out uint value)
        {
            if (!TryReadToken(line, ref cursor, out ReadOnlySpan<byte> token))
            {
                value = 0u;
                return false;
            }

            return TryParseUInt(token, out value) && value <= 255u;
        }

        private static bool TryReadByteUnlockToken(ReadOnlySpan<byte> line, ref int cursor, out ulong value)
        {
            if (!TryReadToken(line, ref cursor, out ReadOnlySpan<byte> token))
            {
                value = AlwaysUnlockedMask;
                return true;
            }

            if (TryParseULong(token, out value))
                return true;

            if (!TryHashToken(token, out uint hash))
            {
                value = 0UL;
                return false;
            }

            value = ResolveBlueprintUnlockMask(hash);
            return true;
        }

        private static bool TryReadULongToken(ReadOnlySpan<char> line, ref int cursor, out ulong value)
        {
            if (!TryReadToken(line, ref cursor, out ReadOnlySpan<char> token))
            {
                value = 0UL;
                return false;
            }

            return TryParseULong(token, out value);
        }

        private static bool TryHashToken(ReadOnlySpan<byte> token, out uint hash)
        {
            hash = 0u;
            if (token.Length <= 0)
                return false;

            hash = Fnv1AAsciiLower(token);
            return hash != 0u;
        }

        private static bool TryReadToken(ReadOnlySpan<char> line, ref int cursor, out ReadOnlySpan<char> token)
        {
            while ((uint)cursor < (uint)line.Length && char.IsWhiteSpace(line[cursor]))
                cursor++;

            int start = cursor;
            while ((uint)cursor < (uint)line.Length && line[cursor] != ',' && line[cursor] != ';')
                cursor++;

            int end = cursor;
            if ((uint)cursor < (uint)line.Length)
                cursor++;

            while (end > start && char.IsWhiteSpace(line[end - 1]))
                end--;

            token = line.Slice(start, end - start);
            return token.Length > 0;
        }

        private static bool TryReadToken(ReadOnlySpan<byte> line, ref int cursor, out ReadOnlySpan<byte> token)
        {
            while ((uint)cursor < (uint)line.Length && IsAsciiWhitespace(line[cursor]))
                cursor++;

            int start = cursor;
            while ((uint)cursor < (uint)line.Length &&
                   line[cursor] != (byte)',' &&
                   line[cursor] != (byte)';')
            {
                cursor++;
            }

            int end = cursor;
            if ((uint)cursor < (uint)line.Length)
                cursor++;

            while (end > start && IsAsciiWhitespace(line[end - 1]))
                end--;

            token = line.Slice(start, end - start);
            return token.Length > 0;
        }

        private static bool TryParseUInt(ReadOnlySpan<char> token, out uint value)
        {
            value = 0u;
            int cursor = 0;
            int numberBase = 10;
            bool parsed = false;
            if (token.Length > 2 && token[0] == '0' && (token[1] == 'x' || token[1] == 'X'))
            {
                cursor = 2;
                numberBase = 16;
            }

            for (; cursor < token.Length; cursor++)
            {
                int digit = ResolveDigit(token[cursor]);
                if (digit < 0 || digit >= numberBase)
                    return false;

                uint factor = (uint)numberBase;
                if (value > (uint.MaxValue - (uint)digit) / factor)
                    return false;
                value = (value * factor) + (uint)digit;
                parsed = true;
            }

            return parsed;
        }

        private static bool TryParseUInt(ReadOnlySpan<byte> token, out uint value)
        {
            value = 0u;
            int cursor = 0;
            int numberBase = 10;
            bool parsed = false;
            if (token.Length > 2 && token[0] == (byte)'0' && (token[1] == (byte)'x' || token[1] == (byte)'X'))
            {
                cursor = 2;
                numberBase = 16;
            }

            for (; cursor < token.Length; cursor++)
            {
                int digit = ResolveDigit(token[cursor]);
                if (digit < 0 || digit >= numberBase)
                    return false;

                uint factor = (uint)numberBase;
                if (value > (uint.MaxValue - (uint)digit) / factor)
                    return false;
                value = (value * factor) + (uint)digit;
                parsed = true;
            }

            return parsed;
        }

        private static bool TryParseULong(ReadOnlySpan<char> token, out ulong value)
        {
            value = 0UL;
            int cursor = 0;
            int numberBase = 10;
            bool parsed = false;
            if (token.Length > 2 && token[0] == '0' && (token[1] == 'x' || token[1] == 'X'))
            {
                cursor = 2;
                numberBase = 16;
            }

            for (; cursor < token.Length; cursor++)
            {
                int digit = ResolveDigit(token[cursor]);
                if (digit < 0 || digit >= numberBase)
                    return false;

                ulong factor = (ulong)numberBase;
                if (value > (ulong.MaxValue - (ulong)digit) / factor)
                    return false;
                value = (value * factor) + (ulong)digit;
                parsed = true;
            }

            return parsed;
        }

        private static bool TryParseULong(ReadOnlySpan<byte> token, out ulong value)
        {
            value = 0UL;
            int cursor = 0;
            int numberBase = 10;
            bool parsed = false;
            if (token.Length > 2 && token[0] == (byte)'0' && (token[1] == (byte)'x' || token[1] == (byte)'X'))
            {
                cursor = 2;
                numberBase = 16;
            }

            for (; cursor < token.Length; cursor++)
            {
                int digit = ResolveDigit(token[cursor]);
                if (digit < 0 || digit >= numberBase)
                    return false;

                ulong factor = (ulong)numberBase;
                if (value > (ulong.MaxValue - (ulong)digit) / factor)
                    return false;
                value = (value * factor) + (ulong)digit;
                parsed = true;
            }

            return parsed;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int ResolveDigit(char c)
        {
            if (c >= '0' && c <= '9')
                return c - '0';
            if (c >= 'a' && c <= 'f')
                return 10 + c - 'a';
            if (c >= 'A' && c <= 'F')
                return 10 + c - 'A';
            return -1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int ResolveDigit(byte c)
        {
            if (c >= (byte)'0' && c <= (byte)'9')
                return c - (byte)'0';
            if (c >= (byte)'a' && c <= (byte)'f')
                return 10 + c - (byte)'a';
            if (c >= (byte)'A' && c <= (byte)'F')
                return 10 + c - (byte)'A';
            return -1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsAsciiWhitespace(byte value)
        {
            return value == (byte)' ' || value == (byte)'\t' || value == (byte)'\r' || value == (byte)'\n';
        }

        private static uint Fnv1AAsciiLower(ReadOnlySpan<byte> token)
        {
            uint hash = 2166136261u;
            for (int i = 0; i < token.Length; i++)
            {
                byte value = token[i];
                if (value >= (byte)'A' && value <= (byte)'Z')
                    value = (byte)(value + 32);
                hash ^= value;
                hash *= 16777619u;
            }

            return hash;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct GenerateMockRecipesJob : IJob
    {
        [WriteOnly, NoAlias] public NativeArray<RecipeRequirementDTO> Recipes;
        public int RequestedRecipeCount;
        public uint Seed;

        public void Execute()
        {
            if (!Recipes.IsCreated)
                return;

            int count = math.min(math.max(0, RequestedRecipeCount), Recipes.Length);
            uint seed = Seed == 0u ? 0xC3170001u : Seed;
            for (int i = 0; i < count; i++)
            {
                uint index = (uint)i;
                uint result = 0xC3170000u ^ math.hash(new uint4(seed, index, 0xA511E9B3u, 0x6C8E9CF5u));
                uint hashA = 0xA5000001u + (index & 127u);
                uint hashB = 0xB6000001u + ((index * 3u) & 127u);
                uint hashC = (i & 1) == 0 ? 0u : 0xC7000001u + ((index * 5u) & 127u);
                uint hashD = (i & 3) == 0 ? 0u : 0xD8000001u + ((index * 7u) & 127u);
                uint quantityA = 1u + (index & 7u);
                uint quantityB = 1u + ((index >> 1) & 7u);
                uint quantityC = hashC == 0u ? 0u : 1u + ((index >> 2) & 3u);
                uint quantityD = hashD == 0u ? 0u : 1u + ((index >> 3) & 3u);
                ulong unlockMask = 1UL << (int)(index & 63u);
                Recipes[i] = CraftingFastFailValidator.BuildRequirement(result, hashA, hashB, hashC, hashD, quantityA, quantityB, quantityC, quantityD, unlockMask);
            }
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct EvaluateCraftingAvailabilityJob : IJobParallelFor
    {
        [ReadOnly, NoAlias] public NativeArray<RecipeRequirementDTO> Recipes;
        [ReadOnly, NoAlias] public NativeArray<uint> InventoryHashes;
        [ReadOnly, NoAlias] public NativeArray<uint> InventoryQuantities;
        // SAFE: each Execute(wordIndex) owns exactly one ulong word, so no two workers write the same element.
        [NoAlias, NativeDisableParallelForRestriction] public NativeArray<ulong> CraftableWords;
        // SAFE: ring slot is selected through atomic TelemetryCursor; concurrent workers never claim the same cursor value.
        [NoAlias, NativeDisableParallelForRestriction] public NativeArray<CraftingFastFailTelemetryEntry> Telemetry;
        // SAFE: this is the single atomic cursor lane for telemetry claims, updated only via Interlocked.
        [NoAlias, NativeDisableParallelForRestriction] public NativeArray<int> TelemetryCursor;
        public int RecipeCount;
        public int UiPublicationRecipeBudget;
        public int InventoryCount;
        public ulong CurrentInventoryMask;
        public ulong PlayerUnlockMask;
        public uint Frame;
        public uint InventoryVersion;
        public float GlobalQualityWeight;
        public float ScheduleMicroseconds;

        public void Execute(int wordIndex)
        {
            if (!Recipes.IsCreated ||
                !InventoryHashes.IsCreated ||
                !InventoryQuantities.IsCreated ||
                !CraftableWords.IsCreated ||
                (uint)wordIndex >= (uint)CraftableWords.Length)
            {
                return;
            }

            int recipeLimit = math.min(math.max(0, RecipeCount), Recipes.Length);
            int firstRecipe = wordIndex << 6;
            int lastRecipeExclusive = math.min(firstRecipe + CraftingFastFailValidator.RecipesPerWord, recipeLimit);
            ulong word = 0UL;
            uint recipesEvaluated = 0u;
            uint unlockCullCount = 0u;
            uint maskCullCount = 0u;
            uint simdSuccessCount = 0u;
            uint stateHash = 0xC317F001u ^ (uint)wordIndex;
            ulong lastRequirementMask = 0UL;
            ulong normalizedUnlockMask = CraftingFastFailValidator.NormalizePlayerUnlockMask(PlayerUnlockMask);

            for (int recipeIndex = firstRecipe; recipeIndex < lastRecipeExclusive; recipeIndex++)
            {
                RecipeRequirementDTO recipe = Recipes[recipeIndex];
                recipesEvaluated++;
                bool craftable = EvaluateRecipe(
                    in recipe,
                    normalizedUnlockMask,
                    out CraftingFastFailStatus failure,
                    out uint simdLaneMask,
                    out ulong requirementMask);
                lastRequirementMask = requirementMask;
                if (failure == CraftingFastFailStatus.UnlockMissing)
                    unlockCullCount++;
                else if (failure == CraftingFastFailStatus.MaskMissing)
                    maskCullCount++;
                else if (failure == CraftingFastFailStatus.Success)
                    simdSuccessCount++;

                stateHash = math.hash(new uint4(stateHash, recipe.ResultItemHash, (uint)failure, simdLaneMask));
                if (craftable)
                    word |= 1UL << (recipeIndex & 63);
            }

            CraftableWords[wordIndex] = word;
            WriteTelemetry(
                wordIndex,
                recipesEvaluated,
                unlockCullCount,
                maskCullCount,
                simdSuccessCount,
                lastRequirementMask,
                normalizedUnlockMask,
                stateHash,
                word != 0UL ? 1u : 0u);
        }

        private bool EvaluateRecipe(
            in RecipeRequirementDTO recipe,
            ulong normalizedUnlockMask,
            out CraftingFastFailStatus failure,
            out uint simdLaneMask,
            out ulong requirementMask)
        {
            simdLaneMask = 0u;
            requirementMask = CraftingFastFailValidator.BuildRequirementMask(in recipe);
            if (recipe.ResultItemHash == 0u || requirementMask == 0UL)
            {
                failure = CraftingFastFailStatus.InvalidInput;
                return false;
            }

            if ((normalizedUnlockMask & recipe.BlueprintUnlockMask) == 0UL)
            {
                failure = CraftingFastFailStatus.UnlockMissing;
                return false;
            }

            if ((CurrentInventoryMask & requirementMask) != requirementMask)
            {
                failure = CraftingFastFailStatus.MaskMissing;
                return false;
            }

            uint hashA = recipe.IngredientHashA;
            uint hashB = recipe.IngredientHashB;
            uint hashC = recipe.IngredientHashC;
            uint hashD = recipe.IngredientHashD;
            CraftingFastFailValidator.UnpackQuantities(recipe.QuantitiesPacked, out uint reqA, out uint reqB, out uint reqC, out uint reqD);
            CraftingFastFailValidator.NormalizeDuplicateRequirements(ref hashA, ref reqA, ref hashB, ref reqB, ref hashC, ref reqC, ref hashD, ref reqD);
            CraftingFastFailValidator.ResolveAvailableQuantities4(
                InventoryHashes,
                InventoryQuantities,
                InventoryCount,
                hashA,
                hashB,
                hashC,
                hashD,
                out uint haveA,
                out uint haveB,
                out uint haveC,
                out uint haveD);

            if (!CraftingFastFailValidator.CompareQuantities4(haveA, haveB, haveC, haveD, reqA, reqB, reqC, reqD, out simdLaneMask))
            {
                failure = CraftingFastFailStatus.MissingQuantity;
                return false;
            }

            failure = CraftingFastFailStatus.Success;
            return true;
        }

        private unsafe void WriteTelemetry(
            int wordIndex,
            uint recipesEvaluated,
            uint unlockCullCount,
            uint maskCullCount,
            uint simdSuccessCount,
            ulong requirementMask,
            ulong unlockMask,
            uint stateHash,
            uint flags)
        {
            if (!Telemetry.IsCreated || Telemetry.Length <= 0)
                return;

            int slot = wordIndex % Telemetry.Length;
            if (TelemetryCursor.IsCreated && TelemetryCursor.Length > 0)
            {
                int* cursorPtr = (int*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(TelemetryCursor);
                ref int cursorRef = ref UnsafeUtility.AsRef<int>(cursorPtr);
                int cursor = Interlocked.Increment(ref cursorRef) - 1;
                slot = PositiveModulo(cursor, Telemetry.Length);
            }

            Telemetry[slot] = new CraftingFastFailTelemetryEntry
            {
                Frame = Frame,
                RecipeWordIndex = (uint)wordIndex,
                RecipesEvaluated = recipesEvaluated,
                UnlockCullCount = unlockCullCount,
                MaskCullCount = maskCullCount,
                SimdSuccessCount = simdSuccessCount,
                ScheduleMicroseconds = ScheduleMicroseconds,
                GlobalQualityWeight = math.saturate(math.isfinite(GlobalQualityWeight) ? GlobalQualityWeight : 0f),
                RequirementMask = requirementMask,
                UnlockMask = unlockMask,
                InventoryVersion = InventoryVersion,
                UiPublicationBudget = (uint)math.max(0, UiPublicationRecipeBudget),
                StateHash = stateHash,
                Flags = flags
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int PositiveModulo(int value, int length)
        {
            int result = value % length;
            return result < 0 ? result + length : result;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public unsafe struct CraftingFastFailTransactionJob : IJob
    {
        [ReadOnly, NoAlias] public NativeArray<RecipeRequirementDTO> Recipes;
        // SAFE: transaction job is scheduled as a single authoritative IJob and mutates rows only through CAS.
        [NoAlias, NativeDisableParallelForRestriction] public NativeArray<uint> InventoryHashes;
        // SAFE: transaction job is scheduled as a single authoritative IJob and mutates rows only through CAS.
        [NoAlias, NativeDisableParallelForRestriction] public NativeArray<uint> InventoryQuantities;
        // SAFE: single result slot is written by one IJob instance after transaction resolution.
        [NoAlias, NativeDisableParallelForRestriction] public NativeArray<int> Result;
        public int RecipeIndex;
        public int InventoryCount;
        public ulong CurrentInventoryMask;
        public ulong PlayerUnlockMask;

        public void Execute()
        {
            if (!Recipes.IsCreated ||
                !InventoryHashes.IsCreated ||
                !InventoryQuantities.IsCreated ||
                !Result.IsCreated ||
                Result.Length <= 0 ||
                (uint)RecipeIndex >= (uint)Recipes.Length)
            {
                WriteResult(CraftingFastFailStatus.InvalidInput);
                return;
            }

            RecipeRequirementDTO recipe = Recipes[RecipeIndex];
            ulong requirementMask = CraftingFastFailValidator.BuildRequirementMask(in recipe);
            if (recipe.ResultItemHash == 0u || requirementMask == 0UL)
            {
                WriteResult(CraftingFastFailStatus.InvalidInput);
                return;
            }

            if ((CraftingFastFailValidator.NormalizePlayerUnlockMask(PlayerUnlockMask) & recipe.BlueprintUnlockMask) == 0UL)
            {
                WriteResult(CraftingFastFailStatus.UnlockMissing);
                return;
            }

            if ((CurrentInventoryMask & requirementMask) != requirementMask)
            {
                WriteResult(CraftingFastFailStatus.MaskMissing);
                return;
            }

            uint hashA = recipe.IngredientHashA;
            uint hashB = recipe.IngredientHashB;
            uint hashC = recipe.IngredientHashC;
            uint hashD = recipe.IngredientHashD;
            CraftingFastFailValidator.UnpackQuantities(recipe.QuantitiesPacked, out uint reqA, out uint reqB, out uint reqC, out uint reqD);
            CraftingFastFailValidator.NormalizeDuplicateRequirements(ref hashA, ref reqA, ref hashB, ref reqB, ref hashC, ref reqC, ref hashD, ref reqD);
            CraftingFastFailValidator.ResolveAvailableQuantities4(
                InventoryHashes,
                InventoryQuantities,
                InventoryCount,
                hashA,
                hashB,
                hashC,
                hashD,
                out uint haveA,
                out uint haveB,
                out uint haveC,
                out uint haveD);
            if (!CraftingFastFailValidator.CompareQuantities4(haveA, haveB, haveC, haveD, reqA, reqB, reqC, reqD, out _))
            {
                WriteResult(CraftingFastFailStatus.MissingQuantity);
                return;
            }

            if (!TryDeduct(hashA, reqA))
            {
                WriteResult(CraftingFastFailStatus.AtomicConflict);
                return;
            }

            if (!TryDeduct(hashB, reqB))
            {
                Rollback(hashA, reqA);
                WriteResult(CraftingFastFailStatus.AtomicConflict);
                return;
            }

            if (!TryDeduct(hashC, reqC))
            {
                Rollback(hashB, reqB);
                Rollback(hashA, reqA);
                WriteResult(CraftingFastFailStatus.AtomicConflict);
                return;
            }

            if (!TryDeduct(hashD, reqD))
            {
                Rollback(hashC, reqC);
                Rollback(hashB, reqB);
                Rollback(hashA, reqA);
                WriteResult(CraftingFastFailStatus.AtomicConflict);
                return;
            }

            WriteResult(CraftingFastFailStatus.Success);
        }

        private bool TryDeduct(uint itemHash, uint quantity)
        {
            if (itemHash == 0u || quantity == 0u)
                return true;

            int capacity = math.min(InventoryCount, math.min(InventoryHashes.Length, InventoryQuantities.Length));
            int* hashPtr = (int*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(InventoryHashes);
            int* quantityPtr = (int*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(InventoryQuantities);
            int itemHashBits = unchecked((int)itemHash);
            uint remaining = quantity;
            uint removed = 0u;

            for (int index = 0; index < capacity && remaining > 0u; index++)
            {
                ref int hashRef = ref UnsafeUtility.AsRef<int>(hashPtr + index);
                if (Interlocked.CompareExchange(ref hashRef, 0, 0) != itemHashBits)
                    continue;

                ref int quantityRef = ref UnsafeUtility.AsRef<int>(quantityPtr + index);
                for (int attempt = 0; attempt < CraftingFastFailValidator.SlotCasRetryLimit; attempt++)
                {
                    int current = Interlocked.CompareExchange(ref quantityRef, 0, 0);
                    if (current <= 0)
                        break;

                    uint currentQuantity = unchecked((uint)current);
                    uint deducted = math.min(currentQuantity, remaining);
                    int next = unchecked((int)(currentQuantity - deducted));
                    if (Interlocked.CompareExchange(ref quantityRef, next, current) != current)
                        continue;

                    if (next == 0)
                        Interlocked.Exchange(ref hashRef, 0);

                    removed += deducted;
                    remaining -= deducted;
                    break;
                }
            }

            if (remaining == 0u)
                return true;

            if (removed > 0u)
                Rollback(itemHash, removed);
            return false;
        }

        private void Rollback(uint itemHash, uint quantity)
        {
            if (itemHash == 0u || quantity == 0u)
                return;

            int capacity = math.min(InventoryCount, math.min(InventoryHashes.Length, InventoryQuantities.Length));
            int* hashPtr = (int*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(InventoryHashes);
            int* quantityPtr = (int*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(InventoryQuantities);
            int itemHashBits = unchecked((int)itemHash);

            for (int index = 0; index < capacity; index++)
            {
                ref int hashRef = ref UnsafeUtility.AsRef<int>(hashPtr + index);
                int observedHash = Interlocked.CompareExchange(ref hashRef, 0, 0);
                if (observedHash != itemHashBits && observedHash != 0)
                    continue;

                if (observedHash == 0 && Interlocked.CompareExchange(ref hashRef, itemHashBits, 0) != 0)
                    continue;

                ref int quantityRef = ref UnsafeUtility.AsRef<int>(quantityPtr + index);
                for (int attempt = 0; attempt < CraftingFastFailValidator.SlotCasRetryLimit; attempt++)
                {
                    int current = Interlocked.CompareExchange(ref quantityRef, 0, 0);
                    if (current < 0)
                        continue;

                    long next = (long)current + quantity;
                    if (next > int.MaxValue)
                        next = int.MaxValue;

                    if (Interlocked.CompareExchange(ref quantityRef, (int)next, current) == current)
                        return;
                }
            }
        }

        private void WriteResult(CraftingFastFailStatus status)
        {
            Result[0] = (int)status;
        }
    }

    internal static partial class CraftingSystem
    {
        internal static bool TryBuildFastFailRequirement(RecipeData recipe, int multiplier, out RecipeRequirementDTO requirement)
        {
            return CraftingFastFailValidator.TryBuildRequirementFromRecipeData(recipe, multiplier, out requirement);
        }
    }

    public sealed class CraftingFastFailDebugGizmo : MonoBehaviour
    {
        [SerializeField] private Color craftableColor = new Color(0.1f, 0.85f, 0.38f, 0.85f);
        [SerializeField] private Color blockedColor = new Color(0.95f, 0.18f, 0.12f, 0.85f);
        [SerializeField, Min(0f)] private float radius = 0.4f;
        [SerializeField] private bool lastCraftable;

        public void SetLastCraftable(bool craftable)
        {
            lastCraftable = craftable;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = lastCraftable ? craftableColor : blockedColor;
            Vector3 center = transform.position + (Vector3.up * math.max(0.05f, radius));
            float diameter = math.max(0.05f, radius) * 2f;
            Gizmos.DrawWireCube(center, new Vector3(diameter, diameter, diameter));
        }
    }
}
