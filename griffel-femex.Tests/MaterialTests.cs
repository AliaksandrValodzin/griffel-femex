using griffel_femex.Loads;
using griffel_femex.Materials;
using Xunit;

namespace griffel_femex.Tests
{
    /// <summary>
    /// Schema 1.7: the material says what it is.
    ///
    /// Two of SAF's mandatory columns had no FEMEX home at all —
    /// <c>StructuralMaterial.Type</c> and <c>.Quality</c> — so an exporter could not
    /// write a workbook SAF's own validator would accept without inventing values.
    /// Two more concepts crossed silently wrong: a temperature load with no α behind
    /// it is a number the receiver cannot turn into a strain, and a shear modulus
    /// derived from E and ν is not timber's measured G.
    ///
    /// The design half is <see cref="MaterialProperties"/>, which is deliberately
    /// the same escape hatch <see cref="Geometry.Sections.SectionProperties"/> opened
    /// for sections in 1.5: one optional block of resolved numbers, so a value
    /// crosses even when the receiver has never heard of the grade.
    /// </summary>
    public class MaterialTests
    {
        private static string Example1Path =>
            Path.Combine(AppContext.BaseDirectory, "Examples", "Example1.femex");

        private static string Example2Path =>
            Path.Combine(AppContext.BaseDirectory, "Examples", "Example2.femex");

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

        /// <summary>The sample's one material, which every fact below starts from.</summary>
        private static Material Concrete(FemexModel model) => model.Materials.Single(m => m.Id == 1);

        // ----- What the material is -----

        [Fact]
        public void TypeAndQuality_RoundTrip()
        {
            var model = SampleModels.Build();

            Concrete(model).Type = MaterialType.Timber;
            Concrete(model).Quality = "GL24h";

            var restored = FemexModel.FromJson(model.ToJson());
            var material = Concrete(restored);

            Assert.Equal(MaterialType.Timber, material.Type);
            Assert.Equal("GL24h", material.Quality);

            // The enum crosses as its name, through the model's own converter, with
            // no attribute on the property — the whole point of the global policy.
            Assert.Contains("\"type\": \"Timber\"", model.ToJson());
        }

        [Fact]
        public void Quality_IsNotTheName()
        {
            // Both are strings and a program is free to key on either; they are
            // different statements. The name is what Robot and ETABS index by and an
            // author may call anything; the quality is what a code writes.
            var model = SampleModels.Build();

            Concrete(model).Name = "slab concrete";
            Concrete(model).Quality = "C30/37";

            var restored = FemexModel.FromJson(model.ToJson());

            Assert.Equal("slab concrete", Concrete(restored).Name);
            Assert.Equal("C30/37", Concrete(restored).Quality);
        }

        // ----- The design block -----

        [Fact]
        public void Properties_RoundTripAllTwentyTwo()
        {
            var model = SampleModels.Build();

            Concrete(model).Properties = new MaterialProperties
            {
                Fy = 355e3, Fu = 470e3, FuMinimum = 450e3, Ry = 1.1, Rt = 1.2,
                Fck = 30e3, Fcm = 38e3, Fctm = 2.9e3, Fctk05 = 2.0e3, Fctk95 = 3.8e3,
                EpsC2 = 0.002, EpsCu2 = 0.0035, EpsC3 = 0.00175, EpsCu3 = 0.0035,
                E005 = 9.4e6, E90Mean = 3.7e5, Fmk = 24e3, Ft0k = 16.5e3,
                Ft90k = 0.5e3, Fc0k = 24e3, Fc90k = 2.5e3, Fvk = 3.5e3,
            };

            var properties = Concrete(FemexModel.FromJson(model.ToJson())).Properties!;

            Assert.Equal(355e3, properties.Fy);
            Assert.Equal(470e3, properties.Fu);
            Assert.Equal(450e3, properties.FuMinimum);
            Assert.Equal(1.1, properties.Ry);
            Assert.Equal(1.2, properties.Rt);
            Assert.Equal(30e3, properties.Fck);
            Assert.Equal(38e3, properties.Fcm);
            Assert.Equal(2.9e3, properties.Fctm);
            Assert.Equal(2.0e3, properties.Fctk05);
            Assert.Equal(3.8e3, properties.Fctk95);
            Assert.Equal(0.002, properties.EpsC2);
            Assert.Equal(0.0035, properties.EpsCu2);
            Assert.Equal(0.00175, properties.EpsC3);
            Assert.Equal(0.0035, properties.EpsCu3);
            Assert.Equal(9.4e6, properties.E005);
            Assert.Equal(3.7e5, properties.E90Mean);
            Assert.Equal(24e3, properties.Fmk);
            Assert.Equal(16.5e3, properties.Ft0k);
            Assert.Equal(0.5e3, properties.Ft90k);
            Assert.Equal(24e3, properties.Fc0k);
            Assert.Equal(2.5e3, properties.Fc90k);
            Assert.Equal(3.5e3, properties.Fvk);
        }

        [Fact]
        public void Properties_StatingOneValue_WritesOnlyThatKey()
        {
            // "Not stated" is distinct from zero, and the file says so by omission —
            // the same contract SectionProperties draws.
            var model = new FemexModel
            {
                Materials = { new Material { Id = 1, Properties = new MaterialProperties { Fy = 355e3 } } },
            };

            string json = model.ToJson();

            Assert.Contains("\"fy\": 355000", json);
            Assert.DoesNotContain("\"fu\"", json);
            Assert.DoesNotContain("\"fck\"", json);
            Assert.DoesNotContain("\"fvk\"", json);
        }

        // ----- Additivity -----

        [Fact]
        public void AMaterialUsingNoneOfIt_OmitsTheKeysEntirely()
        {
            // A 1.6 material re-saved as 1.7 gains not one byte. "type" is asserted
            // here rather than only the new scalars because the model holds nothing
            // else that carries a discriminator, so its absence is unambiguous.
            var model = new FemexModel
            {
                Materials = { new Material(1, "Concrete C30", 33e6, 0.2, 2.5, 30e3) },
            };

            string json = model.ToJson();

            Assert.DoesNotContain("\"type\"", json);
            Assert.DoesNotContain("\"quality\"", json);
            Assert.DoesNotContain("\"shearModulus\"", json);
            Assert.DoesNotContain("\"thermalExpansion\"", json);
            Assert.DoesNotContain("\"properties\"", json);
        }

        // ----- Shear modulus -----

        [Fact]
        public void GetShearModulus_DerivesWhenNoneIsStated()
        {
            var material = new Material(1, "Concrete C30", 33e6, 0.2, 2.5, 30e3);

            Assert.Null(material.ShearModulus);
            Assert.Equal(33e6 / (2 * 1.2), material.GetShearModulus(), 9);
        }

        [Fact]
        public void GetShearModulus_PrefersTheStatedOne()
        {
            // Timber is the case: its measured G is nothing like E/(2(1+ν)), and
            // deriving one silently substitutes a different number into every shear
            // deformation downstream. The stated one is the measured one and wins —
            // the identical rule Section.GetArea() states for area.
            var material = new Material(1, "GL24h", 11.5e6, 0.3, 0.42, 24e3)
            {
                Type = MaterialType.Timber,
                ShearModulus = 650e3,
            };

            Assert.Equal(650e3, material.GetShearModulus());
            Assert.NotEqual(material.ModulusOfElasticity / (2 * 1.3), material.GetShearModulus(), 9);
        }

        // ----- Errors: a stated value that is not a quantity -----

        [Fact]
        public void Reports_ANonPositiveShearModulus()
        {
            var model = SampleModels.Build();
            Concrete(model).ShearModulus = 0.0;

            AssertReports(model, "Material 1 states a shearModulus of 0, which is not a positive quantity.");
        }

        [Fact]
        public void Reports_ANonPositiveThermalExpansion()
        {
            var model = SampleModels.Build();
            Concrete(model).ThermalExpansion = -1e-5;

            AssertReports(model, "Material 1 states a thermalExpansion of -1E-05, which is not a positive quantity.");
        }

        [Fact]
        public void Reports_ANonPositiveDesignValue()
        {
            var model = SampleModels.Build();
            Concrete(model).Properties = new MaterialProperties { Fck = 0.0, EpsCu3 = -0.0035 };

            AssertReports(model, "Material 1 states a fck of 0, which is not a positive quantity.");
            AssertReports(model, "Material 1 states an epsCu3 of -0.0035, which is not a positive quantity.");
        }

        [Fact]
        public void Accepts_ADesignBlockOfPositiveValues()
        {
            var model = SampleModels.Build();
            Concrete(model).Properties = new MaterialProperties { Fck = 30e3, Fctm = 2.9e3 };

            Assert.Empty(model.Validate());
        }

        // ----- Warnings: a material a receiver gets wrong -----

        [Fact]
        public void Warns_WhenAMaterialStatesNoType()
        {
            var model = SampleModels.Build();
            Concrete(model).Type = null;
            Concrete(model).Quality = null;

            AssertWarns(model, "Material 1 states no type; a program that has to write one will guess it");
        }

        [Fact]
        public void Warns_WhenAGradeHasNoCodeFamilyBehindIt()
        {
            var model = SampleModels.Build();
            Concrete(model).Type = null;
            Concrete(model).Quality = "C30/37";

            AssertWarns(model, "Material 1 is graded \"C30/37\" but states no type");

            // The graded wording says strictly more, so it replaces the plain one
            // rather than joining it: a material with a quality and no type is a
            // subset of a material with no type, and stating one fact twice is what
            // the repository's messages never do.
            Assert.Single(model.Validate().Where(m => m.Text.Contains("Material 1") && m.Text.Contains("no type")));
        }

        [Fact]
        public void Accepts_AQualityStatedWithItsType()
        {
            Assert.Empty(SampleModels.Build().Validate());
        }

        // ----- Warnings: the thermal inconsistency, made executable -----

        [Fact]
        public void Warns_WhenAThermalLoadReachesAMaterialWithNoExpansion()
        {
            var model = SampleModels.Build();
            Concrete(model).ThermalExpansion = null;

            AssertWarns(model, "Temperature load 'T1' acts on material 1, which states no thermalExpansion");
        }

        [Fact]
        public void Warns_OncePerMaterial_NotOncePerElement()
        {
            var model = SampleModels.Build();
            Concrete(model).ThermalExpansion = null;

            // A bar and two plates, all of the one concrete. A thermal load on a
            // meshed slab names dozens of faces of one material, and one message per
            // face would bury every other message in the report.
            var thermal = model.Loads.OfType<TemperatureLoad>().Single();
            thermal.ElementIds = new List<int> { SampleModels.BarId, SampleModels.SlabId, SampleModels.WallId };

            Assert.Single(model.Validate().Where(m => m.Text.Contains("nothing to turn it into a strain with")));
        }

        [Fact]
        public void Warns_WhenAMeshFaceResolvesItsMaterialThroughItsPanel()
        {
            // The face's resolved cache is a cache, not the only answer: a mesher
            // that left it null must reach the same material as one that filled it,
            // so the lookup falls back to the panel's own resolution.
            var model = SampleModels.Build();
            Concrete(model).ThermalExpansion = null;

            model.Mesh!.Faces.Single().MaterialId = null;
            model.Loads.OfType<TemperatureLoad>().Single().ElementIds =
                new List<int> { SampleModels.MeshFaceId };

            AssertWarns(model, "Temperature load 'T1' acts on material 1, which states no thermalExpansion");
        }

        [Fact]
        public void Accepts_AThermalLoadOnAMaterialThatStatesAlpha()
        {
            var model = SampleModels.Build();

            Assert.Equal(1e-5, Concrete(model).ThermalExpansion);
            Assert.Empty(model.Validate());
        }

        // ----- Reading a 1.6 file -----

        [Fact]
        public void ASixFile_ReadsUnchangedAndIsToldWhatItLacks()
        {
            string json = """
                {
                  "schemaVersion": "1.6",
                  "materials": [
                    {
                      "id": 1,
                      "name": "Concrete C30/37",
                      "modulusOfElasticity": 33000000,
                      "poissonsRatio": 0.2,
                      "density": 2.5,
                      "strength": 30000
                    }
                  ]
                }
                """;

            var model = FemexModel.FromJson(json);
            var material = model.Materials.Single();

            // Nothing is migrated — the bump is purely additive, so every new field
            // is null and the old ones are untouched.
            Assert.Null(material.Type);
            Assert.Null(material.Quality);
            Assert.Null(material.ShearModulus);
            Assert.Null(material.ThermalExpansion);
            Assert.Null(material.Properties);
            Assert.Equal(2.5, material.Density);
            Assert.Equal(30000.0, material.Strength);

            AssertWarns(model, "schemaVersion \"1.6\", written before a material could say what it is");
            AssertWarns(model, "Material 1 states no type");

            // Re-saving stamps the current version and adds no key: the version is
            // the only thing that changed about the file. Written against the
            // constant rather than a literal, because what this fact is about is the
            // 1.7 material staying additive, and the stamp bumps under it.
            string resaved = model.ToJson();
            Assert.Contains($"\"schemaVersion\": \"{FemexModel.CurrentSchemaVersion}\"", resaved);
            Assert.DoesNotContain("\"type\"", resaved);
        }

        // ----- Reading a file from further ahead -----

        [Fact]
        public void AnUnknownMemberOnTheDesignBlock_SurvivesAndIsNamed()
        {
            string json = """
                {
                  "schemaVersion": "1.7",
                  "materials": [
                    {
                      "id": 1,
                      "name": "S355",
                      "type": "Steel",
                      "quality": "S355",
                      "modulusOfElasticity": 210000000,
                      "poissonsRatio": 0.3,
                      "density": 7.85,
                      "properties": {
                        "fy": 355000,
                        "gammaM0": 1.0
                      }
                    }
                  ]
                }
                """;

            var model = FemexModel.FromJson(json);

            AssertWarns(model, "\"gammaM0\", on Material 1 properties");

            var restored = FemexModel.FromJson(model.ToJson());
            var properties = restored.Materials.Single().Properties!;

            Assert.Equal(355000.0, properties.Fy);
            Assert.Equal(1.0, properties.UnknownMembers!["gammaM0"].GetDouble());
        }

        // ----- The reference files -----

        [Fact]
        public void Example1_MaterialsAreTypedAndCanBeHeated()
        {
            // Its thermal load reaches eleven mesh faces of material 1, so the file
            // is only silent because that material now states an α — the check that
            // the new warning is really wired to the elements a load names.
            var model = FemexModel.Load(Example1Path);

            Assert.Equal(FemexModel.CurrentSchemaVersion, model.SchemaVersion);
            Assert.All(model.Materials, m => Assert.Equal(MaterialType.Concrete, m.Type));
            Assert.All(model.Materials, m => Assert.Equal(1e-5, m.ThermalExpansion));

            // No design block anywhere: Example1 is what proves the bump additive,
            // exactly as it proves it for sections.
            Assert.All(model.Materials, m => Assert.Null(m.Properties));
            Assert.Empty(model.Validate());
        }

        [Fact]
        public void Example2_S355StatesItsYield()
        {
            // The first file in the repository where a grade name and the numbers
            // behind it travel together — a receiver with no Euronorm material
            // library still gets 355 out of it.
            var model = FemexModel.Load(Example2Path);
            var steel = model.Materials.Single();

            Assert.Equal(MaterialType.Steel, steel.Type);
            Assert.Equal("S355", steel.Quality);
            Assert.Equal(355000000.0, steel.Properties!.Fy);
            Assert.Equal(470000000.0, steel.Properties.Fu);

            // Its G is not stated, so it is still derived — the two halves of the
            // rule, one file apart.
            Assert.Null(steel.ShearModulus);
            Assert.Equal(210000000000.0 / 2.6, steel.GetShearModulus(), 6);

            Assert.Empty(model.Validate());
        }
    }
}
