using griffel_femex.Geometry.Sections;
using griffel_femex.Geometry.Surfaces;
using griffel_femex.Loads;
using griffel_femex.Materials;
using Xunit;

namespace griffel_femex.Tests
{
    /// <summary>
    /// The optional <c>uid</c>, <c>Load.Id</c>, and the 1.2 → 1.3 migration that
    /// gives a pre-1.3 file's loads the ids they never had.
    ///
    /// The thing every fact here is ultimately about is the failure the feature
    /// exists to remove: a program exports to FEMEX, the file is edited elsewhere,
    /// the program re-imports it — and without a uid it cannot tell that the
    /// returning object is the one it exported, so it appends a duplicate instead of
    /// merging.
    /// </summary>
    public class RoundTripIdentityTests
    {
        // ----- The uid itself -----

        [Fact]
        public void Uid_SurvivesARoundTrip()
        {
            var model = SampleModels.Build();
            var uid = Guid.NewGuid();
            model.Column().Uid = uid;

            var restored = FemexModel.FromJson(model.ToJson());

            Assert.Equal(uid, restored.Column().Uid);
        }

        [Fact]
        public void Uid_IsOmittedEntirely_WhenNull()
        {
            var model = SampleModels.Build();

            // Null is a truthful value, not a gap: it says this object has no
            // round-trip identity, which is the honest state of a hand-authored
            // file. WhenWritingNull means it costs the file nothing to say so.
            Assert.DoesNotContain("\"uid\"", model.ToJson());

            model.Column().Uid = Guid.NewGuid();
            Assert.Contains("\"uid\"", model.ToJson());
        }

        [Fact]
        public void Uid_IsWrittenAsTheCanonicalGuidString()
        {
            var model = SampleModels.Build();
            var uid = new Guid("3f2a1b4c-5d6e-4f70-8192-a3b4c5d6e7f8");
            model.Materials.Single().Uid = uid;

            Assert.Contains("\"uid\": \"3f2a1b4c-5d6e-4f70-8192-a3b4c5d6e7f8\"", model.ToJson());
        }

        [Fact]
        public void Uid_IsCarriedByEveryAuthoredEntity_AndByNoMeshObject()
        {
            var model = SampleModels.Build();
            model.AssignMissingUids();

            // One of each declaration site, reached through the concrete types.
            Assert.NotNull(model.PrimaryGrid().Uid);
            Assert.NotNull(model.Levels[0].Uid);
            Assert.NotNull(model.Nodes[0].Uid);
            Assert.NotNull(model.Sections[0].Uid);
            Assert.NotNull(model.SurfaceProperties[0].Uid);
            Assert.NotNull(model.Column().Uid);
            Assert.NotNull(model.Slab().Uid);
            Assert.NotNull(model.Slab().Regions[0].Uid);
            Assert.NotNull(model.Materials[0].Uid);
            Assert.NotNull(model.LoadCases[0].Uid);
            Assert.NotNull(model.Loads[0].Uid);
            Assert.NotNull(model.LoadCombinations[0].Uid);
            Assert.NotNull(model.Supports[0].Uid);
            Assert.NotNull(model.Hinges[0].Uid);

            // The mesh is regenerated wholesale, so a stable identity for a mesh
            // node or face would mean nothing. It has no uid to fill and therefore
            // never trips the partial-coverage warning either.
            Assert.Empty(model.Validate());
            Assert.DoesNotContain("uid", MeshSectionOf(model.ToJson()));
        }

        // ----- AssignMissingUids -----

        [Fact]
        public void AssignMissingUids_FillsEveryNull_AndReturnsTheCount()
        {
            var model = SampleModels.Build();

            int assigned = model.AssignMissingUids();

            Assert.True(assigned > 0);
            Assert.Equal(assigned, CountDistinctUids(model));
        }

        [Fact]
        public void AssignMissingUids_NeverOverwrites()
        {
            var model = SampleModels.Build();
            var anchor = Guid.NewGuid();
            model.Column().Uid = anchor;

            int assigned = model.AssignMissingUids();

            // The existing uid is the anchor the whole feature rests on: rewriting
            // it is the one thing that would destroy it.
            Assert.Equal(anchor, model.Column().Uid);
            Assert.Equal(CountDistinctUids(model) - 1, assigned);
        }

        [Fact]
        public void AssignMissingUids_IsIdempotent()
        {
            var model = SampleModels.Build();
            model.AssignMissingUids();

            Assert.Equal(0, model.AssignMissingUids());
        }

        [Fact]
        public void AssignMissingUids_SurvivesASaveAndReload()
        {
            // The round trip the whole change exists for, end to end.
            var model = SampleModels.Build();
            model.AssignMissingUids();

            var restored = FemexModel.FromJson(model.ToJson());

            Assert.Equal(model.Column().Uid, restored.Column().Uid);
            Assert.Equal(model.Slab().Regions[0].Uid, restored.Slab().Regions[0].Uid);
            Assert.Equal(model.Loads[3].Uid, restored.Loads[3].Uid);
            Assert.Equal(CountDistinctUids(model), CountDistinctUids(restored));

            // Nothing was minted on the way through: the reloaded model already
            // carries every uid, so there is nothing left to fill.
            Assert.Equal(0, restored.AssignMissingUids());
        }

        [Fact]
        public void ToJson_NeverMintsAUid()
        {
            // Auto-stamping on save was rejected: the same model built twice from
            // the same source would then carry different uids, and Example1.femex
            // could never be regenerated byte-identically.
            var model = SampleModels.Build();

            Assert.Equal(model.ToJson(), SampleModels.Build().ToJson());
            Assert.DoesNotContain("\"uid\"", model.ToJson());
        }

        // ----- Uid errors -----

        [Fact]
        public void Reports_AUidNamingTwoObjectsInDifferentCollections()
        {
            var model = SampleModels.Build();
            var uid = Guid.NewGuid();
            model.Column().Uid = uid;
            model.Materials.Single().Uid = uid;

            // Uniqueness is model-wide, not per collection: that is what a GUID
            // means, and a receiver merging by uid does not care which list an
            // object came from.
            AssertReports(model, $"Uid {uid} names both Bar 1 and Material 1; a uid names one object.");
        }

        [Fact]
        public void Reports_AUidNamingTwoObjectsInOneCollection()
        {
            var model = SampleModels.Build();
            var uid = Guid.NewGuid();
            model.Sections[0].Uid = uid;
            model.Sections[2].Uid = uid;

            AssertReports(model, "names both Section 1 and Section 3");
        }

        [Fact]
        public void Reports_TheNilUid()
        {
            var model = SampleModels.Build();
            model.Materials.Single().Uid = Guid.Empty;

            // Guid.Empty is the "I forgot" value, not an identity — and two objects
            // both carrying it would merge into one another.
            AssertReports(model, "Material 1 carries the nil uid");
        }

        [Fact]
        public void TheNilUid_IsNotAlsoReportedAsADuplicate()
        {
            var model = SampleModels.Build();
            model.Materials.Single().Uid = Guid.Empty;
            model.Column().Uid = Guid.Empty;

            var errors = model.Validate(ValidationSeverity.Error).ToList();

            Assert.Equal(2, errors.Count);
            Assert.All(errors, e => Assert.Contains("nil uid", e.Text));
        }

        // ----- Coverage -----

        [Fact]
        public void Warns_OnPartialUidCoverage()
        {
            var model = SampleModels.Build();
            int total = model.AssignMissingUids();
            model.Column().Uid = null;

            var warning = Assert.Single(
                model.Validate(ValidationSeverity.Warning),
                w => w.Text.Contains("carry a uid"));

            Assert.Equal(
                $"{total - 1} of {total} authored objects carry a uid; a receiving program merges those " +
                "and duplicates the rest.",
                warning.Text);
        }

        [Fact]
        public void DoesNotWarn_OnTheTwoNormalCoverageStates()
        {
            // Nothing carries one — a hand-authored file — and everything does, an
            // exported one. Both are complete answers, so both are silent.
            var none = SampleModels.Build();
            Assert.Empty(none.Validate());

            var all = SampleModels.Build();
            all.AssignMissingUids();
            Assert.Empty(all.Validate());
        }

        // ----- Names, which Robot and ETABS key by -----

        [Fact]
        public void Warns_OnABlankName()
        {
            AssertWarns(WithSection(s => s.Name = null), "Section 1 has no name; a program that keys sections by name will invent one.");
            AssertWarns(WithSection(s => s.Name = "   "), "Section 1 has no name");

            var surface = SampleModels.Build();
            surface.SurfaceProperties[0].Name = null;
            AssertWarns(surface, "Surface property 1 has no name; a program that keys surface properties by name will invent one.");

            var material = SampleModels.Build();
            material.Materials.Single().Name = null;
            AssertWarns(material, "Material 1 has no name; a program that keys materials by name will invent one.");

            var loadCase = SampleModels.Build();
            loadCase.LoadCases[0].Label = null;
            AssertWarns(loadCase, "Load case 1 has no label; a program that keys load cases by name will invent one.");
        }

        [Fact]
        public void Warns_OnADuplicatedName()
        {
            var sections = SampleModels.Build();
            sections.Sections[1].Name = sections.Sections[0].Name;
            AssertWarns(sections, "More than one section is named \"R300x500\". A program that keys sections by name cannot tell them apart.");

            var surfaces = SampleModels.Build();
            surfaces.SurfaceProperties[1].Name = "SLAB-250";
            AssertWarns(surfaces, "More than one surface property is named \"SLAB-250\".");

            var materials = SampleModels.Build();
            materials.Materials.Add(new Material(2, "Concrete C30", 33e9, 0.2, 2.5, 30e6));
            AssertWarns(materials, "More than one material is named \"Concrete C30\".");

            var cases = SampleModels.Build();
            cases.LoadCases[1].Label = "Dead";
            AssertWarns(cases, "More than one load case is labelled \"Dead\".");
        }

        [Fact]
        public void Warns_OnceForThreeSectionsSharingAName()
        {
            var model = SampleModels.Build();
            foreach (var section in model.Sections)
                section.Name = "SHARED";

            // The name is the subject, not the section, so three collisions produce
            // one message — the shape ValidateLoadCombinationUsage already uses.
            Assert.Single(model.Validate(ValidationSeverity.Warning), w => w.Text.Contains("More than one section"));
        }

        [Fact]
        public void ABlankName_IsNotAlsoADuplicate()
        {
            var model = SampleModels.Build();
            foreach (var section in model.Sections)
                section.Name = null;

            var warnings = model.Validate(ValidationSeverity.Warning).ToList();

            // An absent name is not a name three sections share.
            Assert.Equal(3, warnings.Count(w => w.Text.Contains("has no name")));
            Assert.DoesNotContain(warnings, w => w.Text.Contains("More than one section"));
        }

        [Fact]
        public void ANameIsOnlyEverAWarning()
        {
            // The half-step: the interop review wanted these names required. A
            // warning tells an author their material has no name; it does not stop
            // them shipping one, and every existing file stays valid at error level.
            var model = SampleModels.Build();
            model.Materials.Single().Name = null;
            model.Sections[0].Name = null;

            Assert.Empty(model.Validate(ValidationSeverity.Error));
        }

        // ----- Load.Id -----

        [Fact]
        public void Reports_ADuplicateLoadId()
        {
            var model = SampleModels.Build();
            model.Loads[1].Id = model.Loads[0].Id;

            AssertReports(model, "Duplicate load id 1.");
        }

        [Fact]
        public void LoadId_IsInItsOwnSpace()
        {
            // Beside Support.Id and Hinge.Id rather than in the shared element
            // space: a load is not an element, and nothing addresses it as one.
            var model = SampleModels.Build();
            model.Loads[0].Id = SampleModels.SlabId;

            Assert.Empty(model.Validate(ValidationSeverity.Error));
        }

        [Fact]
        public void LoadId_RoundTrips()
        {
            var restored = FemexModel.FromJson(SampleModels.Build().ToJson());

            Assert.Equal(new[] { 1, 2, 3, 4, 5, 6, 7 }, restored.Loads.Select(l => l.Id));
            Assert.Equal(3, restored.Loads.OfType<AreaLoad>().First(l => l.Label == "A1").Id);
        }

        // ----- The 1.2 -> 1.3 migration -----

        /// <summary>A 1.2 model, whose three loads therefore have no ids at all.</summary>
        private const string LegacyJson = """
            {
              "schemaVersion": "1.2",
              "loadCases": [
                { "number": 1, "label": "Dead", "nature": "Dead", "selfWeightFactor": 0 }
              ],
              "loads": [
                { "type": "temperature", "deltaT": 10, "label": "T1", "loadCaseNumber": 1 },
                { "type": "temperature", "deltaT": 20, "label": "T2", "loadCaseNumber": 1 },
                { "type": "temperature", "deltaT": 30, "label": "T3", "loadCaseNumber": 1 }
              ]
            }
            """;

        [Fact]
        public void LegacyLoads_AreNumberedInListOrder()
        {
            var model = FemexModel.FromJson(LegacyJson);

            // List position is the only identity a pre-1.3 load ever had, so it is
            // the only honest thing to number them by.
            Assert.Equal(new[] { 1, 2, 3 }, model.Loads.Select(l => l.Id));
            Assert.Equal(new[] { "T1", "T2", "T3" }, model.Loads.Select(l => l.Label));
        }

        [Fact]
        public void LegacyLoads_AreNotReportedAsDuplicates()
        {
            // The trap the migration exists to avoid: without it every load would
            // read back as id 0 and the duplicate check would report a flood of
            // errors on a file that was never wrong.
            var model = FemexModel.FromJson(LegacyJson);

            Assert.Empty(model.Validate(ValidationSeverity.Error));
        }

        [Fact]
        public void LegacyLoads_ReportTheirNumbering()
        {
            var model = FemexModel.FromJson(LegacyJson);

            AssertWarns(model, "This model was written before loads had ids; its 3 loads have been " +
                               "numbered 1–3 in list order. Re-saving the model writes those ids.");
        }

        [Fact]
        public void ALegacyFile_ReportsWhatItsVersionMeans()
        {
            var model = FemexModel.FromJson(LegacyJson);

            AssertWarns(model, "The model declares schemaVersion \"1.2\", written before round-trip " +
                               "identity existed, so nothing in it carries a uid");
        }

        [Fact]
        public void ALegacyFile_ReEmitsAs13_WithItsLoadIds()
        {
            var model = FemexModel.FromJson(LegacyJson);
            Assert.Equal("1.2", model.SchemaVersion);

            string json = model.ToJson();

            // Reading it migrated it, so what is being written is the current
            // format; a "1.2" stamp on a document carrying load ids would be a file
            // that lies about itself.
            Assert.Equal(FemexModel.CurrentSchemaVersion, model.SchemaVersion);
            Assert.StartsWith("{" + Environment.NewLine + "  \"schemaVersion\": \"1.3\",", json);

            var restored = FemexModel.FromJson(json);
            Assert.Equal(new[] { 1, 2, 3 }, restored.Loads.Select(l => l.Id));

            // A 1.3 file is left alone by the migration, so the re-read reports
            // neither the numbering nor the version.
            Assert.Empty(restored.Validate());
        }

        [Fact]
        public void A13File_IsLeftAloneByTheMigration()
        {
            // Gating on the version rather than on "the id is zero" is what keeps a
            // genuinely duplicated load id in a current file an error.
            const string json = """
                {
                  "schemaVersion": "1.3",
                  "loadCases": [
                    { "number": 1, "label": "Dead", "nature": "Dead", "selfWeightFactor": 0 }
                  ],
                  "loads": [
                    { "type": "temperature", "deltaT": 10, "label": "T1", "loadCaseNumber": 1, "id": 4 },
                    { "type": "temperature", "deltaT": 20, "label": "T2", "loadCaseNumber": 1, "id": 4 }
                  ]
                }
                """;

            var model = FemexModel.FromJson(json);

            Assert.Equal(new[] { 4, 4 }, model.Loads.Select(l => l.Id));
            AssertReports(model, "Duplicate load id 4.");
        }

        [Fact]
        public void A11File_IsMigratedTwice()
        {
            // Both migrations run, in version order, from the one deserialize hook.
            const string json = """
                {
                  "schemaVersion": "1.1",
                  "materials": [ { "id": 1, "name": "Concrete", "unitWeight": 25 } ],
                  "loadCases": [
                    { "number": 1, "label": "Dead", "nature": "Dead" }
                  ],
                  "loads": [
                    { "type": "temperature", "deltaT": 10, "label": "T1", "loadCaseNumber": 1 }
                  ]
                }
                """;

            var model = FemexModel.FromJson(json);

            Assert.Equal(25.0 / 9.80665, model.Materials.Single().Density, 12);
            Assert.Equal(1, model.Loads.Single().Id);
            AssertWarns(model, "was written as a unit weight and has been read as a density");
            AssertWarns(model, "its 1 loads have been numbered 1–1 in list order");
        }

        // ----- The reference file -----

        [Fact]
        public void Example1_CarriesLoadIdsAndNoUids()
        {
            string path = Path.Combine(AppContext.BaseDirectory, "Examples", "Example1.femex");
            Assert.True(File.Exists(path), $"Example file not found at {path}.");

            var model = FemexModel.Load(path);

            Assert.Equal(FemexModel.CurrentSchemaVersion, model.SchemaVersion);
            Assert.Empty(model.Validate());

            // Numbered 1..N in list order, which is what the migration produces —
            // so the shipped file is exactly what re-saving it yields, and reading
            // it reports no migration at all.
            Assert.Equal(Enumerable.Range(1, model.Loads.Count), model.Loads.Select(l => l.Id));

            // No uids: the file is hand-authored and has no producing application,
            // so null is the truthful value. Adding them to part of it would trip
            // the coverage warning and adding them to all of it would wreck the
            // hand-editability the interop review counted as a cost of GUIDs.
            Assert.All(model.Loads, l => Assert.Null(l.Uid));
            Assert.DoesNotContain("\"uid\"", File.ReadAllText(path));
        }

        [Fact]
        public void Example1_ReSerializesToItself()
        {
            string path = Path.Combine(AppContext.BaseDirectory, "Examples", "Example1.femex");

            Assert.Equal(File.ReadAllText(path), FemexModel.Load(path).ToJson());
        }

        // ----- Helpers -----

        private static FemexModel WithSection(Action<Section> edit)
        {
            var model = SampleModels.Build();
            edit(model.Sections[0]);
            return model;
        }

        private static int CountDistinctUids(FemexModel model)
        {
            string json = model.ToJson();
            var uids = new HashSet<string>(StringComparer.Ordinal);

            int at = 0;
            while ((at = json.IndexOf("\"uid\": \"", at, StringComparison.Ordinal)) >= 0)
            {
                at += "\"uid\": \"".Length;
                uids.Add(json.Substring(at, 36));
            }

            return uids.Count;
        }

        private static string MeshSectionOf(string json)
        {
            int start = json.IndexOf("\"mesh\":", StringComparison.Ordinal);
            Assert.True(start >= 0, "No mesh block in the document.");

            return json[start..];
        }

        private static void AssertReports(FemexModel model, string fragment)
        {
            AssertReports(model, ValidationSeverity.Error, fragment);
        }

        private static void AssertWarns(FemexModel model, string fragment)
        {
            AssertReports(model, ValidationSeverity.Warning, fragment);
        }

        private static void AssertReports(FemexModel model, ValidationSeverity severity, string fragment)
        {
            var messages = model.Validate().ToList();
            Assert.True(
                messages.Any(m => m.Severity == severity && m.Text.Contains(fragment)),
                $"Expected a {severity} containing \"{fragment}\". Got: {string.Join(" | ", messages)}");
        }
    }
}
