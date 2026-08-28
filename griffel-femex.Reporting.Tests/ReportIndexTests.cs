using System.Linq;
using System.Text.Json;

namespace griffel_femex.Reporting.Tests
{
    /// <summary>
    /// C4's index: what a migration engagement hands back beside forty reports.
    /// </summary>
    public class ReportIndexTests
    {
        [Fact]
        public void TheIndex_SaysWhatEachReportSays()
        {
            AssuranceReport report = Reports.Everything();
            ReportIndexEntry entry = ReportIndexEntry.From(report, "steel-hall.report.html");

            Assert.Equal(report.SubjectName, entry.Name);
            Assert.Equal(report.IsClean, entry.Clean);
            Assert.Equal(report.Provenance.Subject!.Sha256, entry.Sha256);

            foreach (ReportSummaryRow row in report.Summary())
                Assert.Contains(row.Detail, entry.Summary);
        }

        /// <summary>
        /// Relative links, because the deliverable is the folder: an index whose
        /// rows pointed at <c>C:\work\clients\…</c> would be an index that works on
        /// exactly one machine.
        /// </summary>
        [Fact]
        public void TheIndex_LinksRelatively_AndFetchesNothing()
        {
            string html = ReportIndex.Render(new[]
            {
                ReportIndexEntry.From(Reports.Everything(), "steel-hall.report.html"),
                new ReportIndexEntry("clean.femex", "clean.report.html", true, "Check no findings"),
            });

            Assert.Contains("href=\"steel-hall.report.html\"", html);
            Assert.DoesNotContain("http", html);
            Assert.DoesNotContain("<script", html);
            Assert.Contains("2 models", html);
        }

        [Fact]
        public void TheIndexJson_ListsEveryModel()
        {
            string json = ReportIndex.RenderJson(new[]
            {
                ReportIndexEntry.From(Reports.Everything(), "steel-hall.report.json"),
                new ReportIndexEntry("clean.femex", "clean.report.json", true, "Check no findings"),
            });

            using JsonDocument document = JsonDocument.Parse(json);
            JsonElement models = document.RootElement.GetProperty("models");

            Assert.Equal(2, models.GetArrayLength());
            Assert.False(models.EnumerateArray().First().GetProperty("clean").GetBoolean());
            Assert.True(models.EnumerateArray().Last().GetProperty("clean").GetBoolean());
        }
    }
}
