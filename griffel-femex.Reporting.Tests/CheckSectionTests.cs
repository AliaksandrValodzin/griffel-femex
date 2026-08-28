using System.Collections.Generic;
using System.Linq;

namespace griffel_femex.Reporting.Tests
{
    /// <summary>
    /// The Check section is <c>Validate()</c> and nothing else — decision 8, made
    /// executable. Any report that is sold or handed to a client is produced by the
    /// C# engine, so the section must not add a finding, drop one, reword one, or
    /// reorder the set.
    /// </summary>
    public class CheckSectionTests
    {
        [Fact]
        public void TheSection_IsExactlyWhatTheEngineSaid()
        {
            FemexModel model = Reports.WithFindings();
            List<ValidationMessage> expected = model.Validate().ToList();

            var section = new CheckSection(model.Validate());

            Assert.Equal(expected.Count, section.Count);
            Assert.Equal(expected.Select(m => m.Text), section.Findings.Select(m => m.Text));
            Assert.Equal(expected.Select(m => m.Severity), section.Findings.Select(m => m.Severity));
        }

        [Fact]
        public void TheCounts_AddUp()
        {
            var section = new CheckSection(Reports.WithFindings().Validate());

            Assert.Equal(section.Count, section.ErrorCount + section.WarningCount);
            Assert.Equal(section.Count,
                         section.CountOf(ValidationCategory.Judgement) +
                         section.CountOf(ValidationCategory.Referential) +
                         section.CountOf(ValidationCategory.Provenance));
        }

        /// <summary>
        /// The reason the category exists: §4 of the business model says the
        /// judgement half is the product and the referential half is table stakes,
        /// and this fixture is built to have both at once.
        /// </summary>
        [Fact]
        public void TheThreeHalves_AreAllPresent_AndJudgementLeads()
        {
            var section = new CheckSection(Reports.WithFindings().Validate());

            Assert.True(section.CountOf(ValidationCategory.Judgement) > 0,
                        "the fixture's two coincident nodes are a judgement finding");
            Assert.True(section.CountOf(ValidationCategory.Referential) > 0,
                        "the fixture's bar references a section that does not exist");
            Assert.True(section.CountOf(ValidationCategory.Provenance) > 0,
                        "the fixture declares schemaVersion 1.99");

            Assert.Equal(ValidationCategory.Judgement, section.Categories[0]);
        }

        [Fact]
        public void ACategoryWithNothingInIt_IsNotAHeading()
        {
            var section = new CheckSection(new[]
            {
                ValidationMessage.Warning("nothing to see", ValidationCategory.Judgement),
            });

            Assert.Equal(new[] { ValidationCategory.Judgement }, section.Categories);
        }

        [Fact]
        public void AModelWithNothingWrong_SaysSo()
        {
            var section = new CheckSection(FemexModel.Load(Reports.Example("Example1.femex")).Validate());

            Assert.True(section.IsClean);
            Assert.Equal("no findings", section.Summary());
        }

        /// <summary>
        /// P4 and C4: a file this build cannot read is a finding about the file, and
        /// it is anchored in the category that is about files.
        /// </summary>
        [Fact]
        public void AnUnreadableFile_IsAProvenanceError_NotACrash()
        {
            CheckSection section = CheckSection.Unreadable("later.femex", "\"lengthUnit\": \"Furlong\"");

            ValidationMessage finding = Assert.Single(section.Findings);
            Assert.Equal(ValidationSeverity.Error, finding.Severity);
            Assert.Equal(ValidationCategory.Provenance, finding.Category);
            Assert.Contains("later.femex", finding.Text);
            Assert.Contains("Furlong", finding.Text);
            Assert.False(section.IsClean);
        }
    }
}
