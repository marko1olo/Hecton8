namespace Hecton.Localization
{
    /// <summary>
    /// Централизованные ключи локализации. Никаких magic-strings в коде.
    /// Все UI-тексты ссылаются только на эти константы.
    /// </summary>
    public static class LocalizationKeys
    {
        // ── Main Menu ──
        public const string MENU_NEW_GAME   = "menu.new_game";
        public const string MENU_LOAD_GAME  = "menu.load_game";
        public const string MENU_SETTINGS   = "menu.settings";
        public const string MENU_QUIT       = "menu.quit";

        // ── Modal Window ──
        public const string MODAL_CONFIRM   = "modal.confirm";
        public const string MODAL_CANCEL    = "modal.cancel";
        public const string MODAL_NEW_GAME_TITLE   = "modal.new_game.title";
        public const string MODAL_NEW_GAME_MESSAGE = "modal.new_game.message";
        public const string MODAL_LOAD_TITLE       = "modal.load.title";
        public const string MODAL_LOAD_MESSAGE     = "modal.load.message";
        public const string MODAL_QUIT_TITLE       = "modal.quit.title";
        public const string MODAL_QUIT_MESSAGE     = "modal.quit.message";

        // ── Save Slots ──
        public const string SLOT_PREFIX     = "slot.prefix";
        public const string SLOT_NO_DATA    = "slot.no_data";
        public const string SLOT_PLAYTIME   = "slot.playtime";

        // ── Loading ──
        public const string LOADING_PERCENT = "loading.percent";
    }
}