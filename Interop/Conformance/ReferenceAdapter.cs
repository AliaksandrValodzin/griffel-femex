using System.Text;
using System.Text.Json;
using System.Threading;
using griffel_femex.BoundaryConditions;
using griffel_femex.Geometry;
using griffel_femex.Geometry.Sections;
using griffel_femex.Geometry.Surfaces;
using griffel_femex.Materials;
using griffel_femex.Synthesis;

namespace griffel_femex.Interop.Conformance
{
    /// <summary>
    /// A complete, deliberately lossy adapter over <see cref="ReferenceDocument"/> —
    /// FEMEX in, FEMEX out, no native API, nothing installed.
    ///
    /// <b>Why it exists.</b> A conformance base class with no implementation in this
    /// repository is a suite that has never been shown to fail. This adapter's job is
    /// not to be useful; it is to prove the conformance tests can tell a compliant
    /// plugin from a non-compliant one, <i>before</i> any real plugin depends on that
    /// distinction. Break one of its declarations and
    /// <see cref="ConformanceHarness"/> goes red, which is the only way to know the
    /// harness works at all.
    ///
    /// It exercises every one of §4's five categories, and it obeys every rule §6
    /// pre-decides: nodes and levels through two-phase synthesis, names through
    /// <see cref="NameSynthesis"/>, no second gate, and a failure that returns rather
    /// than throws.
    /// </summary>
    public sealed class ReferenceAdapter : IFemexImporter, IFemexExporter
    {
        /// <summary>
        /// What the adapter assumes when the model states no unit convention. §6.6
        /// names the assumption rather than leaving it per-adapter, because "each
        /// adapter assumes something and says what" is the five-ways failure the rule
        /// exists to prevent — only now with a paper trail.
        /// </summary>
        public const string AssumedUnitSystem = "metre-kilonewton";

        private static readonly JsonSerializerOptions DocumentOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
        };

        public ReferenceAdapter(string? schemaVersion = null)
        {
            Info = new AdapterInfo("Reference", "ReferenceDocument", "1.0",
                                   schemaVersion ?? FemexModel.CurrentSchemaVersion);
        }

        public AdapterInfo Info { get; }

        /// <summary>
        /// What the reference format can hold, and nothing more. Six entities cross;
        /// a <see cref="FemexEntity.Level"/> is produced on import and carried by
        /// nothing on export, because the format has no storeys; and grids, load
        /// cases, loads, combinations, hinges and the mesh do not cross at all.
        ///
        /// Every absence here is a promise the export leg has to keep by reporting
        /// the concept as <see cref="LossCategory.Dropped"/>, which is exactly what
        /// §7.3's capability-honesty test checks.
        /// </summary>
        public AdapterCapabilities Capabilities { get; } = new AdapterCapabilities(
            new[]
            {
                new KeyValuePair<FemexEntity, TransferDirection>(FemexEntity.Node, TransferDirection.Both),
                new KeyValuePair<FemexEntity, TransferDirection>(FemexEntity.Bar, TransferDirection.Both),
                new KeyValuePair<FemexEntity, TransferDirection>(FemexEntity.Plate, TransferDirection.Both),
                new KeyValuePair<FemexEntity, TransferDirection>(FemexEntity.Section, TransferDirection.Both),
                // Import only: a panel carries a bare thickness, so an import has to
                // build a surface property FEMEX needs and the document does not
                // have, and an export throws the FEMEX one away and keeps the number.
                new KeyValuePair<FemexEntity, TransferDirection>(FemexEntity.SurfaceProperty,
                                                                TransferDirection.Import),
                new KeyValuePair<FemexEntity, TransferDirection>(FemexEntity.Material, TransferDirection.Both),
                new KeyValuePair<FemexEntity, TransferDirection>(FemexEntity.Support, TransferDirection.Both),
                new KeyValuePair<FemexEntity, TransferDirection>(FemexEntity.Level, TransferDirection.Import),
            });

        // ================= Export =================

        public TransferResult<ExportReceipt> Export(FemexModel model, ExportRequest request,
                                                    IProgress<TransferProgress>? progress,
                                                    CancellationToken cancellationToken)
        {
            if (model is null)
                throw new ArgumentNullException(nameof(model));
            if (request is null)
                throw new ArgumentNullException(nameof(request));

            if (request is not StreamExportRequest stream)
            {
                return TransferResult<ExportReceipt>.Failed(
                    $"The reference adapter writes to a stream; it was handed a {request.GetType().Name}.");
            }

            var messages = new List<TransferMessage>();
            var document = new ReferenceDocument();

            TransferMessage? stale = Info.CompareSchema(model);
            if (stale is not null)
                messages.Add(stale);

            messages.AddRange(NameSynthesis.Apply(model));
            WriteUnits(model, document, messages);
            WriteMaterials(model, document, messages);
            WriteSections(model, document, messages);

            var handles = new Dictionary<Guid, string>();
            Dictionary<int, Guid> nodeUids = WriteNodes(model, document, handles);
            WriteMembers(model, document, messages, nodeUids, handles);
            WritePanels(model, document, messages, nodeUids, handles);
            WriteSupports(model, document, messages, nodeUids, handles);
            ReportUncarried(model, messages);

            progress?.Report(new TransferProgress(null, 1, 1, "written"));
            cancellationToken.ThrowIfCancellationRequested();

            byte[] bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(document, DocumentOptions));
            stream.Destination.Write(bytes, 0, bytes.Length);
            stream.Destination.Flush();

            return TransferResult<ExportReceipt>.Ok(
                new ExportReceipt(request.DestinationName, handles), messages);
        }

        /// <summary>
        /// §6.6: an adapter proceeds on a declared assumption and reports the assumed
        /// system as <see cref="LossCategory.Invented"/>. Refusing a model whose units
        /// it cannot read would be an adapter inventing a notion of "ready" outside
        /// <c>Validate()</c>, and would block the half-drawn handoff §2.1 exists to
        /// protect.
        ///
        /// Gravity rides along, because §6.5's larger trap is that
        /// <c>Gravity.Acceleration</c> defaults to a metre-specific 9.80665 and a
        /// millimetre model that accepts it is 1000x light — with nothing in
        /// <c>Validate()</c> to catch it, since gravity's validation checks the
        /// direction and the sign, not whether the magnitude matches the declared
        /// length unit. When the model states no length unit, the number cannot be
        /// checked against anything, and saying so is the whole of the rule.
        /// </summary>
        private static void WriteUnits(FemexModel model, ReferenceDocument document,
                                       List<TransferMessage> messages)
        {
            LengthUnit? length = model.Units?.Length;
            ForceUnit? force = model.Units?.Force;

            if (length is null || force is null)
            {
                document.UnitSystem = AssumedUnitSystem;
                messages.Add(TransferMessage.ModelLoss(
                    LossCategory.Invented,
                    "The reference format requires a unit system and this model states none, so " +
                    $"\"{AssumedUnitSystem}\" was assumed. Every number in the exported document is " +
                    "written on that assumption."));

                messages.Add(TransferMessage.ModelLoss(
                    LossCategory.Invented,
                    $"Gravity was written as {model.Gravity.Acceleration}, which cannot be checked " +
                    "against a length unit the model does not state. A millimetre model that accepted " +
                    "the metre-specific default is a thousand times light, and nothing in the file says " +
                    "which this is."));
            }
            else
            {
                document.UnitSystem = $"{length}-{force}".ToLowerInvariant();
            }

            if (model.Units?.Temperature is not null || model.Units?.Angle is not null
                || model.Units?.Mass is not null)
            {
                messages.Add(TransferMessage.ModelLoss(
                    LossCategory.Dropped,
                    "The reference format states its units as one string. FEMEX's temperature, angle " +
                    "and mass units have nowhere in it to go, so a temperature that was stated in " +
                    "Celsius arrives as a bare number."));
            }

            document.GravityAcceleration = model.Gravity.Acceleration;
        }

        private static void WriteMaterials(FemexModel model, ReferenceDocument document,
                                           List<TransferMessage> messages)
        {
            bool anyRicher = false;

            foreach (Material material in model.Materials)
            {
                document.Materials.Add(new ReferenceMaterial
                {
                    Uid = material.Uid ?? Guid.Empty,
                    Name = material.Name ?? string.Empty,
                    ModulusOfElasticity = material.ModulusOfElasticity,
                    PoissonsRatio = material.PoissonsRatio,
                    Density = material.Density,
                });

                anyRicher |= material.Type.HasValue || material.Quality is not null
                             || material.ShearModulus.HasValue || material.ThermalExpansion.HasValue
                             || material.Properties is not null || material.Strength != 0.0;
            }

            if (anyRicher)
            {
                // Per concept, not per object: one message saying what a material
                // loses is a useful report; one per material is a denial of service
                // against the person reading them.
                messages.Add(TransferMessage.Loss(
                    LossCategory.Dropped,
                    new ObjectRef(FemexEntity.Material),
                    "The reference format holds a material as three numbers and a name. Type, quality, " +
                    "a stated shear modulus, thermal expansion, the characteristic strength and the " +
                    "design-value block have nowhere to go, so a temperature load arriving at the far " +
                    "side is a number nothing can turn into a strain."));
            }
        }

        private static void WriteSections(FemexModel model, ReferenceDocument document,
                                          List<TransferMessage> messages)
        {
            foreach (Section section in model.Sections)
            {
                document.Sections.Add(new ReferenceSection
                {
                    Uid = section.Uid ?? Guid.Empty,
                    Name = section.Name ?? string.Empty,
                    Area = section.GetArea(),
                });
            }

            if (model.Sections.Count > 0)
            {
                messages.Add(TransferMessage.Loss(
                    LossCategory.Approximated,
                    new ObjectRef(FemexEntity.Section),
                    "The reference format holds a section as a name and an area. The shape, its " +
                    "dimensions, any catalogue profile and every second moment are gone, so a member " +
                    "crosses with its axial stiffness and not its bending stiffness."));
            }
        }

        private static Dictionary<int, Guid> WriteNodes(FemexModel model, ReferenceDocument document,
                                                        Dictionary<Guid, string> handles)
        {
            var uids = new Dictionary<int, Guid>();

            foreach (Node node in model.Nodes)
            {
                Level? level = model.Levels.Find(l => l.LevelNumber == node.LevelNumber);
                if (level is null)
                    continue;

                Guid uid = node.Uid ?? Guid.Empty;
                uids[node.NodeNumber] = uid;

                document.Nodes.Add(new ReferenceNode
                {
                    Uid = uid,
                    X = node.X,
                    Y = node.Y,
                    Z = level.AbsoluteElevation + node.VerticalOffset,
                });

                Handle(handles, uid, $"N{node.NodeNumber}");
            }

            return uids;
        }

        private static void WriteMembers(FemexModel model, ReferenceDocument document,
                                         List<TransferMessage> messages, Dictionary<int, Guid> nodeUids,
                                         Dictionary<Guid, string> handles)
        {
            foreach (Bar bar in model.Bars)
            {
                Section? section = model.Sections.Find(s => s.Id == bar.SectionId);
                Material? material = model.Materials.Find(m => m.Id == bar.MaterialId);

                // §2.2: no adapter ever leaves an unresolvable reference standing.
                // A bar drawn before a section was chosen carries SectionId 0, and
                // that is the three-levels-then-ETABS handoff, not a corner case.
                if (section is null)
                {
                    ReferenceSection placeholder = Placeholder(document, bar, "Section");
                    messages.Add(TransferMessage.Loss(
                        LossCategory.Invented,
                        new ObjectRef(FemexEntity.Bar, bar.Id, bar.Uid),
                        $"Bar {bar.Id} names section {bar.SectionId}, which does not resolve. It was " +
                        $"given the placeholder \"{placeholder.Name}\" so the export could complete; " +
                        "its area is zero and nothing was chosen."));
                }

                if (material is null)
                {
                    ReferenceMaterial placeholder = PlaceholderMaterial(document, bar);
                    messages.Add(TransferMessage.Loss(
                        LossCategory.Invented,
                        new ObjectRef(FemexEntity.Bar, bar.Id, bar.Uid),
                        $"Bar {bar.Id} names material {bar.MaterialId}, which does not resolve. It was " +
                        $"given the placeholder \"{placeholder.Name}\"; every property of it is zero."));
                }

                Guid uid = bar.Uid ?? Guid.Empty;
                document.Members.Add(new ReferenceMember
                {
                    Uid = uid,
                    StartNode = Lookup(nodeUids, bar.StartNodeId),
                    EndNode = Lookup(nodeUids, bar.EndNodeId),
                    Section = section?.Uid ?? PlaceholderUid(document, bar, "Section"),
                    Material = material?.Uid ?? PlaceholderUid(document, bar, "Material"),
                    Rotation = bar.RotationAngle,
                });

                Handle(handles, uid, $"M{bar.Id}");
            }
        }

        private static void WritePanels(FemexModel model, ReferenceDocument document,
                                        List<TransferMessage> messages, Dictionary<int, Guid> nodeUids,
                                        Dictionary<Guid, string> handles)
        {
            bool anyPanelDetail = false;

            foreach (Plate plate in model.Plates)
            {
                SurfaceProperty? surface = plate.SurfacePropertyId.HasValue
                    ? model.SurfaceProperties.Find(s => s.Id == plate.SurfacePropertyId.Value)
                    : null;

                var nodes = new List<Guid>();
                foreach (int nodeId in plate.NodeIds)
                    nodes.Add(Lookup(nodeUids, nodeId));

                Guid uid = plate.Uid ?? Guid.Empty;
                document.Panels.Add(new ReferencePanel
                {
                    Uid = uid,
                    Nodes = nodes,
                    Thickness = surface?.GetNominalThickness() ?? 0.0,
                    Material = plate.MaterialId.HasValue
                        ? model.Materials.Find(m => m.Id == plate.MaterialId.Value)?.Uid
                        : null,
                });

                Handle(handles, uid, $"P{plate.Id}");

                anyPanelDetail |= plate.Kind != PlateRegionKind.Structural
                                  || plate.Behaviour != PlateBehaviour.Shell
                                  || plate.Alignment != SurfaceAlignment.Centre
                                  || plate.SurfaceOffset != 0.0
                                  || plate.LocalAxisAngle != 0.0;

                // Regions are the canonical Dropped, and per object rather than per
                // concept because each one is a piece of the structure that is simply
                // not there on the other side.
                foreach (PlateRegion region in plate.Regions)
                {
                    messages.Add(TransferMessage.Loss(
                        LossCategory.Dropped,
                        new ObjectRef(FemexEntity.Plate, region.Id, region.Uid),
                        $"Plate {plate.Id} region {region.Id} ({region.Kind}, priority " +
                        $"{region.Priority}) has nowhere to go: the reference format models a panel as " +
                        "one thickness and has no region model, so FEMEX's priority rule — which is " +
                        "total where SAF's regions are undefined — is exactly what cannot cross."));
                }
            }

            if (anyPanelDetail)
            {
                messages.Add(TransferMessage.Loss(
                    LossCategory.Dropped,
                    new ObjectRef(FemexEntity.Plate),
                    "A panel in the reference format is a boundary, a thickness and a material. Its " +
                    "kind, its bending behaviour, its alignment, its surface offset and its local axis " +
                    "angle are not carried."));
            }
        }

        private static void WriteSupports(FemexModel model, ReferenceDocument document,
                                          List<TransferMessage> messages, Dictionary<int, Guid> nodeUids,
                                          Dictionary<Guid, string> handles)
        {
            foreach (Support support in model.Supports)
            {
                var nodes = new List<Guid>();
                foreach (int nodeId in support.NodeIds)
                    nodes.Add(Lookup(nodeUids, nodeId));

                Guid uid = support.Uid ?? Guid.Empty;
                document.Supports.Add(new ReferenceSupport
                {
                    Uid = uid,
                    Nodes = nodes,
                    Ux = Resists(support.Ux),
                    Uy = Resists(support.Uy),
                    Uz = Resists(support.Uz),
                    Rx = Resists(support.Rx),
                    Ry = Resists(support.Ry),
                    Rz = Resists(support.Rz),
                });

                Handle(handles, uid, $"S{support.Id}");

                if (Restraints(support).Exists(r => r.Stiffness.HasValue || r.Sense.HasValue))
                {
                    messages.Add(TransferMessage.Loss(
                        LossCategory.Approximated,
                        new ObjectRef(FemexEntity.Support, support.Id, support.Uid),
                        $"Support {support.Id} states a stiffness or a sense. The reference format has " +
                        "six booleans, so a spring arrives rigid and a compression-only bearing arrives " +
                        "resisting uplift as well — which is stiffer than what was drawn, not softer."));
                }

                if (support.Target != SupportTarget.Point)
                {
                    messages.Add(TransferMessage.Loss(
                        LossCategory.Approximated,
                        new ObjectRef(FemexEntity.Support, support.Id, support.Uid),
                        $"Support {support.Id} is a {support.Target} support. The reference format " +
                        "restrains nodes only, so it arrives as restraint at the nodes it names."));
                }
            }
        }

        /// <summary>
        /// The other half of capability honesty: an entity the adapter declares it
        /// cannot export, and that the model actually has, is reported once as a
        /// concept rather than left to be noticed by its absence.
        /// </summary>
        private void ReportUncarried(FemexModel model, List<TransferMessage> messages)
        {
            Report(FemexEntity.Grid, model.Grids.Count,
                   "Architectural grids are setting-out information the reference format has no notion of.");
            Report(FemexEntity.SurfaceProperty, model.SurfaceProperties.Count,
                   "A panel in the reference format carries a bare thickness, so the surface property " +
                   "itself — its name, its identity, and anything a later implementation of it would " +
                   "add — does not cross; only the number does.");
            Report(FemexEntity.Level, model.Levels.Count,
                   "The reference format has no storeys; every node is written at its absolute elevation, " +
                   "so the storey a piece of geometry belonged to is not recoverable from the file.");
            Report(FemexEntity.LoadCase, model.LoadCases.Count,
                   "The reference format carries geometry only. No load case crosses.");
            Report(FemexEntity.Load, model.Loads.Count,
                   "The reference format carries geometry only. No load crosses.");
            Report(FemexEntity.LoadCombination, model.LoadCombinations.Count,
                   "The reference format carries geometry only. No load combination crosses.");
            Report(FemexEntity.Hinge, model.Hinges.Count,
                   "The reference format has no member releases, so every member arrives continuous — " +
                   "which is stiffer than what was drawn.");
            Report(FemexEntity.Mesh, model.Mesh is null ? 0 : 1,
                   "The generated mesh is not carried. It is regenerated wholesale by whatever meshes " +
                   "next, so this is a loss of work rather than of information.");

            void Report(FemexEntity entity, int count, string why)
            {
                if (count == 0 || Capabilities.Supports(entity, TransferDirection.Export))
                    return;

                messages.Add(TransferMessage.Loss(LossCategory.Dropped, new ObjectRef(entity),
                                                  $"{count} {entity} object(s) were not written. {why}"));
            }
        }

        // ================= Import =================

        public TransferResult<FemexModel> Import(ImportRequest request,
                                                 IProgress<TransferProgress>? progress,
                                                 CancellationToken cancellationToken)
        {
            if (request is null)
                throw new ArgumentNullException(nameof(request));

            if (request is not StreamImportRequest stream)
            {
                return TransferResult<FemexModel>.Failed(
                    $"The reference adapter reads a stream; it was handed a {request.GetType().Name}.");
            }

            ReferenceDocument? document;
            try
            {
                using var reader = new StreamReader(stream.Source, Encoding.UTF8, true, 1024, leaveOpen: true);
                document = JsonSerializer.Deserialize<ReferenceDocument>(reader.ReadToEnd(), DocumentOptions);
            }
            catch (JsonException exception)
            {
                // §3.6: a failure the adapter can describe returns; it does not
                // throw. A host that had to catch this would lose the distinction
                // between a corrupt file and a bug in the adapter.
                return TransferResult<FemexModel>.Failed(
                    $"{request.SourceName ?? "The source"} is not a reference document: {exception.Message}");
            }

            if (document is null)
                return TransferResult<FemexModel>.Failed($"{request.SourceName ?? "The source"} was empty.");

            var messages = new List<TransferMessage>();
            var model = new FemexModel { SchemaVersion = Info.SchemaVersion };

            ReadUnits(document, model, messages);
            Dictionary<Guid, int> materials = ReadMaterials(document, model);
            Dictionary<Guid, int> sections = ReadSections(document, model);
            Dictionary<Guid, Node> nodes = ReadNodes(document, model, messages);

            cancellationToken.ThrowIfCancellationRequested();

            ReadMembers(document, model, messages, nodes, sections, materials);
            ReadPanels(document, model, messages, nodes, materials);
            ReadSupports(document, model, nodes);

            // §5.1: FEMEX can mint the value; only the exporter can remember what it
            // stands for. Everything this import built that the document had no
            // identity for gets one here — and says so, because a silent mint is a
            // false provenance claim.
            int minted = model.AssignMissingUids();
            if (minted > 0)
            {
                messages.Add(TransferMessage.ModelLoss(
                    LossCategory.Invented,
                    $"{minted} object(s) were given a fresh uid because the document carried no identity " +
                    "for them. Those uids are FEMEX identities minted at this boundary, not provenance " +
                    "read from the source, and re-importing the same document mints different ones."));
            }

            progress?.Report(new TransferProgress(null, 1, 1, "read"));

            return TransferResult<FemexModel>.Ok(model, messages);
        }

        private static void ReadUnits(ReferenceDocument document, FemexModel model,
                                      List<TransferMessage> messages)
        {
            model.Gravity = new Gravity(0.0, 0.0, -1.0, document.GravityAcceleration);

            if (string.Equals(document.UnitSystem, AssumedUnitSystem, StringComparison.OrdinalIgnoreCase))
            {
                model.Units = new Units(LengthUnit.Metre, ForceUnit.Kilonewton);
                return;
            }

            // The reference format's unit system is one string where FEMEX has five
            // typed enums, and anything that is not the one spelling this adapter
            // writes cannot be decomposed. Reported rather than guessed.
            messages.Add(TransferMessage.ModelLoss(
                LossCategory.Invented,
                $"The document states its units as \"{document.UnitSystem}\", which is one string where " +
                $"FEMEX has five typed enums. \"{AssumedUnitSystem}\" was assumed instead."));

            model.Units = new Units(LengthUnit.Metre, ForceUnit.Kilonewton);
        }

        private static Dictionary<Guid, int> ReadMaterials(ReferenceDocument document, FemexModel model)
        {
            var ids = new Dictionary<Guid, int>();
            int next = 1;

            foreach (ReferenceMaterial source in document.Materials)
            {
                model.Materials.Add(new Material
                {
                    Id = next,
                    Uid = source.Uid == Guid.Empty ? null : source.Uid,
                    Name = string.IsNullOrEmpty(source.Name) ? null : source.Name,
                    ModulusOfElasticity = source.ModulusOfElasticity,
                    PoissonsRatio = source.PoissonsRatio,
                    Density = source.Density,
                });

                ids[source.Uid] = next++;
            }

            return ids;
        }

        private static Dictionary<Guid, int> ReadSections(ReferenceDocument document, FemexModel model)
        {
            var ids = new Dictionary<Guid, int>();
            int next = 1;

            foreach (ReferenceSection source in document.Sections)
            {
                // The escape hatch schema 1.5 built for exactly this: a shape FEMEX
                // does not model crosses by its stiffness rather than being lost.
                model.Sections.Add(new GenericSection(next,
                                                      string.IsNullOrEmpty(source.Name) ? null : source.Name,
                                                      new SectionProperties(source.Area))
                {
                    Uid = source.Uid == Guid.Empty ? null : source.Uid,
                });

                ids[source.Uid] = next++;
            }

            return ids;
        }

        /// <summary>
        /// Every point in the document through one two-phase synthesis, which is what
        /// makes the same document read in two different orders produce the same node
        /// and level tables — §6.2's rule, and a precondition of §7.2's equivalence.
        /// </summary>
        private static Dictionary<Guid, Node> ReadNodes(ReferenceDocument document, FemexModel model,
                                                        List<TransferMessage> messages)
        {
            var synthesis = new GeometrySynthesis();
            var tickets = new Dictionary<Guid, int>();

            foreach (ReferenceNode source in document.Nodes)
                tickets[source.Uid] = synthesis.AddPoint(source.X, source.Y, source.Z);

            SynthesisResult result = synthesis.Build(model);
            messages.AddRange(result.Messages);

            if (result.InventedLevels.Count > 0)
            {
                // The levels themselves are already reported one by one. This is the
                // consequence of them, which is a separate fact about every node: a
                // node's storey is FEMEX's own invention, so a node that came from a
                // storeyed model and goes back to one does not return to the storey
                // it left.
                messages.Add(TransferMessage.Loss(
                    LossCategory.Invented,
                    new ObjectRef(FemexEntity.Node),
                    $"The document has no storeys, so all {document.Nodes.Count} node(s) were given the " +
                    $"{result.InventedLevels.Count} level(s) synthesised from their own elevations. The " +
                    "storey a node belongs to in this model was decided here."));
            }

            var nodes = new Dictionary<Guid, Node>();
            foreach (var pair in tickets)
                nodes[pair.Key] = result.NodeFor(pair.Value);

            // A node's uid is the smallest of the uids that resolved to it, rather
            // than the first: two coincident points in the document must give the
            // same answer whichever order they were listed in, or the determinism the
            // synthesis just bought is spent again here.
            var claimed = new Dictionary<Node, Guid>();
            foreach (var pair in nodes)
            {
                if (!claimed.TryGetValue(pair.Value, out Guid held) || pair.Key.CompareTo(held) < 0)
                    claimed[pair.Value] = pair.Key;
            }

            foreach (var pair in claimed)
                pair.Key.Uid = pair.Value == Guid.Empty ? null : pair.Value;

            return nodes;
        }

        private static void ReadMembers(ReferenceDocument document, FemexModel model,
                                        List<TransferMessage> messages, Dictionary<Guid, Node> nodes,
                                        Dictionary<Guid, int> sections, Dictionary<Guid, int> materials)
        {
            int next = NextElementId(model);
            int modified = 0;

            foreach (ReferenceMember source in document.Members)
            {
                model.Bars.Add(new Bar
                {
                    Id = next++,
                    Uid = source.Uid == Guid.Empty ? null : source.Uid,
                    StartNodeId = NodeNumber(nodes, source.StartNode),
                    EndNodeId = NodeNumber(nodes, source.EndNode),
                    SectionId = sections.TryGetValue(source.Section, out int section) ? section : 0,
                    MaterialId = materials.TryGetValue(source.Material, out int material) ? material : 0,
                    RotationAngle = source.Rotation,
                });

                if (source.StiffnessModifier.HasValue)
                    modified++;
            }

            if (modified > 0)
            {
                // §4.4, per concept and not per object: one message saying "142
                // members carried stiffness modifiers, which FEMEX cannot express"
                // is a useful report; 142 messages saying it is a denial of service
                // against the person reading them.
                messages.Add(TransferMessage.Loss(
                    LossCategory.Unmapped,
                    new ObjectRef(FemexEntity.Bar),
                    $"{modified} member(s) carried a stiffness modifier. FEMEX has no noun for one, so " +
                    "the members arrive with their full uncracked stiffness and the analysis they were " +
                    "modified for is not the analysis that will run."));
            }
        }

        private static void ReadPanels(ReferenceDocument document, FemexModel model,
                                       List<TransferMessage> messages, Dictionary<Guid, Node> nodes,
                                       Dictionary<Guid, int> materials)
        {
            int next = NextElementId(model);
            var thicknesses = new Dictionary<double, int>();

            if (document.Panels.Count > 0)
            {
                messages.Add(TransferMessage.Loss(
                    LossCategory.Invented,
                    new ObjectRef(FemexEntity.SurfaceProperty),
                    "A panel in the document carries a bare thickness. FEMEX addresses a thickness " +
                    "through a surface property, so one was created per distinct thickness — an object " +
                    "the document does not have, with a name and an identity nobody chose."));
            }

            foreach (ReferencePanel source in document.Panels)
            {
                if (!thicknesses.TryGetValue(source.Thickness, out int surfaceId))
                {
                    surfaceId = model.SurfaceProperties.Count + 1;
                    model.SurfaceProperties.Add(
                        new ConstantThickness(surfaceId, null, source.Thickness));
                    thicknesses[source.Thickness] = surfaceId;
                }

                var nodeIds = new List<int>();
                foreach (Guid nodeUid in source.Nodes)
                    nodeIds.Add(NodeNumber(nodes, nodeUid));

                model.Plates.Add(new Plate
                {
                    Id = next++,
                    Uid = source.Uid == Guid.Empty ? null : source.Uid,
                    NodeIds = nodeIds,
                    SurfacePropertyId = surfaceId,
                    MaterialId = source.Material.HasValue
                                 && materials.TryGetValue(source.Material.Value, out int material)
                        ? material
                        : null,
                });
            }
        }

        private static void ReadSupports(ReferenceDocument document, FemexModel model,
                                         Dictionary<Guid, Node> nodes)
        {
            int next = 1;

            foreach (ReferenceSupport source in document.Supports)
            {
                var nodeIds = new List<int>();
                foreach (Guid nodeUid in source.Nodes)
                    nodeIds.Add(NodeNumber(nodes, nodeUid));

                model.Supports.Add(new Support
                {
                    Id = next++,
                    Uid = source.Uid == Guid.Empty ? null : source.Uid,
                    Target = SupportTarget.Point,
                    NodeIds = nodeIds,
                    Ux = new Restraint(source.Ux),
                    Uy = new Restraint(source.Uy),
                    Uz = new Restraint(source.Uz),
                    Rx = new Restraint(source.Rx),
                    Ry = new Restraint(source.Ry),
                    Rz = new Restraint(source.Rz),
                });
            }
        }

        // ================= Shared =================

        private static bool Resists(Restraint restraint) => restraint.Fixed || restraint.Stiffness.HasValue;

        private static List<Restraint> Restraints(Support support)
        {
            return new List<Restraint> { support.Ux, support.Uy, support.Uz,
                                         support.Rx, support.Ry, support.Rz };
        }

        private static Guid Lookup(Dictionary<int, Guid> uids, int nodeNumber)
        {
            return uids.TryGetValue(nodeNumber, out Guid uid) ? uid : Guid.Empty;
        }

        private static int NodeNumber(Dictionary<Guid, Node> nodes, Guid uid)
        {
            return nodes.TryGetValue(uid, out Node? node) ? node.NodeNumber : 0;
        }

        private static int NextElementId(FemexModel model)
        {
            int highest = 0;
            foreach (Bar bar in model.Bars)
                highest = Math.Max(highest, bar.Id);
            foreach (Plate plate in model.Plates)
                highest = Math.Max(highest, plate.Id);

            return highest + 1;
        }

        /// <summary>
        /// The placeholder §2.2 requires, and it is recognisable rather than merely
        /// present: a bar that was previously un-exportable must not become a bar
        /// carrying a confident, wrong self-weight, so the area is zero and the name
        /// says outright what it is.
        /// </summary>
        private static ReferenceSection Placeholder(ReferenceDocument document, Bar bar, string kind)
        {
            Guid uid = PlaceholderUid(document, bar, kind);
            ReferenceSection? existing = document.Sections.Find(s => s.Uid == uid);
            if (existing is not null)
                return existing;

            var placeholder = new ReferenceSection
            {
                Uid = uid,
                Name = NameSynthesis.For(FemexEntity.Section, uid),
                Area = 0.0,
            };

            document.Sections.Add(placeholder);
            return placeholder;
        }

        private static ReferenceMaterial PlaceholderMaterial(ReferenceDocument document, Bar bar)
        {
            Guid uid = PlaceholderUid(document, bar, "Material");
            ReferenceMaterial? existing = document.Materials.Find(m => m.Uid == uid);
            if (existing is not null)
                return existing;

            var placeholder = new ReferenceMaterial
            {
                Uid = uid,
                Name = NameSynthesis.For(FemexEntity.Material, uid),
            };

            document.Materials.Add(placeholder);
            return placeholder;
        }

        /// <summary>
        /// A placeholder's identity, derived from the bar's own uid and the kind so
        /// that a second export of the same model invents the same placeholder rather
        /// than a new one — the stability §5.4 demands of names, applied to the thing
        /// a name is derived from.
        /// </summary>
        private static Guid PlaceholderUid(ReferenceDocument document, Bar bar, string kind)
        {
            byte[] bytes = (bar.Uid ?? Guid.Empty).ToByteArray();
            byte tag = kind == "Section" ? (byte)0x51 : (byte)0x4D;
            bytes[0] ^= tag;
            return new Guid(bytes);
        }

        private static void Handle(Dictionary<Guid, string> handles, Guid uid, string handle)
        {
            if (uid != Guid.Empty)
                handles[uid] = handle;
        }
    }
}
