// ============================================================================
// HECTON-8 — SaveEvents.cs
// Статический Event Bus для системы сохранений.
// UI, HUD и другие системы подписываются здесь.
// ============================================================================

using System;

namespace Hecton8.SaveSystem
{
    public static class SaveEvents
    {
        /// <summary>Сохранение началось. Param: имя слота.</summary>
        public static event Action<string> OnSaveStarted;

        /// <summary>Сохранение завершено успешно. Param: имя слота.</summary>
        public static event Action<string> OnSaveCompleted;

        /// <summary>Ошибка сохранения. Params: слот, сообщение ошибки.</summary>
        public static event Action<string, string> OnSaveFailed;

        /// <summary>Загрузка началась. Param: имя слота.</summary>
        public static event Action<string> OnLoadStarted;

        /// <summary>Загрузка завершена успешно. Param: имя слота.</summary>
        public static event Action<string> OnLoadCompleted;

        /// <summary>Ошибка загрузки. Params: слот, сообщение ошибки.</summary>
        public static event Action<string, string> OnLoadFailed;

        // ── Raise Methods ──

        public static void RaiseSaveStarted(string slot)
        {
            var h = OnSaveStarted; h?.Invoke(slot);
        }

        public static void RaiseSaveCompleted(string slot)
        {
            var h = OnSaveCompleted; h?.Invoke(slot);
        }

        public static void RaiseSaveFailed(string slot, string error)
        {
            var h = OnSaveFailed; h?.Invoke(slot, error);
        }

        public static void RaiseLoadStarted(string slot)
        {
            var h = OnLoadStarted; h?.Invoke(slot);
        }

        public static void RaiseLoadCompleted(string slot)
        {
            var h = OnLoadCompleted; h?.Invoke(slot);
        }

        public static void RaiseLoadFailed(string slot, string error)
        {
            var h = OnLoadFailed; h?.Invoke(slot, error);
        }
    }
}