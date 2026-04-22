using UnityEngine;

namespace Hecton8.World
{
    /// <summary>
    /// Cartographer-published far-field HLOD instance payload.
    /// Coordinates stay in local runtime space and are shifted on GPU.
    /// </summary>
    public struct HLODInstance
    {
        public Matrix4x4 LocalToWorld;
        public Bounds LocalBounds;
        public float Fade01;
    }
}
