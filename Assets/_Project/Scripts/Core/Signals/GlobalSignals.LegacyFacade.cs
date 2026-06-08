using System.Runtime.CompilerServices;
using System.Threading;
using Hecton8.Core.Contracts.Signals;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using AbsoluteUniversePosition = Hecton8.World.AbsoluteUniversePosition;
using AbsoluteUniversePositionBlit = Hecton8.World.AbsoluteUniversePositionBlit;

namespace Hecton8.Core
{
    public static partial class GlobalSignals
    {
        private static bool TryPushLegacy<T>(in T signal)
            where T : unmanaged, ISignal
        {
            if (SignalBus<T>.TryPush(in signal))
                return true;

            SignalBridgeState.RecordLegacyPublishDrop();
            return false;
        }

        [global::System.Obsolete("Legacy publish facade is retired. Use SignalBus<T> or an owner route wrapper.", true)]
        public static void Publish(in CombatDamageSignal signal)
        {
            EnsureInitialized();
            CombatDamageSignal sanitizedSignal = signal;
            int guardCode = SignalPayloadFiniteGuards.Sanitize(ref sanitizedSignal);
            if (guardCode != 0)
            {
                GlobalTelemetryBus.PublishMathGuardInvalidNumber(guardCode);
                return;
            }

            _latestDamageSignal = sanitizedSignal;
            AdvanceSignalSequence(ref _latestDamageSignalSequence);
            TryPushLegacy(in sanitizedSignal);
        }

        /// <summary>Queues one physics impact packet from the main thread.</summary>
        [global::System.Obsolete("Legacy publish facade is retired. Use SignalBus<T> or an owner route wrapper.", true)]
        public static void Publish(in ImpactSignal signal)
        {
            EnsureInitialized();
            ImpactSignal sanitizedSignal = signal;
            int guardCode = SignalPayloadFiniteGuards.Sanitize(ref sanitizedSignal);
            if (guardCode != 0)
                GlobalTelemetryBus.PublishMathGuardInvalidNumber(guardCode);

            TryPushLegacy(in sanitizedSignal);
        }

        /// <summary>Queues one high-speed kinematic CCD impact packet on the typed native lane.</summary>
        [global::System.Obsolete("Legacy publish facade is retired. Use SignalBus<T> or an owner route wrapper.", true)]
        public static void Publish(in HighSpeedImpactSignal signal)
        {
            EnsureInitialized();
            TryPushLegacy(in signal);
        }

        /// <summary>Queues one haptic rupture request on the typed native lane.</summary>
        [global::System.Obsolete("Legacy publish facade is retired. Use SignalBus<T> or an owner route wrapper.", true)]
        public static void Publish(in HapticRequest signal)
        {
            EnsureInitialized();
            TryPushLegacy(in signal);
        }

        /// <summary>Queues one final haptic synthesis envelope on the typed native lane.</summary>
        [global::System.Obsolete("Legacy publish facade is retired. Use SignalBus<T> or an owner route wrapper.", true)]
        public static void Publish(in HapticPulseSignal signal)
        {
            EnsureInitialized();
            TryPushLegacy(in signal);
        }

        /// <summary>Queues one player state packet on the typed native lane.</summary>
        [global::System.Obsolete("Legacy publish facade is retired. Use SignalBus<T> or an owner route wrapper.", true)]
        public static void Publish(in PlayerStateSignal signal)
        {
            EnsureInitialized();
            _latestPlayerStateSignal = signal;
            AdvanceSignalSequence(ref _latestPlayerStateSignalSequence);
            TryPushLegacy(in signal);
        }

        /// <summary>Queues one player survival-vitals dirty packet on the typed native lane.</summary>
        [global::System.Obsolete("Legacy publish facade is retired. Use SignalBus<T> or an owner route wrapper.", true)]
        public static void Publish(in SurvivalVitalsChangedSignal signal)
        {
            EnsureInitialized();
            if (!SurvivalSignalRoute.TryQueueVitals(in signal))
                SignalBridgeState.RecordLegacyPublishDrop();
        }

        /// <summary>Queues one hull deformation VFX packet for downstream audio and feedback systems.</summary>
        [global::System.Obsolete("Legacy publish facade is retired. Use SignalBus<T> or an owner route wrapper.", true)]
        public static void Publish(in HullDeformedSignal signal)
        {
            EnsureInitialized();
            TryPushLegacy(in signal);
        }

        /// <summary>Queues one hull repair completion packet for atmosphere and VFX consumers.</summary>
        [global::System.Obsolete("Legacy publish facade is retired. Use SignalBus<T> or an owner route wrapper.", true)]
        public static void Publish(in HullRepairedSignal signal)
        {
            EnsureInitialized();
            TryPushLegacy(in signal);
        }

        [global::System.Obsolete("Legacy publish facade is retired. Use SignalBus<T> or an owner route wrapper.", true)]
        public static void Publish(in BaseModuleCompromisedSignal signal)
        {
            EnsureInitialized();
            TryPushLegacy(in signal);
        }

        /// <summary>Queues one player-entered-base packet for habitat atmosphere hibernation gates.</summary>
        [global::System.Obsolete("Legacy publish facade is retired. Use SignalBus<T> or an owner route wrapper.", true)]
        public static void Publish(in PlayerBaseEnterSignal signal)
        {
            EnsureInitialized();
            TryPushLegacy(in signal);
        }

        /// <summary>Queues one player-exited-base packet for habitat atmosphere hibernation gates.</summary>
        [global::System.Obsolete("Legacy publish facade is retired. Use SignalBus<T> or an owner route wrapper.", true)]
        public static void Publish(in PlayerBaseExitSignal signal)
        {
            EnsureInitialized();
            TryPushLegacy(in signal);
        }

        /// <summary>Queues one AUP shift broadcast packet from the main thread.</summary>
        [global::System.Obsolete("Legacy publish facade is retired. Use SignalBus<T> or an owner route wrapper.", true)]
        public static void Publish(in AupPreShiftSignal signal)
        {
            AupSignalRoute.TryQueuePreShift(in signal);
        }

        /// <summary>Queues one AUP shift broadcast packet from the main thread.</summary>
        [global::System.Obsolete("Legacy publish facade is retired. Use SignalBus<T> or an owner route wrapper.", true)]
        public static void Publish(in AupShiftSignal signal)
        {
            AupSignalRoute.TryQueueShift(in signal);
        }

        /// <summary>Queues one drop-pod landing anchor packet with AUP precision.</summary>
        [global::System.Obsolete("Legacy publish facade is retired. Use SignalBus<T> or an owner route wrapper.", true)]
        public static void Publish(in DropPodLandedSignal signal)
        {
            EnsureInitialized();
            TryPushLegacy(in signal);
        }

        /// <summary>Queues one absolute-position temperature change packet.</summary>
        [global::System.Obsolete("Legacy publish facade is retired. Use SignalBus<T> or an owner route wrapper.", true)]
        public static void Publish(in TemperatureChangedSignal signal)
        {
            EnsureInitialized();
            TryPushLegacy(in signal);
        }

        /// <summary>Queues one logistics brownout packet from the main thread.</summary>
        [global::System.Obsolete("Legacy publish facade is retired. Use SignalBus<T> or an owner route wrapper.", true)]
        public static void Publish(in BrownoutSignal signal)
        {
            EnsureInitialized();
            TryPushLegacy(in signal);
        }

        /// <summary>Queues one ecosystem debris spawn packet from the main thread.</summary>
        [global::System.Obsolete("Legacy publish facade is retired. Use SignalBus<T> or an owner route wrapper.", true)]
        public static void Publish(in DebrisSpawnSignal signal)
        {
            EnsureInitialized();
            TryPushLegacy(in signal);
        }

        /// <summary>Queues one armor deflection packet from the main thread.</summary>
        [global::System.Obsolete("Legacy publish facade is retired. Use SignalBus<T> or an owner route wrapper.", true)]
        public static void Publish(in DeflectSignal signal)
        {
            EnsureInitialized();
            TryPushLegacy(in signal);
        }

        /// <summary>Queues one entity death packet from the main thread.</summary>
        [global::System.Obsolete("Legacy publish facade is retired. Use SignalBus<T> or an owner route wrapper.", true)]
        public static void Publish(in EntityDeathSignal signal)
        {
            EnsureInitialized();
            TryPushLegacy(in signal);
        }

        /// <summary>Queues one entity spawn packet from the main thread.</summary>
        [global::System.Obsolete("Legacy publish facade is retired. Use SignalBus<T> or an owner route wrapper.", true)]
        public static void Publish(in EntitySpawnSignal signal)
        {
            EnsureInitialized();
            TryPushLegacy(in signal);
        }

        /// <summary>Queues one narrative solar flare packet from the main thread.</summary>
        [global::System.Obsolete("Legacy publish facade is retired. Use SignalBus<T> or an owner route wrapper.", true)]
        public static void Publish(in SolarFlareSignal signal)
        {
            EnsureInitialized();
            TryPushLegacy(in signal);
        }

        /// <summary>Queues one origin rebase packet from the main thread.</summary>
        [global::System.Obsolete("Legacy publish facade is retired. Use SignalBus<T> or an owner route wrapper.", true)]
        public static void Publish(in RebaseSignal signal)
        {
            EnsureInitialized();
            TryPushLegacy(in signal);
        }

        /// <summary>Queues one input control packet from the main thread.</summary>
        [global::System.Obsolete("Legacy publish facade is retired. Use SignalBus<T> or an owner route wrapper.", true)]
        public static void Publish(in ControlSignal signal)
        {
            EnsureInitialized();
            TryPushLegacy(in signal);
        }

        /// <summary>Queues one runtime anomaly packet from the main thread.</summary>
        [global::System.Obsolete("Legacy publish facade is retired. Use SignalBus<T> or an owner route wrapper.", true)]
        public static void Publish(in AnomalySignal signal)
        {
            EnsureInitialized();
            TryPushLegacy(in signal);
        }

        /// <summary>Queues one telemetry anomaly packet from the main thread.</summary>
        [global::System.Obsolete("Legacy publish facade is retired. Use SignalBus<T> or an owner route wrapper.", true)]
        public static void Publish(in TelemetryAnomalySignal signal)
        {
            EnsureInitialized();
            TryPushLegacy(in signal);
        }

        /// <summary>Queues one postmortem crash telemetry packet from the main thread.</summary>
        [global::System.Obsolete("Legacy publish facade is retired. Use SignalBus<T> or an owner route wrapper.", true)]
        public static void Publish(in CrashTelemetrySignal signal)
        {
            EnsureInitialized();
            TryPushLegacy(in signal);
        }

        /// <summary>Queues one habitat construction packet from the main thread.</summary>
        [global::System.Obsolete("Legacy publish facade is retired. Use SignalBus<T> or an owner route wrapper.", true)]
        public static void Publish(in HabitatConstructionSignal signal)
        {
            EnsureInitialized();
            TryPushLegacy(in signal);
        }

        /// <summary>Queues one habitat deconstruction request packet from the main thread.</summary>
        [global::System.Obsolete("Legacy publish facade is retired. Use SignalBus<T> or an owner route wrapper.", true)]
        public static void Publish(in DeconstructRequestSignal signal)
        {
            EnsureInitialized();
            TryPushLegacy(in signal);
        }

        /// <summary>Queues one habitat deconstruction result packet from the main thread.</summary>
        [global::System.Obsolete("Legacy publish facade is retired. Use SignalBus<T> or an owner route wrapper.", true)]
        public static void Publish(in DeconstructResultSignal signal)
        {
            EnsureInitialized();
            TryPushLegacy(in signal);
        }

        /// <summary>Queues one persistence-facing habitat deletion delta packet from the main thread.</summary>
        [global::System.Obsolete("Legacy publish facade is retired. Use SignalBus<T> or an owner route wrapper.", true)]
        public static void Publish(in ModuleDeconstructSignal signal)
        {
            EnsureInitialized();
            TryPushLegacy(in signal);
        }

        /// <summary>Queues one player-vital warning packet from the main thread.</summary>
        [global::System.Obsolete("Legacy publish facade is retired. Use SignalBus<T> or an owner route wrapper.", true)]
        public static void Publish(in VitalWarningSignal signal)
        {
            EnsureInitialized();
            TryPushLegacy(in signal);
        }

        /// <summary>Queues one crush-depth warning packet from the main thread.</summary>
        [global::System.Obsolete("Legacy publish facade is retired. Use SignalBus<T> or an owner route wrapper.", true)]
        public static void Publish(in CrushWarningSignal signal)
        {
            EnsureInitialized();
            TryPushLegacy(in signal);
        }

        /// <summary>Queues one vocal warning packet from the main thread.</summary>
        [global::System.Obsolete("Legacy publish facade is retired. Use SignalBus<T> or an owner route wrapper.", true)]
        public static void Publish(in VocalWarningSignal signal)
        {
            EnsureInitialized();
            TryPushLegacy(in signal);
        }

        /// <summary>Queues one hash-addressed protagonist voice packet from the main thread.</summary>
        [global::System.Obsolete("Legacy publish facade is retired. Use SignalBus<T> or an owner route wrapper.", true)]
        public static void Publish(in VocalCueSignal signal)
        {
            EnsureInitialized();
            VocalCueSignal sanitizedSignal = signal;
            int guardCode = SignalPayloadFiniteGuards.Sanitize(ref sanitizedSignal);
            if (guardCode != 0)
                GlobalTelemetryBus.PublishMathGuardInvalidNumber(guardCode);
            TryPushLegacy(in sanitizedSignal);
        }

        /// <summary>Queues one subtitle packet from the main thread.</summary>
        [global::System.Obsolete("Legacy publish facade is retired. Use SignalBus<T> or an owner route wrapper.", true)]
        public static void Publish(in SubtitleSignal signal)
        {
            EnsureInitialized();
            TryPushLegacy(in signal);
        }

        /// <summary>Queues one editor data reload packet from the main thread.</summary>
        [global::System.Obsolete("Legacy publish facade is retired. Use SignalBus<T> or an owner route wrapper.", true)]
        public static void Publish(in DataReloadSignal signal)
        {
            EnsureInitialized();
            TryPushLegacy(in signal);
        }

        /// <summary>Queues one memory pressure packet from the main thread.</summary>
        [global::System.Obsolete("Legacy publish facade is retired. Use SignalBus<T> or an owner route wrapper.", true)]
        public static void Publish(in MemoryPressureSignal signal)
        {
            EnsureInitialized();
            TryPushLegacy(in signal);
        }

        /// <summary>Queues one vault pointer relocation packet from the main thread.</summary>
        [global::System.Obsolete("Legacy publish facade is retired. Use SignalBus<T> or an owner route wrapper.", true)]
        public static void Publish(in MemoryAddressShiftSignal signal)
        {
            EnsureInitialized();
            TryPushLegacy(in signal);
        }

        /// <summary>Queues one runtime resolution/mip residency transition packet.</summary>
        [global::System.Obsolete("Legacy publish facade is retired. Use SignalBus<T> or an owner route wrapper.", true)]
        public static void Publish(in ResolutionChangedSignal signal)
        {
            EnsureInitialized();
            TryPushLegacy(in signal);
        }

        /// <summary>Queues one homeostasis health-index packet.</summary>
        [global::System.Obsolete("Legacy publish facade is retired. Use SignalBus<T> or an owner route wrapper.", true)]
        public static void Publish(in SystemHealthIndexSignal signal)
        {
            EnsureInitialized();
            TryPushLegacy(in signal);
        }

        /// <summary>Queues one CPU job-admission starvation diagnostic signal.</summary>
        [global::System.Obsolete("Legacy publish facade is retired. Use SignalBus<T> or an owner route wrapper.", true)]
        public static void Publish(in CpuStarvationSignal signal)
        {
            EnsureInitialized();
            TryPushLegacy(in signal);
        }

        /// <summary>Queues one acoustic ping packet from the main thread.</summary>
        [global::System.Obsolete("Legacy publish facade is retired. Use SignalBus<T> or an owner route wrapper.", true)]
        public static void Publish(in AcousticPingSignal signal)
        {
            EnsureInitialized();
            _latestAcousticPingSignal = signal;
            AdvanceSignalSequence(ref _latestAcousticPingSignalSequence);
            TryPushLegacy(in signal);
        }

        /// <summary>Queues one movement acoustic packet from the main thread.</summary>
        [global::System.Obsolete("Legacy publish facade is retired. Use SignalBus<T> or an owner route wrapper.", true)]
        public static void Publish(in MovementAcousticSignal signal)
        {
            EnsureInitialized();
            TryPushLegacy(in signal);
        }

        /// <summary>Queues one swarm dispersion packet from the main thread.</summary>
        [global::System.Obsolete("Legacy publish facade is retired. Use SignalBus<T> or an owner route wrapper.", true)]
        public static void Publish(in SwarmDispersedSignal signal)
        {
            EnsureInitialized();
            TryPushLegacy(in signal);
        }

        /// <summary>Queues one sonar ping packet from the main thread.</summary>
        [global::System.Obsolete("Legacy publish facade is retired. Use SignalBus<T> or an owner route wrapper.", true)]
        public static void Publish(in SonarPingSignal signal)
        {
            EnsureInitialized();
            TryPushLegacy(in signal);
        }

        /// <summary>Queues one hypoxia packet from the main thread.</summary>
        [global::System.Obsolete("Legacy publish facade is retired. Use SignalBus<T> or an owner route wrapper.", true)]
        public static void Publish(in HypoxiaSignal signal)
        {
            EnsureInitialized();
            TryPushLegacy(in signal);
        }

        /// <summary>Queues one oxygen critical packet from the main thread.</summary>
        [global::System.Obsolete("Legacy publish facade is retired. Use SignalBus<T> or an owner route wrapper.", true)]
        public static void Publish(in OxygenCriticalSignal signal)
        {
            EnsureInitialized();
            TryPushLegacy(in signal);
        }

        /// <summary>Queues one interaction UI packet from the main thread.</summary>
        [global::System.Obsolete("Legacy publish facade is retired. Use SignalBus<T> or an owner route wrapper.", true)]
        public static void Publish(in InteractionUiSignal signal)
        {
            EnsureInitialized();
            TryPushLegacy(in signal);
        }

        /// <summary>Queues one UI rescale request packet from the main thread.</summary>
        [global::System.Obsolete("Legacy publish facade is retired. Use SignalBus<T> or an owner route wrapper.", true)]
        public static void Publish(in UIRescaleRequestSignal signal)
        {
            EnsureInitialized();
            TryPushLegacy(in signal);
        }

        /// <summary>Queues one fluid incursion packet from the main thread.</summary>
        [global::System.Obsolete("Legacy publish facade is retired. Use SignalBus<T> or an owner route wrapper.", true)]
        public static void Publish(in FluidIncursionSignal signal)
        {
            EnsureInitialized();
            TryPushLegacy(in signal);
        }

        /// <summary>Queues one submarine flood mass-state packet from the main thread.</summary>
        [global::System.Obsolete("Legacy publish facade is retired. Use SignalBus<T> or an owner route wrapper.", true)]
        public static void Publish(in SubmarineFloodStateSignal signal)
        {
            EnsureInitialized();
            TryPushLegacy(in signal);
        }

        /// <summary>Queues one fluid-density transition packet from the main thread.</summary>
        [global::System.Obsolete("Legacy publish facade is retired. Use SignalBus<T> or an owner route wrapper.", true)]
        public static void Publish(in FluidDensityChangedSignal signal)
        {
            EnsureInitialized();
            _latestFluidDensityChangedSignal = signal;
            AdvanceSignalSequence(ref _latestFluidDensityChangedSignalSequence);
            TryPushLegacy(in signal);
        }

        /// <summary>Queues one fluid pipe rupture packet from the main thread.</summary>
        [global::System.Obsolete("Legacy publish facade is retired. Use SignalBus<T> or an owner route wrapper.", true)]
        public static void Publish(in PipeRuptureSignal signal)
        {
            EnsureInitialized();
            TryPushLegacy(in signal);
        }

        /// <summary>Queues one spectrum scan packet from the main thread.</summary>
        [global::System.Obsolete("Legacy publish facade is retired. Use SignalBus<T> or an owner route wrapper.", true)]
        public static void Publish(in SpectrumScanSignal signal)
        {
            EnsureInitialized();
            TryPushLegacy(in signal);
        }

        /// <summary>Queues one rigidbody sleep packet from the main thread.</summary>
        [global::System.Obsolete("Legacy publish facade is retired. Use SignalBus<T> or an owner route wrapper.", true)]
        public static void Publish(in RigidbodySleepSignal signal)
        {
            EnsureInitialized();
            TryPushLegacy(in signal);
        }

        /// <summary>Queues one scanner-active packet from the main thread.</summary>
        [global::System.Obsolete("Legacy publish facade is retired. Use SignalBus<T> or an owner route wrapper.", true)]
        public static void Publish(in ScannerToolActiveSignal signal)
        {
            EnsureInitialized();
            _latestScannerToolActiveSignal = signal;
            AdvanceSignalSequence(ref _latestScannerToolActiveSignalSequence);
            TryPushLegacy(in signal);
        }

        /// <summary>Queues one scan-complete packet from the main thread.</summary>
        [global::System.Obsolete("Legacy publish facade is retired. Use SignalBus<T> or an owner route wrapper.", true)]
        public static void Publish(in ScanCompleteSignal signal)
        {
            EnsureInitialized();
            TryPushLegacy(in signal);
        }

        /// <summary>Queues one lore-fragment scanned packet from the main thread.</summary>
        [global::System.Obsolete("Legacy publish facade is retired. Use SignalBus<T> or an owner route wrapper.", true)]
        public static void Publish(in LoreFragmentScannedSignal signal)
        {
            EnsureInitialized();
            TryPushLegacy(signal);
        }

        /// <summary>Queues one blueprint-unlocked packet from the main thread.</summary>
        [global::System.Obsolete("Legacy publish facade is retired. Use SignalBus<T> or an owner route wrapper.", true)]
        public static void Publish(in BlueprintUnlockedSignal signal)
        {
            EnsureInitialized();
            TryPushLegacy(in signal);
        }

        /// <summary>Queues one crafting-started packet from the main thread.</summary>
        [global::System.Obsolete("Legacy publish facade is retired. Use SignalBus<T> or an owner route wrapper.", true)]
        public static void Publish(in CraftingStartedSignal signal)
        {
            EnsureInitialized();
            TryPushLegacy(in signal);
        }

        /// <summary>Queues one crafting-completed packet from the main thread.</summary>
        [global::System.Obsolete("Legacy publish facade is retired. Use SignalBus<T> or an owner route wrapper.", true)]
        public static void Publish(in CraftingCompletedSignal signal)
        {
            CraftingSignalRoute.TryQueueCompleted(in signal);
        }

        /// <summary>Queues one tool runtime state packet from the main thread.</summary>
        [global::System.Obsolete("Legacy publish facade is retired. Use SignalBus<T> or an owner route wrapper.", true)]
        public static void Publish(in ToolStateChangedSignal signal)
        {
            EnsureInitialized();
            _latestToolStateChangedSignal = signal;
            AdvanceSignalSequence(ref _latestToolStateChangedSignalSequence);
            TryPushLegacy(in signal);
        }

        /// <summary>Queues one player tool loadout or active-slot dirty packet.</summary>
        [global::System.Obsolete("Legacy publish facade is retired. Use SignalBus<T> or an owner route wrapper.", true)]
        public static void Publish(in ToolLoadoutChangedSignal signal)
        {
            EnsureInitialized();
            TryPushLegacy(in signal);
        }

        /// <summary>Queues one tool acoustic packet from the main thread.</summary>
        [global::System.Obsolete("Legacy publish facade is retired. Use SignalBus<T> or an owner route wrapper.", true)]
        public static void Publish(in ToolAcousticSignal signal)
        {
            EnsureInitialized();
            TryPushLegacy(in signal);
        }

        /// <summary>Queues one power-drain packet from the main thread.</summary>
        [global::System.Obsolete("Legacy publish facade is retired. Use SignalBus<T> or an owner route wrapper.", true)]
        public static void Publish(in PowerDrainSignal signal)
        {
            EnsureInitialized();
            TryPushLegacy(in signal);
        }

        /// <summary>Queues one tool trigger packet from the main thread.</summary>
        [global::System.Obsolete("Legacy publish facade is retired. Use SignalBus<T> or an owner route wrapper.", true)]
        public static void Publish(in ToolTriggerSignal signal)
        {
            EnsureInitialized();
            TryPushLegacy(in signal);
        }

        /// <summary>Queues one HUD notification packet from the main thread.</summary>
        [global::System.Obsolete("Legacy publish facade is retired. Use SignalBus<T> or an owner route wrapper.", true)]
        public static void Publish(in HUDNotificationSignal signal)
        {
            EnsureInitialized();
            TryPushLegacy(in signal);
        }

        /// <summary>Queues one diegetic HUD prompt packet from the main thread.</summary>
        [global::System.Obsolete("Legacy publish facade is retired. Use SignalBus<T> or an owner route wrapper.", true)]
        public static void Publish(in DiegeticHudSignal signal)
        {
            EnsureInitialized();
            TryPushLegacy(in signal);
        }

        /// <summary>Queues one cached platform thermal state packet.</summary>
        [global::System.Obsolete("Legacy publish facade is retired. Use SignalBus<T> or an owner route wrapper.", true)]
        public static void Publish(in ThermalStateChangedSignal signal)
        {
            EnsureInitialized();
            TryPushLegacy(in signal);
        }

        /// <summary>Queues one cached platform battery level packet.</summary>
        [global::System.Obsolete("Legacy publish facade is retired. Use SignalBus<T> or an owner route wrapper.", true)]
        public static void Publish(in BatteryLevelSignal signal)
        {
            EnsureInitialized();
            TryPushLegacy(in signal);
        }

        /// <summary>Queues one inventory item durability update packet.</summary>
        [global::System.Obsolete("Legacy publish facade is retired. Use SignalBus<T> or an owner route wrapper.", true)]
        public static void Publish(in ItemDurabilityChangedSignal signal)
        {
            EnsureInitialized();
            TryPushLegacy(in signal);
        }

        /// <summary>Queues one player delayed-action progress packet.</summary>
        [global::System.Obsolete("Legacy publish facade is retired. Use SignalBus<T> or an owner route wrapper.", true)]
        public static void Publish(in PlayerActionProgressSignal signal)
        {
            EnsureInitialized();
            TryPushLegacy(in signal);
        }

        /// <summary>Queues one player delayed-action completion packet.</summary>
        [global::System.Obsolete("Legacy publish facade is retired. Use SignalBus<T> or an owner route wrapper.", true)]
        public static void Publish(in PlayerActionCompletedSignal signal)
        {
            EnsureInitialized();
            TryPushLegacy(in signal);
        }

        /// <summary>Queues one player delayed-action cancellation packet.</summary>
        [global::System.Obsolete("Legacy publish facade is retired. Use SignalBus<T> or an owner route wrapper.", true)]
        public static void Publish(in PlayerActionCancelledSignal signal)
        {
            EnsureInitialized();
            TryPushLegacy(in signal);
        }

        /// <summary>Queues one scan-log mutation packet.</summary>
        [global::System.Obsolete("Legacy publish facade is retired. Use SignalBus<T> or an owner route wrapper.", true)]
        public static void Publish(in ScanLogChangedSignal signal)
        {
            EnsureInitialized();
            TryPushLegacy(in signal);
        }

        /// <summary>Queues one PDA exchange dirty-state packet.</summary>
        [global::System.Obsolete("Legacy publish facade is retired. Use SignalBus<T> or an owner route wrapper.", true)]
        public static void Publish(in PdaExchangeStateChangedSignal signal)
        {
            EnsureInitialized();
            TryPushLegacy(in signal);
        }

        /// <summary>Queues one vehicle upgrade bitmask mutation packet.</summary>
        [global::System.Obsolete("Legacy publish facade is retired. Use SignalBus<T> or an owner route wrapper.", true)]
        public static void Publish(in VehicleUpgradesChangedSignal signal)
        {
            EnsureInitialized();
            TryPushLegacy(in signal);
        }

        /// <summary>Queues one storage IO backpressure scalar packet from the streaming service.</summary>
        [global::System.Obsolete("Legacy publish facade is retired. Use SignalBus<T> or an owner route wrapper.", true)]
        public static void Publish(in StorageDebtSignal signal)
        {
            EnsureInitialized();
            Volatile.Write(ref _latestStorageDebtMilli, (int)math.round(math.saturate(signal.Debt01) * 1000f));
            Volatile.Write(ref _latestStorageLatencyMilli, (int)math.round(math.max(0f, signal.LatencyEwmaMs)));
            Volatile.Write(ref _latestStorageDebtSequence, unchecked((int)signal.Sequence));
            TryPushLegacy(in signal);
        }

        /// <summary>Queues a visual-only turbulence cover-up packet when IO backpressure is high.</summary>
        [global::System.Obsolete("Legacy publish facade is retired. Use SignalBus<T> or an owner route wrapper.", true)]
        public static void Publish(in StreamingTurbulenceSignal signal)
        {
            EnsureInitialized();
            TryPushLegacy(in signal);
        }

        /// <summary>Queues one orbital prologue atmospheric re-entry state packet.</summary>
        [global::System.Obsolete("Legacy publish facade is retired. Use SignalBus<T> or an owner route wrapper.", true)]
        public static void Publish(in AtmosphericReentrySignal signal)
        {
            EnsureInitialized();
            TryPushLegacy(in signal);
        }

        /// <summary>Queues one orbital prologue completion handoff packet.</summary>
        [global::System.Obsolete("Legacy publish facade is retired. Use SignalBus<T> or an owner route wrapper.", true)]
        public static void Publish(in PrologueCompleteSignal signal)
        {
            EnsureInitialized();
            TryPushLegacy(in signal);
        }

        /// <summary>Queues one physical cockpit manual override latch packet.</summary>
        [global::System.Obsolete("Legacy publish facade is retired. Use SignalBus<T> or an owner route wrapper.", true)]
        public static void Publish(in ManualOverridePulledSignal signal)
        {
            EnsureInitialized();
            TryPushLegacy(in signal);
        }

        /// <summary>Queues one recon data packet from the main thread.</summary>
        [global::System.Obsolete("Legacy publish facade is retired. Use SignalBus<T> or an owner route wrapper.", true)]
        public static void Publish(in ReconDataSignal signal)
        {
            EnsureInitialized();
            TryPushLegacy(in signal);
        }

        /// <summary>Queues one save lifecycle packet from the main thread.</summary>
        [global::System.Obsolete("Legacy publish facade is retired. Use SignalBus<T> or an owner route wrapper.", true)]
        public static void Publish(in SaveLifecycleSignal signal)
        {
            EnsureInitialized();
            TryPushLegacy(in signal);
        }

        /// <summary>Queues one macro database hydration packet on the typed native lane.</summary>
        [global::System.Obsolete("Legacy publish facade is retired. Use SignalBus<T> or an owner route wrapper.", true)]
        public static void Publish(in MacroDatabaseSectorHydrationSignal signal)
        {
            EnsureInitialized();
            TryPushLegacy(in signal);
        }

        /// <summary>Queues one WFC outpost generation completion packet on the typed native lane.</summary>
        [global::System.Obsolete("Legacy publish facade is retired. Use SignalBus<T> or an owner route wrapper.", true)]
        public static void Publish(in WfcOutpostGeneratedSignal signal)
        {
            EnsureInitialized();
            TryPushLegacy(in signal);
        }

        /// <summary>Queues one WFC outpost mutable-cell state change on the typed native lane.</summary>
        [global::System.Obsolete("Legacy publish facade is retired. Use SignalBus<T> or an owner route wrapper.", true)]
        public static void Publish(in WfcOutpostStateChangedSignal signal)
        {
            EnsureInitialized();
            TryPushLegacy(in signal);
        }

        /// <summary>Queues one WFC outpost door-power packet on the typed native lane.</summary>
        [global::System.Obsolete("Legacy publish facade is retired. Use SignalBus<T> or an owner route wrapper.", true)]
        public static void Publish(in WfcOutpostDoorPowerSignal signal)
        {
            EnsureInitialized();
            TryPushLegacy(in signal);
        }

        /// <summary>Queues one compliance violation packet from the main thread.</summary>
        [global::System.Obsolete("Legacy publish facade is retired. Use SignalBus<T> or an owner route wrapper.", true)]
        public static void Publish(in ComplianceViolationSignal signal)
        {
            EnsureInitialized();
            TryPushLegacy(in signal);
        }

        /// <summary>Queues one global time sync packet from the main thread.</summary>
        [global::System.Obsolete("Legacy publish facade is retired. Use SignalBus<T> or an owner route wrapper.", true)]
        public static void Publish(in GlobalTimeSyncSignal signal)
        {
            EnsureInitialized();
            TryPushLegacy(in signal);
        }

        /// <summary>Queues one deterministic seismic/tide packet from the main thread.</summary>
        [global::System.Obsolete("Legacy publish facade is retired. Use SignalBus<T> or an owner route wrapper.", true)]
        public static void Publish(in SeismicSignal signal)
        {
            EnsureInitialized();
            SeismicSignal sanitizedSignal = signal;
            int guardCode = SignalPayloadFiniteGuards.Sanitize(ref sanitizedSignal);
            if (guardCode != 0)
                GlobalTelemetryBus.PublishMathGuardInvalidNumber(guardCode);

            _latestSeismicSignal = sanitizedSignal;
            AdvanceSignalSequence(ref _latestSeismicSignalSequence);
            TryPushLegacy(in sanitizedSignal);
        }

        /// <summary>Queues one authoritative dispatcher time-dilation packet from the main thread.</summary>
        [global::System.Obsolete("Legacy publish facade is retired. Use SignalBus<T> or an owner route wrapper.", true)]
        public static void Publish(in TimeDilationSignal signal)
        {
            SimulationSignalRoute.TryQueueTimeDilation(in signal);
        }

        /// <summary>Queues one pause/unpause packet from the main thread.</summary>
        [global::System.Obsolete("Legacy publish facade is retired. Use SignalBus<T> or an owner route wrapper.", true)]
        public static void Publish(in SimulationPauseSignal signal)
        {
            SimulationSignalRoute.TryQueuePause(in signal);
        }

        /// <summary>Queues one system-pause/input-lock packet without mutating simulation time state.</summary>
        [global::System.Obsolete("Legacy publish facade is retired. Use SignalBus<T> or an owner route wrapper.", true)]
        public static void Publish(in SystemPauseSignal signal)
        {
            EnsureInitialized();
            TryPushLegacy(in signal);
        }

        /// <summary>Queues one bullet-time post-process fake packet from the main thread.</summary>
        [global::System.Obsolete("Legacy publish facade is retired. Use SignalBus<T> or an owner route wrapper.", true)]
        public static void Publish(in BulletTimeVisualSignal signal)
        {
            SimulationSignalRoute.TryQueueBulletTimeVisual(in signal);
        }

        /// <summary>Queues one weather strength packet from the main thread.</summary>
        [global::System.Obsolete("Legacy publish facade is retired. Use SignalBus<T> or an owner route wrapper.", true)]
        public static void Publish(in WeatherStrengthSignal signal)
        {
            EnsureInitialized();
            WeatherStrengthSignal sanitizedSignal = signal;
            int guardCode = SignalPayloadFiniteGuards.Sanitize(ref sanitizedSignal);
            if (guardCode != 0)
                GlobalTelemetryBus.PublishMathGuardInvalidNumber(guardCode);

            TryPushLegacy(in sanitizedSignal);
            WeatherChangedSignal weatherSignal = default;
            weatherSignal.Strength01 = sanitizedSignal.Strength01;
            weatherSignal.FlowFieldScale = sanitizedSignal.FlowFieldScale;
            weatherSignal.PreviousWeatherHash = 0u;
            weatherSignal.WeatherHash = sanitizedSignal.WeatherHash;
            weatherSignal.Frame = sanitizedSignal.Frame;
            weatherSignal.QualityWeightByte = EncodeSignalQualityWeightByte(SignalBusRegistry.GlobalQualityWeight01);
            weatherSignal.Flags = sanitizedSignal.Flags;
            TryPushLegacy(in weatherSignal);
        }

        /// <summary>Queues one item decay packet from the main thread.</summary>
        [global::System.Obsolete("Legacy publish facade is retired. Use SignalBus<T> or an owner route wrapper.", true)]
        public static void Publish(in ItemDecaySignal signal)
        {
            EnsureInitialized();
            TryPushLegacy(in signal);
        }

        /// <summary>Queues one resource-acquired packet from the main thread.</summary>
        [global::System.Obsolete("Legacy publish facade is retired. Use SignalBus<T> or an owner route wrapper.", true)]
        public static void Publish(in ItemAcquiredSignal signal)
        {
            EnsureInitialized();
            TryPushLegacy(in signal);
        }

        /// <summary>Queues one radiation dose packet from the main thread.</summary>
        [global::System.Obsolete("Legacy publish facade is retired. Use SignalBus<T> or an owner route wrapper.", true)]
        public static void Publish(in RadiationDoseSignal signal)
        {
            EnsureInitialized();
            TryPushLegacy(in signal);
        }

        /// <summary>Queues one resource-depletion delta packet from the main thread.</summary>
        [global::System.Obsolete("Legacy publish facade is retired. Use SignalBus<T> or an owner route wrapper.", true)]
        public static void Publish(in ResourceDepletionDeltaSignal signal)
        {
            EnsureInitialized();
            TryPushLegacy(in signal);
        }

        /// <summary>Legacy alias pinned to the typed signal lane.</summary>
        [global::System.Obsolete("Legacy push facade is retired. Use SignalBus<T> or an owner route wrapper.", true)]
        public static void Push(in ItemAcquiredSignal signal) => TryPushLegacy(in signal);

        /// <summary>Legacy alias pinned to the typed signal lane.</summary>
        [global::System.Obsolete("Legacy push facade is retired. Use SignalBus<T> or an owner route wrapper.", true)]
        public static void Push(in RadiationDoseSignal signal) => TryPushLegacy(in signal);

        /// <summary>Legacy alias pinned to the typed signal lane.</summary>
        [global::System.Obsolete("Legacy push facade is retired. Use SignalBus<T> or an owner route wrapper.", true)]
        public static void Push(in ResourceDepletionDeltaSignal signal) => TryPushLegacy(in signal);

        /// <summary>Queues one player-light sample packet from the main thread.</summary>
        [global::System.Obsolete("Legacy publish facade is retired. Use SignalBus<T> or an owner route wrapper.", true)]
        public static void Publish(in LightLevelSignal signal)
        {
            EnsureInitialized();
            _latestLightLevelSignal = signal;
            AdvanceSignalSequence(ref _latestLightLevelSignalSequence);
            TryPushLegacy(in signal);
        }

        /// <summary>Queues one player/submersible headlight state packet into the typed lane.</summary>
        [global::System.Obsolete("Legacy publish facade is retired. Use SignalBus<T> or an owner route wrapper.", true)]
        public static void Publish(in SubmarineLightsChangedSignal signal)
        {
            EnsureInitialized();
            TryPushLegacy(in signal);
        }

        /// <summary>Queues one fauna state transition packet from the main thread.</summary>
        [global::System.Obsolete("Legacy publish facade is retired. Use SignalBus<T> or an owner route wrapper.", true)]
        public static void Publish(in FaunaStateChangedSignal signal)
        {
            EnsureInitialized();
            TryPushLegacy(in signal);
        }

        /// <summary>Queues one physiology-state packet from the main thread.</summary>
        [global::System.Obsolete("Legacy publish facade is retired. Use SignalBus<T> or an owner route wrapper.", true)]
        public static void Publish(in PhysiologyStateSignal signal)
        {
            EnsureInitialized();
            PhysiologyStateSignal sanitizedSignal = signal;
            int guardCode = SignalPayloadFiniteGuards.Sanitize(ref sanitizedSignal);
            if (guardCode != 0)
                GlobalTelemetryBus.PublishMathGuardInvalidNumber(guardCode);

            _latestPhysiologyStateSignal = sanitizedSignal;
            AdvanceSignalSequence(ref _latestPhysiologyStateSignalSequence);
            TryPushLegacy(in sanitizedSignal);
        }

        /// <summary>Queues one player stress packet from the main thread.</summary>
        [global::System.Obsolete("Legacy publish facade is retired. Use SignalBus<T> or an owner route wrapper.", true)]
        public static void Publish(in PlayerStressSignal signal)
        {
            EnsureInitialized();
            PlayerStressSignal sanitizedSignal = signal;
            int guardCode = SignalPayloadFiniteGuards.Sanitize(ref sanitizedSignal);
            if (guardCode != 0)
                GlobalTelemetryBus.PublishMathGuardInvalidNumber(guardCode);

            _latestPlayerStressSignal = sanitizedSignal;
            AdvanceSignalSequence(ref _latestPlayerStressSignalSequence);
            TryPushLegacy(in sanitizedSignal);
        }

        /// <summary>Queues one player trauma packet from the main thread.</summary>
        [global::System.Obsolete("Legacy publish facade is retired. Use SignalBus<T> or an owner route wrapper.", true)]
        public static void Publish(in TraumaSignal signal)
        {
            EnsureInitialized();
            TryPushLegacy(in signal);
        }

        /// <summary>Queues one procedural flora wake packet from the main thread.</summary>
        [global::System.Obsolete("Legacy publish facade is retired. Use SignalBus<T> or an owner route wrapper.", true)]
        public static void Publish(in WakeGeneratedSignal signal)
        {
            EnsureInitialized();
            TryPushLegacy(in signal);
        }

        /// <summary>Queues one bounded visual-fluid impulse for GPU advection consumers.</summary>
        [global::System.Obsolete("Legacy publish facade is retired. Use SignalBus<T> or an owner route wrapper.", true)]
        public static void Publish(in FluidImpulseSignal signal)
        {
            EnsureInitialized();
            TryPushLegacy(in signal);
        }

        /// <summary>Queues one bounded submarine bubble-spawn marker for VFX consumers.</summary>
        [global::System.Obsolete("Legacy publish facade is retired. Use SignalBus<T> or an owner route wrapper.", true)]
        public static void Publish(in BubbleSpawnSignal signal)
        {
            EnsureInitialized();
            TryPushLegacy(in signal);
        }

        /// <summary>Queues one narrative progression packet from the main thread.</summary>
        [global::System.Obsolete("Legacy publish facade is retired. Use SignalBus<T> or an owner route wrapper.", true)]
        public static void Publish(in ProgressionEventSignal signal)
        {
            EnsureInitialized();
            TryPushLegacy(in signal);
        }

        /// <summary>Queues one AUP-independent global world-state mutation from the main thread.</summary>
        [global::System.Obsolete("Legacy publish facade is retired. Use SignalBus<T> or an owner route wrapper.", true)]
        public static void Publish(in GlobalWorldStateSignal signal)
        {
            EnsureInitialized();
            TryPushLegacy(in signal);
        }

        /// <summary>Queues one biome transition packet from the main thread.</summary>
        [global::System.Obsolete("Legacy publish facade is retired. Use SignalBus<T> or an owner route wrapper.", true)]
        public static void Publish(in BiomeChangedSignal signal)
        {
            EnsureInitialized();
            TryPushLegacy(in signal);
        }

        /// <summary>Queues one procedural narrative camera focus packet from the main thread.</summary>
        [global::System.Obsolete("Legacy publish facade is retired. Use SignalBus<T> or an owner route wrapper.", true)]
        public static void Publish(in NarrativeFocusSignal signal)
        {
            EnsureInitialized();
            TryPushLegacy(in signal);
        }

        /// <summary>Queues one player-authored focus break packet from the main thread.</summary>
        [global::System.Obsolete("Legacy publish facade is retired. Use SignalBus<T> or an owner route wrapper.", true)]
        public static void Publish(in FocusBrokenSignal signal)
        {
            EnsureInitialized();
            TryPushLegacy(in signal);
        }

        /// <summary>Queues one mixer-state request packet from the main thread.</summary>
        [global::System.Obsolete("Legacy publish facade is retired. Use SignalBus<T> or an owner route wrapper.", true)]
        public static void Publish(in MixerStateSignal signal)
        {
            EnsureInitialized();
            TryPushLegacy(in signal);
        }

        /// <summary>Queues one diegetic narrative waypoint packet from the main thread.</summary>
        [global::System.Obsolete("Legacy publish facade is retired. Use SignalBus<T> or an owner route wrapper.", true)]
        public static void Publish(in NarrativeHudWaypointSignal signal)
        {
            EnsureInitialized();
            TryPushLegacy(in signal);
        }

        /// <summary>Queues one soundscape profile handoff packet from the main thread.</summary>
        [global::System.Obsolete("Legacy publish facade is retired. Use SignalBus<T> or an owner route wrapper.", true)]
        public static void Publish(in SoundscapeProfileSignal signal)
        {
            EnsureInitialized();
            TryPushLegacy(in signal);
        }

        /// <summary>Queues one narrative POI save-state packet from the main thread.</summary>
        [global::System.Obsolete("Legacy publish facade is retired. Use SignalBus<T> or an owner route wrapper.", true)]
        public static void Publish(in NarrativePoiStateSignal signal)
        {
            EnsureInitialized();
            TryPushLegacy(in signal);
        }

        [global::System.Obsolete("Legacy destructive dequeue facade is retired. Use SignalBus<T>.TryConsumeFrame or an owner route reader.", true)]
        public static bool TryDequeueImpact(out ImpactSignal signal) => SignalBus<ImpactSignal>.TryConsumeFrame(out signal);
        [global::System.Obsolete("Legacy destructive dequeue facade is retired. Use SignalBus<T>.TryConsumeFrame or an owner route reader.", true)]
        public static bool TryDequeueAupPreShift(out AupPreShiftSignal signal) => SignalBus<AupPreShiftSignal>.TryConsumeFrame(out signal);
        [global::System.Obsolete("Legacy destructive dequeue facade is retired. Use SignalBus<T>.TryConsumeFrame or an owner route reader.", true)]
        public static bool TryDequeueAupShift(out AupShiftSignal signal) => SignalBus<AupShiftSignal>.TryConsumeFrame(out signal);
        [global::System.Obsolete("Legacy destructive dequeue facade is retired. Use SignalBus<T>.TryConsumeFrame or an owner route reader.", true)]
        public static bool TryDequeueDropPodLanded(out DropPodLandedSignal signal) => SignalBus<DropPodLandedSignal>.TryConsumeFrame(out signal);
        [global::System.Obsolete("Legacy destructive dequeue facade is retired. Use SignalBus<T>.TryConsumeFrame or an owner route reader.", true)]
        public static bool TryDequeueBrownout(out BrownoutSignal signal) => SignalBus<BrownoutSignal>.TryConsumeFrame(out signal);
        [global::System.Obsolete("Legacy destructive dequeue facade is retired. Use SignalBus<T>.TryConsumeFrame or an owner route reader.", true)]
        public static bool TryDequeueDebrisSpawn(out DebrisSpawnSignal signal) => SignalBus<DebrisSpawnSignal>.TryConsumeFrame(out signal);
        [global::System.Obsolete("Legacy destructive dequeue facade is retired. Use SignalBus<T>.TryConsumeFrame or an owner route reader.", true)]
        public static bool TryDequeueDeflect(out DeflectSignal signal) => SignalBus<DeflectSignal>.TryConsumeFrame(out signal);
        [global::System.Obsolete("Legacy destructive dequeue facade is retired. Use SignalBus<T>.TryConsumeFrame or an owner route reader.", true)]
        public static bool TryDequeueEntityDeath(out EntityDeathSignal signal) => SignalBus<EntityDeathSignal>.TryConsumeFrame(out signal);
        [global::System.Obsolete("Legacy destructive dequeue facade is retired. Use SignalBus<T>.TryConsumeFrame or an owner route reader.", true)]
        public static bool TryDequeueSolarFlare(out SolarFlareSignal signal) => SignalBus<SolarFlareSignal>.TryConsumeFrame(out signal);
        [global::System.Obsolete("Legacy destructive dequeue facade is retired. Use SignalBus<T>.TryConsumeFrame or an owner route reader.", true)]
        public static bool TryDequeueRebase(out RebaseSignal signal) => SignalBus<RebaseSignal>.TryConsumeFrame(out signal);
        [global::System.Obsolete("Legacy destructive dequeue facade is retired. Use SignalBus<T>.TryConsumeFrame or an owner route reader.", true)]
        public static bool TryDequeueControl(out ControlSignal signal) => SignalBus<ControlSignal>.TryConsumeFrame(out signal);
        [global::System.Obsolete("Legacy destructive dequeue facade is retired. Use SignalBus<T>.TryConsumeFrame or an owner route reader.", true)]
        public static bool TryDequeueAnomaly(out AnomalySignal signal) => SignalBus<AnomalySignal>.TryConsumeFrame(out signal);
        [global::System.Obsolete("Legacy destructive dequeue facade is retired. Use SignalBus<T>.TryConsumeFrame or an owner route reader.", true)]
        public static bool TryDequeueTelemetryAnomaly(out TelemetryAnomalySignal signal) => SignalBus<TelemetryAnomalySignal>.TryConsumeFrame(out signal);
        [global::System.Obsolete("Legacy destructive dequeue facade is retired. Use SignalBus<T>.TryConsumeFrame or an owner route reader.", true)]
        public static bool TryDequeueCrashTelemetry(out CrashTelemetrySignal signal) => SignalBus<CrashTelemetrySignal>.TryConsumeFrame(out signal);
        [global::System.Obsolete("Legacy destructive dequeue facade is retired. Use SignalBus<T>.TryConsumeFrame or an owner route reader.", true)]
        public static bool TryDequeueHabitatConstruction(out HabitatConstructionSignal signal) => SignalBus<HabitatConstructionSignal>.TryConsumeFrame(out signal);
        [global::System.Obsolete("Legacy destructive dequeue facade is retired. Use SignalBus<T>.TryConsumeFrame or an owner route reader.", true)]
        public static bool TryDequeueDeconstructRequest(out DeconstructRequestSignal signal) => SignalBus<DeconstructRequestSignal>.TryConsumeFrame(out signal);
        [global::System.Obsolete("Legacy destructive dequeue facade is retired. Use SignalBus<T>.TryConsumeFrame or an owner route reader.", true)]
        public static bool TryDequeueDeconstructResult(out DeconstructResultSignal signal) => SignalBus<DeconstructResultSignal>.TryConsumeFrame(out signal);
        [global::System.Obsolete("Legacy destructive dequeue facade is retired. Use SignalBus<T>.TryConsumeFrame or an owner route reader.", true)]
        public static bool TryDequeueModuleDeconstruct(out ModuleDeconstructSignal signal) => SignalBus<ModuleDeconstructSignal>.TryConsumeFrame(out signal);
        [global::System.Obsolete("Legacy destructive dequeue facade is retired. Use SignalBus<T>.TryConsumeFrame or an owner route reader.", true)]
        public static bool TryDequeueVitalWarning(out VitalWarningSignal signal) => SignalBus<VitalWarningSignal>.TryConsumeFrame(out signal);
        [global::System.Obsolete("Legacy destructive dequeue facade is retired. Use SignalBus<T>.TryConsumeFrame or an owner route reader.", true)]
        public static bool TryDequeueCrushWarning(out CrushWarningSignal signal) => SignalBus<CrushWarningSignal>.TryConsumeFrame(out signal);
        [global::System.Obsolete("Legacy destructive dequeue facade is retired. Use SignalBus<T>.TryConsumeFrame or an owner route reader.", true)]
        public static bool TryDequeueVocalWarning(out VocalWarningSignal signal) => SignalBus<VocalWarningSignal>.TryConsumeFrame(out signal);
        [global::System.Obsolete("Legacy destructive dequeue facade is retired. Use SignalBus<T>.TryConsumeFrame or an owner route reader.", true)]
        public static bool TryDequeueSubtitle(out SubtitleSignal signal) => SignalBus<SubtitleSignal>.TryConsumeFrame(out signal);
        [global::System.Obsolete("Legacy destructive dequeue facade is retired. Use SignalBus<T>.TryConsumeFrame or an owner route reader.", true)]
        public static bool TryDequeueDataReload(out DataReloadSignal signal) => SignalBus<DataReloadSignal>.TryConsumeFrame(out signal);
        [global::System.Obsolete("Legacy destructive dequeue facade is retired. Use SignalBus<T>.TryConsumeFrame or an owner route reader.", true)]
        public static bool TryDequeueMemoryPressure(out MemoryPressureSignal signal) => SignalBus<MemoryPressureSignal>.TryConsumeFrame(out signal);
        [global::System.Obsolete("Legacy destructive dequeue facade is retired. Use SignalBus<T>.TryConsumeFrame or an owner route reader.", true)]
        public static bool TryDequeueAcousticPing(out AcousticPingSignal signal) => SignalBus<AcousticPingSignal>.TryConsumeFrame(out signal);
        [global::System.Obsolete("Legacy destructive dequeue facade is retired. Use SignalBus<T>.TryConsumeFrame or an owner route reader.", true)]
        public static bool TryDequeueMovementAcoustic(out MovementAcousticSignal signal) => SignalBus<MovementAcousticSignal>.TryConsumeFrame(out signal);
        [global::System.Obsolete("Legacy destructive dequeue facade is retired. Use SignalBus<T>.TryConsumeFrame or an owner route reader.", true)]
        public static bool TryDequeueSonarPing(out SonarPingSignal signal) => SignalBus<SonarPingSignal>.TryConsumeFrame(out signal);
        [global::System.Obsolete("Legacy destructive dequeue facade is retired. Use SignalBus<T>.TryConsumeFrame or an owner route reader.", true)]
        public static bool TryDequeueHypoxia(out HypoxiaSignal signal) => SignalBus<HypoxiaSignal>.TryConsumeFrame(out signal);
        [global::System.Obsolete("Legacy destructive dequeue facade is retired. Use SignalBus<T>.TryConsumeFrame or an owner route reader.", true)]
        public static bool TryDequeueOxygenCritical(out OxygenCriticalSignal signal) => SignalBus<OxygenCriticalSignal>.TryConsumeFrame(out signal);
        [global::System.Obsolete("Legacy destructive dequeue facade is retired. Use SignalBus<T>.TryConsumeFrame or an owner route reader.", true)]
        public static bool TryDequeueInteractionUi(out InteractionUiSignal signal) => SignalBus<InteractionUiSignal>.TryConsumeFrame(out signal);
        [global::System.Obsolete("Legacy destructive dequeue facade is retired. Use SignalBus<T>.TryConsumeFrame or an owner route reader.", true)]
        public static bool TryDequeueUIRescaleRequest(out UIRescaleRequestSignal signal) => SignalBus<UIRescaleRequestSignal>.TryConsumeFrame(out signal);
        [global::System.Obsolete("Legacy destructive dequeue facade is retired. Use SignalBus<T>.TryConsumeFrame or an owner route reader.", true)]
        public static bool TryDequeueFluidIncursion(out FluidIncursionSignal signal) => SignalBus<FluidIncursionSignal>.TryConsumeFrame(out signal);
        [global::System.Obsolete("Legacy destructive dequeue facade is retired. Use SignalBus<T>.TryConsumeFrame or an owner route reader.", true)]
        public static bool TryDequeueFluidDensityChanged(out FluidDensityChangedSignal signal) => SignalBus<FluidDensityChangedSignal>.TryConsumeFrame(out signal);
        [global::System.Obsolete("Legacy destructive dequeue facade is retired. Use SignalBus<T>.TryConsumeFrame or an owner route reader.", true)]
        public static bool TryDequeuePipeRupture(out PipeRuptureSignal signal) => SignalBus<PipeRuptureSignal>.TryConsumeFrame(out signal);
        [global::System.Obsolete("Legacy destructive dequeue facade is retired. Use SignalBus<T>.TryConsumeFrame or an owner route reader.", true)]
        public static bool TryDequeueSpectrumScan(out SpectrumScanSignal signal) => SignalBus<SpectrumScanSignal>.TryConsumeFrame(out signal);
        [global::System.Obsolete("Legacy destructive dequeue facade is retired. Use SignalBus<T>.TryConsumeFrame or an owner route reader.", true)]
        public static bool TryDequeueRigidbodySleep(out RigidbodySleepSignal signal) => SignalBus<RigidbodySleepSignal>.TryConsumeFrame(out signal);
        [global::System.Obsolete("Legacy destructive dequeue facade is retired. Use SignalBus<T>.TryConsumeFrame or an owner route reader.", true)]
        public static bool TryDequeueScannerToolActive(out ScannerToolActiveSignal signal) => SignalBus<ScannerToolActiveSignal>.TryConsumeFrame(out signal);
        [global::System.Obsolete("Legacy destructive dequeue facade is retired. Use SignalBus<T>.TryConsumeFrame or an owner route reader.", true)]
        public static bool TryDequeueScanComplete(out ScanCompleteSignal signal) => SignalBus<ScanCompleteSignal>.TryConsumeFrame(out signal);
        [global::System.Obsolete("Legacy destructive dequeue facade is retired. Use SignalBus<T>.TryConsumeFrame or an owner route reader.", true)]
        public static bool TryDequeueLoreFragmentScanned(out LoreFragmentScannedSignal signal) => SignalBus<LoreFragmentScannedSignal>.TryConsumeFrame(out signal);
        [global::System.Obsolete("Legacy destructive dequeue facade is retired. Use SignalBus<T>.TryConsumeFrame or an owner route reader.", true)]
        public static bool TryDequeueBlueprintUnlocked(out BlueprintUnlockedSignal signal) => SignalBus<BlueprintUnlockedSignal>.TryConsumeFrame(out signal);
        [global::System.Obsolete("Legacy destructive dequeue facade is retired. Use SignalBus<T>.TryConsumeFrame or an owner route reader.", true)]
        public static bool TryDequeueCraftingStarted(out CraftingStartedSignal signal) => SignalBus<CraftingStartedSignal>.TryConsumeFrame(out signal);
        [global::System.Obsolete("Legacy destructive dequeue facade is retired. Use SignalBus<T>.TryConsumeFrame or an owner route reader.", true)]
        public static bool TryDequeueCraftingCompleted(out CraftingCompletedSignal signal) => SignalBus<CraftingCompletedSignal>.TryConsumeFrame(out signal);
        [global::System.Obsolete("Legacy destructive dequeue facade is retired. Use SignalBus<T>.TryConsumeFrame or an owner route reader.", true)]
        public static bool TryDequeueToolStateChanged(out ToolStateChangedSignal signal) => SignalBus<ToolStateChangedSignal>.TryConsumeFrame(out signal);
        [global::System.Obsolete("Legacy destructive dequeue facade is retired. Use SignalBus<T>.TryConsumeFrame or an owner route reader.", true)]
        public static bool TryDequeueToolAcoustic(out ToolAcousticSignal signal) => SignalBus<ToolAcousticSignal>.TryConsumeFrame(out signal);
        [global::System.Obsolete("Legacy destructive dequeue facade is retired. Use SignalBus<T>.TryConsumeFrame or an owner route reader.", true)]
        public static bool TryDequeuePowerDrain(out PowerDrainSignal signal) => SignalBus<PowerDrainSignal>.TryConsumeFrame(out signal);
        [global::System.Obsolete("Legacy destructive dequeue facade is retired. Use SignalBus<T>.TryConsumeFrame or an owner route reader.", true)]
        public static bool TryDequeueToolTrigger(out ToolTriggerSignal signal) => SignalBus<ToolTriggerSignal>.TryConsumeFrame(out signal);
        [global::System.Obsolete("Legacy destructive dequeue facade is retired. Use SignalBus<T>.TryConsumeFrame or an owner route reader.", true)]
        public static bool TryDequeueHUDNotification(out HUDNotificationSignal signal) => SignalBus<HUDNotificationSignal>.TryConsumeFrame(out signal);
        [global::System.Obsolete("Legacy destructive dequeue facade is retired. Use SignalBus<T>.TryConsumeFrame or an owner route reader.", true)]
        public static bool TryDequeueStorageDebt(out StorageDebtSignal signal) => SignalBus<StorageDebtSignal>.TryConsumeFrame(out signal);
        [global::System.Obsolete("Legacy destructive dequeue facade is retired. Use SignalBus<T>.TryConsumeFrame or an owner route reader.", true)]
        public static bool TryDequeueStreamingTurbulence(out StreamingTurbulenceSignal signal) => SignalBus<StreamingTurbulenceSignal>.TryConsumeFrame(out signal);
        [global::System.Obsolete("Legacy destructive dequeue facade is retired. Use SignalBus<T>.TryConsumeFrame or an owner route reader.", true)]
        public static bool TryDequeueAtmosphericReentry(out AtmosphericReentrySignal signal) => SignalBus<AtmosphericReentrySignal>.TryConsumeFrame(out signal);
        [global::System.Obsolete("Legacy destructive dequeue facade is retired. Use SignalBus<T>.TryConsumeFrame or an owner route reader.", true)]
        public static bool TryDequeuePrologueComplete(out PrologueCompleteSignal signal) => SignalBus<PrologueCompleteSignal>.TryConsumeFrame(out signal);
        [global::System.Obsolete("Legacy destructive dequeue facade is retired. Use SignalBus<T>.TryConsumeFrame or an owner route reader.", true)]
        public static bool TryDequeueManualOverridePulled(out ManualOverridePulledSignal signal) => SignalBus<ManualOverridePulledSignal>.TryConsumeFrame(out signal);
        [global::System.Obsolete("Legacy destructive dequeue facade is retired. Use SignalBus<T>.TryConsumeFrame or an owner route reader.", true)]
        public static bool TryDequeueDiegeticHud(out DiegeticHudSignal signal) => SignalBus<DiegeticHudSignal>.TryConsumeFrame(out signal);
        [global::System.Obsolete("Legacy destructive dequeue facade is retired. Use SignalBus<T>.TryConsumeFrame or an owner route reader.", true)]
        public static bool TryDequeueReconData(out ReconDataSignal signal) => SignalBus<ReconDataSignal>.TryConsumeFrame(out signal);
        [global::System.Obsolete("Legacy destructive dequeue facade is retired. Use SignalBus<T>.TryConsumeFrame or an owner route reader.", true)]
        public static bool TryDequeueSaveLifecycle(out SaveLifecycleSignal signal) => SignalBus<SaveLifecycleSignal>.TryConsumeFrame(out signal);
        [global::System.Obsolete("Legacy destructive dequeue facade is retired. Use SignalBus<T>.TryConsumeFrame or an owner route reader.", true)]
        public static bool TryDequeueComplianceViolation(out ComplianceViolationSignal signal) => SignalBus<ComplianceViolationSignal>.TryConsumeFrame(out signal);
        [global::System.Obsolete("Legacy destructive dequeue facade is retired. Use SignalBus<T>.TryConsumeFrame or an owner route reader.", true)]
        public static bool TryDequeueGlobalTimeSync(out GlobalTimeSyncSignal signal) => SignalBus<GlobalTimeSyncSignal>.TryConsumeFrame(out signal);
        [global::System.Obsolete("Legacy destructive dequeue facade is retired. Use SignalBus<T>.TryConsumeFrame or an owner route reader.", true)]
        public static bool TryDequeueSeismic(out SeismicSignal signal) => SignalBus<SeismicSignal>.TryConsumeFrame(out signal);
        [global::System.Obsolete("Legacy destructive dequeue facade is retired. Use SignalBus<T>.TryConsumeFrame or an owner route reader.", true)]
        public static bool TryDequeueTimeDilation(out TimeDilationSignal signal) => SignalBus<TimeDilationSignal>.TryConsumeFrame(out signal);
        [global::System.Obsolete("Legacy destructive dequeue facade is retired. Use SignalBus<T>.TryConsumeFrame or an owner route reader.", true)]
        public static bool TryDequeueSimulationPause(out SimulationPauseSignal signal) => SignalBus<SimulationPauseSignal>.TryConsumeFrame(out signal);
        [global::System.Obsolete("Legacy destructive dequeue facade is retired. Use SignalBus<T>.TryConsumeFrame or an owner route reader.", true)]
        public static bool TryDequeueHapticRequest(out HapticRequest signal) => SignalBus<HapticRequest>.TryConsumeFrame(out signal);
        [global::System.Obsolete("Legacy destructive dequeue facade is retired. Use SignalBus<T>.TryConsumeFrame or an owner route reader.", true)]
        public static bool TryDequeueSubmarineFloodState(out SubmarineFloodStateSignal signal) => SignalBus<SubmarineFloodStateSignal>.TryConsumeFrame(out signal);
        [global::System.Obsolete("Legacy destructive dequeue facade is retired. Use SignalBus<T>.TryConsumeFrame or an owner route reader.", true)]
        public static bool TryDequeueBulletTimeVisual(out BulletTimeVisualSignal signal) => SignalBus<BulletTimeVisualSignal>.TryConsumeFrame(out signal);
        [global::System.Obsolete("Legacy destructive dequeue facade is retired. Use SignalBus<T>.TryConsumeFrame or an owner route reader.", true)]
        public static bool TryDequeueWeatherStrength(out WeatherStrengthSignal signal) => SignalBus<WeatherStrengthSignal>.TryConsumeFrame(out signal);
        [global::System.Obsolete("Legacy destructive dequeue facade is retired. Use SignalBus<T>.TryConsumeFrame or an owner route reader.", true)]
        public static bool TryDequeueItemDecay(out ItemDecaySignal signal) => SignalBus<ItemDecaySignal>.TryConsumeFrame(out signal);
        [global::System.Obsolete("Legacy destructive dequeue facade is retired. Use SignalBus<T>.TryConsumeFrame or an owner route reader.", true)]
        public static bool TryDequeueItemAcquired(out ItemAcquiredSignal signal) => SignalBus<ItemAcquiredSignal>.TryConsumeFrame(out signal);
        [global::System.Obsolete("Legacy destructive dequeue facade is retired. Use SignalBus<T>.TryConsumeFrame or an owner route reader.", true)]
        public static bool TryDequeueRadiationDose(out RadiationDoseSignal signal) => SignalBus<RadiationDoseSignal>.TryConsumeFrame(out signal);
        [global::System.Obsolete("Legacy destructive dequeue facade is retired. Use SignalBus<T>.TryConsumeFrame or an owner route reader.", true)]
        public static bool TryDequeueResourceDepletionDelta(out ResourceDepletionDeltaSignal signal) => SignalBus<ResourceDepletionDeltaSignal>.TryConsumeFrame(out signal);
        [global::System.Obsolete("Legacy destructive dequeue facade is retired. Use SignalBus<T>.TryConsumeFrame or an owner route reader.", true)]
        public static bool TryDequeueLightLevel(out LightLevelSignal signal) => SignalBus<LightLevelSignal>.TryConsumeFrame(out signal);
        [global::System.Obsolete("Legacy destructive dequeue facade is retired. Use SignalBus<T>.TryConsumeFrame or an owner route reader.", true)]
        public static bool TryDequeueSubmarineLightsChanged(out SubmarineLightsChangedSignal signal) =>
            SignalBus<SubmarineLightsChangedSignal>.TryConsumeFrame(out signal);
        [global::System.Obsolete("Legacy destructive dequeue facade is retired. Use SignalBus<T>.TryConsumeFrame or an owner route reader.", true)]
        public static bool TryDequeueFaunaStateChanged(out FaunaStateChangedSignal signal) => SignalBus<FaunaStateChangedSignal>.TryConsumeFrame(out signal);
        [global::System.Obsolete("Legacy destructive dequeue facade is retired. Use SignalBus<T>.TryConsumeFrame or an owner route reader.", true)]
        public static bool TryDequeuePhysiologyState(out PhysiologyStateSignal signal) => SignalBus<PhysiologyStateSignal>.TryConsumeFrame(out signal);
        [global::System.Obsolete("Legacy destructive dequeue facade is retired. Use SignalBus<T>.TryConsumeFrame or an owner route reader.", true)]
        public static bool TryDequeuePlayerStress(out PlayerStressSignal signal) => SignalBus<PlayerStressSignal>.TryConsumeFrame(out signal);
        [global::System.Obsolete("Legacy destructive dequeue facade is retired. Use SignalBus<T>.TryConsumeFrame or an owner route reader.", true)]
        public static bool TryDequeuePlayerState(out PlayerStateSignal signal) => SignalBus<PlayerStateSignal>.TryConsumeFrame(out signal);
        [global::System.Obsolete("Legacy destructive dequeue facade is retired. Use SignalBus<T>.TryConsumeFrame or an owner route reader.", true)]
        public static bool TryDequeueTrauma(out TraumaSignal signal) => SignalBus<TraumaSignal>.TryConsumeFrame(out signal);
        [global::System.Obsolete("Legacy destructive dequeue facade is retired. Use SignalBus<T>.TryConsumeFrame or an owner route reader.", true)]
        public static bool TryDequeueProgressionEvent(out ProgressionEventSignal signal) =>
            SignalBus<ProgressionEventSignal>.TryConsumeFrame(out signal);
        [global::System.Obsolete("Legacy destructive dequeue facade is retired. Use SignalBus<T>.TryConsumeFrame or an owner route reader.", true)]
        public static bool TryDequeueGlobalWorldState(out GlobalWorldStateSignal signal) => SignalBus<GlobalWorldStateSignal>.TryConsumeFrame(out signal);
        [global::System.Obsolete("Legacy destructive dequeue facade is retired. Use SignalBus<T>.TryConsumeFrame or an owner route reader.", true)]
        public static bool TryDequeueBiomeChanged(out BiomeChangedSignal signal) => SignalBus<BiomeChangedSignal>.TryConsumeFrame(out signal);
        [global::System.Obsolete("Legacy destructive dequeue facade is retired. Use SignalBus<T>.TryConsumeFrame or an owner route reader.", true)]
        public static bool TryDequeueNarrativeFocus(out NarrativeFocusSignal signal) => SignalBus<NarrativeFocusSignal>.TryConsumeFrame(out signal);
        [global::System.Obsolete("Legacy destructive dequeue facade is retired. Use SignalBus<T>.TryConsumeFrame or an owner route reader.", true)]
        public static bool TryDequeueFocusBroken(out FocusBrokenSignal signal) => SignalBus<FocusBrokenSignal>.TryConsumeFrame(out signal);
        [global::System.Obsolete("Legacy destructive dequeue facade is retired. Use SignalBus<T>.TryConsumeFrame or an owner route reader.", true)]
        public static bool TryDequeueMixerState(out MixerStateSignal signal) => SignalBus<MixerStateSignal>.TryConsumeFrame(out signal);
        [global::System.Obsolete("Legacy destructive dequeue facade is retired. Use SignalBus<T>.TryConsumeFrame or an owner route reader.", true)]
        public static bool TryDequeueNarrativeHudWaypoint(out NarrativeHudWaypointSignal signal) => SignalBus<NarrativeHudWaypointSignal>.TryConsumeFrame(out signal);
        [global::System.Obsolete("Legacy destructive dequeue facade is retired. Use SignalBus<T>.TryConsumeFrame or an owner route reader.", true)]
        public static bool TryDequeueSoundscapeProfile(out SoundscapeProfileSignal signal) => SignalBus<SoundscapeProfileSignal>.TryConsumeFrame(out signal);
        [global::System.Obsolete("Legacy destructive dequeue facade is retired. Use SignalBus<T>.TryConsumeFrame or an owner route reader.", true)]
        public static bool TryDequeueNarrativePoiState(out NarrativePoiStateSignal signal) => SignalBus<NarrativePoiStateSignal>.TryConsumeFrame(out signal);

        [global::System.Obsolete("Central latest-state facade is retired. Use SignalBus<CombatDamageSignal>.TryGetLatest or an owner route reader.", true)]
        public static bool TryGetLatestDamageSignal(out CombatDamageSignal signal, out int sequence)
        {
            return SignalBus<CombatDamageSignal>.TryGetLatest(out signal, out sequence);
        }

        [global::System.Obsolete("Central latest-state facade is retired. Use SignalBus<AcousticPingSignal>.TryGetLatest or an owner route reader.", true)]
        public static bool TryGetLatestAcousticPingSignal(out AcousticPingSignal signal, out int sequence)
        {
            return SignalBus<AcousticPingSignal>.TryGetLatest(out signal, out sequence);
        }

        [global::System.Obsolete("Central latest-state facade is retired. Use SignalBus<FluidDensityChangedSignal>.TryGetLatest or an owner route reader.", true)]
        public static bool TryGetLatestFluidDensityChangedSignal(out FluidDensityChangedSignal signal, out int sequence)
        {
            sequence = Volatile.Read(ref _latestFluidDensityChangedSignalSequence);
            signal = _latestFluidDensityChangedSignal;
            return sequence != 0;
        }

        [global::System.Obsolete("Central latest-state facade is retired. Use SignalBus<LightLevelSignal>.TryGetLatest or an owner route reader.", true)]
        public static bool TryGetLatestLightLevelSignal(out LightLevelSignal signal, out int sequence)
        {
            return SignalBus<LightLevelSignal>.TryGetLatest(out signal, out sequence);
        }

        [global::System.Obsolete("Central latest-state facade is retired. Use SignalBus<PlayerStressSignal>.TryGetLatest or an owner route reader.", true)]
        public static bool TryGetLatestPlayerStressSignal(out PlayerStressSignal signal, out int sequence)
        {
            return SignalBus<PlayerStressSignal>.TryGetLatest(out signal, out sequence);
        }

        [global::System.Obsolete("Central latest-state facade is retired. Use SignalBus<PlayerStateSignal>.TryGetLatest or an owner route reader.", true)]
        public static bool TryGetLatestPlayerStateSignal(out PlayerStateSignal signal, out int sequence)
        {
            return SignalBus<PlayerStateSignal>.TryGetLatest(out signal, out sequence);
        }

        [global::System.Obsolete("Central latest-state facade is retired. Use SignalBus<PhysiologyStateSignal>.TryGetLatest or an owner route reader.", true)]
        public static bool TryGetLatestPhysiologyStateSignal(out PhysiologyStateSignal signal, out int sequence)
        {
            return SignalBus<PhysiologyStateSignal>.TryGetLatest(out signal, out sequence);
        }

        [global::System.Obsolete("Central latest-state facade is retired. Use SurvivalSignalRoute.TryGetLatestDeath.", true)]
        public static bool TryGetLatestSurvivalDeathSignal(out SurvivalVitalsChangedSignal signal, out int sequence)
        {
            return SurvivalSignalRoute.TryGetLatestDeath(out signal, out sequence);
        }

        [global::System.Obsolete("Central latest-state facade is retired. Use SignalBus<SeismicSignal>.TryGetLatest or an owner route reader.", true)]
        public static bool TryGetLatestSeismicSignal(out SeismicSignal signal, out int sequence)
        {
            return SignalBus<SeismicSignal>.TryGetLatest(out signal, out sequence);
        }

        [global::System.Obsolete("Central latest-state facade is retired. Use ScannerSignalRoute.TryGetLatestActive.", true)]
        public static bool TryGetLatestScannerToolActiveSignal(out ScannerToolActiveSignal signal, out int sequence)
        {
            return SignalBus<ScannerToolActiveSignal>.TryGetLatest(out signal, out sequence);
        }

        [global::System.Obsolete("Central latest-state facade is retired. Use SignalBus<ToolStateChangedSignal>.TryGetLatest or an owner route reader.", true)]
        public static bool TryGetLatestToolStateChangedSignal(out ToolStateChangedSignal signal, out int sequence)
        {
            return SignalBus<ToolStateChangedSignal>.TryGetLatest(out signal, out sequence);
        }

    }
}
