using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using Hecton8.Core.Signals;
using Hecton8.World;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Core
{
    /// <summary>
    /// Global native signal corridor. Producers enqueue unmanaged packets; consumers drain their own lanes.
    /// </summary>
    public static class GlobalSignals
    {
        private const int DamageSignalCapacity = 256;
        private const int ImpactSignalCapacity = 256;
        private const int AupPreShiftSignalCapacity = 64;
        private const int AupShiftSignalCapacity = 64;
        private const int BrownoutSignalCapacity = 64;
        private const int DebrisSpawnSignalCapacity = 128;
        private const int DeflectSignalCapacity = 128;
        private const int EntityDeathSignalCapacity = 64;
        private const int SolarFlareSignalCapacity = 16;
        private const int RebaseSignalCapacity = 64;
        private const int ControlSignalCapacity = 256;
        private const int AnomalySignalCapacity = 128;
        private const int TelemetryAnomalySignalCapacity = 128;
        private const int HabitatConstructionSignalCapacity = 64;
        private const int VocalWarningSignalCapacity = 64;
        private const int DataReloadSignalCapacity = 32;
        private const int MemoryPressureSignalCapacity = 16;
        private const int AcousticPingSignalCapacity = 64;
        private const int MovementAcousticSignalCapacity = 128;
        private const int SonarPingSignalCapacity = 64;
        private const int HypoxiaSignalCapacity = 32;
        private const int OxygenCriticalSignalCapacity = 32;
        private const int InteractionUiSignalCapacity = 128;
        private const int FluidIncursionSignalCapacity = 64;
        private const int SpectrumScanSignalCapacity = 128;
        private const int RigidbodySleepSignalCapacity = 128;
        private const int ScanCompleteSignalCapacity = 128;
        private const int BlueprintUnlockedSignalCapacity = 128;
        private const int ToolAcousticSignalCapacity = 128;
        private const int HUDNotificationSignalCapacity = 128;
        private const int ReconDataSignalCapacity = 128;
        private const int SaveLifecycleSignalCapacity = 16;
        private const int ComplianceViolationSignalCapacity = 64;
        private const int GlobalTimeSyncSignalCapacity = 16;
        private const int WeatherStrengthSignalCapacity = 32;
        private const int ItemDecaySignalCapacity = 64;
        private const int PlayerStressSignalCapacity = 64;

        private static NativeQueue<DamageSignal> _damageSignals;
        private static NativeQueue<ImpactSignal> _impactSignals;
        private static NativeQueue<AupPreShiftSignal> _aupPreShiftSignals;
        private static NativeQueue<AupShiftSignal> _aupShiftSignals;
        private static NativeQueue<BrownoutSignal> _brownoutSignals;
        private static NativeQueue<DebrisSpawnSignal> _debrisSpawnSignals;
        private static NativeQueue<DeflectSignal> _deflectSignals;
        private static NativeQueue<EntityDeathSignal> _entityDeathSignals;
        private static NativeQueue<SolarFlareSignal> _solarFlareSignals;
        private static NativeQueue<RebaseSignal> _rebaseSignals;
        private static NativeQueue<ControlSignal> _controlSignals;
        private static NativeQueue<AnomalySignal> _anomalySignals;
        private static NativeQueue<TelemetryAnomalySignal> _telemetryAnomalySignals;
        private static NativeQueue<HabitatConstructionSignal> _habitatConstructionSignals;
        private static NativeQueue<VocalWarningSignal> _vocalWarningSignals;
        private static NativeQueue<DataReloadSignal> _dataReloadSignals;
        private static NativeQueue<MemoryPressureSignal> _memoryPressureSignals;
        private static NativeQueue<AcousticPingSignal> _acousticPingSignals;
        private static NativeQueue<MovementAcousticSignal> _movementAcousticSignals;
        private static NativeQueue<SonarPingSignal> _sonarPingSignals;
        private static NativeQueue<HypoxiaSignal> _hypoxiaSignals;
        private static NativeQueue<OxygenCriticalSignal> _oxygenCriticalSignals;
        private static NativeQueue<InteractionUiSignal> _interactionUiSignals;
        private static NativeQueue<FluidIncursionSignal> _fluidIncursionSignals;
        private static NativeQueue<SpectrumScanSignal> _spectrumScanSignals;
        private static NativeQueue<RigidbodySleepSignal> _rigidbodySleepSignals;
        private static NativeQueue<ScanCompleteSignal> _scanCompleteSignals;
        private static NativeQueue<BlueprintUnlockedSignal> _blueprintUnlockedSignals;
        private static NativeQueue<ToolAcousticSignal> _toolAcousticSignals;
        private static NativeQueue<HUDNotificationSignal> _hudNotificationSignals;
        private static NativeQueue<ReconDataSignal> _reconDataSignals;
        private static NativeQueue<SaveLifecycleSignal> _saveLifecycleSignals;
        private static NativeQueue<ComplianceViolationSignal> _complianceViolationSignals;
        private static NativeQueue<GlobalTimeSyncSignal> _globalTimeSyncSignals;
        private static NativeQueue<WeatherStrengthSignal> _weatherStrengthSignals;
        private static NativeQueue<ItemDecaySignal> _itemDecaySignals;
        private static NativeQueue<PlayerStressSignal> _playerStressSignals;
        private static bool _initialized;

        /// <summary>Damage routing writer for Burst jobs or background producers.</summary>
        public static NativeQueue<DamageSignal>.ParallelWriter DamageSignalWriter
        {
            get
            {
                EnsureInitialized();
                return _damageSignals.AsParallelWriter();
            }
        }

        /// <summary>Physics impact writer for Burst jobs or background producers.</summary>
        public static NativeQueue<ImpactSignal>.ParallelWriter ImpactSignalWriter
        {
            get
            {
                EnsureInitialized();
                return _impactSignals.AsParallelWriter();
            }
        }

        /// <summary>AUP shift broadcast writer for Burst jobs or background producers.</summary>
        public static NativeQueue<AupPreShiftSignal>.ParallelWriter AupPreShiftSignalWriter
        {
            get
            {
                EnsureInitialized();
                return _aupPreShiftSignals.AsParallelWriter();
            }
        }

        /// <summary>AUP shift broadcast writer for Burst jobs or background producers.</summary>
        public static NativeQueue<AupShiftSignal>.ParallelWriter AupShiftSignalWriter
        {
            get
            {
                EnsureInitialized();
                return _aupShiftSignals.AsParallelWriter();
            }
        }

        /// <summary>Logistics brownout writer for Burst jobs or background producers.</summary>
        public static NativeQueue<BrownoutSignal>.ParallelWriter BrownoutSignalWriter
        {
            get
            {
                EnsureInitialized();
                return _brownoutSignals.AsParallelWriter();
            }
        }

        /// <summary>Armor deflection writer for Burst combat jobs.</summary>
        public static NativeQueue<DeflectSignal>.ParallelWriter DeflectSignalWriter
        {
            get
            {
                EnsureInitialized();
                return _deflectSignals.AsParallelWriter();
            }
        }

        /// <summary>Entity death writer for Burst producers.</summary>
        public static NativeQueue<EntityDeathSignal>.ParallelWriter EntityDeathSignalWriter
        {
            get
            {
                EnsureInitialized();
                return _entityDeathSignals.AsParallelWriter();
            }
        }

        /// <summary>Runtime anomaly writer for Burst jobs or background producers.</summary>
        public static NativeQueue<AnomalySignal>.ParallelWriter AnomalySignalWriter
        {
            get
            {
                EnsureInitialized();
                return _anomalySignals.AsParallelWriter();
            }
        }

        /// <summary>Acoustic ping writer for Burst jobs or background producers.</summary>
        public static NativeQueue<AcousticPingSignal>.ParallelWriter AcousticPingSignalWriter
        {
            get
            {
                EnsureInitialized();
                return _acousticPingSignals.AsParallelWriter();
            }
        }

        /// <summary>Movement acoustic writer for Burst jobs or background producers.</summary>
        public static NativeQueue<MovementAcousticSignal>.ParallelWriter MovementAcousticSignalWriter
        {
            get
            {
                EnsureInitialized();
                return _movementAcousticSignals.AsParallelWriter();
            }
        }

        /// <summary>Hypoxia writer for Burst jobs or background producers.</summary>
        public static NativeQueue<HypoxiaSignal>.ParallelWriter HypoxiaSignalWriter
        {
            get
            {
                EnsureInitialized();
                return _hypoxiaSignals.AsParallelWriter();
            }
        }

        /// <summary>Scan completion writer for Burst jobs or background producers.</summary>
        public static NativeQueue<ScanCompleteSignal>.ParallelWriter ScanCompleteSignalWriter
        {
            get
            {
                EnsureInitialized();
                return _scanCompleteSignals.AsParallelWriter();
            }
        }

        /// <summary>Blueprint unlock writer for Burst jobs or background producers.</summary>
        public static NativeQueue<BlueprintUnlockedSignal>.ParallelWriter BlueprintUnlockedSignalWriter
        {
            get
            {
                EnsureInitialized();
                return _blueprintUnlockedSignals.AsParallelWriter();
            }
        }

        /// <summary>Tool acoustic writer for Burst jobs or background producers.</summary>
        public static NativeQueue<ToolAcousticSignal>.ParallelWriter ToolAcousticSignalWriter
        {
            get
            {
                EnsureInitialized();
                return _toolAcousticSignals.AsParallelWriter();
            }
        }

        /// <summary>HUD notification writer for Burst jobs or background producers.</summary>
        public static NativeQueue<HUDNotificationSignal>.ParallelWriter HUDNotificationSignalWriter
        {
            get
            {
                EnsureInitialized();
                return _hudNotificationSignals.AsParallelWriter();
            }
        }

        /// <summary>Rigidbody sleep-state writer for Burst jobs or background producers.</summary>
        public static NativeQueue<RigidbodySleepSignal>.ParallelWriter RigidbodySleepSignalWriter
        {
            get
            {
                EnsureInitialized();
                return _rigidbodySleepSignals.AsParallelWriter();
            }
        }

        /// <summary>Global time synchronization writer for Burst jobs or background producers.</summary>
        public static NativeQueue<GlobalTimeSyncSignal>.ParallelWriter GlobalTimeSyncSignalWriter
        {
            get
            {
                EnsureInitialized();
                return _globalTimeSyncSignals.AsParallelWriter();
            }
        }

        /// <summary>Initializes every native signal lane during bootstrap prewarm.</summary>
        public static void InitializeAllQueues()
        {
            if (_initialized)
                return;

            CreateQueue(ref _damageSignals, DamageSignalCapacity, nameof(_damageSignals));
            CreateQueue(ref _impactSignals, ImpactSignalCapacity, nameof(_impactSignals));
            CreateQueue(ref _aupPreShiftSignals, AupPreShiftSignalCapacity, nameof(_aupPreShiftSignals));
            CreateQueue(ref _aupShiftSignals, AupShiftSignalCapacity, nameof(_aupShiftSignals));
            CreateQueue(ref _brownoutSignals, BrownoutSignalCapacity, nameof(_brownoutSignals));
            CreateQueue(ref _debrisSpawnSignals, DebrisSpawnSignalCapacity, nameof(_debrisSpawnSignals));
            CreateQueue(ref _deflectSignals, DeflectSignalCapacity, nameof(_deflectSignals));
            CreateQueue(ref _entityDeathSignals, EntityDeathSignalCapacity, nameof(_entityDeathSignals));
            CreateQueue(ref _solarFlareSignals, SolarFlareSignalCapacity, nameof(_solarFlareSignals));
            CreateQueue(ref _rebaseSignals, RebaseSignalCapacity, nameof(_rebaseSignals));
            CreateQueue(ref _controlSignals, ControlSignalCapacity, nameof(_controlSignals));
            CreateQueue(ref _anomalySignals, AnomalySignalCapacity, nameof(_anomalySignals));
            CreateQueue(ref _telemetryAnomalySignals, TelemetryAnomalySignalCapacity, nameof(_telemetryAnomalySignals));
            CreateQueue(ref _habitatConstructionSignals, HabitatConstructionSignalCapacity, nameof(_habitatConstructionSignals));
            CreateQueue(ref _vocalWarningSignals, VocalWarningSignalCapacity, nameof(_vocalWarningSignals));
            CreateQueue(ref _dataReloadSignals, DataReloadSignalCapacity, nameof(_dataReloadSignals));
            CreateQueue(ref _memoryPressureSignals, MemoryPressureSignalCapacity, nameof(_memoryPressureSignals));
            CreateQueue(ref _acousticPingSignals, AcousticPingSignalCapacity, nameof(_acousticPingSignals));
            CreateQueue(ref _movementAcousticSignals, MovementAcousticSignalCapacity, nameof(_movementAcousticSignals));
            CreateQueue(ref _sonarPingSignals, SonarPingSignalCapacity, nameof(_sonarPingSignals));
            CreateQueue(ref _hypoxiaSignals, HypoxiaSignalCapacity, nameof(_hypoxiaSignals));
            CreateQueue(ref _oxygenCriticalSignals, OxygenCriticalSignalCapacity, nameof(_oxygenCriticalSignals));
            CreateQueue(ref _interactionUiSignals, InteractionUiSignalCapacity, nameof(_interactionUiSignals));
            CreateQueue(ref _fluidIncursionSignals, FluidIncursionSignalCapacity, nameof(_fluidIncursionSignals));
            CreateQueue(ref _spectrumScanSignals, SpectrumScanSignalCapacity, nameof(_spectrumScanSignals));
            CreateQueue(ref _rigidbodySleepSignals, RigidbodySleepSignalCapacity, nameof(_rigidbodySleepSignals));
            CreateQueue(ref _scanCompleteSignals, ScanCompleteSignalCapacity, nameof(_scanCompleteSignals));
            CreateQueue(ref _blueprintUnlockedSignals, BlueprintUnlockedSignalCapacity, nameof(_blueprintUnlockedSignals));
            CreateQueue(ref _toolAcousticSignals, ToolAcousticSignalCapacity, nameof(_toolAcousticSignals));
            CreateQueue(ref _hudNotificationSignals, HUDNotificationSignalCapacity, nameof(_hudNotificationSignals));
            CreateQueue(ref _reconDataSignals, ReconDataSignalCapacity, nameof(_reconDataSignals));
            CreateQueue(ref _saveLifecycleSignals, SaveLifecycleSignalCapacity, nameof(_saveLifecycleSignals));
            CreateQueue(ref _complianceViolationSignals, ComplianceViolationSignalCapacity, nameof(_complianceViolationSignals));
            CreateQueue(ref _globalTimeSyncSignals, GlobalTimeSyncSignalCapacity, nameof(_globalTimeSyncSignals));
            CreateQueue(ref _weatherStrengthSignals, WeatherStrengthSignalCapacity, nameof(_weatherStrengthSignals));
            CreateQueue(ref _itemDecaySignals, ItemDecaySignalCapacity, nameof(_itemDecaySignals));
            CreateQueue(ref _playerStressSignals, PlayerStressSignalCapacity, nameof(_playerStressSignals));

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            ValidateSignalPayload<DamageSignal>(32);
            ValidateSignalPayload<ImpactSignal>(64);
            ValidateSignalPayload<AupPreShiftSignal>(32);
            ValidateSignalPayload<AupShiftSignal>(32);
            ValidateSignalSize<BrownoutSignal>(32);
            ValidateSignalSize<DebrisSpawnSignal>(64);
            ValidateSignalSize<DeflectSignal>(32);
            ValidateSignalSize<EntityDeathSignal>(64);
            ValidateSignalSize<SolarFlareSignal>(32);
            ValidateSignalSize<RebaseSignal>(32);
            ValidateSignalSize<ControlSignal>(32);
            ValidateSignalPayload<AnomalySignal>(32);
            ValidateSignalSize<TelemetryAnomalySignal>(32);
            ValidateSignalSize<HabitatConstructionSignal>(64);
            ValidateSignalSize<VocalWarningSignal>(32);
            ValidateSignalSize<DataReloadSignal>(32);
            ValidateSignalSize<MemoryPressureSignal>(32);
            ValidateSignalPayload<AcousticPingSignal>(64);
            ValidateSignalPayload<MovementAcousticSignal>(64);
            ValidateSignalSize<SonarPingSignal>(64);
            ValidateSignalPayload<HypoxiaSignal>(32);
            ValidateSignalSize<OxygenCriticalSignal>(32);
            ValidateSignalSize<InteractionUiSignal>(64);
            ValidateSignalSize<FluidIncursionSignal>(64);
            ValidateSignalSize<SpectrumScanSignal>(32);
            ValidateSignalSize<RigidbodySleepSignal>(64);
            ValidateSignalPayload<ScanCompleteSignal>(64);
            ValidateSignalSize<BlueprintUnlockedSignal>(32);
            ValidateSignalSize<ToolAcousticSignal>(32);
            ValidateSignalSize<HUDNotificationSignal>(32);
            ValidateSignalSize<ReconDataSignal>(64);
            ValidateSignalSize<SaveLifecycleSignal>(32);
            ValidateSignalSize<ComplianceViolationSignal>(32);
            ValidateSignalSize<GlobalTimeSyncSignal>(32);
            ValidateSignalSize<WeatherStrengthSignal>(32);
            ValidateSignalSize<ItemDecaySignal>(64);
            ValidateSignalSize<PlayerStressSignal>(32);
#endif

            _initialized = true;
        }

        /// <summary>Disposes every native signal lane. Call during clean application or session shutdown.</summary>
        public static void DisposeAllQueues()
        {
            DisposeQueue(ref _damageSignals, nameof(_damageSignals));
            DisposeQueue(ref _impactSignals, nameof(_impactSignals));
            DisposeQueue(ref _aupPreShiftSignals, nameof(_aupPreShiftSignals));
            DisposeQueue(ref _aupShiftSignals, nameof(_aupShiftSignals));
            DisposeQueue(ref _brownoutSignals, nameof(_brownoutSignals));
            DisposeQueue(ref _debrisSpawnSignals, nameof(_debrisSpawnSignals));
            DisposeQueue(ref _deflectSignals, nameof(_deflectSignals));
            DisposeQueue(ref _entityDeathSignals, nameof(_entityDeathSignals));
            DisposeQueue(ref _solarFlareSignals, nameof(_solarFlareSignals));
            DisposeQueue(ref _rebaseSignals, nameof(_rebaseSignals));
            DisposeQueue(ref _controlSignals, nameof(_controlSignals));
            DisposeQueue(ref _anomalySignals, nameof(_anomalySignals));
            DisposeQueue(ref _telemetryAnomalySignals, nameof(_telemetryAnomalySignals));
            DisposeQueue(ref _habitatConstructionSignals, nameof(_habitatConstructionSignals));
            DisposeQueue(ref _vocalWarningSignals, nameof(_vocalWarningSignals));
            DisposeQueue(ref _dataReloadSignals, nameof(_dataReloadSignals));
            DisposeQueue(ref _memoryPressureSignals, nameof(_memoryPressureSignals));
            DisposeQueue(ref _acousticPingSignals, nameof(_acousticPingSignals));
            DisposeQueue(ref _movementAcousticSignals, nameof(_movementAcousticSignals));
            DisposeQueue(ref _sonarPingSignals, nameof(_sonarPingSignals));
            DisposeQueue(ref _hypoxiaSignals, nameof(_hypoxiaSignals));
            DisposeQueue(ref _oxygenCriticalSignals, nameof(_oxygenCriticalSignals));
            DisposeQueue(ref _interactionUiSignals, nameof(_interactionUiSignals));
            DisposeQueue(ref _fluidIncursionSignals, nameof(_fluidIncursionSignals));
            DisposeQueue(ref _spectrumScanSignals, nameof(_spectrumScanSignals));
            DisposeQueue(ref _rigidbodySleepSignals, nameof(_rigidbodySleepSignals));
            DisposeQueue(ref _scanCompleteSignals, nameof(_scanCompleteSignals));
            DisposeQueue(ref _blueprintUnlockedSignals, nameof(_blueprintUnlockedSignals));
            DisposeQueue(ref _toolAcousticSignals, nameof(_toolAcousticSignals));
            DisposeQueue(ref _hudNotificationSignals, nameof(_hudNotificationSignals));
            DisposeQueue(ref _reconDataSignals, nameof(_reconDataSignals));
            DisposeQueue(ref _saveLifecycleSignals, nameof(_saveLifecycleSignals));
            DisposeQueue(ref _complianceViolationSignals, nameof(_complianceViolationSignals));
            DisposeQueue(ref _globalTimeSyncSignals, nameof(_globalTimeSyncSignals));
            DisposeQueue(ref _weatherStrengthSignals, nameof(_weatherStrengthSignals));
            DisposeQueue(ref _itemDecaySignals, nameof(_itemDecaySignals));
            DisposeQueue(ref _playerStressSignals, nameof(_playerStressSignals));
            _initialized = false;
        }

        /// <summary>Queues one damage-routing packet from the main thread.</summary>
        public static void Publish(in DamageSignal signal)
        {
            EnsureInitialized();
            _damageSignals.Enqueue(signal);
        }

        /// <summary>Queues one physics impact packet from the main thread.</summary>
        public static void Publish(in ImpactSignal signal)
        {
            EnsureInitialized();
            _impactSignals.Enqueue(signal);
        }

        /// <summary>Queues one AUP shift broadcast packet from the main thread.</summary>
        public static void Publish(in AupPreShiftSignal signal)
        {
            EnsureInitialized();
            _aupPreShiftSignals.Enqueue(signal);
        }

        /// <summary>Queues one AUP shift broadcast packet from the main thread.</summary>
        public static void Publish(in AupShiftSignal signal)
        {
            EnsureInitialized();
            _aupShiftSignals.Enqueue(signal);
        }

        /// <summary>Queues one logistics brownout packet from the main thread.</summary>
        public static void Publish(in BrownoutSignal signal)
        {
            EnsureInitialized();
            _brownoutSignals.Enqueue(signal);
        }

        /// <summary>Queues one ecosystem debris spawn packet from the main thread.</summary>
        public static void Publish(in DebrisSpawnSignal signal)
        {
            EnsureInitialized();
            _debrisSpawnSignals.Enqueue(signal);
        }

        /// <summary>Queues one armor deflection packet from the main thread.</summary>
        public static void Publish(in DeflectSignal signal)
        {
            EnsureInitialized();
            _deflectSignals.Enqueue(signal);
        }

        /// <summary>Queues one entity death packet from the main thread.</summary>
        public static void Publish(in EntityDeathSignal signal)
        {
            EnsureInitialized();
            _entityDeathSignals.Enqueue(signal);
        }

        /// <summary>Queues one narrative solar flare packet from the main thread.</summary>
        public static void Publish(in SolarFlareSignal signal)
        {
            EnsureInitialized();
            _solarFlareSignals.Enqueue(signal);
        }

        /// <summary>Queues one origin rebase packet from the main thread.</summary>
        public static void Publish(in RebaseSignal signal)
        {
            EnsureInitialized();
            _rebaseSignals.Enqueue(signal);
        }

        /// <summary>Queues one input control packet from the main thread.</summary>
        public static void Publish(in ControlSignal signal)
        {
            EnsureInitialized();
            _controlSignals.Enqueue(signal);
        }

        /// <summary>Queues one runtime anomaly packet from the main thread.</summary>
        public static void Publish(in AnomalySignal signal)
        {
            EnsureInitialized();
            _anomalySignals.Enqueue(signal);
        }

        /// <summary>Queues one telemetry anomaly packet from the main thread.</summary>
        public static void Publish(in TelemetryAnomalySignal signal)
        {
            EnsureInitialized();
            _telemetryAnomalySignals.Enqueue(signal);
        }

        /// <summary>Queues one habitat construction packet from the main thread.</summary>
        public static void Publish(in HabitatConstructionSignal signal)
        {
            EnsureInitialized();
            _habitatConstructionSignals.Enqueue(signal);
        }

        /// <summary>Queues one vocal warning packet from the main thread.</summary>
        public static void Publish(in VocalWarningSignal signal)
        {
            EnsureInitialized();
            _vocalWarningSignals.Enqueue(signal);
        }

        /// <summary>Queues one editor data reload packet from the main thread.</summary>
        public static void Publish(in DataReloadSignal signal)
        {
            EnsureInitialized();
            _dataReloadSignals.Enqueue(signal);
        }

        /// <summary>Queues one memory pressure packet from the main thread.</summary>
        public static void Publish(in MemoryPressureSignal signal)
        {
            EnsureInitialized();
            _memoryPressureSignals.Enqueue(signal);
        }

        /// <summary>Queues one acoustic ping packet from the main thread.</summary>
        public static void Publish(in AcousticPingSignal signal)
        {
            EnsureInitialized();
            _acousticPingSignals.Enqueue(signal);
        }

        /// <summary>Queues one movement acoustic packet from the main thread.</summary>
        public static void Publish(in MovementAcousticSignal signal)
        {
            EnsureInitialized();
            _movementAcousticSignals.Enqueue(signal);
        }

        /// <summary>Queues one sonar ping packet from the main thread.</summary>
        public static void Publish(in SonarPingSignal signal)
        {
            EnsureInitialized();
            _sonarPingSignals.Enqueue(signal);
        }

        /// <summary>Queues one hypoxia packet from the main thread.</summary>
        public static void Publish(in HypoxiaSignal signal)
        {
            EnsureInitialized();
            _hypoxiaSignals.Enqueue(signal);
        }

        /// <summary>Queues one oxygen critical packet from the main thread.</summary>
        public static void Publish(in OxygenCriticalSignal signal)
        {
            EnsureInitialized();
            _oxygenCriticalSignals.Enqueue(signal);
        }

        /// <summary>Queues one interaction UI packet from the main thread.</summary>
        public static void Publish(in InteractionUiSignal signal)
        {
            EnsureInitialized();
            _interactionUiSignals.Enqueue(signal);
        }

        /// <summary>Queues one fluid incursion packet from the main thread.</summary>
        public static void Publish(in FluidIncursionSignal signal)
        {
            EnsureInitialized();
            _fluidIncursionSignals.Enqueue(signal);
        }

        /// <summary>Queues one spectrum scan packet from the main thread.</summary>
        public static void Publish(in SpectrumScanSignal signal)
        {
            EnsureInitialized();
            _spectrumScanSignals.Enqueue(signal);
        }

        /// <summary>Queues one rigidbody sleep packet from the main thread.</summary>
        public static void Publish(in RigidbodySleepSignal signal)
        {
            EnsureInitialized();
            _rigidbodySleepSignals.Enqueue(signal);
        }

        /// <summary>Queues one scan-complete packet from the main thread.</summary>
        public static void Publish(in ScanCompleteSignal signal)
        {
            EnsureInitialized();
            _scanCompleteSignals.Enqueue(signal);
        }

        /// <summary>Queues one blueprint-unlocked packet from the main thread.</summary>
        public static void Publish(in BlueprintUnlockedSignal signal)
        {
            EnsureInitialized();
            _blueprintUnlockedSignals.Enqueue(signal);
        }

        /// <summary>Queues one tool acoustic packet from the main thread.</summary>
        public static void Publish(in ToolAcousticSignal signal)
        {
            EnsureInitialized();
            _toolAcousticSignals.Enqueue(signal);
        }

        /// <summary>Queues one HUD notification packet from the main thread.</summary>
        public static void Publish(in HUDNotificationSignal signal)
        {
            EnsureInitialized();
            _hudNotificationSignals.Enqueue(signal);
        }

        /// <summary>Queues one recon data packet from the main thread.</summary>
        public static void Publish(in ReconDataSignal signal)
        {
            EnsureInitialized();
            _reconDataSignals.Enqueue(signal);
        }

        /// <summary>Queues one save lifecycle packet from the main thread.</summary>
        public static void Publish(in SaveLifecycleSignal signal)
        {
            EnsureInitialized();
            _saveLifecycleSignals.Enqueue(signal);
        }

        /// <summary>Queues one compliance violation packet from the main thread.</summary>
        public static void Publish(in ComplianceViolationSignal signal)
        {
            EnsureInitialized();
            _complianceViolationSignals.Enqueue(signal);
        }

        /// <summary>Queues one global time sync packet from the main thread.</summary>
        public static void Publish(in GlobalTimeSyncSignal signal)
        {
            EnsureInitialized();
            _globalTimeSyncSignals.Enqueue(signal);
        }

        /// <summary>Queues one weather strength packet from the main thread.</summary>
        public static void Publish(in WeatherStrengthSignal signal)
        {
            EnsureInitialized();
            _weatherStrengthSignals.Enqueue(signal);
        }

        /// <summary>Queues one item decay packet from the main thread.</summary>
        public static void Publish(in ItemDecaySignal signal)
        {
            EnsureInitialized();
            _itemDecaySignals.Enqueue(signal);
        }

        /// <summary>Queues one player stress packet from the main thread.</summary>
        public static void Publish(in PlayerStressSignal signal)
        {
            EnsureInitialized();
            _playerStressSignals.Enqueue(signal);
        }

        public static bool TryDequeueDamage(out DamageSignal signal) => TryDequeue(ref _damageSignals, out signal);
        public static bool TryDequeueImpact(out ImpactSignal signal) => TryDequeue(ref _impactSignals, out signal);
        public static bool TryDequeueAupPreShift(out AupPreShiftSignal signal) => TryDequeue(ref _aupPreShiftSignals, out signal);
        public static bool TryDequeueAupShift(out AupShiftSignal signal) => TryDequeue(ref _aupShiftSignals, out signal);
        public static bool TryDequeueBrownout(out BrownoutSignal signal) => TryDequeue(ref _brownoutSignals, out signal);
        public static bool TryDequeueDebrisSpawn(out DebrisSpawnSignal signal) => TryDequeue(ref _debrisSpawnSignals, out signal);
        public static bool TryDequeueDeflect(out DeflectSignal signal) => TryDequeue(ref _deflectSignals, out signal);
        public static bool TryDequeueEntityDeath(out EntityDeathSignal signal) => TryDequeue(ref _entityDeathSignals, out signal);
        public static bool TryDequeueSolarFlare(out SolarFlareSignal signal) => TryDequeue(ref _solarFlareSignals, out signal);
        public static bool TryDequeueRebase(out RebaseSignal signal) => TryDequeue(ref _rebaseSignals, out signal);
        public static bool TryDequeueControl(out ControlSignal signal) => TryDequeue(ref _controlSignals, out signal);
        public static bool TryDequeueAnomaly(out AnomalySignal signal) => TryDequeue(ref _anomalySignals, out signal);
        public static bool TryDequeueTelemetryAnomaly(out TelemetryAnomalySignal signal) => TryDequeue(ref _telemetryAnomalySignals, out signal);
        public static bool TryDequeueHabitatConstruction(out HabitatConstructionSignal signal) => TryDequeue(ref _habitatConstructionSignals, out signal);
        public static bool TryDequeueVocalWarning(out VocalWarningSignal signal) => TryDequeue(ref _vocalWarningSignals, out signal);
        public static bool TryDequeueDataReload(out DataReloadSignal signal) => TryDequeue(ref _dataReloadSignals, out signal);
        public static bool TryDequeueMemoryPressure(out MemoryPressureSignal signal) => TryDequeue(ref _memoryPressureSignals, out signal);
        public static bool TryDequeueAcousticPing(out AcousticPingSignal signal) => TryDequeue(ref _acousticPingSignals, out signal);
        public static bool TryDequeueMovementAcoustic(out MovementAcousticSignal signal) => TryDequeue(ref _movementAcousticSignals, out signal);
        public static bool TryDequeueSonarPing(out SonarPingSignal signal) => TryDequeue(ref _sonarPingSignals, out signal);
        public static bool TryDequeueHypoxia(out HypoxiaSignal signal) => TryDequeue(ref _hypoxiaSignals, out signal);
        public static bool TryDequeueOxygenCritical(out OxygenCriticalSignal signal) => TryDequeue(ref _oxygenCriticalSignals, out signal);
        public static bool TryDequeueInteractionUi(out InteractionUiSignal signal) => TryDequeue(ref _interactionUiSignals, out signal);
        public static bool TryDequeueFluidIncursion(out FluidIncursionSignal signal) => TryDequeue(ref _fluidIncursionSignals, out signal);
        public static bool TryDequeueSpectrumScan(out SpectrumScanSignal signal) => TryDequeue(ref _spectrumScanSignals, out signal);
        public static bool TryDequeueRigidbodySleep(out RigidbodySleepSignal signal) => TryDequeue(ref _rigidbodySleepSignals, out signal);
        public static bool TryDequeueScanComplete(out ScanCompleteSignal signal) => TryDequeue(ref _scanCompleteSignals, out signal);
        public static bool TryDequeueBlueprintUnlocked(out BlueprintUnlockedSignal signal) => TryDequeue(ref _blueprintUnlockedSignals, out signal);
        public static bool TryDequeueToolAcoustic(out ToolAcousticSignal signal) => TryDequeue(ref _toolAcousticSignals, out signal);
        public static bool TryDequeueHUDNotification(out HUDNotificationSignal signal) => TryDequeue(ref _hudNotificationSignals, out signal);
        public static bool TryDequeueReconData(out ReconDataSignal signal) => TryDequeue(ref _reconDataSignals, out signal);
        public static bool TryDequeueSaveLifecycle(out SaveLifecycleSignal signal) => TryDequeue(ref _saveLifecycleSignals, out signal);
        public static bool TryDequeueComplianceViolation(out ComplianceViolationSignal signal) => TryDequeue(ref _complianceViolationSignals, out signal);
        public static bool TryDequeueGlobalTimeSync(out GlobalTimeSyncSignal signal) => TryDequeue(ref _globalTimeSyncSignals, out signal);
        public static bool TryDequeueWeatherStrength(out WeatherStrengthSignal signal) => TryDequeue(ref _weatherStrengthSignals, out signal);
        public static bool TryDequeueItemDecay(out ItemDecaySignal signal) => TryDequeue(ref _itemDecaySignals, out signal);
        public static bool TryDequeuePlayerStress(out PlayerStressSignal signal) => TryDequeue(ref _playerStressSignals, out signal);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            DisposeAllQueues();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        private static void RegisterQuitHook()
        {
            Application.quitting -= DisposeAllQueues;
            Application.quitting += DisposeAllQueues;
        }

        private static void EnsureInitialized()
        {
            if (!_initialized)
                InitializeAllQueues();
        }

        private static void CreateQueue<T>(ref NativeQueue<T> queue, int expectedCapacity, string label)
            where T : unmanaged
        {
            if (queue.IsCreated)
                return;

            queue = new NativeQueue<T>(Allocator.Persistent); // COLD ALLOC: NativeQueue<T>[expectedCapacity] - global signal corridor lane - owner: GlobalSignals
            NativeMemorySentinel.RegisterNativeQueue(
                queue,
                expectedCapacity,
                nameof(GlobalSignals),
                label,
                NativeAllocationLifetime.Session);
            PrewarmQueue(ref queue, expectedCapacity);
        }

        private static bool TryDequeue<T>(ref NativeQueue<T> queue, out T signal)
            where T : unmanaged
        {
            if (!queue.IsCreated)
            {
                signal = default;
                return false;
            }

            return queue.TryDequeue(out signal);
        }

        private static void DisposeQueue<T>(ref NativeQueue<T> queue, string label)
            where T : unmanaged
        {
            if (!queue.IsCreated)
                return;

            NativeMemorySentinel.UnregisterNativeQueue(nameof(GlobalSignals), label);
            queue.Dispose();
            queue = default;
        }

        private static void PrewarmQueue<T>(ref NativeQueue<T> queue, int capacity)
            where T : unmanaged
        {
            if (!queue.IsCreated || capacity <= 0)
                return;

            for (int i = 0; i < capacity; i++)
                queue.Enqueue(default);

            while (queue.TryDequeue(out _))
            {
            }
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private static void ValidateSignalPayload<T>(int expectedBytes)
            where T : unmanaged
        {
            if (RuntimeHelpers.IsReferenceOrContainsReferences<T>())
                Debug.LogError("[GlobalSignals] signal managed-reference violation.");

            ValidateSignalSize<T>(expectedBytes);
        }

        private static void ValidateSignalSize<T>(int expectedBytes)
            where T : unmanaged
        {
            int size = UnsafeUtility.SizeOf<T>();
            if (size != expectedBytes)
                Debug.LogError("[GlobalSignals] signal size violation.");
        }
#endif
    }

    /// <summary>Power-of-two single-producer/single-consumer signal fallback using mask wrapping.</summary>
    public struct SpscSignalRingBuffer<T> : IDisposable
        where T : unmanaged
    {
        private NativeArray<T> _buffer;
        private int _mask;
        private int _head;
        private int _tail;

        public SpscSignalRingBuffer(int requestedCapacity, Allocator allocator)
        {
            int capacity = CeilPowerOfTwo(math.max(2, requestedCapacity + 1));
            _buffer = new NativeArray<T>(capacity, allocator, NativeArrayOptions.UninitializedMemory);
            _mask = capacity - 1;
            _head = 0;
            _tail = 0;
        }

        public bool IsCreated => _buffer.IsCreated;
        public int Capacity => _buffer.IsCreated ? _buffer.Length - 1 : 0;

        public void Dispose()
        {
            if (_buffer.IsCreated)
                _buffer.Dispose();

            _buffer = default;
            _mask = 0;
            _head = 0;
            _tail = 0;
        }

        public void Clear()
        {
            Volatile.Write(ref _head, 0);
            Volatile.Write(ref _tail, 0);
        }

        public bool TryEnqueue(in T signal)
        {
            if (!_buffer.IsCreated)
                return false;

            int tail = Volatile.Read(ref _tail);
            int nextTail = (tail + 1) & _mask;
            if (nextTail == Volatile.Read(ref _head))
                return false;

            _buffer[tail] = signal;
            Volatile.Write(ref _tail, nextTail);
            return true;
        }

        public bool TryDequeue(out T signal)
        {
            if (!_buffer.IsCreated)
            {
                signal = default;
                return false;
            }

            int head = Volatile.Read(ref _head);
            if (head == Volatile.Read(ref _tail))
            {
                signal = default;
                return false;
            }

            signal = _buffer[head];
            Volatile.Write(ref _head, (head + 1) & _mask);
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int CeilPowerOfTwo(int value)
        {
            value = math.clamp(value, 2, 1 << 30);
            value--;
            value |= value >> 1;
            value |= value >> 2;
            value |= value >> 4;
            value |= value >> 8;
            value |= value >> 16;
            return value + 1;
        }
    }
}

namespace Hecton8.Core.Signals
{
    /// <summary>Central damage routing signal. Size: 32 bytes.</summary>
    [StructLayout(LayoutKind.Sequential, Pack = 4, Size = 32)]
    public struct DamageSignal
    {
        public float Magnitude;
        public float3 LocalPoint;
        public uint DamageType;
        public uint SubjectHash;
        public ushort SourceId;
        public byte IntegrityDelta;
        public byte Channel;
        public uint TargetId;
    }

    /// <summary>Physics-to-sound impact signal. Size: 64 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct ImpactSignal
    {
        [FieldOffset(0)] public AbsoluteUniversePosition PointAup;
        [FieldOffset(48)] public float Force;
        [FieldOffset(48)] public float Velocity;
        [FieldOffset(52)] public float Intensity;
        [FieldOffset(52)] public float Mass;
        [FieldOffset(56)] public uint PrimaryBodyId;
        [FieldOffset(56)] public uint MaterialHash;
        [FieldOffset(60)] public byte WeightClass;
        [FieldOffset(61)] public byte PrimaryMaterialId;
        [FieldOffset(62)] public byte SecondaryMaterialId;
        [FieldOffset(63)] public byte Flags;
    }

    /// <summary>AUP sector pre-shift warning signal. Size: 32 bytes.</summary>
    [StructLayout(LayoutKind.Sequential, Pack = 4, Size = 32)]
    public struct AupPreShiftSignal
    {
        public float3 ShiftMeters;
        public uint ShiftFrameId;
        public int3 SectorDelta;
        public uint Flags;
    }

    /// <summary>AUP sector shift broadcast signal. Size: 32 bytes.</summary>
    [StructLayout(LayoutKind.Sequential, Pack = 4, Size = 32)]
    public struct AupShiftSignal
    {
        public float3 ShiftMeters;
        public uint ShiftFrameId;
        public int3 SectorDelta;
        public uint Flags;
    }

    /// <summary>Logistics-to-UI brownout signal. Size: 32 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct BrownoutSignal
    {
        [FieldOffset(0)] public uint NetworkId;
        [FieldOffset(4)] public uint NodeId;
        [FieldOffset(8)] public float SupplyRatio;
        [FieldOffset(12)] public float Severity01;
        [FieldOffset(16)] public uint Frame;
        [FieldOffset(20)] public byte Priority;
        [FieldOffset(21)] public byte Flags;
    }

    /// <summary>Ecosystem-to-VFX debris spawn signal. Size: 64 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct DebrisSpawnSignal
    {
        [FieldOffset(0)] public AbsoluteUniversePosition PositionAup;
        [FieldOffset(48)] public uint SpeciesHash;
        [FieldOffset(52)] public uint SourceEntityId;
        [FieldOffset(56)] public float Intensity01;
        [FieldOffset(60)] public byte DebrisKind;
        [FieldOffset(61)] public byte Flags;
    }

    /// <summary>Combat-to-feedback armor deflection signal. Size: 32 bytes.</summary>
    [StructLayout(LayoutKind.Sequential, Pack = 4, Size = 32)]
    public struct DeflectSignal
    {
        public float3 LocalPoint;
        public float FrontDot;
        public uint TargetHash;
        public uint SourceHash;
        public float DamageScalar;
        public byte Flags;
        public byte ArmorClass;
        public ushort Reserved;
    }

    /// <summary>Combat-to-ecosystem death signal. Size: 64 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct EntityDeathSignal
    {
        [FieldOffset(0)] public AbsoluteUniversePosition PositionAup;
        [FieldOffset(48)] public uint EntityHash;
        [FieldOffset(52)] public uint SourceHash;
        [FieldOffset(56)] public float Intensity01;
        [FieldOffset(60)] public byte Flags;
    }

    /// <summary>Narrative-to-celestial solar flare signal. Size: 32 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct SolarFlareSignal
    {
        [FieldOffset(0)] public uint QuestStepHash;
        [FieldOffset(4)] public float Intensity01;
        [FieldOffset(8)] public float DurationSeconds;
        [FieldOffset(12)] public uint Seed;
        [FieldOffset(16)] public byte Flags;
    }

    /// <summary>Origin rebase broadcast signal. Size: 32 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct RebaseSignal
    {
        [FieldOffset(0)] public float3 ShiftMeters;
        [FieldOffset(12)] public uint ShiftFrameId;
        [FieldOffset(16)] public int3 GridDelta;
        [FieldOffset(28)] public uint Flags;
    }

    /// <summary>Input-to-KCC control signal. Size: 32 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct ControlSignal
    {
        [FieldOffset(0)] public uint ControlMask;
        [FieldOffset(4)] public uint Frame;
        [FieldOffset(8)] public float2 Move;
        [FieldOffset(16)] public float2 Look;
        [FieldOffset(24)] public ushort Sequence;
        [FieldOffset(26)] public byte Device;
        [FieldOffset(27)] public byte Flags;
    }

    /// <summary>Runtime anomaly signal for watchdog systems. Size: 32 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct AnomalySignal
    {
        [FieldOffset(0)] public uint SystemHash;
        [FieldOffset(4)] public uint AnomalyHash;
        [FieldOffset(8)] public float Scalar;
        [FieldOffset(12)] public uint Frame;
        [FieldOffset(16)] public byte Severity;
        [FieldOffset(17)] public byte Flags;
    }

    /// <summary>Telemetry anomaly signal. Size: 32 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct TelemetryAnomalySignal
    {
        [FieldOffset(0)] public uint SystemHash;
        [FieldOffset(4)] public uint AnomalyHash;
        [FieldOffset(8)] public float Scalar;
        [FieldOffset(12)] public uint Frame;
        [FieldOffset(16)] public byte Severity;
        [FieldOffset(17)] public byte Flags;
    }

    /// <summary>Habitat construction graph mutation signal. Size: 64 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct HabitatConstructionSignal
    {
        [FieldOffset(0)] public AbsoluteUniversePosition PositionAup;
        [FieldOffset(48)] public uint ModuleHash;
        [FieldOffset(52)] public uint GraphId;
        [FieldOffset(56)] public ushort NodeId;
        [FieldOffset(58)] public byte Operation;
        [FieldOffset(59)] public byte Flags;
    }

    /// <summary>Submarine vocal warning signal. Size: 32 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct VocalWarningSignal
    {
        [FieldOffset(0)] public uint WarningHash;
        [FieldOffset(4)] public uint SourceId;
        [FieldOffset(8)] public float Severity01;
        [FieldOffset(12)] public float CooldownSeconds;
        [FieldOffset(16)] public byte Priority;
        [FieldOffset(17)] public byte Flags;
    }

    /// <summary>Editor data reload signal. Size: 32 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct DataReloadSignal
    {
        [FieldOffset(0)] public uint DataHash;
        [FieldOffset(4)] public uint CategoryHash;
        [FieldOffset(8)] public uint Revision;
        [FieldOffset(12)] public uint Frame;
        [FieldOffset(16)] public byte Flags;
    }

    /// <summary>Memory pressure signal. Size: 32 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct MemoryPressureSignal
    {
        [FieldOffset(0)] public long ReservedMemoryBytes;
        [FieldOffset(8)] public long PhysicalMemoryBytes;
        [FieldOffset(16)] public float UsageRatio;
        [FieldOffset(20)] public uint Frame;
        [FieldOffset(24)] public byte Severity;
        [FieldOffset(25)] public byte Flags;
    }

    /// <summary>Acoustic ping broadcast signal. Size: 64 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct AcousticPingSignal
    {
        [FieldOffset(0)] public AbsoluteUniversePosition PositionAup;
        [FieldOffset(48)] public float RadiusMeters;
        [FieldOffset(52)] public float Intensity01;
        [FieldOffset(56)] public uint SourceId;
        [FieldOffset(60)] public byte Channel;
        [FieldOffset(61)] public byte Flags;
    }

    /// <summary>Player movement acoustic broadcast signal. Size: 64 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct MovementAcousticSignal
    {
        [FieldOffset(0)] public AbsoluteUniversePosition PositionAup;
        [FieldOffset(48)] public float Volume;
        [FieldOffset(52)] public float VelocitySq;
        [FieldOffset(56)] public uint SourceId;
        [FieldOffset(60)] public byte LocomotionMode;
        [FieldOffset(61)] public byte SurfaceMode;
        [FieldOffset(62)] public byte Flags;
    }

    /// <summary>Sonar ping broadcast signal. Size: 64 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct SonarPingSignal
    {
        [FieldOffset(0)] public AbsoluteUniversePosition PositionAup;
        [FieldOffset(48)] public float RadiusMeters;
        [FieldOffset(52)] public float Intensity01;
        [FieldOffset(56)] public uint SourceId;
        [FieldOffset(60)] public byte Flags;
    }

    /// <summary>Hypoxia warning signal. Size: 32 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct HypoxiaSignal
    {
        [FieldOffset(0)] public float Oxygen01;
        [FieldOffset(4)] public float SecondsRemaining;
        [FieldOffset(8)] public uint SourceId;
        [FieldOffset(12)] public uint Frame;
        [FieldOffset(16)] public byte Severity;
        [FieldOffset(17)] public byte Flags;
    }

    /// <summary>Oxygen critical signal. Size: 32 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct OxygenCriticalSignal
    {
        [FieldOffset(0)] public float Oxygen01;
        [FieldOffset(4)] public float SecondsRemaining;
        [FieldOffset(8)] public uint SourceId;
        [FieldOffset(12)] public uint Frame;
        [FieldOffset(16)] public byte Severity;
        [FieldOffset(17)] public byte Flags;
    }

    /// <summary>Interaction UI show/hide signal. Size: 64 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct InteractionUiSignal
    {
        [FieldOffset(0)] public AbsoluteUniversePosition TargetAup;
        [FieldOffset(48)] public uint TargetHash;
        [FieldOffset(52)] public uint ToolHash;
        [FieldOffset(56)] public byte State;
        [FieldOffset(57)] public byte Flags;
    }

    /// <summary>Fluid incursion compartment signal. Size: 64 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct FluidIncursionSignal
    {
        [FieldOffset(0)] public AbsoluteUniversePosition LeakAup;
        [FieldOffset(48)] public uint CompartmentId;
        [FieldOffset(52)] public float FloodLevel01;
        [FieldOffset(56)] public float FlowRate01;
        [FieldOffset(60)] public byte Flags;
    }

    /// <summary>Spectrum scan frequency signal. Size: 32 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct SpectrumScanSignal
    {
        [FieldOffset(0)] public uint ScanId;
        [FieldOffset(4)] public float FrequencyHz;
        [FieldOffset(8)] public float Amplitude01;
        [FieldOffset(12)] public float Noise01;
        [FieldOffset(16)] public byte Band;
        [FieldOffset(17)] public byte Flags;
    }

    /// <summary>Rigidbody sleep-state signal. Size: 64 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct RigidbodySleepSignal
    {
        [FieldOffset(0)] public AbsoluteUniversePosition PositionAup;
        [FieldOffset(48)] public uint BodyId;
        [FieldOffset(52)] public float DistanceMeters;
        [FieldOffset(56)] public byte SleepState;
        [FieldOffset(57)] public byte Flags;
    }

    /// <summary>Scan-complete signal for PDA/lore unlock consumers. Size: 64 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct ScanCompleteSignal
    {
        [FieldOffset(0)] public AbsoluteUniversePosition PositionAup;
        [FieldOffset(48)] public uint EntryHash;
        [FieldOffset(52)] public uint ScanId;
        [FieldOffset(56)] public uint SourceId;
        [FieldOffset(60)] public byte ReconKind;
        [FieldOffset(61)] public byte Flags;
    }

    /// <summary>Blueprint unlock signal for crafting and PDA consumers. Size: 32 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct BlueprintUnlockedSignal
    {
        [FieldOffset(0)] public uint EntityHash;
        [FieldOffset(4)] public uint BlueprintHash;
        [FieldOffset(8)] public uint SourceId;
        [FieldOffset(12)] public uint Frame;
        [FieldOffset(16)] public byte Category;
        [FieldOffset(17)] public byte Flags;
    }

    /// <summary>Tool acoustic state signal. Size: 32 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct ToolAcousticSignal
    {
        [FieldOffset(0)] public uint ToolHash;
        [FieldOffset(4)] public uint TargetHash;
        [FieldOffset(8)] public float Progress01;
        [FieldOffset(12)] public float PitchScale;
        [FieldOffset(16)] public float Intensity01;
        [FieldOffset(20)] public uint Frame;
        [FieldOffset(24)] public byte State;
        [FieldOffset(25)] public byte Flags;
    }

    /// <summary>Hash-only HUD notification signal. Size: 32 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct HUDNotificationSignal
    {
        [FieldOffset(0)] public uint MessageHash;
        [FieldOffset(4)] public uint ContextHash;
        [FieldOffset(8)] public uint SourceId;
        [FieldOffset(12)] public uint Frame;
        [FieldOffset(16)] public byte Severity;
        [FieldOffset(17)] public byte Flags;
    }

    /// <summary>Recon data signal for PDA map population. Size: 64 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct ReconDataSignal
    {
        [FieldOffset(0)] public AbsoluteUniversePosition PositionAup;
        [FieldOffset(48)] public uint EntryHash;
        [FieldOffset(52)] public uint SourceId;
        [FieldOffset(56)] public byte ReconKind;
        [FieldOffset(57)] public byte Flags;
    }

    /// <summary>Save start/end gate signal. Size: 32 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct SaveLifecycleSignal
    {
        [FieldOffset(0)] public uint SlotHash;
        [FieldOffset(4)] public uint OperationId;
        [FieldOffset(8)] public float Progress01;
        [FieldOffset(12)] public uint Frame;
        [FieldOffset(16)] public byte State;
        [FieldOffset(17)] public byte Flags;
    }

    /// <summary>Compliance violation signal. Size: 32 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct ComplianceViolationSignal
    {
        [FieldOffset(0)] public uint RuleHash;
        [FieldOffset(4)] public uint SystemHash;
        [FieldOffset(8)] public uint ContextHash;
        [FieldOffset(12)] public uint Frame;
        [FieldOffset(16)] public byte Severity;
        [FieldOffset(17)] public byte Flags;
    }

    /// <summary>Global time sync signal. Size: 32 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct GlobalTimeSyncSignal
    {
        [FieldOffset(0)] public double WorldSeconds;
        [FieldOffset(8)] public float TimeScale;
        [FieldOffset(12)] public float MoonPhase01;
        [FieldOffset(16)] public uint Sequence;
        [FieldOffset(20)] public byte Flags;
    }

    /// <summary>Weather strength signal. Size: 32 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct WeatherStrengthSignal
    {
        [FieldOffset(0)] public float Strength01;
        [FieldOffset(4)] public float FlowFieldScale;
        [FieldOffset(8)] public uint WeatherHash;
        [FieldOffset(12)] public uint Frame;
        [FieldOffset(16)] public byte Flags;
    }

    /// <summary>Item decay/broken signal. Size: 64 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct ItemDecaySignal
    {
        [FieldOffset(0)] public AbsoluteUniversePosition PositionAup;
        [FieldOffset(48)] public uint ItemHash;
        [FieldOffset(52)] public float Durability01;
        [FieldOffset(56)] public ushort OwnerSlot;
        [FieldOffset(58)] public byte State;
        [FieldOffset(59)] public byte Flags;
    }

    /// <summary>Player stress signal. Size: 32 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct PlayerStressSignal
    {
        [FieldOffset(0)] public float Stress01;
        [FieldOffset(4)] public float OxygenDrainScale;
        [FieldOffset(8)] public float AggressionScale;
        [FieldOffset(12)] public uint Frame;
        [FieldOffset(16)] public byte Cause;
        [FieldOffset(17)] public byte Flags;
    }
}
