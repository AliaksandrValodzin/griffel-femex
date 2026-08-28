using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using griffel_femex.Comparison;
using griffel_femex.Interop;

namespace griffel_femex.Reporting
{
    /// <summary>
    /// The same report, for something that is not a person: <c>--format json</c>.
    ///
    /// <b>It renders the same <see cref="AssuranceReport"/> the HTML does.</b> That
    /// is the whole design: a machine-readable form built by walking the model a
    /// second time would be a second opinion about what the report says, and the two
    /// would part company on the first rule added under pressure. Both writers are
    /// views; neither is a source.
    ///
    /// The shape is deliberately flat and named rather than positional, so that a
    /// consumer written against today's report keeps working when a section gains a
    /// field — the same reasoning <c>IExtensible</c> applies to the format itself.
    /// </summary>
    public static class JsonReport
    {
        private static readonly JsonSerializerOptions Options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        };

        public static string Render(AssuranceReport report)
        {
            if (report is null)
                throw new ArgumentNullException(nameof(report));

            var document = new Dictionary<string, object?>
            {
                ["title"] = report.Title,
                ["subject"] = report.SubjectName,
                ["clean"] = report.IsClean,
                ["summary"] = report.Summary()
                                    .Select(row => new Dictionary<string, object?>
                                    {
                                        ["section"] = row.Section,
                                        ["subject"] = row.Subject,
                                        ["detail"] = row.Detail,
                                    })
                                    .ToList(),
                ["provenance"] = Provenance(report.Provenance),
            };

            if (report.Check is not null)
                document["check"] = Check(report.Check);

            if (report.Compare is not null)
                document["compare"] = Compare(report.Compare);

            if (report.Transfer is not null)
                document["transfer"] = Transfer(report.Transfer);

            return JsonSerializer.Serialize(document, Options);
        }

        public static void Write(AssuranceReport report, string path)
        {
            if (path is null)
                throw new ArgumentNullException(nameof(path));

            File.WriteAllText(path, Render(report), new UTF8Encoding(false));
        }

        private static Dictionary<string, object?> Provenance(ReportProvenance provenance)
        {
            return new Dictionary<string, object?>
            {
                ["generatedAt"] = provenance.GeneratedAt,
                ["tool"] = provenance.ToolName,
                ["toolVersion"] = provenance.ToolVersion,
                ["schemaVersion"] = provenance.SchemaVersion,
                ["sources"] = provenance.Sources.Select(Source).ToList(),
            };
        }

        private static Dictionary<string, object?> Source(SourceFile source)
        {
            var row = new Dictionary<string, object?>
            {
                ["name"] = source.Name,
                ["sha256"] = source.Sha256,
                ["bytes"] = source.ByteCount,
                ["declaredSchemaVersion"] = source.SchemaVersion,
            };

            FileMetadata? metadata = source.Metadata;
            if (metadata is not null)
            {
                row["producer"] = metadata.Producer;
                row["producerVersion"] = metadata.ProducerVersion;
                row["projectName"] = metadata.ProjectName;
                row["createdAt"] = metadata.CreatedAt;
            }

            return row;
        }

        private static Dictionary<string, object?> Check(CheckSection check)
        {
            return new Dictionary<string, object?>
            {
                ["count"] = check.Count,
                ["errors"] = check.ErrorCount,
                ["warnings"] = check.WarningCount,
                ["judgement"] = check.CountOf(ValidationCategory.Judgement),
                ["referential"] = check.CountOf(ValidationCategory.Referential),
                ["provenance"] = check.CountOf(ValidationCategory.Provenance),
                ["findings"] = check.Findings.Select(finding => new Dictionary<string, object?>
                {
                    ["severity"] = finding.Severity.ToString(),
                    ["category"] = finding.Category.ToString(),
                    ["text"] = finding.Text,
                }).ToList(),
            };
        }

        private static Dictionary<string, object?> Compare(CompareSection compare)
        {
            return new Dictionary<string, object?>
            {
                ["baseline"] = Source(compare.Baseline),
                ["count"] = compare.Count,
                ["differences"] = compare.Differences.Select(difference => new Dictionary<string, object?>
                {
                    ["kind"] = difference.Kind.ToString(),
                    ["entity"] = difference.Subject.HasValue ? difference.Subject.Value.Entity.ToString() : null,
                    ["id"] = difference.Subject.HasValue ? difference.Subject.Value.Id : null,
                    ["uid"] = difference.Subject.HasValue ? difference.Subject.Value.Uid?.ToString() : null,
                    ["member"] = difference.Member,
                    ["left"] = difference.Left,
                    ["right"] = difference.Right,
                    ["text"] = difference.Text,
                }).ToList(),
            };
        }

        private static Dictionary<string, object?> Transfer(TransferSection transfer)
        {
            return new Dictionary<string, object?>
            {
                ["route"] = transfer.Route,
                ["losses"] = transfer.LossCount,
                ["succeeded"] = transfer.Succeeded,
                ["legs"] = transfer.Legs.Select(Leg).ToList(),
            };
        }

        private static Dictionary<string, object?> Leg(TransferLeg leg)
        {
            return new Dictionary<string, object?>
            {
                ["direction"] = leg.Direction.ToString(),
                ["adapter"] = leg.Adapter.Name,
                ["targetProgram"] = leg.Adapter.TargetProgram,
                ["targetProgramVersion"] = leg.Adapter.TargetProgramVersion,
                ["adapterSchemaVersion"] = leg.Adapter.SchemaVersion,
                ["source"] = leg.SourceName,
                ["destination"] = leg.DestinationName,
                ["succeeded"] = leg.Succeeded,
                ["losses"] = leg.LossCount,
                ["messages"] = leg.Messages.Select(message => new Dictionary<string, object?>
                {
                    ["severity"] = message.Severity.ToString(),
                    ["category"] = message.Category?.ToString(),
                    ["entity"] = message.Subject.HasValue ? message.Subject.Value.Entity.ToString() : null,
                    ["id"] = message.Subject.HasValue ? message.Subject.Value.Id : null,
                    ["uid"] = message.Subject.HasValue ? message.Subject.Value.Uid?.ToString() : null,
                    ["native"] = message.NativeHandle,
                    ["text"] = message.Text,
                }).ToList(),
            };
        }
    }
}
