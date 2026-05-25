using UnityEngine;

namespace Hecton8.Visor
{
    /// <summary>
    /// Legacy serialized caustics projector component. Deferred screen-space caustics own runtime output.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CausticsProjectorManager : MonoBehaviour
    {
        private void Awake()
        {
            enabled = false;
        }

        private void OnEnable()
        {
            enabled = false;
        }
    }
}
