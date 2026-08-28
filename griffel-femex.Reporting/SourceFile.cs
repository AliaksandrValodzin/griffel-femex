using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace griffel_femex.Reporting
{
    /// <summary>
    /// One input a report is about: what it was called, what its bytes hash to, what
    /// format it declares, and who says they wrote it.
    ///
    /// <b>The hash is the load-bearing field.</b> C3 of <c>SAF_Adapter.md</c>:
    /// provable binding — <i>"these findings came from this model, this producer,
    /// this version, on this date"</i> — <i>"is the auditable part, and it is
    /// worthless if it is a byline"</i>. A file name binds nothing; two files called
    /// <c>steel-hall.femex</c> a week apart are the usual case rather than the
    /// exception, and a report filed against a project is read when nobody
    /// remembers which one it was.
    /// </summary>
    public sealed class SourceFile
    {
        public SourceFile(string name, string? sha256 = null, long? byteCount = null,
                          string? schemaVersion = null, FileMetadata? metadata = null)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("A source has a name.", nameof(name));

            Name = name;
            Sha256 = sha256;
            ByteCount = byteCount;
            SchemaVersion = schemaVersion;
            Metadata = metadata;
        }

        /// <summary>
        /// What to call it in the report. A file name rather than a full path,
        /// deliberately: an audit engagement's reports are handed back to a client
        /// whose models were never on the author's disk, and
        /// <c>C:\work\clients\acme\...</c> in a header line says more about the
        /// author than about the model.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Lowercase hex SHA-256 of the bytes read, or null where the model never
        /// was a file — one built in memory, or handed over as an object. Null is an
        /// answer, and the report prints it as one rather than as an empty column.
        /// </summary>
        public string? Sha256 { get; }

        public long? ByteCount { get; }

        /// <summary>
        /// The <c>schemaVersion</c> the file declared, which is not necessarily the
        /// one this build reads: a 1.8 file read by a 1.10 build was migrated, and
        /// <c>ReportMigrations()</c> says what that did.
        /// </summary>
        public string? SchemaVersion { get; }

        /// <summary>
        /// The file's own producer stamp. Null for a format that has none — a SAF
        /// workbook read through the adapter arrives with whatever the adapter chose
        /// to record, and nothing else.
        /// </summary>
        public FileMetadata? Metadata { get; }

        /// <summary>The first eight hex digits, which is what a header line has room for.</summary>
        public string? ShortHash => Sha256 is null || Sha256.Length < 8 ? Sha256 : Sha256.Substring(0, 8);

        /// <summary>
        /// A source read from disk: hashed, sized and, where a model was parsed out
        /// of it, stamped with what that model declares about itself.
        /// </summary>
        public static SourceFile FromPath(string path, FemexModel? model = null)
        {
            if (path is null)
                throw new ArgumentNullException(nameof(path));

            byte[] bytes = File.ReadAllBytes(path);
            return FromBytes(Path.GetFileName(path), bytes, model);
        }

        /// <summary>
        /// A source that arrived as bytes — a stream from a web request, a workbook
        /// already in memory. The same statement, without the assumption that
        /// everything the tool reports on came off a local disk.
        /// </summary>
        public static SourceFile FromBytes(string name, byte[] bytes, FemexModel? model = null)
        {
            if (bytes is null)
                throw new ArgumentNullException(nameof(bytes));

            return new SourceFile(name, Hash(bytes), bytes.LongLength,
                                  model?.SchemaVersion, model?.Metadata);
        }

        /// <summary>
        /// A model that never was a file. Hash and size are null rather than
        /// invented: hashing the serialization of an in-memory model would produce a
        /// number that binds the report to nothing a reader can re-derive.
        /// </summary>
        public static SourceFile FromModel(string name, FemexModel model)
        {
            if (model is null)
                throw new ArgumentNullException(nameof(model));

            return new SourceFile(name, null, null, model.SchemaVersion, model.Metadata);
        }

        /// <summary>Lowercase hex SHA-256, the spelling every other tool prints.</summary>
        public static string Hash(byte[] bytes)
        {
            if (bytes is null)
                throw new ArgumentNullException(nameof(bytes));

            using (var sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(bytes);
                var text = new StringBuilder(hash.Length * 2);
                foreach (byte b in hash)
                    text.Append(b.ToString("x2"));

                return text.ToString();
            }
        }
    }
}
