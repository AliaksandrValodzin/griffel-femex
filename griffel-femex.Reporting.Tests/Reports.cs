using System.IO;
using griffel_femex.Comparison;
using griffel_femex.Geometry;
using griffel_femex.Interop;

namespace griffel_femex.Reporting.Tests
{
    /// <summary>
    /// The fixtures this suite is built from: a model that has something wrong with
    /// it, a transfer that lost something, and one fixed timestamp so that two runs
    /// of the same assertion compare the same document.
    /// </summary>
    internal static class Reports
    {
        /// <summary>
        /// Fixed, not <c>Now()</c>. A report is a deliverable that has to be
        /// reproducible from its inputs, and a suite that stamped the clock could
        /// not assert that two renderings of one report are the same document.
        /// </summary>
        public const string Timestamp = "2026-08-28T09:30:00+02:00";

        /// <summary>
        /// A model with findings in all three categories at once: a bar referencing
        /// nothing that exists (referential), two nodes in the same place
        /// (judgement), and a schema version this build has never heard of
        /// (provenance).
        /// </summary>
        public static FemexModel WithFindings()
        {
            var model = new FemexModel { SchemaVersion = "1.99" };
            model.Levels.Add(new Level(1, "Ground", 0.0, 0.0, isGround: true));
            model.Nodes.Add(new Node(1, 0.0, 0.0, 1));
            model.Nodes.Add(new Node(2, 0.0, 0.0, 1));
            model.Bars.Add(new Bar(1, 1, 2, 7, 7));

            // Uid coverage is a precondition of comparison, not a nicety: without it
            // every object is Unkeyed and the diff can find nothing.
            model.AssignMissingUids();

            return model;
        }

        /// <summary>
        /// A copy of a model with one node moved, so a diff has something to find.
        /// Copied through JSON rather than rebuilt, so both sides carry the same
        /// uids and the difference is the one deliberately introduced.
        /// </summary>
        public static FemexModel Altered(FemexModel source)
        {
            FemexModel model = FemexModel.FromJson(source.ToJson());
            model.Nodes[1].X = 3.5;

            return model;
        }

        public static AdapterInfo Adapter =>
            new AdapterInfo("SAF", "Structural Analysis Format", "2.3.0", FemexModel.CurrentSchemaVersion);

        /// <summary>
        /// An import leg exercising four of the five loss categories and one
        /// per-concept message anchored to an entity kind with no id.
        /// </summary>
        public static TransferLeg ImportLeg(bool succeeded = true)
        {
            var messages = new[]
            {
                TransferMessage.ModelLoss(LossCategory.Invented,
                                          "The workbook states one coarse flag; five typed units were assumed."),
                TransferMessage.Loss(LossCategory.Invented, new ObjectRef(FemexEntity.Level, 3),
                                     "Level 3 at elevation 9.6 was synthesised; the native model has no storey.",
                                     "Storey <none>"),
                TransferMessage.Loss(LossCategory.Unmapped, new ObjectRef(FemexEntity.Bar),
                                     "142 members carried a stiffness modifier FEMEX has no property for."),
                TransferMessage.Loss(LossCategory.Approximated, new ObjectRef(FemexEntity.Section, 7),
                                     "The shape is outside FEMEX's eight discriminators and arrives generic.",
                                     "CS7 <\"quoted\" & unsafe>"),
            };

            return new TransferLeg(TransferDirection.Import, Adapter, "steel-hall.xlsx", null,
                                   succeeded, messages);
        }

        public static TransferLeg ExportLeg()
        {
            var messages = new[]
            {
                TransferMessage.Loss(LossCategory.Dropped, new ObjectRef(FemexEntity.Grid),
                                     "SAF has no grid concept, so both grids were dropped."),
            };

            return new TransferLeg(TransferDirection.Export, Adapter, null, "steel-hall-out.xlsx",
                                   true, messages);
        }

        /// <summary>A report carrying all three sections, which is a migration engagement's.</summary>
        public static AssuranceReport Everything()
        {
            FemexModel model = WithFindings();
            FemexModel baseline = Altered(model);

            var subject = new SourceFile("steel-hall.femex", SourceFile.Hash(new byte[] { 1, 2, 3 }), 3,
                                         model.SchemaVersion,
                                         new FileMetadata("griffel-etabs", "0.4.1", "Acme Warehouse",
                                                          "2026-08-14T11:02:00+02:00"));

            var baselineSource = new SourceFile("steel-hall-2026-08-14.femex",
                                                SourceFile.Hash(new byte[] { 4, 5, 6 }), 3);

            return new AssuranceReport(
                new ReportProvenance(new[] { subject, baselineSource }, Timestamp),
                new CheckSection(model.Validate()),
                new CompareSection(baselineSource, ModelDiff.Compare(model, baseline)),
                new TransferSection("SAF → FEMEX → SAF", ImportLeg(), ExportLeg()));
        }

        /// <summary>The repository's <c>Examples</c> folder, as copied beside the test binaries.</summary>
        public static string Example(string name)
        {
            return Path.Combine(AppContext.BaseDirectory, "Examples", name);
        }
    }
}
