using UnityEditor;
using UnityEngine;
public class OptLoad {
    public static void Apply() {
        EditorSettings.enterPlayModeOptionsEnabled = true;
        EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload | EnterPlayModeOptions.DisableSceneReload;
        Debug.Log("Applied Fast Playmode!");
    }
}
