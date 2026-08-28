using System.IO;
using System.Text;
using System.Threading;
using griffel_femex.Adapters.Saf;
using griffel_femex.Interop;
using griffel_femex.Reporting;

namespace griffel_femex.Cli
{
    /// <summary>What reading one input produced.</summary>
    public sealed class ReadResult
    {
        internal ReadResult(SourceFile source, FemexModel? model, string? failure, TransferLeg? importLeg)
        {
            Source = source;
            Model = model;
            Failure = failure;
            ImportLeg = importLeg;
        }

        /// <summary>The file, hashed — whether or not it could be parsed.</summary>
        public SourceFile Source { get; }

        /// <summary>The model, or null when the file could not be read as one.</summary>
        public FemexModel? Model { get; }

        /// <summary>Why not, in one sentence. Null when there was no failure.</summary>
        public string? Failure { get; }

        /// <summary>
        /// The import leg, for a file that arrived through an adapter. Null for a
        /// <c>.femex</c>, which crosses no boundary and therefore loses nothing.
        /// </summary>
        public TransferLeg? ImportLeg { get; }

        public bool Succeeded => Model is not null;
    }

    /// <summary>
    /// Turning a path into a model, without ever throwing at the caller.
    ///
    /// <b>This class is where P4 lands.</b> The plan's minimum scope for it, whatever
    /// is eventually decided about the format itself, is that <i>"the CLI and the
    /// adapter never surface a <c>.femex</c> read failure as an unhandled
    /// exception"</i> — because every enum in the library throws on an unrecognised
    /// value, so a file from a later schema carrying one <c>"lengthUnit":
    /// "Furlong"</c> would otherwise exit 2 with a stack trace where
    /// <c>IExtensible</c>'s whole design says an unknown member is preserved,
    /// re-emitted and named. C4 fixes the answer: a 1 with a finding.
    ///
    /// So the catch here is deliberately broad, and it is the boundary that makes it
    /// legitimate rather than lazy: everything below is parsing a file the user
    /// chose, and there is no exception type that arrives from that which the user
    /// should be shown as a crash.
    /// </summary>
    public static class ModelReader
    {
        /// <summary>Whether the path names a SAF workbook rather than a FEMEX file.</summary>
        public static bool IsWorkbook(string path)
        {
            return string.Equals(Path.GetExtension(path), ".xlsx", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Reads a <c>.femex</c> or a SAF <c>.xlsx</c>, hashing the bytes on the way
        /// past, so that a report about the model can bind to the file it came from.
        /// </summary>
        public static ReadResult Read(string path)
        {
            if (path is null)
                throw new ArgumentNullException(nameof(path));

            string name = Path.GetFileName(path);
            byte[] bytes;

            try
            {
                bytes = File.ReadAllBytes(path);
            }
            catch (IOException error)
            {
                // A file that will not open at all is not a finding about a model —
                // there is no model — but it is still not a crash.
                return new ReadResult(new SourceFile(name), null, error.Message, null);
            }
            catch (UnauthorizedAccessException error)
            {
                return new ReadResult(new SourceFile(name), null, error.Message, null);
            }

            return IsWorkbook(path) ? ReadWorkbook(name, bytes) : ReadFemex(name, bytes);
        }

        private static ReadResult ReadFemex(string name, byte[] bytes)
        {
            try
            {
                // Decoded from the bytes already read rather than read a second
                // time, so the hash in the report is of exactly what was parsed.
                string json;
                using (var stream = new MemoryStream(bytes))
                using (var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true))
                {
                    json = reader.ReadToEnd();
                }

                FemexModel model = FemexModel.FromJson(json);
                return new ReadResult(SourceFile.FromBytes(name, bytes, model), model, null, null);
            }
            catch (Exception error)
            {
                // Broad on purpose; see the class remarks. An unrecognised enum
                // value arrives here as a JsonException wrapping the converter's
                // own complaint, and the inner sentence is the one that names the
                // value — which is what a reader needs and the outer one does not
                // say.
                string reason = error.InnerException is null
                    ? error.Message
                    : error.Message + " " + error.InnerException.Message;

                return new ReadResult(new SourceFile(name, SourceFile.Hash(bytes), bytes.LongLength),
                                      null, Flatten(reason), null);
            }
        }

        private static ReadResult ReadWorkbook(string name, byte[] bytes)
        {
            var importer = new SafImporter();

            try
            {
                using var stream = new MemoryStream(bytes, writable: false);

                TransferResult<FemexModel> imported = importer.Import(
                    new StreamImportRequest(stream) { SourceName = name }, null, CancellationToken.None);

                var leg = new TransferLeg(TransferDirection.Import, importer.Info, name, null,
                                          imported.Succeeded, imported.Messages);

                SourceFile source = SourceFile.FromBytes(name, bytes, imported.Value);

                if (imported.Succeeded)
                    return new ReadResult(source, imported.Value, null, leg);

                return new ReadResult(source, null, Explain(imported), leg);
            }
            catch (Exception error)
            {
                // §3.6's rule is that an adapter returns rather than throws, and the
                // SAF adapter obeys it. This is the belt for the braces: a workbook
                // that makes a third-party Excel reader throw somewhere the adapter
                // did not anticipate is still a bad input file, not a broken tool.
                return new ReadResult(new SourceFile(name, SourceFile.Hash(bytes), bytes.LongLength),
                                      null, Flatten(error.Message), null);
            }
        }

        private static string Explain(TransferResult<FemexModel> result)
        {
            var text = new StringBuilder();
            foreach (TransferMessage message in result.Messages)
            {
                if (message.Severity != ValidationSeverity.Error)
                    continue;

                if (text.Length > 0)
                    text.Append(' ');

                text.Append(message.Text);
            }

            return text.Length == 0 ? "the adapter produced no model and did not say why" : text.ToString();
        }

        /// <summary>
        /// One line. A parser's message can carry a JSON path and a line number
        /// across three lines of its own, and this text goes into a table cell and a
        /// terminal line.
        /// </summary>
        private static string Flatten(string text)
        {
            return text.Replace("\r\n", " ").Replace('\n', ' ').Replace('\r', ' ').Trim();
        }
    }
}
