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
    /// The third reference file: everything 1.7 through 1.11 added, in one small
    /// model a human can read.
    ///
    /// Example1 is the general one and Example2 the steel one; both were written
    /// before these bumps and were migrated into them. This one is <b>authored</b> in
    /// them, which is a different kind of evidence: it is what an adapter author
    /// reads to see what a typed material, a typed unit convention, an uplift-free
    /// bearing, a bedding modulus, a load group, a one-way panel, a tension-only
    /// member and a tapered one look like written down together.
    ///
    /// <b>Fragments in one file rather than one building</b>, and deliberately —
    /// each is here for what it demonstrates, and a model contrived to join them
    /// would have taught less per line.
    ///
    /// <list type="bullet">
    /// <item><b>1.7</b> — timber, because it is the material whose measured G is
    /// nothing like E/(2(1+ν)), so the stated-wins rule is visible and not merely
    /// present.</item>
    /// <item><b>1.8</b> — bearings, because a pad that cannot take uplift is SAF's
    /// <c>Compression only</c> and 1.7 could only write it as a support that resists
    /// the uplift; and a raft, because a <c>Restraint.Stiffness</c> on an area
    /// support is a bedding modulus and until 1.8 nothing in FEMEX said so.</item>
    /// <item><b>1.9</b> — three load groups, one per action category, which is what
    /// SAF's mandatory <c>Load group</c> column is filled from; a deck panel that
    /// spans <i>one way</i> onto the two beams under it, which is the concept a 1.8
    /// file could not state and which a receiver therefore read as two-way; a
    /// point load at a station along a member, which needs no node minted for it; and
    /// a signed thermal gradient.</item>
    /// <item><b>1.10</b> — a tension-only tie, a beam <i>tapered</i> to a haunch at
    /// one end, a system line that is the top of the section rather than its
    /// centroid, and an eccentricity block that states the structural and the
    /// analysis offset separately.</item>
    /// <item><b>1.11</b> — a line load along the deck's edge, in that edge's own
    /// frame and over the middle half of it, which is the shape no 1.10 file could
    /// hold: a local direction and a partial extent with no bar to measure either
    /// against.</item>
    /// </list>
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
                Metadata = new FileMetadata("hand-authored", null,
                                            "FEMEX example 3 - glulam floor on a raft",
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
                    // The two beams, 6 m between pad centres and 4 m apart, 3 m
                    // above the raft. The deck panel spans between them.
                    new Node(1, 0.0, 0.0, levelNumber: 1),
                    new Node(2, 6.0, 0.0, levelNumber: 1),
                    new Node(3, 0.0, 4.0, levelNumber: 1),
                    new Node(4, 6.0, 4.0, levelNumber: 1),

                    // The raft, 6 x 4 at ground.
                    new Node(11, 0.0, -2.0, levelNumber: 0),
                    new Node(12, 6.0, -2.0, levelNumber: 0),
                    new Node(13, 6.0, 2.0, levelNumber: 0),
                    new Node(14, 0.0, 2.0, levelNumber: 0),
                },
                Sections =
                {
                    new Rectangle(1, "GL 200x600", 0.2, 0.6),

                    // The haunch the first beam tapers into. Same shape as the
                    // section it tapers from, which is what makes the taper
                    // buildable; a rectangle varying into a circle is not.
                    new Rectangle(2, "GL 200x900", 0.2, 0.9),

                    new Circle(3, "ROD-24", 0.024),
                },
                SurfaceProperties =
                {
                    new ConstantThickness(1, "RAFT-400", 0.40),
                    new ConstantThickness(2, "DECK-180", 0.18),
                },
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

                    new Material(3, "Steel S355", 210e6, 0.3, 7.85, 355e3)
                    {
                        Type = MaterialType.Steel,
                        Quality = "S355",
                        ThermalExpansion = 1.2e-5,
                    },
                },

                // One group per action category, which is what SAF's mandatory
                // Load group column is filled from. Every relation here is
                // Standard, and that is a statement rather than a default: the two
                // producers in the SAF reference corpus disagree about which
                // relation a variable group takes, so an exporter that has to
                // invent one is guessing.
                LoadGroups =
                {
                    new LoadGroup(1, "G - permanent", LoadGroupType.Permanent),
                    new LoadGroup(2, "Q - imposed", LoadGroupType.Variable),
                    new LoadGroup(3, "T - thermal", LoadGroupType.Variable),
                },
                LoadCases =
                {
                    new LoadCase(1, "Dead", LoadNature.Dead, selfWeightFactor: 1.0) { LoadGroupId = 1 },
                    new LoadCase(2, "Imposed", LoadNature.Live) { LoadGroupId = 2 },
                    new LoadCase(3, "Thermal", LoadNature.Temperature) { LoadGroupId = 3 },
                },
            };

            // The first beam, tapered into a haunch at its far end and set out on
            // the top of the section rather than its centroid — which is what a
            // floor level is, and which SAF marks mandatory and fills with
            // something other than Centre on four fifths of the members in its own
            // reference file.
            model.Bars.Add(new Bar(1, startNodeId: 1, endNodeId: 2, sectionId: 1, materialId: 1)
            {
                EndSectionId = 2,
                Alignment = BarAlignment.Top,
            });

            // The second beam, carrying the eccentricity block: the deck bears on
            // its top flange, 300 mm off the setting-out line structurally, and the
            // analysis line is taken 20 mm off it as well. The two are stated apart
            // because they do different things — the first moves the picture, the
            // second moves the answer.
            model.Bars.Add(new Bar(2, startNodeId: 3, endNodeId: 4, sectionId: 1, materialId: 1)
            {
                Alignment = BarAlignment.Top,
                Eccentricity = new BarEccentricity
                {
                    StructuralZBegin = -0.30,
                    StructuralZEnd = -0.30,
                    AnalysisZBegin = -0.02,
                    AnalysisZEnd = -0.02,
                },
            });

            // A tie back to the raft. Tension only, so no compression is attracted
            // to it whatever its stiffness — the concept a 1.9 file could not state
            // and which a receiver therefore built as a strut.
            model.Bars.Add(new Bar(3, startNodeId: 11, endNodeId: 1, sectionId: 3, materialId: 3)
            {
                Behaviour = BarBehaviour.TensionOnly,
            });

            model.Plates.Add(new Plate(10, new List<int> { 11, 12, 13, 14 }, surfacePropertyId: 1, materialId: 2)
            {
                Name = "Raft",
            });

            // The deck. Its local x runs node 1 to node 2, along the beams, so a
            // panel that spans across onto them carries its load along local y —
            // and it names the two members that receive it, which is SAF's
            // "Load applied to" and the row of its reference file that most needed
            // it. A 1.9 reader gets a two-way panel and puts half this load on the
            // wrong supports.
            model.Plates.Add(new Plate(11, new List<int> { 1, 2, 4, 3 }, surfacePropertyId: 2, materialId: 1)
            {
                Name = "Deck",
                Distribution = new LoadDistribution(SurfaceLoadSpanning.OneWayY)
                {
                    BarIds = new List<int> { 1, 2 },
                },
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
            // reproduced. The gradient is signed from 1.9: +8 per metre of depth
            // means the temperature rises along the beam's local +z, so the top face
            // is the hot one and the beam hogs. The 1.8 spelling could not say
            // which face that was.
            model.Loads.Add(new TemperatureLoad
            {
                Id = 2,
                Label = "Summer",
                LoadCaseNumber = 3,
                ElementIds = { 1 },
                DeltaT = 25.0,
                GradientZ = 8.0,
            });

            // A plant load sitting at a station along the first beam. No node is
            // minted for it and it is not snapped to either end: the position is
            // data about the load, which is what makes it exactly reversible.
            model.Loads.Add(new PointLoad
            {
                Id = 3,
                Label = "Plant",
                LoadCaseNumber = 2,
                BarId = 1,
                Position = 0.4,
                Fz = -12.0,
            });

            model.Loads.Add(new AreaLoad
            {
                Id = 4,
                Label = "Imposed on deck",
                LoadCaseNumber = 2,
                PlateId = 11,
                Magnitude = -2.5,
            });

            // 1.11. A line load along the deck's east edge, stated in the edge's own
            // frame and covering the middle half of it. Node order 2 -> 4 is the
            // edge's order in the deck's contour, and it is what local x runs along;
            // written 4 -> 2 the same load would push the other way.
            model.Loads.Add(new LinearLoad
            {
                Id = 5,
                Label = "Parapet",
                LoadCaseNumber = 2,
                PlateId = 11,
                StartNode = 2,
                EndNode = 4,
                StartPosition = 0.25,
                EndPosition = 0.75,
                CoordinateSystem = LoadCoordinateSystem.Local,
                Direction = LoadDirection.Z,
                MagnitudeStart = -3.0,
                MagnitudeEnd = -3.0,
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
        public void Example3_EveryCaseNamesAGroup_AndNoneDisagreesWithIt()
        {
            // SAF's Load group column is mandatory, so a file that names no group
            // is one an exporter has to invent three rows for. And the nature and
            // the group type are two statements of one category, so the file has to
            // demonstrate them agreeing as well as being present.
            var model = FemexModel.Load(Example3Path);

            Assert.Equal(3, model.LoadGroups.Count);
            Assert.All(model.LoadCases, c => Assert.NotNull(c.LoadGroupId));
            Assert.All(model.LoadGroups, g => Assert.Equal(LoadGroupRelation.Standard, g.Relation));

            Assert.Empty(model.Validate());
        }

        /// <summary>
        /// 1.11's fixture, and the reason it is authored here rather than taken from
        /// a converted workbook: the new validation rules need something to check
        /// that does not move when the SAF adapter does.
        /// </summary>
        [Fact]
        public void Example3_TheParapetRunsAlongTheDecksEdge_InThatEdgesOwnFrame()
        {
            var model = FemexModel.Load(Example3Path);
            var parapet = model.Loads.OfType<LinearLoad>().Single(l => l.Label == "Parapet");

            Assert.Equal(11, parapet.PlateId);
            Assert.Null(parapet.BarId);
            Assert.Null(parapet.RegionId);
            Assert.Equal(LoadCoordinateSystem.Local, parapet.CoordinateSystem);
            Assert.Equal(0.25, parapet.StartPosition);
            Assert.Equal(0.75, parapet.EndPosition);

            // The frame is the edge's, not the panel's: TryGetEdgeLocalAxes is the
            // one call, and it is the one a hinge on this edge would get too.
            Assert.True(model.TryGetLoadDirection(parapet, out Vector3d direction));
            Assert.True(model.TryGetEdgeLocalAxes(11, parapet.StartNode, parapet.EndNode,
                                                  out Vector3d _, out Vector3d _, out Vector3d z));
            Assert.Equal(z.X, direction.X, 9);
            Assert.Equal(z.Y, direction.Y, 9);
            Assert.Equal(z.Z, direction.Z, 9);

            Assert.Empty(model.Validate(ValidationSeverity.Error));
        }

        [Fact]
        public void Example3_TheDeckSpansOneWay_OntoTheTwoBeamsUnderIt()
        {
            var model = FemexModel.Load(Example3Path);
            var deck = model.Plates.Single(p => p.Id == 11);

            // The concept a 1.9 reader has no property for, so it reads a two-way
            // panel and puts half this load on the wrong supports.
            Assert.Equal(SurfaceLoadSpanning.OneWayY, deck.Distribution!.Spanning);
            Assert.Equal(new[] { 1, 2 }, deck.Distribution.BarIds!);

            // On the panel, never on the load: the area load says nothing about
            // spanning and could not contradict this if it wanted to.
            Assert.Single(model.Loads.OfType<AreaLoad>().Where(l => l.PlateId == 11));
        }

        [Fact]
        public void Example3_TheTie_CarriesTensionOnly()
        {
            var tie = FemexModel.Load(Example3Path).Bars.Single(b => b.Id == 3);

            Assert.Equal(BarBehaviour.TensionOnly, tie.Behaviour);
            Assert.Equal(MaterialType.Steel,
                         FemexModel.Load(Example3Path).Materials.Single(m => m.Id == tie.MaterialId).Type);
        }

        [Fact]
        public void Example3_TheHaunchedBeam_TapersAndKeepsItsPrismaticFallback()
        {
            var beam = FemexModel.Load(Example3Path).Bars.Single(b => b.Id == 1);

            Assert.Equal(1, beam.SectionId);
            Assert.Equal(2, beam.EndSectionId);

            // Set out on the top of the section, which is what a floor level is —
            // and which SAF marks mandatory and fills with something other than
            // Centre on four fifths of the members in its own reference file.
            Assert.Equal(BarAlignment.Top, beam.Alignment);
        }

        [Fact]
        public void Example3_TheSecondBeam_StatesBothEccentricities_Separately()
        {
            var eccentricity = FemexModel.Load(Example3Path).Bars.Single(b => b.Id == 2).Eccentricity!;

            // The split, in one file: 300 mm of drawn offset that changes no force,
            // and 20 mm of analysis offset that changes several. A receiver that
            // fused them would apply 300 mm of lever arm.
            Assert.Equal(-0.30, eccentricity.StructuralZBegin);
            Assert.Equal(-0.02, eccentricity.AnalysisZBegin);
            Assert.True(eccentricity.MovesTheAnalysisLine());

            // Derived, so neither helper is in the file.
            Assert.DoesNotContain("isEmpty", File.ReadAllText(Example3Path));
        }

        [Fact]
        public void Example3_ThePlantLoad_SitsAlongAMemberWithNoNodeMintedForIt()
        {
            var model = FemexModel.Load(Example3Path);
            var plant = model.Loads.OfType<PointLoad>().Single(l => l.Label == "Plant");

            Assert.Equal(1, plant.BarId);
            Assert.Equal(0.4, plant.Position);

            // Topology untouched: the file's four level-1 nodes are the two beam
            // lines and nothing else, which is what makes the crossing reversible.
            Assert.Equal(4, model.Nodes.Count(n => n.LevelNumber == 1));
        }

        [Fact]
        public void Example3_TheThermalGradient_IsSigned()
        {
            var summer = FemexModel.Load(Example3Path).Loads.OfType<TemperatureLoad>().Single();

            // +8 per metre along the beam's local +z: the top face is the hot one
            // and the beam hogs. The 1.8 spelling could not say which face that was,
            // which is why the migration reports a reinterpretation.
            Assert.Equal(8.0, summer.GradientZ);
            Assert.Null(summer.GradientY);
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
