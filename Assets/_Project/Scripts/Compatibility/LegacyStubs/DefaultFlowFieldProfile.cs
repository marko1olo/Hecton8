using UnityEngine;

/// <summary>
/// Legacy compatibility stub for abandoned flow-field editor profile data.
/// </summary>
[CreateAssetMenu(menuName = "Hecton8/Legacy/Flow Field Profile", fileName = "DefaultFlowFieldProfile")]
public sealed class DefaultFlowFieldProfile : ScriptableObject
{
    public float areaWidth = 50f;
    public float areaHeight = 50f;
    public int gridResolutionX = 20;
    public int gridResolutionY = 20;
    public float sampleHeight = 0.5f;
    public float arrowLength = 2f;
    public float arrowThickness = 0.05f;
    public float maxForceScale = 5f;
    public bool showGlobalCurrent = true;
    public bool showLocalCurrents = true;
    public bool onlySelectedVolumes;
}
