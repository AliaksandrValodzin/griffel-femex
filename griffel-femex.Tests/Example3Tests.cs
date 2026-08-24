using griffel_femex.BoundaryConditions;
using griffel_femex.Geometry;
using griffel_femex.Geometry.Sections;
using griffel_femex.Geometry.Surfaces;
using griffel_femex.Loads;
using griffel_femex.Loads.Combinations;
using griffel_femex.Materials;
using Xunit;

namespace griffel_femex.Tests
{
    /// <summary>
    /// The third reference file: everything 1.7 and 1.8 added, in one small model a
    /// human can read.
    ///
    /// Example1 is the general one and Example2 the steel one; both were written
    /// before this pair of bumps and were migrated into them. This one is
    /// <b>authored</b> in them, which is a different kind of evidence: it is what an
    /// adapter author reads to see what a typed material, a typed unit convention,
    /// an uplift-free bearing and a bedding modulus look like written down together.
    ///
    /// A glulam beam on two uplift-free bearings, and a concrete raft on soil.
    /// <b>Two fragments in one file rather than one building</b>, and deliberately —
    /// each is here for what it demonstrates, and a model contrived to join them
    /// would have taught less per line. Timber because it is the material whose
    /// measured G is nothing like E/(2(1+ν)), so the stated-wins rule is visible and
    /// not merely present; bearings because a pad that cannot take uplift is SAF's
    /// <c>Compression only</c> and 1.7 could only write it as a support that resists
    /// the uplift; a raft because a <c>Restraint.Stiffness</c> on an area support is
    /// a bedding modulus and until 1.8 nothing in FEMEX said so.
    /// </summary>
    public class Example3Tests
    {
        private static string Example3Path =>
            Path.Combine(AppContext.BaseDirectory, "Examples", "Example3.femex");

        /// <summary>
        /// The model the file holds, in code — so that what the file is can be read
        /// as a program as well as as JSON, and so that the identity fact below
        /// compares the file against something rather than against itself.
        /// </summary>
        internal static FemexModel Build()
        {
            var model = new FemexModel
            {
                SchemaVersion = FemexModel.CurrentSchemaVersion,
                Metadata = new FileMetadata("hand-authored", null, "FEMEX example 3 - glulam beam on a raft",
                                            "2026-08-24T00:00:00Z"),

                // All five, which is what a check report needs to print a number with
                // a unit beside it. Millimetres would have done as well; metres and
                // kilonewtons make the tonne the consistent mass unit, and the
                // density below is 0.42 rather than 420 because of it.
                Units = new Units(LengthUnit.Metre, ForceUnit.Kilonewton)
                {
                    Temperature = TemperatureUnit.Celsius,
                    Angle = AngleUnit.Degree,
                    Mass = MassUnit.Tonne,
                },

                Levels =
                {
                    new Level(0, "Ground", 0.0, 0.0, isGround: true),
                    new Level(1, "Beam", 3.0, 3.0),
                },
                Nodes =
                {
                    // The beam, 6 m between pad centres, 3 m above the raft.
                    new Node(1, 0.0, 0.0, levelNumber: 1),
                    new Node(2, 6.0, 0.0, levelNumber: 1),

                    // The raft, 6 x 4 at ground.
                    new Node(11, 0.0, -2.0, levelNumber: 0),
                    new Node(12, 6.0, -2.0, levelNumber: 0),
                    new Node(13, 6.0, 2.0, levelNumber: 0),
                    new Node(14, 0.0, 2.0, levelNumber: 0),
                },
                Sections = { new Rectangle(1, "GL 200x600", 0.2, 0.6) },
                SurfaceProperties = { new ConstantThickness(1, "RAFT-400", 0.40) },
                Materials =
                {
                    // The timber. Its G is stated and is nothing like E/(2(1+ν)) —
                    // 650 MPa against 4 423 — which is the whole of the stated-wins
                    // rule, visible in one file.
                    new Material(1, "Glulam GL24h", 11.5e6, 0.3, 0.42, 24e3)
                    {
                        Type = MaterialType.Timber,
                        Quality = "GL24h",
                        ShearModulus = 650e3,
                        ThermalExpansion = 5e-6,
                        Properties = new MaterialProperties
                        {
                            E005 = 9.6e6,
                            E90Mean = 3.0e5,
                            Fmk = 24e3,
                            Ft0k = 19.2e3,
                            Ft90k = 0.5e3,
                            Fc0k = 24e3,
                            Fc90k = 2.5e3,
                            Fvk = 3.5e3,
                        },
                    },

                    new Material(2, "Concrete C30/37", 33e6, 0.2, 2.5, 30e3)
                    {
                        Type = MaterialType.Concrete,
                        Quality = "C30/37",
                        ThermalExpansion = 1e-5,
                        Properties = new MaterialProperties { Fck = 30e3, Fctm = 2.9e3 },
                    },
                },
                LoadCases =
                {
                    new LoadCase(1, "Dead", LoadNature.Dead, selfWeightFactor: 1.0),
                    new LoadCase(2, "Imposed", LoadNature.Live),
                    new LoadCase(3, "Thermal", LoadNature.Temperature),
                },
            };

            model.Bars.Add(new Bar(1, startNodeId: 1, endNodeId: 2, sectionId: 1, materialId: 1));

            model.Plates.Add(new Plate(10, new List<int> { 11, 12, 13, 14 }, surfacePropertyId: 1, materialId: 2)
            {
                Name = "Raft",
            });

            model.Loads.Add(new LinearLoad
            {
                Id = 1,
                Label = "Imposed on beam",
                LoadCaseNumber = 2,
                StartNode = 1,
                EndNode = 2,
                BarId = 1,
                MagnitudeStart = -4.0,
                MagnitudeEnd = -4.0,
            });

            // A summer swing on a beam that states an α, so the temperature reaches
            // something that can turn it into a strain — the 1.7 inconsistency, not
            // reproduced.
            model.Loads.Add(new TemperatureLoad
            {
                Id = 2,
                Label = "Summer",
                LoadCaseNumber = 3,
                ElementIds = { 1 },
                DeltaT = 25.0,
            });

            model.LoadCombinations.Add(new LoadCombination(101, "ULS", LimitState.Ultimate)
            {
                Terms =
                {
                    new LoadCombinationTerm(1, 1.35),
                    new LoadCombinationTerm(2, 1.50),
                    new LoadCombinationTerm(3, 0.90),
                },
            });

            // The pads. Rigid horizontally, and vertically an uplift-free bearing:
            // SAF's Compression only, which 1.7 could only write as a rigid support
            // that resists an uplift the real pad cannot.
            foreach (int nodeId in new[] { 1, 2 })
            {
                model.Supports.Add(new Support(nodeId, SupportTarget.Point, new List<int> { nodeId })
                {
                    Ux = Restraint.FixedDof(),
                    Uy = Restraint.FixedDof(),
                    Uz = Restraint.CompressionOnly(),
                    Rx = Restraint.Free(),
                    Ry = Restraint.Free(),
                    Rz = Restraint.Free(),
                });
            }

            // The raft on soil. 50 000 kN/m³ is a Winkler bedding modulus — SAF's
            // C1z — and it is a bedding modulus rather than a total spring because
            // the support's target is Area. SAF's Pasternak C2 has no home here.
            model.Supports.Add(new Support(3, SupportTarget.Area, new List<int>())
            {
                PlateId = 10,
                Uz = Restraint.Spring(50000.0),
            });

            return model;
        }

        [Fact]
        public void Example3_LoadsAndValidates()
        {
            var model = FemexModel.Load(Example3Path);

            Assert.Equal(FemexModel.CurrentSchemaVersion, model.SchemaVersion);
            Assert.Empty(model.Validate());
        }

        [Fact]
        public void Example3_ReSerializesToItself()
        {
            Assert.Equal(File.ReadAllText(Example3Path), FemexModel.Load(Example3Path).ToJson());
        }

        [Fact]
        public void Example3_IsTheModelBuiltAbove()
        {
            // The file and the code say the same thing, which is what lets either be
            // read as the explanation of the other.
            Assert.Equal(File.ReadAllText(Example3Path), Build().ToJson());
        }

        [Fact]
        public void Example3_StatesAllFiveUnits()
        {
            var units = FemexModel.Load(Example3Path).Units!;

            Assert.Equal(LengthUnit.Metre, units.Length);
            Assert.Equal(ForceUnit.Kilonewton, units.Force);
            Assert.Equal(TemperatureUnit.Celsius, units.Temperature);
            Assert.Equal(AngleUnit.Degree, units.Angle);
            Assert.Equal(MassUnit.Tonne, units.Mass);
        }

        [Fact]
        public void Example3_TimberStatesAGThatIsNotTheIsotropicQuotient()
        {
            var timber = FemexModel.Load(Example3Path).Materials.Single(m => m.Id == 1);

            Assert.Equal(MaterialType.Timber, timber.Type);
            Assert.Equal("GL24h", timber.Quality);
            Assert.Equal(650e3, timber.GetShearModulus());

            // The stated one wins, and the file is the demonstration of why it must:
            // deriving instead would put 4 423 MPa into every shear deformation of
            // this beam.
            Assert.NotEqual(timber.ModulusOfElasticity / (2 * 1.3), timber.GetShearModulus(), 3);
        }

        [Fact]
        public void Example3_TheBearingsCannotResistUplift()
        {
            var model = FemexModel.Load(Example3Path);

            foreach (int id in new[] { 1, 2 })
            {
                var pad = model.Supports.Single(s => s.Id == id);

                Assert.True(pad.Uz.Fixed);
                Assert.Equal(RestraintSense.CompressionOnly, pad.Uz.Sense);

                // Horizontally bidirectional, which is what null still means.
                Assert.Null(pad.Ux.Sense);
            }
        }

        [Fact]
        public void Example3_TheRaftStatesABeddingModulus()
        {
            var model = FemexModel.Load(Example3Path);
            var raft = model.Supports.Single(s => s.Id == 3);

            Assert.Equal(SupportTarget.Area, raft.Target);
            Assert.Equal(10, raft.PlateId);
            Assert.Equal(50000.0, raft.Uz.Stiffness);

            // Force per length cubed, and readable only because the model says which
            // force and which length — the rule the area-support warning enforces.
            Assert.NotNull(model.Units!.Length);
            Assert.NotNull(model.Units.Force);
        }
    }
}
