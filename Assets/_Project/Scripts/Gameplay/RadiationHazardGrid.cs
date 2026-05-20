using System;
using System.IO;
using System.Runtime.InteropServices;
using Hecton8.Core;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Data;
using Hecton8.SaveSystem;
using Hecton8.World;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Gameplay
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/Gameplay/Radiation Hazard Grid")]
    public sealed class RadiationHazardGrid : MonoBehaviour, IFrostTickable, ILateFrameTickable, IOriginShiftListener, ISaveable
    {
        public const int GridResolution = 32;
        public const int GridCellCount = GridResolution * GridResolution * GridResolution;
        public const int MaxSourceCount = 64;
        public const int TelemetryCapacity = 300;
        public const int RlePacketSizeBytes = 5;
        public const int MaxRlePayloadBytes = 81920;

        private const string NativeMemoryOwner = nameof(RadiationHazardGrid);
        private const NativeAllocationLifetime NativeMemoryLifetime = NativeAllocationLifetime.Session;
        private const float DoseDecayPerFrostTick = 0.999f;
        private const float DefaultCellSizeMeters = 4f;
        private const float DefaultSourceRadiusMeters = 18f;
        private const float StaticVfxThreshold = 0.5f;
        private const float IodineDoseReduction = 50f;
        private const uint GeigerSourceId = 0x52414447u;
        private const byte GeigerAcousticChannel = 9;
        private const byte RadiationDoseGridKind = 1;
        private const byte RadiationDoseAtmosphereKind = 2;

        private static readonly uint _iodineItemHash = H8DataHash.ComputeFnv1A32("iodine");
        private static readonly uint _iodineCapsItemHash = H8DataHash.ComputeFnv1A32("Iodine");
        private static readonly int _HazardRadiationLevelId = Shader.PropertyToID("_HazardRadiationLevel");
        private static readonly int _HectonVisualStaticGlitchId = Shader.PropertyToID("_HectonVisualStaticGlitch");
        private static readonly int _HectonVisualStaticGlitchSeedId = Shader.PropertyToID("_HectonVisualStaticGlitchSeed");
        private static readonly int _HectonHandRadiationDoseId = Shader.PropertyToID("_HectonHandRadiationDose");
        private static readonly int _HectonHandRadiationMutationId = Shader.PropertyToID("_HectonHandRadiationMutation01");
        private static readonly int _HectonHandRadiationTintId = Shader.PropertyToID("_HectonHandRadiationTint");
        internal static RadiationHazardGrid ActiveRuntimeInstance { get; private set; }

        [SerializeField, Min(0.5f)] private float cellSizeMeters = DefaultCellSizeMeters;
        [SerializeField, Min(0f)] private float doseScalePerFrostTick = 1f;
        [SerializeField] private bool forceLowTierMathLod;

        private NativeArray<float> _gridRead;
        private NativeArray<float> _gridWrite;
        private NativeArray<float> _gridSource;
        private NativeArray<RadiationSource> _sources;
        private NativeArray<RadiationTelemetryEntry> _telemetryRing;
        private JobHandle _diffusionJobHandle;
        private AbsoluteUniversePosition _gridOriginAup;
        private int _activeSourceCount;
        private int _telemetryWriteIndex;
        private int _sourceVersion;
        private int _gridVersion;
        private uint _geigerLcg = 0xA21F3B5Du;
        private uint _lastShiftSequence;
        private float _accumulatedRadiationDose;
        private float _lastGridIntensity01;
        private float _lastExternalIntensity01;
        private float _geigerPhase;
        private int _lastItemSignalDrainFrame = -1;
        private int _lastSourceSignalDrainFrame = -1;
        private int _lastExternalDoseSignalDrainFrame = -1;
        private bool _hasGridOrigin;
        private bool _diffusionJobActive;
        private bool _registeredFrostTick;
        private bool _registeredLateFrame;
        private bool _registeredOriginShift;
        private bool _registeredSave;

        public int SavePriority => 54;
        public int LoadPriority => 54;

        public static void RegisterSource(int sourceId, Vector3 runtimePosition, float intensity, float radiusMeters)
        {
            if (!Application.isPlaying || sourceId == 0)
                return;

            AbsoluteUniversePosition sourceAup = AbsoluteUniversePosition.FromRuntimePosition(runtimePosition);
            RadiationSourceSignal signal = new RadiationSourceSignal
            {
                PositionAup = sourceAup,
                Intensity = math.max(0f, intensity),
                RadiusMeters = math.max(0.5f, radiusMeters),
                SourceId = sourceId,
                Operation = RadiationSourceSignal.OperationUpsert,
                Flags = 0
            };
            SignalBus<RadiationSourceSignal>.Push(in signal);
        }

        public static void UnregisterSource(int sourceId)
        {
            if (!Application.isPlaying || sourceId == 0)
                return;

            RadiationSourceSignal signal = new RadiationSourceSignal
            {
                SourceId = sourceId,
                Operation = RadiationSourceSignal.OperationRemove
            };
            SignalBus<RadiationSourceSignal>.Push(in signal);
        }

        public static void ReportExternalDose(float dose, float intensity01, Vector3 runtimePosition)
        {
            if (!Application.isPlaying || !(dose > 0f) || !math.isfinite(dose))
                return;

            AbsoluteUniversePosition positionAup = AbsoluteUniversePosition.FromRuntimePosition(runtimePosition);
            RadiationDoseSignal signal = new RadiationDoseSignal
            {
                PositionAup = positionAup,
                Dose = dose,
                Intensity01 = math.saturate(intensity01),
                SourceId = 0u,
                DoseKind = RadiationDoseAtmosphereKind,
                Flags = 0
            };
            GlobalSignals.Publish(in signal);
        }

        internal static bool TrySampleRadiationIntensity01(Vector3 runtimePosition, out float intensity01)
        {
            intensity01 = 0f;
            RadiationHazardGrid grid = ActiveRuntimeInstance;
            if (grid == null)
                return false;

            AbsoluteUniversePosition sampleAup = AbsoluteUniversePosition.FromRuntimePosition(runtimePosition);
            intensity01 = math.max(grid.SampleInverseSquare(in sampleAup), grid.SampleGridNearest(in sampleAup));
            return intensity01 > 0f;
        }

        private void Awake()
        {
            EnsureNativeBuffers();
        }

        private void Start()
        {
            TryRegisterRuntimeLanes();
        }

        private void OnEnable()
        {
            ActiveRuntimeInstance = this;
            EnsureNativeBuffers();
            TryRegisterRuntimeLanes();
        }

        private void OnDisable()
        {
            if (ActiveRuntimeInstance == this)
                ActiveRuntimeInstance = null;

            TryUnregisterRuntimeLanes();
        }

        private void OnDestroy()
        {
            if (ActiveRuntimeInstance == this)
                ActiveRuntimeInstance = null;

            TryUnregisterRuntimeLanes();
            DisposeNativeBuffers();
        }

        public void FrostTick()
        {
            TryRegisterRuntimeLanes();
            EnsureNativeBuffers();
            DrainRadiationSourceSignals();
            DrainExternalDoseSignals();
            DrainItemAcquiredSignals();

            PlayerRuntimeContext playerContext = ResolvePlayerRuntimeContext();
            AbsoluteUniversePosition playerAup = ResolvePlayerAup(playerContext);

            bool lowTier = UsesLowTierMathLod();
            if (lowTier)
            {
                _lastGridIntensity01 = math.max(SampleInverseSquare(in playerAup), _lastExternalIntensity01);
            }
            else
            {
                CompleteDiffusionJobIfReady();
                RebuildSourceGrid();
                _lastGridIntensity01 = math.max(SampleGridNearest(in playerAup), _lastExternalIntensity01);
                ScheduleDiffusionJobIfIdle();
            }

            float doseAdd = math.max(0f, _lastGridIntensity01 * doseScalePerFrostTick);
            _accumulatedRadiationDose = math.max(0f, (_accumulatedRadiationDose + doseAdd) * DoseDecayPerFrostTick);
            ApplyDoseToPlayerContext(playerContext, _accumulatedRadiationDose, _lastGridIntensity01);
            PublishDoseSignal(in playerAup, doseAdd, _lastGridIntensity01, RadiationDoseGridKind);
            EmitGeigerIfNeeded(in playerAup, _lastGridIntensity01);
            PushVisualGlobals(_accumulatedRadiationDose, _lastGridIntensity01);
            RecordTelemetry(playerAup, _lastGridIntensity01, _accumulatedRadiationDose, lowTier ? 1u : 0u);
            _lastExternalIntensity01 *= 0.5f;
        }

        public void LateFrameTick()
        {
            TryRegisterRuntimeLanes();
            CompleteDiffusionJobIfReady();
            DrainRadiationSourceSignals();
            DrainExternalDoseSignals();
            DrainItemAcquiredSignals();
        }

        public void OnOriginShift(in OriginShiftEventData shiftData)
        {
            _lastShiftSequence = shiftData.Sequence;
            RecordTelemetry(_gridOriginAup, _lastGridIntensity01, _accumulatedRadiationDose, 1u << 1);
        }

        public void PopulateSaveData(SaveData data)
        {
            if (data == null)
                return;

            EnsureNativeBuffers();
            CompleteDiffusionJobForReadback();
            data.radiationDose = _accumulatedRadiationDose;
            data.radiationGridCellSizeMeters = math.max(0.5f, cellSizeMeters);
            double3 origin = _hasGridOrigin ? _gridOriginAup.ToAbsoluteDouble3() : double3.zero;
            data.radiationGridOriginX = origin.x;
            data.radiationGridOriginY = origin.y;
            data.radiationGridOriginZ = origin.z;
            EnsureRleSaveBuffer(data);
            data.radiationGridRleLength = EncodeSparseRle(data.radiationGridRle);
        }

        public void LoadFromSaveData(SaveData data)
        {
            EnsureNativeBuffers();
            CompleteDiffusionJobForReadback();
            ClearGrid(_gridRead);
            ClearGrid(_gridWrite);
            ClearGrid(_gridSource);

            if (data == null)
            {
                _accumulatedRadiationDose = 0f;
                return;
            }

            _accumulatedRadiationDose = math.max(0f, data.radiationDose);
            cellSizeMeters = math.max(0.5f, data.radiationGridCellSizeMeters);
            if (math.isfinite(data.radiationGridOriginX) &&
                math.isfinite(data.radiationGridOriginY) &&
                math.isfinite(data.radiationGridOriginZ))
            {
                _gridOriginAup = AbsoluteUniversePosition.FromAbsolutePosition(new double3(
                    data.radiationGridOriginX,
                    data.radiationGridOriginY,
                    data.radiationGridOriginZ));
                _hasGridOrigin = true;
            }

            DecodeSparseRle(data.radiationGridRle, data.radiationGridRleLength);
            ApplyDoseToPlayerContext(ResolvePlayerRuntimeContext(), _accumulatedRadiationDose, _lastGridIntensity01);
        }

        private void RegisterSourceInternal(int sourceId, in AbsoluteUniversePosition sourceAup, float intensity, float radiusMeters)
        {
            EnsureNativeBuffers();
            TryRegisterRuntimeLanes();

            float sourceIntensity01 = NormalizeSourceIntensity(intensity);
            float sourceRadiusMeters = math.max(0.5f, radiusMeters > 0f ? radiusMeters : DefaultSourceRadiusMeters);
            if (!_hasGridOrigin)
            {
                _gridOriginAup = sourceAup;
                _hasGridOrigin = true;
            }

            int freeIndex = -1;
            for (int i = 0; i < MaxSourceCount; i++)
            {
                RadiationSource source = _sources[i];
                if (source.Active == 0)
                {
                    if (freeIndex < 0)
                        freeIndex = i;
                    continue;
                }

                if (source.SourceId != sourceId)
                    continue;

                source.PositionAup = sourceAup;
                source.Intensity01 = sourceIntensity01;
                source.RadiusMeters = sourceRadiusMeters;
                _sources[i] = source;
                _sourceVersion++;
                return;
            }

            if (freeIndex < 0)
                return;

            _sources[freeIndex] = new RadiationSource
            {
                PositionAup = sourceAup,
                Intensity01 = sourceIntensity01,
                RadiusMeters = sourceRadiusMeters,
                SourceId = sourceId,
                Active = 1
            };
            _activeSourceCount++;
            _sourceVersion++;
        }

        private void UnregisterSourceInternal(int sourceId)
        {
            if (!_sources.IsCreated)
                return;

            for (int i = 0; i < MaxSourceCount; i++)
            {
                RadiationSource source = _sources[i];
                if (source.Active == 0 || source.SourceId != sourceId)
                    continue;

                _sources[i] = default;
                _activeSourceCount = math.max(0, _activeSourceCount - 1);
                _sourceVersion++;
                return;
            }
        }

        private void EnsureNativeBuffers()
        {
            if (!_gridRead.IsCreated)
            {
                _gridRead = new NativeArray<float>(GridCellCount, Allocator.Persistent, NativeArrayOptions.ClearMemory);
                NativeMemorySentinel.RegisterNativeArray(_gridRead, NativeMemoryOwner, nameof(_gridRead), NativeMemoryLifetime);
            }

            if (!_gridWrite.IsCreated)
            {
                _gridWrite = new NativeArray<float>(GridCellCount, Allocator.Persistent, NativeArrayOptions.ClearMemory);
                NativeMemorySentinel.RegisterNativeArray(_gridWrite, NativeMemoryOwner, nameof(_gridWrite), NativeMemoryLifetime);
            }

            if (!_gridSource.IsCreated)
            {
                _gridSource = new NativeArray<float>(GridCellCount, Allocator.Persistent, NativeArrayOptions.ClearMemory);
                NativeMemorySentinel.RegisterNativeArray(_gridSource, NativeMemoryOwner, nameof(_gridSource), NativeMemoryLifetime);
            }

            if (!_sources.IsCreated)
            {
                _sources = new NativeArray<RadiationSource>(MaxSourceCount, Allocator.Persistent, NativeArrayOptions.ClearMemory);
                NativeMemorySentinel.RegisterNativeArray(_sources, NativeMemoryOwner, nameof(_sources), NativeMemoryLifetime);
            }

            if (!_telemetryRing.IsCreated)
            {
                _telemetryRing = new NativeArray<RadiationTelemetryEntry>(TelemetryCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
                NativeMemorySentinel.RegisterNativeArray(_telemetryRing, NativeMemoryOwner, nameof(_telemetryRing), NativeMemoryLifetime);
            }
        }

        private void DisposeNativeBuffers()
        {
            if (_diffusionJobActive)
            {
                JobHandle dependency = _diffusionJobHandle;
                DisposeNativeArrayDeferred(ref _gridRead, dependency);
                DisposeNativeArrayDeferred(ref _gridWrite, dependency);
                DisposeNativeArrayDeferred(ref _gridSource, dependency);
                DisposeNativeArray(ref _sources);
                DisposeNativeArray(ref _telemetryRing);
                _diffusionJobHandle = default;
                _diffusionJobActive = false;
                return;
            }

            DisposeNativeArray(ref _gridRead);
            DisposeNativeArray(ref _gridWrite);
            DisposeNativeArray(ref _gridSource);
            DisposeNativeArray(ref _sources);
            DisposeNativeArray(ref _telemetryRing);
        }

        private static void DisposeNativeArray<T>(ref NativeArray<T> array) where T : struct
        {
            if (!array.IsCreated)
                return;

            NativeMemorySentinel.UnregisterNativeArray(array);
            array.Dispose();
            array = default;
        }

        private static void DisposeNativeArrayDeferred<T>(ref NativeArray<T> array, JobHandle dependency) where T : struct
        {
            if (!array.IsCreated)
                return;

            NativeMemorySentinel.UnregisterNativeArray(array);
            array.Dispose(dependency);
            array = default;
        }

        private void TryRegisterRuntimeLanes()
        {
            if (!Application.isPlaying)
                return;

            if (!_registeredFrostTick)
                _registeredFrostTick = GlobalRegistry.TryRegisterFrostTickable(this, PriorityLayer.Environment);

            if (!_registeredLateFrame)
                _registeredLateFrame = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Environment);

            if (!_registeredOriginShift)
            {
                HectonFloatingOrigin.RegisterListener(this);
                _registeredOriginShift = true;
            }

            if (!_registeredSave && GlobalRegistry.Save != null)
            {
                GlobalRegistry.Save.Register(this);
                _registeredSave = true;
            }
        }

        private void TryUnregisterRuntimeLanes()
        {
            if (_registeredFrostTick)
            {
                GlobalRegistry.UnregisterFrostTickable(this, PriorityLayer.Environment);
                _registeredFrostTick = false;
            }

            if (_registeredLateFrame)
            {
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Environment);
                _registeredLateFrame = false;
            }

            if (_registeredOriginShift)
            {
                HectonFloatingOrigin.UnregisterListener(this);
                _registeredOriginShift = false;
            }

            if (_registeredSave && GlobalRegistry.Save != null)
            {
                GlobalRegistry.Save.Unregister(this);
                _registeredSave = false;
            }
        }

        private void RebuildSourceGrid()
        {
            if (!_gridSource.IsCreated || !_gridRead.IsCreated)
                return;

            ClearGrid(_gridSource);
            if (_activeSourceCount <= 0 || !_hasGridOrigin)
                return;

            double3 origin = _gridOriginAup.ToAbsoluteDouble3();
            float safeCellSize = math.max(0.5f, cellSizeMeters);
            int half = GridResolution >> 1;

            for (int sourceIndex = 0; sourceIndex < MaxSourceCount; sourceIndex++)
            {
                RadiationSource source = _sources[sourceIndex];
                if (source.Active == 0 || source.Intensity01 <= 0f)
                    continue;

                double3 sourceAbsolute = source.PositionAup.ToAbsoluteDouble3();
                double3 sourceOffset = sourceAbsolute - origin;
                int centerX = (int)math.floor(sourceOffset.x / safeCellSize) + half;
                int centerY = (int)math.floor(sourceOffset.y / safeCellSize) + half;
                int centerZ = (int)math.floor(sourceOffset.z / safeCellSize) + half;
                int radiusCells = math.max(1, (int)math.ceil(source.RadiusMeters / safeCellSize));
                int minX = math.max(0, centerX - radiusCells);
                int maxX = math.min(GridResolution - 1, centerX + radiusCells);
                int minY = math.max(0, centerY - radiusCells);
                int maxY = math.min(GridResolution - 1, centerY + radiusCells);
                int minZ = math.max(0, centerZ - radiusCells);
                int maxZ = math.min(GridResolution - 1, centerZ + radiusCells);
                float radiusSq = math.max(0.25f, source.RadiusMeters * source.RadiusMeters);

                for (int z = minZ; z <= maxZ; z++)
                {
                    float dz = (z - centerZ) * safeCellSize;
                    for (int y = minY; y <= maxY; y++)
                    {
                        float dy = (y - centerY) * safeCellSize;
                        for (int x = minX; x <= maxX; x++)
                        {
                            float dx = (x - centerX) * safeCellSize;
                            float distanceSq = dx * dx + dy * dy + dz * dz;
                            if (distanceSq > radiusSq)
                                continue;

                            float falloff = 1f - math.saturate(distanceSq / radiusSq);
                            float value = source.Intensity01 * falloff;
                            int cellIndex = Flatten(x, y, z);
                            if (value > _gridSource[cellIndex])
                                _gridSource[cellIndex] = value;
                            if (value > _gridRead[cellIndex])
                                _gridRead[cellIndex] = value;
                        }
                    }
                }
            }
        }

        private void ScheduleDiffusionJobIfIdle()
        {
            if (_diffusionJobActive || !_gridRead.IsCreated || !_gridWrite.IsCreated || !_gridSource.IsCreated)
                return;

            RadiationJacobiDiffusionJob job = new RadiationJacobiDiffusionJob
            {
                Previous = _gridRead,
                Sources = _gridSource,
                Next = _gridWrite,
                Width = GridResolution,
                Height = GridResolution,
                Depth = GridResolution
            };
            _diffusionJobHandle = job.Schedule(GridCellCount, 64);
            _diffusionJobActive = true;
        }

        private void CompleteDiffusionJobIfReady()
        {
            if (!_diffusionJobActive || !_diffusionJobHandle.IsCompleted)
                return;

            if (!DispatcherJobFence.TryFinalizeCompleted(ref _diffusionJobHandle))
                return;

            _diffusionJobActive = false;
            NativeArray<float> previousRead = _gridRead;
            _gridRead = _gridWrite;
            _gridWrite = previousRead;
            _gridVersion++;
        }

        private void CompleteDiffusionJobForReadback()
        {
            if (!_diffusionJobActive)
                return;

            DispatcherJobFence.TryComplete(ref _diffusionJobHandle, forceComplete: true);
            _diffusionJobActive = false;
            NativeArray<float> previousRead = _gridRead;
            _gridRead = _gridWrite;
            _gridWrite = previousRead;
            _gridVersion++;
        }

        private float SampleGridNearest(in AbsoluteUniversePosition sampleAup)
        {
            if (!_gridRead.IsCreated || !_hasGridOrigin)
                return 0f;

            if (!TryResolveGridCell(in sampleAup, out int x, out int y, out int z))
                return 0f;

            return math.saturate(_gridRead[Flatten(x, y, z)]);
        }

        private float SampleInverseSquare(in AbsoluteUniversePosition sampleAup)
        {
            if (!_sources.IsCreated || _activeSourceCount <= 0)
                return 0f;

            float total = 0f;
            for (int i = 0; i < MaxSourceCount; i++)
            {
                RadiationSource source = _sources[i];
                if (source.Active == 0 || source.Intensity01 <= 0f)
                    continue;

                double distanceSq = AbsoluteUniversePosition.DistanceSq(in sampleAup, in source.PositionAup);
                float radiusSq = source.RadiusMeters * source.RadiusMeters;
                float inverseSq = radiusSq * math.rcp((float)math.max(1d, distanceSq));
                total += source.Intensity01 * math.saturate(inverseSq);
            }

            return math.saturate(total);
        }

        private bool TryResolveGridCell(in AbsoluteUniversePosition sampleAup, out int x, out int y, out int z)
        {
            double3 origin = _gridOriginAup.ToAbsoluteDouble3();
            double3 sample = sampleAup.ToAbsoluteDouble3();
            double3 offset = sample - origin;
            float safeCellSize = math.max(0.5f, cellSizeMeters);
            int half = GridResolution >> 1;
            x = (int)math.floor(offset.x / safeCellSize) + half;
            y = (int)math.floor(offset.y / safeCellSize) + half;
            z = (int)math.floor(offset.z / safeCellSize) + half;
            return (uint)x < GridResolution && (uint)y < GridResolution && (uint)z < GridResolution;
        }

        private void DrainItemAcquiredSignals()
        {
            ReadOnlySpan<ItemAcquiredSignal> itemSignals = SignalBus<ItemAcquiredSignal>.GetFrameSnapshot();
            if (itemSignals.Length == 0)
                return;

            int frame = Time.frameCount;
            if (_lastItemSignalDrainFrame == frame)
                return;

            _lastItemSignalDrainFrame = frame;
            for (int i = 0; i < itemSignals.Length; i++)
            {
                ItemAcquiredSignal signal = itemSignals[i];
                if (signal.ItemHash != _iodineItemHash && signal.ItemHash != _iodineCapsItemHash)
                    continue;

                float quantity = math.max(1, signal.Quantity);
                _accumulatedRadiationDose = math.max(0f, _accumulatedRadiationDose - IodineDoseReduction * quantity);
                ApplyDoseToPlayerContext(ResolvePlayerRuntimeContext(), _accumulatedRadiationDose, _lastGridIntensity01);
                PublishDoseSignal(in signal.PositionAup, -IodineDoseReduction * quantity, _lastGridIntensity01, RadiationDoseAtmosphereKind);
            }
        }

        private void DrainRadiationSourceSignals()
        {
            ReadOnlySpan<RadiationSourceSignal> sourceSignals = SignalBus<RadiationSourceSignal>.GetFrameSnapshot();
            if (sourceSignals.Length == 0)
                return;

            int frame = Time.frameCount;
            if (_lastSourceSignalDrainFrame == frame)
                return;

            _lastSourceSignalDrainFrame = frame;
            for (int i = 0; i < sourceSignals.Length; i++)
            {
                RadiationSourceSignal signal = sourceSignals[i];
                if (signal.SourceId == 0)
                    continue;

                if (signal.Operation == RadiationSourceSignal.OperationUpsert)
                    RegisterSourceInternal(signal.SourceId, in signal.PositionAup, signal.Intensity, signal.RadiusMeters);
                else
                    UnregisterSourceInternal(signal.SourceId);
            }
        }

        private void DrainExternalDoseSignals()
        {
            ReadOnlySpan<RadiationDoseSignal> doseSignals = SignalBus<RadiationDoseSignal>.GetFrameSnapshot();
            if (doseSignals.Length == 0)
                return;

            int frame = Time.frameCount;
            if (_lastExternalDoseSignalDrainFrame == frame)
                return;

            _lastExternalDoseSignalDrainFrame = frame;
            for (int i = 0; i < doseSignals.Length; i++)
            {
                RadiationDoseSignal signal = doseSignals[i];
                if (signal.SourceId != 0u || signal.DoseKind != RadiationDoseAtmosphereKind)
                    continue;

                if (!math.isfinite(signal.Dose) || !math.isfinite(signal.Intensity01))
                {
                    DumpBlackBox();
                    continue;
                }

                _accumulatedRadiationDose = math.max(0f, _accumulatedRadiationDose + signal.Dose);
                _lastExternalIntensity01 = math.max(_lastExternalIntensity01, math.saturate(signal.Intensity01));
            }
        }

        private PlayerRuntimeContext ResolvePlayerRuntimeContext()
        {
            return PlayerRuntimeContextService.TryGetActiveRuntimeContext(out PlayerRuntimeContext runtimeContext)
                ? runtimeContext
                : null;
        }

        private static AbsoluteUniversePosition ResolvePlayerAup(PlayerRuntimeContext playerContext)
        {
            if (playerContext != null)
            {
                var playerMovement = playerContext.PlayerMovement;
                if (playerMovement != null)
                {
                    AbsoluteUniversePosition currentAup = playerMovement.CurrentAup;
                    if (MathGuard.IsFinite(in currentAup))
                        return currentAup;
                }

                AbsoluteUniversePosition predictedAup = playerContext.MovementState.PredictedAup;
                if (MathGuard.IsFinite(in predictedAup))
                    return predictedAup;
            }

            return AbsoluteUniversePosition.FromRuntimePosition(Vector3.zero);
        }

        private void ApplyDoseToPlayerContext(PlayerRuntimeContext playerContext, float dose, float intensity01)
        {
            if (!math.isfinite(dose) || !math.isfinite(intensity01))
            {
                DumpBlackBox();
                dose = 0f;
                intensity01 = 0f;
            }

            if (playerContext == null)
                return;

            float safeDose = math.max(0f, dose);
            float penalty01 = math.saturate(1f - HectonPlayerHealth.ResolveRadiationFatigueScale(safeDose));
            playerContext.RadiationDose = safeDose;
            playerContext.RadiationIntensity01 = math.saturate(intensity01);
            playerContext.RadiationMaxHealthPenalty01 = penalty01;
            if (penalty01 > 0.0001f)
                playerContext.SurvivalState.StatusMask |= SurvivalStatusMasks.RadiationPenalty;
            else
                playerContext.SurvivalState.StatusMask &= ~SurvivalStatusMasks.RadiationPenalty;

            if (playerContext.PlayerHealth != null)
                playerContext.PlayerHealth.SetRadiationExposure(safeDose);
        }

        private void PublishDoseSignal(in AbsoluteUniversePosition positionAup, float dose, float intensity01, byte doseKind)
        {
            RadiationDoseSignal signal = new RadiationDoseSignal
            {
                PositionAup = positionAup,
                Dose = dose,
                Intensity01 = math.saturate(intensity01),
                SourceId = GeigerSourceId,
                DoseKind = doseKind,
                Flags = UsesLowTierMathLod() ? (byte)1 : (byte)0
            };
            GlobalSignals.Publish(in signal);
        }

        private void EmitGeigerIfNeeded(in AbsoluteUniversePosition playerAup, float intensity01)
        {
            float safeIntensity = math.saturate(intensity01);
            if (safeIntensity <= 0.001f)
            {
                _geigerPhase = 0f;
                return;
            }

            _geigerPhase += 0.2f + safeIntensity * 5f;
            if (_geigerPhase < 1f)
                return;

            _geigerPhase -= math.floor(_geigerPhase);
            _geigerLcg = unchecked(_geigerLcg * 1664525u + 1013904223u);
            float jitter01 = ((_geigerLcg >> 8) & 0x00FFFFFFu) * (1f / 16777215f);
            if (jitter01 > 0.35f + safeIntensity * 0.60f)
                return;

            AcousticPingSignal signal = new AcousticPingSignal
            {
                PositionAup = playerAup,
                RadiusMeters = 2f,
                Intensity01 = safeIntensity,
                SourceId = GeigerSourceId,
                Channel = GeigerAcousticChannel,
                Flags = 1
            };
            GlobalSignals.Publish(in signal);
        }

        private void PushVisualGlobals(float dose, float intensity01)
        {
            float safeDose = math.max(0f, dose);
            float safeIntensity = math.saturate(intensity01);
            float mutation01 = math.saturate(safeDose * 0.01f);
            float static01 = safeIntensity > StaticVfxThreshold ? safeIntensity : 0f;
            Shader.SetGlobalFloat(_HazardRadiationLevelId, safeIntensity);
            Shader.SetGlobalFloat(_HectonVisualStaticGlitchId, static01);
            Shader.SetGlobalFloat(_HectonVisualStaticGlitchSeedId, (_geigerLcg & 1023u) * (1f / 1023f));
            Shader.SetGlobalFloat(_HectonHandRadiationDoseId, safeDose);
            Shader.SetGlobalFloat(_HectonHandRadiationMutationId, mutation01);
            Shader.SetGlobalVector(_HectonHandRadiationTintId, new Vector4(0.65f, 1f, 0.42f, mutation01));
        }

        private int EncodeSparseRle(byte[] payload)
        {
            if (payload == null || payload.Length < RlePacketSizeBytes || !_gridRead.IsCreated)
                return 0;

            int cursor = 0;
            int cellIndex = 0;
            while (cellIndex < GridCellCount && cursor + RlePacketSizeBytes <= payload.Length)
            {
                byte value = QuantizeCell(_gridRead[cellIndex]);
                if (value == 0)
                {
                    cellIndex++;
                    continue;
                }

                int runStart = cellIndex;
                int runLength = 1;
                cellIndex++;
                while (cellIndex < GridCellCount && runLength < ushort.MaxValue)
                {
                    byte next = QuantizeCell(_gridRead[cellIndex]);
                    if (next != value)
                        break;

                    runLength++;
                    cellIndex++;
                }

                payload[cursor++] = (byte)(runStart & 0xFF);
                payload[cursor++] = (byte)((runStart >> 8) & 0xFF);
                payload[cursor++] = value;
                payload[cursor++] = (byte)(runLength & 0xFF);
                payload[cursor++] = (byte)((runLength >> 8) & 0xFF);
            }

            return cursor;
        }

        private void DecodeSparseRle(byte[] payload, int byteLength)
        {
            if (payload == null || !_gridRead.IsCreated)
                return;

            int safeLength = math.min(math.max(0, byteLength), payload.Length);
            int cursor = 0;
            while (cursor + RlePacketSizeBytes <= safeLength)
            {
                int runStart = payload[cursor] | (payload[cursor + 1] << 8);
                byte quantized = payload[cursor + 2];
                int runLength = payload[cursor + 3] | (payload[cursor + 4] << 8);
                cursor += RlePacketSizeBytes;
                if ((uint)runStart >= GridCellCount || runLength <= 0)
                    continue;

                float value = quantized * (1f / 127f);
                int end = math.min(GridCellCount, runStart + runLength);
                for (int i = runStart; i < end; i++)
                {
                    _gridRead[i] = value;
                    _gridWrite[i] = value;
                }
            }

            _gridVersion++;
        }

        private static void EnsureRleSaveBuffer(SaveData data)
        {
            if (data.radiationGridRle == null || data.radiationGridRle.Length < MaxRlePayloadBytes)
                data.radiationGridRle = new byte[MaxRlePayloadBytes];
        }

        private static byte QuantizeCell(float value)
        {
            if (!math.isfinite(value) || value <= 0f)
                return 0;

            return (byte)math.clamp((int)math.round(math.saturate(value) * 127f), 0, 127);
        }

        private bool UsesLowTierMathLod()
        {
            if (forceLowTierMathLod)
                return true;

            HectonQualityTier tier = GlobalRegistry.ScalabilityTier;
            return tier == HectonQualityTier.Low || tier == HectonQualityTier.Mx350 || tier == HectonQualityTier.Unknown;
        }

        private void RecordTelemetry(in AbsoluteUniversePosition playerAup, float intensity01, float accumulatedRads, uint flags)
        {
            if (!_telemetryRing.IsCreated)
                return;

            float3 runtimePosition = playerAup.ToRuntimeFloat3();
            RadiationTelemetryEntry entry = new RadiationTelemetryEntry
            {
                PlayerRuntimePosition = runtimePosition,
                AccumulatedRads = accumulatedRads,
                GridIntensity01 = intensity01,
                MaxHealthPenalty01 = math.saturate(1f - HectonPlayerHealth.ResolveRadiationFatigueScale(math.max(0f, accumulatedRads))),
                SourceCount = _activeSourceCount,
                SourceVersion = _sourceVersion,
                GridVersion = _gridVersion,
                Frame = Time.frameCount,
                ShiftSequence = _lastShiftSequence,
                Flags = flags
            };
            _telemetryRing[_telemetryWriteIndex % TelemetryCapacity] = entry;
            _telemetryWriteIndex++;
        }

        private void DumpBlackBox()
        {
            if (!_telemetryRing.IsCreated)
                return;

            try
            {
                string path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Docs", "AgentLogs", "Dump_RADIATION_HAZARD_SYS.bin"));
                string directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);

                using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))
                using (BinaryWriter writer = new BinaryWriter(stream))
                {
                    writer.Write(_telemetryWriteIndex);
                    writer.Write(TelemetryCapacity);
                    for (int i = 0; i < TelemetryCapacity; i++)
                    {
                        RadiationTelemetryEntry entry = _telemetryRing[i];
                        writer.Write(entry.PlayerRuntimePosition.x);
                        writer.Write(entry.PlayerRuntimePosition.y);
                        writer.Write(entry.PlayerRuntimePosition.z);
                        writer.Write(entry.AccumulatedRads);
                        writer.Write(entry.GridIntensity01);
                        writer.Write(entry.MaxHealthPenalty01);
                        writer.Write(entry.SourceCount);
                        writer.Write(entry.SourceVersion);
                        writer.Write(entry.GridVersion);
                        writer.Write(entry.Frame);
                        writer.Write(entry.ShiftSequence);
                        writer.Write(entry.Flags);
                    }
                }
            }
            catch (Exception)
            {
            }
        }

        private static void ClearGrid(NativeArray<float> grid)
        {
            if (!grid.IsCreated)
                return;

            for (int i = 0; i < grid.Length; i++)
                grid[i] = 0f;
        }

        private static float NormalizeSourceIntensity(float intensity)
        {
            if (!math.isfinite(intensity) || intensity <= 0f)
                return 0f;

            return math.saturate(intensity > 1f ? intensity * 0.01f : intensity);
        }

        private static int Flatten(int x, int y, int z)
        {
            return x + y * GridResolution + z * GridResolution * GridResolution;
        }

        [StructLayout(LayoutKind.Explicit, Size = 64)]
        private struct RadiationSource
        {
            [FieldOffset(0)] public AbsoluteUniversePosition PositionAup;
            [FieldOffset(48)] public float Intensity01;
            [FieldOffset(52)] public float RadiusMeters;
            [FieldOffset(56)] public int SourceId;
            [FieldOffset(60)] public byte Active;
            [FieldOffset(61)] private byte _pad0;
            [FieldOffset(62)] private byte _pad1;
            [FieldOffset(63)] private byte _pad2;
        }

        [StructLayout(LayoutKind.Explicit, Size = 64)]
        private struct RadiationTelemetryEntry
        {
            [FieldOffset(0)] public float3 PlayerRuntimePosition;
            [FieldOffset(12)] public float AccumulatedRads;
            [FieldOffset(16)] public float GridIntensity01;
            [FieldOffset(20)] public float MaxHealthPenalty01;
            [FieldOffset(24)] public int SourceCount;
            [FieldOffset(28)] public int SourceVersion;
            [FieldOffset(32)] public int GridVersion;
            [FieldOffset(36)] public int Frame;
            [FieldOffset(40)] public uint ShiftSequence;
            [FieldOffset(44)] public uint Flags;
            [FieldOffset(48)] private ulong _pad0;
            [FieldOffset(56)] private ulong _pad1;
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private struct RadiationJacobiDiffusionJob : IJobParallelFor
        {
            [ReadOnly, NoAlias] public NativeArray<float> Previous;
            [ReadOnly, NoAlias] public NativeArray<float> Sources;
            [WriteOnly, NoAlias] public NativeArray<float> Next;
            public int Width;
            public int Height;
            public int Depth;

            public void Execute(int index)
            {
                int plane = Width * Height;
                int z = index / plane;
                int rem = index - z * plane;
                int y = rem / Width;
                int x = rem - y * Width;

                float self = Previous[index];
                float left = Previous[FlattenLocal(math.max(0, x - 1), y, z)];
                float right = Previous[FlattenLocal(math.min(Width - 1, x + 1), y, z)];
                float down = Previous[FlattenLocal(x, math.max(0, y - 1), z)];
                float up = Previous[FlattenLocal(x, math.min(Height - 1, y + 1), z)];
                float back = Previous[FlattenLocal(x, y, math.max(0, z - 1))];
                float forward = Previous[FlattenLocal(x, y, math.min(Depth - 1, z + 1))];
                float next = (self + left + right + down + up + back + forward) * 0.16f;
                next = math.max(next, Sources[index]);
                Next[index] = math.isfinite(next) ? math.saturate(next) : 0f;
            }

            private int FlattenLocal(int x, int y, int z)
            {
                return x + y * Width + z * Width * Height;
            }
        }
    }
}
