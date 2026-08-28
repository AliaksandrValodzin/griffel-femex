using System.Collections.Generic;
using System.IO;
using griffel_femex.Comparison;
using griffel_femex.Reporting;

namespace griffel_femex.Cli
{
    /// <summary>
    /// <c>femex compare model.femex baseline.femex</c> — Claim 2, at the command
    /// line.
    ///
    /// <b>Exactly two files, and the order matters.</b> The first is the model the
    /// report is about and the second is what it is being held against, which is the
    /// same order <c>diff</c> has taught everyone to expect. It also decides the
    /// wording of every difference, since <see cref="ModelDiff"/> speaks of a left
    /// and a right.
    ///
    /// <b>Exit 1 when the models differ</b>, matching <c>diff</c> and
    /// <c>git diff --exit-code</c>: a comparison that found something is the tool
    /// working, and a build step that wants to fail on any change has the answer it
    /// needs without parsing the report.
    /// </summary>
    internal static class CompareCommand
    {
        public static int Run(CommandLine line, IReadOnlyList<string> inputs, TextWriter output,
                              TextWriter progress, TextWriter error)
        {
            if (inputs.Count != 2)
            {
                error.WriteLine("compare takes two files: the model, then the baseline it is held against.");
                return ExitCode.ToolFailure;
            }

            string generatedAt = ReportProvenance.Now();

            ReadResult subject = ModelReader.Read(inputs[0]);
            ReadResult baseline = ModelReader.Read(inputs[1]);

            // Either file being unreadable is a finding about that file, not a
            // failure of the tool — P4 again, and the report says which one it was.
            if (subject.Model is null || baseline.Model is null)
            {
                ReadResult unreadable = subject.Model is null ? subject : baseline;

                var failed = new AssuranceReport(
                    new ReportProvenance(new[] { subject.Source, baseline.Source }, generatedAt),
                    CheckSection.Unreadable(unreadable.Source.Name, unreadable.Failure ?? "no reason was given"));

                ReportOutput.Emit(failed, line, Path.GetFileNameWithoutExtension(inputs[0]), output, progress);
                return ExitCode.Findings;
            }

            IReadOnlyList<ModelDifference> differences = ModelDiff.Compare(subject.Model, baseline.Model);

            var report = new AssuranceReport(
                new ReportProvenance(new[] { subject.Source, baseline.Source }, generatedAt),
                check: null,
                compare: new CompareSection(baseline.Source, differences));

            ReportOutput.Emit(report, line, Path.GetFileNameWithoutExtension(inputs[0]), output, progress);

            return report.IsClean ? ExitCode.Clean : ExitCode.Findings;
        }
    }
}
