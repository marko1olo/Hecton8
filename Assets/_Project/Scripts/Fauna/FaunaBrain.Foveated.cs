using Hecton8.Core;
using Hecton8.Core.Contracts;
using Hecton8.World;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.AI
{
    public partial class FaunaBrain : IFoveatedSimulationTarget
    {
        private int _foveatedTargetIndex = -1;
        private Transform _foveatedVisualTransform;
        private AudioSource _foveatedAudioSource;
        private FoveatedTickRate _foveatedTickRate = FoveatedTickRate.Center60Hz;
        private float _foveatedTickIntervalSeconds = 1.0f / 60.0f;
        private float _foveatedImportanceScore = 1.0f;
        private bool _foveatedInsideFrustum = true;
        private FoveatedSimulationTier _foveatedSimulationTier = FoveatedSimulationTier.Active;
        private float _foveatedDistanceMeters;
        private bool _foveatedTier0Locked;
        private float _cognitionTimeSeconds;

        int IFoveatedSimulationTarget.FoveatedTargetIndex
        {
            get => _foveatedTargetIndex;
            set => _foveatedTargetIndex = value;
        }

        Transform IFoveatedSimulationTarget.SimulationTransform => transform;

        Transform IFoveatedSimulationTarget.VisualTransform => _foveatedVisualTransform;

        AudioSource IFoveatedSimulationTarget.DopplerAudioSource => _foveatedAudioSource;

        uint IFoveatedSimulationTarget.FoveatedEntityHash
        {
            get
            {
                uint entityHash = unchecked((uint)EntityId.ToULong(gameObject.GetEntityId()));
                return entityHash != 0u ? entityHash : ResolveStableFaunaHash(FaunaTickStaggerHashSalt, 0u);
            }
        }

        ushort IFoveatedSimulationTarget.FoveatedEntityId
        {
            get
            {
                uint entityHash = unchecked((uint)EntityId.ToULong(gameObject.GetEntityId()));
                return entityHash >= ushort.MaxValue ? ushort.MaxValue : (ushort)entityHash;
            }
        }

        void IFoveatedSimulationTarget.OnFoveatedCadenceResolved(FoveatedTickRate tickRate, float tickIntervalSeconds, float importanceScore, bool insideFrustum)
        {
            _foveatedTickRate = tickRate;
            _foveatedTickIntervalSeconds = tickIntervalSeconds > 0f ? tickIntervalSeconds : (1.0f / 60.0f);
            _foveatedImportanceScore = importanceScore;
            _foveatedInsideFrustum = insideFrustum;
            _sensorSuite.SetFoveatedCadence(_foveatedTickRate, _foveatedTickIntervalSeconds, _foveatedImportanceScore, _foveatedInsideFrustum);
        }

        void IFoveatedSimulationTarget.OnFoveatedTierResolved(FoveatedSimulationTier tier, float distanceMeters, bool tier0Locked)
        {
            _foveatedSimulationTier = tier;
            _foveatedDistanceMeters = float.IsFinite(distanceMeters) && distanceMeters > 0.0f ? distanceMeters : 0.0f;
            _foveatedTier0Locked = tier0Locked;
        }

        bool IFoveatedSimulationTarget.TryHandleFoveatedFrozenWrap(Vector3 cameraPosition, Vector3 cameraForward, float distanceMeters)
        {
            return TryApplyFoveatedFrozenPredatorWrap(cameraPosition, cameraForward, distanceMeters);
        }

        int IFoveatedSimulationTarget.BuildDeferredRaycastCommands(RaycastCommand[] commands)
        {
            return _sensorSuite.BuildDeferredRaycastCommands(commands);
        }

        void IFoveatedSimulationTarget.ConsumeDeferredRaycastHit(int commandIndex, in RaycastHit hit)
        {
            _sensorSuite.ConsumeDeferredRaycastHit(commandIndex, hit);
        }

        private void ResolveFoveatedBindings()
        {
            _foveatedVisualTransform = _renderer != null && _renderer.transform != transform
                ? _renderer.transform
                : null;

            if (!TryGetComponent(out _foveatedAudioSource))
                _foveatedAudioSource = Hecton8.Core.ComponentReferenceUtility.ResolveOwnedComponent<AudioSource>(transform);
        }

        private void NotifyFoveatedCombatDamageLock()
        {
            IFoveatedSimulationDirector director = GlobalRegistry.FoveatedSimulationDirector;
            if (director == null)
                return;

            director.LockTier0(((IFoveatedSimulationTarget)this).FoveatedEntityHash, ((IFoveatedSimulationTarget)this).FoveatedEntityId, 10.0f);
        }

        private bool TryApplyFoveatedFrozenPredatorWrap(Vector3 cameraPosition, Vector3 cameraForward, float distanceMeters)
        {
            float resolvedDistanceMeters = distanceMeters > 0.0f ? distanceMeters : _foveatedDistanceMeters;
            if (_isDead || !_utilityBrain.IsActivePredator || resolvedDistanceMeters <= 600.0f)
                return false;

            Vector3 safeForward = cameraForward.sqrMagnitude > 0.0001f
                ? ResolveDominantAxisDirection(cameraForward)
                : Vector3.forward;
            Vector3 candidate = cameraPosition + safeForward * 200.0f;
            if (!TryResolveSelfLogicPosition(out Vector3 selfPosition))
                return false;

            candidate.y = selfPosition.y;
            if (!VoxelDynamicNavGridRuntime.TrySampleHybridNavigation((float3)candidate, out VoxelDynamicNavGridRuntime.HybridNavigationSample sample) ||
                sample.Mode == VoxelDynamicNavGridRuntime.HybridNavigationMode.SolidVoxel ||
                sample.Passability == VoxelDynamicNavGridRuntime.SolidCell)
            {
                return false;
            }

            Vector3 preservedVelocity = _rb != null ? _rb.linearVelocity : Vector3.zero;
            Vector3 preservedAngularVelocity = _rb != null ? _rb.angularVelocity : Vector3.zero;
            AbsoluteUniversePosition candidateAup = AbsoluteUniversePosition.FromRuntimePosition(candidate);
            ApplyAupPresentationPosition(in candidateAup);
            if (_rb != null)
            {
                _rb.linearVelocity = preservedVelocity;
                _rb.angularVelocity = preservedAngularVelocity;
            }

            ForceDirectorHuntTarget(cameraPosition, DirectorHuntTargetDurationSeconds);
            return true;
        }
    }
}
