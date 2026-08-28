using System.IO;
using System.Text.Json;

namespace griffel_femex.Cli.Tests
{
    /// <summary>
    /// The compare verb, and the exit codes — which are the part of a CLI that other
    /// software depends on and that nobody notices is wrong until a build has been
    /// green for a month.
    /// </summary>
    public class CompareAndUsageTests
    {
        /// <summary>
        /// <c>Conformance1.femex</c> rather than <c>Example1.femex</c>, and that is
        /// the point rather than a convenience: §7.2 says a model whose uid coverage
        /// is partial cannot be compared at all, and the golden fixture is the one
        /// with full coverage. A hand-authored model without uids reports
        /// <see cref="Comparison.DifferenceKind.Unkeyed"/> against itself — loudly,
        /// as designed.
        /// </summary>
        [Fact]
        public void AModelComparedWithItself_IsEquivalent()
        {
            Invocation run = Run.Femex("compare", Run.Example("Conformance1.femex"),
                                       Run.Example("Conformance1.femex"), "--format", "text");

            Assert.Equal(ExitCode.Clean, run.ExitCode);
            Assert.Contains("no differences", run.Output);
        }

        /// <summary>
        /// And a model with no uids says why it cannot be compared, rather than
        /// reporting that nothing changed.
        /// </summary>
        [Fact]
        public void AModelWithNoUids_SaysItCannotBeMatched()
        {
            Invocation run = Run.Femex("compare", Run.Example("Example1.femex"), Run.Example("Example1.femex"),
                                       "--format", "text");

            Assert.Equal(ExitCode.Findings, run.ExitCode);
            Assert.Contains("carry no uid", run.Output);
        }

        [Fact]
        public void AModelComparedWithADifferentOne_ExitsWithFindings()
        {
            string scratch = Run.Scratch();

            try
            {
                string baseline = Path.Combine(scratch, "baseline.femex");

                FemexModel model = FemexModel.Load(Run.Example("Conformance1.femex"));
                model.Nodes[0].X += 12.5;
                model.Save(baseline);

                Invocation run = Run.Femex("compare", Run.Example("Conformance1.femex"), baseline,
                                           "--format", "json");

                Assert.Equal(ExitCode.Findings, run.ExitCode);

                using JsonDocument document = JsonDocument.Parse(run.Output);
                JsonElement compare = document.RootElement.GetProperty("compare");

                Assert.True(compare.GetProperty("count").GetInt32() > 0);
                Assert.Equal("baseline.femex", compare.GetProperty("baseline").GetProperty("name").GetString());
            }
            finally
            {
                Run.Discard(scratch);
            }
        }

        [Fact]
        public void CompareNeedsTwoFiles()
        {
            Invocation run = Run.Femex("compare", Run.Example("Example1.femex"));

            Assert.Equal(ExitCode.ToolFailure, run.ExitCode);
            Assert.Contains("two files", run.Error);
        }

        /// <summary>
        /// An unreadable file on either side is a finding about that file — P4 —
        /// rather than a tool failure, and the report names which side it was.
        /// </summary>
        [Fact]
        public void AnUnreadableBaseline_IsAFinding()
        {
            string scratch = Run.Scratch();

            try
            {
                string baseline = Path.Combine(scratch, "broken.femex");
                File.WriteAllText(baseline, "{\"schemaVersion\":\"1.11\",\"units\":{\"forceUnit\":\"Poundal\"}}");

                Invocation run = Run.Femex("compare", Run.Example("Conformance1.femex"), baseline,
                                           "--format", "text");

                Assert.Equal(ExitCode.Findings, run.ExitCode);
                Assert.Contains("broken.femex", run.Output);
            }
            finally
            {
                Run.Discard(scratch);
            }
        }

        // ----- Usage, and the three exit codes -----

        [Fact]
        public void AFileThatIsNotThere_IsAToolFailure()
        {
            Invocation run = Run.Femex("check", "no-such-model.femex");

            Assert.Equal(ExitCode.ToolFailure, run.ExitCode);
            Assert.Contains("does not exist", run.Error);
        }

        [Fact]
        public void AVerbThatIsNot_IsAToolFailure()
        {
            Invocation run = Run.Femex("validate", "model.femex");

            Assert.Equal(ExitCode.ToolFailure, run.ExitCode);
            Assert.Contains("is not a verb", run.Error);
        }

        [Fact]
        public void AnOptionThatIsNot_IsAToolFailure()
        {
            Invocation run = Run.Femex("check", Run.Example("Example1.femex"), "--strict");

            Assert.Equal(ExitCode.ToolFailure, run.ExitCode);
            Assert.Contains("--strict", run.Error);
        }

        [Fact]
        public void AFormatThatIsNot_IsAToolFailure()
        {
            Invocation run = Run.Femex("check", Run.Example("Example1.femex"), "--format", "pdf");

            Assert.Equal(ExitCode.ToolFailure, run.ExitCode);
            Assert.Contains("is not a format", run.Error);
        }

        [Fact]
        public void AWildcardThatMatchesNothing_IsAToolFailure()
        {
            Invocation run = Run.Femex("check", Path.Combine(AppContext.BaseDirectory, "Examples", "*.dwg"));

            Assert.Equal(ExitCode.ToolFailure, run.ExitCode);
            Assert.Contains("No file matched", run.Error);
        }

        [Fact]
        public void HelpIsHelp_AndNoArgumentsIsAMistake()
        {
            Invocation help = Run.Femex("--help");
            Assert.Equal(ExitCode.Clean, help.ExitCode);
            Assert.Contains("femex check", help.Output);
            Assert.Contains("femex compare", help.Output);
            Assert.Contains("femex convert", help.Output);

            Invocation nothing = Run.Femex();
            Assert.Equal(ExitCode.ToolFailure, nothing.ExitCode);
        }

        [Fact]
        public void TheVersion_NamesTheBuildAndTheSchema()
        {
            Invocation run = Run.Femex("--version");

            Assert.Equal(ExitCode.Clean, run.ExitCode);
            Assert.Contains("femex", run.Output);
            Assert.Contains(FemexModel.CurrentSchemaVersion, run.Output);
        }

        /// <summary>
        /// C6 and decision 9 at the outermost surface: the usage text is user-facing
        /// too, and it says what the tool is not.
        /// </summary>
        [Fact]
        public void TheUsageText_OffersNoEngineeringOpinion()
        {
            Assert.DoesNotContain("certif", Cli.Usage.ToLowerInvariant());
            Assert.Contains("does not offer an engineering opinion", Cli.Usage);
        }
    }
}
