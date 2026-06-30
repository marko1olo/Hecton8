#if UNITY_EDITOR
using System;
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
                UnityEngine.Object.DestroyImmediate(_tempRT);
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
                UnityEngine.Object.DestroyImmediate(tex);
            }
        }

        [Test]
        public void CaptureScreenshot_WhenExceptionThrown_RestoresActiveRenderTexture()
        {
            var rt = new RenderTexture(256, 256, 0, RenderTextureFormat.ARGB32);
            rt.Create();

            var prevActive = new RenderTexture(128, 128, 0, RenderTextureFormat.ARGB32);
            prevActive.Create();
            RenderTexture.active = prevActive;

            // In order to reliably trigger an exception in standard Unity APIs inside the try-catch block
            // without relying on file I/O exceptions which differ by platform, we can destroy the RenderTexture
            // instance's underlying native object. This causes Unity's internal methods (like Blit or active assignment)
            // to throw an exception when they attempt to use the destroyed object.
            UnityEngine.Object.DestroyImmediate(rt);

            try
            {
                // This call will fail internally and throw an exception
                RenderTextureResolutionAnalyzer.CaptureScreenshot(rt, "test_exception_path.png");
            }
            catch (Exception)
            {
                // We expect an exception to be thrown
            }

            // The main assertion: The finally block should have restored the active RenderTexture
            Assert.That(RenderTexture.active, Is.EqualTo(prevActive), "RenderTexture.active was not restored after exception in CaptureScreenshot.");

            // Cleanup
            RenderTexture.active = null;
            if (prevActive != null)
            {
                prevActive.Release();
                UnityEngine.Object.DestroyImmediate(prevActive);
            }
        }
    }
}
#endif
