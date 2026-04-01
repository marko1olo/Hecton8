using System;

namespace Hecton8.SaveSystem
{
    [Serializable]
    public sealed class SaveSlotRepairResult
    {
        public string SlotName;
        public bool Success;
        public bool ChangedAnything;
        public bool UsedBackupSource;
        public int SourceBackupGeneration;
        public bool UsedLegacyCompression;
        public bool RewrotePrimarySave;
        public bool RewrotePrimaryMetadata;
        public string Message;
        public SaveSlotIntegrityState IntegrityBefore;
        public SaveSlotIntegrityState IntegrityAfter;

        public string slotName => SlotName;
        public string message => Message;
    }
}
