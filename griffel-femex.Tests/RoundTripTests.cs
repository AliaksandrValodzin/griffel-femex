using griffel_femex.Geometry.Grids;
using griffel_femex.Geometry.Sections;
using griffel_femex.Geometry.Surfaces;
using griffel_femex.Loads;
using Xunit;

namespace griffel_femex.Tests
{
    public class RoundTripTests
    {
        [Fact]
        public void SampleModel_IsValid()
        {
            var model = SampleModels.Build();
            Assert.Empty(model.Validate());
        }

        [Fact]
        public void ToJson_IsCamelCase_AndHasDiscriminators()
        {
            var json = SampleModels.Build().ToJson();

            // camelCase property names
            Assert.Contains("\"levelNumber\"", json);
            Assert.Contains("\"absoluteElevation\"", json);
            Assert.Contains("\"startNodeId\"", json);
            Assert.Contains("\"rotationAngle\"", json);
            Assert.Contains("\"surfacePropertyId\"", json);
            Assert.Contains("\"localAxisAngle\"", json);

            // polymorphic discriminators emitted as "type" for base-typed lists
            // (Sections, SurfaceProperties and Loads). Bars/Plates live in
            // concrete-typed lists, so no Element discriminator is written for them.
            Assert.Contains("\"type\": \"rectangle\"", json);
            Assert.Contains("\"type\": \"circle\"", json);
            Assert.Contains("\"type\": \"tshape\"", json);
            Assert.Contains("\"type\": \"constant\"", json);
            Assert.Contains("\"type\": \"point\"", json);
            Assert.Contains("\"type\": \"linear\"", json);
            Assert.Contains("\"type\": \"area\"", json);
            Assert.Contains("\"type\": \"temperature\"", json);

            // enums as readable strings
            Assert.Contains("\"nature\": \"Dead\"", json);
            Assert.Contains("\"target\": \"Point\"", json);
        }

        [Fact]
        public void Gridline_IsPolymorphic()
        {
            var json = SampleModels.Build().ToJson();

            Assert.Contains("\"type\": \"orthogonal\"", json);
            Assert.Contains("\"type\": \"free\"", json);
            Assert.Contains("\"direction\": \"Y\"", json);
            Assert.Contains("\"label\": \"D1\"", json);
        }

        [Fact]
        public void Grid_RoundTrips()
        {
            var restored = FemexModel.FromJson(SampleModels.Build().ToJson());

            Assert.Equal(2, restored.Grids.Count);
            Assert.Equal(new List<int> { SampleModels.PrimaryGridId }, restored.DefaultGridIds);

            Grid primary = restored.PrimaryGrid();
            Assert.Equal("Primary", primary.Name);
            Assert.Equal(5, primary.Lines.Count);
            Assert.Equal(-2.0, primary.Extent!.MinX);
            Assert.Equal(12.0, primary.Extent.MaxY);

            var b = Assert.IsType<OrthogonalGridline>(primary.Lines[1]);
            Assert.Equal("B", b.Label);
            Assert.Equal(GridDirection.Y, b.Direction);
            Assert.Equal(10.0, b.Offset);

            var d1 = Assert.IsType<FreeGridline>(primary.Lines[4]);
            Assert.Equal("D1", d1.Label);
            Assert.Equal(10.0, d1.X2);
            Assert.Equal(5.0, d1.Y2);

            Grid core = restored.CoreGrid();
            Assert.Equal(45.0, core.RotationAngle);
            Assert.Equal(3.0, core.OriginX);
            Assert.Null(core.Extent);
        }

        [Fact]
        public void Level_GridIds_AreOmitted_WhenNull()
        {
            var model = SampleModels.Build();

            // The first floor names its grids; the ground floor inherits, and so
            // writes no gridIds at all rather than a null.
            Assert.Contains("\"gridIds\": [", model.ToJson());

            foreach (var level in model.Levels)
                level.GridIds = null;

            Assert.DoesNotContain("\"gridIds\"", model.ToJson());
        }

        [Fact]
        public void Level_GridIds_RoundTripEmptyAsDistinctFromNull()
        {
            var model = SampleModels.Build();
            model.Levels.Single(l => l.LevelNumber == 0).GridIds = new List<int>();

            var restored = FemexModel.FromJson(model.ToJson());

            Assert.Empty(restored.Levels.Single(l => l.LevelNumber == 0).GridIds!);
            Assert.NotNull(restored.Levels.Single(l => l.LevelNumber == 1).GridIds);
        }

        [Fact]
        public void RoundTrip_PreservesKeyFields()
        {
            var original = SampleModels.Build();
            var json = original.ToJson();
            var restored = FemexModel.FromJson(json);

            Assert.Empty(restored.Validate());

            // Units
            Assert.Equal("m", restored.Units!.Length);

            // Geometry
            Assert.Equal(2, restored.Levels.Count);
            Assert.Equal(145.50, restored.Levels[0].AbsoluteElevation);
            Assert.True(restored.Levels[0].IsGround);

            // Polymorphic sections survive as concrete types
            Assert.IsType<Rectangle>(restored.Sections.Single(s => s.Id == 1));
            Assert.IsType<Circle>(restored.Sections.Single(s => s.Id == 2));
            Assert.IsType<TSection>(restored.Sections.Single(s => s.Id == 3));

            // Bar
            var bar = Assert.Single(restored.Bars);
            Assert.Equal(30.0, bar.RotationAngle);
            Assert.Equal(1, bar.SectionId);
            Assert.Equal(1, bar.MaterialId);

            // Plate: thickness now resolves through the shared surface property
            var slab = restored.Slab();
            Assert.Equal(new List<int> { 2, 12, 13, 14 }, slab.NodeIds);
            var property = (ConstantThickness)restored.SurfaceProperties.Single(s => s.Id == slab.SurfacePropertyId);
            Assert.Equal(0.25, property.Thickness);
            Assert.Equal(0.25, property.GetNominalThickness());

            // Loads (polymorphic)
            Assert.IsType<PointLoad>(restored.Loads.Single(l => l.Label == "P1"));
            var area = Assert.IsType<AreaLoad>(restored.Loads.Single(l => l.Label == "A1"));
            Assert.Equal(SampleModels.SlabId, area.PlateId);
            Assert.Null(area.NodeSequence);
            var temp = Assert.IsType<TemperatureLoad>(restored.Loads.Single(l => l.Label == "T1"));
            Assert.Equal(20.0, temp.DeltaT);
            Assert.Equal(5.0, temp.GradientPerDepth);

            // Boundary conditions
            var support = Assert.Single(restored.Supports);
            Assert.True(support.Ux.Fixed);
            Assert.False(support.Rz.Fixed);
            Assert.Equal(1000.0, support.Rz.Stiffness);

            var barHinge = restored.Hinges.Single(h => h.Id == 1);
            Assert.True(barHinge.Rz.Released);
            Assert.Equal(500.0, barHinge.Ry.ResidualStiffness);
            Assert.Equal(1, barHinge.EndOrEdgeIndex);

            var plateHinge = restored.Hinges.Single(h => h.Id == 2);
            Assert.Equal(2, plateHinge.EdgeStartNodeId);
            Assert.Equal(12, plateHinge.EdgeEndNodeId);
            Assert.Null(plateHinge.RegionId);
        }

        [Fact]
        public void Node_VerticalOffset_RoundTrips()
        {
            // No node in the sample is offset from its level — every one of them is
            // shared by the elements meeting there — so the field is exercised here.
            var model = SampleModels.Build();
            model.Nodes.Single(n => n.NodeNumber == 42).VerticalOffset = -0.4;

            var restored = FemexModel.FromJson(model.ToJson());

            Assert.Equal(-0.4, restored.Nodes.Single(n => n.NodeNumber == 42).VerticalOffset);
        }

        [Fact]
        public void SurfaceProperty_IsPolymorphic()
        {
            var restored = FemexModel.FromJson(SampleModels.Build().ToJson());

            var property = Assert.IsType<ConstantThickness>(restored.SurfaceProperties.Single(s => s.Id == 2));
            Assert.Equal("DROP-450", property.Name);
            Assert.Equal(0.45, property.Thickness);
        }

        [Fact]
        public void Mesh_IsOmitted_WhenNull()
        {
            var model = SampleModels.Build();
            model.Mesh = null;

            var json = model.ToJson();

            Assert.DoesNotContain("\"mesh\"", json);
            Assert.Empty(model.Validate());
        }

        [Fact]
        public void Mesh_RoundTrips_WithBacklinks()
        {
            var restored = FemexModel.FromJson(SampleModels.Build().ToJson());

            Assert.NotNull(restored.Mesh);
            Assert.Equal("test", restored.Mesh!.Generator);
            Assert.Equal(4, restored.Mesh.Nodes.Count);
            Assert.Equal(2, restored.Mesh.Nodes[0].SourceNodeId);
            Assert.Equal(SampleModels.FirstFloorElevation, restored.Mesh.Nodes[0].Z);

            var face = Assert.Single(restored.Mesh.Faces);
            Assert.Equal(SampleModels.SlabId, face.PlateId);
            Assert.Null(face.RegionId);
            Assert.Equal(0.25, face.Thickness);
        }
    }
}
