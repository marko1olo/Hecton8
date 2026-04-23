using Hecton8.Core;
using Hecton8.Gameplay;
using Hecton8.Interaction;
using Hecton8.Input;
using Hecton8.Physics;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Hecton8.Bootstrap
{
    /// <summary>
    /// Deterministic bootstrap owner for the GlobalRegistry core and guarded scene routing.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-29980)]
    public sealed class GameBootstrapper : MonoBehaviour
    {
        private const string BootstrapSceneName = "00_BOOTSTRAP";
        private const string BiosErrorMessageTemplate =
            "BIOS ERROR 0xBOOT\nEXPECTED: 00_BOOTSTRAP [0]\nDETECTED: {0} [{1}]\nACTION: FORCED RECOVERY";

        private static GameBootstrapper _instance;
        private static bool _isBootstrapComplete;
        private static bool _sceneGuardRegistered;
        private static bool _entryRecoveryIssued;

        /// <summary>
        /// True once the bootstrap core finished its ordered initialization phases.
        /// </summary>
        public static bool IsBootstrapComplete => _isBootstrapComplete;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _instance = null;
            _isBootstrapComplete = false;
            _entryRecoveryIssued = false;

            if (_sceneGuardRegistered)
            {
                SceneManager.sceneLoaded -= HandleSceneLoadedGuard;
                _sceneGuardRegistered = false;
            }

            BootstrapBiosErrorOverlay.Hide();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void GuardInitialSceneEntry()
        {
            if (!Application.isPlaying || _isBootstrapComplete)
                return;

            TryRecoverEntryVector(SceneManager.GetActiveScene(), true);
        }

        /// <summary>
        /// Ensures a runtime bootstrap owner exists on the current bootstrap shell object.
        /// </summary>
        /// <param name="owner">Bootstrap shell owner.</param>
        /// <returns>Live bootstrap component.</returns>
        public static GameBootstrapper EnsureRuntimeInstance(GameObject owner)
        {
            if (owner == null)
                return null;

            if (!owner.TryGetComponent(out GameBootstrapper bootstrapper))
                bootstrapper = owner.AddComponent<GameBootstrapper>(); // COLD ALLOC: GameBootstrapper[1] - deterministic bootstrap owner on 00_BOOTSTRAP shell - owner: BootstrapController

            return bootstrapper;
        }

        /// <summary>
        /// Executes the ordered bootstrap phases once.
        /// </summary>
        public void InitializeBootstrap()
        {
            if (_isBootstrapComplete)
                return;

            if (!TryRecoverEntryVector(SceneManager.GetActiveScene(), false))
                return;

            RegisterSceneLoadGuard();

            InitializeCoreLayer();
            InitializeEnvironmentLayer();
            InitializePlayerLayer();
            InitializeUILayer();

            _isBootstrapComplete = true;
            BootstrapBiosErrorOverlay.Hide();
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(this);
                return;
            }

            _instance = this;
        }

        private void OnDestroy()
        {
            if (_instance == this)
                _instance = null;
        }

        private void InitializeCoreLayer()
        {
            SystemDispatcher.EnsureRuntimeInstance();
            SceneRuntimeService sceneRuntimeService = SceneRuntimeService.EnsureRuntimeInstance();
            EquipmentInteractionHandler interactionHandler = EquipmentInteractionHandler.EnsureRuntimeInstance();
            sceneRuntimeService.InitializeService();
            interactionHandler.InitializeService();
        }

        private void InitializeEnvironmentLayer()
        {
            GlobalPhysicsStateManager.EnsureRuntimeInstance();
            PhysicsApplySystem physicsApplySystem = PhysicsApplySystem.EnsureRuntimeInstance();
            DebrisManager debrisManager = DebrisManager.EnsureRuntimeInstance();
            physicsApplySystem.InitializeService();
            debrisManager.InitializeService();
        }

        private void InitializePlayerLayer()
        {
            InputManager inputManager = InputManager.Instance;
            if (inputManager != null && Application.isPlaying)
                DontDestroyOnLoad(inputManager.gameObject);

            InputDispatcher inputDispatcher = InputDispatcher.EnsureRuntimeInstance();
            inputDispatcher.InitializeService();
        }

        private void InitializeUILayer()
        {
            // No UI-layer GlobalRegistry adapter exists yet.
            // Existing menu/HUD ownership remains on scene-authored controllers.
        }

        private static void RegisterSceneLoadGuard()
        {
            if (_sceneGuardRegistered)
                return;

            SceneManager.sceneLoaded += HandleSceneLoadedGuard;
            _sceneGuardRegistered = true;
        }

        private static void HandleSceneLoadedGuard(Scene scene, LoadSceneMode mode)
        {
            if (!Application.isPlaying)
                return;

            if (_isBootstrapComplete)
            {
                BootstrapBiosErrorOverlay.Hide();
                return;
            }

            TryRecoverEntryVector(scene, true);
        }

        private static bool TryRecoverEntryVector(Scene scene, bool allowRecovery)
        {
            if (IsBootstrapScene(scene))
            {
                _entryRecoveryIssued = false;
                BootstrapBiosErrorOverlay.Hide();
                return true;
            }

            string message = string.Format(
                BiosErrorMessageTemplate,
                string.IsNullOrEmpty(scene.name) ? "<unnamed>" : scene.name,
                scene.buildIndex);

            BootstrapBiosErrorOverlay.Show(message);

            if (!allowRecovery || _entryRecoveryIssued)
                return false;

            _entryRecoveryIssued = true;
            GameStartContextHolder.Reset();
            SceneManager.LoadScene(BootstrapSceneName);
            return false;
        }

        private static bool IsBootstrapScene(Scene scene)
        {
            return scene.IsValid() &&
                   scene.buildIndex == 0 &&
                   string.Equals(scene.name, BootstrapSceneName, System.StringComparison.Ordinal);
        }
    }

    [DisallowMultipleComponent]
    internal sealed class BootstrapBiosErrorOverlay : MonoBehaviour
    {
        private const string OverlayRootName = "[Bootstrap BIOS ERROR]";
        private const int OverlaySortingOrder = 32767;

        private static BootstrapBiosErrorOverlay _instance;

        private Text _messageText;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _instance = null;
        }

        internal static void Show(string message)
        {
            BootstrapBiosErrorOverlay overlay = EnsureInstance();
            if (overlay == null)
                return;

            overlay.ApplyMessage(message);
        }

        internal static void Hide()
        {
            if (_instance == null)
                return;

            GameObject root = _instance.gameObject;
            _instance = null;

            if (root == null)
                return;

            if (Application.isPlaying)
                Destroy(root);
            else
                DestroyImmediate(root);
        }

        private static BootstrapBiosErrorOverlay EnsureInstance()
        {
            if (_instance != null)
                return _instance;

            GameObject runtimeRoot = new GameObject(OverlayRootName); // COLD ALLOC: GameObject[1] - bootstrap BIOS violation overlay root - owner: BootstrapBiosErrorOverlay
            BootstrapBiosErrorOverlay overlay = runtimeRoot.AddComponent<BootstrapBiosErrorOverlay>();
            return overlay;
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;

            if (Application.isPlaying)
                DontDestroyOnLoad(gameObject);

            BuildVisualTree();
        }

        private void ApplyMessage(string message)
        {
            if (_messageText == null)
                BuildVisualTree();

            if (_messageText == null)
                return;

            _messageText.text = message;
        }

        private void BuildVisualTree()
        {
            if (_messageText != null)
                return;

            Canvas canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = OverlaySortingOrder;

            gameObject.AddComponent<CanvasScaler>();
            gameObject.AddComponent<GraphicRaycaster>();

            Image background = gameObject.AddComponent<Image>();
            background.color = new Color(0.01f, 0.01f, 0.01f, 0.96f);

            GameObject textRoot = new GameObject("Message"); // COLD ALLOC: GameObject[1] - bootstrap BIOS overlay message node - owner: BootstrapBiosErrorOverlay
            textRoot.transform.SetParent(transform, false);

            RectTransform rectTransform = textRoot.AddComponent<RectTransform>();
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = new Vector2(72f, 72f);
            rectTransform.offsetMax = new Vector2(-72f, -72f);

            Text text = textRoot.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontStyle = FontStyle.Bold;
            text.fontSize = 28;
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.color = new Color(1f, 0.25f, 0.25f, 1f);
            text.supportRichText = false;
            text.raycastTarget = false;

            _messageText = text;
        }
    }
}
