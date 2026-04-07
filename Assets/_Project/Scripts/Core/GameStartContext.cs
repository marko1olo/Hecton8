// ============================================================================
// HECTON-8 — GameStartContext.cs
// Контекст инициализации игры при переходе из меню в мир.
//
// НАЗНАЧЕНИЕ:
//   • Единый контейнер для передачи параметров игровой сессии
//     между 00_BOOTSTRAP → 01_MAIN_MENU → 02_HECTON_WORLD
//   • Заменяет разбросанные static string TargetSaveSlot + PlayerPrefs
//   • Содержит стартовый режим, слот сохранения, режим спавна и контекст интро
//
// ВЛАДЕЛЕЦ СОСТОЯНИЯ:
//   • MainMenuController — пишет в GameStartContextHolder.Current при StartGame()
//   • SceneBootstrap — читает из GameStartContextHolder.Current в Start()
//
// ZERO-GC:
//   • Использует enum для startMode/spawnMode (no boxing)
//   • Нет new[] / new List / string allocations
//   • Serializable для Debug Inspector-инга
// ============================================================================

using System;
using UnityEngine;

namespace Hecton8.Core
{
    /// <summary>
    /// Режим инициализации игровой сессии.
    /// </summary>
    public enum GameStartMode
    {
        /// <summary>Новая игра с нуля.</summary>
        NewGame,

        /// <summary>Загрузка существующего сохранения.</summary>
        LoadGame,

        /// <summary>Повторное включение паузы (вернулись в меню и загружаемся обратно)</summary>
        Resume,
    }

    /// <summary>
    /// Режим размещения игрока при старте.
    /// </summary>
    public enum GameSpawnMode
    {
        /// <summary>Использовать сохраненную позицию (из сейва или конфига)</summary>
        SavedLocation,

        /// <summary>Использовать fallback позицию, если есть проблемы с сохранением</summary>
        FallbackLocation,

        /// <summary>Использовать позицию из IntroBootLoader (если есть prologue)</summary>
        IntroLocation,
    }

    /// <summary>
    /// Контекст запуска игры. Передается из меню в игровой мир.
    /// </summary>
    [Serializable]
    public struct GameStartContext
    {
        /// <summary>Режим инициализации (новая игра / загрузка / возобновление)</summary>
        public GameStartMode StartMode;

        /// <summary>Имя слота сохранения (пустой string = новая игра)</summary>
        public string TargetSaveSlot;

        /// <summary>Режим спавна игрока (сохраненная позиция / fallback)</summary>
        public GameSpawnMode SpawnMode;

        /// <summary>Имя сцены интро (пустой string = без интро)</summary>
        public string IntroSceneName;

        /// <summary>Пресет ландинга (пустой string = default)</summary>
        public string LandingPresetName;

        /// <summary>
        /// Возвращает значимый контекст (true если это реальная сессия, а не пустой struct).
        /// </summary>
        public readonly bool IsValid => !string.IsNullOrEmpty(TargetSaveSlot) || StartMode == GameStartMode.NewGame;

        /// <summary>
        /// Создает новую игровую сессию (NewGame).
        /// </summary>
        public static GameStartContext CreateNewGame(string landingPreset = "")
        {
            return new GameStartContext
            {
                StartMode = GameStartMode.NewGame,
                TargetSaveSlot = string.Empty,
                SpawnMode = GameSpawnMode.SavedLocation,
                IntroSceneName = string.Empty,
                LandingPresetName = landingPreset,
            };
        }

        /// <summary>
        /// Создает контекст загрузки существующей сессии (LoadGame).
        /// </summary>
        public static GameStartContext CreateLoadGame(
            string saveSlot,
            GameSpawnMode spawnMode = GameSpawnMode.SavedLocation)
        {
            return new GameStartContext
            {
                StartMode = GameStartMode.LoadGame,
                TargetSaveSlot = saveSlot ?? string.Empty,
                SpawnMode = spawnMode,
                IntroSceneName = string.Empty,
                LandingPresetName = string.Empty,
            };
        }

        /// <summary>
        /// Создает контекст возобновления паузы (Resume из меню).
        /// </summary>
        public static GameStartContext CreateResume(string saveSlot)
        {
            return new GameStartContext
            {
                StartMode = GameStartMode.Resume,
                TargetSaveSlot = saveSlot ?? string.Empty,
                SpawnMode = GameSpawnMode.SavedLocation,
                IntroSceneName = string.Empty,
                LandingPresetName = string.Empty,
            };
        }

        /// <summary>
        /// Вспомогательный метод для скопирования с модификацией одного поля.
        /// </summary>
        public readonly GameStartContext WithSpawnMode(GameSpawnMode newSpawnMode)
        {
            return new GameStartContext
            {
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
    /// Контейнер для передачи GameStartContext между сценами.
    /// Singleton, живет в памяти пока не используется.
    /// </summary>
    public static class GameStartContextHolder
    {
        /// <summary>Текущий контекст игровой сессии.</summary>
        public static GameStartContext Current { get; set; }

        /// <summary>
        /// Сбрасывает контекст (используется при выходе в главное меню).
        /// </summary>
        public static void Reset()
        {
            Current = default;
        }

        /// <summary>
        /// Логирует текущий контекст для отладки.
        /// </summary>
        public static void LogCurrent()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[GameStartContextHolder] {Current}");
#endif
        }
    }
}
