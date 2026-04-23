using Hecton8.Core;
using UnityEngine;

namespace Hecton8.AI
{
    public partial class FaunaBrain : IFoveatedSimulationTarget
    {
        private int _foveatedTargetIndex = -1;
        private Transform _foveatedVisualTransform;
        private AudioSource _foveatedAudioSource;

        int IFoveatedSimulationTarget.FoveatedTargetIndex
        {
            get => _foveatedTargetIndex;
            set => _foveatedTargetIndex = value;
        }

        Transform IFoveatedSimulationTarget.SimulationTransform => transform;

        Transform IFoveatedSimulationTarget.VisualTransform => _foveatedVisualTransform;

        AudioSource IFoveatedSimulationTarget.DopplerAudioSource => _foveatedAudioSource;

        bool IFoveatedSimulationTarget.TryBuildDeferredRaycastCommand(out RaycastCommand command)
        {
            command = default;
            return false;
        }

        void IFoveatedSimulationTarget.ConsumeDeferredRaycastHit(in RaycastHit hit)
        {
        }

        private void ResolveFoveatedBindings()
        {
            _foveatedVisualTransform = _renderer != null && _renderer.transform != transform
                ? _renderer.transform
                : null;

            if (!TryGetComponent(out _foveatedAudioSource))
                _foveatedAudioSource = GetComponentInChildren<AudioSource>(true);
        }
    }
}
