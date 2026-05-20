using UnityEngine;

namespace Hecton8.Building
{
    /// <summary>
    /// Optional authored placement rule evaluated by PlayerBuilder after collision checks.
    /// Use this only for semantic placement constraints that cannot be expressed by construction validation data alone.
    /// </summary>
    internal interface IBuildPlacementRule
    {
        bool ValidatePlacement(Vector3 position, Quaternion rotation, out string blockReason);
    }
}
