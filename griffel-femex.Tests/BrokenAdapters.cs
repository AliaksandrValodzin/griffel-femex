using System.Text;
using System.Text.Json;
using griffel_femex.Geometry;
using griffel_femex.Geometry.Sections;
using griffel_femex.Interop;
using griffel_femex.Interop.Conformance;
using griffel_femex.Loads;

namespace griffel_femex.Tests
{
    /// <summary>
    /// Seven adapters, each broken in exactly one of the ways §7.3 forbids.
    ///
    /// They exist because a harness that has never failed is a decoration. Each is
    /// the smallest deviation from <see cref="ReferenceAdapter"/> that breaks its
    /// rule — and each is a mistake somebody will make for real: assuming a model is
    /// complete, refusing a half-drawn one, naming with a counter, streaming nodes
    /// one at a time, quietly not carrying something.
    /// </summary>
    internal abstract class AdapterDecorator : IFemexImporter, IFemexExporter
    {
        protected ReferenceAdapter Inner { get; } = new ReferenceAdapter();

        public virtual AdapterInfo Info => Inner.Info;

        public virtual AdapterCapabilities Capabilities => Inner.Capabilities;

        public virtual TransferResult<FemexModel> Import(ImportRequest request,
                                                         IProgress<TransferProgress>? progress,
                                                         CancellationToken cancellationToken)
        {
            return Inner.Import(request, progress, cancellationToken);
        }

        public virtual TransferResult<ExportReceipt> Export(FemexModel model, ExportRequest request,
                                                            IProgress<TransferProgress>? progress,
                                                            CancellationToken cancellationToken)
        {
            return Inner.Export(model, request, progress, cancellationToken);
        }

        /// <summary>The same export, with some of what it said taken away.</summary>
        protected static TransferResult<ExportReceipt> Without(TransferResult<ExportReceipt> result,
                                                               Func<TransferMessage, bool> drop)
        {
            if (result.Value is null)
                return result;

            var kept = new List<TransferMessage>();
            foreach (TransferMessage message in result.Messages)
            {
                if (!drop(message))
                    kept.Add(message);
            }

            return TransferResult<ExportReceipt>.Ok(result.Value, kept);
        }
    }

    /// <summary>
    /// Carries no grids, declares no grids, and says nothing about either. The
    /// declaration is not a lie; the silence is. §4's obligation is that what does
    /// not cross is reported, and an entity absent from the capabilities is a
    /// promise to report it.
    /// </summary>
    internal sealed class DishonestAdapter : AdapterDecorator
    {
        public override TransferResult<ExportReceipt> Export(FemexModel model, ExportRequest request,
                                                             IProgress<TransferProgress>? progress,
                                                             CancellationToken cancellationToken)
        {
            return Without(base.Export(model, request, progress, cancellationToken),
                           m => m.Subject?.Entity == FemexEntity.Grid);
        }
    }

    /// <summary>
    /// Dereferences the unit block. The naive shape — assume completeness,
    /// dereference everything, throw on null — is precisely what you get if plugin #1
    /// is written before the contract exists, because from inside a plugin every null
    /// looks like a bug in the caller.
    /// </summary>
    internal sealed class BrittleAdapter : AdapterDecorator
    {
        public override TransferResult<ExportReceipt> Export(FemexModel model, ExportRequest request,
                                                             IProgress<TransferProgress>? progress,
                                                             CancellationToken cancellationToken)
        {
            _ = model.Units!.Length!.Value;
            return base.Export(model, request, progress, cancellationToken);
        }
    }

    /// <summary>
    /// Refuses a model with no sections — an adapter inventing its own notion of
    /// "ready" outside <c>Validate()</c>, which is §2.3's prohibition and blocks
    /// §2.1's founding example.
    /// </summary>
    internal sealed class FussyAdapter : AdapterDecorator
    {
        public override TransferResult<ExportReceipt> Export(FemexModel model, ExportRequest request,
                                                             IProgress<TransferProgress>? progress,
                                                             CancellationToken cancellationToken)
        {
            if (model.Sections.Count == 0)
                return TransferResult<ExportReceipt>.Failed("No sections have been chosen yet.");

            return base.Export(model, request, progress, cancellationToken);
        }
    }

    /// <summary>Reports a loss against a bar that is not in the model.</summary>
    internal sealed class MisanchoringAdapter : AdapterDecorator
    {
        public override TransferResult<ExportReceipt> Export(FemexModel model, ExportRequest request,
                                                             IProgress<TransferProgress>? progress,
                                                             CancellationToken cancellationToken)
        {
            TransferResult<ExportReceipt> result = base.Export(model, request, progress, cancellationToken);
            if (result.Value is null)
                return result;

            var messages = new List<TransferMessage>(result.Messages)
            {
                TransferMessage.Loss(LossCategory.Approximated,
                                     new ObjectRef(FemexEntity.Bar, 999999, Guid.NewGuid()),
                                     "Something happened to a bar that is not here."),
            };

            return TransferResult<ExportReceipt>.Ok(result.Value, messages);
        }
    }

    /// <summary>
    /// Approximates every support exactly as the reference adapter does, and does not
    /// mention it. The model that comes back is stiffer than the one that went, and
    /// nothing in the report says so — §4.3's failure, applied to a category that is
    /// even easier to leave out.
    /// </summary>
    internal sealed class SilentAdapter : AdapterDecorator
    {
        public override TransferResult<ExportReceipt> Export(FemexModel model, ExportRequest request,
                                                             IProgress<TransferProgress>? progress,
                                                             CancellationToken cancellationToken)
        {
            return Without(base.Export(model, request, progress, cancellationToken),
                           m => m.Subject?.Entity == FemexEntity.Support);
        }
    }

    /// <summary>
    /// Names by counter. The names are stable across two exports of the same model,
    /// which is why the check tests the <i>form</i> as well: a counter is stable only
    /// until the list changes, and <c>Section1</c> does not tell a reader it was
    /// invented.
    /// </summary>
    internal sealed class CountingAdapter : AdapterDecorator
    {
        public override TransferResult<ExportReceipt> Export(FemexModel model, ExportRequest request,
                                                             IProgress<TransferProgress>? progress,
                                                             CancellationToken cancellationToken)
        {
            int n = 0;
            foreach (Section section in model.Sections)
            {
                n++;
                if (string.IsNullOrWhiteSpace(section.Name))
                    section.Name = $"Section{n}";
            }

            return base.Export(model, request, progress, cancellationToken);
        }
    }

    /// <summary>
    /// Imports a model with one load its own format cannot resolve, and says nothing
    /// about it.
    ///
    /// The mistake is the one the SAF adapter actually made: a line load hosted on a
    /// plate edge, given the properties of a bar-hosted one — a local direction and a
    /// pair of positions, with no host to measure either against. Nothing here is
    /// lost in transit, so <see cref="ConformanceHarness"/>'s loss-coverage check
    /// cannot see it: the load round-trips perfectly and the model is invalid. That
    /// is the whole argument for a second check that looks at the imported model
    /// rather than at the difference.
    /// </summary>
    internal sealed class InvalidatingAdapter : AdapterDecorator
    {
        public override TransferResult<FemexModel> Import(ImportRequest request,
                                                          IProgress<TransferProgress>? progress,
                                                          CancellationToken cancellationToken)
        {
            TransferResult<FemexModel> result = base.Import(request, progress, cancellationToken);
            FemexModel? model = result.Value;
            if (model is null || model.Nodes.Count < 2)
                return result;

            if (model.LoadCases.Count == 0)
                model.LoadCases.Add(new LoadCase { Number = 1, Label = "LC1" });

            model.Loads.Add(new LinearLoad
            {
                Id = model.Loads.Count + 1,
                Label = "Manufactured",
                LoadCaseNumber = model.LoadCases[0].Number,
                StartNode = model.Nodes[0].NodeNumber,
                EndNode = model.Nodes[1].NodeNumber,
                CoordinateSystem = LoadCoordinateSystem.Local,
                Direction = LoadDirection.Z,
                StartPosition = 0.0,
                EndPosition = 1.0,
                MagnitudeStart = -1000.0,
                MagnitudeEnd = -1000.0,
            });

            return result;
        }
    }

    /// <summary>
    /// Imports the way it is natural to write an importer and the way §6.2 forbids:
    /// one node at a time, each matched against the model as it stands.
    ///
    /// Nothing about it looks wrong. It uses <c>GetOrAddNode</c>, which is the helper
    /// the contract mandates, and it creates levels on demand. What it does not do is
    /// collect first and cluster once — so the tolerance grows underneath it, the
    /// numbering follows the order the source happened to be walked in, and the same
    /// document read twice is two different models.
    /// </summary>
    internal sealed class StreamingAdapter : IFemexImporter, IFemexExporter
    {
        private readonly ReferenceAdapter _inner = new ReferenceAdapter();

        public AdapterInfo Info => _inner.Info;

        public AdapterCapabilities Capabilities => _inner.Capabilities;

        public TransferResult<ExportReceipt> Export(FemexModel model, ExportRequest request,
                                                    IProgress<TransferProgress>? progress,
                                                    CancellationToken cancellationToken)
        {
            return _inner.Export(model, request, progress, cancellationToken);
        }

        public TransferResult<FemexModel> Import(ImportRequest request,
                                                 IProgress<TransferProgress>? progress,
                                                 CancellationToken cancellationToken)
        {
            var stream = (StreamImportRequest)request;
            using var reader = new StreamReader(stream.Source, Encoding.UTF8, true, 1024, leaveOpen: true);
            ReferenceDocument document =
                JsonSerializer.Deserialize<ReferenceDocument>(reader.ReadToEnd())!;

            var model = new FemexModel { SchemaVersion = FemexModel.CurrentSchemaVersion };
            var nodes = new Dictionary<Guid, Node>();

            foreach (ReferenceNode source in document.Nodes)
            {
                Level? level = model.Levels.Find(l => Math.Abs(l.AbsoluteElevation - source.Z) < 1e-9);
                if (level is null)
                {
                    level = new Level(model.Levels.Count, null, source.Z, source.Z);
                    model.Levels.Add(level);
                }

                Node node = model.GetOrAddNode(source.X, source.Y, level.LevelNumber);
                node.Uid = source.Uid == Guid.Empty ? null : source.Uid;
                nodes[source.Uid] = node;
            }

            return TransferResult<FemexModel>.Ok(model);
        }
    }
}
