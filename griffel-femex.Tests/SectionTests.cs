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

        private static string Example2Path =>
            Path.Combine(AppContext.BaseDirectory, "Examples", "Example2.femex");

        /// <summary>One section through the model's own options, so it carries its discriminator.</summary>
        private static string Serialize(Section section)
        {
            var model = new FemexModel { Sections = { section } };
            return model.ToJson();
        }

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

            AssertReports(model, "Section 4 states neither dimensions nor stiffness, so nothing here " +
                                 "can build it. If it came from a program that holds this profile in " +
                                 "its own library, the properties exist there and did not cross.");
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

        // ----- The five parametric shapes -----

        [Fact]
        public void AnISection_HasItsDiscriminatorAndItsArea()
        {
            // 2(0.150)(0.0107) + 0.0071(0.300 - 2(0.0107)) = 5.18806e-3, the IPE300
            // parametric area, 3.6% under the tabulated one.
            var section = new ISection(1, "IPE300", 0.150, 0.0107, 0.0071, 0.300);

            Assert.Equal(5.18806e-3, section.CalculateArea(), 9);
            Assert.Contains("\"type\": \"ishape\"", Serialize(section));
        }

        [Fact]
        public void AChannel_HasItsDiscriminatorAndTheSameAreaAsAnI()
        {
            // Deliberately the same formula: a channel and an I differ only in where
            // the web sits, which moves the centroid and not the area.
            var channel = new Channel(1, "UPE300", 0.100, 0.0155, 0.0095, 0.300);
            var equivalentI = new ISection(2, "As an I", 0.100, 0.0155, 0.0095, 0.300);

            // 2(0.100)(0.0155) + 0.0095(0.300 - 0.031) = 5.6555e-3.
            Assert.Equal(5.6555e-3, channel.CalculateArea(), 9);
            Assert.Equal(equivalentI.CalculateArea(), channel.CalculateArea());
            Assert.Contains("\"type\": \"channel\"", Serialize(channel));
        }

        [Fact]
        public void AnAngle_HasItsDiscriminatorAndItsArea()
        {
            // (0.100 + 0.075 - 0.010) x 0.010 = 1.65e-3: the two legs less the corner
            // they share, which would otherwise be counted twice.
            var section = new Angle(1, "L100x75x10", 0.100, 0.075, 0.010);

            Assert.Equal(1.65e-3, section.CalculateArea(), 9);
            Assert.Contains("\"type\": \"angle\"", Serialize(section));
        }

        [Fact]
        public void ABox_HasItsDiscriminatorAndItsArea()
        {
            // 0.200 x 0.100 - 0.190 x 0.090 = 2.90e-3.
            var section = new Box(1, "RHS200x100x5", 0.200, 0.100, 0.005);

            Assert.Equal(2.90e-3, section.CalculateArea(), 9);
            Assert.Contains("\"type\": \"box\"", Serialize(section));
        }

        [Fact]
        public void APipe_HasItsDiscriminatorAndItsArea()
        {
            // pi/4 (0.1397^2 - 0.1297^2) = 2.11586e-3.
            var section = new Pipe(1, "CHS 139.7x5", 0.1397, 0.005);

            Assert.Equal(Math.PI / 4.0 * (0.1397 * 0.1397 - 0.1297 * 0.1297), section.CalculateArea(), 12);
            Assert.Equal(2.11586e-3, section.CalculateArea(), 8);
            Assert.Contains("\"type\": \"pipe\"", Serialize(section));
        }

        [Fact]
        public void AllNineDiscriminators_SurviveOneRoundTrip()
        {
            var model = SampleModels.Build();

            model.Sections.Add(new ISection(4, "IPE300", 0.150, 0.0107, 0.0071, 0.300));
            model.Sections.Add(new Channel(5, "UPE300", 0.100, 0.0155, 0.0095, 0.300));
            model.Sections.Add(new Angle(6, "L100x75x10", 0.100, 0.075, 0.010));
            model.Sections.Add(new Box(7, "RHS200x100x5", 0.200, 0.100, 0.005));
            model.Sections.Add(new Pipe(8, "CHS 139.7x5", 0.1397, 0.005));
            model.Sections.Add(new GenericSection(9, "GEN",
                new SectionProperties(Ipe300TabulatedArea, iy: Ipe300Iy, iz: Ipe300Iz)));

            var restored = FemexModel.FromJson(model.ToJson());

            Assert.IsType<Rectangle>(restored.Sections.Single(s => s.Id == 1));
            Assert.IsType<Circle>(restored.Sections.Single(s => s.Id == 2));
            Assert.IsType<TSection>(restored.Sections.Single(s => s.Id == 3));
            Assert.IsType<ISection>(restored.Sections.Single(s => s.Id == 4));
            Assert.IsType<Channel>(restored.Sections.Single(s => s.Id == 5));
            Assert.IsType<Angle>(restored.Sections.Single(s => s.Id == 6));
            Assert.IsType<Box>(restored.Sections.Single(s => s.Id == 7));
            Assert.IsType<Pipe>(restored.Sections.Single(s => s.Id == 8));
            Assert.IsType<GenericSection>(restored.Sections.Single(s => s.Id == 9));

            // Every one of them keeps its dimensions, not just its type.
            var pipe = (Pipe)restored.Sections.Single(s => s.Id == 8);
            Assert.Equal(0.1397, pipe.Diameter);
            Assert.Equal(0.005, pipe.WallThickness);
        }

        // ----- The catalogue block -----

        [Fact]
        public void ACatalogue_RoundTrips()
        {
            var model = SampleModels.Build();
            model.Sections.Single(s => s.Id == 1).Catalogue =
                new SectionCatalogue("Euronorm", "IPE300", SectionManufacture.HotRolled);

            var restored = FemexModel.FromJson(model.ToJson());
            SectionCatalogue catalogue = restored.Sections.Single(s => s.Id == 1).Catalogue!;

            Assert.Equal("Euronorm", catalogue.Source);
            Assert.Equal("IPE300", catalogue.Profile);
            Assert.Equal(SectionManufacture.HotRolled, catalogue.Manufacture);
        }

        [Fact]
        public void ASectionWithNoCatalogue_OmitsTheKeyEntirely()
        {
            string json = SampleModels.Build().ToJson();

            Assert.DoesNotContain("\"catalogue\"", json);
            Assert.All(FemexModel.FromJson(json).Sections, s => Assert.Null(s.Catalogue));
        }

        [Fact]
        public void Manufacture_SerializesAsAString()
        {
            // Through the JsonStringEnumConverter the options already register, so
            // the field needs no converter of its own.
            var model = SampleModels.Build();
            model.Sections.Single(s => s.Id == 1).Catalogue =
                new SectionCatalogue("BS 5950", "SHS 100x100x5", SectionManufacture.ColdFormed);

            Assert.Contains("\"manufacture\": \"ColdFormed\"", model.ToJson());
        }

        [Fact]
        public void Warns_AProfileNamedWithNoSource()
        {
            var model = SampleModels.Build();
            model.Sections.Single(s => s.Id == 1).Catalogue = new SectionCatalogue(null, "IPE300");

            AssertWarns(model, "Section 1 names profile \"IPE300\" with no source; the same designation " +
                               "names different profiles in different libraries.");
        }

        [Fact]
        public void Accepts_AShapeWithACatalogueAndNoProperties()
        {
            // Geometry is the fallback: an ishape hands the receiver four dimensions,
            // and every program FEMEX targets can integrate them.
            var model = SampleModels.Build();
            model.Sections.Add(new ISection(4, "IPE300", 0.150, 0.0107, 0.0071, 0.300)
            {
                Catalogue = new SectionCatalogue("Euronorm", "IPE300", SectionManufacture.HotRolled),
            });

            Assert.Empty(model.Validate());
        }

        [Fact]
        public void AnUnknownMemberInsideCatalogue_RoundTripsAndIsReported()
        {
            const string json = """
                {
                  "schemaVersion": "1.6",
                  "sections": [
                    { "type": "rectangle", "id": 1, "name": "R1", "width": 0.3, "depth": 0.5,
                      "catalogue": { "source": "Euronorm", "profile": "R1", "formCode": "I" } }
                  ]
                }
                """;

            var model = FemexModel.FromJson(json);

            AssertWarns(model, "\"formCode\", on Section 1 catalogue");

            var restored = FemexModel.FromJson(model.ToJson());
            SectionCatalogue catalogue = restored.Sections.Single(s => s.Id == 1).Catalogue!;

            Assert.Equal("I", catalogue.UnknownMembers!["formCode"].GetString());
        }

        // ----- The reference files -----

        [Fact]
        public void Example2_LoadsAndValidates()
        {
            // The first file in the repository a steel adapter author can read: all
            // three layers together, and silent — every section, material and load
            // case named, one case carrying self-weight, and the stated areas within
            // fillet distance of the dimensions.
            var model = FemexModel.Load(Example2Path);

            Assert.Empty(model.Validate());

            var column = model.Sections.Single(s => s.Id == 1);
            Assert.IsType<ISection>(column);
            Assert.Equal("HEB240", column.Catalogue!.Profile);
            Assert.Equal(SectionManufacture.HotRolled, column.Catalogue.Manufacture);
            Assert.Equal(1.060e-2, column.GetArea());

            // Identity and geometry, no numbers; and identity and numbers, no
            // geometry — the two halves of the degradation the layering buys.
            Assert.Null(model.Sections.Single(s => s.Id == 3).Properties);
            Assert.IsType<GenericSection>(model.Sections.Single(s => s.Id == 4));
        }

        [Fact]
        public void Example2_ReSerializesToItself()
        {
            // What proves the three layers serialize in a stable order.
            Assert.Equal(File.ReadAllText(Example2Path), FemexModel.Load(Example2Path).ToJson());
        }

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
