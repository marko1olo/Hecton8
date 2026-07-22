using NUnit.Framework;
using Den.Tools;

namespace Den.Tools.Tests
{
    [TestFixture]
    public class StructsTests
    {
        [Test]
        public void DistanceAxisAligned_CoordInsideRect_ReturnsZero()
        {
            var rect = new CoordRect(new Coord(0, 0), new Coord(10, 10));
            var coord = new Coord(5, 5);
            Assert.That(Coord.DistanceAxisAligned(coord, rect), Is.EqualTo(0));
        }

        [Test]
        public void DistanceAxisAligned_CoordOutsideRight_ReturnsDistance()
        {
            var rect = new CoordRect(new Coord(0, 0), new Coord(10, 10));
            var coord = new Coord(15, 5);
            Assert.That(Coord.DistanceAxisAligned(coord, rect), Is.EqualTo(5));
        }

        [Test]
        public void DistanceAxisAligned_CoordOutsideLeft_ReturnsDistance()
        {
            var rect = new CoordRect(new Coord(0, 0), new Coord(10, 10));
            var coord = new Coord(-5, 5);
            Assert.That(Coord.DistanceAxisAligned(coord, rect), Is.EqualTo(5));
        }

        [Test]
        public void DistanceAxisAligned_CoordOutsideTop_ReturnsDistance()
        {
            var rect = new CoordRect(new Coord(0, 0), new Coord(10, 10));
            var coord = new Coord(5, 15);
            Assert.That(Coord.DistanceAxisAligned(coord, rect), Is.EqualTo(5));
        }

        [Test]
        public void DistanceAxisAligned_CoordOutsideBottom_ReturnsDistance()
        {
            var rect = new CoordRect(new Coord(0, 0), new Coord(10, 10));
            var coord = new Coord(5, -5);
            Assert.That(Coord.DistanceAxisAligned(coord, rect), Is.EqualTo(5));
        }

        [Test]
        public void DistanceAxisAligned_CoordOutsideDiagonal_ReturnsMaxDistance()
        {
            var rect = new CoordRect(new Coord(0, 0), new Coord(10, 10));
            var coord = new Coord(15, 20);
            Assert.That(Coord.DistanceAxisAligned(coord, rect), Is.EqualTo(10)); // Max of (15-10)=5 and (20-10)=10
        }
    }
}
