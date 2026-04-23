using System;
using Hecton8.Interaction;
using UnityEngine;

namespace Hecton8.Core
{
    /// <summary>
    /// Static runtime service locator and dense bucket registry for first-party core systems.
    /// </summary>
    public static class GlobalRegistry
    {
        // COLD ALLOC: RegistryBucket<IUpdatable>[128] — global multi-instance update registry — owner: GlobalRegistry
        private static readonly RegistryBucket<IUpdatable> _updatables = new RegistryBucket<IUpdatable>(128);
        // COLD ALLOC: RegistryBucket<IRenderable>[64] — global multi-instance render registry — owner: GlobalRegistry
        private static readonly RegistryBucket<IRenderable> _renderables = new RegistryBucket<IRenderable>(64);

        private static IInputService _input;
        private static IPhysicsService _physics;
        private static IAudioService _audio;
        private static ISceneService _scene;
        private static IUIService _ui;
        private static IWeatherService _weather;
        private static IInteractionSignalService _interactionSignals;
        private static IDebrisService _debris;

        /// <summary>
        /// Registered input service slot.
        /// </summary>
        public static IInputService Input => _input;

        /// <summary>
        /// Registered physics service slot.
        /// </summary>
        public static IPhysicsService Physics => _physics;

        /// <summary>
        /// Registered audio service slot.
        /// </summary>
        public static IAudioService Audio => _audio;

        /// <summary>
        /// Registered scene service slot.
        /// </summary>
        public static ISceneService Scene => _scene;

        /// <summary>
        /// Registered UI service slot.
        /// </summary>
        public static IUIService UI => _ui;

        /// <summary>
        /// Registered weather service slot.
        /// </summary>
        public static IWeatherService Weather => _weather;

        /// <summary>
        /// Registered interaction signal service slot.
        /// </summary>
        public static IInteractionSignalService InteractionSignals => _interactionSignals;

        /// <summary>
        /// Registered debris service slot.
        /// </summary>
        public static IDebrisService Debris => _debris;

        /// <summary>
        /// Dense multi-instance update registry.
        /// </summary>
        public static RegistryBucket<IUpdatable> Updatables => _updatables;

        /// <summary>
        /// Dense multi-instance render registry.
        /// </summary>
        public static RegistryBucket<IRenderable> Renderables => _renderables;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _input = null;
            _physics = null;
            _audio = null;
            _scene = null;
            _ui = null;
            _weather = null;
            _interactionSignals = null;
            _debris = null;
            _updatables.Clear();
            _renderables.Clear();
            SystemDispatcher.ClearAllLanes();
        }

        /// <summary>
        /// Registers the authoritative input service.
        /// </summary>
        /// <param name="instance">Input service instance.</param>
        public static void RegisterInputService(IInputService instance)
        {
            RegisterService(ref _input, instance);
        }

        /// <summary>
        /// Registers the authoritative physics service.
        /// </summary>
        /// <param name="instance">Physics service instance.</param>
        public static void RegisterPhysicsService(IPhysicsService instance)
        {
            RegisterService(ref _physics, instance);
        }

        /// <summary>
        /// Registers the authoritative audio service.
        /// </summary>
        /// <param name="instance">Audio service instance.</param>
        public static void RegisterAudioService(IAudioService instance)
        {
            RegisterService(ref _audio, instance);
        }

        /// <summary>
        /// Registers the authoritative scene service.
        /// </summary>
        /// <param name="instance">Scene service instance.</param>
        public static void RegisterSceneService(ISceneService instance)
        {
            RegisterService(ref _scene, instance);
        }

        /// <summary>
        /// Registers the authoritative UI service.
        /// </summary>
        /// <param name="instance">UI service instance.</param>
        public static void RegisterUIService(IUIService instance)
        {
            RegisterService(ref _ui, instance);
        }

        /// <summary>
        /// Registers the authoritative weather service.
        /// </summary>
        /// <param name="instance">Weather service instance.</param>
        public static void RegisterWeatherService(IWeatherService instance)
        {
            RegisterService(ref _weather, instance);
        }

        /// <summary>
        /// Registers the authoritative interaction signal service.
        /// </summary>
        /// <param name="instance">Interaction signal service instance.</param>
        public static void RegisterInteractionSignalService(IInteractionSignalService instance)
        {
            RegisterService(ref _interactionSignals, instance);
        }

        /// <summary>
        /// Registers the authoritative debris service.
        /// </summary>
        /// <param name="instance">Debris service instance.</param>
        public static void RegisterDebrisService(IDebrisService instance)
        {
            RegisterService(ref _debris, instance);
        }

        /// <summary>
        /// Unregisters the current input service if the owner matches.
        /// </summary>
        /// <param name="instance">Service owner requesting unregistration.</param>
        public static void UnregisterInputService(IInputService instance)
        {
            UnregisterService(ref _input, instance);
        }

        /// <summary>
        /// Unregisters the current physics service if the owner matches.
        /// </summary>
        /// <param name="instance">Service owner requesting unregistration.</param>
        public static void UnregisterPhysicsService(IPhysicsService instance)
        {
            UnregisterService(ref _physics, instance);
        }

        /// <summary>
        /// Unregisters the current audio service if the owner matches.
        /// </summary>
        /// <param name="instance">Service owner requesting unregistration.</param>
        public static void UnregisterAudioService(IAudioService instance)
        {
            UnregisterService(ref _audio, instance);
        }

        /// <summary>
        /// Unregisters the current scene service if the owner matches.
        /// </summary>
        /// <param name="instance">Service owner requesting unregistration.</param>
        public static void UnregisterSceneService(ISceneService instance)
        {
            UnregisterService(ref _scene, instance);
        }

        /// <summary>
        /// Unregisters the current UI service if the owner matches.
        /// </summary>
        /// <param name="instance">Service owner requesting unregistration.</param>
        public static void UnregisterUIService(IUIService instance)
        {
            UnregisterService(ref _ui, instance);
        }

        /// <summary>
        /// Unregisters the current weather service if the owner matches.
        /// </summary>
        /// <param name="instance">Service owner requesting unregistration.</param>
        public static void UnregisterWeatherService(IWeatherService instance)
        {
            UnregisterService(ref _weather, instance);
        }

        /// <summary>
        /// Unregisters the current interaction signal service if the owner matches.
        /// </summary>
        /// <param name="instance">Service owner requesting unregistration.</param>
        public static void UnregisterInteractionSignalService(IInteractionSignalService instance)
        {
            UnregisterService(ref _interactionSignals, instance);
        }

        /// <summary>
        /// Unregisters the current debris service if the owner matches.
        /// </summary>
        /// <param name="instance">Service owner requesting unregistration.</param>
        public static void UnregisterDebrisService(IDebrisService instance)
        {
            UnregisterService(ref _debris, instance);
        }

        /// <summary>
        /// Registers an update owner into both the global bucket and its fixed dispatcher lane.
        /// </summary>
        /// <param name="item">Update owner.</param>
        /// <param name="layer">Dispatcher priority lane.</param>
        public static void RegisterUpdatable(IUpdatable item, PriorityLayer layer)
        {
            if (item == null)
                return;

            _updatables.Register(item);
            SystemDispatcher.Register(item, layer);
        }

        /// <summary>
        /// Unregisters an update owner from both the global bucket and its dispatcher lane.
        /// </summary>
        /// <param name="item">Update owner.</param>
        /// <param name="layer">Dispatcher priority lane.</param>
        public static void UnregisterUpdatable(IUpdatable item, PriorityLayer layer)
        {
            if (item == null)
                return;

            _updatables.Unregister(item);
            SystemDispatcher.Unregister(item, layer);
        }

        /// <summary>
        /// Clears all global multi-instance registries.
        /// </summary>
        public static void ClearRuntimeBuckets()
        {
            _updatables.Clear();
            _renderables.Clear();
            SystemDispatcher.ClearAllLanes();
        }

        private static void RegisterService<T>(ref T slot, T instance) where T : class
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (slot != null)
            {
                throw new InvalidOperationException(
                    $"[GlobalRegistry] Service {typeof(T).Name} already registered.");
            }

            if (instance == null)
            {
                throw new ArgumentNullException(
                    nameof(instance),
                    $"[GlobalRegistry] Cannot register null as {typeof(T).Name}.");
            }
#endif
            slot = instance;
        }

        private static void UnregisterService<T>(ref T slot, T instance) where T : class
        {
            if (!ReferenceEquals(slot, instance))
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogWarning($"[GlobalRegistry] Unregister mismatch for {typeof(T).Name}.");
#endif
                return;
            }

            slot = null;
        }
    }
}
