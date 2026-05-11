// ============================================================================
// HECTON-8 - GameStartContext.cs
// Game session initialization context passed from menu to world.
//
// PURPOSE:
//   - Single container for game-session parameters passed between
//     00_BOOTSTRAP -> 01_MAIN_MENU -> 02_HECTON_WORLD.
//   - Replaces scattered static TargetSaveSlot strings and PlayerPrefs handoff.
//   - Stores start mode, save slot, spawn mode, and intro context.
//
// STATE OWNER:
//   - MainMenuController writes GameStartContextHolder.Current during StartGame().
//   - GameBootstrapper reads GameStartContextHolder.Current during Start().
//
// ZERO-GC:
//   - Uses enums for startMode/spawnMode; no boxing.
//   - No new[] / new List / string allocations.
//   - Serializable for Debug Inspector visibility.
// ============================================================================

using System;
using UnityEngine;

namespace Hecton8.Core
{
    /// <summary>
    /// Game session initialization mode.
    /// </summary>
    public enum GameStartMode
    {
        /// <summary>Start a new session from scratch.</summary>
        NewGame,

        /// <summary>Load an existing save slot.</summary>
        LoadGame,

        /// <summary>Resume after returning through the menu.</summary>
        Resume,
    }

    /// <summary>
    /// Player placement mode during startup.
    /// </summary>
    public enum GameSpawnMode
    {
        /// <summary>Use the saved position from the save or startup config.</summary>
        SavedLocation,

        /// <summary>Use the fallback position when saved placement is unavailable.</summary>
        FallbackLocation,

        /// <summary>Use the IntroBootLoader position when a prologue handoff exists.</summary>
        IntroLocation,
    }

    /// <summary>
    /// Game startup context passed from menu to world.
    /// </summary>
    [Serializable]
    public struct GameStartContext
    {
        [SerializeField] private bool _isInitialized;

        /// <summary>Session initialization mode: new, load, or resume.</summary>
        public GameStartMode StartMode;

        /// <summary>Save slot name; empty means a new game.</summary>
        public string TargetSaveSlot;

        /// <summary>Player spawn mode: saved position or fallback.</summary>
        public GameSpawnMode SpawnMode;

        /// <summary>Intro scene name; empty means no intro.</summary>
        public string IntroSceneName;

        /// <summary>Landing preset; empty means default.</summary>
        public string LandingPresetName;

        /// <summary>
        /// Returns true when this is a real session context, not an empty struct.
        /// </summary>
        public readonly bool IsValid => _isInitialized && (!string.IsNullOrEmpty(TargetSaveSlot) || StartMode == GameStartMode.NewGame);

        /// <summary>
        /// Creates a NewGame session context.
        /// </summary>
        public static GameStartContext CreateNewGame(string landingPreset = "")
        {
            return new GameStartContext
            {
                _isInitialized = true,
                StartMode = GameStartMode.NewGame,
                TargetSaveSlot = string.Empty,
                SpawnMode = GameSpawnMode.SavedLocation,
                IntroSceneName = string.Empty,
                LandingPresetName = landingPreset,
            };
        }

        /// <summary>
        /// Creates a LoadGame context for an existing session.
        /// </summary>
        public static GameStartContext CreateLoadGame(
            string saveSlot,
            GameSpawnMode spawnMode = GameSpawnMode.SavedLocation)
        {
            return new GameStartContext
            {
                _isInitialized = true,
                StartMode = GameStartMode.LoadGame,
                TargetSaveSlot = saveSlot ?? string.Empty,
                SpawnMode = spawnMode,
                IntroSceneName = string.Empty,
                LandingPresetName = string.Empty,
            };
        }

        /// <summary>
        /// Creates a Resume context after returning through the menu.
        /// </summary>
        public static GameStartContext CreateResume(string saveSlot)
        {
            return new GameStartContext
            {
                _isInitialized = true,
                StartMode = GameStartMode.Resume,
                TargetSaveSlot = saveSlot ?? string.Empty,
                SpawnMode = GameSpawnMode.SavedLocation,
                IntroSceneName = string.Empty,
                LandingPresetName = string.Empty,
            };
        }

        internal static GameStartContext CreateRestored(
            GameStartMode startMode,
            string targetSaveSlot,
            GameSpawnMode spawnMode,
            string introSceneName,
            string landingPresetName)
        {
            return new GameStartContext
            {
                _isInitialized = true,
                StartMode = startMode,
                TargetSaveSlot = targetSaveSlot ?? string.Empty,
                SpawnMode = spawnMode,
                IntroSceneName = introSceneName ?? string.Empty,
                LandingPresetName = landingPresetName ?? string.Empty,
            };
        }

        /// <summary>
        /// Copies the context while changing only the spawn mode.
        /// </summary>
        public readonly GameStartContext WithSpawnMode(GameSpawnMode newSpawnMode)
        {
            return new GameStartContext
            {
                _isInitialized = this._isInitialized,
                StartMode = this.StartMode,
                TargetSaveSlot = this.TargetSaveSlot,
                SpawnMode = newSpawnMode,
                IntroSceneName = this.IntroSceneName,
                LandingPresetName = this.LandingPresetName,
            };
        }

        public override string ToString()
        {
            return $"[GameStartContext] Mode={StartMode}, Slot={TargetSaveSlot}, " +
                   $"Spawn={SpawnMode}, Intro={IntroSceneName}, Preset={LandingPresetName}";
        }
    }

    /// <summary>
    /// Container that carries GameStartContext between scenes.
    /// Static handoff only; it lives in memory until consumed or reset.
    /// </summary>
    public static class GameStartContextHolder
    {
        private const string PersistKeyValid = "GameStartContext.Valid";
        private const string PersistKeyStartMode = "GameStartContext.StartMode";
        private const string PersistKeyTargetSaveSlot = "GameStartContext.TargetSaveSlot";
        private const string PersistKeySpawnMode = "GameStartContext.SpawnMode";
        private const string PersistKeyIntroSceneName = "GameStartContext.IntroSceneName";
        private const string PersistKeyLandingPresetName = "GameStartContext.LandingPresetName";
        private const string PersistKeyIssuedAtUtcTicks = "GameStartContext.IssuedAtUtcTicks";
        private const double PersistedHandoffMaxAgeSeconds = 45d;

        /// <summary>Current game session context.</summary>
        public static GameStartContext Current { get; set; }

        /// <summary>
        /// Stores the active handoff context in memory and in a cold persistence
        /// slot so bootstrap can recover from domain reload during scene transit.
        /// </summary>
        public static void SetCurrent(GameStartContext context)
        {
            Current = context;
            PersistCurrentContext();
        }

        /// <summary>
        /// Returns the current in-memory context or restores one cold persisted
        /// handoff snapshot if the static holder was wiped during scene transit.
        /// </summary>
        public static bool TryGetCurrentOrRestore(out GameStartContext context)
        {
            context = Current;
            if (context.IsValid)
                return true;

            return TryRestorePersistedContext(out context);
        }

        /// <summary>
        /// Clears only the cold persisted handoff snapshot after bootstrap has
        /// consumed it, leaving the in-memory runtime context intact.
        /// </summary>
        public static void ClearPersistedHandoff()
        {
            PlayerPrefs.DeleteKey(PersistKeyValid);
            PlayerPrefs.DeleteKey(PersistKeyStartMode);
            PlayerPrefs.DeleteKey(PersistKeyTargetSaveSlot);
            PlayerPrefs.DeleteKey(PersistKeySpawnMode);
            PlayerPrefs.DeleteKey(PersistKeyIntroSceneName);
            PlayerPrefs.DeleteKey(PersistKeyLandingPresetName);
            PlayerPrefs.DeleteKey(PersistKeyIssuedAtUtcTicks);
            PlayerPrefs.Save();
        }

        /// <summary>
        /// Resets context when returning to the main menu.
        /// </summary>
        public static void Reset()
        {
            Current = default;
            ClearPersistedHandoff();
        }

        /// <summary>
        /// Logs the current context for diagnostics.
        /// </summary>
        public static void LogCurrent()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Hecton8.Dev.RuntimeDiagnosticsTrace.WriteEvent("game-start", Current.ToString());
#endif
        }

        private static void PersistCurrentContext()
        {
            if (!Current.IsValid)
            {
                ClearPersistedHandoff();
                return;
            }

            PlayerPrefs.SetInt(PersistKeyValid, 1);
            PlayerPrefs.SetInt(PersistKeyStartMode, (int)Current.StartMode);
            PlayerPrefs.SetString(PersistKeyTargetSaveSlot, Current.TargetSaveSlot ?? string.Empty);
            PlayerPrefs.SetInt(PersistKeySpawnMode, (int)Current.SpawnMode);
            PlayerPrefs.SetString(PersistKeyIntroSceneName, Current.IntroSceneName ?? string.Empty);
            PlayerPrefs.SetString(PersistKeyLandingPresetName, Current.LandingPresetName ?? string.Empty);
            PlayerPrefs.SetString(PersistKeyIssuedAtUtcTicks, DateTime.UtcNow.Ticks.ToString());
            PlayerPrefs.Save();
        }

        private static bool TryRestorePersistedContext(out GameStartContext context)
        {
            context = default;

            if (PlayerPrefs.GetInt(PersistKeyValid, 0) == 0)
                return false;

            int startModeValue = PlayerPrefs.GetInt(PersistKeyStartMode, -1);
            int spawnModeValue = PlayerPrefs.GetInt(PersistKeySpawnMode, -1);
            string issuedAtUtcTicksRaw = PlayerPrefs.GetString(PersistKeyIssuedAtUtcTicks, string.Empty);

            if (!long.TryParse(issuedAtUtcTicksRaw, out long issuedAtUtcTicks))
            {
                ClearPersistedHandoff();
                return false;
            }

            long handoffAgeTicks = DateTime.UtcNow.Ticks - issuedAtUtcTicks;
            if (handoffAgeTicks < 0L ||
                handoffAgeTicks > TimeSpan.FromSeconds(PersistedHandoffMaxAgeSeconds).Ticks)
            {
                ClearPersistedHandoff();
                return false;
            }

            if ((uint)startModeValue > (uint)GameStartMode.Resume ||
                (uint)spawnModeValue > (uint)GameSpawnMode.IntroLocation)
            {
                ClearPersistedHandoff();
                return false;
            }

            context = GameStartContext.CreateRestored(
                (GameStartMode)startModeValue,
                PlayerPrefs.GetString(PersistKeyTargetSaveSlot, string.Empty),
                (GameSpawnMode)spawnModeValue,
                PlayerPrefs.GetString(PersistKeyIntroSceneName, string.Empty),
                PlayerPrefs.GetString(PersistKeyLandingPresetName, string.Empty));

            if (!context.IsValid)
            {
                ClearPersistedHandoff();
                context = default;
                return false;
            }

            Current = context;
            return true;
        }
    }
}
