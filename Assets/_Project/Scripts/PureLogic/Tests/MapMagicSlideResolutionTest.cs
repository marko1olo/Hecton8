using NUnit.Framework;
using UnityEngine;
using MapMagic.Nodes.ObjectsGenerators;
using MapMagic.Products;
using Den.Tools;
using Den.Tools.Matrices;

namespace MapMagic.Tests
{
    public class MapMagicSlideResolutionTest
    {
        [Test]
        public void SlideResolutionIndependenceTest()
        {
            // Set up test data
            var pos = new Vector3(100, 0, 100);
            var size = new Vector3(1000, 100, 1000);

            // Resolution 1
            int res1 = 512;
            MatrixWorld m1 = new MatrixWorld(new Rect(pos.x, pos.z, size.x, size.z), new CoordRect(0, 0, res1, res1), new Vector3(size.x, 1, size.z));
            for (int x = 0; x < res1; x++)
            for (int z = 0; z < res1; z++)
                m1[x, z] = ((float)x + (float)z) / res1; // A simple slope

            // Resolution 2
            int res2 = 1024;
            MatrixWorld m2 = new MatrixWorld(new Rect(pos.x, pos.z, size.x, size.z), new CoordRect(0, 0, res2, res2), new Vector3(size.x, 1, size.z));
            for (int x = 0; x < res2; x++)
            for (int z = 0; z < res2; z++)
                m2[x, z] = ((float)x + (float)z) / res2; // Same slope, different resolution

            int iterations = 100;
            float moveFactor = 0.2f;
            float stopDelta1 = 0.001f;
            // The algorithm scales stop delta based on PixelSize.x
            // stopDelta = Mathf.Tan(stopSlope*Mathf.Deg2Rad) * data.area.PixelSize.x / data.globals.height;
            // Since PixelSize.x is inversely proportional to resolution, stopDelta2 should be exactly scaled by (res1/res2) relative to stopDelta1
            float stopDelta2 = stopDelta1 * (512f / 1024f);
            float terrainHeight = 100f;

            Transition t1 = new Transition() { pos = new Vector3(500, 0, 500) };
            Transition t2 = new Transition() { pos = new Vector3(500, 0, 500) };

            // Scaled iterations inside Slide algorithm:
            // int resIndepIterations = (int)(iterations/512f * data.area.active.rect.size.x);
            // Here data.area.active.rect.size.x is the pixel resolution.
            int iters1 = (int)(iterations / 512f * res1);
            int iters2 = (int)(iterations / 512f * res2);

            SlideGenerator200.Slide(ref t1, m1, iters1, moveFactor, stopDelta1, terrainHeight);
            SlideGenerator200.Slide(ref t2, m2, iters2, moveFactor, stopDelta2, terrainHeight);

            // Check they actually moved from the starting position
            Assert.IsTrue((t1.pos - new Vector3(500, 0, 500)).magnitude > 0, "Object should have slid down the slope");

            // Are they close (within a small tolerance)?
            Assert.IsTrue((t1.pos - t2.pos).magnitude < 1.0f, $"Slide should be resolution independent. t1: {t1.pos}, t2: {t2.pos}");
        }
    }
}
