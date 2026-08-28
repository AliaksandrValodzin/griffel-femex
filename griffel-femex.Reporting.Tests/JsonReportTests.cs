using System.Linq;
using System.Text.Json;

namespace griffel_femex.Reporting.Tests
{
    /// <summary>
    /// <c>--format json</c> is the same report, for something that is not a person.
    /// The property worth asserting is that it <b>is</b> the same report: a
    /// machine-readable form that disagreed with the document about the same model
    /// would be the confidently-incorrect failure class landed on the deliverable
    /// itself.
    /// </summary>
    public class JsonReportTests
    {
        [Fact]
        public void TheJson_ParsesAndCountsWhatTheReportCounts()
        {
            AssuranceReport report = Reports.Everything();

            using JsonDocument document = JsonDocument.Parse(JsonReport.Render(report));
            JsonElement root = document.RootElement;

            Assert.Equal(report.SubjectName, root.GetProperty("subject").GetString());
            Assert.Equal(report.IsClean, root.GetProperty("clean").GetBoolean());

            JsonElement check = root.GetProperty("check");
            Assert.Equal(report.Check!.Count, check.GetProperty("count").GetInt32());
            Assert.Equal(report.Check.ErrorCount, check.GetProperty("errors").GetInt32());
            Assert.Equal(report.Check.Count, check.GetProperty("findings").GetArrayLength());

            Assert.Equal(report.Compare!.Count, root.GetProperty("compare").GetProperty("count").GetInt32());
            Assert.Equal(report.Transfer!.LossCount, root.GetProperty("transfer").GetProperty("losses").GetInt32());
            Assert.Equal(2, root.GetProperty("transfer").GetProperty("legs").GetArrayLength());
        }

        /// <summary>
        /// The three claims each carry the anchor a consumer needs to act on them:
        /// a finding carries its category, a difference and a loss carry the object
        /// they are about — <c>ObjectRef</c>'s id <i>and</i> uid, since either alone
        /// leaves one of the two consumers unable to use the message.
        /// </summary>
        [Fact]
        public void EveryRow_CarriesItsAnchor()
        {
            using JsonDocument document = JsonDocument.Parse(JsonReport.Render(Reports.Everything()));
            JsonElement root = document.RootElement;

            foreach (JsonElement finding in root.GetProperty("check").GetProperty("findings").EnumerateArray())
            {
                Assert.False(string.IsNullOrEmpty(finding.GetProperty("category").GetString()));
                Assert.False(string.IsNullOrEmpty(finding.GetProperty("severity").GetString()));
            }

            JsonElement importLeg = root.GetProperty("transfer").GetProperty("legs").EnumerateArray().First();
            JsonElement level = importLeg.GetProperty("messages").EnumerateArray()
                                         .First(m => m.GetProperty("entity").ValueKind != JsonValueKind.Null &&
                                                     m.GetProperty("entity").GetString() == "Level");

            Assert.Equal("Invented", level.GetProperty("category").GetString());
            Assert.Equal(3, level.GetProperty("id").GetInt32());
            Assert.Equal("Storey <none>", level.GetProperty("native").GetString());
        }

        [Fact]
        public void TheSameReport_SerialisesIdentically()
        {
            AssuranceReport report = Reports.Everything();

            Assert.Equal(JsonReport.Render(report), JsonReport.Render(report));
        }

        /// <summary>
        /// A per-concept message — one saying "142 members carried a stiffness
        /// modifier" — is anchored to the entity kind with no id, and that shape has
        /// to survive into the JSON or a consumer cannot tell it from a message
        /// about one object.
        /// </summary>
        [Fact]
        public void APerConceptMessage_KeepsItsNullId()
        {
            using JsonDocument document = JsonDocument.Parse(JsonReport.Render(Reports.Everything()));

            JsonElement bars = document.RootElement
                .GetProperty("transfer").GetProperty("legs").EnumerateArray().First()
                .GetProperty("messages").EnumerateArray()
                .First(m => m.GetProperty("category").GetString() == "Unmapped");

            Assert.Equal("Bar", bars.GetProperty("entity").GetString());
            Assert.Equal(JsonValueKind.Null, bars.GetProperty("id").ValueKind);
        }
    }
}
