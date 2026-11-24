using NUnit.Framework;

namespace Compacted.Sim.Tests
{
    public abstract class SimTestBase
    {
        protected static World NewWorld(bool freeBuild = false)
        {
            var w = new World(EarlyGameContent.Build()) { FreeBuild = freeBuild };
            return w;
        }

        /// <summary>Place a block or fail the test with the reason.</summary>
        protected static T Place<T>(World w, string type, int dim, int x, int y, Dir dir = Dir.E) where T : Entity
        {
            var r = w.Place(type, dim, x, y, dir, out var e);
            Assert.IsTrue(r.Ok, $"Placing {type} at ({x},{y}): {r.Reason}");
            return (T)e;
        }

        protected static void Destroy(World w, int dim, int x, int y) => w.ExecuteDestroy(w.PlanDestroy(dim, x, y, x, y));
    }
}
