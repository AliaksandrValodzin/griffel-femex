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
            Assert.Equal(0.2, restored.Nodes.Single(n => n.NodeNumber == 2).VerticalOffset);

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
            Assert.Equal(new List<int> { 11, 12, 13, 14 }, slab.NodeIds);
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
            Assert.Equal(11, plateHinge.EdgeStartNodeId);
            Assert.Equal(12, plateHinge.EdgeEndNodeId);
            Assert.Null(plateHinge.RegionId);
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
            Assert.Equal(11, restored.Mesh.Nodes[0].SourceNodeId);
            Assert.Equal(SampleModels.FirstFloorElevation, restored.Mesh.Nodes[0].Z);

            var face = Assert.Single(restored.Mesh.Faces);
            Assert.Equal(SampleModels.SlabId, face.PlateId);
            Assert.Null(face.RegionId);
            Assert.Equal(0.25, face.Thickness);
        }
    }
}
