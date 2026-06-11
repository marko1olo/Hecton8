using UnityEditor;
using UnityEngine;

public static class CheckInputSettings
{
    [MenuItem("Tools/Check Input Settings")]
    public static void Run()
    {
        Debug.Log("ENABLE_LEGACY_INPUT_MANAGER defined? " + 
#if ENABLE_LEGACY_INPUT_MANAGER
            "YES"
#else
            "NO"
#endif
        );

        var settings = UnityEngine.InputSystem.InputSystem.settings;
        Debug.Log("InputSystem.settings is null? " + (settings == null ? "YES" : "NO"));
        if (settings != null)
            Debug.Log("InputSystem.settings Name: " + settings.name);
    }
}
