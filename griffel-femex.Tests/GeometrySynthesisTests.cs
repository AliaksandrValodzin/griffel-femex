using griffel_femex.Geometry;
using griffel_femex.Interop;
using griffel_femex.Synthesis;
using Xunit;

namespace griffel_femex.Tests
{
    /// <summary>
    /// The level-clustering helper of <c>FEMEX_Adapters.md</c> §6.1 and the
    /// two-phase rule of §6.2 — the piece of implied code the contract counted and
    /// named and then had to admit did not exist.
    ///
    /// The bug these remove is one that would otherwise be rediscovered by every
    /// plugin author separately. <c>GetCoincidenceTolerance</c> is 1e-6 of the
    /// model's <i>current</i> bounding diagonal, so an import that starts from an
    /// empty model begins at the 1e-9 floor and grows as the model fills: the first
    /// nodes are matched against a far tighter test than the last, and the same
    /// native model read in a different order yields a different node table. That is
    /// fatal to §7.2's equivalence and therefore to every conformance test built on
    /// it — and it fails as a wrong answer, not as an exception.
    /// </summary>
    public class GeometrySynthesisTests
    {
        private static FemexModel Empty() => new FemexModel { SchemaVersion = FemexModel.CurrentSchemaVersion };

        // ----- §6.2, the rule the whole class exists for -----

        [Fact]
        public void SamePoints_InAnyOrder_GiveTheSameNodeTable()
        {
            double[][] points =
            {
                new[] { 0.0, 0.0, 0.0 },
                new[] { 6.0, 0.0, 0.0 },
                new[] { 6.0, 4.0, 3.5 },
                new[] { 0.0, 4.0, 3.5 },
                new[] { 0.0, 0.0, 7.0 },
            };

            FemexModel forward = Empty();
            var first = new GeometrySynthesis();
            foreach (double[] point in points)
                first.AddPoint(point[0], point[1], point[2]);
            first.Build(forward);

            FemexModel backward = Empty();
            var second = new GeometrySynthesis();
            for (int i = points.Length - 1; i >= 0; i--)
                second.AddPoint(points[i][0], points[i][1], points[i][2]);
            second.Build(backward);

            Assert.Equal(Render(forward), Render(backward));
        }

        [Fact]
        public void TheTolerance_IsDerivedOnce_NotAsTheModelGrows()
        {
            // Two points a hair closer together than 1e-6 of the finished diagonal.
            // Added first they would meet the 1e-9 floor and stay apart; added last
            // they would meet the full tolerance and merge. Two-phase synthesis
            // means the answer does not depend on which.
            const double span = 100.0;
            double gap = 1e-6 * span * 0.5;

            FemexModel near = Empty();
            var first = new GeometrySynthesis();
            first.AddPoint(0.0, 0.0, 0.0);
            first.AddPoint(gap, 0.0, 0.0);
            first.AddPoint(span, 0.0, 0.0);
            first.Build(near);

            FemexModel far = Empty();
            var second = new GeometrySynthesis();
            second.AddPoint(span, 0.0, 0.0);
            second.AddPoint(gap, 0.0, 0.0);
            second.AddPoint(0.0, 0.0, 0.0);
            second.Build(far);

            Assert.Equal(2, near.Nodes.Count);
            Assert.Equal(Render(near), Render(far));
        }

        [Fact]
        public void CoincidentPoints_ShareOneNode()
        {
            FemexModel model = Empty();
            var synthesis = new GeometrySynthesis();

            int a = synthesis.AddPoint(3.0, 2.0, 0.0);
            int b = synthesis.AddPoint(3.0, 2.0, 0.0);
            synthesis.AddPoint(9.0, 2.0, 0.0);

            SynthesisResult result = synthesis.Build(model);

            Assert.Same(result.NodeFor(a), result.NodeFor(b));
            Assert.Equal(2, model.Nodes.Count);
        }

        // ----- §6.1, the level policy -----

        [Fact]
        public void AnElevationWithNoLevel_InventsOne_AndSaysSo()
        {
            FemexModel model = Empty();
            var synthesis = new GeometrySynthesis();
            synthesis.AddPoint(0.0, 0.0, 4.2);

            SynthesisResult result = synthesis.Build(model);

            Level invented = Assert.Single(result.InventedLevels);
            Assert.Equal(4.2, invented.AbsoluteElevation);

            TransferMessage message = Assert.Single(result.Messages);
            Assert.Equal(LossCategory.Invented, message.Category);
            Assert.Equal(ValidationSeverity.Warning, message.Severity);
            Assert.Equal(FemexEntity.Level, message.Subject!.Value.Entity);
        }

        [Fact]
        public void AnElevationNearAnExistingLevel_SnapsToIt_AndInventsNothing()
        {
            FemexModel model = Empty();
            model.Levels.Add(new Level(0, "Ground", 0.0, 0.0, isGround: true));

            var synthesis = new GeometrySynthesis();
            // Well inside 1e-6 of the 10 m vertical extent this model ends up with.
            int ticket = synthesis.AddPoint(0.0, 0.0, 1e-9);
            synthesis.AddPoint(0.0, 0.0, 10.0);

            SynthesisResult result = synthesis.Build(model);

            Assert.Equal(0, result.NodeFor(ticket).LevelNumber);
            Assert.Single(result.InventedLevels);   // the 10 m one, not the ground
        }

        [Fact]
        public void ADeclaredLevel_IsNeverReportedAsInvented()
        {
            FemexModel model = Empty();
            var synthesis = new GeometrySynthesis();

            int ticket = synthesis.AddLevel(3.5, "First floor");
            synthesis.AddPoint(0.0, 0.0, 3.5);

            SynthesisResult result = synthesis.Build(model);

            Assert.Equal("First floor", result.LevelFor(ticket).Name);
            Assert.Empty(result.InventedLevels);
            Assert.Empty(result.Messages);
        }

        [Fact]
        public void AnInventedLevel_IsNamedSoItCanBeQuestioned()
        {
            FemexModel model = Empty();
            var synthesis = new GeometrySynthesis();
            synthesis.AddPoint(0.0, 0.0, 3.5);
            synthesis.AddPoint(0.0, 0.0, -1.25);
            synthesis.Build(model);

            Assert.Contains(model.Levels, l => l.Name == "Level +3.500");
            Assert.Contains(model.Levels, l => l.Name == "Level -1.250");
        }

        // ----- Building twice is the bug, so it is refused -----

        [Fact]
        public void CollectingAfterBuilding_Throws()
        {
            var synthesis = new GeometrySynthesis();
            synthesis.AddPoint(0.0, 0.0, 0.0);
            synthesis.Build(Empty());

            Assert.Throws<InvalidOperationException>(() => synthesis.AddPoint(1.0, 0.0, 0.0));
            Assert.Throws<InvalidOperationException>(() => synthesis.Build(Empty()));
        }

        [Fact]
        public void ExistingGeometry_IsJoinedOnTo_NotDuplicated()
        {
            FemexModel model = Empty();
            model.Levels.Add(new Level(0, "Ground", 0.0, 0.0, isGround: true));
            model.Nodes.Add(new Node(1, 5.0, 5.0, 0));

            var synthesis = new GeometrySynthesis();
            int ticket = synthesis.AddPoint(5.0, 5.0, 0.0);
            synthesis.AddPoint(50.0, 5.0, 0.0);
            SynthesisResult result = synthesis.Build(model);

            Assert.Equal(1, result.NodeFor(ticket).NodeNumber);
            Assert.Equal(2, model.Nodes.Count);
        }

        private static string Render(FemexModel model)
        {
            var rows = new List<string>();
            foreach (Level level in model.Levels)
                rows.Add($"L{level.LevelNumber} @ {level.AbsoluteElevation}");
            foreach (Node node in model.Nodes)
                rows.Add($"N{node.NodeNumber} @ {node.X},{node.Y},{node.LevelNumber}+{node.VerticalOffset}");

            rows.Sort(StringComparer.Ordinal);
            return string.Join("\n", rows);
        }
    }
}
