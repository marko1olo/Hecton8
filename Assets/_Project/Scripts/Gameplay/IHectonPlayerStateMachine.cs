namespace Hecton8.Gameplay
{
    /// <summary>
    /// Minimal player state-machine contract exposed to locomotion adjunct systems.
    /// </summary>
    public interface IHectonPlayerStateMachine
    {
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

        /// <summary>Begins or extends wipeout recovery state.</summary>
        void BeginWipeout(float severity, float duration);

        /// <summary>Advances wipeout recovery timers.</summary>
        void AdvanceFixed(float fixedDeltaTime);

        /// <summary>Clears runtime state during teardown.</summary>
        void ResetRuntimeState();
    }
}
