#if UNITY_EDITOR
using NUnit.Framework;
using System.IO;
using System;
using UnityEngine;
using VLB;
using UnityEditor;

namespace VLB.Tests
{
    public class ShaderGeneratorTests
    {
        [TearDown]
        public void TearDown()
        {
            // Reset the mock after tests so it doesn't break anything else
            ShaderGenerator.mockWriteAllText = null;
        }

        [Test]
        public void Generate_FileWriteException_ReturnsNullAndLogsError()
        {
            // Arrange
            ShaderGenerator.mockWriteAllText = (path, content) =>
            {
                throw new IOException("Simulated write failure");
            };

            var configProps = new ShaderGenerator.ConfigProps
            {
                renderPipeline = RenderPipeline.BuiltIn,
                renderingMode = RenderingMode.MultiPass,
                raymarchingQualities = new RaymarchingQuality[0]
            };

            // Act
            var result = ShaderGenerator.Generate(ShaderMode.SD, configProps);

            // Assert
            Assert.IsNull(result, "ShaderGenerator.Generate should return null if a file write error occurs.");

            // LogAssert.Expect with a simpler string or a Singleline regex avoids newline matching issues
            UnityEngine.TestTools.LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("Simulated write failure"));
        }
    }
}
#endif
