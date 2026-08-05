using griffel_femex.BoundaryConditions;
using griffel_femex.Geometry;
using griffel_femex.Loads;
using griffel_femex.Mesh;
using Xunit;

namespace griffel_femex.Tests
{
    public class ValidationTests
    {
        private static void AssertReports(FemexModel model, string fragment)
        {
            var messages = model.Validate().ToList();
            Assert.True(
                messages.Any(m => m.Contains(fragment)),
                $"Expected a message containing \"{fragment}\". Got: {string.Join(" | ", messages)}");
        }

        [Fact]
        public void Reports_OpeningThatCarriesMaterial()
        {
            var model = SampleModels.Build();
            model.Slab().Regions.Single(r => r.Id == SampleModels.VoidRegionId).MaterialId = 1;

            AssertReports(model, "is an Opening but carries material 1");
        }

        [Fact]
        public void Reports_StructuralPlateWithoutSurfaceProperty()
        {
            var model = SampleModels.Build();
            model.Slab().SurfacePropertyId = null;

            AssertReports(model, "is Structural but has no surface property");
        }

        [Fact]
        public void Reports_UnknownSurfaceProperty()
        {
            var model = SampleModels.Build();
            model.Slab().SurfacePropertyId = 99;

            AssertReports(model, "references unknown surface property 99");
        }

        [Fact]
        public void Reports_ContourWithTwoNodes()
        {
            var model = SampleModels.Build();
            model.Slab().NodeIds = new List<int> { 11, 12 };

            AssertReports(model, "outer contour has 2 nodes; at least 3 are required");
        }

        [Fact]
        public void Reports_ContourRepeatingANode()
        {
            var model = SampleModels.Build();
            model.Slab().NodeIds = new List<int> { 11, 12, 13, 11 };

            AssertReports(model, "outer contour repeats node 11");
        }

        [Fact]
        public void Reports_DuplicateRegionId()
        {
            var model = SampleModels.Build();
            model.Slab().Regions[1].Id = SampleModels.DropPanelRegionId;

            AssertReports(model, "has more than one region with id 1");
        }

        [Fact]
        public void Reports_DuplicateNodeNumber()
        {
            var model = SampleModels.Build();
            model.Nodes.Add(new Node(11, 1.0, 1.0, levelNumber: 1));

            AssertReports(model, "Duplicate node number 11");
        }

        [Fact]
        public void Reports_ElementIdCollision()
        {
            var model = SampleModels.Build();
            model.Mesh!.Faces[0].Id = SampleModels.BarId;

            AssertReports(model, $"Element id {SampleModels.BarId} is used by more than one");
        }

        [Fact]
        public void Reports_AreaLoadWithBothTargets()
        {
            var model = SampleModels.Build();
            var load = model.Loads.OfType<AreaLoad>().Single();
            load.NodeSequence = new List<int> { 11, 12, 13, 14 };

            AssertReports(model, "sets both plateId and nodeSequence");
        }

        [Fact]
        public void Reports_AreaLoadWithNoTarget()
        {
            var model = SampleModels.Build();
            model.Loads.OfType<AreaLoad>().Single().PlateId = null;

            AssertReports(model, "has no target");
        }

        [Fact]
        public void Reports_AreaLoadOnUnknownRegion()
        {
            var model = SampleModels.Build();
            model.Loads.OfType<AreaLoad>().Single().RegionId = 99;

            AssertReports(model, "references region 99, which does not exist on plate");
        }

        [Fact]
        public void Reports_HingeEdgeNotAdjacent()
        {
            var model = SampleModels.Build();
            model.Hinges.Single(h => h.Id == 2).EdgeEndNodeId = 13;

            AssertReports(model, "are not adjacent in the contour");
        }

        [Fact]
        public void Reports_BarHingeWithPlateEdgeNodes()
        {
            var model = SampleModels.Build();
            model.Hinges.Single(h => h.Id == 1).EdgeStartNodeId = 1;

            AssertReports(model, "sets plate edge nodes but element 1 is not a plate");
        }

        [Fact]
        public void Reports_BarHingeWithBadEndIndex()
        {
            var model = SampleModels.Build();
            model.Hinges.Single(h => h.Id == 1).EndOrEdgeIndex = 2;

            AssertReports(model, "with end index 2; expected 0 or 1");
        }

        [Fact]
        public void Reports_AreaSupportWithNeitherPlateNorNodes()
        {
            var model = SampleModels.Build();
            model.Supports.Add(new Support(2, SupportTarget.Area, new List<int>()));

            AssertReports(model, "is an area support with neither a plate nor any nodes");
        }

        [Fact]
        public void Reports_MeshFaceOnAnOpening()
        {
            var model = SampleModels.Build();
            model.Mesh!.Faces[0].RegionId = SampleModels.VoidRegionId;

            AssertReports(model, "belongs to an opening on plate");
        }

        [Fact]
        public void Reports_MeshFaceOnUnknownRegion()
        {
            var model = SampleModels.Build();
            model.Mesh!.Faces[0].RegionId = 99;

            AssertReports(model, "references region 99, which does not exist on plate");
        }

        [Fact]
        public void Reports_MeshFaceWithWrongNodeCount()
        {
            var model = SampleModels.Build();
            model.Mesh!.Faces[0].NodeIds = new List<int> { 1, 2, 3, 4, 1 };

            AssertReports(model, "has 5 nodes; 3 or 4 are required");
        }

        [Fact]
        public void Reports_NonCoplanarContour()
        {
            var model = SampleModels.Build();

            // Lift one corner of the slab out of its plane.
            model.Nodes.Single(n => n.NodeNumber == 14).VerticalOffset = 0.5;

            AssertReports(model, $"Plate {SampleModels.SlabId} outer contour is not planar");
        }

        [Fact]
        public void Accepts_ContourThatIsPlanarButNotAxisAligned()
        {
            var model = SampleModels.Build();

            // Tilt the whole wall about its base: still exactly planar.
            model.Nodes.Single(n => n.NodeNumber == 43).Y = 1.0;
            model.Nodes.Single(n => n.NodeNumber == 44).Y = 1.0;

            Assert.Empty(model.Validate());
        }

        [Fact]
        public void Reports_PriorityCollisionBetweenOverlappingRegions()
        {
            var model = SampleModels.Build();
            var slab = model.Slab();

            // A second drop panel of the same kind and priority, overlapping the first.
            model.Nodes.Add(new Node(51, 5.0, 5.0, levelNumber: 1));
            model.Nodes.Add(new Node(52, 9.0, 5.0, levelNumber: 1));
            model.Nodes.Add(new Node(53, 9.0, 9.0, levelNumber: 1));
            model.Nodes.Add(new Node(54, 5.0, 9.0, levelNumber: 1));

            slab.Regions.Add(new PlateRegion(3, new List<int> { 51, 52, 53, 54 }, PlateRegionKind.Structural, priority: 10)
            {
                SurfacePropertyId = 2,
            });

            AssertReports(model, "have the same kind (Structural) and priority (10) and overlapping extents");
        }

        [Fact]
        public void Accepts_RegionsThatOnlyTouchAtAnEdge()
        {
            var model = SampleModels.Build();
            var slab = model.Slab();

            // Butts up against the drop panel (x = 7) without overlapping it.
            model.Nodes.Add(new Node(51, 7.0, 3.0, levelNumber: 1));
            model.Nodes.Add(new Node(52, 9.0, 3.0, levelNumber: 1));
            model.Nodes.Add(new Node(53, 9.0, 7.0, levelNumber: 1));
            model.Nodes.Add(new Node(54, 7.0, 7.0, levelNumber: 1));

            slab.Regions.Add(new PlateRegion(3, new List<int> { 51, 52, 53, 54 }, PlateRegionKind.Structural, priority: 10)
            {
                SurfacePropertyId = 2,
            });

            Assert.Empty(model.Validate());
        }

        [Fact]
        public void Accepts_OverlappingRegionsWithDifferentPriorities()
        {
            var model = SampleModels.Build();

            // The void already sits inside the slab; give it the drop panel's
            // footprint so the boxes overlap, but leave the priorities distinct.
            model.Slab().Regions.Single(r => r.Id == SampleModels.VoidRegionId).NodeIds =
                new List<int> { 21, 22, 23, 24 };

            Assert.Empty(model.Validate());
        }

        [Fact]
        public void Example1_LoadsAndValidates()
        {
            string path = Path.Combine(AppContext.BaseDirectory, "Examples", "Example1.femex");
            Assert.True(File.Exists(path), $"Example file not found at {path}.");

            var model = FemexModel.Load(path);

            Assert.Empty(model.Validate());

            Assert.Equal(20, model.Plates.Count);
            Assert.Equal(2, model.SurfaceProperties.Count);
            Assert.Equal(44, model.Mesh!.Faces.Count);
            Assert.Equal(8, model.Loads.OfType<AreaLoad>().Count());

            // The four slab panels each carry exactly one core-shaft opening.
            var slabs = model.Plates.Where(p => p.Regions.Count > 0).ToList();
            Assert.Equal(4, slabs.Count);
            Assert.All(slabs, p => Assert.Equal(PlateRegionKind.Opening, Assert.Single(p.Regions).Kind));

            // The roof temperature load still addresses the same element ids, which
            // are now mesh faces.
            var temperature = Assert.Single(model.Loads.OfType<TemperatureLoad>());
            Assert.All(temperature.ElementIds, id => Assert.Contains(model.Mesh.Faces, f => f.Id == id));
        }
    }
}
