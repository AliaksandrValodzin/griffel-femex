using griffel_femex.BoundaryConditions;
using griffel_femex.Comparison;
using griffel_femex.Geometry;
using griffel_femex.Geometry.Sections;
using griffel_femex.Interop;
using griffel_femex.Loads;
using Xunit;

namespace griffel_femex.Tests
{
    /// <summary>
    /// The model diff of <c>FEMEX_Adapters.md</c> §7.2, whose whole purpose is that
    /// the equivalence was fixed <i>before</i> the comparison loop was written.
    ///
    /// The definition has to earn its keep against a real round trip, where a clean
    /// crossing differs in ways no adapter should have to report: ids and node
    /// numbers are renumbered by the native program and renumbered again on the way
    /// back, list order is preserved by nothing, and coordinates come back through
    /// the native program's own precision. Every test below is one of those, asserted
    /// not to be a difference — and its opposite, asserted to be one.
    /// </summary>
    public class ModelDiffTests
    {
        private static FemexModel Golden() => FemexModel.Load(Path.Combine("Examples", "Conformance1.femex"));

        private static FemexModel Clone(FemexModel model) => FemexModel.FromJson(model.ToJson());

        // ----- The baseline -----

        [Fact]
        public void AModelAgainstItself_HasNoDifferences()
        {
            FemexModel model = Golden();
            Assert.Empty(ModelDiff.Compare(model, Clone(model)));
        }

        [Fact]
        public void TheGoldenFixture_CarriesAUidOnEverything()
        {
            // §7.2: uid coverage is a precondition of the test suite, not a nicety.
            // A conformance baseline without it could not be round-trip-tested at all.
            foreach (var (entity, _, owner) in Golden().EnumerateIdentified())
                Assert.True(entity.Uid.HasValue, $"{owner} carries no uid.");
        }

        // ----- What is not a difference -----

        [Fact]
        public void Renumbering_IsNotADifference()
        {
            FemexModel left = Golden();
            FemexModel right = Clone(left);

            // What a native program does on the way out and again on the way back:
            // every id shifted, every reference to it shifted with it.
            foreach (Node node in right.Nodes)
                node.NodeNumber += 1000;
            foreach (Bar bar in right.Bars)
            {
                bar.StartNodeId += 1000;
                bar.EndNodeId += 1000;
                bar.Id += 500;
            }
            foreach (Plate plate in right.Plates)
            {
                plate.NodeIds = plate.NodeIds.ConvertAll(id => id + 1000);
                plate.Id += 500;
                foreach (PlateRegion region in plate.Regions)
                    region.NodeIds = region.NodeIds.ConvertAll(id => id + 1000);
            }
            foreach (Load load in right.Loads)
            {
                load.Id += 700;
                if (load is PointLoad point)
                    point.NodeNumber += 1000;
                if (load is LinearLoad linear)
                {
                    linear.StartNode += 1000;
                    linear.EndNode += 1000;
                    if (linear.BarId.HasValue)
                        linear.BarId += 500;
                }
                if (load is AreaLoad area)
                {
                    if (area.PlateId.HasValue)
                        area.PlateId += 500;
                    if (area.NodeSequence is not null)
                        area.NodeSequence = area.NodeSequence.ConvertAll(id => id + 1000);
                }
                if (load is TemperatureLoad temperature)
                    temperature.ElementIds = temperature.ElementIds.ConvertAll(id => id + 500);
            }
            foreach (Support support in right.Supports)
            {
                support.NodeIds = support.NodeIds.ConvertAll(id => id + 1000);
                if (support.PlateId.HasValue)
                    support.PlateId += 500;
            }
            foreach (Hinge hinge in right.Hinges)
            {
                hinge.NodeIds = hinge.NodeIds.ConvertAll(id => id + 1000);
                hinge.ElementId += 500;
                if (hinge.EdgeStartNodeId.HasValue)
                    hinge.EdgeStartNodeId += 1000;
                if (hinge.EdgeEndNodeId.HasValue)
                    hinge.EdgeEndNodeId += 1000;
            }

            Assert.Empty(ModelDiff.Compare(left, right));
        }

        [Fact]
        public void ListOrder_IsNotADifference()
        {
            FemexModel left = Golden();
            FemexModel right = Clone(left);

            right.Nodes.Reverse();
            right.Bars.Reverse();
            right.Sections.Reverse();
            right.Materials.Reverse();
            right.Loads.Reverse();
            right.Supports.Reverse();

            Assert.Empty(ModelDiff.Compare(left, right));
        }

        [Fact]
        public void GeometryWithinTheCoincidenceTolerance_IsNotADifference()
        {
            FemexModel left = Golden();
            FemexModel right = Clone(left);

            double nudge = left.GetCoincidenceTolerance() * 0.4;
            foreach (Node node in right.Nodes)
                node.X += nudge;

            Assert.Empty(ModelDiff.Compare(left, right));
        }

        [Fact]
        public void Provenance_IsNotADifference()
        {
            // A round trip is supposed to change who wrote the file and when.
            FemexModel left = Golden();
            FemexModel right = Clone(left);
            right.Metadata = new FileMetadata("somebody else", "9.9", "another project", "2030-01-01T00:00:00Z");

            Assert.Empty(ModelDiff.Compare(left, right));
        }

        // ----- What is -----

        [Fact]
        public void GeometryBeyondTheTolerance_IsADifference()
        {
            FemexModel left = Golden();
            FemexModel right = Clone(left);

            right.Nodes[0].X += left.GetCoincidenceTolerance() * 100.0;

            ModelDifference difference = Assert.Single(ModelDiff.Compare(left, right));
            Assert.Equal(DifferenceKind.MemberDiffers, difference.Kind);
            Assert.Equal("Position", difference.Member);
            Assert.Equal(FemexEntity.Node, difference.Subject!.Value.Entity);
        }

        [Fact]
        public void ALoadMagnitude_IsExact_ByDefault()
        {
            // §7.2 grants tolerance to geometry and to nothing else. A diff that
            // quietly rounded load magnitudes together would be the confidently
            // wrong answer the product exists to catch.
            FemexModel left = Golden();
            FemexModel right = Clone(left);

            PointLoad load = right.Loads.OfType<PointLoad>().First();
            load.Fz *= 1.0000000001;

            Assert.NotEmpty(ModelDiff.Compare(left, right));
            Assert.Empty(ModelDiff.Compare(left, right, new ModelDiffOptions { RelativeTolerance = 1e-6 }));
        }

        [Fact]
        public void ARewiredBar_IsADifference_EvenWhenEveryIdMatches()
        {
            // The case the uid resolution exists for: the numbers are the same and
            // the structure is not.
            FemexModel left = Golden();
            FemexModel right = Clone(left);

            Bar bar = right.Bars[0];
            (bar.StartNodeId, bar.EndNodeId) = (bar.EndNodeId, bar.StartNodeId);

            ModelDifference[] differences = ModelDiff.Compare(left, right).ToArray();
            Assert.Contains(differences, d => d.Member == nameof(Bar.StartNodeId));
            Assert.Contains(differences, d => d.Member == nameof(Bar.EndNodeId));
        }

        [Fact]
        public void ARegionPriority_IsADifference()
        {
            // FEMEX's one lead over SAF, and a diff is where a reader sees it: two
            // models that agree everywhere except a priority are two different
            // structures, because the priority is what resolves the overlap.
            FemexModel left = Golden();
            FemexModel right = Clone(left);

            PlateRegion region = right.Plates.SelectMany(p => p.Regions).First();
            region.Priority += 1;

            ModelDifference difference = Assert.Single(ModelDiff.Compare(left, right));
            Assert.Equal(nameof(PlateRegion.Priority), difference.Member);
        }

        [Fact]
        public void AMissingObject_IsADifference_OnTheSideItIsMissingFrom()
        {
            FemexModel left = Golden();
            FemexModel right = Clone(left);
            right.Supports.RemoveAt(0);

            ModelDifference difference = Assert.Single(
                ModelDiff.Compare(left, right), d => d.Kind == DifferenceKind.OnlyInLeft);
            Assert.Equal(FemexEntity.Support, difference.Subject!.Value.Entity);
        }

        [Fact]
        public void AChangedSectionShape_IsATypeDifference()
        {
            FemexModel left = Golden();
            FemexModel right = Clone(left);

            Section original = right.Sections[0];
            right.Sections[0] = new GenericSection(original.Id, original.Name) { Uid = original.Uid };

            ModelDifference difference = Assert.Single(ModelDiff.Compare(left, right));
            Assert.Equal(DifferenceKind.TypeDiffers, difference.Kind);
        }

        [Fact]
        public void AnUnrecognisedMemberLostInTransit_IsADifference()
        {
            // IExtensible preserves a member this build has no property for, so a
            // crossing that drops it is a Stale loss — and this is what makes it
            // visible rather than a thing nobody notices until 1.9 ships.
            FemexModel left = FemexModel.FromJson(
                Golden().ToJson().Replace("\"schemaVersion\": \"1.8\",",
                                          "\"schemaVersion\": \"1.8\",\n  \"somethingNew\": 42,"));
            FemexModel right = Golden();

            ModelDifference difference = Assert.Single(ModelDiff.Compare(left, right));
            Assert.Equal("UnknownMembers[somethingNew]", difference.Member);
        }

        [Fact]
        public void TheUnitConvention_IsADifference_AndIsAnchoredToNothing()
        {
            // §3.3 keeps Units out of the entity vocabulary on purpose, so a
            // difference in it is about the model — the same null subject
            // TransferMessage.ModelLoss uses, so the two can be matched.
            FemexModel left = Golden();
            FemexModel right = Clone(left);
            right.Units!.Mass = null;

            ModelDifference difference = Assert.Single(ModelDiff.Compare(left, right));
            Assert.Null(difference.Subject);
            Assert.Equal("Units.Mass", difference.Member);
        }

        // ----- Uid coverage is a precondition, and it says so -----

        [Fact]
        public void ObjectsWithNoUid_AreReportedAsUncomparable_NotAsEqual()
        {
            FemexModel left = Golden();
            FemexModel right = Clone(left);

            foreach (var (entity, _, _) in right.EnumerateIdentified())
                entity.Uid = null;

            ModelDifference[] differences = ModelDiff.Compare(left, right).ToArray();

            Assert.Contains(differences, d => d.Kind == DifferenceKind.Unkeyed);
            Assert.Contains(differences, d => d.Kind == DifferenceKind.OnlyInLeft);

            // One per entity kind, not one per object: a partly-stamped model of a
            // thousand objects would otherwise bury every other message.
            Assert.Equal(differences.Count(d => d.Kind == DifferenceKind.Unkeyed),
                         differences.Where(d => d.Kind == DifferenceKind.Unkeyed)
                                    .Select(d => d.Subject!.Value.Entity).Distinct().Count());
        }

        [Fact]
        public void AreEquivalent_IsTheSameQuestion()
        {
            FemexModel model = Golden();
            Assert.True(ModelDiff.AreEquivalent(model, Clone(model)));

            FemexModel changed = Clone(model);
            changed.Materials[0].Density *= 2.0;
            Assert.False(ModelDiff.AreEquivalent(model, changed));
        }
    }
}
