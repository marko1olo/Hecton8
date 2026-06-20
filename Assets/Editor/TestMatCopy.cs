using UnityEngine;
using UnityEditor;

public class TestMatCopy
{
    public static void Run()
    {
        var shader = Shader.Find("Hidden/InternalErrorShader");
        var mat1 = new Material(shader);
        var tex = new Texture2D(2, 2);
        mat1.SetTexture("HiddenProp", tex);
        var mat2 = new Material(mat1);
        var tex2 = mat2.GetTexture("HiddenProp");
        Debug.Log("Copied tex is null: " + (tex2 == null));
        EditorApplication.Exit(0);
    }
}
