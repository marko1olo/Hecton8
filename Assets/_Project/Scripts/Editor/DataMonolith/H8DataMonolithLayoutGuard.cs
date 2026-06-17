#if UNITY_EDITOR
using System;
using System.Reflection;
using System.Runtime.InteropServices;
using Hecton8.Core;
using Hecton8.Data;
using Unity.Collections.LowLevel.Unsafe;
using UnityEditor;

namespace Hecton8.Editor.Validation
{
    [InitializeOnLoad]
    internal static class H8DataMonolithLayoutGuard
    {
        static H8DataMonolithLayoutGuard()
        {
            ValidateOrThrow();
        }

        [MenuItem("Hecton8/Data Monolith/Validate Binary Layouts")]
        internal static void ValidateOrThrow()
        {
            try
            {
                ExpectSize<H8DataBlobHeader>(H8DataLayoutConstants.HeaderSizeBytes);
                ExpectField<H8DataBlobHeader>(nameof(H8DataBlobHeader.Magic), 0);
                ExpectField<H8DataBlobHeader>(nameof(H8DataBlobHeader.FormatVersion), 4);
                ExpectField<H8DataBlobHeader>(nameof(H8DataBlobHeader.HeaderBytes), 6);
                ExpectField<H8DataBlobHeader>(nameof(H8DataBlobHeader.Checksum64), 8);
                ExpectField<H8DataBlobHeader>(nameof(H8DataBlobHeader.BlobBytes), 16);
                ExpectField<H8DataBlobHeader>(nameof(H8DataBlobHeader.DirectoryOffset), 20);
                ExpectField<H8DataBlobHeader>(nameof(H8DataBlobHeader.DirectoryBytes), 24);
                ExpectField<H8DataBlobHeader>(nameof(H8DataBlobHeader.SectionTableOffset), 28);
                ExpectField<H8DataBlobHeader>(nameof(H8DataBlobHeader.SectionCount), 32);
                ExpectField<H8DataBlobHeader>(nameof(H8DataBlobHeader.Flags), 36);
                ExpectField<H8DataBlobHeader>(nameof(H8DataBlobHeader.WorldSeed), 40);
                ExpectField<H8DataBlobHeader>(nameof(H8DataBlobHeader.AppVersionHash), 44);
                ExpectField<H8DataBlobHeader>(nameof(H8DataBlobHeader.SchemaHash), 48);

                ExpectSize<H8DataBlobDirectory>(H8DataLayoutConstants.DirectorySizeBytes);
                ExpectField<H8DataBlobDirectory>(nameof(H8DataBlobDirectory.Magic), 0);
                ExpectField<H8DataBlobDirectory>(nameof(H8DataBlobDirectory.SectionCount), 6);
                ExpectField<H8DataBlobDirectory>(nameof(H8DataBlobDirectory.SectionTableOffset), 8);
                ExpectField<H8DataBlobDirectory>(nameof(H8DataBlobDirectory.BlobBytes), 16);
                ExpectField<H8DataBlobDirectory>(nameof(H8DataBlobDirectory.AppVersionHash), 40);

                ExpectSize<H8DataSectionEntry>(16);
                ExpectField<H8DataSectionEntry>(nameof(H8DataSectionEntry.SectionId), 0);
                ExpectField<H8DataSectionEntry>(nameof(H8DataSectionEntry.RecordSize), 4);
                ExpectField<H8DataSectionEntry>(nameof(H8DataSectionEntry.Count), 8);
                ExpectField<H8DataSectionEntry>(nameof(H8DataSectionEntry.OffsetBytes), 12);

                ExpectSize<H8ItemRecord>(H8DataLayoutConstants.ItemRecordSize);
                ExpectField<H8ItemRecord>(nameof(H8ItemRecord.RecipeMask0), 0);
                ExpectField<H8ItemRecord>(nameof(H8ItemRecord.RecipeMask1), 8);
                ExpectField<H8ItemRecord>(nameof(H8ItemRecord.HashId), 16);
                ExpectField<H8ItemRecord>(nameof(H8ItemRecord.MassKg), 32);
                ExpectField<H8ItemRecord>(nameof(H8ItemRecord.AccessFrequency), 72);
                ExpectField<H8ItemRecord>(nameof(H8ItemRecord.MaxStack), 76);
                ExpectField<H8ItemRecord>(nameof(H8ItemRecord.RecipeIngredientCount), 78);

                ExpectSize<H8CreatureTraitRecord>(H8DataLayoutConstants.CreatureTraitRecordSize);
                ExpectField<H8CreatureTraitRecord>(nameof(H8CreatureTraitRecord.Genome), 16);
                ExpectField<H8CreatureTraitRecord>(nameof(H8CreatureTraitRecord.DisplayNameUtf8ByteLength), 60);

                ExpectSize<H8BiomeRecord>(H8DataLayoutConstants.BiomeRecordSize);
                ExpectField<H8BiomeRecord>(nameof(H8BiomeRecord.MinDepthMeters), 16);
                ExpectField<H8BiomeRecord>(nameof(H8BiomeRecord.DisplayNameUtf8ByteLength), 60);

                ExpectSize<H8EconomyRecord>(H8DataLayoutConstants.EconomyRecordSize);
                ExpectField<H8EconomyRecord>(nameof(H8EconomyRecord.BasePrice), 12);
                ExpectField<H8EconomyRecord>(nameof(H8EconomyRecord.AccessFrequency), 28);

                ExpectSize<H8PhysicsConstantsRecord>(H8DataLayoutConstants.PhysicsConstantsRecordSize);
                ExpectField<H8PhysicsConstantsRecord>(nameof(H8PhysicsConstantsRecord.MassKg), 20);
                ExpectField<H8PhysicsConstantsRecord>(nameof(H8PhysicsConstantsRecord.AccessFrequency), 48);

                ExpectSize<H8DataMonolithTelemetryEntry>(H8DataLayoutConstants.TelemetryEntrySize);
                ExpectField<H8DataMonolithTelemetryEntry>(nameof(H8DataMonolithTelemetryEntry.Checksum64), 0);
                ExpectField<H8DataMonolithTelemetryEntry>(nameof(H8DataMonolithTelemetryEntry.StateHash), 44);

                if (!H8DataLayoutAudit.ValidateBlittableSizes())
                    throw new InvalidOperationException("H8DataLayoutAudit returned false.");

                ExpectAllDeclaredLayouts();
            }
            catch (InvalidOperationException ex)
            {
                throw new FatalArchitectureException("[H8DataMonolithLayoutGuard] " + ex.Message);
            }
            catch (ArgumentException ex)
            {
                throw new FatalArchitectureException("[H8DataMonolithLayoutGuard] " + ex.Message);
            }
            catch (NotSupportedException ex)
            {
                throw new FatalArchitectureException("[H8DataMonolithLayoutGuard] " + ex.Message);
            }
        }

        private static void ExpectSize<T>(int expected)
            where T : struct
        {
            int observed = UnsafeUtility.SizeOf<T>();
            if (observed != expected)
                throw new InvalidOperationException(typeof(T).Name + " size " + observed + " expected " + expected);

            if (expected >= H8DataLayoutConstants.RecordAlignmentBytes &&
                (observed & (H8DataLayoutConstants.RecordAlignmentBytes - 1)) != 0)
            {
                throw new InvalidOperationException(typeof(T).Name + " is not " + H8DataLayoutConstants.RecordAlignmentBytes + "-byte aligned.");
            }
        }

        private static void ExpectField<T>(string fieldName, int expectedOffset)
            where T : struct
        {
            FieldInfo field = typeof(T).GetField(fieldName, BindingFlags.Public | BindingFlags.Instance);
            int observed = field == null ? -1 : UnsafeUtility.GetFieldOffset(field);
            if (observed != expectedOffset)
            {
                throw new InvalidOperationException(
                    typeof(T).Name + "." + fieldName + " offset " + observed + " expected " + expectedOffset);
            }
        }

        private static void ExpectAllDeclaredLayouts()
        {
            ExpectDeclaredLayout<H8DataBlobHeader>(H8DataLayoutConstants.HeaderSizeBytes);
            ExpectDeclaredLayout<H8DataBlobDirectory>(H8DataLayoutConstants.DirectorySizeBytes);
            ExpectDeclaredLayout<H8DataSectionEntry>(16);
            ExpectDeclaredLayout<H8ItemRecord>(H8DataLayoutConstants.ItemRecordSize);
            ExpectDeclaredLayout<H8CreatureGenomeTraitBlock>(H8DataLayoutConstants.CreatureGenomeTraitBlockSize);
            ExpectDeclaredLayout<H8CreatureTraitRecord>(H8DataLayoutConstants.CreatureTraitRecordSize);
            ExpectDeclaredLayout<H8BiomeRecord>(H8DataLayoutConstants.BiomeRecordSize);
            ExpectDeclaredLayout<H8RecipeRecord>(64);
            ExpectDeclaredLayout<H8BiomeHeatmapCellRecord>(16);
            ExpectDeclaredLayout<H8QuestNodeRecord>(32);
            ExpectDeclaredLayout<H8QuestEdgeRecord>(16);
            ExpectDeclaredLayout<H8LootCdfRecord>(16);
            ExpectDeclaredLayout<H8VoxelMaterialRecord>(32);
            ExpectDeclaredLayout<H8AudioClipRegistryRecord>(16);
            ExpectDeclaredLayout<H8VfxScalarRecord>(32);
            ExpectDeclaredLayout<H8DepthPressureSampleRecord>(16);
            ExpectDeclaredLayout<H8ToolHeatCapacityRecord>(16);
            ExpectDeclaredLayout<H8SubmarineHullConstantRecord>(32);
            ExpectDeclaredLayout<H8NarrativeTriggerRecord>(32);
            ExpectDeclaredLayout<H8PhysicsMaterialRecord>(16);
            ExpectDeclaredLayout<H8GhostModuleRecord>(64);
            ExpectDeclaredLayout<H8RadiationIntensityCellRecord>(16);
            ExpectDeclaredLayout<H8SpawnCreditCostRecord>(16);
            ExpectDeclaredLayout<H8LightAttenuationSampleRecord>(32);
            ExpectDeclaredLayout<H8SopErrorRecord>(16);
            ExpectDeclaredLayout<H8HudLayoutRecord>(64);
            ExpectDeclaredLayout<H8SectorPageRecord>(32);
            ExpectDeclaredLayout<H8EconomyRecord>(H8DataLayoutConstants.EconomyRecordSize);
            ExpectDeclaredLayout<H8PhysicsConstantsRecord>(H8DataLayoutConstants.PhysicsConstantsRecordSize);
            ExpectDeclaredLayout<H8DataMonolithTelemetryEntry>(H8DataLayoutConstants.TelemetryEntrySize);
            ExpectDeclaredLayout<H8StaticLocalizationReference>(16);
            ExpectDeclaredLayout<H8StaticLocalizationCursor>(8);
        }

        private static void ExpectDeclaredLayout<T>(int expectedSize)
            where T : struct
        {
            Type type = typeof(T);
            StructLayoutAttribute layout = type.StructLayoutAttribute;
            if (layout == null || layout.Value != LayoutKind.Explicit)
                throw new InvalidOperationException(type.Name + " must use explicit layout.");

            if (layout.Pack == 1)
            {
                throw new InvalidOperationException(
                    type.Name + " must not use StructLayout Pack=1 in runtime-view Data Monolith DTOs; explicit FieldOffset/Size own the ABI.");
            }

            int observedSize = UnsafeUtility.SizeOf<T>();
            if (observedSize != expectedSize)
                throw new InvalidOperationException(type.Name + " declared size " + observedSize + " expected " + expectedSize);

            if ((observedSize & 7) != 0)
                throw new InvalidOperationException(type.Name + " size " + observedSize + " is not 8-byte aligned.");

            FieldInfo[] fields = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (fields.Length == 0)
                throw new InvalidOperationException(type.Name + " has no instance fields.");

            bool[] occupied = new bool[observedSize];
            for (int i = 0; i < fields.Length; i++)
            {
                FieldInfo field = fields[i];
                FieldOffsetAttribute fieldOffset = field.GetCustomAttribute<FieldOffsetAttribute>();
                if (fieldOffset == null)
                    throw new InvalidOperationException(type.Name + "." + field.Name + " has no FieldOffset.");

                int observedOffset = UnsafeUtility.GetFieldOffset(field);
                if (observedOffset != fieldOffset.Value)
                {
                    throw new InvalidOperationException(
                        type.Name + "." + field.Name + " offset " + observedOffset + " does not match FieldOffset " + fieldOffset.Value);
                }

                int fieldSize = ResolveFieldSize(field.FieldType);
                if (observedOffset < 0 || observedOffset + fieldSize > observedSize)
                {
                    throw new InvalidOperationException(
                        type.Name + "." + field.Name + " overruns size " + observedSize + " at " + observedOffset + "+" + fieldSize);
                }

                int requiredAlignment = fieldSize > 8 ? 8 : fieldSize;
                if (requiredAlignment > 1 && (observedOffset % requiredAlignment) != 0)
                {
                    throw new InvalidOperationException(
                        type.Name + "." + field.Name + " offset " + observedOffset + " violates " + requiredAlignment + "-byte natural alignment.");
                }

                for (int byteIndex = observedOffset; byteIndex < observedOffset + fieldSize; byteIndex++)
                {
                    if (occupied[byteIndex])
                        throw new InvalidOperationException(type.Name + "." + field.Name + " overlaps byte " + byteIndex);

                    occupied[byteIndex] = true;
                }
            }

            for (int byteIndex = 0; byteIndex < occupied.Length; byteIndex++)
            {
                if (!occupied[byteIndex])
                    throw new InvalidOperationException(type.Name + " has undeclared padding byte at offset " + byteIndex);
            }
        }

        private static int ResolveFieldSize(Type fieldType)
        {
            if (fieldType == typeof(bool))
                throw new InvalidOperationException("bool fields are forbidden in Data Monolith DTOs.");

            if (!fieldType.IsValueType || fieldType == typeof(string))
                throw new InvalidOperationException("Managed field type " + fieldType.Name + " is forbidden in Data Monolith DTOs.");

            if (fieldType == typeof(byte) || fieldType == typeof(sbyte))
                return 1;

            if (fieldType == typeof(short) || fieldType == typeof(ushort) || fieldType == typeof(char))
                return 2;

            if (fieldType == typeof(int) || fieldType == typeof(uint) || fieldType == typeof(float))
                return 4;

            if (fieldType == typeof(long) || fieldType == typeof(ulong) || fieldType == typeof(double))
                return 8;

            if (fieldType == typeof(H8CreatureGenomeTraitBlock))
                return H8DataLayoutConstants.CreatureGenomeTraitBlockSize;

            throw new InvalidOperationException("Unsupported Data Monolith DTO field type " + fieldType.Name + ".");
        }
    }
}
#endif
