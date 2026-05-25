using System;
using System.Runtime.CompilerServices;
using System.Threading;
using Hecton8.Core.Contracts;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Generated;
using Hecton8.Core.Memory;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;
using static Hecton8.Core.Contracts.Signals.SignalPayloadSanitizer;
using AbsoluteUniversePosition = Hecton8.World.AbsoluteUniversePosition;

namespace Hecton8.Core
{
    public static partial class GlobalSignals
    {
        private static NativeQueue<TSignal>.ParallelWriter OpenSignalWriterForProducerPhase<TSignal>()
            where TSignal : unmanaged, ISignal
        {
            return SignalBus<TSignal>.OpenParallelWriter();
        }

        // Compatibility writer properties below preserve existing sibling-domain ABI.
        // Maintained Core producer code uses thread-local scratch or OpenSignalWriterForProducerPhase<TSignal>() only as a legacy bridge.

        /// <summary>Damage routing legacy bridge writer for low-frequency compatibility producers.</summary>
        [global::System.Obsolete("Legacy writer properties are retired. Use SignalBus<T>.OpenParallelWriter or an owner route writer.", true)]
        public static NativeQueue<CombatDamageSignal>.ParallelWriter DamageSignalWriter
        {
            get
            {
                return OpenSignalWriterForProducerPhase<CombatDamageSignal>();
            }
        }

        /// <summary>Physics impact legacy bridge writer for low-frequency compatibility producers.</summary>
        [global::System.Obsolete("Legacy writer properties are retired. Use SignalBus<T>.OpenParallelWriter or an owner route writer.", true)]
        public static NativeQueue<ImpactSignal>.ParallelWriter ImpactSignalWriter
        {
            get
            {
                return OpenSignalWriterForProducerPhase<ImpactSignal>();
            }
        }

        /// <summary>AUP pre-shift legacy bridge writer for low-frequency compatibility producers.</summary>
        [global::System.Obsolete("Legacy writer properties are retired. Use SignalBus<T>.OpenParallelWriter or an owner route writer.", true)]
        public static NativeQueue<AupPreShiftSignal>.ParallelWriter AupPreShiftSignalWriter
        {
            get
            {
                return OpenSignalWriterForProducerPhase<AupPreShiftSignal>();
            }
        }

        /// <summary>AUP shift legacy bridge writer for low-frequency compatibility producers.</summary>
        [global::System.Obsolete("Legacy writer properties are retired. Use SignalBus<T>.OpenParallelWriter or an owner route writer.", true)]
        public static NativeQueue<AupShiftSignal>.ParallelWriter AupShiftSignalWriter
        {
            get
            {
                return OpenSignalWriterForProducerPhase<AupShiftSignal>();
            }
        }

        /// <summary>Logistics brownout legacy bridge writer for low-frequency compatibility producers.</summary>
        [global::System.Obsolete("Legacy writer properties are retired. Use SignalBus<T>.OpenParallelWriter or an owner route writer.", true)]
        public static NativeQueue<BrownoutSignal>.ParallelWriter BrownoutSignalWriter
        {
            get
            {
                return OpenSignalWriterForProducerPhase<BrownoutSignal>();
            }
        }

        /// <summary>Armor deflection legacy bridge writer for low-frequency compatibility producers.</summary>
        [global::System.Obsolete("Legacy writer properties are retired. Use SignalBus<T>.OpenParallelWriter or an owner route writer.", true)]
        public static NativeQueue<DeflectSignal>.ParallelWriter DeflectSignalWriter
        {
            get
            {
                return OpenSignalWriterForProducerPhase<DeflectSignal>();
            }
        }

        /// <summary>Entity death legacy bridge writer for low-frequency compatibility producers.</summary>
        [global::System.Obsolete("Legacy writer properties are retired. Use SignalBus<T>.OpenParallelWriter or an owner route writer.", true)]
        public static NativeQueue<EntityDeathSignal>.ParallelWriter EntityDeathSignalWriter
        {
            get
            {
                return OpenSignalWriterForProducerPhase<EntityDeathSignal>();
            }
        }

        /// <summary>Runtime anomaly legacy bridge writer for low-frequency compatibility producers.</summary>
        [global::System.Obsolete("Legacy writer properties are retired. Use SignalBus<T>.OpenParallelWriter or an owner route writer.", true)]
        public static NativeQueue<AnomalySignal>.ParallelWriter AnomalySignalWriter
        {
            get
            {
                return OpenSignalWriterForProducerPhase<AnomalySignal>();
            }
        }

        /// <summary>Acoustic ping legacy bridge writer for low-frequency compatibility producers.</summary>
        [global::System.Obsolete("Legacy writer properties are retired. Use SignalBus<T>.OpenParallelWriter or an owner route writer.", true)]
        public static NativeQueue<AcousticPingSignal>.ParallelWriter AcousticPingSignalWriter
        {
            get
            {
                return OpenSignalWriterForProducerPhase<AcousticPingSignal>();
            }
        }

        /// <summary>Movement acoustic legacy bridge writer for low-frequency compatibility producers.</summary>
        [global::System.Obsolete("Legacy writer properties are retired. Use SignalBus<T>.OpenParallelWriter or an owner route writer.", true)]
        public static NativeQueue<MovementAcousticSignal>.ParallelWriter MovementAcousticSignalWriter
        {
            get
            {
                return OpenSignalWriterForProducerPhase<MovementAcousticSignal>();
            }
        }

        /// <summary>Hypoxia legacy bridge writer for low-frequency compatibility producers.</summary>
        [global::System.Obsolete("Legacy writer properties are retired. Use SignalBus<T>.OpenParallelWriter or an owner route writer.", true)]
        public static NativeQueue<HypoxiaSignal>.ParallelWriter HypoxiaSignalWriter
        {
            get
            {
                return OpenSignalWriterForProducerPhase<HypoxiaSignal>();
            }
        }

        /// <summary>Scan completion legacy bridge writer for low-frequency compatibility producers.</summary>
        [global::System.Obsolete("Legacy writer properties are retired. Use SignalBus<T>.OpenParallelWriter or an owner route writer.", true)]
        public static NativeQueue<ScanCompleteSignal>.ParallelWriter ScanCompleteSignalWriter
        {
            get
            {
                return OpenSignalWriterForProducerPhase<ScanCompleteSignal>();
            }
        }

        /// <summary>Blueprint unlock legacy bridge writer for low-frequency compatibility producers.</summary>
        [global::System.Obsolete("Legacy writer properties are retired. Use SignalBus<T>.OpenParallelWriter or an owner route writer.", true)]
        public static NativeQueue<BlueprintUnlockedSignal>.ParallelWriter BlueprintUnlockedSignalWriter
        {
            get
            {
                return OpenSignalWriterForProducerPhase<BlueprintUnlockedSignal>();
            }
        }

        /// <summary>Crafting-start legacy bridge writer for low-frequency compatibility producers.</summary>
        [global::System.Obsolete("Legacy writer properties are retired. Use SignalBus<T>.OpenParallelWriter or an owner route writer.", true)]
        public static NativeQueue<CraftingStartedSignal>.ParallelWriter CraftingStartedSignalWriter
        {
            get
            {
                return OpenSignalWriterForProducerPhase<CraftingStartedSignal>();
            }
        }

        /// <summary>Crafting-completed legacy bridge writer for low-frequency compatibility producers.</summary>
        [global::System.Obsolete("Legacy writer properties are retired. Use SignalBus<T>.OpenParallelWriter or an owner route writer.", true)]
        public static NativeQueue<CraftingCompletedSignal>.ParallelWriter CraftingCompletedSignalWriter
        {
            get
            {
                return OpenSignalWriterForProducerPhase<CraftingCompletedSignal>();
            }
        }

        /// <summary>Tool acoustic legacy bridge writer for low-frequency compatibility producers.</summary>
        [global::System.Obsolete("Legacy writer properties are retired. Use SignalBus<T>.OpenParallelWriter or an owner route writer.", true)]
        public static NativeQueue<ToolAcousticSignal>.ParallelWriter ToolAcousticSignalWriter
        {
            get
            {
                return OpenSignalWriterForProducerPhase<ToolAcousticSignal>();
            }
        }

        /// <summary>Tool state legacy bridge writer for low-frequency compatibility producers.</summary>
        [global::System.Obsolete("Legacy writer properties are retired. Use SignalBus<T>.OpenParallelWriter or an owner route writer.", true)]
        public static NativeQueue<ToolStateChangedSignal>.ParallelWriter ToolStateChangedSignalWriter
        {
            get
            {
                return OpenSignalWriterForProducerPhase<ToolStateChangedSignal>();
            }
        }

        /// <summary>Power-drain legacy bridge writer for low-frequency crafting and power-network producers.</summary>
        [global::System.Obsolete("Legacy writer properties are retired. Use SignalBus<T>.OpenParallelWriter or an owner route writer.", true)]
        public static NativeQueue<PowerDrainSignal>.ParallelWriter PowerDrainSignalWriter
        {
            get
            {
                return OpenSignalWriterForProducerPhase<PowerDrainSignal>();
            }
        }

        /// <summary>Habitat deconstruction request legacy bridge writer for low-frequency tool producers.</summary>
        [global::System.Obsolete("Legacy writer properties are retired. Use SignalBus<T>.OpenParallelWriter or an owner route writer.", true)]
        public static NativeQueue<DeconstructRequestSignal>.ParallelWriter DeconstructRequestSignalWriter
        {
            get
            {
                return OpenSignalWriterForProducerPhase<DeconstructRequestSignal>();
            }
        }

        /// <summary>Habitat deconstruction result legacy bridge writer for low-frequency validation/execution producers.</summary>
        [global::System.Obsolete("Legacy writer properties are retired. Use SignalBus<T>.OpenParallelWriter or an owner route writer.", true)]
        public static NativeQueue<DeconstructResultSignal>.ParallelWriter DeconstructResultSignalWriter
        {
            get
            {
                return OpenSignalWriterForProducerPhase<DeconstructResultSignal>();
            }
        }

        /// <summary>Tool trigger legacy bridge writer for low-frequency device bridge producers.</summary>
        [global::System.Obsolete("Legacy writer properties are retired. Use SignalBus<T>.OpenParallelWriter or an owner route writer.", true)]
        public static NativeQueue<ToolTriggerSignal>.ParallelWriter ToolTriggerSignalWriter
        {
            get
            {
                return OpenSignalWriterForProducerPhase<ToolTriggerSignal>();
            }
        }

        /// <summary>HUD notification legacy bridge writer for low-frequency compatibility producers.</summary>
        [global::System.Obsolete("Legacy writer properties are retired. Use SignalBus<T>.OpenParallelWriter or an owner route writer.", true)]
        public static NativeQueue<HUDNotificationSignal>.ParallelWriter HUDNotificationSignalWriter
        {
            get
            {
                return OpenSignalWriterForProducerPhase<HUDNotificationSignal>();
            }
        }

        /// <summary>Rigidbody sleep-state legacy bridge writer for low-frequency compatibility producers.</summary>
        [global::System.Obsolete("Legacy writer properties are retired. Use SignalBus<T>.OpenParallelWriter or an owner route writer.", true)]
        public static NativeQueue<RigidbodySleepSignal>.ParallelWriter RigidbodySleepSignalWriter
        {
            get
            {
                return OpenSignalWriterForProducerPhase<RigidbodySleepSignal>();
            }
        }

        /// <summary>Fluid pipe rupture legacy bridge writer for low-frequency graph bridge producers.</summary>
        [global::System.Obsolete("Legacy writer properties are retired. Use SignalBus<T>.OpenParallelWriter or an owner route writer.", true)]
        public static NativeQueue<PipeRuptureSignal>.ParallelWriter PipeRuptureSignalWriter
        {
            get
            {
                return OpenSignalWriterForProducerPhase<PipeRuptureSignal>();
            }
        }

        /// <summary>Scanner-active legacy bridge writer for low-frequency compatibility producers.</summary>
        [global::System.Obsolete("Legacy writer properties are retired. Use SignalBus<T>.OpenParallelWriter or an owner route writer.", true)]
        public static NativeQueue<ScannerToolActiveSignal>.ParallelWriter ScannerToolActiveSignalWriter
        {
            get
            {
                return OpenSignalWriterForProducerPhase<ScannerToolActiveSignal>();
            }
        }

        /// <summary>Global time synchronization legacy bridge writer for low-frequency compatibility producers.</summary>
        [global::System.Obsolete("Legacy writer properties are retired. Use SignalBus<T>.OpenParallelWriter or an owner route writer.", true)]
        public static NativeQueue<GlobalTimeSyncSignal>.ParallelWriter GlobalTimeSyncSignalWriter
        {
            get
            {
                return OpenSignalWriterForProducerPhase<GlobalTimeSyncSignal>();
            }
        }

        /// <summary>Deterministic seismic shake legacy bridge writer for low-frequency compatibility producers.</summary>
        [global::System.Obsolete("Legacy writer properties are retired. Use SignalBus<T>.OpenParallelWriter or an owner route writer.", true)]
        public static NativeQueue<SeismicSignal>.ParallelWriter SeismicSignalWriter
        {
            get
            {
                return OpenSignalWriterForProducerPhase<SeismicSignal>();
            }
        }

        /// <summary>Ore/resource yield legacy bridge writer for low-frequency compatibility producers.</summary>
        [global::System.Obsolete("Legacy writer properties are retired. Use SignalBus<T>.OpenParallelWriter or an owner route writer.", true)]
        public static NativeQueue<ItemAcquiredSignal>.ParallelWriter ItemAcquiredSignalWriter
        {
            get
            {
                return OpenSignalWriterForProducerPhase<ItemAcquiredSignal>();
            }
        }

        /// <summary>Radiation dose legacy bridge writer for low-frequency physiology and hazard-grid producers.</summary>
        [global::System.Obsolete("Legacy writer properties are retired. Use SignalBus<T>.OpenParallelWriter or an owner route writer.", true)]
        public static NativeQueue<RadiationDoseSignal>.ParallelWriter RadiationDoseSignalWriter
        {
            get
            {
                return OpenSignalWriterForProducerPhase<RadiationDoseSignal>();
            }
        }

        /// <summary>Ore depletion delta legacy bridge writer for low-frequency compatibility producers.</summary>
        [global::System.Obsolete("Legacy writer properties are retired. Use SignalBus<T>.OpenParallelWriter or an owner route writer.", true)]
        public static NativeQueue<ResourceDepletionDeltaSignal>.ParallelWriter ResourceDepletionDeltaSignalWriter
        {
            get
            {
                return OpenSignalWriterForProducerPhase<ResourceDepletionDeltaSignal>();
            }
        }

        /// <summary>Narrative progression legacy bridge writer for low-frequency compatibility producers.</summary>
        [global::System.Obsolete("Legacy writer properties are retired. Use SignalBus<T>.OpenParallelWriter or an owner route writer.", true)]
        public static NativeQueue<ProgressionEventSignal>.ParallelWriter ProgressionEventSignalWriter
        {
            get
            {
                return OpenSignalWriterForProducerPhase<ProgressionEventSignal>();
            }
        }

        /// <summary>Global narrative state legacy bridge writer for low-frequency compatibility producers.</summary>
        [global::System.Obsolete("Legacy writer properties are retired. Use SignalBus<T>.OpenParallelWriter or an owner route writer.", true)]
        public static NativeQueue<GlobalWorldStateSignal>.ParallelWriter GlobalWorldStateSignalWriter
        {
            get
            {
                return OpenSignalWriterForProducerPhase<GlobalWorldStateSignal>();
            }
        }

        /// <summary>Biome transition legacy bridge writer for low-frequency compatibility producers.</summary>
        [global::System.Obsolete("Legacy writer properties are retired. Use SignalBus<T>.OpenParallelWriter or an owner route writer.", true)]
        public static NativeQueue<BiomeChangedSignal>.ParallelWriter BiomeChangedSignalWriter
        {
            get
            {
                return OpenSignalWriterForProducerPhase<BiomeChangedSignal>();
            }
        }

        /// <summary>Crash/postmortem telemetry writer for watchdog producers.</summary>
        [global::System.Obsolete("Legacy writer properties are retired. Use SignalBus<T>.OpenParallelWriter or an owner route writer.", true)]
        public static NativeQueue<CrashTelemetrySignal>.ParallelWriter CrashTelemetrySignalWriter
        {
            get
            {
                return OpenSignalWriterForProducerPhase<CrashTelemetrySignal>();
            }
        }

        /// <summary>Initializes every native signal lane during bootstrap prewarm.</summary>
    }
}
