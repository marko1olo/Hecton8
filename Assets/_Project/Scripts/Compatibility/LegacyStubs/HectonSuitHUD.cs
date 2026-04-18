using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Legacy compatibility stub for obsolete HUD components stored in recovery scenes.
/// </summary>
public sealed class HectonSuitHUD : MonoBehaviour
{
    public Component survival;
    public Text oxygenLabel;
    public Text energyLabel;
    public Text integrityLabel;
    public Image oxygenBar;
    public Image energyBar;
    public Image integrityBar;
    public Text statusLabel;
    public Text depthLabel;
    public Text pressureLabel;
    public Color normalColor = Color.cyan;
    public Color warningColor = Color.yellow;
    public Color criticalColor = Color.red;
}
