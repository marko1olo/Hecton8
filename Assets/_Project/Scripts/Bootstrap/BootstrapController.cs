// ============================================================================
// HECTON-8 — BootstrapController.cs
// Точка входа для инициализации глобальных систем.
//
// НАЗНАЧЕНИЕ:
//   • Явно инициализирует все требуемые менеджеры (zero lazy loading)
//   • Гарантирует DontDestroyOnLoad для всех систем
//   • Запрещает duplicate инициализации
//   • Обеспечивает гарантированный порядок инициализации
//
// ИНИЦИАЛИЗАЦИЯ (порядок важен):
//   1. Verify bootstrap is first scene in Build Settings
//   2. Create/Access GameTickManager (прерывает дальше если не найден)
//   3. Create/Access SaveManager
//   4. Create/Access InputManager
//   5. Create/Access ObjectPoolManager
//   6. Log successful bootstrap
//   7. Forward control to MainMenuController or SceneBootstrap
//
// ОРУЖИЕ ПРОТИВ БАГОВ:
//   • Если кто-то запустит 01_MAIN_MENU напрямую — сцена выведет ошибку
//   • Если кто-то запустит 02_HECTON_WORLD напрямую — сцена выведет ошибку
//   • Все менеджеры гарантированно существуют до начала gameplay
//
// ============================================================================

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Hecton8.Core;
using Hecton8.Input;
using Hecton8.SaveSystem;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Hecton8.Bootstrap
{
    /// <summary>
    /// Контроллер инициализации bootstrap сцены.
    /// Запускается только один раз в 00_BOOTSTRAP.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-30000)] // Раньше даже SceneBootstrap
    public sealed class BootstrapController : MonoBehaviour
    {
        // ══════════════════════════════════════════════════════════
        //  SINGLETON
        // ══════════════════════════════════════════════════════════

        private const string MainMenuSceneName = "01_MAIN_MENU";
        private const string GameTickManagerRuntimeName = "[GameTickManager]";
        private const string SaveManagerRuntimeName = "[SaveManager]";
        private const string ObjectPoolManagerRuntimeName = "[ObjectPoolManager]";
        private static BootstrapController _instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _instance = null;
        }

        public static BootstrapController Instance => _instance;

        // ══════════════════════════════════════════════════════════
        //  STATIC EVENTS
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Выстреливает когда все системы инициализированы.
        /// </summary>
        public static event System.Action OnBootstrapComplete;

        // ══════════════════════════════════════════════════════════
        //  PRIVATE STATE
        // ══════════════════════════════════════════════════════════

        private bool _initializationComplete;

        // ══════════════════════════════════════════════════════════
        //  LIFECYCLE
        // ══════════════════════════════════════════════════════════

        private void Awake()
        {
            // ── Проверка что это 00_BOOTSTRAP ──
            Scene currentScene = gameObject.scene;
            if (!currentScene.name.Contains("00_BOOTSTRAP"))
            {
                Debug.LogError(
                    $"[BootstrapController] Must be placed in 00_BOOTSTRAP scene, " +
                    $"but found in: {currentScene.name}");
                Destroy(gameObject);
                return;
            }

            // ── Singleton guard ──
            if (_instance != null && _instance != this)
            {
                Debug.LogWarning(
                    "[BootstrapController] Duplicate instance detected. Destroying.");
                Destroy(gameObject);
                return;
            }

            _instance = this;

            // ── DontDestroyOnLoad ──
            DontDestroyOnLoad(gameObject);

            // ── Инициализация всех систем ──
            Log("═══════════════════════════════════════════════");
            Log("HECTON-8 Bootstrap Controller — Initializing");
            Log("═══════════════════════════════════════════════");

            InitializeGlobalSystems();

            _initializationComplete = true;

            Log("═══════════════════════════════════════════════");
            Log("HECTON-8 Bootstrap Controller — COMPLETE");
            Log("═══════════════════════════════════════════════");

            OnBootstrapComplete?.Invoke();
        }

        // ══════════════════════════════════════════════════════════
        //  INITIALIZATION
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Инициализирует все требуемые глобальные системы в правильном порядке.
        /// </summary>
        private void Start()
        {
            if (!_initializationComplete || !Application.isPlaying)
                return;

            SceneManager.LoadScene(MainMenuSceneName);
        }

        private void InitializeGlobalSystems()
        {
            // ── Проверка Build Settings ──
            VerifyBootstrapIsFirstScene();

            // ── Game Tick Manager (должен быть первым) ──
            Log("[1/4] Initializing GameTickManager...");
            GameTickManager gameTickManager = EnsureGameTickManager();
            if (gameTickManager == null)
            {
                LogError("GameTickManager.Instance is null after access!");
                return;
            }
            EnsureDontDestroyOnLoad(gameTickManager.gameObject);
            Log("  ✓ GameTickManager initialized");

            // ── Save Manager ──
            Log("[2/4] Initializing SaveManager...");
            SaveManager saveManager = EnsureSaveManager();
            if (saveManager == null)
            {
                LogError("SaveManager.Instance is null after access!");
                return;
            }
            EnsureDontDestroyOnLoad(saveManager.gameObject);
            Log("  ✓ SaveManager initialized");

            // ── Input Manager ──
            Log("[3/4] Initializing InputManager...");
            if (InputManager.Instance == null)
            {
                LogWarning("InputManager.Instance is null. Gameplay input will be unavailable.");
            }
            else
            {
                EnsureDontDestroyOnLoad(InputManager.Instance.gameObject);
                Log("  ✓ InputManager initialized");
            }

            // ── Object Pool Manager ──
            Log("[4/4] Initializing ObjectPoolManager...");
            ObjectPoolManager objectPoolManager = EnsureObjectPoolManager();
            if (objectPoolManager == null)
            {
                LogError("ObjectPoolManager.Instance is null after access!");
                return;
            }
            EnsureDontDestroyOnLoad(objectPoolManager.gameObject);
            Log("  ✓ ObjectPoolManager initialized");

            Log("All systems initialized successfully.");
        }

        /// <summary>
        /// Убеждается что 00_BOOTSTRAP — это первая сцена в Build Settings.
        /// </summary>
        private void VerifyBootstrapIsFirstScene()
        {
#if UNITY_EDITOR
            if (EditorBuildSettings.scenes.Length == 0)
            {
                LogError("No scenes in Build Settings! Add 00_BOOTSTRAP as the first scene.");
                return;
            }

            string firstScenePath = EditorBuildSettings.scenes[0].path;
            if (!firstScenePath.Contains("00_BOOTSTRAP"))
            {
                LogError(
                    $"CRITICAL: First scene in Build Settings is '{firstScenePath}', " +
                    $"but should be 00_BOOTSTRAP. This breaks the architecture!");
            }
#else
            // In runtime, we can't check Build Settings, but we can at least verify
            // that the active scene is 00_BOOTSTRAP
            Scene activeScene = SceneManager.GetActiveScene();
            if (!activeScene.name.Contains("00_BOOTSTRAP"))
            {
                LogWarning(
                    $"Active scene is '{activeScene.name}', not 00_BOOTSTRAP. " +
                    $"Ensure it's the first scene in Build Settings.");
            }
#endif
        }

        /// <summary>
        /// Убеждается что GameObject не дублируется на другую сцену.
        /// </summary>
        private static void EnsureDontDestroyOnLoad(GameObject obj)
        {
            if (obj == null) return;

            Scene objScene = obj.scene;
            if (objScene.name == "DontDestroyOnLoad")
                return; // Уже там

            DontDestroyOnLoad(obj);
        }

        private static GameTickManager EnsureGameTickManager()
        {
            GameTickManager manager = GameTickManager.Instance;
            if (manager != null)
                return manager;

            // COLD ALLOC: bootstrap fallback singleton root when scene authoring omitted manager.
            GameObject go = new GameObject(GameTickManagerRuntimeName);
            return go.AddComponent<GameTickManager>();
        }

        private static SaveManager EnsureSaveManager()
        {
            SaveManager manager = SaveManager.Instance;
            if (manager != null)
                return manager;

            // COLD ALLOC: bootstrap fallback singleton root when scene authoring omitted manager.
            GameObject go = new GameObject(SaveManagerRuntimeName);
            return go.AddComponent<SaveManager>();
        }

        private static ObjectPoolManager EnsureObjectPoolManager()
        {
            ObjectPoolManager manager = ObjectPoolManager.Instance;
            if (manager != null)
                return manager;

            // COLD ALLOC: bootstrap fallback singleton root when scene authoring omitted manager.
            GameObject go = new GameObject(ObjectPoolManagerRuntimeName);
            return go.AddComponent<ObjectPoolManager>();
        }

        // ══════════════════════════════════════════════════════════
        //  DEBUG
        // ══════════════════════════════════════════════════════════

        private static void Log(string message)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[BootstrapController] {message}");
#endif
        }

        private static void LogWarning(string message)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogWarning($"[BootstrapController] ⚠️ {message}");
#endif
        }

        private static void LogError(string message)
        {
            Debug.LogError($"[BootstrapController] ❌ {message}");
        }

        // ══════════════════════════════════════════════════════════
        //  PUBLIC QUERY
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Проверяет готовность всех систем.
        /// </summary>
        public static bool AreAllSystemsReady()
        {
            return _instance != null && _instance._initializationComplete;
        }
    }
}
