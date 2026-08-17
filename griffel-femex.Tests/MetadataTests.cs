using System.Text.Json;
using griffel_femex.Geometry.Sections;
using griffel_femex.Loads;
using Xunit;

namespace griffel_femex.Tests
{
    /// <summary>
    /// The two halves of schema 1.4: what the file says about itself — who wrote it,
    /// with what, when — and what it says that this build cannot read.
    ///
    /// The failure the second half removes is the one the interop review calls
    /// disqualifying, and the one <c>FEMEX_Identity_Summary.md</c> flags against the
    /// format itself: a build reading a file from a schema it has never heard of
    /// drops every member that schema added and reports nothing. From 1.4 on the
    /// payload survives the read, comes back on save, and <c>Validate()</c> names it.
    ///
    /// What that does <b>not</b> fix is its own illustration — a 1.3 file read by a
    /// <i>1.2</i> build. A 1.2 build is already written and nothing added here
    /// reaches it. The claim is about the loss class going forwards: 1.4 is the last
    /// build that can lose a future field in silence.
    /// </summary>
    public class MetadataTests
    {
        // ----- The metadata block -----

        [Fact]
        public void AllFourFields_RoundTrip()
        {
            var model = SampleModels.Build();
            model.Metadata = new FileMetadata("griffel-etabs", "0.1.0", "Tower A", "2026-08-17T09:30:00Z");

            var restored = FemexModel.FromJson(model.ToJson());

            Assert.NotNull(restored.Metadata);
            Assert.Equal("griffel-etabs", restored.Metadata!.Producer);
            Assert.Equal("0.1.0", restored.Metadata.ProducerVersion);
            Assert.Equal("Tower A", restored.Metadata.ProjectName);
            Assert.Equal("2026-08-17T09:30:00Z", restored.Metadata.CreatedAt);
        }

        [Fact]
        public void Metadata_IsTheSecondKey()
        {
            // Immediately after schemaVersion and ahead of units and gravity: a
            // reader branching on the version wants what produced the file next,
            // before it parses anything that costs.
            string json = SampleModels.Build().ToJson();

            Assert.StartsWith(
                "{" + Environment.NewLine +
                "  \"schemaVersion\": \"" + FemexModel.CurrentSchemaVersion + "\"," + Environment.NewLine +
                "  \"metadata\": {",
                json);
        }

        [Fact]
        public void AModelWithNoMetadata_OmitsTheKeyEntirely()
        {
            var model = SampleModels.Build();
            model.Metadata = null;

            string json = model.ToJson();

            // Nullable with no initializer, like Units: a model that says nothing
            // about its provenance writes nothing rather than an empty block.
            Assert.DoesNotContain("\"metadata\"", json);
            Assert.Null(FemexModel.FromJson(json).Metadata);
        }

        [Fact]
        public void ToJson_InventsNoProvenance()
        {
            // The argument AssignMissingUids makes: auto-stamping a producer or a
            // timestamp would mean the same model built twice from the same source
            // produced different files, and Example1_ReSerializesToItself could
            // never hold. schemaVersion is stamped because it is a statement about
            // the format, which the library knows; this is a statement about the
            // caller, which it does not.
            var bare = new FemexModel();
            bare.ToJson();
            Assert.Null(bare.Metadata);

            Assert.Equal(SampleModels.Build().ToJson(), SampleModels.Build().ToJson());
        }

        [Fact]
        public void A13File_LoadsClean_AndDrawsTheVersionWarning()
        {
            const string json = """
                {
                  "schemaVersion": "1.3",
                  "loadCases": [
                    { "number": 1, "label": "Dead", "nature": "Dead", "selfWeightFactor": 0 }
                  ]
                }
                """;

            var model = FemexModel.FromJson(json);

            Assert.Null(model.Metadata);
            AssertWarns(model, "The model declares schemaVersion \"1.3\", written before file metadata " +
                               "existed, so it does not say what produced it or when.");
        }

        // ----- Unknown members -----

        [Fact]
        public void TheTypeDiscriminator_DoesNotLeakIntoExtensionData()
        {
            // The one thing that could have sunk the whole approach. "type" is on
            // every section, surface property, load and gridline in the example and
            // in every fixture, so had System.Text.Json 7 routed it into extension
            // data, ReportUnknownMembers would fire model-wide and about
            // twenty-eight tests would go red rather than one.
            string path = Path.Combine(AppContext.BaseDirectory, "Examples", "Example1.femex");
            var model = FemexModel.Load(path);

            Assert.All(model.Sections, s => Assert.Null(s.UnknownMembers));
            Assert.All(model.SurfaceProperties, s => Assert.Null(s.UnknownMembers));
            Assert.All(model.Loads, l => Assert.Null(l.UnknownMembers));
            Assert.Empty(model.Validate());
        }

        [Fact]
        public void AnUnknownMemberAtTheRoot_SurvivesARoundTrip()
        {
            var model = FemexModel.FromJson(WithExtras);

            Assert.NotNull(model.UnknownMembers);
            Assert.Equal(3, model.UnknownMembers!["diaphragms"][0].GetProperty("id").GetInt32());

            var restored = FemexModel.FromJson(model.ToJson());

            Assert.Equal(3, restored.UnknownMembers!["diaphragms"][0].GetProperty("id").GetInt32());
        }

        [Fact]
        public void AnUnknownMemberOnAPolymorphicEntity_SurvivesARoundTrip()
        {
            // The JsonPolymorphic x JsonExtensionData interaction, on the two lists
            // FEMEX really does serialize through a base type.
            var model = FemexModel.FromJson(WithExtras);

            var restored = FemexModel.FromJson(model.ToJson());

            Section section = restored.Sections.Single(s => s.Id == 1);
            Assert.Equal(0.85, section.UnknownMembers!["shearAreaFactor"].GetDouble());

            Load load = restored.Loads.Single(l => l.Id == 1);
            Assert.Equal("ASCE7", load.UnknownMembers!["derivedFrom"].GetString());

            // And the discriminator still round-trips as the discriminator.
            Assert.IsType<Rectangle>(section);
            Assert.IsType<TemperatureLoad>(load);
        }

        [Fact]
        public void Validate_ReportsOncePerDistinctMemberName_WithTheCountAndTheKind()
        {
            // One message per (name, kind) and not one per object, for the reason
            // ValidateUidCoverage gives: the fact is about the file, and a file from
            // a schema two versions ahead would otherwise bury every other message.
            var model = FemexModel.FromJson(ThreeBarsWithAnExtra);

            var reported = model.Validate()
                                .Where(m => m.Text.Contains("a member this build does not know"))
                                .ToList();

            Assert.Single(reported);
            Assert.Equal(ValidationSeverity.Warning, reported[0].Severity);
            Assert.Contains("\"endOffset\", on 3 bars", reported[0].Text);
            Assert.Contains("preserved when the model is re-saved", reported[0].Text);
        }

        [Fact]
        public void Validate_NamesTheObject_WhenThereIsOnlyOne()
        {
            var model = FemexModel.FromJson(WithExtras);

            AssertWarns(model, "\"shearAreaFactor\", on Section 1");
            AssertWarns(model, "\"diaphragms\", on the model root");
        }

        [Fact]
        public void TheIdentitySummarysFailure_Inverted()
        {
            // A file carrying a member this build does not know, on a bar: preserved
            // and re-emitted rather than dropped. Not the summary's own scenario —
            // that one is a 1.3 file read by a 1.2 build, which is already written
            // and which nothing added here can reach.
            var model = FemexModel.FromJson(ThreeBarsWithAnExtra);

            string json = model.ToJson();

            Assert.Contains("\"endOffset\"", json);
            Assert.All(FemexModel.FromJson(json).Bars,
                       b => Assert.Equal(0.15, b.UnknownMembers!["endOffset"].GetDouble()));
        }

        [Fact]
        public void AnUnrecognisedVersion_AndItsUnknownMembers_BothSurvive()
        {
            // The two halves composing. ToJson() already leaves an unrecognised
            // version alone — "it was not migrated, so it is not ours to restate" —
            // so an older build round-tripping a newer file now preserves the
            // version *and* the payload it does not understand.
            var model = FemexModel.FromJson(WithExtras);
            Assert.Equal("2.0", model.SchemaVersion);

            var restored = FemexModel.FromJson(model.ToJson());

            Assert.Equal("2.0", restored.SchemaVersion);
            Assert.NotNull(restored.UnknownMembers);
            AssertWarns(restored, "The model declares schemaVersion \"2.0\", which this build does not " +
                                  "recognise");
            AssertWarns(restored, "a member this build does not know");
        }

        // ----- Regression -----

        [Fact]
        public void A13FileWithSomethingToWeigh_StillDrawsTheSelfWeightWarning()
        {
            // The bump's one regression. The gate read "1.2 or CurrentSchemaVersion";
            // once the current version moved past 1.3, a 1.3 file would have stopped
            // satisfying it and the warning would have silently stopped firing. Every
            // bump since has had to add its own line to SelfWeightVersions for the
            // same reason.
            var model = SampleModels.Build();
            model.SchemaVersion = "1.3";
            foreach (var loadCase in model.LoadCases)
                loadCase.SelfWeightFactor = 0.0;

            AssertWarns(model, "No load case carries self-weight");
        }

        // ----- Fixtures and helpers -----

        /// <summary>
        /// A file from a schema this build has never heard of: an unknown array at
        /// the root, and an unknown member on each of the two lists FEMEX serializes
        /// through a polymorphic base.
        /// </summary>
        private const string WithExtras = """
            {
              "schemaVersion": "2.0",
              "diaphragms": [ { "id": 3, "plateId": 10 } ],
              "sections": [
                { "type": "rectangle", "id": 1, "name": "R1", "width": 0.3, "depth": 0.5,
                  "shearAreaFactor": 0.85 }
              ],
              "loadCases": [
                { "number": 1, "label": "Thermal", "nature": "Temperature", "selfWeightFactor": 0 }
              ],
              "loads": [
                { "type": "temperature", "id": 1, "label": "T1", "loadCaseNumber": 1, "deltaT": 20,
                  "derivedFrom": "ASCE7" }
              ]
            }
            """;

        /// <summary>Three bars carrying one member this build has no property for.</summary>
        private const string ThreeBarsWithAnExtra = """
            {
              "schemaVersion": "2.0",
              "levels": [ { "levelNumber": 0, "absoluteElevation": 0, "relativeElevation": 0 } ],
              "nodes": [
                { "nodeNumber": 1, "x": 0, "y": 0, "levelNumber": 0 },
                { "nodeNumber": 2, "x": 3, "y": 0, "levelNumber": 0 },
                { "nodeNumber": 3, "x": 6, "y": 0, "levelNumber": 0 },
                { "nodeNumber": 4, "x": 9, "y": 0, "levelNumber": 0 }
              ],
              "sections": [ { "type": "circle", "id": 1, "name": "C300", "diameter": 0.3 } ],
              "materials": [
                { "id": 1, "name": "Steel", "modulusOfElasticity": 210e6, "poissonsRatio": 0.3,
                  "density": 7.85, "strength": 355e3 }
              ],
              "loadCases": [
                { "number": 1, "label": "Dead", "nature": "Dead", "selfWeightFactor": 1 }
              ],
              "bars": [
                { "id": 1, "startNodeId": 1, "endNodeId": 2, "sectionId": 1, "materialId": 1,
                  "endOffset": 0.15 },
                { "id": 2, "startNodeId": 2, "endNodeId": 3, "sectionId": 1, "materialId": 1,
                  "endOffset": 0.15 },
                { "id": 3, "startNodeId": 3, "endNodeId": 4, "sectionId": 1, "materialId": 1,
                  "endOffset": 0.15 }
              ]
            }
            """;

        private static void AssertWarns(FemexModel model, string fragment)
        {
            var messages = model.Validate().ToList();
            Assert.True(
                messages.Any(m => m.Severity == ValidationSeverity.Warning && m.Text.Contains(fragment)),
                $"Expected a warning containing \"{fragment}\". Got: {string.Join(" | ", messages)}");
        }
    }
}
