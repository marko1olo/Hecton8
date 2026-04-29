using System.Collections.Generic;
using Hecton8.Building;
using Hecton8.Core;
using Hecton8.Gameplay;
using Hecton8.Power;
using Hecton8.World;
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
        private const float IntegritySocketThreshold = 0.5f;
        private const string LeakStripeDecalChildName = "LeakStripeDecal";
        private const string LeakScuffDecalChildName = "LeakScuffDecal";
        private const string LeakWetSheenChildName = "LeakWetSheen";

        private struct RuptureNodeState
        {
            public bool IsRuptured;
            public Vector3 AbsoluteUniversePosition;
            public Matrix4x4 DecalMatrix;
            public int DecalAtlasIndex;
        }

        // COLD ALLOC: Dictionary<UInt32,RuptureNodeState>[64] - last-known rupture state per habitat graph node - owner: BaseDegradationSystem
        private static readonly Dictionary<uint, RuptureNodeState> _ruptureStates = new Dictionary<uint, RuptureNodeState>(64);
        // COLD ALLOC: List<UInt32>[64] - seen-node scratch for one habitat graph synchronization pass - owner: BaseDegradationSystem
        private static readonly List<uint> _seenNodeIds = new List<uint>(64);
        // COLD ALLOC: List<UInt32>[16] - stale-node scratch for rupture-state eviction after graph synchronization - owner: BaseDegradationSystem
        private static readonly List<uint> _staleNodeIds = new List<uint>(16);
        // COLD ALLOC: List<Matrix4x4>[64] - global crack decal matrix cache for downstream decal render owners - owner: BaseDegradationSystem
        private static readonly List<Matrix4x4> _globalCrackDecalMatrices = new List<Matrix4x4>(64);
        // COLD ALLOC: List<Int32>[64] - global crack decal atlas index cache aligned with matrix cache - owner: BaseDegradationSystem
        private static readonly List<int> _globalCrackDecalAtlasIndices = new List<int>(64);
        // COLD ALLOC: Dictionary<Int32,Boolean>[64] - integrity-threshold latch per runtime module instance - owner: BaseDegradationSystem
        private static readonly Dictionary<int, bool> _integritySocketStates = new Dictionary<int, bool>(64);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _ruptureStates.Clear();
            _seenNodeIds.Clear();
            _staleNodeIds.Clear();
            _globalCrackDecalMatrices.Clear();
            _globalCrackDecalAtlasIndices.Clear();
            _integritySocketStates.Clear();
        }

        internal static IReadOnlyList<Matrix4x4> GlobalCrackDecalMatrices => _globalCrackDecalMatrices;
        internal static IReadOnlyList<int> GlobalCrackDecalAtlasIndices => _globalCrackDecalAtlasIndices;

        internal static void BeginRuptureSync()
        {
            _seenNodeIds.Clear();
        }

        internal static void SynchronizeNode(GameObject moduleObject, uint nodeId, LogisticsNodeFlags flags, Vector3 ruptureWorldPosition)
        {
            _seenNodeIds.Add(nodeId);

            bool isRuptured = (flags & LogisticsNodeFlags.Ruptured) != 0;
            bool hadPreviousState = _ruptureStates.TryGetValue(nodeId, out RuptureNodeState previousState);

            if (!isRuptured)
            {
                if (hadPreviousState && previousState.IsRuptured)
                    ConnectionSplineBatchRenderer.SetPipeNodeRuptured(nodeId, false);

                _ruptureStates.Remove(nodeId);
                return;
            }

            Vector3 absoluteUniversePosition = HectonFloatingOrigin.ToAbsoluteUniversePosition(ruptureWorldPosition);
            Matrix4x4 decalMatrix = BuildCrackDecalMatrix(moduleObject, ruptureWorldPosition);

            _ruptureStates[nodeId] = new RuptureNodeState
            {
                IsRuptured = true,
                AbsoluteUniversePosition = absoluteUniversePosition,
                DecalMatrix = decalMatrix,
                DecalAtlasIndex = StructuralIntegrityProfile.DefaultRuptureDecalAtlasIndex
            };

            ConnectionSplineBatchRenderer.SetPipeNodeRuptured(nodeId, true);

            if (!hadPreviousState || !previousState.IsRuptured)
                DispatchRuptureEffects(moduleObject, ruptureWorldPosition, decalMatrix);
        }

        internal static void EndRuptureSync()
        {
            _staleNodeIds.Clear();
            Dictionary<uint, RuptureNodeState>.Enumerator enumerator = _ruptureStates.GetEnumerator();
            while (enumerator.MoveNext())
            {
                if (!_seenNodeIds.Contains(enumerator.Current.Key))
                    _staleNodeIds.Add(enumerator.Current.Key);
            }

            int staleCount = _staleNodeIds.Count;
            for (int i = 0; i < staleCount; i++)
            {
                uint nodeId = _staleNodeIds[i];
                ConnectionSplineBatchRenderer.SetPipeNodeRuptured(nodeId, false);
                _ruptureStates.Remove(nodeId);
            }

            RebuildGlobalDecalBuffer();
        }

        internal static void SynchronizeIntegrityState(BaseModule baseModule)
        {
            if (baseModule == null)
                return;

            int moduleInstanceId = unchecked((int)EntityId.ToULong(baseModule.GetEntityId()));
            bool isBelowThreshold = baseModule.IntegrityStateNormalized < IntegritySocketThreshold;
            bool hadLatchedState = _integritySocketStates.TryGetValue(moduleInstanceId, out bool wasBelowThreshold) && wasBelowThreshold;

            if (!isBelowThreshold)
            {
                _integritySocketStates[moduleInstanceId] = false;
                return;
            }

            if (hadLatchedState)
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

            _integritySocketStates.Remove(unchecked((int)EntityId.ToULong(baseModule.GetEntityId())));
        }

        private static void DispatchRuptureEffects(GameObject moduleObject, Vector3 ruptureWorldPosition, Matrix4x4 decalMatrix)
        {
            if (moduleObject != null && moduleObject.TryGetComponent(out BaseModule baseModule))
            {
                Vector3 localRupturePoint = baseModule.transform.InverseTransformPoint(ruptureWorldPosition);
                baseModule.EmitHullBreachJet(localRupturePoint, DefaultPressureDelta);
            }

            if (AbyssalFluidDecalManager.Instance != null)
                AbyssalFluidDecalManager.Instance.RegisterRuptureFluid(ruptureWorldPosition, DefaultFluidRadiusScale);

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
        }

        private static Matrix4x4 BuildCrackDecalMatrix(GameObject moduleObject, Vector3 ruptureWorldPosition)
        {
            Vector3 outward = moduleObject != null
                ? ruptureWorldPosition - moduleObject.transform.position
                : Vector3.forward;

            if (outward.sqrMagnitude <= 0.0001f)
                outward = moduleObject != null ? moduleObject.transform.forward : Vector3.forward;

            Quaternion rotation = Quaternion.LookRotation(outward.normalized, Vector3.up);
            Vector3 scale = Vector3.one * DefaultDecalScaleMeters;
            return Matrix4x4.TRS(ruptureWorldPosition, rotation, scale);
        }

        private static void ApplyAuthoringDecal(GameObject moduleObject, Vector3 ruptureWorldPosition, Matrix4x4 decalMatrix, string childName)
        {
            if (moduleObject == null)
                return;

            Transform decalTransform = ResolveDecalTransform(moduleObject.transform, childName);
            if (decalTransform == null)
                return;

            Vector4 forwardColumn = decalMatrix.GetColumn(2);
            Vector3 forward = new Vector3(forwardColumn.x, forwardColumn.y, forwardColumn.z);
            if (forward.sqrMagnitude <= 0.0001f)
                forward = moduleObject.transform.forward;

            decalTransform.gameObject.SetActive(true);
            decalTransform.SetPositionAndRotation(
                ruptureWorldPosition,
                Quaternion.LookRotation(forward.normalized, Vector3.up));
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
    }
}
