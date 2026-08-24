using Xunit;

namespace griffel_femex.Tests
{
    /// <summary>
    /// Schema 1.8, first half: the unit convention is typed.
    ///
    /// 1.7's <c>Units</c> was two free-text strings with comment-level guidance and
    /// no validation, so <c>"length": "banana"</c> round-tripped clean — an
    /// annotation nothing could rely on, which is the only defect an annotation can
    /// have, and the one <c>FEMEX_SAF_Fit.md</c> §3 cites by name.
    ///
    /// This is also the <b>first bump in FEMEX to rename a JSON key</b>.
    /// <c>"length": "m"</c> and <c>"length": "Metre"</c> cannot share a key without a
    /// custom converter and there is not one in the repository, so the typed
    /// spellings take <c>lengthUnit</c> and <c>forceUnit</c> and the old keys are
    /// bound read-only. Everything below about migration follows from that choice;
    /// so does the pair of hand-migrated example files.
    /// </summary>
    public class UnitsTests
    {
        private static void AssertWarns(FemexModel model, string fragment)
        {
            var messages = model.Validate().ToList();
            Assert.True(
                messages.Any(m => m.Severity == ValidationSeverity.Warning && m.Text.Contains(fragment)),
                $"Expected a warning containing \"{fragment}\". Got: {string.Join(" | ", messages)}");
        }

        // ----- The typed block -----

        [Fact]
        public void AllFiveUnits_RoundTrip()
        {
            var model = SampleModels.Build();

            model.Units!.Temperature = TemperatureUnit.Kelvin;
            model.Units.Angle = AngleUnit.Degree;
            model.Units.Mass = MassUnit.Tonne;

            var units = FemexModel.FromJson(model.ToJson()).Units!;

            Assert.Equal(LengthUnit.Metre, units.Length);
            Assert.Equal(ForceUnit.Kilonewton, units.Force);
            Assert.Equal(TemperatureUnit.Kelvin, units.Temperature);
            Assert.Equal(AngleUnit.Degree, units.Angle);
            Assert.Equal(MassUnit.Tonne, units.Mass);
        }

        [Fact]
        public void TheTypedSpellings_TakeTheirOwnKeys()
        {
            // The rename, asserted in both directions: the new keys are written and
            // the old ones cannot be, because their properties have no getter.
            string json = new FemexModel { Units = new Units(LengthUnit.Millimetre, ForceUnit.Newton) }.ToJson();

            Assert.Contains("\"lengthUnit\": \"Millimetre\"", json);
            Assert.Contains("\"forceUnit\": \"Newton\"", json);
            Assert.DoesNotContain("\"length\":", json);
            Assert.DoesNotContain("\"force\":", json);
        }

        [Fact]
        public void EnumsCrossAsTheirNames_ThroughTheGlobalConverter()
        {
            // No [JsonConverter] anywhere on this block: the model's own
            // JsonStringEnumConverter and camelCase policy carry all five, which is
            // what makes the [JsonPropertyName] pair the only attribute the rename
            // needed.
            string json = new FemexModel
            {
                Units = new Units { Length = LengthUnit.Foot, Force = ForceUnit.Kip, Mass = MassUnit.Slug },
            }.ToJson();

            Assert.Contains("\"lengthUnit\": \"Foot\"", json);
            Assert.Contains("\"forceUnit\": \"Kip\"", json);
            Assert.Contains("\"mass\": \"Slug\"", json);
        }

        // ----- Additivity -----

        [Fact]
        public void AModelStatingNoUnits_OmitsTheKeyEntirely()
        {
            Assert.DoesNotContain("\"units\"", new FemexModel().ToJson());
        }

        [Fact]
        public void ABlockStatingTwoUnits_OmitsTheOtherThree()
        {
            // The three 1.8 additions with no free-text predecessor. A model that
            // states length and force — which is every model written so far — gains
            // not one byte from them.
            string json = new FemexModel { Units = new Units(LengthUnit.Metre, ForceUnit.Kilonewton) }.ToJson();

            Assert.DoesNotContain("\"temperature\"", json);
            Assert.DoesNotContain("\"angle\"", json);
            Assert.DoesNotContain("\"mass\"", json);
        }

        // ----- Reading a 1.7 file -----

        [Fact]
        public void FreeTextUnits_AreMigratedAndReported()
        {
            string json = """
                {
                  "schemaVersion": "1.7",
                  "units": {
                    "length": "m",
                    "force": "kN"
                  }
                }
                """;

            var model = FemexModel.FromJson(json);

            Assert.Equal(LengthUnit.Metre, model.Units!.Length);
            Assert.Equal(ForceUnit.Kilonewton, model.Units.Force);

            AssertWarns(model, "a length of \"m\" as free text, which has been read as Metre");
            AssertWarns(model, "a force of \"kN\" as free text, which has been read as Kilonewton");

            // The migration is a property of the read, so the file it produces
            // carries the typed spelling and reports nothing.
            string resaved = model.ToJson();
            Assert.Contains("\"lengthUnit\": \"Metre\"", resaved);
            Assert.DoesNotContain("\"length\":", resaved);
            Assert.Empty(FemexModel.FromJson(resaved).Validate()
                .Where(m => m.Text.Contains("units block")));
        }

        [Theory]
        [InlineData("mm", LengthUnit.Millimetre)]
        [InlineData("MM", LengthUnit.Millimetre)]
        [InlineData("cm", LengthUnit.Centimetre)]
        [InlineData("m", LengthUnit.Metre)]
        [InlineData("  m  ", LengthUnit.Metre)]
        [InlineData("Metre", LengthUnit.Metre)]
        [InlineData("meter", LengthUnit.Metre)]
        [InlineData("in", LengthUnit.Inch)]
        [InlineData("ft", LengthUnit.Foot)]
        [InlineData("FEET", LengthUnit.Foot)]
        public void TheLengthSymbols_ParseCaseInsensitively(string text, LengthUnit expected)
        {
            var model = FemexModel.FromJson($$"""{ "units": { "length": "{{text}}" } }""");

            Assert.Equal(expected, model.Units!.Length);
        }

        [Theory]
        [InlineData("N", ForceUnit.Newton)]
        [InlineData("kN", ForceUnit.Kilonewton)]
        [InlineData("KN", ForceUnit.Kilonewton)]
        [InlineData("MN", ForceUnit.Meganewton)]
        [InlineData("lbf", ForceUnit.PoundForce)]
        [InlineData("kip", ForceUnit.Kip)]
        [InlineData("Kips", ForceUnit.Kip)]
        public void TheForceSymbols_ParseCaseInsensitively(string text, ForceUnit expected)
        {
            var model = FemexModel.FromJson($$"""{ "units": { "force": "{{text}}" } }""");

            Assert.Equal(expected, model.Units!.Force);
        }

        // ----- What does not parse -----

        [Fact]
        public void TextThatNamesNoUnit_IsDroppedAndNamed()
        {
            // The one migration in FEMEX that loses something. It is deliberate:
            // free text round-tripping clean is the defect §3 row 4 cites, so losing
            // it loudly is the change rather than a regression in it.
            var model = FemexModel.FromJson("""{ "units": { "length": "banana" } }""");

            Assert.Null(model.Units!.Length);
            AssertWarns(model, "a length of \"banana\", which names no unit this build knows");
            AssertWarns(model, "It has been dropped, and the model now states no length unit at all.");

            Assert.DoesNotContain("banana", model.ToJson());
        }

        [Fact]
        public void TheDroppedTextDoesNotSurviveAsAnUnknownMember()
        {
            // "length" is a declared property, so it never reaches extension data —
            // the same guarantee Material.unitWeight has held since 1.2, and what
            // makes "dropped" mean dropped rather than moved.
            var model = FemexModel.FromJson("""{ "units": { "length": "banana" } }""");

            Assert.Null(model.Units!.UnknownMembers);
        }

        [Fact]
        public void AnEmptyUnitString_IsNotAMigrationAndIsNotReported()
        {
            // A key written and left blank says no more than one never written, and
            // reporting it would nag about a file that lost nothing.
            var model = FemexModel.FromJson("""{ "units": { "length": "", "force": "   " } }""");

            Assert.Null(model.Units!.Length);
            Assert.Null(model.Units.Force);
            Assert.DoesNotContain(model.Validate(), m => m.Text.Contains("units block"));
        }

        [Fact]
        public void BothSpellings_KeepTheTypedOneAndSaySo()
        {
            // They cannot both be right and the newer one wins — the rule the 1.1
            // unit-weight migration already applies to a material carrying both
            // spellings of its density. Reported, because silently preferring one of
            // two contradictory statements is what this repository never does.
            var model = FemexModel.FromJson("""
                { "units": { "length": "ft", "lengthUnit": "Metre" } }
                """);

            Assert.Equal(LengthUnit.Metre, model.Units!.Length);
            AssertWarns(model, "states the length unit both as free text and as a typed lengthUnit");
        }

        [Fact]
        public void ASevenFile_IsToldWhatItLacks()
        {
            var model = FemexModel.FromJson("""{ "schemaVersion": "1.7" }""");

            AssertWarns(model, "schemaVersion \"1.7\", written before units were typed and before a " +
                               "restraint had a direction");
        }

        // ----- Reading a file from further ahead -----

        [Fact]
        public void AnUnknownMemberOnTheUnitsBlock_SurvivesAndIsNamed()
        {
            // Where a unit this enum set deliberately excludes goes. The block has
            // been IExtensible since 1.4 and is already registered in
            // EnumerateExtensible, so 1.8 adds no registration.
            var model = FemexModel.FromJson("""
                {
                  "schemaVersion": "1.8",
                  "units": { "lengthUnit": "Metre", "energyUnit": "Joule" }
                }
                """);

            AssertWarns(model, "\"energyUnit\", on the units block");

            var restored = FemexModel.FromJson(model.ToJson());
            Assert.Equal("Joule", restored.Units!.UnknownMembers!["energyUnit"].GetString());
        }

        // ----- The reference files -----

        [Fact]
        public void BothExamples_CarryTypedUnits()
        {
            // Hand-migrated at this bump rather than merely version-bumped: under the
            // rename, re-serialising cannot emit the old keys, so a file left
            // carrying them would fail byte identity and would warn on every load.
            foreach (string name in new[] { "Example1.femex", "Example2.femex" })
            {
                var model = FemexModel.Load(Path.Combine(AppContext.BaseDirectory, "Examples", name));

                Assert.Equal(LengthUnit.Metre, model.Units!.Length);
                Assert.Equal(ForceUnit.Kilonewton, model.Units.Force);

                // The three additions stay unstated, which is what proves that half
                // of the bump additive.
                Assert.Null(model.Units.Temperature);
                Assert.Null(model.Units.Angle);
                Assert.Null(model.Units.Mass);

                Assert.Empty(model.Validate());
            }
        }
    }
}
