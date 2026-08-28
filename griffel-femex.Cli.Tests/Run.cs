using System.Collections.Generic;
using System.IO;

namespace griffel_femex.Cli.Tests
{
    /// <summary>What one invocation of the tool did.</summary>
    internal sealed class Invocation
    {
        internal Invocation(int exitCode, string output, string error)
        {
            ExitCode = exitCode;
            Output = output;
            Error = error;
        }

        public int ExitCode { get; }

        public string Output { get; }

        public string Error { get; }

        public override string ToString() => $"exit {ExitCode}{Environment.NewLine}{Output}{Error}";
    }

    /// <summary>
    /// Running the tool, and the scratch folder each test gets to run it in.
    ///
    /// Every test here drives <see cref="Cli.Run"/> rather than a process, which is
    /// the whole reason that method takes its writers as arguments: a driver that
    /// could only be exercised by spawning itself is a driver whose exit codes are
    /// asserted by nobody.
    /// </summary>
    internal static class Run
    {
        public static Invocation Femex(params string[] args)
        {
            var output = new StringWriter();
            var error = new StringWriter();

            int exitCode = Cli.Run(args, output, error);

            return new Invocation(exitCode, output.ToString(), error.ToString());
        }

        /// <summary>An example, as copied beside the test binaries.</summary>
        public static string Example(string name) => Path.Combine(AppContext.BaseDirectory, "Examples", name);

        /// <summary>The published SAF corpus, likewise.</summary>
        public static string Corpus(string name) => Path.Combine(AppContext.BaseDirectory, "Corpus", name);

        public static string CorpusDirectory => Path.Combine(AppContext.BaseDirectory, "Corpus");

        public static IReadOnlyList<string> CorpusFiles
        {
            get
            {
                var files = new List<string>(Directory.GetFiles(CorpusDirectory, "*.xlsx"));
                files.Sort(StringComparer.OrdinalIgnoreCase);
                return files;
            }
        }

        /// <summary>
        /// A directory of this test's own, deleted afterwards. Reports are files, so
        /// every assertion about them is an assertion about a disk.
        /// </summary>
        public static string Scratch()
        {
            string path = Path.Combine(Path.GetTempPath(), "femex-cli-" + Path.GetRandomFileName());
            Directory.CreateDirectory(path);

            return path;
        }

        public static void Discard(string directory)
        {
            try
            {
                if (Directory.Exists(directory))
                    Directory.Delete(directory, recursive: true);
            }
            catch (IOException)
            {
                // A scratch folder that will not delete is not a test failure.
            }
        }
    }
}
