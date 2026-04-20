using UnityEngine;

namespace Hecton8.World
{
    /// <summary>
    /// Receives a large-scale sargassum displacement event such as a leviathan pass or submarine breach.
    /// </summary>
    public interface ISargassumMassiveDisplacementReceiver
    {
        /// <summary>
        /// Registers a massive displacement volume that tears or clears the local sargassum canopy.
        /// </summary>
        /// <param name="position">World-space center of the displacement.</param>
        /// <param name="radius">World-space radius of the displaced canopy.</param>
        /// <param name="duration">Lifetime of the displacement cue in seconds.</param>
        void RegisterMassiveDisplacement(Vector3 position, float radius, float duration);
    }
}
