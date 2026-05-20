namespace Hecton8.Core
{
    /// <summary>
    /// Narrow audio bridge consumed by scene-transition presentation code.
    /// The audio owner implements this contract; Core must not cast to an audio-domain concrete type.
    /// </summary>
    public interface ISceneTransitionAudioBridge : ISystem
    {
        /// <summary>
        /// Starts the world-drone mixer transition during the guarded main-menu to world handoff.
        /// </summary>
        /// <param name="startDb">Initial world-drone level in decibels.</param>
        /// <param name="targetDb">Runtime world-drone level in decibels.</param>
        /// <param name="durationSeconds">Transition duration in seconds.</param>
        void BeginWorldDroneTransition(float startDb, float targetDb, float durationSeconds);

        /// <summary>
        /// Applies normalized progress for the active world-drone transition.
        /// </summary>
        /// <param name="normalized">Normalized transition progress in the range [0, 1].</param>
        void SetWorldDroneTransitionProgress(float normalized);
    }

    /// <summary>
    /// Narrow physics bridge consumed by guarded scene-transition cleanup code.
    /// The physics owner implements this contract; Core scene flow must not cast to physics-domain concrete types.
    /// </summary>
    public interface ISceneTransitionPhysicsBridge : ISystem
    {
        /// <summary>
        /// Clears queued force packets and scene-bound physics runtime state.
        /// </summary>
        void ClearSceneTransitionRuntimeState();
    }

    /// <summary>
    /// Narrow world-residency bridge consumed by guarded scene activation code.
    /// The world owner implements this contract; Core scene flow must not cast to a world-domain concrete type.
    /// </summary>
    public interface ISceneTransitionWorldResidencyBridge : ISystem
    {
        /// <summary>
        /// True when resident world prefab pools are ready for activation of the world scene.
        /// </summary>
        bool AreResidentWorldPrefabPoolsReady();
    }

    /// <summary>
    /// Narrow bridge used by the runtime watchdog to sample indexed world-save health.
    /// </summary>
    public interface IRuntimeWatchdogWorldHealthBridge : ISystem
    {
        /// <summary>
        /// Resolves the indexed-sector save path and active sector hash for cold MMF health checks.
        /// </summary>
        bool TryGetIndexedSaveHealth(out string absolutePath, out long currentSectorHash);
    }
}
