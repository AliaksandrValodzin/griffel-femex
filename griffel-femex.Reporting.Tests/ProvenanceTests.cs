using System.IO;
using System.Text;

namespace griffel_femex.Reporting.Tests
{
    /// <summary>
    /// C3: <b>provenance is a first-class section, not a footer.</b> The claim the
    /// business model calls auditable is <i>"these findings came from this model,
    /// this producer, this version, on this date"</i>, and every one of those five
    /// has to be in the document for the claim to be worth anything.
    /// </summary>
    public class ProvenanceTests
    {
        /// <summary>
        /// The hash is the whole of the binding, so it is worth pinning to a value
        /// computed by something that is not this code. This is the SHA-256 of the
        /// three bytes 01 02 03, which any other tool will agree with.
        /// </summary>
        [Fact]
        public void TheHash_IsOrdinarySha256()
        {
            Assert.Equal("039058c6f2c0cb492c533b0a4d14ef77cc0f78abccced5287d84a1a2011cfb81",
                         SourceFile.Hash(new byte[] { 1, 2, 3 }));
        }

        [Fact]
        public void AFileOnDisk_IsHashedAsItIs()
        {
            string path = Reports.Example("Example1.femex");

            SourceFile source = SourceFile.FromPath(path, FemexModel.Load(path));

            Assert.Equal("Example1.femex", source.Name);
            Assert.Equal(SourceFile.Hash(File.ReadAllBytes(path)), source.Sha256);
            Assert.Equal(new FileInfo(path).Length, source.ByteCount);
            Assert.Equal(FemexModel.CurrentSchemaVersion, source.SchemaVersion);
            Assert.Equal(8, source.ShortHash!.Length);
        }

        /// <summary>
        /// A model that never was a file says so. Hashing the serialization of an
        /// in-memory model would produce a number binding the report to nothing a
        /// reader could re-derive, which is worse than an honest blank.
        /// </summary>
        [Fact]
        public void AModelThatWasNeverAFile_HasNoHash()
        {
            SourceFile source = SourceFile.FromModel("in memory", Reports.WithFindings());

            Assert.Null(source.Sha256);
            Assert.Null(source.ByteCount);
            Assert.Equal("1.99", source.SchemaVersion);
        }

        [Fact]
        public void EveryPartOfTheBinding_ReachesTheDocument()
        {
            AssuranceReport report = Reports.Everything();
            string html = HtmlReport.Render(report);

            // This model, by content.
            Assert.Contains(report.Provenance.Subject!.Sha256!, html);
            Assert.Contains("steel-hall.femex", html);

            // This producer, this version — the file's own stamp, and the tool's.
            Assert.Contains("griffel-etabs 0.4.1", html);
            Assert.Contains("Acme Warehouse", html);
            Assert.Contains(ReportTool.Name + " " + ReportTool.Version, html);

            // This adapter, and the schema each side was built against.
            Assert.Contains("Structural Analysis Format", html);
            Assert.Contains(FemexModel.CurrentSchemaVersion, html);

            // On this date.
            Assert.Contains(Reports.Timestamp, html);
        }

        /// <summary>
        /// The timestamp is supplied rather than read from the clock — the same
        /// reason <c>ToJson</c> stamps a schema version and refuses to stamp a
        /// producer.
        /// </summary>
        [Fact]
        public void TheTimestamp_IsWhateverWasGiven()
        {
            var provenance = new ReportProvenance(null, "2026-08-28T09:30:00+02:00");

            Assert.Equal("2026-08-28T09:30:00+02:00", provenance.GeneratedAt);
            Assert.Equal("2026-08-28", provenance.Date);
        }

        /// <summary>
        /// And a timestamp this build cannot parse is shown rather than dropped: a
        /// header that silently omitted the date would be a header that lies about
        /// when the report was made.
        /// </summary>
        [Fact]
        public void AnUnparseableTimestamp_IsStillShown()
        {
            var provenance = new ReportProvenance(null, "the third Tuesday");

            Assert.Equal("the third Tuesday", provenance.Date);
        }

        [Fact]
        public void TheReport_IsWrittenWithoutAByteOrderMark()
        {
            string path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".html");

            try
            {
                HtmlReport.Write(Reports.Everything(), path);

                byte[] bytes = File.ReadAllBytes(path);
                Assert.False(bytes.Length > 2 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF,
                             "a byte-order mark is the one thing that can stop an HTML file rendering " +
                             "as HTML in an old viewer");
                Assert.Equal(HtmlReport.Render(Reports.Everything()), Encoding.UTF8.GetString(bytes));
            }
            finally
            {
                File.Delete(path);
            }
        }
    }
}
