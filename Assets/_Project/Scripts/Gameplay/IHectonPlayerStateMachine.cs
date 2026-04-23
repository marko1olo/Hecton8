namespace Hecton8.Gameplay
{
    /// <summary>
    /// Authoritative environment-side locomotion classification.
    /// </summary>
    public enum PlayerEnvironmentState : byte
    {
        DryExterior = 0,
        DryInterior = 1,
        ShallowExterior = 2,
        SurfaceExterior = 3,
        UnderwaterExterior = 4
    }

    /// <summary>
    /// Authoritative support-side locomotion classification.
    /// </summary>
    public enum PlayerSupportState : byte
    {
        Unsupported = 0,
        Grounded = 1
    }

    /// <summary>
    /// Authoritative override-side locomotion classification.
    /// </summary>
    public enum PlayerOverrideState : byte
    {
        None = 0,
        Exosuit = 1,
        Wipeout = 2
    }

    /// <summary>
    /// Minimal player state-machine contract exposed to locomotion adjunct systems.
    /// </summary>
    public interface IHectonPlayerStateMachine
    {
        /// <summary>Current environment-side locomotion state.</summary>
        PlayerEnvironmentState CurrentEnvironmentState { get; }

        /// <summary>Current support-side locomotion state.</summary>
        PlayerSupportState CurrentSupportState { get; }

        /// <summary>Current override-side locomotion state.</summary>
        PlayerOverrideState CurrentOverrideState { get; }

        /// <summary>Current locomotion mode mirrored from the player controller.</summary>
        PlayerLocomotionMode CurrentLocomotionMode { get; }

        /// <summary>True while wipeout recovery is active.</summary>
        bool IsInWipeout { get; }

        /// <summary>Current wipeout recovery timer.</summary>
        float WipeoutTimer { get; }

        /// <summary>Current wipeout severity.</summary>
        float WipeoutSeverity { get; }

        /// <summary>Synchronizes locomotion mode from the controller owner.</summary>
        void SyncLocomotionMode(PlayerLocomotionMode mode);

        /// <summary>Synchronizes environment/support/override context from the controller owner.</summary>
        void SyncContext(
            PlayerEnvironmentState environmentState,
            PlayerSupportState supportState,
            PlayerOverrideState overrideState,
            PlayerLocomotionMode mode);

        /// <summary>Begins or extends wipeout recovery state.</summary>
        void BeginWipeout(float severity, float duration);

        /// <summary>Advances wipeout recovery timers.</summary>
        void AdvanceFixed(float fixedDeltaTime);

        /// <summary>Clears runtime state during teardown.</summary>
        void ResetRuntimeState();
    }
}
