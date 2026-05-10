// ============================================================================
// HECTON-8 — GameStartContext.cs
// Kontekst initsializatsii igry pri perehode iz menyu v mir.
//
// NAZNAChENIE:
//   • Edinyy konteyner dlya peredachi parametrov igrovoy sessii
//     mezhdu 00_BOOTSTRAP → 01_MAIN_MENU → 02_HECTON_WORLD
//   • Zamenyaet razbrosannye static string TargetSaveSlot + PlayerPrefs
//   • Soderzhit startovyy rezhim, slot sohraneniya, rezhim spavna i kontekst intro
//
// VLADELETs SOSTOYaNIYa:
//   • MainMenuController — pishet v GameStartContextHolder.Current pri StartGame()
//   • GameBootstrapper — chitaet iz GameStartContextHolder.Current v Start()
//
// ZERO-GC:
//   • Ispolzuet enum dlya startMode/spawnMode (no boxing)
//   • Net new[] / new List / string allocations
//   • Serializable dlya Debug Inspector-inga
// ============================================================================

using System;
using UnityEngine;

namespace Hecton8.Core
{
    /// <summary>
    /// Rezhim initsializatsii igrovoy sessii.
    /// </summary>
    public enum GameStartMode
    {
        /// <summary>Novaya igra s nulya.</summary>
        NewGame,

        /// <summary>Zagruzka suschestvuyuschego sohraneniya.</summary>
        LoadGame,

        /// <summary>Povtornoe vklyuchenie pauzy (vernulis v menyu i zagruzhaemsya obratno)</summary>
        Resume,
    }

    /// <summary>
    /// Rezhim razmescheniya igroka pri starte.
    /// </summary>
    public enum GameSpawnMode
    {
        /// <summary>Ispolzovat sohranennuyu pozitsiyu (iz seyva ili konfiga)</summary>
        SavedLocation,

        /// <summary>Ispolzovat fallback pozitsiyu, esli est problemy s sohraneniem</summary>
        FallbackLocation,

        /// <summary>Ispolzovat pozitsiyu iz IntroBootLoader (esli est prologue)</summary>
        IntroLocation,
    }

    /// <summary>
    /// Kontekst zapuska igry. Peredaetsya iz menyu v igrovoy mir.
    /// </summary>
    [Serializable]
    public struct GameStartContext
    {
        [SerializeField] private bool _isInitialized;

        /// <summary>Rezhim initsializatsii (novaya igra / zagruzka / vozobnovlenie)</summary>
        public GameStartMode StartMode;

        /// <summary>Imya slota sohraneniya (pustoy string = novaya igra)</summary>
        public string TargetSaveSlot;

        /// <summary>Rezhim spavna igroka (sohranennaya pozitsiya / fallback)</summary>
        public GameSpawnMode SpawnMode;

        /// <summary>Imya stseny intro (pustoy string = bez intro)</summary>
        public string IntroSceneName;

        /// <summary>Preset landinga (pustoy string = default)</summary>
        public string LandingPresetName;

        /// <summary>
        /// Vozvraschaet znachimyy kontekst (true esli eto realnaya sessiya, a ne pustoy struct).
        /// </summary>
        public readonly bool IsValid => _isInitialized && (!string.IsNullOrEmpty(TargetSaveSlot) || StartMode == GameStartMode.NewGame);

        /// <summary>
        /// Sozdaet novuyu igrovuyu sessiyu (NewGame).
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
        /// Sozdaet kontekst zagruzki suschestvuyuschey sessii (LoadGame).
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
        /// Sozdaet kontekst vozobnovleniya pauzy (Resume iz menyu).
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
        /// Vspomogatelnyy metod dlya skopirovaniya s modifikatsiey odnogo polya.
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
    /// Konteyner dlya peredachi GameStartContext mezhdu stsenami.
    /// Singleton, zhivet v pamyati poka ne ispolzuetsya.
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

        /// <summary>Tekuschiy kontekst igrovoy sessii.</summary>
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
        /// Sbrasyvaet kontekst (ispolzuetsya pri vyhode v glavnoe menyu).
        /// </summary>
        public static void Reset()
        {
            Current = default;
            ClearPersistedHandoff();
        }

        /// <summary>
        /// Logiruet tekuschiy kontekst dlya otladki.
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
