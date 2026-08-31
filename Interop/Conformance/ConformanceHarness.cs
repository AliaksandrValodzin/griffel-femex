using System.Text.RegularExpressions;
using System.Threading;
using griffel_femex.Comparison;
using griffel_femex.Geometry;
using griffel_femex.Geometry.Sections;
using griffel_femex.Geometry.Surfaces;
using griffel_femex.Loads;
using griffel_femex.Materials;

namespace griffel_femex.Interop.Conformance
{
    /// <summary>
    /// The Tier-1 conformance suite: every rule <c>FEMEX_Adapters.md</c> §7.3 lists,
    /// run against any adapter, on any machine, with nothing installed.
    ///
    /// <b>Why a base class rather than a checklist.</b> A rule an adapter author has
    /// to remember to write a test for is a rule adapter #5 skips by not writing it.
    /// Inheriting <see cref="RunTier1"/> means the reverse: a check added here runs
    /// against every adapter that already exists, and an adapter cannot opt out of
    /// one by omission. The suite is deliberately a single entry point for that
    /// reason — there is no per-rule test to leave unwritten.
    ///
    /// <b>Why the tiers are named separately.</b> §7.1's full round trip through a
    /// live program needs Robot's COM server, the ETABS OAPI or an RFEM endpoint,
    /// installed and licensed. A single undifferentiated suite gets skipped wholesale
    /// on every machine lacking any one of them, which means Tier 1 stops running
    /// too. Everything here touches no native API.
    ///
    /// <b>What it does not do.</b> It has no opinion about how failures are reported.
    /// It returns results; a test project turns them into whatever its framework
    /// wants. That keeps the harness in the core library, beside the contract it
    /// enforces, with no test-framework dependency travelling with the shipped
    /// assembly.
    /// </summary>
    public abstract class ConformanceHarness
    {
        private static readonly Regex SynthesisedName = new Regex(@"^[A-Za-z]+-[0-9a-f]{8}$",
                                                                  RegexOptions.CultureInvariant);

        /// <summary>The adapter under test. Called once per check, so it may be fresh each time.</summary>
        protected abstract IFemexAdapter CreateAdapter();

        /// <summary>How to get a model out and back. See <see cref="ConformanceTransport"/>.</summary>
        protected abstract ConformanceTransport CreateTransport();

        /// <summary>
        /// A complete FEMEX model to push through the adapter — §7.7's golden file,
        /// read fresh each time so that one check's mutations cannot reach another's.
        /// </summary>
        protected abstract FemexModel CreateGoldenModel();

        /// <summary>
        /// A native fixture to import, or null if the adapter cannot import.
        /// Defaults to whatever exporting <see cref="CreateGoldenModel"/> produces,
        /// which is the only fixture a harness can build without knowing the format.
        /// </summary>
        protected virtual ImportRequest? CreateNativeFixture(ConformanceTransport transport)
        {
            if (CreateAdapter() is not IFemexExporter exporter)
                return null;

            exporter.Export(CreateGoldenModel(), transport.BeginExport(), null, CancellationToken.None);
            return transport.BeginImport();
        }

        /// <summary>
        /// Every Tier-1 rule, run in order. Nothing here throws for a
        /// non-conformance: a failure is a result, so one bad rule does not hide the
        /// other five.
        /// </summary>
        public IReadOnlyList<ConformanceCheck> RunTier1()
        {
            return new[]
            {
                CheckNullTolerance(),
                CheckNoSecondGate(),
                CheckMessageAnchoring(),
                CheckNameStability(),
                CheckCapabilityHonesty(),
                CheckTwoPhaseSynthesis(),
                CheckLossCoverage(),
                CheckImportedValidity(),
            };
        }

        // ----- §7.3, rule 1: null tolerance -----

        /// <summary>
        /// §2.1's nullable surface, fed to the adapter one model at a time.
        ///
        /// The failure this removes is the naive shape — assume completeness,
        /// dereference everything, throw on null — which is precisely what you get if
        /// plugin #1 is written before the contract exists, because from inside a
        /// plugin every null looks like a bug in the caller. The motivating case is
        /// not exotic: three levels laid out in the editor and handed to ETABS, with
        /// no sections, no load cases and no supports, is exactly the model a user
        /// wants to send.
        /// </summary>
        private ConformanceCheck CheckNullTolerance()
        {
            const string name = "Null tolerance";
            const string rule = "Every nullable in §2.1's table is fed as null and the adapter does not throw.";

            if (CreateAdapter() is not IFemexExporter exporter)
                return ConformanceCheck.Skip(name, rule, "The adapter does not export.");

            var findings = new List<string>();

            foreach (var (what, model) in NullCases())
            {
                try
                {
                    TransferResult<ExportReceipt> result =
                        exporter.Export(model, CreateTransport().BeginExport(), null, CancellationToken.None);

                    if (!result.Succeeded && result.Messages.Count == 0)
                        findings.Add($"{what}: the export failed and said nothing about why.");
                }
                catch (Exception exception)
                {
                    findings.Add($"{what}: threw {exception.GetType().Name} — {exception.Message}");
                }
            }

            return findings.Count == 0
                ? ConformanceCheck.Pass(name, rule)
                : ConformanceCheck.Fail(name, rule, findings);
        }

        private IEnumerable<(string What, FemexModel Model)> NullCases()
        {
            yield return ("no unit convention", Mutate(m => m.Units = null));
            yield return ("no mesh", Mutate(m => m.Mesh = null));
            yield return ("no metadata", Mutate(m => m.Metadata = null));

            yield return ("no names anywhere", Mutate(StripNames));

            yield return ("no round-trip identity", Mutate(m =>
            {
                foreach (var (entity, _, _) in m.EnumerateIdentified())
                    entity.Uid = null;
            }));

            yield return ("a plate with no property and no material", Mutate(m =>
            {
                foreach (Plate plate in m.Plates)
                {
                    plate.SurfacePropertyId = null;
                    plate.MaterialId = null;
                }
            }));

            // §2.2: the bar is the exception, and it fails on the contract's own
            // motivating example. A bar drawn before a section was chosen carries
            // SectionId 0, resolves to nothing, and is the three-levels-then-ETABS
            // handoff blocked.
            yield return ("bars with no section and no material", Mutate(m =>
            {
                foreach (Bar bar in m.Bars)
                {
                    bar.SectionId = 0;
                    bar.MaterialId = 0;
                }
            }));

            yield return ("geometry authored, analysis not set up", Mutate(m =>
            {
                m.LoadCases.Clear();
                m.Loads.Clear();
                m.LoadCombinations.Clear();
                m.Supports.Clear();
                m.Hinges.Clear();
            }));

            yield return ("an empty model", new FemexModel());
        }

        // ----- §7.3, rule 5: no second gate -----

        /// <summary>
        /// §2.3: an adapter accepts every model that passes
        /// <c>Validate(ValidationSeverity.Error)</c>, including deliberately
        /// incomplete ones, and defines no notion of "ready" of its own.
        ///
        /// The test model is the founding example — levels, nodes and a panel, with
        /// no sections, no load cases and no supports — checked first to be free of
        /// errors, so that a refusal cannot be defended as the model being invalid.
        /// </summary>
        private ConformanceCheck CheckNoSecondGate()
        {
            const string name = "No second gate";
            const string rule = "Every model that passes Validate(Error) is accepted, incomplete or not.";

            if (CreateAdapter() is not IFemexExporter exporter)
                return ConformanceCheck.Skip(name, rule, "The adapter does not export.");

            FemexModel halfDrawn = HalfDrawnModel();
            var errors = new List<ValidationMessage>(halfDrawn.Validate(ValidationSeverity.Error));
            if (errors.Count > 0)
            {
                return ConformanceCheck.Fail(name, rule, new[]
                {
                    "The harness's own half-drawn model does not pass Validate(Error), so this check " +
                    $"cannot mean anything: {errors[0].Text}",
                });
            }

            try
            {
                TransferResult<ExportReceipt> result =
                    exporter.Export(halfDrawn, CreateTransport().BeginExport(), null, CancellationToken.None);

                if (result.Succeeded)
                    return ConformanceCheck.Pass(name, rule);

                var findings = new List<string>
                {
                    "A model with no sections, no load cases and no supports was refused, which is the " +
                    "half-drawn handoff §2.1 exists to protect.",
                };

                foreach (TransferMessage message in result.Messages)
                {
                    if (message.Severity == ValidationSeverity.Error)
                        findings.Add(message.Text);
                }

                return ConformanceCheck.Fail(name, rule, findings);
            }
            catch (Exception exception)
            {
                return ConformanceCheck.Fail(name, rule, new[]
                {
                    $"A half-drawn model threw {exception.GetType().Name}: {exception.Message}",
                });
            }
        }

        // ----- §7.3, rule 2: message anchoring -----

        /// <summary>
        /// Every message names its object, and names it in a way something can act
        /// on: an <see cref="ObjectRef"/> that resolves in the model the message is
        /// about, not prose. A UI has to highlight it and a round-trip test has to
        /// match it, and neither can do that with a sentence.
        /// </summary>
        private ConformanceCheck CheckMessageAnchoring()
        {
            const string name = "Message anchoring";
            const string rule = "Every message carries an ObjectRef that resolves in the model it is about.";

            var findings = new List<string>();
            ConformanceTransport transport = CreateTransport();
            FemexModel source = CreateGoldenModel();

            if (CreateAdapter() is IFemexExporter exporter)
            {
                TransferResult<ExportReceipt> exported =
                    exporter.Export(source, transport.BeginExport(), null, CancellationToken.None);
                Anchored(findings, "export", exported.Messages, source);
            }

            if (CreateAdapter() is IFemexImporter importer)
            {
                ImportRequest? fixture = CreateNativeFixture(CreateTransport());
                if (fixture is not null)
                {
                    TransferResult<FemexModel> imported =
                        importer.Import(fixture, null, CancellationToken.None);

                    if (imported.Value is not null)
                        Anchored(findings, "import", imported.Messages, imported.Value);
                }
            }

            return findings.Count == 0
                ? ConformanceCheck.Pass(name, rule)
                : ConformanceCheck.Fail(name, rule, findings);
        }

        private static void Anchored(List<string> findings, string leg,
                                     IReadOnlyList<TransferMessage> messages, FemexModel model)
        {
            var anchors = new List<ObjectRef>();
            foreach (var (_, reference, _) in model.EnumerateIdentified())
                anchors.Add(reference);

            foreach (TransferMessage message in messages)
            {
                // Model-level facts and failures may go unanchored; see
                // TransferMessage.ModelLoss for the three that are.
                if (message.Subject is not ObjectRef subject)
                    continue;

                // A per-concept report, which §4.4 asks for by name.
                if (!subject.Id.HasValue && !subject.Uid.HasValue)
                    continue;

                if (!anchors.Exists(anchor => Matches(subject, anchor)))
                {
                    findings.Add($"On {leg}, a message names {subject}, which is not in the model it " +
                                 $"is about: \"{message.Text}\"");
                }
            }
        }

        /// <summary>
        /// Whether one <see cref="ObjectRef"/> is about the other, on the keys it
        /// actually states.
        ///
        /// Stating one key and not the other is normal and is not vagueness: a
        /// message written before <c>AssignMissingUids</c> has an id and no uid, one
        /// about an object matched across a crossing has a uid and an id that
        /// renumbered, and a per-concept report has neither. So a stated key must
        /// agree and an unstated one says nothing — which makes "both null" the
        /// entity-wide case rather than a special case.
        /// </summary>
        private static bool Matches(ObjectRef reference, ObjectRef anchor)
        {
            if (reference.Entity != anchor.Entity)
                return false;

            if (reference.Id.HasValue && anchor.Id.HasValue && reference.Id != anchor.Id)
                return false;

            if (reference.Uid.HasValue && anchor.Uid.HasValue && reference.Uid != anchor.Uid)
                return false;

            return true;
        }

        // ----- §7.3, rule 3: name stability -----

        /// <summary>
        /// Export twice; the synthesised names must be identical, and must be §5.4's
        /// <c>{Kind}-{8 hex}</c> where the source was null.
        ///
        /// The failure is a counter: a second export yielding <c>Section1_2</c> and a
        /// third <c>Section1_2_2</c> means the Robot label a firm's model is keyed by
        /// changes every time somebody re-exports.
        /// </summary>
        private ConformanceCheck CheckNameStability()
        {
            const string name = "Name stability";
            const string rule = "Synthesised names are identical across exports and take §5.4's form.";

            if (CreateAdapter() is not IFemexExporter exporter)
                return ConformanceCheck.Skip(name, rule, "The adapter does not export.");

            FemexModel first = Mutate(StripNames);
            FemexModel second = Mutate(StripNames);

            exporter.Export(first, CreateTransport().BeginExport(), null, CancellationToken.None);
            exporter.Export(second, CreateTransport().BeginExport(), null, CancellationToken.None);

            var findings = new List<string>();
            List<(string What, string? Name)> left = SynthesisedNames(first);
            List<(string What, string? Name)> right = SynthesisedNames(second);

            for (int i = 0; i < left.Count && i < right.Count; i++)
            {
                if (left[i].Name is null)
                {
                    findings.Add($"{left[i].What} was left nameless, and the target keys by name.");
                    continue;
                }

                if (!string.Equals(left[i].Name, right[i].Name, StringComparison.Ordinal))
                {
                    findings.Add($"{left[i].What} was named \"{left[i].Name}\" on one export and " +
                                 $"\"{right[i].Name}\" on the next.");
                    continue;
                }

                if (!SynthesisedName.IsMatch(left[i].Name!))
                {
                    findings.Add($"{left[i].What} was named \"{left[i].Name}\", which is not §5.4's " +
                                 "{Kind}-{8 hex} form, so a reader cannot tell it was invented.");
                }
            }

            return findings.Count == 0
                ? ConformanceCheck.Pass(name, rule)
                : ConformanceCheck.Fail(name, rule, findings);
        }

        /// <summary>
        /// The six name-keyed families §5.5 extends the rule to — the validator's
        /// four, plus levels and plates, because a storey is name-keyed in ETABS and
        /// Robot every bit as much as a section is.
        /// </summary>
        private static List<(string What, string? Name)> SynthesisedNames(FemexModel model)
        {
            var names = new List<(string, string?)>();

            foreach (Section section in model.Sections)
                names.Add(($"Section {section.Id}", section.Name));
            foreach (SurfaceProperty surface in model.SurfaceProperties)
                names.Add(($"Surface property {surface.Id}", surface.Name));
            foreach (Material material in model.Materials)
                names.Add(($"Material {material.Id}", material.Name));
            foreach (LoadCase loadCase in model.LoadCases)
                names.Add(($"Load case {loadCase.Number}", loadCase.Label));
            foreach (Level level in model.Levels)
                names.Add(($"Level {level.LevelNumber}", level.Name));
            foreach (Plate plate in model.Plates)
                names.Add(($"Plate {plate.Id}", plate.Name));

            return names;
        }

        // ----- §7.3, rule 4: capability honesty -----

        /// <summary>
        /// The declaration matches what the adapter actually does, entity by entity —
        /// checkable only because §3.3 fixed the vocabulary.
        ///
        /// Two halves, and the second is the one that matters. Producing an entity
        /// you did not declare is a host offering the wrong menu. <b>Not carrying an
        /// entity you did not declare, and saying nothing, is a silent loss</b> — so
        /// an undeclared export direction obliges a message about that entity, which
        /// is the same obligation §4 puts on every other loss.
        /// </summary>
        private ConformanceCheck CheckCapabilityHonesty()
        {
            const string name = "Capability honesty";
            const string rule = "The declaration matches what crosses, and what does not cross is reported.";

            IFemexAdapter adapter = CreateAdapter();
            AdapterCapabilities capabilities = adapter.Capabilities;
            var findings = new List<string>();

            if (adapter is IFemexExporter exporter)
            {
                FemexModel source = CreateGoldenModel();
                TransferResult<ExportReceipt> result =
                    exporter.Export(source, CreateTransport().BeginExport(), null, CancellationToken.None);

                foreach (var (entity, count) in Populations(source))
                {
                    if (count == 0 || capabilities.Supports(entity, TransferDirection.Export))
                        continue;

                    if (!Mentions(result.Messages, entity))
                    {
                        findings.Add($"{count} {entity} object(s) were in the model, the adapter does " +
                                     "not declare Export for them, and no message says they did not " +
                                     "cross.");
                    }
                }
            }

            if (adapter is IFemexImporter importer)
            {
                ImportRequest? fixture = CreateNativeFixture(CreateTransport());
                if (fixture is not null)
                {
                    TransferResult<FemexModel> result = importer.Import(fixture, null, CancellationToken.None);
                    if (result.Value is not null)
                    {
                        foreach (var (entity, count) in Populations(result.Value))
                        {
                            if (count > 0 && !capabilities.Supports(entity, TransferDirection.Import))
                            {
                                findings.Add($"The import produced {count} {entity} object(s) and the " +
                                             "adapter does not declare Import for them, so a host would " +
                                             "not have offered this.");
                            }
                        }
                    }
                }
            }

            return findings.Count == 0
                ? ConformanceCheck.Pass(name, rule)
                : ConformanceCheck.Fail(name, rule, findings);
        }

        private static bool Mentions(IReadOnlyList<TransferMessage> messages, FemexEntity entity)
        {
            foreach (TransferMessage message in messages)
            {
                if (message.Subject is ObjectRef subject && subject.Entity == entity)
                    return true;
            }

            return false;
        }

        private static IEnumerable<(FemexEntity Entity, int Count)> Populations(FemexModel model)
        {
            yield return (FemexEntity.Grid, model.Grids.Count);
            yield return (FemexEntity.Level, model.Levels.Count);
            yield return (FemexEntity.Node, model.Nodes.Count);
            yield return (FemexEntity.Section, model.Sections.Count);
            yield return (FemexEntity.SurfaceProperty, model.SurfaceProperties.Count);
            yield return (FemexEntity.Bar, model.Bars.Count);
            yield return (FemexEntity.Plate, model.Plates.Count);
            yield return (FemexEntity.Material, model.Materials.Count);
            yield return (FemexEntity.LoadGroup, model.LoadGroups.Count);
            yield return (FemexEntity.LoadCase, model.LoadCases.Count);
            yield return (FemexEntity.Load, model.Loads.Count);
            yield return (FemexEntity.LoadCombination, model.LoadCombinations.Count);
            yield return (FemexEntity.Support, model.Supports.Count);
            yield return (FemexEntity.Hinge, model.Hinges.Count);
            yield return (FemexEntity.Mesh, model.Mesh is null ? 0 : 1);
        }

        // ----- §7.3, rule 6: two-phase synthesis -----

        /// <summary>
        /// §6.2: the same native model read in two different orders yields identical
        /// node and level tables.
        ///
        /// The bug it catches is subtle and certain to be rediscovered otherwise.
        /// <c>GetCoincidenceTolerance</c> is 1e-6 of the model's <i>current</i>
        /// bounding diagonal, so an import that starts from an empty model begins at
        /// the floor and grows as the model fills: the first nodes are matched
        /// against a far tighter test than the last. Two nodes kept apart early can
        /// be coincident by the time the model is finished — which the validator will
        /// report afterwards, as a warning, which is a diagnosis and not a fix.
        /// </summary>
        private ConformanceCheck CheckTwoPhaseSynthesis()
        {
            const string name = "Two-phase synthesis";
            const string rule = "The same source read in two orders yields identical node and level tables.";

            if (CreateAdapter() is not IFemexImporter importer)
                return ConformanceCheck.Skip(name, rule, "The adapter does not import.");

            ConformanceTransport transport = CreateTransport();
            ImportRequest? forward = CreateNativeFixture(transport);
            if (forward is null)
                return ConformanceCheck.Skip(name, rule, "The harness could not build a native fixture.");

            if (!transport.TryBeginReorderedImport(out ImportRequest? reversed) || reversed is null)
            {
                return ConformanceCheck.Skip(name, rule,
                    "The transport cannot present the same source in a different order, so the rule " +
                    "cannot be tested here. It still binds the adapter.");
            }

            TransferResult<FemexModel> first = importer.Import(forward, null, CancellationToken.None);
            TransferResult<FemexModel> second = importer.Import(reversed, null, CancellationToken.None);

            if (first.Value is null || second.Value is null)
                return ConformanceCheck.Fail(name, rule, new[] { "One of the two imports produced nothing." });

            var findings = new List<string>();
            CompareTables(findings, "level", Table(first.Value.Levels,
                                                   l => $"{l.LevelNumber} @ {l.AbsoluteElevation}"),
                          Table(second.Value.Levels, l => $"{l.LevelNumber} @ {l.AbsoluteElevation}"));
            CompareTables(findings, "node", Table(first.Value.Nodes,
                                                  n => $"{n.NodeNumber} @ {n.X},{n.Y},{n.LevelNumber}+{n.VerticalOffset}"),
                          Table(second.Value.Nodes,
                                n => $"{n.NodeNumber} @ {n.X},{n.Y},{n.LevelNumber}+{n.VerticalOffset}"));

            return findings.Count == 0
                ? ConformanceCheck.Pass(name, rule)
                : ConformanceCheck.Fail(name, rule, findings);
        }

        private static List<string> Table<T>(List<T> entities, Func<T, string> render)
        {
            var rows = new List<string>(entities.Count);
            foreach (T entity in entities)
                rows.Add(render(entity));

            rows.Sort(StringComparer.Ordinal);
            return rows;
        }

        private static void CompareTables(List<string> findings, string what,
                                          List<string> first, List<string> second)
        {
            if (first.Count != second.Count)
            {
                findings.Add($"The {what} table has {first.Count} row(s) read one way and " +
                             $"{second.Count} the other.");
                return;
            }

            for (int i = 0; i < first.Count; i++)
            {
                if (!string.Equals(first[i], second[i], StringComparison.Ordinal))
                {
                    findings.Add($"The {what} tables differ: \"{first[i]}\" against \"{second[i]}\".");
                    return;
                }
            }
        }

        // ----- §7.1: the loss report is the test specification -----

        /// <summary>
        /// Round-trip the golden model and assert that <b>every</b> difference
        /// between it and what came back is covered by a reported message.
        ///
        /// This is the assertion that turns §2.4 from a matter of plugin-author
        /// diligence into something a suite can fail on, and it is the only reason to
        /// believe an adapter will report its losses at all. An undeclared difference
        /// is a bug; a declared one is the adapter working as designed.
        ///
        /// Here in Tier 1 because the reference adapter is offline. For a live
        /// adapter it is Tier 2 and needs the program, which is why the harness runs
        /// it only when the transport can actually complete a round trip.
        ///
        /// <b>What counts as covered</b> is deliberately stated rather than left to
        /// the comparison loop: a message whose subject is exactly the difference's,
        /// or one anchored to the difference's entity kind with no id — the
        /// per-concept report §4.4 asks for by name — or, for a difference about the
        /// model itself, any message about the model itself.
        /// </summary>
        private ConformanceCheck CheckLossCoverage()
        {
            const string name = "Loss coverage";
            const string rule = "Every difference between a model and its round trip is named by a message.";

            IFemexAdapter adapter = CreateAdapter();
            if (adapter is not IFemexExporter exporter || adapter is not IFemexImporter importer)
                return ConformanceCheck.Skip(name, rule, "The adapter does not do both legs.");

            ConformanceTransport transport = CreateTransport();
            FemexModel source = CreateGoldenModel();

            TransferResult<ExportReceipt> exported =
                exporter.Export(source, transport.BeginExport(), null, CancellationToken.None);
            if (!exported.Succeeded)
                return ConformanceCheck.Fail(name, rule, new[] { "The export leg produced nothing." });

            TransferResult<FemexModel> imported =
                importer.Import(transport.BeginImport(), null, CancellationToken.None);
            if (imported.Value is null)
                return ConformanceCheck.Fail(name, rule, new[] { "The import leg produced nothing." });

            var declared = new List<TransferMessage>(exported.Messages);
            declared.AddRange(imported.Messages);

            var findings = new List<string>();
            foreach (ModelDifference difference in ModelDiff.Compare(source, imported.Value))
            {
                if (!Covered(declared, difference))
                    findings.Add($"Undeclared: {difference.Text}");
            }

            return findings.Count == 0
                ? ConformanceCheck.Pass(name, rule)
                : ConformanceCheck.Fail(name, rule, findings);
        }

        private static bool Covered(List<TransferMessage> messages, ModelDifference difference)
        {
            foreach (TransferMessage message in messages)
            {
                if (difference.Subject is not ObjectRef subject)
                {
                    // A difference about the model is covered by a message about the
                    // model — the three facts §3.3 keeps out of the entity vocabulary.
                    if (message.Subject is null)
                        return true;

                    continue;
                }

                if (message.Subject is ObjectRef anchor && Matches(subject, anchor))
                    return true;
            }

            return false;
        }

        // ----- §7.3: an adapter does not manufacture a defect and stay quiet -----

        /// <summary>
        /// Import a native fixture and assert that the model that comes back carries
        /// no <see cref="ValidationSeverity.Error"/> finding that no
        /// <see cref="TransferMessage"/> names.
        ///
        /// <b>Why this is not covered by <see cref="CheckLossCoverage"/>.</b> That
        /// check compares a model against its round trip, so it only ever sees
        /// <i>differences</i>. An adapter can emit an internally inconsistent model
        /// and still round-trip it perfectly — the SAF adapter did, for eleven of the
        /// house workbook's loads: a line load hosted on a plate edge, given bar-only
        /// properties it could not resolve, was written back out exactly as it came
        /// in. Equivalence modulo declared losses held; the model was invalid.
        ///
        /// <b>Why Tier 1 rather than each adapter's own suite.</b> It is a property
        /// of every adapter, and §7.3's whole design is that a later adapter inherits
        /// the rules rather than choosing which tests to write.
        ///
        /// <b>What counts as named</b> is deliberately generous, and stated rather
        /// than left to the loop, because a validation finding carries no
        /// <see cref="ObjectRef"/> of its own — it is a sentence. A message names a
        /// finding when the finding's text contains a token that message supplies:
        /// its native handle, or its subject written the way the validator writes
        /// that entity — <c>"Section 26"</c>, <c>"Support 3"</c>. Generous on
        /// purpose: the failure worth catching is silence, and a check that fails on
        /// a message merely worded differently would be turned off.
        ///
        /// A warning is not enough to fail on. §2.4's obligation is about what did
        /// not cross, and an adapter reporting an imperfect but usable model is doing
        /// its job; an <i>error</i> is the model saying there is nothing a receiver
        /// can fall back on, and one nobody mentioned is the adapter's own.
        /// </summary>
        private ConformanceCheck CheckImportedValidity()
        {
            const string name = "Imported validity";
            const string rule = "No Error-severity finding on an imported model goes unnamed by a message.";

            IFemexAdapter adapter = CreateAdapter();
            if (adapter is not IFemexImporter importer)
                return ConformanceCheck.Skip(name, rule, "The adapter does not import.");

            ConformanceTransport transport = CreateTransport();
            var declared = new List<TransferMessage>();
            ImportRequest? fixture;

            if (adapter is IFemexExporter exporter)
            {
                // Both legs, so that a loss declared on the way out excuses the
                // finding it caused on the way back. The fixture is our own export
                // either way; taking it through the exporter here is what keeps its
                // messages, which CreateNativeFixture discards.
                TransferResult<ExportReceipt> exported =
                    exporter.Export(CreateGoldenModel(), transport.BeginExport(), null, CancellationToken.None);
                if (!exported.Succeeded)
                    return ConformanceCheck.Fail(name, rule, new[] { "The export leg produced nothing." });

                declared.AddRange(exported.Messages);
                fixture = transport.BeginImport();
            }
            else
            {
                fixture = CreateNativeFixture(transport);
                if (fixture is null)
                    return ConformanceCheck.Skip(name, rule, "The adapter has no native fixture to import.");
            }

            TransferResult<FemexModel> imported =
                importer.Import(fixture, null, CancellationToken.None);
            if (imported.Value is null)
                return ConformanceCheck.Fail(name, rule, new[] { "The import leg produced nothing." });

            declared.AddRange(imported.Messages);

            var findings = new List<string>();
            foreach (ValidationMessage finding in imported.Value.Validate(ValidationSeverity.Error))
            {
                if (!Named(declared, finding.Text))
                    findings.Add($"Undeclared: {finding.Text}");
            }

            return findings.Count == 0
                ? ConformanceCheck.Pass(name, rule)
                : ConformanceCheck.Fail(name, rule, findings);
        }

        /// <summary>True when any message supplies a token the finding's text uses.</summary>
        private static bool Named(List<TransferMessage> messages, string finding)
        {
            foreach (TransferMessage message in messages)
            {
                if (ContainsToken(finding, message.NativeHandle))
                    return true;

                // A message about a kind of thing rather than a thing — §4.4's
                // per-concept report — supplies no token, and does not count. It
                // cannot: "Load" appears in every message about every load.
                if (message.Subject is ObjectRef subject && subject.Id is int id &&
                    ContainsToken(finding, $"{subject.Entity} {id}"))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Whole-token containment, so that handle <c>B5</c> does not answer for a
        /// finding about <c>B51</c>.
        /// </summary>
        private static bool ContainsToken(string text, string? token)
        {
            if (string.IsNullOrEmpty(token))
                return false;

            int at = text.IndexOf(token!, StringComparison.Ordinal);
            while (at >= 0)
            {
                bool before = at == 0 || !char.IsLetterOrDigit(text[at - 1]);
                int after = at + token!.Length;
                if (before && (after == text.Length || !char.IsLetterOrDigit(text[after])))
                    return true;

                at = text.IndexOf(token!, at + 1, StringComparison.Ordinal);
            }

            return false;
        }

        // ----- Fixtures -----

        /// <summary>A fresh golden model with one thing changed, so checks cannot leak into each other.</summary>
        private FemexModel Mutate(Action<FemexModel> change)
        {
            FemexModel model = CreateGoldenModel();
            change(model);
            return model;
        }

        private static void StripNames(FemexModel model)
        {
            foreach (Section section in model.Sections)
                section.Name = null;
            foreach (SurfaceProperty surface in model.SurfaceProperties)
                surface.Name = null;
            foreach (Material material in model.Materials)
                material.Name = null;
            foreach (LoadCase loadCase in model.LoadCases)
                loadCase.Label = null;
            foreach (Level level in model.Levels)
                level.Name = null;
            foreach (Plate plate in model.Plates)
                plate.Name = null;
        }

        /// <summary>
        /// §2.1's motivating case, built rather than borrowed: three levels, a slab
        /// on the top one, and nothing else. <b>No sections, no bars, no load cases,
        /// no supports, no combinations.</b> It is incomplete, it passes
        /// <c>Validate(Error)</c>, and it is exactly what a user wants to send.
        ///
        /// The slab carries a surface property and a material because a structural
        /// plate without them is an <i>Error</i> today, not a warning —
        /// <c>ValidateSurfaces</c> says so — and this check must not be able to be
        /// defended as the model having been invalid. What is missing here is
        /// everything §2.1 actually names: the analysis, not the geometry.
        /// </summary>
        private static FemexModel HalfDrawnModel()
        {
            var model = new FemexModel
            {
                SchemaVersion = FemexModel.CurrentSchemaVersion,
                Units = new Units(LengthUnit.Metre, ForceUnit.Kilonewton),
            };

            for (int i = 0; i < 3; i++)
                model.Levels.Add(new Level(i, null, i * 3.5, i * 3.5, i == 0));

            model.Nodes.Add(new Node(1, 0.0, 0.0, 2));
            model.Nodes.Add(new Node(2, 6.0, 0.0, 2));
            model.Nodes.Add(new Node(3, 6.0, 4.0, 2));
            model.Nodes.Add(new Node(4, 0.0, 4.0, 2));

            model.SurfaceProperties.Add(new ConstantThickness(1, "Slab 200", 0.2));
            model.Materials.Add(new Material
            {
                Id = 1,
                Name = "C30/37",
                ModulusOfElasticity = 33000000.0,
                PoissonsRatio = 0.2,
                Density = 2.5,
            });

            model.Plates.Add(new Plate(1, new List<int> { 1, 2, 3, 4 }, 1, 1));

            return model;
        }
    }
}
