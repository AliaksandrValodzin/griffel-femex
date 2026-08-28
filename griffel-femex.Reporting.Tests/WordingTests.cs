using System.Collections.Generic;

namespace griffel_femex.Reporting.Tests
{
    /// <summary>
    /// C6 and decision 9, made executable: <b>the word "certify" does not appear in
    /// any user-facing string</b> until the professional indemnity question in the
    /// business model's <i>Still open</i> has an answer. The report states findings
    /// and provenance; it does not offer an engineering opinion.
    ///
    /// A rule kept by discipline alone is a practice rather than a guarantee — the
    /// same argument A8 makes about the viewer's JavaScript mirror — and this one is
    /// the difference between a document that describes what a tool found and a
    /// document that says something an insurer would want to have been asked about.
    /// </summary>
    public class WordingTests
    {
        /// <summary>
        /// The stems, not the words: <i>certified</i>, <i>certification</i> and
        /// <i>certifies</i> are the same claim, and a check for the exact word
        /// "certify" would pass a report that used any of them.
        /// </summary>
        private static readonly string[] Forbidden =
        {
            "certif",     // certify, certified, certification, certificate
            "guarantee",
            "we confirm",
            "safe to use",
            "fit for purpose",
        };

        public static IEnumerable<object[]> EveryRendering()
        {
            AssuranceReport report = Reports.Everything();

            yield return new object[] { "html", HtmlReport.Render(report) };
            yield return new object[] { "json", JsonReport.Render(report) };
            yield return new object[] { "text", TextReport.Render(report, findings: true) };
            yield return new object[] { "index", ReportIndex.Render(new[] { ReportIndexEntry.From(report, "a.html") }) };
        }

        [Theory]
        [MemberData(nameof(EveryRendering))]
        public void NoRendering_OffersAnEngineeringOpinion(string which, string rendered)
        {
            string lowered = rendered.ToLowerInvariant();

            foreach (string word in Forbidden)
            {
                Assert.False(lowered.Contains(word),
                             $"the {which} rendering contains \"{word}\". Decision 9: the report states " +
                             "findings and provenance, and does not offer an engineering opinion.");
            }
        }

        /// <summary>
        /// And it says so, rather than leaving the reader to infer it. A document
        /// that merely avoids the claim can still be read as making it.
        /// </summary>
        [Fact]
        public void TheDocument_SaysWhatItIsNot()
        {
            string html = HtmlReport.Render(Reports.Everything());

            Assert.Contains("not an engineering opinion", html);
        }
    }
}
