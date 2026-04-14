using System;
using System.Collections.Generic;
using UnityEngine;
using Hecton8.Input;

namespace Hecton.Localization
{
    /// <summary>
    /// Язык игры. Расширяется по мере добавления локализаций.
    /// </summary>
    public enum GameLanguage
    {
        English = 0,
        Russian = 1,
        // Chinese = 2,
        // Japanese = 3,
    }

    /// <summary>
    /// Менеджер локализации. Загружает таблицы переводов, 
    /// отдаёт строки по ключу, оповещает подписчиков при смене языка.
    /// 
    /// Singleton (DontDestroyOnLoad). Инициализируется до любого UI.
    /// </summary>
    public sealed class LocalizationManager : MonoBehaviour
    {
        // ──────────────────────────────────────────────
        // SINGLETON
        // ──────────────────────────────────────────────
        public static LocalizationManager Instance { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            Instance = null;
            OnLanguageChanged = null;
        }

        // ──────────────────────────────────────────────
        // EVENTS
        // ──────────────────────────────────────────────
        /// <summary>
        /// Вызывается при смене языка. Все UI-компоненты подписываются
        /// и обновляют свои тексты.
        /// </summary>
        public static event Action<GameLanguage> OnLanguageChanged;

        // ──────────────────────────────────────────────
        // STATE
        // ──────────────────────────────────────────────
        public GameLanguage CurrentLanguage { get; private set; } = GameLanguage.English;

        // Таблица: язык → (ключ → перевод)
        private readonly Dictionary<GameLanguage, Dictionary<string, string>> _tables =
            new Dictionary<GameLanguage, Dictionary<string, string>>();

        // ──────────────────────────────────────────────
        // INSPECTOR
        // ──────────────────────────────────────────────
        [Header("=== CONFIG ===")]
        [SerializeField] private GameLanguage defaultLanguage = GameLanguage.English;

        [Header("=== LOCALIZATION DATA (JSON TextAssets) ===")]
        [Tooltip("Каждый TextAsset — JSON-файл с парами key:value для одного языка. " +
                 "Имя файла должно совпадать с GameLanguage enum (English.json, Russian.json).")]
        [SerializeField] private TextAsset[] languageFiles;

        // Shared user-options key owned by UserOptionsPersistence.
        private const string PREFS_LANGUAGE_KEY = UserOptionsPersistence.LanguageKey;

        // ══════════════════════════════════════════════
        // LIFECYCLE
        // ══════════════════════════════════════════════

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            LoadAllTables();
            RestoreSavedLanguage();
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
                OnLanguageChanged = null;
            }
        }

        // ══════════════════════════════════════════════
        // PUBLIC API
        // ══════════════════════════════════════════════

        /// <summary>
        /// Возвращает локализованную строку по ключу.
        /// Если ключ не найден — возвращает сам ключ (для дебага).
        /// </summary>
        public string Get(string key)
        {
            if (string.IsNullOrEmpty(key))
                return string.Empty;

            if (_tables.TryGetValue(CurrentLanguage, out Dictionary<string, string> table))
            {
                if (table.TryGetValue(key, out string value))
                    return value;
            }

            // Fallback: попробовать English
            if (CurrentLanguage != GameLanguage.English &&
                _tables.TryGetValue(GameLanguage.English, out Dictionary<string, string> fallback))
            {
                if (fallback.TryGetValue(key, out string value))
                    return value;
            }

#if UNITY_EDITOR
            Debug.LogWarning($"[Localization] Missing key: \"{key}\" for {CurrentLanguage}");
#endif
            // Возвращаем ключ как есть — сразу видно что не переведено
            return key;
        }

        /// <summary>
        /// Возвращает локализованную строку с подстановкой аргументов.
        /// Использует string.Format: Get("modal.load.message") → "Load save \"{0}\"?"
        /// </summary>
        public string GetFormatted(string key, params object[] args)
        {
            string template = Get(key);

            if (args == null || args.Length == 0)
                return template;

            try
            {
                return string.Format(template, args);
            }
            catch (FormatException)
            {
#if UNITY_EDITOR
                Debug.LogError(
                    $"[Localization] Format error for key \"{key}\", " +
                    $"template: \"{template}\", args count: {args.Length}"
                );
#endif
                return template;
            }
        }

        /// <summary>
        /// Переключает язык и оповещает всех подписчиков.
        /// </summary>
        public void SetLanguage(GameLanguage language)
        {
            if (CurrentLanguage == language) return;

            CurrentLanguage = language;

            // Сохраняем выбор
            UserOptionsPersistence options = UserOptionsPersistence.Instance;
            options.SetInt(PREFS_LANGUAGE_KEY, (int)language);
            options.Save();

            // Оповещаем подписчиков
            OnLanguageChanged?.Invoke(language);

#if UNITY_EDITOR
            Debug.Log($"[Localization] Language changed to: {language}");
#endif
        }

        /// <summary>
        /// Переключает на следующий язык (для кнопки в настройках).
        /// </summary>
        public void CycleLanguage()
        {
            int count = Enum.GetValues(typeof(GameLanguage)).Length;
            int next = ((int)CurrentLanguage + 1) % count;
            SetLanguage((GameLanguage)next);
        }

        // ══════════════════════════════════════════════
        // ЗАГРУЗКА ТАБЛИЦ
        // ══════════════════════════════════════════════

        private void LoadAllTables()
        {
            _tables.Clear();

            if (languageFiles == null || languageFiles.Length == 0)
            {
                // Нет файлов — загружаем встроенные данные (hardcoded fallback)
                LoadBuiltInTables();
                return;
            }

            for (int i = 0; i < languageFiles.Length; i++)
            {
                TextAsset file = languageFiles[i];
                if (file == null) continue;

                // Имя файла = имя языка
                if (!Enum.TryParse(file.name, true, out GameLanguage lang))
                {
#if UNITY_EDITOR
                    Debug.LogWarning(
                        $"[Localization] Cannot parse language from filename: \"{file.name}\". " +
                        $"Expected: {string.Join(", ", Enum.GetNames(typeof(GameLanguage)))}"
                    );
#endif
                    continue;
                }

                Dictionary<string, string> table = ParseJsonTable(file.text);
                _tables[lang] = table;
            }

            // Если English не загрузился из файлов — добавляем встроенный
            if (!_tables.ContainsKey(GameLanguage.English))
            {
                LoadBuiltInTables();
            }
        }

        /// <summary>
        /// Парсит простой плоский JSON: { "key": "value", ... }
        /// Не требует Newtonsoft — работает через Unity JsonUtility обёртку.
        /// </summary>
        private static Dictionary<string, string> ParseJsonTable(string json)
        {
            var result = new Dictionary<string, string>(64);

            if (string.IsNullOrEmpty(json))
                return result;

            // Простой парсер для плоского JSON (ключ-значение)
            // Для продакшена заменить на Newtonsoft или Unity Localization Package
            try
            {
                // Убираем внешние скобки и разбиваем по строкам
                json = json.Trim();
                if (json.StartsWith("{")) json = json.Substring(1);
                if (json.EndsWith("}"))   json = json.Substring(0, json.Length - 1);

                string[] entries = json.Split(',');

                for (int i = 0; i < entries.Length; i++)
                {
                    string entry = entries[i].Trim();
                    if (string.IsNullOrEmpty(entry)) continue;

                    int colonIndex = entry.IndexOf(':');
                    if (colonIndex < 0) continue;

                    string key   = entry.Substring(0, colonIndex).Trim().Trim('"');
                    string value = entry.Substring(colonIndex + 1).Trim().Trim('"');

                    if (!string.IsNullOrEmpty(key))
                    {
                        result[key] = value;
                    }
                }
            }
            catch (Exception e)
            {
#if UNITY_EDITOR
                Debug.LogError($"[Localization] JSON parse error: {e.Message}");
#endif
            }

            return result;
        }

        /// <summary>
        /// Встроенные таблицы — гарантированный fallback,
        /// чтобы игра работала даже без JSON-файлов.
        /// </summary>
        private void LoadBuiltInTables()
        {
            // ── ENGLISH ──
            if (!_tables.ContainsKey(GameLanguage.English))
            {
                _tables[GameLanguage.English] = new Dictionary<string, string>(32)
                {
                    { LocalizationKeys.MENU_NEW_GAME,   "New Game" },
                    { LocalizationKeys.MENU_LOAD_GAME,  "Load Game" },
                    { LocalizationKeys.MENU_SETTINGS,   "Settings" },
                    { LocalizationKeys.MENU_QUIT,       "Quit" },

                    { LocalizationKeys.MODAL_CONFIRM,   "Confirm" },
                    { LocalizationKeys.MODAL_CANCEL,    "Cancel" },
                    { LocalizationKeys.MODAL_NEW_GAME_TITLE,   "New Game" },
                    { LocalizationKeys.MODAL_NEW_GAME_MESSAGE, "Start a new game?" },
                    { LocalizationKeys.MODAL_LOAD_TITLE,       "Load Game" },
                    { LocalizationKeys.MODAL_LOAD_MESSAGE,     "Load save \"{0}\"?" },
                    { LocalizationKeys.MODAL_QUIT_TITLE,       "Quit" },
                    { LocalizationKeys.MODAL_QUIT_MESSAGE,     "Quit the game?" },

                    { LocalizationKeys.SLOT_PREFIX,     "SLOT" },
                    { LocalizationKeys.SLOT_NO_DATA,    "NO DATA" },
                    { LocalizationKeys.SLOT_PLAYTIME,   "Playtime" },

                    { LocalizationKeys.LOADING_PERCENT, "{0}%" },
                };
            }

            // ── RUSSIAN ──
            if (!_tables.ContainsKey(GameLanguage.Russian))
            {
                _tables[GameLanguage.Russian] = new Dictionary<string, string>(32)
                {
                    { LocalizationKeys.MENU_NEW_GAME,   "\u041d\u043e\u0432\u0430\u044f \u0438\u0433\u0440\u0430" },
                    { LocalizationKeys.MENU_LOAD_GAME,  "\u0417\u0430\u0433\u0440\u0443\u0437\u0438\u0442\u044c" },
                    { LocalizationKeys.MENU_SETTINGS,   "\u041d\u0430\u0441\u0442\u0440\u043e\u0439\u043a\u0438" },
                    { LocalizationKeys.MENU_QUIT,       "\u0412\u044b\u0445\u043e\u0434" },

                    { LocalizationKeys.MODAL_CONFIRM,   "\u041f\u043e\u0434\u0442\u0432\u0435\u0440\u0434\u0438\u0442\u044c" },
                    { LocalizationKeys.MODAL_CANCEL,    "\u041e\u0442\u043c\u0435\u043d\u0430" },
                    { LocalizationKeys.MODAL_NEW_GAME_TITLE,   "\u041d\u043e\u0432\u0430\u044f \u0438\u0433\u0440\u0430" },
                    { LocalizationKeys.MODAL_NEW_GAME_MESSAGE, "\u041d\u0430\u0447\u0430\u0442\u044c \u043d\u043e\u0432\u0443\u044e \u0438\u0433\u0440\u0443?" },
                    { LocalizationKeys.MODAL_LOAD_TITLE,       "\u0417\u0430\u0433\u0440\u0443\u0437\u043a\u0430" },
                    { LocalizationKeys.MODAL_LOAD_MESSAGE,     "\u0417\u0430\u0433\u0440\u0443\u0437\u0438\u0442\u044c \u0441\u043e\u0445\u0440\u0430\u043d\u0435\u043d\u0438\u0435 \"{0}\"?" },
                    { LocalizationKeys.MODAL_QUIT_TITLE,       "\u0412\u044b\u0445\u043e\u0434" },
                    { LocalizationKeys.MODAL_QUIT_MESSAGE,     "\u0412\u044b\u0439\u0442\u0438 \u0438\u0437 \u0438\u0433\u0440\u044b?" },

                    { LocalizationKeys.SLOT_PREFIX,     "\u0421\u041b\u041e\u0422" },
                    { LocalizationKeys.SLOT_NO_DATA,    "\u041d\u0415\u0422 \u0414\u0410\u041d\u041d\u042b\u0425" },
                    { LocalizationKeys.SLOT_PLAYTIME,   "\u0412\u0440\u0435\u043c\u044f \u0438\u0433\u0440\u044b" },

                    { LocalizationKeys.LOADING_PERCENT, "{0}%" },
                };
            }
        }

        private void RestoreSavedLanguage()
        {
            UserOptionsPersistence options = UserOptionsPersistence.Instance;
            if (options.HasKey(PREFS_LANGUAGE_KEY))
            {
                int saved = options.GetInt(PREFS_LANGUAGE_KEY, (int)defaultLanguage);
                CurrentLanguage = (GameLanguage)saved;
                return;
            }

            CurrentLanguage = defaultLanguage;
        }
    }
}
