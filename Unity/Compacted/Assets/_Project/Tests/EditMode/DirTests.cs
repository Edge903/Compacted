using NUnit.Framework;

namespace Compacted.Sim.Tests
{
    public class DirTests
    {
        [TestCase(Dir.N, Dir.S)]
        [TestCase(Dir.E, Dir.W)]
        [TestCase(Dir.S, Dir.N)]
        [TestCase(Dir.W, Dir.E)]
        public void Opposite(Dir d, Dir expected) => Assert.AreEqual(expected, d.Opposite());

        [TestCase(Dir.N, 1, Dir.E)]
        [TestCase(Dir.W, 1, Dir.N)]
        [TestCase(Dir.N, -1, Dir.W)]
        [TestCase(Dir.E, 6, Dir.W)]
        public void Rotate(Dir d, int turns, Dir expected) => Assert.AreEqual(expected, d.Rotate(turns));

        [Test]
        public void OffsetsMatchThePrototype()
        {
            // North is screen-up (y - 1), east is x + 1.
            Assert.AreEqual(new Cell(0, -1), Dir.N.Offset());
            Assert.AreEqual(new Cell(1, 0), Dir.E.Offset());
            Assert.AreEqual(new Cell(0, 1), Dir.S.Offset());
            Assert.AreEqual(new Cell(-1, 0), Dir.W.Offset());
        }

        [Test]
        public void StepAndBackReturnsToStart()
        {
            var start = new Cell(2, 3);
            foreach (Dir d in System.Enum.GetValues(typeof(Dir)))
                Assert.AreEqual(start, start.Step(d).Step(d.Opposite()));
        }
    }
}
