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

using System.Runtime.InteropServices;
using System.Threading;
using UnityEngine;

namespace Hecton8.Core
{
    /// <summary>
    /// Fixed slot map for the dispatcher-owned H8 time array.
    /// </summary>
    public enum H8TimeSlot : int
    {
        Time = 0,
        DeltaTime = 1,
        UnscaledTime = 2,
        UnscaledDeltaTime = 3,
        Count = 4
    }

    /// <summary>
    /// Blittable snapshot of the dispatcher-owned time state.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public readonly struct H8TimeSnapshot
    {
        [FieldOffset(0)]
        public readonly double Time;

        [FieldOffset(8)]
        public readonly double DeltaTime;

        [FieldOffset(16)]
        public readonly double UnscaledTime;

        [FieldOffset(24)]
        public readonly double UnscaledDeltaTime;

        public H8TimeSnapshot(double time, double deltaTime, double unscaledTime, double unscaledDeltaTime)
        {
            Time = time;
            DeltaTime = deltaTime;
            UnscaledTime = unscaledTime;
            UnscaledDeltaTime = unscaledDeltaTime;
        }
    }

    /// <summary>
    /// Authoritative runtime time source. Do not read Unity Time for simulation decisions.
    /// </summary>
    public interface ITickDispatcher
    {
        float TimeDilationScalar { get; }
        bool SimulationPaused { get; }
        double DilatedTimeSeconds { get; }
        double UnscaledTimeSeconds { get; }
        H8TimeSnapshot TimeSnapshot { get; }

        void RequestTimeDilation(float scalar, uint reasonHash = 0u);
        void RequestHeadlessTimeDilation(float scalar, uint reasonHash = 0u);
        void RequestCoreTickDilation(float scalar, int frameCount, uint reasonHash = 0u);
        void RequestSimulationPause(bool paused, uint reasonHash = 0u);
        void RequestAupPreShiftPause(uint shiftFrameId);
        Awaitable DelayDilated(float seconds, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// 60 Hz deterministic tick lane for high-priority simulation that does not need every rendered frame.
    /// </summary>
    public interface IFastTickable
    {
        void FastTick(float deltaTime);
    }

    /// <summary>
    /// Per-frame tick lane. Replacement for Update().
    /// Use for input, movement, animation, and UI logic.
    /// </summary>
    public interface ITickable : IUpdatable
    {
        /// <param name="deltaTime">Frame delta supplied by the dispatcher. Do not read Unity frame time inside implementations.</param>
        new void Tick(float deltaTime);
    }

    /// <summary>
    /// Fixed-step tick lane. Replacement for FixedUpdate().
    /// Use for Rigidbody motion and synchronous physics checks.
    /// </summary>
    public interface IFixedTickable
    {
        /// <param name="fixedDeltaTime">Fixed delta supplied by the dispatcher. Do not read Unity fixed time inside implementations.</param>
        void FixedTick(float fixedDeltaTime);
    }

    /// <summary>
    /// Slow tick lane, normally about twice per second.
    /// Use for base life support, AI decisions, autosave hints, and work not needed every frame.
    /// </summary>
    public interface ISlowTickable
    {
        /// <summary>
        /// Called on the configured 10 Hz slow cadence. Delta time is intentionally not passed.
        /// </summary>
        void SlowTick();
    }

    /// <summary>
    /// 1 Hz cold maintenance tick for work that should never ride the render frame.
    /// </summary>
    public interface IColdTickable
    {
        void ColdTick();
    }

    /// <summary>
    /// 0.2 Hz frost maintenance tick for audits, low-frequency telemetry, and memory hygiene.
    /// </summary>
    public interface IFrostTickable
    {
        /// <summary>
        /// Called by SystemDispatcher on the fixed 5 second maintenance cadence.
        /// </summary>
        void FrostTick();
    }

    /// <summary>
    /// 60 Hz UI/menu lane that drains on unscaled time while simulation lanes are paused.
    /// </summary>
    public interface IUnscaledFastTickable
    {
        void UnscaledFastTick(float unscaledDeltaTime);
    }

    /// <summary>
    /// Dilated delay helper. Replaces managed timer waits for gameplay.
    /// </summary>
    public static class AwaitableExtension
    {
        public static async Awaitable DelayDilated(float seconds, CancellationToken cancellationToken = default)
        {
            if (float.IsNaN(seconds) || float.IsInfinity(seconds) || seconds <= 0f)
                return;

            double remainingSeconds = seconds;
            ITickDispatcher dispatcher = GlobalRegistry.TickDispatcher;
            while (remainingSeconds > 0d)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await AwaitableDebtMonitor.NextFrameAsync(cancellationToken: cancellationToken);

                if (dispatcher == null)
                    dispatcher = GlobalRegistry.TickDispatcher;

                H8TimeSnapshot snapshot = dispatcher != null
                    ? dispatcher.TimeSnapshot
                    : new H8TimeSnapshot(0d, SystemDispatcher.CurrentFrameDeltaTime, 0d, SystemDispatcher.CurrentFrameUnscaledDeltaTime);
                double deltaTime = snapshot.DeltaTime;
                if (deltaTime > 0d && double.IsFinite(deltaTime))
                    remainingSeconds -= deltaTime;
            }
        }
    }
}
