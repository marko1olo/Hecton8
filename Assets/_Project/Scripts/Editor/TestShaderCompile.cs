using UnityEngine;
using UnityEditor;

public class TestShaderCompile
{
    public static void CheckShader()
    {
        Shader shader = Shader.Find("Hecton8/URP/ProceduralFlora");
        if (shader == null)
        {
            Debug.LogError("Shader not found!");
            return;
        }

        bool hasErrors = ShaderUtil.ShaderHasError(shader);
        if (hasErrors)
        {
            Debug.LogError("SHADER HAS ERRORS!");

            // Getting the actual error messages from Unity's internal API requires reflection or just looking at the log.
            // But just knowing if it has errors is enough to fail.
        }
        else
        {
            Debug.Log("SHADER COMPILED SUCCESSFULLY!");
        }
    }
}
