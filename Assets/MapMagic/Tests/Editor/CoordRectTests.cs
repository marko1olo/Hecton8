using NUnit.Framework;
using Den.Tools;

namespace Den.Tools.Tests
{
    public class CoordRectTests
    {
        [Test]
        public void DistanceAxisAligned_InsideRect_ReturnsZero()
        {
            var rect = new CoordRect(5, 5, 3, 3);

            Assert.That(CoordRect.DistanceAxisAligned(new Coord(5, 5), rect), Is.EqualTo(0));
            Assert.That(CoordRect.DistanceAxisAligned(new Coord(6, 6), rect), Is.EqualTo(0));
            Assert.That(CoordRect.DistanceAxisAligned(new Coord(7, 7), rect), Is.EqualTo(0));
        }

        [Test]
        public void DistanceAxisAligned_OutsideRect_ReturnsCorrectDistance()
        {
            var rect = new CoordRect(5, 5, 3, 3); // Valid coords: (5,5) to (7,7)

            // Distance 1
            Assert.That(CoordRect.DistanceAxisAligned(new Coord(4, 5), rect), Is.EqualTo(1));
            Assert.That(CoordRect.DistanceAxisAligned(new Coord(8, 5), rect), Is.EqualTo(1));
            Assert.That(CoordRect.DistanceAxisAligned(new Coord(5, 4), rect), Is.EqualTo(1));
            Assert.That(CoordRect.DistanceAxisAligned(new Coord(5, 8), rect), Is.EqualTo(1));

            // Diagonal distance 1 (Chebyshev distance)
            Assert.That(CoordRect.DistanceAxisAligned(new Coord(4, 4), rect), Is.EqualTo(1));
            Assert.That(CoordRect.DistanceAxisAligned(new Coord(8, 8), rect), Is.EqualTo(1));

            // Distance 2
            Assert.That(CoordRect.DistanceAxisAligned(new Coord(3, 5), rect), Is.EqualTo(2));
            Assert.That(CoordRect.DistanceAxisAligned(new Coord(9, 5), rect), Is.EqualTo(2));
            Assert.That(CoordRect.DistanceAxisAligned(new Coord(5, 3), rect), Is.EqualTo(2));
            Assert.That(CoordRect.DistanceAxisAligned(new Coord(5, 9), rect), Is.EqualTo(2));
        }
    }
}
