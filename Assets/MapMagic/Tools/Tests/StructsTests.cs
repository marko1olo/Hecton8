using NUnit.Framework;
using MapMagic;
using Den.Tools;
using UnityEngine;

namespace MapMagic.Tests
{
    public class StructsTests
    {
        [Test]
        public void PickIntersectingCells_WithCellSize1_ReturnsCorrectRect()
        {
            Coord center = new Coord(0, 0);
            int range = 1;
            int cellRes = 1;

            CoordRect rect = CoordRect.PickIntersectingCells(center, range, cellRes);

            // center (0, 0) and range 1 creates:
            // new CoordRect(0 - 1, 0 - 1, 1 * 2, 1 * 2) -> offset = (-1, -1), size = (2, 2)
            // rectMaxX = offset.x + size.x = -1 + 2 = 1
            // rectMaxZ = offset.z + size.z = -1 + 2 = 1

            // expected minX = -1 / 1 = -1
            // expected minZ = -1 / 1 = -1
            // expected maxX = 1 / 1 = 1. if (1 >= 0 && 1%1 != 0) false. maxX = 1.
            // expected maxZ = 1 / 1 = 1. if (1 >= 0 && 1%1 != 0) false. maxZ = 1.

            // Expected offset = (-1, -1)
            // Expected size = (maxX - minX, maxZ - minZ) = (1 - (-1), 1 - (-1)) = (2, 2)

            Assert.That(rect.offset.x, Is.EqualTo(-1));
            Assert.That(rect.offset.z, Is.EqualTo(-1));
            Assert.That(rect.size.x, Is.EqualTo(2));
            Assert.That(rect.size.z, Is.EqualTo(2));
        }

        [Test]
        public void PickIntersectingCells_WithCellSize2_ReturnsCorrectRect()
        {
            Coord center = new Coord(0, 0);
            int range = 1;
            int cellRes = 2;

            CoordRect rect = CoordRect.PickIntersectingCells(center, range, cellRes);

            // new CoordRect(0 - 1, 0 - 1, 1 * 2, 1 * 2) -> offset = (-1, -1), size = (2, 2)
            // rectMaxX = 1
            // rectMaxZ = 1

            // expected minX = -1 / 2 = 0. if (-1 < 0 && -1%2 != 0) minX-- -> minX = -1
            // expected minZ = -1 / 2 = 0. if (-1 < 0 && -1%2 != 0) minZ-- -> minZ = -1
            // expected maxX = 1 / 2 = 0. if (1 >= 0 && 1%2 != 0) maxX++ -> maxX = 1
            // expected maxZ = 1 / 2 = 0. if (1 >= 0 && 1%2 != 0) maxZ++ -> maxZ = 1

            // Expected offset = (-1, -1)
            // Expected size = (maxX - minX, maxZ - minZ) = (1 - (-1), 1 - (-1)) = (2, 2)

            Assert.That(rect.offset.x, Is.EqualTo(-1));
            Assert.That(rect.offset.z, Is.EqualTo(-1));
            Assert.That(rect.size.x, Is.EqualTo(2));
            Assert.That(rect.size.z, Is.EqualTo(2));
        }
    }
}
