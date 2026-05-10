using System;

namespace Hecton8.SaveSystem
{
    [Serializable]
    public sealed class SaveSlotMaintenanceRecord
    {
        internal const byte LastAuditReadableFlag = 1 << 0;
        internal const byte LastAuditRecommendedRepairFlag = 1 << 1;
        internal const byte LastLoadUsedBackupFlag = 1 << 2;
        internal const byte LastLoadUsedLegacyCompressionFlag = 1 << 3;
        internal const byte LastLoadSelfRepairedFlag = 1 << 4;

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

        internal byte PackStateFlags()
        {
            return (byte)(
                (LastAuditReadable ? LastAuditReadableFlag : 0) |
                (LastAuditRecommendedRepair ? LastAuditRecommendedRepairFlag : 0) |
                (LastLoadUsedBackup ? LastLoadUsedBackupFlag : 0) |
                (LastLoadUsedLegacyCompression ? LastLoadUsedLegacyCompressionFlag : 0) |
                (LastLoadSelfRepaired ? LastLoadSelfRepairedFlag : 0));
        }

        internal void ApplyStateFlags(byte flags)
        {
            LastAuditReadable = (flags & LastAuditReadableFlag) != 0;
            LastAuditRecommendedRepair = (flags & LastAuditRecommendedRepairFlag) != 0;
            LastLoadUsedBackup = (flags & LastLoadUsedBackupFlag) != 0;
            LastLoadUsedLegacyCompression = (flags & LastLoadUsedLegacyCompressionFlag) != 0;
            LastLoadSelfRepaired = (flags & LastLoadSelfRepairedFlag) != 0;
        }

        public static string GetPath(string slotName)
        {
            return SaveManager.GetDiagnosticSaveFilePath(slotName);
        }

        public void Save()
        {
            if (!SaveManager.TryResolveSafeSlotName(SlotName, out string safeSlotName))
                return;

            SlotName = safeSlotName;
            if (!SaveSidecarStorage.SaveMaintenanceRecord(this, out string error))
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                UnityEngine.Debug.LogWarning($"[SaveSlotMaintenanceRecord] Failed to save diag for '{SlotName}': {error}");
#endif
            }
        }

        public static SaveSlotMaintenanceRecord Load(string slotName)
        {
            if (!SaveManager.TryResolveSafeSlotName(slotName, out string safeSlotName))
                return null;

            if (!SaveSidecarStorage.Exists(GetPath(safeSlotName)))
                return null;

            try
            {
                return SaveSidecarStorage.LoadMaintenanceRecord(safeSlotName, out SaveSlotMaintenanceRecord record, out string error)
                    ? record
                    : HandleLoadFailure(safeSlotName, error);
            }
            catch (Exception ex)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                UnityEngine.Debug.LogWarning($"[SaveSlotMaintenanceRecord] Failed to load diag for '{safeSlotName}': {ex.Message}");
#endif
                return null;
            }
        }

        public static SaveSlotMaintenanceRecord Create(string slotName)
        {
            if (!SaveManager.TryResolveSafeSlotName(slotName, out string safeSlotName))
                return null;

            return new SaveSlotMaintenanceRecord
            {
                SlotName = safeSlotName,
                LastKnownIntegrityState = SaveSlotInfo.ToStorageString(SaveSlotIntegrityState.Empty),
                LastFailureContext = string.Empty,
                LastFailureMessage = string.Empty,
                LastAuditMessage = string.Empty,
                LastRepairMessage = string.Empty
            };
        }

        private static SaveSlotMaintenanceRecord HandleLoadFailure(string slotName, string error)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            UnityEngine.Debug.LogWarning($"[SaveSlotMaintenanceRecord] Failed to load diag for '{slotName}': {error}");
#endif
            return null;
        }
    }
}
