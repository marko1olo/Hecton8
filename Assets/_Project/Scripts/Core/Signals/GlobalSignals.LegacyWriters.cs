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
        private static global::Hecton8.Core.MpscSignalRingBuffer<TSignal>.ParallelWriter OpenSignalWriterForProducerPhase<TSignal>()
            where TSignal : unmanaged, ISignal
        {
            return SignalBus<TSignal>.OpenParallelWriter();
        }

        // Compatibility writer properties below preserve source-level bridge names while returning bounded first-party MPSC writers.
        // Maintained Core producer code uses SignalBus<T>.OpenParallelWriter() or owner-local scratch.

        /// <summary>Damage routing bounded ring writer for low-frequency compatibility producers.</summary>
        [global::System.Obsolete("Legacy property names are retired. Use SignalBus<T>.OpenParallelWriter or an owner route writer.", true)]
        public static global::Hecton8.Core.MpscSignalRingBuffer<CombatDamageSignal>.ParallelWriter DamageSignalWriter
        {
            get
            {
                return OpenSignalWriterForProducerPhase<CombatDamageSignal>();
            }
        }

        /// <summary>Physics impact bounded ring writer for low-frequency compatibility producers.</summary>
        [global::System.Obsolete("Legacy property names are retired. Use SignalBus<T>.OpenParallelWriter or an owner route writer.", true)]
        public static global::Hecton8.Core.MpscSignalRingBuffer<ImpactSignal>.ParallelWriter ImpactSignalWriter
        {
            get
            {
                return OpenSignalWriterForProducerPhase<ImpactSignal>();
            }
        }

        /// <summary>AUP pre-shift bounded ring writer for low-frequency compatibility producers.</summary>
        [global::System.Obsolete("Legacy property names are retired. Use SignalBus<T>.OpenParallelWriter or an owner route writer.", true)]
        public static global::Hecton8.Core.MpscSignalRingBuffer<AupPreShiftSignal>.ParallelWriter AupPreShiftSignalWriter
        {
            get
            {
                return OpenSignalWriterForProducerPhase<AupPreShiftSignal>();
            }
        }

        /// <summary>AUP shift bounded ring writer for low-frequency compatibility producers.</summary>
        [global::System.Obsolete("Legacy property names are retired. Use SignalBus<T>.OpenParallelWriter or an owner route writer.", true)]
        public static global::Hecton8.Core.MpscSignalRingBuffer<AupShiftSignal>.ParallelWriter AupShiftSignalWriter
        {
            get
            {
                return OpenSignalWriterForProducerPhase<AupShiftSignal>();
            }
        }

        /// <summary>Logistics brownout bounded ring writer for low-frequency compatibility producers.</summary>
        [global::System.Obsolete("Legacy property names are retired. Use SignalBus<T>.OpenParallelWriter or an owner route writer.", true)]
        public static global::Hecton8.Core.MpscSignalRingBuffer<BrownoutSignal>.ParallelWriter BrownoutSignalWriter
        {
            get
            {
                return OpenSignalWriterForProducerPhase<BrownoutSignal>();
            }
        }

        /// <summary>Armor deflection bounded ring writer for low-frequency compatibility producers.</summary>
        [global::System.Obsolete("Legacy property names are retired. Use SignalBus<T>.OpenParallelWriter or an owner route writer.", true)]
        public static global::Hecton8.Core.MpscSignalRingBuffer<DeflectSignal>.ParallelWriter DeflectSignalWriter
        {
            get
            {
                return OpenSignalWriterForProducerPhase<DeflectSignal>();
            }
        }

        /// <summary>Entity death bounded ring writer for low-frequency compatibility producers.</summary>
        [global::System.Obsolete("Legacy property names are retired. Use SignalBus<T>.OpenParallelWriter or an owner route writer.", true)]
        public static global::Hecton8.Core.MpscSignalRingBuffer<EntityDeathSignal>.ParallelWriter EntityDeathSignalWriter
        {
            get
            {
                return OpenSignalWriterForProducerPhase<EntityDeathSignal>();
            }
        }

        /// <summary>Runtime anomaly bounded ring writer for low-frequency compatibility producers.</summary>
        [global::System.Obsolete("Legacy property names are retired. Use SignalBus<T>.OpenParallelWriter or an owner route writer.", true)]
        public static global::Hecton8.Core.MpscSignalRingBuffer<AnomalySignal>.ParallelWriter AnomalySignalWriter
        {
            get
            {
                return OpenSignalWriterForProducerPhase<AnomalySignal>();
            }
        }

        /// <summary>Acoustic ping bounded ring writer for low-frequency compatibility producers.</summary>
        [global::System.Obsolete("Legacy property names are retired. Use SignalBus<T>.OpenParallelWriter or an owner route writer.", true)]
        public static global::Hecton8.Core.MpscSignalRingBuffer<AcousticPingSignal>.ParallelWriter AcousticPingSignalWriter
        {
            get
            {
                return OpenSignalWriterForProducerPhase<AcousticPingSignal>();
            }
        }

        /// <summary>Movement acoustic bounded ring writer for low-frequency compatibility producers.</summary>
        [global::System.Obsolete("Legacy property names are retired. Use SignalBus<T>.OpenParallelWriter or an owner route writer.", true)]
        public static global::Hecton8.Core.MpscSignalRingBuffer<MovementAcousticSignal>.ParallelWriter MovementAcousticSignalWriter
        {
            get
            {
                return OpenSignalWriterForProducerPhase<MovementAcousticSignal>();
            }
        }

        /// <summary>Hypoxia bounded ring writer for low-frequency compatibility producers.</summary>
        [global::System.Obsolete("Legacy property names are retired. Use SignalBus<T>.OpenParallelWriter or an owner route writer.", true)]
        public static global::Hecton8.Core.MpscSignalRingBuffer<HypoxiaSignal>.ParallelWriter HypoxiaSignalWriter
        {
            get
            {
                return OpenSignalWriterForProducerPhase<HypoxiaSignal>();
            }
        }

        /// <summary>Scan completion bounded ring writer for low-frequency compatibility producers.</summary>
        [global::System.Obsolete("Legacy property names are retired. Use SignalBus<T>.OpenParallelWriter or an owner route writer.", true)]
        public static global::Hecton8.Core.MpscSignalRingBuffer<ScanCompleteSignal>.ParallelWriter ScanCompleteSignalWriter
        {
            get
            {
                return OpenSignalWriterForProducerPhase<ScanCompleteSignal>();
            }
        }

        /// <summary>Blueprint unlock bounded ring writer for low-frequency compatibility producers.</summary>
        [global::System.Obsolete("Legacy property names are retired. Use SignalBus<T>.OpenParallelWriter or an owner route writer.", true)]
        public static global::Hecton8.Core.MpscSignalRingBuffer<BlueprintUnlockedSignal>.ParallelWriter BlueprintUnlockedSignalWriter
        {
            get
            {
                return OpenSignalWriterForProducerPhase<BlueprintUnlockedSignal>();
            }
        }

        /// <summary>Crafting-start bounded ring writer for low-frequency compatibility producers.</summary>
        [global::System.Obsolete("Legacy property names are retired. Use SignalBus<T>.OpenParallelWriter or an owner route writer.", true)]
        public static global::Hecton8.Core.MpscSignalRingBuffer<CraftingStartedSignal>.ParallelWriter CraftingStartedSignalWriter
        {
            get
            {
                return OpenSignalWriterForProducerPhase<CraftingStartedSignal>();
            }
        }

        /// <summary>Crafting-completed bounded ring writer for low-frequency compatibility producers.</summary>
        [global::System.Obsolete("Legacy property names are retired. Use SignalBus<T>.OpenParallelWriter or an owner route writer.", true)]
        public static global::Hecton8.Core.MpscSignalRingBuffer<CraftingCompletedSignal>.ParallelWriter CraftingCompletedSignalWriter
        {
            get
            {
                return OpenSignalWriterForProducerPhase<CraftingCompletedSignal>();
            }
        }

        /// <summary>Tool acoustic bounded ring writer for low-frequency compatibility producers.</summary>
        [global::System.Obsolete("Legacy property names are retired. Use SignalBus<T>.OpenParallelWriter or an owner route writer.", true)]
        public static global::Hecton8.Core.MpscSignalRingBuffer<ToolAcousticSignal>.ParallelWriter ToolAcousticSignalWriter
        {
            get
            {
                return OpenSignalWriterForProducerPhase<ToolAcousticSignal>();
            }
        }

        /// <summary>Tool state bounded ring writer for low-frequency compatibility producers.</summary>
        [global::System.Obsolete("Legacy property names are retired. Use SignalBus<T>.OpenParallelWriter or an owner route writer.", true)]
        public static global::Hecton8.Core.MpscSignalRingBuffer<ToolStateChangedSignal>.ParallelWriter ToolStateChangedSignalWriter
        {
            get
            {
                return OpenSignalWriterForProducerPhase<ToolStateChangedSignal>();
            }
        }

        /// <summary>Power-drain bounded ring writer for low-frequency crafting and power-network producers.</summary>
        [global::System.Obsolete("Legacy property names are retired. Use SignalBus<T>.OpenParallelWriter or an owner route writer.", true)]
        public static global::Hecton8.Core.MpscSignalRingBuffer<PowerDrainSignal>.ParallelWriter PowerDrainSignalWriter
        {
            get
            {
                return OpenSignalWriterForProducerPhase<PowerDrainSignal>();
            }
        }

        /// <summary>Habitat deconstruction request bounded ring writer for low-frequency tool producers.</summary>
        [global::System.Obsolete("Legacy property names are retired. Use SignalBus<T>.OpenParallelWriter or an owner route writer.", true)]
        public static global::Hecton8.Core.MpscSignalRingBuffer<DeconstructRequestSignal>.ParallelWriter DeconstructRequestSignalWriter
        {
            get
            {
                return OpenSignalWriterForProducerPhase<DeconstructRequestSignal>();
            }
        }

        /// <summary>Habitat deconstruction result bounded ring writer for low-frequency validation/execution producers.</summary>
        [global::System.Obsolete("Legacy property names are retired. Use SignalBus<T>.OpenParallelWriter or an owner route writer.", true)]
        public static global::Hecton8.Core.MpscSignalRingBuffer<DeconstructResultSignal>.ParallelWriter DeconstructResultSignalWriter
        {
            get
            {
                return OpenSignalWriterForProducerPhase<DeconstructResultSignal>();
            }
        }

        /// <summary>Tool trigger bounded ring writer for low-frequency device bridge producers.</summary>
        [global::System.Obsolete("Legacy property names are retired. Use SignalBus<T>.OpenParallelWriter or an owner route writer.", true)]
        public static global::Hecton8.Core.MpscSignalRingBuffer<ToolTriggerSignal>.ParallelWriter ToolTriggerSignalWriter
        {
            get
            {
                return OpenSignalWriterForProducerPhase<ToolTriggerSignal>();
            }
        }

        /// <summary>HUD notification bounded ring writer for low-frequency compatibility producers.</summary>
        [global::System.Obsolete("Legacy property names are retired. Use SignalBus<T>.OpenParallelWriter or an owner route writer.", true)]
        public static global::Hecton8.Core.MpscSignalRingBuffer<HUDNotificationSignal>.ParallelWriter HUDNotificationSignalWriter
        {
            get
            {
                return OpenSignalWriterForProducerPhase<HUDNotificationSignal>();
            }
        }

        /// <summary>Rigidbody sleep-state bounded ring writer for low-frequency compatibility producers.</summary>
        [global::System.Obsolete("Legacy property names are retired. Use SignalBus<T>.OpenParallelWriter or an owner route writer.", true)]
        public static global::Hecton8.Core.MpscSignalRingBuffer<RigidbodySleepSignal>.ParallelWriter RigidbodySleepSignalWriter
        {
            get
            {
                return OpenSignalWriterForProducerPhase<RigidbodySleepSignal>();
            }
        }

        /// <summary>Fluid pipe rupture bounded ring writer for low-frequency graph bridge producers.</summary>
        [global::System.Obsolete("Legacy property names are retired. Use SignalBus<T>.OpenParallelWriter or an owner route writer.", true)]
        public static global::Hecton8.Core.MpscSignalRingBuffer<PipeRuptureSignal>.ParallelWriter PipeRuptureSignalWriter
        {
            get
            {
                return OpenSignalWriterForProducerPhase<PipeRuptureSignal>();
            }
        }

        /// <summary>Scanner-active bounded ring writer for low-frequency compatibility producers.</summary>
        [global::System.Obsolete("Legacy property names are retired. Use SignalBus<T>.OpenParallelWriter or an owner route writer.", true)]
        public static global::Hecton8.Core.MpscSignalRingBuffer<ScannerToolActiveSignal>.ParallelWriter ScannerToolActiveSignalWriter
        {
            get
            {
                return OpenSignalWriterForProducerPhase<ScannerToolActiveSignal>();
            }
        }

        /// <summary>Global time synchronization bounded ring writer for low-frequency compatibility producers.</summary>
        [global::System.Obsolete("Legacy property names are retired. Use SignalBus<T>.OpenParallelWriter or an owner route writer.", true)]
        public static global::Hecton8.Core.MpscSignalRingBuffer<GlobalTimeSyncSignal>.ParallelWriter GlobalTimeSyncSignalWriter
        {
            get
            {
                return OpenSignalWriterForProducerPhase<GlobalTimeSyncSignal>();
            }
        }

        /// <summary>Deterministic seismic shake bounded ring writer for low-frequency compatibility producers.</summary>
        [global::System.Obsolete("Legacy property names are retired. Use SignalBus<T>.OpenParallelWriter or an owner route writer.", true)]
        public static global::Hecton8.Core.MpscSignalRingBuffer<SeismicSignal>.ParallelWriter SeismicSignalWriter
        {
            get
            {
                return OpenSignalWriterForProducerPhase<SeismicSignal>();
            }
        }

        /// <summary>Ore/resource yield bounded ring writer for low-frequency compatibility producers.</summary>
        [global::System.Obsolete("Legacy property names are retired. Use SignalBus<T>.OpenParallelWriter or an owner route writer.", true)]
        public static global::Hecton8.Core.MpscSignalRingBuffer<ItemAcquiredSignal>.ParallelWriter ItemAcquiredSignalWriter
        {
            get
            {
                return OpenSignalWriterForProducerPhase<ItemAcquiredSignal>();
            }
        }

        /// <summary>Radiation dose bounded ring writer for low-frequency physiology and hazard-grid producers.</summary>
        [global::System.Obsolete("Legacy property names are retired. Use SignalBus<T>.OpenParallelWriter or an owner route writer.", true)]
        public static global::Hecton8.Core.MpscSignalRingBuffer<RadiationDoseSignal>.ParallelWriter RadiationDoseSignalWriter
        {
            get
            {
                return OpenSignalWriterForProducerPhase<RadiationDoseSignal>();
            }
        }

        /// <summary>Ore depletion delta bounded ring writer for low-frequency compatibility producers.</summary>
        [global::System.Obsolete("Legacy property names are retired. Use SignalBus<T>.OpenParallelWriter or an owner route writer.", true)]
        public static global::Hecton8.Core.MpscSignalRingBuffer<ResourceDepletionDeltaSignal>.ParallelWriter ResourceDepletionDeltaSignalWriter
        {
            get
            {
                return OpenSignalWriterForProducerPhase<ResourceDepletionDeltaSignal>();
            }
        }

        /// <summary>Narrative progression bounded ring writer for low-frequency compatibility producers.</summary>
        [global::System.Obsolete("Legacy property names are retired. Use SignalBus<T>.OpenParallelWriter or an owner route writer.", true)]
        public static global::Hecton8.Core.MpscSignalRingBuffer<ProgressionEventSignal>.ParallelWriter ProgressionEventSignalWriter
        {
            get
            {
                return OpenSignalWriterForProducerPhase<ProgressionEventSignal>();
            }
        }

        /// <summary>Global narrative state bounded ring writer for low-frequency compatibility producers.</summary>
        [global::System.Obsolete("Legacy property names are retired. Use SignalBus<T>.OpenParallelWriter or an owner route writer.", true)]
        public static global::Hecton8.Core.MpscSignalRingBuffer<GlobalWorldStateSignal>.ParallelWriter GlobalWorldStateSignalWriter
        {
            get
            {
                return OpenSignalWriterForProducerPhase<GlobalWorldStateSignal>();
            }
        }

        /// <summary>Biome transition bounded ring writer for low-frequency compatibility producers.</summary>
        [global::System.Obsolete("Legacy property names are retired. Use SignalBus<T>.OpenParallelWriter or an owner route writer.", true)]
        public static global::Hecton8.Core.MpscSignalRingBuffer<BiomeChangedSignal>.ParallelWriter BiomeChangedSignalWriter
        {
            get
            {
                return OpenSignalWriterForProducerPhase<BiomeChangedSignal>();
            }
        }

        /// <summary>Crash/postmortem telemetry writer for watchdog producers.</summary>
        [global::System.Obsolete("Legacy property names are retired. Use SignalBus<T>.OpenParallelWriter or an owner route writer.", true)]
        public static global::Hecton8.Core.MpscSignalRingBuffer<CrashTelemetrySignal>.ParallelWriter CrashTelemetrySignalWriter
        {
            get
            {
                return OpenSignalWriterForProducerPhase<CrashTelemetrySignal>();
            }
        }

        /// <summary>Initializes every native signal lane during bootstrap prewarm.</summary>
    }
}
