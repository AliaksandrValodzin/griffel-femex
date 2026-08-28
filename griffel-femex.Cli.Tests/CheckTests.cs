using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace griffel_femex.Cli.Tests
{
    /// <summary>
    /// C's stated proof for the check verb: <b>on <c>Examples/Example1.femex</c>,
    /// <c>femex check</c> produces a report whose findings match
    /// <c>Validate()</c></b> — and, per C4 and P4, a <c>.femex</c> carrying an
    /// unrecognised enum value exits <b>1</b>, not 2.
    /// </summary>
    public class CheckTests
    {
        public static IEnumerable<object[]> EveryExample()
        {
            foreach (string path in Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "Examples"), "*.femex"))
                yield return new object[] { Path.GetFileName(path) };
        }

        /// <summary>
        /// Decision 8 at the outer boundary: the report a client is handed is
        /// <c>Validate()</c>, message for message, in the engine's own order. Every
        /// example, not one — the fixture that happens to be clean would prove
        /// nothing about a fixture that is not.
        /// </summary>
        [Theory]
        [MemberData(nameof(EveryExample))]
        public void TheReportedFindings_AreExactlyWhatValidateSaid(string name)
        {
            string path = Run.Example(name);

            Invocation run = Run.Femex("check", path, "--format", "json");
            Assert.NotEqual(2, run.ExitCode);

            List<string> expected = FemexModel.Load(path).Validate().Select(m => m.Text).ToList();

            using JsonDocument document = JsonDocument.Parse(run.Output);
            List<string> reported = document.RootElement
                .GetProperty("check").GetProperty("findings").EnumerateArray()
                .Select(finding => finding.GetProperty("text").GetString()!)
                .ToList();

            Assert.Equal(expected, reported);
            Assert.Equal(expected.Count == 0 ? 0 : 1, run.ExitCode);
        }

        [Fact]
        public void AModelWithNothingWrong_ExitsClean()
        {
            Invocation run = Run.Femex("check", Run.Example("Example1.femex"), "--format", "text");

            Assert.Equal(ExitCode.Clean, run.ExitCode);
            Assert.Contains("no findings", run.Output);
            Assert.Contains("sha256", run.Output);
        }

        /// <summary>
        /// P4, which the plan has carried as open since Phase 0 and which C4 answers
        /// for the driver whatever the format eventually decides: every enum in the
        /// library throws on an unrecognised value, so a file from a later schema is
        /// unreadable — and it must arrive as a finding rather than as a stack
        /// trace, because a batch run over forty client models cannot stop at the
        /// seventh.
        /// </summary>
        [Fact]
        public void AFileThisBuildCannotRead_IsAFinding_NotACrash()
        {
            string scratch = Run.Scratch();

            try
            {
                string path = Path.Combine(scratch, "later.femex");
                File.WriteAllText(path, "{\"schemaVersion\":\"1.11\",\"units\":{\"lengthUnit\":\"Furlong\"}}");

                Invocation run = Run.Femex("check", path, "--format", "text");

                Assert.Equal(ExitCode.Findings, run.ExitCode);
                Assert.Contains("later.femex", run.Output);
                Assert.Contains("could not be read", run.Output);
                Assert.Empty(run.Error);
            }
            finally
            {
                Run.Discard(scratch);
            }
        }

        [Fact]
        public void AFileThatIsNotFemexAtAll_IsAlsoAFinding()
        {
            string scratch = Run.Scratch();

            try
            {
                string path = Path.Combine(scratch, "notes.femex");
                File.WriteAllText(path, "these are my notes, not a model");

                Invocation run = Run.Femex("check", path);

                Assert.Equal(ExitCode.Findings, run.ExitCode);
            }
            finally
            {
                Run.Discard(scratch);
            }
        }

        /// <summary>
        /// A workbook is a legitimate input to <c>check</c>: it is imported first,
        /// and the report then says both what the crossing cost and what the
        /// resulting model looks like — which is the only way to tell a finding
        /// about the structure from a finding about what SAF could not carry.
        /// </summary>
        [Fact]
        public void AWorkbook_IsCheckedThroughTheAdapter_AndTheCrossingIsReported()
        {
            Invocation run = Run.Femex("check", Run.Corpus("SAF_example_STEEL_HALL_metrix_ZYX_210.xlsx"),
                                       "--format", "json");

            Assert.NotEqual(ExitCode.ToolFailure, run.ExitCode);

            using JsonDocument document = JsonDocument.Parse(run.Output);
            JsonElement root = document.RootElement;

            Assert.Equal("SAF → FEMEX", root.GetProperty("transfer").GetProperty("route").GetString());
            Assert.True(root.GetProperty("transfer").GetProperty("losses").GetInt32() > 0,
                        "every SAF import synthesises levels, and that is an Invented message");
            Assert.True(root.GetProperty("check").GetProperty("count").GetInt32() >= 0);
        }
    }
}
