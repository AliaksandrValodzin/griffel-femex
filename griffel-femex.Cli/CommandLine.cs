using System.Collections.Generic;
using System.IO;

namespace griffel_femex.Cli
{
    /// <summary>How a report is rendered.</summary>
    public enum ReportFormat
    {
        /// <summary>One self-contained HTML file — C2, and the default.</summary>
        Html,

        /// <summary>The same report as data, for something that is not a person.</summary>
        Json,

        /// <summary>The summary block, for a terminal. Never written to a file.</summary>
        Text,
    }

    /// <summary>
    /// The command line, parsed — and nothing else. No file is opened here and no
    /// verb is run, so a caller can be told what it asked for before anything acts
    /// on it.
    ///
    /// <b>Hand-written rather than a package.</b> Three verbs and four options do
    /// not justify a dependency, and this repository's one durable rule about
    /// dependencies is that every one of them is a way for a deliverable to stop
    /// working later.
    /// </summary>
    public sealed class CommandLine
    {
        private CommandLine(string verb, IReadOnlyList<string> operands)
        {
            Verb = verb;
            Operands = operands;
        }

        /// <summary>check, compare or convert.</summary>
        public string Verb { get; }

        /// <summary>
        /// The files, with any wildcard already expanded — see
        /// <see cref="Expand"/>, which the shell does not do on Windows.
        /// </summary>
        public IReadOnlyList<string> Operands { get; }

        /// <summary>Where reports go. Null prints the summary to the terminal instead.</summary>
        public string? OutputDirectory { get; private set; }

        /// <summary>Where a single conversion writes its converted model.</summary>
        public string? To { get; private set; }

        public ReportFormat Format { get; private set; } = ReportFormat.Html;

        public bool FormatWasStated { get; private set; }

        public static bool TryParse(IReadOnlyList<string> args, out CommandLine? parsed, out string? error)
        {
            parsed = null;
            error = null;

            if (args.Count == 0)
            {
                error = "No verb. Try one of: check, compare, convert.";
                return false;
            }

            string verb = args[0];
            if (verb != "check" && verb != "compare" && verb != "convert")
            {
                error = $"'{verb}' is not a verb. Try one of: check, compare, convert.";
                return false;
            }

            var operands = new List<string>();
            var line = new CommandLine(verb, operands);

            for (int i = 1; i < args.Count; i++)
            {
                string arg = args[i];

                switch (arg)
                {
                    case "--out":
                        if (!Next(args, ref i, out string? outputDirectory))
                        {
                            error = "--out names a directory.";
                            return false;
                        }

                        line.OutputDirectory = outputDirectory;
                        continue;

                    case "--to":
                        if (!Next(args, ref i, out string? to))
                        {
                            error = "--to names a file.";
                            return false;
                        }

                        line.To = to;
                        continue;

                    case "--format":
                        if (!Next(args, ref i, out string? format))
                        {
                            error = "--format is one of: html, json, text.";
                            return false;
                        }

                        switch (format)
                        {
                            case "html": line.Format = ReportFormat.Html; break;
                            case "json": line.Format = ReportFormat.Json; break;
                            case "text": line.Format = ReportFormat.Text; break;
                            default:
                                error = $"'{format}' is not a format. Try one of: html, json, text.";
                                return false;
                        }

                        line.FormatWasStated = true;
                        continue;
                }

                if (arg.Length > 1 && arg[0] == '-')
                {
                    error = $"'{arg}' is not an option this build knows.";
                    return false;
                }

                operands.Add(arg);
            }

            if (operands.Count == 0)
            {
                error = $"'{verb}' needs at least one file.";
                return false;
            }

            parsed = line;
            return true;
        }

        private static bool Next(IReadOnlyList<string> args, ref int i, out string? value)
        {
            if (i + 1 >= args.Count)
            {
                value = null;
                return false;
            }

            value = args[++i];
            return true;
        }

        /// <summary>
        /// Wildcards, expanded here rather than by the shell.
        ///
        /// <c>femex check *.femex</c> is C4's own example, and on Windows — where
        /// this is developed and where the engineering audience works — the shell
        /// hands the pattern through untouched. A tool that only worked under a
        /// POSIX shell would be a tool whose documented example fails on the first
        /// machine it is run on.
        ///
        /// Results are sorted, because a batch index whose row order depends on the
        /// order the file system happened to return is a document that differs from
        /// itself between runs.
        /// </summary>
        public static IReadOnlyList<string> Expand(IReadOnlyList<string> operands)
        {
            var expanded = new List<string>();

            foreach (string operand in operands)
            {
                if (operand.IndexOf('*') < 0 && operand.IndexOf('?') < 0)
                {
                    expanded.Add(operand);
                    continue;
                }

                string directory = Path.GetDirectoryName(operand) ?? string.Empty;
                string pattern = Path.GetFileName(operand);

                if (directory.Length == 0)
                    directory = ".";

                if (!Directory.Exists(directory))
                    continue;

                var matches = new List<string>(Directory.GetFiles(directory, pattern));
                matches.Sort(StringComparer.OrdinalIgnoreCase);
                expanded.AddRange(matches);
            }

            return expanded;
        }
    }
}
