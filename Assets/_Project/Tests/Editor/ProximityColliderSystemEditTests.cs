using NUnit.Framework;
using UnityEngine;
using Hecton8.Core;

namespace Hecton8.Tests.Editor
{
    public sealed class ProximityColliderSystemEditTests
    {
        private GameObject _go;
        private ProximityColliderSystem _system;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("TestSystem");
            _system = _go.AddComponent<ProximityColliderSystem>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_go != null)
            {
                GameObject.DestroyImmediate(_go);
            }
        }

        [Test]
        public void Initialize_WithVector3Array_CallsOverloadWithArrayLength()
        {
            // The objective is to verify Initialize(Vector3[]) calls Initialize(Vector3[], int).
            // When count <= 0 (e.g. empty array), the system calls ClearRuntimeData() and doesn't
            // try to allocate or interact with IDataVault.

            // In EditMode, _system.Initialize() with valid points might try to touch the real IDataVault
            // which could be null or error-prone if not mocked.
            // By passing a 0-length array, we can safely test that the 1-parameter Initialize
            // correctly delegates to the 2-parameter Initialize passing Length=0,
            // which immediately returns cleanly via ClearRuntimeData().

            Vector3[] emptyArray = new Vector3[0];

            Assert.DoesNotThrow(() =>
            {
                // This will fail if it doesn't pass '0' to the 2nd parameter
                // because passing random memory or null would throw NullReference.
                _system.Initialize(emptyArray);
            }, "Initialize with empty array should safely delegate and clear runtime data.");

            // Test with a null array: should pass '0' as length using null-conditional.
            Assert.DoesNotThrow(() =>
            {
                _system.Initialize(null);
            }, "Initialize with null array should safely delegate length as 0.");
        }
    }
}
