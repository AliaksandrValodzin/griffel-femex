using griffel_femex.Geometry;
using griffel_femex.Geometry.Sections;
using griffel_femex.Loads;
using griffel_femex.Materials;
using Xunit;

namespace griffel_femex.Tests
{
    /// <summary>
    /// Gravity, density and the self-weight helpers — the arithmetic γ = ρ·g, γ·A and
    /// γ·t, the 1.1 <c>unitWeight</c> migration, and the rule that a self-weight
    /// intensity is a vector because a wall's weight is not along its normal.
    /// </summary>
    public class SelfWeightTests
    {
        private const int Places = 9;

        /// <summary>Standard gravity, and the weight density of the sample's concrete.</summary>
        private const double G = 9.80665;
        private const double Density = 2.5;
        private const double WeightDensity = Density * G;   // 24.516625 kN/m³

        private static void AssertVector(Vector3d expected, Vector3d actual)
        {
            Assert.Equal(expected.X, actual.X, Places);
            Assert.Equal(expected.Y, actual.Y, Places);
            Assert.Equal(expected.Z, actual.Z, Places);
        }

        // ----- Defaults and round trip -----

        [Fact]
        public void NewModel_HasGravityDownAtStandardAcceleration()
        {
            var model = new FemexModel();

            Assert.Equal(0.0, model.Gravity.Dx);
            Assert.Equal(0.0, model.Gravity.Dy);
            Assert.Equal(-1.0, model.Gravity.Dz);
            Assert.Equal(G, model.Gravity.Acceleration);
        }

        [Fact]
        public void NewLoadCase_CarriesNoSelfWeight()
        {
            Assert.Equal(0.0, new LoadCase().SelfWeightFactor);
            Assert.Equal(0.0, new LoadCase(1, "Dead", LoadNature.Dead).SelfWeightFactor);
        }

        [Fact]
        public void Gravity_RoundTrips()
        {
            var model = SampleModels.Build();
            model.Gravity = new Gravity(0.0, -0.6, -0.8, 9.81);

            var restored = FemexModel.FromJson(model.ToJson());

            Assert.Equal(0.0, restored.Gravity.Dx);
            Assert.Equal(-0.6, restored.Gravity.Dy);
            Assert.Equal(-0.8, restored.Gravity.Dz);
            Assert.Equal(9.81, restored.Gravity.Acceleration);
        }

        [Fact]
        public void Gravity_IsWrittenOnEveryModel()
        {
            // Never omitted when it is the default: a file that does not say which
            // way down is is the problem the block exists to fix.
            string json = new FemexModel().ToJson();

            Assert.Contains("\"gravity\":", json);
            Assert.Contains("\"dz\": -1", json);
            Assert.Contains("\"acceleration\": 9.80665", json);
        }

        [Fact]
        public void SelfWeightFactor_RoundTrips()
        {
            var model = SampleModels.Build();

            string json = model.ToJson();

            // Written on every case, the zero included: "no self-weight here" is a
            // statement the format has to be able to make.
            Assert.Contains("\"selfWeightFactor\": 1", json);
            Assert.Contains("\"selfWeightFactor\": 0", json);

            var restored = FemexModel.FromJson(json);
            Assert.Equal(1.0, restored.LoadCases.Single(c => c.Number == 1).SelfWeightFactor);
            Assert.Equal(0.0, restored.LoadCases.Single(c => c.Number == 2).SelfWeightFactor);
        }

        [Fact]
        public void SelfWeight_JsonHasNoUnitWeightKey()
        {
            string json = SampleModels.Build().ToJson();

            Assert.Contains("\"density\": 2.5", json);
            Assert.DoesNotContain("unitWeight", json);
        }

        // ----- Migration from 1.1 -----

        /// <summary>A 1.1 model whose one material states γ under the old key.</summary>
        private const string LegacyJson = """
            {
              "schemaVersion": "1.1",
              "materials": [
                { "id": 1, "name": "Concrete C30", "modulusOfElasticity": 33000000,
                  "poissonsRatio": 0.2, "unitWeight": 25, "strength": 30000 }
              ]
            }
            """;

        [Fact]
        public void LegacyUnitWeight_IsReadAsADensity()
        {
            var model = FemexModel.FromJson(LegacyJson);

            Assert.Equal(25.0 / G, model.Materials.Single().Density, Places);
        }

        [Fact]
        public void LegacyUnitWeight_IsNeverWrittenBack()
        {
            var model = FemexModel.FromJson(LegacyJson);

            string json = model.ToJson();

            // The binder is getter-less, so System.Text.Json cannot re-emit the key
            // whatever the migration does.
            Assert.DoesNotContain("unitWeight", json);
            Assert.Contains("\"density\":", json);
        }

        [Fact]
        public void LegacyUnitWeight_SurvivesAsTheSameWeightDensity()
        {
            var model = FemexModel.FromJson(LegacyJson);

            // ρ = γ/g on load and γ = ρ·g here read the same acceleration, so the
            // physics is preserved exactly whatever units the file was written in.
            // This is the result that says the migration was right rather than
            // merely self-consistent.
            Assert.Equal(25.0, model.GetWeightDensity(1), 12);
        }

        [Fact]
        public void LegacyUnitWeight_UsesTheModelsOwnGravity()
        {
            // "gravity" placed after "materials" on purpose: the migration runs from
            // IJsonOnDeserialized, once the whole document is read, so it cannot
            // depend on key order.
            const string json = """
                {
                  "schemaVersion": "1.1",
                  "materials": [ { "id": 1, "unitWeight": 25 } ],
                  "gravity": { "dx": 0, "dy": 0, "dz": -1, "acceleration": 10 }
                }
                """;

            var model = FemexModel.FromJson(json);

            Assert.Equal(2.5, model.Materials.Single().Density, Places);
            Assert.Equal(25.0, model.GetWeightDensity(1), Places);
        }

        [Fact]
        public void LegacyUnitWeight_IsIgnoredWhenADensityIsAlsoPresent()
        {
            const string json = """
                {
                  "schemaVersion": "1.1",
                  "materials": [ { "id": 1, "unitWeight": 25, "density": 7.85 } ]
                }
                """;

            var model = FemexModel.FromJson(json);

            // The two cannot both be right, and the newer spelling wins.
            Assert.Equal(7.85, model.Materials.Single().Density);
            AssertWarns(model, "carries both a unitWeight and a density");
        }

        [Fact]
        public void ToJson_UpgradesALegacySchemaVersionStamp()
        {
            var model = FemexModel.FromJson(LegacyJson);
            Assert.Equal("1.1", model.SchemaVersion);

            string json = model.ToJson();

            // Reading it migrated it, so what is being written is the current
            // format; a "1.1" stamp on a document carrying "density" would be a file
            // that lies about itself.
            Assert.Equal(FemexModel.CurrentSchemaVersion, model.SchemaVersion);
            Assert.StartsWith("{" + Environment.NewLine + "  \"schemaVersion\": \"1.4\",", json);
        }

        private static void AssertWarns(FemexModel model, string fragment)
        {
            var messages = model.Validate().ToList();
            Assert.True(
                messages.Any(m => m.Severity == ValidationSeverity.Warning && m.Text.Contains(fragment)),
                $"Expected a warning containing \"{fragment}\". Got: {string.Join(" | ", messages)}");
        }

        // ----- Gravity and weight density -----

        [Fact]
        public void GetGravityDirection_IsStraightDownByDefault()
        {
            AssertVector(new Vector3d(0.0, 0.0, -1.0), new FemexModel().GetGravityDirection());
        }

        [Fact]
        public void GetGravityDirection_IsNormalized()
        {
            var model = new FemexModel { Gravity = new Gravity(0.0, 0.0, -G, G) };

            // The vector's own magnitude is discarded, so writing 9.80665 in both
            // places is harmless rather than a gravity of 96.
            AssertVector(new Vector3d(0.0, 0.0, -1.0), model.GetGravityDirection());
        }

        [Fact]
        public void GetWeightDensity_IsDensityTimesAcceleration()
        {
            Assert.Equal(WeightDensity, SampleModels.Build().GetWeightDensity(1), Places);
        }

        [Fact]
        public void GetWeightDensity_IsZeroForAnUnknownMaterial()
        {
            // GetTotalFactor's precedent: 0.0 rather than a Try. The dangling
            // reference itself is already an error Validate() reports.
            Assert.Equal(0.0, SampleModels.Build().GetWeightDensity(99));
        }

        // ----- Bars -----

        [Fact]
        public void BarSelfWeightPerLength_IsWeightDensityTimesArea()
        {
            var model = SampleModels.Build();

            Assert.True(model.TryGetBarSelfWeightPerLength(SampleModels.BarId, out Vector3d w));

            // A 300x500 column at ρ = 2.5 and g = 9.80665: 0.15 m² x 24.517 kN/m³.
            const double area = 0.3 * 0.5;
            Assert.Equal(WeightDensity * area, w.Length, Places);
            Assert.Equal(3.67749375, w.Length, 8);
        }

        [Fact]
        public void BarSelfWeightPerLength_PointsAlongGravity()
        {
            var model = SampleModels.Build();

            Assert.True(model.TryGetBarSelfWeightPerLength(SampleModels.BarId, out Vector3d w));

            AssertVector(new Vector3d(0.0, 0.0, -3.67749375), w);
        }

        [Fact]
        public void BarSelfWeightPerLength_FollowsATiltedGravityVector()
        {
            var model = SampleModels.Build();
            model.Gravity = new Gravity(0.0, 0.6, -0.8, G);

            Assert.True(model.TryGetBarSelfWeightPerLength(SampleModels.BarId, out Vector3d w));

            const double magnitude = 3.67749375;
            AssertVector(new Vector3d(0.0, 0.6 * magnitude, -0.8 * magnitude), w);
        }

        [Fact]
        public void BarSelfWeightPerLength_IsNotFound_ForAnUnknownBar()
        {
            var model = SampleModels.Build();

            Assert.False(model.TryGetBarSelfWeightPerLength(999, out Vector3d w));
            Assert.Equal(Vector3d.Zero, w);
        }

        // ----- Plates and regions -----

        [Fact]
        public void PlateSelfWeightPerArea_IsWeightDensityTimesThickness()
        {
            var model = SampleModels.Build();

            Assert.True(model.TryGetPlateSelfWeightPerArea(SampleModels.SlabId, out Vector3d w));

            AssertVector(new Vector3d(0.0, 0.0, -WeightDensity * 0.25), w);
        }

        [Fact]
        public void PlateSelfWeightPerArea_Region_UsesItsOwnThickness()
        {
            var model = SampleModels.Build();

            Assert.True(model.TryGetPlateSelfWeightPerArea(
                SampleModels.SlabId, SampleModels.DropPanelRegionId, out Vector3d w));

            // The drop panel's own 0.45, not the slab's 0.25.
            AssertVector(new Vector3d(0.0, 0.0, -WeightDensity * 0.45), w);
        }

        [Fact]
        public void PlateSelfWeightPerArea_Region_InheritsThePlatesMaterial()
        {
            var model = SampleModels.Build();
            model.Materials.Add(new Material(2, "Concrete C40", 35e9, 0.2, 4.0, 40e6));

            // The drop panel names no material of its own, so it takes the slab's —
            // and changing the slab's changes the region's with it.
            model.Slab().MaterialId = 2;

            Assert.True(model.TryGetPlateSelfWeightPerArea(
                SampleModels.SlabId, SampleModels.DropPanelRegionId, out Vector3d w));

            AssertVector(new Vector3d(0.0, 0.0, -4.0 * G * 0.45), w);
        }

        [Fact]
        public void PlateSelfWeightPerArea_IsZeroForAnOpening()
        {
            var model = SampleModels.Build();

            // A definite answer, not a refusal: the stair void weighs nothing, and a
            // caller summing regions should not have to branch.
            Assert.True(model.TryGetPlateSelfWeightPerArea(
                SampleModels.SlabId, SampleModels.VoidRegionId, out Vector3d w));

            Assert.Equal(Vector3d.Zero, w);
        }

        [Fact]
        public void PlateSelfWeightPerArea_IsZeroForALoadOnlyPanel()
        {
            var model = SampleModels.Build();
            model.Slab().Regions.Single(r => r.Id == SampleModels.DropPanelRegionId).Kind =
                PlateRegionKind.LoadOnly;

            Assert.True(model.TryGetPlateSelfWeightPerArea(
                SampleModels.SlabId, SampleModels.DropPanelRegionId, out Vector3d w));

            Assert.Equal(Vector3d.Zero, w);
        }

        [Fact]
        public void PlateSelfWeightPerArea_IsNotFound_ForAnUnknownRegion()
        {
            var model = SampleModels.Build();

            Assert.False(model.TryGetPlateSelfWeightPerArea(SampleModels.SlabId, 99, out Vector3d w));
            Assert.Equal(Vector3d.Zero, w);
            Assert.False(model.TryGetPlateSelfWeightPerArea(999, out w));
        }

        [Fact]
        public void PlateSelfWeightPerArea_OnAWall_ActsDownward_NotAlongTheNormal()
        {
            var model = SampleModels.Build();

            Assert.True(model.TryGetPlateLocalAxes(SampleModels.WallId, out _, out _, out Vector3d normal));
            Assert.True(model.TryGetPlateSelfWeightPerArea(SampleModels.WallId, out Vector3d w));

            // The wall stands in the y = 0 plane, so its normal is horizontal while
            // its weight is vertical: a scalar plus an implied axis would report the
            // weight as a lateral pressure. This is why the helpers return a vector.
            Assert.Equal(0.0, normal.Z, Places);
            Assert.Equal(0.0, w.Dot(normal), Places);
            AssertVector(new Vector3d(0.0, 0.0, -WeightDensity * 0.30), w);
        }

        // ----- Load cases -----

        [Fact]
        public void SelfWeight_IsUnfactored_ByTheCasesFactor()
        {
            var model = SampleModels.Build();

            Assert.True(model.TryGetBarSelfWeightPerLength(SampleModels.BarId, out Vector3d before));

            model.LoadCases.Single(c => c.Number == 1).SelfWeightFactor = 2.0;
            Assert.True(model.TryGetBarSelfWeightPerLength(SampleModels.BarId, out Vector3d after));

            // The helper answers "what does this bar weigh", not "what does case 1
            // apply"; multiplying inside it would make the answer depend on which
            // case was asking.
            AssertVector(before, after);
        }

        [Fact]
        public void GetSelfWeightCases_ReturnsOnlyCasesWithANonZeroFactor()
        {
            var model = SampleModels.Build();

            Assert.Equal(1, Assert.Single(model.GetSelfWeightCases()).Number);

            model.LoadCases.Single(c => c.Number == 1).SelfWeightFactor = 0.0;
            Assert.Empty(model.GetSelfWeightCases());
        }

        // ----- The reference file -----

        [Fact]
        public void Example1_SelfWeightResolves()
        {
            string path = Path.Combine(AppContext.BaseDirectory, "Examples", "Example1.femex");
            var model = FemexModel.Load(path);

            // Case 6 alone carries the weight; case 1 is superimposed dead load and
            // says so with a zero factor.
            var selfWeight = Assert.Single(model.GetSelfWeightCases());
            Assert.Equal(6, selfWeight.Number);
            Assert.Equal("Dead - self weight", selfWeight.Label);
            Assert.Equal(1.0, selfWeight.SelfWeightFactor);

            // A named column: a D450 circular section in C40/50, ρ = 2.5.
            var column = model.Bars.Single(b => b.Id == 1100);
            var section = model.Sections.Single(s => s.Id == column.SectionId);
            Assert.Equal("COL-D450", section.Name);

            double expected = model.GetWeightDensity(column.MaterialId) * section.CalculateArea();
            Assert.Equal(2.5 * G, model.GetWeightDensity(column.MaterialId), Places);

            Assert.True(model.TryGetBarSelfWeightPerLength(column.Id, out Vector3d w));
            AssertVector(model.GetGravityDirection() * expected, w);
            AssertVector(new Vector3d(0.0, 0.0, -1.0), model.GetGravityDirection());
            Assert.Equal(expected, w.Length, Places);
        }
    }
}
