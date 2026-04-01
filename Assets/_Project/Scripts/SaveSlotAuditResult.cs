using System;

namespace Hecton8.SaveSystem
{
    [Serializable]
    public sealed class SaveSlotAuditResult
    {
        public string SlotName;
        public bool Success;
        public bool SlotReadable;
        public bool HasPrimaryCandidate;
        public bool HasBackupCandidate;
        public bool PrimaryReadable;
        public bool BackupReadable;
        public bool SelectedBackupSource;
        public int SelectedBackupGeneration;
        public bool SelectedLegacyCompression;
        public bool RequiresMigration;
        public bool RecommendedRepair;
        public int DetectedVersion;
        public SaveSlotIntegrityState IntegrityState;
        public string RecommendedSource;
        public string PrimaryError;
        public string BackupError;
        public string Message;

        public string slotName => SlotName;
        public string message => Message;
    }
}
