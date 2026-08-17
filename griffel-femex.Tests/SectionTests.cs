using griffel_femex.Geometry;
using griffel_femex.Geometry.Sections;
using Xunit;

namespace griffel_femex.Tests
{
    /// <summary>
    /// Schema 1.5's escape hatch: a section's resolved numbers, stated beside its
    /// shape or instead of one.
    ///
    /// The hole this closes is the largest the interop review found — a shape FEMEX
    /// has no class for was <i>Dropped</i>, not approximated, because there was
    /// nothing to approximate it with. A <see cref="GenericSection"/> carrying a
    /// <see cref="SectionProperties"/> block is that something, and the stated
    /// numbers win over the parametric ones wherever both exist, because the
    /// tabulated area of a rolled profile includes root fillets no idealisation
    /// carries.
    /// </summary>
    public class SectionTests
    {
        // IPE300, Euronorm: the four dimensions, and the tabulated numbers beside
        // the ones those dimensions give. 2(0.150)(0.0107) + 0.0071(0.300 - 2(0.0107))
        // = 5.188e-3 against a tabulated 5.381e-3 — the 3.6% the root fillets are.
        private const double Ipe300TabulatedArea = 5.381e-3;
        private const double Ipe300Iy = 8.356e-5;
        private const double Ipe300Iz = 6.038e-6;
        private const double Ipe300J = 2.012e-7;

        private static void AssertReports(FemexModel model, string fragment)
        {
            var messages = model.Validate().ToList();
            Assert.True(
                messages.Any(m => m.Severity == ValidationSeverity.Error && m.Text.Contains(fragment)),
                $"Expected an error containing \"{fragment}\". Got: {string.Join(" | ", messages)}");
        }

        private static void AssertWarns(FemexModel model, string fragment)
        {
            var messages = model.Validate().ToList();
            Assert.True(
                messages.Any(m => m.Severity == ValidationSeverity.Warning && m.Text.Contains(fragment)),
                $"Expected a warning containing \"{fragment}\". Got: {string.Join(" | ", messages)}");
        }

        // ----- The properties block -----

        [Fact]
        public void Properties_RoundTripOnEveryExistingSubtype()
        {
            var model = SampleModels.Build();

            model.Sections.Single(s => s.Id == 1).Properties =
                new SectionProperties(0.15, iy: 3.125e-3, iz: 1.125e-3, j: 2.9e-3);
            model.Sections.Single(s => s.Id == 2).Properties =
                new SectionProperties(0.1257) { ShearAreaY = 0.113, ShearAreaZ = 0.113 };
            model.Sections.Single(s => s.Id == 3).Properties =
                new SectionProperties(0.22) { Iw = 1.1e-6, Wely = 1.2e-3, Welz = 1.3e-3, Wply = 1.4e-3, Wplz = 1.5e-3 };

            var restored = FemexModel.FromJson(model.ToJson());

            var rectangle = restored.Sections.Single(s => s.Id == 1);
            Assert.IsType<Rectangle>(rectangle);
            Assert.Equal(0.15, rectangle.Properties!.Area);
            Assert.Equal(3.125e-3, rectangle.Properties.Iy);
            Assert.Equal(1.125e-3, rectangle.Properties.Iz);
            Assert.Equal(2.9e-3, rectangle.Properties.J);

            var circle = restored.Sections.Single(s => s.Id == 2);
            Assert.IsType<Circle>(circle);
            Assert.Equal(0.113, circle.Properties!.ShearAreaY);
            Assert.Equal(0.113, circle.Properties.ShearAreaZ);

            var tee = restored.Sections.Single(s => s.Id == 3);
            Assert.IsType<TSection>(tee);
            Assert.Equal(1.1e-6, tee.Properties!.Iw);
            Assert.Equal(1.2e-3, tee.Properties.Wely);
            Assert.Equal(1.3e-3, tee.Properties.Welz);
            Assert.Equal(1.4e-3, tee.Properties.Wply);
            Assert.Equal(1.5e-3, tee.Properties.Wplz);
        }

        [Fact]
        public void ASectionWithNoProperties_OmitsTheKeyEntirely()
        {
            // WhenWritingNull is already set, so no existing file gains a byte.
            string json = SampleModels.Build().ToJson();

            Assert.DoesNotContain("\"properties\"", json);
            Assert.All(FemexModel.FromJson(json).Sections, s => Assert.Null(s.Properties));
        }

        [Fact]
        public void AnUnstatedProperty_IsToldApartFromZero()
        {
            // Every field is double?, the same distinction Restraint.Stiffness draws.
            var properties = new SectionProperties(Ipe300TabulatedArea, iy: Ipe300Iy, iz: Ipe300Iz);

            Assert.Null(properties.J);
            Assert.Null(properties.Wply);
        }

        // ----- GetArea() -----

        [Fact]
        public void GetArea_ReturnsTheStatedArea_WhereThereIsOne()
        {
            var section = new Rectangle(1, "R300x500", 0.3, 0.5)
            {
                Properties = new SectionProperties(0.16),
            };

            Assert.Equal(0.15, section.CalculateArea());
            Assert.Equal(0.16, section.GetArea());
        }

        [Fact]
        public void GetArea_FallsBackToCalculateArea_WhereThereIsNone()
        {
            var section = new Rectangle(1, "R300x500", 0.3, 0.5);

            Assert.Null(section.Properties);
            Assert.Equal(section.CalculateArea(), section.GetArea());
        }

        [Fact]
        public void SelfWeight_UsesTheStatedArea_AndNotTheParametricOne()
        {
            // The one behavioural change to an existing model, and a correctness
            // improvement in the direction decision 2 argues.
            var model = SampleModels.Build();
            var column = model.Sections.Single(s => s.Id == 1);

            Assert.True(model.TryGetBarSelfWeightPerLength(SampleModels.BarId, out Vector3d before));

            column.Properties = new SectionProperties(column.CalculateArea() * 1.05);

            Assert.True(model.TryGetBarSelfWeightPerLength(SampleModels.BarId, out Vector3d after));
            Assert.Equal(before.Length * 1.05, after.Length, 8);
        }

        // ----- The generic section -----

        [Fact]
        public void AGenericSection_RoundTripsWithItsDiscriminator()
        {
            var model = SampleModels.Build();
            model.Sections.Add(new GenericSection(4, "IPE300-as-numbers",
                new SectionProperties(Ipe300TabulatedArea, iy: Ipe300Iy, iz: Ipe300Iz, j: Ipe300J)));

            string json = model.ToJson();
            Assert.Contains("\"type\": \"generic\"", json);

            var restored = FemexModel.FromJson(json);
            var section = restored.Sections.Single(s => s.Id == 4);

            Assert.IsType<GenericSection>(section);
            Assert.Equal(Ipe300TabulatedArea, section.Properties!.Area);
            Assert.Equal(Ipe300J, section.Properties.J);
        }

        [Fact]
        public void AGenericSection_HasNoGeometry_ButHasAnArea()
        {
            var section = new GenericSection(4, "General",
                new SectionProperties(Ipe300TabulatedArea, iy: Ipe300Iy, iz: Ipe300Iz));

            Assert.Equal(0.0, section.CalculateArea());
            Assert.Equal(Ipe300TabulatedArea, section.GetArea());
        }

        // ----- Validation -----

        [Fact]
        public void Reports_AGenericSectionWithNoArea()
        {
            var model = SampleModels.Build();
            model.Sections.Add(new GenericSection(4, "Nothing at all"));

            AssertReports(model, "Section 4 is generic and states no area, so it has no geometry and " +
                                 "no stiffness; nothing can be built from it.");
        }

        [Fact]
        public void Reports_AStatedPropertyThatIsNotPositive()
        {
            var model = SampleModels.Build();
            model.Sections.Single(s => s.Id == 1).Properties = new SectionProperties(-0.01) { Iz = 0.0 };

            AssertReports(model, "Section 1 states an area of -0.01, which is not a positive quantity.");
            AssertReports(model, "Section 1 states an iz of 0, which is not a positive quantity.");
        }

        [Fact]
        public void Warns_WhenTheStatedAreaAndTheDimensionsDisagreePastTenPercent()
        {
            // A unit error is what it usually is: mm² written where m² was meant.
            var model = SampleModels.Build();
            model.Sections.Single(s => s.Id == 1).Properties = new SectionProperties(5.38);

            AssertWarns(model, "Section 1 states an area of 5.38 and its dimensions give 0.15; " +
                               "one of the two is wrong.");
        }

        [Fact]
        public void Accepts_AFilletSizedDisagreement()
        {
            // 3.6% is what an IPE300's root radii account for, and no idealisation
            // carries them.
            var model = SampleModels.Build();
            var section = model.Sections.Single(s => s.Id == 1);
            section.Properties = new SectionProperties(section.CalculateArea() * 1.036);

            Assert.DoesNotContain(model.Validate(), m => m.Text.Contains("one of the two is wrong"));
        }

        [Fact]
        public void Warns_AGenericSectionWithAnAreaButNoIz()
        {
            var model = SampleModels.Build();
            model.Sections.Add(new GenericSection(4, "Half stated",
                new SectionProperties(Ipe300TabulatedArea, iy: Ipe300Iy)));

            AssertWarns(model, "Section 4 is generic and states an area but no iz; every bar using it " +
                               "will weigh correctly and bend wrongly.");

            // And exactly one warning: iy is stated, so only its mirror fires.
            Assert.Single(model.Validate().Where(m => m.Text.Contains("bend wrongly")));
        }

        [Fact]
        public void Accepts_AGenericSectionStatingAreaIyAndIz()
        {
            // The regression that catches an unscoped W1. GenericSection.CalculateArea()
            // returns zero, so a disagreement check that did not exclude generic
            // sections would read a stated 5.381e-3 against a computed 0.0 as a 100%
            // disagreement and fire on the exact case 1.5 exists to make legal.
            var model = SampleModels.Build();
            model.Sections.Add(new GenericSection(4, "GEN-IPE300",
                new SectionProperties(Ipe300TabulatedArea, iy: Ipe300Iy, iz: Ipe300Iz, j: Ipe300J)));

            Assert.Empty(model.Validate());
        }

        // ----- Extensibility -----

        [Fact]
        public void AnUnknownMemberInsideProperties_RoundTripsAndIsReported()
        {
            const string json = """
                {
                  "schemaVersion": "1.5",
                  "sections": [
                    { "type": "rectangle", "id": 1, "name": "R1", "width": 0.3, "depth": 0.5,
                      "properties": { "area": 0.15, "torsionalWarpingFactor": 0.42 } }
                  ]
                }
                """;

            var model = FemexModel.FromJson(json);

            AssertWarns(model, "\"torsionalWarpingFactor\", on Section 1 properties");

            var restored = FemexModel.FromJson(model.ToJson());
            SectionProperties properties = restored.Sections.Single(s => s.Id == 1).Properties!;

            Assert.Equal(0.42, properties.UnknownMembers!["torsionalWarpingFactor"].GetDouble());
        }

        // ----- The reference file -----

        [Fact]
        public void Example1_LoadsAndValidates_AfterTheBump()
        {
            // Its sections carry no properties, so nothing here reaches them and the
            // file is still silent — the check that the change really is additive.
            string path = Path.Combine(AppContext.BaseDirectory, "Examples", "Example1.femex");
            var model = FemexModel.Load(path);

            Assert.Equal(FemexModel.CurrentSchemaVersion, model.SchemaVersion);
            Assert.All(model.Sections, s => Assert.Null(s.Properties));
            Assert.Empty(model.Validate());
        }
    }
}
