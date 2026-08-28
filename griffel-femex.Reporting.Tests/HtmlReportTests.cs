using System.Linq;
using System.Text.RegularExpressions;

namespace griffel_femex.Reporting.Tests
{
    /// <summary>
    /// C2's founding property, asserted rather than intended: <b>one self-contained
    /// HTML file — no dependencies, no build step, opens from <c>file://</c>,
    /// survives being emailed, still opens in five years.</b>
    ///
    /// The test that matters is <see cref="TheDocument_FetchesNothing"/>. Every other
    /// property of this report can be fixed later; a report that needs the network
    /// is worthless the day the network is not there, which for a document filed
    /// against a project is most of the days it exists.
    /// </summary>
    public class HtmlReportTests
    {
        /// <summary>
        /// Anything a browser would go and fetch. Deliberately looser than a search
        /// for <c>http</c>: a protocol-relative <c>//cdn…</c> or a bare
        /// <c>src="style.css"</c> beside the file are both ways to break the same
        /// promise, and neither contains the string "http".
        /// </summary>
        private static readonly Regex[] Fetches =
        {
            new Regex("<script", RegexOptions.IgnoreCase),
            new Regex("<link", RegexOptions.IgnoreCase),
            new Regex("<img", RegexOptions.IgnoreCase),
            new Regex("<iframe", RegexOptions.IgnoreCase),
            new Regex(" src=", RegexOptions.IgnoreCase),
            new Regex("@import", RegexOptions.IgnoreCase),
            new Regex("url\\(", RegexOptions.IgnoreCase),
            new Regex("https?:", RegexOptions.IgnoreCase),
            new Regex("//[a-z0-9-]+\\.[a-z]{2,}", RegexOptions.IgnoreCase),
        };

        [Fact]
        public void TheDocument_FetchesNothing()
        {
            string html = HtmlReport.Render(Reports.Everything());

            foreach (Regex fetch in Fetches)
            {
                Assert.False(fetch.IsMatch(html),
                             $"the report matched {fetch} — a report that fetches anything is a report " +
                             "that stops working when the thing it fetches moves");
            }
        }

        [Fact]
        public void TheDocument_IsWellFormedEnoughToOpen()
        {
            string html = HtmlReport.Render(Reports.Everything());

            Assert.StartsWith("<!DOCTYPE html>", html);
            Assert.Contains("<meta charset=\"utf-8\">", html);
            Assert.Contains("<title>", html);
            Assert.EndsWith("</html>\n", html);
            Assert.Equal(CountOf(html, "<table"), CountOf(html, "</table>"));
            Assert.Equal(CountOf(html, "<section"), CountOf(html, "</section>"));
        }

        [Fact]
        public void EverySection_IsPresent_WhenThereIsOne()
        {
            string html = HtmlReport.Render(Reports.Everything());

            Assert.Contains("id=\"check\"", html);
            Assert.Contains("id=\"compare\"", html);
            Assert.Contains("id=\"transfer\"", html);
            Assert.Contains("id=\"provenance\"", html);
        }

        [Fact]
        public void ASectionThatWasNotRun_IsAbsent_RatherThanEmpty()
        {
            var report = new AssuranceReport(
                new ReportProvenance(null, Reports.Timestamp),
                new CheckSection(Reports.WithFindings().Validate()));

            string html = HtmlReport.Render(report);

            Assert.Contains("id=\"check\"", html);
            Assert.DoesNotContain("id=\"compare\"", html);
            Assert.DoesNotContain("id=\"transfer\"", html);
        }

        [Fact]
        public void EveryFinding_ReachesTheDocument()
        {
            AssuranceReport report = Reports.Everything();
            string html = HtmlReport.Render(report);

            foreach (ValidationMessage finding in report.Check!.Findings)
                Assert.Contains(HtmlReport.Escape(finding.Text), html);

            foreach (var difference in report.Compare!.Differences)
                Assert.Contains(HtmlReport.Escape(difference.Text), html);

            foreach (TransferLeg leg in report.Transfer!.Legs)
            {
                foreach (var message in leg.Messages)
                    Assert.Contains(HtmlReport.Escape(message.Text), html);
            }
        }

        /// <summary>
        /// Model data reaches this document — a load case label somebody typed, a
        /// profile name out of a catalogue — and a report whose markup depended on
        /// what a user called a load case would be a report that could be broken, or
        /// steered, by the file it is about.
        /// </summary>
        [Fact]
        public void ModelText_CannotEscapeIntoTheMarkup()
        {
            var report = new AssuranceReport(
                new ReportProvenance(new[] { new SourceFile("<b>evil</b>.femex") }, Reports.Timestamp),
                new CheckSection(new[]
                {
                    ValidationMessage.Warning("Load case 3 is labelled \"<script>alert('x')</script>\" & is odd.",
                                              ValidationCategory.Judgement),
                }),
                transfer: new TransferSection("SAF → FEMEX", Reports.ImportLeg()));

            string html = HtmlReport.Render(report);

            Assert.DoesNotContain("<script>alert", html);
            Assert.DoesNotContain("<b>evil</b>", html);
            Assert.Contains("&lt;script&gt;", html);
            Assert.Contains("&lt;b&gt;evil&lt;/b&gt;.femex", html);

            // The native handle is model data too, and it lands in a table cell.
            Assert.Contains("CS7 &lt;&quot;quoted&quot; &amp; unsafe&gt;", html);
        }

        /// <summary>
        /// The same report rendered twice is the same document. A deliverable that
        /// differed between two runs over the same inputs could not be filed against
        /// anything.
        /// </summary>
        [Fact]
        public void TheSameReport_RendersIdentically()
        {
            AssuranceReport report = Reports.Everything();

            Assert.Equal(HtmlReport.Render(report), HtmlReport.Render(report));
        }

        [Fact]
        public void TheHeadline_NamesTheModel_TheDate_TheBuild_AndTheHash()
        {
            AssuranceReport report = Reports.Everything();
            string headline = report.Headline();

            Assert.Contains("steel-hall.femex", headline);
            Assert.Contains("2026-08-28", headline);
            Assert.Contains(ReportTool.Name, headline);
            Assert.Contains(ReportTool.Version, headline);
            Assert.Contains("sha256 " + report.Provenance.Subject!.ShortHash, headline);
        }

        private static int CountOf(string text, string needle)
        {
            int count = 0;
            int at = 0;

            while ((at = text.IndexOf(needle, at, StringComparison.Ordinal)) >= 0)
            {
                count++;
                at += needle.Length;
            }

            return count;
        }
    }
}
