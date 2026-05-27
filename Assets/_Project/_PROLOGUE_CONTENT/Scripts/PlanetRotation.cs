using UnityEngine;

[DisallowMultipleComponent]
public class PlanetRotation : MonoBehaviour
{
    [Tooltip("Deprecated prototype value. Orbital presentation phase is now owned by OrbitalRelativityDirector shader globals.")]
    public float rotationSpeed = 2f;

    private void OnEnable()
    {
        enabled = false;
    }
}
