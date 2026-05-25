#if UNITY_EDITOR
using System;
using System.Reflection;
using Hecton8.Core;
using Hecton8.Data;
using Unity.Collections.LowLevel.Unsafe;
using UnityEditor;

namespace Hecton8.EditorValidation
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
                ExpectField<H8ItemRecord>(nameof(H8ItemRecord.HashId), 0);
                ExpectField<H8ItemRecord>(nameof(H8ItemRecord.MassKg), 32);
                ExpectField<H8ItemRecord>(nameof(H8ItemRecord.AccessFrequency), 76);

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
            }
            catch (Exception ex)
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
    }
}
#endif
