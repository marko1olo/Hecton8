// ============================================================================
// HECTON-8 - ITickable.cs
// Interface contracts for the centralized runtime update system.
//
// Any script implementing one or more tick interfaces must:
//   1. Register with GlobalRegistry in OnEnable().
//   2. Unregister from GlobalRegistry in OnDisable().
//   3. Never declare its own Update, FixedUpdate, or LateUpdate loop.
//
// SystemDispatcher owns the Unity message loop and pumps registered systems
// through deterministic lanes instead of scattered Unity message callbacks.
// ============================================================================

namespace Hecton8.Core
{
    /// <summary>
    /// Per-frame tick lane. Replacement for Update().
    /// Use for input, movement, animation, and UI logic.
    /// </summary>
    public interface ITickable : IUpdatable
    {
        /// <param name="deltaTime">Frame delta supplied by the dispatcher. Do not read Time.deltaTime inside implementations.</param>
        new void Tick(float deltaTime);
    }

    /// <summary>
    /// Fixed-step tick lane. Replacement for FixedUpdate().
    /// Use for Rigidbody motion and synchronous physics checks.
    /// </summary>
    public interface IFixedTickable
    {
        /// <param name="fixedDeltaTime">Fixed delta supplied by the dispatcher. Do not read Time.fixedDeltaTime inside implementations.</param>
        void FixedTick(float fixedDeltaTime);
    }

    /// <summary>
    /// Slow tick lane, normally about twice per second.
    /// Use for base life support, AI decisions, autosave hints, and work not needed every frame.
    /// </summary>
    public interface ISlowTickable
    {
        /// <summary>
        /// Called on the configured slow cadence. Delta time is intentionally not passed.
        /// </summary>
        void SlowTick();
    }

    /// <summary>
    /// Cold maintenance tick for audits, low-frequency telemetry, and memory hygiene.
    /// </summary>
    public interface IFrostTickable
    {
        /// <summary>
        /// Called by SystemDispatcher on the fixed 300-frame maintenance cadence.
        /// </summary>
        void FrostTick();
    }
}
