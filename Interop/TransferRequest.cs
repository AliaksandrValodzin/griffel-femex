using System.IO;

namespace griffel_femex.Interop
{
    /// <summary>
    /// Where a model is coming from. Abstract and carrying no vendor type, because
    /// this is the seam that decides whether a driver — a CLI, a batch run, a web
    /// shell — is cheap or expensive.
    ///
    /// A live-session adapter (Revit, ETABS) subclasses this in <b>its own</b>
    /// assembly with whatever handle its API needs, so the core library never
    /// references a vendor assembly and a host that has none of them can still load
    /// the contract.
    /// </summary>
    public abstract class ImportRequest
    {
        /// <summary>
        /// What to call the source in a message — a file name, a document title.
        /// Diagnostics only: nothing resolves it, and an adapter that opens a stream
        /// must not go looking for a file of this name.
        /// </summary>
        public string? SourceName { get; init; }

        /// <summary>
        /// Adapter-specific switches, as strings so that a CLI can pass them through
        /// without the core library knowing what any of them mean. An adapter
        /// ignores what it does not recognise; it does not fail on it.
        /// </summary>
        public IReadOnlyDictionary<string, string>? Options { get; init; }
    }

    /// <summary>
    /// A model arriving as bytes — SAF's workbook, and anything else read through a
    /// stream. The SAF SDK settles the shape: its services are stream-based
    /// (<c>IExcelImportService.Import(stream)</c>), so a request that named a path
    /// would force every driver to write a temporary file it does not want.
    /// </summary>
    public sealed class StreamImportRequest : ImportRequest
    {
        public StreamImportRequest(Stream source)
        {
            Source = source ?? throw new ArgumentNullException(nameof(source));
        }

        /// <summary>
        /// The bytes. Owned by the caller: an adapter reads it and does not dispose
        /// it, because a batch driver reuses one stream across two adapters.
        /// </summary>
        public Stream Source { get; }
    }

    /// <summary>
    /// Where a model is going. The mirror of <see cref="ImportRequest"/>, and
    /// separate from it rather than one request type with a direction, because a
    /// live-session exporter needs a different native handle from an importer and
    /// fusing them would give both a property the other must ignore.
    /// </summary>
    public abstract class ExportRequest
    {
        /// <summary>What to call the destination in a message. Diagnostics only.</summary>
        public string? DestinationName { get; init; }

        /// <summary>As <see cref="ImportRequest.Options"/>.</summary>
        public IReadOnlyDictionary<string, string>? Options { get; init; }
    }

    /// <summary>A model leaving as bytes.</summary>
    public sealed class StreamExportRequest : ExportRequest
    {
        public StreamExportRequest(Stream destination)
        {
            Destination = destination ?? throw new ArgumentNullException(nameof(destination));
        }

        /// <summary>
        /// Where the bytes go. Owned by the caller, as on the import side — an
        /// adapter writes and flushes, and does not close a stream it was handed.
        /// </summary>
        public Stream Destination { get; }
    }
}
