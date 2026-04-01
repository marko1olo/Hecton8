using System;

namespace Hecton8.SaveSystem
{
    [Serializable]
    public sealed class SaveSlotMaintenanceRecord
    {
        public string SlotName;
        public long LastSuccessfulSaveTicksUtc;
        public long LastSuccessfulLoadTicksUtc;
        public long LastAuditTicksUtc;
        public long LastRepairTicksUtc;
        public long LastFailureTicksUtc;
        public int SuccessfulSaveCount;
        public int SuccessfulLoadCount;
        public int AuditCount;
        public int RepairCount;
        public int FailureCount;
        public bool LastAuditReadable;
        public bool LastAuditRecommendedRepair;
        public bool LastLoadUsedBackup;
        public int LastLoadBackupGeneration;
        public bool LastLoadUsedLegacyCompression;
        public bool LastLoadSelfRepaired;
        public int LastKnownSaveVersion;
        public string LastKnownIntegrityState;
        public string LastFailureContext;
        public string LastFailureMessage;
        public string LastAuditMessage;
        public string LastRepairMessage;

        public string slotName => SlotName;

        public static string GetPath(string slotName)
        {
            return $"{slotName}.diag";
        }

        public void Save()
        {
            if (string.IsNullOrEmpty(SlotName))
                return;

            ES3Settings settings = new ES3Settings { compressionType = ES3.CompressionType.None };
            ES3.Save("diag", this, GetPath(SlotName), settings);
        }

        public static SaveSlotMaintenanceRecord Load(string slotName)
        {
            if (string.IsNullOrEmpty(slotName))
                return null;

            string path = GetPath(slotName);
            if (!ES3.FileExists(path))
                return null;

            try
            {
                ES3Settings settings = new ES3Settings { compressionType = ES3.CompressionType.None };
                return ES3.Load<SaveSlotMaintenanceRecord>("diag", path, settings);
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning($"[SaveSlotMaintenanceRecord] Failed to load diag for '{slotName}': {ex.Message}");
                return null;
            }
        }

        public static SaveSlotMaintenanceRecord Create(string slotName)
        {
            return new SaveSlotMaintenanceRecord
            {
                SlotName = slotName,
                LastKnownIntegrityState = SaveSlotIntegrityState.Empty.ToString(),
                LastFailureContext = string.Empty,
                LastFailureMessage = string.Empty,
                LastAuditMessage = string.Empty,
                LastRepairMessage = string.Empty
            };
        }
    }
}
