using System;
using Hecton8.Bootstrap;
using Hecton8.Physics;
using Hecton8.World;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Hecton8.Core
{
    /// <summary>
    /// Guarded scene transition owner for GlobalRegistry cleanup and bootstrap gating.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-9940)]
    public sealed class SceneRuntimeService : MonoBehaviour, ISceneService, IUpdatable
    {
        private const int SceneActivationWatchdogInitialFrames = 1200;
        private const int SceneActivationWatchdogRepeatFrames = 300;
        private const string MainMenuSceneName = "01_MAIN_MENU";
        private const string TransitionOverlayRootName = "[SceneRuntimeService_TransitionOverlay]";
        private const string TransitionDitherShaderName = "Hecton8/UI/BlueNoiseDitherDissolve";
        private const float MainMenuCameraPanDurationSeconds = 2f;
        private const float MainMenuCameraPanDepth = 9f;
        private const float MainMenuCameraPanPitchDegrees = 16f;
        private const float TransitionDissolveSeconds = 2f;
        private const int TransitionOverlaySortingOrder = 32766;
        private static readonly int _TransitionDitherProgressId = Shader.PropertyToID("_DitherProgress");
        private static readonly int _TransitionDitherColorId = Shader.PropertyToID("_Color");
        private static readonly int _TransitionBlueNoiseTextureId = Shader.PropertyToID("_BlueNoiseTex");
        private static readonly Color _TransitionAbyssColor = new Color(0.002f, 0.004f, 0.009f, 1f);

        private static SceneRuntimeService _instance;
        private static bool _suppressRuntimeClearForManagedUnload;
        private bool _isInitialized;
        private bool _registeredSceneService;
        private bool _registeredSceneCallbacks;
        private bool _registeredUpdatable;
        private bool _sceneLoadInFlight;
        private string _pendingSceneName;
        private AsyncOperation _pendingSceneLoadOperation;
        private int _gpuResidencyReadyFrame = -1;
        private bool _cinematicTransitionActive;
        private float _cinematicTransitionElapsed;
        private Camera _cinematicCamera;
        private Camera _configuredCinematicCamera;
        private Texture _configuredBlueNoiseTexture;
        private Vector3 _cinematicCameraStartPosition;
        private Quaternion _cinematicCameraStartRotation;
        private GameObject _transitionOverlayRoot;
        private CanvasGroup _transitionOverlayGroup;
        private Material _transitionDitherMaterial;

        /// <summary>
        /// True once the service has registered itself into <see cref="GlobalRegistry"/>.
        /// </summary>
        public bool IsInitialized => _isInitialized;

        /// <summary>
        /// True when bootstrap has completed and guarded transitions are allowed.
        /// </summary>
        public bool CanLoadScene => GameBootstrapper.IsBootstrapComplete;

        internal void ConfigureMainMenuCinematic(Camera menuCamera, Texture blueNoiseTexture)
        {
            _configuredCinematicCamera = menuCamera;
            _configuredBlueNoiseTexture = blueNoiseTexture;
        }

        /// <summary>
        /// Ensures a live runtime instance exists.
        /// </summary>
        /// <returns>Live scene service instance.</returns>
        public static SceneRuntimeService EnsureRuntimeInstance()
        {
            if (_instance != null)
                return _instance;

            GameObject runtimeRoot = new GameObject("[SceneRuntimeService]");
            SceneRuntimeService sceneService = runtimeRoot.AddComponent<SceneRuntimeService>();
            return sceneService;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _instance = null;
            _suppressRuntimeClearForManagedUnload = false;
        }

        /// <summary>
        /// Explicitly initializes the service and registers it into <see cref="GlobalRegistry"/>.
        /// </summary>
        public void InitializeService()
        {
            if (_isInitialized)
            {
                TryRegisterUpdatable();
                TryRegisterSceneService();
                TryRegisterSceneCallbacks();
                return;
            }

            _isInitialized = true;
            TryRegisterUpdatable();
            TryRegisterSceneService();
            TryRegisterSceneCallbacks();
        }

        /// <summary>
        /// Performs a guarded scene transition after clearing registry state.
        /// </summary>
        /// <param name="sceneName">Build-settings scene name.</param>
        public void LoadScene(string sceneName)
        {
            if (_sceneLoadInFlight)
            {
                LogSceneLoadRejectedInFlight(sceneName, _pendingSceneName);
                return;
            }

            _ = LoadSceneAsync(sceneName);
        }

        /// <inheritdoc />
        public async Awaitable LoadSceneAsync(string sceneName)
        {
            if (!CanLoadScene)
            {
                LogSceneLoadRejectedBootstrapIncomplete(sceneName);
                return;
            }

            if (_sceneLoadInFlight)
            {
                LogSceneLoadRejectedInFlight(sceneName, _pendingSceneName);
                return;
            }

            try
            {
                _sceneLoadInFlight = true;
                _pendingSceneName = sceneName;
                _gpuResidencyReadyFrame = -1;
                Scene previousScene = SceneManager.GetActiveScene();
                bool useCinematicTransition = ShouldUseMainMenuCinematicTransition(previousScene, sceneName);
                if (useCinematicTransition)
                    BeginMainMenuCinematicTransition();

                ClearRuntimeState();

                LoadSceneMode loadMode = useCinematicTransition ? LoadSceneMode.Additive : LoadSceneMode.Single;
                AsyncOperation loadOperation = SceneManager.LoadSceneAsync(sceneName, loadMode);
                if (loadOperation == null)
                {
                    LogSceneLoadOperationMissing(sceneName);
                    return;
                }

                _pendingSceneLoadOperation = loadOperation;
                _pendingSceneLoadOperation.allowSceneActivation = false;
                int waitFrames = 0;
                int nextWatchdogFrame = SceneActivationWatchdogInitialFrames;

                while (Application.isPlaying && ReferenceEquals(_instance, this) && !_pendingSceneLoadOperation.isDone)
                {
                    if (useCinematicTransition)
                        TickMainMenuCinematicTransition(Time.unscaledDeltaTime);

                    bool loadReady = _pendingSceneLoadOperation.progress >= 0.9f;
                    bool poolsReady = loadReady && ArePersistentWorldPoolsReadyForSceneActivation();
                    bool originStable = poolsReady && IsFloatingOriginStableForSceneActivation();
                    bool gpuResidencyReady = IsGpuResidencyReadyForSceneActivation(loadReady, poolsReady, originStable);

                    if (loadReady && poolsReady && originStable && gpuResidencyReady)
                    {
                        _pendingSceneLoadOperation.allowSceneActivation = true;
                    }
                    else if (waitFrames >= nextWatchdogFrame)
                    {
                        LogSceneActivationWatchdog(sceneName, _pendingSceneLoadOperation.progress, loadReady, poolsReady, originStable, gpuResidencyReady, waitFrames);
                        nextWatchdogFrame = waitFrames + SceneActivationWatchdogRepeatFrames;
                    }

                    waitFrames++;
                    await Awaitable.NextFrameAsync(cancellationToken: destroyCancellationToken);
                }

                if (useCinematicTransition && Application.isPlaying && ReferenceEquals(_instance, this))
                    await CompleteMainMenuCinematicTransitionAsync(previousScene, sceneName);
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                EndMainMenuCinematicTransition();
                _sceneLoadInFlight = false;
                _pendingSceneName = null;
                _pendingSceneLoadOperation = null;
                _gpuResidencyReadyFrame = -1;
            }
        }

        /// <summary>
        /// Core-lane dispatcher hook required by the runtime registry contract.
        /// </summary>
        /// <param name="deltaTime">Scaled frame delta supplied by the dispatcher.</param>
        public void Tick(float deltaTime)
        {
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;

        }

        private void OnEnable()
        {
            TryRegisterUpdatable();
            if (_isInitialized)
            {
                TryRegisterSceneService();
                TryRegisterSceneCallbacks();
            }
        }

        private void OnDisable()
        {
            TryUnregisterUpdatable();
            TryUnregisterSceneCallbacks();
            TryUnregisterSceneService();
        }

        private void OnDestroy()
        {
            TryUnregisterUpdatable();
            TryUnregisterSceneCallbacks();
            TryUnregisterSceneService();
            _isInitialized = false;

            if (_instance == this)
                _instance = null;
        }

        private static void HandleSceneUnloaded(Scene scene)
        {
            if (_suppressRuntimeClearForManagedUnload)
                return;

            ClearRuntimeState();
        }

        private static void ClearRuntimeState()
        {
            GlobalRegistry.ClearRuntimeBuckets();
            ThreadSafeCommandQueue.Clear();

            if (GlobalRegistry.Physics != null)
                GlobalRegistry.Physics.ClearQueuedPackets();
            else
                PhysicsApplySystem.ClearQueuedPacketsStatic();

            if (GlobalRegistry.InteractionSignals != null)
                GlobalRegistry.InteractionSignals.ClearQueuedSignals();

            if (GlobalRegistry.Debris != null)
                GlobalRegistry.Debris.ClearActiveDebris();

            GlobalPhysicsStateManager.ClearRuntimeStateStatic();
        }

        private static bool ArePersistentWorldPoolsReadyForSceneActivation()
        {
            if (!GameBootstrapper.ArePreWarmAssetsReady)
                return false;

            PersistentWorldRegistry registry = GlobalRegistry.PersistentWorldRegistry;
            if (registry == null)
                return false;

            return registry.AreResidentWorldPrefabPoolsReady();
        }

        private static bool ShouldUseMainMenuCinematicTransition(Scene previousScene, string nextSceneName)
        {
            return previousScene.IsValid() &&
                   previousScene.isLoaded &&
                   string.Equals(previousScene.name, MainMenuSceneName, StringComparison.Ordinal) &&
                   !string.Equals(previousScene.name, nextSceneName, StringComparison.Ordinal);
        }

        private void BeginMainMenuCinematicTransition()
        {
            _cinematicTransitionActive = true;
            _cinematicTransitionElapsed = 0f;
            _cinematicCamera = _configuredCinematicCamera;
            if (_cinematicCamera != null)
            {
                Transform cameraTransform = _cinematicCamera.transform;
                _cinematicCameraStartPosition = cameraTransform.position;
                _cinematicCameraStartRotation = cameraTransform.rotation;
            }

            EnsureTransitionOverlay();
            if (_transitionOverlayGroup != null)
                _transitionOverlayGroup.alpha = 0f;
            SetTransitionDitherCoverage(1f);
        }

        private void TickMainMenuCinematicTransition(float unscaledDeltaTime)
        {
            if (!_cinematicTransitionActive)
                return;

            _cinematicTransitionElapsed += Mathf.Max(0f, unscaledDeltaTime);
            float normalized = MainMenuCameraPanDurationSeconds > 0f
                ? Mathf.Clamp01(_cinematicTransitionElapsed / MainMenuCameraPanDurationSeconds)
                : 1f;
            float eased = SmoothStep01(normalized);

            if (_cinematicCamera != null)
            {
                Transform cameraTransform = _cinematicCamera.transform;
                Vector3 targetPosition = _cinematicCameraStartPosition + (Vector3.down * MainMenuCameraPanDepth);
                Quaternion targetRotation = _cinematicCameraStartRotation * Quaternion.Euler(MainMenuCameraPanPitchDegrees, 0f, 0f);
                cameraTransform.SetPositionAndRotation(
                    Vector3.LerpUnclamped(_cinematicCameraStartPosition, targetPosition, eased),
                    Quaternion.SlerpUnclamped(_cinematicCameraStartRotation, targetRotation, eased));
            }

            if (_transitionOverlayGroup != null)
                _transitionOverlayGroup.alpha = eased;
            SetTransitionDitherCoverage(1f);
        }

        private async Awaitable CompleteMainMenuCinematicTransitionAsync(Scene previousScene, string loadedSceneName)
        {
            Scene loadedScene = SceneManager.GetSceneByName(loadedSceneName);
            if (loadedScene.IsValid() && loadedScene.isLoaded)
                SceneManager.SetActiveScene(loadedScene);

            if (previousScene.IsValid() && previousScene.isLoaded && !string.Equals(previousScene.name, loadedSceneName, StringComparison.Ordinal))
            {
                _suppressRuntimeClearForManagedUnload = true;
                try
                {
                    AsyncOperation unloadOperation = SceneManager.UnloadSceneAsync(previousScene);
                    while (unloadOperation != null && !unloadOperation.isDone)
                    {
                        await Awaitable.NextFrameAsync(cancellationToken: destroyCancellationToken);
                    }
                }
                finally
                {
                    _suppressRuntimeClearForManagedUnload = false;
                }
            }

            await DissolveTransitionOverlayAsync();
        }

        private async Awaitable DissolveTransitionOverlayAsync()
        {
            EnsureTransitionOverlay();
            if (_transitionOverlayGroup == null)
                return;

            _transitionOverlayGroup.alpha = 1f;
            float elapsed = 0f;
            while (Application.isPlaying && elapsed < TransitionDissolveSeconds)
            {
                elapsed += Mathf.Max(0f, Time.unscaledDeltaTime);
                float normalized = TransitionDissolveSeconds > 0f
                    ? Mathf.Clamp01(elapsed / TransitionDissolveSeconds)
                    : 1f;
                float eased = SmoothStep01(normalized);
                SetTransitionDitherCoverage(1f - eased);
                if (_transitionDitherMaterial == null)
                    _transitionOverlayGroup.alpha = 1f - eased;

                await Awaitable.NextFrameAsync(cancellationToken: destroyCancellationToken);
            }

            SetTransitionDitherCoverage(0f);
            _transitionOverlayGroup.alpha = 0f;
        }

        private void EndMainMenuCinematicTransition()
        {
            _cinematicTransitionActive = false;
            _cinematicTransitionElapsed = 0f;
            _cinematicCamera = null;
            _configuredCinematicCamera = null;
            _configuredBlueNoiseTexture = null;

            if (_transitionOverlayRoot != null)
                Destroy(_transitionOverlayRoot);

            _transitionOverlayRoot = null;
            _transitionOverlayGroup = null;
            if (_transitionDitherMaterial != null)
                Destroy(_transitionDitherMaterial);
            _transitionDitherMaterial = null;
        }

        private void EnsureTransitionOverlay()
        {
            if (_transitionOverlayRoot != null)
                return;

            GameObject root = new GameObject(TransitionOverlayRootName, typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup)); // COLD ALLOC: GameObject[1] - scene transition blackout overlay - owner: SceneRuntimeService
            root.transform.SetParent(transform, false);
            Canvas canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = TransitionOverlaySortingOrder;

            _transitionOverlayGroup = root.GetComponent<CanvasGroup>();
            _transitionOverlayGroup.alpha = 0f;
            _transitionOverlayGroup.interactable = false;
            _transitionOverlayGroup.blocksRaycasts = true;

            GameObject imageRoot = new GameObject("DitherBlackout", typeof(RectTransform), typeof(Image)); // COLD ALLOC: GameObject[1] - full-screen dither image - owner: SceneRuntimeService
            imageRoot.transform.SetParent(root.transform, false);
            RectTransform imageRect = imageRoot.GetComponent<RectTransform>();
            imageRect.anchorMin = Vector2.zero;
            imageRect.anchorMax = Vector2.one;
            imageRect.offsetMin = Vector2.zero;
            imageRect.offsetMax = Vector2.zero;

            Image image = imageRoot.GetComponent<Image>();
            image.raycastTarget = true;
            image.color = _TransitionAbyssColor;

            Shader ditherShader = Shader.Find(TransitionDitherShaderName);
            if (ditherShader != null)
            {
                _transitionDitherMaterial = new Material(ditherShader); // COLD ALLOC: Material[1] - blue-noise scene dissolve material - owner: SceneRuntimeService
                _transitionDitherMaterial.SetColor(_TransitionDitherColorId, _TransitionAbyssColor);
                _transitionDitherMaterial.SetFloat(_TransitionDitherProgressId, 1f);
                if (_configuredBlueNoiseTexture != null)
                    _transitionDitherMaterial.SetTexture(_TransitionBlueNoiseTextureId, _configuredBlueNoiseTexture);
                image.material = _transitionDitherMaterial;
            }

            _transitionOverlayRoot = root;
        }

        private void SetTransitionDitherCoverage(float coverage)
        {
            if (_transitionDitherMaterial == null)
                return;

            _transitionDitherMaterial.SetFloat(_TransitionDitherProgressId, Mathf.Clamp01(coverage));
        }

        private static float SmoothStep01(float value)
        {
            value = Mathf.Clamp01(value);
            return value * value * (3f - (2f * value));
        }

        private static bool IsFloatingOriginStableForSceneActivation()
        {
            return !HectonFloatingOrigin.IsShiftInProgress &&
                   !HectonFloatingOrigin.IsPhysicsPausedForShift;
        }

        private bool IsGpuResidencyReadyForSceneActivation(bool loadReady, bool poolsReady, bool originStable)
        {
            if (!loadReady || !poolsReady || !originStable)
            {
                _gpuResidencyReadyFrame = -1;
                return false;
            }

            if (_gpuResidencyReadyFrame < 0)
            {
                _gpuResidencyReadyFrame = Time.frameCount + 1;
                return false;
            }

            return Time.frameCount >= _gpuResidencyReadyFrame;
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogSceneActivationWatchdog(
            string sceneName,
            float progress,
            bool loadReady,
            bool poolsReady,
            bool originStable,
            bool gpuResidencyReady,
            int waitFrames)
        {
            string blockedBy = GetSceneActivationBlockedReason(loadReady, poolsReady, originStable, gpuResidencyReady);
            Debug.LogError($"[SceneRuntimeService] Scene load '{sceneName}' still blocked after {waitFrames} frames. Reason: {blockedBy}. Progress: {progress:0.00}.");
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogSceneLoadRejectedInFlight(string sceneName, string pendingSceneName)
        {
            Debug.LogWarning($"[SceneRuntimeService] Scene load '{sceneName}' rejected because '{pendingSceneName}' is already in flight.");
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogSceneLoadRejectedBootstrapIncomplete(string sceneName)
        {
            Debug.LogError($"[SceneRuntimeService] Scene load '{sceneName}' rejected while bootstrap is incomplete.");
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogSceneLoadOperationMissing(string sceneName)
        {
            Debug.LogError($"[SceneRuntimeService] Scene load '{sceneName}' failed to create an AsyncOperation.");
        }

        private static string GetSceneActivationBlockedReason(bool loadReady, bool poolsReady, bool originStable, bool gpuResidencyReady)
        {
            if (!loadReady)
                return "AsyncOperation progress below activation threshold";
            if (!poolsReady)
                return "PersistentWorldRegistry pools not ready";
            if (!originStable)
                return "floating origin shift in progress";
            if (!gpuResidencyReady)
                return "GPU residency settle frame pending";

            return "activation pending";
        }

        private void TryRegisterUpdatable()
        {
            if (_registeredUpdatable || !Application.isPlaying)
                return;

            if (GlobalRegistry.Dispatcher == null)
                return;

            GlobalRegistry.RegisterUpdatable(this, PriorityLayer.Core);
            _registeredUpdatable = GlobalRegistry.Updatables.Contains(this);
        }

        private void TryUnregisterUpdatable()
        {
            if (!_registeredUpdatable)
                return;

            GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Core);
            _registeredUpdatable = false;
        }

        private void TryRegisterSceneService()
        {
            if (_registeredSceneService)
                return;

            GlobalRegistry.RegisterSceneService(this);
            _registeredSceneService = ReferenceEquals(GlobalRegistry.Scene, this);
        }

        private void TryUnregisterSceneService()
        {
            if (!_registeredSceneService)
                return;

            GlobalRegistry.UnregisterSceneService(this);
            _registeredSceneService = false;
        }

        private void TryRegisterSceneCallbacks()
        {
            if (_registeredSceneCallbacks)
                return;

            SceneManager.sceneUnloaded += HandleSceneUnloaded;
            _registeredSceneCallbacks = true;
        }

        private void TryUnregisterSceneCallbacks()
        {
            if (!_registeredSceneCallbacks)
                return;

            SceneManager.sceneUnloaded -= HandleSceneUnloaded;
            _registeredSceneCallbacks = false;
        }
    }
}
