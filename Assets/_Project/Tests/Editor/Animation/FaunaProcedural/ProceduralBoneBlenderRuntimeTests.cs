#if UNITY_EDITOR && HECTON8_ENABLE_EDITMODE_TESTS
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Hecton8.Animation.FaunaProcedural;

namespace Hecton8.Animation.FaunaProcedural.Tests.Editor
{
    public class ProceduralBoneBlenderRuntimeTests
    {
        [SetUp]
        public void SetUp()
        {
            ClearActiveInstance();
        }

        [TearDown]
        public void TearDown()
        {
            ClearActiveInstance();
        }

        private void ClearActiveInstance()
        {
            var field = typeof(ProceduralBoneBlenderRuntime).GetField("_activeRuntimeInstance", BindingFlags.NonPublic | BindingFlags.Static);
            if (field != null)
            {
                field.SetValue(null, null);
            }
        }

        [Test]
        public void TryGetActiveRuntimeInstance_WhenNoInstance_ReturnsFalseAndNull()
        {
            // Act
            bool result = ProceduralBoneBlenderRuntime.TryGetActiveRuntimeInstance(out var runtime);

            // Assert
            Assert.That(result, Is.False);
            Assert.That(runtime, Is.Null);
        }

        [Test]
        public void TryGetActiveRuntimeInstance_WhenInstanceExistsAndNotDisposed_ReturnsTrueAndInstance()
        {
            // Arrange
            var obj = new GameObject("TestBlender");
            var instance = obj.AddComponent<ProceduralBoneBlenderRuntime>();

            // Set it as active via reflection
            var field = typeof(ProceduralBoneBlenderRuntime).GetField("_activeRuntimeInstance", BindingFlags.NonPublic | BindingFlags.Static);
            field.SetValue(null, instance);

            // Set _disposed to false explicitly
            var disposedField = typeof(ProceduralBoneBlenderRuntime).GetField("_disposed", BindingFlags.NonPublic | BindingFlags.Instance);
            if (disposedField != null)
            {
                disposedField.SetValue(instance, false);
            }

            // Act
            bool result = ProceduralBoneBlenderRuntime.TryGetActiveRuntimeInstance(out var runtime);

            // Assert
            Assert.That(result, Is.True);
            Assert.That(runtime, Is.EqualTo(instance));

            Object.DestroyImmediate(obj);
        }

        [Test]
        public void TryGetActiveRuntimeInstance_WhenInstanceDisposed_ReturnsFalseAndInstance()
        {
            // Arrange
            var obj = new GameObject("TestBlender");
            var instance = obj.AddComponent<ProceduralBoneBlenderRuntime>();

            // Set it as active via reflection
            var field = typeof(ProceduralBoneBlenderRuntime).GetField("_activeRuntimeInstance", BindingFlags.NonPublic | BindingFlags.Static);
            field.SetValue(null, instance);

            // Set _disposed to true explicitly
            var disposedField = typeof(ProceduralBoneBlenderRuntime).GetField("_disposed", BindingFlags.NonPublic | BindingFlags.Instance);
            if (disposedField != null)
            {
                disposedField.SetValue(instance, true);
            }

            // Act
            bool result = ProceduralBoneBlenderRuntime.TryGetActiveRuntimeInstance(out var runtime);

            // Assert
            Assert.That(result, Is.False);
            Assert.That(runtime, Is.EqualTo(instance));

            Object.DestroyImmediate(obj);
        }
    }
}
#endif
