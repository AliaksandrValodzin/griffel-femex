using System.Collections.Generic;
using System.Globalization;

namespace griffel_femex.Reporting
{
    /// <summary>
    /// Where a report came from: which files, hashed; which build of the tool; which
    /// schema; and when.
    ///
    /// <b>C3 makes this a section, not a footer.</b> The claim a report supports is
    /// not "this model has fourteen findings" — anyone can produce that — it is
    /// "<i>this</i> model, by content, checked by <i>this</i> build, on <i>this</i>
    /// date, has fourteen findings". The findings are the perishable half; the
    /// binding is the auditable one.
    ///
    /// <b><see cref="GeneratedAt"/> is supplied, not read from the clock.</b> A
    /// report is a deliverable that has to be reproducible from its inputs — the
    /// same reason <c>FemexModel.ToJson</c> stamps a schema version and refuses to
    /// stamp a timestamp — and a section that reached for <c>DateTime.Now</c> could
    /// not be tested for its own content.
    /// </summary>
    public sealed class ReportProvenance
    {
        private static readonly SourceFile[] NoSources = new SourceFile[0];

        public ReportProvenance(IEnumerable<SourceFile>? sources = null, string? generatedAt = null,
                                string? toolName = null, string? toolVersion = null,
                                string? schemaVersion = null)
        {
            Sources = sources is null ? (IReadOnlyList<SourceFile>)NoSources : new List<SourceFile>(sources);
            GeneratedAt = generatedAt ?? Now();
            ToolName = toolName ?? ReportTool.Name;
            ToolVersion = toolVersion ?? ReportTool.Version;
            SchemaVersion = schemaVersion ?? ReportTool.SchemaVersion;
        }

        /// <summary>
        /// Every file this report is about, in the order they were read — the model
        /// first, then a comparison baseline, then anything a transfer wrote.
        /// </summary>
        public IReadOnlyList<SourceFile> Sources { get; }

        /// <summary>
        /// ISO-8601, as free text, matching the spelling
        /// <see cref="FileMetadata.CreatedAt"/> and <c>FemexMesh.GeneratedAt</c>
        /// already use. A <c>DateTimeOffset</c> here would be reformatted by
        /// whichever writer rendered it, and the value is read by people.
        /// </summary>
        public string GeneratedAt { get; }

        public string ToolName { get; }

        public string ToolVersion { get; }

        /// <summary>The FEMEX schema this build reads and writes.</summary>
        public string SchemaVersion { get; }

        /// <summary>The first source, which is the model the report is about.</summary>
        public SourceFile? Subject => Sources.Count == 0 ? null : Sources[0];

        /// <summary>
        /// The current instant in the one spelling this library uses. Public because
        /// a driver that wants a real timestamp should not have to reinvent the
        /// format to get one the report will render consistently.
        /// </summary>
        public static string Now()
        {
            return DateTimeOffset.Now.ToString("yyyy-MM-ddTHH:mm:sszzz", CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Just the date, for the header line C1 sketches. Falls back to the whole
        /// string when a caller supplied something this does not recognise, because
        /// a header that silently dropped an unparseable timestamp would be a header
        /// that lies about when the report was made.
        /// </summary>
        public string Date
        {
            get
            {
                if (DateTimeOffset.TryParse(GeneratedAt, CultureInfo.InvariantCulture,
                                            DateTimeStyles.None, out DateTimeOffset parsed))
                {
                    return parsed.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                }

                return GeneratedAt;
            }
        }
    }
}
