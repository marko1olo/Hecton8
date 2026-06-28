#if UNITY_EDITOR
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using System.IO;
using System.Reflection;
using VLB;
using UnityEditor;

namespace VLB.Tests
{
    public class ShaderGeneratorTests
    {
        [Test]
        public void Generate_FailsGracefully_WhenWriteAllTextThrowsException()
        {
            // We want to trigger the catch block in ShaderGenerator.GenShader.Generate()
            // This catch block handles exceptions from File.WriteAllText(outputFullPath, code)

            // To do this, we can create a directory with the exact same name as the target file.
            // File.WriteAllText will throw an UnauthorizedAccessException when trying to write to a directory path.

            var folderMethod = typeof(ShaderGenerator).GetMethod("GetFolderOutputPath", BindingFlags.NonPublic | BindingFlags.Static);
            string folderPath = (string)folderMethod.Invoke(null, null);

            var nameMethod = typeof(ShaderGenerator).GetMethod("GetShaderAssetName", BindingFlags.NonPublic | BindingFlags.Static);
            string fileName = (string)nameMethod.Invoke(null, new object[] { ShaderMode.SD });

            string fullPath = Path.Combine(folderPath, fileName);

            bool directoryCreated = false;
            string backupContent = null;

            try
            {
                // If the file already exists, we back it up
                if (File.Exists(fullPath))
                {
                    backupContent = File.ReadAllText(fullPath);
                    File.Delete(fullPath);
                }

                // Create a directory with the file's name to force an UnauthorizedAccessException
                Directory.CreateDirectory(fullPath);
                directoryCreated = true;

                var config = new ShaderGenerator.ConfigProps
                {
                    renderPipeline = RenderPipeline.BuiltIn,
                    renderingMode = RenderingMode.MultiPass,
                    raymarchingQualities = new[] { new RaymarchingQuality { stepCount = 10 } }
                };

                // The method should catch the exception, log an error, and return null
                LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("Failed to generate shader Hidden/VLB_SD_BuiltIn_MultiPass in folder .*"));

                var result = ShaderGenerator.Generate(ShaderMode.SD, config);

                Assert.IsNull(result, "Generate should return null when an exception occurs during File.WriteAllText.");
            }
            finally
            {
                if (directoryCreated)
                {
                    Directory.Delete(fullPath);
                }

                // Restore the original file if we backed it up
                if (backupContent != null)
                {
                    File.WriteAllText(fullPath, backupContent);
                }
            }
        }
    }
}
#endif
