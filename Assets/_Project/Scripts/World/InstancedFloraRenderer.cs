using UnityEngine;

namespace Hecton8.World
{
    /// <summary>
    /// Adapter component that exposes the project indirect vegetation renderer under the
    /// explicit instanced-flora name requested by external graphics tasks.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-90)]
    public sealed class InstancedFloraRenderer : HectonIndirectVegetationRenderer
    {
    }
}
