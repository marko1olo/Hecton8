using System;
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
        
        [Header("── Integrity ─────────────────────────────────")]
        public string Checksum; // CRC32 (hex)

        public DateTime GetDateTime() => new DateTime(Timestamp, DateTimeKind.Utc);
        public string slotName => SlotName;
        public string sceneName => SceneName;
        public string timestamp => GetDateTime().ToLocalTime().ToString("g");
        public float totalPlayTime => PlayTimeSeconds;
        public string version => GameVersion;

        public string GetFormattedPlayTime()
        {
            TimeSpan t = TimeSpan.FromSeconds(PlayTimeSeconds);
            return $"{(int)t.TotalHours:D2}:{t.Minutes:D2}:{t.Seconds:D2}";
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

            ES3Settings settings = new ES3Settings { compressionType = ES3.CompressionType.None };
            ES3.Save("meta", this, path, settings);
        }

        public static SaveMetadata Load(string slotName)
        {
            return LoadFromPath(GetPrimaryMetadataPath(slotName));
        }

        public static SaveMetadata LoadFromPath(string path)
        {
            if (string.IsNullOrEmpty(path) || !ES3.FileExists(path))
                return null;

            try
            {
                ES3Settings settings = new ES3Settings { compressionType = ES3.CompressionType.None };
                return ES3.Load<SaveMetadata>("meta", path, settings);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[SaveMetadata] Failed to load meta from '{path}': {ex.Message}");
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
            return !string.IsNullOrEmpty(path) && ES3.FileExists(path);
        }

        public static bool Delete(string path)
        {
            if (!ES3.FileExists(path))
                return false;

            ES3.DeleteFile(path);
            return true;
        }
    }
}
