using System.Reflection;

namespace griffel_femex.Reporting
{
    /// <summary>
    /// What produced a report, and which build of it.
    ///
    /// A constant rather than a caller-supplied string, because C3 of
    /// <c>SAF_Adapter.md</c> is about <i>provable</i> binding — <i>"these findings
    /// came from this model, this producer, this version, on this date"</i> — and a
    /// producer stamp a caller can pass in is a producer stamp a caller can get
    /// wrong.
    /// </summary>
    public static class ReportTool
    {
        /// <summary>The name the CLI is invoked by, and the name in every report.</summary>
        public const string Name = "femex";

        private static string? _version;

        /// <summary>
        /// This assembly's own version, from the version stamped at build time.
        /// Read once and cached: it cannot change while the process runs, and a
        /// report that asked the reflection API per finding would be paying for an
        /// answer it already had.
        /// </summary>
        public static string Version
        {
            get
            {
                if (_version is not null)
                    return _version;

                Assembly assembly = typeof(ReportTool).GetTypeInfo().Assembly;

                string? informational = assembly
                    .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

                // The SDK appends "+<commit sha>" to the informational version when
                // the build is in a repository. Useful, and not what a reader of a
                // header line wants.
                if (informational is not null)
                {
                    int plus = informational.IndexOf('+');
                    if (plus > 0)
                        informational = informational.Substring(0, plus);
                }

                _version = string.IsNullOrWhiteSpace(informational)
                    ? assembly.GetName().Version?.ToString() ?? "0.0.0"
                    : informational!;

                return _version;
            }
        }

        /// <summary>
        /// The FEMEX schema this build reads and writes. Stated in the report beside
        /// the tool version because the two answer different questions: which format
        /// the findings are about, and which build of the checker found them.
        /// </summary>
        public static string SchemaVersion => FemexModel.CurrentSchemaVersion;
    }
}
