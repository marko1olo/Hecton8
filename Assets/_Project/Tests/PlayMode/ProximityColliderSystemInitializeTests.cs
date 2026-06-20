using System.Collections;
using Hecton8.Core;
using Unity.Mathematics;
using Unity.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Hecton8.Tests.PlayMode
{
    public class ProximityColliderSystemInitializeTests
    {
        private GameObject _go;
        private ProximityColliderSystem _system;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("ProximitySystemTester");
            _system = _go.AddComponent<ProximityColliderSystem>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_go != null)
            {
                Object.DestroyImmediate(_go);
            }
        }

        [Test]
        public void Initialize_WithValidVector3Array_DoesNotThrow()
        {
            var positions = new Vector3[] { new Vector3(1, 2, 3), new Vector3(4, 5, 6) };

            // Testing behavior that should work seamlessly without crashing.
            Assert.DoesNotThrow(() => _system.Initialize(positions));
        }

        [Test]
        public void Initialize_WithNullVector3Array_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => _system.Initialize((Vector3[])null));
        }

        [Test]
        public void Initialize_WithEmptyVector3Array_DoesNotThrow()
        {
            var positions = new Vector3[0];
            Assert.DoesNotThrow(() => _system.Initialize(positions));
        }

        [Test]
        public void Initialize_WithCount_ExceedingVector3BufferLength_DoesNotThrow()
        {
            var positions = new Vector3[2];
            Assert.DoesNotThrow(() => _system.Initialize(positions, 5));
        }

        [Test]
        public void Initialize_WithValidNativeArray_DoesNotThrow()
        {
            var positions = new NativeArray<float3>(2, Allocator.Temp);
            positions[0] = new float3(1, 2, 3);
            positions[1] = new float3(4, 5, 6);

            try
            {
                Assert.DoesNotThrow(() => _system.Initialize(positions));
            }
            finally
            {
                positions.Dispose();
            }
        }

        [Test]
        public void Initialize_WithUncreatedNativeArray_DoesNotThrow()
        {
            var positions = new NativeArray<float3>();
            Assert.DoesNotThrow(() => _system.Initialize(positions));
        }

        [Test]
        public void Initialize_WithEmptyNativeArray_DoesNotThrow()
        {
            var positions = new NativeArray<float3>(0, Allocator.Temp);
            try
            {
                Assert.DoesNotThrow(() => _system.Initialize(positions));
            }
            finally
            {
                positions.Dispose();
            }
        }
    }
}
