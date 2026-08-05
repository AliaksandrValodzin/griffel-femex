using griffel_femex.Geometry;
using Xunit;

namespace griffel_femex.Tests
{
    public class PlateTests
    {
        [Fact]
        public void Plate_Regions_RoundTrip()
        {
            var restored = FemexModel.FromJson(SampleModels.Build().ToJson());
            var slab = restored.Slab();

            Assert.Equal(2, slab.Regions.Count);

            var drop = slab.Regions.Single(r => r.Id == SampleModels.DropPanelRegionId);
            Assert.Equal(PlateRegionKind.Structural, drop.Kind);
            Assert.Equal(10, drop.Priority);
            Assert.Equal(2, drop.SurfacePropertyId);
            Assert.Equal(-0.2, drop.SurfaceOffset);
            Assert.Null(drop.MaterialId); // inherits the plate's

            var hole = slab.Regions.Single(r => r.Id == SampleModels.VoidRegionId);
            Assert.Equal(PlateRegionKind.Opening, hole.Kind);
            Assert.Equal(20, hole.Priority);
            Assert.Equal(new List<int> { 31, 32, 33, 34 }, hole.NodeIds);
        }

        [Fact]
        public void Opening_HasNoThicknessOrMaterial()
        {
            var restored = FemexModel.FromJson(SampleModels.Build().ToJson());

            var hole = restored.Slab().Regions.Single(r => r.Id == SampleModels.VoidRegionId);

            Assert.Null(hole.SurfacePropertyId);
            Assert.Null(hole.MaterialId);
        }

        [Fact]
        public void PlateEnums_SerializeAsStrings()
        {
            var json = SampleModels.Build().ToJson();

            Assert.Contains("\"kind\": \"Opening\"", json);
            Assert.Contains("\"kind\": \"Structural\"", json);
            Assert.Contains("\"behaviour\": \"Membrane\"", json);
            Assert.Contains("\"alignment\": \"Top\"", json);
        }

        [Fact]
        public void WallPlate_SpansTwoLevels_AndValidates()
        {
            var model = SampleModels.Build();
            var wall = model.Wall();

            var levels = wall.NodeIds
                .Select(id => model.Nodes.Single(n => n.NodeNumber == id).LevelNumber)
                .Distinct()
                .ToList();

            Assert.Equal(2, levels.Count);
            Assert.Equal(PlateBehaviour.Membrane, wall.Behaviour);
            Assert.Empty(model.Validate());
        }

        [Fact]
        public void GetNodeIds_YieldsOuterContourThenRegions()
        {
            var slab = SampleModels.Build().Slab();

            Assert.Equal(
                new[] { 11, 12, 13, 14, 21, 22, 23, 24, 31, 32, 33, 34 },
                slab.GetNodeIds());
        }
    }
}
