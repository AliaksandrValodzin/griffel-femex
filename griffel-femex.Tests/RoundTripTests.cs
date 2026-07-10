using System.Linq;
using griffel_femex;
using griffel_femex.BoundaryConditions;
using griffel_femex.Geometry;
using griffel_femex.Geometry.Sections;
using griffel_femex.Loads;
using griffel_femex.Materials;
using Xunit;

namespace griffel_femex.Tests
{
    public class RoundTripTests
    {
        private static FemexModel BuildSampleModel()
        {
            var model = new FemexModel
            {
                Units = new Units("m", "kN"),
                Levels =
                {
                    new Level(0, "Ground", 145.50, 0.00, isGround: true),
                    new Level(1, "First Floor", 148.50, 3.00),
                },
                Nodes =
                {
                    new Node(1, 0.0, 0.0, levelNumber: 0),
                    new Node(2, 5.0, 0.0, levelNumber: 1, verticalOffset: 0.2),
                    new Node(3, 5.0, 5.0, levelNumber: 1),
                    new Node(4, 0.0, 5.0, levelNumber: 1),
                },
                Sections =
                {
                    new Rectangle(1, "R300x500", 0.3, 0.5),
                    new Circle(2, "C400", 0.4),
                    new TSection(3, "T-beam", 0.6, 0.15, 0.2, 0.8),
                },
                Materials =
                {
                    new Material(1, "Concrete C30", 33e9, 0.2, 25.0, 30e6),
                },
                LoadCases =
                {
                    new LoadCase(1, "Dead", LoadNature.Dead),
                    new LoadCase(2, "Thermal", LoadNature.Temperature),
                },
            };

            model.Bars.Add(new Bar(1, startNodeId: 1, endNodeId: 2, sectionId: 1, materialId: 1, rotationAngle: 30.0));
            model.Plates.Add(new Plate(1, new List<int> { 1, 2, 3, 4 }, thickness: 0.25, materialId: 1));

            model.Loads.Add(new PointLoad { Label = "P1", LoadCaseNumber = 1, NodeNumber = 2, Fz = -10.0, Mx = 2.5 });
            model.Loads.Add(new LinearLoad { Label = "L1", LoadCaseNumber = 1, StartNode = 1, EndNode = 2, MagnitudeStart = -5.0, MagnitudeEnd = -5.0 });
            model.Loads.Add(new AreaLoad { Label = "A1", LoadCaseNumber = 1, NodeSequence = { 1, 2, 3, 4 }, Magnitude = -2.0 });
            model.Loads.Add(new TemperatureLoad { Label = "T1", LoadCaseNumber = 2, ElementIds = { 1 }, DeltaT = 20.0, GradientPerDepth = 5.0 });

            var support = new Support(1, SupportTarget.Point, new List<int> { 1 })
            {
                Ux = Restraint.FixedDof(),
                Uy = Restraint.FixedDof(),
                Uz = Restraint.FixedDof(),
                Rx = Restraint.Free(),
                Ry = Restraint.Free(),
                Rz = Restraint.Spring(1000.0),
            };
            model.Supports.Add(support);

            var hinge = new Hinge(1, HingeTarget.Point, elementId: 1, endOrEdgeIndex: 1, new List<int> { 2 })
            {
                Rz = Release.Full(),
                Ry = Release.Partial(500.0),
            };
            model.Hinges.Add(hinge);

            return model;
        }

        [Fact]
        public void SampleModel_IsValid()
        {
            var model = BuildSampleModel();
            Assert.Empty(model.Validate());
        }

        [Fact]
        public void ToJson_IsCamelCase_AndHasDiscriminators()
        {
            var json = BuildSampleModel().ToJson();

            // camelCase property names
            Assert.Contains("\"levelNumber\"", json);
            Assert.Contains("\"absoluteElevation\"", json);
            Assert.Contains("\"startNodeId\"", json);
            Assert.Contains("\"rotationAngle\"", json);

            // polymorphic discriminators emitted as "type" for base-typed lists
            // (Sections and Loads). Bars/Plates live in concrete-typed lists, so
            // no Element discriminator is written for them.
            Assert.Contains("\"type\": \"rectangle\"", json);
            Assert.Contains("\"type\": \"circle\"", json);
            Assert.Contains("\"type\": \"tshape\"", json);
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
            var original = BuildSampleModel();
            var json = original.ToJson();
            var restored = FemexModel.FromJson(json);

            Assert.Empty(restored.Validate());

            // Units
            Assert.Equal("m", restored.Units!.Length);

            // Geometry
            Assert.Equal(2, restored.Levels.Count);
            Assert.Equal(145.50, restored.Levels[0].AbsoluteElevation);
            Assert.True(restored.Levels[0].IsGround);
            Assert.Equal(4, restored.Nodes.Count);
            Assert.Equal(0.2, restored.Nodes.Single(n => n.NodeNumber == 2).VerticalOffset);

            // Polymorphic sections survive as concrete types
            Assert.IsType<Rectangle>(restored.Sections.Single(s => s.Id == 1));
            Assert.IsType<Circle>(restored.Sections.Single(s => s.Id == 2));
            Assert.IsType<TSection>(restored.Sections.Single(s => s.Id == 3));

            // Bar
            var bar = Assert.Single(restored.Bars);
            Assert.Equal(30.0, bar.RotationAngle);
            Assert.Equal(1, bar.SectionId);

            // Plate
            var plate = Assert.Single(restored.Plates);
            Assert.Equal(new List<int> { 1, 2, 3, 4 }, plate.NodeIds);
            Assert.Equal(0.25, plate.Thickness);

            // Loads (polymorphic)
            Assert.IsType<PointLoad>(restored.Loads.Single(l => l.Label == "P1"));
            var temp = Assert.IsType<TemperatureLoad>(restored.Loads.Single(l => l.Label == "T1"));
            Assert.Equal(20.0, temp.DeltaT);
            Assert.Equal(5.0, temp.GradientPerDepth);

            // Boundary conditions
            var support = Assert.Single(restored.Supports);
            Assert.True(support.Ux.Fixed);
            Assert.False(support.Rz.Fixed);
            Assert.Equal(1000.0, support.Rz.Stiffness);

            var hinge = Assert.Single(restored.Hinges);
            Assert.True(hinge.Rz.Released);
            Assert.Equal(500.0, hinge.Ry.ResidualStiffness);
            Assert.Equal(1, hinge.EndOrEdgeIndex);
        }
    }
}
