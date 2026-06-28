#if UNITY_EDITOR

using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Technie.PhysicsCreator;

namespace Technie.PhysicsCreator.QHull.Tests
{
    [TestFixture]
    public class QHullUtilTests
    {
        private Mesh _insufficientPointsMesh;
        private Vector3[] _insufficientMeshVertices;
        private int[] _insufficientMeshIndices;

        [SetUp]
        public void SetUp()
        {
            _insufficientPointsMesh = new Mesh();
            // Create a minimal mesh with only 1 triangle (3 vertices).
            // QuickHull3D requires at least 4 non-coplanar points to build a hull.
            // This will intentionally trigger the SystemException ("Less than four input points specified").
            _insufficientPointsMesh.vertices = new Vector3[]
            {
                new Vector3(0, 0, 0),
                new Vector3(1, 0, 0),
                new Vector3(0, 1, 0)
            };
            _insufficientPointsMesh.triangles = new int[] { 0, 1, 2 };

            _insufficientMeshVertices = _insufficientPointsMesh.vertices;
            _insufficientMeshIndices = _insufficientPointsMesh.triangles;
        }

        [TearDown]
        public void TearDown()
        {
            if (_insufficientPointsMesh != null)
            {
                Object.DestroyImmediate(_insufficientPointsMesh);
            }
        }

        [Test]
        public void FindConvexHull_WithMesh_ThrowsExceptionAndLogsError_WhenShowErrorInLogIsTrue()
        {
            string debugName = "TestHullMesh_LogExpected";

            LogAssert.Expect(LogType.Error, "Could not generate convex hull for " + debugName);

            Mesh resultMesh = QHullUtil.FindConvexHull(debugName, _insufficientPointsMesh, showErrorInLog: true);

            // Despite the exception, it shouldn't crash entirely; it returns an empty/invalid mesh.
            Assert.IsNotNull(resultMesh);
            Object.DestroyImmediate(resultMesh);
        }

        [Test]
        public void FindConvexHull_WithMesh_ThrowsExceptionButDoesNotLogError_WhenShowErrorInLogIsFalse()
        {
            string debugName = "TestHullMesh_NoLogExpected";

            // If an error is logged when it shouldn't be, this test will fail because we expect no errors
            LogAssert.NoUnexpectedReceived();

            Mesh resultMesh = QHullUtil.FindConvexHull(debugName, _insufficientPointsMesh, showErrorInLog: false);

            Assert.IsNotNull(resultMesh);
            Object.DestroyImmediate(resultMesh);
        }

        [Test]
        public void FindConvexHull_WithDataArrays_ThrowsExceptionAndLogsError_WhenShowErrorInLogIsTrue()
        {
            string debugName = "TestHullArrays_LogExpected";

            LogAssert.Expect(LogType.Error, "Could not generate convex hull for " + debugName);

            int[] selectedFaces = new int[] { 0 }; // 1 face (3 vertices)

            QHullUtil.FindConvexHull(debugName, selectedFaces, _insufficientMeshVertices, _insufficientMeshIndices,
                out Vector3[] hullVertices, out int[] hullIndices, showErrorInLog: true);

            Assert.IsNotNull(hullVertices);
            Assert.IsNotNull(hullIndices);
        }

        [Test]
        public void FindConvexHull_WithDataArrays_ThrowsExceptionButDoesNotLogError_WhenShowErrorInLogIsFalse()
        {
            string debugName = "TestHullArrays_NoLogExpected";

            LogAssert.NoUnexpectedReceived();

            int[] selectedFaces = new int[] { 0 };

            QHullUtil.FindConvexHull(debugName, selectedFaces, _insufficientMeshVertices, _insufficientMeshIndices,
                out Vector3[] hullVertices, out int[] hullIndices, showErrorInLog: false);

            Assert.IsNotNull(hullVertices);
            Assert.IsNotNull(hullIndices);
        }
    }
}

#endif
