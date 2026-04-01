using System;

namespace Hecton8.SaveSystem
{
    public enum SaveSlotIntegrityState
    {
        Empty = 0,
        Healthy,
        HealthyWithBackup,
        BackupOnly,
        MissingMetadata,
        MetadataRecoveredFromBackup,
        MetadataSynthesized,
        CorruptedMetadata
    }

    [Serializable]
    public sealed class SaveSlotInfo
    {
        public string SlotName;
        public SaveMetadata Metadata;
        public SaveSlotIntegrityState IntegrityState;
        public bool HasPrimarySave;
        public bool HasBackupSave;
        public bool HasPrimaryMetadata;
        public bool HasBackupMetadata;
        public bool HasThumbnail;
        public bool MetadataRecoveredFromBackup;
        public bool MetadataSynthesized;
        public long LastWriteTicksUtc;
        public long PrimarySaveBytes;
        public long BackupSaveBytes;

        public string slotName => SlotName;
        public SaveMetadata metadata => Metadata;
        public string integrity => IntegrityState.ToString();

        public bool HasAnySaveData => HasPrimarySave || HasBackupSave;

        public string GetStatusLabel()
        {
            switch (IntegrityState)
            {
                case SaveSlotIntegrityState.Healthy:
                    return "Primary";
                case SaveSlotIntegrityState.HealthyWithBackup:
                    return "Primary + Backup";
                case SaveSlotIntegrityState.BackupOnly:
                    return "Backup Only";
                case SaveSlotIntegrityState.MissingMetadata:
                    return "Missing Metadata";
                case SaveSlotIntegrityState.MetadataRecoveredFromBackup:
                    return "Metadata Recovered";
                case SaveSlotIntegrityState.MetadataSynthesized:
                    return "Synthesized Metadata";
                case SaveSlotIntegrityState.CorruptedMetadata:
                    return "Corrupted Metadata";
                default:
                    return "Empty";
            }
        }
    }
}
