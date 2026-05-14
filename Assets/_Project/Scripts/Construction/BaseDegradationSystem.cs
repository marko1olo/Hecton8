using System.Collections.Generic;
using Hecton8.Building;
using Hecton8.Caves;
using Hecton8.Core;
using Hecton8.Gameplay;
using Hecton8.Power;
using Hecton8.World;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Construction
{
    /// <summary>
    /// Cold-path degradation bridge for habitat-graph rupture state.
    /// Consumes graph snapshots, propagates pipe-deformation flags, and fans rupture entries into existing module VFX and decal owners.
    /// </summary>
    internal static class BaseDegradationSystem
    {
        private const float DefaultPressureDelta = 4f;
        private const float DefaultFluidRadiusScale = 0.75f;
        private const float DefaultDecalScaleMeters = 0.72f;
        private const float MaxDecalScaleMultiplier = 3f;
        private const float IntegritySocketThreshold = 0.5f;
        private const float DefaultInGameDaySeconds = 3600f;
        private const float ParasiteStructuralCollapseDelaySeconds = DefaultInGameDaySeconds * 2f;
        private const float ParasiteCollapseMinimumHorizontalHalfExtent = 2.5f;
        private const float ParasiteCollapseMinimumVerticalHalfExtent = 1.5f;
        private const float ParasiteCollapseMassExtentScale = 0.0125f;
        private const float DecalStateChangeEpsilon = 0.0001f;
        private const float DecalStateChangeEpsilonSq = DecalStateChangeEpsilon * DecalStateChangeEpsilon;
        private const int RustDecalAtlasIndex = 1;
        private const int CrackDecalAtlasIndex = 2;
        private const int ParasiteSporeHazardIdSalt = unchecked((int)0x58C20D40);
        private const string LeakStripeDecalChildName = "LeakStripeDecal";
        private const string LeakScuffDecalChildName = "LeakScuffDecal";
        private const string LeakWetSheenChildName = "LeakWetSheen";

        private struct RuptureNodeState
        {
            public bool IsRuptured;
            public int ModuleRuntimeId;
            public uint SyncStamp;
            public Vector3 AbsoluteUniversePosition;
            public double3 AbsoluteUniversePositionDouble;
            public Matrix4x4 DecalMatrix;
            public int DecalAtlasIndex;
        }

        private struct IntegrityDecalState
        {
            public Matrix4x4 DecalMatrix;
            public int DecalAtlasIndex;
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
            public bool CollapseDispatched;
        }

        // COLD ALLOC: Dictionary<UInt32,RuptureNodeState>[64] - last-known rupture state per habitat graph node - owner: BaseDegradationSystem
        private static readonly Dictionary<uint, RuptureNodeState> _ruptureStates = new Dictionary<uint, RuptureNodeState>(64);
        // COLD ALLOC: List<UInt32>[64] - stale-node scratch for rupture-state eviction after graph synchronization - owner: BaseDegradationSystem
        private static readonly List<uint> _staleNodeIds = new List<uint>(64);
        // COLD ALLOC: List<Matrix4x4>[64] - global crack decal matrix cache for downstream decal render owners - owner: BaseDegradationSystem
        private static readonly List<Matrix4x4> _globalCrackDecalMatrices = new List<Matrix4x4>(64);
        // COLD ALLOC: List<Int32>[64] - global crack decal atlas index cache aligned with matrix cache - owner: BaseDegradationSystem
        private static readonly List<int> _globalCrackDecalAtlasIndices = new List<int>(64);
        // COLD ALLOC: Dictionary<Int32,Boolean>[64] - integrity-threshold latch per runtime module instance - owner: BaseDegradationSystem
        private static readonly Dictionary<int, bool> _integritySocketStates = new Dictionary<int, bool>(64);
        // COLD ALLOC: Dictionary<Int32,IntegrityDecalState>[64] - degraded-module deferred decal cache keyed by runtime module id - owner: BaseDegradationSystem
        private static readonly Dictionary<int, IntegrityDecalState> _integrityDecalStates = new Dictionary<int, IntegrityDecalState>(64);
        // COLD ALLOC: List<Int32>[64] - integrity decal keys shifted during AUP rebases - owner: BaseDegradationSystem
        private static readonly List<int> _integrityDecalShiftKeys = new List<int>(64);
        // COLD ALLOC: Dictionary<Int32,Boolean>[64] - rupture-state mirror keyed by runtime module id for fleet arbitration - owner: BaseDegradationSystem
        private static readonly Dictionary<int, bool> _moduleRuptureStates = new Dictionary<int, bool>(64);
        // COLD ALLOC: Dictionary<Int32,ParasiteSporeHazardState>[32] - active parasite spore room hazards keyed by runtime module id - owner: BaseDegradationSystem
        private static readonly Dictionary<int, ParasiteSporeHazardState> _parasiteSporeHazards = new Dictionary<int, ParasiteSporeHazardState>(32);
        // COLD ALLOC: Dictionary<Int32,PressureCompressionState>[64] - pressure-compressed module render state keyed by runtime module id - owner: BaseDegradationSystem
        private static readonly Dictionary<int, PressureCompressionState> _pressureCompressionStates = new Dictionary<int, PressureCompressionState>(64);
        // COLD ALLOC: Dictionary<Int32,ParasiteStructuralState>[32] - mature parasite structural-collapse latch keyed by runtime module id - owner: BaseDegradationSystem
        private static readonly Dictionary<int, ParasiteStructuralState> _parasiteStructuralStates = new Dictionary<int, ParasiteStructuralState>(32);
        private static uint _ruptureSyncStamp;
        private static bool _globalDecalBufferDirty;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _ruptureStates.Clear();
            _staleNodeIds.Clear();
            _globalCrackDecalMatrices.Clear();
            _globalCrackDecalAtlasIndices.Clear();
            _integritySocketStates.Clear();
            _integrityDecalStates.Clear();
            _integrityDecalShiftKeys.Clear();
            _moduleRuptureStates.Clear();
            _parasiteSporeHazards.Clear();
            _pressureCompressionStates.Clear();
            _parasiteStructuralStates.Clear();
            _ruptureSyncStamp = 0u;
            _globalDecalBufferDirty = false;
        }

        internal static IReadOnlyList<Matrix4x4> GlobalCrackDecalMatrices
        {
            get
            {
                RebuildGlobalDecalBufferIfDirty();
                return _globalCrackDecalMatrices;
            }
        }

        internal static void ApplyOriginShift(in OriginShiftEventData shiftData)
        {
            Vector3 runtimeOffset = -shiftData.ShiftOffset;
            if (!IsFiniteVector(runtimeOffset) || runtimeOffset.sqrMagnitude <= 0.000001f)
                return;

            _staleNodeIds.Clear();
            Dictionary<uint, RuptureNodeState>.Enumerator ruptureEnumerator = _ruptureStates.GetEnumerator();
            while (ruptureEnumerator.MoveNext())
                _staleNodeIds.Add(ruptureEnumerator.Current.Key);

            int ruptureCount = _staleNodeIds.Count;
            for (int keyIndex = 0; keyIndex < ruptureCount; keyIndex++)
            {
                uint nodeId = _staleNodeIds[keyIndex];
                if (!_ruptureStates.TryGetValue(nodeId, out RuptureNodeState state))
                    continue;

                state.DecalMatrix = RebaseDecalMatrix(state.DecalMatrix, runtimeOffset);
                _ruptureStates[nodeId] = state;
            }

            _integrityDecalShiftKeys.Clear();
            Dictionary<int, IntegrityDecalState>.Enumerator integrityEnumerator = _integrityDecalStates.GetEnumerator();
            while (integrityEnumerator.MoveNext())
                _integrityDecalShiftKeys.Add(integrityEnumerator.Current.Key);

            int integrityCount = _integrityDecalShiftKeys.Count;
            for (int keyIndex = 0; keyIndex < integrityCount; keyIndex++)
            {
                int moduleRuntimeId = _integrityDecalShiftKeys[keyIndex];
                if (!_integrityDecalStates.TryGetValue(moduleRuntimeId, out IntegrityDecalState state))
                    continue;

                state.DecalMatrix = RebaseDecalMatrix(state.DecalMatrix, runtimeOffset);
                _integrityDecalStates[moduleRuntimeId] = state;
            }

            _staleNodeIds.Clear();
            _integrityDecalShiftKeys.Clear();
            MarkGlobalDecalBufferDirty();
        }

        internal static IReadOnlyList<int> GlobalCrackDecalAtlasIndices
        {
            get
            {
                RebuildGlobalDecalBufferIfDirty();
                return _globalCrackDecalAtlasIndices;
            }
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
            if (sanitizedMatureSeconds <= 0.001f)
            {
                if (hadState && !state.CollapseDispatched)
                    _parasiteStructuralStates.Remove(moduleRuntimeId);
                return;
            }

            state.MatureAttachedSeconds = sanitizedMatureSeconds;
            state.InfectionLevel = sanitizedInfection;
            state.AddedMassKilograms = sanitizedMass;
            if (!state.CollapseDispatched &&
                sanitizedMatureSeconds >= ParasiteStructuralCollapseDelaySeconds)
            {
                state.CollapseDispatched = true;
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
                !state.CollapseDispatched)
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

        internal static void SynchronizeNode(GameObject moduleObject, uint nodeId, LogisticsNodeFlags flags, Vector3 ruptureWorldPosition)
        {
            bool isRuptured = (flags & LogisticsNodeFlags.Ruptured) != 0;
            bool hadPreviousState = _ruptureStates.TryGetValue(nodeId, out RuptureNodeState previousState);
            int moduleRuntimeId = ResolveModuleRuntimeId(moduleObject);

            if (!isRuptured)
            {
                if (hadPreviousState && previousState.IsRuptured)
                    ConnectionSplineBatchRenderer.SetPipeNodeRuptured(nodeId, false);

                if (moduleRuntimeId != 0)
                    _moduleRuptureStates[moduleRuntimeId] = false;

                if (_ruptureStates.Remove(nodeId))
                    MarkGlobalDecalBufferDirty();
                return;
            }

            double3 absoluteUniversePositionDouble = HectonFloatingOrigin.ToAbsoluteUniversePositionDouble3(ruptureWorldPosition);
            Vector3 absoluteUniversePosition = ToVector3(absoluteUniversePositionDouble);
            Matrix4x4 decalMatrix = BuildCrackDecalMatrix(moduleObject, ruptureWorldPosition);
            int decalAtlasIndex = StructuralIntegrityProfile.DefaultRuptureDecalAtlasIndex;
            bool decalStateChanged = !hadPreviousState ||
                                     !previousState.IsRuptured ||
                                     previousState.ModuleRuntimeId != moduleRuntimeId ||
                                     previousState.DecalAtlasIndex != decalAtlasIndex ||
                                     !ApproximatelySameDouble3(
                                         ResolveRuptureAbsolutePositionDouble(in previousState),
                                         absoluteUniversePositionDouble) ||
                                     !ApproximatelySameMatrix(previousState.DecalMatrix, decalMatrix);

            _ruptureStates[nodeId] = new RuptureNodeState
            {
                IsRuptured = true,
                ModuleRuntimeId = moduleRuntimeId,
                SyncStamp = _ruptureSyncStamp,
                AbsoluteUniversePosition = absoluteUniversePosition,
                AbsoluteUniversePositionDouble = absoluteUniversePositionDouble,
                DecalMatrix = decalMatrix,
                DecalAtlasIndex = decalAtlasIndex
            };

            if (moduleRuntimeId != 0)
                _moduleRuptureStates[moduleRuntimeId] = true;

            ConnectionSplineBatchRenderer.SetPipeNodeRuptured(nodeId, true);
            if (decalStateChanged)
                MarkGlobalDecalBufferDirty();

            if (!hadPreviousState || !previousState.IsRuptured)
                DispatchRuptureEffects(moduleObject, ruptureWorldPosition, decalMatrix);
        }

        internal static void EndRuptureSync()
        {
            _staleNodeIds.Clear();
            Dictionary<uint, RuptureNodeState>.Enumerator enumerator = _ruptureStates.GetEnumerator();
            while (enumerator.MoveNext())
            {
                if (enumerator.Current.Value.SyncStamp != _ruptureSyncStamp)
                    _staleNodeIds.Add(enumerator.Current.Key);
            }

            int staleCount = _staleNodeIds.Count;
            for (int i = 0; i < staleCount; i++)
            {
                uint nodeId = _staleNodeIds[i];
                if (_ruptureStates.TryGetValue(nodeId, out RuptureNodeState staleState) && staleState.ModuleRuntimeId != 0)
                    _moduleRuptureStates[staleState.ModuleRuntimeId] = false;

                ConnectionSplineBatchRenderer.SetPipeNodeRuptured(nodeId, false);
                if (_ruptureStates.Remove(nodeId))
                    MarkGlobalDecalBufferDirty();
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
                if (_integrityDecalStates.Remove(moduleInstanceId))
                    MarkGlobalDecalBufferDirty();
                return;
            }

            int decalAtlasIndex = hasParasiteVisual
                ? RustDecalAtlasIndex
                : ResolveIntegrityDecalAtlasIndex(
                    baseModule.IntegrityStateNormalized,
                    baseModule.BulkheadFloodStress01);
            Matrix4x4 decalMatrix = BuildIntegrityDecalMatrix(baseModule, parasiteVisual01);
            IntegrityDecalState nextDecalState = new IntegrityDecalState
            {
                DecalMatrix = decalMatrix,
                DecalAtlasIndex = decalAtlasIndex
            };
            bool hadPreviousDecalState = _integrityDecalStates.TryGetValue(moduleInstanceId, out IntegrityDecalState previousDecalState);
            _integrityDecalStates[moduleInstanceId] = nextDecalState;
            if (!hadPreviousDecalState ||
                previousDecalState.DecalAtlasIndex != decalAtlasIndex ||
                !ApproximatelySameMatrix(previousDecalState.DecalMatrix, decalMatrix))
            {
                MarkGlobalDecalBufferDirty();
            }

            if (hadLatchedState || !isBelowThreshold)
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
            if (_integrityDecalStates.Remove(moduleInstanceId))
                MarkGlobalDecalBufferDirty();
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
                _moduleRuptureStates[moduleRuntimeId] = true;

            ConstructionManager constructionManager = GlobalRegistry.ConstructionRuntime;
            if (constructionManager != null)
            {
                constructionManager.NotifyModuleParasiteRootStateChanged(baseModule);
            }
            else
            {
                Matrix4x4 decalMatrix = BuildCrackDecalMatrix(baseModule.gameObject, ruptureWorldPosition);
                DispatchRuptureEffects(baseModule.gameObject, ruptureWorldPosition, decalMatrix);
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

        private static void DispatchRuptureEffects(GameObject moduleObject, Vector3 ruptureWorldPosition, Matrix4x4 decalMatrix)
        {
            if (moduleObject != null && moduleObject.TryGetComponent(out BaseModule baseModule))
            {
                Vector3 localRupturePoint = baseModule.SetBreachVisualAnchor(ruptureWorldPosition);
                baseModule.EmitHullBreachJet(localRupturePoint, DefaultPressureDelta);
            }

            AbyssalFluidDecalManager fluidDecals = Hecton8.Core.GlobalRegistry.AbyssalFluidDecals;
            if (fluidDecals != null)
                fluidDecals.RegisterRuptureFluid(ruptureWorldPosition, DefaultFluidRadiusScale);

            ApplyAuthoringDecal(moduleObject, ruptureWorldPosition, decalMatrix, LeakStripeDecalChildName);
            ApplyAuthoringDecal(moduleObject, ruptureWorldPosition, decalMatrix, LeakScuffDecalChildName);
            ApplyAuthoringDecal(moduleObject, ruptureWorldPosition, decalMatrix, LeakWetSheenChildName);
        }

        private static void RebuildGlobalDecalBuffer()
        {
            _globalCrackDecalMatrices.Clear();
            _globalCrackDecalAtlasIndices.Clear();

            Dictionary<uint, RuptureNodeState>.Enumerator enumerator = _ruptureStates.GetEnumerator();
            while (enumerator.MoveNext())
            {
                RuptureNodeState state = enumerator.Current.Value;
                if (!state.IsRuptured)
                    continue;

                _globalCrackDecalMatrices.Add(state.DecalMatrix);
                _globalCrackDecalAtlasIndices.Add(state.DecalAtlasIndex);
            }

            Dictionary<int, IntegrityDecalState>.Enumerator integrityEnumerator = _integrityDecalStates.GetEnumerator();
            while (integrityEnumerator.MoveNext())
            {
                IntegrityDecalState state = integrityEnumerator.Current.Value;
                _globalCrackDecalMatrices.Add(state.DecalMatrix);
                _globalCrackDecalAtlasIndices.Add(state.DecalAtlasIndex);
            }
        }

        private static void MarkGlobalDecalBufferDirty()
        {
            _globalDecalBufferDirty = true;
        }

        private static void RebuildGlobalDecalBufferIfDirty()
        {
            if (!_globalDecalBufferDirty)
                return;

            _globalDecalBufferDirty = false;
            RebuildGlobalDecalBuffer();
        }

        private static bool ApproximatelySameVector(Vector3 left, Vector3 right)
        {
            return (left - right).sqrMagnitude <= DecalStateChangeEpsilonSq;
        }

        private static bool ApproximatelySameDouble3(double3 left, double3 right)
        {
            double3 delta = left - right;
            return math.lengthsq(delta) <= DecalStateChangeEpsilonSq;
        }

        private static double3 ResolveRuptureAbsolutePositionDouble(in RuptureNodeState state)
        {
            if (math.all(math.isfinite(state.AbsoluteUniversePositionDouble)) &&
                (math.any(state.AbsoluteUniversePositionDouble != double3.zero) ||
                 state.AbsoluteUniversePosition == Vector3.zero))
                return state.AbsoluteUniversePositionDouble;

            return ToDouble3(state.AbsoluteUniversePosition);
        }

        private static bool ApproximatelySameMatrix(Matrix4x4 left, Matrix4x4 right)
        {
            for (int elementIndex = 0; elementIndex < 16; elementIndex++)
            {
                if (Mathf.Abs(left[elementIndex] - right[elementIndex]) > DecalStateChangeEpsilon)
                    return false;
            }

            return true;
        }

        private static bool IsFiniteVector(Vector3 value)
        {
            return math.isfinite(value.x) &&
                   math.isfinite(value.y) &&
                   math.isfinite(value.z);
        }

        private static Vector3 ToVector3(double3 value)
        {
            return new Vector3((float)value.x, (float)value.y, (float)value.z);
        }

        private static double3 ToDouble3(Vector3 value)
        {
            return new double3(value.x, value.y, value.z);
        }

        private static Matrix4x4 BuildCrackDecalMatrix(GameObject moduleObject, Vector3 ruptureWorldPosition)
        {
            Vector3 outward = Vector3.forward;
            Vector3 fallbackForward = Vector3.forward;
            if (moduleObject != null)
            {
                Transform moduleTransform = moduleObject.transform;
                Matrix4x4 localToWorld = moduleTransform.localToWorldMatrix;
                Vector3 moduleWorldPosition = localToWorld.GetColumn(3);
                fallbackForward = localToWorld.GetColumn(2);
                double3 ruptureAup = HectonFloatingOrigin.ToAbsoluteUniversePositionDouble3(ruptureWorldPosition);
                double3 moduleAup = HectonFloatingOrigin.ToAbsoluteUniversePositionDouble3(moduleWorldPosition);
                outward = ToVector3(ruptureAup - moduleAup);
            }

            if (outward.sqrMagnitude <= 0.0001f)
                outward = fallbackForward;

            Quaternion rotation = Quaternion.LookRotation(FastDirectionOrFallback(outward, fallbackForward), Vector3.up);
            Vector3 scale = Vector3.one * DefaultDecalScaleMeters;
            return Matrix4x4.TRS(ruptureWorldPosition, rotation, scale);
        }

        private static Matrix4x4 RebaseDecalMatrix(Matrix4x4 matrix, Vector3 runtimeOffset)
        {
            Vector4 position = matrix.GetColumn(3);
            position.x += runtimeOffset.x;
            position.y += runtimeOffset.y;
            position.z += runtimeOffset.z;
            matrix.SetColumn(3, position);
            return matrix;
        }

        private static Matrix4x4 BuildIntegrityDecalMatrix(BaseModule baseModule, float parasiteVisual01)
        {
            Transform moduleTransform = baseModule.transform;
            Matrix4x4 localToWorld = moduleTransform.localToWorldMatrix;
            Vector3 moduleWorldPosition = localToWorld.GetColumn(3);
            Vector3 worldPosition = moduleWorldPosition;
            Vector3 forward = localToWorld.GetColumn(2);

            if (baseModule.TryGetDegradationSockets(out BaseModuleTemplate.VfxSocket[] sockets) && sockets.Length > 0)
            {
                BaseModuleTemplate.VfxSocket socket = sockets[0];
                worldPosition = localToWorld.MultiplyPoint3x4(new Vector3(socket.LocalPosition.x, socket.LocalPosition.y, socket.LocalPosition.z));
                Vector3 outward = worldPosition - moduleWorldPosition;
                if (outward.sqrMagnitude > 0.0001f)
                    forward = FastDirectionOrFallback(outward, forward);
            }

            float damage01 = Mathf.Clamp01(1f - baseModule.IntegrityStateNormalized);
            float stress01 = Mathf.Clamp01(baseModule.BulkheadFloodStress01);
            float scaleMultiplier = Mathf.Min(
                MaxDecalScaleMultiplier,
                1f + (damage01 * 1.2f) + (stress01 * 1.8f) + (Mathf.Clamp01(parasiteVisual01) * 1.4f));
            float scaleMeters = DefaultDecalScaleMeters * scaleMultiplier;
            return Matrix4x4.TRS(worldPosition, Quaternion.LookRotation(forward, Vector3.up), Vector3.one * scaleMeters);
        }

        private static int ResolveIntegrityDecalAtlasIndex(float integrityStateNormalized, float bulkheadStress01)
        {
            return integrityStateNormalized < 0.25f || bulkheadStress01 >= 0.6f
                ? CrackDecalAtlasIndex
                : RustDecalAtlasIndex;
        }

        private static void ApplyAuthoringDecal(GameObject moduleObject, Vector3 ruptureWorldPosition, Matrix4x4 decalMatrix, string childName)
        {
            if (moduleObject == null)
                return;

            Transform moduleTransform = moduleObject.transform;
            Transform decalTransform = ResolveDecalTransform(moduleTransform, childName);
            if (decalTransform == null)
                return;

            Vector4 forwardColumn = decalMatrix.GetColumn(2);
            Vector3 forward = new Vector3(forwardColumn.x, forwardColumn.y, forwardColumn.z);
            if (forward.sqrMagnitude <= 0.0001f)
                forward = moduleTransform.forward;

            decalTransform.gameObject.SetActive(true);
            decalTransform.SetPositionAndRotation(
                ruptureWorldPosition,
                Quaternion.LookRotation(FastDirectionOrFallback(forward, moduleTransform.forward), Vector3.up));
            decalTransform.localScale = Vector3.one * DefaultDecalScaleMeters;

            if (decalTransform.TryGetComponent(out Renderer renderer))
                renderer.enabled = true;
        }

        private static Transform ResolveDecalTransform(Transform root, string childName)
        {
            if (root == null)
                return null;

            Transform decalTransform = root.Find(childName);
            if (decalTransform != null)
                return decalTransform;

            Transform lod0Transform = root.Find("LOD0");
            if (lod0Transform == null)
                return null;

            return lod0Transform.Find(childName);
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

        private static Vector3 FastDirectionOrFallback(Vector3 value, Vector3 fallback)
        {
            float lengthSq = value.sqrMagnitude;
            if (lengthSq <= 0.000001f || float.IsNaN(lengthSq) || float.IsInfinity(lengthSq))
                return fallback.sqrMagnitude > 0.000001f ? fallback : Vector3.forward;

            float invLength = math.rsqrt(lengthSq);
            return new Vector3(value.x * invLength, value.y * invLength, value.z * invLength);
        }

        private static int ResolveModuleRuntimeId(GameObject moduleObject)
        {
            if (moduleObject == null || !moduleObject.TryGetComponent(out BaseModule baseModule))
                return 0;

            return unchecked((int)EntityId.ToULong(baseModule.GetEntityId()));
        }
    }
}
