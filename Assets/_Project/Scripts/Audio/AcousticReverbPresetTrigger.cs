using Hecton8.Core;
using Hecton8.World;
using UnityEngine;
using UnityEngine.Audio;

namespace Hecton8.Audio
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BoxCollider))]
    public sealed class AcousticReverbPresetTrigger : MonoBehaviour
    {
        public enum ReverbPreset : byte
        {
            SmallRoom = 0,
            LargeRoom = 1
        }

        [SerializeField] private ReverbPreset preset = ReverbPreset.SmallRoom;
        [SerializeField] private AudioMixerSnapshot smallRoomSnapshot;
        [SerializeField] private AudioMixerSnapshot largeRoomSnapshot;
        [SerializeField, Min(0f)] private float transitionSeconds = 0.08f;
        [SerializeField] private LayerMask playerLayerMask = HectonLayerMasks.PlayerLayerMask;

        private int _insideCount;

        private void Reset()
        {
            ForceTriggerCollider();
        }

        private void OnValidate()
        {
            ForceTriggerCollider();
        }

        private void OnDisable()
        {
            _insideCount = 0;
            AcousticOcclusionUtility.ClearTriggerReverbPreset();
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!IsPlayerCollider(other))
                return;

            _insideCount++;
            if (_insideCount == 1)
                ApplyPreset();
        }

        private void OnTriggerExit(Collider other)
        {
            if (!IsPlayerCollider(other))
                return;

            _insideCount = Mathf.Max(0, _insideCount - 1);
            if (_insideCount == 0)
                AcousticOcclusionUtility.ClearTriggerReverbPreset();
        }

        private void ApplyPreset()
        {
            AcousticOcclusionUtility.SetTriggerReverbPreset(
                preset == ReverbPreset.LargeRoom
                    ? AcousticReverbPresetKind.LargeRoom
                    : AcousticReverbPresetKind.SmallRoom);

            AudioMixerSnapshot snapshot = preset == ReverbPreset.LargeRoom
                ? largeRoomSnapshot
                : smallRoomSnapshot;
            if (snapshot != null)
                snapshot.TransitionTo(transitionSeconds);
        }

        private bool IsPlayerCollider(Collider other)
        {
            if (other == null)
                return false;

            int mask = playerLayerMask.value;
            return (mask & (1 << other.gameObject.layer)) != 0;
        }

        private void ForceTriggerCollider()
        {
            BoxCollider boxCollider = GetComponent<BoxCollider>();
            if (boxCollider != null)
                boxCollider.isTrigger = true;
        }
    }
}
