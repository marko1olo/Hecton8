using System;
using UnityEngine;

namespace Hecton8.Construction
{
    /// <summary>
    /// Acoustic payload raised by repair drones while their weld torch is active.
    /// </summary>
    public readonly struct RepairDroneTorchAcousticEvent
    {
        public RepairDroneTorchAcousticEvent(Vector3 position, AudioClip clip, float volume, float pitch)
        {
            Position = position;
            Clip = clip;
            Volume = volume;
            Pitch = pitch;
        }

        public Vector3 Position { get; }
        public AudioClip Clip { get; }
        public float Volume { get; }
        public float Pitch { get; }
    }

    /// <summary>
    /// Static event bridge that lets the audio owner consume repair-torch pulses without scene scans.
    /// </summary>
    public static class RepairDroneTorchAcousticEvents
    {
        public delegate void RepairDroneTorchAcousticEventHandler(in RepairDroneTorchAcousticEvent acousticEvent);

        public static event RepairDroneTorchAcousticEventHandler OnTorchAcoustic;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            OnTorchAcoustic = null;
        }

        public static void Notify(in RepairDroneTorchAcousticEvent acousticEvent)
        {
            OnTorchAcoustic?.Invoke(acousticEvent);
        }
    }

    /// <summary>
    /// Retired source-name marker. Runtime drones now live exclusively in DroneFleetManager native state.
    /// </summary>
    [Obsolete("RepairDroneEntity MonoBehaviour is retired. Use DroneFleetManager headless native state.", true)]
    public sealed class RepairDroneEntity
    {
        private RepairDroneEntity()
        {
        }
    }
}
