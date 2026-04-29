using Hecton8.Core;
using Unity.Jobs;
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
        private float _cognitionTimeSeconds;

        int IFoveatedSimulationTarget.FoveatedTargetIndex
        {
            get => _foveatedTargetIndex;
            set => _foveatedTargetIndex = value;
        }

        Transform IFoveatedSimulationTarget.SimulationTransform => transform;

        Transform IFoveatedSimulationTarget.VisualTransform => _foveatedVisualTransform;

        AudioSource IFoveatedSimulationTarget.DopplerAudioSource => _foveatedAudioSource;

        void IFoveatedSimulationTarget.OnFoveatedCadenceResolved(FoveatedTickRate tickRate, float tickIntervalSeconds, float importanceScore, bool insideFrustum)
        {
            _foveatedTickRate = tickRate;
            _foveatedTickIntervalSeconds = tickIntervalSeconds > 0f ? tickIntervalSeconds : (1.0f / 60.0f);
            _foveatedImportanceScore = importanceScore;
            _foveatedInsideFrustum = insideFrustum;
            _sensorSuite.SetFoveatedCadence(_foveatedTickRate, _foveatedTickIntervalSeconds, _foveatedImportanceScore, _foveatedInsideFrustum);
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
    }
}
