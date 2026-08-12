using griffel_femex.BoundaryConditions;
using griffel_femex.Geometry;
using griffel_femex.Geometry.Grids;
using griffel_femex.Loads;
using griffel_femex.Loads.Combinations;
using griffel_femex.Mesh;
using Xunit;

namespace griffel_femex.Tests
{
    public class ValidationTests
    {
        private static void AssertReports(FemexModel model, string fragment)
        {
            AssertReports(model, ValidationSeverity.Error, fragment);
        }

        private static void AssertReports(FemexModel model, ValidationSeverity severity, string fragment)
        {
            var messages = model.Validate().ToList();
            Assert.True(
                messages.Any(m => m.Severity == severity && m.Text.Contains(fragment)),
                $"Expected a {severity} containing \"{fragment}\". Got: {string.Join(" | ", messages)}");
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
            model.Slab().NodeIds = new List<int> { 2, 12 };

            AssertReports(model, "outer contour has 2 nodes; at least 3 are required");
        }

        [Fact]
        public void Reports_ContourRepeatingANode()
        {
            var model = SampleModels.Build();
            model.Slab().NodeIds = new List<int> { 2, 12, 13, 2 };

            AssertReports(model, "outer contour repeats node 2");
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
            model.Nodes.Add(new Node(12, 1.0, 1.0, levelNumber: 1));

            AssertReports(model, "Duplicate node number 12");
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
            load.NodeSequence = new List<int> { 2, 12, 13, 14 };

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

            // Tilt the whole wall about its top edge: still exactly planar. The base
            // moves rather than the top, because the top corners are slab corners.
            model.Nodes.Single(n => n.NodeNumber == 1).Y = 1.0;
            model.Nodes.Single(n => n.NodeNumber == 42).Y = 1.0;

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

            // Butts up against the drop panel (x = 7) without overlapping it, and
            // shares the two corners it has in common with it rather than
            // duplicating them.
            model.Nodes.Add(new Node(52, 9.0, 3.0, levelNumber: 1));
            model.Nodes.Add(new Node(53, 9.0, 7.0, levelNumber: 1));

            slab.Regions.Add(new PlateRegion(3, new List<int> { 22, 52, 53, 23 }, PlateRegionKind.Structural, priority: 10)
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

        // ----- Coincident nodes -----

        [Fact]
        public void Warns_TwoNodesAtOneLocation()
        {
            var model = SampleModels.Build();

            // A second node on the slab's far corner, as if the wall had been drawn
            // without noticing the slab was already there.
            model.Nodes.Add(new Node(99, 10.0, 10.0, levelNumber: 1));

            AssertReports(model, ValidationSeverity.Warning, "Nodes 13 and 99 are at the same location (10, 10, 148.5)");
        }

        [Fact]
        public void CoincidentNodes_AreAWarning_NotAnError()
        {
            var model = SampleModels.Build();
            model.Nodes.Add(new Node(99, 10.0, 10.0, levelNumber: 1));

            // The format allows it: this is how a deliberately disconnected joint is
            // written, so nothing about the model is actually invalid.
            Assert.Empty(model.Validate(ValidationSeverity.Error));
            Assert.Single(model.Validate(ValidationSeverity.Warning));
        }

        [Fact]
        public void Warns_OnceForThreeNodesAtOneLocation()
        {
            var model = SampleModels.Build();
            model.Nodes.Add(new Node(98, 10.0, 10.0, levelNumber: 1));
            model.Nodes.Add(new Node(99, 10.0, 10.0, levelNumber: 1));

            var warning = Assert.Single(model.Validate(ValidationSeverity.Warning));
            Assert.Contains("Nodes 13, 98 and 99 are at the same location", warning.Text);
        }

        [Fact]
        public void Warns_WhenTheSameLocationIsReachedFromAnotherLevel()
        {
            var model = SampleModels.Build();

            // Hung 3.0 above the ground level, which is where the first floor is.
            model.Nodes.Add(new Node(99, 10.0, 10.0, levelNumber: 0, verticalOffset: 3.0));

            AssertReports(model, ValidationSeverity.Warning, "Nodes 13 and 99 are at the same location");
        }

        [Fact]
        public void Accepts_NodesSeparatedByMoreThanTheTolerance()
        {
            var model = SampleModels.Build();

            // A millimetre apart is a distinct location, not a coincidence.
            model.Nodes.Add(new Node(99, 10.0, 10.001, levelNumber: 1));

            Assert.Empty(model.Validate());
        }

        [Fact]
        public void Warns_ForNodesWithinTheTolerance()
        {
            var model = SampleModels.Build();

            // Well inside 1e-6 of the model's ~14 m diagonal.
            model.Nodes.Add(new Node(99, 10.0, 10.0 + 1e-9, levelNumber: 1));

            AssertReports(model, ValidationSeverity.Warning, "Nodes 13 and 99 are at the same location");
        }

        // ----- Gridlines -----

        [Fact]
        public void Reports_DuplicateGridId()
        {
            var model = SampleModels.Build();
            model.Grids.Add(new Grid(SampleModels.PrimaryGridId, "Copy"));

            AssertReports(model, $"Duplicate grid id {SampleModels.PrimaryGridId}.");
        }

        [Fact]
        public void Reports_UnknownGridInTheModelDefault()
        {
            var model = SampleModels.Build();
            model.DefaultGridIds.Add(99);

            AssertReports(model, "Model default grid list references unknown grid 99.");
        }

        [Fact]
        public void Reports_UnknownGridOnALevel()
        {
            var model = SampleModels.Build();
            model.Levels.Single(l => l.LevelNumber == 1).GridIds!.Add(99);

            AssertReports(model, "Level 1 references unknown grid 99.");
        }

        [Fact]
        public void Reports_RepeatedGridInALevelsList()
        {
            var model = SampleModels.Build();
            model.Levels.Single(l => l.LevelNumber == 1).GridIds!.Add(SampleModels.CoreGridId);

            AssertReports(model, $"Level 1 repeats grid {SampleModels.CoreGridId}.");
        }

        [Fact]
        public void Reports_GridlineWithNoLabel()
        {
            var model = SampleModels.Build();
            model.PrimaryGrid().Lines.Add(new OrthogonalGridline("  ", GridDirection.X, 20.0));

            AssertReports(model, $"Grid {SampleModels.PrimaryGridId} has a line with no label.");
        }

        [Fact]
        public void Reports_DuplicateGridlineLabel()
        {
            var model = SampleModels.Build();
            model.PrimaryGrid().Lines.Add(new OrthogonalGridline("A", GridDirection.X, 20.0));

            AssertReports(model, $"Grid {SampleModels.PrimaryGridId} has more than one line labelled \"A\".");
        }

        [Fact]
        public void Reports_FreeGridlineWithCoincidentEndPoints()
        {
            var model = SampleModels.Build();
            model.PrimaryGrid().Lines.Add(new FreeGridline("D2", 4.0, 4.0, 4.0, 4.0));

            AssertReports(model, $"Grid {SampleModels.PrimaryGridId} line \"D2\" has coincident end points");
        }

        [Fact]
        public void Reports_BackToFrontExtent()
        {
            var model = SampleModels.Build();
            model.PrimaryGrid().Extent = new GridExtent(12.0, -2.0, -2.0, 12.0);

            AssertReports(model, $"Grid {SampleModels.PrimaryGridId} has an extent whose minX is not less than its maxX.");
        }

        [Fact]
        public void Warns_DuplicateLineInOneGrid()
        {
            var model = SampleModels.Build();

            // The same line as "A", drawn a second time as a free line.
            model.PrimaryGrid().Lines.Add(new FreeGridline("A2", 0.0, -5.0, 0.0, 5.0));

            AssertReports(model, ValidationSeverity.Warning,
                $"Grid {SampleModels.PrimaryGridId} lines \"A\" and \"A2\" are the same line.");
        }

        [Fact]
        public void DuplicateLine_IsAWarning_NotAnError()
        {
            var model = SampleModels.Build();
            model.PrimaryGrid().Lines.Add(new FreeGridline("A2", 0.0, -5.0, 0.0, 5.0));

            Assert.Empty(model.Validate(ValidationSeverity.Error));
            Assert.Single(model.Validate(ValidationSeverity.Warning));
        }

        [Fact]
        public void Accepts_ParallelLinesThatAreNotTheSameLine()
        {
            var model = SampleModels.Build();

            // Parallel to "A" but half a metre off it.
            model.PrimaryGrid().Lines.Add(new FreeGridline("A2", 0.5, -5.0, 0.5, 5.0));

            Assert.Empty(model.Validate());
        }

        [Fact]
        public void Warns_WhenOneLevelsGridsShareALabel()
        {
            var model = SampleModels.Build();
            model.CoreGrid().Lines.Add(new OrthogonalGridline("B", GridDirection.X, 8.0));

            AssertReports(model, ValidationSeverity.Warning,
                $"Level 1 uses grids {SampleModels.PrimaryGridId} and {SampleModels.CoreGridId}, " +
                "which both have a line labelled \"B\".");
        }

        [Fact]
        public void Accepts_TheSameLabelOnGridsNoLevelUsesTogether()
        {
            var model = SampleModels.Build();

            // Grid 3 also has an "A", but no level names it.
            model.Grids.Add(new Grid(model.NextGridId(), "Unused")
            {
                Lines = { new OrthogonalGridline("A", GridDirection.Y, 0.0) },
            });

            Assert.Empty(model.Validate());
        }

        // ----- Load combinations -----

        [Fact]
        public void Reports_DuplicateLoadCombinationNumber()
        {
            var model = SampleModels.Build();
            model.LoadCombinations.Add(
                new LoadCombination(SampleModels.UltimateCombinationNumber, "U1 again", LimitState.Ultimate)
                {
                    Terms = { new LoadCombinationTerm(1, 1.0) },
                });

            AssertReports(model, $"Duplicate load combination number {SampleModels.UltimateCombinationNumber}.");
        }

        [Fact]
        public void Reports_LoadCombinationWithNoTerms()
        {
            var model = SampleModels.Build();
            model.FindLoadCombination(SampleModels.EnvelopeCombinationNumber)!.Terms.Clear();

            AssertReports(model, $"Load combination {SampleModels.EnvelopeCombinationNumber} has no terms.");
        }

        [Fact]
        public void Reports_LoadCombinationReferencingUnknownLoadCase()
        {
            var model = SampleModels.Build();
            model.FindLoadCombination(SampleModels.UltimateCombinationNumber)!.Terms[1].LoadCaseNumber = 99;

            AssertReports(model,
                $"Load combination {SampleModels.UltimateCombinationNumber} references unknown load case 99.");
        }

        [Fact]
        public void Warns_LoadCaseRepeatedInOneCombination()
        {
            var model = SampleModels.Build();
            model.FindLoadCombination(SampleModels.UltimateCombinationNumber)!.Terms.Add(
                new LoadCombinationTerm(1, 0.3));

            AssertReports(model, ValidationSeverity.Warning,
                $"Load combination {SampleModels.UltimateCombinationNumber} includes load case 1 " +
                "more than once; the factors add.");
        }

        [Fact]
        public void RepeatedLoadCase_IsAWarning_NotAnError()
        {
            var model = SampleModels.Build();
            model.FindLoadCombination(SampleModels.UltimateCombinationNumber)!.Terms.Add(
                new LoadCombinationTerm(1, 0.3));

            // Legal FEMEX, and legal in ETABS too: the factors add to 1.5. Unlike a
            // repeated node in a contour, it means something.
            Assert.Empty(model.Validate(ValidationSeverity.Error));
            Assert.Single(model.Validate(ValidationSeverity.Warning));
        }

        [Fact]
        public void Warns_DuplicateLoadCombinationLabel()
        {
            var model = SampleModels.Build();
            model.FindLoadCombination(SampleModels.ExcludedCombinationNumber)!.Label = "U1";

            AssertReports(model, ValidationSeverity.Warning,
                "More than one load combination is labelled \"U1\".");
        }

        [Fact]
        public void Warns_OnceForThreeCombinationsSharingALabel()
        {
            var model = SampleModels.Build();
            foreach (var combination in model.LoadCombinations)
                combination.Label = "C";

            var warning = Assert.Single(model.Validate(ValidationSeverity.Warning));
            Assert.Contains("More than one load combination is labelled \"C\".", warning.Text);
        }

        [Fact]
        public void Warns_LoadCombinationWithNoLabel()
        {
            var model = SampleModels.Build();
            model.FindLoadCombination(SampleModels.EnvelopeCombinationNumber)!.Label = null;

            AssertReports(model, ValidationSeverity.Warning,
                $"Load combination {SampleModels.EnvelopeCombinationNumber} has no label; " +
                "a program that keys combinations by name will invent one.");
        }

        [Fact]
        public void NullLabels_DoNotCollideWithEachOther()
        {
            var model = SampleModels.Build();
            model.FindLoadCombination(SampleModels.EnvelopeCombinationNumber)!.Label = null;
            model.FindLoadCombination(SampleModels.ExcludedCombinationNumber)!.Label = null;

            var warnings = model.Validate(ValidationSeverity.Warning).ToList();

            // Two "no label" warnings, and emphatically not a duplicate-label one:
            // an absent name is not a name two combinations share.
            Assert.Empty(model.Validate(ValidationSeverity.Error));
            Assert.Equal(2, warnings.Count);
            Assert.All(warnings, w => Assert.Contains("has no label", w.Text));
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
            Assert.Equal(7, model.LoadCombinations.Count);

            // Five ultimate combinations, and one of the two serviceability ones
            // kept out of the envelope on purpose.
            Assert.Equal(5, model.GetDesignEnvelope(LimitState.Ultimate).Count());
            var serviceability = Assert.Single(model.GetDesignEnvelope(LimitState.Serviceability));
            Assert.Equal(201, serviceability.Number);
            Assert.Equal(1.2, model.GetTotalFactor(105, 1));

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
