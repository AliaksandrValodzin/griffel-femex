using System.IO;
using System.Linq;
using System.Text.Json;
using griffel_femex.Comparison;

namespace griffel_femex.Cli.Tests
{
    /// <summary>
    /// C5's third verb, and C's stated proof for it: <c>femex convert</c> round-trips
    /// through SAF and back, and the FEMEX file it writes stands up to <b>the
    /// existing byte-identity assertion</b> — <c>File.ReadAllText(path)</c> equals
    /// <c>FemexModel.Load(path).ToJson()</c>.
    ///
    /// That is the assertion this repository has used since Phase A, and it is the
    /// right one here: a converted model that is not byte-identical to its own
    /// re-serialization is a file the tool cannot read back as what it wrote, and
    /// every downstream claim about it — the diff, the round-trip, the report —
    /// would be about a different model.
    /// </summary>
    public class ConvertTests
    {
        [Fact]
        public void AWorkbookConverts_ToAFemexFileThatIsItsOwnSerialization()
        {
            string scratch = Run.Scratch();

            try
            {
                string femex = Path.Combine(scratch, "hall.femex");

                Invocation run = Run.Femex("convert", Run.Corpus("SAF_example_STEEL_HALL_metrix_ZYX_210.xlsx"),
                                           "--to", femex, "--format", "text");

                Assert.NotEqual(ExitCode.ToolFailure, run.ExitCode);
                Assert.True(File.Exists(femex));

                Assert.Equal(File.ReadAllText(femex), FemexModel.Load(femex).ToJson());
                Assert.Equal(FemexModel.CurrentSchemaVersion, FemexModel.Load(femex).SchemaVersion);
            }
            finally
            {
                Run.Discard(scratch);
            }
        }

        /// <summary>
        /// SAF → FEMEX → SAF → FEMEX, through the CLI rather than through the
        /// adapter's own API — and the two FEMEX models are compared under §7.2
        /// equivalence. The adapter's suite already asserts that every difference is
        /// named by a message; what this asserts is that the <i>driver</i> loses
        /// nothing on top of that.
        /// </summary>
        [Fact]
        public void AWorkbookRoundTrips_ThroughTheToolAndBack()
        {
            string scratch = Run.Scratch();

            try
            {
                string first = Path.Combine(scratch, "first.femex");
                string workbook = Path.Combine(scratch, "second.xlsx");
                string second = Path.Combine(scratch, "second.femex");

                Assert.NotEqual(ExitCode.ToolFailure,
                                Run.Femex("convert", Run.Corpus("SAF_example_STEEL_HALL_metrix_ZYX_210.xlsx"),
                                          "--to", first).ExitCode);

                Assert.NotEqual(ExitCode.ToolFailure,
                                Run.Femex("convert", first, "--to", workbook).ExitCode);

                Assert.NotEqual(ExitCode.ToolFailure,
                                Run.Femex("convert", workbook, "--to", second).ExitCode);

                Assert.True(File.Exists(workbook));
                Assert.True(new FileInfo(workbook).Length > 0);

                // The steel-hall frame round-trips with zero differences through the
                // adapter, and it does through the tool.
                var differences = ModelDiff.Compare(FemexModel.Load(first), FemexModel.Load(second));
                Assert.True(differences.Count == 0,
                            string.Join(Environment.NewLine, differences.Take(10).Select(d => d.Text)));
            }
            finally
            {
                Run.Discard(scratch);
            }
        }

        /// <summary>
        /// A conversion always produces a report as well as a file. §4.3's warning is
        /// that from inside an adapter an invention does not feel like a loss — it
        /// feels like success — so the loss report is not something a caller can
        /// forget to ask for.
        /// </summary>
        [Fact]
        public void EveryConversion_ReportsWhatItCost()
        {
            string scratch = Run.Scratch();

            try
            {
                Invocation run = Run.Femex("convert", Run.Example("Example1.femex"),
                                           "--to", Path.Combine(scratch, "out.xlsx"), "--format", "json");

                Assert.NotEqual(ExitCode.ToolFailure, run.ExitCode);

                using JsonDocument document = JsonDocument.Parse(run.Output);
                JsonElement transfer = document.RootElement.GetProperty("transfer");

                Assert.Equal("FEMEX → SAF", transfer.GetProperty("route").GetString());
                Assert.Equal("Export", transfer.GetProperty("legs").EnumerateArray()
                                               .Single().GetProperty("direction").GetString());
                Assert.True(transfer.GetProperty("losses").GetInt32() > 0,
                            "FEMEX cannot write a workbook SAF's validator accepts without inventing " +
                            "something, and decision 12 says every invention is a message");

                // Both files are bound into the report: the model in, the workbook out.
                JsonElement sources = document.RootElement.GetProperty("provenance").GetProperty("sources");
                Assert.Equal(2, sources.GetArrayLength());
                foreach (JsonElement source in sources.EnumerateArray())
                    Assert.False(string.IsNullOrEmpty(source.GetProperty("sha256").GetString()));
            }
            finally
            {
                Run.Discard(scratch);
            }
        }

        /// <summary>
        /// Without <c>--to</c>, the converted model goes beside the reports — so one
        /// <c>--out</c> holds everything a run produced.
        /// </summary>
        [Fact]
        public void WithoutTo_TheConvertedModelGoesBesideTheReport()
        {
            string scratch = Run.Scratch();

            try
            {
                Run.Femex("convert", Run.Corpus("SAF_example_HOUSE_metric_ZYX_220.xlsx"), "--out", scratch);

                Assert.Single(Directory.GetFiles(scratch, "*.femex"));
                Assert.Single(Directory.GetFiles(scratch, "*.report.html"));
            }
            finally
            {
                Run.Discard(scratch);
            }
        }

        [Fact]
        public void ConvertingOverTheInput_IsRefused()
        {
            string scratch = Run.Scratch();

            try
            {
                string path = Path.Combine(scratch, "model.femex");
                File.Copy(Run.Example("Example1.femex"), path);

                Invocation run = Run.Femex("convert", path, "--to", path);

                Assert.Equal(ExitCode.ToolFailure, run.ExitCode);
                Assert.Contains("over itself", run.Error);
            }
            finally
            {
                Run.Discard(scratch);
            }
        }

        [Fact]
        public void ToWithSeveralInputs_IsRefusedRatherThanOverwritten()
        {
            Invocation run = Run.Femex("convert", Run.Example("Example1.femex"), Run.Example("Example2.femex"),
                                       "--to", "one.xlsx");

            Assert.Equal(ExitCode.ToolFailure, run.ExitCode);
            Assert.Contains("--to names one file", run.Error);
        }
    }
}
