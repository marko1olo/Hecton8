using System;
using System.IO;
using System.Text;
using Hecton8.Core;
using Hecton8.Gameplay;
using Hecton8.Interaction;
using Hecton8.Input;
using Hecton8.Optimization;
using Hecton8.Physics;
using Hecton8.SaveSystem;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
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
        private const string FatalBootCrashFileName = "fatal_boot_crash.log";
        private const string FatalBootOverlayMessageTemplate =
            "BIOS ERROR 0xBOOT_FATAL\nPHASE: {0}\nACTION: SEE fatal_boot_crash.log";
        private const int FatalBootCrashLogBufferBytes = 24576;
        private const string BiosErrorMessageTemplate =
            "BIOS ERROR 0xBOOT\nEXPECTED: 00_BOOTSTRAP [0]\nDETECTED: {0} [{1}]\nACTION: FORCED RECOVERY";
        private static readonly UTF8Encoding _fatalBootCrashEncoding = new UTF8Encoding(false);

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

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void GuardEntryVectorBeforeSceneLoad()
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
        public bool InitializeBootstrap()
        {
            if (_isBootstrapComplete)
                return true;

            BootstrapStatus.BeginBoot();
            try
            {
                if (!TryRecoverEntryVector(SceneManager.GetActiveScene(), false))
                    return false;

                RegisterSceneLoadGuard();

                if (!TryRunBootstrapStep(BootstrapStepToken.Core, "Core", InitializeCoreLayer))
                    return false;

                if (!TryRunBootstrapStep(BootstrapStepToken.Environment, "Environment", InitializeEnvironmentLayer))
                    return false;

                if (!TryRunBootstrapStep(BootstrapStepToken.Player, "Player", InitializePlayerLayer))
                    return false;

                if (!TryRunBootstrapStep(BootstrapStepToken.UI, "UI", InitializeUILayer))
                    return false;

                _isBootstrapComplete = true;
                BootstrapBiosErrorOverlay.Hide();
                return true;
            }
            catch (Exception exception)
            {
                HandleFatalBootstrapException("BootstrapEntry", exception);
                return false;
            }
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
            VRAMEnforcer.InitializeRuntimeBudget();
            EnsureSystemDispatcherRegistered();
            EnsureRenderDispatcherRegistered();
            SceneInstantiationGate.EnsureRuntimeInstance();
            SceneRuntimeService sceneRuntimeService = SceneRuntimeService.EnsureRuntimeInstance();
            EquipmentInteractionHandler interactionHandler = EquipmentInteractionHandler.EnsureRuntimeInstance();
            sceneRuntimeService.InitializeService();
            interactionHandler.InitializeService();
        }

        private void InitializeEnvironmentLayer()
        {
            EnsureGlobalPhysicsStateManagerRegistered();
            PhysicsApplySystem physicsApplySystem = PhysicsApplySystem.EnsureRuntimeInstance();
            DebrisManager debrisManager = DebrisManager.EnsureRuntimeInstance();
            EnvironmentRuntimeContextService environmentContextService = EnvironmentRuntimeContextService.EnsureRuntimeInstance();
            OceanKinematicsRuntimeService oceanKinematicsRuntimeService = OceanKinematicsRuntimeService.EnsureRuntimeInstance();
            physicsApplySystem.InitializeService();
            debrisManager.InitializeService();
            environmentContextService.InitializeService();
            oceanKinematicsRuntimeService.InitializeService();
        }

        private bool InitializePlayerLayer()
        {
            if (!InputManager.TryValidateRuntimeConfiguration(out string inputConfigurationError))
            {
                BootstrapBiosErrorOverlay.Show(inputConfigurationError);
                return false;
            }

            InputManager inputManager = InputManager.Instance;
            if (inputManager == null)
            {
                BootstrapBiosErrorOverlay.Show(
                    "BIOS ERROR 0xINPUT\nEXPECTED: Runtime InputManager instance\nDETECTED: InputManager.Instance returned null\nACTION: Repair the bootstrap input owner before boot.");
                return false;
            }

            if (!inputManager.TryValidateRuntimeActions(out string inputActionsError))
            {
                BootstrapBiosErrorOverlay.Show(inputActionsError);
                return false;
            }

            if (Application.isPlaying)
                DontDestroyOnLoad(inputManager.gameObject);

            InputDispatcher inputDispatcher = InputDispatcher.EnsureRuntimeInstance();
            PlayerRuntimeContextService playerContextService = PlayerRuntimeContextService.EnsureRuntimeInstance();
            PlayerInventoryManager playerInventoryManager = PlayerInventoryManager.EnsureRuntimeInstance();
            PlayerSensoryManager playerSensoryManager = PlayerSensoryManager.EnsureRuntimeInstance();
            ContextualPhysicalIkRuntime.EnsureRuntimeInstance();
            inputDispatcher.InitializeService();
            playerContextService.InitializeService();
            playerInventoryManager.InitializeService();
            playerSensoryManager.InitializeService();
            return true;
        }

        private void InitializeUILayer()
        {
            // No UI-layer GlobalRegistry adapter exists yet.
            // Existing menu/HUD ownership remains on scene-authored controllers.
        }

        private static SystemDispatcher EnsureSystemDispatcherRegistered()
        {
            if (GlobalRegistry.Dispatcher != null)
                return GlobalRegistry.Dispatcher;

            GameObject runtimeRoot = new GameObject("[SystemDispatcher]"); // COLD ALLOC: GameObject[1] - bootstrap-owned gameplay dispatcher root - owner: GameBootstrapper
            return runtimeRoot.AddComponent<SystemDispatcher>();
        }

        private static RenderDispatcher EnsureRenderDispatcherRegistered()
        {
            if (GlobalRegistry.RenderDispatcher != null)
                return GlobalRegistry.RenderDispatcher;

            GameObject runtimeRoot = new GameObject("[RenderDispatcher]"); // COLD ALLOC: GameObject[1] - bootstrap-owned SRP render dispatcher root - owner: GameBootstrapper
            return runtimeRoot.AddComponent<RenderDispatcher>();
        }

        private static GlobalPhysicsStateManager EnsureGlobalPhysicsStateManagerRegistered()
        {
            if (GlobalRegistry.PhysicsStateManager != null)
                return GlobalRegistry.PhysicsStateManager;

            GameObject runtimeRoot = new GameObject("[GlobalPhysicsStateManager]"); // COLD ALLOC: GameObject[1] - bootstrap-owned global physics-state manager root - owner: GameBootstrapper
            return runtimeRoot.AddComponent<GlobalPhysicsStateManager>();
        }

        private static bool TryRunBootstrapStep(BootstrapStepToken stepToken, string phaseName, Action initializeAction)
        {
            BootstrapStatus.BeginStep(stepToken);
            try
            {
                initializeAction?.Invoke();
                return true;
            }
            catch (Exception exception)
            {
                HandleFatalBootstrapException(phaseName, exception);
                return false;
            }
            finally
            {
                BootstrapStatus.EndStep(stepToken);
            }
        }

        private static bool TryRunBootstrapStep(BootstrapStepToken stepToken, string phaseName, Func<bool> initializeAction)
        {
            BootstrapStatus.BeginStep(stepToken);
            try
            {
                return initializeAction == null || initializeAction.Invoke();
            }
            catch (Exception exception)
            {
                HandleFatalBootstrapException(phaseName, exception);
                return false;
            }
            finally
            {
                BootstrapStatus.EndStep(stepToken);
            }
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

        private static void HandleFatalBootstrapException(string phaseName, Exception exception)
        {
            if (exception == null)
                return;

            string crashMessage = BuildFatalBootstrapMessage(phaseName, exception);
            WriteFatalBootstrapLog(crashMessage);
            BootstrapBiosErrorOverlay.Show(string.Format(FatalBootOverlayMessageTemplate, phaseName));

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogException(exception);
#endif
        }

        private static string BuildFatalBootstrapMessage(string phaseName, Exception exception)
        {
            Scene activeScene = SceneManager.GetActiveScene();
            StringBuilder builder = new StringBuilder(1024);
            builder.Append("HECTON-8 FATAL BOOT CRASH").Append('\n')
                .Append("UTC: ").Append(DateTime.UtcNow.ToString("O")).Append('\n')
                .Append("PHASE: ").Append(string.IsNullOrEmpty(phaseName) ? "Unknown" : phaseName).Append('\n')
                .Append("SCENE: ").Append(string.IsNullOrEmpty(activeScene.name) ? "<unnamed>" : activeScene.name)
                .Append(" [").Append(activeScene.buildIndex).Append(']').Append('\n')
                .Append("PERSISTENT_DATA_PATH: ").Append(Application.persistentDataPath).Append('\n')
                .Append("STACKTRACE:").Append('\n')
                .Append(exception);
            return builder.ToString();
        }

        private static unsafe void WriteFatalBootstrapLog(string message)
        {
            if (string.IsNullOrEmpty(message))
                return;

            string persistentDataPath = Application.persistentDataPath;
            if (string.IsNullOrEmpty(persistentDataPath))
                return;

            string truncatedMessage = message;
            int requiredBytes = _fatalBootCrashEncoding.GetByteCount(truncatedMessage);
            while (requiredBytes > FatalBootCrashLogBufferBytes && truncatedMessage.Length > 1)
            {
                truncatedMessage = truncatedMessage.Substring(0, truncatedMessage.Length >> 1);
                requiredBytes = _fatalBootCrashEncoding.GetByteCount(truncatedMessage);
            }

            if (requiredBytes <= 0)
                return;

            string absolutePath = Path.Combine(persistentDataPath, FatalBootCrashFileName);
            NativeArray<byte> scratch = new NativeArray<byte>(requiredBytes, Allocator.Temp, NativeArrayOptions.UninitializedMemory); // COLD ALLOC: NativeArray<byte>[fatal boot crash payload bytes] - bootstrap fatal log staging - owner: GameBootstrapper
            try
            {
                byte* destination = (byte*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(scratch);
                fixed (char* source = truncatedMessage)
                {
                    int bytesWritten = _fatalBootCrashEncoding.GetBytes(source, truncatedMessage.Length, destination, requiredBytes);
                    if (bytesWritten > 0)
                        AsyncWriteManager.WriteAll(absolutePath, destination, bytesWritten, out _);
                }
            }
            finally
            {
                scratch.Dispose();
            }
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
