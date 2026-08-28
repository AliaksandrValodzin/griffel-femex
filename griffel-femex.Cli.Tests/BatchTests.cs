using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace griffel_femex.Cli.Tests
{
    /// <summary>
    /// C4: <c>femex check *.femex --out reports/</c> produces N reports and one
    /// summary index — <b>and it is proven on the published SAF corpus</b>, because
    /// a batch driver proven on four hand-written examples is not proven on
    /// anything a client would send.
    ///
    /// This is also where the wildcard matters. C4's own example is
    /// <c>*.femex</c>, and the shell this repository is developed and used on does
    /// not expand it, so the tool does.
    /// </summary>
    public class BatchTests
    {
        [Fact]
        public void EveryWorkbookInTheCorpus_IsCheckedInOneRun()
        {
            string scratch = Run.Scratch();

            try
            {
                Invocation run = Run.Femex("check", Path.Combine(Run.CorpusDirectory, "*.xlsx"), "--out", scratch);

                Assert.NotEqual(ExitCode.ToolFailure, run.ExitCode);

                int workbooks = Run.CorpusFiles.Count;
                Assert.Equal(11, workbooks);

                string[] reports = Directory.GetFiles(scratch, "*.report.html");
                Assert.Equal(workbooks, reports.Length);

                string index = Path.Combine(scratch, "index.html");
                Assert.True(File.Exists(index), "a batch run of more than one model produces one index");

                string html = File.ReadAllText(index);
                foreach (string workbook in Run.CorpusFiles)
                {
                    string name = Path.GetFileNameWithoutExtension(workbook);
                    Assert.Contains(name + ".report.html", html);
                }

                Assert.Contains(workbooks + " models", html);
            }
            finally
            {
                Run.Discard(scratch);
            }
        }

        [Fact]
        public void TheExamples_AreCheckedByWildcard()
        {
            string scratch = Run.Scratch();

            try
            {
                string pattern = Path.Combine(AppContext.BaseDirectory, "Examples", "*.femex");
                int examples = Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "Examples"), "*.femex").Length;

                Invocation run = Run.Femex("check", pattern, "--out", scratch);

                Assert.NotEqual(ExitCode.ToolFailure, run.ExitCode);
                Assert.Equal(examples, Directory.GetFiles(scratch, "*.report.html").Length);
                Assert.True(File.Exists(Path.Combine(scratch, "index.html")));
            }
            finally
            {
                Run.Discard(scratch);
            }
        }

        /// <summary>
        /// One model needs no index. A folder holding one report and an index that
        /// points only at it is a folder with a redundant file in it.
        /// </summary>
        [Fact]
        public void OneModel_ProducesNoIndex()
        {
            string scratch = Run.Scratch();

            try
            {
                Run.Femex("check", Run.Example("Example1.femex"), "--out", scratch);

                Assert.Single(Directory.GetFiles(scratch, "*.report.html"));
                Assert.False(File.Exists(Path.Combine(scratch, "index.html")));
            }
            finally
            {
                Run.Discard(scratch);
            }
        }

        /// <summary>
        /// The artefact this phase actually sells, checked where it is sold: on
        /// disk, opened from <c>file://</c>, with nothing to fetch. Asserted on the
        /// file the CLI wrote rather than on a string the report layer returned,
        /// because those are two different claims.
        /// </summary>
        [Fact]
        public void TheWrittenReport_OpensWithTheNetworkDisabled()
        {
            string scratch = Run.Scratch();

            try
            {
                Run.Femex("check", Run.Corpus("SAF_example_HOUSE_metric_ZYX_220.xlsx"), "--out", scratch);

                string report = Directory.GetFiles(scratch, "*.report.html").Single();
                string html = File.ReadAllText(report);

                Assert.StartsWith("<!DOCTYPE html>", html);
                Assert.DoesNotMatch(new Regex("https?:", RegexOptions.IgnoreCase), html);
                Assert.DoesNotMatch(new Regex("<script|<link|<img| src=|@import", RegexOptions.IgnoreCase), html);

                // And it says something: an empty document fetches nothing either.
                Assert.Contains("Model Assurance Report", html);
                Assert.Contains("id=\"provenance\"", html);
                Assert.True(html.Length > 4000, "the house has plenty to report");
            }
            finally
            {
                Run.Discard(scratch);
            }
        }

        [Fact]
        public void TheJsonBatch_WritesJsonReportsAndAJsonIndex()
        {
            string scratch = Run.Scratch();

            try
            {
                string pattern = Path.Combine(AppContext.BaseDirectory, "Examples", "*.femex");

                Run.Femex("check", pattern, "--out", scratch, "--format", "json");

                Assert.NotEmpty(Directory.GetFiles(scratch, "*.report.json"));
                Assert.True(File.Exists(Path.Combine(scratch, "index.json")));
                Assert.Empty(Directory.GetFiles(scratch, "*.html"));
            }
            finally
            {
                Run.Discard(scratch);
            }
        }
    }
}
