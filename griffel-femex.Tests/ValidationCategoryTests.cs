using System.Linq;
using griffel_femex.BoundaryConditions;
using griffel_femex.Geometry;
using griffel_femex.Geometry.Sections;
using griffel_femex.Loads;
using griffel_femex.Materials;

namespace griffel_femex.Tests
{
    /// <summary>
    /// The split <c>FEMEX_BusinessModel.md</c> §4 makes when it audits this engine,
    /// asserted where the engine is: <b>half is referential integrity, which is
    /// table stakes, and half is engineering judgement, which is the product.</b>
    ///
    /// The reason this is in the library and not in the reporting layer is that a
    /// classifier living beside the report would be a second statement about which
    /// rule is which, free to disagree with the rules themselves. Decision 8 of
    /// <c>SAF_Adapter.md</c> settles it: the C# engine is authoritative.
    ///
    /// The checks named in the tests below are quoted from §4's own sample, which is
    /// what makes them the right ones to pin.
    /// </summary>
    public class ValidationCategoryTests
    {
        /// <summary>
        /// The two axes are independent. A model with two regions of equal priority
        /// and overlapping extents is an Error <i>and</i> a judgement finding, and a
        /// report that could only sort by severity would file it beside a dangling
        /// section reference.
        /// </summary>
        [Fact]
        public void SeverityAndCategory_AreIndependent()
        {
            FemexModel model = SampleModels.Build();

            // Gravity with no direction at all: §4 quotes it, and it is an Error.
            model.Gravity = new Gravity { Dx = 0.0, Dy = 0.0, Dz = 0.0 };

            ValidationMessage finding = model.Validate()
                .Single(m => m.Text.Contains("which is no direction at all") &&
                             m.Text.StartsWith("Gravity"));

            Assert.Equal(ValidationSeverity.Error, finding.Severity);
            Assert.Equal(ValidationCategory.Judgement, finding.Category);
        }

        /// <summary>
        /// A reference that does not resolve is the other half — the half an
        /// engineer will not pay for, because their own program would not have
        /// opened the file.
        /// </summary>
        [Fact]
        public void AReferenceThatDoesNotResolve_IsReferential()
        {
            var model = new FemexModel { SchemaVersion = FemexModel.CurrentSchemaVersion };
            model.Levels.Add(new Level(1, "Ground", 0.0, 0.0));
            model.Nodes.Add(new Node(1, 0.0, 0.0, 1));
            model.Nodes.Add(new Node(2, 3.0, 0.0, 1));
            model.Bars.Add(new Bar(1, 1, 2, 99, 99));

            var findings = model.Validate()
                                .Where(m => m.Text.Contains("references unknown"))
                                .ToList();

            Assert.NotEmpty(findings);
            Assert.All(findings, f => Assert.Equal(ValidationCategory.Referential, f.Category));
        }

        /// <summary>
        /// Two nodes in the same place is the check §4 leads with, and it is the
        /// canonical judgement finding: the model opens cleanly, solves, and is
        /// wrong.
        /// </summary>
        [Fact]
        public void CoincidentNodes_AreJudgement()
        {
            var model = new FemexModel { SchemaVersion = FemexModel.CurrentSchemaVersion };
            model.Levels.Add(new Level(1, "Ground", 0.0, 0.0));
            model.Nodes.Add(new Node(1, 0.0, 0.0, 1));
            model.Nodes.Add(new Node(2, 0.0, 0.0, 1));

            ValidationMessage finding = model.Validate()
                .Single(m => m.Text.Contains("are at the same location"));

            Assert.Equal(ValidationCategory.Judgement, finding.Category);
        }

        /// <summary>
        /// What the file says it is, what reading it changed, and how much of it a
        /// receiver can match: neither half of §4's audit describes these, because
        /// they are not findings about a structure at all.
        /// </summary>
        [Fact]
        public void WhatTheFileSaysAboutItself_IsProvenance()
        {
            var model = new FemexModel { SchemaVersion = "1.99" };
            model.Levels.Add(new Level(1, "Ground", 0.0, 0.0));
            model.Nodes.Add(new Node(1, 0.0, 0.0, 1));
            model.Sections.Add(new GenericSection { Id = 900, Properties = new SectionProperties { Area = 0.01 } });

            // Half the model stamped, which is the coverage state that is neither of
            // the two normal ones and the only one reported.
            model.Levels[0].Uid = Guid.NewGuid();

            var findings = model.Validate().ToList();

            Assert.Equal(ValidationCategory.Provenance,
                         findings.Single(m => m.Text.Contains("which this build does not")).Category);
            Assert.Equal(ValidationCategory.Provenance,
                         findings.Single(m => m.Text.Contains("authored objects carry a uid")).Category);
            // A name is what another program keys by, so a missing one is a
            // statement about what survives a crossing rather than about the
            // structure — which is the line the third category draws.
            Assert.Equal(ValidationCategory.Provenance,
                         findings.Single(m => m.Text.Contains("keys sections by name")).Category);
        }

        /// <summary>
        /// A section stating an area and no second moment is completeness — the
        /// model is legal, and a program reading it will get less than it needs.
        ///
        /// Note what this test does <b>not</b> claim: the same section also has no
        /// name, and that finding is <see cref="ValidationCategory.Provenance"/>,
        /// because a missing name is a statement about what survives a crossing
        /// rather than about the structure. The two live in one validator family
        /// each, which is why the categories are declared per family.
        /// </summary>
        [Fact]
        public void CompletenessChecks_AreJudgement()
        {
            FemexModel model = SampleModels.Build();
            model.Sections.Add(new GenericSection { Id = 900, Properties = new SectionProperties { Area = 0.01 } });

            var findings = model.Validate()
                                .Where(m => m.Text.StartsWith("Section 900") && m.Text.Contains("states an area"))
                                .ToList();

            Assert.NotEmpty(findings);
            Assert.All(findings, f => Assert.Equal(ValidationCategory.Judgement, f.Category));
        }

        /// <summary>
        /// Every finding states its half. There is no default, deliberately: a rule
        /// added later must say which half it belongs to, because §4's consequence
        /// for the roadmap is that new rules go into the judgement half
        /// <i>deliberately</i>.
        /// </summary>
        [Fact]
        public void EveryFinding_StatesItsHalf()
        {
            FemexModel model = SampleModels.Build();
            model.Gravity = new Gravity { Dx = 0.0, Dy = 0.0, Dz = 0.0 };
            model.SchemaVersion = "1.99";
            model.Bars.Add(new Bar(77, 1, 2, 404, 404));

            var findings = model.Validate().ToList();

            Assert.NotEmpty(findings);
            Assert.Equal(findings.Count,
                         findings.Count(f => f.Category == ValidationCategory.Referential) +
                         findings.Count(f => f.Category == ValidationCategory.Judgement) +
                         findings.Count(f => f.Category == ValidationCategory.Provenance));

            // And all three halves are present in one model, which is what makes the
            // distinction worth drawing at all.
            Assert.Contains(findings, f => f.Category == ValidationCategory.Referential);
            Assert.Contains(findings, f => f.Category == ValidationCategory.Judgement);
            Assert.Contains(findings, f => f.Category == ValidationCategory.Provenance);
        }

        [Fact]
        public void ValidateByCategory_FiltersTheSameSet()
        {
            FemexModel model = SampleModels.Build();
            model.SchemaVersion = "1.99";

            var all = model.Validate().ToList();

            foreach (ValidationCategory category in new[]
                     {
                         ValidationCategory.Referential,
                         ValidationCategory.Judgement,
                         ValidationCategory.Provenance,
                     })
            {
                Assert.Equal(all.Where(m => m.Category == category).Select(m => m.Text),
                             model.Validate(category).Select(m => m.Text));
            }
        }

        /// <summary>
        /// The category is orthogonal to severity, so filtering by one must not
        /// disturb the other.
        /// </summary>
        [Fact]
        public void ValidateBySeverity_IsUnchanged()
        {
            FemexModel model = SampleModels.Build();
            model.SchemaVersion = "1.99";

            Assert.Equal(model.Validate().Where(m => m.Severity == ValidationSeverity.Warning).Select(m => m.Text),
                         model.Validate(ValidationSeverity.Warning).Select(m => m.Text));
        }
    }
}
