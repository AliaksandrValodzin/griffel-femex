using griffel_femex.Geometry.Grids;
using Xunit;

namespace griffel_femex.Tests
{
    /// <summary>
    /// Architectural grids: which grids a level resolves to, and the snapping
    /// primitives built on the grid-local to model-plan transform.
    /// </summary>
    public class GridTests
    {
        // Decimal places for the coordinate comparisons; the transform's own error
        // is around 1e-16.
        private const int Precision = 9;

        // ----- Which grids a level uses -----

        [Fact]
        public void GetGridsForLevel_InheritsTheModelDefault()
        {
            var model = SampleModels.Build();

            var grids = model.GetGridsForLevel(0).ToList();

            Assert.Equal(new[] { SampleModels.PrimaryGridId }, grids.Select(g => g.Id));
        }

        [Fact]
        public void GetGridsForLevel_OverrideReplacesTheDefault()
        {
            var model = SampleModels.Build();

            var grids = model.GetGridsForLevel(1).ToList();

            Assert.Equal(new[] { SampleModels.PrimaryGridId, SampleModels.CoreGridId }, grids.Select(g => g.Id));
        }

        [Fact]
        public void GetGridsForLevel_OverrideIsNotMergedWithTheDefault()
        {
            var model = SampleModels.Build();
            model.Levels.Single(l => l.LevelNumber == 1).GridIds = new List<int> { SampleModels.CoreGridId };

            var grids = model.GetGridsForLevel(1).ToList();

            Assert.Equal(new[] { SampleModels.CoreGridId }, grids.Select(g => g.Id));
        }

        [Fact]
        public void GetGridsForLevel_EmptyListMeansNoGrid()
        {
            var model = SampleModels.Build();
            model.Levels.Single(l => l.LevelNumber == 0).GridIds = new List<int>();

            Assert.Empty(model.GetGridsForLevel(0));
        }

        [Fact]
        public void GetGridsForLevel_ThrowsForAnUnknownLevel()
        {
            var model = SampleModels.Build();

            Assert.Throws<InvalidOperationException>(() => model.GetGridsForLevel(99).ToList());
        }

        // ----- Lookups -----

        [Fact]
        public void FindGridline_IsCaseSensitive()
        {
            var model = SampleModels.Build();

            Assert.NotNull(model.FindGridline(SampleModels.PrimaryGridId, "A"));
            Assert.Null(model.FindGridline(SampleModels.PrimaryGridId, "a"));
        }

        [Fact]
        public void NextGridId_IsOnePastTheHighestInUse()
        {
            Assert.Equal(3, SampleModels.Build().NextGridId());
            Assert.Equal(1, new FemexModel().NextGridId());
        }

        // ----- Intersections -----

        [Fact]
        public void TryGetIntersection_ReturnsThePlanPoint()
        {
            var model = SampleModels.Build();

            Assert.True(model.TryGetIntersection(SampleModels.PrimaryGridId, "B", "2", out double x, out double y));

            Assert.Equal(10.0, x, Precision);
            Assert.Equal(10.0, y, Precision);
        }

        [Fact]
        public void TryGetIntersection_HandlesARotatedGrid()
        {
            var model = SampleModels.Build();

            // Grid-local (0, 4) on a grid at (3, 3) turned 45 degrees.
            Assert.True(model.TryGetIntersection(SampleModels.CoreGridId, "CA", "C2", out double x, out double y));

            double leg = 4.0 / Math.Sqrt(2.0);
            Assert.Equal(3.0 - leg, x, Precision);
            Assert.Equal(3.0 + leg, y, Precision);
        }

        [Fact]
        public void TryGetIntersection_WorksForAFreeLine()
        {
            var model = SampleModels.Build();

            // D1 runs (0, 0) to (10, 5); B is the line x = 10.
            Assert.True(model.TryGetIntersection(SampleModels.PrimaryGridId, "D1", "B", out double x, out double y));

            Assert.Equal(10.0, x, Precision);
            Assert.Equal(5.0, y, Precision);
        }

        [Fact]
        public void TryGetIntersection_IsIndependentOfArgumentOrder()
        {
            var model = SampleModels.Build();

            model.TryGetIntersection(SampleModels.PrimaryGridId, "D1", "B", out double x1, out double y1);
            model.TryGetIntersection(SampleModels.PrimaryGridId, "B", "D1", out double x2, out double y2);

            Assert.Equal(x1, x2, Precision);
            Assert.Equal(y1, y2, Precision);
        }

        [Fact]
        public void TryGetIntersection_ReturnsFalseForParallelLines()
        {
            var model = SampleModels.Build();

            Assert.False(model.TryGetIntersection(SampleModels.PrimaryGridId, "A", "B", out _, out _));
        }

        [Fact]
        public void TryGetIntersection_ReturnsFalseForAnUnknownLabel()
        {
            var model = SampleModels.Build();

            Assert.False(model.TryGetIntersection(SampleModels.PrimaryGridId, "A", "Z", out _, out _));
        }

        [Fact]
        public void ToGridLocal_InvertsToModelPoint()
        {
            var model = SampleModels.Build();
            Grid core = model.CoreGrid();

            model.ToModelPoint(core, 2.5, -1.5, out double x, out double y);
            model.ToGridLocal(core, x, y, out double u, out double v);

            Assert.Equal(2.5, u, Precision);
            Assert.Equal(-1.5, v, Precision);
        }

        // ----- Snapping -----

        [Fact]
        public void GetOrAddNodeAtGrid_ReturnsTheNodeAlreadyThere()
        {
            var model = SampleModels.Build();
            int before = model.Nodes.Count;

            // The core grid's origin is the drop panel's near corner.
            var node = model.GetOrAddNodeAtGrid(SampleModels.CoreGridId, "CA", "C1", levelNumber: 1);

            Assert.Equal(21, node.NodeNumber);
            Assert.Equal(before, model.Nodes.Count);
        }

        [Fact]
        public void GetOrAddNodeAtGrid_AddsWhereNothingIsThere()
        {
            var model = SampleModels.Build();
            int expectedNumber = model.NextNodeNumber();

            var node = model.GetOrAddNodeAtGrid(SampleModels.PrimaryGridId, "B", "2", levelNumber: 0);

            Assert.Equal(expectedNumber, node.NodeNumber);
            Assert.Equal(10.0, node.X, Precision);
            Assert.Equal(10.0, node.Y, Precision);
            Assert.Empty(model.Validate());
        }

        [Fact]
        public void GetOrAddNodeAtGrid_ThrowsForAnUnknownLabel()
        {
            var model = SampleModels.Build();

            Assert.Throws<InvalidOperationException>(
                () => model.GetOrAddNodeAtGrid(SampleModels.PrimaryGridId, "A", "Z", levelNumber: 1));
        }

        [Fact]
        public void GetOrAddNodeAtGrid_ThrowsForParallelLines()
        {
            var model = SampleModels.Build();

            Assert.Throws<InvalidOperationException>(
                () => model.GetOrAddNodeAtGrid(SampleModels.PrimaryGridId, "1", "2", levelNumber: 1));
        }
    }
}
