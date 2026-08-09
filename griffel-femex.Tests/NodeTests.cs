using griffel_femex.Geometry;
using Xunit;

namespace griffel_femex.Tests
{
    /// <summary>
    /// Node sharing: elements are connected where they name the same node, so
    /// geometry-building code has to reuse the node already at a location.
    /// </summary>
    public class NodeTests
    {
        [Fact]
        public void SampleModel_HasOneNodePerLocation()
        {
            Assert.Empty(SampleModels.Build().Validate(ValidationSeverity.Warning));
        }

        [Fact]
        public void GetOrAddNode_ReturnsTheNodeAlreadyThere()
        {
            var model = SampleModels.Build();
            int before = model.Nodes.Count;

            var node = model.GetOrAddNode(10.0, 10.0, levelNumber: 1);

            Assert.Equal(13, node.NodeNumber);
            Assert.Equal(before, model.Nodes.Count);
        }

        [Fact]
        public void GetOrAddNode_AddsWhenNothingIsThere()
        {
            var model = SampleModels.Build();
            int expectedNumber = model.NextNodeNumber();

            var node = model.GetOrAddNode(5.0, 5.0, levelNumber: 0);

            Assert.Equal(expectedNumber, node.NodeNumber);
            Assert.Same(node, model.Nodes.Single(n => n.NodeNumber == expectedNumber));
            Assert.Empty(model.Validate(ValidationSeverity.Warning));
        }

        [Fact]
        public void GetOrAddNode_ShareTheNodeRatherThanDuplicatingIt()
        {
            var model = SampleModels.Build();

            // Draw a second wall along y = 10 the way authoring code would: ask for
            // each corner, and take whatever is already there.
            var contour = new List<int>
            {
                model.GetOrAddNode(0.0, 10.0, levelNumber: 0).NodeNumber,
                model.GetOrAddNode(10.0, 10.0, levelNumber: 0).NodeNumber,
                model.GetOrAddNode(10.0, 10.0, levelNumber: 1).NodeNumber,
                model.GetOrAddNode(0.0, 10.0, levelNumber: 1).NodeNumber,
            };

            // Its top edge is the slab's far edge, so those two are shared, not new.
            Assert.Equal(13, contour[2]);
            Assert.Equal(14, contour[3]);
            Assert.Empty(model.Validate(ValidationSeverity.Warning));
        }

        [Fact]
        public void FindNodeAt_ReturnsNullWhenNothingIsThere()
        {
            Assert.Null(SampleModels.Build().FindNodeAt(5.0, 5.0, levelNumber: 0));
        }

        [Fact]
        public void FindNodeAt_MatchesOnTheAbsolutePoint_NotTheLevel()
        {
            var model = SampleModels.Build();

            // The first floor is 3.0 above the ground level, so this is node 13.
            var node = model.FindNodeAt(10.0, 10.0, levelNumber: 0, verticalOffset: 3.0);

            Assert.Equal(13, node?.NodeNumber);
        }

        [Fact]
        public void FindNodeAt_ThrowsForAnUnknownLevel()
        {
            var model = SampleModels.Build();

            Assert.Throws<InvalidOperationException>(() => model.FindNodeAt(0.0, 0.0, levelNumber: 9));
        }

        [Fact]
        public void NextNodeNumber_IsOnePastTheHighestInUse()
        {
            var model = SampleModels.Build();
            int expected = model.Nodes.Max(n => n.NodeNumber) + 1;

            Assert.Equal(expected, model.NextNodeNumber());
            Assert.Equal(1, new FemexModel().NextNodeNumber());
        }
    }
}
