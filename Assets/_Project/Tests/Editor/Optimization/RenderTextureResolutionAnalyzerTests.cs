using System;
using NUnit.Framework;
using UnityEngine;
using Hecton8.Optimization.Editor;

namespace Hecton8.Optimization.Editor.Tests
{
    public class RenderTextureResolutionAnalyzerTests
    {
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
            }
        }
    }
}
