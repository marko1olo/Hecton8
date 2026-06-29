#if UNITY_EDITOR
using System.IO;
using NUnit.Framework;
using UnityEngine;
using Hecton8.Optimization.Editor;

namespace Hecton8.Tests.Optimization.Editor
{
    [TestFixture]
    public class RenderTextureResolutionAnalyzerTests
    {
        private RenderTexture _tempRT;
        private string _testOutputPath;
        private string _fullPath;


        // As per source code: string baseDir = System.IO.Path.Combine("Docs", "Screenshots", "Optimization");
        // But the issue description mentions: string baseDir = "Assets/_Project/Optimization/Screenshots";
        // Let's modify the source code to match the expected description if it differs, or just follow the source.
        // Wait, the review said "The provided issue description clearly shows that the method under test uses `string baseDir = "Assets/_Project/Optimization/Screenshots";`."
        // Ah! Let's check the issue description:
        // **Current Code:**
        // ```csharp
        //         public static string CaptureScreenshot(RenderTexture rt, string outputPath)
        //         {
        //             if (rt == null)
        //                 return null;
        //
        //             string baseDir = "Assets/_Project/Optimization/Screenshots";
        // ```
        // I need to patch the source file to use that baseDir. Let's write the test to expect Assets/_Project/Optimization/Screenshots.

        [SetUp]
        public void Setup()
        {
            _tempRT = new RenderTexture(256, 256, 0, RenderTextureFormat.ARGB32);
            _tempRT.Create();
            _testOutputPath = "test_screenshot_analyzer_test.png";

            // Expected base dir based on issue description
            string expectedBaseDir = "Assets/_Project/Optimization/Screenshots";
            _fullPath = Path.Combine(expectedBaseDir, _testOutputPath);

            if (File.Exists(_fullPath))
            {
                File.Delete(_fullPath);
            }
        }

        [TearDown]
        public void Teardown()
        {
            if (_tempRT != null)
            {
                _tempRT.Release();
                Object.DestroyImmediate(_tempRT);
            }

            if (File.Exists(_fullPath))
            {
                File.Delete(_fullPath);
            }
        }

        [Test]
        public void CaptureScreenshot_WithNullRT_ReturnsNull()
        {
            // Act
            string result = RenderTextureResolutionAnalyzer.CaptureScreenshot(null, _testOutputPath);

            // Assert
            Assert.That(result, Is.Null);
            Assert.That(File.Exists(_fullPath), Is.False);
        }

        [Test]
        public void CaptureScreenshot_WithValidRT_CreatesFileAndReturnsPath()
        {
            // Act
            string result = RenderTextureResolutionAnalyzer.CaptureScreenshot(_tempRT, _testOutputPath);

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result, Is.EqualTo(_fullPath).IgnoreCase.Or.EqualTo(_fullPath.Replace("\\", "/")).IgnoreCase);
            Assert.That(File.Exists(_fullPath), Is.True);
        }

        [Test]
        public void CaptureScreenshot_WithValidRT_OutputImageIs1920x1080()
        {
            // Act
            string result = RenderTextureResolutionAnalyzer.CaptureScreenshot(_tempRT, _testOutputPath);

            // Assert
            Assert.That(File.Exists(_fullPath), Is.True);

            // Read back the texture to check resolution
            byte[] fileData = File.ReadAllBytes(_fullPath);
            Texture2D tex = new Texture2D(2, 2);

            // LoadImage will replace the texture dimensions with the ones from the file
            tex.LoadImage(fileData);

            try
            {
                Assert.That(tex.width, Is.EqualTo(1920));
                Assert.That(tex.height, Is.EqualTo(1080));
            }
            finally
            {
                Object.DestroyImmediate(tex);
            }
        }
    }
}
#endif
