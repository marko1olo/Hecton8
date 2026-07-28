// ============================================================================
// HECTON-8 - PowerDrainBillingRuntime.cs
//
// The consumer end of the PowerDrainSignal lane.
//
// DEFECT THIS CLOSES: PowerDrainSignal had two producers and zero consumers.
// LaserCutterDodRuntime.cs:1306 and Fabricator.cs:3433 both published a watt
// figure; the only reader was the unused wrapper
// GlobalSignals.TryDequeuePowerDrain (GlobalSignals.LegacyFacade.cs:1139),
// which had no caller of its own. The laser cutter computed its battery cost
// every hit and nothing was ever charged, so cutting was free.
//
// ROUTE. Typed SignalBus<PowerDrainSignal> snapshot in, cached IPowerGridService
// contract out - routes 2 and 1 of SYSTEMS_CONTRACTS.md:19. No new lane, no new
// registry slot, no new vault buffer, and no extension of the legacy
// GlobalSignals direct-queue surface.
//
// OWNERSHIP. This class owns NO energy. PowerGridManager remains the single
// energy owner; TryQueueWirelessToolDrain reserves against it and the owner's
// own phase applies the debit (PowerGrid.ConsumeReservedWirelessToolDemand ->
// BatteryBankModule.TryConsumeDirectGridEnergy, PowerGrid.cs:380). This is the
// missing adapter between the signal and that owner, not a second owner.
//
// CADENCE. Per-frame IUpdatable, deliberately. The lane snapshot is rebuilt
// every frame at the POST_SIMULATION flush
// (GlobalSignals.RuntimeLifecycle.cs:350), so a 10 Hz ISlowTickable would only
// ever see the snapshot of whichever frame it happened to land on and would
// silently drop the drains published on every other frame - the same
// charged-to-nothing failure in a quieter form. The monotonic
// SignalBus<T>.SnapshotGeneration guard makes consumption exactly-once, and the
// dispatcher-supplied delta is what converts watts into watt-seconds honestly.
// No logic runs in Update/LateUpdate/FixedUpdate (COMMON_SENSE.md:47).
// ============================================================================

using System;
using Hecton8.Core;
using Hecton8.Core.Contracts.Signals;
using UnityEngine;

namespace Hecton8.Power
{
    /// <summary>
    /// Drains the <c>PowerDrainSignal</c> lane each frame and charges the live drains
    /// against the power runtime's wireless tool budget through <see cref="IPowerGridService"/>.
    /// </summary>
    public sealed class PowerDrainBillingRuntime : IUpdatable, IGlobalRegistryHotSwapListener, IDisposable
    {
        private const PriorityLayer TickLayer = PriorityLayer.Environment;

        private static PowerDrainBillingRuntime s_active;

        private int _lastConsumedSnapshotGeneration;
        private float _carriedResidualWattSeconds;
        private float _billedEnergyWattSecondsTotal;
        private float _unpaidEnergyWattSecondsTotal;
        private int _unpaidTickCount;
        private int _billedDrainCount;
        private bool _registeredUpdatable;
        private bool _registeredHotSwap;
        private bool _shutdown;

        /// <summary>Domain reload is disabled on this project, so every static must be reset explicitly.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            s_active = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            if (!Application.isPlaying || s_active != null)
                return;

            PowerDrainBillingRuntime runtime = new PowerDrainBillingRuntime();
            s_active = runtime;
            runtime.Initialize();
        }

        private void Initialize()
        {
            _shutdown = false;
            TryRegisterHotSwapListener();
            TryRegisterTick();
            Application.quitting -= ShutdownActive;
            Application.quitting += ShutdownActive;
        }

        private static void ShutdownActive()
        {
            PowerDrainBillingRuntime runtime = s_active;
            if (runtime != null)
                runtime.Dispose();
        }

        /// <summary>Total energy the power runtime actually reserved for signalled tool drains this session.</summary>
        public static float BilledEnergyWattSeconds
        {
            get
            {
                PowerDrainBillingRuntime runtime = s_active;
                return runtime != null ? runtime._billedEnergyWattSecondsTotal : 0f;
            }
        }

        /// <summary>
        /// Total energy that was drawn but could not be paid for - an empty or absent battery bank.
        /// A rising value is a real brownout, not a routing fault.
        /// </summary>
        public static float UnpaidEnergyWattSeconds
        {
            get
            {
                PowerDrainBillingRuntime runtime = s_active;
                return runtime != null ? runtime._unpaidEnergyWattSecondsTotal : 0f;
            }
        }

        /// <summary>Reads the billing counters without exposing the runtime instance.</summary>
        /// <param name="billedEnergyWattSeconds">Energy successfully reserved against the power runtime.</param>
        /// <param name="unpaidEnergyWattSeconds">Energy drawn that the power runtime could not cover.</param>
        /// <param name="billedDrainCount">Number of individual payloads charged.</param>
        /// <param name="unpaidTickCount">Number of ticks that ended with an unpaid remainder.</param>
        /// <returns>True when a runtime instance exists to report.</returns>
        public static bool TryReadBillingState(
            out float billedEnergyWattSeconds,
            out float unpaidEnergyWattSeconds,
            out int billedDrainCount,
            out int unpaidTickCount)
        {
            PowerDrainBillingRuntime runtime = s_active;
            if (runtime == null)
            {
                billedEnergyWattSeconds = 0f;
                unpaidEnergyWattSeconds = 0f;
                billedDrainCount = 0;
                unpaidTickCount = 0;
                return false;
            }

            billedEnergyWattSeconds = runtime._billedEnergyWattSecondsTotal;
            unpaidEnergyWattSeconds = runtime._unpaidEnergyWattSecondsTotal;
            billedDrainCount = runtime._billedDrainCount;
            unpaidTickCount = runtime._unpaidTickCount;
            return true;
        }

        /// <summary>
        /// Reads this frame's <c>PowerDrainSignal</c> snapshot and charges the live drains.
        /// Allocation-free: a span walk over unmanaged payloads, no LINQ, no closures, no string work.
        /// </summary>
        /// <param name="deltaTime">Dispatcher-supplied frame delta, used to turn watts into watt-seconds.</param>
        public void Tick(float deltaTime)
        {
            if (_shutdown)
                return;

            int snapshotGeneration = SignalBus<PowerDrainSignal>.SnapshotGeneration;
            if (snapshotGeneration == 0 || snapshotGeneration == _lastConsumedSnapshotGeneration)
                return;

            _lastConsumedSnapshotGeneration = snapshotGeneration;

            ReadOnlySpan<PowerDrainSignal> drains = SignalBus<PowerDrainSignal>.GetFrameSnapshot();
            if (drains.Length == 0)
                return;

            float billableWatts = 0f;
            int billableCount = 0;
            for (int index = 0; index < drains.Length; index++)
            {
                PowerDrainSignal drain = drains[index];
                if (!PowerDrainBilling.IsBillableAsWirelessToolDrain(drain.Reason, drain.Flags))
                    continue;

                float previousWatts = billableWatts;
                billableWatts = PowerDrainBilling.AccumulateBillableWatts(billableWatts, drain.Watts);
                if (billableWatts > previousWatts)
                    billableCount++;
            }

            if (billableCount == 0)
                return;

            float submittedEnergy = PowerDrainBilling.ResolveSubmittedEnergyWattSeconds(
                billableWatts,
                deltaTime,
                _carriedResidualWattSeconds,
                out float nextResidual);
            _carriedResidualWattSeconds = nextResidual;
            if (submittedEnergy <= 0f)
                return;

            float grantedEnergy = 0f;
            IPowerGridService powerGrid = GlobalRegistry.PowerGrid;
            if (powerGrid != null && !powerGrid.TryQueueWirelessToolDrain(submittedEnergy, out grantedEnergy))
                grantedEnergy = 0f;

            if (grantedEnergy > 0f)
            {
                _billedEnergyWattSecondsTotal = PowerDrainBilling.AccumulateEnergyWattSeconds(
                    _billedEnergyWattSecondsTotal,
                    grantedEnergy);
                if (_billedDrainCount < int.MaxValue - billableCount)
                    _billedDrainCount += billableCount;
            }

            float unpaidEnergy = PowerDrainBilling.ResolveUnpaidEnergyWattSeconds(submittedEnergy, grantedEnergy);
            if (unpaidEnergy <= 0f)
                return;

            _unpaidEnergyWattSecondsTotal = PowerDrainBilling.AccumulateEnergyWattSeconds(
                _unpaidEnergyWattSecondsTotal,
                unpaidEnergy);
            if (_unpaidTickCount < int.MaxValue)
                _unpaidTickCount++;
        }

        /// <summary>
        /// Re-arms the dispatcher registration when the dispatcher slot is replaced at runtime.
        /// This is also how the initial registration lands: the BeforeSceneLoad bootstrap runs
        /// before the bootstrapper publishes the dispatcher, so the first attempt legitimately
        /// fails and this callback is the retry.
        /// </summary>
        /// <param name="serviceSlot">Registry slot that changed.</param>
        /// <param name="previousService">Previous service instance.</param>
        /// <param name="currentService">Current service instance.</param>
        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot != GlobalRegistryServiceSlot.Dispatcher || _shutdown)
                return;

            UnregisterTick();
            if (currentService != null)
                TryRegisterTick();
        }

        private void TryRegisterTick()
        {
            if (_registeredUpdatable || GlobalRegistry.Dispatcher == null)
                return;

            _registeredUpdatable = GlobalRegistry.TryRegisterUpdatable(this, TickLayer);
        }

        private void UnregisterTick()
        {
            if (!_registeredUpdatable)
                return;

            GlobalRegistry.UnregisterUpdatable(this, TickLayer);
            _registeredUpdatable = false;
        }

        private void TryRegisterHotSwapListener()
        {
            if (_registeredHotSwap)
                return;

            _registeredHotSwap = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterHotSwapListener()
        {
            if (!_registeredHotSwap)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _registeredHotSwap = false;
        }

        /// <summary>Releases the dispatcher lane, the hot-swap lane, and the quit subscription.</summary>
        public void Dispose()
        {
            if (_shutdown)
                return;

            _shutdown = true;
            Application.quitting -= ShutdownActive;
            UnregisterTick();
            TryUnregisterHotSwapListener();
            _carriedResidualWattSeconds = 0f;
            _lastConsumedSnapshotGeneration = 0;
            if (ReferenceEquals(s_active, this))
                s_active = null;
        }
    }
}
