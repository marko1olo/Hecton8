#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using Hecton8.Core;
using Hecton8.World;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Hecton8.Editor
{
    /// <summary>
    /// Editor-only validator that disables scene render/audio presentation and executes simulation interfaces directly.
    /// </summary>
    public static class HeadlessSimulationValidator
    {
        private const int DefaultTickCount = 500;
        private const float SimulatedDeltaTime = 0.02f;
        private const int SlowTickCadence = 25;

        private static readonly List<ComponentState<MeshRenderer>> _meshRendererStates = new List<ComponentState<MeshRenderer>>(4096);
        private static readonly List<ComponentState<SkinnedMeshRenderer>> _skinnedRendererStates = new List<ComponentState<SkinnedMeshRenderer>>(1024);
        private static readonly List<ComponentState<Camera>> _cameraStates = new List<ComponentState<Camera>>(32);
        private static readonly List<ComponentState<AudioSource>> _audioSourceStates = new List<ComponentState<AudioSource>>(512);
        private static readonly List<MonoBehaviour> _simulationBehaviours = new List<MonoBehaviour>(2048);

        private static bool _isRunning;
        private static int _remainingTicks;
        private static int _executedTicks;
        private static int _nullReferenceCount;
        private static Exception _firstException;

        private readonly struct ComponentState<T> where T : Component
        {
            public readonly T Component;
            public readonly bool WasEnabled;

            public ComponentState(T component)
            {
                Component = component;
                WasEnabled = IsComponentEnabled(component);
            }
        }

        [MenuItem("Hecton8/Validation/Headless Simulation Validator")]
        private static void RunFromMenu()
        {
            RunHeadlessSimulationTicks(DefaultTickCount);
        }

        [MenuItem("Hecton8/Validation/Headless Scatter Refresh Validator")]
        private static void RunScatterRefreshFromMenu()
        {
            RunHeadlessScatterRefresh();
        }

        /// <summary>
        /// Executes a scatter refresh with presentation cameras/renderers disabled so Camera.main and renderer paths cannot be required.
        /// </summary>
        /// <summary>
        /// Runs the scatter refresh path with presentation components disabled to validate headless simulation safety.
        /// </summary>
        public static void RunHeadlessScatterRefresh()
        {
            WorldProceduralScatterDirector director = Object.FindAnyObjectByType<WorldProceduralScatterDirector>(FindObjectsInactive.Include);
            if (director == null)
            {
                Debug.LogError("[HeadlessSimulationValidator] FAILED: WorldProceduralScatterDirector not found.");
                return;
            }

            _nullReferenceCount = 0;
            _firstException = null;
            CaptureAndDisablePresentation();
            Application.logMessageReceived += HandleLogMessageReceived;
            try
            {
                director.RebuildScatterPreview();
            }
            catch (NullReferenceException exception)
            {
                _nullReferenceCount++;
                _firstException = exception;
                throw;
            }
            catch (Exception exception)
            {
                _firstException = exception;
                throw;
            }
            finally
            {
                Application.logMessageReceived -= HandleLogMessageReceived;
                RestorePresentation();
            }

            if (_nullReferenceCount > 0)
            {
                Debug.LogError($"[HeadlessSimulationValidator] FAILED: {_nullReferenceCount} NullReferenceException entries during headless scatter refresh.");
                return;
            }

            if (_firstException != null)
            {
                Debug.LogError($"[HeadlessSimulationValidator] FAILED: {_firstException.GetType().Name} during headless scatter refresh.");
                return;
            }

            Debug.Log($"[HeadlessSimulationValidator] PASS: headless scatter refresh executed, ActivePlacementCount={director.ActivePlacementCount}, Camera.main path disabled.");
        }

        /// <summary>
        /// Runs a headless scene simulation pass with renderers and audio sources disabled.
        /// </summary>
        /// <param name="tickCount">Number of direct simulation ticks to execute.</param>
        public static void RunHeadlessSimulationTicks(int tickCount)
        {
            if (_isRunning)
                return;

            if (!EditorApplication.isPlaying)
            {
                Debug.LogError("[HeadlessSimulationValidator] Enter Play Mode before running headless simulation validation.");
                return;
            }

            _isRunning = true;
            _remainingTicks = Mathf.Max(1, tickCount);
            _executedTicks = 0;
            _nullReferenceCount = 0;
            _firstException = null;
            CaptureAndDisablePresentation();
            CaptureSimulationBehaviours();
            Application.logMessageReceived += HandleLogMessageReceived;
            EditorApplication.update += RunOneEditorTick;
        }

        private static void RunOneEditorTick()
        {
            if (!_isRunning)
                return;

            try
            {
                ExecuteSimulationTick(_executedTicks);
                _executedTicks++;
                _remainingTicks--;
                if (_remainingTicks <= 0)
                    CompleteValidation();
            }
            catch (NullReferenceException exception)
            {
                _nullReferenceCount++;
                _firstException = exception;
                CompleteValidation();
                throw;
            }
            catch (Exception exception)
            {
                _firstException = exception;
                CompleteValidation();
                throw;
            }
        }

        private static void CompleteValidation()
        {
            EditorApplication.update -= RunOneEditorTick;
            Application.logMessageReceived -= HandleLogMessageReceived;
            RestorePresentation();
            _simulationBehaviours.Clear();
            _isRunning = false;

            if (_nullReferenceCount > 0)
            {
                Debug.LogError($"[HeadlessSimulationValidator] FAILED: {_nullReferenceCount} NullReferenceException entries during {_executedTicks} headless ticks.");
                return;
            }

            if (_firstException != null)
            {
                Debug.LogError($"[HeadlessSimulationValidator] FAILED: {_firstException.GetType().Name} during {_executedTicks} headless ticks.");
                return;
            }

            Debug.Log($"[HeadlessSimulationValidator] PASS: {_executedTicks} headless ticks, renderers/audio disabled, NullReferenceExceptions=0.");
        }

        private static void ExecuteSimulationTick(int tickIndex)
        {
            for (int i = 0; i < _simulationBehaviours.Count; i++)
            {
                MonoBehaviour behaviour = _simulationBehaviours[i];
                if (behaviour == null || !behaviour.isActiveAndEnabled)
                    continue;

                if (behaviour is IFixedTickable fixedTickable)
                    fixedTickable.FixedTick(SimulatedDeltaTime);

                if (behaviour is IPostFixedTickable postFixedTickable)
                    postFixedTickable.PostFixedTick(SimulatedDeltaTime);

                if (behaviour is IUpdatable updatable)
                    updatable.Tick(SimulatedDeltaTime);

                if (behaviour is ILateFrameTickable lateFrameTickable)
                    lateFrameTickable.LateFrameTick();

                if (tickIndex % SlowTickCadence == 0 && behaviour is ISlowTickable slowTickable)
                    slowTickable.SlowTick();
            }
        }

        private static void CaptureAndDisablePresentation()
        {
            _meshRendererStates.Clear();
            _skinnedRendererStates.Clear();
            _cameraStates.Clear();
            _audioSourceStates.Clear();

            Camera[] cameras = Object.FindObjectsByType<Camera>(FindObjectsInactive.Include);
            for (int i = 0; i < cameras.Length; i++)
            {
                Camera camera = cameras[i];
                if (!IsSceneComponent(camera))
                    continue;

                _cameraStates.Add(new ComponentState<Camera>(camera));
                camera.enabled = false;
            }

            MeshRenderer[] meshRenderers = Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include);
            for (int i = 0; i < meshRenderers.Length; i++)
            {
                MeshRenderer meshRenderer = meshRenderers[i];
                if (!IsSceneComponent(meshRenderer))
                    continue;

                _meshRendererStates.Add(new ComponentState<MeshRenderer>(meshRenderer));
                meshRenderer.enabled = false;
            }

            SkinnedMeshRenderer[] skinnedRenderers = Object.FindObjectsByType<SkinnedMeshRenderer>(FindObjectsInactive.Include);
            for (int i = 0; i < skinnedRenderers.Length; i++)
            {
                SkinnedMeshRenderer skinnedRenderer = skinnedRenderers[i];
                if (!IsSceneComponent(skinnedRenderer))
                    continue;

                _skinnedRendererStates.Add(new ComponentState<SkinnedMeshRenderer>(skinnedRenderer));
                skinnedRenderer.enabled = false;
            }

            AudioSource[] audioSources = Object.FindObjectsByType<AudioSource>(FindObjectsInactive.Include);
            for (int i = 0; i < audioSources.Length; i++)
            {
                AudioSource audioSource = audioSources[i];
                if (!IsSceneComponent(audioSource))
                    continue;

                _audioSourceStates.Add(new ComponentState<AudioSource>(audioSource));
                audioSource.enabled = false;
            }
        }

        private static void RestorePresentation()
        {
            for (int i = 0; i < _meshRendererStates.Count; i++)
            {
                ComponentState<MeshRenderer> state = _meshRendererStates[i];
                if (state.Component != null)
                    SetComponentEnabled(state.Component, state.WasEnabled);
            }

            for (int i = 0; i < _skinnedRendererStates.Count; i++)
            {
                ComponentState<SkinnedMeshRenderer> state = _skinnedRendererStates[i];
                if (state.Component != null)
                    SetComponentEnabled(state.Component, state.WasEnabled);
            }

            for (int i = 0; i < _cameraStates.Count; i++)
            {
                ComponentState<Camera> state = _cameraStates[i];
                if (state.Component != null)
                    SetComponentEnabled(state.Component, state.WasEnabled);
            }

            for (int i = 0; i < _audioSourceStates.Count; i++)
            {
                ComponentState<AudioSource> state = _audioSourceStates[i];
                if (state.Component != null)
                    SetComponentEnabled(state.Component, state.WasEnabled);
            }

            _meshRendererStates.Clear();
            _skinnedRendererStates.Clear();
            _cameraStates.Clear();
            _audioSourceStates.Clear();
        }

        private static void CaptureSimulationBehaviours()
        {
            _simulationBehaviours.Clear();
            MonoBehaviour[] behaviours = Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include);
            for (int i = 0; i < behaviours.Length; i++)
            {
                MonoBehaviour behaviour = behaviours[i];
                if (!IsSceneComponent(behaviour) || !behaviour.isActiveAndEnabled)
                    continue;

                if (behaviour is IFixedTickable ||
                    behaviour is IPostFixedTickable ||
                    behaviour is IUpdatable ||
                    behaviour is ILateFrameTickable ||
                    behaviour is ISlowTickable)
                {
                    _simulationBehaviours.Add(behaviour);
                }
            }
        }

        private static bool IsSceneComponent(Component component)
        {
            return component != null &&
                   component.gameObject != null &&
                   component.gameObject.scene.IsValid() &&
                   !EditorUtility.IsPersistent(component);
        }

        private static bool IsComponentEnabled(Component component)
        {
            if (component is Renderer renderer)
                return renderer.enabled;

            if (component is Behaviour behaviour)
                return behaviour.enabled;

            return false;
        }

        private static void SetComponentEnabled(Component component, bool enabled)
        {
            if (component is Renderer renderer)
            {
                renderer.enabled = enabled;
                return;
            }

            if (component is Behaviour behaviour)
                behaviour.enabled = enabled;
        }

        private static void HandleLogMessageReceived(string condition, string stackTrace, LogType type)
        {
            if (type != LogType.Exception || condition == null)
                return;

            if (condition.IndexOf("NullReferenceException", StringComparison.Ordinal) >= 0)
                _nullReferenceCount++;
        }
    }
}
#endif
