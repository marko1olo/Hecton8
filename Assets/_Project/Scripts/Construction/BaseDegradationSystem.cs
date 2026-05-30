using System.Collections.Generic;
using Hecton8.Building;
using Hecton8.Caves;
using Hecton8.Core;
using Hecton8.Core.Contracts;
using Hecton8.Gameplay;
using Hecton8.Power;
using Hecton8.World;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Construction
{
    /// <summary>
    /// Cold-path degradation bridge for habitat-graph rupture state.
    /// Consumes graph snapshots, propagates pipe-deformation flags, and fans rupture entries into existing module VFX owners.
    /// </summary>
    internal static class BaseDegradationSystem
    {
        private const float DefaultPressureDelta = 4f;
        private const float DefaultFluidRadiusScale = 0.75f;
        private const float IntegritySocketThreshold = 0.5f;
        private const float DefaultInGameDaySeconds = 3600f;
        private const float ParasiteStructuralCollapseDelaySeconds = DefaultInGameDaySeconds * 2f;
        private const float ParasiteCollapseMinimumHorizontalHalfExtent = 2.5f;
        private const float ParasiteCollapseMinimumVerticalHalfExtent = 1.5f;
        private const float ParasiteCollapseMassExtentScale = 0.0125f;
        private const float RupturePositionChangeEpsilonSq = 0.00000001f;
        private const int ParasiteSporeHazardIdSalt = unchecked((int)0x58C20D40);
        private const int RuptureStateCapacity = 64;
        private const int IntegritySocketStateCapacity = 64;
        private const int ModuleRuptureStateCapacity = 64;
        private const int ParasiteSporeHazardCapacity = 32;
        private const int PressureCompressionStateCapacity = 64;
        private const int ParasiteStructuralStateCapacity = 32;

        private struct RuptureNodeState
        {
            public double3 AbsoluteUniversePositionDouble;
            public int ModuleRuntimeId;
            public uint SyncStamp;
            public byte IsRuptured;
            private byte _pad0;
            private byte _pad1;
            private byte _pad2;
        }

        private struct ParasiteSporeHazardState
        {
            public Vector3 Position;
            public float Intensity;
            public float Radius;
        }

        private struct PressureCompressionState
        {
            public Matrix4x4 CompressionMatrix;
            public float VolumeScale;
            public float DepthMeters;
        }

        private struct ParasiteStructuralState
        {
            public float MatureAttachedSeconds;
            public float InfectionLevel;
            public float AddedMassKilograms;
            public byte CollapseDispatched;
        }

        // COLD ALLOC: Dictionary<UInt32,RuptureNodeState>[64] - last-known rupture state per habitat graph node - owner: BaseDegradationSystem
        private static readonly Dictionary<uint, RuptureNodeState> _ruptureStates = new Dictionary<uint, RuptureNodeState>(RuptureStateCapacity);
        // COLD ALLOC: List<UInt32>[64] - stale-node scratch for rupture-state eviction after graph synchronization - owner: BaseDegradationSystem
        private static readonly List<uint> _staleNodeIds = new List<uint>(RuptureStateCapacity);
        // COLD ALLOC: Dictionary<Int32,Boolean>[64] - integrity-threshold latch per runtime module instance - owner: BaseDegradationSystem
        private static readonly Dictionary<int, bool> _integritySocketStates = new Dictionary<int, bool>(IntegritySocketStateCapacity);
        // COLD ALLOC: Dictionary<Int32,Boolean>[64] - rupture-state mirror keyed by runtime module id for fleet arbitration - owner: BaseDegradationSystem
        private static readonly Dictionary<int, bool> _moduleRuptureStates = new Dictionary<int, bool>(ModuleRuptureStateCapacity);
        // COLD ALLOC: Dictionary<Int32,ParasiteSporeHazardState>[32] - active parasite spore room hazards keyed by runtime module id - owner: BaseDegradationSystem
        private static readonly Dictionary<int, ParasiteSporeHazardState> _parasiteSporeHazards = new Dictionary<int, ParasiteSporeHazardState>(ParasiteSporeHazardCapacity);
        // COLD ALLOC: Dictionary<Int32,PressureCompressionState>[64] - pressure-compressed module render state keyed by runtime module id - owner: BaseDegradationSystem
        private static readonly Dictionary<int, PressureCompressionState> _pressureCompressionStates = new Dictionary<int, PressureCompressionState>(PressureCompressionStateCapacity);
        // COLD ALLOC: Dictionary<Int32,ParasiteStructuralState>[32] - mature parasite structural-collapse latch keyed by runtime module id - owner: BaseDegradationSystem
        private static readonly Dictionary<int, ParasiteStructuralState> _parasiteStructuralStates = new Dictionary<int, ParasiteStructuralState>(ParasiteStructuralStateCapacity);
        private static IConstructionParasiteGraphService s_constructionParasiteGraph;
        private static IFluidDecalPresentationSink s_fluidDecals;

        internal static void BindRuntimeServices(
            IConstructionParasiteGraphService constructionParasiteGraph,
            IFluidDecalPresentationSink fluidDecals)
        {
            s_constructionParasiteGraph = constructionParasiteGraph;
            s_fluidDecals = fluidDecals;
        }
        private static uint _ruptureSyncStamp;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _ruptureStates.Clear();
            _staleNodeIds.Clear();
            _integritySocketStates.Clear();
            _moduleRuptureStates.Clear();
            _parasiteSporeHazards.Clear();
            _pressureCompressionStates.Clear();
            _parasiteStructuralStates.Clear();
            _ruptureSyncStamp = 0u;
        }

        internal static void ApplyOriginShift(in OriginShiftEventData shiftData)
        {
            _staleNodeIds.Clear();
        }

        internal static void SynchronizePressureCompression(BaseModule baseModule, Matrix4x4 compressionMatrix, float volumeScale, float depthMeters)
        {
            if (baseModule == null)
                return;

            int moduleRuntimeId = unchecked((int)EntityId.ToULong(baseModule.GetEntityId()));
            if (moduleRuntimeId == 0)
                return;

            float sanitizedVolumeScale = Mathf.Clamp(volumeScale, 0.1f, 1f);
            float sanitizedDepthMeters = Mathf.Max(0f, depthMeters);
            if (sanitizedVolumeScale >= 0.99999f)
            {
                _pressureCompressionStates.Remove(moduleRuntimeId);
                return;
            }

            if (!CanWriteDictionarySlot(_pressureCompressionStates, moduleRuntimeId, PressureCompressionStateCapacity))
                return;

            _pressureCompressionStates[moduleRuntimeId] = new PressureCompressionState
            {
                CompressionMatrix = compressionMatrix,
                VolumeScale = sanitizedVolumeScale,
                DepthMeters = sanitizedDepthMeters
            };
        }

        internal static bool TryGetPressureCompressionState(BaseModule baseModule, out Matrix4x4 compressionMatrix, out float volumeScale, out float depthMeters)
        {
            compressionMatrix = Matrix4x4.identity;
            volumeScale = 1f;
            depthMeters = 0f;
            if (baseModule == null)
                return false;

            int moduleRuntimeId = unchecked((int)EntityId.ToULong(baseModule.GetEntityId()));
            if (moduleRuntimeId == 0 ||
                !_pressureCompressionStates.TryGetValue(moduleRuntimeId, out PressureCompressionState state))
            {
                return false;
            }

            compressionMatrix = state.CompressionMatrix;
            volumeScale = state.VolumeScale;
            depthMeters = state.DepthMeters;
            return volumeScale < 0.99999f;
        }

        internal static void ClearPressureCompressionState(BaseModule baseModule)
        {
            if (baseModule == null)
                return;

            int moduleRuntimeId = unchecked((int)EntityId.ToULong(baseModule.GetEntityId()));
            if (moduleRuntimeId != 0)
                _pressureCompressionStates.Remove(moduleRuntimeId);
        }

        internal static bool IsModuleRuptured(BaseModule baseModule)
        {
            if (baseModule == null)
                return false;

            int moduleRuntimeId = unchecked((int)EntityId.ToULong(baseModule.GetEntityId()));
            return _moduleRuptureStates.TryGetValue(moduleRuntimeId, out bool isRuptured) && isRuptured;
        }

        internal static void SynchronizeParasiteStructuralStress(
            BaseModule baseModule,
            float matureAttachedSeconds,
            float infectionLevel,
            float addedMassKilograms)
        {
            if (baseModule == null)
                return;

            int moduleRuntimeId = unchecked((int)EntityId.ToULong(baseModule.GetEntityId()));
            if (moduleRuntimeId == 0)
                return;

            float sanitizedMatureSeconds = Mathf.Max(0f, matureAttachedSeconds);
            float sanitizedInfection = Mathf.Clamp01(infectionLevel);
            float sanitizedMass = Mathf.Max(0f, addedMassKilograms);
            bool hadState = _parasiteStructuralStates.TryGetValue(moduleRuntimeId, out ParasiteStructuralState state);
            if (!hadState && _parasiteStructuralStates.Count >= ParasiteStructuralStateCapacity)
                return;

            if (sanitizedMatureSeconds <= 0.001f)
            {
                if (hadState && state.CollapseDispatched == 0)
                    _parasiteStructuralStates.Remove(moduleRuntimeId);
                return;
            }

            state.MatureAttachedSeconds = sanitizedMatureSeconds;
            state.InfectionLevel = sanitizedInfection;
            state.AddedMassKilograms = sanitizedMass;
            if (state.CollapseDispatched == 0 &&
                sanitizedMatureSeconds >= ParasiteStructuralCollapseDelaySeconds)
            {
                state.CollapseDispatched = 1;
                DispatchParasiteStructuralCollapse(baseModule, sanitizedInfection, sanitizedMass);
            }

            _parasiteStructuralStates[moduleRuntimeId] = state;
        }

        internal static void ClearParasiteStructuralState(BaseModule baseModule)
        {
            if (baseModule == null)
                return;

            int moduleRuntimeId = unchecked((int)EntityId.ToULong(baseModule.GetEntityId()));
            if (moduleRuntimeId != 0 &&
                _parasiteStructuralStates.TryGetValue(moduleRuntimeId, out ParasiteStructuralState state) &&
                state.CollapseDispatched == 0)
            {
                _parasiteStructuralStates.Remove(moduleRuntimeId);
            }
        }

        internal static bool TryGetParasiteThermalModifier(BaseModule baseModule, out float insulation01, out float bioReactorOverheatMultiplier)
        {
            insulation01 = 0f;
            bioReactorOverheatMultiplier = 1f;
            if (baseModule == null)
                return false;

            insulation01 = Mathf.Clamp01(baseModule.ParasiteThermalInsulation01);
            bioReactorOverheatMultiplier = Mathf.Max(1f, baseModule.ParasiteBioReactorOverheatMultiplier);
            return insulation01 > 0.001f || bioReactorOverheatMultiplier > 1.001f;
        }

        internal static void BeginRuptureSync()
        {
            _ruptureSyncStamp++;
            if (_ruptureSyncStamp == 0u)
                _ruptureSyncStamp = 1u;
        }

        internal static void SynchronizeNode(BaseModule baseModule, uint nodeId, LogisticsNodeFlags flags, Vector3 ruptureWorldPosition)
        {
            bool isRuptured = (flags & LogisticsNodeFlags.Ruptured) != 0;
            bool hadPreviousState = _ruptureStates.TryGetValue(nodeId, out RuptureNodeState previousState);
            int moduleRuntimeId = CaptureModuleRuntimeId(baseModule);

            if (!isRuptured)
            {
                if (hadPreviousState && previousState.IsRuptured != 0)
                    ConnectionSplineBatchRenderer.SetPipeNodeRuptured(nodeId, false);

                if (moduleRuntimeId != 0)
                    TryWriteModuleRuptureState(moduleRuntimeId, false);

                _ruptureStates.Remove(nodeId);
                return;
            }

            if (!hadPreviousState && _ruptureStates.Count >= RuptureStateCapacity)
                return;

            if (!TryResolveAbsoluteFromRuntimeOrigin(ruptureWorldPosition, out double3 absoluteUniversePositionDouble))
                return;

            bool ruptureStateChanged = !hadPreviousState ||
                                       previousState.IsRuptured == 0 ||
                                       previousState.ModuleRuntimeId != moduleRuntimeId ||
                                       !math.all(math.isfinite(previousState.AbsoluteUniversePositionDouble)) ||
                                       !ApproximatelySameDouble3(previousState.AbsoluteUniversePositionDouble, absoluteUniversePositionDouble);

            _ruptureStates[nodeId] = new RuptureNodeState
            {
                AbsoluteUniversePositionDouble = absoluteUniversePositionDouble,
                IsRuptured = 1,
                ModuleRuntimeId = moduleRuntimeId,
                SyncStamp = _ruptureSyncStamp
            };

            if (moduleRuntimeId != 0)
                TryWriteModuleRuptureState(moduleRuntimeId, true);

            ConnectionSplineBatchRenderer.SetPipeNodeRuptured(nodeId, true);

            if (ruptureStateChanged)
                DispatchRuptureEffects(baseModule, ruptureWorldPosition);
        }

        internal static void EndRuptureSync()
        {
            _staleNodeIds.Clear();
            Dictionary<uint, RuptureNodeState>.Enumerator enumerator = _ruptureStates.GetEnumerator();
            while (enumerator.MoveNext())
            {
                if (enumerator.Current.Value.SyncStamp != _ruptureSyncStamp)
                {
                    if (_staleNodeIds.Count < RuptureStateCapacity)
                        _staleNodeIds.Add(enumerator.Current.Key);
                }
            }

            int staleCount = _staleNodeIds.Count;
            for (int i = 0; i < staleCount; i++)
            {
                uint nodeId = _staleNodeIds[i];
                if (_ruptureStates.TryGetValue(nodeId, out RuptureNodeState staleState) && staleState.ModuleRuntimeId != 0)
                    TryWriteModuleRuptureState(staleState.ModuleRuntimeId, false);

                ConnectionSplineBatchRenderer.SetPipeNodeRuptured(nodeId, false);
                _ruptureStates.Remove(nodeId);
            }
        }

        internal static void SynchronizeIntegrityState(BaseModule baseModule)
        {
            if (baseModule == null)
                return;

            int moduleInstanceId = unchecked((int)EntityId.ToULong(baseModule.GetEntityId()));
            bool isBelowThreshold = baseModule.IntegrityStateNormalized < IntegritySocketThreshold ||
                                    baseModule.BulkheadFloodStress01 > 0.001f;
            float parasiteVisual01 = baseModule.AttachedParasiteCount > 0
                ? Mathf.Clamp01(Mathf.Max(0.25f, baseModule.ParasiteInfectionLevel))
                : 0f;
            bool hasParasiteVisual = parasiteVisual01 > 0.001f;
            bool hadLatchedState = _integritySocketStates.TryGetValue(moduleInstanceId, out bool wasBelowThreshold) && wasBelowThreshold;

            if (!isBelowThreshold && !hasParasiteVisual)
            {
                _integritySocketStates.Remove(moduleInstanceId);
                return;
            }

            if (hadLatchedState || !isBelowThreshold)
                return;

            if (!CanWriteDictionarySlot(_integritySocketStates, moduleInstanceId, IntegritySocketStateCapacity))
                return;

            _integritySocketStates[moduleInstanceId] = true;
            if (!baseModule.TryGetDegradationSockets(out BaseModuleTemplate.VfxSocket[] sockets))
                return;

            float integrityState = baseModule.IntegrityStateNormalized;
            for (int i = 0; i < sockets.Length; i++)
            {
                BaseModuleTemplate.VfxSocket socket = sockets[i];
                baseModule.EmitIntegritySocketVfx(socket.LocalPosition, socket.SocketType, integrityState);
            }
        }

        internal static void ClearIntegrityState(BaseModule baseModule)
        {
            if (baseModule == null)
                return;

            int moduleInstanceId = unchecked((int)EntityId.ToULong(baseModule.GetEntityId()));
            _integritySocketStates.Remove(moduleInstanceId);
        }

        internal static void SynchronizeParasiteSporeHazard(BaseModule baseModule)
        {
            if (baseModule == null)
                return;

            int moduleRuntimeId = unchecked((int)EntityId.ToULong(baseModule.GetEntityId()));
            if (moduleRuntimeId == 0)
                return;

            if (!baseModule.TryGetParasiteSporeHazard(out Vector3 position, out float radius, out float intensity))
            {
                ClearParasiteSporeHazard(baseModule);
                return;
            }

            if (!CanWriteDictionarySlot(_parasiteSporeHazards, moduleRuntimeId, ParasiteSporeHazardCapacity))
            {
                baseModule.SetParasiteSporeVfxActive(false);
                return;
            }

            _parasiteSporeHazards[moduleRuntimeId] = new ParasiteSporeHazardState
            {
                Position = position,
                Intensity = Mathf.Clamp01(intensity),
                Radius = Mathf.Max(0.1f, radius)
            };

            baseModule.SetParasiteSporeVfxActive(true);
        }

        internal static void ClearParasiteSporeHazard(BaseModule baseModule)
        {
            if (baseModule == null)
                return;

            int moduleRuntimeId = unchecked((int)EntityId.ToULong(baseModule.GetEntityId()));
            if (moduleRuntimeId == 0)
                return;

            if (_parasiteSporeHazards.ContainsKey(moduleRuntimeId))
                _parasiteSporeHazards.Remove(moduleRuntimeId);

            baseModule.SetParasiteSporeVfxActive(false);
        }

        internal static bool IsModuleParasiteToxic(BaseModule baseModule)
        {
            if (baseModule == null)
                return false;

            int moduleRuntimeId = unchecked((int)EntityId.ToULong(baseModule.GetEntityId()));
            return moduleRuntimeId != 0 &&
                   _parasiteSporeHazards.TryGetValue(moduleRuntimeId, out ParasiteSporeHazardState state) &&
                   state.Intensity > 0.001f;
        }

        internal static bool TryGetParasiteSporeHazard(BaseModule baseModule, out float intensity, out float radius)
        {
            intensity = 0f;
            radius = 0f;
            if (baseModule == null)
                return false;

            int moduleRuntimeId = unchecked((int)EntityId.ToULong(baseModule.GetEntityId()));
            if (moduleRuntimeId == 0 ||
                !_parasiteSporeHazards.TryGetValue(moduleRuntimeId, out ParasiteSporeHazardState state))
            {
                return false;
            }

            intensity = state.Intensity;
            radius = state.Radius;
            return intensity > 0.001f;
        }

        internal static bool TryGetParasiteSporeHazard(BaseModule baseModule, out Vector3 position, out float intensity, out float radius)
        {
            position = Vector3.zero;
            intensity = 0f;
            radius = 0f;
            if (baseModule == null)
                return false;

            int moduleRuntimeId = unchecked((int)EntityId.ToULong(baseModule.GetEntityId()));
            if (moduleRuntimeId == 0 ||
                !_parasiteSporeHazards.TryGetValue(moduleRuntimeId, out ParasiteSporeHazardState state))
            {
                return false;
            }

            position = state.Position;
            intensity = state.Intensity;
            radius = state.Radius;
            return intensity > 0.001f;
        }

        private static void DispatchParasiteStructuralCollapse(BaseModule baseModule, float infectionLevel, float addedMassKilograms)
        {
            if (baseModule == null)
                return;

            Vector3 ruptureWorldPosition = baseModule.transform.position;
            float moduleRadius = 3f;
            if (baseModule.TryGetInteriorHazardBounds(out Vector3 interiorCenter, out float interiorRadius))
            {
                ruptureWorldPosition = interiorCenter;
                moduleRadius = Mathf.Max(moduleRadius, interiorRadius);
            }

            float massExtent = ApproximateSqrtPositive(addedMassKilograms) * ParasiteCollapseMassExtentScale;
            Vector3 halfExtents = new Vector3(
                Mathf.Max(ParasiteCollapseMinimumHorizontalHalfExtent, moduleRadius * 0.45f + massExtent),
                Mathf.Max(ParasiteCollapseMinimumVerticalHalfExtent, 1.25f + infectionLevel * 2f + massExtent),
                Mathf.Max(ParasiteCollapseMinimumHorizontalHalfExtent, moduleRadius * 0.45f + massExtent));
            Vector3 collapseCenter = ruptureWorldPosition + Vector3.down * Mathf.Max(1f, halfExtents.y * 0.75f);
            TryDispatchParasiteCollapseBox(collapseCenter, halfExtents);

            baseModule.SetIntegrityState(BaseModuleIntegrityState.Ruptured);
            FloraInteractionManager floraInteractionManager = FloraInteractionManager.ActiveRuntimeInstance;
            if (floraInteractionManager != null)
                floraInteractionManager.KillAttachedParasites(baseModule);

            int moduleRuntimeId = unchecked((int)EntityId.ToULong(baseModule.GetEntityId()));
            if (moduleRuntimeId != 0)
                TryWriteModuleRuptureState(moduleRuntimeId, true);

            IConstructionParasiteGraphService constructionParasiteGraph = s_constructionParasiteGraph;
            if (constructionParasiteGraph != null)
            {
                constructionParasiteGraph.NotifyModuleParasiteRootStateChanged(baseModule);
            }
            else
            {
                DispatchRuptureEffects(baseModule, ruptureWorldPosition);
            }
        }

        private static bool TryDispatchParasiteCollapseBox(Vector3 runtimeCenter, Vector3 halfExtents)
        {
            HectonVoxelEngine engine = HectonVoxelEngine.ActiveRuntimeInstance;
            if (engine == null)
                return false;

            if (!engine.TryGetNearestActiveVolume(runtimeCenter, out HectonVoxelVolume volume) || volume == null)
                return false;

            return volume.ApplyParasiteCollapseBox(runtimeCenter, halfExtents);
        }

        private static void DispatchRuptureEffects(BaseModule baseModule, Vector3 ruptureWorldPosition)
        {
            if (baseModule != null)
            {
                Vector3 localRupturePoint = baseModule.SetBreachVisualAnchor(ruptureWorldPosition);
                baseModule.EmitHullBreachJet(localRupturePoint, DefaultPressureDelta);
            }

            IFluidDecalPresentationSink fluidDecals = s_fluidDecals;
            if (fluidDecals != null)
                fluidDecals.RegisterRuptureFluid(ruptureWorldPosition, DefaultFluidRadiusScale);
        }

        private static bool ApproximatelySameDouble3(double3 left, double3 right)
        {
            double3 delta = left - right;
            return math.lengthsq(delta) <= RupturePositionChangeEpsilonSq;
        }

        private static bool TryResolveAbsoluteFromRuntimeOrigin(Vector3 runtimePosition, out double3 absolutePosition)
        {
            absolutePosition = default;
            float3 localRuntime = new float3(runtimePosition.x, runtimePosition.y, runtimePosition.z);
            if (!math.all(math.isfinite(localRuntime)))
                return false;

            AbsoluteUniversePosition originAup = RuntimeOriginRoute.CurrentRuntimeOriginAup();
            if (!originAup.IsFinite())
                return false;

            absolutePosition = AbsoluteUniversePosition.OffsetAbsoluteMeters(
                in originAup,
                new double3(localRuntime.x, localRuntime.y, localRuntime.z));
            return math.all(math.isfinite(absolutePosition));
        }

        private static bool CanWriteDictionarySlot<TKey, TValue>(Dictionary<TKey, TValue> dictionary, TKey key, int capacity)
        {
            return dictionary.ContainsKey(key) || dictionary.Count < capacity;
        }

        private static bool TryWriteModuleRuptureState(int moduleRuntimeId, bool isRuptured)
        {
            if (moduleRuntimeId == 0 ||
                !CanWriteDictionarySlot(_moduleRuptureStates, moduleRuntimeId, ModuleRuptureStateCapacity))
            {
                return false;
            }

            _moduleRuptureStates[moduleRuntimeId] = isRuptured;
            return true;
        }

        private static int ComposeParasiteSporeHazardId(int moduleRuntimeId)
        {
            return moduleRuntimeId ^ ParasiteSporeHazardIdSalt;
        }

        private static float ApproximateSqrtPositive(float value)
        {
            float safeValue = math.max(0f, value);
            return safeValue > 0f ? safeValue * math.rsqrt(safeValue) : 0f;
        }

        private static int CaptureModuleRuntimeId(BaseModule baseModule)
        {
            if (baseModule == null)
                return 0;

            return unchecked((int)EntityId.ToULong(baseModule.GetEntityId()));
        }
    }
}
