using System;
using System.Collections.Generic;
using AmplifyImpostors;
using CandiceAIforGames.Data;
using NUnit.Framework;
using Unity.Profiling;
using UnityEngine;

namespace VendorCompatibility.Tests.Editor
{
    public sealed class VendorBridgeEditModeTests
    {
        private static readonly Vector2[] QuadPoints =
        {
            new Vector2(0f, 0f),
            new Vector2(1f, 0f),
            new Vector2(1f, 1f),
            new Vector2(0f, 1f)
        };

        [Test]
        public void CandiceSqliteDisabledProvider_ReturnsFailClosedAndCachedEmptyResults()
        {
            CandiceSQLiteProvider provider = new CandiceSQLiteProvider("Data Source=:memory:");
            Dictionary<object, object> row = new Dictionary<object, object>(0);
            Dictionary<object, object> originalRow = row;

            Assert.AreEqual(-1, provider.Insert(row));
            Assert.AreEqual(-1, provider.Update(row));
            Assert.AreEqual(-1, provider.Delete("probe"));
            Assert.AreEqual(-1, provider.SelectObject(ref row, "probe"));
            Assert.AreSame(originalRow, row);

            List<object> firstObjects = provider.SelectAll();
            List<object> secondObjects = provider.SelectAll();
            List<string> firstTables = provider.GetTableNames();
            List<string> secondTables = provider.GetTableNames();
            List<CandiceColumnInfo> firstColumns = provider.GetColumnInfo("probe");
            List<CandiceColumnInfo> secondColumns = provider.GetColumnInfo("probe");

            Assert.AreEqual(0, firstObjects.Count);
            Assert.AreSame(firstObjects, secondObjects);
            Assert.AreEqual(0, firstTables.Count);
            Assert.AreSame(firstTables, secondTables);
            Assert.AreEqual(0, firstColumns.Count);
            Assert.AreSame(firstColumns, secondColumns);
        }

        [Test]
        public void AmplifyGenerateMesh_WithMockQuad_ReturnsMeshWithoutTypeLoadFailure()
        {
            GameObject host = new GameObject("VendorCompatibility_AmplifyProbe");
            Mesh mesh = null;

            try
            {
                AmplifyImpostor impostor = host.AddComponent<AmplifyImpostor>();
                mesh = impostor.GenerateMesh(QuadPoints, Vector3.zero, 1f, 1f, true);

                Assert.IsNotNull(mesh);
                Assert.AreEqual(4, mesh.vertexCount);
                Assert.GreaterOrEqual(mesh.triangles.Length, 3);
            }
            finally
            {
                if (mesh != null)
                {
                    UnityEngine.Object.DestroyImmediate(mesh);
                }

                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void CandiceSqliteDisabledProvider_SelectAllWarmLoop_HasNoGcAllocCounterIncrease()
        {
            CandiceSQLiteProvider provider = new CandiceSQLiteProvider("Data Source=:memory:");
            provider.SelectAll();

            ProfilerRecorder recorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame");

            try
            {
                Assert.IsTrue(recorder.Valid);
                long beforeBytes = GC.GetAllocatedBytesForCurrentThread();

                for (int i = 0; i < 10000; i++)
                {
                    provider.SelectAll();
                }

                long afterBytes = GC.GetAllocatedBytesForCurrentThread();
                Assert.AreEqual(beforeBytes, afterBytes);
            }
            finally
            {
                recorder.Dispose();
            }
        }
    }
}
