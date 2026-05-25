using System;
using System.Globalization;
using UnityEngine;

namespace Hecton8.SaveSystem
{
    /// <summary>
    /// Lightweight metadata for a save slot.
    /// This is stored separately (.meta) to allow the UI
    /// to display save details without loading the full game state.
    /// </summary>
    [Serializable]
    public sealed class SaveMetadata
    {
        [Header("── Identification ─────────────────────────────")]
        public string SlotName;
        public string GameVersion;
        public long Timestamp; // UTC Ticks

        [Header("── Gameplay Info ─────────────────────────────")]
        public float PlayTimeSeconds;
        public string SceneName;
        public Vector3 PlayerPosition;
        public int WorldSeed;
        public int WorldGenerationVersionId;
        
        [Header("── Integrity ─────────────────────────────────")]
        public string Checksum; // XXHash3 checksum (hex; v4+ uses 64-bit, legacy v3 remains 32-bit)

        public DateTime GetDateTime() => new DateTime(Timestamp, DateTimeKind.Utc);
        public string slotName => SlotName;
        public string sceneName => SceneName;
        public string timestamp => GetDateTime().ToLocalTime().ToString("g", CultureInfo.InvariantCulture);
        public float totalPlayTime => PlayTimeSeconds;
        public string version => GameVersion;

        public string GetFormattedPlayTime()
        {
            TimeSpan t = TimeSpan.FromSeconds(PlayTimeSeconds);
            return ((int)t.TotalHours).ToString("D2", CultureInfo.InvariantCulture) + ":" +
                   t.Minutes.ToString("D2", CultureInfo.InvariantCulture) + ":" +
                   t.Seconds.ToString("D2", CultureInfo.InvariantCulture);
        }

        // ═════════════════════════════════════════════════════════
        //  Persistence Methods (Static)
        // ═════════════════════════════════════════════════════════

        public void Save()
        {
            Save(GetPrimaryMetadataPath(SlotName));
        }

        public void Save(string path)
        {
            if (string.IsNullOrEmpty(path))
                return;

            if (!SaveSidecarStorage.SaveMetadata(this, path, out string error))
                Hecton8.Core.H8Debug.LogWarning($"[SaveMetadata] Failed to save meta to '{path}': {error}");
        }

        public static SaveMetadata Load(string slotName)
        {
            return LoadFromPath(GetPrimaryMetadataPath(slotName));
        }

        public static SaveMetadata LoadFromPath(string path)
        {
            if (string.IsNullOrEmpty(path) || !SaveSidecarStorage.Exists(path))
                return null;

            try
            {
                return SaveSidecarStorage.LoadMetadata(path, out SaveMetadata metadata, out string error)
                    ? metadata
                    : HandleLoadFailure(path, error);
            }
            catch (Exception ex)
            {
                Hecton8.Core.H8Debug.LogWarning($"[SaveMetadata] Failed to load meta from '{path}': {ex.Message}");
                return null;
            }
        }

        public static SaveMetadata CreateFallback(string slotName, long timestampTicksUtc)
        {
            return new SaveMetadata
            {
                SlotName = slotName,
                GameVersion = "unknown",
                Timestamp = timestampTicksUtc > 0 ? timestampTicksUtc : DateTime.UtcNow.Ticks,
                PlayTimeSeconds = 0f,
                SceneName = "Unknown",
                PlayerPosition = Vector3.zero,
                WorldSeed = 0,
                WorldGenerationVersionId = 0,
                Checksum = string.Empty
            };
        }

        public static string GetPrimaryMetadataPath(string slotName)
        {
            return $"{slotName}.meta";
        }

        public static string GetBackupMetadataPath(string slotName)
        {
            return GetBackupMetadataPath(slotName, 1);
        }

        public static string GetBackupMetadataPath(string slotName, int generation)
        {
            if (generation <= 1)
                return $"{slotName}.meta.bak";

            return $"{slotName}.meta.bak{generation}";
        }

        public static string GetTempMetadataPath(string slotName)
        {
            return $"{slotName}.meta.tmp";
        }

        public static bool Exists(string path)
        {
            return !string.IsNullOrEmpty(path) && SaveSidecarStorage.Exists(path);
        }

        public static bool Delete(string path)
        {
            return SaveSidecarStorage.Delete(path);
        }

        private static SaveMetadata HandleLoadFailure(string path, string error)
        {
            Hecton8.Core.H8Debug.LogWarning($"[SaveMetadata] Failed to load meta from '{path}': {error}");
            return null;
        }
    }
}
