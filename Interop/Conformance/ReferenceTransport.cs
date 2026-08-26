using System.Text;
using System.Text.Json;

namespace griffel_femex.Interop.Conformance
{
    /// <summary>
    /// A round trip for <see cref="ReferenceAdapter"/>, held entirely in memory —
    /// and the worked example an adapter author copies.
    ///
    /// It is what a file-based transport looks like: a buffer, a
    /// <see cref="StreamExportRequest"/> writing into it and a
    /// <see cref="StreamImportRequest"/> reading it back. The only part that takes
    /// any thought is <see cref="TryBeginReorderedImport"/>, and that is the point of
    /// it — §6.2's rule cannot be tested without a source the test can present in a
    /// different order, and only something that knows the format can reorder it.
    /// </summary>
    public sealed class ReferenceTransport : ConformanceTransport
    {
        private static readonly JsonSerializerOptions Options = new JsonSerializerOptions
        {
            WriteIndented = true,
        };

        private byte[] _written = new byte[0];

        public override ExportRequest BeginExport()
        {
            var buffer = new CapturingStream(this);
            return new StreamExportRequest(buffer) { DestinationName = "reference-document.json" };
        }

        public override ImportRequest BeginImport()
        {
            return new StreamImportRequest(new MemoryStream(_written, writable: false))
            {
                SourceName = "reference-document.json",
            };
        }

        /// <summary>
        /// The same document with every list reversed. Nothing about the model
        /// changes — the same nodes at the same places, the same members between
        /// them — so an import whose answer depends on this has an answer that
        /// depends on traversal order, which §6.2 says is fatal to §7.2's
        /// equivalence.
        /// </summary>
        public override bool TryBeginReorderedImport(out ImportRequest? request)
        {
            request = null;
            if (_written.Length == 0)
                return false;

            ReferenceDocument? document =
                JsonSerializer.Deserialize<ReferenceDocument>(Encoding.UTF8.GetString(_written), Options);

            if (document is null)
                return false;

            document.Materials.Reverse();
            document.Sections.Reverse();
            document.Nodes.Reverse();
            document.Members.Reverse();
            document.Panels.Reverse();
            document.Supports.Reverse();

            byte[] bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(document, Options));
            request = new StreamImportRequest(new MemoryStream(bytes, writable: false))
            {
                SourceName = "reference-document.json (reversed)",
            };

            return true;
        }

        /// <summary>
        /// Keeps what was written after the adapter has finished with it. An adapter
        /// writes and flushes and does not close a stream it was handed, but a
        /// transport cannot rely on that being true of every adapter, so the bytes
        /// are captured on every write rather than read off a stream afterwards.
        /// </summary>
        private sealed class CapturingStream : MemoryStream
        {
            private readonly ReferenceTransport _owner;

            internal CapturingStream(ReferenceTransport owner)
            {
                _owner = owner;
            }

            public override void Write(byte[] buffer, int offset, int count)
            {
                base.Write(buffer, offset, count);
                _owner._written = ToArray();
            }

            public override void Flush()
            {
                base.Flush();
                _owner._written = ToArray();
            }
        }
    }
}
