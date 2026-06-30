#if UNITY_EDITOR
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Hecton8.Tests.Editor
{
    public class BakeryShaderGUITests
    {
        private Material _material;

        [TearDown]
        public void TearDown()
        {
            if (_material != null)
            {
                UnityEngine.Object.DestroyImmediate(_material);
                _material = null;
            }
        }

        [Test]
        public void FindProperties_WithMissingVolumeProperties_CatchesExceptionAndReturnsSilently()
        {
            var gui = new BakeryShaderGUI();

            var shader = Shader.Find("Standard");
            if (shader == null)
            {
                Assert.Ignore("Standard shader not found, skipping test.");
                return;
            }

            _material = new Material(shader);
            var properties = MaterialEditor.GetMaterialProperties(new UnityEngine.Object[] { _material });

            // The test simply verifies that `gui.FindProperties` executes safely without crashing
            // when given a set of properties that lacks the volume-related properties.
            // If FindProperty throws an ArgumentException, the try/catch in BakeryShaderGUI handles it.
            Assert.DoesNotThrow(() => gui.FindProperties(properties));
        }
    }
}
#endif
