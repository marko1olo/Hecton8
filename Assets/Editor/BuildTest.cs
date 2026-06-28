using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

public class BuildTest
{
    [MenuItem("Hecton8/Test Build")]
    public static void DoBuild()
    {
        Run();
    }

    public static void Run()
    {
        Debug.Log("BuildTest.Run: AssetDatabase refreshed!");
    }
}
