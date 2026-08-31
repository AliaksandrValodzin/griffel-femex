using griffel_femex.Geometry;
using griffel_femex.Loads;
using Xunit;

namespace griffel_femex.Tests
{
    /// <summary>
    /// The orientation a distributed load carries, the element local-axis
    /// conventions it is measured against, and the schema version that tells a
    /// file written before all this from one written after.
    /// </summary>
    public class LoadDirectionTests
    {
        /// <summary>Axes are unit vectors, so this is generous by an order of magnitude.</summary>
        private const int Places = 9;

        private static void AssertVector(Vector3d expected, Vector3d actual)
        {
            Assert.Equal(expected.X, actual.X, Places);
            Assert.Equal(expected.Y, actual.Y, Places);
            Assert.Equal(expected.Z, actual.Z, Places);
        }

        private static void AssertReportsError(FemexModel model, string fragment)
        {
            AssertReports(model, ValidationSeverity.Error, fragment);
        }

        private static void AssertReports(FemexModel model, ValidationSeverity severity, string fragment)
        {
            var messages = model.Validate().ToList();
            Assert.True(
                messages.Any(m => m.Severity == severity && m.Text.Contains(fragment)),
                $"Expected a {severity} containing \"{fragment}\". Got: {string.Join(" | ", messages)}");
        }

        // ----- Defaults and the legacy path -----

        [Fact]
        public void NewDistributedLoad_IsGlobalZ_AndNotProjected()
        {
            var area = new AreaLoad();

            Assert.Equal(LoadCoordinateSystem.Global, area.CoordinateSystem);
            Assert.Equal(LoadDirection.Z, area.Direction);
            Assert.False(area.Projected);
            Assert.Null(area.Dx);
        }

        [Fact]
        public void LoadWrittenBeforeDirectionsExisted_ReadsAsGlobalZ()
        {
            // A 1.0 file: no schemaVersion, and a bare magnitude on each
            // distributed load. System.Text.Json leaves a property untouched when
            // its key is absent, so the property initializers are what it gets.
            const string json = """
                {
                  "loadCases": [ { "number": 1, "label": "Dead", "nature": "Dead" } ],
                  "loads": [
                    { "type": "area", "plateId": 1, "magnitude": 1.5, "loadCaseNumber": 1 },
                    { "type": "linear", "startNode": 1, "endNode": 2, "magnitudeStart": 6, "loadCaseNumber": 1 }
                  ]
                }
                """;

            var model = FemexModel.FromJson(json);

            Assert.Null(model.SchemaVersion);

            foreach (var load in model.Loads.OfType<DistributedLoad>())
            {
                Assert.Equal(LoadCoordinateSystem.Global, load.CoordinateSystem);
                Assert.Equal(LoadDirection.Z, load.Direction);
                Assert.False(load.Projected);
            }

            // Which is exactly the trap the version field exists to flag: that 1.5
            // was authored as a downward load and now reads as an upward one.
            AssertReports(model, ValidationSeverity.Warning, "has the wrong sign");
        }

        // ----- Round trip -----

        [Fact]
        public void Orientation_RoundTrips()
        {
            var restored = FemexModel.FromJson(SampleModels.Build().ToJson());

            var wall = restored.AreaLoad("A2");
            Assert.Equal(LoadCoordinateSystem.Local, wall.CoordinateSystem);
            Assert.Equal(LoadDirection.Z, wall.Direction);

            var vector = restored.AreaLoad("A3");
            Assert.Equal(LoadDirection.Vector, vector.Direction);
            Assert.Equal(0.0, vector.Dx);
            Assert.Equal(0.6, vector.Dy);
            Assert.Equal(-0.8, vector.Dz);

            var line = restored.LinearLoad("L2");
            Assert.Equal(SampleModels.BarId, line.BarId);
            Assert.Equal(LoadCoordinateSystem.Local, line.CoordinateSystem);
            Assert.Equal(LoadDirection.Y, line.Direction);

            // The defaults survive as themselves rather than being reconstructed:
            // enums and bools are never null, so they are always written.
            Assert.Equal(LoadCoordinateSystem.Global, restored.AreaLoad("A1").CoordinateSystem);
            Assert.Null(restored.LinearLoad("L1").BarId);
        }

        [Fact]
        public void Dxyz_AreOmitted_WhenDirectionIsNotVector()
        {
            var model = SampleModels.Build();
            Assert.Contains("\"dy\": 0.6", model.ToJson());

            model.AreaLoad("A3").Direction = LoadDirection.Z;
            model.AreaLoad("A3").Dx = model.AreaLoad("A3").Dy = model.AreaLoad("A3").Dz = null;

            // Scoped to the loads array: the root gravity block spells its direction
            // with the same three component names on purpose — "a direction as three
            // components" is said one way in the format — so the whole document has
            // a dx/dy/dz in it whatever any load does.
            string json = LoadsSectionOf(model.ToJson());
            Assert.DoesNotContain("\"dx\"", json);
            Assert.DoesNotContain("\"dy\"", json);
            Assert.DoesNotContain("\"dz\"", json);
        }

        /// <summary>The <c>"loads"</c> array's text, from its key to the one after it.</summary>
        private static string LoadsSectionOf(string json)
        {
            int start = json.IndexOf("\"loads\":", StringComparison.Ordinal);
            Assert.True(start >= 0, "No loads array in the document.");

            int end = json.IndexOf("\"loadCombinations\":", start, StringComparison.Ordinal);
            Assert.True(end > start, "No loadCombinations key after the loads array.");

            return json[start..end];
        }

        [Fact]
        public void SchemaVersion_IsTheFirstKey()
        {
            string json = SampleModels.Build().ToJson();

            Assert.StartsWith(
                "{" + Environment.NewLine +
                "  \"schemaVersion\": \"" + FemexModel.CurrentSchemaVersion + "\",",
                json);
        }

        [Fact]
        public void ToJson_StampsAnUnversionedModel()
        {
            var model = new FemexModel();
            Assert.Null(model.SchemaVersion);

            model.ToJson();

            // The one deliberate mutation: every file FEMEX writes is versioned,
            // including one built in memory and never read from disk.
            Assert.Equal(FemexModel.CurrentSchemaVersion, model.SchemaVersion);
        }

        [Fact]
        public void ToJson_KeepsAnUnrecognisedVersion()
        {
            var model = new FemexModel { SchemaVersion = "0.9" };

            model.ToJson();

            // "0.9" is not a version this build reads, so nothing about the model was
            // migrated and the stamp is not ours to restate. A version that *is*
            // readable is upgraded instead — see
            // SelfWeightTests.ToJson_UpgradesALegacySchemaVersionStamp.
            Assert.Equal("0.9", model.SchemaVersion);
        }

        // ----- Bar local axes -----

        [Fact]
        public void BarLocalAxes_HorizontalBeam_HasYUpAndZHorizontal()
        {
            var model = SampleModels.Build();

            // Along +X on the first floor, from the column top to the slab's far
            // corner, with no roll.
            const int beamId = 2;
            model.Bars.Add(new Bar(beamId, startNodeId: 2, endNodeId: 12, sectionId: 1, materialId: 1));

            Assert.True(model.TryGetBarLocalAxes(beamId, out Vector3d x, out Vector3d y, out Vector3d z));

            AssertVector(new Vector3d(1.0, 0.0, 0.0), x);
            AssertVector(new Vector3d(0.0, 0.0, 1.0), y);   // in the vertical plane, upward
            AssertVector(new Vector3d(0.0, -1.0, 0.0), z);  // horizontal
        }

        [Fact]
        public void BarLocalAxes_VerticalColumn_UsesTheSubstitution()
        {
            var model = SampleModels.Build();
            model.Column().RotationAngle = 0.0;

            Assert.True(model.TryGetBarLocalAxes(SampleModels.BarId, out Vector3d x, out Vector3d y, out Vector3d z));

            AssertVector(new Vector3d(0.0, 0.0, 1.0), x);
            AssertVector(new Vector3d(1.0, 0.0, 0.0), y);   // global +X
            AssertVector(new Vector3d(0.0, 1.0, 0.0), z);   // global +Y
        }

        [Fact]
        public void BarLocalAxes_RotationAngle_RollsYOntoZ()
        {
            var model = SampleModels.Build();
            model.Column().RotationAngle = 90.0;

            Assert.True(model.TryGetBarLocalAxes(SampleModels.BarId, out Vector3d x, out Vector3d y, out Vector3d z));

            // A right-hand quarter turn about local x, which for this column is
            // global +Z: y lands on where z was, and z on where -y was.
            AssertVector(new Vector3d(0.0, 0.0, 1.0), x);
            AssertVector(new Vector3d(0.0, 1.0, 0.0), y);
            AssertVector(new Vector3d(-1.0, 0.0, 0.0), z);
        }

        [Fact]
        public void BarLocalAxes_AreRightHanded_ForABarDrawnDownward()
        {
            var model = SampleModels.Build();

            // The same column the other way up. Global +Y is no longer local z —
            // it cannot be and the triad stay right-handed — but x cross y is.
            const int hangerId = 2;
            model.Bars.Add(new Bar(hangerId, startNodeId: 2, endNodeId: 1, sectionId: 1, materialId: 1));

            Assert.True(model.TryGetBarLocalAxes(hangerId, out Vector3d x, out Vector3d y, out Vector3d z));

            AssertVector(new Vector3d(0.0, 0.0, -1.0), x);
            AssertVector(new Vector3d(1.0, 0.0, 0.0), y);
            AssertVector(x.Cross(y), z);
        }

        [Fact]
        public void BarLocalAxes_AreNotFound_ForAnUnknownBar()
        {
            var model = SampleModels.Build();

            Assert.False(model.TryGetBarLocalAxes(99, out _, out _, out _));
        }

        // ----- Plate local axes -----

        [Fact]
        public void PlateLocalAxes_HorizontalSlab_HasNormalUp_AndAnAngledX()
        {
            var model = SampleModels.Build();

            Assert.True(model.TryGetPlateLocalAxes(SampleModels.SlabId, out Vector3d x, out Vector3d y, out Vector3d z));

            // The contour 2, 12, 13, 14 runs counter-clockwise seen from above.
            AssertVector(new Vector3d(0.0, 0.0, 1.0), z);

            // Unrotated local x runs 2 -> 12, which is +X; the sample's 15-degree
            // angle then turns it counter-clockwise about +Z.
            double radians = 15.0 * Math.PI / 180.0;
            AssertVector(new Vector3d(Math.Cos(radians), Math.Sin(radians), 0.0), x);
            AssertVector(new Vector3d(-Math.Sin(radians), Math.Cos(radians), 0.0), y);
        }

        [Fact]
        public void PlateLocalAxes_Wall_HasAHorizontalNormal()
        {
            var model = SampleModels.Build();

            Assert.True(model.TryGetPlateLocalAxes(SampleModels.WallId, out Vector3d x, out Vector3d y, out Vector3d z));

            // The wall stands in the y = 0 plane; its contour 1, 42, 12, 2 winds so
            // that the normal faces -Y.
            AssertVector(new Vector3d(0.0, -1.0, 0.0), z);
            AssertVector(new Vector3d(1.0, 0.0, 0.0), x);
            AssertVector(new Vector3d(0.0, 0.0, 1.0), y);
        }

        [Fact]
        public void PlateLocalAxes_ReverseTheContour_ReversesTheNormal()
        {
            var model = SampleModels.Build();
            model.Slab().NodeIds.Reverse();

            Assert.True(model.TryGetPlateLocalAxes(SampleModels.SlabId, out _, out _, out Vector3d z));

            AssertVector(new Vector3d(0.0, 0.0, -1.0), z);
        }

        // ----- Resolving a load direction -----

        [Fact]
        public void LoadDirection_GlobalZ_IsStraightUp()
        {
            var model = SampleModels.Build();

            Assert.True(model.TryGetLoadDirection(model.AreaLoad("A1"), out Vector3d direction));

            // And the load's -2.0 magnitude therefore acts downward, which is the
            // whole of the sign convention.
            AssertVector(new Vector3d(0.0, 0.0, 1.0), direction);
            Assert.True(model.AreaLoad("A1").Magnitude * direction.Z < 0.0);
        }

        [Fact]
        public void LoadDirection_LocalZOnAWall_IsTheWallNormal()
        {
            var model = SampleModels.Build();

            Assert.True(model.TryGetLoadDirection(model.AreaLoad("A2"), out Vector3d direction));

            AssertVector(new Vector3d(0.0, -1.0, 0.0), direction);
        }

        [Fact]
        public void LoadDirection_Vector_IsNormalized()
        {
            var model = SampleModels.Build();

            Assert.True(model.TryGetLoadDirection(model.AreaLoad("A3"), out Vector3d direction));

            AssertVector(new Vector3d(0.0, 0.6, -0.8), direction);
            Assert.Equal(1.0, direction.Length, Places);
        }

        [Fact]
        public void LoadDirection_LocalOnABar_FollowsItsRoll()
        {
            var model = SampleModels.Build();

            // L2 is local +y on the column, which is rolled 30 degrees off global
            // +X. The load follows the beam, which is the whole reason barId exists.
            Assert.True(model.TryGetLoadDirection(model.LinearLoad("L2"), out Vector3d direction));

            double radians = 30.0 * Math.PI / 180.0;
            AssertVector(new Vector3d(Math.Cos(radians), Math.Sin(radians), 0.0), direction);
        }

        [Fact]
        public void LoadDirection_FreePolygon_UsesItsOwnContour()
        {
            var model = SampleModels.Build();
            var load = model.AreaLoad("A2");

            // The same four wall corners as a free polygon, with no host plate.
            load.PlateId = null;
            load.NodeSequence = new List<int> { 1, 42, 12, 2 };

            Assert.True(model.TryGetLoadDirection(load, out Vector3d direction));

            AssertVector(new Vector3d(0.0, -1.0, 0.0), direction);
            Assert.Empty(model.Validate());
        }

        [Fact]
        public void LoadDirection_IsNotFound_ForALoadThatCarriesNone()
        {
            var model = SampleModels.Build();

            // A point load already says which way it points, in its own fields.
            Assert.False(model.TryGetLoadDirection(model.Loads.Single(l => l.Label == "P1"), out _));
            Assert.False(model.TryGetLoadDirection(model.Loads.Single(l => l.Label == "T1"), out _));
        }

        // ----- Validation: errors -----

        [Fact]
        public void Reports_LocalLineLoadWithNoBar()
        {
            var model = SampleModels.Build();
            model.LinearLoad("L2").BarId = null;

            AssertReportsError(model, "has a local direction but names neither a bar nor a plate");
        }

        [Fact]
        public void Reports_UnknownBarOnALineLoad()
        {
            var model = SampleModels.Build();
            model.LinearLoad("L2").BarId = 99;

            AssertReportsError(model, "Linear load 'L2' references unknown bar 99.");
        }

        [Fact]
        public void Reports_LineLoadWhoseBarIsAPlate()
        {
            var model = SampleModels.Build();
            model.LinearLoad("L2").BarId = SampleModels.SlabId;

            AssertReportsError(model, $"names element {SampleModels.SlabId} as its bar, but that element is not a bar");
        }

        [Fact]
        public void Reports_VectorDirectionWithAMissingComponent()
        {
            var model = SampleModels.Build();
            model.AreaLoad("A3").Dy = null;

            AssertReportsError(model, "has direction Vector but does not set all of dx, dy and dz");
        }

        [Fact]
        public void Reports_VectorDirectionThatIsAllZero()
        {
            var model = SampleModels.Build();
            var load = model.AreaLoad("A3");
            load.Dx = load.Dy = load.Dz = 0.0;

            AssertReportsError(model, "with dx, dy and dz all zero");
        }

        [Fact]
        public void Reports_ComponentsSetWithoutAVectorDirection()
        {
            var model = SampleModels.Build();
            model.AreaLoad("A1").Dz = -1.0;

            AssertReportsError(model, "sets dx/dy/dz but its direction is Z");
        }

        [Fact]
        public void Reports_ProjectedLocalLoad()
        {
            var model = SampleModels.Build();
            model.AreaLoad("A2").Projected = true;

            AssertReportsError(model, "is projected and in local coordinates");
        }

        /// <summary>
        /// 1.11 is additive, so a 1.10 file is read exactly as it was written and
        /// re-emitted byte for byte — apart from the version stamp, which
        /// <see cref="FemexModel.ToJson"/> restates deliberately and argues for at
        /// length. Asserting the whole text rather than a property is what makes
        /// this evidence: any member read differently would show up here.
        /// </summary>
        [Fact]
        public void AFileWrittenAt110_ReadsUnchanged_AndOnlyItsVersionStampMoves()
        {
            string current = File.ReadAllText(
                Path.Combine(AppContext.BaseDirectory, "Examples", "Example1.femex"));
            string older = current.Replace("\"schemaVersion\": \"1.11\"",
                                           "\"schemaVersion\": \"1.10\"");

            Assert.NotEqual(current, older);

            FemexModel model = FemexModel.FromJson(older);

            Assert.Empty(model.Validate(ValidationSeverity.Error));
            Assert.Contains(model.Validate(ValidationSeverity.Warning),
                            m => m.Text.Contains("schemaVersion \"1.10\", written before a line load"));
            Assert.Equal(current, model.ToJson());
        }

        // ----- 1.11: a line load on a plate contour edge -----

        /// <summary>
        /// A load on a panel edge in the panel edge's own frame — and the assertion
        /// that matters is <i>which</i> frame: the same one
        /// <see cref="griffel_femex.BoundaryConditions.Hinge"/> states for a hinge on
        /// that edge, taken from the same call. Two conventions for one edge is the
        /// failure this is written to prevent.
        /// </summary>
        [Fact]
        public void LineLoadOnAPlateEdge_ResolvesInTheEdgeFrame_TheSameOneAHingeUses()
        {
            var model = EdgeHosted();

            Assert.True(model.TryGetLoadDirection(model.LinearLoad("L2"), out Vector3d direction));
            Assert.True(model.TryGetEdgeLocalAxes(SampleModels.SlabId, 2, 12,
                                                  out Vector3d _, out Vector3d y, out Vector3d _));

            // L2's direction is local Y.
            AssertVector(y, direction);
        }

        [Fact]
        public void WritingTheSameEdgeBackwards_ReversesTheFrame()
        {
            var model = EdgeHosted();
            Assert.True(model.TryGetLoadDirection(model.LinearLoad("L2"), out Vector3d forward));

            var load = model.LinearLoad("L2");
            (load.StartNode, load.EndNode) = (load.EndNode, load.StartNode);

            Assert.True(model.TryGetLoadDirection(load, out Vector3d backward));
            AssertVector(forward * -1.0, backward);
        }

        [Fact]
        public void AnEdgeHostedLoad_MayStateAPartialExtent_WithNoBarAnywhere()
        {
            var model = EdgeHosted();
            var load = model.LinearLoad("L2");
            load.StartPosition = 0.25;
            load.EndPosition = 0.75;

            Assert.Empty(model.Validate(ValidationSeverity.Error));
        }

        [Fact]
        public void Reports_LineLoadNamingBothABarAndAPlate()
        {
            var model = EdgeHosted();
            model.LinearLoad("L2").BarId = SampleModels.BarId;

            AssertReportsError(model, "names both bar 1 and plate 10");
        }

        [Fact]
        public void Reports_LineLoadOnAPlateThatIsNotThere()
        {
            var model = EdgeHosted();
            model.LinearLoad("L2").PlateId = 4242;

            AssertReportsError(model, "references unknown plate 4242");
        }

        [Fact]
        public void Reports_LineLoadNamingAnElementThatIsNotAPlate()
        {
            var model = EdgeHosted();
            model.LinearLoad("L2").PlateId = SampleModels.BarId;

            AssertReportsError(model, "as its plate, but that element is not a plate");
        }

        /// <summary>
        /// The rule the hinge already keeps, applied to a load: two nodes that are not
        /// one edge of the named contour name no edge at all, and the frame the load
        /// is stated in cannot be found.
        /// </summary>
        [Fact]
        public void Reports_TwoNodesThatAreNotAnEdgeOfTheNamedContour()
        {
            var model = EdgeHosted();
            model.LinearLoad("L2").EndNode = 13;

            AssertReportsError(model, "edge 2->13, but those nodes are not adjacent in the contour");
        }

        [Fact]
        public void Reports_ARegionTheNamedPlateDoesNotHave()
        {
            var model = EdgeHosted();
            model.LinearLoad("L2").RegionId = 99;

            AssertReportsError(model, "references region 99, which does not exist on plate 10");
        }

        [Fact]
        public void Reports_ARegionWithNoPlate()
        {
            var model = EdgeHosted();
            var load = model.LinearLoad("L2");
            load.PlateId = null;
            load.RegionId = SampleModels.VoidRegionId;

            AssertReportsError(model, "sets regionId without plateId");
        }

        /// <summary>
        /// The edge of the slab's <i>void</i>, which is a contour of its own: the
        /// nodes have to be adjacent in the region rather than in the plate.
        /// </summary>
        [Fact]
        public void ALoadOnARegionsEdge_IsCheckedAgainstThatRegionsContour()
        {
            var model = EdgeHosted();
            var load = model.LinearLoad("L2");
            load.RegionId = SampleModels.VoidRegionId;
            load.StartNode = 31;
            load.EndNode = 32;

            Assert.Empty(model.Validate(ValidationSeverity.Error));

            load.EndNode = 33;
            AssertReportsError(model, "not adjacent in region 2's contour");
        }

        /// <summary>
        /// L2 moved off its bar and onto the slab's first contour edge. Everything
        /// else about the load — local coordinates, direction Y — is left alone, so
        /// what changes between these tests is only the host.
        /// </summary>
        private static FemexModel EdgeHosted()
        {
            var model = SampleModels.Build();
            var load = model.LinearLoad("L2");

            load.BarId = null;
            load.PlateId = SampleModels.SlabId;
            load.StartNode = 2;
            load.EndNode = 12;

            return model;
        }

        // ----- Validation: warnings -----

        [Fact]
        public void Warns_ProjectedLoadWhoseDirectionLiesInThePlatesPlane()
        {
            var model = SampleModels.Build();

            // A projected gravity load on the wall, which is vertical: its plan
            // projection is a line, so the area is zero and the load means nothing.
            var load = model.AreaLoad("A2");
            load.CoordinateSystem = LoadCoordinateSystem.Global;
            load.Direction = LoadDirection.Z;
            load.Projected = true;

            AssertReports(model, ValidationSeverity.Warning, "lies in the loaded surface's plane");
            Assert.Empty(model.Validate(ValidationSeverity.Error));
        }

        [Fact]
        public void Warns_ProjectedLoadRunningAlongItsOwnLine()
        {
            var model = SampleModels.Build();

            // L1 spans the column, which is vertical, so a global Z load along it
            // projects to nothing.
            model.LinearLoad("L1").Projected = true;

            AssertReports(model, ValidationSeverity.Warning, "runs along the loaded line");
        }

        [Fact]
        public void Accepts_AProjectedLoadThatProjectsToSomething()
        {
            var model = SampleModels.Build();

            // Global Z on the horizontal slab: projected and real area agree, which
            // is legal and common — it is only a zero projection that is suspect.
            model.AreaLoad("A1").Projected = true;

            Assert.Empty(model.Validate());
        }

        [Fact]
        public void Warns_UnrecognisedSchemaVersion()
        {
            var model = SampleModels.Build();
            model.SchemaVersion = "2.0";

            AssertReports(model, ValidationSeverity.Warning, "declares schemaVersion \"2.0\"");
            Assert.Empty(model.Validate(ValidationSeverity.Error));
        }

        // ----- 1.8 -> 1.9: the thermal gradient acquires a sign convention -----

        [Fact]
        public void ALegacyGradient_IsReadAsGradientZ_AndTheOldKeyCannotBeWritten()
        {
            // The getter-less shim, asserted in both directions: the value lands in
            // the typed property, and no 1.9 file can carry the old spelling —
            // the same contract Material.unitWeight has held since 1.2.
            var model = FemexModel.FromJson("""
                {
                  "schemaVersion": "1.8",
                  "loads": [
                    { "type": "temperature", "id": 1, "label": "T1", "elementIds": [ 1 ],
                      "deltaT": 20, "gradientPerDepth": 30 }
                  ]
                }
                """);

            var load = Assert.IsType<TemperatureLoad>(Assert.Single(model.Loads));
            Assert.Equal(30.0, load.GradientZ);
            Assert.Null(load.GradientY);

            string json = model.ToJson();
            Assert.Contains("\"gradientZ\": 30", json);
            Assert.DoesNotContain("gradientPerDepth", json);
        }

        [Fact]
        public void ALegacyGradient_IsReportedAsAReinterpretation_NotARename()
        {
            // The word is the point. 1.8's units change dropped text that named no
            // unit and said so; this one keeps a number and changes what it means,
            // and which face of the element is the hot one decides which way the
            // element curves. A reader has to be told to confirm the sign.
            var model = FemexModel.FromJson("""
                {
                  "schemaVersion": "1.8",
                  "loads": [
                    { "type": "temperature", "id": 1, "label": "T1", "elementIds": [ 1 ],
                      "deltaT": 20, "gradientPerDepth": 30 }
                  ]
                }
                """);

            AssertReports(model, ValidationSeverity.Warning,
                          "a reinterpretation rather than a rename");
            AssertReports(model, ValidationSeverity.Warning, "Confirm the sign.");
        }

        [Fact]
        public void AMigratedGradient_DoesNotReportTwice()
        {
            // A property of the read and not of the model: the re-emitted file is
            // 1.9 and carries gradientZ, so it has nothing left to migrate.
            var once = FemexModel.FromJson("""
                {
                  "schemaVersion": "1.8",
                  "loads": [
                    { "type": "temperature", "id": 1, "label": "T1", "elementIds": [ 1 ],
                      "deltaT": 20, "gradientPerDepth": 30 }
                  ]
                }
                """);

            var twice = FemexModel.FromJson(once.ToJson());

            Assert.DoesNotContain(twice.Validate(), m => m.Text.Contains("reinterpretation"));
        }

        [Fact]
        public void ALoadStatingBothSpellings_KeepsTheTypedOne_AndSaysSo()
        {
            // The rule MigrateLegacyUnitWeight and MigrateLegacyUnits already
            // apply: the two cannot both be right and the newer one wins, reported
            // rather than preferred in silence.
            var model = FemexModel.FromJson("""
                {
                  "schemaVersion": "1.8",
                  "loads": [
                    { "type": "temperature", "id": 1, "label": "T1", "elementIds": [ 1 ],
                      "deltaT": 20, "gradientPerDepth": 30, "gradientZ": -30 }
                  ]
                }
                """);

            Assert.Equal(-30.0, Assert.IsType<TemperatureLoad>(model.Loads[0]).GradientZ);
            AssertReports(model, ValidationSeverity.Warning,
                          "carries both a gradientPerDepth and a gradientZ");
        }

        [Fact]
        public void BothGradients_RoundTrip_AndAModelWithoutThem_OmitsTheKeysEntirely()
        {
            var model = SampleModels.Build();
            var thermal = model.Loads.OfType<TemperatureLoad>().Single();

            thermal.GradientZ = null;
            Assert.DoesNotContain("\"gradient", model.ToJson());

            thermal.GradientY = -2.5;
            thermal.GradientZ = 5.0;

            var restored = FemexModel.FromJson(model.ToJson()).Loads.OfType<TemperatureLoad>().Single();

            Assert.Equal(-2.5, restored.GradientY);
            Assert.Equal(5.0, restored.GradientZ);
        }

        [Fact]
        public void Warns_GradientYOnASurface()
        {
            // A surface has one through-thickness axis, its local z. A warning and
            // not a schema rule, because one load may name bars and plates together
            // and the same field is right for half of them.
            var model = SampleModels.Build();
            var thermal = model.Loads.OfType<TemperatureLoad>().Single();

            thermal.GradientY = 3.0;
            thermal.ElementIds.Add(SampleModels.SlabId);

            AssertReports(model, ValidationSeverity.Warning,
                          $"states a gradientY and acts on surface elements {SampleModels.SlabId}");
        }

        [Fact]
        public void Accepts_GradientYOnABar()
        {
            var model = SampleModels.Build();
            model.Loads.OfType<TemperatureLoad>().Single().GradientY = 3.0;

            Assert.Empty(model.Validate());
        }

        // ----- The reference file -----

        [Fact]
        public void Example1_CarriesASignedGradient_HandMigrated()
        {
            // Hand-migrated at this bump rather than machine-migrated, and the sign
            // was decided rather than carried across. The load is "Roof slab
            // cooling" on the mesh faces of plate 3004, whose contour runs
            // anticlockwise seen from above, so its local +z points up. A roof
            // losing heat from its exposed top face against a warmer soffit has a
            // temperature that *falls* along +z, so the 1.6 magnitude of 30 is
            // written as -30. A file left carrying the old key would warn on every
            // load and would fail byte identity.
            string path = Path.Combine(AppContext.BaseDirectory, "Examples", "Example1.femex");
            var model = FemexModel.Load(path);

            var roof = model.Loads.OfType<TemperatureLoad>().Single(l => l.Label == "Roof slab cooling");

            Assert.Equal(-12.0, roof.DeltaT);
            Assert.Equal(-30.0, roof.GradientZ);
            Assert.Null(roof.GradientY);
            Assert.DoesNotContain("gradientPerDepth", File.ReadAllText(path));
        }

        [Fact]
        public void Example1_GravityLoadsResolveDownward()
        {
            string path = Path.Combine(AppContext.BaseDirectory, "Examples", "Example1.femex");
            var model = FemexModel.Load(path);

            // The migration's sign check, kept as an assertion rather than done once
            // by hand: this is what says the re-signing was right rather than merely
            // consistent. Every dead and live load in the file resolves to a force
            // pointing straight down.
            var gravity = model.Loads
                .OfType<DistributedLoad>()
                .Where(l => l.LoadCaseNumber is 1 or 2)
                .ToList();

            // Case 6 is the file's self-weight case and carries no loads of its own:
            // self-weight is a property of the case, not an entry in this array, so
            // adding it disturbed nothing here.
            Assert.Equal(64, gravity.Count);

            foreach (var load in gravity)
            {
                Assert.True(model.TryGetLoadDirection(load, out Vector3d direction));
                AssertVector(new Vector3d(0.0, 0.0, 1.0), direction);

                double magnitude = load switch
                {
                    AreaLoad area => area.Magnitude,
                    LinearLoad line => line.MagnitudeStart,
                    _ => 0.0,
                };

                Assert.True(magnitude < 0.0, $"'{load.Label}' is not downward.");
            }

            // And the wind panel, the one load in the file that is not global.
            var wind = model.Loads.OfType<AreaLoad>().Single(l => l.PlateId == 4101);
            Assert.Equal(LoadCoordinateSystem.Local, wind.CoordinateSystem);
            Assert.True(model.TryGetLoadDirection(wind, out Vector3d normal));
            AssertVector(new Vector3d(1.0, 0.0, 0.0), normal);
            Assert.True(wind.Magnitude > 0.0);
        }
    }
}
