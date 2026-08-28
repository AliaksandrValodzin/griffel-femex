using System.Collections.Generic;
using System.IO;
using griffel_femex.Reporting;

namespace griffel_femex.Cli
{
    /// <summary>
    /// The whole tool, as one callable function.
    ///
    /// <b>Not <c>Main</c>.</b> <see cref="Run"/> takes its output as parameters and
    /// returns its exit code, so the end-to-end verification C asks for — a report
    /// whose findings match <c>Validate()</c>, a batch run over a corpus, a file
    /// that exits 1 rather than 2 — is a test that calls a method rather than one
    /// that launches a process and parses its console. A driver that can only be
    /// exercised by spawning itself is a driver whose behaviour is asserted by
    /// nobody.
    /// </summary>
    public static class Cli
    {
        public const string Usage = @"femex — model assurance for FEMEX and SAF

  femex check    <file...> [--out DIR] [--format html|json|text]
  femex compare  <model> <baseline> [--out DIR] [--format html|json|text]
  femex convert  <file...> [--to FILE] [--out DIR] [--format html|json|text]

  check      reads a .femex or a SAF .xlsx and says what is wrong with it
  compare    says what changed between a model and a baseline, matched by uid
  convert    .xlsx to .femex, or .femex to .xlsx, with a report of what it cost

  --out      where reports go, and where convert writes what it converted.
             Without it the summary is printed and no document is written.
  --to       where one conversion writes its converted model
  --format   html (default), json, or text

  Wildcards are expanded by femex itself, so `femex check *.femex` works
  in any shell.

  Exit codes:  0 nothing to report · 1 findings · 2 the tool could not run

  This tool states findings and provenance.
  It does not offer an engineering opinion.";

        /// <summary>
        /// Runs one command line. Never throws: a driver that can throw is a driver
        /// whose batch run over forty models stops at the seventh with a stack trace
        /// where a report should have been.
        /// </summary>
        public static int Run(IReadOnlyList<string> args, TextWriter output, TextWriter error)
        {
            if (args is null)
                throw new ArgumentNullException(nameof(args));
            if (output is null)
                throw new ArgumentNullException(nameof(output));
            if (error is null)
                throw new ArgumentNullException(nameof(error));

            if (args.Count == 0 || args[0] == "--help" || args[0] == "-h" || args[0] == "help")
            {
                output.WriteLine(Usage);
                return args.Count == 0 ? ExitCode.ToolFailure : ExitCode.Clean;
            }

            if (args[0] == "--version")
            {
                output.WriteLine($"{ReportTool.Name} {ReportTool.Version} · FEMEX schema {ReportTool.SchemaVersion}");
                return ExitCode.Clean;
            }

            if (!CommandLine.TryParse(args, out CommandLine? line, out string? parseError))
            {
                error.WriteLine(parseError);
                error.WriteLine();
                error.WriteLine(Usage);
                return ExitCode.ToolFailure;
            }

            IReadOnlyList<string> inputs = CommandLine.Expand(line!.Operands);

            if (inputs.Count == 0)
            {
                error.WriteLine("No file matched " + string.Join(" ", line.Operands) + ".");
                return ExitCode.ToolFailure;
            }

            foreach (string input in inputs)
            {
                if (!File.Exists(input))
                {
                    // A file that is not there is the caller's mistake, not a
                    // finding about a model: there is no model. Checked before
                    // anything is written, so a batch run does not half-produce a
                    // folder of reports and then stop.
                    error.WriteLine($"{input} does not exist.");
                    return ExitCode.ToolFailure;
                }
            }

            // Where the running commentary goes. When the report itself is stdout —
            // no --out, and a format something is going to parse — it goes nowhere:
            // one line of "converted this, wrote that" in front of a JSON document
            // makes the whole stream unparseable, and a driver whose output has to
            // have a preamble stripped off it is a driver nobody pipes twice.
            TextWriter progress = line.OutputDirectory is null && line.Format != ReportFormat.Text
                ? TextWriter.Null
                : output;

            try
            {
                switch (line.Verb)
                {
                    case "check":
                        return CheckCommand.Run(line, inputs, output, progress);

                    case "compare":
                        return CompareCommand.Run(line, inputs, output, progress, error);

                    default:
                        return ConvertCommand.Run(line, inputs, output, progress, error);
                }
            }
            catch (Exception failure)
            {
                // The last resort, and the reason exit code 2 exists. Anything that
                // reaches here is a defect in this tool or a folder it cannot write
                // to — never a bad input file, which every verb above turns into a
                // finding long before it gets this far.
                error.WriteLine(failure.Message);
                return ExitCode.ToolFailure;
            }
        }
    }
}
