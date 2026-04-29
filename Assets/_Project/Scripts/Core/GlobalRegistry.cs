using System;
using Hecton8.Construction;
using Hecton8.Quest;
using Hecton8.Gameplay;
using Hecton8.Interaction;
using Hecton8.Physics;
using Hecton8.SaveSystem;
using Hecton8.Systems.AI;
using Hecton8.Tools;
using Hecton8.World;
using UnityEngine;

namespace Hecton8.Core
{
    /// <summary>
    /// Static runtime service locator and dense bucket registry for first-party core systems.
    /// </summary>
    public static class GlobalRegistry
    {
        // COLD ALLOC: RegistryBucket<IUpdatable>[128] — global multi-instance update registry — owner: GlobalRegistry
        private static readonly RegistryBucket<IUpdatable> _updatables = new RegistryBucket<IUpdatable>(512);
        // COLD ALLOC: RegistryBucket<IRenderable>[64] — global multi-instance render registry — owner: GlobalRegistry
        private static readonly RegistryBucket<IRenderable> _renderables = new RegistryBucket<IRenderable>(64);
        private static readonly RegistryBucket<IFixedTickable> _fixedTickables = new RegistryBucket<IFixedTickable>(256);
        private static readonly RegistryBucket<ISlowTickable> _slowTickables = new RegistryBucket<ISlowTickable>(256);

        private static IInputService _input;
        private static IPhysicsService _physics;
        private static IAudioService _audio;
        private static ISceneService _scene;
        private static ISaveService _save;
        private static IUIService _ui;
        private static ObjectPoolManager _objectPool;
        private static IPlayerRuntimeContext _player;
        private static IPlayerInventoryService _playerInventory;
        private static IModularEquipmentService _modularEquipment;
        private static IPlayerSensoryService _playerSensory;
        private static IEnvironmentRuntimeContext _environment;
        private static IWeatherService _weather;
        private static IHectonOceanKinematicsService _oceanKinematics;
        private static IPowerGridService _powerGrid;
        private static ISubmarineRuntimeContext _submarine;
        private static ISubmarineHullBreachReadModel _submarineHullBreach;
        private static IInteractionSignalService _interactionSignals;
        private static IDebrisService _debris;
        private static IEcosystemDirectorService _ecosystemDirector;
        private static IThermodynamicsService _thermodynamicsService;
        private static ILogisticsService _logistics;
        private static IWorldGenService _worldGen;
        private static IEncounterDirectorService _encounterDirector;
        private static IQuestSystem _questSystem;
        private static HectonFluidEngine _fluidRuntime;
        private static AbyssalThermalManager _thermodynamicsRuntime;
        private static HectonNarrativeDirector _narrativeDirectorRuntime;
        private static QuestManager _questRuntime;
        private static GameTickManager _tickManager;
        private static SystemDispatcher _dispatcher;
        private static RenderDispatcher _renderDispatcher;
        private static GlobalPhysicsStateManager _physicsStateManager;
        private static bool _dispatcherRegistrationErrorLogged;

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
        /// Registered save service slot.
        /// </summary>
        public static ISaveService Save => _save;

        /// <summary>
        /// Registered concrete save runtime owner for compatibility during singleton migration.
        /// </summary>
        public static Hecton8.SaveSystem.SaveManager SaveRuntime => _save as Hecton8.SaveSystem.SaveManager;

        /// <summary>
        /// Registered UI service slot.
        /// </summary>
        public static IUIService UI => _ui;

        /// <summary>
        /// Registered object-pool runtime owner.
        /// </summary>
        public static ObjectPoolManager ObjectPool => _objectPool;

        /// <summary>
        /// Registered player runtime context slot.
        /// </summary>
        public static IPlayerRuntimeContext Player => _player;

        /// <summary>
        /// Registered player inventory/tooling service slot.
        /// </summary>
        public static IPlayerInventoryService PlayerInventory => _playerInventory;

        /// <summary>
        /// Registered modular-equipment runtime service slot.
        /// </summary>
        public static IModularEquipmentService ModularEquipment => _modularEquipment;

        /// <summary>
        /// Registered player sensory/presentation service slot.
        /// </summary>
        public static IPlayerSensoryService PlayerSensory => _playerSensory;

        /// <summary>
        /// Registered environment runtime context slot.
        /// </summary>
        public static IEnvironmentRuntimeContext Environment => _environment;

        /// <summary>
        /// Registered weather service slot.
        /// </summary>
        public static IWeatherService Weather => _weather;

        /// <summary>
        /// Registered ocean-kinematics selector service slot.
        /// </summary>
        public static IHectonOceanKinematicsService OceanKinematics => _oceanKinematics;

        /// <summary>
        /// Registered power-grid runtime service slot.
        /// </summary>
        public static IPowerGridService PowerGrid => _powerGrid;

        /// <summary>
        /// Registered authoritative submarine runtime root slot.
        /// </summary>
        public static ISubmarineRuntimeContext Submarine => _submarine;

        /// <summary>
        /// Registered submarine hull-breach read model slot.
        /// Front-buffer only. Writers must keep back-buffer private.
        /// </summary>
        public static ISubmarineHullBreachReadModel SubmarineHullBreach => _submarineHullBreach;

        /// <summary>
        /// Registered interaction signal service slot.
        /// </summary>
        public static IInteractionSignalService InteractionSignals => _interactionSignals;

        /// <summary>
        /// Registered debris service slot.
        /// </summary>
        public static IDebrisService Debris => _debris;

        /// <summary>
        /// Registered ecosystem sector simulation service slot.
        /// </summary>
        public static IEcosystemDirectorService EcosystemDirector => _ecosystemDirector;

        /// <summary>
        /// Registered thermodynamics service slot.
        /// </summary>
        public static IThermodynamicsService ThermodynamicsService => _thermodynamicsService;

        /// <summary>
        /// Registered logistics/build-network service slot.
        /// </summary>
        public static ILogisticsService Logistics => _logistics;

        /// <summary>
        /// Registered world-generation service slot.
        /// </summary>
        public static IWorldGenService WorldGen => _worldGen;

        /// <summary>
        /// Registered encounter-direction service slot.
        /// </summary>
        public static IEncounterDirectorService EncounterDirector => _encounterDirector;

        /// <summary>
        /// Registered quest-system service slot.
        /// </summary>
        public static IQuestSystem QuestSystem => _questSystem;

        /// <summary>
        /// Registered fluid simulation runtime owner.
        /// </summary>
        public static HectonFluidEngine Fluid => _fluidRuntime;

        /// <summary>
        /// Registered thermodynamic simulation runtime owner.
        /// </summary>
        public static AbyssalThermalManager Thermodynamics => _thermodynamicsRuntime;

        /// <summary>
        /// Registered narrative director runtime owner.
        /// </summary>
        public static HectonNarrativeDirector NarrativeDirector => _narrativeDirectorRuntime;

        /// <summary>
        /// Registered quest runtime owner.
        /// </summary>
        public static QuestManager Quest => _questRuntime;

        /// <summary>
        /// Registered tick-manager owner.
        /// </summary>
        public static GameTickManager TickManager => _tickManager;

        /// <summary>
        /// Registered gameplay dispatcher owner.
        /// </summary>
        public static SystemDispatcher Dispatcher => _dispatcher;

        /// <summary>
        /// Registered SRP render dispatcher owner.
        /// </summary>
        public static RenderDispatcher RenderDispatcher => _renderDispatcher;

        /// <summary>
        /// Registered global physics-state manager owner.
        /// </summary>
        public static GlobalPhysicsStateManager PhysicsStateManager => _physicsStateManager;

        /// <summary>
        /// Dense multi-instance update registry.
        /// </summary>
        public static RegistryBucket<IUpdatable> Updatables => _updatables;

        /// <summary>
        /// Dense multi-instance render registry.
        /// </summary>
        public static RegistryBucket<IRenderable> Renderables => _renderables;

        /// <summary>
        /// Dense multi-instance fixed-update registry.
        /// </summary>
        public static RegistryBucket<IFixedTickable> FixedTickables => _fixedTickables;

        /// <summary>
        /// Dense multi-instance slow-tick registry.
        /// </summary>
        public static RegistryBucket<ISlowTickable> SlowTickables => _slowTickables;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _input = null;
            _physics = null;
            _audio = null;
            _scene = null;
            _save = null;
            _ui = null;
            _objectPool = null;
            _player = null;
            _playerInventory = null;
            _modularEquipment = null;
            _playerSensory = null;
            _environment = null;
            _weather = null;
            _oceanKinematics = null;
            _powerGrid = null;
            _submarine = null;
            _submarineHullBreach = null;
            _interactionSignals = null;
            _debris = null;
            _ecosystemDirector = null;
            _thermodynamicsService = null;
            _logistics = null;
            _worldGen = null;
            _encounterDirector = null;
            _questSystem = null;
            _fluidRuntime = null;
            _thermodynamicsRuntime = null;
            _narrativeDirectorRuntime = null;
            _questRuntime = null;
            _tickManager = null;
            _dispatcher = null;
            _renderDispatcher = null;
            _physicsStateManager = null;
            _dispatcherRegistrationErrorLogged = false;
            _updatables.Clear();
            _fixedTickables.Clear();
            _slowTickables.Clear();
            _renderables.Clear();
            SystemDispatcher.ClearAllLanes();
        }

        /// <summary>
        /// Registers the authoritative tick-manager owner.
        /// </summary>
        /// <param name="instance">Tick-manager instance.</param>
        public static void RegisterTickManager(GameTickManager instance)
        {
            RegisterService(ref _tickManager, instance);
        }

        /// <summary>
        /// Registers the authoritative gameplay dispatcher owner.
        /// </summary>
        /// <param name="instance">Dispatcher instance.</param>
        public static void RegisterSystemDispatcher(SystemDispatcher instance)
        {
            RegisterService(ref _dispatcher, instance);
        }

        /// <summary>
        /// Registers the authoritative SRP render dispatcher owner.
        /// </summary>
        /// <param name="instance">Render dispatcher instance.</param>
        public static void RegisterRenderDispatcher(RenderDispatcher instance)
        {
            RegisterService(ref _renderDispatcher, instance);
        }

        /// <summary>
        /// Registers the authoritative global physics-state manager owner.
        /// </summary>
        /// <param name="instance">Physics-state manager instance.</param>
        public static void RegisterPhysicsStateManager(GlobalPhysicsStateManager instance)
        {
            RegisterService(ref _physicsStateManager, instance);
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
        /// Registers the authoritative save service.
        /// </summary>
        /// <param name="instance">Save service instance.</param>
        public static void RegisterSaveService(ISaveService instance)
        {
            RegisterService(ref _save, instance);
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
        /// Registers the authoritative object-pool runtime owner.
        /// </summary>
        /// <param name="instance">Object-pool owner instance.</param>
        public static void RegisterObjectPoolService(ObjectPoolManager instance)
        {
            RegisterService(ref _objectPool, instance);
        }

        /// <summary>
        /// Registers the authoritative player runtime context.
        /// </summary>
        /// <param name="instance">Player runtime context instance.</param>
        public static void RegisterPlayerRuntimeContext(IPlayerRuntimeContext instance)
        {
            RegisterService(ref _player, instance);
        }

        /// <summary>
        /// Registers the authoritative player inventory/tooling service.
        /// </summary>
        /// <param name="instance">Player inventory/tooling service instance.</param>
        public static void RegisterPlayerInventoryService(IPlayerInventoryService instance)
        {
            RegisterService(ref _playerInventory, instance);
        }

        /// <summary>
        /// Registers the authoritative modular-equipment runtime service.
        /// </summary>
        public static void RegisterModularEquipmentService(IModularEquipmentService instance)
        {
            RegisterService(ref _modularEquipment, instance);
        }

        /// <summary>
        /// Registers the authoritative player sensory/presentation service.
        /// </summary>
        /// <param name="instance">Player sensory service instance.</param>
        public static void RegisterPlayerSensoryService(IPlayerSensoryService instance)
        {
            RegisterService(ref _playerSensory, instance);
        }

        /// <summary>
        /// Registers the authoritative environment runtime context.
        /// </summary>
        /// <param name="instance">Environment runtime context instance.</param>
        public static void RegisterEnvironmentRuntimeContext(IEnvironmentRuntimeContext instance)
        {
            RegisterService(ref _environment, instance);
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
        /// Registers the authoritative ocean-kinematics selector service.
        /// </summary>
        /// <param name="instance">Ocean-kinematics service instance.</param>
        public static void RegisterOceanKinematicsService(IHectonOceanKinematicsService instance)
        {
            RegisterService(ref _oceanKinematics, instance);
        }

        /// <summary>
        /// Registers the authoritative power-grid runtime service.
        /// </summary>
        /// <param name="instance">Power-grid runtime service instance.</param>
        public static void RegisterPowerGridService(IPowerGridService instance)
        {
            RegisterService(ref _powerGrid, instance);
        }

        /// <summary>
        /// Registers the authoritative submarine runtime root.
        /// </summary>
        /// <param name="instance">Submarine runtime root instance.</param>
        public static void RegisterSubmarine(ISubmarineRuntimeContext instance)
        {
            RegisterService(ref _submarine, instance);
        }

        /// <summary>
        /// Registers the authoritative submarine hull-breach read model.
        /// </summary>
        /// <param name="instance">Submarine hull-breach read model instance.</param>
        public static void RegisterSubmarineHullBreach(ISubmarineHullBreachReadModel instance)
        {
            RegisterService(ref _submarineHullBreach, instance);
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
        /// Registers the authoritative ecosystem sector simulation service.
        /// </summary>
        /// <param name="instance">Ecosystem director service instance.</param>
        public static void RegisterEcosystemDirectorService(IEcosystemDirectorService instance)
        {
            RegisterService(ref _ecosystemDirector, instance);
        }

        /// <summary>
        /// Registers the authoritative thermodynamics service.
        /// </summary>
        /// <param name="instance">Thermodynamics service instance.</param>
        public static void RegisterThermodynamicsService(IThermodynamicsService instance)
        {
            RegisterServiceAllowSameInstance(ref _thermodynamicsService, instance);
        }

        /// <summary>
        /// Registers the authoritative logistics/build-network service.
        /// </summary>
        /// <param name="instance">Logistics service instance.</param>
        public static void RegisterLogisticsService(ILogisticsService instance)
        {
            RegisterServiceAllowSameInstance(ref _logistics, instance);
        }

        /// <summary>
        /// Registers the authoritative world-generation service.
        /// </summary>
        /// <param name="instance">World-generation service instance.</param>
        public static void RegisterWorldGenService(IWorldGenService instance)
        {
            RegisterServiceAllowSameInstance(ref _worldGen, instance);
        }

        /// <summary>
        /// Registers the authoritative encounter-direction service.
        /// </summary>
        /// <param name="instance">Encounter-direction service instance.</param>
        public static void RegisterEncounterDirectorService(IEncounterDirectorService instance)
        {
            RegisterServiceAllowSameInstance(ref _encounterDirector, instance);
        }

        /// <summary>
        /// Registers the authoritative quest-system service.
        /// </summary>
        /// <param name="instance">Quest-system service instance.</param>
        public static void RegisterQuestSystem(IQuestSystem instance)
        {
            RegisterServiceAllowSameInstance(ref _questSystem, instance);
        }

        /// <summary>
        /// Registers the authoritative fluid simulation runtime owner.
        /// </summary>
        public static void RegisterFluidRuntime(HectonFluidEngine instance)
        {
            RegisterService(ref _fluidRuntime, instance);
        }

        /// <summary>
        /// Registers the authoritative thermodynamic simulation runtime owner.
        /// </summary>
        public static void RegisterThermodynamicsRuntime(AbyssalThermalManager instance)
        {
            RegisterServiceAllowSameInstance(ref _thermodynamicsRuntime, instance);

            if (instance is IThermodynamicsService thermodynamicsService)
                RegisterThermodynamicsService(thermodynamicsService);
        }

        /// <summary>
        /// Registers the authoritative narrative runtime owner.
        /// </summary>
        public static void RegisterNarrativeDirectorRuntime(HectonNarrativeDirector instance)
        {
            RegisterService(ref _narrativeDirectorRuntime, instance);
        }

        /// <summary>
        /// Registers the authoritative quest runtime owner.
        /// </summary>
        public static void RegisterQuestRuntime(QuestManager instance)
        {
            RegisterServiceAllowSameInstance(ref _questRuntime, instance);

            if (instance is IQuestSystem questSystem)
                RegisterQuestSystem(questSystem);
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
        /// Unregisters the current save service if the owner matches.
        /// </summary>
        /// <param name="instance">Service owner requesting unregistration.</param>
        public static void UnregisterSaveService(ISaveService instance)
        {
            UnregisterService(ref _save, instance);
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
        /// Unregisters the current object-pool runtime owner if the owner matches.
        /// </summary>
        /// <param name="instance">Object-pool owner requesting unregistration.</param>
        public static void UnregisterObjectPoolService(ObjectPoolManager instance)
        {
            UnregisterService(ref _objectPool, instance);
        }

        /// <summary>
        /// Unregisters the current player runtime context if the owner matches.
        /// </summary>
        /// <param name="instance">Context owner requesting unregistration.</param>
        public static void UnregisterPlayerRuntimeContext(IPlayerRuntimeContext instance)
        {
            UnregisterService(ref _player, instance);
        }

        /// <summary>
        /// Unregisters the current player inventory/tooling service if the owner matches.
        /// </summary>
        /// <param name="instance">Service owner requesting unregistration.</param>
        public static void UnregisterPlayerInventoryService(IPlayerInventoryService instance)
        {
            UnregisterService(ref _playerInventory, instance);
        }

        /// <summary>
        /// Unregisters the current modular-equipment runtime service if the owner matches.
        /// </summary>
        public static void UnregisterModularEquipmentService(IModularEquipmentService instance)
        {
            UnregisterService(ref _modularEquipment, instance);
        }

        /// <summary>
        /// Unregisters the current player sensory/presentation service if the owner matches.
        /// </summary>
        /// <param name="instance">Service owner requesting unregistration.</param>
        public static void UnregisterPlayerSensoryService(IPlayerSensoryService instance)
        {
            UnregisterService(ref _playerSensory, instance);
        }

        /// <summary>
        /// Unregisters the current environment runtime context if the owner matches.
        /// </summary>
        /// <param name="instance">Context owner requesting unregistration.</param>
        public static void UnregisterEnvironmentRuntimeContext(IEnvironmentRuntimeContext instance)
        {
            UnregisterService(ref _environment, instance);
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
        /// Unregisters the current ocean-kinematics selector service if the owner matches.
        /// </summary>
        /// <param name="instance">Service owner requesting unregistration.</param>
        public static void UnregisterOceanKinematicsService(IHectonOceanKinematicsService instance)
        {
            UnregisterService(ref _oceanKinematics, instance);
        }

        /// <summary>
        /// Unregisters the current power-grid runtime service if the owner matches.
        /// </summary>
        /// <param name="instance">Service owner requesting unregistration.</param>
        public static void UnregisterPowerGridService(IPowerGridService instance)
        {
            UnregisterService(ref _powerGrid, instance);
        }

        /// <summary>
        /// Unregisters the current submarine runtime root if the owner matches.
        /// </summary>
        /// <param name="instance">Submarine runtime root requesting unregistration.</param>
        public static void UnregisterSubmarine(ISubmarineRuntimeContext instance)
        {
            UnregisterService(ref _submarine, instance);
        }

        /// <summary>
        /// Unregisters the current submarine hull-breach read model if the owner matches.
        /// </summary>
        /// <param name="instance">Read-model owner requesting unregistration.</param>
        public static void UnregisterSubmarineHullBreach(ISubmarineHullBreachReadModel instance)
        {
            UnregisterService(ref _submarineHullBreach, instance);
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
        /// Unregisters the current ecosystem sector simulation service if the owner matches.
        /// </summary>
        /// <param name="instance">Service owner requesting unregistration.</param>
        public static void UnregisterEcosystemDirectorService(IEcosystemDirectorService instance)
        {
            UnregisterService(ref _ecosystemDirector, instance);
        }

        /// <summary>
        /// Unregisters the current thermodynamics service if the owner matches.
        /// </summary>
        /// <param name="instance">Service owner requesting unregistration.</param>
        public static void UnregisterThermodynamicsService(IThermodynamicsService instance)
        {
            UnregisterService(ref _thermodynamicsService, instance);
        }

        /// <summary>
        /// Unregisters the current logistics/build-network service if the owner matches.
        /// </summary>
        /// <param name="instance">Service owner requesting unregistration.</param>
        public static void UnregisterLogisticsService(ILogisticsService instance)
        {
            UnregisterService(ref _logistics, instance);
        }

        /// <summary>
        /// Unregisters the current world-generation service if the owner matches.
        /// </summary>
        /// <param name="instance">Service owner requesting unregistration.</param>
        public static void UnregisterWorldGenService(IWorldGenService instance)
        {
            UnregisterService(ref _worldGen, instance);
        }

        /// <summary>
        /// Unregisters the current encounter-direction service if the owner matches.
        /// </summary>
        /// <param name="instance">Service owner requesting unregistration.</param>
        public static void UnregisterEncounterDirectorService(IEncounterDirectorService instance)
        {
            UnregisterService(ref _encounterDirector, instance);
        }

        /// <summary>
        /// Unregisters the current quest-system service if the owner matches.
        /// </summary>
        /// <param name="instance">Service owner requesting unregistration.</param>
        public static void UnregisterQuestSystem(IQuestSystem instance)
        {
            UnregisterService(ref _questSystem, instance);
        }

        /// <summary>
        /// Unregisters the current fluid simulation runtime owner if the owner matches.
        /// </summary>
        public static void UnregisterFluidRuntime(HectonFluidEngine instance)
        {
            UnregisterService(ref _fluidRuntime, instance);
        }

        /// <summary>
        /// Unregisters the current thermodynamic simulation runtime owner if the owner matches.
        /// </summary>
        public static void UnregisterThermodynamicsRuntime(AbyssalThermalManager instance)
        {
            UnregisterService(ref _thermodynamicsRuntime, instance);

            if (instance is IThermodynamicsService thermodynamicsService)
                UnregisterThermodynamicsService(thermodynamicsService);
        }

        /// <summary>
        /// Unregisters the current narrative runtime owner if the owner matches.
        /// </summary>
        public static void UnregisterNarrativeDirectorRuntime(HectonNarrativeDirector instance)
        {
            UnregisterService(ref _narrativeDirectorRuntime, instance);
        }

        /// <summary>
        /// Unregisters the current quest runtime owner if the owner matches.
        /// </summary>
        public static void UnregisterQuestRuntime(QuestManager instance)
        {
            UnregisterService(ref _questRuntime, instance);

            if (instance is IQuestSystem questSystem)
                UnregisterQuestSystem(questSystem);
        }

        /// <summary>
        /// Unregisters the current tick-manager owner if the owner matches.
        /// </summary>
        /// <param name="instance">Tick-manager owner requesting unregistration.</param>
        public static void UnregisterTickManager(GameTickManager instance)
        {
            UnregisterService(ref _tickManager, instance);
        }

        /// <summary>
        /// Unregisters the current gameplay dispatcher owner if the owner matches.
        /// </summary>
        /// <param name="instance">Dispatcher owner requesting unregistration.</param>
        public static void UnregisterSystemDispatcher(SystemDispatcher instance)
        {
            UnregisterService(ref _dispatcher, instance);
        }

        /// <summary>
        /// Unregisters the current SRP render dispatcher owner if the owner matches.
        /// </summary>
        /// <param name="instance">Render dispatcher owner requesting unregistration.</param>
        public static void UnregisterRenderDispatcher(RenderDispatcher instance)
        {
            UnregisterService(ref _renderDispatcher, instance);
        }

        /// <summary>
        /// Unregisters the current global physics-state manager owner if the owner matches.
        /// </summary>
        /// <param name="instance">Physics-state manager owner requesting unregistration.</param>
        public static void UnregisterPhysicsStateManager(GlobalPhysicsStateManager instance)
        {
            UnregisterService(ref _physicsStateManager, instance);
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

            if (!Application.isPlaying)
                return;

            if (!TryEnsureDispatcherRegistration())
                return;
            _updatables.Register(item);
            SystemDispatcher.Register(item, layer);
        }

        /// <summary>
        /// Registers a fixed-update owner into both the global bucket and its fixed dispatcher lane.
        /// </summary>
        /// <param name="item">Fixed-update owner.</param>
        /// <param name="layer">Dispatcher priority lane.</param>
        public static void RegisterFixedTickable(IFixedTickable item, PriorityLayer layer)
        {
            if (item == null)
                return;

            if (!Application.isPlaying)
                return;

            if (!TryEnsureDispatcherRegistration())
                return;
            _fixedTickables.Register(item);
            SystemDispatcher.Register(item, layer);
        }

        /// <summary>
        /// Registers a slow-tick owner into both the global bucket and its dispatcher lane.
        /// </summary>
        /// <param name="item">Slow-tick owner.</param>
        /// <param name="layer">Dispatcher priority lane.</param>
        public static void RegisterSlowTickable(ISlowTickable item, PriorityLayer layer)
        {
            if (item == null)
                return;

            if (!Application.isPlaying)
                return;

            if (!TryEnsureDispatcherRegistration())
                return;
            _slowTickables.Register(item);
            SystemDispatcher.Register(item, layer);
        }

        /// <summary>
        /// Registers an end-of-frame owner into its dispatcher late-frame lane.
        /// </summary>
        /// <param name="item">Late-frame owner.</param>
        /// <param name="layer">Dispatcher priority lane.</param>
        public static void RegisterLateFrameTickable(ILateFrameTickable item, PriorityLayer layer)
        {
            if (item == null)
                return;

            if (!Application.isPlaying)
                return;

            if (!TryEnsureDispatcherRegistration())
                return;

            SystemDispatcher.Register(item, layer);
        }

        /// <summary>
        /// Registers a post-fixed-step owner into its dispatcher lane.
        /// </summary>
        /// <param name="item">Post-fixed owner.</param>
        /// <param name="layer">Dispatcher priority lane.</param>
        public static void RegisterPostFixedTickable(IPostFixedTickable item, PriorityLayer layer)
        {
            if (item == null)
                return;

            if (!Application.isPlaying)
                return;

            if (!TryEnsureDispatcherRegistration())
                return;

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
        /// Unregisters a fixed-update owner from both the global bucket and its dispatcher lane.
        /// </summary>
        /// <param name="item">Fixed-update owner.</param>
        /// <param name="layer">Dispatcher priority lane.</param>
        public static void UnregisterFixedTickable(IFixedTickable item, PriorityLayer layer)
        {
            if (item == null)
                return;

            _fixedTickables.Unregister(item);
            SystemDispatcher.Unregister(item, layer);
        }

        /// <summary>
        /// Unregisters a slow-tick owner from both the global bucket and its dispatcher lane.
        /// </summary>
        /// <param name="item">Slow-tick owner.</param>
        /// <param name="layer">Dispatcher priority lane.</param>
        public static void UnregisterSlowTickable(ISlowTickable item, PriorityLayer layer)
        {
            if (item == null)
                return;

            _slowTickables.Unregister(item);
            SystemDispatcher.Unregister(item, layer);
        }

        /// <summary>
        /// Unregisters an end-of-frame owner from its dispatcher lane.
        /// </summary>
        /// <param name="item">Late-frame owner.</param>
        /// <param name="layer">Dispatcher priority lane.</param>
        public static void UnregisterLateFrameTickable(ILateFrameTickable item, PriorityLayer layer)
        {
            if (item == null)
                return;

            SystemDispatcher.Unregister(item, layer);
        }

        /// <summary>
        /// Unregisters a post-fixed-step owner from its dispatcher lane.
        /// </summary>
        /// <param name="item">Post-fixed owner.</param>
        /// <param name="layer">Dispatcher priority lane.</param>
        public static void UnregisterPostFixedTickable(IPostFixedTickable item, PriorityLayer layer)
        {
            if (item == null)
                return;

            SystemDispatcher.Unregister(item, layer);
        }

        /// <summary>
        /// Clears all global multi-instance registries.
        /// </summary>
        public static void ClearRuntimeBuckets()
        {
            _updatables.Clear();
            _fixedTickables.Clear();
            _slowTickables.Clear();
            _renderables.Clear();
            SystemDispatcher.ClearAllLanes();
        }

        private static bool TryEnsureDispatcherRegistration()
        {
            if (_dispatcher != null)
                return true;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!_dispatcherRegistrationErrorLogged)
            {
                _dispatcherRegistrationErrorLogged = true;
                Debug.LogError("[GlobalRegistry] SystemDispatcher is not registered. Bootstrap must create and register it before runtime tick registration.");
            }
#endif
            return false;
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

        private static void RegisterServiceAllowSameInstance<T>(ref T slot, T instance) where T : class
        {
            if (ReferenceEquals(slot, instance))
                return;

            RegisterService(ref slot, instance);
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
